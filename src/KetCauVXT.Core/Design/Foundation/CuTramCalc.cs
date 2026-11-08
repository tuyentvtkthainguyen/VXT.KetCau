using System.Globalization;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

// =====================================================================
// [CừTràm] GIA CỐ NỀN BẰNG CỪ TRÀM DƯỚI MÓNG NÔNG
// Tài liệu tham chiếu (ghi trung thực):
//  • TCVN 10304:2014 "Móng cọc – Tiêu chuẩn thiết kế": sức kháng ma sát fi theo độ sâu và độ sệt IL cho cọc đóng/ép
//    (bảng fi – cùng số liệu với SNiP 2.02.03-85 Bảng 2; người dùng đối chiếu lại bảng trong bản tiêu chuẩn đang dùng);
//    sức kháng mũi đất dính qb = 9·cu, đất rời theo SPT qb = 300·Np (Phụ lục G.3.2) – áp dụng gần đúng cho cây cừ.
//  • TCVN 9362:2012 "Nền nhà và công trình": R = m1·m2/ktc·(A·b·γ + B·h·γ' + D·c), độ lún cộng lớp (Phụ lục C), Sgh = 8 cm (Bảng 16).
//  • Thực hành thiết kế cừ tràm ở Việt Nam (sổ tay / hướng dẫn kỹ thuật địa phương): mật độ 16–25 cây/m², dài 3–5 m, Ø gốc 8–10 cm,
//    đầu cừ phải luôn nằm dưới mực nước ngầm thấp nhất; nền tương đương e1 = e0 − (1 + e0)·Vcừ/V; khối móng quy ước mở rộng góc φtb/4.
//  Các hệ số không có trong tiêu chuẩn (αs, quy luật tăng φ, c theo e, ngưỡng N, tải tối đa) là KINH NGHIỆM – người dùng kiểm tra.
// =====================================================================

/// <summary>Một đoạn thân cừ trong một lớp đất (ma sát fi theo TCVN 10304:2014).</summary>
public sealed class CuTramShaftRow
{
    public double From { get; set; }
    public double To { get; set; }
    public double Z => (From + To) / 2;
    public double Length => To - From;
    public string Soil { get; set; } = "";
    /// <summary>Độ sệt dùng tra bảng (cột tương đương với đất rời)</summary>
    public double IL { get; set; }
    public bool Cohesive { get; set; }
    /// <summary>Ma sát đơn vị fi (kPa)</summary>
    public double F { get; set; }
    /// <summary>u·fi·li (kN)</summary>
    public double Q { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>Kết quả tính gia cố cừ tràm dưới một móng L × B (đáy móng ở độ sâu Df).</summary>
public sealed class CuTramResult
{
    // ---- đầu vào ----
    public string Borehole { get; set; } = "";
    /// <summary>true = hố khoan giả định nền yếu (chưa có khảo sát)</summary>
    public bool AssumedSoil { get; set; }
    public double L { get; set; }
    public double B { get; set; }
    public double Df { get; set; }
    public double Length { get; set; }
    public double D1 { get; set; }
    public double D2 { get; set; }
    public double Density { get; set; }
    public double SandCushion { get; set; }
    public double LeanThickness { get; set; }
    public CuTramMethod Method { get; set; }
    /// <summary>Độ sâu đầu cừ (đáy bê tông lót), độ sâu bắt đầu tiếp xúc đất (dưới cát đệm), độ sâu mũi cừ (m từ mặt đất)</summary>
    public double TopDepth { get; set; }
    public double SoilTop { get; set; }
    public double TipDepth { get; set; }

    // ---- (1) cừ đơn + nhóm ----
    /// <summary>Chu vi trung bình u = π(D1 + D2)/2 (m), diện tích mũi Ap = πD2²/4 (m²), thể tích 1 cây (m³)</summary>
    public double U { get; set; }
    public double Ap { get; set; }
    public double Volume1 { get; set; }
    public List<CuTramShaftRow> Rows { get; set; } = new();
    public double Qs { get; set; }
    public double Qp { get; set; }
    public double QpUnit { get; set; }
    public string TipLayer { get; set; } = "";
    /// <summary>Sức chịu tải cực hạn 1 cây Qc = u·Σfi·li + qp·Ap (kN)</summary>
    public double Qc => Qs + Qp;
    public double SafetyFactor { get; set; } = 2;
    /// <summary>Sức chịu tải theo vật liệu gỗ Pvl = Rgỗ·Ap (tiết diện ngọn) (kN)</summary>
    public double Pvl { get; set; }
    /// <summary>Sức chịu tải cho phép 1 cây Qa = min(Qc/FS; Pvl) (kN)</summary>
    public double Qa => Math.Min(Qc / Math.Max(SafetyFactor, 1e-6), Pvl);
    /// <summary>Số cây n = ⌈mật độ × L × B⌉, lưới Nx × Ny (bản vẽ), bước s = 1/√mật độ</summary>
    public int Count { get; set; }
    public int Nx { get; set; }
    public int Ny { get; set; }
    public double Spacing { get; set; }
    public double Eta { get; set; } = 1;
    /// <summary>Tỷ lệ thay thế diện tích m = n·Ap,tb/A</summary>
    public double AreaRatio { get; set; }
    public double SoilShare { get; set; }
    /// <summary>Sức chịu tải nền thiên nhiên tại đáy móng R0 (kPa)</summary>
    public double R0 { get; set; }
    /// <summary>Trọng lượng (đẩy nổi) các cây cừ (kN)</summary>
    public double TimberWeight { get; set; }
    /// <summary>[p]1 = (η·n·Qa − Gcừ)/A + αs·(1 − m)·R0 (kPa)</summary>
    public double P1 { get; set; }

    // ---- (2) nền tương đương + khối quy ước ----
    public double E0 { get; set; }
    public bool E0Assumed { get; set; }
    /// <summary>Tỷ lệ thể tích cừ / đất Vc/V = mật độ·Vcừ/Lc</summary>
    public double VolumeRatio { get; set; }
    public double E1 { get; set; }
    public double Phi0 { get; set; }
    public double C0 { get; set; }
    public double Phi1 { get; set; }
    public double C1 { get; set; }
    /// <summary>R của nền sau gia cố tại đáy móng (kPa)</summary>
    public double R1 { get; set; }
    public double PhiAvg { get; set; }
    public double Lqu { get; set; }
    public double Bqu { get; set; }
    public double Aqu => Lqu * Bqu;
    /// <summary>Trọng lượng hữu hiệu khối đất + cừ từ đáy móng tới mũi cừ trên Aqu (kN)</summary>
    public double BlockWeight { get; set; }
    /// <summary>Sức chịu tải nền tại mũi cừ (khối quy ước Bqu, h = độ sâu mũi) (kPa)</summary>
    public double RTip { get; set; }
    /// <summary>[p]2 = min(R1; (RTip·Aqu − W)/A) (kPa)</summary>
    public double P2 { get; set; }

    // ---- kết luận ----
    /// <summary>Sức chịu tải nền dùng chọn kích thước móng (theo Method) (kPa)</summary>
    public double Allowable { get; set; }
    /// <summary>Áp lực tiêu chuẩn đáy móng ptb, pmax (kPa, gồm trọng lượng móng + đất đắp); NaN = chưa kiểm tra</summary>
    public double Ptb { get; set; } = double.NaN;
    public double Pmax { get; set; } = double.NaN;
    /// <summary>Áp lực tại mũi khối quy ước σ = (ptb·A + W)/Aqu (kPa)</summary>
    public double SigmaTip { get; set; } = double.NaN;
    public SettlementResult Settlement { get; set; }
    /// <summary>Mực nước ngầm thấp nhất dùng kiểm tra độ bền (m, +∞ = không gặp)</summary>
    public double WaterDepth { get; set; } = double.PositiveInfinity;
    public bool WaterFromBorehole { get; set; }
    public bool DurabilityOk => TopDepth >= WaterDepth - 1e-9;
    /// <summary>N trung bình dọc thân cừ (độ yếu của đất)</summary>
    public double NAvg { get; set; }
    /// <summary>Lực nén tiêu chuẩn chân cột lớn nhất (kN), NaN = chưa có</summary>
    public double Ntc { get; set; } = double.NaN;
    /// <summary>Tổng chiều dài cừ (m), thể tích cát đệm (m³) cho 1 móng</summary>
    public double TotalLength => Count * Length;
    public double SandVolume { get; set; }
    public List<SafetyItem> Items { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    /// <summary>Diễn giải công thức (thuyết minh, hộp thoại)</summary>
    public List<string> Notes { get; set; } = new();
    public bool AnyFail => Items.Any(i => i.Status == CheckStatus.Fail);
    /// <summary>false = móng nông trên nền cừ tràm KHÔNG KHẢ THI (kích thước vượt giới hạn mà chưa thoả [p]) → dùng cọc BTCT</summary>
    public bool Feasible { get; set; } = true;
    /// <summary>Kết luận ngắn (rỗng = khả thi)</summary>
    public string Conclusion { get; set; } = "";
}

/// <summary>Tính toán gia cố nền bằng cừ tràm: cừ đơn (ma sát + mũi), nhóm cừ – nền phức hợp, nền tương đương sau gia cố,
/// khối móng quy ước tại mũi cừ, lún, độ bền (mực nước ngầm), khối lượng.</summary>
public static class CuTramCalc
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v, string fmt = "0.##") => double.IsFinite(v) ? v.ToString(fmt, Inv) : "-";
    public const string Group = "Cừ tràm";
    /// <summary>Ngưỡng N trung bình của đất phù hợp đóng cừ tràm (kinh nghiệm: bùn / sét dẻo chảy N &lt; 5)</summary>
    public const double SoftN = 5;

    // ------------------------------------------------------------------
    // Bảng ma sát fi (kPa) – TCVN 10304:2014 (cọc đóng/ép; cùng số liệu SNiP 2.02.03-85 Bảng 2)
    // hàng: độ sâu trung bình lớp z (m); cột: IL = 0,2 … 1,0 (đất rời: cát thô + vừa ↔ cột 0,2; cát nhỏ ↔ 0,3; cát bụi ↔ 0,4)
    // ------------------------------------------------------------------
    public static readonly double[] TableZ = { 1, 2, 3, 4, 5, 6, 8, 10, 15, 20, 25, 30, 35 };
    public static readonly double[] TableIL = { 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 };
    public static readonly double[,] TableF =
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
        { 100, 70, 50, 36, 22, 13, 9, 8, 7 }
    };

    private static void Lerp1(double[] xs, double x, out int i, out double t)
    {
        if (x <= xs[0]) { i = 0; t = 0; return; }
        if (x >= xs[^1]) { i = xs.Length - 2; t = 1; return; }
        i = 0;
        while (i + 2 < xs.Length && x > xs[i + 1]) i++;
        t = (x - xs[i]) / (xs[i + 1] - xs[i]);
    }

    /// <summary>Ma sát đơn vị fi (kPa) nội suy song tuyến theo độ sâu z (giới hạn 1…35 m) và độ sệt IL (giới hạn 0,2…1,0).</summary>
    public static double Friction(double z, double il)
    {
        Lerp1(TableZ, z, out int i, out double t);
        Lerp1(TableIL, il, out int j, out double u);
        double f00 = TableF[i, j], f01 = TableF[i, j + 1], f10 = TableF[i + 1, j], f11 = TableF[i + 1, j + 1];
        return (1 - t) * ((1 - u) * f00 + u * f01) + t * ((1 - u) * f10 + u * f11);
    }

    /// <summary>Độ sệt dùng tra bảng fi: đất dính theo IL (chưa có IL → ước lượng theo N, kinh nghiệm); đất rời theo cỡ hạt
    /// (cát thô / vừa / sỏi ↔ 0,2; cát nhỏ ↔ 0,3); bùn IL &gt; 1 lấy 1,0 (cận bảng).</summary>
    public static double TableIndex(SoilLayer l, out string note)
    {
        note = "";
        if (l == null) { note = "không có lớp đất"; return 1.0; }
        if (!l.IsCohesive)
        {
            if (l.Kind is SoilKind.Gravel or SoilKind.Rock) return 0.2;
            if (l.Kind == SoilKind.Sand) return l.Grain is SandGrain.Fine or SandGrain.Silty ? 0.3 : 0.2;
            note = "đất rời không xác định – lấy cột cát bụi (0,4)";
            return 0.4;
        }
        if (l.IL >= 0)
        {
            if (l.IL > 1.0) note = "IL > 1 (bùn) – lấy cột IL = 1,0";
            return Math.Clamp(l.IL, 0.2, 1.0);
        }
        double n = l.N;
        note = "chưa có IL – ước lượng theo N (kinh nghiệm)";
        return n < 2 ? 1.0 : n < 4 ? 0.8 : n < 8 ? 0.55 : n < 15 ? 0.35 : 0.2;
    }

    // ------------------------------------------------------------------
    // Áp dụng / nền
    // ------------------------------------------------------------------
    /// <summary>Cừ tràm áp dụng tại chân cột nodeId: ghi đè từng chân cột, ngược lại thiết lập chung.</summary>
    public static bool ActiveAt(Project p, int nodeId, bool strip = false)
    {
        var cs = p?.Foundation?.CuTram;
        if (cs == null) return false;
        var o = cs.Overrides?.FirstOrDefault(x => x != null && x.NodeId == nodeId);
        if (o != null) return o.Enabled;
        return cs.Enabled && (!strip || cs.ApplyToStrip);
    }

    /// <summary>Ghi / xoá lựa chọn cừ tràm riêng các chân cột (on = null → theo thiết lập chung).</summary>
    public static void Set(FoundationSettings fs, IEnumerable<int> nodes, bool? on)
    {
        if (fs == null) return;
        fs.CuTram ??= new CuTramSettings();
        fs.CuTram.Overrides ??= new List<CuTramOverride>();
        foreach (int id in nodes.Distinct())
        {
            fs.CuTram.Overrides.RemoveAll(o => o == null || o.NodeId == id);
            if (on.HasValue) fs.CuTram.Overrides.Add(new CuTramOverride { NodeId = id, Enabled = on.Value });
        }
    }

    /// <summary>Hố khoan "nền yếu giả định" (chưa có khảo sát): đất đắp 0–0,8 m, bùn sét 0,8–8 m (N = 2, IL = 1,1, e0 = 1,6), sét pha dẻo cứng 8–20 m; MNN 0,5 m.
    /// Chỉ để ước tính – BẮT BUỘC khảo sát địa chất trước khi thiết kế.</summary>
    public static Borehole SoftDefault() => new()
    {
        Name = "Nền yếu giả định", WaterDepth = 0.5,
        Layers =
        {
            new SoilLayer { Name = "Đất đắp", Kind = SoilKind.Fill, Top = 0, Bottom = 0.8, Gamma = 17, Phi = 8, C = 5, E = 2, N = 3, IL = -1 },
            new SoilLayer { Name = "Bùn sét (giả định)", Kind = SoilKind.Organic, Top = 0.8, Bottom = 8, Gamma = 15.5, GammaSat = 16, Phi = 4, C = 7, E = 1.5, N = 2, IL = 1.1, VoidRatio = 1.6 },
            new SoilLayer { Name = "Sét pha dẻo cứng (giả định)", Kind = SoilKind.SiltyClay, Top = 8, Bottom = 20, Gamma = 19, GammaSat = 19.5, Phi = 16, C = 25, E = 10, N = 12, IL = 0.35, VoidRatio = 0.75 }
        }
    };

    /// <summary>Hố khoan dùng cho cừ tràm tại (x, y): hố khoan gần nhất (hoặc theo tên); không có → nền yếu giả định.</summary>
    public static Borehole BoreholeFor(Project p, double x, double y, out bool assumed)
    {
        var bh = GeotechCalc.SelectBorehole(p, x, y);
        assumed = bh == null;
        return bh ?? SoftDefault();
    }

    /// <summary>R theo TCVN 9362:2012 cho đất tại độ sâu z (φ, c cho trước), bề rộng b; γ tại z (đẩy nổi dưới MNN), γ' trên = σ'bt(z)/z.</summary>
    public static double BearingAt(SoilSettings baseSoil, Borehole bh, double z, double b, double phi, double c)
    {
        var s = GeotechCalc.CopySoil(baseSoil);
        s.ROverride = 0;
        s.Phi = phi; s.C = c;
        s.Gamma = GeotechCalc.EffectiveUnitWeight(bh, z);
        s.Depth = Math.Max(z, 0);
        s.GammaAbove = z > 1e-6 ? GeotechCalc.EffectiveStress(bh, z) / z : s.Gamma;
        return FootingDesigner.BearingR(s, Math.Max(b, 0.1));
    }

    /// <summary>Hệ số nhóm Converse–Labarre: η = 1 − θ/90·[(k − 1)·m + (m − 1)·k]/(m·k), θ = arctg(d/s) (độ).</summary>
    public static double ConverseLabarre(int m, int k, double d, double s)
    {
        if (m <= 1 && k <= 1) return 1;
        double th = Math.Atan(d / Math.Max(s, 1e-6)) * 180 / Math.PI;
        return Math.Clamp(1 - th / 90 * ((k - 1) * m + (m - 1) * k) / (double)(m * k), 0.3, 1.0);
    }

    /// <summary>Thể tích một cây cừ thân côn: V = π·L/12·(D1² + D1·D2 + D2²).</summary>
    public static double TaperVolume(double L, double d1, double d2) => Math.PI * L / 12 * (d1 * d1 + d1 * d2 + d2 * d2);

    /// <summary>Hệ số rỗng sau gia cố: e1 = e0 − (1 + e0)·Vc/V (đất bị ép chặt bởi thể tích cây cừ chiếm chỗ).</summary>
    public static double ImprovedVoidRatio(double e0, double volumeRatio) => e0 - (1 + e0) * volumeRatio;

    /// <summary>Số cây cừ dưới móng: n = ⌈mật độ × diện tích⌉.</summary>
    public static int CountFor(double density, double area) => (int)Math.Ceiling(Math.Max(density, 0) * Math.Max(area, 0) - 1e-9);

    // ------------------------------------------------------------------
    // TÍNH TOÁN
    // ------------------------------------------------------------------
    /// <summary>Tính cừ tràm dưới móng L × B (đáy móng ở độ sâu Df). ptb, pmax: áp lực tiêu chuẩn đáy móng (kPa, gồm trọng lượng móng + đất) –
    /// NaN = chỉ tính sức chịu tải (không lập bảng kiểm tra áp lực); ntc: lực nén tiêu chuẩn chân cột lớn nhất (kN) – cảnh báo tải lớn.</summary>
    public static CuTramResult Evaluate(CuTramSettings cs, SoilSettings soil, Borehole bh, bool assumed, double L, double B, double Df,
        double ptb = double.NaN, double pmax = double.NaN, double ntc = double.NaN)
    {
        cs ??= new CuTramSettings();
        soil ??= new SoilSettings();
        bh ??= SoftDefault();
        L = Math.Max(L, 0.2); B = Math.Max(B, 0.2); Df = Math.Max(Df, 0);
        double Lc = Math.Clamp(cs.Length, 0.5, 12), d1 = Math.Clamp(cs.TopDiameter, 0.02, 0.4), d2 = Math.Clamp(cs.TipDiameter, 0.01, d1);
        double rho = Math.Clamp(cs.Density, 1, 100), tl = Math.Max(cs.LeanThickness, 0), ts = Math.Clamp(cs.SandCushion, 0, Math.Max(Lc - 0.3, 0));
        var r = new CuTramResult
        {
            Borehole = bh.Name, AssumedSoil = assumed, L = L, B = B, Df = Df, Length = Lc, D1 = d1, D2 = d2, Density = rho, SandCushion = ts, LeanThickness = tl,
            Method = cs.Method, SafetyFactor = cs.SafetyFactor > 0 ? cs.SafetyFactor : 2.0, Ntc = ntc, Ptb = ptb, Pmax = pmax
        };
        r.TopDepth = Df + tl;
        r.SoilTop = r.TopDepth + ts;
        r.TipDepth = r.TopDepth + Lc;
        double A = L * B;
        if (assumed) r.Warnings.Add("CHƯA CÓ HỐ KHOAN – dùng nền yếu giả định để ước tính cừ tràm; bắt buộc khảo sát địa chất trước khi thiết kế");

        // ---- (1) cừ đơn ----
        r.U = Math.PI * (d1 + d2) / 2;
        r.Ap = Math.PI * d2 * d2 / 4;
        r.Volume1 = TaperVolume(Lc, d1, d2);
        var pts = new List<double> { r.SoilTop, r.TipDepth };
        foreach (var l in bh.Layers.Where(l => l != null))
        {
            if (l.Top > r.SoilTop + 1e-9 && l.Top < r.TipDepth - 1e-9) pts.Add(l.Top);
            if (l.Bottom > r.SoilTop + 1e-9 && l.Bottom < r.TipDepth - 1e-9) pts.Add(l.Bottom);
        }
        pts = pts.Distinct().OrderBy(v => v).ToList();
        double nSum = 0, phiSum = 0, len = 0;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            double a = pts[i], b = pts[i + 1];
            int k = Math.Max(1, (int)Math.Ceiling((b - a) / 1.0 - 1e-9));   // chia đoạn ≤ 1 m (TCVN 10304: lớp phân tố ≤ 2 m)
            var lay = GeotechCalc.LayerAt(bh, (a + b) / 2);
            double il = TableIndex(lay, out string note);
            for (int j = 0; j < k; j++)
            {
                double s0 = a + (b - a) * j / k, s1 = a + (b - a) * (j + 1) / k;
                double f = lay == null || lay.Kind == SoilKind.Fill ? 0 : Friction((s0 + s1) / 2, il);
                var row = new CuTramShaftRow
                {
                    From = s0, To = s1, Soil = lay == null ? "?" : lay.Name + " (" + lay.KindText + ")", IL = il, Cohesive = lay?.IsCohesive ?? true, F = f,
                    Q = r.U * f * (s1 - s0), Note = lay?.Kind == SoilKind.Fill ? "đất đắp – không kể ma sát" : note
                };
                r.Rows.Add(row);
            }
            nSum += Math.Min(lay?.N ?? 0, GeotechCalc.MaxN) * (b - a);
            phiSum += (lay?.Phi ?? 0) * (b - a);
            len += b - a;
        }
        r.Qs = r.Rows.Sum(x => x.Q);
        r.NAvg = len > 1e-9 ? nSum / len : 0;
        var tip = GeotechCalc.LayerAt(bh, r.TipDepth);
        r.TipLayer = tip == null ? "?" : tip.Name + " (" + tip.KindText + ")";
        if (tip == null || tip.Kind == SoilKind.Fill) r.QpUnit = 0;
        else if (tip.IsCohesive) r.QpUnit = 9 * (tip.C > 0 ? tip.C : 6.25 * Math.Min(tip.N, GeotechCalc.MaxN));
        else r.QpUnit = 300 * Math.Min(tip.N, GeotechCalc.MaxN);
        r.Qp = r.QpUnit * r.Ap;
        r.Pvl = Math.Max(cs.TimberStrength, 0) * r.Ap;

        // ---- (1) nhóm cừ – nền phức hợp ----
        r.Count = CountFor(rho, A);
        r.Spacing = 1 / Math.Sqrt(rho);
        r.Nx = Math.Max(1, (int)Math.Round(L / r.Spacing)); r.Ny = Math.Max(1, (int)Math.Round(B / r.Spacing));
        r.Eta = cs.GroupEfficiency > 0 ? Math.Min(cs.GroupEfficiency, 1) : ConverseLabarre(r.Nx, r.Ny, (d1 + d2) / 2, r.Spacing);
        r.AreaRatio = Math.Min(r.Count * Math.PI * Math.Pow((d1 + d2) / 2, 2) / 4 / A, 1);
        r.SoilShare = Math.Clamp(cs.SoilShare, 0, 1);
        var baseLayer = GeotechCalc.LayerAt(bh, Df);
        r.R0 = BearingAt(soil, bh, Df, Math.Min(L, B), baseLayer?.Phi ?? soil.Phi, baseLayer?.C ?? soil.C);
        double wl = bh.Water;
        double gT = Math.Max(cs.TimberUnitWeight, 0), gTw = Math.Max(gT - GeotechCalc.GammaW, 0);
        double subLen = Math.Clamp(r.TipDepth - Math.Max(wl, r.TopDepth), 0, Lc);
        r.TimberWeight = r.Count * r.Volume1 / Lc * (gT * (Lc - subLen) + gTw * subLen);
        r.P1 = (r.Eta * r.Count * r.Qa - r.TimberWeight) / A + r.SoilShare * (1 - r.AreaRatio) * r.R0;

        // ---- (2) nền tương đương sau gia cố ----
        var tl0 = GeotechCalc.LayerAt(bh, r.SoilTop) ?? baseLayer;
        r.E0Assumed = !(tl0?.VoidRatio > 0);
        r.E0 = r.E0Assumed ? Math.Max(cs.DefaultVoidRatio, 0.3) : tl0.VoidRatio;
        r.VolumeRatio = rho * r.Volume1 / Lc;
        r.E1 = Math.Max(ImprovedVoidRatio(r.E0, r.VolumeRatio), 0.3);
        r.Phi0 = tl0?.Phi ?? soil.Phi; r.C0 = tl0?.C ?? soil.C;
        // kinh nghiệm (người dùng kiểm tra / nhập theo thí nghiệm): φ1 = φ0 + 10°·(e0 − e1)/e0;  c1 = c0·e0/e1
        r.Phi1 = cs.ImprovedPhi > 0 ? cs.ImprovedPhi : r.Phi0 + 10 * (r.E0 - r.E1) / Math.Max(r.E0, 1e-6);
        r.C1 = cs.ImprovedC > 0 ? cs.ImprovedC : r.C0 * r.E0 / Math.Max(r.E1, 1e-6);
        r.R1 = BearingAt(soil, bh, Df, Math.Min(L, B), r.Phi1, r.C1);
        r.PhiAvg = len > 1e-9 ? phiSum / len : 0;
        double ext = 2 * (r.TipDepth - Df) * Math.Tan(r.PhiAvg / 4 * Math.PI / 180);
        r.Lqu = L + ext; r.Bqu = B + ext;
        r.BlockWeight = r.Aqu * (GeotechCalc.EffectiveStress(bh, r.TipDepth) - GeotechCalc.EffectiveStress(bh, Df));
        r.RTip = BearingAt(soil, bh, r.TipDepth, Math.Min(r.Lqu, r.Bqu), tip?.Phi ?? 0, tip?.C ?? 0);
        r.P2 = Math.Min(r.R1, (r.RTip * r.Aqu - r.BlockWeight) / A);

        r.Allowable = cs.Method switch
        {
            CuTramMethod.PileFriction => r.P1,
            CuTramMethod.EquivalentSoil => r.P2,
            _ => Math.Min(r.P1, r.P2)
        };
        r.Allowable = Math.Max(r.Allowable, 1);

        // ---- mực nước ngầm ----
        if (cs.MinWaterDepth >= 0) r.WaterDepth = cs.MinWaterDepth;
        else { r.WaterDepth = wl; r.WaterFromBorehole = true; }

        // ---- lún khối quy ước ----
        double pUse = double.IsFinite(ptb) ? ptb : r.Allowable;
        r.SigmaTip = (pUse * A + r.BlockWeight) / r.Aqu;
        try
        {
            r.Settlement = GeotechCalc.Settlement(bh, r.Lqu, r.Bqu, r.TipDepth, r.SigmaTip);
            r.Settlement.Limit = cs.SettlementLimit > 0 ? cs.SettlementLimit : GeotechCalc.SettlementLimit;
        }
        catch (Exception ex) { r.Warnings.Add("Không tính được lún khối quy ước: " + ex.Message); }
        if (!double.IsFinite(ptb)) r.SigmaTip = double.NaN;

        Explain(r, cs);
        BuildItems(r, cs);
        return r;
    }

    /// <summary>Tính cừ tràm cho chân cột / dải tại (x, y) theo thông số dự án.</summary>
    public static CuTramResult Evaluate(Project p, double x, double y, SoilSettings soil, double L, double B,
        double ptb = double.NaN, double pmax = double.NaN, double ntc = double.NaN)
    {
        var bh = BoreholeFor(p, x, y, out bool assumed);
        soil ??= p?.Soil ?? new SoilSettings();
        return Evaluate(p?.Foundation?.CuTram, soil, bh, assumed, L, B, soil.Depth, ptb, pmax, ntc);
    }

    private static void Explain(CuTramResult r, CuTramSettings cs)
    {
        var n = r.Notes;
        n.Add($"Cừ tràm dài Lc = {F(r.Length)} m, Ø gốc D1 = {F(r.D1 * 100, "0.#")} cm, Ø ngọn D2 = {F(r.D2 * 100, "0.#")} cm, mật độ {F(r.Density, "0")} cây/m² (bước s = 1/√{F(r.Density, "0")} = {F(r.Spacing, "0.###")} m); hố khoan {r.Borehole}{(r.AssumedSoil ? " (giả định)" : "")}");
        n.Add($"Đầu cừ tại đáy lớp bê tông lót: z_đầu = Df + t_lót = {F(r.Df)} + {F(r.LeanThickness)} = {F(r.TopDepth)} m; cát đệm {F(r.SandCushion * 1000, "0")} mm → cừ tiếp xúc đất từ {F(r.SoilTop)} m; mũi cừ z_mũi = {F(r.TipDepth)} m");
        n.Add($"(1) Cừ đơn: u = π(D1 + D2)/2 = {F(r.U, "0.####")} m; Ap = πD2²/4 = {F(r.Ap, "0.######")} m²; Qs = u·Σfi·li = {F(r.Qs, "0.##")} kN (fi theo TCVN 10304:2014, bảng ma sát cọc đóng/ép theo z và IL)");
        n.Add($"    mũi trong lớp {r.TipLayer}: qp = {F(r.QpUnit, "0")} kPa (9·cu đất dính / 300·N đất rời) → Qp = qp·Ap = {F(r.Qp, "0.###")} kN; Qc = Qs + Qp = {F(r.Qc, "0.##")} kN");
        n.Add($"    Qa = min(Qc/FS; Pvl) = min({F(r.Qc, "0.##")}/{F(r.SafetyFactor)}; {F(r.Pvl, "0.#")}) = {F(r.Qa, "0.###")} kN (Pvl = Rgỗ·Ap, Rgỗ = {F(cs.TimberStrength / 1000, "0.#")} MPa – kinh nghiệm)");
        n.Add($"    Nhóm: n = ⌈{F(r.Density, "0")} × {F(r.L)} × {F(r.B)}⌉ = {r.Count} cây (lưới ≈ {r.Nx}×{r.Ny}); η = {F(r.Eta, "0.###")} (Converse–Labarre){(cs.GroupEfficiency > 0 ? " – người dùng nhập" : "")}; m = n·Ap,tb/A = {F(r.AreaRatio, "0.###")}");
        n.Add($"    [p]1 = (η·n·Qa − Gcừ)/A + αs·(1 − m)·R0 = ({F(r.Eta, "0.###")}×{r.Count}×{F(r.Qa, "0.###")} − {F(r.TimberWeight, "0.##")})/{F(r.L * r.B, "0.###")} + {F(r.SoilShare)}×(1 − {F(r.AreaRatio, "0.###")})×{F(r.R0, "0.#")} = {F(r.P1, "0.#")} kPa");
        n.Add($"(2) Nền tương đương: Vc/V = mật độ·Vcừ/Lc = {F(r.VolumeRatio, "0.####")}; e1 = e0 − (1 + e0)·Vc/V = {F(r.E0, "0.###")} − (1 + {F(r.E0, "0.###")})×{F(r.VolumeRatio, "0.####")} = {F(r.E1, "0.###")}{(r.E0Assumed ? " (e0 giả định – lớp đất chưa có hệ số rỗng)" : "")}");
        n.Add($"    φ1 = {F(r.Phi1, "0.#")}°, c1 = {F(r.C1, "0.#")} kPa {(cs.ImprovedPhi > 0 || cs.ImprovedC > 0 ? "(người dùng nhập)" : "(kinh nghiệm: φ1 = φ0 + 10°·Δe/e0, c1 = c0·e0/e1 – người dùng kiểm tra)")}; R1 (TCVN 9362:2012, b = {F(Math.Min(r.L, r.B))} m) = {F(r.R1, "0.#")} kPa");
        n.Add($"    Khối móng quy ước tại mũi cừ: α = φtb/4 = {F(r.PhiAvg / 4, "0.##")}°; Lqu = L + 2·(z_mũi − Df)·tgα = {F(r.Lqu, "0.###")} m, Bqu = {F(r.Bqu, "0.###")} m; W = Aqu·[σ'bt(z_mũi) − σ'bt(Df)] = {F(r.BlockWeight, "0.#")} kN");
        n.Add($"    R_mũi (b = Bqu, h = z_mũi) = {F(r.RTip, "0.#")} kPa;  [p]2 = min(R1; (R_mũi·Aqu − W)/A) = {F(r.P2, "0.#")} kPa");
        n.Add($"Sức chịu tải dùng thiết kế [p] = {F(r.Allowable, "0.#")} kPa – {CuTramSettings.MethodName(r.Method)}");
    }

    private static void BuildItems(CuTramResult r, CuTramSettings cs)
    {
        var it = r.Items;
        bool use1 = r.Method != CuTramMethod.EquivalentSoil, use2 = r.Method != CuTramMethod.PileFriction;
        if (double.IsFinite(r.Ptb))
        {
            it.Add(SafetyItem.Make(Group, "PP(1) nền phức hợp: ptb ≤ [p]1", r.Ptb, r.P1, "kPa", "[p]1 = (η·n·Qa − Gcừ)/A + αs·(1 − m)·R0",
                "Nhóm cừ + đất giữa cừ đủ chịu tải trung bình", "Áp lực trung bình vượt [p]1 – tăng kích thước móng / mật độ / chiều dài cừ", false, use1 ? CheckStatus.Fail : CheckStatus.Warning));
            if (double.IsFinite(r.Pmax))
                it.Add(SafetyItem.Make(Group, "PP(1): pmax ≤ 1,2[p]1", r.Pmax, 1.2 * r.P1, "kPa", "TCVN 9362:2012 mục 4.6 (pmax ≤ 1,2R)",
                    "Áp lực mép móng trong giới hạn", "Áp lực mép móng vượt 1,2[p]1", false, use1 ? CheckStatus.Fail : CheckStatus.Warning));
            it.Add(SafetyItem.Make(Group, "PP(2) nền tương đương: ptb ≤ R1 (đáy móng)", r.Ptb, r.R1, "kPa", "R1 = m1m2/ktc·(A·b·γ + B·h·γ' + D·c1) với φ1, c1 sau gia cố",
                "Nền sau gia cố đủ chịu lực tại đáy móng", "Vượt R1 của nền sau gia cố", false, use2 ? CheckStatus.Fail : CheckStatus.Warning));
            it.Add(SafetyItem.Make(Group, "PP(2) khối quy ước: σ mũi cừ ≤ R mũi", r.SigmaTip, r.RTip, "kPa", "σ = (ptb·A + W)/(Lqu·Bqu), mở rộng góc φtb/4",
                "Lớp đất dưới mũi cừ đủ chịu lực", "Áp lực tại mũi cừ vượt R – tăng chiều dài cừ / chuyển cọc BTCT", false, use2 ? CheckStatus.Fail : CheckStatus.Warning));
        }
        if (r.Settlement != null)
            it.Add(SafetyItem.Make(Group, "Độ lún khối quy ước S ≤ Sgh", r.Settlement.S * 100, r.Settlement.Limit * 100, "cm", "Cộng lún từng lớp dưới mũi cừ (TCVN 9362:2012 Phụ lục C)",
                "Độ lún trong giới hạn", "Độ lún vượt giới hạn – xem xét cọc BTCT / gia tải trước"));
        double wl = r.WaterDepth;
        var dur = new SafetyItem
        {
            Group = Group, Name = "Độ bền: đầu cừ nằm dưới MNN thấp nhất", Value = r.TopDepth, Limit = double.IsFinite(wl) ? wl : 0, Unit = "m", Inverse = true,
            Formula = $"z_đầu cừ = {F(r.TopDepth)} m ≥ z_MNN thấp nhất = {(double.IsFinite(wl) ? F(wl) : "không gặp")} m"
        };
        if (!double.IsFinite(wl))
        {
            dur.Status = CheckStatus.Fail;
            dur.Explanation = "KHÔNG ĐẠT: không gặp nước ngầm – cừ tràm nằm trên mực nước ngầm sẽ mục; dùng cọc BTCT (hoặc nhập MNN thấp nhất nếu có số liệu)";
        }
        else if (r.DurabilityOk)
        {
            dur.Status = CheckStatus.Ok;
            dur.Explanation = "Đầu cừ luôn ngập nước – gỗ tràm không bị mục";
        }
        else
        {
            dur.Status = CheckStatus.Fail;
            dur.Explanation = "KHÔNG ĐẠT: cừ tràm nằm trên mực nước ngầm sẽ mục – hạ cao độ đầu cừ hoặc dùng cọc BTCT";
        }
        it.Add(dur);
        if (r.WaterFromBorehole && double.IsFinite(wl))
            r.Warnings.Add($"Mực nước ngầm lấy theo hố khoan {r.Borehole} ({F(wl)} m) – là mực nước lúc khảo sát, chưa chắc là MNN thấp nhất (mùa khô): nhập 'MNN thấp nhất' để kiểm tra độ bền cừ");
        it.Add(SafetyItem.Make(Group, "Đất phù hợp đóng cừ: N trung bình dọc thân ≤ 5", r.NAvg, SoftN, "búa", "Kinh nghiệm: cừ tràm hiệu quả trong bùn / sét dẻo chảy (N < 5)",
            "Đất yếu – cừ tràm phát huy hiệu quả nén chặt", "Đất không yếu: đóng cừ khó, gãy cừ, ít hiệu quả – xem xét móng nông thường hoặc cọc BTCT", false, CheckStatus.Warning));
        if (double.IsFinite(r.Ntc))
            it.Add(SafetyItem.Make(Group, "Tải chân cột phù hợp cừ tràm Ntc ≤ Nmax", r.Ntc, cs.MaxColumnLoad, "kN", "Kinh nghiệm (nhà thấp tầng) – người dùng kiểm tra",
                "Tải nhỏ, phù hợp gia cố cừ tràm", "Tải chân cột lớn – khuyến nghị móng cọc BTCT", false, CheckStatus.Warning));
        it.Add(SafetyItem.Make(Group, "Cừ đơn: Qc/FS ≤ Pvl (vật liệu gỗ)", r.Qc / Math.Max(r.SafetyFactor, 1e-6), r.Pvl, "kN", "Pvl = Rgỗ·Ap (tiết diện ngọn)",
            "Sức chịu tải do đất quyết định", "Sức chịu tải bị giới hạn bởi vật liệu gỗ (Qa = Pvl)", false, CheckStatus.Info));
        if (r.Allowable < r.R0 - 1e-6)
            r.Warnings.Add($"Sức chịu tải sau gia cố [p] = {F(r.Allowable, "0.#")} kPa nhỏ hơn R nền thiên nhiên R0 = {F(r.R0, "0.#")} kPa theo phương pháp đang dùng – cừ tràm không cải thiện; kiểm tra lại αs / phương pháp / số liệu đất");
        foreach (var x in it.Where(i => i.Status == CheckStatus.Fail)) r.Warnings.Add("LỖI cừ tràm – " + x.Name + ": " + x.Explanation);
        foreach (var x in it.Where(i => i.Status == CheckStatus.Warning)) r.Warnings.Add("Cảnh báo cừ tràm – " + x.Name + ": " + x.Explanation);
    }

    /// <summary>Ghi kết quả cừ tràm vào danh sách kiểm tra móng (CheckItem): độ bền (MNN), lún khối quy ước, áp lực mũi (PP 2).</summary>
    public static void AddChecks(List<CheckItem> checks, List<string> messages, CuTramResult r)
    {
        if (r == null) return;
        double wl = double.IsFinite(r.WaterDepth) ? r.WaterDepth : 99;
        checks.Add(new CheckItem
        {
            Name = "Cừ tràm: đầu cừ dưới MNN thấp nhất (z_MNN ≤ z_đầu cừ)", Demand = wl, Capacity = r.TopDepth, Unit = "m",
            Note = r.DurabilityOk ? "đầu cừ luôn ngập nước" : "KHÔNG ĐẠT: cừ tràm nằm trên mực nước ngầm sẽ mục – hạ cao độ đầu cừ hoặc dùng cọc BTCT"
        });
        if (r.Settlement != null)
            checks.Add(new CheckItem { Name = "Cừ tràm: độ lún khối quy ước S ≤ Sgh", Demand = r.Settlement.S * 100, Capacity = r.Settlement.Limit * 100, Unit = "cm", Note = "mũi cừ " + F(r.TipDepth) + " m" });
        if (r.Method != CuTramMethod.PileFriction && double.IsFinite(r.SigmaTip))
            checks.Add(new CheckItem { Name = "Cừ tràm: áp lực mũi khối quy ước σ ≤ R mũi", Demand = r.SigmaTip, Capacity = r.RTip, Unit = "kN/m²", Note = $"Lqu×Bqu = {F(r.Lqu)}×{F(r.Bqu)} m" });
        messages.Add($"Gia cố nền cừ tràm: {r.Count} cây Ø{F(r.D1 * 100, "0")}–{F(r.D2 * 100, "0.#")} cm dài {F(r.Length)} m ({F(r.Density, "0")} cây/m²); [p]1 = {F(r.P1, "0")}, [p]2 = {F(r.P2, "0")} → [p] = {F(r.Allowable, "0")} kN/m² ({CuTramSettings.MethodName(r.Method)})");
        foreach (var w in r.Warnings.Where(w => !messages.Contains(w))) messages.Add(w.StartsWith("LỖI") ? w.Replace("LỖI cừ tràm", "Cảnh báo: cừ tràm") : w.StartsWith("Cảnh báo") ? w : "Cảnh báo: " + w);
    }

    /// <summary>Đánh dấu phương án móng nông trên nền cừ tràm KHÔNG KHẢ THI (móng dừng ở kích thước giới hạn): hạng mục KHÔNG ĐẠT + thông báo LỖI
    /// "không khả thi – dùng cọc" (móng hiển thị KHÔNG ĐẠT, không phóng to móng vô hạn).</summary>
    public static void MarkInfeasible(CuTramResult r, List<string> messages, string why)
    {
        if (r == null) return;
        r.Feasible = false;
        r.Conclusion = "KHÔNG KHẢ THI – dùng móng cọc BTCT: " + why;
        r.Items.Insert(0, new SafetyItem
        {
            Group = Group, Name = "Khả thi móng nông trên nền cừ tràm", Value = r.Ptb, Limit = r.Allowable, Unit = "kPa", Status = CheckStatus.Fail,
            Formula = "Kích thước móng ≤ 0,5·bước cột (móng không chạm nhau)", Explanation = r.Conclusion
        });
        r.Warnings.Insert(0, "LỖI cừ tràm – " + r.Conclusion);
        messages?.Add("LỖI móng trên nền cừ tràm KHÔNG KHẢ THI – dùng móng cọc BTCT (hoặc móng băng / bè trên nền gia cố): " + why);
    }

    /// <summary>Lún lệch giữa các móng cừ tràm kề nhau: ΔS/l ≤ 0,002 (TCVN 9362:2012 Bảng 16, khung BTCT) – thêm cảnh báo vào móng.</summary>
    public static void DifferentialSettlement(IList<FootingDesign> footings, double limit = 0.002)
    {
        var list = footings.Where(f => f.CuTram?.Settlement != null).ToList();
        foreach (var a in list)
        {
            FootingDesign worst = null; double wr = 0, wl = 0;
            foreach (var b in footings.Where(f => !ReferenceEquals(f, a)))
            {
                double l = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
                if (l < 0.5 || l > 9) continue;
                double sb = b.CuTram?.Settlement?.S ?? b.Checks.FirstOrDefault(c => c.Name == "Độ lún S ≤ Sgh")?.Demand / 100 ?? double.NaN;
                if (!double.IsFinite(sb)) continue;
                double ratio = Math.Abs(a.CuTram.Settlement.S - sb) / l;
                if (ratio > wr) { wr = ratio; worst = b; wl = l; }
            }
            if (worst != null && wr > limit)
                a.Messages.Add($"Cảnh báo: lún lệch giữa {a.Name} và {worst.Name} ΔS/l = {F(wr, "0.0000")} > {F(limit, "0.###")} (l = {F(wl)} m) – đồng đều kích thước móng / mật độ cừ hoặc bổ sung giằng móng");
        }
    }
}
