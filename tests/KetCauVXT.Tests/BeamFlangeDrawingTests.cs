using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm tra cánh sàn trên mặt cắt dầm (Drawing/BeamSlabContext.cs + BeamDrawing.cs, kiểu Kết cấu Soft / XSCad):
/// dầm giữa → cánh hai bên (chữ T), dầm biên → một cánh đúng phía, cạnh lỗ cầu thang → không có cánh phía đó,
/// kích thước "hs=100", đường cắt đầu cánh, thép sàn trong cánh theo thiết kế sàn, dầm lệch trục, tắt thiết lập → về mặt cắt chữ nhật.
/// check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class BeamFlangeDrawingTests
{
    private static double B(bool b) => b ? 1 : 0;

    /// <summary>Phạm vi x của nét (LINE/POLYLINE) lớp layer.</summary>
    private static (double X1, double X2) RangeX(DxfWriter w, string layer)
    {
        double a = double.MaxValue, b = double.MinValue;
        foreach (var pr in w.GetPrimitives().Where(q => q.Layer == layer && q.Kind is PrimKind.Line or PrimKind.Polyline))
            for (int i = 0; i + 1 < pr.Pts.Length; i += 2) { a = Math.Min(a, pr.Pts[i]); b = Math.Max(b, pr.Pts[i]); }
        return (a, b);
    }

    /// <summary>Đường cắt lửng đầu cánh: polyline lớp BE_TONG hẹp theo x (≤ 0,6·hs·k) và cao hơn hs·k.</summary>
    private static List<DrawPrim> Breaks(DxfWriter w, double hsK)
    {
        var res = new List<DrawPrim>();
        foreach (var pr in w.GetPrimitives().Where(q => q.Layer == Lay.BeTong && q.Kind == PrimKind.Polyline && !q.Closed && q.Pts.Length >= 12))
        {
            double x1 = double.MaxValue, x2 = double.MinValue, y1 = double.MaxValue, y2 = double.MinValue;
            for (int i = 0; i + 1 < pr.Pts.Length; i += 2)
            {
                x1 = Math.Min(x1, pr.Pts[i]); x2 = Math.Max(x2, pr.Pts[i]);
                y1 = Math.Min(y1, pr.Pts[i + 1]); y2 = Math.Max(y2, pr.Pts[i + 1]);
            }
            if (x2 - x1 <= 0.6 * hsK + 1e-6 && y2 - y1 >= hsK - 1e-6) res.Add(pr);
        }
        return res;
    }

    private static List<int> LineFrames(Project p, string story, double y)
    {
        var res = new List<int>();
        foreach (var f in p.Frames.Where(q => q.Kind == FrameKind.Beam && q.Story == story))
        {
            var a = p.Node(f.NodeI); var c = p.Node(f.NodeJ);
            if (Math.Abs(a.Y - y) < 1e-6 && Math.Abs(c.Y - y) < 1e-6) res.Add(f.Id);
        }
        return res.OrderBy(id => Math.Min(p.Node(p.Frame(id).NodeI).X, p.Node(p.Frame(id).NodeJ).X)).ToList();
    }

    private static BeamLineDesign Find(DesignSummary d, List<int> ids) =>
        d.Beams.FirstOrDefault(b => b.Frames.Count == ids.Count && b.Frames.All(ids.Contains));

    /// <summary>Thiết kế dầm "mượn" của một dải khác cùng hình học (dải biên đối xứng có thể gộp SL=2): thay danh sách phần tử.</summary>
    private static List<Figure> BuildWith(Project p, BeamLineDesign d, List<int> frames, DesignSummary ds, string story, double z)
    {
        var keep = d.Frames; string ks = d.Story; double kz = d.TopElevation;
        d.Frames = frames; d.Story = story; d.TopElevation = z;
        try { return BeamDrawing.Build(p, d, ds); }
        finally { d.Frames = keep; d.Story = ks; d.TopElevation = kz; }
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var p = ModelBuilder.NewProject("Cánh sàn mặt cắt dầm");
        ModelBuilder.SetGrid(p, new[] { 4.5, 5.0 }, new[] { 4.2, 4.2 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        const string st = "T2";
        var ys = p.GridsY.Select(g => g.Coord).ToList();
        var xs = p.GridsX.Select(g => g.Coord).ToList();
        // lỗ cầu thang ô trục 1-2 / B-C tầng T2
        var op = ModelEdit.AddOpening(p, st, (xs[0] + xs[1]) / 2, (ys[1] + ys[2]) / 2, OpeningKind.Stair);
        check("Mô hình: thêm lỗ cầu thang ô 1-2/B-C", B(op.Ok), 1, 0);
        var r = FrameAnalysis.Run(p);
        var ds = ConcreteDesigner.Design(p, r);
        var def0 = DrawingStyle.Default();
        check("Thiết lập mặc định: mặt cắt dầm KHÔNG vẽ thép sàn (chỉ cấu tạo dầm), vẫn vẽ cánh sàn", B(!def0.ShowSlabRebarInFlange && def0.ShowSlabFlanges), 1, 0);
        var def = def0.Clone(); def.ShowSlabRebarInFlange = true;   // các kiểm tra thép sàn trong cánh dưới đây bật tuỳ chọn
        double S = 50, k = S / 25, b = 220 * k, Lf = def.SlabFlangeLength * k, hsK = 100 * k;
        check("Thiết lập (bật thép sàn): bật cánh sàn, dài 400 mm, có thép sàn, đường cắt dích dắc",
            B(def.ShowSlabFlanges && Math.Abs(def.SlabFlangeLength - 400) < 1e-9 && def.ShowSlabRebarInFlange && def.SlabBreak == SlabBreakStyle.ZigZag), 1, 0);

        var fA = LineFrames(p, st, ys[0]); var fB = LineFrames(p, st, ys[1]); var fC = LineFrames(p, st, ys[2]);
        double z2 = p.Stories.First(x => x.Name == st).Elevation;
        // thiết kế giống nhau có thể gộp (SL > 1) – lấy thiết kế đại diện của dải cùng trục ở tầng khác nếu cần
        var dA = Find(ds, fA) ?? Find(ds, fC) ?? Find(ds, LineFrames(p, "MAI", ys[0]));
        var dB = Find(ds, fB) ?? Find(ds, LineFrames(p, "MAI", ys[1]));
        check("Thiết kế: có dầm biên trục A (hoặc C) và dầm giữa trục B tầng T2", B(dA != null && dB != null), 1, 0);
        if (dA == null || dB == null) return;
        double x1 = ((xs[0] + xs[1]) / 2 - xs[0]) * 1000, x2 = ((xs[1] + xs[2]) / 2 - xs[0]) * 1000;   // giữa nhịp 1-2, 2-3 (mm dọc dầm)

        // ---------------- 1. bối cảnh sàn (BeamSlabContext) ----------------
        BeamLineDesign Syn(List<int> ids) => new BeamLineDesign { Frames = ids, Story = st, B = 0.22, H = 0.30, TopElevation = z2, Section = dA.Section };
        var cA = BeamSlabContext.At(p, Syn(fA), x2, ds);
        var cC = BeamSlabContext.At(p, Syn(fC), x2, ds);
        var cB1 = BeamSlabContext.At(p, Syn(fB), x1, ds);
        var cB2 = BeamSlabContext.At(p, Syn(fB), x2, ds);
        check("Dầm biên trục A (hướng +X): sàn bên trái (+Y), không có sàn bên phải", B(cA.Left != null && cA.Right == null && cA.Shape == "L"), 1, 0);
        check("Dầm biên trục C: sàn bên phải (−Y), không có bên trái", B(cC.Left == null && cC.Right != null && cC.Shape == "L"), 1, 0);
        check("Dầm giữa trục B nhịp 2-3: sàn hai bên (chữ T)", B(cB2.Shape == "T"), 1, 0);
        check("Dầm giữa trục B nhịp 1-2 cạnh lỗ cầu thang: bên trái là lỗ sàn, chỉ có cánh phải", B(cB1.LeftOpening && cB1.Left == null && cB1.Right != null), 1, 0);
        check("Chiều dày cánh theo tiết diện sàn S100: hs = 100 mm", cB2.HsL, 100, 1e-9);
        check("Mặt sàn trùng mặt dầm (mô hình chưa có hạ cốt): dz = 0", Math.Abs(cB2.DzL) + Math.Abs(cB2.DzR), 0, 0);
        check("Thép sàn trong cánh lấy theo kết quả thiết kế sàn", B(cB2.Left.BotFromDesign && cB2.Left.TopFromDesign), 1, 0);
        var cNo = BeamSlabContext.At(p, Syn(fB), x2, null);
        check("Không có kết quả thiết kế sàn: thép sàn quy ước Ø8a200", B(!cNo.Left.BotFromDesign && cNo.Left.BotPerpText == "Ø8a200"), 1, 0);
        check("Đọc nhãn thép sàn \"Ø10a150\"", B(BeamSlabContext.TryParseBar("Ø10a150", out int d10, out double s150) && d10 == 10 && Math.Abs(s150 - 150) < 1e-9), 1, 0);

        // ---------------- 2. mặt cắt dầm giữa: chữ T ----------------
        List<Figure> figB;
        using (DrawingStyle.Use(def)) figB = BuildWith(p, dB, fB, ds, st, z2);
        var secsB = figB.Skip(1).Select(f => f.W).ToList();
        var tSec = secsB.FirstOrDefault(w => { var (a, e) = RangeX(w, Lay.BeTong); return a < -0.5 * Lf && e > b + 0.5 * Lf; });
        check("Dầm giữa: có mặt cắt chữ T (cánh trái x < 0 và cánh phải x > b)", B(tSec != null), 1, 0);
        if (tSec != null)
        {
            check("Mặt cắt chữ T: 2 đường cắt lửng đầu cánh", Breaks(tSec, hsK).Count, 2, 0);
            check("Mặt cắt chữ T: kích thước \"hs=100\"", B(tSec.Texts().Contains("hs=100")), 1, 0);
            check("Mặt cắt chữ T: nhãn thép sàn \"(sàn)\"", B(tSec.Texts().Any(t => t.EndsWith("(sàn)", StringComparison.Ordinal))), 1, 0);
            check("Mặt cắt chữ T: vẫn có tên \"n-n\" và \"tl: 1/25\"", B(tSec.Texts().Contains("tl: 1/25") && tSec.Texts().Any(t => t.Length >= 3 && t[0] == t[^1] && t.Contains('-'))), 1, 0);
            var lay = tSec.UsedLayers();
            check("Mặt cắt chữ T: chỉ dùng lớp chuẩn (BE_TONG, THEP, DAI, KICH_THUOC, CHU, KY_HIEU, TRUC)",
                B(lay.All(l => l == "0" || l == Lay.BeTong || l == Lay.Thep || l == Lay.Dai || l == Lay.KichThuoc || l == Lay.Chu || l == Lay.KyHieu || l == Lay.Truc)), 1, 0);
        }
        var lSecB = secsB.FirstOrDefault(w => { var (a, e) = RangeX(w, Lay.BeTong); return a > -1e-6 && e > b + 0.5 * Lf; });
        check("Dầm giữa cạnh lỗ cầu thang: có mặt cắt chỉ cánh phải (không cánh phía lỗ)", B(lSecB != null), 1, 0);
        if (lSecB != null) check("Mặt cắt cạnh lỗ: 1 đường cắt lửng", Breaks(lSecB, hsK).Count, 1, 0);
        check("Mặt đứng dầm giữa: ghi chú \"Sàn liền khối … hs=100\"", B(figB[0].W.Texts().Any(t => t.StartsWith("Sàn liền khối", StringComparison.Ordinal) && t.Contains("hs=100"))), 1, 0);

        // ---------------- 3. dầm biên: một cánh đúng phía ----------------
        bool aIsA = Find(ds, fA) != null;
        List<Figure> figA, figC;
        using (DrawingStyle.Use(def))
        {
            figA = BuildWith(p, dA, fA, ds, st, z2);
            figC = BuildWith(p, dA, fC, ds, st, z2);
        }
        bool LeftOnly(DxfWriter w) { var (a, e) = RangeX(w, Lay.BeTong); return a < -0.5 * Lf && e < b + 0.25 * Lf; }
        bool RightOnly(DxfWriter w) { var (a, e) = RangeX(w, Lay.BeTong); return a > -1e-6 && e > b + 0.5 * Lf; }
        check("Dầm biên trục A: mọi mặt cắt chỉ có cánh bên trái (phía sàn +Y)", B(figA.Skip(1).Any() && figA.Skip(1).All(f => LeftOnly(f.W))), 1, 0);
        bool Rect(DxfWriter w) { var (a, e) = RangeX(w, Lay.BeTong); return a > -1e-6 && e < b + 1e-6; }
        check("Dầm biên trục C nhịp 2-3: mặt cắt chỉ có cánh bên phải (phía sàn −Y)", B(figC.Skip(1).Any(f => RightOnly(f.W))), 1, 0);
        check("Dầm biên trục C nhịp 1-2 (viền lỗ cầu thang): mặt cắt chữ nhật, không cánh", B(figC.Skip(1).Any(f => Rect(f.W))), 1, 0);
        check("Dầm biên trục C: không mặt cắt nào có cánh bên trái (phía ngoài nhà)", B(figC.Skip(1).All(f => RangeX(f.W, Lay.BeTong).X1 > -1e-6)), 1, 0);
        check("Dầm biên: mỗi mặt cắt đúng 1 đường cắt lửng", B(figA.Skip(1).All(f => Breaks(f.W, hsK).Count == 1)), 1, 0);
        check("Dầm biên: kích thước \"hs=100\" ở phía có cánh", B(figA.Skip(1).All(f => f.W.Texts().Contains("hs=100")) && figC.Skip(1).Where(f => RightOnly(f.W)).All(f => f.W.Texts().Contains("hs=100"))), 1, 0);
        Console.WriteLine("   · dầm biên vẽ từ thiết kế trục " + (aIsA ? "A" : "C"));

        // ---------------- 4. đường cắt nét thẳng ----------------
        var sStr = def.Clone(); sStr.SlabBreak = SlabBreakStyle.Straight;
        List<Figure> figS;
        using (DrawingStyle.Use(sStr)) figS = BuildWith(p, dB, fB, ds, st, z2);
        var tS = figS.Skip(1).FirstOrDefault(f => { var (a, e) = RangeX(f.W, Lay.BeTong); return a < -0.5 * Lf && e > b + 0.5 * Lf; });
        check("Đường cắt nét thẳng: không còn dích dắc, cánh giữ nguyên chiều dài", B(tS != null && Breaks(tS.W, hsK).Count == 0 && Math.Abs(RangeX(tS.W, Lay.BeTong).X1 + Lf) < 1e-6), 1, 0);

        // ---------------- 5. chiều dài cánh, tắt thép sàn ----------------
        var s600 = def.Clone(); s600.SlabFlangeLength = 600;
        List<Figure> fig600;
        using (DrawingStyle.Use(s600)) fig600 = BuildWith(p, dB, fB, ds, st, z2);
        double min600 = fig600.Skip(1).Min(f => RangeX(f.W, Lay.BeTong).X1);
        check("Chiều dài cánh 600 mm: cánh trái tới x ≈ −600·k (kể cả đường cắt lửng)", B(min600 <= -600 * k + 1e-6 && min600 >= -600 * k - 0.3 * hsK), 1, 0);
        var sNoBar = def.Clone(); sNoBar.ShowSlabRebarInFlange = false;
        List<Figure> figNb;
        using (DrawingStyle.Use(sNoBar)) figNb = BuildWith(p, dB, fB, ds, st, z2);
        check("Tắt thép sàn trong cánh: không còn nhãn \"(sàn)\", vẫn có cánh",
            B(!figNb.Any(f => f.W.Texts().Any(t => t.Contains("(sàn)"))) && figNb.Skip(1).Any(f => RangeX(f.W, Lay.BeTong).X1 < -0.5 * Lf)), 1, 0);

        // ---------------- 6. tắt cánh sàn: về mặt cắt chữ nhật cũ ----------------
        var sOff = def.Clone(); sOff.ShowSlabFlanges = false;
        List<Figure> figOff;
        using (DrawingStyle.Use(sOff)) figOff = BuildWith(p, dB, fB, ds, st, z2);
        check("Tắt cánh sàn: mọi mặt cắt BE_TONG chỉ là 1 hình chữ nhật kín [0; b]",
            B(figOff.Skip(1).All(f =>
            {
                var pr = f.W.GetPrimitives().Where(q => q.Layer == Lay.BeTong && q.Kind is PrimKind.Line or PrimKind.Polyline).ToList();
                var (a, e) = RangeX(f.W, Lay.BeTong);
                return pr.Count == 1 && pr[0].Closed && Math.Abs(a) < 1e-6 && Math.Abs(e - b) < 1e-6;
            })), 1, 0);
        check("Tắt cánh sàn: không có \"hs=\" và \"(thép sàn)\"", B(!figOff.Any(f => f.W.Texts().Any(t => t.StartsWith("hs=", StringComparison.Ordinal) || t.Contains("(sàn)")))), 1, 0);
        check("Tắt cánh sàn: ít thực thể hơn (mặt đứng + mặt cắt)", B(figOff.Sum(f => f.W.EntityCount) < figB.Sum(f => f.W.EntityCount)), 1, 0);
        check("Tắt cánh sàn: mặt đứng không có ghi chú sàn", B(!figOff[0].W.Texts().Any(t => t.StartsWith("Sàn liền khối", StringComparison.Ordinal))), 1, 0);
        check("Tắt cánh sàn: số mặt cắt không nhiều hơn khi bật", B(figOff.Count <= figB.Count), 1, 0);

        // ---------------- 7. dầm lệch trục (điểm chèn mép) ----------------
        foreach (int id in fA) p.Frame(id).Insertion = BeamInsertion.MiddleRight;   // mép phải trên trục → tim dầm dời sang trái (vào phía sàn)
        var cOff = BeamSlabContext.At(p, Syn(fA), x2, ds);
        check("Dầm biên điểm chèn mép phải: tim dầm lệch trục +b/2 = 110 mm (sang trái)", cOff.Offset, 110, 1e-6);
        check("Dầm biên lệch trục: vẫn chỉ có sàn bên trái", B(cOff.Left != null && cOff.Right == null), 1, 0);
        List<Figure> figOffs;
        using (DrawingStyle.Use(def)) figOffs = BuildWith(p, dA, fA, ds, st, z2);
        check("Mặt cắt dầm lệch trục: có đường trục định vị (lớp TRUC)", B(figOffs.Skip(1).All(f => f.W.CountEntities("LINE", Lay.Truc) >= 1)), 1, 0);
        foreach (int id in fA) p.Frame(id).Insertion = BeamInsertion.Centroid;

        // ---------------- 8. JSON, DXF mẫu ----------------
        var sj = def.Clone(); sj.SlabFlangeLength = 550; sj.SlabBreak = SlabBreakStyle.Straight; sj.ShowSlabRebarInFlange = false;
        var back = DrawingStyle.FromJson(sj.ToJson());
        check("JSON: lưu/mở giữ thiết lập cánh sàn", B(Math.Abs(back.SlabFlangeLength - 550) < 1e-9 && back.SlabBreak == SlabBreakStyle.Straight && !back.ShowSlabRebarInFlange && back.ShowSlabFlanges), 1, 0);
        var old = DrawingStyle.FromJson("{\"NoteHeight\": 2.5}");
        check("JSON cũ (không có mục cánh sàn): mặc định bật, 400 mm", B(old.ShowSlabFlanges && Math.Abs(old.SlabFlangeLength - 400) < 1e-9), 1, 0);
        var bad = DrawingStyle.FromJson("{\"SlabFlangeLength\": -5}");
        check("JSON sai (chiều dài cánh âm) → về 400 mm", bad.SlabFlangeLength, 400, 1e-9);
        try
        {
            using (DrawingStyle.Use(def))
            {
                figB[0].W.Save(Path.Combine(outDir, "DamCanhSan_MatDung.dxf"));
                if (tSec != null) tSec.Save(Path.Combine(outDir, "DamCanhSan_MatCatT.dxf"));
                if (figA.Count > 1) figA[1].W.Save(Path.Combine(outDir, "DamCanhSan_MatCatL.dxf"));
            }
            check("DXF mẫu cánh sàn: đã ghi file", B(File.Exists(Path.Combine(outDir, "DamCanhSan_MatDung.dxf"))), 1, 0);
        }
        catch (Exception ex) { Console.WriteLine("   · Lỗi ghi DXF cánh sàn: " + ex.Message); check("DXF mẫu cánh sàn: ghi file", 0, 1, 0); }
    }
}
