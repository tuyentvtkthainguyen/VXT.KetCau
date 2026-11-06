using KetCauVXT.Drawing;

namespace KetCauVXT.Detailing;

/// <summary>Một dòng bảng toạ độ cọc.</summary>
public sealed class PileCoordRow
{
    public string No { get; set; } = "";
    public string Cap { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double D { get; set; }
    public double Length { get; set; }
    /// <summary>Cao độ đầu cọc (sau đập đầu) và mũi cọc (m, so với cốt ±0.000 = mặt đất)</summary>
    public double HeadElev { get; set; }
    public double TipElev { get; set; }
    /// <summary>Lực nén lớn nhất lên cọc (kN)</summary>
    public double P { get; set; }
    public double Pa { get; set; }
}

/// <summary>MẶT BẰNG ĐỊNH VỊ CỌC (chế độ hồ sơ): các đài cọc đặt trên mặt bằng móng → toạ độ từng cọc P1…Pn theo hệ trục công trình
/// (tim cột + toạ độ cọc trong đài, có xoay), bảng toạ độ và bảng thống kê cọc (tên, D, chiều dài, cao độ đầu / mũi, [P], số lượng).</summary>
public static class PileLocationPlan
{
    public const string Kind = "Coc";
    public const string Title = "MẶT BẰNG ĐỊNH VỊ CỌC";

    /// <summary>Các đài cọc đặt trên mặt bằng móng (vị trí, cấu kiện).</summary>
    public static List<(PlanItem Item, ManualPileCap Cap)> Caps(DetailingProject p)
    {
        var res = new List<(PlanItem, ManualPileCap)>();
        var fnd = p?.Plan?.Foundation;
        if (fnd == null) return res;
        foreach (var it in fnd.Items.Where(i => i.Kind == PlanKinds.Footing))
        {
            var cap = p.PileCaps?.FirstOrDefault(c => c.Name == it.Member);
            if (cap != null) res.Add((it, cap));
        }
        return res;
    }

    /// <summary>Bảng toạ độ cọc (đánh số P1… theo thứ tự đài trên mặt bằng, trong đài theo Y giảm rồi X tăng).</summary>
    public static List<PileCoordRow> Rows(DetailingProject p)
    {
        var rows = new List<PileCoordRow>();
        var st = p?.Settings ?? new DetailSettings();
        int k = 0;
        foreach (var (it, cap) in Caps(p).OrderBy(c => -Math.Round(c.Item.Y1, 3)).ThenBy(c => Math.Round(c.Item.X1, 3)))
        {
            if (cap.Piles == null || cap.Piles.Count == 0) { try { cap.Run(st); } catch (Exception) { } }
            if (cap.Piles == null || cap.Piles.Count == 0) continue;
            double a = it.Rotation * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a);
            var idx = Enumerable.Range(0, cap.Piles.Count).OrderBy(i => -Math.Round(cap.Piles[i].Y, 3)).ThenBy(i => Math.Round(cap.Piles[i].X, 3)).ToList();
            foreach (int i in idx)
            {
                var q = cap.Piles[i];
                k++;
                rows.Add(new PileCoordRow
                {
                    No = "P" + k, Cap = cap.Name, X = Math.Round(it.X1 + q.X * ca - q.Y * sa, 3), Y = Math.Round(it.Y1 + q.X * sa + q.Y * ca, 3),
                    D = cap.PileD, Length = cap.PileLength, HeadElev = -cap.Df + cap.Embed, TipElev = -cap.Df - cap.PileLength,
                    P = i < cap.PileForces.Length ? cap.PileForces[i] : 0, Pa = cap.PaUsed
                });
            }
        }
        return rows;
    }

    /// <summary>Bảng thống kê cọc gộp theo (đài, D, L): tên đài, D, chiều dài, cao độ đầu / mũi, [P], số lượng.</summary>
    public static List<(string Cap, double D, double L, double Head, double Tip, double Pa, int Count)> Schedule(List<PileCoordRow> rows) =>
        rows.GroupBy(r => (r.Cap, Math.Round(r.D, 3), Math.Round(r.Length, 2)))
            .Select(g => (g.Key.Cap, g.Key.Item2, g.Key.Item3, g.First().HeadElev, g.First().TipElev, g.First().Pa, g.Count())).ToList();

    /// <summary>Hình mặt bằng định vị cọc (tỉ lệ 1/100) kèm bảng toạ độ và bảng thống kê cọc. null nếu không có đài cọc trên mặt bằng.</summary>
    public static Figure PlanFigure(DetailingProject p)
    {
        var caps = Caps(p);
        if (caps.Count == 0) return null;
        var rows = Rows(p);
        var outlines = new List<(string Name, List<(double X, double Y)> Pts)>();
        foreach (var (it, cap) in caps)
        {
            if (cap.CapL <= 0) continue;
            double a = it.Rotation * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a);
            double hx = cap.CapL / 2, hy = cap.CapB / 2;
            outlines.Add((cap.Name, new[] { (-hx, -hy), (hx, -hy), (hx, hy), (-hx, hy) }
                .Select(c => (it.X1 + (c.Item1 + cap.CapCx) * ca - (c.Item2 + cap.CapCy) * sa, it.Y1 + (c.Item1 + cap.CapCx) * sa + (c.Item2 + cap.CapCy) * ca)).ToList()));
        }
        var plan = p.Plan;
        var gx = plan.Xs.Select((v, i) => ((i + 1).ToString(), v)).ToList();
        var gy = plan.Ys.Select((v, j) => (((char)('A' + j)).ToString(), v)).ToList();
        return DrawPlan(rows, outlines, gx, gy);
    }

    /// <summary>Vẽ mặt bằng định vị cọc dùng chung cho chế độ hồ sơ và chế độ thiết kế: trục (tên, toạ độ m), đường bao đài (m), cọc + bảng.</summary>
    public static Figure DrawPlan(List<PileCoordRow> rows, List<(string Name, List<(double X, double Y)> Pts)> outlines,
        List<(string Name, double Coord)> gridX, List<(string Name, double Coord)> gridY)
    {
        double S = 100;
        var w = new DxfWriter(S);
        double minX = rows.Count > 0 ? rows.Min(r => r.X) : 0, maxX = rows.Count > 0 ? rows.Max(r => r.X) : 0;
        double minY = rows.Count > 0 ? rows.Min(r => r.Y) : 0, maxY = rows.Count > 0 ? rows.Max(r => r.Y) : 0;
        if (gridX.Count > 0) { minX = Math.Min(minX, gridX.Min(g => g.Coord)); maxX = Math.Max(maxX, gridX.Max(g => g.Coord)); }
        if (gridY.Count > 0) { minY = Math.Min(minY, gridY.Min(g => g.Coord)); maxY = Math.Max(maxY, gridY.Max(g => g.Coord)); }
        double ext = 1500, r0 = w.BubbleR;
        var gxs = gridX.OrderBy(g => g.Coord).ToList();
        var gys = gridY.OrderBy(g => g.Coord).ToList();
        for (int i = 0; i < gxs.Count; i++)
        {
            double x = gxs[i].Coord * 1000;
            w.Line(Lay.Truc, x, minY * 1000 - ext, x, maxY * 1000 + ext, "CENTER");
            w.Circle(Lay.Truc, x, maxY * 1000 + ext + r0, r0);
            w.Text(Lay.Chu, x, maxY * 1000 + ext + r0, w.Th, gxs[i].Name, 0, 1, 2);
            if (i + 1 < gxs.Count) w.Dim(x, maxY * 1000 + ext, gxs[i + 1].Coord * 1000, maxY * 1000 + ext, -0.8 * ext);
        }
        for (int j = 0; j < gys.Count; j++)
        {
            double y = gys[j].Coord * 1000;
            w.Line(Lay.Truc, minX * 1000 - ext, y, maxX * 1000 + ext, y, "CENTER");
            w.Circle(Lay.Truc, minX * 1000 - ext - r0, y, r0);
            w.Text(Lay.Chu, minX * 1000 - ext - r0, y, w.Th, gys[j].Name, 0, 1, 2);
            if (j + 1 < gys.Count) w.Dim(minX * 1000 - ext, y, minX * 1000 - ext, gys[j + 1].Coord * 1000, -0.8 * ext);
        }
        foreach (var (name, pts0) in outlines)
        {
            var pts = pts0.Select(q => (q.X * 1000, q.Y * 1000)).ToList();
            if (pts.Count < 3) continue;
            w.Polyline(Lay.BeTong, pts, true);
            w.Text(Lay.Chu, pts[3 % pts.Count].Item1, pts[3 % pts.Count].Item2 + 0.4 * w.Th, 0.8 * w.Th, name, 0, 0, 0);
        }
        foreach (var r in rows)
        {
            double x = r.X * 1000, y = r.Y * 1000, rr = Math.Max(r.D * 500, 100);
            w.Circle(Lay.Thep, x, y, rr);
            w.Line(Lay.KyHieu, x - rr * 1.3, y, x + rr * 1.3, y);
            w.Line(Lay.KyHieu, x, y - rr * 1.3, x, y + rr * 1.3);
            w.Text(Lay.Chu, x + rr + 0.2 * w.Th, y + rr + 0.2 * w.Th, 0.7 * w.Th, r.No, 0, 0, 0);
        }
        double tx = maxX * 1000 + ext + 4 * r0 + 1000, ty = maxY * 1000 + ext;
        double rowH = 1.8 * w.Th;
        double[] cw = { 6 * w.Th, 6 * w.Th, 8 * w.Th, 8 * w.Th, 7 * w.Th };
        string[] hd = { "CỌC", "ĐÀI", "X (m)", "Y (m)", "P (kN)" };
        Table(w, tx, ty, cw, rowH, "BẢNG TOẠ ĐỘ CỌC", hd,
            rows.Select(r => new[] { r.No, r.Cap, r.X.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture), r.Y.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture), DxfWriter.N0(r.P) }).ToList());
        var sch = Schedule(rows);
        double ty2 = ty - (rows.Count + 3) * rowH - 2 * rowH;
        double[] cw2 = { 6 * w.Th, 6 * w.Th, 7 * w.Th, 8 * w.Th, 8 * w.Th, 7 * w.Th, 5 * w.Th };
        Table(w, tx, ty2, cw2, rowH, "BẢNG THỐNG KÊ CỌC", new[] { "ĐÀI", "D (mm)", "L (m)", "CĐ ĐẦU", "CĐ MŨI", "[P] kN", "SL" },
            sch.Select(c => new[] { c.Cap, DxfWriter.N0(c.D * 1000), c.L.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture), DxfWriter.ElevText(c.Head), DxfWriter.ElevText(c.Tip), DxfWriter.N0(c.Pa), c.Count.ToString() }).ToList());
        w.Title((minX + maxX) / 2 * 1000, minY * 1000 - ext - 3 * r0, Title, DrawUtil.ScaleText(S));
        return new Figure(w, Title, true);
    }

    static void Table(DxfWriter w, double x0, double y0, double[] cw, double rowH, string title, string[] head, List<string[]> rows)
    {
        double W = cw.Sum();
        w.Text(Lay.Chu, x0 + W / 2, y0 + 0.5 * rowH, w.Th, title, 0, 1, 0);
        int nr = rows.Count + 1;
        for (int i = 0; i <= nr; i++) w.Line(Lay.KyHieu, x0, y0 - i * rowH, x0 + W, y0 - i * rowH);
        double x = x0;
        for (int j = 0; j <= cw.Length; j++)
        {
            w.Line(Lay.KyHieu, x, y0, x, y0 - nr * rowH);
            if (j < cw.Length) x += cw[j];
        }
        void Row(int i, string[] cells)
        {
            double cx = x0;
            for (int j = 0; j < cw.Length && j < cells.Length; j++)
            {
                w.Text(Lay.Chu, cx + cw[j] / 2, y0 - (i + 0.5) * rowH, 0.7 * w.Th, cells[j] ?? "", 0, 1, 2);
                cx += cw[j];
            }
        }
        Row(0, head);
        for (int i = 0; i < rows.Count; i++) Row(i + 1, rows[i]);
    }
}
