using KetCauVXT.Model;

namespace KetCauVXT.Import;

// =====================================================================
// NHẬP PHẦN KIẾN TRÚC + ĐIỆN NƯỚC TỪ REVIT (schema *.vxtrevit.json 2.0) và "Cập nhật từ Revit" theo UniqueId.
// Tường (đường định vị, loại + lớp cấu tạo), cửa đi / cửa sổ (tường chủ, vị trí dọc tường), phòng (đường bao),
// thiết bị điện nước (ống, phụ kiện, thiết bị vệ sinh, đèn, ổ cắm…) → khối bộ môn Điện nước.
// Bộ môn Điện nước (Model/Mep) có thể thay cách nhận ống/thiết bị qua RevitArchImport.MepHandler.
// =====================================================================
public class RvWall
{
    public string Id { get; set; } = "";
    public string Level { get; set; } = "";
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    /// <summary>Chiều cao (m)</summary>
    public double Height { get; set; }
    public double BaseOffset { get; set; }
    public string TypeName { get; set; } = "";
    /// <summary>Lớp cấu tạo từ ngoài vào trong</summary>
    public List<RvLayer> Layers { get; set; } = new();
}

public class RvLayer
{
    public string Function { get; set; } = "";
    public string Material { get; set; } = "";
    public double Thickness { get; set; }
}

public class RvOpening
{
    public string Id { get; set; } = "";
    public string HostId { get; set; } = "";
    /// <summary>"Door" | "Window"</summary>
    public string Category { get; set; } = "Door";
    public string TypeName { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Sill { get; set; }
    public bool Flipped { get; set; }
}

public class RvRoom
{
    public string Id { get; set; } = "";
    public string Level { get; set; } = "";
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";
    public List<double[]> Outline { get; set; } = new();
}

public class RvMep
{
    public string Id { get; set; } = "";
    /// <summary>Nhóm Revit: "Pipe", "PipeFitting", "PlumbingFixture", "Conduit", "LightingFixture", "ElectricalFixture", "Duct"…</summary>
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
    /// <summary>Ống / ống luồn: đoạn thẳng (X1..Z2) + đường kính (m); thiết bị: điểm X, Y, Z + hộp bao W×D×H</summary>
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double Z1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public double Z2 { get; set; }
    public double Diameter { get; set; }
    public double W { get; set; }
    public double D { get; set; }
    public double H { get; set; }
    public string SystemName { get; set; } = "";
}

public sealed class RevitUpdateReport
{
    public int Added, Updated, Removed, Unchanged;
    public List<string> Lines { get; } = new();
    public override string ToString() => $"Cập nhật từ Revit: thêm {Added}, sửa {Updated}, xoá {Removed}, giữ nguyên {Unchanged}";
}

public static class RevitArchImport
{
    /// <summary>Cách nhận ống / thiết bị điện nước (bộ môn Điện nước gán). null = tạo khối thiết bị chung (FurnitureItem bộ môn Điện nước).</summary>
    public static Action<Project, List<RvMep>, ImportReport, double, double> MepHandler;
    /// <summary>Nhập ống / thiết bị Revit thành ĐỐI TƯỢNG ĐIỆN NƯỚC (thiết bị theo thư viện MEP + tuyến ống) thay vì khối chung.
    /// Mặc định false (giữ cách cũ: khối thiết bị/ống bộ môn Điện nước); bật trong hộp thoại nhập Revit.</summary>
    public static bool UseMepHandler { get; set; }

    private static string StoryFor(Project p, Dictionary<string, double> lvElev, string level, double extra = 0)
    {
        double z = (lvElev.TryGetValue(level ?? "", out double e) ? e : p.GroundElevation) + extra;
        if (z < (p.Stories.Count == 0 ? double.MaxValue : p.Stories.Min(s => s.Elevation)) - 0.05) return "NEN";
        return p.Stories.Where(s => s.Elevation <= z + 0.05).OrderByDescending(s => s.Elevation).FirstOrDefault()?.Name ?? "NEN";
    }

    /// <summary>Gọi trong RevitJsonImporter.Build (sau phần kết cấu). ox, oy: gốc toạ độ đã dời.</summary>
    public static void Build(Project p, RevitExchange ex, ImportReport rep, double ox, double oy)
    {
        ex.Walls ??= new(); ex.Openings ??= new(); ex.Rooms ??= new(); ex.Mep ??= new();
        if (ex.Walls.Count + ex.Openings.Count + ex.Rooms.Count + ex.Mep.Count == 0) return;
        var lvElev = (ex.Levels ?? new()).Where(l => l != null).GroupBy(q => q.Name ?? "").ToDictionary(g => g.Key, g => g.First().Elevation);
        p.Walls.Clear();
        p.Arch ??= new ArchModel();
        p.Arch.Rooms.Clear();
        p.Arch.Furniture.RemoveAll(f => f.Discipline == Discipline.DienNuoc && f.ExternalId != null);
        int nw = AddWalls(p, ex.Walls.Where(w => w != null), lvElev, ox, oy);
        int no = AddOpenings(p, ex.Openings.Where(o => o != null), ox, oy);
        int nr = AddRooms(p, ex.Rooms.Where(r => r != null), lvElev, ox, oy);
        rep.Info.Add($"Kiến trúc từ Revit: {nw} tường, {no} cửa, {nr} phòng");
        var mep = ex.Mep.Where(m => m != null).ToList();
        if (mep.Count > 0)
        {
            if (MepHandler != null && UseMepHandler) MepHandler(p, mep, rep, ox, oy);
            else
            {
                int nm = AddMepBoxes(p, mep, lvElev, ox, oy);
                rep.Info.Add($"Điện nước từ Revit: {nm} đối tượng (khối thiết bị/ống – bộ môn Điện nước)");
            }
        }
    }

    private static string WallTypeFor(Project p, RvWall w)
    {
        double t = w.Layers?.Sum(l => Math.Max(0, l.Thickness)) ?? 0;
        if (!string.IsNullOrWhiteSpace(w.TypeName) && p.WallTypes.Any(x => x.Name == w.TypeName)) return w.TypeName;
        if (w.Layers != null && w.Layers.Count > 0 && !string.IsNullOrWhiteSpace(w.TypeName))
        {
            var layers = w.Layers.Select(l => new WallLayer
            {
                Function = MapFn(l.Function), Material = string.IsNullOrWhiteSpace(l.Material) ? "Gạch đặc" : l.Material, Thickness = l.Thickness,
                UnitWeight = WallMaterials.Find(p.WallMaterials, l.Material)?.UnitWeight ?? 18
            }).ToArray();
            var wt = WallAssembly.Make(w.TypeName, WallFunctionName.Interior, layers);
            p.WallTypes.Add(wt);
            return wt.Name;
        }
        return p.WallTypes.OrderBy(x => Math.Abs(x.TotalThickness - (t > 0 ? t : 0.22))).FirstOrDefault()?.Name ?? "";
    }

    private static string MapFn(string revitFn)
    {
        string f = (revitFn ?? "").ToLowerInvariant();
        if (f.Contains("structure") || f.Contains("kết cấu")) return WallLayerFn.Core;
        if (f.Contains("finish")) return WallLayerFn.Plaster;
        if (f.Contains("thermal") || f.Contains("insul")) return WallLayerFn.Insulation;
        return WallLayerFn.Finish;
    }

    private static int AddWalls(Project p, IEnumerable<RvWall> walls, Dictionary<string, double> lvElev, double ox, double oy)
    {
        int n = 0;
        foreach (var w in walls)
        {
            var wl = new WallLine
            {
                Id = p.Walls.Count == 0 ? 1 : p.Walls.Max(x => x.Id) + 1, Story = StoryFor(p, lvElev, w.Level, w.BaseOffset),
                X1 = Math.Round(w.X1 - ox, 4), Y1 = Math.Round(w.Y1 - oy, 4), X2 = Math.Round(w.X2 - ox, 4), Y2 = Math.Round(w.Y2 - oy, 4),
                Type = WallTypeFor(p, w), Height = Math.Round(Math.Max(0, w.Height), 3), ExternalId = w.Id
            };
            p.Walls.Add(wl); n++;
        }
        return n;
    }

    private static int AddOpenings(Project p, IEnumerable<RvOpening> ops, double ox, double oy)
    {
        int n = 0;
        foreach (var o in ops)
        {
            var host = p.Walls.FirstOrDefault(w => w.ExternalId == o.HostId);
            double x = o.X - ox, y = o.Y - oy;
            if (host == null)
            {
                double bd = 1e9;
                foreach (var w in p.Walls) { WallAssembly.ProjectPoint(w, x, y, out double d); if (d < bd) { bd = d; host = w; } }
                if (bd > 0.5) host = null;
            }
            if (host == null) continue;
            bool door = !o.Category.Contains("Window", StringComparison.OrdinalIgnoreCase);
            var kind = door ? WallOpeningKind.Door : WallOpeningKind.Window;
            var ot = p.OpeningTypes.FirstOrDefault(t => t.Kind == kind && Math.Abs(t.Width - o.Width) < 0.02 && Math.Abs(t.Height - o.Height) < 0.02 && Math.Abs(t.Sill - o.Sill) < 0.05);
            if (ot == null)
            {
                string tag = $"{(door ? "D" : "S")}{p.OpeningTypes.Count(t => t.Kind == kind) + 1}";
                ot = new OpeningType { Name = $"{tag} {Math.Round(o.Width * 1000)}x{Math.Round(o.Height * 1000)}", Kind = kind, Width = o.Width, Height = o.Height, Sill = door ? 0 : o.Sill, Operation = door ? "1 cánh" : "2 cánh" };
                p.OpeningTypes.Add(ot);
            }
            double along = WallAssembly.ProjectPoint(host, x, y, out _);
            var r = WallAssembly.PlaceOpening(p, host, ot.Name, along, o.Flipped);
            if (r.Ok) n++;
        }
        return n;
    }

    private static int AddRooms(Project p, IEnumerable<RvRoom> rooms, Dictionary<string, double> lvElev, double ox, double oy)
    {
        int n = 0;
        foreach (var r in rooms)
        {
            if (r.Outline == null || r.Outline.Count < 3) continue;
            p.Arch.Rooms.Add(new Room
            {
                Id = p.Arch.Rooms.Count == 0 ? 1 : p.Arch.Rooms.Max(x => x.Id) + 1, Story = StoryFor(p, lvElev, r.Level), Name = string.IsNullOrWhiteSpace(r.Name) ? "Phòng" : r.Name,
                Number = r.Number ?? "", Outline = r.Outline.Where(q => q != null && q.Length >= 2).Select(q => new[] { Math.Round(q[0] - ox, 4), Math.Round(q[1] - oy, 4) }).ToList()
            });
            n++;
        }
        return n;
    }

    /// <summary>Tên nhóm điện nước tiếng Việt theo nhóm Revit.</summary>
    public static string MepCategory(string revitCat)
    {
        string c = (revitCat ?? "").ToLowerInvariant();
        if (c.Contains("pipefitting") || c.Contains("fitting")) return "Phụ kiện ống";
        if (c.Contains("pipe")) return "Ống nước";
        if (c.Contains("plumbing")) return "Thiết bị vệ sinh";
        if (c.Contains("conduit")) return "Ống luồn dây điện";
        if (c.Contains("light")) return "Đèn";
        if (c.Contains("electrical") || c.Contains("socket") || c.Contains("outlet")) return "Thiết bị điện";
        if (c.Contains("duct")) return "Ống gió";
        return "Thiết bị điện nước";
    }

    internal static int AddMepBoxes(Project p, List<RvMep> mep, Dictionary<string, double> lvElev, double ox, double oy)
    {
        int n = 0;
        foreach (var m in mep)
        {
            string cat = MepCategory(m.Category);
            bool linear = Math.Abs(m.X2 - m.X1) + Math.Abs(m.Y2 - m.Y1) + Math.Abs(m.Z2 - m.Z1) > 1e-6 && m.Diameter > 0;
            double cx, cy, cz, w, d, h, rot = 0;
            if (linear)
            {
                cx = (m.X1 + m.X2) / 2 - ox; cy = (m.Y1 + m.Y2) / 2 - oy;
                double L = Math.Sqrt((m.X2 - m.X1) * (m.X2 - m.X1) + (m.Y2 - m.Y1) * (m.Y2 - m.Y1));
                rot = Math.Atan2(m.Y2 - m.Y1, m.X2 - m.X1) * 180 / Math.PI;
                w = Math.Max(L, m.Diameter); d = m.Diameter; h = Math.Max(m.Diameter, Math.Abs(m.Z2 - m.Z1));
                cz = Math.Min(m.Z1, m.Z2);
                if (L < 1e-6) { w = m.Diameter; }
            }
            else
            {
                cx = m.X1 - ox; cy = m.Y1 - oy; cz = m.Z1; w = Math.Max(0.05, m.W); d = Math.Max(0.05, m.D); h = Math.Max(0.05, m.H);
            }
            string story = p.Stories.Where(s => s.Elevation <= cz + 0.05).OrderByDescending(s => s.Elevation).FirstOrDefault()?.Name ?? "NEN";
            double z0 = ModelSolids.StoryZ(p, story);
            p.Arch.Furniture.Add(new FurnitureItem
            {
                Id = p.Arch.Furniture.Count == 0 ? 1 : p.Arch.Furniture.Max(f => f.Id) + 1, Story = story, Name = string.IsNullOrWhiteSpace(m.Name) ? cat : m.Name,
                Category = cat, X = Math.Round(cx, 4), Y = Math.Round(cy, 4), Rotation = Math.Round(rot, 2), Width = w, Depth = d, Height = h,
                Elevation = Math.Round(cz - z0, 4), Discipline = Discipline.DienNuoc, ExternalId = m.Id, LibraryId = "revit:" + m.Category
            });
            n++;
        }
        return n;
    }

    /// <summary>"Cập nhật từ Revit": so khớp theo UniqueId – tường, phòng, thiết bị điện nước: thêm mới, sửa vị trí / loại, xoá đối tượng
    /// không còn trong Revit. Kết cấu không đổi (nhập lại toàn bộ bằng Nhập từ Revit nếu cần).</summary>
    public static RevitUpdateReport Update(Project p, RevitExchange ex, bool moveToOrigin = true)
    {
        var rep = new RevitUpdateReport();
        ex.Walls ??= new(); ex.Openings ??= new(); ex.Rooms ??= new(); ex.Mep ??= new();
        p.Arch ??= new ArchModel();
        double ox = 0, oy = 0;
        if (moveToOrigin && ex.Grids != null && ex.Grids.Count > 0)
        {
            var vx = ex.Grids.Where(g => g != null && Math.Abs(g.X1 - g.X2) < 1e-3).Select(g => g.X1).ToList();
            var hy = ex.Grids.Where(g => g != null && Math.Abs(g.Y1 - g.Y2) < 1e-3).Select(g => g.Y1).ToList();
            if (vx.Count > 0) ox = Math.Round(vx.Min(), 4);
            if (hy.Count > 0) oy = Math.Round(hy.Min(), 4);
        }
        var lvElev = (ex.Levels ?? new()).Where(l => l != null).GroupBy(q => q.Name ?? "").ToDictionary(g => g.Key, g => g.First().Elevation);
        // tường
        var incoming = ex.Walls.Where(w => w != null && !string.IsNullOrEmpty(w.Id)).ToDictionary(w => w.Id);
        foreach (var w in p.Walls.Where(w => w.ExternalId != null && !incoming.ContainsKey(w.ExternalId)).ToList())
        {
            p.Walls.Remove(w); rep.Removed++; rep.Lines.Add($"Xoá tường {w.Id} (không còn trong Revit)");
        }
        foreach (var rv in incoming.Values)
        {
            var cur = p.Walls.FirstOrDefault(w => w.ExternalId == rv.Id);
            double x1 = Math.Round(rv.X1 - ox, 4), y1 = Math.Round(rv.Y1 - oy, 4), x2 = Math.Round(rv.X2 - ox, 4), y2 = Math.Round(rv.Y2 - oy, 4);
            string type = WallTypeFor(p, rv);
            if (cur == null) { AddWalls(p, new[] { rv }, lvElev, ox, oy); rep.Added++; rep.Lines.Add($"Thêm tường {rv.Id}"); continue; }
            bool changed = Math.Abs(cur.X1 - x1) > 1e-3 || Math.Abs(cur.Y1 - y1) > 1e-3 || Math.Abs(cur.X2 - x2) > 1e-3 || Math.Abs(cur.Y2 - y2) > 1e-3
                           || cur.Type != type || Math.Abs(cur.Height - Math.Round(rv.Height, 3)) > 1e-3;
            if (!changed) { rep.Unchanged++; continue; }
            cur.X1 = x1; cur.Y1 = y1; cur.X2 = x2; cur.Y2 = y2; cur.Type = type; cur.Height = Math.Round(rv.Height, 3);
            // cửa trên tường: chặn lại trong chiều dài mới
            foreach (var o in cur.Openings)
            {
                var ot = WallAssembly.TypeOf(p, o);
                if (ot != null) o.Offset = WallAssembly.ClampOffset(cur, ot.Width, o.Offset);
            }
            rep.Updated++; rep.Lines.Add($"Sửa tường {cur.Id} theo Revit");
        }
        // thiết bị điện nước
        var mepIn = ex.Mep.Where(m => m != null && !string.IsNullOrEmpty(m.Id)).ToDictionary(m => m.Id);
        int rm = p.Arch.Furniture.RemoveAll(f => f.ExternalId != null && f.Discipline == Discipline.DienNuoc && !mepIn.ContainsKey(f.ExternalId));
        rep.Removed += rm;
        var have = p.Arch.Furniture.Where(f => f.ExternalId != null).Select(f => f.ExternalId).ToHashSet();
        if (p.Mep != null)
        {
            // đối tượng điện nước đã nhập dạng thiết bị / tuyến MEP (UseMepHandler): xoá phần không còn trong Revit, giữ phần còn lại
            int rr = p.Mep.Runs.RemoveAll(r => r.ExternalId != null && !mepIn.ContainsKey(r.ExternalId));
            int rd = p.Mep.Devices.RemoveAll(d => d.ExternalId != null && !d.ExternalId.StartsWith("rv-node:") && !mepIn.ContainsKey(d.ExternalId));
            var used = p.Mep.Runs.SelectMany(r => new[] { r.FromId, r.ToId }).ToHashSet();
            p.Mep.Devices.RemoveAll(d => d.ExternalId != null && d.ExternalId.StartsWith("rv-node:") && !used.Contains(d.Id));
            rep.Removed += rr + rd;
            foreach (var r in p.Mep.Runs) if (r.ExternalId != null) have.Add(r.ExternalId);
            foreach (var d in p.Mep.Devices) if (d.ExternalId != null) have.Add(d.ExternalId);
        }
        var newMep = mepIn.Values.Where(m => !have.Contains(m.Id)).ToList();
        if (newMep.Count > 0)
        {
            if (MepHandler != null && UseMepHandler) { MepHandler(p, newMep, new ImportReport(), ox, oy); rep.Added += newMep.Count; }
            else rep.Added += AddMepBoxes(p, newMep, lvElev, ox, oy);
        }
        rep.Unchanged += mepIn.Count - newMep.Count;
        return rep;
    }
}
