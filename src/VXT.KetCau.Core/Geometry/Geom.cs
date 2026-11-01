using System;
using System.Collections.Generic;
using System.Linq;

namespace VXT.KetCau.Core.Geometry
{
    /// <summary>Điểm/vector 2D, đơn vị mm.</summary>
    public struct P2 : IEquatable<P2>
    {
        public double X, Y;
        public P2(double x, double y) { X = x; Y = y; }
        public static P2 operator +(P2 a, P2 b) => new P2(a.X + b.X, a.Y + b.Y);
        public static P2 operator -(P2 a, P2 b) => new P2(a.X - b.X, a.Y - b.Y);
        public static P2 operator *(P2 a, double k) => new P2(a.X * k, a.Y * k);
        public double Length => Math.Sqrt(X * X + Y * Y);
        public P2 Unit { get { var l = Length; return l < 1e-9 ? new P2(0, 0) : new P2(X / l, Y / l); } }
        public double Dot(P2 o) => X * o.X + Y * o.Y;
        public double Cross(P2 o) => X * o.Y - Y * o.X;
        public double DistanceTo(P2 o) => (this - o).Length;
        public bool Equals(P2 o) => Math.Abs(X - o.X) < Tol.Mm && Math.Abs(Y - o.Y) < Tol.Mm;
        public override bool Equals(object obj) => obj is P2 p && Equals(p);
        public override int GetHashCode() => (Math.Round(X / Tol.Mm), Math.Round(Y / Tol.Mm)).GetHashCode();
        public override string ToString() => $"({X:0},{Y:0})";
    }

    /// <summary>Điểm 3D, đơn vị mm.</summary>
    public struct P3
    {
        public double X, Y, Z;
        public P3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public P2 XY => new P2(X, Y);
        public override string ToString() => $"({X:0},{Y:0},{Z:0})";
    }

    public static class Tol
    {
        /// <summary>Dung sai trùng điểm (mm).</summary>
        public const double Mm = 5.0;
        /// <summary>Dung sai gom trục/tường song song (mm).</summary>
        public const double Snap = 50.0;
    }

    /// <summary>Đoạn thẳng 2D.</summary>
    public class Seg2
    {
        public P2 A, B;
        public Seg2(P2 a, P2 b) { A = a; B = b; }
        public double Length => A.DistanceTo(B);
        public P2 Dir => (B - A).Unit;
        public P2 Mid => (A + B) * 0.5;
        public bool IsHorizontal(double tolDeg = 1) => Math.Abs(Math.Atan2(B.Y - A.Y, B.X - A.X) * 180 / Math.PI % 180) < tolDeg
                                                       || Math.Abs(Math.Abs(Math.Atan2(B.Y - A.Y, B.X - A.X) * 180 / Math.PI) - 180) < tolDeg;
        public bool IsVertical(double tolDeg = 1) => Math.Abs(Math.Abs(Math.Atan2(B.Y - A.Y, B.X - A.X) * 180 / Math.PI) - 90) < tolDeg;

        /// <summary>Khoảng cách từ điểm tới đoạn.</summary>
        public double DistanceTo(P2 p)
        {
            var d = B - A; var l2 = d.Dot(d);
            if (l2 < 1e-9) return p.DistanceTo(A);
            var t = Math.Max(0, Math.Min(1, (p - A).Dot(d) / l2));
            return p.DistanceTo(A + d * t);
        }

        /// <summary>Tham số t (0..1) của hình chiếu điểm lên đoạn.</summary>
        public double Param(P2 p)
        {
            var d = B - A; var l2 = d.Dot(d);
            return l2 < 1e-9 ? 0 : (p - A).Dot(d) / l2;
        }

        /// <summary>Giao điểm hai đường thẳng (kéo dài). Trả về false nếu song song.</summary>
        public static bool LineIntersect(Seg2 s1, Seg2 s2, out P2 ip)
        {
            var r = s1.B - s1.A; var s = s2.B - s2.A;
            var den = r.Cross(s);
            ip = default;
            if (Math.Abs(den) < 1e-9) return false;
            var t = (s2.A - s1.A).Cross(s) / den;
            ip = s1.A + r * t;
            return true;
        }

        /// <summary>Giao điểm hai đoạn (có dung sai ở đầu mút).</summary>
        public static bool SegIntersect(Seg2 s1, Seg2 s2, out P2 ip, double tol = Tol.Mm)
        {
            if (!LineIntersect(s1, s2, out ip)) return false;
            return s1.DistanceTo(ip) <= tol && s2.DistanceTo(ip) <= tol;
        }
        public override string ToString() => $"{A}-{B}";
    }

    public static class GeomUtil
    {
        /// <summary>Gom các giá trị gần nhau (mm) thành nhóm, trả về giá trị trung bình mỗi nhóm đã sắp xếp.</summary>
        public static List<double> Cluster(IEnumerable<double> values, double tol)
        {
            var sorted = values.OrderBy(v => v).ToList();
            var res = new List<double>();
            var grp = new List<double>();
            foreach (var v in sorted)
            {
                if (grp.Count > 0 && v - grp.Last() > tol) { res.Add(grp.Average()); grp.Clear(); }
                grp.Add(v);
            }
            if (grp.Count > 0) res.Add(grp.Average());
            return res;
        }

        /// <summary>Diện tích đa giác (mm²), dấu dương nếu ngược chiều kim đồng hồ.</summary>
        public static double Area(IList<P2> poly)
        {
            double a = 0;
            for (int i = 0; i < poly.Count; i++) { var p = poly[i]; var q = poly[(i + 1) % poly.Count]; a += p.X * q.Y - q.X * p.Y; }
            return a / 2;
        }

        public static bool PointInPolygon(P2 p, IList<P2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].Y > p.Y) != (poly[j].Y > p.Y)) &&
                    (p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Làm tròn lên bội số step (mm).</summary>
        public static double RoundUp(double v, double step) => Math.Ceiling(v / step - 1e-9) * step;
        public static double Round(double v, double step) => Math.Round(v / step) * step;
    }
}
