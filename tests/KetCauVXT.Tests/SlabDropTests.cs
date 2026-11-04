using System.IO.Compression;
using System.Text.Json;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm tra khai báo SÀN HẠ CỐT (Model/SlabDrop.cs, Design/Concrete/SlabDropChecks.cs, Drawing/SlabDropDrawing.cs, Report/SlabDropReport.cs):
/// hạ cốt mặc định khi đổi công năng (không đổi ô cũ khi mở file), JSON tương thích ngược, bậc sàn trên mặt cắt dầm,
/// kiểm tra dầm biên h ≥ hs1 + Δ + hs2, cạnh bậc tính khớp + thép mũ tách riêng từng ô, lưới trên dừng tại bậc,
/// tải không đổi, mặt bằng kết cấu / thép sàn / dầm / thuyết minh / khối lượng có ghi chú hạ cốt.
/// check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class SlabDropTests
{
    private static double B(bool b) => b ? 1 : 0;

    private static Project Make(string name)
    {
        var p = ModelBuilder.NewProject(name);
        ModelBuilder.SetGrid(p, new[] { 4.0, 4.0 }, new[] { 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        return p;
    }

    /// <summary>Phần tử dầm tầng story trên đường x = const (dầm theo phương Y).</summary>
    private static List<int> LineX(Project p, string story, double x) =>
        p.Frames.Where(f => f.Kind == FrameKind.Beam && f.Story == story
                            && Math.Abs(p.Node(f.NodeI).X - x) < 1e-6 && Math.Abs(p.Node(f.NodeJ).X - x) < 1e-6)
            .Select(f => f.Id).OrderBy(id => Math.Min(p.Node(p.Frame(id).NodeI).Y, p.Node(p.Frame(id).NodeJ).Y)).ToList();

    private static SlabDesign DesignOf(DesignSummary ds, Slab s) =>
        ds.Slabs.FirstOrDefault(x => x.SlabId == s.Id) ?? ds.Slabs.FirstOrDefault(x => x.RebarKeys.Contains("S:" + s.Id));

    /// <summary>Thanh mũ vẽ trên đường x = 4 (cạnh chung hai ô) của các ô tầng story.</summary>
    private static List<SlabEdgeBar> EdgeBarsAtX(DesignSummary ds, string story, double x) =>
        ds.Slabs.Where(d => d.Story == story).SelectMany(d => d.EdgeBars)
            .Where(e => e.Bar != null && Math.Abs(e.X1 - x) < 1e-6 && Math.Abs(e.X2 - x) < 1e-6).ToList();

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Directory.CreateDirectory(outDir);
        const string st = "T2";

        // ---------------- 1. JSON: file cũ / mặc định không ghi trường mới ----------------
        var p0 = Make("Sàn hạ cốt – gốc");
        string j0 = JsonSerializer.Serialize(p0, Project.JsonOptions);
        check("JSON mặc định: không ghi LevelOffset / DropReason / SlabDrop (tương thích file cũ)",
            B(!j0.Contains("\"LevelOffset\"") && !j0.Contains("\"DropReason\"") && !j0.Contains("\"SlabDrop\"")), 1, 0);
        var old = JsonSerializer.Deserialize<Project>(j0, Project.JsonOptions);
        check("Mở file cũ: mọi ô sàn hạ cốt = 0, thiết lập = mặc định (null)", B(old.Slabs.All(s => s.LevelOffset == 0 && s.DropReason == null) && old.SlabDrop == null), 1, 0);
        check("Thiết lập mặc định: WC −50 mm, ban công −50 mm, mái 0, ngưỡng bậc 30 mm",
            B(Math.Abs(SlabDropSettings.Of(old).For(SlabUse.WC) + 0.05) < 1e-9 && Math.Abs(SlabDropSettings.Of(old).For(SlabUse.Balcony) + 0.05) < 1e-9
              && SlabDropSettings.Of(old).For(SlabUse.Roof) == 0 && Math.Abs(SlabDropSettings.Of(old).StepThreshold - 0.03) < 1e-9), 1, 0);
        // ô có cấu tạo "Sàn vệ sinh" (gán cấu tạo, không qua đổi công năng) → mở lại không tự hạ cốt
        var wcOld = ModelEdit.SlabAt(p0, st, 2.0, 2.25);
        ModelEdit.SetBuildUp(p0, wcOld, "Sàn vệ sinh");
        string tmp = Path.Combine(outDir, "SanHaCot_cu.kcvxt");
        p0.Save(tmp);
        var re = Project.Load(tmp);
        var wcRe = re.Slabs.First(s => s.Id == wcOld.Id);
        check("Mở lại dự án có ô vệ sinh cũ: công năng WC nhưng KHÔNG tự đổi hạ cốt", B(ModelEdit.UseOf(re, wcRe) == SlabUse.WC && wcRe.LevelOffset == 0), 1, 0);

        // ---------------- 2. hạ cốt mặc định khi đổi công năng ----------------
        var p = Make("Sàn hạ cốt");
        var wc = ModelEdit.SlabAt(p, st, 2.0, 2.25);
        var room = ModelEdit.SlabAt(p, st, 6.0, 2.25);
        var r1 = ModelEdit.SetSlabUse(p, wc, SlabUse.WC);
        Console.WriteLine("   · " + r1.Message);
        check("SetSlabUse(Vệ sinh): hạ cốt mặc định −0,05 m", wc.LevelOffset, -0.05, 1e-9);
        check("SetSlabUse(Vệ sinh): thông báo có \"Hạ cốt mặc định\"", B(r1.Message.Contains("Hạ cốt mặc định")), 1, 0);
        check("Ô bên cạnh không bị đổi", room.LevelOffset, 0, 0);
        ModelEdit.SetSlabUse(p, wc, SlabUse.Normal);
        check("Trả về phòng ở: bỏ hạ cốt mặc định", wc.LevelOffset, 0, 0);
        ModelEdit.SetSlabUse(p, wc, SlabUse.WC);
        check("Đặt lại WC: −0,05 m", wc.LevelOffset, -0.05, 1e-9);
        var custom = SlabDrop.SetDrop(p, wc, -0.10, "WC đặt bệt âm");
        var r2 = ModelEdit.SetSlabUse(p, wc, SlabUse.Corridor);
        check("Hạ cốt chỉnh tay (−100) được giữ khi đổi công năng", B(custom.Ok && Math.Abs(wc.LevelOffset + 0.10) < 1e-9 && r2.Message.Contains("Giữ hạ cốt")), 1, 0);
        ModelEdit.SetSlabUse(p, wc, SlabUse.WC);
        SlabDrop.SetDrop(p, wc, -0.05, "Sàn vệ sinh");
        check("Hạ cốt ngoài phạm vi (+100 / −600 mm) bị từ chối", B(!SlabDrop.SetDrop(p, wc, 0.10).Ok && !SlabDrop.SetDrop(p, wc, -0.60).Ok && Math.Abs(wc.LevelOffset + 0.05) < 1e-9), 1, 0);
        var pOff = Make("Tắt hạ cốt mặc định");
        pOff.SlabDrop = new SlabDropSettings { ApplyOnUse = false };
        var wcOff = ModelEdit.SlabAt(pOff, st, 2.0, 2.25);
        ModelEdit.SetSlabUse(pOff, wcOff, SlabUse.WC);
        check("Tắt 'tự hạ cốt theo công năng': SetSlabUse(Vệ sinh) không hạ", wcOff.LevelOffset, 0, 0);
        // ban công: ô sàn mới tự hạ cốt theo bảng
        var pBal = Make("Ban công hạ cốt");
        var edge = ModelEdit.StoryBeams(pBal, st).First(f => Math.Abs(pBal.Node(f.NodeI).Y) < 1e-6 && Math.Abs(pBal.Node(f.NodeJ).Y) < 1e-6);
        var rb = ModelEdit.AddBalcony(pBal, st, edge, 1.2, 1.0);
        check("Thêm ban công: ô sàn ban công hạ cốt −0,05 m", B(rb.Ok && rb.NewSlabs.All(id => Math.Abs(pBal.Slabs.First(s => s.Id == id).LevelOffset + 0.05) < 1e-9)), 1, 0);

        // ---------------- 3. JSON lưu / mở ----------------
        p.SlabDrop = new SlabDropSettings { StepThreshold = 0.02 };
        var cl = p.Clone();
        var wcCl = cl.Slabs.First(s => s.Id == wc.Id);
        check("Lưu / mở: giữ LevelOffset, DropReason, thiết lập", B(Math.Abs(wcCl.LevelOffset + 0.05) < 1e-9 && wcCl.DropReason == "Sàn vệ sinh" && cl.SlabDrop != null && Math.Abs(cl.SlabDrop.StepThreshold - 0.02) < 1e-9), 1, 0);
        p.SlabDrop = null;

        // ---------------- 4. phân tích: tải không đổi ----------------
        var pB = p.Clone();                                  // so sánh: WC không hạ cốt
        SlabDrop.SetDrop(pB, pB.Slabs.First(s => s.Id == wc.Id), 0);
        var rD = FrameAnalysis.Run(p);
        var rB = FrameAnalysis.Run(pB);
        double fzD = rD.Get("HTHT").Reactions.Values.Sum(v => v[2]), fzB = rB.Get("HTHT").Reactions.Values.Sum(v => v[2]);
        check("Phân tích: tải sàn không đổi khi hạ cốt (ΣFZ HTHT bằng nhau)", fzD, fzB, 1e-9);
        var dsD = ConcreteDesigner.Design(p, rD);
        var dsB = ConcreteDesigner.Design(pB, rB);

        // ---------------- 5. bối cảnh mặt cắt dầm: bậc sàn ----------------
        var ids = LineX(p, st, 4.0);
        double z2 = p.Stories.First(s => s.Name == st).Elevation;
        BeamLineDesign Syn(double h) => new BeamLineDesign
        {
            Name = "DTEST", Frames = ids, Story = st, B = 0.22, H = h, TopElevation = z2, Section = "D22x30",
            Spans = new List<BeamSpanResult> { new() { Frame = ids.FirstOrDefault(), L = 4.5 } }
        };
        var sec = BeamSlabContext.At(p, Syn(0.30), 2250, dsD);
        check("Mặt cắt dầm giữa WC / phòng: chữ T, một cánh Dz = −50, cánh kia 0",
            B(sec.Shape == "T" && Math.Abs(Math.Min(sec.DzL, sec.DzR) + 50) < 1e-9 && Math.Abs(Math.Max(sec.DzL, sec.DzR)) < 1e-9), 1, 0);
        check("Cánh hạ cốt nằm phía ô WC (trái hướng +Y = phía −X)", B(sec.Left?.SlabId == wc.Id && Math.Abs(sec.DzL + 50) < 1e-9), 1, 0);
        check("Không hạ cốt: hai cánh Dz = 0", Math.Abs(BeamSlabContext.At(pB, Syn(0.30), 2250, dsB).DzL), 0, 0);

        // ---------------- 6. kiểm tra dầm biên h ≥ hs1 + Δ + hs2 ----------------
        var c30 = SlabDropChecks.BeamChecks(p, new DesignSummary { Beams = { Syn(0.30) } }).FirstOrDefault();
        check("Dầm 220×300, sàn 100, Δ = 50: h yêu cầu = 100 + 50 + 100 = 250 mm", c30?.HReq ?? -1, 250, 0);
        check("Dầm 220×300: đạt", B(c30 != null && c30.Ok), 1, 0);
        var c22 = SlabDropChecks.BeamChecks(p, new DesignSummary { Beams = { Syn(0.22) } }).FirstOrDefault();
        check("Dầm cao 220 < 250: KHÔNG đạt, đề nghị h ≥ 250", B(c22 != null && !c22.Ok && Math.Abs(c22.Suggested - 250) < 1e-9 && SlabDropChecks.Text(c22).StartsWith("CẢNH BÁO")), 1, 0);
        var dD = dsD.Beams.Where(b => b.Story == st).FirstOrDefault(b => b.Frames.Count == ids.Count && b.Frames.All(ids.Contains))
                 ?? dsD.Beams.FirstOrDefault(b => b.RebarKeys.Contains("D:" + ids.Min()));
        check("Thiết kế: dầm trục giữa có lời kiểm tra \"Dầm biên ô sàn hạ cốt … đạt\"",
            B(dD != null && dD.Messages.Any(m => m.StartsWith("Dầm biên ô sàn hạ cốt") && m.EndsWith("đạt"))), 1, 0);
        check("Thiết kế Δ = 50: không có cảnh báo dầm biên", B(!dsD.Warnings.Any(w => w.Contains("Dầm biên ô sàn hạ cốt"))), 1, 0);
        var pDeep = p.Clone();
        SlabDrop.SetDrop(pDeep, pDeep.Slabs.First(s => s.Id == wc.Id), -0.15, "WC hạ sâu");
        var dsDeep = ConcreteDesigner.Design(pDeep, FrameAnalysis.Run(pDeep));
        check("Δ = 150 với dầm cao 300 (yêu cầu 350): cảnh báo + đề nghị h ≥ 350",
            B(dsDeep.Warnings.Any(w => w.Contains("CẢNH BÁO Dầm biên ô sàn hạ cốt") && w.Contains("h ≥ 350"))), 1, 0);
        check("Cảnh báo dầm biên không đổi trạng thái đạt của dầm (không phải LỖI)",
            B(dsDeep.Beams.All(b => !b.Messages.Any(m => m.StartsWith("LỖI") && m.Contains("hạ cốt")))), 1, 0);

        // ---------------- 7. thiết kế sàn: cạnh bậc khớp, mũ không liên tục ----------------
        var sdD = DesignOf(dsD, wc); var sdB = DesignOf(dsB, pB.Slabs.First(s => s.Id == wc.Id));
        var srD = DesignOf(dsD, room);
        check("Không hạ cốt: ô WC có 1 cạnh ngàm (cạnh chung)", B(sdB != null && sdB.EdgeCondition.StartsWith("1 cạnh ngàm")), 1, 0);
        check("Hạ cốt: cạnh chung thành khớp → ô WC 4 cạnh khớp, ô phòng 4 cạnh khớp",
            B(sdD != null && srD != null && sdD.EdgeCondition == "4 cạnh khớp" && srD.EdgeCondition == "4 cạnh khớp"), 1, 0);
        check("Ô WC ghi chú \"Sàn hạ cốt Δ = -50\" và cạnh giáp ô chênh cốt",
            B(sdD != null && sdD.Messages.Any(m => m.StartsWith("Sàn hạ cốt Δ = -50")) && sdD.Messages.Any(m => m.Contains("chênh cốt"))), 1, 0);
        check("Cạnh khớp → mô men nhịp phương ngắn ô WC tăng (M1 hạ cốt > M1 liên tục)", B(sdD != null && sdB != null && sdD.M1 > sdB.M1 + 1e-6), 1, 0);
        var ebB = EdgeBarsAtX(dsB, st, 4.0); var ebD = EdgeBarsAtX(dsD, st, 4.0);
        check("Không hạ cốt: cạnh chung có MỘT thanh mũ vắt qua dầm (Shared)", B(ebB.Count == 1 && ebB[0].Shared), 1, 0);
        check("Hạ cốt: không có thanh mũ vắt qua bậc", B(ebD.All(e => !e.Shared)), 1, 0);
        check("Hạ cốt: mỗi ô có thanh mũ riêng tại cạnh bậc (2 thanh, 'mũ cấu tạo cạnh hạ cốt')",
            B(ebD.Count == 2 && ebD.All(e => (e.Bar.Role ?? "").Contains("hạ cốt")) && Math.Sign(ebD[0].NX) != Math.Sign(ebD[1].NX)), 1, 0);
        check("Thanh mũ cạnh bậc neo qua dầm (Ext2 ≤ ½ bề rộng dầm)", B(ebD.Count > 0 && ebD.All(e => e.Ext2 <= 0.11 + 1e-9 && e.Ext1 > 0.5)), 1, 0);
        // lưới trên liên tục dừng tại bậc
        var pMesh = p.Clone(); pMesh.RebarPrefs.SlabScheme = SlabRebarScheme.TwoLayerMesh;
        var pMeshB = pB.Clone(); pMeshB.RebarPrefs.SlabScheme = SlabRebarScheme.TwoLayerMesh;
        var dsM = ConcreteDesigner.Design(pMesh, FrameAnalysis.Run(pMesh));
        var dsMB = ConcreteDesigner.Design(pMeshB, FrameAnalysis.Run(pMeshB));
        bool Cross(DesignSummary d) => d.Slabs.Where(x => x.Story == st).SelectMany(x => x.TopMeshes).Any(m => m.AlongX && m.X1 < 4.0 - 0.01 && m.X2 > 4.0 + 0.01);
        check("Lưới 2 lớp không hạ cốt: lưới trên phương X liên tục qua trục giữa", B(Cross(dsMB)), 1, 0);
        check("Lưới 2 lớp có hạ cốt: lưới trên dừng tại bậc", B(!Cross(dsM)), 1, 0);

        // ---------------- 8. bản vẽ ----------------
        var story = p.Stories.First(s => s.Name == st);
        var fp = FramingPlan.Build(p, dsD, story, 100);
        var fpTexts = fp.Texts();
        check("Mặt bằng kết cấu: chữ \"SÀN HẠ COS -50\"", B(fpTexts.Any(t => t == "SÀN HẠ COS -50")), 1, 0);
        check("Mặt bằng kết cấu: cao độ ▽ -0.050 và chú thích", B(fpTexts.Contains("-0.050") && fpTexts.Any(t => t.StartsWith("SÀN HẠ COS: mặt sàn thấp hơn"))), 1, 0);
        var fpB = FramingPlan.Build(pB, dsB, pB.Stories.First(s => s.Name == st), 100);
        int kyD = fp.GetPrimitives().Count(q => q.Layer == Lay.KyHieu), kyB = fpB.GetPrimitives().Count(q => q.Layer == Lay.KyHieu);
        check("Mặt bằng kết cấu: thêm nét gạch chéo / bao nét đứt (lớp KY_HIEU) cho ô hạ cốt", B(kyD > kyB + 5), 1, 0);
        check("Mặt bằng không hạ cốt: không có chữ \"SÀN HẠ COS\"", B(!fpB.Texts().Any(t => t.StartsWith("SÀN HẠ COS"))), 1, 0);
        fp.Save(Path.Combine(outDir, "SanHaCot_MatBangKetCau.dxf"));
        var sp = SlabDrawing.Build(p, dsD, story, 50);
        check("Mặt bằng thép sàn: ghi \"SÀN HẠ COS -50 (▽ -0.050)\"", B(sp.Texts().Any(t => t.StartsWith("SÀN HẠ COS -50"))), 1, 0);
        if (dD != null)
        {
            List<Figure> figs;
            // thiết kế gộp (SL > 1) có thể giữ phần tử của dải khác → vẽ với đúng dải trục giữa tầng T2
            var keep = dD.Frames; string ks = dD.Story; double kz = dD.TopElevation;
            dD.Frames = ids; dD.Story = st; dD.TopElevation = z2;
            try { using (DrawingStyle.Use(DrawingStyle.Default())) figs = BeamDrawing.Build(p, dD, dsD); }
            finally { dD.Frames = keep; dD.Story = ks; dD.TopElevation = kz; }
            check("Mặt cắt dầm cạnh ô hạ cốt: kích thước bậc \"Δ=50\" và ghi chú \"Sàn hạ cốt Δ = 50\"",
                B(figs.Skip(1).Any(f => f.W.Texts().Contains("Δ=50") && f.W.Texts().Any(t => t.Contains("Sàn hạ cốt Δ = 50")))), 1, 0);
            check("Mặt đứng dầm: ghi chú \"Sàn hạ cốt … Δ = 50\"", B(figs[0].W.Texts().Any(t => t.Contains("Sàn hạ cốt") && t.Contains("Δ = 50"))), 1, 0);
        }
        else check("Thiết kế: tìm thấy dải dầm trục giữa tầng T2 (để vẽ mặt cắt)", 0, 1, 0);

        // ---------------- 9. khối lượng + thuyết minh ----------------
        var qty = QuantityCalculator.Compute(p, dsD);
        var sheets = QuantityReport.BuildSheets(p, qty);
        var qt = sheets[5].Tables.FirstOrDefault(t => t.Title.StartsWith("Sàn hạ cốt"));
        check("Bảng khối lượng 'Hoàn thiện sàn': bảng ô sàn hạ cốt (1 ô + dòng tổng)", qt?.Rows.Count ?? 0, 2, 0);
        check("Bảng khối lượng: diện tích ô WC 4,0 × 4,5 = 18 m²", qt != null ? (double)qt.Rows[0][5] : 0, 18.0, 1e-9);
        check("Dự án không hạ cốt: không thêm bảng", B(QuantityReport.BuildSheets(pB, QuantityCalculator.Compute(pB, dsB))[5].Tables.All(t => !t.Title.StartsWith("Sàn hạ cốt"))), 1, 0);
        string docx = Path.Combine(outDir, "SanHaCot_ThuyetMinh.docx");
        CalculationReport.Export(p, rD, dsD, docx);
        string xml = "";
        try
        {
            using var z = ZipFile.OpenRead(docx);
            using var sr = new StreamReader(z.GetEntry("word/document.xml").Open());
            xml = sr.ReadToEnd();
        }
        catch (Exception ex) { Console.WriteLine("   · đọc thuyết minh lỗi: " + ex.Message); }
        check("Thuyết minh: mục \"Sàn hạ cốt (sàn vệ sinh, ban công, lô gia…)\" + kiểm tra h ≥ hs1 + Δ + hs2",
            B(xml.Contains("Sàn hạ cốt (sàn vệ sinh, ban công, lô gia") && xml.Contains("hs1 + Δ + hs2")), 1, 0);
    }
}
