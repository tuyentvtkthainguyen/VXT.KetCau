using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using KetCauVXT.Loads;

namespace KetCauVXT.Model;

// =====================================================================
// CHI TIẾT KIẾN TRÚC KIỂU REVIT: mái dốc theo đường bao (Roof by footprint), trần theo phòng (Ceiling), lan can (Railing),
// phào chỉ / gờ trang trí chạy theo mặt tường (Wall sweep), vùng ốp – sơn hoàn thiện trên mặt tường (Split face + Paint),
// sao chép kiến trúc sang tầng khác (Copy to levels), khối lượng hoàn thiện kiến trúc.
// Khối 3D đăng ký vào ModelSolids.Providers (3D, IFC, OBJ/DAE cho SketchUp, va chạm). Lan can xây / sắt / kính
// sinh tải đường lên dầm đỡ (WallLoadGenerator gọi ArchDetailLoads.Apply mỗi lần phân tích).
// Giao diện: App/MainWindow.ArchDetail.cs.
// =====================================================================

public enum RoofShape { Bang, MotMai, HaiMai, BonMai }

public enum RailKind { Inox, Kinh, SatHoaVan, Xay }

/// <summary>Lan can: đường gấp khúc trên mặt sàn tầng.</summary>
public class Railing
{
    public int Id { get; set; }
    public string Story { get; set; } = "";
    public List<double[]> Points { get; set; } = new();
    public double Height { get; set; } = 1.0;
    public RailKind Kind { get; set; } = RailKind.Inox;
    /// <summary>Cao độ chân lan can so với sàn tầng (m) – vd. đặt trên gờ / bậc</summary>
    public double BaseOffset { get; set; }
    /// <summary>Tải ngang tiêu chuẩn tác dụng lên tay vịn (kN/m, hoạt tải): nhà ở 0,3; công trình công cộng 0,8; khán đài, sân vận động 1,5
    /// (TCVN 2737 – tải trọng lên lan can). 0 = không xét.</summary>
    public double HorizontalLoad { get; set; } = 0.3;

    [System.Text.Json.Serialization.JsonIgnore]
    public double Length
    {
        get { double L = 0; for (int i = 1; i < Points.Count; i++) L += Math.Sqrt(Math.Pow(Points[i][0] - Points[i - 1][0], 2) + Math.Pow(Points[i][1] - Points[i - 1][1], 2)); return L; }
    }
}

/// <summary>Phào chỉ / gờ chạy theo một mặt tường ở cao độ Z (tâm) so với sàn tầng.</summary>
public class WallSweep
{
    public int Id { get; set; }
    public int WallId { get; set; }
    /// <summary>+1: mặt bên trái hướng đầu 1 → đầu 2 của tường; −1: mặt bên phải</summary>
    public int Side { get; set; } = 1;
    public double Z { get; set; } = 0.05;
    /// <summary>Độ nhô ra khỏi mặt tường (m)</summary>
    public double Width { get; set; } = 0.015;
    /// <summary>Chiều cao tiết diện (m)</summary>
    public double Height { get; set; } = 0.1;
    public string Name { get; set; } = "Phào chân tường";
}

/// <summary>Vùng ốp / sơn trên một mặt tường: đoạn A0…A1 dọc tường (A1 &lt; 0 = hết tường), cao Z0…Z1 so với sàn tầng.</summary>
public class WallCladding
{
    public int Id { get; set; }
    public int WallId { get; set; }
    public int Side { get; set; } = 1;
    public double A0 { get; set; }
    public double A1 { get; set; } = -1;
    public double Z0 { get; set; }
    public double Z1 { get; set; } = 1.2;
    public string Material { get; set; } = "Ốp đá granite";
    public double Thickness { get; set; } = 0.02;
}

/// <summary>Mẫu phào chỉ có sẵn (kích thước phổ biến nhà ở Việt Nam).</summary>
public sealed record SweepPreset(string Name, double Z, double Width, double Height, string Note);

/// <summary>Mẫu vật liệu ốp / sơn.</summary>
public sealed record CladPreset(string Material, double Z0, double Z1, double Thickness, double[] Color);

public static class ArchDetail
{
    public static readonly SweepPreset[] SweepPresets =
    {
        new("Phào chân tường 100×15", 0.05, 0.015, 0.10, "chân tường trong nhà, chừa ô cửa đi"),
        new("Gờ chỉ ngang tầng 150×100", -0.075, 0.10, 0.15, "băng chỉ mặt tiền tại cao độ sàn (Z âm = dưới mặt sàn)"),
        new("Gờ bậu cửa sổ 60×100", 0.85, 0.10, 0.06, "dưới bậu cửa sổ, chống thấm nước mưa"),
        new("Gờ chắn nước ô văng 50×50", 2.35, 0.05, 0.05, "trên đầu cửa ngoài nhà"),
        new("Phào trần 80×80", 2.86, 0.08, 0.08, "góc tường – trần trong nhà"),
        new("Chỉ đỉnh tường chắn mái 200×150", 0.83, 0.10, 0.15, "đỉnh tường chắn mái (tầng mái)")
    };

    public static readonly CladPreset[] CladPresets =
    {
        new("Ốp đá granite", 0, 1.2, 0.02, new[] { 0.45, 0.42, 0.40 }),
        new("Ốp gạch thẻ", 0, 3.3, 0.015, new[] { 0.72, 0.38, 0.25 }),
        new("Ốp gạch men vệ sinh", 0, 2.7, 0.012, new[] { 0.88, 0.92, 0.95 }),
        new("Ốp gỗ trang trí", 0, 2.7, 0.018, new[] { 0.62, 0.45, 0.28 }),
        new("Ốp đá chân tường ngoài", 0, 0.6, 0.02, new[] { 0.35, 0.35, 0.35 }),
        new("Sơn ngoại thất màu nhấn", 0, 3.3, 0.002, new[] { 0.85, 0.75, 0.55 })
    };

    public static double[] CladColor(string material) =>
        CladPresets.FirstOrDefault(c => c.Material == material)?.Color ?? new[] { 0.7, 0.66, 0.6 };

    private static int NextId<T>(List<T> list, Func<T, int> id) => list.Count == 0 ? 1 : list.Max(id) + 1;

    // ------------------------------------------------------------------
    // Thêm phần tử
    // ------------------------------------------------------------------
    public static Railing AddRailing(Project p, string story, IEnumerable<(double X, double Y)> pts, RailKind kind, double height = 1.0)
    {
        p.Arch ??= new ArchModel();
        var list = pts.Select(q => new[] { Math.Round(q.X, 4), Math.Round(q.Y, 4) }).ToList();
        for (int i = list.Count - 1; i > 0; i--)
            if (Math.Abs(list[i][0] - list[i - 1][0]) + Math.Abs(list[i][1] - list[i - 1][1]) < 1e-4) list.RemoveAt(i);
        if (list.Count < 2) throw new ArgumentException("Lan can cần ít nhất 2 điểm khác nhau.");
        var r = new Railing { Id = NextId(p.Arch.Railings, x => x.Id), Story = story, Points = list, Kind = kind, Height = Math.Max(0.3, height) };
        p.Arch.Railings.Add(r);
        return r;
    }

    /// <summary>Mặt tường gần điểm (x, y): +1 bên trái hướng đầu 1 → 2, −1 bên phải.</summary>
    public static int SideOf(WallLine w, double x, double y) =>
        (w.X2 - w.X1) * (y - w.Y1) - (w.Y2 - w.Y1) * (x - w.X1) >= 0 ? 1 : -1;

    public static WallSweep AddSweep(Project p, WallLine w, int side, SweepPreset pre)
    {
        p.Arch ??= new ArchModel();
        var s = new WallSweep { Id = NextId(p.Arch.Sweeps, x => x.Id), WallId = w.Id, Side = side >= 0 ? 1 : -1, Z = pre.Z, Width = pre.Width, Height = pre.Height, Name = pre.Name };
        p.Arch.Sweeps.Add(s);
        return s;
    }

    public static WallCladding AddCladding(Project p, WallLine w, int side, CladPreset pre, double z0 = double.NaN, double z1 = double.NaN)
    {
        p.Arch ??= new ArchModel();
        var c = new WallCladding
        {
            Id = NextId(p.Arch.Claddings, x => x.Id), WallId = w.Id, Side = side >= 0 ? 1 : -1, Material = pre.Material, Thickness = pre.Thickness,
            Z0 = double.IsNaN(z0) ? pre.Z0 : z0, Z1 = double.IsNaN(z1) ? pre.Z1 : z1
        };
        p.Arch.Claddings.Add(c);
        return c;
    }

    /// <summary>Mặt ngoài nhà của tường: phía xa tâm mặt bằng tầng (tâm hộp bao các tường cùng tầng).</summary>
    public static int OutsideSide(Project p, WallLine w)
    {
        var ws = p.Walls.Where(q => q.Story == w.Story).ToList();
        double cx = ws.SelectMany(q => new[] { q.X1, q.X2 }).DefaultIfEmpty(0).Average();
        double cy = ws.SelectMany(q => new[] { q.Y1, q.Y2 }).DefaultIfEmpty(0).Average();
        return -SideOf(w, cx, cy);
    }

    /// <summary>Tường biên (mặt ngoài) của tầng: tường nằm sát hộp bao các tường của tầng (≤ 0,4 m), hoặc kiểu "Tường ngoài".</summary>
    public static List<WallLine> PerimeterWalls(Project p, string story)
    {
        var ws = p.Walls.Where(q => q.Story == story && !q.IsCurved && q.Chord > 0.3).ToList();
        if (ws.Count == 0) return ws;
        double x0 = ws.Min(q => Math.Min(q.X1, q.X2)), x1 = ws.Max(q => Math.Max(q.X1, q.X2));
        double y0 = ws.Min(q => Math.Min(q.Y1, q.Y2)), y1 = ws.Max(q => Math.Max(q.Y1, q.Y2));
        const double tol = 0.4;
        bool OnEdge(WallLine q) =>
            (Math.Abs(q.X1 - q.X2) < 0.05 && (Math.Abs(q.X1 - x0) < tol || Math.Abs(q.X1 - x1) < tol)) ||
            (Math.Abs(q.Y1 - q.Y2) < 0.05 && (Math.Abs(q.Y1 - y0) < tol || Math.Abs(q.Y1 - y1) < tol));
        return ws.Where(q => OnEdge(q) || p.WallTypes?.FirstOrDefault(t => t.Name == q.Type)?.Function == WallFunctionName.Exterior).ToList();
    }

    /// <summary>Trần theo phòng của tầng (bỏ phòng đã có trần trùng tâm). Trả số trần mới.</summary>
    public static int CeilingsFromRooms(Project p, string story, double height, string type)
    {
        p.Arch ??= new ArchModel();
        int n = 0;
        foreach (var r in p.Arch.Rooms.Where(r => r.Story == story && r.Outline.Count >= 3))
        {
            var c = r.Center;
            if (p.Arch.Ceilings.Any(q => q.Story == story && q.Outline.Count >= 3 && Math.Abs(Geo2.Centroid(q.Outline).X - c.X) < 0.05 && Math.Abs(Geo2.Centroid(q.Outline).Y - c.Y) < 0.05)) continue;
            p.Arch.Ceilings.Add(new Ceiling
            {
                Id = NextId(p.Arch.Ceilings, x => x.Id), Story = story, Height = height, Type = type,
                Outline = r.Outline.Select(q => new[] { q[0], q[1] }).ToList()
            });
            n++;
        }
        return n;
    }

    /// <summary>Đường bao mặc định cho mái của tầng: hộp bao ô sàn / tường / nút của tầng (m).</summary>
    public static (double X0, double Y0, double X1, double Y1)? Footprint(Project p, string story)
    {
        var xs = new List<double>(); var ys = new List<double>();
        foreach (var s in p.Slabs.Where(s => s.Story == story))
            foreach (var id in s.Nodes) { var n = p.Node(id); if (n != null) { xs.Add(n.X); ys.Add(n.Y); } }
        if (xs.Count == 0)
            foreach (var w in p.Walls.Where(w => w.Story == story)) { xs.Add(w.X1); xs.Add(w.X2); ys.Add(w.Y1); ys.Add(w.Y2); }
        if (xs.Count == 0)
        {
            double z = ModelSolids.StoryZ(p, story);
            foreach (var n in p.Nodes.Where(n => Math.Abs(n.Z - z) < 0.01)) { xs.Add(n.X); ys.Add(n.Y); }
        }
        if (xs.Count == 0) return null;
        return (xs.Min(), ys.Min(), xs.Max(), ys.Max());
    }

    public static RoofElement AddRoof(Project p, string story, double x0, double y0, double x1, double y1, RoofShape shape, double slopePercent, double overhang, string type, double eave = 0)
    {
        if (x1 - x0 < 0.5 || y1 - y0 < 0.5) throw new ArgumentException("Đường bao mái quá nhỏ.");
        return AddRoof(p, story, new List<(double X, double Y)> { (x0, y0), (x1, y0), (x1, y1), (x0, y1) }, shape, slopePercent, overhang, type, eave);
    }

    /// <summary>Đường bao các ô sàn của tầng (hợp các ô, lưới 5 cm) – nhà chữ L, T, U… cho mái theo đường bao sàn.</summary>
    public static List<(double X, double Y)> FootprintOutline(Project p, string story)
    {
        var polys = p.Slabs.Where(s => s.Story == story)
            .Select(s => (IReadOnlyList<(double X, double Y)>)s.Nodes.Select(id => p.Node(id)).Where(n => n != null).Select(n => (n.X, n.Y)).ToList()).ToList();
        return RasterOutline.UnionOutline(polys, 0.05);
    }

    /// <summary>Mái theo đa giác đường bao (chữ nhật hoặc trực giao chữ L, T, U…).</summary>
    public static RoofElement AddRoof(Project p, string story, IReadOnlyList<(double X, double Y)> outline, RoofShape shape, double slopePercent, double overhang, string type, double eave = 0)
    {
        p.Arch ??= new ArchModel();
        if (outline == null || outline.Count < 3 || Math.Abs(RasterOutline.SignedArea(outline)) < 0.25) throw new ArgumentException("Đường bao mái quá nhỏ.");
        var pts = outline.Select(q => new[] { Math.Round(q.X, 4), Math.Round(q.Y, 4) }).ToList();
        if (RasterOutline.SignedArea(outline) < 0) pts.Reverse();
        var r = new RoofElement
        {
            Id = NextId(p.Arch.Roofs, x => x.Id), Story = story, Type = type, Shape = shape, SlopePercent = Math.Max(0, slopePercent),
            Overhang = Math.Max(0, overhang), EaveHeight = eave, Outline = pts
        };
        r.RidgeHeight = Math.Round(RoofGeometry.Rise(r), 3);
        p.Arch.Roofs.Add(r);
        return r;
    }

    // ------------------------------------------------------------------
    // Sao chép kiến trúc sang tầng khác (Copy to levels)
    // ------------------------------------------------------------------
    public sealed class CopyOptions
    {
        public bool Walls { get; set; } = true;      // kèm cửa, phào chỉ, ốp của tường
        public bool Rooms { get; set; } = true;
        public bool Ceilings { get; set; } = true;
        public bool Railings { get; set; } = true;
        public bool Furniture { get; set; } = true;
    }

    private static T Clone<T>(T o) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(o, Project.JsonOptions), Project.JsonOptions);

    /// <summary>Chép phần kiến trúc của tầng src sang các tầng đích (bỏ tường trùng vị trí đã có). Trả về tổng số phần tử đã chép.</summary>
    public static int CopyToStories(Project p, string src, IEnumerable<string> targets, CopyOptions o = null)
    {
        o ??= new CopyOptions();
        p.Arch ??= new ArchModel();
        int n = 0;
        foreach (var dst in targets.Where(t => t != src && p.Stories.Any(s => s.Name == t)).Distinct())
        {
            if (o.Walls)
                foreach (var w in p.Walls.Where(w => w.Story == src).ToList())
                {
                    if (p.Walls.Any(q => q.Story == dst && ((Near(q.X1, w.X1) && Near(q.Y1, w.Y1) && Near(q.X2, w.X2) && Near(q.Y2, w.Y2)) || (Near(q.X1, w.X2) && Near(q.Y1, w.Y2) && Near(q.X2, w.X1) && Near(q.Y2, w.Y1))))) continue;
                    var c = Clone(w);
                    c.Id = p.Walls.Select(q => q.Id).DefaultIfEmpty(0).Max() + 1;
                    c.Story = dst; c.ExternalId = null;
                    p.Walls.Add(c); n++;
                    foreach (var s in p.Arch.Sweeps.Where(s => s.WallId == w.Id).ToList())
                    { var cs = Clone(s); cs.Id = NextId(p.Arch.Sweeps, x => x.Id); cs.WallId = c.Id; p.Arch.Sweeps.Add(cs); n++; }
                    foreach (var s in p.Arch.Claddings.Where(s => s.WallId == w.Id).ToList())
                    { var cs = Clone(s); cs.Id = NextId(p.Arch.Claddings, x => x.Id); cs.WallId = c.Id; p.Arch.Claddings.Add(cs); n++; }
                }
            if (o.Rooms)
                foreach (var r in p.Arch.Rooms.Where(r => r.Story == src).ToList())
                {
                    if (p.Arch.Rooms.Any(q => q.Story == dst && q.Outline.Count >= 3 && Near(q.Center.X, r.Center.X) && Near(q.Center.Y, r.Center.Y))) continue;
                    var c = Clone(r); c.Story = dst;
                    TrySetId(c, p.Arch.Rooms);
                    p.Arch.Rooms.Add(c); n++;
                }
            if (o.Ceilings)
                foreach (var r in p.Arch.Ceilings.Where(r => r.Story == src).ToList())
                { var c = Clone(r); c.Story = dst; c.Id = NextId(p.Arch.Ceilings, x => x.Id); p.Arch.Ceilings.Add(c); n++; }
            if (o.Railings)
                foreach (var r in p.Arch.Railings.Where(r => r.Story == src).ToList())
                { var c = Clone(r); c.Story = dst; c.Id = NextId(p.Arch.Railings, x => x.Id); p.Arch.Railings.Add(c); n++; }
            if (o.Furniture)
                foreach (var r in p.Arch.Furniture.Where(r => r.Story == src).ToList())
                { var c = Clone(r); c.Story = dst; c.ExternalId = null; c.Id = NextId(p.Arch.Furniture, x => x.Id); p.Arch.Furniture.Add(c); n++; }
        }
        return n;
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.01;

    private static void TrySetId<T>(T item, List<T> list)
    {
        var pi = typeof(T).GetProperty("Id");
        if (pi == null || pi.PropertyType != typeof(int)) return;
        int max = list.Select(x => (int)pi.GetValue(x)).DefaultIfEmpty(0).Max();
        pi.SetValue(item, max + 1);
    }

    // ------------------------------------------------------------------
    // Hình học mặt tường: các hình chữ nhật (A, Z) của vùng trừ ô cửa
    // ------------------------------------------------------------------
    /// <summary>Ô cửa của tường trong toạ độ mặt tường: (a0, a1, z0, z1) – a dọc tường từ đầu 1, z so với sàn tầng.</summary>
    public static List<(double A0, double A1, double Z0, double Z1)> OpeningRects(Project p, WallLine w)
    {
        var res = new List<(double, double, double, double)>();
        foreach (var o in w.Openings ?? new List<WallOpening>())
        {
            var t = WallAssembly.TypeOf(p, o);
            if (t == null) continue;
            res.Add((o.Offset - t.Width / 2, o.Offset + t.Width / 2, t.Sill, t.Sill + t.Height));
        }
        return res;
    }

    /// <summary>Chia hình chữ nhật [a0,a1]×[z0,z1] thành các hình chữ nhật không chồng lên ô cửa.</summary>
    public static List<(double A0, double A1, double Z0, double Z1)> Subtract(double a0, double a1, double z0, double z1, List<(double A0, double A1, double Z0, double Z1)> holes)
    {
        var res = new List<(double, double, double, double)>();
        if (a1 - a0 < 1e-6 || z1 - z0 < 1e-6) return res;
        var cuts = new SortedSet<double> { a0, a1 };
        foreach (var h in holes) { if (h.A0 > a0 && h.A0 < a1) cuts.Add(h.A0); if (h.A1 > a0 && h.A1 < a1) cuts.Add(h.A1); }
        var xs = cuts.ToList();
        for (int i = 0; i + 1 < xs.Count; i++)
        {
            double xa = xs[i], xb = xs[i + 1], xm = (xa + xb) / 2;
            var zs = holes.Where(h => h.A0 < xm && h.A1 > xm && h.Z1 > z0 && h.Z0 < z1).Select(h => (Math.Max(z0, h.Z0), Math.Min(z1, h.Z1))).OrderBy(q => q.Item1).ToList();
            double cur = z0;
            foreach (var (h0, h1) in zs)
            {
                if (h0 - cur > 1e-6) res.Add((xa, xb, cur, h0));
                cur = Math.Max(cur, h1);
            }
            if (z1 - cur > 1e-6) res.Add((xa, xb, cur, z1));
        }
        // gộp các mảnh liền nhau cùng cao độ
        var merged = new List<(double A0, double A1, double Z0, double Z1)>();
        foreach (var r in res.OrderBy(q => q.Item3).ThenBy(q => q.Item4).ThenBy(q => q.Item1))
        {
            int k = merged.FindIndex(m => Math.Abs(m.Z0 - r.Item3) < 1e-6 && Math.Abs(m.Z1 - r.Item4) < 1e-6 && Math.Abs(m.A1 - r.Item1) < 1e-6);
            if (k >= 0) merged[k] = (merged[k].A0, r.Item2, r.Item3, r.Item4);
            else merged.Add(r);
        }
        return merged;
    }

    public static WallLine WallOf(Project p, int id) => p.Walls.FirstOrDefault(w => w.Id == id);

    /// <summary>Các đoạn phào (a0, a1) theo tường, cắt bỏ ô cửa giao với dải cao độ phào.</summary>
    public static List<(double A0, double A1)> SweepPieces(Project p, WallSweep s)
    {
        var w = WallOf(p, s.WallId);
        if (w == null || w.IsCurved) return new();
        double z0 = s.Z - s.Height / 2, z1 = s.Z + s.Height / 2;
        return Subtract(0, w.Chord, z0, z1, OpeningRects(p, w))
            .Where(r => Math.Abs(r.Z0 - z0) < 1e-6 && Math.Abs(r.Z1 - z1) < 1e-6).Select(r => (r.A0, r.A1)).ToList();
    }

    public static double SweepLength(Project p, WallSweep s) => SweepPieces(p, s).Sum(q => q.A1 - q.A0);

    public static List<(double A0, double A1, double Z0, double Z1)> CladdingRects(Project p, WallCladding c)
    {
        var w = WallOf(p, c.WallId);
        if (w == null || w.IsCurved) return new();
        double a1 = c.A1 < 0 ? w.Chord : Math.Min(c.A1, w.Chord);
        return Subtract(Math.Max(0, c.A0), a1, c.Z0, c.Z1, OpeningRects(p, w));
    }

    public static double CladdingArea(Project p, WallCladding c) => CladdingRects(p, c).Sum(r => (r.A1 - r.A0) * (r.Z1 - r.Z0));

    // ------------------------------------------------------------------
    // Khối lượng hoàn thiện kiến trúc
    // ------------------------------------------------------------------
    public sealed record QtyRow(string Group, string Story, string Item, string Unit, double Qty);

    public static List<QtyRow> Quantities(Project p)
    {
        var rows = new List<QtyRow>();
        var a = p.Arch ?? new ArchModel();
        foreach (var g in a.Roofs.GroupBy(r => (r.Story, r.Type)))
        {
            rows.Add(new QtyRow("Mái", g.Key.Story, g.Key.Type + " (diện tích nghiêng)", "m²", Math.Round(g.Sum(RoofGeometry.SlopedArea), 2)));
            double gab = g.Sum(RoofGeometry.GableArea);
            if (gab > 0.01) rows.Add(new QtyRow("Mái", g.Key.Story, "Tường thu hồi xây 220", "m²", Math.Round(gab, 2)));
        }
        foreach (var g in a.Ceilings.GroupBy(c => (c.Story, c.Type)))
            rows.Add(new QtyRow("Trần", g.Key.Story, g.Key.Type, "m²", Math.Round(g.Sum(c => Geo2.Area(c.Outline)), 2)));
        foreach (var g in a.Railings.GroupBy(r => (r.Story, r.Kind, Math.Round(r.Height, 2))))
            rows.Add(new QtyRow("Lan can", g.Key.Story, $"{RailName(g.Key.Kind)} cao {g.Key.Item3:0.00} m", "md", Math.Round(g.Sum(r => r.Length), 2)));
        foreach (var g in a.Sweeps.Where(s => WallOf(p, s.WallId) != null).GroupBy(s => (WallOf(p, s.WallId).Story, s.Name)))
            rows.Add(new QtyRow("Phào chỉ", g.Key.Story, g.Key.Name, "md", Math.Round(g.Sum(s => SweepLength(p, s)), 2)));
        foreach (var g in a.Claddings.Where(c => WallOf(p, c.WallId) != null).GroupBy(c => (WallOf(p, c.WallId).Story, c.Material)))
            rows.Add(new QtyRow("Ốp / sơn", g.Key.Story, g.Key.Material, "m²", Math.Round(g.Sum(c => CladdingArea(p, c)), 2)));
        var order = p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name).ToList();
        return rows.OrderBy(r => r.Group).ThenBy(r => order.IndexOf(r.Story)).ThenBy(r => r.Item).ToList();
    }

    public static string QuantitiesCsv(Project p)
    {
        var sb = new StringBuilder("Nhóm;Tầng;Hạng mục;Đơn vị;Khối lượng\n");
        foreach (var r in Quantities(p)) sb.Append($"{r.Group};{r.Story};{r.Item};{r.Unit};{r.Qty.ToString("0.##", CultureInfo.InvariantCulture)}\n");
        return sb.ToString();
    }

    public static string RailName(RailKind k) => k switch
    {
        RailKind.Inox => "Lan can inox tay vịn gỗ",
        RailKind.Kinh => "Lan can kính cường lực",
        RailKind.SatHoaVan => "Lan can sắt hoa văn",
        _ => "Lan can xây gạch 110"
    };

    /// <summary>Tải đường thẳng đứng của lan can (kN/m, tiêu chuẩn): xây 110 trát 2 mặt ≈ (18·0,11 + 2·0,015·18)·H.</summary>
    public static double RailLoad(Railing r) => r.Kind switch
    {
        RailKind.Inox => 0.3,
        RailKind.Kinh => 0.5,
        RailKind.SatHoaVan => 0.4,
        _ => Math.Round((18 * 0.11 + 2 * 0.015 * 18) * r.Height, 3)
    };
}

/// <summary>Hình học mái dốc. Đường bao chữ nhật: mái 1 dốc, 2 dốc (tường thu hồi), 4 dốc (vuông → mái chóp).
/// Đường bao trực giao chữ L, T, U…: chia thành các hình chữ nhật, mỗi khối một mái cùng độ dốc; khối hẹp hơn (mái cánh) kéo dài vào
/// khối rộng hơn tới đường nóc của khối đó để hai mái giao nhau như mái thật (phần khuất nằm dưới mặt mái chính).</summary>
public static class RoofGeometry
{
    public sealed record Block(double X0, double Y0, double X1, double Y1, bool ExtW, bool ExtE, bool ExtS, bool ExtN);

    private static List<(double X, double Y)> Pts(RoofElement r) => r.Outline.Select(q => (q[0], q[1])).ToList();

    /// <summary>Các khối chữ nhật của mái (đã cộng độ vươn ở cạnh ngoài, đã kéo dài mái cánh). Ext* = cạnh đó là cạnh ngoài nhà (có diềm).</summary>
    public static List<Block> Blocks(RoofElement r)
    {
        var res = new List<Block>();
        if (r.Outline.Count < 3) return res;
        var pts = Pts(r);
        List<(double X0, double Y0, double X1, double Y1)> rects;
        if (pts.Count > 4 && RasterOutline.IsOrthogonal(pts)) rects = RasterOutline.Rectangles(pts);
        else rects = new() { (pts.Min(q => q.X), pts.Min(q => q.Y), pts.Max(q => q.X), pts.Max(q => q.Y)) };
        if (rects.Count == 0) return res;
        bool Shared(int i, char side)
        {
            var a = rects[i];
            for (int j = 0; j < rects.Count; j++)
            {
                if (j == i) continue;
                var b = rects[j];
                double oy = Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0), ox = Math.Min(a.X1, b.X1) - Math.Max(a.X0, b.X0);
                if (side == 'W' && Math.Abs(a.X0 - b.X1) < 1e-6 && oy > 1e-6) return true;
                if (side == 'E' && Math.Abs(a.X1 - b.X0) < 1e-6 && oy > 1e-6) return true;
                if (side == 'S' && Math.Abs(a.Y0 - b.Y1) < 1e-6 && ox > 1e-6) return true;
                if (side == 'N' && Math.Abs(a.Y1 - b.Y0) < 1e-6 && ox > 1e-6) return true;
            }
            return false;
        }
        double ov = r.Overhang;
        for (int i = 0; i < rects.Count; i++)
        {
            var q = rects[i];
            bool w = !Shared(i, 'W'), e = !Shared(i, 'E'), so = !Shared(i, 'S'), n = !Shared(i, 'N');
            double x0 = q.X0 - (w ? ov : 0), x1 = q.X1 + (e ? ov : 0), y0 = q.Y0 - (so ? ov : 0), y1 = q.Y1 + (n ? ov : 0);
            // mái cánh: kéo dài vào khối kề rộng hơn tới đường nóc khối đó
            double sh = Math.Min(q.X1 - q.X0, q.Y1 - q.Y0);
            for (int j = 0; j < rects.Count; j++)
            {
                if (j == i) continue;
                var b = rects[j];
                double shb = Math.Min(b.X1 - b.X0, b.Y1 - b.Y0);
                double ai = (q.X1 - q.X0) * (q.Y1 - q.Y0), aj = (b.X1 - b.X0) * (b.Y1 - b.Y0);
                // khối hẹp hơn kéo vào khối rộng hơn; bằng nhau → khối nhỏ hơn (diện tích) kéo vào khối lớn
                bool into = shb > sh + 1e-6 || (Math.Abs(shb - sh) <= 1e-6 && (aj > ai + 1e-6 || (Math.Abs(aj - ai) <= 1e-6 && j < i)));
                if (!into) continue;
                double oy = Math.Min(q.Y1, b.Y1) - Math.Max(q.Y0, b.Y0), ox = Math.Min(q.X1, b.X1) - Math.Max(q.X0, b.X0);
                if (Math.Abs(q.X1 - b.X0) < 1e-6 && oy > 1e-6) x1 = Math.Max(x1, b.X0 + (b.X1 - b.X0) / 2);
                if (Math.Abs(q.X0 - b.X1) < 1e-6 && oy > 1e-6) x0 = Math.Min(x0, b.X1 - (b.X1 - b.X0) / 2);
                if (Math.Abs(q.Y1 - b.Y0) < 1e-6 && ox > 1e-6) y1 = Math.Max(y1, b.Y0 + (b.Y1 - b.Y0) / 2);
                if (Math.Abs(q.Y0 - b.Y1) < 1e-6 && ox > 1e-6) y0 = Math.Min(y0, b.Y1 - (b.Y1 - b.Y0) / 2);
            }
            res.Add(new Block(x0, y0, x1, y1, w, e, so, n));
        }
        return res;
    }

    /// <summary>Chiều cao đỉnh mái cao nhất trên diềm (m).</summary>
    public static double Rise(RoofElement r)
    {
        double s = r.SlopePercent / 100.0, best = 0;
        foreach (var b in Blocks(r))
        {
            double sh = Math.Min(b.X1 - b.X0, b.Y1 - b.Y0);
            best = Math.Max(best, r.Shape switch { RoofShape.MotMai => s * sh, RoofShape.HaiMai => s * sh / 2, RoofShape.BonMai => s * sh / 2, _ => 0 });
        }
        return best;
    }

    /// <summary>Các mặt mái (đa giác 3D, ngược chiều kim đồng hồ nhìn từ trên) và tam giác tường thu hồi (mái 2 dốc, ở cạnh ngoài nhà).</summary>
    public static (List<List<Vec3>> Faces, List<List<Vec3>> Gables) Build(RoofElement r, double storyZ)
    {
        var faces = new List<List<Vec3>>(); var gables = new List<List<Vec3>>();
        foreach (var b in Blocks(r)) BuildBlock(r, b, storyZ, faces, gables);
        return (faces, gables);
    }

    private static void BuildBlock(RoofElement r, Block bk, double storyZ, List<List<Vec3>> faces, List<List<Vec3>> gables)
    {
        double x0 = bk.X0, y0 = bk.Y0, x1 = bk.X1, y1 = bk.Y1;
        bool alongX = x1 - x0 >= y1 - y0;
        // hệ địa phương: u dọc cạnh dài, v ngang (cạnh ngắn)
        double U0 = alongX ? x0 : y0, U1 = alongX ? x1 : y1, V0 = alongX ? y0 : x0, V1 = alongX ? y1 : x1;
        bool extU0 = alongX ? bk.ExtW : bk.ExtS, extU1 = alongX ? bk.ExtE : bk.ExtN;
        double s = r.SlopePercent / 100.0;
        double ze = storyZ + r.EaveHeight, vm = (V0 + V1) / 2, hv = (V1 - V0) / 2;
        double h = r.Shape switch { RoofShape.MotMai => s * (V1 - V0), RoofShape.HaiMai or RoofShape.BonMai => s * hv, _ => 0 };
        Vec3 P(double u, double v, double z) => alongX ? new Vec3(u, v, z) : new Vec3(v, u, z);
        List<Vec3> Poly(params Vec3[] pts)
        {
            var l = pts.ToList();
            double a = 0; for (int i = 0; i < l.Count; i++) { var p = l[i]; var q = l[(i + 1) % l.Count]; a += p.X * q.Y - q.X * p.Y; }
            if (a < 0) l.Reverse();
            return l;
        }
        switch (r.Shape)
        {
            case RoofShape.Bang:
                faces.Add(Poly(P(U0, V0, ze), P(U1, V0, ze), P(U1, V1, ze), P(U0, V1, ze)));
                break;
            case RoofShape.MotMai:
                faces.Add(Poly(P(U0, V0, ze), P(U1, V0, ze), P(U1, V1, ze + h), P(U0, V1, ze + h)));
                break;
            case RoofShape.HaiMai:
                faces.Add(Poly(P(U0, V0, ze), P(U1, V0, ze), P(U1, vm, ze + h), P(U0, vm, ze + h)));
                faces.Add(Poly(P(U0, vm, ze + h), P(U1, vm, ze + h), P(U1, V1, ze), P(U0, V1, ze)));
                // tường thu hồi ở đầu hồi ngoài nhà (lùi vào bằng độ vươn)
                double ov = r.Overhang, vA = V0 + ov, vB = V1 - ov;
                double hw = s * (vB - vA) / 2;
                if (hw > 0.05)
                {
                    if (extU0) gables.Add(new List<Vec3> { P(U0 + ov, vA, ze), P(U0 + ov, vB, ze), P(U0 + ov, (vA + vB) / 2, ze + hw) });
                    if (extU1) gables.Add(new List<Vec3> { P(U1 - ov, vA, ze), P(U1 - ov, vB, ze), P(U1 - ov, (vA + vB) / 2, ze + hw) });
                }
                break;
            case RoofShape.BonMai:
                double d = Math.Min(hv, (U1 - U0) / 2);
                if (!extU0 || !extU1)
                {   // đầu khối nằm trong mái khác: nóc chạy thẳng tới đầu khối (mái cánh giao mái chính), chỉ đầu ngoài nhà mới có mặt hông
                    var ra0 = P(extU0 ? U0 + d : U0, vm, ze + h); var rb0 = P(extU1 ? U1 - d : U1, vm, ze + h);
                    faces.Add(Poly(P(U0, V0, ze), P(U1, V0, ze), rb0, ra0));
                    faces.Add(Poly(P(U1, V1, ze), P(U0, V1, ze), ra0, rb0));
                    if (extU1) faces.Add(Poly(P(U1, V0, ze), P(U1, V1, ze), rb0));
                    if (extU0) faces.Add(Poly(P(U0, V1, ze), P(U0, V0, ze), ra0));
                    break;
                }
                if (U1 - U0 - 2 * d < 1e-6)
                {   // mái chóp (mặt bằng vuông)
                    var top = P((U0 + U1) / 2, vm, ze + h);
                    faces.Add(Poly(P(U0, V0, ze), P(U1, V0, ze), top));
                    faces.Add(Poly(P(U1, V0, ze), P(U1, V1, ze), top));
                    faces.Add(Poly(P(U1, V1, ze), P(U0, V1, ze), top));
                    faces.Add(Poly(P(U0, V1, ze), P(U0, V0, ze), top));
                }
                else
                {
                    var ra = P(U0 + d, vm, ze + h); var rb = P(U1 - d, vm, ze + h);
                    faces.Add(Poly(P(U0, V0, ze), P(U1, V0, ze), rb, ra));
                    faces.Add(Poly(P(U1, V1, ze), P(U0, V1, ze), ra, rb));
                    faces.Add(Poly(P(U1, V0, ze), P(U1, V1, ze), rb));
                    faces.Add(Poly(P(U0, V1, ze), P(U0, V0, ze), ra));
                }
                break;
        }
    }

    /// <summary>Diện tích mặt bằng mái (kể độ vươn, không kể phần mái cánh kéo vào trong mái chính).</summary>
    public static double PlanArea(RoofElement r)
    {
        if (r.Outline.Count < 3) return 0;
        var pts = Pts(r);
        double ov = r.Overhang;
        if (!(pts.Count > 4 && RasterOutline.IsOrthogonal(pts)))
            return (pts.Max(q => q.X) - pts.Min(q => q.X) + 2 * ov) * (pts.Max(q => q.Y) - pts.Min(q => q.Y) + 2 * ov);
        // đa giác trực giao: diện tích + độ vươn dọc chu vi + góc
        double A = Math.Abs(RasterOutline.SignedArea(pts)), per = 0;
        for (int i = 0; i < pts.Count; i++) per += Math.Abs(pts[i].X - pts[(i + 1) % pts.Count].X) + Math.Abs(pts[i].Y - pts[(i + 1) % pts.Count].Y);
        int n = pts.Count, convex = (n + 4) / 2, reflex = n - convex;
        return A + per * ov + (convex - reflex) * ov * ov;
    }

    /// <summary>Diện tích nghiêng: các mặt cùng độ dốc → S bằng / cos α (mái bằng: S bằng).</summary>
    public static double SlopedArea(RoofElement r)
    {
        double s = r.Shape == RoofShape.Bang ? 0 : r.SlopePercent / 100.0;
        return PlanArea(r) * Math.Sqrt(1 + s * s);
    }

    public static double GableArea(RoofElement r) => Build(r, 0).Gables.Sum(Area3D);

    public static double Area3D(IReadOnlyList<Vec3> pts)
    {
        var n = new Vec3(0, 0, 0);
        for (int i = 0; i < pts.Count; i++) n = n + pts[i].Cross(pts[(i + 1) % pts.Count]);
        return n.Length / 2;
    }


    /// <summary>Khối tấm phẳng dày t trên mặt đa giác 3D (kéo theo pháp tuyến hướng lên / theo dir).</summary>
    public static Solid PlateSolid(IReadOnlyList<Vec3> poly, double t, Vec3? extrude = null)
    {
        var p0 = poly[0];
        var x = (poly[1] - p0).Normalize();
        var nrm = new Vec3(0, 0, 0);
        for (int i = 0; i < poly.Count; i++) nrm = nrm + poly[i].Cross(poly[(i + 1) % poly.Count]);
        nrm = nrm.Normalize();
        if (extrude is { } e) { if (nrm.Dot(e) < 0) nrm = nrm * -1; }
        else if (nrm.Z < 0) nrm = nrm * -1;
        var s = new Solid { Origin = p0, XDir = x, ZDir = nrm, Depth = t };
        var y = s.YDir;
        var prof = poly.Select(q => { var d = q - p0; return new[] { d.Dot(x), d.Dot(y) }; }).ToList();
        if (Geo2.SignedArea(prof) < 0) prof.Reverse();
        s.Profile = prof;
        return s;
    }
}

/// <summary>Khối 3D chi tiết kiến trúc (đăng ký vào ModelSolids.Providers khi nạp thư viện).</summary>
public static class ArchDetailSolids
{
    private static bool _done;

    [ModuleInitializer]
    internal static void Init() => Register();

    public static void Register()
    {
        if (_done) return;
        _done = true;
        ModelSolids.Providers.Add(Solids);
    }

    private static readonly double[] ColRoof = { 0.6, 0.25, 0.2 }, ColGable = { 0.93, 0.86, 0.74 }, ColRail = { 0.75, 0.77, 0.8 },
        ColGlass = { 0.6, 0.8, 0.9 }, ColSweep = { 0.96, 0.95, 0.9 };

    public static IEnumerable<Solid> Solids(Project p)
    {
        var a = p.Arch;
        if (a == null) yield break;
        // ---- mái dốc (mái Bằng vẫn do ModelSolids vẽ như trước) ----
        foreach (var r in a.Roofs.Where(r => r.Shape != RoofShape.Bang && r.Outline.Count >= 3))
        {
            var (faces, gables) = RoofGeometry.Build(r, ModelSolids.StoryZ(p, r.Story));
            int k = 0;
            foreach (var f in faces)
            {
                var s = RoofGeometry.PlateSolid(f, Math.Max(0.01, r.Thickness));
                s.Id = $"MAID-{r.Id}-{++k}"; s.Name = r.Type; s.Category = "Mái"; s.IfcType = "IfcRoof"; s.Discipline = Discipline.KienTruc; s.Story = r.Story; s.Color = ColRoof;
                yield return s;
            }
            k = 0;
            foreach (var g in gables)
            {
                var c = new Vec3(g.Average(q => q.X), g.Average(q => q.Y), 0);
                var mid = new Vec3((r.Outline.Min(q => q[0]) + r.Outline.Max(q => q[0])) / 2, (r.Outline.Min(q => q[1]) + r.Outline.Max(q => q[1])) / 2, 0);
                var s = RoofGeometry.PlateSolid(g, 0.22, mid - c);
                s.Id = $"THUHOI-{r.Id}-{++k}"; s.Name = "Tường thu hồi 220"; s.Category = "Tường"; s.IfcType = "IfcWall"; s.Discipline = Discipline.KienTruc; s.Story = r.Story; s.Color = ColGable;
                yield return s;
            }
        }
        // ---- lan can ----
        foreach (var r in a.Railings.Where(r => r.Points.Count >= 2))
        {
            double z0 = ModelSolids.StoryZ(p, r.Story) + r.BaseOffset, H = r.Height;
            for (int i = 1; i < r.Points.Count; i++)
            {
                double ax = r.Points[i - 1][0], ay = r.Points[i - 1][1], bx = r.Points[i][0], by = r.Points[i][1];
                double L = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                if (L < 1e-6) continue;
                var x = new Vec3((bx - ax) / L, (by - ay) / L, 0);
                Solid Seg(string id, string name, double y0, double y1, double zb, double depth, double[] col) => new()
                {
                    Id = id, Name = name, Category = "Lan can", IfcType = "IfcRailing", Discipline = Discipline.KienTruc, Story = r.Story,
                    Origin = new Vec3(ax, ay, zb), XDir = x, ZDir = Vec3.UnitZ, Profile = Solid.Rect(0, y0, L, y1), Depth = depth, Color = col
                };
                string nm = ArchDetail.RailName(r.Kind);
                if (r.Kind == RailKind.Xay) { yield return Seg($"LC-{r.Id}-{i}", nm, -0.055, 0.055, z0, H, ColGable); continue; }
                yield return Seg($"LC-{r.Id}-{i}-TV", nm + " – tay vịn", -0.03, 0.03, z0 + H - 0.05, 0.05, r.Kind == RailKind.Inox ? new[] { 0.55, 0.38, 0.22 } : ColRail);
                if (r.Kind == RailKind.Kinh) yield return Seg($"LC-{r.Id}-{i}-K", nm + " – kính", -0.006, 0.006, z0 + 0.05, H - 0.12, ColGlass);
                else
                {
                    yield return Seg($"LC-{r.Id}-{i}-TD", nm + " – thanh dưới", -0.015, 0.015, z0 + 0.08, 0.03, ColRail);
                    yield return Seg($"LC-{r.Id}-{i}-S", nm + " – song", -0.008, 0.008, z0 + 0.11, H - 0.18, ColRail);
                }
                // trụ: đầu đoạn + chia đều ≤ 1,2 m
                int nPost = Math.Max(1, (int)Math.Ceiling(L / 1.2));
                for (int k = 0; k <= nPost; k++)
                {
                    if (k == nPost && i < r.Points.Count - 1) continue;   // trụ cuối đoạn = trụ đầu đoạn sau
                    double t = L * k / nPost;
                    yield return new Solid
                    {
                        Id = $"LC-{r.Id}-{i}-T{k}", Name = nm + " – trụ", Category = "Lan can", IfcType = "IfcRailing", Discipline = Discipline.KienTruc, Story = r.Story,
                        Origin = new Vec3(ax + x.X * t, ay + x.Y * t, z0), XDir = x, ZDir = Vec3.UnitZ, Profile = Solid.Rect(-0.02, -0.02, 0.02, 0.02), Depth = H - 0.05, Color = ColRail
                    };
                }
            }
        }
        // ---- phào chỉ ----
        foreach (var s in a.Sweeps)
        {
            var w = ArchDetail.WallOf(p, s.WallId);
            if (w == null || w.IsCurved || w.Chord < 1e-6) continue;
            double t = ModelSolids.WallThickness(p, w), L = w.Chord, z0 = ModelSolids.StoryZ(p, w.Story);
            var x = new Vec3((w.X2 - w.X1) / L, (w.Y2 - w.Y1) / L, 0);
            double ya = s.Side * t / 2, yb = s.Side * (t / 2 + s.Width);
            int k = 0;
            foreach (var (a0, a1) in ArchDetail.SweepPieces(p, s))
                yield return new Solid
                {
                    Id = $"PHAO-{s.Id}-{++k}", Name = s.Name, Category = "Phào chỉ", IfcType = "IfcBuildingElementProxy", Discipline = Discipline.KienTruc, Story = w.Story,
                    Origin = new Vec3(w.X1, w.Y1, z0 + s.Z - s.Height / 2), XDir = x, ZDir = Vec3.UnitZ,
                    Profile = Solid.Rect(a0, Math.Min(ya, yb), a1, Math.Max(ya, yb)), Depth = s.Height, Color = ColSweep
                };
        }
        // ---- ốp / sơn mặt tường ----
        foreach (var c in a.Claddings)
        {
            var w = ArchDetail.WallOf(p, c.WallId);
            if (w == null || w.IsCurved || w.Chord < 1e-6) continue;
            double t = ModelSolids.WallThickness(p, w), L = w.Chord, z0 = ModelSolids.StoryZ(p, w.Story);
            var x = new Vec3((w.X2 - w.X1) / L, (w.Y2 - w.Y1) / L, 0);
            double ya = c.Side * t / 2, yb = c.Side * (t / 2 + Math.Max(0.002, c.Thickness));
            int k = 0;
            foreach (var (a0, a1, zz0, zz1) in ArchDetail.CladdingRects(p, c))
                yield return new Solid
                {
                    Id = $"OP-{c.Id}-{++k}", Name = c.Material, Category = "Ốp / sơn", IfcType = "IfcCovering", Discipline = Discipline.KienTruc, Story = w.Story,
                    Origin = new Vec3(w.X1, w.Y1, z0 + zz0), XDir = x, ZDir = Vec3.UnitZ,
                    Profile = Solid.Rect(a0, Math.Min(ya, yb), a1, Math.Max(ya, yb)), Depth = zz1 - zz0, Color = ArchDetail.CladColor(c.Material)
                };
        }
    }
}

/// <summary>Tải lan can lên dầm đỡ (gọi trong WallLoadGenerator.Apply, nguồn "WALL" nên tự sinh lại mỗi lần phân tích).</summary>
public static class ArchDetailLoads
{
    /// <summary>Tải mặc định của lớp mái (kN/m² mặt bằng, kể xà gồ / li tô) theo tên kiểu mái.</summary>
    public static double DefaultRoofLoad(string type)
    {
        string t = KetCauVXT.Ui.CommandSearch.Fold(type ?? "").ToLowerInvariant();
        return t.Contains("ngoi") ? 0.9 : t.Contains("ton") ? 0.3 : t.Contains("kinh") ? 0.6 : 0.5;
    }

    public static void Apply(Project p, List<string> warn)
    {
        if (p.Arch == null) return;
        // ---- tải lớp mái (tôn / ngói trên tường thu hồi, xà gồ) dồn lên sàn mái ----
        foreach (var r in p.Arch.Roofs.Where(r => r.SlabLoad > 0 && r.Outline.Count >= 3))
        {
            double x0 = r.Outline.Min(q => q[0]), x1 = r.Outline.Max(q => q[0]), y0 = r.Outline.Min(q => q[1]), y1 = r.Outline.Max(q => q[1]);
            int n = 0;
            foreach (var sl in p.Slabs.Where(s => s.Story == r.Story))
            {
                var pts = sl.Nodes.Select(id => p.Node(id)).Where(nd => nd != null).ToList();
                if (pts.Count < 3) continue;
                double cx = pts.Average(q => q.X), cy = pts.Average(q => q.Y);
                if (cx < x0 - 1e-6 || cx > x1 + 1e-6 || cy < y0 - 1e-6 || cy > y1 + 1e-6) continue;
                if (r.Outline.Count > 4 && !RasterOutline.Inside(r.Outline.Select(q => (q[0], q[1])).ToList(), cx, cy)) continue;
                p.AreaLoads.Add(new AreaLoad { Slab = sl.Id, Pattern = "HTHT", Q = Math.Round(r.SlabLoad, 4), Note = $"{r.Type} (mái #{r.Id}) dồn lên sàn", Source = WallLoadGenerator.WallSource });
                n++;
            }
            if (n == 0) warn.Add($"Mái #{r.Id} ({r.Type}) tầng {r.Story}: không có ô sàn nào trong đường bao để dồn tải {r.SlabLoad:0.##} kN/m².");
        }
        if (p.Arch.Railings == null || p.Arch.Railings.Count == 0) return;
        var stories = p.Stories.ToDictionary(s => s.Name, s => s.Elevation);
        foreach (var r in p.Arch.Railings)
        {
            if (!stories.TryGetValue(r.Story, out double z)) { warn.Add($"Lan can #{r.Id}: không có tầng {r.Story}."); continue; }
            double q = ArchDetail.RailLoad(r);
            if (q <= 0) continue;
            string note = $"{ArchDetail.RailName(r.Kind)} H = {r.Height:0.00} m";
            for (int i = 1; i < r.Points.Count; i++)
            {
                var w = new WallLine { Story = r.Story, X1 = r.Points[i - 1][0], Y1 = r.Points[i - 1][1], X2 = r.Points[i][0], Y2 = r.Points[i][1] };
                double L = w.Chord;
                if (L < 0.05) continue;
                double covered = 0;
                // hướng ra ngoài (phía xa tâm các ô sàn của tầng) cho tải ngang
                double nx = -(w.Y2 - w.Y1) / L, ny = (w.X2 - w.X1) / L;
                var sl0 = p.Slabs.Where(s => s.Story == r.Story).SelectMany(s => s.Nodes).Select(id => p.Node(id)).Where(nd => nd != null).ToList();
                if (sl0.Count > 0)
                {
                    double cx = sl0.Average(nd => nd.X), cy = sl0.Average(nd => nd.Y);
                    if ((cx - (w.X1 + w.X2) / 2) * nx + (cy - (w.Y1 + w.Y2) / 2) * ny > 0) { nx = -nx; ny = -ny; }
                }
                double qh = Math.Max(0, r.HorizontalLoad);
                foreach (var (f, r0, r1, len) in WallLoadGenerator.BeamsUnder(p, w, z, 0.22))
                {
                    var fl = new FrameLoad { Element = f.Id, Pattern = "HTHT", Dir = LoadDir.Gravity, Note = note, Source = WallLoadGenerator.WallSource };
                    fl.Points.Add(new[] { r0, q }); fl.Points.Add(new[] { r1, q });
                    p.FrameLoads.Add(fl);
                    if (qh > 0 && p.Pattern("HT") != null)
                        foreach (var (dir, comp) in new[] { (LoadDir.GlobalX, qh * nx), (LoadDir.GlobalY, qh * ny) })
                        {
                            if (Math.Abs(comp) < 1e-6) continue;
                            var fh = new FrameLoad { Element = f.Id, Pattern = "HT", Dir = dir, Note = $"Tải ngang tay vịn {qh:0.##} kN/m (lan can #{r.Id})", Source = WallLoadGenerator.WallSource };
                            fh.Points.Add(new[] { r0, Math.Round(comp, 4) }); fh.Points.Add(new[] { r1, Math.Round(comp, 4) });
                            p.FrameLoads.Add(fh);
                        }
                    covered += len;
                }
                double rest = L - covered;
                if (rest <= 0.1) continue;
                var sl = ModelEdit.SlabAt(p, r.Story, (w.X1 + w.X2) / 2, (w.Y1 + w.Y2) / 2);
                if (sl == null)
                {
                    // lan can sát mép sàn (ban công) thường nằm ngay trong mép ô sàn: thử lùi vào 0,15 m hai phía
                    foreach (int sg in new[] { 1, -1 })
                    {
                        sl = ModelEdit.SlabAt(p, r.Story, (w.X1 + w.X2) / 2 + sg * 0.15 * nx, (w.Y1 + w.Y2) / 2 + sg * 0.15 * ny);
                        if (sl != null) break;
                    }
                }
                if (sl == null) { warn.Add($"Lan can #{r.Id} tầng {r.Story}: {rest:0.00} m không có dầm hoặc sàn đỡ – bỏ qua tải phần này."); continue; }
                var pts = sl.Nodes.Select(id => p.Node(id)).Select(n => (n.X, n.Y)).ToList();
                double a = Math.Abs(KetCauVXT.Import.ImportGeometry.SignedArea(pts));
                if (a < 1e-6) continue;
                p.AreaLoads.Add(new AreaLoad { Slab = sl.Id, Pattern = "HTHT", Q = Math.Round(q * rest / a, 4), Note = $"Lan can không có dầm đỡ dài {rest:0.00} m ({note})", Source = WallLoadGenerator.WallSource });
            }
        }
    }
}
