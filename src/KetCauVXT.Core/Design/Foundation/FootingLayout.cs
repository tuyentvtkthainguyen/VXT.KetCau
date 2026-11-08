using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

// =====================================================================
// [MóngHìnhDạng] NHÂN BẢN MÓNG / ĐỒNG NHẤT MÓNG GIỐNG NHAU (giao diện: App/MainWindow.FootingLayout.cs)
//   • CopyTo: chép kích thước (chốt L × B × H) và bố trí (dóng mép cột, góc, đa giác so với tim cột) của một móng sang các chân cột khác
//     → thiết kế lại cho kết quả giống hệt (gộp chung một tên móng nếu nội lực cho phép).
//   • Unify: gom các móng đơn có kích thước gần nhau (chênh ≤ dung sai) và cùng tiết diện cột thành một loại, lấy kích thước lớn nhất.
// =====================================================================
public static class FootingLayout
{
    public sealed record CopyResult(int Targets, int Sizes, int Placements);

    /// <summary>Kích thước chữ nhật dùng để chốt của móng đã thiết kế (trước khi cắt theo đường chuẩn).</summary>
    public static (double L, double B, double H) SizeOf(Project p, DesignSummary d, int node)
    {
        var ft = p.Footings.FirstOrDefault(f => f.NodeId == node);
        var fd = d?.Footings.FirstOrDefault(f => f.NodeId == node);
        double L = ft?.L > 0 ? ft.L : fd == null ? 0 : fd.RectL > 0 ? fd.RectL : fd.Shaped ? 0 : fd.L;
        double B = ft?.B > 0 ? ft.B : fd == null ? 0 : fd.RectB > 0 ? fd.RectB : fd.Shaped ? 0 : fd.B;
        double H = ft?.Height > 0 ? ft.Height : fd?.H ?? 0;
        return (L, B, H);
    }

    /// <summary>Nhân bản móng tại chân cột src sang các chân cột targets.</summary>
    public static CopyResult CopyTo(Project p, DesignSummary d, int src, IEnumerable<int> targets, bool copySize = true, bool copyPlacement = true)
    {
        var (L, B, H) = SizeOf(p, d, src);
        var srcFt = p.Footings.FirstOrDefault(f => f.NodeId == src);
        var srcPl = ShapedFooting.PlacementOf(p, src);
        int nt = 0, ns = 0, np = 0;
        foreach (int t in targets.Distinct())
        {
            if (t == src) continue;
            nt++;
            if (copySize && (L > 0 || B > 0 || H > 0))
            {
                FoundationReset.Clear(p, null, new[] { t });   // giữ kết quả thiết kế đang xem (sẽ thiết kế lại)
                FoundationReset.SetSize(p, t, L, B, H, srcFt?.Type);
                ns++;
            }
            if (copyPlacement)
            {
                p.Foundation.Placements.RemoveAll(x => x.NodeId == t);
                if (srcPl != null)
                {
                    var c = srcPl.Clone();
                    c.NodeId = t;
                    p.Foundation.Placements.Add(c);
                    np++;
                }
            }
        }
        return new CopyResult(nt, ns, np);
    }

    /// <summary>Một nhóm móng đồng nhất: các chân cột và kích thước chung.</summary>
    public sealed record UnifyGroup(List<int> Nodes, double L, double B, double H);

    /// <summary>Gom các móng đơn có cạnh chênh ≤ tol (m), cùng tiết diện cột (sameColumn) – lấy kích thước lớn nhất của nhóm; chỉ ghi nhóm ≥ 2 móng.
    /// nodes: chỉ xét các chân cột này (null = mọi móng đơn). Móng đa giác không gộp.</summary>
    public static List<UnifyGroup> Unify(Project p, DesignSummary d, double tol = 0.3, bool sameColumn = true, IEnumerable<int> nodes = null, bool apply = true)
    {
        var res = new List<UnifyGroup>();
        if (d == null) return res;
        var set = nodes == null ? null : new HashSet<int>(nodes);
        var items = new List<(int Node, string Col, double L, double B, double H)>();
        foreach (var f in d.Footings)
        {
            if (f.Eccentric?.Combined != null) continue;
            var pl = ShapedFooting.PlacementOf(p, f.NodeId);
            if (pl != null && pl.IsPolygon) continue;
            var (L, B, H) = SizeOf(p, d, f.NodeId);
            if (L <= 0 || B <= 0) continue;
            foreach (int n in AllNodes(d, f))
            {
                if (set != null && !set.Contains(n)) continue;
                string col = sameColumn ? ColumnSection(p, n) : "";
                items.Add((n, col, L, B, H));
            }
        }
        // lớn trước: móng nhỏ hơn nhập vào nhóm của móng lớn gần nó
        var groups = new List<(string Col, double L, double B, double H, List<int> Nodes)>();
        foreach (var it in items.OrderByDescending(x => x.L * x.B).ThenByDescending(x => x.H))
        {
            int k = groups.FindIndex(g => g.Col == it.Col && it.L >= g.L - tol - 1e-9 && it.B >= g.B - tol - 1e-9 && it.L <= g.L + 1e-9 && it.B <= g.B + 1e-9);
            if (k >= 0) { var g = groups[k]; g.Nodes.Add(it.Node); groups[k] = (g.Col, g.L, g.B, Math.Max(g.H, it.H), g.Nodes); }
            else groups.Add((it.Col, it.L, it.B, it.H, new List<int> { it.Node }));
        }
        res.AddRange(groups.Select(g => new UnifyGroup(g.Nodes, g.L, g.B, g.H)));
        res = res.Where(g => g.Nodes.Count >= 2).ToList();
        if (apply)
            foreach (var g in res)
                foreach (int n in g.Nodes)
                {
                    var ft = p.Footings.FirstOrDefault(f => f.NodeId == n);
                    FoundationReset.Clear(p, null, new[] { n });
                    FoundationReset.SetSize(p, n, g.L, g.B, g.H, ft?.Type);
                }
        return res;
    }

    /// <summary>Mọi chân cột dùng chung kết quả móng f (móng gộp giống hệt có Quantity &gt; 1: nút lấy từ RebarKeys "M:id").</summary>
    public static IEnumerable<int> AllNodes(DesignSummary d, FootingDesign f)
    {
        var res = new List<int> { f.NodeId };
        foreach (var k in f.RebarKeys)
            if (k.StartsWith("M:") && int.TryParse(k.Substring(2), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int n) && !res.Contains(n))
                res.Add(n);
        return res;
    }

    private static readonly System.Reflection.MethodInfo CloneM =
        typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    /// <summary>Mọi vị trí đặt của một loại móng đơn: móng gộp giống hệt (Quantity &gt; 1) chỉ lưu một bản ghi trong kết quả thiết kế –
    /// trả về bản sao (nông) tại từng chân cột để vẽ mặt bằng, 3D, bản vẽ bố trí. Móng lệch tâm tự vẽ các vị trí (Eccentric.Locations).</summary>
    public static List<FootingDesign> Instances(Project p, FootingDesign f)
    {
        var res = new List<FootingDesign> { f };
        if (f == null || f.Eccentric != null || p == null) return res;
        foreach (int id in AllNodes(null, f))
        {
            if (id == f.NodeId) continue;
            var n = p.Nodes.FirstOrDefault(x => x.Id == id);
            if (n == null) continue;
            var c = (FootingDesign)CloneM.Invoke(f, null);
            double dx = n.X - f.X, dy = n.Y - f.Y;
            c.NodeId = id; c.X = n.X; c.Y = n.Y;
            var col = p.Frames.FirstOrDefault(x => x.Kind == FrameKind.Column && (x.NodeI == id || x.NodeJ == id));
            if (col != null) c.ColumnFrame = col.Id;
            if (f.Outline != null) c.Outline = f.Outline.Select(v => new[] { v[0] + dx, v[1] + dy }).ToList();
            c.Gx = f.Gx + dx; c.Gy = f.Gy + dy;
            res.Add(c);
        }
        return res;
    }

    /// <summary>Mọi vị trí đặt của mọi móng đơn trong kết quả thiết kế.</summary>
    public static List<FootingDesign> AllInstances(Project p, DesignSummary s) =>
        s?.Footings == null ? new List<FootingDesign>() : s.Footings.SelectMany(f => Instances(p, f)).ToList();

    private static string ColumnSection(Project p, int node)
    {
        var col = p.Frames.FirstOrDefault(f => f.Kind == FrameKind.Column && (f.NodeI == node || f.NodeJ == node));
        return col?.Section ?? "";
    }

    /// <summary>Chân cột móng đơn cùng tiết diện cột với chân cột src.</summary>
    public static List<int> SameColumnNodes(Project p, int src)
    {
        string s = ColumnSection(p, src);
        return FoundationUtil.BaseColumns(p).Where(x => x.Item2.Section == s && x.Item1.Id != src
                && FoundationUtil.TypeOf(p, x.Item1.Id) == FoundationType.Isolated).Select(x => x.Item1.Id).ToList();
    }

    /// <summary>Mọi chân cột móng đơn (trừ src).</summary>
    public static List<int> AllIsolatedNodes(Project p, int src = 0) =>
        FoundationUtil.BaseColumns(p).Where(x => x.Item1.Id != src && FoundationUtil.TypeOf(p, x.Item1.Id) == FoundationType.Isolated).Select(x => x.Item1.Id).ToList();
}
