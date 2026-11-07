using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Mep;

// =====================================================================
// ĐIỆN NƯỚC – ĐHKK ĐỢT 4
// • MepWallFaceSnap: kéo thiết bị (dàn lạnh / dàn nóng / thiết bị tự do) bám mặt tường gần nhất – tâm cách mặt tường = chiều sâu / 2,
//   trục thiết bị song song tường, bước dọc tường 5 cm; ngoài vùng bắt (0,3 m) → bắt lưới 5 cm theo X, Y.
// • HvacGridRouter: dò tuyến ống gió trực giao trên lưới mặt bằng (A*) ở cao độ trần: cột = vật cản (nở thêm nửa bề rộng ống + khe),
//   cắt qua dầm = phạt BeamPenalty (phải hạ ống), xuyên tường = phạt WallPenalty, mỗi lần đổi hướng = phạt BendPenalty.
// • HvacCoolingDetailed: tải lạnh chi tiết kiểu CLTD rút gọn theo từng phòng (tường / kính theo hướng, mái, người hiện + ẩn,
//   chiếu sáng W/m², thiết bị W/m², gió lọt) – phương pháp diện tích giữ tên "Ước lượng nhanh".
// • ElectricalPanels: chia mạch (chiếu sáng ≤ 10 điểm, ổ cắm ≤ 8 điểm, mỗi máy lạnh / bình nóng lạnh một mạch riêng),
//   tải mạch, aptomat, dây; cân pha (1 pha / 3 pha), công suất tính toán theo hệ số nhu cầu Kc, aptomat tổng, cáp tổng;
//   sơ đồ nguyên lý tủ điện (DXF) + bảng tủ điện (Excel).
// • WaterSupplyCalc: dung tích bể nước mái / bể ngầm = N·q·số ngày (+ dự trữ chữa cháy); bơm Q = Qngày·Kngày / T bơm,
//   H = H tĩnh + 1,3·tổn thất dọc đường (Hazen-Williams) + cột áp tự do.
// • RiserDiagrams: sơ đồ không gian cấp nước (trục đứng + nhánh tầng vẽ xiên 45°) và sơ đồ ống đứng thoát nước.
// =====================================================================

// ---------------------------------------------------------------------
// 1. Bám mặt tường khi kéo
// ---------------------------------------------------------------------

/// <summary>Kết quả bám: vị trí tâm, góc trục (độ), tường bám (0 = không), phía (+1 trái / −1 phải tường), đã bám tường?, mô tả.</summary>
public sealed record WallFaceSnapResult(double X, double Y, double Angle, int WallId, int Side, bool Snapped, string Text);

public static class MepWallFaceSnap
{
    public const double Step = 0.05;
    public const double Tolerance = 0.3;

    private static double Round(double v, double step) => step > 1e-9 ? Math.Round(v / step) * step : v;

    /// <summary>Bám tâm thiết bị (chiều sâu depth vuông góc tường, bề rộng width dọc tường) vào mặt tường gần nhất của tầng.
    /// Tâm = tim tường + pháp tuyến·(dày/2 + depth/2), vị trí dọc tường làm tròn bước step và kẹp trong [width/2, L − width/2].
    /// Không có tường trong vùng tol quanh vị trí bám → làm tròn X, Y theo bước step.</summary>
    public static WallFaceSnapResult Snap(Project p, string story, double x, double y, double depth, double width = 0, double tol = Tolerance, double step = Step)
    {
        WallLine best = null; double bestGap = double.MaxValue, bestT = 0; int bestSide = 1; double bestC = 0;
        foreach (var w in p?.Walls ?? new List<WallLine>())
        {
            if (w.Story != story || w.Chord < 1e-6 || w.IsCurved) continue;
            double L = w.Chord, ux = (w.X2 - w.X1) / L, uy = (w.Y2 - w.Y1) / L;
            double t = ((x - w.X1) * ux + (y - w.Y1) * uy) / L;
            if (t < -1e-9 || t > 1 + 1e-9) continue;
            double cr = ux * (y - w.Y1) - uy * (x - w.X1);
            double dist = Math.Abs(cr);
            double c = ModelSolids.WallThickness(p, w) / 2 + Math.Max(0, depth) / 2;
            double gap = Math.Abs(dist - c);
            if (dist > c + tol) continue;
            if (gap < bestGap) { bestGap = gap; best = w; bestT = t; bestSide = cr >= 0 ? 1 : -1; bestC = c; }
        }
        if (best == null)
        {
            double sx = Round(x, step), sy = Round(y, step);
            return new WallFaceSnapResult(sx, sy, double.NaN, 0, 0, false, "Bắt lưới 5 cm");
        }
        double Lb = best.Chord, bx = (best.X2 - best.X1) / Lb, by = (best.Y2 - best.Y1) / Lb;
        double half = Math.Min(Math.Max(0, width) / 2, Lb / 2);
        double s = Math.Clamp(Round(bestT * Lb, step), half, Math.Max(half, Lb - half));
        double nx = -by * bestSide, ny = bx * bestSide;
        double px = best.X1 + bx * s + nx * bestC, py = best.Y1 + by * s + ny * bestC;
        double ang = Math.Atan2(by, bx) * 180 / Math.PI + (bestSide < 0 ? 180 : 0);
        ang = ((ang % 360) + 360) % 360;
        return new WallFaceSnapResult(px, py, ang, best.Id, bestSide, true, $"Bám mặt tường {best.Id} (cách đầu tường {s:0.00} m, bước 5 cm)");
    }

    /// <summary>Kéo dàn lạnh / dàn nóng tới (x, y) có bám mặt tường: dời qua HvacEdit.Move (ống đồng, ống ngưng đi lại), đặt góc theo tường.</summary>
    public static WallFaceSnapResult MoveUnit(Project p, HvacUnit u, double x, double y)
    {
        var r = Snap(p, u.Story, x, y, u.D, u.W);
        double dx = r.X - u.X, dy = r.Y - u.Y;
        if (r.Snapped && !double.IsNaN(r.Angle)) u.Angle = r.Angle;
        if (Math.Abs(dx) + Math.Abs(dy) > 1e-9) HvacEdit.Move(p, HvacElemKind.Unit, u.Id, dx, dy);
        return r;
    }

    /// <summary>Thiết bị điện nước tự do (không gắn tường, không trên trần): bám mặt tường (chiều sâu = D của họ). Thiết bị gắn tường giữ tường chủ.</summary>
    public static WallFaceSnapResult MoveDevice(Project p, MepDevice d, double x, double y)
    {
        var f = p.Mep.FamilyOf(d);
        if (d.WallId > 0 || f == null || f.Ceiling)
        {
            MepDeviceDragMath.Apply(p, d, x, y, Step);
            return new WallFaceSnapResult(d.X, d.Y, double.NaN, d.WallId, d.Side, d.WallId > 0, d.WallId > 0 ? $"Dọc tường chủ {d.WallId}" : "Bắt lưới 5 cm");
        }
        var r = Snap(p, d.Story, x, y, f.D, f.W);
        d.X = r.X; d.Y = r.Y;
        return r;
    }
}

// ---------------------------------------------------------------------
// 2. Dò tuyến ống gió A* tránh cột, hạn chế cắt dầm / tường
// ---------------------------------------------------------------------

/// <summary>Kết quả dò tuyến: các đỉnh (mặt bằng), số lần đổi hướng, chiều dài, số lần cắt dầm / xuyên tường, tìm được?</summary>
public sealed record HvacRouteResult(List<(double X, double Y)> Points, int Bends, double Length, int BeamCrossings, int WallCrossings, bool Found);

public static class HvacGridRouter
{
    public const double Margin = 3.0;
    public const int MaxNodes = 250000;

    private sealed record Obst(double X1, double Y1, double X2, double Y2);

    /// <summary>Hình chữ nhật cột (mặt bằng) có cao độ cắt qua z, nở thêm inflate.</summary>
    private static List<Obst> Columns(Project p, double z, double inflate)
    {
        var res = new List<Obst>();
        foreach (var f in p.Frames.Where(f => f.IsVerticalKind))
        {
            Node ni, nj;
            try { ni = p.Node(f.NodeI); nj = p.Node(f.NodeJ); } catch { continue; }
            if (ni == null || nj == null) continue;
            if (z < Math.Min(ni.Z, nj.Z) - 1e-6 || z > Math.Max(ni.Z, nj.Z) + 1e-6) continue;
            var sec = p.FrameSections.FirstOrDefault(s => s.Name == f.Section);
            double b = sec?.B > 0 ? sec.B : 0.22, h = sec?.H > 0 ? sec.H : 0.22;
            if (Math.Abs(Math.Sin(f.Angle * Math.PI / 180)) > 0.7071) (b, h) = (h, b);
            res.Add(new Obst(ni.X - b / 2 - inflate, ni.Y - h / 2 - inflate, ni.X + b / 2 + inflate, ni.Y + h / 2 + inflate));
        }
        return res;
    }

    /// <summary>Dầm (tim mặt bằng) có cao độ cắt dải ống [z − half, z + half].</summary>
    private static List<(double X1, double Y1, double X2, double Y2)> Beams(Project p, double z, double half)
    {
        var res = new List<(double, double, double, double)>();
        foreach (var f in p.Frames.Where(f => !f.IsVerticalKind))
        {
            Node ni, nj;
            try { ni = p.Node(f.NodeI); nj = p.Node(f.NodeJ); } catch { continue; }
            if (ni == null || nj == null) continue;
            var sec = p.FrameSections.FirstOrDefault(s => s.Name == f.Section);
            double bh = sec?.H > 0 ? sec.H : 0.3, zt = Math.Max(ni.Z, nj.Z), zb = zt - bh;
            if (z + half <= zb + 1e-6 || z - half >= zt - 1e-6) continue;
            res.Add((ni.X, ni.Y, nj.X, nj.Y));
        }
        return res;
    }

    private static bool Inside(List<Obst> obs, double x, double y)
    {
        foreach (var o in obs) if (x > o.X1 + 1e-9 && x < o.X2 - 1e-9 && y > o.Y1 + 1e-9 && y < o.Y2 - 1e-9) return true;
        return false;
    }

    private static List<double> Axis(double a, double b, double lo, double hi, double step)
    {
        var v = new List<double>();
        for (double t = a; t >= lo - 1e-9; t -= step) v.Add(t);
        for (double t = a + step; t <= hi + 1e-9; t += step) v.Add(t);
        v.Add(b);
        v.Sort();
        var res = new List<double>();
        foreach (double t in v) if (res.Count == 0 || t - res[^1] > 1e-6) res.Add(t);
        return res;
    }

    private static int Count(List<(double X1, double Y1, double X2, double Y2)> segs, double ax, double ay, double bx, double by)
    {
        int n = 0;
        foreach (var s in segs) if (MepGeom.SegmentsIntersect(ax, ay, bx, by, s.X1, s.Y1, s.X2, s.Y2)) n++;
        return n;
    }

    /// <summary>Dò tuyến trực giao (x1,y1) → (x2,y2) ở cao độ tim z cho ống bề rộng widthMm, chiều cao depthMm.
    /// Chi phí = chiều dài + BendPenalty·số lần đổi hướng + BeamPenalty·số dầm cắt + WallPenalty·số tường xuyên; cột không đi qua được.</summary>
    public static HvacRouteResult Route(Project p, string story, double x1, double y1, double x2, double y2, double z, double widthMm, double depthMm)
    {
        var set = p.Mep?.Hvac?.Settings ?? new HvacSettings();
        double step = Math.Max(0.02, set.RouteStep);
        double inflate = widthMm / 2000.0 + Math.Max(0, set.BeamClearance);
        var cols = Columns(p, z, inflate);
        var beams = Beams(p, z, depthMm / 2000.0);
        var walls = p.Walls.Where(w => w.Story == story && w.Chord > 1e-6).Select(w => (w.X1, w.Y1, w.X2, w.Y2)).ToList();
        double lox = Math.Min(x1, x2) - Margin, hix = Math.Max(x1, x2) + Margin, loy = Math.Min(y1, y2) - Margin, hiy = Math.Max(y1, y2) + Margin;
        List<double> xs, ys;
        while (true)
        {
            xs = Axis(x1, x2, lox, hix, step); ys = Axis(y1, y2, loy, hiy, step);
            if ((long)xs.Count * ys.Count <= MaxNodes) break;
            step *= 1.5;
        }
        int nx = xs.Count, ny = ys.Count;
        int si = xs.FindIndex(v => Math.Abs(v - x1) < 1e-6), sj = ys.FindIndex(v => Math.Abs(v - y1) < 1e-6);
        int ei = xs.FindIndex(v => Math.Abs(v - x2) < 1e-6), ej = ys.FindIndex(v => Math.Abs(v - y2) < 1e-6);
        var blocked = new bool[nx * ny];
        for (int j = 0; j < ny; j++) for (int i = 0; i < nx; i++) blocked[j * nx + i] = Inside(cols, xs[i], ys[j]);
        blocked[sj * nx + si] = false; blocked[ej * nx + ei] = false;

        int[] di = { 1, -1, 0, 0 }, dj = { 0, 0, 1, -1 };
        int S = nx * ny * 5;
        var g = new double[S]; var prev = new int[S];
        for (int k = 0; k < S; k++) { g[k] = double.MaxValue; prev[k] = -1; }
        var pq = new PriorityQueue<int, double>();
        int start = (sj * nx + si) * 5 + 4;
        g[start] = 0; pq.Enqueue(start, 0);
        int goal = -1;
        double H(int i, int j) => Math.Abs(xs[i] - x2) + Math.Abs(ys[j] - y2);
        var done = new bool[S];
        while (pq.Count > 0)
        {
            int st = pq.Dequeue();
            if (done[st]) continue;
            done[st] = true;
            int cell = st / 5, dir = st % 5, ci = cell % nx, cj = cell / nx;
            if (ci == ei && cj == ej) { goal = st; break; }
            for (int d = 0; d < 4; d++)
            {
                if (dir < 4 && (d ^ 1) == dir) continue;     // không quay ngược
                int ni = ci + di[d], nj = cj + dj[d];
                if (ni < 0 || nj < 0 || ni >= nx || nj >= ny) continue;
                int nc = nj * nx + ni;
                if (blocked[nc]) continue;
                double ax = xs[ci], ay = ys[cj], bx = xs[ni], by = ys[nj];
                if (Inside(cols, (ax + bx) / 2, (ay + by) / 2)) continue;
                double cost = Math.Abs(bx - ax) + Math.Abs(by - ay);
                if (dir < 4 && dir != d) cost += set.BendPenalty;
                if (beams.Count > 0) cost += set.BeamPenalty * Count(beams, ax, ay, bx, by);
                if (walls.Count > 0) cost += set.WallPenalty * Count(walls, ax, ay, bx, by);
                int ns = nc * 5 + d;
                double ng = g[st] + cost;
                if (ng < g[ns] - 1e-12) { g[ns] = ng; prev[ns] = st; pq.Enqueue(ns, ng + H(ni, nj)); }
            }
        }
        if (goal < 0)
        {
            var o = HvacRouting.Orthogonal(x1, y1, x2, y2, z).Select(q => (q.X, q.Y)).ToList();
            return Summ(o, beams, walls, false);
        }
        var cells = new List<(double X, double Y)>();
        for (int st = goal; st >= 0; st = prev[st]) { int c = st / 5; cells.Add((xs[c % nx], ys[c / nx])); }
        cells.Reverse();
        return Summ(Simplify(cells), beams, walls, true);
    }

    /// <summary>Bỏ các đỉnh thẳng hàng.</summary>
    public static List<(double X, double Y)> Simplify(List<(double X, double Y)> pts)
    {
        var res = new List<(double X, double Y)>();
        foreach (var q in pts)
        {
            if (res.Count > 0 && Math.Abs(res[^1].X - q.X) + Math.Abs(res[^1].Y - q.Y) < 1e-9) continue;
            if (res.Count >= 2)
            {
                var a = res[^2]; var b = res[^1];
                double cr = (b.X - a.X) * (q.Y - b.Y) - (b.Y - a.Y) * (q.X - b.X);
                if (Math.Abs(cr) < 1e-9) { res[^1] = q; continue; }
            }
            res.Add(q);
        }
        return res;
    }

    private static HvacRouteResult Summ(List<(double X, double Y)> pts, List<(double X1, double Y1, double X2, double Y2)> beams,
        List<(double X1, double Y1, double X2, double Y2)> walls, bool found)
    {
        double L = 0; int nb = 0, nw = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            L += MepGeom.Dist2D(pts[i - 1].X, pts[i - 1].Y, pts[i].X, pts[i].Y);
            nb += Count(beams, pts[i - 1].X, pts[i - 1].Y, pts[i].X, pts[i].Y);
            nw += Count(walls, pts[i - 1].X, pts[i - 1].Y, pts[i].X, pts[i].Y);
        }
        return new HvacRouteResult(pts, Math.Max(0, pts.Count - 2), L, nb, nw, found);
    }

    /// <summary>Đi lại tuyến ống gió d giữa hai đầu hiện có theo lưới A* (giữ cao độ sát trần), rồi hạ qua dầm (nếu bật) và sinh lại phụ kiện.</summary>
    public static HvacRouteResult Reroute(Project p, HvacDuct d)
    {
        if (d == null || d.Path.Count < 2) return new HvacRouteResult(new(), 0, 0, 0, 0, false);
        double z = HvacRouting.DuctZ(p, d.Story, d.Depth);
        var a = d.Path[0]; var b = d.Path[^1];
        var r = Route(p, d.Story, a.X, a.Y, b.X, b.Y, z, d.Width, d.Depth);
        d.Path = r.Points.Select(q => new MepPt { X = q.X, Y = q.Y, Z = z }).ToList();
        if (p.Mep.Hvac.Settings.AutoDropUnderBeams) HvacBeamAvoid.DropUnderBeams(p, d);
        HvacRouting.RebuildFittings(p);
        return r;
    }

    /// <summary>Thêm ống gió mới đi theo tuyến A* (tránh cột).</summary>
    public static HvacDuct AddRouted(Project p, string story, double x1, double y1, double x2, double y2, out HvacRouteResult route,
        HvacDuctSystem sys = HvacDuctSystem.GioCap, double flow = 0, bool round = false, bool branch = false)
    {
        var d = HvacRouting.AddDuct(p, story, x1, y1, x2, y2, sys, flow, round, branch);
        route = Reroute(p, d);
        return d;
    }

    /// <summary>Đi lại mọi ống gió của tầng (null = mọi tầng).</summary>
    public static int RerouteAll(Project p, string story = null)
    {
        int n = 0;
        foreach (var d in (p.Mep?.Hvac?.Ducts ?? new List<HvacDuct>()).Where(d => story == null || d.Story == story).ToList())
            if (Reroute(p, d).Found) n++;
        return n;
    }
}

// ---------------------------------------------------------------------
// 3. Tải lạnh chi tiết (CLTD rút gọn)
// ---------------------------------------------------------------------

/// <summary>Thông số tải lạnh chi tiết (mặc định khí hậu nóng ẩm Việt Nam, phòng ở 26 °C).</summary>
public sealed class CoolingInput
{
    /// <summary>Số người; 0 = 2 người</summary>
    public int People { get; set; }
    /// <summary>Góc hướng Bắc so với trục +Y của mặt bằng (độ, theo chiều kim đồng hồ)</summary>
    public double NorthAngle { get; set; }
    /// <summary>Phòng có mái phía trên: null = tự nhận (tầng cao nhất)</summary>
    public bool? Roof { get; set; }
    public double UWall { get; set; } = 2.0;
    public double UGlass { get; set; } = 5.8;
    public double URoof { get; set; } = 1.8;
    public double ShadingCoef { get; set; } = 0.6;
    /// <summary>Hệ số tải lạnh bức xạ kính (CLF)</summary>
    public double GlassClf { get; set; } = 0.7;
    public double DeltaT { get; set; } = 9;
    public double CltdRoof { get; set; } = 22;
    public double PeopleSensible { get; set; } = 75;
    public double PeopleLatent { get; set; } = 55;
    public double LightingWm2 { get; set; } = 10;
    public double EquipmentWm2 { get; set; } = 15;
    /// <summary>Số lần trao đổi gió lọt (1/h)</summary>
    public double Ach { get; set; } = 0.5;
    /// <summary>Chênh lệch độ chứa ẩm trong / ngoài (kg/kg)</summary>
    public double DeltaW { get; set; } = 0.008;
    public double Safety { get; set; } = 1.1;
    /// <summary>Hiệu chỉnh CLTD tường / mái (°C): (25,5 − t trong) + (t ngoài − DR/2 − 29,4); 0 = dùng CLTD bảng (MepRound5.ClimateCooling.Apply tính theo địa phương)</summary>
    public double CltdCorrection { get; set; }
}

/// <summary>Một mặt bao ngoài: tường, hướng, diện tích tường đặc / kính.</summary>
public sealed record CoolingWall(int WallId, string Orient, double WallArea, double GlassArea, double QWall, double QGlass);

/// <summary>Tải lạnh chi tiết của phòng (W) và máy chọn.</summary>
public sealed record CoolingResult(int RoomId, string Room, double Area, double Height, List<CoolingWall> Walls,
    double QWall, double QGlass, double QRoof, double QPeopleS, double QPeopleL, double QLight, double QEquip,
    double QInfS, double QInfL, double TotalW, double Btu, double UnitBtu, int UnitCount, double QuickBtu);

public static class HvacCoolingDetailed
{
    public static readonly string[] Orients = { "B", "ĐB", "Đ", "ĐN", "N", "TN", "T", "TB" };
    /// <summary>CLTD tường (°C) theo hướng B, ĐB, Đ, ĐN, N, TN, T, TB.</summary>
    public static readonly double[] CltdWall = { 6, 10, 14, 12, 9, 14, 16, 11 };
    /// <summary>Hệ số bức xạ mặt trời qua kính lớn nhất SHGF (W/m²) theo hướng.</summary>
    public static readonly double[] Shgf = { 120, 400, 500, 400, 200, 400, 500, 400 };

    public const string MethodText = "CLTD rút gọn: Q = Σ U·A·CLTD (tường, mái) + Σ A·(U·Δt + SHGF·SC·CLF) (kính) + người (hiện + ẩn) + đèn + thiết bị + gió lọt (1,23·Q·Δt + 3010·Q·Δw); × hệ số an toàn";

    /// <summary>Chỉ số hướng (0 = Bắc … 7 = Tây Bắc) của pháp tuyến ra ngoài (nx, ny) trên mặt bằng.</summary>
    public static int OrientIndex(double nx, double ny, double northAngle = 0)
    {
        double az = Math.Atan2(nx, ny) * 180 / Math.PI - northAngle;
        az = ((az % 360) + 360) % 360;
        return (int)Math.Floor((az + 22.5) / 45) % 8;
    }

    private static bool TopStory(Project p, string story)
    {
        var s = p.Stories.FirstOrDefault(q => q.Name == story);
        return s == null || p.Stories.All(q => q.Elevation <= s.Elevation + 1e-6);
    }

    /// <summary>Tải lạnh chi tiết của phòng.</summary>
    public static CoolingResult Room(Project p, Room r, CoolingInput o = null)
    {
        o ??= new CoolingInput();
        double hgt = r.CeilingHeight > 0 ? r.CeilingHeight : MepRouting.StoryHeight(p, r.Story, (p.Mep ?? new MepModel()).Settings);
        double A = r.Area;
        var (cx, cy) = r.Center;
        var walls = new List<CoolingWall>();
        double qW = 0, qG = 0;
        foreach (var w in HvacLoad.RoomWalls(p, r).Where(w => HvacLoad.IsExterior(p, w)))
        {
            double L = w.Chord, ux = (w.X2 - w.X1) / L, uy = (w.Y2 - w.Y1) / L;
            double nx = -uy, ny = ux;
            double mx = (w.X1 + w.X2) / 2, my = (w.Y1 + w.Y2) / 2;
            if ((mx - cx) * nx + (my - cy) * ny < 0) { nx = -nx; ny = -ny; }
            int k = OrientIndex(nx, ny, o.NorthAngle);
            double ag = 0;
            foreach (var op in w.Openings ?? new List<WallOpening>())
            {
                var t = WallAssembly.TypeOf(p, op);
                if (t != null && t.Kind == WallOpeningKind.Window) ag += t.Width * t.Height;
            }
            double aw = Math.Max(0, L * hgt - ag);
            double qw = o.UWall * aw * Math.Max(0, CltdWall[k] + o.CltdCorrection);
            double qg = ag * (o.UGlass * o.DeltaT + Shgf[k] * o.ShadingCoef * o.GlassClf);
            walls.Add(new CoolingWall(w.Id, Orients[k], aw, ag, qw, qg));
            qW += qw; qG += qg;
        }
        bool roof = o.Roof ?? TopStory(p, r.Story);
        double qR = roof ? o.URoof * A * Math.Max(0, o.CltdRoof + o.CltdCorrection) : 0;
        int n = o.People > 0 ? o.People : 2;
        double qPS = n * o.PeopleSensible, qPL = n * o.PeopleLatent;
        double qL = o.LightingWm2 * A, qE = o.EquipmentWm2 * A;
        double ls = o.Ach * A * hgt * 1000 / 3600;   // l/s
        double qIS = 1.23 * ls * o.DeltaT, qIL = 3010 * ls * o.DeltaW;
        double tot = (qW + qG + qR + qPS + qPL + qL + qE + qIS + qIL) * o.Safety;
        double btu = tot / 1000 / HvacLoad.BtuToKw;
        var (ub, uc) = HvacLoad.Choose(btu);
        bool ext = walls.Count > 0;
        double quick = HvacLoad.Load(A, hgt, ext, o.People);
        return new CoolingResult(r.Id, string.IsNullOrEmpty(r.Name) ? $"Phòng {r.Id}" : r.Name, A, hgt, walls,
            qW, qG, qR, qPS, qPL, qL, qE, qIS, qIL, tot, btu, ub, uc, quick);
    }

    /// <summary>Bảng tải lạnh chi tiết mọi phòng (Excel).</summary>
    public static KetCauVXT.Report.QtyTable Table(Project p, CoolingInput o = null)
    {
        var t = new KetCauVXT.Report.QtyTable
        {
            Title = "Tải lạnh chi tiết từng phòng (CLTD rút gọn, W) – so sánh ước lượng nhanh",
            Headers = new[] { "Phòng", "Tầng", "A (m²)", "Tường", "Kính", "Mái", "Người", "Đèn", "Thiết bị", "Gió lọt", "Tổng (W)", "BTU/h", "Chọn máy", "Ước lượng nhanh (BTU/h)" },
            Widths = new double[] { 18, 8, 8, 9, 9, 9, 9, 8, 9, 9, 10, 10, 14, 14 }
        };
        foreach (var r in (p.Arch?.Rooms ?? new List<Room>()).Where(r => r.Outline.Count >= 3))
        {
            var c = Room(p, r, o);
            t.Add(c.Room, r.Story, R1(c.Area), R0(c.QWall), R0(c.QGlass), R0(c.QRoof), R0(c.QPeopleS + c.QPeopleL), R0(c.QLight), R0(c.QEquip),
                R0(c.QInfS + c.QInfL), R0(c.TotalW), R0(c.Btu), (c.UnitCount > 1 ? $"{c.UnitCount} × " : "") + $"{c.UnitBtu:0} BTU", R0(c.QuickBtu));
        }
        return t;
    }

    private static double R0(double v) => Math.Round(v, 0);
    private static double R1(double v) => Math.Round(v, 1);
}

// ---------------------------------------------------------------------
// 4. Tủ điện: chia mạch, cân pha, aptomat / cáp tổng, sơ đồ nguyên lý
// ---------------------------------------------------------------------

/// <summary>Thông số tủ điện (lưu trong MepSettings.Panel).</summary>
public sealed class PanelOptions
{
    public int MaxLightingPoints { get; set; } = (int)MepStandards.Val("dien.max_cs");
    public int MaxSocketPoints { get; set; } = (int)MepStandards.Val("dien.max_oc");
    /// <summary>"Tự động" / "1 pha" / "3 pha"</summary>
    public string Phases { get; set; } = PanelOptions.Auto;
    /// <summary>Tự động chuyển 3 pha khi công suất tính toán vượt (kW)</summary>
    public double ThreePhaseAboveKw { get; set; } = 12;
    public double KcLighting { get; set; } = MepStandards.Val("dien.kc_cs");
    public double KcSocket { get; set; } = MepStandards.Val("dien.kc_oc");
    public double KcAc { get; set; } = MepStandards.Val("dien.kc_ac");
    public double KcHeater { get; set; } = MepStandards.Val("dien.kc_nong");
    public double CosPhi { get; set; } = MepStandards.Val("dien.cos");
    public double MinBreakerLighting { get; set; } = 10;
    public double MinBreakerSocket { get; set; } = 16;
    public const string Auto = "Tự động", One = "1 pha", Three = "3 pha";
}

/// <summary>Thông số bể nước / bơm (lưu trong MepSettings.Water).</summary>
public sealed class WaterTankOptions
{
    /// <summary>Số người dùng nước; 0 = chưa khai báo (không lập bảng bể / bơm)</summary>
    public int Occupants { get; set; }
    /// <summary>Tiêu chuẩn dùng nước (l/người·ngày đêm)</summary>
    public double LitersPerPerson { get; set; } = MepStandards.Val("cap.q_nguoi");
    /// <summary>Hệ số không điều hoà ngày</summary>
    public double KDay { get; set; } = MepStandards.Val("cap.kngay");
    public double RoofDays { get; set; } = 1;
    public double GroundDays { get; set; } = 2;
    /// <summary>Dự trữ chữa cháy cộng vào bể ngầm (m³)</summary>
    public double FireReserve { get; set; }
    /// <summary>Số giờ bơm trong ngày</summary>
    public double PumpHours { get; set; } = 4;
    /// <summary>Chiều cao bể mái (đáy → miệng cấp vào) (m)</summary>
    public double RoofTankHeight { get; set; } = 1.5;
    /// <summary>Cao độ đặt bể mái so với sàn tầng cao nhất (m)</summary>
    public double RoofTankStand { get; set; } = 0.5;
    /// <summary>Cao độ mực nước thấp nhất bể ngầm so với ±0,00 (m, âm = dưới đất)</summary>
    public double GroundTankLevel { get; set; } = -1.5;
    /// <summary>Chiều dài ống ngang thêm ngoài chiều cao đẩy (m)</summary>
    public double HorizontalLength { get; set; } = 10;
    public double HazenC { get; set; } = 140;
    /// <summary>Hệ số tổn thất cục bộ (nhân tổn thất dọc đường)</summary>
    public double LocalFactor { get; set; } = 1.3;
    /// <summary>Cột áp tự do tại miệng xả vào bể (m)</summary>
    public double FreeHead { get; set; } = 2;
    public double PumpEfficiency { get; set; } = 0.6;
}

/// <summary>Một mạch điện của tủ.</summary>
public sealed class PanelCircuit
{
    public string Name { get; set; } = "";
    public string Story { get; set; } = "";
    /// <summary>"Chiếu sáng" / "Ổ cắm" / "Điều hoà" / "Bình nóng lạnh"</summary>
    public string Group { get; set; } = "";
    public MepSystemKind System { get; set; }
    public List<int> DeviceIds { get; } = new();
    public List<int> UnitIds { get; } = new();
    public int Points => DeviceIds.Count + UnitIds.Count;
    public double PowerW { get; set; }
    public double Kc { get; set; } = 1;
    public double CurrentA { get; set; }
    public double BreakerA { get; set; }
    public double WireMm2 { get; set; }
    public bool Rcd { get; set; }
    /// <summary>Pha: L1 / L2 / L3 (tủ 1 pha: L)</summary>
    public string Phase { get; set; } = "L";
    public string BreakerText => $"MCB 1P {BreakerA:0}A" + (Rcd ? " + RCD 30mA" : "");
    public string WireText => $"Cu/PVC 2x{N(WireMm2)}+E{N(WireMm2)}";
    internal static string N(double v) => v.ToString("0.#", global::System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
}

/// <summary>Kết quả tủ điện: các mạch, P đặt, P tính toán, dòng tính toán, aptomat / cáp tổng, số pha, tải từng pha, độ lệch pha (%).</summary>
public sealed class PanelResult
{
    public List<PanelCircuit> Circuits { get; } = new();
    public double InstalledW { get; set; }
    public double DemandW { get; set; }
    public bool ThreePhase { get; set; }
    public double[] PhaseW { get; set; } = new double[3];
    public double UnbalancePct { get; set; }
    public double DesignA { get; set; }
    public double MainBreakerA { get; set; }
    public double MainCableMm2 { get; set; }
    public string MainBreakerText => (ThreePhase ? "MCCB 4P " : "MCB 2P ") + $"{MainBreakerA:0}A";
    public string MainCableText => ThreePhase ? $"Cu/XLPE/PVC 3x{PanelCircuit.N(MainCableMm2)}+1x{PanelCircuit.N(Neutral(MainCableMm2))}" : $"Cu/XLPE/PVC 2x{PanelCircuit.N(MainCableMm2)}";
    public static double Neutral(double s) => s <= 16 ? s : ElectricalPanels.Cable.Select(c => c.S).Where(x => x >= s / 2 - 1e-9).DefaultIfEmpty(s).First();
}

public static class ElectricalPanels
{
    public static readonly double[] MainBreakers = { 16, 20, 25, 32, 40, 50, 63, 80, 100, 125, 160, 200, 250, 315, 400 };
    /// <summary>Cáp đồng cách điện XLPE (mm², dòng cho phép A).</summary>
    public static readonly (double S, double Iz)[] Cable = { (4, 42), (6, 54), (10, 75), (16, 100), (25, 127), (35, 158), (50, 192), (70, 246), (95, 298), (120, 346), (150, 395), (185, 450), (240, 538) };
    public const string Lighting = "Chiếu sáng", Socket = "Ổ cắm", Ac = "Điều hoà", Heater = "Bình nóng lạnh";
    public const string DiagramTitle = "SƠ ĐỒ NGUYÊN LÝ TỦ ĐIỆN";

    private static double MainBreaker(double i)
    {
        foreach (double b in MainBreakers) if (b >= i - 1e-9) return b;
        return MainBreakers[^1];
    }

    public static double MainCable(double inom)
    {
        foreach (var (s, iz) in Cable) if (iz >= inom - 1e-9) return s;
        return Cable[^1].S;
    }

    /// <summary>Dây nhỏ nhất ≥ min có Iz ≥ In.</summary>
    public static double WireForBreaker(double inom, double min)
    {
        foreach (var (s, iz) in MepSizing.Wire) if (s >= min - 1e-9 && iz >= inom - 1e-9) return s;
        return MepSizing.Wire[^1].S;
    }

    private static string GroupOf(MepFamily f) => f?.Category switch
    {
        MepCategory.Den or MepCategory.Quat => Lighting,
        MepCategory.OCam => Socket,
        MepCategory.BinhNong => Heater,
        _ => null
    };

    private static List<string> StoryOrder(Project p, IEnumerable<string> stories)
    {
        var set = stories.Distinct().ToList();
        var order = p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name).Where(set.Contains).ToList();
        order.AddRange(set.Where(s => !order.Contains(s)));
        return order;
    }

    /// <summary>Chia mạch + tính tủ (không sửa mô hình).</summary>
    public static PanelResult Build(Project p, PanelOptions o = null)
    {
        o ??= p.Mep?.Settings?.Panel ?? new PanelOptions();
        var res = new PanelResult();
        var m = p.Mep;
        if (m == null) return res;
        var devs = m.Devices.Select(d => (D: d, F: m.FamilyOf(d))).Where(x => x.F != null && x.F.Power > 0 && GroupOf(x.F) != null).ToList();
        var units = (m.Hvac?.Units ?? new List<HvacUnit>()).Where(u => u.Kind == HvacUnitKind.DanLanh).ToList();
        int k = 0;
        foreach (var st in StoryOrder(p, devs.Select(x => x.D.Story).Concat(units.Select(u => u.Story))))
        {
            void Chunk(string grp, int max, MepSystemKind sys)
            {
                var list = devs.Where(x => x.D.Story == st && GroupOf(x.F) == grp).OrderBy(x => x.D.Id).ToList();
                for (int i = 0; i < list.Count; i += Math.Max(1, max))
                {
                    var c = new PanelCircuit { Name = $"C{++k}", Story = st, Group = grp, System = sys };
                    foreach (var x in list.Skip(i).Take(Math.Max(1, max))) { c.DeviceIds.Add(x.D.Id); c.PowerW += x.F.Power; }
                    res.Circuits.Add(c);
                }
            }
            Chunk(Lighting, o.MaxLightingPoints, MepSystemKind.DienChieuSang);
            Chunk(Socket, o.MaxSocketPoints, MepSystemKind.DienOCam);
            foreach (var x in devs.Where(x => x.D.Story == st && GroupOf(x.F) == Heater).OrderBy(x => x.D.Id))
            {
                var c = new PanelCircuit { Name = $"C{++k}", Story = st, Group = Heater, System = MepSystemKind.DienOCam, PowerW = x.F.Power };
                c.DeviceIds.Add(x.D.Id); res.Circuits.Add(c);
            }
            foreach (var u in units.Where(x => x.Story == st).OrderBy(x => x.Id))
            {
                var c = new PanelCircuit { Name = $"C{++k}", Story = st, Group = Ac, System = MepSystemKind.DienDieuHoa, PowerW = HvacLoad.Power(u.CapacityBtu).PowerW };
                c.UnitIds.Add(u.Id); res.Circuits.Add(c);
            }
        }
        foreach (var c in res.Circuits)
        {
            c.CurrentA = MepSizing.Current(c.PowerW, 220, o.CosPhi);
            switch (c.Group)
            {
                case Lighting:
                    c.Kc = o.KcLighting; c.BreakerA = Math.Max(o.MinBreakerLighting, MepSizing.Breaker(c.CurrentA)); c.WireMm2 = WireForBreaker(c.BreakerA, 1.5); break;
                case Socket:
                    c.Kc = o.KcSocket; c.Rcd = true; c.BreakerA = Math.Max(o.MinBreakerSocket, MepSizing.Breaker(c.CurrentA)); c.WireMm2 = WireForBreaker(c.BreakerA, 2.5); break;
                case Heater:
                    c.Kc = o.KcHeater; c.Rcd = true; c.BreakerA = Math.Max(16, MepSizing.Breaker(c.CurrentA)); c.WireMm2 = WireForBreaker(c.BreakerA, 2.5); break;
                default:
                    c.Kc = o.KcAc;
                    var u = units.First(x => x.Id == c.UnitIds[0]);
                    var pw = HvacLoad.Power(u.CapacityBtu);
                    c.CurrentA = pw.CurrentA; c.BreakerA = pw.BreakerA; c.WireMm2 = pw.WireMm2; break;
            }
        }
        res.InstalledW = res.Circuits.Sum(c => c.PowerW);
        res.DemandW = res.Circuits.Sum(c => c.Kc * c.PowerW);
        res.ThreePhase = o.Phases == PanelOptions.Three || (o.Phases != PanelOptions.One && res.DemandW > o.ThreePhaseAboveKw * 1000 + 1e-9);
        if (res.ThreePhase)
        {
            // cân pha: mạch lớn trước, gán vào pha đang nhẹ nhất (theo công suất tính toán)
            foreach (var c in res.Circuits.OrderByDescending(x => x.Kc * x.PowerW).ThenBy(x => res.Circuits.IndexOf(x)))
            {
                int ph = 0;
                for (int i = 1; i < 3; i++) if (res.PhaseW[i] < res.PhaseW[ph] - 1e-9) ph = i;
                res.PhaseW[ph] += c.Kc * c.PowerW; c.Phase = "L" + (ph + 1);
            }
            double avg = res.PhaseW.Average();
            res.UnbalancePct = avg > 1e-9 ? res.PhaseW.Max(v => Math.Abs(v - avg)) / avg * 100 : 0;
            res.DesignA = res.PhaseW.Max() / (220 * o.CosPhi);
        }
        else
        {
            foreach (var c in res.Circuits) c.Phase = "L";
            res.PhaseW = new[] { res.DemandW, 0, 0 };
            res.DesignA = res.DemandW / (220 * o.CosPhi);
        }
        res.MainBreakerA = MainBreaker(Math.Max(res.DesignA, res.Circuits.Count > 0 ? res.Circuits.Max(c => c.BreakerA) : 0));
        res.MainCableMm2 = MainCable(res.MainBreakerA);
        return res;
    }

    /// <summary>Ghi tên mạch vào thiết bị điện (MepDevice.Circuit).</summary>
    public static int Apply(Project p, PanelResult r)
    {
        int n = 0;
        foreach (var c in r.Circuits)
            foreach (int id in c.DeviceIds)
            {
                var d = p.Mep.Devices.FirstOrDefault(x => x.Id == id);
                if (d != null) { d.Circuit = c.Name; n++; }
            }
        return n;
    }

    /// <summary>Bảng tủ điện (Excel).</summary>
    public static KetCauVXT.Report.QtyTable Table(PanelResult r)
    {
        var t = new KetCauVXT.Report.QtyTable
        {
            Title = "Bảng tủ điện: mạch, phụ tải, aptomat, dây, pha" + (r.ThreePhase ? " (tủ 3 pha)" : " (tủ 1 pha)"),
            Headers = new[] { "Mạch", "Tầng", "Loại", "Số điểm", "P đặt (W)", "Kc", "I (A)", "Aptomat", "Dây dẫn", "Pha" },
            Widths = new double[] { 8, 10, 16, 9, 11, 7, 9, 22, 22, 7 }
        };
        foreach (var c in r.Circuits)
            t.Add(c.Name, c.Story, c.Group, c.Points, Math.Round(c.PowerW, 0), c.Kc, Math.Round(c.CurrentA, 1), c.BreakerText, c.WireText, c.Phase);
        t.AddTotal("Tổng", "", "P đặt", r.Circuits.Sum(c => c.Points), Math.Round(r.InstalledW, 0), "", "", "", "", "");
        t.AddTotal("", "", "P tính toán Σ Kc·P", "", Math.Round(r.DemandW, 0), "", Math.Round(r.DesignA, 1), r.MainBreakerText, r.MainCableText,
            r.ThreePhase ? $"lệch {r.UnbalancePct:0.#}%" : "1 pha");
        return t;
    }

    /// <summary>Sơ đồ nguyên lý tủ điện (DXF, tỷ lệ 1: đơn vị mm giấy).</summary>
    public static Figure Diagram(PanelResult r, string panelName = "TĐ")
    {
        if (r.Circuits.Count == 0) return null;
        var w = new DxfWriter(1);
        double th = 2.5, dx = 24, n = r.Circuits.Count, busL = Math.Max(60, (n + 1) * dx);
        double yBus = 0, yTop = 55;
        w.Text(Lay.Chu, busL / 2, yTop + 14, 1.6 * th, DiagramTitle + " " + panelName, 0, 1, 0);
        // nguồn vào: cáp tổng → aptomat tổng → thanh cái
        double x0 = busL / 2;
        w.Line(Lay.Thep, x0, yTop + 8, x0, yTop - 10);
        w.Text(Lay.Chu, x0 + 2, yTop + 4, th, "NGUỒN " + (r.ThreePhase ? "3 PHA 380/220V" : "1 PHA 220V") + " – " + r.MainCableText, 0, 0, 2);
        Breaker(w, x0, yTop - 10, yTop - 22);
        w.Text(Lay.Chu, x0 + 4, yTop - 16, th, r.MainBreakerText, 0, 0, 2);
        w.Line(Lay.Thep, x0, yTop - 22, x0, yBus);
        w.Text(Lay.Chu, x0 + 4, yTop - 30, th, $"P đặt = {r.InstalledW / 1000:0.00} kW; Ptt = {r.DemandW / 1000:0.00} kW; Itt = {r.DesignA:0.0} A", 0, 0, 2);
        w.Polyline(Lay.BeTong, new List<(double X, double Y)> { (0, yBus), (busL, yBus) }, false, 1.0);
        for (int i = 0; i < r.Circuits.Count; i++)
        {
            var c = r.Circuits[i];
            double x = (i + 1) * dx;
            w.Line(Lay.Thep, x, yBus, x, yBus - 8);
            Breaker(w, x, yBus - 8, yBus - 20);
            w.Line(Lay.Thep, x, yBus - 20, x, yBus - 70);
            w.Text(Lay.Chu, x - 1.5, yBus - 22, th, c.BreakerText, 90, 2, 2);
            w.Text(Lay.Chu, x + 2.0, yBus - 22, th, c.WireText, 90, 2, 0);
            w.Text(Lay.Chu, x, yBus - 74, th, c.Name, 0, 1, 2);
            w.Text(Lay.Chu, x, yBus - 80, th * 0.8, c.Group, 0, 1, 2);
            w.Text(Lay.Chu, x, yBus - 85, th * 0.8, $"{c.PowerW:0} W", 0, 1, 2);
            w.Text(Lay.Chu, x, yBus - 90, th * 0.8, $"{c.Story} · {c.Phase}", 0, 1, 2);
        }
        w.Rect(Lay.KhungTen, -6, yBus - 96, busL + 6, yTop + 22);
        return new Figure(w, DiagramTitle);
    }

    /// <summary>Ký hiệu aptomat: đoạn tiếp điểm xiên + dấu ×.</summary>
    private static void Breaker(DxfWriter w, double x, double y1, double y2)
    {
        double h = y1 - y2;
        w.Line(Lay.Thep, x, y1, x, y1 - 0.2 * h);
        w.Line(Lay.Thep, x, y1 - 0.2 * h, x + 0.35 * h, y1 - 0.75 * h);
        w.Line(Lay.Thep, x, y1 - 0.8 * h, x, y2);
        w.Line(Lay.KyHieu, x - 1.2, y1 - 0.2 * h - 1.2, x + 1.2, y1 - 0.2 * h + 1.2);
        w.Line(Lay.KyHieu, x - 1.2, y1 - 0.2 * h + 1.2, x + 1.2, y1 - 0.2 * h - 1.2);
    }
}

// ---------------------------------------------------------------------
// 5. Bể nước, bơm
// ---------------------------------------------------------------------

/// <summary>Kết quả bể / bơm: Q ngày (m³), V bể mái, V bể ngầm, bể chọn (l), Q bơm (m³/h), Ø ống đẩy, H tĩnh, tổn thất, H bơm, công suất (kW).</summary>
public sealed record TankPumpResult(double DailyM3, double RoofM3, double GroundM3, double RoofTankL, double GroundTankL,
    double PumpM3h, double PipeDn, double StaticHead, double FrictionHead, double PumpHead, double PowerKw);

public static class WaterSupplyCalc
{
    /// <summary>Bồn inox thông dụng (lít).</summary>
    public static readonly double[] StandardTanks = { 500, 700, 1000, 1500, 2000, 2500, 3000, 4000, 5000, 6000 };

    /// <summary>Dung tích V (m³) = N·q·ngày / 1000 + dự trữ.</summary>
    public static double Volume(int people, double litersPerPerson, double days, double reserveM3 = 0) =>
        Math.Max(0, people) * Math.Max(0, litersPerPerson) * Math.Max(0, days) / 1000 + Math.Max(0, reserveM3);

    /// <summary>Bồn chuẩn nhỏ nhất ≥ V (l); V &gt; 6000 l → bội số 1000 l.</summary>
    public static double StandardTank(double liters)
    {
        foreach (double s in StandardTanks) if (s >= liters - 1e-6) return s;
        return Math.Ceiling(liters / 1000 - 1e-9) * 1000;
    }

    /// <summary>Tổn thất dọc đường Hazen-Williams (m): hf = 10,67·L·Q^1,852 / (C^1,852·D^4,87), Q m³/s, D m.</summary>
    public static double HazenWilliams(double qM3s, double dM, double lM, double c = 140) =>
        qM3s <= 0 || dM <= 0 ? 0 : 10.67 * lM * Math.Pow(qM3s, 1.852) / (Math.Pow(c, 1.852) * Math.Pow(dM, 4.87));

    /// <summary>Tính bể mái, bể ngầm, bơm cho dự án (cao độ tầng cao nhất lấy từ p.Stories).</summary>
    public static TankPumpResult Compute(Project p, WaterTankOptions o = null)
    {
        o ??= p.Mep?.Settings?.Water ?? new WaterTankOptions();
        double daily = Volume(o.Occupants, o.LitersPerPerson, 1);
        double roof = Volume(o.Occupants, o.LitersPerPerson, o.RoofDays);
        double ground = Volume(o.Occupants, o.LitersPerPerson, o.GroundDays, o.FireReserve);
        double q = daily * o.KDay / Math.Max(0.5, o.PumpHours);              // m³/h
        double qls = q * 1000 / 3600;
        double dn = MepSizing.SupplyPipe(qls);
        double di = MepSizing.Ppr.First(x => Math.Abs(x.Dn - dn) < 1e-9).Di / 1000;
        double top = p.Stories.Count > 0 ? p.Stories.Max(s => s.Elevation) : 0;
        double hst = top + o.RoofTankStand + o.RoofTankHeight - o.GroundTankLevel;
        double L = Math.Max(0, hst) + Math.Max(0, o.HorizontalLength);
        double hf = o.LocalFactor * HazenWilliams(q / 3600, di, L, o.HazenC);
        double H = hst + hf + o.FreeHead;
        double kw = 1000 * 9.81 * (q / 3600) * H / Math.Max(0.1, o.PumpEfficiency) / 1000;
        return new TankPumpResult(daily, roof, ground, StandardTank(roof * 1000), StandardTank(ground * 1000), q, dn, hst, hf, H, kw);
    }

    public static KetCauVXT.Report.QtyTable Table(Project p, WaterTankOptions o = null)
    {
        o ??= p.Mep?.Settings?.Water ?? new WaterTankOptions();
        var r = Compute(p, o);
        var t = new KetCauVXT.Report.QtyTable
        {
            Title = "Bể nước và bơm cấp nước",
            Headers = new[] { "Hạng mục", "Công thức", "Giá trị", "Đơn vị" },
            Widths = new double[] { 26, 46, 12, 8 }
        };
        t.Add("Lượng nước ngày đêm", $"N·q = {o.Occupants}·{o.LitersPerPerson:0}/1000 (q theo TCVN 4513:1988 Bảng 1)", Math.Round(r.DailyM3, 2), "m³");
        t.Add("Bể nước mái", $"N·q·{o.RoofDays:0.#} ngày → bồn {r.RoofTankL:0} l", Math.Round(r.RoofM3, 2), "m³");
        t.Add("Bể ngầm", $"N·q·{o.GroundDays:0.#} ngày + dự trữ {o.FireReserve:0.#} m³ → {r.GroundTankL:0} l", Math.Round(r.GroundM3, 2), "m³");
        t.Add("Lưu lượng bơm", $"Q = Qngày·Kngày {o.KDay:0.##} / {o.PumpHours:0.#} h (Kngày theo TCVN 4513:1988)", Math.Round(r.PumpM3h, 2), "m³/h");
        t.Add("Ống đẩy PPR", $"v ≤ {MepStandards.Get(p.Mep?.Settings?.Standards, "cap.v_max"):0.0#} m/s (Theo TCVN 4513:1988)", r.PipeDn, "mm");
        t.Add("Chiều cao đẩy tĩnh", "z miệng bể mái − z mực nước bể ngầm", Math.Round(r.StaticHead, 2), "m");
        t.Add("Tổn thất", $"{o.LocalFactor:0.#}·Hazen-Williams (C = {o.HazenC:0})", Math.Round(r.FrictionHead, 2), "m");
        t.Add("Cột áp bơm", $"H = Htĩnh + tổn thất + {o.FreeHead:0.#} m tự do", Math.Round(r.PumpHead, 2), "m");
        t.Add("Công suất bơm", $"ρ·g·Q·H / η (η = {o.PumpEfficiency:0.##})", Math.Round(r.PowerKw, 2), "kW");
        return t;
    }
}

// ---------------------------------------------------------------------
// 6. Sơ đồ không gian cấp nước, sơ đồ ống đứng thoát nước
// ---------------------------------------------------------------------

public static class RiserDiagrams
{
    public const string SupplyTitle = "SƠ ĐỒ KHÔNG GIAN CẤP NƯỚC";
    public const string DrainTitle = "SƠ ĐỒ ỐNG ĐỨNG THOÁT NƯỚC";
    private const double Gap = 40, Th = 2.5, Iso = 0.7071;

    private static List<Story> Stories(Project p, IEnumerable<string> used)
    {
        var set = used.ToHashSet();
        return p.Stories.OrderBy(s => s.Elevation).Where(s => set.Contains(s.Name)).ToList();
    }

    private static string N(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Sơ đồ không gian cấp nước: bể ngầm → bơm → ống đẩy lên bể mái → trục cấp xuống từng tầng, nhánh tầng xiên 45° có tên thiết bị, Ø.</summary>
    public static Figure Supply(Project p)
    {
        var m = p.Mep; if (m == null) return null;
        var devs = m.Devices.Select(d => (D: d, F: m.FamilyOf(d))).Where(x => x.F != null && x.F.SupplyUnits > 0).ToList();
        if (devs.Count == 0) return null;
        var sts = Stories(p, devs.Select(x => x.D.Story));
        if (sts.Count == 0) return null;
        var w = new DxfWriter(1);
        double xr = 0, yTop = sts.Count * Gap + 20;
        TankPumpResult tp = null;
        try { if ((m.Settings.Water?.Occupants ?? 0) > 0) tp = WaterSupplyCalc.Compute(p); } catch (Exception) { }
        // bể mái + trục cấp xuống
        w.Rect(Lay.BeTong, xr - 10, yTop, xr + 10, yTop + 10);
        w.Text(Lay.Chu, xr + 13, yTop + 5, Th, "BỂ NƯỚC MÁI" + (tp != null ? $" V = {tp.RoofTankL:0} l" : ""), 0, 0, 2);
        double nAll = devs.Sum(x => x.F.SupplyUnits);
        w.Line(Lay.Thep, xr, yTop, xr, 0);
        // bể ngầm + bơm + ống đẩy (bên trái)
        double xp = -40;
        w.Rect(Lay.BeTong, xp - 10, -25, xp + 10, -12);
        w.Text(Lay.Chu, xp, -29, Th, "BỂ NGẦM" + (tp != null ? $" V = {tp.GroundTankL:0} l" : ""), 0, 1, 2);
        w.Circle(Lay.KyHieu, xp, -6, 3);
        w.Text(Lay.Chu, xp - 5, -6, Th, "BƠM" + (tp != null ? $" Q = {tp.PumpM3h:0.0} m³/h; H = {tp.PumpHead:0.0} m" : ""), 0, 2, 2);
        w.Line(Lay.Thep, xp, -12, xp, -9);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xp, -3), (xp, yTop + 5), (xr - 10, yTop + 5) }, false);
        w.Text(Lay.Chu, xp - 2, yTop / 2, Th, "ỐNG ĐẨY" + (tp != null ? $" PPR Ø{tp.PipeDn:0}" : ""), 90, 1, 0);
        double below = nAll;
        for (int i = sts.Count - 1; i >= 0; i--)
        {
            var st = sts[i];
            double y = (i + 1) * Gap;
            var fl = devs.Where(x => x.D.Story == st.Name).ToList();
            w.Line(Lay.KhungTen, xr - 30, y - 12, xr + 140, y - 12, "DASHED");
            w.Text(Lay.Chu, xr - 30, y - 10, Th, $"{st.Name} ({N(st.Elevation)})", 0, 0, 0);
            double nFl = fl.Sum(x => x.F.SupplyUnits);
            // đoạn trục từ tầng trên xuống tầng này mang lưu lượng của tầng này và các tầng dưới
            double dRiser = MepSizing.SupplyPipe(MepSizing.SupplyFlow(below));
            w.Text(Lay.Chu, xr - 2, y + Gap / 2, Th, $"Ø{dRiser:0}", 90, 1, 0);
            below -= nFl;
            double dBr = MepSizing.SupplyPipe(MepSizing.SupplyFlow(nFl));
            double L = 20 + 18 * fl.Count;
            double ex = xr + L * Iso, ey = y + L * Iso * 0.5;
            w.Line(Lay.Thep, xr, y, ex, ey);
            w.Text(Lay.Chu, xr + 6, y + 4, Th, $"Ø{dBr:0}", 26.565, 0, 0);
            for (int k = 0; k < fl.Count; k++)
            {
                double s = 20 + 18 * k;
                double bx = xr + s * Iso, by = y + s * Iso * 0.5;
                w.Line(Lay.Thep, bx, by, bx, by + 6);
                w.Circle(Lay.KyHieu, bx, by + 6, 1.0);
                w.Text(Lay.Chu, bx, by + 8, Th * 0.8, fl[k].F.Name, 90, 0, 2);
            }
        }
        w.Text(Lay.Chu, 50, yTop + 25, 1.6 * Th, SupplyTitle, 0, 1, 0);
        return new Figure(w, SupplyTitle);
    }

    /// <summary>Sơ đồ ống đứng thoát nước: ống đứng thoát xí (WC) và ống đứng thoát rửa; nhánh tầng xiên 45°, Ø theo DFU luỹ kế, ống thông hơi trên mái, hố ga / bể tự hoại dưới chân.</summary>
    public static Figure Drain(Project p)
    {
        var m = p.Mep; if (m == null) return null;
        var devs = m.Devices.Select(d => (D: d, F: m.FamilyOf(d))).Where(x => x.F != null && x.F.DrainUnits > 0).ToList();
        if (devs.Count == 0) return null;
        var sts = Stories(p, devs.Select(x => x.D.Story));
        if (sts.Count == 0) return null;
        var w = new DxfWriter(1);
        double yTop = sts.Count * Gap + 10;
        var stacks = new[] { (Name: "ỐNG ĐỨNG THOÁT XÍ", Wc: true, X: 0.0), (Name: "ỐNG ĐỨNG THOÁT RỬA", Wc: false, X: 90.0) };
        foreach (var sk in stacks)
        {
            var list = devs.Where(x => (x.F.Symbol == "boncau") == sk.Wc).ToList();
            if (list.Count == 0) continue;
            double x0 = sk.X;
            w.Line(Lay.Thep, x0, yTop + 15, x0, -10);
            w.Text(Lay.Chu, x0 + 2, yTop + 15, Th, "THÔNG HƠI Ø" + (sk.Wc ? "90" : "60"), 0, 0, 2);
            w.Text(Lay.Chu, x0, yTop + 22, Th, sk.Name, 0, 1, 0);
            w.Rect(Lay.BeTong, x0 - 12, -22, x0 + 12, -10);
            w.Text(Lay.Chu, x0, -26, Th, sk.Wc ? "BỂ TỰ HOẠI" : "HỐ GA", 0, 1, 2);
            double acc = 0;
            for (int i = sts.Count - 1; i >= 0; i--)
            {
                var st = sts[i];
                double y = (i + 1) * Gap - 15;
                var fl = list.Where(x => x.D.Story == st.Name).ToList();
                if (sk.X < 1e-9)
                {
                    w.Line(Lay.KhungTen, -30, y - 4, 180, y - 4, "DASHED");
                    w.Text(Lay.Chu, -30, y - 2, Th, $"{st.Name} ({N(st.Elevation)})", 0, 0, 0);
                }
                double dfu = fl.Sum(x => x.F.DrainUnits);
                acc += dfu;
                double dStack = Math.Max(sk.Wc ? 110 : 60, MepSizing.DrainPipe(acc, sk.Wc));
                w.Text(Lay.Chu, x0 - 2, y - Gap / 2, Th, $"Ø{dStack:0}", 90, 1, 0);
                if (fl.Count == 0) continue;
                double dBr = MepSizing.DrainPipe(dfu, sk.Wc);
                double L = 14 + 14 * fl.Count;
                w.Line(Lay.Thep, x0, y, x0 + L * Iso, y + L * Iso * 0.5);
                w.Text(Lay.Chu, x0 + 5, y + 3.5, Th, $"Ø{dBr:0}", 26.565, 0, 0);
                for (int k = 0; k < fl.Count; k++)
                {
                    double s = 14 + 14 * k, bx = x0 + s * Iso, by = y + s * Iso * 0.5;
                    w.Line(Lay.Thep, bx, by, bx, by + 6);
                    w.Text(Lay.Chu, bx, by + 8, Th * 0.8, fl[k].F.Name, 90, 0, 2);
                }
            }
        }
        w.Text(Lay.Chu, 45, yTop + 32, 1.6 * Th, DrainTitle, 0, 1, 0);
        return new Figure(w, DrainTitle);
    }
}

/// <summary>Gom phần đợt 4 vào bộ bản vẽ điện nước và sheet Excel "Vật tư điện nước".</summary>
public static class MepRound4
{
    /// <summary>Hình vẽ sơ đồ (tỷ lệ 1, không theo tỷ lệ mặt bằng).</summary>
    public static bool IsSchematic(string name) => name == ElectricalPanels.DiagramTitle || name == RiserDiagrams.SupplyTitle || name == RiserDiagrams.DrainTitle || MepRound5.IsSchematic(name);

    public static List<Figure> Figures(Project p)
    {
        var res = new List<Figure>();
        if (p.Mep == null || p.Mep.IsEmpty) return res;
        try { var f = ElectricalPanels.Diagram(ElectricalPanels.Build(p)); if (f != null) res.Add(f); } catch (Exception) { }
        try { var f = RiserDiagrams.Supply(p); if (f != null) res.Add(f); } catch (Exception) { }
        try { var f = RiserDiagrams.Drain(p); if (f != null) res.Add(f); } catch (Exception) { }
        try { res.AddRange(MepRound5.Figures(p)); } catch (Exception) { }
        return res;
    }

    public static List<KetCauVXT.Report.QtyTable> Tables(Project p)
    {
        var res = new List<KetCauVXT.Report.QtyTable>();
        if (p.Mep == null || p.Mep.IsEmpty) return res;
        try { var r = ElectricalPanels.Build(p); if (r.Circuits.Count > 0) res.Add(ElectricalPanels.Table(r)); } catch (Exception) { }
        try { if ((p.Mep.Settings.Water?.Occupants ?? 0) > 0) res.Add(WaterSupplyCalc.Table(p)); } catch (Exception) { }
        try
        {
            if (p.Mep.Hvac?.Settings?.CoolingMethod == HvacMount.DetailMethod && (p.Arch?.Rooms?.Count ?? 0) > 0)
            {
                var so = p.Mep.Settings?.Standards;
                var t = HvacCoolingDetailed.Table(p, so?.CoolingFromClimate == true ? ClimateCooling.Input(so) : null);
                if (t.Rows.Count > 0) res.Add(t);
            }
        }
        catch (Exception) { }
        // chiếu sáng (LPD, phương pháp quang thông), ống đứng – ống thoát ra, bể tự hoại – Mep/MepRound5.cs
        try { res.AddRange(MepRound5.Tables(p)); } catch (Exception) { }
        // có bảng tính (tủ điện / bể – bơm / tải lạnh) → kèm bảng thông số tiêu chuẩn đã áp dụng (nguồn TCVN, giá trị tuỳ chỉnh)
        if (res.Count > 0) res.Add(MepStandards.Table(p.Mep.Settings?.Standards));
        return res;
    }
}
