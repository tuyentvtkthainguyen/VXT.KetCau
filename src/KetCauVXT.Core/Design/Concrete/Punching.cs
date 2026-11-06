namespace KetCauVXT.Design.Concrete;

// =====================================================================
// CHỌC THỦNG CÓ KỂ MÔ MEN – TCVN 5574:2018 mục 8.1.6.2
//   F/Fb,u + Mx/Mbx,u + My/My,u ≤ 1, trong đó phần mô men lấy không lớn hơn F/Fb,u;
//   Fb,u = Rbt·u·h0 ; Mb,u = Rbt·Wb·h0 ; Wb = Ib / ymax của đường bao tính toán (cách mép cột h0/2).
// Đường bao có thể bị cắt ở mép móng / mép đài (cột sát ranh: 3 cạnh, cột góc: 2 cạnh) –
// khi đó trọng tâm đường bao lệch khỏi tim cột e0 → mô men bổ sung F·e0 (mục 8.1.6.2.3).
// Đơn vị: kN, m.
// =====================================================================

/// <summary>Đường bao tính toán chọc thủng gồm các đoạn thẳng (toạ độ so với tim cột, m).</summary>
public sealed class PunchingContour
{
    public List<(double X1, double Y1, double X2, double Y2)> Segments { get; } = new();

    /// <summary>Đường bao chữ nhật quanh cột hc × bc cách mép cột h0/2 (cạnh lx = hc + h0 theo X, ly = bc + h0 theo Y).
    /// cutXNeg / cutXPos / cutYNeg / cutYPos: bỏ cạnh phía đó (cột sát mép móng – mép tự do cách mép cột &lt; h0/2).
    /// extXNeg…: khi cạnh bị cắt, hai cạnh vuông góc kéo dài tới mép móng cách tim cột một khoảng ext (m) – 0 = giữ lx/2.</summary>
    public static PunchingContour Rect(double hc, double bc, double h0, bool cutXNeg = false, bool cutXPos = false, bool cutYNeg = false, bool cutYPos = false,
        double extXNeg = 0, double extXPos = 0, double extYNeg = 0, double extYPos = 0)
    {
        double ax = (hc + h0) / 2, ay = (bc + h0) / 2;
        double xn = cutXNeg && extXNeg > 0 ? -Math.Min(extXNeg, ax) : -ax, xp = cutXPos && extXPos > 0 ? Math.Min(extXPos, ax) : ax;
        double yn = cutYNeg && extYNeg > 0 ? -Math.Min(extYNeg, ay) : -ay, yp = cutYPos && extYPos > 0 ? Math.Min(extYPos, ay) : ay;
        var c = new PunchingContour();
        if (!cutYNeg) c.Segments.Add((xn, yn, xp, yn));
        if (!cutXPos) c.Segments.Add((xp, yn, xp, yp));
        if (!cutYPos) c.Segments.Add((xp, yp, xn, yp));
        if (!cutXNeg) c.Segments.Add((xn, yp, xn, yn));
        return c;
    }

    /// <summary>Chu vi u (m).</summary>
    public double U => Segments.Sum(s => Len(s));

    static double Len((double X1, double Y1, double X2, double Y2) s) => Math.Sqrt((s.X2 - s.X1) * (s.X2 - s.X1) + (s.Y2 - s.Y1) * (s.Y2 - s.Y1));

    /// <summary>Trọng tâm đường bao (x0, y0).</summary>
    public (double X0, double Y0) Centroid()
    {
        double u = U;
        if (u <= 1e-12) return (0, 0);
        double sx = 0, sy = 0;
        foreach (var s in Segments) { double l = Len(s); sx += l * (s.X1 + s.X2) / 2; sy += l * (s.Y1 + s.Y2) / 2; }
        return (sx / u, sy / u);
    }

    /// <summary>Mô men kháng uốn Wb (m²) của đường bao đối với trục song song Y qua trọng tâm (aboutY = true – mô men làm phản lực thay đổi theo X)
    /// hoặc song song X (aboutY = false). Ib = Σ∫(x − x0)² ds, Wb = Ib / max|x − x0|.</summary>
    public double Wb(bool aboutY)
    {
        var (x0, y0) = Centroid();
        double ib = 0, dmax = 0;
        foreach (var s in Segments)
        {
            double a = aboutY ? s.X1 - x0 : s.Y1 - y0, b = aboutY ? s.X2 - x0 : s.Y2 - y0;
            double l = Len(s);
            if (l <= 1e-12) continue;
            // ∫ t² ds dọc đoạn thẳng t biến thiên tuyến tính từ a tới b trên chiều dài l: l·(a² + ab + b²)/3
            ib += l * (a * a + a * b + b * b) / 3;
            dmax = Math.Max(dmax, Math.Max(Math.Abs(a), Math.Abs(b)));
        }
        return dmax > 1e-12 ? ib / dmax : 0;
    }
}

/// <summary>Kết quả kiểm tra chọc thủng có kể mô men.</summary>
public sealed class PunchingResult
{
    public double F { get; set; }
    public double U { get; set; }
    public double Fbu { get; set; }
    public double Mx { get; set; }
    public double My { get; set; }
    public double Mbx { get; set; }
    public double Mby { get; set; }
    public double E0X { get; set; }
    public double E0Y { get; set; }
    /// <summary>F/Fb,u</summary>
    public double RatioF { get; set; }
    /// <summary>Phần mô men (đã chặn ≤ F/Fb,u)</summary>
    public double RatioM { get; set; }
    public double Ratio => RatioF + RatioM;
    public bool Ok => Ratio <= 1 + 1e-9;
    public string Text =>
        $"F = {F:0.0} kN, u = {U * 1000:0} mm, Fb,u = Rbt·u·h0 = {Fbu:0.0} kN; Mx = {Mx:0.0}, My = {My:0.0} kN.m, Mbx,u = Rbt·Wbx·h0 = {Mbx:0.0}, Mby,u = {Mby:0.0} kN.m"
        + (Math.Abs(E0X) + Math.Abs(E0Y) > 1e-6 ? $" (kể F·e0, e0 = {E0X * 1000:0}/{E0Y * 1000:0} mm do đường bao bị cắt)" : "")
        + $"; F/Fb,u + ΣM/Mb,u = {RatioF:0.000} + {RatioM:0.000} = {Ratio:0.000} ≤ 1";
}

/// <summary>Các công thức chọc thủng dùng chung (thiết kế + triển khai hồ sơ).</summary>
public static class Punching
{
    /// <summary>Kiểm tra chọc thủng có kể mô men (TCVN 5574:2018 mục 8.1.6.2).
    /// f: lực chọc thủng (kN); mAboutY: mô men làm phản lực thay đổi theo X (kN.m); mAboutX: theo Y; momentFactor: hệ số lấy mô men
    /// (0,5 với nút cột – móng / cột – đài như thực hành TCVN 5574:2018 mục 8.1.6.2.4; 1 = thiên an toàn).</summary>
    public static PunchingResult Check(PunchingContour c, double f, double mAboutY, double mAboutX, double h0, double rbt, double momentFactor = 0.5, double fbuOverride = 0)
    {
        var (x0, y0) = c.Centroid();
        double u = c.U;
        double fbu = fbuOverride > 0 ? fbuOverride : rbt * u * h0;
        double my = momentFactor * Math.Abs(mAboutY) + Math.Abs(f * x0);
        double mx = momentFactor * Math.Abs(mAboutX) + Math.Abs(f * y0);
        double mby = rbt * c.Wb(true) * h0, mbx = rbt * c.Wb(false) * h0;
        double rf = fbu > 1e-9 ? Math.Max(f, 0) / fbu : (f > 1e-9 ? 99 : 0);
        double rm = (mby > 1e-9 ? my / mby : 0) + (mbx > 1e-9 ? mx / mbx : 0);
        rm = Math.Min(rm, rf);
        return new PunchingResult { F = Math.Max(f, 0), U = u, Fbu = fbu, Mx = mx, My = my, Mbx = mbx, Mby = mby, E0X = x0, E0Y = y0, RatioF = rf, RatioM = rm };
    }

    /// <summary>Khả năng chịu cắt tiết diện nghiêng không cốt đai (TCVN 5574:2018 mục 8.1.3.3): Qb = 1,5·Rbt·b·h0²/c, 0,5·Rbt·b·h0 ≤ Qb ≤ 2,5·Rbt·b·h0.</summary>
    public static double ShearNoLinks(double b, double h0, double c, double rbt)
    {
        if (b <= 0 || h0 <= 0) return 0;
        c = Math.Max(c, 1e-6);
        double qb = 1.5 * rbt * b * h0 * h0 / c;
        return Math.Clamp(qb, 0.5 * rbt * b * h0, 2.5 * rbt * b * h0);
    }

    /// <summary>Cắt theo mép móng (bản móng / bản cánh không cốt đai): xét tiết diện nghiêng bắt đầu ở mép cột (mép sườn), hình chiếu c chạy
    /// 0 &lt; c ≤ min(l1; 3h0). Q(c) = p·b·(l1 − c), so với Qb(c). Trả về (tỉ số bất lợi, Q, Qb, c).</summary>
    public static (double Ratio, double Q, double Qb, double C) EdgeShear(double p, double b, double l1, double h0, double rbt, int steps = 30)
    {
        double worst = 0, wq = 0, wqb = 0, wc = 0;
        if (l1 <= 1e-6 || h0 <= 1e-6 || p <= 0) return (0, 0, ShearNoLinks(b, h0, h0, rbt), h0);
        double cmax = Math.Min(l1, 3 * h0);
        for (int i = 1; i <= steps; i++)
        {
            double c = cmax * i / steps;
            double q = p * b * Math.Max(l1 - c, 0);
            double qb = ShearNoLinks(b, h0, c, rbt);
            double r = qb > 0 ? q / qb : 99;
            if (r > worst || i == 1) { worst = r; wq = q; wqb = qb; wc = c; }
        }
        return (worst, wq, wqb, wc);
    }
}
