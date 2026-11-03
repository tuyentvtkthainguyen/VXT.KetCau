namespace KetCauVXT.Analysis;

/// <summary>Đại số tuyến tính cho bài toán trị riêng tổng quát đối xứng K·φ = ω²·M·φ (phân tích dao động riêng).
/// – SymmetricEigen: Householder đưa về ba đường chéo + QL ẩn (tred2/tql2 – EISPACK, bản JAMA), trị riêng tăng dần.
/// – Dense: bài toán sau khử tĩnh các bậc tự do không khối lượng, dạng độ mềm F·M·φ = μ·φ (μ = 1/ω²);
///   đối xứng hoá bằng Cholesky của F (F = R·Rᵀ, B = Rᵀ·M·R) nên cho phép M suy biến (khối 3×3 sàn cứng hạng thiếu…).
/// – Subspace: lặp không gian con (Bathe) dùng K⁻¹ đã phân tích, giải bài toán thu gọn q×q bằng Cholesky của K̄.
/// – Hệ số tương quan CQC (Der Kiureghian 1981, cùng tỷ số cản ζ).</summary>
public static class ModalSolver
{
    /// <summary>Trị riêng, véc tơ riêng ma trận đối xứng (a không bị thay đổi). d tăng dần, cột v[:,k] ứng với d[k].</summary>
    public static void SymmetricEigen(double[,] a, out double[] d, out double[,] v)
    {
        int n = a.GetLength(0);
        var V = (double[,])a.Clone();
        d = new double[n];
        var e = new double[n];
        if (n == 0) { v = V; return; }
        // ---- tred2 ----
        for (int j = 0; j < n; j++) d[j] = V[n - 1, j];
        for (int i = n - 1; i > 0; i--)
        {
            double scale = 0, h = 0;
            for (int k = 0; k < i; k++) scale += Math.Abs(d[k]);
            if (scale == 0)
            {
                e[i] = d[i - 1];
                for (int j = 0; j < i; j++) { d[j] = V[i - 1, j]; V[i, j] = 0; V[j, i] = 0; }
            }
            else
            {
                for (int k = 0; k < i; k++) { d[k] /= scale; h += d[k] * d[k]; }
                double f = d[i - 1], g = Math.Sqrt(h);
                if (f > 0) g = -g;
                e[i] = scale * g; h -= f * g; d[i - 1] = f - g;
                for (int j = 0; j < i; j++) e[j] = 0;
                for (int j = 0; j < i; j++)
                {
                    f = d[j]; V[j, i] = f; g = e[j] + V[j, j] * f;
                    for (int k = j + 1; k <= i - 1; k++) { g += V[k, j] * d[k]; e[k] += V[k, j] * f; }
                    e[j] = g;
                }
                f = 0;
                for (int j = 0; j < i; j++) { e[j] /= h; f += e[j] * d[j]; }
                double hh = f / (h + h);
                for (int j = 0; j < i; j++) e[j] -= hh * d[j];
                for (int j = 0; j < i; j++)
                {
                    f = d[j]; g = e[j];
                    for (int k = j; k <= i - 1; k++) V[k, j] -= f * e[k] + g * d[k];
                    d[j] = V[i - 1, j]; V[i, j] = 0;
                }
            }
            d[i] = h;
        }
        for (int i = 0; i < n - 1; i++)
        {
            V[n - 1, i] = V[i, i]; V[i, i] = 1;
            double h = d[i + 1];
            if (h != 0)
            {
                for (int k = 0; k <= i; k++) d[k] = V[k, i + 1] / h;
                for (int j = 0; j <= i; j++)
                {
                    double g = 0;
                    for (int k = 0; k <= i; k++) g += V[k, i + 1] * V[k, j];
                    for (int k = 0; k <= i; k++) V[k, j] -= g * d[k];
                }
            }
            for (int k = 0; k <= i; k++) V[k, i + 1] = 0;
        }
        for (int j = 0; j < n; j++) { d[j] = V[n - 1, j]; V[n - 1, j] = 0; }
        V[n - 1, n - 1] = 1; e[0] = 0;

        // ---- tql2 ----
        for (int i = 1; i < n; i++) e[i - 1] = e[i];
        e[n - 1] = 0;
        double ff = 0, tst1 = 0, eps = Math.Pow(2, -52);
        for (int l = 0; l < n; l++)
        {
            tst1 = Math.Max(tst1, Math.Abs(d[l]) + Math.Abs(e[l]));
            int m = l;
            while (m < n) { if (Math.Abs(e[m]) <= eps * tst1) break; m++; }
            if (m >= n) m = n - 1;
            if (m > l)
            {
                int iter = 0;
                do
                {
                    iter++;
                    double g = d[l];
                    double p = (d[l + 1] - g) / (2 * e[l]);
                    double r = Hypot(p, 1);
                    if (p < 0) r = -r;
                    d[l] = e[l] / (p + r);
                    d[l + 1] = e[l] * (p + r);
                    double dl1 = d[l + 1];
                    double h = g - d[l];
                    for (int i = l + 2; i < n; i++) d[i] -= h;
                    ff += h;
                    p = d[m];
                    double c = 1, c2 = c, c3 = c, el1 = e[l + 1], s = 0, s2 = 0;
                    for (int i = m - 1; i >= l; i--)
                    {
                        c3 = c2; c2 = c; s2 = s;
                        g = c * e[i]; h = c * p; r = Hypot(p, e[i]);
                        e[i + 1] = s * r; s = e[i] / r; c = p / r;
                        p = c * d[i] - s * g;
                        d[i + 1] = h + s * (c * g + s * d[i]);
                        for (int k = 0; k < n; k++)
                        {
                            h = V[k, i + 1];
                            V[k, i + 1] = s * V[k, i] + c * h;
                            V[k, i] = c * V[k, i] - s * h;
                        }
                    }
                    p = -s * s2 * c3 * el1 * e[l] / dl1;
                    e[l] = s * p; d[l] = c * p;
                } while (Math.Abs(e[l]) > eps * tst1 && iter < 200);
            }
            d[l] += ff; e[l] = 0;
        }
        // sắp xếp tăng dần
        for (int i = 0; i < n - 1; i++)
        {
            int k = i; double p = d[i];
            for (int j = i + 1; j < n; j++) if (d[j] < p) { k = j; p = d[j]; }
            if (k != i)
            {
                d[k] = d[i]; d[i] = p;
                for (int j = 0; j < n; j++) (V[j, i], V[j, k]) = (V[j, k], V[j, i]);
            }
        }
        v = V;
    }

    private static double Hypot(double a, double b)
    {
        a = Math.Abs(a); b = Math.Abs(b);
        if (a > b) { double r = b / a; return a * Math.Sqrt(1 + r * r); }
        if (b > 0) { double r = a / b; return b * Math.Sqrt(1 + r * r); }
        return 0;
    }

    /// <summary>Phân tích Cholesky tại chỗ a = L·Lᵀ (L lưu ở tam giác dưới, tam giác trên đặt 0). false nếu không xác định dương.</summary>
    public static bool Cholesky(double[,] a)
    {
        int n = a.GetLength(0);
        double maxd = 0;
        for (int i = 0; i < n; i++) maxd = Math.Max(maxd, Math.Abs(a[i, i]));
        for (int j = 0; j < n; j++)
        {
            double s = a[j, j];
            for (int k = 0; k < j; k++) s -= a[j, k] * a[j, k];
            if (s <= maxd * 1e-15 || !double.IsFinite(s)) return false;
            double ljj = Math.Sqrt(s);
            a[j, j] = ljj;
            for (int i = j + 1; i < n; i++)
            {
                double t = a[i, j];
                for (int k = 0; k < j; k++) t -= a[i, k] * a[j, k];
                a[i, j] = t / ljj;
            }
            for (int i = 0; i < j; i++) a[i, j] = 0;
        }
        return true;
    }

    /// <summary>Bài toán trị riêng tổng quát đầy đủ dạng độ mềm: F·M·φ = μ·φ, F đối xứng xác định dương (n×n), M đối xứng nửa xác định dương.
    /// Trả về ω² tăng dần (bỏ các trị μ ≈ 0 – bậc tự do không khối lượng) và dạng φ (cột) chuẩn hoá φᵀMφ = 1.</summary>
    public static (double[] omega2, double[,] phi) SolveDense(double[,] F, double[,] M)
    {
        int n = F.GetLength(0);
        var R = (double[,])F.Clone();
        for (int i = 0; i < n; i++) for (int j = 0; j < i; j++) { double s = 0.5 * (R[i, j] + R[j, i]); R[i, j] = s; R[j, i] = s; }
        if (!Cholesky(R)) throw new InvalidOperationException("Ma trận độ mềm không xác định dương – kết cấu có thể bị biến hình.");
        // B = Rᵀ·M·R
        var MR = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int k = 0; k < n; k++)
            {
                double mik = M[i, k];
                if (mik == 0) continue;
                for (int j = 0; j <= k; j++) MR[i, j] += mik * R[k, j];   // R tam giác dưới: R[k,j] = 0 khi j > k
            }
        var B = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j <= i; j++)
            {
                double s = 0;
                for (int k = Math.Max(i, j); k < n; k++) s += R[k, i] * MR[k, j];   // Rᵀ[i,k] = R[k,i], khác 0 khi k ≥ i
                B[i, j] = s; B[j, i] = s;
            }
        SymmetricEigen(B, out var mu, out var V);
        double muMax = n > 0 ? Math.Max(mu[n - 1], 0) : 0;
        var keep = new List<int>();
        for (int k = n - 1; k >= 0; k--) if (mu[k] > muMax * 1e-12 && mu[k] > 0) keep.Add(k);   // μ giảm dần ↔ ω² tăng dần
        var w2 = new double[keep.Count];
        var phi = new double[n, keep.Count];
        for (int c = 0; c < keep.Count; c++)
        {
            int k = keep[c];
            w2[c] = 1.0 / mu[k];
            double sc = 1.0 / Math.Sqrt(mu[k]);   // φ = R·ψ có φᵀMφ = μ
            for (int i = 0; i < n; i++)
            {
                double s = 0;
                for (int j = 0; j <= i; j++) s += R[i, j] * V[j, k];
                phi[i, c] = s * sc;
            }
        }
        return (w2, phi);
    }

    /// <summary>Lặp không gian con (Bathe): p dạng thấp nhất của K·φ = ω²·M·φ.
    /// solveK(x): ghi đè x bằng K⁻¹x (K đã phân tích); mulM(x): trả về M·x; massEqs: các bậc tự do có khối lượng.
    /// Trả về ω² tăng dần và véc tơ riêng (mảng theo dạng, chiều dài n) chuẩn hoá φᵀMφ = 1.</summary>
    public static (double[] omega2, double[][] phi, int iterations, bool converged) Subspace(
        int n, Action<double[]> solveK, Func<double[], double[]> mulM, double[] diagK, double[] diagM,
        IReadOnlyList<int> massEqs, int p, double tol, int maxIter = 80)
    {
        int nm = massEqs.Count;
        p = Math.Min(p, nm);
        int q = Math.Min(nm, Math.Max(2 * p, p + 8));
        var X = new double[q][];
        for (int k = 0; k < q; k++) X[k] = new double[n];
        foreach (int i in massEqs) X[0][i] = diagM[i];
        var ratio = massEqs.OrderByDescending(i => diagM[i] / Math.Max(diagK[i], 1e-30)).ToList();
        for (int k = 1; k < q; k++)
        {
            if (k == q - 1 && q > 2)
            {
                var rnd = new Random(12345);
                foreach (int i in massEqs) X[k][i] = rnd.NextDouble() - 0.5;
            }
            else X[k][ratio[(k - 1) % ratio.Count]] = 1.0;
        }
        var lamOld = new double[q];
        var lam = new double[q];
        int it = 0; bool conv = false;
        double[][] Xb = null;
        double[,] Qv = null;
        while (it < maxIter)
        {
            it++;
            var Y = new double[q][];
            Xb = new double[q][];
            for (int k = 0; k < q; k++)
            {
                Y[k] = mulM(X[k]);
                var x = (double[])Y[k].Clone();
                solveK(x);
                Xb[k] = x;
            }
            var Kr = new double[q, q];
            var Mr = new double[q, q];
            var Y2 = new double[q][];
            for (int k = 0; k < q; k++) Y2[k] = mulM(Xb[k]);
            for (int a = 0; a < q; a++)
                for (int b = 0; b <= a; b++)
                {
                    double sk = 0, sm = 0;
                    for (int i = 0; i < n; i++) { sk += Xb[a][i] * Y[b][i]; sm += Xb[a][i] * Y2[b][i]; }
                    Kr[a, b] = Kr[b, a] = sk; Mr[a, b] = Mr[b, a] = sm;
                }
            // Kr·Q = λ·Mr·Q  ⇔  (Mr dạng độ mềm) Kr⁻¹·Mr·Q = (1/λ)·Q
            var Kinv = InverseSpd(Kr);
            (var w2, var phiR) = SolveDense(Kinv, Mr);
            int got = w2.Length;
            Qv = phiR;
            for (int k = 0; k < q; k++) lam[k] = k < got ? w2[k] : double.PositiveInfinity;
            // X mới = Xb·Q (các cột Q đã chuẩn hoá Qᵀ·Mr·Q = I)
            var Xn = new double[q][];
            for (int k = 0; k < q; k++)
            {
                Xn[k] = new double[n];
                if (k >= got) { Xn[k] = (double[])Xb[k].Clone(); continue; }
                for (int a = 0; a < q; a++)
                {
                    double c = Qv[a, k];
                    if (c == 0) continue;
                    var xa = Xb[a]; var xn = Xn[k];
                    for (int i = 0; i < n; i++) xn[i] += c * xa[i];
                }
            }
            X = Xn;
            if (it > 1)
            {
                conv = true;
                for (int k = 0; k < p; k++)
                    if (!(Math.Abs(lam[k] - lamOld[k]) <= tol * Math.Abs(lam[k]))) { conv = false; break; }
                if (conv) break;
            }
            Array.Copy(lam, lamOld, q);
        }
        int pp = Math.Min(p, lam.Count(double.IsFinite));
        var res = new double[pp][];
        var om = new double[pp];
        for (int k = 0; k < pp; k++) { om[k] = lam[k]; res[k] = X[k]; }
        return (om, res, it, conv);
    }

    /// <summary>Nghịch đảo ma trận đối xứng xác định dương nhỏ (Cholesky).</summary>
    public static double[,] InverseSpd(double[,] a)
    {
        int n = a.GetLength(0);
        var L = (double[,])a.Clone();
        for (int i = 0; i < n; i++) for (int j = 0; j < i; j++) { double s = 0.5 * (L[i, j] + L[j, i]); L[i, j] = s; L[j, i] = s; }
        if (!Cholesky(L)) return Dense.Inverse(a);
        var inv = new double[n, n];
        var col = new double[n];
        for (int c = 0; c < n; c++)
        {
            Array.Clear(col);
            col[c] = 1;
            for (int i = 0; i < n; i++) { double s = col[i]; for (int k = 0; k < i; k++) s -= L[i, k] * col[k]; col[i] = s / L[i, i]; }
            for (int i = n - 1; i >= 0; i--) { double s = col[i]; for (int k = i + 1; k < n; k++) s -= L[k, i] * col[k]; col[i] = s / L[i, i]; }
            for (int i = 0; i < n; i++) inv[i, c] = col[i];
        }
        return inv;
    }

    /// <summary>Hệ số tương quan CQC giữa hai dạng (Der Kiureghian 1981), cùng tỷ số cản ζ:
    /// ρij = 8ζ²(1 + r)·r^1,5 / [(1 − r²)² + 4ζ²·r·(1 + r)²], r = ωj/ωi.</summary>
    public static double CqcRho(double wi, double wj, double zeta)
    {
        if (wi <= 0 || wj <= 0) return wi == wj ? 1 : 0;
        double r = wj / wi;
        double z2 = zeta * zeta;
        double num = 8 * z2 * (1 + r) * Math.Pow(r, 1.5);
        double den = (1 - r * r) * (1 - r * r) + 4 * z2 * r * (1 + r) * (1 + r);
        return den <= 0 ? 1 : num / den;
    }

    /// <summary>Ma trận tương quan: CQC (ζ) hoặc SRSS (đơn vị).</summary>
    public static double[,] Correlation(IReadOnlyList<double> omega, bool cqc, double zeta)
    {
        int n = omega.Count;
        var r = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                r[i, j] = i == j ? 1 : cqc ? CqcRho(omega[i], omega[j], zeta) : 0;
        return r;
    }

    /// <summary>Tổ hợp đáp ứng các dạng: R = √(Σi Σj ρij·ri·rj) (không dấu).</summary>
    public static double Combine(double[] r, double[,] rho)
    {
        int n = r.Length;
        double s = 0;
        for (int i = 0; i < n; i++)
        {
            double ri = r[i];
            if (ri == 0) continue;
            s += ri * ri;
            for (int j = i + 1; j < n; j++) if (r[j] != 0 && rho[i, j] != 0) s += 2 * rho[i, j] * ri * r[j];
        }
        return Math.Sqrt(Math.Max(s, 0));
    }
}
