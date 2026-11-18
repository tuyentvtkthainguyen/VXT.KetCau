using KetCauVXT.Import;
using KetCauVXT.Import.Arch;

namespace KetCauVXT.Tests;

/// <summary>Phương án kiến trúc chung (*.vxtphuongan.json) cho đội agent: mẫu nhà phố, kiểm tra, ghi / đọc,
/// CAD → phương án, và Việt Const tính kết cấu thẳng từ phương án.</summary>
public static class PlanSpecTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        static double B(bool v) => v ? 1 : 0;
        Directory.CreateDirectory(outDir);
        var ex = PlanSpec.SampleTownhouse();
        var iss = PlanSpec.Validate(ex);
        foreach (var i in iss) Console.WriteLine("    " + i);
        check("Phương án mẫu: không lỗi", iss.Count(i => i.Error), 0, 0);
        check("Phương án mẫu: 4 cao độ, 3 cầu thang", ex.Levels.Count * 10 + ex.Stairs.Count, 43, 0);

        string f = Path.Combine(outDir, "NhaPhoMau_5x20" + PlanSpec.Extension);
        PlanSpec.Save(ex, f);
        var back = RevitJsonImporter.ReadFile(f);
        check("Phương án: đọc lại giữ cầu thang", back.Stairs.Count, 3, 0);
        check("Phương án: đọc lại giữ mặt tiền", back.Facade?.Materials.Count ?? 0, 5, 0);
        check("Phương án: đọc lại giữ tầng của cửa", B(back.Openings.All(o => o.Level.Length > 0)), 1, 0);
        check("Phương án: đọc lại giữ lớp cấu tạo tường", B(back.Walls.All(w => w.Layers.Count == 3)), 1, 0);

        // dầm dưới sàn: tường hạ xuống đáy dầm / đáy sàn, chạy lại không thêm trùng
        var bm = PlanSpec.SampleTownhouse();
        PlanSpec.AddBeams(bm, 0.35);
        int nBeam = bm.Beams.Count;
        check("Dầm: có dầm dưới tường 220", B(nBeam > 0 && bm.Beams.All(b => b.H > 0.29 && b.Z1 > 0)), 1, 0);
        var lvs = bm.Levels.OrderBy(l => l.Elevation).ToList();
        int through = 0;
        for (int i = 0; i + 1 < lvs.Count; i++)
            if (bm.Floors.Any(fl => fl.Level == lvs[i + 1].Name))
                through += bm.Walls.Count(w => w.Level == lvs[i].Name && Math.Abs(w.Height - (lvs[i + 1].Elevation - lvs[i].Elevation)) < 0.05);
        check("Dầm: không còn tường xuyên sàn", through, 0, 0);
        PlanSpec.AddBeams(bm, 0.35);
        check("Dầm: chạy lại không thêm trùng", bm.Beams.Count, nBeam, 0);
        check("Dầm: phương án vẫn không lỗi", PlanSpec.Validate(bm).Count(i => i.Error), 0, 0);
        bm.Facade.Elements.Add(new RvFacadeElement { Id = "LC1", Type = "LanCan", Style = "Kính", Level = "Tầng 2", Points = new() { new[] { 0.2, -1.2 }, new[] { 4.8, -1.2 } }, Height = 1.1 });
        bm.Facade.Elements.Add(new RvFacadeElement { Id = "K1", Type = "Khoi", Part = "Khung viền", Points = new() { new[] { 0.0, -1.3 }, new[] { 0.2, 0.0 } }, Z0 = 3.3, Z1 = 10 });
        string fb = Path.Combine(outDir, "NhaPhoMau_dam" + PlanSpec.Extension);
        PlanSpec.Save(bm, fb);
        var bb = RevitJsonImporter.ReadFile(fb);
        check("Mặt tiền: đọc lại giữ chi tiết + dầm", bb.Facade.Elements.Count * 100 + bb.Beams.Count, 200 + nBeam, 0);
        bb.Facade.Elements.Add(new RvFacadeElement { Id = "X", Type = "Tháp", Points = new() { new[] { 0.0, 0.0 } } });
        check("Mặt tiền: bắt loại chi tiết sai", PlanSpec.Validate(bb).Count(i => i.Error), 1, 0);

        // lỗi phải bắt được
        var bad = PlanSpec.SampleTownhouse();
        bad.Openings.Add(new RvOpening { Id = "X1", Level = "Tầng 1", Category = "Door", X = 2.5, Y = 7, Width = 0.9, Height = 2.2 });
        bad.Stairs.Clear(); bad.Rooms.RemoveAll(r => r.Name.Contains("thang", StringComparison.OrdinalIgnoreCase));
        bad.Rooms.Add(new RvRoom { Id = "R99", Level = "Tầng 2", Name = "Room" });
        var bi = PlanSpec.Validate(bad);
        check("Kiểm tra: cửa không nằm trên tường", B(bi.Any(i => i.Error && i.Message.Contains("X1"))), 1, 0);
        check("Kiểm tra: nhà nhiều tầng thiếu cầu thang", B(bi.Any(i => i.Error && i.Message.Contains("cầu thang"))), 1, 0);
        check("Kiểm tra: phòng chưa đặt tên", B(bi.Any(i => !i.Error && i.Message.Contains("chưa có tên"))), 1, 0);

        // Việt Const tính kết cấu thẳng từ phương án (đường kiến trúc)
        check("Phương án → kết cấu: không có khung KC → đi đường kiến trúc", B(!RevitArchReader.HasStructure(back)), 1, 0);
        var d = RevitArchReader.ToDrawing(back, new ArchImportOptions());
        check("Phương án → kết cấu: 3 tầng + tum thang", d.Floors.Count(p => !p.IsRoofPlan), 4, 0);
        check("Phương án → kết cấu: tầng trên cùng là tum", B(d.Floors.Where(p => !p.IsRoofPlan).Last().IsTum), 1, 0);
        var o = KetCauVXT.Pipeline.RevitDesignPipeline.ParseArgs(new[] { f, Path.Combine(outDir, "PhuongAnKetCau"), "--vung-gio", "II", "--khong-ho-so" }, out _);
        var rep = KetCauVXT.Pipeline.RevitDesignPipeline.Run(o);
        foreach (var s in rep.Steps) Console.WriteLine($"    {(s.Ok ? "✓" : "✗")} {s.Title}: {s.Lines.FirstOrDefault()}");
        Console.WriteLine($"    {rep.Columns} cột, {rep.Beams} dầm, {rep.Slabs} ô sàn, không đạt {rep.Failed}, {rep.CostVnd / 1e6:0.0} triệu");
        check("Phương án → kết cấu: chạy không lỗi", B(rep.Ok), 1, 0);
        check("Phương án → kết cấu: có cột", B(rep.Columns >= 6), 1, 0);
        check("Phương án → kết cấu: mọi cấu kiện đạt", rep.Failed, 0, 0);

        // CAD → phương án (bản vẽ kiến trúc mẫu 4 mặt bằng trong 1 file DXF)
        string dxf = Path.Combine(Path.GetTempPath(), "KienTrucMau_PhuongAn.dxf");   // mẫu nguồn, không thuộc kết quả kiểm DXF
        File.WriteAllText(dxf, ArchSample.Build(), new System.Text.UTF8Encoding(false));
        var dw = ArchPlanReader.Read(dxf, new ArchImportOptions());
        var cad = PlanSpec.FromArchDrawing(dw, "Từ CAD");
        PlanSpec.Save(cad, Path.Combine(outDir, "TuCAD" + PlanSpec.Extension));
        Console.WriteLine($"    CAD → phương án: {cad.Levels.Count} cao độ, {cad.Walls.Count} tường, {cad.Openings.Count} cửa, {cad.Rooms.Count} phòng, {cad.Stairs.Count} ô thang, {cad.Grids.Count} trục");
        foreach (var i in PlanSpec.Validate(cad).Take(12)) Console.WriteLine("    " + i);
        check("CAD → phương án: có tường mọi tầng", B(cad.Levels.Where(l => l.Name != "Mái").All(l => cad.Walls.Any(w => w.Level == l.Name))), 1, 0);
        check("CAD → phương án: cao độ tăng dần", B(cad.Levels.Zip(cad.Levels.Skip(1)).All(p => p.Second.Elevation > p.First.Elevation)), 1, 0);
        check("CAD → phương án: có cửa", B(cad.Openings.Count > 0), 1, 0);
        check("CAD → phương án: có trục", B(cad.Grids.Count >= 4), 1, 0);
        check("CAD → phương án: không lỗi cửa ngoài tường", PlanSpec.Validate(cad).Count(i => i.Error && i.Message.StartsWith("Cửa")), 0, 0);

        // Revit → phương án (Project1 thật): cửa có tầng, giữ họ / loại, ô thang từ phòng Cầu thang
        var p1 = KetCauVXT.Import.RevitJsonImporter.ReadFile(Path.Combine(AppContext.BaseDirectory, "Data", "Project1_Rooms.vxtrevit.json"));
        var rp = PlanSpec.FromRevit(p1, new[] { @"C:\mau\Project1.rvt" }, true);
        check("Revit → phương án: mọi cửa có tầng", rp.Openings.Count(o => string.IsNullOrEmpty(o.Level)), 0, 0);
        check("Revit → phương án: có ô thang từ phòng Cầu thang", B(rp.Stairs.Count >= 1), 1, 0);
        check("Revit → phương án: dời gốc về góc lô (x, y ≥ 0)", B(rp.Walls.All(w => Math.Min(w.X1, w.X2) >= -1e-6 && Math.Min(w.Y1, w.Y2) >= -1e-6)), 1, 0);
        check("Revit → phương án: giữ nguồn họ", rp.FamilySources.Count, 1, 0);
        check("Revit → phương án: không lỗi cửa ngoài tường", PlanSpec.Validate(rp).Count(i => i.Error && i.Message.StartsWith("Cửa")), 0, 0);
        // cầu thang theo vế: bậc 163 mm / 270 mm đạt, vế ngắn bị báo lỗi
        var good = new RvStair { Id = "T", BaseLevel = "Tầng 1", TopLevel = "Tầng 2", Outline = new List<double[]> { new[] { 2.6, 9.0 }, new[] { 5.0, 9.0 }, new[] { 5.0, 14.5 }, new[] { 2.6, 14.5 } }, Width = 1, Runs = { new RvStairRun { X1 = 3, Y1 = 9, X2 = 3, Y2 = 9 + 0.27 * 21, Width = 1, Risers = 22 } } };
        var ss = PlanSpec.SampleTownhouse(); ss.Stairs.Clear(); ss.Stairs.Add(good);
        check("Thang theo vế đạt: không lỗi thang", PlanSpec.Validate(ss).Count(i => i.Error && i.Message.StartsWith("Cầu thang")), 0, 0);
        good.Runs[0].Y2 = 9 + 0.2 * 21;
        check("Thang theo vế: mặt bậc 200 mm → lỗi", B(PlanSpec.Validate(ss).Any(i => i.Error && i.Message.Contains("mặt bậc"))), 1, 0);
    }
}
