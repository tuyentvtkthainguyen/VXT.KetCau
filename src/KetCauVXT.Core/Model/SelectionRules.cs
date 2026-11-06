namespace KetCauVXT.Model;

// =====================================================================
// CHỌN NHIỀU ĐỐI TƯỢNG trên mặt bằng kiểu AutoCAD (toạ độ mô hình, m):
//   • khung Window (kéo trái → phải): đối tượng nằm TRỌN trong khung; Crossing (phải → trái): đối tượng CHẠM khung;
//   • dầm = đoạn thẳng I–J, cột = điểm tâm (± nửa kích thước khi crossing), ô sàn = đa giác theo nút;
//   • chọn tương tự: cùng tiết diện / cùng loại / cùng tầng (sàn: cùng tiết diện, cùng công năng / cấu tạo).
// =====================================================================

public enum SelectFilter { All, Beams, Columns, Slabs, Footings }

public enum SimilarBy { Section, Kind, Story }

public static class SelectionRules
{
    public readonly record struct Box(double X0, double Y0, double X1, double Y1)
    {
        public static Box Of(double xa, double ya, double xb, double yb) => new(Math.Min(xa, xb), Math.Min(ya, yb), Math.Max(xa, xb), Math.Max(ya, yb));
        public bool Contains(double x, double y) => x >= X0 - 1e-9 && x <= X1 + 1e-9 && y >= Y0 - 1e-9 && y <= Y1 + 1e-9;
    }

    /// <summary>Đoạn thẳng có chạm hình chữ nhật không (Liang–Barsky).</summary>
    public static bool SegmentHits(Box r, double ax, double ay, double bx, double by)
    {
        if (r.Contains(ax, ay) || r.Contains(bx, by)) return true;
        double t0 = 0, t1 = 1, dx = bx - ax, dy = by - ay;
        bool Clip(double pp, double q)
        {
            if (Math.Abs(pp) < 1e-12) return q >= 0;
            double t = q / pp;
            if (pp < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
            else { if (t < t0) return false; if (t < t1) t1 = t; }
            return true;
        }
        return Clip(-dx, ax - r.X0) && Clip(dx, r.X1 - ax) && Clip(-dy, ay - r.Y0) && Clip(dy, r.Y1 - ay) && t0 <= t1;
    }

    public static bool PointInPolygon(IReadOnlyList<(double X, double Y)> poly, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    /// <summary>Đa tuyến (mở) trong khung: window = mọi điểm trong khung; crossing = có đoạn chạm khung.</summary>
    public static bool Polyline(Box r, IReadOnlyList<(double X, double Y)> pts, bool crossing)
    {
        if (pts.Count == 0) return false;
        if (!crossing) return pts.All(q => r.Contains(q.X, q.Y));
        if (pts.Count == 1) return r.Contains(pts[0].X, pts[0].Y);
        for (int i = 0; i + 1 < pts.Count; i++) if (SegmentHits(r, pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y)) return true;
        return false;
    }

    /// <summary>Đa giác kín (ô sàn) trong khung: window = mọi đỉnh trong khung; crossing = cạnh chạm khung hoặc khung nằm trong đa giác.</summary>
    public static bool Polygon(Box r, IReadOnlyList<(double X, double Y)> poly, bool crossing)
    {
        if (poly.Count < 3) return false;
        if (!crossing) return poly.All(q => r.Contains(q.X, q.Y));
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            if (SegmentHits(r, a.X, a.Y, b.X, b.Y)) return true;
        }
        return PointInPolygon(poly, (r.X0 + r.X1) / 2, (r.Y0 + r.Y1) / 2);
    }

    private static Node N(Project p, int id) => p.Nodes.FirstOrDefault(n => n.Id == id);

    private static bool OnStory(string objStory, string story) => string.IsNullOrEmpty(story) || objStory == story;

    /// <summary>Dầm (không phải cột) của tầng chọn bằng khung.</summary>
    public static List<int> Beams(Project p, string story, Box r, bool crossing) =>
        p.Frames.Where(f => !f.IsVerticalKind && OnStory(f.Story, story)).Where(f =>
        {
            var a = N(p, f.NodeI); var b = N(p, f.NodeJ);
            return a != null && b != null && Polyline(r, new[] { (a.X, a.Y), (b.X, b.Y) }, crossing);
        }).Select(f => f.Id).ToList();

    /// <summary>Cột của tầng (theo tâm): window = tâm trong khung; crossing = khung chạm hình vuông ± halfSize quanh tâm.</summary>
    public static List<int> Columns(Project p, string story, Box r, bool crossing, double halfSize = 0.11) =>
        p.Frames.Where(f => f.IsVerticalKind && OnStory(f.Story, story)).Where(f =>
        {
            var a = N(p, f.NodeJ) ?? N(p, f.NodeI);
            if (a == null) return false;
            if (!crossing) return r.Contains(a.X, a.Y);
            return a.X + halfSize >= r.X0 && a.X - halfSize <= r.X1 && a.Y + halfSize >= r.Y0 && a.Y - halfSize <= r.Y1;
        }).Select(f => f.Id).ToList();

    public static List<(double X, double Y)> SlabPolygon(Project p, Slab s) =>
        s.Nodes.Select(id => N(p, id)).Where(n => n != null).Select(n => (n.X, n.Y)).ToList();

    /// <summary>Ô sàn của tầng chọn bằng khung.</summary>
    public static List<int> Slabs(Project p, string story, Box r, bool crossing) =>
        p.Slabs.Where(s => OnStory(s.Story, story) && Polygon(r, SlabPolygon(p, s), crossing)).Select(s => s.Id).ToList();

    /// <summary>Thanh cùng tiết diện / cùng loại / cùng tầng với các thanh mẫu (giữ đúng loại: dầm ↔ dầm, cột ↔ cột).</summary>
    public static List<int> SimilarFrames(Project p, IEnumerable<int> seeds, SimilarBy by, string onlyStory = null)
    {
        var src = p.Frames.Where(f => seeds.Contains(f.Id)).ToList();
        if (src.Count == 0) return new List<int>();
        return p.Frames.Where(f => OnStory(f.Story, onlyStory) && src.Any(s => s.IsVerticalKind == f.IsVerticalKind && by switch
        {
            SimilarBy.Section => s.Section == f.Section,
            SimilarBy.Kind => s.Kind == f.Kind,
            _ => s.Story == f.Story
        })).Select(f => f.Id).ToList();
    }

    /// <summary>Ô sàn tương tự: cùng tiết diện (Section) / cùng cấu tạo – công năng (Kind) / cùng tầng.</summary>
    public static List<int> SimilarSlabs(Project p, IEnumerable<int> seeds, SimilarBy by, string onlyStory = null)
    {
        var src = p.Slabs.Where(s => seeds.Contains(s.Id)).ToList();
        if (src.Count == 0) return new List<int>();
        return p.Slabs.Where(s => OnStory(s.Story, onlyStory) && src.Any(q => by switch
        {
            SimilarBy.Section => q.Section == s.Section,
            SimilarBy.Kind => q.BuildUp == s.BuildUp && ModelEdit.UseOf(p, q) == ModelEdit.UseOf(p, s),
            _ => q.Story == s.Story
        })).Select(s => s.Id).ToList();
    }
}

/// <summary>Thao tác vẽ của dòng lệnh mặt bằng (MainWindow.CadCommandLine.cs): cột tại điểm, offset / mảng dầm.</summary>
public static class PlanDrawOps
{
    /// <summary>Thêm cột tại (x, y) cho tầng story: chân cột ở cao độ tầng dưới (hoặc chân công trình – ngàm), đầu cột ở cao độ tầng;
    /// dầm đi qua vị trí cột được chia tại nút mới.</summary>
    public static EditResult AddColumn(Project p, string story, double x, double y, string section)
    {
        var st = p.Stories.FirstOrDefault(s => s.Name == story);
        if (st == null) return EditResult.Fail("Chọn một tầng cụ thể trước khi đặt cột.");
        if (string.IsNullOrWhiteSpace(section) || p.FrameSections.All(s => s.Name != section)) return EditResult.Fail("Chọn tiết diện cột có trong danh sách tiết diện.");
        x = Math.Round(x, 4); y = Math.Round(y, 4);
        foreach (var f in p.Frames.Where(q => q.IsVerticalKind && q.Story == story))
        {
            var n = p.Nodes.FirstOrDefault(q => q.Id == f.NodeJ);
            if (n != null && Math.Abs(n.X - x) < 0.01 && Math.Abs(n.Y - y) < 0.01) return EditResult.Fail("Vị trí này đã có cột.");
        }
        var below = p.Stories.Where(s => s.Elevation < st.Elevation - 1e-6).OrderByDescending(s => s.Elevation).FirstOrDefault();
        double z0 = below?.Elevation ?? p.BaseElevation;
        int nNodes = p.Nodes.Count;
        var a = p.GetOrAddNode(x, y, z0, below?.Name ?? "Base");
        if (below == null && p.Nodes.Count > nNodes) a.Restraint = Node.Fixed;
        var b = p.GetOrAddNode(x, y, st.Elevation, st.Name);
        int count = p.Frames.Count(f => f.IsVerticalKind && f.Story == story);
        var col = new FrameElement { Id = p.NextFrameId(), NodeI = a.Id, NodeJ = b.Id, Kind = FrameKind.Column, Section = section, Story = story, Label = $"C{count + 1}" };
        p.Frames.Add(col);
        ModelBuilder.SplitFramesAtNodes(p);
        p.InvalidateIndex();
        var r = new EditResult { Ok = true, Message = $"Đã đặt cột {section} tại ({x:0.000}; {y:0.000}) tầng {story}." };
        r.NewFrames.Add(col.Id);
        return r;
    }

    /// <summary>Offset dầm: bản sao song song cách dầm gốc một khoảng d (m) – dương về bên trái hướng I→J.</summary>
    public static EditResult OffsetBeams(Project p, IEnumerable<int> ids, double distance)
    {
        if (Math.Abs(distance) < 1e-4) return EditResult.Fail("Nhập khoảng offset khác 0.");
        var res = new EditResult();
        var msgs = new List<string>();
        foreach (int id in ids.Distinct().ToList())
        {
            var f = p.Frames.FirstOrDefault(q => q.Id == id && !q.IsVerticalKind);
            if (f == null) continue;
            var a = p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var b = p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
            if (a == null || b == null) continue;
            double dx = b.X - a.X, dy = b.Y - a.Y, L = Math.Sqrt(dx * dx + dy * dy);
            if (L < 1e-6) continue;
            var e = BeamTools.Replicate(p, new[] { id }, -dy / L * distance, dx / L * distance, 1);
            if (e.Ok) res.NewFrames.AddRange(e.NewFrames); else msgs.Add(e.Message);
        }
        res.Ok = res.NewFrames.Count > 0;
        res.Message = res.Ok ? $"Đã offset {res.NewFrames.Count} dầm cách {distance * 1000:0} mm." : "Không offset được: " + string.Join(" ", msgs.Distinct().Take(2));
        return res;
    }

    /// <summary>Mảng chữ nhật: nx cột × ny hàng, bước dx, dy (m), tính cả bản gốc.</summary>
    public static EditResult ArrayBeams(Project p, IEnumerable<int> ids, int nx, int ny, double dx, double dy)
    {
        var src = ids.Distinct().ToList();
        if (nx < 1 || ny < 1 || nx * ny < 2) return EditResult.Fail("Số cột × số hàng của mảng phải ≥ 2.");
        var res = new EditResult();
        var all = new List<int>(src);
        if (nx > 1)
        {
            var e = BeamTools.Replicate(p, src, dx, 0, nx - 1);
            if (!e.Ok) return e;
            res.NewFrames.AddRange(e.NewFrames); all.AddRange(e.NewFrames);
        }
        if (ny > 1)
        {
            var e = BeamTools.Replicate(p, all, 0, dy, ny - 1);
            if (!e.Ok && res.NewFrames.Count == 0) return e;
            res.NewFrames.AddRange(e.NewFrames);
        }
        res.Ok = res.NewFrames.Count > 0;
        res.Message = res.Ok ? $"Đã tạo mảng {nx} × {ny}: thêm {res.NewFrames.Count} dầm." : "Không tạo được mảng.";
        return res;
    }
}
