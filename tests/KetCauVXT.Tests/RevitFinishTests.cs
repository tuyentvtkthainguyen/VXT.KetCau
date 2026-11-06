using System.Text.Json;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Hoàn thiện giao diện kiểu Revit: khối tường 3D quanh ô cửa, tay nắm kéo đầu / dời tường (cửa giữ vị trí, chặn trong tường),
/// hoàn tác – làm lại, sao chép / dời / đối xứng / mảng / offset tường và dầm theo điểm bấm, khoá bản vẽ nền CAD lưu trong dự án,
/// chữ nhãn giữ cỡ màn hình, Tab chuyển vòng.</summary>
public static class RevitFinishTests
{
    private static double B(bool b) => b ? 1 : 0;

    private static Project House()
    {
        var p = ModelBuilder.NewProject("Kiểm tra hoàn thiện kiểu Revit");
        ModelBuilder.SetGrid(p, new[] { 5.0, 4.0 }, new[] { 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        WallAssembly.Upgrade(p);
        return p;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var p = House();
        string st = p.Stories.OrderBy(s => s.Elevation).Skip(1).First().Name;
        const string wtName = "Gạch đặc 220 trát 2 mặt";

        // ---- 1. Khối tường 3D quanh ô cửa ----
        var w = new WallLine { Id = 1, Story = st, X1 = 0, Y1 = 0, X2 = 5, Y2 = 0, Type = wtName };
        p.Walls.Add(w);
        WallAssembly.PlaceOpening(p, w, "D1 900x2200", 1.0);       // [0,55; 1,45], bậu 0, đầu 2,2
        WallAssembly.PlaceOpening(p, w, "S1 1200x1400", 3.5);      // [2,9; 4,1], bậu 0,9, đầu 2,3
        double H = 3.0;
        var pcs = WallSolid3D.Pieces(p, w, H);
        // liền 0–0,55 | trên D1 | liền 1,45–2,9 | dưới S1 | trên S1 | liền 4,1–5
        check("3D: tường 2 cửa → 6 khối", pcs.Count, 6, 0);
        check("3D: cửa đi không có khối dưới bậu", pcs.Count(q => q.A >= 0.55 - 1e-9 && q.B <= 1.45 + 1e-9 && q.Z0 < 1e-9), 0, 0);
        check("3D: khối trên cửa đi cao 3,0 − 2,2 = 0,8", pcs.First(q => Math.Abs(q.A - 0.55) < 1e-9 && q.Z0 > 0).Height, 0.8, 1e-9);
        check("3D: khối dưới bậu cửa sổ cao 0,9", pcs.First(q => Math.Abs(q.A - 2.9) < 1e-9 && q.Z0 < 1e-9).Height, 0.9, 1e-9);
        double t = 0.25;
        double vol = WallSolid3D.Volume(pcs, t);
        check("3D: thể tích = (L·H − Σ b·h)·t", vol, (5 * 3 - 0.9 * 2.2 - 1.2 * 1.4) * t, 1e-9);
        check("3D: tường không cửa → 1 khối", WallSolid3D.Pieces(p, new WallLine { X2 = 4, Type = wtName }, H).Count, 1, 0);
        check("3D: cửa cao bằng tường → không có khối trên", WallSolid3D.Pieces(p, w, 2.2).Count(q => Math.Abs(q.A - 0.55) < 1e-9), 0, 0);
        double hDef = WallSolid3D.DefaultHeight(p, w);
        var stories = p.Stories.OrderBy(s => s.Elevation).ToList();
        int si = stories.FindIndex(s => s.Name == w.Story);
        if (si >= 0 && si + 1 < stories.Count)
            check("3D: chiều cao mặc định = cao tầng − 0,35", hDef, stories[si + 1].Elevation - stories[si].Elevation - 0.35, 1e-9);
        else
            check("3D: chiều cao mặc định > 0 (tầng " + w.Story + " không có tầng trên trong danh sách)", B(hDef > 0), 1, 0);

        // ---- 2. Tay nắm: kéo đầu tường, cửa giữ vị trí tuyệt đối ----
        var r1 = WallGrips.MoveEnd(p, w, false, 1.0 - 1.0, 0);   // không đổi
        check("Tay nắm: kéo về đúng chỗ cũ vẫn hợp lệ", B(r1.Ok), 1, 0);
        var r2 = WallGrips.MoveEnd(p, w, false, -1.0, 0);        // kéo dài đầu 1 thêm 1 m
        check("Tay nắm: kéo dài đầu 1 → dài 6 m", w.Chord, 6, 1e-9);
        check("Tay nắm: cửa D1 giữ vị trí tuyệt đối (tâm x = 1,0 → offset 2,0)", w.Openings[0].Offset, 2.0, 1e-9);
        check("Tay nắm: cửa S1 giữ vị trí (offset 4,5)", w.Openings[1].Offset, 4.5, 1e-9);
        var r3 = WallGrips.MoveEnd(p, w, true, 3.6, 0);          // rút ngắn đầu 2 tới x = 3,6 → S1 bị chặn trong tường
        check("Tay nắm: rút ngắn đầu 2 → hợp lệ", B(r3.Ok), 1, 0);
        double sOff = w.Openings[1].Offset;
        check("Tay nắm: cửa S1 chặn trong tường (mép phải ≤ L − 0,05)", B(sOff + 0.6 <= w.Chord - WallAssembly.MinEndClear + 1e-9), 1, 0);
        check("Tay nắm: hai cửa không chồng nhau sau khi chặn", B(!WallAssembly.Overlaps(p, w, 1.2, sOff, w.Openings[1].Id)), 1, 0);
        var len0 = w.Chord;
        var r4 = WallGrips.MoveEnd(p, w, true, -0.5, 0);         // còn 0,5 m → không đủ cho 2 cửa
        check("Tay nắm: rút quá ngắn so với cửa → báo lỗi", B(!r4.Ok), 1, 0);
        check("Tay nắm: lỗi → giữ nguyên tường", w.Chord, len0, 1e-9);
        var r5 = WallGrips.MoveEnd(p, w, true, -0.95, 0);        // dài 0,05 m < 0,1
        check("Tay nắm: tường < 100 mm → báo lỗi", B(!r5.Ok), 1, 0);
        double ox = w.X1;
        WallGrips.Move(w, 0.3, 0.2);
        check("Tay nắm giữa: dời cả tường", B(Math.Abs(w.X1 - (ox + 0.3)) < 1e-9 && Math.Abs(w.Y1 - 0.2) < 1e-9 && Math.Abs(w.Y2 - 0.2) < 1e-9), 1, 0);

        // ---- 3. Hoàn tác / làm lại ----
        var undo = new List<string>(); var redo = new List<string>();
        SnapshotHistory.Push(undo, redo, "A");
        SnapshotHistory.Push(undo, redo, "B");
        string cur = "C";
        cur = SnapshotHistory.Undo(undo, redo, cur);
        check("Hoàn tác: về B", B(cur == "B"), 1, 0);
        check("Hoàn tác: ngăn làm lại có 1", redo.Count, 1, 0);
        cur = SnapshotHistory.Undo(undo, redo, cur);
        check("Hoàn tác lần 2: về A", B(cur == "A"), 1, 0);
        cur = SnapshotHistory.Redo(undo, redo, cur);
        check("Làm lại: về B", B(cur == "B"), 1, 0);
        cur = SnapshotHistory.Redo(undo, redo, cur);
        check("Làm lại lần 2: về C", B(cur == "C"), 1, 0);
        check("Làm lại khi hết → null", B(SnapshotHistory.Redo(undo, redo, cur) == null), 1, 0);
        SnapshotHistory.Undo(undo, redo, cur);
        SnapshotHistory.Push(undo, redo, "D");
        check("Sửa mới → xoá ngăn làm lại", redo.Count, 0, 0);
        var big = new List<string>(); var bigR = new List<string>();
        for (int i = 0; i < 40; i++) SnapshotHistory.Push(big, bigR, i.ToString(), 30);
        check("Hoàn tác tối đa 30 bước", big.Count, 30, 0);

        // ---- 4. Sao chép / dời / đối xứng / offset tường ----
        var p2 = House();
        var wa = new WallLine { Id = 10, Story = st, X1 = 0, Y1 = 0, X2 = 5, Y2 = 0, Type = wtName };
        int n0 = p2.Walls.Count;
        p2.Walls.Add(wa);
        WallAssembly.PlaceOpening(p2, wa, "D1 900x2200", 1.0);
        var c1 = PlanEditOps.CopyWalls(p2, new[] { 10 }, 0, 4.5, 1);
        check("Sao chép tường: thêm 1", p2.Walls.Count, n0 + 2, 0);
        var wc = p2.Walls.Last();
        check("Sao chép tường: toạ độ dời (0; 4,5)", B(Math.Abs(wc.Y1 - 4.5) < 1e-9 && Math.Abs(wc.X2 - 5) < 1e-9), 1, 0);
        check("Sao chép tường: cửa chép theo, id mới", B(wc.Openings.Count == 1 && wc.Openings[0].Id != wa.Openings[0].Id && Math.Abs(wc.Openings[0].Offset - 1.0) < 1e-9), 1, 0);
        check("Sao chép lại đúng chỗ đã có tường → bỏ qua", B(!PlanEditOps.CopyWalls(p2, new[] { 10 }, 0, 4.5, 1).Ok), 1, 0);
        var c3 = PlanEditOps.CopyWalls(p2, new[] { 10 }, 0, -3, 3);
        check("Sao chép 3 bản: thêm 3 tường", p2.Walls.Count, n0 + 5, 0);
        check("Sao chép: khoảng 0 → lỗi", B(!PlanEditOps.CopyWalls(p2, new[] { 10 }, 0, 0, 1).Ok), 1, 0);
        PlanEditOps.MoveWalls(p2, new[] { 10 }, 1, 0);
        check("Dời tường (1; 0)", wa.X1, 1, 1e-9);
        // đối xứng qua đường x = y (trục nghiêng)
        var mp = PlanEditOps.MirrorPoint(2, 0, 0, 0, 1, 1);
        check("Đối xứng điểm (2;0) qua y = x → (0;2)", B(Math.Abs(mp.X) < 1e-9 && Math.Abs(mp.Y - 2) < 1e-9), 1, 0);
        var mp2 = PlanEditOps.MirrorPoint(3, 1, 5, 0, 5, 10);
        check("Đối xứng điểm (3;1) qua x = 5 → (7;1)", B(Math.Abs(mp2.X - 7) < 1e-9 && Math.Abs(mp2.Y - 1) < 1e-9), 1, 0);
        int nw = p2.Walls.Count;
        var mr = PlanEditOps.MirrorWalls(p2, new[] { 10 }, 0, 2.25, 10, 2.25);
        check("Đối xứng tường qua y = 2,25: thêm 1", p2.Walls.Count, nw + 1, 0);
        var wm = p2.Walls.Last();
        check("Đối xứng tường: y = 4,5", wm.Y1, 4.5, 1e-9);
        check("Đối xứng tường: cửa lật hướng mở", B(wm.Openings[0].Flip != wa.Openings[0].Flip), 1, 0);
        check("Đối xứng tường nằm trên trục → bỏ qua", B(!PlanEditOps.MirrorWalls(p2, new[] { 10 }, 0, 0, 10, 0).Ok), 1, 0);
        nw = p2.Walls.Count;
        var lift = PlanEditOps.MirrorWalls(p2, new[] { wm.Id }, 0, 3, 10, 3, keepOriginal: false);
        check("Lật tường (không giữ gốc): số tường không đổi", p2.Walls.Count, nw, 0);
        var off = PlanEditOps.OffsetWall(p2, wa, 1.2, 2, 1);
        check("Offset tường về phía điểm bấm (y > 0): tường mới y = 1,2", p2.Walls.Last().Y1, 1.2, 1e-9);
        check("Offset tường: không chép cửa", p2.Walls.Last().Openings.Count, 0, 0);
        PlanEditOps.OffsetWall(p2, wa, 1.2, 2, -1);
        check("Offset tường phía dưới: y = −1,2", p2.Walls.Last().Y1, -1.2, 1e-9);
        check("Offset: bấm trên tim → lỗi", B(!PlanEditOps.OffsetWall(p2, wa, 1.2, 2, 0).Ok), 1, 0);
        check("Phía điểm: trái = +1", PlanEditOps.Side(0, 0, 1, 0, 0.5, 1), 1, 0);
        check("Phía điểm: phải = −1", PlanEditOps.Side(0, 0, 1, 0, 0.5, -1), -1, 0);
        check("Khoảng cách có dấu (trái +)", PlanEditOps.SignedDistance(0, 0, 4, 0, 1, 2.5), 2.5, 1e-9);

        // ---- 5. Mảng ----
        var la = PlanEditOps.LinearArray(3, 0, 4);
        check("Mảng 4 phần tử bước 3 → 3 bản sao, dx = 3", B(la.Copies == 3 && Math.Abs(la.Dx - 3) < 1e-9), 1, 0);
        var last = PlanEditOps.LastArray(9, 0, 4);
        check("Mảng 'điểm cuối' 4 phần tử trên 9 m → bước 3", last.Dx, 3, 1e-9);
        check("Mảng 1 phần tử → 0 bản sao", PlanEditOps.LinearArray(3, 0, 1).Copies, 0, 0);

        // ---- 6. Đối xứng dầm qua trục nghiêng / trục song song ----
        var p3 = House();
        var beams = p3.Frames.Where(f => !f.IsVerticalKind && f.Story == st).ToList();
        var bx = beams.First(f => { var a = p3.Node(f.NodeI); var b = p3.Node(f.NodeJ); return Math.Abs(a.Y) < 1e-6 && Math.Abs(b.Y) < 1e-6; });
        int nf = p3.Frames.Count;
        var mb = PlanEditOps.MirrorBeams(p3, new[] { bx.Id }, 0, 1.5, 1, 1.5);   // trục song song X → BeamTools.Mirror (y = 3,0 chưa có dầm)
        check("Đối xứng dầm qua y = 1,5 (song song trục): thêm dầm", B(mb.Ok && p3.Frames.Count > nf), 1, 0);
        nf = p3.Frames.Count;
        var md = PlanEditOps.MirrorBeams(p3, new[] { bx.Id }, 0, 0, 1, 1);         // trục nghiêng y = x
        check("Đối xứng dầm qua y = x: thêm dầm", B(md.Ok && md.NewFrames.Count > 0), 1, 0);
        if (md.NewFrames.Count > 0)
        {
            var nb = p3.Frames.First(f => f.Id == md.NewFrames[0]);
            var na = p3.Node(nb.NodeI); var nc = p3.Node(nb.NodeJ);
            check("Dầm đối xứng qua y = x nằm trên trục x = 0", B(Math.Abs(na.X) < 1e-6 && Math.Abs(nc.X) < 1e-6), 1, 0);
            check("Dầm đối xứng giữ tiết diện", B(nb.Section == bx.Section), 1, 0);
        }
        check("Đối xứng: 2 điểm trùng → lỗi", B(!PlanEditOps.MirrorBeams(p3, new[] { bx.Id }, 1, 1, 1, 1).Ok), 1, 0);

        // ---- 7. Khoá bản vẽ nền CAD lưu trong dự án ----
        var p4 = House();
        p4.CadLinks.Add(new CadLink { Name = "KT T2", FilePath = "KT.dxf", Story = st, Locked = true, Opacity = 0.35, Visible = false, OffsetX = 1.5, Rotation = 12 });
        string json = JsonSerializer.Serialize(p4, Project.JsonOptions);
        var p5 = JsonSerializer.Deserialize<Project>(json, Project.JsonOptions);
        var l5 = p5.CadLinks.First();
        check("Link CAD: khoá lưu / mở", B(l5.Locked), 1, 0);
        check("Link CAD: độ mờ, ẩn, dời, xoay giữ nguyên", B(Math.Abs(l5.Opacity - 0.35) < 1e-9 && !l5.Visible && Math.Abs(l5.OffsetX - 1.5) < 1e-9 && Math.Abs(l5.Rotation - 12) < 1e-9 && l5.Story == st), 1, 0);
        var pOld = JsonSerializer.Deserialize<Project>("{\"CadLinks\":[{\"Name\":\"cu\",\"FilePath\":\"a.dxf\"}]}", Project.JsonOptions);
        check("Link CAD file cũ (không có Locked) → không khoá", B(!pOld.CadLinks[0].Locked), 1, 0);

        // ---- 8. Nhãn giữ cỡ màn hình, ẩn khi thu nhỏ ----
        check("Cỡ chữ màn hình không đổi khi zoom ×4", PlanLabelRules.ScreenFont(9, 40), PlanLabelRules.ScreenFont(9, 10), 1e-12);
        check("Chiều cao chữ quy ra m: 10 px ở 50 px/m = 0,2 m", PlanLabelRules.WorldHeight(10, 50), 0.2, 1e-12);
        check("Zoom gấp đôi → chiều cao chữ (m) giảm một nửa", PlanLabelRules.WorldHeight(10, 100), 0.1, 1e-12);
        check("Nhãn cấu kiện ẩn khi tỷ lệ < ngưỡng", B(!PlanLabelRules.Visible(PlanLabelRules.DefaultMinScale - 1, true)), 1, 0);
        check("Nhãn cấu kiện hiện khi tỷ lệ ≥ ngưỡng", B(PlanLabelRules.Visible(PlanLabelRules.DefaultMinScale, true)), 1, 0);
        check("Nhãn trục luôn hiện", B(PlanLabelRules.Visible(0.5, false)), 1, 0);

        // ---- 9. Tab chuyển vòng ----
        check("Tab: lần đầu → 0", HitCycle.Next(3, -1), 0, 0);
        check("Tab: 0 → 1", HitCycle.Next(3, 0), 1, 0);
        check("Tab: cuối → vòng lại 0", HitCycle.Next(3, 2), 0, 0);
        check("Tab: không ứng viên → −1", HitCycle.Next(0, 0), -1, 0);
        check("Tab: cùng danh sách ứng viên", B(HitCycle.Same(new[] { "Cột C1", "Dầm D1" }, new[] { "Cột C1", "Dầm D1" })), 1, 0);
        check("Tab: khác danh sách", B(!HitCycle.Same(new[] { "Cột C1" }, new[] { "Dầm D1" })), 1, 0);
    }
}
