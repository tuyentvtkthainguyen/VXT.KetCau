using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Bản vẽ chi tiết dầm: mặt đứng tl 1/50 + mặt cắt 1-1 (gối), 2-2 (nhịp), 3-3... tl 1/25.
/// Toạ độ mặt đứng: x = 0 tại tim trục gối đầu tiên, y = 0 tại đỉnh dầm (mm).</summary>
public static class BeamDrawing
{
    public const double SheetScale = 50;
    public const double SectionScale = 25;

    public static List<Figure> BuildAll(Project p, IEnumerable<BeamLineDesign> beams)
    {
        var res = new List<Figure>();
        foreach (var d in beams)
        {
            try { res.AddRange(Build(p, d)); }
            catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure(d.Name, ex, SheetScale), d.Name, true)); }
        }
        return res;
    }

    private sealed class Seg
    {
        public BarMark Bar;
        public double X1, X2, Y;
        public int Lane;
    }

    /// <summary>Vẽ một dầm liên tục: hình đầu là mặt đứng (hàng mới), sau đó là các mặt cắt.</summary>
    public static List<Figure> Build(Project p, BeamLineDesign d)
    {
        double S = SheetScale;
        var w = new DxfWriter(S);
        double th = w.Th;
        var figs = new List<Figure>();

        double b = d.B * 1000, h = d.H * 1000;
        if (b <= 0 || h <= 0)
        {
            var si = DrawUtil.SectionOf(p, d.Section);
            if (b <= 0) b = si.B * 1000;
            if (h <= 0) h = si.H * 1000;
        }
        double cover = Math.Max(15, p.Concrete.CoverBeam * 1000);

        // ---- gối / trục ----
        var spans = d.Spans ?? new List<BeamSpanResult>();
        var xs = new List<double> { 0 };
        foreach (var sp in spans) xs.Add(xs[^1] + sp.L * 1000);
        if (spans.Count == 0)
        {
            double tl = d.Bars.Count > 0 ? d.Bars.Max(x => Math.Max(x.Start, x.End)) * 1000 : 3000;
            xs.Add(Math.Max(1000, tl));
        }
        int ns = xs.Count;
        var sw = new double[ns];
        var gn = new string[ns];
        for (int i = 0; i < ns; i++)
        {
            double v;
            if (spans.Count == 0) v = 220;
            else if (i == 0) v = spans[0].SupportWidthLeft;
            else if (i == ns - 1) v = spans[^1].SupportWidthRight;
            else v = Math.Max(spans[i - 1].SupportWidthRight, spans[i].SupportWidthLeft);
            sw[i] = v > 1e-6 ? v * 1000 : 220;
            gn[i] = spans.Count == 0 ? "" : (i == 0 ? spans[0].LeftSupport : spans[i - 1].RightSupport) ?? "";
        }
        double xL = xs[0] - sw[0] / 2, xR = xs[^1] + sw[^1] / 2;
        double stubUp = Math.Max(0.6 * h, 300), stubDn = Math.Max(0.8 * h, 400);

        // ---- bê tông: dầm giữa các mặt cột + cột cụt ----
        for (int i = 0; i + 1 < ns; i++)
        {
            double a = xs[i] + sw[i] / 2, e = xs[i + 1] - sw[i + 1] / 2;
            w.Line(Lay.BeTong, a, 0, e, 0);
            w.Line(Lay.BeTong, a, -h, e, -h);
        }
        for (int i = 0; i < ns; i++)
        {
            double c1 = xs[i] - sw[i] / 2, c2 = xs[i] + sw[i] / 2;
            w.Line(Lay.BeTong, c1, -h - stubDn, c1, stubUp);
            w.Line(Lay.BeTong, c2, -h - stubDn, c2, stubUp);
            w.BreakH(Lay.BeTong, c1, c2, stubUp);
            w.BreakH(Lay.BeTong, c1, c2, -h - stubDn);
        }

        var layout = new TagLayout(0.2 * th);
        layout.Reserve(new Rect2(xL, -h - 0.5 * th, xR, 0.5 * th));
        for (int i = 0; i < ns; i++)
            layout.Reserve(new Rect2(xs[i] - sw[i] / 2, -h - stubDn - 0.5 * th, xs[i] + sw[i] / 2, stubUp + 0.5 * th));

        // ---- vị trí mặt cắt ----
        var cuts = ComputeCuts(d, xs, sw, h);
        foreach (var c in cuts)
        {
            w.CutMark(c.X, 0.5 * th, c.X, 2.3 * th, c.Num.ToString(), c.X + 0.9 * th, 1.4 * th);
            w.CutMark(c.X, -h - 0.5 * th, c.X, -h - 2.3 * th, c.Num.ToString(), c.X + 0.9 * th, -h - 1.4 * th);
            layout.Reserve(new Rect2(c.X - 0.3 * th, 0.4 * th, c.X + 1.7 * th, 2.4 * th));
            layout.Reserve(new Rect2(c.X - 0.3 * th, -h - 2.4 * th, c.X + 1.7 * th, -h - 0.4 * th));
        }

        // ---- thép dọc ----
        var longi = d.Bars.Where(x => !DrawUtil.IsStirrup(x)).ToList();
        var gia = longi.Where(x => (x.Role ?? "").Contains("Giá")).ToList();
        var tops = longi.Where(x => x.IsTop && !gia.Contains(x)).ToList();
        var bots = longi.Where(x => !x.IsTop && !gia.Contains(x)).ToList();
        double laneGap = Math.Max(20, Math.Min(40, (h - 2 * cover) / 8));
        var segs = new List<Seg>();
        void AddSegs(List<BarMark> list, bool top)
        {
            var lanes = DrawUtil.AssignLanes(list);
            for (int i = 0; i < list.Count; i++)
            {
                var bm = list[i];
                double y = top ? -(cover + lanes[i] * laneGap) : -h + cover + lanes[i] * laneGap;
                segs.Add(new Seg
                {
                    Bar = bm, Lane = lanes[i], Y = y,
                    X1 = Math.Min(bm.Start, bm.End) * 1000, X2 = Math.Max(bm.Start, bm.End) * 1000
                });
            }
        }
        AddSegs(tops, true);
        AddSegs(bots, false);
        foreach (var g in gia)
            segs.Add(new Seg { Bar = g, Lane = 0, Y = -h / 2, X1 = Math.Min(g.Start, g.End) * 1000, X2 = Math.Max(g.Start, g.End) * 1000 });

        double maxHook = Math.Max(50, h - 2 * cover);
        foreach (var sg in segs)
        {
            var (hs, he) = DrawUtil.Hooks(sg.Bar);
            bool top = sg.Y > -h / 2 + 1e-6 || (sg.Bar.IsTop && sg.Y >= -h / 2 - 1e-6);
            double dir = top ? -1 : 1;
            var pts = new List<(double X, double Y)>();
            if (hs > 1) pts.Add((sg.X1, sg.Y + dir * Math.Min(hs, maxHook)));
            pts.Add((sg.X1, sg.Y));
            pts.Add((sg.X2, sg.Y));
            if (he > 1) pts.Add((sg.X2, sg.Y + dir * Math.Min(he, maxHook)));
            w.Polyline(Lay.Thep, pts, false, 0.3 * S);
        }

        // ---- đai ----
        var zones = Zones(d);
        foreach (var z in zones)
        {
            double a = z.From * 1000, e = z.To * 1000, s = Math.Max(50, z.Spacing * 1000);
            for (double x = a + s / 2; x < e - 1e-6; x += s)
                w.Line(Lay.Dai, x, -0.6 * cover, x, -h + 0.6 * cover);
        }

        // ---- nhãn thép dọc ----
        var usedTop = new List<double>();
        var usedBot = new List<double>();
        foreach (var sg in segs.OrderBy(x => x.Lane).ThenBy(x => x.X1))
        {
            bool top = sg.Bar.IsTop && !gia.Contains(sg.Bar);
            string txt = DrawUtil.BarTag(sg.Bar.Count, sg.Bar.Dia);
            double len = sg.X2 - sg.X1;
            double ax = DrawUtil.PickAnchor(sg.X1 + 0.08 * len, sg.X2 - 0.08 * len, top ? usedTop : usedBot, 1.6 * th);
            if (top)
            {
                double ky = 3.2 * th;
                var box = w.TagBox(ax, ky, txt, sg.Bar.Mark, 1);
                var (_, dy) = layout.Place(box, 0, 1.8 * th);
                w.LeaderTag(ax, sg.Y, ax, ky + dy, txt, sg.Bar.Mark, 1);
            }
            else
            {
                double ky = -h - 3.6 * th;
                var box = w.TagBox(ax, ky, txt, sg.Bar.Mark, 1);
                var (_, dy) = layout.Place(box, 0, -1.8 * th);
                w.LeaderTag(ax, sg.Y, ax, ky + dy, txt, sg.Bar.Mark, 1);
            }
        }
        double topMax = Math.Max(stubUp, layout.Occupied.Count > 0 ? layout.Occupied.Max(r => r.Y2) : 0);
        double botMin = Math.Min(-h - stubDn, layout.Occupied.Count > 0 ? layout.Occupied.Min(r => r.Y1) : -h);

        // ---- chuỗi vùng đai trên cùng ----
        var zt = zones.Select(z => (Z: z, Text: DrawUtil.StirrupTag(z.Dia, z.Spacing))).ToList();
        var zRows = new int[zt.Count];
        var rowEnds = new List<double>();
        for (int i = 0; i < zt.Count; i++)
        {
            double cx = (zt[i].Z.From + zt[i].Z.To) * 500;
            double tw = DxfWriter.TextWidth(zt[i].Text, th) + 2.6 * w.MarkR;
            double x1 = cx - tw / 2, x2 = cx + tw / 2;
            int r = 0;
            while (r < rowEnds.Count && rowEnds[r] > x1) r++;
            if (r == rowEnds.Count) rowEnds.Add(x2); else rowEnds[r] = x2;
            zRows[i] = r;
        }
        int nRows = Math.Max(1, rowEnds.Count);
        double yChain = topMax + (nRows * 1.8 + 1.5) * th;
        for (int i = 0; i < zt.Count; i++)
        {
            var z = zt[i].Z;
            double a = z.From * 1000, e = z.To * 1000;
            w.Dim(a, yChain, e, yChain, 0);
            double cx = (a + e) / 2;
            double tw = DxfWriter.TextWidth(zt[i].Text, th);
            double tot = tw + 2.6 * w.MarkR;
            double yt = yChain - (zRows[i] + 1) * 1.8 * th;
            double tx = cx - tot / 2;
            w.Text(Lay.Chu, tx, yt, th, zt[i].Text, 0, 0, 0);
            if (z.Mark > 0) w.MarkCircle(tx + tw + 1.3 * w.MarkR, yt + 0.4 * th, z.Mark);
        }

        // ---- kích thước nhịp + tổng ----
        double yD1 = botMin - 2.5 * th, yD2 = yD1 - 2.6 * th, yBub = yD2 - 3.2 * th;
        w.DimChainH(xs, yD1, yD1);
        if (ns > 2) w.Dim(xs[0], yD2, xs[^1], yD2, 0);
        for (int i = 0; i < ns; i++)
        {
            w.Line(Lay.Truc, xs[i], stubUp + 0.8 * th, xs[i], yBub + 1.5 * th, "CENTER");
            if (!string.IsNullOrEmpty(gn[i])) w.GridBubble(xs[i], yBub, gn[i]);
        }

        // ---- cao độ đỉnh dầm bên trái ----
        w.ElevMark(xL - 2.5 * th, 0, DxfWriter.ElevText(d.TopElevation), -1);
        w.Line(Lay.KyHieu, xL - 2.5 * th, 0, xL - 0.3 * th, 0);

        // ---- tên hình ----
        double Lout = (xs[^1] - xs[0]) + sw[0] / 2 + sw[^1] / 2;
        string title = $"{d.Name} (SL={Math.Max(1, d.Quantity)}; L={DxfWriter.N0(Math.Round(Lout / 10) * 10)})";
        w.Title((xs[0] + xs[^1]) / 2, yChain + 6.5 * th, title, DrawUtil.ScaleText(S));
        figs.Add(new Figure(w, d.Name, true));

        // ---- mặt cắt ----
        foreach (var c in cuts.GroupBy(x => x.Num).Select(g => g.First()).OrderBy(x => x.Num))
            figs.Add(new Figure(Section(c, b, h, cover, S), d.Name + " " + c.Num));
        return figs;
    }

    // ==================================================================
    internal sealed class Cut
    {
        public int Num;
        public double X;          // mm
        public List<BarMark> Top = new();
        public List<BarMark> Bot = new();
        public List<BarMark> Side = new();
        public StirrupZone Zone;
        public string Key = "";
    }

    internal static List<StirrupZone> Zones(BeamLineDesign d)
    {
        if (d.StirrupZones != null && d.StirrupZones.Count > 0) return d.StirrupZones.OrderBy(z => z.From).ToList();
        var res = new List<StirrupZone>();
        var st = d.Bars.FirstOrDefault(DrawUtil.IsStirrup);
        double x0 = 0;
        for (int i = 0; i < d.Spans.Count; i++)
        {
            var sp = d.Spans[i];
            double a = x0 + sp.SupportWidthLeft / 2, e = x0 + sp.L - sp.SupportWidthRight / 2;
            double zl = Math.Min(sp.SupportZone > 0 ? sp.SupportZone : sp.Lclear / 4, (e - a) / 2);
            int dia = sp.StirrupDia > 0 ? sp.StirrupDia : (st?.Dia ?? 6);
            double s1 = sp.StirrupSupport > 0 ? sp.StirrupSupport : 0.1, s2 = sp.StirrupMid > 0 ? sp.StirrupMid : 0.2;
            int mk = st?.Mark ?? 0;
            res.Add(new StirrupZone { Span = i, From = a, To = a + zl, Dia = dia, Spacing = s1, Mark = mk });
            res.Add(new StirrupZone { Span = i, From = a + zl, To = e - zl, Dia = dia, Spacing = s2, Mark = mk });
            res.Add(new StirrupZone { Span = i, From = e - zl, To = e, Dia = dia, Spacing = s1, Mark = mk });
            x0 += sp.L;
        }
        return res.Where(z => z.To - z.From > 1e-4).ToList();
    }

    private static List<Cut> ComputeCuts(BeamLineDesign d, List<double> xs, double[] sw, double h)
    {
        var zones = Zones(d);
        var pos = new List<double>();
        for (int i = 0; i + 1 < xs.Count; i++)
        {
            double a = xs[i] + sw[i] / 2, e = xs[i + 1] - sw[i + 1] / 2;
            double off = Math.Min(Math.Max(h, 300), (e - a) / 5);
            pos.Add(a + off);
            pos.Add((a + e) / 2);
            if (i == xs.Count - 2) pos.Add(e - off);
        }
        var cuts = new List<Cut>();
        var keys = new Dictionary<string, int>();
        foreach (double x in pos)
        {
            double xm = x / 1000;
            var c = new Cut { X = x };
            foreach (var bm in d.Bars.Where(bb => !DrawUtil.IsStirrup(bb)))
            {
                double a = Math.Min(bm.Start, bm.End), e = Math.Max(bm.Start, bm.End);
                if (xm < a - 1e-4 || xm > e + 1e-4) continue;
                if ((bm.Role ?? "").Contains("Giá")) c.Side.Add(bm);
                else if (bm.IsTop) c.Top.Add(bm);
                else c.Bot.Add(bm);
            }
            c.Zone = zones.FirstOrDefault(z => xm >= z.From - 1e-4 && xm <= z.To + 1e-4) ?? zones.FirstOrDefault();
            string Sig(List<BarMark> l) => string.Join(",", l.OrderBy(t => t.Mark).Select(t => $"{t.Mark}:{t.Count}:{t.Dia}"));
            c.Key = Sig(c.Top) + "|" + Sig(c.Bot) + "|" + Sig(c.Side) + "|" +
                    (c.Zone == null ? "" : $"{c.Zone.Dia}:{Math.Round(c.Zone.Spacing * 1000)}:{c.Zone.Legs}:{c.Zone.Mark}");
            if (!keys.TryGetValue(c.Key, out int n)) { n = keys.Count + 1; keys[c.Key] = n; }
            c.Num = n;
            cuts.Add(c);
        }
        return cuts;
    }

    /// <summary>Thứ tự vị trí trong một hàng: 2 góc trước, sau đó từ giữa ra.</summary>
    internal static List<int> CornerFirstOrder(int n)
    {
        var o = new List<int>();
        if (n <= 0) return o;
        o.Add(0);
        if (n > 1) o.Add(n - 1);
        var mid = Enumerable.Range(1, Math.Max(0, n - 2)).OrderBy(i => Math.Abs(i - (n - 1) / 2.0)).ThenBy(i => i);
        o.AddRange(mid);
        return o;
    }

    /// <summary>Đặt n thanh vào một hàng từ x1 tới x2.</summary>
    internal static List<double> RowXs(int n, double x1, double x2)
    {
        var r = new List<double>();
        if (n <= 0) return r;
        if (n == 1) { r.Add((x1 + x2) / 2); return r; }
        for (int i = 0; i < n; i++) r.Add(x1 + (x2 - x1) * i / (n - 1));
        return r;
    }

    /// <summary>Bố trí thép một phía (trên/dưới) của mặt cắt dầm, toạ độ đã nhân k.
    /// Trả về danh sách (thanh, x, y).</summary>
    private static List<(BarMark Bar, double X, double Y)> PlaceRow(List<BarMark> bars, double b, double h, double cover,
        double k, bool top)
    {
        var res = new List<(BarMark Bar, double X, double Y)>();
        var ordered = bars.OrderBy(x => DrawUtil.RolePriority(x.Role)).ThenByDescending(x => x.Dia).ToList();
        var items = new List<BarMark>();
        foreach (var bm in ordered) for (int i = 0; i < Math.Max(0, bm.Count); i++) items.Add(bm);
        if (items.Count == 0) return res;
        double dmax = items.Max(x => x.Dia);
        int perRow = Math.Max(2, (int)Math.Floor((b - 2 * cover + 25) / (dmax + 25)));
        var row1 = items.Take(perRow).ToList();
        var row2 = items.Skip(perRow).ToList();
        double xa = cover + dmax / 2, xb = b - cover - dmax / 2;
        double y1 = top ? h - cover - dmax / 2 : cover + dmax / 2;
        double y2 = top ? y1 - (dmax + 25) : y1 + (dmax + 25);
        void Fill(List<BarMark> row, double y)
        {
            var xsr = RowXs(row.Count, xa, xb);
            var ord = CornerFirstOrder(row.Count);
            for (int i = 0; i < row.Count; i++) res.Add((row[i], xsr[ord[i]] * k, y * k));
        }
        Fill(row1, y1);
        if (row2.Count > 0) Fill(row2, y2);
        return res;
    }

    private static DxfWriter Section(Cut c, double b, double h, double cover, double S)
    {
        var w = new DxfWriter(S);
        double th = w.Th, k = S / SectionScale;
        double B = b * k, H = h * k;
        w.Rect(Lay.BeTong, 0, 0, B, H);
        // đai
        int ds = c.Zone?.Dia ?? 6;
        double cs = Math.Max(0.5 * cover, cover - ds / 2.0) * k;
        w.Polyline(Lay.Dai, new List<(double X, double Y)> { (cs, cs), (B - cs, cs), (B - cs, H - cs), (cs, H - cs) }, true, 0.25 * S);
        double hk = Math.Min(0.25 * Math.Min(B, H), 80 * k);
        w.Line(Lay.Dai, cs, H - cs - 0.25 * hk, cs + 0.7 * hk, H - cs - 0.95 * hk);
        w.Line(Lay.Dai, cs + 0.25 * hk, H - cs, cs + 0.95 * hk, H - cs - 0.7 * hk);
        if (c.Zone != null && c.Zone.Legs > 2)
            w.Line(Lay.Dai, B / 2, cs, B / 2, H - cs);

        var layout = new TagLayout(0.2 * th);
        layout.Reserve(new Rect2(0, 0, B, H));
        var topPos = PlaceRow(c.Top, b, h, cover, k, true);
        var botPos = PlaceRow(c.Bot, b, h, cover, k, false);
        foreach (var (bar, x, y) in topPos.Concat(botPos)) w.Dot(Lay.Thep, x, y, Math.Max(bar.Dia / 2.0 * k, 0.18 * th));

        void TagGroups(List<(BarMark Bar, double X, double Y)> pos, bool top)
        {
            foreach (var g in pos.GroupBy(t => t.Bar.Mark).OrderBy(gg => gg.Min(t => t.X)))
            {
                var list = g.ToList();
                foreach (var row in list.GroupBy(t => Math.Round(t.Y, 1)))
                {
                    var rl = row.OrderBy(t => t.X).ToList();
                    if (rl.Count > 1) w.Line(Lay.KyHieu, rl[0].X, rl[0].Y, rl[^1].X, rl[^1].Y);
                }
                var bar = list[0].Bar;
                var p0 = list.OrderBy(t => top ? -t.Y : t.Y).ThenBy(t => Math.Abs(t.X - B / 2)).First();
                double px = list.Count > 1 && list.Select(t => Math.Round(t.Y, 1)).Distinct().Count() == 1
                    ? (list.Min(t => t.X) + list.Max(t => t.X)) / 2 : p0.X;
                string txt = DrawUtil.BarTag(list.Count, bar.Dia);
                double ky = top ? H + 2.2 * th : -2.6 * th;
                var box = w.TagBox(px, ky, txt, bar.Mark, 1);
                var (_, dy) = layout.Place(box, 0, top ? 1.8 * th : -1.8 * th);
                w.LeaderTag(px, p0.Y, px, ky + dy, txt, bar.Mark, 1, false, list.Count == 1);
            }
        }
        TagGroups(topPos, true);
        TagGroups(botPos, false);

        // thép giá 2 bên
        if (c.Side.Count > 0)
        {
            foreach (var sb in c.Side)
            {
                int per = Math.Max(1, sb.Count / 2);
                for (int i = 0; i < per; i++)
                {
                    double y = H * (i + 1) / (per + 1);
                    w.Dot(Lay.Thep, (cover + sb.Dia / 2.0) * k, y, Math.Max(sb.Dia / 2.0 * k, 0.18 * th));
                    w.Dot(Lay.Thep, B - (cover + sb.Dia / 2.0) * k, y, Math.Max(sb.Dia / 2.0 * k, 0.18 * th));
                }
                string txt = DrawUtil.BarTag(sb.Count, sb.Dia);
                double px = B - (cover + sb.Dia / 2.0) * k, py = H / (per + 1);
                var box = w.TagBox(B + 2 * th, py - 1.5 * th, txt, sb.Mark, 1);
                var (_, dy) = layout.Place(box, 0, -1.8 * th);
                w.LeaderTag(px, py, B + 2 * th, py - 1.5 * th + dy, txt, sb.Mark, 1);
            }
        }
        // nhãn đai sang phải
        if (c.Zone != null)
        {
            string txt = DrawUtil.StirrupTag(c.Zone.Dia, c.Zone.Spacing);
            double py = H * 0.6;
            var box = w.TagBox(B + 2 * th, py, txt, c.Zone.Mark, 1);
            var (_, dy) = layout.Place(box, 0, 1.8 * th);
            w.LeaderTag(B - cs, py, B + 2 * th, py + dy, txt, c.Zone.Mark, 1);
        }
        double yMin = Math.Min(0, layout.Occupied.Min(r => r.Y1));
        w.Dim(0, 0, B, 0, yMin - 2.0 * th, k);
        w.Dim(0, 0, 0, H, 2.6 * th, k);
        double yt = w.Extents.Y1 - 1.2 * th;
        w.Title(B / 2, yt, $"{c.Num}-{c.Num}", DrawUtil.ScaleText(SectionScale));
        return w;
    }
}
