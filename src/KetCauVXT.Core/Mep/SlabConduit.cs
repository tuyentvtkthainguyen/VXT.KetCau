using KetCauVXT.Model;

namespace KetCauVXT.Mep;

/// <summary>Đi tuyến ống gen điện trong SÀN / TRẦN (mặc định): đoạn ngang nằm giữa chiều dày sàn (ổ cắm, điều hoà thấp → sàn dưới chân;
/// chiếu sáng, thiết bị cao → sàn trên / trần), chỉ đi ĐỨNG trong tường tới thiết bị. Dò tuyến trực giao (A*) trên mặt bằng
/// tránh tiết diện cột (nở thêm khe hở) và dải bề rộng dầm. Chỉ được qua dầm trong chiều dày sàn khi sàn ≥ 100 mm, ống gen ≤ Ø25
/// và điểm qua nằm trong 1/3 giữa nhịp dầm (cách gối ≥ L/3); không đạt → không qua (vòng hoặc đi theo tường + cảnh báo).
/// Thiết bị nằm trên tường đặt trên dầm (hoặc dưới dầm): ống đi đứng tới lớp cán nền / dưới đáy dầm rồi chuyển ra khỏi bề rộng dầm.</summary>
public static class SlabConduit
{
    /// <summary>Dải dầm trên mặt bằng (tim I→J, nửa bề rộng đã nở), cao độ đỉnh / đáy.</summary>
    public sealed record BeamStrip(int FrameId, string Label, double X1, double Y1, double X2, double Y2, double Half, double Top, double Bottom)
    {
        public double Length => MepGeom.Dist2D(X1, Y1, X2, Y2);
        /// <summary>Tham số dọc dầm t ∈ [0,1]; null nếu điểm ngoài dải.</summary>
        public double? Hit(double x, double y)
        {
            var pr = MepGeom.Project(x, y, X1, Y1, X2, Y2);   // T kẹp [0,1]: vùng tròn quanh đầu dầm (nút giao dầm) cũng bị chặn
            if (pr.Dist >= Half - 1e-9) return null;
            return pr.T;
        }
    }

    /// <summary>Hình chữ nhật cột trên mặt bằng (đã nở).</summary>
    public sealed record ColRect(int FrameId, double X1, double Y1, double X2, double Y2)
    {
        public bool Inside(double x, double y) => x > X1 + 1e-9 && x < X2 - 1e-9 && y > Y1 + 1e-9 && y < Y2 - 1e-9;
    }

    /// <summary>Mặt phẳng đi ống gen: cao độ tim Z, mặt trên sàn Top, chiều dày sàn T (0 = nền không có sàn), sàn trên (Ceiling).</summary>
    public sealed record Plane(double Z, double Top, double T, bool Ceiling, bool HasSlab, string SlabStory);

    public static bool Applies(Project p, MepRun r) =>
        (p.Mep?.Settings?.ConduitRoute ?? ConduitRouteMode.TranSan) == ConduitRouteMode.TranSan && MepSystems.IsElectric(r.System)
        && r.Kind is MepRunKind.Day or MepRunKind.OngGen;

    /// <summary>Tầng kế trên (theo cao độ).</summary>
    public static Story Above(Project p, string story)
    {
        double el = MepRouting.StoryElevation(p, story);
        return p.Stories.Where(s => s.Elevation > el + 1e-6).OrderBy(s => s.Elevation).FirstOrDefault();
    }

    /// <summary>Đi trong sàn trên (trần) hay sàn dưới chân: chiếu sáng → trần; còn lại theo cao độ trung bình 2 thiết bị (&gt; ½ tầng → trần).</summary>
    public static bool UseCeiling(Project p, MepRun r, MepDevice a, MepDevice b)
    {
        if (r.System == MepSystemKind.DienChieuSang) return true;
        double H = MepRouting.StoryHeight(p, a.Story, p.Mep.Settings);
        return (a.Height + b.Height) / 2 > H / 2 + 1e-9;
    }

    public static Plane PlaneFor(Project p, string story, bool ceiling, double x, double y)
    {
        double el = MepRouting.StoryElevation(p, story);
        if (ceiling)
        {
            var up = Above(p, story);
            double top = up?.Elevation ?? el + MepRouting.StoryHeight(p, story, p.Mep.Settings);
            string ss = up?.Name ?? story;
            bool has = up != null && p.Slabs.Any(s => s.Story == up.Name);
            double t = MepDrainage.SlabThicknessAt(p, ss, x, y);
            return new Plane(top - t / 2, top, t, true, has, ss);
        }
        bool hasF = p.Slabs.Any(s => s.Story == story);
        if (!hasF && MepDrainage.IsLowestStory(p, story)) return new Plane(el - 0.05, el, 0, false, false, story);   // nền: ống gen trong lớp bê tông lót / cán nền
        double tf = MepDrainage.SlabThicknessAt(p, story, x, y);
        return new Plane(el - tf / 2, el, tf, false, hasF, story);
    }

    private static (double B, double H) Dims(Project p, FrameElement f)
    {
        var sec = p.FrameSections.FirstOrDefault(s => s.Name == f.Section);
        return (sec?.B > 0 ? sec.B : 0.22, sec?.H > 0 ? sec.H : (f.IsVerticalKind ? 0.22 : 0.3));
    }

    /// <summary>Cột cắt qua cao độ z (nở inflate).</summary>
    public static List<ColRect> Columns(Project p, double z, double inflate)
    {
        var res = new List<ColRect>();
        foreach (var f in p.Frames.Where(f => f.IsVerticalKind))
        {
            Node ni, nj;
            try { ni = p.Node(f.NodeI); nj = p.Node(f.NodeJ); } catch { continue; }
            if (ni == null || nj == null) continue;
            if (z < Math.Min(ni.Z, nj.Z) - 1e-6 || z > Math.Max(ni.Z, nj.Z) + 1e-6) continue;
            var (b, h) = Dims(p, f);
            if (Math.Abs(Math.Sin(f.Angle * Math.PI / 180)) > 0.7071) (b, h) = (h, b);
            double cx = ni.X + f.ColOffsetX, cy = ni.Y + f.ColOffsetY;
            res.Add(new ColRect(f.Id, cx - b / 2 - inflate, cy - h / 2 - inflate, cx + b / 2 + inflate, cy + h / 2 + inflate));
        }
        return res;
    }

    /// <summary>Dầm có chiều cao chồng lên dải [z − rad, z + rad] (nửa bề rộng nở inflate).</summary>
    public static List<BeamStrip> Beams(Project p, double z, double rad, double inflate)
    {
        var res = new List<BeamStrip>();
        foreach (var f in p.Frames.Where(f => !f.IsVerticalKind))
        {
            Node ni, nj;
            try { ni = p.Node(f.NodeI); nj = p.Node(f.NodeJ); } catch { continue; }
            if (ni == null || nj == null) continue;
            var (b, h) = Dims(p, f);
            double zt = Math.Max(ni.Z, nj.Z), zb = zt - h;
            if (z + rad <= zb + 1e-6 || z - rad >= zt - 1e-6) continue;
            res.Add(new BeamStrip(f.Id, string.IsNullOrWhiteSpace(f.Label) ? "D" + f.Id : f.Label, ni.X, ni.Y, nj.X, nj.Y, b / 2 + inflate, zt, zb));
        }
        return res;
    }

    /// <summary>Ống gen Ø dia (mm) được qua dầm trong chiều dày sàn T (m) không (không xét vị trí dọc nhịp)?</summary>
    public static bool CrossAllowed(MepStdOptions o, double slabT, double dia) =>
        slabT >= MepStandards.Get(o, "gen.san_min") - 1e-9 && dia <= MepStandards.Get(o, "gen.d_qua_dam") + 1e-9;

    /// <summary>Vị trí dọc nhịp t (0…1) cho phép qua: 1/3 giữa nhịp (cách gối ≥ L/3).</summary>
    public static bool InMiddleThird(double t) => t >= 1.0 / 3 - 1e-9 && t <= 2.0 / 3 + 1e-9;

    /// <summary>Đi tuyến ống gen trong sàn / trần. false = không đi được (người gọi đi theo tường + ghi chú).</summary>
    public static bool Route(Project p, MepRun r, MepDevice a, MepDevice b)
    {
        var set = p.Mep.Settings; var o = set.Standards ??= new MepStdOptions();
        var pa = MepRouting.DevicePos(p, a); var pb = MepRouting.DevicePos(p, b);
        var ca = MepRouting.DeviceChase(p, a); var cb = MepRouting.DeviceChase(p, b);
        bool ceil = UseCeiling(p, r, a, b);
        var pl = PlaneFor(p, a.Story, ceil, (ca.X + cb.X) / 2, (ca.Y + cb.Y) / 2);
        double dia = StructuralImpact.RunDia(r), rad = dia / 2000;
        double clear = MepStandards.Get(o, "gen.cach_cot");
        var cols = Columns(p, pl.Z, rad + clear);
        var beams = Beams(p, pl.Z, rad, rad + 0.02);
        bool canCross = pl.HasSlab && CrossAllowed(o, pl.T, dia);

        var ea = Exit(p, a, ca, cols, beams); var eb = Exit(p, b, cb, cols, beams);
        if (ea == null || eb == null) { r.RouteNote = $"Tuyến {r.Id}: thiết bị nằm trong vùng cột / dầm – không chuyển ống gen ra khỏi kết cấu được"; return false; }
        var mid = Grid(p, set, ea.Value.X, ea.Value.Y, eb.Value.X, eb.Value.Y, cols, beams, canCross);
        if (mid == null)
        {
            r.RouteNote = $"Tuyến {r.Id} ({MepSystems.Name(r.System)}) Ø{dia:0}: không có đường trong {(ceil ? "sàn trên / trần" : "sàn")} tránh cột – dầm" +
                          (pl.HasSlab && !canCross ? $" (sàn {pl.T * 1000:0} mm / Ø{dia:0}: không được qua dầm – cần sàn ≥ {MepStandards.Get(o, "gen.san_min") * 1000:0} mm và Ø ≤ {MepStandards.Get(o, "gen.d_qua_dam"):0})" : "") +
                          " – đã đi theo tường";
            return false;
        }

        var path = new List<MepPt>();
        void Add(MepPt q) { if (path.Count == 0 || MepGeom.Dist(path[^1], q) > 1e-6) path.Add(q); }
        // đầu A: đứng trong tường → (chuyển ra khỏi dầm) → vào sàn
        End(p, pl, a, pa.Z, ca, ea.Value, beams, Add);
        foreach (var q in mid) Add(new MepPt(q.X, q.Y, pl.Z, 0));
        var tail = new List<MepPt>();
        End(p, pl, b, pb.Z, cb, eb.Value, beams, q => tail.Add(q));
        tail.Reverse();
        // quy ước: đoạn thuộc tường của điểm CUỐI đoạn → đi ngược chiều thì dời WallId lên một điểm
        for (int k = tail.Count - 1; k >= 1; k--) tail[k] = new MepPt(tail[k].X, tail[k].Y, tail[k].Z, tail[k - 1].WallId);
        if (tail.Count > 0) tail[0] = new MepPt(tail[0].X, tail[0].Y, tail[0].Z, 0);
        foreach (var q in tail) Add(q);
        Simplify(path);
        r.Path = path;
        r.RouteNote = null;
        return true;
    }

    /// <summary>Bỏ điểm thẳng hàng như MepRouting.Simplify nhưng GIỮ điểm đổi tường / ra khỏi tường (đoạn đứng trong tường tới mặt sàn
    /// tách khỏi đoạn trong chiều dày sàn – để rãnh tường chỉ tính phần trong tường).</summary>
    public static void Simplify(List<MepPt> path)
    {
        for (int i = path.Count - 2; i >= 1; i--)
        {
            var a = path[i - 1]; var b = path[i]; var c = path[i + 1];
            if (MepGeom.Dist(a, b) < 1e-6) { path.RemoveAt(i); continue; }
            if (b.WallId != c.WallId) continue;
            double v1x = b.X - a.X, v1y = b.Y - a.Y, v1z = b.Z - a.Z, v2x = c.X - b.X, v2y = c.Y - b.Y, v2z = c.Z - b.Z;
            double cx = v1y * v2z - v1z * v2y, cy = v1z * v2x - v1x * v2z, cz = v1x * v2y - v1y * v2x;
            double dot = v1x * v2x + v1y * v2y + v1z * v2z;
            if (Math.Sqrt(cx * cx + cy * cy + cz * cz) < 1e-9 && dot > 0) path.RemoveAt(i);
        }
    }

    /// <summary>Đoạn đầu tuyến từ thiết bị tới mặt phẳng sàn (theo thứ tự đi ra từ thiết bị).</summary>
    private static void End(Project p, Plane pl, MepDevice d, double zDev, (double X, double Y) c, (double X, double Y, bool Moved) e,
        List<BeamStrip> beams, Action<MepPt> add)
    {
        int wid = d.WallId;
        add(new MepPt(c.X, c.Y, zDev, wid));
        // mặt sàn kề tường: rãnh đứng trong tường chỉ tới mặt sàn (sàn dưới) / đáy sàn (trần)
        double face = pl.Ceiling ? pl.Top - pl.T : pl.Top;
        if (e.Moved)
        {
            double zx;
            if (pl.Ceiling)
            {
                double bot = beams.Where(s => s.Hit(c.X, c.Y) != null).Select(s => s.Bottom).DefaultIfEmpty(face).Min();
                zx = Math.Min(face, bot) - 0.03;
            }
            else zx = pl.Top + Math.Max(0.01, p.Mep.Settings.ScreedLevel);
            add(new MepPt(c.X, c.Y, zx, wid));
            add(new MepPt(e.X, e.Y, zx, 0));
            add(new MepPt(e.X, e.Y, pl.Z, 0));
            return;
        }
        if (wid > 0 && (pl.Ceiling ? zDev < face - 1e-6 : zDev > face + 1e-6)) add(new MepPt(c.X, c.Y, face, wid));
        add(new MepPt(c.X, c.Y, pl.Z, 0));
    }

    /// <summary>Điểm vào sàn của thiết bị: chân thiết bị nếu không nằm trong cột / dầm; ngược lại dời theo pháp tuyến tường (phía thiết bị) /
    /// theo trục cho thiết bị tự do tới điểm gần nhất ra khỏi kết cấu. null = không tìm được trong 1,5 m.</summary>
    private static (double X, double Y, bool Moved)? Exit(Project p, MepDevice d, (double X, double Y) c, List<ColRect> cols, List<BeamStrip> beams)
    {
        bool Bad(double x, double y) => cols.Any(k => k.Inside(x, y)) || beams.Any(s => s.Hit(x, y) != null);
        if (!Bad(c.X, c.Y)) return (c.X, c.Y, false);
        var dirs = new List<(double X, double Y)>();
        var w = d.WallId > 0 ? p.Walls.FirstOrDefault(x => x.Id == d.WallId) : null;
        if (w != null)
        {
            var (_, _, ux, uy) = MepRouting.PointOnWall(w, d.Offset);
            int side = d.Side == 0 ? 1 : Math.Sign(d.Side);
            dirs.Add((-uy * side, ux * side)); dirs.Add((uy * side, -ux * side));
        }
        else dirs.AddRange(new[] { (1.0, 0.0), (-1.0, 0.0), (0.0, 1.0), (0.0, -1.0) });
        for (double s = 0.05; s <= 1.5 + 1e-9; s += 0.05)
            foreach (var (dx, dy) in dirs)
            {
                double x = c.X + dx * s, y = c.Y + dy * s;
                if (!Bad(x, y)) return (x, y, true);
            }
        return null;
    }

    private static List<double> Axis(double a, double b, double lo, double hi, double step)
    {
        var v = new List<double> { a, b };
        for (double t = a; t >= lo - 1e-9; t -= step) v.Add(t);
        for (double t = a + step; t <= hi + 1e-9; t += step) v.Add(t);
        v.Sort();
        var res = new List<double>();
        foreach (double t in v) if (res.Count == 0 || t - res[^1] > 1e-6) res.Add(t);
        return res;
    }

    /// <summary>Dò tuyến trực giao A* (x1,y1) → (x2,y2) trên mặt phẳng sàn. Ô trong cột: cấm; ô trong dải dầm: cấm, trừ khi canCross và
    /// điểm nằm trong 1/3 giữa nhịp (phạt ConduitBeamPenalty mỗi lần vào dải). Trả các điểm góc (gồm 2 đầu) hoặc null.</summary>
    public static List<(double X, double Y)> Grid(Project p, MepSettings set, double x1, double y1, double x2, double y2,
        List<ColRect> cols, List<BeamStrip> beams, bool canCross)
    {
        if (MepGeom.Dist2D(x1, y1, x2, y2) < 1e-6) return new List<(double, double)> { (x1, y1) };
        foreach (double margin in new[] { 2.0, 6.0 })
        {
            var res = Search(set, x1, y1, x2, y2, cols, beams, canCross, margin);
            if (res != null) return res;
        }
        return null;
    }

    private static List<(double X, double Y)> Search(MepSettings set, double x1, double y1, double x2, double y2,
        List<ColRect> cols, List<BeamStrip> beams, bool canCross, double margin)
    {
        double step = Math.Max(0.02, set.ConduitRouteStep);
        double lox = Math.Min(x1, x2) - margin, hix = Math.Max(x1, x2) + margin, loy = Math.Min(y1, y2) - margin, hiy = Math.Max(y1, y2) + margin;
        List<double> xs, ys;
        while (true)
        {
            xs = Axis(x1, x2, lox, hix, step); ys = Axis(y1, y2, loy, hiy, step);
            if ((long)xs.Count * ys.Count <= 200000) break;
            step *= 1.5;
        }
        int nx = xs.Count, ny = ys.Count;
        int si = xs.FindIndex(v => Math.Abs(v - x1) < 1e-6), sj = ys.FindIndex(v => Math.Abs(v - y1) < 1e-6);
        int ei = xs.FindIndex(v => Math.Abs(v - x2) < 1e-6), ej = ys.FindIndex(v => Math.Abs(v - y2) < 1e-6);
        // 0 = tự do, 1 = cấm, 2 = trong dải dầm được qua
        int Cls(double x, double y)
        {
            if (cols.Any(k => k.Inside(x, y))) return 1;
            int c = 0;
            foreach (var s in beams)
            {
                var t = s.Hit(x, y);
                if (t == null) continue;
                if (!canCross || !InMiddleThird(t.Value)) return 1;
                c = 2;
            }
            return c;
        }
        var cls = new byte[nx * ny];
        for (int j = 0; j < ny; j++) for (int i = 0; i < nx; i++) cls[j * nx + i] = (byte)Cls(xs[i], ys[j]);
        cls[sj * nx + si] = 0; cls[ej * nx + ei] = 0;

        int[] di = { 1, -1, 0, 0 }, dj = { 0, 0, 1, -1 };
        int S = nx * ny * 5;
        var g = new double[S]; var prev = new int[S]; var done = new bool[S];
        for (int k = 0; k < S; k++) { g[k] = double.MaxValue; prev[k] = -1; }
        var pq = new PriorityQueue<int, double>();
        int start = (sj * nx + si) * 5 + 4;
        g[start] = 0; pq.Enqueue(start, 0);
        int goal = -1;
        double H(int i, int j) => Math.Abs(xs[i] - x2) + Math.Abs(ys[j] - y2);
        while (pq.Count > 0)
        {
            int st = pq.Dequeue();
            if (done[st]) continue;
            done[st] = true;
            int cell = st / 5, dir = st % 5, ci = cell % nx, cj = cell / nx;
            if (ci == ei && cj == ej) { goal = st; break; }
            for (int d = 0; d < 4; d++)
            {
                if (dir < 4 && (d ^ 1) == dir) continue;
                int ni = ci + di[d], nj = cj + dj[d];
                if (ni < 0 || nj < 0 || ni >= nx || nj >= ny) continue;
                int nc = nj * nx + ni;
                if (cls[nc] == 1) continue;
                double ax = xs[ci], ay = ys[cj], bx = xs[ni], by = ys[nj];
                int cm = Cls((ax + bx) / 2, (ay + by) / 2);
                if (cm == 1) continue;
                double len = Math.Abs(bx - ax) + Math.Abs(by - ay);
                double cost = len * (cm == 2 ? 3 : 1);
                if (dir < 4 && dir != d) cost += set.ConduitBendPenalty;
                if (cls[cell] == 0 && cls[nc] == 2) cost += set.ConduitBeamPenalty;
                int ns = nc * 5 + d;
                double ng = g[st] + cost;
                if (ng < g[ns] - 1e-12) { g[ns] = ng; prev[ns] = st; pq.Enqueue(ns, ng + H(ni, nj)); }
            }
        }
        if (goal < 0) return null;
        var cells = new List<(double X, double Y)>();
        for (int st = goal; st >= 0; st = prev[st]) { int c = st / 5; cells.Add((xs[c % nx], ys[c / nx])); }
        cells.Reverse();
        var pts = new List<(double X, double Y)> { cells[0] };
        for (int k = 1; k < cells.Count - 1; k++)
        {
            var a = pts[^1]; var b = cells[k]; var c = cells[k + 1];
            bool col = Math.Abs((b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X)) < 1e-9;
            if (!col) pts.Add(b);
        }
        if (cells.Count > 1) pts.Add(cells[^1]);
        return pts;
    }

    // ------------------------------------------------------------------
    // Kiểm tra ống gen trong sàn (gọi từ StructuralImpact.Scan)
    // ------------------------------------------------------------------
    /// <summary>Sàn chứa cao độ z tại (x,y): (tầng, cao độ mặt sàn, chiều dày); null nếu z không nằm trong chiều dày sàn nào.</summary>
    public static (string Story, double Top, double T)? SlabAtZ(Project p, double x, double y, double z, double rad = 0)
    {
        foreach (var st in p.Stories)
        {
            if (!p.Slabs.Any(s => s.Story == st.Name)) continue;
            double t = MepDrainage.SlabThicknessAt(p, st.Name, x, y);
            if (z + rad <= st.Elevation + 1e-6 && z - rad >= st.Elevation - t - 1e-6) return (st.Name, st.Elevation, t);
        }
        return null;
    }

    /// <summary>Hạng mục kiểm tra kết cấu cho ống gen điện đi trong sàn: qua dầm (điều kiện sàn ≥ 100, Ø ≤ 25, 1/3 giữa nhịp),
    /// Ø &gt; 1/3 chiều dày sàn, đoạn ngang trong tường (khi chọn đi trần / sàn), ghi chú không đi được trong sàn.</summary>
    public static List<StructuralImpactItem> Check(Project p, MepRun r)
    {
        var list = new List<StructuralImpactItem>();
        var path = r.Path; if (path == null || path.Count < 2 || !MepSystems.IsElectric(r.System) || r.Kind == MepRunKind.Ong) return list;
        var o = p.Mep.Settings.Standards ?? new MepStdOptions();
        double dia = StructuralImpact.RunDia(r), rad = dia / 2000;
        const string refS = "TCVN 9207:2012; TCVN 5574:2018 (cấu tạo sàn)";
        if (!string.IsNullOrEmpty(r.RouteNote))
            list.Add(new StructuralImpactItem
            {
                Code = "ONG_GEN_TRAN_SAN", Severity = ImpactSeverity.CanhBao, Story = r.Story, X = path[0].X, Y = path[0].Y, Z = path[0].Z, RunId = r.Id, Dia = dia,
                Message = r.RouteNote, Rule = "Ống gen ưu tiên đi trong trần / sàn, không qua cột, dầm", Reference = refS,
                Proposal = "Dời thiết bị / thêm hộp chia để tuyến vòng qua cột – dầm, hoặc đi ống nổi trong trần giả"
            });
        bool slabMode = Applies(p, r);
        bool thinDone = false; var beamDone = new HashSet<int>();
        for (int i = 1; i < path.Count; i++)
        {
            var a = path[i - 1]; var c = path[i];
            double planL = MepGeom.Dist2D(a.X, a.Y, c.X, c.Y);
            if (planL < 1e-6) continue;
            if (c.WallId > 0)
            {
                if (slabMode && planL > 0.3 + 1e-9)
                    list.Add(new StructuralImpactItem
                    {
                        Code = "ONG_GEN_NGANG_TUONG", Severity = ImpactSeverity.GoiY, Story = r.Story, X = (a.X + c.X) / 2, Y = (a.Y + c.Y) / 2, Z = c.Z, RunId = r.Id, WallId = c.WallId, Dia = dia,
                        Message = $"Tuyến {r.Id} Ø{dia:0}: đoạn ngang {planL:0.##} m đi trong tường {c.WallId} (đã chọn tuyến trần / sàn)",
                        Rule = "Ống gen đi ngang trong sàn / trần, chỉ đi đứng trong tường", Reference = refS, Proposal = "Đi lại tuyến (bỏ điểm tay) để đi trong sàn / trần"
                    });
                continue;
            }
            double zm = (a.Z + c.Z) / 2;
            var sl = SlabAtZ(p, (a.X + c.X) / 2, (a.Y + c.Y) / 2, zm, rad);
            if (sl == null) continue;
            if (!thinDone && dia / 1000 > sl.Value.T * MepStandards.Get(o, "gen.ty_san") + 1e-9)
            {
                thinDone = true;
                list.Add(new StructuralImpactItem
                {
                    Code = "SAN_MONG", Severity = ImpactSeverity.CanhBao, Story = sl.Value.Story, X = (a.X + c.X) / 2, Y = (a.Y + c.Y) / 2, Z = zm, RunId = r.Id, Dia = dia,
                    Message = $"Tuyến {r.Id}: ống gen Ø{dia:0} chôn trong sàn dày {sl.Value.T * 1000:0} mm > 1/3 chiều dày ({sl.Value.T / 3 * 1000:0} mm)",
                    Rule = "Ø ống chôn sàn ≤ 1/3 chiều dày sàn, đặt giữa 2 lớp thép", Reference = refS,
                    Proposal = "Giảm Ø ống gen (tách mạch) hoặc đi trong lớp cán nền / trần giả"
                });
            }
            foreach (var s in Beams(p, zm, rad, rad))
            {
                if (beamDone.Contains(s.FrameId)) continue;
                if (!MepGeom.SegmentsIntersect(a.X, a.Y, c.X, c.Y, s.X1, s.Y1, s.X2, s.Y2)) continue;
                if (zm + rad > s.Top + 1e-6 || zm - rad < s.Top - sl.Value.T - 1e-6) continue;   // không nằm trong chiều dày sàn → để XUYEN_DAM xét
                beamDone.Add(s.FrameId);
                var pr = Cross(a, c, s);
                bool ok = CrossAllowed(o, sl.Value.T, dia) && InMiddleThird(pr.T);
                var why = new List<string>();
                if (sl.Value.T < MepStandards.Get(o, "gen.san_min") - 1e-9) why.Add($"sàn {sl.Value.T * 1000:0} mm < {MepStandards.Get(o, "gen.san_min") * 1000:0} mm");
                if (dia > MepStandards.Get(o, "gen.d_qua_dam") + 1e-9) why.Add($"Ø{dia:0} > Ø{MepStandards.Get(o, "gen.d_qua_dam"):0}");
                if (!InMiddleThird(pr.T)) why.Add($"cách gối {Math.Min(pr.T, 1 - pr.T) * s.Length:0.##} m < L/3 = {s.Length / 3:0.##} m");
                list.Add(new StructuralImpactItem
                {
                    Code = "ONG_GEN_QUA_DAM", Severity = ok ? ImpactSeverity.GoiY : ImpactSeverity.CanhBao, Story = sl.Value.Story, X = pr.X, Y = pr.Y, Z = zm, RunId = r.Id, FrameId = s.FrameId, Dia = dia,
                    Message = $"Tuyến {r.Id} Ø{dia:0} qua dầm {s.Label} trong chiều dày sàn {sl.Value.T * 1000:0} mm" + (ok ? " – đạt (1/3 giữa nhịp)" : ": " + string.Join("; ", why)),
                    Rule = "Chỉ qua dầm trong chiều dày sàn khi sàn ≥ 100 mm, Ø ≤ 25, cách gối ≥ L/3", Reference = refS,
                    Proposal = ok ? "Đặt ống gen giữa lớp thép trên của dầm, buộc cố định trước khi đổ bê tông" : "Đổi tuyến: vòng qua đoạn giữa nhịp dầm hoặc đi đứng theo tường xuống / lên tầng"
                });
            }
        }
        return list;
    }

    private static (double X, double Y, double T) Cross(MepPt a, MepPt c, BeamStrip s)
    {
        double rX = c.X - a.X, rY = c.Y - a.Y, sX = s.X2 - s.X1, sY = s.Y2 - s.Y1;
        double den = rX * sY - rY * sX;
        double u = Math.Abs(den) < 1e-12 ? 0.5 : Math.Clamp(((s.X1 - a.X) * sY - (s.Y1 - a.Y) * sX) / den, 0, 1);
        double x = a.X + u * rX, y = a.Y + u * rY;
        var pr = MepGeom.Project(x, y, s.X1, s.Y1, s.X2, s.Y2);
        return (x, y, pr.T);
    }
}
