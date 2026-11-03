using System.Globalization;
using System.Text.RegularExpressions;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Verification;

/// <summary>Tính tay ĐỘC LẬP theo công thức của tiêu chuẩn – viết lại trực tiếp, không gọi các lớp thiết kế của chương trình
/// (RcSection, SlabDesigner, FootingDesigner, GeotechCalc…). Đơn vị kN, m, kN/m². Dùng làm giá trị tham chiếu cho bộ kiểm chứng.</summary>
public static partial class HandCalc
{
    /// <summary>Biến dạng tới hạn của bê tông vùng nén εb2 = 0,0035 (TCVN 5574:2018, tác dụng ngắn hạn).</summary>
    public const double EpsB2 = 0.0035;

    /// <summary>ξR = 0,8/(1 + εs,el/εb2), εs,el = Rs/Es (TCVN 5574:2018 mục 8.1.2.2).</summary>
    public static double XiR(double Rs, double Es) => 0.8 / (1 + Rs / Es / EpsB2);

    /// <summary>Uốn tiết diện chữ nhật cốt đơn theo con đường "ζ" của sách giáo khoa:
    /// αm = M/(Rb·b·h0²); ξ = 1 − √(1 − 2αm); ζ = 1 − ξ/2 = (1 + √(1 − 2αm))/2; As = M/(Rs·ζ·h0).
    /// Trả về αm, ξ, ζ, As (m²), ξR, αR = ξR(1 − ξR/2).</summary>
    public static (double AlphaM, double Xi, double Zeta, double As, double XiR, double AlphaR) FlexureSingly(double M, double b, double h0, double Rb, double Rs, double Es)
    {
        double am = M / (Rb * b * h0 * h0);
        double root = Math.Sqrt(1 - 2 * am);
        double xi = 1 - root, zeta = (1 + root) / 2;
        double xr = XiR(Rs, Es);
        return (am, xi, zeta, M / (Rs * zeta * h0), xr, xr * (1 - 0.5 * xr));
    }

    /// <summary>Uốn cốt kép (αm &gt; αR): lấy x = ξR·h0; cân bằng mô men với trọng tâm cốt chịu kéo:
    /// As' = [M − Rb·b·x·(h0 − x/2)] / [Rsc·(h0 − a')]; cân bằng lực: As = (Rb·b·x + Rsc·As')/Rs.</summary>
    public static (double AsComp, double As, double X) FlexureDoubly(double M, double b, double h0, double aComp, double Rb, double Rs, double Rsc, double Es)
    {
        double x = XiR(Rs, Es) * h0;
        double asc = (M - Rb * b * x * (h0 - x / 2)) / (Rsc * (h0 - aComp));
        return (asc, (Rb * b * x + Rsc * asc) / Rs, x);
    }

    /// <summary>Khả năng chịu mô men cốt đơn: x = Rs·As/(Rb·b) (≤ ξR·h0); Mu = Rs·As·(h0 − x/2).</summary>
    public static double MomentCapacitySingly(double As, double b, double h0, double Rb, double Rs, double Es)
    {
        double x = Math.Min(Rs * As / (Rb * b), XiR(Rs, Es) * h0);
        return Rb * b * x * (h0 - x / 2);
    }

    /// <summary>Cắt theo tiết diện nghiêng (TCVN 5574:2018 mục 8.1.3): Qb = φb2·Rbt·b·h0²/c với φb2 = 1,5, giới hạn 0,5…2,5·Rbt·b·h0;
    /// qsw = Rsw·Asw/sw; Qsw = φsw·qsw·c0, φsw = 0,75, c0 ≤ 2h0 (đai chỉ kể khi qsw ≥ 0,25·Rbt·b); Qu = Qb + Qsw ≤ 0,3·Rb·b·h0 (dải nén).</summary>
    public static (double Qb, double Qsw, double Qu, double Qstrut, double Qsw1) Shear(double b, double h0, double c, double Rb, double Rbt, double Rsw, double Asw, double s)
    {
        double qb = 1.5 * Rbt * b * h0 * h0 / c;
        qb = Math.Min(Math.Max(qb, 0.5 * Rbt * b * h0), 2.5 * Rbt * b * h0);
        double qsw = Rsw * Asw / s;
        double c0 = Math.Min(c, 2 * h0);
        double Qsw = qsw >= 0.25 * Rbt * b ? 0.75 * qsw * c0 : 0;
        double strut = 0.3 * Rb * b * h0;
        return (qb, Qsw, Math.Min(qb + Qsw, strut), strut, qsw);
    }

    /// <summary>Nén lệch tâm tiết diện chữ nhật cốt đối xứng As = As', trường hợp lệch tâm lớn (2a' ≤ x ≤ ξR·h0), khối ứng suất chữ nhật:
    /// x = N/(Rb·b); điều kiện N·e ≤ Rb·b·x·(h0 − x/2) + Rsc·As'·(h0 − a') với e = e0 + (h0 − a')/2
    /// → mô men giới hạn đối với trọng tâm: Mu = Rb·b·x·(h0 − x/2) + Rsc·As'·(h0 − a') − N·(h0 − a')/2.</summary>
    public static (double Mu, double X, double Xi) ColumnSymmetric(double N, double b, double h, double a, double As1, double Rb, double Rsc)
    {
        double h0 = h - a, x = N / (Rb * b);
        return (Rb * b * x * (h0 - x / 2) + Rsc * As1 * (h0 - a) - N * (h0 - a) / 2, x, x / h0);
    }

    /// <summary>Hệ số uốn dọc η = 1/(1 − N/Ncr), Ncr = π²·D/l0², D = kb·Eb·I + ks·Es·Is, kb = 0,15/(φL·(0,3 + δe)), ks = 0,7,
    /// δe = e0/h trong khoảng [0,15; 1,5] (TCVN 5574:2018, độ cứng quy ước khi kể uốn dọc).</summary>
    public static (double Eta, double Ncr, double D) Eta(double N, double M, double l0, double b, double h, double Is, double Eb, double Es, double phiL)
    {
        double I = b * h * h * h / 12;
        double de = Math.Min(Math.Max(Math.Abs(M) / N / h, 0.15), 1.5);
        double kb = 0.15 / (phiL * (0.3 + de));
        double D = kb * Eb * I + 0.7 * Es * Is;
        double ncr = Math.PI * Math.PI * D / (l0 * l0);
        return (1 / (1 - N / ncr), ncr, D);
    }

    /// <summary>Độ cứng chống uốn tiết diện chữ nhật có kể nứt và từ biến – theo công thức chương trình công bố (gần đúng 8.2.3 TCVN 5574:2018):
    /// Eb,red = Eb/(1 + φb,cr), φb,cr = 2,5; αs = Es/Eb,red; vùng nén tiết diện nứt x = h0·(√((αs·μ)² + 2αs·μ) − αs·μ), μ = As/(b·h0);
    /// Icr = b·x³/3 + αs·As·(h0 − x)²; Mcr = 1,3·Rbt·b·h²/6; Ieff = Icr/(1 − (1 − Icr/Ig)(Mcr/Ms)²) (Bischoff), Ig ≥ Ieff ≥ Icr; B = Eb,red·Ieff.</summary>
    public static double CrackedStiffness(double b, double h, double h0, double As, double Eb, double Rbt, double Es, double Ms)
    {
        double ebRed = Eb / (1 + 2.5);
        double ig = b * h * h * h / 12;
        double alpha = Es / ebRed, mu = As / (b * h0), am = alpha * mu;
        double x = h0 * (Math.Sqrt(am * am + 2 * am) - am);
        double icr = b * x * x * x / 3 + alpha * As * (h0 - x) * (h0 - x);
        double mcr = 1.3 * Rbt * b * h * h / 6;
        double ieff = Math.Abs(Ms) <= mcr ? ig : icr / (1 - (1 - icr / ig) * Math.Pow(mcr / Math.Abs(Ms), 2));
        return ebRed * Math.Min(ig, Math.Max(icr, ieff));
    }
}

public static partial class Benchmarks
{
    // hằng số vật liệu nhập tay theo TCVN 5574:2018 (không đọc thư viện vật liệu của chương trình)
    private const double RbB25 = 14.5e3, RbtB25 = 1.05e3, EbB25 = 30.0e6;            // Bảng 7, Bảng 10
    private const double RsCB400 = 350e3, RscCB400 = 350e3, RsCB300 = 260e3, RswCB240 = 170e3, EsSteel = 200.0e6;   // Bảng 13

    private static double Bar(int d) => Math.PI * d * d / 4 * 1e-6;

    // ==================================================================
    // B. THIẾT KẾ BTCT
    // ==================================================================
    private static IEnumerable<Suite> DesignSuites()
    {
        const string G = GroupConcrete;
        yield return S("B01", G, "Dầm chữ nhật cốt đơn – tính As", FlexureSinglyCase);
        yield return S("B02", G, "Dầm chữ nhật cốt kép – tính As, As'", FlexureDoublyCase);
        yield return S("B03", G, "Khả năng chịu mô men tiết diện đã bố trí thép", MomentCapacityCase);
        yield return S("B04", G, "Chịu cắt: bê tông + cốt đai (tiết diện nghiêng c = 2h0)", ShearCase);
        yield return S("B05", G, "Cột nén lệch tâm cốt đối xứng (lệch tâm lớn)", ColumnCase);
        yield return S("B06", G, "Hệ số uốn dọc η của cột mảnh", EtaCase);
        yield return S("B07", G, "Độ cứng chống uốn có kể nứt và từ biến", StiffnessCase);
        yield return S("B08", G, "Sàn bản dầm (một phương) – mô men, khả năng chịu lực, độ võng", OneWaySlab);
    }

    private static Material B25() => Material.Concrete("B25");
    private static Material CB400() => Material.Rebar("CB400-V");

    // ---- B01 ----
    private static List<BenchmarkCase> FlexureSinglyCase()
    {
        const string G = GroupConcrete, T = "Dầm chữ nhật cốt đơn – tính As";
        double M = 120, b = 0.22, h = 0.50, a = 0.04, h0 = h - a;
        var hc = HandCalc.FlexureSingly(M, b, h0, RbB25, RsCB400, EsSteel);
        var fr = RcSection.DesignRect(M, b, h0, 0.035, B25(), CB400(), 0);
        var fr300 = RcSection.DesignRect(M, b, h0, 0.035, B25(), Material.Rebar("CB300-V"), 0);
        string desc = $"Tiết diện {b * 1000:0}×{h * 1000:0} mm, a = {a * 1000:0} mm (h0 = {h0 * 1000:0} mm), M = {M} kN.m, bê tông B25 (Rb = 14,5 MPa), thép CB400-V (Rs = 350 MPa, Es = 2·10⁵ MPa).";
        string src = "TCVN 5574:2018 mục 8.1.2.2 – tính tay: αm = M/(Rb·b·h0²), ζ = (1 + √(1 − 2αm))/2, As = M/(Rs·ζ·h0)";
        return new List<BenchmarkCase>
        {
            C("B01-1", G, T, desc, src, "Hệ số αm", "", hc.AlphaM, fr.AlphaM, 0.5),
            C("B01-2", G, T, desc, src, "Chiều cao vùng nén tương đối ξ", "", hc.Xi, fr.Xi, 0.5),
            C("B01-3", G, T, desc, src, "Diện tích cốt thép As", "cm²", hc.As * 1e4, fr.As * 1e4, 1.0,
              "Chương trình tính As = Rb·b·ξ·h0/Rs (con đường ξ); tham chiếu dùng con đường ζ – hai cách tương đương về đại số."),
            C("B01-4", G, T, desc, "TCVN 5574:2018 mục 8.1.2.2: ξR = 0,8/(1 + Rs/(Es·εb2)), εb2 = 0,0035", "ξR (CB400-V)", "", hc.XiR, fr.XiR, 0.1),
            C("B01-5", G, T, desc, "TCVN 5574:2018 mục 8.1.2.2: αR = ξR·(1 − ξR/2)", "αR (CB400-V)", "", hc.AlphaR, fr.AlphaR, 0.1),
            C("B01-6", G, T, desc, "TCVN 5574:2018 mục 8.1.2.2: ξR với Rs = 260 MPa", "ξR (CB300-V)", "", HandCalc.XiR(RsCB300, EsSteel), fr300.XiR, 0.1)
        };
    }

    // ---- B02 ----
    private static List<BenchmarkCase> FlexureDoublyCase()
    {
        const string G = GroupConcrete, T = "Dầm chữ nhật cốt kép – tính As, As'";
        double M = 300, b = 0.22, h = 0.50, a = 0.04, ac = 0.035, h0 = h - a;
        var hc = HandCalc.FlexureDoubly(M, b, h0, ac, RbB25, RsCB400, RscCB400, EsSteel);
        var fr = RcSection.DesignRect(M, b, h0, ac, B25(), CB400(), 0);
        string desc = $"Tiết diện {b * 1000:0}×{h * 1000:0} mm, h0 = {h0 * 1000:0} mm, a' = {ac * 1000:0} mm, M = {M} kN.m, B25, CB400-V – αm > αR nên cần cốt kép.";
        string src = "TCVN 5574:2018 mục 8.1.2.2 – tính tay: x = ξR·h0; As' = [M − Rb·b·x(h0 − x/2)]/[Rsc(h0 − a')]; As = (Rb·b·x + Rsc·As')/Rs";
        return new List<BenchmarkCase>
        {
            C("B02-1", G, T, desc, src, "Cốt chịu nén As'", "cm²", hc.AsComp * 1e4, fr.AsComp * 1e4, 1.0, fr.Doubly ? "" : "CẢNH BÁO: chương trình không nhận ra trường hợp cốt kép."),
            C("B02-2", G, T, desc, src, "Cốt chịu kéo As", "cm²", hc.As * 1e4, fr.As * 1e4, 1.0)
        };
    }

    // ---- B03 ----
    private static List<BenchmarkCase> MomentCapacityCase()
    {
        const string G = GroupConcrete, T = "Khả năng chịu mô men tiết diện đã bố trí thép";
        double b = 0.22, h0 = 0.46, As = 3 * Bar(20);
        double hc = HandCalc.MomentCapacitySingly(As, b, h0, RbB25, RsCB400, EsSteel);
        double mu = RcSection.MomentCapacity(b, h0, As, 0, 0.035, B25(), CB400());
        string desc = $"Tiết diện 220×500 mm, h0 = 460 mm, thép chịu kéo 3Ø20 (As = {BenchmarkCase.Fmt(As * 1e4, "0.00")} cm²), không kể cốt chịu nén, B25, CB400-V.";
        return new List<BenchmarkCase>
        {
            C("B03-1", G, T, desc, "TCVN 5574:2018 mục 8.1.2.2: x = Rs·As/(Rb·b) ≤ ξR·h0; Mu = Rb·b·x·(h0 − x/2)", "Mu", "kN.m", hc, mu, 0.5)
        };
    }

    // ---- B04 ----
    private static List<BenchmarkCase> ShearCase()
    {
        const string G = GroupConcrete, T = "Chịu cắt: bê tông + cốt đai (tiết diện nghiêng c = 2h0)";
        double b = 0.22, h0 = 0.46, s = 0.15, Q = 150;
        double asw = 2 * Bar(8);
        var hc = HandCalc.Shear(b, h0, 2 * h0, RbB25, RbtB25, RswCB240, asw, s);
        var cb240 = Material.Rebar("CB240-T");
        double qu = RcSection.ShearCapacity(b, h0, B25(), cb240, 8, 2, s);
        double qb = RcSection.QbConcrete(b, h0, B25());
        var sh = RcSection.Shear(Q, b, h0, B25(), cb240, 8, 2);
        // bước đai yêu cầu (tính tay): Q = Qb + 0,75·qsw·2h0 → qsw = (Q − Qb)/(1,5h0) ≥ 0,25Rbt·b; s = Rsw·Asw/qsw
        double qswReq = Math.Max((Q - hc.Qb) / (1.5 * h0), 0.25 * RbtB25 * b);
        double sReq = RswCB240 * asw / qswReq;
        // kiểm tra đầy đủ (lực cắt không đổi trên đoạn dầm) – c chạy trong [h0; 3h0]: thấp nhất tại c = 3h0
        double quMin = double.MaxValue;
        for (int i = 0; i <= 200; i++)
        {
            double c = h0 + 2 * h0 * i / 200.0;
            quMin = Math.Min(quMin, HandCalc.Shear(b, h0, c, RbB25, RbtB25, RswCB240, asw, s).Qu);
        }
        string desc = $"Dầm 220×500, h0 = 460 mm, B25 (Rbt = 1,05 MPa), đai Ø8 hai nhánh CB240-T (Rsw = 170 MPa) bước {s * 1000:0} mm; lực cắt Q = {Q} kN để tính bước đai yêu cầu.";
        string src = "TCVN 5574:2018 mục 8.1.3: Qb = 1,5·Rbt·b·h0²/c, Qsw = 0,75·qsw·c0, qsw = Rsw·Asw/s; c = c0 = 2h0 (giả thiết của chương trình)";
        string lim = $"Lưu ý: chương trình lấy tiết diện nghiêng c = c0 = 2h0. Khi lực cắt không đổi trên đoạn dài (lực tập trung xa gối), kiểm tra đầy đủ xét c trong khoảng h0…3h0 cho "
                     + $"Qu,min = {BenchmarkCase.Fmt(quMin, "0.0")} kN (tại c ≈ 3h0) – thấp hơn {BenchmarkCase.Fmt((hc.Qu - quMin) / hc.Qu * 100, "0.0")}% so với c = 2h0. Xem mục Giới hạn đã biết.";
        return new List<BenchmarkCase>
        {
            C("B04-1", G, T, desc, src, "Qb (bê tông)", "kN", hc.Qb, qb, 0.5),
            C("B04-2", G, T, desc, src, "Qu = Qb + Qsw (bước 150)", "kN", hc.Qu, qu, 0.5, lim),
            C("B04-3", G, T, desc, "TCVN 5574:2018 mục 8.1.3: dải bê tông giữa các vết nứt nghiêng 0,3·Rb·b·h0", "Qstrut", "kN", hc.Qstrut, sh.QStrut, 0.5),
            C("B04-4", G, T, desc, "Tính tay: qsw = max((Q − Qb)/(1,5h0); 0,25Rbt·b), s = Rsw·Asw/qsw", "Bước đai yêu cầu (Q = 150 kN)", "mm", sReq * 1000, sh.SReq * 1000, 0.5)
        };
    }

    // ---- B05 ----
    private static List<BenchmarkCase> ColumnCase()
    {
        const string G = GroupConcrete, T = "Cột nén lệch tâm cốt đối xứng (lệch tâm lớn)";
        double N = 400, b = 0.30, h = 0.40, a = 0.04, As1 = 3 * Bar(20);
        var hc = HandCalc.ColumnSymmetric(N, b, h, a, As1, RbB25, RscCB400);
        var bars = new List<(double D, double A)> { (a, As1), (h - a, As1) };
        double mu = RcSection.UniaxialCapacity(N, b, h, bars, B25(), CB400());
        string desc = $"Cột 300×400 mm (uốn theo cạnh 400), a = a' = 40 mm, mỗi phía 3Ø20 (As = As' = {BenchmarkCase.Fmt(As1 * 1e4, "0.00")} cm²), B25, CB400-V, lực nén N = {N} kN. "
                      + $"x = N/(Rb·b) = {BenchmarkCase.Fmt(hc.X * 1000, "0.0")} mm (ξ = {BenchmarkCase.Fmt(hc.Xi, "0.000")} < ξR; x > 2a' = 80 mm → lệch tâm lớn, cốt nén chảy).";
        return new List<BenchmarkCase>
        {
            C("B05-1", G, T, desc, "TCVN 5574:2018 (nén lệch tâm, khối ứng suất chữ nhật): N·e ≤ Rb·b·x(h0 − x/2) + Rsc·As'(h0 − a'), x = N/(Rb·b)",
              "Mu tại N = 400 kN", "kN.m", hc.Mu, mu, 2.0,
              "Chương trình dùng mô hình tương thích biến dạng (khối 0,8x, εb2 = 0,0035, trừ phần bê tông bị thép chiếm chỗ) – sai khác so với khối chữ nhật cổ điển ≈ 0,5%.")
        };
    }

    // ---- B06 ----
    private static List<BenchmarkCase> EtaCase()
    {
        const string G = GroupConcrete, T = "Hệ số uốn dọc η của cột mảnh";
        double N = 1200, M = 60, l0 = 4.2, b = 0.30, h = 0.30, a = 0.04;
        double As = 4 * Bar(20), Is = As * Math.Pow(h / 2 - a, 2);
        var hc = HandCalc.Eta(N, M, l0, b, h, Is, EbB25, EsSteel, 2.0);
        double I = b * h * h * h / 12, lambda = l0 / Math.Sqrt(I / (b * h));
        double eta = RcSection.Eta(N, M, l0, h, I, Is, lambda, B25(), CB400(), 2.0, out double ncr);
        string desc = $"Cột 300×300 mm, l0 = {l0} m (λ = l0/i = {BenchmarkCase.Fmt(lambda, "0.0")} > 14), 4Ø20 ở góc (a = 40 mm), N = {N} kN, M = {M} kN.m, φL = 2,0 (chưa tách tải dài hạn), B25, CB400-V.";
        string src = "TCVN 5574:2018 (uốn dọc): Ncr = π²D/l0², D = kb·Eb·I + 0,7·Es·Is, kb = 0,15/(φL(0,3 + δe)); η = 1/(1 − N/Ncr)";
        return new List<BenchmarkCase>
        {
            C("B06-1", G, T, desc, src, "Lực tới hạn quy ước Ncr", "kN", hc.Ncr, ncr, 0.5),
            C("B06-2", G, T, desc, src, "Hệ số η", "", hc.Eta, eta, 0.5)
        };
    }

    // ---- B07 ----
    private static List<BenchmarkCase> StiffnessCase()
    {
        const string G = GroupConcrete, T = "Độ cứng chống uốn có kể nứt và từ biến";
        double b = 0.22, h = 0.50, h0 = 0.46, As = 3 * Bar(18), Ms = 80;
        double hc = HandCalc.CrackedStiffness(b, h, h0, As, EbB25, RbtB25, EsSteel, Ms);
        double B = RcSection.FlexuralStiffness(b, h, h0, As, B25(), EsSteel, Ms);
        double L = 6.0, f = 5.0 / 48 * Ms * L * L / hc;
        string desc = $"Dầm 220×500 mm, h0 = 460 mm, 3Ø18 (As = {BenchmarkCase.Fmt(As * 1e4, "0.00")} cm²), B25, mô men tiêu chuẩn Ms = {Ms} kN.m (> Mcr – tiết diện nứt).";
        return new List<BenchmarkCase>
        {
            C("B07-1", G, T, desc, "Công thức chương trình công bố (gần đúng TCVN 5574:2018 mục 8.2): Eb,red = Eb/(1 + 2,5), Ieff Bischoff, Mcr = 1,3Rbt·b·h²/6 – viết lại độc lập",
              "Độ cứng B = Eb,red·Ieff", "kN.m²", hc, B, 0.5,
              $"Ví dụ: độ võng dầm đơn giản nhịp {L} m với Mmax = Ms: f = 5/48·Ms·L²/B = {BenchmarkCase.Fmt(f * 1000, "0.0")} mm. "
              + "Đây là kiểm chứng việc lập trình đúng công thức đã công bố, không thay thế so sánh với phương pháp đầy đủ của tiêu chuẩn (cột tài liệu).")
        };
    }

    // ---- B08 ----
    private static List<BenchmarkCase> OneWaySlab()
    {
        const string G = GroupConcrete, T = "Sàn bản dầm (một phương) – mô men, khả năng chịu lực, độ võng";
        double lx = 2.4, ly = 6.0, t = 0.12, gFinish = 1.2, qLive = 1.5;
        var p = new Project { Name = "Kiểm chứng sàn" };
        p.Materials.Add(Material.Concrete("B25"));
        p.Materials.Add(Material.Rebar("CB240-T")); p.Materials.Add(Material.Rebar("CB400-V"));
        p.SlabSections.Add(new SlabSection { Name = "S120", Thickness = t, Material = "B25" });
        p.Patterns.Add(new LoadPattern { Name = "TT", Type = LoadType.Dead, SelfWeight = 1, Gamma = 1.1 });
        p.Patterns.Add(new LoadPattern { Name = "HTHT", Type = LoadType.SuperDead, Gamma = 1.3 });
        p.Patterns.Add(new LoadPattern { Name = "HT", Type = LoadType.Live, Gamma = 1.3 });
        p.Stories.Add(new Story { Name = "T2", Elevation = 3.0, Height = 3.0 });
        int id = 1;
        foreach (var (x, y) in new[] { (0.0, 0.0), (lx, 0.0), (lx, ly), (0.0, ly) })
            p.Nodes.Add(new Node { Id = id++, X = x, Y = y, Z = 3.0, Story = "T2" });
        p.Slabs.Add(new Slab { Id = 1, Label = "S1", Nodes = { 1, 2, 3, 4 }, Section = "S120", Story = "T2", EdgeFixed = { false, false, false, false } });
        p.AreaLoads.Add(new AreaLoad { Slab = 1, Pattern = "HTHT", Q = gFinish });
        p.AreaLoads.Add(new AreaLoad { Slab = 1, Pattern = "HT", Q = qLive });
        var warn = new List<string>();
        var d = SlabDesigner.Design(p, null, warn).FirstOrDefault()
                ?? throw new InvalidOperationException("Không thiết kế được ô sàn: " + string.Join("; ", warn));

        // tính tay
        double g0 = 25.0 * t;
        double q = 1.1 * g0 + 1.3 * gFinish + 1.3 * qLive, qs = g0 + gFinish + qLive;
        double M = q * lx * lx / 8;
        var m = Regex.Match(d.BotX ?? "", @"Ø(\d+)a(\d+)");
        if (!m.Success) throw new InvalidOperationException("Không đọc được thép lớp dưới phương ngắn: '" + d.BotX + "'");
        int dia = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        double sp = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) / 1000;
        double asPerM = Bar(dia) / sp;
        double cover = p.Concrete.CoverSlab, h0 = t - cover - dia / 2000.0;
        double rs = dia < 10 ? 210e3 : RsCB400;   // Ø < 10 dùng CB240-T (Rs = 210 MPa), Ø ≥ 10 dùng CB400-V
        double mu = HandCalc.MomentCapacitySingly(asPerM, 1.0, h0, RbB25, rs, EsSteel);
        double ms = qs * lx * lx / 8;
        double B = HandCalc.CrackedStiffness(1.0, t, h0, asPerM, EbB25, RbtB25, EsSteel, ms);
        double f = 5.0 / 384 * qs * Math.Pow(lx, 4) / B;
        var chk = d.Checks.FirstOrDefault(c => c.Name == "Uốn nhịp phương ngắn");
        string desc = $"Ô sàn {lx}×{ly} m (ly/lx = {ly / lx:0.0} > 2 – bản dầm), dày {t * 1000:0} mm, 4 cạnh khớp; tĩnh tải bản thân 25·t (γf 1,1), hoàn thiện {gFinish} (γf 1,3), "
                      + $"hoạt tải {qLive} kN/m² (γf 1,3); lớp bảo vệ {cover * 1000:0} mm. Thép chương trình chọn: {d.BotX}.";
        return new List<BenchmarkCase>
        {
            C("B08-1", G, T, desc, "Tải tính toán q = Σγf·qi (hệ số γf mặc định của chương trình)", "q tính toán", "kN/m²", q, d.Q, 0.1),
            C("B08-2", G, T, desc, "Dải 1 m khớp hai đầu: M = q·lx²/8", "M1 nhịp phương ngắn", "kN.m/m", M, d.M1, 0.1),
            C("B08-3", G, T, desc, "TCVN 5574:2018 mục 8.1.2.2: Mu = Rs·As(h0 − x/2), x = Rs·As/(Rb·b), b = 1 m", $"Mu dải 1 m với {d.BotX}", "kN.m/m",
              mu, chk?.Capacity ?? double.NaN, 0.5),
            C("B08-4", G, T, desc, "f = 5/384·qs·lx⁴/B, B theo B07 (tải tiêu chuẩn qs = Σqi)", "Độ võng giữa ô", "mm", f * 1000, d.Deflection * 1000, 1.0)
        };
    }
}
