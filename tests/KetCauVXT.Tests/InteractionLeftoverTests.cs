using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Thao tác giao diện còn lại: quét chọn trong khung nhìn 3D (chiếu khối lên màn hình), kéo trục bằng chuột (bắt 10 mm,
/// kích thước tạm, kéo theo cấu kiện), kéo đường cao độ trên mặt đứng, bong bóng trục từng đầu + ẩn trục theo tầng (lưu JSON,
/// vẽ DXF), hoàn tác / làm lại bộ bản vẽ Triển khai hồ sơ.</summary>
public static class InteractionLeftoverTests
{
    private static double B(bool b) => b ? 1 : 0;

    private static Project House()
    {
        var p = ModelBuilder.NewProject("Kiểm tra thao tác còn lại");
        ModelBuilder.SetGrid(p, new[] { 5.0, 4.0 }, new[] { 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        return p;
    }

    private static Solid Box(string id, double x, double y, double z, double half, double h) => new()
    {
        Id = id, Origin = new Vec3(x, y, z), XDir = Vec3.UnitX, ZDir = Vec3.UnitZ, Depth = h,
        Profile = new List<double[]> { new[] { -half, -half }, new[] { half, -half }, new[] { half, half }, new[] { -half, half } }
    };

    private static int Circles(DxfWriter w)
    {
        string s = w.ToDxfString();
        int n = 0, i = 0;
        while ((i = s.IndexOf("\nCIRCLE", i, StringComparison.Ordinal)) >= 0) { n++; i += 7; }
        return n;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---------------- 1. quét chọn 3D ----------------
        var pos = new Vec3(0, -20, 0.5); var look = new Vec3(0, 1, 0); var up = Vec3.UnitZ;
        const double W = 800, H = 600, Fov = 45;
        var c = Screen3DSelect.Project(pos, look, up, Fov, W, H, new Vec3(0, 0, 0.5));
        check("3D: điểm trên trục nhìn → giữa màn hình (x)", c.Value.X, W / 2, 1e-6);
        check("3D: điểm trên trục nhìn → giữa màn hình (y)", c.Value.Y, H / 2, 1e-6);
        check("3D: độ sâu = khoảng cách dọc hướng nhìn", c.Value.Depth, 20, 1e-9);
        check("3D: điểm sau camera → không chiếu", B(Screen3DSelect.Project(pos, look, up, Fov, W, H, new Vec3(0, -30, 0)) == null), 1, 0);
        var right = Screen3DSelect.Project(pos, look, up, Fov, W, H, new Vec3(2, 0, 0.5)).Value;
        var high = Screen3DSelect.Project(pos, look, up, Fov, W, H, new Vec3(0, 0, 2.5)).Value;
        check("3D: điểm bên phải → x lớn hơn tâm", B(right.X > W / 2), 1, 0);
        check("3D: điểm cao hơn → y nhỏ hơn tâm (màn hình hướng xuống)", B(high.Y < H / 2), 1, 0);
        // FOV ngang: điểm ở mép ngang khung nhìn x = depth·tan(fov/2) → x = W
        double edge = 20 * Math.Tan(Fov * Math.PI / 360);
        check("3D: FOV ngang – điểm biên phải chiếu tới x = W", Screen3DSelect.Project(pos, look, up, Fov, W, H, new Vec3(edge, 0, 0.5)).Value.X, W, 1e-6);

        var colA = Box("COT-11", 0, 0, 0, 0.2, 1);      // giữa màn hình
        var colB = Box("COT-12", 5, 0, 0, 0.2, 1);      // lệch phải
        var beam = Box("DAM-7", 2.5, 0, 0.8, 0.2, 0.2);
        var hole = Box("CUA-3-4", 0, 0, 0, 0.1, 0.5); hole.IsVoid = true;
        var solids = new List<Solid> { colA, colB, beam, hole };
        var rA = Screen3DSelect.ScreenRect(colA, pos, look, up, Fov, W, H).Value;
        var frameWin = SelectionRules.Box.Of(rA.X0 - 5, rA.Y0 - 5, rA.X1 + 5, rA.Y1 + 5);
        var win = Screen3DSelect.Select(solids, pos, look, up, Fov, W, H, frameWin, false);
        check("3D khung bao: chỉ cột nằm trọn khung (bỏ lỗ khoét)", win.Count, 1, 0);
        check("3D khung bao: đúng cột COT-11", B(win.Count == 1 && win[0].Id == "COT-11"), 1, 0);
        var rB = Screen3DSelect.ScreenRect(beam, pos, look, up, Fov, W, H).Value;
        var tiny = SelectionRules.Box.Of(rB.X0 + 1, rB.Y0 + 1, rB.X0 + 3, rB.Y0 + 3);
        check("3D khung bao nhỏ: không khối nào nằm trọn", Screen3DSelect.Select(solids, pos, look, up, Fov, W, H, tiny, false).Count, 0, 0);
        var cross = Screen3DSelect.Select(solids, pos, look, up, Fov, W, H, tiny, true);
        check("3D khung cắt: chạm dầm", B(cross.Any(s => s.Id == "DAM-7")), 1, 0);
        var all = Screen3DSelect.Select(solids, pos, look, up, Fov, W, H, SelectionRules.Box.Of(0, 0, W, H), false);
        check("3D khung toàn màn hình: 3 khối (không tính lỗ khoét)", all.Count, 3, 0);

        var it = Screen3DSelect.ToSelItem(colA).Value;
        check("Mã COT-11 → Cột 11", B(it.Cat == SelCat.Cot && it.Id == 11), 1, 0);
        var itC = Screen3DSelect.ToSelItem(new Solid { Id = "CUA-3-4" }).Value;
        check("Mã CUA-3-4 → Cửa 4 trên tường 3", B(itC.Cat == SelCat.Cua && itC.Id == 4 && itC.Host == 3), 1, 0);
        check("Mã DN-TB-5 → thiết bị 5", B(Screen3DSelect.ToSelItem(new Solid { Id = "DN-TB-5" }) is { Cat: SelCat.ThietBi, Id: 5 }), 1, 0);
        check("Mã DN-ONG-2-1 → tuyến ống 2", B(Screen3DSelect.ToSelItem(new Solid { Id = "DN-ONG-2-1" }) is { Cat: SelCat.TuyenOng, Id: 2 }), 1, 0);
        check("Mã lạ → không chọn", B(Screen3DSelect.ToSelItem(new Solid { Id = "MONG-1" }) == null), 1, 0);
        var items = Screen3DSelect.ToSelItems(new[] { colA, colA, beam, new Solid { Id = "COT-x" } });
        check("Danh sách chọn 3D: bỏ trùng, bỏ mã hỏng", items.Count, 2, 0);

        // 3D từ mô hình thật: mọi cột / dầm nhìn thấy khi khung bao toàn màn hình
        var ph = House();
        var ms = ModelSolids.Build(ph, null, false);
        var cam = new Vec3(4.5, -40, 4); var lk = new Vec3(0, 1, -0.05);
        var hitAll = Screen3DSelect.ToSelItems(Screen3DSelect.Select(ms, cam, lk, up, 60, W, H, SelectionRules.Box.Of(-1e6, -1e6, 1e6, 1e6), false));
        int nCol = ms.Count(s => s.Id.StartsWith("COT-"));
        check("3D mô hình: khung lớn chọn đủ mọi cột", hitAll.Count(x => x.Cat == SelCat.Cot), nCol, 0);
        check("3D mô hình: có dầm", B(hitAll.Any(x => x.Cat == SelCat.Dam)), 1, 0);

        // ---------------- 2. kéo trục ----------------
        check("Bắt 10 mm: 5.0049 → 5.00", GridDragMath.Snap(5.0049), 5.00, 1e-9);
        check("Bắt 10 mm: 5.0051 → 5.01", GridDragMath.Snap(5.0051), 5.01, 1e-9);
        check("Bắt 50 mm: 1.23 → 1.25", GridDragMath.Snap(1.23, 0.05), 1.25, 1e-9);
        var p = House();
        var gx = p.GridsX.ToList();
        var g2 = gx[1];
        double g2Old = g2.Coord, g1c = gx[0].Coord, g3c = gx[2].Coord;
        var (prev, next) = GridDragMath.Neighbours(p, g2);
        check("Trục kề trước đúng", B(prev == gx[0]), 1, 0);
        check("Trục kề sau đúng", B(next == gx[2]), 1, 0);
        var (dp, dn) = GridDragMath.TempDims(p, g2, g2Old + 0.3);
        check("Kích thước tạm tới trục trước", dp, g2Old + 0.3 - g1c, 1e-9);
        check("Kích thước tạm tới trục sau", dn, g3c - g2Old - 0.3, 1e-9);
        check("Trục biên: không có trục kề trước (NaN)", B(double.IsNaN(GridDragMath.TempDims(p, gx[0], 0).ToPrev)), 1, 0);
        check("Bấm gần trục (tol 0,2 m) → đúng trục", B(GridDragMath.HitGrid(p, g2Old + 0.1, -50, 0.2) == g2), 1, 0);
        int nodesOn = p.Nodes.Count(n => Math.Abs(n.X - g2Old) < 1e-6);
        var (newC, moved) = GridDragMath.Drop(p, g2, g2Old + 0.3047, true);
        check("Thả trục: toạ độ đã bắt 10 mm", newC, Math.Round(g2Old + 0.30, 6), 1e-6);
        check("Thả trục: kéo theo mọi nút trên trục", moved, nodesOn, 0);
        check("Thả trục: nút đã dời theo", p.Nodes.Count(n => Math.Abs(n.X - newC) < 1e-6), nodesOn, 0);
        var p2 = House();
        var h2 = p2.GridsX.ToList()[1]; double h2Old = h2.Coord;
        int on2 = p2.Nodes.Count(n => Math.Abs(n.X - h2Old) < 1e-6);
        var (_, moved2) = GridDragMath.Drop(p2, h2, h2Old - 0.5, false);
        check("Thả trục (Ctrl): không kéo theo cấu kiện", moved2, 0, 0);
        check("Thả trục (Ctrl): nút giữ nguyên", p2.Nodes.Count(n => Math.Abs(n.X - h2Old) < 1e-6), on2, 0);

        // ---------------- 3. kéo cao độ ----------------
        var pl = House();
        var ord = pl.Stories.OrderBy(s => s.Elevation).ToList();
        string s1 = ord[0].Name;
        double e1 = ord[0].Elevation, e2 = ord[1].Elevation, h1Old = ord[0].Height;
        check("Tìm tầng gần con trỏ (trong 0,3 m)", B(LevelDragMath.NearestStory(pl, e1 + 0.2, 0.3) == ord[0]), 1, 0);
        check("Không tầng nào gần → null", B(LevelDragMath.NearestStory(pl, e1 + 1.5, 0.3) == null), 1, 0);
        check("Kẹp cao độ: không vượt tầng trên − 0,5 m", LevelDragMath.Clamp(pl, s1, e2 + 3), e2 - 0.5, 1e-9);
        check("Kẹp cao độ: không thấp hơn chân + 0,5 m", LevelDragMath.Clamp(pl, s1, pl.BaseElevation - 5), pl.BaseElevation + 0.5, 1e-9);
        int nodesAtE1 = pl.Nodes.Count(n => Math.Abs(n.Z - e1) < 1e-6);
        double z = LevelDragMath.Drop(pl, s1, e1 + 0.2047);
        check("Thả cao độ: bắt 10 mm", z, Math.Round(e1 + 0.20, 6), 1e-6);
        check("Thả cao độ: chiều cao tầng dưới tăng 0,2 m", ord[0].Height, h1Old + 0.2, 1e-6);
        check("Thả cao độ: chiều cao tầng trên giảm 0,2 m", ord[1].Height, (e2 - e1) - 0.2, 1e-6);
        check("Thả cao độ: nút dời theo", pl.Nodes.Count(n => Math.Abs(n.Z - z) < 1e-6), nodesAtE1, 0);

        // ---------------- 4. bong bóng / ẩn trục ----------------
        var pb = House();
        var stA = pb.Stories.OrderBy(s => s.Elevation).First();
        int bubAll = GridDisplay.BubbleCount(pb, stA.Name);
        check("Mặc định: 2 bong bóng mỗi trục", bubAll, 2 * pb.Grids.Count(), 0);
        int circ0 = Circles(FramingPlan.Build(pb, null, stA, 100));
        var ga = pb.GridsX.First(); var gb = pb.GridsY.First();
        GridDisplay.SetMode(ga, GridBubbleMode.Start);
        check("Chế độ 'chỉ đầu dưới'", B(GridDisplay.Mode(ga) == GridBubbleMode.Start && ga.BubbleStart && !ga.BubbleEnd), 1, 0);
        GridDisplay.SetMode(gb, GridBubbleMode.None);
        check("Chế độ 'không bong bóng'", B(GridDisplay.Mode(gb) == GridBubbleMode.None), 1, 0);
        check("Đếm bong bóng sau khi đổi", GridDisplay.BubbleCount(pb, stA.Name), bubAll - 3, 0);
        int circ1 = Circles(FramingPlan.Build(pb, null, stA, 100));
        check("DXF mặt bằng: bớt 3 vòng tròn trục", circ0 - circ1, 3, 0);
        var gc = pb.GridsX.Last();
        GridDisplay.SetHidden(gc, stA.Name, true);
        GridDisplay.SetHidden(gc, stA.Name, true);   // gọi lại không nhân đôi
        check("Ẩn trục: lưu 1 lần", gc.HiddenStories.Count, 1, 0);
        check("Ẩn trục: không hiện trên tầng đó", B(GridDisplay.Visible(gc, stA.Name)), 0, 0);
        check("Ẩn trục: vẫn hiện ở 'mọi tầng'", B(GridDisplay.Visible(gc, null)), 1, 0);
        int circ2 = Circles(FramingPlan.Build(pb, null, stA, 100));
        check("DXF: trục ẩn không vẽ bong bóng trên tầng", circ1 - circ2, 2, 0);
        var stB = pb.Stories.OrderBy(s => s.Elevation).Last();
        check("Trục ẩn tầng này vẫn hiện tầng khác", B(GridDisplay.Visible(gc, stB.Name)), 1, 0);
        string path = Path.Combine(outDir, "TacVuConLai_TrucBongBong.kcvxt");
        pb.Save(path);
        var q = Project.Load(path);
        var qa = q.GridsX.First(); var qb = q.GridsY.First(); var qc = q.GridsX.Last();
        check("Lưu/mở: giữ bong bóng một đầu", B(qa.BubbleStart && !qa.BubbleEnd), 1, 0);
        check("Lưu/mở: giữ không bong bóng", B(!qb.BubbleStart && !qb.BubbleEnd), 1, 0);
        check("Lưu/mở: giữ trục ẩn theo tầng", B(qc.HiddenStories.Contains(stA.Name)), 1, 0);
        string oldJson = "{\"Name\":\"A\",\"Dir\":0,\"Coord\":1.5}";
        var old = System.Text.Json.JsonSerializer.Deserialize<GridLine>(oldJson);
        check("File cũ: trục mặc định hai bong bóng", B(old.BubbleStart && old.BubbleEnd), 1, 0);
        check("File cũ: không ẩn tầng nào", B(GridDisplay.Visible(old, stA.Name)), 1, 0);
        GridDisplay.SetHidden(gc, stA.Name, false);
        check("Hiện lại trục", B(GridDisplay.Visible(gc, stA.Name)), 1, 0);

        // ---------------- 5. hoàn tác bộ bản vẽ ----------------
        var set = new DetailSheetSet();
        set.Sheets.Add(new DetailSheet { No = "KC-01", Title = "Dầm" });
        var u = new SheetUndoStack();
        check("Ngăn hoàn tác rỗng ban đầu", B(!u.CanUndo && !u.CanRedo), 1, 0);
        check("Hoàn tác khi rỗng → giữ nguyên", B(ReferenceEquals(u.Undo(set), set)), 1, 0);
        u.Push(set);
        set.Sheets.Add(new DetailSheet { No = "KC-02", Title = "Cột" });
        u.Push(set);
        set.Sheets[0].Title = "Dầm tầng 1";
        int nNow = set.Sheets.Count; string tNow = set.Sheets[0].Title;
        check("Hai bước hoàn tác", u.UndoCount, 2, 0);
        var b1 = u.Undo(set);
        check("Hoàn tác 1: bỏ đổi tên", B(b1.Sheets[0].Title == "Dầm" && b1.Sheets.Count == 2), 1, 0);
        var b2 = u.Undo(b1);
        check("Hoàn tác 2: bỏ tờ thêm", b2.Sheets.Count, 1, 0);
        check("Làm lại sẵn 2 bước", u.RedoCount, 2, 0);
        var r1 = u.Redo(b2);
        check("Làm lại 1: có lại tờ KC-02", B(r1.Sheets.Count == 2 && r1.Sheets[1].No == "KC-02"), 1, 0);
        var r2 = u.Redo(r1);
        check("Làm lại 2: tên mới", B(r2.Sheets.Count == nNow && r2.Sheets[0].Title == tNow), 1, 0);
        u.Undo(r2);
        u.Push(r2);
        check("Sửa mới sau hoàn tác → xoá ngăn làm lại", B(!u.CanRedo), 1, 0);
        var cap = new SheetUndoStack { Capacity = 3 };
        for (int i = 0; i < 6; i++) cap.Push(set);
        check("Giới hạn số bước hoàn tác", cap.UndoCount, 3, 0);
        check("Ảnh chụp null = chưa dàn trang", B(SheetUndoStack.Restore(SheetUndoStack.Snap(null)) == null), 1, 0);
    }
}
