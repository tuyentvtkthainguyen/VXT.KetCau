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
    public static int Create(DetailingProject p, PlanLevel lv, List<string> log = null, bool faces = true)
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
        var existing = lv.Items.Where(i => i.Kind == PlanKinds.Slab || i.Kind == PlanKinds.Stair || i.Kind == PlanKinds.Void).ToList();   // bỏ ô đã có sàn, ô thang, giếng trời
        cells = cells.Where(c => !existing.Any(e => (c.x0 + c.x1) / 2 > e.MinX && (c.x0 + c.x1) / 2 < e.MaxX && (c.y0 + c.y1) / 2 > e.MinY && (c.y0 + c.y1) / 2 < e.MaxY)).ToList();
        // [Ô sàn theo mép dầm] thu mỗi cạnh nửa bề rộng dầm nằm trên cạnh đó (ô không chia theo trục / tim)
        var axisCells = new HashSet<(double, double, double, double)>();
        if (faces)
            cells = cells.Select(c =>
            {
                var q = PlanOutline.InsetToBeamFaces(p, lv, new List<double[]> { new[] { c.x0, c.y0 }, new[] { c.x1, c.y0 }, new[] { c.x1, c.y1 }, new[] { c.x0, c.y1 } });
                if (q == null || q.Count != 4) { axisCells.Add((c.x0, c.y0, c.x1, c.y1)); return c; }
                return (x0: q.Min(v => v[0]), y0: q.Min(v => v[1]), x1: q.Max(v => v[0]), y1: q.Max(v => v[1]));
            }).Where(c => c.x1 - c.x0 > 0.3 && c.y1 - c.y0 > 0.3).ToList();
        int made = 0;
        foreach (var g in cells.GroupBy(c => (Math.Round(Math.Min(c.x1 - c.x0, c.y1 - c.y0), 2), Math.Round(Math.Max(c.x1 - c.x0, c.y1 - c.y0), 2)))
                               .OrderByDescending(g => g.Key.Item1 * g.Key.Item2))
        {
            var m = p.AddNew("San");
            m.Quantity = g.Count();
            bool first = true;
            foreach (var c in g)
            {
                var it = new PlanItem { Kind = PlanKinds.Slab, Member = m.Name, X1 = Math.Round(c.x0, 4), Y1 = Math.Round(c.y0, 4), X2 = Math.Round(c.x1, 4), Y2 = Math.Round(c.y1, 4), Faces = faces && !axisCells.Contains((c.x0, c.y0, c.x1, c.y1)) };
                lv.Items.Add(it); made++;
                if (first) { plan.ApplyGeometry(m, it, lv); first = false; }
            }
        }
        plan.SyncQuantities(p);
        log?.Add($"Mặt bằng {lv.Name}: tạo {made} ô sàn từ mặt bằng dầm{(faces ? " – biên ô theo mép dầm" : "")} ({cells.Select(c => (Math.Round(c.x1 - c.x0, 2), Math.Round(c.y1 - c.y0, 2))).Distinct().Count()} loại kích thước).");
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
                t.Items.Add(new PlanItem { Kind = it.Kind, Member = it.Member, X1 = it.X1, Y1 = it.Y1, X2 = it.X2, Y2 = it.Y2, Rotation = it.Rotation, Label = it.Label, Bulge = it.Bulge, Drop = it.Drop, Faces = it.Faces,
                    Outline = it.Outline?.Select(q => new[] { q[0], q[1] }).ToList() });
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

/// <summary>[Dầm liên tục] Dầm thẳng hàng nối liền trên một trục = MỘT dầm dài nhiều nhịp (nhịp chia theo trục / cột cắt qua);
/// chỉ tách thành các dầm riêng khi người dùng chia tại vị trí chọn (<see cref="Split"/>).</summary>
public static class PlanBeamMerge
{
    private static (double B, double H)? Sec(DetailingProject p, PlanItem it) =>
        p.All.FirstOrDefault(m => m.Name == it.Member) is ManualBeam mb ? (Math.Round(mb.B, 3), Math.Round(mb.H, 3)) : null;

    /// <summary>Gộp các dầm thẳng (không cong) thẳng hàng, nối liền (hở ≤ 5 cm hoặc hở chứa tâm cột) trên mặt bằng lv thành một vị trí dầm.
    /// sameMemberOnly: chỉ gộp các đoạn cùng tên cấu kiện (dùng khi nhập); ngược lại gộp các đoạn cùng tiết diện b × h.
    /// seeds: chỉ gộp các chuỗi có chứa vị trí trong seeds (null = mọi dầm). Cấu kiện không còn vị trí nào sau khi gộp bị xoá khỏi hồ sơ.
    /// Trả về số vị trí đã gộp bớt.</summary>
    public static int Merge(DetailingProject p, PlanLevel lv, bool sameMemberOnly, IEnumerable<PlanItem> seeds = null, List<string> log = null,
                            Func<PlanItem, PlanItem, bool> same = null)
    {
        if (p?.Plan == null || lv == null) return 0;
        var plan = p.Plan;
        var seedSet = seeds != null ? new HashSet<PlanItem>(seeds) : null;
        var cols = lv.Items.Where(i => i.Kind == PlanKinds.Column).ToList();
        var beams = lv.Items.Where(i => i.Kind == PlanKinds.Beam && !i.IsCurved && i.Length > 1e-3).ToList();
        // khoá đường thẳng: hướng chuẩn hoá + khoảng cách tới gốc
        (double Ux, double Uy, double D) Key(PlanItem it)
        {
            double L = it.Length, ux = (it.X2 - it.X1) / L, uy = (it.Y2 - it.Y1) / L;
            if (ux < -1e-9 || (Math.Abs(ux) <= 1e-9 && uy < 0)) { ux = -ux; uy = -uy; }
            return (ux, uy, -it.X1 * uy + it.Y1 * ux);
        }
        var groups = new List<List<PlanItem>>();
        foreach (var b in beams)
        {
            var k = Key(b);
            var g = groups.FirstOrDefault(gr => { var k2 = Key(gr[0]); return Math.Abs(k.Ux * k2.Uy - k.Uy * k2.Ux) < 1e-3 && Math.Abs(k.D - k2.D) < 0.02; });
            if (g == null) groups.Add(new List<PlanItem> { b }); else g.Add(b);
        }
        int merged = 0;
        var removedMembers = new HashSet<string>();
        foreach (var g in groups.Where(x => x.Count > 1))
        {
            var (ux, uy, _) = Key(g[0]);
            double T(double x, double y) => x * ux + y * uy;
            var segs = g.Select(it => (It: it, T0: Math.Min(T(it.X1, it.Y1), T(it.X2, it.Y2)), T1: Math.Max(T(it.X1, it.Y1), T(it.X2, it.Y2)))).OrderBy(s => s.T0).ToList();
            var cur = segs[0];
            var chain = new List<(PlanItem It, double T0, double T1)> { cur };
            void Flush()
            {
                if (chain.Count > 1 && (seedSet == null || chain.Any(c => seedSet.Contains(c.It))))
                {
                    var keep = chain[0].It;
                    double t0 = chain.Min(c => c.T0), t1 = chain.Max(c => c.T1);
                    // điểm đầu / cuối trên đường thẳng của keep
                    double ox = keep.X1 - T(keep.X1, keep.Y1) * ux, oy = keep.Y1 - T(keep.X1, keep.Y1) * uy;
                    keep.X1 = Math.Round(ox + ux * t0, 4); keep.Y1 = Math.Round(oy + uy * t0, 4);
                    keep.X2 = Math.Round(ox + ux * t1, 4); keep.Y2 = Math.Round(oy + uy * t1, 4);
                    foreach (var c in chain.Skip(1)) { lv.Items.Remove(c.It); merged++; if (!string.IsNullOrEmpty(c.It.Member) && c.It.Member != keep.Member) removedMembers.Add(c.It.Member); }
                    var m = p.All.FirstOrDefault(x => x.Name == keep.Member);
                    if (m != null) plan.ApplyGeometry(m, keep, lv);
                    log?.Add($"{lv.Name}: gộp {chain.Count} đoạn thành dầm {keep.Member} dài {Dt.F(t1 - t0, "0.###")} m (nhịp {string.Join("; ", plan.BeamSpans(keep, lv).Select(v => Dt.F(v, "0.###")))}).");
                }
                chain.Clear();
            }
            for (int i = 1; i < segs.Count; i++)
            {
                var s = segs[i];
                var last = chain[^1];
                double gap = s.T0 - chain.Max(c => c.T1);
                bool touch = gap <= 0.05 || (gap <= 1.0 && cols.Any(c =>
                {
                    double tc = T(c.X1, c.Y1), d = Math.Abs(-(c.X1) * uy + c.Y1 * ux - Key(g[0]).D);
                    return d < 0.05 && tc >= chain.Max(q => q.T1) - 0.01 && tc <= s.T0 + 0.01;
                }));
                bool ok = same != null ? same(last.It, s.It) : sameMemberOnly ? s.It.Member == last.It.Member : Equals(Sec(p, s.It), Sec(p, last.It));
                if (touch && ok) chain.Add(s);
                else { Flush(); chain.Add(s); }
            }
            Flush();
        }
        // cấu kiện không còn vị trí nào → xoá khỏi hồ sơ
        foreach (var nm in removedMembers)
            if (plan.Count(nm) == 0 && p.Beams.FirstOrDefault(b => b.Name == nm) is ManualBeam mb) p.Beams.Remove(mb);
        if (merged > 0) plan.SyncQuantities(p);
        return merged;
    }

    /// <summary>Dầm vừa vẽ nối thẳng hàng với dầm có sẵn: nhận cấu kiện của dầm có sẵn (bỏ cấu kiện mới tạo nếu không còn vị trí) rồi gộp thành một dầm.
    /// Trả về true nếu đã gộp.</summary>
    public static bool AttachNew(DetailingProject p, PlanLevel lv, PlanItem it, bool memberWasNew)
    {
        if (p?.Plan == null || lv == null || it == null || it.Kind != PlanKinds.Beam || it.IsCurved || it.Length < 1e-3) return false;
        double L = it.Length, ux = (it.X2 - it.X1) / L, uy = (it.Y2 - it.Y1) / L;
        PlanItem nb = null;
        foreach (var o in lv.Items.Where(i => i != it && i.Kind == PlanKinds.Beam && !i.IsCurved && i.Length > 1e-3))
        {
            double Lo = o.Length, vx = (o.X2 - o.X1) / Lo, vy = (o.Y2 - o.Y1) / Lo;
            if (Math.Abs(ux * vy - uy * vx) > 1e-3) continue;
            if (Math.Abs(-(o.X1 - it.X1) * uy + (o.Y1 - it.Y1) * ux) > 0.02) continue;
            double a0 = 0, a1 = L, b0 = (o.X1 - it.X1) * ux + (o.Y1 - it.Y1) * uy, b1 = (o.X2 - it.X1) * ux + (o.Y2 - it.Y1) * uy;
            if (Math.Max(a0, Math.Min(b0, b1)) - Math.Min(a1, Math.Max(b0, b1)) > 0.05) continue;   // không chạm
            nb = o; break;
        }
        if (nb == null || string.IsNullOrEmpty(nb.Member)) return false;
        if (it.Member != nb.Member)
        {
            // [Dầm liên tục] dầm qua nhiều cột thẳng hàng là MỘT dầm: đoạn vẽ thêm nối liền, cùng tiết diện → nhập vào dầm có sẵn
            // (kể cả khi đang chọn tên khác); khác tiết diện → dầm riêng. Muốn tách: công cụ Chia dầm.
            if (!memberWasNew)
            {
                var ma = p.Beams.FirstOrDefault(b => b.Name == it.Member) as ManualBeam; var mb2 = p.Beams.FirstOrDefault(b => b.Name == nb.Member) as ManualBeam;
                if (ma == null || mb2 == null || Math.Abs(ma.B - mb2.B) > 1e-6 || Math.Abs(ma.H - mb2.H) > 1e-6) return false;
            }
            string old = it.Member;
            it.Member = nb.Member;
            if (memberWasNew && !string.IsNullOrEmpty(old) && p.Plan.Count(old) == 0 && p.Beams.FirstOrDefault(b => b.Name == old) is ManualBeam mb) p.Beams.Remove(mb);
        }
        return Merge(p, lv, true, new[] { it }) > 0;
    }

    /// <summary>Chia dầm tại điểm (x, y) (chiếu lên dầm): đoạn đầu giữ cấu kiện, đoạn sau thành cấu kiện mới cùng tiết diện (tên tiếp theo).
    /// Trả về vị trí đoạn sau; null nếu điểm quá gần đầu dầm hoặc dầm cong.</summary>
    public static PlanItem Split(DetailingProject p, PlanLevel lv, PlanItem it, double x, double y)
    {
        if (p?.Plan == null || lv == null || it == null || it.Kind != PlanKinds.Beam || it.IsCurved) return null;
        double L = it.Length; if (L < 0.2) return null;
        double ux = (it.X2 - it.X1) / L, uy = (it.Y2 - it.Y1) / L;
        double t = (x - it.X1) * ux + (y - it.Y1) * uy;
        if (t < 0.1 || t > L - 0.1) return null;
        double sx = Math.Round(it.X1 + ux * t, 4), sy = Math.Round(it.Y1 + uy * t, 4);
        var it2 = new PlanItem { Kind = PlanKinds.Beam, X1 = sx, Y1 = sy, X2 = it.X2, Y2 = it.Y2, Label = it.Label };
        it.X2 = sx; it.Y2 = sy;
        lv.Items.Add(it2);
        var m = p.All.FirstOrDefault(q => q.Name == it.Member) as ManualBeam;
        if (m != null)
        {
            var c = (ManualBeam)PlanStorey.CloneMember(m);
            var names = new HashSet<string>(p.All.Select(q => q.Name));
            string prefix = new string(m.Name.TakeWhile(ch => !char.IsDigit(ch)).ToArray());
            if (prefix.Length == 0) prefix = "D";
            int k = 1; while (names.Contains(prefix + k)) k++;
            c.Name = prefix + k; c.Quantity = 1;
            p.Beams.Add(c);
            it2.Member = c.Name;
            p.Plan.ApplyGeometry(m, it, lv);
            p.Plan.ApplyGeometry(c, it2, lv);
        }
        else p.Plan.EnsureMember(p, it2, lv);
        p.Plan.SyncQuantities(p);
        return it2;
    }
}

/// <summary>[Biên dạng sàn] Pick đường bo viền: bấm vào trong vùng kín bao bởi dầm (thẳng + cong, chia cung thành đoạn ngắn) →
/// đa giác biên ô sàn (vùng nhỏ nhất chứa điểm bấm); hỗ trợ ô chữ L, ô xiên, ô giáp dầm cong.</summary>
public static class PlanOutline
{
    /// <summary>Trọng tâm diện tích đa giác.</summary>
    public static (double X, double Y) Centroid(IList<double[]> poly)
    {
        double a = 0, cx = 0, cy = 0; int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            var p = poly[i]; var q = poly[(i + 1) % n];
            double c = p[0] * q[1] - q[0] * p[1];
            a += c; cx += (p[0] + q[0]) * c; cy += (p[1] + q[1]) * c;
        }
        if (Math.Abs(a) < 1e-12) return (poly.Average(p => p[0]), poly.Average(p => p[1]));
        return (cx / (3 * a), cy / (3 * a));
    }

    public static bool Inside(List<double[]> poly, double x, double y)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i][1] > y) != (poly[j][1] > y) && x < (poly[j][0] - poly[i][0]) * (y - poly[i][1]) / (poly[j][1] - poly[i][1] + 1e-300) + poly[i][0]) c = !c;
        return c;
    }

    public static double Area(List<double[]> poly)
    {
        double a = 0;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) a += poly[j][0] * poly[i][1] - poly[i][0] * poly[j][1];
        return a / 2;
    }

    /// <summary>Vùng kín nhỏ nhất bao điểm (x, y) tạo bởi tim dầm của mặt bằng (bỏ nhánh cụt); null nếu không có.</summary>
    public static List<double[]> Region(PlanLevel lv, double x, double y)
    {
        var segs = new List<(double X1, double Y1, double X2, double Y2)>();
        foreach (var it in lv?.Items ?? new())
        {
            if (it.Kind != PlanKinds.Beam && it.Kind != PlanKinds.Lintel) continue;
            var pts = PlanArc.Path(it, 24);
            for (int i = 0; i + 1 < pts.Count; i++) segs.Add((pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y));
        }
        return RegionOf(segs, x, y);
    }

    /// <summary>[Biên sàn từ nét CAD] vùng kín nhỏ nhất chứa (x, y) tạo bởi các đoạn thẳng bất kỳ (nét nền kiến trúc / xref:
    /// đường cong đã chia đoạn, zic zắc…). Chỉ xét các đoạn trong phạm vi r quanh điểm bấm.</summary>
    public static List<double[]> RegionOf(IEnumerable<(double X1, double Y1, double X2, double Y2)> all, double x, double y, double r = 80)
    {
        var segs = all.Where(q => Math.Min(q.X1, q.X2) < x + r && Math.Max(q.X1, q.X2) > x - r && Math.Min(q.Y1, q.Y2) < y + r && Math.Max(q.Y1, q.Y2) > y - r
                                  && Math.Abs(q.X2 - q.X1) + Math.Abs(q.Y2 - q.Y1) > 1e-4).ToList();
        if (segs.Count < 3) return null;
        // chia đoạn tại giao điểm
        var cuts = segs.Select(_ => new List<double> { 0, 1 }).ToList();
        for (int i = 0; i < segs.Count; i++)
            for (int j = i + 1; j < segs.Count; j++)
            {
                var a = segs[i]; var b = segs[j];
                double rx = a.X2 - a.X1, ry = a.Y2 - a.Y1, sx = b.X2 - b.X1, sy = b.Y2 - b.Y1, den = rx * sy - ry * sx;
                if (Math.Abs(den) < 1e-12) continue;
                double qx = b.X1 - a.X1, qy = b.Y1 - a.Y1;
                double t = (qx * sy - qy * sx) / den, u = (qx * ry - qy * rx) / den;
                double ta = 0.02 / Math.Max(1e-9, Math.Sqrt(rx * rx + ry * ry)), tb = 0.02 / Math.Max(1e-9, Math.Sqrt(sx * sx + sy * sy));   // bắt 2 cm ở đầu đoạn
                if (t < -ta || t > 1 + ta || u < -tb || u > 1 + tb) continue;
                cuts[i].Add(Math.Clamp(t, 0, 1)); cuts[j].Add(Math.Clamp(u, 0, 1));
            }
        var nodes = new List<(double X, double Y)>();
        int Node(double px, double py)
        {
            for (int k = 0; k < nodes.Count; k++) if (Math.Abs(nodes[k].X - px) < 0.02 && Math.Abs(nodes[k].Y - py) < 0.02) return k;
            nodes.Add((px, py)); return nodes.Count - 1;
        }
        var edges = new HashSet<(int, int)>();
        for (int i = 0; i < segs.Count; i++)
        {
            var s = segs[i];
            var ts = cuts[i].Distinct().OrderBy(t => t).ToList();
            int prev = Node(s.X1 + (s.X2 - s.X1) * ts[0], s.Y1 + (s.Y2 - s.Y1) * ts[0]);
            for (int k = 1; k < ts.Count; k++)
            {
                int n = Node(s.X1 + (s.X2 - s.X1) * ts[k], s.Y1 + (s.Y2 - s.Y1) * ts[k]);
                if (n != prev) { edges.Add((Math.Min(n, prev), Math.Max(n, prev))); }
                prev = n;
            }
        }
        // bỏ nhánh cụt (nút bậc 1) lặp lại
        var adj = new Dictionary<int, HashSet<int>>();
        foreach (var (a, b) in edges) { (adj.TryGetValue(a, out var la) ? la : adj[a] = new()).Add(b); (adj.TryGetValue(b, out var lb) ? lb : adj[b] = new()).Add(a); }
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var k in adj.Keys.ToList())
                if (adj.TryGetValue(k, out var l) && l.Count <= 1) { foreach (var o in l) adj[o].Remove(k); adj.Remove(k); changed = true; }
        }
        if (adj.Count < 3) return null;
        // duyệt mặt: tại mỗi nút đi tiếp cạnh quay trái nhiều nhất (theo thứ tự góc)
        double Ang(int a, int b) => Math.Atan2(nodes[b].Y - nodes[a].Y, nodes[b].X - nodes[a].X);
        var used = new HashSet<(int, int)>();
        List<double[]> best = null; double bestA = double.MaxValue;
        foreach (var a in adj.Keys)
            foreach (var b in adj[a])
            {
                if (used.Contains((a, b))) continue;
                var face = new List<int>();
                int u = a, v = b, guard = 0;
                while (guard++ < 10000)
                {
                    used.Add((u, v));
                    face.Add(u);
                    // cạnh kế tiếp: từ v, góc quay so với hướng ngược (v→u) theo chiều kim đồng hồ nhỏ nhất → mặt bên trái
                    double back = Ang(v, u);
                    int w = -1; double bestTurn = double.MaxValue;
                    foreach (var c in adj[v])
                    {
                        if (c == u && adj[v].Count > 1) continue;
                        double t = back - Ang(v, c);
                        while (t <= 1e-9) t += 2 * Math.PI;
                        while (t > 2 * Math.PI) t -= 2 * Math.PI;
                        if (t < bestTurn) { bestTurn = t; w = c; }
                    }
                    if (w < 0) break;
                    u = v; v = w;
                    if (u == a && v == b) break;
                }
                if (face.Count < 3) continue;
                var poly = face.Select(k => new[] { Math.Round(nodes[k].X, 4), Math.Round(nodes[k].Y, 4) }).ToList();
                double ar = Area(poly);
                if (ar <= 1e-4) continue;   // mặt ngoài (chiều kim đồng hồ)
                if (!Inside(poly, x, y)) continue;
                if (ar < bestA) { bestA = ar; best = poly; }
            }
        return best == null ? null : Simplify(best);
    }

    /// <summary>Bỏ đỉnh thẳng hàng.</summary>
    public static List<double[]> Simplify(List<double[]> p)
    {
        var r = new List<double[]>(p);
        bool ch = true;
        while (ch && r.Count > 3)
        {
            ch = false;
            for (int i = 0; i < r.Count; i++)
            {
                var a = r[(i - 1 + r.Count) % r.Count]; var b = r[i]; var c = r[(i + 1) % r.Count];
                double cr = (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0]);
                double L = Math.Sqrt((c[0] - a[0]) * (c[0] - a[0]) + (c[1] - a[1]) * (c[1] - a[1]));
                if (Math.Abs(cr) < 1e-4 * Math.Max(L, 1e-9)) { r.RemoveAt(i); ch = true; break; }
            }
        }
        return r;
    }

    /// <summary>[Biên theo mép dầm] Thu đường biên tim dầm vào trong mỗi cạnh nửa bề rộng dầm nằm trên cạnh đó
    /// (cạnh không nằm trên dầm / lanh tô giữ nguyên). null nếu đa giác suy biến.</summary>
    public static List<double[]> InsetToBeamFaces(DetailingProject p, PlanLevel lv, List<double[]> poly, double defB = 0.22)
    {
        if (poly == null || poly.Count < 3 || lv == null) return null;
        int n = poly.Count;
        var d = new double[n];
        var segs = lv.Items.Where(i => i.Kind == PlanKinds.Beam || i.Kind == PlanKinds.Lintel).ToList();
        for (int i = 0; i < n; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % n];
            double mx = (a[0] + b[0]) / 2, my = (a[1] + b[1]) / 2;
            var on = segs.Select(it => (It: it, D: PlanArc.Dist(it, mx, my))).Where(t => t.D < 0.05).OrderBy(t => t.D).Select(t => t.It).FirstOrDefault();
            if (on == null) continue;
            var m = p?.All.FirstOrDefault(x => x.Name == on.Member);
            double w = m switch { ManualBeam mb when mb.B > 0 => mb.B, ManualLintel ml when ml.Bw > 0 => ml.Bw, _ => defB };
            d[i] = w / 2;
        }
        return Offset(poly, d);
    }

    /// <summary>Dời từng cạnh đa giác vào trong một khoảng d[i] (cạnh i nối đỉnh i → i+1); đỉnh mới = giao hai cạnh kề.</summary>
    public static List<double[]> Offset(List<double[]> poly, double[] d)
    {
        int n = poly.Count;
        double A0 = Area(poly);
        if (Math.Abs(A0) < 1e-9) return null;
        double sg = A0 > 0 ? 1 : -1;
        var P = new (double X, double Y)[n]; var U = new (double X, double Y)[n];
        for (int i = 0; i < n; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % n];
            double L = Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
            if (L < 1e-9) return null;
            double ux = (b[0] - a[0]) / L, uy = (b[1] - a[1]) / L, nx = -uy * sg, ny = ux * sg;
            U[i] = (ux, uy); P[i] = (a[0] + nx * d[i], a[1] + ny * d[i]);
        }
        var r = new List<double[]>();
        for (int i = 0; i < n; i++)
        {
            int j = (i - 1 + n) % n;
            double cr = U[j].X * U[i].Y - U[j].Y * U[i].X;
            if (Math.Abs(cr) < 1e-9) { r.Add(new[] { P[i].X, P[i].Y }); continue; }
            double t = ((P[i].X - P[j].X) * U[i].Y - (P[i].Y - P[j].Y) * U[i].X) / cr;
            r.Add(new[] { Math.Round(P[j].X + U[j].X * t, 4), Math.Round(P[j].Y + U[j].Y * t, 4) });
        }
        double A1 = Area(r);
        if (A1 * A0 <= 0 || Math.Abs(A1) > Math.Abs(A0) + 1e-9) return null;
        return Simplify(r);
    }

    /// <summary>[Bắt điểm mép / giữa dầm] Điểm gần (x, y) trong tol: góc giao mép dầm (mép trong ô), điểm giữa dầm, đầu mép dầm,
    /// sau đó chân vuông góc trên mép dầm (≤ tol/2). Trả (X, Y, loại) hoặc null. Chỉ dầm / lanh tô thẳng.</summary>
    public static (double X, double Y, string Kind)? FaceSnap(DetailingProject p, PlanLevel lv, double x, double y, double tol)
    {
        if (lv == null) return null;
        var edges = new List<(double Ax, double Ay, double Bx, double By)>();
        (double X, double Y, string K)? best = null; double bd = tol;
        void Try(double px, double py, string k)
        {
            double d = Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
            if (d < bd - 1e-12) { bd = d; best = (px, py, k); }
        }
        foreach (var it in lv.Items.Where(i => (i.Kind == PlanKinds.Beam || i.Kind == PlanKinds.Lintel) && !i.IsCurved && i.Length > 1e-3))
        {
            var m = p?.All.FirstOrDefault(q => q.Name == it.Member);
            double h = (m switch { ManualBeam mb when mb.B > 0 => mb.B, ManualLintel ml when ml.Bw > 0 => ml.Bw, _ => 0.22 }) / 2;
            double L = it.Length, ux = (it.X2 - it.X1) / L, uy = (it.Y2 - it.Y1) / L, nx = -uy, ny = ux;
            Try((it.X1 + it.X2) / 2, (it.Y1 + it.Y2) / 2, "giữa dầm");
            foreach (int sgn in new[] { 1, -1 })
            {
                // mép kéo dài thêm h hai đầu để giao được với mép dầm vuông góc tại nút
                double ax = it.X1 + sgn * nx * h - ux * h, ay = it.Y1 + sgn * ny * h - uy * h, bx = it.X2 + sgn * nx * h + ux * h, by = it.Y2 + sgn * ny * h + uy * h;
                edges.Add((ax, ay, bx, by));
                Try(it.X1 + sgn * nx * h, it.Y1 + sgn * ny * h, "đầu mép dầm"); Try(it.X2 + sgn * nx * h, it.Y2 + sgn * ny * h, "đầu mép dầm");
            }
        }
        for (int i = 0; i < edges.Count; i++)
            for (int j = i + 1; j < edges.Count; j++)
            {
                var a = edges[i]; var b = edges[j];
                double rx = a.Bx - a.Ax, ry = a.By - a.Ay, sx = b.Bx - b.Ax, sy = b.By - b.Ay, cr = rx * sy - ry * sx;
                if (Math.Abs(cr) < 1e-9) continue;
                double t = ((b.Ax - a.Ax) * sy - (b.Ay - a.Ay) * sx) / cr, u = ((b.Ax - a.Ax) * ry - (b.Ay - a.Ay) * rx) / cr;
                if (t < -1e-9 || t > 1 + 1e-9 || u < -1e-9 || u > 1 + 1e-9) continue;
                double px = a.Ax + rx * t, py = a.Ay + ry * t;
                double d = Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d <= bd + 1e-9) { bd = d - 1e-9; best = (px, py, "góc mép dầm"); }   // góc mép ưu tiên khi trùng khoảng cách
            }
        if (best != null) return (Math.Round(best.Value.X, 4), Math.Round(best.Value.Y, 4), best.Value.K);
        double bt = tol / 2;
        foreach (var e in edges)
        {
            double L = Math.Sqrt((e.Bx - e.Ax) * (e.Bx - e.Ax) + (e.By - e.Ay) * (e.By - e.Ay));
            double ux = (e.Bx - e.Ax) / L, uy = (e.By - e.Ay) / L;
            double t = Math.Clamp((x - e.Ax) * ux + (y - e.Ay) * uy, 0, L);
            double px = e.Ax + ux * t, py = e.Ay + uy * t, d = Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
            if (d < bt) { bt = d; best = (px, py, "trên mép dầm"); }
        }
        return best == null ? null : (Math.Round(best.Value.X, 4), Math.Round(best.Value.Y, 4), best.Value.K);
    }

    /// <summary>[Cạnh cong 3 điểm] cung tròn từ a qua m đến b, chia n đoạn; trả các điểm sau a (gồm b). Ba điểm thẳng hàng → chỉ b.</summary>
    public static List<(double X, double Y)> Arc3((double X, double Y) a, (double X, double Y) m, (double X, double Y) b, int n = 16)
    {
        double d = 2 * (a.X * (m.Y - b.Y) + m.X * (b.Y - a.Y) + b.X * (a.Y - m.Y));
        if (Math.Abs(d) < 1e-9) return new() { b };
        double a2 = a.X * a.X + a.Y * a.Y, m2 = m.X * m.X + m.Y * m.Y, b2 = b.X * b.X + b.Y * b.Y;
        double cx = (a2 * (m.Y - b.Y) + m2 * (b.Y - a.Y) + b2 * (a.Y - m.Y)) / d;
        double cy = (a2 * (b.X - m.X) + m2 * (a.X - b.X) + b2 * (m.X - a.X)) / d;
        double r = Math.Sqrt((a.X - cx) * (a.X - cx) + (a.Y - cy) * (a.Y - cy));
        double t0 = Math.Atan2(a.Y - cy, a.X - cx), tm = Math.Atan2(m.Y - cy, m.X - cx), t1 = Math.Atan2(b.Y - cy, b.X - cx);
        double Norm(double t) { while (t < 0) t += 2 * Math.PI; while (t >= 2 * Math.PI) t -= 2 * Math.PI; return t; }
        double sw = Norm(t1 - t0), sm = Norm(tm - t0);
        if (sm > sw) sw -= 2 * Math.PI;   // đi chiều kim đồng hồ qua m
        var r2 = new List<(double X, double Y)>();
        for (int i = 1; i <= n; i++)
        {
            double t = t0 + sw * i / n;
            r2.Add(i == n ? b : (Math.Round(cx + r * Math.Cos(t), 4), Math.Round(cy + r * Math.Sin(t), 4)));
        }
        return r2;
    }

    /// <summary>Đa giác có hai cạnh không kề nhau cắt nhau.</summary>
    public static bool SelfIntersects(List<double[]> p)
    {
        int n = p.Count;
        static double Cr(double[] a, double[] b, double[] c) => (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]);
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                if (j == i + 1 || (i == 0 && j == n - 1)) continue;
                var a = p[i]; var b = p[(i + 1) % n]; var c = p[j]; var d = p[(j + 1) % n];
                double d1 = Cr(a, b, c), d2 = Cr(a, b, d), d3 = Cr(c, d, a), d4 = Cr(c, d, b);
                if (((d1 > 1e-9 && d2 < -1e-9) || (d1 < -1e-9 && d2 > 1e-9)) && ((d3 > 1e-9 && d4 < -1e-9) || (d3 < -1e-9 && d4 > 1e-9))) return true;
            }
        return false;
    }

    /// <summary>Đọc toạ độ đỉnh "x,y; x,y …" (m, dấu chấm thập phân; mỗi đỉnh một dòng cũng được). null nếu lỗi hoặc &lt; 3 đỉnh.</summary>
    public static List<double[]> ParseCoords(string t)
    {
        var r = new List<double[]>();
        foreach (var part in (t ?? "").Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var xy = part.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (xy.Length == 0) continue;
            if (xy.Length != 2 || !double.TryParse(xy[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x)
                || !double.TryParse(xy[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double y)) return null;
            r.Add(new[] { x, y });
        }
        return r.Count >= 3 ? r : null;
    }

    /// <summary>Đặt ô sàn theo biên dạng (đa giác): hình bao = X1..Y2. Đa giác là hình chữ nhật song song trục → sàn chữ nhật thường.</summary>
    public static PlanItem PlaceSlab(DetailingProject p, PlanLevel lv, List<double[]> poly, string member = "", bool faces = false)
    {
        double x0 = poly.Min(q => q[0]), x1 = poly.Max(q => q[0]), y0 = poly.Min(q => q[1]), y1 = poly.Max(q => q[1]);
        bool rect = poly.Count == 4 && Math.Abs(Math.Abs(Area(poly)) - (x1 - x0) * (y1 - y0)) < 1e-4;
        var it = p.Plan.Place(p, lv, PlanKinds.Slab, x0, y0, x1, y1, member ?? "");
        if (!rect) it.Outline = poly.Select(q => new[] { q[0], q[1] }).ToList();
        it.Faces = faces;
        if (faces && p.All.FirstOrDefault(x => x.Name == it.Member) is ManualSlab ms && p.Plan.Count(ms.Name) == 1) p.Plan.ApplyGeometry(ms, it, lv);
        return it;
    }
}

/// <summary>[Nắm kéo đầu dầm] Kéo đầu mút dầm / lanh tô (thẳng hoặc cong) để đổi chiều dài; dầm cong giữ tỷ lệ độ võng / dây cung.
/// Kết thúc: dầm chạm dầm thẳng hàng cùng cấu kiện → gộp thành một dầm; cấu kiện chỉ đặt một vị trí → cập nhật nhịp theo hình học.</summary>
public static class PlanGrip
{
    /// <summary>Đầu mút gần điểm (x, y) trong phạm vi tol: 1 = đầu 1, 2 = đầu 2, 0 = không.</summary>
    public static int At(PlanItem it, double x, double y, double tol)
    {
        if (it == null || !PlanKinds.IsSegment(it.Kind)) return 0;
        double d1 = Math.Sqrt((it.X1 - x) * (it.X1 - x) + (it.Y1 - y) * (it.Y1 - y));
        double d2 = Math.Sqrt((it.X2 - x) * (it.X2 - x) + (it.Y2 - y) * (it.Y2 - y));
        if (Math.Min(d1, d2) > tol) return 0;
        return d1 <= d2 ? 1 : 2;
    }

    /// <summary>Đặt đầu mút end tại (x, y). Trả false nếu dầm còn quá ngắn (&lt; 0,1 m) – giữ nguyên.</summary>
    public static bool Drag(PlanItem it, int end, double x, double y)
    {
        if (it == null || end is not (1 or 2)) return false;
        double c0 = it.Length;
        double nx1 = end == 1 ? x : it.X1, ny1 = end == 1 ? y : it.Y1, nx2 = end == 2 ? x : it.X2, ny2 = end == 2 ? y : it.Y2;
        double c1 = Math.Sqrt((nx2 - nx1) * (nx2 - nx1) + (ny2 - ny1) * (ny2 - ny1));
        if (c1 < 0.1) return false;
        if (it.IsCurved && c0 > 1e-6) it.Bulge = Math.Round(it.Bulge * c1 / c0, 4);
        it.X1 = Math.Round(nx1, 4); it.Y1 = Math.Round(ny1, 4); it.X2 = Math.Round(nx2, 4); it.Y2 = Math.Round(ny2, 4);
        return true;
    }

    /// <summary>Hoàn tất kéo: gộp với dầm thẳng hàng cùng cấu kiện nếu chạm, cập nhật nhịp cấu kiện.</summary>
    public static string Finish(DetailingProject p, PlanLevel lv, PlanItem it)
    {
        if (p?.Plan == null || it == null) return "";
        if (it.Kind == PlanKinds.Beam && !it.IsCurved && !string.IsNullOrEmpty(it.Member)
            && PlanBeamMerge.Merge(p, lv, true, new[] { it }) > 0)
            return " – đã nối liền với dầm thẳng hàng cùng tên thành một dầm";
        var m = p.All.FirstOrDefault(x => x.Name == it.Member);
        if (m != null && p.Plan.Count(m.Name) == 1) return " – " + p.Plan.ApplyGeometry(m, it, lv);
        return "";
    }
}
