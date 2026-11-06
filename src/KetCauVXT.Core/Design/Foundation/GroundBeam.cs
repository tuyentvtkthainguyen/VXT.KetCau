namespace KetCauVXT.Design.Foundation;

// =====================================================================
// DẦM MÓNG / GIẰNG MÓNG – NỘI LỰC VÀ PHẢN LỰC LÊN MÓNG
//  (a) Giằng tựa lên móng / đài: dầm liên tục gối tựa đơn tại tim móng, tải phân bố đều q (tường + bản thân + sàn nền)
//      – phương trình ba mô men; phản lực gối Ri cộng vào lực dọc chân cột khi tính móng.
//  (b) Dầm móng trên nền đàn hồi Winkler (dầm đặt trực tiếp trên đất): q(x) = k·b·w(x); tổng phản lực nền = tổng tải.
// Đơn vị: kN, m. Mô men dương căng thớ dưới.
// =====================================================================

public sealed class GroundBeamResult
{
    /// <summary>Phản lực tại các gối (kN), từ trái sang phải – cộng vào N móng tương ứng.</summary>
    public List<double> Reactions { get; } = new();
    /// <summary>Mô men tại các gối (kN.m, âm căng trên).</summary>
    public List<double> SupportMoments { get; } = new();
    /// <summary>Mô men dương lớn nhất từng nhịp (kN.m).</summary>
    public List<double> SpanMoments { get; } = new();
    public double MPosMax { get; set; }
    public double MNegMax { get; set; }
    public double VMax { get; set; }
    /// <summary>Tổng tải tác dụng (kN)</summary>
    public double TotalLoad { get; set; }
    /// <summary>Dầm trên nền: áp lực nền lớn nhất (kN/m²), độ lún lớn nhất (m), tổng phản lực nền (kN)</summary>
    public double PressureMax { get; set; }
    public double SettlementMax { get; set; }
    public double SoilReaction { get; set; }
    public List<string> Calc { get; } = new();
}

public static class GroundBeam
{
    /// <summary>Tải tường xây trên dầm móng (tính toán): q = γf·γt·t·h (kN/m) – γt trọng lượng tường kể vữa trát (kN/m³).</summary>
    public static double WallLoad(double thickness, double height, double gammaWall = 18, double gammaF = 1.1) =>
        gammaF * gammaWall * Math.Max(thickness, 0) * Math.Max(height, 0);

    /// <summary>Dầm liên tục n nhịp gối tựa đơn chịu tải phân bố đều q (kN/m) – phương trình ba mô men:
    /// M(i−1)·Li + 2·Mi·(Li + Li+1) + M(i+1)·Li+1 = −q·(Li³ + Li+1³)/4, M0 = Mn = 0.</summary>
    public static GroundBeamResult Continuous(IList<double> spans, double q)
    {
        var r = new GroundBeamResult();
        int n = spans?.Count ?? 0;
        if (n == 0) return r;
        var L = spans.Select(x => Math.Max(x, 1e-6)).ToArray();
        var M = new double[n + 1];
        int m = n - 1;   // số gối giữa
        if (m > 0)
        {
            // hệ ba đường chéo – thuật toán Thomas
            var a = new double[m]; var b = new double[m]; var c = new double[m]; var d = new double[m];
            for (int i = 0; i < m; i++)
            {
                double l1 = L[i], l2 = L[i + 1];
                a[i] = i > 0 ? l1 : 0; b[i] = 2 * (l1 + l2); c[i] = i < m - 1 ? l2 : 0;
                d[i] = -q * (l1 * l1 * l1 + l2 * l2 * l2) / 4;
            }
            for (int i = 1; i < m; i++) { double w = a[i] / b[i - 1]; b[i] -= w * c[i - 1]; d[i] -= w * d[i - 1]; }
            var x = new double[m];
            x[m - 1] = d[m - 1] / b[m - 1];
            for (int i = m - 2; i >= 0; i--) x[i] = (d[i] - c[i] * x[i + 1]) / b[i];
            for (int i = 0; i < m; i++) M[i + 1] = x[i];
        }
        var R = new double[n + 1];
        double vmax = 0, mpos = 0;
        for (int k = 0; k < n; k++)
        {
            double vl = q * L[k] / 2 + (M[k + 1] - M[k]) / L[k];   // lực cắt đầu trái nhịp (dương lên)
            double vr = q * L[k] - vl;
            R[k] += vl; R[k + 1] += vr;
            vmax = Math.Max(vmax, Math.Max(Math.Abs(vl), Math.Abs(vr)));
            double xm = q > 1e-12 ? Math.Clamp(vl / q, 0, L[k]) : 0;
            double ms = M[k] + vl * xm - q * xm * xm / 2;
            r.SpanMoments.Add(ms);
            mpos = Math.Max(mpos, ms);
        }
        r.Reactions.AddRange(R);
        r.SupportMoments.AddRange(M);
        r.MPosMax = mpos; r.MNegMax = M.Length > 0 ? -M.Min() : 0; r.VMax = vmax;
        r.TotalLoad = q * L.Sum();
        r.Calc.Add($"Giằng móng tựa lên móng: dầm liên tục {n} nhịp ({string.Join(" + ", L.Select(v => v.ToString("0.##")))} m), q = {q:0.00} kN/m (tường + bản thân + sàn nền); "
                   + "phương trình ba mô men M(i−1)·Li + 2Mi(Li + Li+1) + M(i+1)·Li+1 = −q(Li³ + Li+1³)/4");
        r.Calc.Add($"M gối = {string.Join("; ", M.Select(v => v.ToString("0.0")))} kN.m; M nhịp lớn nhất = {mpos:0.0} kN.m; Qmax = {vmax:0.0} kN");
        r.Calc.Add($"Phản lực gối truyền xuống móng R = {string.Join("; ", R.Select(v => v.ToString("0.0")))} kN (ΣR = {R.Sum():0.0} = q·ΣL = {r.TotalLoad:0.0} kN)");
        return r;
    }

    /// <summary>Dầm móng đặt trên nền Winkler dài L (m), bề rộng đáy b (m), EI (kN.m²), hệ số nền k (kN/m³), tải phân bố q (kN/m) và các lực tập trung
    /// (x, P) hướng xuống (vd. lực từ cột / giằng). Phần tử 0,1 m. Trả về áp lực nền, lún, nội lực; ΣR nền = ΣP + q·L (cân bằng).</summary>
    public static GroundBeamResult OnWinkler(double length, double b, double EI, double k, double q, IList<(double X, double P)> points = null)
    {
        var r = new GroundBeamResult();
        if (length <= 0.05 || b <= 0 || EI <= 0 || k <= 0) return r;
        int ne = Math.Max(4, (int)Math.Ceiling(length / 0.1));
        var xs = Enumerable.Range(0, ne + 1).Select(i => length * i / ne).ToList();
        var beam = new StripFootingDesigner.WinklerBeam(xs, EI, k, b);
        var fz = new double[xs.Count]; var mt = new double[xs.Count];
        for (int i = 0; i < xs.Count; i++) fz[i] = -q * beam.Trib[i];
        double total = q * length;
        foreach (var (x, p) in points ?? new List<(double, double)>())
        {
            int i = (int)Math.Round(Math.Clamp(x, 0, length) / length * ne);
            fz[i] -= p; total += p;
        }
        var sol = beam.Solve(fz, mt);
        double soil = 0;
        for (int i = 0; i < xs.Count; i++) soil += beam.Ks[i] * Math.Max(-sol.W[i], 0) - beam.Ks[i] * Math.Max(sol.W[i], 0);
        r.TotalLoad = total;
        r.SoilReaction = soil;
        r.PressureMax = sol.Pressure.Max();
        r.SettlementMax = -sol.W.Min();
        r.MPosMax = Math.Max(0, sol.M.Concat(sol.MLeft).Max());
        r.MNegMax = Math.Max(0, -sol.M.Concat(sol.MLeft).Min());
        r.VMax = sol.V.Select(Math.Abs).Max();
        r.Calc.Add($"Dầm móng trên nền đàn hồi Winkler: L = {length:0.00} m, b = {b * 1000:0} mm, EI = {EI:0} kN.m², k = {k:0} kN/m³; phản lực nền q(x) = k·b·w(x)");
        r.Calc.Add($"Tổng tải = {total:0.0} kN; tổng phản lực nền = {soil:0.0} kN (cân bằng); pmax = {r.PressureMax:0.0} kN/m²; lún lớn nhất = {r.SettlementMax * 1000:0.0} mm; "
                   + $"M+ = {r.MPosMax:0.0}, M− = {r.MNegMax:0.0} kN.m, Qmax = {r.VMax:0.0} kN");
        return r;
    }
}
