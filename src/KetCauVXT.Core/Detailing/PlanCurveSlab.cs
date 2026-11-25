using KetCauVXT.Import;

namespace KetCauVXT.Detailing;

// =====================================================================
// [Triển khai – dầm cong, sàn từ dầm, sao chép mặt bằng]
//  • PlanArc: hình học dầm cong trên mặt bằng (PlanItem.Bulge = độ vồng tại giữa cung, + lệch trái hướng 1→2) – cùng quy ước dầm cong mô hình.
//  • PlanAutoSlab: tạo ô sàn từ mặt bằng dầm đã nhận (ô kín chữ nhật bao bởi dầm thẳng), gộp ô cùng kích thước thành cùng cấu kiện S1, S2…
//  • PlanCopy: sao chép toàn bộ vị trí cấu kiện của một mặt bằng sang các mặt bằng CÓ SẴN giống nhau (dùng chung cấu kiện).
// =====================================================================

/// <summary>Hình học cung của dầm cong trên mặt bằng.</summary>
public static class PlanArc
{
    /// <summary>Tâm, bán kính, góc đầu (rad, tại điểm 1), góc quét có dấu (rad, từ điểm 1 tới điểm 2); null nếu thẳng.</summary>
    public static (double Cx, double Cy, double R, double A0, double Sweep)? Geom(PlanItem it)
    {
        if (it == null || !it.IsCurved) return null;
        return Geom(it.X1, it.Y1, it.X2, it.Y2, it.Bulge);
    }

    public static (double Cx, double Cy, double R, double A0, double Sweep)? Geom(double x1, double y1, double x2, double y2, double s)
    {
        double dx = x2 - x1, dy = y2 - y1, c = Math.Sqrt(dx * dx + dy * dy);
        if (c < 1e-6 || Math.Abs(s) < 1e-6) return null;
        double ux = dx / c, uy = dy / c, nx = -uy, ny = ux;
        double R = (c * c / 4 + s * s) / (2 * Math.Abs(s));
        double mx = (x1 + x2) / 2, my = (y1 + y2) / 2, sg = Math.Sign(s);
        double cx = mx + nx * (s - sg * R), cy = my + ny * (s - sg * R);
        double phi = 4 * Math.Atan(2 * Math.Abs(s) / c);
        return (cx, cy, R, Math.Atan2(y1 - cy, x1 - cx), -sg * phi);
    }

    /// <summary>Chiều dài cung (dầm thẳng: chiều dài đoạn).</summary>
    public static double Length(PlanItem it)
    {
        var g = Geom(it);
        return g == null ? it.Length : g.Value.R * Math.Abs(g.Value.Sweep);
    }

    /// <summary>Điểm trên tim dầm (thẳng: 2 điểm; cong: n + 1 điểm). offset: dời song song (m, + về tâm cung / bên trái).</summary>
    public static List<(double X, double Y)> Path(PlanItem it, int n = 32, double offset = 0)
    {
        var g = Geom(it);
        if (g == null)
        {
            double L = Math.Max(it.Length, 1e-9), nx = -(it.Y2 - it.Y1) / L, ny = (it.X2 - it.X1) / L;
            return new() { (it.X1 + nx * offset, it.Y1 + ny * offset), (it.X2 + nx * offset, it.Y2 + ny * offset) };
        }
        var (cx, cy, R, a0, sw) = g.Value;
        int m = Math.Max(4, n);
        double r = R - offset * Math.Sign(sw);   // offset dương = bên trái hướng đi: quét ngược chiều kim đồng hồ → phía tâm
        var pts = new List<(double, double)>();
        for (int i = 0; i <= m; i++) { double a = a0 + sw * i / m; pts.Add((cx + r * Math.Cos(a), cy + r * Math.Sin(a))); }
        return pts;
    }

    /// <summary>Điểm giữa cung.</summary>
    public static (double X, double Y) Mid(PlanItem it)
    {
        var g = Geom(it);
        if (g == null) return ((it.X1 + it.X2) / 2, (it.Y1 + it.Y2) / 2);
        var (cx, cy, R, a0, sw) = g.Value;
        return (cx + R * Math.Cos(a0 + sw / 2), cy + R * Math.Sin(a0 + sw / 2));
    }

    /// <summary>Khoảng cách điểm tới tim dầm cong.</summary>
    public static double Dist(PlanItem it, double x, double y)
    {
        var p = Path(it, 48);
        double d = double.MaxValue;
        for (int i = 0; i + 1 < p.Count; i++) d = Math.Min(d, StructuralPlan.DistSeg(x, y, p[i].X, p[i].Y, p[i + 1].X, p[i + 1].Y));
        return d;
    }

    /// <summary>Độ vồng có dấu của cung qua 2 đầu (x1,y1)-(x2,y2) và một điểm (px,py) trên cung; 0 nếu thẳng hàng.</summary>
    public static double SagittaFrom3(double x1, double y1, double x2, double y2, double px, double py)
    {
        double dx = x2 - x1, dy = y2 - y1, c = Math.Sqrt(dx * dx + dy * dy);
        if (c < 1e-6) return 0;
        double nx = -dy / c, ny = dx / c;
        double d = (px - x1) * nx + (py - y1) * ny;   // phía của điểm so với dây cung
        if (Math.Abs(d) < 1e-4) return 0;
        // tâm đường tròn qua 3 điểm
        double ax = x1, ay = y1, bx = x2, by = y2, qx = px, qy = py;
        double D = 2 * (ax * (by - qy) + bx * (qy - ay) + qx * (ay - by));
        if (Math.Abs(D) < 1e-12) return 0;
        double ux = ((ax * ax + ay * ay) * (by - qy) + (bx * bx + by * by) * (qy - ay) + (qx * qx + qy * qy) * (ay - by)) / D;
        double uy = ((ax * ax + ay * ay) * (qx - bx) + (bx * bx + by * by) * (ax - qx) + (qx * qx + qy * qy) * (bx - ax)) / D;
        double R = Math.Sqrt((ax - ux) * (ax - ux) + (ay - uy) * (ay - uy));
        double dc = (ux - (x1 + x2) / 2) * nx + (uy - (y1 + y2) / 2) * ny;   // tâm so với dây cung
        double h = Math.Sign(dc) == Math.Sign(d) && Math.Abs(dc) > 1e-9 ? R + Math.Abs(dc) : R - Math.Abs(dc);
        return Math.Sign(d) * h;
    }

    /// <summary>Cung (tâm, bán kính, góc đầu, góc quét có dấu) → dầm: 2 đầu + độ vồng.</summary>
    public static (double X1, double Y1, double X2, double Y2, double Bulge) FromArc(double cx, double cy, double r, double a0, double sweep)
    {
        double x1 = cx + r * Math.Cos(a0), y1 = cy + r * Math.Sin(a0), x2 = cx + r * Math.Cos(a0 + sweep), y2 = cy + r * Math.Sin(a0 + sweep);
        double mx = cx + r * Math.Cos(a0 + sweep / 2), my = cy + r * Math.Sin(a0 + sweep / 2);
        double dx = x2 - x1, dy = y2 - y1, c = Math.Sqrt(dx * dx + dy * dy);
        double s = c < 1e-9 ? 0 : (mx - (x1 + x2) / 2) * (-dy / c) + (my - (y1 + y2) / 2) * (dx / c);
        return (x1, y1, x2, y2, s);
    }
}

/// <summary>Tạo ô sàn từ mặt bằng dầm.</summary>
public static class PlanAutoSlab
{
    /// <summary>Tạo ô sàn trong mọi ô kín chữ nhật bao bởi dầm (thẳng, song song trục) của mặt bằng lv; bỏ ô đã có sàn.
    /// Ô cùng kích thước (làm tròn 10 mm) dùng chung cấu kiện; tên S1, S2… theo thứ tự diện tích giảm dần. Trả về số ô tạo mới.</summary>
    public static int Create(DetailingProject p, PlanLevel lv, List<string> log = null)
    {
        if (p == null || lv == null) return 0;
        var plan = p.Plan ??= new StructuralPlan();
        var beams = lv.Items.Where(i => (i.Kind == PlanKinds.Beam) && !i.IsCurved).Select(i => new Seg2(i.X1, i.Y1, i.X2, i.Y2))
                      .Where(s => s.IsAlongX() || s.IsAlongY()).ToList();
        if (beams.Count < 4) { log?.Add("Mặt bằng " + lv.Name + " chưa đủ dầm để tạo ô sàn (cần dầm bao kín ô)."); return 0; }
        if (lv.Items.Any(i => i.Kind == PlanKinds.Beam && i.IsCurved)) log?.Add("Ô sàn giáp dầm cong chưa tự tạo – vẽ ô sàn bằng công cụ Sàn.");
        double x0 = beams.Min(b => Math.Min(b.Ax, b.Bx)), x1 = beams.Max(b => Math.Max(b.Ax, b.Bx));
        double y0 = beams.Min(b => Math.Min(b.Ay, b.By)), y1 = beams.Max(b => Math.Max(b.Ay, b.By));
        var cells = ImportGeometry.RectCells(beams, x0, y0, x1, y1, true, log, "Sàn từ dầm")
                                  .Where(c => c.x1 - c.x0 > 0.5 && c.y1 - c.y0 > 0.5).ToList();
        var existing = lv.Items.Where(i => i.Kind == PlanKinds.Slab || i.Kind == PlanKinds.Stair).ToList();
        cells = cells.Where(c => !existing.Any(e => (c.x0 + c.x1) / 2 > e.MinX && (c.x0 + c.x1) / 2 < e.MaxX && (c.y0 + c.y1) / 2 > e.MinY && (c.y0 + c.y1) / 2 < e.MaxY)).ToList();
        int made = 0;
        foreach (var g in cells.GroupBy(c => (Math.Round(Math.Min(c.x1 - c.x0, c.y1 - c.y0), 2), Math.Round(Math.Max(c.x1 - c.x0, c.y1 - c.y0), 2)))
                               .OrderByDescending(g => g.Key.Item1 * g.Key.Item2))
        {
            var m = p.AddNew("San");
            m.Quantity = g.Count();
            bool first = true;
            foreach (var c in g)
            {
                var it = new PlanItem { Kind = PlanKinds.Slab, Member = m.Name, X1 = Math.Round(c.x0, 4), Y1 = Math.Round(c.y0, 4), X2 = Math.Round(c.x1, 4), Y2 = Math.Round(c.y1, 4) };
                lv.Items.Add(it); made++;
                if (first) { plan.ApplyGeometry(m, it, lv); first = false; }
            }
        }
        plan.SyncQuantities(p);
        log?.Add($"Mặt bằng {lv.Name}: tạo {made} ô sàn từ mặt bằng dầm ({cells.Select(c => (Math.Round(c.x1 - c.x0, 2), Math.Round(c.y1 - c.y0, 2))).Distinct().Count()} loại kích thước).");
        return made;
    }
}

/// <summary>Sao chép mặt bằng sang các mặt bằng có sẵn.</summary>
public static class PlanCopy
{
    /// <summary>Sao mọi vị trí (cột, dầm, sàn, lanh tô, ô văng, cầu thang; móng chỉ khi đích là mặt bằng móng) của src sang các mặt bằng đích,
    /// dùng chung cấu kiện (SL tự cập nhật theo số vị trí). replace: xoá vị trí cũ của mặt bằng đích trước. Trả về số vị trí đã sao.</summary>
    public static int CopyTo(DetailingProject p, PlanLevel src, IEnumerable<PlanLevel> targets, bool replace, bool copySections = true)
    {
        if (p?.Plan == null || src == null) return 0;
        int n = 0;
        foreach (var t in targets.Where(t => t != null && t != src))
        {
            if (replace) t.Items.Clear();
            foreach (var it in src.Items)
            {
                if (it.Kind == PlanKinds.Footing && !t.IsFoundation) continue;
                t.Items.Add(new PlanItem { Kind = it.Kind, Member = it.Member, X1 = it.X1, Y1 = it.Y1, X2 = it.X2, Y2 = it.Y2, Rotation = it.Rotation, Label = it.Label, Bulge = it.Bulge });
                n++;
            }
            if (copySections && p.Plan.Sections != null)
                foreach (var sm in p.Plan.Sections.Where(q => q.Level == src.Name).ToList())
                    if (!p.Plan.Sections.Any(q => q.Level == t.Name && q.Name == sm.Name))
                        p.Plan.Sections.Add(new SectionMark { Name = sm.Name, Level = t.Name, X1 = sm.X1, Y1 = sm.Y1, X2 = sm.X2, Y2 = sm.Y2 });
        }
        p.Plan.SyncQuantities(p);
        return n;
    }
}
