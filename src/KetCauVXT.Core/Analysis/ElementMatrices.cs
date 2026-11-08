using KetCauVXT.Model;

namespace KetCauVXT.Analysis;

/// <summary>Ma trận phần tử thanh không gian 12 bậc tự do (Timoshenko – có kể biến dạng trượt như ETABS/SAP2000).
/// Thứ tự bậc tự do địa phương: [u1 u2 u3 r1 r2 r3]_I [u1 u2 u3 r1 r2 r3]_J.</summary>
public static class ElementMatrices
{
    /// <summary>Hệ trục địa phương mặc định giống ETABS/SAP2000:
    /// – Thanh đứng: trục 1 theo chiều I→J, trục 2 theo +X, trục 3 = 1×2.
    /// – Thanh còn lại: trục 3 = 1×Z (nằm ngang), trục 2 = 3×1 (hướng lên).
    /// Sau đó xoay quanh trục 1 một góc angleDeg.
    /// Trả về ma trận R 3×3 có các hàng là e1, e2, e3 biểu diễn trong hệ tổng thể.</summary>
    public static double[,] LocalAxes(Vec3 pi, Vec3 pj, double angleDeg)
    {
        Vec3 e1 = (pj - pi).Normalize();
        Vec3 e2, e3;
        double horiz = Math.Sqrt(e1.X * e1.X + e1.Y * e1.Y);
        if (horiz < 1e-3)
        {
            e2 = Vec3.UnitX;
            e3 = e1.Cross(e2).Normalize();
            e2 = e3.Cross(e1).Normalize();
        }
        else
        {
            e3 = e1.Cross(Vec3.UnitZ).Normalize();
            e2 = e3.Cross(e1).Normalize();
        }
        if (Math.Abs(angleDeg) > 1e-9)
        {
            double a = angleDeg * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            Vec3 n2 = e2 * c + e3 * s;
            Vec3 n3 = e2 * (-s) + e3 * c;
            e2 = n2; e3 = n3;
        }
        return new double[,]
        {
            { e1.X, e1.Y, e1.Z },
            { e2.X, e2.Y, e2.Z },
            { e3.X, e3.Y, e3.Z }
        };
    }

    /// <summary>Ma trận chuyển trục 12×12: d_local = T · d_global.</summary>
    public static double[,] Transformation(double[,] r)
    {
        var t = new double[12, 12];
        for (int b = 0; b < 4; b++)
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    t[b * 3 + i, b * 3 + j] = r[i, j];
        return t;
    }

    /// <summary>Ma trận độ cứng địa phương.</summary>
    public static double[,] LocalStiffness(double L, double E, double G, double A, double As2, double As3,
                                           double I33, double I22, double J)
    {
        var k = new double[12, 12];
        double ea = E * A / L, gj = G * J / L;
        k[0, 0] = ea; k[0, 6] = -ea; k[6, 0] = -ea; k[6, 6] = ea;
        k[3, 3] = gj; k[3, 9] = -gj; k[9, 3] = -gj; k[9, 9] = gj;

        // Uốn trong mặt phẳng 1-2 (u2, r3) – dùng I33, As2
        double phi2 = As2 > 0 ? 12 * E * I33 / (G * As2 * L * L) : 0;
        double c2 = E * I33 / ((1 + phi2) * L * L * L);
        int[] v = { 1, 5, 7, 11 };
        double[,] kb = BendingBlock(L, phi2);
        for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) k[v[a], v[b]] += c2 * kb[a, b];

        // Uốn trong mặt phẳng 1-3 (u3, r2) – dùng I22, As3, đổi dấu số hạng ghép
        double phi3 = As3 > 0 ? 12 * E * I22 / (G * As3 * L * L) : 0;
        double c3 = E * I22 / ((1 + phi3) * L * L * L);
        int[] w = { 2, 4, 8, 10 };
        double[,] kc = BendingBlock(L, phi3);
        double[] sgn = { 1, -1, 1, -1 };
        for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) k[w[a], w[b]] += c3 * kc[a, b] * sgn[a] * sgn[b];
        return k;
    }

    private static double[,] BendingBlock(double L, double phi) => new double[,]
    {
        { 12, 6 * L, -12, 6 * L },
        { 6 * L, (4 + phi) * L * L, -6 * L, (2 - phi) * L * L },
        { -12, -6 * L, 12, -6 * L },
        { 6 * L, (2 - phi) * L * L, -6 * L, (4 + phi) * L * L }
    };

    /// <summary>Khử tĩnh các bậc tự do bị giải phóng: K* = Kkk − Kkr Krr⁻¹ Krk ; P* = Pk − Kkr Krr⁻¹ Pr.
    /// Các hàng/cột bị giải phóng đặt bằng 0. Trả về ma trận phục hồi (để tính lại chuyển vị tương đối nếu cần).</summary>
    public static void Condense(double[,] k, double[] p, bool[] relI, bool[] relJ)
    {
        var rel = new List<int>();
        for (int i = 0; i < 6; i++) if (relI != null && relI[i]) rel.Add(i);
        for (int i = 0; i < 6; i++) if (relJ != null && relJ[i]) rel.Add(6 + i);
        if (rel.Count == 0) return;
        var keep = Enumerable.Range(0, 12).Where(i => !rel.Contains(i)).ToArray();
        int nr = rel.Count;
        var krr = new double[nr, nr];
        for (int a = 0; a < nr; a++) for (int b = 0; b < nr; b++) krr[a, b] = k[rel[a], rel[b]];
        var inv = Dense.Inverse(krr);
        // X = Krr⁻¹ Krk  (nr × 12)
        var x = new double[nr, 12];
        for (int a = 0; a < nr; a++)
            for (int j = 0; j < 12; j++)
            {
                double s = 0;
                for (int b = 0; b < nr; b++) s += inv[a, b] * k[rel[b], j];
                x[a, j] = s;
            }
        var y = new double[nr];
        if (p != null)
            for (int a = 0; a < nr; a++) { double s = 0; for (int b = 0; b < nr; b++) s += inv[a, b] * p[rel[b]]; y[a] = s; }

        var kn = new double[12, 12];
        foreach (int i in keep)
            foreach (int j in keep)
            {
                double s = k[i, j];
                for (int a = 0; a < nr; a++) s -= k[i, rel[a]] * x[a, j];
                kn[i, j] = s;
            }
        if (p != null)
        {
            var pn = new double[12];
            foreach (int i in keep)
            {
                double s = p[i];
                for (int a = 0; a < nr; a++) s -= k[i, rel[a]] * y[a];
                pn[i] = s;
            }
            Array.Copy(pn, p, 12);
        }
        Array.Copy(kn, k, 144);
    }

    /// <summary>Tải nút tương đương (địa phương) dùng hàm dạng Hermite.
    /// segs: các đoạn tải tuyến tính {xa, xb, q1a, q2a, q3a, q1b, q2b, q3b} (x tính từ đầu I, m; q kN/m).
    /// points: lực tập trung {x, P1, P2, P3} (kN).</summary>
    public static double[] EquivalentLoads(double L, IReadOnlyList<double[]> segs, IReadOnlyList<double[]> points)
    {
        var p = new double[12];
        double[] gx = { -Math.Sqrt(0.6), 0, Math.Sqrt(0.6) };
        double[] gw = { 5.0 / 9, 8.0 / 9, 5.0 / 9 };
        if (segs != null)
            foreach (var sg in segs)
            {
                double xa = sg[0], xb = sg[1], len = xb - xa;
                if (len <= 1e-12) continue;
                for (int g = 0; g < 3; g++)
                {
                    double t = (gx[g] + 1) / 2;
                    double x = xa + t * len;
                    double w = gw[g] * len / 2;
                    double q1 = sg[2] + t * (sg[5] - sg[2]);
                    double q2 = sg[3] + t * (sg[6] - sg[3]);
                    double q3 = sg[4] + t * (sg[7] - sg[4]);
                    AddPoint(p, L, x, q1 * w, q2 * w, q3 * w);
                }
            }
        if (points != null)
            foreach (var pt in points) AddPoint(p, L, pt[0], pt[1], pt[2], pt[3]);
        return p;
    }

    /// <summary>Tải nút tương đương của mômen xoắn phân bố (xa, xb, ta, tb) và tập trung (x, T) quanh trục 1 – hàm dạng tuyến tính như lực dọc.</summary>
    public static void AddTorsion(double[] p, double L, IReadOnlyList<double[]> tsegs, IReadOnlyList<double[]> tpts)
    {
        double[] gx = { -Math.Sqrt(0.6), 0, Math.Sqrt(0.6) };
        double[] gw = { 5.0 / 9, 8.0 / 9, 5.0 / 9 };
        if (tsegs != null)
            foreach (var sg in tsegs)
            {
                double len = sg[1] - sg[0];
                if (len <= 1e-12) continue;
                for (int g = 0; g < 3; g++)
                {
                    double t = (gx[g] + 1) / 2, x = sg[0] + t * len, w = gw[g] * len / 2;
                    double m = (sg[2] + t * (sg[3] - sg[2])) * w, xi = Math.Clamp(x / L, 0, 1);
                    p[3] += m * (1 - xi); p[9] += m * xi;
                }
            }
        if (tpts != null)
            foreach (var pt in tpts) { double xi = Math.Clamp(pt[0] / L, 0, 1); p[3] += pt[1] * (1 - xi); p[9] += pt[1] * xi; }
    }

    private static void AddPoint(double[] p, double L, double x, double f1, double f2, double f3)
    {
        double xi = Math.Clamp(x / L, 0, 1);
        double n1 = 1 - xi, n2 = xi;
        double h1 = 1 - 3 * xi * xi + 2 * xi * xi * xi;
        double h2 = L * (xi - 2 * xi * xi + xi * xi * xi);
        double h3 = 3 * xi * xi - 2 * xi * xi * xi;
        double h4 = L * (-xi * xi + xi * xi * xi);
        p[0] += f1 * n1; p[6] += f1 * n2;
        // mặt phẳng 1-2: (u2, r3)
        p[1] += f2 * h1; p[5] += f2 * h2; p[7] += f2 * h3; p[11] += f2 * h4;
        // mặt phẳng 1-3: (u3, r2) – r2 = −du3/dx
        p[2] += f3 * h1; p[4] -= f3 * h2; p[8] += f3 * h3; p[10] -= f3 * h4;
    }
}
