using KetCauVXT.Model;

namespace KetCauVXT.Mep;

/// <summary>Thông số tường dùng cho điện nước.</summary>
public readonly record struct MepWallInfo(double Total, double Core, double Plaster)
{
    /// <summary>Đường kính lớn nhất chôn được trong tường (m): ½ lõi + 1 lớp trát (rãnh đục không quá nửa chiều dày lõi).</summary>
    public double MaxEmbed => 0.5 * Core + Plaster;
}

/// <summary>Đặt thiết bị, vị trí thiết bị, đi tuyến chôn ngầm trong tường và sinh phụ kiện.</summary>
public static class MepRouting
{
    // ------------------------------------------------------------------
    // Tường, tầng
    // ------------------------------------------------------------------
    public static MepWallInfo WallInfo(Project p, WallLine w)
    {
        var t = p.WallTypes.FirstOrDefault(x => x.Name == w?.Type);
        if (t == null) return new MepWallInfo(0.14, 0.11, 0.015);
        double total = t.TotalThickness, core;
        if (t.IsLayered)
        {
            core = t.Layers.Where(l => l.IsCore && l.Thickness > 0).Sum(l => l.Thickness);
            if (core <= 1e-9) core = total;
        }
        else core = t.Thickness;
        double plaster = Math.Max(0, (total - core) / 2);
        return new MepWallInfo(total, core, plaster);
    }

    public static double StoryElevation(Project p, string story) => p.Stories.FirstOrDefault(s => s.Name == story)?.Elevation ?? 0;

    /// <summary>Chiều cao tầng: tới tầng trên kế tiếp; tầng trên cùng: Story.Height hoặc mặc định.</summary>
    public static double StoryHeight(Project p, string story, MepSettings set = null)
    {
        var st = p.Stories.OrderBy(s => s.Elevation).ToList();
        int i = st.FindIndex(s => s.Name == story);
        if (i >= 0 && i + 1 < st.Count) return st[i + 1].Elevation - st[i].Elevation;
        if (i >= 0 && st[i].Height > 0) return st[i].Height;
        return (set ?? new MepSettings()).DefaultStoryHeight;
    }

    /// <summary>Điểm trên tim tường cách đầu (X1,Y1) một đoạn s (m) (tường cong: lấy theo dây cung).</summary>
    public static (double X, double Y, double Ux, double Uy) PointOnWall(WallLine w, double s)
    {
        double L = Math.Max(1e-9, w.Chord);
        double ux = (w.X2 - w.X1) / L, uy = (w.Y2 - w.Y1) / L;
        return (w.X1 + ux * s, w.Y1 + uy * s, ux, uy);
    }

    /// <summary>Vị trí 3D của thiết bị (mặt tường nếu gắn tường). Wall = null nếu tự do hoặc tường đã bị xoá.</summary>
    public static (double X, double Y, double Z, WallLine Wall) DevicePos(Project p, MepDevice d)
    {
        double z = StoryElevation(p, d.Story) + d.Height;
        var w = d.WallId > 0 ? p.Walls.FirstOrDefault(x => x.Id == d.WallId) : null;
        if (w == null) return (d.X, d.Y, z, null);
        var (x, y, ux, uy) = PointOnWall(w, d.Offset);
        double half = WallInfo(p, w).Total / 2;
        int s = d.Side >= 0 ? 1 : -1;
        return (x - uy * half * s, y + ux * half * s, z, w);
    }

    /// <summary>Điểm tim tường ngay sau thiết bị (nơi tuyến chôn tường bắt đầu).</summary>
    public static (double X, double Y) DeviceChase(Project p, MepDevice d)
    {
        var w = d.WallId > 0 ? p.Walls.FirstOrDefault(x => x.Id == d.WallId) : null;
        if (w == null) return (d.X, d.Y);
        var (x, y, _, _) = PointOnWall(w, d.Offset);
        return (x, y);
    }

    /// <summary>Tường gần nhất trên tầng: (tường, vị trí dọc tường, mặt, khoảng cách tới tim).</summary>
    public static (WallLine Wall, double Offset, int Side, double Dist) NearestWall(Project p, string story, double x, double y, double maxDist = 1.0)
    {
        WallLine best = null; double bo = 0, bd = double.MaxValue; int bs = 1;
        foreach (var w in p.Walls.Where(w => w.Story == story))
        {
            var pr = MepGeom.Project(x, y, w.X1, w.Y1, w.X2, w.Y2);
            if (pr.Dist < bd)
            {
                bd = pr.Dist; best = w; bo = pr.T * w.Chord;
                double cr = (w.X2 - w.X1) * (y - w.Y1) - (w.Y2 - w.Y1) * (x - w.X1);
                bs = cr >= 0 ? 1 : -1;
            }
        }
        if (best == null || bd > maxDist) return (null, 0, 1, bd);
        return (best, bo, bs, bd);
    }

    // ------------------------------------------------------------------
    // Đặt thiết bị
    // ------------------------------------------------------------------
    /// <summary>Đặt thiết bị gắn tường: cao độ theo họ (thiết bị trần: cao tầng − 0,1).</summary>
    public static MepDevice PlaceOnWall(Project p, string family, WallLine w, double offset, int side, double height = double.NaN)
    {
        var m = p.Mep; var fam = m.Library.Find(family) ?? throw new ArgumentException("Không có họ thiết bị '" + family + "'");
        double L = w.Chord;
        double half = Math.Min(fam.W / 2, L / 2);
        var d = new MepDevice
        {
            Id = m.NextDeviceId(), Family = fam.Name, Story = w.Story, WallId = w.Id,
            Offset = Math.Clamp(offset, half, Math.Max(half, L - half)), Side = side >= 0 ? 1 : -1,
            Height = double.IsNaN(height) ? DefaultHeight(p, fam, w.Story) : height
        };
        var pos = DevicePos(p, d); d.X = pos.X; d.Y = pos.Y;
        m.Devices.Add(d);
        return d;
    }

    /// <summary>Đặt thiết bị tự do (sàn / trần) tại (x, y).</summary>
    public static MepDevice PlaceFree(Project p, string family, string story, double x, double y, double height = double.NaN)
    {
        var m = p.Mep; var fam = m.Library.Find(family) ?? throw new ArgumentException("Không có họ thiết bị '" + family + "'");
        var d = new MepDevice
        {
            Id = m.NextDeviceId(), Family = fam.Name, Story = story, WallId = 0, X = x, Y = y,
            Height = double.IsNaN(height) ? DefaultHeight(p, fam, story) : height
        };
        m.Devices.Add(d);
        return d;
    }

    /// <summary>Đặt theo họ: họ gắn tường → bám tường gần nhất (≤ 1 m), không có tường → đặt tự do.</summary>
    public static MepDevice Place(Project p, string family, string story, double x, double y)
    {
        var fam = p.Mep.Library.Find(family) ?? throw new ArgumentException("Không có họ thiết bị '" + family + "'");
        if (fam.WallHosted)
        {
            var nw = NearestWall(p, story, x, y);
            if (nw.Wall != null) return PlaceOnWall(p, family, nw.Wall, nw.Offset, nw.Side);
        }
        return PlaceFree(p, family, story, x, y);
    }

    public static double DefaultHeight(Project p, MepFamily fam, string story)
    {
        // cao độ theo nhóm trong "Cài đặt điện nước" (ghi đè họ; thiết bị trần vẫn theo trần)
        var ch = p.Mep?.Settings?.CategoryHeights?.FirstOrDefault(c => c != null && c.Category == fam.Category);
        if (ch != null && !fam.Ceiling) return ch.Height;
        return fam.Ceiling ? Math.Max(0.1, StoryHeight(p, story, p.Mep.Settings) - 0.1) : fam.MountHeight;
    }

    /// <summary>Thêm tuyến nối 2 thiết bị và đi tuyến ngay.</summary>
    public static MepRun Connect(Project p, int fromId, int toId, MepSystemKind sys, double size = 0, MepRunKind? kind = null)
    {
        var m = p.Mep;
        var a = m.Device(fromId) ?? throw new ArgumentException("Không có thiết bị " + fromId);
        if (m.Device(toId) == null) throw new ArgumentException("Không có thiết bị " + toId);
        var k = kind ?? (MepSystems.IsWater(sys) ? MepRunKind.Ong : MepRunKind.Day);
        if (size <= 0) size = k == MepRunKind.Ong ? (MepSystems.IsDrain(sys) ? 60 : 20) : (sys == MepSystemKind.DienChieuSang ? 1.5 : 2.5);
        var r = new MepRun
        {
            Id = m.NextRunId(), System = sys, Kind = k, Story = a.Story, FromId = fromId, ToId = toId, Size = size,
            Material = k == MepRunKind.Ong ? (MepSystems.IsDrain(sys) ? "uPVC" : "PPR PN20") : "Cu/PVC",
            Conductors = k == MepRunKind.Ong ? 0 : 3
        };
        m.Runs.Add(r);
        Route(p, r);
        return r;
    }

    // ------------------------------------------------------------------
    // Đi tuyến
    // ------------------------------------------------------------------
    /// <summary>Cao độ tuyến ngang của hệ thống trên tầng.</summary>
    public static double RunLevel(Project p, MepSystemKind sys, string story)
    {
        var set = p.Mep.Settings; double el = StoryElevation(p, story);
        if (MepSystems.IsDrain(sys)) return el + set.DrainLevel;
        if (sys == MepSystemKind.DienChieuSang) return el + StoryHeight(p, story, set) - set.CeilingDrop;
        return el + set.RunHeight;
    }

    /// <summary>Tính lại đường đi của tuyến. Trả false nếu không đi được trong tường (đã đi thẳng thay thế).</summary>
    public static bool Route(Project p, MepRun r)
    {
        var m = p.Mep; var a = m.Device(r.FromId); var b = m.Device(r.ToId);
        if (r.FixedPath && r.Path != null && r.Path.Count >= 2) return true;   // tuyến nhập từ Revit: giữ nguyên hình học gốc
        r.Path = new List<MepPt>();
        if (a == null || b == null) return false;
        // thoát nước: đi dưới sàn có độ dốc tới ống đứng (MepDrainage) – không đi trong tường trừ khi ép (DrainInWall)
        if (MepSystems.IsDrain(r.System)) return MepDrainage.Route(p, r, a, b);
        var pa = DevicePos(p, a); var pb = DevicePos(p, b);
        double zr = RunLevel(p, r.System, a.Story);
        var ca = DeviceChase(p, a); var cb = DeviceChase(p, b);
        bool ok = true;

        if (r.ManualPoints != null && r.ManualPoints.Count > 0)
        {
            r.Path.Add(new MepPt(ca.X, ca.Y, pa.Z, a.WallId));
            AddIfNew(r.Path, new MepPt(ca.X, ca.Y, zr, a.WallId));
            foreach (var q in r.ManualPoints.Where(q => q != null && q.Length >= 2))
            {
                var nw = NearestWall(p, a.Story, q[0], q[1], 0.05);
                AddIfNew(r.Path, new MepPt(q[0], q[1], zr, nw.Wall?.Id ?? 0));
            }
            AddIfNew(r.Path, new MepPt(cb.X, cb.Y, zr, b.WallId));
            AddIfNew(r.Path, new MepPt(cb.X, cb.Y, pb.Z, b.WallId));
            Simplify(r.Path);
            return true;
        }

        bool drain = MepSystems.IsDrain(r.System);
        // điểm vào mạng tường của mỗi đầu: thiết bị gắn tường = chân thiết bị trên tim tường; thiết bị tự do = điểm tường gần nhất (≤ 3 m) – đoạn nối đi trong sàn / trần
        var ea = Entry(p, a, ca, drain); var eb = Entry(p, b, cb, drain);
        r.Path.Add(new MepPt(ca.X, ca.Y, pa.Z, a.WallId));
        if (drain)
        {
            // thoát nước: xuống trong tường tới cao độ thoát, đi ngang trong sàn (gấp khúc vuông góc)
            AddIfNew(r.Path, new MepPt(ca.X, ca.Y, zr, a.WallId));
            Orthogonal(r.Path, ca.X, ca.Y, cb.X, cb.Y, zr);
            AddIfNew(r.Path, new MepPt(cb.X, cb.Y, pb.Z, b.WallId));
            Simplify(r.Path);
            return true;
        }
        AddIfNew(r.Path, new MepPt(ca.X, ca.Y, zr, a.WallId));
        List<(double X, double Y, int Wall)> mid = null;
        if (ea.Wall != null && eb.Wall != null) mid = WallPath(p, a.Story, ea.Wall, ea.S, eb.Wall, eb.S);
        if (mid == null)
        {
            ok = a.WallId == 0 && b.WallId == 0;   // hai thiết bị tự do: đi thẳng trong trần / sàn là bình thường
            Orthogonal(r.Path, ca.X, ca.Y, cb.X, cb.Y, zr);
        }
        else
        {
            if (a.WallId == 0) Orthogonal(r.Path, ca.X, ca.Y, mid[0].X, mid[0].Y, zr);
            foreach (var q in mid) AddIfNew(r.Path, new MepPt(q.X, q.Y, zr, q.Wall));
            if (b.WallId == 0) Orthogonal(r.Path, mid[^1].X, mid[^1].Y, cb.X, cb.Y, zr);
            else AddIfNew(r.Path, new MepPt(cb.X, cb.Y, zr, b.WallId));
        }
        AddIfNew(r.Path, new MepPt(cb.X, cb.Y, pb.Z, b.WallId));
        Simplify(r.Path);
        return ok;
    }

    private static (WallLine Wall, double S) Entry(Project p, MepDevice d, (double X, double Y) c, bool drain)
    {
        if (d.WallId > 0)
        {
            var w = p.Walls.FirstOrDefault(x => x.Id == d.WallId);
            if (w != null) return (w, Math.Clamp(d.Offset, 0, w.Chord));
        }
        var nw = NearestWall(p, d.Story, c.X, c.Y, 3.0);
        return nw.Wall == null ? (null, 0) : (nw.Wall, nw.Offset);
    }

    private static void AddIfNew(List<MepPt> path, MepPt q)
    {
        if (path.Count > 0 && MepGeom.Dist(path[^1], q) < 1e-6) return;
        path.Add(q);
    }

    /// <summary>Đi vuông góc (X trước, rồi Y) ngoài tường (trong sàn / trần).</summary>
    private static void Orthogonal(List<MepPt> path, double x1, double y1, double x2, double y2, double z)
    {
        AddIfNew(path, new MepPt(x1, y1, z, 0));
        if (Math.Abs(x2 - x1) > 1e-6 && Math.Abs(y2 - y1) > 1e-6) AddIfNew(path, new MepPt(x2, y1, z, 0));
        AddIfNew(path, new MepPt(x2, y2, z, 0));
    }

    /// <summary>Bỏ điểm thẳng hàng và điểm trùng.</summary>
    public static void Simplify(List<MepPt> path)
    {
        for (int i = path.Count - 2; i >= 1; i--)
        {
            var a = path[i - 1]; var b = path[i]; var c = path[i + 1];
            if (MepGeom.Dist(a, b) < 1e-6) { path.RemoveAt(i); continue; }
            double v1x = b.X - a.X, v1y = b.Y - a.Y, v1z = b.Z - a.Z, v2x = c.X - b.X, v2y = c.Y - b.Y, v2z = c.Z - b.Z;
            double cx = v1y * v2z - v1z * v2y, cy = v1z * v2x - v1x * v2z, cz = v1x * v2y - v1y * v2x;
            double dot = v1x * v2x + v1y * v2y + v1z * v2z;
            if (Math.Sqrt(cx * cx + cy * cy + cz * cz) < 1e-9 && dot > 0) path.RemoveAt(i);
        }
    }

    /// <summary>Đường ngắn nhất theo tim các tường liền nhau (Dijkstra) từ điểm s1 trên tường w1 tới s2 trên w2. null = không nối được.</summary>
    public static List<(double X, double Y, int Wall)> WallPath(Project p, string story, WallLine w1, double s1, WallLine w2, double s2)
    {
        var walls = p.Walls.Where(w => w.Story == story && w.Chord > 1e-6).ToList();
        if (!walls.Contains(w1)) walls.Add(w1);
        if (!walls.Contains(w2)) walls.Add(w2);
        const double tol = 0.03;
        var nodes = new List<(double X, double Y)>();
        int NodeOf(double x, double y)
        {
            for (int i = 0; i < nodes.Count; i++) if (MepGeom.Dist2D(nodes[i].X, nodes[i].Y, x, y) < tol) return i;
            nodes.Add((x, y)); return nodes.Count - 1;
        }
        // các điểm chia trên mỗi tường: 2 đầu, giao với tường khác, điểm của thiết bị
        var cuts = walls.ToDictionary(w => w, w => new List<double> { 0, w.Chord });
        foreach (var a in walls)
            foreach (var b in walls)
            {
                if (ReferenceEquals(a, b)) continue;
                foreach (var (ex, ey) in new[] { (b.X1, b.Y1), (b.X2, b.Y2) })
                {
                    var pr = MepGeom.Project(ex, ey, a.X1, a.Y1, a.X2, a.Y2);
                    if (pr.Dist < tol) cuts[a].Add(pr.T * a.Chord);
                }
                var ix = Intersect(a, b);
                if (ix.HasValue) cuts[a].Add(ix.Value);
            }
        cuts[w1].Add(s1); cuts[w2].Add(s2);
        var adj = new Dictionary<int, List<(int To, double L, int Wall)>>();
        void Edge(int i, int j, double L, int wall)
        {
            if (i == j) return;
            if (!adj.TryGetValue(i, out var li)) adj[i] = li = new();
            if (!adj.TryGetValue(j, out var lj)) adj[j] = lj = new();
            li.Add((j, L, wall)); lj.Add((i, L, wall));
        }
        foreach (var w in walls)
        {
            var ss = cuts[w].Select(s => Math.Clamp(s, 0, w.Chord)).OrderBy(s => s).ToList();
            for (int k = 1; k < ss.Count; k++)
            {
                if (ss[k] - ss[k - 1] < 1e-6) continue;
                var pa = PointOnWall(w, ss[k - 1]); var pb = PointOnWall(w, ss[k]);
                Edge(NodeOf(pa.X, pa.Y), NodeOf(pb.X, pb.Y), ss[k] - ss[k - 1], w.Id);
            }
        }
        var A = PointOnWall(w1, s1); var B = PointOnWall(w2, s2);
        int src = NodeOf(A.X, A.Y), dst = NodeOf(B.X, B.Y);
        if (src == dst) return new List<(double, double, int)> { (A.X, A.Y, w1.Id) };
        var dist = new Dictionary<int, double> { [src] = 0 };
        var prev = new Dictionary<int, (int From, int Wall)>();
        var done = new HashSet<int>();
        while (true)
        {
            int u = -1; double best = double.MaxValue;
            foreach (var kv in dist) if (!done.Contains(kv.Key) && kv.Value < best) { best = kv.Value; u = kv.Key; }
            if (u < 0) return null;
            if (u == dst) break;
            done.Add(u);
            if (!adj.TryGetValue(u, out var list)) continue;
            foreach (var (to, L, wall) in list)
            {
                double nd = best + L;
                if (!dist.TryGetValue(to, out double old) || nd < old - 1e-12) { dist[to] = nd; prev[to] = (u, wall); }
            }
        }
        var res = new List<(double X, double Y, int Wall)>();
        int cur = dst;
        while (cur != src)
        {
            var (from, wall) = prev[cur];
            res.Add((nodes[cur].X, nodes[cur].Y, wall));
            cur = from;
        }
        res.Add((nodes[src].X, nodes[src].Y, w1.Id));
        res.Reverse();
        return res;
    }

    /// <summary>Giao điểm trong (không phải đầu mút) của 2 tường – trả vị trí dọc tường a.</summary>
    private static double? Intersect(WallLine a, WallLine b)
    {
        double x1 = a.X1, y1 = a.Y1, x2 = a.X2, y2 = a.Y2, x3 = b.X1, y3 = b.Y1, x4 = b.X2, y4 = b.Y2;
        double den = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(den) < 1e-12) return null;
        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / den;
        double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / den;
        if (t < -1e-9 || t > 1 + 1e-9 || u < -1e-9 || u > 1 + 1e-9) return null;
        return t * a.Chord;
    }

    // ------------------------------------------------------------------
    // Phụ kiện
    // ------------------------------------------------------------------
    /// <summary>Phụ kiện dọc một tuyến: cút 90° / chếch 45° tại điểm gấp, măng sông (khớp nối) theo chiều dài cây.
    /// Dây đơn / cáp: tính cho ống gen luồn dây (cỡ ConduitSize).</summary>
    public static List<MepFitting> RunFittings(Project p, MepRun r)
    {
        if (r.Kind == MepRunKind.Ong && MepSystems.IsDrain(r.System)) return MepDrainage.RunFittings(p, r);
        var res = new List<MepFitting>();
        var path = r.Path;
        if (path == null || path.Count < 2) return res;
        bool elec = r.Kind != MepRunKind.Ong;
        double size = elec ? (r.ConduitSize > 0 ? r.ConduitSize : MepSizing.ConduitFor(r.Size, Math.Max(1, r.Conductors))) : r.Size;
        for (int i = 1; i < path.Count - 1; i++)
        {
            double ang = TurnAngle(path[i - 1], path[i], path[i + 1]);
            if (ang < 10) continue;
            var k = ang < 60 ? MepFittingKind.Chech45 : MepFittingKind.Cut90;
            res.Add(new MepFitting { Kind = k, System = r.System, Size = size, X = path[i].X, Y = path[i].Y, Z = path[i].Z, RunId = r.Id, Story = r.Story });
        }
        double stock = elec ? p.Mep.Settings.StockConduit : p.Mep.Settings.StockPipe;
        for (int i = 1; i < path.Count; i++)
        {
            double L = MepGeom.Dist(path[i - 1], path[i]);
            int n = stock > 0 ? (int)Math.Floor((L - 1e-6) / stock) : 0;
            for (int k = 1; k <= n; k++)
            {
                double t = k * stock / L;
                res.Add(new MepFitting
                {
                    Kind = MepFittingKind.MangSong, System = r.System, Size = size, RunId = r.Id, Story = r.Story,
                    X = path[i - 1].X + t * (path[i].X - path[i - 1].X), Y = path[i - 1].Y + t * (path[i].Y - path[i - 1].Y), Z = path[i - 1].Z + t * (path[i].Z - path[i - 1].Z)
                });
            }
        }
        return res;
    }

    /// <summary>Góc đổi hướng (độ) tại b.</summary>
    public static double TurnAngle(MepPt a, MepPt b, MepPt c)
    {
        double v1x = b.X - a.X, v1y = b.Y - a.Y, v1z = b.Z - a.Z, v2x = c.X - b.X, v2y = c.Y - b.Y, v2z = c.Z - b.Z;
        double l1 = Math.Sqrt(v1x * v1x + v1y * v1y + v1z * v1z), l2 = Math.Sqrt(v2x * v2x + v2y * v2y + v2z * v2z);
        if (l1 < 1e-9 || l2 < 1e-9) return 0;
        double cos = Math.Clamp((v1x * v2x + v1y * v2y + v1z * v2z) / (l1 * l2), -1, 1);
        return Math.Acos(cos) * 180 / Math.PI;
    }

    /// <summary>Phụ kiện tại thiết bị / điểm nối: ≥ 3 tuyến cùng hệ → tê (khác cỡ: tê giảm) – điện: hộp nối;
    /// 2 tuyến cùng hệ khác cỡ tại điểm nối ống → côn thu.</summary>
    public static List<MepFitting> NodeFittings(Project p)
    {
        var m = p.Mep; var res = new List<MepFitting>();
        foreach (var d in m.Devices)
        {
            foreach (var g in m.Runs.Where(r => r.FromId == d.Id || r.ToId == d.Id).GroupBy(r => r.System))
            {
                var runs = g.ToList();
                if (MepSystems.IsDrain(g.Key)) { res.AddRange(MepDrainage.NodeFittings(p, d, runs, g.Key)); continue; }
                bool elec = MepSystems.IsElectric(g.Key);
                var pos = DevicePos(p, d); var c = DeviceChase(p, d);
                if (runs.Count >= 3)
                {
                    if (elec)
                    {
                        res.Add(new MepFitting { Kind = MepFittingKind.HopNoi, System = g.Key, X = c.X, Y = c.Y, Z = pos.Z, DeviceId = d.Id, Story = d.Story });
                        continue;
                    }
                    double big = runs.Max(r => r.Size), small = runs.Min(r => r.Size);
                    for (int k = 0; k < runs.Count - 2; k++)
                        res.Add(new MepFitting
                        {
                            Kind = Math.Abs(big - small) < 1e-6 ? MepFittingKind.Te : MepFittingKind.TeGiam, System = g.Key, Size = big, Size2 = small,
                            X = c.X, Y = c.Y, Z = pos.Z, DeviceId = d.Id, Story = d.Story
                        });
                }
                else if (runs.Count == 2 && !elec && Math.Abs(runs[0].Size - runs[1].Size) > 1e-6)
                {
                    res.Add(new MepFitting
                    {
                        Kind = MepFittingKind.Con, System = g.Key, Size = Math.Max(runs[0].Size, runs[1].Size), Size2 = Math.Min(runs[0].Size, runs[1].Size),
                        X = c.X, Y = c.Y, Z = pos.Z, DeviceId = d.Id, Story = d.Story
                    });
                }
            }
        }
        res.AddRange(MepDrainage.StackBaseCleanouts(p));
        return res;
    }

    /// <summary>Đi lại mọi tuyến và tính lại toàn bộ phụ kiện (lưu vào Mep.Fittings).</summary>
    public static List<MepFitting> RebuildAll(Project p)
    {
        var m = p.Mep;
        foreach (var d in m.Devices)
        {
            var pos = DevicePos(p, d);
            if (pos.Wall != null) { d.X = pos.X; d.Y = pos.Y; }
        }
        foreach (var r in m.Runs) Route(p, r);
        var all = new List<MepFitting>();
        foreach (var r in m.Runs) all.AddRange(RunFittings(p, r));
        all.AddRange(NodeFittings(p));
        m.Fittings = all;
        return all;
    }
}
