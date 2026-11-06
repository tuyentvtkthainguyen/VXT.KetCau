namespace KetCauVXT.Model;

// =====================================================================
// CAO ĐỘ CÔNG TRÌNH TỪ MÓNG ĐẾN MÁI
// Đáy móng (−) → Mặt móng / chân cột (BaseElevation) → Nền tầng 1 (±0.000, sàn nền trên đất) →
// Sàn tầng 2, Sàn tầng 3, … → Sàn mái → (Sàn tum, Mái tum).
// Project.Stories vẫn là các sàn do khung đỡ (phân tích). Đáy móng, mặt móng, nền tầng 1 là cao độ "ảo"
// lấy từ Project.Soil.Depth, Project.BaseElevation, Project.GroundElevation; sàn nền (Arch.GroundSlabs) nằm trên đất,
// KHÔNG đưa vào phân tích khung (không tải lên dầm) – chỉ dùng cho khối lượng, bản vẽ, IFC.
// =====================================================================

/// <summary>Loại cao độ.</summary>
public enum LevelKind
{
    /// <summary>Đáy móng</summary>
    DayMong,
    /// <summary>Mặt móng / chân cột</summary>
    MatMong,
    /// <summary>Nền tầng 1 (sàn trên đất)</summary>
    Nen,
    /// <summary>Sàn tầng (do dầm cột đỡ)</summary>
    San,
    /// <summary>Sàn mái</summary>
    Mai,
    /// <summary>Sàn tum / mái tum</summary>
    Tum
}

/// <summary>Một cao độ trong danh sách cao độ công trình.</summary>
public sealed class LevelInfo
{
    public string Name { get; init; } = "";
    /// <summary>Tên hiển thị: "Nền tầng 1", "Sàn tầng 2", "Sàn mái"…</summary>
    public string Title { get; init; } = "";
    public double Elevation { get; init; }
    public LevelKind Kind { get; init; }
    /// <summary>Tầng phân tích tương ứng (null với đáy móng, mặt móng, nền)</summary>
    public Story Story { get; init; }
    public override string ToString() => $"{Title} ({(Elevation >= 0 ? "+" : "")}{Elevation:0.000})";
}

/// <summary>Thông số trình tạo cao độ tầng.</summary>
public class LevelWizardOptions
{
    /// <summary>Số tầng (kể cả tầng 1). Vd 3 → nền T1, sàn T2, sàn T3, sàn mái.</summary>
    public int Floors { get; set; } = 3;
    /// <summary>Chiều cao tầng 1 (nền → sàn tầng 2), m</summary>
    public double FirstFloorHeight { get; set; } = 3.6;
    /// <summary>Chiều cao các tầng trên, m</summary>
    public double FloorHeight { get; set; } = 3.3;
    /// <summary>Cao độ nền tầng 1 (±0.000), m</summary>
    public double GroundElevation { get; set; }
    /// <summary>Độ sâu chôn móng (đáy móng dưới nền tầng 1), m</summary>
    public double FoundationDepth { get; set; } = 1.5;
    /// <summary>Cao độ mặt móng / chân cột dưới nền tầng 1, m (thường 0,7…1,0)</summary>
    public double FootingTopDepth { get; set; } = 0.9;
    /// <summary>Có tầng tum</summary>
    public bool Tum { get; set; }
    public double TumHeight { get; set; } = 3.0;
    /// <summary>Kiểu mái: "Mái bằng BTCT" | "Mái tôn" | "Mái ngói"</summary>
    public string RoofType { get; set; } = "Mái bằng BTCT";
    /// <summary>Sinh khung (cột, dầm, sàn) theo lưới trục nếu đã có trục và chưa có cấu kiện</summary>
    public bool BuildFrame { get; set; } = true;
    /// <summary>Tạo sàn nền tầng 1 theo các ô lưới</summary>
    public bool GroundSlab { get; set; } = true;
    public double GroundSlabThickness { get; set; } = 0.10;
}

/// <summary>Cao độ từ móng đến mái, trình tạo cao độ, chèn / xoá / đổi tên tầng.</summary>
public static class Levels
{
    /// <summary>Loại của một tầng phân tích (Story.Kind nếu có; không thì suy: tầng cao nhất = mái, tên TUM… = tum).</summary>
    public static LevelKind KindOf(Project p, Story s)
    {
        if (!string.IsNullOrEmpty(s.Kind))
        {
            if (s.Kind.Equals("Mai", StringComparison.OrdinalIgnoreCase)) return LevelKind.Mai;
            if (s.Kind.Equals("Tum", StringComparison.OrdinalIgnoreCase)) return LevelKind.Tum;
            return LevelKind.San;
        }
        string n = (s.Name ?? "").ToUpperInvariant();
        if (n.StartsWith("TUM") || n.Contains("MAI TUM") || n.Contains("MÁI TUM")) return LevelKind.Tum;
        var top = p.Stories.OrderBy(x => x.Elevation).LastOrDefault();
        if (n.StartsWith("MAI") || n.StartsWith("MÁI") || n == "ROOF") return LevelKind.Mai;
        if (top == s && p.Stories.Count > 1 && !p.Stories.Any(x => (x.Name ?? "").ToUpperInvariant().StartsWith("MAI"))) return LevelKind.Mai;
        return LevelKind.San;
    }

    /// <summary>Tên hiển thị của tầng phân tích: "Sàn tầng k", "Sàn mái", "Sàn tum".</summary>
    public static string TitleOf(Project p, Story s)
    {
        var kind = KindOf(p, s);
        if (kind == LevelKind.Mai) return "Sàn mái";
        if (kind == LevelKind.Tum) return "Sàn tum";
        var sans = p.Stories.Where(x => KindOf(p, x) == LevelKind.San).OrderBy(x => x.Elevation).ToList();
        int idx = sans.IndexOf(s);
        return idx >= 0 ? $"Sàn tầng {idx + 2}" : s.Name;
    }

    /// <summary>Danh sách cao độ từ đáy móng tới mái (thấp → cao).</summary>
    public static List<LevelInfo> All(Project p)
    {
        var list = new List<LevelInfo>();
        double ground = p.GroundElevation;
        double depth = p.Soil?.Depth > 0 ? p.Soil.Depth : 1.5;
        list.Add(new LevelInfo { Name = "DAY_MONG", Title = "Đáy móng", Elevation = Math.Round(ground - depth, 4), Kind = LevelKind.DayMong });
        if (p.BaseElevation < ground - 1e-6 && p.BaseElevation > ground - depth + 1e-6)
            list.Add(new LevelInfo { Name = "MAT_MONG", Title = "Mặt móng", Elevation = p.BaseElevation, Kind = LevelKind.MatMong });
        list.Add(new LevelInfo { Name = "NEN", Title = "Nền tầng 1", Elevation = ground, Kind = LevelKind.Nen });
        foreach (var s in p.Stories.OrderBy(x => x.Elevation))
            list.Add(new LevelInfo { Name = s.Name, Title = TitleOf(p, s), Elevation = s.Elevation, Kind = KindOf(p, s), Story = s });
        return list;
    }

    /// <summary>Tạo cao độ tầng: thay toàn bộ Stories (nếu chưa có cấu kiện), đặt BaseElevation, GroundElevation, độ sâu móng,
    /// sinh khung theo lưới (nếu có trục) và sàn nền tầng 1. Trả về thông báo.</summary>
    public static string ApplyWizard(Project p, LevelWizardOptions o)
    {
        if (o.Floors < 1) o.Floors = 1;
        bool hasMembers = p.Frames.Count > 0;
        if (hasMembers)
            return "Mô hình đã có cấu kiện: hãy dùng Chèn / Xoá tầng để sửa cao độ (trình tạo chỉ dùng cho mô hình mới).";
        p.GroundElevation = o.GroundElevation;
        p.BaseElevation = Math.Round(o.GroundElevation - Math.Max(0, o.FootingTopDepth), 4);
        if (p.Soil != null) p.Soil.Depth = Math.Max(0.5, o.FoundationDepth);
        p.Stories.Clear();
        double z = o.GroundElevation;
        // cột tầng 1 đi từ mặt móng → sàn tầng 2 (chiều cao phân tích = tầng 1 + chôn móng)
        for (int i = 1; i <= o.Floors; i++)
        {
            double h = i == 1 ? o.FirstFloorHeight : o.FloorHeight;
            z += h;
            bool roof = i == o.Floors;
            string name = roof ? "MAI" : $"T{i + 1}";
            double hAnalysis = i == 1 ? z - p.BaseElevation : h;
            p.Stories.Add(new Story { Name = name, Elevation = Math.Round(z, 4), Height = Math.Round(hAnalysis, 4), Kind = roof ? "Mai" : "San" });
        }
        if (o.Tum)
        {
            z += o.TumHeight;
            p.Stories.Add(new Story { Name = "TUM", Elevation = Math.Round(z, 4), Height = o.TumHeight, Kind = "Tum" });
        }
        var msg = new List<string> { $"Đã tạo {p.Stories.Count} cao độ sàn (nền ±{o.GroundElevation:0.000}, mặt móng {p.BaseElevation:0.000}, đáy móng {o.GroundElevation - o.FoundationDepth:0.000})" };
        if (o.BuildFrame && p.GridsX.Any() && p.GridsY.Any())
        {
            var go = new ModelBuilder.GridFrameOptions();
            if (!o.RoofType.Contains("BTCT", StringComparison.OrdinalIgnoreCase)) go.RoofLiveLoad = 0.3;
            ModelBuilder.BuildFromGrid(p, go);
            msg.Add($"sinh khung theo lưới: {p.Frames.Count} thanh, {p.Slabs.Count} ô sàn");
        }
        if (o.GroundSlab && p.GridsX.Any() && p.GridsY.Any())
        {
            int n = GroundSlabsFromGrid(p, o.GroundSlabThickness);
            msg.Add($"{n} ô sàn nền tầng 1 (trên đất, không tải lên khung)");
        }
        p.InvalidateIndex();
        return string.Join("; ", msg);
    }

    /// <summary>Tạo sàn nền tầng 1 theo các ô lưới (thay sàn nền cũ). Trả số ô.</summary>
    public static int GroundSlabsFromGrid(Project p, double thickness = 0.10)
    {
        var xs = p.GridsX.Select(g => g.Coord).ToList();
        var ys = p.GridsY.Select(g => g.Coord).ToList();
        p.Arch ??= new ArchModel();
        p.Arch.GroundSlabs.Clear();
        int k = 1;
        for (int i = 0; i + 1 < xs.Count; i++)
            for (int j = 0; j + 1 < ys.Count; j++)
            {
                p.Arch.GroundSlabs.Add(new GroundSlab
                {
                    Id = k, Label = $"SN{k}", Thickness = thickness, Elevation = p.GroundElevation,
                    Outline = new List<double[]> { new[] { xs[i], ys[j] }, new[] { xs[i + 1], ys[j] }, new[] { xs[i + 1], ys[j + 1] }, new[] { xs[i], ys[j + 1] } }
                });
                k++;
            }
        return p.Arch.GroundSlabs.Count;
    }

    /// <summary>Tạo các ô sàn còn thiếu theo lưới cho mọi tầng (ô lưới có đủ dầm bao). Trả số ô thêm.</summary>
    public static int SlabsForAllBays(Project p, string story = null)
    {
        int added = 0;
        var xs = p.GridsX.Select(g => g.Coord).ToList();
        var ys = p.GridsY.Select(g => g.Coord).ToList();
        foreach (var st in p.Stories.Where(s => story == null || s.Name == story))
            for (int i = 0; i + 1 < xs.Count; i++)
                for (int j = 0; j + 1 < ys.Count; j++)
                {
                    double cx = (xs[i] + xs[i + 1]) / 2, cy = (ys[j] + ys[j + 1]) / 2;
                    if (ModelEdit.SlabAt(p, st.Name, cx, cy) != null) continue;
                    if (p.Openings.Any(o => o.Story == st.Name && o.Contains(cx, cy))) continue;
                    var r = ModelEdit.AddSlab(p, st.Name, cx, cy, KindOf(p, st) == LevelKind.San ? SlabUse.Normal : SlabUse.Roof);
                    if (r.Ok) added++;
                }
        return added;
    }

    /// <summary>Mọi tham chiếu tên tầng trong mô hình (để đổi tên).</summary>
    public static void RenameStory(Project p, string oldName, string newName)
    {
        if (oldName == newName || string.IsNullOrEmpty(newName)) return;
        foreach (var s in p.Stories.Where(s => s.Name == oldName)) s.Name = newName;
        foreach (var n in p.Nodes.Where(x => x.Story == oldName)) n.Story = newName;
        foreach (var f in p.Frames.Where(x => x.Story == oldName)) f.Story = newName;
        foreach (var s in p.Slabs.Where(x => x.Story == oldName)) s.Story = newName;
        foreach (var w in p.Walls.Where(x => x.Story == oldName)) w.Story = newName;
        foreach (var o in p.Openings.Where(x => x.Story == oldName)) o.Story = newName;
        foreach (var s in p.Stairs.Where(x => x.Story == oldName)) s.Story = newName;
        if (p.Arch != null)
        {
            foreach (var r in p.Arch.Rooms.Where(x => x.Story == oldName)) r.Story = newName;
            foreach (var c in p.Arch.Ceilings.Where(x => x.Story == oldName)) c.Story = newName;
            foreach (var r in p.Arch.Roofs.Where(x => x.Story == oldName)) r.Story = newName;
            foreach (var f in p.Arch.Furniture.Where(x => x.Story == oldName)) f.Story = newName;
        }
    }

    /// <summary>Đặt lại tên tự động: sàn tầng → T2, T3…; mái → MAI; tum → TUM (dùng tên tạm để tránh trùng).</summary>
    public static void AutoRename(Project p)
    {
        var ordered = p.Stories.OrderBy(s => s.Elevation).ToList();
        var kinds = ordered.ToDictionary(s => s, s => KindOf(p, s));
        foreach (var s in ordered) s.Kind ??= kinds[s] switch { LevelKind.Mai => "Mai", LevelKind.Tum => "Tum", _ => "San" };
        var temp = new Dictionary<Story, string>();
        int i = 0;
        foreach (var s in ordered) { string t = $"__TMP{i++}"; RenameStory(p, s.Name, t); temp[s] = t; }
        int k = 2, tum = 0, mai = 0;
        foreach (var s in ordered)
        {
            string name = kinds[s] switch
            {
                LevelKind.Mai => mai++ == 0 ? "MAI" : $"MAI{mai}",
                LevelKind.Tum => tum++ == 0 ? "TUM" : $"TUM{tum}",
                _ => $"T{k++}"
            };
            RenameStory(p, temp[s], name);
        }
    }

    /// <summary>Chèn một tầng sàn cao h (m) ngay trên tầng "below" (null = ngay trên nền tầng 1): mọi tầng phía trên
    /// và nút của chúng nâng lên h; cột của tầng mới nối thêm. Đơn giản: tầng mới sao chép dầm / sàn / cột của tầng ngay dưới
    /// (nếu có), sau đó đặt lại tên tự động. Trả tên tầng mới.</summary>
    public static string InsertStory(Project p, string below, double h, bool copyMembers = true)
    {
        if (h <= 0.5) throw new ArgumentException("Chiều cao tầng chèn phải > 0,5 m");
        var ordered = p.Stories.OrderBy(s => s.Elevation).ToList();
        double zBelow = below == null ? p.GroundElevation : ordered.First(s => s.Name == below).Elevation;
        // nâng mọi thứ phía trên
        foreach (var s in ordered.Where(s => s.Elevation > zBelow + 1e-6)) s.Elevation = Math.Round(s.Elevation + h, 4);
        foreach (var n in p.Nodes.Where(n => n.Z > zBelow + 1e-6)) n.Z = Math.Round(n.Z + h, 4);
        var ns = new Story { Name = "__NEW", Elevation = Math.Round(zBelow + h, 4), Height = h, Kind = "San" };
        p.Stories.Add(ns);
        var src = below == null ? null : p.Stories.First(s => s.Name == below);
        if (copyMembers && src != null)
        {
            var map = new Dictionary<int, int>();
            int Map(int id)
            {
                if (map.TryGetValue(id, out int m)) return m;
                var a = p.Node(id);
                var b = p.GetOrAddNode(a.X, a.Y, ns.Elevation, ns.Name);
                map[id] = b.Id; return b.Id;
            }
            foreach (var f in p.Frames.Where(f => f.Story == src.Name && f.Kind == FrameKind.Beam).ToList())
            {
                p.Frames.Add(new FrameElement { Id = p.NextFrameId(), NodeI = Map(f.NodeI), NodeJ = Map(f.NodeJ), Kind = FrameKind.Beam, Section = f.Section, Story = ns.Name, Label = f.Label, DesignGroup = f.DesignGroup });
            }
            foreach (var s in p.Slabs.Where(s => s.Story == src.Name).ToList())
                p.Slabs.Add(new Slab { Id = p.NextSlabId(), Label = s.Label, Story = ns.Name, Section = s.Section, BuildUp = s.BuildUp, Nodes = s.Nodes.Select(Map).ToList() });
            // cột: cột của tầng phía trên (đi từ src → tầng trên) cắt tại cao độ mới
            foreach (var c in p.Frames.Where(f => f.Kind == FrameKind.Column).ToList())
            {
                var a = p.Node(c.NodeI); var b = p.Node(c.NodeJ);
                double z0 = Math.Min(a.Z, b.Z), z1 = Math.Max(a.Z, b.Z);
                if (z0 < ns.Elevation - 1e-6 && z1 > ns.Elevation + 1e-6 && Math.Abs(z0 - src.Elevation) < 1e-6)
                {
                    var mid = p.GetOrAddNode(a.X, a.Y, ns.Elevation, ns.Name);
                    int top = a.Z > b.Z ? c.NodeI : c.NodeJ;
                    int bot = a.Z > b.Z ? c.NodeJ : c.NodeI;
                    c.NodeI = bot; c.NodeJ = mid.Id; c.Story = ns.Name;
                    p.Frames.Add(new FrameElement { Id = p.NextFrameId(), NodeI = mid.Id, NodeJ = top, Kind = FrameKind.Column, Section = c.Section, Story = StoryAt(p, p.Node(top).Z), Label = c.Label, DesignGroup = c.DesignGroup });
                }
            }
        }
        // chiều cao các tầng
        var all = p.Stories.OrderBy(s => s.Elevation).ToList();
        for (int i = 0; i < all.Count; i++)
            all[i].Height = Math.Round(all[i].Elevation - (i == 0 ? p.BaseElevation : all[i - 1].Elevation), 4);
        p.InvalidateIndex();
        AutoRename(p);
        return p.Stories.OrderBy(s => s.Elevation).First(s => Math.Abs(s.Elevation - ns.Elevation) < 1e-6).Name;
    }

    private static string StoryAt(Project p, double z) =>
        p.Stories.OrderBy(s => Math.Abs(s.Elevation - z)).FirstOrDefault()?.Name ?? "";

    /// <summary>Xoá một tầng: xoá dầm, sàn, tường, lỗ sàn của tầng; cột tầng đó nối với cột tầng trên; hạ các tầng phía trên.</summary>
    public static void DeleteStory(Project p, string name)
    {
        var st = p.Stories.FirstOrDefault(s => s.Name == name);
        if (st == null) return;
        var ordered = p.Stories.OrderBy(s => s.Elevation).ToList();
        int idx = ordered.IndexOf(st);
        double zPrev = idx == 0 ? p.BaseElevation : ordered[idx - 1].Elevation;
        double h = st.Elevation - zPrev;
        var slabIds = p.Slabs.Where(s => s.Story == name).Select(s => s.Id).ToHashSet();
        p.Slabs.RemoveAll(s => s.Story == name);
        p.AreaLoads.RemoveAll(a => slabIds.Contains(a.Slab));
        var beamIds = p.Frames.Where(f => f.Story == name && f.Kind != FrameKind.Column).Select(f => f.Id).ToHashSet();
        p.Frames.RemoveAll(f => beamIds.Contains(f.Id));
        p.FrameLoads.RemoveAll(l => beamIds.Contains(l.Element));
        p.Walls.RemoveAll(w => w.Story == name);
        p.Openings.RemoveAll(o => o.Story == name);
        // nối cột: cột tầng name (bot→mid) + cột tầng trên (mid→top) → bot→top
        foreach (var c in p.Frames.Where(f => f.Kind == FrameKind.Column && f.Story == name).ToList())
        {
            var a = p.Node(c.NodeI); var b = p.Node(c.NodeJ);
            int mid = a.Z > b.Z ? c.NodeI : c.NodeJ, bot = a.Z > b.Z ? c.NodeJ : c.NodeI;
            var up = p.Frames.FirstOrDefault(f => f.Kind == FrameKind.Column && f != c && (f.NodeI == mid || f.NodeJ == mid));
            if (up != null)
            {
                if (up.NodeI == mid) up.NodeI = bot; else up.NodeJ = bot;
                p.Frames.Remove(c);
            }
            else p.Frames.Remove(c);
        }
        p.Stories.Remove(st);
        var usedNodes = p.Frames.SelectMany(f => new[] { f.NodeI, f.NodeJ }).Concat(p.Slabs.SelectMany(s => s.Nodes)).ToHashSet();
        p.Nodes.RemoveAll(n => Math.Abs(n.Z - st.Elevation) < 1e-6 && !usedNodes.Contains(n.Id));
        foreach (var s in p.Stories.Where(s => s.Elevation > st.Elevation)) s.Elevation = Math.Round(s.Elevation - h, 4);
        foreach (var n in p.Nodes.Where(n => n.Z > st.Elevation + 1e-6)) n.Z = Math.Round(n.Z - h, 4);
        var all = p.Stories.OrderBy(s => s.Elevation).ToList();
        for (int i = 0; i < all.Count; i++)
            all[i].Height = Math.Round(all[i].Elevation - (i == 0 ? p.BaseElevation : all[i - 1].Elevation), 4);
        p.InvalidateIndex();
        AutoRename(p);
    }
}
