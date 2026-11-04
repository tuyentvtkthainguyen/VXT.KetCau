using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// BÊ TÔNG DỰ ỨNG LỰC CĂNG SAU – dầm đơn giản hoặc dải sàn rộng 1 m, cáp parabol.
// Quy trình rút gọn theo TCVN 5574:2018 (mục 9 – kết cấu ứng lực trước; 8.1 – độ bền; 8.2 – nứt, võng):
// đặc trưng tiết diện → hao tổn ứng suất (thứ nhất: chùng ứng suất, ma sát, biến dạng neo; thứ hai: co ngót, từ biến;
// tổng ≥ 100 MPa) → ứng suất khi buông neo và khi sử dụng → khả năng chịu uốn, cắt → độ võng (kể độ vồng).
// Quy ước: ứng suất NÉN DƯƠNG; độ lệch tâm cáp e dương = cáp nằm DƯỚI trọng tâm.
// =====================================================================

/// <summary>Số liệu cấu kiện dự ứng lực căng sau (chế độ triển khai hồ sơ).</summary>
public class PrestressInput : IDetailMember
{
    [Field("Tên cấu kiện", Group = "Chung")] public string Name { get; set; } = "DUL1";
    [Field("Số lượng", Unit = "ck", Group = "Chung")] public int Quantity { get; set; } = 1;
    [Field("Loại cấu kiện", Group = "Chung", Choices = "Dầm|Sàn (dải 1 m)")] public string MemberType { get; set; } = "Dầm";
    [Field("Cấp bê tông (trống = theo hồ sơ)", Group = "Chung", Choices = "|B25|B30|B35|B40")] public string ConcreteGrade { get; set; } = "B35";
    [Field("Cường độ BT khi căng / cường độ thiết kế", Group = "Chung", Hint = "Thường 0,7 – 0,8")] public double TransferRatio { get; set; } = 0.7;

    [Field("Bề rộng sườn b (sàn: 1 m)", Unit = "mm", Scale = 1000, Group = "Tiết diện")] public double B { get; set; } = 0.30;
    [Field("Chiều cao h", Unit = "mm", Scale = 1000, Group = "Tiết diện")] public double H { get; set; } = 0.70;
    [Field("Bề rộng cánh bf (0 = chữ nhật)", Unit = "mm", Scale = 1000, Group = "Tiết diện")] public double Bf { get; set; }
    [Field("Chiều dày cánh hf", Unit = "mm", Scale = 1000, Group = "Tiết diện")] public double Hf { get; set; }
    [Field("Nhịp tính toán L (gối đơn giản)", Unit = "m", Group = "Tiết diện")] public double L { get; set; } = 12.0;

    [Field("Tĩnh tải thêm G2 (ngoài trọng lượng bản thân)", Unit = "kN/m", Group = "Tải trọng", Hint = "Sàn: kN/m² × 1 m")] public double G2 { get; set; } = 12.0;
    [Field("Hoạt tải tiêu chuẩn Qk", Unit = "kN/m", Group = "Tải trọng")] public double Qk { get; set; } = 10.0;
    [Field("Hệ số tin cậy tĩnh tải γg", Group = "Tải trọng")] public double GammaG { get; set; } = 1.1;
    [Field("Hệ số tin cậy hoạt tải γq", Group = "Tải trọng")] public double GammaQ { get; set; } = 1.3;
    [Field("Phần dài hạn của hoạt tải ψ2", Group = "Tải trọng")] public double Psi2 { get; set; } = 0.3;

    [Field("Loại cáp", Group = "Cáp DƯL", Choices = "T12.7|T15.2")] public string StrandType { get; set; } = "T15.2";
    [Field("Cường độ cực hạn fpk", Unit = "MPa", Group = "Cáp DƯL")] public double Fpk { get; set; } = 1860;
    [Field("Số tao cáp", Unit = "tao", Group = "Cáp DƯL")] public int NumStrands { get; set; } = 8;
    [Field("Số tao / bó (neo)", Unit = "tao", Group = "Cáp DƯL")] public int StrandsPerTendon { get; set; } = 4;
    [Field("Cáp có bám dính (ống gen + bơm vữa)", Group = "Cáp DƯL")] public bool Bonded { get; set; } = true;
    [Field("Độ lệch tâm giữa nhịp ec (dưới trọng tâm +)", Unit = "mm", Scale = 1000, Group = "Cáp DƯL")] public double EcMid { get; set; } = 0.22;
    [Field("Độ lệch tâm tại đầu e0", Unit = "mm", Scale = 1000, Group = "Cáp DƯL")] public double EEnd { get; set; }
    [Field("Ứng suất kéo căng σcon / fpk", Group = "Cáp DƯL")] public double JackRatio { get; set; } = 0.75;
    [Field("Hệ số ma sát μ", Group = "Cáp DƯL")] public double Mu { get; set; } = 0.20;
    [Field("Hệ số lắc ống k", Unit = "1/m", Group = "Cáp DƯL")] public double Wobble { get; set; } = 0.0010;
    [Field("Tụt neo Δ", Unit = "mm", Scale = 1000, Group = "Cáp DƯL")] public double AnchorSet { get; set; } = 0.006;
    [Field("Kéo căng hai đầu", Group = "Cáp DƯL")] public bool TwoEnds { get; set; }

    [Field("Thép thường dưới – số thanh (sàn: /1 m)", Group = "Thép thường")] public int BotCount { get; set; } = 2;
    [Field("Thép thường dưới – Ø", Unit = "mm", Group = "Thép thường")] public int BotDia { get; set; } = 16;
    [Field("Thép thường trên – số thanh", Group = "Thép thường")] public int TopCount { get; set; } = 2;
    [Field("Thép thường trên – Ø", Unit = "mm", Group = "Thép thường")] public int TopDia { get; set; } = 14;
    [Field("Đai – Ø (0 = không có, sàn)", Unit = "mm", Group = "Thép thường")] public int StirrupDia { get; set; } = 8;
    [Field("Đai – bước", Unit = "mm", Scale = 1000, Group = "Thép thường")] public double StirrupSpacing { get; set; } = 0.20;
    [Field("Đai – số nhánh", Group = "Thép thường")] public int StirrupLegs { get; set; } = 2;

    public DetailResult Run(DetailSettings s) => PrestressDesigner.Design(this, s);
    public List<Figure> Draw(DetailResult r, DetailSettings s) => PrestressDesigner.Draw(this, r as PrestressResult, s);
}

/// <summary>Kết quả tính dự ứng lực (các đại lượng chính để kiểm thử / thuyết minh). Ứng suất MPa, lực kN, chiều dài m.</summary>
public class PrestressResult : DetailResult
{
    public PrestressSection Section { get; set; }
    public double Ap { get; set; }            // m²
    public double SigmaCon { get; set; }      // MPa
    public double LossRelax { get; set; }
    public double LossFriction { get; set; }
    public double LossAnchor { get; set; }
    public double LossShrink { get; set; }
    public double LossCreep { get; set; }
    public double LossFirst => LossRelax + LossFriction + LossAnchor;
    public double LossTotal { get; set; }
    public double P0 { get; set; }            // kN sau hao tổn thứ nhất
    public double PInf { get; set; }          // kN sau toàn bộ hao tổn
    public double WBalance { get; set; }      // kN/m
    public double Mg0 { get; set; }           // kN.m, bản thân
    public double Mser { get; set; }          // kN.m, tiêu chuẩn
    public double Mu { get; set; }            // kN.m tính toán (tải)
    public double MuCap { get; set; }         // kN.m khả năng
    public double Xi { get; set; }
    public double XiR { get; set; }
    // ứng suất (MPa, nén +): khi buông neo / sử dụng – giữa nhịp (Mid) và đầu (End), thớ trên (T) / dưới (B)
    public double TrMidT { get; set; }
    public double TrMidB { get; set; }
    public double TrEndT { get; set; }
    public double TrEndB { get; set; }
    public double SvMidT { get; set; }
    public double SvMidB { get; set; }
    public double SvEndT { get; set; }
    public double SvEndB { get; set; }
    public double Camber { get; set; }        // m (vồng lên, dương)
    public double Deflection { get; set; }    // m (võng xuống dương, dài hạn, đã trừ vồng)
    public double DeflectionLimit { get; set; }
    public string CrackCategory { get; set; } = "";
}

/// <summary>Đặc trưng hình học tiết diện chữ nhật / chữ T (cánh trên).</summary>
public class PrestressSection
{
    public double A { get; set; }
    public double I { get; set; }
    /// <summary>Khoảng cách trọng tâm tới thớ trên / dưới (m)</summary>
    public double Yt { get; set; }
    public double Yb { get; set; }
    public double Wt => Yt > 0 ? I / Yt : 0;
    public double Wb => Yb > 0 ? I / Yb : 0;

    /// <summary>Tiết diện chữ nhật b×h hoặc chữ T: sườn b×h, cánh trên bf×hf (bf &gt; b, hf &gt; 0).</summary>
    public static PrestressSection Of(double b, double h, double bf = 0, double hf = 0)
    {
        bool tee = bf > b + 1e-9 && hf > 1e-9 && hf < h;
        double ow = tee ? bf - b : 0, oh = tee ? hf : 0;
        double a1 = b * h, a2 = ow * oh;
        double A = a1 + a2;
        double yt = A > 0 ? (a1 * h / 2 + a2 * oh / 2) / A : 0;
        double I = b * h * h * h / 12 + a1 * Math.Pow(h / 2 - yt, 2) + ow * oh * oh * oh / 12 + a2 * Math.Pow(yt - oh / 2, 2);
        return new PrestressSection { A = A, I = I, Yt = yt, Yb = h - yt };
    }
}

public static class PrestressDesigner
{
    /// <summary>Môđun đàn hồi cáp (MPa)</summary>
    public const double Ep = 195000;
    public const double SteelDensity = 7850;   // kg/m³
    /// <summary>Tổng hao tổn tối thiểu (MPa) – TCVN 5574:2018 mục 9.1.</summary>
    public const double MinTotalLoss = 100;
    public const double WorkLength = 0.6;      // m mỗi đầu kéo căng (thống kê cáp)

    public static double StrandArea(string type) => type == "T12.7" ? 98.7e-6 : 140e-6;

    /// <summary>Rbt,n (MPa) – TCVN 5574:2018 Bảng 6.</summary>
    public static double Rbtn(string grade) => grade switch
    {
        "B15" => 1.10, "B20" => 1.35, "B25" => 1.55, "B30" => 1.75, "B35" => 1.95, "B40" => 2.10, _ => 1.55
    };

    /// <summary>Hệ số từ biến φb,cr (độ ẩm môi trường 40–75 %) – TCVN 5574:2018 Bảng 11 (rút gọn).</summary>
    public static double PhiCreep(string grade) => grade switch
    {
        "B15" => 3.4, "B20" => 2.8, "B25" => 2.5, "B30" => 2.3, "B35" => 2.1, "B40" => 1.9, _ => 2.5
    };

    /// <summary>Biến dạng co ngót εb,sh: ≤ B35 0,0002; B40 0,00025; ≥ B45 0,0003.</summary>
    public static double ShrinkStrain(string grade)
    {
        int n = int.TryParse(grade?.TrimStart('B', 'b'), out int v) ? v : 25;
        return n <= 35 ? 0.0002 : n <= 40 ? 0.00025 : 0.0003;
    }

    /// <summary>Hao tổn do chùng ứng suất cáp/sợi kéo cơ học: Δσ = (0,22·σsp/Rs,n − 0,1)·σsp ≥ 0 (MPa).</summary>
    public static double RelaxationLoss(double sigmaSp, double rsn) => Math.Max((0.22 * sigmaSp / rsn - 0.1) * sigmaSp, 0);

    /// <summary>Góc chuyển hướng tích luỹ của cáp parabol độ võng f từ đầu tới tiết diện x (x ≤ L/2): θ = 8·f·x/L².</summary>
    public static double ThetaParabola(double sag, double L, double x) => L > 0 ? 8 * Math.Abs(sag) * x / (L * L) : 0;

    /// <summary>Hao tổn ma sát: Δσ = σcon·(1 − e^−(μθ + kx)).</summary>
    public static double FrictionLoss(double sigmaCon, double mu, double theta, double k, double x) => sigmaCon * (1 - Math.Exp(-(mu * theta + k * x)));

    /// <summary>Tải cân bằng của cáp parabol: w = 8·P·f/L².</summary>
    public static double BalancedLoad(double P, double sag, double L) => L > 0 ? 8 * P * sag / (L * L) : 0;

    /// <summary>Độ vồng giữa nhịp do cáp parabol (độ võng f) + lệch tâm đầu e0: Δ = 5·P·f·L²/(48EI) + P·e0·L²/(8EI).</summary>
    public static double CamberOf(double P, double sag, double eEnd, double L, double EI) =>
        EI > 0 ? 5 * P * sag * L * L / (48 * EI) + P * eEnd * L * L / (8 * EI) : 0;

    /// <summary>Ứng suất thớ trên / dưới (nén +, kN/m²): σt = P/A − P·e/Wt + M/Wt; σb = P/A + P·e/Wb − M/Wb.</summary>
    public static (double Top, double Bot) Stresses(PrestressSection s, double P, double e, double M) =>
        (P / s.A - P * e / s.Wt + M / s.Wt, P / s.A + P * e / s.Wb - M / s.Wb);

    private static string F(double v, string f = "0.##") => Dt.F(v, f);

    public static PrestressResult Design(PrestressInput inp, DetailSettings st)
    {
        st ??= new DetailSettings();
        var r = new PrestressResult { Name = inp.Name ?? "DUL", Quantity = Math.Max(1, inp.Quantity), Kind = "Dự ứng lực" };
        try { Compute(inp, st, r); }
        catch (Exception ex) { r.Messages.Add("LỖI tính dự ứng lực: " + ex.Message); }
        return r;
    }

    private static void Compute(PrestressInput inp, DetailSettings st, PrestressResult r)
    {
        bool slab = (inp.MemberType ?? "").StartsWith("Sàn");
        double b = slab ? 1.0 : inp.B, h = inp.H, L = inp.L;
        string grade = string.IsNullOrWhiteSpace(inp.ConcreteGrade) ? st.Concrete : inp.ConcreteGrade.Trim();
        Material conc;
        try { conc = Material.Concrete(grade); }
        catch (ArgumentException) { r.Messages.Add("LỖI cấp bê tông " + grade + " không có trong thư viện – dùng " + st.Concrete); grade = st.Concrete; conc = Material.Concrete(grade); }
        r.ConcreteClass = grade;
        if (b <= 0 || h <= 0 || L <= 0) { r.Messages.Add("LỖI kích thước tiết diện / nhịp không hợp lệ"); return; }
        var main = st.Main();
        var small = st.Small();
        double cover = st.CoverBeam;
        var c = r.Calc;

        // ---- 1. Tiết diện ----
        var sec = PrestressSection.Of(b, h, slab ? 0 : inp.Bf, slab ? 0 : inp.Hf);
        bool tee = !slab && inp.Bf > b + 1e-9 && inp.Hf > 1e-9;
        r.Section = sec;
        c.Add("## 1. Đặc trưng tiết diện");
        c.Add((tee ? $"Tiết diện chữ T: sườn b × h = {Dt.Mm(b)} × {Dt.Mm(h)} mm, cánh bf × hf = {Dt.Mm(inp.Bf)} × {Dt.Mm(inp.Hf)} mm"
                   : slab ? $"Dải sàn rộng 1 m, chiều dày h = {Dt.Mm(h)} mm" : $"Tiết diện chữ nhật b × h = {Dt.Mm(b)} × {Dt.Mm(h)} mm") + $", nhịp L = {F(L)} m (gối đơn giản).");
        c.Add($"A = {F(sec.A, "0.0000")} m²; yt = {Dt.Mm(sec.Yt)} mm; yb = {Dt.Mm(sec.Yb)} mm; I = {F(sec.I * 1e4, "0.0000")}·10⁻⁴ m⁴; Wt = I/yt = {F(sec.Wt * 1e3, "0.000")}·10⁻³ m³; Wb = I/yb = {F(sec.Wb * 1e3, "0.000")}·10⁻³ m³.");
        c.Add($"Bê tông {grade}: Rb = {F(conc.Rb / U.MPa, "0.0")} MPa, Rbt = {F(conc.Rbt / U.MPa, "0.00")} MPa, Rb,n = {F(conc.Rbn / U.MPa, "0.0")} MPa, Rbt,n = {F(Rbtn(grade), "0.00")} MPa, Eb = {F(conc.E / U.MPa, "0")} MPa.");

        // ---- 2. Tải trọng, nội lực ----
        double g0 = 25.0 * sec.A, gk = g0 + inp.G2, qk = inp.Qk;
        double wser = gk + qk, wu = inp.GammaG * gk + inp.GammaQ * qk;
        r.Mg0 = g0 * L * L / 8; r.Mser = wser * L * L / 8; r.Mu = wu * L * L / 8;
        double Qu = wu * L / 2;
        c.Add("## 2. Tải trọng và nội lực");
        c.Add($"Trọng lượng bản thân g0 = 25·A = {F(g0, "0.00")} kN/m; tĩnh tải tiêu chuẩn gk = g0 + G2 = {F(gk, "0.00")} kN/m; hoạt tải qk = {F(qk, "0.00")} kN/m.");
        c.Add($"Mg0 = g0·L²/8 = {F(r.Mg0, "0.0")} kN.m; Mtc = (gk + qk)·L²/8 = {F(r.Mser, "0.0")} kN.m; M = ({F(inp.GammaG)}·gk + {F(inp.GammaQ)}·qk)·L²/8 = {F(r.Mu, "0.0")} kN.m; Q = w·L/2 = {F(Qu, "0.0")} kN.");

        // ---- 3. Cáp, hao tổn ----
        double ap1 = StrandArea(inp.StrandType);
        int n = Math.Max(0, inp.NumStrands);
        r.Ap = n * ap1;
        double fpk = inp.Fpk > 0 ? inp.Fpk : 1860, rsn = fpk;
        r.SigmaCon = inp.JackRatio * fpk;
        double sag = inp.EcMid - inp.EEnd;
        c.Add("## 3. Cáp dự ứng lực và hao tổn ứng suất (TCVN 5574:2018 mục 9.1)");
        c.Add($"Cáp {inp.StrandType} (Ap1 = {F(ap1 * 1e6, "0.0")} mm²) × {n} tao → Ap = {F(r.Ap * 1e6, "0")} mm²; fpk = Rs,n = {F(fpk, "0")} MPa; Ep = {F(Ep, "0")} MPa; σcon = {F(inp.JackRatio)}·fpk = {F(r.SigmaCon, "0")} MPa; "
              + (inp.Bonded ? "cáp có bám dính." : "cáp không bám dính."));
        c.Add($"Quỹ đạo parabol: e(x) = e0 + 4f·x·(L − x)/L², e0 = {Dt.Mm(inp.EEnd)} mm, ec = {Dt.Mm(inp.EcMid)} mm, độ võng cáp f = ec − e0 = {Dt.Mm(sag)} mm.");
        if (n == 0)
        {
            r.Messages.Add("LỖI chưa khai báo số tao cáp (Số tao cáp = 0) – nhập số tao cáp để tính dự ứng lực");
            AddRebarAndQuantities(inp, st, r, slab, b, h, L, conc, main, small, cover, 0);
            return;
        }
        if (inp.JackRatio > 0.8 + 1e-9) r.Messages.Add("LỖI σcon = " + F(inp.JackRatio) + "·fpk > 0,8·fpk – giảm ứng suất kéo căng");
        if (inp.EcMid + 0.05 > sec.Yb) r.Messages.Add("LỖI cáp quá sát mép dưới: ec = " + Dt.Mm(inp.EcMid) + " mm > yb − 50 = " + Dt.Mm(sec.Yb - 0.05) + " mm");

        r.LossRelax = RelaxationLoss(r.SigmaCon, rsn);
        double x = L / 2;
        double theta = ThetaParabola(sag, L, x);
        r.LossFriction = FrictionLoss(r.SigmaCon, inp.Mu, theta, inp.Wobble, x);
        int ends = inp.TwoEnds ? 2 : 1;
        r.LossAnchor = ends * inp.AnchorSet * Ep / L;
        c.Add($"Chùng ứng suất: Δσ1 = (0,22·σcon/Rs,n − 0,1)·σcon = (0,22·{F(r.SigmaCon, "0")}/{F(rsn, "0")} − 0,1)·{F(r.SigmaCon, "0")} = {F(r.LossRelax, "0.0")} MPa.");
        c.Add($"Ma sát tới giữa nhịp (x = L/2 = {F(x)} m): θ = 8f·x/L² = {F(theta, "0.0000")} rad; Δσ2 = σcon·(1 − e^−(μθ + kx)) = {F(r.SigmaCon, "0")}·(1 − e^−({F(inp.Mu)}·{F(theta, "0.0000")} + {F(inp.Wobble, "0.0000")}·{F(x)})) = {F(r.LossFriction, "0.0")} MPa.");
        c.Add($"Biến dạng neo (tụt neo Δ = {Dt.Mm(inp.AnchorSet)} mm, kéo {(ends == 2 ? "hai" : "một")} đầu, rút gọn): Δσ3 = {ends}·Δ·Ep/L = {F(r.LossAnchor, "0.0")} MPa.");
        double s1 = r.SigmaCon - r.LossFirst;
        r.P0 = s1 * U.MPa * r.Ap;
        c.Add($"Hao tổn thứ nhất σlos1 = {F(r.LossFirst, "0.0")} MPa → σsp1 = {F(s1, "0.0")} MPa; P0 = σsp1·Ap = {F(r.P0, "0.0")} kN.");

        double alpha = Ep / (conc.E / U.MPa);
        double phi = PhiCreep(grade);
        double esh = ShrinkStrain(grade);
        r.LossShrink = esh * Ep;
        double e = inp.EcMid;
        double sbp = (r.P0 / sec.A + r.P0 * e * e / sec.I - r.Mg0 * e / sec.I) / U.MPa;   // MPa tại cao độ cáp
        double mu = r.Ap / sec.A;
        r.LossCreep = sbp > 0 ? 0.8 * alpha * phi * sbp / (1 + alpha * mu * (1 + e * e * sec.A / sec.I) * (1 + 0.8 * phi)) : 0;
        c.Add($"Co ngót: Δσ5 = εb,sh·Ep = {F(esh, "0.00000")}·{F(Ep, "0")} = {F(r.LossShrink, "0.0")} MPa.");
        c.Add($"Ứng suất bê tông tại cao độ cáp (P0 + bản thân): σbp = P0/A + P0·e²/I − Mg0·e/I = {F(sbp, "0.00")} MPa; α = Ep/Eb = {F(alpha, "0.00")}; μsp = Ap/A = {F(mu, "0.00000")}; φb,cr = {F(phi)}.");
        c.Add($"Từ biến: Δσ6 = 0,8·α·φb,cr·σbp / [1 + α·μsp·(1 + e²·A/I)·(1 + 0,8·φb,cr)] = {F(r.LossCreep, "0.0")} MPa.");
        double tot = r.LossFirst + r.LossShrink + r.LossCreep;
        r.LossTotal = Math.Max(tot, MinTotalLoss);
        c.Add($"Tổng hao tổn σlos = {F(tot, "0.0")} MPa" + (tot < MinTotalLoss ? $" < {F(MinTotalLoss, "0")} MPa → lấy {F(MinTotalLoss, "0")} MPa (giá trị tối thiểu)." : "."));
        double s2 = r.SigmaCon - r.LossTotal;
        r.PInf = s2 * U.MPa * r.Ap;
        c.Add($"σsp2 = σcon − σlos = {F(s2, "0.0")} MPa; P∞ = σsp2·Ap = {F(r.PInf, "0.0")} kN.");
        r.WBalance = BalancedLoad(r.PInf, sag, L);
        c.Add($"Tải cân bằng: wbal = 8·P∞·f/L² = 8·{F(r.PInf, "0.0")}·{F(sag, "0.000")}/{F(L)}² = {F(r.WBalance, "0.00")} kN/m (so với gk = {F(gk, "0.00")} kN/m – {F(gk > 0 ? r.WBalance / gk * 100 : 0, "0")} % tĩnh tải).");

        // ---- 4. Ứng suất ----
        double rbnp = inp.TransferRatio * conc.Rbn / U.MPa, rbtnp = inp.TransferRatio * Rbtn(grade);
        double rbn = conc.Rbn / U.MPa, rbtn = Rbtn(grade);
        (double t, double bt) tm = Stresses(sec, r.P0, inp.EcMid, r.Mg0), te = Stresses(sec, r.P0, inp.EEnd, 0);
        (double t, double bt) sm = Stresses(sec, r.PInf, inp.EcMid, r.Mser), se = Stresses(sec, r.PInf, inp.EEnd, 0);
        r.TrMidT = tm.t / U.MPa; r.TrMidB = tm.bt / U.MPa; r.TrEndT = te.t / U.MPa; r.TrEndB = te.bt / U.MPa;
        r.SvMidT = sm.t / U.MPa; r.SvMidB = sm.bt / U.MPa; r.SvEndT = se.t / U.MPa; r.SvEndB = se.bt / U.MPa;
        c.Add("## 4. Ứng suất pháp (nén dương): σt = P/A − P·e/Wt + M/Wt; σb = P/A + P·e/Wb − M/Wb");
        c.Add($"Giới hạn dùng: khi buông neo (bê tông đạt {F(inp.TransferRatio * 100, "0")} %) nén ≤ 0,6·Rb,n(p) = {F(0.6 * rbnp, "0.0")} MPa, kéo ≤ Rbt,n(p) = {F(rbtnp, "0.00")} MPa; "
              + $"khi sử dụng (tải tiêu chuẩn) nén ≤ 0,6·Rb,n = {F(0.6 * rbn, "0.0")} MPa, kéo ≤ Rbt,n = {F(rbtn, "0.00")} MPa (không cho phép nứt).");
        c.Add($"Buông neo – giữa nhịp: σt = {F(r.TrMidT, "0.00")}, σb = {F(r.TrMidB, "0.00")} MPa; đầu dầm: σt = {F(r.TrEndT, "0.00")}, σb = {F(r.TrEndB, "0.00")} MPa.");
        c.Add($"Sử dụng – giữa nhịp: σt = {F(r.SvMidT, "0.00")}, σb = {F(r.SvMidB, "0.00")} MPa; đầu dầm: σt = {F(r.SvEndT, "0.00")}, σb = {F(r.SvEndB, "0.00")} MPa.");
        double trMaxC = new[] { r.TrMidT, r.TrMidB, r.TrEndT, r.TrEndB }.Max(), trMaxT = -new[] { r.TrMidT, r.TrMidB, r.TrEndT, r.TrEndB }.Min();
        double svMaxC = new[] { r.SvMidT, r.SvMidB, r.SvEndT, r.SvEndB }.Max(), svMaxT = -new[] { r.SvMidT, r.SvMidB, r.SvEndT, r.SvEndB }.Min();
        r.Checks.Add(Dt.Check("Nén khi buông neo", trMaxC, 0.6 * rbnp, "MPa"));
        r.Checks.Add(Dt.Check("Kéo khi buông neo", Math.Max(trMaxT, 0), rbtnp, "MPa", "thớ trên giữa nhịp / đầu dầm"));
        r.Checks.Add(Dt.Check("Nén khi sử dụng", svMaxC, 0.6 * rbn, "MPa"));
        r.Checks.Add(Dt.Check("Kéo khi sử dụng (chống nứt)", Math.Max(svMaxT, 0), rbtn, "MPa"));
        r.CrackCategory = svMaxT <= 1e-9 ? "Toàn tiết diện chịu nén khi sử dụng – không xuất hiện ứng suất kéo"
            : svMaxT <= rbtn ? "Có ứng suất kéo ≤ Rbt,n – không hình thành vết nứt"
            : "Ứng suất kéo > Rbt,n – có hình thành vết nứt, cần kiểm tra bề rộng vết nứt hoặc tăng lực căng";
        c.Add("Phân loại chống nứt: " + r.CrackCategory + ".");
        if (trMaxT > rbtnp) r.Messages.Add("Cảnh báo: kéo thớ trên khi buông neo " + F(trMaxT, "0.00") + " MPa > " + F(rbtnp, "0.00") + " MPa – bố trí thép thường thớ trên hoặc giảm e0 / số tao căng đợt 1");
        if (svMaxT > rbtn) r.Messages.Add("LỖI ứng suất kéo khi sử dụng " + F(svMaxT, "0.00") + " MPa > Rbt,n – tăng số tao cáp, tăng độ lệch tâm hoặc tăng chiều cao h");
        if (svMaxC > 0.6 * rbn || trMaxC > 0.6 * rbnp) r.Messages.Add("LỖI ứng suất nén vượt giới hạn – tăng tiết diện hoặc cấp bê tông, giảm lực căng");

        // ---- 5. Độ bền uốn ----
        double rsp = fpk / 1.2;   // MPa
        double asB = (slab ? inp.BotCount : inp.BotCount) * Dt.Area(inp.BotDia), asT = inp.TopCount * Dt.Area(inp.TopDia);
        var mB = st.SteelFor(inp.BotDia); var mT = st.SteelFor(inp.TopDia);
        double dp = sec.Yt + inp.EcMid;
        double ds = h - cover - inp.BotDia / 2000.0;
        double aT = cover + inp.TopDia / 2000.0;
        double Tp = rsp * U.MPa * r.Ap, Ts = mB.Rs * asB;
        double T = Tp + Ts;
        double h0 = T > 0 ? (Tp * dp + Ts * ds) / T : dp;
        double epsel = (rsp + 400 - Math.Max(s2, 0)) / Ep;
        r.XiR = 0.8 / (1 + epsel / 0.0035);
        double rb = conc.Rb;
        double bc = tee ? inp.Bf : b;
        double Ccomp = T - mT.Rsc * asT;
        double xR = r.XiR * h0;
        double mu2;
        double xx;
        if (!tee || Ccomp <= rb * inp.Bf * inp.Hf)
        {
            xx = Math.Max(Ccomp / (rb * bc), 0);
            if (xx > xR) { xx = xR; r.Messages.Add("ξ > ξR – vùng nén lấy x = ξR·h0 (có thể tăng chiều cao hoặc thêm thép chịu nén)"); }
            mu2 = rb * bc * xx * (h0 - xx / 2) + mT.Rsc * asT * (h0 - aT);
        }
        else
        {
            xx = (Ccomp - rb * (inp.Bf - b) * inp.Hf) / (rb * b);
            if (xx > xR) { xx = xR; r.Messages.Add("ξ > ξR – vùng nén lấy x = ξR·h0"); }
            mu2 = rb * (inp.Bf - b) * inp.Hf * (h0 - inp.Hf / 2) + rb * b * xx * (h0 - xx / 2) + mT.Rsc * asT * (h0 - aT);
        }
        r.Xi = h0 > 0 ? xx / h0 : 0;
        r.MuCap = mu2;
        c.Add("## 5. Khả năng chịu uốn (TCVN 5574:2018 mục 8.1.2, ứng suất chữ nhật)");
        c.Add($"Cường độ tính toán cáp Rsp = fpk/1,2 = {F(rsp, "0")} MPa (γs = 1,2); dp = yt + ec = {Dt.Mm(dp)} mm; thép thường dưới {Dt.Bars(inp.BotCount, inp.BotDia)} (As = {F(asB * 1e6, "0")} mm², ds = {Dt.Mm(ds)} mm); trên {Dt.Bars(inp.TopCount, inp.TopDia)} (As' = {F(asT * 1e6, "0")} mm²).");
        c.Add($"h0 = (Rsp·Ap·dp + Rs·As·ds)/(Rsp·Ap + Rs·As) = {Dt.Mm(h0)} mm; εs,el = (Rsp + 400 − σsp2)/Ep = {F(epsel, "0.00000")}; ξR = 0,8/(1 + εs,el/0,0035) = {F(r.XiR, "0.000")}.");
        c.Add($"x = {Dt.Mm(xx)} mm (ξ = {F(r.Xi, "0.000")}); Mu = {F(r.MuCap, "0.0")} kN.m " + (r.MuCap >= r.Mu ? "≥" : "<") + $" M = {F(r.Mu, "0.0")} kN.m.");
        r.Checks.Add(Dt.Check("Mô men giữa nhịp", r.Mu, r.MuCap, "kN.m"));
        if (r.MuCap < r.Mu) r.Messages.Add("LỖI không đủ khả năng chịu uốn: Mu = " + F(r.MuCap, "0.0") + " < M = " + F(r.Mu, "0.0") + " kN.m – tăng số tao cáp hoặc chiều cao h");

        // ---- 6. Cắt ----
        double h0s = Math.Max(h0, 0.8 * h);
        double qcap;
        string qnote;
        if (!slab && inp.StirrupDia > 0 && inp.StirrupSpacing > 0)
        {
            qcap = RcSection.ShearCapacity(b, h0s, conc, small, inp.StirrupDia, Math.Max(1, inp.StirrupLegs), inp.StirrupSpacing);
            qnote = $"đai Ø{inp.StirrupDia}a{Dt.Mm(inp.StirrupSpacing)} {inp.StirrupLegs} nhánh";
        }
        else { qcap = RcSection.QbConcrete(b, h0s, conc); qnote = "bê tông chịu (không đai)"; }
        c.Add("## 6. Khả năng chịu cắt (TCVN 5574:2018 mục 8.1.3, bỏ qua có lợi của ứng lực trước – thiên về an toàn)");
        c.Add($"Q = {F(Qu, "0.0")} kN; Qu = Qb + Qsw = {F(qcap, "0.0")} kN ({qnote}, ≤ 0,3·Rb·b·h0 = {F(0.3 * rb * b * h0s, "0.0")} kN).");
        r.Checks.Add(Dt.Check("Lực cắt tại gối", Qu, qcap, "kN", qnote));
        if (Qu > qcap) r.Messages.Add("LỖI không đủ khả năng chịu cắt – giảm bước đai, tăng Ø đai hoặc bề rộng sườn");

        // ---- 7. Độ võng ----
        double EI = conc.E * sec.I;
        r.Camber = CamberOf(r.PInf, sag, inp.EEnd, L, EI);
        double wlong = gk + inp.Psi2 * qk, wshort = (1 - inp.Psi2) * qk;
        double dLong = 5 * wlong * Math.Pow(L, 4) / (384 * EI);
        double dShort = 5 * wshort * Math.Pow(L, 4) / (384 * EI);
        r.Deflection = (dLong - r.Camber) * (1 + phi) + dShort;
        r.DeflectionLimit = L / 250;
        c.Add("## 7. Độ võng (tiết diện không nứt, kể từ biến cho tải dài hạn và độ vồng)");
        c.Add($"EbI = {F(EI, "0")} kN.m²; độ vồng do cáp Δp = 5·P∞·f·L²/(48EI) + P∞·e0·L²/(8EI) = {F(r.Camber * 1000, "0.0")} mm.");
        c.Add($"Võng do tải dài hạn (gk + ψ2·qk) = {F(dLong * 1000, "0.0")} mm, ngắn hạn (1 − ψ2)·qk = {F(dShort * 1000, "0.0")} mm; f = (fdh − Δp)·(1 + φb,cr) + fnh = {F(r.Deflection * 1000, "0.0")} mm; [f] = L/250 = {F(r.DeflectionLimit * 1000, "0")} mm.");
        r.Checks.Add(Dt.Check("Độ võng", Math.Abs(r.Deflection), r.DeflectionLimit, "m", r.Deflection < 0 ? "vồng lên" : ""));
        if (Math.Abs(r.Deflection) > r.DeflectionLimit) r.Messages.Add("LỖI độ võng/vồng vượt L/250 – điều chỉnh lực căng (số tao) hoặc tăng chiều cao h");

        c.Add("## 8. Vùng neo");
        c.Add($"Vùng đặt neo cần lưới thép gián tiếp (lò xo xoắn + lưới Ø10–12a50…100) theo hồ sơ của hãng neo; chiều dài vùng truyền ứng suất ≈ h = {Dt.Mm(h)} mm; kiểm tra ép mặt cục bộ dưới bản đệm neo (TCVN 5574:2018 mục 8.1.5).");

        AddRebarAndQuantities(inp, st, r, slab, b, h, L, conc, main, small, cover, n);
    }

    private static void AddRebarAndQuantities(PrestressInput inp, DetailSettings st, PrestressResult r, bool slab, double b, double h, double L,
        Material conc, Material main, Material small, double cover, int n)
    {
        var sec = r.Section ?? PrestressSection.Of(b, h, slab ? 0 : inp.Bf, slab ? 0 : inp.Hf);
        r.Section = sec;
        double len = Math.Max(L - 2 * cover, 0.1);
        if (inp.BotCount > 0 && inp.BotDia > 0)
            r.Bars.Add(Dt.Hooked("Dưới", inp.BotDia, inp.BotCount, len, Math.Max(10 * inp.BotDia / 1000.0, 0.15), Math.Max(10 * inp.BotDia / 1000.0, 0.15)));
        if (inp.TopCount > 0 && inp.TopDia > 0)
            r.Bars.Add(Dt.Hooked("Trên", inp.TopDia, inp.TopCount, len, Math.Max(10 * inp.TopDia / 1000.0, 0.15), Math.Max(10 * inp.TopDia / 1000.0, 0.15), true));
        if (!slab && inp.StirrupDia > 0 && inp.StirrupSpacing > 0)
        {
            double cs = Math.Max(cover - inp.StirrupDia / 1000.0, 0.015);
            int cnt = (int)Math.Floor(len / inp.StirrupSpacing + 1e-6) + 1;
            r.Bars.Add(Dt.Stirrup("Đai", inp.StirrupDia, cnt, b - 2 * cs, h - 2 * cs, inp.StirrupSpacing));
        }
        RebarNumbering.Renumber(r.Bars);

        double ap1 = StrandArea(inp.StrandType);
        r.StrandWeight = n * ap1 * (L + 2 * WorkLength) * SteelDensity;
        int tendons = n == 0 ? 0 : (int)Math.Ceiling(n / (double)Math.Max(1, inp.StrandsPerTendon));
        if (n > 0)
        {
            r.Extra.Add(("Cáp DƯL " + inp.StrandType, "kg", r.StrandWeight));
            r.Extra.Add(("Neo cáp DƯL (" + Math.Max(1, inp.StrandsPerTendon) + " tao/neo)", "bộ", 2 * tendons));
            if (inp.Bonded)
            {
                r.Extra.Add(("Ống gen cáp DƯL", "m", tendons * L));
                r.Extra.Add(("Vữa bơm ống gen", "m³", tendons * L * Math.PI * Math.Pow(DuctDiameter(inp) / 2, 2)));
            }
        }
        r.ConcreteVolume = sec.A * L;
        bool tee = !slab && inp.Bf > b + 1e-9 && inp.Hf > 1e-9;
        double perim = slab ? 1.0 : b + 2 * h + (tee ? (inp.Bf - b) + 2 * inp.Hf : 0);
        r.FormworkArea = perim * L;
    }

    /// <summary>Đường kính ống gen (m) theo số tao/bó.</summary>
    public static double DuctDiameter(PrestressInput inp)
    {
        int k = Math.Max(1, inp.StrandsPerTendon);
        return k <= 1 ? 0.025 : k <= 4 ? 0.050 : k <= 7 ? 0.060 : 0.080;
    }

    // ==================================================================
    // BẢN VẼ
    // ==================================================================
    public static List<Figure> Draw(PrestressInput inp, PrestressResult r, DetailSettings st)
    {
        st ??= new DetailSettings();
        r ??= Design(inp, st);
        var figs = new List<Figure>();
        try
        {
            figs.Add(Elevation(inp, r));
            figs.Add(Section(inp, r, st));
        }
        catch (Exception) { /* hình không vẽ được: bỏ qua, kết quả tính vẫn dùng được */ }
        return figs;
    }

    private static Figure Elevation(PrestressInput inp, PrestressResult r)
    {
        const double S = 50;
        var w = new DxfWriter(S);
        double th = w.Th;
        bool slab = (inp.MemberType ?? "").StartsWith("Sàn");
        double L = inp.L * 1000, h = inp.H * 1000;
        var sec = r.Section ?? PrestressSection.Of(slab ? 1 : inp.B, inp.H, slab ? 0 : inp.Bf, slab ? 0 : inp.Hf);
        double yt = sec.Yt * 1000;
        double ext = 300;
        // bê tông
        w.Line(Lay.BeTong, -ext, 0, L + ext, 0);
        w.Line(Lay.BeTong, -ext, -h, L + ext, -h);
        w.Line(Lay.BeTong, -ext, 0, -ext, -h);
        w.Line(Lay.BeTong, L + ext, 0, L + ext, -h);
        if (!slab && inp.Bf > inp.B && inp.Hf > 0) w.Line(Lay.BeTong, -ext, -inp.Hf * 1000, L + ext, -inp.Hf * 1000, "DASHED");
        // trục trọng tâm
        w.Line(Lay.Truc, -ext - 200, -yt, L + ext + 200, -yt, "CENTER");
        w.Text(Lay.Chu, L + ext + 250, -yt, th, "TRỌNG TÂM", 0, 0, 2);
        // cáp parabol
        var pts = new List<(double X, double Y)>();
        int nseg = 40;
        double sag = (inp.EcMid - inp.EEnd) * 1000, e0 = inp.EEnd * 1000;
        for (int i = 0; i <= nseg; i++)
        {
            double x = L * i / nseg;
            double ex = e0 + 4 * sag * x * (L - x) / (L * L);
            pts.Add((x, -yt - ex));
        }
        w.Polyline(Lay.Thep, pts, false, 0.6 * S);
        w.Line(Lay.Thep, -ext, -yt - e0, 0, -yt - e0);
        w.Line(Lay.Thep, L, -yt - e0, L + ext, -yt - e0);
        // neo
        foreach (double xa in new[] { -ext, L + ext })
            w.Rect(Lay.KyHieu, xa - 30, -yt - e0 - 90, xa + 30, -yt - e0 + 90);
        // gối
        foreach (double xs in new[] { 0.0, L })
        {
            double y = -h;
            w.Polyline(Lay.KyHieu, new List<(double, double)> { (xs, y), (xs - 150, y - 260), (xs + 150, y - 260) }, true);
        }
        // kích thước: nhịp, độ lệch tâm đầu / giữa
        w.Dim(0, -h - 300, L, -h - 300, -400);
        w.Dim(-ext, 0, -ext, -h, 500);
        if (Math.Abs(e0) > 1) w.Dim(0, -yt, 0, -yt - e0, -350);
        w.Dim(L / 2, -yt, L / 2, -yt - (e0 + sag), -350);
        double ym = -yt - e0 - sag;
        string ns = (r.Ap > 0 ? Math.Round(r.Ap / StrandArea(inp.StrandType)).ToString("0", Dt.Inv) : inp.NumStrands.ToString(Dt.Inv)) + " tao " + inp.StrandType;
        w.LeaderTag(L * 0.3, -yt - (e0 + 4 * sag * 0.3 * 0.7), L * 0.3 + 600, -h - 900, "Cáp DƯL " + ns, 0, 1);
        w.Text(Lay.Chu, L / 2, ym - 2.2 * th, th, "ec = " + Dt.Mm(inp.EcMid) + "  (e0 = " + Dt.Mm(inp.EEnd) + ")", 0, 1, 2);
        w.Title(L / 2, -h - 1500, $"{r.Name} (SL={Math.Max(1, r.Quantity)}; L={Math.Round(L).ToString("0", Dt.Inv)})", DrawUtil.ScaleText(S));
        return new Figure(w, r.Name + " – mặt đứng cáp");
    }

    private static Figure Section(PrestressInput inp, PrestressResult r, DetailSettings st)
    {
        const double S = 50, Ss = 25;
        double k = S / Ss;
        var w = new DxfWriter(S);
        double th = w.Th;
        bool slab = (inp.MemberType ?? "").StartsWith("Sàn");
        double b = (slab ? 1.0 : inp.B) * 1000 * k, h = inp.H * 1000 * k;
        bool tee = !slab && inp.Bf > inp.B && inp.Hf > 0;
        double bf = tee ? inp.Bf * 1000 * k : b, hf = tee ? inp.Hf * 1000 * k : 0;
        // bê tông
        if (tee)
            w.Polyline(Lay.BeTong, new List<(double, double)> { (-bf / 2, 0), (bf / 2, 0), (bf / 2, -hf), (b / 2, -hf), (b / 2, -h), (-b / 2, -h), (-b / 2, -hf), (-bf / 2, -hf) }, true);
        else w.Rect(Lay.BeTong, -b / 2, -h, b / 2, 0);
        double c = st.CoverBeam * 1000 * k;
        var sec = r.Section ?? PrestressSection.Of(slab ? 1 : inp.B, inp.H, slab ? 0 : inp.Bf, slab ? 0 : inp.Hf);
        // đai
        var stir = r.Bars.FirstOrDefault(x => x.Role == "Đai");
        if (stir != null)
        {
            double cs = Math.Max(c - stir.Dia * k, 15 * k);
            w.Rect(Lay.Dai, -b / 2 + cs, -h + cs, b / 2 - cs, -cs);
            w.LeaderTag(b / 2 - cs, -h / 2, b / 2 + 250 * k, -h / 2, Dt.Spaced(stir.Dia, inp.StirrupSpacing), stir.Mark, 1);
        }
        // thép thường
        foreach (var bar in r.Bars.Where(x => x.Role == "Dưới" || x.Role == "Trên"))
        {
            bool top = bar.Role == "Trên";
            double rr = bar.Dia / 2.0 * k;
            double y = top ? -c - rr : -h + c + rr;
            int nb = slab ? Math.Min(bar.Count, 8) : bar.Count;
            double span = b - 2 * c - 2 * rr;
            for (int i = 0; i < nb; i++)
            {
                double x = nb == 1 ? 0 : -span / 2 + span * i / (nb - 1);
                w.Dot(Lay.Thep, x, y, rr);
            }
            double x0 = nb == 1 ? 0 : -span / 2, x1 = nb == 1 ? 0 : span / 2;
            if (nb > 1) w.Line(Lay.KyHieu, x0, y, x1, y);
            string txt = slab ? Dt.Spaced(bar.Dia, 1.0 / Math.Max(1, bar.Count)) : Dt.Bars(bar.Count, bar.Dia);
            double ky = top ? 250 * k : -h - 250 * k;
            w.LeaderTag(x1, y, b / 2 + 150 * k, ky, txt, bar.Mark, 1);
        }
        // cáp / ống gen tại giữa nhịp
        double yc = -(sec.Yt + inp.EcMid) * 1000 * k;
        double dd = DuctDiameter(inp) * 1000 * k;
        w.Circle(Lay.Thep, 0, yc, dd / 2);
        w.Dot(Lay.Thep, 0, yc, Math.Max(dd / 5, 6 * k));
        w.LeaderTag(0, yc, -b / 2 - 200 * k, yc - 300 * k, "Cáp DƯL " + inp.NumStrands + " tao " + inp.StrandType, 0, -1);
        // kích thước
        w.Dim(-bf / 2, 0, bf / 2, 0, 200 * k, k);
        if (tee) w.Dim(-b / 2, -h, b / 2, -h, -400 * k, k);
        w.Dim(-bf / 2, 0, -bf / 2, -h, -150 * k, k);
        w.Dim(b / 2, -h, b / 2, yc, -120 * k, k);
        w.Title(0, -h - 700 * k, "1-1", DrawUtil.ScaleText(Ss), true);
        return new Figure(w, r.Name + " – mặt cắt 1-1");
    }
}
