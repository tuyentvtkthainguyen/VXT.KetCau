using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Drawing;
using KetCauVXT.Import;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Cửa sổ "Hiệu chỉnh bản vẽ…": mở một bản vẽ tự sinh (bộ KC-xx, gia công thép, tờ dàn trang) dạng nét vẽ, chọn thực thể
/// (chữ, nhãn thép, kích thước, nét), kéo/dời, sửa nội dung chữ, xoá, đổi lớp/màu, thêm chữ/mũi tên/đường/mây/khung; hoàn tác/làm lại.
/// Chỉnh sửa lưu vào dự án (Project.DrawingEdits/DrawingNotes – Drawing/DrawingOverlay.cs) và được áp lại sau mỗi lần dựng lại/xuất;
/// chỉnh sửa không còn khớp được liệt kê "chỉnh sửa không còn áp dụng được". Nạp lại bản vẽ đã sửa trong CAD (DXF/DWG) để khoá bản vẽ; Bỏ khoá để dựng lại.</summary>
internal sealed class DrawingEditWindow : Window
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Một bản vẽ có thể hiệu chỉnh.</summary>
    public sealed class DrawingRef
    {
        public string Key = "", Title = "", Number = "", FileName = "";
        /// <summary>"DS" (bộ KC-xx), "SHOP" (gia công thép), "SHEET" (tờ dàn trang).</summary>
        public string Kind = "DS";
        public Sheet Sheet;
        public string Badge = "";
        public override string ToString() => $"{Number} – {Title}{Badge}";
    }

    private readonly Project _p;
    private readonly AnalysisResults _r;
    private readonly DesignSummary _d;
    private readonly Action _markDirty;

    private readonly ListBox _list = new() { Width = 270, Margin = new Thickness(0, 0, 6, 0) };
    private readonly Border _view = new() { Background = Brushes.White, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), ClipToBounds = true, Focusable = true };
    private readonly Canvas _canvas = new() { IsHitTestVisible = false };
    private readonly DrawingHost _host = new() { IsHitTestVisible = false };
    private readonly DrawingHost _selHost = new() { IsHitTestVisible = false };
    private readonly TextBlock _status = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _badge = new() { Background = new SolidColorBrush(Color.FromRgb(200, 40, 40)), Padding = new Thickness(8, 3, 8, 3), Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8), IsHitTestVisible = false };
    private readonly TextBlock _badgeText = new() { Foreground = Brushes.White, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, MaxWidth = 700 };
    private readonly ListBox _staleList = new() { Height = 90 };
    private readonly Expander _staleBox = new() { Header = "Chỉnh sửa không còn áp dụng được (0)", IsExpanded = false, Margin = new Thickness(0, 4, 0, 0) };
    private readonly CheckBox _chkGroup = new() { Content = "Chọn cả nhóm", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0), ToolTip = "Nhãn thép (vai + chữ + vòng số), tên hình, kích thước, cao độ, đầu trục được chọn/dời cùng nhau" };
    private readonly RadioButton _tSelect = Tool("Chọn / dời", "Nhấp chọn (Ctrl: chọn thêm), kéo để dời; phím mũi tên dời 1 mm (Shift: 10 mm)", true);
    private readonly RadioButton _tText = Tool("Chữ", "Nhấp để thêm chữ (cao 3,5 mm khi in)");
    private readonly RadioButton _tArrow = Tool("Mũi tên", "Kéo từ đuôi tới đầu mũi tên");
    private readonly RadioButton _tLine = Tool("Đường", "Kéo để vẽ đoạn thẳng");
    private readonly RadioButton _tCloud = Tool("Mây", "Đám mây sửa đổi – kéo để vẽ");
    private readonly RadioButton _tRect = Tool("Khung", "Kéo để vẽ khung chữ nhật");

    private readonly List<DrawingRef> _refsList = new();
    private DrawingRef _cur;
    private DxfWriter _base, _shown;
    private List<EntityRef> _ents = new();
    private Dictionary<int, DrawPrim> _primOf = new();
    private FrozenDrawing _fz;
    private OverlayReport _rep = new();
    private double _S = 1, _ox, _oy, _pw = 100, _ph = 100;
    private double _zoom = 1, _panX, _panY;
    private bool _autoFit = true;
    private readonly DispatcherTimer _redraw;

    /// <summary>Đơn vị chọn: (Owner, Group) khi chọn nhóm, ngược lại (Owner, Key).</summary>
    private readonly List<(string Owner, string Key, string Group)> _sel = new();
    private readonly Stack<string> _undo = new(), _redo = new();

    private Point? _down;
    private bool _dragging, _panning;
    private Point _panStart;
    private double _panX0, _panY0;
    private (double X, double Y) _downModel, _dragD;

    public DrawingEditWindow(Project p, AnalysisResults r, DesignSummary d, Action markDirty, string startKey = null)
    {
        _p = p ?? throw new ArgumentNullException(nameof(p));
        _r = r; _d = d; _markDirty = markDirty;
        _p.DrawingEdits ??= new List<DrawingEdit>();
        _p.DrawingNotes ??= new List<DrawingAnnotation>();
        _p.FrozenDrawings ??= new List<FrozenDrawing>();
        DialogUi.Setup(this, "Hiệu chỉnh bản vẽ", 1400, 860);
        ShowInTaskbar = true;
        MinWidth = 800; MinHeight = 500;
        _redraw = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        _redraw.Tick += (_, _) => { _redraw.Stop(); RenderAll(); };
        BuildUi();
        BuildCatalog();
        Loaded += (_, _) =>
        {
            var start = _refsList.FirstOrDefault(x => x.Key == startKey) ?? _refsList.FirstOrDefault();
            if (start != null) _list.SelectedItem = start;
        };
    }

    private static RadioButton Tool(string text, string tip, bool on = false) =>
        new() { Content = text, ToolTip = tip, IsChecked = on, GroupName = "dewTool", Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };

    // ==================================================================
    // Giao diện
    // ==================================================================
    private void BuildUi()
    {
        var root = new DockPanel { Margin = new Thickness(8) };

        var tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        foreach (var t in new[] { _tSelect, _tText, _tArrow, _tLine, _tCloud, _tRect }) tools.Children.Add(t);
        tools.Children.Add(_chkGroup);
        tools.Children.Add(DialogUi.Btn("Sửa chữ… (F2)", (s, e) => EditTextUi()));
        tools.Children.Add(DialogUi.Btn("Xoá (Del)", (s, e) => DeleteSelection()));
        tools.Children.Add(DialogUi.Btn("Lớp / màu…", (s, e) => RestyleUi()));
        tools.Children.Add(DialogUi.Btn("↶", (s, e) => Undo()));
        tools.Children.Add(DialogUi.Btn("↷", (s, e) => Redo()));
        tools.Children.Add(DialogUi.Btn("Vừa màn hình", (s, e) => Fit()));
        tools.Children.Add(DialogUi.Btn("Khôi phục bản gốc", (s, e) => ResetDrawing()));
        DockPanel.SetDock(tools, Dock.Top);
        root.Children.Add(tools);

        var bottom = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(DialogUi.Btn("Xuất DXF bản vẽ này…", (s, e) => ExportCurrentUi()));
        right.Children.Add(DialogUi.Btn("Nạp lại bản vẽ đã chỉnh sửa (DXF/DWG)…", (s, e) => AttachUi()));
        right.Children.Add(DialogUi.Btn("Bỏ khoá", (s, e) => UnlockUi()));
        right.Children.Add(DialogUi.Btn("Đóng", (s, e) => Close()));
        DockPanel.SetDock(right, Dock.Right);
        bottom.Children.Add(right);
        bottom.Children.Add(_status);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        var stalePanel = new DockPanel();
        var staleBtn = DialogUi.Btn("Xoá các chỉnh sửa này", (s, e) => RemoveStale());
        DockPanel.SetDock(staleBtn, Dock.Right);
        staleBtn.VerticalAlignment = VerticalAlignment.Top;
        stalePanel.Children.Add(staleBtn);
        stalePanel.Children.Add(_staleList);
        _staleBox.Content = stalePanel;
        DockPanel.SetDock(_staleBox, Dock.Bottom);
        root.Children.Add(_staleBox);

        var left = new DockPanel();
        var lh = DialogUi.Label("Bản vẽ", double.NaN, true);
        DockPanel.SetDock(lh, Dock.Top);
        left.Children.Add(lh);
        var hint = DialogUi.Hint("[KHOÁ]: đã nạp bản sửa trong CAD. (n sửa): số chỉnh sửa trên bản vẽ. Chỉnh sửa trên một hình (dầm, cột…) thấy cả ở bộ KC-xx và tờ dàn trang.");
        hint.Width = 270;
        DockPanel.SetDock(hint, Dock.Bottom);
        left.Children.Add(hint);
        left.Children.Add(_list);
        DockPanel.SetDock(left, Dock.Left);
        root.Children.Add(left);

        _canvas.Children.Add(_host);
        _canvas.Children.Add(_selHost);
        var grid = new Grid();
        grid.Children.Add(_canvas);
        _badge.Child = _badgeText;
        grid.Children.Add(_badge);
        _view.Child = grid;
        root.Children.Add(_view);
        Content = root;

        _list.SelectionChanged += (s, e) => { if (_list.SelectedItem is DrawingRef d && !ReferenceEquals(d, _cur)) LoadDrawing(d); };
        _view.MouseWheel += View_MouseWheel;
        _view.MouseDown += View_MouseDown;
        _view.MouseMove += View_MouseMove;
        _view.MouseUp += View_MouseUp;
        _view.SizeChanged += (s, e) => { if (_autoFit) Fit(); };
        PreviewKeyDown += Window_PreviewKeyDown;
    }

    private void SetStatus(string s) => _status.Text = s;

    // ==================================================================
    // Danh sách bản vẽ
    // ==================================================================
    private void BuildCatalog()
    {
        _refsList.Clear();
        try
        {
            using (DrawingOverlay.Bypass())
                foreach (var it in DrawingSet.BuildDrawings(_p, _d, _ => false))
                    _refsList.Add(new DrawingRef { Key = it.Key, Title = it.Title, Number = it.Number, FileName = it.FileName, Kind = "DS" });
        }
        catch (Exception ex) { SetStatus("Lỗi liệt kê bộ bản vẽ: " + ex.Message); }
        if (_d != null && _d.All.Any(m => m.Bars.Count > 0))
        {
            try
            {
                using (DrawingOverlay.Bypass())
                    foreach (var it in RebarShopDrawing.BuildDrawings(_p, _d, _ => false))
                        _refsList.Add(new DrawingRef { Key = it.Key, Title = it.Title, Number = it.Number, FileName = it.FileName, Kind = "SHOP" });
            }
            catch (Exception) { /* không có thép gia công */ }
        }
        foreach (var s in _p.Sheets ?? new List<Sheet>())
            _refsList.Add(new DrawingRef { Key = DrawingOverlay.SheetKey(s), Title = "Tờ " + s.Name, Number = s.Number, FileName = SheetBuilder.FileName(s), Kind = "SHEET", Sheet = s });
        UpdateBadges();
        _list.ItemsSource = _refsList;
    }

    private void UpdateBadges()
    {
        foreach (var d in _refsList)
        {
            var (ed, nt) = DrawingOverlay.MadeIn(_p, d.Key);
            int n = ed.Count + nt.Count;
            d.Badge = (DrawingOverlay.FrozenOf(_p, d.Key) != null ? "  [KHOÁ]" : "") + (n > 0 ? $"  ({n} sửa)" : "");
        }
        _list.Items.Refresh();
    }

    /// <summary>Các bản vẽ trong danh sách (tự kiểm tra).</summary>
    public IReadOnlyList<DrawingRef> Drawings => _refsList;
    public string CurrentKey => _cur?.Key;
    /// <summary>Bản vẽ đang hiển thị (đã áp chỉnh sửa / bản khoá).</summary>
    public DxfWriter Shown => _shown;
    public OverlayReport Report => _rep;

    public bool SelectDrawing(string key)
    {
        var d = _refsList.FirstOrDefault(x => x.Key == key);
        if (d == null) return false;
        _list.SelectedItem = d;
        if (!ReferenceEquals(_cur, d)) LoadDrawing(d);
        return true;
    }

    // ==================================================================
    // Dựng bản vẽ
    // ==================================================================
    private DxfWriter Generate(DrawingRef d)
    {
        using (DrawingOverlay.Bypass())
        {
            if (d.Kind == "SHEET") return d.Sheet == null ? null : SheetBuilder.Compose(_p, _r, _d, d.Sheet);
            var items = d.Kind == "SHOP" ? RebarShopDrawing.BuildDrawings(_p, _d, k => k == d.Key) : DrawingSet.BuildDrawings(_p, _d, k => k == d.Key);
            return items.FirstOrDefault(i => i.Key == d.Key)?.W;
        }
    }

    /// <summary>Mã băm bản tự sinh đã áp chỉnh sửa (không dùng bản khoá) – như khi xuất.</summary>
    private string GeneratedHash(DrawingRef d)
    {
        try
        {
            if (d.Kind == "SHEET") return d.Sheet == null ? null : SheetBuilder.GeneratedHash(_p, _r, _d, d.Sheet);
            using (DrawingOverlay.IgnoreFrozen())
            {
                var items = d.Kind == "SHOP" ? RebarShopDrawing.BuildDrawings(_p, _d, k => k == d.Key) : DrawingSet.BuildDrawings(_p, _d, k => k == d.Key);
                return items.FirstOrDefault(i => i.Key == d.Key)?.W?.ContentHash();
            }
        }
        catch (Exception) { return null; }
    }

    private void LoadDrawing(DrawingRef d)
    {
        _cur = d;
        _sel.Clear();
        _undo.Clear(); _redo.Clear();
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            _base = Generate(d);
            if (_base == null || _base.EntityCount == 0)
            {
                _base = new DxfWriter(1);
                _base.Text(Lay.Chu, 0, 0, 5, "(Bản vẽ không có dữ liệu – cần chạy Thiết kế BTCT)");
            }
            _base.EnsureKeys();
            _S = _base.Scale > 0 ? _base.Scale : 1;
            _fz = DrawingOverlay.FrozenOf(_p, d.Key);
            Rebuild(fit: true);
            if (_fz != null)
            {
                string gh = GeneratedHash(d);
                string w = DrawingOverlay.FrozenWarning(_fz, gh);
                _badgeText.Text = (DrawingOverlay.ModelChanged(_fz, gh) ? "⚠ " : "") + w + "  –  Bấm \"Bỏ khoá\" để dựng lại từ mô hình và hiệu chỉnh.";
                _badge.Visibility = Visibility.Visible;
            }
            else _badge.Visibility = Visibility.Collapsed;
            Title = "Hiệu chỉnh bản vẽ – " + d;
            SetStatus($"{d.Number} – {d.Title}: {_shown.EntityCount} thực thể, tỷ lệ 1/{_S.ToString("0.#", Inv)}." +
                      (_fz != null ? " Bản vẽ đang khoá (chỉ xem)." : " Nhấp chọn, kéo để dời; F2 sửa chữ; Del xoá."));
        }
        catch (Exception ex)
        {
            SetStatus("Lỗi dựng bản vẽ: " + ex.Message);
        }
        finally { Mouse.OverrideCursor = null; }
    }

    /// <summary>Áp lại lớp chỉnh sửa lên bản gốc (hoặc hiện bản khoá) và vẽ lại.</summary>
    private void Rebuild(bool fit = false)
    {
        if (_base == null) return;
        _rep = new OverlayReport();
        if (_fz != null)
        {
            _shown = DrawingOverlay.FrozenWriter(_fz) ?? _base.Clone();
            if (_shown.EntityCount > 0 && Math.Abs(_shown.Scale - _S) > 1e-9) _shown.Scale = _S;
        }
        else
        {
            _shown = _base.Clone();
            DrawingOverlay.Apply(_p, _shown, _cur.Key, true, _rep, _cur.Kind != "SHEET");
        }
        _ents = _shown.EntityRefs();
        _primOf = new Dictionary<int, DrawPrim>();
        foreach (var pr in _shown.GetPrimitives()) if (pr.Ent >= 0) _primOf[pr.Ent] = pr;
        if (fit || _pw <= 0)
        {
            var ext = _base.Extents.Union(_shown.Extents);
            if (ext.IsEmpty) ext = new Rect2(0, 0, 100 * _S, 100 * _S);
            double m = 0.03 * Math.Max(ext.W, ext.H) / _S + 5;
            _ox = ext.X1 / _S - m; _oy = ext.Y2 / _S + m;
            _pw = ext.W / _S + 2 * m; _ph = ext.H / _S + 2 * m;
        }
        _staleList.ItemsSource = _rep.Stale.Select(x => x.Describe()).ToList();
        _staleBox.Header = $"Chỉnh sửa không còn áp dụng được ({_rep.Stale.Count})";
        if (_rep.Stale.Count > 0) _staleBox.IsExpanded = true;
        if (fit) { _autoFit = true; Fit(); }
        else RenderAll();
        UpdateBadges();
    }

    private double MinStroke => 0.8 / Math.Max(_zoom, 1e-6);

    private void RenderAll()
    {
        if (_shown == null) return;
        _host.Width = _pw; _host.Height = _ph;
        _host.Drawing = SheetRender.Render(new[] { (_shown, 1.0 / _S, 0.0, 0.0) }, _ox, _oy, MinStroke, w => w.GetPrimitives());
        _host.InvalidateVisual();
        ApplyTransform();
    }

    private void ApplyTransform()
    {
        var mt = new MatrixTransform(_zoom, 0, 0, _zoom, _panX, _panY);
        _host.RenderTransform = mt;
        _selHost.RenderTransform = mt;
        _selHost.Width = _pw; _selHost.Height = _ph;
        RenderSelection();
    }

    private void Fit()
    {
        double w = _view.ActualWidth, h = _view.ActualHeight;
        if (w < 50 || h < 50 || _pw <= 0 || _ph <= 0) { RenderAll(); return; }
        _zoom = Math.Min((w - 20) / _pw, (h - 20) / _ph);
        _panX = (w - _pw * _zoom) / 2; _panY = (h - _ph * _zoom) / 2;
        _autoFit = true;
        RenderAll();
    }

    // ---- toạ độ: mô hình (x,y) ↔ giấy (mm, y xuống) ↔ màn hình ----
    private Point ToScreen(double x, double y) => new((x / _S - _ox) * _zoom + _panX, (_oy - y / _S) * _zoom + _panY);
    private (double X, double Y) ToModel(Point s) => ((((s.X - _panX) / _zoom) + _ox) * _S, (_oy - (s.Y - _panY) / _zoom) * _S);

    // ==================================================================
    // Chọn
    // ==================================================================
    private static double SegDist(double px, double py, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1, l2 = dx * dx + dy * dy;
        double t = l2 < 1e-18 ? 0 : Math.Clamp(((px - x1) * dx + (py - y1) * dy) / l2, 0, 1);
        double qx = x1 + t * dx - px, qy = y1 + t * dy - py;
        return Math.Sqrt(qx * qx + qy * qy);
    }

    private double Dist(EntityRef e, double x, double y, double tol)
    {
        var b = e.Box;
        if (b.IsEmpty || x < b.X1 - tol || x > b.X2 + tol || y < b.Y1 - tol || y > b.Y2 + tol) return double.MaxValue;
        if (!_primOf.TryGetValue(e.Index, out var pr)) return 0.5 * tol;
        switch (pr.Kind)
        {
            case PrimKind.Line:
                return SegDist(x, y, pr.Pts[0], pr.Pts[1], pr.Pts[2], pr.Pts[3]);
            case PrimKind.Polyline:
            {
                double best = double.MaxValue;
                int n = pr.Pts.Length / 2;
                for (int i = 0; i + 1 < n; i++) best = Math.Min(best, SegDist(x, y, pr.Pts[2 * i], pr.Pts[2 * i + 1], pr.Pts[2 * i + 2], pr.Pts[2 * i + 3]));
                if (pr.Closed && n > 2) best = Math.Min(best, SegDist(x, y, pr.Pts[2 * n - 2], pr.Pts[2 * n - 1], pr.Pts[0], pr.Pts[1]));
                return best;
            }
            case PrimKind.Circle:
            case PrimKind.Arc:
            {
                double dx = x - pr.Pts[0], dy = y - pr.Pts[1];
                return Math.Abs(Math.Sqrt(dx * dx + dy * dy) - pr.R);
            }
            case PrimKind.Hatch:
                return 0.95 * tol;   // tô vật liệu: chỉ chọn khi không có nét nào gần hơn
            default:
                return 0.3 * tol;   // chữ, miền tô: trong khung bao
        }
    }

    private EntityRef HitTest(Point screen)
    {
        var (x, y) = ToModel(screen);
        double tol = 5 / _zoom * _S;
        EntityRef best = null;
        double bd = double.MaxValue, barea = double.MaxValue;
        foreach (var e in _ents)
        {
            double d = Dist(e, x, y, tol);
            if (d > tol) continue;
            double area = e.Box.W * e.Box.H;
            bool better = d < bd - 1e-9 || (Math.Abs(d - bd) < 1e-9 && area < barea);
            if (e.Type == "TEXT" && d <= 0.3 * tol + 1e-9 && (best == null || best.Type != "TEXT")) better = true;
            if (better) { best = e; bd = d; barea = area; }
        }
        return best;
    }

    private (string Owner, string Key, string Group) UnitOf(EntityRef e, bool group) =>
        (DrawingOverlay.OwnerOf(e, _cur?.Key), e.Key, group && !string.IsNullOrEmpty(e.Group) ? e.Group : null);

    private IEnumerable<EntityRef> Members((string Owner, string Key, string Group) u) =>
        _ents.Where(e => DrawingOverlay.OwnerOf(e, _cur?.Key) == u.Owner && (u.Group != null ? e.Group == u.Group : e.Key == u.Key));

    private bool IsSelected(EntityRef e) => _sel.Any(u => DrawingOverlay.OwnerOf(e, _cur?.Key) == u.Owner && (u.Group != null ? e.Group == u.Group : e.Key == u.Key));

    private void RenderSelection()
    {
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0, 120, 255)), 1.5 / _zoom) { DashStyle = DashStyles.Dash };
            var fill = new SolidColorBrush(Color.FromArgb(40, 0, 120, 255));
            double ox = _dragging ? _dragD.X : 0, oy = _dragging ? _dragD.Y : 0;
            foreach (var u in _sel)
            {
                var r = Rect2.Empty;
                foreach (var e in Members(u)) r = r.Union(e.Box);
                if (r.IsEmpty) continue;
                r = r.Offset(ox, oy);
                double x1 = r.X1 / _S - _ox, y1 = _oy - r.Y2 / _S, w = Math.Max(r.W / _S, 0.5), h = Math.Max(r.H / _S, 0.5);
                double pad = 3 / _zoom;
                dc.DrawRectangle(fill, pen, new Rect(x1 - pad, y1 - pad, w + 2 * pad, h + 2 * pad));
            }
            dc.DrawRectangle(null, null, new Rect(0, 0, Math.Max(1, _pw), Math.Max(1, _ph)));
        }
        _selHost.Drawing = dg;
        _selHost.InvalidateVisual();
        if (_sel.Count > 0 && !_dragging)
        {
            var first = _sel.SelectMany(Members).FirstOrDefault();
            if (first != null)
                SetStatus($"Chọn {_sel.Count}: {first.Type} lớp {first.Layer}" + (first.Text != null ? $" \"{first.Text}\"" : "") +
                          (first.Group != null ? $" – nhóm {first.Group}" : "") + $" – hình {DrawingOverlay.OwnerOf(first, _cur?.Key)}");
        }
    }

    /// <summary>Chọn thực thể đầu tiên thoả điều kiện (tự kiểm tra). group: chọn cả nhóm.</summary>
    public bool SelectWhere(Func<EntityRef, bool> pred, bool group = true)
    {
        var e = _ents.FirstOrDefault(pred);
        _sel.Clear();
        if (e == null) { RenderSelection(); return false; }
        _sel.Add(UnitOf(e, group));
        RenderSelection();
        return true;
    }

    private bool ReadOnlyView()
    {
        if (_fz == null) return false;
        SetStatus("Bản vẽ đang khoá (đã nạp bản sửa trong CAD) – bấm \"Bỏ khoá\" để hiệu chỉnh trên bản tự sinh.");
        return true;
    }

    // ==================================================================
    // Thao tác
    // ==================================================================
    private void PushUndo()
    {
        _undo.Push(DrawingOverlay.Snapshot(_p));
        _redo.Clear();
    }

    private void Changed()
    {
        _markDirty?.Invoke();
        Rebuild();
    }

    /// <summary>Dời các đối tượng đang chọn (mm giấy).</summary>
    public void MoveSelection(double dxMm, double dyMm) => MoveSelectionModel(dxMm * _S, dyMm * _S);

    private void MoveSelectionModel(double dx, double dy)
    {
        if (_sel.Count == 0 || ReadOnlyView()) return;
        PushUndo();
        foreach (var u in _sel.ToList())
        {
            var e = Members(u).FirstOrDefault();
            if (e != null) DrawingOverlay.RecordMove(_p, _cur.Key, e, u.Group != null, dx, dy, _S);
        }
        Changed();
        SetStatus($"Đã dời {_sel.Count} đối tượng ({(dx / _S).ToString("0.#", Inv)}; {(dy / _S).ToString("0.#", Inv)}) mm.");
    }

    /// <summary>Sửa chữ của đối tượng đang chọn (tự kiểm tra). Trả về false nếu không có chữ.</summary>
    public bool EditSelectedText(string text)
    {
        if (_sel.Count == 0 || ReadOnlyView()) return false;
        var t = Members(_sel[0]).Where(e => e.Type == "TEXT").OrderByDescending(e => (e.Text ?? "").Length).FirstOrDefault();
        if (t == null) return false;
        PushUndo();
        DrawingOverlay.RecordText(_p, _cur.Key, t, text);
        Changed();
        return true;
    }

    private void EditTextUi()
    {
        if (_sel.Count == 0) { SetStatus("Chọn một chữ (hoặc nhãn) trước."); return; }
        var t = Members(_sel[0]).Where(e => e.Type == "TEXT").OrderByDescending(e => (e.Text ?? "").Length).FirstOrDefault();
        if (t == null) { SetStatus("Đối tượng đang chọn không có chữ."); return; }
        string s = DialogUi.InputBox(this, "Sửa chữ", "Nội dung mới:", t.Text);
        if (s != null) EditSelectedText(s);
    }

    private void DeleteSelection()
    {
        if (_sel.Count == 0 || ReadOnlyView()) return;
        PushUndo();
        foreach (var u in _sel.ToList())
        {
            var e = Members(u).FirstOrDefault();
            if (e != null) DrawingOverlay.RecordDelete(_p, _cur.Key, e, u.Group != null);
        }
        int n = _sel.Count;
        _sel.Clear();
        Changed();
        SetStatus($"Đã xoá {n} đối tượng (hoàn tác: Ctrl+Z).");
    }

    private void RestyleUi()
    {
        if (_sel.Count == 0 || ReadOnlyView()) { if (_sel.Count == 0) SetStatus("Chọn đối tượng trước."); return; }
        string s = DialogUi.InputBox(this, "Lớp / màu", "Lớp mới và màu ACI, ngăn bằng dấu ; (vd. \"CHU;1\" hoặc \";3\"; màu 0 = theo lớp):", ";1");
        if (s == null) return;
        var parts = s.Split(';');
        string layer = parts[0].Trim();
        int aci = parts.Length > 1 && int.TryParse(parts[1].Trim(), NumberStyles.Integer, Inv, out int c) ? c : -1;
        PushUndo();
        foreach (var u in _sel.ToList())
        {
            var e = Members(u).FirstOrDefault();
            if (e != null) DrawingOverlay.RecordRestyle(_p, _cur.Key, e, u.Group != null, layer.Length > 0 ? layer : null, aci);
        }
        Changed();
    }

    private void AddItem(SheetItem it)
    {
        if (ReadOnlyView()) return;
        PushUndo();
        DrawingOverlay.AddAnnotation(_p, _cur.Key, it);
        Changed();
    }

    public void Undo()
    {
        if (_undo.Count == 0) { SetStatus("Không còn bước để hoàn tác."); return; }
        _redo.Push(DrawingOverlay.Snapshot(_p));
        DrawingOverlay.Restore(_p, _undo.Pop());
        Changed();
        SetStatus("Đã hoàn tác.");
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(DrawingOverlay.Snapshot(_p));
        DrawingOverlay.Restore(_p, _redo.Pop());
        Changed();
        SetStatus("Đã làm lại.");
    }

    private void ResetDrawing()
    {
        if (_cur == null) return;
        var (ed, nt) = DrawingOverlay.MadeIn(_p, _cur.Key);
        if (ed.Count + nt.Count == 0) { SetStatus("Bản vẽ chưa có chỉnh sửa."); return; }
        if (MessageBox.Show(this, $"Xoá {ed.Count + nt.Count} chỉnh sửa tạo trên bản vẽ {_cur.Number} và trở về bản tự sinh?", Title,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        PushUndo();
        DrawingOverlay.ClearEdits(_p, _cur.Key);
        _sel.Clear();
        Changed();
    }

    private void RemoveStale()
    {
        if (_rep.Stale.Count == 0) return;
        PushUndo();
        int n = DrawingOverlay.RemoveEdits(_p, _rep.Stale);
        Changed();
        SetStatus($"Đã xoá {n} chỉnh sửa không còn áp dụng được.");
    }

    // ==================================================================
    // Xuất, khoá
    // ==================================================================
    private IDisposable LayerScope(DxfWriter w)
    {
        var mw = Application.Current?.MainWindow as MainWindow;
        return mw?.BeginDxfExport(ExportLayerProbe.ForWriters(new[] { w }), "Xuất bản vẽ đã hiệu chỉnh", this);
    }

    private void ExportCurrentUi()
    {
        if (_cur == null || _shown == null) return;
        var dlg = new SaveFileDialog { Filter = "Bản vẽ DXF (*.dxf)|*.dxf", FileName = _cur.FileName, Title = "Xuất bản vẽ đã hiệu chỉnh" };
        if (dlg.ShowDialog(this) != true) return;
        var scope = LayerScope(_shown);
        if (Application.Current?.MainWindow is MainWindow && scope == null) return;
        try
        {
            using (scope)
            {
                if (_fz != null) DrawingOverlay.WriteFrozen(_fz, dlg.FileName);
                else _shown.Save(dlg.FileName);
            }
            SetStatus("Đã xuất " + dlg.FileName + ". Sửa tiếp trong AutoCAD rồi \"Nạp lại bản vẽ đã chỉnh sửa\" để khoá bản vẽ.");
        }
        catch (Exception ex) { MessageBox.Show(this, "Lỗi xuất DXF: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    /// <summary>Chọn bản vẽ có tên file trùng (bỏ phần mở rộng, không phân biệt hoa thường) – khi nạp lại file đã sửa.</summary>
    public bool SelectByFileName(string path)
    {
        string nm = Path.GetFileNameWithoutExtension(path ?? "");
        var d = _refsList.FirstOrDefault(x => string.Equals(Path.GetFileNameWithoutExtension(x.FileName), nm, StringComparison.OrdinalIgnoreCase));
        return d != null && SelectDrawing(d.Key);
    }

    private void AttachUi()
    {
        var dlg = new OpenFileDialog { Filter = CadFileReaders.OpenFilter, Title = "Nạp lại bản vẽ đã chỉnh sửa trong CAD" };
        if (dlg.ShowDialog(this) != true) return;
        AttachFile(dlg.FileName, true);
    }

    /// <summary>Khoá bản vẽ bằng file DXF/DWG đã sửa trong CAD. Tên file trùng bản vẽ nào thì chọn bản đó; ask: hỏi xác nhận.</summary>
    public bool AttachFile(string path, bool ask)
    {
        SelectByFileName(path);
        if (_cur == null) return false;
        if (ask && MessageBox.Show(this, $"Khoá bản vẽ {_cur.Number} – {_cur.Title} bằng file\n{path}?\n\nCác lần xuất, in sau sẽ dùng file này thay cho bản tự sinh tới khi \"Bỏ khoá\".",
                Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return false;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var fz = DrawingOverlay.Freeze(_p, _cur.Key, _cur.Title, path, GeneratedHash(_cur), _S, out string err);
            if (fz == null) { MessageBox.Show(this, "Không nạp được bản vẽ:\n" + err, Title, MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
            DrawingOverlay.FrozenWriter(fz, out var warns);
            _markDirty?.Invoke();
            var d = _cur; _cur = null;
            LoadDrawing(d);
            SetStatus($"Đã khoá {d.Number} bằng {fz.FileName}." + (warns.Count > 0 ? " " + string.Join(" ", warns) : ""));
            return true;
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private void UnlockUi()
    {
        if (_cur == null || _fz == null) { SetStatus("Bản vẽ đang chọn không bị khoá."); return; }
        if (MessageBox.Show(this, $"Bỏ khoá {_cur.Number}? Bản vẽ sẽ được dựng lại từ mô hình (kèm các chỉnh sửa trong chương trình); file đã nạp bị bỏ.",
                Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        DrawingOverlay.Unfreeze(_p, _cur.Key);
        _markDirty?.Invoke();
        var d = _cur; _cur = null;
        LoadDrawing(d);
    }

    // ==================================================================
    // Chuột, phím
    // ==================================================================
    private void View_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var s = e.GetPosition(_view);
        double f = e.Delta > 0 ? 1.2 : 1 / 1.2;
        double nz = Math.Clamp(_zoom * f, 1e-4, 1e4);
        f = nz / _zoom;
        _panX = s.X - (s.X - _panX) * f;
        _panY = s.Y - (s.Y - _panY) * f;
        _zoom = nz;
        _autoFit = false;
        ApplyTransform();
        _redraw.Stop(); _redraw.Start();
        e.Handled = true;
    }

    private void View_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _view.Focus();
        var pt = e.GetPosition(_view);
        if (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right)
        {
            _panning = true; _panStart = pt; _panX0 = _panX; _panY0 = _panY;
            _view.CaptureMouse();
            return;
        }
        if (e.ChangedButton != MouseButton.Left || _shown == null) return;
        _down = pt;
        _downModel = ToModel(pt);
        _dragD = (0, 0);
        _view.CaptureMouse();
        if (_tSelect.IsChecked != true) return;
        var hit = HitTest(pt);
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (hit == null) { if (!ctrl) _sel.Clear(); RenderSelection(); return; }
        if (!IsSelected(hit))
        {
            if (!ctrl) _sel.Clear();
            _sel.Add(UnitOf(hit, _chkGroup.IsChecked == true));
        }
        else if (ctrl)
        {
            _sel.RemoveAll(u => Members(u).Any(m => m.Index == hit.Index));
        }
        RenderSelection();
    }

    private void View_MouseMove(object sender, MouseEventArgs e)
    {
        var pt = e.GetPosition(_view);
        if (_panning)
        {
            _panX = _panX0 + pt.X - _panStart.X; _panY = _panY0 + pt.Y - _panStart.Y;
            _autoFit = false;
            ApplyTransform();
            return;
        }
        if (_down == null || e.LeftButton != MouseButtonState.Pressed) return;
        if (_tSelect.IsChecked == true && _sel.Count > 0 && _fz == null)
        {
            if (!_dragging && (Math.Abs(pt.X - _down.Value.X) > 3 || Math.Abs(pt.Y - _down.Value.Y) > 3)) _dragging = true;
            if (_dragging)
            {
                var m = ToModel(pt);
                _dragD = (m.X - _downModel.X, m.Y - _downModel.Y);
                RenderSelection();
            }
        }
    }

    private void View_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _view.ReleaseMouseCapture();
        if (_panning) { _panning = false; return; }
        if (_down == null) return;
        var pt = e.GetPosition(_view);
        var m = ToModel(pt);
        bool wasDrag = _dragging;
        var d = _dragD;
        _down = null; _dragging = false; _dragD = (0, 0);
        if (_tSelect.IsChecked == true)
        {
            if (wasDrag && (Math.Abs(d.X) > 1e-9 || Math.Abs(d.Y) > 1e-9)) MoveSelectionModel(d.X, d.Y);
            else RenderSelection();
            return;
        }
        if (ReadOnlyView()) return;
        // đối tượng thêm: toạ độ mm giấy của bản vẽ = mô hình / tỷ lệ
        double x1 = _downModel.X / _S, y1 = _downModel.Y / _S, x2 = m.X / _S, y2 = m.Y / _S;
        double w = Math.Abs(x2 - x1), h = Math.Abs(y2 - y1);
        if (_tText.IsChecked == true)
        {
            string s = DialogUi.InputBox(this, "Thêm chữ", "Nội dung:", "");
            if (s != null) AddItem(SheetBuilder.NewText(x1, y1, s, 3.5));
        }
        else if (_tArrow.IsChecked == true || _tLine.IsChecked == true)
        {
            if (w + h > 1) AddItem(SheetBuilder.NewLine(x1, y1, x2, y2, _tArrow.IsChecked == true));
        }
        else if (_tCloud.IsChecked == true)
        {
            if (w > 2 && h > 2) AddItem(SheetBuilder.NewCloud(Math.Min(x1, x2), Math.Min(y1, y2), w, h));
        }
        else if (_tRect.IsChecked == true)
        {
            if (w > 1 && h > 1) AddItem(SheetBuilder.NewRect(Math.Min(x1, x2), Math.Min(y1, y2), w, h));
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox) return;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10 : 1;
        switch (e.Key)
        {
            case Key.Delete: DeleteSelection(); e.Handled = true; break;
            case Key.F2: EditTextUi(); e.Handled = true; break;
            case Key.Z when ctrl: Undo(); e.Handled = true; break;
            case Key.Y when ctrl: Redo(); e.Handled = true; break;
            case Key.Left when _sel.Count > 0: MoveSelection(-step, 0); e.Handled = true; break;
            case Key.Right when _sel.Count > 0: MoveSelection(step, 0); e.Handled = true; break;
            case Key.Up when _sel.Count > 0: MoveSelection(0, step); e.Handled = true; break;
            case Key.Down when _sel.Count > 0: MoveSelection(0, -step); e.Handled = true; break;
        }
    }
}
