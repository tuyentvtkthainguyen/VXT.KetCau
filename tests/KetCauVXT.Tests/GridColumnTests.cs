using KetCauVXT.Mep;
using KetCauVXT.Model;
using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;

namespace KetCauVXT.Tests;

/// <summary>Lưới trục + lưới cột + chiều cao tầng trên một hộp thoại (Model/GridColumnSetup.cs).</summary>
public static class GridColumnTests
{
    private static double B(bool b) => b ? 1 : 0;

    private static GridColumnPlan Plan3x3()
    {
        var pl = new GridColumnPlan { SpansX = new() { 4, 4 }, SpansY = new() { 5, 5 }, FirstX = "1", FirstY = "A", BaseElevation = -0.6 };
        pl.Stories.Add(new StoryRow { Name = "T2", Height = 4.2 });
        pl.Stories.Add(new StoryRow { Name = "MAI", Height = 3.3 });
        pl.ResizeMask();
        return pl;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---- tên trục ----
        var n1 = GridColumnSetup.AxisNames("1", 4);
        check("GC tên trục số 1..4", B(string.Join(",", n1) == "1,2,3,4"), 1, 0);
        check("GC tên trục chữ Y..AB", B(string.Join(",", GridColumnSetup.AxisNames("Y", 4)) == "Y,Z,AA,AB"), 1, 0);
        check("GC tên trục X1..X3", B(string.Join(",", GridColumnSetup.AxisNames("X1", 3)) == "X1,X2,X3"), 1, 0);
        check("GC tên tầng mặc định", B(string.Join(",", GridColumnSetup.DefaultStoryNames(3)) == "T2,T3,MAI"), 1, 0);

        // ---- ma trận cột ----
        var pl = Plan3x3();
        check("GC mặc định 9 vị trí cột", pl.ColumnCount, 9, 0);
        pl.SetPerimeter();
        check("GC chỉ cột biên 8", pl.ColumnCount, 8, 0);
        pl.ToggleRow(1);   // trục B: có cột biên → bỏ cả trục
        check("GC đảo trục B → 6", pl.ColumnCount, 6, 0);
        pl.SpansX.Add(3); pl.ResizeMask();
        check("GC thêm nhịp X: giao điểm mới có cột", pl.ColumnCount, 6 + 3, 0);
        check("GC lỗi trùng tên trục", B(GridColumnSetup.Validate(new GridColumnPlan { SpansX = new() { 3 }, SpansY = new() { 3 }, FirstX = "1", FirstY = "1", Stories = new() { new StoryRow { Name = "T2", Height = 3 } } }) != null), 1, 0);

        // ---- bảng trục (như bảng cao độ): tên từng trục, chèn / xoá trục giữ đúng cột ----
        var pn = Plan3x3();
        pn.NamesX = new() { "1", "2", "3" }; pn.NamesY = new() { "A", "B", "C" };
        pn.Columns[1, 1] = false;                          // 2-B không cột
        pn.SpansX = new() { 4, 2, 2 }; pn.NamesX = new() { "1", "2", "2'", "3" };
        pn.InsertMaskAxis(GridDir.X, 2);                   // chèn trục 2' giữa 2 và 3
        check("GC chèn trục 2': 4 trục X", pn.Columns.GetLength(0), 4, 0);
        check("GC chèn trục: 2-B vẫn không cột, 3-B vẫn có cột", B(!pn.Columns[1, 1] && pn.Columns[3, 1] && pn.Columns[2, 1]), 1, 0);
        check("GC tên trục theo bảng", B(string.Join(",", pn.Names(GridDir.X)) == "1,2,2',3"), 1, 0);
        pn.RemoveMaskAxis(GridDir.X, 1);                   // xoá trục 2
        pn.SpansX = new() { 6, 2 }; pn.NamesX = new() { "1", "2'", "3" };
        check("GC xoá trục 2: còn 3 trục, trục 2' giữ cột", B(pn.Columns.GetLength(0) == 3 && pn.Columns[1, 1]), 1, 0);
        pn.NamesY = new() { "A", "A", "C" };
        check("GC tên trục Y trùng → báo lỗi", B(GridColumnSetup.Validate(pn) != null), 1, 0);
        pn.NamesY = new() { "A", "B", "D" };
        var pq = ModelBuilder.NewProject();
        GridColumnSetup.Rebuild(pq, pn, new ModelBuilder.GridFrameOptions());
        check("GC tạo lưới theo tên trong bảng (2', D)", B(pq.GridsX.Any(g => g.Name == "2'" && Math.Abs(g.Coord - 6) < 1e-9) && pq.GridsY.Any(g => g.Name == "D")), 1, 0);
        var pback = GridColumnSetup.FromProject(pq);
        check("GC đọc lại tên trục từ dự án", B(string.Join(",", pback.Names(GridDir.X)) == "1,2',3"), 1, 0);

        // ---- tạo lại khung, bỏ cột giữa ----
        var p = ModelBuilder.NewProject();
        var plan = Plan3x3();
        plan.Columns[1, 1] = false;
        var (rm, msg) = GridColumnSetup.Rebuild(p, plan, new ModelBuilder.GridFrameOptions());
        int cols = p.Frames.Count(f => f.Kind == FrameKind.Column);
        check("GC cột = 8 vị trí × 2 tầng", cols, 16, 0);
        check("GC xoá 2 thanh cột giữa", rm, 2, 0);
        check("GC cao độ MAI = −0,6 + 4,2 + 3,3", p.Stories.First(s => s.Name == "MAI").Elevation, 6.9, 1e-9);
        bool centerFree = !p.Nodes.Any(n => Math.Abs(n.X - 4) < 1e-6 && Math.Abs(n.Y - 5) < 1e-6 && n.HasRestraint);
        check("GC giao điểm 2-B không còn gối", B(centerFree), 1, 0);
        bool beamThrough = p.Frames.Any(f => f.Kind == FrameKind.Beam && f.Story == "T2" && new[] { p.Node(f.NodeI), p.Node(f.NodeJ) }.Any(n => Math.Abs(n.X - 4) < 1e-6 && Math.Abs(n.Y - 5) < 1e-6));
        check("GC dầm vẫn qua giao điểm 2-B", B(beamThrough), 1, 0);
        check("GC thông báo có 'dầm đỡ dầm'", B(msg.Contains("dầm đỡ dầm")), 1, 0);

        // ---- đọc lại từ dự án ----
        var back = GridColumnSetup.FromProject(p);
        check("GC đọc lại 2 nhịp X", back.SpansX.Count, 2, 0);
        check("GC đọc lại chiều cao T2", back.Stories[0].Height, 4.2, 1e-9);
        check("GC đọc lại giao điểm 2-B không cột", B(!back.Columns[1, 1] && back.Columns[0, 0]), 1, 0);

        // ---- chỉ đổi chiều cao tầng, giữ mô hình ----
        int frames0 = p.Frames.Count;
        p.Mep ??= new MepModel();
        p.Mep.Runs.Add(new MepRun { Id = 1, Story = "MAI", Path = new() { new MepPt(0, 0, 6.85), new MepPt(4, 0, 6.85) } });
        var mid = p.GetOrAddNode(0, 0, 1.5);    // nút giữa tầng 1 (vd chiếu nghỉ): −0,6 → 3,6 → nội suy
        int moved = GridColumnSetup.ChangeStoryHeights(p, new List<double> { 4.5, 3.6 });
        check("GC đổi chiều cao: số thanh giữ nguyên", p.Frames.Count, frames0, 0);
        check("GC cao độ T2 mới = 3,9", p.Stories.First(s => s.Name == "T2").Elevation, 3.9, 1e-9);
        check("GC cao độ MAI mới = 7,5", p.Stories.First(s => s.Name == "MAI").Elevation, 7.5, 1e-9);
        check("GC chiều cao MAI = 3,6", p.Stories.First(s => s.Name == "MAI").Height, 3.6, 1e-9);
        check("GC nút sàn mái dời lên 7,5", B(p.Nodes.Count(n => Math.Abs(n.Z - 7.5) < 1e-6) >= 8), 1, 0);
        check("GC không còn nút ở 6,9", p.Nodes.Count(n => Math.Abs(n.Z - 6.9) < 1e-6), 0, 0);
        check("GC chân cột giữ −0,6", B(p.Nodes.Any(n => Math.Abs(n.Z + 0.6) < 1e-6 && n.HasRestraint)), 1, 0);
        // 1,5 cách T2 cũ (3,6) 2,1 m > 0,5 → nội suy: −0,6 + (2,1/4,2)·4,5 = 1,65
        check("GC nút giữa tầng nội suy 1,65", mid.Z, 1.65, 1e-6);
        check("GC ống điện nước theo mái (6,85 → 7,45)", p.Mep.Runs[0].Path[1].Z, 7.45, 1e-6);
        check("GC số nút dời > 0", B(moved > 0), 1, 0);
        bool colsOk = p.Frames.Where(f => f.Kind == FrameKind.Column && f.Story == "MAI").All(f => Math.Abs(Math.Abs(p.Node(f.NodeJ).Z - p.Node(f.NodeI).Z) - 3.6) < 1e-6);
        check("GC cột tầng MAI dài 3,6", B(colsOk), 1, 0);
        bool threw = false;
        try { GridColumnSetup.ChangeStoryHeights(p, new List<double> { 3.3 }); } catch (ArgumentException) { threw = true; }
        check("GC sai số tầng → báo lỗi", B(threw), 1, 0);

        // ---- xoá móng đã thiết kế để thiết kế lại; chốt kích thước móng sửa trên mặt bằng ----
        {
            var pf = ModelBuilder.NewProject();
            var ds = new DesignSummary();
            ds.Footings.Add(new FootingDesign { NodeId = 1, L = 1.8, B = 1.5, RebarKeys = { "M:1" } });
            ds.Footings.Add(new FootingDesign { NodeId = 2, L = 2.0, B = 1.6, RebarKeys = { "M:2" } });
            ds.PileCaps.Add(new PileCapDesign { NodeIds = { 3 }, RebarKeys = { "P:3" } });
            pf.Nodes.Add(new Node { Id = 1 }); pf.Nodes.Add(new Node { Id = 2 }); pf.Nodes.Add(new Node { Id = 3 });
            pf.Footings.Add(new Footing { Label = "M1", NodeId = 1, L = 1.8, B = 1.5, Height = 0.5 });
            pf.Footings.Add(new Footing { Label = "M2", NodeId = 2, L = 2.0, B = 1.6, Type = FoundationType.Isolated });
            pf.Foundation.CapOverrides.Add(new PileCapOverride { NodeId = 3 });
            pf.RebarChoices.Add(new RebarChoice { Member = "M:1" }); pf.RebarChoices.Add(new RebarChoice { Member = "P:3" }); pf.RebarChoices.Add(new RebarChoice { Member = "B:7" });
            var r1 = FoundationReset.Clear(pf, ds, new[] { 1 });
            check("MÓNG xoá móng chọn: 1 móng đơn", r1.Footings, 1, 0);
            check("MÓNG xoá móng chọn: còn móng 2 + đài 3", ds.Footings.Count + ds.PileCaps.Count, 2, 0);
            check("MÓNG xoá móng chọn: bỏ kích thước chốt nút 1", B(pf.Footings.All(f => f.NodeId != 1)), 1, 0);
            check("MÓNG xoá móng chọn: bỏ thép đã chọn M:1, giữ thép dầm", B(pf.RebarChoices.All(r => r.Member != "M:1") && pf.RebarChoices.Any(r => r.Member == "B:7")), 1, 0);
            var r2 = FoundationReset.Clear(pf, ds);
            check("MÓNG xoá tất cả: móng 2 + đài 3", r2.Footings + r2.PileCaps, 2, 0);
            check("MÓNG xoá tất cả: bố trí cọc người dùng bỏ", pf.Foundation.CapOverrides.Count, 0, 0);
            var m2 = pf.Footings.FirstOrDefault(f => f.NodeId == 2);
            check("MÓNG xoá tất cả: giữ loại móng riêng, kích thước về 0 (tự chọn lại)", B(m2 != null && m2.Type == FoundationType.Isolated && m2.L == 0 && m2.B == 0), 1, 0);
            check("MÓNG xoá tất cả: kết quả rỗng", ds.Footings.Count + ds.PileCaps.Count, 0, 0);
            var sz = FoundationReset.SetSize(pf, 1, 2.2, 1.8, 0.6);
            check("MÓNG sửa trên mặt bằng: chốt 2,2 × 1,8 × 0,6", B(sz.L == 2.2 && sz.B == 1.8 && sz.Height == 0.6 && pf.Footings.Count(f => f.NodeId == 1) == 1), 1, 0);
            check("MÓNG sửa trên mặt bằng: nhãn mới không trùng", B(pf.Footings.Select(f => f.Label).Distinct().Count() == pf.Footings.Count), 1, 0);
        }
    }
}
