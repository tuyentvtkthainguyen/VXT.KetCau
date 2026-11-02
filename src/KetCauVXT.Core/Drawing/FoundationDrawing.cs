using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Bản vẽ móng cọc (đài cọc) và móng băng trên tờ KC-02, theo quy ước nhãn thép kiểu Kết cấu Soft
/// (chấm tại thanh → đường dẫn → vai có chữ "Ø..a.." / "nØ.." → vòng số hiệu).
/// Mặt bằng: vẽ chung vào mặt bằng móng của FootingDrawing; chi tiết: mặt bằng + mặt cắt đài, mặt cắt dọc + mặt cắt ngang dải băng.</summary>
public static class FoundationDrawing
{
    /// <summary>Tỷ lệ mặc định của chương trình (tỷ lệ dùng thật theo Thiết lập bản vẽ: chi tiết móng, mặt đứng dầm).</summary>
    public const double DetailScale = 25;
    public const double StripElevationScale = 50;

    private static double Det => DrawingStyle.Current.ScaleOf(DrawingKind.FootingDetail);

    private static (double X, double Y) P(double x, double y) => (x, y);

    // ------------------------------------------------------------------
    // MẶT BẰNG MÓNG
    // ------------------------------------------------------------------
    /// <summary>Vẽ đài cọc và dải móng băng lên mặt bằng móng (toạ độ thật × 1000 × k, mm). Mở rộng box theo phạm vi đã vẽ.</summary>
    public static void AddToLayout(DxfWriter w, DesignSummary s, double k, ref Rect2 box)
    {
        if (w == null || s == null) return;
        double th = w.Th;
        foreach (var c in s.PileCaps)
        {
            var locs = c.Locations != null && c.Locations.Count > 0 ? c.Locations : new List<(double X, double Y)> { (c.X, c.Y) };
            foreach (var (x, y) in locs)
            {
                double cx = x * 1000 * k, cy = y * 1000 * k, hl = c.L * 500 * k, hb = c.B * 500 * k;
                w.Rect(Lay.BeTong, cx - hl, cy - hb, cx + hl, cy + hb);
                ColumnMark(w, cx, cy, c.ColumnH * 1000 * k, c.ColumnB * 1000 * k);
                foreach (var (px, py) in c.Piles) PileMark(w, cx + px * 1000 * k, cy + py * 1000 * k, c.PileSize * 1000 * k, c.PileRound);
                w.Text(Lay.Chu, cx - hl + 0.3 * th, cy + hb + 0.4 * th, th, c.Name, 0, 0, 0);
                box = box.Union(new Rect2(cx - hl, cy - hb, cx + hl, cy + hb + 1.5 * th));
            }
        }
        foreach (var st in s.StripFootings)
        {
            double a1 = st.Start * 1000 * k, a2 = st.End * 1000 * k, c0 = st.C * 1000 * k;
            double hw = st.Width * 500 * k, hr = st.RibB * 500 * k;
            Rect2 r;
            if (st.AlongX)
            {
                w.Rect(Lay.BeTong, a1, c0 - hw, a2, c0 + hw);
                w.Line(Lay.BeTong, a1, c0 - hr, a2, c0 - hr, "DASHED");
                w.Line(Lay.BeTong, a1, c0 + hr, a2, c0 + hr, "DASHED");
                r = new Rect2(a1, c0 - hw, a2, c0 + hw);
            }
            else
            {
                w.Rect(Lay.BeTong, c0 - hw, a1, c0 + hw, a2);
                w.Line(Lay.BeTong, c0 - hr, a1, c0 - hr, a2, "DASHED");
                w.Line(Lay.BeTong, c0 + hr, a1, c0 + hr, a2, "DASHED");
                r = new Rect2(c0 - hw, a1, c0 + hw, a2);
            }
            for (int i = 0; i < st.ColumnPositions.Count; i++)
            {
                double sp = st.ColumnPositions[i] * 1000 * k;
                double along = (i < st.ColumnSizes.Count && st.ColumnSizes[i] > 0 ? st.ColumnSizes[i] : 0.22) * 1000 * k;
                double across = (i < st.ColumnWidths.Count && st.ColumnWidths[i] > 0 ? st.ColumnWidths[i] : 0.22) * 1000 * k;
                if (st.AlongX) ColumnMark(w, sp, c0, along, across);
                else ColumnMark(w, c0, sp, across, along);
            }
            string tag = st.Name + " (B=" + DxfWriter.N0(st.Width * 1000) + ")";
            if (st.AlongX) w.Text(Lay.Chu, a1 + 0.5 * th, c0 + hw + 0.4 * th, th, tag, 0, 0, 0);
            else w.Text(Lay.Chu, c0 - hw - 0.4 * th, a1 + 0.5 * th, th, tag, 90, 0, 0);
            box = box.Union(r.Inflate(1.5 * th));
        }
    }

    private static void ColumnMark(DxfWriter w, double cx, double cy, double sx, double sy)
    {
        double a = Math.Max(sx, 1) / 2, b = Math.Max(sy, 1) / 2;
        w.Rect(Lay.BeTong, cx - a, cy - b, cx + a, cy + b);
        w.Solid(Lay.BeTong, cx - a, cy - b, cx + a, cy - b, cx - a, cy + b, cx + a, cy + b);
    }

    private static void PileMark(DxfWriter w, double cx, double cy, double d, bool round)
    {
        double r = Math.Max(d, 1) / 2;
        if (round) w.Circle(Lay.BeTong, cx, cy, r);
        else w.Rect(Lay.BeTong, cx - r, cy - r, cx + r, cy + r, "DASHED");
        w.Line(Lay.KyHieu, cx - 0.7 * r, cy, cx + 0.7 * r, cy);
        w.Line(Lay.KyHieu, cx, cy - 0.7 * r, cx, cy + 0.7 * r);
    }

    // ------------------------------------------------------------------
    // CHI TIẾT
    // ------------------------------------------------------------------
    /// <summary>Các hình chi tiết đài cọc và móng băng (tờ tỷ lệ 1/S).</summary>
    public static List<Figure> BuildDetails(Project p, DesignSummary s, double S)
    {
        var res = new List<Figure>();
        if (s == null) return res;
        bool first = true;
        foreach (var c in s.PileCaps)
        {
            try
            {
                res.Add(new Figure(CapPlan(p, c, S), c.Name, first));
                res.Add(new Figure(CapSection(p, c, S), c.Name + " mc"));
            }
            catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure(c.Name, ex, S), c.Name, true)); }
            first = false;
        }
        first = true;
        foreach (var st in s.StripFootings)
        {
            // tên hình không bắt đầu bằng "MB" (tiền tố dành cho mặt bằng móng)
            string fn = "Băng " + st.Name;
            try
            {
                res.Add(new Figure(StripElevation(p, st, S), fn, first));
                res.Add(new Figure(StripSection(p, st, S), fn + " mc"));
            }
            catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure(st.Name, ex, S), fn, true)); }
            first = false;
        }
        return res;
    }

    private static BarMark Bar(MemberDesignBase m, string role, int dia = 0) =>
        m.Bars.FirstOrDefault(b => b.Role == role && (dia == 0 || b.Dia == dia)) ?? m.Bars.FirstOrDefault(b => b.Role == role);

    private static string SpTag(BarMark b, int dia, double sp) =>
        DrawUtil.StirrupTag(b?.Dia > 0 ? b.Dia : dia, b?.Spacing > 0 ? b.Spacing : (sp > 0 ? sp : 0.15));

    /// <summary>Mặt bằng đài cọc: đài, cọc, cổ cột, lưới thép đáy 2 phương, kích thước bố trí cọc.</summary>
    public static DxfWriter CapPlan(Project p, PileCapDesign c, double S)
    {
        var w = new DxfWriter(S);
        double det = Det;
        double th = w.Th, k = S / det;
        double L = c.L * 1000 * k, B = c.B * 1000 * k, xc = L / 2, yc = B / 2;
        double cv = Math.Max(30, p.Concrete.CoverFooting * 1000) * k;
        w.Rect(Lay.BeTong, 0, 0, L, B);
        double hc = (c.ColumnH > 0 ? c.ColumnH : 0.22) * 1000 * k, bc = (c.ColumnB > 0 ? c.ColumnB : 0.22) * 1000 * k;
        w.Rect(Lay.BeTong, xc - hc / 2, yc - bc / 2, xc + hc / 2, yc + bc / 2);
        foreach (var (px, py) in c.Piles) PileMark(w, xc + px * 1000 * k, yc + py * 1000 * k, c.PileSize * 1000 * k, c.PileRound);
        w.Line(Lay.Truc, xc, -3 * th, xc, B + 3 * th, "CENTER");
        w.Line(Lay.Truc, -3 * th, yc, L + 3 * th, yc, "CENTER");

        var bl = Bar(c, "Lớp dưới phương L", c.DiaL);
        var bb = Bar(c, "Lớp dưới phương B", c.DiaB);
        double sL = Math.Max(50, (bl?.Spacing > 0 ? bl.Spacing : 0.15) * 1000) * k;
        double sB = Math.Max(50, (bb?.Spacing > 0 ? bb.Spacing : 0.15) * 1000) * k;
        int i = 0;
        for (double y = cv; y <= B - cv + 1e-6; y += sL, i++)
            if (i % 2 == 0 || y > B - cv - sL) w.Line(Lay.Thep, cv, y, L - cv, y);
        i = 0;
        for (double x = cv; x <= L - cv + 1e-6; x += sB, i++)
            if (i % 2 == 0 || x > L - cv - sB) w.Line(Lay.Thep, x, cv, x, B - cv);
        double yS = B * 0.2, xS = L * 0.8;
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(cv, yS), P(L - cv, yS) }, false, 0.4 * S);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(xS, cv), P(xS, B - cv) }, false, 0.4 * S);
        w.LeaderTag(L * 0.3, yS, L * 0.3 + 2 * th, -3.2 * th, SpTag(bl, c.DiaL, c.SpacingL), bl?.Mark ?? 1, 1);
        w.LeaderTag(xS, B * 0.75, L + 3 * th, B + 2.5 * th, SpTag(bb, c.DiaB, c.SpacingB), bb?.Mark ?? 2, 1);

        // kích thước: toàn bộ + chuỗi tim cọc
        var xsP = c.Piles.Select(q => Math.Round(xc + q.X * 1000 * k, 3)).Distinct().OrderBy(v => v).ToList();
        var ysP = c.Piles.Select(q => Math.Round(yc + q.Y * 1000 * k, 3)).Distinct().OrderBy(v => v).ToList();
        var chainX = new List<double> { 0 }; chainX.AddRange(xsP); chainX.Add(L);
        var chainY = new List<double> { 0 }; chainY.AddRange(ysP); chainY.Add(B);
        w.DimChainH(chainX, 0, -6.5 * th, k);
        w.Dim(0, 0, L, 0, -9.0 * th, k);
        w.DimChainV(chainY, 0, -3 * th, k);
        w.Dim(0, 0, 0, B, 5.5 * th, k);
        PileNumbers(w, c, xc, yc, k, L);   // [FoundationChecks] số hiệu + toạ độ cọc
        w.Title(L / 2, w.Extents.Y2 + 4 * th, $"{c.Name} (SL={Math.Max(1, c.Quantity)})", DrawUtil.ScaleText(det));
        return w;
    }

    /// <summary>Mặt cắt đài cọc theo phương L: bê tông lót, đài, đầu cọc ngàm vào đài, cổ cột, thép đáy/đỉnh đài.</summary>
    public static DxfWriter CapSection(Project p, PileCapDesign c, double S)
    {
        var w = new DxfWriter(S);
        double det = Det;
        double th = w.Th, k = S / det;
        double L = c.L * 1000 * k, H = Math.Max(0.3, c.H) * 1000 * k, xc = L / 2;
        double cv = Math.Max(30, p.Concrete.CoverFooting * 1000) * k;
        double lot = 100 * k, emb = Math.Max(0, p.Foundation?.PileEmbed ?? 0.1) * 1000 * k;
        double hc = (c.ColumnH > 0 ? c.ColumnH : 0.22) * 1000 * k;
        double neck = 500 * k, dp = c.PileSize * 1000 * k, pileShow = 700 * k;
        // lót + đài
        w.Rect(Lay.BeTong, -lot, -lot, L + lot, 0);
        w.Rect(Lay.BeTong, 0, 0, L, H);
        // cổ cột
        w.Line(Lay.BeTong, xc - hc / 2, H, xc - hc / 2, H + neck);
        w.Line(Lay.BeTong, xc + hc / 2, H, xc + hc / 2, H + neck);
        w.BreakH(Lay.BeTong, xc - hc / 2, xc + hc / 2, H + neck);
        w.Line(Lay.Truc, xc, -lot - pileShow - 2 * th, xc, H + neck + 2 * th, "CENTER");
        // cọc (hình chiếu các hàng cọc)
        foreach (double px in c.Piles.Select(q => Math.Round(q.X, 4)).Distinct())
        {
            double x = xc + px * 1000 * k;
            w.Line(Lay.BeTong, x - dp / 2, -lot - pileShow, x - dp / 2, emb);
            w.Line(Lay.BeTong, x + dp / 2, -lot - pileShow, x + dp / 2, emb);
            w.Line(Lay.BeTong, x - dp / 2, emb, x + dp / 2, emb, "DASHED");
            w.BreakH(Lay.BeTong, x - dp / 2, x + dp / 2, -lot - pileShow);
        }
        var bl = Bar(c, "Lớp dưới phương L", c.DiaL);
        var bb = Bar(c, "Lớp dưới phương B", c.DiaB);
        var tl = Bar(c, "Lớp trên phương L");
        var tb = Bar(c, "Lớp trên phương B");
        double dl = (bl?.Dia ?? 16) * k, db = (bb?.Dia ?? 16) * k;
        double yb = emb + 30 * k + db + dl / 2;
        double legB = Math.Min(H - yb - cv, (bl != null && bl.Segments.Count > 0 ? bl.Segments[0] : 200) * k);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(cv, yb + legB), P(cv, yb), P(L - cv, yb), P(L - cv, yb + legB) }, false, 0.4 * S);
        double sB = Math.Max(50, (bb?.Spacing > 0 ? bb.Spacing : 0.15) * 1000) * k;
        for (double x = cv + db; x <= L - cv - db + 1e-6; x += sB) w.Dot(Lay.Thep, x, emb + 30 * k + db / 2, Math.Max(db / 2, 0.15 * th));
        double yt = H - cv - (tl?.Dia ?? 12) * k / 2;
        double legT = Math.Min(H / 3, (tl != null && tl.Segments.Count > 0 ? tl.Segments[0] : 150) * k);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(cv, yt - legT), P(cv, yt), P(L - cv, yt), P(L - cv, yt - legT) }, false, 0.4 * S);
        double sT = Math.Max(50, (tb?.Spacing > 0 ? tb.Spacing : 0.2) * 1000) * k;
        double dt = (tb?.Dia ?? 12) * k;
        for (double x = cv + dt; x <= L - cv - dt + 1e-6; x += sT) w.Dot(Lay.Thep, x, yt - dt, Math.Max(dt / 2, 0.15 * th));
        // thép chờ cột
        for (int i = 0; i < 2; i++)
        {
            double x = i == 0 ? xc - hc / 2 + 30 * k : xc + hc / 2 - 30 * k;
            w.Polyline(Lay.Thep, new List<(double X, double Y)>
            {
                P(x + (i == 0 ? -200 * k : 200 * k), yb + dl), P(x, yb + dl), P(x, H + neck)
            }, false, 0.3 * S);
        }
        // nhãn thép
        w.LeaderTag(L * 0.25, yb, L * 0.25 + 2 * th, -lot - pileShow - 3.0 * th, SpTag(bl, c.DiaL, c.SpacingL), bl?.Mark ?? 1, 1);
        w.LeaderTag(L * 0.62, emb + 30 * k + db / 2, L * 0.62 + 2 * th, -lot - pileShow - 5.7 * th, SpTag(bb, c.DiaB, c.SpacingB), bb?.Mark ?? 2, 1);
        if (tl != null) w.LeaderTag(L * 0.3, yt, L * 0.3 - 2 * th, H + neck + 2.5 * th, SpTag(tl, 12, 0.2), tl.Mark, -1);
        if (tb != null) w.LeaderTag(L * 0.7, yt - dt, L * 0.7 + 2 * th, H + neck + 2.5 * th, SpTag(tb, 12, 0.2), tb.Mark, 1);
        // kích thước
        w.Dim(0, 0, 0, H, 2.5 * th, k);
        if (emb > 1) w.Dim(L, 0, L, emb, -2.5 * th, k);
        w.Dim(-lot, -lot, -lot, 0, 2.5 * th + lot, k);
        var xsP = c.Piles.Select(q => Math.Round(xc + q.X * 1000 * k, 3)).Distinct().OrderBy(v => v).ToList();
        var chain = new List<double> { 0 }; chain.AddRange(xsP); chain.Add(L);
        w.DimChainH(chain, -lot, -lot - pileShow - 8.5 * th, k);
        w.Dim(0, -lot, L, -lot, -pileShow - 11 * th, k);
        if (p.Foundation?.DrawPunchingPyramid ?? true) PunchPyramid(w, c, xc, H, hc, k);   // [FoundationChecks] tháp chọc thủng 45°
        double zTop = p.BaseElevation;
        w.ElevMark(L + 4 * th, H, DxfWriter.ElevText(zTop), 1);
        w.ElevMark(L + 4 * th, 0, DxfWriter.ElevText(zTop - c.H), 1);
        w.Title(L / 2, w.Extents.Y1 - 1.2 * th, $"MẶT CẮT {c.Name}", DrawUtil.ScaleText(det), true);
        string info = $"{c.Piles.Count} cọc {(c.PileRound ? "D" : "")}{DxfWriter.N0(c.PileSize * 1000)}{(c.PileRound ? "" : "x" + DxfWriter.N0(c.PileSize * 1000))}, L = {c.PileLength.ToString("0.0", DrawUtil.Inv)} m" +
                      (w.Style.ShowDesignValues ? $", [P] = {DxfWriter.N0(c.PileCapacity)} kN" : "");
        w.Text(Lay.Chu, L / 2, w.Extents.Y1 - 1.0 * th, th, info, 0, 1, 3);
        return w;
    }

    // ------------------------------------------------------------------
    // [FoundationChecks] số hiệu cọc, tháp chọc thủng, biểu đồ mô men sườn móng băng
    // ------------------------------------------------------------------
    /// <summary>Ghi số hiệu cọc C1, C2… cạnh từng cọc và bảng toạ độ tim cọc (so với tim cột, mm) bên phải mặt bằng đài.</summary>
    private static void PileNumbers(DxfWriter w, PileCapDesign c, double xc, double yc, double k, double L)
    {
        if (c?.Piles == null || c.Piles.Count == 0) return;
        double th = w.Th, r = Math.Max(c.PileSize * 500 * k, 1);
        for (int i = 0; i < c.Piles.Count; i++)
        {
            double x = xc + c.Piles[i].X * 1000 * k, y = yc + c.Piles[i].Y * 1000 * k;
            w.Text(Lay.Chu, x + r + 0.2 * th, y + r + 0.2 * th, 0.8 * th, "C" + (i + 1), 0, 0, 0);
        }
        if (c.Piles.Count > 16) return;
        double tx = L + 9 * th, ty = w.Extents.Y2 - th;
        w.Text(Lay.Chu, tx, ty, 0.85 * th, "TOẠ ĐỘ TIM CỌC (mm, gốc tại tim cột)", 0, 0, 0);
        for (int i = 0; i < c.Piles.Count; i++)
        {
            ty -= 1.4 * th;
            string txt = "C" + (i + 1) + ": x = " + DxfWriter.N0(c.Piles[i].X * 1000) + "; y = " + DxfWriter.N0(c.Piles[i].Y * 1000)
                         + (w.Style.ShowDesignValues && c.Safety?.Piles != null && i < c.Safety.Piles.Count ? "; Pmax = " + DxfWriter.N0(c.Safety.Piles[i].PMax) + " kN" : "");
            w.Text(Lay.Chu, tx, ty, 0.8 * th, txt, 0, 0, 0);
        }
    }

    /// <summary>Tháp chọc thủng 45° (nét đứt) trên mặt cắt đài: từ mép cột tại mặt trên đài xuống mức h0, mở rộng h0 mỗi phía.</summary>
    private static void PunchPyramid(DxfWriter w, PileCapDesign c, double xc, double H, double hc, double k)
    {
        if (c == null || c.Piles.Count < 2) return;
        double h0 = (c.H0 > 0 ? c.H0 : c.H - 0.15) * 1000 * k;
        if (h0 <= 1) return;
        double y1 = H - h0;
        w.Line(Lay.KyHieu, xc - hc / 2, H, xc - hc / 2 - h0, y1, "DASHED");
        w.Line(Lay.KyHieu, xc + hc / 2, H, xc + hc / 2 + h0, y1, "DASHED");
        w.Text(Lay.Chu, xc + hc / 2 + h0 + 0.3 * w.Th, y1 + 0.3 * w.Th, 0.75 * w.Th, "tháp chọc thủng 45°", 0, 0, 0);
    }

    /// <summary>Biểu đồ bao mô men sườn móng băng (TTGH I, kN.m) vẽ dưới mặt cắt dọc; mô men dương (căng dưới) vẽ phía dưới trục.</summary>
    private static void StripMomentDiagram(DxfWriter w, StripFootingDesign st, double k, double L)
    {
        if (st?.Stations == null || st.Stations.Count < 2) return;
        var mx = st.MMax != null && st.MMax.Count == st.Stations.Count ? st.MMax : st.M;
        var mn = st.MMin != null && st.MMin.Count == st.Stations.Count ? st.MMin : st.M;
        if (mx == null || mn == null || mx.Count != st.Stations.Count) return;
        double mAbs = Math.Max(mx.Select(v => Math.Abs(v)).DefaultIfEmpty(0).Max(), mn.Select(v => Math.Abs(v)).DefaultIfEmpty(0).Max());
        if (mAbs < 1e-6) return;
        double th = w.Th, hDia = 6 * th, sc = hDia / mAbs;
        double y0 = w.Extents.Y1 - 3 * th - hDia;
        w.Line(Lay.KyHieu, 0, y0, L, y0);
        List<(double X, double Y)> Curve(List<double> m) =>
            st.Stations.Select((x, i) => P((x - st.Start) * 1000 * k, y0 - m[i] * sc)).ToList();
        w.Polyline(Lay.KyHieu, Curve(mx), false);
        w.Polyline(Lay.KyHieu, Curve(mn), false, 0, "DASHED");
        int iMax = mx.IndexOf(mx.Max()), iMin = mn.IndexOf(mn.Min());
        if (mx[iMax] > 1e-6) w.Text(Lay.Chu, (st.Stations[iMax] - st.Start) * 1000 * k, y0 - mx[iMax] * sc - 1.0 * th, 0.8 * th, "M+ = " + mx[iMax].ToString("0.0", DrawUtil.Inv), 0, 1, 0);
        if (mn[iMin] < -1e-6) w.Text(Lay.Chu, (st.Stations[iMin] - st.Start) * 1000 * k, y0 - mn[iMin] * sc + 0.4 * th, 0.8 * th, "M− = " + mn[iMin].ToString("0.0", DrawUtil.Inv), 0, 1, 0);
        w.Text(Lay.Chu, 0, y0 + 0.4 * th, 0.8 * th, "BIỂU ĐỒ BAO MÔ MEN SƯỜN (kN.m) – " + (st.Safety?.Classification ?? "dầm trên nền đàn hồi"), 0, 0, 0);
    }

    /// <summary>Mặt cắt dọc dải móng băng: sườn, cổ cột, thép trên/dưới, đai, chuỗi kích thước tim cột.</summary>
    public static DxfWriter StripElevation(Project p, StripFootingDesign st, double S)
    {
        var w = new DxfWriter(S);
        double th = w.Th;
        // mặt cắt dọc dải băng vẽ theo tỷ lệ mặt đứng dầm (Thiết lập bản vẽ); dải dài > 18 m tối thiểu 1/100
        double baseSc = DrawingStyle.Current.ScaleOf(DrawingKind.BeamElevation);
        double scale = st.Length > 18 ? Math.Max(100, baseSc) : baseSc;
        double k = S / scale;
        double L = st.Length * 1000 * k, h = st.RibH * 1000 * k, he = st.FlangeEdge * 1000 * k;
        double cv = Math.Max(30, p.Concrete.CoverFooting * 1000) * k;
        double lot = 100 * k, neck = 500 * k;
        w.Rect(Lay.BeTong, -lot, -lot, L + lot, 0);
        w.Rect(Lay.BeTong, 0, 0, L, h);
        if (he < h - 1) w.Line(Lay.BeTong, 0, he, L, he, "DASHED");
        var pos = new List<double>();
        for (int i = 0; i < st.ColumnPositions.Count; i++)
        {
            double x = (st.ColumnPositions[i] - st.Start) * 1000 * k;
            double a = (i < st.ColumnSizes.Count && st.ColumnSizes[i] > 0 ? st.ColumnSizes[i] : 0.22) * 500 * k;
            w.Line(Lay.BeTong, x - a, h, x - a, h + neck);
            w.Line(Lay.BeTong, x + a, h, x + a, h + neck);
            w.BreakH(Lay.BeTong, x - a, x + a, h + neck);
            w.Line(Lay.Truc, x, -lot - 2 * th, x, h + neck + 2 * th, "CENTER");
            pos.Add(x);
        }
        var bot = st.Bars.Where(b => b.Role == "Dưới").ToList();
        var top = st.Bars.Where(b => b.Role == "Trên").ToList();
        var stir = Bar(st, "Đai");
        double yb = cv + 10 * k, yt = h - cv - 10 * k;
        double leg = Math.Min(h / 3, 200 * k);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(cv, yb + leg), P(cv, yb), P(L - cv, yb), P(L - cv, yb + leg) }, false, 0.4 * S);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(cv, yt - leg), P(cv, yt), P(L - cv, yt), P(L - cv, yt - leg) }, false, 0.4 * S);
        if (stir != null && stir.Spacing > 0)
        {
            double ss = stir.Spacing * 1000 * k;
            int j = 0;
            for (double x = cv + ss / 2; x < L - cv; x += ss, j++)
                if (j % 3 == 0) w.Line(Lay.Dai, x, cv, x, h - cv);
        }
        // nhãn thép: mỗi số hiệu một nhãn
        double tagY = -lot - 3 * th;
        int n = 0;
        foreach (var b in bot)
        {
            double x = L * (0.15 + 0.12 * n);
            w.LeaderTag(x, yb, x + 2 * th, tagY - 2.7 * th * n, DrawUtil.BarTag(b.Count, b.Dia), b.Mark, 1);
            n++;
        }
        double tagT = h + neck + 2.5 * th;
        n = 0;
        foreach (var b in top)
        {
            double x = L * (0.35 + 0.12 * n);
            w.LeaderTag(x, yt, x + 2 * th, tagT + 2.7 * th * n, DrawUtil.BarTag(b.Count, b.Dia), b.Mark, 1);
            n++;
        }
        if (stir != null)
        {
            double x = pos.Count > 1 ? (pos[0] + pos[1]) / 2 : L * 0.6;
            w.LeaderTag(x, h / 2, x + 3 * th, tagT + 2.7 * th * Math.Max(top.Count, 1), SpTag(stir, st.StirrupDia, st.StirrupSpacing), stir.Mark, 1);
        }
        var chain = new List<double> { 0 }; chain.AddRange(pos); chain.Add(L);
        w.DimChainH(chain, -lot, tagY - 2.7 * th * Math.Max(bot.Count, 1) - 2.5 * th, k);
        w.Dim(0, -lot, L, -lot, tagY - 2.7 * th * Math.Max(bot.Count, 1) - 5.5 * th + lot, k);
        w.Dim(0, 0, 0, h, 2.5 * th, k);
        w.ElevMark(L + 3 * th, h, DxfWriter.ElevText(p.BaseElevation), 1);
        w.ElevMark(L + 3 * th, 0, DxfWriter.ElevText(p.BaseElevation - st.RibH), 1);
        if (p.Foundation?.DrawStripMoment == true) StripMomentDiagram(w, st, k, L);   // [FoundationChecks] biểu đồ mô men sườn
        w.Title(L / 2, w.Extents.Y1 - 1.2 * th, $"MẶT CẮT DỌC {st.Name} ({st.Grid})", DrawUtil.ScaleText(scale), true);
        return w;
    }

    /// <summary>Mặt cắt ngang điển hình dải móng băng: bản đáy vát, sườn, đai, thép dọc trên/dưới, thép bản cánh.</summary>
    public static DxfWriter StripSection(Project p, StripFootingDesign st, double S)
    {
        var w = new DxfWriter(S);
        double det = Det;
        double th = w.Th, k = S / det;
        double B = st.Width * 1000 * k, b = st.RibB * 1000 * k, h = st.RibH * 1000 * k;
        double hr = Math.Min(st.FlangeRoot, st.RibH) * 1000 * k, he = Math.Min(st.FlangeEdge, st.FlangeRoot) * 1000 * k;
        double cv = Math.Max(30, p.Concrete.CoverFooting * 1000) * k;
        double lot = 100 * k, x0 = -B / 2, x1 = B / 2, r0 = -b / 2, r1 = b / 2;
        w.Rect(Lay.BeTong, x0 - lot, -lot, x1 + lot, 0);
        if (B > b + 1)
            w.Polyline(Lay.BeTong, new List<(double X, double Y)>
            {
                P(x0, 0), P(x1, 0), P(x1, he), P(r1, hr), P(r1, h), P(r0, h), P(r0, hr), P(x0, he)
            }, true);
        else w.Rect(Lay.BeTong, r0, 0, r1, h);
        w.Line(Lay.Truc, 0, -lot - 2 * th, 0, h + 3 * th, "CENTER");

        // đai sườn
        var stir = Bar(st, "Đai");
        double ds = (stir?.Dia ?? 8) * k;
        w.Rect(Lay.Dai, r0 + cv, cv, r1 - cv, h - cv);
        // thép bản cánh (lớp dưới ngang) và thép rải dọc
        var fl = Bar(st, "Lớp dưới bản móng");
        var fr = Bar(st, "Thép rải dọc bản móng");
        double dfl = (fl?.Dia ?? 12) * k;
        double yf = cv + dfl / 2;
        if (fl != null)
        {
            double lg = Math.Max(Math.Min(he - 2 * cv, 150 * k), 50 * k);
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { P(x0 + cv, yf + lg), P(x0 + cv, yf), P(x1 - cv, yf), P(x1 - cv, yf + lg) }, false, 0.4 * S);
        }
        if (fr != null)
        {
            double dr = fr.Dia * k, sp = Math.Max(fr.Spacing, 0.1) * 1000 * k;
            for (double x = r0 - sp / 2; x > x0 + cv; x -= sp) w.Dot(Lay.Thep, x, yf + dfl / 2 + dr / 2, Math.Max(dr / 2, 0.15 * th));
            for (double x = r1 + sp / 2; x < x1 - cv; x += sp) w.Dot(Lay.Thep, x, yf + dfl / 2 + dr / 2, Math.Max(dr / 2, 0.15 * th));
        }
        // thép dọc sườn
        var bot = st.Bars.Where(q => q.Role == "Dưới").ToList();
        var top = st.Bars.Where(q => q.Role == "Trên").ToList();
        var side = Bar(st, "Giá");
        double yb0 = cv + ds, yt0 = h - cv - ds;
        void Row(List<BarMark> set, bool isTop)
        {
            int total = set.Sum(q => q.Count);
            if (total <= 0) return;
            int dmax = set.Max(q => q.Dia);
            double y = isTop ? yt0 - dmax * k / 2 : Math.Max(yb0, yf + dfl) + dmax * k / 2;
            double xa = r0 + cv + ds + dmax * k / 2, xb = r1 - cv - ds - dmax * k / 2;
            int idx = 0;
            foreach (var q in set)
                for (int i = 0; i < q.Count; i++, idx++)
                {
                    double x = total == 1 ? 0 : xa + (xb - xa) * idx / (total - 1);
                    w.Dot(Lay.Thep, x, y, Math.Max(q.Dia * k / 2, 0.15 * th));
                }
        }
        Row(bot, false);
        Row(top, true);
        if (side != null && side.Count >= 2)
        {
            int per = side.Count / 2;
            for (int i = 1; i <= per; i++)
            {
                double y = cv + (h - 2 * cv) * i / (per + 1);
                w.Dot(Lay.Thep, r0 + cv + ds + 6 * k, y, Math.Max(6 * k, 0.15 * th));
                w.Dot(Lay.Thep, r1 - cv - ds - 6 * k, y, Math.Max(6 * k, 0.15 * th));
            }
        }
        // nhãn
        double right = x1 + 3 * th;
        int nT = 0;
        foreach (var q in top)
        {
            w.LeaderTag(r1 - cv - ds, yt0 - q.Dia * k / 2, right, h + 1.5 * th + 2.7 * th * nT, DrawUtil.BarTag(q.Count, q.Dia), q.Mark, 1);
            nT++;
        }
        if (stir != null) w.LeaderTag(r1 - cv, h * 0.62, right, h * 0.62, SpTag(stir, st.StirrupDia, st.StirrupSpacing), stir.Mark, 1);
        if (side != null) w.LeaderTag(r0 + cv + ds + 6 * k, h * 0.5, x0 - 3 * th, h * 0.5 + 2 * th, DrawUtil.BarTag(side.Count, side.Dia), side.Mark, -1);
        int nB = 0;
        foreach (var q in bot)
        {
            w.LeaderTag(0, Math.Max(yb0, yf + dfl) + q.Dia * k / 2, x0 - 3 * th, -lot - 2.5 * th - 2.7 * th * nB, DrawUtil.BarTag(q.Count, q.Dia), q.Mark, -1);
            nB++;
        }
        if (fl != null) w.LeaderTag(x1 * 0.6, yf, right, -lot - 2.5 * th, SpTag(fl, fl.Dia, fl.Spacing), fl.Mark, 1);
        if (fr != null) w.LeaderTag(x1 - (x1 - r1) * 0.35, yf + dfl, right, -lot - 5.2 * th, SpTag(fr, fr.Dia, fr.Spacing), fr.Mark, 1);
        // kích thước
        double yd = -lot - 2.7 * th * Math.Max(nB, 2) - 3 * th;
        if (B > b + 1) w.DimChainH(new List<double> { x0, r0, r1, x1 }, -lot, yd, k);
        w.Dim(x0, -lot, x1, -lot, yd - 2.5 * th + lot, k);
        var hv = new List<double> { 0, h };
        if (B > b + 1) { hv.Add(he); hv.Add(hr); }
        w.DimChainV(hv.Distinct().ToList(), x0, x0 - 2.5 * th, k);
        w.Title(0, w.Extents.Y1 - 1.2 * th, $"MẶT CẮT NGANG {st.Name}", DrawUtil.ScaleText(det), true);
        return w;
    }
}
