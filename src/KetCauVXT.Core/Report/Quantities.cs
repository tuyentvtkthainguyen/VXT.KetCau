using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Drawing;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

// =====================================================================
// BẢNG KHỐI LƯỢNG VẬT TƯ (bê tông, ván khuôn, cốt thép, xây – trát, hoàn thiện sàn, phân tích vật tư)
// Đơn vị: m, m², m³, kg. Quy ước tính (in kèm bảng – QuantityResult.Notes):
//  • Cột: từ chân cột (mặt móng) tới mặt sàn tầng trên, toàn tiết diện → phần giao cột–dầm–sàn thuộc cột.
//  • Dầm: tính đến mép cột (dầm phụ tính đến mép dầm chính), tiết diện b×h toàn phần.
//  • Sàn: diện tích ô sàn trừ phần dầm, cột nằm trong ô và lỗ sàn, × chiều dày.
//  • Móng/đài/móng băng: theo kích thước thiết kế (DesignSummary); bê tông lót mở rộng mỗi phía LeanOffset.
//  • Cốt thép: lấy đúng bảng thống kê cốt thép (Drawing/RebarSchedule.BuildRows).
// =====================================================================

/// <summary>Tên nhóm cấu kiện dùng trong bảng khối lượng.</summary>
public static class QtyCat
{
    public const string Footing = "Móng đơn", PileCap = "Đài cọc", Strip = "Móng băng", TieBeam = "Giằng móng", Pile = "Cọc",
        Column = "Cột", Beam = "Dầm", Slab = "Sàn", Balcony = "Ban công, lô gia", Stair = "Cầu thang", Lintel = "Lanh tô", Lean = "Bê tông lót";

    /// <summary>Thứ tự in</summary>
    public static readonly string[] Order = { Footing, PileCap, Strip, TieBeam, Pile, Column, Beam, Slab, Balcony, Stair, Lintel, Lean };

    public static int Rank(string c) { int i = Array.IndexOf(Order, c); return i < 0 ? 99 : i; }
}

/// <summary>Một dòng khối lượng bê tông (đã nhân số lượng) + ván khuôn.</summary>
public class ConcreteItem
{
    public string Category { get; set; } = "";
    public string Story { get; set; } = "";
    public string Member { get; set; } = "";
    public string Grade { get; set; } = "";
    /// <summary>Mô tả kích thước, ví dụ "220×300, L = 4.78"</summary>
    public string Dims { get; set; } = "";
    public int Count { get; set; } = 1;
    /// <summary>Thể tích bê tông (m³), đã nhân Count</summary>
    public double Volume { get; set; }
    /// <summary>Diện tích ván khuôn (m²), đã nhân Count</summary>
    public double Formwork { get; set; }
    /// <summary>Diễn giải ván khuôn</summary>
    public string FormworkNote { get; set; } = "";
    public string Note { get; set; } = "";
}

/// <summary>Tổng hợp cọc.</summary>
public class PileSummary
{
    public int Count { get; set; }
    public double LengthEach { get; set; }
    public double TotalLength { get; set; }
    public double Area { get; set; }
    public double Volume { get; set; }
    public string Size { get; set; } = "";
    public string Grade { get; set; } = "";
}

/// <summary>Cốt thép theo loại cấu kiện (kg) và chỉ tiêu kg/m³.</summary>
public class RebarCategoryRow
{
    public string Category { get; set; } = "";
    public double Le10 { get; set; }
    public double From10To18 { get; set; }
    public double Gt18 { get; set; }
    public double Total => Le10 + From10To18 + Gt18;
    /// <summary>Thể tích bê tông của nhóm cấu kiện tương ứng (m³)</summary>
    public double ConcreteVolume { get; set; }
    /// <summary>Hàm lượng kg/m³</summary>
    public double Ratio => ConcreteVolume > 1e-9 ? Total / ConcreteVolume : 0;
}

/// <summary>Khối lượng xây – trát của một đoạn tường.</summary>
public class MasonryItem
{
    public int WallId { get; set; }
    public string Story { get; set; } = "";
    public string WallType { get; set; } = "";
    public double Thickness { get; set; }
    /// <summary>Chiều dài tim trục (m)</summary>
    public double Length { get; set; }
    /// <summary>Chiều dài đã trừ cột (m)</summary>
    public double NetLength { get; set; }
    public double Height { get; set; }
    public double GrossArea { get; set; }
    public double OpeningArea { get; set; }
    /// <summary>Diện tích lanh tô trong mặt tường (m²) – đã trừ khỏi khối xây</summary>
    public double LintelArea { get; set; }
    /// <summary>Diện tích khối xây = gross − cửa − lanh tô (m²)</summary>
    public double NetArea { get; set; }
    public double Volume { get; set; }
    public int PlasterSides { get; set; }
    public double PlasterThickness { get; set; }
    /// <summary>Diện tích trát (m²) = (gross − cửa) × số mặt</summary>
    public double PlasterArea { get; set; }
    public double Bricks { get; set; }
    /// <summary>Vữa xây (m³)</summary>
    public double Mortar { get; set; }
    /// <summary>Vữa trát (m³)</summary>
    public double PlasterMortar { get; set; }
}

/// <summary>Khối lượng một lớp hoàn thiện sàn.</summary>
public class FinishItem
{
    public string BuildUp { get; set; } = "";
    public string Layer { get; set; } = "";
    public string Story { get; set; } = "";
    public double Area { get; set; }
    public double Thickness { get; set; }
    public double Volume { get; set; }
    /// <summary>"Vữa", "Gạch", "Chống thấm", "Bê tông", "Trần", "Khác"</summary>
    public string Kind { get; set; } = "Khác";
}

/// <summary>Một dòng phân tích vật tư: khối lượng công tác × định mức = nhu cầu vật tư.</summary>
public class MaterialLine
{
    public string Group { get; set; } = "";
    public string Source { get; set; } = "";
    public double SourceQty { get; set; }
    public string SourceUnit { get; set; } = "";
    public string Material { get; set; } = "";
    public double Norm { get; set; }
    public string NormUnit { get; set; } = "";
    public double Quantity { get; set; }
    public string Unit { get; set; } = "";
}

/// <summary>Tổng hợp vật tư (+ đơn giá, thành tiền nếu người dùng nhập).</summary>
public class MaterialTotal
{
    public string Material { get; set; } = "";
    public string Unit { get; set; } = "";
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Amount => Quantity * Price;
}

/// <summary>Toàn bộ kết quả bảng khối lượng.</summary>
public class QuantityResult
{
    public QuantitySettings Settings { get; set; } = new();
    public List<ConcreteItem> Concrete { get; set; } = new();
    /// <summary>Bê tông lót (thể tích m³, không ván khuôn)</summary>
    public List<ConcreteItem> Lean { get; set; } = new();
    public PileSummary Piles { get; set; }
    public List<ScheduleRow> RebarRows { get; set; } = new();
    public List<DiameterSummary> RebarByDia { get; set; } = new();
    public GroupSummary RebarGroups { get; set; } = new();
    public List<RebarCategoryRow> RebarByCategory { get; set; } = new();
    public List<MasonryItem> Masonry { get; set; } = new();
    public List<FinishItem> Finishes { get; set; } = new();
    /// <summary>Cầu thang: xây bậc gạch, ốp bậc, trát (Report/StairQuantity.cs)</summary>
    public List<StairWorkItem> StairWorks { get; set; } = new();
    public List<MaterialLine> MaterialLines { get; set; } = new();
    public List<MaterialTotal> Materials { get; set; } = new();
    public List<string> Notes { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    /// <summary>true = có kết quả thiết kế (móng, cốt thép)</summary>
    public bool HasDesign { get; set; }

    public double ConcreteTotal => Concrete.Sum(c => c.Volume);
    public double LeanTotal => Lean.Sum(c => c.Volume);
    public double FormworkTotal => Concrete.Sum(c => c.Formwork);
    public double RebarTotal => RebarRows.Sum(r => r.WeightKg);
    public double MasonryTotal => Masonry.Sum(m => m.Volume);
    public double PlasterTotal => Masonry.Sum(m => m.PlasterArea);

    public double Volume(string category) => Concrete.Where(c => c.Category == category).Sum(c => c.Volume);
    public double Formwork(string category) => Concrete.Where(c => c.Category == category).Sum(c => c.Formwork);

    /// <summary>Danh sách tầng theo thứ tự in ("Móng" trước).</summary>
    public List<string> StoryOrder { get; set; } = new();
}

/// <summary>Tính bảng khối lượng vật tư từ mô hình + kết quả thiết kế.</summary>
public static class QuantityCalculator
{
    private const double ZTol = 0.05;
    public const string FoundationStory = "Móng";

    private sealed class BeamInfo
    {
        public FrameElement F;
        public double B, H, L, Lc, Z, WL, WR;
        public string Grade, Story;
        public bool Tie;
        public List<List<(double X, double Y)>> Foot = new();
        public double MinX, MinY, MaxX, MaxY;
        public double SlabOverlap, SlabT;
    }

    public static QuantityResult Compute(Project p, DesignSummary s, QuantitySettings qs = null)
    {
        if (p == null) throw new ArgumentNullException(nameof(p));
        qs ??= p.QuantitySettings ?? new QuantitySettings();
        var q = new QuantityResult { Settings = qs, HasDesign = s != null };
        var cs = p.Concrete ?? new ConcreteDesignSettings();
        var byNode = new Dictionary<int, List<FrameElement>>();
        foreach (var f in p.Frames)
        {
            foreach (int id in new[] { f.NodeI, f.NodeJ })
            {
                if (!byNode.TryGetValue(id, out var l)) byNode[id] = l = new List<FrameElement>();
                l.Add(f);
            }
        }
        var nodeIds = new HashSet<int>(p.Nodes.Select(n => n.Id));

        // ---------------- cột ----------------
        var colTopDeduct = new Dictionary<int, double>();
        var columns = new List<(FrameElement F, FrameSection Sec, Node Lo, Node Hi)>();
        foreach (var f in p.Frames.Where(f => f.Kind == FrameKind.Column))
        {
            if (!nodeIds.Contains(f.NodeI) || !nodeIds.Contains(f.NodeJ)) continue;
            var sec = Sec(p, f.Section);
            if (sec == null || sec.Shape != SectionShape.RectConcrete) continue;
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            var lo = a.Z <= b.Z ? a : b; var hi = a.Z <= b.Z ? b : a;
            columns.Add((f, sec, lo, hi));
        }

        // ---------------- dầm (thông tin hình học, chưa ghi) ----------------
        var beams = new List<BeamInfo>();
        foreach (var f in p.Frames.Where(f => f.Kind == FrameKind.Beam))
        {
            if (!nodeIds.Contains(f.NodeI) || !nodeIds.Contains(f.NodeJ)) continue;
            var sec = Sec(p, f.Section);
            if (sec == null || sec.Shape != SectionShape.RectConcrete) continue;
            try
            {
                var bi = BuildBeam(p, f, sec, byNode, cs);
                if (bi != null) beams.Add(bi);
            }
            catch (Exception ex) { q.Warnings.Add($"Dầm {f.Label}: {ex.Message}"); }
        }

        // ---------------- sàn ----------------
        foreach (var sl in p.Slabs)
        {
            try { SlabQty(p, q, sl, beams, columns, cs); }
            catch (Exception ex) { q.Warnings.Add($"Sàn {sl.Label}: {ex.Message}"); }
        }

        // ---------------- ghi cột ----------------
        foreach (var (f, sec, lo, hi) in columns)
        {
            double H = hi.Z - lo.Z;
            if (H <= 1e-6) continue;
            double hTop = 0;
            if (byNode.TryGetValue(hi.Id, out var atTop))
                foreach (var bm in atTop.Where(x => x.Kind == FrameKind.Beam))
                {
                    var bs = Sec(p, bm.Section);
                    if (bs != null) hTop = Math.Max(hTop, bs.Shape == SectionShape.RectConcrete ? bs.H : bs.Props(0.5).Depth);
                }
            foreach (var sl in p.Slabs.Where(x => x.Nodes.Contains(hi.Id)))
            {
                var ss = SlabSec(p, sl.Section);
                if (ss != null) hTop = Math.Max(hTop, ss.Thickness);
            }
            double Hc = Math.Max(0, H - hTop);
            q.Concrete.Add(new ConcreteItem
            {
                Category = QtyCat.Column, Story = StoryOf(p, f, hi), Member = Lbl(f.Label, "C", f.Id), Grade = Grade(sec.Material, cs.ConcreteColumn),
                Dims = $"{Mm(sec.B)}×{Mm(sec.H)}, H = {F2(H)}",
                Volume = sec.B * sec.H * H,
                Formwork = 2 * (sec.B + sec.H) * Hc,
                FormworkNote = $"2×({F2(sec.B)}+{F2(sec.H)})×{F2(Hc)}"
            });
        }

        // ---------------- ghi dầm ----------------
        foreach (var bi in beams)
        {
            if (bi.Lc <= 1e-6) continue;
            double fw; string note;
            if (bi.Tie)
            {
                fw = 2 * bi.H * bi.Lc;
                note = $"2×{F2(bi.H)}×{F2(bi.Lc)} (thành, đáy trên bê tông lót)";
                double wl = bi.B + 2 * qs.LeanOffset;
                if (qs.LeanThickness > 0)
                    q.Lean.Add(new ConcreteItem { Category = QtyCat.Lean, Story = FoundationStory, Member = "Lót " + Lbl(bi.F.Label, "GM", bi.F.Id), Grade = qs.LeanGrade, Dims = $"{F2(wl)}×{F2(bi.Lc)}×{F2(qs.LeanThickness)}", Volume = wl * bi.Lc * qs.LeanThickness });
            }
            else
            {
                double ratio = bi.B * bi.Lc > 1e-9 ? bi.SlabOverlap / (bi.B * bi.Lc) : 0;
                int ns = (int)Math.Clamp(Math.Round(2 * ratio), 0, 2);
                double hs = ns > 0 ? bi.SlabT : 0;
                double side1 = Math.Max(0, bi.H - hs);
                fw = bi.B * bi.Lc + bi.Lc * ((2 - ns) * bi.H + ns * side1);
                note = ns == 2 ? $"{F2(bi.B)}×{F2(bi.Lc)} + 2×({F2(bi.H)}−{F2(hs)})×{F2(bi.Lc)}"
                     : ns == 1 ? $"{F2(bi.B)}×{F2(bi.Lc)} + ({F2(bi.H)} + {F2(side1)})×{F2(bi.Lc)}"
                     : $"{F2(bi.B)}×{F2(bi.Lc)} + 2×{F2(bi.H)}×{F2(bi.Lc)}";
            }
            q.Concrete.Add(new ConcreteItem
            {
                Category = bi.Tie ? QtyCat.TieBeam : QtyCat.Beam, Story = bi.Tie ? FoundationStory : bi.Story,
                Member = Lbl(bi.F.Label, bi.Tie ? "GM" : "D", bi.F.Id), Grade = bi.Grade,
                Dims = $"{Mm(bi.B)}×{Mm(bi.H)}, L = {F2(bi.L)}, Ltt = {F2(bi.Lc)}" + (bi.F.IsCurved ? " (cong)" : ""),
                Volume = bi.B * bi.H * bi.Lc, Formwork = fw, FormworkNote = note
            });
        }

        // ---------------- móng ----------------
        FoundationQty(p, q, s, qs, cs);

        // ---------------- cầu thang (Report/StairQuantity.cs) ----------------
        if (qs.IncludeStairs) StairQty(p, q, qs, cs);

        // ---------------- tường + lanh tô ----------------
        WallQty(p, q, qs, cs, columns);

        // ---------------- hoàn thiện sàn ----------------
        FinishQty(p, q);

        // ---------------- cốt thép ----------------
        RebarQty(q, s);

        // ---------------- phân tích vật tư ----------------
        MaterialQty(q, qs);

        // ---------------- thứ tự tầng + quy ước ----------------
        var order = new List<string> { FoundationStory };
        order.AddRange(p.Stories.OrderBy(t => t.Elevation).Select(t => t.Name));
        foreach (var st in q.Concrete.Select(c => c.Story).Concat(q.Masonry.Select(m => m.Story)).Distinct())
            if (!order.Contains(st)) order.Add(st);
        q.StoryOrder = order;
        q.Concrete = q.Concrete.OrderBy(c => QtyCat.Rank(c.Category)).ThenBy(c => order.IndexOf(c.Story)).ThenBy(c => c.Member, StringComparer.Ordinal).ToList();
        Notes(q, qs);
        return q;
    }

    // ==================================================================
    private static BeamInfo BuildBeam(Project p, FrameElement f, FrameSection sec, Dictionary<int, List<FrameElement>> byNode, ConcreteDesignSettings cs)
    {
        var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
        var pts = f.IsCurved ? CurvedBeam.Discretize(p, f) : new List<Vec3> { a.P, b.P };
        if (pts.Count < 2) return null;
        double L = p.Length(f);
        if (L < 1e-6) return null;
        var u0 = Horiz(pts[1] - pts[0]);
        var u1 = Horiz(pts[^1] - pts[^2]);
        double zmax = Math.Max(a.Z, b.Z);
        bool tie = zmax <= p.BaseElevation + ZTol;
        var bi = new BeamInfo
        {
            F = f, B = sec.B, H = sec.H, L = L, Z = (a.Z + b.Z) / 2, Tie = tie,
            Grade = Grade(sec.Material, cs.ConcreteBeam), Story = StoryOf(p, f, a.Z >= b.Z ? a : b)
        };
        bi.WL = EndWidth(p, f, sec, f.NodeI, u0, byNode);
        bi.WR = EndWidth(p, f, sec, f.NodeJ, u1, byNode);
        bi.Lc = Math.Max(0, L - bi.WL / 2 - bi.WR / 2);
        // vết dầm trên mặt bằng (các hình chữ nhật theo từng đoạn), cắt đầu theo mép gối
        int n = pts.Count - 1;
        for (int i = 0; i < n; i++)
        {
            var p0 = pts[i]; var p1 = pts[i + 1];
            var d = Horiz(p1 - p0);
            double len = Math.Sqrt((p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y));
            if (len < 1e-9) continue;
            double t0 = i == 0 ? Math.Min(bi.WL / 2, len) : 0;
            double t1 = i == n - 1 ? Math.Max(t0, len - bi.WR / 2) : len;
            if (t1 - t0 < 1e-9) continue;
            double sx = p0.X + d.X * t0, sy = p0.Y + d.Y * t0, ex = p0.X + d.X * t1, ey = p0.Y + d.Y * t1;
            double nx = -d.Y * sec.B / 2, ny = d.X * sec.B / 2;
            bi.Foot.Add(new List<(double X, double Y)> { (sx - nx, sy - ny), (ex - nx, ey - ny), (ex + nx, ey + ny), (sx + nx, sy + ny) });
        }
        var all = bi.Foot.SelectMany(x => x).ToList();
        if (all.Count == 0) { bi.MinX = bi.MaxX = a.X; bi.MinY = bi.MaxY = a.Y; }
        else { bi.MinX = all.Min(v => v.X); bi.MaxX = all.Max(v => v.X); bi.MinY = all.Min(v => v.Y); bi.MaxY = all.Max(v => v.Y); }
        return bi;
    }

    /// <summary>Bề rộng gối tại đầu dầm theo phương dầm: cột (kích thước theo phương dầm); không có cột → dầm chính đỡ dầm này
    /// (sâu hơn; bằng chiều cao thì dầm phương X chạy suốt, dầm phương Y dừng tại mép dầm X).</summary>
    private static double EndWidth(Project p, FrameElement f, FrameSection sec, int nodeId, Vec3 u, Dictionary<int, List<FrameElement>> byNode)
    {
        if (!byNode.TryGetValue(nodeId, out var fs)) return 0;
        double w = 0; bool col = false;
        foreach (var c in fs.Where(x => x.IsVerticalKind && x.Id != f.Id))
        {
            var cs = Sec(p, c.Section);
            if (cs == null) continue;
            col = true;
            if (cs.Shape == SectionShape.RectConcrete) w = Math.Max(w, ColumnDimAlong(p, c, cs, u));
            else w = Math.Max(w, cs.Props(0.5).Depth);
        }
        if (col) return w;
        bool ownX = Math.Abs(u.X) >= Math.Abs(u.Y);
        foreach (var bm in fs.Where(x => x.Kind == FrameKind.Beam && x.Id != f.Id))
        {
            var bs = Sec(p, bm.Section);
            if (bs == null || bs.Shape != SectionShape.RectConcrete) continue;
            var na = p.Node(bm.NodeI); var nb = p.Node(bm.NodeJ);
            var v = Horiz(nb.P - na.P);
            if (Math.Abs(u.X * v.Y - u.Y * v.X) < 0.25) continue;   // song song (dầm nối tiếp)
            bool support = bs.H > sec.H + 1e-6 || (Math.Abs(bs.H - sec.H) <= 1e-6 && !ownX && Math.Abs(v.X) >= Math.Abs(v.Y));
            if (support) w = Math.Max(w, bs.B);
        }
        return w;
    }

    private static double ColumnDimAlong(Project p, FrameElement c, FrameSection cs, Vec3 u)
    {
        var e2 = ColAxis(p, c, 1);
        var d = new Vec3(u.X, u.Y, 0).Normalize();
        return Math.Abs(e2.Dot(d)) >= 0.7071 ? cs.H : cs.B;
    }

    /// <summary>Trục địa phương của cột (row = 1: trục 2 song song H; row = 2: trục 3 song song B).</summary>
    private static Vec3 ColAxis(Project p, FrameElement c, int row)
    {
        var R = ElementMatrices.LocalAxes(p.Node(c.NodeI).P, p.Node(c.NodeJ).P, c.Angle);
        return new Vec3(R[row, 0], R[row, 1], R[row, 2]);
    }

    private static List<(double X, double Y)> ColumnFoot(Project p, FrameElement c, FrameSection cs, double x, double y)
    {
        var e2 = ColAxis(p, c, 1); var e3 = ColAxis(p, c, 2);
        double hx = e2.X * cs.H / 2, hy = e2.Y * cs.H / 2, bx = e3.X * cs.B / 2, by = e3.Y * cs.B / 2;
        return new List<(double X, double Y)> { (x - hx - bx, y - hy - by), (x + hx - bx, y + hy - by), (x + hx + bx, y + hy + by), (x - hx + bx, y - hy + by) };
    }

    // ==================================================================
    private static void SlabQty(Project p, QuantityResult q, Slab sl, List<BeamInfo> beams,
        List<(FrameElement F, FrameSection Sec, Node Lo, Node Hi)> columns, ConcreteDesignSettings cs)
    {
        if (sl.Nodes.Count < 3) return;
        var nodes = sl.Nodes.Select(id => p.Node(id)).ToList();
        var poly = nodes.Select(n => (n.X, n.Y)).ToList();
        double z = nodes.Average(n => n.Z);
        double A = Math.Abs(Area(poly));
        if (A < 1e-9) return;
        var ss = SlabSec(p, sl.Section);
        double t = ss?.Thickness ?? 0.1;
        double minX = poly.Min(v => v.X), maxX = poly.Max(v => v.X), minY = poly.Min(v => v.Y), maxY = poly.Max(v => v.Y);
        double ob = 0, oc = 0, oo = 0;
        foreach (var bi in beams)
        {
            if (bi.Tie || Math.Abs(bi.Z - z) > ZTol) continue;
            if (bi.MaxX < minX - 1e-6 || bi.MinX > maxX + 1e-6 || bi.MaxY < minY - 1e-6 || bi.MinY > maxY + 1e-6) continue;
            double a = bi.Foot.Sum(r => ClipArea(poly, r));
            if (a > 1e-9) { ob += a; bi.SlabOverlap += a; bi.SlabT = Math.Max(bi.SlabT, t); }
        }
        foreach (var (f, sec, lo, hi) in columns)
        {
            if (Math.Abs(hi.Z - z) > ZTol) continue;
            if (hi.X < minX - 1 || hi.X > maxX + 1 || hi.Y < minY - 1 || hi.Y > maxY + 1) continue;
            oc += ClipArea(poly, ColumnFoot(p, f, sec, hi.X, hi.Y));
        }
        double oOpen = 0;
        foreach (var op in p.Openings.Where(o => o.Story == sl.Story))
        {
            var r = new List<(double X, double Y)> { (op.X0, op.Y0), (op.X1, op.Y0), (op.X1, op.Y1), (op.X0, op.Y1) };
            oOpen += ClipArea(poly, r);
        }
        oo = oOpen;
        double net = Math.Max(0, A - ob - oc - oo);
        bool balcony = (sl.BuildUp ?? "").IndexOf("Ban công", StringComparison.OrdinalIgnoreCase) >= 0
                    || (sl.BuildUp ?? "").IndexOf("lô gia", StringComparison.OrdinalIgnoreCase) >= 0;
        string st = !string.IsNullOrEmpty(sl.Story) ? sl.Story : nodes[0].Story;
        q.Concrete.Add(new ConcreteItem
        {
            Category = balcony ? QtyCat.Balcony : QtyCat.Slab, Story = st, Member = Lbl(sl.Label, "S", sl.Id),
            Grade = Grade(ss?.Material, cs.ConcreteSlab),
            Dims = $"A = {F2(A)} − dầm {F2(ob)} − cột {F2(oc)}" + (oo > 1e-6 ? $" − lỗ {F2(oo)}" : "") + $" = {F2(net)} m²; hb = {Mm(t)}",
            Volume = net * t, Formwork = net, FormworkNote = $"đáy sàn {F2(net)} m²",
            Note = sl.BuildUp ?? ""
        });
    }

    // ==================================================================
    private static void FoundationQty(Project p, QuantityResult q, DesignSummary s, QuantitySettings qs, ConcreteDesignSettings cs)
    {
        string g = Grade(cs.ConcreteFooting, "B20");
        double lt = Math.Max(0, qs.LeanThickness), lo = Math.Max(0, qs.LeanOffset);
        void Lean(string member, double l, double b, int n)
        {
            if (lt <= 0 || l <= 0 || b <= 0) return;
            double L1 = l + 2 * lo, B1 = b + 2 * lo;
            q.Lean.Add(new ConcreteItem { Category = QtyCat.Lean, Story = FoundationStory, Member = "Lót " + member, Grade = qs.LeanGrade, Count = n, Dims = $"{F2(L1)}×{F2(B1)}×{F2(lt)}", Volume = n * L1 * B1 * lt });
        }
        bool any = false;
        if (s != null)
        {
            foreach (var f in s.Footings.Where(f => f.L > 0 && f.B > 0 && f.H > 0))
            {
                any = true;
                int n = Math.Max(1, f.Quantity);
                double V, fw; string dims, fwn;
                if (f.Hb > 1e-6 && f.Hb < f.H - 1e-6)
                {
                    double tx = Math.Min(f.L, Math.Max(f.ColumnB, 0.1) + 0.1), ty = Math.Min(f.B, Math.Max(f.ColumnH, 0.1) + 0.1);
                    double hv = f.H - f.Hb;
                    double A1 = f.L * f.B, A2 = tx * ty, Am = (f.L + tx) / 2 * (f.B + ty) / 2;
                    V = A1 * f.Hb + hv / 6 * (A1 + 4 * Am + A2);
                    fw = 2 * (f.L + f.B) * f.Hb;
                    dims = $"{F2(f.L)}×{F2(f.B)}×{F2(f.H)} (vát, mép {F2(f.Hb)})";
                    fwn = $"2×({F2(f.L)}+{F2(f.B)})×{F2(f.Hb)}";
                }
                else
                {
                    V = f.L * f.B * f.H; fw = 2 * (f.L + f.B) * f.H;
                    dims = $"{F2(f.L)}×{F2(f.B)}×{F2(f.H)}"; fwn = $"2×({F2(f.L)}+{F2(f.B)})×{F2(f.H)}";
                }
                q.Concrete.Add(new ConcreteItem { Category = QtyCat.Footing, Story = FoundationStory, Member = f.Name, Grade = g, Count = n, Dims = dims, Volume = n * V, Formwork = n * fw, FormworkNote = (n > 1 ? n + "×" : "") + fwn });
                Lean(f.Name, f.L, f.B, n);
            }
            var fs = p.Foundation ?? new FoundationSettings();
            int piles = 0; double plen = 0, pvol = 0, parea = 0; string psize = "";
            foreach (var c in s.PileCaps.Where(c => c.L > 0 && c.B > 0 && c.H > 0))
            {
                any = true;
                int n = Math.Max(1, c.Quantity);
                q.Concrete.Add(new ConcreteItem
                {
                    Category = QtyCat.PileCap, Story = FoundationStory, Member = c.Name, Grade = g, Count = n,
                    Dims = $"{F2(c.L)}×{F2(c.B)}×{F2(c.H)}, {c.Piles.Count} cọc", Volume = n * c.L * c.B * c.H,
                    Formwork = n * 2 * (c.L + c.B) * c.H, FormworkNote = (n > 1 ? n + "×" : "") + $"2×({F2(c.L)}+{F2(c.B)})×{F2(c.H)}"
                });
                Lean(c.Name, c.L, c.B, n);
                double d = c.PileSize > 0 ? c.PileSize : fs.PileSize;
                bool round = c.PileSize > 0 ? c.PileRound : fs.PileRound;
                double len = c.PileLength > 0 ? c.PileLength : fs.PileLength;
                double a = round ? Math.PI * d * d / 4 : d * d;
                int np = c.Piles.Count * n;
                piles += np; plen += np * len; pvol += np * a * len; parea = a;
                psize = (round ? "D" : "") + Mm(d) + (round ? "" : "×" + Mm(d));
            }
            if (piles > 0)
            {
                q.Piles = new PileSummary { Count = piles, LengthEach = plen / piles, TotalLength = plen, Area = parea, Volume = pvol, Size = psize, Grade = g };
                q.Concrete.Add(new ConcreteItem { Category = QtyCat.Pile, Story = FoundationStory, Member = $"Cọc {psize}", Grade = g, Count = piles, Dims = $"{piles} cọc × {F2(plen / piles)} m = {F2(plen)} m", Volume = pvol, Note = "Cọc đúc sẵn/khoan nhồi – ván khuôn, thép cọc tính riêng" });
            }
            var strips = s.StripFootings.Where(x => x.Width > 0 && x.End > x.Start).ToList();
            foreach (var sf in strips)
            {
                any = true;
                int n = Math.Max(1, sf.Quantity);
                double len = sf.Length;
                double Ash = StripArea(sf);
                double fw = 2 * len * Math.Max(0, sf.FlangeEdge) + 2 * len * Math.Max(0, sf.RibH - Math.Max(sf.FlangeRoot, sf.FlangeEdge)) + 2 * Ash;
                q.Concrete.Add(new ConcreteItem
                {
                    Category = QtyCat.Strip, Story = FoundationStory, Member = sf.Name, Grade = g, Count = n,
                    Dims = $"B = {F2(sf.Width)}, sườn {Mm(sf.RibB)}×{Mm(sf.RibH)}, L = {F2(len)}", Volume = n * Ash * len,
                    Formwork = n * fw, FormworkNote = $"2×{F2(len)}×{F2(sf.FlangeEdge)} + 2×{F2(len)}×{F2(Math.Max(0, sf.RibH - Math.Max(sf.FlangeRoot, sf.FlangeEdge)))} + 2 đầu"
                });
                Lean(sf.Name, len, sf.Width, n);
            }
            // trừ phần giao nhau giữa dải phương X và phương Y (móng băng giao thoa)
            foreach (var sx in strips.Where(x => x.AlongX))
                foreach (var sy in strips.Where(x => !x.AlongX))
                {
                    if (sy.C < sx.Start - 1e-6 || sy.C > sx.End + 1e-6 || sx.C < sy.Start - 1e-6 || sx.C > sy.End + 1e-6) continue;
                    double hx = StripArea(sx) / sx.Width, hy = StripArea(sy) / sy.Width;
                    double dv = sx.Width * sy.Width * Math.Min(hx, hy);
                    q.Concrete.Add(new ConcreteItem { Category = QtyCat.Strip, Story = FoundationStory, Member = $"Trừ giao {sx.Name}×{sy.Name}", Grade = g, Dims = $"−{F2(sx.Width)}×{F2(sy.Width)}×{F2(Math.Min(hx, hy))}", Volume = -dv });
                    if (lt > 0)
                        q.Lean.Add(new ConcreteItem { Category = QtyCat.Lean, Story = FoundationStory, Member = $"Trừ giao lót {sx.Name}×{sy.Name}", Grade = qs.LeanGrade, Volume = -(sx.Width + 2 * lo) * (sy.Width + 2 * lo) * lt });
                }
        }
        if (!any)
        {
            // chưa thiết kế: lấy kích thước móng khai báo trong mô hình (nếu có)
            foreach (var f in p.Footings.Where(f => f.L > 0 && f.B > 0 && f.Height > 0))
            {
                q.Concrete.Add(new ConcreteItem
                {
                    Category = QtyCat.Footing, Story = FoundationStory, Member = f.Label, Grade = g, Dims = $"{F2(f.L)}×{F2(f.B)}×{F2(f.Height)} (khai báo)",
                    Volume = f.L * f.B * f.Height, Formwork = 2 * (f.L + f.B) * f.Height, FormworkNote = $"2×({F2(f.L)}+{F2(f.B)})×{F2(f.Height)}"
                });
                Lean(f.Label, f.L, f.B, 1);
            }
            if (s == null) q.Warnings.Add("Chưa có kết quả thiết kế: khối lượng móng lấy theo kích thước khai báo (nếu có), chưa có khối lượng cốt thép.");
        }
    }

    /// <summary>Diện tích tiết diện móng băng chữ T ngược: sườn b×h + 2 cánh hình thang.</summary>
    public static double StripArea(StripFootingDesign sf)
    {
        double hf = (Math.Max(0, sf.FlangeEdge) + Math.Max(0, sf.FlangeRoot)) / 2;
        double rib = Math.Max(sf.RibB, 0) * Math.Max(sf.RibH, 0);
        double fl = Math.Max(0, sf.Width - sf.RibB) * hf;
        double a = rib + fl;
        return a > 1e-9 ? a : sf.Width * Math.Max(sf.RibH, 0.3);
    }

    // ==================================================================
    /// <summary>Cầu thang: bê tông chỉ gồm bản thang, chiếu nghỉ, dầm cốn, dầm chiếu nghỉ (+ bậc nếu đổ BTCT); bậc xây gạch, ốp bậc,
    /// trát tính thành công tác riêng (QuantityResult.StairWorks). Lỗ sàn chưa khai báo cầu thang → cầu thang mặc định.</summary>
    private static void StairQty(Project p, QuantityResult q, QuantitySettings qs, ConcreteDesignSettings cs) =>
        StairQuantity.Compute(p, q, qs, cs);

    // ==================================================================
    private static void WallQty(Project p, QuantityResult q, QuantitySettings qs, ConcreteDesignSettings cs,
        List<(FrameElement F, FrameSection Sec, Node Lo, Node Hi)> columns)
    {
        var stories = p.Stories.OrderBy(t => t.Elevation).ToList();
        foreach (var w in p.Walls)
        {
            var wt = p.WallTypes.FirstOrDefault(t => t.Name == w.Type);
            if (wt == null) { q.Warnings.Add($"Tường #{w.Id}: không có loại tường \"{w.Type}\"."); continue; }
            var st = stories.FirstOrDefault(t => t.Name == w.Story);
            if (st == null) { q.Warnings.Add($"Tường #{w.Id}: không có tầng {w.Story}."); continue; }
            double L = w.Length;
            if (L < 0.05) continue;
            double hBeam = 0.35;
            try
            {
                var bu = WallLoadGenerator.BeamsUnder(p, w, st.Elevation, wt.Thickness);
                hBeam = bu.Select(b => p.Section(b.f.Section).Props(0.5).Depth).DefaultIfEmpty(0.35).Max();
            }
            catch (Exception) { }
            int idx = stories.IndexOf(st);
            double H = w.Height > 0 ? w.Height : idx + 1 < stories.Count ? stories[idx + 1].Elevation - st.Elevation - Math.Max(hBeam, 0.35) : 0.9;
            if (H <= 0) continue;
            // trừ cột đứng trên sàn tầng này nằm trên trục tường
            double ux = (w.X2 - w.X1) / L, uy = (w.Y2 - w.Y1) / L;
            var cuts = new List<(double a, double b)>();
            foreach (var (f, sec, lo, hi) in columns)
            {
                if (Math.Abs(lo.Z - st.Elevation) > ZTol) continue;
                double rx = lo.X - w.X1, ry = lo.Y - w.Y1;
                double tpar = rx * ux + ry * uy, dper = Math.Abs(-rx * uy + ry * ux);
                double dc = ColumnDimAlong(p, f, sec, new Vec3(ux, uy, 0));
                double dw = ColumnDimAlong(p, f, sec, new Vec3(-uy, ux, 0));
                if (dper > dw / 2 + 1e-3) continue;
                double a = Math.Max(0, tpar - dc / 2), b = Math.Min(L, tpar + dc / 2);
                if (b > a) cuts.Add((a, b));
            }
            double cut = UnionLength(cuts);
            double Ln = Math.Max(0, L - cut);
            double gross = Ln * H;
            double open = w.OpeningArea >= 0 ? w.OpeningArea : wt.OpeningRatio * gross;
            open = Math.Clamp(open, 0, 0.9 * gross);
            double lintelArea = 0;
            if (qs.IncludeLintels && open > 1e-6 && qs.DoorHeight > 0 && qs.LintelHeight > 0)
            {
                double W = open / qs.DoorHeight;
                double len = Math.Min(Ln, W + 2 * Math.Max(0, qs.LintelBearing));
                lintelArea = Math.Min(len * qs.LintelHeight, Math.Max(0, gross - open));
                q.Concrete.Add(new ConcreteItem
                {
                    Category = QtyCat.Lintel, Story = w.Story, Member = $"LT tường #{w.Id}", Grade = Grade(cs.ConcreteBeam, "B20"),
                    Dims = $"{Mm(wt.Thickness)}×{Mm(qs.LintelHeight)}, L = {F2(len)} (cửa rộng ≈ {F2(W)})",
                    Volume = lintelArea * wt.Thickness, Formwork = 2 * len * qs.LintelHeight + W * wt.Thickness,
                    FormworkNote = $"2×{F2(len)}×{F2(qs.LintelHeight)} + đáy {F2(W)}×{F2(wt.Thickness)}",
                    Note = $"Ước tính: diện tích cửa / chiều cao cửa {F2(qs.DoorHeight)} m, 1 lỗ cửa"
                });
            }
            double net = Math.Max(0, gross - open - lintelArea);
            double vol = net * wt.Thickness;
            var mn = qs.MasonryNormFor(wt.Thickness);
            int sides = wt.PlasterSides;
            double pa = (gross - open) * sides;
            double pt = wt.PlasterThickness > 0 ? wt.PlasterThickness : 0.015;
            q.Masonry.Add(new MasonryItem
            {
                WallId = w.Id, Story = w.Story, WallType = wt.Name, Thickness = wt.Thickness, Length = L, NetLength = Ln, Height = H,
                GrossArea = gross, OpeningArea = open, LintelArea = lintelArea, NetArea = net, Volume = vol,
                PlasterSides = sides, PlasterThickness = pt, PlasterArea = pa,
                Bricks = vol * (mn?.Bricks ?? 0), Mortar = vol * (mn?.Mortar ?? 0),
                PlasterMortar = pa * qs.PlasterMortarPerM2 * pt / 0.015
            });
        }
    }

    private static double UnionLength(List<(double a, double b)> iv)
    {
        double tot = 0, ca = double.NegativeInfinity, cb = double.NegativeInfinity;
        foreach (var (a, b) in iv.OrderBy(x => x.a))
        {
            if (a > cb) { if (cb > ca) tot += cb - ca; ca = a; cb = b; }
            else cb = Math.Max(cb, b);
        }
        if (cb > ca) tot += cb - ca;
        return tot;
    }

    // ==================================================================
    private static void FinishQty(Project p, QuantityResult q)
    {
        foreach (var sl in p.Slabs.Where(x => !string.IsNullOrEmpty(x.BuildUp)))
        {
            var bu = p.FloorBuildUps.FirstOrDefault(b => b.Name == sl.BuildUp);
            if (bu == null || sl.Nodes.Count < 3) continue;
            List<(double X, double Y)> poly;
            try { poly = sl.Nodes.Select(id => p.Node(id)).Select(n => (n.X, n.Y)).ToList(); }
            catch (KeyNotFoundException) { continue; }
            double A = Math.Abs(Area(poly));
            foreach (var op in p.Openings.Where(o => o.Story == sl.Story))
                A -= ClipArea(poly, new List<(double X, double Y)> { (op.X0, op.Y0), (op.X1, op.Y0), (op.X1, op.Y1), (op.X0, op.Y1) });
            A = Math.Max(0, A);
            foreach (var l in bu.Layers)
                q.Finishes.Add(new FinishItem
                {
                    BuildUp = bu.Name, Layer = l.Name, Story = sl.Story, Area = A, Thickness = l.Thickness, Volume = A * l.Thickness, Kind = LayerKind(l)
                });
        }
    }

    public static string LayerKind(FloorLayer l)
    {
        string n = (l.Name ?? "").ToLowerInvariant();
        if (n.Contains("vữa") || n.Contains("láng")) return l.Thickness > 0 ? "Vữa" : "Khác";
        if (n.Contains("chống thấm")) return "Chống thấm";
        if (n.Contains("bê tông")) return "Bê tông";
        if (n.Contains("gạch") || n.Contains("granite") || n.Contains("ceramic") || n.Contains("đá")) return "Gạch";
        if (n.Contains("trần")) return "Trần";
        return "Khác";
    }

    // ==================================================================
    private static void RebarQty(QuantityResult q, DesignSummary s)
    {
        var rows = s != null ? RebarSchedule.BuildRows(s) : new List<ScheduleRow>();
        if (s != null) StairSchedule.AppendTo(rows, s);   // thép cầu thang (Drawing/StairDrawing.cs) – không trùng nếu BuildRows đã có
        q.RebarRows = rows;
        q.RebarByDia = RebarSchedule.ByDiameter(rows);
        q.RebarGroups = RebarSchedule.Groups(rows);
        foreach (var g in rows.GroupBy(r => r.Category))
        {
            var row = new RebarCategoryRow { Category = g.Key };
            foreach (var r in g)
            {
                if (r.Dia <= 10) row.Le10 += r.WeightKg;
                else if (r.Dia <= 18) row.From10To18 += r.WeightKg;
                else row.Gt18 += r.WeightKg;
            }
            row.ConcreteVolume = RebarCategoryConcrete(q, g.Key).Sum(c => q.Volume(c));
            q.RebarByCategory.Add(row);
        }
    }

    /// <summary>Nhóm cấu kiện bê tông tương ứng với loại cấu kiện của bảng thống kê thép (để tính kg/m³).</summary>
    public static string[] RebarCategoryConcrete(QuantityResult q, string rebarCategory) => rebarCategory switch
    {
        "Dầm" => new[] { QtyCat.Beam, QtyCat.TieBeam },
        "Cột" => new[] { QtyCat.Column },
        "Sàn" => new[] { QtyCat.Slab, QtyCat.Balcony },
        "Móng" => new[] { QtyCat.Footing },
        "Đài cọc" => new[] { QtyCat.PileCap },
        "Móng băng" => new[] { QtyCat.Strip },
        _ => new[] { rebarCategory }
    };

    // ==================================================================
    private static void MaterialQty(QuantityResult q, QuantitySettings qs)
    {
        var lines = q.MaterialLines;
        void Add(string group, string src, double sq, string su, string mat, double norm, string nu, string unit)
        {
            if (Math.Abs(sq) < 1e-9 || norm <= 0) return;
            lines.Add(new MaterialLine { Group = group, Source = src, SourceQty = sq, SourceUnit = su, Material = mat, Norm = norm, NormUnit = nu, Quantity = sq * norm, Unit = unit });
        }
        // bê tông theo cấp độ bền
        var byGrade = q.Concrete.Concat(q.Lean).GroupBy(c => c.Grade).OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var g in byGrade)
        {
            double v = g.Sum(c => c.Volume);
            if (v <= 1e-9) continue;
            var n = qs.ConcreteNorm(g.Key);
            if (n == null) { q.Warnings.Add($"Chưa có định mức cấp phối cho bê tông {g.Key} – thêm vào bảng Định mức."); continue; }
            string src = $"Bê tông {g.Key}";
            Add("Bê tông", src, v, "m³", "Xi măng", n.Cement, "kg/m³", "kg");
            Add("Bê tông", src, v, "m³", "Cát vàng", n.Sand, "m³/m³", "m³");
            Add("Bê tông", src, v, "m³", "Đá dăm", n.Stone, "m³/m³", "m³");
            Add("Bê tông", src, v, "m³", "Nước", n.Water, "lít/m³", "lít");
        }
        // vữa
        void Mortar(string src, double vol, string grade)
        {
            if (vol <= 1e-9) return;
            var m = qs.MortarNorm(grade);
            if (m == null) { q.Warnings.Add($"Chưa có định mức cấp phối vữa {grade}."); return; }
            Add("Vữa", src + " " + grade, vol, "m³", "Xi măng", m.Cement, "kg/m³", "kg");
            Add("Vữa", src + " " + grade, vol, "m³", "Cát vàng", m.Sand, "m³/m³", "m³");
            Add("Vữa", src + " " + grade, vol, "m³", "Nước", m.Water, "lít/m³", "lít");
        }
        double vMas = q.Masonry.Sum(m => m.Volume);
        foreach (var g in q.Masonry.GroupBy(m => qs.MasonryNormFor(m.Thickness)))
        {
            double v = g.Sum(m => m.Volume);
            if (g.Key != null) Add("Xây tường", "Xây " + g.Key.Name.ToLowerInvariant(), v, "m³", qs.BrickName, g.Key.Bricks, "viên/m³", "viên");
        }
        Mortar("Vữa xây", q.Masonry.Sum(m => m.Mortar), qs.MasonryMortar);
        Mortar("Vữa trát tường", q.Masonry.Sum(m => m.PlasterMortar), qs.PlasterMortar);
        Mortar("Vữa lót, láng, trát trần (cấu tạo sàn)", q.Finishes.Where(f => f.Kind == "Vữa").Sum(f => f.Volume), qs.FloorMortar);
        // lớp hoàn thiện khác (m²)
        foreach (var g in q.Finishes.Where(f => f.Kind is "Gạch" or "Chống thấm" or "Trần").GroupBy(f => f.Layer))
            Add("Hoàn thiện sàn", g.Key, g.Sum(f => f.Area), "m²", g.Key, 1, "m²/m²", "m²");
        foreach (var g in q.Finishes.Where(f => f.Kind == "Bê tông").GroupBy(f => f.Layer))
            Add("Hoàn thiện sàn", g.Key, g.Sum(f => f.Volume), "m³", g.Key, 1, "m³/m³", "m³");
        // cầu thang: xây bậc, ốp bậc, vữa lót, trát (Report/StairQuantity.cs)
        StairQuantity.AddMaterials(q, qs, Add, Mortar);
        // cốt thép
        var gr = q.RebarGroups ?? new GroupSummary();
        Add("Cốt thép", "Cốt thép Ø≤10", gr.WeightLe10, "kg", "Thép Ø≤10", 1, "kg/kg", "kg");
        Add("Cốt thép", "Cốt thép 10<Ø≤18", gr.Weight10To18, "kg", "Thép 10<Ø≤18", 1, "kg/kg", "kg");
        Add("Cốt thép", "Cốt thép Ø>18", gr.WeightGt18, "kg", "Thép Ø>18", 1, "kg/kg", "kg");
        Add("Cốt thép", "Cốt thép (tổng)", gr.Total / 1000.0, "tấn", "Dây thép buộc", qs.TieWirePerTon, "kg/tấn", "kg");
        // ván khuôn
        double fwTot = q.FormworkTotal;
        Add("Ván khuôn", "Ván khuôn", fwTot / 100.0, "100 m²", "Gỗ ván khuôn", qs.FormworkWoodPer100, "m³/100 m²", "m³");
        Add("Ván khuôn", "Ván khuôn", fwTot / 100.0, "100 m²", "Đinh", qs.FormworkNailsPer100, "kg/100 m²", "kg");

        q.Materials = lines.GroupBy(l => (l.Material, l.Unit)).Select(g => new MaterialTotal
        {
            Material = g.Key.Material, Unit = g.Key.Unit, Quantity = g.Sum(l => l.Quantity), Price = qs.PriceOf(g.Key.Material)
        }).ToList();
    }

    private static void Notes(QuantityResult q, QuantitySettings qs)
    {
        var n = q.Notes;
        n.Add("Cột: tính từ chân cột (mặt móng) đến mặt sàn tầng trên, toàn tiết diện b×h – phần giao cột với dầm, sàn tính vào cột.");
        n.Add("Dầm: chiều dài thông thuỷ tính đến mép cột (dầm phụ tính đến mép dầm chính), tiết diện b×h toàn phần (kể phần nằm trong chiều dày sàn).");
        n.Add("Sàn: diện tích ô sàn (theo tim trục) trừ phần dầm, cột nằm trong ô và lỗ sàn (cầu thang, giếng trời), nhân chiều dày sàn.");
        n.Add("Ván khuôn: cột = chu vi × chiều cao thông thuỷ (đến đáy dầm/đáy sàn); dầm = đáy + 2 thành (thành có sàn trừ chiều dày sàn); sàn = đáy sàn đã trừ dầm, cột; móng = thành bên; giằng móng = 2 thành (đáy trên bê tông lót).");
        n.Add($"Bê tông lót {qs.LeanGrade} dày {Mm(qs.LeanThickness)} mm, mở rộng {Mm(qs.LeanOffset)} mm mỗi phía so với đáy móng, đài, móng băng, giằng móng.");
        n.Add("Tường: chiều dài trừ cột, chiều cao tới đáy dầm (cùng quy ước tính tải tường), trừ diện tích cửa và lanh tô; trát = (diện tích tường − cửa) × số mặt trát.");
        if (qs.IncludeStairs)
        {
            n.Add("Cầu thang: bê tông chỉ tính bản thang (chiều dài nghiêng × bề rộng × chiều dày), chiếu nghỉ, dầm cốn (nếu có) và dầm chiếu nghỉ; bậc thang xây gạch tính khối xây Σ ½·b·h·bề rộng vế (bậc đổ BTCT liền khối mới tính vào bê tông).");
            n.Add("Cầu thang: ốp bậc (đá granite / gỗ / gạch) = (số mặt bậc × b + số cổ bậc × h) × bề rộng vế + mặt chiếu nghỉ; trát mặt dưới bản thang và chiếu nghỉ. Lỗ sàn cầu thang chưa khai báo lấy cầu thang 2 vế mặc định – khai báo cầu thang để có số liệu đúng.");
        }
        if (qs.IncludeLintels) n.Add("Lanh tô (theo diện tích cửa của tường) là khối lượng ƯỚC TÍNH sơ bộ – kiểm tra theo bản vẽ chi tiết.");
        n.Add("Cốt thép lấy theo bảng thống kê cốt thép của chương trình (chưa kể hao hụt, thép chờ, thép cọc).");
        n.Add("Khối lượng chưa kể hao hụt thi công. " + (string.IsNullOrWhiteSpace(qs.NormSource) ? QuantitySettings.DefaultSource : qs.NormSource));
    }

    // ==================================================================
    // Hình học
    // ==================================================================
    public static double Area(List<(double X, double Y)> pts)
    {
        double a = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i]; var q = pts[(i + 1) % pts.Count];
            a += p.X * q.Y - q.X * p.Y;
        }
        return a / 2;
    }

    /// <summary>Diện tích phần giao của đa giác bất kỳ <paramref name="subject"/> với đa giác LỒI <paramref name="clip"/> (Sutherland–Hodgman).</summary>
    public static double ClipArea(List<(double X, double Y)> subject, List<(double X, double Y)> clip)
    {
        if (subject == null || clip == null || subject.Count < 3 || clip.Count < 3) return 0;
        var c = Area(clip) < 0 ? Enumerable.Reverse(clip).ToList() : clip;
        var output = subject.ToList();
        for (int i = 0; i < c.Count && output.Count > 0; i++)
        {
            var a = c[i]; var b = c[(i + 1) % c.Count];
            var input = output; output = new List<(double X, double Y)>();
            for (int k = 0; k < input.Count; k++)
            {
                var cur = input[k]; var prev = input[(k + input.Count - 1) % input.Count];
                bool cin = Side(a, b, cur) >= -1e-12, pin = Side(a, b, prev) >= -1e-12;
                if (cin)
                {
                    if (!pin) output.Add(Intersect(prev, cur, a, b));
                    output.Add(cur);
                }
                else if (pin) output.Add(Intersect(prev, cur, a, b));
            }
        }
        return output.Count < 3 ? 0 : Math.Abs(Area(output));
    }

    private static double Side((double X, double Y) a, (double X, double Y) b, (double X, double Y) p) =>
        (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

    private static (double X, double Y) Intersect((double X, double Y) p1, (double X, double Y) p2, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = p2.X - p1.X, dy = p2.Y - p1.Y, ex = b.X - a.X, ey = b.Y - a.Y;
        double den = dx * ey - dy * ex;
        if (Math.Abs(den) < 1e-15) return p2;
        double t = ((a.X - p1.X) * ey - (a.Y - p1.Y) * ex) / den;
        return (p1.X + t * dx, p1.Y + t * dy);
    }

    private static Vec3 Horiz(Vec3 v) => new Vec3(v.X, v.Y, 0).Normalize();

    // ==================================================================
    private static FrameSection Sec(Project p, string name)
    {
        try { return p.Section(name); } catch (KeyNotFoundException) { return null; }
    }

    private static SlabSection SlabSec(Project p, string name) => p.SlabSections.FirstOrDefault(x => x.Name == name);

    /// <summary>Cấp độ bền bê tông: tên vật liệu nếu là cấp bê tông "Bxx", ngược lại lấy giá trị mặc định.</summary>
    public static string Grade(string material, string fallback)
    {
        string m = (material ?? "").Trim();
        if (m.Length >= 2 && (m[0] == 'B' || m[0] == 'b') && char.IsDigit(m[1])) return "B" + m.Substring(1).Replace(',', '.');
        string f = (fallback ?? "").Trim();
        return f.Length > 0 ? f : "B20";
    }

    private static string StoryOf(Project p, FrameElement f, Node top)
    {
        if (!string.IsNullOrEmpty(f.Story)) return f.Story;
        if (!string.IsNullOrEmpty(top.Story)) return top.Story;
        return p.StoryAt(top.Z)?.Name ?? "";
    }

    private static string Lbl(string label, string prefix, int id) => string.IsNullOrWhiteSpace(label) ? prefix + id : label;
    private static string Mm(double m) => (m * 1000).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
    private static string F2(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
