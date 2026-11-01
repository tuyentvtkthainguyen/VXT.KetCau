using System.Globalization;
using KetCauVXT.Model;

namespace KetCauVXT.Import;

/// <summary>Báo cáo kết quả nhập mô hình (DXF / Revit).</summary>
public class ImportReport
{
    public string Source { get; set; } = "";
    public int Grids { get; set; }
    public int Stories { get; set; }
    public int Columns { get; set; }
    public int Beams { get; set; }
    public int Slabs { get; set; }
    public int Nodes { get; set; }
    public int Frames { get; set; }
    public int SplitCount { get; set; }
    public List<string> Warnings { get; } = new();
    public List<string> Info { get; } = new();

    public override string ToString()
    {
        var s = $"Nhập từ {Source}:\n" +
                $"- Trục: {Grids}\n- Tầng: {Stories}\n- Cột: {Columns}\n- Dầm: {Beams}\n- Ô sàn: {Slabs}\n" +
                $"- Tổng số nút: {Nodes}, tổng số thanh: {Frames} (chia tại nút: {SplitCount})";
        if (Info.Count > 0) s += "\n\nThông tin:\n- " + string.Join("\n- ", Info);
        if (Warnings.Count > 0) s += "\n\nCảnh báo:\n- " + string.Join("\n- ", Warnings.Take(40))
                                     + (Warnings.Count > 40 ? $"\n... và {Warnings.Count - 40} cảnh báo khác" : "");
        return s;
    }
}

/// <summary>Đoạn thẳng trên mặt bằng (m).</summary>
public struct Seg2
{
    public double Ax, Ay, Bx, By;
    public Seg2(double ax, double ay, double bx, double by) { Ax = ax; Ay = ay; Bx = bx; By = by; }
    public double Length => Math.Sqrt((Bx - Ax) * (Bx - Ax) + (By - Ay) * (By - Ay));
    public bool IsAlongX(double tol = 0.02) => Math.Abs(By - Ay) < tol && Math.Abs(Bx - Ax) >= tol;
    public bool IsAlongY(double tol = 0.02) => Math.Abs(Bx - Ax) < tol && Math.Abs(By - Ay) >= tol;
}

/// <summary>Các hàm hình học dùng chung cho bộ nhập.</summary>
public static class ImportGeometry
{
    public static string Fmt(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>Tên tiết diện chữ nhật theo quy ước của chương trình: kích thước cm, vd "C22x30", "D22x40".</summary>
    public static string RectName(string prefix, double bM, double hM) =>
        $"{prefix}{Fmt(Math.Round(bM * 1000) / 10)}x{Fmt(Math.Round(hM * 1000) / 10)}";

    /// <summary>Làm tròn kích thước (m) tới bội số step mm.</summary>
    public static double RoundMm(double m, double stepMm = 5) => Math.Round(m * 1000 / stepMm) * stepMm / 1000.0;

    /// <summary>Tìm hoặc tạo tiết diện chữ nhật BTCT b×h (m).</summary>
    public static string EnsureRect(Project p, string prefix, double b, double h, string material)
    {
        foreach (var s in p.FrameSections)
            if (s.Shape == SectionShape.RectConcrete && s.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && Math.Abs(s.B - b) < 1e-4 && Math.Abs(s.H - h) < 1e-4)
                return s.Name;
        string name = RectName(prefix, b, h);
        string n = name; int k = 2;
        while (p.FrameSections.Any(s => s.Name == n)) n = name + "_" + k++;
        p.FrameSections.Add(FrameSection.Rect(n, b * 1000, h * 1000, string.IsNullOrEmpty(material) ? "B25" : material));
        p.InvalidateIndex();
        return n;
    }

    public static string EnsureSlabSection(Project p, double t, string material)
    {
        foreach (var s in p.SlabSections) if (Math.Abs(s.Thickness - t) < 1e-4) return s.Name;
        string name = "S" + Math.Round(t * 1000).ToString(CultureInfo.InvariantCulture);
        p.SlabSections.Add(new SlabSection { Name = name, Thickness = t, Material = string.IsNullOrEmpty(material) ? "B25" : material });
        return name;
    }

    /// <summary>Gộp các giá trị gần nhau (sai số tol) và sắp xếp tăng dần.</summary>
    public static List<double> Cluster(IEnumerable<double> values, double tol)
    {
        var res = new List<double>();
        foreach (var v in values.OrderBy(v => v))
        {
            if (res.Count > 0 && Math.Abs(v - res[^1]) <= tol) continue;
            res.Add(v);
        }
        return res;
    }

    /// <summary>Đoạn [from,to] có được phủ kín bởi các dầm song song trục (along X: dầm có y = c) không.</summary>
    public static bool Covered(List<Seg2> segs, bool alongX, double c, double from, double to, double tol = 0.03)
    {
        var iv = new List<(double a, double b)>();
        foreach (var s in segs)
        {
            if (alongX && s.IsAlongX() && Math.Abs((s.Ay + s.By) / 2 - c) < tol)
                iv.Add((Math.Min(s.Ax, s.Bx), Math.Max(s.Ax, s.Bx)));
            else if (!alongX && s.IsAlongY() && Math.Abs((s.Ax + s.Bx) / 2 - c) < tol)
                iv.Add((Math.Min(s.Ay, s.By), Math.Max(s.Ay, s.By)));
        }
        double cur = from;
        foreach (var (a, b) in iv.OrderBy(x => x.a))
        {
            if (a > cur + tol) break;
            cur = Math.Max(cur, b);
            if (cur >= to - tol) return true;
        }
        return cur >= to - tol;
    }

    /// <summary>Chia vùng chữ nhật [x0,x1]×[y0,y1] thành các ô sàn chữ nhật được bao bởi dầm.
    /// Các ô lưới nhỏ nhất (theo toạ độ dầm) được gộp lại khi cạnh chung không có dầm.
    /// requireClosed = true: bỏ các vùng có cạnh biên không có dầm (vùng trống ngoài nhà).
    /// Vùng gộp không phải chữ nhật: trả về từng ô nhỏ (kèm cảnh báo).</summary>
    public static List<(double x0, double y0, double x1, double y1)> RectCells(
        List<Seg2> beams, double x0, double y0, double x1, double y1, bool requireClosed, List<string> warn, string tag)
    {
        var result = new List<(double, double, double, double)>();
        var xs = Cluster(beams.Where(b => b.IsAlongY()).Select(b => (b.Ax + b.Bx) / 2)
                    .Where(x => x > x0 + 0.05 && x < x1 - 0.05).Append(x0).Append(x1), 0.03);
        var ys = Cluster(beams.Where(b => b.IsAlongX()).Select(b => (b.Ay + b.By) / 2)
                    .Where(y => y > y0 + 0.05 && y < y1 - 0.05).Append(y0).Append(y1), 0.03);
        int nx = xs.Count - 1, ny = ys.Count - 1;
        if (nx < 1 || ny < 1) return result;

        var parent = new int[nx * ny];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[b] = a; }
        int Id(int i, int j) => j * nx + i;

        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                if (i + 1 < nx && !Covered(beams, false, xs[i + 1], ys[j], ys[j + 1])) Union(Id(i, j), Id(i + 1, j));
                if (j + 1 < ny && !Covered(beams, true, ys[j + 1], xs[i], xs[i + 1])) Union(Id(i, j), Id(i, j + 1));
            }

        var open = new HashSet<int>();
        if (requireClosed)
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    bool o = (i == 0 && !Covered(beams, false, xs[0], ys[j], ys[j + 1]))
                          || (i == nx - 1 && !Covered(beams, false, xs[nx], ys[j], ys[j + 1]))
                          || (j == 0 && !Covered(beams, true, ys[0], xs[i], xs[i + 1]))
                          || (j == ny - 1 && !Covered(beams, true, ys[ny], xs[i], xs[i + 1]));
                    if (o) open.Add(Find(Id(i, j)));
                }

        foreach (var g in Enumerable.Range(0, nx * ny).GroupBy(k => Find(k)))
        {
            if (open.Contains(g.Key)) continue;
            var cells = g.Select(id => (i: id % nx, j: id / nx)).ToList();
            int i0 = cells.Min(c => c.i), i1 = cells.Max(c => c.i), j0 = cells.Min(c => c.j), j1 = cells.Max(c => c.j);
            if (cells.Count == (i1 - i0 + 1) * (j1 - j0 + 1))
                result.Add((xs[i0], ys[j0], xs[i1 + 1], ys[j1 + 1]));
            else
            {
                warn?.Add($"{tag}: vùng sàn không phải hình chữ nhật – chia thành {cells.Count} ô nhỏ theo lưới dầm.");
                foreach (var (i, j) in cells) result.Add((xs[i], ys[j], xs[i + 1], ys[j + 1]));
            }
        }
        return result;
    }

    /// <summary>Hình chiếu điểm lên đoạn: trả về tham số t ∈ [0,1] và khoảng cách vuông góc.</summary>
    public static (double t, double dist) ProjectPoint(Seg2 s, double px, double py)
    {
        double dx = s.Bx - s.Ax, dy = s.By - s.Ay, L2 = dx * dx + dy * dy;
        if (L2 < 1e-12) return (0, Math.Sqrt((px - s.Ax) * (px - s.Ax) + (py - s.Ay) * (py - s.Ay)));
        double t = ((px - s.Ax) * dx + (py - s.Ay) * dy) / L2;
        double qx = s.Ax + t * dx, qy = s.Ay + t * dy;
        return (t, Math.Sqrt((px - qx) * (px - qx) + (py - qy) * (py - qy)));
    }

    /// <summary>Giao điểm thực sự (nằm trong cả hai đoạn, không trùng đầu mút).</summary>
    public static bool Intersect(Seg2 a, Seg2 b, out double x, out double y, double endTol = 0.02)
    {
        x = y = 0;
        double d1x = a.Bx - a.Ax, d1y = a.By - a.Ay, d2x = b.Bx - b.Ax, d2y = b.By - b.Ay;
        double den = d1x * d2y - d1y * d2x;
        double la = a.Length, lb = b.Length;
        if (la < 1e-9 || lb < 1e-9 || Math.Abs(den) < 1e-9 * la * lb * 100) return false;
        double t = ((b.Ax - a.Ax) * d2y - (b.Ay - a.Ay) * d2x) / den;
        double u = ((b.Ax - a.Ax) * d1y - (b.Ay - a.Ay) * d1x) / den;
        double ea = endTol / la, eb = endTol / lb;
        if (t <= ea || t >= 1 - ea || u <= eb || u >= 1 - eb) return false;
        x = a.Ax + t * d1x; y = a.Ay + t * d1y;
        return true;
    }

    /// <summary>Kiểm tra đa giác là chữ nhật song song trục, trả về bao.</summary>
    public static bool IsAxisRect(List<(double X, double Y)> pts, out double x0, out double y0, out double x1, out double y1, double tol = 0.01)
    {
        var q = RemoveCollinear(pts, tol);
        x0 = pts.Min(p => p.X); x1 = pts.Max(p => p.X); y0 = pts.Min(p => p.Y); y1 = pts.Max(p => p.Y);
        if (q.Count != 4) return false;
        double ax0 = x0, ax1 = x1, ay0 = y0, ay1 = y1;
        return q.All(p => (Math.Abs(p.X - ax0) < tol || Math.Abs(p.X - ax1) < tol) && (Math.Abs(p.Y - ay0) < tol || Math.Abs(p.Y - ay1) < tol));
    }

    /// <summary>Bỏ các đỉnh trùng hoặc thẳng hàng.</summary>
    public static List<(double X, double Y)> RemoveCollinear(List<(double X, double Y)> pts, double tol = 0.01)
    {
        var q = new List<(double X, double Y)>();
        foreach (var p in pts)
            if (q.Count == 0 || Math.Abs(p.X - q[^1].X) > tol || Math.Abs(p.Y - q[^1].Y) > tol) q.Add(p);
        if (q.Count > 1 && Math.Abs(q[0].X - q[^1].X) <= tol && Math.Abs(q[0].Y - q[^1].Y) <= tol) q.RemoveAt(q.Count - 1);
        bool changed = true;
        while (changed && q.Count > 3)
        {
            changed = false;
            for (int i = 0; i < q.Count; i++)
            {
                var a = q[(i - 1 + q.Count) % q.Count]; var b = q[i]; var c = q[(i + 1) % q.Count];
                double cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                double l = Math.Sqrt((c.X - a.X) * (c.X - a.X) + (c.Y - a.Y) * (c.Y - a.Y));
                if (l < 1e-9 || Math.Abs(cross) / l < tol) { q.RemoveAt(i); changed = true; break; }
            }
        }
        return q;
    }

    /// <summary>Diện tích đại số (dương khi các đỉnh ngược chiều kim đồng hồ).</summary>
    public static double SignedArea(List<(double X, double Y)> pts)
    {
        double a = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i]; var q = pts[(i + 1) % pts.Count];
            a += p.X * q.Y - q.X * p.Y;
        }
        return a / 2;
    }

    /// <summary>Thêm diện tích tải hoàn thiện và hoạt tải cho ô sàn (như ModelBuilder.BuildFromGrid).</summary>
    public static void AddSlabLoads(Project p, Slab slab, bool roof, double finish, double live, double roofLive)
    {
        if (finish > 0 && p.Pattern("HTHT") != null)
            p.AreaLoads.Add(new AreaLoad { Slab = slab.Id, Pattern = "HTHT", Q = finish, Note = "Hoàn thiện" });
        double q = roof ? roofLive : live;
        if (q > 0 && p.Pattern("HT") != null)
            p.AreaLoads.Add(new AreaLoad { Slab = slab.Id, Pattern = "HT", Q = q, Note = roof ? "Hoạt tải mái" : "Hoạt tải sàn" });
    }

    /// <summary>Đảm bảo có các trường hợp tải cơ bản TT, HTHT, HT.</summary>
    public static void EnsurePatterns(Project p)
    {
        if (p.Pattern("TT") == null) p.Patterns.Add(new LoadPattern { Name = "TT", Type = LoadType.Dead, SelfWeight = 1, Gamma = 1.1 });
        if (p.Pattern("HTHT") == null) p.Patterns.Add(new LoadPattern { Name = "HTHT", Type = LoadType.SuperDead, Gamma = 1.2 });
        if (p.Pattern("HT") == null) p.Patterns.Add(new LoadPattern { Name = "HT", Type = LoadType.Live, Gamma = 1.2 });
    }

    /// <summary>Thêm thanh nếu chưa có thanh nào nối đúng 2 nút này.</summary>
    public static FrameElement AddFrame(Project p, Node a, Node b, FrameKind kind, string sec, string story, string label, string group, double angle = 0)
    {
        if (a.Id == b.Id) return null;
        if (p.Frames.Any(f => (f.NodeI == a.Id && f.NodeJ == b.Id) || (f.NodeI == b.Id && f.NodeJ == a.Id))) return null;
        var fr = new FrameElement
        {
            Id = p.NextFrameId(), NodeI = a.Id, NodeJ = b.Id, Kind = kind, Section = sec, Story = story,
            Label = label, DesignGroup = group, Angle = angle
        };
        p.Frames.Add(fr);
        return fr;
    }

    public static void ClearModel(Project p)
    {
        p.Nodes.Clear(); p.Frames.Clear(); p.Slabs.Clear(); p.Footings.Clear();
        p.FrameLoads.Clear(); p.NodeLoads.Clear(); p.AreaLoads.Clear();
        p.InvalidateIndex();
    }

    /// <summary>Tên trục tự động: X → 1,2,3…; Y → A,B,C… (sau Z: AA, AB…).</summary>
    public static string AutoName(GridDir d, int index)
    {
        if (d == GridDir.X) return (index + 1).ToString(CultureInfo.InvariantCulture);
        string s = "";
        int k = index;
        do { s = (char)('A' + k % 26) + s; k = k / 26 - 1; } while (k >= 0);
        return s;
    }
}
