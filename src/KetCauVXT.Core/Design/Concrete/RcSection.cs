using System.Globalization;
using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

// =====================================================================
// CÔNG THỨC TIẾT DIỆN BTCT THEO TCVN 5574:2018
// Đơn vị nội bộ: kN, m, kN/m²; đường kính thép int (mm).
// =====================================================================

/// <summary>Kết quả tính cốt thép chịu uốn tiết diện chữ nhật (TCVN 5574:2018 mục 8.1.2.2).</summary>
public struct FlexureResult
{
    /// <summary>Mô men thiết kế (trị tuyệt đối), kN.m</summary>
    public double M;
    /// <summary>Diện tích cốt chịu kéo yêu cầu (đã kể hàm lượng tối thiểu), m²</summary>
    public double As;
    /// <summary>Diện tích cốt chịu nén yêu cầu (cốt kép), m²</summary>
    public double AsComp;
    public double AlphaM;
    public double AlphaR;
    public double Xi;
    public double XiR;
    /// <summary>Diện tích cốt thép tối thiểu μmin·b·h0, m²</summary>
    public double AsMin;
    public bool Doubly;
    public bool Ok;
    public string Note;
}

/// <summary>Kết quả tính cốt đai chịu cắt (TCVN 5574:2018 mục 8.1.3.3).</summary>
public struct ShearResult
{
    public double Q;
    /// <summary>Khả năng chịu nén của dải bê tông giữa các vết nứt nghiêng 0,3·Rb·b·h0 (8.1.3.2)</summary>
    public double QStrut;
    /// <summary>Khả năng chịu cắt của bê tông Qb với c = 2h0</summary>
    public double Qb;
    /// <summary>0,5·Rbt·b·h0 – dưới giá trị này đai đặt theo cấu tạo</summary>
    public double QbMin;
    /// <summary>Lực cắt đai yêu cầu trên một đơn vị dài qsw, kN/m</summary>
    public double QswReq;
    /// <summary>Bước đai theo tính toán Rsw·Asw/qsw (m), +∞ nếu đặt cấu tạo</summary>
    public double SReq;
    public double SSupport;
    public double SMid;
    /// <summary>Khả năng chịu cắt với bước đai vùng gối đã chọn, kN</summary>
    public double Capacity;
    public int Dia;
    public int Legs;
    public bool Constructive;
    public bool Ok;
    public string Note;
}

/// <summary>Một tổ hợp thanh thép dọc, ví dụ 2Ø18+1Ø16.</summary>
public class BarSet
{
    public List<(int Dia, int Count)> Groups { get; set; } = new();
    public int Layers { get; set; } = 1;
    public bool Ok { get; set; } = true;
    public double Area => Groups.Sum(g => g.Count * RcSection.BarArea(g.Dia));
    public int Count => Groups.Sum(g => g.Count);
    public int MaxDia => Groups.Count == 0 ? 0 : Groups.Max(g => g.Dia);
    public bool IsEmpty => Count == 0;
    public string Text => RcSection.BarText(Groups);
    public override string ToString() => Text;
}

/// <summary>Thép rải theo bước (sàn, móng): Ø{d}a{s}.</summary>
public class SpacingChoice
{
    public int Dia { get; set; }
    /// <summary>Bước thép, m</summary>
    public double Spacing { get; set; }
    /// <summary>Diện tích yêu cầu trên 1 m bề rộng, m²/m</summary>
    public double AsReqPerM { get; set; }
    public bool Ok { get; set; } = true;
    /// <summary>Diện tích bố trí trên 1 m bề rộng, m²/m</summary>
    public double AsPerM => Spacing > 0 ? RcSection.BarArea(Dia) / Spacing : 0;
    public string Text => "Ø" + Dia.ToString(CultureInfo.InvariantCulture) + "a" + Math.Round(Spacing * 1000).ToString("0", CultureInfo.InvariantCulture);
    public override string ToString() => Text;
}

/// <summary>Bố trí thép dọc đối xứng trên chu vi tiết diện cột chữ nhật.
/// Toạ độ thanh: X theo trục địa phương 3 (cạnh B), Y theo trục địa phương 2 (cạnh H), gốc tại trọng tâm.</summary>
public class ColumnLayout
{
    public double B { get; set; }
    public double H { get; set; }
    /// <summary>Khoảng cách từ mép tiết diện tới tâm thanh, m</summary>
    public double A { get; set; }
    public int Dia { get; set; }
    /// <summary>Số thanh trên mỗi cạnh b (song song trục 3, kể cả 2 thanh góc)</summary>
    public int NB { get; set; }
    /// <summary>Số thanh trên mỗi cạnh h (song song trục 2, kể cả 2 thanh góc)</summary>
    public int NH { get; set; }
    public List<(double X, double Y, double Area)> Bars { get; set; } = new();
    public double As => Bars.Sum(b => b.Area);
    public int Count => Bars.Count;

    /// <summary>Tạo bố trí nb thanh trên mỗi cạnh b và nh thanh trên mỗi cạnh h (tổng 2nb + 2(nh−2) thanh).
    /// areaScale ≠ 1 dùng để dò diện tích thép yêu cầu.</summary>
    public static ColumnLayout Create(double b, double h, double a, int dia, int nb, int nh, double areaScale = 1.0)
    {
        nb = Math.Max(nb, 2); nh = Math.Max(nh, 2);
        var l = new ColumnLayout { B = b, H = h, A = a, Dia = dia, NB = nb, NH = nh };
        double ab = RcSection.BarArea(dia) * areaScale;
        double xe = b / 2 - a, ye = h / 2 - a;
        for (int i = 0; i < nb; i++)
        {
            double x = -xe + 2 * xe * i / (nb - 1);
            l.Bars.Add((x, ye, ab));
            l.Bars.Add((x, -ye, ab));
        }
        for (int j = 1; j < nh - 1; j++)
        {
            double y = -ye + 2 * ye * j / (nh - 1);
            l.Bars.Add((xe, y, ab));
            l.Bars.Add((-xe, y, ab));
        }
        return l;
    }

    public override string ToString() => $"{Count}Ø{Dia} ({NB}×{NH})";
}

/// <summary>Các công thức tính toán tiết diện BTCT theo TCVN 5574:2018.</summary>
public static class RcSection
{
    /// <summary>Biến dạng tới hạn của bê tông chịu nén εb2 = 0,0035 (TCVN 5574:2018 mục 6.1.4.2, tác dụng ngắn hạn).</summary>
    public const double EpsB2 = 0.0035;
    /// <summary>Mô đun đàn hồi cốt thép mặc định Es = 2·10⁵ MPa, kN/m²</summary>
    public const double EsDefault = 2.0e8;

    // ------------------------------------------------------------------
    // Uốn tiết diện chữ nhật
    // ------------------------------------------------------------------

    /// <summary>Chiều cao vùng nén giới hạn tương đối ξR = 0,8 / (1 + εs,el/εb2), εs,el = Rs/Es
    /// (TCVN 5574:2018 mục 8.1.2.2.3, công thức 8.1).</summary>
    public static double XiR(double rs, double es = EsDefault)
    {
        if (es <= 0) es = EsDefault;
        return 0.8 / (1 + (rs / es) / EpsB2);
    }

    /// <summary>αR = ξR·(1 − ξR/2) (TCVN 5574:2018 mục 8.1.2.2).</summary>
    public static double AlphaR(double xiR) => xiR * (1 - xiR / 2);

    /// <summary>Tính cốt thép tiết diện chữ nhật chịu uốn (TCVN 5574:2018 mục 8.1.2.2.4–8.1.2.2.5):
    /// αm = M/(Rb·b·h0²); αm ≤ αR: As = Rb·b·h0·(1 − √(1 − 2αm))/Rs;
    /// αm &gt; αR (cốt kép): As' = (M − αR·Rb·b·h0²)/(Rsc·(h0 − a')), As = (ξR·Rb·b·h0 + Rsc·As')/Rs.
    /// Kết quả As không nhỏ hơn μmin·b·h0.</summary>
    /// <param name="M">Mô men (lấy trị tuyệt đối), kN.m</param>
    /// <param name="minRatioPct">Hàm lượng tối thiểu (%)</param>
    public static FlexureResult DesignRect(double M, double b, double h0, double aComp, Material conc, Material steel, double minRatioPct)
    {
        double m = Math.Abs(M);
        var r = new FlexureResult { M = m, Ok = true, Note = "" };
        r.XiR = XiR(steel.Rs, steel.E);
        r.AlphaR = AlphaR(r.XiR);
        r.AsMin = Math.Max(minRatioPct, 0) / 100.0 * b * h0;
        if (b <= 0 || h0 <= 0)
        {
            r.Ok = false; r.Note = "LỖI kích thước tiết diện không hợp lệ";
            return r;
        }
        if (m < 1e-9) { r.As = r.AsMin; return r; }

        r.AlphaM = m / (conc.Rb * b * h0 * h0);
        if (r.AlphaM <= r.AlphaR)
        {
            r.Xi = 1 - Math.Sqrt(1 - 2 * r.AlphaM);
            r.As = conc.Rb * b * h0 * r.Xi / steel.Rs;
        }
        else
        {
            r.Doubly = true;
            r.Xi = r.XiR;
            double z = h0 - aComp;
            if (z <= 1e-6)
            {
                r.Ok = false; r.Note = "LỖI không tính được cốt kép (h0 ≤ a')";
                return r;
            }
            r.AsComp = (m - r.AlphaR * conc.Rb * b * h0 * h0) / (steel.Rsc * z);
            r.As = (r.XiR * conc.Rb * b * h0 + steel.Rsc * r.AsComp) / steel.Rs;
            r.Note = "αm = " + r.AlphaM.ToString("0.000", CultureInfo.InvariantCulture) + " > αR = "
                     + r.AlphaR.ToString("0.000", CultureInfo.InvariantCulture) + ": tính cốt kép";
            if (r.AsComp > 0.5 * r.As) r.Note += " – nên tăng tiết diện";
        }
        r.As = Math.Max(r.As, r.AsMin);
        return r;
    }

    /// <summary>Khả năng chịu mô men của tiết diện chữ nhật đã có As, As' (TCVN 5574:2018 mục 8.1.2.2.4):
    /// x = (Rs·As − Rsc·As')/(Rb·b); nếu 2a' ≤ x ≤ ξR·h0: Mu = Rb·b·x·(h0 − x/2) + Rsc·As'·(h0 − a');
    /// x &gt; ξR·h0 lấy x = ξR·h0; x &lt; 2a': Mu = Rs·As·(h0 − a') (hoặc bỏ qua As' nếu lớn hơn).</summary>
    public static double MomentCapacity(double b, double h0, double As, double AsComp, double aComp, Material conc, Material steel)
    {
        if (As <= 0 || b <= 0 || h0 <= 0) return 0;
        double xiR = XiR(steel.Rs, steel.E);
        double rb = conc.Rb;
        double x = (steel.Rs * As - steel.Rsc * AsComp) / (rb * b);
        double mu;
        if (x >= 2 * aComp)
        {
            x = Math.Min(x, xiR * h0);
            mu = rb * b * x * (h0 - x / 2) + steel.Rsc * AsComp * (h0 - aComp);
        }
        else
        {
            double m1 = steel.Rs * As * (h0 - aComp);
            double x1 = Math.Min(steel.Rs * As / (rb * b), xiR * h0);
            double m2 = rb * b * x1 * (h0 - x1 / 2);
            mu = Math.Max(m1, m2);
        }
        return Math.Max(mu, 0);
    }

    // ------------------------------------------------------------------
    // Cắt
    // ------------------------------------------------------------------

    /// <summary>Tính cốt đai chịu cắt, đơn giản hoá với tiết diện nghiêng c = c0 = 2h0 (TCVN 5574:2018 mục 8.1.3.2–8.1.3.3):
    /// <list type="bullet">
    /// <item>Dải nén: Q ≤ 0,3·Rb·b·h0 (8.1.3.2).</item>
    /// <item>Qb = 1,5·Rbt·b·h0²/c = 0,75·Rbt·b·h0, giới hạn 0,5…2,5·Rbt·b·h0 (8.1.3.3.2).</item>
    /// <item>Q ≤ 0,5·Rbt·b·h0: đai cấu tạo; ngược lại qsw = max((Q − Qb)/(1,5·h0); 0,25·Rbt·b) vì Qsw = 0,75·qsw·c0.</item>
    /// <item>s = Rsw·Asw/qsw; vùng gối s ≤ min(h0/2; 300 mm) và s ≤ Rbt·b·h0²/Q; giữa nhịp s ≤ min(0,75h0; 500 mm) (mục 10.3.4).</item>
    /// <item>Bước đai làm tròn xuống bội số 50 mm, tối thiểu 100 mm.</item>
    /// </list></summary>
    public static ShearResult Shear(double Q, double b, double h0, Material conc, Material stirrup, int dia, int legs = 2)
    {
        Q = Math.Abs(Q);
        var r = new ShearResult { Q = Q, Dia = dia, Legs = legs, Note = "" };
        double rbt = conc.Rbt;
        r.QStrut = 0.3 * conc.Rb * b * h0;
        r.Qb = QbConcrete(b, h0, conc);
        r.QbMin = 0.5 * rbt * b * h0;
        double asw = legs * BarArea(dia);

        double sSupMax = Math.Min(h0 / 2, 0.30);
        if (Q > 1e-9) sSupMax = Math.Min(sSupMax, rbt * b * h0 * h0 / Q);
        double sMidMax = Math.Min(0.75 * h0, 0.50);

        if (Q <= r.QbMin)
        {
            r.Constructive = true;
            r.QswReq = 0;
            r.SReq = double.PositiveInfinity;
        }
        else
        {
            r.QswReq = Math.Max((Q - r.Qb) / (1.5 * h0), 0.25 * rbt * b);
            r.SReq = stirrup.Rsw * asw / r.QswReq;
        }
        double s = FloorTo(Math.Min(r.SReq, sSupMax), 0.05);
        bool spacingOk = s >= 0.10 - 1e-9;
        r.SSupport = Math.Max(s, 0.10);
        r.SMid = Math.Max(Math.Max(FloorTo(sMidMax, 0.05), r.SSupport), 0.10);
        r.Capacity = ShearCapacity(b, h0, conc, stirrup, dia, legs, r.SSupport);

        bool strutOk = Q <= r.QStrut + 1e-9;
        r.Ok = strutOk && spacingOk;
        if (!strutOk)
            r.Note = "Q = " + Q.ToString("0.0", CultureInfo.InvariantCulture) + " kN > 0,3·Rb·b·h0 = "
                     + r.QStrut.ToString("0.0", CultureInfo.InvariantCulture) + " kN: dải bê tông giữa các vết nứt nghiêng bị phá hoại – cần tăng tiết diện";
        else if (!spacingOk)
            r.Note = "Bước đai Ø" + dia + " yêu cầu " + (r.SReq * 1000).ToString("0", CultureInfo.InvariantCulture)
                     + " mm < 100 mm – cần tăng đường kính hoặc số nhánh đai";
        else if (r.Constructive)
            r.Note = "Q ≤ 0,5·Rbt·b·h0: đai đặt theo cấu tạo";
        return r;
    }

    /// <summary>Khả năng chịu cắt của bê tông Qb = 1,5·Rbt·b·h0²/c với c = 2h0, giới hạn 0,5…2,5·Rbt·b·h0 (TCVN 5574:2018 8.1.3.3.2).</summary>
    public static double QbConcrete(double b, double h0, Material conc)
    {
        double c = 2 * h0;
        double qb = 1.5 * conc.Rbt * b * h0 * h0 / c;
        return Math.Clamp(qb, 0.5 * conc.Rbt * b * h0, 2.5 * conc.Rbt * b * h0);
    }

    /// <summary>Khả năng chịu cắt Qu = min(Qb + 0,75·qsw·c0; 0,3·Rb·b·h0) với c0 = 2h0, qsw = Rsw·Asw/s.
    /// Cốt đai chỉ được kể khi qsw ≥ 0,25·Rbt·b (TCVN 5574:2018 8.1.3.3.2).</summary>
    public static double ShearCapacity(double b, double h0, Material conc, Material stirrup, int dia, int legs, double s)
    {
        if (s <= 0 || b <= 0 || h0 <= 0) return 0;
        double qsw = stirrup.Rsw * legs * BarArea(dia) / s;
        if (qsw < 0.25 * conc.Rbt * b) qsw = 0;
        double qu = QbConcrete(b, h0, conc) + 0.75 * qsw * 2 * h0;
        return Math.Min(qu, 0.3 * conc.Rb * b * h0);
    }

    // ------------------------------------------------------------------
    // Cột – nén lệch tâm
    // ------------------------------------------------------------------

    /// <summary>Độ lệch tâm ngẫu nhiên ea = max(L/600; h/30; 10 mm) (TCVN 5574:2018 mục 8.1.2.4 / 4.2.6 bản 2012).</summary>
    public static double Ea(double L, double h) => Math.Max(Math.Max(L / 600.0, h / 30.0), 0.010);

    /// <summary>Khả năng chịu mô men Mu của tiết diện chữ nhật ứng với lực dọc N (nén dương), uốn một phương.
    /// Mô hình tương thích biến dạng: khối ứng suất chữ nhật cao 0,8x với ứng suất Rb, εb2 = 0,0035 tại thớ nén,
    /// ứng suất cốt thép σs = Es·εs giới hạn trong [−Rs; Rsc] (gần đúng theo mô hình biến dạng TCVN 5574:2018 mục 8.1.2.7 / 8.1.2.3).
    /// Vị trí trục trung hoà x được dò bằng chia đôi sao cho nội lực dọc cân bằng N. Trả về 0 nếu N nằm ngoài miền chịu lực.</summary>
    /// <param name="width">Bề rộng vuông góc phương uốn, m</param>
    /// <param name="depth">Chiều cao tiết diện theo phương uốn, m</param>
    /// <param name="bars">(khoảng cách từ thớ chịu nén tới tâm thanh, diện tích) – m, m²</param>
    public static double UniaxialCapacity(double N, double width, double depth, IReadOnlyList<(double D, double A)> bars, Material conc, Material steel)
    {
        double es = steel.E > 0 ? steel.E : EsDefault;
        double rb = conc.Rb;

        double Force(double x, out double m)
        {
            double a = Math.Min(0.8 * x, depth);
            double cc = rb * width * a;
            double n = cc;
            m = cc * (depth / 2 - a / 2);
            for (int i = 0; i < bars.Count; i++)
            {
                double d = bars[i].D, ai = bars[i].A;
                double eps = EpsB2 * (x - d) / x;
                double s = Math.Clamp(es * eps, -steel.Rs, steel.Rsc);
                if (s > 0 && d < a) s -= rb;   // trừ phần bê tông bị thép chiếm chỗ trong vùng nén
                n += s * ai;
                m += s * ai * (depth / 2 - d);
            }
            return n;
        }

        double lo = 1e-7, hi = 50 * depth;
        double nLo = Force(lo, out _), nHi = Force(hi, out _);
        if (N <= nLo || N >= nHi) return 0;
        double mm = 0;
        for (int it = 0; it < 100; it++)
        {
            double mid = 0.5 * (lo + hi);
            double n = Force(mid, out mm);
            if (n < N) lo = mid; else hi = mid;
            if (hi - lo < 1e-7 * depth) break;
        }
        Force(0.5 * (lo + hi), out mm);
        return Math.Abs(mm);
    }

    /// <summary>Lực nén giới hạn N0 = Rb·A + Rsc·As (nén đúng tâm, chưa kể uốn dọc).</summary>
    public static double N0(ColumnLayout lay, Material conc, Material steel) => conc.Rb * lay.B * lay.H + steel.Rsc * lay.As;

    /// <summary>Kiểm tra nén lệch tâm xiên bằng đường bao tải trọng (load contour, Bresler):
    /// (M2/Mu2)^α + (M3/Mu3)^α ≤ 1 với α = 1 + N/N0 giới hạn [1; 2], N0 = Rb·A + Rsc·As.
    /// Mu2, Mu3 tính theo <see cref="UniaxialCapacity"/> tại cùng N. M3 uốn quanh trục 3 (chiều cao H), M2 quanh trục 2 (chiều cao B).
    /// Trả về hệ số sử dụng (≤ 1 là đạt), 99 nếu N vượt khả năng chịu lực dọc.</summary>
    public static double BiaxialUtilization(double N, double M2, double M3, ColumnLayout lay, Material conc, Material steel, out double mu2, out double mu3)
    {
        var b3 = new List<(double D, double A)>(lay.Bars.Count);
        var b2 = new List<(double D, double A)>(lay.Bars.Count);
        foreach (var b in lay.Bars)
        {
            b3.Add((lay.H / 2 - b.Y, b.Area));
            b2.Add((lay.B / 2 - b.X, b.Area));
        }
        mu3 = UniaxialCapacity(N, lay.B, lay.H, b3, conc, steel);
        mu2 = UniaxialCapacity(N, lay.H, lay.B, b2, conc, steel);
        if (mu2 <= 1e-9 || mu3 <= 1e-9) return 99;
        double n0 = N0(lay, conc, steel);
        double alpha = Math.Clamp(1 + N / n0, 1.0, 2.0);
        return Math.Pow(Math.Abs(M2) / mu2, alpha) + Math.Pow(Math.Abs(M3) / mu3, alpha);
    }

    /// <summary>Hệ số uốn dọc η = 1/(1 − N/Ncr) (TCVN 5574:2018 mục 8.1.2.4):
    /// chỉ xét khi l0/i &gt; 14; Ncr = π²·D/l0², D = kb·Eb·I + ks·Es·Is, kb = 0,15/(φL·(0,3 + δe)), ks = 0,7,
    /// δe = e0/h giới hạn [0,15; 1,5]. Trả về +∞ khi N ≥ Ncr (cột mất ổn định).</summary>
    /// <param name="lambda">Độ mảnh l0/i theo phương đang xét</param>
    /// <param name="phiL">Hệ số kể đến tác dụng dài hạn φL = 1 + Ml/M (lấy 2,0 khi chưa tách tải dài hạn – thiên về an toàn)</param>
    public static double Eta(double N, double M, double l0, double h, double I, double Is, double lambda, Material conc, Material steel, double phiL, out double ncr)
    {
        ncr = double.PositiveInfinity;
        if (N <= 0 || l0 <= 0 || lambda <= 14) return 1.0;
        double e0 = Math.Abs(M) / N;
        double de = Math.Clamp(e0 / h, 0.15, 1.5);
        double kb = 0.15 / (Math.Max(phiL, 1.0) * (0.3 + de));
        double es = steel.E > 0 ? steel.E : EsDefault;
        double D = kb * conc.E * I + 0.7 * es * Is;
        ncr = Math.PI * Math.PI * D / (l0 * l0);
        if (N >= ncr) return double.PositiveInfinity;
        return 1.0 / (1.0 - N / ncr);
    }

    // ------------------------------------------------------------------
    // Chọn thép
    // ------------------------------------------------------------------

    /// <summary>Diện tích một thanh Ø d (mm), m².</summary>
    public static double BarArea(int d) => Math.PI * d * d / 4.0 * 1e-6;

    /// <summary>Số thanh tối đa trên một hàng: khoảng hở giữa các thanh ≥ max(25 mm; d) (TCVN 5574:2018 mục 10.3.5),
    /// bề rộng bố trí = b − 2·lớp bảo vệ (lớp bảo vệ tính tới mép cốt dọc).</summary>
    public static int MaxBarsInLayer(double width, double cover, int d)
    {
        double dm = d / 1000.0;
        double s = Math.Max(0.025, dm);
        double avail = width - 2 * cover;
        if (avail < dm) return 0;
        return Math.Max((int)Math.Floor((avail + s) / (dm + s) + 1e-9), 0);
    }

    /// <summary>Chọn thép dọc cho diện tích yêu cầu: tối đa 2 loại đường kính (chênh nhau ≤ 6 mm), ≥ minCount thanh,
    /// lọt bề rộng với khoảng hở ≥ max(25 mm; d), tối đa maxLayers hàng. Ưu tiên diện tích thừa ít, ít hàng, ít thanh.</summary>
    public static BarSet ChooseBars(double asReq, double width, double cover, int[] dias, int minCount = 2, int maxLayers = 2)
    {
        var ds = dias.Where(x => x > 0).Distinct().OrderBy(x => x).ToArray();
        BarSet best = null;
        double bestScore = double.MaxValue;

        void Consider(List<(int Dia, int Count)> groups)
        {
            int total = groups.Sum(g => g.Count);
            int dmax = groups.Max(g => g.Dia);
            int perLayer = MaxBarsInLayer(width, cover, dmax);
            if (perLayer < Math.Min(2, total)) return;
            int layers = (int)Math.Ceiling(total / (double)Math.Max(perLayer, 1));
            if (layers > maxLayers) return;
            double area = groups.Sum(g => g.Count * BarArea(g.Dia));
            if (area < asReq * (1 - 1e-6)) return;
            double score = area * (1 + 0.04 * (layers - 1) + 0.005 * total + 0.02 * (groups.Count - 1));
            if (score < bestScore)
            {
                bestScore = score;
                best = new BarSet { Groups = groups, Layers = layers, Ok = true };
            }
        }

        foreach (int d1 in ds)
        {
            int nmax = Math.Max(MaxBarsInLayer(width, cover, d1) * maxLayers, minCount);
            for (int n1 = Math.Max(minCount, 1); n1 <= nmax; n1++)
            {
                Consider(new List<(int, int)> { (d1, n1) });
                if (n1 < 2) continue;
                foreach (int d2 in ds)
                {
                    if (d2 >= d1 || d1 - d2 > 6) continue;
                    for (int n2 = 1; n1 + n2 <= nmax; n2++)
                        Consider(new List<(int, int)> { (d1, n1), (d2, n2) });
                }
            }
        }
        if (best != null) return best;
        int dl = ds.Length > 0 ? ds[^1] : 25;
        int nf = Math.Max(MaxBarsInLayer(width, cover, dl) * maxLayers, minCount);
        return new BarSet { Groups = new List<(int, int)> { (dl, nf) }, Layers = maxLayers, Ok = false };
    }

    /// <summary>Chọn thép bổ sung (một loại Ø) đặt cùng hàng với existingCount thanh Ø existingDia đã có
    /// (thép tăng cường, mũ gối). Ưu tiên cùng đường kính, ít hàng, ít thanh.</summary>
    public static BarSet ChooseExtra(double asReq, int existingCount, int existingDia, double width, double cover, int[] dias, int maxLayers = 2)
    {
        if (asReq <= 1e-12) return new BarSet();
        var ds = dias.Where(x => x > 0).Distinct().OrderBy(x => x).ToArray();
        BarSet best = null;
        double bestScore = double.MaxValue;
        foreach (int d in ds)
        {
            int perLayer = MaxBarsInLayer(width, cover, Math.Max(d, existingDia));
            if (perLayer <= 0) continue;
            for (int n = 1; existingCount + n <= perLayer * maxLayers; n++)
            {
                double area = n * BarArea(d);
                if (area < asReq * (1 - 1e-6)) continue;
                int layers = (int)Math.Ceiling((existingCount + n) / (double)perLayer);
                double score = area * (1 + 0.04 * (layers - 1) + 0.005 * n + (d == existingDia ? 0 : 0.01));
                if (score < bestScore)
                {
                    bestScore = score;
                    best = new BarSet { Groups = new List<(int, int)> { (d, n) }, Layers = layers, Ok = true };
                }
                break;   // n nhỏ nhất đạt yêu cầu với Ø này
            }
        }
        if (best != null) return best;
        int dl = ds.Length > 0 ? ds[^1] : 25;
        int pl = Math.Max(MaxBarsInLayer(width, cover, Math.Max(dl, existingDia)), 1);
        int nf = Math.Max(pl * maxLayers - existingCount, 1);
        return new BarSet { Groups = new List<(int, int)> { (dl, nf) }, Layers = maxLayers, Ok = false };
    }

    /// <summary>Chọn Ø và bước thép rải cho diện tích yêu cầu trên 1 m (sàn, móng). Bước làm tròn xuống 10 mm,
    /// trong khoảng [sMin; sMax]. Duyệt Ø tăng dần, nhận Ø đầu tiên cho bước ≥ sPref; nếu không có, lấy Ø lớn nhất còn đạt.</summary>
    /// <param name="asReqPerM">Hàm trả về As yêu cầu (m²/m) theo Ø (vì h0 phụ thuộc Ø)</param>
    public static SpacingChoice ChooseSpacing(Func<int, double> asReqPerM, int[] dias, double sMax, double sMin = 0.10, double sPref = 0.15, int minDia = 0)
    {
        var all = dias.Where(x => x > 0).Distinct().OrderBy(x => x).ToArray();
        var ds = all.Where(x => x >= minDia).ToArray();
        if (ds.Length == 0) ds = all;
        if (ds.Length == 0) ds = new[] { 8 };
        SpacingChoice last = null;
        foreach (int d in ds)
        {
            double req = asReqPerM(d);
            double s = req > 1e-12 ? FloorTo(BarArea(d) / req, 0.01) : sMax;
            s = Math.Round(Math.Min(s, sMax), 3);
            if (s < sMin - 1e-9) continue;
            var c = new SpacingChoice { Dia = d, Spacing = s, AsReqPerM = req, Ok = true };
            if (s >= sPref - 1e-9) return c;
            last = c;
        }
        if (last != null) return last;
        int dm = ds[^1];
        return new SpacingChoice { Dia = dm, Spacing = sMin, AsReqPerM = asReqPerM(dm), Ok = false };
    }

    /// <summary>Chuỗi thép kiểu "2Ø18+1Ø16" (Ø lớn trước).</summary>
    public static string BarText(IEnumerable<(int Dia, int Count)> groups)
    {
        if (groups == null) return "";
        var parts = groups.Where(g => g.Count > 0).GroupBy(g => g.Dia).OrderByDescending(g => g.Key)
            .Select(g => g.Sum(x => x.Count).ToString(CultureInfo.InvariantCulture) + "Ø" + g.Key.ToString(CultureInfo.InvariantCulture));
        return string.Join("+", parts);
    }

    /// <summary>Làm tròn xuống bội số step (khử sai số dấu phẩy động).</summary>
    public static double FloorTo(double v, double step)
    {
        if (double.IsInfinity(v) || double.IsNaN(v)) return v;
        return Math.Round(Math.Floor(v / step + 1e-9) * step, 6);
    }

    /// <summary>Làm tròn lên bội số step.</summary>
    public static double CeilTo(double v, double step) => Math.Round(Math.Ceiling(v / step - 1e-9) * step, 6);
}

/// <summary>Tiện ích dùng chung cho các module thiết kế BTCT.</summary>
internal static class DesignUtil
{
    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Bê tông của tiết diện; nếu vật liệu tiết diện không phải bê tông thì dùng cấp bê tông mặc định trong thông số thiết kế.</summary>
    public static Material ConcreteOf(Project p, string name, string fallback)
    {
        try
        {
            var m = p.Material(name);
            if (m.Kind == MaterialKind.Concrete && m.Rb > 0) return m;
        }
        catch (KeyNotFoundException) { }
        return p.Material(fallback);
    }

    public static int[] Sorted(int[] a, int[] fallback)
    {
        var src = a == null || a.Length == 0 ? fallback : a;
        var r = src.Where(x => x > 0).Distinct().OrderBy(x => x).ToArray();
        return r.Length > 0 ? r : fallback;
    }

    /// <summary>Làm tròn chiều dài tới 10 mm (m).</summary>
    public static double R10(double m) => Math.Round(m * 100.0, MidpointRounding.AwayFromZero) / 100.0;

    public static double Mm(double m) => Math.Round(m * 1000.0);

    public static string F(double v, string fmt) => v.ToString(fmt, Inv);

    /// <summary>Chiều dài đai kín gồm 2 móc 135°, mỗi móc max(75 mm; 10d). s1, s2 (m) là 2 cạnh đai.</summary>
    public static double StirrupLength(double s1, double s2, int d) =>
        R10(2 * (s1 + s2) + 2 * Math.Max(0.075, 10 * d / 1000.0));

    /// <summary>Chiều dài đai C (thanh giằng 1 nhánh) gồm 2 móc 135°.</summary>
    public static double TieLength(double s, int d) => R10(s + 2 * Math.Max(0.075, 10 * d / 1000.0));

    public static string NearestGridName(IEnumerable<GridLine> grids, double c, double tol = 0.3)
    {
        GridLine best = null;
        foreach (var g in grids)
            if (best == null || Math.Abs(g.Coord - c) < Math.Abs(best.Coord - c)) best = g;
        return best != null && Math.Abs(best.Coord - c) <= tol ? best.Name : null;
    }

    /// <summary>Tên giao trục "2-B"; nếu không có trục gần thì ghi toạ độ.</summary>
    public static string GridName(Project p, double x, double y)
    {
        string gx = NearestGridName(p.GridsX, x) ?? ("x" + F(x, "0.00"));
        string gy = NearestGridName(p.GridsY, y) ?? ("y" + F(y, "0.00"));
        return gx + "-" + gy;
    }

    /// <summary>Số tầng dùng trong tên cấu kiện: "T2" → "2", tên khác giữ nguyên.</summary>
    public static string StoryNo(string story)
    {
        if (string.IsNullOrEmpty(story)) return "";
        if (story.Length > 1 && (story[0] == 'T' || story[0] == 't') && story.Skip(1).All(char.IsDigit)) return story.Substring(1);
        return story;
    }

    public static int StoryOrder(Project p, string story)
    {
        var list = p.Stories.OrderBy(s => s.Elevation).ToList();
        int i = list.FindIndex(s => s.Name == story);
        return i < 0 ? int.MaxValue : i;
    }

    /// <summary>Trục địa phương 2 của thanh (hệ tổng thể).</summary>
    public static Vec3 Axis2(Project p, FrameElement f)
    {
        var R = ElementMatrices.LocalAxes(p.Node(f.NodeI).P, p.Node(f.NodeJ).P, f.Angle);
        return new Vec3(R[1, 0], R[1, 1], R[1, 2]);
    }

    /// <summary>Kích thước cột theo phương dir (mặt bằng): H nếu trục 2 của cột song song dir, ngược lại B.</summary>
    public static double ColumnDimAlong(Project p, FrameElement col, Vec3 dir)
    {
        var sec = p.Section(col.Section);
        var e2 = Axis2(p, col);
        var d = new Vec3(dir.X, dir.Y, 0).Normalize();
        return Math.Abs(e2.Dot(d)) >= 0.7071 ? sec.H : sec.B;
    }

    public static Node LowerNode(Project p, FrameElement f)
    {
        var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
        return a.Z <= b.Z ? a : b;
    }

    public static Node UpperNode(Project p, FrameElement f)
    {
        var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
        return a.Z <= b.Z ? b : a;
    }

    /// <summary>Hình dạng thanh theo số đoạn bẻ: 0 → Thẳng, 1 → L, 2 → U.</summary>
    public static string ShapeOf(int legs) => legs >= 2 ? "U" : legs == 1 ? "L" : "Thẳng";
}
