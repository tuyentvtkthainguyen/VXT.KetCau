namespace KetCauVXT.Analysis;

/// <summary>Ma trận đối xứng lưu dạng skyline (profile) và giải bằng phân tích LDLᵀ (thuật toán Crout – Bathe).
/// Đủ nhanh cho nhà ≤ 7 tầng (vài nghìn bậc tự do).</summary>
public sealed class SkylineMatrix
{
    public readonly int N;
    private readonly int[] _first;   // hàng nhỏ nhất có phần tử khác 0 ở cột j
    private readonly int[] _ptr;     // vị trí bắt đầu cột j trong mảng _a
    private readonly double[] _a;
    private bool _factored;

    public SkylineMatrix(int n, int[] firstRow)
    {
        N = n;
        _first = (int[])firstRow.Clone();
        _ptr = new int[n + 1];
        for (int j = 0; j < n; j++)
        {
            if (_first[j] > j || _first[j] < 0) _first[j] = j;
            _ptr[j + 1] = _ptr[j] + (j - _first[j] + 1);
        }
        _a = new double[_ptr[n]];
    }

    public long StorageSize => _a.LongLength;

    private int Idx(int i, int j) => _ptr[j] + (i - _first[j]);

    /// <summary>Cộng giá trị vào phần tử (i,j) với i ≤ j.</summary>
    public void Add(int i, int j, double v)
    {
        if (i > j) (i, j) = (j, i);
        if (i < _first[j]) throw new InvalidOperationException($"Phần tử ({i},{j}) nằm ngoài profile");
        _a[Idx(i, j)] += v;
    }

    public double Get(int i, int j)
    {
        if (i > j) (i, j) = (j, i);
        return i < _first[j] ? 0.0 : _a[Idx(i, j)];
    }

    /// <summary>Phân tích LDLᵀ tại chỗ. Ném lỗi kèm số phương trình nếu ma trận suy biến (kết cấu biến hình).</summary>
    public void Factor()
    {
        if (_factored) return;
        double maxDiag = 0;
        for (int j = 0; j < N; j++) maxDiag = Math.Max(maxDiag, Math.Abs(_a[Idx(j, j)]));
        double tol = Math.Max(maxDiag, 1.0) * 1e-13;

        for (int j = 0; j < N; j++)
        {
            int fj = _first[j];
            // g_ij = a_ij − Σ l_ki g_kj
            for (int i = fj + 1; i < j; i++)
            {
                int m = Math.Max(_first[i], fj);
                double s = 0;
                int pi = _ptr[i] - _first[i];
                int pj = _ptr[j] - fj;
                for (int k = m; k < i; k++) s += _a[pi + k] * _a[pj + k];
                _a[pj + i] -= s;
            }
            // l_ij = g_ij / d_i ; d_j = a_jj − Σ g_ij l_ij
            double d = _a[Idx(j, j)];
            int pjj = _ptr[j] - fj;
            for (int i = fj; i < j; i++)
            {
                double g = _a[pjj + i];
                double l = g / _a[Idx(i, i)];
                _a[pjj + i] = l;
                d -= g * l;
            }
            if (d <= tol)
                throw new SingularStiffnessException(j, d);
            _a[Idx(j, j)] = d;
        }
        _factored = true;
    }

    /// <summary>Giải K x = b (b bị ghi đè bởi nghiệm).</summary>
    public void Solve(double[] b)
    {
        if (!_factored) Factor();
        // L y = b
        for (int j = 0; j < N; j++)
        {
            int fj = _first[j], pj = _ptr[j] - fj;
            double s = b[j];
            for (int i = fj; i < j; i++) s -= _a[pj + i] * b[i];
            b[j] = s;
        }
        // D z = y
        for (int j = 0; j < N; j++) b[j] /= _a[Idx(j, j)];
        // Lᵀ x = z
        for (int j = N - 1; j >= 0; j--)
        {
            int fj = _first[j], pj = _ptr[j] - fj;
            double bj = b[j];
            for (int i = fj; i < j; i++) b[i] -= _a[pj + i] * bj;
        }
    }
}

public class SingularStiffnessException : Exception
{
    public int Equation { get; }
    public SingularStiffnessException(int eq, double pivot)
        : base($"Ma trận độ cứng suy biến tại phương trình {eq} (pivot = {pivot:E2}). Kết cấu có thể bị biến hình: thiếu liên kết gối, thanh treo, hoặc giải phóng liên kết quá mức.")
    { Equation = eq; }
}

/// <summary>Đại số tuyến tính nhỏ cho ma trận phần tử.</summary>
public static class Dense
{
    public static double[,] Mul(double[,] a, double[,] b)
    {
        int n = a.GetLength(0), m = b.GetLength(1), k = a.GetLength(1);
        var c = new double[n, m];
        for (int i = 0; i < n; i++)
            for (int p = 0; p < k; p++)
            {
                double aip = a[i, p];
                if (aip == 0) continue;
                for (int j = 0; j < m; j++) c[i, j] += aip * b[p, j];
            }
        return c;
    }

    public static double[,] Transpose(double[,] a)
    {
        int n = a.GetLength(0), m = a.GetLength(1);
        var t = new double[m, n];
        for (int i = 0; i < n; i++) for (int j = 0; j < m; j++) t[j, i] = a[i, j];
        return t;
    }

    public static double[] Mul(double[,] a, double[] x)
    {
        int n = a.GetLength(0), m = a.GetLength(1);
        var y = new double[n];
        for (int i = 0; i < n; i++) { double s = 0; for (int j = 0; j < m; j++) s += a[i, j] * x[j]; y[i] = s; }
        return y;
    }

    public static double[] MulT(double[,] a, double[] x)
    {
        int n = a.GetLength(0), m = a.GetLength(1);
        var y = new double[m];
        for (int i = 0; i < n; i++) { double xi = x[i]; if (xi == 0) continue; for (int j = 0; j < m; j++) y[j] += a[i, j] * xi; }
        return y;
    }

    /// <summary>Nghịch đảo ma trận vuông nhỏ bằng Gauss-Jordan có chọn trụ.</summary>
    public static double[,] Inverse(double[,] m)
    {
        int n = m.GetLength(0);
        var a = (double[,])m.Clone();
        var inv = new double[n, n];
        for (int i = 0; i < n; i++) inv[i, i] = 1;
        for (int c = 0; c < n; c++)
        {
            int p = c; double best = Math.Abs(a[c, c]);
            for (int r = c + 1; r < n; r++) if (Math.Abs(a[r, c]) > best) { best = Math.Abs(a[r, c]); p = r; }
            if (best < 1e-30) throw new InvalidOperationException("Ma trận giải phóng liên kết suy biến");
            if (p != c)
                for (int j = 0; j < n; j++) { (a[c, j], a[p, j]) = (a[p, j], a[c, j]); (inv[c, j], inv[p, j]) = (inv[p, j], inv[c, j]); }
            double d = a[c, c];
            for (int j = 0; j < n; j++) { a[c, j] /= d; inv[c, j] /= d; }
            for (int r = 0; r < n; r++)
            {
                if (r == c) continue;
                double f = a[r, c];
                if (f == 0) continue;
                for (int j = 0; j < n; j++) { a[r, j] -= f * a[c, j]; inv[r, j] -= f * inv[c, j]; }
            }
        }
        return inv;
    }
}
