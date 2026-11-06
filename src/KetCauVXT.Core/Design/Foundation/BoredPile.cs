using KetCauVXT.Detailing;

namespace KetCauVXT.Design.Foundation;

// =====================================================================
// CỌC KHOAN NHỒI (MINI D300–600 VÀ THƯỜNG) – TCVN 10304:2014
//  • Sức chịu tải theo đất nền (mục 7.2.2.2):  Rc,u = γc·(γcq·qb·Ab + u·Σ γcf·fi·li)
//      – theo chỉ tiêu cơ lý: fi theo Bảng 3 (độ sâu trung bình lớp z, chỉ số sệt IL / loại cát),
//        qb mũi cọc trong đất dính theo Bảng 7 (cọc khoan nhồi), li ≤ 2 m;
//      – theo SPT (Phụ lục G.3.2, công thức Nhật Bản): cát fs = 10·Ns/3, sét fc = αp·fL·cu; mũi cát qb = 150·Np, sét qb = 9·cu.
//    Sức chịu tải thiết kế theo đất [P]đ = γ0·Rc,u/(γn·γk) (mục 7.1.11).
//  • Theo vật liệu (mục 7.1.9): Pvl = φ·(γcb·γ'cb·Rb·Ab + Rsc·As); γcb = 0,85 (đổ bê tông trong lỗ khoan),
//    γ'cb = 0,7 (đổ trong dung dịch / dưới nước – thiên an toàn); φ = 1 với cọc ngàm hoàn toàn trong đất (không có đoạn tự do).
//  • Tải ngang (Phụ lục A – nền Winkler hệ số K tăng tuyến tính): αbd = (K·bp/(γc·Eb·I))^(1/5), bp = 1,5d + 0,5 (d ≤ 0,8 m), γc = 3;
//    cọc dài (αbd·L ≥ 4), đầu cọc ngàm vào đài: Mng = H·B0/(αbd·C0), y0 = H·(A0 − B0²/C0)/(αbd³·Eb·I), A0 = 2,441; B0 = 1,621; C0 = 1,751.
//  • Lún nhóm cọc – khối móng quy ước (mục 7.4.4): Bm = Bg + 2L·tan(φtb/4); cộng lún các lớp dưới mũi s = Σ 0,8·σz·h/E,
//    σz = p0·Bm·Lm/((Bm + z)(Lm + z)) (phân bố 2:1), dừng khi σz ≤ 0,2·σbt.
//  • Nhổ khối nhóm cọc: [Pk,khối] = 0,9·(W đất trong khối + G đài) + γc·u_khối·Σfi·li/γk.
// Đơn vị: kN, m, kPa (kN/m²).
// =====================================================================

/// <summary>Một lớp đất dùng tính cọc (đo từ mặt đất tự nhiên xuống).</summary>
public class PileSoilLayer
{
    [Field("Tên lớp")] public string Name { get; set; } = "Lớp";
    [Field("Bề dày", Unit = "m")] public double Thickness { get; set; } = 3;
    [Field("Loại đất", Choices = BoredPile.KindClay + "|" + BoredPile.KindSandCoarse + "|" + BoredPile.KindSandFine + "|" + BoredPile.KindSandSilty)]
    public string Kind { get; set; } = BoredPile.KindClay;
    [Field("Chỉ số sệt IL (đất dính)")] public double IL { get; set; } = 0.4;
    [Field("SPT N")] public double NSpt { get; set; }
    [Field("cu (0 = 6,25·N)", Unit = "kPa")] public double Cu { get; set; }
    [Field("γ (γ' dưới mực nước)", Unit = "kN/m³")] public double Gamma { get; set; } = 18;
    [Field("Mô đun biến dạng E", Unit = "kPa")] public double E { get; set; } = 10000;
    [Field("qb nhập tay (0 = theo bảng)", Unit = "kPa")] public double QbUser { get; set; }

    [System.Text.Json.Serialization.JsonIgnore] public bool IsSand => BoredPile.IsSandKind(Kind);
}

/// <summary>Một đoạn thân cọc trong bảng tính ma sát.</summary>
public sealed class BoredPileRow
{
    public string Layer { get; set; } = "";
    public double From { get; set; }
    public double To { get; set; }
    public double Z { get; set; }
    public double Fi { get; set; }
    public double GammaCf { get; set; }
    /// <summary>u·γcf·fi·li (kN)</summary>
    public double Q { get; set; }
    public double Len => To - From;
}

/// <summary>Kết quả sức chịu tải cọc khoan nhồi.</summary>
public sealed class BoredPileResult
{
    public double D { get; set; }
    public double Ab { get; set; }
    public double U { get; set; }
    public double Head { get; set; }
    public double Tip { get; set; }
    public double Length => Tip - Head;
    public List<BoredPileRow> Rows { get; } = new();
    public double Qb { get; set; }
    public string TipLayer { get; set; } = "";
    public double TipResistance { get; set; }
    public double ShaftResistance { get; set; }
    /// <summary>Σ fi·li (kN/m) – dùng cho cọc chịu nhổ</summary>
    public double SumFiLi { get; set; }
    public double Rcu => TipResistance + ShaftResistance;
    public double Gamma0 { get; set; } = 1.15;
    public double GammaN { get; set; } = 1.15;
    public double GammaK { get; set; } = 1.4;
    /// <summary>[P]đ = γ0·Rc,u/(γn·γk)</summary>
    public double Psoil => Rcu * Gamma0 / Math.Max(GammaN * GammaK, 1e-9);
    public double As { get; set; }
    public double Pvl { get; set; }
    public double Mu { get; set; }
    public double Allowable => Math.Min(Psoil, Pvl);
    public List<string> Calc { get; } = new();
    public List<string> Notes { get; } = new();
}

/// <summary>Kết quả tải ngang cọc (đầu cọc ngàm vào đài).</summary>
public sealed class PileLateralResult
{
    public double Bp { get; set; }
    public double AlphaBd { get; set; }
    public double M0 { get; set; }
    public double Y0 { get; set; }
    public bool Long { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>Kết quả lún khối móng quy ước.</summary>
public sealed class PileSettlementResult
{
    public double Bm { get; set; }
    public double Lm { get; set; }
    public double P { get; set; }
    public double P0 { get; set; }
    public double SigmaBtTip { get; set; }
    public double S { get; set; }
    public double Depth { get; set; }
    public List<string> Calc { get; } = new();
}

public static class BoredPile
{
    public const string KindClay = "Sét, á sét (đất dính)", KindSandCoarse = "Cát thô, cát vừa", KindSandFine = "Cát mịn", KindSandSilty = "Cát bụi";
    public const string MethodTable = "Chỉ tiêu cơ lý (Bảng 3, Bảng 7)", MethodSpt = "SPT (Phụ lục G.3.2)";
    public const string DrillSlurry = "Khoan dùng dung dịch bentonite", DrillCasing = "Khoan có ống vách", DrillDry = "Khoan khô (trên mực nước ngầm)";
    /// <summary>γcb – đổ bê tông trong lỗ khoan (TCVN 10304:2014 mục 7.1.9)</summary>
    public const double GammaCb = 0.85;
    /// <summary>γ'cb – đổ bê tông trong dung dịch / dưới nước</summary>
    public const double GammaCbWet = 0.7;
    public const double A0 = 2.441, B0 = 1.621, C0 = 1.751;

    public static bool IsSandKind(string k) => (k ?? "").StartsWith("Cát", StringComparison.OrdinalIgnoreCase);

    /// <summary>γcf cọc khoan nhồi theo phương pháp khoan (TCVN 10304:2014 Bảng 5 – giá trị thường dùng).</summary>
    public static double GammaCf(string drill) => drill == DrillSlurry ? 0.6 : 0.7;

    // ---- Bảng 3: ma sát fi (kPa) theo độ sâu trung bình z (m) và IL 0,2…1,0 (cát thô/vừa ≈ cột IL ≤ 0,2; cát mịn ≈ 0,3; cát bụi ≈ 0,4) ----
    static readonly double[] FiZ = { 1, 2, 3, 4, 5, 6, 8, 10, 15, 20, 25, 30, 35 };
    static readonly double[] FiIL = { 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 };
    static readonly double[,] FiTab =
    {
        { 35, 23, 15, 12, 8, 4, 4, 3, 2 },
        { 42, 30, 21, 17, 12, 7, 5, 4, 4 },
        { 48, 35, 25, 20, 14, 8, 7, 6, 5 },
        { 53, 38, 27, 22, 16, 9, 8, 7, 5 },
        { 56, 40, 29, 24, 17, 10, 8, 7, 6 },
        { 58, 42, 31, 25, 18, 10, 8, 7, 6 },
        { 62, 44, 33, 26, 19, 10, 8, 7, 6 },
        { 65, 46, 34, 27, 19, 10, 8, 7, 6 },
        { 72, 51, 38, 28, 20, 11, 8, 7, 6 },
        { 79, 56, 41, 30, 20, 12, 8, 7, 6 },
        { 86, 61, 44, 32, 20, 12, 8, 7, 6 },
        { 93, 66, 47, 34, 21, 12, 9, 8, 7 },
        { 100, 70, 50, 36, 22, 13, 9, 8, 7 },
    };

    // ---- Bảng 7: qb (kPa) mũi cọc khoan nhồi trong đất dính theo z (m) và IL 0…0,6 (z = 30, 40 m với IL 0,5; 0,6 ngoại suy) ----
    static readonly double[] QbZ = { 3, 5, 7, 10, 12, 15, 18, 20, 30, 40 };
    static readonly double[] QbIL = { 0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6 };
    static readonly double[,] QbTab =
    {
        { 850, 750, 650, 500, 400, 300, 250 },
        { 1000, 850, 750, 650, 500, 400, 350 },
        { 1150, 1000, 850, 750, 600, 500, 450 },
        { 1350, 1200, 1050, 950, 800, 700, 600 },
        { 1550, 1400, 1250, 1100, 950, 800, 700 },
        { 1800, 1650, 1500, 1300, 1100, 1000, 800 },
        { 2100, 1900, 1700, 1500, 1300, 1150, 950 },
        { 2300, 2100, 1900, 1650, 1450, 1250, 1050 },
        { 3300, 3000, 2600, 2300, 2000, 1700, 1400 },
        { 4500, 4000, 3500, 3000, 2500, 2100, 1700 },
    };

    static double Interp1(double[] xs, double x, Func<int, double> val)
    {
        if (x <= xs[0]) return val(0);
        if (x >= xs[^1]) return val(xs.Length - 1);
        int i = 0;
        while (i + 1 < xs.Length && xs[i + 1] < x) i++;
        double t = (x - xs[i]) / (xs[i + 1] - xs[i]);
        return val(i) + t * (val(i + 1) - val(i));
    }

    static double Interp2(double[] zs, double[] cs, double[,] tab, double z, double c) =>
        Interp1(zs, z, iz => Interp1(cs, c, ic => tab[iz, ic]));

    /// <summary>Ma sát fi (kPa) theo Bảng 3 TCVN 10304:2014 tại độ sâu trung bình z (m).</summary>
    public static double FiTable(double z, string kind, double il)
    {
        double col = kind == KindSandCoarse ? 0.2 : kind == KindSandFine ? 0.3 : kind == KindSandSilty ? 0.4 : Math.Clamp(il, 0.2, 1.0);
        if (!IsSandKind(kind) && il > 1.0) return 0;   // đất chảy: bỏ qua ma sát
        return Interp2(FiZ, FiIL, FiTab, z, col);
    }

    /// <summary>Cường độ mũi qb (kPa) cọc khoan nhồi trong đất dính theo Bảng 7 TCVN 10304:2014.</summary>
    public static double QbClayTable(double z, double il) => Interp2(QbZ, QbIL, QbTab, z, Math.Clamp(il, 0, 0.6));

    /// <summary>Hệ số αp (SPT, sét): ψ = cu/σ'v; αp = 0,5ψ^−0,5 (ψ ≤ 1), 0,5ψ^−0,25 (ψ &gt; 1), ≤ 1.</summary>
    public static double AlphaP(double cu, double sigmaV)
    {
        if (cu <= 0) return 1;
        double psi = sigmaV > 1e-9 ? cu / sigmaV : double.PositiveInfinity;
        return Math.Min(psi <= 1 ? 0.5 * Math.Pow(psi, -0.5) : 0.5 * Math.Pow(psi, -0.25), 1);
    }

    /// <summary>Lớp đất tại độ sâu z (m từ mặt đất); null nếu sâu hơn lớp cuối (lấy lớp cuối).</summary>
    public static PileSoilLayer LayerAt(IList<PileSoilLayer> layers, double z, out double top)
    {
        top = 0;
        double t = 0;
        for (int i = 0; i < layers.Count; i++)
        {
            double b = t + Math.Max(layers[i].Thickness, 0);
            if (z < b - 1e-9 || i == layers.Count - 1) { top = t; return layers[i]; }
            t = b;
        }
        return null;
    }

    /// <summary>Ứng suất bản thân σ'v (kPa) tại độ sâu z.</summary>
    public static double SigmaV(IList<PileSoilLayer> layers, double z)
    {
        double s = 0, t = 0;
        for (int i = 0; i < layers.Count && t < z - 1e-12; i++)
        {
            double th = i == layers.Count - 1 ? Math.Max(z - t, 0) : Math.Max(layers[i].Thickness, 0);
            double dz = Math.Min(th, z - t);
            s += layers[i].Gamma * dz;
            t += th;
        }
        return s;
    }

    /// <summary>Sức chịu tải cọc khoan nhồi đường kính d (m), đầu cọc ở độ sâu head, chiều dài L (m).
    /// method: MethodTable / MethodSpt; gammaCf ≤ 0 → theo phương pháp khoan.</summary>
    public static BoredPileResult Capacity(IList<PileSoilLayer> layers, double d, double head, double length, string method, string drill,
        double gammaCf = 0, double gamma0 = 1.15, double gammaN = 1.15, double gammaK = 1.4)
    {
        var r = new BoredPileResult
        {
            D = d, Ab = Math.PI * d * d / 4, U = Math.PI * d, Head = Math.Max(head, 0), Tip = Math.Max(head, 0) + Math.Max(length, 0),
            Gamma0 = gamma0 > 0 ? gamma0 : 1.15, GammaN = gammaN > 0 ? gammaN : 1.15, GammaK = gammaK > 0 ? gammaK : 1.4
        };
        if (layers == null || layers.Count == 0 || length <= 0 || d <= 0) { r.Notes.Add("LỖI cần bảng lớp đất, chiều dài cọc và đường kính > 0"); return r; }
        double gcf = gammaCf > 0 ? gammaCf : GammaCf(drill);
        bool spt = method == MethodSpt;
        // chia thân cọc theo ranh lớp, mỗi đoạn ≤ 2 m (TCVN 10304:2014 Bảng 3, ghi chú)
        var breaks = new List<double> { r.Head };
        double t = 0;
        foreach (var l in layers) { t += Math.Max(l.Thickness, 0); if (t > r.Head + 1e-9 && t < r.Tip - 1e-9) breaks.Add(t); }
        breaks.Add(r.Tip);
        breaks = breaks.Distinct().OrderBy(x => x).ToList();
        for (int i = 0; i + 1 < breaks.Count; i++)
        {
            double a = breaks[i], b = breaks[i + 1];
            int nsub = Math.Max(1, (int)Math.Ceiling((b - a) / 2.0 - 1e-9));
            for (int k = 0; k < nsub; k++)
            {
                double z0 = a + (b - a) * k / nsub, z1 = a + (b - a) * (k + 1) / nsub, zm = (z0 + z1) / 2;
                var ly = LayerAt(layers, zm, out _);
                double fi;
                if (spt)
                {
                    if (ly.IsSand) fi = 10 * Math.Max(ly.NSpt, 0) / 3;
                    else { double cu = ly.Cu > 0 ? ly.Cu : 6.25 * Math.Max(ly.NSpt, 0); fi = AlphaP(cu, SigmaV(layers, zm)) * 1.0 * cu; }
                }
                else fi = FiTable(zm, ly.Kind, ly.IL);
                var row = new BoredPileRow { Layer = ly.Name, From = z0, To = z1, Z = zm, Fi = fi, GammaCf = gcf };
                row.Q = r.U * gcf * fi * (z1 - z0);
                r.Rows.Add(row);
            }
        }
        r.ShaftResistance = r.Rows.Sum(x => x.Q);
        r.SumFiLi = r.Rows.Sum(x => x.Fi * x.Len);
        // mũi cọc
        var tl = LayerAt(layers, r.Tip - 1e-6, out _);
        r.TipLayer = tl.Name;
        double qb;
        string how;
        if (tl.QbUser > 0) { qb = tl.QbUser; how = "nhập tay"; }
        else if (spt || tl.IsSand)
        {
            if (tl.IsSand) { qb = 150 * Math.Max(tl.NSpt, 0); how = "cát: qb = 150·Np (Phụ lục G.3.2, cọc khoan nhồi)"; }
            else { double cu = tl.Cu > 0 ? tl.Cu : 6.25 * Math.Max(tl.NSpt, 0); qb = 9 * cu; how = "sét: qb = 9·cu"; }
            if (tl.IsSand && tl.NSpt <= 0) r.Notes.Add("CẢNH BÁO mũi cọc trong cát nhưng chưa có SPT N / qb nhập tay – qb = 0");
        }
        else
        {
            qb = QbClayTable(r.Tip, tl.IL); how = "Bảng 7 (đất dính, IL = " + tl.IL.ToString("0.##") + ", z = " + r.Tip.ToString("0.##") + " m)";
            if (tl.IL > 0.6) r.Notes.Add("CẢNH BÁO mũi cọc trong đất dính dẻo mềm / dẻo chảy (IL > 0,6) – nên hạ mũi xuống lớp tốt hơn");
        }
        r.Qb = qb;
        r.TipResistance = 1.0 * qb * r.Ab;   // γcq = 1 (cọc khoan nhồi)
        r.Calc.Add($"Cọc khoan nhồi D{d * 1000:0}: Ab = {r.Ab:0.0000} m², u = {r.U:0.000} m; đầu cọc −{r.Head:0.00} m, mũi −{r.Tip:0.00} m (L = {r.Length:0.00} m)");
        r.Calc.Add($"Ma sát thân ({(spt ? "SPT: cát fs = 10·N/3, sét fc = αp·cu" : "Bảng 3 theo z và IL")}), γcf = {gcf:0.0#} ({(gammaCf > 0 ? "nhập" : drill)}), mỗi đoạn ≤ 2 m: u·Σγcf·fi·li = {r.ShaftResistance:0.0} kN");
        r.Calc.Add($"Mũi cọc ({r.TipLayer}): qb = {qb:0} kPa ({how}) → γcq·qb·Ab = {r.TipResistance:0.0} kN");
        r.Calc.Add($"Rc,u = γc·(γcq·qb·Ab + u·Σγcf·fi·li) = {r.Rcu:0.0} kN (γc = γcq = 1); [P]đ = γ0·Rc,u/(γn·γk) = {r.Gamma0:0.00}·{r.Rcu:0.0}/({r.GammaN:0.00}·{r.GammaK:0.00}) = {r.Psoil:0.0} kN (TCVN 10304:2014 mục 7.1.11)");
        return r;
    }

    /// <summary>Sức chịu tải theo vật liệu Pvl = φ·(γcb·γ'cb·Rb·Ab + Rsc·As) (TCVN 10304:2014 mục 7.1.9), φ = 1. rb, rsc: kPa.</summary>
    public static double MaterialCapacity(double d, double rb, double rsc, double asTotal, bool wet = true, double phi = 1.0)
    {
        double ab = Math.PI * d * d / 4;
        return phi * (GammaCb * (wet ? GammaCbWet : 1.0) * rb * ab + rsc * asTotal);
    }

    /// <summary>Hàm lượng thép dọc μ = As/Ab (%).</summary>
    public static double SteelRatio(double d, double asTotal) => asTotal / (Math.PI * d * d / 4) * 100;

    /// <summary>Mô men giới hạn gần đúng tiết diện tròn chỉ kể thép phân bố đều trên vòng bán kính rs (bỏ qua bê tông, thiên an toàn):
    /// Mu = Rs·As·2rs/π.</summary>
    public static double MomentCapacity(double rs, double asTotal, double rsDist) => rs * asTotal * 2 * Math.Max(rsDist, 0) / Math.PI;

    /// <summary>Tải ngang H (kN) tại đầu cọc ngàm vào đài: αbd, Mngàm, chuyển vị y0 (Phụ lục A TCVN 10304:2014, cọc dài).
    /// k: hệ số tỉ lệ K (kN/m⁴); eb: mô đun bê tông (kPa).</summary>
    public static PileLateralResult Lateral(double h, double d, double length, double k, double eb, double gammaC = 3)
    {
        double bp = d <= 0.8 ? 1.5 * d + 0.5 : d + 1.0;
        double I = Math.PI * Math.Pow(d, 4) / 64;
        double a = Math.Pow(k * bp / (gammaC * eb * I), 0.2);
        var r = new PileLateralResult { Bp = bp, AlphaBd = a, Long = a * length >= 4 };
        r.M0 = h * B0 / (a * C0);
        r.Y0 = h * (A0 - B0 * B0 / C0) / (Math.Pow(a, 3) * eb * I);
        r.Text = $"Tải ngang H = {h:0.0} kN/cọc: bp = {(d <= 0.8 ? "1,5d + 0,5" : "d + 1")} = {bp:0.00} m, αbd = (K·bp/(γc·Eb·I))^(1/5) = {a:0.000} 1/m (K = {k:0} kN/m⁴, γc = {gammaC:0})"
                 + $"; αbd·L = {a * length:0.0} {(r.Long ? "≥ 4 (cọc dài)" : "< 4: cọc ngắn – kết quả gần đúng")}"
                 + $"; đầu cọc ngàm vào đài: M = H·B0/(αbd·C0) = {r.M0:0.0} kN.m, y0 = H·(A0 − B0²/C0)/(αbd³·Eb·I) = {r.Y0 * 1000:0.0} mm";
        return r;
    }

    /// <summary>Lún nhóm cọc theo khối móng quy ước. bg, lg: kích thước ngoài nhóm cọc (m, mép cọc biên); ntc: tải tiêu chuẩn (kN);
    /// phi: góc ma sát trung bình các lớp thân cọc (độ); depthTip: độ sâu mũi cọc từ mặt đất (m).</summary>
    public static PileSettlementResult Settlement(IList<PileSoilLayer> layers, double bg, double lg, double length, double depthTip, double ntc, double phiDeg, double gammaTb = 20)
    {
        var r = new PileSettlementResult();
        double tn = Math.Tan(phiDeg / 4 * Math.PI / 180);
        r.Bm = bg + 2 * length * tn; r.Lm = lg + 2 * length * tn;
        double am = r.Bm * r.Lm;
        r.P = ntc / am + gammaTb * depthTip;
        r.SigmaBtTip = SigmaV(layers, depthTip);
        r.P0 = Math.Max(r.P - r.SigmaBtTip, 0);
        double h = Math.Max(0.2 * r.Bm, 0.2), z = 0, s = 0;
        for (int it = 0; it < 400; it++)
        {
            double zm = z + h / 2;
            double sz = r.P0 * r.Bm * r.Lm / ((r.Bm + zm) * (r.Lm + zm));
            double sbt = SigmaV(layers, depthTip + zm);
            if (sz <= 0.2 * sbt + 1e-9) break;
            var ly = LayerAt(layers, depthTip + zm, out _);
            double e = ly != null && ly.E > 0 ? ly.E : 10000;
            s += 0.8 * sz * h / e;
            z += h;
        }
        r.S = s; r.Depth = z;
        r.Calc.Add($"Khối móng quy ước: Bm = Bg + 2L·tan(φtb/4) = {bg:0.00} + 2·{length:0.0}·tan({phiDeg:0}°/4) = {r.Bm:0.00} m; Lm = {r.Lm:0.00} m");
        r.Calc.Add($"p = Ntc/(Bm·Lm) + γtb·H = {r.P:0.0} kPa; σbt mũi = {r.SigmaBtTip:0.0} kPa; p0 = {r.P0:0.0} kPa");
        r.Calc.Add($"Lún cộng lớp (lớp 0,2Bm, σz = p0·Bm·Lm/((Bm+z)(Lm+z)), dừng khi σz ≤ 0,2σbt – sâu {z:0.0} m): S = Σ0,8·σz·h/E = {s * 1000:0.0} mm ≤ 80 mm (nhà khung BTCT – TCVN 10304:2014 Phụ lục E)");
        return r;
    }

    /// <summary>Nhổ khối nhóm cọc: [Pk,khối] = 0,9·(Lb·Bb·depth·γ + Gđài) + 0,8·2(Lb + Bb)·Σfi·li/γk.</summary>
    public static (double Cap, double W, double Fs) BlockUplift(double blockL, double blockB, double depth, double gammaSoil, double sumFiLi, double gCap, double gammaK = 1.4)
    {
        double w = blockL * blockB * depth * gammaSoil;
        double fs = PileGroup.GammaCTension * 2 * (blockL + blockB) * Math.Max(sumFiLi, 0) / Math.Max(gammaK, 1e-9);
        return (0.9 * (w + gCap) + fs, w, fs);
    }

    /// <summary>Lồng thép cọc khoan nhồi theo thực hành: chiều dài lồng ≥ max(L/3; 4/αbd + 1 m; 6 m), toàn chiều dài khi cọc chịu nhổ.</summary>
    public static double CageLength(double length, double alphaBd, bool tension)
    {
        if (tension) return length;
        double l = Math.Max(length / 3, 6);
        if (alphaBd > 1e-9) l = Math.Max(l, 4 / alphaBd + 1);
        return Math.Min(l, length);
    }

    /// <summary>Ghi chú thi công & thử tải cọc khoan nhồi (TCVN 9395:2012, TCVN 9393:2012).</summary>
    public static List<string> ConstructionNotes(int nPiles, double d)
    {
        int nTest = Math.Max(1, (int)Math.Ceiling(nPiles * 0.01));
        return new List<string>
        {
            "Bê tông cọc đổ bằng ống tremie liên tục, độ sụt 18 ± 2 cm; đầu ống ngập trong bê tông ≥ 1,5 m; đổ cao hơn cốt đầu cọc ≥ 0,5–1,0 m rồi đập bỏ phần bê tông kém chất lượng (TCVN 9395:2012).",
            "Dung dịch bentonite: tỉ trọng 1,05–1,15, độ nhớt 18–45 s, hàm lượng cát ≤ 4 %; vét lắng đáy hố khoan trước khi đổ bê tông (lắng ≤ 50 mm với cọc chống, ≤ 100 mm cọc ma sát).",
            "Con kê lồng thép (bê tông / nhựa) 4 cái mỗi tầng, cách nhau ≤ 2 m theo chiều dài lồng; đai gia cường Ø14–16 mỗi 2 m giữ hình lồng; nối lồng bằng buộc / hàn chồng ≥ 40d.",
            $"Thử tải tĩnh nén ≥ max(1 %; 1) số cọc = {nTest} cọc (TCVN 9393:2012); kiểm tra biến dạng nhỏ PIT ≥ 25–50 % số cọc; cọc D ≥ {d * 1000:0} mm đặt ống siêu âm khi cần (TCVN 9396:2012).",
            "Ghi chép nhật ký khoan từng cọc (cao độ, lớp đất, độ thẳng đứng ≤ 1 %, vị trí lệch ≤ 0,1d), đối chiếu địa chất thực tế với thiết kế."
        };
    }
}
