using System.Globalization;
using System.Text.RegularExpressions;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Arch;

// =====================================================================
// BẢNG NHẬN DIỆN TẢI TRỌNG (xác nhận một lần trước khi lập mô hình kết cấu)
//   Phòng (theo tên) → công năng → tải hoàn thiện + hoạt tải (TCVN 2737:2023); tên không nhận ra → "Chưa rõ" (tô vàng).
//   Loại tường → trọng lượng 1 m² theo lớp cấu tạo Revit; vật liệu không nhận ra → giả thiết gạch đặc (tô vàng).
//   Mái: tải hoàn thiện, hoạt tải, chiều cao tường chắn mái.
//   Thiết bị nặng (bồn nước, téc nước) → tải tập trung tại nút gần nhất của cao trình chứa thiết bị.
// Người dùng sửa trên bảng → Apply(drawing, options) ghi lại công năng phòng, γ tường, tải mái; ApplyEquipment(project) sau khi lập mô hình.
// =====================================================================

public sealed class RoomLoadRow
{
    /// <summary>Tên phòng như trong mô hình (gộp các phòng cùng tên)</summary>
    public string Name { get; set; } = "";
    public int Count { get; set; }
    /// <summary>Các mặt bằng có phòng này</summary>
    public string Plans { get; set; } = "";
    public RoomKind Kind { get; set; }
    /// <summary>Công năng tự nhận từ tên (false = không nhận ra, đang lấy mặc định – cần xác nhận)</summary>
    public bool Sure { get; set; }
    public string KindName { get; set; } = "";
    public double Finish { get; set; }
    public double Live { get; set; }
}

public sealed class WallLoadRow
{
    /// <summary>Tên loại tường (Revit Wall Type) hoặc "Tường {t} mm"</summary>
    public string TypeName { get; set; } = "";
    public double Thickness { get; set; }
    /// <summary>Trọng lượng 1 m² tường (kN/m²), kể cả lớp trát / ốp – người dùng sửa được</summary>
    public double PerArea { get; set; }
    /// <summary>Vật liệu lớp chính nhận được</summary>
    public string Core { get; set; } = "";
    public bool Sure { get; set; }
    public int Count { get; set; }
    public double Length { get; set; }
    public string Note { get; set; } = "";
}

public sealed class EquipmentLoadRow
{
    public string Name { get; set; } = "";
    /// <summary>Toạ độ trong hệ của mặt bằng (đã dời gốc), m; Z so với mặt sàn tầng 1</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    /// <summary>Tải tập trung (kN, hướng xuống), gán vào HTHT</summary>
    public double Load { get; set; }
    public bool Sure { get; set; }
    public bool Include { get; set; } = true;
    public string Note { get; set; } = "";
}

public sealed class ArchLoadTakeoff
{
    public List<RoomLoadRow> Rooms { get; } = new();
    public List<WallLoadRow> Walls { get; } = new();
    public List<EquipmentLoadRow> Equipment { get; } = new();
    /// <summary>Bảng công năng → tải hoàn thiện, hoạt tải (bản sao của ArchImportOptions.Rooms, sửa được)</summary>
    public List<RoomType> Catalog { get; } = new();
    public double RoofFinish { get; set; }
    public double RoofLive { get; set; }
    public double ParapetHeight { get; set; }
    public double PlasterThickness { get; set; } = 0.015;
    public List<string> Notes { get; } = new();

    /// <summary>Số mục chương trình chưa chắc (tô vàng) – người dùng cần xem kỹ.</summary>
    public int UncertainCount => Rooms.Count(r => !r.Sure) + Walls.Count(w => !w.Sure) + Equipment.Count(e => !e.Sure);

    /// <summary>Lập bảng từ mặt bằng kiến trúc. <paramref name="ex"/> (file Revit) cho lớp cấu tạo tường và thiết bị; null = bản vẽ DXF.</summary>
    public static ArchLoadTakeoff From(ArchDrawing d, ArchImportOptions o, RevitExchange ex = null)
    {
        o ??= new ArchImportOptions();
        var t = new ArchLoadTakeoff { RoofFinish = o.RoofFinish, RoofLive = o.RoofLive, ParapetHeight = o.ParapetHeight, PlasterThickness = o.PlasterThickness };
        foreach (var rt in o.Rooms) t.Catalog.Add(new RoomType { Kind = rt.Kind, Name = rt.Name, Keys = rt.Keys, Finish = rt.Finish, Live = rt.Live, NoSlab = rt.NoSlab });
        var plans = d.Floors.Where(f => f.Include).ToList();

        // ---- phòng ----
        foreach (var g in plans.SelectMany(p => p.Rooms.Select(r => (p, r))).GroupBy(q => (q.r.Text ?? "").Trim(), StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key))
        {
            var r0 = g.First().r;
            bool sure = r0.Kind != RoomKind.Unknown;
            var kind = sure ? r0.Kind : RoomKind.Living;
            var rt = o.Rooms.FirstOrDefault(q => q.Kind == kind);
            t.Rooms.Add(new RoomLoadRow
            {
                Name = g.Key, Count = g.Count(), Plans = string.Join(", ", g.Select(q => Short(q.p)).Distinct()),
                Kind = kind, Sure = sure, KindName = rt?.Name ?? kind.ToString(), Finish = rt?.Finish ?? 1.2, Live = rt?.Live ?? 1.5,
            });
        }

        // ---- tường ----
        var rvByType = (ex?.Walls ?? new List<RvWall>()).Where(w => w != null).GroupBy(w => w.TypeName ?? "").ToDictionary(g => g.Key, g => g.First());
        double plaster = 2 * o.PlasterThickness * 18.0;
        foreach (var g in plans.Where(p => !p.IsRoofPlan).SelectMany(p => p.Walls).GroupBy(w => WallKey(w)).OrderByDescending(g => g.Sum(w => w.Length)))
        {
            var w0 = g.First();
            var row = new WallLoadRow { TypeName = g.Key, Thickness = w0.Thickness, Count = g.Count(), Length = Math.Round(g.Sum(w => w.Length), 2) };
            if (!string.IsNullOrEmpty(w0.TypeName) && rvByType.TryGetValue(w0.TypeName, out var rw))
            {
                var m = RevitMaterials.WallMass(rw, o);
                row.PerArea = m.PerArea; row.Core = m.Core; row.Sure = m.Sure; row.Note = m.Note;
                if (!m.Sure) row.Note = (row.Note + " Có lớp vật liệu không nhận ra – đang giả thiết gạch đặc 18 kN/m³.").Trim();
            }
            else
            {
                double gam = w0.UnitWeight > 0 ? w0.UnitWeight : o.WallUnitWeight;
                row.PerArea = Math.Round(w0.Thickness * gam + plaster, 3);
                row.Core = $"khối xây γ = {gam:0.#} kN/m³";
                row.Sure = false;
                row.Note = "Bản vẽ không ghi vật liệu – theo tuỳ chọn chung, kiểm tra loại gạch.";
            }
            t.Walls.Add(row);
        }

        // ---- thiết bị nặng (bồn / téc nước) ----
        if (ex?.Mep != null)
        {
            var groundZ = GroundElevation(ex);
            foreach (var m in ex.Mep.Where(q => q != null))
            {
                string n = ArchPlanReader.Norm((m.Name ?? "") + " " + (m.Category ?? ""));
                if (!Regex.IsMatch(n, @"BON NUOC|TEC NUOC|BE NUOC|WATER TANK|WATER HEATER TANK|STORAGE TANK|\bTANK\b")) continue;
                var (load, sure, note) = TankLoad(m.Name);
                t.Equipment.Add(new EquipmentLoadRow
                {
                    Name = string.IsNullOrWhiteSpace(m.Name) ? "Bồn nước" : m.Name.Trim(),
                    X = Math.Round(m.X1 - RevitOriginX(ex), 3), Y = Math.Round(m.Y1 - RevitOriginY(ex), 3), Z = Math.Round(m.Z1 - groundZ, 3),
                    Load = load, Sure = sure, Note = note,
                });
            }
        }
        if (t.Rooms.Count == 0) t.Notes.Add("Mô hình không có phòng (Room) – mọi ô sàn lấy công năng phòng ở (1,2 + 1,5 kN/m²). Nên đặt Room trong Revit để nhận tải đúng.");
        if (!plans.Any(p => p.Zones.Any(z => z.Kind == RoomKind.Stair)))
            t.Notes.Add("Không thấy phòng cầu thang – ô cầu thang sẽ thành ô sàn thường. Đặt Room tên \"Cầu thang\" trong Revit, hoặc khai báo ô cầu thang sau khi lập mô hình.");
        return t;
    }

    /// <summary>Ghi các giá trị đã xác nhận vào mặt bằng và tuỳ chọn trước khi lập đề xuất kết cấu.</summary>
    public void Apply(ArchDrawing d, ArchImportOptions o)
    {
        // công năng phòng theo tên
        var byName = Rooms.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var p in d.Floors)
            foreach (var r in p.Rooms)
                if (byName.TryGetValue((r.Text ?? "").Trim(), out var row))
                {
                    var was = r.Kind;
                    r.Kind = row.Kind;
                    if (was == row.Kind) continue;
                    // vùng đặc biệt theo công năng mới: bỏ vùng cũ của phòng này, thêm vùng (tại tâm phòng) cho cầu thang / thông tầng / ban công
                    p.Zones.RemoveAll(z => z.Kind == was && z.Contains(r.X, r.Y, 0.01));
                    if (row.Kind is RoomKind.Stair or RoomKind.Void or RoomKind.Balcony)
                        p.Zones.Add(new ArchZone { Kind = row.Kind, X0 = r.X, Y0 = r.Y, X1 = r.X, Y1 = r.Y, Note = "người dùng chọn cho phòng \"" + r.Text + "\"" });
                }
        // tải hoàn thiện / hoạt tải theo công năng
        foreach (var c in Catalog)
        {
            var rt = o.Rooms.FirstOrDefault(q => q.Kind == c.Kind);
            if (rt == null) continue;
            rt.Finish = c.Finish;
            rt.Live = c.Live;
        }
        // tường: γ tương đương theo loại
        double plaster = 2 * o.PlasterThickness * 18.0;
        var wByType = Walls.ToDictionary(w => w.TypeName, StringComparer.Ordinal);
        foreach (var w in d.Floors.SelectMany(p => p.Walls))
            if (wByType.TryGetValue(WallKey(w), out var row) && w.Thickness > 1e-6)
                w.UnitWeight = Math.Max(0.01, Math.Round((row.PerArea - plaster) / w.Thickness, 3));
        o.RoofFinish = RoofFinish;
        o.RoofLive = RoofLive;
        o.ParapetHeight = ParapetHeight;
    }

    /// <summary>Sau khi lập mô hình: tải thiết bị → nút gần nhất trên cao trình gần cao độ thiết bị (trong 1,5 m), trường hợp HTHT.</summary>
    public int ApplyEquipment(Project p)
    {
        int n = 0;
        foreach (var e in Equipment.Where(q => q.Include && q.Load > 0))
        {
            // cao trình đỡ thiết bị: cao trình cao nhất không vượt quá đáy thiết bị + 0,5 m
            var st = p.Stories.Where(s => s.Elevation <= e.Z + 0.5).OrderByDescending(s => s.Elevation).FirstOrDefault()
                     ?? p.Stories.OrderBy(s => Math.Abs(s.Elevation - e.Z)).FirstOrDefault();
            if (st == null) continue;
            var node = p.Nodes.Where(q => Math.Abs(q.Z - st.Elevation) < 0.02)
                .OrderBy(q => (q.X - e.X) * (q.X - e.X) + (q.Y - e.Y) * (q.Y - e.Y)).FirstOrDefault();
            if (node == null) continue;
            p.NodeLoads.Add(new NodeLoad { Node = node.Id, Pattern = "HTHT", Fz = -e.Load, Source = "" });
            n++;
        }
        return n;
    }

    // ------------------------------------------------------------------
    private static string WallKey(ArchWall w) =>
        !string.IsNullOrWhiteSpace(w.TypeName) ? w.TypeName.Trim() : $"Tường {(w.Thickness * 1000).ToString("0", CultureInfo.InvariantCulture)} mm";

    private static string Short(ArchFloorPlan p) => p.IsRoofPlan ? "Mái" : p.IsTum ? "Tum" : "T" + p.FloorNo.ToString(CultureInfo.InvariantCulture);

    /// <summary>Tải bồn nước từ tên: "1500L", "1,5 m3", "2000 lít" → nước + 10 % vỏ, chân đế; không đọc được → 1500 L (chưa chắc).</summary>
    public static (double Load, bool Sure, string Note) TankLoad(string name)
    {
        string s = (name ?? "").Replace(',', '.');
        var m = Regex.Match(s, @"(\d+(?:\.\d+)?)\s*(m3|m³|khoi|khối)", RegexOptions.IgnoreCase);
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v3) && v3 > 0 && v3 < 50)
            return (Math.Round(v3 * 10 * 1.1, 2), true, $"{v3:0.##} m³ nước + 10 % vỏ, chân đế");
        m = Regex.Match(s, @"(\d{3,5})\s*(l\b|lit|lít|liters?|litres?)?", RegexOptions.IgnoreCase);
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lit) && lit >= 300 && lit <= 20000)
            return (Math.Round(lit / 100 * 1.1, 2), true, $"{lit:0} lít nước + 10 % vỏ, chân đế");
        return (16.5, false, "Không đọc được dung tích từ tên – giả thiết 1500 lít; sửa theo bồn thực tế.");
    }

    private static double GroundElevation(RevitExchange ex)
    {
        var tall = (ex.Walls ?? new List<RvWall>()).Where(w => w != null && w.Height >= RevitArchReader.MinStoreyWall).Select(w => w.Level).ToHashSet();
        var lv = (ex.Levels ?? new List<RvLevel>()).Where(l => l != null && tall.Contains(l.Name)).OrderBy(l => l.Elevation).FirstOrDefault();
        return lv?.Elevation ?? 0;
    }

    // gốc toạ độ: cùng quy tắc với RevitArchReader.ToDrawing
    private static double RevitOriginX(RevitExchange ex)
    {
        var g = (ex.Grids ?? new List<RvGrid>()).Where(q => q != null && Math.Abs(q.X2 - q.X1) < 0.005 * Math.Max(1e-6, Math.Sqrt((q.X2 - q.X1) * (q.X2 - q.X1) + (q.Y2 - q.Y1) * (q.Y2 - q.Y1)))).ToList();
        return Math.Round(g.Count > 0 ? g.Min(q => (q.X1 + q.X2) / 2) : (ex.Walls ?? new List<RvWall>()).Where(w => w != null).Select(w => Math.Min(w.X1, w.X2)).DefaultIfEmpty(0).Min(), 4);
    }

    private static double RevitOriginY(RevitExchange ex)
    {
        var g = (ex.Grids ?? new List<RvGrid>()).Where(q => q != null && Math.Abs(q.Y2 - q.Y1) < 0.005 * Math.Max(1e-6, Math.Sqrt((q.X2 - q.X1) * (q.X2 - q.X1) + (q.Y2 - q.Y1) * (q.Y2 - q.Y1)))).ToList();
        return Math.Round(g.Count > 0 ? g.Min(q => (q.Y1 + q.Y2) / 2) : (ex.Walls ?? new List<RvWall>()).Where(w => w != null).Select(w => Math.Min(w.Y1, w.Y2)).DefaultIfEmpty(0).Min(), 4);
    }
}
