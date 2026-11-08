using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

// =====================================================================
// CỌC KHOAN NHỒI – PHẦN NÂNG CAO (dùng chung chế độ Thiết kế và Triển khai hồ sơ)
//  • Quy định cấu tạo: khoảng cách tim cọc s ≥ 3D (D < 0,8 m); s ≥ max(2,5D; D + 1,0 m) (D ≥ 0,8 m);
//    mép cọc – mép đài ≥ max(150 mm; D/3) (D < 0,8 m) / max(250 mm; D/3) (D ≥ 0,8 m) (TCVN 10304:2014 mục 8.13, thực hành).
//  • Đường cong sức chịu tải Rc,u(L) và tự chọn chiều dài cọc: L nhỏ nhất (bước 0,5 m) để [P] = min([P]đ; Pvl) ≥ [P] yêu cầu
//    và mũi cọc nằm trong lớp tốt (cát N ≥ 15; đất dính IL ≤ 0,5 hoặc N ≥ 15; hoặc có qb nhập tay).
//  • Ma sát âm (đất yếu cố kết / đất đắp nền): các đoạn thân cọc trên mặt phẳng trung hoà (lấy thiên an toàn = đáy lớp cố kết sâu nhất)
//    không kể ma sát dương; lực kéo xuống Pn = u·Σ fi·li (γcf = 1) cộng vào tải: [P]đ,net = γ0·(Rc,u − ΣQ trên mặt trung hoà)/(γn·γk) − Pn.
//  • Hệ số nhóm Converse–Labarre (cọc ma sát): η = 1 − θ·[(n1 − 1)·n2 + (n2 − 1)·n1]/(90·n1·n2), θ = arctan(D/s) (độ).
//  • Đài cọc sâu – mô hình thanh chống – giằng (Blévot): lực ngang hướng tâm tại đầu cọc Hr = R·a/z, a = r − k·c
//    (k = 0,25 đài 2 cọc; 0,30 đài 3 cọc; √2/4 đài 4 cọc), z = h0; giằng: 2 cọc T = Hr; 3 cọc (giằng theo cạnh) T = Hr/√3;
//    4 cọc (giằng theo cạnh) T = Hr/√2. Kiểm tra góc thanh chống θ = arctan(z/a) ∈ [40°; 55°], ứng suất nút
//    cột σ = N/(Ac·sin²θ) ≤ ν'·Rb, nút đầu cọc σ = R/(Ap·sin²θ) ≤ 0,85·ν'·Rb, ν' = 1 − fck/250 (EN 1992-1-1 mục 6.5.4 – tham khảo).
//  • Lồng thép dài > 11,7 m: nối chồng 40d, so le 50 % số thanh, khoảng so le ≥ 1,3·lap. Con kê mỗi 2 m, 4/6/8 cái mỗi tầng theo D.
//  • Ống siêu âm (TCVN 9396:2012, thực hành): D < 0,8 m không bắt buộc; 0,8 ≤ D < 1,0 m: 2 ống; 1,0 ≤ D ≤ 1,5 m: 3 ống; D > 1,5 m: 4 ống.
//  • Lún nhóm cọc – móng tương đương ở độ sâu 2/3·L (Tomlinson), phân bố 2:1, dừng khi σz ≤ 0,2·σbt.
// Đơn vị: kN, m, kPa.
// =====================================================================

/// <summary>Một điểm của đường cong sức chịu tải theo chiều dài cọc.</summary>
public sealed class CapacityCurvePoint
{
    public double Length { get; set; }
    public double Tip { get; set; }
    public double Rcu { get; set; }
    public double Psoil { get; set; }
    public double Allowable { get; set; }
    public string TipLayer { get; set; } = "";
    public bool GoodTip { get; set; }
}

/// <summary>Kết quả tự chọn chiều dài cọc.</summary>
public sealed class AutoLengthResult
{
    public double Length { get; set; }
    public BoredPileResult Result { get; set; }
    public bool Ok { get; set; }
    public string Text { get; set; } = "";
    public List<CapacityCurvePoint> Curve { get; set; } = new();
}

/// <summary>Kết quả ma sát âm.</summary>
public sealed class NegativeFrictionResult
{
    public double NeutralDepth { get; set; }
    /// <summary>Phần ma sát dương bị loại bỏ (u·Σγcf·fi·li trên mặt trung hoà), kN</summary>
    public double ShaftLost { get; set; }
    /// <summary>Lực kéo xuống Pn = u·Σ fi·li (γcf = 1), kN</summary>
    public double Pn { get; set; }
    public double RcuNet { get; set; }
    public double PsoilNet { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>Kết quả mô hình thanh chống – giằng đài cọc.</summary>
public sealed class StrutTieResult
{
    public int Piles { get; set; }
    public double Reaction { get; set; }
    public double Radius { get; set; }
    public double A { get; set; }
    public double Z { get; set; }
    public double ThetaDeg { get; set; }
    public double Hr { get; set; }
    public double Tie { get; set; }
    public double AsReq { get; set; }
    public double SigmaColNode { get; set; }
    public double SigmaPileNode { get; set; }
    public double LimitColNode { get; set; }
    public double LimitPileNode { get; set; }
    public double ShearSpanRatio { get; set; }
    public bool Deep { get; set; }
    public List<string> Calc { get; } = new();
}

/// <summary>Chia lồng thép dài thành các đoạn nối chồng.</summary>
public sealed class CageSpliceResult
{
    public double CageLength { get; set; }
    public double Lap { get; set; }
    public double Stagger { get; set; }
    public List<double> Segments { get; } = new();
    /// <summary>Độ sâu (từ đầu cọc) bắt đầu các mối nối nhóm 1 (50 % số thanh); nhóm 2 lệch thêm Stagger.</summary>
    public List<double> SpliceDepths { get; } = new();
    /// <summary>Tổng chiều dài một thanh thép dọc kể đoạn nối (m)</summary>
    public double BarLength => Segments.Sum();
    public int Splices => Math.Max(Segments.Count - 1, 0);
}

public static class BoredPileDesign
{
    public const double StockLength = 11.7;
    public const double LapFactor = 40;

    /// <summary>Khoảng cách tim cọc tối thiểu (m).</summary>
    public static double MinSpacing(double d) => d < 0.8 - 1e-9 ? 3 * d : Math.Max(2.5 * d, d + 1.0);

    /// <summary>Khoảng hở mép cọc – mép đài tối thiểu (m).</summary>
    public static double MinEdgeClear(double d) => d < 0.8 - 1e-9 ? Math.Max(0.15, d / 3) : Math.Max(0.25, d / 3);

    /// <summary>Số ống siêu âm kiểm tra chất lượng bê tông cọc theo đường kính.</summary>
    public static int SonicTubes(double d) => d < 0.8 - 1e-9 ? 0 : d < 1.0 - 1e-9 ? 2 : d <= 1.5 + 1e-9 ? 3 : 4;

    /// <summary>Số con kê mỗi tầng theo đường kính.</summary>
    public static int SpacersPerLevel(double d) => d < 0.8 - 1e-9 ? 4 : d < 1.2 - 1e-9 ? 6 : 8;

    /// <summary>Số tầng con kê (mỗi 2 m, cả đầu và chân lồng).</summary>
    public static int SpacerLevels(double cage, double step = 2.0) => Math.Max(1, (int)Math.Floor(Math.Max(cage, 0) / Math.Max(step, 0.5) + 1e-9) + 1);

    /// <summary>Hệ số hao hụt bê tông gợi ý theo địa chất thân cọc (bình quân theo bề dày): đất yếu / hữu cơ (IL &gt; 0,75 hoặc cố kết) 1,20;
    /// cát 1,15; đất dính dẻo cứng – nửa cứng 1,10. Làm tròn 0,05.</summary>
    public static double OvercutFactor(IList<PileSoilLayer> layers, double head, double length)
    {
        if (layers == null || layers.Count == 0 || length <= 0) return 1.15;
        double t = 0, sum = 0, w = 0;
        foreach (var l in layers)
        {
            double a = t, b = t + Math.Max(l.Thickness, 0);
            t = b;
            double lo = Math.Max(a, head), hi = Math.Min(b, head + length);
            if (hi <= lo) continue;
            double f = l.Consolidating || (!l.IsSand && l.IL > 0.75) ? 1.20 : l.IsSand ? 1.15 : 1.10;
            sum += f * (hi - lo); w += hi - lo;
        }
        if (w <= 1e-9) return 1.15;
        return Math.Round(sum / w / 0.05, MidpointRounding.AwayFromZero) * 0.05;
    }

    /// <summary>Lớp mũi tốt: có qb nhập tay; cát N ≥ 15; đất dính IL ≤ 0,5 hoặc N ≥ 15.</summary>
    public static bool GoodTip(PileSoilLayer l) =>
        l != null && (l.QbUser > 0 || (l.IsSand ? l.NSpt >= 15 - 1e-9 : (l.IL <= 0.5 + 1e-9 || l.NSpt >= 15 - 1e-9)));

    /// <summary>Đường cong sức chịu tải theo chiều dài cọc L = minL, minL + step, …, maxL.</summary>
    public static List<CapacityCurvePoint> Curve(IList<PileSoilLayer> layers, double d, double head, double minL, double maxL, double step,
        string method, string drill, double pvl, double gammaCf = 0, double g0 = 1.15, double gn = 1.15, double gk = 1.4)
    {
        var res = new List<CapacityCurvePoint>();
        if (layers == null || layers.Count == 0 || d <= 0) return res;
        step = Math.Max(step, 0.1);
        for (double L = Math.Max(minL, step); L <= maxL + 1e-9; L = Math.Round(L + step, 6))
        {
            var bp = BoredPile.Capacity(layers, d, head, L, method, drill, gammaCf, g0, gn, gk);
            var tl = BoredPile.LayerAt(layers, head + L - 1e-6, out _);
            res.Add(new CapacityCurvePoint
            {
                Length = L, Tip = head + L, Rcu = bp.Rcu, Psoil = bp.Psoil, Allowable = pvl > 0 ? Math.Min(bp.Psoil, pvl) : bp.Psoil,
                TipLayer = tl?.Name ?? "", GoodTip = GoodTip(tl)
            });
        }
        return res;
    }

    /// <summary>Tổng bề dày các lớp đất (m).</summary>
    public static double TotalDepth(IList<PileSoilLayer> layers) => layers?.Sum(l => Math.Max(l.Thickness, 0)) ?? 0;

    /// <summary>Tự chọn chiều dài cọc: L nhỏ nhất (bước step) có [P] ≥ preq và mũi trong lớp tốt. Không đạt: lấy L cho [P] lớn nhất (Ok = false).</summary>
    public static AutoLengthResult AutoLength(IList<PileSoilLayer> layers, double d, double head, double preq, double pvl,
        string method, string drill, double gammaCf = 0, double g0 = 1.15, double gn = 1.15, double gk = 1.4,
        double minL = 0, double maxL = 0, double step = 0.5)
    {
        var a = new AutoLengthResult();
        if (minL <= 0) minL = d < 0.8 - 1e-9 ? 6 : 10;
        double avail = TotalDepth(layers) - head - 0.5;   // mũi cách đáy hố khoan ≥ 0,5 m
        if (maxL <= 0) maxL = Math.Max(avail, minL);
        maxL = Math.Max(Math.Min(maxL, Math.Max(avail, minL)), minL);
        a.Curve = Curve(layers, d, head, minL, maxL, step, method, drill, pvl, gammaCf, g0, gn, gk);
        if (a.Curve.Count == 0) { a.Text = "Không đủ số liệu địa chất để chọn chiều dài cọc"; return a; }
        var ok = a.Curve.FirstOrDefault(c => c.Allowable >= preq - 1e-6 && c.GoodTip);
        CapacityCurvePoint pick;
        if (ok != null) { pick = ok; a.Ok = true; }
        else
        {
            pick = a.Curve.OrderByDescending(c => c.Allowable).ThenBy(c => c.Length).First();
            a.Ok = false;
        }
        a.Length = pick.Length;
        a.Result = BoredPile.Capacity(layers, d, head, pick.Length, method, drill, gammaCf, g0, gn, gk);
        a.Text = a.Ok
            ? $"Tự chọn chiều dài cọc: L = {pick.Length:0.0#} m (mũi −{pick.Tip:0.0#} m trong lớp {pick.TipLayer}) – nhỏ nhất (bước {step:0.0#} m) để [P] = {pick.Allowable:0} kN ≥ [P] yêu cầu {preq:0} kN và mũi nằm trong lớp tốt"
            : $"CẢNH BÁO không chọn được chiều dài cọc đạt [P] ≥ {preq:0} kN với mũi trong lớp tốt (L ≤ {maxL:0.0#} m" + (pvl > 0 && pvl < preq ? $"; Pvl = {pvl:0} kN < [P] yêu cầu – tăng D hoặc mác bê tông / thép" : "")
              + $") – lấy L = {pick.Length:0.0#} m có [P] lớn nhất = {pick.Allowable:0} kN";
        return a;
    }

    /// <summary>Ma sát âm: loại bỏ ma sát dương phía trên mặt trung hoà (đáy lớp cố kết sâu nhất trong phạm vi thân cọc)
    /// và cộng lực kéo xuống Pn = u·Σ fi·li (γcf = 1). Trả về null nếu không có lớp cố kết.</summary>
    public static NegativeFrictionResult NegativeFriction(BoredPileResult r, IList<PileSoilLayer> layers)
    {
        if (r == null || layers == null || !layers.Any(l => l.Consolidating)) return null;
        double t = 0, neutral = 0;
        foreach (var l in layers) { t += Math.Max(l.Thickness, 0); if (l.Consolidating) neutral = t; }
        neutral = Math.Min(neutral, r.Tip);
        var nf = new NegativeFrictionResult { NeutralDepth = neutral };
        if (neutral <= r.Head + 1e-9) { nf.RcuNet = r.Rcu; nf.PsoilNet = r.Psoil; nf.Text = "Lớp cố kết nằm trên đầu cọc – không có ma sát âm"; return nf; }
        foreach (var row in r.Rows.Where(x => x.To <= neutral + 1e-9))
        {
            nf.ShaftLost += row.Q;
            nf.Pn += r.U * row.Fi * row.Len;
        }
        // đoạn bị cắt qua mặt trung hoà
        foreach (var row in r.Rows.Where(x => x.From < neutral - 1e-9 && x.To > neutral + 1e-9))
        {
            double f = (neutral - row.From) / Math.Max(row.Len, 1e-9);
            nf.ShaftLost += row.Q * f;
            nf.Pn += r.U * row.Fi * (neutral - row.From);
        }
        nf.RcuNet = r.Rcu - nf.ShaftLost;
        nf.PsoilNet = r.Gamma0 * nf.RcuNet / Math.Max(r.GammaN * r.GammaK, 1e-9) - nf.Pn;
        nf.Text = $"Ma sát âm (đất yếu cố kết / đất đắp): mặt trung hoà lấy tại đáy lớp cố kết sâu nhất z = {neutral:0.0#} m; bỏ ma sát dương phía trên ΣQ = {nf.ShaftLost:0.0} kN; "
                  + $"lực kéo xuống Pn = u·Σfi·li = {nf.Pn:0.0} kN → [P]đ,net = γ0·(Rc,u − ΣQ)/(γn·γk) − Pn = {r.Gamma0:0.00}·{nf.RcuNet:0.0}/({r.GammaN:0.00}·{r.GammaK:0.00}) − {nf.Pn:0.0} = {nf.PsoilNet:0.0} kN (TCVN 10304:2014 – ma sát âm)";
        return nf;
    }

    /// <summary>Hệ số nhóm Converse–Labarre cho nhóm n1 × n2 cọc, đường kính d, khoảng cách s.</summary>
    public static double GroupEfficiency(int n1, int n2, double d, double s)
    {
        if (n1 < 1 || n2 < 1 || s <= 1e-9) return 1;
        if (n1 * n2 == 1) return 1;
        double theta = Math.Atan(d / s) * 180 / Math.PI;
        return 1 - theta * ((n1 - 1) * n2 + (n2 - 1) * n1) / (90.0 * n1 * n2);
    }

    /// <summary>Số hàng cọc theo X và Y (toạ độ khác nhau, dung sai 1 cm).</summary>
    public static (int Nx, int Ny) Rows(IList<(double X, double Y)> piles)
    {
        if (piles == null || piles.Count == 0) return (0, 0);
        int nx = piles.Select(p => Math.Round(p.X, 2)).Distinct().Count(), ny = piles.Select(p => Math.Round(p.Y, 2)).Distinct().Count();
        // nhóm so le / tam giác: quy về hàng tương đương sao cho nx·ny ≈ n
        if (nx * ny > piles.Count) { ny = Math.Max(1, (int)Math.Round(piles.Count / (double)nx)); }
        return (Math.Max(nx, 1), Math.Max(ny, 1));
    }

    /// <summary>Hệ số k của bề rộng cột trong cánh tay đòn a = r − k·c (Blévot).</summary>
    public static double BlevotK(int n) => n == 2 ? 0.25 : n == 3 ? 0.30 : n == 4 ? Math.Sqrt(2) / 4 : 0.25;

    /// <summary>Thanh chống – giằng đài 2/3/4 cọc (Blévot). n: số cọc đều trên vòng bán kính r (m) quanh cột; c: cạnh cột tương đương (m);
    /// z = h0 (m); Ac, Ap: diện tích cột, cọc (m²); rb, rs (kPa); fckMPa: cường độ đặc trưng bê tông (MPa) cho ν'.</summary>
    public static StrutTieResult StrutTie(double N, int n, double r, double c, double z, double ac, double ap, double rb, double rs, double fckMPa)
    {
        var st = new StrutTieResult { Piles = n, Radius = r, Z = Math.Max(z, 1e-6) };
        if (n < 2 || N <= 0) return st;
        st.Reaction = N / n;
        st.A = Math.Max(r - BlevotK(n) * c, 0.05);
        st.ThetaDeg = Math.Atan(st.Z / st.A) * 180 / Math.PI;
        st.Hr = st.Reaction * st.A / st.Z;
        st.Tie = n == 3 ? st.Hr / Math.Sqrt(3) : n == 4 ? st.Hr / Math.Sqrt(2) : st.Hr;
        st.AsReq = st.Tie / Math.Max(rs, 1e-9);
        double s2 = Math.Pow(Math.Sin(st.ThetaDeg * Math.PI / 180), 2);
        double nu = 1 - Math.Max(fckMPa, 0) / 250;
        st.SigmaColNode = N / Math.Max(ac * s2, 1e-9);
        st.SigmaPileNode = st.Reaction / Math.Max(ap * s2, 1e-9);
        st.LimitColNode = nu * rb;
        st.LimitPileNode = 0.85 * nu * rb;
        st.ShearSpanRatio = Math.Max(r - c / 2, 0) / st.Z;
        st.Deep = st.ShearSpanRatio <= 1.0 + 1e-9;
        string tieTxt = n == 2 ? "T = Hr" : n == 3 ? "giằng theo cạnh T = Hr/√3" : "giằng theo cạnh T = Hr/√2";
        st.Calc.Add($"Thanh chống – giằng (Blévot) đài {n} cọc: R = N/n = {st.Reaction:0.0} kN, r = {r:0.000} m, a = r − {BlevotK(n):0.###}·c = {st.A:0.000} m, z = h0 = {st.Z:0.000} m, "
                    + $"θ = arctan(z/a) = {st.ThetaDeg:0.0}°; Hr = R·a/z = {st.Hr:0.0} kN; {tieTxt} = {st.Tie:0.0} kN → As,giằng = T/Rs = {st.AsReq * 1e4:0.00} cm²");
        st.Calc.Add($"Nút cột σ = N/(Ac·sin²θ) = {st.SigmaColNode / 1000:0.00} MPa ≤ ν'·Rb = {st.LimitColNode / 1000:0.00} MPa; nút đầu cọc σ = R/(Ap·sin²θ) = {st.SigmaPileNode / 1000:0.00} MPa ≤ 0,85·ν'·Rb = {st.LimitPileNode / 1000:0.00} MPa (ν' = 1 − fck/250 = {nu:0.000}); "
                    + $"a/h0 = {st.ShearSpanRatio:0.00} {(st.Deep ? "≤ 1 – đài sâu, mô hình thanh chống – giằng quyết định" : "> 1 – đài làm việc như dầm, kiểm tra thêm thanh chống – giằng")}");
        return st;
    }

    /// <summary>Kích thước tối thiểu đài 1 cọc: cạnh ≥ max(D + 2·mép; cạnh cột lớn nhất + 0,2 m); H ≥ max(0,6 m; neo râu thép + ngàm + 0,1 m).</summary>
    public static (double Side, double H) SinglePileCapMin(double d, double colMax, double anchor, double embed) =>
        (Math.Max(d + 2 * MinEdgeClear(d), colMax + 0.2), Math.Max(0.6, anchor + embed + 0.10));

    /// <summary>Chia lồng thép thành các đoạn ≤ cây thép thương phẩm, nối chồng 40d (so le 50 %, lệch ≥ 1,3·lap).</summary>
    public static CageSpliceResult CageSplices(double cage, int barD, double stock = StockLength, double lapFactor = LapFactor)
    {
        var c = new CageSpliceResult { CageLength = Math.Max(cage, 0), Lap = Dt0(lapFactor * Math.Max(barD, 10) / 1000.0) };
        c.Stagger = Dt0(1.3 * c.Lap);
        if (c.CageLength <= stock + 1e-9) { c.Segments.Add(c.CageLength); return c; }
        double eff = stock - c.Lap;
        int n = (int)Math.Ceiling((c.CageLength - c.Lap) / Math.Max(eff, 0.5) - 1e-9);
        for (int i = 0; i < n - 1; i++) { c.Segments.Add(stock); c.SpliceDepths.Add(Dt0((i + 1) * eff)); }
        c.Segments.Add(Dt0(c.CageLength - (n - 1) * eff));
        return c;
    }

    static double Dt0(double v) => Math.Round(v * 100, MidpointRounding.AwayFromZero) / 100;

    /// <summary>Lún nhóm cọc theo móng tương đương đặt ở độ sâu head + 2L/3 (kích thước bằng nhóm cọc), phân bố 2:1.
    /// ntc: tải tiêu chuẩn (kN).</summary>
    public static PileSettlementResult SettlementEquivalentRaft(IList<PileSoilLayer> layers, double bg, double lg, double length, double head, double ntc)
    {
        var r = new PileSettlementResult { Bm = Math.Max(bg, 0.1), Lm = Math.Max(lg, 0.1) };
        double zr = head + 2 * length / 3;
        r.P = ntc / (r.Bm * r.Lm);
        r.P0 = r.P;
        r.SigmaBtTip = BoredPile.SigmaV(layers, zr);
        double h = Math.Max(0.2 * r.Bm, 0.2), z = 0, s = 0;
        for (int it = 0; it < 500; it++)
        {
            double zm = z + h / 2;
            double sz = r.P0 * r.Bm * r.Lm / ((r.Bm + zm) * (r.Lm + zm));
            double sbt = BoredPile.SigmaV(layers, zr + zm);
            if (sz <= 0.2 * sbt + 1e-9) break;
            var ly = BoredPile.LayerAt(layers, zr + zm, out _);
            double e = ly != null && ly.E > 0 ? ly.E : 10000;
            s += 0.8 * sz * h / e;
            z += h;
        }
        r.S = s; r.Depth = z;
        r.Calc.Add($"Lún nhóm cọc – móng tương đương ở độ sâu 2L/3: z = {zr:0.0#} m, B×L = {r.Bm:0.00}×{r.Lm:0.00} m, p0 = Ntc/(B·L) = {r.P0:0.0} kPa; "
                   + $"phân bố 2:1, dừng khi σz ≤ 0,2σbt (sâu {z:0.0} m dưới móng tương đương): S = Σ0,8·σz·h/E = {s * 1000:0.0} mm");
        return r;
    }

    // ---------------- chế độ thiết kế: hố khoan → bảng lớp đất cọc khoan nhồi ----------------

    /// <summary>Chuyển hố khoan sang bảng lớp đất dùng cho cọc khoan nhồi: chia lớp tại mực nước ngầm (dưới mực nước γ' = γsat − 10);
    /// đất dính chưa có IL thì ước theo N; đất đắp / bùn hữu cơ đánh dấu cố kết (ma sát âm); E: MPa → kPa.</summary>
    public static List<PileSoilLayer> FromBorehole(Borehole bh)
    {
        var res = new List<PileSoilLayer>();
        if (bh == null) return res;
        double prev = 0, w = bh.Water;
        foreach (var l in bh.Sorted())
        {
            double top = Math.Max(l.Top, prev), bot = Math.Max(l.Bottom, top);
            if (bot <= top + 1e-9) continue;
            if (top > prev + 1e-9 && res.Count > 0) res[^1].Thickness += top - prev;   // khe hở giữa các lớp: gộp vào lớp trên
            foreach (var (a, b) in Split(top, bot, w))
            {
                var p = Map(l, b <= w + 1e-9);
                p.Thickness = b - a;
                res.Add(p);
            }
            prev = bot;
        }
        return res;
    }

    static IEnumerable<(double A, double B)> Split(double a, double b, double w)
    {
        if (w > a + 1e-9 && w < b - 1e-9) { yield return (a, w); yield return (w, b); }
        else yield return (a, b);
    }

    static PileSoilLayer Map(SoilLayer l, bool aboveWater)
    {
        double n = Math.Max(l.N, 0);
        string kind = l.Kind switch
        {
            SoilKind.Sand => l.Grain is SandGrain.Fine or SandGrain.Silty ? BoredPile.KindSandFine : BoredPile.KindSandCoarse,
            SoilKind.Gravel or SoilKind.Rock => BoredPile.KindSandCoarse,
            _ => l.IsCohesive || l.Kind == SoilKind.Fill ? BoredPile.KindClay : BoredPile.KindSandSilty
        };
        double il = l.IL >= 0 ? l.IL : n >= 30 ? 0 : n >= 15 ? 0.25 : n >= 8 ? 0.5 : n >= 4 ? 0.75 : 1.0;
        if (l.Kind == SoilKind.Organic) il = Math.Max(il, 1.0);
        return new PileSoilLayer
        {
            Name = string.IsNullOrWhiteSpace(l.Name) ? l.KindText : l.Name,
            Kind = kind, IL = il, NSpt = l.Kind == SoilKind.Rock ? Math.Max(n, 50) : n,
            Gamma = aboveWater ? l.Gamma : Math.Max(l.GammaSatOrDefault - GeotechCalc.GammaW, 1),
            E = Math.Max(l.E, 0.5) * 1000,
            Consolidating = l.Kind is SoilKind.Organic or SoilKind.Fill
        };
    }

    /// <summary>Sức chịu tải cọc khoan nhồi chế độ thiết kế (FoundationSettings.BoredMethod khác rỗng): TCVN 10304:2014 theo bảng lớp đất
    /// chuyển từ hố khoan (Bảng 3/7 hoặc SPT), ma sát âm, Pvl có γcb·γ'cb, tự chọn chiều dài (PileAutoLength). Trả về PileCapacityResult
    /// với FS = γn·γk/γ0 để [P]đn = γ0·Rc,u/(γn·γk) (− Pn quy đổi khi có ma sát âm).</summary>
    public static PileCapacityResult DesignCapacity(Project p, Borehole bh, FoundationSettings fs)
    {
        fs ??= p?.Foundation ?? new FoundationSettings();
        var layers = FromBorehole(bh);
        double d = Math.Max(fs.PileSize, 0.1);
        double head = Math.Max(p?.Soil?.Depth ?? 1.5, 0);
        string method = fs.BoredMethod == BoredPile.MethodSpt ? BoredPile.MethodSpt : BoredPile.MethodTable;
        string drill = string.IsNullOrWhiteSpace(fs.BoredDrill) ? BoredPile.DrillSlurry : fs.BoredDrill;
        Material conc, st;
        string cname = !string.IsNullOrWhiteSpace(fs.PileConcrete) ? fs.PileConcrete : p?.Concrete?.ConcreteFooting ?? "B25";
        try { conc = Material.Concrete(cname); } catch (Exception) { conc = Material.Concrete("B25"); }
        try { st = Material.Rebar(p?.Concrete?.RebarMain ?? "CB400-V"); } catch (Exception) { st = Material.Rebar("CB400-V"); }
        double asP = fs.PileRebarArea > 0 ? fs.PileRebarArea : Math.Max(GeotechCalc.DefaultRebarArea(d), 0.004 * Math.PI * d * d / 4);
        double pvl = BoredPile.MaterialCapacity(d, conc.Rb, st.Rsc, asP, drill != BoredPile.DrillDry);
        var notes = new List<string>();
        BoredPileResult bp;
        List<CapacityCurvePoint> curve;
        if (fs.PileAutoLength)
        {
            var al = AutoLength(layers, d, head, Math.Max(fs.PileCapacity, 1), pvl, method, drill);
            bp = al.Result ?? BoredPile.Capacity(layers, d, head, Math.Max(fs.PileLength, 1), method, drill);
            curve = al.Curve;
            notes.Add(al.Text);
        }
        else
        {
            double L = fs.PileTipDepth > head ? fs.PileTipDepth - head : Math.Max(fs.PileLength, 1);
            bp = BoredPile.Capacity(layers, d, head, L, method, drill);
            curve = Curve(layers, d, head, Math.Max(d < 0.8 ? 6 : 10, 1), Math.Max(TotalDepth(layers) - head - 0.5, 6), 1.0, method, drill, pvl);
        }
        bp.As = asP; bp.Pvl = pvl;
        var nf = NegativeFriction(bp, layers);
        double sf = bp.GammaN * bp.GammaK / Math.Max(bp.Gamma0, 1e-9);
        var r = new PileCapacityResult
        {
            Borehole = bh?.Name ?? "", Size = d, Round = true, Bored = true, TipDepth = bp.Tip, CutoffDepth = bp.Head,
            Ab = bp.Ab, U = bp.U, Qb = bp.Qb, TipResistance = bp.TipResistance,
            ShaftResistance = nf == null ? bp.ShaftResistance : bp.ShaftResistance - nf.ShaftLost - nf.Pn * sf,
            SafetyFactor = sf, Pvl = pvl, As = asP, Rb = conc.Rb, Rsc = st.Rsc, TipLayer = bp.TipLayer
        };
        foreach (var row in bp.Rows)
            r.Rows.Add(new PileLayerRow { From = row.From, To = row.To, Soil = row.Layer, Cohesive = !BoredPile.IsSandKind(BoredPile.LayerAt(layers, row.Z, out _)?.Kind), F = row.Fi, Q = row.Q });
        r.Rows.Add(new PileLayerRow { From = bp.Tip, To = bp.Tip, Soil = "Mũi cọc: " + bp.TipLayer, F = bp.Qb, Q = bp.TipResistance, IsTip = true });
        r.Notes.Add($"Cọc khoan nhồi D{d * 1000:0} ({drill}) – TCVN 10304:2014 {method}; hố khoan {r.Borehole} → bảng lớp đất {layers.Count} lớp (chia tại mực nước ngầm)");
        r.Notes.AddRange(notes);
        r.Notes.AddRange(bp.Calc);
        r.Notes.AddRange(bp.Notes);
        if (nf != null) r.Notes.Add(nf.Text);
        r.Notes.Add($"Pvl = φ·(γcb·γ'cb·Rb·Ab + Rsc·As) = {pvl:0.0} kN (bê tông {conc.Name}); [P] = min([P]đ; Pvl) = {r.Allowable:0.0} kN");
        if (curve.Count > 0)
            r.Notes.Add("Đường cong sức chịu tải (L m: Rc,u / [P] kN): " + string.Join("; ", curve.Select(c => $"{c.Length:0.#}: {c.Rcu:0}/{c.Allowable:0}{(c.GoodTip ? "" : "*")}")) + " (* mũi chưa vào lớp tốt)");
        return r;
    }

    // ---------------- hình: đường cong sức chịu tải ----------------

    /// <summary>Biểu đồ Rc,u và [P] theo độ sâu mũi cọc (tỉ lệ 1/100 theo độ sâu; trục ngang 1 kN = kx mm).</summary>
    public static Figure CurveFigure(List<CapacityCurvePoint> curve, string title = "ĐƯỜNG CONG SỨC CHỊU TẢI CỌC THEO CHIỀU DÀI")
    {
        double S = 100;
        var w = new DxfWriter(S);
        if (curve == null || curve.Count == 0) return new Figure(w, title);
        double vmax = Math.Max(curve.Max(c => c.Rcu), 1);
        double width = 6000, kx = width / vmax;
        double z0 = curve.Min(c => c.Tip), z1 = curve.Max(c => c.Tip);
        double Y(double z) => -z * 1000;
        // khung + lưới
        w.Rect(Lay.KyHieu, 0, Y(z1) - 500, width, Y(z0) + 500);
        int nTick = 5;
        for (int i = 0; i <= nTick; i++)
        {
            double v = vmax * i / nTick, x = v * kx;
            w.Line(Lay.KichThuoc, x, Y(z1) - 500, x, Y(z0) + 500, "DASHED");
            w.Text(Lay.Chu, x, Y(z0) + 500 + 0.5 * w.Th, 0.8 * w.Th, DxfWriter.N0(v), 0, 1, 0);
        }
        foreach (var c in curve.Where((c, i) => i % Math.Max(1, curve.Count / 10) == 0 || i == curve.Count - 1))
            w.Text(Lay.Chu, -0.5 * w.Th, Y(c.Tip), 0.8 * w.Th, "-" + c.Tip.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), 0, 2, 2);
        w.Polyline(Lay.Thep, curve.Select(c => (c.Rcu * kx, Y(c.Tip))).ToList(), false, 0.35 * S);
        w.Polyline(Lay.BeTong, curve.Select(c => (c.Allowable * kx, Y(c.Tip))).ToList(), false, 0.25 * S);
        foreach (var c in curve.Where(c => !c.GoodTip)) w.Circle(Lay.KyHieu, c.Allowable * kx, Y(c.Tip), 0.3 * w.Th);
        w.Text(Lay.Chu, width + 0.5 * w.Th, Y(curve[^1].Tip), 0.8 * w.Th, "Rc,u", 0, 0, 2);
        w.Text(Lay.Chu, curve[^1].Allowable * kx + 0.5 * w.Th, Y(curve[^1].Tip) - 1.2 * w.Th, 0.8 * w.Th, "[P] = min([P]đ; Pvl)", 0, 0, 2);
        w.Text(Lay.Chu, width / 2, Y(z0) + 500 + 2.2 * w.Th, 0.8 * w.Th, "SỨC CHỊU TẢI (kN) – ○: MŨI CHƯA VÀO LỚP TỐT", 0, 1, 0);
        w.Title(width / 2, Y(z1) - 500 - 1.5 * w.Th, title, DrawUtil.ScaleText(S));
        return new Figure(w, title);
    }
}
