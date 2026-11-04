using System.Globalization;
using System.Text.RegularExpressions;
using KetCauVXT.Import.Arch;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Link;

// =====================================================================
// "CẬP NHẬT MÔ HÌNH TỪ LINK": nhận dạng tường / cột / dầm vẽ sẵn / trục trên bản vẽ liên kết (ArchPlanReader với bảng gán layer
// của link + chuẩn layer CAD), đã đặt vào toạ độ mô hình, rồi so với mô hình của một tầng:
//   • Thêm: cấu kiện có trên bản vẽ chưa có trong mô hình (đoạn tường/dầm chỉ phủ một phần → thêm phần còn thiếu);
//   • Dời: cấu kiện mô hình lệch so với bản vẽ (song song, cách ≤ MoveTolerance; cột gần nhất; trục cùng tên);
//   • Xoá: cấu kiện mô hình không còn trên bản vẽ (mặc định KHÔNG đánh dấu – người dùng tự chọn).
// Áp dụng: Apply (gọi trong khung hoàn tác của ứng dụng: Snapshot → Apply → PushUndo).
// =====================================================================

public enum CadDiffAction { Add, Move, Remove }
public enum CadDiffElement { Wall, Column, Beam, Grid }

/// <summary>Một khác biệt bản vẽ ↔ mô hình.</summary>
public class CadDiffItem
{
    public CadDiffAction Action { get; set; }
    public CadDiffElement Element { get; set; }
    /// <summary>Đánh dấu áp dụng</summary>
    public bool Apply { get; set; } = true;
    /// <summary>Vị trí mới (bản vẽ): đoạn tường/dầm X1,Y1 → X2,Y2; cột tâm X1,Y1; trục Coord.</summary>
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    /// <summary>Chiều dày tường / bề rộng dầm (m)</summary>
    public double Thickness { get; set; }
    /// <summary>Độ vồng tường cong (m, có dấu)</summary>
    public double Sagitta { get; set; }
    /// <summary>Kích thước cột (m), góc xoay (độ), đường kính cột tròn</summary>
    public double Bx { get; set; }
    public double By { get; set; }
    public double Angle { get; set; }
    public double Diameter { get; set; }
    public GridDir Dir { get; set; }
    public string GridName { get; set; } = "";
    public double Coord { get; set; }
    /// <summary>Đối tượng mô hình (dời / xoá): WallLine.Id, FrameElement.Id, tên trục</summary>
    public int WallId { get; set; }
    public int FrameId { get; set; }
    public string ModelGrid { get; set; } = "";
    /// <summary>Khoảng dời (m) – thông tin</summary>
    public double Distance { get; set; }
    public string Layer { get; set; } = "";

    public string ActionText => Action switch { CadDiffAction.Add => "Thêm", CadDiffAction.Move => "Dời", _ => "Xoá" };
    public string ElementText => Element switch { CadDiffElement.Wall => "Tường", CadDiffElement.Column => "Cột", CadDiffElement.Beam => "Dầm", _ => "Trục" };

    public string Description
    {
        get
        {
            var c = CultureInfo.InvariantCulture;
            string P(double x, double y) => string.Format(c, "({0:0.000}; {1:0.000})", x, y);
            switch (Element)
            {
                case CadDiffElement.Grid:
                    return Action switch
                    {
                        CadDiffAction.Add => string.Format(c, "trục {0} ({1} = {2:0.000} m)", GridName, Dir == GridDir.X ? "x" : "y", Coord),
                        CadDiffAction.Move => string.Format(c, "trục {0}: → {1:0.000} m (lệch {2:0.000} m)", ModelGrid, Coord, Distance),
                        _ => $"trục {ModelGrid} không còn trên bản vẽ"
                    };
                case CadDiffElement.Column:
                    return Action switch
                    {
                        CadDiffAction.Add => $"cột {(Diameter > 0 ? string.Format(c, "D{0:0}", Diameter * 1000) : string.Format(c, "{0:0}×{1:0}", Bx * 1000, By * 1000))} tại {P(X1, Y1)}" + (Math.Abs(Angle) > 1e-6 ? string.Format(c, ", xoay {0:0.#}°", Angle) : ""),
                        CadDiffAction.Move => $"cột #{FrameId} → {P(X1, Y1)} (lệch {Distance.ToString("0.000", c)} m)",
                        _ => $"cột #{FrameId} tại {P(X1, Y1)} không còn trên bản vẽ"
                    };
                default:
                {
                    string what = Element == CadDiffElement.Wall ? "tường" : "dầm";
                    double L = Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
                    string th = Thickness > 0 ? string.Format(c, " dày {0:0}", Thickness * 1000) : "";
                    return Action switch
                    {
                        CadDiffAction.Add => $"{what}{th} {P(X1, Y1)} → {P(X2, Y2)}, L = {L.ToString("0.00", c)} m" + (Math.Abs(Sagitta) > 1e-6 ? " (cong)" : ""),
                        CadDiffAction.Move => $"{what} #{(Element == CadDiffElement.Wall ? WallId : FrameId)} → {P(X1, Y1)} – {P(X2, Y2)} (lệch {Distance.ToString("0.000", c)} m)",
                        _ => $"{what} #{(Element == CadDiffElement.Wall ? WallId : FrameId)} {P(X1, Y1)} → {P(X2, Y2)} không còn trên bản vẽ"
                    };
                }
            }
        }
    }

    public override string ToString() => $"{ActionText} {ElementText}: {Description}";
}

/// <summary>Tuỳ chọn so sánh bản vẽ ↔ mô hình.</summary>
public class CadSyncOptions
{
    /// <summary>Dung sai trùng vị trí tường/dầm (m)</summary>
    public double Tolerance { get; set; } = 0.05;
    /// <summary>Dung sai trùng tâm cột (m)</summary>
    public double ColumnTolerance { get; set; } = 0.05;
    /// <summary>Dung sai trùng trục (m)</summary>
    public double GridTolerance { get; set; } = 0.01;
    /// <summary>Lệch lớn nhất còn coi là "dời" (m); lớn hơn → xoá + thêm</summary>
    public double MoveTolerance { get; set; } = 0.6;
    /// <summary>Đoạn tường/dầm thêm ngắn nhất (m)</summary>
    public double MinLength { get; set; } = 0.3;
    public bool Walls { get; set; } = true;
    public bool Columns { get; set; } = true;
    public bool Beams { get; set; } = true;
    public bool Grids { get; set; } = true;
}

/// <summary>Kết quả so sánh một tầng.</summary>
public class CadLinkDiff
{
    public string LinkId { get; set; } = "";
    public string LinkName { get; set; } = "";
    public string Hash { get; set; } = "";
    public string Story { get; set; } = "";
    public List<CadDiffItem> Items { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Info { get; } = new();
    /// <summary>Mặt bằng nhận dạng (toạ độ mô hình)</summary>
    public ArchFloorPlan Plan { get; set; }

    public int Count(CadDiffAction a, CadDiffElement e) => Items.Count(i => i.Action == a && i.Element == e);
    public bool IsEmpty => Items.Count == 0;

    public string Summary
    {
        get
        {
            if (Items.Count == 0) return $"Bản vẽ \"{LinkName}\" khớp mô hình tầng {Story} – không có thay đổi.";
            var parts = new List<string>();
            foreach (CadDiffAction a in Enum.GetValues(typeof(CadDiffAction)))
            {
                var g = Items.Where(i => i.Action == a).GroupBy(i => i.Element).Select(x => $"{x.Count()} {x.First().ElementText.ToLowerInvariant()}").ToList();
                if (g.Count > 0) parts.Add((a == CadDiffAction.Add ? "thêm " : a == CadDiffAction.Move ? "dời " : "không còn trên bản vẽ: ") + string.Join(", ", g));
            }
            return $"Bản vẽ \"{LinkName}\" ↔ tầng {Story}: " + string.Join("; ", parts) + ".";
        }
    }
}

public static class CadLinkSync
{
    /// <summary>Tuỳ chọn nhận dạng: quy tắc chuẩn layer CAD của dự án (hoặc chuẩn đang dùng / mặc định) + bảng gán layer của link.</summary>
    public static ArchImportOptions OptionsFor(Project p, CadLink l)
    {
        var std = p?.CadStandard ?? CadStandard.Active;
        var o = new ArchImportOptions
        {
            LayerRules = (std?.InputRules ?? CadStandard.DefaultInputRules()).Select(r => r.Clone()).ToList(),
            LayerMap = (l?.LayerMap ?? new List<LayerMapEntry>()).Where(m => m != null && !string.IsNullOrEmpty(m.Layer))
                .Select(m => new LayerMapEntry(m.Layer, m.Category)).ToList(),
            SkipOffLayers = std?.SkipOffLayers ?? true,
            Concrete = p?.Concrete?.ConcreteBeam ?? "B25"
        };
        // layer ẩn trên nền (HiddenLayers) vẫn được nhận dạng – muốn bỏ qua thì gán nhóm "Bỏ qua" trong bảng layer của link
        return o;
    }

    /// <summary>Bảng layer của bản vẽ liên kết (đếm thực thể, màu, nhóm tự nhận theo quy tắc + gán của link).</summary>
    public static CadLayerScanResult ScanLayers(Project p, CadLink l, CadLinkGeometry g)
    {
        if (g == null) return new CadLayerScanResult();
        var colors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < g.Layers.Count; i++) colors[g.Layers[i]] = g.LayerColors[i];
        return CadLayerScan.FromEntities(g.Entities.Concat(g.Fills), OptionsFor(p, l), Path.GetFileName(g.Path ?? ""), colors, null, g.FrozenLayers, g.OffLayers);
    }

    /// <summary>Nhận dạng mặt bằng trên bản vẽ liên kết, trả toạ độ mô hình. Nhiều mặt bằng trên một file → chọn mặt bằng chồng lên
    /// phạm vi mô hình của tầng nhiều nhất (chưa có mô hình: mặt bằng có tổng chiều dài tường lớn nhất).</summary>
    public static ArchFloorPlan Recognize(Project p, CadLink l, CadLinkGeometry g, string story, out ArchDrawing drawing)
    {
        drawing = null;
        if (g == null) return null;
        var (ents, fills) = g.ModelEntities(l);
        var o = OptionsFor(p, l);
        drawing = ArchPlanReader.FromEntities(ents, fills, o, l?.DisplayName ?? "");
        if (drawing.Floors.Count == 0) return null;
        foreach (var f in drawing.Floors) Unshift(f);
        var ext = ModelExtent(p, story);
        if (ext == null) return drawing.Floors.OrderByDescending(f => f.Walls.Sum(w => w.Length)).First();
        var e = ext.Value;
        return drawing.Floors.OrderByDescending(f =>
        {
            double ox = Math.Min(f.X1, e.X1) - Math.Max(f.X0, e.X0), oy = Math.Min(f.Y1, e.Y1) - Math.Max(f.Y0, e.Y0);
            return ox > 0 && oy > 0 ? ox * oy : 0;
        }).ThenByDescending(f => f.Walls.Sum(w => w.Length)).First();
    }

    /// <summary>ArchPlanReader dời gốc toạ độ về giao trục đầu tiên – trả về toạ độ gốc (đã là toạ độ mô hình).</summary>
    private static void Unshift(ArchFloorPlan f)
    {
        double dx = f.OffsetX, dy = f.OffsetY;
        if (Math.Abs(dx) < 1e-12 && Math.Abs(dy) < 1e-12) return;
        static double R(double v) => Math.Round(v, 4);
        foreach (var w in f.Walls) { w.Ax = R(w.Ax - dx); w.Bx = R(w.Bx - dx); w.Ay = R(w.Ay - dy); w.By = R(w.By - dy); }
        foreach (var gr in f.Grids) gr.Coord = R(gr.Coord - (gr.Dir == GridDir.X ? dx : dy));
        foreach (var c in f.Columns) { c.X = R(c.X - dx); c.Y = R(c.Y - dy); }
        foreach (var b in f.Beams) { b.Ax = R(b.Ax - dx); b.Bx = R(b.Bx - dx); b.Ay = R(b.Ay - dy); b.By = R(b.By - dy); }
        foreach (var r in f.Rooms) { r.X -= dx; r.Y -= dy; }
        foreach (var op in f.Openings) { op.X -= dx; op.Y -= dy; }
        foreach (var z in f.Zones) { z.X0 = R(z.X0 - dx); z.X1 = R(z.X1 - dx); z.Y0 = R(z.Y0 - dy); z.Y1 = R(z.Y1 - dy); }
        foreach (var sr in f.SlabRegions)
        {
            sr.Points = sr.Points.Select(q => (R(q.X - dx), R(q.Y - dy))).ToList();
            sr.X0 = R(sr.X0 - dx); sr.X1 = R(sr.X1 - dx); sr.Y0 = R(sr.Y0 - dy); sr.Y1 = R(sr.Y1 - dy);
        }
        f.Outline = f.Outline.Select(q => (R(q.X - dx), R(q.Y - dy))).ToList();
        f.OffsetX = 0; f.OffsetY = 0;
    }

    private static (double X0, double Y0, double X1, double Y1)? ModelExtent(Project p, string story)
    {
        if (p == null) return null;
        double z = SafeZ(p, story);
        var xs = new List<double>(); var ys = new List<double>();
        foreach (var n in p.Nodes) if (double.IsNaN(z) || Math.Abs(n.Z - z) < 0.01) { xs.Add(n.X); ys.Add(n.Y); }
        foreach (var w in p.Walls.Where(w => w.Story == story)) { xs.Add(w.X1); xs.Add(w.X2); ys.Add(w.Y1); ys.Add(w.Y2); }
        if (xs.Count == 0)
        {
            foreach (var g in p.Grids) { if (g.Dir == GridDir.X) xs.Add(g.Coord); else ys.Add(g.Coord); }
            if (xs.Count == 0 || ys.Count == 0) return null;
        }
        if (xs.Count == 0 || ys.Count == 0) return null;
        return (xs.Min() - 0.5, ys.Min() - 0.5, xs.Max() + 0.5, ys.Max() + 0.5);
    }

    private static double SafeZ(Project p, string story)
    {
        var s = p.Stories.FirstOrDefault(q => q.Name == story);
        return s?.Elevation ?? double.NaN;
    }

    // ------------------------------------------------------------------
    // So sánh
    // ------------------------------------------------------------------
    private struct Lin
    {
        public double Ax, Ay, Bx, By, W, Sag;
        public int Ref;
        public string Layer;
        public double Len => Math.Sqrt((Bx - Ax) * (Bx - Ax) + (By - Ay) * (By - Ay));
        public bool Curved => Math.Abs(Sag) > 1e-6;
    }

    /// <summary>Các khoảng [t0, t1] (m, dọc r từ A) được phủ bởi các đoạn song song nằm trên đường r trong dung sai tol.</summary>
    private static List<(double T0, double T1)> Covered(Lin r, IEnumerable<Lin> others, double tol)
    {
        double L = r.Len;
        var iv = new List<(double, double)>();
        if (L < 1e-9) return iv;
        double ux = (r.Bx - r.Ax) / L, uy = (r.By - r.Ay) / L;
        foreach (var o in others)
        {
            if (o.Curved) continue;
            double d1 = Math.Abs(-(o.Ax - r.Ax) * uy + (o.Ay - r.Ay) * ux), d2 = Math.Abs(-(o.Bx - r.Ax) * uy + (o.By - r.Ay) * ux);
            if (d1 > tol || d2 > tol) continue;
            double t1 = (o.Ax - r.Ax) * ux + (o.Ay - r.Ay) * uy, t2 = (o.Bx - r.Ax) * ux + (o.By - r.Ay) * uy;
            double a = Math.Max(0, Math.Min(t1, t2)), b = Math.Min(L, Math.Max(t1, t2));
            if (b > a + 1e-9) iv.Add((a, b));
        }
        return Union(iv);
    }

    private static List<(double T0, double T1)> Union(List<(double, double)> iv)
    {
        var res = new List<(double T0, double T1)>();
        foreach (var (a, b) in iv.OrderBy(q => q.Item1))
        {
            if (res.Count > 0 && a <= res[^1].T1 + 1e-6) res[^1] = (res[^1].T0, Math.Max(res[^1].T1, b));
            else res.Add((a, b));
        }
        return res;
    }

    private static double Sum(List<(double T0, double T1)> iv) => iv.Sum(q => q.T1 - q.T0);

    private static List<(double T0, double T1)> Gaps(List<(double T0, double T1)> cov, double L)
    {
        var g = new List<(double T0, double T1)>();
        double t = 0;
        foreach (var (a, b) in cov) { if (a > t) g.Add((t, a)); t = Math.Max(t, b); }
        if (t < L) g.Add((t, L));
        return g;
    }

    /// <summary>Khoảng cách vuông góc giữa hai đường song song (∞ nếu không song song trong 1°) + chiều dài chồng hình chiếu.</summary>
    private static bool ParallelOffset(Lin r, Lin m, out double dist, out double overlap)
    {
        dist = double.PositiveInfinity; overlap = 0;
        double L = r.Len, Lm = m.Len;
        if (L < 1e-9 || Lm < 1e-9 || r.Curved || m.Curved) return false;
        double ux = (r.Bx - r.Ax) / L, uy = (r.By - r.Ay) / L, vx = (m.Bx - m.Ax) / Lm, vy = (m.By - m.Ay) / Lm;
        if (Math.Abs(ux * vy - uy * vx) > Math.Sin(Math.PI / 180)) return false;
        double d1 = -(m.Ax - r.Ax) * uy + (m.Ay - r.Ay) * ux, d2 = -(m.Bx - r.Ax) * uy + (m.By - r.Ay) * ux;
        dist = Math.Abs((d1 + d2) / 2);
        double t1 = (m.Ax - r.Ax) * ux + (m.Ay - r.Ay) * uy, t2 = (m.Bx - r.Ax) * ux + (m.By - r.Ay) * uy;
        overlap = Math.Min(L, Math.Max(t1, t2)) - Math.Max(0, Math.Min(t1, t2));
        return true;
    }

    /// <summary>So sánh bản vẽ liên kết với mô hình của tầng story.</summary>
    public static CadLinkDiff Compare(Project p, CadLink l, CadLinkGeometry g, string story, CadSyncOptions o = null)
    {
        o ??= new CadSyncOptions();
        var diff = new CadLinkDiff { LinkId = l?.Id ?? "", LinkName = l?.DisplayName ?? "", Hash = g?.Hash ?? "", Story = story ?? "" };
        if (p == null || g == null) { diff.Warnings.Add("Chưa đọc được bản vẽ liên kết."); return diff; }
        if (p.Stories.All(s => s.Name != story)) { diff.Warnings.Add($"Tầng \"{story}\" không có trong mô hình – chọn một tầng cụ thể."); return diff; }
        var plan = Recognize(p, l, g, story, out var drawing);
        if (drawing != null) { diff.Warnings.AddRange(drawing.Warnings); diff.Info.AddRange(drawing.Info); }
        if (plan == null) { diff.Warnings.Add("Không nhận dạng được mặt bằng trên bản vẽ link (kiểm tra gán layer Tường/Trục/Cột)."); return diff; }
        diff.Plan = plan;
        diff.Info.Add($"Mặt bằng nhận dạng: {plan.Walls.Count} đoạn tường, {plan.Columns.Count} cột, {plan.Beams.Count} dầm vẽ sẵn, {plan.Grids.Count(q => !q.Auto)} trục.");
        if (o.Grids) CompareGrids(p, plan, o, diff);
        if (o.Walls) CompareWalls(p, plan, story, o, diff);
        if (o.Columns) CompareColumns(p, plan, story, o, diff);
        if (o.Beams) CompareBeams(p, plan, story, o, diff);
        return diff;
    }

    private static void CompareGrids(Project p, ArchFloorPlan plan, CadSyncOptions o, CadLinkDiff diff)
    {
        var rec = plan.Grids.Where(q => !q.Auto).ToList();
        if (rec.Count == 0) { diff.Info.Add("Bản vẽ không có lớp trục – bỏ qua so sánh trục."); return; }
        var used = new HashSet<GridLine>();
        var pending = new List<ArchGrid>();
        foreach (var r in rec)
        {
            var same = p.Grids.Where(q => q.Dir == r.Dir && !used.Contains(q) && Math.Abs(q.Coord - r.Coord) <= o.GridTolerance)
                .OrderBy(q => Math.Abs(q.Coord - r.Coord)).FirstOrDefault();
            if (same != null) { used.Add(same); continue; }
            pending.Add(r);
        }
        foreach (var r in pending)
        {
            var byName = p.Grids.FirstOrDefault(q => q.Dir == r.Dir && !used.Contains(q) && string.Equals(q.Name, r.Name, StringComparison.OrdinalIgnoreCase));
            if (byName != null && Math.Abs(byName.Coord - r.Coord) <= Math.Max(o.MoveTolerance, 5.0))
            {
                used.Add(byName);
                diff.Items.Add(new CadDiffItem { Action = CadDiffAction.Move, Element = CadDiffElement.Grid, Dir = r.Dir, Coord = r.Coord, GridName = r.Name, ModelGrid = byName.Name, Distance = Math.Abs(byName.Coord - r.Coord) });
                continue;
            }
            diff.Items.Add(new CadDiffItem { Action = CadDiffAction.Add, Element = CadDiffElement.Grid, Dir = r.Dir, Coord = r.Coord, GridName = r.Name });
        }
        foreach (var m in p.Grids.Where(q => !used.Contains(q)))
            diff.Items.Add(new CadDiffItem { Action = CadDiffAction.Remove, Element = CadDiffElement.Grid, Dir = m.Dir, Coord = m.Coord, ModelGrid = m.Name, Apply = false });
    }

    private static void CompareWalls(Project p, ArchFloorPlan plan, string story, CadSyncOptions o, CadLinkDiff diff)
    {
        var rec = plan.Walls.Select(w => new Lin { Ax = w.Ax, Ay = w.Ay, Bx = w.Bx, By = w.By, W = w.Thickness, Sag = w.Sagitta, Layer = w.Layer }).ToList();
        var mod = p.Walls.Where(w => w.Story == story).Select(w => new Lin { Ax = w.X1, Ay = w.Y1, Bx = w.X2, By = w.Y2, Sag = w.Sagitta, Ref = w.Id }).ToList();
        CompareLinear(rec, mod, CadDiffElement.Wall, o, diff, allowMove: true);
    }

    private static void CompareBeams(Project p, ArchFloorPlan plan, string story, CadSyncOptions o, CadLinkDiff diff)
    {
        if (plan.Beams.Count == 0) return;   // bản vẽ không vẽ dầm → không đề xuất xoá dầm mô hình
        var rec = plan.Beams.Select(b => new Lin { Ax = b.Ax, Ay = b.Ay, Bx = b.Bx, By = b.By, W = b.Width, Layer = b.Layer }).ToList();
        var mod = new List<Lin>();
        foreach (var f in ModelEdit.StoryBeams(p, story))
        {
            var a = p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var b = p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
            if (a == null || b == null) continue;
            mod.Add(new Lin { Ax = a.X, Ay = a.Y, Bx = b.X, By = b.Y, Sag = f.CurveMidOffset, Ref = f.Id });
        }
        CompareLinear(rec, mod, CadDiffElement.Beam, o, diff, allowMove: false);
    }

    private static void CompareLinear(List<Lin> rec, List<Lin> mod, CadDiffElement el, CadSyncOptions o, CadLinkDiff diff, bool allowMove)
    {
        double tol = o.Tolerance;
        var moved = new HashSet<int>();
        var straightMod = mod.Where(m => !m.Curved).ToList();
        foreach (var r in rec)
        {
            double L = r.Len;
            if (L < 1e-6) continue;
            if (r.Curved)
            {
                bool match = mod.Any(m => m.Curved && ((Near(m.Ax, m.Ay, r.Ax, r.Ay, tol) && Near(m.Bx, m.By, r.Bx, r.By, tol) && Math.Abs(m.Sag - r.Sag) <= tol)
                                                    || (Near(m.Ax, m.Ay, r.Bx, r.By, tol) && Near(m.Bx, m.By, r.Ax, r.Ay, tol) && Math.Abs(m.Sag + r.Sag) <= tol)));
                if (!match) diff.Items.Add(NewLin(CadDiffAction.Add, el, r.Ax, r.Ay, r.Bx, r.By, r.W, r.Sag, r.Layer));
                continue;
            }
            var cov = Covered(r, straightMod, tol);
            double c = Sum(cov);
            if (c >= 0.9 * L || L - c < Math.Max(o.MinLength, 0.05)) continue;
            if (allowMove && c < 0.1 * L)
            {
                // đoạn mô hình song song lệch ≤ MoveTolerance, chồng ≥ 50 % và không có đoạn bản vẽ nào trên vị trí cũ → "dời"
                Lin best = default; double bestD = double.PositiveInfinity; bool found = false;
                foreach (var m in straightMod)
                {
                    if (moved.Contains(m.Ref)) continue;
                    if (!ParallelOffset(r, m, out double d, out double ov) || d <= tol || d > o.MoveTolerance) continue;
                    if (ov < 0.5 * Math.Min(L, m.Len)) continue;
                    if (Sum(Covered(m, rec, tol)) > 0.1 * m.Len) continue;
                    if (d < bestD) { bestD = d; best = m; found = true; }
                }
                if (found)
                {
                    moved.Add(best.Ref);
                    var it = NewLin(CadDiffAction.Move, el, r.Ax, r.Ay, r.Bx, r.By, r.W, 0, r.Layer);
                    if (el == CadDiffElement.Wall) it.WallId = best.Ref; else it.FrameId = best.Ref;
                    it.Distance = Math.Round(bestD, 4);
                    diff.Items.Add(it);
                    continue;
                }
            }
            double ux = (r.Bx - r.Ax) / L, uy = (r.By - r.Ay) / L;
            foreach (var (t0, t1) in Gaps(cov, L))
            {
                if (t1 - t0 < o.MinLength) continue;
                diff.Items.Add(NewLin(CadDiffAction.Add, el, Math.Round(r.Ax + ux * t0, 4), Math.Round(r.Ay + uy * t0, 4),
                                      Math.Round(r.Ax + ux * t1, 4), Math.Round(r.Ay + uy * t1, 4), r.W, 0, r.Layer));
            }
        }
        foreach (var m in mod)
        {
            if (moved.Contains(m.Ref)) continue;
            bool gone;
            if (m.Curved)
                gone = !rec.Any(r => r.Curved && ((Near(m.Ax, m.Ay, r.Ax, r.Ay, tol) && Near(m.Bx, m.By, r.Bx, r.By, tol)) || (Near(m.Ax, m.Ay, r.Bx, r.By, tol) && Near(m.Bx, m.By, r.Ax, r.Ay, tol))));
            else gone = Sum(Covered(m, rec, tol)) < 0.1 * m.Len;
            if (!gone) continue;
            var it = NewLin(CadDiffAction.Remove, el, m.Ax, m.Ay, m.Bx, m.By, 0, m.Sag, "");
            it.Apply = false;
            if (el == CadDiffElement.Wall) it.WallId = m.Ref; else it.FrameId = m.Ref;
            diff.Items.Add(it);
        }
    }

    private static CadDiffItem NewLin(CadDiffAction a, CadDiffElement el, double x1, double y1, double x2, double y2, double w, double sag, string layer) =>
        new() { Action = a, Element = el, X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Thickness = w, Sagitta = sag, Layer = layer ?? "" };

    private static bool Near(double ax, double ay, double bx, double by, double tol) => Math.Abs(ax - bx) <= tol && Math.Abs(ay - by) <= tol;

    private static void CompareColumns(Project p, ArchFloorPlan plan, string story, CadSyncOptions o, CadLinkDiff diff)
    {
        var mod = new List<(FrameElement f, double X, double Y)>();
        foreach (var f in p.Frames.Where(f => f.IsVerticalKind && f.Story == story))
        {
            var n = p.Nodes.FirstOrDefault(q => q.Id == f.NodeJ);
            if (n != null) mod.Add((f, n.X, n.Y));
        }
        var rec = plan.Columns.ToList();
        var usedM = new HashSet<int>(); var usedR = new HashSet<int>();
        // khớp tham lam theo khoảng cách tăng dần
        var pairs = new List<(int r, int m, double d)>();
        for (int i = 0; i < rec.Count; i++)
            for (int j = 0; j < mod.Count; j++)
            {
                double d = Math.Sqrt((rec[i].X - mod[j].X) * (rec[i].X - mod[j].X) + (rec[i].Y - mod[j].Y) * (rec[i].Y - mod[j].Y));
                if (d <= o.MoveTolerance) pairs.Add((i, j, d));
            }
        foreach (var (ri, mj, d) in pairs.OrderBy(q => q.d))
        {
            if (usedR.Contains(ri) || usedM.Contains(mj)) continue;
            usedR.Add(ri); usedM.Add(mj);
            if (d <= o.ColumnTolerance) continue;
            var c = rec[ri];
            diff.Items.Add(new CadDiffItem
            {
                Action = CadDiffAction.Move, Element = CadDiffElement.Column, X1 = c.X, Y1 = c.Y, Bx = c.Bx, By = c.By, Angle = c.Angle, Diameter = c.Diameter,
                FrameId = mod[mj].f.Id, Distance = Math.Round(d, 4),
                Apply = false   // cột lệch thường do mô hình đặt tim cột lên trục – để người dùng tự chọn
            });
        }
        for (int i = 0; i < rec.Count; i++)
        {
            if (usedR.Contains(i)) continue;
            var c = rec[i];
            diff.Items.Add(new CadDiffItem { Action = CadDiffAction.Add, Element = CadDiffElement.Column, X1 = c.X, Y1 = c.Y, Bx = c.Bx, By = c.By, Angle = c.Angle, Diameter = c.Diameter });
        }
        if (rec.Count == 0) return;   // bản vẽ không có lớp cột → không đề xuất xoá cột
        for (int j = 0; j < mod.Count; j++)
        {
            if (usedM.Contains(j)) continue;
            diff.Items.Add(new CadDiffItem { Action = CadDiffAction.Remove, Element = CadDiffElement.Column, X1 = mod[j].X, Y1 = mod[j].Y, FrameId = mod[j].f.Id, Apply = false });
        }
    }

    // ------------------------------------------------------------------
    // Áp dụng
    // ------------------------------------------------------------------
    /// <summary>Áp dụng các mục đã đánh dấu vào mô hình (xoá → dời → thêm). Gọi trong khung hoàn tác của ứng dụng.</summary>
    public static EditResult Apply(Project p, CadLinkDiff diff)
    {
        if (p == null || diff == null) return EditResult.Fail("Chưa có kết quả so sánh.");
        var items = diff.Items.Where(i => i.Apply).ToList();
        if (items.Count == 0) return EditResult.Fail("Chưa đánh dấu thay đổi nào để áp dụng.");
        string story = diff.Story;
        var st = p.Stories.FirstOrDefault(s => s.Name == story);
        if (st == null) return EditResult.Fail($"Tầng \"{story}\" không có trong mô hình.");
        var r = new EditResult { Ok = true };
        var cnt = new Dictionary<string, int>();
        void Count(CadDiffItem it) { string k = it.ActionText.ToLowerInvariant() + " " + it.ElementText.ToLowerInvariant(); cnt[k] = cnt.TryGetValue(k, out int n) ? n + 1 : 1; }
        var warn = new List<string>();

        // ---- xoá ----
        foreach (var it in items.Where(i => i.Action == CadDiffAction.Remove))
        {
            switch (it.Element)
            {
                case CadDiffElement.Wall:
                    if (p.Walls.RemoveAll(w => w.Id == it.WallId) > 0) Count(it);
                    break;
                case CadDiffElement.Column:
                case CadDiffElement.Beam:
                {
                    var f = p.Frames.FirstOrDefault(q => q.Id == it.FrameId);
                    if (f != null && ModelEdit.DeleteFrame(p, f).Ok) Count(it);
                    break;
                }
                case CadDiffElement.Grid:
                    if (p.Grids.RemoveAll(g => g.Name == it.ModelGrid && g.Dir == it.Dir) > 0) Count(it);
                    break;
            }
        }
        // ---- dời ----
        foreach (var it in items.Where(i => i.Action == CadDiffAction.Move))
        {
            switch (it.Element)
            {
                case CadDiffElement.Wall:
                {
                    var w = p.Walls.FirstOrDefault(q => q.Id == it.WallId);
                    if (w == null) break;
                    w.X1 = it.X1; w.Y1 = it.Y1; w.X2 = it.X2; w.Y2 = it.Y2; w.Sagitta = 0;
                    Count(it);
                    break;
                }
                case CadDiffElement.Column:
                {
                    var f = p.Frames.FirstOrDefault(q => q.Id == it.FrameId);
                    if (f == null) break;
                    foreach (int id in new[] { f.NodeI, f.NodeJ })
                    {
                        var n = p.Nodes.FirstOrDefault(q => q.Id == id);
                        if (n == null) continue;
                        n.X = Math.Round(it.X1, 4); n.Y = Math.Round(it.Y1, 4);
                    }
                    if (p.Frames.Count(q => q.IsVerticalKind && (q.NodeI == f.NodeJ || q.NodeJ == f.NodeI) && q.Id != f.Id) > 0)
                        warn.Add($"Cột #{f.Id}: nút chung với cột tầng trên/dưới cũng đã dời – kiểm tra cột các tầng khác.");
                    Count(it);
                    break;
                }
                case CadDiffElement.Grid:
                {
                    var g = p.Grids.FirstOrDefault(q => q.Name == it.ModelGrid && q.Dir == it.Dir);
                    if (g == null) break;
                    g.Coord = Math.Round(it.Coord, 4);
                    Count(it);
                    break;
                }
            }
        }
        // ---- thêm ----
        string beamSec = null;
        foreach (var it in items.Where(i => i.Action == CadDiffAction.Add))
        {
            switch (it.Element)
            {
                case CadDiffElement.Grid:
                {
                    string name = string.IsNullOrWhiteSpace(it.GridName) ? "" : it.GridName.Trim();
                    if (name.Length == 0 || p.Grids.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        string bas = name.Length == 0 ? (it.Dir == GridDir.X ? "X" : "Y") : name;
                        int k = 1;
                        while (p.Grids.Any(g => string.Equals(g.Name, $"{bas}'{(k > 1 ? k.ToString(CultureInfo.InvariantCulture) : "")}", StringComparison.OrdinalIgnoreCase))) k++;
                        name = $"{bas}'{(k > 1 ? k.ToString(CultureInfo.InvariantCulture) : "")}";
                    }
                    p.Grids.Add(new GridLine { Name = name, Dir = it.Dir, Coord = Math.Round(it.Coord, 4) });
                    Count(it);
                    break;
                }
                case CadDiffElement.Wall:
                {
                    var wt = NearestWallType(p, it.Thickness);
                    if (wt == null) { warn.Add("Chưa có loại tường nào – khai báo ở Mô hình → Loại tường."); break; }
                    var w = new WallLine
                    {
                        Id = p.Walls.Select(q => q.Id).DefaultIfEmpty(0).Max() + 1, Story = story,
                        X1 = Math.Round(it.X1, 4), Y1 = Math.Round(it.Y1, 4), X2 = Math.Round(it.X2, 4), Y2 = Math.Round(it.Y2, 4),
                        Type = wt.Name, Sagitta = Math.Round(it.Sagitta, 4), Note = "Từ bản vẽ link " + diff.LinkName
                    };
                    if (w.Length < 0.2) break;
                    if (it.Thickness > 0 && Math.Abs(wt.Thickness - it.Thickness) > 0.03)
                        warn.Add($"Tường dày {it.Thickness * 1000:0} mm: chưa có loại tường cùng chiều dày – dùng \"{wt.Name}\".");
                    p.Walls.Add(w);
                    Count(it);
                    break;
                }
                case CadDiffElement.Column:
                {
                    var f = AddColumn(p, st, it);
                    if (f != null) { r.NewFrames.Add(f.Id); Count(it); }
                    break;
                }
                case CadDiffElement.Beam:
                {
                    beamSec ??= BeamSection(p, story, it.Thickness);
                    var br = BeamTools.AddBeamExact(p, story, it.X1, it.Y1, it.X2, it.Y2, beamSec, "L", false);
                    if (br.Ok) { r.NewFrames.AddRange(br.NewFrames); Count(it); }
                    else warn.Add(br.Message);
                    break;
                }
            }
        }
        int split = ModelBuilder.SplitFramesAtNodes(p);
        p.InvalidateIndex();
        var link = p.CadLinks?.FirstOrDefault(q => q.Id == diff.LinkId);
        if (link != null && !string.IsNullOrEmpty(diff.Hash)) link.SyncedHash = diff.Hash;
        if (cnt.Count == 0) return EditResult.Fail("Không áp dụng được thay đổi nào." + (warn.Count > 0 ? " " + warn[0] : ""));
        r.Message = $"Đã cập nhật tầng {story} từ bản vẽ link \"{diff.LinkName}\": " + string.Join(", ", cnt.Select(kv => $"{kv.Key} {kv.Value}"))
                    + (split > 0 ? $" (chia {split} thanh tại nút giao)" : "") + "."
                    + (warn.Count > 0 ? " Lưu ý: " + string.Join(" ", warn.Distinct().Take(3)) : "");
        return r;
    }

    private static WallType NearestWallType(Project p, double t)
    {
        if (p.WallTypes == null || p.WallTypes.Count == 0) return null;
        if (t <= 0) t = 0.11;
        return p.WallTypes.OrderBy(w => Math.Abs(w.Thickness - t)).First();
    }

    /// <summary>Tiết diện dầm mới: tiết diện dầm phổ biến nhất của tầng, chưa có → b theo bản vẽ (mặc định 220) × 350.</summary>
    private static string BeamSection(Project p, string story, double width)
    {
        var common = ModelEdit.StoryBeams(p, story).GroupBy(f => f.Section).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        if (!string.IsNullOrEmpty(common) && p.FrameSections.Any(s => s.Name == common)) return common;
        double b = width > 0.05 ? ImportGeometry.RoundMm(width, 10) : 0.22;
        return ImportGeometry.EnsureRect(p, "D", b, Math.Max(0.3, ImportGeometry.RoundMm(b * 1.6, 50)), p.Concrete?.ConcreteBeam ?? "B25");
    }

    private static FrameElement AddColumn(Project p, Story st, CadDiffItem it)
    {
        var below = p.Stories.Where(s => s.Elevation < st.Elevation - 1e-6).OrderByDescending(s => s.Elevation).FirstOrDefault();
        double zb = below?.Elevation ?? p.BaseElevation;
        if (zb >= st.Elevation - 1e-6) zb = st.Elevation - Math.Max(st.Height, 3.0);
        double b, h;
        if (it.Diameter > 0) { b = h = Math.Max(0.2, ImportGeometry.RoundMm(ArchPlanReader.RoundColumnSide(it.Diameter), 10)); }
        else
        {
            b = Math.Max(0.15, ImportGeometry.RoundMm(Math.Min(it.Bx, it.By), 10));
            h = Math.Max(b, ImportGeometry.RoundMm(Math.Max(it.Bx, it.By), 10));
        }
        string sec = ImportGeometry.EnsureRect(p, "C", b, h, p.Concrete?.ConcreteColumn ?? "B25");
        double x = Math.Round(it.X1, 4), y = Math.Round(it.Y1, 4);
        var a = p.GetOrAddNode(x, y, zb, below?.Name ?? "Base");
        var t = p.GetOrAddNode(x, y, st.Elevation, st.Name);
        if (below == null && !a.HasRestraint) a.Restraint = Node.Fixed;
        int k = 1;
        foreach (var f in p.Frames.Where(q => q.IsVerticalKind))
        {
            var m = Regex.Match(f.Label ?? "", @"^C(\d+)$");
            if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) k = Math.Max(k, n + 1);
        }
        bool longX = it.Bx >= it.By;
        return ImportGeometry.AddFrame(p, a, t, FrameKind.Column, sec, st.Name, $"C{k}", $"C{k}", (longX ? 0 : 90) + it.Angle);
    }
}
