using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Arch;

/// <summary>Đọc bản vẽ kiến trúc sơ bộ (DXF/DWG), tách các mặt bằng tầng, nhận diện trục, tường, cột, phòng, cầu thang, thông tầng.
/// Quy ước nhận dạng (sửa được trong ArchImportOptions):
/// <list type="bullet">
/// <item>Tường: các nét trên lớp tường; 2 nét song song cách nhau 80–450 mm → tường có chiều dày bằng khoảng cách; khe hở ≤ 1,6 m giữa
///   hai đoạn tường thẳng hàng coi là lỗ cửa. Tường xiên (góc bất kỳ): ghép từng nhóm nét cùng phương (lệch ≤ 1°) như tường song song trục.
///   Tường cong: 2 cung tròn đồng tâm (ARC, cung lồi của đa tuyến, CIRCLE) có bán kính chênh 80–450 mm → tường cong (mỗi đoạn ≤ 180°).</item>
/// <item>Cột: đa tuyến kín trên lớp cột (chữ nhật xoay → cột có góc xoay); hình tròn → cột vuông cùng diện tích (cạnh = D·√π/2).</item>
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
        var lc = LayerClassifier.For(o);
        foreach (var f in files)
        {
            DxfReader dxf = ReadFile(f, o.ScaleOverride, d);
            if (dxf == null) continue;
            string fn = Path.GetFileName(f);
            d.Warnings.AddRange(dxf.Warnings.Select(w => fn + ": " + w));
            d.Info.Add($"{fn}: {dxf.Entities.Count} thực thể (+{dxf.FillEntities.Count} miền tô), $INSUNITS = {dxf.InsUnits} → ×{dxf.Scale.ToString(CultureInfo.InvariantCulture)} (đơn vị bản vẽ → m)");
            int nEl = dxf.Entities.Count(e => e.SourceType == "ELLIPSE"), nSp = dxf.Entities.Count(e => e.SourceType == "SPLINE");
            if (nEl + nSp > 0)
                d.Info.Add($"{fn}: đọc {nEl} ELLIPSE, {nSp} SPLINE – đã chia thành đa tuyến (dùng được làm biên sàn, lỗ sàn, cột, tường).");
            var (ents, fills) = VisibleEntities(dxf, o, d, fn);
            var tmp = new ArchDrawing();
            var plans = SplitPlans(ents, fills, o, lc, tmp, Path.GetFileNameWithoutExtension(f), files.Count == 1);
            // không tách được mặt bằng nào, đơn vị theo file: thử đơn vị m / cm (bản vẽ vẽ theo m nhưng $INSUNITS = mm hoặc không khai báo)
            if (plans.Count == 0 && o.ScaleOverride <= 0)
            {
                double span = Span(dxf.Entities);
                foreach (double k in span > 0 && span < 0.5 ? new[] { 1000.0 } : span >= 0.5 && span < 5 ? new[] { 10.0 } : Array.Empty<double>())
                {
                    var dx2 = ReadFile(f, dxf.Scale * k, null);
                    if (dx2 == null) continue;
                    var (e2, f2) = VisibleEntities(dx2, o, null, fn);
                    var tmp2 = new ArchDrawing();
                    var p2 = SplitPlans(e2, f2, o, lc, tmp2, Path.GetFileNameWithoutExtension(f), files.Count == 1);
                    if (p2.Count == 0) continue;
                    d.Warnings.Add($"{fn}: kích thước bản vẽ theo $INSUNITS chỉ {span:0.###} m – có vẻ bản vẽ vẽ theo {(k >= 1000 ? "m" : "cm")}; đã tự đổi đơn vị (×{(dxf.Scale * k).ToString(CultureInfo.InvariantCulture)}). Chọn \"Đơn vị bản vẽ\" thủ công nếu sai.");
                    plans = p2; tmp = tmp2;
                    break;
                }
            }
            d.Warnings.AddRange(tmp.Warnings); d.Info.AddRange(tmp.Info);
            raw.AddRange(plans);
        }
        foreach (var kv in lc.Seen) d.LayerCategories[kv.Key] = kv.Value;
        Finish(d, raw, o);
        return d;
    }

    /// <summary>Hàm đọc file dùng thay cho DxfReader/ODA (vd. bộ đọc DWG riêng). null = mặc định. Trả null kèm lỗi nếu không đọc được.</summary>
    public static Func<string, double, (DxfReader reader, string error)> CustomReader { get; set; }

    internal static DxfReader ReadFile(string f, double scale, ArchDrawing d)
    {
        DxfReader dxf = null; string err = null;
        if (CustomReader != null) { var r = CustomReader(f, scale); dxf = r.reader; err = r.error; }
        else if (DwgConverter.IsDwg(f)) dxf = DwgConverter.ReadAny(f, scale, out err);
        else dxf = DxfReader.Read(f, scale);
        if (dxf == null) d?.Warnings.Add(Path.GetFileName(f) + ": " + (err ?? "không đọc được bản vẽ") +
                                          (DwgConverter.IsDwg(f) ? " – Lưu bản vẽ dạng DXF (SaveAs DXF) hoặc dùng add-in AutoCAD VXT." : ""));
        return dxf;
    }

    /// <summary>Thực thể trên layer đang bật (bỏ layer tắt nếu SkipOffLayers).</summary>
    private static (List<DxfEntity> ents, List<DxfEntity> fills) VisibleEntities(DxfReader dxf, ArchImportOptions o, ArchDrawing d, string fn)
    {
        if (!o.SkipOffLayers || dxf.OffLayers.Count == 0) return (dxf.Entities, dxf.FillEntities);
        var ents = dxf.Entities.Where(e => !dxf.OffLayers.Contains(e.Layer)).ToList();
        var fills = dxf.FillEntities.Where(e => !dxf.OffLayers.Contains(e.Layer)).ToList();
        int skipped = dxf.Entities.Count - ents.Count;
        if (skipped > 0) d?.Info.Add($"{fn}: bỏ {skipped} thực thể trên layer đang tắt: {string.Join(", ", dxf.OffLayers.Take(8))}{(dxf.OffLayers.Count > 8 ? "…" : "")}");
        return (ents, fills);
    }

    private static double Span(List<DxfEntity> ents)
    {
        if (ents.Count == 0) return 0;
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var e in ents) foreach (var (x, y) in e.Points) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
        return Math.Max(x1 - x0, y1 - y0);
    }

    /// <summary>Đọc từ danh sách thực thể đã có (dùng cho kiểm thử).</summary>
    public static ArchDrawing FromEntities(List<DxfEntity> ents, ArchImportOptions o, string source = "") => FromEntities(ents, null, o, source);

    /// <summary>Đọc từ danh sách thực thể + miền tô (HATCH/SOLID) đã có – dùng cho kiểm thử và bộ đọc DWG riêng.</summary>
    public static ArchDrawing FromEntities(List<DxfEntity> ents, List<DxfEntity> fills, ArchImportOptions o, string source = "")
    {
        o ??= new ArchImportOptions();
        var d = new ArchDrawing { Source = source };
        var lc = LayerClassifier.For(o);
        var plans = SplitPlans(ents ?? new List<DxfEntity>(), fills ?? new List<DxfEntity>(), o, lc, d, source, true);
        foreach (var kv in lc.Seen) d.LayerCategories[kv.Key] = kv.Value;
        Finish(d, plans, o);
        return d;
    }

    // ==================================================================
    // 1. TÁCH CÁC MẶT BẰNG
    // ==================================================================
    private static List<ArchFloorPlan> SplitPlans(List<DxfEntity> ents, List<DxfEntity> fills, ArchImportOptions o, LayerClassifier lc, ArchDrawing d, string fileTag, bool singleFile)
    {
        var res = new List<ArchFloorPlan>();
        foreach (var e in ents) lc.Of(e.Layer);
        foreach (var e in ents.Where(e => e.Type == DxfEntityType.Text)) e.Text = VnText.Fix(e.Text, e.FontHint);
        var wallEnts = ents.Where(e => e.Type != DxfEntityType.Text && lc.Is(e.Layer, CadCategory.Tuong)).ToList();
        if (wallEnts.Count == 0)
        {
            d.Warnings.Add($"{fileTag}: không tìm thấy nét trên layer nhóm Tường (từ khoá {o.WallLayers}). Gán layer tường ở bảng \"Layer trong file\" hoặc thêm quy tắc trong \"Chuẩn layer CAD…\" (vd. lớp tường của văn phòng là \"KT-TUONG\").");
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
            bool Near(DxfEntity e) => e.Points.Count > 0 && e.Points.Min(p => p.X) <= x1 + m && e.Points.Max(p => p.X) >= x0 - m
                                      && e.Points.Min(p => p.Y) <= y1 + m && e.Points.Max(p => p.Y) >= y0 - m;
            var inEnts = ents.Where(Near).ToList();
            var inFills = fills.Where(Near).ToList();
            Recognize(plan, inEnts, inFills, x0, y0, x1, y1, o, lc);
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
    private static void Recognize(ArchFloorPlan plan, List<DxfEntity> ents, List<DxfEntity> fills, double bx0, double by0, double bx1, double by1, ArchImportOptions o, LayerClassifier lc)
    {
        bool InBox(double x, double y, double m = 0.3) => x >= bx0 - m && x <= bx1 + m && y >= by0 - m && y <= by1 + m;
        bool Is(DxfEntity e, string cat) => lc.Is(e.Layer, cat);
        // layer "Bỏ qua": không dùng cho bất kỳ nhận diện nào
        ents = ents.Where(e => !Is(e, CadCategory.BoQua)).ToList();

        // ---- nền xem trước ----
        foreach (var e in ents.Where(e => e.Type != DxfEntityType.Text))
        {
            string cat = lc.Of(e.Layer);
            int kind = cat == CadCategory.Tuong ? 1 : IsGridEntity(e, lc) ? 2 : cat == CadCategory.Cot ? 3
                     : cat == CadCategory.Cua || IsDoorBlock(e.Block) ? 4 : cat == CadCategory.Thang ? 5
                     : cat == CadCategory.Dam ? 6 : cat == CadCategory.San ? 7 : 0;
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
        // tường vẽ bằng 1 đa tuyến có bề rộng (bề rộng = chiều dày tường)
        bool Wide(DxfEntity e) => e.Type == DxfEntityType.Polyline && e.Width >= o.MinWallThickness - 1e-6 && e.Width <= o.MaxWallThickness + 1e-6;
        var wideWalls = new List<ArchWall>();
        var special = new List<ArchWall>();   // tường xiên / cong (đã ghép, không qua MergeWalls trục giao)
        int wideSpecial = 0;
        foreach (var e in ents.Where(e => e.Type != DxfEntityType.Text && Is(e, CadCategory.Tuong) && Wide(e)))
        {
            double t = Math.Round(e.Width, 3);
            foreach (var ap in ArcPieces(e))
                foreach (var cw in CurvedWalls(ap.Cx, ap.Cy, ap.R, ap.A0, ap.Sw, t, 0, e.Layer))
                    if (InBox((cw.Ax + cw.Bx) / 2, (cw.Ay + cw.By) / 2, 1.0)) { special.Add(cw); wideSpecial++; }
            foreach (var s in StraightSegments(new[] { e }).Where(s => s.Length >= 0.05 && InBox((s.Ax + s.Bx) / 2, (s.Ay + s.By) / 2)))
            {
                double ang = Math.Abs(Math.Atan2(s.By - s.Ay, s.Bx - s.Ax) * 180 / Math.PI) % 180;
                if (s.IsAlongX(0.01) || ang < 1 || ang > 179) wideWalls.Add(new ArchWall { Ax = Math.Min(s.Ax, s.Bx), Ay = (s.Ay + s.By) / 2, Bx = Math.Max(s.Ax, s.Bx), By = (s.Ay + s.By) / 2, Thickness = t, Layer = e.Layer });
                else if (s.IsAlongY(0.01) || Math.Abs(ang - 90) < 1) wideWalls.Add(new ArchWall { Ax = (s.Ax + s.Bx) / 2, Ay = Math.Min(s.Ay, s.By), Bx = (s.Ax + s.Bx) / 2, By = Math.Max(s.Ay, s.By), Thickness = t, Layer = e.Layer });
                else if (s.Length >= 0.3) { special.Add(new ArchWall { Ax = s.Ax, Ay = s.Ay, Bx = s.Bx, By = s.By, Thickness = t, Layer = e.Layer }); wideSpecial++; }
            }
        }
        if (wideWalls.Count + wideSpecial > 0) plan.Notes.Add($"{wideWalls.Count + wideSpecial} đoạn tường vẽ bằng đa tuyến có bề rộng – lấy bề rộng nét làm chiều dày tường"
                                                             + (wideSpecial > 0 ? $" ({wideSpecial} đoạn xiên/cong)." : "."));
        var wallEntsN = ents.Where(e => e.Type != DxfEntityType.Text && Is(e, CadCategory.Tuong) && !Wide(e)).ToList();
        var wallSegs = StraightSegments(wallEntsN)
            .Where(s => s.Length >= 0.05 && InBox((s.Ax + s.Bx) / 2, (s.Ay + s.By) / 2)).ToList();
        var hx = new List<(double c, double a, double b)>();   // nét ngang: y, x đầu, x cuối
        var vy = new List<(double c, double a, double b)>();   // nét đứng: x, y đầu, y cuối
        var dg = new List<(double ang, Seg2 s)>();              // nét xiên: góc (độ, 1…179)
        foreach (var s in wallSegs)
        {
            double dx = s.Bx - s.Ax, dy = s.By - s.Ay;
            double ang = Math.Atan2(dy, dx) * 180 / Math.PI % 180;
            if (ang < 0) ang += 180;
            if (ang < 1 || ang > 179) hx.Add(((s.Ay + s.By) / 2, Math.Min(s.Ax, s.Bx), Math.Max(s.Ax, s.Bx)));
            else if (Math.Abs(ang - 90) < 1) vy.Add(((s.Ax + s.Bx) / 2, Math.Min(s.Ay, s.By), Math.Max(s.Ay, s.By)));
            else if (s.Length >= 0.3) dg.Add((ang, s));
        }
        var walls = new List<ArchWall>();
        walls.AddRange(PairWalls(hx, true, o));
        walls.AddRange(PairWalls(vy, false, o));
        int orthoPairs = walls.Count;

        // tường xiên: nhóm nét cùng phương, ghép cặp trong hệ toạ độ quay theo phương nét
        var diagSingles = new List<ArchWall>();
        int diagUnpaired = 0, diagPairs = 0;
        foreach (var grp in GroupByAngle(dg, 1.0))
        {
            double th = grp.Average(q => q.ang) * Math.PI / 180, ux = Math.Cos(th), uy = Math.Sin(th);
            var loc = grp.Select(q =>
            {
                double a1 = q.s.Ax * ux + q.s.Ay * uy, a2 = q.s.Bx * ux + q.s.By * uy;
                double c = ((q.s.Ax + q.s.Bx) / 2) * -uy + ((q.s.Ay + q.s.By) / 2) * ux;
                return (c, a: Math.Min(a1, a2), b: Math.Max(a1, a2));
            }).ToList();
            var paired = PairWalls(loc, true, o).ToList();
            if (paired.Count == 0)
            {
                diagUnpaired += loc.Count;
                foreach (var (c, a, b) in loc.Where(l => l.b - l.a >= 1.0))
                    diagSingles.Add(ToWorld(new ArchWall { Ax = a, Ay = c, Bx = b, By = c, Thickness = o.DefaultWallThickness }, ux, uy));
                continue;
            }
            diagPairs += paired.Count;
            foreach (var w in MergeWalls(paired, o)) special.Add(ToWorld(w, ux, uy));
        }

        // tường cong: cung tròn đồng tâm
        var arcs = new List<ArcPiece>();
        foreach (var e in wallEntsN) arcs.AddRange(ArcPieces(e));
        foreach (var e in ents.Where(e => e.Type == DxfEntityType.Circle && Is(e, CadCategory.Tuong) && e.Radius > 0.05 && e.Points.Count > 0))
            arcs.Add(new ArcPiece(e.Points[0].X, e.Points[0].Y, e.Radius, 0, 2 * Math.PI, e.Layer));
        arcs = arcs.Where(a => InBox(a.Cx + a.R * Math.Cos(a.A0 + a.Sw / 2), a.Cy + a.R * Math.Sin(a.A0 + a.Sw / 2), 1.0)).ToList();
        var curved = PairArcs(arcs, o.MinWallThickness, o.MaxWallThickness, out var arcUnpaired);
        special.AddRange(curved);

        bool hadPairs = orthoPairs + diagPairs + curved.Count > 0;
        walls.AddRange(wideWalls);
        if (!hadPairs && (wallSegs.Count > 0 || arcs.Count > 0))
        {
            // bản vẽ tường 1 nét
            foreach (var (c, a, b) in hx.Where(t => t.b - t.a >= 1.0)) walls.Add(new ArchWall { Ax = a, Ay = c, Bx = b, By = c, Thickness = o.DefaultWallThickness });
            foreach (var (c, a, b) in vy.Where(t => t.b - t.a >= 1.0)) walls.Add(new ArchWall { Ax = c, Ay = a, Bx = c, By = b, Thickness = o.DefaultWallThickness });
            special.AddRange(diagSingles);
            foreach (var ap in arcUnpaired.Where(a => a.R * a.Sw >= 1.0))
                special.AddRange(CurvedWalls(ap.Cx, ap.Cy, ap.R, ap.A0, ap.Sw, o.DefaultWallThickness, 0, ap.Layer));
            plan.Notes.Add($"Tường vẽ 1 nét – lấy chiều dày {o.DefaultWallThickness * 1000:0} mm.");
            diagUnpaired = 0; arcUnpaired.Clear();
        }
        walls = MergeWalls(walls, o).Where(w => w.Length >= o.MinWallLength).ToList();
        walls.AddRange(special.Where(w => w.Length >= o.MinWallLength));
        int nDiag = walls.Count(w => w.IsDiagonal), nCurved = walls.Count(w => w.IsCurved);
        if (nDiag > 0)
            plan.Notes.Add($"{nDiag} đoạn tường xiên (góc {string.Join(", ", walls.Where(w => w.IsDiagonal).Select(w => w.AngleDeg.ToString("0.#", CultureInfo.InvariantCulture) + "°").Distinct().Take(6))}) – "
                           + "nhận từ nét song song theo phương bất kỳ; khi triển khai sẽ đặt dầm xiên đỡ tường.");
        if (nCurved > 0)
            plan.Notes.Add($"{nCurved} đoạn tường cong (R = {string.Join(", ", walls.Where(w => w.IsCurved).Select(w => w.Radius.ToString("0.00", CultureInfo.InvariantCulture)).Distinct().Take(6))} m) – "
                           + "nhận từ cung tròn đồng tâm; khi triển khai sẽ đặt dầm cong đỡ tường, tải tường tính theo chiều dài cung.");
        if (diagUnpaired + arcUnpaired.Count > 0)
            plan.Notes.Add($"{diagUnpaired} nét xiên và {arcUnpaired.Count} cung trên layer tường không ghép được thành tường (không có nét song song/đồng tâm cách 80–450 mm) – bỏ qua.");

        // ---- cột kiến trúc ----
        int nRot = 0, nRound = 0;
        foreach (var e in ents.Concat(fills).Where(e => e.Type == DxfEntityType.Polyline && e.Closed && Is(e, CadCategory.Cot)))
        {
            // chữ nhật xoay một góc → cột có góc xoay (kích thước theo trục riêng của cột)
            if (RotatedRect(e.Points, out double rcx, out double rcy, out double rb, out double rh, out double rang))
            {
                if (rb < 0.12 || rh < 0.12 || rb > 1.0 || rh > 1.0 || !InBox(rcx, rcy)) continue;
                if (plan.Columns.Any(c => Math.Abs(c.X - rcx) < 0.05 && Math.Abs(c.Y - rcy) < 0.05)) continue;
                plan.Columns.Add(new ArchColumn { X = rcx, Y = rcy, Bx = Math.Round(rb, 3), By = Math.Round(rh, 3), Angle = Math.Round(rang, 2) });
                nRot++;
                continue;
            }
            double x0 = e.Points.Min(p => p.X), x1 = e.Points.Max(p => p.X), y0 = e.Points.Min(p => p.Y), y1 = e.Points.Max(p => p.Y);
            double w = x1 - x0, h = y1 - y0;
            if (w < 0.12 || h < 0.12 || w > 1.0 || h > 1.0) continue;
            double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
            if (!InBox(cx, cy)) continue;
            if (plan.Columns.Any(c => Math.Abs(c.X - cx) < 0.05 && Math.Abs(c.Y - cy) < 0.05)) continue;
            plan.Columns.Add(new ArchColumn { X = cx, Y = cy, Bx = Math.Round(w, 3), By = Math.Round(h, 3) });
        }
        // cột tròn (CIRCLE trên layer cột): quy về cột vuông cùng diện tích, cạnh a = D·√π/2
        foreach (var e in ents.Concat(fills).Where(e => e.Type == DxfEntityType.Circle && Is(e, CadCategory.Cot) && e.Points.Count > 0))
        {
            double dia = 2 * e.Radius;
            if (dia < 0.12 || dia > 1.0) continue;
            var (cx, cy) = e.Points[0];
            if (!InBox(cx, cy)) continue;
            if (plan.Columns.Any(c => Math.Abs(c.X - cx) < 0.05 && Math.Abs(c.Y - cy) < 0.05)) continue;
            double side = Math.Round(RoundColumnSide(dia), 3);
            plan.Columns.Add(new ArchColumn { X = cx, Y = cy, Bx = side, By = side, Diameter = Math.Round(dia, 3) });
            nRound++;
        }
        if (nRot > 0)
            plan.Notes.Add($"{nRot} cột xoay (góc {string.Join(", ", plan.Columns.Where(c => Math.Abs(c.Angle) > 1e-9).Select(c => c.Angle.ToString("0.#", CultureInfo.InvariantCulture) + "°").Distinct().Take(6))}) – góc xoay được đưa vào tiết diện cột khi triển khai.");
        if (nRound > 0)
            plan.Notes.Add($"{nRound} cột tròn D = {string.Join(", ", plan.Columns.Where(c => c.Diameter > 0).Select(c => (c.Diameter * 1000).ToString("0", CultureInfo.InvariantCulture)).Distinct().Take(6))} mm – quy về cột vuông cùng diện tích (cạnh = D·√π/2 ≈ 0,886·D).");
        int curvy = ents.Count(e => e.SourceType is "ELLIPSE" or "SPLINE");
        if (curvy > 0) plan.Notes.Add($"{curvy} đường ELLIPSE/SPLINE đã chia thành đa tuyến (biên kín trên layer sàn/cột được dùng như đa tuyến kín).");

        // ---- trục ----
        var texts = ents.Where(e => e.Type == DxfEntityType.Text).ToList();
        var gridSegs = Segments(ents.Where(e => e.Type != DxfEntityType.Text && IsGridEntity(e, lc))).ToList();
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
            plan.Notes.Add("Không đọc được lưới trục (layer nhóm Trục, từ khoá " + o.GridLayers + ") – tự lập trục theo tim tường chính, kiểm tra lại.");
        }

        // ---- lỗ cửa ----
        var doorEnts = ents.Where(e => e.Type != DxfEntityType.Text && (Is(e, CadCategory.Cua) || IsDoorBlock(e.Block))
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
                double t, dist;
                if (w.IsCurved)
                {
                    var arc = CurvedBeam.Arc(new Vec3(w.Ax, w.Ay, 0), new Vec3(w.Bx, w.By, 0), w.Sagitta);
                    if (arc == null) continue;
                    t = arc.ParamOf(op.X, op.Y, out dist);
                    if (double.IsNaN(t)) continue;
                }
                else (t, dist) = ImportGeometry.ProjectPoint(new Seg2(w.Ax, w.Ay, w.Bx, w.By), op.X, op.Y);
                if (t < -0.02 || t > 1.02 || dist > w.Thickness / 2 + 0.25) continue;
                if (dist < bd) { bd = dist; best = w; }
            }
            if (best != null && !InGap(best, op.X, op.Y)) best.OpeningLength += op.Width;
        }
        plan.Walls.AddRange(walls);

        // ---- phòng, cầu thang, thông tầng ----
        // có layer nhóm "Tên phòng" → chỉ đọc tên phòng trên các layer đó
        var roomTexts = texts.Where(t => Is(t, CadCategory.TenPhong)).ToList();
        if (roomTexts.Count == 0) roomTexts = texts;
        foreach (var t in roomTexts)
        {
            var (px, py) = t.Points[0];
            if (!InBox(px, py, Math.Max(o.MaxCantilever, 1.0) + 0.5)) continue;
            string s = Norm(t.Text);
            if (s.Length == 0 || s.Length > 40 || s.Contains("MAT BANG")) continue;
            var rt = Classify(s, o.Rooms);
            if (rt == null) continue;
            // ngoài tường bao: chỉ nhận ban công / sân thượng
            if (!InBox(px, py, -0.05) && rt.Kind != RoomKind.Balcony && rt.Kind != RoomKind.RoofTerrace) continue;
            plan.Rooms.Add(new ArchRoom { Text = t.Text, Kind = rt.Kind, X = px, Y = py });
            if (rt.Kind == RoomKind.Void) plan.Zones.Add(new ArchZone { Kind = RoomKind.Void, X0 = px, Y0 = py, X1 = px, Y1 = py, Note = "từ chữ \"" + t.Text + "\"" });
        }
        var stairEnts = ents.Where(e => e.Type != DxfEntityType.Text && Is(e, CadCategory.Thang) && e.Points.Count > 0 && InBox(e.Points[0].X, e.Points[0].Y)).ToList();
        foreach (var cl in ClusterBoxes(stairEnts, 0.35))
        {
            double w = cl.x1 - cl.x0, h = cl.y1 - cl.y0;
            if (w * h < 1.5 || w < 0.8 || h < 0.8) continue;
            plan.Zones.Add(new ArchZone { Kind = RoomKind.Stair, X0 = cl.x0, Y0 = cl.y0, X1 = cl.x1, Y1 = cl.y1, Note = "lớp cầu thang" });
        }
        if (!plan.Zones.Any(z => z.Kind == RoomKind.Stair))
            foreach (var r in plan.Rooms.Where(r => r.Kind == RoomKind.Stair))
                plan.Zones.Add(new ArchZone { Kind = RoomKind.Stair, X0 = r.X, Y0 = r.Y, X1 = r.X, Y1 = r.Y, Note = "từ chữ \"" + r.Text + "\"" });

        // ---- dầm, vùng sàn vẽ sẵn (layer nhóm Dầm / Sàn) ----
        ReadBeams(plan, ents.Where(e => e.Type != DxfEntityType.Text && e.Type != DxfEntityType.Circle && Is(e, CadCategory.Dam)).ToList(), InBox, o);
        ReadSlabs(plan, ents.Concat(fills).Where(e => e.Type == DxfEntityType.Polyline && e.Closed && Is(e, CadCategory.San)).ToList(), InBox);

        // ---- đường bao sàn ----
        plan.Outline = Footprint(walls, plan.Columns);
        foreach (var w in walls) w.Exterior = OnOutline(plan.Outline, w);
        AddBalconies(plan, ents, o);
    }

    // ------------------------------------------------------------------
    // Ghép nét thành tường
    // ------------------------------------------------------------------
    private static readonly double[] StdThick = { 0.10, 0.11, 0.15, 0.20, 0.22, 0.25, 0.30, 0.33 };

    private static IEnumerable<ArchWall> PairWalls(List<(double c, double a, double b)> lines, bool horiz, ArchImportOptions o) =>
        PairWalls(lines, horiz, o.MinWallThickness, o.MaxWallThickness);

    private static IEnumerable<ArchWall> PairWalls(List<(double c, double a, double b)> lines, bool horiz, double minT, double maxT)
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
                    if (d > maxT) break;
                    if (d < minT) continue;
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
        if (w.IsCurved) return false;
        double s = w.AlongX ? x : w.AlongY ? y : (x * (w.Bx - w.Ax) + y * (w.By - w.Ay)) / Math.Max(w.Chord, 1e-9);   // tường xiên: toạ độ dọc phương tường
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
        for (int i = 0; i < res.Count; i++) { res[i].Name = ImportGeometry.AutoName(dir, i); res[i].Auto = true; }
        return res;
    }

    // ------------------------------------------------------------------
    // Dầm, vùng sàn vẽ sẵn
    // ------------------------------------------------------------------
    /// <summary>Dầm vẽ 2 nét (cách nhau 100–800 mm) → tim dầm + bề rộng; dầm vẽ 1 nét (≥ 0,5 m, không thuộc cặp) → bề rộng 0 (lấy mặc định).</summary>
    private static void ReadBeams(ArchFloorPlan plan, List<DxfEntity> ents, Func<double, double, double, bool> inBox, ArchImportOptions o)
    {
        if (ents.Count == 0) return;
        var hx = new List<(double c, double a, double b)>(); var vy = new List<(double c, double a, double b)>();
        int diag = 0;
        foreach (var s in Segments(ents).Where(s => s.Length >= 0.05 && inBox((s.Ax + s.Bx) / 2, (s.Ay + s.By) / 2, 1.0)))
        {
            if (s.IsAlongX(0.01)) hx.Add(((s.Ay + s.By) / 2, Math.Min(s.Ax, s.Bx), Math.Max(s.Ax, s.Bx)));
            else if (s.IsAlongY(0.01)) vy.Add(((s.Ax + s.Bx) / 2, Math.Min(s.Ay, s.By), Math.Max(s.Ay, s.By)));
            else if (s.Length > 0.5) diag++;
        }
        var res = new List<ArchBeamLine>();
        foreach (bool horiz in new[] { true, false })
        {
            var lines = horiz ? hx : vy;
            var pairs = PairWalls(lines, horiz, 0.10, 0.80).Where(w => w.Length >= 0.3).ToList();
            foreach (var w in pairs) res.Add(new ArchBeamLine { Ax = w.Ax, Ay = w.Ay, Bx = w.Bx, By = w.By, Width = w.Thickness });
            foreach (var (c, a, b) in lines.Where(l => l.b - l.a >= 0.5))
            {
                bool inPair = pairs.Any(w =>
                {
                    double wc = horiz ? w.Ay : w.Ax, wa = horiz ? Math.Min(w.Ax, w.Bx) : Math.Min(w.Ay, w.By), wb = horiz ? Math.Max(w.Ax, w.Bx) : Math.Max(w.Ay, w.By);
                    return Math.Abs(wc - c) <= w.Thickness / 2 + 0.02 && Math.Min(b, wb) - Math.Max(a, wa) > 0.5 * (b - a);
                });
                if (!inPair) res.Add(horiz ? new ArchBeamLine { Ax = a, Ay = c, Bx = b, By = c } : new ArchBeamLine { Ax = c, Ay = a, Bx = c, By = b });
            }
        }
        // gộp đoạn thẳng hàng chồng/kề nhau
        var merged = new List<ArchBeamLine>();
        foreach (var g in res.GroupBy(b => (b.AlongX, Math.Round((b.AlongX ? b.Ay : b.Ax) / 0.02))))
        {
            var iv = g.Select(b => (a: b.AlongX ? Math.Min(b.Ax, b.Bx) : Math.Min(b.Ay, b.By), b: b.AlongX ? Math.Max(b.Ax, b.Bx) : Math.Max(b.Ay, b.By), w: b.Width, c: b.AlongX ? b.Ay : b.Ax))
                      .OrderBy(t => t.a).ToList();
            var cur = iv[0];
            void Emit() => merged.Add(g.Key.AlongX ? new ArchBeamLine { Ax = cur.a, Ay = cur.c, Bx = cur.b, By = cur.c, Width = cur.w } : new ArchBeamLine { Ax = cur.c, Ay = cur.a, Bx = cur.c, By = cur.b, Width = cur.w });
            for (int k = 1; k < iv.Count; k++)
            {
                if (iv[k].a <= cur.b + 0.05) cur = (cur.a, Math.Max(cur.b, iv[k].b), Math.Max(cur.w, iv[k].w), cur.c);
                else { Emit(); cur = iv[k]; }
            }
            Emit();
        }
        string layer = ents[0].Layer;
        foreach (var b in merged) b.Layer = layer;
        plan.Beams.AddRange(merged.Where(b => b.Length >= 0.5));
        if (diag > 0) plan.Notes.Add($"{diag} nét dầm xiên – chưa hỗ trợ, bỏ qua.");
        if (plan.Beams.Count > 0) plan.Notes.Add($"{plan.Beams.Count} dầm vẽ sẵn trên layer dầm ({string.Join(", ", ents.Select(e => e.Layer).Distinct().Take(4))}).");
    }

    /// <summary>Đa tuyến kín trên layer sàn → vùng sàn; vùng nằm trọn trong vùng sàn khác → lỗ sàn.</summary>
    private static void ReadSlabs(ArchFloorPlan plan, List<DxfEntity> ents, Func<double, double, double, bool> inBox)
    {
        var regs = new List<ArchSlabRegion>();
        foreach (var e in ents)
        {
            if (e.Points.Count < 3) continue;
            var pts = e.Points.ToList();
            double x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X), y0 = pts.Min(p => p.Y), y1 = pts.Max(p => p.Y);
            if (!inBox((x0 + x1) / 2, (y0 + y1) / 2, 2.5)) continue;
            var r = new ArchSlabRegion { Points = pts, X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, Layer = e.Layer };
            if (r.Area < 0.25) continue;
            if (regs.Any(q => Math.Abs(q.X0 - x0) < 0.01 && Math.Abs(q.X1 - x1) < 0.01 && Math.Abs(q.Y0 - y0) < 0.01 && Math.Abs(q.Y1 - y1) < 0.01)) continue;
            regs.Add(r);
        }
        foreach (var r in regs)
            r.IsOpening = regs.Any(q => q != r && q.Area > r.Area + 1e-6 && r.Points.All(p => PointInPoly(q.Points, p.X, p.Y, 0.01)));
        plan.SlabRegions.AddRange(regs);
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
                                                  || cols.Any(c => (sgn > 0 ? c.X > cx : c.X < cx) && Math.Abs(c.Y - cy) <= c.By / 2)
                                                  || walls.Any(w => DiagRayHit(w, cx, cy, sgn, true));
        bool HitY(double cx, double cy, int sgn) => walls.Any(w => w.AlongX && (sgn > 0 ? w.Ay > cy : w.Ay < cy) && cx >= Math.Min(w.Ax, w.Bx) - 0.02 && cx <= Math.Max(w.Ax, w.Bx) + 0.02)
                                                  || cols.Any(c => (sgn > 0 ? c.Y > cy : c.Y < cy) && Math.Abs(c.X - cx) <= c.Bx / 2)
                                                  || walls.Any(w => DiagRayHit(w, cx, cy, sgn, false));
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                double cx = (xs[i] + xs[i + 1]) / 2, cy = (ys[j] + ys[j + 1]) / 2;
                int hits = (HitX(cx, cy, 1) ? 1 : 0) + (HitX(cx, cy, -1) ? 1 : 0) + (HitY(cx, cy, 1) ? 1 : 0) + (HitY(cx, cy, -1) ? 1 : 0);
                inside[i, j] = hits == 4;
            }
        // lần 2 (lan dần): ô có 3 hướng gặp tường và kề ô trong nhà (khe cửa cuốn mặt tiền, lỗ mở lớn)
        var three = new bool[nx, ny];
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                if (inside[i, j]) continue;
                double cx = (xs[i] + xs[i + 1]) / 2, cy = (ys[j] + ys[j + 1]) / 2;
                int hits = (HitX(cx, cy, 1) ? 1 : 0) + (HitX(cx, cy, -1) ? 1 : 0) + (HitY(cx, cy, 1) ? 1 : 0) + (HitY(cx, cy, -1) ? 1 : 0);
                three[i, j] = hits == 3;
            }
        bool grew = true;
        while (grew)
        {
            grew = false;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    if (inside[i, j] || !three[i, j]) continue;
                    bool nb = (i > 0 && inside[i - 1, j]) || (i + 1 < nx && inside[i + 1, j]) || (j > 0 && inside[i, j - 1]) || (j + 1 < ny && inside[i, j + 1]);
                    if (nb) { inside[i, j] = true; grew = true; }
                }
        }
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
                if (mx == null) mx = WallOffset(baseP, p, true);
                if (my == null) my = WallOffset(baseP, p, false);
                dx = mx.Value - ox0;
                dy = my.Value - oy0;
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
            d.Info.Add($"  {p.Title}: {p.Walls.Count} đoạn tường ({p.Walls.Sum(w => w.Length):0.0} m), {p.Grids.Count} trục, {p.Columns.Count} cột KT, {p.Rooms.Count} phòng, {p.Openings.Count} cửa, {p.Zones.Count} vùng đặc biệt"
                       + (p.Walls.Any(w => w.IsDiagonal) ? $", {p.Walls.Count(w => w.IsDiagonal)} tường xiên" : "") + (p.Walls.Any(w => w.IsCurved) ? $", {p.Walls.Count(w => w.IsCurved)} tường cong" : "")
                       + (p.Columns.Any(c => Math.Abs(c.Angle) > 1e-9) ? $", {p.Columns.Count(c => Math.Abs(c.Angle) > 1e-9)} cột xoay" : "") + (p.Columns.Any(c => c.Diameter > 0) ? $", {p.Columns.Count(c => c.Diameter > 0)} cột tròn" : "")
                       + (p.Beams.Count > 0 ? $", {p.Beams.Count} dầm vẽ sẵn" : "") + (p.SlabRegions.Count > 0 ? $", {p.SlabRegions.Count(r => !r.IsOpening)} vùng sàn + {p.SlabRegions.Count(r => r.IsOpening)} lỗ sàn vẽ sẵn" : ""));
    }

    /// <summary>Khớp tầng bằng vị trí tường khi không khớp được tên trục: chọn độ dời làm trùng nhiều chiều dài tường nhất
    /// (tường song song phương còn lại), ưu tiên độ dời gần với độ dời góc trái dưới.</summary>
    private static double WallOffset(ArchFloorPlan a, ArchFloorPlan b, bool alongXShift)
    {
        // dời theo X → so tim các tường dọc phương Y (x = const)
        var wa = a.Walls.Where(w => alongXShift ? w.AlongY : w.AlongX).Select(w => (c: alongXShift ? w.Ax : w.Ay, len: w.Length)).ToList();
        var wb = b.Walls.Where(w => alongXShift ? w.AlongY : w.AlongX).Select(w => (c: alongXShift ? w.Ax : w.Ay, len: w.Length)).ToList();
        double corner = alongXShift ? a.X0 - b.X0 : a.Y0 - b.Y0;
        b.Notes.Add("Không khớp được tên trục với tầng 1 – căn vị trí theo tường trùng nhau, kiểm tra lại trên hình xem trước.");
        if (wa.Count == 0 || wb.Count == 0) return corner;
        var cands = ImportGeometry.Cluster(wa.SelectMany(x => wb.Select(y => Math.Round(x.c - y.c, 3))), 0.01);
        double best = corner, bestScore = -1;
        foreach (double d in cands)
        {
            double score = wb.Where(y => wa.Any(x => Math.Abs(x.c - (y.c + d)) < 0.03)).Sum(y => y.len);
            if (score > bestScore + 1e-6 || (Math.Abs(score - bestScore) <= 1e-6 && Math.Abs(d - corner) < Math.Abs(best - corner)))
            { bestScore = score; best = d; }
        }
        return best;
    }

    private static double? MatchOffset(ArchFloorPlan a, ArchFloorPlan b, GridDir dir)
    {
        var ga = a.Grids.Where(g => g.Dir == dir && !g.Auto).ToList(); var gb = b.Grids.Where(g => g.Dir == dir && !g.Auto).ToList();
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
        foreach (var b in p.Beams) { b.Ax = R(b.Ax + dx); b.Bx = R(b.Bx + dx); b.Ay = R(b.Ay + dy); b.By = R(b.By + dy); }
        foreach (var sr in p.SlabRegions)
        {
            sr.Points = sr.Points.Select(q => (R(q.X + dx), R(q.Y + dy))).ToList();
            sr.X0 = R(sr.X0 + dx); sr.X1 = R(sr.X1 + dx); sr.Y0 = R(sr.Y0 + dy); sr.Y1 = R(sr.Y1 + dy);
        }
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

    private static bool IsGridEntity(DxfEntity e, LayerClassifier lc)
    {
        string cat = lc.Of(e.Layer);
        if (cat == CadCategory.Truc) return true;
        if (cat == CadCategory.Dam || cat == CadCategory.San || cat == CadCategory.BoQua) return false;   // tim dầm nét CENTER không phải trục
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

    // ------------------------------------------------------------------
    // Tường xiên, tường cong, cột xoay / cột tròn
    // ------------------------------------------------------------------
    /// <summary>Một cung tròn trên layer tường: tâm, bán kính, góc đầu A0 và góc quét Sw (rad, ngược chiều kim đồng hồ, &gt; 0).</summary>
    private readonly record struct ArcPiece(double Cx, double Cy, double R, double A0, double Sw, string Layer);

    /// <summary>Các đoạn thẳng của thực thể, bỏ các đoạn thuộc cung tròn (DxfEntity.Arcs) – cung được xử lý riêng thành tường cong.</summary>
    private static IEnumerable<Seg2> StraightSegments(IEnumerable<DxfEntity> ents)
    {
        foreach (var e in ents)
        {
            if (e.Type == DxfEntityType.Text || e.Type == DxfEntityType.Circle) continue;
            var pts = e.Points;
            int n = pts.Count;
            var spans = ArcSpansOf(e);
            HashSet<int> onArc = null;
            if (spans.Count > 0)
            {
                onArc = new HashSet<int>();
                foreach (var sp in spans) for (int k = sp.I0; k < sp.I1 && k < n; k++) onArc.Add(k);
            }
            int segs = e.Type == DxfEntityType.Polyline && e.Closed && n > 2 ? n : n - 1;
            for (int k = 0; k < segs; k++)
            {
                if (onArc != null && onArc.Contains(k)) continue;
                var a = pts[k]; var b = pts[(k + 1) % n];
                yield return new Seg2(a.X, a.Y, b.X, b.Y);
            }
        }
    }

    /// <summary>Cung tròn của thực thể: lấy từ DxfEntity.Arcs; thực thể ARC (vd. từ add-in AutoCAD) không có thông tin cung → dựng đường tròn qua các điểm chia.</summary>
    private static List<DxfArcSpan> ArcSpansOf(DxfEntity e)
    {
        var res = new List<DxfArcSpan>();
        if (e.Type != DxfEntityType.Polyline || e.Points.Count < 2) return res;
        if (e.Arcs != null)
        {
            foreach (var a in e.Arcs) if (a != null && a.I0 >= 0 && a.I1 > a.I0 && a.I1 <= e.Points.Count && a.R > 1e-6) res.Add(a);
            return res;
        }
        if (e.SourceType == "ARC" && e.Points.Count >= 3)
        {
            var p = e.Points;
            if (CadCurves.CircleThrough(p[0], p[p.Count / 2], p[^1], out double cx, out double cy, out double r) && r > 1e-3
                && p.All(q => Math.Abs(Math.Sqrt((q.X - cx) * (q.X - cx) + (q.Y - cy) * (q.Y - cy)) - r) <= Math.Max(1e-3, 1e-4 * r)))
                res.Add(new DxfArcSpan { I0 = 0, I1 = p.Count - 1, Cx = cx, Cy = cy, R = r });
        }
        return res;
    }

    /// <summary>Cung tròn (góc đầu, góc quét dương ngược chiều kim đồng hồ) của từng đoạn cung trong thực thể.</summary>
    private static List<ArcPiece> ArcPieces(DxfEntity e)
    {
        var res = new List<ArcPiece>();
        var pts = e.Points;
        int n = pts.Count;
        foreach (var sp in ArcSpansOf(e))
        {
            double prev = Math.Atan2(pts[sp.I0].Y - sp.Cy, pts[sp.I0].X - sp.Cx), a0 = prev, sw = 0;
            for (int k = sp.I0 + 1; k <= sp.I1; k++)
            {
                var q = pts[k % n];
                double a = Math.Atan2(q.Y - sp.Cy, q.X - sp.Cx), d = a - prev;
                while (d > Math.PI) d -= 2 * Math.PI;
                while (d <= -Math.PI) d += 2 * Math.PI;
                sw += d; prev = a;
            }
            if (Math.Abs(sw) < 1e-6) continue;
            if (sw < 0) { a0 += sw; sw = -sw; }
            res.Add(new ArcPiece(sp.Cx, sp.Cy, sp.R, a0, Math.Min(sw, 2 * Math.PI), e.Layer));
        }
        return res;
    }

    /// <summary>Tường cong tim bán kính R từ góc a0, quét sw (rad, &gt; 0): chia thành các đoạn ≤ 180° (để dầm cong có độ vồng ≤ dây cung).</summary>
    private static List<ArchWall> CurvedWalls(double cx, double cy, double r, double a0, double sw, double t, double opening, string layer)
    {
        var res = new List<ArchWall>();
        if (r <= 1e-6 || sw <= 1e-6) return res;
        int k = Math.Max(1, (int)Math.Ceiling(sw / Math.PI - 1e-9));
        for (int i = 0; i < k; i++)
        {
            double s0 = a0 + sw * i / k, s1 = a0 + sw * (i + 1) / k, sm = (s0 + s1) / 2;
            double ax = cx + r * Math.Cos(s0), ay = cy + r * Math.Sin(s0), bx = cx + r * Math.Cos(s1), by = cy + r * Math.Sin(s1);
            double sag = CurvedBeam.SagittaThrough(ax, ay, bx, by, cx + r * Math.Cos(sm), cy + r * Math.Sin(sm));
            if (Math.Abs(sag) < 1e-6) continue;
            res.Add(new ArchWall { Ax = ax, Ay = ay, Bx = bx, By = by, Sagitta = sag, Thickness = t, OpeningLength = opening / k, Layer = layer ?? "" });
        }
        return res;
    }

    /// <summary>Ghép cặp cung đồng tâm (tâm lệch ≤ 20 mm + 0,2 % R) có bán kính chênh minT…maxT → tường cong tim bán kính trung bình,
    /// trên phần góc chung của hai cung; các đoạn cùng đường tròn cách nhau ≤ 1,6 m (theo cung) được nối, khe tính là lỗ cửa.</summary>
    private static List<ArchWall> PairArcs(List<ArcPiece> arcs, double minT, double maxT, out List<ArcPiece> unpaired)
    {
        var pieces = new List<(double cx, double cy, double r, double lo, double hi, double t, string layer)>();
        var usedArc = new bool[arcs.Count];
        var usedPair = new HashSet<(int, int)>();
        for (int i = 0; i < arcs.Count; i++)
        {
            var ai = arcs[i];
            int best = -1; double bestScore = double.MaxValue, bLo = 0, bHi = 0;
            for (int j = 0; j < arcs.Count; j++)
            {
                if (j == i) continue;
                var aj = arcs[j];
                double dc = Math.Sqrt((ai.Cx - aj.Cx) * (ai.Cx - aj.Cx) + (ai.Cy - aj.Cy) * (ai.Cy - aj.Cy));
                if (dc > 0.02 + 0.002 * Math.Max(ai.R, aj.R)) continue;
                double d = Math.Abs(ai.R - aj.R);
                if (d < minT || d > maxT) continue;
                // phần góc chung (thử dời góc ±2π)
                double lo = 0, hi = 0, ov = -1;
                for (int k = -1; k <= 1; k++)
                {
                    double s0 = aj.A0 + 2 * Math.PI * k;
                    double l = Math.Max(ai.A0, s0), h = Math.Min(ai.A0 + ai.Sw, s0 + aj.Sw);
                    if (h - l > ov) { ov = h - l; lo = l; hi = h; }
                }
                double rm = (ai.R + aj.R) / 2;
                if (ov * rm < Math.Min(0.3, 0.5 * Math.Min(ai.Sw * ai.R, aj.Sw * aj.R))) continue;
                double score = StdThick.Min(t => Math.Abs(t - d)) - 0.001 * d;
                if (score < bestScore) { bestScore = score; best = j; bLo = lo; bHi = hi; }
            }
            if (best < 0) continue;
            usedArc[i] = usedArc[best] = true;
            if (!usedPair.Add((Math.Min(i, best), Math.Max(i, best)))) continue;
            var ab = arcs[best];
            pieces.Add(((ai.Cx + ab.Cx) / 2, (ai.Cy + ab.Cy) / 2, (ai.R + ab.R) / 2, bLo, bHi, Math.Round(Math.Abs(ai.R - ab.R), 3), ai.Layer));
        }
        unpaired = arcs.Where((a, k) => !usedArc[k]).ToList();

        // nối các đoạn cùng đường tròn, cùng chiều dày
        var res = new List<ArchWall>();
        var groups = new List<List<(double cx, double cy, double r, double lo, double hi, double t, string layer)>>();
        foreach (var pc in pieces)
        {
            var g = groups.FirstOrDefault(q => Math.Abs(q[0].cx - pc.cx) < 0.02 && Math.Abs(q[0].cy - pc.cy) < 0.02 && Math.Abs(q[0].r - pc.r) < 0.03 && Math.Abs(q[0].t - pc.t) < 0.03);
            if (g == null) groups.Add(new List<(double cx, double cy, double r, double lo, double hi, double t, string layer)> { pc }); else g.Add(pc);
        }
        foreach (var g in groups)
        {
            var f = g[0];
            // đưa góc về cùng vòng với đoạn đầu
            var iv = g.Select(q =>
            {
                double sh = Math.Round((f.lo - q.lo) / (2 * Math.PI)) * 2 * Math.PI;
                return (lo: q.lo + sh, hi: q.hi + sh);
            }).OrderBy(q => q.lo).ToList();
            double r = g.Average(q => q.r), cx = g.Average(q => q.cx), cy = g.Average(q => q.cy), t = g.Max(q => q.t);
            double lo0 = iv[0].lo, hi0 = iv[0].hi, open = 0;
            for (int k = 1; k <= iv.Count; k++)
            {
                if (k < iv.Count)
                {
                    double gap = iv[k].lo - hi0;
                    if (gap <= 1e-6) { hi0 = Math.Max(hi0, iv[k].hi); continue; }
                    if (gap * r <= 1.6) { open += gap * r; hi0 = Math.Max(hi0, iv[k].hi); continue; }
                }
                res.AddRange(CurvedWalls(cx, cy, r, lo0, Math.Min(hi0 - lo0, 2 * Math.PI), t, open, f.layer));
                if (k < iv.Count) { lo0 = iv[k].lo; hi0 = iv[k].hi; open = 0; }
            }
        }
        return res;
    }

    /// <summary>Nhóm nét xiên theo góc phương (độ, 1…179), các nét trong nhóm lệch nhau ≤ tol.</summary>
    private static List<List<(double ang, Seg2 s)>> GroupByAngle(List<(double ang, Seg2 s)> segs, double tol)
    {
        var res = new List<List<(double ang, Seg2 s)>>();
        foreach (var q in segs.OrderBy(q => q.ang))
        {
            if (res.Count > 0 && q.ang - res[^1][0].ang <= tol) res[^1].Add(q);
            else res.Add(new List<(double ang, Seg2 s)> { q });
        }
        return res;
    }

    /// <summary>Đổi tường ghép trong hệ toạ độ quay (u = (ux, uy) dọc tường, n = (−uy, ux)) về hệ toạ độ bản vẽ (sửa tại chỗ).</summary>
    private static ArchWall ToWorld(ArchWall w, double ux, double uy)
    {
        double a = w.Ax, b = w.Bx, c = w.Ay;
        w.Ax = a * ux - c * uy; w.Ay = a * uy + c * ux;
        w.Bx = b * ux - c * uy; w.By = b * uy + c * ux;
        return w;
    }

    /// <summary>Đa tuyến kín là hình chữ nhật xoay (lệch trục &gt; 0,5°): tâm, kích thước theo trục riêng (b theo phương góc ang), góc ang ∈ (−45°, 45°].</summary>
    internal static bool RotatedRect(List<(double X, double Y)> pts, out double cx, out double cy, out double b, out double h, out double ang)
    {
        cx = cy = b = h = ang = 0;
        if (pts == null || pts.Count < 4) return false;
        var q = ImportGeometry.RemoveCollinear(pts.ToList(), 0.002);
        if (q.Count > 4)
        {
            // bỏ đỉnh trùng (đa tuyến khép bằng đỉnh lặp)
            var r2 = new List<(double X, double Y)>();
            foreach (var p in q) if (r2.Count == 0 || Math.Abs(r2[^1].X - p.X) > 1e-6 || Math.Abs(r2[^1].Y - p.Y) > 1e-6) r2.Add(p);
            if (r2.Count > 4 && Math.Abs(r2[0].X - r2[^1].X) < 1e-6 && Math.Abs(r2[0].Y - r2[^1].Y) < 1e-6) r2.RemoveAt(r2.Count - 1);
            q = r2;
        }
        if (q.Count != 4) return false;
        for (int i = 0; i < 4; i++)
        {
            var p0 = q[i]; var p1 = q[(i + 1) % 4]; var p2 = q[(i + 2) % 4];
            double ux = p1.X - p0.X, uy = p1.Y - p0.Y, vx = p2.X - p1.X, vy = p2.Y - p1.Y;
            double lu = Math.Sqrt(ux * ux + uy * uy), lv = Math.Sqrt(vx * vx + vy * vy);
            if (lu < 1e-6 || lv < 1e-6 || Math.Abs(ux * vx + uy * vy) > 0.01 * lu * lv) return false;   // không vuông góc
        }
        double e0x = q[1].X - q[0].X, e0y = q[1].Y - q[0].Y;
        double a = Math.Atan2(e0y, e0x) * 180 / Math.PI;
        double l0 = Math.Sqrt(e0x * e0x + e0y * e0y), l1 = Math.Sqrt((q[2].X - q[1].X) * (q[2].X - q[1].X) + (q[2].Y - q[1].Y) * (q[2].Y - q[1].Y));
        // quy góc về (−45°, 45°]; quay 90° thì đổi cạnh
        while (a > 45) { a -= 90; (l0, l1) = (l1, l0); }
        while (a <= -45) { a += 90; (l0, l1) = (l1, l0); }
        if (Math.Abs(a) <= 0.5) return false;   // gần như song song trục → xử lý như cột thường
        cx = q.Average(p => p.X); cy = q.Average(p => p.Y);
        b = l0; h = l1; ang = a;
        return true;
    }

    /// <summary>Cạnh cột vuông cùng diện tích với cột tròn đường kính D: a = D·√π/2.</summary>
    public static double RoundColumnSide(double diameter) => diameter * Math.Sqrt(Math.PI) / 2;

    /// <summary>Tia ngang (alongX = true, từ (cx, cy) về phía sgn) hoặc tia đứng cắt tường xiên.</summary>
    private static bool DiagRayHit(ArchWall w, double cx, double cy, int sgn, bool alongX)
    {
        if (!w.IsDiagonal) return false;
        if (alongX)
        {
            if (Math.Abs(w.By - w.Ay) < 1e-9 || cy < Math.Min(w.Ay, w.By) - 0.02 || cy > Math.Max(w.Ay, w.By) + 0.02) return false;
            double x = w.Ax + (cy - w.Ay) * (w.Bx - w.Ax) / (w.By - w.Ay);
            return sgn > 0 ? x > cx : x < cx;
        }
        if (Math.Abs(w.Bx - w.Ax) < 1e-9 || cx < Math.Min(w.Ax, w.Bx) - 0.02 || cx > Math.Max(w.Ax, w.Bx) + 0.02) return false;
        double y = w.Ay + (cx - w.Ax) * (w.By - w.Ay) / (w.Bx - w.Ax);
        return sgn > 0 ? y > cy : y < cy;
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
