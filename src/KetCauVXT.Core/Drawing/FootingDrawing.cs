using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Bản vẽ móng đơn (và đài cọc, móng băng qua FoundationDrawing): mặt bằng bố trí móng (tl 1/100) + chi tiết từng loại móng
/// (mặt bằng thép + mặt cắt, tl 1/25 thể hiện trên tờ 1/50 bằng hệ số phóng k = 2).</summary>
public static class FootingDrawing
{
    /// <summary>Tỷ lệ mặc định của chương trình (tỷ lệ dùng thật theo Thiết lập bản vẽ: <see cref="CurrentPlanScale"/>, <see cref="CurrentDetailScale"/>).</summary>
    public const double PlanScale = 100;
    /// <summary>Mẫu số tỷ lệ cơ sở của tờ móng (chữ in theo tỷ lệ này; hình phóng/thu bằng hệ số k).</summary>
    public const double SheetScale = 50;
    public const double DetailScale = 25;

    /// <summary>Mẫu số tỷ lệ mặt bằng móng theo Thiết lập bản vẽ (mặc định 100).</summary>
    public static double CurrentPlanScale => DrawingStyle.Current.ScaleOf(DrawingKind.FoundationPlan);
    /// <summary>Mẫu số tỷ lệ chi tiết móng theo Thiết lập bản vẽ (mặc định 25).</summary>
    public static double CurrentDetailScale => DrawingStyle.Current.ScaleOf(DrawingKind.FootingDetail);

    public static List<Figure> BuildAll(Project p, DesignSummary s)
    {
        var res = new List<Figure>();
        if (s == null || (s.Footings.Count == 0 && s.PileCaps.Count == 0 && s.StripFootings.Count == 0)) return res;
        try
        {
            // mặt bằng vẽ ở 1/100 rồi thu nhỏ hình học ×0.5 để ghép lên tờ 1/50: dựng trực tiếp với hệ số k
            res.Add(new Figure(Layout(p, s, SheetScale, SheetScale / CurrentPlanScale), "MB móng", true));
        }
        catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure("MB móng", ex, SheetScale), "MB", true)); }
        bool first = true;
        foreach (var g in Distinct(s.Footings))
        {
            try
            {
                var f = g.First();
                if (f.Eccentric?.Combined != null)   // [MóngLệchTâm] móng kết hợp
                {
                    res.Add(new Figure(EccentricDrawing.CombinedPlan(p, f, SheetScale), f.Name, first));
                    res.Add(new Figure(EccentricDrawing.CombinedSection(p, f, SheetScale), f.Name + " mc"));
                    first = false;
                    continue;
                }
                res.Add(new Figure(DetailPlan(p, f, g.Count(), SheetScale), f.Name, first));
                res.Add(new Figure(DetailSection(p, f, SheetScale), f.Name + " mc"));
            }
            catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure(g.Key, ex, SheetScale), g.Key, true)); }
            first = false;
        }
        // [CừTràm] mặt bằng lưới cừ + mặt cắt (MNN thấp nhất)
        try { res.AddRange(CuTramDrawing.BuildDetails(p, s, SheetScale)); }
        catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure("Cừ tràm", ex, SheetScale), "CT", true)); }
        // đài cọc, móng băng
        try { res.AddRange(FoundationDrawing.BuildDetails(p, s, SheetScale)); }
        catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure("Đài cọc / móng băng", ex, SheetScale), "ĐC", true)); }
        // [MóngLệchTâm] dầm móng cân bằng (mặt đứng + mặt cắt)
        try { res.AddRange(EccentricDrawing.BuildDetails(p, s, SheetScale)); }
        catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure("Dầm móng cân bằng", ex, SheetScale), "DM", true)); }
        return res;
    }

    /// <summary>Gộp móng theo tên (M1, M2...). Móng cùng tên được coi là giống hệt.</summary>
    public static List<IGrouping<string, FootingDesign>> Distinct(IEnumerable<FootingDesign> list) =>
        list.GroupBy(f => string.IsNullOrEmpty(f.Name) ? $"{f.L:0.00}x{f.B:0.00}x{f.H:0.00}|{f.BarsL}|{f.BarsB}" : f.Name)
            .OrderBy(g => g.Key, StringComparer.Ordinal).ToList();

    /// <summary>Mặt bằng bố trí móng. k: hệ số co hình (giấy 1/S, hình vẽ ở tỷ lệ 1/(S/k)).</summary>
    public static DxfWriter Layout(Project p, DesignSummary s, double S, double k)
    {
        // vẽ ở toạ độ thật rồi co bằng một DxfWriter phụ là phức tạp; ở đây vẽ trực tiếp toạ độ × k.
        var w = new DxfWriter(S);
        double th = w.Th;
        var box = Rect2.Empty;
        foreach (var f in s.Footings)
        {
            if (f.Eccentric != null) { EccentricDrawing.FootingPlan(w, f, k, ref box); continue; }   // [MóngLệchTâm] móng lệch tâm / kết hợp
            double cx = f.X * 1000 * k, cy = f.Y * 1000 * k;
            double hl = f.L * 500 * k, hb = f.B * 500 * k;
            w.Rect(Lay.BeTong, cx - hl, cy - hb, cx + hl, cy + hb);
            double cb = (f.ColumnH > 0 ? f.ColumnH : 0.22) * 500 * k, ch = (f.ColumnB > 0 ? f.ColumnB : 0.22) * 500 * k;
            w.Rect(Lay.BeTong, cx - cb, cy - ch, cx + cb, cy + ch);
            w.Solid(Lay.BeTong, cx - cb, cy - ch, cx + cb, cy - ch, cx - cb, cy + ch, cx + cb, cy + ch);
            w.Text(Lay.Chu, cx + hl * 0.15, cy + hb - 1.5 * th, th, f.Name, 0, 0, 0);
            box = box.Union(new Rect2(cx - hl, cy - hb, cx + hl, cy + hb));
        }
        FoundationDrawing.AddToLayout(w, s, k, ref box);
        EccentricDrawing.AddToLayout(w, p, s, k, ref box);   // [MóngLệchTâm] ranh giới đất + dầm cân bằng
        CuTramDrawing.AddToLayout(w, s, k, ref box);   // [CừTràm] ghi chú móng gia cố cừ tràm
        PileSetbackDrawing.AddToLayout(w, p, s, k, ref box);   // [LùiCọcRanh] đường giới hạn tim cọc + "≥ 500 (lùi ép cọc)"
        // trục
        var gx = p.GridsX.ToList(); var gy = p.GridsY.ToList();
        foreach (var g in gx) box = box.Union(g.Coord * 1000 * k, box.Cy);
        foreach (var g in gy) box = box.Union(box.Cx, g.Coord * 1000 * k);
        double ext = 6 * th, r = w.BubbleR;
        double xL = box.X1 - ext, xR = box.X2 + ext, yB = box.Y1 - ext, yT = box.Y2 + ext;
        foreach (var g in gx)
        {
            double x = g.Coord * 1000 * k;
            w.Line(Lay.Truc, x, yB, x, yT, "CENTER");
            w.GridBubble(x, yB - r, g.Name);
            w.GridBubble(x, yT + r, g.Name);
        }
        foreach (var g in gy)
        {
            double y = g.Coord * 1000 * k;
            w.Line(Lay.Truc, xL, y, xR, y, "CENTER");
            w.GridBubble(xL - r, y, g.Name);
            w.GridBubble(xR + r, y, g.Name);
        }
        bool gridDims = w.Style.ShowGridDims;
        double yd1 = yB - 2 * r - 2.5 * th, yd2 = yd1 - 2.5 * th;
        if (gx.Count > 1 && gridDims)
        {
            w.DimChainH(gx.Select(g => g.Coord * 1000 * k).ToList(), yd1, yd1, k);
            if (gx.Count > 2) w.Dim(gx[0].Coord * 1000 * k, yd2, gx[^1].Coord * 1000 * k, yd2, 0, k);
        }
        double xd1 = xL - 2 * r - 2.5 * th, xd2 = xd1 - 2.5 * th;
        if (gy.Count > 1 && gridDims)
        {
            w.DimChainV(gy.Select(g => g.Coord * 1000 * k).ToList(), xd1, xd1, k);
            if (gy.Count > 2) w.Dim(xd2, gy[0].Coord * 1000 * k, xd2, gy[^1].Coord * 1000 * k, 0, k);
        }
        w.Title(box.Cx, w.Extents.Y2 + 5 * th, "MẶT BẰNG MÓNG", DrawUtil.ScaleText(S / k));
        return w;
    }

    private static (int Dia, double Sp, int Mark, int Count) BarsDir(FootingDesign f, bool alongL)
    {
        int dia = alongL ? f.DiaL : f.DiaB;
        double sp = alongL ? f.SpacingL : f.SpacingB;
        // vai trò của FootingDesigner: "Lớp dưới phương L" / "Lớp dưới phương B" (giữ cả hậu tố X/Y cũ)
        string[] roles = alongL ? new[] { "phương L", "X" } : new[] { "phương B", "Y" };
        var bm = f.Bars.Where(b => !DrawUtil.IsStirrup(b) || b.Spacing > 0)
                       .FirstOrDefault(b => roles.Any(r => (b.Role ?? "").EndsWith(r, StringComparison.Ordinal)) && (dia == 0 || b.Dia == dia))
                 ?? f.Bars.FirstOrDefault(b => b.Dia == dia && Math.Abs(b.Spacing - sp) < 1e-4)
                 ?? (f.Bars.Count > (alongL ? 0 : 1) ? f.Bars[alongL ? 0 : 1] : null);
        if (dia == 0) dia = bm?.Dia ?? 12;
        if (sp <= 0) sp = bm?.Spacing > 0 ? bm.Spacing : 0.15;
        int mark = bm?.Mark ?? (alongL ? 1 : 2);
        int count = bm?.Count ?? 0;
        return (dia, sp, mark, count);
    }

    /// <summary>Mặt bằng chi tiết móng: thép 2 phương, kích thước, cổ cột.</summary>
    public static DxfWriter DetailPlan(Project p, FootingDesign f, int qty, double S)
    {
        var w = new DxfWriter(S);
        double det = CurrentDetailScale;
        double th = w.Th, k = S / det;
        double L = f.L * 1000 * k, B = f.B * 1000 * k;
        double c = Math.Max(30, p.Concrete.CoverFooting * 1000) * k;
        double cb = (f.ColumnH > 0 ? f.ColumnH : 0.22) * 1000 * k, ch = (f.ColumnB > 0 ? f.ColumnB : 0.22) * 1000 * k;
        // [MóngLệchTâm] tim cột lệch so với tâm móng: xc = L/2 − ex, yc = B/2 − ey
        double ex = (f.Eccentric?.OffsetX ?? 0) * 1000 * k, ey = (f.Eccentric?.OffsetY ?? 0) * 1000 * k, xc = L / 2 - ex, yc = B / 2 - ey;
        w.Rect(Lay.BeTong, 0, 0, L, B);
        w.Rect(Lay.BeTong, xc - cb / 2, yc - ch / 2, xc + cb / 2, yc + ch / 2);
        w.Line(Lay.Truc, xc, -3 * th, xc, B + 3 * th, "CENTER");
        w.Line(Lay.Truc, -3 * th, yc, L + 3 * th, yc, "CENTER");

        var bl = BarsDir(f, true);
        var bb = BarsDir(f, false);
        // thép phương L (chạy theo X), rải theo Y
        double sL = Math.Max(50, bl.Sp * 1000) * k, sB = Math.Max(50, bb.Sp * 1000) * k;
        int i = 0;
        for (double y = c; y <= B - c + 1e-6; y += sL, i++)
            if (i % 2 == 0 || y > B - c - sL) w.Line(Lay.Thep, c, y, L - c, y);
        i = 0;
        for (double x = c; x <= L - c + 1e-6; x += sB, i++)
            if (i % 2 == 0 || x > L - c - sB) w.Line(Lay.Thep, x, c, x, B - c);
        // thanh điển hình đậm
        double yS = B * 0.25, xS = L * 0.75;
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (c, yS), (L - c, yS) }, false, 0.4 * S);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xS, c), (xS, B - c) }, false, 0.4 * S);
        string tL = DrawUtil.StirrupTag(bl.Dia, bl.Sp), tB = DrawUtil.StirrupTag(bb.Dia, bb.Sp);
        w.LeaderTag(L * 0.3, yS, L * 0.3 + 2 * th, -3.2 * th, tL, bl.Mark, 1);
        w.LeaderTag(xS, B * 0.7, L + 3 * th, B + 2.5 * th, tB, bb.Mark, 1);

        w.Dim(0, 0, L, 0, -6.5 * th, k);
        w.Dim(0, 0, 0, B, 3 * th, k);
        w.Dim(xc - cb / 2, B, xc + cb / 2, B, 2.5 * th, k);
        if (Math.Abs(ex) > 1e-6) w.DimChainH(new List<double> { 0, xc, L }, 0, -9 * th, k);   // [MóngLệchTâm] tim cột – mép móng
        if (Math.Abs(ey) > 1e-6) w.DimChainV(new List<double> { 0, yc, B }, 0, -6 * th, k);
        if (f.Eccentric != null && (Math.Abs(ex) > 1e-6 || Math.Abs(ey) > 1e-6))
        {
            // mép móng sát ranh giới
            if (f.Eccentric.Sides.Contains("trái")) w.Text(Lay.Chu, -0.6 * th, B + 1.2 * th, 0.8 * th, EccentricDrawing.BoundaryLabel, 90, 0, 0);
            if (f.Eccentric.Sides.Contains("phải")) w.Text(Lay.Chu, L + 0.6 * th, B + 1.2 * th, 0.8 * th, EccentricDrawing.BoundaryLabel, 90, 0, 2);
            if (f.Eccentric.Sides.Contains("trước")) w.Text(Lay.Chu, 0, -1.0 * th, 0.8 * th, EccentricDrawing.BoundaryLabel, 0, 0, 2);
            if (f.Eccentric.Sides.Contains("sau")) w.Text(Lay.Chu, 0, B + 1.0 * th, 0.8 * th, EccentricDrawing.BoundaryLabel, 0, 0, 0);
            foreach (var st in f.Eccentric.Straps.Where(x => x.NodeA == f.NodeId).Take(2))   // đầu dầm cân bằng
            {
                double hb = 150 * k, dir = st.AlongX ? Math.Sign(st.XB - st.XA) : Math.Sign(st.YB - st.YA);
                if (st.AlongX) { double x0 = dir > 0 ? L : 0; w.Rect(Lay.BeTong, Math.Min(x0, x0 + dir * 3 * th), yc - hb, Math.Max(x0, x0 + dir * 3 * th), yc + hb, "DASHED"); w.Text(Lay.Chu, x0 + dir * 1.5 * th, yc + hb + 0.3 * th, 0.75 * th, "DẦM CÂN BẰNG", 0, 1, 0); }
                else { double y0 = dir > 0 ? B : 0; w.Rect(Lay.BeTong, xc - hb, Math.Min(y0, y0 + dir * 3 * th), xc + hb, Math.Max(y0, y0 + dir * 3 * th), "DASHED"); w.Text(Lay.Chu, xc + hb + 0.3 * th, y0 + dir * 1.5 * th, 0.75 * th, "DẦM CÂN BẰNG", 90, 1, 0); }
            }
        }
        w.Title(L / 2, w.Extents.Y2 + 4 * th, $"{f.Name}{(f.Eccentric != null && (Math.Abs(ex) > 1e-6 || Math.Abs(ey) > 1e-6) ? " – MÓNG LỆCH TÂM" : "")} (SL={Math.Max(1, qty)})", DrawUtil.ScaleText(det));
        return w;
    }

    /// <summary>Mặt cắt móng theo phương L: bê tông lót, đế móng (vát nếu Hb &lt; H), cổ cột, thép đáy.</summary>
    public static DxfWriter DetailSection(Project p, FootingDesign f, double S)
    {
        var w = new DxfWriter(S);
        double det = CurrentDetailScale;
        double th = w.Th, k = S / det;
        double L = f.L * 1000 * k, H = Math.Max(0.2, f.H) * 1000 * k;
        double Hb = (f.Hb > 0 && f.Hb < f.H ? f.Hb : f.H) * 1000 * k;
        double cb = (f.ColumnH > 0 ? f.ColumnH : 0.22) * 1000 * k;
        double c = Math.Max(30, p.Concrete.CoverFooting * 1000) * k;
        double lot = 100 * k;
        double neck = Math.Max(300, (f.Depth > 0 ? f.Depth - f.H : 0.5) * 1000) * k;
        double exS = (f.Eccentric?.OffsetX ?? 0) * 1000 * k;   // [MóngLệchTâm] tim cột tại L/2 − ex
        double xc1 = L / 2 - exS - cb / 2, xc2 = L / 2 - exS + cb / 2, ped = Math.Abs(exS) > 1e-6 ? 0 : 50 * k;
        // lót
        w.Rect(Lay.BeTong, -lot, -lot, L + lot, 0);
        // đế
        w.Polyline(Lay.BeTong, new List<(double X, double Y)>
        {
            (0, 0), (L, 0), (L, Hb), (xc2 + ped, H), (xc2, H), (xc2, H + neck),
        }, false);
        w.Polyline(Lay.BeTong, new List<(double X, double Y)>
        {
            (xc1, H + neck), (xc1, H), (xc1 - ped, H), (0, Hb), (0, 0)
        }, false);
        w.BreakH(Lay.BeTong, xc1, xc2, H + neck);
        w.Line(Lay.Truc, (xc1 + xc2) / 2, -lot - 2 * th, (xc1 + xc2) / 2, H + neck + 2 * th, "CENTER");
        if (Math.Abs(exS) > 1e-6)   // [MóngLệchTâm] kích thước tim cột – mép móng, đầu dầm cân bằng
        {
            w.DimChainH(new List<double> { 0, (xc1 + xc2) / 2, L }, H, H + neck + 3.5 * th, k);
            var st = f.Eccentric.Straps.FirstOrDefault(x => x.AlongX && x.NodeA == f.NodeId);
            if (st != null)
            {
                double hs = Math.Min(H, 600 * k);
                double x0 = exS > 0 ? L : 0, dir = exS > 0 ? 1 : -1;
                w.Rect(Lay.BeTong, Math.Min(x0, x0 + dir * 4 * th), H - hs, Math.Max(x0, x0 + dir * 4 * th), H, "DASHED");
                w.Text(Lay.Chu, x0 + dir * 2 * th, H + 0.4 * th, 0.75 * th, "DẦM CÂN BẰNG", 0, 1, 0);
            }
        }

        var bl = BarsDir(f, true);
        var bb = BarsDir(f, false);
        double dl = bl.Dia * k, db = bb.Dia * k;
        // thanh phương L (dài) và chấm thanh phương B
        double y1 = c + db + dl / 2;
        w.Polyline(Lay.Thep, new List<(double X, double Y)>
        {
            (c, y1 + Math.Min(Hb - 2 * c, 150 * k)), (c, y1), (L - c, y1), (L - c, y1 + Math.Min(Hb - 2 * c, 150 * k))
        }, false, 0.4 * S);
        double sB = Math.Max(50, bb.Sp * 1000) * k;
        for (double x = c + db; x <= L - c - db + 1e-6; x += sB) w.Dot(Lay.Thep, x, c + db / 2, Math.Max(db / 2, 0.15 * th));
        // thép chờ cột
        int nc = 2;
        for (int i = 0; i < nc; i++)
        {
            double x = i == 0 ? xc1 + 30 * k : xc2 - 30 * k;
            w.Polyline(Lay.Thep, new List<(double X, double Y)>
            {
                (x + (i == 0 ? -200 * k : 200 * k), y1 + dl), (x, y1 + dl), (x, H + neck)
            }, false, 0.3 * S);
        }
        string tL = DrawUtil.StirrupTag(bl.Dia, bl.Sp), tB = DrawUtil.StirrupTag(bb.Dia, bb.Sp);
        w.LeaderTag(L * 0.3, y1, L * 0.3 + 2 * th, -lot - 3.5 * th, tL, bl.Mark, 1);
        w.LeaderTag(L * 0.72, c + db / 2, L * 0.72 + 2 * th, -lot - 6.2 * th, tB, bb.Mark, 1);
        // kích thước
        w.Dim(-lot, -lot, -lot, 0, 2.5 * th + lot, k);
        w.Dim(0, 0, 0, Hb, 2.5 * th, k);
        if (H - Hb > 1) w.Dim(0, Hb, 0, H, 2.5 * th, k);
        w.Dim(0, -lot, L, -lot, -9 * th, k);
        w.Dim(xc1, H + neck, xc2, H + neck, 2 * th, k);
        // cao độ
        double zBot = p.BaseElevation - (f.H > 0 ? f.H : 0.4);
        w.ElevMark(L + 3 * th, 0, DxfWriter.ElevText(zBot), 1);
        w.ElevMark(L + 3 * th, H, DxfWriter.ElevText(p.BaseElevation), 1);
        w.Title(L / 2, w.Extents.Y1 - 1.2 * th, $"MẶT CẮT {f.Name}", DrawUtil.ScaleText(det), true);
        return w;
    }
}
