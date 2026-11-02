using System.Globalization;
using System.Text.RegularExpressions;
using KetCauVXT.Import.Arch;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Acad;

/// <summary>Kết quả chuyển dữ liệu AutoCAD: bản vẽ kiến trúc (đưa vào hộp thoại triển khai kết cấu) + dầm, sàn người dùng đã chỉ định.</summary>
public class AcadLinkResult
{
    public AcadExchange Data { get; set; }
    public ArchDrawing Drawing { get; set; }
    /// <summary>Đoạn dầm (nhóm "Dam") theo hệ toạ độ chung của ArchDrawing, m</summary>
    public List<AcadLinkBeam> Beams { get; } = new();
    /// <summary>Đường bao sàn (nhóm "San") theo hệ toạ độ chung, m</summary>
    public List<AcadLinkSlab> Slabs { get; } = new();
    /// <summary>Số đối tượng theo nhóm (toàn bộ các tầng)</summary>
    public Dictionary<string, int> Counts { get; } = new();

    public override string ToString() =>
        $"{Drawing?.Floors.Count ?? 0} tầng; " + string.Join(", ", Counts.Where(kv => kv.Value > 0).Select(kv => $"{AcadCategory.Display(kv.Key)} {kv.Value}"));
}

public class AcadLinkBeam
{
    public string Storey = "";
    public double Ax, Ay, Bx, By;
    public string Layer = "";
}

public class AcadLinkSlab
{
    public string Storey = "";
    public List<(double X, double Y)> Outline = new();
    public string Layer = "";
}

/// <summary>Đọc file trao đổi AutoCAD (acad_*.json) → mô hình bản vẽ kiến trúc <see cref="ArchDrawing"/>.
/// Cách làm: mỗi đối tượng được đổi thành <see cref="DxfEntity"/> trên lớp tổng hợp theo nhóm (VXT_TUONG, VXT_COT, …),
/// dời về điểm gốc tầng, rồi chạy bộ nhận diện <see cref="ArchPlanReader.FromEntities"/> cho từng tầng với từ khoá lớp
/// gồm các lớp tổng hợp đó. Tên tầng, cao độ, chiều cao tầng lấy theo khai báo trong AutoCAD (lệnh VXT_TANG).</summary>
public static class AcadLinkReader
{
    private const double ArcStepDeg = 10.0;

    public static AcadLinkResult Read(string path, ArchImportOptions o)
    {
        var x = AcadExchange.Load(path);
        var r = Convert(x, o);
        if (string.IsNullOrEmpty(r.Drawing.Source)) r.Drawing.Source = Path.GetFileName(path);
        return r;
    }

    /// <summary>Hệ số đổi đơn vị toạ độ trong file → m.</summary>
    public static double UnitScale(string units) => (units ?? "m").Trim().ToLowerInvariant() switch
    {
        "mm" or "millimeters" or "milimet" => 0.001,
        "cm" or "centimeters" => 0.01,
        "dm" => 0.1,
        "in" or "inch" or "inches" => 0.0254,
        "ft" or "feet" => 0.3048,
        _ => 1.0,
    };

    /// <summary>Bản sao thông số nhận diện với từ khoá lớp có thêm các lớp tổng hợp VXT_*.
    /// Nếu ArchImportOptions có thêm trường BeamLayers / SlabLayers (chuẩn lớp CAD) thì cũng được bổ sung.</summary>
    public static ArchImportOptions PrepareOptions(ArchImportOptions o)
    {
        o ??= new ArchImportOptions();
        var c = new ArchImportOptions();
        foreach (var p in typeof(ArchImportOptions).GetProperties())
            if (p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                p.SetValue(c, p.GetValue(o));
        static string Add(string keys, string layer) =>
            string.IsNullOrWhiteSpace(keys) ? layer
            : keys.Split(',', ';').Any(k => string.Equals(k.Trim(), layer, StringComparison.OrdinalIgnoreCase)) ? keys : layer + "," + keys;
        c.WallLayers = Add(c.WallLayers, AcadCategory.SyntheticLayer(AcadCategory.Tuong));
        c.ColumnLayers = Add(c.ColumnLayers, AcadCategory.SyntheticLayer(AcadCategory.Cot));
        c.GridLayers = Add(c.GridLayers, AcadCategory.SyntheticLayer(AcadCategory.Truc));
        c.DoorLayers = Add(c.DoorLayers, AcadCategory.SyntheticLayer(AcadCategory.Cua));
        c.StairLayers = Add(c.StairLayers, AcadCategory.SyntheticLayer(AcadCategory.Thang));
        foreach (var (prop, cat) in new[] { ("BeamLayers", AcadCategory.Dam), ("SlabLayers", AcadCategory.San), ("RoomLayers", AcadCategory.TenPhong) })
        {
            var pi = typeof(ArchImportOptions).GetProperty(prop);
            if (pi != null && pi.PropertyType == typeof(string) && pi.CanWrite)
                pi.SetValue(c, Add(pi.GetValue(c) as string, AcadCategory.SyntheticLayer(cat)));
        }
        // chuẩn layer CAD (ArchImportOptions.LayerMap: danh sách {Layer, Category}, ưu tiên hơn mọi quy tắc) – gán thẳng lớp tổng hợp → nhóm.
        // Dùng phản chiếu để không phụ thuộc cứng vào kiểu dữ liệu của chuẩn layer.
        var lm = typeof(ArchImportOptions).GetProperty("LayerMap");
        if (lm != null && lm.CanWrite && lm.PropertyType.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(lm.PropertyType))
        {
            var itemType = lm.PropertyType.GetGenericArguments()[0];
            var pl = itemType.GetProperty("Layer"); var pc = itemType.GetProperty("Category");
            if (pl != null && pc != null && itemType.GetConstructor(Type.EmptyTypes) != null
                && Activator.CreateInstance(lm.PropertyType) is System.Collections.IList list)
            {
                foreach (var cat in AcadCategory.All.Where(q => q != AcadCategory.BoQua))
                {
                    var e = Activator.CreateInstance(itemType);
                    pl.SetValue(e, AcadCategory.SyntheticLayer(cat));
                    pc.SetValue(e, cat);
                    list.Add(e);
                }
                if (lm.GetValue(c) is System.Collections.IEnumerable old)
                    foreach (var q in old) if (q != null) list.Add(q);
                lm.SetValue(c, list);   // danh sách mới – không sửa thông số gốc của hộp thoại
            }
        }
        return c;
    }

    // ==================================================================
    // ĐỐI TƯỢNG → DxfEntity (m, toạ độ đã dời về gốc tầng)
    // ==================================================================
    /// <summary>Đổi các đối tượng của một tầng sang thực thể DXF đơn giản hoá (đơn vị m, gốc = điểm gốc tầng).
    /// Cung, đoạn phình của đa tuyến được chia nhỏ (≤ 10°/đoạn); cột vẽ bằng 4 nét rời được ghép thành hình chữ nhật kín.</summary>
    public static List<DxfEntity> ToEntities(AcadStorey s, double unitScale, List<string> warnings = null)
    {
        var res = new List<DxfEntity>();
        double k = unitScale > 0 ? unitScale : 1.0;
        (double X, double Y) P(double[] p) => p == null || p.Length < 2 ? (0, 0) : ((p[0] - s.BaseX) * k, (p[1] - s.BaseY) * k);
        var colLines = new List<DxfEntity>();
        int bad = 0;
        foreach (var it in s.Items ?? new List<AcadItem>())
        {
            string cat = AcadCategory.Normalize(it.Category);
            if (cat == null || cat == AcadCategory.BoQua) continue;
            string layer = AcadCategory.SyntheticLayer(cat);
            var pts = it.Points ?? new List<double[]>();
            string kind = (it.Kind ?? "").Trim().ToLowerInvariant();
            var e = new DxfEntity { Layer = layer, Block = it.Block ?? "" };
            switch (kind)
            {
                case "line":
                    if (pts.Count < 2) { bad++; continue; }
                    e.Type = DxfEntityType.Line;
                    e.Points.Add(P(pts[0])); e.Points.Add(P(pts[1]));
                    if (cat == AcadCategory.Cot) { colLines.Add(e); continue; }
                    break;
                case "polyline":
                case "lwpolyline":
                    if (pts.Count < 2) { bad++; continue; }
                    e.Type = DxfEntityType.Polyline;
                    e.Closed = it.Closed && pts.Count > 2;
                    e.Points.AddRange(BulgePoints(pts.Select(P).ToList(), it.Bulges, it.Closed));
                    if (e.Closed && e.Points.Count > 2 && Same(e.Points[0], e.Points[^1])) e.Points.RemoveAt(e.Points.Count - 1);
                    if (!e.Closed && pts.Count > 2 && Same(e.Points[0], e.Points[^1]) && (it.Bulges == null || it.Bulges.All(b => Math.Abs(b) < 1e-9)))
                    { e.Closed = true; e.Points.RemoveAt(e.Points.Count - 1); }   // đa tuyến khép kín bằng đỉnh trùng
                    break;
                case "circle":
                    if (pts.Count < 1 || it.Radius <= 0) { bad++; continue; }
                    // cột tròn (lớp cột): giữ là hình tròn – bộ nhận diện quy về cột vuông cùng diện tích (cạnh = D·√π/2), ghi đường kính
                    e.Type = DxfEntityType.Circle; e.Radius = it.Radius * k; e.SourceType = "CIRCLE";
                    e.Points.Add(P(pts[0]));
                    break;
                case "arc":
                {
                    if (pts.Count < 1 || it.Radius <= 0) { bad++; continue; }
                    var (cx, cy) = P(pts[0]); double r = it.Radius * k;
                    double a0 = it.StartAngle, sweep = it.EndAngle - it.StartAngle;
                    while (sweep <= 1e-9) sweep += 360;
                    while (sweep > 360 + 1e-9) sweep -= 360;
                    int n = Math.Max(2, (int)Math.Ceiling(sweep / ArcStepDeg));
                    e.Type = DxfEntityType.Polyline; e.SourceType = "ARC";
                    e.Arcs = new List<DxfArcSpan> { new DxfArcSpan { I0 = 0, I1 = n, Cx = cx, Cy = cy, R = r } };   // tường cong: tâm, bán kính đúng
                    for (int i = 0; i <= n; i++)
                    {
                        double a = (a0 + sweep * i / n) * Math.PI / 180;
                        e.Points.Add((cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
                    }
                    break;
                }
                case "text":
                case "mtext":
                case "attrib":
                    if (pts.Count < 1 || string.IsNullOrWhiteSpace(it.Text)) continue;
                    e.Type = DxfEntityType.Text; e.Text = it.Text; e.TextHeight = it.Height * k;
                    e.Points.Add(P(pts[0]));
                    break;
                case "block":
                    // điểm chèn block (hình học đã được add-in phân rã thành các đối tượng riêng mang tên block) – bỏ qua
                    continue;
                default:
                    bad++; continue;
            }
            res.Add(e);
        }
        res.AddRange(ColumnsFromLines(colLines));
        if (bad > 0) warnings?.Add($"{s.Name}: bỏ qua {bad} đối tượng thiếu dữ liệu hoặc loại không hỗ trợ.");
        return res;
    }

    private static bool Same((double X, double Y) a, (double X, double Y) b) => Math.Abs(a.X - b.X) < 1e-6 && Math.Abs(a.Y - b.Y) < 1e-6;

    /// <summary>Chia các đoạn phình (bulge) của đa tuyến thành dãy điểm. Bulge b = tan(θ/4), θ là góc ở tâm (dương = ngược chiều kim đồng hồ).</summary>
    public static List<(double X, double Y)> BulgePoints(List<(double X, double Y)> v, IList<double> bulges, bool closed)
    {
        var res = new List<(double X, double Y)>();
        if (v.Count == 0) return res;
        int segs = closed ? v.Count : v.Count - 1;
        res.Add(v[0]);
        for (int i = 0; i < segs; i++)
        {
            var a = v[i]; var b = v[(i + 1) % v.Count];
            double bu = bulges != null && i < bulges.Count ? bulges[i] : 0;
            if (Math.Abs(bu) > 1e-9)
            {
                double dx = b.X - a.X, dy = b.Y - a.Y, c = Math.Sqrt(dx * dx + dy * dy);
                if (c > 1e-12)
                {
                    double theta = 4 * Math.Atan(bu);
                    double off = c / 2 * (1 - bu * bu) / (2 * bu);       // khoảng cách từ trung điểm dây cung đến tâm (về bên trái dây)
                    double mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2;
                    double nx = -dy / c, ny = dx / c;
                    double cx = mx + nx * off, cy = my + ny * off;
                    double r = Math.Sqrt((a.X - cx) * (a.X - cx) + (a.Y - cy) * (a.Y - cy));
                    double a0 = Math.Atan2(a.Y - cy, a.X - cx);
                    int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(theta) * 180 / Math.PI / ArcStepDeg));
                    for (int k = 1; k < n; k++)
                    {
                        double t = a0 + theta * k / n;
                        res.Add((cx + r * Math.Cos(t), cy + r * Math.Sin(t)));
                    }
                }
            }
            res.Add(b);
        }
        return res;
    }

    /// <summary>Cột vẽ bằng các nét rời: gom nét gần nhau thành cụm, cụm có kích thước 0,12–1,0 m → hình chữ nhật kín.</summary>
    private static IEnumerable<DxfEntity> ColumnsFromLines(List<DxfEntity> lines)
    {
        var used = new bool[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            if (used[i]) continue;
            var grp = new List<int> { i }; used[i] = true;
            for (int g = 0; g < grp.Count; g++)
            {
                var a = lines[grp[g]];
                for (int j = 0; j < lines.Count; j++)
                {
                    if (used[j]) continue;
                    var b = lines[j];
                    if (a.Points.Any(p => b.Points.Any(q => Math.Abs(p.X - q.X) < 0.01 && Math.Abs(p.Y - q.Y) < 0.01)))
                    { used[j] = true; grp.Add(j); }
                }
            }
            var pts = grp.SelectMany(n => lines[n].Points).ToList();
            double x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X), y0 = pts.Min(p => p.Y), y1 = pts.Max(p => p.Y);
            if (grp.Count >= 3 && x1 - x0 >= 0.1 && y1 - y0 >= 0.1 && x1 - x0 <= 1.2 && y1 - y0 <= 1.2)
            {
                var e = new DxfEntity { Type = DxfEntityType.Polyline, Layer = lines[i].Layer, Closed = true, Block = lines[i].Block };
                e.Points.AddRange(new[] { (x0, y0), (x1, y0), (x1, y1), (x0, y1) });
                yield return e;
            }
            else foreach (int n in grp) yield return lines[n];
        }
    }

    // ==================================================================
    // DỮ LIỆU TRAO ĐỔI → ArchDrawing
    // ==================================================================
    public static AcadLinkResult Convert(AcadExchange x, ArchImportOptions o)
    {
        if (x == null) throw new ArgumentNullException(nameof(x));
        var opt = PrepareOptions(o);
        double k = UnitScale(x.Units);
        var res = new AcadLinkResult { Data = x };
        foreach (var c in AcadCategory.All) res.Counts[c] = 0;
        var d = new ArchDrawing { Source = string.IsNullOrEmpty(x.Dwg) ? (x.Source ?? "AutoCAD") : Path.GetFileName(x.Dwg) + (string.IsNullOrEmpty(x.Source) ? "" : " (" + x.Source + ")") };
        res.Drawing = d;
        if (x.Storeys == null || x.Storeys.Count == 0)
        {
            d.Warnings.Add("File dữ liệu AutoCAD không có tầng nào (khai báo tầng bằng lệnh VXT_TANG rồi gửi lại bằng VXT_GUI).");
            return res;
        }
        d.Info.Add($"Dữ liệu từ {(string.IsNullOrEmpty(x.Source) ? "AutoCAD" : x.Source)}: {x.Storeys.Count} tầng, đơn vị \"{x.Units}\" → ×{k.ToString(CultureInfo.InvariantCulture)} (→ m)" +
                   (string.IsNullOrEmpty(x.Dwg) ? "" : ", bản vẽ " + x.Dwg));

        // thứ tự tầng theo cao độ (giữ thứ tự khai báo khi trùng cao độ)
        var storeys = x.Storeys.Select((s, i) => (s, i)).OrderBy(t => t.s.Elevation).ThenBy(t => t.i).Select(t => t.s).ToList();
        var plans = new List<(ArchFloorPlan plan, AcadStorey st, List<DxfEntity> ents)>();
        int floorNo = 0;
        for (int si = 0; si < storeys.Count; si++)
        {
            var st = storeys[si];
            string name = string.IsNullOrWhiteSpace(st.Name) ? $"Tầng {si + 1}" : st.Name.Trim();
            foreach (var it in st.Items ?? new List<AcadItem>())
            {
                string c = AcadCategory.Normalize(it.Category);
                if (c != null) res.Counts[c]++;
            }
            var ents = ToEntities(st, k, d.Warnings);
            var sub = ArchPlanReader.FromEntities(ents, opt, name);
            foreach (var w in sub.Warnings.Where(w => !w.Contains("không có tên", StringComparison.OrdinalIgnoreCase)))
                d.Warnings.Add(name + ": " + w);

            ArchFloorPlan plan;
            if (sub.Floors.Count == 0)
            {
                plan = new ArchFloorPlan { Title = name };
                plan.Notes.Add("Không nhận diện được tường (cần nét nhóm \"Tường\", tổng chiều dài ≥ 8 m).");
            }
            else
            {
                plan = sub.Floors.OrderByDescending(f => f.Walls.Sum(w => w.Length)).First();
                if (sub.Floors.Count > 1)
                    d.Warnings.Add($"{name}: nhận diện được {sub.Floors.Count} cụm tường rời nhau – lấy cụm lớn nhất. Kiểm tra phạm vi tầng (VXT_TANG).");
                Shift(plan, -plan.OffsetX, -plan.OffsetY);   // bỏ phép dời của bộ đọc – dùng điểm gốc người dùng chọn
                plan.Notes.RemoveAll(n => n.Contains("Không đọc được tên tầng", StringComparison.OrdinalIgnoreCase));
            }
            plan.Title = name;
            plan.Include = true;
            string norm = ArchPlanReader.Norm(name);
            plan.IsTum = Regex.IsMatch(norm, @"\bTUM\b");
            plan.IsRoofPlan = !plan.IsTum && Regex.IsMatch(norm, @"\bMAI\b") && !Regex.IsMatch(norm, @"\bTANG\s+\d");
            plan.FloorNo = plan.IsTum ? 90 : plan.IsRoofPlan ? 99 : ++floorNo;

            // chiều cao tầng: khai báo → hiệu cao độ với tầng trên → mặc định
            double h = st.Height;
            if (h <= 0 && si + 1 < storeys.Count) h = storeys[si + 1].Elevation - st.Elevation;
            if (h <= 0.5 || h > 30) h = plan.IsTum ? opt.TumHeight : plan.FloorNo == 1 ? opt.GroundStoreyHeight : opt.TypicalStoreyHeight;
            plan.Height = Math.Round(h, 4);
            plan.Notes.Add($"Tầng khai báo trong AutoCAD: cao độ {st.Elevation:+0.000;-0.000;±0.000} m, chiều cao {plan.Height:0.000} m.");
            plans.Add((plan, st, ents));
        }

        // ---- hệ toạ độ chung: gốc = giao trục X nhỏ nhất – trục Y nhỏ nhất của tầng dưới cùng ----
        var baseT = plans.FirstOrDefault(p => !p.plan.IsRoofPlan && p.plan.Walls.Count > 0);
        if (baseT.plan == null) baseT = plans[0];
        var baseP = baseT.plan;
        double ox0 = baseP.Grids.Where(g => g.Dir == GridDir.X).Select(g => g.Coord).DefaultIfEmpty(0).Min();
        double oy0 = baseP.Grids.Where(g => g.Dir == GridDir.Y).Select(g => g.Coord).DefaultIfEmpty(0).Min();
        // mọi tầng cùng một điểm gốc (chưa chọn điểm gốc riêng) mà phạm vi khác nhau → căn theo tên trục
        bool sameBase = plans.Count > 1 && plans.All(p => Math.Abs(p.st.BaseX - baseT.st.BaseX) < 1e-9 && Math.Abs(p.st.BaseY - baseT.st.BaseY) < 1e-9)
                        && plans.Any(p => Math.Abs(p.st.X0 - baseT.st.X0) > 1e-6 || Math.Abs(p.st.Y0 - baseT.st.Y0) > 1e-6);
        foreach (var (plan, st, ents) in plans)
        {
            double dx = -ox0, dy = -oy0;
            if (sameBase && plan != baseP)
            {
                var mx = MatchOffset(baseP, plan, GridDir.X); var my = MatchOffset(baseP, plan, GridDir.Y);
                if (mx != null) dx += mx.Value;
                if (my != null) dy += my.Value;
                plan.Notes.Add(mx != null && my != null ? "Các tầng chưa có điểm gốc riêng – căn theo tên trục với tầng dưới cùng."
                                                        : "Các tầng chưa có điểm gốc riêng và không khớp được tên trục – kiểm tra vị trí (chọn điểm gốc bằng VXT_TANG).");
            }
            Shift(plan, dx, dy);
            foreach (var e in ents.Where(e => e.Layer == AcadCategory.SyntheticLayer(AcadCategory.Dam) && e.Type != DxfEntityType.Text && e.Type != DxfEntityType.Circle))
                for (int i = 0; i + 1 < e.Points.Count; i++)
                    res.Beams.Add(new AcadLinkBeam { Storey = plan.Title, Ax = R(e.Points[i].X + dx), Ay = R(e.Points[i].Y + dy), Bx = R(e.Points[i + 1].X + dx), By = R(e.Points[i + 1].Y + dy) });
            foreach (var e in ents.Where(e => e.Layer == AcadCategory.SyntheticLayer(AcadCategory.San) && e.Type == DxfEntityType.Polyline && e.Closed))
                res.Slabs.Add(new AcadLinkSlab { Storey = plan.Title, Outline = e.Points.Select(p => (R(p.X + dx), R(p.Y + dy))).ToList() });
        }

        d.Floors = plans.Select(p => p.plan).OrderBy(p => p.FloorNo).ToList();
        foreach (var g in d.Floors.Where(p => p.FloorNo > 0 && p.FloorNo < 90).GroupBy(p => p.FloorNo).Where(g => g.Count() > 1))
            d.Warnings.Add($"Có {g.Count()} tầng cùng số {g.Key} – bỏ chọn bớt.");
        foreach (var p in d.Floors)
            d.Info.Add($"  {p.Title}: {p.Walls.Count} đoạn tường ({p.Walls.Sum(w => w.Length):0.0} m), {p.Grids.Count} trục, {p.Columns.Count} cột KT, {p.Rooms.Count} phòng, {p.Openings.Count} cửa, {p.Zones.Count} vùng đặc biệt"
                       + $", {res.Beams.Count(b => b.Storey == p.Title)} đoạn dầm, {res.Slabs.Count(s => s.Storey == p.Title)} vùng sàn");
        d.Info.Add("Số đối tượng theo nhóm: " + string.Join(", ", res.Counts.Where(kv => kv.Value > 0).Select(kv => $"{AcadCategory.Display(kv.Key)} {kv.Value}")));
        return res;
    }

    private static double? MatchOffset(ArchFloorPlan a, ArchFloorPlan b, GridDir dir)
    {
        var ga = a.Grids.Where(g => g.Dir == dir && !g.Auto).ToList(); var gb = b.Grids.Where(g => g.Dir == dir && !g.Auto).ToList();
        var diffs = new List<double>();
        foreach (var g in ga)
        {
            var h = gb.FirstOrDefault(q => q.Name == g.Name);
            if (h != null) diffs.Add(g.Coord - h.Coord);
        }
        if (diffs.Count == 0) return null;
        double med = diffs.OrderBy(q => q).ElementAt(diffs.Count / 2);
        var ok = diffs.Where(q => Math.Abs(q - med) < 0.05).ToList();
        return ok.Count >= Math.Max(1, diffs.Count / 2) ? ok.Average() : null;
    }

    /// <summary>Phép dời của chính bộ đọc (ArchPlanReader.Shift – dời mọi thành phần của mặt bằng, kể cả phần mới thêm sau này).</summary>
    private static readonly System.Reflection.MethodInfo ReaderShift = typeof(ArchPlanReader).GetMethod("Shift",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
        null, new[] { typeof(ArchFloorPlan), typeof(double), typeof(double) }, null);

    /// <summary>Dời toàn bộ mặt bằng (cùng quy ước với ArchPlanReader); OffsetX/OffsetY cộng dồn.</summary>
    private static void Shift(ArchFloorPlan p, double dx, double dy)
    {
        if (Math.Abs(dx) < 1e-12 && Math.Abs(dy) < 1e-12) return;
        double ox = p.OffsetX + dx, oy = p.OffsetY + dy;
        if (ReaderShift != null)
        {
            try
            {
                ReaderShift.Invoke(null, new object[] { p, dx, dy });
                p.OffsetX = ox; p.OffsetY = oy;
                return;
            }
            catch (System.Reflection.TargetInvocationException) { /* dùng phép dời riêng bên dưới */ }
        }
        p.OffsetX = ox; p.OffsetY = oy;
        foreach (var w in p.Walls) { w.Ax = R(w.Ax + dx); w.Bx = R(w.Bx + dx); w.Ay = R(w.Ay + dy); w.By = R(w.By + dy); }
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
}
