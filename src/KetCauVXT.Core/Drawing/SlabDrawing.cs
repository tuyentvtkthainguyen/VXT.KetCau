using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Mặt bằng thép sàn từng tầng: dầm đỡ (nét đứt) + cột, thép lớp dưới mỗi ô theo 2 phương,
/// thép mũ trên các cạnh ngàm (cạnh chung 2 ô vẽ MỘT thanh vắt qua dầm, số hiệu của ô thống kê thanh đó),
/// lưới trên liên tục (phương án lưới 2 lớp), tên ô sàn. Toạ độ mm.</summary>
public static class SlabDrawing
{
    /// <summary>Tỷ lệ tự chọn: 1/50 nếu mặt bằng ≤ 25 m, ngược lại 1/100.</summary>
    public static double ChooseScale(Project p)
    {
        // ---- Thiết lập bản vẽ (DrawingStyle.ScaleSlabPlan > 0: tỷ lệ cố định do người dùng chọn) ----
        double fixedScale = DrawingStyle.Current.ScaleOf(DrawingKind.SlabPlan);
        if (fixedScale > 0) return fixedScale;
        // ---- hết Thiết lập bản vẽ ----
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
            // ô gộp (Quantity > 1) chỉ giữ SlabId của ô đại diện → tra theo khoá "S:{id}"
            string key = "S:" + sl.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var d = designs.FirstOrDefault(x => x.SlabId == sl.Id) ?? designs.FirstOrDefault(x => x.RebarKeys.Contains(key));
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
        // ---- [Sàn hạ cốt] ghi "SÀN HẠ COS -50 (▽ -0.050)" dưới tên ô – Drawing/SlabDropDrawing.cs ----
        foreach (var sl in SlabDrop.Lowered(p, st.Name)) layout.Reserve(SlabDropDrawing.Note(w, p, sl));

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

        // ---- thép trên: mũ gối (cạnh chung 2 ô = MỘT thanh vắt qua dầm) và lưới trên liên tục – theo SlabDesign.EdgeBars / TopMeshes ----
        var storyDesigns = panels.Where(x => x.D != null).Select(x => x.D).Distinct().ToList();
        foreach (var d in storyDesigns)
        {
            foreach (var eb in d.EdgeBars.Where(x => x.Bar != null))
            {
                double x1 = eb.X1 * 1000, y1 = eb.Y1 * 1000, x2 = eb.X2 * 1000, y2 = eb.Y2 * 1000;
                double ex = x2 - x1, ey = y2 - y1, el = Math.Sqrt(ex * ex + ey * ey);
                if (el < 1) continue;
                double ux = ex / el, uy = ey / el, nx = eb.NX, ny = eb.NY;
                bool edgeAlongX = Math.Abs(ux) >= Math.Abs(uy);
                double l1 = eb.Ext1 * 1000, l2 = eb.Ext2 * 1000;
                string tag = eb.Bar.Tag;
                int mark = eb.Bar.Mark;
                double px = x1 + ex * 0.42, py = y1 + ey * 0.42;
                double ax = px - nx * l2, ay = py - ny * l2, qx = px + nx * l1, qy = py + ny * l1;
                double hk = Math.Max(80, 0.06 * Math.Max(l1, l2));
                w.Polyline(Lay.Thep, new List<(double X, double Y)>
                {
                    (ax - ux * hk, ay - uy * hk), (ax, ay), (qx, qy), (qx - ux * hk, qy - uy * hk)
                }, false, 0.3 * S);
                double kx, ky;
                if (edgeAlongX)
                {
                    double up = ny >= 0 ? l1 : l2;
                    kx = px + 1.5 * th; ky = py + Math.Max(up, 600) * 0.6;
                    var box = w.TagBox(kx, ky, tag, mark, 1);
                    var (_, dy) = layout.PlaceBothWays(box, 0, 1.6 * th, 15);
                    w.LeaderTag(px, py + 0.4 * up, kx, ky + dy, tag, mark, 1);
                }
                else
                {
                    double left = nx <= 0 ? l1 : l2;
                    kx = px - Math.Max(left, 600) * 0.6; ky = py + 1.5 * th;
                    var box = w.TagBox(kx, ky, tag, mark, 1, true);
                    var (dx, _) = layout.PlaceBothWays(box, 1.6 * th, 0, 15);
                    w.LeaderTag(px - 0.4 * left, py, kx + dx, ky, tag, mark, 1, true);
                }
                if (l1 > 1) w.Dim(px, py, qx, qy, edgeAlongX ? -1.5 * th : 1.5 * th);
                if (l2 > 1) w.Dim(ax, ay, px, py, edgeAlongX ? -1.5 * th : 1.5 * th);
            }
            foreach (var tm in d.TopMeshes.Where(x => x.Bar != null))
            {
                double x1 = tm.X1 * 1000, y1 = tm.Y1 * 1000, x2 = tm.X2 * 1000, y2 = tm.Y2 * 1000;
                double hk = 100;
                string tag = tm.Bar.Tag + " lớp trên";
                int mark = tm.Bar.Mark;
                if (tm.AlongX)
                {
                    double yb = y1 + (y2 - y1) * 0.78;
                    w.Polyline(Lay.Thep, new List<(double X, double Y)> { (x1 + 60, yb - hk), (x1 + 60, yb), (x2 - 60, yb), (x2 - 60, yb - hk) }, false, 0.3 * S);
                    double ax = x1 + (x2 - x1) * 0.15;
                    var box = w.TagBox(ax + 0.5 * th, yb + 1.2 * th, tag, mark, 1);
                    var (_, dy) = layout.PlaceBothWays(box, 0, 1.6 * th, 12);
                    w.LeaderTag(ax, yb, ax + 0.5 * th, yb + 1.2 * th + dy, tag, mark, 1);
                    w.Dim(x1, yb, x2, yb, -1.8 * th);
                }
                else
                {
                    double xb = x1 + (x2 - x1) * 0.22;
                    w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xb + hk, y1 + 60), (xb, y1 + 60), (xb, y2 - 60), (xb + hk, y2 - 60) }, false, 0.3 * S);
                    double ay = y1 + (y2 - y1) * 0.15;
                    var box = w.TagBox(xb - 2.0 * th, ay, tag, mark, 1, true);
                    var (dx, _) = layout.PlaceBothWays(box, -1.6 * th, 0, 12);
                    w.LeaderTag(xb, ay, xb - 2.0 * th + dx, ay, tag, mark, 1, true);
                    w.Dim(xb, y1, xb, y2, 1.8 * th);
                }
            }
        }

        string title = $"MẶT BẰNG THÉP SÀN TẦNG {st.Name} ({DxfWriter.ElevText(st.Elevation)})";
        w.Title(extent.Cx, w.Extents.Y2 + 5 * th, title, DrawUtil.ScaleText(S));
        return w;
    }
}
