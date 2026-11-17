using KetCauVXT.Design;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng quy trình Revit (chỉ kiến trúc) → thiết kế: đọc tường / phòng / cửa / cao độ (RevitArchReader), trọng lượng tường theo lớp cấu tạo
/// (RevitMaterials), bảng nhận diện tải (ArchLoadTakeoff), lập mô hình kết cấu, tối ưu tiết diện theo chi phí (SectionOptimizer).
/// Nhà mẫu 2 tầng 5×9 m, mái BTCT có tường chắn mái và bồn nước 1500 L. check(tên, giá trị, kỳ vọng, sai số).</summary>
public static class RevitArchToDesignTests
{
    private static List<RvLayer> L220() => new()
    {
        new() { Function = "Finish1", Material = "Vữa xi măng mác 75", Thickness = 0.015 },
        new() { Function = "Structure", Material = "Gạch đặc", Thickness = 0.19 },
        new() { Function = "Finish2", Material = "Vữa xi măng mác 75", Thickness = 0.015 },
    };

    private static List<RvLayer> L110() => new()
    {
        new() { Function = "Finish1", Material = "Plaster", Thickness = 0.015 },
        new() { Function = "Structure", Material = "Gạch rỗng 2 lỗ", Thickness = 0.08 },
        new() { Function = "Finish2", Material = "Plaster", Thickness = 0.015 },
    };

    private static List<double[]> Rect(double x0, double y0, double x1, double y1) => new() { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } };

    /// <summary>File trao đổi Revit tối giản (toạ độ lệch gốc 100; 200 m như mô hình thật).</summary>
    public static RevitExchange Sample()
    {
        double ox = 100, oy = 200;
        var ex = new RevitExchange { ProjectName = "Nhà mẫu Revit 2 tầng", SchemaVersion = "2.0" };
        ex.Levels.Add(new RvLevel { Name = "Tầng 1", Elevation = 0 });
        ex.Levels.Add(new RvLevel { Name = "Tầng 2", Elevation = 3.6 });
        ex.Levels.Add(new RvLevel { Name = "Mái", Elevation = 6.9 });
        ex.Grids.Add(new RvGrid { Name = "1", X1 = ox, Y1 = oy - 2, X2 = ox, Y2 = oy + 11 });
        ex.Grids.Add(new RvGrid { Name = "2", X1 = ox + 5, Y1 = oy - 2, X2 = ox + 5, Y2 = oy + 11 });
        ex.Grids.Add(new RvGrid { Name = "A", X1 = ox - 2, Y1 = oy, X2 = ox + 7, Y2 = oy });
        ex.Grids.Add(new RvGrid { Name = "B", X1 = ox - 2, Y1 = oy + 4.5, X2 = ox + 7, Y2 = oy + 4.5 });
        ex.Grids.Add(new RvGrid { Name = "C", X1 = ox - 2, Y1 = oy + 9, X2 = ox + 7, Y2 = oy + 9 });
        int id = 1;
        foreach (var (lv, h) in new[] { ("Tầng 1", 3.6), ("Tầng 2", 3.3) })
        {
            string tag = lv == "Tầng 1" ? "1" : "2";
            ex.Walls.Add(new RvWall { Id = $"W{tag}-S", Level = lv, X1 = ox, Y1 = oy, X2 = ox + 5, Y2 = oy, Height = h, TypeName = "Tường 220 gạch đặc", Layers = L220() });
            ex.Walls.Add(new RvWall { Id = $"W{tag}-E", Level = lv, X1 = ox + 5, Y1 = oy, X2 = ox + 5, Y2 = oy + 9, Height = h, TypeName = "Tường 220 gạch đặc", Layers = L220() });
            ex.Walls.Add(new RvWall { Id = $"W{tag}-N", Level = lv, X1 = ox + 5, Y1 = oy + 9, X2 = ox, Y2 = oy + 9, Height = h, TypeName = "Tường 220 gạch đặc", Layers = L220() });
            ex.Walls.Add(new RvWall { Id = $"W{tag}-W", Level = lv, X1 = ox, Y1 = oy + 9, X2 = ox, Y2 = oy, Height = h, TypeName = "Tường 220 gạch đặc", Layers = L220() });
            ex.Walls.Add(new RvWall { Id = $"W{tag}-I", Level = lv, X1 = ox, Y1 = oy + 4.5, X2 = ox + 5, Y2 = oy + 4.5, Height = h, TypeName = "Tường 110 gạch rỗng", Layers = L110() });
            ex.Openings.Add(new RvOpening { Id = $"D{tag}", HostId = $"W{tag}-I", Category = "Door", X = ox + 2.5, Y = oy + 4.5, Width = 0.9, Height = 2.2 });
            ex.Openings.Add(new RvOpening { Id = $"S{tag}", HostId = $"W{tag}-S", Category = "Window", X = ox + 2.5, Y = oy, Width = 1.6, Height = 1.4, Sill = 0.9 });
            id++;
        }
        // tường chắn mái 0,9 m
        foreach (var (a, b) in new[] { ((0.0, 0.0), (5.0, 0.0)), ((5.0, 0.0), (5.0, 9.0)), ((5.0, 9.0), (0.0, 9.0)), ((0.0, 9.0), (0.0, 0.0)) })
            ex.Walls.Add(new RvWall { Id = $"P{a.Item1}{a.Item2}", Level = "Mái", X1 = ox + a.Item1, Y1 = oy + a.Item2, X2 = ox + b.Item1, Y2 = oy + b.Item2, Height = 0.9, TypeName = "Tường 110 gạch rỗng", Layers = L110() });
        ex.Rooms.Add(new RvRoom { Id = "R11", Level = "Tầng 1", Name = "Phòng khách", Number = "101", Outline = Rect(ox, oy, ox + 5, oy + 4.5) });
        ex.Rooms.Add(new RvRoom { Id = "R12", Level = "Tầng 1", Name = "Bếp + ăn", Number = "102", Outline = Rect(ox, oy + 4.5, ox + 5, oy + 9) });
        ex.Rooms.Add(new RvRoom { Id = "R21", Level = "Tầng 2", Name = "Phòng ngủ 1", Number = "201", Outline = Rect(ox, oy, ox + 5, oy + 4.5) });
        ex.Rooms.Add(new RvRoom { Id = "R22", Level = "Tầng 2", Name = "Phòng ABC", Number = "202", Outline = Rect(ox, oy + 4.5, ox + 5, oy + 9) });
        ex.Rooms.Add(new RvRoom { Id = "R31", Level = "Mái", Name = "Sân thượng", Number = "301", Outline = Rect(ox, oy, ox + 5, oy + 9) });
        ex.Mep.Add(new RvMep { Id = "T1", Category = "PlumbingFixture", Name = "Bồn nước inox 1500L", Level = "Mái", X1 = ox + 2.5, Y1 = oy + 7, Z1 = 7.2, W = 1.0, D = 1.0, H = 1.6 });
        return ex;
    }

    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---- vật liệu tường ----
        check("Vật liệu: \"Gạch đặc\" → 18 kN/m³", RevitMaterials.Match("Gạch đặc")?.Gamma ?? -1, 18, 0);
        check("Vật liệu: \"Gạch rỗng 2 lỗ\" → 15 kN/m³", RevitMaterials.Match("Gạch rỗng 2 lỗ")?.Gamma ?? -1, 15, 0);
        check("Vật liệu: \"AAC block\" → 8 kN/m³", RevitMaterials.Match("AAC block 600x200")?.Gamma ?? -1, 8, 0);
        check("Vật liệu: \"Masonry - Brick\" → 18 kN/m³", RevitMaterials.Match("Masonry - Brick")?.Gamma ?? -1, 18, 0);
        check("Vật liệu: \"Concrete, Cast-in-Place\" → 25 kN/m³", RevitMaterials.Match("Concrete, Cast-in-Place gray")?.Gamma ?? -1, 25, 0);
        check("Vật liệu: tên lạ không nhận", B(RevitMaterials.Match("Default") == null), 1, 0);
        check("Tên loại tường \"Tường 220\" → 0,22 m", RevitMaterials.ThicknessFromName("Tường 220") ?? 0, 0.22, 1e-9);
        var ex = Sample();
        var w220 = RevitMaterials.WallMass(ex.Walls[0], new ArchImportOptions());
        check("Tường 220 gạch đặc: 0,19·18 + 0,03·18 = 3,96 kN/m²", w220.PerArea, 3.96, 1e-6);
        check("Tường 220: chắc chắn (nhận mọi lớp)", B(w220.Sure), 1, 0);
        var w110 = RevitMaterials.WallMass(ex.Walls[4], new ArchImportOptions());
        check("Tường 110 gạch rỗng: 0,08·15 + 0,03·18 = 1,74 kN/m²", w110.PerArea, 1.74, 1e-6);
        check("Bồn nước 1500L → 16,5 kN", ArchLoadTakeoff.TankLoad("Bồn nước inox 1500L").Load, 16.5, 1e-9);
        check("Bồn nước 2 m3 → 22 kN", ArchLoadTakeoff.TankLoad("Téc nước 2 m3").Load, 22, 1e-9);

        // ---- đọc kiến trúc ----
        check("Mô hình mẫu chỉ có kiến trúc", B(!RevitArchReader.HasStructure(ex)), 1, 0);
        var o = new ArchImportOptions();
        var d = RevitArchReader.ToDrawing(ex, o);
        foreach (var i in d.Info) Console.WriteLine("    " + i);
        var plans = d.Floors.Where(f => !f.IsRoofPlan).ToList();
        check("Revit: 2 mặt bằng tầng", plans.Count, 2, 0);
        check("Revit: có mặt bằng mái", d.Floors.Count(f => f.IsRoofPlan), 1, 0);
        check("Revit: chiều cao tầng 1 = 3,6 m", plans[0].Height, 3.6, 1e-9);
        check("Revit: chiều cao tầng 2 = 3,3 m", plans[1].Height, 3.3, 1e-9);
        check("Revit: tầng 1 có 5 đoạn tường", plans[0].Walls.Count, 5, 0);
        check("Revit: 2 trục X, 3 trục Y", plans[0].Grids.Count(g => g.Dir == GridDir.X) * 10 + plans[0].Grids.Count(g => g.Dir == GridDir.Y), 23, 0);
        check("Revit: gốc toạ độ dời về giao trục 1-A", plans[0].Walls.Min(w => Math.Min(w.Ax, w.Bx)), 0, 1e-9);
        check("Revit: cửa đi trừ trên tường 110", plans[0].Walls.First(w => w.TypeName.Contains("110")).OpeningLength, 0.9, 1e-9);
        check("Revit: 4 tường ngoài", plans[0].Walls.Count(w => w.Exterior), 4, 0);
        check("Revit: phòng khách → công năng phòng khách", B(plans[0].Rooms.Any(r => r.Text == "Phòng khách" && r.Kind == RoomKind.Living)), 1, 0);
        check("Revit: \"Bếp + ăn\" → bếp", B(plans[0].Rooms.Any(r => r.Kind == RoomKind.Kitchen)), 1, 0);
        check("Revit: \"Phòng ABC\" không nhận ra", B(plans[1].Rooms.Any(r => r.Text == "Phòng ABC" && r.Kind == RoomKind.Unknown)), 1, 0);
        check("Revit: tường chắn mái 0,9 m", o.ParapetHeight, 0.9, 1e-9);

        // ---- bảng nhận diện tải ----
        var t = ArchLoadTakeoff.From(d, o, ex);
        check("Bảng tải: 5 tên phòng", t.Rooms.Count, 5, 0);
        check("Bảng tải: \"Phòng ABC\" tô vàng (chưa chắc)", B(t.Rooms.Any(r => r.Name == "Phòng ABC" && !r.Sure)), 1, 0);
        check("Bảng tải: 2 loại tường", t.Walls.Count, 2, 0);
        check("Bảng tải: tường 220 = 3,96 kN/m²", t.Walls.First(w => w.TypeName.Contains("220")).PerArea, 3.96, 1e-6);
        check("Bảng tải: 1 bồn nước 16,5 kN", t.Equipment.Count == 1 ? t.Equipment[0].Load : -1, 16.5, 1e-9);
        check("Bảng tải: bồn nước cao độ 7,2 m so với sàn tầng 1", t.Equipment.Count == 1 ? t.Equipment[0].Z : -1, 7.2, 1e-9);
        check("Bảng tải: 1 mục chưa chắc", t.UncertainCount, 1, 0);
        // người dùng xác nhận: "Phòng ABC" là phòng làm việc, hoạt tải phòng làm việc 2,0
        t.Rooms.First(r => r.Name == "Phòng ABC").Kind = RoomKind.Office;
        t.Apply(d, o);
        check("Xác nhận: phòng ABC → làm việc", B(d.Floors.SelectMany(f => f.Rooms).Any(r => r.Text == "Phòng ABC" && r.Kind == RoomKind.Office)), 1, 0);
        check("Xác nhận: γ tương đương tường 220 = (3,96 − 0,54)/0,22", plans[0].Walls.First(w => w.TypeName.Contains("220")).UnitWeight, (3.96 - 0.54) / 0.22, 1e-3);

        // ---- lập mô hình kết cấu ----
        var s = ArchToStructure.Propose(d, o);
        foreach (var w in s.Warnings) Console.WriteLine("    ⚠ " + w);
        var p = ModelBuilder.NewProject("Từ Revit");
        p.Wind.Enabled = false;
        var rep = ArchToStructure.Apply(p, d, s, o);
        Console.WriteLine($"    mô hình: {p.Frames.Count(f => f.Kind == FrameKind.Column)} cột, {p.Frames.Count(f => f.Kind == FrameKind.Beam)} dầm, {p.Slabs.Count} ô sàn, {p.Walls.Count} tường");
        check("Mô hình: có cột (≥ 6 giao trục)", p.Frames.Where(f => f.Kind == FrameKind.Column).Select(f => (Math.Round(p.Node(f.NodeI).X, 2), Math.Round(p.Node(f.NodeI).Y, 2))).Distinct().Count() >= 6 ? 1 : 0, 1, 0);
        check("Mô hình: có ô sàn", B(p.Slabs.Count > 0), 1, 0);
        check("Mô hình: 2 cao trình (T2, mái)", p.Stories.Count, 2, 0);
        check("Mô hình: tường tầng 2 có loại γ theo Revit", B(p.WallTypes.Any(wt => Math.Abs(wt.UnitWeight - (3.96 - 0.54) / 0.22) < 0.01)), 1, 0);
        check("Mô hình: bồn nước → 1 tải nút 16,5 kN", t.ApplyEquipment(p), 1, 0);
        check("Mô hình: tải bồn nước ở cao trình mái", B(p.NodeLoads.Any(nl => Math.Abs(nl.Fz + 16.5) < 1e-9 && Math.Abs(p.Node(nl.Node).Z - p.Stories.Max(st => st.Elevation)) < 0.02)), 1, 0);
        check("Mô hình: kiểm tra mô hình không lỗi", ModelCheck.Run(p).Errors, 0, 0);

        // ---- xuất ngược sang Revit (*.vxt2revit.json): toạ độ trở về hệ Revit ----
        check("Gốc Revit ghi nhận khi đọc kiến trúc", d.OriginX * 1000 + d.OriginY, 100 * 1000 + 200, 1e-9);
        p.RevitLink = new RevitLink { OriginX = d.OriginX, OriginY = d.OriginY, GroundZ = d.GroundZ };
        var js = RevitSyncExport.Build(p);
        var jcols = js["Columns"]!.AsArray(); var jbeams = js["Beams"]!.AsArray(); var jslabs = js["Slabs"]!.AsArray();
        check("Xuất Revit: số cột = số cột mô hình", jcols.Count, p.Frames.Count(f => f.Kind == FrameKind.Column), 0);
        check("Xuất Revit: có dầm, sàn", B(jbeams.Count > 0 && jslabs.Count > 0), 1, 0);
        check("Xuất Revit: cột nhỏ nhất ở X = 100 (toạ độ Revit)", jcols.Min(c => (double)c!["X"]!), 100, 1e-6);
        check("Xuất Revit: cột nhỏ nhất ở Y = 200 (toạ độ Revit)", jcols.Min(c => (double)c!["Y"]!), 200, 1e-6);
        check("Xuất Revit: đỉnh cột cao nhất = mái +6,9", jcols.Max(c => (double)c!["ZTop"]!), 6.9, 1e-6);
        check("Xuất Revit: trục 1 tại X = 100", B(js["Grids"]!.AsArray().Any(g => (string)g!["Name"]! == "1" && Math.Abs((double)g["Coord"]! - 100) < 1e-6)), 1, 0);
        check("Xuất Revit: cao độ tầng 1 tại 0", B(js["Levels"]!.AsArray().Any(l => Math.Abs((double)l!["Elevation"]!) < 1e-6)), 1, 0);
        check("Xuất Revit: loại tường có lớp cấu tạo", B(js["WallTypes"]!.AsArray().All(w => w!["Layers"]!.AsArray().Count > 0)), 1, 0);
        check("Xuất Revit: tên loại cột theo quy ước C…x…", B(js["ColumnTypes"]!.AsArray().All(t => ((string)t!["Name"]!).StartsWith("C"))), 1, 0);
        check("Xuất Revit: chuỗi JSON đọc lại được", B(System.Text.Json.Nodes.JsonNode.Parse(RevitSyncExport.ToJson(p)) != null), 1, 0);

        // ---- tối ưu tiết diện theo chi phí (giữ bề rộng) ----
        var widths = p.Frames.Where(f => f.Kind == FrameKind.Beam).ToDictionary(f => f.Id, f => p.Section(f.Section).B);
        var opt = SectionOptimizer.Run(p, new CostRates(), new OptimizeOptions { MaxRounds = 4 });
        foreach (var l in opt.Log) Console.WriteLine("    " + l);
        check("Tối ưu: mọi dầm, cột, sàn đạt", B(opt.AllOk), 1, 0);
        check("Tối ưu: kết quả cuối không có cấu kiện không đạt", opt.Final?.Bad ?? -1, 0, 0);
        if (opt.Initial.Bad == 0) check("Tối ưu: chi phí không tăng so với ban đầu (đã đạt)", B(opt.Final.Cost <= opt.Initial.Cost + 1), 1, 0);
        int widened = p.Frames.Where(f => f.Kind == FrameKind.Beam && widths.ContainsKey(f.Id)).Count(f => Math.Abs(p.Section(f.Section).B - widths[f.Id]) > 1e-6);
        check("Tối ưu: giữ bề rộng dầm (trừ chỗ báo phải tăng bề rộng)", B(widened == 0 || opt.WidthChanges.Count > 0), 1, 0);
        check("Tối ưu: có số liệu chi phí", B(opt.Final.Cost > 0 && opt.Final.ConcreteM3 > 0), 1, 0);
        Console.WriteLine($"    chi phí: ban đầu {opt.Initial.Cost / 1e6:0.00} → cuối {opt.Final.Cost / 1e6:0.00} triệu đồng, {opt.Evaluations} lần phân tích");
        try { Directory.CreateDirectory(outDir); p.Save(Path.Combine(outDir, "TuRevitKienTruc.kcvxt")); } catch { /* chỉ để xem */ }
    }
}
