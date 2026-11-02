using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Loads;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng lựa chọn bố trí thép (RebarOptions, Project.RebarChoices) và triển khai thép gia công (RebarShop, RebarShopDrawing,
/// RebarShopReport): phương án thoả As + khoảng hở, lựa chọn được bộ thiết kế tôn trọng và lưu JSON, lựa chọn thiếu bị báo,
/// chiều dài nối chồng TCVN 5574:2018, tách thanh 15 m, tối ưu cắt, đai kín móc 135°, số hiệu gia công khớp bảng thống kê, file Excel/DXF.
/// check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class RebarShopTests
{
    private static double B(bool v) => v ? 1 : 0;

    /// <summary>Nhà 1 nhịp 5 m × 3 nhịp 4,5 m (dầm trục Y dài 13,5 m &gt; 11,7 m), 2 tầng.</summary>
    private static Project Sample()
    {
        var p = ModelBuilder.NewProject("Kiểm thử chọn thép & gia công");
        ModelBuilder.SetGrid(p, new[] { 5.0 }, new[] { 4.5, 4.5, 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.9, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
        {
            ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5
        });
        if (p.Combinations.Count == 0) ComboGenerator.Generate(p);
        return p;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Console.WriteLine("RS. Lựa chọn bố trí thép & triển khai thép gia công (shop drawing)");
        string dir = Path.Combine(outDir ?? Path.GetTempPath(), "GiaCongThep");
        Directory.CreateDirectory(dir);

        // ---------------- công thức neo / nối (TCVN 5574:2018 10.3.5.4, 10.3.6.2) ----------------
        var b25 = Material.Concrete("B25");
        var cb400 = Material.Rebar("CB400-V");
        // l0,an = Rs·d/(4·η1·η2·Rbt) = 350·20/(4·2,5·1,0·1,05) = 666,7 mm ; ll = 1,2·l0,an = 800 mm
        check("Neo cơ sở l0,an Ø20 B25 CB400-V = 666,7 mm", RebarDetailing.BasicAnchorage(b25, cb400, 20), 0.66667, 1e-3);
        check("Nối chồng vùng kéo ll Ø20 B25 CB400-V = 800 mm", RebarDetailing.LapLength(b25, cb400, 20), 0.80, 1e-9);
        check("Nối chồng vùng nén Ø20 = 0,9·l0,an → 600 mm", RebarDetailing.LapLength(b25, cb400, 20, tension: false), 0.60, 1e-9);
        check("Nối chồng Ø10 trơn CB240-T: ll ≥ 250 mm, η1 = 1,5", RebarDetailing.LapLength(b25, Material.Rebar("CB240-T"), 10),
            Math.Max(0.25, RcSection.CeilTo(1.2 * 210e3 * 0.010 / (4 * 1.5 * 1.05e3), 0.01)), 1e-9);
        check("Khoảng so le 1,3·ll (ll = 800) = 1040 mm", RebarDetailing.StaggerDistance(0.80), 1.04, 1e-9);

        // ---------------- khoảng hở thép trên 30 mm ----------------
        check("Số thanh Ø22 / hàng, b = 220: thép dưới (khe 25) = 4", RcSection.MaxBarsInLayer(0.22, 0.025, 22), 4, 0);
        check("Số thanh Ø22 / hàng, b = 220: thép trên (khe 30) = 3", RcSection.MaxBarsInLayer(0.22, 0.025, 22, RcSection.ClearTop), 3, 0);

        // ---------------- tách thanh 15 m ----------------
        {
            var cs = RebarShop.SpliceCenters(0, 15.0, 0, 0, 0.80, 11.7, new List<(double, double)>(), out bool forced);
            var ps = RebarShop.PiecesFromCenters(0, 15.0, 0.80, cs);
            check("Thanh thẳng 15 m: 1 mối nối", cs.Count, 1, 0);
            check("Thanh thẳng 15 m: tách 2 đoạn", ps.Count, 2, 0);
            check("Thanh 15 m: tổng chiều dài 2 đoạn = 15 + ll", ps.Sum(x => x.B - x.A), 15.8, 1e-9);
            check("Thanh 15 m: mỗi đoạn ≤ 11,7 m", B(ps.All(x => x.B - x.A <= 11.7 + 1e-9)), 1, 0);
            check("Thanh 15 m: không có mối nối ngoài vùng", B(!forced), 1, 0);
            var sb = RebarShop.StaggerCenters(cs, 0, 15.0, 0, 0, 0.80, 11.7, new List<(double, double)>(), out _);
            check("Thanh 15 m: nhóm so le dời 1,3·ll", sb != null ? Math.Abs(sb[0] - cs[0]) : 0, 1.04, 1e-6);
        }

        // ---------------- tối ưu cắt ----------------
        {
            var pl = RebarShop.Optimize(20, new[] { ("A", 3900.0, 6), ("B", 5850.0, 4) }, 11700, 1000);
            check("Tối ưu cắt: 6×3900 + 4×5850 vừa khít 4 cây 11,7 m", pl.StockCount, 4, 0);
            check("Tối ưu cắt: hao hụt 0", pl.WasteM, 0, 1e-9);
            check("Tối ưu cắt: 2 mẫu cắt", pl.Patterns().Count, 2, 0);
            var p2 = RebarShop.Optimize(16, new[] { ("C", 7000.0, 1), ("D", 3000.0, 1) }, 11700, 1000);
            check("Tối ưu cắt: 7000 + 3000 chung 1 cây (dùng đầu thừa)", p2.StockCount, 1, 0);
            check("Tối ưu cắt: đầu thừa 1,70 m ≥ 1 m → tái sử dụng", p2.ReusableM, 1.70, 1e-9);
            check("Tối ưu cắt: đầu thừa tái sử dụng không tính hao hụt", p2.WasteM, 0, 1e-9);
            var p3 = RebarShop.Optimize(16, new[] { ("E", 11000.0, 2) }, 11700, 1000);
            check("Tối ưu cắt: đầu thừa 700 mm < 1 m là hao hụt (2 × 0,7 m)", p3.WasteM, 1.40, 1e-9);
        }

        // ---------------- đai kín móc 135° ----------------
        check("Đai kín 170×320 Ø6: L = 2(A+B) + 2·max(75; 10d) = 1130 mm", RebarShop.ClosedStirrupLengthMm(170, 320, 6), 1130, 0);
        check("Đai kín 170×320 Ø10: móc 10d = 100 → 1180 mm", RebarShop.ClosedStirrupLengthMm(170, 320, 10), 1180, 0);
        check("Mã hình dạng đai kín = 51", B(RebarShop.ShapeCodeOf("Đai").Code == "51"), 1, 0);
        check("Mã hình dạng đai C = 52, chữ L = 11, chữ U = 21, thẳng = 00",
            B(RebarShop.ShapeCodeOf("Đai C").Code == "52" && RebarShop.ShapeCodeOf("L").Code == "11" && RebarShop.ShapeCodeOf("U").Code == "21"
              && RebarShop.ShapeCodeOf("Thẳng").Code == "00"), 1, 0);

        // ---------------- mô hình mẫu ----------------
        var p = Sample();
        AnalysisResults r;
        DesignSummary ds;
        try
        {
            r = FrameAnalysis.Run(p);
            ds = ConcreteDesigner.Design(p, r);
        }
        catch (Exception ex)
        {
            Console.WriteLine("   · lỗi phân tích/thiết kế mẫu: " + ex.Message);
            check("Mô hình mẫu chọn thép: phân tích + thiết kế", 0, 1, 0);
            return;
        }
        check("Mô hình mẫu: có dầm, cột, sàn", B(ds.Beams.Count > 0 && ds.Columns.Count > 0 && ds.Slabs.Count > 0), 1, 0);
        check("Mọi cấu kiện có khoá lựa chọn thép (RebarKeys)", B(ds.Beams.Cast<MemberDesignBase>().Concat(ds.Columns).Concat(ds.Slabs).Concat(ds.Footings).All(m => m.RebarKeys.Count > 0)), 1, 0);
        check("Số khoá = số cấu kiện vật lý gộp (Quantity)", B(ds.Beams.All(b => b.RebarKeys.Count == b.Quantity)), 1, 0);

        // đai dầm: kích thước theo mép ngoài đai = b − 2(c − Øđai), chiều dài = 2(A+B) + 2 móc
        {
            var bm = ds.Beams.First();
            var st = bm.Bars.First(x => x.Role == "Đai");
            double cst = p.Concrete.CoverBeam * 1000 - st.Dia;
            check("Đai dầm: cạnh A = b − 2(c − Øđ) (đai ôm ngoài thép dọc)", st.Segments[0], Math.Round(bm.B * 1000 - 2 * cst), 0);
            check("Đai dầm: chiều dài = 2(A+B) + 2·max(75; 10d)", st.Length * 1000, RebarShop.ClosedStirrupLengthMm(st.Segments[0], st.Segments[1], st.Dia), 1e-9);
        }

        // ---------------- phương án thép ----------------
        var beam = ds.Beams.Where(b => !b.IsCurved).OrderByDescending(b => b.Spans.Count).ThenByDescending(b => b.Spans.Max(s => s.AsBotReq)).First();
        var zones = RebarOptions.Zones(p, beam);
        check("Dầm: số vùng thép = nhịp + gối + 2 chạy suốt + đai", zones.Count, beam.Spans.Count * 2 + 1 + 2 + beam.Spans.Count, 0);
        int nAlt = 0, badAs = 0, badFit = 0;
        foreach (var z in zones.Where(q => q.Zone != RebarZones.Stirrup))
            foreach (var a in RebarOptions.Alternatives(p, r, beam, z))
            {
                nAlt++;
                if (a.AsProv < z.AsReq * (1 - 1e-6)) badAs++;
                var fit = RebarOptions.CheckFit(a.Bars.Select(g => (g.Dia, g.Count)).ToList(), beam.B, p.Concrete.CoverBeam, z.Top);
                if (!fit.Fits || fit.Layers > 2 || a.ClearMm < a.ClearReqMm - 1e-6 || a.ClearReqMm < (z.Top ? 30 : 25) - 1e-6) badFit++;
            }
        check("Phương án thép dầm: có phương án", B(nAlt > 0), 1, 0);
        check("Phương án thép dầm: mọi phương án As ≥ As yêu cầu", badAs, 0, 0);
        check("Phương án thép dầm: mọi phương án lọt bề rộng, khe hở ≥ max(d; 25/30 mm), ≤ 2 hàng", badFit, 0, 0);
        {
            var zs = zones.First(z => z.Zone == RebarZones.Stirrup);
            var sa = RebarOptions.Alternatives(p, r, beam, zs);
            check("Phương án đai dầm: có phương án, Qu ≥ Q", B(sa.Count > 0 && sa.All(a => a.AsProv >= a.AsReq - 1e-6)), 1, 0);
        }
        {
            var col = ds.Columns.First();
            var cz = RebarOptions.Zones(p, col).First(z => z.Zone == RebarZones.ColumnMain);
            var ca = RebarOptions.Alternatives(p, r, col, cz);
            check("Phương án thép cột: có phương án, hệ số sử dụng ≤ 1, khe hở ≥ max(d; 50)",
                B(ca.Count > 0 && ca.All(a => a.Utilization <= 1 + 1e-6 && a.ClearMm >= a.ClearReqMm - 1e-6)), 1, 0);
            var slab = ds.Slabs.First();
            int badSlab = 0, nSlab = 0;
            foreach (var z in RebarOptions.Zones(p, slab))
                foreach (var a in RebarOptions.Alternatives(p, r, slab, z))
                {
                    nSlab++;
                    if (a.AsProv < a.AsReq * (1 - 1e-6) || a.Spacing < 0.10 - 1e-9) badSlab++;
                }
            check("Phương án thép sàn: có phương án, As ≥ yêu cầu, bước ≥ 100 mm", B(nSlab > 0 && badSlab == 0), 1, 0);
            if (ds.Footings.Count > 0)
            {
                var ft = ds.Footings.First();
                var fa = RebarOptions.Alternatives(p, r, ft, RebarOptions.Zones(p, ft).First());
                check("Phương án thép móng: As ≥ yêu cầu", B(fa.Count > 0 && fa.All(a => a.AsProv >= a.AsReq * (1 - 1e-6))), 1, 0);
            }
        }
        {
            var pref = new RebarPreferences { AllowTwoLayers = false };
            var one = RebarOptions.BeamCombos(beam.Spans.Max(s => s.AsBotReq), beam.B, p.Concrete.CoverBeam, false, new[] { 14, 16, 18, 20, 22 }, pref);
            check("Tuỳ chọn không cho 2 hàng: mọi phương án 1 hàng", B(one.All(a => a.Layers == 1)), 1, 0);
        }

        // ---------------- áp dụng lựa chọn: thiết kế lại tôn trọng ----------------
        string key = beam.RebarKeys[0];
        var zb = zones.First(z => z.Zone == RebarZones.BeamBottom && z.Index == 0);
        var alts = RebarOptions.Alternatives(p, r, beam, zb);
        // phương án khác hiện tại, 1 hàng, thừa ≥ 3% (tránh sai khác nhỏ do h0 thực tế theo Ø)
        var pick = alts.FirstOrDefault(a => !a.IsCurrent && a.Bars.Count == 2 && a.Layers == 1 && a.OverPct >= 3)
                   ?? alts.FirstOrDefault(a => !a.IsCurrent && a.Layers == 1 && a.OverPct >= 3) ?? alts.First();
        int nw = RebarOptions.Apply(p, beam, zb, pick);
        check("Áp dụng lựa chọn: ghi cho mọi dầm gộp", nw, beam.Quantity, 0);
        var ds2 = ConcreteDesigner.Design(p, r);
        var beam2 = ds2.Beams.FirstOrDefault(b => b.RebarKeys.Contains(key));
        check("Lựa chọn được tôn trọng: thép dưới nhịp 1 = phương án chọn (" + pick.Text + ")",
            B(beam2 != null && beam2.Spans[0].BotText == pick.Text), 1, 0);
        check("Lựa chọn được tôn trọng: đếm vùng dùng thép chọn ≥ 1", B(beam2 != null && beam2.UserRebarZones >= 1), 1, 0);
        check("Lựa chọn đủ As: kiểm tra uốn giữa nhịp 1 đạt", B(beam2 != null && beam2.Checks.First(c => c.Name == "Uốn giữa nhịp nhịp 1").Ok), 1, 0);
        check("Số hiệu thép hợp lệ sau khi chọn thép", beam2 != null ? RebarNumbering.Validate(beam2.Bars).Count : 1, 0, 0);
        // cột
        {
            var col = ds.Columns.First();
            var cz = RebarOptions.Zones(p, col).First(z => z.Zone == RebarZones.ColumnMain && z.Index == 0);
            var ca = RebarOptions.Alternatives(p, r, col, cz);
            var cp = ca.LastOrDefault();
            if (cp != null)
            {
                RebarOptions.Apply(p, col, cz, cp);
                var dsc = ConcreteDesigner.Design(p, r);
                var col2 = dsc.Columns.FirstOrDefault(c => c.RebarKeys.Contains(col.RebarKeys[0]));
                check("Lựa chọn thép cột được tôn trọng (" + cp.Text + ")",
                    B(col2 != null && col2.Stories[0].Dia == cp.Dia && col2.Stories[0].BarsAlongB == cp.NB && col2.Stories[0].BarsAlongH == cp.NH), 1, 0);
                RebarOptions.Clear(p, col);
            }
        }

        // ---------------- lưu / mở JSON ----------------
        {
            string json = JsonSerializer.Serialize(p, Project.JsonOptions);
            var q = JsonSerializer.Deserialize<Project>(json, Project.JsonOptions);
            var c0 = p.FindRebarChoice(key, RebarZones.BeamBottom, 0);
            var c1 = q.FindRebarChoice(key, RebarZones.BeamBottom, 0);
            check("JSON: giữ danh sách lựa chọn thép", q.RebarChoices.Count, p.RebarChoices.Count, 0);
            check("JSON: giữ nhóm thanh + cơ sở tiết diện",
                B(c1 != null && c0 != null && c1.Basis == c0.Basis && RcSection.BarText(c1.Bars.Select(g => (g.Dia, g.Count))) == pick.Text), 1, 0);
            var dq = ConcreteDesigner.Design(q, FrameAnalysis.Run(q));
            var bq = dq.Beams.FirstOrDefault(b => b.RebarKeys.Contains(key));
            check("JSON: mở lại → thiết kế lại vẫn dùng thép chọn", B(bq != null && bq.Spans[0].BotText == pick.Text), 1, 0);
            // file cũ không có RebarChoices / RebarPrefs
            var node = JsonNode.Parse(json).AsObject();
            node.Remove("RebarChoices"); node.Remove("RebarPrefs");
            var old = JsonSerializer.Deserialize<Project>(node.ToJsonString(), Project.JsonOptions);
            check("JSON cũ (không có mục chọn thép): danh sách rỗng, tuỳ chọn mặc định",
                B(old.RebarChoices != null && old.RebarChoices.Count == 0 && old.RebarPrefs != null && Math.Abs(old.RebarPrefs.StockLength - 11.7) < 1e-9), 1, 0);
            // đổi tiết diện → lựa chọn hết hiệu lực
            check("Lựa chọn hết hiệu lực khi đổi kích thước tiết diện", B(p.ValidRebarChoice(key, RebarZones.BeamBottom, 0, "300x600") == null), 1, 0);
        }

        // ---------------- lựa chọn thiếu → không đạt ----------------
        {
            var q = p.Clone();
            var zq = RebarOptions.Zones(q, beam).First(z => z.Zone == RebarZones.BeamBottom && z.Index == 0);
            double req = beam.Spans[0].AsBotReq;
            var weak = new RebarAlternative { Kind = "Dầm", Zone = zq.Zone, Index = 0, Bars = new List<RebarGroup> { new(10, 2) }, Text = "2Ø10" };
            RebarOptions.Apply(q, beam, zq, weak);
            var dw = ConcreteDesigner.Design(q, r);
            var bw = dw.Beams.FirstOrDefault(b => b.RebarKeys.Contains(key));
            bool expectFail = req > 2 * RcSection.BarArea(10) * 1.05;
            check("Lựa chọn thiếu thép (2Ø10): báo KHÔNG ĐẠT", B(bw != null && (!expectFail || (!bw.Ok && bw.Messages.Any(m => m.Contains("người dùng chọn không đạt"))))), 1, 0);
            check("Lựa chọn thiếu thép: thép dưới vẫn theo lựa chọn", B(bw != null && bw.Spans[0].BotText == "2Ø10"), 1, 0);
            // sàn: Ø6a200 cho mũ X thường thiếu → báo
            var sl = dw.Slabs.FirstOrDefault(s => !string.IsNullOrEmpty(s.TopX));
            if (sl != null)
            {
                var sz = RebarOptions.Zones(q, sl).First(z => z.Zone == RebarZones.SlabTopX);
                RebarOptions.Apply(q, sl, sz, new RebarAlternative { Kind = "Sàn", Zone = sz.Zone, Dia = 6, Spacing = 0.20, Text = "Ø6a200" });
                var d3 = ConcreteDesigner.Design(q, r);
                var s3 = d3.Slabs.FirstOrDefault(s => s.RebarKeys.Contains(sl.RebarKeys[0]));
                bool weakOk = s3 != null && s3.TopX == "Ø6a200";
                bool flagged = s3 != null && (s3.Checks.First(c => c.Name == "Uốn gối phương ngắn").Ok || !s3.Ok);
                check("Sàn: lựa chọn Ø6a200 được dùng và kiểm tra lại (thiếu → không đạt)", B(weakOk && flagged), 1, 0);
            }
        }

        // ---------------- gia công thép ----------------
        var dsS = ConcreteDesigner.Design(p, r);
        var shop = RebarShop.Build(p, dsS);
        check("Gia công: có dòng gia công", B(shop.Bars.Count > 0), 1, 0);
        check("Gia công: mọi đoạn cắt ≤ cây 11,7 m", B(shop.Bars.All(b => b.CutLengthMm <= 11700 + 1e-6)), 1, 0);
        check("Gia công: có thanh dầm 13,5 m được tách nối chồng", B(shop.Bars.Any(b => b.Category == "Dầm" && b.Spliced)), 1, 0);
        {
            var rows = RebarSchedule.BuildRows(dsS);
            check("Bảng thống kê: mỗi số hiệu một hàng trong mỗi cấu kiện",
                rows.GroupBy(x => x.Category + "|" + x.Member + "|" + x.Mark).Count(g => g.Count() > 1), 0, 0);
            int missing = 0, mism = 0;
            foreach (var row in rows)
            {
                var sb = shop.Bars.Where(b => b.Category == row.Category && b.Member == row.Member && b.ParentMark == row.Mark).ToList();
                if (sb.Count == 0) { missing++; continue; }
                if (sb.Count == 1 && !sb[0].Spliced)
                {
                    if (sb[0].Label != row.Mark.ToString() || sb[0].Dia != row.Dia || Math.Abs(sb[0].CutLengthMm - row.LengthMm) > 1e-6
                        || sb[0].BarsTotal != row.BarsTotal) mism++;
                }
                else if (sb.Any(b => !b.Label.StartsWith(row.Mark.ToString()) || b.Dia != row.Dia)) mism++;
            }
            check("Số hiệu gia công khớp bảng thống kê: mọi số hiệu có trong bảng gia công", missing, 0, 0);
            check("Số hiệu gia công khớp bảng thống kê: Ø, chiều dài, số thanh trùng (thanh không nối)", mism, 0, 0);
            // thanh tách nối: tổng chiều dài ≥ thống kê (cộng đoạn nối)
            var spl = rows.FirstOrDefault(x => shop.Bars.Any(b => b.Member == x.Member && b.ParentMark == x.Mark && b.Spliced));
            if (spl != null)
            {
                double tot = shop.Bars.Where(b => b.Member == spl.Member && b.ParentMark == spl.Mark && b.Category == spl.Category).Sum(b => b.TotalLengthM);
                check("Thanh tách nối: tổng chiều dài gia công > thống kê (cộng chiều dài nối)", B(tot > spl.TotalLengthM + 0.1), 1, 0);
                var parts = shop.Bars.Where(b => b.Member == spl.Member && b.ParentMark == spl.Mark && b.Category == spl.Category).ToList();
                check("Thanh tách nối: số đoạn × thanh ≥ 2 × số thanh gốc", B(parts.Sum(b => b.BarsPerMember) >= 2 * spl.BarsPerMember), 1, 0);
                check("Thanh tách nối: hậu tố a, b…", B(parts.All(b => b.Label.Length > spl.Mark.ToString().Length)), 1, 0);
            }
            double need = shop.Bars.Sum(b => b.TotalLengthM), plan = shop.Plans.Sum(x => x.PieceLengthM);
            check("Phương án cắt: tổng chiều dài cắt = tổng gia công", plan, need, 1e-3);
        }
        // thanh 15 m trong ô sàn giả định: tách 2 đoạn + so le
        {
            var fake = new DesignSummary();
            var sd = new SlabDesign { Name = "S-test", Story = "T1", SlabId = -1, Quantity = 1 };
            sd.Bars.Add(new BarMark { Mark = 1, Role = "Lớp dưới phương X", Dia = 12, Count = 4, Length = 15.0, Shape = "Thẳng", Segments = new List<double> { 15000 } });
            fake.Slabs.Add(sd);
            var sh = RebarShop.Build(p, fake, new ShopOptions { StockLength = 11.7, Stagger = true });
            var bars = sh.Bars.Where(x => x.ParentMark == 1).ToList();
            double lap = RebarDetailing.LapLength(DesignUtilProxy.Conc(p), Material.Rebar(p.Concrete.RebarMain), 12) * 1000;
            check("Thanh 15 m (4Ø12): mỗi thanh thành 2 đoạn", bars.Sum(x => x.BarsPerMember), 8, 0);
            check("Thanh 15 m: tổng chiều dài = 4 × (15000 + ll)", bars.Sum(x => x.CutLengthMm * x.BarsPerMember), 4 * (15000 + lap), 0.002);
            check("Thanh 15 m: so le → 4 loại đoạn (1a…1d)", bars.Count, 4, 0);
            check("Thanh 15 m: mọi đoạn ≤ 11,7 m", B(bars.All(x => x.CutLengthMm <= 11700)), 1, 0);
        }

        // ---------------- file Excel / DXF / tờ in ----------------
        {
            string xlsx = Path.Combine(dir, "GiaCongThep.xlsx");
            try
            {
                RebarShopReport.Export(p, dsS, xlsx, shop);
                bool sheet = false;
                using (var z = ZipFile.OpenRead(xlsx))
                {
                    var e = z.GetEntry("xl/workbook.xml");
                    if (e != null) using (var srd = new StreamReader(e.Open())) sheet = srd.ReadToEnd().Contains("Gia công thép");
                }
                check("Excel \"Gia công thép\": file tạo được và có trang tính", B(File.Exists(xlsx) && new FileInfo(xlsx).Length > 0 && sheet), 1, 0);
            }
            catch (Exception ex) { Console.WriteLine("   · lỗi Excel gia công: " + ex.Message); check("Excel \"Gia công thép\": file tạo được và có trang tính", 0, 1, 0); }

            List<string> files = new();
            try { files = RebarShopDrawing.ExportAll(p, dsS, dir); }
            catch (Exception ex) { Console.WriteLine("   · lỗi DXF gia công: " + ex.Message); }
            check("DXF gia công: có bản vẽ dầm, cột, sàn + phương án cắt", B(files.Count >= 4 && files.All(f => File.Exists(f) && new FileInfo(f).Length > 1000)), 1, 0);
            var damFile = files.FirstOrDefault(f => f.Contains("GiaCongThep_Dam"));
            if (damFile != null)
            {
                var lines = new HashSet<string>(File.ReadAllLines(damFile).Select(x => x.Trim()));
                var dam = shop.Bars.Where(b => b.Category == "Dầm").ToList();
                check("DXF gia công dầm: có mọi số hiệu gia công", dam.Count(b => !lines.Contains(b.Label)), 0, 0);
                check("DXF gia công dầm: có thực thể TEXT, CIRCLE (vòng số hiệu), LINE", B(lines.Contains("TEXT") && lines.Contains("CIRCLE") && lines.Contains("LINE")), 1, 0);
            }
            var figs = SheetBuilder.Figures(p, r, dsS, new SheetView { Kind = SheetViewKind.RebarShop, Param = "Dầm" });
            check("Tờ in: khung nhìn Gia công thép có hình", B(figs.Count >= 2 && figs.All(f => f.Name != "placeholder")), 1, 0);
            check("SheetViewKind.RebarShop thêm cuối enum", (int)SheetViewKind.RebarShop, Enum.GetValues<SheetViewKind>().Max(k => (int)k), 0);
        }

        // ---------------- tuỳ chọn Ø ưu tiên ----------------
        {
            var q = Sample();
            q.RebarPrefs.PreferredDias = new[] { 16, 20 };
            var rq = FrameAnalysis.Run(q);
            var dq = ConcreteDesigner.Design(q, rq);
            bool okD = dq.Beams.All(b => b.Bars.Where(x => !DxfWriterIsStirrup(x) && x.Role != "Giá").All(x => x.Dia == 16 || x.Dia == 20));
            check("Tuỳ chọn Ø ưu tiên {16; 20}: thép dọc dầm chỉ dùng Ø16/Ø20", B(okD), 1, 0);
        }
    }

    private static bool DxfWriterIsStirrup(BarMark b) => (b.Shape ?? "").StartsWith("Đai") || (b.Role ?? "").StartsWith("Đai");

    /// <summary>Bê tông dầm/sàn mặc định của dự án (DesignUtil là internal).</summary>
    private static class DesignUtilProxy
    {
        public static Material Conc(Project p) => p.Material(p.Concrete.ConcreteSlab);
    }
}
