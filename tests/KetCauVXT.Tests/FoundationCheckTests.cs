using System.Text.Json;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng bố trí cọc do người dùng chọn, kiểm tra an toàn đài cọc (phản lực, nhổ, nhóm, bố trí, chọc thủng),
/// sự làm việc móng băng (dầm trên nền Winkler) và móng đơn (lệch tâm, tách nền).
/// check(tên, giá trị, kỳ vọng, sai số tương đối): đạt khi |got − exp| ≤ tol·max(1, |exp|).</summary>
public static class FoundationCheckTests
{
    private static double B(bool x) => x ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        PileReactions(check);
        Geometry(check);
        Punching(check);
        Winkler(check);
        Isolated(check);
        Designs(check, outDir);
    }

    // ------------------------------------------------------------------
    private static void PileReactions(Action<string, double, double, double> check)
    {
        // đài 4 cọc s = 0,9 m: Pi = (N + G)/n + (−My − Qx·H)·xi/Σx² + (Mx − Qy·H)·yi/Σy²
        var piles = PileLayout.Generate(PilePattern.P4, 0.9, 0, 0, 0, true);
        check("4 cọc: số cọc", piles.Count, 4, 0);
        var l = new BaseLoad { Combo = "T", N = 1000, Mx = 100, My = 80, Qx = 20, Qy = -10 };
        double G = 100, H = 0.8;
        var P = PileCapDesigner.PileForces(l, piles, G, H);
        double sx2 = 4 * 0.45 * 0.45, sy2 = sx2;
        for (int i = 0; i < 4; i++)
        {
            double exp = (l.N + G) / 4 + (-l.My - H * l.Qx) * piles[i].X / sx2 + (l.Mx - H * l.Qy) * piles[i].Y / sy2;
            check($"4 cọc: P{i + 1} theo công thức tay ({piles[i].X:0.00}; {piles[i].Y:0.00})", P[i], exp, 1e-9);
        }
        // giá trị số: cọc (−0,45; +0,45): 275 + 96·0,45/0,81 + 108·0,45/0,81 = 275 + 53,33 + 60 = 388,33
        int k = Enumerable.Range(0, 4).First(i => piles[i].X < 0 && piles[i].Y > 0);
        check("4 cọc: Pmax = 388,33 kN (tính tay)", P[k], 275 + 96 * 0.45 / 0.81 + 108 * 0.45 / 0.81, 1e-9);

        var inp = new PileCapInput
        {
            Piles = piles, Loads = new List<BaseLoad> { l }, G = G, H = H, H0 = 0.65, Hc = 0.3, Bc = 0.3, D = 0.3, L = 1.8, B = 1.8, P = 450, Rbt = 1050
        };
        var rep = PileCapChecks.Evaluate(inp);
        check("4 cọc: bảng phản lực 4 hàng", rep.Piles.Count, 4, 0);
        check("4 cọc: hạng mục Pmax = max Pi", rep.Items.First(i => i.Name.StartsWith("Phản lực lớn nhất")).Value, P.Max(), 1e-9);
        check("4 cọc: Pmax ≤ [P] → ĐẠT", B(rep.Items.First(i => i.Name.StartsWith("Phản lực lớn nhất")).Status == CheckStatus.Ok), 1, 0);
        check("4 cọc: không có cọc nhổ", B(rep.Items.Any(i => i.Name.StartsWith("Phản lực nhỏ nhất") && i.Status == CheckStatus.Ok)), 1, 0);
        check("4 cọc: hiệu ứng nhóm η Converse–Labarre (s = 3d)", rep.GroupEta, 1 - Math.Atan(1.0 / 3) * 180 / Math.PI * (1 * 2 + 1 * 2) / (90.0 * 4), 1e-9);
        check("4 cọc: chưa có hố khoan → cảnh báo lún", B(rep.Warnings.Any(w => w.Contains("chưa có số liệu địa chất"))), 1, 0);

        // cọc chịu nhổ: 2 cọc, N nhỏ, My lớn
        var p2 = PileLayout.Generate(PilePattern.P2, 0.9, 0, 0, 0, true);
        var lt = new BaseLoad { Combo = "G", N = 200, My = 400 };
        var inp2 = new PileCapInput { Piles = p2, Loads = new List<BaseLoad> { lt }, G = 50, H = 0.6, H0 = 0.45, L = 1.5, B = 0.6, D = 0.3, P = 450, Rbt = 1050 };
        var r2 = PileCapChecks.Evaluate(inp2);
        double pminExp = 125 - 400 * 0.45 / (2 * 0.45 * 0.45);
        check("Nhổ: Pmin = (N+G)/2 − My·x/Σx²", r2.Piles.Min(r => r.PMin), pminExp, 1e-9);
        var tens = r2.Items.First(i => i.Name.StartsWith("Cọc chịu nhổ"));
        check("Nhổ: [Pk] = 0 → KHÔNG ĐẠT", B(tens.Status == CheckStatus.Fail), 1, 0);
        inp2.Pt = 400;
        var tens2 = PileCapChecks.Evaluate(inp2).Items.First(i => i.Name.StartsWith("Cọc chịu nhổ"));
        check("Nhổ: |Pmin| ≤ [Pk] → CẢNH BÁO (neo thép, mối nối chịu kéo)", B(tens2.Status == CheckStatus.Warning), 1, 0);

        // nhóm cọc lệch tâm (toạ độ tuỳ chọn): cân bằng lực và mô men đối với tim cột
        var cust = PileLayout.Generate(PilePattern.Custom, 0, 0, 0, 0, true,
            new List<PilePoint> { new(-0.45, -0.3), new(0.45, -0.3), new(0.45, 0.6) });
        var le = new BaseLoad { Combo = "E", N = 800, Mx = 30, My = -50, Qx = 10, Qy = 5 };
        var Pe = PileCapDesigner.PileForces(le, cust, 60, 0.7);
        check("Lệch tâm: ΣP = N + G", Pe.Sum(), 860, 1e-9);
        check("Lệch tâm: Σx·P = −My − H·Qx", Enumerable.Range(0, 3).Sum(i => cust[i].X * Pe[i]), 50 - 0.7 * 10, 1e-9);
        check("Lệch tâm: Σy·P = Mx − H·Qy", Enumerable.Range(0, 3).Sum(i => cust[i].Y * Pe[i]), 30 - 0.7 * 5, 1e-9);
        var ecc = PileLayout.CheckRules(cust, 0.3, 1.6, 1.8).First(i => i.Name.StartsWith("Lệch tâm"));
        check("Lệch tâm: cảnh báo trọng tâm nhóm cọc lệch tim cột", B(ecc.Status == CheckStatus.Warning), 1, 0);
    }

    // ------------------------------------------------------------------
    private static void Geometry(Action<string, double, double, double> check)
    {
        double s = 0.9;
        var t = PileLayout.Generate(PilePattern.P3, s, 0, 0, 0, true);
        check("3 cọc tam giác: 3 cọc", t.Count, 3, 0);
        double dMin = double.MaxValue, dMax = 0;
        for (int i = 0; i < 3; i++)
            for (int j = i + 1; j < 3; j++)
            {
                double dd = Math.Sqrt(Math.Pow(t[i].X - t[j].X, 2) + Math.Pow(t[i].Y - t[j].Y, 2));
                dMin = Math.Min(dMin, dd); dMax = Math.Max(dMax, dd);
            }
        check("3 cọc tam giác: cạnh nhỏ nhất = s", dMin, s, 1e-6);
        check("3 cọc tam giác: cạnh lớn nhất = s (tam giác đều)", dMax, s, 1e-6);
        check("3 cọc tam giác: trọng tâm X tại tim cột", t.Average(q => q.X), 0, 1e-9);
        check("3 cọc tam giác: trọng tâm Y tại tim cột", t.Average(q => q.Y), 0, 1e-9);
        var (L3, B3) = PileLayout.CapSize(t, 0.3, 0.15, 0.3, 0.3);
        check("3 cọc tam giác: đài đủ mép cọc – mép đài ≥ 150 mm", B(PileLayout.MinEdgeDistance(t, L3, B3) - 0.15 >= 0.15 - 1e-9), 1, 0);

        var h7 = PileLayout.Generate(PilePattern.P7, s, 0, 0, 0, true);
        check("7 cọc lục giác: 7 cọc, khoảng cách nhỏ nhất = s", PileLayout.MinSpacing(h7), s, 1e-6);
        check("Lưới 3×2: số cọc", PileLayout.Generate(PilePattern.Grid, 0.9, 1.0, 3, 2, true).Count, 6, 0);
        check("Lưới 3×2: bước Y = 1,0 m", PileLayout.Generate(PilePattern.Grid, 0.9, 1.0, 3, 2, true).Max(q => q.Y) * 2, 1.0, 1e-9);
        foreach (var pat in new[] { PilePattern.P1, PilePattern.P2, PilePattern.P4, PilePattern.P5, PilePattern.P6, PilePattern.P8, PilePattern.P9 })
            check($"Sơ đồ {PileLayout.Name(pat)}: số cọc", PileLayout.Generate(pat, s, 0, 0, 0, true).Count, (int)pat, 0);

        // quy định cấu tạo: khoảng cách 2d → cảnh báo (chỉ cọc chống), 1,2d → không đạt; mép đài quá sát → không đạt
        double d = 0.25;
        var g2 = PileLayout.Generate(PilePattern.Grid, 2 * d, 2 * d, 2, 2, true);
        var r2 = PileLayout.CheckRules(g2, d, 1.2, 1.2).First(i => i.Name.StartsWith("Khoảng cách tim cọc"));
        check("Quy định: s = 2d → CẢNH BÁO", B(r2.Status == CheckStatus.Warning), 1, 0);
        var g1 = PileLayout.Generate(PilePattern.Grid, 1.2 * d, 1.2 * d, 2, 2, true);
        check("Quy định: s = 1,2d → KHÔNG ĐẠT", B(PileLayout.CheckRules(g1, d, 1.2, 1.2).First(i => i.Name.StartsWith("Khoảng cách tim cọc")).Status == CheckStatus.Fail), 1, 0);
        var g3 = PileLayout.Generate(PilePattern.Grid, 3 * d, 3 * d, 2, 2, true);
        check("Quy định: s = 3d → ĐẠT", B(PileLayout.CheckRules(g3, d, 1.6, 1.6).First(i => i.Name.StartsWith("Khoảng cách tim cọc")).Status == CheckStatus.Ok), 1, 0);
        var edge = PileLayout.CheckRules(g3, d, 1.0, 1.0).First(i => i.Name.StartsWith("Mép cọc"));
        check("Quy định: mép cọc cách mép đài 0,5 − 0,375 − 0,125 = 0 → KHÔNG ĐẠT", B(edge.Status == CheckStatus.Fail), 1, 0);
        check("Quy định: khoảng cách mép = 0 mm", edge.Value, 0, 1e-9);
        check("Nhóm cọc: s ≥ 6d → η = 1", PileLayout.GroupEfficiency(2, 2, 6 * d, d), 1, 0);
    }

    // ------------------------------------------------------------------
    private static void Punching(Action<string, double, double, double> check)
    {
        // mọi cọc trong tháp chọc thủng 45°: cột 400×400, h0 = 0,8 m, cọc ±0,375 → |x| ≤ 0,2 + 0,8
        var piles = PileLayout.Generate(PilePattern.P4, 0.75, 0, 0, 0, true);
        var load = new BaseLoad { Combo = "U", N = 1500 };
        var inp = new PileCapInput
        {
            Piles = piles, Loads = new List<BaseLoad> { load }, G = 60, H = 0.95, H0 = 0.8, Hc = 0.4, Bc = 0.4, D = 0.25, L = 1.4, B = 1.4, P = 450, Rbt = 1050
        };
        var rep = PileCapChecks.Evaluate(inp);
        check("Chọc thủng: mọi cọc trong tháp → AllInside", B(rep.Punching.AllInside), 1, 0);
        var pi = rep.Items.First(i => i.Group == "Chọc thủng" && i.Name.Contains("mọi cọc trong tháp"));
        check("Chọc thủng: mọi cọc trong tháp → tự động an toàn (F = 0, ĐẠT)", B(pi.Status == CheckStatus.Ok && pi.Value == 0), 1, 0);
        check("Chọc thủng: mọi cọc đánh dấu trong tháp", rep.Piles.Count(r => r.InsidePyramid), 4, 0);

        // công thức Fb,u với c = h0 (k = 1) trùng công thức bản: Rbt·2(hc + bc + 2h0)·h0
        var far = PileLayout.Generate(PilePattern.P4, 3.0, 0, 0, 0, true);
        double fb = PileCapChecks.ColumnPunchCapacity(far, 0.4, 0.4, 0.5, 0.3, 1050, out _, out _, out double k1, out double k2);
        check("Chọc thủng: cọc xa (c > h0) → k = 1", k1 + k2, 2, 1e-12);
        check("Chọc thủng: Fb,u = Rbt·2(hc + bc + 2h0)·h0 khi k = 1", fb, 1050 * 2 * (0.4 + 0.4 + 1.0) * 0.5, 1e-9);
        // cọc lớn d = 0,8 m, tim ±0,75 (ngoài tháp: 0,75 > 0,2 + 0,5): mép trong cọc cách mép cột 0,15 m < 0,4h0 → c = 0,4h0, k = 2,5
        var near = PileLayout.Generate(PilePattern.P4, 1.5, 0, 0, 0, true);
        PileCapChecks.ColumnPunchCapacity(near, 0.4, 0.4, 0.5, 0.8, 1050, out double c1, out _, out double kk, out _);
        check("Chọc thủng: c < 0,4h0 → c = 0,4h0", c1, 0.2, 1e-9);
        check("Chọc thủng: c < 0,4h0 → k = 2,5", kk, 2.5, 1e-9);
        // chu vi chọc thủng cọc góc: u = 2·(a_mép + d + h0/2)
        double uc = PileCapChecks.PilePerimeter(0.6, 0.6, 0.3, 0.5, 1.8, 1.8);
        check("Chọc thủng cọc góc: u = 2(mép + d + h0/2)", uc, 2 * ((0.9 - 0.6 - 0.15) + 0.3 + 0.25), 1e-9);
        check("Chọc thủng cọc giữa: u = 4(d + h0)", PileCapChecks.PilePerimeter(0, 0, 0.3, 0.5, 5, 5), 4 * 0.8, 1e-9);

        // chọc thủng không đạt → tự tăng chiều cao
        var p4 = PileLayout.Generate(PilePattern.P4, 0.9, 0, 0, 0, true);
        var big = new PileCapInput
        {
            Piles = p4, Loads = new List<BaseLoad> { new() { Combo = "U", N = 1600 } }, G = 80, H = 0.40, H0 = 0.25, Hc = 0.3, Bc = 0.3, D = 0.3,
            L = 1.8, B = 1.8, P = 450, Rbt = 1050, HStart = 0.40
        };
        var rb = PileCapChecks.Evaluate(big);
        check("Chọc thủng: H = 400 mm không đạt", B(rb.Items.Any(i => i.Group == "Chọc thủng" && i.Name.StartsWith("Chọc thủng đài do cột") && i.Status == CheckStatus.Fail)), 1, 0);
        double hReq = PileCapChecks.RequiredHeight(big);
        Console.WriteLine($"   · chọc thủng: H yêu cầu = {hReq * 1000:0} mm (từ 400 mm)");
        check("Chọc thủng: chiều cao yêu cầu > 400 mm", B(hReq > 0.40 + 1e-9), 1, 0);
        big.H = hReq; big.H0 = hReq - 0.15;
        var rok = PileCapChecks.Evaluate(big);
        check("Chọc thủng: với H yêu cầu → không còn hạng mục chọc thủng / cắt không đạt",
            rok.Items.Count(i => (i.Group == "Chọc thủng" || i.Group == "Cắt") && i.Status == CheckStatus.Fail), 0, 0);
        check("Chọc thủng: báo cáo việc tăng chiều cao", B(rok.Behaviour.Contains("tự tăng")), 1, 0);
        // chiều cao cố định: cốt thép chống chọc thủng (Fb,u < F ≤ 2Fb,u) hoặc yêu cầu tăng chiều cao (F > 2Fb,u)
        var fix = new PileCapInput
        {
            Piles = p4, Loads = new List<BaseLoad> { new() { Combo = "U", N = 1600 } }, G = 80, H = 0.40, H0 = 0.25, Hc = 0.3, Bc = 0.3, D = 0.3,
            L = 1.8, B = 1.8, P = 450, Rbt = 1050, FixedHeight = true
        };
        var rf = PileCapChecks.Evaluate(fix);
        bool handled = rf.Punching.NeedHeight ? rf.Warnings.Any(w => w.Contains("cần tăng chiều cao đài")) : rf.Punching.AswPerM > 0 && rf.Punching.Reinforcement.Length > 0;
        check("Chọc thủng chiều cao cố định: đề xuất cốt thép chống chọc thủng hoặc yêu cầu tăng chiều cao", B(handled), 1, 0);
        var pd = new PunchingDetail { Fbu = 1000, U = 3.0, H0 = 0.6 };
        PileCapChecks.PunchReinforcement(pd, 1400, 170000);
        check("Cốt thép chống chọc thủng: Asw/sw = (F − Fb,u)/(0,8·u·Rsw)", pd.AswPerM, 400 / (0.8 * 3.0) / 170000, 1e-9);
        check("Cốt thép chống chọc thủng: Fb,u + Fsw,u ≥ F", B(pd.Fbu + pd.Fswu >= 1400 - 1e-6), 1, 0);
    }

    // ------------------------------------------------------------------
    private static void Winkler(Action<string, double, double, double> check)
    {
        // dải ngắn rất cứng chịu tải phân bố đều → áp lực nền đều q/B
        {
            var xs = Enumerable.Range(0, 9).Select(i => i * 0.25).ToList();
            double k = 20000, Bw = 1.2, EI = 1e9, q = 120;
            var beam = new StripFootingDesigner.WinklerBeam(xs, EI, k, Bw);
            var fz = beam.Trib.Select(t => -q * t).ToArray();
            var sol = beam.Solve(fz, new double[xs.Count]);
            check("Winkler dải cứng: áp lực lớn nhất = q/B", sol.Pressure.Max(), q / Bw, 1e-4);
            check("Winkler dải cứng: áp lực nhỏ nhất = q/B", sol.Pressure.Min(), q / Bw, 1e-4);
            double lam = FootingBehaviour.Lambda(k, Bw, EI);
            check("Winkler dải cứng: λL ≤ π/4 → dầm cứng", FootingBehaviour.StripClass(lam * 2.0), 0, 0);
        }
        // dải dài mềm chịu lực tập trung giữa dải → nghiệm dầm vô hạn M0 = P/(4λ), w0 = Pλ/(2kB)
        {
            int n = 161;
            var xs = Enumerable.Range(0, n).Select(i => i * 0.25).ToList();
            double k = 20000, Bw = 1.0, EI = 2e5, P = 100;
            var beam = new StripFootingDesigner.WinklerBeam(xs, EI, k, Bw);
            var fz = new double[n]; fz[80] = -P;
            var sol = beam.Solve(fz, new double[n]);
            double lam = FootingBehaviour.Lambda(k, Bw, EI);
            Console.WriteLine($"   · Winkler: λ = {lam:0.0000} 1/m, M(0) = {sol.M[80]:0.00} / P/4λ = {P / (4 * lam):0.00} kN.m, w0 = {-sol.W[80] * 1000:0.000} / {FootingBehaviour.InfiniteBeamDeflection(P, lam, k, Bw) * 1000:0.000} mm");
            check("Winkler dải dài: λL ≥ π → dầm dài / mềm", FootingBehaviour.StripClass(lam * 40), 2, 0);
            check("Winkler dải dài: M dưới lực = P/(4λ) (±3%)", sol.M[80], FootingBehaviour.InfiniteBeamMoment(P, lam), 0.03);
            check("Winkler dải dài: lún dưới lực = Pλ/(2kB) (±3%)", -sol.W[80], FootingBehaviour.InfiniteBeamDeflection(P, lam, k, Bw), 0.03);
            check("Winkler dải dài: mô men đổi dấu tại x = 3π/(4λ) (âm)", B(sol.M[80 + (int)Math.Round(3 * Math.PI / (4 * lam) / 0.25)] < 0), 1, 0);
            check("Winkler dải dài: ΣR lò xo = P", Enumerable.Range(0, n).Sum(i => sol.Pressure[i] * beam.Trib[i] * Bw), P, 1e-6);
        }
    }

    // ------------------------------------------------------------------
    private static void Isolated(Action<string, double, double, double> check)
    {
        check("Tách nền: pmax = 2N/(3B(L/2 − e))", FootingBehaviour.PartialContactPmax(600, 0.5, 2.0, 1.5), 2 * 600 / (3 * 1.5 * 0.5), 1e-12);
        var soil = new SoilSettings { Depth = 1.5, GammaAvg = 22, LoadFactorAvg = 1.15, Phi = 16, C = 25 };
        double ntot = 600 + 22 * 1.5 * 2.0 * 1.5;
        var f = new FootingDesign
        {
            Name = "M", L = 2.0, B = 1.5, H = 0.5, ColumnH = 0.3, ColumnB = 0.3, N = 690, Ntc = 600, My = 0.5 * ntot * 1.15, R = 200, Combo = "T", DiaL = 12, DiaB = 12
        };
        var conc = Material.Concrete("B20");
        var rep = FootingBehaviour.AnalyzeIsolated(f, soil, conc, 0.04);
        var kern = rep.Items.First(i => i.Name.StartsWith("Lệch tâm trong lõi"));
        check("Móng đơn e > L/6: cảnh báo lệch tâm ngoài lõi", B(kern.Status == CheckStatus.Warning), 1, 0);
        check("Móng đơn e > L/6: eL/L = 0,25", kern.Value, 0.25, 1e-9);
        var pc = rep.Items.First(i => i.Name.StartsWith("Tách nền"));
        check("Móng đơn e > L/6: pmax tiếp xúc một phần", pc.Value, FootingBehaviour.PartialContactPmax(ntot, 0.5, 2.0, 1.5), 1e-9);
        check("Móng đơn e > L/6: pmax > 1,2R → KHÔNG ĐẠT", B(pc.Status == CheckStatus.Fail), 1, 0);
        check("Móng đơn: vùng tiếp xúc 3c0/L = 75%", rep.Items.First(i => i.Name.StartsWith("Vùng tiếp xúc")).Value, 75, 1e-9);
        check("Móng đơn: phân loại móng cứng (c = 0,85 ≤ 2,5H)", B(rep.Classification == "Móng cứng"), 1, 0);
        check("Móng đơn: có giải thích sự làm việc", B(rep.Behaviour.Contains("công xôn")), 1, 0);
        // móng mềm: c/H > 2,5
        var f2 = new FootingDesign { L = 3.0, B = 3.0, H = 0.4, ColumnH = 0.3, ColumnB = 0.3, N = 900, Ntc = 780, R = 200, DiaL = 12, DiaB = 12 };
        var r2 = FootingBehaviour.AnalyzeIsolated(f2, soil, conc, 0.04);
        check("Móng đơn mềm: c = 1,35 > 2,5H = 1,0 → cảnh báo", B(r2.Items.First(i => i.Name.StartsWith("Móng cứng")).Status == CheckStatus.Warning), 1, 0);
    }

    // ------------------------------------------------------------------
    private static Project Frame()
    {
        var p = ModelBuilder.NewProject("Khung thử kiểm tra móng");
        ModelBuilder.SetGrid(p, new[] { 4.0, 4.0 }, new[] { 4.5, 4.5, 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
        {
            ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5
        });
        return p;
    }

    private static void Designs(Action<string, double, double, double> check, string outDir)
    {
        var p = Frame();
        AnalysisResults r = FrameAnalysis.Run(p);
        p.Foundation.Type = FoundationType.Pile;
        p.Foundation.PileCapacity = 450;
        p.Foundation.PileSize = 0.25;

        var s0 = ConcreteDesigner.Design(p, r);
        check("Móng cọc tự chọn: mọi đài có bảng kiểm tra an toàn", s0.PileCaps.Count(c => c.Safety != null), s0.PileCaps.Count, 0);
        check("Móng cọc tự chọn: mọi đài vẫn đạt", s0.PileCaps.Count(c => !c.Ok), 0, 0);
        foreach (var c in s0.PileCaps)
            check($"{c.Name}: bảng phản lực đủ số cọc", c.Safety.Piles.Count, c.Piles.Count, 0);

        // ---- bố trí do người dùng chọn: 5 cọc hoa mai tại chân cột chịu tải lớn nhất ----
        var nodes = FoundationUtil.BaseColumns(p).Select(x => x.Node).ToList();
        var nBig = nodes.OrderByDescending(n => FoundationUtil.Loads(r, n.Id, r.DesignCombos()).Max(l => l.N)).First();
        p.Foundation.CapOverrides.Add(new PileCapOverride { NodeId = nBig.Id, Pattern = PilePattern.P5 });
        var s1 = ConcreteDesigner.Design(p, r);
        var c5 = s1.PileCaps.First(c => c.NodeIds.Contains(nBig.Id));
        Console.WriteLine($"   · bố trí người dùng: {c5.Name} {c5.Piles.Count} cọc, {c5.L:0.00}×{c5.B:0.00}×{c5.H:0.00} m, Pmax = {c5.PMax:0.0} kN, {(c5.Ok ? "đạt" : "KHÔNG ĐẠT")}");
        check("Bố trí người dùng: đài có 5 cọc", c5.Piles.Count, 5, 0);
        check("Bố trí người dùng: đánh dấu UserLayout", B(c5.Safety?.UserLayout == true), 1, 0);
        check("Bố trí người dùng: ΣP = N + G", c5.Reactions.Sum(), c5.N + c5.Weight, 1e-6);
        check("Bố trí người dùng: có thông báo lựa chọn", B(c5.Messages.Any(m => m.StartsWith("Bố trí cọc theo lựa chọn của người dùng"))), 1, 0);
        check("Bố trí người dùng: các đài khác không đổi số cọc",
            s1.PileCaps.Where(c => !c.NodeIds.Contains(nBig.Id)).Sum(c => c.Piles.Count * c.Quantity),
            s0.PileCaps.Sum(c => c.Piles.Count * c.Quantity) - s0.PileCaps.First(c => c.NodeIds.Contains(nBig.Id)).Piles.Count, 0);
        // dư sức chịu tải → cảnh báo giảm số cọc
        if (c5.PMax < 0.7 * c5.PileCapacity)
            check("Bố trí người dùng: dư > 30% [P] → cảnh báo", B(c5.Messages.Any(m => m.Contains("dư > 30%"))), 1, 0);
        // kích thước đài do người dùng chọn
        var nOther = nodes.First(n => n.Id != nBig.Id);
        p.Foundation.CapOverrides.Add(new PileCapOverride { NodeId = nOther.Id, Pattern = PilePattern.P4, L = 1.6, B = 1.4 });
        var s2 = ConcreteDesigner.Design(p, r);
        var c4 = s2.PileCaps.First(c => c.NodeIds.Contains(nOther.Id));
        check("Kích thước người dùng: L = 1,6 m", c4.L, 1.6, 1e-12);
        check("Kích thước người dùng: B = 1,4 m", c4.B, 1.4, 1e-12);
        check("Kích thước người dùng: 4 cọc", c4.Piles.Count, 4, 0);

        // chiều cao cố định quá nhỏ → chọc thủng: yêu cầu tăng H hoặc cốt thép chống chọc thủng
        p.Foundation.CapOverrides.First(o => o.NodeId == nBig.Id).H = 0.35;
        var s3 = ConcreteDesigner.Design(p, r);
        var ch = s3.PileCaps.First(c => c.NodeIds.Contains(nBig.Id));
        Console.WriteLine($"   · H cố định 350 mm: {ch.Name} H = {ch.H:0.00}, F = {ch.Safety.Punching.F:0} kN, Fb,u = {ch.Safety.Punching.Fbu:0} kN, {ch.Safety.Punching.Reinforcement}");
        check("Chiều cao cố định: H giữ 350 mm", ch.H, 0.35, 1e-12);
        check("Chiều cao cố định: báo cần tăng chiều cao / cốt thép chống chọc thủng",
            B(ch.Messages.Any(m => m.Contains("cần tăng chiều cao đài") || m.Contains("cốt thép chống chọc thủng"))), 1, 0);
        p.Foundation.CapOverrides.First(o => o.NodeId == nBig.Id).H = 0;

        // ---- JSON: lưu / mở dự án giữ bố trí cọc ----
        p.Foundation.CapOverrides.Add(new PileCapOverride
        {
            NodeId = -99, Pattern = PilePattern.Custom, CustomPiles = new List<PilePoint> { new(-0.4, 0), new(0.4, 0) }, Rotate = true, Note = "thử"
        });
        string json = JsonSerializer.Serialize(p, Project.JsonOptions);
        var back = JsonSerializer.Deserialize<Project>(json, Project.JsonOptions);
        check("JSON: số bố trí cọc giữ nguyên", back.Foundation.CapOverrides.Count, p.Foundation.CapOverrides.Count, 0);
        check("JSON: sơ đồ 5 cọc giữ nguyên", B(back.Foundation.CapOverrides.First(o => o.NodeId == nBig.Id).Pattern == PilePattern.P5), 1, 0);
        var cb = back.Foundation.CapOverrides.First(o => o.NodeId == -99);
        check("JSON: toạ độ cọc tuỳ chọn", cb.CustomPiles.Count == 2 ? cb.CustomPiles[1].X : -1, 0.4, 1e-12);
        check("JSON: xoay sơ đồ (bool?)", B(cb.Rotate == true), 1, 0);
        check("JSON: kích thước đài người dùng", back.Foundation.CapOverrides.First(o => o.NodeId == nOther.Id).L, 1.6, 1e-12);
        var old = JsonSerializer.Deserialize<FoundationSettings>("{\"Type\":\"Pile\",\"PileSize\":0.3}", Project.JsonOptions);
        check("JSON cũ (không có CapOverrides): danh sách rỗng", old.CapOverrides?.Count ?? -1, 0, 0);
        p.Foundation.CapOverrides.RemoveAll(o => o.NodeId == -99);
        var s1b = ConcreteDesigner.Design(back, FrameAnalysis.Run(back));
        check("JSON: thiết kế lại sau khi mở vẫn 5 cọc", s1b.PileCaps.First(c => c.NodeIds.Contains(nBig.Id)).Piles.Count, 5, 0);

        // ---- thiết kế thử 1 nút (hộp thoại Bố trí cọc) ----
        var trialFs = JsonSerializer.Deserialize<FoundationSettings>(JsonSerializer.Serialize(p.Foundation, Project.JsonOptions), Project.JsonOptions);
        PileLayout.Set(trialFs, new[] { nBig.Id }, new PileCapOverride { Pattern = PilePattern.Grid, Nx = 3, Ny = 3 });
        var trial = PileCapDesigner.DesignNode(p, r, nBig.Id, trialFs);
        check("Thiết kế thử: lưới 3×3 = 9 cọc", trial.Piles.Count, 9, 0);
        check("Thiết kế thử: không đổi thông số dự án", B(PileLayout.Find(p.Foundation, nBig.Id).Pattern == PilePattern.P5), 1, 0);
        PileLayout.Set(trialFs, new[] { nBig.Id }, null);
        check("Xoá bố trí → tự chọn", B(PileLayout.Find(trialFs, nBig.Id) == null), 1, 0);

        // ---- bản vẽ + thuyết minh có mục kiểm tra mới ----
        string dir = Path.Combine(string.IsNullOrEmpty(outDir) ? Path.GetTempPath() : outDir, "KiemTraMong");
        try
        {
            Directory.CreateDirectory(dir);
            var figs = FootingDrawing.BuildAll(p, s1);
            check("Bản vẽ móng cọc (bố trí người dùng): xuất DXF mọi hình", figs.Count(f => f.W.ToDxfString().Length > 0), figs.Count, 0);
            var capFig = FoundationDrawing.CapPlan(p, c5, 50).ToDxfString();
            check("Bản vẽ đài: đánh số cọc C1…C5", B(capFig.Contains("C5")), 1, 0);
            CalculationReport.Export(p, r, s1, Path.Combine(dir, "tm_kiemtra_coc.docx"));
            check("Thuyết minh móng cọc có kiểm tra an toàn", 1, 1, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("   · lỗi bản vẽ / thuyết minh móng cọc: " + ex);
            check("Thuyết minh móng cọc có kiểm tra an toàn", 0, 1, 0);
        }

        // ---- móng băng: phân loại sự làm việc ----
        p.Foundation.CapOverrides.Clear();
        p.Foundation.Type = FoundationType.Strip;
        p.Soil.ROverride = 150;
        var sb = ConcreteDesigner.Design(p, r);
        check("Móng băng: mọi dải có phân loại sự làm việc", sb.StripFootings.Count(x => x.Safety != null && x.Safety.LambdaL > 0), sb.StripFootings.Count, 0);
        foreach (var m in sb.StripFootings)
        {
            var conc = p.Material(p.Concrete.ConcreteFooting);
            double lam = FootingBehaviour.Lambda(m.K, m.Width, m.Safety.EI);
            Console.WriteLine($"   · {m.Name}: λ = {m.Safety.Lambda:0.000} 1/m, λL = {m.Safety.LambdaL:0.00} → {m.Safety.Classification}");
            check($"{m.Name}: λL = λ·L", m.Safety.LambdaL, lam * m.Length, 1e-9);
            check($"{m.Name}: lớp phân loại khớp λL", B(m.Safety.Classification == FootingBehaviour.StripClassName(FootingBehaviour.StripClass(m.Safety.LambdaL))), 1, 0);
            check($"{m.Name}: giải thích 'dầm trên nền đàn hồi'", B(m.Safety.Behaviour.Contains("dầm trên nền đàn hồi")), 1, 0);
            check($"{m.Name}: EI = E·I(T ngược)", m.Safety.EI, conc.E * StripFootingDesigner.InertiaT(m.Width, (m.FlangeEdge + m.FlangeRoot) / 2, m.RibB, m.RibH), 1e-9);
        }
        try
        {
            p.Foundation.DrawStripMoment = true;
            var figs = FootingDrawing.BuildAll(p, sb);
            check("Bản vẽ móng băng có biểu đồ mô men: xuất DXF", figs.Count(f => f.W.ToDxfString().Length > 0), figs.Count, 0);
            CalculationReport.Export(p, r, sb, Path.Combine(dir, "tm_kiemtra_bang.docx"));
            check("Thuyết minh móng băng có phân loại sự làm việc", 1, 1, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("   · lỗi móng băng: " + ex);
            check("Thuyết minh móng băng có phân loại sự làm việc", 0, 1, 0);
        }
        p.Foundation.DrawStripMoment = false;
        p.Soil.ROverride = 0;

        // ---- móng đơn: sự làm việc ----
        p.Foundation.Type = FoundationType.Isolated;
        var si = ConcreteDesigner.Design(p, r);
        check("Móng đơn: mọi móng có phân tích sự làm việc", si.Footings.Count(x => x.Safety != null && x.Safety.Items.Count > 0), si.Footings.Count, 0);
        try
        {
            CalculationReport.Export(p, r, si, Path.Combine(dir, "tm_kiemtra_don.docx"));
            check("Thuyết minh móng đơn có sự làm việc", 1, 1, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("   · lỗi móng đơn: " + ex);
            check("Thuyết minh móng đơn có sự làm việc", 0, 1, 0);
        }
    }
}
