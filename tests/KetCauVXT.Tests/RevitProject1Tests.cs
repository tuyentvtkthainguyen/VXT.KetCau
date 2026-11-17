using KetCauVXT.Design;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Nhà phố 3 tầng thật (Project1, Revit 2025) xuất bằng add-in: 5 cao độ (Tầng 1 −0,45 chỉ là cốt sân), tường 220/110 có lớp cấu tạo,
/// 71 thanh Structural Framing nhưng KHÔNG có cột (34 thanh là lan can / khung kính 20×40…), phòng chưa đặt tên.
/// Trước đây phần mềm nhập thẳng 71 dầm không cột → mô hình hỏng. Nay phải đi đường kiến trúc → bố trí kết cấu → phân tích được.</summary>
public static class RevitProject1Tests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        static double B(bool v) => v ? 1 : 0;
        string file = Path.Combine(AppContext.BaseDirectory, "Data", "Project1.vxtrevit.json");
        if (!File.Exists(file)) { check("Project1: có file dữ liệu", 0, 1, 0); return; }
        var ex = RevitJsonImporter.ReadFile(file);
        Console.WriteLine($"    Project1: {ex.Levels.Count} cao độ, {ex.Walls.Count} tường, {ex.Beams.Count} thanh framing, {ex.Columns.Count} cột, {ex.Rooms.Count} phòng");
        check("Project1: không nhập trực tiếp (dầm không có cột)", B(!RevitArchReader.HasStructure(ex)), 1, 0);
        var why = RevitArchReader.StructureSkipReason(ex);
        Console.WriteLine("    lý do: " + why);
        check("Project1: báo lý do bỏ dầm Revit", B(why != null), 1, 0);

        var o = new ArchImportOptions();
        var d = RevitArchReader.ToDrawing(ex, o);
        foreach (var i in d.Info) Console.WriteLine("    " + i);
        foreach (var f in d.Floors) foreach (var n in f.Notes) Console.WriteLine("    · " + f.Title + ": " + n);
        foreach (var w in d.Warnings) Console.WriteLine("    ⚠ " + w);
        var plans = d.Floors.Where(f => !f.IsRoofPlan).ToList();
        foreach (var f in d.Floors) Console.WriteLine($"    mặt bằng {f.Title}: cao {f.Height:0.00} m, {f.Walls.Count} tường, {f.Rooms.Count} phòng, {f.Grids.Count} trục, mái={f.IsRoofPlan}");
        check("Project1: 3 mặt bằng tầng có tường (T2, T3, T4)", plans.Count, 3, 0);
        check("Project1: có mặt bằng mái", B(d.Floors.Any(f => f.IsRoofPlan)), 1, 0);

        var t = ArchLoadTakeoff.From(d, o, ex);
        foreach (var n in t.Notes) Console.WriteLine("    tải ⚠ " + n);
        Console.WriteLine($"    bảng tải: {t.Rooms.Count} tên phòng, {t.Walls.Count} loại tường, {t.Equipment.Count} thiết bị, {t.UncertainCount} mục chưa chắc");
        t.Apply(d, o);

        var s = ArchToStructure.Propose(d, o);
        foreach (var w in s.Warnings) Console.WriteLine("    bố trí ⚠ " + w);
        var p = ModelBuilder.NewProject("Project1");
        p.Wind.Enabled = false;
        ArchToStructure.Apply(p, d, s, o);
        int nc = p.Frames.Count(f => f.Kind == FrameKind.Column), nb = p.Frames.Count(f => f.Kind == FrameKind.Beam);
        Console.WriteLine($"    mô hình: {nc} cột, {nb} dầm, {p.Slabs.Count} ô sàn, {p.Walls.Count} tường, {p.Stories.Count} cao trình");
        check("Project1: có cột", B(nc >= 6), 1, 0);
        check("Project1: có ô sàn", B(p.Slabs.Count > 0), 1, 0);
        var mc = ModelCheck.Run(p);
        foreach (var it in mc.Issues.Where(i => i.Severity != IssueSeverity.Info).Take(30)) Console.WriteLine($"    kiểm tra {it.Severity}: {it.Message}");
        check("Project1: kiểm tra mô hình không lỗi", mc.Errors, 0, 0);

        var opt = SectionOptimizer.Run(p, new CostRates(), new OptimizeOptions { MaxRounds = 3 });
        foreach (var l in opt.Log.Take(40)) Console.WriteLine("    " + l);
        check("Project1: sau tối ưu mọi cấu kiện đạt", B(opt.AllOk), 1, 0);
        try { Directory.CreateDirectory(outDir); p.Save(Path.Combine(outDir, "Project1_TuRevit.kcvxt")); } catch { /* chỉ để xem */ }
    }
}
