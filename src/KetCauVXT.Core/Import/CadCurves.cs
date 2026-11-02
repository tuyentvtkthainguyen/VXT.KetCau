namespace KetCauVXT.Import;

/// <summary>Chia đường cong CAD (cung lồi bulge, ELLIPSE, SPLINE) thành đa tuyến – dùng chung cho bộ đọc DXF và bộ đọc DWG.
/// Toạ độ theo đơn vị bản vẽ (chưa đổi sang m), góc/tham số theo radian.</summary>
public static class CadCurves
{
    /// <summary>Bước chia ELLIPSE theo tham số (độ): elip kín → 36 điểm.</summary>
    public const double EllipseStepDeg = 10.0;
    /// <summary>Số đoạn chia cho mỗi nhịp nút của SPLINE (giữa hai giá trị nút liên tiếp).</summary>
    public const int SplineSegmentsPerSpan = 8;
    /// <summary>Số điểm tối đa của một SPLINE sau khi chia.</summary>
    public const int SplineMaxPoints = 400;

    /// <summary>Tâm, bán kính của cung lồi (bulge) từ (x1, y1) tới (x2, y2). false nếu suy biến.</summary>
    public static bool BulgeCircle(double x1, double y1, double x2, double y2, double bulge, out double cx, out double cy, out double r)
    {
        cx = cy = r = 0;
        double dx = x2 - x1, dy = y2 - y1, c = Math.Sqrt(dx * dx + dy * dy);
        if (c < 1e-12 || Math.Abs(bulge) < 1e-9) return false;
        bulge = Math.Clamp(bulge, -1000, 1000);
        double k = (1 - bulge * bulge) / (4 * bulge);
        cx = (x1 + x2) / 2 - dy * k; cy = (y1 + y2) / 2 + dx * k;
        r = Math.Sqrt((x1 - cx) * (x1 - cx) + (y1 - cy) * (y1 - cy));
        return r > 1e-12 && !double.IsNaN(r) && !double.IsInfinity(r);
    }

    /// <summary>Đường tròn qua 3 điểm; false nếu thẳng hàng.</summary>
    public static bool CircleThrough((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, out double cx, out double cy, out double r)
    {
        cx = cy = r = 0;
        double d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
        double scale = Math.Max(1e-12, Math.Max(Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y), Math.Abs(c.X - a.X) + Math.Abs(c.Y - a.Y)));
        if (Math.Abs(d) < 1e-9 * scale * scale) return false;
        double a2 = a.X * a.X + a.Y * a.Y, b2 = b.X * b.X + b.Y * b.Y, c2 = c.X * c.X + c.Y * c.Y;
        cx = (a2 * (b.Y - c.Y) + b2 * (c.Y - a.Y) + c2 * (a.Y - b.Y)) / d;
        cy = (a2 * (c.X - b.X) + b2 * (a.X - c.X) + c2 * (b.X - a.X)) / d;
        r = Math.Sqrt((a.X - cx) * (a.X - cx) + (a.Y - cy) * (a.Y - cy));
        return !double.IsNaN(r) && !double.IsInfinity(r);
    }

    /// <summary>ELLIPSE: tâm (cx, cy), đầu trục lớn (mx, my) so với tâm, tỷ số trục nhỏ/lớn ratio, tham số t0 → t1 (rad, ngược chiều kim đồng hồ quanh pháp tuyến).
    /// Điểm = C + cos t·M + sin t·N, N = (Z × M)·ratio (pháp tuyến ngược trục Z: flipped → N đổi dấu).
    /// Elip kín (t1 − t0 = 2π): 36 điểm, closed = true; cung elip: ⌈Δt/10°⌉ + 1 điểm.</summary>
    public static List<(double X, double Y)> Ellipse(double cx, double cy, double mx, double my, double ratio, double t0, double t1, bool flipped, out bool closed)
    {
        var res = new List<(double X, double Y)>();
        closed = false;
        double ma = Math.Sqrt(mx * mx + my * my);
        if (ma < 1e-12 || double.IsNaN(ma)) return res;
        if (ratio <= 0 || double.IsNaN(ratio) || ratio > 1e6) ratio = 1;
        double sg = flipped ? -1 : 1;
        double nx = -my * ratio * sg, ny = mx * ratio * sg;
        double span = t1 - t0;
        if (double.IsNaN(span) || double.IsInfinity(span)) span = 2 * Math.PI;
        span %= 2 * Math.PI;
        if (span < 0) span += 2 * Math.PI;
        if (span < 1e-9 || Math.Abs(span - 2 * Math.PI) < 1e-6) { span = 2 * Math.PI; closed = true; }
        int n = Math.Max(4, (int)Math.Ceiling(span * 180 / Math.PI / EllipseStepDeg - 1e-9));
        int last = closed ? n - 1 : n;
        for (int k = 0; k <= last; k++)
        {
            double t = t0 + span * k / n;
            res.Add((cx + Math.Cos(t) * mx + Math.Sin(t) * nx, cy + Math.Cos(t) * my + Math.Sin(t) * ny));
        }
        return res;
    }

    /// <summary>SPLINE → đa tuyến.
    /// <list type="bullet">
    /// <item>Có điểm điều khiển: B-spline (hữu tỷ nếu có trọng số) bậc p theo vectơ nút; vectơ nút sai số lượng (≠ n + p + 1) → thay bằng nút đều kẹp hai đầu.
    ///   Chia mỗi nhịp nút khác 0 thành 8 đoạn (tối đa 400 điểm); bậc 1 → chính các điểm điều khiển.</item>
    /// <item>Chỉ có điểm khớp (fit points): đường cong Catmull-Rom đi qua các điểm khớp (8 đoạn mỗi khoảng).</item>
    /// </list>
    /// closed = cờ kín (70 &amp; 1) hoặc điểm đầu trùng điểm cuối (khi đó bỏ điểm cuối trùng).</summary>
    public static List<(double X, double Y)> Spline(int degree, IList<double> knots, IList<(double X, double Y)> ctrl, IList<double> weights,
                                                    IList<(double X, double Y)> fit, bool closedFlag, out bool closed)
    {
        var res = new List<(double X, double Y)>();
        closed = false;
        ctrl ??= Array.Empty<(double X, double Y)>();
        fit ??= Array.Empty<(double X, double Y)>();
        if (ctrl.Count >= 2) res = EvalBSpline(degree, knots, ctrl, weights);
        else if (fit.Count >= 2) res = CatmullRom(fit, closedFlag);
        if (res.Count < 2) return res;
        double tol = 1e-9 * Math.Max(1.0, res.Max(p => Math.Max(Math.Abs(p.X), Math.Abs(p.Y))));
        bool same = Math.Abs(res[0].X - res[^1].X) <= tol && Math.Abs(res[0].Y - res[^1].Y) <= tol;
        if (same && res.Count > 2) { res.RemoveAt(res.Count - 1); closed = true; }
        else if (closedFlag && res.Count > 2) closed = true;
        return res;
    }

    private static List<(double X, double Y)> EvalBSpline(int degree, IList<double> knots, IList<(double X, double Y)> ctrl, IList<double> weights)
    {
        int n = ctrl.Count;
        int p = Math.Clamp(degree, 1, n - 1);
        var res = new List<(double X, double Y)>();
        if (p == 1) { foreach (var c in ctrl) res.Add(c); return res; }
        bool rational = weights != null && weights.Count == n && weights.All(w => w > 0 && !double.IsNaN(w)) && weights.Any(w => Math.Abs(w - 1) > 1e-12);
        double[] U;
        bool knotsOk = knots != null && knots.Count == n + p + 1;
        if (knotsOk)
            for (int i = 1; i < knots.Count; i++)
                if (knots[i] < knots[i - 1] - 1e-12 || double.IsNaN(knots[i])) { knotsOk = false; break; }
        if (knotsOk && knots[n] - knots[p] < 1e-12) knotsOk = false;
        if (knotsOk) U = knots.ToArray();
        else
        {
            // nút đều kẹp hai đầu: p + 1 số 0, n − p − 1 nút trong, p + 1 số 1
            U = new double[n + p + 1];
            int inner = n - p;
            for (int i = 0; i < U.Length; i++) U[i] = i <= p ? 0 : i >= n ? 1 : (double)(i - p) / inner;
        }
        double u0 = U[p], u1 = U[n];
        // các tham số chia: mỗi nhịp nút khác 0 chia 8 đoạn
        var ts = new List<double>();
        int spans = 0;
        for (int k = p; k < n; k++) if (U[k + 1] - U[k] > 1e-12) spans++;
        int per = Math.Max(1, Math.Min(SplineSegmentsPerSpan, (SplineMaxPoints - 1) / Math.Max(1, spans)));
        for (int k = p; k < n; k++)
        {
            double a = U[k], b = U[k + 1];
            if (b - a <= 1e-12) continue;
            for (int s = 0; s < per; s++) ts.Add(a + (b - a) * s / per);
        }
        ts.Add(u1);
        var d = new double[p + 1, 3];
        foreach (double t in ts)
        {
            // nhịp k: U[k] ≤ t < U[k+1] (t = u1 → nhịp khác 0 cuối cùng)
            int k = n - 1;
            if (t < u1)
                for (int i = p; i < n; i++) if (t >= U[i] && t < U[i + 1]) { k = i; break; }
            while (k > p && U[k + 1] - U[k] <= 1e-12) k--;
            for (int j = 0; j <= p; j++)
            {
                var c = ctrl[j + k - p];
                double w = rational ? weights[j + k - p] : 1.0;
                d[j, 0] = c.X * w; d[j, 1] = c.Y * w; d[j, 2] = w;
            }
            for (int r = 1; r <= p; r++)
                for (int j = p; j >= r; j--)
                {
                    double den = U[j + 1 + k - r] - U[j + k - p];
                    double alpha = den > 1e-15 ? (t - U[j + k - p]) / den : 0;
                    for (int q = 0; q < 3; q++) d[j, q] = (1 - alpha) * d[j - 1, q] + alpha * d[j, q];
                }
            double ww = Math.Abs(d[p, 2]) > 1e-15 ? d[p, 2] : 1;
            var pt = (X: d[p, 0] / ww, Y: d[p, 1] / ww);
            if (res.Count == 0 || Math.Abs(res[^1].X - pt.X) > 1e-12 || Math.Abs(res[^1].Y - pt.Y) > 1e-12) res.Add(pt);
        }
        return res;
    }

    private static List<(double X, double Y)> CatmullRom(IList<(double X, double Y)> f, bool closed)
    {
        var res = new List<(double X, double Y)>();
        int m = f.Count;
        if (m == 2) { res.Add(f[0]); res.Add(f[1]); return res; }
        int spans = closed ? m : m - 1;
        int per = Math.Max(1, Math.Min(SplineSegmentsPerSpan, (SplineMaxPoints - 1) / spans));
        (double X, double Y) P(int i) => closed ? f[((i % m) + m) % m] : f[Math.Clamp(i, 0, m - 1)];
        for (int i = 0; i < spans; i++)
        {
            var p0 = P(i - 1); var p1 = P(i); var p2 = P(i + 1); var p3 = P(i + 2);
            for (int s = 0; s < per; s++)
            {
                double t = (double)s / per, t2 = t * t, t3 = t2 * t;
                double x = 0.5 * (2 * p1.X + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
                double y = 0.5 * (2 * p1.Y + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);
                res.Add((x, y));
            }
        }
        res.Add(closed ? f[0] : f[m - 1]);
        return res;
    }
}
