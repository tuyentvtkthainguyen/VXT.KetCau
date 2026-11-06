using System.Globalization;
using KetCauVXT.Mep;

namespace KetCauVXT.Model;

// =====================================================================
// QUÉT CHỌN NHIỀU LOẠI ĐỐI TƯỢNG + SỬA LƯỚI TRỤC + THÊM / KÉO CAO ĐỘ TẦNG
//   • MixedSelection: khung Window (trái → phải: nằm trọn) / Crossing (phải → trái: chạm) cho cột, dầm, sàn, tường,
//     cửa, đồ đạc, thiết bị và tuyến điện nước, trục, đường mặt cắt; bộ lọc theo loại; đếm "5 dầm, 3 cột"; xoá.
//   • GridOps: thêm trục (tên tự động 1, 2… / A…Z, AA…), sao chép lệch, dời trục kéo theo cấu kiện, đổi tên, xoá.
//   • LevelOps: thêm tầng trên / dưới / chèn giữa (không sao chép / chỉ cột / toàn bộ), kéo đường cao độ (nút dời theo),
//     xoá, đổi tên (dùng lại Levels.InsertStory / DeleteStory / RenameStory).
// Toạ độ m. Lõi không phụ thuộc giao diện (App: MainWindow.SelectGridLevel.cs).
// =====================================================================

/// <summary>Loại đối tượng chọn được trên mặt bằng.</summary>
public enum SelCat { Cot, Dam, San, Tuong, Cua, DoDac, ThietBi, TuyenOng, Truc, MatCat }

/// <summary>Một đối tượng đã chọn. Id: Id của đối tượng; với cửa: Id cửa (WallOpening.Id) và Host = Id tường;
/// trục: chỉ số trong Project.Grids; đường mặt cắt: chỉ số trong Arch.Sections.</summary>
public readonly record struct PlanSelItem(SelCat Cat, int Id, int Host = 0);

public enum SelMode { Replace, Add, Remove }

public static class MixedSelection
{
    public static readonly SelCat[] AllCats = (SelCat[])Enum.GetValues(typeof(SelCat));

    public static string Title(SelCat c) => c switch
    {
        SelCat.Cot => "cột", SelCat.Dam => "dầm", SelCat.San => "ô sàn", SelCat.Tuong => "tường", SelCat.Cua => "cửa",
        SelCat.DoDac => "đồ đạc", SelCat.ThietBi => "thiết bị điện nước", SelCat.TuyenOng => "tuyến ống / dây",
        SelCat.Truc => "trục", SelCat.MatCat => "đường mặt cắt", _ => c.ToString()
    };

    private static bool OnStory(string objStory, string story) => string.IsNullOrEmpty(story) || objStory == story;

    /// <summary>Đầu mút trục (để chọn bằng khung): trục X = đường đứng x = Coord kéo hết phạm vi trục Y (± 1,2 m), trục Y tương tự.</summary>
    public static (double X1, double Y1, double X2, double Y2) GridSegment(Project p, GridLine g)
    {
        var other = (g.Dir == GridDir.X ? p.GridsY : p.GridsX).Select(q => q.Coord).ToList();
        double lo = other.Count > 0 ? other.Min() - 1.2 : -1.2, hi = other.Count > 0 ? other.Max() + 1.2 : 1.2;
        return g.Dir == GridDir.X ? (g.Coord, lo, g.Coord, hi) : (lo, g.Coord, hi, g.Coord);
    }

    /// <summary>Đa giác chiếm chỗ của đồ đạc (khung chữ nhật xoay).</summary>
    public static List<(double X, double Y)> FurniturePolygon(FurnitureItem f)
    {
        double a = f.Rotation * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a), hw = f.Width / 2, hd = f.Depth / 2;
        var res = new List<(double X, double Y)>();
        foreach (var (lx, ly) in new[] { (-hw, -hd), (hw, -hd), (hw, hd), (-hw, hd) })
            res.Add((f.X + lx * c - ly * s, f.Y + lx * s + ly * c));
        return res;
    }

    /// <summary>Quét chọn bằng khung trên tầng story (null = mọi tầng). cats = các loại được chọn (null = tất cả).</summary>
    public static List<PlanSelItem> Select(Project p, string story, SelectionRules.Box r, bool crossing, ISet<SelCat> cats = null)
    {
        bool Want(SelCat c) => cats == null || cats.Contains(c);
        var res = new List<PlanSelItem>();
        if (p == null) return res;
        if (Want(SelCat.Cot)) res.AddRange(SelectionRules.Columns(p, story, r, crossing).Select(id => new PlanSelItem(SelCat.Cot, id)));
        if (Want(SelCat.Dam)) res.AddRange(SelectionRules.Beams(p, story, r, crossing).Select(id => new PlanSelItem(SelCat.Dam, id)));
        if (Want(SelCat.San)) res.AddRange(SelectionRules.Slabs(p, story, r, crossing).Select(id => new PlanSelItem(SelCat.San, id)));
        foreach (var w in p.Walls.Where(w => OnStory(w.Story, story)))
        {
            if (Want(SelCat.Tuong) && SelectionRules.Polyline(r, new[] { (w.X1, w.Y1), (w.X2, w.Y2) }, crossing)) res.Add(new PlanSelItem(SelCat.Tuong, w.Id));
            if (!Want(SelCat.Cua)) continue;
            foreach (var o in w.Openings ?? new List<WallOpening>())
            {
                var (a, b) = WallAssembly.Span(p, o);
                var pa = WallAssembly.PointAt(w, Math.Max(0, a)); var pb = WallAssembly.PointAt(w, Math.Max(0, b));
                if (SelectionRules.Polyline(r, new[] { pa, pb }, crossing)) res.Add(new PlanSelItem(SelCat.Cua, o.Id, w.Id));
            }
        }
        if (Want(SelCat.DoDac))
            foreach (var f in p.Arch?.Furniture ?? new List<FurnitureItem>())
                if (OnStory(f.Story, story) && SelectionRules.Polygon(r, FurniturePolygon(f), crossing)) res.Add(new PlanSelItem(SelCat.DoDac, f.Id));
        if (Want(SelCat.ThietBi))
            foreach (var d in p.Mep?.Devices ?? new List<MepDevice>())
                if (OnStory(d.Story, story) && r.Contains(d.X, d.Y)) res.Add(new PlanSelItem(SelCat.ThietBi, d.Id));
        if (Want(SelCat.TuyenOng))
            foreach (var run in p.Mep?.Runs ?? new List<MepRun>())
            {
                if (!OnStory(run.Story, story) || run.Path == null || run.Path.Count == 0) continue;
                if (SelectionRules.Polyline(r, run.Path.Select(q => (q.X, q.Y)).ToList(), crossing)) res.Add(new PlanSelItem(SelCat.TuyenOng, run.Id));
            }
        if (Want(SelCat.Truc))
            for (int i = 0; i < p.Grids.Count; i++)
            {
                var (x1, y1, x2, y2) = GridSegment(p, p.Grids[i]);
                if (SelectionRules.Polyline(r, new[] { (x1, y1), (x2, y2) }, crossing)) res.Add(new PlanSelItem(SelCat.Truc, i));
            }
        if (Want(SelCat.MatCat))
        {
            var secs = p.Arch?.Sections ?? new List<SectionLine>();
            for (int i = 0; i < secs.Count; i++)
                if (SelectionRules.Polyline(r, new[] { (secs[i].X1, secs[i].Y1), (secs[i].X2, secs[i].Y2) }, crossing)) res.Add(new PlanSelItem(SelCat.MatCat, i));
        }
        return res;
    }

    /// <summary>Gộp kết quả quét vào lựa chọn hiện có: Replace (bấm kéo thường), Add (Ctrl + kéo), Remove (Shift + kéo).</summary>
    public static List<PlanSelItem> Combine(IEnumerable<PlanSelItem> current, IEnumerable<PlanSelItem> picked, SelMode mode)
    {
        var cur = (current ?? Enumerable.Empty<PlanSelItem>()).ToList();
        var pk = (picked ?? Enumerable.Empty<PlanSelItem>()).Distinct().ToList();
        return mode switch
        {
            SelMode.Add => cur.Concat(pk).Distinct().ToList(),
            SelMode.Remove => cur.Where(c => !pk.Contains(c)).ToList(),
            _ => pk
        };
    }

    /// <summary>Bộ lọc (kiểu Revit): chỉ giữ các loại được tích.</summary>
    public static List<PlanSelItem> Filter(IEnumerable<PlanSelItem> items, ISet<SelCat> keep) =>
        (items ?? Enumerable.Empty<PlanSelItem>()).Where(i => keep == null || keep.Contains(i.Cat)).ToList();

    public static Dictionary<SelCat, int> Counts(IEnumerable<PlanSelItem> items) =>
        (items ?? Enumerable.Empty<PlanSelItem>()).GroupBy(i => i.Cat).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>"5 dầm, 3 cột, 2 tường" (theo thứ tự loại); rỗng → "không có đối tượng".</summary>
    public static string Summary(IEnumerable<PlanSelItem> items)
    {
        var c = Counts(items);
        if (c.Count == 0) return "không có đối tượng";
        return string.Join(", ", AllCats.Where(c.ContainsKey).Select(k => $"{c[k]} {Title(k)}"));
    }

    /// <summary>Xoá các đối tượng đã chọn (một lần). Trả số đối tượng đã xoá. Trục / mặt cắt xoá theo chỉ số giảm dần.</summary>
    public static int Delete(Project p, IEnumerable<PlanSelItem> items)
    {
        var list = (items ?? Enumerable.Empty<PlanSelItem>()).Distinct().ToList();
        int n = 0;
        var frameIds = list.Where(i => i.Cat is SelCat.Cot or SelCat.Dam).Select(i => i.Id).ToHashSet();
        if (frameIds.Count > 0)
        {
            n += p.Frames.RemoveAll(f => frameIds.Contains(f.Id));
            p.FrameLoads.RemoveAll(l => frameIds.Contains(l.Element));
        }
        var slabIds = list.Where(i => i.Cat == SelCat.San).Select(i => i.Id).ToHashSet();
        if (slabIds.Count > 0) { n += p.Slabs.RemoveAll(s => slabIds.Contains(s.Id)); p.AreaLoads.RemoveAll(a => slabIds.Contains(a.Slab)); }
        foreach (var it in list.Where(i => i.Cat == SelCat.Cua))
        {
            var w = p.Walls.FirstOrDefault(x => x.Id == it.Host);
            if (w?.Openings != null) n += w.Openings.RemoveAll(o => o.Id == it.Id);
        }
        var wallIds = list.Where(i => i.Cat == SelCat.Tuong).Select(i => i.Id).ToHashSet();
        if (wallIds.Count > 0) n += p.Walls.RemoveAll(w => wallIds.Contains(w.Id));
        var furn = list.Where(i => i.Cat == SelCat.DoDac).Select(i => i.Id).ToHashSet();
        if (furn.Count > 0 && p.Arch != null) n += p.Arch.Furniture.RemoveAll(f => furn.Contains(f.Id));
        var dev = list.Where(i => i.Cat == SelCat.ThietBi).Select(i => i.Id).ToHashSet();
        var runs = list.Where(i => i.Cat == SelCat.TuyenOng).Select(i => i.Id).ToHashSet();
        if (p.Mep != null)
        {
            if (dev.Count > 0)
            {
                n += p.Mep.Devices.RemoveAll(d => dev.Contains(d.Id));
                p.Mep.Runs.RemoveAll(r => dev.Contains(r.FromId) || dev.Contains(r.ToId));   // tuyến treo không còn thiết bị
            }
            if (runs.Count > 0) n += p.Mep.Runs.RemoveAll(r => runs.Contains(r.Id));
        }
        foreach (int i in list.Where(i => i.Cat == SelCat.MatCat).Select(i => i.Id).Distinct().OrderByDescending(i => i))
            if (p.Arch != null && i >= 0 && i < p.Arch.Sections.Count) { p.Arch.Sections.RemoveAt(i); n++; }
        foreach (int i in list.Where(i => i.Cat == SelCat.Truc).Select(i => i.Id).Distinct().OrderByDescending(i => i))
            if (i >= 0 && i < p.Grids.Count) { p.Grids.RemoveAt(i); n++; }
        if (n > 0) p.InvalidateIndex();
        return n;
    }
}

/// <summary>Sửa lưới trục: thêm, sao chép lệch, dời (kéo theo cấu kiện), đổi tên, xoá.</summary>
public static class GridOps
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public const double Tol = 0.005;

    /// <summary>Chỉ số chữ cái: A → 0, Z → 25, AA → 26… (−1 nếu không phải chữ).</summary>
    public static int LetterIndex(string name)
    {
        if (string.IsNullOrEmpty(name) || !name.All(char.IsLetter)) return -1;
        int v = 0;
        foreach (char ch in name.ToUpperInvariant()) v = v * 26 + (ch - 'A' + 1);
        return v - 1;
    }

    public static string LetterName(int index)
    {
        string s = ""; int i = index + 1;
        while (i > 0) { int r = (i - 1) % 26; s = (char)('A' + r) + s; i = (i - 1) / 26; }
        return s;
    }

    /// <summary>Tên trục kế tiếp: trục X → số lớn nhất + 1; trục Y → chữ sau chữ lớn nhất (Z → AA).</summary>
    public static string NextName(Project p, GridDir dir)
    {
        var names = p.Grids.Where(g => g.Dir == dir).Select(g => g.Name ?? "").ToList();
        if (dir == GridDir.X)
        {
            int mx = 0;
            foreach (var n in names) if (int.TryParse(n, NumberStyles.Integer, Inv, out int k)) mx = Math.Max(mx, k);
            string s = (mx + 1).ToString(Inv);
            while (p.Grids.Any(g => g.Name == s)) s += "'";
            return s;
        }
        int li = names.Select(LetterIndex).DefaultIfEmpty(-1).Max();
        string nm = LetterName(li + 1);
        while (p.Grids.Any(g => g.Name == nm)) nm += "'";
        return nm;
    }

    /// <summary>Trục từ 2 điểm vẽ: phương gần ngang → trục Y (y = trung bình), gần đứng → trục X. null nếu quá ngắn.</summary>
    public static (GridDir Dir, double Coord)? FromTwoPoints(double x1, double y1, double x2, double y2)
    {
        double dx = Math.Abs(x2 - x1), dy = Math.Abs(y2 - y1);
        if (dx + dy < 0.05) return null;
        return dy >= dx ? (GridDir.X, Math.Round((x1 + x2) / 2, 4)) : (GridDir.Y, Math.Round((y1 + y2) / 2, 4));
    }

    /// <summary>Thêm trục (đã có trục trùng toạ độ → trả trục đó). name null/rỗng → tên tự động.</summary>
    public static GridLine AddGrid(Project p, GridDir dir, double coord, string name = null)
    {
        var same = p.Grids.FirstOrDefault(g => g.Dir == dir && Math.Abs(g.Coord - coord) < Tol);
        if (same != null) return same;
        if (string.IsNullOrWhiteSpace(name) || p.Grids.Any(g => g.Name == name)) name = NextName(p, dir);
        var gl = new GridLine { Name = name, Dir = dir, Coord = Math.Round(coord, 4) };
        p.Grids.Add(gl);
        return gl;
    }

    /// <summary>Sao chép trục lệch một khoảng (m, có dấu).</summary>
    public static GridLine OffsetCopy(Project p, GridLine g, double distance) => AddGrid(p, g.Dir, g.Coord + distance);

    public static GridLine Find(Project p, string name) => p.Grids.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));

    private static bool OnGrid(GridLine g, double x, double y) => Math.Abs((g.Dir == GridDir.X ? x : y) - g.Coord) < Tol;

    /// <summary>Số nút và tường gắn với trục (nằm trên đường trục).</summary>
    public static (int Nodes, int Walls) Attached(Project p, GridLine g) =>
        (p.Nodes.Count(n => OnGrid(g, n.X, n.Y)), p.Walls.Count(w => OnGrid(g, w.X1, w.Y1) || OnGrid(g, w.X2, w.Y2)));

    /// <summary>Dời trục tới toạ độ mới. drag = kéo theo cấu kiện: mọi nút (cột, đầu dầm, góc sàn – mọi tầng) và đầu tường,
    /// đồ đạc / thiết bị tự do nằm trên trục dời cùng. Trả số đối tượng đã dời.</summary>
    public static int MoveGrid(Project p, GridLine g, double newCoord, bool drag = true)
    {
        double d = newCoord - g.Coord;
        if (Math.Abs(d) < 1e-9) return 0;
        int n = 0;
        if (drag)
        {
            bool isX = g.Dir == GridDir.X;
            foreach (var nd in p.Nodes.Where(q => OnGrid(g, q.X, q.Y))) { if (isX) nd.X = Math.Round(nd.X + d, 6); else nd.Y = Math.Round(nd.Y + d, 6); n++; }
            foreach (var w in p.Walls)
            {
                bool m = false;
                if (OnGrid(g, w.X1, w.Y1)) { if (isX) w.X1 = Math.Round(w.X1 + d, 6); else w.Y1 = Math.Round(w.Y1 + d, 6); m = true; }
                if (OnGrid(g, w.X2, w.Y2)) { if (isX) w.X2 = Math.Round(w.X2 + d, 6); else w.Y2 = Math.Round(w.Y2 + d, 6); m = true; }
                if (m) n++;
            }
            foreach (var s in p.Arch?.Sections ?? new List<SectionLine>())
            {
                if (OnGrid(g, s.X1, s.Y1) && OnGrid(g, s.X2, s.Y2)) { if (isX) { s.X1 += d; s.X2 += d; } else { s.Y1 += d; s.Y2 += d; } n++; }
            }
        }
        g.Coord = Math.Round(newCoord, 4);
        p.InvalidateIndex();
        return n;
    }

    /// <summary>Đổi tên trục (tên trùng → false).</summary>
    public static bool Rename(Project p, GridLine g, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        name = name.Trim();
        if (p.Grids.Any(x => x != g && x.Name == name)) return false;
        g.Name = name; return true;
    }

    /// <summary>Xoá trục; cấu kiện gắn trên trục giữ nguyên (chỉ còn không có trục định vị). Trả số nút gắn để cảnh báo.</summary>
    public static int DeleteGrid(Project p, GridLine g)
    {
        int att = Attached(p, g).Nodes;
        p.Grids.Remove(g);
        return att;
    }

    /// <summary>Đặt lại tên toàn bộ trục theo thứ tự toạ độ: X → 1, 2, 3…; Y → A, B, C…</summary>
    public static void Renumber(Project p)
    {
        int i = 0;
        foreach (var g in p.GridsX.ToList()) g.Name = (++i).ToString(Inv);
        i = 0;
        foreach (var g in p.GridsY.ToList()) g.Name = LetterName(i++);
    }

    /// <summary>Chuỗi bước trục "3.6 4.2 3.6" (ô "Bước cột" tab Công trình).</summary>
    public static string Spans(Project p, GridDir dir)
    {
        var c = (dir == GridDir.X ? p.GridsX : p.GridsY).Select(g => g.Coord).ToList();
        return c.Count < 2 ? "" : string.Join(" ", c.Zip(c.Skip(1), (a, b) => (b - a).ToString("0.###", Inv)));
    }
}

public enum LevelCopyMode { None, ColumnsOnly, Full }

/// <summary>Thêm / kéo / xoá cao độ tầng.</summary>
public static class LevelOps
{
    /// <summary>Thêm tầng: above = true → ngay trên tầng refStory (null = trên nền / chân cột); false → ngay dưới refStory.
    /// h = chiều cao tầng mới (m). Chép: không / chỉ cột / toàn bộ (dầm, sàn, cột) từ tầng tham chiếu. Trả tên tầng mới.</summary>
    public static string AddLevel(Project p, string refStory, bool above, double h, LevelCopyMode copy = LevelCopyMode.Full)
    {
        var ordered = p.Stories.OrderBy(s => s.Elevation).ToList();
        string below = refStory;
        if (!above && refStory != null)
        {
            int idx = ordered.FindIndex(s => s.Name == refStory);
            below = idx <= 0 ? null : ordered[idx - 1].Name;
        }
        bool wasTop = below != null && ordered.Count > 0 && ordered[^1].Name == below;
        double zBelow = below == null ? p.GroundElevation : ordered.First(s => s.Name == below).Elevation;
        var beforeFrames = p.Frames.Select(f => f.Id).ToHashSet();
        var beforeSlabs = p.Slabs.Select(s => s.Id).ToHashSet();
        string name = Levels.InsertStory(p, below, h, copy != LevelCopyMode.None);
        double zNew = Math.Round(zBelow + h, 4);
        var ns = p.Stories.First(s => s.Name == name);
        if (copy == LevelCopyMode.ColumnsOnly)
        {
            var newBeams = p.Frames.Where(f => !beforeFrames.Contains(f.Id) && f.Kind == FrameKind.Beam && f.Story == name).Select(f => f.Id).ToHashSet();
            p.Frames.RemoveAll(f => newBeams.Contains(f.Id));
            p.Slabs.RemoveAll(s => !beforeSlabs.Contains(s.Id) && s.Story == name);
        }
        // thêm tầng trên cùng: nối cột từ đỉnh cột tầng dưới lên cao độ mới (InsertStory chỉ cắt cột đi qua)
        if (wasTop && copy != LevelCopyMode.None)
        {
            var tops = p.Frames.Where(f => f.Kind == FrameKind.Column && beforeFrames.Contains(f.Id)).Select(f =>
            {
                var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
                return (F: f, Top: a.Z > b.Z ? a : b);
            }).Where(t => Math.Abs(t.Top.Z - zBelow) < 1e-6).ToList();
            foreach (var (f, top) in tops)
            {
                var nn = p.GetOrAddNode(top.X, top.Y, zNew, name);
                if (p.Frames.Any(x => x.Kind == FrameKind.Column && ((x.NodeI == top.Id && x.NodeJ == nn.Id) || (x.NodeJ == top.Id && x.NodeI == nn.Id)))) continue;
                p.Frames.Add(new FrameElement { Id = p.NextFrameId(), NodeI = top.Id, NodeJ = nn.Id, Kind = FrameKind.Column, Section = f.Section, Story = name, Label = f.Label, DesignGroup = f.DesignGroup });
            }
            p.InvalidateIndex();
        }
        return p.Stories.OrderBy(s => Math.Abs(s.Elevation - ns.Elevation)).First().Name;
    }

    /// <summary>Giới hạn cao độ khi kéo: (thấp nhất, cao nhất) để tầng còn cao ≥ 0,5 m so với tầng dưới / trên.</summary>
    public static (double Min, double Max) Limits(Project p, string story)
    {
        var ordered = p.Stories.OrderBy(s => s.Elevation).ToList();
        int i = ordered.FindIndex(s => s.Name == story);
        if (i < 0) return (double.NaN, double.NaN);
        double lo = (i == 0 ? p.BaseElevation : ordered[i - 1].Elevation) + 0.5;
        double hi = i == ordered.Count - 1 ? double.PositiveInfinity : ordered[i + 1].Elevation - 0.5;
        return (lo, hi);
    }

    /// <summary>Kéo đường cao độ: đổi cao độ tầng story tới z (m); mọi nút ở cao độ cũ dời theo (cột, dầm, sàn co giãn), chiều cao
    /// các tầng cập nhật. Ngoài giới hạn → ArgumentException.</summary>
    public static void SetElevation(Project p, string story, double z)
    {
        var st = p.Stories.FirstOrDefault(s => s.Name == story) ?? throw new ArgumentException("Không có tầng " + story);
        var (lo, hi) = Limits(p, story);
        if (z < lo - 1e-9 || z > hi + 1e-9)
            throw new ArgumentException($"Cao độ {z:0.###} m ngoài phạm vi cho phép [{lo:0.###}; {(double.IsInfinity(hi) ? "∞" : hi.ToString("0.###"))}] m (tầng cao ≥ 0,5 m).");
        double old = st.Elevation;
        z = Math.Round(z, 4);
        foreach (var n in p.Nodes.Where(n => Math.Abs(n.Z - old) < 1e-6)) n.Z = z;
        st.Elevation = z;
        var all = p.Stories.OrderBy(s => s.Elevation).ToList();
        for (int i = 0; i < all.Count; i++)
            all[i].Height = Math.Round(all[i].Elevation - (i == 0 ? p.BaseElevation : all[i - 1].Elevation), 4);
        p.InvalidateIndex();
    }

    /// <summary>Chuỗi chiều cao tầng "3.6 3.3 3.3" (ô "Chiều cao tầng" tab Công trình).</summary>
    public static string Heights(Project p)
    {
        var st = p.Stories.OrderBy(s => s.Elevation).ToList();
        var hs = new List<string>();
        double prev = p.BaseElevation;
        foreach (var s in st) { hs.Add((s.Elevation - prev).ToString("0.###", CultureInfo.InvariantCulture)); prev = s.Elevation; }
        return string.Join(" ", hs);
    }
}
