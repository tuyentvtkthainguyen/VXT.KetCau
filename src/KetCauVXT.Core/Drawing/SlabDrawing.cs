using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Mặt bằng thép sàn từng tầng: dầm đỡ (nét đứt) + cột, thép lớp dưới mỗi ô theo 2 phương,
/// thép mũ trên các cạnh (cạnh chung 2 ô vẽ một thanh vắt qua dầm), tên ô sàn. Toạ độ mm.</summary>
public static class SlabDrawing
{
    /// <summary>Tỷ lệ tự chọn: 1/50 nếu mặt bằng ≤ 25 m, ngược lại 1/100.</summary>
    public static double ChooseScale(Project p)
    {
        if (p.Nodes.Count == 0) return 100;
        double dx = p.Nodes.Max(n => n.X) - p.Nodes.Min(n => n.X);
        double dy = p.Nodes.Max(n => n.Y) - p.Nodes.Min(n => n.Y);
        return Math.Max(dx, dy) <= 25 ? 50 : 100;
    }

    public static List<Figure> BuildAll(Project p, DesignSummary s, double S)
    {
        var res = new List<Figure>();
        foreach (var st in p.Stories.OrderBy(x => x.Elevation))
        {
            bool any = p.Slabs.Any(x => x.Story == st.Name) || (s?.Slabs.Any(x => x.Story == st.Name) ?? false);
            if (!any) continue;
            try { res.Add(new Figure(Build(p, s, st, S), st.Name, true)); }
            catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure(st.Name, ex, S), st.Name, true)); }
        }
        return res;
    }

    private sealed class Panel
    {
        public string Name = "";
        public List<(double X, double Y)> Pts = new();   // mm
        public SlabDesign D;
        public double X1, Y1, X2, Y2;
        public bool ShortIsX;
        public double Lshort => ShortIsX ? X2 - X1 : Y2 - Y1;
    }

    private static List<Panel> Panels(Project p, DesignSummary s, Story st)
    {
        var res = new List<Panel>();
        var designs = s?.Slabs ?? new List<SlabDesign>();
        var usedD = new HashSet<SlabDesign>();
        foreach (var sl in p.Slabs.Where(x => x.Story == st.Name))
        {
            var pts = sl.Nodes.Select(id => p.Nodes.FirstOrDefault(n => n.Id == id)).Where(n => n != null)
                .Select(n => (n.X * 1000, n.Y * 1000)).ToList();
            var d = designs.FirstOrDefault(x => x.SlabId == sl.Id);
            if (pts.Count < 3 && d != null) pts = d.Outline.Select(q => (q.x * 1000, q.y * 1000)).ToList();
            if (pts.Count < 3) continue;
            if (d != null) usedD.Add(d);
            res.Add(Make(string.IsNullOrEmpty(d?.Name) ? (string.IsNullOrEmpty(sl.Label) ? "S" + sl.Id : sl.Label) : d.Name, pts, d));
        }
        foreach (var d in designs.Where(x => !usedD.Contains(x) && x.Story == st.Name && x.Outline.Count >= 3))
            res.Add(Make(d.Name, d.Outline.Select(q => (q.x * 1000, q.y * 1000)).ToList(), d));
        return res;
    }

    private static Panel Make(string name, List<(double X, double Y)> pts, SlabDesign d)
    {
        var pn = new Panel { Name = name, Pts = pts, D = d };
        pn.X1 = pts.Min(q => q.X); pn.X2 = pts.Max(q => q.X);
        pn.Y1 = pts.Min(q => q.Y); pn.Y2 = pts.Max(q => q.Y);
        pn.ShortIsX = pn.X2 - pn.X1 <= pn.Y2 - pn.Y1;
        return pn;
    }

    private static string TagOr(string t, string def) => string.IsNullOrWhiteSpace(t) ? def : t.Trim();

    /// <summary>Số hiệu của nhãn rải: ưu tiên BarMark khớp nhãn, sau đó gán số mới chung cho cả mặt bằng.</summary>
    private sealed class MarkBook
    {
        private readonly Dictionary<string, int> _auto = new();
        public int Get(SlabDesign d, string tag, bool top, string roleHint)
        {
            if (d != null)
            {
                var c = d.Bars.Where(b => b.IsTop == top && (roleHint == null || (b.Role ?? "").Contains(roleHint))).ToList();
                int m = DrawUtil.FindMark(c, tag, top);
                if (m == 0) m = DrawUtil.FindMark(d.Bars, tag, top);
                if (m > 0) return m;
            }
            string key = (top ? "T:" : "B:") + tag;
            if (!_auto.TryGetValue(key, out int v)) { v = _auto.Count + 1; _auto[key] = v; }
            return v;
        }
    }

    public static DxfWriter Build(Project p, DesignSummary s, Story st, double S)
    {
        var w = new DxfWriter(S);
        double th = w.Th;
        var extent = FramingPlan.DrawBase(w, p, st, false, true);
        var panels = Panels(p, s, st);
        var book = new MarkBook();
        var layout = new TagLayout(0.2 * th);

        foreach (var pn in panels)
        {
            if (FramingPlan.ColumnsBelow(p, st).Count == 0 && !p.Frames.Any(f => f.Kind == FrameKind.Beam && f.Story == st.Name))
                w.Polyline(Lay.BeTong, pn.Pts, true);
            double cx = (pn.X1 + pn.X2) / 2, cy = (pn.Y1 + pn.Y2) / 2;
            FramingPlan.SlabNameBox(w, cx, cy, pn.Name);
            layout.Reserve(new Rect2(cx - 3 * th, cy - 0.6 * th, cx + 3 * th, cy + 1.8 * th));
        }

        // ---- thép lớp dưới: 1 thanh đại diện mỗi phương / ô ----
        foreach (var pn in panels)
        {
            double inset = 120;
            string tx = TagOr(pn.D?.BotX, "Ø8a200"), ty = TagOr(pn.D?.BotY, "Ø8a200");
            // phương ngắn -> BotX ; phương dài -> BotY
            string tagAlongX = pn.ShortIsX ? tx : ty;
            string tagAlongY = pn.ShortIsX ? ty : tx;
            double lx = pn.X2 - pn.X1, ly = pn.Y2 - pn.Y1;
            double hk = Math.Min(150, 0.08 * Math.Min(lx, ly));
            // thanh chạy theo X, đặt ở 1/3 chiều Y
            double yb = pn.Y1 + ly * 0.36;
            w.Polyline(Lay.Thep, new List<(double X, double Y)>
            {
                (pn.X1 + inset, yb + hk), (pn.X1 + inset, yb), (pn.X2 - inset, yb), (pn.X2 - inset, yb + hk)
            }, false, 0.3 * S);
            int mX = book.Get(pn.D, tagAlongX, false, null);
            double ax = pn.X1 + lx * 0.30;
            var boxX = w.TagBox(ax + 0.5 * th, yb - 2.2 * th, tagAlongX, mX, 1);
            var (_, dyx) = layout.PlaceBothWays(boxX, 0, -1.6 * th, 12);
            w.LeaderTag(ax, yb, ax + 0.5 * th, yb - 2.2 * th + dyx, tagAlongX, mX, 1);

            // thanh chạy theo Y, đặt ở 1/3 chiều X
            double xb = pn.X1 + lx * 0.64;
            w.Polyline(Lay.Thep, new List<(double X, double Y)>
            {
                (xb - hk, pn.Y1 + inset), (xb, pn.Y1 + inset), (xb, pn.Y2 - inset), (xb - hk, pn.Y2 - inset)
            }, false, 0.3 * S);
            int mY = book.Get(pn.D, tagAlongY, false, null);
            double ay = pn.Y1 + ly * 0.62;
            var boxY = w.TagBox(xb + 2.0 * th, ay + 0.5 * th, tagAlongY, mY, 1, true);
            var (dxy, _) = layout.PlaceBothWays(boxY, 1.6 * th, 0, 12);
            w.Line(Lay.KyHieu, xb, ay, xb + 2.0 * th + dxy, ay);
            w.Dot(Lay.KyHieu, xb, ay, 0.22 * th);
            w.LeaderTag(xb + 2.0 * th + dxy, ay, xb + 2.0 * th + dxy, ay + 0.5 * th, tagAlongY, mY, 1, true, false);
        }

        // ---- thép mũ trên cạnh ----
        var edges = new Dictionary<string, List<(Panel P, double X1, double Y1, double X2, double Y2)>>();
        foreach (var pn in panels)
        {
            for (int i = 0; i < pn.Pts.Count; i++)
            {
                var a = pn.Pts[i]; var b = pn.Pts[(i + 1) % pn.Pts.Count];
                if (Math.Abs(a.X - b.X) < 1 && Math.Abs(a.Y - b.Y) < 1) continue;
                var (p1, p2) = (a.X < b.X - 0.5 || (Math.Abs(a.X - b.X) <= 0.5 && a.Y < b.Y)) ? (a, b) : (b, a);
                string key = $"{Math.Round(p1.X)},{Math.Round(p1.Y)},{Math.Round(p2.X)},{Math.Round(p2.Y)}";
                if (!edges.TryGetValue(key, out var l)) edges[key] = l = new List<(Panel P, double X1, double Y1, double X2, double Y2)>();
                l.Add((pn, p1.X, p1.Y, p2.X, p2.Y));
            }
        }
        foreach (var kv in edges)
        {
            var e = kv.Value[0];
            double ex = e.X2 - e.X1, ey = e.Y2 - e.Y1, el = Math.Sqrt(ex * ex + ey * ey);
            if (el < 1) continue;
            double ux = ex / el, uy = ey / el, nx = -uy, ny = ux;
            bool edgeAlongX = Math.Abs(ux) >= Math.Abs(uy);
            string tag = null; int mark = 0; double lenSide1 = 0, lenSide2 = 0;
            double mx = (e.X1 + e.X2) / 2, my = (e.Y1 + e.Y2) / 2;
            foreach (var it in kv.Value)
            {
                var pn = it.P;
                // cạnh song song phương dài -> thép mũ chạy theo phương ngắn: TopX
                bool edgeParallelLong = pn.ShortIsX ? !edgeAlongX : edgeAlongX;
                string t = TagOr(edgeParallelLong ? pn.D?.TopX : pn.D?.TopY, "Ø8a200");
                if (tag == null) { tag = t; mark = book.Get(pn.D, t, true, null); }
                double ext = 0.25 * pn.Lshort;
                double pcx = (pn.X1 + pn.X2) / 2, pcy = (pn.Y1 + pn.Y2) / 2;
                double side = (pcx - mx) * nx + (pcy - my) * ny;
                if (side >= 0) lenSide1 = Math.Max(lenSide1, ext); else lenSide2 = Math.Max(lenSide2, ext);
            }
            if (tag == null) continue;
            // vị trí dọc cạnh: 0.4 chiều dài (lệch khỏi thanh lớp dưới)
            double px = e.X1 + ex * 0.42, py = e.Y1 + ey * 0.42;
            double ax = px - nx * lenSide2, ay = py - ny * lenSide2;
            double qx = px + nx * lenSide1, qy = py + ny * lenSide1;
            double hk = Math.Max(80, 0.06 * Math.Max(lenSide1, lenSide2));
            // móc chống xuống: thể hiện bằng đoạn ngắn vuông góc theo hướng cạnh
            w.Polyline(Lay.Thep, new List<(double X, double Y)>
            {
                (ax - ux * hk, ay - uy * hk), (ax, ay), (qx, qy), (qx - ux * hk, qy - uy * hk)
            }, false, 0.3 * S);
            // nhãn
            if (edgeAlongX)
            {
                double kx = px + 1.5 * th, ky = py + Math.Max(lenSide1, 600) * 0.6;
                var box = w.TagBox(kx, ky, tag, mark, 1);
                var (_, dy) = layout.PlaceBothWays(box, 0, 1.6 * th, 15);
                w.LeaderTag(px, py + 0.4 * lenSide1, kx, ky + dy, tag, mark, 1);
            }
            else
            {
                double kx = px - Math.Max(lenSide2, 600) * 0.6, ky = py + 1.5 * th;
                var box = w.TagBox(kx, ky, tag, mark, 1, true);
                var (dx, _) = layout.PlaceBothWays(box, 1.6 * th, 0, 15);
                w.LeaderTag(px - 0.4 * lenSide2, py, kx + dx, ky, tag, mark, 1, true);
            }
            // kích thước đoạn vươn
            if (lenSide1 > 1) w.Dim(px, py, qx, qy, edgeAlongX ? -1.5 * th : 1.5 * th);
            if (lenSide2 > 1) w.Dim(ax, ay, px, py, edgeAlongX ? -1.5 * th : 1.5 * th);
        }

        string title = $"MẶT BẰNG THÉP SÀN TẦNG {st.Name} ({DxfWriter.ElevText(st.Elevation)})";
        w.Title(extent.Cx, w.Extents.Y2 + 5 * th, title, DrawUtil.ScaleText(S));
        return w;
    }
}
