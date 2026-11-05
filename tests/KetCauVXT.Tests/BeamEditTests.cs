using KetCauVXT.Import.Link;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Sửa dầm kiểu AutoCAD (Model/BeamEdit.cs): extend tới dầm vuông góc / dầm chọn, trim, kéo đầu dầm, dời dầm,
/// nối đầu dầm hở; pick đối tượng trên bản vẽ liên kết (Import/Link/CadPick.cs): cặp nét song song → trục + bề rộng dầm,
/// một nét → trục, đa tuyến kín → ô sàn / cầu thang. check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class BeamEditTests
{
    private static double B(bool b) => b ? 1 : 0;

    private static Project House(double[] spansX, double[] spansY, bool build = true)
    {
        var p = ModelBuilder.NewProject("Kiểm tra sửa dầm");
        ModelBuilder.SetGrid(p, spansX, spansY);
        ModelBuilder.SetStories(p, new[] { 3.6 }, -0.6);   // tầng "T2" cao độ 3,0
        if (build) ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        return p;
    }

    private static bool HasNode(Project p, double x, double y, double z = 3.0) =>
        p.Nodes.Any(n => Math.Abs(n.X - x) < 1e-3 && Math.Abs(n.Y - y) < 1e-3 && Math.Abs(n.Z - z) < 1e-3);

    private static List<FrameElement> BeamsOnLineY(Project p, double y) =>
        p.Frames.Where(f => !f.IsVerticalKind && Math.Abs(p.Node(f.NodeI).Y - y) < 1e-3 && Math.Abs(p.Node(f.NodeJ).Y - y) < 1e-3 && Math.Abs(p.Node(f.NodeI).Z - 3.0) < 1e-3).ToList();

    private static List<FrameElement> BeamsOnLineX(Project p, double x) =>
        p.Frames.Where(f => !f.IsVerticalKind && Math.Abs(p.Node(f.NodeI).X - x) < 1e-3 && Math.Abs(p.Node(f.NodeJ).X - x) < 1e-3 && Math.Abs(p.Node(f.NodeI).Z - 3.0) < 1e-3).ToList();

    private static double Len(Project p, IEnumerable<FrameElement> fs) => fs.Sum(f => p.Length(f));

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        const string st = "T2";
        var p = House(new[] { 5.0, 5.0 }, new[] { 4.5 });
        int beamsY45 = BeamsOnLineY(p, 4.5).Count;

        // ---- 1. Extend tới dầm vuông góc gặp đầu tiên ----
        var r1 = BeamTools.AddBeamExact(p, st, 2.5, 1.0, 2.5, 3.0, "D22x30");
        check("Thêm dầm hở 2,5 / 1,0 → 3,0", B(r1.Ok && r1.NewFrames.Count == 1), 1, 0);
        int id1 = r1.NewFrames[0];
        bool e1 = BeamEdit.Extend(p, id1, true, null, out string m1);
        check("Extend đầu J tới dầm vuông góc trục B: thành công", B(e1), 1, 0);
        check("Extend: nút mới tại giao (2,5; 4,5)", B(HasNode(p, 2.5, 4.5)), 1, 0);
        check("Extend: dầm trục B (0–5) được chia tại điểm nối", BeamsOnLineY(p, 4.5).Count, beamsY45 + 1, 0);
        bool e1b = BeamEdit.Extend(p, id1, false, null, out _);
        check("Extend đầu I tới dầm trục A: thành công", B(e1b), 1, 0);
        check("Sau 2 lần extend: dầm 2,5 dài 4,5 m (0 → 4,5)", Len(p, BeamsOnLineX(p, 2.5)), 4.5, 1e-6);
        check("Thông báo extend có ghi vuông góc", B(m1.Contains("vuông góc")), 1, 0);

        // ---- 2. Extend tới dầm được chọn (vượt qua dầm gần hơn) ----
        var r2 = BeamTools.AddBeamExact(p, st, 6.0, 2.0, 8.0, 2.0, "D22x30");
        int id2 = r2.NewFrames[0];
        var gX10 = BeamsOnLineX(p, 10.0).First();
        check("Extend đầu J tới dầm trục 3 (chỉ định)", B(BeamEdit.Extend(p, id2, true, gX10.Id, out _)), 1, 0);
        var gX0 = BeamsOnLineX(p, 0.0).First();
        check("Extend đầu I tới dầm trục 1 (chỉ định, vượt dầm trục 2)", B(BeamEdit.Extend(p, id2, false, gX0.Id, out _)), 1, 0);
        check("Dầm y = 2 phủ 0 → 10 m", Len(p, BeamsOnLineY(p, 2.0)), 10.0, 1e-6);
        check("Dầm y = 2 chia tại trục 2 và tại dầm 2,5 (3 đoạn)", BeamsOnLineY(p, 2.0).Count, 3, 0);
        check("Dầm trục 2 được chia tại y = 2 (nút giao kết cấu)", B(HasNode(p, 5.0, 2.0) && BeamsOnLineX(p, 5.0).Count >= 2), 1, 0);
        var ghost = BeamTools.AddBeamExact(p, st, 6.0, 3.8, 6.0, 4.0, "D22x30");
        check("Extend tới dầm song song (không cắt) → thất bại", B(!BeamEdit.Extend(p, ghost.NewFrames[0], true, BeamsOnLineX(p, 10.0).First().Id, out _)), 1, 0);
        BeamTools.DeleteFrames(p, ghost.NewFrames);

        // ---- 3. Trim ----
        double before = Len(p, BeamsOnLineY(p, 2.0));
        var piece = BeamEdit.NearestBeam(p, st, 1.2, 2.0, 0.2, out _, out _);
        check("Tìm dầm gần điểm bấm (1,2; 2,0)", B(piece != null), 1, 0);
        check("Trim đoạn 0 → 2,5 của dầm y = 2", B(BeamEdit.Trim(p, piece.Id, 1.2, 2.0, out _)), 1, 0);
        check("Sau trim: dầm y = 2 còn 7,5 m", Len(p, BeamsOnLineY(p, 2.0)), before - 2.5, 1e-6);
        // nhô quá dầm trục B: dầm x = 7 từ y = 1 tới 5,5 cắt trục B → trim phần nhô
        var r3 = BeamTools.AddBeamExact(p, st, 7.0, 1.0, 7.0, 5.5, "D22x30");
        check("Dầm x = 7 vượt qua trục B được chia tại giao", BeamsOnLineX(p, 7.0).Count >= 2 ? 1 : 0, 1, 0);
        var over = BeamEdit.NearestBeam(p, st, 7.0, 5.2, 0.2, out _, out _);
        check("Trim đầu nhô (y > 4,5)", B(over != null && BeamEdit.Trim(p, over.Id, 7.0, 5.2, out _)), 1, 0);
        check("Sau trim: không còn nút (7; 5,5)", B(!HasNode(p, 7.0, 5.5)), 1, 0);
        check("Sau trim: dầm x = 7 còn 3,5 m (1 → 4,5)", Len(p, BeamsOnLineX(p, 7.0)), 3.5, 1e-6);
        var lone = BeamTools.AddBeamExact(p, st, 8.5, 0.6, 9.0, 0.6, "D22x30");
        check("Trim dầm không giao gì → báo dùng Xoá", B(!BeamEdit.Trim(p, lone.NewFrames[0], 8.7, 0.6, out string mt) && mt.Contains("Xoá")), 1, 0);
        BeamTools.DeleteFrames(p, lone.NewFrames);

        // ---- 4. Kéo đầu dầm ----
        var r4 = BeamTools.AddBeamExact(p, st, 6.0, 3.5, 8.0, 3.5, "D22x30");
        int id4 = r4.NewFrames[0];
        // dầm x = 7 cắt → đoạn 6 → 7 và 7 → 8; kéo đầu J của đoạn đầu (nút 7 dùng chung) sang (6,5; 3,5)?? dùng đoạn sau
        var seg78 = BeamsOnLineY(p, 3.5).First(f => Math.Abs(Math.Max(p.Node(f.NodeI).X, p.Node(f.NodeJ).X) - 8.0) < 1e-3);
        bool jAt8 = Math.Abs(p.Node(seg78.NodeJ).X - 8.0) < 1e-3;
        check("Kéo đầu hở (8; 3,5) tới (9,2; 3,5)", B(BeamEdit.Stretch(p, seg78.Id, jAt8, 9.2, 3.5, out _)), 1, 0);
        check("Kéo đầu dầm: dầm y = 3,5 dài 3,2 m", Len(p, BeamsOnLineY(p, 3.5)), 3.2, 1e-6);
        check("Kéo đầu dầm: không còn nút (8; 3,5)", B(!HasNode(p, 8.0, 3.5)), 1, 0);
        check("Kéo đầu dầm quá ngắn → thất bại", B(!BeamEdit.Stretch(p, seg78.Id, jAt8, 7.01, 3.5, out _)), 1, 0);
        _ = id4;

        // ---- 5. Dời dầm song song ----
        var r5 = BeamTools.AddBeamExact(p, st, 8.0, 1.0, 9.5, 1.0, "D22x30");
        int id5 = r5.NewFrames[0];
        check("Dời dầm Δy = +0,5 m", B(BeamEdit.Move(p, new[] { id5 }, 0, 0.5, out _)), 1, 0);
        check("Dời dầm: nằm tại y = 1,5", Len(p, BeamsOnLineY(p, 1.5)), 1.5, 1e-6);
        check("Dời dầm: nút cũ (8; 1) đã xoá", B(!HasNode(p, 8.0, 1.0)), 1, 0);
        check("Dời 0 → thất bại", B(!BeamEdit.Move(p, new[] { id5 }, 0, 0, out _)), 1, 0);

        // ---- 6. Nối đầu dầm hở ----
        var r6 = BeamTools.AddBeamExact(p, st, 6.0, 0.5, 8.0, 0.5, "D22x30");
        int id6 = r6.NewFrames[0];
        check("Nối dầm hở với khoảng 0,5 m → không tới (cách 1 và 2 m)", B(!BeamEdit.JoinToNearest(p, id6, 0.5, out _)), 1, 0);
        check("Nối dầm hở với khoảng 2,5 m → nối hai đầu", B(BeamEdit.JoinToNearest(p, id6, 2.5, out string mj)), 1, 0);
        check("Nối dầm hở: dầm y = 0,5 phủ 5 → 10", Len(p, BeamsOnLineY(p, 0.5)), 5.0, 1e-6);
        check("Nối dầm hở: thông báo nêu hai đầu", B(mj.Contains("đầu I") && mj.Contains("đầu J")), 1, 0);
        check("Mô hình hợp lệ sau mọi thao tác (Validate)", p.Validate().Count, 0, 0);

        // ---- 7. Pick CAD: cặp nét song song, một nét, đa tuyến kín ----
        var segs = new List<(double X1, double Y1, double X2, double Y2)>
        {
            (0, 1.0, 6, 1.0), (6, 1.22, 0, 1.22),   // mép dầm 220, nét thứ hai vẽ ngược chiều
            (0, 3.0, 6, 3.0), (2, 3.8, 6, 3.8)      // cặp cách 800 – quá rộng
        };
        var pk = CadPick.BeamFromPair(segs, 3.0, 1.1);
        check("Pick cặp nét: nhận được dầm", B(pk != null), 1, 0);
        check("Pick cặp nét: bề rộng 220 mm", pk?.Width ?? 0, 0.22, 1e-9);
        check("Pick cặp nét: trục ở y = 1,11", pk?.Y1 ?? 0, 1.11, 1e-9);
        check("Pick cặp nét: chiều dài đoạn chồng 6 m", pk?.Length ?? 0, 6.0, 1e-9);
        check("Pick cặp nét cách 800 mm → không nhận", B(CadPick.BeamFromPair(segs, 3.0, 3.4) == null), 1, 0);
        var pl = CadPick.BeamFromLine(segs, 3.0, 3.05);
        check("Pick một nét: theo nét y = 3", B(pl != null && Math.Abs(pl.Y1 - 3.0) < 1e-9 && pl.Width == 0), 1, 0);
        var polys = new List<IList<(double X, double Y)>>
        {
            new List<(double, double)> { (0, 0), (4, 0), (4, 3), (0, 3) },
            new List<(double, double)> { (1, 1), (2, 1), (2, 2), (1, 2) }
        };
        check("Đa tuyến kín nhỏ nhất chứa điểm (1,5; 1,5): diện tích 1", CadPick.Area(CadPick.ClosedLoopAt(polys, 1.5, 1.5) ?? new List<(double, double)>()), 1.0, 1e-9);
        check("Đa tuyến kín chứa (3; 2,5): diện tích 12", CadPick.Area(CadPick.ClosedLoopAt(polys, 3, 2.5) ?? new List<(double, double)>()), 12.0, 1e-9);
        check("Ngoài mọi đa tuyến → null", B(CadPick.ClosedLoopAt(polys, 9, 9) == null), 1, 0);

        // ---- 8. Tạo đối tượng mô hình từ pick ----
        var q = House(new[] { 5.0 }, new[] { 4.5 }, false);
        var cb = CadPick.CreateBeam(q, st, new CadBeamPick { X1 = 0, Y1 = 0, X2 = 5, Y2 = 0, Width = 0.25 }, "D22x30");
        check("Tạo dầm từ pick bề rộng 250: thành công", B(cb.Ok), 1, 0);
        var fsec = q.Frames.Where(f => cb.NewFrames.Contains(f.Id)).Select(f => q.FrameSections.First(s => s.Name == f.Section)).FirstOrDefault();
        check("Tạo dầm từ pick: tiết diện b = 250 mm", fsec?.B ?? 0, 0.25, 1e-9);
        check("Tạo dầm từ pick: h theo tiết diện mặc định 300 mm", fsec?.H ?? 0, 0.30, 1e-9);
        var slabLoop = new List<(double, double)> { (0, 0), (5, 0), (5, 4.5), (0, 4.5) };
        var cs = CadPick.CreateFromLoop(q, st, slabLoop, CadPick.LoopTarget.Slab, "D22x30");
        check("Đa tuyến kín → dầm bao + ô sàn", B(cs.Ok && q.Slabs.Count == 1), 1, 0);
        check("Đa tuyến kín → 3 cạnh dầm mới (cạnh y = 0 đã có)", B(Math.Abs(Len(q, BeamsOnLineX(q, 0.0)) - 4.5) < 1e-6 && Math.Abs(Len(q, BeamsOnLineX(q, 5.0)) - 4.5) < 1e-6), 1, 0);
        var stairLoop = new List<(double, double)> { (5, 0), (8, 0), (8, 4.5), (5, 4.5) };
        var cst = CadPick.CreateFromLoop(q, st, stairLoop, CadPick.LoopTarget.Stair, "D22x30");
        check("Đa tuyến kín → ô cầu thang (lỗ sàn)", B(cst.Ok && q.Openings.Count == 1 && q.Openings[0].Kind == OpeningKind.Stair), 1, 0);
    }
}
