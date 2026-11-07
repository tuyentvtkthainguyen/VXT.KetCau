using KetCauVXT.Import;
using KetCauVXT.Mep;

namespace KetCauVXT.Model;

// =====================================================================
// LƯỚI TRỤC + LƯỚI CỘT + CHIỀU CAO TẦNG TRÊN MỘT HỘP THOẠI (App/GridColumnDialog.cs)
//   • Bước trục X, Y (vd "3.6 4.2 3*3.3"), tên trục đầu (1 / A), cao độ chân cột, bảng tầng (tên, chiều cao).
//   • Lưới cột: ma trận giao điểm trục [i, j] = có cột / không – bấm từng giao điểm, hoặc cả hàng / cả cột / biên.
//   • Áp dụng: tạo lại khung từ lưới (bỏ cột tại giao điểm không đánh dấu: dầm đi qua thành dầm đỡ dầm)
//     hoặc CHỈ ĐỔI CHIỀU CAO TẦNG giữ nguyên mô hình (dời nút, ống điện nước theo cao độ mới).
// =====================================================================

/// <summary>Một dòng bảng tầng: tên cao độ (sàn trên cùng của tầng) và chiều cao tầng (m).</summary>
public sealed class StoryRow
{
    public string Name { get; set; } = "";
    public double Height { get; set; } = 3.3;
}

/// <summary>Số liệu hộp thoại lưới trục + lưới cột + tầng.</summary>
public sealed class GridColumnPlan
{
    public List<double> SpansX { get; set; } = new();
    public List<double> SpansY { get; set; } = new();
    public string FirstX { get; set; } = "1";
    public string FirstY { get; set; } = "A";
    public double BaseElevation { get; set; }
    public List<StoryRow> Stories { get; set; } = new();
    /// <summary>[i, j]: i = chỉ số trục X (1, 2, 3…), j = chỉ số trục Y (A, B…); true = có cột.</summary>
    public bool[,] Columns { get; set; } = new bool[0, 0];

    public int NX => SpansX.Count + 1;
    public int NY => SpansY.Count + 1;
    public int ColumnCount { get { int n = 0; foreach (bool b in Columns) if (b) n++; return n; } }

    /// <summary>Đổi số nhịp → giữ trạng thái các giao điểm cũ, giao điểm mới mặc định có cột.</summary>
    public void ResizeMask()
    {
        var old = Columns ?? new bool[0, 0];
        var m = new bool[NX, NY];
        for (int i = 0; i < NX; i++)
            for (int j = 0; j < NY; j++)
                m[i, j] = i < old.GetLength(0) && j < old.GetLength(1) ? old[i, j] : true;
        Columns = m;
    }

    public void SetAll(bool v) { ResizeMask(); for (int i = 0; i < NX; i++) for (int j = 0; j < NY; j++) Columns[i, j] = v; }

    /// <summary>Chỉ cột biên (chu vi lưới).</summary>
    public void SetPerimeter() { ResizeMask(); for (int i = 0; i < NX; i++) for (int j = 0; j < NY; j++) Columns[i, j] = i == 0 || j == 0 || i == NX - 1 || j == NY - 1; }

    /// <summary>Đảo cả hàng trục Y thứ j (mọi giao điểm trên trục A, B…).</summary>
    public void ToggleRow(int j) { bool any = false; for (int i = 0; i < NX; i++) any |= Columns[i, j]; for (int i = 0; i < NX; i++) Columns[i, j] = !any; }

    /// <summary>Đảo cả cột trục X thứ i (mọi giao điểm trên trục 1, 2…).</summary>
    public void ToggleCol(int i) { bool any = false; for (int j = 0; j < NY; j++) any |= Columns[i, j]; for (int j = 0; j < NY; j++) Columns[i, j] = !any; }

    public List<double> Coords(GridDir d)
    {
        var r = new List<double> { 0 };
        foreach (var s in d == GridDir.X ? SpansX : SpansY) r.Add(Math.Round(r[^1] + s, 4));
        return r;
    }

    public List<double> Elevations()
    {
        var r = new List<double> { BaseElevation };
        foreach (var s in Stories) r.Add(Math.Round(r[^1] + s.Height, 4));
        return r;
    }
}

public static class GridColumnSetup
{
    /// <summary>Tên trục liên tiếp từ tên đầu: "1" → 1, 2, 3…; "A" → A, B… Z, AA; "X1" → X1, X2…; tên khác → tên + số.</summary>
    public static List<string> AxisNames(string first, int count)
    {
        first = string.IsNullOrWhiteSpace(first) ? "1" : first.Trim();
        var r = new List<string>();
        if (int.TryParse(first, out int n0)) { for (int k = 0; k < count; k++) r.Add((n0 + k).ToString()); return r; }
        int li = GridOps.LetterIndex(first);
        if (li >= 0) { for (int k = 0; k < count; k++) r.Add(GridOps.LetterName(li + k)); return r; }
        int d = first.Length;
        while (d > 0 && char.IsDigit(first[d - 1])) d--;
        string pre = first[..d];
        int start = d < first.Length ? int.Parse(first[d..]) : 1;
        for (int k = 0; k < count; k++) r.Add(pre + (start + k));
        return r;
    }

    /// <summary>Tên tầng mặc định cho n tầng: T2, T3… tầng trên cùng "MAI" (1 tầng: T2).</summary>
    public static List<string> DefaultStoryNames(int n)
    {
        var r = new List<string>();
        for (int i = 0; i < n; i++) r.Add(i == n - 1 && n > 1 ? "MAI" : $"T{i + 2}");
        return r;
    }

    /// <summary>Đọc lưới, tầng và vị trí cột (chân cột đang có) của dự án để mở hộp thoại.</summary>
    public static GridColumnPlan FromProject(Project p)
    {
        var plan = new GridColumnPlan { BaseElevation = p.BaseElevation };
        var xs = p.GridsX.ToList(); var ys = p.GridsY.ToList();
        for (int i = 1; i < xs.Count; i++) plan.SpansX.Add(Math.Round(xs[i].Coord - xs[i - 1].Coord, 4));
        for (int j = 1; j < ys.Count; j++) plan.SpansY.Add(Math.Round(ys[j].Coord - ys[j - 1].Coord, 4));
        if (xs.Count > 0) plan.FirstX = xs[0].Name;
        if (ys.Count > 0) plan.FirstY = ys[0].Name;
        double prev = p.BaseElevation;
        foreach (var s in p.Stories.OrderBy(s => s.Elevation)) { plan.Stories.Add(new StoryRow { Name = s.Name, Height = Math.Round(s.Elevation - prev, 4) }); prev = s.Elevation; }
        plan.ResizeMask();
        if (xs.Count > 0 && ys.Count > 0)
        {
            var cols = p.Frames.Where(f => f.Kind == FrameKind.Column).ToList();
            if (cols.Count > 0)
                for (int i = 0; i < xs.Count; i++)
                    for (int j = 0; j < ys.Count; j++)
                        plan.Columns[i, j] = cols.Any(f => { var a = p.Node(f.NodeI); return a != null && Math.Abs(a.X - xs[i].Coord) < GridOps.Tol && Math.Abs(a.Y - ys[j].Coord) < GridOps.Tol; });
        }
        return plan;
    }

    /// <summary>Kiểm tra số liệu; trả về thông báo lỗi (null = hợp lệ).</summary>
    public static string Validate(GridColumnPlan plan)
    {
        if (plan.SpansX.Any(s => s <= 0) || plan.SpansY.Any(s => s <= 0)) return "Bước trục phải > 0.";
        if (plan.Stories.Count == 0) return "Chưa có tầng nào.";
        if (plan.Stories.Any(s => s.Height < 0.5)) return "Chiều cao tầng phải ≥ 0,5 m.";
        var names = plan.Stories.Select(s => (s.Name ?? "").Trim()).ToList();
        if (names.Any(n => n.Length == 0)) return "Tên tầng không được để trống.";
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count) return "Tên tầng bị trùng.";
        var nx = AxisNames(plan.FirstX, plan.NX); var ny = AxisNames(plan.FirstY, plan.NY);
        if (nx.Intersect(ny, StringComparer.OrdinalIgnoreCase).Any()) return "Tên trục X và trục Y bị trùng (vd cùng bắt đầu bằng số).";
        return null;
    }

    /// <summary>Ghi lưới trục (tên theo tên đầu) và tầng vào dự án (chưa sinh khung).</summary>
    public static void ApplyGridAndStories(Project p, GridColumnPlan plan)
    {
        p.Grids.Clear();
        var nx = AxisNames(plan.FirstX, plan.NX); var ny = AxisNames(plan.FirstY, plan.NY);
        var cx = plan.Coords(GridDir.X); var cy = plan.Coords(GridDir.Y);
        for (int i = 0; i < cx.Count; i++) p.Grids.Add(new GridLine { Name = nx[i], Dir = GridDir.X, Coord = cx[i] });
        for (int j = 0; j < cy.Count; j++) p.Grids.Add(new GridLine { Name = ny[j], Dir = GridDir.Y, Coord = cy[j] });
        p.Stories.Clear();
        p.BaseElevation = plan.BaseElevation;
        var el = plan.Elevations();
        for (int k = 0; k < plan.Stories.Count; k++)
            p.Stories.Add(new Story { Name = plan.Stories[k].Name.Trim(), Elevation = el[k + 1], Height = plan.Stories[k].Height });
    }

    /// <summary>Sau ModelBuilder.BuildFromGrid: xoá cột (mọi tầng) tại giao điểm không đánh dấu, gỡ liên kết chân và nút thừa.
    /// Trả về số thanh cột đã xoá.</summary>
    public static int RemoveUnmarkedColumns(Project p, GridColumnPlan plan)
    {
        var xs = p.GridsX.ToList(); var ys = p.GridsY.ToList();
        if (plan.Columns.GetLength(0) != xs.Count || plan.Columns.GetLength(1) != ys.Count) return 0;
        bool Marked(double x, double y)
        {
            int i = xs.FindIndex(g => Math.Abs(g.Coord - x) < GridOps.Tol), j = ys.FindIndex(g => Math.Abs(g.Coord - y) < GridOps.Tol);
            return i < 0 || j < 0 || plan.Columns[i, j];
        }
        var del = p.Frames.Where(f => f.Kind == FrameKind.Column).Where(f => { var a = p.Node(f.NodeI); return a != null && !Marked(a.X, a.Y); }).Select(f => f.Id).ToList();
        if (del.Count == 0) return 0;
        foreach (var f in p.Frames.Where(f => del.Contains(f.Id)).ToList()) ModelEdit.DeleteFrame(p, f);
        foreach (var n in p.Nodes.Where(n => !Marked(n.X, n.Y) && Math.Abs(n.Z - p.BaseElevation) < 1e-6)) n.Restraint = new bool[6];
        DxfImporter.RemoveOrphanNodes(p);
        p.InvalidateIndex();
        return del.Count;
    }

    /// <summary>Chỉ đổi chiều cao tầng, giữ nguyên mô hình: số tầng phải bằng số tầng hiện có. Nút ở (hoặc sát ≤ 0,5 m) một cao độ
    /// dời theo cao độ đó; nút giữa hai cao độ (chiếu nghỉ, nút giữa cột) nội suy tuyến tính; dưới chân cột / trên mái tịnh tiến.
    /// Ống điện nước (đường đi) dời cùng quy tắc. Trả về số nút đã dời.</summary>
    public static int ChangeStoryHeights(Project p, IList<double> heights, double? baseElevation = null)
    {
        var st = p.Stories.OrderBy(s => s.Elevation).ToList();
        if (heights == null || heights.Count != st.Count) throw new ArgumentException($"Cần đúng {st.Count} chiều cao tầng (số tầng hiện có).");
        if (heights.Any(h => h < 0.5)) throw new ArgumentException("Chiều cao tầng phải ≥ 0,5 m.");
        var oldE = new List<double> { p.BaseElevation }; oldE.AddRange(st.Select(s => s.Elevation));
        var newE = new List<double> { Math.Round(baseElevation ?? p.BaseElevation, 4) };
        foreach (var h in heights) newE.Add(Math.Round(newE[^1] + h, 4));
        double Map(double z)
        {
            int near = 0;
            for (int k = 1; k < oldE.Count; k++) if (Math.Abs(z - oldE[k]) < Math.Abs(z - oldE[near])) near = k;
            if (Math.Abs(z - oldE[near]) <= 0.5 + 1e-9 || z <= oldE[0] || z >= oldE[^1]) return z + (newE[near] - oldE[near]);
            int b = 0; while (b + 1 < oldE.Count - 1 && z > oldE[b + 1]) b++;
            double t = (z - oldE[b]) / (oldE[b + 1] - oldE[b]);
            return newE[b] + t * (newE[b + 1] - newE[b]);
        }
        int moved = 0;
        foreach (var n in p.Nodes)
        {
            double z = Math.Round(Map(n.Z), 4);
            if (Math.Abs(z - n.Z) > 1e-9) { n.Z = z; moved++; }
        }
        foreach (var r in p.Mep?.Runs ?? new List<MepRun>())
            for (int k = 0; k < r.Path.Count; k++) { var q = r.Path[k]; q.Z = Math.Round(Map(q.Z), 4); r.Path[k] = q; }
        foreach (var g in p.Arch?.GroundSlabs ?? new List<GroundSlab>()) g.Elevation = Math.Round(Map(g.Elevation), 4);
        p.BaseElevation = newE[0];
        for (int k = 0; k < st.Count; k++) { st[k].Elevation = newE[k + 1]; st[k].Height = Math.Round(heights[k], 4); }
        p.InvalidateIndex();
        return moved;
    }

    /// <summary>Tạo lại khung từ hộp thoại: lưới + tầng → ModelBuilder.BuildFromGrid → bỏ cột tại giao điểm không đánh dấu.</summary>
    public static (int Removed, string Message) Rebuild(Project p, GridColumnPlan plan, ModelBuilder.GridFrameOptions o)
    {
        var err = Validate(plan);
        if (err != null) throw new ArgumentException(err);
        plan.ResizeMask();
        if (plan.ColumnCount == 0) throw new ArgumentException("Chưa đánh dấu giao điểm nào có cột.");
        ImportGeometry.ClearModel(p);
        ApplyGridAndStories(p, plan);
        ImportGeometry.EnsurePatterns(p);
        ModelBuilder.BuildFromGrid(p, o);
        int rm = RemoveUnmarkedColumns(p, plan);
        int ncol = plan.ColumnCount;
        return (rm, $"Đã tạo lưới {plan.NX}×{plan.NY} trục, {plan.Stories.Count} tầng, {ncol} vị trí cột"
                    + (rm > 0 ? $" (bỏ cột ở {plan.NX * plan.NY - ncol} giao điểm – dầm qua đó thành dầm đỡ dầm)" : "") + ".");
    }
}
