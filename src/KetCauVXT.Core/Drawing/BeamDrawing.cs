using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Bản vẽ chi tiết dầm: mặt đứng tl 1/50 + mặt cắt 1-1 (gối), 2-2 (nhịp), 3-3... tl 1/25.
/// Toạ độ mặt đứng: x = 0 tại tim trục gối đầu tiên, y = 0 tại đỉnh dầm (mm).</summary>
public static class BeamDrawing
{
    /// <summary>Tỷ lệ mặc định của chương trình (tỷ lệ dùng thật: <see cref="CurrentScale"/> theo Thiết lập bản vẽ).</summary>
    public const double SheetScale = 50;
    public const double SectionScale = 25;

    /// <summary>Mẫu số tỷ lệ mặt đứng dầm theo Thiết lập bản vẽ (mặc định 50).</summary>
    public static double CurrentScale => DrawingStyle.Current.ScaleOf(DrawingKind.BeamElevation);
    /// <summary>Mẫu số tỷ lệ mặt cắt theo Thiết lập bản vẽ (mặc định 25).</summary>
    public static double CurrentSectionScale => DrawingStyle.Current.ScaleOf(DrawingKind.MemberSection);

    /// <param name="ds">Kết quả thiết kế (để lấy thép sàn thực tế vẽ trong cánh sàn); null → thép sàn quy ước Ø8a200.</param>
    public static List<Figure> BuildAll(Project p, IEnumerable<BeamLineDesign> beams, DesignSummary ds = null)
    {
        var res = new List<Figure>();
        foreach (var d in beams)
        {
            try { res.AddRange(Build(p, d, ds)); }
            catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure(d.Name, ex, CurrentScale), d.Name, true)); }
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
    public static List<Figure> Build(Project p, BeamLineDesign d, DesignSummary ds = null)
    {
        double S = CurrentScale;
        var w = new DxfWriter(S);
        double th = w.Th, tg = w.TagTh;
        var style = w.Style;
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

        // ---- bối cảnh sàn hai bên dầm (cánh sàn mặt cắt, dải sàn mặt đứng) – Drawing/BeamSlabContext.cs ----
        Func<double, BeamSlabSection> slabAt = null;
        if (style.ShowSlabFlanges && p != null)
        {
            var cache = new Dictionary<long, BeamSlabSection>();
            slabAt = x =>
            {
                long key = (long)Math.Round(x);
                if (!cache.TryGetValue(key, out var v))
                {
                    try { v = BeamSlabContext.At(p, d, x, ds); }
                    catch (Exception) { v = null; }   // mô hình thiếu nút/tiết diện: vẽ như dầm chữ nhật
                    cache[key] = v;
                }
                return v;
            };
        }

        // ---- bê tông: dầm giữa các mặt cột + cột cụt ----
        for (int i = 0; i + 1 < ns; i++)
        {
            double a = xs[i] + sw[i] / 2, e = xs[i + 1] - sw[i + 1] / 2;
            w.Line(Lay.BeTong, a, 0, e, 0);
            w.Line(Lay.BeTong, a, -h, e, -h);
        }
        // dải sàn liền khối: mép dưới sàn (và mặt sàn hạ cốt) nhìn thấy sau mặt cắt dọc dầm, giữa hai mặt cột
        var slabNotes = new List<string>();
        if (slabAt != null && spans.Count > 0)
        {
            for (int i = 0; i + 1 < ns; i++)
            {
                var sc = slabAt((xs[i] + xs[i + 1]) / 2);
                if (sc == null) continue;
                double a = xs[i] + sw[i] / 2, e = xs[i + 1] - sw[i + 1] / 2;
                if (e - a < 1) continue;
                foreach (var sd in new[] { sc.Left, sc.Right }.Where(x => x != null && x.Hs > 0)
                             .GroupBy(x => (Math.Round(x.Hs), Math.Round(x.Dz))).Select(g => g.First()))
                {
                    double yb = sd.Dz - sd.Hs;
                    if (yb > -h + 1) w.Line(Lay.BeTong, a, yb, e, yb);
                    if (sd.Dz < -1) w.Line(Lay.BeTong, a, sd.Dz, e, sd.Dz);
                }
                string sideTxt = sc.Shape == "T" ? "hai bên dầm" : sc.Left != null ? "bên trái dầm" : "bên phải dầm";
                if (sc.Shape != "R")
                {
                    var hsList = new[] { sc.Left, sc.Right }.Where(x => x != null).Select(x => DxfWriter.N0(x.Hs)).Distinct().ToList();
                    string n = $"Sàn liền khối {sideTxt} hs={string.Join("/", hsList)}";
                    if (!slabNotes.Contains(n)) slabNotes.Add(n);
                }
                string dropNote = SlabDropDrawing.ElevationNote(sc);   // [Sàn hạ cốt]
                if (dropNote != null && !slabNotes.Contains(dropNote)) slabNotes.Add(dropNote);
            }
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
        var cuts = ComputeCuts(d, xs, sw, h, slabAt);
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
            double tw = DxfWriter.TextWidth(zt[i].Text, tg) * w.WidthFactor + 2.6 * w.MarkR;
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
            double tw = DxfWriter.TextWidth(zt[i].Text, tg) * w.WidthFactor;
            double tot = tw + 2.6 * w.MarkR;
            double yt = yChain - (zRows[i] + 1) * 1.8 * th;
            double tx = cx - tot / 2;
            w.Text(Lay.Chu, tx, yt, tg, zt[i].Text, 0, 0, 0);
            if (z.Mark > 0) w.MarkCircle(tx + tw + 1.3 * w.MarkR, yt + 0.4 * tg, z.Mark);
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
        if (style.ShowElevationMarks)
        {
            w.ElevMark(xL - 2.5 * th, 0, DxfWriter.ElevText(d.TopElevation), -1);
            w.Line(Lay.KyHieu, xL - 2.5 * th, 0, xL - 0.3 * th, 0);
        }

        // ---- ghi chú sàn (chiều dày cánh sàn theo mô hình) ----
        double yNote = yBub - 3.2 * th;
        if (slabNotes.Count > 0)
        {
            string txt = string.Join("; ", slabNotes) + (slabNotes.Count == 1 ? " (mặt sàn trùng mặt dầm)" : "");
            w.Text(Lay.Chu, xs[0], yNote, th, txt, 0, 0, 0);
            yNote -= 1.8 * th;
        }

        // ---- tên hình ----
        double Lout = (xs[^1] - xs[0]) + sw[0] / 2 + sw[^1] / 2;
        string title = $"{d.Name} (SL={Math.Max(1, d.Quantity)}; L={DxfWriter.N0(Math.Round(Lout / 10) * 10)})";
        w.Title((xs[0] + xs[^1]) / 2, yChain + 6.5 * th, title, DrawUtil.ScaleText(S));
        if (d.IsCurved)
        {
            // dầm cong: mặt đứng là hình trải theo cung; chiều dài thép đã tính theo cung.
            // "chiều dài thép dọc tính theo cung; dùng đai kín móc 135°" chung cho mọi dầm cong → chuyển vào GHI CHÚ CHUNG (GeneralNotes)
            double yN = yNote;
            w.Text(Lay.Chu, xs[0], yN, th, CurvedDraw.Note(d.CurveRadius, d.ArcLength), 0, 0, 0);
            w.Text(Lay.Chu, xs[0], yN - 1.8 * th, 0.85 * th,
                $"Dây cung {DxfWriter.N0(d.Chord * 1000)}, độ vồng {DxfWriter.N0(d.Sagitta * 1000)}" +
                (style.ShowDesignValues ? $"; Tmax = {d.TorsionMax.ToString("0.0", DrawUtil.Inv)} kN.m" : ""), 0, 0, 0);
        }
        figs.Add(new Figure(w, d.Name, true));

        // ---- mặt cắt ----
        foreach (var c in cuts.GroupBy(x => x.Num).Select(g => g.First()).OrderBy(x => x.Num))
            figs.Add(new Figure(Section(c, b, h, cover, S, p), d.Name + " " + c.Num));
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
        /// <summary>Sàn hai bên dầm tại mặt cắt (null = tắt cánh sàn / không xác định)</summary>
        public BeamSlabSection Slab;
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

    private static List<Cut> ComputeCuts(BeamLineDesign d, List<double> xs, double[] sw, double h, Func<double, BeamSlabSection> slabAt = null)
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
            if (slabAt != null)
            {
                c.Slab = slabAt(x);
                if (c.Slab != null) c.Key += "|" + c.Slab.Key;
            }
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
        // khoảng hở như khi thiết kế (TCVN 5574:2018 mục 10.3.5): thép dưới ≥ max(d; 25), thép trên ≥ max(d; 30) mm
        double clr = Math.Max(top ? 30 : 25, dmax);
        int perRow = Math.Max(2, (int)Math.Floor((b - 2 * cover + clr) / (dmax + clr) + 1e-9));
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

    /// <summary>Cánh sàn một bên mặt cắt (mm thật, chưa nhân hệ số phóng).</summary>
    private sealed class FlangeSide
    {
        public SlabSideInfo Info;
        public double Len, Hs, Dz;
    }

    /// <summary>Đường bao mặt cắt dầm có cánh sàn (chữ T / L): nét bao hở tại đầu cánh, đầu cánh là đường cắt lửng.
    /// Toạ độ đã phóng (×k); gốc tại góc dưới trái dầm, trục y hướng lên, mặt trên dầm y = H.</summary>
    private static void FlangedOutline(DxfWriter w, double B, double H, double k, FlangeSide L, FlangeSide R, SlabBreakStyle brk)
    {
        var v = new List<(double X, double Y)>();
        var br = new List<bool>();
        void Add(double x, double y, bool breakNext = false)
        {
            if (v.Count > 0 && Math.Abs(v[^1].X - x) < 1e-6 && Math.Abs(v[^1].Y - y) < 1e-6) { br[^1] = breakNext; return; }
            v.Add((x, y)); br.Add(breakNext);
        }
        Add(0, 0);
        Add(B, 0);
        if (R != null)
        {
            double yt = H + R.Dz * k, yb = yt - R.Hs * k, x2 = B + R.Len * k;
            Add(B, yb); Add(x2, yb, true); Add(x2, yt); Add(B, yt);
        }
        Add(B, H);
        Add(0, H);
        if (L != null)
        {
            double yt = H + L.Dz * k, yb = yt - L.Hs * k, x1 = -L.Len * k;
            Add(0, yt); Add(x1, yt, true); Add(x1, yb); Add(0, yb);
        }
        if (v.Count > 1 && Math.Abs(v[^1].X - v[0].X) < 1e-6 && Math.Abs(v[^1].Y - v[0].Y) < 1e-6) { v.RemoveAt(v.Count - 1); br.RemoveAt(br.Count - 1); }
        w.HatchMat(HatchMaterial.Concrete, v);   // tô bê tông cả cánh sàn (nếu thiết lập bật – DxfWriter.Hatch.cs)
        int n = v.Count, first = br.IndexOf(true);
        if (first < 0) { w.Polyline(Lay.BeTong, v, true); return; }
        var chain = new List<(double X, double Y)> { v[(first + 1) % n] };
        for (int s = 1; s <= n; s++)
        {
            int j = (first + s) % n, jn = (j + 1) % n;
            if (br[j])
            {
                if (chain.Count > 1) w.Polyline(Lay.BeTong, chain, false);
                BreakSymbol(w, v[j].X, v[j].Y, v[jn].Y, brk);
                chain = new List<(double X, double Y)> { v[jn] };
            }
            else chain.Add(v[jn]);
        }
        if (chain.Count > 1) w.Polyline(Lay.BeTong, chain, false);
    }

    /// <summary>Đường cắt đầu cánh sàn (đứng tại x, từ y1 tới y2) – kích thước theo chiều dày cánh, không theo cỡ chữ.</summary>
    private static void BreakSymbol(DxfWriter w, double x, double y1, double y2, SlabBreakStyle brk)
    {
        double lo = Math.Min(y1, y2), hi = Math.Max(y1, y2), q = hi - lo, ym = (lo + hi) / 2;
        if (brk == SlabBreakStyle.Straight || q < 1e-6) { w.Line(Lay.BeTong, x, lo, x, hi); return; }
        w.Polyline(Lay.BeTong, new List<(double X, double Y)>
        {
            (x, lo - 0.25 * q), (x, ym - 0.2 * q), (x + 0.25 * q, ym - 0.07 * q),
            (x - 0.25 * q, ym + 0.07 * q), (x, ym + 0.2 * q), (x, hi + 0.25 * q)
        }, false);
    }

    /// <summary>Thép sàn trong cánh: lớp dưới vuông góc dầm (đường, neo vào dầm), lớp dưới song song dầm (chấm),
    /// thép mũ vắt qua dầm (đường gần mặt trên; dầm biên: bẻ móc xuống trong dầm) và thép phân bố dưới mũ (chấm).
    /// Nhãn nhẹ không số hiệu "Ø8a200 (thép sàn)" – số hiệu thép sàn thuộc bản vẽ sàn. Thép quy ước (chưa có kết quả thiết kế sàn): nét đứt.</summary>
    private static void SlabBars(DxfWriter w, TagLayout layout, double B, double H, double k, double cover, FlangeSide fL, FlangeSide fR, Project p, double th)
    {
        double cs = Math.Max(10, (p?.Concrete?.CoverSlab ?? 0.015) * 1000);
        double csb = Math.Max(0.5 * cover, cover - 3) * k;   // mép trong đai dầm
        double rMin = 0.15 * th;
        var tagged = new HashSet<string>();
        // ---- thép mũ: vắt liên tục qua dầm khi hai cánh cùng cao độ ----
        bool cont = fL != null && fR != null && Math.Abs(fL.Dz - fR.Dz) < 0.5 && fL.Info.TopDia == fR.Info.TopDia;
        double TopY(FlangeSide f) => H + (f.Dz - cs - f.Info.TopDia / 2.0) * k;
        if (cont)
        {
            double y = Math.Max(TopY(fL), TopY(fR));
            string lt = fL.Info.TopFromDesign || fR.Info.TopFromDesign ? null : "DASHED";
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (-fL.Len * k + 0.2 * fL.Hs * k, y), (B + fR.Len * k - 0.2 * fR.Hs * k, y) }, false, 0, lt);
        }
        void Side(FlangeSide f, int sg)
        {
            if (f == null) return;
            var si = f.Info;
            double hs = f.Hs, yt = H + f.Dz * k, yb = yt - hs * k;
            double xFace = sg < 0 ? 0 : B, xEnd = sg < 0 ? -f.Len * k : B + f.Len * k;
            double inset = 0.2 * hs * k;                          // thép dừng trước đường cắt
            double xIn = xEnd - sg * inset;
            string ltB = si.BotFromDesign ? null : "DASHED", ltT = si.TopFromDesign ? null : "DASHED";
            // lớp dưới vuông góc dầm: neo vào dầm ≥ 10d (≤ ½ bề rộng dầm)
            double yB1 = yb + (cs + si.BotPerpDia / 2.0) * k;
            double anc = Math.Min(B / 2, Math.Max(10 * si.BotPerpDia, 100) * k);
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xIn, yB1), (xFace - sg * anc, yB1) }, false, 0, ltB);
            // lớp dưới song song dầm: chấm theo bước
            double yB2 = yb + (cs + si.BotPerpDia + si.BotParDia / 2.0) * k;
            double sB = Math.Max(50, si.BotParSpacing);
            var dotsB = new List<double>();
            for (double t = sB / 2; t < f.Len - 0.2 * hs; t += sB) dotsB.Add(xFace + sg * t * k);
            if (dotsB.Count == 0) dotsB.Add((xFace + xIn) / 2);
            foreach (double x in dotsB) w.Dot(Lay.Thep, x, yB2, Math.Max(si.BotParDia / 2.0 * k, rMin));
            // thép mũ (không liên tục): vào hết dầm tới mép trong đai phía kia rồi bẻ móc xuống
            double yT1 = TopY(f);
            if (!cont)
            {
                double xFar = sg < 0 ? B - csb : csb;
                double hook = Math.Min(H - 2 * csb, Math.Max(hs, 10 * si.TopDia) * k);
                w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xIn, yT1), (xFar, yT1), (xFar, yT1 - Math.Max(0, hook)) }, false, 0, ltT);
            }
            // thép phân bố dưới mũ Ø6a250 (chấm)
            double yT2 = yT1 - (si.TopDia / 2.0 + 3) * k;
            for (double t = 125; t < f.Len - 0.2 * hs; t += 250) w.Dot(Lay.Thep, xFace + sg * t * k, yT2, Math.Max(3 * k, rMin));
            // ---- nhãn nhẹ ----
            double xm = (xFace + xEnd) / 2;
            string tb = si.BotPerpText + " (thép sàn)";
            if (tagged.Add("B|" + tb))
            {
                double ky = yb - 1.6 * th;
                var box = w.TagBox(xm, ky, tb, 0, sg);
                var (_, dy) = layout.Place(box, 0, -1.8 * th);
                w.LeaderTag(xm, yB1, xm, ky + dy, tb, 0, sg);
            }
            string tt = si.TopText + " (thép sàn)";
            if (tagged.Add("T|" + tt))
            {
                double ky = H + 1.6 * th;
                var box = w.TagBox(xm, ky, tt, 0, sg);
                var (_, dy) = layout.Place(box, 0, 1.8 * th);
                w.LeaderTag(xm, yT1, xm, ky + dy, tt, 0, sg);
            }
        }
        Side(fL, -1);
        Side(fR, 1);
    }

    private static DxfWriter Section(Cut c, double b, double h, double cover, double S, Project p = null)
    {
        var w = new DxfWriter(S);
        double secScale = CurrentSectionScale;
        double th = w.Th, k = S / secScale;
        double B = b * k, H = h * k;
        var style = w.Style;
        // ---- cánh sàn (kiểu Kết cấu Soft / XSCad): T – sàn hai bên, L – dầm biên, chữ nhật – không có sàn ----
        FlangeSide fL = null, fR = null;
        if (style.ShowSlabFlanges && c.Slab != null)
        {
            FlangeSide Mk(SlabSideInfo si)
            {
                if (si == null || si.Hs <= 0) return null;
                double hs = Math.Min(si.Hs, Math.Max(20, h - 20));
                double dz = Math.Clamp(si.Dz, -(h - hs), 0);
                return new FlangeSide { Info = si, Len = style.SlabFlangeLength, Hs = hs, Dz = dz };
            }
            fL = Mk(c.Slab.Left); fR = Mk(c.Slab.Right);
        }
        bool flanged = fL != null || fR != null;
        if (!flanged) { w.HatchMatRect(HatchMaterial.Concrete, 0, 0, B, H); w.Rect(Lay.BeTong, 0, 0, B, H); }   // tô bê tông (DxfWriter.Hatch.cs)
        else FlangedOutline(w, B, H, k, fL, fR, style.SlabBreak);
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
        if (fL != null) layout.Reserve(new Rect2(-fL.Len * k, H + (fL.Dz - fL.Hs) * k, 0, H + fL.Dz * k));
        if (fR != null) layout.Reserve(new Rect2(B, H + (fR.Dz - fR.Hs) * k, B + fR.Len * k, H + fR.Dz * k));
        if (flanged && style.ShowSlabRebarInFlange) SlabBars(w, layout, B, H, k, cover, fL, fR, p, th);
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
                var (_, dy) = layout.Place(box, 0, -1.8 * th);   // cánh phải đã giữ chỗ → nhãn dịch xuống dưới cánh
                w.LeaderTag(px, py, B + 2 * th, py - 1.5 * th + dy, txt, sb.Mark, 1);
            }
        }
        // nhãn đai sang phải
        if (c.Zone != null)
        {
            string txt = DrawUtil.StirrupTag(c.Zone.Dia, c.Zone.Spacing);
            double py = H * 0.6;
            var box = w.TagBox(B + 2 * th, py, txt, c.Zone.Mark, 1);
            var (_, dy) = layout.Place(box, 0, fR != null ? -1.8 * th : 1.8 * th);   // có cánh phải: dịch xuống, không vắt qua cánh
            w.LeaderTag(B - cs, py, B + 2 * th, py + dy, txt, c.Zone.Mark, 1);
        }
        double yMin = Math.Min(0, layout.Occupied.Min(r => r.Y1));
        w.Dim(0, 0, B, 0, yMin - 2.0 * th, k);
        if (fL == null) w.Dim(0, 0, 0, H, 2.6 * th, k);
        else
        {
            // cánh trái: hs đo tại đầu cánh, h (từ mặt trên sàn/dầm) đặt ngoài hs
            double x0 = -fL.Len * k;
            w.Dim(x0, H + (fL.Dz - fL.Hs) * k, x0, H + fL.Dz * k, 1.4 * th, k, "hs=" + DxfWriter.N0(fL.Hs));
            // kích thước h đặt ngoài các nhãn thép sàn dưới cánh trái (không cắt qua chữ)
            double xOcc = x0 - 1.4 * th;
            foreach (var r in layout.Occupied) if (r.Y2 > 0 && r.Y1 < H && r.X2 < 0) xOcc = Math.Min(xOcc, r.X1);
            w.Dim(x0, 0, x0, H, (x0 - xOcc) + 2.4 * th, k);
        }
        if (fR != null && (fL == null || Math.Abs(fR.Hs - fL.Hs) > 0.5 || Math.Abs(fR.Dz - fL.Dz) > 0.5))
        {
            double x1 = B + fR.Len * k;
            w.Dim(x1, H + (fR.Dz - fR.Hs) * k, x1, H + fR.Dz * k, -1.4 * th, k, "hs=" + DxfWriter.N0(fR.Hs));
        }
        if (flanged) SlabDropDrawing.SectionNote(w, layout, B, H, k, fL?.Dz, fR?.Dz);   // [Sàn hạ cốt] kích thước bậc Δ + ghi chú
        // dầm lệch trục (điểm chèn / dời ngang): đường trục định vị và khoảng cách mép dầm – trục
        if (c.Slab != null && Math.Abs(c.Slab.Offset) > 1)
        {
            double xa = B / 2 + c.Slab.Offset * k;
            w.Line(Lay.Truc, xa, -0.8 * th, xa, H + 0.8 * th, "CENTER");
            double xf = Math.Abs(xa) <= Math.Abs(xa - B) ? 0 : B;
            if (Math.Abs(xa - xf) > 1e-6) w.Dim(Math.Min(xf, xa), 0, Math.Max(xf, xa), 0, yMin - 4.6 * th, k);
        }
        double yt = w.Extents.Y1 - 1.2 * th;
        w.Title(B / 2, yt, $"{c.Num}-{c.Num}", DrawUtil.ScaleText(secScale), true);
        return w;
    }
}
