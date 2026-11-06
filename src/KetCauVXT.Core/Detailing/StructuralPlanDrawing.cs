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
        var map = Map(p, res);
        var st = p.Settings ?? new DetailSettings();

        var fnd = plan.Foundation;
        if (fnd != null && (fnd.Items.Count > 0 || plan.IsEmpty))
        {
            figs.Add(new Figure(Level(plan, fnd, map, TitleFoundation, false), TitleFoundation, true));
            figs.AddRange(SectionFigures(plan, fnd, map, st));
        }
        var colLevel = fnd != null && fnd.Items.Any(i => i.Kind == PlanKinds.Column) ? fnd : plan.Levels.FirstOrDefault(l => l.Items.Any(i => i.Kind == PlanKinds.Column));
        if (colLevel != null) figs.Add(new Figure(Level(plan, colLevel, map, TitleColumns, true), TitleColumns, true));
        foreach (var lv in plan.Levels.Where(l => !l.IsFoundation).OrderBy(l => l.Elevation))
        {
            if (lv.Items.Count == 0) continue;   // mặt bằng chưa đặt cấu kiện: không xuất
            string title = LevelTitle(lv);
            figs.Add(new Figure(Level(plan, lv, map, title, false), title, true));
            figs.AddRange(SectionFigures(plan, lv, map, st));
        }
        return figs;
    }

    /// <summary>Tiêu đề mặt bằng kết cấu tầng: "MẶT BẰNG KẾT CẤU TẦNG 2 (+3.600)".</summary>
    public static string LevelTitle(PlanLevel lv) => TitleLevelPrefix + (lv?.Name ?? "").ToUpperInvariant() + " (" + DxfWriter.ElevText(lv?.Elevation ?? 0) + ")";

    /// <summary>Tên cấu kiện → (cấu kiện, kết quả).</summary>
    public static Dictionary<string, (IDetailMember M, DetailResult R)> Map(DetailingProject p, List<(IDetailMember Member, DetailResult Result)> res)
    {
        var map = new Dictionary<string, (IDetailMember M, DetailResult R)>();
        foreach (var m in p?.All ?? Enumerable.Empty<IDetailMember>()) if (!string.IsNullOrEmpty(m.Name) && !map.ContainsKey(m.Name)) map[m.Name] = (m, null);
        foreach (var (m, r) in res ?? new()) if (m != null && !string.IsNullOrEmpty(m.Name)) map[m.Name] = (m, r);
        return map;
    }

    /// <summary>Hình xem trước cho một nút mặt bằng của cây thư mục (StructuralPlan.GroupForTree): mặt bằng + các mặt cắt thép sàn của mặt bằng đó.</summary>
    public static List<Figure> ForGroup(DetailingProject p, List<(IDetailMember Member, DetailResult Result)> res, PlanTreeGroup g)
    {
        var figs = new List<Figure>();
        var plan = p?.Plan;
        if (plan == null || g == null) return figs;
        var map = Map(p, res);
        var st = p.Settings ?? new DetailSettings();
        switch (g.Kind)
        {
            case "Mong":
            case "DamMong":
                if (g.Level != null) { figs.Add(new Figure(Level(plan, g.Level, map, TitleFoundation, false), TitleFoundation, true)); figs.AddRange(SectionFigures(plan, g.Level, map, st)); }
                break;
            case "Cot":
            {
                var fnd = plan.Foundation;
                var colLevel = fnd != null && fnd.Items.Any(i => i.Kind == PlanKinds.Column) ? fnd : plan.Levels.FirstOrDefault(l => l.Items.Any(i => i.Kind == PlanKinds.Column));
                if (colLevel != null) figs.Add(new Figure(Level(plan, colLevel, map, TitleColumns, true), TitleColumns, true));
                break;
            }
            case StructuralPlan.LintelGroupKind:
            {
                var lf = LintelPlanDrawing.PlanFigure(plan, g.Level, map);
                if (lf != null) figs.Add(lf);
                break;
            }
            case "Tang":
                if (g.Level != null)
                {
                    string title = LevelTitle(g.Level);
                    figs.Add(new Figure(Level(plan, g.Level, map, title, false), title, true));
                    figs.AddRange(SectionFigures(plan, g.Level, map, st));
                }
                break;
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
        // kích thước nằm TRONG phạm vi đầu trục: kết cấu → chuỗi nhịp → tổng → đầu trục (bong bóng ở ngoài cùng)
        double dIn = 3.5 * th, dTot = 6 * th, axisOut = 8 * th;
        double gx0 = box.X1 - axisOut, gx1 = box.X2 + axisOut, gy0 = box.Y1 - axisOut, gy1 = box.Y2 + axisOut;

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
        // vùng chiếm chỗ: thân cột + nhãn cột, thân dầm (trục song song) giữ trước ⇒ nhãn dầm, lanh tô, ô văng, thép sàn tránh đè
        var tags = new TagLayout(0.25 * th);
        foreach (var it in items)
        {
            if (it.Kind == PlanKinds.Column)
            {
                var (cb, ch) = ColumnSize(it, map);
                double x = it.X1 * 1000, y = it.Y1 * 1000;
                tags.Reserve(new Rect2(x - cb / 2, y - ch / 2, x + cb / 2, y + ch / 2));
                tags.Reserve(w.TextBox(x + cb / 2 + 0.3 * th, y + ch / 2 + 0.3 * th, 0.9 * th, string.IsNullOrEmpty(it.Member) ? "C?" : it.Member, 0, 0, 0));
            }
            else if (it.Kind == PlanKinds.Beam || it.Kind == PlanKinds.Lintel)
            {
                double bw = it.Kind == PlanKinds.Beam ? BeamWidth(it, map) : (Get<ManualLintel>(map, it.Member)?.Bw ?? 0.22) * 1000;
                double x1 = it.X1 * 1000, y1 = it.Y1 * 1000, x2 = it.X2 * 1000, y2 = it.Y2 * 1000;
                if (Math.Abs(y2 - y1) < 1) tags.Reserve(new Rect2(Math.Min(x1, x2), y1 - bw / 2, Math.Max(x1, x2), y1 + bw / 2));
                else if (Math.Abs(x2 - x1) < 1) tags.Reserve(new Rect2(x1 - bw / 2, Math.Min(y1, y2), x1 + bw / 2, Math.Max(y1, y2)));
            }
        }
        _tags = tags;
        var prevBarTags = PlanBarDraw.Tags;
        PlanBarDraw.Tags = tags;
        try
        {
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
        }
        finally { _tags = null; PlanBarDraw.Tags = prevBarTags; }

        // đường cắt mặt cắt thép sàn
        if (!columnsOnly)
            foreach (var sm in (plan.Sections ?? new()).Where(q => q.Level == lv.Name && q.Length > 1e-6))
                DrawSectionMark(w, sm);
        if (!columnsOnly) CalloutDrawing.DrawOnPlan(w, plan, lv, map);   // chi tiết trích + vạch cắt dầm (Detailing/DetailCallout.cs)
        if (!columnsOnly) PenetrationPlan.Draw(w, lv, map);   // lỗ xuyên dầm / sàn LX, LS (Detailing/Penetrations.cs)

        // kích thước: dưới + trái (nhịp, tổng); trên + phải (nhịp)
        if (xs.Count >= 2)
        {
            double yc = box.Y1 - dIn, yt = box.Y1 - dTot;
            w.DimChainH(xs, yc, yc);
            if (xs.Count > 2) w.Dim(xs[0], yt, xs[^1], yt, 0);
            w.DimChainH(xs, box.Y2 + dIn, box.Y2 + dIn);
        }
        if (ys.Count >= 2)
        {
            double xc = box.X1 - dIn, xt = box.X1 - dTot;
            w.DimChainV(ys, xc, xc);
            if (ys.Count > 2) w.Dim(xt, ys[0], xt, ys[^1], 0);
            w.DimChainV(ys, box.X2 + dIn, box.X2 + dIn);
        }

        var e = w.Extents;
        w.Title(e.Cx, e.Y1 - 2.5 * th, title, DrawUtil.ScaleText(Scale));
        if (!columnsOnly && items.Count == 0)
            w.Text(Lay.Chu, (box.X1 + box.X2) / 2, (box.Y1 + box.Y2) / 2, th, "(chưa đặt cấu kiện – tab Mặt bằng kết cấu)", 0, 1, 2);
        return w;
    }

    // ------------------------------------------------------------------
    /// <summary>Vùng chiếm chỗ của mặt bằng đang vẽ (nhãn tránh nhau); null ngoài hàm Level.</summary>
    [ThreadStatic] private static TagLayout _tags;

    private static (double B, double H) ColumnSize(PlanItem it, Dictionary<string, (IDetailMember M, DetailResult R)> map)
    {
        var c = Get<ManualColumn>(map, it.Member);
        double b = (c?.B ?? 0.22) * 1000, h = (c?.H ?? 0.22) * 1000;
        if (Math.Abs(Math.Abs(it.Rotation) - 90) < 1) (b, h) = (h, b);
        return (b, h);
    }

    private static double BeamWidth(PlanItem it, Dictionary<string, (IDetailMember M, DetailResult R)> map)
    {
        var m = map.TryGetValue(it.Member ?? "", out var v) ? v.M : null;
        return m switch { ManualBeam mb => mb.B * 1000, PrestressInput pr => pr.B * 1000, ManualStrip st => (st.BUsed > 0 ? st.BUsed : st.B > 0 ? st.B : 1.0) * 1000, _ => 220 };
    }

    /// <summary>Đặt nhãn dọc cấu kiện thẳng: thử giữa đoạn rồi dời dọc (±6 %, ±12 %… chiều dài), rồi phía bên kia; giữ chỗ nhãn đã đặt.
    /// side0 = +1: phía pháp tuyến n (chữ trên đường), −1: phía ngược (chữ treo dưới).</summary>
    private static void LabelAlong(DxfWriter w, PlanItem it, double off, double h, string text, int side0)
    {
        var (ux, uy, nx, ny, ang) = Dir(it);
        double len = it.Length * 1000;
        double cx = (it.X1 + it.X2) * 500, cy = (it.Y1 + it.Y2) * 500;
        (double X, double Y, int Va) At(int side, double t) => (cx + ux * t + nx * side * off, cy + uy * t + ny * side * off, side > 0 ? 0 : 3);
        var tags = _tags;
        if (tags != null)
            foreach (int side in new[] { side0, -side0 })
                foreach (double f in new[] { 0.0, 0.06, -0.06, 0.12, -0.12, 0.18, -0.18, 0.26, -0.26, 0.34, -0.34 })
                {
                    var (x, y, va) = At(side, f * len);
                    var box = w.TextBox(x, y, h, text, ang, 1, va);
                    if (!tags.IsFree(box)) continue;
                    tags.Reserve(box);
                    w.Text(Lay.Chu, x, y, h, text, ang, 1, va);
                    return;
                }
        var (x0, y0, va0) = At(side0, 0);
        tags?.Reserve(w.TextBox(x0, y0, h, text, ang, 1, va0));
        w.Text(Lay.Chu, x0, y0, h, text, ang, 1, va0);
    }

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
        LabelAlong(w, it, b / 2 + 0.4 * th, th, label, 1);
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
        LabelAlong(w, it, b / 2 + 0.4 * th, 0.9 * th, nm, -1);
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
        string cap = "Ô VĂNG " + (string.IsNullOrEmpty(it.Member) ? "" : "(" + it.Member + ")");
        // nhãn ô văng: phía ngoài ô văng (trên / dưới / giữa ô) – chọn chỗ không đè dầm, nhãn khác
        var cands = new[] { ((x0 + x1) / 2, y1 + 0.4 * th, 0), ((x0 + x1) / 2, y0 - 0.4 * th, 3), ((x0 + x1) / 2, (y0 + y1) / 2, 2), (x1 + 0.6 * th, (y0 + y1) / 2, 2) };
        int pick = 0;
        if (_tags != null)
            for (int i = 0; i < cands.Length; i++)
            {
                var c0 = cands[i];
                var bx = w.TextBox(c0.Item1, c0.Item2, 0.9 * th, cap, 0, i == 3 ? 0 : 1, c0.Item3);
                if (_tags.IsFree(bx)) { pick = i; break; }
            }
        var cc = cands[pick];
        _tags?.Reserve(w.TextBox(cc.Item1, cc.Item2, 0.9 * th, cap, 0, pick == 3 ? 0 : 1, cc.Item3));
        w.Text(Lay.Chu, cc.Item1, cc.Item2, 0.9 * th, cap, 0, pick == 3 ? 0 : 1, cc.Item3);
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
        // ô tên sàn + chiều dày: giữ chỗ để thép sàn trên mặt bằng không đè
        _tags?.Reserve(new Rect2(cx - 2.2 * th - 0.35 * th * nm.Length, cy - 2.8 * th, cx + 2.2 * th + 0.35 * th * nm.Length, cy + 1.6 * th));
        if (t > 0) w.Text(Lay.Chu, cx, cy - 1.6 * th, 0.8 * th, "h=" + DxfWriter.N0(t * 1000), 0, 1, 3);
        if (m is ManualSlab sl) DrawSlabBars(w, x0, y0, x1, y1, sl, v.R);
        else if (m is ManualSlabPlan spl) w.Text(Lay.Chu, cx, cy - 2.8 * th, 0.8 * th, "THÉP SÀN XEM MẶT BẰNG THÉP " + (spl.Name ?? "").ToUpperInvariant(), 0, 1, 3);
    }

    /// <summary>Thép sàn trên mặt bằng kết cấu tầng (kiểu PlanBarDraw): lớp dưới mỗi phương 1 thanh đại diện lệch khỏi tâm ô
    /// (tránh ô tên sàn ở tâm, tên dầm ở giữa cạnh), thép mũ ở các cạnh ngàm đặt tại 0,38 chiều dài cạnh.</summary>
    public static void DrawSlabBars(DxfWriter w, double x0, double y0, double x1, double y1, ManualSlab sl, DetailResult r)
    {
        if (r == null || r.Bars.Count == 0 || x1 - x0 < 1 || y1 - y0 < 1) return;
        double th = w.Th, W = x1 - x0, H = y1 - y0;
        bool shortX = W <= H + 1e-6;
        double Ls = Math.Min(W, H);
        BarMark Role(string role) => r.Bars.FirstOrDefault(b => b.Role == role);
        var bS = Role("Lớp dưới phương ngắn"); var bL = Role("Lớp dưới phương dài");
        var tL = Role("Mũ gối cạnh dài"); var tS = Role("Mũ gối cạnh ngắn");
        double bw = (sl.BeamWidth > 0 ? sl.BeamWidth : 0.22) * 1000, cv = 15;

        void Bottom(BarMark b, bool horiz, double posFrac, double textAt, double rangeAt)
        {
            if (b == null) return;
            double a = (horiz ? x0 : y0) - bw / 2 + cv, e = (horiz ? x1 : y1) + bw / 2 - cv;
            double lo = horiz ? y0 : x0, hi = horiz ? y1 : x1;
            PlanBarDraw.Bar(w, horiz, a, e, lo + (hi - lo) * posFrac, Dt.Spaced(b.Dia, b.Spacing), b.Mark,
                textAt: textAt, range: (lo + 50, hi - 50), rangeAt: rangeAt);
        }
        // lớp dưới: phương ngắn lệch 0,62 bề rộng, phương dài lệch 0,25 (không trùng tâm ô)
        Bottom(bS, shortX, 0.62, 0.70, 0.88);
        Bottom(bL, !shortX, 0.25, 0.30, 0.88);

        // mũ gối: thanh vuông góc cạnh, vươn Ls/4 vào ô, phần còn lại qua dầm; móc đứng ở đầu trong
        void Top(BarMark b, bool horiz, int edges, double posFrac)
        {
            if (b == null || edges <= 0) return;
            double ext = b.Segments.Count > 0 ? b.Segments[0] : Ls / 4 + bw / 2;
            double inner = Ls / 4, outer = Math.Max(ext - inner, bw / 2);
            double leg = Math.Max(b.Segments.Count > 1 ? b.Segments[1] : 70, 0.6 * th);
            double lo = horiz ? y0 : x0, hi = horiz ? y1 : x1;
            double e0 = horiz ? x0 : y0, e1 = horiz ? x1 : y1;
            double pos = lo + (hi - lo) * posFrac;
            string txt = Dt.Spaced(b.Dia, b.Spacing);
            PlanBarDraw.Bar(w, horiz, e0 - outer, e0 + inner, pos, txt, b.Mark, hookB: leg, hookSide: -1, textAt: 0.62,
                range: (lo + 50, hi - 50), rangeAt: 0.80);
            if (edges >= 2)
                PlanBarDraw.Bar(w, horiz, e1 + outer, e1 - inner, pos, txt, b.Mark, hookB: leg, hookSide: -1, textAt: 0.62);
        }
        // cạnh dài song song phương dài → thanh mũ chạy theo phương ngắn
        Top(tL, shortX, Math.Max(sl.FixedLongEdges, tL != null ? 1 : 0), 0.38);
        Top(tS, !shortX, Math.Max(sl.FixedShortEdges, tS != null ? 1 : 0), 0.62);
    }

    /// <summary>Ký hiệu đường cắt trên mặt bằng: nét đậm ngắn ở hai đầu, nét chấm gạch mảnh ở giữa, mũi tên hướng nhìn và tên "1".</summary>
    public static void DrawSectionMark(DxfWriter w, SectionMark sm)
    {
        double th = w.Th;
        double x1 = sm.X1 * 1000, y1 = sm.Y1 * 1000, x2 = sm.X2 * 1000, y2 = sm.Y2 * 1000;
        double len = Math.Max(Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1)), 1e-6);
        double ux = (x2 - x1) / len, uy = (y2 - y1) / len, nx = -uy, ny = ux;
        double k = Math.Min(1.4 * th, len / 3);
        w.Polyline(Lay.KyHieu, new List<(double, double)> { (x1, y1), (x1 + ux * k, y1 + uy * k) }, false, 0.6 * w.Scale);
        w.Polyline(Lay.KyHieu, new List<(double, double)> { (x2, y2), (x2 - ux * k, y2 - uy * k) }, false, 0.6 * w.Scale);
        w.Line(Lay.KyHieu, x1 + ux * k, y1 + uy * k, x2 - ux * k, y2 - uy * k, "CENTER");
        foreach (var (px, py) in new[] { (x1, y1), (x2, y2) })
        {
            double ax = px + nx * 1.2 * th, ay = py + ny * 1.2 * th;
            w.Line(Lay.KyHieu, px, py, ax, ay);
            w.Line(Lay.KyHieu, ax, ay, ax - nx * 0.45 * th + ux * 0.22 * th, ay - ny * 0.45 * th + uy * 0.22 * th);
            w.Line(Lay.KyHieu, ax, ay, ax - nx * 0.45 * th - ux * 0.22 * th, ay - ny * 0.45 * th - uy * 0.22 * th);
            w.Text(Lay.Chu, px + nx * 2.0 * th, py + ny * 2.0 * th, 1.2 * th, sm.Tag, 0, 1, 2);
        }
    }

    /// <summary>Các mặt cắt thép sàn của một mặt bằng (tỷ lệ 1/25), mỗi đường cắt một hình "MẶT CẮT 1-1 (tl 1/25)".</summary>
    public static List<Figure> SectionFigures(StructuralPlan plan, PlanLevel lv, Dictionary<string, (IDetailMember M, DetailResult R)> map, DetailSettings st)
    {
        var figs = new List<Figure>();
        if (plan == null || lv == null) return figs;
        foreach (var sm in (plan.Sections ?? new()).Where(q => q.Level == lv.Name && q.Length > 1e-6))
            figs.Add(new Figure(SlabSection(plan, lv, sm, map, st), SectionTitle(sm), false));
        figs.AddRange(CalloutDrawing.Figures(plan, lv, map, st));   // chi tiết trích (khoanh vùng) của mặt bằng
        return figs;
    }

    public const double SectionScale = 25;
    public static string SectionTitle(SectionMark sm) => "MẶT CẮT " + sm.Name + " (tl 1/" + SectionScale.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + ")";

    /// <summary>Mặt cắt thép sàn theo đường cắt người dùng chọn: các ô sàn bị cắt (chiều dày, thép lớp dưới dạng thanh / chấm,
    /// thép mũ ở mép ô), các dầm bị cắt (b × h), kích thước trục dầm và chiều dày sàn.</summary>
    public static DxfWriter SlabSection(StructuralPlan plan, PlanLevel lv, SectionMark sm, Dictionary<string, (IDetailMember M, DetailResult R)> map, DetailSettings st)
    {
        var w = new DxfWriter(SectionScale);
        double th = w.Th, cv = (st?.CoverSlab ?? 0.015) * 1000;
        double ax = sm.X1, ay = sm.Y1, L = sm.Length;
        double ux = (sm.X2 - ax) / L, uy = (sm.Y2 - ay) / L;
        bool cutAlongX = Math.Abs(ux) >= Math.Abs(uy);

        // ô sàn bị cắt: khoảng [t0, t1] (m dọc đường cắt)
        var slabs = new List<(double T0, double T1, double Th, IDetailMember M, DetailResult R, PlanItem It)>();
        foreach (var it in lv.Items.Where(i => i.Kind == PlanKinds.Slab))
        {
            if (!ClipRect(ax, ay, ux, uy, L, it.MinX, it.MinY, it.MaxX, it.MaxY, out double t0, out double t1)) continue;
            var mm = map.TryGetValue(it.Member ?? "", out var v) ? v.M : null;
            double t = mm switch { ManualSlab s1 => s1.T, ManualSlabPlan s2 => s2.T, RaftInput rf => rf.T, _ => 0.10 } * 1000;
            slabs.Add((t0 * 1000, t1 * 1000, t, mm, v.R, it));
        }
        // dầm bị cắt
        var beams = new List<(double U, double B, double H, string Name)>();
        foreach (var it in lv.Items.Where(i => i.Kind == PlanKinds.Beam || i.Kind == PlanKinds.Lintel))
        {
            double bx = it.X2 - it.X1, by = it.Y2 - it.Y1, bl = it.Length;
            if (bl < 1e-9) continue;
            double cr = ux * by - uy * bx;
            if (Math.Abs(cr) < 1e-6 * bl) continue;   // song song
            double qx = it.X1 - ax, qy = it.Y1 - ay;
            double tc = (qx * by - qy * bx) / cr, sb = (qx * uy - qy * ux) / cr;
            if (tc < -1e-6 || tc > L + 1e-6 || sb < -1e-6 || sb > 1 + 1e-6) continue;
            var mm = map.TryGetValue(it.Member ?? "", out var v) ? v.M : null;
            double b = mm switch { ManualBeam mb => mb.B, ManualLintel ml => ml.Bw, _ => 0.22 };
            double h = mm switch { ManualBeam mb => mb.H, _ => it.Kind == PlanKinds.Lintel ? 0.20 : 0.40 };
            double sinA = Math.Abs(cr) / bl;
            beams.Add((tc * 1000, b * 1000 / Math.Max(sinA, 0.2), h * 1000, string.IsNullOrEmpty(it.Member) ? "D?" : it.Member));
        }
        beams = beams.GroupBy(q => Math.Round(q.U)).Select(g => g.First()).OrderBy(q => q.U).ToList();

        if (slabs.Count == 0 && beams.Count == 0)
        {
            w.Text(Lay.Chu, L * 500, 0, th, "(đường cắt " + sm.Name + " không qua ô sàn / dầm nào)", 0, 1, 2);
            w.Title(L * 500, -3 * th, "MẶT CẮT " + sm.Name, DrawUtil.ScaleText(SectionScale), true);
            return w;
        }
        double SlabT(double u) { foreach (var q in slabs) if (u >= q.T0 - 1 && u <= q.T1 + 1) return q.Th; return 0; }
        bool InBeam(double u) => beams.Any(q => Math.Abs(u - q.U) < q.B / 2 - 0.5);

        double uMin = Math.Min(slabs.Count > 0 ? slabs.Min(q => q.T0) : double.MaxValue, beams.Count > 0 ? beams.Min(q => q.U - q.B / 2) : double.MaxValue);
        double uMax = Math.Max(slabs.Count > 0 ? slabs.Max(q => q.T1) : double.MinValue, beams.Count > 0 ? beams.Max(q => q.U + q.B / 2) : double.MinValue);
        // bê tông: mặt trên liền, đáy sàn giữa các dầm, dầm b × h
        w.Line(Lay.BeTong, uMin, 0, uMax, 0);
        foreach (var q in slabs)
        {
            var cuts = beams.Where(bm => bm.U + bm.B / 2 > q.T0 && bm.U - bm.B / 2 < q.T1).Select(bm => (bm.U - bm.B / 2, bm.U + bm.B / 2)).OrderBy(z => z.Item1).ToList();
            double u = q.T0;
            foreach (var (c0, c1) in cuts) { if (c0 > u + 0.5) w.Line(Lay.BeTong, u, -q.Th, c0, -q.Th); u = Math.Max(u, c1); }
            if (q.T1 > u + 0.5) w.Line(Lay.BeTong, u, -q.Th, q.T1, -q.Th);
            if (!InBeam(q.T0 - 1) && !InBeam(q.T0 + 1)) w.Line(Lay.BeTong, q.T0, 0, q.T0, -q.Th);
            if (!InBeam(q.T1 - 1) && !InBeam(q.T1 + 1)) w.Line(Lay.BeTong, q.T1, 0, q.T1, -q.Th);
        }
        foreach (var bm in beams)
        {
            double l = bm.U - bm.B / 2, r = bm.U + bm.B / 2;
            w.Line(Lay.BeTong, l, -SlabT(l - 1), l, -bm.H);
            w.Line(Lay.BeTong, r, -SlabT(r + 1), r, -bm.H);
            w.Line(Lay.BeTong, l, -bm.H, r, -bm.H);
            w.Line(Lay.Truc, bm.U, 0.8 * th, bm.U, -bm.H - 0.8 * th, "CENTER");
            w.Text(Lay.Chu, bm.U, -bm.H - 1.2 * th, 0.8 * th, bm.Name, 0, 1, 3);
        }

        // thép từng ô sàn
        foreach (var q in slabs)
        {
            if (q.M is not ManualSlab sl || q.R == null) continue;
            var it = q.It;
            bool shortX = (it.MaxX - it.MinX) <= (it.MaxY - it.MinY) + 1e-6;
            double Ls = Math.Min(it.MaxX - it.MinX, it.MaxY - it.MinY) * 1000;
            // thanh chạy theo phương đường cắt hay vuông góc
            bool shortAlong = shortX == cutAlongX;
            BarMark Role(string role) => q.R.Bars.FirstOrDefault(b => b.Role == role);
            var bAlong = Role(shortAlong ? "Lớp dưới phương ngắn" : "Lớp dưới phương dài");
            var bAcross = Role(shortAlong ? "Lớp dưới phương dài" : "Lớp dưới phương ngắn");
            var tAlong = Role(shortAlong ? "Mũ gối cạnh dài" : "Mũ gối cạnh ngắn");
            double t = q.Th, bwE = (sl.BeamWidth > 0 ? sl.BeamWidth : 0.22) * 1000;
            double mid = (q.T0 + q.T1) / 2;
            // lớp dưới theo phương cắt = đường thép; lớp còn lại = chấm
            double dA = bAlong?.Dia ?? 0;
            // [Shop/ghi chú cấu tạo] lớp dưới: thép PHƯƠNG NGẮN đặt trước (sát đáy, cánh tay đòn lớn), phương dài đặt trên
            double dLow = shortAlong ? 0 : (bAcross?.Dia ?? 0);   // cắt theo phương dài: chấm phương ngắn nằm dưới đường thép
            if (bAlong != null)
            {
                double vb = -t + cv + dLow + dA / 2;
                w.Line(Lay.Thep, q.T0 - bwE / 2 + cv, vb, q.T1 + bwE / 2 - cv, vb);
                w.LeaderTag(q.T0 + (q.T1 - q.T0) * 0.40, vb, q.T0 + (q.T1 - q.T0) * 0.40 + 1.5 * th, -t - 3.2 * th, Dt.Spaced(bAlong.Dia, bAlong.Spacing), bAlong.Mark);
            }
            if (bAcross != null)
            {
                double vd = (shortAlong ? -t + cv + dA : -t + cv) + bAcross.Dia / 2.0, sp = Math.Max(bAcross.Spacing * 1000, 100);
                double first = q.T0 + 50, lastX = first;
                for (double u = first; u <= q.T1 - 50 + 1e-6; u += sp) { w.Dot(Lay.Thep, u, vd, Math.Max(bAcross.Dia / 2.0, 0.12 * th)); lastX = u; }
                double tagU = first + Math.Floor((q.T1 - q.T0) * 0.65 / sp) * sp;
                if (tagU > lastX) tagU = lastX;
                w.LeaderTag(tagU, vd, tagU + 1.5 * th, -t - 5.6 * th, Dt.Spaced(bAcross.Dia, bAcross.Spacing), bAcross.Mark);
            }
            // thép mũ theo phương cắt ở hai mép ô (cạnh ngàm)
            if (tAlong != null)
            {
                double ext = tAlong.Segments.Count > 0 ? tAlong.Segments[0] : Ls / 4 + bwE / 2;
                double inner = Ls / 4, outer = Math.Max(ext - inner, bwE / 2);
                // lớp trên: thép mũ PHƯƠNG NGẮN nằm trên cùng (đặt sau), mũ phương dài nằm dưới nó
                double vt = -cv - (shortAlong ? 0 : (Role("Mũ gối cạnh dài")?.Dia ?? 0)) - tAlong.Dia / 2.0, legDown = Math.Max(t - 2 * cv, 40);
                bool fixedSides = (shortAlong ? sl.FixedLongEdges : sl.FixedShortEdges) >= 1;
                int sides = shortAlong ? sl.FixedLongEdges : sl.FixedShortEdges;
                if (!fixedSides) sides = 1;
                var ends = new List<(double E, int Dir)> { (q.T0, 1) };
                if (sides >= 2) ends.Add((q.T1, -1));
                foreach (var (e, dir) in ends)
                {
                    double uo = e - dir * outer, ui = e + dir * inner;
                    w.Polyline(Lay.Thep, new List<(double, double)> { (uo, vt - legDown), (uo, vt), (ui, vt), (ui, vt - Math.Min(60, legDown)) }, false);
                }
                double tu = q.T0 + inner * 0.55;
                w.LeaderTag(tu, vt, tu + 1.5 * th, 3.0 * th, Dt.Spaced(tAlong.Dia, tAlong.Spacing), tAlong.Mark);
            }
            w.Text(Lay.Chu, mid, 0.5 * th, 0.9 * th, sl.Name ?? "", 0, 1, 0);
        }

        // kích thước: trục dầm + mép, chiều dày sàn
        double hMax = Math.Max(beams.Count > 0 ? beams.Max(q => q.H) : 0, slabs.Count > 0 ? slabs.Max(q => q.Th) : 0);
        var axes = beams.Select(q => q.U).ToList();
        if (axes.Count >= 2) w.DimChainH(axes, -hMax - 2.4 * th, -hMax - 3.6 * th);
        else if (slabs.Count > 0) w.Dim(slabs[0].T0, -hMax - 2.4 * th, slabs[0].T1, -hMax - 2.4 * th, -1.2 * th);
        if (slabs.Count > 0) { var q0 = slabs[0]; w.Dim(q0.T0 - 2.4 * th, -q0.Th, q0.T0 - 2.4 * th, 0, 0); }
        var e2 = w.Extents;
        w.Title(e2.Cx, e2.Y1 - 2.0 * th, "MẶT CẮT " + sm.Name, DrawUtil.ScaleText(SectionScale), true);
        return w;
    }

    /// <summary>Cắt đoạn P(t) = A + t·u, t ∈ [0, L] với hình chữ nhật (Liang–Barsky). Trả về khoảng [t0, t1] có độ dài &gt; 0.</summary>
    public static bool ClipRect(double ax, double ay, double ux, double uy, double L, double x0, double y0, double x1, double y1, out double t0, out double t1)
    {
        t0 = 0; t1 = L;
        double[] p = { -ux, ux, -uy, uy };
        double[] q = { ax - x0, x1 - ax, ay - y0, y1 - ay };
        for (int i = 0; i < 4; i++)
        {
            if (Math.Abs(p[i]) < 1e-12) { if (q[i] < 0) return false; continue; }
            double r = q[i] / p[i];
            if (p[i] < 0) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
        }
        return t1 - t0 > 1e-6;
    }
}
