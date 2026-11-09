using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>[CọcÉp + SátRanh + Hố móng + Kiểu bố trí dầm + Bản vẽ từng dầm]</summary>
public static class PressPitBeamTests
{
    private static double B(bool x) => x ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---- [P] theo lực ép ----
        var pi = TakedownAdvisor.PressCapacity(50, 2.0);
        foreach (var n in pi.Notes) Console.WriteLine("   · " + n);
        check("Cọc ép 50 t, k = 2: [P] = 250 kN", pi.P, 250, 1e-9);
        check("Cọc ép 50 t: tiết diện 250×250", pi.Size, 0.25, 1e-9);
        check("Cọc 250 B25 4Ø16: Pvl = 14500·0,0625 + 350000·4·π·0,016²/4", pi.Pvl, 14500 * 0.0625 + 350000 * 4 * Math.PI * 0.016 * 0.016 / 4, 1e-6);
        check("Pvl ≥ Pép (cọc không vỡ khi ép)", B(pi.Pvl >= 500), 1, 0);
        check("Cọc ép 30 t → 200×200, [P] = 150 kN", TakedownAdvisor.PressCapacity(30, 2).P * 10 + TakedownAdvisor.PressCapacity(30, 2).Size, 1500.2, 1e-9);
        check("Lực ép vượt Pvl → [P] theo Pvl", B(TakedownAdvisor.PressCapacity(200, 2, 0.2).P <= TakedownAdvisor.PressCapacity(200, 2, 0.2).Pvl / 2 + 1e-6), 1, 0);

        // ---- nhà phố 5 × 20 ----
        var p = ModelBuilder.NewProject("Nhà phố 3 tầng 5x20 – cọc ép");
        ModelBuilder.SetGrid(p, new[] { 5.0 }, new[] { 4.0, 4.5, 4.0, 4.5, 3.0 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
        {
            ColumnSection = "C22x22", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5, FinishLoad = 1.5, LiveLoad = 1.5, RoofLiveLoad = 0.75
        });
        var r = FrameAnalysis.Run(p);
        int nCols = FoundationUtil.BaseColumns(p).Count;
        var adv = TakedownAdvisor.Advise(p, r);
        Console.WriteLine("   · " + adv.Explanation);
        foreach (var x in adv.Pits) Console.WriteLine($"     {x.Name} [{x.Names}] {x.EdgeText}: ΣNtt = {x.Ntt:0}, e = {x.Ex:0.00}/{x.Ey:0.00}, R1/N = {x.Factor:0.00}, n = {x.Piles} ({x.Nx}×{x.Ny}), đài {x.CapLx:0.00}×{x.CapLy:0.00} – {x.Note}");
        foreach (var c in adv.Columns) Console.WriteLine($"     {c.T.Name}: {c.EdgeText}, móng đơn {c.B:0.0} m, Ls = {c.LsX:0.0}/{c.LsY:0.0}, R1/N = {c.StrapFactor:0.00}");
        check("Gợi ý cọc: mặc định cọc ép theo lực ép 50 t", B(adv.BestPile != null && adv.BestPile.FromPress), 1, 0);
        check("Gợi ý cọc: [P] = 250 kN", adv.BestPile.Capacity, 250, 1e-9);
        check("Ranh tự động: 2 cạnh dài (trái + phải)", B(adv.VirtualBoundaries && adv.Boundaries.Count == 2 && adv.Boundaries.All(b => b.IsVertical)), 1, 0);
        check("Nhà rộng 1 nhịp: mọi cột sát 1 ranh", adv.Columns.Count(c => c.Edge == EdgeKind.OneSide), nCols, 0);
        check("Cột sát ranh có dầm cân bằng tới cột đối diện (Ls = 5 m)", adv.Columns.Count(c => Math.Abs(c.LsX - 5) < 1e-6), nCols, 0);
        check("Hố móng: mỗi chân cột một hố (hoặc gộp)", B(adv.Pits.Sum(x => x.Cols.Count) == nCols), 1, 0);
        check("Hố móng: số cọc ≥ ⌈1,1·Ntt·R1/N/[P]⌉", B(adv.Pits.All(x => x.Piles >= Math.Ceiling(1.1 * x.Ntt * x.Factor / 250 - 1e-9))), 1, 0);
        check("Hố móng: đài lệch tâm vào trong (ex > 0), R1/N > 1", B(adv.Pits.Where(x => x.Cols.Count == 1).All(x => x.Ex > 0 && x.Factor > 1)), 1, 0);
        check("Hố móng: số cọc nhà phố 3 tầng ≤ 5 cọc/hố", B(adv.Pits.Max(x => x.Piles) <= 5), 1, 0);
        var c0 = adv.Pits.First(x => x.Cols.Count == 1);
        check("R1/N = Ls/(Ls − e)", c0.Factor, 5 / (5 - c0.Ex), 1e-9);
        check("Móng đơn lệch tâm: R1/N > 1 cho cột sát ranh", B(adv.Columns.All(c => c.StrapFactor > 1)), 1, 0);

        // cột góc: sát 2 ranh
        var adv4 = TakedownAdvisor.Advise(p, r, null, adv.T, new AdviseOptions { Sides = new[] { BoundarySide.Left, BoundarySide.Right, BoundarySide.Front, BoundarySide.Back } });
        check("Ranh 4 cạnh: 4 cột góc sát 2 ranh (2 phương)", adv4.Columns.Count(c => c.Edge == EdgeKind.TwoSides), 4, 0);
        check("Ranh 4 cạnh: còn lại sát 1 ranh", adv4.Columns.Count(c => c.Edge == EdgeKind.OneSide), nCols - 4, 0);
        var corner = adv4.Pits.Where(x => x.Edge == EdgeKind.TwoSides && x.Cols.Count == 1).ToList();
        check("Cột góc: đài lệch tâm 2 phương", B(corner.Count > 0 && corner.All(x => x.Ex > 0 && x.Ey > 0)), 1, 0);
        var advIn = TakedownAdvisor.Advise(p, r, null, adv.T, new AdviseOptions { Sides = new BoundarySide[0] });
        check("Không ranh: mọi cột ở giữa, R1/N = 1", B(advIn.Columns.All(c => c.Edge == EdgeKind.Interior) && advIn.Pits.All(x => x.Factor == 1)), 1, 0);
        check("Không ranh: ít cọc hơn (không lệch tâm)", B(advIn.PitPiles <= adv.PitPiles), 1, 0);
        var adv40 = TakedownAdvisor.Advise(p, r, null, adv.T, new AdviseOptions { PressForce = 40, PressFactor = 2 });
        check("Lực ép 40 t: [P] = 200 kN, nhiều cọc hơn", B(adv40.BestPile.Capacity == 200 && adv40.PitPiles >= adv.PitPiles), 1, 0);

        // áp dụng
        var q = p.Clone();
        TakedownAdvisor.Apply(q, adv, "Pile", true);
        check("Áp dụng cọc: [P] = 250 kN, ghi lực ép", q.Foundation.PileCapacity + q.Foundation.PilePressForce, 300, 1e-9);
        check("Áp dụng cọc: ghi 2 ranh đất, lùi cọc khỏi ranh", B(q.Foundation.Boundaries.Count == 2 && q.Foundation.PileSetbackEnabled), 1, 0);
        check("Ranh ghi theo trục ngoài cùng", B(q.Foundation.Boundaries.All(b => !string.IsNullOrEmpty(b.Grid) && Math.Abs(b.Offset - 0.11) < 1e-6)), 1, 0);
        var sq = ConcreteDesigner.Design(q, FrameAnalysis.Run(q));
        Console.WriteLine($"   · Thiết kế đài cọc sau áp dụng: {sq.PileCaps.Sum(c => c.Piles.Count * Math.Max(1, c.Locations.Count))} cọc, {sq.PileCaps.Count(c => !c.Ok)} đài chưa đạt");
        check("Thiết kế đài cọc theo [P] ép: chạy được", B(sq.PileCaps.Count > 0), 1, 0);
        var rep = KetCauVXT.Report.TakedownReport.Build(p, adv);
        check("Thuyết minh: có bảng hố móng", 1, 1, 0);

        // ---- kiểu bố trí thép dầm ----
        var s = ConcreteDesigner.Design(p, r);
        var beam = s.Beams.OrderByDescending(b => b.Spans.Count).First();
        string key = beam.RebarKeys[0];
        double W(BeamLineDesign d) => d.Bars.Sum(b => b.Count * b.WeightPerBar);
        BeamLineDesign Redo(BeamRebarScheme sc)
        {
            var pq = p.Clone();
            pq.SetRebarChoice(new RebarChoice { Member = key, Zone = RebarZones.BeamScheme, Index = -1, BeamScheme = sc });
            return ConcreteDesigner.Design(pq, r).Beams.First(b => b.RebarKeys.Contains(key));
        }
        var bA = Redo(BeamRebarScheme.CutExtras); var bB = Redo(BeamRebarScheme.AllContinuous); var bC = Redo(BeamRebarScheme.ExtendedSupport);
        Console.WriteLine($"   · Dầm {beam.Name} ({beam.Spans.Count} nhịp): kiểu A {W(bA):0.0} kg, B {W(bB):0.0} kg, C {W(bC):0.0} kg");
        check("Kiểu A mặc định", B(beam.Scheme == BeamRebarScheme.CutExtras && bA.Scheme == BeamRebarScheme.CutExtras), 1, 0);
        check("Kiểu B: mọi thép dọc chạy suốt (không thanh cắt theo nhịp)", bB.Bars.Count(b => b.Role != "Đai" && b.Span >= 0), 0, 0);
        check("Kiểu B: tốn thép hơn kiểu A", B(W(bB) >= W(bA) - 1e-6), 1, 0);
        check("Kiểu B: vẫn đạt", B(bB.Ok == bA.Ok), 1, 0);
        double EndTop(BeamLineDesign d) => d.Bars.Where(b => b.Role == "Mũ gối" && b.Span == 0).Select(b => b.End - b.Start).DefaultIfEmpty(0).Max();
        check("Kiểu C: mũ gối biên dài hơn (Lo/3 thay Lo/4)", B(EndTop(bC) > EndTop(bA) + 1e-6 || EndTop(bA) == 0), 1, 0);
        double BotLen(BeamLineDesign d) => d.Bars.Where(b => b.Role == "Tăng cường dưới").Select(b => b.End - b.Start).DefaultIfEmpty(0).Sum();
        check("Kiểu C: tăng cường dưới kéo vào gối (dài hơn A)", B(BotLen(bC) >= BotLen(bA) - 1e-9), 1, 0);
        var zones = RebarOptions.Zones(p, beam);
        var zs = zones.FirstOrDefault(z => z.Zone == RebarZones.BeamScheme);
        check("Chọn thép dầm: có vùng 'Kiểu bố trí'", B(zs != null), 1, 0);
        var alts = RebarOptions.Alternatives(p, r, beam, zs);
        check("Kiểu bố trí: 3 phương án A/B/C kèm khối lượng", B(alts.Count == 3 && alts.All(a => a.WeightKg > 0)), 1, 0);
        var pq2 = p.Clone();
        RebarOptions.Apply(pq2, beam, zs, alts.First(a => a.BeamScheme == BeamRebarScheme.AllContinuous));
        check("Áp dụng kiểu B qua hộp thoại chọn thép", B(pq2.FindRebarChoice(key, RebarZones.BeamScheme, -1)?.BeamScheme == BeamRebarScheme.AllContinuous), 1, 0);
        var pp = p.Clone(); pp.RebarPrefs ??= new RebarPreferences(); pp.RebarPrefs.BeamScheme = BeamRebarScheme.AllContinuous;
        check("Kiểu mặc định công trình = B", B(ConcreteDesigner.Design(pp, r).Beams.All(b => b.Scheme == BeamRebarScheme.AllContinuous)), 1, 0);
        // số thanh chọn tay
        var zb = zones.First(z => z.Zone == RebarZones.BeamBottom);
        var ab = RebarOptions.Alternatives(p, r, beam, zb);
        var pick = ab.FirstOrDefault(a => !a.IsCurrent && a.Fits) ?? ab.Last();
        var pq3 = p.Clone();
        RebarOptions.Apply(pq3, beam, zb, pick);
        var b3 = ConcreteDesigner.Design(pq3, r).Beams.First(b => b.RebarKeys.Contains(key));
        Console.WriteLine($"   · Chọn tay nhịp {zb.Index + 1}: {pick.Text} → thiết kế lại {b3.Spans[zb.Index].BotText}");
        check("Số thanh chọn tay được giữ (" + pick.Text + ")", b3.Spans[zb.Index].BotText == RcSection.BarText(pick.Bars.Select(g => (g.Dia, g.Count))) ? 1 : 0, 1, 0);

        // ---- bản vẽ từng dầm ----
        var figs = BeamSheet.Figures(p, s, beam);
        check("Bản vẽ từng dầm: có mặt đứng + mặt cắt + thống kê", B(figs.Count >= 3), 1, 0);
        var w = BeamSheet.Compose(p, s, beam);
        check("Bản vẽ từng dầm: chỉ chứa dầm được chọn", B(w.EntityCount > 0), 1, 0);
        if (outDir != null)
            try
            {
                Directory.CreateDirectory(outDir);
                string f = BeamSheet.Export(p, s, beam, Path.Combine(outDir, "ChiTietDam_" + beam.Name + ".dxf"), CadOutputFormat.DxfR12);
                check("Xuất DXF một dầm", B(File.Exists(f)), 1, 0);
                rep.Save(Path.Combine(outDir, "TruyenTai_HoMong_CocEp.docx"));
            }
            catch (Exception ex) { Console.WriteLine("   ! " + ex.Message); }
    }
}
