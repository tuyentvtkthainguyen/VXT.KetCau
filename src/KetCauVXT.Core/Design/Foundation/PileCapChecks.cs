using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

/// <summary>Dữ liệu đầu vào kiểm tra an toàn một đài cọc (đơn vị kN, m, kN/m²). Toạ độ cọc so với tim cột = tâm đài.</summary>
public sealed class PileCapInput
{
    public List<(double X, double Y)> Piles { get; set; } = new();
    /// <summary>Nội lực chân cột các tổ hợp TTGH I (quy ước FoundationUtil.Loads)</summary>
    public List<BaseLoad> Loads { get; set; } = new();
    /// <summary>Trọng lượng tính toán đài + đất trên đài (kN)</summary>
    public double G { get; set; }
    /// <summary>Chiều cao đài H, chiều cao làm việc h0 (m)</summary>
    public double H { get; set; } = 0.6;
    public double H0 { get; set; } = 0.45;
    /// <summary>Kích thước cột theo X (hc) và Y (bc), m</summary>
    public double Hc { get; set; } = 0.3;
    public double Bc { get; set; } = 0.3;
    /// <summary>Cạnh / đường kính cọc d (m)</summary>
    public double D { get; set; } = 0.3;
    public bool Round { get; set; }
    /// <summary>Kích thước đài theo X, Y (m)</summary>
    public double L { get; set; }
    public double B { get; set; }
    /// <summary>[P] nén, [Pk] kéo, Pvl vật liệu, [H] ngang của 1 cọc (kN); HAllowEstimated = [H] chỉ là ước lượng (cảnh báo, không kết luận không đạt)</summary>
    public double P { get; set; }
    public double Pt { get; set; }
    public double Pvl { get; set; }
    public double HAllow { get; set; }
    public bool HAllowEstimated { get; set; } = true;
    /// <summary>Rbt bê tông đài, Rsw cốt thép ngang (kN/m²)</summary>
    public double Rbt { get; set; } = 1050;
    public double Rsw { get; set; } = 170000;
    /// <summary>true = chiều cao đài do người dùng cố định (không tự tăng) – khi chọc thủng không đạt sẽ tính cốt thép chống chọc thủng</summary>
    public bool FixedHeight { get; set; }
    public bool UserLayout { get; set; }
    /// <summary>Chiều cao đài tối thiểu ban đầu (m) – để báo cáo việc tự tăng chiều cao</summary>
    public double HStart { get; set; }
    public string PatternName { get; set; } = "";
    /// <summary>Hố khoan để tính lún nhóm cọc (null = chưa có địa chất)</summary>
    public Borehole Borehole { get; set; }
    /// <summary>Độ sâu đáy đài Df (m), chiều dài cọc (m), hệ số vượt tải trung bình (quy tải tính toán về tiêu chuẩn)</summary>
    public double Df { get; set; } = 1.5;
    public double PileLength { get; set; } = 15;
    public double LoadFactor { get; set; } = 1.15;
}

/// <summary>Kiểm tra an toàn đài cọc – phản lực từng cọc, sức chịu tải, cọc chịu nhổ, hiệu ứng nhóm, tải ngang, lún nhóm cọc,
/// quy định bố trí cọc, chọc thủng (cột + cọc góc), cắt trên tiết diện nghiêng.
/// <list type="bullet">
/// <item>Pi = (N + G)/n + ax·(xi − x̄) + ay·(yi − ȳ), ax = (−My − H·Qx − (N + G)·x̄)/Σ(xi − x̄)², ay = (Mx − H·Qy − (N + G)·ȳ)/Σ(yi − ȳ)²
/// (PileCapDesigner.PileForces; với nhóm cọc đối xứng: Pi = (N+G)/n ± My'·xi/Σx² ± Mx'·yi/Σy²).</item>
/// <item>Chọc thủng do cột (TCVN 5574:2018 mục 8.1.6.2, F ≤ Rbt·u·h0) có kể hệ số tăng cho đài cọc k = h0/c (1 ≤ k ≤ 2,5; c = khoảng cách mép cột – mép trong hàng cọc, 0,4h0 ≤ c ≤ h0):
/// Fb,u = 2·Rbt·h0·[k1·(bc + c2) + k2·(hc + c1)] (với c = h0: Fb,u = Rbt·2(hc + bc + 2h0)·h0 như bản sàn). Cọc có tim nằm trong đáy tháp 45° không gây chọc thủng.</item>
/// <item>Khi chiều cao cố định và F &gt; Fb,u: cốt thép chống chọc thủng theo mục 8.1.6.3: F ≤ Fb,u + Fsw,u, Fsw,u = 0,8·qsw·u, qsw = Rsw·Asw/sw,
/// Fsw,u ≥ 0,25Fb,u, Fb,u + Fsw,u ≤ 2Fb,u (F &gt; 2Fb,u → bắt buộc tăng chiều cao).</item>
/// <item>Chọc thủng cọc: F = Pi − G/n ≤ Rbt·ui·h0, ui = chu vi đường bao cách mép cọc h0/2, bỏ các cạnh vượt ra ngoài mép đài.</item>
/// <item>Cắt tiết diện nghiêng (8.1.3.3): Q ≤ Qb = 1,5·Rbt·b·h0²/c, 0,5·Rbt·b·h0 ≤ Qb ≤ 2,5·Rbt·b·h0.</item>
/// <item>Lún nhóm cọc: móng khối quy ước đặt tại 2/3 chiều dài cọc, mở rộng 1:4 (Tomlinson): Lq = Lg + Lc/3, Bq = Bg + Lc/3; p = σ'bt(zq) + Ntc/(Lq·Bq);
/// S = GeotechCalc.Settlement ≤ 8 cm.</item>
/// </list></summary>
public static class PileCapChecks
{
    /// <summary>Hệ số dư sức chịu tải coi là chưa tận dụng (Pmax &lt; 70%[P] → gợi ý giảm số cọc)</summary>
    public const double UnderUseRatio = 0.70;
    /// <summary>[H] ước lượng = 0,1·[P] khi chưa khai báo</summary>
    public const double LateralFactor = 0.10;

    // ------------------------------------------------------------------
    // Chọc thủng do cột
    // ------------------------------------------------------------------
    /// <summary>Tim cọc nằm trong đáy tháp chọc thủng 45° (mở rộng h0 mỗi phía mép cột).</summary>
    public static bool InsidePyramid(double x, double y, double hc, double bc, double h0) =>
        Math.Abs(x) <= hc / 2 + h0 + 1e-9 && Math.Abs(y) <= bc / 2 + h0 + 1e-9;

    /// <summary>Khả năng chịu chọc thủng do cột Fb,u (kN) có kể hệ số h0/c, kèm c1, c2, k1, k2.</summary>
    public static double ColumnPunchCapacity(IList<(double X, double Y)> piles, double hc, double bc, double h0, double d, double rbt,
        out double c1, out double c2, out double k1, out double k2)
    {
        c1 = h0; c2 = h0;
        if (h0 <= 0) { k1 = k2 = 1; return 0; }
        double best1 = double.MaxValue, best2 = double.MaxValue;
        foreach (var q in piles)
        {
            if (InsidePyramid(q.X, q.Y, hc, bc, h0)) continue;
            double ox = Math.Abs(q.X) - hc / 2, oy = Math.Abs(q.Y) - bc / 2;
            if (ox >= oy) best1 = Math.Min(best1, Math.Max(ox - d / 2, 0));
            else best2 = Math.Min(best2, Math.Max(oy - d / 2, 0));
        }
        c1 = Math.Clamp(best1 == double.MaxValue ? h0 : best1, 0.4 * h0, h0);
        c2 = Math.Clamp(best2 == double.MaxValue ? h0 : best2, 0.4 * h0, h0);
        k1 = h0 / c1; k2 = h0 / c2;
        return 2 * rbt * h0 * (k1 * (bc + c2) + k2 * (hc + c1));
    }

    public static double ColumnPunchCapacity(IList<(double X, double Y)> piles, double hc, double bc, double h0, double d, double rbt) =>
        ColumnPunchCapacity(piles, hc, bc, h0, d, rbt, out _, out _, out _, out _);

    /// <summary>Chu vi chọc thủng của cọc ui (m): đường bao vuông cách mép cọc h0/2, bỏ các cạnh nằm ngoài mép đài L × B (đài tâm tại gốc).</summary>
    public static double PilePerimeter(double px, double py, double d, double h0, double L, double B)
    {
        double a = d / 2 + h0 / 2;
        double x1 = px - a, x2 = px + a, y1 = py - a, y2 = py + a;
        double yl = Math.Max(y1, -B / 2), yh = Math.Min(y2, B / 2), xl = Math.Max(x1, -L / 2), xh = Math.Min(x2, L / 2);
        double u = 0;
        if (x2 < L / 2 - 1e-9) u += Math.Max(yh - yl, 0);
        if (x1 > -L / 2 + 1e-9) u += Math.Max(yh - yl, 0);
        if (y2 < B / 2 - 1e-9) u += Math.Max(xh - xl, 0);
        if (y1 > -B / 2 + 1e-9) u += Math.Max(xh - xl, 0);
        return u;
    }

    /// <summary>Khả năng chịu cắt bê tông không cốt đai trên tiết diện nghiêng (8.1.3.3): Qb = 1,5·Rbt·b·h0²/c, 0,5 ≤ Qb/(Rbt·b·h0) ≤ 2,5.</summary>
    public static double ShearCapacity(double b, double h0, double c, double rbt)
    {
        if (h0 <= 0 || b <= 0) return 0;
        return Math.Clamp(1.5 * rbt * b * h0 * h0 / Math.Max(c, 1e-3), 0.5 * rbt * b * h0, 2.5 * rbt * b * h0);
    }

    // ------------------------------------------------------------------
    /// <summary>Kiểm tra an toàn đầy đủ một đài cọc.</summary>
    public static FoundationCheckReport Evaluate(PileCapInput inp)
    {
        var rep = new FoundationCheckReport { Kind = "Đài cọc", Classification = inp.PatternName, UserLayout = inp.UserLayout, HStart = inp.HStart, HFinal = inp.H };
        var piles = inp.Piles ?? new List<(double X, double Y)>();
        int np = piles.Count;
        double d = Math.Max(inp.D, 0.05), h0 = inp.H0, hc = inp.Hc, bc = inp.Bc, rbt = inp.Rbt;
        if (np == 0 || inp.Loads == null || inp.Loads.Count == 0)
        {
            rep.Items.AddRange(PileLayout.CheckRules(piles, d, inp.L, inp.B));
            rep.Warnings.Add("Chưa đủ dữ liệu (cọc / nội lực chân cột) để kiểm tra đài.");
            return rep;
        }
        double g1 = inp.G / np;

        // ---- phản lực từng cọc qua mọi tổ hợp ----
        var rows = piles.Select((q, i) => new PileReactionRow
        {
            No = i + 1, X = q.X, Y = q.Y, PMax = double.MinValue, PMin = double.MaxValue, InsidePyramid = InsidePyramid(q.X, q.Y, hc, bc, h0)
        }).ToList();
        double pmax = double.MinValue, pmin = double.MaxValue, fCol = 0, hMax = 0, nMax = double.MinValue;
        double qxPos = 0, qxNeg = 0, qyPos = 0, qyNeg = 0;
        double[] gov = null;
        string govCombo = "";
        var pileNetMax = new double[np];
        foreach (var l in inp.Loads)
        {
            var P = PileCapDesigner.PileForces(l, piles, inp.G, inp.H);
            double f = 0, sxp = 0, sxn = 0, syp = 0, syn = 0;
            for (int i = 0; i < np; i++)
            {
                if (P[i] > rows[i].PMax) { rows[i].PMax = P[i]; rows[i].ComboMax = l.Combo; }
                rows[i].PMin = Math.Min(rows[i].PMin, P[i]);
                double pn = P[i] - g1;
                pileNetMax[i] = Math.Max(pileNetMax[i], pn);
                if (!rows[i].InsidePyramid) f += pn;
                double ox = Math.Abs(piles[i].X) - hc / 2, oy = Math.Abs(piles[i].Y) - bc / 2;
                if (ox > 1e-6) { if (piles[i].X > 0) sxp += pn; else sxn += pn; }
                if (oy > 1e-6) { if (piles[i].Y > 0) syp += pn; else syn += pn; }
            }
            fCol = Math.Max(fCol, f);
            qxPos = Math.Max(qxPos, sxp); qxNeg = Math.Max(qxNeg, sxn); qyPos = Math.Max(qyPos, syp); qyNeg = Math.Max(qyNeg, syn);
            double mx = P.Max(), mn = P.Min();
            if (mx > pmax) { pmax = mx; gov = P; govCombo = l.Combo; }
            pmin = Math.Min(pmin, mn);
            hMax = Math.Max(hMax, Math.Sqrt(l.Qx * l.Qx + l.Qy * l.Qy));
            nMax = Math.Max(nMax, l.N);
        }
        for (int i = 0; i < np; i++) rows[i].P = gov[i];
        rep.Piles = rows;

        // ---- sức chịu tải cọc ----
        rep.Items.Add(SafetyItem.Make("Phản lực cọc", "Phản lực lớn nhất Pmax ≤ [P]", pmax, inp.P, "kN",
            "Pi = (N + G)/n ± My'·xi/Σxi² ± Mx'·yi/Σyi²;  My' = My + Qx·H, Mx' = Mx − Qy·H (tổ hợp " + govCombo + ")",
            "Cọc chịu nén nhiều nhất vẫn nhỏ hơn sức chịu tải cho phép của cọc đơn.",
            "Cọc chịu nén vượt [P] – tăng số cọc, tăng khoảng cách cọc (giảm phản lực do mô men) hoặc dùng cọc lớn hơn."));
        if (pmin >= -1e-6)
            rep.Items.Add(new SafetyItem
            {
                Group = "Phản lực cọc", Name = "Phản lực nhỏ nhất Pmin ≥ 0 (không có cọc chịu nhổ)", Value = pmin, Limit = 0, Unit = "kN", Inverse = false,
                Status = CheckStatus.Ok, Formula = "Pmin ≥ 0", Explanation = "Mọi cọc đều chịu nén ở mọi tổ hợp."
            });
        else
        {
            var it = SafetyItem.Make("Phản lực cọc", "Cọc chịu nhổ |Pmin| ≤ [Pk]", -pmin, inp.Pt, "kN", "|Pmin| ≤ [Pk] (sức chịu nhổ cho phép của cọc)",
                "Có cọc chịu nhổ nhưng trong giới hạn [Pk]: phải neo thép cọc vào đài đủ chiều dài, mối nối cọc chịu kéo, bố trí thép lớp trên đài.",
                inp.Pt > 0 ? "Lực nhổ vượt [Pk] – tăng khoảng cách cọc theo phương mô men, tăng trọng lượng đài hoặc dùng cọc chịu nhổ."
                           : "Cọc chịu nhổ trong khi [Pk] = 0 (không cho phép kéo) – tăng khoảng cách cọc / số cọc, hoặc khai báo [Pk] và cấu tạo cọc chịu nhổ.");
            if (it.Status == CheckStatus.Ok) it.Status = CheckStatus.Warning;
            rep.Items.Add(it);
        }
        if (inp.Pvl > 0)
            rep.Items.Add(SafetyItem.Make("Phản lực cọc", "Sức chịu tải vật liệu cọc Pmax ≤ Pvl", pmax, inp.Pvl, "kN", "Pvl = φ·(Rb·Ab + Rsc·As), φ = 1",
                "Thân cọc đủ khả năng chịu nén (bê tông + thép dọc).", "Thân cọc không đủ chịu nén – tăng tiết diện / mác bê tông / thép dọc cọc."));
        if (np > 1 && pmax < UnderUseRatio * inp.P && pmax > 0)
        {
            double spare = 1 - pmax / inp.P;
            var it = new SafetyItem
            {
                Group = "Phản lực cọc", Name = "Mức tận dụng sức chịu tải cọc Pmax/[P]", Value = pmax, Limit = inp.P, Unit = "kN",
                Status = inp.UserLayout ? CheckStatus.Warning : CheckStatus.Info, Formula = "Pmax/[P] ≥ 70% (khuyến nghị kinh tế)",
                Explanation = $"Cọc còn dư {spare * 100:0}% sức chịu tải – có thể giảm số cọc, dùng cọc nhỏ hơn hoặc giảm kích thước đài."
            };
            rep.Items.Add(it);
            if (inp.UserLayout) rep.Warnings.Add($"Cảnh báo: cọc chỉ sử dụng {pmax / inp.P * 100:0}% [P] (dư > 30%) – xem xét giảm số cọc hoặc chọn sơ đồ nhỏ hơn.");
        }

        // ---- hiệu ứng nhóm ----
        if (np > 1)
        {
            double s = PileLayout.MinSpacing(piles);
            var (m, n) = PileLayout.RowsOf(piles);
            double eta = PileLayout.GroupEfficiency(m, n, s, d);
            rep.GroupEta = eta;
            double nTot = nMax + inp.G;
            if (s < PileLayout.GroupSpacingLimit * d)
            {
                var it = SafetyItem.Make("Nhóm cọc", "Sức chịu tải nhóm (N + G) ≤ η·n·[P]", nTot, eta * np * inp.P, "kN",
                    $"η = 1 − θ·[(n−1)m + (m−1)n]/(90mn) = {eta:0.000}; θ = arctg(d/s) = {Math.Atan(d / s) * 180 / Math.PI:0.0}°, m = {m}, n = {n} (Converse–Labarre)",
                    "Khoảng cách cọc < 6d nên vùng ứng suất quanh các cọc chồng lấn; tổng tải vẫn nhỏ hơn sức chịu tải nhóm đã giảm theo η.",
                    "Tổng tải vượt sức chịu tải nhóm đã kể hiệu ứng nhóm (η) – với cọc ma sát nên tăng khoảng cách cọc hoặc số cọc.",
                    false, CheckStatus.Warning);
                rep.Items.Add(it);
                if (it.Status != CheckStatus.Ok) rep.Warnings.Add($"Cảnh báo: hiệu ứng nhóm cọc η = {eta:0.00} (s = {s * 1000:0} mm < 6d) – tổng tải {nTot:0} kN > η·n·[P] = {eta * np * inp.P:0} kN.");
            }
        }

        // ---- tải ngang ----
        double hPile = hMax / np;
        double hAllow = inp.HAllow > 0 ? inp.HAllow : LateralFactor * inp.P;
        bool est = inp.HAllow <= 0 || inp.HAllowEstimated;
        var hi = SafetyItem.Make("Tải ngang", "Lực ngang 1 cọc H/n ≤ [H]", hPile, hAllow, "kN",
            "H = √(Qx² + Qy²) chia đều cho n cọc (đài cứng)" + (est ? "; [H] ước lượng 0,1·[P] – cần tính theo Phụ lục A TCVN 10304:2014" : ""),
            "Lực ngang chân cột nhỏ, cọc chịu được (thực tế phần lớn do giằng móng và áp lực đất bị động lên đài tiếp nhận).",
            "Lực ngang mỗi cọc vượt [H] – bố trí giằng móng tiếp nhận lực ngang, kiểm tra cọc chịu tải ngang theo Phụ lục A TCVN 10304:2014.",
            false, est ? CheckStatus.Warning : CheckStatus.Fail);
        rep.Items.Add(hi);

        // ---- quy định bố trí ----
        rep.Items.AddRange(PileLayout.CheckRules(piles, d, inp.L, inp.B));

        // ---- chọc thủng do cột ----
        var pd = new PunchingDetail { H0 = h0, U = 2 * (hc + bc + 2 * h0) };
        rep.Punching = pd;
        if (np == 1)
        {
            rep.Items.Add(new SafetyItem
            {
                Group = "Chọc thủng", Name = "Chọc thủng đài do cột (đài 1 cọc)", Status = CheckStatus.Info, Unit = "kN",
                Explanation = "Đài 1 cọc: lực cột truyền thẳng xuống đầu cọc, không hình thành tháp chọc thủng; đài chỉ cần đủ ngàm cọc và bao thép chờ cột."
            });
        }
        else
        {
            pd.AllInside = rows.All(r => r.InsidePyramid);
            pd.F = fCol;
            pd.Fbu = ColumnPunchCapacity(piles, hc, bc, h0, d, rbt, out double c1, out double c2, out double k1, out double k2);
            pd.C1 = c1; pd.C2 = c2; pd.K1 = k1; pd.K2 = k2;
            string formula = $"F = Σ(Pi − G/n) cọc ngoài tháp 45° ≤ Fb,u = 2·Rbt·h0·[k1(bc + c2) + k2(hc + c1)];  c1 = {c1 * 1000:0}, c2 = {c2 * 1000:0} mm, k1 = h0/c1 = {k1:0.00}, k2 = {k2:0.00} (TCVN 5574:2018 mục 8.1.6.2)";
            if (pd.AllInside)
                rep.Items.Add(new SafetyItem
                {
                    Group = "Chọc thủng", Name = "Chọc thủng đài do cột – mọi cọc trong tháp 45°", Value = 0, Limit = pd.Fbu, Unit = "kN", Status = CheckStatus.Ok,
                    Formula = $"|xi| ≤ hc/2 + h0, |yi| ≤ bc/2 + h0 với mọi cọc (h0 = {h0 * 1000:0} mm)",
                    Explanation = "Tất cả cọc nằm trong đáy tháp chọc thủng 45° của cột: lực truyền trực tiếp qua thanh chống bê tông chịu nén, F = 0 – đài tự động an toàn về chọc thủng do cột (không cần kiểm tra)."
                });
            else
            {
                var it = SafetyItem.Make("Chọc thủng", "Chọc thủng đài do cột F ≤ Fb,u", fCol, pd.Fbu, "kN", formula,
                    "Bê tông đài đủ chống chọc thủng của cột qua tháp 45° tới các hàng cọc.",
                    "Đài bị cột chọc thủng – cần tăng chiều cao đài (h0).");
                if (it.Status != CheckStatus.Ok)
                {
                    if (inp.FixedHeight)
                    {
                        if (fCol > 2 * pd.Fbu + 1e-6)
                        {
                            pd.NeedHeight = true;
                            it.Explanation = $"F = {fCol:0} kN > 2Fb,u = {2 * pd.Fbu:0} kN: cốt thép chống chọc thủng không đủ (8.1.6.3) – cần tăng chiều cao đài.";
                            rep.Warnings.Add("LỖI chọc thủng đài do cột: F > 2Fb,u – cần tăng chiều cao đài (chiều cao đang cố định "
                                             + (inp.H * 1000).ToString("0") + " mm), gợi ý H ≥ " + (RequiredHeight(inp) * 1000).ToString("0") + " mm");
                        }
                        else
                        {
                            PunchReinforcement(pd, fCol, inp.Rsw);
                            it.Status = CheckStatus.Warning;
                            it.Limit = pd.Fbu + pd.Fswu;
                            it.Formula += $";  có cốt thép chống chọc thủng: F ≤ Fb,u + Fsw,u = {pd.Fbu:0} + {pd.Fswu:0} kN (8.1.6.3)";
                            it.Explanation = $"Chiều cao đài cố định nên bê tông không đủ: cần cốt thép chống chọc thủng {pd.Reinforcement} (Asw/sw ≥ {pd.AswPerM * 1e4:0.00} cm²/m chu vi), "
                                             + "đặt thành 3 hàng quanh chu vi cách mép cột h0/3 … 1,5h0, neo móc vào thép lớp trên và lớp dưới. Nên ưu tiên tăng chiều cao đài.";
                            rep.Warnings.Add("Cảnh báo: chọc thủng đài do cột cần cốt thép chống chọc thủng " + pd.Reinforcement + " (hoặc tăng chiều cao đài lên ≥ "
                                             + (RequiredHeight(inp) * 1000).ToString("0") + " mm)");
                        }
                    }
                    else
                    {
                        pd.NeedHeight = true;
                        rep.Warnings.Add("LỖI chọc thủng đài do cột không đạt với chiều cao tối đa – cần tăng chiều cao / mác bê tông đài");
                    }
                }
                rep.Items.Add(it);
            }
        }

        // ---- chọc thủng cọc (cọc góc / cọc biên) ----
        if (np > 1)
        {
            int worst = -1; double wr = -1, wu = 0;
            for (int i = 0; i < np; i++)
            {
                if (rows[i].InsidePyramid) continue;
                double u = PilePerimeter(piles[i].X, piles[i].Y, d, h0, inp.L, inp.B);
                double cap = rbt * Math.Min(u, 4 * (d + h0)) * h0;
                double ratio = cap > 1e-9 ? pileNetMax[i] / cap : 99;
                if (ratio > wr) { wr = ratio; worst = i; wu = Math.Min(u, 4 * (d + h0)); }
            }
            if (worst >= 0)
                rep.Items.Add(SafetyItem.Make("Chọc thủng", $"Chọc thủng đài do cọc P{worst + 1} (góc / biên)", pileNetMax[worst], rbt * wu * h0, "kN",
                    $"F = Pi − G/n ≤ Rbt·u·h0, u = {wu * 1000:0} mm (đường bao cách mép cọc h0/2, bỏ cạnh ra ngoài mép đài)",
                    "Phần đài phía trên cọc góc đủ chống chọc thủng ngược lên của cọc.",
                    "Cọc góc chọc thủng mép đài – tăng chiều cao đài hoặc tăng khoảng cách mép cọc – mép đài."));
        }

        // ---- cắt trên tiết diện nghiêng ----
        void Shear(bool alongX, double q)
        {
            if (q <= 1e-9) return;
            double colDim = alongX ? hc : bc, b = alongX ? inp.B : inp.L;
            double clear = double.MaxValue;
            foreach (var p in piles)
            {
                double o = Math.Abs(alongX ? p.X : p.Y) - colDim / 2;
                if (o > 1e-6) clear = Math.Min(clear, Math.Max(o - d / 2, 0));
            }
            if (clear == double.MaxValue) return;
            double qb = ShearCapacity(b, h0, clear, rbt);
            rep.Items.Add(SafetyItem.Make("Cắt", "Cắt tiết diện nghiêng tại mép cột phương " + (alongX ? "L (X)" : "B (Y)"), q, qb, "kN",
                $"Q = Σ(Pi − G/n) một phía ≤ Qb = 1,5·Rbt·b·h0²/c, c = {clear * 1000:0} mm, 0,5 ≤ Qb/(Rbt·b·h0) ≤ 2,5 (TCVN 5574:2018 mục 8.1.3.3)",
                clear < h0 ? "Hàng cọc gần mép cột (c < h0): khe nứt nghiêng dốc, bê tông chịu cắt rất tốt (thanh chống nén)." : "Bê tông đài đủ chịu cắt không cần cốt đai.",
                "Đài không đủ chịu cắt trên tiết diện nghiêng – tăng chiều cao đài."));
        }
        Shear(true, Math.Max(qxPos, qxNeg));
        Shear(false, Math.Max(qyPos, qyNeg));

        // ---- lún nhóm cọc ----
        if (inp.Borehole != null)
        {
            try
            {
                double lg = piles.Max(q => q.X) - piles.Min(q => q.X) + d, bg = piles.Max(q => q.Y) - piles.Min(q => q.Y) + d;
                double lp = Math.Max(inp.PileLength, 1);
                double zq = inp.Df + 2.0 / 3.0 * lp;
                double lq = lg + lp / 3, bq = bg + lp / 3;
                double ntc = Math.Max(nMax, 0) / Math.Max(inp.LoadFactor, 1.0);
                double pq = GeotechCalc.EffectiveStress(inp.Borehole, zq) + ntc / (lq * bq);
                var st = GeotechCalc.Settlement(inp.Borehole, lq, bq, zq, pq);
                rep.Items.Add(SafetyItem.Make("Nền", "Lún nhóm cọc (móng khối quy ước) S ≤ Sgh", st.S * 100, st.Limit * 100, "cm",
                    $"Móng khối quy ước tại độ sâu {zq:0.0} m (2/3 chiều dài cọc), {lq:0.00} × {bq:0.00} m (mở rộng 1:4), p = σ'bt + Ntc/(Lq·Bq) = {pq:0.0} kPa; hố khoan {inp.Borehole.Name}",
                    "Độ lún của nhóm cọc (móng khối quy ước) nhỏ hơn giới hạn.",
                    "Lún nhóm cọc vượt giới hạn – kéo dài cọc tới lớp tốt hơn hoặc tăng khoảng cách cọc."));
            }
            catch (Exception ex) { rep.Warnings.Add("Cảnh báo: không tính được lún nhóm cọc – " + ex.Message); }
        }
        else
        {
            rep.Items.Add(new SafetyItem
            {
                Group = "Nền", Name = "Lún nhóm cọc", Status = CheckStatus.Info, Unit = "cm",
                Explanation = "Chưa có số liệu địa chất (hố khoan) – chưa kiểm tra lún nhóm cọc theo móng khối quy ước. Nhập hố khoan ở tab 'Địa chất'."
            });
            rep.Warnings.Add("Cảnh báo: chưa có số liệu địa chất – chưa kiểm tra lún nhóm cọc.");
        }

        // ---- sự làm việc ----
        double maxOver = piles.Max(q => Math.Max(Math.Abs(q.X) - hc / 2, Math.Abs(q.Y) - bc / 2));
        string beh = $"Đài {np} cọc ({inp.PatternName}) coi là tuyệt đối cứng: tải cột N, M, Q chia cho các cọc theo quan hệ tuyến tính với toạ độ cọc. ";
        if (np == 1) beh += "Đài 1 cọc chỉ truyền lực dọc; mô men chân cột phải do hệ giằng móng tiếp nhận.";
        else if (pd.AllInside) beh += "Mọi cọc nằm trong tháp chọc thủng 45° của cột – lực truyền xuống cọc qua các thanh chống bê tông chịu nén (mô hình giàn ảo), đài không bị chọc thủng; thép đáy làm việc như thanh giằng chịu kéo.";
        else beh += $"Hàng cọc xa nhất cách mép cột {maxOver * 1000:0} mm ({(h0 > 0 ? maxOver / h0 : 0):0.0}·h0): đài làm việc như bản công xôn ngàm tại mép cột, chịu uốn do phản lực cọc (thép đáy theo 2 phương), cắt trên tiết diện nghiêng và chọc thủng của cột / cọc góc.";
        if (pmin < 0) beh += " Có cọc chịu nhổ: thớ trên đài chịu kéo – bố trí thép lớp trên theo tính toán, neo thép đầu cọc vào đài.";
        if (inp.HStart > 0 && inp.H > inp.HStart + 1e-6)
            beh += $" Chiều cao đài đã tự tăng từ {inp.HStart * 1000:0} lên {inp.H * 1000:0} mm để thoả chọc thủng / cắt.";
        rep.Behaviour = beh;
        return rep;
    }

    /// <summary>Cốt thép chống chọc thủng (8.1.6.3): qsw,yc = max(F − Fb,u; 0,25Fb,u)/(0,8u); Asw/sw = qsw/Rsw;
    /// chọn Ø10…Ø16, 3 hàng quanh chu vi, bước theo chu vi a ≤ h0/3 (≥ 50 mm, làm tròn xuống 25 mm). Fb,u + Fsw,u ≤ 2Fb,u.</summary>
    public static void PunchReinforcement(PunchingDetail pd, double F, double rsw)
    {
        if (pd == null || pd.U <= 0 || rsw <= 0) return;
        double need = Math.Max(F - pd.Fbu, 0.25 * pd.Fbu);
        double qsw = need / (0.8 * pd.U);
        pd.AswPerM = qsw / rsw;
        const int rows = 3;
        double aMax = Math.Max(0.05, Math.Min(pd.H0 / 3, 0.30));
        foreach (int dia in new[] { 10, 12, 14, 16 })
        {
            double a = RcSection.FloorTo(rows * RcSection.BarArea(dia) / pd.AswPerM, 0.025);
            a = Math.Min(a, RcSection.FloorTo(aMax, 0.025));
            if (a < 0.05 - 1e-9) continue;
            pd.Reinforcement = $"Ø{dia}a{a * 1000:0} × {rows} hàng";
            pd.Fswu = Math.Min(0.8 * rsw * rows * RcSection.BarArea(dia) / a * pd.U, pd.Fbu);
            return;
        }
        pd.Reinforcement = "Ø16a50 × 3 hàng (không đủ – tăng chiều cao đài)";
        pd.Fswu = Math.Min(0.8 * rsw * 3 * RcSection.BarArea(16) / 0.05 * pd.U, pd.Fbu);
        pd.NeedHeight = pd.Fbu + pd.Fswu < F;
    }

    /// <summary>Chiều cao đài nhỏ nhất (bước 50 mm, ≤ 3 m) thoả chọc thủng cột, chọc thủng cọc và cắt (giữ khoảng H − h0 và G).</summary>
    public static double RequiredHeight(PileCapInput inp, double hMax = PileCapDesigner.MaxHeight)
    {
        double off = Math.Max(inp.H - inp.H0, 0.05);
        double H = Math.Max(inp.H, off + 0.1);
        for (int it = 0; it < 200 && H <= hMax + 1e-9; it++)
        {
            var t = Copy(inp);
            t.H = H; t.H0 = H - off; t.FixedHeight = false; t.Borehole = null;
            var r = Evaluate(t);
            if (r.Items.Where(x => x.Group == "Chọc thủng" || x.Group == "Cắt").All(x => x.Status != CheckStatus.Fail)) return H;
            H = Math.Round(H + 0.05, 3);
        }
        return hMax;
    }

    private static PileCapInput Copy(PileCapInput a) => new()
    {
        Piles = a.Piles.ToList(), Loads = a.Loads.ToList(), G = a.G, H = a.H, H0 = a.H0, Hc = a.Hc, Bc = a.Bc, D = a.D, Round = a.Round, L = a.L, B = a.B,
        P = a.P, Pt = a.Pt, Pvl = a.Pvl, HAllow = a.HAllow, HAllowEstimated = a.HAllowEstimated, Rbt = a.Rbt, Rsw = a.Rsw, FixedHeight = a.FixedHeight,
        UserLayout = a.UserLayout, HStart = a.HStart, PatternName = a.PatternName, Borehole = a.Borehole, Df = a.Df, PileLength = a.PileLength, LoadFactor = a.LoadFactor
    };

    // ------------------------------------------------------------------
    /// <summary>Gắn báo cáo kiểm tra an toàn vào đài đã thiết kế (gọi cuối PileCapDesigner.DesignOne):
    /// d.Safety, cập nhật hạng mục chọc thủng do cột theo Fb,u có kể h0/c (và cốt thép chống chọc thủng khi chiều cao cố định),
    /// thêm thông báo "LỖI …" cho hạng mục không đạt chưa có trong Checks, "Cảnh báo: …" cho cảnh báo.</summary>
    internal static void Attach(Project p, PileCapDesign d, List<BaseLoad> loads, FoundationSettings fs, PileCapacityResult geo,
        PileCapOverride ovr, double hStart, bool fixedHeight, Material conc)
    {
        if (d == null || d.Piles == null || d.Piles.Count == 0) return;
        var stir = SafeMaterial(p, p?.Concrete?.RebarStirrup);
        var main = SafeMaterial(p, p?.Concrete?.RebarMain);
        double pvl = geo?.Pvl ?? 0;
        if (pvl <= 0)
        {
            Material pc;
            try { pc = Material.Concrete("B25"); } catch (Exception) { pc = conc; }
            double ab = d.PileRound ? Math.PI * d.PileSize * d.PileSize / 4 : d.PileSize * d.PileSize;
            double As = fs.PileRebarArea > 0 ? fs.PileRebarArea : GeotechCalc.DefaultRebarArea(d.PileSize);
            pvl = pc.Rb * ab + (main?.Rsc ?? 350000) * As;
        }
        Borehole bh = null;
        try { bh = GeotechCalc.SelectBorehole(p, d.X, d.Y); } catch (Exception) { bh = null; }
        bool user = PileLayout.IsActive(ovr);
        string pat = user && ovr.Pattern != PilePattern.Auto ? PileLayout.Name(ovr.Pattern) + (ovr.Pattern == PilePattern.Grid ? $" {ovr.Nx}×{ovr.Ny}" : "")
                                                            : PatternOf(d.Piles.Count) + " (tự chọn)";
        var inp = new PileCapInput
        {
            Piles = d.Piles.ToList(), Loads = loads, G = d.Weight, H = d.H, H0 = d.H0, Hc = d.ColumnH, Bc = d.ColumnB, D = d.PileSize, Round = d.PileRound,
            L = d.L, B = d.B, P = d.PileCapacity, Pt = Math.Max(fs.PileTension, 0), Pvl = pvl, HAllow = fs.PileLateralCapacity, HAllowEstimated = fs.PileLateralCapacity <= 0,
            Rbt = conc.Rbt, Rsw = stir?.Rsw > 0 ? stir.Rsw : 170000, FixedHeight = fixedHeight, UserLayout = user, HStart = hStart, PatternName = pat,
            Borehole = bh, Df = d.Depth, PileLength = d.PileLength, LoadFactor = Math.Max(p?.Soil?.LoadFactorAvg ?? 1.15, 1.0)
        };
        var rep = Evaluate(inp);
        d.Safety = rep;

        // hạng mục chọc thủng do cột trong Checks: dùng Fb,u có kể h0/c (+ Fsw,u khi có cốt thép chống chọc thủng)
        var ck = d.Checks.FirstOrDefault(c => c.Name.StartsWith("Chọc thủng đài do cột"));
        if (ck != null && rep.Punching != null && d.Piles.Count > 1)
        {
            ck.Demand = rep.Punching.F;
            ck.Capacity = rep.Punching.Fbu + (rep.Punching.NeedHeight ? 0 : rep.Punching.Fswu);
            ck.Note = $"H = {d.H * 1000:0} mm, h0 = {d.H0 * 1000:0} mm, k1 = {rep.Punching.K1:0.00}, k2 = {rep.Punching.K2:0.00}"
                      + (rep.Punching.AllInside ? "; mọi cọc trong tháp 45° (F = 0)" : "")
                      + (rep.Punching.Fswu > 0 && !rep.Punching.NeedHeight ? "; cốt thép chống chọc thủng " + rep.Punching.Reinforcement : "");
        }
        // giải thích cho các hạng mục uốn đã có trong Checks
        foreach (var c in d.Checks.Where(c => c.Name.StartsWith("Uốn")))
            rep.Items.Add(new SafetyItem
            {
                Group = "Uốn", Name = c.Name, Value = c.Demand, Limit = c.Capacity, Unit = c.Unit, Status = c.Ok ? CheckStatus.Ok : CheckStatus.Fail,
                Formula = "M = Σ Pi·(khoảng cách tim cọc – mép cột); thép " + c.Note,
                Explanation = c.Name.Contains("lớp trên")
                    ? "Cọc chịu nhổ kéo đài xuống ngược chiều: thớ trên đài căng – thép lớp trên chịu mô men này."
                    : "Phản lực cọc làm đài uốn như công xôn ngàm tại mép cột: thép đáy chịu kéo, bẻ móc lên hai đầu để neo."
            });
        if (d.Piles.Count > 1 && !string.IsNullOrEmpty(d.BarsL))
        {
            int dia = Math.Min(d.DiaL > 0 ? d.DiaL : 12, d.DiaB > 0 ? d.DiaB : 12);
            double sp = Math.Max(d.SpacingL, d.SpacingB);
            rep.Items.Add(new SafetyItem
            {
                Group = "Uốn", Name = "Cấu tạo thép đáy đài Ø ≥ 12, bước 100…200 mm", Value = dia, Limit = 12, Unit = "mm", Inverse = true,
                Status = dia >= 12 && sp <= 0.20 + 1e-9 && (sp >= 0.10 - 1e-9 || sp <= 0) ? CheckStatus.Ok : CheckStatus.Warning,
                Formula = $"thép đáy {d.BarsL} / {d.BarsB}",
                Explanation = "Lưới thép đáy đặt trên đầu cọc (phía trên đoạn cọc ngàm), bẻ móc lên tới gần mặt trên đài để neo; bước 100…200 mm cho bê tông đổ lọt và phân bố vết nứt."
            });
        }
        foreach (var it in rep.Items.Where(i => i.Status == CheckStatus.Fail && (i.Group == "Bố trí cọc" || i.Group == "Nền" || i.Group == "Nhóm cọc"
                                                                             || i.Group == "Tải ngang" || i.Name.Contains("Pvl") || i.Name.Contains("do cọc P"))))
            d.Messages.Add("LỖI " + it.Name + ": " + it.Explanation);
        foreach (var w in rep.Warnings.Where(w => !d.Messages.Contains(w))) d.Messages.Add(w);
        if (rep.HFinal > rep.HStart + 1e-6 && rep.HStart > 0)
            d.Messages.Add($"Chiều cao đài tăng từ {rep.HStart * 1000:0} lên {rep.HFinal * 1000:0} mm để thoả chọc thủng / cắt");
        if (user) d.Messages.Add("Bố trí cọc theo lựa chọn của người dùng: " + pat
                                 + (ovr.L > 0 || ovr.B > 0 ? $", đài {Math.Max(ovr.L, 0) * 1000:0}×{Math.Max(ovr.B, 0) * 1000:0}" : "")
                                 + (ovr.H > 0 ? $", H = {ovr.H * 1000:0} mm (cố định)" : ""));
    }

    /// <summary>Tên sơ đồ theo số cọc của bộ tự chọn.</summary>
    public static string PatternOf(int n) => n switch
    {
        1 => "1 cọc", 2 => "2 cọc thẳng hàng", 3 => "3 cọc tam giác", 4 => "4 cọc hình vuông", 5 => "5 cọc hoa mai", 6 => "6 cọc 2×3",
        8 => "8 cọc (3×3 bỏ giữa)", 9 => "9 cọc 3×3", _ => n + " cọc lưới"
    };

    private static Material SafeMaterial(Project p, string name)
    {
        if (p == null || string.IsNullOrEmpty(name)) return null;
        try { return p.Material(name); } catch (Exception) { return null; }
    }
}
