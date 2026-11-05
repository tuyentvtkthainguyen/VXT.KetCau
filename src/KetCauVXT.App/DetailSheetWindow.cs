using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using Microsoft.Win32;
using Shapes = System.Windows.Shapes;
using FixedDocument = System.Windows.Documents.FixedDocument;
using FixedPage = System.Windows.Documents.FixedPage;
using PageContent = System.Windows.Documents.PageContent;
using PlacedFigure = KetCauVXT.Detailing.PlacedFigure;

namespace KetCauVXT.App;

/// <summary>DÀN TRANG IN của chế độ Triển khai hồ sơ: danh sách tờ (trái), tờ đang chọn với các hình kéo thả được (giữa),
/// hình chưa đặt + thuộc tính tờ (phải). Tự dàn trang theo thứ tự hồ sơ (mặt bằng móng → chi tiết móng → mặt bằng cột → …
/// → thống kê), người dùng chỉnh tay (tờ chỉnh tay tự khoá), xuất PDF toàn bộ (dạng nét), in trực tiếp qua máy in đã cài, xuất DWG/DXF từng tờ.
/// Chỉ dùng máy in cài trên máy – không kết nối mạng, không gửi dữ liệu đi đâu.</summary>
public sealed class DetailSheetWindow : Window
{
    private readonly DetailingProject _prj;
    private readonly Action _changed;
    private List<SheetFigure> _cat = new();
    private Dictionary<string, SheetFigure> _map = new();
    private readonly Dictionary<string, System.Windows.Media.Drawing> _figDraw = new();

    private readonly ListBox _sheets = new() { BorderThickness = new Thickness(0) };
    private readonly ListBox _unplaced = new() { BorderThickness = new Thickness(0), MinHeight = 160 };
    private readonly Canvas _canvas = new() { Background = new SolidColorBrush(Color.FromRgb(120, 124, 130)) };
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _status = new() { Margin = new Thickness(6, 2, 6, 2) };
    private readonly ComboBox _paper = new() { ItemsSource = SheetGeometry.Papers, Width = 70 };
    private readonly CheckBox _portrait = new() { Content = "Khổ đứng", Margin = new Thickness(0, 4, 0, 0) };
    private readonly CheckBox _locked = new() { Content = "Khoá tờ (giữ khi tự dàn trang)", Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBox _title = new();
    private readonly ComboBox _setPaper = new() { ItemsSource = SheetGeometry.Papers, Width = 70 };
    private readonly CheckBox _autoPaper = new() { Content = "Tự chọn khổ nhỏ nhất vừa hình", Margin = new Thickness(0, 4, 0, 0) };
    private readonly CheckBox _index = new() { Content = "Có tờ danh mục bản vẽ", Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBox _date = new();
    private readonly ComboBox _moveTo = new() { MinWidth = 120 };
    private readonly CheckBox _mono = new() { Content = "PDF/in đen trắng", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
    private readonly CheckBox _oneToOne = new() { Content = "In 1:1 (không thu)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };

    private double _zoom = 1;          // hệ số phóng so với vừa khung
    private double _k = 1;             // DIP / mm giấy đang dùng
    private string _selFig;
    private bool _loading;

    // kéo thả
    private FrameworkElement _drag;
    private Point _dragStart;
    private double _dragX0, _dragY0;

    private DetailSheetSet Set => _prj.Sheets;
    private DetailSheet Cur => _sheets.SelectedIndex >= 0 && _sheets.SelectedIndex < Set.Sheets.Count ? Set.Sheets[_sheets.SelectedIndex] : null;

    public DetailSheetWindow(DetailingProject prj, List<(IDetailMember, DetailResult)> results, Action changed)
    {
        _prj = prj; _changed = changed;
        Title = "Dàn trang in – " + prj.ProjectName;
        Width = 1360; Height = 860; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var log = new List<string>();
        _cat = SheetLayoutEngine.Catalog(prj, results, log);
        _map = _cat.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
        if (_prj.Sheets == null || _prj.Sheets.Sheets.Count == 0)
        {
            _prj.Sheets ??= new DetailSheetSet();
            SheetLayoutEngine.AutoLayout(_prj.Sheets, _cat, log);
            _changed?.Invoke();
        }
        else if (SheetLayoutEngine.Prune(_prj.Sheets, _cat) > 0) { log.Add("Đã bỏ khỏi tờ các hình không còn trong hồ sơ."); _changed?.Invoke(); }

        var dock = new DockPanel();
        var bar = new ToolBar();
        void B(string text, Action a, string tip) { var b = new Button { Content = text, ToolTip = tip, Padding = new Thickness(8, 2, 8, 2) }; b.Click += (s, e) => a(); bar.Items.Add(b); }
        B("⟳ Tự dàn trang", AutoLayout, "Xếp lại mọi hình theo thứ tự hồ sơ (tờ đã khoá giữ nguyên)");
        B("＋ Thêm tờ", AddSheet, "Thêm tờ trống sau tờ đang chọn");
        B("✕ Xoá tờ", RemoveSheet, "Xoá tờ đang chọn (các hình trên tờ chuyển về danh sách chưa đặt)");
        B("🔒 Khoá / mở tờ", ToggleLock, "Tờ khoá được giữ nguyên khi tự dàn trang");
        B("Đánh số lại", () => { SheetLayoutEngine.Renumber(Set); Changed(); RefreshSheets(); }, "Đánh số KC-01, KC-02… theo thứ tự tờ");
        B("▲", () => MoveSheet(-1), "Đưa tờ lên trước");
        B("▼", () => MoveSheet(1), "Đưa tờ xuống sau");
        bar.Items.Add(new Separator());
        B("Xuất PDF toàn bộ…", ExportPdf, "Một file PDF dạng nét, mỗi tờ một trang đúng khổ giấy");
        B("In…", Print, "In trực tiếp qua máy in đã cài trên máy (chọn máy in, số bản, trang)");
        B("Xuất DWG/DXF…", ExportCad, "Mỗi tờ một file AutoCAD (định dạng theo ô Định dạng xuất của cửa sổ hồ sơ)");
        bar.Items.Add(_mono); bar.Items.Add(_oneToOne);
        bar.Items.Add(new Separator());
        B("＋", () => { _zoom *= 1.25; Render(); }, "Phóng to");
        B("－", () => { _zoom /= 1.25; Render(); }, "Thu nhỏ");
        B("Vừa", () => { _zoom = 1; Render(); }, "Vừa khung");
        DockPanel.SetDock(bar, Dock.Top); dock.Children.Add(bar);
        var sb = new System.Windows.Controls.Primitives.StatusBar(); sb.Items.Add(_status);
        DockPanel.SetDock(sb, Dock.Bottom); dock.Children.Add(sb);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });

        var left = new DockPanel();
        var lh = new TextBlock { Text = "CÁC TỜ IN", FontWeight = FontWeights.Bold, Margin = new Thickness(8, 6, 8, 4) };
        DockPanel.SetDock(lh, Dock.Top); left.Children.Add(lh); left.Children.Add(_sheets);
        Grid.SetColumn(left, 0); grid.Children.Add(left);

        _scroll.Content = _canvas;
        _scroll.SizeChanged += (s, e) => { if (Math.Abs(_zoom - 1) < 1e-9) Render(); };
        Grid.SetColumn(_scroll, 1); grid.Children.Add(_scroll);

        var right = new StackPanel { Margin = new Thickness(10, 6, 10, 6) };
        TextBlock H(string t) => new() { Text = t, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 4) };
        right.Children.Add(H("TỜ ĐANG CHỌN"));
        right.Children.Add(new TextBlock { Text = "Tên bản vẽ" }); right.Children.Add(_title);
        var pr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        pr.Children.Add(new TextBlock { Text = "Khổ giấy  ", VerticalAlignment = VerticalAlignment.Center }); pr.Children.Add(_paper);
        right.Children.Add(pr); right.Children.Add(_portrait); right.Children.Add(_locked);
        right.Children.Add(H("HÌNH ĐANG CHỌN"));
        var mv = new StackPanel { Orientation = Orientation.Horizontal };
        mv.Children.Add(_moveTo);
        mv.Children.Add(Btn("Chuyển", MoveFigureToSheet));
        right.Children.Add(mv);
        right.Children.Add(Btn("Gỡ hình khỏi tờ", RemoveFigure));
        right.Children.Add(H("HÌNH CHƯA ĐẶT"));
        right.Children.Add(new Border { BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(1), Child = _unplaced, MaxHeight = 240 });
        right.Children.Add(Btn("Đặt vào tờ đang chọn", PlaceSelected));
        right.Children.Add(H("BỘ TỜ"));
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new TextBlock { Text = "Khổ mặc định  ", VerticalAlignment = VerticalAlignment.Center }); sp.Children.Add(_setPaper);
        right.Children.Add(sp); right.Children.Add(_autoPaper); right.Children.Add(_index);
        right.Children.Add(new TextBlock { Text = "Ngày ghi khung tên (để trống = không ghi)", Margin = new Thickness(0, 4, 0, 0) }); right.Children.Add(_date);
        right.Children.Add(new TextBlock
        {
            Text = "Kéo hình để dời vị trí (tờ chỉnh tay tự khoá). Khung đỏ = hình chồng hình khác / đè khung tên / ra ngoài khung.",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 10, 0, 0)
        });
        var rs = new ScrollViewer { Content = right, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(rs, 2); grid.Children.Add(rs);
        dock.Children.Add(grid);
        Content = dock;

        _sheets.SelectionChanged += (s, e) => { LoadSheetProps(); Render(); };
        _title.LostFocus += (s, e) => { if (_loading || Cur == null || Cur.Title == _title.Text) return; Cur.Title = _title.Text; Changed(); RefreshSheets(); };
        _paper.SelectionChanged += (s, e) => { if (_loading || Cur == null || _paper.SelectedItem is not string p) return; Cur.Paper = p; Cur.Locked = true; Changed(); RefreshSheets(); };
        _portrait.Click += (s, e) => { if (Cur == null) return; Cur.Portrait = _portrait.IsChecked == true; Cur.Locked = true; Changed(); RefreshSheets(); };
        _locked.Click += (s, e) => { if (Cur == null) return; Cur.Locked = _locked.IsChecked == true; Changed(); RefreshSheets(); };
        _setPaper.SelectionChanged += (s, e) => { if (_loading || _setPaper.SelectedItem is not string p) return; Set.Paper = p; Changed(); };
        _autoPaper.Click += (s, e) => { Set.AutoPaper = _autoPaper.IsChecked == true; Changed(); };
        _index.Click += (s, e) => { Set.IndexSheet = _index.IsChecked == true; Changed(); };
        _date.LostFocus += (s, e) => { if (Set.Date == _date.Text) return; Set.Date = _date.Text; Changed(); Render(); };
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseLeftButtonUp += Canvas_MouseUp;

        _loading = true;
        _setPaper.SelectedItem = Set.Paper; _autoPaper.IsChecked = Set.AutoPaper; _index.IsChecked = Set.IndexSheet; _date.Text = Set.Date ?? "";
        _loading = false;
        RefreshSheets();
        if (Set.Sheets.Count > 0) _sheets.SelectedIndex = 0;
        SetStatus(log.Count > 0 ? string.Join(" | ", log) : "Đã có " + Set.Sheets.Count + " tờ, " + _cat.Count + " hình.");
    }

    private static Button Btn(string t, Action a) { var b = new Button { Content = t, Margin = new Thickness(0, 4, 4, 0), Padding = new Thickness(6, 2, 6, 2), HorizontalAlignment = HorizontalAlignment.Left }; b.Click += (s, e) => a(); return b; }
    private void SetStatus(string s) => _status.Text = s;
    private void Changed() => _changed?.Invoke();

    // ------------------------------------------------------------------
    private void RefreshSheets()
    {
        int sel = _sheets.SelectedIndex;
        _loading = true;
        _sheets.Items.Clear();
        foreach (var s in Set.Sheets)
            _sheets.Items.Add((s.Locked ? "🔒 " : "") + s.No + "  " + s.Title + "  (" + s.Paper + (s.Portrait ? " đứng" : "") + (s.IsIndex ? "" : ", " + s.Figures.Count + " hình") + ")");
        _moveTo.ItemsSource = Set.Sheets.Select(s => s.No + " " + s.Title).ToList();
        _loading = false;
        _sheets.SelectedIndex = Math.Min(Math.Max(sel, 0), Set.Sheets.Count - 1);
        RefreshUnplaced();
        LoadSheetProps();
        Render();
    }

    private void RefreshUnplaced()
    {
        _unplaced.Items.Clear();
        foreach (var f in SheetLayoutEngine.Unplaced(Set, _cat))
            _unplaced.Items.Add(new ListBoxItem { Content = f.Caption + "  [" + Math.Round(f.W) + "×" + Math.Round(f.H) + " mm]", Tag = f.Id });
    }

    private void LoadSheetProps()
    {
        var c = Cur;
        _loading = true;
        _title.Text = c?.Title ?? ""; _paper.SelectedItem = c?.Paper; _portrait.IsChecked = c?.Portrait == true; _locked.IsChecked = c?.Locked == true;
        _loading = false;
    }

    private System.Windows.Media.Drawing FigureDrawing(SheetFigure f)
    {
        if (_figDraw.TryGetValue(f.Id, out var d)) return d;
        var e = f.F.W.Extents;
        double k = 1.0 / Math.Max(f.F.W.Scale, 1e-9);
        d = SheetRender.Render(new[] { (f.F.W, k, -e.X1 * k, -e.Y1 * k) }, 0, f.H, 0.1, x => x.GetPrimitives());
        _figDraw[f.Id] = d;
        return d;
    }

    /// <summary>Hình vẽ cả tờ (mm giấy, y xuống) – in / xem.</summary>
    private System.Windows.Media.Drawing SheetDrawing(DetailSheet sh, bool withFigures = true)
    {
        var w = withFigures ? SheetLayoutEngine.Compose(_prj, Set, sh, _map) : FrameOnly(sh);
        var (_, ph) = sh.Size;
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, sh.Size.W, ph));
            dc.DrawDrawing(SheetRender.Render(new[] { (w, 1.0 / w.Scale, 0.0, 0.0) }, 0, ph, withFigures ? 0 : 0.1, x => x.GetPrimitives(), _mono.IsChecked == true ? (Color?)Colors.Black : null));
        }
        dg.Freeze();
        return dg;
    }

    /// <summary>Chỉ khung + khung tên (+ bảng danh mục) – nền khi chỉnh tay.</summary>
    private DxfWriter FrameOnly(DetailSheet sh)
    {
        var empty = new DetailSheet { No = sh.No, Title = sh.Title, Paper = sh.Paper, Portrait = sh.Portrait, IsIndex = sh.IsIndex };
        // giữ tỷ lệ ghi khung tên theo hình thật
        var w = SheetLayoutEngine.Compose(_prj, Set, empty, _map);
        return w;
    }

    private void Render()
    {
        _canvas.Children.Clear();
        var sh = Cur;
        if (sh == null) { _canvas.Width = _canvas.Height = 0; return; }
        var (pw, ph) = sh.Size;
        double availW = Math.Max(200, _scroll.ActualWidth - 40), availH = Math.Max(200, _scroll.ActualHeight - 40);
        double fit = Math.Min(availW / pw, availH / ph);
        _k = fit * _zoom;
        _canvas.Width = pw * _k + 40; _canvas.Height = ph * _k + 40;
        var bg = new DrawingHost { Drawing = SheetDrawing(sh, false), Scale = _k, Width = pw * _k, Height = ph * _k, Background = Brushes.White };
        Canvas.SetLeft(bg, 20); Canvas.SetTop(bg, 20);
        _canvas.Children.Add(bg);
        var rects = SheetLayoutEngine.PlacedRects(sh, _map);
        var area = SheetGeometry.Drawable(sh.Paper, sh.Portrait);
        var tz = SheetGeometry.TitleZone(sh.Paper, sh.Portrait);
        int i = 0;
        foreach (var pf in sh.Figures)
        {
            if (!_map.TryGetValue(pf.Id, out var f)) continue;
            var r = rects[i++];
            bool bad = rects.Any(o => !o.Equals(r) && SheetGeometry.Overlap(o, r)) || SheetGeometry.Overlap(r, tz) || r.X1 < area.X1 - 9 || r.Y1 < area.Y1 - 9 || r.X2 > area.X2 + 9 || r.Y2 > area.Y2 + 9;
            var host = new DrawingHost { Drawing = FigureDrawing(f), Scale = _k, Width = f.W * _k, Height = f.H * _k, Background = Brushes.Transparent };
            var bd = new Border
            {
                Child = host, Tag = pf, Cursor = Cursors.SizeAll, ToolTip = f.Caption,
                BorderThickness = new Thickness(pf.Id == _selFig || bad ? 1.5 : 0.6),
                BorderBrush = bad ? Brushes.Red : pf.Id == _selFig ? Brushes.DodgerBlue : new SolidColorBrush(Color.FromArgb(70, 30, 110, 200))
            };
            Canvas.SetLeft(bd, 20 + pf.X * _k); Canvas.SetTop(bd, 20 + (ph - pf.Y - f.H) * _k);
            bd.MouseLeftButtonDown += Fig_MouseDown;
            _canvas.Children.Add(bd);
        }
    }

    private void Fig_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border bd || bd.Tag is not PlacedFigure pf) return;
        _selFig = pf.Id;
        foreach (var c in _canvas.Children.OfType<Border>()) c.BorderThickness = new Thickness(ReferenceEquals(c, bd) ? 1.5 : 0.6);
        bd.BorderBrush = Brushes.DodgerBlue;
        _drag = bd; _dragStart = e.GetPosition(_canvas); _dragX0 = Canvas.GetLeft(bd); _dragY0 = Canvas.GetTop(bd);
        _canvas.CaptureMouse();
        e.Handled = true;
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_drag == null || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(_canvas);
        Canvas.SetLeft(_drag, _dragX0 + p.X - _dragStart.X);
        Canvas.SetTop(_drag, _dragY0 + p.Y - _dragStart.Y);
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_drag == null) return;
        _canvas.ReleaseMouseCapture();
        var bd = _drag; _drag = null;
        var sh = Cur;
        if (sh == null || bd.Tag is not PlacedFigure pf || !_map.TryGetValue(pf.Id, out var f)) return;
        var p = e.GetPosition(_canvas);
        if (Math.Abs(p.X - _dragStart.X) < 2 && Math.Abs(p.Y - _dragStart.Y) < 2) return;
        var (pw, ph) = sh.Size;
        double x = (Canvas.GetLeft(bd) - 20) / _k, top = (Canvas.GetTop(bd) - 20) / _k;
        pf.X = Math.Round(Math.Clamp(x, 0, Math.Max(0, pw - f.W)), 1);
        pf.Y = Math.Round(Math.Clamp(ph - top - f.H, 0, Math.Max(0, ph - f.H)), 1);
        if (!sh.Locked) { sh.Locked = true; SetStatus("Tờ " + sh.No + " đã chỉnh tay → tự khoá (giữ nguyên khi tự dàn trang)."); }
        Changed();
        RefreshSheets();
    }

    // ------------------------------------------------------------------
    private void AutoLayout()
    {
        var log = new List<string>();
        SheetLayoutEngine.AutoLayout(Set, _cat, log);
        Changed(); RefreshSheets();
        SetStatus("Đã dàn " + Set.Sheets.Count + " tờ (" + Set.Sheets.Count(s => s.Locked) + " tờ khoá giữ nguyên)." + (log.Count > 0 ? " " + string.Join(" ", log) : ""));
    }

    private void AddSheet()
    {
        int at = Math.Max(0, _sheets.SelectedIndex + 1);
        Set.Sheets.Insert(Math.Min(at, Set.Sheets.Count), new DetailSheet { Paper = Set.Paper, Title = "BẢN VẼ MỚI", Locked = true });
        SheetLayoutEngine.Renumber(Set);
        Changed(); RefreshSheets();
        _sheets.SelectedIndex = Math.Min(at, Set.Sheets.Count - 1);
    }

    private void RemoveSheet()
    {
        var c = Cur; if (c == null) return;
        if (MessageBox.Show(this, "Xoá tờ " + c.No + " – " + c.Title + "? Các hình trên tờ chuyển về danh sách chưa đặt.", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Set.Sheets.Remove(c);
        SheetLayoutEngine.Renumber(Set);
        Changed(); RefreshSheets();
    }

    private void ToggleLock()
    {
        var c = Cur; if (c == null) return;
        c.Locked = !c.Locked; Changed(); RefreshSheets();
    }

    private void MoveSheet(int d)
    {
        int i = _sheets.SelectedIndex, j = i + d;
        if (i < 0 || j < 0 || j >= Set.Sheets.Count) return;
        (Set.Sheets[i], Set.Sheets[j]) = (Set.Sheets[j], Set.Sheets[i]);
        Changed(); RefreshSheets();
        _sheets.SelectedIndex = j;
    }

    private void PlaceSelected()
    {
        var c = Cur;
        if (c == null || _unplaced.SelectedItem is not ListBoxItem it || it.Tag is not string id || !_map.TryGetValue(id, out var f)) { SetStatus("Chọn một hình chưa đặt và một tờ."); return; }
        if (!SheetLayoutEngine.Place(Set, c, f, _map))
        {
            var a = SheetGeometry.Drawable(c.Paper, c.Portrait);
            SheetLayoutEngine.Place(Set, c, f, _map, a.X1, Math.Max(a.Y1, a.Y2 - f.H));
            SetStatus("Tờ " + c.No + " hết chỗ: đã đặt hình ở góc trái trên – kéo để sắp xếp lại hoặc đổi khổ giấy.");
        }
        c.Locked = true; Changed(); RefreshSheets();
    }

    private void RemoveFigure()
    {
        var c = Cur; if (c == null || _selFig == null) return;
        if (c.Figures.RemoveAll(pf => pf.Id == _selFig) > 0) { c.Locked = true; _selFig = null; Changed(); RefreshSheets(); }
    }

    private void MoveFigureToSheet()
    {
        int t = _moveTo.SelectedIndex;
        if (_selFig == null || t < 0 || t >= Set.Sheets.Count || !_map.TryGetValue(_selFig, out var f)) { SetStatus("Chọn một hình trên tờ và tờ đích."); return; }
        var target = Set.Sheets[t];
        var src = Set.Sheets.FirstOrDefault(s => s.Figures.Any(pf => pf.Id == _selFig));
        if (!SheetLayoutEngine.Place(Set, target, f, _map))
        {
            var a = SheetGeometry.Drawable(target.Paper, target.Portrait);
            SheetLayoutEngine.Place(Set, target, f, _map, a.X1, Math.Max(a.Y1, a.Y2 - f.H));
        }
        target.Locked = true; if (src != null) src.Locked = true;
        Changed(); RefreshSheets();
        _sheets.SelectedIndex = t;
    }

    // ------------------------------------------------------------------
    // Xuất / in
    // ------------------------------------------------------------------
    private static string SafeName(string s)
    {
        var bad = Path.GetInvalidFileNameChars();
        var r = new string((s ?? "HoSo").Select(c => bad.Contains(c) || c == ' ' ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(r) ? "HoSo" : r;
    }

    private void ExportPdf()
    {
        if (Set.Sheets.Count == 0) { SetStatus("Chưa có tờ."); return; }
        var dlg = new SaveFileDialog { Filter = "PDF dạng nét (*.pdf)|*.pdf", FileName = SafeName(_prj.ProjectName) + "_HoSoKetCau.pdf" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            SheetLayoutEngine.ExportPdf(_prj, Set, _cat, dlg.FileName, _mono.IsChecked == true);
            SetStatus("Đã xuất " + Set.Sheets.Count + " tờ: " + dlg.FileName + (PdfVectorWriter.FindFont() == null ? " (máy không có phông TrueType – chữ ghi không dấu)" : ""));
        }
        catch (Exception ex) { MessageBox.Show(this, "Lỗi xuất PDF: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { Mouse.OverrideCursor = null; }
    }

    private void ExportCad()
    {
        if (Set.Sheets.Count == 0) { SetStatus("Chưa có tờ."); return; }
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục lưu các tờ (mỗi tờ một file AutoCAD)" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var files = SheetLayoutEngine.ExportCad(_prj, Set, _cat, Path.Combine(dlg.FolderName, "DanTrang_" + SafeName(_prj.ProjectName)));
            SetStatus("Đã ghi " + files.Count + " file vào " + Path.GetDirectoryName(files.FirstOrDefault() ?? dlg.FolderName));
        }
        catch (Exception ex) { MessageBox.Show(this, "Lỗi xuất: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { Mouse.OverrideCursor = null; }
    }

    private static PageMediaSizeName MediaName(string paper) => paper switch
    {
        "A0" => PageMediaSizeName.ISOA0, "A1" => PageMediaSizeName.ISOA1, "A2" => PageMediaSizeName.ISOA2, _ => PageMediaSizeName.ISOA3
    };

    /// <summary>Trang in: tờ vừa vùng in (xoay 90° nếu hợp hơn); oneToOne = không thu nhỏ (cắt phần ngoài khổ máy in).</summary>
    private static FixedPage MakePage(System.Windows.Media.Drawing dg, double pwMm, double phMm, double areaW, double areaH, bool oneToOne, out double scale)
    {
        double wDip = pwMm * SheetRender.DipPerMm, hDip = phMm * SheetRender.DipPerMm;
        double s0 = Math.Min(areaW / wDip, areaH / hDip), s1 = Math.Min(areaW / hDip, areaH / wDip);
        bool rotate = s1 > s0 * 1.02;
        scale = oneToOne ? 1 : Math.Min(1, rotate ? s1 : s0);
        var host = new DrawingHost { Drawing = dg, Scale = SheetRender.DipPerMm * scale, Width = wDip * scale, Height = hDip * scale };
        var page = new FixedPage { Width = areaW, Height = areaH, Background = Brushes.White };
        FrameworkElement el = host;
        if (rotate) el = new Border { Child = host, LayoutTransform = new RotateTransform(90) };
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

    private void Print()
    {
        if (Set.Sheets.Count == 0) { SetStatus("Chưa có tờ để in."); return; }
        var pd = new PrintDialog { UserPageRangeEnabled = true, MinPage = 1, MaxPage = (uint)Set.Sheets.Count, PageRange = new PageRange(1, Set.Sheets.Count) };
        try
        {
            var first = Cur ?? Set.Sheets[0];
            pd.PrintTicket.PageMediaSize = new PageMediaSize(MediaName(first.Paper));
            pd.PrintTicket.PageOrientation = first.Portrait ? PageOrientation.Portrait : PageOrientation.Landscape;
        }
        catch (Exception) { /* máy in không hỗ trợ khổ – giữ mặc định */ }
        if (pd.ShowDialog() != true) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            int from = 1, to = Set.Sheets.Count;
            if (pd.PageRangeSelection == PageRangeSelection.UserPages) { from = Math.Max(1, pd.PageRange.PageFrom); to = Math.Min(Set.Sheets.Count, Math.Max(from, pd.PageRange.PageTo)); }
            else if (pd.PageRangeSelection == PageRangeSelection.CurrentPage && Cur != null) from = to = _sheets.SelectedIndex + 1;
            double aw = pd.PrintableAreaWidth, ah = pd.PrintableAreaHeight;
            var doc = new FixedDocument();
            doc.DocumentPaginator.PageSize = new Size(aw, ah);
            double minScale = 1;
            for (int i = from - 1; i < to; i++)
            {
                var sh = Set.Sheets[i];
                var (pw, ph) = sh.Size;
                var page = MakePage(SheetDrawing(sh), pw, ph, aw, ah, _oneToOne.IsChecked == true, out double sc);
                minScale = Math.Min(minScale, sc);
                doc.Pages.Add(new PageContent { Child = page });
            }
            pd.PrintDocument(doc.DocumentPaginator, _prj.ProjectName + " – hồ sơ kết cấu");
            SetStatus("Đã gửi " + (to - from + 1) + " tờ tới máy in " + pd.PrintQueue?.FullName + "." +
                      (minScale < 0.98 ? " Khổ giấy máy in nhỏ hơn tờ: đã thu còn " + Math.Round(minScale * 100) + "%." : ""));
        }
        catch (Exception ex) { MessageBox.Show(this, "Lỗi in: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { Mouse.OverrideCursor = null; }
    }
}
