using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KetCauVXT.Model;

// =====================================================================
// THƯ VIỆN 3D (đồ nội thất, thiết bị vệ sinh, đèn, cửa…) – cơ sở dữ liệu thiết kế kiến trúc / điện nước.
// Nhập từ SketchUp (File → Export → 3D Model → OBJ hoặc COLLADA .dae) hoặc từ Revit (add-in xuất họ thành *.vxtmesh.json).
// Lưu CỤC BỘ trên máy: %AppData%\VietConst\ThuVien3D\<Nhóm>\<tên>.vxtlib.json – không gửi đi đâu.
// =====================================================================
public sealed class LibraryItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Nhóm: "Nội thất", "Thiết bị vệ sinh", "Đèn", "Điện", "Cửa", "Khác"</summary>
    public string Category { get; set; } = "Khác";
    /// <summary>Nguồn: "SketchUp OBJ", "SketchUp DAE", "Revit", "Tạo trong chương trình"</summary>
    public string Source { get; set; } = "";
    /// <summary>Đỉnh [x,y,z] (m, gốc tại tâm đáy)</summary>
    public List<double[]> Vertices { get; set; } = new();
    public List<int[]> Faces { get; set; } = new();
    public double Width { get; set; }
    public double Depth { get; set; }
    public double Height { get; set; }
    /// <summary>Bộ môn mặc định khi đặt (Kiến trúc cho nội thất, Điện nước cho thiết bị vệ sinh / điện)</summary>
    public Discipline Discipline { get; set; } = Discipline.KienTruc;
    /// <summary>Ký hiệu mặt bằng (đường bao hình chiếu bằng của lưới): các vòng [u, v] tỷ lệ theo Rộng / Sâu (−0,5…0,5)
    /// để đổi kích thước vẫn đúng hình. Tự tính khi nhập / nạp.</summary>
    public List<List<double[]>> PlanSymbol { get; set; }
}

public static class Library3D
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Thư mục thư viện mặc định (cục bộ).</summary>
    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VietConst", "ThuVien3D");

    /// <summary>Đọc OBJ (v, f; mặt đa giác tách quạt tam giác). scale: hệ số đổi đơn vị (SketchUp xuất inch → 0.0254, mm → 0.001).</summary>
    public static LibraryItem ReadObj(string text, string name, double scale = 1.0)
    {
        var it = new LibraryItem { Name = name, Source = "SketchUp OBJ" };
        foreach (var raw in text.Split('\n'))
        {
            var l = raw.Trim();
            if (l.StartsWith("v "))
            {
                var t = l.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (t.Length >= 4)
                    it.Vertices.Add(new[] { double.Parse(t[1], Inv) * scale, double.Parse(t[2], Inv) * scale, double.Parse(t[3], Inv) * scale });
            }
            else if (l.StartsWith("f "))
            {
                var t = l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)
                    .Select(x => int.Parse(x.Split('/')[0], Inv)).Select(i => i < 0 ? it.Vertices.Count + i : i - 1).ToList();
                for (int k = 1; k + 1 < t.Count; k++) it.Faces.Add(new[] { t[0], t[k], t[k + 1] });
            }
        }
        Normalize(it);
        return it;
    }

    /// <summary>Đọc COLLADA (.dae) đơn giản: mọi float_array vị trí + triangles/polylist (bỏ qua biến đổi node). Đơn vị theo &lt;unit meter&gt;.</summary>
    public static LibraryItem ReadDae(string xml, string name)
    {
        var it = new LibraryItem { Name = name, Source = "SketchUp DAE" };
        double unit = 1;
        var um = Regex.Match(xml, "<unit[^>]*meter=\"([0-9.eE+-]+)\"");
        if (um.Success) unit = double.Parse(um.Groups[1].Value, Inv);
        foreach (Match g in Regex.Matches(xml, "<mesh>(.*?)</mesh>", RegexOptions.Singleline))
        {
            string mesh = g.Groups[1].Value;
            var vert = Regex.Match(mesh, "<vertices[^>]*>.*?<input[^>]*semantic=\"POSITION\"[^>]*source=\"#([^\"]+)\"", RegexOptions.Singleline);
            string posSrc = vert.Success ? vert.Groups[1].Value : null;
            Match arr = posSrc == null ? Regex.Match(mesh, "<float_array[^>]*>([^<]*)</float_array>")
                : Regex.Match(mesh, $"<source[^>]*id=\"{Regex.Escape(posSrc)}\"[^>]*>.*?<float_array[^>]*>([^<]*)</float_array>", RegexOptions.Singleline);
            if (!arr.Success) continue;
            var nums = arr.Groups[1].Value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Select(x => double.Parse(x, Inv)).ToList();
            int baseV = it.Vertices.Count;
            for (int k = 0; k + 2 < nums.Count; k += 3) it.Vertices.Add(new[] { nums[k] * unit, nums[k + 1] * unit, nums[k + 2] * unit });
            foreach (Match tr in Regex.Matches(mesh, "<(triangles|polylist)([^>]*)>(.*?)</\\1>", RegexOptions.Singleline))
            {
                string body = tr.Groups[3].Value;
                int stride = Regex.Matches(body, "<input[^>]*offset=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max() + 1;
                int vOff = 0;
                var vin = Regex.Match(body, "<input[^>]*semantic=\"VERTEX\"[^>]*offset=\"(\\d+)\"");
                if (vin.Success) vOff = int.Parse(vin.Groups[1].Value);
                var pm = Regex.Match(body, "<p>([^<]*)</p>");
                if (!pm.Success) continue;
                var idx = pm.Groups[1].Value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                var vs = new List<int>();
                for (int k = vOff; k < idx.Count; k += stride) vs.Add(baseV + idx[k]);
                if (tr.Groups[1].Value == "triangles")
                    for (int k = 0; k + 2 < vs.Count; k += 3) it.Faces.Add(new[] { vs[k], vs[k + 1], vs[k + 2] });
                else
                {
                    var vc = Regex.Match(body, "<vcount>([^<]*)</vcount>");
                    var counts = vc.Success ? vc.Groups[1].Value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList() : new List<int>();
                    int at = 0;
                    foreach (int c in counts)
                    {
                        for (int k = 1; k + 1 < c; k++) if (at + k + 1 < vs.Count) it.Faces.Add(new[] { vs[at], vs[at + k], vs[at + k + 1] });
                        at += c;
                    }
                }
            }
        }
        Normalize(it);
        return it;
    }

    /// <summary>Ký hiệu mặt bằng: hình chiếu bằng các tam giác lên lưới 64 × 64 trong hộp bao → các vòng biên (tỷ lệ −0,5…0,5).</summary>
    public static List<List<double[]>> ComputePlanSymbol(LibraryItem it, int n = 64)
    {
        if (it.Vertices.Count < 3 || it.Faces.Count == 0 || it.Width <= 1e-6 || it.Depth <= 1e-6) return null;
        double x0 = it.Vertices.Min(v => v[0]), y0 = it.Vertices.Min(v => v[1]);
        double W = it.Vertices.Max(v => v[0]) - x0, D = it.Vertices.Max(v => v[1]) - y0;
        if (W <= 1e-9 || D <= 1e-9) return null;
        var cells = new bool[n, n];
        foreach (var f in it.Faces)
        {
            if (f.Length < 3 || f.Any(i => i < 0 || i >= it.Vertices.Count)) continue;
            var a = it.Vertices[f[0]]; var b = it.Vertices[f[1]]; var c = it.Vertices[f[2]];
            double ax = (a[0] - x0) / W * n, ay = (a[1] - y0) / D * n, bx = (b[0] - x0) / W * n, by = (b[1] - y0) / D * n, cx = (c[0] - x0) / W * n, cy = (c[1] - y0) / D * n;
            double area = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay);
            int i0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)))), i1 = Math.Min(n - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
            int j0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy)))), j1 = Math.Min(n - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    if (cells[i, j]) continue;
                    double px = i + 0.5, py = j + 0.5;
                    if (Math.Abs(area) < 1e-12)
                    {   // tam giác đứng (mặt bên): đánh dấu ô mà cạnh đi qua
                        double dmin = new[] { (ax, ay, bx, by), (bx, by, cx, cy), (cx, cy, ax, ay) }.Min(e => SegDist(px, py, e.Item1, e.Item2, e.Item3, e.Item4));
                        if (dmin <= 0.5) cells[i, j] = true;
                        continue;
                    }
                    double d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by), d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy), d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) cells[i, j] = true;
                }
        }
        var loops = RasterOutline.Loops(cells, 0, 0, 1);
        var res = loops.Where(l => Math.Abs(RasterOutline.SignedArea(l)) >= 2)
            .Select(l => l.Select(q => new[] { Math.Round(q.X / n - 0.5, 4), Math.Round(q.Y / n - 0.5, 4) }).ToList()).ToList();
        return res.Count > 0 ? res : null;
    }

    private static double SegDist(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
        double t = l2 < 1e-12 ? 0 : Math.Clamp(((px - ax) * dx + (py - ay) * dy) / l2, 0, 1);
        return Math.Sqrt(Math.Pow(px - ax - t * dx, 2) + Math.Pow(py - ay - t * dy, 2));
    }

    /// <summary>Đọc lưới 3D từ DXF (AutoCAD, 3ds Max, Revit, SketchUp xuất DXF): 3DFACE, POLYLINE lưới đa diện (polyface, cờ 64),
    /// MESH (AcDbSubDMesh). Đơn vị theo $INSUNITS (mm → m…), scale &gt; 0 thì dùng scale.</summary>
    public static LibraryItem ReadDxf3D(string text, string name, double scale = 0)
    {
        var it = new LibraryItem { Name = name, Source = "DXF 3D" };
        var lines = text.Replace("\r", "").Split('\n');
        var pairs = new List<(int Code, string Val)>();
        for (int i = 0; i + 1 < lines.Length; i += 2)
            if (int.TryParse(lines[i].Trim(), out int code)) pairs.Add((code, lines[i + 1].Trim()));
        double D(string v) => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0;
        // đơn vị
        double k = scale;
        if (k <= 0)
        {
            int iu = pairs.FindIndex(q => q.Code == 9 && q.Val == "$INSUNITS");
            int u = iu >= 0 && iu + 1 < pairs.Count ? (int)D(pairs[iu + 1].Val) : 0;
            k = u switch { 1 => 0.0254, 2 => 0.3048, 4 => 0.001, 5 => 0.01, 6 => 1, _ => 0 };
        }
        int ent = pairs.FindIndex(q => q.Code == 2 && q.Val == "ENTITIES");
        if (ent < 0) ent = 0;
        var V = it.Vertices; var F = it.Faces;
        int Add(double x, double y, double z) { V.Add(new[] { x, y, z }); return V.Count - 1; }
        int idx = ent;
        while (idx < pairs.Count)
        {
            var (c0, v0) = pairs[idx];
            if (c0 != 0) { idx++; continue; }
            if (v0 == "ENDSEC" && idx > ent) break;
            int end = idx + 1;
            while (end < pairs.Count && pairs[end].Code != 0) end++;
            var g = pairs.GetRange(idx + 1, end - idx - 1);
            if (v0 == "3DFACE")
            {
                double G(int code) => D(g.FirstOrDefault(q => q.Code == code).Val ?? "0");
                var pts = new[] { (G(10), G(20), G(30)), (G(11), G(21), G(31)), (G(12), G(22), G(32)), (G(13), G(23), G(33)) };
                int a = Add(pts[0].Item1, pts[0].Item2, pts[0].Item3), b = Add(pts[1].Item1, pts[1].Item2, pts[1].Item3), c = Add(pts[2].Item1, pts[2].Item2, pts[2].Item3);
                F.Add(new[] { a, b, c });
                bool quad = Math.Abs(pts[3].Item1 - pts[2].Item1) + Math.Abs(pts[3].Item2 - pts[2].Item2) + Math.Abs(pts[3].Item3 - pts[2].Item3) > 1e-9;
                if (quad) F.Add(new[] { a, c, Add(pts[3].Item1, pts[3].Item2, pts[3].Item3) });
                idx = end;
                continue;
            }
            if (v0 == "POLYLINE")
            {
                int flags = (int)D(g.FirstOrDefault(q => q.Code == 70).Val ?? "0");
                int j = end;
                var pv = new List<int>();
                while (j < pairs.Count && !(pairs[j].Code == 0 && pairs[j].Val == "SEQEND"))
                {
                    if (pairs[j].Code == 0 && pairs[j].Val == "VERTEX")
                    {
                        int e2 = j + 1; while (e2 < pairs.Count && pairs[e2].Code != 0) e2++;
                        var vg = pairs.GetRange(j + 1, e2 - j - 1);
                        double VG(int code) => D(vg.FirstOrDefault(q => q.Code == code).Val ?? "0");
                        int vf = (int)VG(70);
                        if ((flags & 64) != 0)
                        {
                            if ((vf & 64) != 0) pv.Add(Add(VG(10), VG(20), VG(30)));   // đỉnh lưới
                            else if ((vf & 128) != 0)
                            {   // bản ghi mặt: chỉ số 71…74 (từ 1, âm = cạnh ẩn)
                                var ids = new[] { 71, 72, 73, 74 }.Select(cc => (int)Math.Abs(VG(cc))).Where(q => q > 0 && q <= pv.Count).Select(q => pv[q - 1]).ToList();
                                for (int t = 1; t + 1 < ids.Count; t++) F.Add(new[] { ids[0], ids[t], ids[t + 1] });
                            }
                        }
                        j = e2;
                        continue;
                    }
                    j++;
                }
                idx = j + 1;
                continue;
            }
            if (v0 == "MESH")
            {
                // 92 = số đỉnh, tiếp theo các cặp 10/20/30; 93 = độ dài danh sách mặt, các 90: [n, i1…in]
                var mv = new List<int>();
                int p92 = g.FindIndex(q => q.Code == 92);
                if (p92 >= 0)
                {
                    int nv = (int)D(g[p92].Val), t = p92 + 1;
                    while (mv.Count < nv && t + 2 < g.Count)
                    {
                        if (g[t].Code == 10 && g[t + 1].Code == 20 && g[t + 2].Code == 30) { mv.Add(Add(D(g[t].Val), D(g[t + 1].Val), D(g[t + 2].Val))); t += 3; }
                        else t++;
                    }
                    int p93 = g.FindIndex(t, q => q.Code == 93);
                    if (p93 >= 0)
                    {
                        int len = (int)D(g[p93].Val), q2 = p93 + 1, used = 0;
                        while (used < len && q2 < g.Count && g[q2].Code == 90)
                        {
                            int cnt = (int)D(g[q2].Val); var ids = new List<int>();
                            for (int r = 1; r <= cnt && q2 + r < g.Count; r++) { int vi = (int)D(g[q2 + r].Val); if (vi >= 0 && vi < mv.Count) ids.Add(mv[vi]); }
                            for (int r = 1; r + 1 < ids.Count; r++) F.Add(new[] { ids[0], ids[r], ids[r + 1] });
                            q2 += cnt + 1; used += cnt + 1;
                        }
                    }
                }
            }
            idx = end;
        }
        // đơn vị chưa rõ: đoán theo kích thước (&gt; 50 → mm)
        if (k <= 0 && V.Count > 0)
        {
            double ext = new[] { 0, 1, 2 }.Max(i2 => V.Max(v => v[i2]) - V.Min(v => v[i2]));
            k = ext > 50 ? 0.001 : 1;
        }
        if (Math.Abs(k - 1) > 1e-12) foreach (var v in V) { v[0] *= k; v[1] *= k; v[2] *= k; }
        Normalize(it);
        return it;
    }

    /// <summary>Dời gốc về tâm đáy hộp bao, tính kích thước.</summary>
    public static void Normalize(LibraryItem it)
    {
        if (it.Vertices.Count == 0) return;
        double x0 = it.Vertices.Min(v => v[0]), x1 = it.Vertices.Max(v => v[0]);
        double y0 = it.Vertices.Min(v => v[1]), y1 = it.Vertices.Max(v => v[1]);
        double z0 = it.Vertices.Min(v => v[2]), z1 = it.Vertices.Max(v => v[2]);
        double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
        foreach (var v in it.Vertices) { v[0] -= cx; v[1] -= cy; v[2] -= z0; }
        it.Width = Math.Round(x1 - x0, 4); it.Depth = Math.Round(y1 - y0, 4); it.Height = Math.Round(z1 - z0, 4);
        if (string.IsNullOrEmpty(it.Id)) it.Id = Safe(it.Category) + "/" + Safe(it.Name);
        it.PlanSymbol = ComputePlanSymbol(it);
    }

    private static string Safe(string s) => string.Concat((s ?? "").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();

    /// <summary>Nhóm gợi ý theo tên (bồn cầu, lavabo → thiết bị vệ sinh; đèn; ổ cắm…).</summary>
    public static string GuessCategory(string name)
    {
        string n = KetCauVXT.Ui.CommandSearch.Fold(name ?? "").ToLowerInvariant();
        if (new[] { "bon cau", "lavabo", "chau rua", "sen", "voi", "toilet", "wc", "bon tam", "tieu nam" }.Any(n.Contains)) return "Thiết bị vệ sinh";
        if (new[] { "den", "light", "lamp" }.Any(n.Contains)) return "Đèn";
        if (new[] { "o cam", "cong tac", "quat", "dieu hoa", "socket", "switch", "fan" }.Any(n.Contains)) return "Điện";
        if (new[] { "cua", "door", "window" }.Any(n.Contains)) return "Cửa";
        if (new[] { "giuong", "ban", "ghe", "tu", "sofa", "ke", "bed", "table", "chair" }.Any(n.Contains)) return "Nội thất";
        return "Khác";
    }

    /// <summary>Lưu mục thư viện vào thư mục (mặc định DefaultFolder). Trả đường dẫn file.</summary>
    public static string Save(LibraryItem it, string folder = null)
    {
        folder ??= DefaultFolder;
        string dir = Path.Combine(folder, Safe(it.Category));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, Safe(it.Name) + ".vxtlib.json");
        File.WriteAllText(path, JsonSerializer.Serialize(it, Project.JsonOptions));
        return path;
    }

    /// <summary>Mọi mục trong thư viện (lỗi đọc file nào thì bỏ qua file đó).</summary>
    public static List<LibraryItem> LoadAll(string folder = null)
    {
        bool builtIn = folder == null;
        folder ??= DefaultFolder;
        var res = new List<LibraryItem>();
        if (builtIn) res.AddRange(FurnitureFamilies.AsLibraryItems());   // họ tham số có sẵn
        if (!Directory.Exists(folder)) return res;
        foreach (var f in Directory.GetFiles(folder, "*.vxtlib.json", SearchOption.AllDirectories))
        {
            try
            {
                var it = JsonSerializer.Deserialize<LibraryItem>(File.ReadAllText(f), Project.JsonOptions);
                if (it == null) continue;
                // file do plugin SketchUp / 3ds Max / add-in Revit ghi thẳng vào thư viện: tính kích thước, ký hiệu mặt bằng nếu thiếu
                if (it.Vertices.Count > 0 && (it.Width <= 0 || it.Depth <= 0)) Normalize(it);
                else if (it.PlanSymbol == null && it.Vertices.Count > 0) it.PlanSymbol = ComputePlanSymbol(it);
                if (string.IsNullOrEmpty(it.Id)) it.Id = Safe(it.Category) + "/" + Safe(it.Name);
                res.Add(it);
            }
            catch { /* bỏ qua file hỏng */ }
        }
        return res.OrderBy(i => i.Category).ThenBy(i => i.Name).ToList();
    }

    /// <summary>Nhập file OBJ / DAE / *.vxtmesh.json (Revit) vào thư viện. scale chỉ dùng cho OBJ.</summary>
    public static LibraryItem Import(string path, string category = null, double objScale = 1.0, string folder = null)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (name.EndsWith(".vxtmesh", StringComparison.OrdinalIgnoreCase)) name = name[..^8];
        string ext = Path.GetExtension(path).ToLowerInvariant();
        LibraryItem it = ext switch
        {
            ".obj" => ReadObj(File.ReadAllText(path), name, objScale),
            ".dae" => ReadDae(File.ReadAllText(path), name),
            ".dxf" => ReadDxf3D(File.ReadAllText(path), name, objScale == 1.0 ? 0 : objScale),
            _ => ReadRevitMesh(File.ReadAllText(path), name)
        };
        it.Category = category ?? GuessCategory(name);
        it.Discipline = it.Category is "Thiết bị vệ sinh" or "Đèn" or "Điện" ? Discipline.DienNuoc : Discipline.KienTruc;
        it.Id = Safe(it.Category) + "/" + Safe(it.Name);
        Save(it, folder);
        return it;
    }

    /// <summary>Lưới do add-in Revit xuất ({"Name","Category","Vertices":[[x,y,z]],"Faces":[[i,j,k]]}, m).</summary>
    public static LibraryItem ReadRevitMesh(string json, string name)
    {
        var it = JsonSerializer.Deserialize<LibraryItem>(json, Project.JsonOptions) ?? new LibraryItem();
        if (string.IsNullOrEmpty(it.Name)) it.Name = name;
        it.Source = "Revit";
        Normalize(it);
        return it;
    }

    /// <summary>Đặt mục thư viện vào mô hình (đồ đạc kiến trúc).</summary>
    public static FurnitureItem Place(Project p, LibraryItem it, string story, double x, double y, double rotationDeg = 0)
    {
        p.Arch ??= new ArchModel();
        var f = new FurnitureItem
        {
            Id = p.Arch.Furniture.Count == 0 ? 1 : p.Arch.Furniture.Max(q => q.Id) + 1,
            Story = story, LibraryId = it.Id, Name = it.Name, Category = it.Category, X = x, Y = y, Rotation = rotationDeg,
            Width = Math.Max(0.05, it.Width), Depth = Math.Max(0.05, it.Depth), Height = Math.Max(0.05, it.Height),
            Discipline = it.Discipline, PlanSymbol = it.PlanSymbol
        };
        if (it.Id != null && it.Id.StartsWith(FurnitureFamilies.IdPrefix) && FurnitureFamilies.Find(it.Id[FurnitureFamilies.IdPrefix.Length..]) is { } fam)
        {
            f.Family = fam.Name; f.Elevation = fam.Elevation; f.Discipline = fam.Discipline;
        }
        p.Arch.Furniture.Add(f);
        return f;
    }
}
