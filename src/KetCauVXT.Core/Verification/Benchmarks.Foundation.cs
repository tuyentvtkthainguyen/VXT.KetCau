using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Verification;

public static partial class HandCalc
{
    /// <summary>Hệ số A, B, D của công thức sức chịu tải tiêu chuẩn R – giá trị TRA BẢNG 14 TCVN 9362:2012 (nhập tay, không tính từ công thức).
    /// Chỉ gồm các góc φ có giá trị chắc chắn; φ khác trả về false.</summary>
    public static bool TableABD(int phiDeg, out double A, out double B, out double D)
    {
        (A, B, D) = phiDeg switch
        {
            0 => (0.00, 1.00, 3.14),
            10 => (0.18, 1.73, 4.17),
            16 => (0.36, 2.43, 4.99),
            20 => (0.51, 3.06, 5.66),
            24 => (0.72, 3.87, 6.45),
            30 => (1.15, 5.59, 7.95),
            _ => (double.NaN, double.NaN, double.NaN)
        };
        return double.IsFinite(A);
    }

    /// <summary>R = m1·m2/ktc·(A·b·γII + B·h·γ'II + D·cII) với A, B, D tra bảng (TCVN 9362:2012).</summary>
    public static double BearingR(int phiDeg, double c, double b, double gamma, double h, double gammaAbove, double m1, double m2, double ktc)
    {
        if (!TableABD(phiDeg, out double A, out double B, out double D)) return double.NaN;
        return m1 * m2 / ktc * (A * b * gamma + B * h * gammaAbove + D * c);
    }

    /// <summary>Áp lực đáy móng chữ nhật lệch tâm một phương: e = M/N ≤ L/6: p = N/(L·B)·(1 ± 6e/L);
    /// e &gt; L/6 (tiếp xúc một phần, phản lực tam giác): pmax = 2N/(3·B·c0), c0 = L/2 − e.</summary>
    public static (double Pmax, double Pmin, bool Partial) FootingPressure(double N, double M, double L, double B)
    {
        double e = Math.Abs(M) / N;
        if (e <= L / 6) return (N / (L * B) * (1 + 6 * e / L), N / (L * B) * (1 - 6 * e / L), false);
        double c0 = L / 2 - e;
        return (2 * N / (3 * B * c0), 0, true);
    }

    /// <summary>Chọc thủng móng đơn, cột hc × bc ở tâm móng L × B (TCVN 5574:2018 mục 8.1.6): Fb,u = Rbt·u·h0,
    /// u = 2(hc + bc + 2h0) (chu vi cách mép cột h0/2); F = N·[1 − (hc + 2h0)(bc + 2h0)/(L·B)] (trừ phản lực trong đáy tháp 45°).</summary>
    public static (double F, double Fu, double U) Punching(double N, double L, double B, double hc, double bc, double h0, double Rbt)
    {
        double u = 2 * (hc + bc + 2 * h0);
        double ap = Math.Min((hc + 2 * h0) * (bc + 2 * h0), L * B);
        return (N * (1 - ap / (L * B)), Rbt * u * h0, u);
    }

    /// <summary>Một lớp đất cho tính tay: độ sâu mặt/đáy lớp (m), γ, γsat (kN/m³), E (MPa), N SPT, đất dính.</summary>
    public sealed record SoilRow(double Top, double Bottom, double Gamma, double GammaSat, double E, double N, bool Cohesive);

    private static SoilRow RowAt(IList<SoilRow> rows, double z)
    {
        foreach (var r in rows) if (r.Top <= z + 1e-9 && z < r.Bottom - 1e-9) return r;
        return z < rows[0].Top ? rows[0] : rows[^1];
    }

    /// <summary>Ứng suất hữu hiệu do trọng lượng bản thân σ'bt(z) = Σγi·hi, dưới mực nước ngầm γ' = γsat − 10 (tích phân theo bước 1 mm, độc lập với chương trình).</summary>
    public static double SigmaBt(IList<SoilRow> rows, double water, double z)
    {
        int n = Math.Max(1, (int)Math.Ceiling(z / 0.001));
        double dz = z / n, s = 0;
        for (int i = 0; i < n; i++)
        {
            double zm = (i + 0.5) * dz;
            var r = RowAt(rows, zm);
            s += (zm >= water ? r.GammaSat - 10 : r.Gamma) * dz;
        }
        return s;
    }

    /// <summary>Ứng suất σz tại độ sâu z dưới TÂM móng chữ nhật L × B chịu tải đều p: tích phân số lời giải Boussinesq cho lực tập trung
    /// σz = 3P·z³/(2π·R⁵) trên diện chịu tải (Simpson 120 × 120 trên 1/4 diện tích × 4) – độc lập với công thức góc của chương trình.</summary>
    public static double SigmaZCenter(double L, double B, double z, double p)
    {
        const int n = 120;
        double a = L / 2, b = B / 2, hx = a / n, hy = b / n, s = 0;
        for (int i = 0; i <= n; i++)
        {
            double wi = i == 0 || i == n ? 1 : i % 2 == 1 ? 4 : 2, x = i * hx;
            for (int j = 0; j <= n; j++)
            {
                double wj = j == 0 || j == n ? 1 : j % 2 == 1 ? 4 : 2, y = j * hy;
                double r2 = x * x + y * y + z * z;
                s += wi * wj * 3 * z * z * z / (2 * Math.PI * Math.Pow(r2, 2.5));
            }
        }
        return 4 * p * s * hx * hy / 9;
    }

    /// <summary>Độ lún móng nông theo phương pháp cộng lún từng lớp phân tố (TCVN 9362:2012): σgl = p − σ'bt(Df);
    /// lớp phân tố hi ≤ 0,4b (chia tại ranh giới lớp và mực nước ngầm), σz tại giữa lớp phân tố; giới hạn vùng nén σz ≤ 0,2σ'bt (0,1 khi E &lt; 5 MPa);
    /// S = β·Σσz,i·hi/Ei, β = 0,8. Trả về S (m), chiều sâu vùng nén Hc (m dưới đáy móng), số lớp phân tố.</summary>
    public static (double S, double Hc, int Layers) Settlement(IList<SoilRow> rows, double water, double L, double B, double Df, double p)
    {
        if (B > L) (L, B) = (B, L);
        double sgl = p - SigmaBt(rows, water, Df);
        if (sgl <= 0) return (0, 0, 0);
        var breaks = rows.SelectMany(r => new[] { r.Top, r.Bottom }).Append(water).Where(double.IsFinite).Distinct().OrderBy(v => v).ToList();
        double z = 0, S = 0; int k = 0;
        while (z < 60)
        {
            double top = Df + z, end = top + 0.4 * B;
            foreach (var v in breaks) if (v > top + 1e-9 && v < end - 1e-9) { end = v; break; }
            double hh = end - top, zm = z + hh / 2;
            var r = RowAt(rows, Df + zm);
            double sz = SigmaZCenter(L, B, zm, sgl), sbt = SigmaBt(rows, water, Df + zm);
            if (sz <= (r.E < 5 ? 0.1 : 0.2) * sbt) break;
            S += 0.8 * sz * hh / (r.E * 1000);
            z += hh; k++;
        }
        return (S, z, k);
    }

    /// <summary>Sức chịu tải cọc đơn theo SPT – công thức Viện Kiến trúc Nhật Bản (TCVN 10304:2014 Phụ lục G), viết lại độc lập:
    /// Rc,u = qb·Ab + u·Σ(fc,i·lc,i + fs,i·ls,i); đất rời fs = 10·Ns/3 kPa; đất dính fc = αp·fL·cu, cu = 6,25·Nc, fL = 1,
    /// αp = 0,5·ψ^−0,5 (ψ = cu/σ'v ≤ 1) hoặc 0,5·ψ^−0,25 (ψ &gt; 1), αp ≤ 1, σ'v tại giữa lớp; mũi cọc đóng/ép trong đất rời qb = 300·Np
    /// (Np trung bình từ 1d trên đến 4d dưới mũi); mọi N ≤ 50. Trả về ma sát thân, sức kháng mũi (kN).</summary>
    public static (double Shaft, double Tip) PileSpt(IList<SoilRow> rows, double water, double d, double cutoff, double tip, bool bored)
    {
        double u = 4 * d, Ab = d * d, shaft = 0;
        foreach (var r in rows)
        {
            double a = Math.Max(r.Top, cutoff), b = Math.Min(r.Bottom, tip);
            if (b <= a + 1e-9) continue;
            double n = Math.Min(r.N, 50), f;
            if (r.Cohesive)
            {
                double cu = 6.25 * n, sv = SigmaBt(rows, water, (a + b) / 2), psi = cu / sv;
                double ap = Math.Min(psi <= 1 ? 0.5 / Math.Sqrt(psi) : 0.5 * Math.Pow(psi, -0.25), 1.0);
                f = ap * 1.0 * cu;
            }
            else f = 10 * n / 3;
            shaft += u * f * (b - a);
        }
        // Np: trung bình gia quyền theo chiều dài từ (mũi − d) đến (mũi + 4d)
        double z1 = tip - d, z2 = tip + 4 * d, sum = 0;
        int steps = 1000;
        for (int i = 0; i < steps; i++) sum += Math.Min(RowAt(rows, z1 + (i + 0.5) * (z2 - z1) / steps).N, 50);
        double np = Math.Min(sum / steps, 50);
        var tr = RowAt(rows, tip);
        double qb = tr.Cohesive ? 9 * 6.25 * np : (bored ? 150 : 300) * np;
        return (shaft, qb * Ab);
    }
}

public static partial class Benchmarks
{
    // ==================================================================
    // C. NỀN MÓNG
    // ==================================================================
    private static IEnumerable<Suite> FoundationSuites()
    {
        const string G = GroupFoundation;
        yield return S("C01", G, "Sức chịu tải tiêu chuẩn của nền R (móng nông)", BearingCase);
        yield return S("C02", G, "Áp lực đáy móng đơn lệch tâm", PressureCase);
        yield return S("C03", G, "Chọc thủng móng đơn dưới cột", PunchingCase);
        yield return S("C04", G, "Độ lún móng đơn – cộng lún từng lớp", SettlementCase);
        yield return S("C05", G, "Sức chịu tải cọc ép theo SPT – nền 2 lớp", PileCase);
    }

    // ---- C01 ----
    private static List<BenchmarkCase> BearingCase()
    {
        const string G = GroupFoundation, T = "Sức chịu tải tiêu chuẩn của nền R (móng nông)";
        var res = new List<BenchmarkCase>();
        var cases = new[]
        {
            (Phi: 16, C: 25.0, B: 1.5, Gam: 19.0, Df: 1.5, GamA: 18.0),
            (Phi: 20, C: 15.0, B: 1.8, Gam: 18.5, Df: 1.2, GamA: 17.5),
            (Phi: 30, C: 2.0, B: 2.0, Gam: 19.0, Df: 1.5, GamA: 18.0),
            (Phi: 10, C: 30.0, B: 1.4, Gam: 18.0, Df: 1.0, GamA: 17.0)
        };
        int k = 1;
        foreach (var x in cases)
        {
            var s = new SoilSettings { Phi = x.Phi, C = x.C, Gamma = x.Gam, Depth = x.Df, GammaAbove = x.GamA, M1 = 1.1, M2 = 1.0, Ktc = 1.0, ROverride = 0 };
            double hand = HandCalc.BearingR(x.Phi, x.C, x.B, x.Gam, x.Df, x.GamA, 1.1, 1.0, 1.0);
            double prog = FootingDesigner.BearingR(s, x.B);
            HandCalc.TableABD(x.Phi, out double A, out double Bc, out double D);
            string desc = $"φII = {x.Phi}°, cII = {x.C} kPa, b = {x.B} m, γII = {x.Gam}, h = Df = {x.Df} m, γ'II = {x.GamA} kN/m³, m1 = 1,1, m2 = 1,0, ktc = 1,0.";
            res.Add(C($"C01-{k++}", G, T, desc, $"TCVN 9362:2012: R = m1m2/ktc·(A·b·γII + B·h·γ'II + D·cII), A, B, D tra Bảng 14 (φ = {x.Phi}°: A = {A:0.00}, B = {Bc:0.00}, D = {D:0.00})",
                $"R (φ = {x.Phi}°)", "kPa", hand, prog, 1.0,
                "Chương trình tính A, B, D trực tiếp từ công thức giải tích theo φ (thay cho tra bảng) – sai khác do làm tròn bảng ≤ 0,1%."));
        }
        return res;
    }

    // ---- C02 ----
    private static List<BenchmarkCase> PressureCase()
    {
        const string G = GroupFoundation, T = "Áp lực đáy móng đơn lệch tâm";
        double L = 2.4, B = 2.0;
        var h1 = HandCalc.FootingPressure(800, 100, L, B);
        var p1 = EccentricFooting.Pressure(800, 100, 0, L, B);
        var h2 = HandCalc.FootingPressure(500, 250, L, B);
        var p2 = EccentricFooting.Pressure(500, 250, 0, L, B);
        string d1 = $"Móng L × B = {L} × {B} m, N = 800 kN, M = 100 kN.m theo phương L (e = 0,125 m < L/6 = 0,4 m – toàn bộ đáy tiếp xúc).";
        string d2 = $"Móng L × B = {L} × {B} m, N = 500 kN, M = 250 kN.m (e = 0,5 m > L/6 – tách nền một phần, phản lực tam giác).";
        return new List<BenchmarkCase>
        {
            C("C02-1", G, T, d1, "Nén lệch tâm: pmax = N/(LB)·(1 + 6e/L)", "pmax", "kPa", h1.Pmax, p1.Pmax, 0.1),
            C("C02-2", G, T, d1, "Nén lệch tâm: pmin = N/(LB)·(1 − 6e/L)", "pmin", "kPa", h1.Pmin, p1.Pmin, 0.1),
            C("C02-3", G, T, d2, "Tiếp xúc một phần: pmax = 2N/(3B·c0), c0 = L/2 − e", "pmax (tách nền)", "kPa", h2.Pmax, p2.Pmax, 0.1)
        };
    }

    // ---- C03 ----
    private static List<BenchmarkCase> PunchingCase()
    {
        const string G = GroupFoundation, T = "Chọc thủng móng đơn dưới cột";
        double N = 900, L = 2.4, B = 2.0, hc = 0.30, bc = 0.22, h0 = 0.45;
        var hcalc = HandCalc.Punching(N, L, B, hc, bc, h0, RbtB25);
        EccentricFooting.Punching(N, L, B, hc, bc, 0, 0, h0, B25().Rbt, out double F, out double Fu, out double u);
        string desc = $"Móng {L} × {B} m, cột {hc * 1000:0}×{bc * 1000:0} mm ở tâm, h0 = {h0 * 1000:0} mm, B25 (Rbt = 1,05 MPa), lực dọc tính toán N = {N} kN (áp lực ròng đều).";
        string src = "TCVN 5574:2018 mục 8.1.6 (chọc thủng): Fb,u = Rbt·u·h0, u = 2(hc + bc + 2h0); F = N·[1 − (hc + 2h0)(bc + 2h0)/(L·B)]";
        return new List<BenchmarkCase>
        {
            C("C03-1", G, T, desc, src, "Chu vi tính toán u", "m", hcalc.U, u, 0.1),
            C("C03-2", G, T, desc, src, "Khả năng chống chọc thủng Fb,u", "kN", hcalc.Fu, Fu, 0.1),
            C("C03-3", G, T, desc, src, "Lực chọc thủng F", "kN", hcalc.F, F, 0.1)
        };
    }

    private static readonly HandCalc.SoilRow[] SettleRows =
    {
        new(0, 3, 18.0, 19.0, 8.0, 6, true),
        new(3, 6, 18.5, 19.5, 12.0, 10, true),
        new(6, 30, 19.0, 20.0, 20.0, 18, false)
    };

    private static Borehole BoreholeOf(IList<HandCalc.SoilRow> rows, double water, string name)
    {
        var bh = new Borehole { Name = name, WaterDepth = double.IsFinite(water) ? water : -1 };
        int i = 1;
        foreach (var r in rows)
            bh.Layers.Add(new SoilLayer
            {
                Name = "Lớp " + i++, Top = r.Top, Bottom = r.Bottom, Gamma = r.Gamma, GammaSat = r.GammaSat, E = r.E, N = r.N,
                Kind = r.Cohesive ? SoilKind.SiltyClay : SoilKind.Sand, Phi = r.Cohesive ? 14 : 28, C = r.Cohesive ? 20 : 1
            });
        return bh;
    }

    // ---- C04 ----
    private static List<BenchmarkCase> SettlementCase()
    {
        const string G = GroupFoundation, T = "Độ lún móng đơn – cộng lún từng lớp";
        double L = 2.4, B = 2.0, Df = 1.5, p = 220, water = 4.0;
        var hand = HandCalc.Settlement(SettleRows, water, L, B, Df, p);
        var prog = GeotechCalc.Settlement(BoreholeOf(SettleRows, water, "HK-KC"), L, B, Df, p);
        string desc = $"Móng {L} × {B} m, Df = {Df} m, áp lực tiêu chuẩn đáy móng p = {p} kPa; nền 3 lớp: sét pha 0–3 m (γ 18, E 8 MPa), sét pha 3–6 m (γ 18,5, E 12 MPa), "
                      + $"cát 6–30 m (γ 19, E 20 MPa); mực nước ngầm {water} m (γ' = γsat − 10).";
        string src = "Cộng lún từng lớp (TCVN 9362:2012): β = 0,8, hi ≤ 0,4b, σz dưới tâm móng tích phân số lời giải Boussinesq, vùng nén σz ≤ 0,2σ'bt";
        return new List<BenchmarkCase>
        {
            C("C04-1", G, T, desc, src, "Độ lún S", "cm", hand.S * 100, prog.S * 100, 1.0,
              "Chương trình tính σz bằng công thức hệ số góc (dạng kín) – tham chiếu tích phân số độc lập."),
            C("C04-2", G, T, desc, src, "Chiều sâu vùng chịu nén Hc", "m", hand.Hc, prog.Hc, 0.5, $"Số lớp phân tố tính tay: {hand.Layers}; chương trình: {prog.Rows.Count}.")
        };
    }

    // ---- C05 ----
    private static List<BenchmarkCase> PileCase()
    {
        const string G = GroupFoundation, T = "Sức chịu tải cọc ép theo SPT – nền 2 lớp";
        var rows = new[]
        {
            new HandCalc.SoilRow(0, 8, 18.0, 19.0, 6, 8, true),
            new HandCalc.SoilRow(8, 25, 19.0, 20.0, 25, 25, false)
        };
        double d = 0.30, cut = 1.5, tip = 12.0;
        var hand = HandCalc.PileSpt(rows, double.PositiveInfinity, d, cut, tip, false);
        var bh = BoreholeOf(rows, double.PositiveInfinity, "HK-COC");
        double As = 4 * Bar(16);
        var prog = GeotechCalc.PileCapacity(bh, d, false, tip, cut, false, 2.5, As, "B25", "CB400-V");
        double pvl = RbB25 * d * d + RscCB400 * As;
        string desc = $"Cọc BTCT vuông {d * 1000:0}×{d * 1000:0} mm ép, đầu cọc {cut} m, mũi {tip} m; sét pha 0–8 m N = 8 (γ = 18), cát 8–25 m N = 25; không có nước ngầm; thép 4Ø16, B25.";
        string src = "TCVN 10304:2014 Phụ lục G (công thức Nhật Bản): Rc,u = qb·Ab + u·Σ(fc·lc + fs·ls); fs = 10N/3, fc = αp·cu, cu = 6,25N; qb = 300Np";
        return new List<BenchmarkCase>
        {
            C("C05-1", G, T, desc, src, "Ma sát thân cọc u·Σf·l", "kN", hand.Shaft, prog.ShaftResistance, 1.0),
            C("C05-2", G, T, desc, src, "Sức kháng mũi qb·Ab", "kN", hand.Tip, prog.TipResistance, 1.0),
            C("C05-3", G, T, desc, src, "Sức chịu tải cực hạn Rc,u", "kN", hand.Shaft + hand.Tip, prog.Rcu, 1.0),
            C("C05-4", G, T, desc, "Theo vật liệu (φ = 1): Pvl = Rb·Ab + Rsc·As", "Pvl", "kN", pvl, prog.Pvl, 0.1)
        };
    }

    // ==================================================================
    // D. TẢI TRỌNG VÀ TỔ HỢP
    // ==================================================================
    private static IEnumerable<Suite> LoadSuites()
    {
        const string G = GroupLoads;
        yield return S("D01", G, "Hệ số k(z) theo độ cao và dạng địa hình", KzCase);
        yield return S("D02", G, "Lực gió tĩnh tác dụng lên các tầng", WindStoreyCase);
        yield return S("D03", G, "Tổ hợp tải trọng – hệ số và cộng tác dụng", ComboCase);
    }

    // ---- D01 ----
    private static List<BenchmarkCase> KzCase()
    {
        const string G = GroupLoads, T = "Hệ số k(z) theo độ cao và dạng địa hình";
        var tab = new[] { ("B", 10.0, 1.00), ("B", 20.0, 1.13), ("B", 40.0, 1.28), ("A", 10.0, 1.18), ("C", 10.0, 0.66), ("B", 3.0, 0.80) };
        var res = new List<BenchmarkCase>();
        int k = 1;
        foreach (var (ter, z, kt) in tab)
            res.Add(C($"D01-{k++}", G, T,
                "Hệ số k(z) chương trình dùng (LateralLoadGenerator.Kz, dạng hàm mũ k = 1,844·(z/zg)^2α) so với giá trị bảng.",
                "Bảng k(z) của TCVN 2737:1995 (Bảng 5, giá trị tham khảo lập từ cùng dạng hàm mũ) – cần đối chiếu bảng tương ứng của TCVN 2737:2023 (nhập vào cột tài liệu)",
                $"k(z = {z:0} m, địa hình {ter})", "", kt, LateralLoadGenerator.Kz(z, ter), 1.5,
                $"Bảng làm tròn 2 chữ số; dạng hàm mũ viết lại độc lập cho {BenchmarkCase.Fmt(HandKz(z, ter), "0.0000")}."));
        return res;
    }

    // ---- D02 ----
    private static List<BenchmarkCase> WindStoreyCase()
    {
        const string G = GroupLoads, T = "Lực gió tĩnh tác dụng lên các tầng";
        var p = StoreyModel();
        double width = p.Nodes.Max(n => n.Y) - p.Nodes.Min(n => n.Y);
        var hand = HandWindForces(p, width);
        var res = FrameAnalysis.Run(p);
        var list = new List<BenchmarkCase>();
        int k = 1;
        foreach (var hf in hand)
        {
            var lf = res.LateralForces.FirstOrDefault(x => x.Pattern == "GX" && x.Story == hf.Story);
            list.Add(C($"D02-{k++}", G, T,
                $"Khung 3 tầng của A12; gió theo phương X, bề rộng đón gió B = {width} m, W0 = {p.Wind.W0} kN/m², địa hình {p.Wind.Terrain}, c = {p.Wind.Cp} (đón + hút), hệ số điều chỉnh {p.Wind.Factor}.",
                "TCVN 2737:2023 (giá trị tiêu chuẩn tĩnh): W = W0·k(z)·c; lực tầng F = W·h·B, h = nửa tầng dưới + nửa tầng trên – tính tay theo lựa chọn hệ số của chương trình",
                $"Fx tầng {hf.Story} (z = {hf.Z:0.0} m)", "kN", hf.F, lf?.Fx ?? double.NaN, 0.1,
                $"k = {BenchmarkCase.Fmt(hf.K, "0.000")}, h = {BenchmarkCase.Fmt(hf.H, "0.00")} m. Chương trình chưa kể thành phần động của gió (nhà thấp tầng, H < 40 m)."));
        }
        return list;
    }

    // ---- D03 ----
    private static List<BenchmarkCase> ComboCase()
    {
        const string G = GroupLoads, T = "Tổ hợp tải trọng – hệ số và cộng tác dụng";
        var p = StoreyModel();
        var res = FrameAnalysis.Run(p);
        // hệ số tính tay: γf mặc định (TT 1,1; HTHT 1,3; HT 1,3; gió 1,2), hệ số tổ hợp ψ = 0,9 cho tổ hợp ≥ 2 tải tạm thời
        double gTT = 1.1, gHTHT = 1.3, gHT = 1.3, gW = 1.2, psi = 0.9;
        var cWind = p.Combinations.FirstOrDefault(c => c.Type == ComboType.ULS && c.Items.Any(i => i.Case == "HT") && c.Items.Any(i => i.Case == "GX" && i.Factor > 0))
                    ?? throw new InvalidOperationException("không tìm thấy tổ hợp TT + 0,9(HT + GX)");
        var cLive = p.Combinations.FirstOrDefault(c => c.Type == ComboType.ULS && c.Items.Any(i => i.Case == "HT") && c.Items.All(i => i.Case != "GX"))
                    ?? throw new InvalidOperationException("không tìm thấy tổ hợp TT + HT");
        var baseNode = p.Nodes.Where(n => n.IsSupport).OrderBy(n => n.X).ThenBy(n => n.Y).First();
        double R(string cs, int d) => res.Get(cs).Reactions[baseNode.Id][d];
        double handWind = gTT * R("TT", 4) + gHTHT * R("HTHT", 4) + psi * gHT * R("HT", 4) + psi * gW * R("GX", 4);
        double handLive = gTT * R("TT", 2) + gHTHT * R("HTHT", 2) + gHT * R("HT", 2);
        double fHT = cWind.Items.First(i => i.Case == "HT").Factor;
        string desc = $"Khung 3 tầng của A12; chân cột tại ({baseNode.X:0.##}; {baseNode.Y:0.##}). Tổ hợp tự sinh: {cLive.Name} = {ComboGenerator.Describe(cLive)}; {cWind.Name} = {ComboGenerator.Describe(cWind)}.";
        string src = "TCVN 2737:2023 – tổ hợp cơ bản: γf theo trường hợp tải (mặc định của chương trình); tổ hợp có ≥ 2 tải tạm thời nhân ψ = 0,9 (lựa chọn của chương trình)";
        return new List<BenchmarkCase>
        {
            C("D03-1", G, T, desc, src, $"Hệ số HT trong {cWind.Name} = ψ·γf", "", psi * gHT, fHT, 0.1),
            C("D03-2", G, T, desc, src + "; cộng tác dụng tuyến tính", $"Phản lực FZ chân cột – {cLive.Name}", "kN", handLive, res.Get(cLive.Name).Reactions[baseNode.Id][2], 0.01),
            C("D03-3", G, T, desc, src + "; cộng tác dụng tuyến tính", $"Phản lực MY chân cột – {cWind.Name}", "kN.m", handWind, res.Get(cWind.Name).Reactions[baseNode.Id][4], 0.01)
        };
    }
}
