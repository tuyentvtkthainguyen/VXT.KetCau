using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm tra module dàn trang: bộ tờ mặc định, xuất DXF từng tờ, khung nhìn nằm trong khổ giấy.</summary>
public static class SheetTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var p = ModelBuilder.NewProject("Dàn trang mẫu");
        p.Location = "Thái Nguyên";
        p.Designer = "Vũ Xuân Tuyến";
        ModelBuilder.SetGrid(p, new[] { 5.0 }, new[] { 4.5, 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
        {
            ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5
        });
        var r = FrameAnalysis.Run(p);
        var d = ConcreteDesigner.Design(p, r);

        var sheets = SheetBuilder.DefaultSet(p, d, r);
        p.Sheets = sheets;
        check("Dàn trang: số tờ mặc định ≥ 4", sheets.Count >= 4 ? 1 : 0, 1, 0);
        check("Dàn trang: mọi tờ có khung nhìn", sheets.All(s => s.Views.Count > 0) ? 1 : 0, 1, 0);
        int outside = sheets.Sum(s => s.Views.Count(v => !SheetBuilder.InsidePaper(s, v)));
        check("Dàn trang: khung nhìn nằm trong khổ giấy", outside, 0, 0);
        int overflow = 0;
        foreach (var s in sheets)
            foreach (var v in s.Views)
                if (SheetBuilder.Layout(SheetBuilder.Figures(p, r, d, v), v).Overflow) overflow++;
        check("Dàn trang: nội dung không tràn khung nhìn", overflow, 0, 0);

        string dir = Path.Combine(outDir, "BanVeDanTrang");
        if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir, "*.dxf")) File.Delete(f);
        var files = SheetBuilder.ExportAllDxf(p, r, d, dir);
        check("Dàn trang: số file DXF = số tờ", files.Count, sheets.Count, 0);
        check("Dàn trang: file DXF tồn tại, không rỗng", files.All(f => File.Exists(f) && new FileInfo(f).Length > 1000) ? 1 : 0, 1, 0);
        bool layers = files.All(f =>
        {
            string t = File.ReadAllText(f);
            return t.Contains("KHUNG_TEN") && t.Contains("EOF") && !t.Any(ch => ch > 127);
        });
        check("Dàn trang: DXF có khung tên, chữ mã hoá \\U+XXXX", layers ? 1 : 0, 1, 0);

        // khung nhìn tỷ lệ cố định: hệ số phóng = tỷ lệ gốc / tỷ lệ khung
        var plan = sheets.SelectMany(s => s.Views).FirstOrDefault(v => v.Kind == SheetViewKind.PlanStory);
        if (plan != null)
        {
            var v2 = SheetBuilder.Clone(plan);
            v2.Scale = 200;
            var lay = SheetBuilder.Layout(SheetBuilder.Figures(p, r, d, v2), v2);
            check("Dàn trang: mặt bằng 1/100 đặt ở 1/200 → hệ số 0,5", lay.Zoom, 0.5, 1e-9);
            check("Dàn trang: tỷ lệ thực của khung = 1/200", lay.EffectiveScale, 200, 1e-9);
        }
        // biểu đồ nội lực và mặt đứng
        var g = p.GridsX.First();
        var dia = ElevationDrawing.Build(p, r, g, "M3", null);
        check("Dàn trang: biểu đồ M3 khung trục có nét vẽ", dia.EntityCount > 20 ? 1 : 0, 1, 0);
        var prims = dia.GetPrimitives();
        check("Dàn trang: đọc lại nét vẽ cơ bản = số thực thể", prims.Count, dia.EntityCount, 0);
        check("Dàn trang: giải mã chữ tiếng Việt", DxfWriter.Decode(DxfWriter.Encode("Mặt đứng Ø16")) == "Mặt đứng Ø16" ? 1 : 0, 1, 0);
        Console.WriteLine($"   · {sheets.Count} tờ: " + string.Join(", ", sheets.Select(s => $"{s.Number} {s.Paper} ({s.Views.Count})")));
    }
}
