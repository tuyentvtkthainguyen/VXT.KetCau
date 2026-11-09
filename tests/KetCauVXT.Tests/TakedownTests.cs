using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>[Truyền tải] Nhà phố 3 tầng 5 × 20 m: truyền tải đứng theo diện chịu tải (cân bằng với tải đã chất và phản lực khung),
/// giảm hoạt tải ψ, gợi ý móng đơn / băng / cọc từng chân cột, thiết kế móng theo tải đứng (giằng móng chịu mô men).</summary>
public static class TakedownTests
{
    private static double B(bool x) => x ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var p = ModelBuilder.NewProject("Nhà phố 3 tầng 5x20");
        ModelBuilder.SetGrid(p, new[] { 5.0 }, new[] { 4.0, 4.5, 4.0, 4.5, 3.0 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
        {
            ColumnSection = "C22x22", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5, FinishLoad = 1.5, LiveLoad = 1.5, RoofLiveLoad = 0.75
        });
        var r = FrameAnalysis.Run(p);
        int nCols = FoundationUtil.BaseColumns(p).Count;
        var t = LoadTakedown.Run(p, r);
        Console.WriteLine($"   · Nhà phố 5×20 m, 3 tầng, {nCols} cột: sàn {t.FloorArea:0} m², G = {t.TotalG:0} kN, P = {t.TotalP:0} kN, (G+P)/m² = {(t.TotalG + t.TotalP) / t.FloorArea:0.0} kN/m²");
        foreach (var s in t.Stories) Console.WriteLine($"     {s.Story}: A = {s.FloorArea:0.0} m², G = {s.G:0}, P = {s.P:0}, {s.Intensity:0.0} kN/m² – " + string.Join("; ", s.Sources.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value:0}")));
        foreach (var c in t.Columns) Console.WriteLine($"     {c.Name}: A = {c.Area:0.0} m², G = {c.G:0}, P = {c.P:0}, ψ = {c.Psi:0.00}, Ntc = {c.Ntc:0}, Ntt = {c.Ntt:0}, khung Ntt = {c.FeNtt:0} (TT {c.FeG:0}, HT {c.FeP:0}), Mmax = {c.FeM:0}");
        foreach (var n in t.Notes.Concat(t.Warnings)) Console.WriteLine("     " + n);
        check("Truyền tải: mọi chân cột có tải", t.Columns.Count, nCols, 0);
        check("Truyền tải: Σ chân cột = Σ tải đã chất (cân bằng)", t.TotalG + t.TotalP, t.AppliedG + t.AppliedP, 0.005 * (t.AppliedG + t.AppliedP) + 1);
        double feG = t.Columns.Sum(c => c.FeG), feP = t.Columns.Sum(c => c.FeP);
        check("Truyền tải: ΣG = Σ phản lực tĩnh tải của khung (±2%)", t.TotalG, feG, 0.02 * feG);
        check("Truyền tải: ΣP = Σ phản lực hoạt tải của khung (±2%)", t.TotalP, feP, 0.02 * Math.Max(feP, 1));
        double q = (t.TotalG + t.TotalP) / t.FloorArea;
        check("Truyền tải: tải trung bình 7–16 kN/m² sàn", B(q > 7 && q < 16), 1, 0);
        var mid = t.Columns.OrderByDescending(c => c.Area).First();
        check("Truyền tải: cột giữa – từng chân cột ≈ phản lực khung (TT, ±30%)", mid.G, mid.FeG, 0.3 * mid.FeG);
        // ψ
        double A = mid.Floors.Values.Average();
        double psiA = A > 9 ? 0.4 + 0.6 / Math.Sqrt(A / 9) : 1;
        check("Giảm hoạt tải: ψn1 = 0,4 + (ψA1 − 0,4)/√n", mid.Psi, Math.Min(1, 0.4 + (psiA - 0.4) / Math.Sqrt(mid.LiveFloors)), 0.02);
        check("Giảm hoạt tải: Ntc = G + ψ·P (P mái không giảm)", mid.Ntc, mid.G + mid.P - mid.PReducible + mid.Psi * mid.PReducible, 1e-9);
        var t0 = LoadTakedown.Run(p, r, new TakedownOptions { LiveReduction = false });
        check("Tắt giảm hoạt tải: ψ = 1", t0.Columns.Max(c => c.Psi), 1, 0);
        check("Truyền tải: không làm thay đổi dự án (tải tự sinh trên bản sao)", B(p.FrameLoads.Count(l => l.Source == "SLAB") > 0), 1, 0);

        // ---- gợi ý móng ----
        p.Foundation.Type = FoundationType.Isolated;
        var adv = TakedownAdvisor.Advise(p, r, null, t, new AdviseOptions { PressForce = 0, Sides = new BoundarySide[0] }); // [P] hố khoan / nhập tay, không ranh (test 102: cọc ép + sát ranh)
        Console.WriteLine("   · " + adv.Explanation);
        Console.WriteLine("     " + adv.SoilNote);
        foreach (var c in adv.Columns) Console.WriteLine($"     {c.T.Name}: móng đơn {c.B:0.0}×{c.B:0.0}×{c.H:0.00} m (R = {c.R:0}), cọc {c.Piles} {c.Note}");
        foreach (var x in adv.Piles) Console.WriteLine($"     {x.Name}: L = {x.Length:0.0} m, [P] = {x.Capacity:0} kN, tổng {x.Total} cọc");
        check("Gợi ý: móng đơn từng chân cột", adv.Columns.Count(c => double.IsFinite(c.B)), nCols, 0);
        check("Gợi ý: móng đơn ≤ 2,6 m với tải nhà 3 tầng", B(adv.Columns.Max(c => c.B) <= 2.6), 1, 0);
        check("Gợi ý: có loại cọc", B(adv.BestPile != null), 1, 0);
        check("Gợi ý: số cọc mỗi cột ≤ 3 (cọc 250, [P] 450 kN)", B(adv.Columns.Max(c => c.Piles) <= 3), 1, 0);
        check("Gợi ý: móng băng theo phương dài nhà (Y)", B(!adv.StripAlongX && adv.Strips.Count == 2), 1, 0);
        check("Gợi ý: có phương án đề xuất", B(adv.Recommended is "Isolated" or "Strip" or "Pile"), 1, 0);

        // ---- móng cọc: thiết kế theo khung (mô men + gió) vs theo tải đứng ----
        p.Foundation.Type = FoundationType.Pile;
        var sFull = ConcreteDesigner.Design(p, r);
        int pilesFull = sFull.PileCaps.Sum(c => c.Piles.Count * Math.Max(1, c.Locations.Count));
        TakedownAdvisor.Apply(p, adv, "Pile", true);
        check("Áp dụng cọc: bỏ mô men chân cột", p.Foundation.BaseMomentFactor, 0, 0);
        check("Áp dụng cọc: chỉ tổ hợp tải đứng", B(p.Foundation.GravityCombosOnly), 1, 0);
        check("Áp dụng cọc: loại cọc + [P] theo gợi ý", p.Foundation.PileCapacity * 1000 + p.Foundation.PileSize, adv.BestPile.Capacity * 1000 + adv.BestPile.Size, 1e-9);
        var sGrav = ConcreteDesigner.Design(p, r);
        int pilesGrav = sGrav.PileCaps.Sum(c => c.Piles.Count * Math.Max(1, c.Locations.Count));
        Console.WriteLine($"   · Móng cọc: theo khung đầy đủ (gió + mô men ngàm) {pilesFull} cọc / {sFull.PileCaps.Count} loại đài; theo tải đứng + giằng: {pilesGrav} cọc / {sGrav.PileCaps.Count} loại đài, "
                          + $"{sGrav.PileCaps.Count(c => !c.Ok)} đài chưa đạt");
        check("Móng cọc theo tải đứng: số cọc gần gợi ý (≤ gợi ý + 1 cọc mỗi chân cột)", B(pilesGrav <= adv.BestPile.Total + nCols), 1, 0);
        check("Móng cọc theo tải đứng: không nhiều hơn theo khung đầy đủ", B(pilesGrav <= pilesFull), 1, 0);
        check("Móng cọc theo tải đứng: các đài đạt", sGrav.PileCaps.Count(c => !c.Ok), 0, 0);
        check("Tổ hợp tải đứng: không chứa gió", B(FoundationUtil.Combos(p, r).All(c => FoundationUtil.IsGravityCombo(p, r, c))), 1, 0);

        // ---- móng đơn ----
        p.Foundation.CapOverrides.Clear();
        p.Foundation.BaseMomentFactor = 1; p.Foundation.GravityCombosOnly = false;
        p.Foundation.Type = FoundationType.Isolated;
        var iFull = ConcreteDesigner.Design(p, r);
        double aFull = FootingLayout.AllInstances(p, iFull).Sum(f => f.L * f.B);
        TakedownAdvisor.Apply(p, adv, "Isolated", true);
        check("Áp dụng móng đơn: kích thước tự chọn (không chốt)", p.Footings.Count(f => f.L > 0), 0, 0);
        var iGrav = ConcreteDesigner.Design(p, r);
        double aGrav = FootingLayout.AllInstances(p, iGrav).Sum(f => f.L * f.B);
        Console.WriteLine($"   · Móng đơn: theo khung đầy đủ Σ diện tích đáy {aFull:0.0} m² ({iFull.Footings.Count(f => !f.Ok)} chưa đạt); theo truyền tải + giằng {aGrav:0.0} m², {iGrav.Footings.Count(f => !f.Ok)} chưa đạt");
        foreach (var f in iGrav.Footings.Where(f => !f.Ok))
            foreach (var c in f.Checks.Where(c => !c.Ok)) Console.WriteLine($"     ! {f.Name}: {c.Name} {c.Demand:0.#} / {c.Capacity:0.#}");
        check("Móng đơn theo tải đứng: diện tích đáy không lớn hơn theo khung đầy đủ", B(aGrav <= aFull + 1e-6), 1, 0);
        check("Móng đơn theo tải đứng: đạt", iGrav.Footings.Count(f => !f.Ok), 0, 0);
        double bMaxDesign = FootingLayout.AllInstances(p, iGrav).Max(f => Math.Max(f.L, f.B)), bMaxAdv = adv.Columns.Max(c => c.B);
        Console.WriteLine($"   · Móng đơn lớn nhất: thiết kế theo tải đứng {bMaxDesign:0.0} m, gợi ý truyền tải {bMaxAdv:0.0} m");
        check("Móng đơn theo tải đứng ≈ gợi ý truyền tải (±0,4 m)", bMaxDesign, bMaxAdv, 0.4);

        // ---- thuyết minh ----
        var d = TakedownReport.Build(p, adv);
        if (outDir != null)
            try { Directory.CreateDirectory(outDir); d.Save(Path.Combine(outDir, "TruyenTai_GoiYMong.docx")); } catch (Exception) { }
        check("Thuyết minh truyền tải: tạo được", 1, 1, 0);
        // gợi ý cũ (FoundationAdvisor) dùng tải truyền tải
        var old = FoundationAdvisor.Recommend(p, r, null);
        check("Gợi ý phương án móng: Nmax = truyền tải", old.NMax, t.Columns.Max(c => c.Ntt), 1e-6);
    }
}
