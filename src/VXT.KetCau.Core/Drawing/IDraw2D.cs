using System;
using System.Collections.Generic;
using System.Globalization;

namespace VXT.KetCau.Core.Drawing
{
    /// <summary>
    /// Bề mặt vẽ 2D (mm). Cài đặt cho DXF (VXT.KetCau.Dwg) và cho view Revit (section/drafting).
    /// Lớp: BE_TONG, THEP, DAI, KICH_THUOC, CHU, KY_HIEU, TRUC, BANG.
    /// </summary>
    public interface IDraw2D
    {
        void Line(string layer, double x1, double y1, double x2, double y2);
        void Circle(string layer, double x, double y, double r);
        /// <summary>Chấm đặc (điểm đầu đường dẫn nhãn, thanh thép trên mặt cắt).</summary>
        void Dot(string layer, double x, double y, double r);
        /// <param name="h">0 trái, 1 giữa, 2 phải</param>
        /// <param name="v">0 chân dòng, 1 dưới, 2 giữa, 3 trên</param>
        void Text(string layer, double x, double y, double height, string text, int h, int v, double rotDeg);
        /// <summary>
        /// Kích thước thật của phần mềm đích (nếu hỗ trợ). Trả về false để vẽ kích thước bằng nét + chữ.
        /// </summary>
        bool Dimension(double x0, double y0, double x1, double y1, double lineY, bool vertical, string overrideText);
    }

    /// <summary>Các hình vẽ dựng từ nét cơ bản.</summary>
    public static class Draw2D
    {
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public static void Poly(this IDraw2D d, string layer, IList<(double x, double y)> pts, bool closed = false)
        {
            for (int i = 0; i + 1 < pts.Count; i++) d.Line(layer, pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y);
            if (closed && pts.Count > 2) d.Line(layer, pts[pts.Count - 1].x, pts[pts.Count - 1].y, pts[0].x, pts[0].y);
        }

        public static void Rect(this IDraw2D d, string layer, double x0, double y0, double x1, double y1)
            => d.Poly(layer, new[] { (x0, y0), (x1, y0), (x1, y1), (x0, y1) }, true);

        public static void Text(this IDraw2D d, string layer, double x, double y, double h, string text, int ha = 0, int va = 0)
            => d.Text(layer, x, y, h, text, ha, va, 0);

        /// <summary>Tên hình gạch chân (căn giữa).</summary>
        public static void TextUnderlined(this IDraw2D d, string layer, double x, double y, double height, string text)
        {
            d.Text(layer, x, y, height, text, 1, 0, 0);
            double w = (text ?? "").Length * height * 0.62;
            d.Line(layer, x - w / 2, y - height * 0.3, x + w / 2, y - height * 0.3);
        }

        /// <summary>Kích thước ngang: đường kích thước tại cao độ y, gạch xiên 45°, đường gióng xuống ext.</summary>
        public static void DimH(this IDraw2D d, double x0, double x1, double y, double txtH, string text = null, double ext0 = double.NaN, double ext1 = double.NaN)
        {
            const string L = "KICH_THUOC";
            if (!double.IsNaN(ext0)) d.Line(L, x0, ext0, x0, y + txtH * 0.75);
            if (!double.IsNaN(ext1)) d.Line(L, x1, ext1, x1, y + txtH * 0.75);
            if (d.Dimension(x0, y, x1, y, y, false, text)) return;
            d.Line(L, x0, y, x1, y);
            double t = txtH * 0.5;
            d.Line(L, x0 - t, y - t, x0 + t, y + t); d.Line(L, x1 - t, y - t, x1 + t, y + t);
            d.Text(L, (x0 + x1) / 2, y + txtH * 0.3, txtH, text ?? Math.Round(Math.Abs(x1 - x0)).ToString("0", Ci), 1, 0, 0);
        }

        public static void DimV(this IDraw2D d, double x, double y0, double y1, double txtH, string text = null)
        {
            const string L = "KICH_THUOC";
            if (d.Dimension(x, y0, x, y1, x, true, text)) return;
            d.Line(L, x, y0, x, y1);
            double t = txtH * 0.5;
            d.Line(L, x - t, y0 - t, x + t, y0 + t); d.Line(L, x - t, y1 - t, x + t, y1 + t);
            d.Text(L, x - txtH * 0.3, (y0 + y1) / 2, txtH, text ?? Math.Round(Math.Abs(y1 - y0)).ToString("0", Ci), 1, 0, 90);
        }

        /// <summary>
        /// Nhãn thép kiểu Kết cấu Soft: chấm tại thanh → đường dẫn → vai ngang có chữ đặt trên vai → vòng tròn số hiệu ở cuối vai.
        /// </summary>
        public static void Tag(this IDraw2D d, double px, double py, double sx, double sy, string text, int mark, double txtH, bool toRight = true, double rotDeg = 0)
        {
            d.Dot("KY_HIEU", px, py, txtH * 0.15);
            d.Line("KY_HIEU", px, py, sx, sy);
            double w = Math.Max((text ?? "").Length * txtH * 0.7, txtH * 2);
            double ex = toRight ? sx + w : sx - w;
            d.Line("KY_HIEU", sx, sy, ex, sy);
            d.Text("CHU", (sx + ex) / 2, sy + txtH * 0.25, txtH, text, 1, 0, 0);
            double r = txtH * 0.75;
            double cx = toRight ? ex + r : ex - r;
            d.Circle("KY_HIEU", cx, sy, r);
            d.Text("CHU", cx, sy, txtH * 0.85, mark.ToString(), 1, 2, 0);
        }

        /// <summary>Nhãn thép xoay dọc (dùng cho mặt đứng cột).</summary>
        public static void TagVertical(this IDraw2D d, double px, double py, double sx, double sy, string text, int mark, double txtH)
        {
            d.Dot("KY_HIEU", px, py, txtH * 0.15);
            d.Line("KY_HIEU", px, py, sx, sy);
            double w = Math.Max((text ?? "").Length * txtH * 0.7, txtH * 2);
            double r = txtH * 0.75;
            d.Circle("KY_HIEU", sx, sy + r, r);
            d.Text("CHU", sx, sy + r, txtH * 0.85, mark.ToString(), 1, 2, 0);
            d.Line("KY_HIEU", sx, sy + 2 * r, sx, sy + 2 * r + w);
            d.Text("CHU", sx - txtH * 0.25, sy + 2 * r + w / 2, txtH, text, 1, 0, 90);
        }
    }
}
