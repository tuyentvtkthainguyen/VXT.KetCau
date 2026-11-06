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
        KetCauVXT.Import.RevitArchImport.MepHandler = MepRevitImport.Handle;   // dùng khi bật RevitArchImport.UseMepHandler
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
        // ---- điều hoà – thông gió: ống gió (tiết diện thật), dàn lạnh / nóng, miệng gió, ống đồng / nước ngưng ----
        var hv = m.Hvac;
        if (hv == null) yield break;
        foreach (var d in hv.Ducts)
            for (int i = 1; i < d.Path.Count; i++)
            {
                var a = new Vec3(d.Path[i - 1].X, d.Path[i - 1].Y, d.Path[i - 1].Z); var c = new Vec3(d.Path[i].X, d.Path[i].Y, d.Path[i].Z);
                var dir = c - a; double L = dir.Length;
                if (L < 1e-6) continue;
                var z = dir.Normalize();
                var x = Math.Abs(z.Z) > 0.9 ? Vec3.UnitX : Vec3.UnitZ.Cross(z).Normalize();
                double hw = d.Width / 2000, hh = d.Depth / 2000;
                yield return new Solid
                {
                    Id = $"DN-GIO-{d.Id}-{i}", Name = "Ống gió " + d.SizeText, Discipline = Discipline.DienNuoc, Category = "Ống gió", IfcType = "IfcFlowSegment", Story = d.Story,
                    Origin = a, XDir = x, ZDir = z, Profile = Solid.Rect(-hw, -hh, hw, hh), Depth = L, Color = Rgb(HvacSizing.SystemColor(d.System)), AllowSleeve = true
                };
            }
        foreach (var u in hv.Units)
        {
            double a0 = u.Angle * Math.PI / 180, zc = HvacRouting.UnitZ(p, u);
            yield return new Solid
            {
                Id = "DN-DH-" + u.Id, Name = u.Name, Discipline = Discipline.DienNuoc, Category = "Thiết bị điều hoà", IfcType = "IfcEnergyConversionDevice", Story = u.Story,
                Origin = new Vec3(u.X, u.Y, zc - u.Hh / 2), XDir = new Vec3(Math.Cos(a0), Math.Sin(a0), 0), ZDir = Vec3.UnitZ,
                Profile = Solid.Rect(-u.W / 2, -u.D / 2, u.W / 2, u.D / 2), Depth = Math.Max(0.05, u.Hh), Color = Rgb(u.Kind == HvacUnitKind.DanLanh ? "#E0F7FA" : "#B0BEC5"), AllowSleeve = true
            };
        }
        // phụ kiện ống gió (cút / tê / côn) – khối hộp nhỏ tại điểm nối (IfcFlowFitting)
        List<HvacFitting> fits;
        try { fits = HvacRouting.RebuildFittings(p); } catch (Exception) { fits = new List<HvacFitting>(); }
        for (int fi = 0; fi < fits.Count; fi++)
        {
            var f = fits[fi]; var dd = hv.Duct(f.DuctId);
            if (dd == null) continue;
            double hw = dd.Width / 2000 + 0.03, hh = dd.Depth / 2000 + 0.03;
            yield return new Solid
            {
                Id = $"DN-PKG-{f.DuctId}-{fi}", Name = f.Name, Discipline = Discipline.DienNuoc, Category = "Phụ kiện ống gió", IfcType = "IfcFlowFitting", Story = dd.Story,
                Origin = new Vec3(f.X, f.Y, f.Z - hh), XDir = Vec3.UnitX, ZDir = Vec3.UnitZ,
                Profile = Solid.Rect(-hw, -hw, hw, hw), Depth = 2 * hh, Color = Rgb(HvacSizing.SystemColor(dd.System)), AllowSleeve = true
            };
        }
        foreach (var t in hv.Terminals)
        {
            double zc = MepRouting.StoryElevation(p, t.Story) + t.Height;
            yield return new Solid
            {
                Id = "DN-MG-" + t.Id, Name = t.Name, Discipline = Discipline.DienNuoc, Category = "Miệng gió", IfcType = "IfcFlowTerminal", Story = t.Story,
                Origin = new Vec3(t.X, t.Y, zc), XDir = Vec3.UnitX, ZDir = Vec3.UnitZ,
                Profile = Solid.Rect(-t.W / 2000, -t.H / 2000, t.W / 2000, t.H / 2000), Depth = 0.05, Color = Rgb(t.Supply ? "#90CAF9" : "#A5D6A7"), AllowSleeve = true
            };
        }
        foreach (var q in hv.Pipes)
            for (int i = 1; i < q.Path.Count; i++)
            {
                var a = new Vec3(q.Path[i - 1].X, q.Path[i - 1].Y, q.Path[i - 1].Z); var c = new Vec3(q.Path[i].X, q.Path[i].Y, q.Path[i].Z);
                var dir = c - a; double L = dir.Length;
                if (L < 1e-6) continue;
                var z = dir.Normalize();
                var x = Math.Abs(z.Z) > 0.9 ? Vec3.UnitX : Vec3.UnitZ.Cross(z).Normalize();
                double r = (q.Kind == HvacPipeKind.OngDong ? q.GasDia + 20 : q.Dia) / 2000;
                yield return new Solid
                {
                    Id = $"DN-DHO-{q.Id}-{i}", Name = (q.Kind == HvacPipeKind.OngDong ? "Ống đồng " : "Ống nước ngưng ") + q.SizeText, Discipline = Discipline.DienNuoc,
                    Category = q.Kind == HvacPipeKind.OngDong ? "Ống đồng" : "Ống nước ngưng", IfcType = "IfcFlowSegment", Story = q.Story,
                    Origin = a, XDir = x, ZDir = z, Profile = Solid.Rect(-r, -r, r, r), Depth = L, Color = Rgb(q.Kind == HvacPipeKind.OngDong ? "#B87333" : "#78909C"), AllowSleeve = true
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
            Scale = f.Name == MepQuantities.Title || MepRound4.IsSchematic(f.Name) ? 1 : MepDrawings.Scale, W = f.W
        }).ToList();
        // chi tiết gia cường kết cấu tại vị trí xuyên dầm / lỗ sàn
        foreach (var f in StructuralImpact.Figures(StructuralImpact.Scan(p)))
            items.Add(new DrawingItem { Key = "DN:" + f.Name, FileName = Slug(f.Name) + ".dxf", Title = f.Name, Scale = 20, W = f.W });
        return items;
    }
}
