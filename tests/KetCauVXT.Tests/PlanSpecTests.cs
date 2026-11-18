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
    }
}
