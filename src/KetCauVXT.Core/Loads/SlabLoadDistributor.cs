using KetCauVXT.Model;

namespace KetCauVXT.Loads;

/// <summary>Dồn tải sàn (trọng lượng bản thân + tải phân bố trên sàn) về các dầm biên ô sàn.
/// Ô chữ nhật: phân tải theo đường phân giác 45° (hình thang trên cạnh dài, tam giác trên cạnh ngắn) –
/// giống cách ETABS phân tải cho sàn kiểu membrane. Ô đa giác khác: phân đều theo chu vi.</summary>
public static class SlabLoadDistributor
{
    public const string Source = "SLAB";

    public static List<string> Apply(Project p)
    {
        var warn = new List<string>();
        p.FrameLoads.RemoveAll(l => l.Source == Source);
        p.NodeLoads.RemoveAll(l => l.Source == Source);
        if (p.Slabs.Count == 0) return warn;

        var horizontal = p.Frames.Where(f => !f.IsVerticalKind).ToList();

        foreach (var slab in p.Slabs)
        {
            var pts = slab.Nodes.Select(id => p.Node(id).P).ToList();
            if (pts.Count < 3) { warn.Add($"Sàn {slab.Label} có ít hơn 3 nút – bỏ qua"); continue; }
            var sec = p.SlabSection(slab.Section);
            var mat = p.Material(sec.Material);

            // tổng tải theo trường hợp
            var qByPattern = new Dictionary<string, double>();
            foreach (var pat in p.Patterns)
            {
                double q = pat.SelfWeight * mat.UnitWeight * sec.Thickness;
                q += p.AreaLoads.Where(a => a.Slab == slab.Id && a.Pattern == pat.Name).Sum(a => a.Q);
                if (Math.Abs(q) > 1e-9) qByPattern[pat.Name] = q;
            }
            if (qByPattern.Count == 0) continue;

            bool rect = IsRectangle(pts, out double la, out double lb);
            double area = PolygonArea(pts), perim = 0;
            for (int i = 0; i < pts.Count; i++) perim += (pts[(i + 1) % pts.Count] - pts[i]).Length;

            for (int i = 0; i < pts.Count; i++)
            {
                Vec3 a = pts[i], b = pts[(i + 1) % pts.Count];
                double le = (b - a).Length;
                Vec3 u = (b - a).Normalize();
                // chiều dài cạnh còn lại (vuông góc) cho ô chữ nhật
                double other = rect ? (Math.Abs(le - la) < 1e-6 ? lb : la) : 0;
                double uniformW = area / perim;

                // các dầm nằm trên cạnh
                var onEdge = new List<(FrameElement f, double tI, double tJ)>();
                foreach (var f in horizontal)
                {
                    var pi = p.Node(f.NodeI).P; var pj = p.Node(f.NodeJ).P;
                    if (!OnSegment(a, u, le, pi, out double ti) || !OnSegment(a, u, le, pj, out double tj)) continue;
                    if (Math.Abs(tj - ti) < 1e-6) continue;
                    onEdge.Add((f, ti, tj));
                }

                // các điểm gãy của hàm tải theo cạnh
                var brk = new List<double> { 0, le };
                if (rect && other / 2 < le / 2) { brk.Add(other / 2); brk.Add(le - other / 2); }
                else if (rect) brk.Add(le / 2);
                double Trib(double t) => rect ? Math.Max(0, Math.Min(Math.Min(t, le - t), other / 2)) : uniformW;

                double covered = 0;
                foreach (var (f, tI, tJ) in onEdge)
                {
                    double t0 = Math.Min(tI, tJ), t1 = Math.Max(tI, tJ);
                    covered += t1 - t0;
                    var ts = brk.Where(t => t > t0 + 1e-6 && t < t1 - 1e-6).Append(t0).Append(t1).Distinct().ToList();
                    var rels = ts.Select(t => ((t - tI) / (tJ - tI), Trib(t))).OrderBy(x => x.Item1).ToList();
                    foreach (var kv in qByPattern)
                    {
                        p.FrameLoads.Add(new FrameLoad
                        {
                            Element = f.Id, Pattern = kv.Key, Dir = LoadDir.Gravity, Source = Source,
                            Note = $"Sàn {slab.Label}",
                            Points = rels.Select(r => new[] { r.Item1, r.Item2 * kv.Value }).ToList()
                        });
                    }
                }
                if (covered < le - 0.02)
                {
                    // phần cạnh không có dầm: dồn tải về 2 nút đầu cạnh (gần đúng)
                    double lost = 0;
                    int n = 40;
                    for (int k = 0; k < n; k++)
                    {
                        double t = (k + 0.5) * le / n;
                        bool cov = onEdge.Any(e => t >= Math.Min(e.tI, e.tJ) - 1e-6 && t <= Math.Max(e.tI, e.tJ) + 1e-6);
                        if (!cov) lost += Trib(t) * le / n;
                    }
                    if (lost > 1e-9)
                    {
                        warn.Add($"Sàn {slab.Label}: cạnh {i + 1} không có dầm đỡ trên {le - covered:0.00} m – tải dồn về nút góc.");
                        foreach (var kv in qByPattern)
                        {
                            p.NodeLoads.Add(new NodeLoad { Node = slab.Nodes[i], Pattern = kv.Key, Fz = -lost * kv.Value / 2, Source = Source });
                            p.NodeLoads.Add(new NodeLoad { Node = slab.Nodes[(i + 1) % pts.Count], Pattern = kv.Key, Fz = -lost * kv.Value / 2, Source = Source });
                        }
                    }
                }
            }
        }
        return warn;
    }

    private static bool OnSegment(Vec3 a, Vec3 u, double le, Vec3 p, out double t)
    {
        var d = p - a;
        t = d.Dot(u);
        var perp = d - u * t;
        return perp.Length < 0.01 && t > -0.01 && t < le + 0.01;
    }

    public static bool IsRectangle(List<Vec3> pts, out double la, out double lb)
    {
        la = lb = 0;
        if (pts.Count != 4) return false;
        for (int i = 0; i < 4; i++)
        {
            var e1 = pts[(i + 1) % 4] - pts[i];
            var e2 = pts[(i + 2) % 4] - pts[(i + 1) % 4];
            if (Math.Abs(e1.Dot(e2)) > 1e-4 * e1.Length * e2.Length) return false;
        }
        la = (pts[1] - pts[0]).Length;
        lb = (pts[2] - pts[1]).Length;
        return true;
    }

    public static double PolygonArea(List<Vec3> pts)
    {
        double s = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % pts.Count];
            s += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(s) / 2;
    }
}
