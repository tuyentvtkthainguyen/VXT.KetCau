using KetCauVXT.Design;
using KetCauVXT.Drawing;

namespace KetCauVXT.Detailing;

/// <summary>Bản vẽ MẶT BẰNG KẾT CẤU của chế độ Triển khai hồ sơ (tỷ lệ 1/100): mặt bằng kết cấu móng, mặt bằng định vị cột,
/// mặt bằng kết cấu từng tầng (dầm nét đứt dưới sàn, tên dầm "D1 (220x400)", tên ô sàn, lanh tô, ô văng, cầu thang),
/// lưới trục có đầu trục hai phía, chuỗi kích thước nhịp + tổng hai phía, kích thước định vị dầm / cột lệch trục.</summary>
public static class StructuralPlanDrawing
{
    public const double Scale = 100;

    /// <summary>Tiêu đề hình.</summary>
    public const string TitleFoundation = "MẶT BẰNG KẾT CẤU MÓNG";
    public const string TitleColumns = "MẶT BẰNG ĐỊNH VỊ CỘT";
    public const string TitleLevelPrefix = "MẶT BẰNG KẾT CẤU ";

    /// <summary>Các hình mặt bằng theo thứ tự: móng, định vị cột, các tầng theo cao độ. Không có lưới / vị trí → rỗng.</summary>
    public static List<Figure> Build(DetailingProject p, List<(IDetailMember Member, DetailResult Result)> res)
    {
        var figs = new List<Figure>();
        var plan = p?.Plan;
        if (plan == null || (!plan.HasGrid && plan.IsEmpty)) return figs;
        var map = new Dictionary<string, (IDetailMember M, DetailResult R)>();
        foreach (var m in p.All) if (!string.IsNullOrEmpty(m.Name) && !map.ContainsKey(m.Name)) map[m.Name] = (m, null);
        foreach (var (m, r) in res ?? new()) if (m != null && !string.IsNullOrEmpty(m.Name)) map[m.Name] = (m, r);

        var fnd = plan.Foundation;
        if (fnd != null && (fnd.Items.Count > 0 || plan.IsEmpty)) figs.Add(new Figure(Level(plan, fnd, map, TitleFoundation, false), TitleFoundation, true));
        var colLevel = fnd != null && fnd.Items.Any(i => i.Kind == PlanKinds.Column) ? fnd : plan.Levels.FirstOrDefault(l => l.Items.Any(i => i.Kind == PlanKinds.Column));
        if (colLevel != null) figs.Add(new Figure(Level(plan, colLevel, map, TitleColumns, true), TitleColumns, true));
        foreach (var lv in plan.Levels.Where(l => !l.IsFoundation).OrderBy(l => l.Elevation))
        {
            if (lv.Items.Count == 0) continue;   // mặt bằng chưa đặt cấu kiện: không xuất
            string title = TitleLevelPrefix + (lv.Name ?? "").ToUpperInvariant() + " (" + DxfWriter.ElevText(lv.Elevation) + ")";
            figs.Add(new Figure(Level(plan, lv, map, title, false), title, true));
        }
        return figs;
    }

    private static T Get<T>(Dictionary<string, (IDetailMember M, DetailResult R)> map, string name) where T : class =>
        name != null && map.TryGetValue(name, out var v) ? v.M as T : null;

    private static DetailResult Res(Dictionary<string, (IDetailMember M, DetailResult R)> map, string name) =>
        name != null && map.TryGetValue(name, out var v) ? v.R : null;

    /// <summary>Một mặt bằng. columnsOnly: mặt bằng định vị cột (chỉ cột + kích thước định vị).</summary>
    public static DxfWriter Level(StructuralPlan plan, PlanLevel lv, Dictionary<string, (IDetailMember M, DetailResult R)> map, string title, bool columnsOnly)
    {
        var w = new DxfWriter(Scale);
        double th = w.Th;
        var xs = plan.Xs.Select(v => v * 1000).ToList();
        var ys = plan.Ys.Select(v => v * 1000).ToList();
        var items = lv.Items.Where(i => !columnsOnly || i.Kind == PlanKinds.Column).ToList();

        // phạm vi
        var box = Rect2.Empty;
        foreach (var x in xs) foreach (var y in ys) box = box.Union(x, y);
        foreach (var it in items) { box = box.Union(it.X1 * 1000, it.Y1 * 1000); box = box.Union(it.X2 * 1000, it.Y2 * 1000); }
        if (box.IsEmpty) box = new Rect2(0, 0, 6000, 6000);
        double ext = 1200;   // trục vươn khỏi kết cấu
        double gx0 = box.X1 - ext, gx1 = box.X2 + ext, gy0 = box.Y1 - ext, gy1 = box.Y2 + ext;

        // lưới trục + đầu trục 2 phía
        double R = w.BubbleR;
        for (int i = 0; i < xs.Count; i++)
        {
            w.Line(Lay.Truc, xs[i], gy0, xs[i], gy1, "CENTER");
            w.GridBubble(xs[i], gy0 - R, StructuralPlan.NameX(i));
            w.GridBubble(xs[i], gy1 + R, StructuralPlan.NameX(i));
        }
        for (int j = 0; j < ys.Count; j++)
        {
            w.Line(Lay.Truc, gx0, ys[j], gx1, ys[j], "CENTER");
            w.GridBubble(gx0 - R, ys[j], StructuralPlan.NameY(j));
            w.GridBubble(gx1 + R, ys[j], StructuralPlan.NameY(j));
        }

        // cấu kiện: móng / sàn / ô văng / cầu thang trước (nằm dưới), dầm, lanh tô, cột sau cùng
        int Order(PlanItem it) => it.Kind switch { PlanKinds.Footing => 0, PlanKinds.Slab => 1, PlanKinds.Stair => 2, PlanKinds.Canopy => 3, PlanKinds.Beam => 4, PlanKinds.Lintel => 5, _ => 6 };
        var labeledCols = new HashSet<string>();
        foreach (var it in items.OrderBy(Order))
        {
            switch (it.Kind)
            {
                case PlanKinds.Footing: DrawFooting(w, it, map); break;
                case PlanKinds.Slab: DrawRectMember(w, it, map); break;
                case PlanKinds.Stair: DrawStair(w, it); break;
                case PlanKinds.Canopy: DrawCanopy(w, it); break;
                case PlanKinds.Beam: DrawBeam(w, it, map, lv.IsFoundation, plan); break;
                case PlanKinds.Lintel: DrawLintel(w, it, map); break;
                case PlanKinds.Column: DrawColumn(w, it, map, plan, columnsOnly, labeledCols); break;
            }
        }

        // kích thước: dưới + trái (nhịp, tổng); trên + phải (nhịp)
        if (xs.Count >= 2)
        {
            double yb = gy0 - 2 * R - 1.5 * th;
            w.DimChainH(xs, gy0, yb);
            w.Dim(xs[0], gy0, xs[^1], gy0, -(gy0 - (yb - 2.5 * th)));
            w.DimChainH(xs, gy1, gy1 + 2 * R + 1.5 * th);
        }
        if (ys.Count >= 2)
        {
            double xl = gx0 - 2 * R - 1.5 * th;
            w.DimChainV(ys, gx0, xl);
            w.Dim(gx0, ys[0], gx0, ys[^1], gx0 - (xl - 2.5 * th));
            w.DimChainV(ys, gx1, gx1 + 2 * R + 1.5 * th);
        }

        var e = w.Extents;
        w.Title(e.Cx, e.Y1 - 2.5 * th, title, DrawUtil.ScaleText(Scale));
        if (!columnsOnly && items.Count == 0)
            w.Text(Lay.Chu, (box.X1 + box.X2) / 2, (box.Y1 + box.Y2) / 2, th, "(chưa đặt cấu kiện – tab Mặt bằng kết cấu)", 0, 1, 2);
        return w;
    }

    // ------------------------------------------------------------------
    private static void DrawColumn(DxfWriter w, PlanItem it, Dictionary<string, (IDetailMember M, DetailResult R)> map, StructuralPlan plan, bool locate, HashSet<string> labeled)
    {
        var c = Get<ManualColumn>(map, it.Member);
        double b = (c?.B ?? 0.22) * 1000, h = (c?.H ?? 0.22) * 1000;
        if (Math.Abs(Math.Abs(it.Rotation) - 90) < 1) (b, h) = (h, b);
        double x = it.X1 * 1000, y = it.Y1 * 1000;
        w.FilledRect(Lay.BeTong, x - b / 2, y - h / 2, x + b / 2, y + h / 2);
        w.Rect(Lay.BeTong, x - b / 2, y - h / 2, x + b / 2, y + h / 2);
        double th = w.Th;
        string nm = string.IsNullOrEmpty(it.Member) ? "C?" : it.Member;
        w.Text(Lay.Chu, x + b / 2 + 0.3 * th, y + h / 2 + 0.3 * th, 0.9 * th, nm, 0, 0, 0);
        // định vị: cột lệch trục → kích thước từ trục gần nhất tới tim cột; mặt bằng định vị cột → kích thước mép cột tới trục (1 lần mỗi tên)
        var xs = plan.Xs; var ys = plan.Ys;
        double gx = StructuralPlan.Nearest(xs, it.X1) * 1000, gy = StructuralPlan.Nearest(ys, it.Y1) * 1000;
        if (xs.Length > 0 && Math.Abs(gx - x) > 5) w.Dim(gx, y - h / 2 - 1.2 * th, x, y - h / 2 - 1.2 * th, 0);
        if (ys.Length > 0 && Math.Abs(gy - y) > 5) w.Dim(x - b / 2 - 1.2 * th, gy, x - b / 2 - 1.2 * th, y, 0);
        if (locate && labeled.Add(nm + "|" + Math.Round(b) + "|" + Math.Round(h)))
        {
            if (xs.Length > 0) w.DimChainH(new List<double> { x - b / 2, gx, x + b / 2 }.Distinct().OrderBy(v => v).ToList(), y - h / 2, y - h / 2 - 2.6 * th);
            if (ys.Length > 0) w.DimChainV(new List<double> { y - h / 2, gy, y + h / 2 }.Distinct().OrderBy(v => v).ToList(), x + b / 2, x + b / 2 + 2.6 * th);
        }
    }

    private static void DrawFooting(DxfWriter w, PlanItem it, Dictionary<string, (IDetailMember M, DetailResult R)> map)
    {
        double th = w.Th, x = it.X1 * 1000, y = it.Y1 * 1000, lean = 100;
        string nm = string.IsNullOrEmpty(it.Member) ? "M?" : it.Member;
        var m = map.TryGetValue(it.Member ?? "", out var v) ? v.M : null;
        var r = v.R;
        switch (m)
        {
            case ManualPileCap pc when pc.CapL > 0:
            {
                double cx = x + pc.CapCx * 1000, cy = y + pc.CapCy * 1000, L = pc.CapL * 1000, B = pc.CapB * 1000;
                w.Rect(Lay.BeTong, cx - L / 2, cy - B / 2, cx + L / 2, cy + B / 2);
                w.Rect(Lay.KyHieu, cx - L / 2 - lean, cy - B / 2 - lean, cx + L / 2 + lean, cy + B / 2 + lean, "DASHED");
                double d = pc.PileD * 1000;
                foreach (var (px, py) in pc.Piles)
                {
                    double qx = x + px * 1000, qy = y + py * 1000;
                    if (pc.Round) w.Circle(Lay.BeTong, qx, qy, d / 2); else w.Rect(Lay.BeTong, qx - d / 2, qy - d / 2, qx + d / 2, qy + d / 2, "DASHED");
                    w.Line(Lay.KyHieu, qx - d / 2, qy - d / 2, qx + d / 2, qy + d / 2);
                    w.Line(Lay.KyHieu, qx - d / 2, qy + d / 2, qx + d / 2, qy - d / 2);
                }
                w.Text(Lay.Chu, cx - L / 2, cy + B / 2 + 0.4 * th, th, nm, 0, 0, 0);
                return;
            }
            case ManualFooting:
            {
                var f = r?.Source as FootingDesign;
                double L = (f?.L > 0 ? f.L : 1.2) * 1000, B = (f?.B > 0 ? f.B : 1.2) * 1000;
                double ex = (f?.Eccentric?.OffsetX ?? 0) * 1000, ey = (f?.Eccentric?.OffsetY ?? 0) * 1000;
                double cx = x + ex, cy = y + ey;   // tim cột tại L/2 − ex so với mép trái móng
                w.Rect(Lay.BeTong, cx - L / 2, cy - B / 2, cx + L / 2, cy + B / 2);
                w.Rect(Lay.KyHieu, cx - L / 2 - lean, cy - B / 2 - lean, cx + L / 2 + lean, cy + B / 2 + lean, "DASHED");
                w.Text(Lay.Chu, cx - L / 2, cy + B / 2 + 0.4 * th, th, nm, 0, 0, 0);
                return;
            }
            default:
                w.Rect(Lay.BeTong, x - 600, y - 600, x + 600, y + 600);
                w.Text(Lay.Chu, x - 600, y + 600 + 0.4 * th, th, nm, 0, 0, 0);
                return;
        }
    }

    private static (double Ux, double Uy, double Nx, double Ny, double Ang) Dir(PlanItem it)
    {
        double len = Math.Max(it.Length, 1e-9);
        double ux = (it.X2 - it.X1) / len, uy = (it.Y2 - it.Y1) / len;
        double ang = Math.Atan2(uy, ux) * 180 / Math.PI;
        if (ang > 90.001) ang -= 180; else if (ang <= -89.999) ang += 180;
        double ar = ang * Math.PI / 180;
        return (ux, uy, -Math.Sin(ar), Math.Cos(ar), ang);
    }

    private static void DoubleLine(DxfWriter w, PlanItem it, double width, string ltype, string layer = Lay.BeTong)
    {
        var (ux, uy, _, _, _) = Dir(it);
        double nx = -uy, ny = ux, hw = width / 2;
        double x1 = it.X1 * 1000, y1 = it.Y1 * 1000, x2 = it.X2 * 1000, y2 = it.Y2 * 1000;
        w.Line(layer, x1 + nx * hw, y1 + ny * hw, x2 + nx * hw, y2 + ny * hw, ltype);
        w.Line(layer, x1 - nx * hw, y1 - ny * hw, x2 - nx * hw, y2 - ny * hw, ltype);
    }

    private static void DrawBeam(DxfWriter w, PlanItem it, Dictionary<string, (IDetailMember M, DetailResult R)> map, bool foundation, StructuralPlan plan)
    {
        double th = w.Th;
        var m = map.TryGetValue(it.Member ?? "", out var v) ? v.M : null;
        double b = 220, h = 400;
        string label = string.IsNullOrEmpty(it.Member) ? "D?" : it.Member;
        switch (m)
        {
            case ManualBeam mb: b = mb.B * 1000; h = mb.H * 1000; label += $" ({DxfWriter.N0(b)}x{DxfWriter.N0(h)})"; break;
            case PrestressInput pr: b = pr.B * 1000; h = pr.H * 1000; label += $" ({DxfWriter.N0(b)}x{DxfWriter.N0(h)}) DƯL"; break;
            case ManualStrip st:
                double bb = (st.BUsed > 0 ? st.BUsed : st.B > 0 ? st.B : 1.0) * 1000;
                DoubleLine(w, it, bb, null);
                DoubleLine(w, it, bb + 200, "DASHED", Lay.KyHieu);
                b = st.Bs * 1000; h = (st.HUsed > 0 ? st.HUsed : st.Hs) * 1000;
                label += $" – B={DxfWriter.N0(bb)}";
                break;
        }
        // dầm dưới sàn: nét đứt; dầm móng / giằng móng: nét liền
        DoubleLine(w, it, b, foundation ? null : "DASHED");
        var (ux, uy, nx, ny, ang) = Dir(it);
        double mx = (it.X1 + it.X2) * 500 + nx * (b / 2 + 0.4 * th), my = (it.Y1 + it.Y2) * 500 + ny * (b / 2 + 0.4 * th);
        w.Text(Lay.Chu, mx, my, th, label, ang, 1, 0);
        // định vị dầm lệch trục: kích thước từ trục song song gần nhất tới tim dầm (tại 1/4 đoạn)
        bool alongX = Math.Abs(uy) < 1e-3, alongY = Math.Abs(ux) < 1e-3;
        double qx = it.X1 * 1000 + (it.X2 - it.X1) * 250, qy = it.Y1 * 1000 + (it.Y2 - it.Y1) * 250;
        if (alongX && plan.Ys.Length > 0)
        {
            double g = StructuralPlan.Nearest(plan.Ys, it.Y1) * 1000;
            if (Math.Abs(g - it.Y1 * 1000) > 5) w.Dim(qx, g, qx, it.Y1 * 1000, 0);
        }
        else if (alongY && plan.Xs.Length > 0)
        {
            double g = StructuralPlan.Nearest(plan.Xs, it.X1) * 1000;
            if (Math.Abs(g - it.X1 * 1000) > 5) w.Dim(g, qy, it.X1 * 1000, qy, 0);
        }
    }

    private static void DrawLintel(DxfWriter w, PlanItem it, Dictionary<string, (IDetailMember M, DetailResult R)> map)
    {
        double th = w.Th;
        var l = Get<ManualLintel>(map, it.Member);
        double b = (l?.Bw ?? 0.22) * 1000;
        DoubleLine(w, it, b, "DASHED");
        var (ux, uy, nx, ny, ang) = Dir(it);
        double x1 = it.X1 * 1000, y1 = it.Y1 * 1000, x2 = it.X2 * 1000, y2 = it.Y2 * 1000;
        // gạch chéo ký hiệu lanh tô trên ô cửa
        w.Line(Lay.KyHieu, x1 - nx * b / 2, y1 - ny * b / 2, x2 + nx * b / 2, y2 + ny * b / 2);
        string nm = string.IsNullOrEmpty(it.Member) ? "LT?" : it.Member;
        w.Text(Lay.Chu, (x1 + x2) / 2 - nx * (b / 2 + 0.4 * th), (y1 + y2) / 2 - ny * (b / 2 + 0.4 * th), 0.9 * th, nm, ang, 1, 3);
    }

    private static void DrawCanopy(DxfWriter w, PlanItem it)
    {
        double th = w.Th;
        double x0 = it.MinX * 1000, y0 = it.MinY * 1000, x1 = it.MaxX * 1000, y1 = it.MaxY * 1000;
        w.Rect(Lay.BeTong, x0, y0, x1, y1);
        double step = 1.2 * th;
        for (double d = -(y1 - y0); d < (x1 - x0); d += step)
        {
            double ax = Math.Max(0, d), ay = ax - d, bx = Math.Min(x1 - x0, (y1 - y0) + d), by = bx - d;
            if (bx - ax > 1) w.Line(Lay.KyHieu, x0 + ax, y0 + ay, x0 + bx, y0 + by);
        }
        w.Text(Lay.Chu, (x0 + x1) / 2, y1 + 0.4 * th, 0.9 * th, "Ô VĂNG " + (string.IsNullOrEmpty(it.Member) ? "" : "(" + it.Member + ")"), 0, 1, 0);
    }

    private static void DrawStair(DxfWriter w, PlanItem it)
    {
        double th = w.Th;
        double x0 = it.MinX * 1000, y0 = it.MinY * 1000, x1 = it.MaxX * 1000, y1 = it.MaxY * 1000;
        w.Rect(Lay.BeTong, x0, y0, x1, y1);
        w.Line(Lay.KyHieu, x0, y0, x1, y1);
        w.Line(Lay.KyHieu, x0, y1, x1, y0);
        bool alongX = x1 - x0 >= y1 - y0;
        // mũi tên lên dọc chiều dài
        if (alongX) { double ym = (y0 + y1) / 2; w.Line(Lay.KyHieu, x0 + 0.1 * (x1 - x0), ym, x1 - 0.1 * (x1 - x0), ym); w.Line(Lay.KyHieu, x1 - 0.1 * (x1 - x0), ym, x1 - 0.1 * (x1 - x0) - th, ym + 0.5 * th); w.Line(Lay.KyHieu, x1 - 0.1 * (x1 - x0), ym, x1 - 0.1 * (x1 - x0) - th, ym - 0.5 * th); }
        else { double xm = (x0 + x1) / 2; w.Line(Lay.KyHieu, xm, y0 + 0.1 * (y1 - y0), xm, y1 - 0.1 * (y1 - y0)); w.Line(Lay.KyHieu, xm, y1 - 0.1 * (y1 - y0), xm - 0.5 * th, y1 - 0.1 * (y1 - y0) - th); w.Line(Lay.KyHieu, xm, y1 - 0.1 * (y1 - y0), xm + 0.5 * th, y1 - 0.1 * (y1 - y0) - th); }
        string nm = string.IsNullOrEmpty(it.Member) ? "CT?" : it.Member;
        FramingPlan.SlabNameBox(w, (x0 + x1) / 2, (y0 + y1) / 2 + 0.8 * th, nm);
        w.Text(Lay.Chu, (x0 + x1) / 2, (y0 + y1) / 2 - 0.8 * th, 0.8 * th, "CẦU THANG – LÊN", 0, 1, 3);
    }

    private static void DrawRectMember(DxfWriter w, PlanItem it, Dictionary<string, (IDetailMember M, DetailResult R)> map)
    {
        double th = w.Th;
        double x0 = it.MinX * 1000, y0 = it.MinY * 1000, x1 = it.MaxX * 1000, y1 = it.MaxY * 1000;
        double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
        var m = map.TryGetValue(it.Member ?? "", out var v) ? v.M : null;
        string nm = string.IsNullOrEmpty(it.Member) ? "S?" : it.Member;
        switch (m)
        {
            case RaftInput rf:
                w.Rect(Lay.BeTong, x0, y0, x1, y1);
                w.Rect(Lay.KyHieu, x0 - 100, y0 - 100, x1 + 100, y1 + 100, "DASHED");
                FramingPlan.SlabNameBox(w, cx, cy, nm);
                w.Text(Lay.Chu, cx, cy - 1.6 * th, 0.8 * th, "MÓNG BÈ h=" + DxfWriter.N0(rf.T * 1000), 0, 1, 3);
                return;
            case WaterTankInput:
                w.Rect(Lay.BeTong, x0, y0, x1, y1);
                FramingPlan.SlabNameBox(w, cx, cy, nm);
                w.Text(Lay.Chu, cx, cy - 1.6 * th, 0.8 * th, "BỂ NƯỚC", 0, 1, 3);
                return;
        }
        double t = m switch { ManualSlab s => s.T, ManualSlabPlan sp => sp.T, _ => 0 };
        FramingPlan.SlabNameBox(w, cx, cy, nm);
        if (t > 0) w.Text(Lay.Chu, cx, cy - 1.6 * th, 0.8 * th, "h=" + DxfWriter.N0(t * 1000), 0, 1, 3);
    }
}
