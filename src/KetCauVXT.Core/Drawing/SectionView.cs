using System.Globalization;
using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

// =====================================================================
// MẶT CẮT ĐỨNG CÔNG TRÌNH (kiểu Revit): cắt khối 3D (ModelSolids) bằng mặt phẳng đứng qua đường cắt,
// tự ghi cao độ các tầng, chuỗi kích thước chiều cao tầng + tổng, trục định vị cắt qua và kích thước trục.
// Mặt cắt 1-1 (theo phương X) và 2-2 (theo phương Y) tự sinh qua giữa công trình; người dùng vẽ thêm (ArchModel.Sections).
// =====================================================================

/// <summary>Một phần tử bị cắt: toạ độ dọc đường cắt u (m, từ điểm đầu) và cao độ z (m).</summary>
public readonly record struct SectionCutItem(string Id, string Category, Discipline Discipline, double U0, double U1, double Z0, double Z1);

/// <summary>Kết quả dựng mặt cắt.</summary>
public sealed class SectionDrawing
{
    public SectionLine Line { get; init; }
    public List<SectionCutItem> Items { get; init; } = new();
    /// <summary>Phần tử nhìn thấy phía sau mặt phẳng cắt (hình chiếu, nét mảnh)</summary>
    public List<SectionCutItem> Beyond { get; init; } = new();
    /// <summary>Cao độ các mức ghi trên mặt cắt (m, thấp → cao)</summary>
    public List<double> Levels { get; init; } = new();
    /// <summary>Vị trí trục cắt qua (u, tên)</summary>
    public List<(double U, string Name)> Axes { get; init; } = new();
    public Figure Figure { get; init; }
    public string Title => "MẶT CẮT " + Line?.Name;
    /// <summary>Hình móng đã vẽ (rỗng khi không thể hiện móng)</summary>
    public List<FoundationShape> Foundations { get; init; } = new();
    /// <summary>Vật liệu đã tô (chú thích vật liệu)</summary>
    public List<SectionMaterial> Materials { get; init; } = new();
    public bool ShowFoundation { get; init; }
    /// <summary>Cốt sân (m)</summary>
    public double SiteLevel { get; init; }
}

public static class SectionView
{
    /// <summary>Mặt cắt tự sinh: 1-1 dọc trục X qua giữa nhịp giữa theo Y, 2-2 dọc trục Y qua giữa nhịp giữa theo X.</summary>
    public static List<SectionLine> Defaults(Project p)
    {
        var (x0, y0, x1, y1) = Extents(p);
        if (x1 - x0 < 1e-6 && y1 - y0 < 1e-6) return new List<SectionLine>();
        double ext = 1.5;
        // chọn nhịp giữa có NHIỀU SÀN BỊ CẮT NHẤT (tránh ô cầu thang / lỗ thông tầng làm mất sàn trên mặt cắt), ưu tiên gần giữa nhà
        double ym = BestBay(p, p.GridsY.Select(g => g.Coord).ToList(), y0, y1, c => new SectionLine { X1 = x0 - ext, Y1 = c, X2 = x1 + ext, Y2 = c });
        double xm = BestBay(p, p.GridsX.Select(g => g.Coord).ToList(), x0, x1, c => new SectionLine { X1 = c, Y1 = y0 - ext, X2 = c, Y2 = y1 + ext });
        return new List<SectionLine>
        {
            new() { Name = "1-1", X1 = x0 - ext, Y1 = ym, X2 = x1 + ext, Y2 = ym, Auto = true },
            new() { Name = "2-2", X1 = xm, Y1 = y0 - ext, X2 = xm, Y2 = y1 + ext, Auto = true }
        };
    }

    /// <summary>Mọi mặt cắt: tự sinh + người dùng vẽ.</summary>
    public static List<SectionLine> All(Project p) => Defaults(p).Concat(p.Arch?.Sections ?? new List<SectionLine>()).ToList();

    /// <summary>Tên mặt cắt kế tiếp ("3-3", "4-4"…).</summary>
    public static string NextName(Project p)
    {
        var used = All(p).Select(s => s.Name).ToHashSet();
        for (int k = 1; ; k++) { string n = $"{k}-{k}"; if (!used.Contains(n)) return n; }
    }

    /// <summary>Thêm mặt cắt do người dùng vẽ bằng 2 điểm (m). null nếu 2 điểm trùng.</summary>
    public static SectionLine Add(Project p, double x1, double y1, double x2, double y2, string name = null)
    {
        if (Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1)) < 0.1) return null;
        p.Arch ??= new ArchModel();
        var s = new SectionLine { Name = string.IsNullOrWhiteSpace(name) ? NextName(p) : name, X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 };
        p.Arch.Sections.Add(s);
        return s;
    }

    /// <summary>Nhịp có số khối sàn bị cắt lớn nhất (đường cắt qua giữa nhịp); bằng nhau → gần nhịp giữa nhất.</summary>
    private static double BestBay(Project p, List<double> coords, double lo, double hi, Func<double, SectionLine> line)
    {
        coords = coords.Distinct().OrderBy(c => c).ToList();
        if (coords.Count < 3) return MidBay(coords, lo, hi);
        List<Solid> solids;
        try { solids = ModelSolids.Build(p, null, false).Where(so => so.Category is "Sàn" or "Sàn nền").ToList(); }
        catch (Exception) { return MidBay(coords, lo, hi); }
        int mid = Math.Max(0, coords.Count / 2 - 1);
        double best = MidBay(coords, lo, hi); int bestN = -1, bestD = int.MaxValue;
        for (int k = 0; k + 1 < coords.Count; k++)
        {
            double c = (coords[k] + coords[k + 1]) / 2;
            var sl = line(c);
            double L = sl.Length;
            if (L < 1e-9) continue;
            double tx = (sl.X2 - sl.X1) / L, ty = (sl.Y2 - sl.Y1) / L;
            int n = solids.Count(so => CutOne(so, sl.X1, sl.Y1, tx, ty, -ty, tx, L) != null);
            int d = Math.Abs(k - mid);
            if (n > bestN || (n == bestN && d < bestD)) { best = c; bestN = n; bestD = d; }
        }
        return best;
    }

    private static double MidBay(List<double> coords, double lo, double hi)
    {
        coords = coords.OrderBy(c => c).ToList();
        if (coords.Count >= 2)
        {
            int k = Math.Max(0, coords.Count / 2 - 1);
            return (coords[k] + coords[k + 1]) / 2;
        }
        return (lo + hi) / 2;
    }

    private static (double X0, double Y0, double X1, double Y1) Extents(Project p)
    {
        var xs = p.GridsX.Select(g => g.Coord).Concat(p.Nodes.Select(n => n.X)).Concat(p.Walls.SelectMany(w => new[] { w.X1, w.X2 })).ToList();
        var ys = p.GridsY.Select(g => g.Coord).Concat(p.Nodes.Select(n => n.Y)).Concat(p.Walls.SelectMany(w => new[] { w.Y1, w.Y2 })).ToList();
        if (xs.Count == 0 || ys.Count == 0) return (0, 0, 0, 0);
        return (xs.Min(), ys.Min(), xs.Max(), ys.Max());
    }

    /// <summary>Cắt mọi khối bằng mặt phẳng đứng qua đường cắt (giới hạn trong đoạn cắt). Tường có ô cửa: trừ phần lỗ.</summary>
    public static List<SectionCutItem> Cut(Project p, SectionLine s, IEnumerable<Discipline> disciplines = null)
    {
        double L = s.Length;
        var res = new List<SectionCutItem>();
        if (L < 1e-9) return res;
        double tx = (s.X2 - s.X1) / L, ty = (s.Y2 - s.Y1) / L, nx = -ty, ny = tx;
        var solids = ModelSolids.Build(p, disciplines, true);
        var raw = new List<(Solid S, SectionCutItem I)>();
        foreach (var so in solids)
        {
            var c = CutOne(so, s.X1, s.Y1, tx, ty, nx, ny, L);
            if (c != null) raw.Add((so, c.Value));
        }
        var voids = raw.Where(r => r.S.IsVoid).ToList();
        foreach (var (so, it) in raw.Where(r => !r.S.IsVoid))
        {
            if (so.Category == "Tường")
            {
                var holes = voids.Where(v => v.S.HostId == so.Id && Overlap(v.I.U0, v.I.U1, it.U0, it.U1) > 0.9 * (it.U1 - it.U0)).Select(v => v.I).ToList();
                if (holes.Count > 0)
                {
                    foreach (var piece in SplitZ(it, holes)) res.Add(piece);
                    continue;
                }
            }
            res.Add(it);
        }
        return res;
    }

    private static double Overlap(double a0, double a1, double b0, double b1) => Math.Max(0, Math.Min(a1, b1) - Math.Max(a0, b0));

    private static IEnumerable<SectionCutItem> SplitZ(SectionCutItem w, List<SectionCutItem> holes)
    {
        double z = w.Z0;
        foreach (var h in holes.OrderBy(h => h.Z0))
        {
            if (h.Z0 > z + 1e-6) yield return w with { Z0 = z, Z1 = Math.Min(h.Z0, w.Z1) };
            z = Math.Max(z, h.Z1);
        }
        if (w.Z1 > z + 1e-6) yield return w with { Z0 = z };
    }

    /// <summary>Giao khối lăng trụ với mặt phẳng đứng: hình chữ nhật bao (u, z) của các giao điểm cạnh – mặt phẳng.</summary>
    private static SectionCutItem? CutOne(Solid so, double x0, double y0, double tx, double ty, double nx, double ny, double L)
    {
        var (bot, top) = so.Rings();
        int n = bot.Count;
        if (n < 3) return null;
        double Sd(Vec3 v) => (v.X - x0) * nx + (v.Y - y0) * ny;
        double U(Vec3 v) => (v.X - x0) * tx + (v.Y - y0) * ty;
        var pts = new List<(double U, double Z)>();
        void Edge(Vec3 a, Vec3 b)
        {
            double da = Sd(a), db = Sd(b);
            if (Math.Abs(da) < 1e-9) pts.Add((U(a), a.Z));
            if (Math.Abs(db) < 1e-9) pts.Add((U(b), b.Z));
            if (da * db < 0)
            {
                double t = da / (da - db);
                var q = a + (b - a) * t;
                pts.Add((U(q), q.Z));
            }
        }
        for (int i = 0; i < n; i++)
        {
            Edge(bot[i], bot[(i + 1) % n]);
            Edge(top[i], top[(i + 1) % n]);
            Edge(bot[i], top[i]);
        }
        if (pts.Count < 2) return null;
        double u0 = pts.Min(q => q.U), u1 = pts.Max(q => q.U), z0 = pts.Min(q => q.Z), z1 = pts.Max(q => q.Z);
        if (u1 < -1e-9 || u0 > L + 1e-9) return null;
        u0 = Math.Max(0, u0); u1 = Math.Min(L, u1);
        if (u1 - u0 < 1e-6 || z1 - z0 < 1e-6) return null;
        return new SectionCutItem(so.Id, so.Category, so.Discipline, Math.Round(u0, 9), Math.Round(u1, 9), Math.Round(z0, 9), Math.Round(z1, 9));
    }

    /// <summary>Chiều sâu nhìn mặc định (m): tới trục song song kế tiếp phía sau đường cắt, tối đa 6 m.</summary>
    public static double ViewDepth(Project p, SectionLine s)
    {
        double L = s.Length;
        if (L < 1e-9) return 6;
        double tx = (s.X2 - s.X1) / L, ty = (s.Y2 - s.Y1) / L, nx = -ty, ny = tx;
        var ds = new List<double>();
        if (Math.Abs(ny) > 0.9) ds.AddRange(p.GridsY.Select(g => (g.Coord - s.Y1) * ny));
        if (Math.Abs(nx) > 0.9) ds.AddRange(p.GridsX.Select(g => (g.Coord - s.X1) * nx));
        var ahead = ds.Where(d => d > 0.05).OrderBy(d => d).ToList();
        return ahead.Count > 0 ? Math.Min(6, ahead[0] + 0.3) : 6;
    }

    /// <summary>Hình chiếu (u, z) các khối nằm hoàn toàn phía sau mặt phẳng cắt trong chiều sâu nhìn (hướng pháp tuyến trái đường cắt),
    /// không bị cắt; bỏ phần đã bị che hoàn toàn bởi phần tử bị cắt cùng loại.</summary>
    public static List<SectionCutItem> Beyond(Project p, SectionLine s, List<SectionCutItem> cut = null, IEnumerable<Discipline> disciplines = null, double depth = 0)
    {
        double L = s.Length;
        var res = new List<SectionCutItem>();
        if (L < 1e-9) return res;
        if (depth <= 0) depth = ViewDepth(p, s);
        double tx = (s.X2 - s.X1) / L, ty = (s.Y2 - s.Y1) / L, nx = -ty, ny = tx;
        cut ??= Cut(p, s, disciplines);
        var cutIds = cut.Select(c => c.Id).ToHashSet();
        List<Solid> solids;
        try { solids = ModelSolids.Build(p, disciplines, false); } catch (Exception) { return res; }
        foreach (var so in solids)
        {
            if (cutIds.Contains(so.Id)) continue;
            var (bot, top) = so.Rings();
            if (bot.Count < 3) continue;
            var all = bot.Concat(top).ToList();
            var sd = all.Select(v => (v.X - s.X1) * nx + (v.Y - s.Y1) * ny).ToList();
            if (sd.Min() < -1e-6 || sd.Min() > depth) continue;
            double u0 = all.Min(v => (v.X - s.X1) * tx + (v.Y - s.Y1) * ty), u1 = all.Max(v => (v.X - s.X1) * tx + (v.Y - s.Y1) * ty);
            double z0 = all.Min(v => v.Z), z1 = all.Max(v => v.Z);
            if (u1 < -1e-9 || u0 > L + 1e-9) continue;
            u0 = Math.Max(0, u0); u1 = Math.Min(L, u1);
            if (u1 - u0 < 1e-6 || z1 - z0 < 1e-6) continue;
            var it = new SectionCutItem(so.Id, so.Category, so.Discipline, Math.Round(u0, 9), Math.Round(u1, 9), Math.Round(z0, 9), Math.Round(z1, 9));
            if (cut.Any(c => c.U0 <= it.U0 + 1e-6 && c.U1 >= it.U1 - 1e-6 && c.Z0 <= it.Z0 + 1e-6 && c.Z1 >= it.Z1 - 1e-6)) continue;   // bị che
            res.Add(it);
        }
        return res;
    }

    public static string ElevText(double z)
    {
        if (Math.Abs(z) < 5e-4) return "±0.000";
        return (z > 0 ? "+" : "-") + Math.Abs(z).ToString("0.000", CultureInfo.InvariantCulture);
    }

    /// <summary>Dựng hình mặt cắt (DXF, mm) với cao độ, chuỗi kích thước tầng, tổng chiều cao, trục và kích thước trục.
    /// opt: thể hiện móng (mặc định theo SectionLine.ShowFoundation, null → có), tô vật liệu, chú thích vật liệu.</summary>
    public static SectionDrawing Draw(Project p, SectionLine s, double scale = 100, IEnumerable<Discipline> disciplines = null, SectionOptions opt = null)
    {
        opt ??= new SectionOptions();
        bool showFound = opt.ShowFoundation ?? s.ShowFoundation ?? true;
        var items = Cut(p, s, disciplines).Where(i => i.Category != ChaseCategory).ToList();
        var w = new DxfWriter(scale);
        double L = s.Length;
        var lv = KetCauVXT.Model.Levels.All(p);
        double zNen = lv.FirstOrDefault(l => l.Kind == LevelKind.Nen)?.Elevation ?? p.GroundElevation;
        double zSite = zNen - Math.Max(0, opt.SiteOffset);
        if (!showFound)
        {
            // MẶT CẮT KIẾN TRÚC: bỏ phần dưới cốt sân (móng, cổ cột, dầm móng) – cắt tại cốt sân
            items = items.Where(i => i.Z1 > zSite + 1e-6).Select(i => i.Z0 < zSite ? i with { Z0 = zSite } : i).ToList();
            lv = lv.Where(l => l.Kind is not (LevelKind.DayMong or LevelKind.MatMong)).ToList();
        }
        var floorLv = lv.Where(l => l.Kind is LevelKind.Nen or LevelKind.San or LevelKind.Mai or LevelKind.Tum).Select(l => l.Elevation).Distinct().OrderBy(z => z).ToList();
        double uMin = items.Count > 0 ? items.Min(i => i.U0) : 0, uMax = items.Count > 0 ? items.Max(i => i.U1) : L;
        var used = new HashSet<SectionMaterial>();
        void Hat(SectionMaterial m, double a, double c, double b, double d)
        {
            if (!opt.Materials) return;
            if (SectionMaterials.HatchRect(w, m, a, c, b, d) > 0) used.Add(m);
        }
        void Box(string layer, double a, double c, double b, double d, string lt = null) =>
            w.Polyline(layer, new List<(double X, double Y)> { (a, c), (b, c), (b, d), (a, d) }, true, 0, lt);

        // móng (chỉ khi thể hiện móng)
        var found = showFound ? FoundationShapes(p, s, opt.Footings) : new List<FoundationShape>();
        foreach (var f in found)
        {
            var pts = f.Points.Select(q => (q.U * 1000, q.Z * 1000)).ToList();
            w.Polyline(Lay.BeTong, pts, true);
            if (opt.Materials && SectionMaterials.Hatch(w, f.Material, pts) > 0) used.Add(f.Material);
            if (!opt.Materials && f.Material == SectionMaterial.BTCT) w.Hatch(new List<IList<(double X, double Y)>> { pts }, HatchPatterns.Solid, 1, 0, Lay.BeTong);
            if (f.BreakAtBottom)
            {
                // nét cắt ngắt cọc (zíc zắc)
                double y = f.Points.Min(q => q.Z) * 1000, x0 = f.Points.Min(q => q.U) * 1000, x1 = f.Points.Max(q => q.U) * 1000, dx = (x1 - x0) / 4;
                w.Polyline(Lay.KyHieu, new List<(double X, double Y)> { (x0 - dx, y), (x0 + dx, y + dx), (x0 + 2 * dx, y - dx), (x0 + 3 * dx, y + dx), (x1 + dx, y) }, false);
            }
            if (!string.IsNullOrEmpty(f.Label))
            {
                double cx = f.Points.Average(q => q.U) * 1000, tyLbl = f.Points.Min(q => q.Z) * 1000 - 1.8 * w.Th;
                w.Text(Lay.Chu, cx, tyLbl, w.Th * 0.9, f.Label, 0, 1, 0);
            }
        }

        // phần tử cắt
        double zTop = floorLv.Count > 0 ? floorLv.Max() : 0;
        foreach (var it in items.OrderBy(i => i.Category == "Tường" ? 0 : 1))
        {
            double a = it.U0 * 1000, b = it.U1 * 1000, c = it.Z0 * 1000, d = it.Z1 * 1000;
            switch (it.Category)
            {
                case "Cột": case "Dầm": case "Sàn":
                    Box(Lay.BeTong, a, c, b, d);
                    if (opt.Materials) Hat(SectionMaterial.BTCT, a, c, b, d); else w.FilledRect(Lay.BeTong, a, c, b, d);
                    if (it.Category == "Sàn" && opt.Materials) SlabFinish(p, it, zTop, w, Box, Hat);
                    break;
                case "Sàn nền":
                    Box(Lay.BeTong, a, c, b, d);
                    if (opt.Materials)
                    {
                        Hat(SectionMaterial.BTCT, a, c, b, d);
                        // lớp dưới sàn nền: bê tông lót 100, cát đệm 100 (trên nền đất đầm chặt)
                        Box(Lay.BeTong, a, c - 100, b, c); Hat(SectionMaterial.BeTongLot, a, c - 100, b, c);
                        Box(Lay.KyHieu, a, c - 200, b, c - 100); Hat(SectionMaterial.Cat, a, c - 200, b, c - 100);
                        Hat(SectionMaterial.DatDap, a, Math.Min(c - 200, zSite * 1000) - 0, b, c - 200);
                    }
                    else w.FilledRect(Lay.BeTong, a, c, b, d);
                    break;
                case "Tường":
                    WallLayers(p, s, it, w, Box, Hat, opt.Materials);
                    break;
                case "Cửa đi": case "Cửa sổ":
                {
                    bool win = it.Category == "Cửa sổ";
                    Box(Lay.KyHieu, a, c, b, d);
                    double fr = 40;   // khuôn cửa 40 mm
                    if (b - a > 2 * fr && d - c > 2 * fr) Box(Lay.KyHieu, a + fr, c + (win ? fr : 0), b - fr, d - fr);
                    double um = (a + b) / 2;
                    if (win) { w.Line(Lay.KyHieu, um - 6, c + fr, um - 6, d - fr); w.Line(Lay.KyHieu, um + 6, c + fr, um + 6, d - fr); }   // kính 2 nét
                    else w.Line(Lay.KyHieu, um, c, um, d - fr);   // cánh cửa
                    if (opt.Materials) { if (win) used.Add(SectionMaterial.Kinh); else used.Add(SectionMaterial.Go); }
                    break;
                }
                case "Trần":
                {
                    Box(Lay.KyHieu, a, c, b, d);
                    Hat(SectionMaterial.ThachCao, a, c, b, d);
                    // ty treo trần lên kết cấu phía trên (bước 1,2 m)
                    var above = items.Where(q => q.Category is "Sàn" or "Dầm" && q.Z0 > it.Z1 - 1e-6 && q.U1 > it.U0 && q.U0 < it.U1).OrderBy(q => q.Z0).FirstOrDefault();
                    if (above.Id != null)
                        for (double u = it.U0 + 0.3; u < it.U1 - 0.2; u += 1.2)
                            w.Line(Lay.KyHieu, u * 1000, d, u * 1000, above.Z0 * 1000);
                    break;
                }
                case "Mái":
                    Box(Lay.BeTong, a, c, b, d);
                    Hat(it.Discipline == Discipline.KetCau ? SectionMaterial.BTCT : SectionMaterial.ThepHinh, a, c, b, d);
                    break;
                default:
                    Box(it.Discipline == Discipline.KetCau ? Lay.BeTong : Lay.KyHieu, a, c, b, d);
                    break;
            }
        }
        // phần tử NHÌN THẤY phía sau mặt phẳng cắt (trong chiều sâu nhìn): nét mảnh, không tô
        var beyond = Beyond(p, s, items, disciplines).Where(i => i.Category != ChaseCategory).ToList();
        if (!showFound)
            beyond = beyond.Where(i => i.Z1 > zSite + 1e-6).Select(i => i.Z0 < zSite ? i with { Z0 = zSite } : i).ToList();
        foreach (var it in beyond)
            Box(Lay.KyHieu, it.U0 * 1000, it.Z0 * 1000, it.U1 * 1000, it.Z1 * 1000);
        // mức sàn không có sàn bị cắt (ô cầu thang / lỗ thông tầng trên đường cắt): ghi chú
        foreach (double zl in floorLv.Where(z => z > 1e-6))
            if (!items.Any(i => i.Category is "Sàn" or "Sàn nền" && Math.Abs(i.Z1 - zl) < 0.15))
                w.Text(Lay.Chu, (uMin + uMax) / 2 * 1000, zl * 1000 + 150, w.Th * 0.8, "(đường cắt qua ô cầu thang / lỗ sàn – không có sàn)", 0, 1, 0);

        // cốt sân + đất (cả hai chế độ): đường mặt đất và dải đất 300 mm bên dưới, chừa chỗ phần móng
        double gl = zSite * 1000, gx0 = uMin * 1000 - 1200, gx1 = uMax * 1000 + 1200;
        w.Line(Lay.BeTong, gx0, gl, gx1, gl);
        var gaps = found.Where(f => f.Points.Max(q => q.Z) > zSite - 0.3).Select(f => (f.Points.Min(q => q.U) * 1000, f.Points.Max(q => q.U) * 1000)).OrderBy(g => g.Item1).ToList();
        double gx = gx0;
        foreach (var (g0, g1) in gaps.Append((gx1, gx1)))
        {
            if (g0 > gx + 1) Hat(SectionMaterial.DatTuNhien, gx, gl - 300, Math.Min(g0, gx1), gl);
            gx = Math.Max(gx, g1);
        }
        if (!showFound) w.Text(Lay.Chu, gx0, gl + 0.4 * w.Th, w.Th * 0.8, "CỐT SÂN " + ElevText(zSite), 0, 0, 0);

        // đường cao độ + ký hiệu
        double xl = uMin * 1000 - 1500, xr = uMax * 1000 + 1500;
        foreach (var l in lv)
        {
            double y = l.Elevation * 1000;
            w.Line(Lay.Truc, xl, y, xr, y, "DASHED");
            w.ElevMark(xl, y, ElevText(l.Elevation), -1);
            w.Text(Lay.Chu, xr + 200, y + 100, w.Th * 0.9, l.Title.ToUpperInvariant(), 0, 0, 0);
        }
        // chuỗi chiều cao tầng + tổng (bên phải)
        var ys = floorLv.Select(z => z * 1000).ToList();
        if (ys.Count >= 2)
        {
            double xd = xr + 2500;
            w.DimChainV(ys, xr, xd);
            w.DimChainV(new List<double> { ys[0], ys[^1] }, xr, xd + 1000);
        }
        // trục cắt qua
        var axes = new List<(double U, string Name)>();
        double tx = (s.X2 - s.X1) / Math.Max(L, 1e-9), ty = (s.Y2 - s.Y1) / Math.Max(L, 1e-9);
        foreach (var g in p.GridsX)
            if (Math.Abs(tx) > 1e-6) { double u = (g.Coord - s.X1) / tx; if (u >= -1e-6 && u <= L + 1e-6) axes.Add((u, g.Name)); }
        foreach (var g in p.GridsY)
            if (Math.Abs(ty) > 1e-6) { double u = (g.Coord - s.Y1) / ty; if (u >= -1e-6 && u <= L + 1e-6) axes.Add((u, g.Name)); }
        axes = axes.OrderBy(a => a.U).ToList();
        double zMin = Math.Min(Math.Min(items.Count > 0 ? items.Min(i => i.Z0) : 0, lv.Count > 0 ? lv.Min(l => l.Elevation) : 0), zSite - 0.3);
        if (found.Count > 0) zMin = Math.Min(zMin, found.Min(f => f.Points.Min(q => q.Z)));
        double zMax = Math.Max(items.Count > 0 ? items.Max(i => i.Z1) : 0, zTop);
        double yb = zMin * 1000;
        double yChain = yb - 1200, yTot = yb - 2000, yBub = yb - 2800 - w.BubbleR;
        foreach (var (u, name) in axes)
        {
            w.Line(Lay.Truc, u * 1000, yBub + w.BubbleR, u * 1000, zMax * 1000 + 600, "CENTER");
            w.GridBubble(u * 1000, yBub, name);
        }
        if (axes.Count >= 2)
        {
            var xs = axes.Select(a => a.U * 1000).ToList();
            w.DimChainH(xs, yChain, yChain);
            if (axes.Count > 2) w.DimChainH(new List<double> { xs[0], xs[^1] }, yTot, yTot);
        }
        w.Title((uMin + uMax) / 2 * 1000, yBub - w.BubbleR - 600, "MẶT CẮT " + s.Name, $"tl: 1/{scale:0}", true);
        // chú thích vật liệu
        if (opt.Materials && opt.Legend && used.Count > 0)
            SectionMaterials.Legend(w, xr + 5500, zMax * 1000 + 600, used);
        return new SectionDrawing
        {
            Line = s, Items = items, Beyond = beyond, Levels = floorLv, Axes = axes, Figure = new Figure(w, "MẶT CẮT " + s.Name),
            Foundations = found, Materials = used.ToList(), ShowFoundation = showFound, SiteLevel = zSite
        };
    }

    /// <summary>Hạng mục khối rãnh tường điện nước (không thể hiện trên mặt cắt kiến trúc).</summary>
    public const string ChaseCategory = "Rãnh tường";

    /// <summary>Lớp hoàn thiện trên sàn: sàn tầng có phòng → vữa lót 20 + gạch lát 10 (WC thêm chống thấm 5);
    /// sàn mái (mức cao nhất) → chống thấm 5 + vữa tạo dốc 40.</summary>
    private static void SlabFinish(Project p, SectionCutItem it, double zTop, DxfWriter w,
        Action<string, double, double, double, double, string> box, Action<SectionMaterial, double, double, double, double> hat)
    {
        double a = it.U0 * 1000, b = it.U1 * 1000, d = it.Z1 * 1000;
        bool roof = Math.Abs(it.Z1 - zTop) < 0.05;
        var story = p.Stories.FirstOrDefault(st => Math.Abs(st.Elevation - it.Z1) < 0.05);
        var rooms = story == null ? new List<Room>() : (p.Arch?.Rooms ?? new List<Room>()).Where(r => r.Story == story.Name).ToList();
        if (roof)
        {
            box(Lay.KyHieu, a, d, b, d + 5, null); hat(SectionMaterial.ChongTham, a, d, b, d + 5);
            box(Lay.KyHieu, a, d + 5, b, d + 45, null); hat(SectionMaterial.VuaTrat, a, d + 5, b, d + 45);
            return;
        }
        if (rooms.Count == 0) return;
        bool wet = rooms.Any(r => { string n = (r.Name ?? "").ToLowerInvariant(); return n.Contains("wc") || n.Contains("vệ sinh") || n.Contains("tắm"); });
        double y = d;
        if (wet) { box(Lay.KyHieu, a, y, b, y + 5, null); hat(SectionMaterial.ChongTham, a, y, b, y + 5); y += 5; }
        box(Lay.KyHieu, a, y, b, y + 20, null); hat(SectionMaterial.VuaTrat, a, y, b, y + 20); y += 20;
        box(Lay.KyHieu, a, y, b, y + 10, null); hat(SectionMaterial.GachMen, a, y, b, y + 10);
    }

    /// <summary>Tường trên mặt cắt: các lớp của kiểu tường (lõi, vữa trát, ốp…) theo bề dày, mỗi lớp tô đúng vật liệu.
    /// Tường cắt gần vuông góc → chia dải theo phương u; tường song song mặt cắt → tô vật liệu lõi.</summary>
    private static void WallLayers(Project p, SectionLine s, SectionCutItem it, DxfWriter w,
        Action<string, double, double, double, double, string> box, Action<SectionMaterial, double, double, double, double> hat, bool materials)
    {
        double a = it.U0 * 1000, b = it.U1 * 1000, c = it.Z0 * 1000, d = it.Z1 * 1000;
        box(Lay.BeTong, a, c, b, d, null);
        if (!materials) return;
        var layers = WallLayersOf(p, it.Id);
        double L = Math.Max(s.Length, 1e-9), tx = (s.X2 - s.X1) / L, ty = (s.Y2 - s.Y1) / L;
        var wl = int.TryParse(it.Id.Replace("TUONG-", ""), out int wid) ? p.Walls.FirstOrDefault(x => x.Id == wid) : null;
        double cos = 1;
        double sign = 1;
        if (wl != null && wl.Chord > 1e-9)
        {
            double wx = (wl.X2 - wl.X1) / wl.Chord, wy = (wl.Y2 - wl.Y1) / wl.Chord;
            cos = Math.Abs(wx * tx + wy * ty);
            double yx = -wy, yy = wx;   // pháp tuyến trái của tường (hướng lớp tăng)
            sign = yx * tx + yy * ty >= 0 ? 1 : -1;
        }
        if (layers.Count == 0 || cos > 0.7)
        {
            var core = layers.FirstOrDefault(l => l.IsCore);
            hat(SectionMaterials.FromName(core?.Material ?? WallMaterials.SolidBrick), a, c, b, d);
            return;
        }
        double tot = layers.Sum(l => Math.Max(0, l.Thickness));
        if (tot < 1e-9) return;
        double span = b - a;
        var seq = sign > 0 ? layers : Enumerable.Reverse(layers).ToList();
        double x = a;
        foreach (var l in seq)
        {
            double wd = span * Math.Max(0, l.Thickness) / tot;
            if (wd < 1e-6) continue;
            box(Lay.KyHieu, x, c, x + wd, d, null);
            hat(SectionMaterials.FromName(l.Function == WallLayerFn.Plaster ? WallMaterials.Mortar : l.Material), x, c, x + wd, d);
            x += wd;
        }
    }

    /// <summary>Các lớp tường (từ mặt y âm → y dương của hệ toạ độ tường): kiểu tường nhiều lớp, hoặc lõi + vữa trát quy đổi.</summary>
    public static List<WallLayer> WallLayersOf(Project p, string solidId)
    {
        if (!int.TryParse((solidId ?? "").Replace("TUONG-", ""), out int wid)) return new List<WallLayer>();
        var wl = p.Walls.FirstOrDefault(x => x.Id == wid);
        var t = wl == null ? null : p.WallTypes.FirstOrDefault(x => x.Name == wl.Type);
        if (t == null) return new List<WallLayer>();
        if (t.IsLayered) return t.Layers.ToList();
        var res = new List<WallLayer>();
        for (int i = 0; i < Math.Min(1, t.PlasterSides); i++) res.Add(new WallLayer { Function = WallLayerFn.Plaster, Material = WallMaterials.Mortar, Thickness = t.PlasterThickness });
        res.Add(new WallLayer { Function = WallLayerFn.Core, Material = WallMaterials.SolidBrick, Thickness = t.Thickness });
        if (t.PlasterSides >= 2) res.Add(new WallLayer { Function = WallLayerFn.Plaster, Material = WallMaterials.Mortar, Thickness = t.PlasterThickness });
        return res;
    }

    /// <summary>Hình móng bị mặt phẳng cắt đi qua (toạ độ u, z – m): móng đơn (bậc / vát + cổ cột), đài cọc + cọc (cắt ngắn),
    /// móng băng (chữ T ngược), bê tông lót. Kích thước: kết quả thiết kế móng (FootingDesign) → móng khai báo (Project.Footings) →
    /// sơ bộ theo cột (ghi chú "sơ bộ").</summary>
    public static List<FoundationShape> FoundationShapes(Project p, SectionLine s, IEnumerable<FootingDesign> designs = null)
    {
        var res = new List<FoundationShape>();
        double L = s.Length;
        if (L < 1e-9) return res;
        double tx = (s.X2 - s.X1) / L, ty = (s.Y2 - s.Y1) / L, nx = -ty, ny = tx;
        var lv = KetCauVXT.Model.Levels.All(p);
        double zBot = lv.FirstOrDefault(l => l.Kind == LevelKind.DayMong)?.Elevation ?? (p.GroundElevation - 1.5);
        var dl = designs?.ToList() ?? new List<FootingDesign>();
        var ft = p.Foundation ?? new FoundationSettings();
        // chân cột: nút thấp nhất của mỗi cột đứng tại cao độ thấp nhất của mô hình
        var cols = p.Frames.Where(f => f.Kind == FrameKind.Column).ToList();
        if (cols.Count == 0) return res;
        var baseNodes = new Dictionary<int, (Node N, FrameElement F)>();
        foreach (var f in cols)
        {
            Node a, b;
            try { a = p.Node(f.NodeI); b = p.Node(f.NodeJ); } catch { continue; }
            var lo = a.Z <= b.Z ? a : b;
            if (!baseNodes.ContainsKey(lo.Id) || baseNodes[lo.Id].N.Z > lo.Z) baseNodes[lo.Id] = (lo, f);
        }
        double zMinNode = baseNodes.Values.Min(v => v.N.Z);
        foreach (var (n, f) in baseNodes.Values.Where(v => v.N.Z < zMinNode + 0.05))
        {
            var fs = p.FrameSections.FirstOrDefault(x => x.Name == f.Section);
            double cb = fs?.B > 0 ? fs.B : 0.22, ch = fs?.H > 0 ? fs.H : 0.22;
            var fd = dl.FirstOrDefault(d => d.NodeId == n.Id);
            var pf = p.Footings.FirstOrDefault(x => x.NodeId == n.Id);
            var type = pf?.Type ?? ft.Type;
            double Lf, Bf, H, Hb;
            string label;
            bool prelim = false;
            if (fd != null && fd.L > 0) { Lf = fd.L; Bf = fd.B; H = fd.H; Hb = fd.Hb > 0 ? fd.Hb : fd.H; label = string.IsNullOrEmpty(fd.Name) ? "M" : fd.Name; }
            else if (pf != null && pf.L > 0) { Lf = pf.L; Bf = pf.B; H = pf.Height > 0 ? pf.Height : 0.45; Hb = Math.Min(H, 0.25); label = pf.Label; }
            else { Lf = Bf = Math.Max(1.2, 4 * Math.Max(cb, ch)); H = 0.45; Hb = 0.25; label = "Móng (sơ bộ)"; prelim = true; }
            if (type == FoundationType.Pile)
            {
                double ps = Math.Max(0.15, ft.PileSize), sp = Math.Max(2.5, ft.PileSpacingFactor) * ps;
                Lf = Bf = Math.Max(Lf, sp + ps + 2 * Math.Max(0.1, ft.CapEdge));
                H = Hb = Math.Max(H, ft.CapMinHeight);
            }
            double sd = (n.X - s.X1) * nx + (n.Y - s.Y1) * ny;
            double halfN = Lf / 2 * Math.Abs(nx) + Bf / 2 * Math.Abs(ny), halfU = Lf / 2 * Math.Abs(tx) + Bf / 2 * Math.Abs(ty);
            if (Math.Abs(sd) > halfN + 1e-6) continue;
            double uc = (n.X - s.X1) * tx + (n.Y - s.Y1) * ty;
            if (uc + halfU < 0 || uc - halfU > L) continue;
            double cu = (cb * Math.Abs(tx) + ch * Math.Abs(ty)) / 2;   // nửa cạnh cột theo u
            double top = zBot + H;
            var body = new FoundationShape { Kind = type == FoundationType.Pile ? "Đài cọc" : type == FoundationType.Strip ? "Móng băng" : "Móng đơn", Material = SectionMaterial.BTCT,
                Label = label + (prelim ? "" : $" {Dt0(Lf)}×{Dt0(Bf)}×{Dt0(H)}"), NodeId = n.Id };
            if (type == FoundationType.Strip)
            {
                // chữ T ngược: bản cánh Hb, sườn (dầm móng) bề rộng cột + 0,1 tới đỉnh móng
                double rb = cu + 0.05;
                body.Points = new() { (uc - halfU, zBot), (uc + halfU, zBot), (uc + halfU, zBot + Hb), (uc + rb, zBot + Hb), (uc + rb, top), (uc - rb, top), (uc - rb, zBot + Hb), (uc - halfU, zBot + Hb) };
            }
            else if (Hb < H - 1e-6)
            {
                double cw = cu + 0.05;   // vát tới mép cổ cột + 50
                body.Points = new() { (uc - halfU, zBot), (uc + halfU, zBot), (uc + halfU, zBot + Hb), (uc + cw, top), (uc - cw, top), (uc - halfU, zBot + Hb) };
            }
            else body.Points = new() { (uc - halfU, zBot), (uc + halfU, zBot), (uc + halfU, top), (uc - halfU, top) };
            res.Add(body);
            // bê tông lót 100, rộng hơn móng 100 mỗi bên
            double lt = 0.10, lo2 = 0.10;
            res.Add(new FoundationShape { Kind = "Bê tông lót", Material = SectionMaterial.BeTongLot, NodeId = n.Id,
                Points = new() { (uc - halfU - lo2, zBot - lt), (uc + halfU + lo2, zBot - lt), (uc + halfU + lo2, zBot), (uc - halfU - lo2, zBot) } });
            // cổ cột: từ đỉnh móng tới chân cột
            if (n.Z > top + 1e-6)
                res.Add(new FoundationShape { Kind = "Cổ cột", Material = SectionMaterial.BTCT, NodeId = n.Id,
                    Points = new() { (uc - cu, top), (uc + cu, top), (uc + cu, n.Z), (uc - cu, n.Z) } });
            if (type == FoundationType.Pile)
            {
                double ps = Math.Max(0.15, ft.PileSize), sp = Math.Max(2.5, ft.PileSpacingFactor) * ps, emb = Math.Max(0, ft.PileEmbed);
                foreach (double off in new[] { -sp / 2, sp / 2 })
                    res.Add(new FoundationShape { Kind = "Cọc", Material = SectionMaterial.BTCT, NodeId = n.Id, BreakAtBottom = true,
                        Points = new() { (uc + off - ps / 2, zBot - 1.5), (uc + off + ps / 2, zBot - 1.5), (uc + off + ps / 2, zBot + emb), (uc + off - ps / 2, zBot + emb) } });
            }
        }
        return res;
    }

    private static string Dt0(double m) => Math.Round(m * 1000).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>Tờ hồ sơ cho mọi mặt cắt. Kiến trúc (prefix "KT"): mặc định KHÔNG thể hiện móng (trừ khi người dùng bật cho mặt cắt đó);
    /// kết cấu (prefix "KC", foundation = true): thể hiện móng theo kết quả thiết kế móng.</summary>
    public static List<DrawingItem> Sheets(Project p, double scale = 100, bool foundation = false, IEnumerable<FootingDesign> footings = null, string prefix = "KT")
    {
        var res = new List<DrawingItem>();
        if (p.Stories.Count == 0) return res;
        foreach (var s in All(p))
        {
            var d = Draw(p, s, scale, null, new SectionOptions { ShowFoundation = foundation ? true : s.ShowFoundation ?? false, Footings = footings });
            if (d.Items.Count == 0) continue;
            string slug = "MatCat_" + s.Name.Replace("-", "_");
            res.Add(new DrawingItem { Key = prefix + ":MC:" + s.Name, FileName = $"{prefix}_{slug}.dxf", Title = d.Title, Scale = scale, W = d.Figure.W });
        }
        return res;
    }
}

/// <summary>Tuỳ chọn dựng mặt cắt.</summary>
public sealed class SectionOptions
{
    /// <summary>null = theo SectionLine.ShowFoundation (null → có móng)</summary>
    public bool? ShowFoundation { get; set; }
    /// <summary>Tô vật liệu theo quy ước (BTCT, gạch, vữa, đất…); false = tô đặc bê tông như cũ</summary>
    public bool Materials { get; set; } = true;
    /// <summary>Bảng chú thích vật liệu bên phải mặt cắt</summary>
    public bool Legend { get; set; } = true;
    /// <summary>Cốt sân thấp hơn nền tầng 1 (m)</summary>
    public double SiteOffset { get; set; } = 0.45;
    /// <summary>Kết quả thiết kế móng (kích thước móng thật); null → móng khai báo / sơ bộ</summary>
    public IEnumerable<FootingDesign> Footings { get; set; }
}

/// <summary>Một hình móng trên mặt cắt (đa giác u, z – m).</summary>
public sealed class FoundationShape
{
    public string Kind { get; set; } = "";
    public SectionMaterial Material { get; set; } = SectionMaterial.BTCT;
    public List<(double U, double Z)> Points { get; set; } = new();
    public string Label { get; set; } = "";
    public int NodeId { get; set; }
    public bool BreakAtBottom { get; set; }
}
