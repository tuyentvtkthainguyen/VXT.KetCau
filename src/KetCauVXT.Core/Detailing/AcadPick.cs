using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace KetCauVXT.Detailing;

// =====================================================================
// LẤY KÍCH THƯỚC TỪ AUTOCAD (add-in lệnh VXT_KT – src/KetCauVXT.AcadLink/PickDims.cs)
// Add-in ghi %AppData%\KetCauVXT\AcadPick\pick_yyyyMMdd_HHmmss_fff.json (đơn vị m):
// { "version":1, "kind":"Dam|Cot|San|Mong|MongBang", "unit":"m", "items":[ { "name", "b", "h", "spans":[…], "L", "B", "colL", "colB",
//   "colX", "colY", "length", "columns":[{"name","x","b"}], "spansX":[…], "spansY":[…], "openings":[{"name","x","y","w","h"}] } ] }
// Chế độ Triển khai hồ sơ đọc file mới nhất → gán cho cấu kiện đang chọn (ApplyTo) hoặc tạo cấu kiện mới (CreateMembers).
// =====================================================================

public class AcadPickColumn
{
    public string Name { get; set; } = "";
    /// <summary>Vị trí tim cột dọc trục băng (m, từ đầu băng)</summary>
    public double X { get; set; }
    /// <summary>Cạnh cột dọc trục băng (m)</summary>
    public double B { get; set; }
}

public class AcadPickOpening
{
    public string Name { get; set; } = "";
    /// <summary>Góc dưới trái lỗ so với giao trục nhỏ nhất (m)</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
}

public class AcadPickItem
{
    public string Name { get; set; } = "";
    public double? B { get; set; }
    public double? H { get; set; }
    public List<double> Spans { get; set; } = new();
    /// <summary>Móng: cạnh L (phương X), B (phương Y)</summary>
    public double? FootL { get; set; }
    public double? FootB { get; set; }
    public double? ColL { get; set; }
    public double? ColB { get; set; }
    /// <summary>Tâm cột so với tâm móng (m, + = sang phải / lên trên)</summary>
    public double? ColX { get; set; }
    public double? ColY { get; set; }
    public double? Length { get; set; }
    public List<AcadPickColumn> Columns { get; set; } = new();
    public List<double> SpansX { get; set; } = new();
    public List<double> SpansY { get; set; } = new();
    public List<AcadPickOpening> Openings { get; set; } = new();
}

public class AcadPickData
{
    public int Version { get; set; } = 1;
    /// <summary>"Dam", "Cot", "San", "Mong", "MongBang"</summary>
    public string Kind { get; set; } = "";
    public List<AcadPickItem> Items { get; set; } = new();
    public string File { get; set; } = "";
    public DateTime Time { get; set; }
    public List<string> Errors { get; set; } = new();

    public string KindTitle => AcadPick.KindTitle(Kind);
}

public static class AcadPick
{
    /// <summary>Thư mục trao đổi (kiểm thử đặt FolderOverride).</summary>
    public static string FolderOverride { get; set; }

    public static string Folder => !string.IsNullOrEmpty(FolderOverride) ? FolderOverride
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KetCauVXT", "AcadPick");

    public static string KindTitle(string kind) => (kind ?? "").ToLowerInvariant() switch
    {
        "dam" => "Dầm", "cot" => "Cột", "san" => "Sàn", "mong" => "Móng đơn", "mongbang" => "Móng băng", _ => "Không rõ"
    };

    // ------------------------------------------------------------------
    // Đọc JSON (không ném lỗi: lỗi ghi vào Errors)
    // ------------------------------------------------------------------
    public static AcadPickData Parse(string json)
    {
        var d = new AcadPickData();
        try
        {
            using var doc = JsonDocument.Parse(json ?? "", new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { d.Errors.Add("File không phải đối tượng JSON"); return d; }
            d.Version = (int)(Num(root, "version") ?? 1);
            d.Kind = Str(root, "kind");
            double k = 1;
            string unit = Str(root, "unit").ToLowerInvariant();
            if (unit == "mm") k = 0.001; else if (unit == "cm") k = 0.01;
            if (Prop(root, "items") is JsonElement items && items.ValueKind == JsonValueKind.Array)
                foreach (var e in items.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Object) continue;
                    var it = new AcadPickItem
                    {
                        Name = Str(e, "name"),
                        B = Scale(Num(e, "b"), k), H = Scale(Num(e, "h"), k),
                        FootL = Scale(Num(e, "L"), k), FootB = Scale(Num(e, "B"), k),
                        ColL = Scale(Num(e, "colL"), k), ColB = Scale(Num(e, "colB"), k),
                        ColX = Scale(Num(e, "colX"), k), ColY = Scale(Num(e, "colY"), k),
                        Length = Scale(Num(e, "length"), k),
                        Spans = Nums(e, "spans", k), SpansX = Nums(e, "spansX", k), SpansY = Nums(e, "spansY", k)
                    };
                    if (Prop(e, "columns") is JsonElement cs && cs.ValueKind == JsonValueKind.Array)
                        foreach (var c in cs.EnumerateArray())
                            if (c.ValueKind == JsonValueKind.Object)
                                it.Columns.Add(new AcadPickColumn { Name = Str(c, "name"), X = (Num(c, "x") ?? 0) * k, B = (Num(c, "b") ?? 0) * k });
                    if (Prop(e, "openings") is JsonElement os && os.ValueKind == JsonValueKind.Array)
                        foreach (var o in os.EnumerateArray())
                            if (o.ValueKind == JsonValueKind.Object)
                                it.Openings.Add(new AcadPickOpening { Name = Str(o, "name"), X = (Num(o, "x") ?? 0) * k, Y = (Num(o, "y") ?? 0) * k, W = (Num(o, "w") ?? 0) * k, H = (Num(o, "h") ?? 0) * k });
                    d.Items.Add(it);
                }
            if (d.Items.Count == 0) d.Errors.Add("File không có đối tượng nào (items rỗng)");
        }
        catch (JsonException ex) { d.Errors.Add("JSON lỗi: " + ex.Message); }
        catch (Exception ex) { d.Errors.Add("Không đọc được: " + ex.Message); }
        return d;
    }

    private static double? Scale(double? v, double k) => v.HasValue ? v.Value * k : null;

    /// <summary>Tìm thuộc tính không phân biệt hoa thường; ưu tiên khớp đúng (vì "L"/"l", "B"/"b" khác nghĩa).</summary>
    private static JsonElement? Prop(JsonElement e, string name)
    {
        if (e.TryGetProperty(name, out var v)) return v;
        foreach (var p in e.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) && name.Length > 1) return p.Value;
        return null;
    }

    private static string Str(JsonElement e, string name) =>
        Prop(e, name) is JsonElement v ? v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ValueKind == JsonValueKind.Number ? v.GetRawText() : "" : "";

    private static double? Num(JsonElement e, string name)
    {
        if (Prop(e, name) is not JsonElement v) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out double d) && double.IsFinite(d)) return d;
        if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString()?.Replace(',', '.'), System.Globalization.NumberStyles.Float, Dt.Inv, out double s) && double.IsFinite(s)) return s;
        return null;
    }

    private static List<double> Nums(JsonElement e, string name, double k)
    {
        var l = new List<double>();
        if (Prop(e, name) is JsonElement a && a.ValueKind == JsonValueKind.Array)
            foreach (var x in a.EnumerateArray())
                if (x.ValueKind == JsonValueKind.Number && x.TryGetDouble(out double d) && double.IsFinite(d) && d > 0) l.Add(d * k);
        return l;
    }

    /// <summary>Đọc một file; lỗi IO ghi vào Errors.</summary>
    public static AcadPickData Load(string path)
    {
        AcadPickData d;
        try { d = Parse(System.IO.File.ReadAllText(path)); }
        catch (Exception ex) { d = new AcadPickData(); d.Errors.Add("Không đọc được file: " + ex.Message); }
        d.File = path;
        try { d.Time = System.IO.File.GetLastWriteTime(path); } catch { }
        return d;
    }

    /// <summary>File pick_*.json mới nhất (mới hơn newerThan nếu có); null nếu không có.</summary>
    public static AcadPickData Latest(DateTime? newerThan = null)
    {
        try
        {
            if (!Directory.Exists(Folder)) return null;
            var f = new DirectoryInfo(Folder).GetFiles("pick_*.json")
                .OrderByDescending(x => x.LastWriteTime).ThenByDescending(x => x.Name).FirstOrDefault();
            if (f == null || (newerThan.HasValue && f.LastWriteTime <= newerThan.Value)) return null;
            return Load(f.FullName);
        }
        catch { return null; }
    }

    // ------------------------------------------------------------------
    // Gán vào cấu kiện
    // ------------------------------------------------------------------
    private static string SpanText(IEnumerable<double> v) => string.Join("; ", v.Select(x => Dt.F(Math.Round(x, 3), "0.###")));

    /// <summary>Gán kích thước một đối tượng AutoCAD cho cấu kiện; trả về các dòng mô tả đã gán (trống = không gán được gì).</summary>
    public static List<string> ApplyTo(IDetailMember m, AcadPickItem it)
    {
        var log = new List<string>();
        if (m == null || it == null) return log;
        void Name() { if (!string.IsNullOrWhiteSpace(it.Name)) { m.Name = it.Name.Trim(); log.Add("tên " + m.Name); } }
        switch (m)
        {
            case ManualBeam b:
                Name();
                if (it.B > 0) { b.B = it.B.Value; log.Add("b = " + Dt.Mm(b.B) + " mm"); }
                if (it.H > 0) { b.H = it.H.Value; log.Add("h = " + Dt.Mm(b.H) + " mm"); }
                if (it.Spans.Count > 0) { b.Spans = SpanText(it.Spans); log.Add("nhịp " + b.Spans + " m"); }
                if (it.Columns.Count > 0 && it.Columns.Max(c => c.B) > 0) { b.SupportWidth = it.Columns.Max(c => c.B); log.Add("bề rộng gối " + Dt.Mm(b.SupportWidth) + " mm"); }
                break;
            case ManualColumn c:
                Name();
                if (it.B > 0) { c.B = it.B.Value; log.Add("b = " + Dt.Mm(c.B) + " mm"); }
                if (it.H > 0) { c.H = it.H.Value; log.Add("h = " + Dt.Mm(c.H) + " mm"); }
                break;
            case ManualSlab s:
            {
                Name();
                double lx = it.SpansX.Count > 0 ? it.SpansX[0] : it.FootL ?? it.B ?? 0;
                double ly = it.SpansY.Count > 0 ? it.SpansY[0] : it.FootB ?? it.H ?? 0;
                if (lx > 0) { s.Lx = lx; log.Add("Lx = " + Dt.F(lx) + " m"); }
                if (ly > 0) { s.Ly = ly; log.Add("Ly = " + Dt.F(ly) + " m"); }
                if (it.SpansX.Count > 1 || it.SpansY.Count > 1) log.Add("chỉ lấy ô đầu tiên – dùng \"Sàn theo mặt bằng\" để nhận đủ " + Math.Max(it.SpansX.Count, 1) * Math.Max(it.SpansY.Count, 1) + " ô");
                break;
            }
            case ManualFooting f:
                Name();
                if (it.FootL > 0) { f.L = it.FootL.Value; log.Add("L = " + Dt.F(f.L) + " m"); }
                if (it.FootB > 0) { f.B = it.FootB.Value; log.Add("B = " + Dt.F(f.B) + " m"); }
                if (it.ColL > 0) { f.ColL = it.ColL.Value; log.Add("cột bc = " + Dt.Mm(f.ColL) + " mm"); }
                if (it.ColB > 0) { f.ColB = it.ColB.Value; log.Add("cột hc = " + Dt.Mm(f.ColB) + " mm"); }
                SetOptional(f, "ColOffsetX", it.ColX, log, "lệch tâm cột X");
                SetOptional(f, "ColOffsetY", it.ColY, log, "lệch tâm cột Y");
                break;
            case ManualPileCap pc:
                Name();
                if (it.ColL > 0) { pc.ColX = it.ColL.Value; log.Add("cột bc = " + Dt.Mm(pc.ColX) + " mm"); }
                if (it.ColB > 0) { pc.ColY = it.ColB.Value; log.Add("cột hc = " + Dt.Mm(pc.ColY) + " mm"); }
                break;
            case ManualStrip st:
                Name();
                double len = it.Length ?? (it.Spans.Count > 0 ? it.Spans.Sum() : 0);
                if (len > 0) { st.Length = len; log.Add("chiều dài băng " + Dt.F(len) + " m"); }
                if (it.B > 0) { st.B = it.B.Value; log.Add("bề rộng bản " + Dt.F(st.B) + " m"); }
                if (it.Columns.Count > 0)
                {
                    var old = st.Columns ?? new List<StripColumn>();
                    var cols = new List<StripColumn>();
                    for (int i = 0; i < it.Columns.Count; i++)
                    {
                        var c = it.Columns[i];
                        var prev = i < old.Count ? old[i] : null;   // giữ nội lực đã nhập theo thứ tự
                        cols.Add(new StripColumn
                        {
                            Name = string.IsNullOrWhiteSpace(c.Name) ? "C" + (i + 1) : c.Name, X = c.X, Bc = c.B > 0 ? c.B : prev?.Bc ?? 0.22,
                            N = prev?.N ?? 0, M = prev?.M ?? 0, Ntc = prev?.Ntc ?? 0
                        });
                    }
                    st.Columns = cols;
                    log.Add(cols.Count + " cột tại x = " + string.Join("; ", cols.Select(c => Dt.F(c.X))) + " m" + (cols.Any(c => c.N <= 0) ? " – nhập N cho cột mới" : ""));
                }
                break;
            default:
                ApplyReflection(m, it, log);
                break;
        }
        return log;
    }

    private static void SetOptional(object target, string prop, double? v, List<string> log, string label)
    {
        if (!v.HasValue) return;
        var p = target.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
        if (p == null || !p.CanWrite || p.PropertyType != typeof(double)) return;
        p.SetValue(target, v.Value);
        log.Add(label + " " + Dt.Mm(v.Value) + " mm");
    }

    /// <summary>Sàn theo mặt bằng (ManualSlabPlan – SlabPlanDesigner.cs) và kiểu khác: gán theo tên thuộc tính SpansX, SpansY, Openings, B, H, Lx, Ly.</summary>
    private static void ApplyReflection(IDetailMember m, AcadPickItem it, List<string> log)
    {
        var t = m.GetType();
        PropertyInfo P(string n) => t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
        if (!string.IsNullOrWhiteSpace(it.Name)) { m.Name = it.Name.Trim(); log.Add("tên " + m.Name); }
        void SetText(string n, List<double> v, string label)
        {
            var p = P(n);
            if (p == null || !p.CanWrite || v.Count == 0) return;
            if (p.PropertyType == typeof(string)) { p.SetValue(m, SpanText(v)); log.Add(label + " " + SpanText(v) + " m"); }
        }
        SetText("SpansX", it.SpansX, "nhịp X");
        SetText("SpansY", it.SpansY, "nhịp Y");
        var po = P("Openings");
        if (po != null && it.Openings.Count > 0 && po.GetValue(m) is IList list && po.PropertyType.IsGenericType)
        {
            var et = po.PropertyType.GetGenericArguments()[0];
            list.Clear();
            int i = 1;
            foreach (var o in it.Openings)
            {
                var row = Activator.CreateInstance(et);
                void S(string n, object v) { var p = et.GetProperty(n); if (p != null && p.CanWrite) p.SetValue(row, v); }
                S("Name", string.IsNullOrWhiteSpace(o.Name) ? "L" + i : o.Name);
                S("X", o.X); S("Y", o.Y); S("W", o.W); S("H", o.H);
                list.Add(row);
                i++;
            }
            log.Add(it.Openings.Count + " lỗ thủng sàn");
        }
        foreach (var (n, v) in new[] { ("B", it.B), ("H", it.H), ("Lx", it.FootL), ("Ly", it.FootB) })
            SetOptional(m, n, v, log, n + " =");
    }

    // ------------------------------------------------------------------
    // Tạo cấu kiện mới
    // ------------------------------------------------------------------
    public static Type SlabPlanType => Type.GetType("KetCauVXT.Detailing.ManualSlabPlan") ?? typeof(AcadPick).Assembly.GetType("KetCauVXT.Detailing.ManualSlabPlan");

    /// <summary>Tạo cấu kiện mới theo loại đối tượng đã chọn trong AutoCAD (đã gán kích thước).</summary>
    public static List<IDetailMember> CreateMembers(AcadPickData d)
    {
        var res = new List<IDetailMember>();
        if (d == null) return res;
        string kind = (d.Kind ?? "").ToLowerInvariant();
        int i = 1;
        foreach (var it in d.Items)
        {
            if (kind == "san" && SlabPlanType != null && Activator.CreateInstance(SlabPlanType) is IDetailMember plan)
            {
                if (string.IsNullOrWhiteSpace(it.Name)) plan.Name = "SMB" + i;
                ApplyTo(plan, it);
                res.Add(plan);
            }
            else if (kind == "san")
            {
                // không có sàn theo mặt bằng → mỗi ô một ManualSlab
                var xs = it.SpansX.Count > 0 ? it.SpansX : new List<double> { it.FootL ?? it.B ?? 3.6 };
                var ys = it.SpansY.Count > 0 ? it.SpansY : new List<double> { it.FootB ?? it.H ?? 4.2 };
                int k = 1;
                foreach (double y in ys)
                    foreach (double x in xs)
                        res.Add(new ManualSlab { Name = (string.IsNullOrWhiteSpace(it.Name) ? "S" : it.Name.Trim() + "-") + k++, Lx = x, Ly = y });
            }
            else
            {
                IDetailMember m = kind switch
                {
                    "dam" => new ManualBeam { Name = "D" + i }, "cot" => new ManualColumn { Name = "C" + i },
                    "mong" => new ManualFooting { Name = "M" + i }, "mongbang" => new ManualStrip { Name = "MBG" + i }, _ => null
                };
                if (m == null) continue;
                ApplyTo(m, it);
                res.Add(m);
            }
            i++;
        }
        return res;
    }
}

/// <summary>Hình học thuần (dùng chung cho kiểm thử; add-in PickDims.cs có bản sao C# 7.3 cùng thuật toán).</summary>
public static class AcadPickGeometry
{
    /// <summary>Gộp các toạ độ gần nhau (≤ tol), sắp xếp tăng dần (lấy trung bình mỗi cụm).</summary>
    public static List<double> Merge(IEnumerable<double> v, double tol)
    {
        var s = v.OrderBy(x => x).ToList();
        var res = new List<double>();
        int i = 0;
        while (i < s.Count)
        {
            int j = i; double sum = 0;
            while (j < s.Count && s[j] - s[i] <= tol) { sum += s[j]; j++; }
            res.Add(sum / (j - i));
            i = j;
        }
        return res;
    }

    /// <summary>Khoảng cách giữa các vị trí trục liên tiếp (sau khi gộp sai số tol).</summary>
    public static List<double> Spacings(IEnumerable<double> positions, double tol)
    {
        var m = Merge(positions, tol);
        var r = new List<double>();
        for (int i = 1; i < m.Count; i++) r.Add(m[i] - m[i - 1]);
        return r;
    }

    /// <summary>Chiếu điểm (x, y) lên trục polyline (danh sách đỉnh): trả về toạ độ cong s (m dọc trục) và khoảng cách tới trục.</summary>
    public static (double S, double Dist) Project(IList<(double X, double Y)> axis, double x, double y)
    {
        double best = double.MaxValue, bestS = 0, acc = 0;
        for (int i = 0; i + 1 < axis.Count; i++)
        {
            double ax = axis[i].X, ay = axis[i].Y, bx = axis[i + 1].X, by = axis[i + 1].Y;
            double dx = bx - ax, dy = by - ay, L2 = dx * dx + dy * dy, L = Math.Sqrt(L2);
            double t = L2 < 1e-18 ? 0 : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / L2, 0, 1);
            double px = ax + t * dx, py = ay + t * dy, d = Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
            if (d < best) { best = d; bestS = acc + t * L; }
            acc += L;
        }
        return (bestS, best);
    }

    public static double Length(IList<(double X, double Y)> axis)
    {
        double s = 0;
        for (int i = 0; i + 1 < axis.Count; i++) s += Math.Sqrt(Math.Pow(axis[i + 1].X - axis[i].X, 2) + Math.Pow(axis[i + 1].Y - axis[i].Y, 2));
        return s;
    }

    /// <summary>Nhịp dầm: gối = tâm các hình chữ nhật cột nằm sát trục (khoảng cách ≤ maxDist); không có cột → các đỉnh polyline là gối.</summary>
    public static List<double> BeamSpans(IList<(double X, double Y)> axis, IEnumerable<(double Cx, double Cy)> columnCentres, double maxDist, double tol)
    {
        var ss = new List<double>();
        foreach (var (cx, cy) in columnCentres ?? Enumerable.Empty<(double, double)>())
        {
            var (s, d) = Project(axis, cx, cy);
            if (d <= maxDist) ss.Add(s);
        }
        if (ss.Count < 2)
        {
            ss.Clear();
            double acc = 0; ss.Add(0);
            for (int i = 0; i + 1 < axis.Count; i++) { acc += Math.Sqrt(Math.Pow(axis[i + 1].X - axis[i].X, 2) + Math.Pow(axis[i + 1].Y - axis[i].Y, 2)); ss.Add(acc); }
        }
        return Spacings(ss, tol).Where(x => x > tol).ToList();
    }

    /// <summary>Hình chữ nhật bao (x0, y0, x1, y1) → lỗ so với gốc (ox, oy): (X, Y, W, H).</summary>
    public static (double X, double Y, double W, double H) Opening(double x0, double y0, double x1, double y1, double ox, double oy) =>
        (Math.Min(x0, x1) - ox, Math.Min(y0, y1) - oy, Math.Abs(x1 - x0), Math.Abs(y1 - y0));

    /// <summary>Đoán hệ số đổi đơn vị khi bản vẽ không khai báo $INSUNITS: kích thước điển hình (m) &lt; 0,02 → bản vẽ vẽ theo m?
    /// Quy ước: cạnh điển hình (đơn vị bản vẽ) ≥ 50 → mm (0,001); 0,05…50 → m (1).</summary>
    public static double GuessUnit(double typicalSize) => typicalSize >= 50 ? 0.001 : 1.0;
}
