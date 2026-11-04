using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VXT.KetCau.Core.Drawing;
using VXT.KetCau.Core.Geometry;

namespace VXT.KetCau.Core.Documentation
{
    /// <summary>Trục định vị, chuỗi kích thước, ký hiệu cao độ dùng chung cho mặt bằng kiến trúc và kết cấu.</summary>
    public class PlanAnnotation
    {
        readonly IDraw2D w;
        readonly double TH;
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        public const double ChainGap = 2.8;
        public const double FirstChain = 3.2;

        public PlanAnnotation(IDraw2D surface, double textH) { w = surface; TH = textH; }

        public double ChainOffset(int i) => (FirstChain + ChainGap * i) * TH;

        /// <summary>Vẽ trục và kích thước trục + tổng ở cả 4 phía. <paramref name="reserved"/> = số chuỗi đã dùng phía trong.</summary>
        public void Grids(IEnumerable<(string Name, Seg2 Line)> grids, double x0, double y0, double x1, double y1, int reserved)
        {
            var list = grids.ToList();
            var verts = list.Where(g => g.Line.IsVertical(2)).OrderBy(g => g.Line.Mid.X).ToList();
            var hors = list.Where(g => g.Line.IsHorizontal(2)).OrderBy(g => g.Line.Mid.Y).ToList();
            double gx = ChainOffset(reserved + 1) + TH * 1.2;  // đầu đường trục, sát ký hiệu
            double R = TH * 1.5;
            foreach (var g in verts)
            {
                double x = g.Line.Mid.X;
                w.Line("TRUC", x, y0 - gx, x, y1 + gx);
                Bubble(x, y0 - gx - R, g.Name, R);
                Bubble(x, y1 + gx + R, g.Name, R);
            }
            foreach (var g in hors)
            {
                double y = g.Line.Mid.Y;
                w.Line("TRUC", x0 - gx, y, x1 + gx, y);
                Bubble(x0 - gx - R, y, g.Name, R);
                Bubble(x1 + gx + R, y, g.Name, R);
            }
            foreach (var g in list.Where(g => !g.Line.IsVertical(2) && !g.Line.IsHorizontal(2)))
            {
                w.Line("TRUC", g.Line.A.X, g.Line.A.Y, g.Line.B.X, g.Line.B.Y);
                Bubble(g.Line.A.X, g.Line.A.Y, g.Name, R);
            }

            var xs = verts.Select(g => g.Line.Mid.X).ToList();
            var ys = hors.Select(g => g.Line.Mid.Y).ToList();
            foreach (var y in new[] { y0 - ChainOffset(reserved), y1 + ChainOffset(reserved) }) ChainH(xs, y);
            foreach (var x in new[] { x0 - ChainOffset(reserved), x1 + ChainOffset(reserved) }) ChainV(ys, x);
            foreach (var y in new[] { y0 - ChainOffset(reserved + 1), y1 + ChainOffset(reserved + 1) }) ChainH(new[] { x0, x1 }, y);
            foreach (var x in new[] { x0 - ChainOffset(reserved + 1), x1 + ChainOffset(reserved + 1) }) ChainV(new[] { y0, y1 }, x);
        }

        public void Bubble(double x, double y, string name, double R)
        {
            w.Circle("TRUC", x, y, R);
            w.Text("CHU", x, y, TH * 1.1, name, 1, 2, 0);
        }

        public void ChainH(IEnumerable<double> values, double y, double minSpan = 100)
        {
            var v = values.OrderBy(q => q).ToList();
            double last = double.NaN;
            foreach (var x in v)
            {
                if (!double.IsNaN(last) && x - last >= minSpan) { w.DimH(last, x, y, TH); last = x; }
                else if (double.IsNaN(last)) last = x;
            }
        }

        public void ChainV(IEnumerable<double> values, double x, double minSpan = 100)
        {
            var v = values.OrderBy(q => q).ToList();
            double last = double.NaN;
            foreach (var y in v)
            {
                if (!double.IsNaN(last) && y - last >= minSpan) { w.DimV(x, last, y, TH); last = y; }
                else if (double.IsNaN(last)) last = y;
            }
        }

        /// <summary>Ký hiệu cao độ: tam giác trên đường chuẩn, chữ phía trên.</summary>
        public void Level(double x, double y, double z, string note = null, bool toLeft = false)
        {
            double len = TH * 7, t = TH * 0.8;
            double e = toLeft ? x - len : x + len;
            w.Line("KY_HIEU", x, y, e, y);
            w.Poly("KY_HIEU", new[] { (x, y), (x + (toLeft ? -t : t), y + t), (x + (toLeft ? t : -t), y + t) }, true);
            string txt = ShopDrawingComposer.LevelText(z);
            w.Text("CHU", toLeft ? x - t * 1.5 : x + t * 1.5, y + TH * 0.35, TH, txt, toLeft ? 2 : 0, 0, 0);
            if (!string.IsNullOrEmpty(note)) w.Text("CHU", toLeft ? x - t * 1.5 : x + t * 1.5, y - TH * 1.35, TH * 0.9, note, toLeft ? 2 : 0, 0, 0);
        }

        /// <summary>Tên hình gạch chân, dòng dưới ghi tỉ lệ.</summary>
        public void Title(double cx, double y, string title, double scale)
        {
            w.TextUnderlined("CHU", cx, y, TH * 1.5, title);
            w.Text("CHU", cx, y - TH * 2.2, TH, "tl: 1/" + scale.ToString("0", Ci), 1, 0, 0);
        }

        /// <summary>Ký hiệu vị trí mặt cắt ở hai đầu đường cắt.</summary>
        public void CutMark(double xa, double ya, double xb, double yb, string name)
        {
            var d = new P2(xb - xa, yb - ya).Unit;
            double l = TH * 2.4;
            w.Line("KY_HIEU", xa, ya, xa + d.X * l, ya + d.Y * l);
            w.Line("KY_HIEU", xb, yb, xb - d.X * l, yb - d.Y * l);
            var n = new P2(d.Y, -d.X); // hướng nhìn
            foreach (var (px, py) in new[] { (xa, ya), (xb, yb) })
            {
                w.Line("KY_HIEU", px, py, px + n.X * l, py + n.Y * l);
                w.Text("CHU", px + n.X * (l + TH * 0.8), py + n.Y * (l + TH * 0.8), TH * 1.3, name, 1, 2, 0);
            }
        }
    }
}
