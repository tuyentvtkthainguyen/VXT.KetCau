using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Kết quả kiểm tra tiết diện chữ nhật chịu xoắn đồng thời với cắt (dầm cong).</summary>
public struct TorsionResult
{
    /// <summary>Mô men xoắn tính toán |T|, kN.m</summary>
    public double T;
    /// <summary>Lực cắt đi kèm |Q|, kN</summary>
    public double Q;
    /// <summary>Khả năng dải bê tông chịu nén giữa các vết nứt không gian 0,1·Rb·b²·h (b ≤ h), kN.m</summary>
    public double TStrut;
    /// <summary>Khả năng chịu xoắn của dải bê tông còn lại khi có lực cắt: TStrut·(1 − Q/(0,3·Rb·b·h0)), kN.m</summary>
    public double TStrutReduced;
    /// <summary>Diện tích lõi tiết diện giới hạn bởi tim cốt đai kín Ak = b1·h1, m²</summary>
    public double Ak;
    /// <summary>Chu vi lõi uk = 2(b1 + h1), m</summary>
    public double Uk;
    /// <summary>Diện tích cốt dọc chịu xoắn toàn chu vi Asl = T·uk/(2·Ak·Rs), m²</summary>
    public double AsLong;
    /// <summary>Diện tích một nhánh đai trên một mét dài yêu cầu (xoắn + cắt), m²/m</summary>
    public double AswPerMReq;
    /// <summary>Bước đai kín yêu cầu (đã làm tròn xuống bội 50 mm và giới hạn cấu tạo), m</summary>
    public double S;
    /// <summary>Bước đai lớn nhất theo cấu tạo khi có xoắn min(uk/8; 300 mm), m</summary>
    public double SMax;
    /// <summary>Khả năng chịu xoắn của cốt đai kín còn lại sau khi đã chịu phần lực cắt, kN.m</summary>
    public double TswCapacity;
    public int Dia;
    public bool StrutOk;
    public bool Ok;
    public string Note;
}

/// <summary>Tính toán cấu kiện BTCT tiết diện chữ nhật chịu xoắn (dầm cong trên mặt bằng) – bản GIẢN LƯỢC theo tinh thần
/// TCVN 5574:2018 mục 8.1.4 (cấu kiện chịu uốn có xoắn), dùng mô hình giàn không gian với góc nghiêng thanh chống θ = 45°.
/// <para>Giả thiết và phạm vi:</para>
/// <list type="number">
/// <item>Độ bền dải bê tông chịu nén giữa các vết nứt xiên không gian (8.1.4.2): T ≤ 0,1·Rb·b²·h với b, h là cạnh nhỏ, cạnh lớn.
///   Khi có lực cắt đồng thời, dùng tương tác tuyến tính (thiên về an toàn): T/(0,1·Rb·b²·h) + Q/(0,3·Rb·b·h0) ≤ 1.</item>
/// <item>Cốt đai: chỉ kể đai KÍN (bắt buộc với dầm chịu xoắn, móc 135°, đoạn móc ≥ 6d – mục 10.3.4/10.4.4).
///   Lõi Ak = b1·h1 với b1 = b − 2c, h1 = h − 2c (c: lớp bảo vệ tới mép cốt dọc ≈ tim đai). Diện tích một nhánh đai trên một mét dài
///   yêu cầu: Asw1/s = T/(2·Ak·Rsw) + qsw/(2·Rsw), trong đó qsw là cường độ đai chịu cắt theo 8.1.3.3 (cộng dồn hai nhu cầu – thiên về an toàn).</item>
/// <item>Bước đai khi có xoắn ≤ min(uk/8; 300 mm) và ≤ bước đai chịu cắt; làm tròn xuống bội 50 mm, tối thiểu 100 mm.</item>
/// <item>Cốt dọc chịu xoắn: Asl = T·uk/(2·Ak·Rs), phân bố đều theo chu vi; chương trình cộng ½·Asl vào thép trên và ½·Asl vào thép dưới
///   (thép góc gánh phần cạnh bên), cộng thêm vào thép chịu uốn. Dầm cao h &gt; 450 mm nên đặt thêm thép giá cạnh ≥ Ø12.</item>
/// <item>Bỏ qua phần bê tông chịu xoắn (Tb) và tương tác uốn–xoắn chi tiết theo tiết diện vênh không gian của 8.1.4.3 – thiên về an toàn.</item>
/// </list></summary>
public static class RcTorsion
{
    /// <summary>Khả năng dải bê tông chịu nén khi xoắn 0,1·Rb·b²·h (b ≤ h), kN.m.</summary>
    public static double StrutCapacity(double b, double h, Material conc)
    {
        double bm = Math.Min(b, h), hm = Math.Max(b, h);
        return 0.1 * conc.Rb * bm * bm * hm;
    }

    /// <summary>Lõi tiết diện (Ak, uk) giới hạn bởi tim cốt đai kín.</summary>
    public static (double Ak, double Uk) Core(double b, double h, double cover)
    {
        double b1 = Math.Max(b - 2 * cover, 0.3 * b), h1 = Math.Max(h - 2 * cover, 0.3 * h);
        return (b1 * h1, 2 * (b1 + h1));
    }

    /// <summary>Diện tích cốt dọc chịu xoắn toàn chu vi Asl = T·uk/(2·Ak·Rs), m².</summary>
    public static double LongitudinalArea(double T, double b, double h, double cover, Material steel)
    {
        var (ak, uk) = Core(b, h, cover);
        if (ak <= 0 || steel.Rs <= 0) return 0;
        return Math.Abs(T) * uk / (2 * ak * steel.Rs);
    }

    /// <summary>Kiểm tra / tính đai kín cho tiết diện chịu xoắn T và cắt Q với đai Ø dia hai nhánh.
    /// sShear: bước đai đã chọn theo cắt (m) – bước đai kết quả không lớn hơn giá trị này. main: thép dọc (tính Asl; null = thép đai).</summary>
    public static TorsionResult Design(double T, double Q, double b, double h, double h0, double cover,
        Material conc, Material stirrup, int dia, double sShear, Material main = null)
    {
        T = Math.Abs(T); Q = Math.Abs(Q);
        var r = new TorsionResult { T = T, Q = Q, Dia = dia, Note = "" };
        r.TStrut = StrutCapacity(b, h, conc);
        double qStrut = 0.3 * conc.Rb * b * h0;
        r.TStrutReduced = r.TStrut * Math.Max(0, 1 - Q / Math.Max(qStrut, 1e-9));
        r.StrutOk = T <= r.TStrutReduced + 1e-9;
        (r.Ak, r.Uk) = Core(b, h, cover);
        r.AsLong = LongitudinalArea(T, b, h, cover, main ?? stirrup);

        // nhu cầu đai một nhánh trên 1 m: xoắn + cắt
        double rsw = Math.Max(stirrup.Rsw, 1);
        double perT = r.Ak > 0 ? T / (2 * r.Ak * rsw) : 0;
        var sh = RcSection.Shear(Q, b, h0, conc, stirrup, dia);
        double perQ = sh.QswReq / (2 * rsw);
        r.AswPerMReq = perT + perQ;
        double a1 = RcSection.BarArea(dia);
        r.SMax = Math.Min(r.Uk / 8, 0.30);
        double sReq = r.AswPerMReq > 1e-12 ? a1 / r.AswPerMReq : double.PositiveInfinity;
        double s = Math.Min(Math.Min(sReq, r.SMax), sShear > 0 ? sShear : double.PositiveInfinity);
        s = RcSection.FloorTo(s, 0.05);
        bool spacingOk = s >= 0.10 - 1e-9;
        r.S = Math.Max(s, 0.10);
        // khả năng xoắn của đai còn lại sau phần chịu cắt
        r.TswCapacity = Math.Max(0, 2 * r.Ak * rsw * (a1 / r.S - perQ));
        r.Ok = r.StrutOk && spacingOk && T <= r.TswCapacity + 1e-9;
        if (!r.StrutOk)
            r.Note = "T = " + DesignUtil.F(T, "0.0") + " kN.m vượt khả năng dải bê tông chịu nén khi xoắn " + DesignUtil.F(r.TStrutReduced, "0.0") + " kN.m – cần tăng tiết diện";
        else if (!spacingOk)
            r.Note = "Bước đai kín Ø" + dia + " yêu cầu " + DesignUtil.F(Math.Min(sReq, r.SMax) * 1000, "0") + " mm < 100 mm – cần tăng đường kính đai";
        return r;
    }
}
