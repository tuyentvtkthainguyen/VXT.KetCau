using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Bản vẽ chi tiết cột: mặt đứng suốt các tầng (tl 1/50) + mặt cắt từng loại bố trí thép (tl 1/25).
/// Toạ độ mặt đứng: x = 0 tại mép trái cột, y = 0 tại chân cột (cao độ Stories[0].ZBottom), mm.</summary>
public static class ColumnDrawing
{
    public const double SheetScale = 50;
    public const double SectionScale = 25;

    public static List<Figure> BuildAll(Project p, IEnumerable<ColumnDesign> cols)
    {
        var res = new List<Figure>();
        foreach (var c in cols)
        {
            try { res.AddRange(Build(p, c)); }
            catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure(c.Name, ex, SheetScale), c.Name, true)); }
        }
        return res;
    }

    internal static List<StirrupZone> Zones(ColumnDesign c)
    {
        if (c.StirrupZones != null && c.StirrupZones.Count > 0) return c.StirrupZones.OrderBy(z => z.From).ToList();
        var res = new List<StirrupZone>();
        if (c.Stories.Count == 0) return res;
        double z0 = c.Stories[0].ZBottom;
        var st = c.Bars.FirstOrDefault(DrawUtil.IsStirrup);
        int i = 0;
        foreach (var s in c.Stories)
        {
            double a = s.ZBottom - z0, e = s.ZTop - z0, L = e - a;
            if (L <= 1e-4) { i++; continue; }
            double ze = Math.Min(L / 3, Math.Max(Math.Max(s.B, s.H), Math.Max(L / 6, 0.5)));
            int dia = s.StirrupDia > 0 ? s.StirrupDia : (st?.Dia ?? 6);
            double se = s.StirrupSpacingEnd > 0 ? s.StirrupSpacingEnd : (s.StirrupSpacing > 0 ? s.StirrupSpacing / 2 : 0.1);
            double sm = s.StirrupSpacing > 0 ? s.StirrupSpacing : 0.2;
            int mk = st?.Mark ?? 0;
            res.Add(new StirrupZone { Span = i, From = a, To = a + ze, Dia = dia, Spacing = se, Mark = mk });
            res.Add(new StirrupZone { Span = i, From = a + ze, To = e - ze, Dia = dia, Spacing = sm, Mark = mk });
            res.Add(new StirrupZone { Span = i, From = e - ze, To = e, Dia = dia, Spacing = se, Mark = mk });
            i++;
        }
        return res.Where(z => z.To - z.From > 1e-4).ToList();
    }

    private sealed class SecType
    {
        public int Num;
        public ColumnStoryResult Story;
        public double Zmid;          // m, tính từ chân cột
        public List<BarMark> Bars = new();
        public StirrupZone Zone;
        public List<BarMark> Ties = new();
    }

    public static List<Figure> Build(Project p, ColumnDesign c)
    {
        double S = SheetScale;
        var w = new DxfWriter(S);
        double th = w.Th;
        var figs = new List<Figure>();
        var stories = c.Stories.OrderBy(s => s.ZBottom).ToList();
        if (stories.Count == 0) return figs;
        double z0 = stories[0].ZBottom;
        double cover = Math.Max(15, p.Concrete.CoverColumn * 1000);
        double Y(double zAbs) => (zAbs - z0) * 1000;

        // bề rộng thể hiện trên mặt đứng: cạnh H (song song trục 2)
        double wMax = stories.Max(s => s.H) * 1000;
        double yTop = Y(stories[^1].ZTop);

        // ---- bê tông ----
        foreach (var s in stories)
        {
            double wd = s.H * 1000, y1 = Y(s.ZBottom), y2 = Y(s.ZTop);
            w.Line(Lay.BeTong, 0, y1, 0, y2);
            w.Line(Lay.BeTong, wd, y1, wd, y2);
            // sàn/dầm cắt tại đỉnh tầng
            double hb = 300, stub = Math.Max(400, 1.5 * wd);
            w.Line(Lay.BeTong, -stub, y2, 0, y2);
            w.Line(Lay.BeTong, -stub, y2 - hb, 0, y2 - hb);
            w.Line(Lay.BeTong, wd, y2, wd + stub, y2);
            w.Line(Lay.BeTong, wd, y2 - hb, wd + stub, y2 - hb);
            w.BreakV(Lay.BeTong, -stub, y2 - hb, y2);
            w.BreakV(Lay.BeTong, wd + stub, y2 - hb, y2);
        }
        w.Line(Lay.BeTong, 0, yTop, stories[^1].H * 1000, yTop);
        // cổ móng cụt dưới chân
        w.Line(Lay.BeTong, 0, 0, 0, -500);
        w.Line(Lay.BeTong, stories[0].H * 1000, 0, stories[0].H * 1000, -500);
        w.BreakH(Lay.BeTong, 0, stories[0].H * 1000, -500);

        // ---- thép dọc ----
        var longi = c.Bars.Where(b => !DrawUtil.IsStirrup(b)).ToList();
        var lanes = DrawUtil.AssignLanes(longi);
        int nl = lanes.Length == 0 ? 1 : lanes.Max() + 1;
        double wMin = stories.Min(s => s.H) * 1000;
        double xa = cover + 10, xb = wMin - cover - 10;
        var barX = new double[longi.Count];
        for (int i = 0; i < longi.Count; i++)
        {
            double xx = nl == 1 ? xa : xa + (xb - xa) * lanes[i] / Math.Max(1, nl - 1) * 0.5;
            barX[i] = xx;
            double y1 = Math.Min(longi[i].Start, longi[i].End) * 1000, y2 = Math.Max(longi[i].Start, longi[i].End) * 1000;
            var (hs, he) = DrawUtil.Hooks(longi[i]);
            var pts = new List<(double X, double Y)>();
            if (hs > 1) pts.Add((xx + Math.Min(hs, 600), y1));
            pts.Add((xx, y1));
            pts.Add((xx, y2));
            if (he > 1) pts.Add((xx + Math.Min(he, wMin - 2 * cover), y2));
            w.Polyline(Lay.Thep, pts, false, 0.3 * S);
            // thanh đối diện (mặt phải) cùng số hiệu
            double xr = wMin - xx;
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xr, y1), (xr, y2) }, false, 0.3 * S);
        }

        // ---- đai ----
        var zones = Zones(c);
        foreach (var z in zones)
        {
            double a = z.From * 1000, e = z.To * 1000, s = Math.Max(50, z.Spacing * 1000);
            double wd = (stories.FirstOrDefault(st => (st.ZTop - z0) * 1000 >= (a + e) / 2) ?? stories[^1]).H * 1000;
            for (double y = a + s / 2; y < e - 1e-6; y += s)
                w.Line(Lay.Dai, 0.6 * cover, y, wd - 0.6 * cover, y);
        }

        // ---- nhãn thép dọc (xoay dọc, bên trái) ----
        var layout = new TagLayout(0.2 * th);
        layout.Reserve(new Rect2(-Math.Max(400, 1.5 * wMax), -500, wMax + Math.Max(400, 1.5 * wMax), yTop));
        double tagX = -Math.Max(400, 1.5 * wMax) - 1.5 * th;
        var used = new List<double>();
        for (int i = 0; i < longi.Count; i++)
        {
            var bm = longi[i];
            double y1 = Math.Min(bm.Start, bm.End) * 1000, y2 = Math.Max(bm.Start, bm.End) * 1000;
            double ay = DrawUtil.PickAnchor(y1 + 0.1 * (y2 - y1), y2 - 0.3 * (y2 - y1), used, 3 * th);
            string txt = DrawUtil.BarTag(bm.Count, bm.Dia);
            var box = w.TagBox(tagX, ay, txt, bm.Mark, 1, true);
            var (dx, _) = layout.Place(box, -2.0 * th, 0);
            double kx = tagX + dx;
            w.Dot(Lay.KyHieu, barX[i], ay, 0.22 * th);
            w.Line(Lay.KyHieu, barX[i], ay, kx, ay);
            w.LeaderTag(kx, ay, kx, ay, txt, bm.Mark, 1, true, false);
        }
        double leftMost = Math.Min(tagX, layout.Occupied.Min(r => r.X1));

        // ---- chuỗi vùng đai bên phải ----
        double xChain = wMax + Math.Max(400, 1.5 * wMax) + 2.5 * th;
        foreach (var z in zones)
        {
            double a = z.From * 1000, e = z.To * 1000;
            w.Dim(xChain, a, xChain, e, 0);
            string t = DrawUtil.StirrupTag(z.Dia, z.Spacing);
            double tw = DxfWriter.TextWidth(t, th) + 2.4 * w.MarkR;
            double cy = (a + e) / 2 - tw / 2;
            if (e - a > tw * 0.6)
            {
                w.Text(Lay.Chu, xChain + 1.8 * th, cy, th, t, 90, 0, 0);
                if (z.Mark > 0) w.MarkCircle(xChain + 1.4 * th, cy + DxfWriter.TextWidth(t, th) + 1.3 * w.MarkR, z.Mark, 90);
            }
        }

        // ---- kích thước chiều cao + cao độ bên trái ----
        var lv = new List<double> { 0 };
        foreach (var s in stories) lv.Add(Y(s.ZTop));
        double xDim = leftMost - 2.5 * th;
        w.DimChainV(lv, xDim, xDim);
        foreach (var bm in longi.Where(b => b.Start < -1e-4 || b.End > (stories[^1].ZTop - z0) + 1e-4))
        {
            double y1 = Math.Min(bm.Start, bm.End) * 1000, y2 = Math.Max(bm.Start, bm.End) * 1000;
            if (y1 < -1) w.Dim(xDim, y1, xDim, 0, 0);
            if (y2 > yTop + 1) w.Dim(xDim, yTop, xDim, y2, 0);
        }
        // đoạn chờ nối: khoảng chồng giữa thanh tầng dưới và trên (cùng làn trái)
        foreach (var s in stories.Skip(1))
        {
            double yb = Y(s.ZBottom);
            var over = longi.Where(b => Math.Min(b.Start, b.End) * 1000 > yb - 1 && Math.Min(b.Start, b.End) * 1000 < yb + 2000).ToList();
            var under = longi.Where(b => Math.Max(b.Start, b.End) * 1000 > yb + 1 && Math.Min(b.Start, b.End) * 1000 < yb - 1).ToList();
            if (under.Count > 0)
            {
                double yl = under.Max(b => Math.Max(b.Start, b.End)) * 1000;
                if (yl - yb > 50 && yl - yb < 3000) w.Dim(xDim + 3 * th, yb, xDim + 3 * th, yl, 0);
            }
            _ = over;
        }
        double xEl = xDim - 3.5 * th;
        w.ElevMark(xEl, 0, DxfWriter.ElevText(z0), -1);
        foreach (var s in stories) w.ElevMark(xEl, Y(s.ZTop), DxfWriter.ElevText(s.ZTop), -1);

        // ---- mặt cắt ----
        var types = new List<SecType>();
        foreach (var s in stories)
        {
            double zm = (s.ZBottom + s.ZTop) / 2 - z0;
            var bars = longi.Where(b => zm >= Math.Min(b.Start, b.End) - 1e-4 && zm <= Math.Max(b.Start, b.End) + 1e-4).ToList();
            var zone = zones.FirstOrDefault(z => zm >= z.From - 1e-4 && zm <= z.To + 1e-4);
            var ties = c.Bars.Where(b => (b.Shape ?? "") == "Đai C" || (b.Role ?? "") == "Đai C").ToList();
            string key = $"{Math.Round(s.B * 1000)}x{Math.Round(s.H * 1000)}|{s.Dia}|{s.BarsAlongB}|{s.BarsAlongH}|" +
                         string.Join(",", bars.OrderBy(b => b.Mark).Select(b => b.Mark)) + "|" +
                         (zone == null ? "" : $"{zone.Dia}:{Math.Round(zone.Spacing * 1000)}:{zone.Mark}");
            var ex = types.FirstOrDefault(t => t.Bars.Count >= 0 && TypeKey(t) == key);
            if (ex == null)
            {
                ex = new SecType { Num = types.Count + 1, Story = s, Zmid = zm, Bars = bars, Zone = zone, Ties = ties };
                types.Add(ex);
            }
            double ycut = zm * 1000;
            double xx1 = -0.6 * th, xx2 = s.H * 1000 + 0.6 * th;
            w.CutMark(xx1 - 1.5 * th, ycut, xx1, ycut, ex.Num.ToString(), xx1 - 1.0 * th, ycut + 1.3 * th);
            w.CutMark(xx2, ycut, xx2 + 1.5 * th, ycut, ex.Num.ToString(), xx2 + 1.0 * th, ycut + 1.3 * th);
        }

        string title = $"{c.Name} (SL={Math.Max(1, c.Quantity)})";
        w.Title(wMax / 2, w.Extents.Y2 + 5 * th, title, DrawUtil.ScaleText(S));
        figs.Add(new Figure(w, c.Name, true));
        foreach (var t in types) figs.Add(new Figure(Section(t, cover, S), c.Name + " " + t.Num));
        return figs;
    }

    private static string TypeKey(SecType t)
    {
        var s = t.Story;
        return $"{Math.Round(s.B * 1000)}x{Math.Round(s.H * 1000)}|{s.Dia}|{s.BarsAlongB}|{s.BarsAlongH}|" +
               string.Join(",", t.Bars.OrderBy(b => b.Mark).Select(b => b.Mark)) + "|" +
               (t.Zone == null ? "" : $"{t.Zone.Dia}:{Math.Round(t.Zone.Spacing * 1000)}:{t.Zone.Mark}");
    }

    /// <summary>Vị trí thanh dọc trên chu vi (mm, gốc góc dưới trái): b theo x, h theo y.</summary>
    internal static List<(double X, double Y, bool Corner)> Perimeter(double b, double h, int nb, int nh, double c)
    {
        var r = new List<(double X, double Y, bool Corner)>();
        nb = Math.Max(2, nb); nh = Math.Max(2, nh);
        double x1 = c, x2 = b - c, y1 = c, y2 = h - c;
        for (int i = 0; i < nb; i++)
        {
            double x = x1 + (x2 - x1) * i / (nb - 1);
            bool corner = i == 0 || i == nb - 1;
            r.Add((x, y1, corner));
            r.Add((x, y2, corner));
        }
        for (int j = 1; j < nh - 1; j++)
        {
            double y = y1 + (y2 - y1) * j / (nh - 1);
            r.Add((x1, y, false));
            r.Add((x2, y, false));
        }
        return r;
    }

    private static DxfWriter Section(SecType t, double cover, double S)
    {
        var w = new DxfWriter(S);
        double th = w.Th, k = S / SectionScale;
        var s = t.Story;
        double b = s.B * 1000, h = s.H * 1000, B = b * k, H = h * k;
        w.Rect(Lay.BeTong, 0, 0, B, H);
        int ds = t.Zone?.Dia ?? (s.StirrupDia > 0 ? s.StirrupDia : 6);
        double cs = Math.Max(0.5 * cover, cover - ds / 2.0) * k;
        w.Polyline(Lay.Dai, new List<(double X, double Y)> { (cs, cs), (B - cs, cs), (B - cs, H - cs), (cs, H - cs) }, true, 0.25 * S);
        double hk = Math.Min(0.25 * Math.Min(B, H), 80 * k);
        w.Line(Lay.Dai, cs, H - cs - 0.25 * hk, cs + 0.7 * hk, H - cs - 0.95 * hk);
        w.Line(Lay.Dai, cs + 0.25 * hk, H - cs, cs + 0.95 * hk, H - cs - 0.7 * hk);

        int dia = s.Dia > 0 ? s.Dia : (t.Bars.Count > 0 ? t.Bars.Max(x => x.Dia) : 16);
        double cc = cover + dia / 2.0;
        var pos = Perimeter(b, h, s.BarsAlongB, s.BarsAlongH, cc);
        // gán số hiệu: góc trước cho thanh Ø lớn nhất
        var marks = t.Bars.OrderByDescending(x => x.Dia).ThenBy(x => x.Mark).ToList();
        var assign = new BarMark[pos.Count];
        var order = Enumerable.Range(0, pos.Count).OrderBy(i => pos[i].Corner ? 0 : 1).ToList();
        int mi = 0, left = marks.Count > 0 ? marks[0].Count : 0;
        foreach (int i in order)
        {
            while (mi < marks.Count && left <= 0) { mi++; if (mi < marks.Count) left = marks[mi].Count; }
            if (mi < marks.Count) { assign[i] = marks[mi]; left--; }
            else assign[i] = marks.Count > 0 ? marks[^1] : null;
        }
        for (int i = 0; i < pos.Count; i++)
        {
            int d = assign[i]?.Dia ?? dia;
            w.Dot(Lay.Thep, pos[i].X * k, pos[i].Y * k, Math.Max(d / 2.0 * k, 0.18 * th));
        }
        // đai C (giằng) nếu có
        if (t.Ties.Count > 0 && s.BarsAlongB > 3)
        {
            double xm = pos.Where(q => !q.Corner && Math.Abs(q.Y - cc) < 1).Select(q => q.X).DefaultIfEmpty(b / 2).First() * k;
            w.Line(Lay.Dai, xm, cs, xm, H - cs);
        }

        var layout = new TagLayout(0.2 * th);
        layout.Reserve(new Rect2(0, 0, B, H));
        var groups = Enumerable.Range(0, pos.Count).Where(i => assign[i] != null).GroupBy(i => assign[i].Mark).OrderBy(g => g.Key);
        foreach (var g in groups)
        {
            var idx = g.ToList();
            var bar = assign[idx[0]];
            bool top = idx.Any(i => pos[i].Y > h / 2);
            var rowIdx = idx.Where(i => Math.Abs(pos[i].Y - (top ? h - cc : cc)) < 1).OrderBy(i => pos[i].X).ToList();
            if (rowIdx.Count == 0) rowIdx = idx;
            if (rowIdx.Count > 1)
                w.Line(Lay.KyHieu, pos[rowIdx[0]].X * k, pos[rowIdx[0]].Y * k, pos[rowIdx[^1]].X * k, pos[rowIdx[^1]].Y * k);
            double px = (pos[rowIdx[0]].X + pos[rowIdx[^1]].X) / 2 * k, py = pos[rowIdx[0]].Y * k;
            string txt = DrawUtil.BarTag(idx.Count, bar.Dia);
            double ky = top ? H + 2.2 * th : -2.6 * th;
            var box = w.TagBox(px, ky, txt, bar.Mark, 1);
            var (_, dy) = layout.Place(box, 0, top ? 1.8 * th : -1.8 * th);
            w.LeaderTag(px, py, px, ky + dy, txt, bar.Mark, 1, false, rowIdx.Count == 1);
        }
        if (t.Zone != null || s.StirrupDia > 0)
        {
            int zd = t.Zone?.Dia ?? s.StirrupDia;
            double sp = t.Zone?.Spacing ?? s.StirrupSpacing;
            string txt = DrawUtil.StirrupTag(zd, sp);
            var box = w.TagBox(B + 2 * th, H * 0.6, txt, t.Zone?.Mark ?? 0, 1);
            var (_, dy) = layout.Place(box, 0, 1.8 * th);
            w.LeaderTag(B - cs, H * 0.6, B + 2 * th, H * 0.6 + dy, txt, t.Zone?.Mark ?? 0, 1);
        }
        double yMin = Math.Min(0, layout.Occupied.Min(r => r.Y1));
        w.Dim(0, 0, B, 0, yMin - 2.0 * th, k);
        w.Dim(0, 0, 0, H, 2.6 * th, k);

        // hình đai tách riêng bên phải
        double ox = Math.Max(w.Extents.X2, B + 4 * th) + 3 * th;
        double sb = (b - 2 * cover + ds) * k, shh = (h - 2 * cover + ds) * k;
        w.Polyline(Lay.Dai, new List<(double X, double Y)> { (ox, 0), (ox + sb, 0), (ox + sb, shh), (ox, shh) }, true, 0.25 * S);
        w.Line(Lay.Dai, ox, shh - 0.25 * hk, ox + 0.7 * hk, shh - 0.95 * hk);
        w.Line(Lay.Dai, ox + 0.25 * hk, shh, ox + 0.95 * hk, shh - 0.7 * hk);
        w.Text(Lay.Chu, ox + sb / 2, -1.2 * th, th, DxfWriter.N0(b - 2 * cover + ds), 0, 1, 3);
        w.Text(Lay.Chu, ox + sb + 0.8 * th, shh / 2, th, DxfWriter.N0(h - 2 * cover + ds), 90, 1, 3);
        if (t.Zone != null && t.Zone.Mark > 0) w.MarkCircle(ox + sb / 2, shh / 2, t.Zone.Mark);

        w.Title(B / 2, w.Extents.Y1 - 1.2 * th, $"{t.Num}-{t.Num}", DrawUtil.ScaleText(SectionScale));
        return w;
    }
}
