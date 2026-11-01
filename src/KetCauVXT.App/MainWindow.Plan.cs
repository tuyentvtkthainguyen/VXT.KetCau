using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Analysis;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Mặt bằng tầng (Canvas): trục, cột, dầm, sàn; thu phóng bằng con lăn, kéo để di chuyển, bấm để chọn.
/// Biểu đồ M3 của cấu kiện đang chọn.</summary>
public partial class MainWindow
{
    public const string AllStories = "(Tất cả)";

    // phép biến đổi thế giới (m) → màn hình (px): sx = _ox + x·_scale ; sy = _oy − y·_scale
    private double _scale = 40, _ox = 60, _oy = 400;
    private bool _panning, _dragged;
    private Point _panStart;
    private double _panOx, _panOy;
    private bool _fitPending = true;

    private static readonly Brush GridBrush = new SolidColorBrush(Color.FromRgb(200, 60, 60));
    private static readonly Brush ColumnFill = new SolidColorBrush(Color.FromRgb(90, 90, 100));
    private static readonly Brush BeamBrush = new SolidColorBrush(Color.FromRgb(30, 90, 200));
    private static readonly Brush SteelBrush = new SolidColorBrush(Color.FromRgb(200, 120, 20));
    private static readonly Brush SlabFill = new SolidColorBrush(Color.FromArgb(50, 120, 200, 120));
    private static readonly Brush SelBrush = new SolidColorBrush(Color.FromRgb(255, 0, 200));
    private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));

    private Point W2S(double x, double y) => new(_ox + x * _scale, _oy - y * _scale);
    private (double x, double y) S2W(Point s) => ((s.X - _ox) / _scale, (_oy - s.Y) / _scale);

    private string CurrentStory => cbStory.SelectedItem as string ?? AllStories;

    /// <summary>Phần tử hiển thị trên mặt bằng tầng đang xem.</summary>
    private IEnumerable<FrameElement> PlanFrames()
    {
        if (_p == null) return Enumerable.Empty<FrameElement>();
        string st = CurrentStory;
        if (st == AllStories) return _p.Frames;
        return _p.Frames.Where(f => f.Story == st);
    }

    private IEnumerable<Slab> PlanSlabs()
    {
        if (_p == null) return Enumerable.Empty<Slab>();
        string st = CurrentStory;
        return st == AllStories ? _p.Slabs : _p.Slabs.Where(s => s.Story == st);
    }

    private void FitPlan()
    {
        if (_p == null) return;
        double w = planHost.ActualWidth, h = planHost.ActualHeight;
        if (w < 10 || h < 10) { _fitPending = true; return; }
        var xs = new List<double>(); var ys = new List<double>();
        foreach (var n in _p.Nodes) { xs.Add(n.X); ys.Add(n.Y); }
        foreach (var g in _p.Grids) { if (g.Dir == GridDir.X) xs.Add(g.Coord); else ys.Add(g.Coord); }
        if (xs.Count == 0) xs.AddRange(new[] { 0.0, 10.0 });
        if (ys.Count == 0) ys.AddRange(new[] { 0.0, 10.0 });
        double x0 = xs.Min(), x1 = xs.Max(), y0 = ys.Min(), y1 = ys.Max();
        double dx = Math.Max(x1 - x0, 1), dy = Math.Max(y1 - y0, 1);
        const double margin = 70;
        _scale = Math.Max(1, Math.Min((w - 2 * margin) / dx, (h - 2 * margin) / dy));
        _ox = (w - dx * _scale) / 2 - x0 * _scale;
        _oy = h - (h - dy * _scale) / 2 + y0 * _scale;
        _fitPending = false;
        RedrawPlan();
    }

    private void RedrawPlan()
    {
        planCanvas.Children.Clear();
        if (_p == null) return;
        if (_fitPending) { FitPlan(); if (_fitPending) return; }
        bool labels = chkLabels.IsChecked == true;
        double w = planHost.ActualWidth, h = planHost.ActualHeight;

        // ---- sàn ----
        foreach (var s in PlanSlabs())
        {
            var poly = new Shapes.Polygon { Fill = SlabFill, Stroke = Brushes.Transparent };
            var pts = new PointCollection();
            double cx = 0, cy = 0;
            foreach (int id in s.Nodes)
            {
                var n = _p.Nodes.FirstOrDefault(q => q.Id == id);
                if (n == null) continue;
                pts.Add(W2S(n.X, n.Y)); cx += n.X; cy += n.Y;
            }
            if (pts.Count < 3) continue;
            poly.Points = pts;
            planCanvas.Children.Add(poly);
            if (labels) AddText(s.Label, W2S(cx / pts.Count, cy / pts.Count), Brushes.DarkGreen, 10, true);
        }

        // ---- trục ----
        double bub = 11;
        var gxs = _p.GridsX.ToList(); var gys = _p.GridsY.ToList();
        double gyMin = gys.Count > 0 ? gys.First().Coord : 0, gyMax = gys.Count > 0 ? gys.Last().Coord : 10;
        double gxMin = gxs.Count > 0 ? gxs.First().Coord : 0, gxMax = gxs.Count > 0 ? gxs.Last().Coord : 10;
        double ext = 1.2;
        foreach (var g in gxs)
        {
            var a = W2S(g.Coord, gyMin - ext); var b = W2S(g.Coord, gyMax + ext);
            AddLine(a, b, GridBrush, 0.8, true);
            AddBubble(g.Name, new Point(a.X, a.Y + bub), bub);
            AddBubble(g.Name, new Point(b.X, b.Y - bub), bub);
        }
        foreach (var g in gys)
        {
            var a = W2S(gxMin - ext, g.Coord); var b = W2S(gxMax + ext, g.Coord);
            AddLine(a, b, GridBrush, 0.8, true);
            AddBubble(g.Name, new Point(a.X - bub, a.Y), bub);
            AddBubble(g.Name, new Point(b.X + bub, b.Y), bub);
        }
        // kích thước bước trục
        for (int i = 0; i + 1 < gxs.Count; i++)
        {
            var a = W2S((gxs[i].Coord + gxs[i + 1].Coord) / 2, gyMin - ext);
            AddText($"{(gxs[i + 1].Coord - gxs[i].Coord) * 1000:0}", new Point(a.X, a.Y + 2 * bub + 10), GridBrush, 10, true);
        }
        for (int i = 0; i + 1 < gys.Count; i++)
        {
            var a = W2S(gxMin - ext, (gys[i].Coord + gys[i + 1].Coord) / 2);
            AddText($"{(gys[i + 1].Coord - gys[i].Coord) * 1000:0}", new Point(a.X - 2 * bub - 22, a.Y), GridBrush, 10, true);
        }

        // ---- dầm / kèo ----
        foreach (var f in PlanFrames().Where(q => !q.IsVerticalKind))
        {
            var ni = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var nj = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
            if (ni == null || nj == null) continue;
            var a = W2S(ni.X, ni.Y); var b = W2S(nj.X, nj.Y);
            bool sel = _sel != null && _sel.Id == f.Id;
            var sec = _p.FrameSections.FirstOrDefault(s => s.Name == f.Section);
            double width = sec != null && sec.Shape == SectionShape.RectConcrete ? Math.Max(1.5, Math.Min(sec.B * _scale, 14)) : 2.5;
            AddLine(a, b, sel ? SelBrush : (f.IsConcrete ? BeamBrush : SteelBrush), sel ? width + 2 : width, false);
            if (labels)
            {
                var m = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                bool vertical = Math.Abs(b.X - a.X) < Math.Abs(b.Y - a.Y);
                AddText($"{f.Label} {f.Section}", vertical ? new Point(m.X + 6, m.Y) : new Point(m.X, m.Y - 12), LabelBrush, 9, !vertical);
            }
        }

        // ---- cột ----
        foreach (var f in PlanFrames().Where(q => q.IsVerticalKind))
        {
            var n = _p.Nodes.FirstOrDefault(q => q.Id == f.NodeJ);
            if (n == null) continue;
            var (dx, dy) = ColumnPlanSize(f);
            var c = W2S(n.X, n.Y);
            double rw = Math.Max(4, dx * _scale), rh = Math.Max(4, dy * _scale);
            bool sel = _sel != null && _sel.Id == f.Id;
            var r = new Shapes.Rectangle
            {
                Width = rw, Height = rh, Fill = sel ? SelBrush : (f.IsConcrete ? ColumnFill : SteelBrush),
                Stroke = Brushes.Black, StrokeThickness = 0.8
            };
            Canvas.SetLeft(r, c.X - rw / 2); Canvas.SetTop(r, c.Y - rh / 2);
            planCanvas.Children.Add(r);
            if (labels) AddText($"{f.Label}", new Point(c.X + rw / 2 + 2, c.Y + rh / 2 + 1), Brushes.Black, 9, false);
        }

        // ---- gối ----
        if (CurrentStory == AllStories || _p.Stories.OrderBy(s => s.Elevation).FirstOrDefault()?.Name == CurrentStory)
            foreach (var n in _p.Nodes.Where(q => q.IsSupport))
            {
                var c = W2S(n.X, n.Y);
                var tri = new Shapes.Polygon
                {
                    Points = new PointCollection { new Point(c.X, c.Y + 3), new Point(c.X - 5, c.Y + 11), new Point(c.X + 5, c.Y + 11) },
                    Fill = Brushes.ForestGreen
                };
                planCanvas.Children.Add(tri);
            }

        // tiêu đề
        AddText($"Mặt bằng: {CurrentStory}   –   tỷ lệ {_scale:0.#} px/m", new Point(8, 6), Brushes.Gray, 11, false);
    }

    /// <summary>Kích thước tiết diện cột trên mặt bằng (theo X, theo Y), m.
    /// Góc 0: trục 2 (cạnh h) song song X; góc 90: cạnh h song song Y.</summary>
    private (double dx, double dy) ColumnPlanSize(FrameElement f)
    {
        var sec = _p.FrameSections.FirstOrDefault(s => s.Name == f.Section);
        double b = 0.22, h = 0.22;
        if (sec != null)
        {
            var sp = sec.Props(0.5);
            b = Math.Max(sp.Width, 0.05); h = Math.Max(sp.Depth, 0.05);
        }
        double a = ((f.Angle % 180) + 180) % 180;
        return Math.Abs(a - 90) < 45 ? (b, h) : (h, b);
    }

    private void AddLine(Point a, Point b, Brush br, double th, bool dash)
    {
        var l = new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = br, StrokeThickness = th };
        if (dash) l.StrokeDashArray = new DoubleCollection { 12, 3, 2, 3 };
        planCanvas.Children.Add(l);
    }

    private void AddBubble(string name, Point c, double r)
    {
        var e = new Shapes.Ellipse { Width = 2 * r, Height = 2 * r, Stroke = GridBrush, StrokeThickness = 1, Fill = Brushes.White };
        Canvas.SetLeft(e, c.X - r); Canvas.SetTop(e, c.Y - r);
        planCanvas.Children.Add(e);
        AddText(name, c, GridBrush, 11, true, true);
    }

    private void AddText(string s, Point p, Brush br, double size, bool center, bool vcenter = false, Canvas target = null)
    {
        var t = new TextBlock { Text = s, Foreground = br, FontSize = size, IsHitTestVisible = false };
        t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double x = center ? p.X - t.DesiredSize.Width / 2 : p.X;
        double y = vcenter || center ? p.Y - t.DesiredSize.Height / 2 : p.Y;
        Canvas.SetLeft(t, x); Canvas.SetTop(t, y);
        (target ?? planCanvas).Children.Add(t);
    }

    // ------------------------------------------------------------------
    // Chuột
    // ------------------------------------------------------------------
    private void PlanHost_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var m = e.GetPosition(planHost);
        var (wx, wy) = S2W(m);
        double k = e.Delta > 0 ? 1.2 : 1 / 1.2;
        _scale = Math.Clamp(_scale * k, 0.5, 5000);
        _ox = m.X - wx * _scale;
        _oy = m.Y + wy * _scale;
        RedrawPlan();
        e.Handled = true;
    }

    private void PlanHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _panning = true; _dragged = false;
        _panStart = e.GetPosition(planHost);
        _panOx = _ox; _panOy = _oy;
        planHost.CaptureMouse();
    }

    private void PlanHost_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_panning) return;
        var p = e.GetPosition(planHost);
        double dx = p.X - _panStart.X, dy = p.Y - _panStart.Y;
        if (!_dragged && Math.Abs(dx) + Math.Abs(dy) < 4) return;
        _dragged = true;
        _ox = _panOx + dx; _oy = _panOy + dy;
        RedrawPlan();
    }

    private void PlanHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_panning) return;
        _panning = false;
        planHost.ReleaseMouseCapture();
        if (!_dragged) SelectAt(e.GetPosition(planHost));
    }

    private void PlanHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_fitPending) FitPlan(); else RedrawPlan();
    }

    private void BtnFit_Click(object sender, RoutedEventArgs e) => FitPlan();

    private void ChkLabels_Changed(object sender, RoutedEventArgs e) { if (IsLoaded) RedrawPlan(); }

    private void SelectAt(Point s)
    {
        if (_p == null) return;
        FrameElement best = null;
        // cột: điểm nằm trong hình chữ nhật
        foreach (var f in PlanFrames().Where(q => q.IsVerticalKind))
        {
            var n = _p.Nodes.FirstOrDefault(q => q.Id == f.NodeJ);
            if (n == null) continue;
            var (dx, dy) = ColumnPlanSize(f);
            var c = W2S(n.X, n.Y);
            double hw = Math.Max(4, dx * _scale) / 2 + 3, hh = Math.Max(4, dy * _scale) / 2 + 3;
            if (Math.Abs(s.X - c.X) <= hw && Math.Abs(s.Y - c.Y) <= hh) { best = f; break; }
        }
        if (best == null)
        {
            double bd = 8;
            foreach (var f in PlanFrames().Where(q => !q.IsVerticalKind))
            {
                var ni = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var nj = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
                if (ni == null || nj == null) continue;
                double d = DistToSeg(s, W2S(ni.X, ni.Y), W2S(nj.X, nj.Y));
                if (d < bd) { bd = d; best = f; }
            }
        }
        _sel = best;
        RedrawPlan();
        ShowSelection();
    }

    private static double DistToSeg(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, L2 = dx * dx + dy * dy;
        double t = L2 < 1e-9 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / L2, 0, 1);
        double qx = a.X + t * dx - p.X, qy = a.Y + t * dy - p.Y;
        return Math.Sqrt(qx * qx + qy * qy);
    }

    // ------------------------------------------------------------------
    // Thông tin + biểu đồ M3
    // ------------------------------------------------------------------
    private void ShowSelection()
    {
        if (_sel == null || _p == null)
        {
            txtInfo.Text = "Bấm chọn dầm/cột trên mặt bằng để xem thông tin.";
            DrawDiagram();
            return;
        }
        var f = _sel;
        var ni = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var nj = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
        double L = ni != null && nj != null ? (nj.P - ni.P).Length : 0;
        string kind = f.Kind switch
        {
            FrameKind.Column => "Cột BTCT", FrameKind.Beam => "Dầm BTCT", FrameKind.SteelColumn => "Cột thép",
            FrameKind.Rafter => "Kèo thép", FrameKind.Purlin => "Xà gồ", _ => "Giằng"
        };
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{kind}: {f.Label}  (thanh {f.Id})");
        sb.AppendLine($"Tiết diện: {f.Section}   góc: {f.Angle:0.#}°");
        sb.AppendLine($"Tầng: {f.Story}   nhóm: {f.DesignGroup}");
        sb.AppendLine($"Nút I: {f.NodeI}  {ni?.P}");
        sb.AppendLine($"Nút J: {f.NodeJ}  {nj?.P}");
        sb.AppendLine($"Chiều dài: {L:0.000} m");
        int nl = _p.FrameLoads.Count(l => l.Element == f.Id);
        if (nl > 0) sb.AppendLine($"Số tải gán trên thanh: {nl}");
        if (_res != null)
        {
            string c = cbDiagramCase.SelectedItem as string;
            var ff = ForcesOf(f.Id, c, out var mn);
            if (ff != null && ff.Count > 0 && (mn == null || mn.Count > 0))
            {
                sb.AppendLine($"--- {c} ---");
                double mmax = Enumerable.Range(0, ff.Count).Max(i => ff.M3(i));
                double mmin = Enumerable.Range(0, (mn ?? ff).Count).Min(i => (mn ?? ff).M3(i));
                double vmax = Enumerable.Range(0, ff.Count).Max(i => Math.Abs(ff.V2(i)));
                if (mn != null) vmax = Math.Max(vmax, Enumerable.Range(0, mn.Count).Max(i => Math.Abs(mn.V2(i))));
                double pmax = Enumerable.Range(0, ff.Count).Max(i => ff.P(i));
                double pmin = Enumerable.Range(0, (mn ?? ff).Count).Min(i => (mn ?? ff).P(i));
                sb.AppendLine($"M3 max = {mmax:0.00}  min = {mmin:0.00} kN.m");
                sb.AppendLine($"|V2| max = {vmax:0.00} kN");
                sb.AppendLine($"P max = {pmax:0.00}  min = {pmin:0.00} kN");
            }
        }
        txtInfo.Text = sb.ToString();
        DrawDiagram();
    }

    /// <summary>Nội lực thanh theo trường hợp/tổ hợp; với tổ hợp bao trả về (max, min).</summary>
    private FrameForces ForcesOf(int frameId, string caseName, out FrameForces min)
    {
        min = null;
        if (_res == null || string.IsNullOrEmpty(caseName)) return null;
        try
        {
            var combo = _p.Combinations.FirstOrDefault(c => c.Name == caseName);
            if (combo != null && combo.Type == ComboType.Envelope)
            {
                var (mx, mn) = _res.Envelope(frameId, _res.ExpandCombos(caseName));
                min = mn;
                return mx;
            }
            var r = _res.Get(caseName);
            return r.Frames.TryGetValue(frameId, out var ff) ? ff : null;
        }
        catch (Exception) { return null; }
    }

    private void DrawDiagram()
    {
        diagramCanvas.Children.Clear();
        double w = diagramHost.ActualWidth, h = diagramHost.ActualHeight;
        if (w < 40 || h < 40) return;
        if (_sel == null || _res == null)
        {
            AddText(_res == null ? "Biểu đồ M3: chạy phân tích trước." : "Biểu đồ M3: chọn một cấu kiện.", new Point(8, 8), Brushes.Gray, 11, false, false, diagramCanvas);
            return;
        }
        string c = cbDiagramCase.SelectedItem as string;
        var mx = ForcesOf(_sel.Id, c, out var mn);
        if (mx == null || mx.Count < 2) return;
        var curves = mn != null ? new[] { mx, mn } : new[] { mx };
        double L = mx.Stations[^1] - mx.Stations[0];
        if (L < 1e-9) return;
        double mAbs = curves.SelectMany(f => Enumerable.Range(0, f.Count).Select(i => Math.Abs(f.M3(i)))).DefaultIfEmpty(0).Max();
        double left = 40, right = 40, top = 26, bottom = 22;
        double sx = (w - left - right) / L;
        double y0 = top + (h - top - bottom) / 2;
        double sy = mAbs > 1e-9 ? (h - top - bottom) / 2 / mAbs : 0;

        // trục thanh
        diagramCanvas.Children.Add(new Shapes.Line { X1 = left, Y1 = y0, X2 = left + L * sx, Y2 = y0, Stroke = Brushes.Black, StrokeThickness = 1.5 });
        var colors = new[] { Brushes.Crimson, Brushes.RoyalBlue };
        for (int k = 0; k < curves.Length; k++)
        {
            var f = curves[k];
            var pts = new PointCollection { new Point(left, y0) };
            for (int i = 0; i < f.Count; i++)
                pts.Add(new Point(left + (f.Stations[i] - mx.Stations[0]) * sx, y0 + f.M3(i) * sy)); // M3 dương (căng dưới) vẽ phía dưới
            pts.Add(new Point(left + L * sx, y0));
            var poly = new Shapes.Polygon
            {
                Points = pts, Stroke = colors[k], StrokeThickness = 1.2,
                Fill = new SolidColorBrush(Color.FromArgb(40, ((SolidColorBrush)colors[k]).Color.R, ((SolidColorBrush)colors[k]).Color.G, ((SolidColorBrush)colors[k]).Color.B))
            };
            diagramCanvas.Children.Add(poly);
            // giá trị đầu, cuối, cực trị
            int iMax = 0, iMin = 0;
            for (int i = 1; i < f.Count; i++) { if (f.M3(i) > f.M3(iMax)) iMax = i; if (f.M3(i) < f.M3(iMin)) iMin = i; }
            foreach (int i in new[] { 0, f.Count - 1, iMax, iMin }.Distinct())
            {
                double v = f.M3(i);
                if (Math.Abs(v) < 1e-3 && i != 0 && i != f.Count - 1) continue;
                var p = new Point(left + (f.Stations[i] - mx.Stations[0]) * sx, y0 + v * sy + (v >= 0 ? 8 : -8));
                AddText($"{v:0.0}", p, colors[k], 10, true, false, diagramCanvas);
            }
        }
        AddText($"M3 (kN.m) – {_sel.Label} – {c}{(mn != null ? " (đỏ: max, xanh: min)" : "")}   L = {L:0.00} m",
            new Point(6, 4), Brushes.Black, 11, false, false, diagramCanvas);
    }

    private void DiagramHost_SizeChanged(object sender, SizeChangedEventArgs e) => DrawDiagram();

    private void CbDiagramCase_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ShowSelection();
    }
}
