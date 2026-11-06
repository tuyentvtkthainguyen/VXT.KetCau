using System.Globalization;
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
    /// <summary>Cao độ các mức ghi trên mặt cắt (m, thấp → cao)</summary>
    public List<double> Levels { get; init; } = new();
    /// <summary>Vị trí trục cắt qua (u, tên)</summary>
    public List<(double U, string Name)> Axes { get; init; } = new();
    public Figure Figure { get; init; }
    public string Title => "MẶT CẮT " + Line?.Name;
}

public static class SectionView
{
    /// <summary>Mặt cắt tự sinh: 1-1 dọc trục X qua giữa nhịp giữa theo Y, 2-2 dọc trục Y qua giữa nhịp giữa theo X.</summary>
    public static List<SectionLine> Defaults(Project p)
    {
        var (x0, y0, x1, y1) = Extents(p);
        if (x1 - x0 < 1e-6 && y1 - y0 < 1e-6) return new List<SectionLine>();
        double ext = 1.5;
        double ym = MidBay(p.GridsY.Select(g => g.Coord).ToList(), y0, y1);
        double xm = MidBay(p.GridsX.Select(g => g.Coord).ToList(), x0, x1);
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

    public static string ElevText(double z)
    {
        if (Math.Abs(z) < 5e-4) return "±0.000";
        return (z > 0 ? "+" : "-") + Math.Abs(z).ToString("0.000", CultureInfo.InvariantCulture);
    }

    /// <summary>Dựng hình mặt cắt (DXF, mm) với cao độ, chuỗi kích thước tầng, tổng chiều cao, trục và kích thước trục.</summary>
    public static SectionDrawing Draw(Project p, SectionLine s, double scale = 100, IEnumerable<Discipline> disciplines = null)
    {
        var items = Cut(p, s, disciplines);
        var w = new DxfWriter(scale);
        double L = s.Length;
        // cao độ: nền ±0, các sàn, mái (bỏ đáy / mặt móng khỏi chuỗi chiều cao tầng, chỉ ghi ký hiệu)
        var lv = KetCauVXT.Model.Levels.All(p);
        var floorLv = lv.Where(l => l.Kind is LevelKind.Nen or LevelKind.San or LevelKind.Mai or LevelKind.Tum).Select(l => l.Elevation).Distinct().OrderBy(z => z).ToList();
        double uMin = items.Count > 0 ? items.Min(i => i.U0) : 0, uMax = items.Count > 0 ? items.Max(i => i.U1) : L;
        double zMin = Math.Min(items.Count > 0 ? items.Min(i => i.Z0) : 0, lv.Min(l => l.Elevation));
        double zMax = Math.Max(items.Count > 0 ? items.Max(i => i.Z1) : 0, floorLv.Count > 0 ? floorLv.Max() : 0);
        // phần tử cắt
        foreach (var it in items.OrderBy(i => i.Category == "Tường" ? 0 : 1))
        {
            double a = it.U0 * 1000, b = it.U1 * 1000, c = it.Z0 * 1000, d = it.Z1 * 1000;
            var pts = new List<(double X, double Y)> { (a, c), (b, c), (b, d), (a, d) };
            bool conc = it.Category is "Cột" or "Dầm" or "Sàn" or "Sàn nền";
            bool door = it.Category is "Cửa đi" or "Cửa sổ";
            w.Polyline(door ? Lay.KyHieu : Lay.BeTong, pts, true);
            if (conc) w.FilledRect(Lay.BeTong, a, c, b, d);
        }
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
        return new SectionDrawing { Line = s, Items = items, Levels = floorLv, Axes = axes, Figure = new Figure(w, "MẶT CẮT " + s.Name) };
    }

    /// <summary>Tờ hồ sơ kiến trúc cho mọi mặt cắt (Key "KT:MC:1-1").</summary>
    public static List<DrawingItem> Sheets(Project p, double scale = 100)
    {
        var res = new List<DrawingItem>();
        if (p.Stories.Count == 0) return res;
        foreach (var s in All(p))
        {
            var d = Draw(p, s, scale);
            if (d.Items.Count == 0) continue;
            string slug = "MatCat_" + s.Name.Replace("-", "_");
            res.Add(new DrawingItem { Key = "KT:MC:" + s.Name, FileName = $"KT_{slug}.dxf", Title = d.Title, Scale = scale, W = d.Figure.W });
        }
        return res;
    }
}
