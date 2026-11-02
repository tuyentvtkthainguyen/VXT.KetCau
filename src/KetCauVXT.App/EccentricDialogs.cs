using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>[MóngLệchTâm] Hộp thoại "Ranh giới đất…": chọn các cạnh ranh giới (trái / phải / trước / sau) theo trục + khoảng cách ra ngoài trục
/// (hoặc toạ độ), khoảng hở mép móng – ranh giới, dung sai nhận cột sát ranh, phương án móng lệch tâm mặc định; xem trước trên mặt bằng.</summary>
internal sealed partial class BoundaryDialog : Window   // [LùiCọcRanh] partial: EccentricDialogs.Setback.cs
{
    private sealed class SideRow
    {
        public BoundarySide Side;
        public CheckBox Chk;
        public ComboBox Grid;
        public TextBox Offset, Coord;
    }

    private const string ByCoord = "(toạ độ)";
    private readonly Project _p;
    private readonly List<SideRow> _rows = new();
    private readonly TextBox _tClear, _tTol, _tSpan, _tBeta;
    private readonly ComboBox _cbScheme;
    private readonly Canvas _cv = new() { Width = 420, Height = 360, Background = Brushes.White, ClipToBounds = true };
    private readonly TextBlock _info = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 0) };

    /// <summary>Thông số móng sau khi bấm Đồng ý (bản sao đã sửa)</summary>
    public FoundationSettings Result { get; private set; }

    public BoundaryDialog(Project p)
    {
        _p = p;
        var fs = p.Foundation ?? new FoundationSettings();
        DialogUi.Setup(this, "Ranh giới đất – móng lệch tâm", 900, 560);
        var root = new DockPanel { Margin = new Thickness(10) };
        var left = new StackPanel { Width = 440 };
        left.Children.Add(DialogUi.Label("Các cạnh ranh giới đất (móng không được vượt ra ngoài)", double.NaN, true));
        var head = new Grid();
        foreach (double w in new[] { 130.0, 110, 100, 90 }) head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        void H(string t, int c) { var tb = DialogUi.Label(t); tb.FontWeight = FontWeights.SemiBold; Grid.SetColumn(tb, c); head.Children.Add(tb); }
        H("Cạnh", 0); H("Theo trục", 1); H("Ra ngoài trục (mm)", 2); H("Toạ độ (m)", 3);
        left.Children.Add(head);
        foreach (BoundarySide side in Enum.GetValues(typeof(BoundarySide)))
        {
            var cur = fs.Boundaries?.FirstOrDefault(b => b != null && b.Side == side);
            bool vert = side == BoundarySide.Left || side == BoundarySide.Right;
            var grids = (vert ? p.GridsX : p.GridsY).ToList();
            var names = new List<string> { ByCoord };
            names.AddRange(grids.Select(g => g.Name));
            var def = side switch
            {
                BoundarySide.Left or BoundarySide.Front => grids.FirstOrDefault(),
                _ => grids.LastOrDefault()
            };
            var r = new SideRow
            {
                Side = side,
                Chk = new CheckBox { Content = PropertyBoundary.SideName(side), IsChecked = cur?.Enabled == true, VerticalAlignment = VerticalAlignment.Center },
                Grid = new ComboBox { ItemsSource = names, Width = 100 },
                Offset = new TextBox { Width = 90, Text = DialogUi.F((cur?.Offset ?? DefaultOffset(p, def, vert)) * 1000, "0") },
                Coord = new TextBox { Width = 80, Text = DialogUi.F(cur?.Coord ?? (def?.Coord ?? 0), "0.###") }
            };
            string g0 = cur != null ? (string.IsNullOrWhiteSpace(cur.Grid) ? ByCoord : cur.Grid) : def?.Name ?? ByCoord;
            r.Grid.SelectedIndex = Math.Max(0, names.IndexOf(g0));
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            foreach (double w in new[] { 130.0, 110, 100, 90 }) row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
            Grid.SetColumn(r.Chk, 0); Grid.SetColumn(r.Grid, 1); Grid.SetColumn(r.Offset, 2); Grid.SetColumn(r.Coord, 3);
            row.Children.Add(r.Chk); row.Children.Add(r.Grid); row.Children.Add(r.Offset); row.Children.Add(r.Coord);
            left.Children.Add(row);
            r.Chk.Checked += (s, e) => Preview(); r.Chk.Unchecked += (s, e) => Preview();
            r.Grid.SelectionChanged += (s, e) => { r.Coord.IsEnabled = (r.Grid.SelectedItem as string) == ByCoord; Preview(); };
            r.Offset.TextChanged += (s, e) => Preview(); r.Coord.TextChanged += (s, e) => Preview();
            r.Coord.IsEnabled = (r.Grid.SelectedItem as string) == ByCoord;
            _rows.Add(r);
        }
        left.Children.Add(DialogUi.Hint("Ranh giới = toạ độ trục + khoảng cách ra phía ngoài lô đất (vd. cột 220 tim trục sát ranh: 110 mm). Chọn '(toạ độ)' để nhập toạ độ tuyệt đối."));
        _tClear = Box(fs.BoundaryClearance * 1000); _tTol = Box(fs.BoundaryTolerance * 1000);
        _tSpan = Box(fs.StrapMaxSpan, "0.##"); _tBeta = Box(fs.StrapReliefFactor, "0.##");
        _cbScheme = new ComboBox
        {
            ItemsSource = new[] { EccentricScheme.Auto, EccentricScheme.Strap, EccentricScheme.SelfBalanced, EccentricScheme.Combined }.Select(EccentricOverride.SchemeName).ToList(),
            Width = 220, SelectedIndex = fs.EccentricDefault switch { EccentricScheme.Strap => 1, EccentricScheme.SelfBalanced => 2, EccentricScheme.Combined => 3, _ => 0 }
        };
        left.Children.Add(Line("Khoảng hở mép móng – ranh giới (mm)", _tClear));
        left.Children.Add(Line("Dung sai nhận cột sát ranh (mm)", _tTol));
        left.Children.Add(Line("Phương án móng sát ranh mặc định", _cbScheme));
        left.Children.Add(Line("Nhịp lớn nhất nối dầm cân bằng (m)", _tSpan));
        left.Children.Add(Line("β giảm tải móng cột trong (≤ 0,5)", _tBeta));
        left.Children.Add(DialogUi.Hint("Tự chọn: có cột trong cùng trục trong phạm vi nhịp trên → (a) dầm móng cân bằng (móng kết hợp nếu 2 móng gần chạm nhau); không có → (b) tự cân bằng. "
                                        + "Chọn riêng từng chân cột bằng nút 'Lệch tâm…' trên tab Mô hình móng."));
        foreach (var tb in new[] { _tClear, _tTol }) tb.TextChanged += (s, e) => Preview();
        AddSetbackUi(left, fs);   // [LùiCọcRanh] khoảng lùi ép cọc + tuỳ chọn mép đài (EccentricDialogs.Setback.cs)

        var ok = DialogUi.Btn("Đồng ý", (s, e) => { Result = Build(); DialogResult = true; }, true);
        ok.IsDefault = true;
        var none = DialogUi.Btn("Bỏ ranh giới", (s, e) => { foreach (var r in _rows) r.Chk.IsChecked = false; Preview(); });
        var cancel = DialogUi.Btn("Huỷ", (s, e) => DialogResult = false);
        cancel.IsCancel = true;
        var br = DialogUi.Row(ok, none, cancel);
        br.HorizontalAlignment = HorizontalAlignment.Right;
        br.Margin = new Thickness(0, 8, 0, 0);
        DockPanel.SetDock(br, Dock.Bottom);
        root.Children.Add(br);
        DockPanel.SetDock(left, Dock.Left);
        root.Children.Add(left);
        var right = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        right.Children.Add(DialogUi.Label("Xem trước mặt bằng (nét đứt đỏ: ranh giới đất, gạch chéo phía ngoài lô đất; cột sát ranh tô cam)", double.NaN, true));
        right.Children.Add(new Border { Child = _cv, BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1) });
        right.Children.Add(_info);
        root.Children.Add(right);
        Content = root;
        Loaded += (s, e) => Preview();
    }

    /// <summary>Khoảng cách mặc định từ trục ra ranh giới = nửa cạnh cột lớn nhất trên trục (ranh giới tại mép ngoài cột).</summary>
    private static double DefaultOffset(Project p, GridLine g, bool vert)
    {
        if (g == null) return 0;
        double best = 0;
        foreach (var (n, col) in FoundationUtil.BaseColumns(p))
        {
            if (Math.Abs((vert ? n.X : n.Y) - g.Coord) > 0.05) continue;
            try
            {
                var sec = p.Section(col.Section);
                best = Math.Max(best, Math.Max(sec.B, sec.H) / 2);
            }
            catch (Exception) { }
        }
        return best;
    }

    private static TextBox Box(double v, string fmt = "0") => new() { Text = DialogUi.F(v, fmt), Width = 90, VerticalContentAlignment = VerticalAlignment.Center };

    private static DockPanel Line(string label, FrameworkElement ctl)
    {
        var d = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        d.Children.Add(DialogUi.Label(label, 230));
        ctl.HorizontalAlignment = HorizontalAlignment.Left;
        d.Children.Add(ctl);
        return d;
    }

    /// <summary>Thông số móng mới (bản sao) theo các ô nhập.</summary>
    public FoundationSettings Build()
    {
        var fs = JsonSerializer.Deserialize<FoundationSettings>(JsonSerializer.Serialize(_p.Foundation ?? new FoundationSettings(), Project.JsonOptions), Project.JsonOptions);
        fs.Boundaries = new List<PropertyBoundary>();
        foreach (var r in _rows)
        {
            string g = r.Grid.SelectedItem as string ?? ByCoord;
            var b = new PropertyBoundary
            {
                Side = r.Side, Enabled = r.Chk.IsChecked == true, Grid = g == ByCoord ? "" : g,
                Offset = DialogUi.ParseNum(r.Offset.Text, 0) / 1000, Coord = DialogUi.ParseNum(r.Coord.Text, 0)
            };
            if (b.Enabled) fs.Boundaries.Add(b);
        }
        fs.BoundaryClearance = Math.Max(0, DialogUi.ParseNum(_tClear.Text, 0) / 1000);
        fs.BoundaryTolerance = Math.Max(0, DialogUi.ParseNum(_tTol.Text, 150) / 1000);
        fs.StrapMaxSpan = Math.Max(0.5, DialogUi.ParseNum(_tSpan.Text, 6));
        fs.StrapReliefFactor = Math.Clamp(DialogUi.ParseNum(_tBeta.Text, 0.5), 0, 1);
        fs.EccentricDefault = _cbScheme.SelectedIndex switch { 1 => EccentricScheme.Strap, 2 => EccentricScheme.SelfBalanced, 3 => EccentricScheme.Combined, _ => EccentricScheme.Auto };
        ReadSetback(fs);   // [LùiCọcRanh]
        return fs;
    }

    /// <summary>Đặt cạnh ranh giới (dùng cho tự kiểm tra).</summary>
    public void SetSide(BoundarySide side, bool on)
    {
        var r = _rows.FirstOrDefault(x => x.Side == side);
        if (r != null) r.Chk.IsChecked = on;
    }

    private void Preview()
    {
        if (_cv == null || _rows.Count < 4) return;
        _cv.Children.Clear();
        FoundationSettings fs;
        try { fs = Build(); } catch (Exception) { return; }
        var save = _p.Foundation;
        List<(PropertyBoundary B, double Coord)> lines;
        var cols = FoundationUtil.BaseColumns(_p);
        var edge = new HashSet<int>();
        try
        {
            _p.Foundation = fs;
            lines = EccentricFooting.Lines(_p);
            foreach (var (n, col) in cols)
            {
                double hc = 0.22, bc = 0.22;
                try { var sec = _p.Section(col.Section); hc = sec.H; bc = sec.B; } catch (Exception) { }
                var lim = EccentricFooting.Limits(_p, n.X, n.Y, hc, bc);
                if (lim.EdgeX || lim.EdgeY) edge.Add(n.Id);
            }
        }
        finally { _p.Foundation = save; }
        var xs = cols.Select(c => c.Node.X).Concat(_p.GridsX.Select(g => g.Coord)).Concat(lines.Where(l => l.B.IsVertical).Select(l => l.Coord)).DefaultIfEmpty(0).ToList();
        var ys = cols.Select(c => c.Node.Y).Concat(_p.GridsY.Select(g => g.Coord)).Concat(lines.Where(l => !l.B.IsVertical).Select(l => l.Coord)).DefaultIfEmpty(0).ToList();
        double x0 = xs.Min() - 1, x1 = xs.Max() + 1, y0 = ys.Min() - 1, y1 = ys.Max() + 1;
        double sc = Math.Min((_cv.Width - 20) / Math.Max(x1 - x0, 1), (_cv.Height - 20) / Math.Max(y1 - y0, 1));
        Point S(double x, double y) => new(10 + (x - x0) * sc, _cv.Height - 10 - (y - y0) * sc);
        void Ln(Point a, Point b, Brush br, double t, DoubleCollection dash = null)
        {
            var l = new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = br, StrokeThickness = t };
            if (dash != null) l.StrokeDashArray = dash;
            _cv.Children.Add(l);
        }
        foreach (var g in _p.GridsX) Ln(S(g.Coord, y0), S(g.Coord, y1), Brushes.LightGray, 0.8, new DoubleCollection { 6, 3 });
        foreach (var g in _p.GridsY) Ln(S(x0, g.Coord), S(x1, g.Coord), Brushes.LightGray, 0.8, new DoubleCollection { 6, 3 });
        foreach (var (n, col) in cols)
        {
            var c = S(n.X, n.Y);
            var r = new Shapes.Rectangle { Width = 8, Height = 8, Fill = edge.Contains(n.Id) ? Brushes.DarkOrange : Brushes.DimGray };
            Canvas.SetLeft(r, c.X - 4); Canvas.SetTop(r, c.Y - 4);
            _cv.Children.Add(r);
        }
        foreach (var (b, c) in lines)
        {
            int o = -b.Inward;
            if (b.IsVertical)
            {
                Ln(S(c, y0), S(c, y1), Brushes.Red, 1.6, new DoubleCollection { 5, 3 });
                for (double y = y0; y < y1; y += 0.4) Ln(S(c, y), S(c + o * 0.3, y + 0.3), Brushes.IndianRed, 0.6);
            }
            else
            {
                Ln(S(x0, c), S(x1, c), Brushes.Red, 1.6, new DoubleCollection { 5, 3 });
                for (double x = x0; x < x1; x += 0.4) Ln(S(x, c), S(x + 0.3, c + o * 0.3), Brushes.IndianRed, 0.6);
            }
        }
        _info.Text = lines.Count == 0 ? "Chưa chọn cạnh ranh giới nào – móng đặt tâm tại tim cột như bình thường."
            : $"{lines.Count} cạnh ranh giới: " + string.Join("; ", lines.Select(l => $"{PropertyBoundary.SideName(l.B.Side)} {(l.B.IsVertical ? "x" : "y")} = {l.Coord:0.000} m"))
              + $". {edge.Count} chân cột sát ranh (móng lệch tâm).";
    }
}

/// <summary>[MóngLệchTâm] Hộp thoại "Lệch tâm…" cho một chân cột (không chặn): phương án (a) dầm cân bằng / (b) tự cân bằng / (c) móng kết hợp,
/// độ lệch tâm tự động theo ranh giới hoặc nhập tay, cột nối dầm, tiết diện dầm; thiết kế thử trực tiếp → áp lực, phương án, dầm DM; Áp dụng có hoàn tác.</summary>
internal sealed class EccentricFootingDialog : Window
{
    private readonly Project _p;
    private readonly Func<AnalysisResults> _res;
    private readonly int _node;
    private readonly Func<int, EccentricOverride, Task> _apply;
    private readonly ComboBox _cbScheme = new() { Width = 240 };
    private readonly RadioButton _rAuto = new() { Content = "Tự động theo ranh giới đất (móng sát ranh)", GroupName = "ecc", Margin = new Thickness(0, 2, 0, 2) };
    private readonly RadioButton _rManual = new() { Content = "Nhập độ lệch tâm tâm móng – tim cột", GroupName = "ecc", Margin = new Thickness(0, 2, 0, 2) };
    private readonly TextBox _tEx, _tEy, _tB, _tH;
    private readonly ComboBox _cbPx = new() { Width = 160 }, _cbPy = new() { Width = 160 };
    private readonly Canvas _cv = new() { Width = 360, Height = 300, Background = Brushes.White, ClipToBounds = true };
    private readonly TextBlock _sum = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
    private readonly TextBlock _detail = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly List<int> _cand = new();
    private bool _loading = true, _working;

    private static readonly EccentricScheme[] Schemes = { EccentricScheme.Auto, EccentricScheme.Strap, EccentricScheme.SelfBalanced, EccentricScheme.Combined, EccentricScheme.None };

    /// <summary>Móng thử gần nhất (FootingDesign / PileCapDesign) và tóm tắt (nhật ký tự kiểm tra)</summary>
    public MemberDesignBase Trial { get; private set; }
    public string Summary => _sum.Text;

    public EccentricFootingDialog(Project p, Func<AnalysisResults> res, int node, Func<int, EccentricOverride, Task> apply)
    {
        _p = p; _res = res; _node = node; _apply = apply;
        var n = p.Nodes.FirstOrDefault(x => x.Id == node);
        DialogUi.Setup(this, $"Móng lệch tâm – chân cột nút {node}" + (n != null ? " (" + GridText(p, n) + ")" : ""), 1080, 640);
        var cur = EccentricFooting.Find(p.Foundation, node) ?? new EccentricOverride { NodeId = node };
        _cbScheme.ItemsSource = Schemes.Select(EccentricOverride.SchemeName).ToList();
        _cbScheme.SelectedIndex = Math.Max(0, Array.IndexOf(Schemes, cur.Scheme));
        _rAuto.IsChecked = cur.AutoOffset; _rManual.IsChecked = !cur.AutoOffset;
        _tEx = Box(cur.OffsetX * 1000); _tEy = Box(cur.OffsetY * 1000); _tB = Box(cur.StrapB * 1000); _tH = Box(cur.StrapH * 1000);
        // cột có thể nối dầm / gộp móng: cùng trục X hoặc Y
        var cands = new List<string> { "Tự tìm" };
        foreach (var (bn, _) in FoundationUtil.BaseColumns(p))
            if (n != null && bn.Id != node && (Math.Abs(bn.Y - n.Y) <= EccentricFooting.LineTol || Math.Abs(bn.X - n.X) <= EccentricFooting.LineTol))
            {
                _cand.Add(bn.Id);
                cands.Add($"Nút {bn.Id} ({GridText(p, bn)})");
            }
        _cbPx.ItemsSource = cands; _cbPy.ItemsSource = cands.ToList();
        _cbPx.SelectedIndex = Math.Max(0, _cand.IndexOf(cur.PartnerX) + 1);
        _cbPy.SelectedIndex = Math.Max(0, _cand.IndexOf(cur.PartnerY) + 1);

        var par = new StackPanel { Width = 380, Margin = new Thickness(0, 0, 10, 0) };
        par.Children.Add(DialogUi.Label("Phương án móng lệch tâm", double.NaN, true));
        par.Children.Add(_cbScheme);
        par.Children.Add(DialogUi.Hint("(a) dầm móng cân bằng nối sang móng cột trong: áp lực đáy móng biên đều, R1 = N1·L/(L − e); (b) đáy móng chịu toàn bộ M = N·e0, mô men ΔM truyền vào cột/giằng; "
                                       + "(c) gộp cột biên và cột trong vào một móng có trọng tâm trùng hợp lực."));
        par.Children.Add(DialogUi.Label("Độ lệch tâm", double.NaN, true));
        par.Children.Add(_rAuto);
        par.Children.Add(_rManual);
        par.Children.Add(Line("ex = tâm móng − tim cột theo X (mm)", _tEx));
        par.Children.Add(Line("ey theo Y (mm)", _tEy));
        par.Children.Add(DialogUi.Label("Dầm cân bằng / móng kết hợp", double.NaN, true));
        par.Children.Add(Line("Nối theo X với", _cbPx));
        par.Children.Add(Line("Nối theo Y với", _cbPy));
        par.Children.Add(Line("Dầm DM: b (mm, 0 = tự chọn)", _tB));
        par.Children.Add(Line("Dầm DM: h (mm, 0 = tự chọn)", _tH));
        foreach (var tb in new[] { _tEx, _tEy, _tB, _tH }) tb.TextChanged += (s, e) => Schedule();
        _cbScheme.SelectionChanged += (s, e) => Schedule();
        _cbPx.SelectionChanged += (s, e) => Schedule(); _cbPy.SelectionChanged += (s, e) => Schedule();
        _rAuto.Checked += (s, e) => { UpdateEnabled(); Schedule(); }; _rManual.Checked += (s, e) => { UpdateEnabled(); Schedule(); };

        var right = new DockPanel();
        var head = new StackPanel();
        head.Children.Add(_sum);
        head.Children.Add(new Border { Child = _cv, BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left });
        DockPanel.SetDock(head, Dock.Top);
        right.Children.Add(head);
        var bApply = DialogUi.Btn("Áp dụng", async (s, e) => await ApplyAsync(), true);
        var bAuto = DialogUi.Btn("Tự động (bỏ lựa chọn riêng)", async (s, e) => await ApplyAsync(clear: true));
        var bClose = DialogUi.Btn("Đóng", (s, e) => Close());
        bClose.IsCancel = true;
        var br = DialogUi.Row(bApply, bAuto, bClose);
        br.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(br, Dock.Bottom);
        right.Children.Add(br);
        right.Children.Add(new ScrollViewer { Content = _detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 6, 0, 6) });
        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(par, Dock.Left);
        root.Children.Add(par);
        root.Children.Add(right);
        Content = root;
        _timer.Tick += (s, e) => { _timer.Stop(); Evaluate(); };
        Loaded += (s, e) => { _loading = false; UpdateEnabled(); Evaluate(); };
        Closed += (s, e) => _timer.Stop();
    }

    private static string GridText(Project p, Node n)
    {
        string gx = DesignNear(p.GridsX, n.X), gy = DesignNear(p.GridsY, n.Y);
        return gx != null && gy != null ? gx + "-" + gy : $"x = {n.X:0.00}, y = {n.Y:0.00}";
    }

    private static string DesignNear(IEnumerable<GridLine> g, double c) => g.Where(q => Math.Abs(q.Coord - c) <= 0.3).OrderBy(q => Math.Abs(q.Coord - c)).Select(q => q.Name).FirstOrDefault();

    private static TextBox Box(double v) => new() { Text = DialogUi.F(v, "0"), Width = 80, VerticalContentAlignment = VerticalAlignment.Center };

    private static DockPanel Line(string label, FrameworkElement ctl)
    {
        var d = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        d.Children.Add(DialogUi.Label(label, 210));
        ctl.HorizontalAlignment = HorizontalAlignment.Left;
        d.Children.Add(ctl);
        return d;
    }

    private void UpdateEnabled() { _tEx.IsEnabled = _rManual.IsChecked == true; _tEy.IsEnabled = _rManual.IsChecked == true; }

    private void Schedule()
    {
        if (_loading) return;
        _timer.Stop(); _timer.Start();
    }

    /// <summary>Lựa chọn theo các ô nhập hiện tại.</summary>
    public EccentricOverride Current() => new()
    {
        NodeId = _node, Scheme = Schemes[Math.Clamp(_cbScheme.SelectedIndex, 0, Schemes.Length - 1)], AutoOffset = _rManual.IsChecked != true,
        OffsetX = DialogUi.ParseNum(_tEx.Text, 0) / 1000, OffsetY = DialogUi.ParseNum(_tEy.Text, 0) / 1000,
        PartnerX = _cbPx.SelectedIndex > 0 ? _cand[_cbPx.SelectedIndex - 1] : 0, PartnerY = _cbPy.SelectedIndex > 0 ? _cand[_cbPy.SelectedIndex - 1] : 0,
        StrapB = Math.Max(0, DialogUi.ParseNum(_tB.Text, 0) / 1000), StrapH = Math.Max(0, DialogUi.ParseNum(_tH.Text, 0) / 1000)
    };

    /// <summary>Chọn phương án (dùng cho tự kiểm tra) và thiết kế thử ngay.</summary>
    public void SelectScheme(EccentricScheme s)
    {
        _cbScheme.SelectedIndex = Math.Max(0, Array.IndexOf(Schemes, s));
        _timer.Stop();
        Evaluate();
    }

    /// <summary>Thiết kế thử móng (đơn / đài cọc) với bản sao thông số móng có lựa chọn đang nhập; cập nhật kết quả + hình phác.</summary>
    public void Evaluate()
    {
        var res = _res();
        if (res == null) { _sum.Text = "Chưa có kết quả phân tích – bấm 'Thiết kế móng' trước."; return; }
        var save = _p.Foundation;
        try
        {
            var fs = JsonSerializer.Deserialize<FoundationSettings>(JsonSerializer.Serialize(_p.Foundation, Project.JsonOptions), Project.JsonOptions);
            EccentricFooting.Set(fs, new[] { _node }, Current());
            _p.Foundation = fs;
            var w = new List<string>();
            var s = new DesignSummary();
            bool pile = FoundationUtil.TypeOf(_p, _node) == FoundationType.Pile;
            if (pile) s.PileCaps = PileCapDesigner.Design(_p, res, w);
            else s.Footings = FootingDesigner.Design(_p, res, w);
            s.StrapBeams = EccentricFooting.DesignStraps(_p, s, w);
            string key = "M:" + _node.ToString(System.Globalization.CultureInfo.InvariantCulture);   // = FootingDesigner.MemberKey (internal)
            var fd = s.Footings.FirstOrDefault(f => f.NodeId == _node || f.RebarKeys.Contains(key) || (f.Eccentric?.Locations.Any(q => q.NodeId == _node) ?? false));
            var pc = s.PileCaps.FirstOrDefault(c => c.NodeIds.Contains(_node));
            var straps = s.StrapBeams.Where(b => b.Links.Any(l => l.NodeA == _node || l.NodeB == _node)).ToList();
            Trial = (MemberDesignBase)fd ?? pc;
            var e = fd?.Eccentric ?? pc?.Eccentric;
            if (Trial == null) { _sum.Text = "Không tìm thấy móng tại nút này (" + string.Join("; ", w.Take(2)) + ")"; _detail.Text = ""; _cv.Children.Clear(); return; }
            var sb = new System.Text.StringBuilder();
            if (fd != null)
                _sum.Text = $"{(e?.Combined != null ? "Móng kết hợp" : "Móng")} {fd.L:0.00}×{fd.B:0.00}×{fd.H:0.00} m; e = ({(e?.OffsetX ?? 0) * 1000:0}; {(e?.OffsetY ?? 0) * 1000:0}) mm; "
                            + $"ptb = {fd.Ptb:0.0}, pmax = {(fd.Pmax >= 1e8 ? "∞" : fd.Pmax.ToString("0.0"))}, pmin = {fd.Pmin:0.0} kN/m², R = {fd.R:0.0} → {(fd.Ok ? "ĐẠT" : "KHÔNG ĐẠT")}";
            else
                _sum.Text = $"Đài {pc.Piles.Count} cọc {pc.L:0.00}×{pc.B:0.00}×{pc.H:0.00} m; e = ({pc.OffsetX * 1000:0}; {pc.OffsetY * 1000:0}) mm; Pmax = {pc.PMax:0.0} / [P] = {pc.PileCapacity:0} kN → {(pc.Ok ? "ĐẠT" : "KHÔNG ĐẠT")}";
            _sum.Foreground = Trial.Ok ? Brushes.DarkGreen : Brushes.DarkRed;
            if (e != null)
            {
                sb.AppendLine($"Phương án: {EccentricOverride.SchemeName(e.Scheme)}   (đề xuất: {EccentricOverride.SchemeName(e.Recommended)})");
                if (!string.IsNullOrEmpty(e.Recommendation)) sb.AppendLine("Lý do: " + e.Recommendation);
                if (!string.IsNullOrEmpty(e.Sides)) sb.AppendLine("Ranh giới: " + e.Sides);
                if (e.Scheme == EccentricScheme.Strap) sb.AppendLine($"Đòn bẩy: N1 = {e.N1:0.0} kN → R1 = {e.R1:0.0} kN (ΔR = {e.DeltaR:0.0} kN)");
                if (e.ColumnMomentX > 0 || e.ColumnMomentY > 0) sb.AppendLine($"Mô men lệch tâm truyền vào cột / giằng: ΔMx = {e.ColumnMomentX:0.0}, ΔMy = {e.ColumnMomentY:0.0} kN.m");
                if (e.Contact < 1 - 1e-6) sb.AppendLine($"Tách nền một phần: vùng tiếp xúc {e.Contact * 100:0}%");
                foreach (var o in e.Options) sb.AppendLine("• " + o);
            }
            else sb.AppendLine("Móng không lệch tâm (đặt tâm tại tim cột).");
            foreach (var st in straps)
                sb.AppendLine($"Dầm cân bằng {st.Name} {st.B * 1000:0}×{st.H * 1000:0}: M = {st.M:0.0} kN.m, Q = {st.V:0.0} kN, trên {st.TopBars}, dưới {st.BottomBars}, đai {st.Stirrups} – {(st.Ok ? "đạt" : "KHÔNG ĐẠT")}");
            foreach (var c in Trial.Checks) sb.AppendLine($"  {c.Name}: {c.Demand:0.##} / {c.Capacity:0.##} {c.Unit} → {c.Ratio:0.00}{(c.Ok ? "" : " KHÔNG ĐẠT")}");
            foreach (var m in Trial.Messages.Where(m => m.StartsWith("LỖI") || m.StartsWith("Cảnh báo") || m.StartsWith("Móng lệch tâm") || m.StartsWith("Dầm móng") || m.StartsWith("Đài lệch tâm")))
                sb.AppendLine("• " + m);
            _detail.Text = sb.ToString();
            DrawPlan(fd, pc, e, straps);
        }
        catch (Exception ex)
        {
            Trial = null;
            _sum.Text = "Lỗi thiết kế thử: " + ex.Message;
            _sum.Foreground = Brushes.DarkRed;
        }
        finally { _p.Foundation = save; }
    }

    private void DrawPlan(FootingDesign fd, PileCapDesign pc, EccentricInfo e, List<StrapBeamDesign> straps)
    {
        _cv.Children.Clear();
        var n = _p.Nodes.FirstOrDefault(x => x.Id == _node);
        if (n == null) return;
        double L = fd?.L ?? pc.L, B = fd?.B ?? pc.B, ox = e?.OffsetX ?? pc?.OffsetX ?? 0, oy = e?.OffsetY ?? pc?.OffsetY ?? 0;
        double ext = Math.Max(L, B) * 1.9 + 0.5;
        double sc = (Math.Min(_cv.Width, _cv.Height) - 20) / ext, cx = _cv.Width / 2, cy = _cv.Height / 2;
        Point S(double x, double y) => new(cx + (x - n.X) * sc, cy - (y - n.Y) * sc);
        void Rect(double x0, double y0, double x1, double y1, Brush fill, Brush stroke, double th, DoubleCollection dash = null)
        {
            var a = S(Math.Min(x0, x1), Math.Max(y0, y1)); var b = S(Math.Max(x0, x1), Math.Min(y0, y1));
            var r = new Shapes.Rectangle { Width = Math.Max(1, b.X - a.X), Height = Math.Max(1, b.Y - a.Y), Fill = fill, Stroke = stroke, StrokeThickness = th };
            if (dash != null) r.StrokeDashArray = dash;
            Canvas.SetLeft(r, a.X); Canvas.SetTop(r, a.Y);
            _cv.Children.Add(r);
        }
        var ok = Trial?.Ok == true ? Brushes.DarkGreen : Brushes.DarkRed;
        if (e?.Combined != null)
        {
            var poly = new Shapes.Polygon { Fill = new SolidColorBrush(Color.FromRgb(236, 236, 230)), Stroke = ok, StrokeThickness = 1.6 };
            foreach (var q in e.Combined.Outline) poly.Points.Add(S(q.X, q.Y));
            _cv.Children.Add(poly);
            Rect(e.Combined.XB - 0.11, e.Combined.YB - 0.11, e.Combined.XB + 0.11, e.Combined.YB + 0.11, Brushes.DimGray, Brushes.Black, 1);
        }
        else Rect(n.X + ox - L / 2, n.Y + oy - B / 2, n.X + ox + L / 2, n.Y + oy + B / 2, new SolidColorBrush(Color.FromRgb(236, 236, 230)), ok, 1.6);
        if (pc != null)
            foreach (var (px, py) in pc.Piles)
            {
                var c = S(n.X + px, n.Y + py);
                double d = Math.Max(5, pc.PileSize * sc);
                var el = new Shapes.Ellipse { Width = d, Height = d, Stroke = Brushes.Black, Fill = Brushes.White };
                Canvas.SetLeft(el, c.X - d / 2); Canvas.SetTop(el, c.Y - d / 2);
                _cv.Children.Add(el);
            }
        double hc = fd?.ColumnH ?? pc.ColumnH, bc = fd?.ColumnB ?? pc.ColumnB;
        Rect(n.X - hc / 2, n.Y - bc / 2, n.X + hc / 2, n.Y + bc / 2, Brushes.DimGray, Brushes.Black, 1);
        foreach (var st in straps)
            foreach (var l in st.Links.Where(q => q.NodeA == _node || q.NodeB == _node))
            {
                double dir = l.AlongX ? Math.Sign(l.XB - l.XA) : Math.Sign(l.YB - l.YA);
                if (l.AlongX) Rect(l.XA + dir * l.FaceA, l.YA - st.B / 2, l.XA + dir * l.FaceB, l.YA + st.B / 2, Brushes.LightSteelBlue, Brushes.SteelBlue, 1);
                else Rect(l.XA - st.B / 2, l.YA + dir * l.FaceA, l.XA + st.B / 2, l.YA + dir * l.FaceB, Brushes.LightSteelBlue, Brushes.SteelBlue, 1);
            }
        foreach (var (b, c) in EccentricFooting.Lines(_p))
        {
            Point a, bb;
            if (b.IsVertical) { a = S(c, n.Y - ext); bb = S(c, n.Y + ext); }
            else { a = S(n.X - ext, c); bb = S(n.X + ext, c); }
            _cv.Children.Add(new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = bb.X, Y2 = bb.Y, Stroke = Brushes.Red, StrokeThickness = 1.6, StrokeDashArray = new DoubleCollection { 5, 3 } });
        }
        var cap = new TextBlock { Text = "Nét đứt đỏ: ranh giới đất; xanh: dầm cân bằng; ô xám đậm: cột", FontSize = 9.5, Foreground = Brushes.DimGray };
        Canvas.SetLeft(cap, 4); Canvas.SetTop(cap, _cv.Height - 16);
        _cv.Children.Add(cap);
    }

    /// <summary>Áp dụng lựa chọn (clear = true: bỏ lựa chọn riêng → tự động) rồi thiết kế lại móng.</summary>
    public async Task ApplyAsync(bool clear = false)
    {
        if (_working) return;
        _working = true;
        try
        {
            await _apply(_node, clear ? null : Current());
            if (clear)
            {
                _loading = true;
                _cbScheme.SelectedIndex = 0; _rAuto.IsChecked = true; _cbPx.SelectedIndex = 0; _cbPy.SelectedIndex = 0;
                foreach (var tb in new[] { _tEx, _tEy, _tB, _tH }) tb.Text = "0";
                _loading = false;
            }
            Evaluate();
        }
        finally { _working = false; }
    }
}
