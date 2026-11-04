using System;
using System.Collections.Generic;
using System.Linq;

namespace VXT.KetCau.Core.Drawing
{
    /// <summary>
    /// Bề mặt vẽ có thêm cung, mẫu tô và tỉ lệ hình hiện hành (AutoCAD, DXF, bộ ghi).
    /// Tách khỏi <see cref="IDraw2D"/> để view Revit không phải cài đặt lại.
    /// </summary>
    public interface IDraw2DEx : IDraw2D
    {
        /// <summary>Cung ngược chiều kim đồng hồ từ startDeg tới endDeg.</summary>
        void Arc(string layer, double cx, double cy, double r, double startDeg, double endDeg);
        /// <param name="pattern">"SOLID" hoặc "ANSI31"</param>
        /// <param name="spacing">Khoảng cách nét tô trong đơn vị mô hình (bỏ qua với SOLID)</param>
        void Hatch(string layer, IList<(double x, double y)> poly, string pattern, double spacing);
        /// <summary>Tỉ lệ in của các đối tượng vẽ tiếp theo (100 = 1/100) — chọn kiểu kích thước, tỉ lệ nét.</summary>
        void SetScale(double scale);
    }

    public static class Draw2DEx
    {
        public static void ArcX(this IDraw2D d, string layer, double cx, double cy, double r, double startDeg, double endDeg)
        {
            if (d is IDraw2DEx x) { x.Arc(layer, cx, cy, r, startDeg, endDeg); return; }
            double a0 = startDeg * Math.PI / 180, a1 = endDeg * Math.PI / 180;
            while (a1 <= a0) a1 += 2 * Math.PI;
            int n = Math.Max(4, (int)Math.Ceiling((a1 - a0) / (Math.PI / 12)));
            for (int i = 0; i < n; i++)
            {
                double t0 = a0 + (a1 - a0) * i / n, t1 = a0 + (a1 - a0) * (i + 1) / n;
                d.Line(layer, cx + r * Math.Cos(t0), cy + r * Math.Sin(t0), cx + r * Math.Cos(t1), cy + r * Math.Sin(t1));
            }
        }

        public static void HatchX(this IDraw2D d, string layer, IList<(double x, double y)> poly, string pattern, double spacing)
        {
            if (d is IDraw2DEx x) { x.Hatch(layer, poly, pattern, spacing); return; }
            HatchLines(d, layer, poly, pattern == "SOLID" ? spacing * 0.25 : spacing);
        }

        public static void SetScaleX(this IDraw2D d, double scale) { if (d is IDraw2DEx x) x.SetScale(scale); }

        /// <summary>Tô đa giác bằng các nét xiên 45° (cắt theo đường quét) khi bề mặt không có đối tượng tô thật.</summary>
        public static void HatchLines(IDraw2D d, string layer, IList<(double x, double y)> poly, double spacing)
        {
            if (poly == null || poly.Count < 3 || spacing <= 0) return;
            // Xoay −45° để nét tô thành đường nằm ngang, cắt đa giác, rồi xoay lại.
            const double c = 0.70710678118654757;
            var q = poly.Select(p => (u: (p.x + p.y) * c, v: (p.y - p.x) * c)).ToList();
            double v0 = q.Min(p => p.v), v1 = q.Max(p => p.v);
            double start = Math.Ceiling(v0 / spacing) * spacing;
            for (double v = start; v < v1; v += spacing)
            {
                var hits = new List<double>();
                for (int i = 0; i < q.Count; i++)
                {
                    var a = q[i]; var b = q[(i + 1) % q.Count];
                    if ((a.v > v) == (b.v > v)) continue;
                    hits.Add(a.u + (v - a.v) / (b.v - a.v) * (b.u - a.u));
                }
                hits.Sort();
                for (int i = 0; i + 1 < hits.Count; i += 2)
                    d.Line(layer, (hits[i] - v) * c, (hits[i] + v) * c, (hits[i + 1] - v) * c, (hits[i + 1] + v) * c);
            }
        }
    }

    /// <summary>
    /// Ghi lại các lệnh vẽ để biết khung bao của hình, rồi phát lại ở vị trí bất kỳ — dùng xếp hình vào tờ bản vẽ.
    /// </summary>
    public class RecordingDraw2D : IDraw2DEx
    {
        readonly List<Action<IDraw2D, double, double>> ops = new List<Action<IDraw2D, double, double>>();
        readonly bool realDimensions;
        double scale = 1;

        public double MinX { get; private set; } = double.MaxValue;
        public double MinY { get; private set; } = double.MaxValue;
        public double MaxX { get; private set; } = double.MinValue;
        public double MaxY { get; private set; } = double.MinValue;
        public bool IsEmpty => ops.Count == 0;
        public double Width => IsEmpty ? 0 : MaxX - MinX;
        public double Height => IsEmpty ? 0 : MaxY - MinY;
        public int Count => ops.Count;

        /// <param name="realDimensions">Bề mặt đích có kích thước thật (AutoCAD) hay vẽ bằng nét + chữ (DXF R12).</param>
        public RecordingDraw2D(bool realDimensions) { this.realDimensions = realDimensions; }

        void Box(double x, double y)
        {
            if (x < MinX) MinX = x; if (x > MaxX) MaxX = x;
            if (y < MinY) MinY = y; if (y > MaxY) MaxY = y;
        }

        public void Line(string layer, double x1, double y1, double x2, double y2)
        {
            Box(x1, y1); Box(x2, y2);
            ops.Add((t, dx, dy) => t.Line(layer, x1 + dx, y1 + dy, x2 + dx, y2 + dy));
        }

        public void Circle(string layer, double x, double y, double r)
        {
            Box(x - r, y - r); Box(x + r, y + r);
            ops.Add((t, dx, dy) => t.Circle(layer, x + dx, y + dy, r));
        }

        public void Dot(string layer, double x, double y, double r)
        {
            Box(x - r, y - r); Box(x + r, y + r);
            ops.Add((t, dx, dy) => t.Dot(layer, x + dx, y + dy, r));
        }

        public void Text(string layer, double x, double y, double height, string text, int h, int v, double rotDeg)
        {
            // ước lượng khung chữ
            double w = (text ?? "").Length * height * 0.6;
            double rad = rotDeg * Math.PI / 180, cs = Math.Cos(rad), sn = Math.Sin(rad);
            double ax = h == 1 ? -w / 2 : (h == 2 ? -w : 0), ay = v == 3 ? -height : (v == 2 ? -height / 2 : 0);
            foreach (var (u, t) in new[] { (ax, ay), (ax + w, ay), (ax, ay + height), (ax + w, ay + height) })
                Box(x + u * cs - t * sn, y + u * sn + t * cs);
            ops.Add((tg, dx, dy) => tg.Text(layer, x + dx, y + dy, height, text, h, v, rotDeg));
        }

        public bool Dimension(double x0, double y0, double x1, double y1, double lineY, bool vertical, string overrideText)
        {
            if (!realDimensions) return false;
            Box(x0, y0); Box(x1, y1);
            // chừa chỗ cho chữ kích thước
            double m = 2.5 * scale * 1.6;
            if (vertical) { Box(lineY - m, y0); Box(lineY + m * 0.3, y1); }
            else { Box(x0, lineY + m); Box(x1, lineY - m * 0.3); }
            ops.Add((t, dx, dy) => t.Dimension(x0 + dx, y0 + dy, x1 + dx, y1 + dy, lineY + (vertical ? dx : dy), vertical, overrideText));
            return true;
        }

        public void Arc(string layer, double cx, double cy, double r, double startDeg, double endDeg)
        {
            double a0 = startDeg * Math.PI / 180, a1 = endDeg * Math.PI / 180;
            while (a1 <= a0) a1 += 2 * Math.PI;
            for (int i = 0; i <= 12; i++) { double t = a0 + (a1 - a0) * i / 12; Box(cx + r * Math.Cos(t), cy + r * Math.Sin(t)); }
            ops.Add((t, dx, dy) => t.ArcX(layer, cx + dx, cy + dy, r, startDeg, endDeg));
        }

        public void Hatch(string layer, IList<(double x, double y)> poly, string pattern, double spacing)
        {
            var copy = poly.ToList();
            foreach (var p in copy) Box(p.x, p.y);
            ops.Add((t, dx, dy) => t.HatchX(layer, copy.Select(p => (p.x + dx, p.y + dy)).ToList(), pattern, spacing));
        }

        public void SetScale(double s)
        {
            scale = s;
            ops.Add((t, dx, dy) => t.SetScaleX(s));
        }

        /// <summary>Phát lại toàn bộ, dịch (dx, dy).</summary>
        public void Replay(IDraw2D target, double dx, double dy) { foreach (var op in ops) op(target, dx, dy); }
    }
}
