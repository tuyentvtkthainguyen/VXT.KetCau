using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Design;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Tab "Mô hình móng": mặt bằng móng (lưới trục, cột chân, móng đơn L×B, đài cọc, móng băng theo kết quả thiết kế)
/// và mặt cắt móng đang chọn (bản đế vát, cổ móng, cốt đất ±0.000, thép lớp dưới, kích thước).
/// Đài cọc, móng băng, chọn loại móng: MainWindow.FoundationTypes.cs.</summary>
public partial class MainWindow
{
    private double _fScale = 40, _fOx = 60, _fOy = 400;
    private bool _fFitPending = true, _fPanning, _fDragged;
    private Point _fPanStart;
    private double _fPanOx, _fPanOy;
    private Project _fndProj;
    private readonly List<(Rect r, int node, FrameElement col, object item)> _fndHits = new();

    private static readonly Brush FootOkFill = Frozen(Color.FromArgb(55, 30, 160, 60));
    private static readonly Brush FootBadFill = Frozen(Color.FromArgb(55, 220, 30, 30));
    private static readonly Brush FootOkStroke = Frozen(Color.FromRgb(20, 140, 50));
    private static readonly Brush FootBadStroke = Frozen(Color.FromRgb(210, 20, 20));
    private static readonly Brush ConcreteFill = Frozen(Color.FromRgb(225, 225, 225));
    private static readonly Brush LeanFill = Frozen(Color.FromRgb(240, 236, 220));
    private static readonly Brush RebarBrush = Frozen(Color.FromRgb(200, 0, 0));
    private static readonly Brush DimBrush = Frozen(Color.FromRgb(30, 30, 140));

    private Point FW2S(double x, double y) => new(_fOx + x * _fScale, _fOy - y * _fScale);
    private Point FS2W(Point s) => new((s.X - _fOx) / _fScale, (_fOy - s.Y) / _fScale);

    /// <summary>Nút chân cột (gối) và cột đứng trên nút đó.</summary>
    private List<(Node n, FrameElement col, Node top)> BaseColumns(Dictionary<int, Node> nodes)
    {
        var res = new List<(Node, FrameElement, Node)>();
        var seen = new HashSet<int>();
        foreach (var f in _p.Frames.Where(q => q.IsVerticalKind))
        {
            if (!nodes.TryGetValue(f.NodeI, out var ni) || !nodes.TryGetValue(f.NodeJ, out var nj)) continue;
            var bot = ni.Z <= nj.Z ? ni : nj; var top = ni.Z <= nj.Z ? nj : ni;
            if (!bot.IsSupport || !seen.Add(bot.Id)) continue;
            res.Add((bot, f, top));
        }
        return res;
    }

    private void RedrawFoundation()
    {
        if (_p == null) return;
        if (!ReferenceEquals(_fndProj, _p)) { _fndProj = _p; _fFitPending = true; }
        RedrawFoundationPlan();
        RedrawFootingSection();
    }

    private void FitFoundation()
    {
        double w = fndHost.ActualWidth, h = fndHost.ActualHeight;
        if (w < 20 || h < 20) return;
        var xs = new List<double>(); var ys = new List<double>();
        foreach (var n in _p.Nodes) { xs.Add(n.X); ys.Add(n.Y); }
        foreach (var g in _p.Grids) { if (g.Dir == GridDir.X) xs.Add(g.Coord); else ys.Add(g.Coord); }
        if (_p.Boreholes != null) foreach (var bh in _p.Boreholes) { xs.Add(bh.X); ys.Add(bh.Y); }
        if (_design != null)
        {
            foreach (var f in _design.Footings)
            {
                if (f.Eccentric != null) { EccExtents(f, xs, ys); continue; }   // [MóngLệchTâm]
                xs.Add(f.X - f.L / 2); xs.Add(f.X + f.L / 2); ys.Add(f.Y - f.B / 2); ys.Add(f.Y + f.B / 2);
            }
            AddPileStripExtents(xs, ys);
        }
        if (xs.Count == 0) xs.AddRange(new[] { 0.0, 10.0 });
        if (ys.Count == 0) ys.AddRange(new[] { 0.0, 10.0 });
        double x0 = xs.Min(), x1 = xs.Max(), y0 = ys.Min(), y1 = ys.Max();
        double dx = Math.Max(x1 - x0, 1), dy = Math.Max(y1 - y0, 1);
        const double margin = 80;
        _fScale = Math.Max(1, Math.Min((w - 2 * margin) / dx, (h - 2 * margin) / dy));
        _fOx = (w - dx * _fScale) / 2 - x0 * _fScale;
        _fOy = h - (h - dy * _fScale) / 2 + y0 * _fScale;
        _fFitPending = false;
    }

    private void RedrawFoundationPlan()
    {
        fndCanvas.Children.Clear();
        _fndHits.Clear();
        double w = fndHost.ActualWidth, h = fndHost.ActualHeight;
        if (w < 20 || h < 20) return;
        if (_fFitPending) FitFoundation();
        bool labels = chkFndLabels.IsChecked == true;
        var nodes = NodeMap(); var secs = SectionMap();
        SyncFoundationUi();
        EnsureEccentricUi();   // [MóngLệchTâm] nút "Ranh giới đất…", "Lệch tâm…"
        EnsureCuTramUi();   // [CừTràm] nút "Cừ tràm…" – MainWindow.CuTram.cs

        DrawPlanGrids(fndCanvas, FW2S, true);

        // ---- móng băng, đài cọc (vẽ dưới móng đơn và cột) ----
        int nOther = DrawStripsAndPileCaps(labels, out int badOther);

        // ---- móng ----
        var foots = _design?.Footings ?? new List<FootingDesign>();
        foreach (var fd in foots)
        {
            if (fd.L <= 0 || fd.B <= 0) continue;
            bool sel = _selFooting != 0 && (_selFooting == fd.NodeId || EccHasNode(fd, _selFooting));
            if (DrawEccentricFooting(fd, sel, labels)) continue;   // [MóngLệchTâm] móng lệch tâm / móng kết hợp
            bool ok = fd.Ok;
            var g = FootingGeom(fd, nodes);
            var p0 = FW2S(fd.X - fd.L / 2, fd.Y + fd.B / 2); var p1 = FW2S(fd.X + fd.L / 2, fd.Y - fd.B / 2);
            var r = new Shapes.Rectangle
            {
                Width = Math.Max(2, p1.X - p0.X), Height = Math.Max(2, p1.Y - p0.Y),
                Fill = ok ? FootOkFill : FootBadFill, Stroke = ok ? FootOkStroke : FootBadStroke,
                StrokeThickness = sel ? 3 : 1.4, Effect = sel ? GlowEffect : null, IsHitTestVisible = false
            };
            Canvas.SetLeft(r, p0.X); Canvas.SetTop(r, p0.Y);
            fndCanvas.Children.Add(r);
            // đường vát (từ góc đáy tới mép phần vát quanh cột)
            if (g.H - g.Hb > 1e-3)
            {
                var t0 = FW2S(fd.X - g.TopX / 2, fd.Y + g.TopY / 2); var t1 = FW2S(fd.X + g.TopX / 2, fd.Y - g.TopY / 2);
                var tr = new Shapes.Rectangle
                {
                    Width = Math.Max(1, t1.X - t0.X), Height = Math.Max(1, t1.Y - t0.Y), Stroke = ok ? FootOkStroke : FootBadStroke,
                    StrokeThickness = 0.7, IsHitTestVisible = false
                };
                Canvas.SetLeft(tr, t0.X); Canvas.SetTop(tr, t0.Y);
                fndCanvas.Children.Add(tr);
                CLine(fndCanvas, p0, t0, ok ? FootOkStroke : FootBadStroke, 0.6, false);
                CLine(fndCanvas, p1, t1, ok ? FootOkStroke : FootBadStroke, 0.6, false);
                CLine(fndCanvas, new Point(p1.X, p0.Y), new Point(t1.X, t0.Y), ok ? FootOkStroke : FootBadStroke, 0.6, false);
                CLine(fndCanvas, new Point(p0.X, p1.Y), new Point(t0.X, t1.Y), ok ? FootOkStroke : FootBadStroke, 0.6, false);
            }
            _fndHits.Add((new Rect(p0, p1), fd.NodeId, null, fd));
            if (labels || sel)
            {
                string name = string.IsNullOrEmpty(fd.Name) ? "M" : fd.Name;
                string dim = $"{name}  {fd.L.ToString("0.00", Inv)}×{fd.B.ToString("0.00", Inv)}×{fd.H.ToString("0.00", Inv)}";
                var tl = CText(fndCanvas, dim, new Point((p0.X + p1.X) / 2, p0.Y - 9), ok ? FootOkStroke : FootBadStroke, sel ? 11 : 9.5, true, true, true);
                if (sel) tl.FontWeight = FontWeights.Bold;
                CText(fndCanvas, $"N = {fd.N.ToString("0.0", Inv)} kN", new Point((p0.X + p1.X) / 2, p1.Y + 9), Brushes.DimGray, 9, true, true, true);
            }
        }

        // ---- cột chân + gối ----
        var bases = BaseColumns(nodes);
        int nAll = foots.Count + nOther;
        var covered = CoveredFoundationNodes();
        foreach (var (n, col, top) in bases)
        {
            var (dx, dy) = ColSizeXY(col, n, top, secs);
            var c = FW2S(n.X, n.Y);
            double rw = Math.Max(5, dx * _fScale), rh = Math.Max(5, dy * _fScale);
            bool selCol = _sel != null && _sel.Id == col.Id;
            var r = new Shapes.Rectangle
            {
                Width = rw, Height = rh, Fill = selCol ? DiaSelBrush : (col.IsConcrete ? DiaColBrush : DiaSteelBrush),
                Stroke = Brushes.Black, StrokeThickness = 0.8, Effect = selCol ? GlowEffect : null, IsHitTestVisible = false
            };
            Canvas.SetLeft(r, c.X - rw / 2); Canvas.SetTop(r, c.Y - rh / 2);
            fndCanvas.Children.Add(r);
            if (nAll == 0) DrawSupport(fndCanvas, new Point(c.X, c.Y + rh / 2 + 1));
            if (!covered.Contains(n.Id)) _fndHits.Add((new Rect(c.X - rw / 2 - 3, c.Y - rh / 2 - 3, rw + 6, rh + 6), 0, col, null));
            if (labels && nAll == 0) CText(fndCanvas, col.Label, new Point(c.X + rw / 2 + 2, c.Y + rh / 2 + 12), Brushes.Black, 9, false);
        }
        DrawBoundariesAndStraps();   // [MóngLệchTâm] ranh giới đất + dầm móng cân bằng – MainWindow.Eccentric.cs
        DrawGroundOverlays();   // [LùiCọcRanh] đường giới hạn tim cọc + [CừTràm] lưới cừ – MainWindow.CuTram.cs
        DrawBoreholeMarkers();   // hố khoan – MainWindow.Geotech.cs

        int bad = foots.Count(f => !f.Ok) + badOther;
        CText(fndCanvas, $"Mặt bằng móng ({FoundationTypeName(_p.Foundation.Type).ToLowerInvariant()}) – cao độ chân cột {Elev(_p.BaseElevation)}" +
                         (nAll > 0 ? $"   •   {nAll} móng, {bad} không đạt (đỏ)" : ""), new Point(8, 6), Brushes.Black, 12, false);
        txtFndInfo.Text = nAll > 0
            ? "Bấm vào móng để xem mặt cắt. Xanh: đạt, đỏ: không đạt."
            : $"{bases.Count} chân cột – chưa có kết quả thiết kế móng.";
        if (nAll == 0)
            CenterHint(fndCanvas, w, h, bases.Count == 0 ? "Mô hình chưa có chân cột (nút gối)" : "Bấm 'Thiết kế móng' để có kích thước móng");
    }

    // ------------------------------------------------------------------
    // Mặt cắt móng đang chọn (theo phương X, cạnh L)
    // ------------------------------------------------------------------
    private void RedrawFootingSection()
    {
        fndSecCanvas.Children.Clear();
        double w = fndSecHost.ActualWidth, h = fndSecHost.ActualHeight;
        if (w < 60 || h < 60) return;
        var item = SectionItem(out bool auto);
        switch (item)
        {
            case null:
                CenterHint(fndSecCanvas, w, h, "Chưa có kết quả thiết kế móng – bấm 'Thiết kế móng'");
                return;
            case KetCauVXT.Design.Foundation.PileCapDesign pc: DrawPileCapSection(pc, auto, w, h); return;
            case KetCauVXT.Design.Foundation.StripFootingDesign sf: DrawStripSection(sf, auto, w, h); return;
            case FootingDesign fd0: DrawIsolatedSection(fd0, auto, w, h); return;
        }
    }

    private void DrawIsolatedSection(FootingDesign fd, bool auto, double w, double h)
    {
        var g = FootingGeom(fd, NodeMap());
        double L = g.L, zb = g.ZBot, zTop = zb + g.H, zEdge = zb + g.Hb, zN = g.ZNode;
        double colTop = zN + 0.5;
        double zHigh = Math.Max(colTop, 0.25), zLow = zb - 0.1;

        // khung toạ độ: x ∈ [−L/2 − 0.9, L/2 + 1.3], z ∈ [zLow − 0.5, zHigh + 0.3]
        double wx0 = -L / 2 - 0.9, wx1 = L / 2 + 1.4, wz0 = zLow - 0.55, wz1 = zHigh + 0.3;
        double top = 44, bottom = 92;
        double s = Math.Min((w - 20) / (wx1 - wx0), (h - top - bottom) / (wz1 - wz0));
        if (s <= 0) return;
        double ox = 10 + ((w - 20) - (wx1 - wx0) * s) / 2 - wx0 * s;
        double oz = top + ((h - top - bottom) - (wz1 - wz0) * s) / 2 + wz1 * s;
        Point S(double x, double z) => new(ox + x * s, oz - z * s);

        // ---- mặt đất ±0.000 ----
        var g0 = S(wx0 + 0.1, 0); var g1 = S(wx1 - 0.1, 0);
        CLine(fndSecCanvas, g0, g1, Brushes.SaddleBrown, 1.2, false);
        for (double x = g0.X; x < g1.X - 4; x += 9)
            CLine(fndSecCanvas, new Point(x, g0.Y), new Point(x + 6, g0.Y + 6), Brushes.SaddleBrown, 0.6, false);
        ElevMark(fndSecCanvas, S(-L / 2 - 0.7, 0), "±0.000 (mặt đất)");

        // ---- bê tông lót ----
        var lean = new Shapes.Rectangle { Width = (L + 0.2) * s, Height = Math.Max(2, 0.1 * s), Fill = LeanFill, Stroke = Brushes.Gray, StrokeThickness = 0.8, IsHitTestVisible = false };
        var lp = S(-L / 2 - 0.1, zb);
        Canvas.SetLeft(lean, lp.X); Canvas.SetTop(lean, lp.Y);
        fndSecCanvas.Children.Add(lean);

        // ---- thân móng + cổ móng + cột ----
        double cx = g.Cx, tx = g.TopX;
        double co = fd.Eccentric?.Combined == null ? -(fd.Eccentric?.OffsetX ?? 0) : 0;   // [MóngLệchTâm] tim cột cách tâm móng −ex
        var body = new Shapes.Polygon
        {
            Points = new PointCollection
            {
                S(-L / 2, zb), S(L / 2, zb), S(L / 2, zEdge), S(co + tx / 2, zTop), S(co + cx / 2, zTop), S(co + cx / 2, colTop),
                S(co - cx / 2, colTop), S(co - cx / 2, zTop), S(co - tx / 2, zTop), S(-L / 2, zEdge)
            },
            Fill = ConcreteFill, Stroke = Brushes.Black, StrokeThickness = 1.6, IsHitTestVisible = false
        };
        fndSecCanvas.Children.Add(body);
        // đường cắt ngắt cột
        var b0 = S(co - cx / 2 - 0.08, colTop); var b1 = S(co + cx / 2 + 0.08, colTop);
        var zig = new Shapes.Polyline
        {
            Points = new PointCollection { b0, new Point(b0.X + (b1.X - b0.X) * 0.4, b0.Y), new Point(b0.X + (b1.X - b0.X) * 0.47, b0.Y - 6),
                                           new Point(b0.X + (b1.X - b0.X) * 0.53, b0.Y + 6), new Point(b0.X + (b1.X - b0.X) * 0.6, b0.Y), b1 },
            Stroke = Brushes.Black, StrokeThickness = 1, Fill = Brushes.White, IsHitTestVisible = false
        };
        fndSecCanvas.Children.Add(zig);
        // mặt trên cổ móng = nút chân cột
        CLine(fndSecCanvas, S(co - cx / 2, zN), S(co + cx / 2, zN), Brushes.Gray, 0.6, true);
        if (Math.Abs(co) > 1e-6)   // [MóngLệchTâm] trục cột + kích thước tim cột – mép móng
        {
            CLine(fndSecCanvas, S(co, zb - 0.05), S(co, colTop + 0.1), Brushes.SteelBlue, 0.7, true);
            DimLine(fndSecCanvas, S(-L / 2, zTop), S(co, zTop), -30, ((co + L / 2) * 1000).ToString("0", Inv));
            DimLine(fndSecCanvas, S(co, zTop), S(L / 2, zTop), -30, ((L / 2 - co) * 1000).ToString("0", Inv));
        }

        // ---- thép lớp dưới ----
        double cover = 0.05;
        var r0 = S(-L / 2 + cover, zb + cover); var r1 = S(L / 2 - cover, zb + cover);
        var bar = new Shapes.Polyline
        {
            Points = new PointCollection { S(-L / 2 + cover, zb + Math.Min(0.15, g.Hb - cover)), r0, r1, S(L / 2 - cover, zb + Math.Min(0.15, g.Hb - cover)) },
            Stroke = RebarBrush, StrokeThickness = 2, IsHitTestVisible = false
        };
        fndSecCanvas.Children.Add(bar);
        double spB = fd.SpacingB > 0.03 ? fd.SpacingB : 0.15;
        double dotR = Math.Max(2, Math.Min(4, (fd.DiaB > 0 ? fd.DiaB : 12) / 1000.0 * s / 2 + 1));
        int nDots = Math.Max(2, (int)Math.Floor((L - 2 * cover) / spB) + 1);
        for (int i = 0; i < nDots; i++)
        {
            double x = -L / 2 + cover + (L - 2 * cover) * i / (nDots - 1);
            var c = S(x, zb + cover);
            var dot = new Shapes.Ellipse { Width = 2 * dotR, Height = 2 * dotR, Fill = RebarBrush, IsHitTestVisible = false };
            Canvas.SetLeft(dot, c.X - dotR); Canvas.SetTop(dot, c.Y - 2 * dotR - 1);
            fndSecCanvas.Children.Add(dot);
        }
        // ghi chú thép
        var tagL = S(L * 0.15, zb + cover); var tagB = S(-L * 0.25, zb + cover);
        Leader(fndSecCanvas, tagL, new Point(tagL.X + 30, oz - (zb - 0.32) * s), $"{(string.IsNullOrEmpty(fd.BarsL) ? "?" : fd.BarsL)} (phương L = X)");
        Leader(fndSecCanvas, new Point(tagB.X, tagB.Y - 2 * dotR), new Point(tagB.X - 10, oz - (zb - 0.55) * s), $"{(string.IsNullOrEmpty(fd.BarsB) ? "?" : fd.BarsB)} (phương B = Y)");

        // ---- kích thước ----
        DimLine(fndSecCanvas, S(-L / 2, zb - 0.1), S(L / 2, zb - 0.1), 26, (L * 1000).ToString("0", Inv));
        DimLine(fndSecCanvas, S(L / 2, zb), S(L / 2, zEdge), -22, (g.Hb * 1000).ToString("0", Inv));
        if (g.H - g.Hb > 1e-3) DimLine(fndSecCanvas, S(L / 2, zEdge), S(L / 2, zTop), -22, ((g.H - g.Hb) * 1000).ToString("0", Inv));
        DimLine(fndSecCanvas, S(L / 2, zb), S(L / 2, zTop), -48, "H=" + (g.H * 1000).ToString("0", Inv));
        DimLine(fndSecCanvas, S(co - cx / 2, colTop), S(co + cx / 2, colTop), -14, (cx * 1000).ToString("0", Inv));
        if (zb < -1e-3) DimLine(fndSecCanvas, S(-L / 2, 0), S(-L / 2, zb), 30, "Df=" + (-zb * 1000).ToString("0", Inv));

        // ---- cao độ ----
        ElevMark(fndSecCanvas, S(L / 2 + 0.75, zb), Elev(zb) + " (đáy móng)");
        ElevMark(fndSecCanvas, S(L / 2 + 0.75, zTop), Elev(zTop));
        ElevMark(fndSecCanvas, S(co + cx / 2 + 0.35, zN), Elev(zN) + " (chân cột)");

        // ---- tiêu đề + thông tin ----
        string name = string.IsNullOrEmpty(fd.Name) ? "M" : fd.Name;
        CText(fndSecCanvas, $"MẶT CẮT MÓNG {name} (theo phương X)", new Point(w / 2, 14), Brushes.Black, 12.5, true, true).FontWeight = FontWeights.Bold;
        if (auto) CText(fndSecCanvas, "(chưa chọn móng – hiển thị móng đầu tiên)", new Point(w / 2, 31), Brushes.Gray, 10, true, true);
        double y = h - bottom + 8;
        void Info(string t, Brush br) { CText(fndSecCanvas, t, new Point(10, y), br, 10.5, false); y += 15; }
        Info($"L×B×H = {fd.L.ToString("0.00", Inv)}×{fd.B.ToString("0.00", Inv)}×{fd.H.ToString("0.00", Inv)} m;  Hb = {g.Hb.ToString("0.00", Inv)} m;  cột {(g.Cx * 1000).ToString("0", Inv)}×{(g.Cy * 1000).ToString("0", Inv)}", Brushes.Black);
        Info($"N = {fd.N.ToString("0.0", Inv)} kN;  Mx = {fd.Mx.ToString("0.0", Inv)};  My = {fd.My.ToString("0.0", Inv)} kN.m  ({fd.Combo})", Brushes.Black);
        Info($"Pmax = {fd.Pmax.ToString("0.0", Inv)};  Ptb = {fd.Ptb.ToString("0.0", Inv)};  R = {fd.R.ToString("0.0", Inv)} kN/m²"
             + (fd.Eccentric != null ? $";  {EccentricOverride.SchemeName(fd.Eccentric.Scheme)}, e = {fd.Eccentric.E0 * 1000:0} mm" : ""), Brushes.Black);   // [MóngLệchTâm]
        string msg = fd.Messages.FirstOrDefault(m => m.StartsWith("LỖI")) ?? fd.Checks.Where(c => !c.Ok).Select(c => $"{c.Name}: {c.Ratio.ToString("0.00", Inv)}").FirstOrDefault();
        Info(fd.Ok ? "Kết quả: ĐẠT" : "Kết quả: KHÔNG ĐẠT" + (string.IsNullOrEmpty(msg) ? "" : " – " + msg), fd.Ok ? FootOkStroke : FootBadStroke);
    }

    private static void ElevMark(Canvas cv, Point p, string text)
    {
        var tri = new Shapes.Polygon
        {
            Points = new PointCollection { p, new Point(p.X - 5, p.Y - 7), new Point(p.X + 5, p.Y - 7) },
            Fill = Brushes.Black, IsHitTestVisible = false
        };
        cv.Children.Add(tri);
        CLine(cv, new Point(p.X - 5, p.Y - 7), new Point(p.X + 60, p.Y - 7), Brushes.Black, 0.6, false);
        CText(cv, text, new Point(p.X + 7, p.Y - 21), Brushes.Black, 10, false);
    }

    private static void Leader(Canvas cv, Point from, Point to, string text)
    {
        CLine(cv, from, to, RebarBrush, 0.7, false);
        CLine(cv, to, new Point(to.X + 8, to.Y), RebarBrush, 0.7, false);
        CText(cv, text, new Point(to.X + 10, to.Y), RebarBrush, 10, false, true, true);
    }

    /// <summary>Đường kích thước giữa a, b (màn hình), dời ra một khoảng offset (px) theo pháp tuyến trái.</summary>
    private static void DimLine(Canvas cv, Point a, Point b, double offset, string text)
    {
        var d = b - a;
        if (d.Length < 1) return;
        d.Normalize();
        var n = new Vector(-d.Y, d.X);
        if (Math.Abs(d.X) >= Math.Abs(d.Y)) { if (n.Y < 0) n = -n; } else if (n.X < 0) n = -n;   // dương: xuống / sang phải
        var a1 = a + n * offset; var b1 = b + n * offset;
        var ext = n * (offset >= 0 ? 3 : -3);
        CLine(cv, a, a1 + ext, DimBrush, 0.5, false);
        CLine(cv, b, b1 + ext, DimBrush, 0.5, false);
        CLine(cv, a1, b1, DimBrush, 0.7, false);
        var tk = (d + n) * 3.5; if (offset < 0) tk = (d - n) * 3.5;
        CLine(cv, a1 - tk, a1 + tk, DimBrush, 1.2, false);
        CLine(cv, b1 - tk, b1 + tk, DimBrush, 1.2, false);
        var m = new Point((a1.X + b1.X) / 2, (a1.Y + b1.Y) / 2);
        bool vertical = Math.Abs(d.Y) > Math.Abs(d.X);
        var t = CText(cv, text, m, DimBrush, 10, true, true, true);
        if (vertical)
        {
            t.RenderTransformOrigin = new Point(0.5, 0.5);
            t.RenderTransform = new RotateTransform(-90);
            Canvas.SetLeft(t, Canvas.GetLeft(t) + (offset >= 0 ? 8 : -8));
        }
        else Canvas.SetTop(t, Canvas.GetTop(t) + (offset >= 0 ? 8 : -8));
    }

    // ------------------------------------------------------------------
    // Chuột
    // ------------------------------------------------------------------
    private void FndHost_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var m = e.GetPosition(fndHost);
        var wpt = FS2W(m);
        double k = e.Delta > 0 ? 1.2 : 1 / 1.2;
        _fScale = Math.Clamp(_fScale * k, 0.5, 5000);
        _fOx = m.X - wpt.X * _fScale;
        _fOy = m.Y + wpt.Y * _fScale;
        RedrawFoundationPlan();
        e.Handled = true;
    }

    private void FndHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _fPanning = true; _fDragged = false;
        _fPanStart = e.GetPosition(fndHost);
        _fPanOx = _fOx; _fPanOy = _fOy;
        fndHost.CaptureMouse();
    }

    private void FndHost_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_fPanning) return;
        var p = e.GetPosition(fndHost);
        double dx = p.X - _fPanStart.X, dy = p.Y - _fPanStart.Y;
        if (!_fDragged && Math.Abs(dx) + Math.Abs(dy) < 4) return;
        _fDragged = true;
        _fOx = _fPanOx + dx; _fOy = _fPanOy + dy;
        RedrawFoundationPlan();
    }

    private void FndHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_fPanning) return;
        _fPanning = false;
        fndHost.ReleaseMouseCapture();
        if (_fDragged) return;
        var s = e.GetPosition(fndHost);
        // ưu tiên ô nhỏ nhất chứa điểm bấm (cột nằm trong móng)
        var hit = _fndHits.Where(x => x.r.Contains(s)).OrderBy(x => x.r.Width * x.r.Height)
            .Select(x => ((int node, FrameElement col, object item)?)(x.node, x.col, x.item)).FirstOrDefault();
        if (hit == null) { _fndSelItem = null; if (HasSelection) ClearSelection(); return; }
        _fndSelItem = hit.Value.item;
        if (hit.Value.node != 0) Select(footingNode: hit.Value.node);
        else if (hit.Value.col != null) Select(frame: hit.Value.col);
    }

    private void FndHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_secInit || !fndHost.IsVisible) return;
        RedrawFoundationPlan();
    }

    private void FndSecHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_secInit || !fndSecHost.IsVisible) return;
        RedrawFootingSection();
    }

    private void BtnFndFit_Click(object sender, RoutedEventArgs e)
    {
        _fFitPending = true;
        RedrawFoundationPlan();
    }

    private void FndOption_Changed(object sender, RoutedEventArgs e) { if (_secInit) RedrawFoundationPlan(); }
}
