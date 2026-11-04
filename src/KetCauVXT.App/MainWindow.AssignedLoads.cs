using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Hiển thị tải gán trên mặt bằng (Hiển thị → Hiển thị tải gán, như Display → Load Assigns của ETABS): tải trên thanh (đều / hình thang:
/// đa giác tải + mũi tên + giá trị; lực tập trung: mũi tên + giá trị), tải sàn (giá trị tại tâm ô), tải nút (mũi tên + giá trị).
/// Chỉ tải nhập tay (nguồn "Nhập tay"); lọc theo trường hợp tải chọn trên cây mô hình, hoặc trường hợp ở ô "Biểu đồ M3 theo" nếu là trường hợp tải.
/// Cũng tô viền các cột đang được chọn nhiều (chọn nhiều của dầm chỉ tô dầm).</summary>
public partial class MainWindow
{
    private bool _showAssignedLoads;
    /// <summary>Trường hợp tải lọc khi hiển thị tải gán (chọn từ cây mô hình); null = theo ô "Biểu đồ M3 theo" hoặc mọi trường hợp.</summary>
    private string _loadPatternFilter;

    private static readonly Color[] LoadPalette =
    {
        Color.FromRgb(200, 40, 60), Color.FromRgb(20, 120, 200), Color.FromRgb(30, 150, 70), Color.FromRgb(190, 100, 0),
        Color.FromRgb(130, 60, 170), Color.FromRgb(0, 140, 140), Color.FromRgb(120, 120, 30)
    };
    private static readonly Brush ColSelBrush = Frozen(Color.FromRgb(255, 140, 0));

    private Brush PatternBrush(string pat)
    {
        int i = Math.Max(0, _p.Patterns.FindIndex(x => x.Name == pat));
        return Frozen(LoadPalette[i % LoadPalette.Length]);
    }

    /// <summary>Trường hợp tải đang lọc; null = mọi trường hợp.</summary>
    private string AssignedLoadFilter()
    {
        if (_loadPatternFilter != null && _p.Pattern(_loadPatternFilter) != null) return _loadPatternFilter;
        var c = cbDiagramCase?.SelectedItem as string;
        return c != null && _p.Pattern(c) != null ? c : null;
    }

    private void DrawAssignedLoads()
    {
        if (_p == null) return;
        // cột đang chọn nhiều
        CheckMultiSelOwner();
        if (_multiSel.Count > 0)
            foreach (var f in PlanFrames().Where(q => q.IsVerticalKind && _multiSel.Contains(q.Id)))
            {
                var n = _p.Nodes.FirstOrDefault(q => q.Id == f.NodeJ);
                if (n == null) continue;
                var (dx, dy) = ColumnPlanSize(f);
                var c = W2S(n.X, n.Y);
                double rw = Math.Max(8, dx * _scale) + 8, rh = Math.Max(8, dy * _scale) + 8;
                var r = new Shapes.Rectangle { Width = rw, Height = rh, Stroke = ColSelBrush, StrokeThickness = 2.5, IsHitTestVisible = false };
                Canvas.SetLeft(r, c.X - rw / 2); Canvas.SetTop(r, c.Y - rh / 2);
                planCanvas.Children.Add(r);
            }
        if (!_showAssignedLoads) return;
        string filter = AssignedLoadFilter();
        bool Want(string pat, string src) => src == "" && (filter == null || pat == filter);
        var frames = PlanFrames().Where(f => !f.IsVerticalKind).ToDictionary(f => f.Id);
        var fl = _p.FrameLoads.Where(l => Want(l.Pattern, l.Source) && frames.ContainsKey(l.Element)).ToList();
        double qmax = fl.SelectMany(l => l.Points.Select(q => Math.Abs(q[1]))).DefaultIfEmpty(0).Max();
        // nhiều tải trên cùng thanh: xếp lớp ra ngoài
        var layer = new Dictionary<int, int>();
        foreach (var l in fl)
        {
            var f = frames[l.Element];
            var a = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var b = _p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
            if (a == null || b == null) continue;
            var A = W2S(a.X, a.Y); var B = W2S(b.X, b.Y);
            var d = B - A; double len = d.Length;
            if (len < 4) continue;
            d /= len;
            var nrm = new Vector(-d.Y, d.X);   // phía trái hướng I→J trên màn hình
            int k = layer.GetValueOrDefault(f.Id); layer[f.Id] = k + 1;
            double baseOff = 5 + 26 * k;
            var br = PatternBrush(l.Pattern);
            string dirTxt = l.Dir == LoadDir.Gravity ? "" : " " + DirShort(l.Dir);
            if (l.Points.Count >= 2)
            {
                var poly = new PointCollection();
                var baseLine = new List<Point>();
                foreach (var q in l.Points)
                {
                    var p0 = A + d * (len * q[0]) + nrm * baseOff;
                    double h = qmax > 1e-9 ? 4 + 16 * Math.Abs(q[1]) / qmax : 8;
                    baseLine.Add(p0);
                    poly.Add(p0 + nrm * h);
                }
                for (int i = baseLine.Count - 1; i >= 0; i--) poly.Add(baseLine[i]);
                planCanvas.Children.Add(new Shapes.Polygon { Points = poly, Stroke = br, StrokeThickness = 1, Fill = new SolidColorBrush(Color.FromArgb(40, ((SolidColorBrush)br).Color.R, ((SolidColorBrush)br).Color.G, ((SolidColorBrush)br).Color.B)), IsHitTestVisible = false });
                // mũi tên
                double r0 = l.Points.First()[0], r1 = l.Points.Last()[0];
                int na = Math.Max(2, (int)((r1 - r0) * len / 28));
                for (int i = 0; i <= na; i++)
                {
                    double t = r0 + (r1 - r0) * i / na;
                    double qv = Interp(l.Points, t);
                    double h = qmax > 1e-9 ? 4 + 16 * Math.Abs(qv) / qmax : 8;
                    var tip = A + d * (len * t) + nrm * baseOff;
                    Arrow(tip + nrm * h, tip, br);
                }
                bool uniform = l.Points.All(q => Math.Abs(q[1] - l.Points[0][1]) < 1e-9);
                string txt = uniform ? $"{l.Points[0][1]:0.##}" : string.Join("→", l.Points.Select(q => q[1].ToString("0.##")));
                var mid = A + d * (len * (r0 + r1) / 2) + nrm * (baseOff + 24);
                AddText($"{txt} kN/m {l.Pattern}{dirTxt}", mid, br, 9, true, true);
            }
            foreach (var pl in l.PointLoads)
            {
                var tip = A + d * (len * pl[0]) + nrm * baseOff;
                Arrow(tip + nrm * 24, tip, br, 1.8);
                AddText($"P={pl[1]:0.##} kN {l.Pattern}{dirTxt}", tip + nrm * 32, br, 9, true, true);
            }
        }

        // tải sàn
        foreach (var s in PlanSlabs())
        {
            var al = _p.AreaLoads.Where(l => l.Slab == s.Id && Want(l.Pattern, l.Source)).ToList();
            if (al.Count == 0) continue;
            SlabPoints(s, out double cx, out double cy);
            var c = W2S(cx, cy);
            double y0 = c.Y + 12;
            foreach (var g in al.GroupBy(l => l.Pattern))
            {
                AddText($"{g.Key}: {g.Sum(l => l.Q):0.##} kN/m²", new Point(c.X, y0), PatternBrush(g.Key), 9, true, true);
                y0 += 12;
            }
        }

        // tải nút
        string st = CurrentStory;
        var story = _p.Stories.FirstOrDefault(x => x.Name == st);
        foreach (var nl in _p.NodeLoads.Where(l => Want(l.Pattern, l.Source)))
        {
            var n = _p.Nodes.FirstOrDefault(q => q.Id == nl.Node);
            if (n == null) continue;
            if (story != null && Math.Abs(n.Z - story.Elevation) > 1e-3) continue;
            var c = W2S(n.X, n.Y);
            var br = PatternBrush(nl.Pattern);
            double fh = Math.Sqrt(nl.Fx * nl.Fx + nl.Fy * nl.Fy);
            if (fh > 1e-9)
            {
                var v = new Vector(nl.Fx / fh, -nl.Fy / fh);
                Arrow(c - v * 30, c, br, 1.8);
            }
            if (Math.Abs(nl.Fz) > 1e-9)
            {
                var e = new Shapes.Ellipse { Width = 10, Height = 10, Stroke = br, StrokeThickness = 1.6, IsHitTestVisible = false };
                Canvas.SetLeft(e, c.X - 5); Canvas.SetTop(e, c.Y - 5);
                planCanvas.Children.Add(e);
            }
            var parts = new List<string>();
            if (Math.Abs(nl.Fx) > 1e-9) parts.Add($"Fx={nl.Fx:0.##}");
            if (Math.Abs(nl.Fy) > 1e-9) parts.Add($"Fy={nl.Fy:0.##}");
            if (Math.Abs(nl.Fz) > 1e-9) parts.Add($"Fz={nl.Fz:0.##}");
            if (Math.Abs(nl.Mx) + Math.Abs(nl.My) + Math.Abs(nl.Mz) > 1e-9) parts.Add($"M=({nl.Mx:0.#};{nl.My:0.#};{nl.Mz:0.#})");
            AddText(string.Join(" ", parts) + " " + nl.Pattern, new Point(c.X + 8, c.Y - 16), br, 9, false);
        }
        AddText($"Tải gán: {(filter ?? "mọi trường hợp")} (nhập tay)", new Point(8, 22), Brushes.Gray, 10, false);
    }

    private static string DirShort(LoadDir d) => d switch
    {
        LoadDir.GlobalX => "(X)", LoadDir.GlobalY => "(Y)", LoadDir.GlobalZ => "(Z+)", LoadDir.Local1 => "(1)", LoadDir.Local2 => "(2)", LoadDir.Local3 => "(3)", _ => ""
    };

    private static double Interp(List<double[]> pts, double r)
    {
        for (int i = 0; i + 1 < pts.Count; i++)
            if (r >= pts[i][0] - 1e-12 && r <= pts[i + 1][0] + 1e-12)
            {
                double d = pts[i + 1][0] - pts[i][0];
                return d < 1e-12 ? pts[i][1] : pts[i][1] + (pts[i + 1][1] - pts[i][1]) * (r - pts[i][0]) / d;
            }
        return pts.Count > 0 ? pts[^1][1] : 0;
    }

    /// <summary>Mũi tên từ a tới b (đầu mũi tên tại b).</summary>
    private void Arrow(Point a, Point b, Brush br, double th = 1)
    {
        planCanvas.Children.Add(new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = br, StrokeThickness = th, IsHitTestVisible = false });
        var v = b - a; double L = v.Length;
        if (L < 3) return;
        v /= L;
        var n = new Vector(-v.Y, v.X);
        double s = Math.Min(5, L * 0.4);
        planCanvas.Children.Add(new Shapes.Polygon
        {
            Points = new PointCollection { b, b - v * s + n * (s * 0.5), b - v * s - n * (s * 0.5) }, Fill = br, IsHitTestVisible = false
        });
    }
}
