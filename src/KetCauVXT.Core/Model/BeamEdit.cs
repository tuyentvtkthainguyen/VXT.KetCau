namespace KetCauVXT.Model;

// =====================================================================
// SỬA DẦM TRÊN MẶT BẰNG KIỂU AUTOCAD (lõi, kiểm thử được – giao diện ở App/MainWindow.BeamEdit.cs):
//   • Extend: kéo dài một đầu dầm theo phương dầm tới dầm được chọn hoặc dầm gặp đầu tiên (ưu tiên dầm vuông góc ±15°);
//   • Trim: cắt bỏ đoạn dầm giữa điểm bấm và các dầm giao gần nhất (như lệnh TRIM);
//   • Stretch: kéo một đầu dầm tới điểm mới; Move: dời cả dầm song song (giữ liên kết hợp lý);
//   • JoinToNearest: các đầu dầm hở tự kéo dài tới dầm vuông góc gần nhất trong khoảng cho phép
//     ("nối dầm cho người không nhớ kích thước").
// Nút giao được tạo/chia đúng (GetOrAddNode + ModelBuilder.SplitFramesAtNodes) → mô hình chạy phân tích được ngay.
// Toạ độ m; mọi hàm trả về bool + thông báo tiếng Việt, không ném lỗi với dữ liệu sai.
// =====================================================================
public static class BeamEdit
{
    /// <summary>Sai số trùng điểm / nằm trên đoạn (m).</summary>
    public const double Tol = 0.005;
    /// <summary>Độ dài dầm nhỏ nhất (m).</summary>
    public const double MinLength = 0.05;
    /// <summary>Góc coi là vuông góc (±, độ).</summary>
    public const double PerpTolDeg = 15;

    private static double R4(double v) => Math.Round(v, 4);
    private static double Hyp(double x, double y) => Math.Sqrt(x * x + y * y);

    private static FrameElement Beam(Project p, int id, out string msg)
    {
        msg = "";
        var f = p?.Frames.FirstOrDefault(q => q.Id == id);
        if (f == null) { msg = "Không tìm thấy dầm."; return null; }
        if (f.IsVerticalKind) { msg = "Thanh đã chọn là cột – chỉ sửa được dầm."; return null; }
        if (f.IsCurved) { msg = "Dầm cong: dùng lệnh sửa dầm cong (độ vồng / bán kính)."; return null; }
        return f;
    }

    /// <summary>Tên tầng của dầm theo cao độ nút.</summary>
    public static string StoryOf(Project p, FrameElement f)
    {
        var st = p.StoryAt(p.Node(f.NodeI).Z);
        return st?.Name ?? f.Story;
    }

    /// <summary>Dầm thẳng cùng cao độ (trừ dầm f).</summary>
    private static List<FrameElement> Others(Project p, FrameElement f)
    {
        double z = p.Node(f.NodeI).Z;
        return p.Frames.Where(g => g.Id != f.Id && !g.IsVerticalKind && !g.IsCurved
                                   && Math.Abs(p.Node(g.NodeI).Z - z) < 0.01 && Math.Abs(p.Node(g.NodeJ).Z - z) < 0.01).ToList();
    }

    /// <summary>Số thanh dùng nút.</summary>
    public static int Usage(Project p, int nodeId) => p.Frames.Count(q => q.NodeI == nodeId || q.NodeJ == nodeId);

    /// <summary>Nút được dùng bởi thứ khác ngoài dầm f (thanh khác, gối, ô sàn, tải nút) → không được dời nút này.</summary>
    public static bool IsShared(Project p, int nodeId, FrameElement f)
    {
        var n = p.Node(nodeId);
        if (n.HasRestraint) return true;
        if (p.Frames.Any(q => q.Id != f.Id && (q.NodeI == nodeId || q.NodeJ == nodeId))) return true;
        if (p.Slabs.Any(s => s.Nodes.Contains(nodeId))) return true;
        if (p.NodeLoads.Any(l => l.Node == nodeId)) return true;
        return false;
    }

    /// <summary>Đầu dầm hở: nút chỉ thuộc dầm này, không gối.</summary>
    public static bool IsFreeEnd(Project p, FrameElement f, bool atJ)
    {
        int id = atJ ? f.NodeJ : f.NodeI;
        return Usage(p, id) == 1 && !p.Node(id).HasRestraint;
    }

    /// <summary>Xoá nút không còn ai dùng (thanh, ô sàn, tải nút, gối).</summary>
    private static void RemoveOrphan(Project p, int nodeId)
    {
        var n = p.Nodes.FirstOrDefault(q => q.Id == nodeId);
        if (n == null || n.HasRestraint) return;
        if (Usage(p, nodeId) > 0 || p.Slabs.Any(s => s.Nodes.Contains(nodeId)) || p.NodeLoads.Any(l => l.Node == nodeId)) return;
        p.Nodes.Remove(n);
        p.InvalidateIndex();
    }

    /// <summary>Tia (ox, oy) + t·(dx, dy) (d đơn vị) cắt đường thẳng chứa đoạn A–B: t (m, dọc tia) và s (0…1 trên đoạn).</summary>
    public static bool RayLine(double ox, double oy, double dx, double dy, double ax, double ay, double bx, double by, out double t, out double s)
    {
        t = s = double.NaN;
        double ex = bx - ax, ey = by - ay;
        double den = dx * ey - dy * ex;
        if (Math.Abs(den) < 1e-12) return false;
        t = ((ax - ox) * ey - (ay - oy) * ex) / den;
        s = ((ax - ox) * dy - (ay - oy) * dx) / den;
        return true;
    }

    /// <summary>Góc giữa hai phương (0…90°).</summary>
    public static double AngleBetween(double ux, double uy, double vx, double vy)
    {
        double lu = Hyp(ux, uy), lv = Hyp(vx, vy);
        if (lu < 1e-12 || lv < 1e-12) return 0;
        double c = Math.Abs((ux * vx + uy * vy) / (lu * lv));
        return Math.Acos(Math.Min(1, c)) * 180 / Math.PI;
    }

    /// <summary>Tìm điểm dừng khi kéo dài đầu dầm: (dầm đích, toạ độ, khoảng kéo thêm t). target = null → dầm gặp đầu tiên
    /// (ưu tiên dầm gần vuông góc trong ±15°); maxLen giới hạn chiều dài kéo thêm.</summary>
    public static (FrameElement Target, double X, double Y, double T)? FindExtendHit(Project p, FrameElement f, bool atJ, int? targetId, double maxLen = 30, bool perpOnly = false)
    {
        var ni = p.Node(f.NodeI); var nj = p.Node(f.NodeJ);
        var e = atJ ? nj : ni; var o = atJ ? ni : nj;
        double L = Hyp(e.X - o.X, e.Y - o.Y);
        if (L < 1e-9) return null;
        double dx = (e.X - o.X) / L, dy = (e.Y - o.Y) / L;
        var cands = new List<(FrameElement G, double X, double Y, double T, bool Perp)>();
        foreach (var g in Others(p, f))
        {
            if (targetId.HasValue && g.Id != targetId.Value) continue;
            if (g.NodeI == e.Id || g.NodeJ == e.Id) continue;   // đã nối tại đầu này
            var a = p.Node(g.NodeI); var b = p.Node(g.NodeJ);
            double lg = Hyp(b.X - a.X, b.Y - a.Y);
            if (lg < 1e-9) continue;
            if (!RayLine(e.X, e.Y, dx, dy, a.X, a.Y, b.X, b.Y, out double t, out double s)) continue;
            double tolS = Tol / lg;
            if (t < Tol || t > maxLen) continue;
            if (s < -tolS || s > 1 + tolS) continue;
            bool perp = Math.Abs(90 - AngleBetween(dx, dy, b.X - a.X, b.Y - a.Y)) <= PerpTolDeg;
            if (perpOnly && !perp) continue;
            double sc = Math.Clamp(s, 0, 1);
            double hx = a.X + (b.X - a.X) * sc, hy = a.Y + (b.Y - a.Y) * sc;
            // điểm dừng nằm trên phương dầm f (chiếu lại lên tia để không làm lệch phương)
            double tt = (hx - e.X) * dx + (hy - e.Y) * dy;
            cands.Add((g, e.X + dx * tt, e.Y + dy * tt, tt, perp));
        }
        if (cands.Count == 0) return null;
        var best = cands.OrderBy(c => targetId.HasValue ? 0 : (c.Perp ? 0 : 1)).ThenBy(c => c.T).First();
        return (best.G, R4(best.X), R4(best.Y), best.T);
    }

    /// <summary>Kéo dài đầu dầm (atJ = đầu J) tới dầm đích (null = dầm gặp đầu tiên, ưu tiên vuông góc).
    /// Đầu hở → dời nút; đầu đã nối với thanh khác → thêm đoạn dầm nối dài cùng tiết diện. Dầm đích được chia tại điểm nối.</summary>
    public static bool Extend(Project p, int frameId, bool atJ, int? targetFrameId, out string msg) =>
        Extend(p, frameId, atJ, targetFrameId, 30, false, out msg);

    public static bool Extend(Project p, int frameId, bool atJ, int? targetFrameId, double maxLen, bool perpOnly, out string msg)
    {
        var f = Beam(p, frameId, out msg);
        if (f == null) return false;
        var hit = FindExtendHit(p, f, atJ, targetFrameId, maxLen, perpOnly);
        if (hit == null)
        {
            msg = targetFrameId.HasValue ? "Phương dầm không cắt dầm đích (hoặc dầm đích nằm phía sau đầu dầm) – chọn dầm khác."
                                         : "Không có dầm nào phía trước đầu dầm trong phạm vi " + maxLen.ToString("0.##") + " m.";
            return false;
        }
        var (target, hx, hy, t) = hit.Value;
        int endId = atJ ? f.NodeJ : f.NodeI;
        var en = p.Node(endId);
        double z = en.Z;
        string story = StoryOf(p, f);
        if (!IsShared(p, endId, f))
        {
            var existing = p.Nodes.FirstOrDefault(n => n.Id != endId && Math.Abs(n.X - hx) < 1e-3 && Math.Abs(n.Y - hy) < 1e-3 && Math.Abs(n.Z - z) < 1e-3);
            if (existing != null)
            {
                if (atJ) f.NodeJ = existing.Id; else f.NodeI = existing.Id;
                RemoveOrphan(p, endId);
            }
            else { en.X = hx; en.Y = hy; }
            p.InvalidateIndex();
        }
        else
        {
            // đầu dầm đã nối thanh khác: thêm đoạn nối dài cùng tiết diện
            var hn = p.GetOrAddNode(hx, hy, z, story);
            var ext = CopyFrame(p, f, endId, hn.Id);
            p.Frames.Add(ext);
            p.InvalidateIndex();
        }
        CrossNodes(p, f);
        ModelBuilder.SplitFramesAtNodes(p);
        p.InvalidateIndex();
        msg = $"Đã kéo dài dầm {Lbl(f)} thêm {t * 1000:0} mm tới dầm {Lbl(target)}" + (Math.Abs(90 - AngleOf(p, f, target)) <= PerpTolDeg ? " (vuông góc)" : "") + ".";
        return true;
    }

    private static double AngleOf(Project p, FrameElement f, FrameElement g)
    {
        var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ); var c = p.Node(g.NodeI); var d = p.Node(g.NodeJ);
        return AngleBetween(b.X - a.X, b.Y - a.Y, d.X - c.X, d.Y - c.Y);
    }

    private static string Lbl(FrameElement f) => string.IsNullOrEmpty(f.Label) ? "#" + f.Id : f.Label;

    /// <summary>Bản sao thuộc tính dầm sang thanh mới nối hai nút.</summary>
    private static FrameElement CopyFrame(Project p, FrameElement f, int ni, int nj) => new()
    {
        Id = p.NextFrameId(), NodeI = ni, NodeJ = nj, Kind = f.Kind, Section = f.Section, Story = f.Story, Label = f.Label, DesignGroup = f.DesignGroup,
        Angle = f.Angle, UnbracedLength22 = f.UnbracedLength22, Insertion = f.Insertion, PlanOffset = f.PlanOffset,
        StiffnessModifiers = (double[])f.StiffnessModifiers?.Clone()
    };

    /// <summary>Tạo nút tại giao điểm của dầm f với các dầm thẳng cùng cao độ (để chia dầm đúng nút giao).</summary>
    private static void CrossNodes(Project p, FrameElement f)
    {
        var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
        string story = StoryOf(p, f);
        foreach (var g in Others(p, f))
        {
            var c = p.Node(g.NodeI); var d = p.Node(g.NodeJ);
            if (SegSeg(a.X, a.Y, b.X, b.Y, c.X, c.Y, d.X, d.Y, out double ix, out double iy))
                p.GetOrAddNode(R4(ix), R4(iy), a.Z, story);
        }
    }

    /// <summary>Giao hai đoạn (kể cả chạm đầu, sai số Tol).</summary>
    public static bool SegSeg(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy, out double x, out double y)
    {
        x = y = double.NaN;
        double rx = bx - ax, ry = by - ay, sx = dx - cx, sy = dy - cy;
        double den = rx * sy - ry * sx;
        double l1 = Hyp(rx, ry), l2 = Hyp(sx, sy);
        if (l1 < 1e-9 || l2 < 1e-9 || Math.Abs(den) < 1e-9 * l1 * l2) return false;
        double t = ((cx - ax) * sy - (cy - ay) * sx) / den;
        double u = ((cx - ax) * ry - (cy - ay) * rx) / den;
        double e1 = Tol / l1, e2 = Tol / l2;
        if (t < -e1 || t > 1 + e1 || u < -e2 || u > 1 + e2) return false;
        x = ax + Math.Clamp(t, 0, 1) * rx; y = ay + Math.Clamp(t, 0, 1) * ry;
        return true;
    }

    /// <summary>Các vị trí cắt (m tính từ nút I) trên dầm f: giao với dầm khác, nút của thanh khác nằm trên dầm (cột, đầu dầm).</summary>
    public static List<double> CutPoints(Project p, FrameElement f)
    {
        var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
        double L = Hyp(b.X - a.X, b.Y - a.Y);
        var res = new List<double>();
        if (L < 1e-9) return res;
        double ux = (b.X - a.X) / L, uy = (b.Y - a.Y) / L;
        void Add(double x, double y)
        {
            double t = (x - a.X) * ux + (y - a.Y) * uy;
            double d = Math.Abs(-(x - a.X) * uy + (y - a.Y) * ux);
            if (d <= Tol && t > 0.01 && t < L - 0.01) res.Add(t);
        }
        foreach (var g in Others(p, f))
        {
            var c = p.Node(g.NodeI); var d = p.Node(g.NodeJ);
            if (SegSeg(a.X, a.Y, b.X, b.Y, c.X, c.Y, d.X, d.Y, out double ix, out double iy)) Add(ix, iy);
        }
        // cột / thanh khác có nút nằm trong dầm (cùng cao độ)
        foreach (var n in p.Nodes.Where(n => n.Id != f.NodeI && n.Id != f.NodeJ && Math.Abs(n.Z - a.Z) < 0.01 && Usage(p, n.Id) > 0))
            Add(n.X, n.Y);
        res.Sort();
        var merged = new List<double>();
        foreach (double t in res) if (merged.Count == 0 || t - merged[^1] > 0.01) merged.Add(t);
        return merged;
    }

    /// <summary>TRIM: bỏ đoạn dầm chứa điểm bấm, giới hạn bởi hai vị trí cắt gần nhất hai phía (hoặc đầu dầm).
    /// Dầm không giao gì → báo dùng Xoá. Đoạn còn lại giữ tiết diện, tải (chia theo tỷ lệ).</summary>
    public static bool Trim(Project p, int frameId, double pickX, double pickY, out string msg)
    {
        var f = Beam(p, frameId, out msg);
        if (f == null) return false;
        var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
        double L = Hyp(b.X - a.X, b.Y - a.Y);
        double ux = (b.X - a.X) / L, uy = (b.Y - a.Y) / L;
        double tp = Math.Clamp((pickX - a.X) * ux + (pickY - a.Y) * uy, 0, L);
        var cuts = CutPoints(p, f);
        if (cuts.Count == 0 && Usage(p, f.NodeI) == 1 && Usage(p, f.NodeJ) == 1 && !p.Node(f.NodeI).HasRestraint && !p.Node(f.NodeJ).HasRestraint)
        { msg = "Dầm không giao dầm / cột nào – dùng lệnh Xoá để bỏ cả dầm."; return false; }
        double lo = cuts.Where(c => c < tp).DefaultIfEmpty(0).Max(), hi = cuts.Where(c => c > tp).DefaultIfEmpty(L).Min();
        if (hi - lo < MinLength / 2) { msg = "Điểm bấm trùng vị trí cắt – bấm vào giữa đoạn cần bỏ."; return false; }
        string story = StoryOf(p, f);
        double z = a.Z;
        var before = new HashSet<int>(p.Frames.Select(q => q.Id));
        int nI = f.NodeI, nJ = f.NodeJ;
        if (lo > 0.01) p.GetOrAddNode(R4(a.X + ux * lo), R4(a.Y + uy * lo), z, story);
        if (hi < L - 0.01) p.GetOrAddNode(R4(a.X + ux * hi), R4(a.Y + uy * hi), z, story);
        ModelBuilder.SplitFramesAtNodes(p);
        p.InvalidateIndex();
        // các mảnh của dầm gốc: id cũ hoặc id mới sinh ra, thẳng hàng với dầm gốc
        double ax = a.X, ay = a.Y;
        var pieces = p.Frames.Where(q => (q.Id == f.Id || !before.Contains(q.Id)) && !q.IsVerticalKind && !q.IsCurved).Where(q =>
        {
            var c = p.Node(q.NodeI); var d = p.Node(q.NodeJ);
            double dc = Math.Abs(-(c.X - ax) * uy + (c.Y - ay) * ux), dd = Math.Abs(-(d.X - ax) * uy + (d.Y - ay) * ux);
            return dc <= Tol && dd <= Tol && Math.Abs(c.Z - z) < 0.01 && Math.Abs(d.Z - z) < 0.01;
        }).ToList();
        int removed = 0;
        var touched = new List<int>();
        foreach (var q in pieces)
        {
            var c = p.Node(q.NodeI); var d = p.Node(q.NodeJ);
            double tc = (c.X - ax) * ux + (c.Y - ay) * uy, td = (d.X - ax) * ux + (d.Y - ay) * uy;
            double tm = (tc + td) / 2;
            if (tm > lo + 1e-6 && tm < hi - 1e-6)
            {
                touched.Add(q.NodeI); touched.Add(q.NodeJ);
                ModelEdit.DeleteFrame(p, q);
                removed++;
            }
        }
        foreach (int id in touched.Distinct()) RemoveOrphan(p, id);
        p.InvalidateIndex();
        if (removed == 0) { msg = "Không cắt được đoạn dầm tại điểm bấm."; return false; }
        bool all = lo <= 0.01 && hi >= L - 0.01;
        msg = all ? $"Đã bỏ cả dầm {Lbl(f)}." : $"Đã trim dầm {Lbl(f)}: bỏ đoạn {(hi - lo) * 1000:0} mm (từ {lo * 1000:0} đến {hi * 1000:0} mm tính từ đầu dầm).";
        return true;
    }

    /// <summary>KÉO ĐẦU DẦM tới (x, y): nút chỉ thuộc dầm này → dời nút; nút dùng chung → tách đầu dầm sang nút mới.
    /// Điểm mới trùng nút có sẵn → nối vào nút đó. Dầm được chia tại các giao điểm mới.</summary>
    public static bool Stretch(Project p, int frameId, bool atJ, double x, double y, out string msg)
    {
        var f = Beam(p, frameId, out msg);
        if (f == null) return false;
        x = R4(x); y = R4(y);
        int endId = atJ ? f.NodeJ : f.NodeI, otherId = atJ ? f.NodeI : f.NodeJ;
        var en = p.Node(endId); var on = p.Node(otherId);
        if (Hyp(x - on.X, y - on.Y) < MinLength) { msg = $"Dầm sau khi kéo quá ngắn (< {MinLength * 1000:0} mm)."; return false; }
        double z = en.Z;
        string story = StoryOf(p, f);
        double oldL = Hyp(en.X - on.X, en.Y - on.Y);
        var existing = p.Nodes.FirstOrDefault(n => n.Id != endId && Math.Abs(n.X - x) < 1e-3 && Math.Abs(n.Y - y) < 1e-3 && Math.Abs(n.Z - z) < 1e-3);
        if (existing != null && existing.Id == otherId) { msg = "Điểm mới trùng đầu kia của dầm."; return false; }
        if (existing != null && p.Frames.Any(q => q.Id != f.Id && ((q.NodeI == otherId && q.NodeJ == existing.Id) || (q.NodeJ == otherId && q.NodeI == existing.Id))))
        { msg = "Đã có dầm nối đúng hai điểm này."; return false; }
        if (existing != null)
        {
            if (atJ) f.NodeJ = existing.Id; else f.NodeI = existing.Id;
            RemoveOrphan(p, endId);
        }
        else if (!IsShared(p, endId, f)) { en.X = x; en.Y = y; }
        else
        {
            var nn = p.GetOrAddNode(x, y, z, story);
            if (atJ) f.NodeJ = nn.Id; else f.NodeI = nn.Id;
            if (atJ) f.ReleaseJ = new bool[6]; else f.ReleaseI = new bool[6];
        }
        p.InvalidateIndex();
        CrossNodes(p, f);
        ModelBuilder.SplitFramesAtNodes(p);
        p.InvalidateIndex();
        double newL = Hyp(x - on.X, y - on.Y);
        msg = $"Đã kéo đầu dầm {Lbl(f)}: dài {oldL * 1000:0} → {newL * 1000:0} mm.";
        return true;
    }

    /// <summary>DỜI DẦM song song (dx, dy): nút chỉ thuộc các dầm được chọn thì dời theo; nút dùng chung (cột, dầm khác, ô sàn)
    /// thì các dầm được chọn tách sang nút mới. Nút tới trùng nút có sẵn → nối vào. Chia lại tại giao điểm.</summary>
    public static bool Move(Project p, IList<int> frameIds, double dx, double dy, out string msg)
    {
        msg = "";
        var set = new HashSet<int>(frameIds ?? Array.Empty<int>());
        var fs = p.Frames.Where(q => set.Contains(q.Id) && !q.IsVerticalKind && !q.IsCurved).ToList();
        if (fs.Count == 0) { msg = "Chưa chọn dầm thẳng để dời."; return false; }
        if (Hyp(dx, dy) < 1e-4) { msg = "Khoảng dời bằng 0."; return false; }
        var nodeIds = fs.SelectMany(q => new[] { q.NodeI, q.NodeJ }).Distinct().ToList();
        var map = new Dictionary<int, int>();
        foreach (int id in nodeIds)
        {
            var n = p.Node(id);
            double nx = R4(n.X + dx), ny = R4(n.Y + dy);
            bool ownedOnly = !n.HasRestraint && p.Frames.Where(q => q.NodeI == id || q.NodeJ == id).All(q => set.Contains(q.Id))
                             && !p.Slabs.Any(s => s.Nodes.Contains(id)) && !p.NodeLoads.Any(l => l.Node == id);
            var existing = p.Nodes.FirstOrDefault(q => q.Id != id && !nodeIds.Contains(q.Id) && Math.Abs(q.X - nx) < 1e-3 && Math.Abs(q.Y - ny) < 1e-3 && Math.Abs(q.Z - n.Z) < 1e-3);
            if (existing != null) map[id] = existing.Id;
            else if (ownedOnly) { n.X = nx; n.Y = ny; map[id] = id; }
            else
            {
                var nn = new Node { Id = p.NextNodeId(), X = nx, Y = ny, Z = n.Z, Story = n.Story };
                p.Nodes.Add(nn); p.InvalidateIndex();
                map[id] = nn.Id;
            }
        }
        foreach (var q in fs)
        {
            if (map[q.NodeI] != q.NodeI) q.ReleaseI = new bool[6];
            if (map[q.NodeJ] != q.NodeJ) q.ReleaseJ = new bool[6];
            q.NodeI = map[q.NodeI]; q.NodeJ = map[q.NodeJ];
        }
        foreach (int id in nodeIds) RemoveOrphan(p, id);
        p.InvalidateIndex();
        foreach (var q in fs) CrossNodes(p, q);
        ModelBuilder.SplitFramesAtNodes(p);
        p.InvalidateIndex();
        msg = $"Đã dời {fs.Count} dầm Δx = {dx * 1000:0} mm, Δy = {dy * 1000:0} mm.";
        return true;
    }

    /// <summary>NỐI DẦM HỞ: mỗi đầu hở (không nối thanh khác, không gối) của dầm tự kéo dài tới dầm vuông góc gần nhất
    /// phía trước trong khoảng maxGap (m). Trả về true nếu nối được ít nhất một đầu.</summary>
    public static bool JoinToNearest(Project p, int frameId, double maxGap, out string msg)
    {
        var f = Beam(p, frameId, out msg);
        if (f == null) return false;
        var parts = new List<string>();
        int ok = 0;
        foreach (bool atJ in new[] { false, true })
        {
            if (!IsFreeEnd(p, f, atJ)) continue;
            if (Extend(p, f.Id, atJ, null, maxGap, true, out string m)) { ok++; parts.Add((atJ ? "đầu J: " : "đầu I: ") + m); }
        }
        if (ok == 0)
        {
            bool anyFree = IsFreeEnd(p, f, false) || IsFreeEnd(p, f, true);
            msg = anyFree ? $"Không có dầm vuông góc trong phạm vi {maxGap * 1000:0} mm phía trước đầu dầm hở." : "Hai đầu dầm đã nối với thanh khác.";
            return false;
        }
        msg = string.Join(" ", parts);
        return true;
    }

    /// <summary>Nối mọi dầm có đầu hở trên tầng (dùng sau khi vẽ nhanh theo bản vẽ CAD). Trả về số đầu đã nối.</summary>
    public static int JoinAll(Project p, string story, double maxGap, out string msg)
    {
        double z = ModelEdit.StoryZ(p, story);
        int n = 0;
        foreach (var id in p.Frames.Where(q => !q.IsVerticalKind && !q.IsCurved && Math.Abs(p.Node(q.NodeI).Z - z) < 0.01 && Math.Abs(p.Node(q.NodeJ).Z - z) < 0.01).Select(q => q.Id).ToList())
        {
            var f = p.Frames.FirstOrDefault(q => q.Id == id);
            if (f == null) continue;
            foreach (bool atJ in new[] { false, true })
                if (IsFreeEnd(p, f, atJ) && Extend(p, f.Id, atJ, null, maxGap, true, out _)) n++;
        }
        msg = n > 0 ? $"Đã nối {n} đầu dầm hở vào dầm vuông góc gần nhất (≤ {maxGap * 1000:0} mm)." : "Không có đầu dầm hở nào nối được.";
        return n;
    }

    /// <summary>Dầm thẳng gần điểm (x, y) nhất trên tầng trong bán kính tol; atJ = điểm gần đầu J hơn đầu I.</summary>
    public static FrameElement NearestBeam(Project p, string story, double x, double y, double tol, out bool nearJ, out double dist)
    {
        nearJ = false; dist = double.MaxValue;
        FrameElement best = null;
        double z;
        try { z = ModelEdit.StoryZ(p, story); } catch (ArgumentException) { return null; }
        foreach (var f in p.Frames.Where(q => !q.IsVerticalKind && !q.IsCurved))
        {
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            if (Math.Abs(a.Z - z) > 0.01 || Math.Abs(b.Z - z) > 0.01) continue;
            double L2 = (b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y);
            double t = L2 < 1e-12 ? 0 : Math.Clamp(((x - a.X) * (b.X - a.X) + (y - a.Y) * (b.Y - a.Y)) / L2, 0, 1);
            double px = a.X + t * (b.X - a.X), py = a.Y + t * (b.Y - a.Y);
            double d = Hyp(x - px, y - py);
            if (d < dist) { dist = d; best = f; nearJ = t > 0.5; }
        }
        return dist <= tol ? best : null;
    }
}
