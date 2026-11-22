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

        // phối hợp hình học trên phương án thật (Project1 làm lại): cột, dầm theo nhịp, cao tường, cửa ↔ cột, thang ↔ tường / dầm
        var pm = RevitJsonImporter.ReadFile(Path.Combine(AppContext.BaseDirectory, "Data", "Project1_Moi.vxtphuongan.json"));
        foreach (var l in PlanStairs.DesignAll(pm)) Console.WriteLine("    " + l);
        check("Thang Project1: U 3 vế, 21 bậc, vế giữa 2–4", B(pm.Stairs.All(t => t.Runs.Count == 3 && t.Runs.Sum(r => r.Risers) == 21 && t.Runs[1].Risers >= 2 && t.Runs[1].Risers <= 4)), 1, 0);
        foreach (var l in PlanCoordination.Coordinate(pm)) Console.WriteLine("    " + l);
        var pmIss = PlanSpec.Validate(pm);
        foreach (var i in pmIss) Console.WriteLine("    " + i);
        Directory.CreateDirectory(Path.Combine(outDir, "PhoiHop"));
        PlanSpec.Save(pm, Path.Combine(outDir, "PhoiHop", "Project1_Moi.vxtphuongan.json"));
        {
            string b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(PlanSpec.ToJson(pm)));
            for (int q = 0, n = 0; q < b64.Length; q += 3000, n++) Console.WriteLine($"PHOIHOP-{n:000}:" + b64.Substring(q, Math.Min(3000, b64.Length - q)));
        }
        foreach (var c in pm.Columns) Console.WriteLine($"    cột {c.Id} ({c.X:0.000}; {c.Y:0.000}) {c.BaseLevel}→{c.TopLevel}");
        foreach (var b in pm.Beams) Console.WriteLine($"    dầm {b.Id} ({b.X1:0.000}; {b.Y1:0.000})→({b.X2:0.000}; {b.Y2:0.000}) {b.B * 1000:0}x{b.H * 1000:0}");
        check("Phối hợp Project1: có cột mỗi tầng", B(pm.Columns.Select(c => c.BaseLevel).Distinct().Count() >= 3), 1, 0);
        check("Phối hợp Project1: cột nằm trong nhà (mặt ngoài ≤ ranh)", B(pm.Columns.All(c => c.X - c.B / 2 >= -0.001 && c.Y - c.H / 2 >= -0.001 && c.Y + c.H / 2 <= 5.001 && c.X + c.B / 2 <= 20.001)), 1, 0);
        check("Phối hợp Project1: không cửa nào lấn cột", pmIss.Count(i => i.Message.Contains("lấn vào cột")), 0, 0);
        int nCol = pm.Columns.Count, nBm = pm.Beams.Count;
        PlanCoordination.Coordinate(pm);
        check("Phối hợp Project1: chạy lại không đổi số cột / dầm", pm.Columns.Count * 1000 + pm.Beams.Count, nCol * 1000 + nBm, 0);
        // thang lấn tường phải bị bắt
        var bad2 = RevitJsonImporter.ReadFile(Path.Combine(AppContext.BaseDirectory, "Data", "Project1_Moi.vxtphuongan.json"));
        bad2.Stairs[1].Runs[0].X1 += 0.2; bad2.Stairs[1].Runs[0].X2 += 0.2;
        check("Phối hợp: bắt vế thang lấn tường", B(PlanCoordination.Check(bad2).Any(i => i.Error && i.Message.Contains("lấn vào tường"))), 1, 0);

        // Project1 theo yêu cầu tầng 1: gara sâu 5,3 m, phòng khách 5 m, cầu thang, bếp + ăn (trục 0 / 4,31 / 10,3 / 13,6 / 16,8 / 20)
        {
            var p2 = RevitJsonImporter.ReadFile(Path.Combine(AppContext.BaseDirectory, "Data", "Project1_T1moi.vxtphuongan.json"));
            foreach (var l in PlanCoordination.Coordinate(p2)) Console.WriteLine("    T1moi: " + l);
            var iss2 = PlanSpec.Validate(p2);
            foreach (var i in iss2) Console.WriteLine("    T1moi: " + i);
            check("Project1 T1 mới: phương án không lỗi", iss2.Count(i => i.Error), 0, 0);
            check("Project1 T1 mới: có cột mỗi tầng", B(p2.Columns.Select(c => c.BaseLevel).Distinct().Count() >= 3), 1, 0);
            string b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(PlanSpec.ToJson(p2)));
            for (int q = 0, n = 0; q < b64.Length; q += 3000, n++) Console.WriteLine($"PHOIHOP2-{n:000}:" + b64.Substring(q, Math.Min(3000, b64.Length - q)));
        }

        // thang chữ U 3 vế theo quy ước 21 bậc, vế giữa 3 bậc
        {
            var lv1 = new RvLevel { Name = "T1", Elevation = 0 }; var lv2 = new RvLevel { Name = "T2", Elevation = 3.75 };
            var (u3, notes) = StairDesign.U3("CTX", lv1, lv2, 9.35, 0.22, 12.43, 3.525);
            foreach (var n in notes) Console.WriteLine("    " + n);
            check("Thang U3: 21 bậc 9 + 3 + 9", u3.Runs.Count * 1000 + u3.Runs[0].Risers * 100 + u3.Runs[1].Risers * 10 + u3.Runs[2].Risers, 3000 + 900 + 30 + 9, 0);
            check("Thang U3: không lỗi", notes.Count(n => n.StartsWith("Lỗi")), 0, 0);
            double b1 = Math.Abs(u3.Runs[0].Y2 - u3.Runs[0].Y1) / (u3.Runs[0].Risers - 1);
            check("Thang U3: mặt bậc 250–300 mm", B(b1 >= 0.25 - 1e-6 && b1 <= 0.30 + 1e-6), 1, 0);
            check("Thang U3: vế 1 và vế 3 nằm trong lồng thang", B(u3.Runs.All(r => Math.Min(r.X1, r.X2) >= 9.35 - 1e-6 && Math.Max(r.X1, r.X2) <= 12.43 + 1e-6 && Math.Min(r.Y1, r.Y2) >= 0.22 - 1e-6 && Math.Max(r.Y1, r.Y2) <= 3.525 + 1e-6)), 1, 0);
        }

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
