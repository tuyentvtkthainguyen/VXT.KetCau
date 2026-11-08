namespace KetCauVXT.Model;

// =====================================================================
// [MóngHìnhDạng] BỐ TRÍ MÓNG ĐƠN: LỆCH TÂM / XOAY GÓC DÓNG THEO MÉP CỘT, ĐƯỜNG CHUẨN (RANH ĐẤT XIÊN),
// MÓNG ĐA GIÁC BẤT KỲ CHO NHÀ CÓ HÌNH ĐẤT MÉO. Thiết kế: Design/Foundation/ShapedFooting.cs.
// =====================================================================

/// <summary>Cách dóng tâm móng theo một phương địa phương của móng (x' = phương L, y' = phương B).</summary>
public enum FootingAlign
{
    /// <summary>Tâm móng trùng tim cột</summary>
    Center = 0,
    /// <summary>Mép móng phía âm (trái / trước) thẳng mép cột phía âm – móng chìa ra phía dương</summary>
    MinFlush = 1,
    /// <summary>Mép móng phía dương (phải / sau) thẳng mép cột phía dương – móng chìa ra phía âm</summary>
    MaxFlush = 2,
    /// <summary>Tâm móng lệch so với tim cột một đoạn nhập tay (Offset)</summary>
    Offset = 3
}

/// <summary>Góc xoay của móng trên mặt bằng.</summary>
public enum FootingAngleMode
{
    /// <summary>Cạnh móng song song trục X / Y</summary>
    Global = 0,
    /// <summary>Cạnh móng song song mép cột (cột xoay góc thì móng xoay theo)</summary>
    Column = 1,
    /// <summary>Góc nhập tay (độ, ngược chiều kim đồng hồ từ trục X)</summary>
    Custom = 2,
    /// <summary>Cạnh móng song song đường chuẩn (ranh đất) gần cột nhất</summary>
    LotLine = 3
}

/// <summary>Bố trí riêng một móng đơn tại chân cột (FoundationSettings.Placements).</summary>
public class FootingPlacement
{
    public int NodeId { get; set; }
    public FootingAlign AlignX { get; set; }
    public FootingAlign AlignY { get; set; }
    /// <summary>Tâm móng − tim cột theo phương L / B của móng (m) khi Align = Offset</summary>
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public FootingAngleMode AngleMode { get; set; }
    /// <summary>Góc (độ) khi AngleMode = Custom</summary>
    public double Angle { get; set; }
    /// <summary>Móng đa giác bất kỳ: các đỉnh (dx, dy) so với tim cột, m, hệ toạ độ tổng thể; null / &lt; 3 đỉnh = móng chữ nhật L × B.
    /// Kích thước đa giác do người dùng định (chương trình chỉ kiểm tra và tính chiều cao, thép).</summary>
    public List<double[]> Polygon { get; set; }
    /// <summary>Cắt móng theo các đường chuẩn (ranh đất) – móng không vượt ra ngoài lô đất</summary>
    public bool ClipToLot { get; set; } = true;
    /// <summary>Móng lệch tâm (trọng tâm đáy lệch tim cột): nối dầm móng cân bằng tới chân cột đối diện (R1 = N·Ls/(Ls − e), áp lực đáy gần đều);
    /// false = tự cân bằng (đáy móng chịu mô men N·e)</summary>
    public bool StrapBeam { get; set; } = true;

    public bool IsPolygon => Polygon != null && Polygon.Count >= 3;

    public FootingPlacement Clone() => new()
    {
        NodeId = NodeId, AlignX = AlignX, AlignY = AlignY, OffsetX = OffsetX, OffsetY = OffsetY, AngleMode = AngleMode, Angle = Angle,
        ClipToLot = ClipToLot, StrapBeam = StrapBeam, Polygon = Polygon?.Select(v => new[] { v[0], v[1] }).ToList()
    };

    public static string AlignName(FootingAlign a) => a switch
    {
        FootingAlign.MinFlush => "Thẳng mép cột phía âm",
        FootingAlign.MaxFlush => "Thẳng mép cột phía dương",
        FootingAlign.Offset => "Lệch tâm nhập tay",
        _ => "Đúng tâm cột"
    };

    public static string AngleName(FootingAngleMode m) => m switch
    {
        FootingAngleMode.Column => "Theo mép cột",
        FootingAngleMode.Custom => "Góc nhập tay",
        FootingAngleMode.LotLine => "Theo đường chuẩn gần nhất",
        _ => "Song song trục X/Y"
    };
}

/// <summary>Đường chuẩn trên mặt bằng móng (ranh đất xiên, mép công trình bên cạnh…): đoạn thẳng (X1,Y1)–(X2,Y2), m.
/// Phía được phép đặt móng là phía chứa tim cột. Móng có ClipToLot bị cắt theo đường này (kéo dài vô hạn).</summary>
public class LotLine
{
    public string Name { get; set; } = "";
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public bool Enabled { get; set; } = true;
    /// <summary>Khoảng hở móng tới đường chuẩn (m)</summary>
    public double Clearance { get; set; }

    public double Length => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
    /// <summary>Góc đường (độ, −90…90]</summary>
    public double AngleDeg
    {
        get
        {
            double a = Math.Atan2(Y2 - Y1, X2 - X1) * 180 / Math.PI;
            while (a > 90) a -= 180;
            while (a <= -90) a += 180;
            return a;
        }
    }

    /// <summary>Khoảng cách có dấu từ điểm tới đường (dương về bên trái hướng 1→2)</summary>
    public double Side(double x, double y)
    {
        double L = Math.Max(Length, 1e-12);
        return ((X2 - X1) * (y - Y1) - (Y2 - Y1) * (x - X1)) / L;
    }

    /// <summary>Khoảng cách từ điểm tới đoạn thẳng</summary>
    public double DistanceToSegment(double x, double y)
    {
        double dx = X2 - X1, dy = Y2 - Y1, l2 = dx * dx + dy * dy;
        double t = l2 < 1e-18 ? 0 : Math.Clamp(((x - X1) * dx + (y - Y1) * dy) / l2, 0, 1);
        double px = X1 + t * dx - x, py = Y1 + t * dy - y;
        return Math.Sqrt(px * px + py * py);
    }

    public LotLine Clone() => new() { Name = Name, X1 = X1, Y1 = Y1, X2 = X2, Y2 = Y2, Enabled = Enabled, Clearance = Clearance };
}

/// <summary>Hình học đa giác phẳng cho móng: diện tích, trọng tâm, mô men quán tính, cắt nửa mặt phẳng, chữ nhật xoay.</summary>
public static class PolyGeom
{
    public readonly record struct Props(double Area, double Cx, double Cy, double Ixx, double Iyy, double Ixy);

    /// <summary>Diện tích có dấu (dương = ngược chiều kim đồng hồ)</summary>
    public static double SignedArea(IList<(double X, double Y)> p)
    {
        double a = 0;
        for (int i = 0, n = p.Count; i < n; i++) { var u = p[i]; var v = p[(i + 1) % n]; a += u.X * v.Y - v.X * u.Y; }
        return a / 2;
    }

    /// <summary>Diện tích, trọng tâm và mô men quán tính quanh trọng tâm (Ixx = ∫y²dA, Iyy = ∫x²dA, Ixy = ∫xy dA).</summary>
    public static Props Properties(IList<(double X, double Y)> p)
    {
        int n = p.Count;
        if (n < 3) return new Props(0, 0, 0, 0, 0, 0);
        double a = 0, cx = 0, cy = 0, ixx = 0, iyy = 0, ixy = 0;
        for (int i = 0; i < n; i++)
        {
            var u = p[i]; var v = p[(i + 1) % n];
            double c = u.X * v.Y - v.X * u.Y;
            a += c; cx += (u.X + v.X) * c; cy += (u.Y + v.Y) * c;
            ixx += (u.Y * u.Y + u.Y * v.Y + v.Y * v.Y) * c;
            iyy += (u.X * u.X + u.X * v.X + v.X * v.X) * c;
            ixy += (u.X * v.Y + 2 * u.X * u.Y + 2 * v.X * v.Y + v.X * u.Y) * c;
        }
        a /= 2;
        if (Math.Abs(a) < 1e-12) return new Props(0, 0, 0, 0, 0, 0);
        cx /= 6 * a; cy /= 6 * a;
        ixx /= 12; iyy /= 12; ixy /= 24;
        double s = Math.Sign(a);
        ixx = s * ixx - Math.Abs(a) * cy * cy;
        iyy = s * iyy - Math.Abs(a) * cx * cx;
        ixy = s * ixy - Math.Abs(a) * cx * cy;
        return new Props(Math.Abs(a), cx, cy, ixx, iyy, ixy);
    }

    /// <summary>Đa giác theo chiều ngược kim đồng hồ</summary>
    public static List<(double X, double Y)> Ccw(IEnumerable<(double X, double Y)> pts)
    {
        var l = pts.ToList();
        if (SignedArea(l) < 0) l.Reverse();
        return l;
    }

    /// <summary>Chữ nhật L × B xoay góc a (rad) quanh tâm (cx, cy); L theo phương x' = (cos a, sin a).</summary>
    public static List<(double X, double Y)> Rect(double cx, double cy, double L, double B, double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        var res = new List<(double, double)>();
        foreach (var (u, v) in new[] { (-L / 2, -B / 2), (L / 2, -B / 2), (L / 2, B / 2), (-L / 2, B / 2) })
            res.Add((cx + u * c - v * s, cy + u * s + v * c));
        return res;
    }

    /// <summary>Cắt đa giác bởi nửa mặt phẳng f(x,y) = nx·x + ny·y − d ≥ 0 (Sutherland–Hodgman).</summary>
    public static List<(double X, double Y)> ClipHalf(IList<(double X, double Y)> poly, double nx, double ny, double d)
    {
        var res = new List<(double X, double Y)>();
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % n];
            double fa = nx * a.X + ny * a.Y - d, fb = nx * b.X + ny * b.Y - d;
            if (fa >= 0) res.Add(a);
            if ((fa >= 0) != (fb >= 0))
            {
                double t = fa / (fa - fb);
                res.Add((a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y)));
            }
        }
        return Dedup(res);
    }

    /// <summary>Cắt đa giác bởi đa giác lồi clip (ngược chiều kim đồng hồ).</summary>
    public static List<(double X, double Y)> ClipConvex(IList<(double X, double Y)> poly, IList<(double X, double Y)> clip)
    {
        var res = poly.ToList();
        var c = Ccw(clip);
        for (int i = 0; i < c.Count && res.Count > 0; i++)
        {
            var a = c[i]; var b = c[(i + 1) % c.Count];
            // phía trái cạnh a→b: n = (−dy, dx)
            double nx = -(b.Y - a.Y), ny = b.X - a.X;
            res = ClipHalf(res, nx, ny, nx * a.X + ny * a.Y);
        }
        return res;
    }

    /// <summary>Cắt đa giác theo đường chuẩn: giữ phía chứa điểm (px, py), lùi vào Clearance.</summary>
    public static List<(double X, double Y)> ClipByLine(IList<(double X, double Y)> poly, LotLine l, double px, double py)
    {
        double len = l.Length;
        if (len < 1e-9) return poly.ToList();
        // pháp tuyến đơn vị phía trái 1→2
        double nx = -(l.Y2 - l.Y1) / len, ny = (l.X2 - l.X1) / len;
        double side = l.Side(px, py) >= 0 ? 1 : -1;
        nx *= side; ny *= side;
        double d = nx * l.X1 + ny * l.Y1 + Math.Max(0, l.Clearance);
        return ClipHalf(poly, nx, ny, d);
    }

    public static bool Inside(IList<(double X, double Y)> poly, double x, double y)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) c = !c;
        }
        return c;
    }

    /// <summary>Đa giác lồi?</summary>
    public static bool IsConvex(IList<(double X, double Y)> p)
    {
        int n = p.Count, sgn = 0;
        if (n < 4) return true;
        for (int i = 0; i < n; i++)
        {
            var a = p[i]; var b = p[(i + 1) % n]; var c = p[(i + 2) % n];
            double z = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (Math.Abs(z) < 1e-12) continue;
            int s = Math.Sign(z);
            if (sgn == 0) sgn = s; else if (s != sgn) return false;
        }
        return true;
    }

    /// <summary>Các đoạn giao của đường thẳng (điểm o, hướng đơn vị t) với đa giác – trả về các khoảng tham số [t1, t2] nằm trong.</summary>
    public static List<(double T1, double T2)> Chords(IList<(double X, double Y)> poly, double ox, double oy, double tx, double ty)
    {
        var ts = new List<double>();
        int n = poly.Count;
        double nx = -ty, ny = tx;   // pháp tuyến của đường
        for (int i = 0; i < n; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % n];
            double fa = (a.X - ox) * nx + (a.Y - oy) * ny, fb = (b.X - ox) * nx + (b.Y - oy) * ny;
            if ((fa > 0) == (fb > 0)) continue;
            double s = fa / (fa - fb);
            double x = a.X + s * (b.X - a.X), y = a.Y + s * (b.Y - a.Y);
            ts.Add((x - ox) * tx + (y - oy) * ty);
        }
        ts.Sort();
        var res = new List<(double, double)>();
        for (int i = 0; i + 1 < ts.Count; i += 2) if (ts[i + 1] - ts[i] > 1e-9) res.Add((ts[i], ts[i + 1]));
        return res;
    }

    /// <summary>Bỏ đỉnh trùng nhau / gần trùng.</summary>
    public static List<(double X, double Y)> Dedup(List<(double X, double Y)> p, double tol = 1e-7)
    {
        var res = new List<(double X, double Y)>();
        foreach (var q in p)
            if (res.Count == 0 || Math.Abs(res[^1].X - q.X) > tol || Math.Abs(res[^1].Y - q.Y) > tol) res.Add(q);
        while (res.Count > 1 && Math.Abs(res[0].X - res[^1].X) <= tol && Math.Abs(res[0].Y - res[^1].Y) <= tol) res.RemoveAt(res.Count - 1);
        return res;
    }

    /// <summary>Giao điểm hai đường thẳng (vô hạn) qua hai đoạn; null nếu song song.</summary>
    public static (double X, double Y)? Intersect(double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
    {
        double d = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(d) < 1e-12) return null;
        double a = x1 * y2 - y1 * x2, b = x3 * y4 - y3 * x4;
        return ((a * (x3 - x4) - (x1 - x2) * b) / d, (a * (y3 - y4) - (y1 - y2) * b) / d);
    }
}
