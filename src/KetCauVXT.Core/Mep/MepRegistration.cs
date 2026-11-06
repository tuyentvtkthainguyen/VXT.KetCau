using System.Runtime.CompilerServices;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Mep;

/// <summary>Đăng ký bộ môn điện nước vào các điểm móc chung khi nạp thư viện Core:
/// khối 3D (ModelSolids.Providers – dùng cho 3D, IFC/OBJ, kiểm tra va chạm) và bản vẽ bộ môn (DisciplineDocs.MepSheets, tiền tố DN-).</summary>
public static class MepRegistration
{
    private static bool _done;

    [ModuleInitializer]
    internal static void Init() => Register();

    public static void Register()
    {
        if (_done) return;
        _done = true;
        ModelSolids.Providers.Add(Solids);
        DisciplineDocs.MepSheets = Sheets;
        ClashDetector.SleeveRule = SleeveAllowed;
    }

    /// <summary>Ống lồng qua dầm có hợp lệ? (quy tắc lỗ xuyên dầm – StructuralImpact). Ống đứng xuyên dầm: không.</summary>
    public static bool SleeveAllowed(Project p, Solid a, Solid b, Vec3 at)
    {
        var pipe = a.Id.StartsWith("DN-ONG-") ? a : b.Id.StartsWith("DN-ONG-") ? b : null;
        var beam = a.Id.StartsWith("DAM-") ? a : b.Id.StartsWith("DAM-") ? b : null;
        if (pipe == null || beam == null) return true;
        if (!int.TryParse(beam.Id.Substring(4), out int fid)) return true;
        var parts = pipe.Id.Split('-');
        if (parts.Length < 3 || !int.TryParse(parts[2], out int rid)) return true;
        var f = p.Frames.FirstOrDefault(x => x.Id == fid);
        var r = p.Mep?.Run(rid);
        if (f == null || r == null) return true;
        if (Math.Abs(pipe.ZDir.Z) > 0.9) return false;   // ống đứng xuyên dầm
        var ev = StructuralImpact.EvaluateBeamCrossing(p, f, at.X, at.Y, at.Z, StructuralImpact.RunDia(r));
        return ev == null || ev.Severity < ImpactSeverity.NguyHiem;
    }

    private static double[] Rgb(string hex)
    {
        try
        {
            var h = hex.TrimStart('#');
            return new[] { Convert.ToInt32(h[..2], 16) / 255.0, Convert.ToInt32(h.Substring(2, 2), 16) / 255.0, Convert.ToInt32(h.Substring(4, 2), 16) / 255.0 };
        }
        catch { return new[] { 0.5, 0.5, 0.5 }; }
    }

    /// <summary>Khối 3D điện nước: thiết bị (IfcFlowTerminal) + đoạn ống / ống gen (IfcFlowSegment, cho phép ống lồng).</summary>
    public static IEnumerable<Solid> Solids(Project p)
    {
        var m = p.Mep;
        if (m == null || m.IsEmpty) yield break;
        foreach (var b in MepSolids.Boxes(p))
        {
            var d = m.Device(b.DeviceId);
            yield return new Solid
            {
                Id = "DN-TB-" + b.DeviceId, Name = string.IsNullOrWhiteSpace(d?.Mark) ? b.Name : d.Mark + " " + b.Name,
                Discipline = Discipline.DienNuoc, Category = "Thiết bị", IfcType = "IfcFlowTerminal", Story = d?.Story ?? "",
                Origin = new Vec3(b.Cx, b.Cy, b.Cz - b.H / 2), XDir = new Vec3(b.Ux, b.Uy, 0).Normalize(), ZDir = Vec3.UnitZ,
                Profile = Solid.Rect(-b.W / 2, -b.D / 2, b.W / 2, b.D / 2), Depth = b.H, Color = Rgb(b.Color), AllowSleeve = true
            };
        }
        int k = 0;
        foreach (var s in MepSolids.Segments(p))
        {
            var a = new Vec3(s.X1, s.Y1, s.Z1); var c = new Vec3(s.X2, s.Y2, s.Z2);
            var dir = c - a; double L = dir.Length;
            if (L < 1e-6) continue;
            var z = dir.Normalize();
            var x = Math.Abs(z.Z) > 0.9 ? Vec3.UnitX : Vec3.UnitZ.Cross(z).Normalize();
            double r = Math.Max(0.005, s.Dia / 2);
            var run = m.Run(s.RunId);
            yield return new Solid
            {
                Id = $"DN-ONG-{s.RunId}-{++k}", Name = run != null ? MepDrawings.RunLabel(run) : "Ống",
                Discipline = Discipline.DienNuoc, Category = "Ống", IfcType = "IfcFlowSegment", Story = run?.Story ?? "",
                Origin = a, XDir = x, ZDir = z, Profile = Solid.Rect(-r, -r, r, r), Depth = L, Color = Rgb(s.Color), AllowSleeve = true
            };
        }
    }

    private static string Slug(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char ch in (s ?? "").Normalize(System.Text.NormalizationForm.FormD))
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            char c = ch == 'đ' ? 'd' : ch == 'Đ' ? 'D' : ch;
            sb.Append(char.IsLetterOrDigit(c) && c < 128 ? c : '_');
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
    }

    /// <summary>Các tờ bộ môn điện nước (chưa đánh số – DisciplineDocs đánh DN-01…). Chưa có mô hình điện nước → tờ liệt kê cũ.</summary>
    public static List<DrawingItem> Sheets(Project p, DisciplineDocOptions o)
    {
        if (p.Mep == null || p.Mep.IsEmpty) return DisciplineDocs.MepPlaceholder(p, o);
        MepRouting.RebuildAll(p);
        var items = MepDrawings.Figures(p).Select(f => new DrawingItem
        {
            Key = "DN:" + f.Name, FileName = Slug(f.Name) + ".dxf", Title = f.Name,
            Scale = f.Name == MepQuantities.Title ? 1 : MepDrawings.Scale, W = f.W
        }).ToList();
        // chi tiết gia cường kết cấu tại vị trí xuyên dầm / lỗ sàn
        foreach (var f in StructuralImpact.Figures(StructuralImpact.Scan(p)))
            items.Add(new DrawingItem { Key = "DN:" + f.Name, FileName = Slug(f.Name) + ".dxf", Title = f.Name, Scale = 20, W = f.W });
        return items;
    }
}
