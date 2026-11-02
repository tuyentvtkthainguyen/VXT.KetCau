using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng địa chất – nền móng: ứng suất bản thân, sức chịu tải cọc theo SPT (TCVN 10304:2014 G.3.2),
/// chọn mũi cọc, lún móng chữ nhật (cộng lún từng lớp), gợi ý phương án móng, móng đơn mặc định nền tốt không đổi.
/// Giá trị kỳ vọng tính độc lập ngay trong test (công thức viết lại, hệ số góc Boussinesq dạng Newmark).</summary>
public static class GeoTests
{
    /// <summary>Hố khoan mẫu: 0–1,2 đất đắp; 1,2–4 sét pha dẻo cứng N = 10; 4–9 bùn sét N = 2; 9–18 cát vừa chặt vừa N = 20; 18–30 cát thô N = 40; nước ngầm 2 m.</summary>
    public static Borehole Sample() => new()
    {
        Name = "HK1", X = 4.5, Y = 4.5, GroundElevation = 0, WaterDepth = 2,
        Layers =
        {
            new SoilLayer { Name = "Đất đắp", Kind = SoilKind.Fill, Top = 0, Bottom = 1.2, Gamma = 17, Phi = 10, C = 8, E = 3, N = 4, IL = -1 },
            new SoilLayer { Name = "Sét pha dẻo cứng", Kind = SoilKind.SiltyClay, Top = 1.2, Bottom = 4, Gamma = 19, Phi = 16, C = 25, E = 10, N = 10, IL = 0.35 },
            new SoilLayer { Name = "Bùn sét", Kind = SoilKind.Organic, Top = 4, Bottom = 9, Gamma = 16, Phi = 4, C = 8, E = 1.5, N = 2, IL = 1.1 },
            new SoilLayer { Name = "Cát vừa chặt vừa", Kind = SoilKind.Sand, Grain = SandGrain.Medium, Top = 9, Bottom = 18, Gamma = 19, GammaSat = 20, Phi = 32, C = 1, E = 20, N = 20 },
            new SoilLayer { Name = "Cát thô", Kind = SoilKind.Sand, Grain = SandGrain.Coarse, Top = 18, Bottom = 30, Gamma = 20, GammaSat = 21, Phi = 37, E = 40, N = 40 }
        }
    };

    public static void Run(Action<string, double, double, double> check)
    {
        var bh = Sample();

        // ---------------- ứng suất hữu hiệu ----------------
        // γ' = γsat − 10 dưới mực nước 2 m; γsat mặc định = γ + 1
        double sg12 = 17 * 1.2, sg2 = sg12 + 19 * 0.8, sg4 = sg2 + (20 - 10) * 2.0, sg9 = sg4 + (17 - 10) * 5.0, sg18 = sg9 + (20 - 10) * 9.0;
        check("σ'bt(1,2 m) = 17·1,2", GeotechCalc.EffectiveStress(bh, 1.2), sg12, 1e-9);
        check("σ'bt(3 m) – dưới mực nước", GeotechCalc.EffectiveStress(bh, 3.0), sg2 + 10 * 1.0, 1e-9);
        check("σ'bt(9 m)", GeotechCalc.EffectiveStress(bh, 9.0), sg9, 1e-9);
        check("σ'bt(20 m)", GeotechCalc.EffectiveStress(bh, 20.0), sg18 + (21 - 10) * 2.0, 1e-9);
        check("Lớp tại 4,0 m (ranh giới) = bùn sét", GeotechCalc.LayerAt(bh, 4.0)?.Name == "Bùn sét" ? 1 : 0, 1, 0);
        check("Lớp dưới đáy hố khoan = lớp cuối", GeotechCalc.LayerAt(bh, 35)?.Name == "Cát thô" ? 1 : 0, 1, 0);

        // ---------------- áp dụng hố khoan cho móng nông ----------------
        var ss = new SoilSettings();
        GeotechCalc.ApplyToSoil(ss, bh, 1.5);
        check("Áp dụng HK: φ của lớp tại Df = 1,5 m", ss.Phi, 16, 1e-12);
        check("Áp dụng HK: c", ss.C, 25, 1e-12);
        check("Áp dụng HK: γ (trên mực nước)", ss.Gamma, 19, 1e-12);
        check("Áp dụng HK: γ' trên đáy = σ'(Df)/Df", ss.GammaAbove, (17 * 1.2 + 19 * 0.3) / 1.5, 1e-9);

        // ---------------- mũi cọc tự chọn ----------------
        double d = 0.30, cut = 1.5;
        double tip = GeotechCalc.AutoTipDepth(bh, d, cut);
        check("Mũi cọc tự chọn = mặt lớp N ≥ 30 (18 m) + 3d", tip, 18 + 3 * d, 1e-9);

        // ---------------- sức chịu tải cọc SPT (cọc ép 300×300, đầu cọc 1,5 m, mũi 18,9 m) ----------------
        {
            double u = 4 * d, Ab = d * d;
            double Clay(double n, double sv)
            {
                double cu = 6.25 * n, psi = cu / sv;
                double a = psi <= 1 ? 0.5 / Math.Sqrt(psi) : 0.5 / Math.Pow(psi, 0.25);
                return Math.Min(a, 1) * cu;
            }
            // đoạn chia tại ranh giới lớp và mực nước ngầm; σ'v tại giữa đoạn
            double q1 = u * Clay(10, sg12 + 19 * 0.55) * 0.5;     // 1,5–2,0 sét pha
            double q2 = u * Clay(10, sg2 + 10 * 1.0) * 2.0;       // 2,0–4,0 sét pha
            double q3 = u * Clay(2, sg4 + 7 * 2.5) * 5.0;         // 4–9 bùn sét
            double q4 = u * (10 * 20 / 3.0) * 9.0;               // 9–18 cát vừa
            double q5 = u * (10 * 40 / 3.0) * 0.9;               // 18–18,9 cát thô
            double qb = 300 * 40 * Ab;                           // Np = 40 (18,6 … 20,1 m trong cát thô)
            double rcu = q1 + q2 + q3 + q4 + q5 + qb;
            double pvl = 14.5e3 * Ab + 350e3 * 4 * Math.PI * 0.016 * 0.016 / 4;
            var pr = GeotechCalc.PileCapacity(bh, d, false, tip, cut, false, 2.5);
            Console.WriteLine($"   · cọc 300×300 L = {pr.Length:0.00} m: Rc,u = {pr.Rcu:0.0} kN (mũi {pr.TipResistance:0.0}, ma sát {pr.ShaftResistance:0.0}), [P]đn = {pr.Pdn:0.0}, Pvl = {pr.Pvl:0.0} → [P] = {pr.Allowable:0.0} kN");
            check("SPT: ma sát thân cọc u·Σf·l", pr.ShaftResistance, q1 + q2 + q3 + q4 + q5, 1e-9);
            check("SPT: sức kháng mũi qb·Ab = 300·Np·Ab", pr.TipResistance, qb, 1e-9);
            check("SPT: Rc,u", pr.Rcu, rcu, 1e-9);
            check("SPT: [P]đn = Rc,u/2,5", pr.Pdn, rcu / 2.5, 1e-9);
            check("Pvl = Rb·Ab + Rsc·As (B25, 4Ø16 CB400)", pr.Pvl, pvl, 1e-6);
            check("[P] = min([P]đn, Pvl)", pr.Allowable, Math.Min(rcu / 2.5, pvl), 1e-9);
            check("Số dòng bảng: 5 đoạn thân + mũi", pr.Rows.Count, 6, 0);
            var prb = GeotechCalc.PileCapacity(bh, 0.6, true, tip, cut, true, 2.5);
            check("Khoan nhồi D600: qb = 150·Np", prb.Qb, 150 * GeotechCalc.AverageN(bh, tip - 0.6, tip + 2.4), 1e-9);
            check("N trung bình vùng mũi 17,2…18,7 m", GeotechCalc.AverageN(bh, 17.5 - d, 17.5 + 4 * d), (0.8 * 20 + 0.7 * 40) / 1.5, 1e-9);
        }

        // ---------------- lún móng 2×2 m trên nền đồng nhất E = 10 MPa ----------------
        {
            var hb = new Borehole
            {
                Name = "ĐN", WaterDepth = -1,
                Layers = { new SoilLayer { Name = "Sét pha", Kind = SoilKind.SiltyClay, Top = 0, Bottom = 30, Gamma = 19, Phi = 16, C = 25, E = 10, N = 10 } }
            };
            double L = 2, B = 2, Df = 1.5, p = 200;
            // hệ số góc Boussinesq dạng Newmark (độc lập với công thức trong GeotechCalc)
            double Corner(double a, double b, double z)
            {
                double m = a / z, n = b / z, V = m * m + n * n + 1;
                return (2 * m * n * Math.Sqrt(V) / (V + m * m * n * n) * (V + 1) / V + Math.Atan2(2 * m * n * Math.Sqrt(V), V - m * m * n * n)) / (4 * Math.PI);
            }
            double sgl = p - 19 * Df, h = 0.4 * B, z = 0, S = 0;
            while (z < 30)
            {
                double zm = z + h / 2, sz = 4 * Corner(L / 2, B / 2, zm) * sgl, sbt = 19 * (Df + zm);
                if (sz <= 0.2 * sbt) break;
                S += 0.8 * sz * h / 10000;
                z += h;
            }
            var st = GeotechCalc.Settlement(hb, L, B, Df, p);
            Console.WriteLine($"   · lún móng 2×2 m, p = 200 kPa: S = {st.S * 100:0.00} cm, Hc = {st.Hc:0.0} m, {st.Rows.Count} lớp phân tố");
            check("Lún 2×2 m E = 10 MPa: S = 0,8·Σσz·h/E", st.S, S, 1e-9);
            check("Lún: chiều sâu vùng nén Hc", st.Hc, z, 1e-9);
            check("Lún: σgl = p − γ·Df", st.SigmaGl, sgl, 1e-12);
            check("Hệ số góc z → 0 = 1/4", GeotechCalc.CornerFactor(1, 1, 0), 0.25, 1e-12);
            check("Hệ số góc 3×1, z = 2 (so dạng Newmark)", GeotechCalc.CornerFactor(3, 1, 2), Corner(3, 1, 2), 1e-12);
        }

        // ---------------- gợi ý phương án móng (chưa phân tích, ước tính tải) ----------------
        {
            var pa = ModelBuilder.NewProject("Gợi ý móng");
            ModelBuilder.SetGrid(pa, new[] { 4.5, 4.5, 4.5 }, new[] { 4.5, 4.5, 4.5 });
            ModelBuilder.SetStories(pa, new[] { 3.6, 3.3, 3.3, 3.3, 3.3, 3.3 }, 0.0);
            pa.Boreholes.Add(Sample());
            var adv = FoundationAdvisor.Recommend(pa, null, pa.Boreholes[0]);
            foreach (var o in adv.Options)
                Console.WriteLine($"   · {o.Name}: {(o.Feasible ? "khả thi" : "không")}{(o.Recommended ? " ← ĐỀ XUẤT" : "")} – {o.Reason}");
            Console.WriteLine("   · " + adv.Explanation);
            check("Gợi ý: Nmax ước tính = 12·4,5·4,5·6 sàn", adv.NMax, 12 * 4.5 * 4.5 * 6, 1e-9);
            check("Gợi ý: móng đơn không khả thi (lớp bùn trong 2B)", adv.Options[0].Feasible ? 0 : 1, 1, 0);
            check("Gợi ý: lý do móng đơn có lớp yếu", adv.Options[0].Reason.Contains("lớp yếu") ? 1 : 0, 1, 0);
            check("Gợi ý: móng băng không khả thi", adv.Options[1].Feasible ? 0 : 1, 1, 0);
            check("Gợi ý: đề xuất móng cọc", adv.Recommended?.Type == FoundationType.Pile ? 1 : 0, 1, 0);
            var pa2 = pa.Clone();
            adv.Recommended.ApplyTo(pa2);
            check("Áp dụng phương án: loại móng = cọc", pa2.Foundation.Type == FoundationType.Pile ? 1 : 0, 1, 0);
            check("Áp dụng phương án: [P] từ hố khoan", pa2.Foundation.PileCapacityFromBorehole ? 1 : 0, 1, 0);
            // lưu / mở giữ hố khoan
            var back = Project.Load(SaveTemp(pa));
            check("Lưu/mở .kcvxt: số lớp hố khoan", back.Boreholes.Count == 1 ? back.Boreholes[0].Layers.Count : -1, 5, 0);
            check("Lưu/mở .kcvxt: mực nước ngầm", back.Boreholes[0].WaterDepth, 2, 1e-12);
        }

        // ---------------- móng đơn: mặc định nền tốt không đổi; dùng hố khoan thêm kiểm tra lún ----------------
        {
            var p = ModelBuilder.NewProject("Khung thử địa chất");
            ModelBuilder.SetGrid(p, new[] { 4.0, 4.0 }, new[] { 4.5, 4.5, 4.5 });
            ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, 0.0);
            ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
            {
                ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5
            });
            check("Mặc định nền đất tốt: γ = 19, φ = 16, c = 25", p.Soil.Gamma + p.Soil.Phi + p.Soil.C, 19 + 16 + 25, 1e-12);
            AnalysisResults r = FrameAnalysis.Run(p);
            int nCols = FoundationUtil.BaseColumns(p).Count;
            var s0 = ConcreteDesigner.Design(p, r);
            p.Boreholes.Add(Sample());
            var s1 = ConcreteDesigner.Design(p, r);   // có hố khoan nhưng UseBorehole = false → không đổi
            check("Nền tốt mặc định: số loại móng đơn không đổi khi thêm hố khoan (chưa áp dụng)", s1.Footings.Count, s0.Footings.Count, 0);
            check("Nền tốt mặc định: tổng số móng = số chân cột", s1.Footings.Sum(f => f.Quantity), nCols, 0);
            check("Nền tốt mặc định: không thêm hạng mục lún", s1.Footings.Count(f => f.Checks.Any(c => c.Name.StartsWith("Độ lún"))), 0, 0);
            check("Nền tốt mặc định: kích thước móng không đổi", s1.Footings.Sum(f => f.L * f.B * f.Quantity), s0.Footings.Sum(f => f.L * f.B * f.Quantity), 1e-12);

            p.Soil.UseBorehole = true;
            var s2 = ConcreteDesigner.Design(p, r);
            check("Dùng hố khoan: tổng số móng = số chân cột", s2.Footings.Sum(f => f.Quantity), nCols, 0);
            check("Dùng hố khoan: mọi móng có kiểm tra lún (hạng mục cuối)", s2.Footings.Count(f => f.Checks.Count > 0 && f.Checks[^1].Name.StartsWith("Độ lún")), s2.Footings.Count, 0);
            var f0 = s2.Footings.OrderByDescending(f => f.N).First();
            double sExp = GeotechCalc.Settlement(Sample(), f0.L, f0.B, p.Soil.Depth, f0.Ptb).S * 100;
            // móng gộp giữ hạng mục bất lợi nhất của các vị trí → S ghi ≥ S của chính móng đại diện
            check("Dùng hố khoan: lún móng ≥ Settlement(L, B, Df, ptb) của móng đại diện", f0.Checks[^1].Demand >= sExp - 1e-9 ? 1 : 0, 1, 0);
            check("Dùng hố khoan: giới hạn lún 8 cm", f0.Checks[^1].Capacity, 8, 1e-12);

            // móng cọc: [P] từ hố khoan
            p.Foundation.Type = FoundationType.Pile;
            p.Foundation.PileSize = 0.25;
            p.Foundation.PileCapacityFromBorehole = true;
            var s3 = ConcreteDesigner.Design(p, r);
            var pr = GeotechCalc.PileCapacity(p, Sample(), p.Foundation);
            check("Cọc theo hố khoan: có đài cọc", s3.PileCaps.Count > 0 ? 1 : 0, 1, 0);
            check("Cọc theo hố khoan: [P] = ⌊min([P]đn, Pvl)⌋", s3.PileCaps.Count > 0 ? s3.PileCaps[0].PileCapacity : -1, Math.Floor(pr.Allowable), 1e-9);
            check("Cọc theo hố khoan: L = mũi − đáy đài", s3.PileCaps.Count > 0 ? s3.PileCaps[0].PileLength : -1, Math.Round(pr.TipDepth - p.Soil.Depth, 2), 1e-9);

            // thuyết minh có mục "ĐỊA CHẤT VÀ NỀN MÓNG" (bảng hố khoan, lún, sức chịu tải cọc)
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "kcvxt_geo_test");
                Directory.CreateDirectory(dir);
                CalculationReport.Export(p, r, s2, Path.Combine(dir, "tm_mongdon_hk.docx"));
                CalculationReport.Export(p, r, s3, Path.Combine(dir, "tm_coc_hk.docx"));
                check("Thuyết minh có mục địa chất (móng đơn + móng cọc)", 1, 1, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine("   · lỗi xuất thuyết minh địa chất: " + ex.Message);
                check("Thuyết minh có mục địa chất (móng đơn + móng cọc)", 0, 1, 0);
            }

            // móng băng có hố khoan: thêm kiểm tra lún
            p.Foundation.Type = FoundationType.Strip;
            p.Foundation.PileCapacityFromBorehole = false;
            var s4 = ConcreteDesigner.Design(p, r);
            check("Móng băng theo hố khoan: có kiểm tra lún", s4.StripFootings.Count > 0 && s4.StripFootings.All(m => m.Checks[^1].Name.StartsWith("Độ lún")) ? 1 : 0, 1, 0);
            p.Foundation.Type = FoundationType.Isolated;
            p.Soil.UseBorehole = false;
        }
    }

    private static string SaveTemp(Project p)
    {
        string dir = Path.Combine(Path.GetTempPath(), "kcvxt_geo_test");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "geo.kcvxt");
        p.Save(path);
        return path;
    }
}
