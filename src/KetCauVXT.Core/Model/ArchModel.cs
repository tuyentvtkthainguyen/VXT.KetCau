using KetCauVXT.Import.Arch;

namespace KetCauVXT.Model;

// =====================================================================
// PHẦN KIẾN TRÚC TRONG MÔ HÌNH (bộ môn Kiến trúc)
// Tường + cửa dùng Project.Walls (WallLine, Openings – Model/WallAssembly.cs). Ở đây thêm: phòng, sàn nền tầng 1 (trên đất),
// trần, mái, đồ đạc / thiết bị đặt từ thư viện 3D (Model/Library3D.cs).
// Chuyển Kiến trúc → Kết cấu: ArchStructureSync.Transfer (khác Import/Arch/ArchToStructure – dựng kết cấu từ bản vẽ CAD).
// =====================================================================

/// <summary>Sàn nền tầng 1 – nằm trên đất (on grade): không tải lên khung, chỉ khối lượng, bản vẽ, IFC.</summary>
public class GroundSlab
{
    public int Id { get; set; }
    public string Label { get; set; } = "";
    /// <summary>Đường bao [x, y] (m)</summary>
    public List<double[]> Outline { get; set; } = new();
    public double Thickness { get; set; } = 0.10;
    /// <summary>Cao độ mặt sàn nền (m)</summary>
    public double Elevation { get; set; }
    /// <summary>Luôn true: sàn trên nền đất (không do dầm đỡ)</summary>
    public bool OnGrade { get; set; } = true;
    public string Note { get; set; } = "Sàn nền BTCT trên lớp cát đầm chặt, bê tông lót";
    [System.Text.Json.Serialization.JsonIgnore] public double Area => Geo2.Area(Outline);
}

/// <summary>Phòng (không gian kiến trúc).</summary>
public class Room
{
    public int Id { get; set; }
    public string Story { get; set; } = "";
    public string Name { get; set; } = "Phòng";
    public string Number { get; set; } = "";
    public List<double[]> Outline { get; set; } = new();
    /// <summary>Lát sàn (vd. "Gạch ceramic 600x600")</summary>
    public string FloorFinish { get; set; } = "Gạch ceramic 600x600";
    /// <summary>Cao độ trần so với sàn (m), 0 = không trần</summary>
    public double CeilingHeight { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public double Area => Geo2.Area(Outline);
    [System.Text.Json.Serialization.JsonIgnore] public (double X, double Y) Center => Geo2.Centroid(Outline);
}

/// <summary>Trần (đơn giản: tấm phẳng theo đường bao).</summary>
public class Ceiling
{
    public int Id { get; set; }
    public string Story { get; set; } = "";
    public List<double[]> Outline { get; set; } = new();
    /// <summary>Cao độ trần so với sàn tầng (m)</summary>
    public double Height { get; set; } = 2.9;
    public string Type { get; set; } = "Trần thạch cao khung chìm";
}

/// <summary>Mái kiến trúc (mái bằng BTCT / mái tôn / mái ngói).</summary>
public class RoofElement
{
    public int Id { get; set; }
    public string Story { get; set; } = "";
    public List<double[]> Outline { get; set; } = new();
    public string Type { get; set; } = "Mái tôn";
    /// <summary>Độ dốc (%)</summary>
    public double SlopePercent { get; set; } = 15;
    /// <summary>Chiều cao đỉnh mái trên cao độ tầng (m)</summary>
    public double RidgeHeight { get; set; } = 1.2;
    /// <summary>Hình mái (Model/ArchDetail.cs → RoofGeometry). Mặc định Bằng = cách vẽ cũ (tấm phẳng).</summary>
    public RoofShape Shape { get; set; } = RoofShape.Bang;
    /// <summary>Độ vươn diềm mái ra ngoài đường bao (m)</summary>
    public double Overhang { get; set; }
    /// <summary>Cao độ chân mái (diềm) trên cao độ tầng (m)</summary>
    public double EaveHeight { get; set; }
    /// <summary>Chiều dày lớp mái (m)</summary>
    public double Thickness { get; set; } = 0.05;
    /// <summary>Tải mái (tôn / ngói + xà gồ, kN/m² mặt bằng) dồn lên các ô sàn của tầng nằm trong đường bao; 0 = không dồn</summary>
    public double SlabLoad { get; set; }
}

/// <summary>Đồ đạc / thiết bị đặt từ thư viện 3D (khối bao + mã thư viện).</summary>
public class FurnitureItem
{
    public int Id { get; set; }
    public string Story { get; set; } = "";
    public string LibraryId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Nội thất";
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Góc xoay quanh trục đứng (độ)</summary>
    public double Rotation { get; set; }
    public double Width { get; set; } = 1;
    public double Depth { get; set; } = 0.6;
    public double Height { get; set; } = 0.8;
    /// <summary>Độ cao đáy so với sàn (m)</summary>
    public double Elevation { get; set; }
    /// <summary>Họ tham số (Model/FurnitureFamilies.cs) – hình 3D dựng lại theo Rộng × Sâu × Cao; null = khối bao thư viện</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Family { get; set; }
    /// <summary>Ký hiệu mặt bằng từ thư viện (vòng [u, v] tỷ lệ −0,5…0,5 theo Rộng / Sâu); null = hình chữ nhật</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<List<double[]>> PlanSymbol { get; set; }
    /// <summary>Bộ môn sở hữu: Kiến trúc (nội thất) hoặc Điện nước (thiết bị nhập từ Revit khi chưa có thư viện điện nước)</summary>
    public Discipline Discipline { get; set; } = Discipline.KienTruc;
    /// <summary>Mã ở phần mềm nguồn (Revit UniqueId)</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string ExternalId { get; set; }
}

/// <summary>Phần kiến trúc của dự án.</summary>
public class ArchModel
{
    public List<GroundSlab> GroundSlabs { get; set; } = new();
    public List<Room> Rooms { get; set; } = new();
    public List<Ceiling> Ceilings { get; set; } = new();
    public List<RoofElement> Roofs { get; set; } = new();
    public List<FurnitureItem> Furniture { get; set; } = new();
    /// <summary>Lan can (Model/ArchDetail.cs)</summary>
    public List<Railing> Railings { get; set; } = new();
    /// <summary>Phào chỉ, gờ trang trí chạy theo mặt tường</summary>
    public List<WallSweep> Sweeps { get; set; } = new();
    /// <summary>Vùng ốp / sơn hoàn thiện trên mặt tường</summary>
    public List<WallCladding> Claddings { get; set; } = new();
    /// <summary>Mặt cắt đứng do người dùng vẽ (ngoài 1-1, 2-2 tự sinh) – Drawing/SectionView.cs</summary>
    public List<SectionLine> Sections { get; set; } = new();
}

/// <summary>Hình học phẳng nhỏ dùng chung.</summary>
public static class Geo2
{
    public static double SignedArea(IReadOnlyList<double[]> pts)
    {
        if (pts == null || pts.Count < 3) return 0;
        double a = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i]; var q = pts[(i + 1) % pts.Count];
            a += p[0] * q[1] - q[0] * p[1];
        }
        return a / 2;
    }

    public static double Area(IReadOnlyList<double[]> pts) => Math.Abs(SignedArea(pts));

    public static (double X, double Y) Centroid(IReadOnlyList<double[]> pts)
    {
        if (pts == null || pts.Count == 0) return (0, 0);
        double a = SignedArea(pts);
        if (Math.Abs(a) < 1e-12) return (pts.Average(p => p[0]), pts.Average(p => p[1]));
        double cx = 0, cy = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i]; var q = pts[(i + 1) % pts.Count];
            double c = p[0] * q[1] - q[0] * p[1];
            cx += (p[0] + q[0]) * c; cy += (p[1] + q[1]) * c;
        }
        return (cx / (6 * a), cy / (6 * a));
    }

    public static bool Contains(IReadOnlyList<double[]> poly, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            double xi = poly[i][0], yi = poly[i][1], xj = poly[j][0], yj = poly[j][1];
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi + 1e-300) + xi) inside = !inside;
        }
        return inside;
    }
}

/// <summary>Tìm phòng từ mạng tường (các mặt khép kín nhỏ nhất của đồ thị tim tường).</summary>
public static class RoomFinder
{
    /// <summary>Tìm các vùng khép kín tạo bởi tim tường thẳng của tầng (bỏ vùng ngoài cùng). Diện tích tối thiểu minArea (m²).</summary>
    public static List<List<double[]>> FromWalls(Project p, string story, double minArea = 1.0)
    {
        var segs = p.Walls.Where(w => w.Story == story && !w.IsCurved && w.Chord > 1e-6)
            .Select(w => (A: (w.X1, w.Y1), B: (w.X2, w.Y2))).ToList();
        return Faces(segs, minArea);
    }

    /// <summary>Mặt khép kín nhỏ nhất của một tập đoạn thẳng (tự cắt nhau tại giao điểm).</summary>
    public static List<List<double[]>> Faces(List<((double X, double Y) A, (double X, double Y) B)> segs, double minArea = 1.0)
    {
        const double tol = 1e-4;
        // 1. chia đoạn tại giao điểm và đầu mút nằm trên đoạn khác
        var cuts = segs.Select(_ => new List<double> { 0, 1 }).ToList();
        for (int i = 0; i < segs.Count; i++)
            for (int j = i + 1; j < segs.Count; j++)
            {
                var (a, b) = segs[i]; var (c, d) = segs[j];
                double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y;
                double den = rx * sy - ry * sx;
                if (Math.Abs(den) < 1e-12) continue;
                double t = ((c.X - a.X) * sy - (c.Y - a.Y) * sx) / den;
                double u = ((c.X - a.X) * ry - (c.Y - a.Y) * rx) / den;
                double et = tol / Math.Sqrt(rx * rx + ry * ry), eu = tol / Math.Sqrt(sx * sx + sy * sy);
                // nối chữ T / L: cho phép vượt nhẹ đầu đoạn (tường vẽ chưa chạm tim)
                double ext = 0.15;
                double et2 = ext / Math.Sqrt(rx * rx + ry * ry), eu2 = ext / Math.Sqrt(sx * sx + sy * sy);
                if (t < -et2 || t > 1 + et2 || u < -eu2 || u > 1 + eu2) continue;
                cuts[i].Add(Math.Clamp(t, 0, 1)); cuts[j].Add(Math.Clamp(u, 0, 1));
                _ = et; _ = eu;
            }
        // 2. đỉnh + cạnh
        var verts = new List<(double X, double Y)>();
        int V((double X, double Y) q)
        {
            for (int k = 0; k < verts.Count; k++)
                if (Math.Abs(verts[k].X - q.X) < 0.02 && Math.Abs(verts[k].Y - q.Y) < 0.02) return k;
            verts.Add(q); return verts.Count - 1;
        }
        var edges = new HashSet<(int, int)>();
        for (int i = 0; i < segs.Count; i++)
        {
            var (a, b) = segs[i];
            var ts = cuts[i].Distinct().OrderBy(x => x).ToList();
            for (int k = 0; k + 1 < ts.Count; k++)
            {
                var p0 = (a.X + (b.X - a.X) * ts[k], a.Y + (b.Y - a.Y) * ts[k]);
                var p1 = (a.X + (b.X - a.X) * ts[k + 1], a.Y + (b.Y - a.Y) * ts[k + 1]);
                int u = V(p0), v = V(p1);
                if (u == v) continue;
                edges.Add(u < v ? (u, v) : (v, u));
            }
        }
        // 3. nửa cạnh, đi vòng trái nhỏ nhất
        var adj = new Dictionary<int, List<int>>();
        foreach (var (u, v) in edges)
        {
            if (!adj.ContainsKey(u)) adj[u] = new List<int>();
            if (!adj.ContainsKey(v)) adj[v] = new List<int>();
            adj[u].Add(v); adj[v].Add(u);
        }
        double Ang(int from, int to) => Math.Atan2(verts[to].Y - verts[from].Y, verts[to].X - verts[from].X);
        foreach (var k in adj.Keys.ToList()) adj[k] = adj[k].OrderBy(n => Ang(k, n)).ToList();
        var used = new HashSet<(int, int)>();
        var faces = new List<List<double[]>>();
        foreach (var (u0, v0) in edges.SelectMany(e => new[] { (e.Item1, e.Item2), (e.Item2, e.Item1) }))
        {
            if (used.Contains((u0, v0))) continue;
            var cyc = new List<int>();
            int u = u0, v = v0, guard = 0;
            while (guard++ < 10000)
            {
                used.Add((u, v)); cyc.Add(u);
                // tại v: chọn cạnh kế tiếp theo chiều kim đồng hồ từ cạnh ngược (v→u) → mặt bên trái
                var nb = adj[v];
                int idx = nb.IndexOf(u);
                int next = nb[(idx - 1 + nb.Count) % nb.Count];
                u = v; v = next;
                if (u == u0 && v == v0) break;
            }
            if (cyc.Count < 3) continue;
            var poly = cyc.Select(i => new[] { verts[i].X, verts[i].Y }).ToList();
            double a = Geo2.SignedArea(poly);
            if (a > minArea) faces.Add(poly);   // CCW = mặt trong; mặt ngoài có diện tích âm
        }
        return faces;
    }

    /// <summary>Tạo phòng cho tầng từ mạng tường (thay phòng cũ của tầng). Tên phòng lấy từ phòng cũ chứa tâm (nếu có).</summary>
    public static List<Room> Detect(Project p, string story, double minArea = 1.0)
    {
        p.Arch ??= new ArchModel();
        var old = p.Arch.Rooms.Where(r => r.Story == story).ToList();
        p.Arch.Rooms.RemoveAll(r => r.Story == story);
        int id = p.Arch.Rooms.Count == 0 ? 1 : p.Arch.Rooms.Max(r => r.Id) + 1;
        var res = new List<Room>();
        int k = 1;
        foreach (var f in FromWalls(p, story, minArea).OrderBy(f => Geo2.Centroid(f).Y).ThenBy(f => Geo2.Centroid(f).X))
        {
            var c = Geo2.Centroid(f);
            var prev = old.FirstOrDefault(r => r.Outline.Count >= 3 && Geo2.Contains(r.Outline, c.X, c.Y));
            var room = new Room { Id = id++, Story = story, Name = prev?.Name ?? $"Phòng {k}", Number = $"{story}-{k:00}", Outline = f, FloorFinish = prev?.FloorFinish ?? "Gạch ceramic 600x600", CeilingHeight = prev?.CeilingHeight ?? 0 };
            k++;
            p.Arch.Rooms.Add(room); res.Add(room);
        }
        return res;
    }
}

/// <summary>Kết quả chuyển Kiến trúc → Kết cấu.</summary>
public sealed class ArchTransferReport
{
    public int WallsOnBeams { get; set; }
    public int WallsOnSlabs { get; set; }
    public int SlabUsesSet { get; set; }
    public int LintelProposals { get; set; }
    public List<(double X, double Y, string Story)> ProposedColumns { get; } = new();
    public int ColumnsAdded { get; set; }
    public List<string> Lines { get; } = new();
    public override string ToString() => string.Join(Environment.NewLine, Lines);
}

/// <summary>Tạo / cập nhật mô hình kết cấu từ phần kiến trúc.</summary>
public static class ArchStructureSync
{
    /// <summary>Tường → tải dầm (tường nằm trên dầm) hoặc tải sàn; phòng → công năng ô sàn; cửa → gợi ý lanh tô;
    /// giao tường (L, T, +) chưa có cột trong 0,3 m → đề xuất cột (addColumns = true thì thêm cột tiết diện columnSection).</summary>
    public static ArchTransferReport Transfer(Project p, bool addColumns = false, string columnSection = "C22x22")
    {
        var rep = new ArchTransferReport();
        // 1. tường: tải tường được WallLoadGenerator dồn khi phân tích; ở đây chỉ thống kê dầm đỡ
        foreach (var w in p.Walls)
        {
            bool onBeam = p.Frames.Any(f => f.Kind == FrameKind.Beam && f.Story == w.Story && Colinear(p, f, w));
            if (onBeam) rep.WallsOnBeams++; else rep.WallsOnSlabs++;
        }
        rep.Lines.Add($"Tường: {rep.WallsOnBeams} đoạn trên dầm (tải phân bố lên dầm), {rep.WallsOnSlabs} đoạn trên sàn (quy đổi tải sàn) – tải tạo khi phân tích.");
        // 2. phòng → công năng ô sàn
        var catalog = RoomCatalog.Default();
        foreach (var r in p.Arch?.Rooms ?? new List<Room>())
        {
            var rt = ArchPlanReader.Classify(ArchPlanReader.Norm(r.Name), catalog);
            if (rt == null || rt.NoSlab) continue;
            var c = r.Center;
            var s = ModelEdit.SlabAt(p, r.Story, c.X, c.Y);
            if (s == null) continue;
            var use = PlanAxisPicker.UseOf(rt.Kind);
            if (ModelEdit.UseOf(p, s) != use && ModelEdit.SetSlabUse(p, s, use).Ok) rep.SlabUsesSet++;
        }
        rep.Lines.Add($"Phòng → công năng sàn: {rep.SlabUsesSet} ô sàn đổi công năng (tải hoàn thiện, hoạt tải TCVN 2737).");
        // 3. cửa → lanh tô
        rep.LintelProposals = WallAssembly.ProposeLintels(p).Count;
        rep.Lines.Add($"Cửa trên tường: {rep.LintelProposals} lanh tô đề xuất (Triển khai hồ sơ → Lanh tô từ cửa mô hình).");
        // 4. cột tại giao tường
        foreach (var st in p.Walls.Select(w => w.Story).Distinct())
        {
            var ws = p.Walls.Where(w => w.Story == st && !w.IsCurved).ToList();
            var pts = new List<(double X, double Y)>();
            foreach (var w in ws) { pts.Add((w.X1, w.Y1)); pts.Add((w.X2, w.Y2)); }
            foreach (var q in pts.Distinct())
            {
                int deg = ws.Count(w => Near(w.X1, w.Y1, q) || Near(w.X2, w.Y2, q) || OnSegment(w, q));
                if (deg < 2) continue;
                // góc L thật sự: hai tường không thẳng hàng
                var dirs = ws.Where(w => Near(w.X1, w.Y1, q) || Near(w.X2, w.Y2, q) || OnSegment(w, q))
                    .Select(w => Math.Round(Math.Abs(Math.Atan2(w.Y2 - w.Y1, w.X2 - w.X1)) % Math.PI, 2)).Distinct().Count();
                if (dirs < 2) continue;
                bool hasCol = p.Frames.Any(f => f.Kind == FrameKind.Column && f.Story == st && Dist2(p.Node(f.NodeJ), q) < 0.3 * 0.3);
                if (hasCol || rep.ProposedColumns.Any(c => c.Story == st && Math.Abs(c.X - q.X) < 0.05 && Math.Abs(c.Y - q.Y) < 0.05)) continue;
                rep.ProposedColumns.Add((q.X, q.Y, st));
            }
        }
        rep.Lines.Add($"Giao tường chưa có cột: {rep.ProposedColumns.Count} vị trí đề xuất cột.");
        if (addColumns && rep.ProposedColumns.Count > 0)
        {
            foreach (var (x, y, st) in rep.ProposedColumns)
            {
                var story = p.Stories.FirstOrDefault(s => s.Name == st);
                if (story == null) continue;
                var ordered = p.Stories.OrderBy(s => s.Elevation).ToList();
                int i = ordered.IndexOf(story);
                double z0 = i == 0 ? p.BaseElevation : ordered[i - 1].Elevation;
                var a = p.GetOrAddNode(x, y, z0, i == 0 ? "Base" : ordered[i - 1].Name);
                if (i == 0) a.Restraint = Node.Fixed;
                var b = p.GetOrAddNode(x, y, story.Elevation, story.Name);
                if (p.Frames.Any(f => (f.NodeI == a.Id && f.NodeJ == b.Id) || (f.NodeI == b.Id && f.NodeJ == a.Id))) continue;
                p.Frames.Add(new FrameElement { Id = p.NextFrameId(), NodeI = a.Id, NodeJ = b.Id, Kind = FrameKind.Column, Section = columnSection, Story = story.Name, Label = $"C{p.Frames.Count(f => f.Kind == FrameKind.Column) + 1}" });
                rep.ColumnsAdded++;
            }
            p.InvalidateIndex();
            rep.Lines.Add($"Đã thêm {rep.ColumnsAdded} cột {columnSection} (cần kiểm tra lại dầm đỡ và chạy lại phân tích).");
        }
        return rep;
    }

    private static bool Near(double x, double y, (double X, double Y) q) => Math.Abs(x - q.X) < 0.02 && Math.Abs(y - q.Y) < 0.02;
    private static double Dist2(Node n, (double X, double Y) q) => (n.X - q.X) * (n.X - q.X) + (n.Y - q.Y) * (n.Y - q.Y);

    private static bool OnSegment(WallLine w, (double X, double Y) q)
    {
        double dx = w.X2 - w.X1, dy = w.Y2 - w.Y1, L2 = dx * dx + dy * dy;
        if (L2 < 1e-12) return false;
        double t = ((q.X - w.X1) * dx + (q.Y - w.Y1) * dy) / L2;
        if (t <= 0.01 || t >= 0.99) return false;
        double px = w.X1 + t * dx - q.X, py = w.Y1 + t * dy - q.Y;
        return px * px + py * py < 0.02 * 0.02;
    }

    private static bool Colinear(Project p, FrameElement f, WallLine w)
    {
        var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
        double dx = b.X - a.X, dy = b.Y - a.Y, L = Math.Sqrt(dx * dx + dy * dy);
        if (L < 1e-9) return false;
        double D(double x, double y) => Math.Abs((x - a.X) * dy - (y - a.Y) * dx) / L;
        if (D(w.X1, w.Y1) > 0.16 || D(w.X2, w.Y2) > 0.16) return false;
        double t1 = ((w.X1 - a.X) * dx + (w.Y1 - a.Y) * dy) / (L * L), t2 = ((w.X2 - a.X) * dx + (w.Y2 - a.Y) * dy) / (L * L);
        return Math.Max(t1, t2) > 0.05 && Math.Min(t1, t2) < 0.95;
    }
}
