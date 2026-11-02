using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>[MóngLệchTâm] Bản vẽ móng lệch tâm: ranh giới đất trên mặt bằng móng (nét đứt + gạch chéo phía ngoài lô đất, chữ "RANH GIỚI ĐẤT"),
/// móng đơn / móng kết hợp lệch tâm (kích thước từ tim cột tới mép móng), dầm móng cân bằng DM (b×h) trên mặt bằng,
/// chi tiết móng kết hợp và chi tiết dầm cân bằng (mặt đứng + mặt cắt, nhãn thép kiểu Kết cấu Soft "3Ø20 ①", "Ø8a150 ④").</summary>
public static class EccentricDrawing
{
    public const string BoundaryLabel = "RANH GIỚI ĐẤT";

    private static (double X, double Y) P(double x, double y) => (x, y);

    // ------------------------------------------------------------------
    // MẶT BẰNG
    // ------------------------------------------------------------------
    /// <summary>Móng đơn lệch tâm trên mặt bằng móng (mọi vị trí gộp): đáy móng lệch, cột, kích thước tim cột – mép móng theo phương lệch tâm.</summary>
    public static void FootingPlan(DxfWriter w, FootingDesign f, double k, ref Rect2 box)
    {
        var e = f.Eccentric;
        if (e == null) return;
        double th = w.Th;
        var locs = e.Locations.Count > 0 ? e.Locations : new List<EccentricLocation> { new() { NodeId = f.NodeId, X = f.X, Y = f.Y, OffsetX = e.OffsetX, OffsetY = e.OffsetY } };
        double sx = (f.ColumnH > 0 ? f.ColumnH : 0.22) * 1000 * k, sy = (f.ColumnB > 0 ? f.ColumnB : 0.22) * 1000 * k;
        foreach (var loc in locs)
        {
            double x = loc.X * 1000 * k, y = loc.Y * 1000 * k;
            Rect2 r;
            if (e.Combined != null)
            {
                var pts = e.Combined.Outline.Select(q => P(q.X * 1000 * k, q.Y * 1000 * k)).ToList();
                w.Polyline(Lay.BeTong, pts, true);
                r = new Rect2(pts.Min(q => q.X), pts.Min(q => q.Y), pts.Max(q => q.X), pts.Max(q => q.Y));
                Column(w, e.Combined.XB * 1000 * k, e.Combined.YB * 1000 * k, sx, sy);
            }
            else
            {
                double cx = x + loc.OffsetX * 1000 * k, cy = y + loc.OffsetY * 1000 * k, hl = f.L * 500 * k, hb = f.B * 500 * k;
                w.Rect(Lay.BeTong, cx - hl, cy - hb, cx + hl, cy + hb);
                r = new Rect2(cx - hl, cy - hb, cx + hl, cy + hb);
            }
            Column(w, x, y, sx, sy);
            // kích thước tim cột – mép móng theo phương lệch tâm
            if (Math.Abs(loc.OffsetX) > 1e-6 || e.Combined?.AlongX == true)
                w.DimChainH(new List<double> { r.X1, x, r.X2 }, r.Y1, r.Y1 - 1.6 * th, k);
            if (Math.Abs(loc.OffsetY) > 1e-6 || e.Combined?.AlongX == false)
                w.DimChainV(new List<double> { r.Y1, y, r.Y2 }, r.X1, r.X1 - 1.6 * th, k);
            w.Text(Lay.Chu, r.X1 + 0.3 * th, r.Y2 + 0.4 * th, th, f.Name + (e.Combined != null ? " (MÓNG KẾT HỢP)" : " (LỆCH TÂM)"), 0, 0, 0);
            box = box.Union(r.Inflate(2.5 * th));
        }
    }

    private static void Column(DxfWriter w, double cx, double cy, double sx, double sy)
    {
        double a = Math.Max(sx, 1) / 2, b = Math.Max(sy, 1) / 2;
        w.Rect(Lay.BeTong, cx - a, cy - b, cx + a, cy + b);
        w.Solid(Lay.BeTong, cx - a, cy - b, cx + a, cy - b, cx - a, cy + b, cx + a, cy + b);
    }

    /// <summary>Ranh giới đất + dầm móng cân bằng trên mặt bằng móng (sau khi đã vẽ móng; box mở rộng tới ranh giới).</summary>
    public static void AddToLayout(DxfWriter w, Project p, DesignSummary s, double k, ref Rect2 box)
    {
        if (w == null || p == null) return;
        double th = w.Th;
        // dầm cân bằng
        if (s?.StrapBeams != null)
            foreach (var sb in s.StrapBeams)
                foreach (var l in sb.Links)
                {
                    double dir = l.AlongX ? Math.Sign(l.XB - l.XA) : Math.Sign(l.YB - l.YA);
                    if (dir == 0) dir = 1;
                    double hb = sb.B * 500 * k;
                    string tag = $"{sb.Name} ({DxfWriter.N0(sb.B * 1000)}×{DxfWriter.N0(sb.H * 1000)})";
                    if (l.AlongX)
                    {
                        double x1 = (l.XA + dir * l.FaceA) * 1000 * k, x2 = (l.XA + dir * l.FaceB) * 1000 * k, y = l.YA * 1000 * k;
                        w.Line(Lay.BeTong, x1, y - hb, x2, y - hb);
                        w.Line(Lay.BeTong, x1, y + hb, x2, y + hb);
                        w.Text(Lay.Chu, (x1 + x2) / 2, y + hb + 0.3 * th, 0.9 * th, tag, 0, 1, 0);
                        box = box.Union(new Rect2(Math.Min(x1, x2), y - hb, Math.Max(x1, x2), y + hb + 1.4 * th));
                    }
                    else
                    {
                        double y1 = (l.YA + dir * l.FaceA) * 1000 * k, y2 = (l.YA + dir * l.FaceB) * 1000 * k, x = l.XA * 1000 * k;
                        w.Line(Lay.BeTong, x - hb, y1, x - hb, y2);
                        w.Line(Lay.BeTong, x + hb, y1, x + hb, y2);
                        w.Text(Lay.Chu, x - hb - 0.3 * th, (y1 + y2) / 2, 0.9 * th, tag, 90, 1, 0);
                        box = box.Union(new Rect2(x - hb - 1.4 * th, Math.Min(y1, y2), x + hb, Math.Max(y1, y2)));
                    }
                }
        // ranh giới đất
        var lines = EccentricFooting.Lines(p);
        if (lines.Count == 0) return;
        var b0 = box.IsEmpty ? new Rect2(0, 0, 10000 * k, 10000 * k) : box;
        foreach (var (b, c) in lines)
        {
            if (b.IsVertical) b0 = b0.Union(c * 1000 * k, b0.Cy);
            else b0 = b0.Union(b0.Cx, c * 1000 * k);
        }
        double ext = 3 * th, tick = 0.8 * th, step = 1.6 * th;
        foreach (var (b, c) in lines)
        {
            double v = c * 1000 * k, o = -b.Inward;   // o: hướng ra ngoài lô đất
            if (b.IsVertical)
            {
                double y1 = b0.Y1 - ext, y2 = b0.Y2 + ext;
                w.Line(Lay.KyHieu, v, y1, v, y2, "DASHED");
                for (double y = y1; y <= y2 - tick; y += step) w.Line(Lay.KyHieu, v, y, v + o * tick, y + tick);
                w.Text(Lay.Chu, v + o * (tick + 0.5 * th), (y1 + y2) / 2, th, BoundaryLabel, 90, 1, o > 0 ? 2 : 0);
                box = box.Union(new Rect2(v, y1, v + o * (tick + 2 * th), y2));
            }
            else
            {
                double x1 = b0.X1 - ext, x2 = b0.X2 + ext;
                w.Line(Lay.KyHieu, x1, v, x2, v, "DASHED");
                for (double x = x1; x <= x2 - tick; x += step) w.Line(Lay.KyHieu, x, v, x + tick, v + o * tick);
                w.Text(Lay.Chu, (x1 + x2) / 2, v + o * (tick + 0.5 * th), th, BoundaryLabel, 0, 1, o > 0 ? 0 : 2);
                box = box.Union(new Rect2(x1, v, x2, v + o * (tick + 2 * th)));
            }
        }
    }

    // ------------------------------------------------------------------
    // CHI TIẾT
    // ------------------------------------------------------------------
    private static double Det => DrawingStyle.Current.ScaleOf(DrawingKind.FootingDetail);

    private static BarMark Bar(MemberDesignBase m, string role) => m.Bars.FirstOrDefault(b => b.Role == role);

    /// <summary>Chi tiết móng kết hợp (mặt bằng + mặt cắt dọc) và dầm cân bằng (mặt đứng + mặt cắt).</summary>
    public static List<Figure> BuildDetails(Project p, DesignSummary s, double S)
    {
        var res = new List<Figure>();
        if (s == null) return res;
        bool first = true;
        foreach (var sb in s.StrapBeams ?? new List<StrapBeamDesign>())
        {
            try
            {
                res.Add(new Figure(StrapElevation(p, sb, S), "DMCB " + sb.Name, first));
                res.Add(new Figure(StrapSection(p, sb, S), "DMCB " + sb.Name + " mc"));
            }
            catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure(sb.Name, ex, S), sb.Name, true)); }
            first = false;
        }
        return res;
    }

    /// <summary>Mặt bằng móng kết hợp (hình chữ nhật / hình thang): 2 cột, lưới thép đáy, thép lớp trên (nét đứt), kích thước tim cột – mép móng.</summary>
    public static DxfWriter CombinedPlan(Project p, FootingDesign f, double S)
    {
        var c = f.Eccentric.Combined;
        var w = new DxfWriter(S);
        double det = Det, th = w.Th, k = S / det;
        double L = c.Length * 1000 * k, b1 = c.B1 * 1000 * k, b2 = c.B2 * 1000 * k, bm = Math.Max(b1, b2);
        double t1 = (c.AlongX ? f.X - c.Start : f.Y - c.Start) * c.Dir * 1000 * k, t2 = (c.AlongX ? c.XB - c.Start : c.YB - c.Start) * c.Dir * 1000 * k;
        w.Polyline(Lay.BeTong, new List<(double X, double Y)> { P(0, -b1 / 2), P(L, -b2 / 2), P(L, b2 / 2), P(0, b1 / 2) }, true);
        double ca = (c.AlongX ? f.ColumnH : f.ColumnB) * 1000 * k, cw = (c.AlongX ? f.ColumnB : f.ColumnH) * 1000 * k;
        foreach (double t in new[] { t1, t2 })
        {
            w.Rect(Lay.BeTong, t - ca / 2, -cw / 2, t + ca / 2, cw / 2);
            w.Line(Lay.Truc, t, -bm / 2 - 3 * th, t, bm / 2 + 3 * th, "CENTER");
        }
        w.Line(Lay.Truc, -3 * th, 0, L + 3 * th, 0, "CENTER");
        double cv = Math.Max(30, p.Concrete.CoverFooting * 1000) * k;
        var bl = Bar(f, "Lớp dưới phương L"); var bb = Bar(f, "Lớp dưới phương B");
        var top = f.Bars.FirstOrDefault(x => x.IsTop);
        var lon = c.AlongX ? bl : bb; var tra = c.AlongX ? bb : bl;
        double hb = Math.Min(b1, b2) / 2 - cv;
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(cv, -hb * 0.4), P(L - cv, -hb * 0.4) }, false, 0.4 * S);
        if (top != null) w.Line(Lay.Thep, cv, hb * 0.4, L - cv, hb * 0.4, "DASHED");
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(L * 0.5, -hb), P(L * 0.5, hb) }, false, 0.4 * S);
        if (lon != null) w.LeaderTag(L * 0.2, -hb * 0.4, L * 0.2 + 2 * th, -bm / 2 - 3.5 * th, DrawUtil.StirrupTag(lon.Dia, lon.Spacing), lon.Mark, 1);
        if (top != null) w.LeaderTag(L * 0.35, hb * 0.4, L * 0.35 + 2 * th, bm / 2 + 2.5 * th, DrawUtil.StirrupTag(top.Dia, top.Spacing), top.Mark, 1);
        if (tra != null) w.LeaderTag(L * 0.5, hb * 0.7, L + 3 * th, bm / 2 + 2.5 * th, DrawUtil.StirrupTag(tra.Dia, tra.Spacing), tra.Mark, 1);
        w.DimChainH(new List<double> { 0, t1, t2, L }, -bm / 2, -bm / 2 - 6.5 * th, k);
        w.Dim(0, -bm / 2, L, -bm / 2, -9 * th, k);
        w.Dim(0, -b1 / 2, 0, b1 / 2, 3 * th, k);
        if (Math.Abs(b1 - b2) > 1) w.Dim(L, -b2 / 2, L, b2 / 2, -3 * th, k);
        w.Text(Lay.Chu, -1.2 * th, bm / 2 + 1.0 * th, 0.85 * th, BoundaryLabel, 0, 2, 0);
        w.Line(Lay.KyHieu, 0, -bm / 2 - 2 * th, 0, bm / 2 + 2 * th, "DASHED");
        w.Title(L / 2, w.Extents.Y2 + 4 * th, $"{f.Name} – MÓNG KẾT HỢP (SL={Math.Max(1, f.Quantity)})", DrawUtil.ScaleText(det));
        return w;
    }

    /// <summary>Mặt cắt dọc móng kết hợp: đế móng, 2 cổ cột, thép lớp dưới + lớp trên.</summary>
    public static DxfWriter CombinedSection(Project p, FootingDesign f, double S)
    {
        var c = f.Eccentric.Combined;
        var w = new DxfWriter(S);
        double det = Det, th = w.Th, k = S / det;
        double L = c.Length * 1000 * k, H = Math.Max(0.3, f.H) * 1000 * k, lot = 100 * k, neck = 500 * k;
        double t1 = (c.AlongX ? f.X - c.Start : f.Y - c.Start) * c.Dir * 1000 * k, t2 = (c.AlongX ? c.XB - c.Start : c.YB - c.Start) * c.Dir * 1000 * k;
        double ca = (c.AlongX ? f.ColumnH : f.ColumnB) * 1000 * k;
        double cv = Math.Max(30, p.Concrete.CoverFooting * 1000) * k;
        w.Rect(Lay.BeTong, 0, -lot, L + lot, 0);
        w.Rect(Lay.BeTong, 0, 0, L, H);
        foreach (double t in new[] { t1, t2 })
        {
            w.Line(Lay.BeTong, t - ca / 2, H, t - ca / 2, H + neck);
            w.Line(Lay.BeTong, t + ca / 2, H, t + ca / 2, H + neck);
            w.BreakH(Lay.BeTong, t - ca / 2, t + ca / 2, H + neck);
            w.Line(Lay.Truc, t, -lot - 2 * th, t, H + neck + 2 * th, "CENTER");
        }
        w.Line(Lay.KyHieu, 0, -lot - 3 * th, 0, H + neck + 3 * th, "DASHED");
        w.Text(Lay.Chu, -0.5 * th, H + neck + 2 * th, 0.85 * th, BoundaryLabel, 0, 2, 0);
        var bl = Bar(f, c.AlongX ? "Lớp dưới phương L" : "Lớp dưới phương B");
        var top = f.Bars.FirstOrDefault(x => x.IsTop);
        double yb = cv + 10 * k, yt = H - cv - 10 * k, leg = Math.Min(H / 3, 200 * k);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(cv, yb + leg), P(cv, yb), P(L - cv, yb), P(L - cv, yb + leg) }, false, 0.4 * S);
        if (top != null) w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(cv, yt - leg), P(cv, yt), P(L - cv, yt), P(L - cv, yt - leg) }, false, 0.4 * S);
        if (bl != null) w.LeaderTag((t1 + t2) / 2, yb, (t1 + t2) / 2 + 2 * th, -lot - 3 * th, DrawUtil.StirrupTag(bl.Dia, bl.Spacing), bl.Mark, 1);
        if (top != null) w.LeaderTag((t1 + t2) / 2, yt, (t1 + t2) / 2 + 2 * th, H + neck + 2.5 * th, DrawUtil.StirrupTag(top.Dia, top.Spacing), top.Mark, 1);
        w.DimChainH(new List<double> { 0, t1, t2, L }, -lot, -lot - 6 * th, k);
        w.Dim(0, 0, 0, H, 2.5 * th, k);
        w.ElevMark(L + 3 * th, H, DxfWriter.ElevText(p.BaseElevation), 1);
        w.ElevMark(L + 3 * th, 0, DxfWriter.ElevText(p.BaseElevation - f.H), 1);
        w.Title(L / 2, w.Extents.Y1 - 1.2 * th, $"MẶT CẮT DỌC {f.Name} (MÓNG KẾT HỢP)", DrawUtil.ScaleText(det), true);
        return w;
    }

    /// <summary>Mặt đứng dầm móng cân bằng: dầm từ mép móng biên tới mép móng cột trong, 2 cổ cột, thép trên (chịu M = N1·e) / dưới / đai, kích thước.</summary>
    public static DxfWriter StrapElevation(Project p, StrapBeamDesign sb, double S)
    {
        var w = new DxfWriter(S);
        double baseSc = DrawingStyle.Current.ScaleOf(DrawingKind.BeamElevation);
        double th = w.Th, k = S / baseSc;
        var l = sb.Links[0];
        double span = sb.Span * 1000 * k, h = sb.H * 1000 * k, fa = l.FaceA * 1000 * k, fb = l.FaceB * 1000 * k;
        double ext = 600 * k, neck = 500 * k, ca = Math.Max(l.ColAlong, 0.2) * 1000 * k;
        double cv = Math.Max(25, p.Concrete.CoverBeam * 1000) * k;
        double x0 = Math.Min(fa - ext, -ca / 2 - 100 * k), x1 = Math.Max(fb + ext, span + ca / 2 + 100 * k);
        // móng 2 đầu (phác, nét đứt) + dầm
        w.Rect(Lay.BeTong, x0, -h, fa, 0, "DASHED");
        w.Rect(Lay.BeTong, fb, -h, x1, 0, "DASHED");
        w.Rect(Lay.BeTong, fa, -h, fb, 0);
        w.Text(Lay.Chu, (x0 + fa) / 2, -h / 2, 0.8 * th, (sb.PileCap ? "ĐÀI" : "MÓNG") + " BIÊN", 0, 1, 2);
        w.Text(Lay.Chu, (fb + x1) / 2, -h / 2, 0.8 * th, sb.E2 > 0 ? (sb.PileCap ? "ĐÀI" : "MÓNG") + " BIÊN" : (sb.PileCap ? "ĐÀI" : "MÓNG") + " CỘT TRONG", 0, 1, 2);
        // lớp đệm dưới đáy dầm (không tựa nền)
        w.Line(Lay.KyHieu, fa, -h - 80 * k, fb, -h - 80 * k, "DASHED");
        w.Text(Lay.Chu, (fa + fb) / 2, -h - 80 * k - 1.2 * th, 0.75 * th, "đệm xốp / cát rời 50÷100 – đáy dầm không tựa nền", 0, 1, 0);
        foreach (double x in new[] { 0.0, span })
        {
            w.Line(Lay.BeTong, x - ca / 2, 0, x - ca / 2, neck);
            w.Line(Lay.BeTong, x + ca / 2, 0, x + ca / 2, neck);
            w.BreakH(Lay.BeTong, x - ca / 2, x + ca / 2, neck);
            w.Line(Lay.Truc, x, -h - 2 * th, x, neck + 2 * th, "CENTER");
        }
        w.BreakV(Lay.BeTong, x0, -h, 0);
        w.BreakV(Lay.BeTong, x1, -h, 0);
        var top = sb.Bars.Where(b => b.Role == "Trên").ToList();
        var bot = sb.Bars.Where(b => b.Role == "Dưới").ToList();
        var stir = sb.Bars.FirstOrDefault(b => b.Role == "Đai");
        double yt = -cv - 10 * k, yb = -h + cv + 10 * k, leg = Math.Min(h / 2, 250 * k);
        double ta = Math.Max(x0 + cv, -ca / 2 - 200 * k), tb = Math.Min(x1 - cv, span + ca / 2 + 200 * k);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(ta, yt - leg), P(ta, yt), P(tb, yt), P(tb, yt - leg) }, false, 0.4 * S);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(fa - 600 * k, yb), P(fb + 600 * k, yb) }, false, 0.4 * S);
        if (stir != null && stir.Spacing > 0)
        {
            double ss = stir.Spacing * 1000 * k;
            int j = 0;
            for (double x = fa + 50 * k; x < fb - 10 * k; x += ss, j++) if (j % 2 == 0) w.Line(Lay.Dai, x, -h + cv, x, -cv);
        }
        double tagT = neck + 2.5 * th;
        int n = 0;
        foreach (var b in top) { double x = fa + (fb - fa) * (0.15 + 0.15 * n); w.LeaderTag(x, yt, x + 2 * th, tagT + 2.7 * th * n, DrawUtil.BarTag(b.Count, b.Dia), b.Mark, 1); n++; }
        n = 0;
        foreach (var b in bot) { double x = fa + (fb - fa) * (0.55 + 0.15 * n); w.LeaderTag(x, yb, x + 2 * th, -h - 3.5 * th - 2.7 * th * n, DrawUtil.BarTag(b.Count, b.Dia), b.Mark, 1); n++; }
        if (stir != null) w.LeaderTag(fa + (fb - fa) * 0.4, -h / 2, fa + (fb - fa) * 0.4 + 3 * th, tagT + 2.7 * th * Math.Max(top.Count, 1), DrawUtil.StirrupTag(stir.Dia, stir.Spacing), stir.Mark, 1);
        double yd = -h - 3.5 * th - 2.7 * th * Math.Max(bot.Count, 1) - 2 * th;
        w.DimChainH(new List<double> { 0, fa, fb, span }, -h, yd, k);
        w.Dim(0, -h, span, -h, yd - 2.5 * th + h, k);
        w.Dim(x1, -h, x1, 0, -2.5 * th, k);
        if (w.Style.ShowDesignValues)
            w.Text(Lay.Chu, span / 2, yd - 5 * th, 0.85 * th, $"M = N1·e1 = {sb.M:0.0} kN.m;  Q = R1 − N1 = {sb.V:0.0} kN;  e1 = {sb.E1 * 1000:0} mm", 0, 1, 0);
        w.Title(span / 2, w.Extents.Y1 - 1.2 * th, $"DẦM MÓNG CÂN BẰNG {sb.Name} {DxfWriter.N0(sb.B * 1000)}×{DxfWriter.N0(sb.H * 1000)} (SL={Math.Max(1, sb.Quantity)})", DrawUtil.ScaleText(baseSc), true);
        return w;
    }

    /// <summary>Mặt cắt dầm cân bằng b×h: đai, thép trên / dưới / giá, nhãn thép.</summary>
    public static DxfWriter StrapSection(Project p, StrapBeamDesign sb, double S)
    {
        var w = new DxfWriter(S);
        double det = DrawingStyle.Current.ScaleOf(DrawingKind.MemberSection), th = w.Th, k = S / det;
        double b = sb.B * 1000 * k, h = sb.H * 1000 * k, cv = Math.Max(25, p.Concrete.CoverBeam * 1000) * k;
        w.Rect(Lay.BeTong, -b / 2, 0, b / 2, h);
        var stir = sb.Bars.FirstOrDefault(x => x.Role == "Đai");
        double ds = (stir?.Dia ?? 8) * k;
        w.Rect(Lay.Dai, -b / 2 + cv, cv, b / 2 - cv, h - cv);
        void Row(List<BarMark> set, double y)
        {
            int tot = set.Sum(q => q.Count);
            if (tot <= 0) return;
            int dm = set.Max(q => q.Dia);
            double xa = -b / 2 + cv + ds + dm * k / 2, xb = b / 2 - cv - ds - dm * k / 2;
            int i = 0;
            foreach (var q in set) for (int j = 0; j < q.Count; j++, i++) w.Dot(Lay.Thep, tot == 1 ? 0 : xa + (xb - xa) * i / (tot - 1), y, Math.Max(q.Dia * k / 2, 0.15 * th));
        }
        var top = sb.Bars.Where(x => x.Role == "Trên").ToList();
        var bot = sb.Bars.Where(x => x.Role == "Dưới").ToList();
        var gia = sb.Bars.FirstOrDefault(x => x.Role == "Giá");
        double ytop = h - cv - ds - (top.Count > 0 ? top.Max(q => q.Dia) : 16) * k / 2, ybot = cv + ds + (bot.Count > 0 ? bot.Max(q => q.Dia) : 16) * k / 2;
        Row(top, ytop); Row(bot, ybot);
        if (gia != null)
            for (int i = 1; i <= gia.Count / 2; i++)
            {
                double y = cv + (h - 2 * cv) * i / (gia.Count / 2 + 1);
                w.Dot(Lay.Thep, -b / 2 + cv + ds + 6 * k, y, Math.Max(6 * k, 0.15 * th));
                w.Dot(Lay.Thep, b / 2 - cv - ds - 6 * k, y, Math.Max(6 * k, 0.15 * th));
            }
        double right = b / 2 + 3 * th;
        int nt = 0;
        foreach (var q in top) { w.LeaderTag(b / 2 - cv - ds, ytop, right, h + 1.5 * th + 2.7 * th * nt, DrawUtil.BarTag(q.Count, q.Dia), q.Mark, 1); nt++; }
        int nb = 0;
        foreach (var q in bot) { w.LeaderTag(b / 2 - cv - ds, ybot, right, -2.5 * th - 2.7 * th * nb, DrawUtil.BarTag(q.Count, q.Dia), q.Mark, 1); nb++; }
        if (stir != null) w.LeaderTag(b / 2 - cv, h * 0.55, right, h * 0.55, DrawUtil.StirrupTag(stir.Dia, stir.Spacing), stir.Mark, 1);
        if (gia != null) w.LeaderTag(-b / 2 + cv + ds + 6 * k, h * 0.45, -b / 2 - 3 * th, h * 0.45, DrawUtil.BarTag(gia.Count, gia.Dia), gia.Mark, -1);
        w.Dim(-b / 2, 0, b / 2, 0, -3 * th - 2.7 * th * Math.Max(nb - 1, 0), k);
        w.Dim(-b / 2, 0, -b / 2, h, 3 * th, k);
        w.Title(0, w.Extents.Y1 - 1.2 * th, $"MẶT CẮT {sb.Name}", DrawUtil.ScaleText(det), true);
        return w;
    }
}
