using KetCauVXT.Import.Arch;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Lấy kích thước lưới trục và tầng từ mặt bằng tải lên (Core/Import/Arch/GridFromPlan.cs).</summary>
public static class GridFromPlanTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var sp = GridFromPlan.Spans(new[] { 0.0, 3.6, 7.2, 10.8, 15.0, 15.05 });
        check("Bước trục: 4 bước, trục trùng (< 0,3 m) bị gộp", sp.Count, 4, 0);
        check("Bước trục cuối 4,2 m", sp[3], 4.2, 1e-9);
        check("Chuỗi ô nhập gộp bước lặp: \"3*3.6 4.2\"", B(GridFromPlan.SpansText(sp) == "3*3.6 4.2"), 1, 0);
        check("Chuỗi 2 bước giống nhau không gộp: \"3.3 3.3\"", B(GridFromPlan.SpansText(new[] { 3.3, 3.3 }) == "3.3 3.3"), 1, 0);
        check("Làm tròn 10 mm: 3,6049 → 3,6", GridFromPlan.Spans(new[] { 0, 3.6049 })[0], 3.6, 1e-9);
        check("Làm tròn 50 mm: 3,62 → 3,6", GridFromPlan.Spans(new[] { 0, 3.62 }, 0.05)[0], 3.6, 1e-9);

        var plan = new ArchFloorPlan { Title = "MẶT BẰNG TẦNG 1", FloorNo = 1, Height = 3.9 };
        foreach (var (n, c) in new[] { ("1", 0.0), ("2", 4.2), ("3", 8.4), ("4", 12.0) }) plan.Grids.Add(new ArchGrid { Name = n, Dir = GridDir.X, Coord = c });
        foreach (var (n, c) in new[] { ("A", 0.0), ("B", 5.0) }) plan.Grids.Add(new ArchGrid { Name = n, Dir = GridDir.Y, Coord = c });
        var r = GridFromPlan.FromPlan(plan);
        check("Từ mặt bằng CAD: bước X \"4.2 4.2 3.6\"", B(r.SpansXText == "4.2 4.2 3.6"), 1, 0);
        check("Từ mặt bằng CAD: bước Y \"5\"", B(r.SpansYText == "5"), 1, 0);
        check("Tên trục giữ theo bản vẽ", B(r.NamesX.SequenceEqual(new[] { "1", "2", "3", "4" })), 1, 0);

        var d = new ArchDrawing();
        d.Floors.Add(plan);
        var p2 = new ArchFloorPlan { Title = "MẶT BẰNG TẦNG 2", FloorNo = 2, Height = 3.6 };
        p2.Grids.AddRange(plan.Grids);
        d.Floors.Add(p2);
        d.Floors.Add(new ArchFloorPlan { Title = "MẶT BẰNG MÁI", FloorNo = 99, IsRoofPlan = true });
        var rd = GridFromPlan.FromDrawing(d);
        check("Bản vẽ nhiều tầng: chiều cao tầng \"3.9 3.6\" (bỏ mặt bằng mái)", B(rd.HeightsText == "3.9 3.6"), 1, 0);
        check("Bản vẽ rỗng: không ném lỗi, có ghi chú", B(GridFromPlan.FromDrawing(new ArchDrawing()).Notes.Count > 0), 1, 0);

        double mpp = GridFromPlan.MetresPerPixel((100, 200), (460, 200), 3.6);
        check("Ảnh: tỷ lệ 3,6 m / 360 điểm ảnh = 0,01 m/điểm ảnh", mpp, 0.01, 1e-12);
        var spx = GridFromPlan.SpansFromPixels(new[] { 100.0, 461.0, 879.0 }, mpp);
        check("Ảnh: bước trục bấm tay làm tròn 50 mm \"3.6 4.2\"", B(GridFromPlan.SpansText(spx) == "3.6 4.2"), 1, 0);
        check("Ảnh chưa hiệu chỉnh tỷ lệ → không có bước", GridFromPlan.SpansFromPixels(new[] { 1.0, 2.0 }, 0).Count, 0, 0);
    }
}
