using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng thép trên sàn: mũ cạnh chung tính một lần, phương án lưới 2 lớp (lưới liên tục, nối chồng khi dài hơn cây),
/// lưới 2 lớp + mũ tăng cường, so sánh khối lượng các phương án và câu cảnh báo vượt / tiết kiệm / dư / thiếu thép.
/// check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class SlabSchemeTests
{
    private static double B(bool v) => v ? 1 : 0;
    private static double R10(double m) => Math.Round(m * 100.0, MidpointRounding.AwayFromZero) / 100.0;

    /// <summary>Sàn 1 tầng trên lưới trục: nhịp X spansX, nhịp Y spansY, dầm D22x40 theo Y, D22x30 theo X.</summary>
    private static Project Floor(double[] spansX, double[] spansY)
    {
        var p = ModelBuilder.NewProject("Kiểm thử thép sàn");
        ModelBuilder.SetGrid(p, spansX, spansY);
        ModelBuilder.SetStories(p, new[] { 3.6 }, 0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
        {
            ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 0
        });
        if (p.Combinations.Count == 0) ComboGenerator.Generate(p);
        return p;
    }

    private static IEnumerable<BarMark> TopBars(IEnumerable<SlabDesign> ds, string rolePart) =>
        ds.SelectMany(d => d.Bars.Where(b => (b.Role ?? "").Contains(rolePart)).Select(b => b));

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Console.WriteLine("SS. Thép trên sàn: mũ cạnh chung, lưới 2 lớp, so sánh khối lượng");

        // ---------------- câu cảnh báo ----------------
        {
            var (t1, l1) = RebarOptions.Compare(120, 100, 150, 1.45);
            check("Cảnh báo: dư thép + vượt + tiết kiệm (chuỗi)",
                B(t1 == "Dư thép (As/As,yc = 1.45); Vượt +20.0 kg (+20%) so với phương án tiết kiệm nhất; Tiết kiệm −30.0 kg (−20%) so với phương án hiện tại"), 1, 0);
            check("Cảnh báo: mức cam khi dư / vượt", l1, 1, 0);
            var (t2, l2) = RebarOptions.Compare(100, 100, 100, 0.9);
            check("Cảnh báo: thiếu thép (chuỗi, mức đỏ)", B(t2 == "Thiếu thép (As/As,yc = 0.90)" && l2 == 2), 1, 0);
            var (t3, l3) = RebarOptions.Compare(80, 80, 100, 1.1);
            check("Cảnh báo: tiết kiệm so với hiện tại (mức xanh)", B(t3 == "Tiết kiệm −20.0 kg (−20%) so với phương án hiện tại" && l3 == 3), 1, 0);
            var (t4, l4) = RebarOptions.Compare(100, 100, 100, 1.2);
            check("Cảnh báo: phương án tiết kiệm nhất, As vừa đủ → không cảnh báo", B(t4 == "" && l4 == 0), 1, 0);
        }

        // ---------------- 2 ô 4×4 m chung một cạnh ----------------
        {
            var p = Floor(new[] { 4.0, 4.0 }, new[] { 4.0 });
            var w = new List<string>();
            var ds = SlabDesigner.Design(p, null, w);
            check("2 ô chung cạnh: thiết kế 2 ô", ds.Sum(d => d.Quantity), 2, 0);
            var shared = TopBars(ds, "mũ gối chung").ToList();
            check("2 ô chung cạnh: MỘT thanh mũ cạnh chung (không tính trùng)", shared.Count, 1, 0);
            check("2 ô chung cạnh: không có mũ ở cạnh biên khớp", TopBars(ds, "mũ gối biên").Count(), 0, 0);
            if (shared.Count == 1)
            {
                var bm = shared[0];
                double t = p.SlabSections.First().Thickness, c = p.Concrete.CoverSlab;
                double bw = p.FrameSections.First(x => x.Name == "D22x40").B;
                double hook = R10(Math.Max(t - 2 * c, 0.05));
                double L = R10(R10(4.0 / 4 + bw + 4.0 / 4) + 2 * hook);
                int n = (int)Math.Ceiling(4.0 / bm.Spacing - 1e-9) + 1;
                double wHand = 0.00617 * bm.Dia * bm.Dia * L * n;
                check("Mũ cạnh chung: chiều dài = lx1/4 + bdầm + lx2/4 + 2 móc", bm.Length, L, 1e-9);
                check("Mũ cạnh chung: số thanh = 4000/s + 1", bm.Count, n, 0);
                var rows = RebarSchedule.BuildRows(new DesignSummary { Slabs = ds });
                double wSched = rows.Where(x => (x.Role ?? "").Contains("Lớp trên")).Sum(x => x.WeightKg);
                check("Mũ cạnh chung: khối lượng thép trên trong bảng thống kê = tính tay", wSched, wHand, 1e-3);
                check("Mũ cạnh chung: hai ô đều kiểm tra uốn gối đạt", B(ds.All(d => d.Checks.Where(k => k.Name.StartsWith("Uốn gối")).All(k => k.Ok))
                                                                      && ds.All(d => d.Checks.Any(k => k.Name.StartsWith("Uốn gối")))), 1, 0);
                check("Mũ cạnh chung: một hình vẽ trên mặt bằng", ds.Sum(d => d.EdgeBars.Count), 1, 0);
                check("Mũ cạnh chung: ô không thống kê có ghi chú", B(ds.Any(d => d.Messages.Any(m => m.Contains("thống kê ở ô")))), 1, 0);
                var shop = RebarShop.Build(p, new DesignSummary { Slabs = ds });
                check("Gia công: thanh mũ cạnh chung xuất hiện một lần", shop.Bars.Count(x => x.Role.Contains("mũ gối chung")), 1, 0);
                try
                {
                    var dw = SlabDrawing.Build(p, new DesignSummary { Slabs = ds }, p.Stories[0], 50);
                    check("Mặt bằng thép sàn vẽ được", B(dw.EntityCount > 0), 1, 0);
                }
                catch (Exception ex) { Console.WriteLine("   · lỗi vẽ sàn: " + ex.Message); check("Mặt bằng thép sàn vẽ được", 0, 1, 0); }
            }

            // so sánh phương án (cả tầng)
            var s0 = ds.First();
            var zs = RebarOptions.Zones(p, s0).FirstOrDefault(z => z.Zone == RebarZones.SlabSchemeStory);
            check("Vùng 'phương án bố trí cả tầng' có trong hộp thoại", B(zs != null), 1, 0);
            if (zs != null)
            {
                var alts = RebarOptions.Alternatives(p, null, s0, zs);
                check("So sánh phương án sàn: 3 phương án", alts.Count, 3, 0);
                double wmin = alts.Any(a => a.Fits) ? alts.Where(a => a.Fits).Min(a => a.WeightKg) : alts.Min(a => a.WeightKg);
                check("So sánh: có khối lượng và kg/m²", B(alts.All(a => a.WeightKg > 0 && a.WeightPerM2 > 0)), 1, 0);
                check("So sánh: chênh so với tiết kiệm nhất = KL − KL min", B(alts.All(a => Math.Abs(a.DeltaVsBestKg - Math.Round(a.WeightKg - wmin, 2)) < 0.011)), 1, 0);
                var cur = alts.First(a => a.IsCurrent);
                check("So sánh: phương án hiện tại = mũ + lưới dưới", B(cur.Scheme == SlabRebarScheme.TopBarsBottomMesh), 1, 0);
                check("So sánh: chênh so với hiện tại = KL − KL hiện tại", B(alts.All(a => Math.Abs(a.DeltaVsCurrentKg - Math.Round(a.WeightKg - cur.WeightKg, 2)) < 0.011)), 1, 0);
                var heavy = alts.OrderByDescending(a => a.WeightKg).First();
                if (heavy.WeightKg - wmin > 0.05)
                    check("So sánh: phương án nặng nhất báo 'Vượt +… so với phương án tiết kiệm nhất'",
                        B(heavy.Warning.Contains(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Vượt +{0:0.0} kg", heavy.WeightKg - wmin))
                          && heavy.Warning.Contains("so với phương án tiết kiệm nhất")), 1, 0);
                var lighter = alts.FirstOrDefault(a => a.WeightKg < cur.WeightKg - 0.05);
                if (lighter != null)
                    check("So sánh: phương án nhẹ hơn hiện tại báo 'Tiết kiệm −…'", B(lighter.Warning.Contains("Tiết kiệm −") && lighter.WarnLevel != 0), 1, 0);
                // áp dụng phương án cả tầng → mọi ô có lựa chọn
                var mesh = alts.First(a => a.Scheme == SlabRebarScheme.TwoLayerMesh);
                int nw = RebarOptions.Apply(p, s0, zs, mesh);
                check("Áp dụng phương án cả tầng: ghi cho mọi ô của tầng", nw, p.Slabs.Count(x => x.Story == s0.Story), 0);
                var dsm = SlabDesigner.Design(p, null, new List<string>());
                check("Áp dụng phương án cả tầng: mọi ô dùng lưới 2 lớp", B(dsm.All(d => d.Scheme == SlabRebarScheme.TwoLayerMesh)), 1, 0);
                var q = p.Clone();
                check("Phương án sàn lưu JSON (bản sao dự án)", B(SlabDesigner.Design(q, null, new List<string>()).All(d => d.Scheme == SlabRebarScheme.TwoLayerMesh)), 1, 0);
                RebarOptions.Clear(p, s0, zs);
                check("Bỏ chọn phương án cả tầng", p.RebarChoices.Count(c => c.Zone == RebarZones.SlabScheme), 0, 0);
            }

            // so sánh khối lượng phương án Ø–bước lớp dưới
            var zb = RebarOptions.Zones(p, s0).First(z => z.Zone == RebarZones.SlabBotX);
            var ab = RebarOptions.Alternatives(p, null, s0, zb);
            double bmin = ab.Where(a => a.Fits && a.AsProv >= a.AsReq * (1 - 1e-6)).Min(a => a.WeightKg);
            check("Phương án lớp dưới: KL = As·7850·Lx·Ly", B(ab.All(a => Math.Abs(a.WeightKg - Math.Round(a.AsProv * 7850 * s0.Lx * s0.Ly, 2)) < 0.011)), 1, 0);
            check("Phương án lớp dưới: chênh so với tiết kiệm nhất đúng", B(ab.All(a => Math.Abs(a.DeltaVsBestKg - Math.Round(a.WeightKg - bmin, 2)) < 0.011)), 1, 0);
            check("Phương án lớp dưới: As/As,yc > 1,3 → 'Dư thép'", B(ab.Where(a => a.AsReq > 0 && a.AsProv / a.AsReq > 1.3 + 1e-9).All(a => a.Warning.Contains("Dư thép"))), 1, 0);
        }

        // ---------------- lưới 2 lớp dải 16 m ----------------
        {
            var p = Floor(new[] { 4.0, 4.0, 4.0, 4.0 }, new[] { 4.0 });
            p.RebarPrefs.SlabScheme = SlabRebarScheme.TwoLayerMesh;
            var ds = SlabDesigner.Design(p, null, new List<string>());
            var meshX = TopBars(ds, "lưới phương X").ToList();
            check("Lưới 2 lớp: lưới trên phương X liên tục – một thanh cho cả dải 4 ô", meshX.Count, 1, 0);
            check("Lưới 2 lớp: thanh lưới dài hơn cây 11,7 m (16 m + neo)", B(meshX.Count == 1 && meshX[0].Length > 16.0), 1, 0);
            check("Lưới 2 lớp: lưới đủ → không cần mũ bổ sung", TopBars(ds, "mũ").Count(), 0, 0);
            check("Lưới 2 lớp: kiểm tra uốn gối đạt", B(ds.All(d => d.Checks.Where(k => k.Name.StartsWith("Uốn gối")).All(k => k.Ok))), 1, 0);
            check("Lưới 2 lớp: chuỗi thép trên có 'lưới'", B(ds.All(d => (d.TopX + d.TopY).Contains("lưới"))), 1, 0);
            if (meshX.Count == 1)
            {
                var bm = meshX[0];
                var shop = RebarShop.Build(p, new DesignSummary { Slabs = ds }, new ShopOptions { StockLength = 11.7, Stagger = true });
                var parts = shop.Bars.Where(b => b.Role == bm.Role).ToList();
                double lap = RebarDetailing.LapLength(p.Material(p.Concrete.ConcreteSlab), p.Material(bm.Dia < 10 ? p.Concrete.RebarStirrup : p.Concrete.RebarMain), bm.Dia);
                check("Lưới 2 lớp: thanh lưới dài được tách nối chồng", B(parts.Count >= 2 && parts.All(b => b.Spliced)), 1, 0);
                check("Lưới 2 lớp: mọi đoạn ≤ 11,7 m", B(parts.All(b => b.CutLengthMm <= 11700)), 1, 0);
                double tot = parts.Sum(b => b.CutLengthMm * b.BarsPerMember) / 1000.0;
                int splices = (int)Math.Ceiling(bm.Length / 11.7 - 1e-9) - 1;
                check("Lưới 2 lớp: tổng chiều dài cắt = số thanh × (L + số mối nối·ll)", tot, bm.Count * (bm.Length + splices * lap), 0.003);
            }
            var plan = SlabDrawing.Build(p, new DesignSummary { Slabs = ds }, p.Stories[0], 50);
            check("Lưới 2 lớp: mặt bằng thép vẽ được", B(plan.EntityCount > 0), 1, 0);
        }

        // ---------------- lưới 2 lớp + mũ tăng cường ----------------
        {
            var p = Floor(new[] { 4.0, 4.0 }, new[] { 4.0 });
            p.RebarPrefs.SlabScheme = SlabRebarScheme.TwoLayerMeshPlusTop;
            var ds = SlabDesigner.Design(p, null, new List<string>());
            check("Lưới + mũ tăng cường: có lưới trên", B(TopBars(ds, "lưới").Any()), 1, 0);
            check("Lưới + mũ tăng cường: mũ cạnh chung tối đa một thanh", B(TopBars(ds, "mũ gối chung").Count() <= 1), 1, 0);
            check("Lưới + mũ tăng cường: kiểm tra uốn gối đạt", B(ds.All(d => d.Checks.Where(k => k.Name.StartsWith("Uốn gối")).All(k => k.Ok))), 1, 0);
            var pa = Floor(new[] { 4.0, 4.0 }, new[] { 4.0 });
            var da = SlabDesigner.Design(pa, null, new List<string>());
            double wa = da.Sum(d => d.Bars.Sum(b => b.Count * b.WeightPerBar)), wc = ds.Sum(d => d.Bars.Sum(b => b.Count * b.WeightPerBar));
            Console.WriteLine($"   · KL thép sàn 2 ô: mũ + lưới dưới {wa:0.0} kg, lưới 2 lớp + mũ {wc:0.0} kg");
            check("Lưới 2 lớp + mũ nặng hơn phương án mũ + lưới dưới (có thêm lưới trên)", B(wc > wa), 1, 0);
        }
    }
}
