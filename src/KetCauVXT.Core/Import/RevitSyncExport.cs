using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using KetCauVXT.Model;

namespace KetCauVXT.Import;

/// <summary>Liên kết với mô hình Revit nguồn: gốc toạ độ đã dời khi nhập và cao độ Revit của mặt sàn tầng 1 (z = 0 của Việt Const).
/// Dùng để xuất ngược về Revit đúng vị trí (Import/RevitSyncExport.cs).</summary>
public class RevitLink
{
    /// <summary>Toạ độ Revit (m) của gốc (0; 0) trong Việt Const</summary>
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    /// <summary>Cao độ Revit (m) ứng với z = 0 của Việt Const</summary>
    public double GroundZ { get; set; }
    /// <summary>Tên file *.vxtrevit.json đã nhập (tham khảo)</summary>
    public string SourceFile { get; set; } = "";
}

// =====================================================================
// VIỆT CONST → REVIT: CHUẨN + KẾT CẤU (*.vxt2revit.json, schema vxt2revit-1)
// Add-in Revit (KetCauVXT.RevitExport) đọc file này để:
//   • Chuẩn hoá theo Việt Const: tên cao độ, trục, đơn vị, vật liệu, loại tường / cột / dầm / sàn, thông tin dự án, khung tên, nét in.
//   • Dựng kết cấu từ Việt Const: cột, dầm, sàn kết cấu theo phương án Việt Const (kiến trúc → kết cấu).
//   • Nhận tiết diện từ Việt Const: đổi loại cột, dầm, sàn trong Revit theo tiết diện sau tối ưu.
// Toạ độ, cao độ ghi theo hệ Revit (m) = toạ độ Việt Const + RevitLink (gốc đã dời khi nhập). Mã liên kết: "C{id}", "B{id}", "S{id}".
// =====================================================================
public static class RevitSyncExport
{
    public const string Schema = "vxt2revit-1";

    /// <summary>Nét in mặc định (số bút Revit 1–16) theo chuẩn bản vẽ Việt Const: cấu kiện bị cắt đậm, hình chiếu mảnh.</summary>
    public static readonly (string Category, int Cut, int Projection)[] DefaultLineWeights =
    {
        ("Structural Columns", 6, 2), ("Structural Framing", 5, 2), ("Floors", 4, 1), ("Walls", 5, 1),
        ("Structural Foundations", 5, 2), ("Stairs", 3, 1), ("Doors", 2, 1), ("Windows", 2, 1), ("Grids", 1, 1), ("Levels", 1, 1),
    };

    public static string ToJson(Project p) => Build(p).ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    public static JsonObject Build(Project p)
    {
        var link = p.RevitLink ?? new RevitLink();
        double ox = link.OriginX, oy = link.OriginY, oz = link.GroundZ;
        static double R(double v) => Math.Round(v, 4);
        var root = new JsonObject
        {
            ["Schema"] = Schema,
            ["Units"] = "mm",
            ["Project"] = new JsonObject
            {
                ["Name"] = p.Name ?? "", ["Owner"] = p.Owner ?? "", ["Location"] = p.Location ?? "", ["Designer"] = p.Designer ?? "", ["Grade"] = p.BuildingGrade ?? "",
            },
            ["Link"] = new JsonObject { ["OriginX"] = R(ox), ["OriginY"] = R(oy), ["GroundZ"] = R(oz) },
        };

        // ---- cao độ: mặt sàn tầng 1 + các tầng ----
        var levels = new JsonArray();
        var stories = p.Stories.OrderBy(s => s.Elevation).ToList();
        if (!stories.Any(s => Math.Abs(s.Elevation) < 0.005)) levels.Add(new JsonObject { ["Name"] = "Tầng 1", ["Elevation"] = R(oz) });
        foreach (var s in stories) levels.Add(new JsonObject { ["Name"] = s.Name, ["Elevation"] = R(s.Elevation + oz) });
        root["Levels"] = levels;

        // ---- trục ----
        var grids = new JsonArray();
        foreach (var g in p.Grids) grids.Add(new JsonObject { ["Name"] = g.Name, ["Dir"] = g.Dir.ToString(), ["Coord"] = R(g.Coord + (g.Dir == GridDir.X ? ox : oy)) });
        root["Grids"] = grids;

        // ---- vật liệu ----
        var mats = new JsonArray();
        foreach (var m in p.Materials) mats.Add(new JsonObject { ["Name"] = MaterialName(m), ["Code"] = m.Name, ["Kind"] = m.Kind.ToString(), ["UnitWeight"] = R(m.UnitWeight) });
        foreach (var name in new[] { "Vữa xi măng", "Gạch đặc", "Gạch rỗng", "Bê tông nhẹ AAC" })
            if (!mats.Any(x => (string)x["Name"] == name))
                mats.Add(new JsonObject { ["Name"] = name, ["Code"] = name, ["Kind"] = "Masonry", ["UnitWeight"] = name == "Gạch rỗng" ? 15 : name == "Bê tông nhẹ AAC" ? 8 : 18 });
        root["Materials"] = mats;

        // ---- loại tường ----
        var wts = new JsonArray();
        foreach (var w in p.WallTypes)
        {
            var layers = new JsonArray();
            if (w.IsLayered)
                foreach (var l in w.Layers) layers.Add(new JsonObject { ["Function"] = l.IsCore ? "Structure" : "Finish", ["Material"] = l.Material, ["Thickness"] = R(l.Thickness) });
            else
            {
                string core = w.UnitWeight <= 9 ? "Bê tông nhẹ AAC" : w.UnitWeight <= 16 ? "Gạch rỗng" : "Gạch đặc";
                double coreT = Math.Max(0.05, w.Thickness - w.PlasterSides * w.PlasterThickness);
                if (w.PlasterSides >= 1) layers.Add(new JsonObject { ["Function"] = "Finish", ["Material"] = "Vữa xi măng", ["Thickness"] = R(w.PlasterThickness) });
                layers.Add(new JsonObject { ["Function"] = "Structure", ["Material"] = core, ["Thickness"] = R(coreT) });
                if (w.PlasterSides >= 2) layers.Add(new JsonObject { ["Function"] = "Finish", ["Material"] = "Vữa xi măng", ["Thickness"] = R(w.PlasterThickness) });
            }
            wts.Add(new JsonObject { ["Name"] = w.Name, ["Thickness"] = R(w.Thickness), ["WeightPerM2"] = R(w.WeightPerM2), ["Layers"] = layers });
        }
        root["WallTypes"] = wts;

        // ---- loại cột / dầm / sàn đang dùng ----
        var colT = new JsonArray(); var beamT = new JsonArray(); var slabT = new JsonArray();
        var usedCol = p.Frames.Where(f => f.Kind == FrameKind.Column).Select(f => f.Section).ToHashSet();
        var usedBeam = p.Frames.Where(f => f.Kind != FrameKind.Column).Select(f => f.Section).ToHashSet();
        foreach (var s in p.FrameSections.Where(s => s.Shape == SectionShape.RectConcrete))
        {
            var o = new JsonObject { ["Name"] = s.Name, ["B"] = R(s.B), ["H"] = R(s.H), ["Material"] = MaterialName(p, string.IsNullOrEmpty(s.Material) ? p.Concrete.ConcreteBeam : s.Material) };
            if (usedCol.Contains(s.Name)) colT.Add(o);
            else if (usedBeam.Contains(s.Name)) beamT.Add(o);
        }
        foreach (var s in p.SlabSections)
            slabT.Add(new JsonObject { ["Name"] = s.Name, ["Thickness"] = R(s.Thickness), ["Material"] = MaterialName(p, s.Material) });
        root["ColumnTypes"] = colT; root["BeamTypes"] = beamT; root["SlabTypes"] = slabT;

        // ---- cấu kiện ----
        var cols = new JsonArray(); var beams = new JsonArray(); var slabs = new JsonArray();
        foreach (var f in p.Frames)
        {
            var sec = p.Section(f.Section);
            if (sec == null || sec.Shape != SectionShape.RectConcrete) continue;
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            if (a == null || b == null) continue;
            if (f.Kind == FrameKind.Column)
            {
                var lo = a.Z <= b.Z ? a : b; var hi = a.Z <= b.Z ? b : a;
                cols.Add(new JsonObject
                {
                    ["Id"] = "C" + f.Id, ["Label"] = string.IsNullOrEmpty(f.Mark) ? f.Label : f.Mark,
                    ["X"] = R(lo.X + f.ColOffsetX + ox), ["Y"] = R(lo.Y + f.ColOffsetY + oy), ["ZBot"] = R(lo.Z + oz), ["ZTop"] = R(hi.Z + oz),
                    ["Section"] = f.Section, ["B"] = R(sec.B), ["H"] = R(sec.H), ["Angle"] = R(f.Angle),
                });
            }
            else if (Math.Abs(a.Z - b.Z) < 0.01)
            {
                beams.Add(new JsonObject
                {
                    ["Id"] = "B" + f.Id, ["Label"] = string.IsNullOrEmpty(f.Mark) ? f.Label : f.Mark,
                    ["X1"] = R(a.X + ox), ["Y1"] = R(a.Y + oy), ["X2"] = R(b.X + ox), ["Y2"] = R(b.Y + oy), ["Z"] = R(a.Z + oz),
                    ["Section"] = f.Section, ["B"] = R(sec.B), ["H"] = R(sec.H),
                });
            }
        }
        foreach (var s in p.Slabs)
        {
            var nodes = s.Nodes.Select(p.Node).Where(n => n != null).ToList();
            if (nodes.Count < 3) continue;
            var ss = p.SlabSections.FirstOrDefault(q => q.Name == s.Section);
            var outline = new JsonArray();
            foreach (var n in nodes) outline.Add(new JsonArray(R(n.X + ox), R(n.Y + oy)));
            slabs.Add(new JsonObject
            {
                ["Id"] = "S" + s.Id, ["Label"] = s.Label, ["Z"] = R(nodes.Average(n => n.Z) + s.LevelOffset + oz),
                ["Section"] = s.Section, ["Thickness"] = R(ss?.Thickness ?? 0.1), ["Outline"] = outline,
            });
        }
        root["Columns"] = cols; root["Beams"] = beams; root["Slabs"] = slabs;

        // ---- dàn trang, khung tên, nét in ----
        root["Sheet"] = new JsonObject
        {
            ["PrefixStructure"] = "KC", ["PrefixArchitecture"] = "KT", ["Paper"] = "A3", ["PlanScale"] = 100, ["DetailScale"] = 25,
            ["TitleFields"] = new JsonObject
            {
                ["Tên công trình"] = p.Name ?? "", ["Chủ đầu tư"] = p.Owner ?? "", ["Địa điểm"] = p.Location ?? "",
                ["Thiết kế"] = p.Designer ?? "", ["Cấp công trình"] = p.BuildingGrade ?? "",
            },
        };
        var lw = new JsonArray();
        foreach (var (c, cut, pr) in DefaultLineWeights) lw.Add(new JsonObject { ["Category"] = c, ["Cut"] = cut, ["Projection"] = pr });
        root["LineWeights"] = lw;
        return root;
    }

    /// <summary>Tên vật liệu hiển thị trong Revit: "Bê tông B25", "Thép CB400-V"...</summary>
    public static string MaterialName(Material m) => m.Kind switch
    {
        MaterialKind.Concrete => m.Name.StartsWith("Bê tông", StringComparison.OrdinalIgnoreCase) ? m.Name : "Bê tông " + m.Name,
        MaterialKind.Rebar => m.Name.StartsWith("Thép", StringComparison.OrdinalIgnoreCase) ? m.Name : "Thép " + m.Name,
        _ => m.Name,
    };

    public static string MaterialName(Project p, string code)
    {
        var m = p.Materials.FirstOrDefault(x => x.Name == code);
        return m != null ? MaterialName(m) : (string.IsNullOrEmpty(code) ? "Bê tông B25" : (code.StartsWith("B", StringComparison.Ordinal) && code.Length <= 4 ? "Bê tông " + code : code));
    }
}
