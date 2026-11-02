using System.Globalization;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;
using Figure = KetCauVXT.Drawing.Figure;
using FixedDocument = System.Windows.Documents.FixedDocument;
using FixedPage = System.Windows.Documents.FixedPage;
using PageContent = System.Windows.Documents.PageContent;

namespace KetCauVXT.App;

/// <summary>Cửa sổ dàn trang: danh sách tờ, trang giấy (kéo/đổi cỡ khung nhìn), bảng thêm khung nhìn,
/// in (PrintDialog – có thể chọn "Microsoft Print to PDF"), xuất PDF/XPS và DXF từng tờ.</summary>
public partial class SheetWindow : Window
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const string AllText = "(tất cả)";

    private readonly Project _p;
    private readonly AnalysisResults _r;
    private readonly DesignSummary _d;
    private readonly Action _markDirty;

    private readonly Dictionary<string, List<Figure>> _figCache = new();
    private readonly Dictionary<DxfWriter, List<DrawPrim>> _primCache = new();

    private Sheet _sheet;
    private SheetView _sel;
    private bool _loading;

    // hiển thị: điểm giấy (mm, y xuống) → màn hình: s = z·p + pan
    private double _zoom = 1, _panX = 20, _panY = 20;
    private bool _autoFit = true;
    private readonly DispatcherTimer _redrawTimer;

    // phần tử trên trang
    private readonly Dictionary<SheetView, (DrawingHost Host, Shapes.Rectangle Outline)> _hosts = new();
    private Shapes.Rectangle _paper, _selRect, _handle;
    private DrawingHost _tbHost;
    private static readonly Effect _glow = MakeGlow();

    private static Effect MakeGlow()
    {
        var e = new DropShadowEffect { Color = Colors.Yellow, BlurRadius = 10, ShadowDepth = 0, Opacity = 1 };
        e.Freeze();
        return e;
    }

    private enum DragMode { None, Move, Resize, Pan }
    private DragMode _drag;
    private Point _dragStart;
    private double _ox, _oy, _ow, _oh, _opx, _opy;
    private bool _dragMoved;

    public SheetWindow(Project p, AnalysisResults r, DesignSummary d, Action markDirty)
    {
        InitializeComponent();
        _p = p ?? throw new ArgumentNullException(nameof(p));
        _r = r; _d = d; _markDirty = markDirty;
        _p.Sheets ??= new List<Sheet>();
        _redrawTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _redrawTimer.Tick += (_, _) => { _redrawTimer.Stop(); RebuildPage(); };

        foreach (var k in SheetBuilder.PaletteKinds) cbKind.Items.Add(new ComboBoxItem { Content = SheetBuilder.KindName(k), Tag = k });
        cbKind.SelectedIndex = 0;
        if (_d == null)
        {
            txtHint.Text = "Chưa có kết quả \"Thiết kế BTCT\": chi tiết dầm, cột, móng và bảng thống kê thép sẽ trống. Chạy Phân tích + Thiết kế BTCT rồi mở lại cửa sổ này.";
            txtHint.Visibility = Visibility.Visible;
        }
        else if (_r == null)
        {
            txtHint.Text = "Chưa có kết quả phân tích: biểu đồ nội lực sẽ trống.";
            txtHint.Visibility = Visibility.Visible;
        }
        RefreshSheetList(_p.Sheets.Count > 0 ? 0 : -1);
        SetStatus(_p.Sheets.Count == 0 ? "Chưa có tờ nào – bấm \"Tạo bộ bản vẽ mặc định\" hoặc \"Thêm\"." : $"{_p.Sheets.Count} tờ bản vẽ.");
    }

    // ==================================================================
    // Bộ đệm hình
    // ==================================================================
    private List<Figure> FiguresOf(SheetView v)
    {
        string key = v.Kind + "|" + v.Param;
        if (!_figCache.TryGetValue(key, out var f))
        {
            f = SheetBuilder.Figures(_p, _r, _d, v);
            _figCache[key] = f;
        }
        return f;
    }

    private List<DrawPrim> PrimsOf(DxfWriter w)
    {
        if (!_primCache.TryGetValue(w, out var l)) { l = w.GetPrimitives(); _primCache[w] = l; }
        return l;
    }

    private void Dirty() => _markDirty?.Invoke();
    private void SetStatus(string s) => txtStatus.Text = s;

    // ==================================================================
    // Danh sách tờ
    // ==================================================================
    private static string SheetText(Sheet s) => $"{s.Number} – {s.Name}  [{s.Paper} {(s.Landscape ? "ngang" : "dọc")}, {s.Views.Count} hình]";

    private void RefreshSheetList(int select)
    {
        _loading = true;
        lstSheets.Items.Clear();
        foreach (var s in _p.Sheets) lstSheets.Items.Add(SheetText(s));
        _loading = false;
        if (select >= 0 && select < _p.Sheets.Count) lstSheets.SelectedIndex = select;
        else { _sheet = null; LoadSheetProps(); RebuildPage(); }
    }

    private void UpdateSheetListText()
    {
        int i = _sheet == null ? -1 : _p.Sheets.IndexOf(_sheet);
        if (i < 0 || i >= lstSheets.Items.Count) return;
        _loading = true;
        bool wasSel = lstSheets.SelectedItems.Contains(lstSheets.Items[i]);
        lstSheets.Items[i] = SheetText(_sheet);
        if (wasSel) lstSheets.SelectedItems.Add(lstSheets.Items[i]);
        _loading = false;
    }

    /// <summary>Chọn tờ thứ i (dùng cho tự kiểm tra).</summary>
    public void SelectSheet(int i)
    {
        if (i >= 0 && i < _p.Sheets.Count) { lstSheets.SelectedIndex = i; _autoFit = true; FitPage(); }
    }

    private void LstSheets_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        int i = lstSheets.SelectedIndex;
        var s = i >= 0 && i < _p.Sheets.Count ? _p.Sheets[i] : null;
        if (ReferenceEquals(s, _sheet)) return;
        _sheet = s;
        _sel = null;
        LoadSheetProps();
        _autoFit = true;
        RebuildPage();
        FitPage();
    }

    private void LoadSheetProps()
    {
        _loading = true;
        var s = _sheet;
        txtNumber.Text = s?.Number ?? "";
        txtName.Text = s?.Name ?? "";
        cbPaper.SelectedIndex = s == null ? -1 : Math.Max(0, Array.IndexOf(SheetBuilder.Papers, s.Paper));
        cbOrient.SelectedIndex = s == null ? -1 : (s.Landscape ? 0 : 1);
        txtDesigner.Text = s?.Designer ?? "";
        txtChecker.Text = s?.Checker ?? "";
        txtDate.Text = s?.Date ?? "";
        _loading = false;
        RefreshViewList();
    }

    private void SheetProp_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || _sheet == null) return;
        var oldPaper = _sheet.PaperSize();
        _sheet.Number = txtNumber.Text;
        _sheet.Name = txtName.Text;
        if (cbPaper.SelectedIndex >= 0) _sheet.Paper = SheetBuilder.Papers[cbPaper.SelectedIndex];
        if (cbOrient.SelectedIndex >= 0) _sheet.Landscape = cbOrient.SelectedIndex == 0;
        _sheet.Designer = txtDesigner.Text;
        _sheet.Checker = txtChecker.Text;
        _sheet.Date = txtDate.Text;
        Dirty();
        UpdateSheetListText();
        bool paperChanged = oldPaper != _sheet.PaperSize();
        if (paperChanged)
        {
            // giữ khung nhìn trong giấy mới
            var (pw, ph) = _sheet.PaperSize();
            foreach (var v in _sheet.Views)
            {
                v.W = Math.Min(v.W, pw); v.H = Math.Min(v.H, ph);
                v.X = Math.Clamp(v.X, 0, pw - v.W); v.Y = Math.Clamp(v.Y, 0, ph - v.H);
            }
            RebuildPage(); FitPage();
        }
        else RebuildTitleBlock();
    }

    private string NextNumber()
    {
        int max = 0;
        foreach (var s in _p.Sheets)
        {
            var digits = new string((s.Number ?? "").SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int n)) max = Math.Max(max, n);
        }
        return "KC-" + (max + 1).ToString("00", Inv);
    }

    private void BtnAddSheet_Click(object sender, RoutedEventArgs e)
    {
        var s = new Sheet
        {
            Number = NextNumber(), Name = "BẢN VẼ MỚI", Paper = _sheet?.Paper ?? "A3", Landscape = _sheet?.Landscape ?? true,
            Designer = _p.Designer ?? "", Date = DateTime.Today.ToString("dd/MM/yyyy", Inv)
        };
        int at = _sheet == null ? _p.Sheets.Count : _p.Sheets.IndexOf(_sheet) + 1;
        _p.Sheets.Insert(at, s);
        Dirty();
        RefreshSheetList(at);
    }

    private void BtnDupSheet_Click(object sender, RoutedEventArgs e)
    {
        if (_sheet == null) return;
        var s = SheetBuilder.Clone(_sheet);
        s.Number = NextNumber();
        int at = _p.Sheets.IndexOf(_sheet) + 1;
        _p.Sheets.Insert(at, s);
        Dirty();
        RefreshSheetList(at);
    }

    private void BtnDelSheet_Click(object sender, RoutedEventArgs e)
    {
        var sel = SelectedSheets(true);
        if (sel.Count == 0) return;
        if (MessageBox.Show($"Xoá {sel.Count} tờ bản vẽ đã chọn?", Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        int i = _p.Sheets.IndexOf(sel[0]);
        foreach (var s in sel) _p.Sheets.Remove(s);
        _sheet = null;
        Dirty();
        RefreshSheetList(Math.Min(i, _p.Sheets.Count - 1));
    }

    private void MoveSheet(int d)
    {
        if (_sheet == null) return;
        int i = _p.Sheets.IndexOf(_sheet), j = i + d;
        if (i < 0 || j < 0 || j >= _p.Sheets.Count) return;
        (_p.Sheets[i], _p.Sheets[j]) = (_p.Sheets[j], _p.Sheets[i]);
        Dirty();
        var keep = _sheet;
        _sheet = null;
        RefreshSheetList(j);
        _sheet = keep;
    }

    private void BtnSheetUp_Click(object sender, RoutedEventArgs e) => MoveSheet(-1);
    private void BtnSheetDown_Click(object sender, RoutedEventArgs e) => MoveSheet(1);

    private void BtnDefaultSet_Click(object sender, RoutedEventArgs e)
    {
        bool replace = true;
        if (_p.Sheets.Count > 0)
        {
            var a = MessageBox.Show("Đã có tờ bản vẽ.\nYes: thay thế toàn bộ bằng bộ mặc định\nNo: thêm bộ mặc định vào cuối danh sách",
                Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (a == MessageBoxResult.Cancel) return;
            replace = a == MessageBoxResult.Yes;
        }
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var set = SheetBuilder.DefaultSet(_p, _d, _r);
            if (replace) _p.Sheets.Clear();
            int first = _p.Sheets.Count;
            _p.Sheets.AddRange(set);
            Dirty();
            _sheet = null;
            RefreshSheetList(first < _p.Sheets.Count ? first : -1);
            SetStatus($"Đã tạo {set.Count} tờ bản vẽ mặc định." + (_d == null ? " (Chưa thiết kế BTCT: chưa có tờ dầm, cột, móng, thống kê thép.)" : ""));
        }
        catch (Exception ex) { MessageBox.Show("Không tạo được bộ bản vẽ: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { Mouse.OverrideCursor = null; }
    }

    private List<Sheet> SelectedSheets(bool selectionOnly)
    {
        if (!selectionOnly) return _p.Sheets.ToList();
        var idx = lstSheets.SelectedItems.Cast<object>().Select(o => lstSheets.Items.IndexOf(o)).Where(i => i >= 0).Distinct().OrderBy(i => i);
        var l = idx.Where(i => i < _p.Sheets.Count).Select(i => _p.Sheets[i]).ToList();
        if (l.Count == 0 && _sheet != null) l.Add(_sheet);
        return l;
    }

    private List<Sheet> OutputSheets() => SelectedSheets(chkOnlySelected.IsChecked == true);

    // ==================================================================
    // Trang giấy
    // ==================================================================
    private double PH => _sheet?.PaperSize().H ?? 297;
    private double PW => _sheet?.PaperSize().W ?? 420;
    private double MinStroke => 0.8 / Math.Max(_zoom, 1e-6);     // ≥ 0,8 px trên màn hình

    private void ApplyTransform()
    {
        pageCanvas.RenderTransform = new MatrixTransform(_zoom, 0, 0, _zoom, _panX, _panY);
        txtZoom.Text = Math.Round(_zoom / SheetRender.DipPerMm * 100).ToString("0", Inv) + " %";
        double px = 1 / _zoom;
        foreach (var (_, o) in _hosts.Values) o.StrokeThickness = px;
        if (_paper != null) _paper.StrokeThickness = px;
        UpdateSelection();
    }

    private void FitPage()
    {
        if (_sheet == null) { ApplyTransform(); return; }
        double w = pageHost.ActualWidth, h = pageHost.ActualHeight;
        if (w < 50 || h < 50) return;
        var (pw, ph) = _sheet.PaperSize();
        _zoom = Math.Min((w - 40) / pw, (h - 40) / ph);
        _panX = (w - pw * _zoom) / 2; _panY = (h - ph * _zoom) / 2;
        _autoFit = true;
        ApplyTransform();
        _redrawTimer.Stop(); _redrawTimer.Start();
    }

    private void BtnFit_Click(object sender, RoutedEventArgs e) => FitPage();

    private void BtnZoom100_Click(object sender, RoutedEventArgs e)
    {
        double cx = pageHost.ActualWidth / 2, cy = pageHost.ActualHeight / 2;
        ZoomAt(new Point(cx, cy), SheetRender.DipPerMm / _zoom);
    }

    private void ZoomAt(Point s, double f)
    {
        double nz = Math.Clamp(_zoom * f, 0.05, 60);
        f = nz / _zoom;
        _panX = s.X - (s.X - _panX) * f;
        _panY = s.Y - (s.Y - _panY) * f;
        _zoom = nz;
        _autoFit = false;
        ApplyTransform();
        _redrawTimer.Stop(); _redrawTimer.Start();
    }

    private void PageHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_autoFit) FitPage();
    }

    private void RebuildTitleBlock()
    {
        if (_sheet == null || _tbHost == null) return;
        var lays = _sheet.Views.Select(v => (v, SheetBuilder.Layout(FiguresOf(v), v))).ToList();
        var tb = SheetBuilder.TitleBlock(_p, _sheet, SheetBuilder.ScaleText(lays));
        _tbHost.Drawing = SheetRender.Render(new[] { (tb, 1.0, 0.0, 0.0) }, 0, PH, MinStroke, w => w.GetPrimitives());
        _tbHost.InvalidateVisual();
    }

    private void RebuildPage()
    {
        pageCanvas.Children.Clear();
        _hosts.Clear();
        _paper = null; _tbHost = null; _selRect = null; _handle = null;
        if (_sheet == null) { RefreshViewList(); return; }
        var (pw, ph) = _sheet.PaperSize();
        var shadow = new Shapes.Rectangle { Width = pw, Height = ph, Fill = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), IsHitTestVisible = false };
        Canvas.SetLeft(shadow, 0.006 * Math.Max(pw, ph)); Canvas.SetTop(shadow, 0.006 * Math.Max(pw, ph));
        pageCanvas.Children.Add(shadow);
        _paper = new Shapes.Rectangle
        {
            Width = pw, Height = ph, Fill = Brushes.White, Stroke = Brushes.Black, StrokeThickness = 1 / _zoom, IsHitTestVisible = false
        };
        pageCanvas.Children.Add(_paper);
        _tbHost = new DrawingHost { Width = pw, Height = ph, IsHitTestVisible = false };
        pageCanvas.Children.Add(_tbHost);
        RebuildTitleBlock();
        foreach (var v in _sheet.Views) AddViewHost(v);

        _selRect = new Shapes.Rectangle
        {
            Stroke = Brushes.Yellow, Fill = new SolidColorBrush(Color.FromArgb(28, 255, 230, 0)), IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        _handle = new Shapes.Rectangle { Fill = Brushes.Yellow, Stroke = Brushes.Black, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        pageCanvas.Children.Add(_selRect);
        pageCanvas.Children.Add(_handle);
        ApplyTransform();
    }

    private void AddViewHost(SheetView v)
    {
        var host = new DrawingHost { IsHitTestVisible = false };
        var outline = new Shapes.Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(120, 150, 190)), StrokeDashArray = new DoubleCollection { 4, 3 },
            StrokeThickness = 1 / _zoom, IsHitTestVisible = false
        };
        int at = Math.Max(0, pageCanvas.Children.Count - (_selRect != null ? 2 : 0));
        pageCanvas.Children.Insert(at, outline);
        pageCanvas.Children.Insert(at + 1, host);
        _hosts[v] = (host, outline);
        RenderView(v);
    }

    private void RenderView(SheetView v)
    {
        if (!_hosts.TryGetValue(v, out var h)) return;
        var lay = SheetBuilder.Layout(FiguresOf(v), v);
        h.Host.Drawing = SheetRender.Render(SheetRender.ViewItems(lay), v.X, v.Y + v.H, MinStroke, PrimsOf);
        h.Host.Width = Math.Max(1, v.W); h.Host.Height = Math.Max(1, v.H);
        h.Host.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(1, v.W), Math.Max(1, v.H)));
        h.Host.InvalidateVisual();
        PositionView(v);
    }

    /// <summary>Đặt lại vị trí khung nhìn (không dựng lại hình): hình dựng theo gốc trái trên của khung.</summary>
    private void PositionView(SheetView v)
    {
        if (!_hosts.TryGetValue(v, out var h)) return;
        double top = PH - v.Y - v.H;
        Canvas.SetLeft(h.Host, v.X); Canvas.SetTop(h.Host, top);
        h.Outline.Width = Math.Max(0.1, v.W); h.Outline.Height = Math.Max(0.1, v.H);
        Canvas.SetLeft(h.Outline, v.X); Canvas.SetTop(h.Outline, top);
        if (ReferenceEquals(v, _sel)) UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (_selRect == null || _handle == null) return;
        if (_sel == null || _sheet == null || !_sheet.Views.Contains(_sel))
        {
            _selRect.Visibility = _handle.Visibility = Visibility.Collapsed;
            return;
        }
        var v = _sel;
        double px = 1 / _zoom, top = PH - v.Y - v.H;
        _selRect.StrokeThickness = 2 * px;
        // hiệu ứng sáng vàng như phần còn lại của chương trình (bỏ khi phóng quá lớn để tránh ảnh đệm khổng lồ)
        _selRect.Effect = Math.Max(v.W, v.H) * _zoom < 3000 ? _glow : null;
        _selRect.Width = Math.Max(0.1, v.W); _selRect.Height = Math.Max(0.1, v.H);
        Canvas.SetLeft(_selRect, v.X); Canvas.SetTop(_selRect, top);
        double hs = 9 * px;
        _handle.Width = _handle.Height = hs; _handle.StrokeThickness = px;
        Canvas.SetLeft(_handle, v.X + v.W - hs / 2); Canvas.SetTop(_handle, top + v.H - hs / 2);
        _selRect.Visibility = _handle.Visibility = Visibility.Visible;
    }

    // ==================================================================
    // Chuột
    // ==================================================================
    private Point ToPage(Point s) => new((s.X - _panX) / _zoom, (s.Y - _panY) / _zoom);
    private static double Snap(double x) => Math.Round(x / SheetBuilder.Snap) * SheetBuilder.Snap;

    private SheetView HitView(Point pg)
    {
        if (_sheet == null) return null;
        for (int i = _sheet.Views.Count - 1; i >= 0; i--)
        {
            var v = _sheet.Views[i];
            double top = PH - v.Y - v.H;
            if (pg.X >= v.X && pg.X <= v.X + v.W && pg.Y >= top && pg.Y <= top + v.H) return v;
        }
        return null;
    }

    private bool HitHandle(Point pg)
    {
        if (_sel == null) return false;
        double tol = 8 / _zoom;
        double hx = _sel.X + _sel.W, hy = PH - _sel.Y;
        return Math.Abs(pg.X - hx) <= tol && Math.Abs(pg.Y - hy) <= tol;
    }

    private void PageHost_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ZoomAt(e.GetPosition(pageHost), e.Delta > 0 ? 1.2 : 1 / 1.2);
        e.Handled = true;
    }

    private void PageHost_MouseDown(object sender, MouseButtonEventArgs e)
    {
        pageHost.Focus();
        var s = e.GetPosition(pageHost);
        _dragStart = s;
        _dragMoved = false;
        if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle)
        {
            _drag = DragMode.Pan; _opx = _panX; _opy = _panY;
            pageHost.CaptureMouse();
            return;
        }
        if (e.ChangedButton != MouseButton.Left || _sheet == null) return;
        var pg = ToPage(s);
        if (HitHandle(pg)) _drag = DragMode.Resize;
        else
        {
            var v = HitView(pg);
            SelectView(v);
            _drag = v != null ? DragMode.Move : DragMode.None;
        }
        if (_drag != DragMode.None && _sel != null)
        {
            _ox = _sel.X; _oy = _sel.Y; _ow = _sel.W; _oh = _sel.H;
            pageHost.CaptureMouse();
        }
    }

    private void PageHost_MouseMove(object sender, MouseEventArgs e)
    {
        var s = e.GetPosition(pageHost);
        if (_drag == DragMode.None)
        {
            if (_sheet != null)
            {
                var pg = ToPage(s);
                pageHost.Cursor = HitHandle(pg) ? Cursors.SizeNWSE : HitView(pg) != null ? Cursors.SizeAll : Cursors.Arrow;
            }
            return;
        }
        double dxs = s.X - _dragStart.X, dys = s.Y - _dragStart.Y;
        if (Math.Abs(dxs) + Math.Abs(dys) > 2) _dragMoved = true;
        if (_drag == DragMode.Pan)
        {
            _panX = _opx + dxs; _panY = _opy + dys; _autoFit = false;
            ApplyTransform();
            return;
        }
        if (_sel == null) return;
        double dx = dxs / _zoom, dy = -dys / _zoom;     // mm giấy, y lên
        var (pw, ph) = _sheet.PaperSize();
        if (_drag == DragMode.Move)
        {
            _sel.X = Math.Clamp(Snap(_ox + dx), 0, Math.Max(0, pw - _sel.W));
            _sel.Y = Math.Clamp(Snap(_oy + dy), 0, Math.Max(0, ph - _sel.H));
            PositionView(_sel);
        }
        else if (_drag == DragMode.Resize)
        {
            double top = _oy + _oh;
            double nw = Math.Clamp(Snap(_ox + _ow + dx) - _ox, 20, pw - _ox);
            double ny = Math.Clamp(Snap(_oy + dy), 0, top - 20);
            _sel.W = nw; _sel.Y = ny; _sel.H = top - ny;
            PositionView(_sel);
            if (_hosts.TryGetValue(_sel, out var h))
            {
                h.Host.Clip = new RectangleGeometry(new Rect(0, 0, _sel.W, _sel.H));
                // hình được dựng theo gốc trái trên: khi đổi cỡ thì xem trước hình cũ, dựng lại khi thả chuột
            }
        }
        LoadViewProps(true);
    }

    private void PageHost_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_drag == DragMode.None) return;
        var mode = _drag;
        _drag = DragMode.None;
        pageHost.ReleaseMouseCapture();
        if (mode == DragMode.Pan)
        {
            if (!_dragMoved && e.ChangedButton == MouseButton.Right) { /* nhấp phải không kéo: không làm gì */ }
            _redrawTimer.Stop(); _redrawTimer.Start();
            return;
        }
        if (_sel == null || !_dragMoved) return;
        if (mode == DragMode.Resize || mode == DragMode.Move) RenderView(_sel);
        RebuildTitleBlock();
        Dirty();
        RefreshViewListText();
        UpdateSheetListText();
        SetStatus($"Khung nhìn: X={F(_sel.X)}, Y={F(_sel.Y)}, W={F(_sel.W)}, H={F(_sel.H)} mm" +
                  (SheetBuilder.InsidePaper(_sheet, _sel) ? "" : " – NẰM NGOÀI GIẤY"));
    }

    private static string F(double v) => v.ToString("0.#", Inv);

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is ComboBox) return;
        if (_sel == null || _sheet == null) return;
        double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 1 : SheetBuilder.Snap;
        switch (e.Key)
        {
            case Key.Delete: DeleteSelectedView(); e.Handled = true; return;
            case Key.Left: _sel.X -= step; break;
            case Key.Right: _sel.X += step; break;
            case Key.Up: _sel.Y += step; break;
            case Key.Down: _sel.Y -= step; break;
            default: return;
        }
        e.Handled = true;
        var (pw, ph) = _sheet.PaperSize();
        _sel.X = Math.Clamp(_sel.X, 0, Math.Max(0, pw - _sel.W));
        _sel.Y = Math.Clamp(_sel.Y, 0, Math.Max(0, ph - _sel.H));
        RenderView(_sel);
        LoadViewProps();
        Dirty();
    }

    // ==================================================================
    // Khung nhìn
    // ==================================================================
    private void RefreshViewList()
    {
        _loading = true;
        lstViews.Items.Clear();
        if (_sheet != null) foreach (var v in _sheet.Views) lstViews.Items.Add(SheetBuilder.Describe(v));
        lstViews.SelectedIndex = _sel != null && _sheet != null ? _sheet.Views.IndexOf(_sel) : -1;
        _loading = false;
        LoadViewProps();
    }

    private void RefreshViewListText()
    {
        if (_sheet == null) return;
        _loading = true;
        for (int i = 0; i < _sheet.Views.Count && i < lstViews.Items.Count; i++) lstViews.Items[i] = SheetBuilder.Describe(_sheet.Views[i]);
        lstViews.SelectedIndex = _sel != null ? _sheet.Views.IndexOf(_sel) : -1;
        _loading = false;
    }

    private void SelectView(SheetView v)
    {
        _sel = v;
        _loading = true;
        lstViews.SelectedIndex = v != null && _sheet != null ? _sheet.Views.IndexOf(v) : -1;
        _loading = false;
        LoadViewProps();
        UpdateSelection();
    }

    private void LstViews_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _sheet == null) return;
        int i = lstViews.SelectedIndex;
        SelectView(i >= 0 && i < _sheet.Views.Count ? _sheet.Views[i] : null);
    }

    private static string ShowParam(string p) => string.IsNullOrEmpty(p) ? AllText : p;
    private static string ReadParam(string t) => t == AllText ? "" : (t ?? "").Trim();

    private void FillParams(ComboBox cb, SheetViewKind k, string current)
    {
        cb.Items.Clear();
        foreach (var o in SheetBuilder.ParamOptions(_p, _d, k).Distinct()) cb.Items.Add(ShowParam(o));
        if (current != null) cb.Text = ShowParam(current);
        else if (cb.Items.Count > 0) cb.SelectedIndex = 0;
    }

    private void LoadViewProps(bool positionOnly = false)
    {
        var v = _sel;
        if (positionOnly && v != null)
        {
            txtVX.Text = F(v.X); txtVY.Text = F(v.Y); txtVW.Text = F(v.W); txtVH.Text = F(v.H);
            return;
        }
        grpView.IsEnabled = v != null;
        if (v == null)
        {
            txtVKind.Text = ""; txtVTitle.Text = ""; txtVScale.Text = ""; txtVX.Text = txtVY.Text = txtVW.Text = txtVH.Text = "";
            txtVInfo.Text = ""; cbVParam.Items.Clear(); cbVParam.Text = "";
            return;
        }
        txtVKind.Text = SheetBuilder.KindName(v.Kind);
        if (v.Kind == SheetViewKind.Notes) { cbVParam.Items.Clear(); cbVParam.Text = (v.Param ?? "").Replace("\r", "").Replace("\n", " | "); }
        else FillParams(cbVParam, v.Kind, v.Param);
        txtVTitle.Text = v.Title ?? "";
        txtVScale.Text = F(v.Scale);
        txtVX.Text = F(v.X); txtVY.Text = F(v.Y); txtVW.Text = F(v.W); txtVH.Text = F(v.H);
        if (_drag == DragMode.None)
        {
            var lay = SheetBuilder.Layout(FiguresOf(v), v);
            txtVInfo.Text = $"Tỷ lệ thực: 1/{Math.Round(lay.EffectiveScale).ToString("0", Inv)}" +
                            (lay.Overflow ? " – hình lớn hơn khung (bị cắt), tăng kích thước khung hoặc giảm tỷ lệ" : "") +
                            (v.Kind == SheetViewKind.Notes ? "\nTham số = nội dung ghi chú, xuống dòng bằng \" | \" (để trống: ghi chú mặc định)." : "");
        }
    }

    private static bool TryNum(string s, out double v) =>
        double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, Inv, out v);

    private void ApplyViewProps()
    {
        var v = _sel;
        if (v == null || _sheet == null) return;
        v.Title = txtVTitle.Text ?? "";
        string par = v.Kind == SheetViewKind.Notes ? (cbVParam.Text ?? "").Replace(" | ", "\n") : ReadParam(cbVParam.Text);
        v.Param = par;
        if (TryNum(txtVScale.Text, out double sc) && sc >= 0) v.Scale = sc;
        var (pw, ph) = _sheet.PaperSize();
        if (TryNum(txtVW.Text, out double w) && w >= 10) v.W = Math.Min(w, pw);
        if (TryNum(txtVH.Text, out double h) && h >= 10) v.H = Math.Min(h, ph);
        if (TryNum(txtVX.Text, out double x)) v.X = Math.Clamp(x, 0, Math.Max(0, pw - v.W));
        if (TryNum(txtVY.Text, out double y)) v.Y = Math.Clamp(y, 0, Math.Max(0, ph - v.H));
        RenderView(v);
        RebuildTitleBlock();
        RefreshViewListText();
        LoadViewProps();
        Dirty();
    }

    private void BtnApplyView_Click(object sender, RoutedEventArgs e) => ApplyViewProps();

    private void ViewProp_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { ApplyViewProps(); e.Handled = true; }
    }

    private void CbKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cbKind.SelectedItem is not ComboBoxItem it) return;
        var k = (SheetViewKind)it.Tag;
        if (k == SheetViewKind.Notes) { cbParam.Items.Clear(); cbParam.Text = ""; return; }
        FillParams(cbParam, k, null);
    }

    private void BtnAddView_Click(object sender, RoutedEventArgs e)
    {
        if (_sheet == null)
        {
            BtnAddSheet_Click(sender, e);
            if (_sheet == null) return;
        }
        if (cbKind.SelectedItem is not ComboBoxItem it) return;
        var k = (SheetViewKind)it.Tag;
        string par = k == SheetViewKind.Notes ? (cbParam.Text ?? "").Replace(" | ", "\n") : ReadParam(cbParam.Text);
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var v = SheetBuilder.NewView(_p, _r, _d, _sheet, k, par);
            _sheet.Views.Add(v);
            AddViewHost(v);
            RefreshViewList();
            SelectView(v);
            RebuildTitleBlock();
            UpdateSheetListText();
            Dirty();
            SetStatus($"Đã thêm {SheetBuilder.Describe(v)}.");
        }
        catch (Exception ex) { MessageBox.Show("Không thêm được khung nhìn: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { Mouse.OverrideCursor = null; }
    }

    private void DeleteSelectedView()
    {
        if (_sel == null || _sheet == null) return;
        _sheet.Views.Remove(_sel);
        _sel = null;
        RebuildPage();
        RefreshViewList();
        UpdateSheetListText();
        Dirty();
    }

    private void BtnDelView_Click(object sender, RoutedEventArgs e) => DeleteSelectedView();

    private void BtnDupView_Click(object sender, RoutedEventArgs e)
    {
        if (_sel == null || _sheet == null) return;
        var v = SheetBuilder.Clone(_sel);
        var (pw, ph) = _sheet.PaperSize();
        v.X = Math.Min(v.X + 10, Math.Max(0, pw - v.W)); v.Y = Math.Max(0, v.Y - 10);
        _sheet.Views.Add(v);
        AddViewHost(v);
        RefreshViewList();
        SelectView(v);
        UpdateSheetListText();
        Dirty();
    }

    private void BtnFitView_Click(object sender, RoutedEventArgs e)
    {
        if (_sel == null) return;
        _sel.Scale = 0;
        RenderView(_sel);
        RebuildTitleBlock();
        RefreshViewListText();
        LoadViewProps();
        Dirty();
    }

    // ==================================================================
    // In, PDF, XPS, DXF
    // ==================================================================
    private DrawingGroup SheetDrawing(Sheet s) => SheetRender.RenderSheet(_p, s, FiguresOf, PrimsOf, 0);

    private static PageMediaSizeName MediaName(string paper) => paper switch
    {
        "A0" => PageMediaSizeName.ISOA0, "A1" => PageMediaSizeName.ISOA1, "A2" => PageMediaSizeName.ISOA2,
        "A4" => PageMediaSizeName.ISOA4, _ => PageMediaSizeName.ISOA3
    };

    /// <summary>Trang in: tờ đặt vừa vùng in (xoay 90° nếu hợp hơn, không phóng to quá 1:1).</summary>
    private static FixedPage MakePage(System.Windows.Media.Drawing dg, double pwMm, double phMm, double areaW, double areaH, out double scale)
    {
        double wDip = pwMm * SheetRender.DipPerMm, hDip = phMm * SheetRender.DipPerMm;
        double s0 = Math.Min(areaW / wDip, areaH / hDip), s1 = Math.Min(areaW / hDip, areaH / wDip);
        bool rotate = s1 > s0 * 1.02;
        scale = Math.Min(1, rotate ? s1 : s0);
        if (scale > 0.98) scale = Math.Min(1, Math.Max(scale, 0.98));
        var host = new DrawingHost { Drawing = dg, Scale = SheetRender.DipPerMm * scale, Width = wDip * scale, Height = hDip * scale };
        var page = new FixedPage { Width = areaW, Height = areaH, Background = Brushes.White };
        FrameworkElement el = host;
        if (rotate)
        {
            var b = new Border { Child = host, LayoutTransform = new RotateTransform(90) };
            el = b;
        }
        el.Measure(new Size(areaW, areaH));
        double ew = rotate ? hDip * scale : wDip * scale, eh = rotate ? wDip * scale : hDip * scale;
        FixedPage.SetLeft(el, Math.Max(0, (areaW - ew) / 2));
        FixedPage.SetTop(el, Math.Max(0, (areaH - eh) / 2));
        page.Children.Add(el);
        page.Measure(new Size(areaW, areaH));
        page.Arrange(new Rect(0, 0, areaW, areaH));
        page.UpdateLayout();
        return page;
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        var sheets = OutputSheets();
        if (sheets.Count == 0) { SetStatus("Chưa có tờ để in."); return; }
        var pd = new PrintDialog { UserPageRangeEnabled = false };
        try
        {
            var first = sheets[0];
            pd.PrintTicket.PageMediaSize = new PageMediaSize(MediaName(first.Paper));
            pd.PrintTicket.PageOrientation = first.Landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
        }
        catch (Exception) { /* máy in không hỗ trợ khổ – giữ mặc định */ }
        if (pd.ShowDialog() != true) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            double aw = pd.PrintableAreaWidth, ah = pd.PrintableAreaHeight;
            var doc = new FixedDocument();
            doc.DocumentPaginator.PageSize = new Size(aw, ah);
            double minScale = 1;
            foreach (var s in sheets)
            {
                var (pw, ph) = s.PaperSize();
                var page = MakePage(SheetDrawing(s), pw, ph, aw, ah, out double sc);
                minScale = Math.Min(minScale, sc);
                var pc = new PageContent { Child = page };
                doc.Pages.Add(pc);
            }
            pd.PrintDocument(doc.DocumentPaginator, $"{_p.Name} – bản vẽ kết cấu");
            SetStatus($"Đã gửi {sheets.Count} tờ tới máy in {pd.PrintQueue?.FullName}." +
                      (minScale < 0.98 ? $" Khổ giấy máy in nhỏ hơn tờ: đã thu nhỏ còn {Math.Round(minScale * 100)}%." : ""));
        }
        catch (Exception ex) { MessageBox.Show("Lỗi in: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { Mouse.OverrideCursor = null; }
    }

    /// <summary>Ghi PDF (ảnh, dpi tự chọn theo khổ) của các tờ – dùng cả cho tự kiểm tra.</summary>
    public void ExportPdf(string path, IList<Sheet> sheets = null, double dpi = 200)
    {
        sheets ??= _p.Sheets;
        var pages = new List<(BitmapSource, double, double)>();
        foreach (var s in sheets)
        {
            var (pw, ph) = s.PaperSize();
            double inch2 = pw * ph / (25.4 * 25.4);
            double d = Math.Min(dpi, Math.Sqrt(24e6 / inch2));      // ≤ ~24 triệu điểm ảnh mỗi trang
            pages.Add((SheetRender.Rasterize(SheetDrawing(s), pw, ph, d), pw, ph));
        }
        SheetRender.WriteRasterPdf(path, pages, $"{_p.Name} – bản vẽ kết cấu");
    }

    /// <summary>Ghi XPS dạng nét (vector), mỗi tờ một trang đúng khổ.</summary>
    public void ExportXps(string path, IList<Sheet> sheets = null)
    {
        sheets ??= _p.Sheets;
        var doc = new FixedDocument();
        foreach (var s in sheets)
        {
            var (pw, ph) = s.PaperSize();
            double w = pw * SheetRender.DipPerMm, h = ph * SheetRender.DipPerMm;
            var page = MakePage(SheetDrawing(s), pw, ph, w, h, out _);
            doc.Pages.Add(new PageContent { Child = page });
        }
        if (File.Exists(path)) File.Delete(path);
        using var xps = new System.Windows.Xps.Packaging.XpsDocument(path, FileAccess.ReadWrite);
        var wr = System.Windows.Xps.Packaging.XpsDocument.CreateXpsDocumentWriter(xps);
        wr.Write(doc);
    }

    private void BtnPdf_Click(object sender, RoutedEventArgs e)
    {
        var sheets = OutputSheets();
        if (sheets.Count == 0) { SetStatus("Chưa có tờ để xuất."); return; }
        var dlg = new SaveFileDialog
        {
            Filter = "PDF – ảnh 200 dpi (*.pdf)|*.pdf|XPS – dạng nét (*.xps)|*.xps",
            FileName = DrawUtilName(_p.Name) + "_BanVe.pdf"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            if (dlg.FileName.EndsWith(".xps", StringComparison.OrdinalIgnoreCase)) ExportXps(dlg.FileName, sheets);
            else ExportPdf(dlg.FileName, sheets);
            SetStatus($"Đã xuất {sheets.Count} tờ: {dlg.FileName}. (PDF dạng nét: dùng In… → Microsoft Print to PDF)");
        }
        catch (Exception ex) { MessageBox.Show("Lỗi xuất: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { Mouse.OverrideCursor = null; }
    }

    private static string DrawUtilName(string s)
    {
        var bad = Path.GetInvalidFileNameChars();
        var r = new string((s ?? "BanVe").Select(c => bad.Contains(c) || c == ' ' ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(r) ? "BanVe" : r;
    }

    private void BtnDxf_Click(object sender, RoutedEventArgs e)
    {
        var sheets = OutputSheets();
        if (sheets.Count == 0) { SetStatus("Chưa có tờ để xuất."); return; }
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục lưu các file DXF (mỗi tờ một file)" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var files = SheetBuilder.ExportAllDxf(_p, _r, _d, dlg.FolderName, sheets);
            SetStatus($"Đã xuất {files.Count} file DXF vào {dlg.FolderName}");
        }
        catch (Exception ex) { MessageBox.Show("Lỗi xuất DXF: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { Mouse.OverrideCursor = null; }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
