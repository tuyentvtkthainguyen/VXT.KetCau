using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng nguyên tắc nối thép (không nối trong vùng nguy hiểm: dầm 3 nhịp 9 m, cột 6 tầng, dải sàn 16 m),
/// ống nối ren / đề xuất cây dài khi vùng cho phép ngắn, độ dãn dài khi uốn, lựa chọn thép đài cọc / móng băng,
/// dầm cong chia nút thiết kế liên tục + đai do người dùng chọn. check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class SpliceRuleTests
{
    private static double B(bool v) => v ? 1 : 0;

    private static Project Frame(double[] sx, double[] sy, double[] heights, string beamX = "D22x50", string beamY = "D22x40")
    {
        var p = ModelBuilder.NewProject("Kiểm thử nguyên tắc nối");
        ModelBuilder.SetGrid(p, sx, sy);
        ModelBuilder.SetStories(p, heights, 0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C30x30", BeamSectionX = beamX, BeamSectionY = beamY, PerimeterWallLoad = 5 });
        if (p.Combinations.Count == 0) ComboGenerator.Generate(p);
        return p;
    }

    private static bool Overlaps(double a, double b, double c, double d) => Math.Min(b, d) - Math.Max(a, c) > 1e-6;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Console.WriteLine("SR. Nguyên tắc nối thép, độ dãn khi uốn, chọn thép móng, dầm cong liên tục");

        // ---------------- độ dãn dài khi uốn ----------------
        {
            var o = new ShopOptions();
            check("Dãn khi uốn: đai kín Ø6 = 6·(3·2 + 2·3) = 72 mm", RebarShop.BendDeduction("51", 6, o), 72, 1e-9);
            check("Dãn khi uốn: chữ U Ø12 = 2·2·12 = 48 mm", RebarShop.BendDeduction("21", 12, o), 48, 1e-9);
            check("Dãn khi uốn: thanh thẳng = 0", RebarShop.BendDeduction("00", 20, o), 0, 0);
            o.BendDeduction = false;
            check("Dãn khi uốn: tắt → 0", RebarShop.BendDeduction("51", 6, o), 0, 0);
            var p0 = ModelBuilder.NewProject("t");
            var fake = new DesignSummary();
            var bd = new BeamLineDesign { Name = "D-test", Quantity = 1 };
            bd.Bars.Add(new BarMark { Mark = 1, Role = "Đai", Dia = 6, Count = 10, Length = 1.13, Shape = "Đai", Spacing = 0.15, Segments = new List<double> { 170, 320 } });
            fake.Beams.Add(bd);
            var sh = RebarShop.Build(p0, fake, new ShopOptions());
            var st = sh.Bars.FirstOrDefault();
            check("Bảng gia công: đai 170×320 Ø6 – kích thước ngoài 1130, chiều dài cắt 1060 mm",
                B(st != null && st.OutsideLengthMm == 1130 && st.CutLengthMm == 1060), 1, 0);
        }

        // ---------------- ống nối ren / đề xuất ----------------
        {
            var pl = RebarShop.PlanSplices(0, 15, 0, 0, 0.8, 11.7, new List<(double, double)> { (5.0, 5.5) }, true);
            check("Vùng cho phép 0,5 m < ll 0,8 m: dùng ống nối ren", pl.Couplers, 1, 0);
            check("Ống nối ren nằm trong vùng cho phép", B(pl.Centers.All(c => c >= 5.0 - 1e-9 && c <= 5.5 + 1e-9)), 1, 0);
            check("Ống nối ren: có ghi chú đề xuất, không vi phạm", B(!pl.Violation && pl.Issues.Any(x => x.Contains("ống nối ren"))), 1, 0);
            var pv = RebarShop.PlanSplices(0, 15, 0, 0, 0.8, 11.7, new List<(double, double)> { (5.0, 5.5), (12.0, 14.0) }, false);
            check("Không cho ống ren, vùng hợp lệ ngoài tầm cây 11,7 m: báo VI PHẠM", B(pv.Violation && pv.Issues.Any(x => x.Contains("VI PHẠM"))), 1, 0);
            check("Đề xuất cây dài ≥ 12,8 m để nối ở vùng 12–14 m", B(pv.Issues.Any(x => x.Contains("dùng cây dài ≥ 12.8"))), 1, 0);
            var pn = RebarShop.PlanSplices(0, 15, 0, 0, 0.8, 11.7, new List<(double, double)>(), false);
            check("Không có vùng cho phép: vi phạm + đề xuất ống nối ren", B(pn.Violation && pn.Issues.Any(x => x.Contains("ống nối ren"))), 1, 0);
        }

        // ---------------- dầm liên tục 3 nhịp 9 m ----------------
        {
            var p = Frame(new[] { 9.0, 9.0, 9.0 }, new[] { 6.0 }, new[] { 3.6 });
            DesignSummary ds = null; AnalysisResults r = null;
            try { r = FrameAnalysis.Run(p); ds = ConcreteDesigner.Design(p, r); }
            catch (Exception ex) { Console.WriteLine("   · lỗi thiết kế khung 3 nhịp: " + ex.Message); }
            var beam = ds?.Beams.Where(b => b.Spans.Count == 3).OrderByDescending(b => b.TotalLength).FirstOrDefault();
            check("Dầm 3 nhịp 9 m: có dải dầm 27 m", B(beam != null && Math.Abs(beam.TotalLength - 27) < 0.01), 1, 0);
            if (beam != null)
            {
                var shop = RebarShop.Build(p, new DesignSummary { Beams = new List<BeamLineDesign> { beam } });
                var sps = shop.Splices.Where(x => x.Member == beam.Name).ToList();
                check("Dầm 27 m: có mối nối", B(sps.Count > 0), 1, 0);
                int bad = 0, forb = 0;
                foreach (var sp in sps)
                {
                    var bm = beam.Bars.First(x => x.Mark == sp.Mark);
                    var allowed = SpliceRules.BeamAllowed(p, beam, bm.IsTop, bm.Role);
                    if (!allowed.Any(z => sp.From >= z.A - 1e-6 && sp.To <= z.B + 1e-6)) bad++;
                    foreach (var (name, a, b) in SpliceRules.BeamForbidden(p, beam, bm.IsTop))
                        if (Overlaps(sp.From, sp.To, a, b) || (sp.Coupler && sp.Center > a + 1e-6 && sp.Center < b - 1e-6)) forb++;
                }
                check("Dầm 27 m: mọi mối nối nằm trọn trong vùng cho phép", bad, 0, 0);
                check("Dầm 27 m: không mối nối nào chạm nút khung / vùng tới hạn / vùng mô men lớn", forb, 0, 0);
                check("Dầm 27 m: không vi phạm", shop.Violations, 0, 0);
                // vùng tới hạn = h (không kháng chấn), 2h khi kháng chấn
                check("Vùng tới hạn dầm lcr = h", SpliceRules.BeamCritical(p, beam.H), beam.H, 1e-9);
                p.Seismic.Enabled = true;
                check("Vùng tới hạn dầm kháng chấn lcr = 2h", SpliceRules.BeamCritical(p, beam.H), 2 * beam.H, 1e-9);
                p.Seismic.Enabled = false;
            }
        }

        // ---------------- cột 6 tầng ----------------
        {
            var p = Frame(new[] { 5.0 }, new[] { 5.0 }, new[] { 3.6, 3.3, 3.3, 3.3, 3.3, 3.3 }, "D22x40", "D22x40");
            DesignSummary ds = null;
            try { ds = ConcreteDesigner.Design(p, FrameAnalysis.Run(p)); }
            catch (Exception ex) { Console.WriteLine("   · lỗi thiết kế cột 6 tầng: " + ex.Message); }
            var col = ds?.Columns.FirstOrDefault(c => c.Stories.Count == 6);
            check("Cột 6 tầng: có chồng cột 6 tầng", B(col != null), 1, 0);
            if (col != null)
            {
                double zBase = col.Stories[0].ZBottom;
                int bad = 0;
                foreach (var sp in col.Splices.Where(x => !x.Coupler))
                {
                    var s = col.Stories[sp.Span];
                    double z0 = s.ZBottom - zBase, z1 = s.ZTop - zBase, hc = z1 - s.JointDepth - z0;
                    double lcr = Math.Max(Math.Max(Math.Max(s.B, s.H), hc / 6), 0.45);
                    if (sp.From < z0 + lcr - 1e-6 || sp.To > z1 - s.JointDepth - lcr + 1e-6) bad++;
                }
                check("Cột 6 tầng: 5 vị trí nối (giữa các tầng)", col.Splices.Select(x => x.Span).Distinct().Count(), 5, 0);
                check("Cột 6 tầng: mọi mối nối ngoài nút khung và lcr = max(hc; Hc/6; 450) hai đầu tầng", bad, 0, 0);
                check("Cột 6 tầng: nối chồng (không cần ống ren) với tầng 3,3 m", col.Splices.Count(x => x.Coupler), 0, 0);
                bool stag = col.Splices.GroupBy(x => x.Span).All(g => g.Count() == 1 ||
                    Math.Abs(g.First(x => x.Group == "A").Center - g.First(x => x.Group == "B").Center) >= 1.3 * g.First().Lap - 0.011);
                check("Cột 6 tầng: so le ≥ 1,3·ll (≤ 50% thanh nối tại một tiết diện)", B(stag && col.Splices.Any(x => x.Group == "B")), 1, 0);
                check("Cột 6 tầng: số hiệu thép hợp lệ", RebarNumbering.Validate(col.Bars).Count, 0, 0);
            }
        }

        // ---------------- dải sàn 16 m (lưới 2 lớp) ----------------
        {
            var p = ModelBuilder.NewProject("Dải sàn 16 m");
            ModelBuilder.SetGrid(p, new[] { 4.0, 4.0, 4.0, 4.0 }, new[] { 4.0 });
            ModelBuilder.SetStories(p, new[] { 3.6 }, 0);
            ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 0 });
            p.RebarPrefs.SlabScheme = SlabRebarScheme.TwoLayerMesh;
            var ds = SlabDesigner.Design(p, null, new List<string>());
            var shop = RebarShop.Build(p, new DesignSummary { Slabs = ds });
            var mesh = ds.SelectMany(d => d.Bars.Select(b => (D: d, B: b))).FirstOrDefault(x => x.B.Role.Contains("lưới phương X") && x.B.Length > 11.7);
            check("Dải sàn 16 m: có lưới trên liên tục > 11,7 m", B(mesh.B != null), 1, 0);
            if (mesh.B != null)
            {
                var sps = shop.Splices.Where(x => x.Member == mesh.D.Name && x.Mark == mesh.B.Mark).ToList();
                var al = mesh.B.SpliceAllowed;
                bool inZone = sps.Count > 0 && sps.All(sp => Enumerable.Range(0, al.Count / 2).Any(i => sp.From >= al[2 * i] - 1e-6 && sp.To <= al[2 * i + 1] + 1e-6));
                check("Dải sàn 16 m: mối nối lưới trên nằm trong ⅓ giữa nhịp ô", B(inZone), 1, 0);
                // gối (dầm) tại 0, 4, 8, 12, 16 m (+ đoạn neo đầu): mối nối cách gối > L/4
                double off = al[0] - 4.0 / 3;
                bool far = sps.All(sp => Enumerable.Range(0, 5).All(k => sp.From > off + 4 * k + 1.0 - 1e-6 || sp.To < off + 4 * k - 1.0 + 1e-6));
                check("Dải sàn 16 m: không nối lưới trên trên gối (cách tim dầm > L/4)", B(far), 1, 0);
                check("Dải sàn 16 m: không vi phạm", shop.Violations, 0, 0);
            }
        }

        // ---------------- chọn thép đài cọc / móng băng ----------------
        foreach (var ft in new[] { FoundationType.Pile, FoundationType.Strip })
        {
            var p = Frame(new[] { 5.0, 5.0 }, new[] { 5.0 }, new[] { 3.6, 3.3 }, "D22x40", "D22x40");
            p.Foundation.Type = ft;
            DesignSummary ds = null; AnalysisResults r = null;
            try { r = FrameAnalysis.Run(p); ds = ConcreteDesigner.Design(p, r); }
            catch (Exception ex) { Console.WriteLine("   · lỗi thiết kế móng " + ft + ": " + ex.Message); }
            MemberDesignBase m = ft == FoundationType.Pile ? ds?.PileCaps.FirstOrDefault() : ds?.StripFootings.FirstOrDefault();
            string what = ft == FoundationType.Pile ? "đài cọc" : "móng băng";
            check("Chọn thép " + what + ": có cấu kiện và khoá", B(m != null && m.RebarKeys.Count > 0), 1, 0);
            if (m == null) continue;
            var zones = RebarOptions.Zones(p, m);
            check("Chọn thép " + what + ": có vùng thép", B(zones.Count >= (ft == FoundationType.Pile ? 4 : 3)), 1, 0);
            var z0 = zones[0];
            var alts = RebarOptions.Alternatives(p, r, m, z0);
            check("Chọn thép " + what + ": phương án đủ As, có khối lượng", B(alts.Count > 0 && alts.All(a => a.AsProv >= a.AsReq * (1 - 1e-6) && a.WeightKg > 0)), 1, 0);
            var pick = alts.FirstOrDefault(a => !a.IsCurrent) ?? alts[0];
            RebarOptions.Apply(p, m, z0, pick);
            var d2 = ConcreteDesigner.Design(p, r);
            var m2 = d2.All.FirstOrDefault(x => x.RebarKeys.Contains(m.RebarKeys[0]));
            string got = m2 switch
            {
                KetCauVXT.Design.Foundation.PileCapDesign pc => pc.BarsL,
                KetCauVXT.Design.Foundation.StripFootingDesign sf => sf.BottomBars,
                _ => ""
            };
            check("Chọn thép " + what + ": thiết kế lại dùng phương án chọn (" + pick.Text + ")", B(m2 != null && got == pick.Text && m2.UserRebarZones >= 1), 1, 0);
            var shop = RebarShop.Build(p, d2);
            if (ft == FoundationType.Pile)
                check("Đài cọc: không nối thép (thanh nguyên)", shop.Splices.Count(x => x.Member == m2.Name), 0, 0);
        }

        // ---------------- dầm cong chia nút: một dải liên tục + đai chọn ----------------
        {
            const double R = 4.0;
            var p = new Project();
            p.Materials.Add(Material.Concrete("B25"));
            p.FrameSections.Add(FrameSection.Rect("D30x50", 300, 500));
            p.FrameSections.Add(FrameSection.Rect("C30x30", 300, 300));
            p.Stories.Add(new Story { Name = "T1", Elevation = 3, Height = 3 });
            p.Patterns.Add(new LoadPattern { Name = "P", Type = LoadType.Dead });
            double c45 = R * Math.Cos(Math.PI / 4), s45 = R * Math.Sin(Math.PI / 4);
            p.Nodes.Add(new Node { Id = 1, X = R, Y = 0, Z = 3, Restraint = Node.Fixed, Story = "T1" });
            p.Nodes.Add(new Node { Id = 2, X = c45, Y = s45, Z = 3, Story = "T1" });
            p.Nodes.Add(new Node { Id = 3, X = 0, Y = R, Z = 3, Restraint = Node.Fixed, Story = "T1" });
            p.Nodes.Add(new Node { Id = 4, X = c45, Y = s45, Z = 0, Restraint = Node.Fixed });
            double a1 = Math.PI / 8, a2 = 3 * Math.PI / 8;
            double sg1 = CurvedBeam.SagittaThrough(R, 0, c45, s45, R * Math.Cos(a1), R * Math.Sin(a1));
            double sg2 = CurvedBeam.SagittaThrough(c45, s45, 0, R, R * Math.Cos(a2), R * Math.Sin(a2));
            p.Frames.Add(new FrameElement { Id = 1, NodeI = 1, NodeJ = 2, Section = "D30x50", Kind = FrameKind.Beam, Story = "T1", DesignGroup = "DC", CurveMidOffset = sg1, CurveSegments = 6 });
            p.Frames.Add(new FrameElement { Id = 2, NodeI = 2, NodeJ = 3, Section = "D30x50", Kind = FrameKind.Beam, Story = "T1", DesignGroup = "DC", CurveMidOffset = sg2, CurveSegments = 6 });
            p.Frames.Add(new FrameElement { Id = 3, NodeI = 4, NodeJ = 2, Section = "C30x30", Kind = FrameKind.Column, Story = "T1" });
            p.FrameLoads.Add(FrameLoad.Uniform(1, "P", 10));
            p.FrameLoads.Add(FrameLoad.Uniform(2, "P", 10));
            if (p.Combinations.Count == 0) ComboGenerator.Generate(p);
            AnalysisResults r = null; DesignSummary ds = null;
            try { r = FrameAnalysis.Run(p); ds = ConcreteDesigner.Design(p, r); }
            catch (Exception ex) { Console.WriteLine("   · lỗi dầm cong chia nút: " + ex.Message); }
            var b = ds?.Beams.FirstOrDefault(x => x.Frames.Contains(1));
            check("Dầm cong chia nút: một dải gồm cả 2 phần tử", B(b != null && b.Frames.Contains(2)), 1, 0);
            check("Dầm cong chia nút: 2 nhịp (gối tại cột giữa)", b?.Spans.Count ?? 0, 2, 0);
            check("Dầm cong chia nút: bán kính chung R = 4 m", b?.CurveRadius ?? 0, R, 1e-3);
            check("Dầm cong chia nút: chiều dài = cung 90°", b?.TotalLength ?? 0, R * Math.PI / 2, 1e-3);
            if (b != null)
            {
                var zs = RebarOptions.Zones(p, b).FirstOrDefault(z => z.Zone == RebarZones.Stirrup && z.Index == 0);
                check("Dầm cong: có vùng chọn đai", B(zs != null), 1, 0);
                if (zs != null)
                {
                    var alts = RebarOptions.Alternatives(p, r, b, zs);
                    var pick = alts.FirstOrDefault(a => !a.IsCurrent) ?? alts.FirstOrDefault();
                    if (pick != null)
                    {
                        RebarOptions.Apply(p, b, zs, pick);
                        var d2 = ConcreteDesigner.Design(p, r);
                        var b2 = d2.Beams.FirstOrDefault(x => x.Frames.Contains(1));
                        check("Dầm cong: thiết kế lại dùng đai chọn (" + pick.Text + ")",
                            B(b2 != null && b2.Spans[0].StirrupDia == pick.Dia && Math.Abs(b2.Spans[0].StirrupSupport - pick.Spacing) < 1e-6 && b2.UserRebarZones >= 1), 1, 0);
                        check("Dầm cong: kiểm tra xoắn với đai chọn", B(b2 != null && b2.Checks.Any(c => c.Name.StartsWith("Xoắn + cắt") && c.Note.Contains("Ø" + pick.Dia))), 1, 0);
                    }
                }
            }
        }
    }
}
