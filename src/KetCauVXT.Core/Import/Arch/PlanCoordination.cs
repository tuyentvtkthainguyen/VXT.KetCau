#nullable enable
using System.Globalization;

namespace KetCauVXT.Import.Arch;

/// <summary>Tuỳ chọn phối hợp hình học kiến trúc – kết cấu (m).</summary>
public sealed class CoordOptions
{
    /// <summary>Tiết diện cột mặc định b × h</summary>
    public double ColB { get; set; } = 0.22;
    public double ColH { get; set; } = 0.22;
    /// <summary>Dầm khung: h = nhịp / SpanRatio, làm tròn lên 50 mm, trong [MinBeamH, MaxBeamH]</summary>
    public double SpanRatio { get; set; } = 12;
    public double MinBeamH { get; set; } = 0.30;
    public double MaxBeamH { get; set; } = 0.60;
    /// <summary>Dầm bo mép sàn hở</summary>
    public double EdgeB { get; set; } = 0.20;
    public double EdgeH { get; set; } = 0.30;
    /// <summary>Dầm đỡ tường tầng trên không có tường bên dưới</summary>
    public double SupportB { get; set; } = 0.22;
    public double SupportH { get; set; } = 0.30;
    /// <summary>Khoảng thông thủy tối thiểu trên bậc / chiếu nghỉ</summary>
    public double Headroom { get; set; } = 2.0;
}

/// <summary>
/// PHỐI HỢP HÌNH HỌC trước khi dựng Revit: cột – dầm – sàn – tường – cửa – cầu thang tính theo tiết diện thật.
///  1. Cột BTCT tại giao trục (và nút tường 220) có tường tầng đó đi qua; cột biên lùi vào để mặt ngoài cột trùng mặt ngoài tường biên.
///  2. Dầm khung nối các cột liền kề cùng phương nằm trong sàn tầng trên; h = nhịp / 12 (làm tròn 50 mm).
///     Dầm bo mép sàn hở (ban công, mái đua, mép lỗ thang) ngoài các dầm khung; dầm đỡ tường tầng trên không có tường bên dưới.
///  3. Cao tường = tới đáy dầm nằm trên tim tường (phủ ≥ 80 % chiều dài tường), không thì tới đáy sàn.
///  4. Cửa trùng cột → dời dọc tường tới chỗ trống gần nhất (cách mặt cột ≥ 50 mm).
///  5. Dầm đi qua trên vế thang → hạ về chiều cao tối thiểu để giữ khoảng thông thủy.
/// Mọi phần tử tự sinh có Id bắt đầu bằng "C-", "D-", "DB-", "DT-" – chạy lại sẽ sinh lại từ đầu (không trùng).
/// </summary>
public static class PlanCoordination
{
    private static readonly string[] GenBeam = { "D-", "DB-", "DT-" };
    public static bool IsGenerated(string id) => id.StartsWith("C-", StringComparison.Ordinal) || GenBeam.Any(p => id.StartsWith(p, StringComparison.Ordinal));

    private static double R(double v) => Math.Round(v, 3);
    private static double Thick(RvWall w)
    {
        double t = w.Layers?.Sum(l => l.Thickness) ?? 0;
        if (t <= 0) t = RevitMaterials.ThicknessFromName(w.TypeName) ?? 0.11;
        return t;
    }
    private static double Len(double x1, double y1, double x2, double y2) => Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
    private static double DistSeg(double x, double y, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
        double t = l2 < 1e-12 ? 0 : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / l2, 0, 1);
        double px = ax + t * dx - x, py = ay + t * dy - y;
        return Math.Sqrt(px * px + py * py);
    }
    /// <summary>Khoảng cách tới đường thẳng chứa tường (không chặn hai đầu) + vị trí dọc tường u (m)</summary>
    private static (double d, double u, double len) Line(RvWall w, double x, double y)
    {
        double len = Len(w.X1, w.Y1, w.X2, w.Y2);
        if (len < 1e-9) return (double.MaxValue, 0, 0);
        double ux = (w.X2 - w.X1) / len, uy = (w.Y2 - w.Y1) / len;
        double u = (x - w.X1) * ux + (y - w.Y1) * uy;
        double d = Math.Abs(-(x - w.X1) * uy + (y - w.Y1) * ux);
        return (d, u, len);
    }

    private static bool InFloor(IEnumerable<RvFloor> fl, double x, double y, double tol) =>
        fl.Any(f => PlanSpec.InPoly(f.Outline, x, y) || EdgeDist(f.Outline, x, y) <= tol);

    private static double EdgeDist(List<double[]> o, double x, double y)
    {
        double best = double.MaxValue;
        for (int i = 0; i < o.Count; i++) { var a = o[i]; var b = o[(i + 1) % o.Count]; best = Math.Min(best, DistSeg(x, y, a[0], a[1], b[0], b[1])); }
        return best;
    }

    private sealed class Storey
    {
        public RvLevel Lo = null!, Up = null!;
        public double H;
        public List<RvWall> Walls = new();
        public List<RvFloor> UpFloors = new();
        public List<RvStair> Holes = new();
        public double SlabT;
    }

    private static List<Storey> Storeys(RevitExchange ex)
    {
        var lv = ex.Levels.OrderBy(l => l.Elevation).ToList();
        var r = new List<Storey>();
        for (int i = 0; i + 1 < lv.Count; i++)
        {
            var s = new Storey { Lo = lv[i], Up = lv[i + 1], H = lv[i + 1].Elevation - lv[i].Elevation };
            s.Walls = ex.Walls.Where(w => w.Level == s.Lo.Name && Math.Abs(w.BaseOffset) < 0.01).ToList();
            s.UpFloors = ex.Floors.Where(f => f.Level == s.Up.Name && f.Outline.Count >= 3).ToList();
            s.Holes = ex.Stairs.Where(t => t.TopLevel == s.Up.Name && t.Outline.Count >= 3).ToList();
            s.SlabT = s.UpFloors.Count > 0 ? s.UpFloors.Max(f => f.Thickness > 0 ? f.Thickness : 0.12) : 0.12;
            if (s.Walls.Any(w => w.Height >= 1.5) && s.UpFloors.Count > 0) r.Add(s);
        }
        return r;
    }

    // ------------------------------------------------------------------ chạy
    public static List<string> Coordinate(RevitExchange ex, CoordOptions? o = null)
    {
        o ??= new CoordOptions();
        var log = new List<string>();
        ex.Columns.RemoveAll(c => IsGenerated(c.Id));
        ex.Beams.RemoveAll(b => IsGenerated(b.Id));
        var st = Storeys(ex);
        // tường tầng đã hạ ở lần trước → trả về cao suốt tầng để tính lại
        foreach (var s in st)
            foreach (var w in s.Walls.Where(w => w.Height >= s.H - 0.65 && w.Height < s.H - 1e-6)) w.Height = s.H;

        int nc = 0, nf = 0, ne = 0, ns = 0;
        var colsBy = new Dictionary<Storey, List<RvColumn>>();
        for (int i = 0; i < st.Count; i++)
        {
            var cols = PlaceColumns(ex, st[i], o);
            // cột tầng trên thẳng hàng cột tầng dưới (lệch ≤ 0,3 m → lấy đúng toạ độ cột dưới)
            if (i > 0 && colsBy.TryGetValue(st[i - 1], out var below))
                foreach (var c in cols)
                {
                    var bc = below.FirstOrDefault(b => Math.Abs(b.X - c.X) <= 0.3 && Math.Abs(b.Y - c.Y) <= 0.3);
                    if (bc != null) { c.X = bc.X; c.Y = bc.Y; }
                }
            colsBy[st[i]] = cols;
        }
        // cột tầng trên chưa có cột đỡ bên dưới → thêm cột tầng dưới nếu có tường chịu lực ở đó và không có cột khác gần (≤ 1 m)
        for (int i = st.Count - 1; i > 0; i--)
        {
            var lower = st[i - 1];
            if (st[i].Lo.Name != lower.Up.Name) continue;
            foreach (var c in colsBy[st[i]].ToList())
            {
                var lc = colsBy[lower];
                if (lc.Any(b => Math.Abs(b.X - c.X) < 0.05 && Math.Abs(b.Y - c.Y) < 0.05)) continue;
                if (lc.Any(b => Len(b.X, b.Y, c.X, c.Y) < 1.0)) continue;
                var p = Snap(c.X, c.Y, lower, o);
                if (p == null || Len(p.Value.Item1, p.Value.Item2, c.X, c.Y) > 0.06 || !InFloor(lower.UpFloors, c.X, c.Y, 0.2)) continue;
                string id = c.Id.Replace("-" + st[i].Lo.Name, "-" + lower.Lo.Name);
                while (lc.Any(b => b.Id == id)) id = id.Replace("-" + lower.Lo.Name, "b-" + lower.Lo.Name);
                lc.Add(new RvColumn { Id = id, X = c.X, Y = c.Y, BaseLevel = lower.Lo.Name, TopLevel = lower.Up.Name, B = c.B, H = c.H, Material = c.Material });
            }
        }
        foreach (var s in st)
        {
            var cols = colsBy[s];
            ex.Columns.AddRange(cols); nc += cols.Count;
            var frame = FrameBeams(s, cols, o);
            ex.Beams.AddRange(frame); nf += frame.Count;
            var edge = EdgeBeams(s, frame, o);
            ex.Beams.AddRange(edge); ne += edge.Count;
        }
        // dầm đỡ tường tầng trên (tường tầng trên đặt trên sàn mà không có tường / dầm bên dưới)
        foreach (var s in st)
        {
            var atUp = ex.Beams.Where(b => b.Level == s.Up.Name).ToList();
            var upper = ex.Walls.Where(w => w.Level == s.Up.Name && Math.Abs(w.BaseOffset) < 0.01).ToList();
            int k = 0;
            foreach (var w in upper)
            {
                double len = Len(w.X1, w.Y1, w.X2, w.Y2);
                if (len < 1.0) continue;
                double t = Thick(w);
                double ux = (w.X2 - w.X1) / len, uy = (w.Y2 - w.Y1) / len;
                int n = Math.Max(2, (int)Math.Ceiling(len / 0.25));
                double? s0 = null;
                for (int q = 0; q <= n; q++)
                {
                    double u = len * q / n, px = w.X1 + ux * u, py = w.Y1 + uy * u;
                    bool sup = s.Walls.Any(b => DistSeg(px, py, b.X1, b.Y1, b.X2, b.Y2) <= Thick(b) / 2 + t / 2 + 0.05)
                            || atUp.Any(b => DistSeg(px, py, b.X1, b.Y1, b.X2, b.Y2) <= b.B / 2 + t / 2 + 0.02);
                    if (!sup && s0 == null) s0 = u;
                    if ((sup || q == n) && s0 != null)
                    {
                        double u1 = sup ? Math.Min(len, u + 0.25) : len, a0 = Math.Max(0, s0.Value - 0.25);
                        if (u1 - a0 >= 0.8)
                        {
                            var b = new RvBeam { Id = $"DT-{s.Up.Name}-{++k}", X1 = R(w.X1 + ux * a0), Y1 = R(w.Y1 + uy * a0), X2 = R(w.X1 + ux * u1), Y2 = R(w.Y1 + uy * u1), Z1 = s.Up.Elevation, Z2 = s.Up.Elevation, B = o.SupportB, H = o.SupportH, Level = s.Up.Name, Material = "Bê tông B20" };
                            ex.Beams.Add(b); atUp.Add(b); ns++;
                        }
                        s0 = null;
                    }
                }
            }
        }
        log.Add($"Cột: {nc}; dầm khung: {nf}; dầm bo mép sàn: {ne}; dầm đỡ tường: {ns}.");

        // dầm qua trên vế thang → hạ về tối thiểu
        foreach (var t in ex.Stairs)
            foreach (var b in ex.Beams.Where(b => b.Level == t.TopLevel && b.H > o.MinBeamH + 1e-6))
                if (CrossesRuns(t, b))
                {
                    log.Add($"Dầm {b.Id} đi qua trên thang {t.Id}: hạ h {b.H * 1000:0} → {o.MinBeamH * 1000:0} mm để giữ thông thủy.");
                    b.H = o.MinBeamH;
                }
        // vẫn thiếu thông thủy → dầm ngược (đáy dầm = đáy sàn, phần dầm nhô lên trên sàn – nằm trong tường tầng trên nếu có)
        foreach (var t in ex.Stairs)
        {
            var top = ex.Levels.FirstOrDefault(l => l.Name == t.TopLevel);
            if (top == null) continue;
            var fl = ex.Floors.Where(f => f.Level == t.TopLevel).ToList();
            double slabT = fl.Count > 0 ? fl.Max(f => f.Thickness > 0 ? f.Thickness : 0.12) : 0.12;
            var treads = Treads(ex, t);
            foreach (var b in ex.Beams.Where(b => b.Level == t.TopLevel && Math.Abs(b.Z1 - top.Elevation) < 0.005 && CrossesRuns(t, b)))
            {
                double clear = double.MaxValue;
                foreach (var (x, y, z, ux, uy, w) in treads)
                    for (int s2 = -1; s2 <= 1; s2++)
                    {
                        double px = x - uy * s2 * (w / 2 - 0.05), py = y + ux * s2 * (w / 2 - 0.05);
                        if (DistSeg(px, py, b.X1, b.Y1, b.X2, b.Y2) <= b.B / 2) clear = Math.Min(clear, top.Elevation - b.H - z);
                    }
                if (clear >= o.Headroom - 0.005) continue;
                double zt = R(top.Elevation - slabT + b.H);
                b.Z1 = zt; b.Z2 = zt;
                log.Add($"Dầm {b.Id} qua thang {t.Id} chỉ còn thông thủy {clear:0.00} m → đổi thành dầm ngược (đáy dầm = đáy sàn, nhô {(b.H - slabT) * 1000:0} mm lên trên sàn – đặt dưới tường tầng trên hoặc làm gờ).");
            }
        }

        // cao tường tới đáy dầm / đáy sàn
        int nw = 0;
        foreach (var s in st)
        {
            var bm = ex.Beams.Where(b => b.Level == s.Up.Name).ToList();
            foreach (var w in s.Walls.Where(w => Math.Abs(w.Height - s.H) < 0.05))
            {
                double len = Len(w.X1, w.Y1, w.X2, w.Y2), t = Thick(w);
                if (len < 0.05) continue;
                double mx = (w.X1 + w.X2) / 2, my = (w.Y1 + w.Y2) / 2;
                double hb = 0, ovSum = 0;
                foreach (var b in bm)
                {
                    double bl = Len(b.X1, b.Y1, b.X2, b.Y2);
                    if (bl < 0.1) continue;
                    double cross = Math.Abs(((w.X2 - w.X1) * (b.Y2 - b.Y1) - (w.Y2 - w.Y1) * (b.X2 - b.X1)) / (len * bl));
                    if (cross > 0.05) continue; // không song song
                    var (d, _, _) = LineOf(b, mx, my);
                    if (d > (t + b.B) / 2 - 0.005) continue;
                    double ov = Overlap(w, b);
                    if (ov > 0.05) { ovSum += ov; hb = Math.Max(hb, b.H); }
                }
                if (hb > 0 && ovSum >= 0.8 * len) w.Height = R(s.H - hb);
                else if (InFloor(s.UpFloors, mx, my, 0.15)) w.Height = R(s.H - s.SlabT);
                else continue;
                nw++;
            }
        }
        log.Add($"Hạ {nw} tường xuống đáy dầm / đáy sàn.");

        // cửa trùng cột → dời
        foreach (var l in MoveOpenings(ex, o)) log.Add(l);
        return log;
    }

    private static (double d, double u, double len) LineOf(RvBeam b, double x, double y) =>
        Line(new RvWall { X1 = b.X1, Y1 = b.Y1, X2 = b.X2, Y2 = b.Y2 }, x, y);

    private static double Overlap(RvWall w, RvBeam b)
    {
        var (_, u1, len) = Line(w, b.X1, b.Y1);
        var (_, u2, _) = Line(w, b.X2, b.Y2);
        double lo = Math.Max(0, Math.Min(u1, u2) - b.B / 2), hi = Math.Min(len, Math.Max(u1, u2) + b.B / 2);
        return Math.Max(0, hi - lo);
    }

    // ------------------------------------------------------------------ cột
    private static List<RvColumn> PlaceColumns(RevitExchange ex, Storey s, CoordOptions o)
    {
        var cand = new List<(double x, double y, string name)>();
        var gr = ex.Grids.Where(g => Len(g.X1, g.Y1, g.X2, g.Y2) > 0.5).ToList();
        for (int i = 0; i < gr.Count; i++)
            for (int j = i + 1; j < gr.Count; j++)
                if (Intersect(gr[i], gr[j], out double x, out double y)) cand.Add((x, y, gr[i].Name + gr[j].Name));
        foreach (var w in s.Walls.Where(w => w.Height >= 1.5 && (Thick(w) >= 0.2 - 1e-6 || Perimeter(w, s))))
        {
            cand.Add((w.X1, w.Y1, "")); cand.Add((w.X2, w.Y2, ""));
        }
        var outl = s.UpFloors;
        var res = new List<RvColumn>();
        int k = 0;
        foreach (var c in cand)
        {
            var p = Snap(c.x, c.y, s, o);
            if (p == null) continue;
            var (px, py) = p.Value;
            if (!InFloor(outl, px, py, 0.2)) continue;
            if (res.Any(r => Math.Abs(r.X - px) < 0.3 && Math.Abs(r.Y - py) < 0.3)) continue;
            string nm = c.name.Length > 0 ? c.name : "N" + (++k);
            res.Add(new RvColumn { Id = $"C-{nm}-{s.Lo.Name}", X = R(px), Y = R(py), BaseLevel = s.Lo.Name, TopLevel = s.Up.Name, B = o.ColB, H = o.ColH, Material = "Bê tông B20" });
        }
        return res;
    }

    /// <summary>Tường biên mỏng (một mặt ra ngoài sàn tầng trên): dấu dời tim cột vào trong (+1 / −1 theo pháp tuyến trái), 0 nếu không phải biên.</summary>
    private static int OutSide(RvWall w, Storey s)
    {
        double t = Thick(w), len2 = Len(w.X1, w.Y1, w.X2, w.Y2);
        if (len2 < 1e-9) return 0;
        double nx = -(w.Y2 - w.Y1) / len2, ny = (w.X2 - w.X1) / len2;
        double mx = (w.X1 + w.X2) / 2, my = (w.Y1 + w.Y2) / 2;
        bool outA = !InFloor(s.UpFloors, mx + nx * (t / 2 + 0.05), my + ny * (t / 2 + 0.05), 0);
        bool outB = !InFloor(s.UpFloors, mx - nx * (t / 2 + 0.05), my - ny * (t / 2 + 0.05), 0);
        return outA ^ outB ? (outA ? -1 : 1) : 0;
    }
    private static bool Perimeter(RvWall w, Storey s) => OutSide(w, s) != 0;

    /// <summary>
    /// Tâm cột tại điểm (x, y): chỉ bắt vào tim tường "chịu lực" đi qua (tường 220, hoặc tường biên – lùi vào để mặt ngoài cột trùng mặt ngoài tường);
    /// gặp hai tường vuông góc → giao điểm, một tường → hình chiếu lên tim tường (giữ toạ độ trục dọc tường). Tường ngăn 110 bên trong không kéo lệch cột.
    /// </summary>
    private static (double, double)? Snap(double x, double y, Storey s, CoordOptions o)
    {
        var near = new List<(RvWall w, double off, double d)>();
        double half = Math.Max(o.ColB, o.ColH) / 2;
        foreach (var w in s.Walls.Where(w => w.Height >= 1.5))
        {
            var (d, u, len) = Line(w, x, y);
            if (d > 0.35 || u < -0.35 || u > len + 0.35) continue;
            double t = Thick(w);
            int side = OutSide(w, s);
            bool strong = t >= 0.2 - 1e-6 || side != 0;
            if (!strong) continue;
            double off = t < 2 * half - 1e-6 && side != 0 ? (half - t / 2) * side : 0;
            near.Add((w, off, d));
        }
        if (near.Count == 0) return null;
        near.Sort((p, q) => p.d.CompareTo(q.d));
        (double ax, double ay, double ux, double uy) L((RvWall w, double off, double d) n)
        {
            double len = Len(n.w.X1, n.w.Y1, n.w.X2, n.w.Y2);
            double ux = (n.w.X2 - n.w.X1) / len, uy = (n.w.Y2 - n.w.Y1) / len;
            return (n.w.X1 - uy * n.off, n.w.Y1 + ux * n.off, ux, uy);
        }
        var a = L(near[0]);
        foreach (var n in near.Skip(1))
        {
            var b = L(n);
            double den = a.ux * b.uy - a.uy * b.ux;
            if (Math.Abs(den) < 0.5) continue;
            double t = ((b.ax - a.ax) * b.uy - (b.ay - a.ay) * b.ux) / den;
            return (a.ax + a.ux * t, a.ay + a.uy * t);
        }
        double tt = (x - a.ax) * a.ux + (y - a.ay) * a.uy;
        return (a.ax + a.ux * tt, a.ay + a.uy * tt);
    }

    private static bool Intersect(RvGrid g, RvGrid h, out double x, out double y)
    {
        x = y = 0;
        double ux = g.X2 - g.X1, uy = g.Y2 - g.Y1, vx = h.X2 - h.X1, vy = h.Y2 - h.Y1;
        double den = ux * vy - uy * vx;
        if (Math.Abs(den) < 1e-9 * Len(0, 0, ux, uy) * Len(0, 0, vx, vy) + 1e-12) return false;
        double t = ((h.X1 - g.X1) * vy - (h.Y1 - g.Y1) * vx) / den;
        x = g.X1 + ux * t; y = g.Y1 + uy * t;
        return true;
    }

    // ------------------------------------------------------------------ dầm khung
    private static List<RvBeam> FrameBeams(Storey s, List<RvColumn> cols, CoordOptions o)
    {
        var r = new List<RvBeam>();
        var pairs = new HashSet<(int, int)>();
        for (int i = 0; i < cols.Count; i++)
            for (int j = 0; j < cols.Count; j++)
            {
                if (i == j) continue;
                var a = cols[i]; var b = cols[j];
                bool sameX = Math.Abs(a.X - b.X) < 0.02, sameY = Math.Abs(a.Y - b.Y) < 0.02;
                if (!(sameX ^ sameY)) continue;
                double da = sameX ? b.Y - a.Y : b.X - a.X;
                if (da <= 0) continue; // chỉ một chiều
                // cột liền kề: không có cột khác ở giữa trên cùng đường
                bool between = cols.Any(c => c != a && c != b && (sameX ? Math.Abs(c.X - a.X) < 0.02 && c.Y > a.Y + 0.01 && c.Y < b.Y - 0.01 : Math.Abs(c.Y - a.Y) < 0.02 && c.X > a.X + 0.01 && c.X < b.X - 0.01));
                if (between) continue;
                double span = Math.Abs(da);
                if (span < 0.8) continue;
                double mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2;
                bool inSlab = InFloor(s.UpFloors, mx, my, 0.15) && !s.Holes.Any(h => PlanSpec.InPoly(h.Outline, mx, my));
                double cov = Coverage(s.Walls, a.X, a.Y, b.X, b.Y);
                if (!inSlab && cov < 0.5) continue;
                if (!pairs.Add((i, j))) continue;
                double h = Math.Clamp(Math.Ceiling(span / o.SpanRatio / 0.05 - 1e-9) * 0.05, o.MinBeamH, o.MaxBeamH);
                r.Add(new RvBeam { Id = $"D-{s.Up.Name}-{a.Id.Replace("C-", "").Replace("-" + s.Lo.Name, "")}-{b.Id.Replace("C-", "").Replace("-" + s.Lo.Name, "")}", X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Z1 = s.Up.Elevation, Z2 = s.Up.Elevation, B = Math.Min(a.B, a.H), H = R(h), Level = s.Up.Name, Material = "Bê tông B20" });
            }
        return r;
    }

    private static double Coverage(List<RvWall> walls, double ax, double ay, double bx, double by)
    {
        double len = Len(ax, ay, bx, by);
        int n = Math.Max(2, (int)Math.Ceiling(len / 0.25)), c = 0;
        for (int q = 0; q <= n; q++)
        {
            double px = ax + (bx - ax) * q / n, py = ay + (by - ay) * q / n;
            if (walls.Any(w => w.Height >= 1.5 && DistSeg(px, py, w.X1, w.Y1, w.X2, w.Y2) <= Thick(w) / 2 + 0.12)) c++;
        }
        return c / (double)(n + 1);
    }

    // ------------------------------------------------------------------ dầm bo
    private static List<RvBeam> EdgeBeams(Storey s, List<RvBeam> frame, CoordOptions o)
    {
        var r = new List<RvBeam>();
        int k = 0;
        foreach (var f in s.UpFloors)
        {
            var ol = f.Outline;
            for (int j = 0; j < ol.Count; j++)
            {
                var a = ol[j]; var b = ol[(j + 1) % ol.Count];
                double dx = b[0] - a[0], dy = b[1] - a[1], len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1.0) continue;
                double ux = dx / len, uy = dy / len, nx = -uy, ny = ux;
                if (!PlanSpec.InPoly(ol, (a[0] + b[0]) / 2 + nx * 0.05, (a[1] + b[1]) / 2 + ny * 0.05)) { nx = -nx; ny = -ny; }
                int n = Math.Max(2, (int)Math.Ceiling(len / 0.25));
                double? s0 = null;
                for (int q = 0; q <= n; q++)
                {
                    double u = len * q / n, px = a[0] + ux * u, py = a[1] + uy * u;
                    bool covered = s.Walls.Any(w => w.Height >= 1.5 && DistSeg(px, py, w.X1, w.Y1, w.X2, w.Y2) <= Thick(w) / 2 + 0.12)
                                || frame.Concat(r).Any(bm => DistSeg(px, py, bm.X1, bm.Y1, bm.X2, bm.Y2) <= bm.B / 2 + 0.12);
                    if (!covered && s0 == null) s0 = u;
                    if ((covered || q == n) && s0 != null)
                    {
                        double u1 = covered ? Math.Min(len, u + 0.2) : len, u0 = s0.Value > 0 ? Math.Max(0, s0.Value - 0.2) : 0;
                        if (u1 - u0 >= 1.0)
                        {
                            double ox = nx * o.EdgeB / 2, oy = ny * o.EdgeB / 2;
                            r.Add(new RvBeam { Id = $"DB-{s.Up.Name}-{++k}", X1 = R(a[0] + ux * u0 + ox), Y1 = R(a[1] + uy * u0 + oy), X2 = R(a[0] + ux * u1 + ox), Y2 = R(a[1] + uy * u1 + oy), Z1 = s.Up.Elevation, Z2 = s.Up.Elevation, B = o.EdgeB, H = o.EdgeH, Level = s.Up.Name, Material = "Bê tông B20" });
                        }
                        s0 = null;
                    }
                }
            }
        }
        return r;
    }

    // ------------------------------------------------------------------ cầu thang
    /// <summary>Các bậc (tâm mặt bậc x, y, cao độ mặt bậc z, hướng vế ux, uy, bề rộng) của thang có Runs.</summary>
    internal static List<(double x, double y, double z, double ux, double uy, double w)> Treads(RevitExchange ex, RvStair t)
    {
        var r = new List<(double, double, double, double, double, double)>();
        var b = ex.Levels.FirstOrDefault(l => l.Name == t.BaseLevel); var top = ex.Levels.FirstOrDefault(l => l.Name == t.TopLevel);
        if (b == null || top == null || t.Runs.Count == 0) return r;
        int total = t.Runs.Sum(x => x.Risers);
        if (total < 2) return r;
        double rise = (top.Elevation - b.Elevation) / total, z = b.Elevation;
        foreach (var run in t.Runs)
        {
            double len = Len(run.X1, run.Y1, run.X2, run.Y2);
            if (run.Risers < 2 || len < 0.1) { z += run.Risers * rise; continue; }
            double ux = (run.X2 - run.X1) / len, uy = (run.Y2 - run.Y1) / len, tr = len / (run.Risers - 1);
            for (int k = 0; k < run.Risers - 1; k++)
                r.Add((run.X1 + ux * (k + 0.5) * tr, run.Y1 + uy * (k + 0.5) * tr, z + (k + 1) * rise, ux, uy, run.Width > 0.3 ? run.Width : t.Width));
            z += run.Risers * rise;
        }
        return r;
    }

    private static bool CrossesRuns(RvStair t, RvBeam b) =>
        t.Runs.Any(run =>
        {
            double len = Len(run.X1, run.Y1, run.X2, run.Y2);
            if (len < 0.1) return false;
            double w = run.Width > 0.3 ? run.Width : t.Width;
            var poly = RunRect(run, w);
            int n = 8;
            for (int q = 0; q <= n; q++)
            {
                double px = b.X1 + (b.X2 - b.X1) * q / n, py = b.Y1 + (b.Y2 - b.Y1) * q / n;
                if (PlanSpec.InPoly(poly, px, py)) return true;
            }
            return false;
        });

    internal static List<double[]> RunRect(RvStairRun run, double w)
    {
        double len = Len(run.X1, run.Y1, run.X2, run.Y2);
        double ux = (run.X2 - run.X1) / len, uy = (run.Y2 - run.Y1) / len, nx = -uy * w / 2, ny = ux * w / 2;
        return new List<double[]> { new[] { run.X1 + nx, run.Y1 + ny }, new[] { run.X2 + nx, run.Y2 + ny }, new[] { run.X2 - nx, run.Y2 - ny }, new[] { run.X1 - nx, run.Y1 - ny } };
    }

    // ------------------------------------------------------------------ cửa ↔ cột
    private static List<(double u0, double u1)> Blocked(RevitExchange ex, RvWall host, string level, double gap)
    {
        var res = new List<(double, double)>();
        double t = Thick(host);
        foreach (var c in ex.Columns.Where(c => c.BaseLevel == level))
        {
            double half = Math.Max(c.B, c.H) / 2;
            var (d, u, len) = Line(host, c.X, c.Y);
            if (d > t / 2 + half - 0.005 || u < -half || u > len + half) continue;
            res.Add((u - half - gap, u + half + gap));
        }
        return res;
    }

    private static RvWall? Host(RevitExchange ex, RvOpening op)
    {
        RvWall? host = null; double best = 0.3;
        foreach (var w in ex.Walls.Where(w => w.Level == op.Level))
        {
            double d = DistSeg(op.X, op.Y, w.X1, w.Y1, w.X2, w.Y2);
            if (d < best) { best = d; host = w; }
        }
        return host;
    }

    private static List<string> MoveOpenings(RevitExchange ex, CoordOptions o)
    {
        var log = new List<string>();
        foreach (var op in ex.Openings)
        {
            var host = Host(ex, op);
            if (host == null) continue;
            var bl = Blocked(ex, host, op.Level, 0.05);
            var (_, u, len) = Line(host, op.X, op.Y);
            double hw = op.Width / 2;
            bool Clash(double c) => bl.Any(b => c + hw > b.u0 && c - hw < b.u1);
            if (!Clash(u)) continue;
            // các cửa khác trên tường
            var others = ex.Openings.Where(x => x != op && x.Level == op.Level && DistSeg(x.X, x.Y, host.X1, host.Y1, host.X2, host.Y2) < 0.3)
                .Select(x => { var (_, uu, _) = Line(host, x.X, x.Y); return (uu - x.Width / 2 - 0.1, uu + x.Width / 2 + 0.1); }).ToList();
            double? best = null;
            for (double dd = 0.01; dd <= 1.5 && best == null; dd += 0.01)
                foreach (var c in new[] { u + dd, u - dd })
                    if (c - hw >= 0.05 && c + hw <= len - 0.05 && !Clash(c) && !others.Any(b => c + hw > b.Item1 && c - hw < b.Item2)) { best = c; break; }
            if (best == null) continue;
            double ux = (host.X2 - host.X1) / len, uy = (host.Y2 - host.Y1) / len, du = best.Value - u;
            op.X = R(op.X + ux * du); op.Y = R(op.Y + uy * du);
            log.Add($"Cửa {op.Id} ({op.Level}) trùng cột → dời {du * 1000:+0;-0} mm dọc tường {host.Id}.");
        }
        return log;
    }

    // ------------------------------------------------------------------ kiểm tra va chạm
    /// <summary>Va chạm và khoảng thông thủy theo tiết diện: cửa ↔ cột, cửa ↔ đáy dầm, vế thang ↔ tường / cột, thông thủy trên bậc.</summary>
    public static List<PlanIssue> Check(RevitExchange ex, CoordOptions? o = null)
    {
        o ??= new CoordOptions();
        var r = new List<PlanIssue>();
        void E(string m) => r.Add(new PlanIssue { Error = true, Message = m });
        void W(string m) => r.Add(new PlanIssue { Message = m });
        var inv = CultureInfo.InvariantCulture;

        foreach (var op in ex.Openings)
        {
            var host = Host(ex, op);
            if (host == null) continue;
            var bl = Blocked(ex, host, op.Level, 0);
            var (_, u, _) = Line(host, op.X, op.Y);
            if (bl.Any(b => u + op.Width / 2 > b.u0 + 0.005 && u - op.Width / 2 < b.u1 - 0.005)) E($"Cửa {op.Id} ({op.Level}) lấn vào cột trên tường {host.Id} – dời cửa hoặc chạy --phoi-hop.");
            else if (Blocked(ex, host, op.Level, 0.05).Any(b => u + op.Width / 2 > b.u0 && u - op.Width / 2 < b.u1)) W($"Cửa {op.Id} ({op.Level}) sát mặt cột (< 50 mm) – khó lắp khuôn cửa.");
        }

        foreach (var t in ex.Stairs)
        {
            var walls = ex.Walls.Where(w => w.Level == t.BaseLevel && w.Height >= 1.0).ToList();
            var cols = ex.Columns.Where(c => c.BaseLevel == t.BaseLevel).ToList();
            foreach (var (run, i) in t.Runs.Select((x, i) => (x, i)))
            {
                double len = Len(run.X1, run.Y1, run.X2, run.Y2);
                if (len < 0.1) continue;
                double w = run.Width > 0.3 ? run.Width : t.Width;
                // lõi vế thu nhỏ 10 mm để tiếp xúc mặt tường không tính là lấn
                double ux = (run.X2 - run.X1) / len, uy = (run.Y2 - run.Y1) / len;
                foreach (var wl in walls)
                {
                    double tw = Thick(wl);
                    double worst = 0;
                    for (int q = 0; q <= 10; q++)
                        for (int s2 = -1; s2 <= 1; s2 += 2)
                        {
                            double px = run.X1 + ux * len * q / 10 - uy * s2 * (w / 2 - 0.01), py = run.Y1 + uy * len * q / 10 + ux * s2 * (w / 2 - 0.01);
                            var (d, uu, ll) = Line(wl, px, py);
                            if (uu < 0 || uu > ll) continue; // tường là hình chữ nhật dọc tim, kết thúc tại điểm đầu / cuối
                            if (d < tw / 2) worst = Math.Max(worst, tw / 2 - d + 0.01);
                        }
                    if (worst > 0.005) E($"Thang {t.Id} vế {i + 1}: lấn vào tường {wl.Id} khoảng {worst * 1000:0} mm – bề rộng vế tính từ mặt tường, không từ tim tường.");
                }
                var poly = RunRect(run, w - 0.02);
                foreach (var c in cols)
                {
                    double h = Math.Max(c.B, c.H) / 2;
                    var corners = new[] { (c.X - h, c.Y - h), (c.X + h, c.Y - h), (c.X + h, c.Y + h), (c.X - h, c.Y + h), (c.X, c.Y) };
                    if (corners.Any(p => PlanSpec.InPoly(poly, p.Item1, p.Item2))) E($"Thang {t.Id} vế {i + 1}: trùng cột {c.Id}.");
                }
            }
            // thông thủy
            var top = ex.Levels.FirstOrDefault(l => l.Name == t.TopLevel);
            if (top == null) continue;
            var beams = ex.Beams.Where(b => b.Level == t.TopLevel).ToList();
            var fl = ex.Floors.Where(f => f.Level == t.TopLevel).ToList();
            double slabT = fl.Count > 0 ? fl.Max(f => f.Thickness > 0 ? f.Thickness : 0.12) : 0.12;
            double worstH = double.MaxValue; string where = "";
            foreach (var (x, y, z, ux, uy, w) in Treads(ex, t))
                for (int s2 = -1; s2 <= 1; s2++)
                {
                    double px = x - uy * s2 * (w / 2 - 0.05), py = y + ux * s2 * (w / 2 - 0.05);
                    double ceil = double.MaxValue;
                    bool inHole = PlanSpec.InPoly(t.Outline, px, py) || ex.Stairs.Any(o2 => o2.TopLevel == t.TopLevel && PlanSpec.InPoly(o2.Outline, px, py));
                    if (!inHole && fl.Any(f => PlanSpec.InPoly(f.Outline, px, py))) ceil = top.Elevation - slabT;
                    foreach (var b in beams)
                        if (DistSeg(px, py, b.X1, b.Y1, b.X2, b.Y2) <= b.B / 2) ceil = Math.Min(ceil, (b.Z1 > 0 || b.Z1 < 0 ? b.Z1 : top.Elevation) - b.H);
                    if (ceil - z < worstH) { worstH = ceil - z; where = string.Format(inv, "({0:0.00}; {1:0.00}) bậc cao {2:0.000}", px, py, z); }
                }
            if (worstH < o.Headroom - 0.1) E($"Thang {t.Id}: khoảng thông thủy chỉ {worstH:0.00} m tại {where} (cần ≥ {o.Headroom:0.0} m) – mở rộng lỗ sàn hoặc dời dầm.");
            else if (worstH < o.Headroom - 0.005) W($"Thang {t.Id}: khoảng thông thủy {worstH:0.00} m tại {where} (khuyến nghị ≥ {o.Headroom:0.0} m).");
        }

        // cửa đi cao chạm đáy dầm
        foreach (var op in ex.Openings)
        {
            var host = Host(ex, op);
            if (host == null) continue;
            double head = op.Sill + op.Height;
            if (head > host.Height - 0.05 + 1e-6 && head <= host.Height + 0.01) W($"Cửa {op.Id}: đỉnh cửa {head:0.00} m sát đáy dầm / sàn ({host.Height:0.00} m) – không còn chỗ lanh tô.");
        }
        return r;
    }
}
