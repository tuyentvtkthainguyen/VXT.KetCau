using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Mặt bằng kết cấu từng tầng (tl 1/100): lưới trục, chuỗi kích thước, cột (tô đặc),
/// dầm (2 nét theo bề rộng tiết diện), tên cấu kiện, tên ô sàn. Toạ độ mặt bằng = toạ độ mô hình × 1000 (mm).</summary>
public static class FramingPlan
{
    public const double SheetScale = 100;

    public static List<Figure> BuildAll(Project p, DesignSummary s)
    {
        var res = new List<Figure>();
        foreach (var st in p.Stories.OrderBy(x => x.Elevation))
        {
            try { res.Add(new Figure(Build(p, s, st, SheetScale), st.Name, true)); }
            catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure(st.Name, ex, SheetScale), st.Name, true)); }
        }
        return res;
    }

    public static DxfWriter Build(Project p, DesignSummary s, Story st, double S)
    {
        var w = new DxfWriter(S);
        double th = w.Th;
        var extent = DrawBase(w, p, st, true);

        // nhãn cột
        var colName = new Dictionary<int, string>();
        if (s != null) foreach (var c in s.Columns) foreach (int f in c.Frames) colName[f] = c.Name;
        foreach (var f in ColumnsBelow(p, st))
        {
            var n = p.Node(f.NodeJ);
            var si = DrawUtil.SectionOf(p, f.Section);
            string nm = colName.TryGetValue(f.Id, out var cn) ? cn : f.Label;
            double half = Math.Max(si.B, si.H) * 500;
            w.Text(Lay.Chu, n.X * 1000 + half + 0.4 * th, n.Y * 1000 + half + 0.4 * th, th, nm, 0, 0, 0);
        }

        // nhãn dầm: mỗi dầm liên tục ghi 1 lần ở phần tử giữa
        var labeled = new HashSet<int>();
        if (s != null)
        {
            foreach (var d in s.Beams.Where(b => SameStory(b.Story, st, b.TopElevation)))
            {
                var fr = d.Frames.Select(id => p.Frames.FirstOrDefault(fe => fe.Id == id)).Where(fe => fe != null).ToList();
                if (fr.Count == 0) continue;
                foreach (var f in fr) labeled.Add(f.Id);
                var mid = fr[fr.Count / 2];
                double bw = d.B > 0 ? d.B : DrawUtil.SectionOf(p, mid.Section).B;
                double hh = d.H > 0 ? d.H : DrawUtil.SectionOf(p, mid.Section).H;
                BeamLabel(w, p, mid, $"{d.Name} ({DxfWriter.N0(bw * 1000)}x{DxfWriter.N0(hh * 1000)})", bw);
            }
        }
        foreach (var f in p.Frames.Where(fe => fe.Kind == FrameKind.Beam && fe.Story == st.Name && !labeled.Contains(fe.Id)))
        {
            var si = DrawUtil.SectionOf(p, f.Section);
            BeamLabel(w, p, f, $"{f.Label} ({DxfWriter.N0(si.B * 1000)}x{DxfWriter.N0(si.H * 1000)})", si.B);
        }

        // tên ô sàn
        foreach (var sl in p.Slabs.Where(x => x.Story == st.Name))
        {
            var pts = sl.Nodes.Where(id => p.Nodes.Any(n => n.Id == id)).Select(id => p.Node(id)).ToList();
            if (pts.Count < 3) continue;
            double cx = pts.Average(n => n.X) * 1000, cy = pts.Average(n => n.Y) * 1000;
            string nm = string.IsNullOrEmpty(sl.Label) ? "S" + sl.Id : sl.Label;
            SlabNameBox(w, cx, cy, nm);
            double t = 0;
            var ss = p.SlabSections.FirstOrDefault(x => x.Name == sl.Section);
            if (ss != null) t = ss.Thickness;
            if (t > 0) w.Text(Lay.Chu, cx, cy - 1.6 * th, 0.8 * th, $"h={DxfWriter.N0(t * 1000)}", 0, 1, 3);
        }

        string title = $"MẶT BẰNG KẾT CẤU TẦNG {st.Name} ({DxfWriter.ElevText(st.Elevation)})";
        w.Title(extent.Cx, w.Extents.Y2 + 5 * th, title, DrawUtil.ScaleText(S));
        return w;
    }

    internal static bool SameStory(string storyName, Story st, double topElev)
    {
        if (!string.IsNullOrEmpty(storyName)) return storyName == st.Name;
        return Math.Abs(topElev - st.Elevation) < 1e-3;
    }

    internal static void SlabNameBox(DxfWriter w, double cx, double cy, string nm)
    {
        double th = w.Th, tw = DxfWriter.TextWidth(nm, 1.2 * th);
        w.Rect(Lay.KyHieu, cx - tw / 2 - 0.5 * th, cy - 0.5 * th, cx + tw / 2 + 0.5 * th, cy + 1.7 * th);
        w.Text(Lay.Chu, cx, cy, 1.2 * th, nm, 0, 1, 0);
    }

    private static void BeamLabel(DxfWriter w, Project p, FrameElement f, string text, double bw)
    {
        var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
        double x1 = a.X * 1000, y1 = a.Y * 1000, x2 = b.X * 1000, y2 = b.Y * 1000;
        double ang = Math.Atan2(y2 - y1, x2 - x1) * 180 / Math.PI;
        if (ang > 90.001) ang -= 180; else if (ang <= -89.999) ang += 180;
        double ar = ang * Math.PI / 180, nx = -Math.Sin(ar), ny = Math.Cos(ar);
        double off = bw * 500 + 0.4 * w.Th;
        double mx = (x1 + x2) / 2 + nx * off, my = (y1 + y2) / 2 + ny * off;
        w.Text(Lay.Chu, mx, my, w.Th, text, ang, 1, 0);
    }

    /// <summary>Các cột đỡ sàn tầng st (đỉnh cột tại cao độ tầng).</summary>
    internal static List<FrameElement> ColumnsBelow(Project p, Story st)
    {
        return p.Frames.Where(f => f.IsVerticalKind && p.Nodes.Any(n => n.Id == f.NodeJ) && p.Nodes.Any(n => n.Id == f.NodeI))
            .Where(f =>
            {
                double zt = Math.Max(p.Node(f.NodeI).Z, p.Node(f.NodeJ).Z);
                return Math.Abs(zt - st.Elevation) < 1e-3 || (f.Story == st.Name && Math.Abs(zt - st.Elevation) < 0.5);
            }).ToList();
    }

    /// <summary>Đỉnh hình chữ nhật cột trên mặt bằng (mm). Quy ước ETABS cột đứng góc 0: trục 2 ∥ X (cạnh H), trục 3 ∥ Y (cạnh B).</summary>
    internal static List<(double X, double Y)> ColumnRect(Project p, FrameElement f, double grow = 0)
    {
        var top = p.Node(f.NodeJ);
        var si = DrawUtil.SectionOf(p, f.Section);
        double hx = si.H * 500 + grow, hy = si.B * 500 + grow;
        double a = f.Angle * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
        double cx = top.X * 1000, cy = top.Y * 1000;
        var r = new List<(double X, double Y)>();
        foreach (var (u, v) in new[] { (-hx, -hy), (hx, -hy), (hx, hy), (-hx, hy) })
            r.Add((cx + u * c - v * s, cy + u * s + v * c));
        return r;
    }

    /// <summary>Vẽ nền mặt bằng: trục + đầu trục + kích thước, dầm (2 nét), cột (tô đặc nếu filled).
    /// Trả về phạm vi kết cấu (mm).</summary>
    internal static Rect2 DrawBase(DxfWriter w, Project p, Story st, bool filledColumns, bool dashedBeams = false)
    {
        double th = w.Th;
        var box = Rect2.Empty;
        var beams = st == null ? new List<FrameElement>() :
            p.Frames.Where(f => f.Kind == FrameKind.Beam && f.Story == st.Name).ToList();
        var cols = st == null ? new List<FrameElement>() : ColumnsBelow(p, st);

        foreach (var f in beams)
        {
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            double x1 = a.X * 1000, y1 = a.Y * 1000, x2 = b.X * 1000, y2 = b.Y * 1000;
            double len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            if (len < 1) continue;
            var si = DrawUtil.SectionOf(p, f.Section);
            double hw = si.B * 500, ux = (x2 - x1) / len, uy = (y2 - y1) / len, nx = -uy * hw, ny = ux * hw;
            string lt = dashedBeams ? "DASHED" : null;
            w.Line(Lay.BeTong, x1 + nx, y1 + ny, x2 + nx, y2 + ny, lt);
            w.Line(Lay.BeTong, x1 - nx, y1 - ny, x2 - nx, y2 - ny, lt);
            box = box.Union(x1, y1).Union(x2, y2);
        }
        foreach (var f in cols)
        {
            var r = ColumnRect(p, f);
            w.Polyline(Lay.BeTong, r, true);
            if (filledColumns) w.Solid(Lay.BeTong, r[0].X, r[0].Y, r[1].X, r[1].Y, r[3].X, r[3].Y, r[2].X, r[2].Y);
            foreach (var q in r) box = box.Union(q.X, q.Y);
        }
        if (st != null)
            foreach (var sl in p.Slabs.Where(x => x.Story == st.Name))
                foreach (int id in sl.Nodes)
                {
                    var n = p.Nodes.FirstOrDefault(q => q.Id == id);
                    if (n != null) box = box.Union(n.X * 1000, n.Y * 1000);
                }
        DrawGrids(w, p, ref box);
        return box;
    }

    /// <summary>Lưới trục, đầu trục (dưới + trái + trên + phải), chuỗi kích thước trục và tổng (dưới, trái).</summary>
    internal static void DrawGrids(DxfWriter w, Project p, ref Rect2 box)
    {
        double th = w.Th;
        var gx = p.GridsX.ToList();
        var gy = p.GridsY.ToList();
        foreach (var g in gx) box = box.Union(g.Coord * 1000, box.IsEmpty ? 0 : box.Cy);
        foreach (var g in gy) box = box.Union(box.IsEmpty ? 0 : box.Cx, g.Coord * 1000);
        if (box.IsEmpty) return;
        double ext = 6 * th;
        double xL = box.X1 - ext, xR = box.X2 + ext, yB = box.Y1 - ext, yT = box.Y2 + ext;
        double r = 1.5 * th;
        foreach (var g in gx)
        {
            double x = g.Coord * 1000;
            w.Line(Lay.Truc, x, yB, x, yT, "CENTER");
            w.GridBubble(x, yB - r, g.Name);
            w.GridBubble(x, yT + r, g.Name);
        }
        foreach (var g in gy)
        {
            double y = g.Coord * 1000;
            w.Line(Lay.Truc, xL, y, xR, y, "CENTER");
            w.GridBubble(xL - r, y, g.Name);
            w.GridBubble(xR + r, y, g.Name);
        }
        double yd1 = yB - 2 * r - 2.5 * th, yd2 = yd1 - 2.5 * th;
        if (gx.Count > 1)
        {
            w.DimChainH(gx.Select(g => g.Coord * 1000).ToList(), yd1, yd1);
            if (gx.Count > 2) w.Dim(gx[0].Coord * 1000, yd2, gx[^1].Coord * 1000, yd2, 0);
        }
        double xd1 = xL - 2 * r - 2.5 * th, xd2 = xd1 - 2.5 * th;
        if (gy.Count > 1)
        {
            w.DimChainV(gy.Select(g => g.Coord * 1000).ToList(), xd1, xd1);
            if (gy.Count > 2) w.Dim(xd2, gy[0].Coord * 1000, xd2, gy[^1].Coord * 1000, 0);
        }
    }
}
