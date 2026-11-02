using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using KetCauVXT.Analysis;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;
using Material = KetCauVXT.Model.Material;

namespace KetCauVXT.App;

/// <summary>Tab "Biểu đồ nội lực": biểu đồ M3, M2, V2, V3, N, T trên mặt bằng tầng, mặt đứng theo trục hoặc 3D.
/// Quy ước: biểu đồ mô men vẽ về phía thớ chịu kéo (M3 &gt; 0 căng dưới); đỏ = dương, xanh = âm.
/// Tổ hợp bao (BAO): vẽ cả đường max và min.</summary>
public partial class MainWindow
{
    private static readonly (string Name, int Comp, string Unit)[] DiaQty =
    {
        ("Mô men M3", 5, "kN.m"), ("Mô men M2", 4, "kN.m"),
        ("Lực cắt V2", 1, "kN"), ("Lực cắt V3", 2, "kN"),
        ("Lực dọc N", 0, "kN"), ("Mô men xoắn T", 3, "kN.m")
    };
    private static readonly string[] DiaViews = { "Mặt bằng tầng", "Mặt đứng theo trục", "3D" };

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly Brush PosFill = Frozen(Color.FromArgb(80, 220, 20, 60));
    private static readonly Brush NegFill = Frozen(Color.FromArgb(80, 65, 105, 225));
    private static readonly Brush PosStroke = Frozen(Color.FromRgb(200, 20, 50));
    private static readonly Brush NegStroke = Frozen(Color.FromRgb(50, 90, 210));
    private static readonly Brush DiaGridBrush = Frozen(Color.FromRgb(200, 60, 60));
    private static readonly Brush DiaColBrush = Frozen(Color.FromRgb(90, 90, 100));
    private static readonly Brush DiaBeamBrush = Frozen(Color.FromRgb(40, 40, 50));
    private static readonly Brush DiaSteelBrush = Frozen(Color.FromRgb(200, 120, 20));
    private static readonly Brush DiaSelBrush = Frozen(Color.FromRgb(255, 0, 200));
    private static readonly Brush DiaLevelBrush = Frozen(Color.FromRgb(120, 120, 120));
    private static readonly Brush TextBg = Frozen(Color.FromArgb(190, 255, 255, 255));
    private static readonly Effect GlowEffect = MakeGlow();

    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    private static Effect MakeGlow()
    {
        var e = new DropShadowEffect { Color = Color.FromRgb(255, 235, 0), ShadowDepth = 0, BlurRadius = 16, Opacity = 1 };
        e.Freeze();
        return e;
    }

    // ---- trạng thái chung các khung nhìn phụ ----
    private bool _secInit, _secPending, _inSecondaryRedraw, _need3DRebuild, _diaGridUpdating;
    private readonly Dictionary<GeometryModel3D, PickMesh> _pickDia = new();

    // ---- biến đổi 2D của tab biểu đồ: sx = _dOx + x·_dScale ; sy = _dOy − y·_dScale ----
    private double _dScale = 40, _dOx = 60, _dOy = 400;
    private bool _dFitPending = true, _dPanning, _dDragged;
    private Point _dPanStart;
    private double _dPanOx, _dPanOy;
    private Project _diaProj;
    private bool _dia3DFitted;

    private List<GridLine> _diaGrids = new();
    private string _diaGridSig = "";
    private DiaData _diaCache;
    private readonly List<DiaHit> _diaHits = new();

    /// <summary>Một thanh trong khung nhìn 2D của biểu đồ.</summary>
    private sealed class DiaItem
    {
        public FrameElement F;
        public Point A, B;            // hai đầu thanh trong hệ toạ độ 2D của khung nhìn (m)
        public bool AsColumn;         // mặt bằng: cột vẽ ô chữ nhật tại A
        public double W, H;           // kích thước ô cột trên mặt bằng (m)
        public Point[] Base;          // điểm trên trục thanh tại các mặt cắt (m)
        public Vector U;              // phương tung độ (vectơ đơn vị) trong 2D
        public double[][] S;          // 1 đường (hoặc 2: max, min) giá trị theo phương U
        public double Sigma = 1;      // S·Sigma = giá trị hiển thị (đỏ/xanh)
        public double ColN = double.NaN;
        public Vector[] Us;           // dầm cong: phương tung độ riêng tại từng mặt cắt (pháp tuyến cung); null = dùng U
        public Point[] Poly;          // dầm cong: đường gấp khúc trục dầm (m); null = đoạn thẳng A–B
    }

    private sealed class DiaData
    {
        public object Res, Proj;
        public string Key = "";
        public int Mode;
        public List<DiaItem> Items = new();
        public double MaxAbs, Span = 3;
        public bool HasForces;
        public string PlaneName = "";
        public List<(double z, string name)> Levels = new();
        public List<(double h, string name)> Cross = new();
        public List<Point> Supports = new();
    }

    private readonly struct DiaHit
    {
        public DiaHit(Point a, Point b, double rx, double ry, bool rect, FrameElement f) { A = a; B = b; Rx = rx; Ry = ry; Rect = rect; F = f; }
        public Point A { get; }
        public Point B { get; }
        public double Rx { get; }
        public double Ry { get; }
        public bool Rect { get; }
        public FrameElement F { get; }
    }

    private int DiaMode => Math.Max(0, cbDiaView.SelectedIndex);
    private int DiaQtyIndex => Math.Clamp(cbDiaQty.SelectedIndex, 0, DiaQty.Length - 1);

    // ==================================================================
    // KHỞI TẠO + ĐIỀU PHỐI VẼ LẠI
    // ==================================================================
    private void EnsureSecondaryInit()
    {
        if (_secInit) return;
        _secInit = true;
        cbDiaQty.ItemsSource = DiaQty.Select(q => q.Name).ToList();
        cbDiaQty.SelectedIndex = 0;
        cbDiaView.ItemsSource = DiaViews;
        cbDiaView.SelectedIndex = 0;
        // nghe thêm (không thay handler sẵn có) các ô chọn trên thanh trên cùng
        cbDiagramCase.SelectionChanged += (s, e) => RedrawSecondaryViews();
        cbStory.SelectionChanged += (s, e) => { if (DiaMode == 2) _need3DRebuild = true; RedrawSecondaryViews(); };
        UpdateDiaViewVisibility();
    }

    private void InvalidateDiagramCache() => _diaCache = null;

    /// <summary>Vẽ lại các khung nhìn phụ (gộp nhiều yêu cầu liên tiếp thành một lần vẽ, chỉ vẽ khung đang hiện).</summary>
    partial void RedrawSecondaryViews()
    {
        if (_secPending) return;
        _secPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(DoRedrawSecondary));
    }

    private void DoRedrawSecondary()
    {
        _secPending = false;
        if (_p == null || !IsLoaded) return;
        _inSecondaryRedraw = true;
        try
        {
            EnsureSecondaryInit();
            bool stale = !ReferenceEquals(_b3dRes, _res) || !ReferenceEquals(_b3dDesign, _design) || !ReferenceEquals(_b3dProj, _p);
            if (stale || _need3DRebuild)
            {
                _need3DRebuild = false;
                BuildMain3D(stale);
            }
            RefreshDiaGridList();
            if (diaHost.IsVisible || diaView3DHost.IsVisible) RedrawDiagramView();
            if (fndHost.IsVisible || fndSecHost.IsVisible) RedrawFoundation();
        }
        catch (Exception ex) { SetStatus("Không vẽ được khung nhìn phụ: " + ex.Message); }
        finally { _inSecondaryRedraw = false; }
    }

    private void RefreshDiaGridList()
    {
        var gl = _p.GridsY.Concat(_p.GridsX).ToList();
        string sig = string.Join(";", gl.Select(g => g.Name + "|" + g.Dir + "|" + g.Coord.ToString("R", Inv)));
        if (sig == _diaGridSig) return;
        _diaGridSig = sig;
        string old = cbDiaGrid.SelectedItem as string;
        var items = gl.Select(GridText).ToList();
        _diaGrids = gl;
        _diaGridUpdating = true;
        try
        {
            cbDiaGrid.ItemsSource = items;
            int k = old != null ? items.IndexOf(old) : -1;
            cbDiaGrid.SelectedIndex = items.Count == 0 ? -1 : Math.Max(k, 0);
        }
        finally { _diaGridUpdating = false; }
        if (DiaMode == 1) _dFitPending = true;
    }

    private static string GridText(GridLine g) =>
        g.Dir == GridDir.Y ? $"Trục {g.Name} (Y = {g.Coord.ToString("0.00", Inv)})" : $"Trục {g.Name} (X = {g.Coord.ToString("0.00", Inv)})";

    private GridLine SelectedDiaGrid()
    {
        int i = cbDiaGrid.SelectedIndex;
        return i >= 0 && i < _diaGrids.Count ? _diaGrids[i] : null;
    }

    // ---- sự kiện XAML ----
    private void DiaOption_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_secInit || _diaGridUpdating) return;
        if (ReferenceEquals(sender, cbDiaView) || ReferenceEquals(sender, cbDiaGrid)) _dFitPending = true;
        if (ReferenceEquals(sender, cbDiaView)) { _dia3DFitted = false; UpdateDiaViewVisibility(); }
        RedrawSecondaryViews();
    }

    private void DiaScale_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_secInit) return;
        if (txtDiaScale != null) txtDiaScale.Text = "×" + e.NewValue.ToString("0.0", Inv);
        RedrawSecondaryViews();
    }

    private void DiaValues_Changed(object sender, RoutedEventArgs e) { if (_secInit) RedrawSecondaryViews(); }

    private void BtnDiaFit_Click(object sender, RoutedEventArgs e)
    {
        if (DiaMode == 2) { FitCameraTarget(true); UpdateCamera(); return; }
        _dFitPending = true;
        RedrawDiagramView();
    }

    private void DiaHost_SizeChanged(object sender, SizeChangedEventArgs e) { if (_secInit && diaHost.IsVisible) RedrawDiagramView(); }

    private void SecondaryHost_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is bool v && v) RedrawSecondaryViews();
    }

    private void UpdateDiaViewVisibility()
    {
        bool is3D = DiaMode == 2;
        diaHost.Visibility = is3D ? Visibility.Collapsed : Visibility.Visible;
        diaView3DHost.Visibility = is3D ? Visibility.Visible : Visibility.Collapsed;
        cbDiaGrid.IsEnabled = DiaMode == 1;
    }

    // ==================================================================
    // DỮ LIỆU BIỂU ĐỒ (2D)
    // ==================================================================
    private static double Val(FrameForces f, int i, int comp) => f.F[i][comp];

    /// <summary>Vị trí tương đối (0..1) của mặt cắt i trên thanh.</summary>
    private static double RelStation(FrameForces f, int i)
    {
        double s0 = f.Stations[0], s1 = f.Stations[^1];
        return s1 - s0 > 1e-12 ? (f.Stations[i] - s0) / (s1 - s0) : (f.Count > 1 ? (double)i / (f.Count - 1) : 0);
    }

    /// <summary>Kích thước cột trên mặt bằng theo X và Y (m), kể cả cột xoay góc bất kỳ (hình bao).</summary>
    private (double dx, double dy) ColSizeXY(FrameElement f, Node ni, Node nj, Dictionary<string, FrameSection> secs)
    {
        double b = 0.22, h = 0.22;
        if (secs.TryGetValue(f.Section ?? "", out var sec)) { var sp = sec.Props(0.5); b = Math.Max(sp.Width, 0.05); h = Math.Max(sp.Depth, 0.05); }
        if (ni == null || nj == null || (nj.P - ni.P).Length < 1e-9) return (h, b);
        var (_, e2, e3) = Axes(ni.P, nj.P, f.Angle);
        return (Math.Abs(e2.X) * h + Math.Abs(e3.X) * b, Math.Abs(e2.Y) * h + Math.Abs(e3.Y) * b);
    }

    private DiaData GetDiaData()
    {
        int mode = DiaMode, qi = DiaQtyIndex;
        string c = cbDiagramCase.SelectedItem as string ?? "";
        var g = SelectedDiaGrid();
        string key = $"{mode}|{qi}|{c}|{(g == null ? "" : g.Name + g.Dir + g.Coord.ToString("R", Inv))}|{CurrentStory}|{_p.Frames.Count}|{_p.Nodes.Count}|{_p.Stories.Count}";
        if (_diaCache != null && ReferenceEquals(_diaCache.Res, _res) && ReferenceEquals(_diaCache.Proj, _p) && _diaCache.Key == key)
            return _diaCache;
        var d = mode == 1 ? BuildElevationData(g, qi, c) : BuildPlanData(qi, c);
        d.Res = _res; d.Proj = _p; d.Key = key; d.Mode = mode;
        _diaCache = d;
        return d;
    }

    private void FinishData(DiaData d, List<double> lengths)
    {
        d.MaxAbs = d.Items.Where(it => it.S != null).SelectMany(it => it.S.SelectMany(a => a)).Select(v => Math.Abs(v)).DefaultIfEmpty(0).Max();
        if (lengths.Count > 0)
        {
            lengths.Sort();
            d.Span = Math.Max(lengths[lengths.Count / 2], 0.5);
        }
    }

    private DiaData BuildPlanData(int qi, string caseName)
    {
        var d = new DiaData { HasForces = _res != null && !string.IsNullOrEmpty(caseName) };
        var nodes = NodeMap(); var secs = SectionMap();
        int comp = DiaQty[qi].Comp;
        var lengths = new List<double>();
        foreach (var f in PlanFrames())
        {
            if (!nodes.TryGetValue(f.NodeI, out var ni) || !nodes.TryGetValue(f.NodeJ, out var nj)) continue;
            FrameForces mx = null, mn = null;
            if (d.HasForces) mx = ForcesOf(f.Id, caseName, out mn);
            bool okF = mx != null && mx.Count >= 2 && (mn == null || mn.Count == mx.Count);
            if (f.IsVerticalKind)
            {
                var top = nj.Z >= ni.Z ? nj : ni;
                var (dx, dy) = ColSizeXY(f, ni, nj, secs);
                var it = new DiaItem { F = f, A = new Point(top.X, top.Y), B = new Point(top.X, top.Y), AsColumn = true, W = dx, H = dy };
                if (okF && comp == 0)
                {
                    int iBot = nj.Z >= ni.Z ? 0 : mx.Count - 1;
                    it.ColN = mn != null ? mn.P(iBot) : mx.P(iBot);
                }
                d.Items.Add(it);
                continue;
            }
            var a = new Point(ni.X, ni.Y); var b = new Point(nj.X, nj.Y);
            var item = new DiaItem { F = f, A = a, B = b };
            d.Items.Add(item);
            var dir = b - a;
            if (dir.Length < 1e-6) continue;
            lengths.Add((nj.P - ni.P).Length);
            dir.Normalize();
            var n = new Vector(dir.Y, -dir.X);
            if (n.Y > 1e-9 || (Math.Abs(n.Y) <= 1e-9 && n.X < 0)) n = -n;   // dầm theo X: dương vẽ về phía −Y (phía dưới màn hình)
            item.U = n;
            var arc = SafeArc(f);
            if (arc != null)
            {
                // dầm cong: trục theo cung, tung độ theo pháp tuyến cung (cùng phía quy ước với dây cung)
                item.Poly = arc.Points(ArcDrawSegments(arc)).Select(q => new Point(q.X, q.Y)).ToArray();
                lengths[^1] = arc.Length;
            }
            if (!okF) continue;
            if (arc != null)
            {
                double sgn = n.X * dir.Y - n.Y * dir.X >= 0 ? 1 : -1;   // (t.Y, −t.X)·n tại giữa cung (tiếp tuyến ∥ dây cung)
                item.Base = Enumerable.Range(0, mx.Count).Select(i => { var q = arc.PointAt(RelStation(mx, i)); return new Point(q.X, q.Y); }).ToArray();
                item.Us = Enumerable.Range(0, mx.Count).Select(i => { var t = arc.TangentAt(RelStation(mx, i)); return new Vector(t.Y, -t.X) * sgn; }).ToArray();
            }
            else item.Base = Enumerable.Range(0, mx.Count).Select(i => a + (b - a) * RelStation(mx, i)).ToArray();
            var curves = mn != null ? new[] { mx, mn } : new[] { mx };
            item.S = curves.Select(cf => Enumerable.Range(0, cf.Count).Select(i => Val(cf, i, comp)).ToArray()).ToArray();
        }
        FinishData(d, lengths);
        return d;
    }

    private DiaData BuildElevationData(GridLine g, int qi, string caseName)
    {
        var d = new DiaData { HasForces = _res != null && !string.IsNullOrEmpty(caseName) };
        if (g == null) return d;
        d.PlaneName = "Trục " + g.Name;
        var nodes = NodeMap();
        int comp = DiaQty[qi].Comp;
        // mặt phẳng trục: trục "theo X" là đường X = Coord (song song Oy) → trục ngang = Y
        Vec3 nrm = g.Dir == GridDir.X ? Vec3.UnitX : Vec3.UnitY;
        Vec3 hv = g.Dir == GridDir.X ? Vec3.UnitY : Vec3.UnitX;
        bool InPlane(Node n) => Math.Abs((g.Dir == GridDir.X ? n.X : n.Y) - g.Coord) < 0.01;
        Point To2D(Vec3 v) => new(v.Dot(hv), v.Z);
        Vector Dir2D(Vec3 v) { var r = new Vector(v.Dot(hv), v.Z); if (r.Length > 1e-12) r.Normalize(); return r; }
        var lengths = new List<double>();
        foreach (var f in _p.Frames)
        {
            if (!nodes.TryGetValue(f.NodeI, out var ni) || !nodes.TryGetValue(f.NodeJ, out var nj)) continue;
            if (!InPlane(ni) || !InPlane(nj)) continue;
            double L = (nj.P - ni.P).Length;
            if (L < 1e-6) continue;
            var a = To2D(ni.P); var b = To2D(nj.P);
            var item = new DiaItem { F = f, A = a, B = b };
            d.Items.Add(item);
            lengths.Add(L);
            var (e1, e2, e3) = Axes(ni.P, nj.P, f.Angle);
            Vec3 u; double c2 = 0, c3 = 0;   // s = X2·c2 + X3·c3 với (X2, X3) = (M2, M3) hoặc (V2, V3)
            if (comp == 4 || comp == 5)
            {
                // thành phần mô men theo pháp tuyến mặt phẳng; tung độ về phía thớ căng: e1 × n
                c2 = e2.Dot(nrm); c3 = e3.Dot(nrm);
                u = e1.Cross(nrm);
            }
            else if (comp == 1 || comp == 2)
            {
                // lực cắt trong mặt phẳng: thành phần của V2·e2 + V3·e3 theo phương n × e1
                u = nrm.Cross(e1);
                c2 = e2.Dot(u); c3 = e3.Dot(u);
            }
            else
            {
                u = nrm.Cross(e1);
                if (Math.Abs(u.Z) > 0.5 ? u.Z < 0 : u.Dot(hv) < 0) u = u * -1;
            }
            item.U = Dir2D(u);
            if (comp == 1 || comp == 2 || comp == 4 || comp == 5)
                item.Sigma = Math.Abs(c3) >= Math.Abs(c2) ? (c3 >= 0 ? 1 : -1) : (c2 >= 0 ? 1 : -1);
            if (!d.HasForces) continue;
            var mx = ForcesOf(f.Id, caseName, out var mn);
            if (mx == null || mx.Count < 2 || (mn != null && mn.Count != mx.Count)) continue;
            var arcE = SafeArc(f);   // dầm cong: hình chiếu các điểm trên cung lên mặt phẳng khung
            item.Base = arcE != null
                ? Enumerable.Range(0, mx.Count).Select(i => To2D(arcE.PointAt(RelStation(mx, i)))).ToArray()
                : Enumerable.Range(0, mx.Count).Select(i => a + (b - a) * RelStation(mx, i)).ToArray();
            var curves = mn != null ? new[] { mx, mn } : new[] { mx };
            item.S = curves.Select(cf => Enumerable.Range(0, cf.Count).Select(i =>
                (comp == 4 || comp == 5) ? cf.M2(i) * c2 + cf.M3(i) * c3 :
                (comp == 1 || comp == 2) ? cf.V2(i) * c2 + cf.V3(i) * c3 :
                Val(cf, i, comp)).ToArray()).ToArray();
        }
        foreach (var n in _p.Nodes.Where(q => q.IsSupport && InPlane(q))) d.Supports.Add(To2D(n.P));
        d.Levels.Add((_p.BaseElevation, "Chân cột"));
        foreach (var s in _p.Stories.OrderBy(x => x.Elevation))
            if (Math.Abs(s.Elevation - _p.BaseElevation) > 1e-6) d.Levels.Add((s.Elevation, s.Name));
        foreach (var cg in g.Dir == GridDir.X ? _p.GridsY : _p.GridsX) d.Cross.Add((cg.Coord, cg.Name));
        FinishData(d, lengths);
        return d;
    }

    // ==================================================================
    // VẼ 2D
    // ==================================================================
    private Point DW2S(Point w) => new(_dOx + w.X * _dScale, _dOy - w.Y * _dScale);
    private Point DS2W(Point s) => new((s.X - _dOx) / _dScale, (_dOy - s.Y) / _dScale);

    private void RedrawDiagramView()
    {
        if (_p == null || !_secInit) return;
        if (!ReferenceEquals(_diaProj, _p)) { _diaProj = _p; _dFitPending = true; _dia3DFitted = false; }
        if (DiaMode == 2) { BuildDiagram3D(); return; }
        RedrawDiagram2D();
    }

    private void FitDiagram(DiaData d)
    {
        double w = diaHost.ActualWidth, h = diaHost.ActualHeight;
        if (w < 20 || h < 20) return;
        var xs = new List<double>(); var ys = new List<double>();
        foreach (var it in d.Items)
        {
            xs.Add(it.A.X); xs.Add(it.B.X); ys.Add(it.A.Y); ys.Add(it.B.Y);
            if (it.Poly != null) foreach (var q in it.Poly) { xs.Add(q.X); ys.Add(q.Y); }
        }
        if (d.Mode == 0)
            foreach (var g in _p.Grids) { if (g.Dir == GridDir.X) xs.Add(g.Coord); else ys.Add(g.Coord); }
        else
        {
            foreach (var c in d.Cross) xs.Add(c.h);
            foreach (var l in d.Levels) ys.Add(l.z);
        }
        if (xs.Count == 0) xs.AddRange(new[] { 0.0, 10.0 });
        if (ys.Count == 0) ys.AddRange(new[] { 0.0, 10.0 });
        double x0 = xs.Min(), x1 = xs.Max(), y0 = ys.Min(), y1 = ys.Max();
        double dx = Math.Max(x1 - x0, 1), dy = Math.Max(y1 - y0, 1);
        double ml = d.Mode == 1 ? 130 : 80, mr = 70, mt = 60, mb = 70;
        _dScale = Math.Max(1, Math.Min((w - ml - mr) / dx, (h - mt - mb) / dy));
        _dOx = ml + (w - ml - mr - dx * _dScale) / 2 - x0 * _dScale;
        _dOy = mt + (h - mt - mb + dy * _dScale) / 2 + y0 * _dScale;
        _dFitPending = false;
    }

    private void RedrawDiagram2D()
    {
        diaCanvas.Children.Clear();
        _diaHits.Clear();
        double w = diaHost.ActualWidth, h = diaHost.ActualHeight;
        if (w < 20 || h < 20) return;
        var d = GetDiaData();
        if (_dFitPending) FitDiagram(d);
        var q = DiaQty[DiaQtyIndex];
        string caseName = cbDiagramCase.SelectedItem as string ?? "";

        // ---- nền: lưới trục / cao độ tầng ----
        if (d.Mode == 0) DrawPlanGrids(diaCanvas, (x, y) => DW2S(new Point(x, y)), true);
        else DrawElevationFrame(d);

        // ---- kết cấu ----
        foreach (var it in d.Items)
        {
            bool sel = _sel != null && _sel.Id == it.F.Id;
            if (it.AsColumn)
            {
                var c = DW2S(it.A);
                double rw = Math.Max(5, it.W * _dScale), rh = Math.Max(5, it.H * _dScale);
                var r = new Shapes.Rectangle
                {
                    Width = rw, Height = rh, Fill = sel ? DiaSelBrush : (it.F.IsConcrete ? DiaColBrush : DiaSteelBrush),
                    Stroke = sel ? Brushes.Yellow : Brushes.Black, StrokeThickness = sel ? 2 : 0.8, Effect = sel ? GlowEffect : null
                };
                Canvas.SetLeft(r, c.X - rw / 2); Canvas.SetTop(r, c.Y - rh / 2);
                diaCanvas.Children.Add(r);
                _diaHits.Add(new DiaHit(c, c, rw / 2 + 3, rh / 2 + 3, true, it.F));
                continue;
            }
            var a = DW2S(it.A); var b = DW2S(it.B);
            var br = sel ? DiaSelBrush : it.F.IsVerticalKind ? DiaColBrush : it.F.IsConcrete ? DiaBeamBrush : DiaSteelBrush;
            if (it.Poly != null)
            {
                // dầm cong: đường gấp khúc theo cung
                var pc = new PointCollection(it.Poly.Select(DW2S));
                diaCanvas.Children.Add(new Shapes.Polyline
                {
                    Points = pc, Stroke = br, StrokeThickness = sel ? 4.5 : 2, StrokeLineJoin = PenLineJoin.Round,
                    Effect = sel ? GlowEffect : null, IsHitTestVisible = false
                });
                for (int i = 0; i + 1 < pc.Count; i++) _diaHits.Add(new DiaHit(pc[i], pc[i + 1], 0, 0, false, it.F));
                continue;
            }
            var l = CLine(diaCanvas, a, b, br, sel ? 4.5 : (it.F.IsVerticalKind ? 2.6 : 2), false);
            if (sel) l.Effect = GlowEffect;
            _diaHits.Add(new DiaHit(a, b, 0, 0, false, it.F));
        }
        if (d.Mode == 1)
            foreach (var s in d.Supports) DrawSupport(diaCanvas, DW2S(s));

        // ---- biểu đồ ----
        double k = d.MaxAbs > 1e-9 ? 0.25 * d.Span * sldDiaScale.Value / d.MaxAbs : 0;   // m / (đơn vị nội lực)
        bool values = chkDiaValues.IsChecked == true;
        if (d.HasForces && k > 0)
        {
            var gPos = new StreamGeometry(); var gNeg = new StreamGeometry();
            using (var cp = gPos.Open())
            using (var cn = gNeg.Open())
            {
                foreach (var it in d.Items)
                {
                    if (it.S == null || it.Base == null) continue;
                    var U = new Vector(it.U.X * k * _dScale, -it.U.Y * k * _dScale);   // px / đơn vị
                    var bs = it.Base.Select(DW2S).ToArray();
                    var Uv = ScaledUs(it, U, k);
                    foreach (var s in it.S) EmitStrip2D(cp, cn, bs, s, Uv, it.Sigma);
                }
            }
            gPos.Freeze(); gNeg.Freeze();
            diaCanvas.Children.Add(new Shapes.Path { Data = gPos, Fill = PosFill, Stroke = PosStroke, StrokeThickness = 0.7, IsHitTestVisible = false });
            diaCanvas.Children.Add(new Shapes.Path { Data = gNeg, Fill = NegFill, Stroke = NegStroke, StrokeThickness = 0.7, IsHitTestVisible = false });

            foreach (var it in d.Items)
            {
                if (it.S == null || it.Base == null) continue;
                bool sel = _sel != null && _sel.Id == it.F.Id;
                if (!values && !sel) continue;
                var a = DW2S(it.A); var b = DW2S(it.B);
                if (!sel && (b - a).Length < 28) continue;
                var U = new Vector(it.U.X * k * _dScale, -it.U.Y * k * _dScale);
                var bs = it.Base.Select(DW2S).ToArray();
                LabelCurves(diaCanvas, bs, it.S, ScaledUs(it, U, k), it.Sigma, sel ? 11 : 9.5, sel);
            }
        }
        // lực dọc cột trên mặt bằng
        if (d.Mode == 0 && q.Comp == 0 && d.HasForces)
            foreach (var it in d.Items.Where(i => i.AsColumn && !double.IsNaN(i.ColN)))
            {
                var c = DW2S(it.A);
                double rw = Math.Max(5, it.W * _dScale);
                bool sel = _sel != null && _sel.Id == it.F.Id;
                if (!values && !sel) continue;
                CText(diaCanvas, Fmt(it.ColN), new Point(c.X + rw / 2 + 3, c.Y - 14), it.ColN >= 0 ? PosStroke : NegStroke, 9.5, false, false, true);
            }

        // ---- tiêu đề, chú thích ----
        string place = d.Mode == 0 ? "Mặt bằng: " + CurrentStory : "Mặt đứng " + (string.IsNullOrEmpty(d.PlaneName) ? "(chưa chọn trục)" : d.PlaneName);
        bool isM = q.Comp == 4 || q.Comp == 5, isV = q.Comp == 1 || q.Comp == 2;
        string what = d.Mode == 1 && isM ? "Mô men trong mặt phẳng khung" : (d.Mode == 1 && isV ? "Lực cắt trong mặt phẳng khung" : q.Name);
        bool env = IsEnvelopeCase(caseName);
        CText(diaCanvas, $"{what} ({q.Unit}) – {caseName}{(env ? " (bao: max và min)" : "")} – {place}", new Point(8, 6), Brushes.Black, 12, false);
        if (d.HasForces && d.MaxAbs > 0)
            CText(diaCanvas, $"Giá trị lớn nhất |{q.Name.Split(' ').Last()}| = {Fmt(d.MaxAbs)} {q.Unit}   •   đỏ: dương, xanh: âm" +
                             (isM ? "   •   vẽ về phía thớ chịu kéo" : ""), new Point(8, 24), Brushes.DimGray, 10.5, false);
        if (_res == null)
            CenterHint(diaCanvas, w, h, "Chạy phân tích (F5) để xem biểu đồ nội lực");
        else if (d.Mode == 1 && d.Items.Count == 0)
            CenterHint(diaCanvas, w, h, d.PlaneName == "" ? "Chọn trục để xem mặt đứng" : $"Không có cấu kiện nằm trong mặt phẳng {d.PlaneName}");
    }

    private bool IsEnvelopeCase(string name) =>
        _p != null && _p.Combinations.Any(c => c.Name == name && c.Type == ComboType.Envelope);

    private static string Fmt(double v) => v.ToString(Math.Abs(v) >= 1000 ? "0" : "0.0", Inv);

    /// <summary>Phương tung độ (px / đơn vị) tại từng mặt cắt: dầm cong theo pháp tuyến cung, thanh thẳng dùng chung U.</summary>
    private Vector[] ScaledUs(DiaItem it, Vector U, double k)
    {
        int n = it.Base?.Length ?? 0;
        if (it.Us == null || it.Us.Length != n) return Enumerable.Repeat(U, n).ToArray();
        return it.Us.Select(u => new Vector(u.X * k * _dScale, -u.Y * k * _dScale)).ToArray();
    }

    /// <summary>Thêm các ô biểu đồ (giữa hai mặt cắt liên tiếp) vào hình dương/âm, tách tại điểm đổi dấu.</summary>
    private static void EmitStrip2D(StreamGeometryContext cp, StreamGeometryContext cn, Point[] b, double[] s, Vector U, double sigma) =>
        EmitStrip2D(cp, cn, b, s, Enumerable.Repeat(U, b.Length).ToArray(), sigma);

    /// <summary>Như trên với phương tung độ riêng tại từng mặt cắt (dầm cong).</summary>
    private static void EmitStrip2D(StreamGeometryContext cp, StreamGeometryContext cn, Point[] b, double[] s, Vector[] Uv, double sigma)
    {
        int n = Math.Min(Math.Min(b.Length, s.Length), Uv.Length);
        for (int i = 0; i + 1 < n; i++)
        {
            double s0 = s[i], s1 = s[i + 1];
            if (Math.Abs(s0) < 1e-12 && Math.Abs(s1) < 1e-12) continue;
            Point b0 = b[i], b1 = b[i + 1];
            Vector u0 = Uv[i], u1 = Uv[i + 1];
            if (s0 * s1 < 0)
            {
                double t = s0 / (s0 - s1);
                var bm = b0 + (b1 - b0) * t;
                Poly(s0 * sigma >= 0 ? cp : cn, b0, b0 + u0 * s0, bm);
                Poly(s1 * sigma >= 0 ? cp : cn, bm, b1 + u1 * s1, b1);
            }
            else Poly((s0 + s1) * sigma >= 0 ? cp : cn, b0, b0 + u0 * s0, b1 + u1 * s1, b1);
        }
    }

    private static void Poly(StreamGeometryContext ctx, params Point[] pts)
    {
        ctx.BeginFigure(pts[0], true, true);
        ctx.PolyLineTo(pts.Skip(1).ToList(), true, false);
    }

    /// <summary>Ghi giá trị tại hai đầu và tại cực trị của mỗi đường biểu đồ.</summary>
    private void LabelCurves(Canvas cv, Point[] b, double[][] curves, Vector U, double sigma, double size, bool bold) =>
        LabelCurves(cv, b, curves, Enumerable.Repeat(U, b.Length).ToArray(), sigma, size, bold);

    /// <summary>Như trên với phương tung độ riêng tại từng mặt cắt (dầm cong).</summary>
    private void LabelCurves(Canvas cv, Point[] b, double[][] curves, Vector[] Uv, double sigma, double size, bool bold)
    {
        var done = new List<(int i, double v)>();
        foreach (var s in curves)
        {
            int n = Math.Min(Math.Min(b.Length, s.Length), Uv.Length);
            if (n == 0) continue;
            int iExt = 0;
            for (int i = 1; i < n; i++) if (Math.Abs(s[i]) > Math.Abs(s[iExt])) iExt = i;
            foreach (int i in new[] { 0, n - 1, iExt }.Distinct())
            {
                double v = s[i] * sigma;
                if (i != 0 && i != n - 1 && Math.Abs(s[i]) < 1e-3) continue;
                if (done.Any(x => x.i == i && Math.Abs(x.v - v) < 1e-6)) continue;
                done.Add((i, v));
                var U = Uv[i];
                var dirU = U; if (dirU.Length > 1e-12) dirU.Normalize();
                var tip = b[i] + U * s[i] + dirU * (s[i] >= 0 ? 9 : -9);
                var t = CText(cv, Fmt(v), tip, v >= 0 ? PosStroke : NegStroke, size, true, true, true);
                if (bold) t.FontWeight = FontWeights.Bold;
            }
        }
    }

    private void DrawElevationFrame(DiaData d)
    {
        double hMin = d.Items.Count > 0 ? d.Items.Min(i => Math.Min(i.A.X, i.B.X)) : 0;
        double hMax = d.Items.Count > 0 ? d.Items.Max(i => Math.Max(i.A.X, i.B.X)) : 10;
        if (d.Cross.Count > 0) { hMin = Math.Min(hMin, d.Cross.Min(c => c.h)); hMax = Math.Max(hMax, d.Cross.Max(c => c.h)); }
        double zMin = d.Levels.Count > 0 ? d.Levels.Min(l => l.z) : 0, zMax = d.Levels.Count > 0 ? d.Levels.Max(l => l.z) : 3;
        if (d.Items.Count > 0) zMax = Math.Max(zMax, d.Items.Max(i => Math.Max(i.A.Y, i.B.Y)));
        // cao độ tầng
        foreach (var (z, name) in d.Levels)
        {
            var a = DW2S(new Point(hMin - 1.0, z)); var b = DW2S(new Point(hMax + 1.0, z));
            CLine(diaCanvas, a, b, DiaLevelBrush, 0.6, true);
            CText(diaCanvas, $"{name}  {Elev(z)}", new Point(a.X - 4, a.Y - 7), DiaLevelBrush, 10, false).RenderTransform =
                new TranslateTransform(-MeasureText($"{name}  {Elev(z)}", 10), 0);
        }
        // trục cắt ngang
        double bub = 10;
        foreach (var (hc, name) in d.Cross)
        {
            var a = DW2S(new Point(hc, zMin - 0.9)); var b = DW2S(new Point(hc, zMax + 0.4));
            CLine(diaCanvas, a, b, DiaGridBrush, 0.7, true);
            CBubble(diaCanvas, name, new Point(a.X, a.Y + bub), bub, DiaGridBrush);
        }
        var cr = d.Cross.OrderBy(c => c.h).ToList();
        for (int i = 0; i + 1 < cr.Count; i++)
        {
            var m = DW2S(new Point((cr[i].h + cr[i + 1].h) / 2, zMin - 0.9));
            CText(diaCanvas, ((cr[i + 1].h - cr[i].h) * 1000).ToString("0", Inv), new Point(m.X, m.Y + 4), DiaGridBrush, 10, true);
        }
    }

    private static string Elev(double z) =>
        Math.Abs(z) < 5e-4 ? "±0.000" : (z > 0 ? "+" : "−") + Math.Abs(z).ToString("0.000", Inv);

    // ==================================================================
    // CHUỘT (2D)
    // ==================================================================
    private void DiaHost_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var m = e.GetPosition(diaHost);
        var wpt = DS2W(m);
        double k = e.Delta > 0 ? 1.2 : 1 / 1.2;
        _dScale = Math.Clamp(_dScale * k, 0.5, 5000);
        _dOx = m.X - wpt.X * _dScale;
        _dOy = m.Y + wpt.Y * _dScale;
        RedrawDiagram2D();
        e.Handled = true;
    }

    private void DiaHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dPanning = true; _dDragged = false;
        _dPanStart = e.GetPosition(diaHost);
        _dPanOx = _dOx; _dPanOy = _dOy;
        diaHost.CaptureMouse();
    }

    private void DiaHost_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dPanning) return;
        var p = e.GetPosition(diaHost);
        double dx = p.X - _dPanStart.X, dy = p.Y - _dPanStart.Y;
        if (!_dDragged && Math.Abs(dx) + Math.Abs(dy) < 4) return;
        _dDragged = true;
        _dOx = _dPanOx + dx; _dOy = _dPanOy + dy;
        RedrawDiagram2D();
    }

    private void DiaHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dPanning) return;
        _dPanning = false;
        diaHost.ReleaseMouseCapture();
        if (_dDragged) return;
        var f = HitFrame(_diaHits, e.GetPosition(diaHost));
        if (f != null) Select(frame: f);
        else if (HasSelection) ClearSelection();
    }

    private static FrameElement HitFrame(List<DiaHit> hits, Point s)
    {
        foreach (var h in hits)
            if (h.Rect && Math.Abs(s.X - h.A.X) <= h.Rx && Math.Abs(s.Y - h.A.Y) <= h.Ry) return h.F;
        FrameElement best = null; double bd = 7;
        foreach (var h in hits)
        {
            if (h.Rect) continue;
            double d = SegDist(s, h.A, h.B);
            if (d < bd) { bd = d; best = h.F; }
        }
        return best;
    }

    private static double SegDist(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, L2 = dx * dx + dy * dy;
        double t = L2 < 1e-9 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / L2, 0, 1);
        double qx = a.X + t * dx - p.X, qy = a.Y + t * dy - p.Y;
        return Math.Sqrt(qx * qx + qy * qy);
    }

    // ==================================================================
    // 3D
    // ==================================================================
    /// <summary>Phương tung độ 3D của biểu đồ: M3 về phía −2 (thớ căng khi M3 &gt; 0), M2 về +3, V2/N/T theo +2, V3 theo +3.</summary>
    private static Vec3 DiaDir3D(int comp, Vec3 e2, Vec3 e3) => comp switch
    {
        5 => e2 * -1,
        4 => e3,
        2 => e3,
        _ => e2
    };

    private void BuildDiagram3D()
    {
        var nodes = NodeMap();
        int qi = DiaQtyIndex, comp = DiaQty[qi].Comp;
        string caseName = cbDiagramCase.SelectedItem as string ?? "";
        bool hasF = _res != null && !string.IsNullOrEmpty(caseName);
        double size = ModelSize();
        double t = Math.Clamp(size * 0.004, 0.03, 0.15);

        var memMesh = new PickMesh(); var selMesh = new PickMesh();
        foreach (var f in _p.Frames)
        {
            if (!nodes.TryGetValue(f.NodeI, out var ni) || !nodes.TryGetValue(f.NodeJ, out var nj)) continue;
            if ((nj.P - ni.P).Length < 1e-9) continue;
            var pm = _sel != null && _sel.Id == f.Id ? selMesh : memMesh;
            pm.Begin(f);
            double tw = _sel != null && _sel.Id == f.Id ? 2.2 * t : t;
            var cpoly = CurvedPolyline(f);   // dầm cong: chuỗi hộp theo cung
            if (cpoly != null) for (int k2 = 0; k2 + 1 < cpoly.Count; k2++) AddMemberBox(pm.Mesh, cpoly[k2], cpoly[k2 + 1], f.Angle, tw, tw, 0);
            else AddMemberBox(pm.Mesh, ni.P, nj.P, f.Angle, tw, tw, 0);
        }

        // dữ liệu biểu đồ
        var list = new List<(FrameElement f, Vec3 pi, Vec3 e1, double L, Vec3 u, FrameForces[] curves)>();
        var arcs3 = new Dictionary<int, ArcGeometry>();   // dầm cong: điểm gốc và phương tung độ theo cung
        var lengths = new List<double>();
        double maxAbs = 0;
        if (hasF)
            foreach (var f in PlanFrames())
            {
                if (!nodes.TryGetValue(f.NodeI, out var ni) || !nodes.TryGetValue(f.NodeJ, out var nj)) continue;
                double L = (nj.P - ni.P).Length;
                if (L < 1e-9) continue;
                var mx = ForcesOf(f.Id, caseName, out var mn);
                if (mx == null || mx.Count < 2 || (mn != null && mn.Count != mx.Count)) continue;
                var (e1, e2, e3) = Axes(ni.P, nj.P, f.Angle);
                var curves = mn != null ? new[] { mx, mn } : new[] { mx };
                foreach (var cf in curves) for (int i = 0; i < cf.Count; i++) maxAbs = Math.Max(maxAbs, Math.Abs(Val(cf, i, comp)));
                list.Add((f, ni.P, e1, L, DiaDir3D(comp, e2, e3), curves));
                var arc3 = SafeArc(f);
                if (arc3 != null) arcs3[f.Id] = arc3;
                lengths.Add(arc3?.Length ?? L);
            }
        double span = 3;
        if (lengths.Count > 0) { lengths.Sort(); span = Math.Max(lengths[lengths.Count / 2], 0.5); }
        double k = maxAbs > 1e-9 ? 0.25 * span * sldDiaScale.Value / maxAbs : 0;

        var pos = new PickMesh(); var neg = new PickMesh();
        if (k > 0)
            foreach (var (f, pi, e1, L, u, curves) in list)
            {
                pos.Begin(f); neg.Begin(f);
                foreach (var cf in curves)
                    for (int i = 0; i + 1 < cf.Count; i++)
                    {
                        double s0 = Val(cf, i, comp), s1 = Val(cf, i + 1, comp);
                        if (Math.Abs(s0) < 1e-12 && Math.Abs(s1) < 1e-12) continue;
                        var b0 = pi + e1 * (RelStation(cf, i) * L); var b1 = pi + e1 * (RelStation(cf, i + 1) * L);
                        Vec3 u0 = u, u1 = u;
                        if (arcs3.TryGetValue(f.Id, out var ac))
                        {
                            // dầm cong: gốc trên cung, phương tung độ theo hệ trục tiếp tuyến tại mặt cắt
                            double r0 = RelStation(cf, i), r1 = RelStation(cf, i + 1);
                            b0 = ac.PointAt(r0); b1 = ac.PointAt(r1);
                            var ra = CurvedBeam.AxesAt(ac, r0, f.Angle); var rb = CurvedBeam.AxesAt(ac, r1, f.Angle);
                            u0 = DiaDir3D(comp, new Vec3(ra[1, 0], ra[1, 1], ra[1, 2]), new Vec3(ra[2, 0], ra[2, 1], ra[2, 2]));
                            u1 = DiaDir3D(comp, new Vec3(rb[1, 0], rb[1, 1], rb[1, 2]), new Vec3(rb[2, 0], rb[2, 1], rb[2, 2]));
                        }
                        if (s0 * s1 < 0)
                        {
                            double tt = s0 / (s0 - s1);
                            var bm = b0 + (b1 - b0) * tt;
                            Poly3(s0 >= 0 ? pos : neg, b0, b0 + u0 * (s0 * k), bm);
                            Poly3(s1 >= 0 ? pos : neg, bm, b1 + u1 * (s1 * k), b1);
                        }
                        else Poly3(s0 + s1 >= 0 ? pos : neg, b0, b0 + u0 * (s0 * k), b1 + u1 * (s1 * k), b1);
                    }
            }

        _pickDia.Clear();
        var group = NewLitGroup();
        AddPickModel(group, _pickDia, memMesh, Diffuse(Color.FromRgb(190, 195, 205)), false);
        AddPickModel(group, _pickDia, selMesh, SelectedMaterial3D(), false);
        AddPickModel(group, _pickDia, pos, DiaMaterial3D(Color.FromRgb(230, 40, 60)), false);
        AddPickModel(group, _pickDia, neg, DiaMaterial3D(Color.FromRgb(70, 120, 240)), false);
        diaModelVisual.Content = group;
        if (!_dia3DFitted) { FitCameraTarget(false); _dia3DFitted = true; }
        UpdateCamera();

        var q = DiaQty[qi];
        string env = IsEnvelopeCase(caseName) ? " (bao: max và min)" : "";
        txtDia3DInfo.Text = _res == null
            ? "Chạy phân tích (F5) để xem biểu đồ nội lực"
            : $"{q.Name} ({q.Unit}) – {caseName}{env} – {CurrentStory}   •   max |giá trị| = {Fmt(maxAbs)} {q.Unit}   •   đỏ: dương, xanh: âm";
    }

    private static System.Windows.Media.Media3D.Material DiaMaterial3D(Color c)
    {
        var g = new MaterialGroup();
        g.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(140, c.R, c.G, c.B))));
        g.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(70, c.R, c.G, c.B))));
        return g;
    }

    /// <summary>Tam giác / tứ giác phẳng (hai mặt) vào lưới.</summary>
    private static void Poly3(PickMesh pm, params Vec3[] pts)
    {
        var m = pm.Mesh;
        int b = m.Positions.Count;
        foreach (var p in pts) m.Positions.Add(P(p));
        for (int i = 1; i + 1 < pts.Length; i++)
        {
            m.TriangleIndices.Add(b); m.TriangleIndices.Add(b + i); m.TriangleIndices.Add(b + i + 1);
            m.TriangleIndices.Add(b); m.TriangleIndices.Add(b + i + 1); m.TriangleIndices.Add(b + i);
        }
    }

    // ==================================================================
    // TIỆN ÍCH VẼ TRÊN CANVAS (dùng chung cho biểu đồ và móng)
    // ==================================================================
    private static TextBlock CText(Canvas cv, string s, Point p, Brush br, double size, bool center, bool vcenter = false, bool bg = false)
    {
        var t = new TextBlock { Text = s, Foreground = br, FontSize = size, IsHitTestVisible = false };
        if (bg) { t.Background = TextBg; t.Padding = new Thickness(1, 0, 1, 0); }
        t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double x = center ? p.X - t.DesiredSize.Width / 2 : p.X;
        double y = vcenter || center ? p.Y - t.DesiredSize.Height / 2 : p.Y;
        Canvas.SetLeft(t, x); Canvas.SetTop(t, y);
        cv.Children.Add(t);
        return t;
    }

    private static double MeasureText(string s, double size)
    {
        var t = new TextBlock { Text = s, FontSize = size };
        t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return t.DesiredSize.Width;
    }

    private static Shapes.Line CLine(Canvas cv, Point a, Point b, Brush br, double th, bool dash)
    {
        var l = new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = br, StrokeThickness = th, IsHitTestVisible = false };
        if (dash) l.StrokeDashArray = new DoubleCollection { 12, 3, 2, 3 };
        cv.Children.Add(l);
        return l;
    }

    private static void CBubble(Canvas cv, string name, Point c, double r, Brush br)
    {
        var e = new Shapes.Ellipse { Width = 2 * r, Height = 2 * r, Stroke = br, StrokeThickness = 1, Fill = Brushes.White, IsHitTestVisible = false };
        Canvas.SetLeft(e, c.X - r); Canvas.SetTop(e, c.Y - r);
        cv.Children.Add(e);
        CText(cv, name, c, br, 11, true, true);
    }

    private static void DrawSupport(Canvas cv, Point c)
    {
        var tri = new Shapes.Polygon
        {
            Points = new PointCollection { c, new Point(c.X - 7, c.Y + 11), new Point(c.X + 7, c.Y + 11) },
            Fill = Brushes.ForestGreen, IsHitTestVisible = false
        };
        cv.Children.Add(tri);
        CLine(cv, new Point(c.X - 10, c.Y + 12), new Point(c.X + 10, c.Y + 12), Brushes.ForestGreen, 1.2, false);
    }

    private static void CenterHint(Canvas cv, double w, double h, string text)
    {
        var t = CText(cv, text, new Point(w / 2, h / 2), Brushes.DimGray, 16, true, true, true);
        t.Padding = new Thickness(12, 6, 12, 6);
        Canvas.SetLeft(t, (w - t.DesiredSize.Width) / 2 - 12);
    }

    /// <summary>Lưới trục mặt bằng có ký hiệu trục (bóng tròn) hai đầu và kích thước bước trục.</summary>
    private void DrawPlanGrids(Canvas cv, Func<double, double, Point> w2s, bool dims)
    {
        double bub = 11, ext = 1.2;
        var gxs = _p.GridsX.ToList(); var gys = _p.GridsY.ToList();
        double gyMin = gys.Count > 0 ? gys.First().Coord : 0, gyMax = gys.Count > 0 ? gys.Last().Coord : 10;
        double gxMin = gxs.Count > 0 ? gxs.First().Coord : 0, gxMax = gxs.Count > 0 ? gxs.Last().Coord : 10;
        if (gxs.Count == 0 && gys.Count == 0) return;
        foreach (var g in gxs)
        {
            var a = w2s(g.Coord, gyMin - ext); var b = w2s(g.Coord, gyMax + ext);
            CLine(cv, a, b, DiaGridBrush, 0.8, true);
            CBubble(cv, g.Name, new Point(a.X, a.Y + bub), bub, DiaGridBrush);
            CBubble(cv, g.Name, new Point(b.X, b.Y - bub), bub, DiaGridBrush);
        }
        foreach (var g in gys)
        {
            var a = w2s(gxMin - ext, g.Coord); var b = w2s(gxMax + ext, g.Coord);
            CLine(cv, a, b, DiaGridBrush, 0.8, true);
            CBubble(cv, g.Name, new Point(a.X - bub, a.Y), bub, DiaGridBrush);
            CBubble(cv, g.Name, new Point(b.X + bub, b.Y), bub, DiaGridBrush);
        }
        if (!dims) return;
        for (int i = 0; i + 1 < gxs.Count; i++)
        {
            var a = w2s((gxs[i].Coord + gxs[i + 1].Coord) / 2, gyMin - ext);
            CText(cv, ((gxs[i + 1].Coord - gxs[i].Coord) * 1000).ToString("0", Inv), new Point(a.X, a.Y + 2 * bub + 10), DiaGridBrush, 10, true);
        }
        for (int i = 0; i + 1 < gys.Count; i++)
        {
            var a = w2s(gxMin - ext, (gys[i].Coord + gys[i + 1].Coord) / 2);
            CText(cv, ((gys[i + 1].Coord - gys[i].Coord) * 1000).ToString("0", Inv), new Point(a.X - 2 * bub - 22, a.Y), DiaGridBrush, 10, true);
        }
    }
}
