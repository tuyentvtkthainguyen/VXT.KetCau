using System.Globalization;
using System.Text;
using KetCauVXT.Model;

namespace KetCauVXT.Export;

// =====================================================================
// XUẤT LƯỚI TAM GIÁC CHO SKETCHUP / PHẦN MỀM DIỄN HOẠ
// OBJ (+ MTL): nhóm "g" theo bộ môn / loại đối tượng, vật liệu theo loại.
// DAE (COLLADA 1.4.1): mỗi khối một geometry, gom node theo bộ môn – SketchUp (File → Import → COLLADA) đọc trực tiếp.
// Đơn vị: m (OBJ không có đơn vị: SketchUp hỏi đơn vị khi nhập – chọn Meters). Trục Z hướng lên.
// =====================================================================
public sealed class Mesh
{
    public List<Vec3> V { get; } = new();
    public List<int[]> T { get; } = new();
    public string Name { get; set; } = "";
    public string Group { get; set; } = "";
    public string Material { get; set; } = "";
    public double[] Color { get; set; } = { 0.8, 0.8, 0.8 };
}

public static class MeshExport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v) => Math.Round(v, 5).ToString("0.#####", Inv);

    /// <summary>Tam giác hoá đa giác phẳng (cắt tai). Trả về chỉ số đỉnh.</summary>
    public static List<int[]> Triangulate(IReadOnlyList<double[]> poly)
    {
        var res = new List<int[]>();
        int n = poly.Count;
        if (n < 3) return res;
        var idx = Enumerable.Range(0, n).ToList();
        if (Geo2.SignedArea(poly) < 0) idx.Reverse();
        double Cross(int a, int b, int c) =>
            (poly[b][0] - poly[a][0]) * (poly[c][1] - poly[a][1]) - (poly[b][1] - poly[a][1]) * (poly[c][0] - poly[a][0]);
        bool Inside(int a, int b, int c, int pIdx)
        {
            double x = poly[pIdx][0], y = poly[pIdx][1];
            double d1 = (poly[b][0] - poly[a][0]) * (y - poly[a][1]) - (poly[b][1] - poly[a][1]) * (x - poly[a][0]);
            double d2 = (poly[c][0] - poly[b][0]) * (y - poly[b][1]) - (poly[c][1] - poly[b][1]) * (x - poly[b][0]);
            double d3 = (poly[a][0] - poly[c][0]) * (y - poly[c][1]) - (poly[a][1] - poly[c][1]) * (x - poly[c][0]);
            return d1 >= -1e-12 && d2 >= -1e-12 && d3 >= -1e-12;
        }
        int guard = 0;
        while (idx.Count > 3 && guard++ < 10000)
        {
            bool cut = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int a = idx[(i - 1 + idx.Count) % idx.Count], b = idx[i], c = idx[(i + 1) % idx.Count];
                if (Cross(a, b, c) <= 1e-12) continue;
                bool any = false;
                foreach (int q in idx)
                    if (q != a && q != b && q != c && Inside(a, b, c, q)) { any = true; break; }
                if (any) continue;
                res.Add(new[] { a, b, c });
                idx.RemoveAt(i);
                cut = true;
                break;
            }
            if (!cut) break;
        }
        if (idx.Count == 3) res.Add(new[] { idx[0], idx[1], idx[2] });
        else if (idx.Count > 3)   // đa giác suy biến: quạt
            for (int i = 1; i + 1 < idx.Count; i++) res.Add(new[] { idx[0], idx[i], idx[i + 1] });
        return res;
    }

    /// <summary>Lưới kín của một khối lăng trụ (đáy, nắp, mặt bên).</summary>
    public static Mesh ToMesh(Solid s)
    {
        var m = new Mesh { Name = s.Id, Group = DisciplineView.Title(s.Discipline) + "/" + s.Category, Material = Safe(s.Category), Color = s.Color };
        var (bot, top) = s.Rings();
        int n = bot.Count;
        if (n < 3) return m;
        m.V.AddRange(bot); m.V.AddRange(top);
        var tri = Triangulate(s.Profile);
        bool flip = Geo2.SignedArea(s.Profile) < 0;
        foreach (var t in tri)
        {
            m.T.Add(new[] { t[0], t[2], t[1] });           // đáy (hướng xuống)
            m.T.Add(new[] { n + t[0], n + t[1], n + t[2] }); // nắp
        }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            if (!flip) { m.T.Add(new[] { i, j, n + j }); m.T.Add(new[] { i, n + j, n + i }); }
            else { m.T.Add(new[] { i, n + j, j }); m.T.Add(new[] { i, n + i, n + j }); }
        }
        return m;
    }

    public static string Safe(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in KetCauVXT.Ui.CommandSearch.Fold(s ?? ""))
            sb.Append(char.IsLetterOrDigit(c) && c < 128 ? c : '_');
        return sb.Length == 0 ? "VatLieu" : sb.ToString();
    }

    /// <summary>Ghi OBJ + MTL (cùng tên, đuôi .mtl). Trả về số tam giác.</summary>
    public static int SaveObj(Project p, string objPath, IEnumerable<Discipline> disciplines = null)
    {
        var meshes = ModelSolids.Build(p, disciplines, includeVoids: false).Select(ToMesh).Where(m => m.T.Count > 0).ToList();
        string mtlName = Path.GetFileNameWithoutExtension(objPath) + ".mtl";
        var (obj, mtl) = Obj(meshes, mtlName);
        File.WriteAllText(objPath, obj, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(objPath) ?? ".", mtlName), mtl, new UTF8Encoding(false));
        return meshes.Sum(m => m.T.Count);
    }

    public static (string Obj, string Mtl) Obj(List<Mesh> meshes, string mtlName)
    {
        var o = new StringBuilder("# Viet Const – OBJ (m, Z lên)\n");
        o.Append("mtllib ").Append(mtlName).Append('\n');
        var mats = new Dictionary<string, double[]>();
        int baseV = 1;
        foreach (var grp in meshes.GroupBy(m => m.Group))
        {
            o.Append("g ").Append(Safe(grp.Key)).Append('\n');
            foreach (var m in grp)
            {
                o.Append("o ").Append(Safe(m.Name)).Append('\n');
                if (!mats.ContainsKey(m.Material)) mats[m.Material] = m.Color;
                o.Append("usemtl ").Append(m.Material).Append('\n');
                foreach (var v in m.V) o.Append("v ").Append(F(v.X)).Append(' ').Append(F(v.Y)).Append(' ').Append(F(v.Z)).Append('\n');
                foreach (var t in m.T) o.Append("f ").Append(baseV + t[0]).Append(' ').Append(baseV + t[1]).Append(' ').Append(baseV + t[2]).Append('\n');
                baseV += m.V.Count;
            }
        }
        var mt = new StringBuilder("# Viet Const – vật liệu\n");
        foreach (var kv in mats)
            mt.Append("newmtl ").Append(kv.Key).Append("\nKd ").Append(F(kv.Value[0])).Append(' ').Append(F(kv.Value[1])).Append(' ').Append(F(kv.Value[2])).Append("\nd 1\n\n");
        return (o.ToString(), mt.ToString());
    }

    /// <summary>Ghi COLLADA 1.4.1 (.dae). Trả về số tam giác.</summary>
    public static int SaveDae(Project p, string path, IEnumerable<Discipline> disciplines = null)
    {
        var meshes = ModelSolids.Build(p, disciplines, includeVoids: false).Select(ToMesh).Where(m => m.T.Count > 0).ToList();
        File.WriteAllText(path, Dae(meshes, p.Name), new UTF8Encoding(false));
        return meshes.Sum(m => m.T.Count);
    }

    private static string X(string s) => System.Security.SecurityElement.Escape(s ?? "");

    public static string Dae(List<Mesh> meshes, string title)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
        sb.Append("<COLLADA xmlns=\"http://www.collada.org/2005/11/COLLADASchema\" version=\"1.4.1\">\n");
        sb.Append("<asset><contributor><authoring_tool>Viet Const</authoring_tool></contributor>")
          .Append("<created>").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", Inv)).Append("</created>")
          .Append("<modified>").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", Inv)).Append("</modified>")
          .Append("<title>").Append(X(title)).Append("</title><unit name=\"meter\" meter=\"1\"/><up_axis>Z_UP</up_axis></asset>\n");
        var mats = meshes.Select(m => (m.Material, m.Color)).GroupBy(x => x.Material).Select(g => g.First()).ToList();
        sb.Append("<library_effects>\n");
        foreach (var (mat, col) in mats)
            sb.Append($"<effect id=\"fx-{mat}\"><profile_COMMON><technique sid=\"common\"><lambert><diffuse><color>{F(col[0])} {F(col[1])} {F(col[2])} 1</color></diffuse></lambert></technique></profile_COMMON></effect>\n");
        sb.Append("</library_effects>\n<library_materials>\n");
        foreach (var (mat, _) in mats) sb.Append($"<material id=\"mat-{mat}\" name=\"{mat}\"><instance_effect url=\"#fx-{mat}\"/></material>\n");
        sb.Append("</library_materials>\n<library_geometries>\n");
        for (int k = 0; k < meshes.Count; k++)
        {
            var m = meshes[k];
            string id = "g" + k;
            sb.Append($"<geometry id=\"{id}\" name=\"{X(m.Name)}\"><mesh>");
            sb.Append($"<source id=\"{id}-pos\"><float_array id=\"{id}-pos-arr\" count=\"{m.V.Count * 3}\">");
            sb.Append(string.Join(" ", m.V.Select(v => $"{F(v.X)} {F(v.Y)} {F(v.Z)}")));
            sb.Append($"</float_array><technique_common><accessor source=\"#{id}-pos-arr\" count=\"{m.V.Count}\" stride=\"3\"><param name=\"X\" type=\"float\"/><param name=\"Y\" type=\"float\"/><param name=\"Z\" type=\"float\"/></accessor></technique_common></source>");
            sb.Append($"<vertices id=\"{id}-vtx\"><input semantic=\"POSITION\" source=\"#{id}-pos\"/></vertices>");
            sb.Append($"<triangles material=\"m\" count=\"{m.T.Count}\"><input semantic=\"VERTEX\" source=\"#{id}-vtx\" offset=\"0\"/><p>");
            sb.Append(string.Join(" ", m.T.Select(t => $"{t[0]} {t[1]} {t[2]}")));
            sb.Append("</p></triangles></mesh></geometry>\n");
        }
        sb.Append("</library_geometries>\n<library_visual_scenes><visual_scene id=\"scene\" name=\"").Append(X(title)).Append("\">\n");
        int gi = 0;
        foreach (var grp in meshes.Select((m, i) => (m, i)).GroupBy(x => x.m.Group))
        {
            sb.Append($"<node id=\"grp{gi++}\" name=\"{X(Safe(grp.Key))}\">\n");
            foreach (var (m, i) in grp)
                sb.Append($"<node id=\"n{i}\" name=\"{X(Safe(m.Name))}\"><instance_geometry url=\"#g{i}\"><bind_material><technique_common><instance_material symbol=\"m\" target=\"#mat-{m.Material}\"/></technique_common></bind_material></instance_geometry></node>\n");
            sb.Append("</node>\n");
        }
        sb.Append("</visual_scene></library_visual_scenes>\n<scene><instance_visual_scene url=\"#scene\"/></scene>\n</COLLADA>\n");
        return sb.ToString();
    }
}
