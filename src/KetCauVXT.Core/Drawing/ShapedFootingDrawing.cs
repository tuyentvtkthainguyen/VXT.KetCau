using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>[MóngHìnhDạng] Bản vẽ móng hình dạng (xoay / dóng mép cột / cắt theo ranh đất xiên / đa giác):
/// đường bao trên mặt bằng móng, đường chuẩn, chi tiết mặt bằng thép theo hệ trục của móng.</summary>
public static class ShapedFootingDrawing
{
    /// <summary>Đường bao móng + cột (góc xoay thật) trên mặt bằng bố trí móng.</summary>
    public static void Plan(DxfWriter w, Project p, FootingDesign f, double k, ref Rect2 box)
    {
        var pts = f.Outline.Select(v => (v[0] * 1000 * k, v[1] * 1000 * k)).ToList();
        w.Polyline(Lay.BeTong, pts, true);
        foreach (var q in pts) box = box.Union(q.Item1, q.Item2);
        var col = p.Frames.FirstOrDefault(x => x.Id == f.ColumnFrame);
        var n = p.Nodes.FirstOrDefault(x => x.Id == f.NodeId);
        if (n != null)
        {
            var cg = ShapedFooting.Column(p, col, n);
            var cc = cg.Corners().Select(q => (q.X * 1000 * k, q.Y * 1000 * k)).ToList();
            w.Polyline(Lay.BeTong, cc, true);
            w.Solid(Lay.BeTong, cc[0].Item1, cc[0].Item2, cc[1].Item1, cc[1].Item2, cc[3].Item1, cc[3].Item2, cc[2].Item1, cc[2].Item2);
        }
        double th = w.Th;
        var pn = f.StrapNode != 0 ? p.Nodes.FirstOrDefault(x => x.Id == f.StrapNode) : null;
        if (pn != null)   // dầm móng cân bằng (nét đứt hai cạnh, rộng 220)
        {
            double ax = f.X * 1000 * k, ay = f.Y * 1000 * k, bx = pn.X * 1000 * k, by = pn.Y * 1000 * k;
            double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            if (len > 1e-6)
            {
                double nx = -(by - ay) / len * 110 * k, ny = (bx - ax) / len * 110 * k;
                w.Line(Lay.BeTong, ax + nx, ay + ny, bx + nx, by + ny, "DASHED");
                w.Line(Lay.BeTong, ax - nx, ay - ny, bx - nx, by - ny, "DASHED");
                double ang = Math.Atan2(by - ay, bx - ax) * 180 / Math.PI;
                if (ang > 90) ang -= 180; else if (ang <= -90) ang += 180;
                w.Text(Lay.Chu, (ax + bx) / 2, (ay + by) / 2 + 0.5 * th, 0.75 * th, "DẦM CÂN BẰNG", ang, 1, 0);
            }
        }
        w.Text(Lay.Chu, f.Gx * 1000 * k, f.Gy * 1000 * k + 1.2 * th, th, f.Name, 0, 1, 0);
        if (Math.Abs(f.Angle) > 0.05) w.Text(Lay.Chu, f.Gx * 1000 * k, f.Gy * 1000 * k - 1.6 * th, 0.75 * th, $"α = {f.Angle:0.#}°", 0, 1, 0);
    }

    /// <summary>Đường chuẩn (ranh đất xiên) trên mặt bằng móng.</summary>
    public static void AddLotLines(DxfWriter w, Project p, double k, ref Rect2 box)
    {
        double th = w.Th;
        foreach (var l in ShapedFooting.Lines(p))
        {
            double x1 = l.X1 * 1000 * k, y1 = l.Y1 * 1000 * k, x2 = l.X2 * 1000 * k, y2 = l.Y2 * 1000 * k;
            w.Line(Lay.KyHieu, x1, y1, x2, y2, "DASHED");
            box = box.Union(x1, y1); box = box.Union(x2, y2);
            double a = l.AngleDeg;
            string name = string.IsNullOrWhiteSpace(l.Name) ? "RANH ĐẤT" : l.Name.ToUpperInvariant();
            w.Text(Lay.Chu, (x1 + x2) / 2, (y1 + y2) / 2 + 0.4 * th, 0.8 * th, name, a, 1, 0);
        }
    }

    /// <summary>Chi tiết mặt bằng thép móng hình dạng, vẽ theo hệ trục của móng (phương L nằm ngang).</summary>
    public static DxfWriter DetailPlan(Project p, FootingDesign f, int qty, double S)
    {
        var w = new DxfWriter(S);
        double det = FootingDrawing.CurrentDetailScale;
        double th = w.Th, k = S / det;
        var n = p.Nodes.FirstOrDefault(x => x.Id == f.NodeId);
        var col = p.Frames.FirstOrDefault(x => x.Id == f.ColumnFrame);
        var cg = n != null ? ShapedFooting.Column(p, col, n) : new ShapedFooting.ColumnGeo(f.X, f.Y, 0, f.ColumnH, f.ColumnB);
        double a = f.Angle * Math.PI / 180;
        var loc = ShapedFooting.ToLocal(f.Outline.Select(v => (v[0], v[1])), cg.Cx, cg.Cy, a);
        double x0 = loc.Min(q => q.X), y0 = loc.Min(q => q.Y), x1 = loc.Max(q => q.X), y1 = loc.Max(q => q.Y);
        (double, double) T(double u, double v) => ((u - x0) * 1000 * k, (v - y0) * 1000 * k);
        var poly = loc.Select(q => T(q.X, q.Y)).ToList();
        w.Polyline(Lay.BeTong, poly, true);
        var colLoc = new ShapedFooting.ColumnGeo(0, 0, cg.Angle - a, cg.D2, cg.D3);
        var cc = colLoc.Corners().Select(q => T(q.X, q.Y)).ToList();
        w.Polyline(Lay.BeTong, cc, true);
        var (xc, yc) = T(0, 0);
        double L = (x1 - x0) * 1000 * k, B = (y1 - y0) * 1000 * k;
        w.Line(Lay.Truc, xc, -3 * th, xc, B + 3 * th, "CENTER");
        w.Line(Lay.Truc, -3 * th, yc, L + 3 * th, yc, "CENTER");

        // thép cắt theo đường bao
        double c = Math.Max(0.03, p.Concrete.CoverFooting);
        var polyM = loc.Select(q => (q.X - x0, q.Y - y0)).ToList();
        int iL = 0, iB = 0;
        double sL = Math.Max(0.05, f.SpacingL), sB = Math.Max(0.05, f.SpacingB);
        var typL = new List<(double, double, double)>(); var typB = new List<(double, double, double)>();
        for (double y = c; y <= y1 - y0 - c + 1e-6; y += sL, iL++)
            foreach (var (t1, t2) in PolyGeom.Chords(polyM, 0, y, 1, 0))
                if (t2 - t1 > 2 * c + 0.2) { if (iL % 2 == 0) w.Line(Lay.Thep, (t1 + c) * 1000 * k, y * 1000 * k, (t2 - c) * 1000 * k, y * 1000 * k); typL.Add((y, t1 + c, t2 - c)); }
        for (double x = c; x <= x1 - x0 - c + 1e-6; x += sB, iB++)
            foreach (var (t1, t2) in PolyGeom.Chords(polyM, x, 0, 0, 1))
                if (t2 - t1 > 2 * c + 0.2) { if (iB % 2 == 0) w.Line(Lay.Thep, x * 1000 * k, (t1 + c) * 1000 * k, x * 1000 * k, (t2 - c) * 1000 * k); typB.Add((x, t1 + c, t2 - c)); }
        int markL = f.Bars.FirstOrDefault(b => b.Role.Contains("phương L"))?.Mark ?? 1;
        int markB = f.Bars.FirstOrDefault(b => b.Role.Contains("phương B"))?.Mark ?? 2;
        if (typL.Count > 0)
        {
            var t = typL[typL.Count / 3];
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (t.Item2 * 1000 * k, t.Item1 * 1000 * k), (t.Item3 * 1000 * k, t.Item1 * 1000 * k) }, false, 0.4 * S);
            w.LeaderTag((t.Item2 + (t.Item3 - t.Item2) * 0.3) * 1000 * k, t.Item1 * 1000 * k, L * 0.3 + 2 * th, -3.2 * th, DrawUtil.StirrupTag(f.DiaL, f.SpacingL), markL, 1);
        }
        if (typB.Count > 0)
        {
            var t = typB[typB.Count * 2 / 3];
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (t.Item1 * 1000 * k, t.Item2 * 1000 * k), (t.Item1 * 1000 * k, t.Item3 * 1000 * k) }, false, 0.4 * S);
            w.LeaderTag(t.Item1 * 1000 * k, (t.Item2 + (t.Item3 - t.Item2) * 0.7) * 1000 * k, L + 3 * th, B + 2.5 * th, DrawUtil.StirrupTag(f.DiaB, f.SpacingB), markB, 1);
        }
        // kích thước khung bao + chiều dài từng cạnh
        w.Dim(0, 0, L, 0, -6.5 * th, k);
        w.Dim(0, 0, 0, B, 3 * th, k);
        w.DimChainH(new List<double> { 0, xc, L }.Distinct().OrderBy(v => v).ToList(), 0, -9 * th, k);
        w.DimChainV(new List<double> { 0, yc, B }.Distinct().OrderBy(v => v).ToList(), 0, -6 * th, k);
        for (int i = 0; i < poly.Count; i++)
        {
            var u = poly[i]; var v = poly[(i + 1) % poly.Count];
            double len = Math.Sqrt((v.Item1 - u.Item1) * (v.Item1 - u.Item1) + (v.Item2 - u.Item2) * (v.Item2 - u.Item2)) / k;
            if (len < 50) continue;
            double ang = Math.Atan2(v.Item2 - u.Item2, v.Item1 - u.Item1) * 180 / Math.PI;
            if (ang > 90) ang -= 180; else if (ang <= -90) ang += 180;
            // chữ phía ngoài cạnh (đa giác ngược chiều kim đồng hồ: ngoài = bên phải hướng cạnh)
            double nx = (v.Item2 - u.Item2), ny = -(v.Item1 - u.Item1), nl = Math.Sqrt(nx * nx + ny * ny);
            double mx = (u.Item1 + v.Item1) / 2 + nx / Math.Max(nl, 1e-9) * 0.9 * th, my = (u.Item2 + v.Item2) / 2 + ny / Math.Max(nl, 1e-9) * 0.9 * th;
            w.Text(Lay.KichThuoc, mx, my, 0.75 * th, Math.Round(len).ToString("0", DrawUtil.Inv), ang, 1, 1);
        }
        string sub = Math.Abs(f.Angle) > 0.05 ? $" – α = {f.Angle:0.#}°" : "";
        w.Title(L / 2, w.Extents.Y2 + 4 * th, $"{f.Name} – MÓNG HÌNH DẠNG{sub} (SL={Math.Max(1, qty)})", DrawUtil.ScaleText(det));
        return w;
    }
}
