using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Arch;

// =====================================================================================================
// PHƯƠNG ÁN KIẾN TRÚC (*.vxtphuongan.json) – định dạng chung cho đội agent:
//   KTS (agent) lập từ mô tả / ảnh / PDF / CAD  →  Revit dựng mô hình + hồ sơ (add-in Việt Const, lệnh "Dựng kiến trúc từ phương án")
//                                               →  SketchUp dựng khối + phối cảnh (plugin vxt_vietconst)
//                                               →  Việt Const tính kết cấu (đọc thẳng như *.vxtrevit.json)
// Cùng lược đồ RevitExchange (m, độ), thêm Stairs, Facade, Site. Cửa dùng Level (không cần HostId).
// =====================================================================================================

public class RvStair
{
    public string Id { get; set; } = "";
    /// <summary>Tầng chân thang (tên cao độ)</summary>
    public string BaseLevel { get; set; } = "";
    public string TopLevel { get; set; } = "";
    /// <summary>Đường bao ô thang (lỗ sàn ở tầng trên), m</summary>
    public List<double[]> Outline { get; set; } = new();
    /// <summary>"1 vế", "2 vế chữ U", "chữ L", "xoắn"</summary>
    public string Shape { get; set; } = "2 vế chữ U";
    public double Width { get; set; } = 0.9;
    /// <summary>Các vế thang theo thứ tự đi lên: đường tim vế từ bậc đầu → bậc cuối (hướng lên), số bậc đứng (risers) của vế. Có thì dựng đúng theo đây.</summary>
    public List<RvStairRun> Runs { get; set; } = new();
    /// <summary>Chiếu nghỉ (đa giác), theo thứ tự giữa các vế</summary>
    public List<List<double[]>> Landings { get; set; } = new();
}

public class RvStairRun
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public double Width { get; set; } = 1.0;
    /// <summary>Số bậc đứng của vế (vế n bậc đứng có n − 1 mặt bậc trên đường tim)</summary>
    public int Risers { get; set; }
}

public class RvFacadeMaterial
{
    /// <summary>Bộ phận: "Tường ngoài", "Tường điểm nhấn", "Lan can", "Khung cửa", "Kính", "Mái", "Ốp chân tường", "Lam"…</summary>
    public string Part { get; set; } = "";
    public string Material { get; set; } = "";
    /// <summary>Màu dạng #RRGGBB</summary>
    public string Color { get; set; } = "";
}

public class RvFacade
{
    /// <summary>Phong cách: Hiện đại, Tân cổ điển, Indochine, Tối giản…</summary>
    public string Style { get; set; } = "";
    /// <summary>Mô tả mặt tiền bằng lời (để dựng và để làm câu lệnh render)</summary>
    public string Description { get; set; } = "";
    public List<RvFacadeMaterial> Materials { get; set; } = new();
    /// <summary>Ảnh tham khảo người dùng đưa (đường dẫn trên máy)</summary>
    public List<string> ReferenceImages { get; set; } = new();
    /// <summary>Hướng mặt tiền: góc pháp tuyến ra ngoài (độ, 0 = +X, 90 = +Y, 180 = −X, 270 = −Y)</summary>
    public double FrontDirectionDeg { get; set; } = 180;
}

public class RvSite
{
    public double Width { get; set; }
    public double Depth { get; set; }
    public string Address { get; set; } = "";
    /// <summary>Khoảng lùi trước / sau (m)</summary>
    public double FrontSetback { get; set; }
    public double BackSetback { get; set; }
}

public sealed class PlanIssue
{
    public bool Error { get; set; }
    public string Message { get; set; } = "";
    public override string ToString() => (Error ? "[Lỗi] " : "[Cảnh báo] ") + Message;
}

public static class PlanSpec
{
    public const string Extension = ".vxtphuongan.json";

    private static readonly JsonSerializerOptions WriteOpt = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static string ToJson(RevitExchange ex) => JsonSerializer.Serialize(ex, WriteOpt);

    public static void Save(RevitExchange ex, string path) => File.WriteAllText(path, ToJson(ex), new UTF8Encoding(false));

    // ------------------------------------------------------------------ Revit → phương án
    /// <summary>
    /// Mô hình Revit đã có (*.vxtrevit.json) → phương án để dựng lại dự án mới: bỏ khung kết cấu, cửa ghi tầng (theo tường chủ) + họ / loại gốc,
    /// cao độ không có tường tầng (VD cốt sân) giữ nguyên, ô thang lấy từ phòng tên "Cầu thang" (tầng chân = tầng ngay dưới) – vế thang (Runs) do KTS thiết kế tiếp.
    /// </summary>
    public static RevitExchange FromRevit(RevitExchange src, IEnumerable<string> familySources = null, bool moveToLotOrigin = false)
    {
        double dx = 0, dy = 0;
        if (moveToLotOrigin && src.Walls.Count > 0)
        {
            dx = -src.Walls.Min(w => Math.Min(w.X1, w.X2)); dy = -src.Walls.Min(w => Math.Min(w.Y1, w.Y2));
        }
        double X(double v) => Math.Round(v + dx, 3); double Y(double v) => Math.Round(v + dy, 3);
        var ex = new RevitExchange { ProjectName = src.ProjectName, SchemaVersion = "2.0" };
        ex.Levels.AddRange(src.Levels.Select(l => new RvLevel { Name = l.Name, Elevation = l.Elevation }));
        ex.Grids.AddRange(src.Grids.Select(g => new RvGrid { Name = g.Name.Split(' ')[0], X1 = X(g.X1), Y1 = Y(g.Y1), X2 = X(g.X2), Y2 = Y(g.Y2) }));
        var wl = new Dictionary<string, string>();
        foreach (var w in src.Walls)
        {
            wl[w.Id] = w.Level;
            ex.Walls.Add(new RvWall { Id = w.Id, Level = w.Level, X1 = X(w.X1), Y1 = Y(w.Y1), X2 = X(w.X2), Y2 = Y(w.Y2), Height = w.Height, BaseOffset = w.BaseOffset, TypeName = w.TypeName, Layers = w.Layers });
        }
        foreach (var o in src.Openings)
            ex.Openings.Add(new RvOpening
            {
                Id = o.Id, Category = o.Category, TypeName = o.TypeName, Family = o.Family, X = X(o.X), Y = Y(o.Y), Width = o.Width, Height = o.Height, Sill = o.Sill,
                Level = !string.IsNullOrEmpty(o.Level) ? o.Level : (wl.TryGetValue(o.HostId ?? "", out var lv) ? lv : ""),
            });
        foreach (var r in src.Rooms) ex.Rooms.Add(new RvRoom { Id = r.Id, Level = r.Level, Name = r.Name, Number = r.Number, Outline = r.Outline.Select(p => new[] { X(p[0]), Y(p[1]) }).ToList() });
        foreach (var f in src.Floors) ex.Floors.Add(new RvFloor { Id = f.Id, Level = f.Level, Thickness = f.Thickness, Outline = f.Outline.Select(p => new[] { X(p[0]), Y(p[1]) }).ToList() });
        var lvs = src.Levels.OrderBy(l => l.Elevation).ToList();
        foreach (var r in src.Rooms.Where(r => LocationDb.Norm(r.Name).Contains("CAU THANG") && r.Outline.Count >= 3))
        {
            int k = lvs.FindIndex(l => l.Name == r.Level);
            if (k <= 0) continue;
            // tầng chân: tầng ngay dưới có tường tầng
            var below = lvs.Take(k).LastOrDefault(l => src.Walls.Any(w => w.Level == l.Name && w.Height >= 1.5));
            if (below == null) continue;
            var ol = r.Outline.Select(p => new[] { X(p[0]), Y(p[1]) }).ToList();
            double w0 = ol.Max(p => p[0]) - ol.Min(p => p[0]), h0 = ol.Max(p => p[1]) - ol.Min(p => p[1]);
            ex.Stairs.Add(new RvStair { Id = "CT" + (ex.Stairs.Count + 1), BaseLevel = below.Name, TopLevel = r.Level, Outline = Rect(ol.Min(p => p[0]), ol.Min(p => p[1]), ol.Max(p => p[0]), ol.Max(p => p[1])), Width = Math.Round(Math.Min(1.0, Math.Min(w0, h0) / (Math.Min(w0, h0) < 1.8 ? 1 : 2)), 2), Shape = Math.Min(w0, h0) < 1.8 ? "1 vế thẳng" : "2 vế chữ U" });
        }
        if (familySources != null) ex.FamilySources.AddRange(familySources);
        return ex;
    }

    // ------------------------------------------------------------------ CAD → phương án
    /// <summary>Mặt bằng kiến trúc đọc từ DWG/DXF (ArchPlanReader) → phương án. Cao độ cộng dồn theo chiều cao tầng, tầng đầu ±0.000.</summary>
    public static RevitExchange FromArchDrawing(ArchDrawing d, string name, double baseElevation = 0)
    {
        var ex = new RevitExchange { ProjectName = name, SchemaVersion = "2.0" };
        double z = baseElevation;
        int n = 0, wid = 0, oid = 0, rid = 0;
        var plans = d.Floors.Where(f => f.Include).OrderBy(f => f.FloorNo).ToList();
        double xmin = double.MaxValue, xmax = double.MinValue, ymin = double.MaxValue, ymax = double.MinValue;
        foreach (var f in plans)
            foreach (var w in f.Walls)
            {
                xmin = Math.Min(xmin, Math.Min(w.Ax, w.Bx)); xmax = Math.Max(xmax, Math.Max(w.Ax, w.Bx));
                ymin = Math.Min(ymin, Math.Min(w.Ay, w.By)); ymax = Math.Max(ymax, Math.Max(w.Ay, w.By));
            }
        foreach (var f in plans)
        {
            n++;
            string lv = f.IsRoofPlan ? "Mái" : (string.IsNullOrWhiteSpace(f.Title) ? "Tầng " + n : Clean(f.Title));
            if (ex.Levels.Any(l => l.Name == lv)) lv += " (" + n + ")";
            ex.Levels.Add(new RvLevel { Name = lv, Elevation = Math.Round(z, 3) });
            double h = f.IsRoofPlan ? 0 : (f.Height > 2 ? f.Height : 3.3);
            foreach (var w in f.Walls)
            {
                double t = w.Thickness > 0.05 ? w.Thickness : 0.11;
                double wh = f.IsRoofPlan ? 0.9 : h;
                ex.Walls.Add(new RvWall
                {
                    Id = "W" + (++wid), Level = lv, X1 = R(w.Ax), Y1 = R(w.Ay), X2 = R(w.Bx), Y2 = R(w.By), Height = wh,
                    TypeName = WallTypeName(t), Layers = WallLayers(t),
                });
            }
            foreach (var o in f.Openings)
            {
                bool door = (o.Kind ?? "").IndexOf("đi", StringComparison.OrdinalIgnoreCase) >= 0 || (o.Kind ?? "").IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0;
                // điểm chèn block cửa trong CAD thường ở bản lề / mép cánh → kéo về tim tường gần nhất (trong phạm vi 1 bề rộng cửa)
                var (ox2, oy2) = SnapToWall(f.Walls, o.X, o.Y, Math.Max(0.6, o.Width));
                ex.Openings.Add(new RvOpening
                {
                    Id = "O" + (++oid), Level = lv, Category = door ? "Door" : "Window", X = R(ox2), Y = R(oy2),
                    Width = o.Width > 0.3 ? R(o.Width) : (door ? 0.9 : 1.2), Height = door ? 2.2 : 1.4, Sill = door ? 0 : 0.9,
                    TypeName = door ? "Cửa đi" : "Cửa sổ",
                });
            }
            foreach (var r in f.Rooms.Where(r => !string.IsNullOrWhiteSpace(r.Text)))
                ex.Rooms.Add(new RvRoom { Id = "R" + (++rid), Level = lv, Name = Clean(r.Text), Number = rid.ToString(CultureInfo.InvariantCulture), Outline = Square(r.X, r.Y, 0.3) });
            foreach (var zn in f.Zones.Where(q => q.Kind == RoomKind.Stair))
                ex.Stairs.Add(new RvStair
                {
                    Id = "T" + (ex.Stairs.Count + 1), BaseLevel = lv, Outline = Rect(zn.X0, zn.Y0, zn.X1, zn.Y1),
                    Width = Math.Round(Math.Min(zn.X1 - zn.X0, zn.Y1 - zn.Y0) / 2, 2),
                });
            if (!f.IsRoofPlan)
            {
                var ol = f.Outline.Count >= 3 ? f.Outline.Select(p => new[] { R(p.X), R(p.Y) }).ToList()
                         : (xmin < xmax ? Rect(xmin, ymin, xmax, ymax) : new List<double[]>());
                if (ol.Count >= 3 && n > 1) ex.Floors.Add(new RvFloor { Id = "F" + n, Level = lv, Thickness = 0.1, Outline = ol });
            }
            z += h;
        }
        // tầng trên cùng chưa có mặt bằng mái → thêm cao độ mái
        if (plans.Count > 0 && !plans.Last().IsRoofPlan)
        {
            ex.Levels.Add(new RvLevel { Name = "Mái", Elevation = Math.Round(z, 3) });
            if (xmin < xmax) ex.Floors.Add(new RvFloor { Id = "F-mai", Level = "Mái", Thickness = 0.1, Outline = Rect(xmin, ymin, xmax, ymax) });
        }
        // cầu thang: tầng trên = tầng kế tiếp
        foreach (var s in ex.Stairs)
        {
            int i = ex.Levels.FindIndex(l => l.Name == s.BaseLevel);
            if (i >= 0 && i + 1 < ex.Levels.Count) s.TopLevel = ex.Levels[i + 1].Name;
        }
        // trục
        foreach (var g in plans.SelectMany(f => f.Grids).GroupBy(g => (g.Dir, Math.Round(g.Coord, 2))).Select(g => g.First()))
        {
            bool x = g.Dir == GridDir.X;   // trục X = Coord: đường song song phương Y
            ex.Grids.Add(x
                ? new RvGrid { Name = g.Name, X1 = R(g.Coord), Y1 = R(ymin - 1.5), X2 = R(g.Coord), Y2 = R(ymax + 1.5) }
                : new RvGrid { Name = g.Name, X1 = R(xmin - 1.5), Y1 = R(g.Coord), X2 = R(xmax + 1.5), Y2 = R(g.Coord) });
        }
        if (xmin < xmax) ex.Site = new RvSite { Width = R(xmax - xmin), Depth = R(ymax - ymin) };
        return ex;
    }

    public static string WallTypeName(double t) => $"Tường {t * 1000:0} trát 2 mặt";

    /// <summary>Lớp cấu tạo tường gạch trát 2 mặt (vữa 15 mm mỗi bên).</summary>
    public static List<RvLayer> WallLayers(double t)
    {
        double core = Math.Max(0.05, t - 0.03);
        return new List<RvLayer>
        {
            new() { Function = "Finish1", Material = "Vữa trát", Thickness = 0.015 },
            new() { Function = "Structure", Material = t >= 0.2 ? "Gạch đặc" : "Gạch rỗng", Thickness = Math.Round(core, 3) },
            new() { Function = "Finish1", Material = "Vữa trát", Thickness = 0.015 },
        };
    }

    private static double R(double v) => Math.Round(v, 3);
    private static string Clean(string s) => (s ?? "").Replace("\\P", " ").Replace("\n", " ").Trim();
    public static List<double[]> Rect(double x0, double y0, double x1, double y1) =>
        new() { new[] { R(x0), R(y0) }, new[] { R(x1), R(y0) }, new[] { R(x1), R(y1) }, new[] { R(x0), R(y1) } };
    private static List<double[]> Square(double x, double y, double h) => Rect(x - h, y - h, x + h, y + h);

    // ------------------------------------------------------------------ kiểm tra
    /// <summary>Kiểm tra phương án trước khi dựng: cao độ, tường, cửa trên tường, phòng, cầu thang, mặt tiền.</summary>
    public static List<PlanIssue> Validate(RevitExchange ex)
    {
        var r = new List<PlanIssue>();
        void E(string m) => r.Add(new PlanIssue { Error = true, Message = m });
        void W(string m) => r.Add(new PlanIssue { Message = m });
        if (ex.Levels.Count < 2) E("Cần ít nhất 2 cao độ (tầng 1 và mái).");
        var lv = ex.Levels.OrderBy(l => l.Elevation).ToList();
        foreach (var g in ex.Levels.GroupBy(l => l.Name).Where(g => g.Count() > 1)) E($"Trùng tên cao độ \"{g.Key}\".");
        for (int i = 1; i < lv.Count; i++)
        {
            double h = lv[i].Elevation - lv[i - 1].Elevation;
            if (h < 2.4 && !(lv[i - 1].Elevation < 0)) W($"Chiều cao tầng {lv[i - 1].Name} → {lv[i].Name} chỉ {h:0.00} m (QCVN tối thiểu thường 2,7–3,0 m phòng ở).");
            if (h > 6) W($"Chiều cao tầng {lv[i - 1].Name} → {lv[i].Name} là {h:0.00} m – kiểm tra lại.");
        }
        var names = new HashSet<string>(ex.Levels.Select(l => l.Name));
        foreach (var w in ex.Walls)
        {
            if (!names.Contains(w.Level)) E($"Tường {w.Id}: không có cao độ \"{w.Level}\".");
            double len = Math.Sqrt((w.X2 - w.X1) * (w.X2 - w.X1) + (w.Y2 - w.Y1) * (w.Y2 - w.Y1));
            if (len < 0.05) E($"Tường {w.Id}: dài {len:0.000} m.");
            double t = w.Layers?.Sum(l => l.Thickness) ?? 0;
            if (t <= 0) t = RevitMaterials.ThicknessFromName(w.TypeName) ?? 0;
            if (t < 0.05 || t > 0.5) W($"Tường {w.Id}: chiều dày {t * 1000:0} mm bất thường.");
            if (w.Height <= 0) E($"Tường {w.Id}: chưa có chiều cao.");
        }
        var storeyLevels = lv.Where(l => ex.Walls.Any(w => w.Level == l.Name && w.Height >= 1.5)).ToList();
        if (storeyLevels.Count == 0) E("Không có tầng nào có tường cao ≥ 1,5 m.");
        foreach (var o in ex.Openings)
        {
            var host = !string.IsNullOrEmpty(o.HostId) ? ex.Walls.FirstOrDefault(w => w.Id == o.HostId) : null;
            if (host == null)
            {
                var cand = ex.Walls.Where(w => string.IsNullOrEmpty(o.Level) || w.Level == o.Level).ToList();
                double best = double.MaxValue;
                foreach (var w in cand) { double d = DistToSeg(o.X, o.Y, w); if (d < best) { best = d; host = w; } }
                if (host == null || best > 0.3) { E($"Cửa {o.Id} ({o.Category} {o.Width:0.00} m tại {o.X:0.00}; {o.Y:0.00}) không nằm trên tường nào{(o.Level.Length > 0 ? " ở " + o.Level : "")}."); continue; }
            }
            double len = Math.Sqrt((host.X2 - host.X1) * (host.X2 - host.X1) + (host.Y2 - host.Y1) * (host.Y2 - host.Y1));
            if (o.Width >= len - 0.05) E($"Cửa {o.Id}: rộng {o.Width:0.00} m ≥ chiều dài tường {host.Id} ({len:0.00} m).");
            if (o.Height + o.Sill > host.Height + 0.01) E($"Cửa {o.Id}: cao {o.Sill + o.Height:0.00} m vượt chiều cao tường {host.Height:0.00} m.");
            if (o.Category == "Door" && o.Width < 0.6) W($"Cửa đi {o.Id} rộng {o.Width:0.00} m (< 0,6 m).");
        }
        var cat = RoomCatalog.Default();
        foreach (var rm in ex.Rooms)
        {
            if (!names.Contains(rm.Level)) E($"Phòng \"{rm.Name}\": không có cao độ \"{rm.Level}\".");
            if (string.IsNullOrWhiteSpace(rm.Name) || rm.Name.Trim().Equals("Room", StringComparison.OrdinalIgnoreCase)) W($"Phòng {rm.Id} ở {rm.Level} chưa có tên công năng.");
            else
            {
                var u = LocationDb.Norm(rm.Name);
                if (!cat.Any(c => c.Keys.Any(k => u.Contains(k)))) W($"Phòng \"{rm.Name}\": tên không nói rõ công năng (phòng ngủ, khách, bếp, WC, thờ, sảnh, cầu thang, ban công, kho, gara…).");
            }
        }
        int storeys = storeyLevels.Count;
        if (storeys >= 2 && ex.Stairs.Count == 0 && !ex.Rooms.Any(q => LocationDb.Norm(q.Name).Contains("THANG")))
            E($"Nhà {storeys} tầng nhưng chưa có cầu thang (mục Stairs hoặc phòng \"Cầu thang\").");
        foreach (var s in ex.Stairs)
        {
            if (!names.Contains(s.BaseLevel) || !names.Contains(s.TopLevel)) E($"Cầu thang {s.Id}: thiếu / sai tầng chân \"{s.BaseLevel}\" – tầng đỉnh \"{s.TopLevel}\".");
            if (s.Outline.Count < 3) E($"Cầu thang {s.Id}: chưa có đường bao ô thang.");
            else
            {
                double a = Math.Abs(Area(s.Outline));
                if (a < 2.5) W($"Cầu thang {s.Id}: ô thang {a:0.0} m² – nhỏ (thang 2 vế nhà phố thường 5–9 m²).");
                if (a > 16) W($"Cầu thang {s.Id}: ô thang {a:0.0} m² – lớn bất thường.");
            }
            if (s.Width < 0.75) W($"Cầu thang {s.Id}: vế rộng {s.Width:0.00} m (< 0,75 m).");
            if (s.Runs.Count > 0 && names.Contains(s.BaseLevel) && names.Contains(s.TopLevel))
            {
                double H = ex.Levels.First(l => l.Name == s.TopLevel).Elevation - ex.Levels.First(l => l.Name == s.BaseLevel).Elevation;
                int n = s.Runs.Sum(q => q.Risers);
                if (n <= 0 || s.Runs.Any(q => q.Risers < 2)) E($"Cầu thang {s.Id}: mỗi vế cần ≥ 2 bậc đứng (Risers).");
                else
                {
                    double h = H / n;
                    if (h > 0.19 || h < 0.13) E($"Cầu thang {s.Id}: chiều cao bậc {h * 1000:0} mm ({n} bậc cho {H:0.00} m) – nên 150–175 mm.");
                    foreach (var q in s.Runs)
                    {
                        double b = Math.Sqrt((q.X2 - q.X1) * (q.X2 - q.X1) + (q.Y2 - q.Y1) * (q.Y2 - q.Y1)) / (q.Risers - 1);
                        if (b < 0.25) E($"Cầu thang {s.Id}: vế {q.Risers} bậc dài không đủ – mặt bậc {b * 1000:0} mm (< 250 mm).");
                        else if (2 * h + b < 0.58 || 2 * h + b > 0.67) W($"Cầu thang {s.Id}: 2h + b = {(2 * h + b) * 1000:0} mm (nên 600–650 mm).");
                        if (q.Width < 0.9) W($"Cầu thang {s.Id}: vế rộng {q.Width:0.00} m (< 0,9 m).");
                    }
                }
            }
        }
        foreach (var f in ex.Floors)
        {
            if (!names.Contains(f.Level)) E($"Sàn {f.Id}: không có cao độ \"{f.Level}\".");
            if (f.Outline.Count < 3) E($"Sàn {f.Id}: chưa có đường bao.");
        }
        if (ex.Facade == null || string.IsNullOrWhiteSpace(ex.Facade.Description) && ex.Facade.Materials.Count == 0)
            W("Chưa có mô tả mặt tiền (Facade) – phối cảnh sẽ dùng vật liệu mặc định.");
        if (ex.Site != null && ex.Site.Width > 0 && ex.Walls.Count > 0)
        {
            double w = ex.Walls.Max(q => Math.Max(q.X1, q.X2)) - ex.Walls.Min(q => Math.Min(q.X1, q.X2));
            double d = ex.Walls.Max(q => Math.Max(q.Y1, q.Y2)) - ex.Walls.Min(q => Math.Min(q.Y1, q.Y2));
            double sw = Math.Min(ex.Site.Width, ex.Site.Depth), sd = Math.Max(ex.Site.Width, ex.Site.Depth);
            double bw = Math.Min(w, d), bd = Math.Max(w, d);
            if (bw > sw + 0.25 || bd > sd + 0.25) E($"Nhà {w:0.00} × {d:0.00} m vượt khu đất {ex.Site.Width:0.00} × {ex.Site.Depth:0.00} m.");
        }
        return r;
    }

    private static (double X, double Y) SnapToWall(List<ArchWall> walls, double x, double y, double maxD)
    {
        double best = maxD; (double, double) r = (x, y);
        foreach (var w in walls)
        {
            double dx = w.Bx - w.Ax, dy = w.By - w.Ay, l2 = dx * dx + dy * dy;
            if (l2 < 1e-9) continue;
            double t = Math.Max(0, Math.Min(1, ((x - w.Ax) * dx + (y - w.Ay) * dy) / l2));
            double px = w.Ax + t * dx, py = w.Ay + t * dy, dd = Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
            if (dd < best) { best = dd; r = (px, py); }
        }
        return r;
    }

    private static double DistToSeg(double x, double y, RvWall w)
    {
        double dx = w.X2 - w.X1, dy = w.Y2 - w.Y1, l2 = dx * dx + dy * dy;
        double t = l2 < 1e-12 ? 0 : Math.Clamp(((x - w.X1) * dx + (y - w.Y1) * dy) / l2, 0, 1);
        double px = w.X1 + t * dx - x, py = w.Y1 + t * dy - y;
        return Math.Sqrt(px * px + py * py);
    }

    public static double Area(List<double[]> o)
    {
        double a = 0;
        for (int i = 0; i < o.Count; i++) { var p = o[i]; var q = o[(i + 1) % o.Count]; a += p[0] * q[1] - q[0] * p[1]; }
        return a / 2;
    }

    // ------------------------------------------------------------------ mẫu
    /// <summary>Phương án mẫu: nhà phố 5 × 20 m, 3 tầng + tum thang, thang 2 vế giữa nhà, WC cuối nhà (dùng cho kiểm thử và làm khuôn cho agent).</summary>
    public static RevitExchange SampleTownhouse()
    {
        var ex = new RevitExchange { ProjectName = "Nhà phố mẫu 5x20", SchemaVersion = "2.0" };
        ex.Site = new RvSite { Width = 5, Depth = 20, Address = "Thái Nguyên", FrontSetback = 0 };
        string[] lvn = { "Tầng 1", "Tầng 2", "Tầng 3", "Mái" };
        double[] el = { 0, 3.6, 6.9, 10.2 };
        for (int i = 0; i < lvn.Length; i++) ex.Levels.Add(new RvLevel { Name = lvn[i], Elevation = el[i] });
        foreach (var (n, x) in new[] { ("1", 0.0), ("2", 5.0) }) ex.Grids.Add(new RvGrid { Name = n, X1 = x, Y1 = -1.5, X2 = x, Y2 = 21.5 });
        foreach (var (n, y) in new[] { ("A", 0.0), ("B", 4.5), ("C", 9.0), ("D", 14.5), ("E", 20.0) }) ex.Grids.Add(new RvGrid { Name = n, X1 = -1.5, Y1 = y, X2 = 6.5, Y2 = y });
        int wid = 0, oid = 0, rid = 0;
        void Wall(string lv, double x1, double y1, double x2, double y2, double t, double h) =>
            ex.Walls.Add(new RvWall { Id = "W" + (++wid), Level = lv, X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Height = h, TypeName = WallTypeName(t), Layers = WallLayers(t) });
        void Door(string lv, double x, double y, double w, double h = 2.2) => ex.Openings.Add(new RvOpening { Id = "O" + (++oid), Level = lv, Category = "Door", X = x, Y = y, Width = w, Height = h, TypeName = "Cửa đi" });
        void Win(string lv, double x, double y, double w, double h = 1.6, double sill = 0.8) => ex.Openings.Add(new RvOpening { Id = "O" + (++oid), Level = lv, Category = "Window", X = x, Y = y, Width = w, Height = h, Sill = sill, TypeName = "Cửa sổ" });
        void Room(string lv, string name, double x0, double y0, double x1, double y1) => ex.Rooms.Add(new RvRoom { Id = "R" + (++rid), Level = lv, Name = name, Number = rid.ToString(CultureInfo.InvariantCulture), Outline = Rect(x0, y0, x1, y1) });
        for (int i = 0; i < 3; i++)
        {
            string lv = lvn[i]; double h = el[i + 1] - el[i];
            // tường bao: 2 tường biên 220, mặt trước / sau 110 (mặt tiền nhiều cửa)
            Wall(lv, 0, 0, 0, 20, 0.22, h); Wall(lv, 5, 0, 5, 20, 0.22, h);
            Wall(lv, 0, 0, 5, 0, 0.11, h); Wall(lv, 0, 20, 5, 20, 0.11, h);
            // vách ô thang (y 9..14.5, x 2.6..5) và WC cuối nhà (y 17..20, x 3..5)
            Wall(lv, 2.6, 9, 2.6, 14.5, 0.11, h);
            Wall(lv, 3, 17, 5, 17, 0.11, h); Wall(lv, 3, 17, 3, 20, 0.11, h);
            Door(lv, 4, 17, 0.7);
            Win(lv, 4, 20, 0.6, 0.6, 1.6);
            if (i == 0)
            {
                Door(lv, 2.5, 0, 3.2, 2.7);      // cửa chính mặt tiền
                Win(lv, 1.5, 20, 1.2);
                Room(lv, "Gara + phòng khách", 0, 0, 5, 9); Room(lv, "Bếp + phòng ăn", 0, 14.5, 5, 17);
                Room(lv, "Sảnh + cầu thang", 2.6, 9, 5, 14.5); Room(lv, "WC", 3, 17, 5, 20);
            }
            else
            {
                Wall(lv, 0, 4.5, 5, 4.5, 0.11, h);   // phòng ngủ trước
                Door(lv, 1.5, 4.5, 0.9);
                Door(lv, 2.5, 0, 2.4, 2.4);          // cửa ra ban công
                Win(lv, 1.5, 20, 1.2);
                Room(lv, "Phòng ngủ " + (2 * i - 1), 0, 0, 5, 4.5);
                Room(lv, i == 2 ? "Phòng thờ + sinh hoạt" : "Sinh hoạt chung", 0, 4.5, 2.6, 17);
                Room(lv, "Phòng ngủ " + (2 * i), 0, 17, 3, 20);
                Room(lv, "WC", 3, 17, 5, 20);
                Room(lv, "Cầu thang", 2.6, 9, 5, 14.5);
                ex.Floors.Add(new RvFloor { Id = "F" + i, Level = lv, Thickness = 0.1, Outline = Rect(0, 0, 5, 20) });
            }
        }
        // mái: sân thượng + tum thang + tường chắn mái 1,1 m
        Wall("Mái", 0, 0, 0, 20, 0.11, 1.1); Wall("Mái", 5, 0, 5, 20, 0.11, 1.1);
        Wall("Mái", 0, 0, 5, 0, 0.11, 1.1); Wall("Mái", 0, 20, 5, 20, 0.11, 1.1);
        Wall("Mái", 2.6, 9, 2.6, 14.5, 0.11, 2.8); Wall("Mái", 2.6, 9, 5, 9, 0.11, 2.8); Wall("Mái", 2.6, 14.5, 5, 14.5, 0.11, 2.8);
        Door("Mái", 2.6, 12, 0.8);
        Room("Mái", "Sân thượng", 0, 0, 5, 9);
        ex.Floors.Add(new RvFloor { Id = "F-mai", Level = "Mái", Thickness = 0.12, Outline = Rect(0, 0, 5, 20) });
        ex.Stairs.Add(new RvStair { Id = "T1", BaseLevel = "Tầng 1", TopLevel = "Tầng 2", Outline = Rect(2.6, 9, 5, 14.5), Width = 1.1 });
        ex.Stairs.Add(new RvStair { Id = "T2", BaseLevel = "Tầng 2", TopLevel = "Tầng 3", Outline = Rect(2.6, 9, 5, 14.5), Width = 1.1 });
        ex.Stairs.Add(new RvStair { Id = "T3", BaseLevel = "Tầng 3", TopLevel = "Mái", Outline = Rect(2.6, 9, 5, 14.5), Width = 1.1 });
        ex.Facade = new RvFacade
        {
            Style = "Hiện đại tối giản",
            Description = "Mặt tiền 5 m, tầng 1 cửa kính lùa khung nhôm đen, tầng 2–3 ban công lan can kính, lam gỗ dọc che nắng, tường sơn trắng, ốp đá xám chân tường.",
            Materials =
            {
                new() { Part = "Tường ngoài", Material = "Sơn ngoại thất", Color = "#F2F0EB" },
                new() { Part = "Lam", Material = "Gỗ nhựa composite", Color = "#8B5A2B" },
                new() { Part = "Khung cửa", Material = "Nhôm sơn tĩnh điện", Color = "#2B2B2B" },
                new() { Part = "Kính", Material = "Kính cường lực", Color = "#9FC5D8" },
                new() { Part = "Ốp chân tường", Material = "Đá granite xám", Color = "#6E6E6E" },
            },
            FrontDirectionDeg = 270,
        };
        return ex;
    }
}
