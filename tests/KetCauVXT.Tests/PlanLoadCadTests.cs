using System.Text.Json;
using KetCauVXT.Model;
using KetCauVXT.Ui;

namespace KetCauVXT.Tests;

/// <summary>Thanh lệnh cấu kiện trên mặt bằng (MainWindow.MemberRibbon.cs / CadCommandLine.cs): chọn nhiều window / crossing,
/// chọn tương tự, gán tải cho nhiều dầm / ô sàn / cột (thay thế – cộng thêm – xoá, theo trường hợp tải), tải tường,
/// tường CAD trên dầm, dòng lệnh kiểu AutoCAD (lệnh tắt, toạ độ, trực giao / góc cực), ưu tiên bắt điểm, căn bản vẽ liên kết 2 điểm,
/// đặt cột, offset / mảng dầm. check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class PlanLoadCadTests
{
    private static double B(bool b) => b ? 1 : 0;

    private static Project House()
    {
        var p = ModelBuilder.NewProject("Kiểm tra gán tải mặt bằng");
        ModelBuilder.SetGrid(p, new[] { 5.0, 4.0 }, new[] { 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);   // T2 (+3,0), T3 (+6,3)
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        return p;
    }

    private static List<FrameElement> Beams(Project p, string st) => p.Frames.Where(f => !f.IsVerticalKind && f.Story == st).ToList();

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var p = House();
        const string st = "T2";
        int nBeams = Beams(p, st).Count, nCols = p.Frames.Count(f => f.IsVerticalKind && f.Story == st), nSlabs = p.Slabs.Count(s => s.Story == st);
        check("Nhà mẫu: 7 dầm, 6 cột, 2 ô sàn mỗi tầng", nBeams * 100 + nCols * 10 + nSlabs, 7 * 100 + 6 * 10 + 2, 0);

        // ---- 1. Window vs crossing ----
        var win = SelectionRules.Box.Of(-0.5, -0.5, 5.5, 5.0);   // bao trọn ô trái (x 0…5)
        var bw = SelectionRules.Beams(p, st, win, false);
        var bc = SelectionRules.Beams(p, st, win, true);
        check("Window: dầm nằm trọn trong khung (4 dầm ô trái)", bw.Count, 4, 0);
        check("Crossing: thêm 2 dầm chạm khung (6 dầm)", bc.Count, 6, 0);
        check("Window: 4 cột trong khung", SelectionRules.Columns(p, st, win, false).Count, 4, 0);
        check("Window: 1 ô sàn trọn trong khung", SelectionRules.Slabs(p, st, win, false).Count, 1, 0);
        check("Crossing: 2 ô sàn chạm khung", SelectionRules.Slabs(p, st, win, true).Count, 2, 0);
        var inner = SelectionRules.Box.Of(1, 1, 2, 2);   // khung nhỏ nằm giữa ô sàn
        check("Crossing: khung nằm trong ô sàn vẫn chọn ô đó", SelectionRules.Slabs(p, st, inner, true).Count, 1, 0);
        check("Window: khung nhỏ trong ô không chọn gì", SelectionRules.Slabs(p, st, inner, false).Count + SelectionRules.Beams(p, st, inner, false).Count, 0, 0);
        check("Đoạn chéo cắt khung (Liang–Barsky)", B(SelectionRules.SegmentHits(inner, 0, 0, 3, 3)), 1, 0);
        check("Đoạn ngoài khung", B(SelectionRules.SegmentHits(inner, 0, 3, 3, 3)), 0, 0);

        // ---- 2. Chọn tương tự ----
        var b0 = Beams(p, st)[0];
        b0.Section = "C22x30";   // đổi tiết diện 1 dầm (tên tiết diện khác) – không lẫn với cột cùng tên
        var simSec = SelectionRules.SimilarFrames(p, new[] { Beams(p, st)[1].Id }, SimilarBy.Section, st);
        check("Tương tự cùng tiết diện D22x30 tầng T2: 6 dầm", simSec.Count, nBeams - 1, 0);
        check("Tương tự cùng tiết diện mọi tầng: 6 + 7 dầm", SelectionRules.SimilarFrames(p, new[] { Beams(p, st)[1].Id }, SimilarBy.Section).Count, p.Frames.Count(f => !f.IsVerticalKind && f.Section == Beams(p, st)[1].Section), 0);
        check("Tương tự mọi tầng nhiều hơn chỉ tầng đang xem", B(SelectionRules.SimilarFrames(p, new[] { Beams(p, st)[1].Id }, SimilarBy.Section).Count > nBeams - 1), 1, 0);
        var colId = p.Frames.First(f => f.IsVerticalKind && f.Story == st).Id;
        check("Tương tự cột: chỉ cột (không lẫn dầm)", SelectionRules.SimilarFrames(p, new[] { colId }, SimilarBy.Story, st).Count, nCols, 0);
        var slabT2 = p.Slabs.Where(s => s.Story == st).ToList();
        ModelEdit.SetSlabUse(p, slabT2[1], SlabUse.WC);
        check("Ô sàn tương tự cùng công năng: 1 ô (ô kia là WC)", SelectionRules.SimilarSlabs(p, new[] { slabT2[0].Id }, SimilarBy.Kind, st).Count, 1, 0);
        check("Ô sàn cùng tầng: 2 ô", SelectionRules.SimilarSlabs(p, new[] { slabT2[0].Id }, SimilarBy.Story).Count, 2, 0);

        // ---- 3. Gán tải nhiều dầm: thay thế / cộng thêm / xoá; giữ tải tự sinh; theo trường hợp ----
        var ids = bw;
        p.FrameLoads.Add(new FrameLoad { Element = ids[0], Pattern = "HTHT", Source = "SLAB", Points = { new[] { 0.0, 9.0 }, new[] { 1.0, 9.0 } } });   // tải tự sinh – không được đụng
        var r1 = LoadAssign.FrameUniform(p, ids, "HTHT", 7.5, LoadAssignMode.Replace);
        check("Gán q = 7,5 kN/m cho 4 dầm", r1.Changed, 4, 0);
        check("Giá trị chung 4 dầm = 7,5", B(LoadAssign.Common(ids.Select(id => LoadAssign.FrameUniformOf(p, id, "HTHT"))) == "7,5"), 1, 0);
        LoadAssign.FrameUniform(p, ids, "HTHT", 7.5, LoadAssignMode.Replace);
        check("Thay thế lần 2 không nhân đôi tải", LoadAssign.FrameUniformOf(p, ids[0], "HTHT") ?? 0, 7.5, 1e-9);
        LoadAssign.FrameUniform(p, ids.Take(1), "HTHT", 2.0, LoadAssignMode.Add);
        check("Cộng thêm 2 kN/m cho 1 dầm → 9,5", LoadAssign.FrameUniformOf(p, ids[0], "HTHT") ?? 0, 9.5, 1e-9);
        check("Giá trị chung → <khác nhau>", B(LoadAssign.Common(ids.Select(id => LoadAssign.FrameUniformOf(p, id, "HTHT"))) == LoadAssign.Different), 1, 0);
        LoadAssign.FramePoint(p, ids, "HTHT", 12, 0.5, LoadAssignMode.Replace);
        LoadAssign.FrameUniform(p, ids, "HTHT", 6, LoadAssignMode.Replace);
        check("Thay tải phân bố không xoá lực tập trung", p.FrameLoads.Count(l => ids.Contains(l.Element) && l.Pattern == "HTHT" && l.PointLoads.Count > 0), 4, 0);
        LoadAssign.FrameUniform(p, ids, "HT", 1.5, LoadAssignMode.Replace);
        check("Trường hợp khác (HT) tách riêng: HTHT vẫn 6", LoadAssign.FrameUniformOf(p, ids[1], "HTHT") ?? 0, 6, 1e-9);
        LoadAssign.FrameUniform(p, ids, "HTHT", 0, LoadAssignMode.Delete);
        LoadAssign.FramePoint(p, ids, "HTHT", 0, 0, LoadAssignMode.Delete);
        LoadAssign.FrameUniform(p, ids, "HT", 0, LoadAssignMode.Delete);
        check("Xoá: không còn tải nhập tay trên các dầm", p.FrameLoads.Count(l => ids.Contains(l.Element) && l.Source == ""), 0, 0);
        check("Tải tự sinh (SLAB) không bị đụng", p.FrameLoads.Count(l => l.Element == ids[0] && l.Source == "SLAB"), 1, 0);
        check("Trường hợp không có → báo lỗi", B(!LoadAssign.FrameUniform(p, ids, "KHONGCO", 1, LoadAssignMode.Replace).Ok), 1, 0);

        // ---- hoàn tác = khôi phục ảnh chụp JSON (như MainWindow.Snapshot) ----
        string snap = JsonSerializer.Serialize(p, Project.JsonOptions);
        var sl = p.Slabs.Where(s => s.Story == st).Select(s => s.Id).ToList();
        double q0 = LoadAssign.SlabQOf(p, sl[0], "HTHT") ?? 0;   // hoàn thiện sinh khi dựng mô hình (nhập tay)
        LoadAssign.Slabs(p, sl, "HTHT", 1.2, LoadAssignMode.Replace);
        LoadAssign.Slabs(p, sl.Take(1), "HTHT", 0.8, LoadAssignMode.Add);
        check("Tải sàn: ô 1 = 1,2 + 0,8", LoadAssign.SlabQOf(p, sl[0], "HTHT") ?? 0, 2.0, 1e-9);
        check("Tải sàn chung → <khác nhau>", B(LoadAssign.Common(sl.Select(id => LoadAssign.SlabQOf(p, id, "HTHT"))) == LoadAssign.Different), 1, 0);
        var undo = JsonSerializer.Deserialize<Project>(snap, Project.JsonOptions);
        check("Hoàn tác (một bước) trả lại tải sàn trước khi gán", LoadAssign.SlabQOf(undo, sl[0], "HTHT") ?? 0, q0, 1e-9);
        check("Thay thế tải sàn: bỏ tải hoàn thiện cũ cùng trường hợp", B(p.AreaLoads.Count(a => a.Slab == sl[1] && a.Pattern == "HTHT" && a.Source == "") == 1), 1, 0);

        // ---- cột: tải đầu cột / chân cột ----
        var cols = p.Frames.Where(f => f.IsVerticalKind && f.Story == st).Take(2).ToList();
        var tops = cols.Select(c => LoadAssign.ColumnNodes(p, c).Top).ToList();
        var bots = cols.Select(c => LoadAssign.ColumnNodes(p, c).Bottom).ToList();
        check("Nút đỉnh cột T2 ở cao độ +3,0", p.Nodes.First(n => n.Id == tops[0]).Z, 3.0, 1e-9);
        LoadAssign.Nodes(p, tops, "HTHT", 50, 5, 0, LoadAssignMode.Replace);
        check("N đầu cột = 50 kN (Fz = −50)", p.NodeLoads.First(l => l.Node == tops[0] && l.Pattern == "HTHT").Fz, -50, 1e-9);
        check("Giá trị chung N = 50 kN", B(LoadAssign.Common(tops.Select(n => LoadAssign.NodeFzOf(p, n, "HTHT")), "kN") == "50 kN"), 1, 0);
        LoadAssign.Nodes(p, bots, "HTHT", 30, 0, 0, LoadAssignMode.Replace);
        check("Tải chân cột (móng) gán vào nút chân", LoadAssign.NodeFzOf(p, bots[1], "HTHT") ?? 0, 30, 1e-9);

        // ---- 4. Tải tường ----
        var w220 = p.WallTypes.First(t => t.Name == "Tường 220 trát 2 mặt");
        double g = 0.22 * 18 + 2 * 0.015 * 18;   // 4,5 kN/m²
        check("g tường 220 trát 2 mặt = 4,5 kN/m²", w220.WeightPerM2, g, 1e-9);
        check("q tường trên dầm = g·h = 4,5 × 3,0", LoadAssign.WallLineLoad(w220, 3.0, 0), 13.5, 1e-9);
        check("q tường có 30% cửa = 4,5 × 3 × 0,7", LoadAssign.WallLineLoad(w220.WeightPerM2, 3.0, 0.30), 9.45, 1e-9);
        check("Tường trên sàn quy đổi q·L/A = 13,5 × 4 / 22,5", LoadAssign.WallOnSlabEquivalent(g, 3.0, 4.0, 22.5), 2.4, 1e-9);
        check("Diện tích ô sàn 5 × 4,5", LoadAssign.SlabArea(p, p.Slabs.First(s => s.Story == st)), 22.5, 1e-6);
        check("Khe 110 → tường 110; 220 → 220; 150 → không phải tường",
            LoadAssign.WallThicknessFromGap(0.11) * 1000 + LoadAssign.WallThicknessFromGap(0.22) * 10 + LoadAssign.WallThicknessFromGap(0.15), 110 + 2.2, 1e-9);

        // ---- 5. Tường CAD nằm trên dầm → đề xuất tường ----
        var lines = new List<(double, double, double, double)>
        {
            (0, -0.11, 5, -0.11), (0, 0.11, 5, 0.11),          // tường 220 trên dầm trục A (y = 0), x 0…5
            (2.0, 2.0, 2.0, 4.0), (2.11, 2.0, 2.11, 4.0),      // tường 110 giữa ô – không có dầm bên dưới
            (9, 1.0, 9, 3.5), (9.11, 1.0, 9.11, 3.5),          // tường 110 trên dầm trục 3 (x = 9) đoạn 1…3,5
            (5, 4.5, 9, 4.5),                                  // một nét đơn – không ghép
        };
        var props = LoadAssign.CadWallsOverBeams(p, st, lines);
        check("Tường CAD: 2 đề xuất (trục A 220, trục 3 110)", props.Count, 2, 0);
        var pa = props.FirstOrDefault(w => Math.Abs(w.Thickness - 0.22) < 1e-9);
        check("Tường 220 trên trục A dài 5 m", pa?.Length ?? 0, 5.0, 1e-6);
        var p3 = props.FirstOrDefault(w => Math.Abs(w.Thickness - 0.11) < 1e-9);
        check("Tường 110 trên trục 3 dài 2,5 m (phần chồng dầm)", p3?.Length ?? 0, 2.5, 1e-6);
        check("Tim tường 110 lệch 55 mm vẫn nhận (≤ t/2 + 50)", B(p3 != null && Math.Abs(p3.X1 - 9.0) < 1e-6), 1, 0);
        int walls0 = p.Walls.Count;
        var ap = LoadAssign.ApplyCadWalls(p, st, props);
        check("Áp dụng: tạo 2 đoạn tường", p.Walls.Count - walls0, 2, 0);
        check("Loại tường theo chiều dày (220 đặc)", B(p.Walls.Any(w => w.Type.StartsWith("Tường 220"))), 1, 0);

        // ---- 6. Dòng lệnh ----
        check("L → dầm", B(CadCommand.Resolve("l") == CadCmd.Beam), 1, 0);
        check("COT / cột (có dấu) → cột", B(CadCommand.Resolve("COT") == CadCmd.Column && CadCommand.Resolve("cột") == CadCmd.Column), 1, 0);
        check("CO → sao chép, C → cột", B(CadCommand.Resolve("CO") == CadCmd.Copy && CadCommand.Resolve("C") == CadCmd.Column), 1, 0);
        check("_TRIM → trim; MI, AR, O, EX, E, U", B(CadCommand.Resolve("_TRIM") == CadCmd.Trim && CadCommand.Resolve("mi") == CadCmd.Mirror && CadCommand.Resolve("AR") == CadCmd.Array
            && CadCommand.Resolve("o") == CadCmd.Offset && CadCommand.Resolve("ex") == CadCmd.Extend && CadCommand.Resolve("E") == CadCmd.Erase && CadCommand.Resolve("u") == CadCmd.Undo), 1, 0);
        check("Tường / tải tiếng Việt", B(CadCommand.Resolve("tường") == CadCmd.Wall && CadCommand.Resolve("TAI") == CadCmd.Load), 1, 0);
        var z = CadCommand.Parse("Z E", null, CoordUnit.Mm);
        check("Z E → zoom toàn bộ", B(z.Kind == CadInputKind.Command && z.Cmd == CadCmd.ZoomExtents), 1, 0);
        check("Lệnh lạ → lỗi", B(CadCommand.Parse("XYZ", null, CoordUnit.Mm).Kind == CadInputKind.Error), 1, 0);
        check("Dòng rỗng → lặp lệnh", B(CadCommand.Parse("  ", null, CoordUnit.Mm).Kind == CadInputKind.Empty), 1, 0);
        var a1 = CadCommand.Parse("3600,1200", null, CoordUnit.Mm);
        check("x,y tuyệt đối (mm) → (3,6; 1,2)", a1.X * 10 + a1.Y, 36 + 1.2, 1e-9);
        var a2 = CadCommand.Parse("@1000,-500", (2.0, 2.0), CoordUnit.Mm);
        check("@dx,dy tương đối → (3,0; 1,5)", B(a2.Kind == CadInputKind.Point && Math.Abs(a2.X - 3.0) < 1e-9 && Math.Abs(a2.Y - 1.5) < 1e-9), 1, 0);
        var a3 = CadCommand.Parse("@2000<90", (1.0, 1.0), CoordUnit.Mm);
        check("@L<góc → (1,0; 3,0)", B(Math.Abs(a3.X - 1.0) < 1e-6 && Math.Abs(a3.Y - 3.0) < 1e-6), 1, 0);
        var a4 = CadCommand.Parse("1500", (0.0, 0.0), CoordUnit.Mm, (3.0, 4.0));
        check("Số đơn = chiều dài theo hướng chuột (3-4-5) → (0,9; 1,2)", B(a4.Kind == CadInputKind.Number && Math.Abs(a4.X - 0.9) < 1e-6 && Math.Abs(a4.Y - 1.2) < 1e-6), 1, 0);
        var along = CadCommand.Along((0, 0), (0, 2), 1.25);
        check("Along: 1,25 m theo phương Y", along?.Y ?? 0, 1.25, 1e-9);
        var o1 = CadCommand.Constrain((0, 0), (3.0, 0.4), true);
        check("Trực giao: (3; 0,4) → (3; 0)", B(o1.X == 3.0 && o1.Y == 0), 1, 0);
        var o2 = CadCommand.Constrain((0, 0), (0.3, -2.0), true);
        check("Trực giao: (0,3; −2) → (0; −2)", B(o2.X == 0 && o2.Y == -2.0), 1, 0);
        var po = CadCommand.Constrain((0, 0), (2.0, 2.1), false, 45);
        check("Góc cực 45°: (2; 2,1) bắt về đường 45°", B(Math.Abs(po.X - po.Y) < 1e-9 && Math.Abs(po.X - 2.05) < 1e-9), 1, 0);
        var pf = CadCommand.Constrain((0, 0), (2.0, 1.0), false, 45);
        check("Góc cực: lệch 26,6° không bắt (giữ nguyên)", B(pf.X == 2.0 && pf.Y == 1.0), 1, 0);
        check("Trợ giúp lệnh có danh sách", B(CadCommand.HelpText().Contains("Dầm (LINE)")), 1, 0);

        // ---- 7. Ưu tiên bắt điểm: đầu dầm/nút > giao > trung điểm > vuông góc > gần nhất ----
        var sEnd = BeamTools.BestSnap(p, st, 0.04, 0.03, 0.2);
        check("Gần góc ô → bắt đầu dầm / nút / giao trục", B(sEnd.Kind is SnapKind.End or SnapKind.Node or SnapKind.GridIntersection && Math.Abs(sEnd.X) < 1e-9 && Math.Abs(sEnd.Y) < 1e-9), 1, 0);
        var sMid = BeamTools.BestSnap(p, st, 2.55, 0.05, 0.2);
        check("Gần giữa dầm 5 m → trung điểm (2,5; 0)", B(sMid.Kind == SnapKind.Midpoint && Math.Abs(sMid.X - 2.5) < 1e-9), 1, 0);
        var sNear = BeamTools.BestSnap(p, st, 1.23, 0.04, 0.2, SnapMask.All & ~SnapMask.GridLine);
        check("Trên dầm, xa nút / trung điểm → gần nhất trên dầm (y = 0)", B(sNear.Kind == SnapKind.Nearest && Math.Abs(sNear.Y) < 1e-9), 1, 0);
        var sPerp = BeamTools.BestSnap(p, st, 2.0, 0.06, 0.2, SnapMask.Perpendicular | SnapMask.Nearest, (2.0, 3.0));
        check("Vuông góc từ điểm trước (2; 3) xuống dầm trục A → (2; 0) ưu tiên hơn gần nhất", B(sPerp.Kind == SnapKind.Perpendicular && Math.Abs(sPerp.X - 2.0) < 1e-9), 1, 0);
        check("Thứ tự ưu tiên enum: End < Intersection < Midpoint < Perpendicular < Nearest",
            B(SnapKind.End < SnapKind.Intersection && SnapKind.Intersection < SnapKind.Midpoint && SnapKind.Midpoint < SnapKind.Perpendicular && SnapKind.Perpendicular < SnapKind.Nearest), 1, 0);

        // ---- 8. Căn bản vẽ liên kết (xref) theo 2 cặp điểm ----
        var link = new CadLink { Name = "KienTruc.dwg" };
        // bản vẽ lệch gốc (100; 50) và xoay 90°: điểm (100; 50) → gốc mô hình (0; 0), điểm (105; 50) → (0; 5)
        link.AlignByTwoPairs((100, 50), (0, 0), (105, 50), (0, 5));
        var m1 = link.ToModel(100, 50); var m2 = link.ToModel(105, 50);
        check("Căn 2 điểm: điểm 1 trùng gốc mô hình", Math.Abs(m1.X) + Math.Abs(m1.Y), 0, 1e-6);
        check("Căn 2 điểm: xoay 90° → điểm 2 về (0; 5)", Math.Abs(m2.X) + Math.Abs(m2.Y - 5), 0, 1e-6);
        check("Góc xoay = 90°", link.Rotation, 90, 1e-6);

        // ---- 9. Đặt cột, offset, mảng dầm ----
        int f0 = p.Frames.Count;
        var rc = PlanDrawOps.AddColumn(p, st, 2.5, 4.5, "C22x30");
        check("Đặt cột giữa dầm trục B → thành công", B(rc.Ok), 1, 0);
        check("Dầm trục B bị chia tại cột (thêm 1 cột + 1 đoạn dầm)", p.Frames.Count - f0, 2, 0);
        check("Đặt lại cùng vị trí → báo đã có cột", B(!PlanDrawOps.AddColumn(p, st, 2.5, 4.5, "C22x30").Ok), 1, 0);
        var newCol = p.Frames.First(f => f.Id == rc.NewFrames[0]);
        check("Cột mới: chân ở chân công trình (−0,6) và ngàm", p.Nodes.First(n => n.Id == newCol.NodeI).Z, -0.6, 1e-9);
        var bA = Beams(p, st).First(f => Math.Abs(p.Nodes.First(n => n.Id == f.NodeI).Y) < 1e-6 && Math.Abs(p.Nodes.First(n => n.Id == f.NodeJ).Y) < 1e-6);
        int nb0 = Beams(p, st).Count;
        var ro = PlanDrawOps.OffsetBeams(p, new[] { bA.Id }, 1.2);
        check("Offset dầm trục A 1200 mm → thêm dầm", B(ro.Ok && Beams(p, st).Count > nb0), 1, 0);
        var off = p.Frames.First(f => f.Id == ro.NewFrames[0]);
        double yOff = p.Nodes.First(n => n.Id == off.NodeI).Y;
        check("Dầm offset nằm ở y = ±1,2", Math.Abs(yOff), 1.2, 1e-6);
        var p2 = House();
        var bx = Beams(p2, st).Where(f => Math.Abs(p2.Nodes.First(n => n.Id == f.NodeI).X - p2.Nodes.First(n => n.Id == f.NodeJ).X) < 1e-6 && Math.Abs(p2.Nodes.First(n => n.Id == f.NodeI).X) < 1e-6).Select(f => f.Id).ToList();
        var ra = PlanDrawOps.ArrayBeams(p2, bx, 3, 1, 1.5, 0);
        int VBeamsAt(double x) => Beams(p2, st).Count(f => Math.Abs(p2.Nodes.First(n => n.Id == f.NodeI).X - x) < 1e-6 && Math.Abs(p2.Nodes.First(n => n.Id == f.NodeJ).X - x) < 1e-6);
        check("Mảng 3 × 1 bước 1,5 m từ dầm trục 1 → dầm tại x = 1,5 và 3,0", B(ra.Ok && VBeamsAt(1.5) == 1 && VBeamsAt(3.0) == 1), 1, 0);
        check("Mảng 1 × 1 → báo lỗi", B(!PlanDrawOps.ArrayBeams(p2, bx, 1, 1, 1, 1).Ok), 1, 0);

        try
        {
            Directory.CreateDirectory(Path.Combine(outDir, "TrienKhai"));
            File.WriteAllText(Path.Combine(outDir, "TrienKhai", "DongLenh_TroGiup.txt"), CadCommand.HelpText());
        }
        catch (Exception) { }
    }
}
