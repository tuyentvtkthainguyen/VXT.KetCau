using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// CẦU THANG BỘ – CÁC DẠNG KẾT CẤU ĐẶC BIỆT (chế độ Triển khai hồ sơ):
//  • Bậc đổ BTCT liền khối với bản thang thường: thép góc mũi bậc + thép chữ L ôm bậc (Ø6–Ø8), chi tiết bậc.
//  • Thang zíc zắc (bản gấp khúc răng cưa, không có bản nghiêng): bản gấp dày t tính như bản đơn giản theo hình chiếu bằng.
//  • Thang xương cá: một dầm cốn giữa (b × h) đỡ các bậc công xôn hai bên / một bên – uốn, cắt, XOẮN dầm cốn (RcTorsion).
//  • Thang thép hình: hai cốn thép (U, I, H, hộp) + bậc thép tấm / gỗ / đá trên thép góc – TCVN 5575:2024 (rút gọn).
// Hình học các vế (số bậc, h, b, bề rộng vế, chiều dài vế, chiếu nghỉ) lấy từ StairLayout của dự án mini (StairDetail.cs).
// Đơn vị: m, kN, kN/m, kN.m; ứng suất thép MPa.
// =====================================================================

/// <summary>Tên các dạng kết cấu thang (ô chọn "Kết cấu thang").</summary>
public static class StairForm
{
    public const string Flat = "Bản thang thường (bản phẳng)";
    public const string Zigzag = "Thang zíc zắc (bản gấp khúc)";
    public const string Fishbone = "Thang xương cá (dầm cốn giữa, bậc công xôn)";
    public const string Steel = "Thang thép hình";
    public const string Choices = Flat + "|" + Zigzag + "|" + Fishbone + "|" + Steel;
    public static bool IsZigzag(string f) => (f ?? "").Contains("zíc", StringComparison.OrdinalIgnoreCase) || (f ?? "").Contains("zic", StringComparison.OrdinalIgnoreCase);
    public static bool IsFishbone(string f) => (f ?? "").Contains("xương cá", StringComparison.OrdinalIgnoreCase);
    public static bool IsSteel(string f) => (f ?? "").Contains("thép hình", StringComparison.OrdinalIgnoreCase);
    public static bool IsFlat(string f) => !IsZigzag(f) && !IsFishbone(f) && !IsSteel(f);
}

/// <summary>Tiết diện thép hình: H, B, tw, tf (mm); A (cm²), Ix (cm⁴), Wx (cm³), khối lượng (kg/m), chu vi sơn (m/m).</summary>
public record SteelShape(string Name, string Kind, double H, double B, double Tw, double Tf, double A, double Ix, double Wx, double Mass)
{
    /// <summary>Diện tích chịu cắt bụng (mm²): U/I/H: (H − 2tf)·tw; hộp: 2·(H − 2t)·t.</summary>
    public double ShearArea => Kind == "Hộp" ? 2 * (H - 2 * Tf) * Tw : (H - 2 * Tf) * Tw;
    /// <summary>Chu vi sơn (m/m dài).</summary>
    public double PaintPerimeter => Kind == "Hộp" ? 2 * (H + B) / 1000 : Kind == "U" ? (2 * H + 4 * B - 2 * Tw) / 1000 : (2 * H + 4 * B - 2 * Tw) / 1000;
}

/// <summary>Bảng thép hình, thép góc, mác thép dùng cho thang thép.</summary>
public static class SteelTables
{
    /// <summary>Thép hộp chữ nhật thành mỏng (góc vuông, gần đúng): A, Ix, Wx, khối lượng 0,785·A.</summary>
    public static SteelShape Rhs(double h, double b, double t)
    {
        double a = h * b - (h - 2 * t) * (b - 2 * t);                       // mm²
        double ix = (b * h * h * h - (b - 2 * t) * Math.Pow(h - 2 * t, 3)) / 12;  // mm⁴
        string nm = Math.Abs(h - b) < 1e-9 ? $"Hộp vuông {h:0}×{b:0}×{t:0.#}" : $"Hộp {h:0}×{b:0}×{t:0.#}";
        return new SteelShape(nm, "Hộp", h, b, t, t, a / 100, ix / 1e4, ix / (h / 2) / 1e3, Math.Round(0.785 * a / 100, 2));
    }

    /// <summary>Thép U (GOST 8240 / TCVN 7571), I (GOST 8239), H cán nóng (JIS), hộp.</summary>
    public static readonly SteelShape[] Sections =
    {
        new("U160", "U", 160, 64, 5.0, 8.4, 18.1, 747, 93.4, 14.2),
        new("U180", "U", 180, 70, 5.1, 8.7, 20.7, 1090, 121, 16.3),
        new("U200", "U", 200, 76, 5.2, 9.0, 23.4, 1520, 152, 18.4),
        new("U220", "U", 220, 82, 5.4, 9.5, 26.7, 2110, 192, 21.0),
        new("U240", "U", 240, 90, 5.6, 10.0, 30.6, 2900, 242, 24.0),
        new("I200", "I", 200, 100, 5.2, 8.4, 26.8, 1840, 184, 21.0),
        new("I240", "I", 240, 115, 5.6, 9.5, 34.8, 3460, 289, 27.3),
        new("H150×150×7×10", "H", 150, 150, 7, 10, 40.14, 1640, 219, 31.5),
        new("H200×200×8×12", "H", 200, 200, 8, 12, 63.53, 4720, 472, 49.9),
        Rhs(150, 100, 5), Rhs(200, 100, 5), Rhs(150, 150, 5)
    };

    public const string SectionChoices = "U160|U180|U200|U220|U240|I200|I240|H150×150×7×10|H200×200×8×12|Hộp 150×100×5|Hộp 200×100×5|Hộp vuông 150×150×5";

    public static SteelShape Find(string name) =>
        Sections.FirstOrDefault(x => string.Equals(x.Name, (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) ?? Sections[2];

    /// <summary>Thép góc đều cạnh: (tên, cạnh mm, Ix cm⁴, Wmin cm³, kg/m).</summary>
    public static readonly (string Name, double Leg, double Ix, double W, double Mass)[] Angles =
    {
        ("L50×5", 50, 11.2, 3.13, 3.77), ("L63×6", 63, 27.1, 6.00, 5.72), ("L75×6", 75, 46.6, 8.57, 6.89)
    };
    public const string AngleChoices = "L50×5|L63×6|L75×6";
    public static (string Name, double Leg, double Ix, double W, double Mass) FindAngle(string name) =>
        Array.Exists(Angles, a => a.Name == (name ?? "").Trim()) ? Array.Find(Angles, a => a.Name == (name ?? "").Trim()) : Angles[0];

    /// <summary>Cường độ tính toán f (MPa) theo mác thép, chiều dày ≤ 20 mm (TCVN 5575:2024 – f = fy/γM, làm tròn).</summary>
    public static double DesignStrength(string grade) => (grade ?? "").Trim().ToUpperInvariant() switch
    {
        "CCT34" => 210, "CCT38" => 230, "Q235" => 224, "SS400" => 233, _ => 210
    };
    public const string GradeChoices = "CCT34|CCT38|Q235|SS400";
    public const double E = 2.1e5;   // MPa
    /// <summary>Bu lông neo M16 cấp 4.6: khả năng chịu cắt 1 bu lông = fvb·A·γb = 150·201·0,9 (kN).</summary>
    public static double BoltShearM16 => 150 * 201.0 * 0.9 / 1000;
}

/// <summary>Công thức dùng chung (công khai để kiểm thử, thuyết minh).</summary>
public static class StairFormulas
{
    /// <summary>Thể tích bậc BTCT liền khối của một vế: Σ ½·b·h·B = nbậc·½·b·h·B (m³).</summary>
    public static double MonolithicStepVolume(int treads, double b, double h, double width) => treads * 0.5 * b * h * width;
    /// <summary>Trọng lượng bản thang zíc zắc dày t trên 1 m² hình chiếu bằng: 25·t·(b + h)/b (diện tích dải gấp khúc mỗi bậc = t·(b + h)).</summary>
    public static double ZigzagSelfWeight(double t, double b, double h) => b > 1e-9 ? 25 * t * (b + h) / b : 0;
    /// <summary>Bản gấp khúc tựa đơn trên hình chiếu bằng: M = q·L²/8 (kN.m/m).</summary>
    public static double SimpleMoment(double q, double L) => q * L * L / 8;
    /// <summary>Bậc công xôn: M = q·l²/2 + P·l (P tải tập trung ở mút).</summary>
    public static double CantileverMoment(double q, double l, double P) => q * l * l / 2 + P * l;
    /// <summary>Mô men xoắn phân bố lên dầm cốn giữa (kN.m/m): một bên bậc: q·lc·(lc + b)/2 / bậc · … đã quy về 1 m dầm:
    /// t = qStep·lc·(lc + bs)/2 / bTread (qStep: tải trên 1 m bậc, bTread: bề rộng mặt bậc = khoảng cách các bậc).</summary>
    public static double SpineTorquePerM(double qStep, double lc, double spineB, double tread) => tread > 1e-9 ? qStep * lc * (lc + spineB) / 2 / tread : 0;
    /// <summary>Dầm ngàm xoắn hai đầu chịu xoắn phân bố đều t: T gối = t·L/2.</summary>
    public static double SupportTorsion(double tPerM, double L) => tPerM * L / 2;
    /// <summary>Độ võng cốn thép nghiêng (vuông góc trục cốn): δ = 5·w·L⁴/(384·E·I·cos²α) – w tải đứng trên 1 m hình chiếu bằng, L nhịp ngang (m), E kN/m², I m⁴.</summary>
    public static double InclinedDeflection(double w, double L, double E, double I, double cosA) => 5 * w * Math.Pow(L, 4) / (384 * E * I * Math.Max(cosA * cosA, 1e-6));
    /// <summary>Tải hoàn thiện mặt bậc (kN/m²): đá 30 + vữa 20 = 1,2; gỗ 0,5; gạch 0,56; không ốp 0.</summary>
    public static double FinishLoad(string finish)
    {
        string f = finish ?? "";
        if (f.StartsWith("Gỗ")) return 0.5;
        if (f.StartsWith("Gạch")) return 0.56;
        if (f.StartsWith("Không")) return 0;
        return 1.2;
    }
}

/// <summary>Kết quả cầu thang dạng đặc biệt (zíc zắc, xương cá, thép hình) – số liệu cho bản vẽ, thuyết minh.</summary>
public class StairFormResult : DetailResult
{
    public string Form { get; set; } = "";
    public int Flights { get; set; }
    public int Risers { get; set; }
    public int Treads { get; set; }
    public double Riser { get; set; }
    public double Tread { get; set; }
    public double Width { get; set; }
    public double Run { get; set; }
    public double CosA { get; set; } = 1;
    /// <summary>Nhịp tính toán hình chiếu bằng (m)</summary>
    public double Span { get; set; }
    public double T { get; set; }
    public double LandingDepth { get; set; }
    public double LandingT { get; set; }
    /// <summary>Tải tính toán (zíc zắc: kN/m²; xương cá: kN/m dầm cốn; thép: kN/m 1 cốn)</summary>
    public double Q { get; set; }
    public double QSer { get; set; }
    public double M { get; set; }
    public double V { get; set; }
    public double AsReq { get; set; }
    public int MainD { get; set; }
    public double MainS { get; set; }
    public int TopD { get; set; }
    public double TopS { get; set; }
    // xương cá
    public double SpineB { get; set; }
    public double SpineH { get; set; }
    public int Sides { get; set; } = 2;
    public double Lc { get; set; }
    public double StepH { get; set; }
    public double StepQ { get; set; }
    public double StepP { get; set; }
    public double StepM { get; set; }
    public int StepBarN { get; set; }
    public int StepBarD { get; set; }
    public double TorquePerM { get; set; }
    public double Torsion { get; set; }
    public int SpineBotN { get; set; }
    public int SpineBotD { get; set; }
    public int SpineTopN { get; set; }
    public int SpineTopD { get; set; }
    public double StirrupS { get; set; }
    // thép hình
    public SteelShape Shape { get; set; }
    public string Grade { get; set; } = "";
    public double F { get; set; }
    public double Sigma { get; set; }
    public double Tau { get; set; }
    public double Deflection { get; set; }
    public double DeflectionLimit { get; set; }
    public string Angle { get; set; } = "";
    public double SteelKg { get; set; }
    public double Reaction { get; set; }
}

public static class StairForms
{
    private const double Gc = 25;

    // ------------------------------------------------------------------
    // Bậc BTCT liền khối (bản thang thường)
    // ------------------------------------------------------------------
    /// <summary>Thép bậc BTCT liền khối: mỗi bậc 1 thanh góc mũi bậc Ø (6–8) chạy suốt bề rộng vế + thép chữ L Ø6a200 ôm mũi bậc neo vào bản thang.</summary>
    public static void AddStepBars(DetailResult r, StairLayout L, DetailSettings s, int dia)
    {
        int d = dia == 6 ? 6 : 8;
        double c = Math.Max(s.CoverSlab, 0.015);
        int treads = 0;
        foreach (var fl in L.Flights.Where(x => x.Treads > 0))
        {
            treads += fl.Treads;
            r.Bars.Add(Dt.Straight($"Vế {fl.Index} – bậc BTCT: thép góc mũi bậc (dọc bề rộng vế)", d, fl.Treads, Dt.R10(fl.Width - 2 * c)));
            int nl = Dt.CountAcross(fl.Width, c, 0.20);
            r.Bars.Add(Dt.Hooked($"Vế {fl.Index} – bậc BTCT: thép chữ L ôm mũi bậc", 6, fl.Treads * nl,
                Dt.R10(Math.Max(L.Tread - c + 0.05, 0.15)), Dt.R10(Math.Max(L.Riser - c + 0.05, 0.10)), 0, true, 0.20));
        }
        r.Calc.Add($"Bậc đổ BTCT liền khối: {treads} bậc tam giác ½·b·h = ½·{Dt.Mm(L.Tread)}·{Dt.Mm(L.Riser)} mm; mỗi bậc 1Ø{d} góc mũi bậc + Ø6a200 chữ L ôm bậc neo vào bản thang (cấu tạo). "
                   + "Bậc chỉ là tải trọng (25 kN/m³) – chiều dày tính toán vẫn là bản thang hb (không kể phần bậc vào h0, thiên về an toàn).");
    }

    // ------------------------------------------------------------------
    // Thiết kế các dạng đặc biệt
    // ------------------------------------------------------------------
    public static StairFormResult Design(ManualStair m, StairLayout L, DetailSettings s)
    {
        var r = new StairFormResult { Kind = "Cầu thang bộ", Name = m.Name, Quantity = Math.Max(1, m.Quantity), ConcreteClass = s.Concrete, Form = m.Form };
        var fl = L.Flights.Where(x => x.Treads > 0).OrderByDescending(x => x.Risers).FirstOrDefault();
        if (fl == null) { r.Messages.Add("LỖI cầu thang không có vế hợp lệ"); return r; }
        r.Flights = L.Flights.Count(x => x.Treads > 0);
        r.Risers = fl.Risers; r.Treads = fl.Treads; r.Riser = L.Riser; r.Tread = L.Tread; r.Width = fl.Width; r.Run = fl.Run;
        r.CosA = fl.CosA > 0 ? fl.CosA : L.Tread / Math.Sqrt(L.Tread * L.Tread + L.Riser * L.Riser);
        r.T = m.Waist > 0 ? m.Waist : Math.Max(L.WaistT, 0.10);
        var lnd = L.Landings.FirstOrDefault();
        r.LandingDepth = lnd?.Span ?? 0;
        r.LandingT = lnd != null ? Math.Max(lnd.Thickness, r.T) : 0;
        // nhịp hình chiếu bằng: phần bậc + gối (có dầm chiếu nghỉ: + bề rộng dầm; không: bản kéo dài qua chiếu nghỉ tới tường / dầm)
        r.Span = r.Run + (m.LandingBeam || lnd == null ? Math.Max(m.LandingBeamB, 0.2) : r.LandingDepth);
        r.Calc.Add("## Cầu thang " + m.Name + " – " + m.Form);
        r.Calc.Add($"H tầng = {Dt.F(m.FloorHeight)} m; {L.NRisers} cổ bậc h = {Dt.Mm(L.Riser)} mm, b = {Dt.Mm(L.Tread)} mm; {r.Flights} vế, vế dài nhất {fl.Risers} bậc, "
                   + $"hình chiếu {Dt.F(r.Run, "0.###")} m, rộng {Dt.Mm(r.Width)} mm, cos α = {Dt.F(r.CosA, "0.000")}; nhịp tính toán L = {Dt.F(r.Span, "0.###")} m.");
        try
        {
            if (StairForm.IsZigzag(m.Form)) Zigzag(m, L, s, r);
            else if (StairForm.IsFishbone(m.Form)) Fishbone(m, L, s, r);
            else Steel(m, L, s, r);
            if (lnd != null) Landing(m, L, s, r);
            for (int i = 0; i < r.Bars.Count; i++) r.Bars[i].Mark = i + 1;
            foreach (var k in r.Checks.Where(x => !x.Ok))
                if (!r.Messages.Any(x => x.Contains(k.Name))) r.Messages.Add("LỖI " + k.Name + ": " + Dt.F(k.Demand, "0.00") + " > " + Dt.F(k.Capacity, "0.00") + " " + k.Unit);
        }
        catch (Exception ex) { r.Messages.Add("LỖI tính cầu thang: " + ex.Message); }
        return r;
    }

    private static (double M, double Mu, double AsReq, int D, double S) SlabStrip(string name, double M, double h, double c, int[] dias, Material conc, Material steel, DetailResult r, double sMax = 0.20)
    {
        double h0 = h - c - 0.005;
        var fx = RcSection.DesignRect(M, 1.0, h0, c + 0.005, conc, steel, 0.1);
        if (!fx.Ok) r.Messages.Add("LỖI " + name + ": " + fx.Note);
        double asReq = Math.Max(fx.As, Dt.AreaPerM(8, 0.20));
        var (d, sp) = Dt.ChooseSpaced(asReq, dias, sMax, 0.10);
        double h0p = h - c - d / 2000.0;
        double mu = RcSection.MomentCapacity(1.0, h0p, Dt.AreaPerM(d, sp), 0, c, conc, steel);
        r.Checks.Add(Dt.Check(name + " – uốn M ≤ Mu (" + Dt.Spaced(d, sp) + ")", M, mu, "kN.m/m"));
        r.Calc.Add($"{name}: h = {Dt.Mm(h)} mm, h0 = {Dt.Mm(h0p)} mm, M = {Dt.F(M, "0.##")} kN.m/m → As = {Dt.F(asReq * 1e4, "0.##")} cm²/m, chọn {Dt.Spaced(d, sp)} (As = {Dt.F(Dt.AreaPerM(d, sp) * 1e4, "0.##")} cm²/m), Mu = {Dt.F(mu, "0.##")} kN.m/m.");
        return (M, mu, asReq, d, sp);
    }

    // ---------------- zíc zắc ----------------
    private static void Zigzag(ManualStair m, StairLayout L, DetailSettings s, StairFormResult r)
    {
        var conc = s.Conc();
        double c = Math.Max(s.CoverSlab, 0.015);
        double b = r.Tread, h = r.Riser, t = r.T;
        double gSelf = StairFormulas.ZigzagSelfWeight(t, b, h);
        double gFin = StairFormulas.FinishLoad(m.Finish) * (b + h) / b + 0.27 * (b + h) / b;
        double live = m.Live > 0 ? m.Live : StairUnits.LiveDefault;
        r.Q = 1.1 * (gSelf + gFin) + 1.3 * live;
        r.QSer = gSelf + gFin + live;
        r.M = StairFormulas.SimpleMoment(r.Q, r.Span);
        r.V = r.Q * r.Span / 2;
        r.Calc.Add("## Thang zíc zắc – bản gấp khúc dày t");
        r.Calc.Add($"Mỗi bậc gồm bản mặt bậc dài b và bản cổ bậc cao h cùng dày t = {Dt.Mm(t)} mm, diện tích dải gấp khúc mỗi bậc = t·(b + h). "
                   + $"Tải bản thân trên 1 m² hình chiếu bằng g = 25·t·(b + h)/b = 25·{Dt.F(t, "0.###")}·({Dt.F(b, "0.###")} + {Dt.F(h, "0.###")})/{Dt.F(b, "0.###")} = {Dt.F(gSelf, "0.##")} kN/m²; "
                   + $"hoàn thiện + trát {Dt.F(gFin, "0.##")} kN/m²; hoạt tải {Dt.F(live)} kN/m².");
        r.Calc.Add($"q = 1,1·({Dt.F(gSelf, "0.##")} + {Dt.F(gFin, "0.##")}) + 1,3·{Dt.F(live)} = {Dt.F(r.Q, "0.##")} kN/m²; sơ đồ bản tựa đơn trên hình chiếu bằng nhịp L = {Dt.F(r.Span, "0.###")} m: "
                   + $"M = q·L²/8 = {Dt.F(r.M, "0.##")} kN.m/m, Q = q·L/2 = {Dt.F(r.V, "0.##")} kN/m (phản lực lên dầm / chiếu nghỉ).");
        r.Calc.Add("Giả thiết: tiết diện tính toán là bản mặt bậc dày t (bỏ qua độ cứng, khả năng tăng thêm của cổ bậc – thiên về an toàn); "
                   + "thép chịu lực đi theo đường gấp khúc của mặt dưới, tại mỗi góc lõm thép KHÔNG uốn vòng qua góc mà đặt thép góc chữ L neo chéo qua góc (tránh bật lớp bê tông bảo vệ).");
        var main = SlabStrip("Thang zíc zắc – thép chịu lực lớp dưới", r.M, t, c, new[] { 8, 10, 12 }, conc, s.SteelFor(10), r);
        r.AsReq = main.AsReq; r.MainD = main.D; r.MainS = main.S;
        double mTop = r.Q * r.Span * r.Span / 24;
        var top = SlabStrip("Thang zíc zắc – thép lớp trên (gối, ngàm một phần qL²/24)", mTop, t, c, new[] { 8, 10 }, conc, s.SteelFor(8), r);
        r.TopD = top.D; r.TopS = top.S;
        double h0 = t - c - main.D / 2000.0;
        double qb = 0.5 * conc.Rbt * 1.0 * h0;
        r.Checks.Add(Dt.Check("Thang zíc zắc – cắt Q ≤ 0,5·Rbt·b·h0", r.V, qb, "kN/m"));
        double slender = r.Span / Math.Max(t, 1e-6);
        r.Checks.Add(Dt.Check("Thang zíc zắc – độ mảnh L/t ≤ 20 (độ võng, gần đúng)", slender, 20, ""));
        r.Calc.Add($"Độ võng: kiểm tra gián tiếp theo độ mảnh L/t = {Dt.F(r.Span, "0.###")}/{Dt.F(t, "0.###")} = {Dt.F(slender, "0.#")} ≤ 20 "
                   + "(bản gấp tính như bản phẳng dày t – độ cứng quy đổi bỏ qua cổ bậc, thiên về an toàn); vượt thì tăng t hoặc thêm dầm cốn.");
        if (t < 0.12) r.Messages.Add("Thang zíc zắc dày < 120 mm – khó bố trí 2 lớp thép và thép góc, đề nghị t ≥ 120 mm.");

        // thép
        double prof = r.Treads * b + r.Risers * h;          // chiều dài đường gấp khúc 1 vế
        int nW = Dt.CountAcross(r.Width, c, main.S) * r.Flights;
        double anc = Dt.R10(Math.Max(0.15, 10 * main.D / 1000.0));
        foreach (var fl in L.Flights.Where(x => x.Treads > 0))
        {
            double pr = fl.Treads * b + fl.Risers * h;
            int nm = Dt.CountAcross(fl.Width, c, main.S);
            r.Bars.Add(new BarMark
            {
                Role = $"Vế {fl.Index} – zíc zắc: thép chịu lực lớp dưới (gấp khúc theo bậc)", Dia = main.D, Count = nm, Spacing = main.S, Shape = "Gấp khúc",
                Length = Dt.R10(pr + 2 * anc), Segments = new List<double> { Math.Round(b * 1000), Math.Round(h * 1000) }, Start = 0, End = Dt.R10(pr)
            });
            int nt = Dt.CountAcross(fl.Width, c, top.S);
            r.Bars.Add(new BarMark
            {
                Role = $"Vế {fl.Index} – zíc zắc: thép lớp trên (gấp khúc theo bậc)", Dia = top.D, Count = nt, Spacing = top.S, Shape = "Gấp khúc", IsTop = true,
                Length = Dt.R10(pr + 2 * anc), Segments = new List<double> { Math.Round(b * 1000), Math.Round(h * 1000) }, Start = 0, End = Dt.R10(pr)
            });
            double legA = Dt.R10(Math.Max(b - c, 0.15)), legB = Dt.R10(Math.Max(h + t - 2 * c, 0.10));
            r.Bars.Add(Dt.Hooked($"Vế {fl.Index} – zíc zắc: thép góc lõm chữ L (neo chéo qua góc)", main.D, fl.Treads * nm, legA, legB, 0, false, main.S));
            int nDist = 2 * (fl.Treads + fl.Risers);    // mỗi bản mặt bậc / cổ bậc 1 thanh mỗi lớp
            r.Bars.Add(Dt.Straight($"Vế {fl.Index} – zíc zắc: thép phân bố Ø6 (2 lớp, mỗi bản 1 thanh)", 6, nDist, Dt.R10(fl.Width - 2 * c), false, 0.25));
        }
        // khối lượng
        double vol = 0, form = 0;
        foreach (var fl in L.Flights.Where(x => x.Treads > 0))
        {
            double pr = fl.Treads * b + fl.Risers * h;
            vol += t * pr * fl.Width;
            form += pr * fl.Width + 2 * t * pr;
        }
        r.ConcreteVolume = vol;
        r.FormworkArea = form;
        r.Extra.Add(("Ốp mặt bậc + cổ bậc (" + m.Finish + ")", "m²", L.Flights.Sum(x => (x.Treads * b + x.Risers * h) * x.Width)));
        r.Calc.Add($"Bê tông vế zíc zắc: Σ t·(nbậc·b + ncổ·h)·B = {Dt.F(vol, "0.###")} m³ (không có bậc xây – bản gấp chính là bậc).");
        _ = prof; _ = nW;
    }

    // ---------------- xương cá ----------------
    private static void Fishbone(ManualStair m, StairLayout L, DetailSettings s, StairFormResult r)
    {
        var conc = s.Conc();
        var mainSteel = s.Main();
        var small = s.Small();
        double cs = Math.Max(s.CoverSlab, 0.015), cb = Math.Max(s.CoverBeam, 0.02);
        double b = r.Tread;
        r.Sides = (m.CantileverSides ?? "").StartsWith("1") ? 1 : 2;
        r.SpineB = Math.Max(m.SpineB, 0.15); r.SpineH = Math.Max(m.SpineH, 0.20);
        r.Lc = r.Sides == 2 ? (r.Width - r.SpineB) / 2 : r.Width - r.SpineB;
        r.StepH = m.StepDepth > 0 ? m.StepDepth : Math.Max(r.Riser, 0.10);
        if (r.Lc < 0.20) { r.Messages.Add($"LỖI thang xương cá: phần bậc công xôn {Dt.Mm(r.Lc)} mm quá ngắn – giảm bề rộng dầm cốn hoặc tăng bề rộng vế"); return; }
        double live = m.Live > 0 ? m.Live : StairUnits.LiveDefault;
        double fin = StairFormulas.FinishLoad(m.Finish);
        double gStep = Gc * b * r.StepH + (fin + 0.27) * b;
        r.StepQ = 1.1 * gStep + 1.3 * live * b;
        r.StepP = 1.0;
        r.StepM = StairFormulas.CantileverMoment(r.StepQ, r.Lc, r.StepP);
        double vStep = r.StepQ * r.Lc + r.StepP;
        r.Calc.Add("## Thang xương cá – bậc công xôn " + (r.Sides == 2 ? "hai bên" : "một bên") + " dầm cốn giữa " + Dt.Mm(r.SpineB) + "×" + Dt.Mm(r.SpineH));
        r.Calc.Add($"Bậc đặc BTCT b × hs = {Dt.Mm(b)} × {Dt.Mm(r.StepH)} mm, vươn lc = {Dt.Mm(r.Lc)} mm. Tải trên 1 m bậc: g = 25·b·hs + (ốp + trát)·b = {Dt.F(gStep, "0.##")} kN/m; "
                   + $"q = 1,1·g + 1,3·p·b = {Dt.F(r.StepQ, "0.##")} kN/m; lực tập trung ở mút P = {Dt.F(r.StepP)} kN.");
        r.Calc.Add($"M = q·lc²/2 + P·lc = {Dt.F(r.StepQ, "0.##")}·{Dt.F(r.Lc, "0.###")}²/2 + {Dt.F(r.StepP)}·{Dt.F(r.Lc, "0.###")} = {Dt.F(r.StepM, "0.###")} kN.m; Q = q·lc + P = {Dt.F(vStep, "0.##")} kN.");
        // thép bậc (lớp trên)
        double h0s = r.StepH - cs - 0.006;
        var fs = RcSection.DesignRect(r.StepM, b, h0s, cs + 0.005, conc, mainSteel, 0.1);
        if (!fs.Ok) r.Messages.Add("LỖI bậc công xôn: " + fs.Note);
        int dS = 10, nS = 2;
        foreach (int dd in new[] { 10, 12, 14 })
        {
            int nn = Math.Max(2, (int)Math.Ceiling(fs.As / Dt.Area(dd) - 1e-9));
            if (nn <= 3) { dS = dd; nS = nn; break; }
            dS = dd; nS = nn;
        }
        r.StepBarD = dS; r.StepBarN = nS;
        double muS = RcSection.MomentCapacity(b, r.StepH - cs - dS / 2000.0, nS * Dt.Area(dS), 0, cs, conc, mainSteel);
        r.Checks.Add(Dt.Check($"Bậc công xôn – uốn M ≤ Mu ({nS}Ø{dS} lớp trên)", r.StepM, muS, "kN.m"));
        double qbS = 0.5 * conc.Rbt * b * h0s;
        r.Checks.Add(Dt.Check("Bậc công xôn – cắt Q ≤ 0,5·Rbt·b·h0", vStep, qbS, "kN"));
        r.Calc.Add($"Thép bậc lớp trên: As = {Dt.F(fs.As * 1e4, "0.##")} cm² → {nS}Ø{dS} (Mu = {Dt.F(muS, "0.###")} kN.m) neo qua dầm cốn ≥ 30d; lớp dưới 2Ø8 cấu tạo; Qb = 0,5·Rbt·b·h0 = {Dt.F(qbS, "0.##")} kN.");

        // dầm cốn giữa
        double gSpine = 1.1 * Gc * r.SpineB * r.SpineH / Math.Max(r.CosA, 0.3);
        double topLoad = (1.1 * (fin + 0.27) + 1.3 * live) * r.SpineB;
        r.Q = r.Sides * r.StepQ * r.Lc / b + gSpine + topLoad;
        r.M = r.Q * r.Span * r.Span / 8;
        r.V = r.Q * r.Span / 2;
        double qTorque = r.Sides == 1 ? r.StepQ : 1.3 * live * b;   // 2 bên: hoạt tải lệch một bên
        r.TorquePerM = StairFormulas.SpineTorquePerM(qTorque, r.Lc, r.SpineB, b);
        r.Torsion = StairFormulas.SupportTorsion(r.TorquePerM, r.Span);
        r.Calc.Add($"Dầm cốn giữa: tải trên 1 m hình chiếu bằng w = {r.Sides}·q·lc/b + bản thân 1,1·25·b·h/cos α + mặt dầm = {Dt.F(r.Q, "0.##")} kN/m; nhịp L = {Dt.F(r.Span, "0.###")} m (gối dầm chiếu nghỉ / dầm sàn): "
                   + $"M = w·L²/8 = {Dt.F(r.M, "0.##")} kN.m, Q = w·L/2 = {Dt.F(r.V, "0.##")} kN.");
        r.Calc.Add($"Xoắn: {(r.Sides == 1 ? "bậc một bên – toàn bộ tải bậc" : "bậc hai bên – hoạt tải đặt lệch một bên")} gây mô men xoắn phân bố t = q'·lc·(lc + bs)/2/b = {Dt.F(r.TorquePerM, "0.###")} kN.m/m; "
                   + $"dầm ngàm xoắn hai đầu: T = t·L/2 = {Dt.F(r.Torsion, "0.###")} kN.m.");
        double h0 = r.SpineH - cb - 0.008 - 0.008;
        var fl = RcSection.DesignRect(r.M, r.SpineB, h0, cb + 0.01, conc, mainSteel, 0.1);
        if (!fl.Ok) r.Messages.Add("LỖI dầm cốn giữa: " + fl.Note);
        var tr = RcTorsion.Design(r.Torsion, r.V, r.SpineB, r.SpineH, h0, cb, conc, small, 8, 0.15, mainSteel);
        double asBot = fl.As + tr.AsLong / 2, asTop = Math.Max(tr.AsLong / 2, 2 * Dt.Area(12));
        int dB = 14, nB = 2;
        foreach (int dd in new[] { 14, 16, 18, 20, 22 })
        {
            int nn = Math.Max(2, (int)Math.Ceiling(asBot / Dt.Area(dd) - 1e-9));
            dB = dd; nB = nn;
            if (Dt.FitsInLayer(r.SpineB, cb, 8, nn, dd, false)) break;
        }
        int dT = 12, nT = Math.Max(2, (int)Math.Ceiling(asTop / Dt.Area(12) - 1e-9));
        if (!Dt.FitsInLayer(r.SpineB, cb, 8, nT, 12, true)) { dT = 16; nT = Math.Max(2, (int)Math.Ceiling(asTop / Dt.Area(16) - 1e-9)); }
        r.SpineBotD = dB; r.SpineBotN = nB; r.SpineTopD = dT; r.SpineTopN = nT; r.StirrupS = tr.S;
        double muB = RcSection.MomentCapacity(r.SpineB, r.SpineH - cb - 0.008 - dB / 2000.0, Math.Max(nB * Dt.Area(dB) - tr.AsLong / 2, 0), 0, cb, conc, mainSteel);
        r.AsReq = asBot;
        r.Checks.Add(Dt.Check($"Dầm cốn giữa – uốn M ≤ Mu ({nB}Ø{dB}, đã trừ ½Asl xoắn)", r.M, muB, "kN.m"));
        double qStrut = 0.3 * conc.Rb * r.SpineB * h0;
        r.Checks.Add(Dt.Check("Dầm cốn giữa – cắt Q ≤ 0,3·Rb·b·h0", r.V, qStrut, "kN"));
        r.Checks.Add(Dt.Check("Dầm cốn giữa – xoắn: dải bê tông chịu nén T ≤ 0,1·Rb·b²·h·(1 − Q/Qbt)", r.Torsion, Math.Max(tr.TStrutReduced, 0), "kN.m"));
        r.Checks.Add(Dt.Check($"Dầm cốn giữa – xoắn: đai kín Ø8a{Dt.Mm(tr.S)} T ≤ Tsw", r.Torsion, Math.Max(tr.TswCapacity, 0), "kN.m"));
        if (!string.IsNullOrEmpty(tr.Note)) r.Messages.Add("Dầm cốn giữa: " + tr.Note);
        r.Calc.Add($"Thép dầm cốn: As uốn = {Dt.F(fl.As * 1e4, "0.##")} cm², thép dọc chịu xoắn Asl = T·uk/(2·Ak·Rs) = {Dt.F(tr.AsLong * 1e4, "0.##")} cm² (½ cộng vào lớp dưới, ½ lớp trên) → "
                   + $"dưới {nB}Ø{dB}, trên {nT}Ø{dT}; đai kín Ø8a{Dt.Mm(tr.S)} (móc 135°) theo RcTorsion (TCVN 5574:2018 mục 8.1.4).");

        // thép
        int nSteps = L.Flights.Where(x => x.Treads > 0).Sum(x => x.Treads);
        double stepLen = r.Sides == 2 ? 2 * r.Lc + r.SpineB - 2 * cs : r.Lc + r.SpineB - cs - cb;
        double hookTip = Dt.R10(Math.Max(r.StepH - 2 * cs, 0.06));
        double anchorDown = r.Sides == 2 ? hookTip : Dt.R10(Math.Max(30 * dS / 1000.0, r.SpineH - cb));
        r.Bars.Add(Dt.Hooked("Bậc công xôn – thép chịu lực lớp trên (neo qua dầm cốn)", dS, nS * nSteps, Dt.R10(stepLen), hookTip, anchorDown, true));
        r.Bars.Add(Dt.Straight("Bậc công xôn – thép cấu tạo lớp dưới", 8, 2 * nSteps, Dt.R10(stepLen)));
        r.Bars.Add(Dt.Straight("Bậc công xôn – thép phân bố dọc bậc Ø6", 6, 2 * nSteps * Math.Max(1, (int)Math.Ceiling(r.Lc / 0.25)) * r.Sides, Dt.R10(b - 2 * cs)));
        foreach (var fv in L.Flights.Where(x => x.Treads > 0))
        {
            double len = fv.Inclined + 2 * Dt.R10(Math.Max(0.30, 30 * dB / 1000.0));
            r.Bars.Add(Dt.Straight($"Vế {fv.Index} – dầm cốn giữa: thép lớp dưới", dB, nB, Dt.R10(len)));
            r.Bars.Add(Dt.Straight($"Vế {fv.Index} – dầm cốn giữa: thép lớp trên", dT, nT, Dt.R10(len), true));
            int ns = (int)Math.Ceiling(fv.Inclined / tr.S) + 1;
            r.Bars.Add(Dt.Stirrup($"Vế {fv.Index} – dầm cốn giữa: đai kín chịu xoắn", 8, ns, r.SpineB - 2 * cb + 0.016, r.SpineH - 2 * cb + 0.016, tr.S));
        }
        // khối lượng
        double vol = 0, form = 0;
        foreach (var fv in L.Flights.Where(x => x.Treads > 0))
        {
            vol += r.SpineB * r.SpineH * fv.Inclined + fv.Treads * b * r.StepH * r.Lc * r.Sides;
            form += (r.SpineB + 2 * r.SpineH) * fv.Inclined + fv.Treads * r.Sides * (b * r.Lc + 2 * r.StepH * r.Lc + b * r.StepH);
        }
        r.ConcreteVolume = vol; r.FormworkArea = form;
        r.Reaction = r.V;
        r.Extra.Add(("Ốp mặt bậc (" + m.Finish + ")", "m²", nSteps * b * r.Width));
        r.Calc.Add($"Bê tông: dầm cốn b·h·Lnghiêng + bậc nbậc·b·hs·lc·{r.Sides} = {Dt.F(vol, "0.###")} m³. Phản lực dầm cốn lên dầm chiếu nghỉ / dầm sàn R = {Dt.F(r.V, "0.##")} kN (lực tập trung) + mô men xoắn {Dt.F(r.Torsion, "0.###")} kN.m.");
    }

    // ---------------- thép hình ----------------
    private static void Steel(ManualStair m, StairLayout L, DetailSettings s, StairFormResult r)
    {
        var sh = SteelTables.Find(m.SteelSection);
        r.Shape = sh; r.Grade = string.IsNullOrWhiteSpace(m.SteelGrade) ? "CCT34" : m.SteelGrade;
        r.F = SteelTables.DesignStrength(r.Grade);
        double gc = m.SteelGammaC > 0 ? m.SteelGammaC : 0.95;
        double live = m.Live > 0 ? m.Live : StairUnits.LiveDefault;
        double gTread = (m.SteelTread ?? "").StartsWith("Gỗ") ? 0.35 : (m.SteelTread ?? "").Contains("đá") ? 1.2 : 0.42;
        double gSelf = sh.Mass * 9.81e-3 / Math.Max(r.CosA, 0.3);
        double rail = (m.Railing ?? "").StartsWith("Không") ? 0 : 0.3;
        double halfW = r.Width / 2;
        r.Q = (1.1 * gTread + 1.3 * live) * halfW + 1.05 * gSelf + 1.1 * rail;
        r.QSer = (gTread + live) * halfW + gSelf + rail;
        r.M = r.Q * r.Span * r.Span / 8;
        r.V = r.Q * r.Span / 2;
        double wx = sh.Wx * 1e-6, ix = sh.Ix * 1e-8;
        r.Sigma = r.M / wx / 1000;                       // MPa
        r.Tau = r.V / (sh.ShearArea * 1e-6) / 1000;     // MPa
        r.Deflection = StairFormulas.InclinedDeflection(r.QSer, r.Span, SteelTables.E * 1000, ix, r.CosA);
        double ls = r.Span / Math.Max(r.CosA, 0.3);
        r.DeflectionLimit = ls / 250;
        r.Calc.Add("## Thang thép hình – 2 cốn " + sh.Name + ", thép " + r.Grade);
        r.Calc.Add($"Tiết diện {sh.Name}: A = {Dt.F(sh.A, "0.##")} cm², Ix = {Dt.F(sh.Ix, "0.#")} cm⁴, Wx = {Dt.F(sh.Wx, "0.#")} cm³, {Dt.F(sh.Mass, "0.##")} kg/m; f = {Dt.F(r.F, "0")} MPa (TCVN 5575:2024, t ≤ 20 mm), γc = {Dt.F(gc)}; E = 2,1·10⁵ MPa.");
        r.Calc.Add($"Mỗi cốn gánh ½ bề rộng vế ({Dt.Mm(halfW)} mm): bậc {m.SteelTread} {Dt.F(gTread, "0.##")} kN/m², hoạt tải {Dt.F(live)} kN/m², bản thân cốn {Dt.F(gSelf, "0.###")} kN/m, lan can {Dt.F(rail, "0.#")} kN/m → "
                   + $"w = {Dt.F(r.Q, "0.##")} kN/m (tiêu chuẩn {Dt.F(r.QSer, "0.##")}) trên hình chiếu bằng, nhịp L = {Dt.F(r.Span, "0.###")} m.");
        r.Calc.Add($"M = w·L²/8 = {Dt.F(r.M, "0.##")} kN.m → σ = M/Wx = {Dt.F(r.Sigma, "0.#")} MPa ≤ f·γc = {Dt.F(r.F * gc, "0.#")} MPa; Q = w·L/2 = {Dt.F(r.V, "0.##")} kN → τ = Q/Aw = {Dt.F(r.Tau, "0.#")} MPa ≤ 0,58·f·γc = {Dt.F(0.58 * r.F * gc, "0.#")} MPa.");
        r.Calc.Add($"Độ võng vuông góc trục cốn: δ = 5·wtc·L⁴/(384·E·Ix·cos²α) = {Dt.F(r.Deflection * 1000, "0.#")} mm ≤ Lnghiêng/250 = {Dt.F(r.DeflectionLimit * 1000, "0.#")} mm.");
        r.Checks.Add(Dt.Check($"Cốn thép {sh.Name} – uốn σ ≤ f·γc", r.Sigma, r.F * gc, "MPa"));
        r.Checks.Add(Dt.Check($"Cốn thép {sh.Name} – cắt τ ≤ 0,58·f·γc", r.Tau, 0.58 * r.F * gc, "MPa"));
        r.Checks.Add(Dt.Check($"Cốn thép {sh.Name} – độ võng δ ≤ L/250", r.Deflection * 1000, r.DeflectionLimit * 1000, "mm"));
        if (sh.Kind == "U" || sh.Kind == "Hộp")
            r.Calc.Add("Ổn định tổng thể: cốn liên kết với các bậc (thép góc hàn) ở mỗi bậc – cánh nén được giữ ngang tại từng bậc, không cần kiểm tra ổn định tổng thể (TCVN 5575:2024 mục 8.4.4).");
        else r.Calc.Add("Ổn định tổng thể: cánh nén được giữ bởi thép góc đỡ bậc ở khoảng cách b ≤ 300 mm – thỏa điều kiện không cần kiểm tra ổn định tổng thể.");

        // thép góc đỡ bậc: mỗi bậc 2 thanh, nhịp = bề rộng vế giữa 2 cốn
        var ang = SteelTables.FindAngle(m.TreadAngle);
        r.Angle = ang.Name;
        double span = Math.Max(r.Width - sh.B / 1000, 0.3);
        double wa = (1.1 * gTread + 1.3 * live) * r.Tread / 2, waS = (gTread + live) * r.Tread / 2;
        double ma = wa * span * span / 8 + 1.0 * span / 4;
        double sa = ma / (ang.W * 1e-6) / 1000;
        double ia = ang.Ix * 1e-8, e = SteelTables.E * 1000;
        double da = 5 * waS * Math.Pow(span, 4) / (384 * e * ia) + 1.0 * Math.Pow(span, 3) / (48 * e * ia);
        r.Checks.Add(Dt.Check($"Thép góc đỡ bậc {ang.Name} – uốn σ ≤ f·γc", sa, r.F * gc, "MPa"));
        r.Checks.Add(Dt.Check($"Thép góc đỡ bậc {ang.Name} – độ võng ≤ l/200", da * 1000, span / 200 * 1000, "mm"));
        r.Calc.Add($"Thép góc đỡ bậc {ang.Name} (2 thanh / bậc, nhịp l = {Dt.Mm(span)} mm giữa 2 cốn): q = {Dt.F(wa, "0.##")} kN/m + P = 1 kN giữa nhịp → M = q·l²/8 + P·l/4 = {Dt.F(ma, "0.###")} kN.m, "
                   + $"σ = {Dt.F(sa, "0.#")} MPa; δ = {Dt.F(da * 1000, "0.##")} mm ≤ l/200 = {Dt.F(span / 200 * 1000, "0.#")} mm. Liên kết hàn góc hf = 5 mm suốt chiều dài tiếp xúc với bụng cốn.");
        // bản đế + bu lông neo
        r.Reaction = r.V;
        double cap = 2 * SteelTables.BoltShearM16;
        r.Checks.Add(Dt.Check("Chân cốn – 2 bu lông neo M16 (4.6) chịu cắt", r.V, cap, "kN"));
        r.Calc.Add($"Chân / đầu cốn: bản mã 10 mm hàn đầu cốn, 2 bu lông neo hoá chất M16 cấp 4.6 sâu ≥ 125 mm: R = {Dt.F(r.V, "0.##")} kN ≤ 2·fvb·A·γb = 2·150·201·0,9 = {Dt.F(cap, "0.#")} kN. "
                   + "Đường hàn bản mã – cốn hf = 6 mm bao quanh tiết diện (que E42, kiểm tra theo TCVN 5575:2024 mục 11.2).");

        // khối lượng thép
        double kgStr = 0, kgAng = 0, kgPlate = 0, kgBase = 0, paint = 0;
        int nBase = 0;
        foreach (var fv in L.Flights.Where(x => x.Treads > 0))
        {
            double lsv = fv.Inclined + 0.2;
            kgStr += 2 * sh.Mass * lsv;
            paint += 2 * sh.PaintPerimeter * lsv;
            double la = Math.Max(fv.Width - sh.B / 1000, 0.3);
            kgAng += 2 * fv.Treads * ang.Mass * la;
            paint += 2 * fv.Treads * 4 * ang.Leg / 1000 * la;
            if (!(m.SteelTread ?? "").StartsWith("Gỗ")) { kgPlate += fv.Treads * 42.0 * r.Tread * fv.Width; paint += 2 * fv.Treads * r.Tread * fv.Width; }
            nBase += 4;
            double bp = 7850 * 0.010 * 0.25 * (sh.H / 1000 + 0.10);
            kgBase += 4 * bp; paint += 4 * 2 * 0.25 * (sh.H / 1000 + 0.10);
        }
        r.SteelKg = kgStr + kgAng + kgPlate + kgBase;
        r.Extra.Add(($"Thép hình cốn thang {sh.Name} ({r.Grade})", "kg", Math.Round(kgStr, 1)));
        r.Extra.Add(($"Thép góc đỡ bậc {ang.Name}", "kg", Math.Round(kgAng, 1)));
        if (kgPlate > 0) r.Extra.Add(("Thép tấm gân mặt bậc 5 mm", "kg", Math.Round(kgPlate, 1)));
        r.Extra.Add(("Bản mã chân / đầu cốn 10 mm", "kg", Math.Round(kgBase, 1)));
        r.Extra.Add(("Bu lông neo hoá chất M16", "bộ", 2 * nBase));
        r.Extra.Add(("Sơn chống gỉ 1 lớp + sơn phủ 2 lớp kết cấu thép", "m²", Math.Round(paint, 2)));
        if ((m.SteelTread ?? "").StartsWith("Gỗ")) r.Extra.Add(("Mặt bậc gỗ dày 40 mm", "m²", L.Flights.Sum(x => x.Treads * r.Tread * x.Width)));
        if ((m.SteelTread ?? "").Contains("đá")) r.Extra.Add(("Ốp đá mặt bậc trên thép tấm", "m²", L.Flights.Sum(x => x.Treads * r.Tread * x.Width)));
        r.Calc.Add($"Khối lượng thép: cốn {Dt.F(kgStr, "0.#")} kg, thép góc {Dt.F(kgAng, "0.#")} kg, thép tấm {Dt.F(kgPlate, "0.#")} kg, bản mã {Dt.F(kgBase, "0.#")} kg; tổng {Dt.F(r.SteelKg, "0.#")} kg; sơn {Dt.F(paint, "0.#")} m².");
    }

    // ---------------- chiếu nghỉ BTCT (dạng đặc biệt) ----------------
    private static void Landing(ManualStair m, StairLayout L, DetailSettings s, StairFormResult r)
    {
        var lnd = L.Landings[0];
        double t = Math.Max(r.LandingT, 0.10);
        double live = m.Live > 0 ? m.Live : StairUnits.LiveDefault;
        double q = 1.1 * (Gc * t + StairFormulas.FinishLoad(m.Finish) + 0.27) + 1.3 * live;
        double reaction = StairForm.IsSteel(m.Form) ? 2 * r.V / Math.Max(r.Width, 0.5) : (StairForm.IsFishbone(m.Form) ? 0 : r.V);
        double span = Math.Max(lnd.Span, 0.6);
        double M = q * span * span / 8 + (StairForm.IsZigzag(m.Form) || StairForm.IsSteel(m.Form) ? 0 : 0);
        double c = Math.Max(s.CoverSlab, 0.015);
        r.Calc.Add("## Chiếu nghỉ BTCT dày " + Dt.Mm(t) + " mm");
        r.Calc.Add($"Bản kê dầm chiếu nghỉ / tường, nhịp {Dt.F(span, "0.###")} m: q = 1,1·(25·t + ốp + trát) + 1,3·p = {Dt.F(q, "0.##")} kN/m², M = q·L²/8 = {Dt.F(M, "0.##")} kN.m/m"
                   + (reaction > 0 ? $"; phản lực vế thang truyền vào dầm chiếu nghỉ {Dt.F(reaction, "0.##")} kN/m (tính dầm chiếu nghỉ ở mục Dầm)." : "."));
        var res = SlabStrip("Chiếu nghỉ – thép lớp dưới", M, t, c, new[] { 8, 10 }, s.Conc(), s.SteelFor(8), r);
        foreach (var ld in L.Landings)
        {
            double lx = ld.Span, ly = ld.Width;
            r.Bars.Add(Dt.Hooked("Chiếu nghỉ – thép lớp dưới phương ngắn", res.D, Dt.CountAcross(ly, c, res.S), Dt.R10(lx - 2 * c), 0.10, 0.10, false, res.S));
            r.Bars.Add(Dt.Hooked("Chiếu nghỉ – thép lớp dưới phương dài", 8, Dt.CountAcross(lx, c, 0.20), Dt.R10(ly - 2 * c), 0.10, 0.10, false, 0.20));
            r.Bars.Add(Dt.Hooked("Chiếu nghỉ – thép mũ gối", 8, 2 * Dt.CountAcross(ly, c, 0.20), Dt.R10(lx / 4 + 0.2), Dt.R10(t - 2 * c), Dt.R10(t - 2 * c), true, 0.20));
            r.ConcreteVolume += ld.Area * t;
            r.FormworkArea += ld.Area + 2 * (lx + ly) * t;
        }
        if (m.LandingBeam)
        {
            double lb = L.Wr;
            r.ConcreteVolume += m.LandingBeamB * m.LandingBeamH * lb;
            r.FormworkArea += (m.LandingBeamB + 2 * m.LandingBeamH) * lb;
            int dLb = 16;
            r.Bars.Add(Dt.Straight("Dầm chiếu nghỉ – thép lớp dưới (cấu tạo, tính ở mục Dầm)", dLb, 2, Dt.R10(lb + 0.4)));
            r.Bars.Add(Dt.Straight("Dầm chiếu nghỉ – thép lớp trên", 14, 2, Dt.R10(lb + 0.4), true));
            r.Bars.Add(Dt.Stirrup("Dầm chiếu nghỉ – đai", 6, (int)Math.Ceiling(lb / 0.15) + 1, m.LandingBeamB - 2 * s.CoverBeam + 0.012, m.LandingBeamH - 2 * s.CoverBeam + 0.012, 0.15));
            r.Calc.Add($"Dầm chiếu nghỉ {Dt.Mm(m.LandingBeamB)}×{Dt.Mm(m.LandingBeamH)} dài {Dt.F(lb, "0.##")} m: thép cấu tạo 2Ø16 + 2Ø14, đai Ø6a150 – nên kiểm tra lại ở mục Dầm với tải phản lực vế thang.");
        }
    }

    // ------------------------------------------------------------------
    // Bản vẽ
    // ------------------------------------------------------------------
    public const double SecScale = 25;

    private static int Mk(DetailResult r, string contains) => r.Bars.FirstOrDefault(b => b.Role.Contains(contains, StringComparison.Ordinal))?.Mark ?? 0;

    public static List<Figure> Draw(StairFormResult r, DetailSettings s)
    {
        var list = new List<Figure>();
        if (r == null || r.Treads <= 0) return list;
        if (StairForm.IsZigzag(r.Form)) { list.Add(new Figure(ZigzagSection(r, s), "MẶT CẮT VẾ THANG ZÍC ZẮC " + r.Name)); list.Add(new Figure(FlightCross(r, s), "MẶT CẮT 1-1 " + r.Name)); }
        else if (StairForm.IsFishbone(r.Form)) { list.Add(new Figure(FishboneElevation(r, s), "MẶT ĐỨNG DẦM CỐN " + r.Name)); list.Add(new Figure(FishboneCross(r, s), "MẶT CẮT 1-1 BẬC CÔNG XÔN " + r.Name)); }
        else if (r.Shape != null) { list.Add(new Figure(SteelElevation(r), "MẶT ĐỨNG CỐN THÉP " + r.Name)); list.Add(new Figure(SteelCross(r), "MẶT CẮT 1-1 THANG THÉP " + r.Name)); list.Add(new Figure(SteelBase(r), "CHI TIẾT CHÂN CỐN " + r.Name)); }
        return list;
    }

    /// <summary>Các điểm mặt trên đường bậc (mm, gốc chân vế): đoạn sàn dưới, cổ bậc, mặt bậc… tới mặt chiếu nghỉ.</summary>
    private static List<(double X, double Y)> StepProfile(int risers, double b, double h, double before, double after)
    {
        var p = new List<(double X, double Y)> { (-before, 0), (0, 0) };
        for (int i = 0; i < risers; i++)
        {
            p.Add((i * b, (i + 1) * h));
            if (i < risers - 1) p.Add(((i + 1) * b, (i + 1) * h));
        }
        p.Add(((risers - 1) * b + after, risers * h));
        return p;
    }

    private static List<(double X, double Y)> Shift(List<(double X, double Y)> p, double dx, double dy) => p.Select(q => (q.X + dx, q.Y + dy)).ToList();

    public static DxfWriter ZigzagSection(StairFormResult r, DetailSettings s)
    {
        var w = new DxfWriter(SecScale);
        double th = w.Th;
        double b = r.Tread * 1000, h = r.Riser * 1000, t = r.T * 1000, c = Math.Max(s.CoverSlab, 0.015) * 1000;
        int n = r.Risers;
        double before = 400, after = Math.Max(400, r.LandingDepth * 1000 * 0.5);
        var top = StepProfile(n, b, h, before, after);
        var bot = Shift(top, t, -t);
        bot[0] = (-before, -t); bot[^1] = (top[^1].X, top[^1].Y - t);
        var outline = new List<(double X, double Y)>(top);
        for (int i = bot.Count - 1; i >= 0; i--) outline.Add(bot[i]);
        if (w.HatchOn) w.HatchMat(HatchMaterial.Concrete, outline);
        w.Polyline(Lay.BeTong, outline, true);
        w.BreakV(Lay.BeTong, -before, -t, 0);
        // thép lớp dưới: theo mặt dưới, cách c
        var barBot = Shift(top, t - c, -t + c);
        barBot[0] = (-before + 50, -t + c); barBot[^1] = (top[^1].X - 50, top[^1].Y - t + c);
        w.Polyline(Lay.Thep, barBot, false, 0.35 * w.Scale);
        var barTop = Shift(top, c, -c);
        barTop[0] = (-before + 50, -c); barTop[^1] = (top[^1].X - 50, top[^1].Y - c);
        w.Polyline(Lay.Thep, barTop, false, 0.30 * w.Scale);
        // thép góc lõm chữ L tại các góc lõm mặt dưới: (i·b + t, (i+1)·h − t)
        double la = Math.Min(b - c, 0.8 * b), lb = Math.Min(h + t - 2 * c, h);
        for (int i = 0; i < n - 1; i++)
        {
            double cx = i * b + t - c * 0.0, cy = (i + 1) * h - t;
            var pl = new List<(double X, double Y)> { (cx - c - 0.0, cy - lb), (cx - c, cy + c), (cx - c + la, cy + c) };
            w.Polyline(Lay.Thep, pl, false, 0.30 * w.Scale);
        }
        // thép phân bố: chấm giữa mỗi bản (2 lớp)
        double dr = Math.Max(0.12 * th, 3);
        for (int i = 0; i < n - 1; i++)
        {
            double xm = i * b + b / 2 + t / 2, yb0 = (i + 1) * h - t + c + 6, yt0 = (i + 1) * h - c - 6;
            w.Dot(Lay.Thep, xm, yb0, dr); w.Dot(Lay.Thep, xm, yt0, dr);
        }
        // nhãn
        int k0 = Math.Min(n - 2, Math.Max(0, n / 2));
        double px = k0 * b + b / 2 + t, py = (k0 + 1) * h - t + c;
        w.LeaderTag(px, py, px + 3 * th, py - 5 * th, Dt.Spaced(r.MainD, r.MainS), Mk(r, "lớp dưới (gấp khúc"), 1);
        double qx = (k0 + 1) * b - b / 3, qy = (k0 + 1) * h - c;
        w.LeaderTag(qx, qy, qx - 3 * th, qy + 5 * th, Dt.Spaced(r.TopD, r.TopS), Mk(r, "lớp trên (gấp khúc"), -1);
        int k1 = Math.Max(0, k0 - 2);
        double lx = k1 * b + t - c, ly = (k1 + 1) * h - t + c;
        w.LeaderTag(lx, ly - lb / 2, lx - 4 * th, ly - 4 * th, Dt.Spaced(r.MainD, r.MainS), Mk(r, "góc lõm"), -1);
        // kích thước
        var xs = new List<double>();
        for (int i = 0; i < n; i++) xs.Add(i * b);
        w.DimChainH(xs, (n) * h, n * h + 3 * th);
        var ys = new List<double>();
        for (int i = 0; i <= n; i++) ys.Add(i * h);
        w.DimChainV(ys, top[^1].X, top[^1].X + 3 * th);
        w.Dim(0, -t, 0, 0, 3 * th);
        w.Text(Lay.Chu, (n - 1) * b * 0.5, -t - 4 * th, th, $"BẢN GẤP KHÚC DÀY {Dt.Mm(r.T)} – {r.Risers} BẬC {Dt.Mm(r.Riser)}×{Dt.Mm(r.Tread)}", 0, 1, 0);
        w.Text(Lay.Chu, (n - 1) * b * 0.5, -t - 5.6 * th, 0.85 * th, "TẠI GÓC LÕM KHÔNG UỐN THÉP VÒNG QUA GÓC – ĐẶT THÉP GÓC CHỮ L NEO CHÉO", 0, 1, 0);
        w.Title((n - 1) * b * 0.5, w.Extents.Y2 + 4 * th, "MẶT CẮT VẾ THANG ZÍC ZẮC " + r.Name, DrawUtil.ScaleText(SecScale));
        return w;
    }

    /// <summary>Mặt cắt ngang vế (zíc zắc): bản dày t × bề rộng vế, thép chịu lực (chấm) và thép phân bố.</summary>
    public static DxfWriter FlightCross(StairFormResult r, DetailSettings s)
    {
        var w = new DxfWriter(SecScale);
        double th = w.Th;
        double bw = r.Width * 1000, t = r.T * 1000, c = Math.Max(s.CoverSlab, 0.015) * 1000;
        if (w.HatchOn) w.HatchMatRect(HatchMaterial.Concrete, 0, 0, bw, t);
        w.Rect(Lay.BeTong, 0, 0, bw, t);
        w.Line(Lay.Thep, c, c + 8, bw - c, c + 8);
        w.Line(Lay.Thep, c, t - c - 6, bw - c, t - c - 6);
        double dr = Math.Max(r.MainD / 2.0, 0.12 * th);
        double sp = Math.Max(r.MainS * 1000, 50);
        for (double x = c + 10; x <= bw - c - 10 + 1e-6; x += sp) w.Dot(Lay.Thep, x, c + dr, dr);
        double spT = Math.Max(r.TopS * 1000, 50);
        for (double x = c + 10; x <= bw - c - 10 + 1e-6; x += spT) w.Dot(Lay.Thep, x, t - c - dr, Math.Max(r.TopD / 2.0, 0.12 * th));
        w.LeaderTag(bw * 0.3, c + dr, bw * 0.3 + 2 * th, -4 * th, Dt.Spaced(r.MainD, r.MainS), Mk(r, "lớp dưới (gấp khúc"), 1);
        w.LeaderTag(bw * 0.6, t - c - dr, bw * 0.6 + 2 * th, t + 4 * th, Dt.Spaced(r.TopD, r.TopS), Mk(r, "lớp trên (gấp khúc"), 1);
        w.LeaderTag(bw * 0.8, c + 8, bw + 2 * th, -2 * th, "Ø6a250", Mk(r, "phân bố"), 1);
        w.Dim(0, 0, bw, 0, -7 * th);
        w.Dim(bw, 0, bw, t, -(3 * th));
        w.Title(bw / 2, w.Extents.Y1 - 1.2 * th, "MẶT CẮT 1-1 " + r.Name, DrawUtil.ScaleText(SecScale), true);
        return w;
    }

    public static DxfWriter FishboneElevation(StairFormResult r, DetailSettings s)
    {
        var w = new DxfWriter(SecScale);
        double th = w.Th;
        double b = r.Tread * 1000, h = r.Riser * 1000, hs = r.StepH * 1000, sh = r.SpineH * 1000, cb = Math.Max(s.CoverBeam, 0.02) * 1000;
        int n = r.Risers;
        double run = (n - 1) * b, rise = n * h;
        double slope = rise / Math.Max(run + b, 1);
        // đường dưới các bậc (mặt trên dầm cốn) đi qua chân cổ bậc: y = slope·x
        double hv = sh / Math.Max(r.CosA, 0.3);
        double x0 = -200, x1 = run + 200;
        double Ytop(double x) => slope * (x + b / 2) - hs * 0.0;
        var spine = new List<(double X, double Y)> { (x0, Ytop(x0) - hs), (x1, Ytop(x1) - hs), (x1, Ytop(x1) - hs - hv), (x0, Ytop(x0) - hs - hv) };
        if (w.HatchOn) w.HatchMat(HatchMaterial.Concrete, spine);
        w.Polyline(Lay.BeTong, spine, true);
        // các bậc công xôn (mặt cắt dọc qua dầm: khối b × hs đặt trên dầm)
        for (int i = 0; i < n - 1; i++)
        {
            double xa = i * b, ytop = (i + 1) * h;
            w.Rect(Lay.BeTong, xa, ytop - hs, xa + b, ytop);
        }
        // thép dọc dầm cốn
        double yb = -hs - hv + cb + 10, yt = -hs - cb - 10;
        w.Line(Lay.Thep, x0 + 30, Ytop(x0) + yb, x1 - 30, Ytop(x1) + yb);
        w.Line(Lay.Thep, x0 + 30, Ytop(x0) + yt, x1 - 30, Ytop(x1) + yt);
        // đai: nét đứng (vuông góc trục ~ đứng)
        double ss = Math.Max(r.StirrupS * 1000, 50);
        for (double x = x0 + 50; x <= x1 - 50; x += ss)
            w.Line(Lay.Dai, x, Ytop(x) - hs - hv + cb, x, Ytop(x) - hs - cb);
        double xm = run * 0.45;
        w.LeaderTag(xm, Ytop(xm) + yb, xm + 3 * th, Ytop(xm) + yb - 6 * th, $"{r.SpineBotN}Ø{r.SpineBotD}", Mk(r, "dầm cốn giữa: thép lớp dưới"), 1);
        double xm2 = run * 0.25;
        w.LeaderTag(xm2, Ytop(xm2) + yt, xm2 - 3 * th, Ytop(xm2) + yt + 7 * th, $"{r.SpineTopN}Ø{r.SpineTopD}", Mk(r, "dầm cốn giữa: thép lớp trên"), -1);
        double xm3 = run * 0.65;
        w.LeaderTag(xm3 + ss / 2, Ytop(xm3) - hs - hv / 2, xm3 + 4 * th, Ytop(xm3) - hs - hv - 4 * th, "Ø8a" + Dt.Mm(r.StirrupS), Mk(r, "đai kín"), 1);
        var xs = new List<double>();
        for (int i = 0; i < n; i++) xs.Add(i * b);
        w.DimChainH(xs, n * h, n * h + 3 * th);
        w.Text(Lay.Chu, run / 2, Ytop(run / 2) - hs - hv - 6 * th, th, $"DẦM CỐN GIỮA {Dt.Mm(r.SpineB)}×{Dt.Mm(r.SpineH)} – BẬC CÔNG XÔN {(r.Sides == 2 ? "HAI BÊN" : "MỘT BÊN")} VƯƠN {Dt.Mm(r.Lc)}", 0, 1, 0);
        w.Title(run / 2, w.Extents.Y2 + 4 * th, "MẶT ĐỨNG DẦM CỐN " + r.Name, DrawUtil.ScaleText(SecScale));
        return w;
    }

    public static DxfWriter FishboneCross(StairFormResult r, DetailSettings s)
    {
        var w = new DxfWriter(SecScale);
        double th = w.Th;
        double sb = r.SpineB * 1000, sh = r.SpineH * 1000, hs = r.StepH * 1000, lc = r.Lc * 1000;
        double cs = Math.Max(s.CoverSlab, 0.015) * 1000, cb = Math.Max(s.CoverBeam, 0.02) * 1000;
        double xl = r.Sides == 2 ? -sb / 2 - lc : -sb / 2, xr = sb / 2 + lc;
        // dầm cốn (tiết diện) + bậc
        var poly = new List<(double X, double Y)> { (-sb / 2, 0), (sb / 2, 0), (sb / 2, sh), (xr, sh), (xr, sh + hs), (xl, sh + hs), (xl, sh), (-sb / 2, sh) };
        if (r.Sides == 1) poly = new List<(double X, double Y)> { (-sb / 2, 0), (sb / 2, 0), (sb / 2, sh), (xr, sh), (xr, sh + hs), (-sb / 2, sh + hs) };
        if (w.HatchOn) w.HatchMat(HatchMaterial.Concrete, poly);
        w.Polyline(Lay.BeTong, poly, true);
        // thép bậc lớp trên (móc xuống ở mút)
        double yT = sh + hs - cs - 6, hook = Math.Max(hs - 2 * cs, 40);
        var top = new List<(double X, double Y)>();
        if (r.Sides == 2) top.Add((xl + cs, yT - hook)); else top.Add((-sb / 2 + cb, yT - Math.Max(30 * r.StepBarD, sh - cb)));
        top.Add((r.Sides == 2 ? xl + cs : -sb / 2 + cb, yT)); top.Add((xr - cs, yT)); top.Add((xr - cs, yT - hook));
        w.Polyline(Lay.Thep, top, false, 0.35 * w.Scale);
        w.Line(Lay.Thep, xl + cs + 10, sh + cs + 6, xr - cs - 10, sh + cs + 6);
        // đai dầm cốn + thép dọc
        w.Rect(Lay.Dai, -sb / 2 + cb, cb, sb / 2 - cb, sh + hs - cs - 14);
        double dr = Math.Max(r.SpineBotD / 2.0, 0.12 * th);
        int nb = Math.Max(2, r.SpineBotN);
        for (int i = 0; i < nb; i++)
        {
            double x = -sb / 2 + cb + 8 + dr + i * (sb - 2 * cb - 16 - 2 * dr) / Math.Max(nb - 1, 1);
            w.Dot(Lay.Thep, x, cb + 8 + dr, dr);
        }
        double drt = Math.Max(r.SpineTopD / 2.0, 0.12 * th);
        int ntp = Math.Max(2, r.SpineTopN);
        for (int i = 0; i < ntp; i++)
        {
            double x = -sb / 2 + cb + 8 + drt + i * (sb - 2 * cb - 16 - 2 * drt) / Math.Max(ntp - 1, 1);
            w.Dot(Lay.Thep, x, sh - cb - 8 - drt, drt);
        }
        w.LeaderTag(xr - lc / 2, yT, xr - lc / 2 + 2 * th, sh + hs + 4 * th, $"{r.StepBarN}Ø{r.StepBarD}", Mk(r, "Bậc công xôn – thép chịu lực"), 1);
        w.LeaderTag(xr - lc / 3, sh + cs + 6, xr + 2 * th, sh - 3 * th, "2Ø8", Mk(r, "Bậc công xôn – thép cấu tạo"), 1);
        w.LeaderTag(0, cb + 8 + dr, sb / 2 + 3 * th, -4 * th, $"{r.SpineBotN}Ø{r.SpineBotD}", Mk(r, "dầm cốn giữa: thép lớp dưới"), 1);
        w.LeaderTag(-sb / 2 + cb, sh * 0.5, xl - 2 * th, sh * 0.3, "Ø8a" + Dt.Mm(r.StirrupS), Mk(r, "đai kín"), -1);
        w.DimChainH(r.Sides == 2 ? new List<double> { xl, -sb / 2, sb / 2, xr } : new List<double> { -sb / 2, sb / 2, xr }, 0, -7 * th);
        w.DimChainV(new List<double> { 0, sh, sh + hs }, xr, xr + 4 * th);
        w.Title((xl + xr) / 2, w.Extents.Y1 - 1.2 * th, "MẶT CẮT 1-1 BẬC CÔNG XÔN " + r.Name, DrawUtil.ScaleText(SecScale), true);
        return w;
    }

    public static DxfWriter SteelElevation(StairFormResult r)
    {
        var w = new DxfWriter(SecScale);
        double th = w.Th;
        double b = r.Tread * 1000, h = r.Riser * 1000, d = r.Shape.H;
        int n = r.Risers;
        double run = (n - 1) * b, rise = n * h;
        double slope = rise / Math.Max(run + b, 1);
        double dv = d / Math.Max(r.CosA, 0.3);
        double x0 = -100, x1 = run + 100;
        double Y(double x) => slope * (x + b / 2) - 60;
        w.Polyline(Lay.BeTong, new List<(double X, double Y)> { (x0, Y(x0)), (x1, Y(x1)), (x1, Y(x1) - dv), (x0, Y(x0) - dv) }, true);
        w.Line(Lay.KyHieu, x0, Y(x0) - r.Shape.Tf / Math.Max(r.CosA, 0.3), x1, Y(x1) - r.Shape.Tf / Math.Max(r.CosA, 0.3), "DASHED");
        w.Line(Lay.KyHieu, x0, Y(x0) - dv + r.Shape.Tf / Math.Max(r.CosA, 0.3), x1, Y(x1) - dv + r.Shape.Tf / Math.Max(r.CosA, 0.3), "DASHED");
        // bậc: thép góc + mặt bậc
        double leg = r.Angle.StartsWith("L63") ? 63 : r.Angle.StartsWith("L75") ? 75 : 50;
        for (int i = 0; i < n - 1; i++)
        {
            double xa = i * b + 10, ytop = (i + 1) * h;
            w.Rect(Lay.BeTong, xa, ytop - 5, xa + b - 20, ytop);
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xa, ytop - 5 - leg), (xa, ytop - 5), (xa + leg, ytop - 5) }, false);
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xa + b - 20, ytop - 5 - leg), (xa + b - 20, ytop - 5), (xa + b - 20 - leg, ytop - 5) }, false);
        }
        // bản mã chân / đầu
        w.Rect(Lay.Thep, x0 - 10, Y(x0) - dv - 10, x0, Y(x0) + 40);
        w.Rect(Lay.Thep, x1, Y(x1) - dv - 10, x1 + 10, Y(x1) + 40);
        double xm = run * 0.5;
        w.LeaderTag(xm, Y(xm) - dv / 2, xm + 3 * th, Y(xm) - dv - 5 * th, r.Shape.Name, 0, 1);
        double xa2 = 1 * b + 10;
        w.LeaderTag(xa2, 2 * h - 5 - leg / 2, xa2 - 3 * th, 2 * h + 5 * th, r.Angle + " đỡ bậc", 0, -1);
        var xs = new List<double>();
        for (int i = 0; i < n; i++) xs.Add(i * b);
        w.DimChainH(xs, n * h, n * h + 3 * th);
        var ys = new List<double>();
        for (int i = 0; i <= n; i += Math.Max(1, n / 6)) ys.Add(i * h);
        if (Math.Abs(ys[^1] - n * h) > 1e-6) ys.Add(n * h);
        w.DimChainV(ys, x1 + 10, x1 + 10 + 3 * th);
        w.Text(Lay.Chu, run / 2, Y(run / 2) - dv - 8 * th, th, $"2 CỐN {r.Shape.Name} ({r.Grade}) – {r.Risers} BẬC {Dt.Mm(r.Riser)}×{Dt.Mm(r.Tread)} – SƠN CHỐNG GỈ + 2 LỚP SƠN PHỦ", 0, 1, 0);
        w.Title(run / 2, w.Extents.Y2 + 4 * th, "MẶT ĐỨNG CỐN THÉP " + r.Name, DrawUtil.ScaleText(SecScale));
        return w;
    }

    private static void DrawShape(DxfWriter w, SteelShape sh, double x, double y, bool flip)
    {
        double hh = sh.H, bb = sh.B, tw = sh.Tw, tf = sh.Tf;
        int dir = flip ? -1 : 1;
        if (sh.Kind == "Hộp") { w.Rect(Lay.BeTong, x, y, x + dir * bb, y + hh); w.Rect(Lay.BeTong, x + dir * tw, y + tw, x + dir * (bb - tw), y + hh - tw); return; }
        if (sh.Kind == "U")
        {
            var p = new List<(double X, double Y)> { (x, y), (x + dir * bb, y), (x + dir * bb, y + tf), (x + dir * tw, y + tf), (x + dir * tw, y + hh - tf), (x + dir * bb, y + hh - tf), (x + dir * bb, y + hh), (x, y + hh) };
            w.Polyline(Lay.BeTong, p, true);
            return;
        }
        double cx = x + dir * bb / 2;
        var q = new List<(double X, double Y)>
        {
            (cx - bb / 2, y), (cx + bb / 2, y), (cx + bb / 2, y + tf), (cx + tw / 2, y + tf), (cx + tw / 2, y + hh - tf), (cx + bb / 2, y + hh - tf), (cx + bb / 2, y + hh),
            (cx - bb / 2, y + hh), (cx - bb / 2, y + hh - tf), (cx - tw / 2, y + hh - tf), (cx - tw / 2, y + tf), (cx - bb / 2, y + tf)
        };
        w.Polyline(Lay.BeTong, q, true);
    }

    public static DxfWriter SteelCross(StairFormResult r)
    {
        var w = new DxfWriter(SecScale / 2.5 > 10 ? 10 : 10);
        double th = w.Th;
        var sh = r.Shape;
        double bw = r.Width * 1000;
        // cốn trái (lưng U quay ra ngoài) và cốn phải
        DrawShape(w, sh, 0, 0, false);
        DrawShape(w, sh, bw, 0, true);
        double leg = r.Angle.StartsWith("L63") ? 63 : r.Angle.StartsWith("L75") ? 75 : 50;
        double t = leg / 10;
        double xa = sh.Kind == "U" ? sh.Tw : sh.B, xb = bw - (sh.Kind == "U" ? sh.Tw : sh.B);
        double yTop = sh.H - 20;
        // thép góc hai đầu (mặt cắt ngang qua bậc: thấy thép góc dọc theo bậc → vẽ chữ L tại hai cốn)
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xa, yTop - leg), (xa + t, yTop - leg), (xa + t, yTop - t), (xa + leg, yTop - t), (xa + leg, yTop), (xa, yTop) }, true);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xb, yTop - leg), (xb - t, yTop - leg), (xb - t, yTop - t), (xb - leg, yTop - t), (xb - leg, yTop), (xb, yTop) }, true);
        // mặt bậc
        w.Rect(Lay.BeTong, xa, yTop, xb, yTop + 5);
        // bu lông / đường hàn ký hiệu
        w.Text(Lay.Chu, bw / 2, yTop + 5 + 0.6 * th, 0.9 * th, "THÉP TẤM GÂN 5 / MẶT BẬC", 0, 1, 0);
        w.LeaderTag(xa + leg / 2, yTop - t / 2, xa + leg + 3 * th, yTop - leg - 4 * th, r.Angle + " – HÀN hf=5", 0, 1);
        w.LeaderTag(sh.B / 2, sh.H / 2, -3 * th, sh.H + 4 * th, sh.Name, 0, -1);
        w.Dim(0, 0, bw, 0, -5 * th);
        w.Dim(bw, 0, bw, sh.H, -(3 * th));
        w.Title(bw / 2, w.Extents.Y1 - 1.2 * th, "MẶT CẮT 1-1 THANG THÉP " + r.Name, DrawUtil.ScaleText(10), true);
        return w;
    }

    public static DxfWriter SteelBase(StairFormResult r)
    {
        var w = new DxfWriter(10);
        double th = w.Th;
        var sh = r.Shape;
        double pl = sh.H + 100, pw = 250;
        // mặt đứng bản mã (mặt bằng bản đế): hình chữ nhật, 2 lỗ bu lông, tiết diện cốn hàn vào
        w.Rect(Lay.Thep, 0, 0, pw, pl);
        DrawShape(w, sh, pw / 2 - sh.B / 2, 50, false);
        foreach (var (bx, by) in new[] { (40.0, pl / 2), (pw - 40.0, pl / 2) })
        {
            w.Circle(Lay.Thep, bx, by, 9);
            w.Line(Lay.Truc, bx - 15, by, bx + 15, by, "CENTER");
            w.Line(Lay.Truc, bx, by - 15, bx, by + 15, "CENTER");
        }
        w.LeaderTag(40, pl / 2 + 9, -3 * th, pl + 3 * th, "2 BU LÔNG NEO HOÁ CHẤT M16 – SÂU 125", 0, -1);
        w.LeaderTag(pw, pl * 0.15, pw + 3 * th, -3 * th, "BẢN MÃ 250×" + Dt.Mm(pl / 1000) + "×10", 0, 1);
        w.Text(Lay.Chu, pw / 2, -6 * th, 0.9 * th, "HÀN BAO QUANH hf = 6 mm – QUE E42", 0, 1, 0);
        w.Dim(0, 0, pw, 0, -3 * th);
        w.Dim(pw, 0, pw, pl, -(3 * th));
        w.Title(pw / 2, w.Extents.Y1 - 1.2 * th, "CHI TIẾT CHÂN CỐN " + r.Name, DrawUtil.ScaleText(10), true);
        return w;
    }

    /// <summary>Chi tiết bậc BTCT liền khối (bản thang thường): 3 bậc trên bản thang, thép bản, thép góc mũi bậc, thép chữ L.</summary>
    public static DxfWriter StepDetail(DetailResult r, double riser, double tread, double waist, int stepDia, DetailSettings s)
    {
        var w = new DxfWriter(10);
        double th = w.Th;
        double b = tread * 1000, h = riser * 1000, t = waist * 1000, c = Math.Max(s.CoverSlab, 0.015) * 1000;
        int n = 3;
        double len = Math.Sqrt(b * b + h * h), sinA = h / len, cosA = b / len;
        // mặt bậc
        var prof = new List<(double X, double Y)> { (0, 0) };
        for (int i = 0; i < n; i++) { prof.Add((i * b, (i + 1) * h)); prof.Add(((i + 1) * b, (i + 1) * h)); }
        // đáy bản thang: song song đường mũi bậc, cách t (vuông góc)
        double off = t / cosA;
        double Ys(double x) => x * h / b - off;
        var poly = new List<(double X, double Y)>(prof) { (n * b, Ys(n * b)), (0, Ys(0)) };
        if (w.HatchOn) w.HatchMat(HatchMaterial.Concrete, poly);
        w.Polyline(Lay.BeTong, poly, true);
        w.Line(Lay.KyHieu, 0, 0, n * b, n * h, "DASHED");
        // thép bản thang lớp dưới
        double cb = c / cosA;
        w.Line(Lay.Thep, 0, Ys(0) + cb + 5, n * b, Ys(n * b) + cb + 5);
        // thép góc mũi bậc + chữ L
        double dr = Math.Max(stepDia / 2.0, 0.12 * th);
        for (int i = 0; i < n; i++)
        {
            double xn = (i + 1) * b - c - dr, yn = (i + 1) * h - c - dr;
            w.Dot(Lay.Thep, xn, yn, dr);
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (i * b + c, (i + 1) * h - c), ((i + 1) * b - c, (i + 1) * h - c), ((i + 1) * b - c, i * h + h * 0.3) }, false);
        }
        w.LeaderTag((n - 1) * b + b - c - dr, n * h - c - dr, n * b + 3 * th, n * h + 3 * th, "1Ø" + stepDia + " / BẬC", Mk(r, "thép góc mũi bậc"), 1);
        w.LeaderTag(b * 0.5, 2 * h - c, -3 * th, 2 * h + 4 * th, "Ø6a200", Mk(r, "thép chữ L ôm mũi bậc"), -1);
        w.Dim(0, 0, b, 0, -3 * th);
        w.Dim(n * b, (n - 1) * h, n * b, n * h, -(3 * th));
        w.Text(Lay.Chu, n * b / 2, Ys(0) - 4 * th, 0.9 * th, "BẬC ĐỔ BTCT LIỀN KHỐI – CHIỀU DÀY TÍNH TOÁN = BẢN THANG " + Dt.Mm(waist), 0, 1, 0);
        w.Title(n * b / 2, w.Extents.Y1 - 1.2 * th, "CHI TIẾT BẬC BTCT", DrawUtil.ScaleText(10), true);
        _ = sinA;
        return w;
    }
}
