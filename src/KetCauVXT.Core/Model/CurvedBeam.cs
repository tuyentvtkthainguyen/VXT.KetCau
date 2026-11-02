using KetCauVXT.Analysis;

namespace KetCauVXT.Model;

/// <summary>Hình học cung tròn của một dầm cong trên mặt bằng (hai đầu cùng cao độ Z).
/// Tham số t ∈ [0, 1] chạy từ đầu I (t = 0) tới đầu J (t = 1), tỷ lệ thuận với chiều dài cung.</summary>
public sealed class ArcGeometry
{
    /// <summary>Hai đầu cung (m).</summary>
    public Vec3 PI, PJ;
    /// <summary>Tâm cung trên mặt bằng (m).</summary>
    public double Cx, Cy;
    /// <summary>Cao độ của cung (m).</summary>
    public double Z;
    /// <summary>Bán kính R (m).</summary>
    public double Radius;
    /// <summary>Độ vồng có dấu f (m): dương = vồng về bên trái hướng I→J.</summary>
    public double Sagitta;
    /// <summary>Chiều dài dây cung c (m).</summary>
    public double Chord;
    /// <summary>Góc ở tâm φ (rad, dương).</summary>
    public double Angle;
    /// <summary>Góc cực của điểm I so với tâm (rad).</summary>
    public double StartAngle;
    /// <summary>Góc quét có dấu từ I tới J (rad): dương = ngược chiều kim đồng hồ.</summary>
    public double Sweep;

    /// <summary>Chiều dài cung L = R·φ (m).</summary>
    public double Length => Radius * Angle;
    /// <summary>Góc ở tâm (độ).</summary>
    public double AngleDeg => Angle * 180 / Math.PI;
    public Vec3 Center => new(Cx, Cy, Z);

    /// <summary>Điểm trên cung tại tham số t.</summary>
    public Vec3 PointAt(double t)
    {
        if (t <= 0) return PI;
        if (t >= 1) return PJ;
        double a = StartAngle + Sweep * t;
        return new Vec3(Cx + Radius * Math.Cos(a), Cy + Radius * Math.Sin(a), Z);
    }

    /// <summary>Véc tơ tiếp tuyến đơn vị (theo chiều I→J) tại tham số t.</summary>
    public Vec3 TangentAt(double t)
    {
        double a = StartAngle + Sweep * Math.Clamp(t, 0, 1);
        double sg = Sweep >= 0 ? 1 : -1;
        return new Vec3(-Math.Sin(a) * sg, Math.Cos(a) * sg, 0);
    }

    /// <summary>Điểm giữa cung.</summary>
    public Vec3 Mid => PointAt(0.5);

    /// <summary>n + 1 điểm chia đều cung (gồm hai đầu).</summary>
    public List<Vec3> Points(int n)
    {
        n = Math.Max(1, n);
        var r = new List<Vec3>(n + 1);
        for (int k = 0; k <= n; k++) r.Add(PointAt((double)k / n));
        return r;
    }

    /// <summary>Góc bắt đầu, kết thúc (độ, ngược chiều kim đồng hồ, trong [0, 360)) – dùng cho thực thể ARC của DXF.</summary>
    public (double A1, double A2) CcwDegrees()
    {
        double a1 = Sweep >= 0 ? StartAngle : StartAngle + Sweep;
        double a2 = a1 + Math.Abs(Sweep);
        static double N(double r) { double d = r * 180 / Math.PI % 360; return d < 0 ? d + 360 : d; }
        return (N(a1), N(a2));
    }

    /// <summary>Tham số t của hình chiếu điểm (x, y) lên cung; dist = khoảng cách từ điểm tới đường tròn.
    /// Trả về NaN nếu hình chiếu nằm ngoài cung.</summary>
    public double ParamOf(double x, double y, out double dist)
    {
        double dx = x - Cx, dy = y - Cy;
        double r = Math.Sqrt(dx * dx + dy * dy);
        dist = Math.Abs(r - Radius);
        if (r < 1e-12) return double.NaN;
        double rel = (Math.Atan2(dy, dx) - StartAngle) * (Sweep >= 0 ? 1 : -1);
        rel %= 2 * Math.PI;
        if (rel < 0) rel += 2 * Math.PI;
        double t = rel / Math.Abs(Sweep);
        if (t <= 1 + 1e-9) return Math.Min(t, 1);
        // gần đầu I (góc âm nhỏ)
        double back = (2 * Math.PI - rel) / Math.Abs(Sweep);
        return back < 1e-9 ? 0 : double.NaN;
    }
}

/// <summary>Một đoạn thẳng của dầm cong (dây cung nối hai điểm trên cung) – đúng như mô hình phân tích và file xuất ETABS/SAP2000.</summary>
public sealed class CurveSegment
{
    /// <summary>Thứ tự đoạn (0 … Count − 1).</summary>
    public int K;
    public int Count;
    /// <summary>Tham số cung đầu/cuối đoạn.</summary>
    public double T0, T1;
    public Vec3 A, B;
    /// <summary>Chiều dài dây cung của đoạn (m).</summary>
    public double Chord;
    /// <summary>Chiều dài cung tương ứng (m).</summary>
    public double ArcLen;
    /// <summary>Hệ số quy tải theo cung về dây cung đoạn ΔS/c.</summary>
    public double LoadScale => Chord > 1e-12 ? ArcLen / Chord : 1;
}

/// <summary>Dầm cong trên mặt bằng (dầm biên ban công cong, mặt tiền cong…): định nghĩa bằng độ vồng
/// <see cref="FrameElement.CurveMidOffset"/> giữa dây cung hai nút I–J (cùng cao độ).
/// <list type="bullet">
/// <item>Phân tích: chia thành các phần tử thẳng nối các điểm trên cung (nút trung gian nội bộ, không lưu vào dự án);
///   nội lực trả về theo chiều dài cung, quy về hệ trục tiếp tuyến của cung tại từng mặt cắt.</item>
/// <item>Tải phân bố (bản thân, tải trên thanh, tường, sàn) tính theo chiều dài cung: <see cref="Project.Length"/> trả về chiều dài cung.</item>
/// <item>Xuất ETABS/SAP2000: các đoạn thẳng Id_1…Id_n cùng điểm trung gian, khớp với mô hình tính.</item>
/// </list></summary>
public static class CurvedBeam
{
    /// <summary>Góc ở tâm lớn nhất của một đoạn chia tự động (độ).</summary>
    public const double MaxSegmentAngleDeg = 10;
    /// <summary>Số đoạn chia tối đa.</summary>
    public const int MaxSegments = 72;

    /// <summary>Hình học cung của thanh; null nếu thanh thẳng hoặc khai báo cong không hợp lệ (khi đó coi như thẳng).</summary>
    public static ArcGeometry Arc(Project p, FrameElement f)
    {
        if (f == null || !f.IsCurved || f.IsVerticalKind) return null;
        var a = p.Node(f.NodeI).P; var b = p.Node(f.NodeJ).P;
        if (Math.Abs(a.Z - b.Z) > 1e-3) return null;
        return Arc(a, b, f.CurveMidOffset);
    }

    /// <summary>Cung qua hai điểm a, b (lấy Z của a) với độ vồng có dấu s; null nếu suy biến.</summary>
    public static ArcGeometry Arc(Vec3 a, Vec3 b, double s)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double c = Math.Sqrt(dx * dx + dy * dy);
        if (c < 1e-6 || Math.Abs(s) < 1e-6) return null;
        double ux = dx / c, uy = dy / c, nx = -uy, ny = ux;   // n: pháp tuyến trái
        double R = (c * c / 4 + s * s) / (2 * Math.Abs(s));
        double mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2;
        double sg = Math.Sign(s);
        // tâm = điểm giữa cung − n·sign(s)·R
        double cx = mx + nx * (s - sg * R), cy = my + ny * (s - sg * R);
        double phi = 4 * Math.Atan(2 * Math.Abs(s) / c);
        return new ArcGeometry
        {
            PI = a, PJ = new Vec3(b.X, b.Y, a.Z), Cx = cx, Cy = cy, Z = a.Z, Radius = R, Sagitta = s, Chord = c, Angle = phi,
            StartAngle = Math.Atan2(a.Y - cy, a.X - cx), Sweep = -sg * phi
        };
    }

    /// <summary>Số đoạn thẳng chia cung: f.CurveSegments nếu &gt; 0, ngược lại mỗi đoạn ≤ 10° (tối thiểu 2 đoạn).</summary>
    public static int SegmentCount(FrameElement f, ArcGeometry arc)
    {
        if (arc == null) return 1;
        if (f != null && f.CurveSegments > 0) return Math.Clamp(f.CurveSegments, 1, MaxSegments);
        return Math.Clamp((int)Math.Ceiling(arc.AngleDeg / MaxSegmentAngleDeg - 1e-9), 2, MaxSegments);
    }

    /// <summary>Đường gấp khúc của thanh: dầm cong → n + 1 điểm trên cung; thanh thẳng → 2 điểm I, J.</summary>
    public static List<Vec3> Discretize(Project p, FrameElement f)
    {
        var arc = Arc(p, f);
        if (arc == null) return new List<Vec3> { p.Node(f.NodeI).P, p.Node(f.NodeJ).P };
        return arc.Points(SegmentCount(f, arc));
    }

    /// <summary>Số đoạn chia dùng trong phân tích (và khi xuất ETABS/SAP2000): max(số đoạn cung, số đoạn thanh vát).</summary>
    public static int AnalysisSegmentCount(Project p, FrameElement f, ArcGeometry arc)
    {
        int n = SegmentCount(f, arc);
        try { if (p.Section(f.Section).IsTapered) n = Math.Max(n, Math.Max(2, p.Steel.TaperSegments)); }
        catch (KeyNotFoundException) { }
        return n;
    }

    /// <summary>Các đoạn thẳng của dầm cong đúng như mô hình phân tích (chia đều theo cung); null nếu thanh thẳng.</summary>
    public static List<CurveSegment> Segments(Project p, FrameElement f)
    {
        var arc = Arc(p, f);
        if (arc == null) return null;
        int n = AnalysisSegmentCount(p, f, arc);
        var res = new List<CurveSegment>(n);
        for (int k = 0; k < n; k++)
        {
            double t0 = (double)k / n, t1 = (double)(k + 1) / n;
            var a = arc.PointAt(t0); var b = arc.PointAt(t1);
            res.Add(new CurveSegment { K = k, Count = n, T0 = t0, T1 = t1, A = a, B = b, Chord = (b - a).Length, ArcLen = arc.Length * (t1 - t0) });
        }
        return res;
    }

    /// <summary>Chia một đoạn tải tuyến tính trên dầm cong (vị trí tương đối r0…r1 theo cung, cường độ q0…q1 kN/m theo cung)
    /// vào các đoạn thẳng: (đoạn, vị trí tương đối đầu/cuối trong đoạn, cường độ theo dây cung đoạn = q·ΔS/c) – bảo toàn tổng tải.</summary>
    public static List<(CurveSegment Seg, double Ra, double Rb, double Qa, double Qb)> SplitLoad(List<CurveSegment> segs, double r0, double r1, double q0, double q1)
    {
        var res = new List<(CurveSegment Seg, double Ra, double Rb, double Qa, double Qb)>();
        if (r1 - r0 < 1e-12) return res;
        foreach (var sg in segs)
        {
            double a = Math.Max(r0, sg.T0), b = Math.Min(r1, sg.T1);
            if (b - a < 1e-9) continue;
            double qa = q0 + (q1 - q0) * (a - r0) / (r1 - r0), qb = q0 + (q1 - q0) * (b - r0) / (r1 - r0);
            double k = sg.LoadScale;
            res.Add((sg, (a - sg.T0) / (sg.T1 - sg.T0), (b - sg.T0) / (sg.T1 - sg.T0), qa * k, qb * k));
        }
        return res;
    }

    /// <summary>Đường gấp khúc mịn để vẽ (mỗi đoạn ≤ maxDeg độ); thanh thẳng → 2 điểm.</summary>
    public static List<Vec3> DrawPoints(Project p, FrameElement f, double maxDeg = 3)
    {
        var arc = Arc(p, f);
        if (arc == null) return new List<Vec3> { p.Node(f.NodeI).P, p.Node(f.NodeJ).P };
        return arc.Points(Math.Clamp((int)Math.Ceiling(arc.AngleDeg / Math.Max(maxDeg, 0.5)), 4, 180));
    }

    /// <summary>Bán kính từ dây cung c và độ vồng s: R = (c²/4 + s²)/(2|s|).</summary>
    public static double RadiusFromSagitta(double chord, double s) =>
        Math.Abs(s) < 1e-9 ? double.PositiveInfinity : (chord * chord / 4 + s * s) / (2 * Math.Abs(s));

    /// <summary>Chiều dài cung từ dây cung c và độ vồng s: L = R·4·atan(2|s|/c).</summary>
    public static double ArcLength(double chord, double s) =>
        Math.Abs(s) < 1e-9 ? chord : RadiusFromSagitta(chord, s) * 4 * Math.Atan(2 * Math.Abs(s) / chord);

    /// <summary>Độ vồng của cung nhỏ bán kính R trên dây cung c: f = R − √(R² − c²/4); side = +1 (trái) / −1 (phải).
    /// NaN nếu R &lt; c/2.</summary>
    public static double SagittaFromRadius(double chord, double radius, int side = 1)
    {
        if (radius < chord / 2 - 1e-9) return double.NaN;
        double s = radius - Math.Sqrt(Math.Max(radius * radius - chord * chord / 4, 0));
        return side < 0 ? -s : s;
    }

    /// <summary>Độ vồng có dấu của cung đi qua (x1, y1) → (x2, y2) và điểm trung gian (xm, ym) bất kỳ trên cung;
    /// 0 nếu ba điểm thẳng hàng.</summary>
    public static double SagittaThrough(double x1, double y1, double x2, double y2, double xm, double ym)
    {
        double dx = x2 - x1, dy = y2 - y1;
        double c = Math.Sqrt(dx * dx + dy * dy);
        if (c < 1e-9) return 0;
        double ux = dx / c, uy = dy / c, nx = -uy, ny = ux;
        double mx = (x1 + x2) / 2, my = (y1 + y2) / 2;
        double w = (xm - mx) * ux + (ym - my) * uy;
        double h = (xm - mx) * nx + (ym - my) * ny;
        if (Math.Abs(h) < 1e-6) return 0;
        double k = (w * w + h * h - c * c / 4) / (2 * h);   // tâm = M + n·k
        double R = Math.Sqrt(c * c / 4 + k * k);
        return k + Math.Sign(h) * R;
    }

    /// <summary>Độ vồng của đoạn cung [t0, t1] (cùng bán kính, cùng phía).</summary>
    public static double SagittaOfPiece(ArcGeometry a, double t0, double t1)
    {
        double half = a.Angle * Math.Abs(t1 - t0) / 2;
        return Math.Sign(a.Sagitta) * a.Radius * (1 - Math.Cos(half));
    }

    /// <summary>Hệ trục địa phương (hàng e1, e2, e3) của dầm cong tại tham số t: e1 theo tiếp tuyến cung,
    /// e2, e3 theo quy ước ETABS như thanh thẳng cùng góc xoay.</summary>
    public static double[,] AxesAt(ArcGeometry a, double t, double angleDeg)
    {
        var p0 = a.PointAt(t);
        return ElementMatrices.LocalAxes(p0, p0 + a.TangentAt(t), angleDeg);
    }

    /// <summary>Giao điểm của đoạn thẳng (ax, ay)–(bx, by) với cung (không tính hai đầu cung).</summary>
    public static List<(double X, double Y)> Intersections(ArcGeometry a, double ax, double ay, double bx, double by)
    {
        var res = new List<(double X, double Y)>();
        double dx = bx - ax, dy = by - ay;
        double fx = ax - a.Cx, fy = ay - a.Cy;
        double A = dx * dx + dy * dy, B = 2 * (fx * dx + fy * dy), C = fx * fx + fy * fy - a.Radius * a.Radius;
        if (A < 1e-12) return res;
        double disc = B * B - 4 * A * C;
        if (disc < 0) return res;
        double sq = Math.Sqrt(disc);
        foreach (double lam in new[] { (-B - sq) / (2 * A), (-B + sq) / (2 * A) })
        {
            if (lam < -1e-6 || lam > 1 + 1e-6) continue;
            double x = ax + dx * lam, y = ay + dy * lam;
            double t = a.ParamOf(x, y, out _);
            if (double.IsNaN(t) || t < 1e-4 || t > 1 - 1e-4) continue;
            if (res.Any(q => Math.Abs(q.X - x) < 1e-6 && Math.Abs(q.Y - y) < 1e-6)) continue;
            res.Add((x, y));
        }
        return res;
    }

    /// <summary>Kiểm tra khai báo cong; trả về thông báo lỗi hoặc null nếu hợp lệ.</summary>
    public static string Validate(Project p, FrameElement f)
    {
        if (!f.IsCurved) return null;
        if (f.IsVerticalKind) return $"Thanh {f.Id}: cột không được khai báo cong (chỉ dầm nằm ngang)";
        var a = p.Node(f.NodeI).P; var b = p.Node(f.NodeJ).P;
        if (Math.Abs(a.Z - b.Z) > 1e-3) return $"Thanh {f.Id}: dầm cong phải nằm ngang (hai đầu cùng cao độ)";
        double c = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
        if (c < 1e-3) return null; // lỗi chiều dài 0 báo riêng
        if (Math.Abs(f.CurveMidOffset) > c + 1e-9) return $"Thanh {f.Id}: độ vồng {Math.Abs(f.CurveMidOffset):0.000} m lớn hơn dây cung {c:0.000} m";
        return null;
    }

    /// <summary>Mô tả ngắn: "Dầm cong R=…, chiều dài cung L=…".</summary>
    public static string Describe(ArcGeometry a) =>
        a == null ? "" : $"Dầm cong R={a.Radius:0.000} m, chiều dài cung L={a.Length:0.000} m (dây cung {a.Chord:0.000} m, độ vồng {Math.Abs(a.Sagitta):0.000} m, góc ở tâm {a.AngleDeg:0.#}°)";
}
