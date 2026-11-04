using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VXT.KetCau.Core.Detailing;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;

namespace VXT.KetCau.Core.Drawing
{
    /// <summary>
    /// Dựng shop drawing thép theo quy tắc thể hiện Kết cấu Soft trên một bề mặt vẽ bất kỳ (DXF, view Revit):
    /// mặt đứng dầm 1/50, mặt cắt dầm 1/25, mặt đứng + mặt cắt cột, bảng thống kê và tổng hợp cốt thép.
    /// Toạ độ vẽ: mm trong không gian mô hình; chữ in cao 2.5 mm × tỉ lệ.
    /// </summary>
    public class ShopDrawingComposer
    {
        readonly IDraw2D w;
        readonly DesignSettings set;
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        string Sym => set.DiaSymbol;

        /// <summary>Chỉ vẽ ký hiệu/kích thước/nhãn (view Revit đã hiển thị bê tông và thép thật).</summary>
        public bool AnnotationsOnly { get; set; }
        public double PrintTextHeight { get; set; } = 2.5;
        public double ElevationScale { get; set; } = 50;
        public double SectionScale { get; set; } = 25;

        public ShopDrawingComposer(IDraw2D surface, DesignSettings s) { w = surface; set = s; }

        double TH => PrintTextHeight * ElevationScale;

        void Geo(string layer, double x1, double y1, double x2, double y2) { if (!AnnotationsOnly) w.Line(layer, x1, y1, x2, y2); }

        // ================================================================== MẶT ĐỨNG DẦM
        /// <summary>
        /// Vẽ mặt đứng dầm liên tục. Gốc vẽ: điểm (ox, oy) ứng với x = 0 theo trục dầm (gốc chuỗi) và cao độ đỉnh dầm.
        /// Trả về chiều cao vùng vẽ (mm) phía dưới oy, dùng xếp hình kế tiếp.
        /// </summary>
        public double DrawBeamElevation(DetailedElement e, double ox, double oy, bool withTitle = true)
        {
            var g = (BeamChainGeometry)e.Geometry;
            double x0 = g.Supports.First().FaceLeft;
            Func<double, double> X = xl => ox + xl;
            Func<double, double> Z = z => oy + (z - g.TopZ);
            Func<P3, (double x, double y)> L = p => (X((p.XY - g.Origin).Dot(g.Dir)), Z(p.Z));
            double len = g.Length, h = g.H;

            // ---- Bê tông: biên dầm + cột gối vẽ cụt trên và dưới
            Geo("BE_TONG", X(x0), Z(g.TopZ), X(x0 + len), Z(g.TopZ));
            Geo("BE_TONG", X(x0), Z(g.TopZ - h), X(x0 + len), Z(g.TopZ - h));
            foreach (var s in g.Supports)
            {
                if (s.Width <= 0) { Geo("BE_TONG", X(s.X), Z(g.TopZ), X(s.X), Z(g.TopZ - h)); continue; }
                foreach (var xx in new[] { s.FaceLeft, s.FaceRight })
                    Geo("BE_TONG", X(xx), Z(g.TopZ + 600), X(xx), Z(g.TopZ - h - 600));
                if (!AnnotationsOnly)
                {
                    BreakLine(X(s.FaceLeft), X(s.FaceRight), Z(g.TopZ + 600));
                    BreakLine(X(s.FaceLeft), X(s.FaceRight), Z(g.TopZ - h - 600));
                }
            }

            // ---- Thép dọc + nhãn xếp tầng (nửa trên ghi phía trên dầm, nửa dưới ghi phía dưới)
            double band = TH * 5;
            var tiersUp = new List<List<(double a, double b)>>();
            var tiersDn = new List<List<(double a, double b)>>();
            double lowTagBase = Z(g.TopZ - h) - 900;
            foreach (var bar in e.Bars.Where(b => !b.Role.IsTransverse()))
            {
                if (!AnnotationsOnly) foreach (var pl in bar.Placements) w.Poly("THEP", pl.Path.Select(L).ToList());
                foreach (var grp in bar.Placements.GroupBy(p => (Math.Round(p.X0), Math.Round(p.X1), p.UpperHalf)))
                {
                    var pl = grp.First();
                    int n = grp.Count(); // số thanh của số hiệu này tại chỗ ghi
                    double xm = bar.Role == BarRole.SupportTop ? pl.X0 + (pl.X1 - pl.X0) * 0.7 : (pl.X0 + pl.X1) / 2;
                    var pz = pl.Path.Select(L).ToList();
                    double zb = pz.Count >= 3 ? pz[1].y : pz[0].y;
                    bool up = pl.UpperHalf;
                    string txt = bar.TagText(Sym, n);
                    double wTxt = txt.Length * TH * 0.7 + TH * 3;
                    int tier = FindTier(up ? tiersUp : tiersDn, X(xm), X(xm) + wTxt);
                    double sy = up ? Z(g.TopZ) + band + TH * 2.5 + tier * TH * 2.2 : lowTagBase - tier * TH * 2.2;
                    w.Tag(X(xm), zb, X(xm) + TH, sy, txt, bar.Mark, TH);
                }
            }

            // ---- Đai: vạch đứng tại từng đai
            var stir = e.Bars.FirstOrDefault(b => b.Role == BarRole.Stirrup);
            if (stir != null && !AnnotationsOnly)
                foreach (var pl in stir.Placements)
                {
                    double step = pl.Count > 1 ? (pl.X1 - pl.X0) / (pl.Count - 1) : 0;
                    for (int i = 0; i < pl.Count; i++)
                        w.Line("DAI", X(pl.X0 + i * step), Z(g.TopZ - set.CoverBeam), X(pl.X0 + i * step), Z(g.TopZ - h + set.CoverBeam));
                }

            // ---- Trên cùng: chuỗi kích thước vùng đai từng nhịp, dưới mỗi đoạn ghi Ø6a100 ④
            double yz = Z(g.TopZ) + band;
            foreach (var zn in g.StirrupZones)
            {
                w.DimH(X(zn.X0), X(zn.X1), yz, TH);
                if (stir != null)
                {
                    double xc = (X(zn.X0) + X(zn.X1)) / 2;
                    string t = $"{Sym}{zn.Spec.D}a{zn.Spec.S:0}";
                    w.Text("CHU", xc - TH * 0.6, yz - TH * 1.6, TH, t, 1, 0, 0);
                    double cx = xc + t.Length * TH * 0.33 + TH * 0.4;
                    w.Circle("KY_HIEU", cx, yz - TH * 1.25, TH * 0.75);
                    w.Text("CHU", cx, yz - TH * 1.25, TH * 0.85, stir.Mark.ToString(), 1, 2, 0);
                }
            }

            // ---- Vạch cắt "1|" ở cả trên và dưới dầm
            foreach (var (xs, name, _) in g.Sections)
            {
                string n1 = name.Split('-')[0];
                foreach (var zz in new[] { Z(g.TopZ) + TH * 0.6, Z(g.TopZ - h) - TH * 0.6 - 350 })
                {
                    w.Line("KY_HIEU", X(xs), zz, X(xs), zz + 350);
                    w.Text("CHU", X(xs) + TH * 0.3, zz + 50, TH, n1, 0, 0, 0);
                }
            }

            // ---- Dưới dầm: kích thước từng nhịp, tổng chiều dài, trục định vị
            double yd = lowTagBase - Math.Max(1, tiersDn.Count) * TH * 2.2 - TH * 2;
            for (int i = 0; i + 1 < g.Supports.Count; i++)
                w.DimH(X(g.Supports[i].X), X(g.Supports[i + 1].X), yd, TH, null, Z(g.TopZ - h - 600), Z(g.TopZ - h - 600));
            w.DimH(X(x0), X(x0 + len), yd - TH * 2.4, TH);
            foreach (var s in g.Supports.Where(s => !string.IsNullOrEmpty(s.GridName)))
            {
                w.Line("TRUC", X(s.X), yd - TH * 2.8, X(s.X), yd - TH * 4.2);
                w.Circle("TRUC", X(s.X), yd - TH * 5.2, TH);
                w.Text("CHU", X(s.X), yd - TH * 5.2, TH, s.GridName, 1, 2, 0);
            }

            // ---- Bên trái: cao độ đỉnh dầm ▽+4.800
            double ex = X(x0) - 1600;
            LevelMark(ex, Z(g.TopZ), g.TopZ);

            double bottom = yd - TH * 7;
            if (withTitle)
            {
                w.TextUnderlined("CHU", X(x0) + len / 2, bottom - TH * 1.5, TH * 1.4, e.Title);
                w.Text("CHU", X(x0) + len / 2, bottom - TH * 3.6, TH, "tl: 1/" + ElevationScale.ToString("0", Ci), 1, 0, 0);
                bottom -= TH * 5;
            }
            return oy - bottom;
        }

        void LevelMark(double x, double y, double z)
        {
            w.Line("KY_HIEU", x, y, x + 700, y);
            w.Poly("KY_HIEU", new[] { (x + 150, y), (x + 300, y + 170), (x, y + 170) }, true);
            w.Text("CHU", x, y + 250, TH, LevelText(z), 0, 0, 0);
        }

        public static string LevelText(double z) => Math.Abs(z) < 0.5 ? "±0.000" : (z > 0 ? "+" : "") + (z / 1000).ToString("0.000", Ci);

        void BreakLine(double xa, double xb, double y)
        {
            double xm = (xa + xb) / 2, d = TH * 0.6;
            w.Poly("BE_TONG", new[] { (xa - 100, y), (xm - d / 2, y), (xm - d / 4, y + d), (xm + d / 4, y - d), (xm + d / 2, y), (xb + 100, y) });
        }

        static int FindTier(List<List<(double a, double b)>> tiers, double a, double b)
        {
            for (int t = 0; ; t++)
            {
                if (t == tiers.Count) tiers.Add(new List<(double, double)>());
                if (tiers[t].All(r => b < r.a || a > r.b)) { tiers[t].Add((a, b)); return t; }
            }
        }

        // ================================================================== MẶT CẮT DẦM
        /// <summary>
        /// Vẽ mặt cắt dầm tại xCut. (ox, oyTop) = điểm giữa mép trên tiết diện; k = hệ số phóng hình
        /// (DXF ghép chung khổ 1/50 thì k = 2 để in ra 1/25; view Revit 1/25 thì k = 1).
        /// </summary>
        public double DrawBeamSection(DetailedElement e, double xCut, string name, double ox, double oyTop, double k, double txtH)
        {
            var g = (BeamChainGeometry)e.Geometry;
            double b = g.B * k, h = g.H * k, c = set.CoverBeam * k;
            double left = ox - b / 2, top = oyTop;
            if (!AnnotationsOnly) w.Rect("BE_TONG", left, top - h, left + b, top);
            var stir = e.Bars.FirstOrDefault(x => x.Role == BarRole.Stirrup);
            if (stir != null && !AnnotationsOnly) w.Rect("DAI", left + c, top - h + c, left + b - c, top - c);

            var hits = new List<(DetailBar bar, double y, double z)>();
            foreach (var bar in e.Bars.Where(x => !x.Role.IsTransverse()))
                foreach (var pl in bar.Placements)
                {
                    if (xCut < pl.X0 - 1 || xCut > pl.X1 + 1) continue;
                    var p = pl.Path.Count >= 3 ? pl.Path[1] : pl.Path[0];
                    if (pl.Path.Count >= 2 && Math.Abs(pl.Path[0].Z - pl.Path[1].Z) < 1) p = pl.Path[0];
                    hits.Add((bar, (p.XY - g.Origin).Dot(g.Perp), p.Z - g.TopZ));
                }
            if (!AnnotationsOnly)
                foreach (var hb in hits)
                {
                    double cx = ox + hb.y * k, cz = top + hb.z * k;
                    w.Circle("THEP", cx, cz, hb.bar.D / 2.0 * k);
                    w.Dot("THEP", cx, cz, hb.bar.D / 2.0 * k * 0.7);
                }
            // nhóm trên ghi lên trên, nhóm dưới ghi xuống dưới; thanh cùng hàng nối vạch mảnh rồi kéo đường dẫn ra nhãn
            int tierUp = 0, tierDn = 0;
            foreach (var grp in hits.GroupBy(x => (x.bar.Mark, Math.Round(x.z))).OrderByDescending(x => x.Key.Item2))
            {
                var list = grp.OrderBy(x => x.y).ToList();
                bool upper = list[0].z > -g.H / 2;
                double zz = top + list[0].z * k;
                double xa = ox + list.First().y * k, xb = ox + list.Last().y * k;
                if (list.Count > 1) w.Line("KY_HIEU", xa, zz, xb, zz);
                double sy = upper ? top + txtH * 1.8 + tierUp++ * txtH * 2.1 : top - h - txtH * 1.8 - tierDn++ * txtH * 2.1;
                w.Tag((xa + xb) / 2, zz, left + b + txtH * 0.8, sy, list[0].bar.TagText(Sym, list.Count), list[0].bar.Mark, txtH);
            }
            // đai ghi sang phải, ghi đúng bước tại vị trí cắt
            if (stir != null)
            {
                var zone = g.StirrupZones.FirstOrDefault(z => xCut >= z.X0 && xCut <= z.X1);
                double s = zone?.Spec.S ?? stir.Spacing;
                w.Tag(left + b - c, top - h / 2, left + b + txtH * 2, top - h / 2, stir.TagText(Sym, null, s), stir.Mark, txtH);
            }
            // kích thước b và h
            double yDim = top - h - txtH * 1.8 - Math.Max(tierDn, 1) * txtH * 2.1;
            w.DimH(left, left + b, yDim, txtH, g.B.ToString("0", Ci), top - h, top - h);
            w.DimV(left - txtH * 1.5, top - h, top, txtH, g.H.ToString("0", Ci));
            double ty = yDim - txtH * 3;
            w.TextUnderlined("CHU", ox, ty, txtH * 1.2, name);
            w.Text("CHU", ox, ty - txtH * 1.9, txtH, "tl: 1/" + SectionScale.ToString("0", Ci), 1, 0, 0);
            return oyTop - (ty - txtH * 2.5);
        }

        // ================================================================== CỘT
        /// <summary>Mặt cắt cột (k = phóng hình, txtH = chiều cao chữ mô hình). (ox, oy) = góc trên trái.</summary>
        public double DrawColumnSection(DetailedElement e, double ox, double oy, double k, double txtH, string title = null)
        {
            var g = (ColumnGeometry)e.Geometry;
            double b = g.B * k, h = g.H * k, c = set.CoverColumn * k;
            if (!AnnotationsOnly) { w.Rect("BE_TONG", ox, oy - h, ox + b, oy); w.Rect("DAI", ox + c, oy - h + c, ox + b - c, oy - c); }
            var main = e.Bars.First(x => x.Role == BarRole.ColumnMain);
            var stir = e.Bars.FirstOrDefault(x => x.Role == BarRole.Stirrup);
            double rot = -g.RotationDeg * Math.PI / 180;
            var local = new List<(double x, double y)>();
            foreach (var pl in main.Placements)
            {
                var p = pl.Path.Count >= 3 && Math.Abs(pl.Path[0].Z - pl.Path[1].Z) < 1 ? pl.Path[1] : pl.Path[0];
                double dx = p.X - g.Location.X, dy = p.Y - g.Location.Y;
                local.Add((dx * Math.Cos(rot) - dy * Math.Sin(rot), dx * Math.Sin(rot) + dy * Math.Cos(rot)));
            }
            foreach (var (lx, ly) in local)
            {
                double cx = ox + b / 2 + lx * k, cy = oy - h / 2 + ly * k;
                if (!AnnotationsOnly) { w.Circle("THEP", cx, cy, main.D / 2.0 * k); w.Dot("THEP", cx, cy, main.D / 2.0 * k * 0.7); }
            }
            // nhãn thép dọc: nối các thanh cạnh trên bằng vạch mảnh
            var topRow = local.Where(p => p.y > 0 && Math.Abs(p.y - local.Max(q => q.y)) < 1).OrderBy(p => p.x).ToList();
            if (topRow.Count > 1) w.Line("KY_HIEU", ox + b / 2 + topRow.First().x * k, oy - h / 2 + topRow[0].y * k, ox + b / 2 + topRow.Last().x * k, oy - h / 2 + topRow[0].y * k);
            var tp = topRow.Count > 0 ? topRow[topRow.Count / 2] : local[0];
            w.Tag(ox + b / 2 + tp.x * k, oy - h / 2 + tp.y * k, ox + b + txtH, oy + txtH * 1.5, main.TagText(Sym), main.Mark, txtH);
            if (stir != null)
                w.Tag(ox + b - c, oy - h / 2, ox + b + txtH * 2, oy - h / 2, stir.TagText(Sym, null, stir.Spacing), stir.Mark, txtH);
            int extra = 0;
            foreach (var t in e.Bars.Where(x => x.Role == BarRole.CTie || x.Role == BarRole.InnerStirrup))
                w.Text("CHU", ox + b + txtH * 2, oy - h / 2 - txtH * (2 + 1.8 * extra++), txtH, $"({t.Mark}) {t.Role.Vn()} {t.TagText(Sym, null, t.Spacing)}", 0, 0, 0);
            w.DimH(ox, ox + b, oy - h - txtH * 2, txtH, g.B.ToString("0", Ci), oy - h, oy - h);
            w.DimV(ox - txtH * 1.5, oy - h, oy, txtH, g.H.ToString("0", Ci));
            w.TextUnderlined("CHU", ox + b / 2, oy - h - txtH * 5, txtH * 1.4, title ?? e.Title);
            w.Text("CHU", ox + b / 2, oy - h - txtH * 7, txtH, $"{g.BaseLevelText} → {g.TopLevelText}   tl: 1/{SectionScale:0}", 1, 0, 0);
            return h + txtH * 8;
        }

        /// <summary>
        /// Mặt đứng cột 1/50: nhãn xoay dọc theo thanh, chuỗi vùng đai đứng bên phải (chữ xoay dọc),
        /// bên trái kích thước chiều cao, đoạn neo móng, đoạn chờ nối; ghi cao độ chân và đỉnh. (ox, oy) = tim cột tại cao độ 0.
        /// </summary>
        public double DrawColumnElevation(DetailedElement e, double ox, double oy)
        {
            var g = (ColumnGeometry)e.Geometry;
            double b = g.B;
            Func<double, double> Z = z => oy + z;
            double zb = g.BaseZ - g.AnchorBelow, zt = g.TopZ + g.LapAbove;
            if (!AnnotationsOnly)
            {
                w.Rect("BE_TONG", ox - b / 2, Z(g.BaseZ), ox + b / 2, Z(g.TopZ));
                var main = e.Bars.First(x => x.Role == BarRole.ColumnMain);
                foreach (double xx in new[] { -b / 2 + set.CoverColumn + 10, b / 2 - set.CoverColumn - 10 })
                    w.Line("THEP", ox + xx, Z(zb), ox + xx, Z(zt));
                var st = e.Bars.FirstOrDefault(x => x.Role == BarRole.Stirrup);
                if (st != null)
                    foreach (var pl in st.Placements)
                    {
                        double step = pl.Count > 1 ? (pl.X1 - pl.X0) / (pl.Count - 1) : 0;
                        for (int i = 0; i < pl.Count; i++) w.Line("DAI", ox - b / 2 + set.CoverColumn, Z(pl.X0 + i * step), ox + b / 2 - set.CoverColumn, Z(pl.X0 + i * step));
                    }
            }
            var m = e.Bars.First(x => x.Role == BarRole.ColumnMain);
            w.TagVertical(ox + b / 2 - set.CoverColumn - 10, Z((g.BaseZ + g.TopZ) / 2), ox + b / 2 + TH * 3, Z((g.BaseZ + g.TopZ) / 2) - TH * 3, m.TagText(Sym), m.Mark, TH);
            // chuỗi vùng đai đứng bên phải
            var stir = e.Bars.FirstOrDefault(x => x.Role == BarRole.Stirrup);
            double xr = ox + b / 2 + TH * 7;
            foreach (var zn in g.StirrupZones)
            {
                w.DimV(xr, Z(zn.X0), Z(zn.X1), TH);
                if (stir != null) w.Text("CHU", xr + TH * 1.6, Z((zn.X0 + zn.X1) / 2), TH, $"{Sym}{zn.Spec.D}a{zn.Spec.S:0} ({stir.Mark})", 1, 0, 90);
            }
            // bên trái: chiều cao, neo móng, chờ nối
            double xl = ox - b / 2 - TH * 2;
            w.DimV(xl, Z(g.BaseZ), Z(g.TopZ), TH);
            if (g.AnchorBelow > 0) w.DimV(xl, Z(zb), Z(g.BaseZ), TH);
            if (g.LapAbove > 0) w.DimV(xl, Z(g.TopZ), Z(zt), TH);
            LevelMark(ox - b / 2 - TH * 8, Z(g.BaseZ), g.BaseZ);
            LevelMark(ox - b / 2 - TH * 8, Z(g.TopZ), g.TopZ);
            w.TextUnderlined("CHU", ox, Z(zb) - TH * 3, TH * 1.4, e.Title);
            w.Text("CHU", ox, Z(zb) - TH * 5, TH, "tl: 1/" + ElevationScale.ToString("0", Ci), 1, 0, 0);
            return zt - zb + TH * 8;
        }

        // ================================================================== BẢNG THỐNG KÊ
        /// <summary>Chiều cao hàng bảng thống kê (mm ở chữ 125 mm) — hồ sơ khổ A3 dùng hàng thấp hơn để đỡ tốn tờ.</summary>
        public double ScheduleRowHeight { get; set; } = 900;

        /// <summary>Bảng thống kê cốt thép + tổng hợp. (ox, oy) = góc trên trái. Trả về chiều cao bảng.</summary>
        public double DrawSchedule(RebarSchedule s, double ox, double oy, double txtH)
        {
            if (s == null) return 0;
            double h = DrawScheduleTable(s, ox, oy, txtH);
            double u = txtH / 125.0;
            return h + 900 * u + DrawScheduleSummary(s, ox, oy - h - 900 * u, txtH);
        }

        /// <summary>Chỉ bảng thống kê. Trả về chiều cao đã vẽ.</summary>
        public double DrawScheduleTable(RebarSchedule s, double ox, double oy, double txtH)
        {
            double u = txtH / 125.0; // tỉ lệ kích thước bảng theo chiều cao chữ (chuẩn 125 mm ở 1/50)
            double th = txtH * 0.9;
            double[] cw = new double[] { 1800, 900, 4200, 900, 1500, 1000, 1000, 1200, 1400, 1600 }.Select(v => v * u).ToArray();
            string[] hd = { "TÊN CẤU KIỆN", "SỐ HIỆU", "HÌNH DẠNG & KÍCH THƯỚC (mm)", "Ø (mm)", "C.DÀI 1 THANH (mm)", "SỐ C.KIỆN", "SỐ THANH 1 CK", "SỐ THANH TOÀN BỘ", "TỔNG C.DÀI (m)", "TỔNG T.LƯỢNG (kg)" };
            hd[3] = Sym + " (mm)";
            double W = cw.Sum(), rh = ScheduleRowHeight * u;
            w.Text("CHU", ox + W / 2, oy + 600 * u, txtH * 1.6, "BẢNG THỐNG KÊ CỐT THÉP", 1, 0, 0);
            double y = oy;
            Row(hd, ox, y, cw, 900 * u * 1.3, th * 0.75); y -= 900 * u * 1.3;
            foreach (var grp in s.Rows.GroupBy(r => r.Element))
            {
                double yStart = y;
                foreach (var r in grp)
                {
                    Row(new[] { "", r.Mark.ToString(), "", r.D.ToString(), r.Length1.ToString("0", Ci), r.ElementQty.ToString(), r.CountPerElement.ToString(),
                                r.CountTotal.ToString(), r.TotalLengthM.ToString("0.00", Ci), r.WeightKg.ToString("0.00", Ci) }, ox, y, cw, rh, th, skipFirst: true);
                    DrawShape(r, ox + cw[0] + cw[1], y, cw[2], rh, th * 0.8, u);
                    y -= rh;
                }
                // ô tên cấu kiện gộp, chữ đứng
                w.Rect("BANG", ox, y, ox + cw[0], yStart);
                w.Text("CHU", ox + cw[0] / 2, (y + yStart) / 2, th, grp.Key, 1, 2, (yStart - y) > cw[0] * 1.2 ? 90 : 0);
            }
            return oy - y;
        }

        /// <summary>Chỉ bảng tổng hợp theo đường kính và nhóm. Trả về chiều cao đã vẽ.</summary>
        public double DrawScheduleSummary(RebarSchedule s, double ox, double oy, double txtH)
        {
            double u = txtH / 125.0;
            double th = txtH * 0.9;
            double rh = 900 * u, y = oy;
            var byD = s.ByDiameter().ToList();
            var cw2 = new[] { 2600.0 * u }.Concat(byD.Select(_ => 1400.0 * u)).ToArray();
            w.Text("CHU", ox + cw2.Sum() / 2, y + 300 * u, txtH * 1.4, "TỔNG HỢP CỐT THÉP", 1, 0, 0);
            Row(new[] { "Đường kính (mm)" }.Concat(byD.Select(d => Sym + d.D)).ToArray(), ox, y, cw2, rh, th); y -= rh;
            Row(new[] { "Chiều dài (m)" }.Concat(byD.Select(d => d.LengthM.ToString("0.00", Ci))).ToArray(), ox, y, cw2, rh, th); y -= rh;
            Row(new[] { "Trọng lượng (kg)" }.Concat(byD.Select(d => d.Kg.ToString("0.00", Ci))).ToArray(), ox, y, cw2, rh, th); y -= rh;
            var gsum = s.ByGroup();
            var cw3 = new[] { 2600.0 * u, 1800 * u };
            Row(new[] { $"NHÓM {Sym}≤10 (kg)", gsum.G1.ToString("0.00", Ci) }, ox, y, cw3, rh, th); y -= rh;
            Row(new[] { $"NHÓM 10<{Sym}≤18 (kg)", gsum.G2.ToString("0.00", Ci) }, ox, y, cw3, rh, th); y -= rh;
            Row(new[] { $"NHÓM {Sym}>18 (kg)", gsum.G3.ToString("0.00", Ci) }, ox, y, cw3, rh, th); y -= rh;
            Row(new[] { "TỔNG CỘNG (kg)", gsum.Total.ToString("0.00", Ci) }, ox, y, cw3, rh, th); y -= rh;
            return oy - y;
        }

        void Row(string[] cells, double x, double y, double[] cw, double rh, double th, bool skipFirst = false)
        {
            double cx = x;
            for (int i = 0; i < cw.Length; i++)
            {
                if (!(skipFirst && i == 0)) w.Rect("BANG", cx, y - rh, cx + cw[i], y);
                if (i < cells.Length && !string.IsNullOrEmpty(cells[i])) w.Text("CHU", cx + cw[i] / 2, y - rh / 2, th, cells[i], 1, 2, 0);
                cx += cw[i];
            }
        }

        /// <summary>Vẽ phác thanh, ghi chiều dài từng đoạn; đai vẽ khung, ghi 2 cạnh và dấu móc 45°.</summary>
        void DrawShape(ScheduleRow r, double x, double y, double wCell, double rh, double th, double u)
        {
            double mx = x + 300 * u, Mx = x + wCell - 300 * u, my = y - rh * 0.65, hk = rh * 0.35;
            string F(double v) => v.ToString("0", Ci);
            switch (r.Shape)
            {
                case ShapeKind.Straight:
                    w.Line("THEP", mx, my, Mx, my); w.Text("CHU", (mx + Mx) / 2, my + th * 0.3, th, F(r.Segments[0]), 1, 0, 0); break;
                case ShapeKind.LHook:
                {
                    bool hookFirst = r.Segments[0] < r.Segments[r.Segments.Count - 1];
                    double main = hookFirst ? r.Segments[1] : r.Segments[0], hook = hookFirst ? r.Segments[0] : r.Segments[1];
                    w.Line("THEP", mx, my, Mx, my);
                    double hx = hookFirst ? mx : Mx;
                    w.Line("THEP", hx, my, hx, my + hk);
                    w.Text("CHU", (mx + Mx) / 2, my + th * 0.3, th, F(main), 1, 0, 0);
                    w.Text("CHU", hx + (hookFirst ? th * 0.4 : -th * 0.4), my + hk / 2, th, F(hook), hookFirst ? 0 : 2, 2, 0);
                    break;
                }
                case ShapeKind.UHook:
                case ShapeKind.Crank:
                {
                    bool down = r.Shape == ShapeKind.Crank;
                    double hy = down ? my - hk * 0.6 : my + hk;
                    w.Poly("THEP", new[] { (mx, hy), (mx, my), (Mx, my), (Mx, hy) });
                    w.Text("CHU", (mx + Mx) / 2, my + th * 0.3, th, F(r.Segments[1]), 1, 0, 0);
                    w.Text("CHU", mx + th * 0.4, (my + hy) / 2, th, F(r.Segments[0]), 0, 2, 0);
                    w.Text("CHU", Mx - th * 0.4, (my + hy) / 2, th, F(r.Segments[2]), 2, 2, 0);
                    break;
                }
                case ShapeKind.Stirrup:
                {
                    double bw = Math.Min(wCell * 0.3, 1300 * u), bh = rh * 0.55;
                    double cx = x + wCell / 2, by = y - rh * 0.8;
                    w.Rect("THEP", cx - bw / 2, by, cx + bw / 2, by + bh);
                    w.Line("THEP", cx - bw / 2, by + bh, cx - bw / 2 + 110 * u, by + bh - 110 * u); // móc 45°
                    w.Line("THEP", cx - bw / 2 + 60 * u, by + bh, cx - bw / 2 + 170 * u, by + bh - 110 * u);
                    w.Text("CHU", cx, by + bh + th * 0.15, th, F(r.Segments[0]), 1, 0, 0);
                    w.Text("CHU", cx + bw / 2 + th * 0.3, by + bh / 2, th, F(r.Segments[1]), 0, 2, 0);
                    break;
                }
                case ShapeKind.CTie:
                    w.Poly("THEP", new[] { (mx + 150 * u, my + hk * 0.6), (mx, my), (Mx, my), (Mx - 150 * u, my + hk * 0.6) });
                    w.Text("CHU", (mx + Mx) / 2, my + th * 0.3, th, F(r.Segments[0]), 1, 0, 0);
                    break;
            }
        }
    }
}
