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
        folder ??= DefaultFolder;
        var res = new List<LibraryItem>();
        if (!Directory.Exists(folder)) return res;
        foreach (var f in Directory.GetFiles(folder, "*.vxtlib.json", SearchOption.AllDirectories))
        {
            try { var it = JsonSerializer.Deserialize<LibraryItem>(File.ReadAllText(f), Project.JsonOptions); if (it != null) res.Add(it); }
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
            Width = Math.Max(0.05, it.Width), Depth = Math.Max(0.05, it.Depth), Height = Math.Max(0.05, it.Height)
        };
        p.Arch.Furniture.Add(f);
        return f;
    }
}
