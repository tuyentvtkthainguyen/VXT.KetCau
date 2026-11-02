using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng móng cọc (đài cọc), móng băng và móng đơn trên khung 2×3 nhịp, 3 tầng.
/// check(tên, giá trị, kỳ vọng, sai số tương đối): đạt khi |got − exp| ≤ tol·max(1, |exp|).</summary>
public static class FoundationTests
{
    public static void Run(Action<string, double, double, double> check)
    {
        var p = ModelBuilder.NewProject("Khung thử móng");
        ModelBuilder.SetGrid(p, new[] { 4.0, 4.0 }, new[] { 4.5, 4.5, 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
        {
            ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5
        });
        AnalysisResults r = FrameAnalysis.Run(p);
        int nCols = FoundationUtil.BaseColumns(p).Count;
        Console.WriteLine($"   · khung {nCols} chân cột, {r.DesignCombos().Count} tổ hợp TTGH I, {r.ServiceCombos().Count} tổ hợp TTGH II");
        int Errors(DesignSummary s, string what) => s.Warnings.Count(w => w.StartsWith("LỖI thiết kế " + what));

        // ---------------- (a) móng cọc ----------------
        {
            p.Foundation.Type = FoundationType.Pile;
            p.Foundation.PileCapacity = 450;
            p.Foundation.PileSize = 0.25;
            var s = ConcreteDesigner.Design(p, r);
            check("Móng cọc: không lỗi thiết kế đài", Errors(s, "đài cọc"), 0, 0);
            check("Móng cọc: không còn móng đơn / móng băng", s.Footings.Count + s.StripFootings.Count, 0, 0);
            check("Móng cọc: tổng số đài (kể cả gộp) = số chân cột", s.PileCaps.Sum(c => c.Quantity), nCols, 0);
            check("Móng cọc: mỗi chân cột thuộc đúng một đài", s.PileCaps.SelectMany(c => c.NodeIds).Distinct().Count(), nCols, 0);
            check("Móng cọc: số vị trí đài trên mặt bằng", s.PileCaps.Sum(c => c.Locations.Count), nCols, 0);
            foreach (var c in s.PileCaps)
            {
                Console.WriteLine($"   · {c.Name} ×{c.Quantity}: {c.Piles.Count} cọc, đài {c.L:0.00}×{c.B:0.00}×{c.H:0.00} m, Pmax = {c.PMax:0.0} kN, Pmin = {c.PMin:0.0} kN, thép {c.BarsL} / {c.BarsB}, {(c.Ok ? "đạt" : "KHÔNG ĐẠT")}");
                check($"{c.Name}: Pmax ≤ [P] = 450 kN", c.PMax <= 450 + 1e-6 ? 1 : 0, 1, 0);
                check($"{c.Name}: ΣPi = N + G (tổ hợp {c.Combo})", c.Reactions.Sum(), c.N + c.Weight, 1e-6);
                // cân bằng mô men: Σxi·Pi = −My − H·Qx; Σyi·Pi = Mx − H·Qy
                double sx = 0, sy = 0;
                for (int i = 0; i < c.Piles.Count; i++) { sx += c.Piles[i].X * c.Reactions[i]; sy += c.Piles[i].Y * c.Reactions[i]; }
                if (c.Piles.Select(q => q.X).Distinct().Count() > 1) check($"{c.Name}: Σx·P = −My − H·Qx", sx, -c.My - c.H * c.Qx, 1e-6);
                if (c.Piles.Select(q => q.Y).Distinct().Count() > 1) check($"{c.Name}: Σy·P = Mx − H·Qy", sy, c.Mx - c.H * c.Qy, 1e-6);
                check($"{c.Name}: số hiệu thép hợp lệ", RebarNumbering.Validate(c.Bars).Count, 0, 0);
            }
            check("Móng cọc: tất cả đài đạt", s.PileCaps.Count(c => !c.Ok), 0, 0);
            Outputs(p, r, s, check, "cọc");
        }

        // ---------------- (b) móng băng ----------------
        {
            p.Foundation.Type = FoundationType.Strip;
            p.Foundation.StripDirection = 0;
            p.Soil.ROverride = 150;
            var s = ConcreteDesigner.Design(p, r);
            check("Móng băng: không lỗi thiết kế", Errors(s, "móng băng"), 0, 0);
            check("Móng băng: tìm được dải", s.StripFootings.Count > 0 ? 1 : 0, 1, 0);
            check("Móng băng: tự chọn phương Y (4 cột/trục) → 3 dải", s.StripFootings.Count, 3, 0);
            check("Móng băng: mọi dải chạy dọc Y", s.StripFootings.Count(x => x.AlongX), 0, 0);
            check("Móng băng: tổng số cột trong các dải", s.StripFootings.Sum(x => x.NodeIds.Count), nCols, 0);
            foreach (var m in s.StripFootings)
            {
                double integ = 0;
                for (int i = 0; i + 1 < m.Stations.Count; i++)
                    integ += (m.Pressure[i] + m.Pressure[i + 1]) / 2 * (m.Stations[i + 1] - m.Stations[i]) * m.Width;
                Console.WriteLine($"   · {m.Name} ({m.Grid}): L = {m.Length:0.00} m, B = {m.Width:0.00} m, sườn {m.RibB * 1000:0}×{m.RibH * 1000:0}, ptb = {m.PAvg:0.0}, pmax = {m.PMax:0.0} / 1,2R = {1.2 * m.R:0.0} kN/m², M+ = {m.MPos:0.0}, M− = {m.MNeg:0.0} kN.m, dưới {m.BottomBars}, trên {m.TopBars}, đai {m.Stirrups}, bản {m.FlangeBars}, {(m.Ok ? "đạt" : "KHÔNG ĐẠT")}");
                check($"{m.Name}: Σp·B·dx = ΣN + G", integ, m.SumN + m.SelfWeight, 0.02);
                if (m.Ok) check($"{m.Name}: pmax ≤ 1,2R", m.Pressure.Max() <= 1.2 * m.R + 1e-6 ? 1 : 0, 1, 0);
                check($"{m.Name}: R = ROverride", m.R, 150, 1e-9);
                check($"{m.Name}: mô men đầu dải tự do ≈ 0", m.M[^1], 0, 1e-4);
                check($"{m.Name}: số hiệu thép hợp lệ", RebarNumbering.Validate(m.Bars).Count, 0, 0);
            }
            check("Móng băng: tất cả dải đạt", s.StripFootings.Count(x => !x.Ok), 0, 0);
            Outputs(p, r, s, check, "băng");

            // giao thoa 2 phương: tổng tải cột chia đúng một lần
            p.Foundation.StripDirection = 3;
            var s3 = ConcreteDesigner.Design(p, r);
            check("Móng băng giao thoa: số dải = 4 trục Y + 3 trục X", s3.StripFootings.Count, 7, 0);
            double shares = s3.StripFootings.Sum(x => x.LoadShares.Sum());
            check("Móng băng giao thoa: Σ tỷ lệ chia tải = số cột", shares, nCols, 1e-9);
            p.Foundation.StripDirection = 0;
            p.Soil.ROverride = 0;
        }

        // ---------------- (c) móng đơn – không đổi ----------------
        {
            p.Foundation.Type = FoundationType.Isolated;
            var s = ConcreteDesigner.Design(p, r);
            check("Móng đơn: tổng số móng (kể cả gộp) = số chân cột", s.Footings.Sum(f => f.Quantity), nCols, 0);
            check("Móng đơn: không sinh đài cọc / móng băng", s.PileCaps.Count + s.StripFootings.Count, 0, 0);
            Console.WriteLine($"   · móng đơn: {s.Footings.Count} loại, lớn nhất {s.Footings.Max(f => f.L):0.00}×{s.Footings.Max(f => f.B):0.00} m");

            // chọn riêng một chân cột là móng cọc
            var n0 = FoundationUtil.BaseColumns(p)[0].Node;
            p.Footings.Add(new Footing { NodeId = n0.Id, Label = "MC", Type = FoundationType.Pile });
            var s2 = ConcreteDesigner.Design(p, r);
            check("Hỗn hợp: móng đơn = số chân cột − 1", s2.Footings.Sum(f => f.Quantity), nCols - 1, 0);
            check("Hỗn hợp: 1 đài cọc tại nút chọn riêng", s2.PileCaps.Sum(c => c.Quantity), 1, 0);
            p.Footings.RemoveAll(f => f.Label == "MC" && f.NodeId == n0.Id);
        }
    }

    /// <summary>Chạy thử bản vẽ móng, thống kê thép, thuyết minh và bảng tính để bắt lỗi thời gian chạy.</summary>
    private static void Outputs(Project p, AnalysisResults r, DesignSummary s, Action<string, double, double, double> check, string what)
    {
        var figs = FootingDrawing.BuildAll(p, s);
        int dxf = 0;
        foreach (var f in figs) dxf += f.W.ToDxfString().Length > 0 ? 1 : 0;
        check($"Bản vẽ móng {what}: có hình chi tiết", figs.Count > 1 ? 1 : 0, 1, 0);
        check($"Bản vẽ móng {what}: xuất DXF mọi hình", dxf, figs.Count, 0);
        var rows = RebarSchedule.BuildRows(s);
        check($"Thống kê thép móng {what}: có hàng", rows.Count(x => x.Category == (what == "cọc" ? "Đài cọc" : "Móng băng")) > 0 ? 1 : 0, 1, 0);
        string dir = Path.Combine(Path.GetTempPath(), "kcvxt_foundation_test");
        try
        {
            Directory.CreateDirectory(dir);
            CalculationReport.Export(p, r, s, Path.Combine(dir, "tm_" + (what == "cọc" ? "coc" : "bang") + ".docx"));
            ExcelReport.Export(p, r, s, Path.Combine(dir, "bt_" + (what == "cọc" ? "coc" : "bang") + ".xlsx"));
            check($"Thuyết minh + bảng tính móng {what}", 1, 1, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("   · lỗi xuất báo cáo: " + ex.Message);
            check($"Thuyết minh + bảng tính móng {what}", 0, 1, 0);
        }
    }
}
