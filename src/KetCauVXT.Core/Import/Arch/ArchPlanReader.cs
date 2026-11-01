using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Arch;

/// <summary>Đọc bản vẽ kiến trúc sơ bộ (DXF/DWG), tách các mặt bằng tầng, nhận diện trục, tường, cột, phòng, cầu thang, thông tầng.
/// Quy ước nhận dạng (sửa được trong ArchImportOptions):
/// <list type="bullet">
/// <item>Tường: các nét trên lớp tường; 2 nét song song cách nhau 80–450 mm → tường có chiều dày bằng khoảng cách; khe hở ≤ 1,6 m giữa
///   hai đoạn tường thẳng hàng coi là lỗ cửa.</item>
/// <item>Trục: nét trên lớp trục hoặc kiểu nét CENTER/DASHDOT; tên trục lấy chữ (hoặc thuộc tính block) gần đầu trục.
///   Không có trục → tự lập trục theo tim tường chính.</item>
/// <item>Nhiều mặt bằng trên một bản vẽ: tách theo cụm tường; tên tầng đọc từ chữ "MẶT BẰNG TẦNG …" gần hình.
///   Các tầng được dời về cùng hệ toạ độ bằng cách khớp tên trục (hoặc góc trái dưới).</item>
/// <item>Phòng: chữ ghi tên phòng → công năng (RoomCatalog). Cầu thang: lớp cầu thang hoặc chữ "CẦU THANG/LÊN".
///   Thông tầng: chữ "THÔNG TẦNG/Ô THOÁNG/GIẾNG TRỜI".</item>
/// </list></summary>
public static class ArchPlanReader
{
    /// <summary>Đọc file .dxf (hoặc .dwg nếu máy có ODA File Converter). Mỗi file có thể chứa nhiều mặt bằng tầng đặt cạnh nhau.</summary>
    public static ArchDrawing Read(string path, ArchImportOptions o) => ReadMany(new[] { path }, o);

    /// <summary>Đọc nhiều file (mỗi file 1 hoặc nhiều mặt bằng) rồi gộp, sắp theo số tầng.</summary>
    public static ArchDrawing ReadMany(IEnumerable<string> paths, ArchImportOptions o)
    {
        o ??= new ArchImportOptions();
        var d = new ArchDrawing();
        var files = paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        d.Source = string.Join(", ", files.Select(Path.GetFileName));
        var raw = new List<ArchFloorPlan>();
        foreach (var f in files)
        {
            DxfReader dxf;
            if (Path.GetExtension(f).Equals(".dwg", StringComparison.OrdinalIgnoreCase))
            {
                dxf = DwgConverter.ReadAny(f, o.ScaleOverride, out string err);
                if (dxf == null) { d.Warnings.Add(Path.GetFileName(f) + ": " + err); continue; }
            }
            else dxf = DxfReader.Read(f, o.ScaleOverride);
            d.Warnings.AddRange(dxf.Warnings.Select(w => Path.GetFileName(f) + ": " + w));
            d.Info.Add($"{Path.GetFileName(f)}: {dxf.Entities.Count} thực thể, $INSUNITS = {dxf.InsUnits} → ×{dxf.Scale.ToString(CultureInfo.InvariantCulture)} (đơn vị bản vẽ → m)");
            var plans = SplitPlans(dxf.Entities, o, d, Path.GetFileNameWithoutExtension(f), files.Count == 1);
            raw.AddRange(plans);
        }
        Finish(d, raw, o);
        return d;
    }

    /// <summary>Đọc từ danh sách thực thể đã có (dùng cho kiểm thử).</summary>
    public static ArchDrawing FromEntities(List<DxfEntity> ents, ArchImportOptions o, string source = "")
    {
        o ??= new ArchImportOptions();
        var d = new ArchDrawing { Source = source };
        var plans = SplitPlans(ents, o, d, source, true);
        Finish(d, plans, o);
        return d;
    }

    // ==================================================================
    // 1. TÁCH CÁC MẶT BẰNG
    // ==================================================================
    private static List<ArchFloorPlan> SplitPlans(List<DxfEntity> ents, ArchImportOptions o, ArchDrawing d, string fileTag, bool singleFile)
    {
        var res = new List<ArchFloorPlan>();
        foreach (var e in ents.Where(e => e.Type == DxfEntityType.Text)) e.Text = Tcvn3.Fix(e.Text);
        var wallEnts = ents.Where(e => e.Type != DxfEntityType.Text && Match(e.Layer, o.WallLayers)).ToList();
        if (wallEnts.Count == 0)
        {
            d.Warnings.Add($"{fileTag}: không tìm thấy nét trên lớp tường ({o.WallLayers}). Kiểm tra tên lớp trong mục \"Thông số\" (vd. lớp tường của văn phòng là \"KT-TUONG\" thì thêm \"KT-TUONG\").");
            return res;
        }
        var wallSegs = Segments(wallEnts).Where(s => s.Length >= 0.05).ToList();

        // ---- cụm tường (union-find trên lưới băm 2 m) ----
        int n = wallSegs.Count;
        var parent = Enumerable.Range(0, n).ToArray();
        int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[b] = a; }
        const double gap = 1.2, cell = 2.0;
        var hash = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < n; i++)
        {
            var s = wallSegs[i];
            int cx0 = (int)Math.Floor((Math.Min(s.Ax, s.Bx) - gap) / cell), cx1 = (int)Math.Floor((Math.Max(s.Ax, s.Bx) + gap) / cell);
            int cy0 = (int)Math.Floor((Math.Min(s.Ay, s.By) - gap) / cell), cy1 = (int)Math.Floor((Math.Max(s.Ay, s.By) + gap) / cell);
            for (int cx = cx0; cx <= cx1; cx++)
                for (int cy = cy0; cy <= cy1; cy++)
                {
                    if (!hash.TryGetValue((cx, cy), out var lst)) hash[(cx, cy)] = lst = new List<int>();
                    foreach (int j in lst)
                        if (Find(i) != Find(j) && SegDist(s, wallSegs[j]) <= gap) Union(i, j);
                    lst.Add(i);
                }
        }
        var clusters = Enumerable.Range(0, n).GroupBy(Find)
            .Select(g => g.Select(i => wallSegs[i]).ToList())
            .Where(g => g.Sum(s => s.Length) >= 8.0)
            .Select(g => (segs: g, x0: g.Min(s => Math.Min(s.Ax, s.Bx)), y0: g.Min(s => Math.Min(s.Ay, s.By)),
                              x1: g.Max(s => Math.Max(s.Ax, s.Bx)), y1: g.Max(s => Math.Max(s.Ay, s.By))))
            .ToList();
        // gộp cụm lồng nhau / chồng lấn (vd. tường rời trong nhà)
        bool merged = true;
        while (merged)
        {
            merged = false;
            for (int i = 0; i < clusters.Count && !merged; i++)
                for (int j = i + 1; j < clusters.Count && !merged; j++)
                {
                    var a = clusters[i]; var b = clusters[j];
                    double ox = Math.Min(a.x1, b.x1) - Math.Max(a.x0, b.x0), oy = Math.Min(a.y1, b.y1) - Math.Max(a.y0, b.y0);
                    double areaB = (b.x1 - b.x0) * (b.y1 - b.y0), areaA = (a.x1 - a.x0) * (a.y1 - a.y0);
                    if (ox > 0 && oy > 0 && ox * oy > 0.5 * Math.Min(areaA, areaB))
                    {
                        clusters[i] = (a.segs.Concat(b.segs).ToList(), Math.Min(a.x0, b.x0), Math.Min(a.y0, b.y0), Math.Max(a.x1, b.x1), Math.Max(a.y1, b.y1));
                        clusters.RemoveAt(j); merged = true;
                    }
                }
        }

        var texts = ents.Where(e => e.Type == DxfEntityType.Text).ToList();
        var used = new HashSet<DxfEntity>();
        var plans = new List<(ArchFloorPlan plan, double x0, double y0, double x1, double y1)>();
        foreach (var c in clusters.OrderBy(c => -c.y1).ThenBy(c => c.x0))
        {
            var title = FindTitle(texts, c.x0, c.y0, c.x1, c.y1, used);
            string norm = title == null ? "" : Norm(title.Text);
            if (Regex.IsMatch(norm, @"\b(MAT DUNG|MAT CAT|CHI TIET|PHOI CANH|TONG MAT BANG|MAT BANG (MONG|DINH VI|KET CAU))\b")) continue;
            var plan = new ArchFloorPlan { Title = title?.Text ?? "", X0 = c.x0, Y0 = c.y0, X1 = c.x1, Y1 = c.y1 };
            ParseFloorNo(plan, norm);
            plans.Add((plan, c.x0, c.y0, c.x1, c.y1));
        }
        if (plans.Count == 0) { d.Warnings.Add($"{fileTag}: không tách được mặt bằng nào (tổng chiều dài tường mỗi cụm < 8 m)."); return res; }

        // số tầng chưa đọc được → đánh theo thứ tự (trái → phải, trên → dưới)
        if (plans.Any(p => p.plan.FloorNo == 0))
        {
            int next = plans.Where(p => p.plan.FloorNo > 0 && !p.plan.IsRoofPlan && !p.plan.IsTum).Select(p => p.plan.FloorNo).DefaultIfEmpty(0).Max() + 1;
            var ordered = plans.Where(p => p.plan.FloorNo == 0).OrderBy(p => Math.Round(-p.y1 / 10)).ThenBy(p => p.x0).ToList();
            foreach (var p in ordered)
            {
                p.plan.FloorNo = next++;
                if (string.IsNullOrEmpty(p.plan.Title)) p.plan.Title = $"{fileTag} – mặt bằng {p.plan.FloorNo}";
                p.plan.Notes.Add("Không đọc được tên tầng – đánh số theo vị trí hình trên bản vẽ, kiểm tra lại.");
            }
            if (!singleFile || ordered.Count > 0)
                d.Warnings.Add($"{fileTag}: {ordered.Count} mặt bằng không có tên \"MẶT BẰNG TẦNG …\" – số tầng gán theo thứ tự, cần kiểm tra.");
        }

        foreach (var (plan, x0, y0, x1, y1) in plans)
        {
            // phạm vi lấy thực thể: bao tường + 3 m (trục, ký hiệu trục nằm ngoài tường)
            double m = 3.0;
            var inEnts = ents.Where(e => e.Points.Count > 0 && e.Points.Any(p => p.X >= x0 - m && p.X <= x1 + m && p.Y >= y0 - m && p.Y <= y1 + m)).ToList();
            Recognize(plan, inEnts, x0, y0, x1, y1, o);
            res.Add(plan);
        }
        return res;
    }

    private static DxfEntity FindTitle(List<DxfEntity> texts, double x0, double y0, double x1, double y1, HashSet<DxfEntity> used)
    {
        double h = y1 - y0, w = x1 - x0;
        double reach = Math.Max(4.0, 0.35 * h);
        DxfEntity best = null; double bestScore = double.MaxValue;
        foreach (var t in texts)
        {
            if (used.Contains(t)) continue;
            string s = Norm(t.Text);
            if (!(s.Contains("MAT BANG") || Regex.IsMatch(s, @"^\s*(MB\b|TANG\b|TUM\b)") || s.Contains("MAT DUNG") || s.Contains("MAT CAT"))) continue;
            var (px, py) = t.Points[0];
            if (px < x0 - 0.3 * w || px > x1 + 0.3 * w) continue;
            double dy = py < y0 ? y0 - py : py > y1 ? py - y1 : -1;
            if (dy < 0 || dy > reach) continue;
            double score = dy + Math.Abs(px - (x0 + x1) / 2) * 0.2 - (py < y0 ? 0.5 : 0);   // ưu tiên chữ ngay dưới hình
            if (score < bestScore) { bestScore = score; best = t; }
        }
        if (best != null) used.Add(best);
        return best;
    }

    private static void ParseFloorNo(ArchFloorPlan p, string norm)
    {
        if (string.IsNullOrEmpty(norm)) return;
        if (Regex.IsMatch(norm, @"\bTANG HAM\b")) { p.Notes.Add("Tầng hầm chưa được hỗ trợ – bỏ qua."); p.Include = false; p.FloorNo = -1; return; }
        if (Regex.IsMatch(norm, @"\bTUM\b")) { p.IsTum = true; p.FloorNo = 90; return; }
        if (Regex.IsMatch(norm, @"\bMAI\b") && !Regex.IsMatch(norm, @"\bTANG\s+\d")) { p.IsRoofPlan = true; p.FloorNo = 99; return; }
        if (Regex.IsMatch(norm, @"\bTANG (TRET|1|MOT)\b") || Regex.IsMatch(norm, @"\bTRET\b")) { p.FloorNo = 1; return; }
        if (Regex.IsMatch(norm, @"\bLUNG\b")) { p.FloorNo = 0; p.Notes.Add("Tầng lửng – nhập số tầng thủ công."); return; }
        var m = Regex.Match(norm, @"\bTANG\s+(\d{1,2})\b");
        if (m.Success) { p.FloorNo = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); return; }
        m = Regex.Match(norm, @"\bTANG\s+(HAI|BA|BON|NAM|SAU|BAY)\b");
        if (m.Success) p.FloorNo = Array.IndexOf(new[] { "", "MOT", "HAI", "BA", "BON", "NAM", "SAU", "BAY" }, m.Groups[1].Value);
    }

    // ==================================================================
    // 2. NHẬN DIỆN MỘT MẶT BẰNG
    // ==================================================================
    private static void Recognize(ArchFloorPlan plan, List<DxfEntity> ents, double bx0, double by0, double bx1, double by1, ArchImportOptions o)
    {
        bool InBox(double x, double y, double m = 0.3) => x >= bx0 - m && x <= bx1 + m && y >= by0 - m && y <= by1 + m;

        // ---- nền xem trước ----
        foreach (var e in ents.Where(e => e.Type != DxfEntityType.Text))
        {
            int kind = Match(e.Layer, o.WallLayers) ? 1 : IsGridEntity(e, o) ? 2 : Match(e.Layer, o.ColumnLayers) ? 3
                     : Match(e.Layer, o.DoorLayers) || IsDoorBlock(e.Block) ? 4 : Match(e.Layer, o.StairLayers) ? 5 : 0;
            if (e.Type == DxfEntityType.Circle)
            {
                var (cx, cy) = e.Points[0];
                for (int k = 0; k < 12; k++)
                {
                    double a0 = k * Math.PI / 6, a1 = (k + 1) * Math.PI / 6;
                    plan.Preview.Add(new PreviewLine(cx + e.Radius * Math.Cos(a0), cy + e.Radius * Math.Sin(a0), cx + e.Radius * Math.Cos(a1), cy + e.Radius * Math.Sin(a1), kind));
                }
                continue;
            }
            foreach (var s in Segments(new[] { e })) plan.Preview.Add(new PreviewLine(s.Ax, s.Ay, s.Bx, s.By, kind));
        }

        // ---- tường ----
        var wallSegs = Segments(ents.Where(e => e.Type != DxfEntityType.Text && Match(e.Layer, o.WallLayers)))
            .Where(s => s.Length >= 0.05 && InBox((s.Ax + s.Bx) / 2, (s.Ay + s.By) / 2)).ToList();
        int diag = 0;
        var hx = new List<(double c, double a, double b)>();   // nét ngang: y, x đầu, x cuối
        var vy = new List<(double c, double a, double b)>();   // nét đứng: x, y đầu, y cuối
        foreach (var s in wallSegs)
        {
            double dx = s.Bx - s.Ax, dy = s.By - s.Ay;
            double ang = Math.Abs(Math.Atan2(dy, dx) * 180 / Math.PI) % 180;
            if (ang < 1 || ang > 179) hx.Add(((s.Ay + s.By) / 2, Math.Min(s.Ax, s.Bx), Math.Max(s.Ax, s.Bx)));
            else if (Math.Abs(ang - 90) < 1) vy.Add(((s.Ax + s.Bx) / 2, Math.Min(s.Ay, s.By), Math.Max(s.Ay, s.By)));
            else if (s.Length > 0.5) diag++;
        }
        if (diag > 0) plan.Notes.Add($"{diag} nét tường xiên/cong – chưa hỗ trợ, bỏ qua (thêm dầm thủ công nếu cần).");
        var walls = new List<ArchWall>();
        walls.AddRange(PairWalls(hx, true, o));
        walls.AddRange(PairWalls(vy, false, o));
        if (walls.Count == 0 && wallSegs.Count > 0)
        {
            // bản vẽ tường 1 nét
            foreach (var (c, a, b) in hx.Where(t => t.b - t.a >= 1.0)) walls.Add(new ArchWall { Ax = a, Ay = c, Bx = b, By = c, Thickness = o.DefaultWallThickness });
            foreach (var (c, a, b) in vy.Where(t => t.b - t.a >= 1.0)) walls.Add(new ArchWall { Ax = c, Ay = a, Bx = c, By = b, Thickness = o.DefaultWallThickness });
            plan.Notes.Add($"Tường vẽ 1 nét – lấy chiều dày {o.DefaultWallThickness * 1000:0} mm.");
        }
        walls = MergeWalls(walls, o).Where(w => w.Length >= o.MinWallLength).ToList();

        // ---- cột kiến trúc ----
        foreach (var e in ents.Where(e => e.Type == DxfEntityType.Polyline && e.Closed && Match(e.Layer, o.ColumnLayers)))
        {
            double x0 = e.Points.Min(p => p.X), x1 = e.Points.Max(p => p.X), y0 = e.Points.Min(p => p.Y), y1 = e.Points.Max(p => p.Y);
            double w = x1 - x0, h = y1 - y0;
            if (w < 0.12 || h < 0.12 || w > 1.0 || h > 1.0) continue;
            double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
            if (!InBox(cx, cy)) continue;
            if (plan.Columns.Any(c => Math.Abs(c.X - cx) < 0.05 && Math.Abs(c.Y - cy) < 0.05)) continue;
            plan.Columns.Add(new ArchColumn { X = cx, Y = cy, Bx = Math.Round(w, 3), By = Math.Round(h, 3) });
        }

        // ---- trục ----
        var texts = ents.Where(e => e.Type == DxfEntityType.Text).ToList();
        var gridSegs = Segments(ents.Where(e => e.Type != DxfEntityType.Text && IsGridEntity(e, o))).ToList();
        double minLen = Math.Max(2.5, 0.35 * Math.Min(bx1 - bx0, by1 - by0));
        var gx = new List<(double c, Seg2 s)>(); var gy = new List<(double c, Seg2 s)>();
        foreach (var s in gridSegs.Where(s => s.Length >= minLen))
        {
            if (s.IsAlongY(0.01) && s.Ax > bx0 - 0.5 && s.Ax < bx1 + 0.5) gx.Add(((s.Ax + s.Bx) / 2, s));
            else if (s.IsAlongX(0.01) && s.Ay > by0 - 0.5 && s.Ay < by1 + 0.5) gy.Add(((s.Ay + s.By) / 2, s));
        }
        plan.Grids.AddRange(GridsFrom(gx, GridDir.X, texts));
        plan.Grids.AddRange(GridsFrom(gy, GridDir.Y, texts));
        if (plan.Grids.Count(g => g.Dir == GridDir.X) < 2 || plan.Grids.Count(g => g.Dir == GridDir.Y) < 2)
        {
            plan.Grids.Clear();
            plan.Grids.AddRange(GridsFromWalls(walls, GridDir.X));
            plan.Grids.AddRange(GridsFromWalls(walls, GridDir.Y));
            plan.Notes.Add("Không đọc được lưới trục (lớp " + o.GridLayers + ") – tự lập trục theo tim tường chính, kiểm tra lại.");
        }

        // ---- lỗ cửa ----
        var doorEnts = ents.Where(e => e.Type != DxfEntityType.Text && (Match(e.Layer, o.DoorLayers) || IsDoorBlock(e.Block))
                                       && e.Points.Count > 0 && InBox(e.Points[0].X, e.Points[0].Y)).ToList();
        foreach (var cl in ClusterBoxes(doorEnts, 0.08))
        {
            double w = Math.Max(cl.x1 - cl.x0, cl.y1 - cl.y0);
            if (w < 0.5 || w > 4.0) continue;
            bool isDoor = cl.ents.Any(e => e.Type == DxfEntityType.Polyline && e.Points.Count >= 4 && !e.Closed) || cl.ents.Any(e => Norm(e.Block).Contains("DOOR") || Regex.IsMatch(Norm(e.Block), @"(^|[^A-Z])(D|CD|CUA ?DI)\d"));
            plan.Openings.Add(new ArchOpening { X = (cl.x0 + cl.x1) / 2, Y = (cl.y0 + cl.y1) / 2, Width = Math.Round(w, 2), Kind = isDoor ? "Cửa đi" : "Cửa sổ" });
        }
        foreach (var op in plan.Openings)
        {
            ArchWall best = null; double bd = double.MaxValue;
            foreach (var w in walls)
            {
                var (t, dist) = ImportGeometry.ProjectPoint(new Seg2(w.Ax, w.Ay, w.Bx, w.By), op.X, op.Y);
                if (t < -0.02 || t > 1.02 || dist > w.Thickness / 2 + 0.25) continue;
                if (dist < bd) { bd = dist; best = w; }
            }
            if (best != null && !InGap(best, op.X, op.Y)) best.OpeningLength += op.Width;
        }
        plan.Walls.AddRange(walls);

        // ---- phòng, cầu thang, thông tầng ----
        foreach (var t in texts)
        {
            var (px, py) = t.Points[0];
            if (!InBox(px, py, -0.05)) continue;
            string s = Norm(t.Text);
            if (s.Length == 0 || s.Length > 40 || s.Contains("MAT BANG")) continue;
            var rt = Classify(s, o.Rooms);
            if (rt == null) continue;
            plan.Rooms.Add(new ArchRoom { Text = t.Text, Kind = rt.Kind, X = px, Y = py });
            if (rt.Kind == RoomKind.Void) plan.Zones.Add(new ArchZone { Kind = RoomKind.Void, X0 = px, Y0 = py, X1 = px, Y1 = py, Note = "từ chữ \"" + t.Text + "\"" });
        }
        var stairEnts = ents.Where(e => e.Type != DxfEntityType.Text && Match(e.Layer, o.StairLayers) && e.Points.Count > 0 && InBox(e.Points[0].X, e.Points[0].Y)).ToList();
        foreach (var cl in ClusterBoxes(stairEnts, 0.35))
        {
            double w = cl.x1 - cl.x0, h = cl.y1 - cl.y0;
            if (w * h < 1.5 || w < 0.8 || h < 0.8) continue;
            plan.Zones.Add(new ArchZone { Kind = RoomKind.Stair, X0 = cl.x0, Y0 = cl.y0, X1 = cl.x1, Y1 = cl.y1, Note = "lớp cầu thang" });
        }
        if (!plan.Zones.Any(z => z.Kind == RoomKind.Stair))
            foreach (var r in plan.Rooms.Where(r => r.Kind == RoomKind.Stair))
                plan.Zones.Add(new ArchZone { Kind = RoomKind.Stair, X0 = r.X, Y0 = r.Y, X1 = r.X, Y1 = r.Y, Note = "từ chữ \"" + r.Text + "\"" });

        // ---- đường bao sàn ----
        plan.Outline = Footprint(walls, plan.Columns);
        foreach (var w in walls) w.Exterior = OnOutline(plan.Outline, w);
        AddBalconies(plan, ents, o);
    }

    // ------------------------------------------------------------------
    // Ghép nét thành tường
    // ------------------------------------------------------------------
    private static readonly double[] StdThick = { 0.10, 0.11, 0.15, 0.20, 0.22, 0.25, 0.30, 0.33 };

    private static IEnumerable<ArchWall> PairWalls(List<(double c, double a, double b)> lines, bool horiz, ArchImportOptions o)
    {
        var sorted = lines.OrderBy(l => l.c).ToList();
        var used = new HashSet<(int, int)>();
        for (int i = 0; i < sorted.Count; i++)
        {
            var li = sorted[i];
            int best = -1; double bestScore = double.MaxValue;
            for (int dir = -1; dir <= 1; dir += 2)
                for (int j = i + dir; j >= 0 && j < sorted.Count; j += dir)
                {
                    var lj = sorted[j];
                    double d = Math.Abs(lj.c - li.c);
                    if (d > o.MaxWallThickness) break;
                    if (d < o.MinWallThickness) continue;
                    double ov = Math.Min(li.b, lj.b) - Math.Max(li.a, lj.a);
                    if (ov < Math.Min(0.3, 0.5 * Math.Min(li.b - li.a, lj.b - lj.a))) continue;
                    double score = StdThick.Min(t => Math.Abs(t - d)) - 0.001 * d;
                    if (score < bestScore) { bestScore = score; best = j; }
                }
            if (best < 0) continue;
            var key = (Math.Min(i, best), Math.Max(i, best));
            if (!used.Add(key)) continue;
            var lb = sorted[best];
            double a = Math.Max(li.a, lb.a), b = Math.Min(li.b, lb.b), c = (li.c + lb.c) / 2, t = Math.Round(Math.Abs(lb.c - li.c), 3);
            yield return horiz ? new ArchWall { Ax = a, Ay = c, Bx = b, By = c, Thickness = t } : new ArchWall { Ax = c, Ay = a, Bx = c, By = b, Thickness = t };
        }
    }

    /// <summary>Gộp tường thẳng hàng: chồng lấn → hợp; khe ≤ 1,6 m → coi là lỗ cửa.</summary>
    private static List<ArchWall> MergeWalls(List<ArchWall> walls, ArchImportOptions o)
    {
        var res = new List<ArchWall>();
        foreach (bool horiz in new[] { true, false })
        {
            var grp = walls.Where(w => horiz ? w.AlongX : w.AlongY)
                .Select(w => (c: horiz ? w.Ay : w.Ax, a: horiz ? Math.Min(w.Ax, w.Bx) : Math.Min(w.Ay, w.By), b: horiz ? Math.Max(w.Ax, w.Bx) : Math.Max(w.Ay, w.By), t: w.Thickness))
                .OrderBy(w => w.c).ThenBy(w => w.a).ToList();
            var lines = new List<List<(double c, double a, double b, double t)>>();
            foreach (var w in grp)
            {
                var line = lines.FirstOrDefault(l => Math.Abs(l[0].c - w.c) < 0.03 && Math.Abs(l[0].t - w.t) < 0.03);
                if (line == null) lines.Add(new List<(double c, double a, double b, double t)> { w }); else line.Add(w);
            }
            foreach (var line in lines)
            {
                double c = line.Average(l => l.c), t = line.Max(l => l.t);
                var iv = line.OrderBy(l => l.a).ToList();
                double a = iv[0].a, b = iv[0].b, openings = 0;
                var gaps = new List<(double, double)>();
                void Emit()
                {
                    var w = horiz ? new ArchWall { Ax = a, Ay = c, Bx = b, By = c, Thickness = t } : new ArchWall { Ax = c, Ay = a, Bx = c, By = b, Thickness = t };
                    w.OpeningLength = openings;
                    GapStore.AddOrUpdate(w, new List<(double, double)>(gaps));
                    res.Add(w);
                }
                for (int k = 1; k < iv.Count; k++)
                {
                    double g = iv[k].a - b;
                    if (g <= 0.02) { b = Math.Max(b, iv[k].b); continue; }
                    if (g <= 1.6) { openings += g; gaps.Add((b, iv[k].a)); b = Math.Max(b, iv[k].b); continue; }
                    Emit(); a = iv[k].a; b = iv[k].b; openings = 0; gaps.Clear();
                }
                Emit();
            }
        }
        return res;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ArchWall, List<(double, double)>> GapStore = new();

    private static bool InGap(ArchWall w, double x, double y)
    {
        if (!GapStore.TryGetValue(w, out var gaps)) return false;
        double s = w.AlongX ? x : y;
        return gaps.Any(g => s >= g.Item1 - 0.1 && s <= g.Item2 + 0.1);
    }

    // ------------------------------------------------------------------
    // Trục
    // ------------------------------------------------------------------
    private static List<ArchGrid> GridsFrom(List<(double c, Seg2 s)> lines, GridDir dir, List<DxfEntity> texts)
    {
        var res = new List<ArchGrid>();
        var groups = new List<List<(double c, Seg2 s)>>();
        foreach (var l in lines.OrderBy(l => l.c))
        {
            if (groups.Count > 0 && Math.Abs(groups[^1][^1].c - l.c) <= 0.02) groups[^1].Add(l);
            else groups.Add(new List<(double c, Seg2 s)> { l });
        }
        foreach (var g in groups)
        {
            double c = g.Average(x => x.c);
            string name = null;
            foreach (var (_, s) in g)
            {
                foreach (var (ex, ey) in new[] { (s.Ax, s.Ay), (s.Bx, s.By) })
                {
                    var t = texts.Where(t => t.Text.Trim().Length is > 0 and <= 4 && Math.Abs(t.Points[0].X - ex) < 1.5 && Math.Abs(t.Points[0].Y - ey) < 1.5
                                             && Regex.IsMatch(t.Text.Trim(), @"^[A-Za-z0-9'’]{1,4}$"))
                        .OrderBy(t => Math.Abs(t.Points[0].X - ex) + Math.Abs(t.Points[0].Y - ey)).FirstOrDefault();
                    if (t != null) { name = t.Text.Trim().ToUpperInvariant(); break; }
                }
                if (name != null) break;
            }
            res.Add(new ArchGrid { Name = name ?? "", Dir = dir, Coord = Math.Round(c, 4) });
        }
        if (res.Any(r => r.Name == "") || res.Select(r => r.Name).Distinct().Count() != res.Count)
            for (int i = 0; i < res.Count; i++) res[i].Name = ImportGeometry.AutoName(dir, i);
        return res;
    }

    private static List<ArchGrid> GridsFromWalls(List<ArchWall> walls, GridDir dir)
    {
        // trục X (đường thẳng đứng x = c) lấy từ tường dọc phương Y, ưu tiên tường dày / dài
        var cand = walls.Where(w => dir == GridDir.X ? w.AlongY : w.AlongX)
            .Select(w => (c: dir == GridDir.X ? w.Ax : w.Ay, len: w.Length, t: w.Thickness)).ToList();
        if (cand.Count == 0) return new List<ArchGrid>();
        double lo = cand.Min(c => c.c), hi = cand.Max(c => c.c);
        var groups = new List<List<(double c, double len, double t)>>();
        foreach (var c in cand.OrderBy(c => c.c))
        {
            if (groups.Count > 0 && c.c - groups[^1][^1].c <= 0.15) groups[^1].Add(c);
            else groups.Add(new List<(double c, double len, double t)> { c });
        }
        var res = new List<ArchGrid>();
        foreach (var g in groups)
        {
            double c = g.Sum(x => x.c * x.len) / g.Sum(x => x.len), len = g.Sum(x => x.len);
            bool edge = Math.Abs(c - lo) < 0.3 || Math.Abs(c - hi) < 0.3;
            if (edge || (len >= 2.5 && g.Max(x => x.t) >= 0.18) || len >= 4.0)
                res.Add(new ArchGrid { Dir = dir, Coord = Math.Round(c, 3) });
        }
        for (int i = 0; i < res.Count; i++) res[i].Name = ImportGeometry.AutoName(dir, i);
        return res;
    }

    // ------------------------------------------------------------------
    // Đường bao sàn: ô lưới theo toạ độ mép tường, ô "trong nhà" khi tia 4 hướng gặp tường (≥ 3 hướng: chịu được khe cửa lớn ở mặt tiền)
    // ------------------------------------------------------------------
    private static List<(double X, double Y)> Footprint(List<ArchWall> walls, List<ArchColumn> cols)
    {
        if (walls.Count == 0) return new List<(double X, double Y)>();
        var xs = ImportGeometry.Cluster(walls.SelectMany(w => w.AlongY ? new[] { w.Ax - w.Thickness / 2, w.Ax + w.Thickness / 2 } : new[] { w.Ax, w.Bx }), 0.03);
        var ys = ImportGeometry.Cluster(walls.SelectMany(w => w.AlongX ? new[] { w.Ay - w.Thickness / 2, w.Ay + w.Thickness / 2 } : new[] { w.Ay, w.By }), 0.03);
        int nx = xs.Count - 1, ny = ys.Count - 1;
        if (nx < 1 || ny < 1) return new List<(double X, double Y)>();
        var inside = new bool[nx, ny];
        bool HitX(double cx, double cy, int sgn) => walls.Any(w => w.AlongY && (sgn > 0 ? w.Ax > cx : w.Ax < cx) && cy >= Math.Min(w.Ay, w.By) - 0.02 && cy <= Math.Max(w.Ay, w.By) + 0.02)
                                                  || cols.Any(c => (sgn > 0 ? c.X > cx : c.X < cx) && Math.Abs(c.Y - cy) <= c.By / 2);
        bool HitY(double cx, double cy, int sgn) => walls.Any(w => w.AlongX && (sgn > 0 ? w.Ay > cy : w.Ay < cy) && cx >= Math.Min(w.Ax, w.Bx) - 0.02 && cx <= Math.Max(w.Ax, w.Bx) + 0.02)
                                                  || cols.Any(c => (sgn > 0 ? c.Y > cy : c.Y < cy) && Math.Abs(c.X - cx) <= c.Bx / 2);
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                double cx = (xs[i] + xs[i + 1]) / 2, cy = (ys[j] + ys[j + 1]) / 2;
                int hits = (HitX(cx, cy, 1) ? 1 : 0) + (HitX(cx, cy, -1) ? 1 : 0) + (HitY(cx, cy, 1) ? 1 : 0) + (HitY(cx, cy, -1) ? 1 : 0);
                inside[i, j] = hits == 4;
            }
        // lần 2: ô có 3 hướng gặp tường và kề ô trong nhà (khe cửa cuốn mặt tiền)
        var add = new List<(int, int)>();
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                if (inside[i, j]) continue;
                double cx = (xs[i] + xs[i + 1]) / 2, cy = (ys[j] + ys[j + 1]) / 2;
                int hits = (HitX(cx, cy, 1) ? 1 : 0) + (HitX(cx, cy, -1) ? 1 : 0) + (HitY(cx, cy, 1) ? 1 : 0) + (HitY(cx, cy, -1) ? 1 : 0);
                bool nb = (i > 0 && inside[i - 1, j]) || (i + 1 < nx && inside[i + 1, j]) || (j > 0 && inside[i, j - 1]) || (j + 1 < ny && inside[i, j + 1]);
                if (hits == 3 && nb) add.Add((i, j));
            }
        foreach (var (i, j) in add) inside[i, j] = true;
        return TraceOutline(xs, ys, inside);
    }

    /// <summary>Lấy biên ngoài lớn nhất của tập ô (đa giác trực giao, ngược chiều kim đồng hồ).</summary>
    internal static List<(double X, double Y)> TraceOutline(List<double> xs, List<double> ys, bool[,] inside)
    {
        int nx = xs.Count - 1, ny = ys.Count - 1;
        bool In(int i, int j) => i >= 0 && j >= 0 && i < nx && j < ny && inside[i, j];
        // cạnh biên có hướng (ô trong nằm bên trái)
        var next = new Dictionary<(int, int), List<(int, int)>>();
        void Edge((int, int) a, (int, int) b) { if (!next.TryGetValue(a, out var l)) next[a] = l = new List<(int, int)>(); l.Add(b); }
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                if (!In(i, j)) continue;
                if (!In(i, j - 1)) Edge((i, j), (i + 1, j));
                if (!In(i + 1, j)) Edge((i + 1, j), (i + 1, j + 1));
                if (!In(i, j + 1)) Edge((i + 1, j + 1), (i, j + 1));
                if (!In(i - 1, j)) Edge((i, j + 1), (i, j));
            }
        var loops = new List<List<(int, int)>>();
        while (next.Count > 0)
        {
            var start = next.Keys.First();
            var loop = new List<(int, int)>();
            var cur = start;
            int guard = 0;
            while (guard++ < 100000)
            {
                loop.Add(cur);
                if (!next.TryGetValue(cur, out var lst) || lst.Count == 0) break;
                var nx2 = lst[0]; lst.RemoveAt(0);
                if (lst.Count == 0) next.Remove(cur);
                cur = nx2;
                if (cur == start) break;
            }
            loops.Add(loop);
        }
        var bestLoop = loops.Select(l => l.Select(p => (X: xs[p.Item1], Y: ys[p.Item2])).ToList())
            .OrderByDescending(l => Math.Abs(ImportGeometry.SignedArea(l))).FirstOrDefault() ?? new List<(double X, double Y)>();
        return bestLoop.Count >= 3 ? ImportGeometry.RemoveCollinear(bestLoop, 0.005) : bestLoop;
    }

    internal static bool PointInPoly(List<(double X, double Y)> poly, double x, double y, double tol = 0.0)
    {
        if (poly == null || poly.Count < 3) return false;
        if (tol > 0)
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                if (ImportGeometry.ProjectPoint(new Seg2(a.X, a.Y, b.X, b.Y), x, y) is var pr && pr.t >= -1e-9 && pr.t <= 1 + 1e-9 && pr.dist <= tol) return true;
            }
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var pi = poly[i]; var pj = poly[j];
            if ((pi.Y > y) != (pj.Y > y) && x < (pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y) + pi.X) c = !c;
        }
        return c;
    }

    private static bool OnOutline(List<(double X, double Y)> poly, ArchWall w)
    {
        if (poly.Count < 3) return false;
        double mx = (w.Ax + w.Bx) / 2, my = (w.Ay + w.By) / 2;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            var (t, dist) = ImportGeometry.ProjectPoint(new Seg2(a.X, a.Y, b.X, b.Y), mx, my);
            if (t >= -0.01 && t <= 1.01 && dist <= w.Thickness / 2 + 0.03) return true;
        }
        return false;
    }

    /// <summary>Ban công/lô gia nằm ngoài đường bao: chữ "BAN CÔNG" ngoài nhà → vùng từ mép nhà tới nét xa nhất (≤ 2,5 m).</summary>
    private static void AddBalconies(ArchFloorPlan plan, List<DxfEntity> ents, ArchImportOptions o)
    {
        var poly = plan.Outline;
        if (poly.Count < 3) return;
        double x0 = poly.Min(p => p.X), x1 = poly.Max(p => p.X), y0 = poly.Min(p => p.Y), y1 = poly.Max(p => p.Y);
        foreach (var r in plan.Rooms.Where(r => r.Kind == RoomKind.Balcony))
        {
            if (PointInPoly(poly, r.X, r.Y)) continue;
            // hướng nhô ra
            double reach = Math.Max(o.MaxCantilever, 1.0) + 0.5;
            ArchZone z = null;
            if (r.Y > y1 && r.Y - y1 <= reach) z = new ArchZone { X0 = x0, X1 = x1, Y0 = y1, Y1 = r.Y };
            else if (r.Y < y0 && y0 - r.Y <= reach) z = new ArchZone { X0 = x0, X1 = x1, Y0 = r.Y, Y1 = y0 };
            else if (r.X > x1 && r.X - x1 <= reach) z = new ArchZone { X0 = x1, X1 = r.X, Y0 = y0, Y1 = y1 };
            else if (r.X < x0 && x0 - r.X <= reach) z = new ArchZone { X0 = r.X, X1 = x0, Y0 = y0, Y1 = y1 };
            if (z == null) continue;
            // mở rộng tới nét lan can xa nhất trong dải
            var segs = Segments(ents.Where(e => e.Type != DxfEntityType.Text)).Where(s => s.Length > 0.3).ToList();
            if (z.Y0 == y1)
            {
                var far = segs.Where(s => s.IsAlongX(0.01) && s.Ay > y1 + 0.3 && s.Ay <= y1 + reach && Math.Max(s.Ax, s.Bx) > x0 && Math.Min(s.Ax, s.Bx) < x1).ToList();
                if (far.Count > 0) { z.Y1 = far.Max(s => s.Ay); z.X0 = Math.Max(x0, far.Min(s => Math.Min(s.Ax, s.Bx))); z.X1 = Math.Min(x1, far.Max(s => Math.Max(s.Ax, s.Bx))); }
            }
            else if (z.Y1 == y0)
            {
                var far = segs.Where(s => s.IsAlongX(0.01) && s.Ay < y0 - 0.3 && s.Ay >= y0 - reach && Math.Max(s.Ax, s.Bx) > x0 && Math.Min(s.Ax, s.Bx) < x1).ToList();
                if (far.Count > 0) { z.Y0 = far.Min(s => s.Ay); z.X0 = Math.Max(x0, far.Min(s => Math.Min(s.Ax, s.Bx))); z.X1 = Math.Min(x1, far.Max(s => Math.Max(s.Ax, s.Bx))); }
            }
            else if (z.X0 == x1)
            {
                var far = segs.Where(s => s.IsAlongY(0.01) && s.Ax > x1 + 0.3 && s.Ax <= x1 + reach && Math.Max(s.Ay, s.By) > y0 && Math.Min(s.Ay, s.By) < y1).ToList();
                if (far.Count > 0) { z.X1 = far.Max(s => s.Ax); z.Y0 = Math.Max(y0, far.Min(s => Math.Min(s.Ay, s.By))); z.Y1 = Math.Min(y1, far.Max(s => Math.Max(s.Ay, s.By))); }
            }
            else
            {
                var far = segs.Where(s => s.IsAlongY(0.01) && s.Ax < x0 - 0.3 && s.Ax >= x0 - reach && Math.Max(s.Ay, s.By) > y0 && Math.Min(s.Ay, s.By) < y1).ToList();
                if (far.Count > 0) { z.X0 = far.Min(s => s.Ax); z.Y0 = Math.Max(y0, far.Min(s => Math.Min(s.Ay, s.By))); z.Y1 = Math.Min(y1, far.Max(s => Math.Max(s.Ay, s.By))); }
            }
            z.Kind = RoomKind.Balcony; z.Note = "ban công từ chữ \"" + r.Text + "\"";
            z.X0 = Math.Round(z.X0, 3); z.X1 = Math.Round(z.X1, 3); z.Y0 = Math.Round(z.Y0, 3); z.Y1 = Math.Round(z.Y1, 3);
            if ((z.X1 - z.X0) > 0.5 && (z.Y1 - z.Y0) > 0.5 && !plan.Zones.Any(q => q.Kind == RoomKind.Balcony && Math.Abs(q.X0 - z.X0) < 0.1 && Math.Abs(q.Y0 - z.Y0) < 0.1))
                plan.Zones.Add(z);
        }
    }

    // ==================================================================
    // 3. HOÀN THIỆN: sắp tầng, dời về hệ toạ độ chung, chiều cao tầng
    // ==================================================================
    private static void Finish(ArchDrawing d, List<ArchFloorPlan> plans, ArchImportOptions o)
    {
        plans = plans.OrderBy(p => p.FloorNo < 0 ? 1000 : p.FloorNo).ToList();
        // trùng số tầng (vd. nhiều file) → giữ tất cả nhưng cảnh báo
        foreach (var g in plans.Where(p => p.FloorNo > 0).GroupBy(p => p.FloorNo).Where(g => g.Count() > 1))
            d.Warnings.Add($"Có {g.Count()} mặt bằng cùng số tầng {g.Key}: {string.Join("; ", g.Select(p => p.Title))} – bỏ chọn bớt.");

        var baseP = plans.FirstOrDefault(p => p.Include && p.FloorNo == 1) ?? plans.FirstOrDefault(p => p.Include && !p.IsRoofPlan);
        if (baseP == null) { d.Floors = plans; return; }
        // gốc toạ độ: giao trục X nhỏ nhất – trục Y nhỏ nhất của tầng 1
        double ox0 = baseP.Grids.Where(g => g.Dir == GridDir.X).Select(g => g.Coord).DefaultIfEmpty(baseP.X0).Min();
        double oy0 = baseP.Grids.Where(g => g.Dir == GridDir.Y).Select(g => g.Coord).DefaultIfEmpty(baseP.Y0).Min();
        foreach (var p in plans)
        {
            double dx, dy;
            if (p == baseP) { dx = -ox0; dy = -oy0; }
            else
            {
                var (mx, my) = (MatchOffset(baseP, p, GridDir.X), MatchOffset(baseP, p, GridDir.Y));
                dx = (mx ?? (baseP.X0 - p.X0)) - ox0;
                dy = (my ?? (baseP.Y0 - p.Y0)) - oy0;
                if (mx == null || my == null)
                    p.Notes.Add("Không khớp được tên trục với tầng 1 – căn theo góc trái dưới của tường, kiểm tra lại vị trí.");
            }
            Shift(p, dx, dy);
        }

        // chiều cao tầng: đọc cao độ "+3.600" trong tên hình nếu có
        var elev = new Dictionary<ArchFloorPlan, double>();
        foreach (var p in plans)
        {
            var m = Regex.Match(p.Title.Replace(',', '.'), @"([+\-±]\s?\d{1,2}\.\d{2,3})");
            if (m.Success && double.TryParse(m.Groups[1].Value.Replace("±", "").Replace(" ", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double z)) elev[p] = z;
        }
        var main = plans.Where(p => p.Include && !p.IsRoofPlan && p.FloorNo > 0).ToList();
        for (int i = 0; i < main.Count; i++)
        {
            var p = main[i];
            p.Height = p.IsTum ? o.TumHeight : p.FloorNo == 1 ? o.GroundStoreyHeight : o.TypicalStoreyHeight;
            if (i + 1 < main.Count && elev.TryGetValue(p, out double z0) && elev.TryGetValue(main[i + 1], out double z1) && z1 - z0 > 2.0 && z1 - z0 < 8.0)
            {
                p.Height = Math.Round(z1 - z0, 3);
                p.Notes.Add($"Chiều cao tầng {p.Height:0.000} m lấy theo cao độ ghi trên tên hình.");
            }
        }
        d.Floors = plans;
        d.Info.Add($"Nhận diện {plans.Count} mặt bằng: " + string.Join("; ", plans.Select(p => $"[{(p.IsRoofPlan ? "Mái" : p.IsTum ? "Tum" : "T" + p.FloorNo)}] {p.Title}")));
        foreach (var p in plans)
            d.Info.Add($"  {p.Title}: {p.Walls.Count} đoạn tường ({p.Walls.Sum(w => w.Length):0.0} m), {p.Grids.Count} trục, {p.Columns.Count} cột KT, {p.Rooms.Count} phòng, {p.Openings.Count} cửa, {p.Zones.Count} vùng đặc biệt");
    }

    private static double? MatchOffset(ArchFloorPlan a, ArchFloorPlan b, GridDir dir)
    {
        var ga = a.Grids.Where(g => g.Dir == dir).ToList(); var gb = b.Grids.Where(g => g.Dir == dir).ToList();
        var diffs = new List<double>();
        foreach (var g in ga)
        {
            var h = gb.FirstOrDefault(x => x.Name == g.Name);
            if (h != null) diffs.Add(g.Coord - h.Coord);
        }
        if (diffs.Count == 0) return null;
        // trục tự đặt tên A, B… có thể lệch → chỉ tin khi các hiệu số gần nhau
        double med = diffs.OrderBy(x => x).ElementAt(diffs.Count / 2);
        var ok = diffs.Where(x => Math.Abs(x - med) < 0.05).ToList();
        return ok.Count >= Math.Max(1, diffs.Count / 2) ? ok.Average() : null;
    }

    private static void Shift(ArchFloorPlan p, double dx, double dy)
    {
        p.OffsetX = dx; p.OffsetY = dy;
        foreach (var w in p.Walls) { w.Ax += dx; w.Bx += dx; w.Ay += dy; w.By += dy; w.Ax = R(w.Ax); w.Bx = R(w.Bx); w.Ay = R(w.Ay); w.By = R(w.By); }
        foreach (var g in p.Grids) g.Coord = R(g.Coord + (g.Dir == GridDir.X ? dx : dy));
        foreach (var c in p.Columns) { c.X = R(c.X + dx); c.Y = R(c.Y + dy); }
        foreach (var r in p.Rooms) { r.X += dx; r.Y += dy; }
        foreach (var z in p.Zones) { z.X0 = R(z.X0 + dx); z.X1 = R(z.X1 + dx); z.Y0 = R(z.Y0 + dy); z.Y1 = R(z.Y1 + dy); }
        foreach (var op in p.Openings) { op.X += dx; op.Y += dy; }
        for (int i = 0; i < p.Preview.Count; i++)
        {
            var l = p.Preview[i];
            p.Preview[i] = new PreviewLine(l.X1 + dx, l.Y1 + dy, l.X2 + dx, l.Y2 + dy, l.Kind);
        }
        p.Outline = p.Outline.Select(q => (R(q.X + dx), R(q.Y + dy))).ToList();
    }

    private static double R(double v) => Math.Round(v, 4);

    // ==================================================================
    // TIỆN ÍCH
    // ==================================================================
    internal static bool Match(string layer, string keys)
    {
        if (string.IsNullOrWhiteSpace(keys) || layer == null) return false;
        string L = Norm(layer);
        foreach (var k in keys.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string K = Norm(k);
            if (K.Length > 0 && L.Contains(K)) return true;
        }
        return false;
    }

    private static bool IsGridEntity(DxfEntity e, ArchImportOptions o)
    {
        if (Match(e.Layer, o.GridLayers)) return true;
        string lt = (e.LineType ?? "").ToUpperInvariant();
        return e.Type == DxfEntityType.Line && (lt.Contains("CENTER") || lt.Contains("DASHDOT") || lt.Contains("AXIS") || lt.Contains("TRUC"));
    }

    private static bool IsDoorBlock(string block)
    {
        if (string.IsNullOrEmpty(block)) return false;
        string b = Norm(block);
        return b.Contains("DOOR") || b.Contains("WINDOW") || b.Contains("CUA") || Regex.IsMatch(b, @"^(D|S|CD|CS|W)\d");
    }

    /// <summary>Bỏ dấu tiếng Việt, chữ hoa, gộp khoảng trắng.</summary>
    public static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var f = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(f.Length);
        foreach (char c in f)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant(), @"\s+", " ").Trim();
    }

    /// <summary>Nhận dạng công năng từ chữ ghi trên bản vẽ (so khớp theo từ).</summary>
    public static RoomType Classify(string normText, List<RoomType> catalog)
    {
        var tokens = Regex.Split(normText, @"[^A-Z0-9]+").Where(t => t.Length > 0).ToList();
        if (tokens.Count == 0) return null;
        string joined = " " + string.Join(" ", tokens) + " ";
        RoomType best = null; int bestLen = 0;
        foreach (var rt in catalog)
            foreach (var k in rt.Keys)
            {
                string kk = " " + string.Join(" ", Regex.Split(Norm(k), @"[^A-Z0-9]+").Where(t => t.Length > 0)) + " ";
                if (kk.Trim().Length == 0) continue;
                if (joined.Contains(kk) && kk.Length > bestLen) { best = rt; bestLen = kk.Length; }
            }
        return best;
    }

    private static IEnumerable<Seg2> Segments(IEnumerable<DxfEntity> ents)
    {
        foreach (var e in ents)
        {
            if (e.Type == DxfEntityType.Text || e.Type == DxfEntityType.Circle) continue;
            var pts = e.Points;
            for (int i = 0; i + 1 < pts.Count; i++) yield return new Seg2(pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y);
            if (e.Type == DxfEntityType.Polyline && e.Closed && pts.Count > 2)
                yield return new Seg2(pts[^1].X, pts[^1].Y, pts[0].X, pts[0].Y);
        }
    }

    private static double SegDist(Seg2 a, Seg2 b)
    {
        if (ImportGeometry.Intersect(a, b, out _, out _, 0)) return 0;
        double d = double.MaxValue;
        foreach (var (px, py) in new[] { (a.Ax, a.Ay), (a.Bx, a.By) })
        {
            var (t, dist) = ImportGeometry.ProjectPoint(b, px, py);
            d = Math.Min(d, t >= 0 && t <= 1 ? dist : Math.Min(Dist(px, py, b.Ax, b.Ay), Dist(px, py, b.Bx, b.By)));
        }
        foreach (var (px, py) in new[] { (b.Ax, b.Ay), (b.Bx, b.By) })
        {
            var (t, dist) = ImportGeometry.ProjectPoint(a, px, py);
            d = Math.Min(d, t >= 0 && t <= 1 ? dist : Math.Min(Dist(px, py, a.Ax, a.Ay), Dist(px, py, a.Bx, a.By)));
        }
        return d;
    }

    private static double Dist(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    private static List<(List<DxfEntity> ents, double x0, double y0, double x1, double y1)> ClusterBoxes(List<DxfEntity> ents, double gap)
    {
        int n = ents.Count;
        var bx = new (double x0, double y0, double x1, double y1)[n];
        for (int i = 0; i < n; i++)
        {
            var e = ents[i];
            double r = e.Type == DxfEntityType.Circle ? e.Radius : 0;
            bx[i] = (e.Points.Min(p => p.X) - r, e.Points.Min(p => p.Y) - r, e.Points.Max(p => p.X) + r, e.Points.Max(p => p.Y) + r);
        }
        var parent = Enumerable.Range(0, n).ToArray();
        int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
        const double cell = 1.0;
        var hash = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < n; i++)
        {
            var q = bx[i];
            int cx0 = (int)Math.Floor((q.x0 - gap) / cell), cx1 = (int)Math.Floor((q.x1 + gap) / cell);
            int cy0 = (int)Math.Floor((q.y0 - gap) / cell), cy1 = (int)Math.Floor((q.y1 + gap) / cell);
            if ((long)(cx1 - cx0 + 1) * (cy1 - cy0 + 1) > 400) { cx1 = cx0 + 19; cy1 = cy0 + 19; }
            for (int cx = cx0; cx <= cx1; cx++)
                for (int cy = cy0; cy <= cy1; cy++)
                {
                    if (!hash.TryGetValue((cx, cy), out var lst)) hash[(cx, cy)] = lst = new List<int>();
                    foreach (int j in lst)
                    {
                        var r = bx[j];
                        if (q.x0 - gap <= r.x1 && r.x0 - gap <= q.x1 && q.y0 - gap <= r.y1 && r.y0 - gap <= q.y1)
                        {
                            int a = Find(i), b = Find(j);
                            if (a != b) parent[b] = a;
                        }
                    }
                    lst.Add(i);
                }
        }
        return Enumerable.Range(0, n).GroupBy(Find).Select(g =>
        {
            var idx = g.ToList();
            return (idx.Select(i => ents[i]).ToList(), idx.Min(i => bx[i].x0), idx.Min(i => bx[i].y0), idx.Max(i => bx[i].x1), idx.Max(i => bx[i].y1));
        }).ToList();
    }
}
