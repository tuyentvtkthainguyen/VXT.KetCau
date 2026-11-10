using System.Text.RegularExpressions;
using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Import.Acad;
using KetCauVXT.Import.Arch;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>[AutoCAD hai chiều] Đọc cao độ mặt đứng → tầng; kết quả thiết kế về toạ độ bản vẽ gốc; bản vẽ chi tiết rời không khung tên.</summary>
public static class AcadRoundTripTests
{
    private static double B(bool x) => x ? 1 : 0;

    private static int LayerCount(string dxf, string layer) => Regex.Matches(dxf, @"\n\s*8\r?\n" + Regex.Escape(layer) + @"\r?\n").Count;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---- cao độ ----
        double z;
        check("Cao độ: +3.600", ElevationReader.TryParse("+3.600", out z) ? z : -99, 3.6, 1e-9);
        check("Cao độ: ±0.000", ElevationReader.TryParse("±0.000", out z) ? z : -99, 0, 1e-9);
        check("Cao độ: -0.450", ElevationReader.TryParse("-0.450", out z) ? z : -99, -0.45, 1e-9);
        check("Cao độ: COS +7,200", ElevationReader.TryParse("COS +7,200", out z) ? z : -99, 7.2, 1e-9);
        check("Cao độ: +10500 (mm)", ElevationReader.TryParse("+10500", out z) ? z : -99, 10.5, 1e-9);
        check("Kích thước 3600 (không dấu) không phải cao độ", B(ElevationReader.TryParse("3600", out _)), 0, 0);
        var texts = new List<(string, double, double, double)>
        {
            ("±0.000", 0, 0, 0.25), ("TẦNG 1", 3, 0.05, 0.3), ("+3.900", 0, 3.9, 0.25), ("TẦNG 2", 3, 3.95, 0.3),
            ("+7.200", 0, 7.2, 0.25), ("TẦNG 3", 3, 7.2, 0.3), ("+10.500", 0, 10.5, 0.25), ("MÁI", 3, 10.5, 0.3),
            ("+11.400", 0, 11.4, 0.25), ("-0.450", 0, -0.45, 0.25), ("3900", 5, 2, 0.25)
        };
        var lv = ElevationReader.FromTexts(texts);
        var st = ElevationReader.Stories(lv);
        Console.WriteLine("   · Mặt đứng: " + string.Join("; ", st.Select(s => $"{s.Name} {s.Elevation:+0.000;-0.000} cao {s.Height:0.00}")));
        check("Mặt đứng: 6 cao độ khác nhau", lv.Count, 6, 0);
        check("Mặt đứng: 3 tầng (bỏ tum +11.4 cách mái < 2,2 m; gốc ±0.000)", st.Count, 3, 0);
        check("Mặt đứng: chiều cao tầng 3,9 / 3,3 / 3,3", st[0].Height * 100 + st[1].Height * 10 + st[2].Height, 390 + 33 + 3.3, 1e-6);
        check("Mặt đứng: tên tầng đọc cạnh cao độ", B(st[0].Name == "TẦNG 1" && st[1].Name == "TẦNG 2"), 1, 0);

        // ---- mô hình + thiết kế ----
        var p = ModelBuilder.NewProject("AutoCAD hai chiều");
        ModelBuilder.SetGrid(p, new[] { 4.0 }, new[] { 4.5, 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x22", BeamSectionX = "D22x30", BeamSectionY = "D22x30", FinishLoad = 1.5, LiveLoad = 1.5, RoofLiveLoad = 0.75 });
        p.Foundation.Type = FoundationType.Isolated;
        var r = FrameAnalysis.Run(p);
        var s = ConcreteDesigner.Design(p, r);

        var pk0 = AcadResults.Build(p, s);
        check("Kết quả về AutoCAD: chưa liên kết bản vẽ → không căn toạ độ", B(!pk0.Aligned), 1, 0);
        check("Kết quả: có ghi chú cột, dầm, móng", B(pk0.Items.Any(i => i.Layer == AcadResults.LCot) && pk0.Items.Any(i => i.Layer == AcadResults.LDam) && pk0.Items.Any(i => i.Layer == AcadResults.LMong)), 1, 0);
        check("Kết quả: tên cột C… có tiết diện 220x220", B(pk0.Items.Any(i => i.Type == "text" && i.Text.StartsWith("C") && i.Text.Contains("220x220"))), 1, 0);
        // liên kết bản vẽ: gốc bản vẽ (100; 200) đặt vào gốc mô hình
        p.CadLinks.Add(new CadLink { Name = "MB.dwg", Story = CadLink.AllStories, BaseX = 100, BaseY = 200, OffsetX = 0, OffsetY = 0 });
        var pk = AcadResults.Build(p, s);
        check("Có bản vẽ liên kết: căn đúng toạ độ bản vẽ gốc", B(pk.Aligned), 1, 0);
        var f0 = s.Footings.First();
        var mf = pk.Items.First(i => i.Layer == AcadResults.LMong && i.Type == "poly");
        double minX = mf.Pts.Min(q => q[0]);
        check("Toạ độ bản vẽ = toạ độ mô hình + gốc bản vẽ", B(pk.Items.Where(i => i.Type == "poly").All(i => i.Pts.All(q => q[0] > 90 && q[1] > 190))), 1, 0);
        var json = pk.ToJson();
        var back = AcadResultPackage.Parse(json);
        check("Gói kết quả JSON đọc lại được (camelCase cho add-in)", B(back.Items.Count == pk.Items.Count && json.Contains("\"items\"") && json.Contains("\"aligned\":true")), 1, 0);

        // ---- chi tiết rời ----
        var figs = BeamDrawing.BuildAllSplit(p, s.Beams.Take(2), s, 760);
        var det = SheetComposer.ComposeDetails(figs, BeamDrawing.CurrentScale, "KC-04 – CHI TIẾT DẦM");
        var sheet = SheetComposer.Compose(figs, BeamDrawing.CurrentScale, new SheetComposer.SheetInfo { Title = "x", SheetNo = "KC-04" });
        string dd = det.ToDxfString(), ds = sheet.ToDxfString();
        Console.WriteLine($"   · Chi tiết rời: {figs.Count} hình, khung bao {LayerCount(dd, Lay.KhungChiTiet)}, khung tên {LayerCount(dd, Lay.KhungTen)} (tờ dàn trang: khung tên {LayerCount(ds, Lay.KhungTen)})");
        check("Chi tiết rời: mỗi hình một khung bao", LayerCount(dd, Lay.KhungChiTiet), figs.Count(f => f.W.EntityCount > 0), 0);
        check("Chi tiết rời: không khung tên", LayerCount(dd, Lay.KhungTen), 0, 0);
        check("Tờ dàn trang thường vẫn có khung tên", B(LayerCount(ds, Lay.KhungTen) > 0), 1, 0);
        if (outDir != null)
        {
            string dir = Path.Combine(outDir, "ChiTietRoi");
            try
            {
                Directory.CreateDirectory(dir);
                var er = DrawingSet.Export(p, s, dir, new DrawingExportOptions { Mode = DrawingExportMode.Split, DetailsOnly = true, Format = CadOutputFormat.DxfR12 });
                int bad = er.Files.Where(f => f.EndsWith(".dxf")).Count(f => LayerCount(File.ReadAllText(f), Lay.KhungTen) > 0);
                Console.WriteLine($"   · Xuất chi tiết rời: {er.Files.Count} file, {bad} file còn khung tên");
                check("Xuất bộ bản vẽ chi tiết rời: không file nào có khung tên", bad, 0, 0);
                pk.Details.AddRange(er.Files);
                AcadResults.Write(pk, dir);
            }
            catch (Exception ex) { Console.WriteLine("   ! " + ex.Message); }
        }
    }
}
