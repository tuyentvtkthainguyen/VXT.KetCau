using System.Text.Json;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>[LùiCọcRanh] + [CừTràm] Kiểm chứng:
/// lùi cọc khỏi ranh giới để ép cọc (mọi cọc đài biên cách ranh ≥ 0,5 m theo tim, đòn bẩy R1 = N1·L/(L − e) với e mới, kiểm tra công xôn đài, tuỳ chọn mép đài);
/// cừ tràm (sức chịu tải 1 cây trên nền 2 lớp – thân côn, nhóm + [p], hệ số rỗng sau gia cố, khối quy ước φ/4 + áp lực mũi, lún, độ bền MNN đạt / không đạt,
/// khối lượng = ⌈mật độ × diện tích⌉, JSON, bản vẽ "CỪ TRÀM" + MNN, gợi ý phương án). Giá trị kỳ vọng tính lại độc lập trong test.</summary>
public static class PileSetbackCuTramTests
{
    private static double B(bool x) => x ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Console.WriteLine("   [LùiCọcRanh] lùi cọc khỏi ranh giới để ép cọc");
        AdjustUnit(check);
        var (p, r) = TownHouse();
        SetbackDesign(check, p, r, outDir);
        OneBay(check);
        Console.WriteLine("   [CừTràm] gia cố nền bằng cừ tràm");
        CuTramFormulas(check);
        CuTramDesign(check, outDir);
        Json(check);
        Advisor(check);
    }

    /// <summary>Nhà phố 2 nhịp 3,6 + 3,6 m × 4,5 + 4,5 m, 3 tầng, cột 220; ranh giới trái / phải tại mép ngoài cột trục đầu / cuối.</summary>
    private static (Project, AnalysisResults) TownHouse()
    {
        var p = ModelBuilder.NewProject("Nhà phố – lùi cọc / cừ tràm");
        ModelBuilder.SetGrid(p, new[] { 3.6, 3.6 }, new[] { 4.5, 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x22", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5 });
        var r = FrameAnalysis.Run(p);
        var gx = p.GridsX.ToList();
        p.Foundation.Boundaries = new List<PropertyBoundary>
        {
            new() { Side = BoundarySide.Left, Grid = gx[0].Name, Offset = 0.11 },
            new() { Side = BoundarySide.Right, Grid = gx[^1].Name, Offset = 0.11 }
        };
        return (p, r);
    }

    // ------------------------------------------------------------------
    private static void AdjustUnit(Action<string, double, double, double> check)
    {
        var p = new Project();
        p.Foundation.Boundaries.Add(new PropertyBoundary { Side = BoundarySide.Left, Grid = "", Coord = -0.11 });
        p.Foundation.PileSetbackEnabled = true;
        p.Foundation.PileSetback = 0.5;
        var piles = new List<(double X, double Y)> { (-0.375, 0), (0.375, 0) };
        // đài 2 cọc d = 250, mép 150 mm dịch sát ranh: tâm đài ox = 0,515 (mép trái = −0,11)
        double L0 = 1.3, ox0 = -0.11 + L0 / 2;
        var sh = piles.Select(q => (X: q.X + ox0, q.Y)).ToList();
        var a = PileSetback.Adjust(p, 0, 0, 0.22, 0.22, 0.25, 0.15, sh, L0, 0.55, ox0, 0);
        double need = -0.11 + 0.5 - (0 + sh.Min(q => q.X));
        check("Lùi cọc: độ dịch Δx = (x_ranh + s) − x_cọc,min", a.Info.ShiftX, Math.Ceiling(need * 1000 - 1e-6) / 1000, 1e-9);
        check("Lùi cọc: tim cọc gần ranh nhất cách ranh ≥ 0,5 m", a.Piles.Min(q => q.X) - (-0.11), 0.5, 1e-3);
        check("Lùi cọc: đài vươn ra sát ranh (mép trái = ranh giới)", a.Ox - a.L / 2, -0.11, 1e-6);
        check("Lùi cọc: mép phải đài = cọc xa + d/2 + mép", a.Ox + a.L / 2, a.Piles.Max(q => q.X) + 0.125 + 0.15, 1e-3);
        check("Lùi cọc: có ghi cạnh ranh giới trái", a.Info.Sides.Count(sd => sd.Side == BoundarySide.Left), 1, 0);
        // mép đài theo cọc: cột tại ranh → mép đài vẫn phủ cột (mép cột = ranh)
        p.Foundation.CapToBoundary = false;
        var b = PileSetback.Adjust(p, 0, 0, 0.22, 0.22, 0.25, 0.15, sh, L0, 0.55, ox0, 0);
        check("Mép đài theo cọc: mép trái đài = mép cột (phủ cột)", b.Ox - b.L / 2, -0.11, 1e-6);
        p.Foundation.CapToBoundary = true;
        // đo tới mép cọc
        p.Foundation.PileSetbackToFace = true;
        var c = PileSetback.Adjust(p, 0, 0, 0.22, 0.22, 0.25, 0.15, sh, L0, 0.55, ox0, 0);
        check("Lùi tới mép cọc: mép cọc cách ranh ≥ 0,5 m", c.Piles.Min(q => q.X) - 0.125 - (-0.11), 0.5, 1e-3);
        p.Foundation.PileSetbackToFace = false;
        // cạnh không có nhà bên cạnh: PileSetback = 0 → không lùi
        p.Foundation.Boundaries[0].PileSetback = 0;
        check("Cạnh ranh giới không cần lùi (0): không có đường giới hạn", PileSetback.Lines(p).Count, 0, 0);
        p.Foundation.Boundaries[0].PileSetback = -1;
        p.Foundation.PileSetbackEnabled = false;
        check("Tắt lùi cọc: không áp dụng", B(PileSetback.Active(p)), 0, 0);
    }

    // ------------------------------------------------------------------
    private static void SetbackDesign(Action<string, double, double, double> check, Project p, AnalysisResults r, string outDir)
    {
        var save = JsonSerializer.Serialize(p.Foundation, Project.JsonOptions);
        try
        {
            p.Foundation.Type = FoundationType.Pile;
            p.Foundation.PileCapacity = 300;
            p.Foundation.PileSize = 0.25;
            p.Foundation.PileSetbackEnabled = true;
            p.Foundation.PileSetback = 0.5;
            p.Foundation.EccentricDefault = EccentricScheme.Auto;
            var s = ConcreteDesigner.Design(p, r);
            check("Lùi cọc: không lỗi thiết kế đài", s.Warnings.Count(w => w.StartsWith("LỖI thiết kế đài")), 0, 0);
            var caps = s.PileCaps.Where(c => c.Setback != null && c.Setback.Shifted).ToList();
            check("Lùi cọc: có đài biên phải lùi cọc", B(caps.Count > 0), 1, 0);
            foreach (var c in caps)
            {
                double dmin = double.MaxValue;
                foreach (var sd in c.Setback.Sides)
                    foreach (var q in c.Piles)
                        dmin = Math.Min(dmin, sd.IsVertical ? sd.Inward * (c.X + q.X - sd.Boundary) : sd.Inward * (c.Y + q.Y - sd.Boundary));
                Console.WriteLine($"   · {c.Name}: {c.Piles.Count} cọc, đài {c.L:0.000}×{c.B:0.000}, ex đài = {c.OffsetX * 1000:0}, e nhóm cọc = {c.Setback.GroupEx * 1000:0} mm, "
                                  + $"Pmax = {c.PMax:0.0} (không DM {c.Setback.PmaxNoStrap:0.0}), công xôn M = {c.Setback.MCant:0.0} kN.m – {c.Setback.Recommended}");
                check($"{c.Name}: mọi cọc cách ranh giới ≥ 0,5 m (theo tim)", dmin, 0.5, 1e-3);
                check($"{c.Name}: mọi cọc cách ranh ≥ 0,5 m (không nhỏ hơn)", B(dmin >= 0.5 - 1e-6), 1, 0);
                bool left = c.Setback.Sides.Any(sd => sd.Side == BoundarySide.Left);
                double edge = left ? c.X + c.OffsetX - c.L / 2 : c.X + c.OffsetX + c.L / 2;
                check($"{c.Name}: đài vươn ra sát ranh (mép đài = ranh giới)", edge, left ? -0.11 : 7.31, 1e-3);
                check($"{c.Name}: ΣPi = N' + G", c.Reactions.Sum(), c.N + c.Weight, 1e-6);
                check($"{c.Name}: có kiểm tra uốn công xôn đài", B(c.Setback.Items.Any(i => i.Name.StartsWith("Uốn công xôn"))), 1, 0);
                check($"{c.Name}: có kiểm tra cắt công xôn đài", B(c.Setback.Items.Any(i => i.Name.StartsWith("Cắt công xôn"))), 1, 0);
                check($"{c.Name}: có kiểm tra phản lực không dầm cân bằng", B(c.Setback.Items.Any(i => i.Name.Contains("KHÔNG dầm cân bằng"))), 1, 0);
                check($"{c.Name}: hạng mục lùi cọc trong bảng an toàn đài", B(c.Safety?.Items.Any(i => i.Group == PileSetback.Group) ?? false), 1, 0);
                var mc = c.Setback.Items.FirstOrDefault(i => i.Name.StartsWith("Uốn công xôn"));
                if (mc != null) check($"{c.Name}: công xôn M = Nmax·a", mc.Value, c.Setback.N1 * Math.Max(c.Setback.CantileverX, c.Setback.CantileverY), 1e-9);
                var ei = c.Eccentric;
                if (ei != null && ei.Scheme == EccentricScheme.Strap && ei.SpanX > 0)
                {
                    double e = Math.Abs(c.Piles.Average(q => q.X));
                    check($"{c.Name}: e đòn bẩy = trọng tâm nhóm cọc (≥ 0,5 − 0,11)", Math.Abs(ei.OffsetX), e, 1e-9);
                    check($"{c.Name}: e ≥ 0,39 m sau khi lùi", B(e >= 0.39 - 1e-6), 1, 0);
                    check($"{c.Name}: R1 = N1·L/(L − e) với e mới", ei.R1, EccentricFooting.LeverReaction(ei.N1, 0, ei.SpanX, e), 1e-9);
                    check($"{c.Name}: có kiểm tra phản lực có dầm cân bằng", B(c.Setback.Items.Any(i => i.Name.Contains("CÓ dầm cân bằng"))), 1, 0);
                    double gxm = c.Piles.Average(q => q.X), m = 0;
                    for (int i = 0; i < c.Piles.Count; i++) m += (c.Piles[i].X - gxm) * c.Reactions[i];
                    check($"{c.Name}: dầm cân bằng → phản lực đều theo phương dầm", m, 0, 1e-6);
                }
            }
            check("Lùi cọc: có dầm móng cân bằng DM", B(s.StrapBeams.Count > 0), 1, 0);
            foreach (var sb in s.StrapBeams.Where(x => x.PileCap).Take(2))
                check($"{sb.Name}: M ≥ N1·e1 (e1 = e nhóm cọc sau khi lùi ≥ 0,39)", B(sb.E1 >= 0.39 - 1e-6 && sb.M >= sb.Links[0].LoadsA.Max(a => a.N) * sb.E1 - 1e-6), 1, 0);
            // bản vẽ + thuyết minh
            var figs = FootingDrawing.BuildAll(p, s);
            string plan = figs[0].W.ToDxfString();
            check("Bản vẽ: mặt bằng móng có '≥ 500 (lùi ép cọc)'", B(plan.Contains(DxfWriter.Encode("≥ 500 (lùi ép cọc)"))), 1, 0);
            var capFig = figs.FirstOrDefault(f => caps.Count > 0 && f.Name == caps[0].Name);
            check("Bản vẽ: chi tiết đài có ranh giới + kích thước lùi ép cọc", B(capFig != null && capFig.W.ToDxfString().Contains(DxfWriter.Encode("lùi ép cọc"))), 1, 0);
            check("Bản vẽ: có dầm cân bằng DM trên mặt bằng", B(plan.Contains("DM1 (")), 1, 0);
            string dir = Path.Combine(string.IsNullOrEmpty(outDir) ? Path.GetTempPath() : outDir, "LuiCocCuTram");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "mb_lui_coc.dxf"), plan);
            CalculationReport.Export(p, r, s, Path.Combine(dir, "tm_lui_coc.docx"));
            check("Thuyết minh có mục lùi cọc (xuất được)", B(File.Exists(Path.Combine(dir, "tm_lui_coc.docx"))), 1, 0);
            // đài console tự cân bằng (không dầm)
            p.Foundation.EccentricDefault = EccentricScheme.SelfBalanced;
            var s2 = ConcreteDesigner.Design(p, r);
            var c2 = s2.PileCaps.FirstOrDefault(c => c.Setback != null && c.Setback.Shifted);
            check("Đài console: có đài lùi cọc", B(c2 != null), 1, 0);
            if (c2 != null)
            {
                var it = c2.Setback.Items.First(i => i.Name.Contains("KHÔNG dầm cân bằng") && i.Name.StartsWith("Phản lực"));
                check("Đài console: Pmax không dầm = Pmax thiết kế", it.Value, c2.PMax, 1e-6);
                check("Đài console: hạng mục là kiểm tra thật (không chỉ thông tin)", B(it.Status != CheckStatus.Info), 1, 0);
                check("Đài console: có phương án đề xuất", B(c2.Setback.Options.Count == 3 && c2.Setback.Recommended.Length > 0), 1, 0);
            }
            // mép đài theo cọc
            p.Foundation.EccentricDefault = EccentricScheme.Auto;
            p.Foundation.CapToBoundary = false;
            var s3 = ConcreteDesigner.Design(p, r);
            check("Mép đài theo cọc: không lỗi thiết kế", s3.Warnings.Count(w => w.StartsWith("LỖI thiết kế đài")), 0, 0);
            check("Mép đài theo cọc: đài không vượt ranh giới", B(s3.PileCaps.All(c => c.X + c.OffsetX - c.L / 2 >= -0.11 - 1e-6 && c.X + c.OffsetX + c.L / 2 <= 7.31 + 1e-6)), 1, 0);
            // cạnh phải không có nhà bên cạnh
            p.Foundation.CapToBoundary = true;
            p.Foundation.Boundaries.First(b => b.Side == BoundarySide.Right).PileSetback = 0;
            var s4 = ConcreteDesigner.Design(p, r);
            check("Cạnh phải không lùi: không đài nào lùi phía phải", s4.PileCaps.Count(c => c.Setback != null && c.Setback.Shifted && c.Setback.Sides.Any(sd => sd.Side == BoundarySide.Right && Math.Abs(sd.Shift) > 1e-6)), 0, 0);
            check("Cạnh phải không lùi: vẫn lùi phía trái", B(s4.PileCaps.Any(c => c.Setback != null && c.Setback.Shifted)), 1, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("   · lỗi lùi cọc: " + ex);
            check("Lùi cọc khỏi ranh giới – chạy không lỗi", 0, 1, 0);
        }
        finally { p.Foundation = JsonSerializer.Deserialize<FoundationSettings>(save, Project.JsonOptions); }
    }

    // ------------------------------------------------------------------
    /// <summary>[CặpĐàiBiên] Nhà phố 1 nhịp 5 m (X) × 3 nhịp 4,5 m (Y), 4 tầng, ranh giới hai phía, lùi ép cọc 500 mm: mọi cột là cột biên,
    /// dầm cân bằng chung giữa hai đài biên đối diện. Trước khi sửa: e2 lấy theo ước lượng móng đơn (≈ L/2) → R1 ≈ 0, Pmax ≈ 20 kN, cọc bị nhổ → đài KHÔNG ĐẠT.</summary>
    private static void OneBay(Action<string, double, double, double> check)
    {
        var p = ModelBuilder.NewProject("Nhà phố 1 nhịp – lùi cọc hai phía");
        ModelBuilder.SetGrid(p, new[] { 5.0 }, new[] { 4.5, 4.5, 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.9, 3.3, 3.3, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5 });
        var r = FrameAnalysis.Run(p);
        var gx = p.GridsX.ToList();
        var fs = p.Foundation;
        fs.Boundaries = new List<PropertyBoundary>
        {
            new() { Side = BoundarySide.Left, Grid = gx[0].Name, Offset = 0.11 },
            new() { Side = BoundarySide.Right, Grid = gx[^1].Name, Offset = 0.11 }
        };
        fs.Type = FoundationType.Pile;
        fs.PileSize = 0.25; fs.PileCapacity = 450; fs.PileTension = 0;
        fs.PileSetbackEnabled = true; fs.PileSetback = 0.5;
        fs.EccentricDefault = EccentricScheme.Auto;
        try
        {
            var s = ConcreteDesigner.Design(p, r);
            var combos = r.DesignCombos();
            check("1 nhịp 5 m: không lỗi thiết kế đài", s.Warnings.Count(w => w.StartsWith("LỖI thiết kế đài")), 0, 0);
            check("1 nhịp 5 m: tổng số đài (kể gộp) = 8", s.PileCaps.Sum(c => Math.Max(1, c.Quantity)), 8, 0);
            foreach (var c in s.PileCaps)
            {
                var ei = c.Eccentric; var sb = c.Setback;
                Console.WriteLine($"   · {c.Name} ×{c.Quantity}: {c.Piles.Count} cọc, đài {c.L:0.000}×{c.B:0.000}×{c.H:0.00}, e = {(sb?.GroupEx ?? 0) * 1000:0} mm, e2 = {(sb?.E2 ?? 0) * 1000:0} mm, "
                                  + $"N' = {c.N:0.0}, Pmax = {c.PMax:0.0}, Pmin = {c.PMin:0.0} kN – {(c.Ok ? "đạt" : "KHÔNG ĐẠT")}");
                if (!c.Ok)
                {
                    foreach (var ck in c.Checks.Where(x => !x.Ok)) Console.WriteLine($"       ✗ {ck.Name}: {ck.Demand:0.0} / {ck.Capacity:0.0} {ck.Unit} {ck.Note}");
                    foreach (var m in c.Messages.Where(x => x.StartsWith("LỖI"))) Console.WriteLine("       ✗ " + m);
                }
                check($"{c.Name}: đạt (1 nhịp, lùi cọc hai phía)", B(c.Ok), 1, 0);
                check($"{c.Name}: có lùi cọc", B(sb != null && sb.Shifted), 1, 0);
                if (sb == null || ei == null) continue;
                double dmin = sb.Sides.SelectMany(sd => c.Piles.Select(q => sd.Inward * (c.X + q.X - sd.Boundary))).Min();
                check($"{c.Name}: tim cọc cách ranh ≥ 0,5 m", B(dmin >= 0.5 - 1e-6), 1, 0);
                check($"{c.Name}: hàng cọc song song ranh giới (e nhỏ nhất)", c.Piles.Select(q => Math.Round(q.X, 4)).Distinct().Count(), 1, 0);
                check($"{c.Name}: phản lực cọc không quá nhỏ (Pmax ≥ 0,25·N/n)", B(c.PMax >= 0.25 * sb.N1 / c.Piles.Count), 1, 0);
                check($"{c.Name}: không cọc bị nhổ (Pmin ≥ 0)", B(c.PMin >= -1e-6), 1, 0);
                check($"{c.Name}: dầm cân bằng chung với đài đối diện", B(ei.Scheme == EccentricScheme.Strap && sb.OppositePair), 1, 0);
                var pc = s.PileCaps.FirstOrDefault(q => q.NodeIds.Contains(ei.PartnerX));
                if (pc?.Eccentric == null) { check($"{c.Name}: tìm thấy đài đối diện", 0, 1, 0); continue; }
                double e1 = Math.Abs(ei.OffsetX), e2 = Math.Abs(pc.Eccentric.OffsetX);
                check($"{c.Name}: e2 = độ lệch tâm thật của đài đối diện (không phải ước lượng)", Math.Abs(sb.E2 - e2), 0, 0.006);
                int own = ei.Locations[0].NodeId;
                var g = FoundationUtil.Loads(r, own, combos).OrderByDescending(l => l.N).First();
                var g2 = FoundationUtil.Loads(r, ei.PartnerX, combos).First(l => l.Combo == g.Combo);
                double R1 = EccentricFooting.LeverReaction(g.N, g2.N, ei.SpanX, e1, sb.E2), R2 = EccentricFooting.LeverReaction(g2.N, g.N, ei.SpanX, sb.E2, e1);
                check($"{c.Name}: R1 = [N1(L − e2) − N2·e2]/(L − e1 − e2)", ei.R1, R1, 1e-9);
                check($"{c.Name}: R1 + R2 = N1 + N2 (cân bằng lực)", R1 + R2, g.N + g2.N, 1e-9);
                check($"{c.Name}: ΣM tại cột 1 = 0: R1·e1 + R2·(L − e2) = N2·L", R1 * e1 + R2 * (ei.SpanX - sb.E2), g2.N * ei.SpanX, 1e-9);
                check($"{c.Name}: hai phía cân bằng → R1 ≈ N1 (|ΔR| < 30%)", B(Math.Abs(R1 - g.N) < 0.3 * g.N), 1, 0);
                var it = sb.Items.FirstOrDefault(i => i.Name.StartsWith("Hai đài biên đối diện"));
                check($"{c.Name}: kiểm tra hợp lực hai đài đối diện đạt", B(it != null && it.Status == CheckStatus.Ok), 1, 0);
            }
            check("1 nhịp 5 m: 4 dầm cân bằng ngang nhịp (kể gộp)", s.StrapBeams.Sum(x => Math.Max(1, x.Quantity)), 4, 0);
            foreach (var sb in s.StrapBeams)
            {
                Console.WriteLine($"   · {sb.Name} ×{sb.Quantity}: {sb.B * 1000:0}×{sb.H * 1000:0}, L = {sb.Span:0.00}, e1 = {sb.E1 * 1000:0}, e2 = {sb.E2 * 1000:0} mm, M = {sb.M:0.0}, Q = {sb.V:0.0} – {(sb.Ok ? "đạt" : "KHÔNG ĐẠT")}");
                check($"{sb.Name}: dầm cân bằng chung 2 đài biên – e2 > 0", B(sb.E2 > 0.3), 1, 0);
                check($"{sb.Name}: đạt", B(sb.Ok), 1, 0);
            }
            // hộp thoại bố trí cọc: đài đối diện được thiết kế trước → cùng kết quả
            var c0 = s.PileCaps.First(c => c.Eccentric != null);
            var t = PileCapDesigner.DesignNode(p, r, c0.Eccentric.Locations[0].NodeId);
            check("Thiết kế thử 1 đài (hộp thoại): e2 thật – Pmax như thiết kế toàn bộ (±2%)", t.PMax, c0.PMax, 0.02);
            check("Thiết kế thử 1 đài: e2 thật của đài đối diện", Math.Abs((t.Setback?.E2 ?? 0) - (c0.Setback?.E2 ?? 0)), 0, 0.006);
        }
        catch (Exception ex)
        {
            Console.WriteLine("   · lỗi nhà 1 nhịp: " + ex);
            check("Nhà 1 nhịp lùi cọc hai phía – chạy không lỗi", 0, 1, 0);
        }
    }

    // ------------------------------------------------------------------
    /// <summary>Nền 2 lớp: 0–3 m bùn sét IL = 0,8 (φ = 4°, c = 8, e = 1,5); 3–12 m sét pha IL = 0,5 (φ = 14°, c = 20); MNN 0,5 m.</summary>
    private static Borehole TwoLayer() => new()
    {
        Name = "HK-CT", X = 3.6, Y = 4.5, WaterDepth = 0.5,
        Layers =
        {
            new SoilLayer { Name = "Bùn sét", Kind = SoilKind.Organic, Top = 0, Bottom = 3, Gamma = 16, GammaSat = 16.5, Phi = 4, C = 8, E = 1.8, N = 2, IL = 0.8, VoidRatio = 1.5 },
            new SoilLayer { Name = "Sét pha dẻo mềm", Kind = SoilKind.SiltyClay, Top = 3, Bottom = 12, Gamma = 18.5, GammaSat = 19, Phi = 14, C = 20, E = 6, N = 6, IL = 0.5, VoidRatio = 0.9 }
        }
    };

    private static void CuTramFormulas(Action<string, double, double, double> check)
    {
        var cs = new CuTramSettings();   // Lc = 4, D1 = 0,08, D2 = 0,045, 25 cây/m², lót 0,10, cát 0,10, FS = 2
        var soil = new SoilSettings { Depth = 1.0 };
        var bh = TwoLayer();
        var r = CuTramCalc.Evaluate(cs, soil, bh, false, 1.2, 1.2, 1.0, 120, 140, 150);
        check("Cừ tràm: đầu cừ = Df + t_lót = 1,1 m", r.TopDepth, 1.1, 1e-12);
        check("Cừ tràm: mũi cừ = 1,1 + 4 = 5,1 m", r.TipDepth, 5.1, 1e-12);
        // bảng fi (TCVN 10304:2014): IL 0,8: z=1→4, 2→5, 3→7;  IL 0,5: z=3→20, 4→22, 5→24
        check("Bảng fi: z = 2, IL = 0,8 → 5 kPa", CuTramCalc.Friction(2, 0.8), 5, 1e-12);
        check("Bảng fi: z = 4,5, IL = 0,5 → 23 kPa (nội suy)", CuTramCalc.Friction(4.5, 0.5), 23, 1e-12);
        double u = Math.PI * (0.08 + 0.045) / 2;
        double sum = 0.9 * (4 + 0.65 * 1) + 0.9 * (5 + 0.55 * 2)        // 1,2–2,1 (z 1,65) và 2,1–3,0 (z 2,55) trong bùn
                   + 0.7 * (20 + 0.35 * 2) + 0.7 * (22 + 0.05 * 2) + 0.7 * (22 + 0.75 * 2);   // 3,0–5,1 chia 3 đoạn trong sét pha
        check("Cừ đơn: u = π(D1 + D2)/2 (thân côn)", r.U, u, 1e-12);
        check("Cừ đơn: Qs = u·Σfi·li (2 lớp, đoạn ≤ 1 m)", r.Qs, u * sum, 1e-9);
        double ap = Math.PI * 0.045 * 0.045 / 4;
        check("Cừ đơn: Qp = 9·c·Ap (mũi trong sét pha)", r.Qp, 9 * 20 * ap, 1e-9);
        check("Cừ đơn: Qc = Qs + Qp", r.Qc, u * sum + 9 * 20 * ap, 1e-9);
        check("Cừ đơn: Qa = min(Qc/FS; Pvl)", r.Qa, Math.Min(r.Qc / 2, 10000 * ap), 1e-12);
        // nhóm
        check("Nhóm: n = ⌈25 × 1,2 × 1,2⌉ = 36", r.Count, 36, 0);
        double th = Math.Atan(0.0625 / 0.2) * 180 / Math.PI, eta = 1 - th / 90 * (5 * 6 + 5 * 6) / 36.0;
        check("Nhóm: η Converse–Labarre (6×6, d/s = 0,0625/0,2)", r.Eta, eta, 1e-9);
        double m = 36 * Math.PI * 0.0625 * 0.0625 / 4 / 1.44;
        check("Nhóm: tỷ lệ thay thế m = n·Ap,tb/A", r.AreaRatio, m, 1e-9);
        check("[p]1 = (η·n·Qa − Gcừ)/A + αs·(1 − m)·R0", r.P1, (eta * 36 * r.Qa - r.TimberWeight) / 1.44 + 0.8 * (1 - m) * r.R0, 1e-9);
        check("Gcừ = n·V/Lc·[γ·(Lc − l_ngập) + (γ − 10)·l_ngập] – cừ ngập hoàn toàn, γ = 10 → 0", r.TimberWeight, 0, 1e-12);
        check("Thể tích cây cừ thân côn V = πL/12·(D1² + D1D2 + D2²)", r.Volume1, Math.PI * 4 / 12 * (0.0064 + 0.0036 + 0.002025), 1e-12);
        // nền tương đương
        double vr = 25 * Math.PI / 12 * (0.0064 + 0.08 * 0.045 + 0.045 * 0.045);
        check("Nền tương đương: Vc/V = mật độ·Vcừ/Lc", r.VolumeRatio, vr, 1e-12);
        check("Nền tương đương: e1 = e0 − (1 + e0)·Vc/V", r.E1, 1.5 - 2.5 * vr, 1e-12);
        check("Hàm ImprovedVoidRatio", CuTramCalc.ImprovedVoidRatio(1.2, 0.08), 1.2 - 2.2 * 0.08, 1e-12);
        check("c1 = c0·e0/e1 (kinh nghiệm)", r.C1, 8 * 1.5 / (1.5 - 2.5 * vr), 1e-9);
        // khối quy ước: φtb = (4·1,8 + 14·2,1)/3,9; mở rộng 2·(z_mũi − Df)·tg(φtb/4)
        double phi = (4 * 1.8 + 14 * 2.1) / 3.9, ext = 2 * (5.1 - 1.0) * Math.Tan(phi / 4 * Math.PI / 180);
        check("Khối quy ước: φtb gia quyền", r.PhiAvg, phi, 1e-9);
        check("Khối quy ước: Lqu = L + 2(z_mũi − Df)·tg(φtb/4)", r.Lqu, 1.2 + ext, 1e-9);
        double W = (1.2 + ext) * (1.2 + ext) * (GeotechCalc.EffectiveStress(bh, 5.1) - GeotechCalc.EffectiveStress(bh, 1.0));
        check("Khối quy ước: W = Aqu·[σ'bt(z_mũi) − σ'bt(Df)]", r.BlockWeight, W, 1e-9);
        check("Khối quy ước: σ mũi = (ptb·A + W)/Aqu", r.SigmaTip, (120 * 1.44 + W) / ((1.2 + ext) * (1.2 + ext)), 1e-9);
        check("[p]2 = min(R1; (R_mũi·Aqu − W)/A)", r.P2, Math.Min(r.R1, (r.RTip * (1.2 + ext) * (1.2 + ext) - W) / 1.44), 1e-9);
        check("[p] thiết kế = min([p]1; [p]2) (mặc định)", r.Allowable, Math.Min(r.P1, r.P2), 1e-12);
        var st = GeotechCalc.Settlement(bh, 1.2 + ext, 1.2 + ext, 5.1, r.SigmaTip);
        check("Lún khối quy ước = GeotechCalc.Settlement(Lqu, Bqu, z_mũi, σ mũi)", r.Settlement.S, st.S, 1e-12);
        // độ bền
        check("Độ bền: MNN 0,5 m < đầu cừ 1,1 m → đạt", B(r.DurabilityOk && r.Items.First(i => i.Name.StartsWith("Độ bền")).Status == CheckStatus.Ok), 1, 0);
        cs.MinWaterDepth = 2.0;
        var r2 = CuTramCalc.Evaluate(cs, soil, bh, false, 1.2, 1.2, 1.0, 120, 140, 150);
        var d2 = r2.Items.First(i => i.Name.StartsWith("Độ bền"));
        check("Độ bền: MNN thấp nhất 2,0 m > đầu cừ → KHÔNG ĐẠT", B(!r2.DurabilityOk && d2.Status == CheckStatus.Fail), 1, 0);
        check("Độ bền: thông báo 'cừ tràm nằm trên mực nước ngầm sẽ mục'", B(d2.Explanation.Contains("cừ tràm nằm trên mực nước ngầm sẽ mục")), 1, 0);
        cs.MinWaterDepth = -1;
        bh.WaterDepth = -1;
        var r3 = CuTramCalc.Evaluate(cs, soil, bh, false, 1.2, 1.2, 1.0, 120, 140, 150);
        check("Độ bền: không gặp nước ngầm → KHÔNG ĐẠT", B(r3.Items.First(i => i.Name.StartsWith("Độ bền")).Status == CheckStatus.Fail), 1, 0);
        // đất không yếu → cảnh báo
        var hard = TwoLayer();
        foreach (var l in hard.Layers) l.N = 15;
        var r4 = CuTramCalc.Evaluate(new CuTramSettings(), soil, hard, false, 1.2, 1.2, 1.0, 120, 140, 900);
        check("Đất không yếu (N = 15) → cảnh báo đóng cừ", B(r4.Items.First(i => i.Name.StartsWith("Đất phù hợp")).Status == CheckStatus.Warning), 1, 0);
        check("Tải chân cột lớn → khuyến nghị cọc BTCT", B(r4.Items.First(i => i.Name.StartsWith("Tải chân cột")).Status == CheckStatus.Warning), 1, 0);
        check("Số cây = ⌈mật độ × diện tích⌉ (1,3 × 1,3 × 25 = 42,25 → 43)", CuTramCalc.CountFor(25, 1.3 * 1.3), 43, 0);
    }

    private static void CuTramDesign(Action<string, double, double, double> check, string outDir)
    {
        var (p, r) = TownHouse();
        p.Foundation.Boundaries.Clear();
        p.Foundation.Type = FoundationType.Isolated;
        p.Soil.Depth = 1.0;
        var bh = TwoLayer();
        bh.X = 3.6; bh.Y = 4.5;
        p.Boreholes.Add(bh);
        p.Foundation.CuTram.Enabled = true;
        try
        {
            var s = ConcreteDesigner.Design(p, r);
            check("Cừ tràm: không lỗi thiết kế móng", s.Warnings.Count(w => w.StartsWith("LỖI thiết kế móng")), 0, 0);
            var fs = s.Footings.Where(f => f.CuTram != null).ToList();
            check("Cừ tràm: mọi móng đơn có kết quả cừ tràm", fs.Count, s.Footings.Count, 0);
            foreach (var f in fs.Take(3))
            {
                Console.WriteLine($"   · {f.Name} ×{f.Quantity}: {f.L:0.00}×{f.B:0.00}, ptb = {f.Ptb:0.0}, [p]1 = {f.CuTram.P1:0.0}, [p]2 = {f.CuTram.P2:0.0}, {f.CuTram.Count} cây, S = {f.CuTram.Settlement?.S * 100:0.00} cm – {(f.Ok ? "đạt" : "KHÔNG ĐẠT")}");
                check($"{f.Name}: R thiết kế = [p] cừ tràm", f.R, f.CuTram.Allowable, 1e-9);
                check($"{f.Name}: số cây = ⌈25·L·B⌉", f.CuTram.Count, (int)Math.Ceiling(25 * f.L * f.B - 1e-9), 0);
                check($"{f.Name}: có kiểm tra độ bền MNN trong danh sách kiểm tra", B(f.Checks.Any(c => c.Name.StartsWith("Cừ tràm: đầu cừ"))), 1, 0);
                check($"{f.Name}: có kiểm tra lún khối quy ước", B(f.Checks.Any(c => c.Name.StartsWith("Cừ tràm: độ lún"))), 1, 0);
            }
            var q = QuantityCalculator.Compute(p, s);
            double trees = q.GroundWorks.Where(x => x.Kind == "Cừ tràm").Sum(x => x.Quantity);
            check("Khối lượng: tổng số cây = Σ SL·⌈mật độ × L × B⌉", trees, s.Footings.Sum(f => Math.Max(1, f.Quantity) * (int)Math.Ceiling(25 * f.L * f.B - 1e-9)), 1e-9);
            check("Khối lượng: tổng chiều dài = số cây × 4 m", q.GroundWorks.Where(x => x.Kind == "Chiều dài cừ").Sum(x => x.Quantity), trees * 4, 1e-9);
            check("Khối lượng: có cát đệm", B(q.GroundWorks.Any(x => x.Kind == "Cát đệm" && x.Quantity > 0)), 1, 0);
            check("Vật tư: có dòng cừ tràm", B(q.Materials.Any(m => m.Material.StartsWith("Cừ tràm") && Math.Abs(m.Quantity - trees) < 1e-6)), 1, 0);
            var figs = FootingDrawing.BuildAll(p, s);
            var ct = figs.Where(f => f.Name.StartsWith("CT ")).ToList();
            check("Bản vẽ: có mặt bằng + mặt cắt cừ tràm", B(ct.Count >= 2), 1, 0);
            string all = string.Join("\n", ct.Select(f => f.W.ToDxfString()));
            check("Bản vẽ: chữ 'CỪ TRÀM'", B(all.Contains(DxfWriter.Encode("CỪ TRÀM"))), 1, 0);
            check("Bản vẽ: đường MNN thấp nhất", B(all.Contains(DxfWriter.Encode(CuTramDrawing.WaterLabel))), 1, 0);
            check("Bản vẽ: ghi chú mật độ 25 cây/m²", B(all.Contains(DxfWriter.Encode("25 cây/m²"))), 1, 0);
            string dir = Path.Combine(string.IsNullOrEmpty(outDir) ? Path.GetTempPath() : outDir, "LuiCocCuTram");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "ct_cu_tram.dxf"), ct.Count > 0 ? ct[1 % ct.Count].W.ToDxfString() : "");
            CalculationReport.Export(p, r, s, Path.Combine(dir, "tm_cu_tram.docx"));
            check("Thuyết minh có chương cừ tràm (xuất được)", B(File.Exists(Path.Combine(dir, "tm_cu_tram.docx"))), 1, 0);
            // từng chân cột tắt cừ tràm
            int node = fs[0].NodeId;
            CuTramCalc.Set(p.Foundation, new[] { node }, false);
            var s2 = ConcreteDesigner.Design(p, r);
            check("Chân cột tắt cừ tràm: móng tại nút không có cừ tràm", B(s2.Footings.First(f => f.NodeId == node || f.RebarKeys.Contains("M:" + node)).CuTram == null), 1, 0);
            CuTramCalc.Set(p.Foundation, new[] { node }, null);
            check("Xoá ghi đè chân cột", p.Foundation.CuTram.Overrides.Count, 0, 0);
            // móng băng trên cừ tràm
            p.Foundation.Type = FoundationType.Strip;
            p.Foundation.StripDirection = 2;
            var s3 = ConcreteDesigner.Design(p, r);
            check("Móng băng trên cừ tràm: mọi dải có kết quả cừ tràm", s3.StripFootings.Count(x => x.CuTram != null), s3.StripFootings.Count, 0);
            check("Móng băng trên cừ tràm: R = [p]", B(s3.StripFootings.All(x => x.CuTram == null || Math.Abs(x.R - x.CuTram.Allowable) < 1e-6)), 1, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("   · lỗi cừ tràm: " + ex);
            check("Thiết kế móng trên nền cừ tràm – chạy không lỗi", 0, 1, 0);
        }
    }

    private static void Json(Action<string, double, double, double> check)
    {
        var fs = new FoundationSettings { PileSetbackEnabled = true, PileSetback = 0.6, PileSetbackToFace = true, PilePressMethod = PilePressMethod.Robot, CapToBoundary = false };
        fs.Boundaries.Add(new PropertyBoundary { Side = BoundarySide.Right, Coord = 5, PileSetback = 0 });
        fs.CuTram.Enabled = true; fs.CuTram.Length = 4.5; fs.CuTram.Density = 16; fs.CuTram.MinWaterDepth = 1.2; fs.CuTram.Method = CuTramMethod.EquivalentSoil;
        fs.CuTram.Overrides.Add(new CuTramOverride { NodeId = 7, Enabled = false });
        string json = JsonSerializer.Serialize(fs, Project.JsonOptions);
        var b = JsonSerializer.Deserialize<FoundationSettings>(json, Project.JsonOptions);
        check("JSON: khoảng lùi ép cọc", b.PileSetback, 0.6, 1e-12);
        check("JSON: bật lùi cọc + đo tới mép + mép đài theo cọc", B(b.PileSetbackEnabled && b.PileSetbackToFace && !b.CapToBoundary), 1, 0);
        check("JSON: phương pháp ép robot (enum chữ)", B(b.PilePressMethod == PilePressMethod.Robot && json.Contains("\"Robot\"")), 1, 0);
        check("JSON: khoảng lùi riêng cạnh ranh giới = 0", b.Boundaries[0].PileSetback, 0, 1e-12);
        check("JSON: Clone ranh giới giữ khoảng lùi", b.Boundaries[0].Clone().PileSetback, 0, 1e-12);
        check("JSON: cừ tràm dài 4,5 m, 16 cây/m²", b.CuTram.Length + b.CuTram.Density, 20.5, 1e-12);
        check("JSON: MNN thấp nhất + phương pháp (2)", B(Math.Abs(b.CuTram.MinWaterDepth - 1.2) < 1e-12 && b.CuTram.Method == CuTramMethod.EquivalentSoil), 1, 0);
        check("JSON: ghi đè chân cột", B(b.CuTram.Overrides.Count == 1 && !b.CuTram.Overrides[0].Enabled), 1, 0);
        var old = JsonSerializer.Deserialize<FoundationSettings>("{\"Type\":\"Isolated\",\"Boundaries\":[{\"Side\":\"Left\",\"Coord\":0}]}", Project.JsonOptions);
        check("JSON cũ: không bật lùi cọc, khoảng lùi mặc định 0,5 m, cạnh theo chung (−1)", B(!old.PileSetbackEnabled && Math.Abs(old.PileSetback - 0.5) < 1e-12 && old.Boundaries[0].PileSetback < 0), 1, 0);
        check("JSON cũ: cừ tràm tắt, mặc định 4 m / 25 cây/m²", B(old.CuTram != null && !old.CuTram.Enabled && old.CuTram.Length == 4 && old.CuTram.Density == 25), 1, 0);
    }

    private static void Advisor(Action<string, double, double, double> check)
    {
        var p = ModelBuilder.NewProject("Gợi ý cừ tràm");
        ModelBuilder.SetGrid(p, new[] { 3.6, 3.6 }, new[] { 4.0, 4.0 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, 0.0);
        p.Soil.Depth = 1.0;
        var bh = TwoLayer();
        var adv = FoundationAdvisor.Recommend(p, null, bh);
        var o = adv.Options.FirstOrDefault(x => x.Choice == FoundationChoice.CuTramFooting);
        foreach (var x in adv.Options) Console.WriteLine($"   · {x.Name}: {(x.Feasible ? "khả thi" : "không")}{(x.Recommended ? " ← ĐỀ XUẤT" : "")} – {x.Reason}");
        check("Gợi ý: có phương án móng nông trên nền cừ tràm", B(o != null), 1, 0);
        check("Gợi ý: 4 phương án cũ giữ nguyên thứ tự", B(adv.Options.Count >= 5 && adv.Options[0].Choice == FoundationChoice.Isolated && adv.Options[3].Choice == FoundationChoice.BoredPile), 1, 0);
        if (o != null)
        {
            check("Gợi ý: nền nông yếu + MNN cao → không bị loại vì đất / nước", B(!o.Reason.Contains("không yếu") && !o.Reason.Contains("sẽ mục")), 1, 0);
            check("Gợi ý: có [p] và kích thước móng", B(double.IsFinite(o.R) && o.R > 0 && double.IsFinite(o.Size)), 1, 0);
            var p2 = p.Clone();
            o.ApplyTo(p2);
            check("Áp dụng phương án cừ tràm: móng đơn + bật cừ tràm", B(p2.Foundation.Type == FoundationType.Isolated && p2.Foundation.CuTram.Enabled), 1, 0);
        }
        var deep = TwoLayer();
        deep.WaterDepth = 6;
        var adv2 = FoundationAdvisor.Recommend(p, null, deep);
        var o2 = adv2.Options.First(x => x.Choice == FoundationChoice.CuTramFooting);
        check("Gợi ý: MNN sâu → cừ tràm không khả thi (sẽ mục)", B(!o2.Feasible && o2.Reason.Contains("sẽ mục")), 1, 0);
        // lùi cọc trong gợi ý phương án cọc
        p.Foundation.Boundaries.Add(new PropertyBoundary { Side = BoundarySide.Left, Coord = -0.11 });
        var adv3 = FoundationAdvisor.Recommend(p, null, bh);
        check("Gợi ý cọc: ghi chú lùi cọc cột sát ranh + dầm cân bằng", B(adv3.Options.Where(x => x.Type == FoundationType.Pile).All(x => x.Details.Any(dl => dl.Contains("lùi") && dl.Contains("L − e")))), 1, 0);
    }
}
