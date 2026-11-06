using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Detailing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng chọc thủng đài cọc / móng có kể mô men (TCVN 5574:2018 mục 8.1.6.2), cắt theo mép móng (8.1.3.3),
/// cọc chịu nhổ (TCVN 10304:2014, neo thép 10.3.5) và phản lực dầm móng (giằng liên tục, dầm trên nền Winkler).
/// Các giá trị kỳ vọng tính tay (dạng đóng).</summary>
public static class PunchingUpliftTests
{
    private static double B(bool x) => x ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        const double rbt = 1050;

        // ---- 1. Phản lực cọc đài cứng ----
        var piles4 = new List<(double X, double Y)> { (-0.6, -0.6), (0.6, -0.6), (-0.6, 0.6), (0.6, 0.6) };
        var P = PileGroup.Reactions(1000, 60, 120, 0, piles4);
        check("Phản lực cọc: P(0,6; 0,6) = N/4 + Mx·y/Σy² + My·x/Σx²", P[3], 250 + 60 * 0.6 / 1.44 + 120 * 0.6 / 1.44, 1e-12);
        check("Phản lực cọc: P(−0,6; −0,6)", P[0], 250 - 25 - 50, 1e-12);
        check("Phản lực cọc: ΣP = N", P.Sum(), 1000, 1e-12);

        // ---- 2. Đường bao chọc thủng chữ nhật: u, Wb ----
        var ct = PunchingContour.Rect(0.3, 0.3, 0.5);
        check("Đường bao: u = 2(hc + bc + 2h0)", ct.U, 2 * (0.3 + 0.3 + 2 * 0.5), 1e-12);
        double lx = 0.8, ly = 0.8;
        check("Đường bao: Wb = lx²/3 + lx·ly (TCVN 5574:2018)", ct.Wb(true), lx * lx / 3 + lx * ly, 1e-12);
        check("Đường bao kín: trọng tâm tại tim cột", Math.Abs(ct.Centroid().X0) + Math.Abs(ct.Centroid().Y0), 0, 1e-12);

        // ---- 3. Chọc thủng có kể mô men ----
        var pr = Punching.Check(ct, 500, 100, 0, 0.5, rbt);
        double fbu = rbt * 3.2 * 0.5, mbu = rbt * (lx * lx / 3 + lx * ly) * 0.5;
        check("Chọc thủng: Fb,u = Rbt·u·h0", pr.Fbu, fbu, 1e-12);
        check("Chọc thủng: Mb,u = Rbt·Wb·h0", pr.Mby, mbu, 1e-12);
        check("Chọc thủng: F/Fb,u + 0,5M/Mb,u", pr.Ratio, 500 / fbu + 0.5 * 100 / mbu, 1e-12);
        var prBig = Punching.Check(ct, 200, 2000, 0, 0.5, rbt);
        check("Chọc thủng: phần mô men chặn ≤ F/Fb,u", prBig.Ratio, 2 * 200 / fbu, 1e-12);

        // ---- 4. Đường bao bị cắt ở cạnh sát ranh (móng lệch tâm, 3 cạnh) ----
        var c3 = PunchingContour.Rect(0.3, 0.3, 0.5, cutXNeg: true, extXNeg: 0.15);
        double lh = 0.15 + 0.4;   // cạnh ngang: từ mép móng (x = −0,15) tới x = 0,4
        check("Đường bao 3 cạnh: u = 2·(0,15 + 0,4) + 0,8", c3.U, 2 * lh + 0.8, 1e-12);
        double x0 = (2 * lh * (-0.15 + 0.4) / 2 + 0.8 * 0.4) / (2 * lh + 0.8);
        check("Đường bao 3 cạnh: trọng tâm lệch x0", c3.Centroid().X0, x0, 1e-12);
        double a = -0.15 - x0, b = 0.4 - x0;
        double ib = 2 * lh * (a * a + a * b + b * b) / 3 + 0.8 * b * b;
        check("Đường bao 3 cạnh: Wb = Ib / xmax", c3.Wb(true), ib / Math.Max(Math.Abs(a), Math.Abs(b)), 1e-12);
        var pr3 = Punching.Check(c3, 300, 0, 0, 0.5, rbt, 0.5);
        check("Đường bao 3 cạnh: kể mô men F·e0", pr3.My, 300 * x0, 1e-12);
        var c2 = PunchingContour.Rect(0.3, 0.3, 0.5, cutXNeg: true, cutYNeg: true, extXNeg: 0.15, extYNeg: 0.15);
        check("Cột góc: đường bao 2 cạnh u = 2·(0,15 + 0,4)", c2.U, 2 * lh, 1e-12);

        // ---- 5. Chọc thủng cọc góc & cắt tiết diện nghiêng ----
        check("Cọc góc: u = 2·(d/2 + h0/2 + mép)", PileCapChecks.PilePerimeter(0.6, 0.6, 0.3, 0.5, 2.0, 2.0), 2 * (0.15 + 0.25 + 0.4), 1e-12);
        check("Cắt nghiêng: Qb chặn trên 2,5·Rbt·b·h0", Punching.ShearNoLinks(2, 0.5, 0.25, rbt), 2.5 * rbt * 2 * 0.5, 1e-12);
        check("Cắt nghiêng: Qb = 1,5·Rbt·b·h0²/c", Punching.ShearNoLinks(2, 0.5, 0.75, rbt), 1.5 * rbt * 2 * 0.25 / 0.75, 1e-12);
        check("Cắt nghiêng: Qb chặn dưới 0,5·Rbt·b·h0", Punching.ShearNoLinks(2, 0.5, 5, rbt), 0.5 * rbt * 2 * 0.5, 1e-12);
        var es = Punching.EdgeShear(200, 2, 0.8, 0.4, rbt);
        check("Cắt mép móng: Q = p·b·(l1 − c)", es.Q, 200 * 2 * (0.8 - es.C), 1e-9);
        check("Cắt mép móng: Qb = Qb(c)", es.Qb, Punching.ShearNoLinks(2, 0.4, es.C, rbt), 1e-9);

        // ---- 6. Cọc chịu nhổ (TCVN 10304:2014) + neo thép ----
        var (fut, pk, gp) = PileGroup.TensionCapacity(1.0, 300, 0.0625, 15);
        check("Nhổ: Fu,t = 0,8·u·Σfi·li", fut, 0.8 * 1.0 * 300, 1e-12);
        check("Nhổ: [Pk] = Fu,t/1,4 + 0,9·Gc", pk, 240 / 1.4 + 0.9 * 0.0625 * 15 * 25, 1e-12);
        var (l0, lan) = PileGroup.AnchorageTension(350000, rbt, 16, 100 / 350000.0, 4 * Math.PI * 16 * 16 / 4 * 1e-6);
        check("Neo chịu kéo: l0,an = Rs·d/(4·2,5·Rbt)", l0, 350000 * 0.016 / (4 * 2.5 * rbt), 1e-12);
        check("Neo chịu kéo: lan ≥ 15d (As,cal nhỏ)", lan, 0.24, 1e-12);
        var up = PileGroup.Uplift(-100, 0, 1.0, 300, 0.0625, 15, 4, 16, 350000, rbt, 0.48);
        check("Nhổ: Nt = −Pmin", up.Nt, 100, 1e-12);
        check("Nhổ: Rs·As thép cọc", up.SteelCap, 350000 * 4 * Math.PI * 0.016 * 0.016 / 4, 1e-9);
        check("Nhổ: [Pk] tính theo ma sát khi có Σfi·li", up.Pk, pk, 1e-12);
        check("Nhổ: có ghi chú mối nối chịu kéo", B(up.Notes.Any(n => n.Contains("mối nối"))), 1, 0);
        var upShort = PileGroup.Uplift(-500, 0, 0, 0, 0, 0, 4, 16, 350000, rbt, 0.10);
        check("Nhổ: neo ngắn → CẢNH BÁO", B(upShort.Notes.Any(n => n.StartsWith("CẢNH BÁO"))), 1, 0);

        // ---- 7. Đài cọc triển khai: chọc thủng có mô men, cọc chịu nhổ ----
        var s = new DetailSettings();
        var cap = new ManualPileCap { N = 600, Mx = 0, My = 700, Pa = 600, Pt = 300, NumPiles = 2, PileBarN = 4, PileBarD = 16 };
        var rc = cap.Run(s);
        check("Đài nhổ: Pmin < 0", B(cap.PileForces.Min() < 0), 1, 0);
        check("Đài nhổ: kiểm tra thép cọc chịu kéo", B(rc.Checks.Any(k => k.Name.StartsWith("Cọc chịu nhổ: thép cọc"))), 1, 0);
        check("Đài nhổ: kiểm tra neo râu thép chịu kéo", B(rc.Checks.Any(k => k.Name.StartsWith("Cọc chịu nhổ: neo râu"))), 1, 0);
        var cap4 = new ManualPileCap { N = 1600, Mx = 80, My = 120, Pa = 600, NumPiles = 4, H = 0.6, Spacing = 1.5 };   // H cố định: cọc nằm ngoài tháp 45° (H tự chọn lớn có thể trùm hết cọc → F = 0, không cần kiểm tra)
        var rc4 = cap4.Run(s);
        var km = rc4.Checks.FirstOrDefault(k => k.Name.StartsWith("Chọc thủng cột – đài có kể mô men"));
        check("Đài 4 cọc: có kiểm tra chọc thủng kể mô men", B(km != null), 1, 0);
        var cap4a = new ManualPileCap { N = 1600, Mx = 80, My = 120, Pa = 600, NumPiles = 4, Spacing = 1.5 };   // H = 0 → tự chọn
        var rc4a = cap4a.Run(s);
        var kma = rc4a.Checks.FirstOrDefault(k => k.Name.StartsWith("Chọc thủng cột – đài có kể mô men"));
        check("Đài 4 cọc tự chọn H: chọc thủng kể mô men đạt", B(kma == null || kma.Ok), 1, 0);
        var capSk = new ManualPileCap { N = 600, My = 700, Pa = 600, NumPiles = 2, SkinFriction = 300, PileD = 0.25, PileLength = 15 };
        var rsk = capSk.Run(s);
        var pkCheck = rsk.Checks.First(k => k.Name.StartsWith("Cọc chịu nhổ: lực nhổ"));
        check("Đài: [Pk] từ Σfi·li = 0,8·4D·Σ/1,4 + 0,9·Gc", pkCheck.Capacity, 0.8 * 1.0 * 300 / 1.4 + 0.9 * 0.0625 * 15 * 25, 1e-9);
        check("Đài: mặt cắt vẽ tháp chọc thủng 45°", B(cap4.Draw(rc4, s).Any(f => f.W.ToDxfStringDec().Contains("THÁP CHỌC THỦNG"))), 1, 0);

        // ---- 8. Thiết kế (PileCapChecks): mô men + nhổ ----
        var p2 = PileLayout.Generate(PilePattern.P2, 0.9, 0, 0, 0, true);
        var inp = new PileCapInput { Piles = p2, Loads = new List<BaseLoad> { new() { Combo = "G", N = 200, My = 400 } }, G = 50, H = 0.6, H0 = 0.45, L = 1.5, B = 0.6, D = 0.3, P = 450, Pt = 400, Rbt = rbt };
        var rep = PileCapChecks.Evaluate(inp);
        check("Thiết kế: nhổ → kiểm tra thép cọc chịu kéo", B(rep.Items.Any(i => i.Name.StartsWith("Cọc chịu nhổ: thép cọc"))), 1, 0);
        check("Thiết kế: nhổ → kiểm tra neo râu thép", B(rep.Items.Any(i => i.Name.StartsWith("Cọc chịu nhổ: neo râu"))), 1, 0);
        var p4 = PileLayout.Generate(PilePattern.P4, 2.0, 0, 0, 0, true);   // cọc ±1,0 m nằm ngoài tháp 45° (hc/2 + h0 = 0,7 m)
        var inp4 = new PileCapInput { Piles = p4, Loads = new List<BaseLoad> { new() { Combo = "G", N = 1600, Mx = 100, My = 150 } }, G = 80, H = 0.7, H0 = 0.55, L = 2.6, B = 2.6, D = 0.3, P = 600, Hc = 0.3, Bc = 0.3, Rbt = rbt };
        var rep4 = PileCapChecks.Evaluate(inp4);
        var it4 = rep4.Items.FirstOrDefault(i => i.Name.StartsWith("Chọc thủng đài có kể mô men"));
        check("Thiết kế: chọc thủng đài có kể mô men", B(it4 != null), 1, 0);
        check("Thiết kế: chọc thủng mô men không phải KHÔNG ĐẠT cứng (cảnh báo)", B(it4 == null || it4.Status != CheckStatus.Fail), 1, 0);

        // ---- 9. Móng đơn: chọc thủng có mô men + cắt mép móng ----
        var fo = new ManualFooting { N = 800, ML = 80, MB = 40, R = 150, Df = 1.5 };
        var rf = fo.Run(s);
        check("Móng đơn: có kiểm tra chọc thủng kể mô men", B(rf.Checks.Any(k => k.Name.StartsWith("Chọc thủng có kể mô men"))), 1, 0);
        check("Móng đơn: có kiểm tra cắt mép móng phương L, B", rf.Checks.Count(k => k.Name.StartsWith("Cắt theo mép móng")), 2, 0);
        check("Móng đơn tự chọn H: đạt mọi kiểm tra", B(rf.Ok), 1, 0);
        var fe = new ManualFooting { N = 600, ML = 0, MB = 0, BoundaryL = "Giáp nhà hàng xóm bên trái (hết đất)", L = 2.0, B = 1.8, H = 0.5 };
        var rfe = fe.Run(s);
        check("Móng sát ranh: đường bao chọc thủng bị cắt (ghi thuyết minh)", B(rfe.Calc.Any(l => l.Contains("đường bao bị cắt"))), 1, 0);
        check("Móng đơn: mặt cắt vẽ tháp chọc thủng", B(fo.Draw(rf, s).Any(f => f.W.ToDxfStringDec().Contains("THÁP CHỌC THỦNG"))), 1, 0);

        // ---- 10. Dầm móng: giằng liên tục (ba mô men) ----
        var g2 = GroundBeam.Continuous(new[] { 4.0, 4.0 }, 10);
        check("Giằng 2 nhịp: M gối giữa = −qL²/8", g2.SupportMoments[1], -10 * 16 / 8.0, 1e-9);
        check("Giằng 2 nhịp: R biên = 3qL/8", g2.Reactions[0], 3 * 10 * 4 / 8.0, 1e-9);
        check("Giằng 2 nhịp: R giữa = 10qL/8", g2.Reactions[1], 10 * 10 * 4 / 8.0, 1e-9);
        check("Giằng 2 nhịp: M nhịp = 9qL²/128", g2.MPosMax, 9 * 10 * 16 / 128.0, 1e-9);
        var g3 = GroundBeam.Continuous(new[] { 5.0, 5.0, 5.0 }, 12);
        check("Giằng 3 nhịp đều: M gối = −qL²/10", g3.SupportMoments[1], -12 * 25 / 10.0, 1e-9);
        check("Giằng 3 nhịp đều: R gối giữa = 1,1qL", g3.Reactions[1], 1.1 * 12 * 5, 1e-9);
        check("Giằng: ΣR = q·ΣL", g3.Reactions.Sum(), 12 * 15, 1e-9);

        // ---- 11. Dầm móng trên nền Winkler: cân bằng, áp lực nền ----
        var gw = GroundBeam.OnWinkler(6, 0.4, 1e10, 20000, 20, new List<(double, double)> { (0, 100), (6, 100) });
        check("Winkler: tổng phản lực nền = tổng tải (sai số rời rạc phần tử 0,1 m)", gw.SoilReaction, 20 * 6 + 200, 1e-4);
        check("Winkler: dầm rất cứng → áp lực đều = ΣP/(b·L)", gw.PressureMax, 320 / (0.4 * 6), 5e-3);
        var gs = GroundBeam.OnWinkler(6, 0.4, 2e4, 20000, 0, new List<(double, double)> { (3, 200) });
        check("Winkler: lực tập trung giữa – áp lực lớn nhất dưới lực > trung bình", B(gs.PressureMax > 200 / (0.4 * 6)), 1, 0);
        check("Winkler: cân bằng lực tập trung", gs.SoilReaction, 200, 1e-6);

        // ---- 12. Phản lực giằng cộng vào N móng ----
        var gb = new ManualBeam { Name = "DM1", Position = "Dầm móng (giằng móng)", Spans = "4; 4", B = 0.22, H = 0.40, GroundCalc = ManualBeam.GroundTie, WallT = 0.22, WallH = 3.0 };
        var rgb = gb.Run(s);
        double qTot = 1.1 * 25 * 0.22 * 0.40 + 1.1 * 18 * 0.22 * 3.0;
        check("Dầm móng giằng: phản lực gối giữa = 1,25·q·L", gb.GroundResult.Reactions[1], 1.25 * qTot * 4, 1e-9);
        check("Dầm móng giằng: M gối dùng kiểm tra = qL²/8", rgb.Checks.First(k => k.Name.StartsWith("Chịu uốn tại gối")).Demand, qTot * 16 / 8, 1e-9);
        check("Dầm móng giằng: thông báo phản lực xuống móng", B(rgb.Messages.Any(m => m.StartsWith("Phản lực giằng DM1"))), 1, 0);
        var fR = new ManualFooting { N = 600, ML = 0, MB = 0, R = 150, Df = 1.5, BeamReaction = gb.GroundResult.Reactions[1] };
        var rfR = fR.Run(s);
        check("Móng: N kể phản lực giằng", ((FootingDesign)rfR.Source).N, 600 + 1.25 * qTot * 4, 1e-9);
        var gwb = new ManualBeam { Name = "DM2", Position = "Dầm móng (giằng móng)", Spans = "3; 3", B = 0.3, H = 0.5, GroundCalc = ManualBeam.GroundWinkler, SupportLoads = "150; 300; 150", SoilR = 150 };
        var rgw = gwb.Run(s);
        check("Dầm móng Winkler: kiểm tra áp lực nền", B(rgw.Checks.Any(k => k.Name.StartsWith("Áp lực nền dưới dầm móng"))), 1, 0);
        check("Dầm móng Winkler: cân bằng ΣR nền = ΣP + q·L", gwb.GroundResult.SoilReaction, gwb.GroundResult.TotalLoad, 1e-6);
    }
}
