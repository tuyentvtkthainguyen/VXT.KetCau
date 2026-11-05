using System.Text.Json;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Import;

// =====================================================================
// LƯỢC ĐỒ FILE TRAO ĐỔI REVIT → VIỆT CONST (*.vxtrevit.json)
// Do add-in KetCauVXT.RevitExport ghi ra. Mọi kích thước, toạ độ: m. Góc: độ.
// =====================================================================
public class RevitExchange
{
    public string ProjectName { get; set; } = "";
    public string SchemaVersion { get; set; } = "1.0";
    public List<RvLevel> Levels { get; set; } = new();
    public List<RvGrid> Grids { get; set; } = new();
    public List<RvColumn> Columns { get; set; } = new();
    public List<RvBeam> Beams { get; set; } = new();
    public List<RvFloor> Floors { get; set; } = new();
}

public class RvLevel
{
    public string Name { get; set; } = "";
    /// <summary>Cao độ, m</summary>
    public double Elevation { get; set; }
}

public class RvGrid
{
    public string Name { get; set; } = "";
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
}

public class RvColumn
{
    public string Id { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public string BaseLevel { get; set; } = "";
    public string TopLevel { get; set; } = "";
    public double BaseOffset { get; set; }
    public double TopOffset { get; set; }
    /// <summary>Bề rộng b (m) – theo phương X địa phương của họ cột trước khi xoay</summary>
    public double B { get; set; }
    /// <summary>Chiều cao h (m) – theo phương Y địa phương của họ cột trước khi xoay</summary>
    public double H { get; set; }
    public double RotationDeg { get; set; }
    public string Material { get; set; } = "";
}

public class RvBeam
{
    public string Id { get; set; } = "";
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double Z1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public double Z2 { get; set; }
    public double B { get; set; }
    public double H { get; set; }
    public string Level { get; set; } = "";
    public string Material { get; set; } = "";
}

public class RvFloor
{
    public string Id { get; set; } = "";
    public string Level { get; set; } = "";
    public double Thickness { get; set; }
    /// <summary>Đường bao ngoài: danh sách [x, y] (m)</summary>
    public List<double[]> Outline { get; set; } = new();
}

public class RevitImportOptions
{
    public string Concrete { get; set; } = "B25";
    public double FinishLoad { get; set; } = 1.2;
    public double LiveLoad { get; set; } = 1.5;
    public double RoofLiveLoad { get; set; } = 0.75;
    public bool FixedBase { get; set; } = true;
    /// <summary>Dời gốc toạ độ về giao trục đầu tiên</summary>
    public bool MoveToOrigin { get; set; } = true;
    /// <summary>Sai số bắt cao độ dầm/sàn vào cao độ tầng, m</summary>
    public double LevelTolerance { get; set; } = 0.30;
}

/// <summary>Nhập mô hình từ file JSON do add-in Revit xuất ra.</summary>
public static class RevitJsonImporter
{
    public static RevitExchange ReadFile(string path)
    {
        var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        return JsonSerializer.Deserialize<RevitExchange>(File.ReadAllText(path), opt)
               ?? throw new InvalidDataException("File JSON Revit không hợp lệ");
    }

    /// <summary>Tạo dự án mới từ file JSON (giữ vật liệu, tiết diện, trường hợp tải mặc định của NewProject).</summary>
    public static Project ImportNew(string path, out ImportReport rep, RevitImportOptions o = null)
    {
        var ex = ReadFile(path);
        var p = ModelBuilder.NewProject(string.IsNullOrWhiteSpace(ex.ProjectName) ? "Công trình từ Revit" : ex.ProjectName);
        rep = Build(p, ex, o);
        rep.Source = Path.GetFileName(path);
        return p;
    }

    /// <summary>Thay thế mô hình hình học của dự án hiện có bằng dữ liệu từ file JSON.</summary>
    public static ImportReport Import(Project p, string path, RevitImportOptions o = null)
    {
        var rep = Build(p, ReadFile(path), o);
        rep.Source = Path.GetFileName(path);
        return rep;
    }

    public static ImportReport Build(Project p, RevitExchange ex, RevitImportOptions o = null)
    {
        o ??= new RevitImportOptions();
        if (ex == null) throw new ArgumentNullException(nameof(ex));
        // JSON có thể ghi "Grids": null ... → tránh NullReferenceException
        ex.Levels ??= new List<RvLevel>();
        ex.Grids ??= new List<RvGrid>();
        ex.Columns ??= new List<RvColumn>();
        ex.Beams ??= new List<RvBeam>();
        ex.Floors ??= new List<RvFloor>();
        ex.Levels.RemoveAll(l => l == null);
        ex.Grids.RemoveAll(g => g == null);
        ex.Columns.RemoveAll(c => c == null);
        ex.Beams.RemoveAll(b => b == null);
        ex.Floors.RemoveAll(f => f == null);
        var rep = new ImportReport { Source = "Revit" };
        if (!string.IsNullOrWhiteSpace(ex.ProjectName) && (p.Name == "" || p.Name == "Công trình mới")) p.Name = ex.ProjectName;
        ImportGeometry.ClearModel(p);
        ImportGeometry.EnsurePatterns(p);

        // ---------------- GỐC TOẠ ĐỘ ----------------
        double ox = 0, oy = 0;
        var gxs = ex.Grids.Where(g => IsVert(g)).ToList();
        var gys = ex.Grids.Where(g => IsHorz(g)).ToList();
        if (o.MoveToOrigin)
        {
            var xs = gxs.Select(g => (g.X1 + g.X2) / 2).Concat(gxs.Count == 0 ? ex.Columns.Select(c => c.X) : Enumerable.Empty<double>()).ToList();
            var ys = gys.Select(g => (g.Y1 + g.Y2) / 2).Concat(gys.Count == 0 ? ex.Columns.Select(c => c.Y) : Enumerable.Empty<double>()).ToList();
            if (xs.Count > 0) ox = Math.Round(xs.Min(), 4);
            if (ys.Count > 0) oy = Math.Round(ys.Min(), 4);
            if (Math.Abs(ox) > 1e-6 || Math.Abs(oy) > 1e-6) rep.Info.Add($"Dời gốc toạ độ về ({ox:0.000}; {oy:0.000}) của Revit");
        }

        // ---------------- TRỤC ----------------
        p.Grids.Clear();
        foreach (var g in gxs) p.Grids.Add(new GridLine { Name = g.Name, Dir = GridDir.X, Coord = Math.Round((g.X1 + g.X2) / 2 - ox, 4) });
        foreach (var g in gys) p.Grids.Add(new GridLine { Name = g.Name, Dir = GridDir.Y, Coord = Math.Round((g.Y1 + g.Y2) / 2 - oy, 4) });
        int skewed = ex.Grids.Count - gxs.Count - gys.Count;
        if (skewed > 0) rep.Warnings.Add($"Bỏ qua {skewed} trục xiên/cong (chỉ nhận trục song song X, Y).");
        foreach (var dir in new[] { GridDir.X, GridDir.Y })
        {
            var l = p.Grids.Where(g => g.Dir == dir).OrderBy(g => g.Coord).ToList();
            for (int i = 0; i < l.Count; i++)
                if (string.IsNullOrWhiteSpace(l[i].Name) || l.Count(g => g.Name == l[i].Name) > 1) l[i].Name = ImportGeometry.AutoName(dir, i);
        }
        rep.Grids = p.Grids.Count;

        // ---------------- TẦNG ----------------
        var levels = ex.Levels.OrderBy(l => l.Elevation).ToList();
        if (levels.Count < 2)
        {
            if (ex.Columns.Count == 0) throw new InvalidOperationException("File Revit cần ít nhất 2 cao độ (Level) hoặc có cột.");
        }
        var lvElev = levels.GroupBy(q => q.Name ?? "").ToDictionary(g => g.Key, g => g.First().Elevation);
        // cao độ chân cột thấp nhất là mặt móng
        double baseZ = ex.Columns.Count > 0
            ? ex.Columns.Min(c => LevelZ(lvElev, c.BaseLevel, levels) + c.BaseOffset)
            : levels[0].Elevation;
        baseZ = Math.Round(baseZ, 4);
        p.BaseElevation = baseZ;
        p.Stories.Clear();
        double prev = baseZ;
        foreach (var l in levels.Where(q => q.Elevation > baseZ + 0.2))
        {
            p.Stories.Add(new Story { Name = SafeName(l.Name, p.Stories.Count + 2), Elevation = Math.Round(l.Elevation, 4), Height = Math.Round(l.Elevation - prev, 4) });
            prev = l.Elevation;
        }
        // thêm cao độ đỉnh cột chưa có tầng
        foreach (var c in ex.Columns)
        {
            double zt = Math.Round(LevelZ(lvElev, c.TopLevel, levels) + c.TopOffset, 4);
            if (zt > baseZ + 0.2 && p.Stories.All(s => Math.Abs(s.Elevation - zt) > o.LevelTolerance))
            {
                p.Stories.Add(new Story { Name = $"Z{zt:0.00}", Elevation = zt });
                rep.Warnings.Add($"Cột {c.Id}: đỉnh cột +{zt:0.000} không trùng Level – thêm tầng phụ.");
            }
        }
        var stories = p.Stories.OrderBy(s => s.Elevation).ToList();
        for (int i = 0; i < stories.Count; i++) stories[i].Height = Math.Round(stories[i].Elevation - (i == 0 ? baseZ : stories[i - 1].Elevation), 4);
        p.Stories = stories;
        rep.Stories = stories.Count;
        if (stories.Count == 0) throw new InvalidOperationException("Không xác định được tầng nào phía trên chân cột.");

        Story NearestStory(double z) =>
            stories.OrderBy(q => Math.Abs(q.Elevation - z)).First();
        double SnapZ(double z)
        {
            var s = NearestStory(z);
            return Math.Abs(s.Elevation - z) <= o.LevelTolerance ? s.Elevation : Math.Round(z, 4);
        }

        // ---------------- CỘT ----------------
        int ci = 1;
        foreach (var c in ex.Columns)
        {
            double x = c.X - ox, y = c.Y - oy;
            double z0 = SnapZ(LevelZ(lvElev, c.BaseLevel, levels) + c.BaseOffset);
            double z1 = SnapZ(LevelZ(lvElev, c.TopLevel, levels) + c.TopOffset);
            if (z1 - z0 < 0.2) { rep.Warnings.Add($"Cột {c.Id}: chiều cao {z1 - z0:0.00} m quá nhỏ – bỏ qua."); continue; }
            double b = c.B > 0.05 ? ImportGeometry.RoundMm(c.B) : 0.22, h = c.H > 0.05 ? ImportGeometry.RoundMm(c.H) : 0.22;
            if (c.B <= 0.05 || c.H <= 0.05) rep.Warnings.Add($"Cột {c.Id}: thiếu kích thước – lấy 220×220.");
            // Kích thước sau xoay theo phương X/Y tổng thể
            double rot = ((c.RotationDeg % 180) + 180) % 180;
            bool quarter = Math.Abs(rot - 90) < 1;
            double dimX = quarter ? h : b, dimY = quarter ? b : h;
            double bmin = Math.Min(dimX, dimY), hmax = Math.Max(dimX, dimY);
            string mat = ConcreteOf(c.Material, o.Concrete);
            string sec = ImportGeometry.EnsureRect(p, "C", bmin, hmax, mat);
            double angle;
            if (Math.Abs(rot) < 1 || quarter) angle = dimY > dimX + 1e-4 ? 90 : 0;
            else
            {
                // cột xoay góc bất kỳ: trục 2 (cạnh h dài) hợp với X góc rot (+90 nếu cạnh dài là h gốc)
                angle = (h >= b ? rot + 90 : rot) % 360;
                rep.Warnings.Add($"Cột {c.Id} xoay {c.RotationDeg:0.#}° – gán góc trục địa phương {angle:0.#}°.");
            }
            // chia cột qua từng tầng trung gian
            var zs = stories.Select(s => s.Elevation).Where(z => z > z0 + 1e-3 && z < z1 - 1e-3).Prepend(z0).Append(z1).ToList();
            var gx = p.GridsX.FirstOrDefault(g => Math.Abs(g.Coord - x) < 0.02);
            var gy = p.GridsY.FirstOrDefault(g => Math.Abs(g.Coord - y) < 0.02);
            string group = gx != null && gy != null ? $"C-{gx.Name}{gy.Name}" : $"C-{c.Id}";
            for (int k = 0; k + 1 < zs.Count; k++)
            {
                var st = NearestStory(zs[k + 1]);
                var na = p.GetOrAddNode(x, y, zs[k], k == 0 && Math.Abs(zs[k] - baseZ) < 1e-3 ? "Base" : NearestStory(zs[k]).Name);
                var nb = p.GetOrAddNode(x, y, zs[k + 1], st.Name);
                if (Math.Abs(zs[k] - baseZ) < 1e-3) na.Restraint = o.FixedBase ? Node.Fixed : Node.Pinned;
                if (ImportGeometry.AddFrame(p, na, nb, FrameKind.Column, sec, st.Name, $"C{ci}", group, angle) != null) rep.Columns++;
            }
            ci++;
        }

        // ---------------- DẦM ----------------
        int bi = 1;
        foreach (var bm in ex.Beams)
        {
            double z1 = SnapZ(bm.Z1), z2 = SnapZ(bm.Z2);
            if (!string.IsNullOrEmpty(bm.Level) && lvElev.TryGetValue(bm.Level, out double lz) && Math.Abs(bm.Z1 - bm.Z2) < 0.05)
            {
                // dầm ngang: đỉnh dầm theo Level của dầm
                double zl = SnapZ(lz);
                if (Math.Abs(zl - z1) <= o.LevelTolerance) z1 = z2 = zl;
            }
            double x1 = bm.X1 - ox, y1 = bm.Y1 - oy, x2 = bm.X2 - ox, y2 = bm.Y2 - oy;
            if (Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1) + (z2 - z1) * (z2 - z1)) < 0.05) continue;
            double b = bm.B > 0.05 ? ImportGeometry.RoundMm(bm.B) : 0.22, h = bm.H > 0.05 ? ImportGeometry.RoundMm(bm.H) : 0.30;
            string sec = ImportGeometry.EnsureRect(p, "D", b, h, ConcreteOf(bm.Material, o.Concrete));
            var st = NearestStory(Math.Max(z1, z2));
            // bắt đầu dầm vào cột gần nhất (trong phạm vi nửa tiết diện cột + 5 cm)
            (x1, y1) = SnapToColumnTop(p, x1, y1, z1);
            (x2, y2) = SnapToColumnTop(p, x2, y2, z2);
            var na = p.GetOrAddNode(x1, y1, z1, st.Name);
            var nb = p.GetOrAddNode(x2, y2, z2, st.Name);
            var seg = new Seg2(x1, y1, x2, y2);
            string grid = "";
            if (seg.IsAlongX()) grid = p.GridsY.FirstOrDefault(g => Math.Abs(g.Coord - y1) < 0.02)?.Name ?? "";
            else if (seg.IsAlongY()) grid = p.GridsX.FirstOrDefault(g => Math.Abs(g.Coord - x1) < 0.02)?.Name ?? "";
            string group = grid != "" ? $"D{st.Name}-{grid}" : $"D{st.Name}-{bm.Id}";
            if (ImportGeometry.AddFrame(p, na, nb, FrameKind.Beam, sec, st.Name, $"D{bi++}", group) != null) rep.Beams++;
            if (Math.Abs(z1 - z2) > 0.05) rep.Warnings.Add($"Dầm {bm.Id} dốc (Δz = {Math.Abs(z1 - z2):0.00} m).");
        }

        // ---------------- SÀN ----------------
        var roofStory = stories[^1];
        var slabCount = new Dictionary<string, int>();
        foreach (var fl in ex.Floors)
        {
            if (fl.Outline == null || fl.Outline.Count < 3) { rep.Warnings.Add($"Sàn {fl.Id}: thiếu đường bao – bỏ qua."); continue; }
            double z = lvElev.TryGetValue(fl.Level ?? "", out double fz) ? SnapZ(fz) : NearestStory(0).Elevation;
            var st = NearestStory(z);
            if (Math.Abs(st.Elevation - z) > o.LevelTolerance) { rep.Warnings.Add($"Sàn {fl.Id} tại +{z:0.000} không trùng tầng nào – bỏ qua."); continue; }
            z = st.Elevation;
            double t = fl.Thickness > 0.04 ? ImportGeometry.RoundMm(fl.Thickness, 10) : 0.10;
            string sec = ImportGeometry.EnsureSlabSection(p, t, o.Concrete);
            var pts = fl.Outline.Where(a => a != null && a.Length >= 2).Select(a => (X: a[0] - ox, Y: a[1] - oy)).ToList();
            pts = ImportGeometry.RemoveCollinear(pts);
            if (pts.Count < 3) continue;
            if (ImportGeometry.SignedArea(pts) < 0) pts.Reverse();
            bool roof = st == roofStory;

            // các dầm của tầng này (mặt bằng)
            var beamSegs = p.Frames.Where(f => f.Kind == FrameKind.Beam && f.Story == st.Name)
                .Select(f => { var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ); return new Seg2(a.X, a.Y, b.X, b.Y); }).ToList();

            var cells = new List<List<(double X, double Y)>>();
            if (ImportGeometry.IsAxisRect(pts, out double x0, out double y0, out double x1, out double y1))
            {
                var rects = ImportGeometry.RectCells(beamSegs, x0, y0, x1, y1, false, rep.Warnings, $"Sàn {fl.Id}");
                foreach (var r in rects)
                    cells.Add(new List<(double X, double Y)> { (r.x0, r.y0), (r.x1, r.y0), (r.x1, r.y1), (r.x0, r.y1) });
            }
            else
            {
                cells.Add(pts);
                rep.Warnings.Add($"Sàn {fl.Id} không phải chữ nhật ({pts.Count} đỉnh) – giữ dạng đa giác, tải dồn đều theo chu vi.");
            }
            foreach (var cell in cells)
            {
                if (cell.Count == 4 && (cell.Max(q => q.X) - cell.Min(q => q.X) < 0.3 || cell.Max(q => q.Y) - cell.Min(q => q.Y) < 0.3)) continue;
                slabCount.TryGetValue(st.Name, out int nsl);
                slabCount[st.Name] = ++nsl;
                var slab = new Slab
                {
                    Id = p.NextSlabId(), Label = $"S{st.Name}-{nsl}", Story = st.Name, Section = sec,
                    Nodes = cell.Select(q => p.GetOrAddNode(q.X, q.Y, z, st.Name).Id).ToList()
                };
                p.Slabs.Add(slab); rep.Slabs++;
                ImportGeometry.AddSlabLoads(p, slab, roof, o.FinishLoad, o.LiveLoad, o.RoofLiveLoad);
            }
        }

        p.InvalidateIndex();
        DxfImporter.RemoveOrphanNodes(p);
        rep.SplitCount = ModelBuilder.SplitFramesAtNodes(p);
        ComboGenerator.Generate(p);
        p.InvalidateIndex();
        if (!p.Nodes.Any(n => n.IsSupport)) rep.Warnings.Add("Mô hình không có gối – kiểm tra cao độ chân cột.");
        rep.Nodes = p.Nodes.Count; rep.Frames = p.Frames.Count;
        return rep;
    }

    // ------------------------------------------------------------------
    private static bool IsVert(RvGrid g)
    {
        double dx = g.X2 - g.X1, dy = g.Y2 - g.Y1, L = Math.Sqrt(dx * dx + dy * dy);
        return L > 1e-6 && Math.Abs(dx) < 0.005 * L;
    }

    private static bool IsHorz(RvGrid g)
    {
        double dx = g.X2 - g.X1, dy = g.Y2 - g.Y1, L = Math.Sqrt(dx * dx + dy * dy);
        return L > 1e-6 && Math.Abs(dy) < 0.005 * L;
    }

    private static double LevelZ(Dictionary<string, double> lv, string name, List<RvLevel> levels)
    {
        if (!string.IsNullOrEmpty(name) && lv.TryGetValue(name, out double z)) return z;
        return levels.Count > 0 ? levels[0].Elevation : 0;
    }

    private static string SafeName(string name, int idx)
    {
        if (string.IsNullOrWhiteSpace(name)) return $"T{idx}";
        return name.Trim();
    }

    private static string ConcreteOf(string material, string def)
    {
        if (!string.IsNullOrEmpty(material))
            foreach (var c in Material.ConcreteClasses)
                if (material.ToUpperInvariant().Contains(c)) return c;
        return def;
    }

    private static (double, double) SnapToColumnTop(Project p, double x, double y, double z)
    {
        double best = double.MaxValue; (double, double) r = (x, y);
        foreach (var f in p.Frames.Where(f => f.Kind == FrameKind.Column))
        {
            var top = p.Node(f.NodeJ);
            if (Math.Abs(top.Z - z) > 1e-3) continue;
            var s = p.FrameSections.FirstOrDefault(q => q.Name == f.Section);
            double half = s != null ? Math.Max(s.B, s.H) / 2 + 0.05 : 0.2;
            double d = Math.Sqrt((top.X - x) * (top.X - x) + (top.Y - y) * (top.Y - y));
            if (d <= half && d < best) { best = d; r = (top.X, top.Y); }
        }
        return r;
    }
}
