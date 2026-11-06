using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KetCauVXT.Model;

namespace KetCauVXT.Export;

// =====================================================================
// XUẤT IFC2X3 (ISO 10303-21, "STEP physical file") – Revit, SketchUp Pro, Tekla, Navisworks… mở được.
// Mỗi đối tượng là IfcExtrudedAreaSolid (mặt cắt chữ nhật / đa giác kéo theo trục Z địa phương), đặt bằng IfcLocalPlacement.
// Cấu trúc: IfcProject → IfcSite → IfcBuilding → IfcBuildingStorey (Đáy móng, Nền tầng 1, các sàn) → đối tượng.
// Tường có IfcOpeningElement (IfcRelVoidsElement) + IfcDoor/IfcWindow (IfcRelFillsElement). Phòng → IfcSpace.
// Chữ tiếng Việt mã hoá \X2\hhhh\X0\ theo chuẩn. GlobalId tất định (theo mã đối tượng) để xuất lại vẫn giữ id.
// =====================================================================
public sealed class IfcOptions
{
    /// <summary>Bộ môn xuất (null = tất cả)</summary>
    public List<Discipline> Disciplines { get; set; }
    public bool IncludeRooms { get; set; } = true;
    public string Author { get; set; } = "";
    public string Organization { get; set; } = "";
}

public static class IfcWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Save(Project p, string path, IfcOptions o = null) =>
        File.WriteAllText(path, Write(p, o), new UTF8Encoding(false));

    /// <summary>Mã hoá chuỗi theo IFC (ASCII; ký tự ngoài ASCII → \X2\hhhh\X0\; dấu ' → '').</summary>
    public static string Str(string s)
    {
        if (s == null) return "$";
        var sb = new StringBuilder("'");
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c >= 32 && c < 127 && c != '\\')
            {
                sb.Append(c == '\'' ? "''" : c.ToString());
                i++;
            }
            else
            {
                sb.Append("\\X2\\");
                while (i < s.Length && !(s[i] >= 32 && s[i] < 127 && s[i] != '\\')) { sb.Append(((int)s[i]).ToString("X4")); i++; }
                sb.Append("\\X0\\");
            }
        }
        return sb.Append('\'').ToString();
    }

    /// <summary>Giải mã chuỗi IFC (ngược của Str).</summary>
    public static string Unstr(string s)
    {
        if (string.IsNullOrEmpty(s) || s == "$") return "";
        if (s.StartsWith("'") && s.EndsWith("'") && s.Length >= 2) s = s[1..^1];
        s = s.Replace("''", "'");
        var sb = new StringBuilder();
        int i = 0;
        while (i < s.Length)
        {
            if (s.AsSpan(i).StartsWith("\\X2\\"))
            {
                int end = s.IndexOf("\\X0\\", i + 4, StringComparison.Ordinal);
                if (end < 0) break;
                string hex = s.Substring(i + 4, end - i - 4);
                for (int k = 0; k + 4 <= hex.Length; k += 4) sb.Append((char)Convert.ToInt32(hex.Substring(k, 4), 16));
                i = end + 4;
            }
            else sb.Append(s[i++]);
        }
        return sb.ToString();
    }

    private const string B64 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

    /// <summary>GlobalId IFC 22 ký tự (tất định theo khoá).</summary>
    public static string GlobalId(string key)
    {
        byte[] h = MD5.HashData(Encoding.UTF8.GetBytes(key ?? ""));
        // 128 bit → 22 ký tự: ký tự đầu 2 bit, còn lại 6 bit/ký tự
        var bits = new System.Collections.BitArray(Enumerable.Reverse(h).ToArray());
        var sb = new StringBuilder();
        int pos = 128;
        int first = (bits[127] ? 2 : 0) + (bits[126] ? 1 : 0);
        sb.Append(B64[first]); pos = 126;
        while (pos > 0)
        {
            int v = 0;
            for (int k = 0; k < 6; k++) { pos--; v = (v << 1) | (bits[pos] ? 1 : 0); }
            sb.Append(B64[v]);
        }
        return sb.ToString();
    }

    private sealed class W
    {
        public readonly StringBuilder Sb = new();
        public int N;
        public int Add(string entity)
        {
            N++;
            Sb.Append('#').Append(N).Append('=').Append(entity).Append(";\n");
            return N;
        }
    }

    private static string F(double v) => (Math.Abs(v) < 1e-12 ? 0 : Math.Round(v, 6)).ToString("0.0#####", Inv);
    private static string Ref(int id) => "#" + id;
    private static string RefList(IEnumerable<int> ids) => "(" + string.Join(",", ids.Select(Ref)) + ")";

    /// <summary>Nội dung file IFC2X3.</summary>
    public static string Write(Project p, IfcOptions o = null)
    {
        o ??= new IfcOptions();
        var w = new W();
        string now = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", Inv);
        // --- đầu mục ---
        int person = w.Add($"IFCPERSON($,{Str(string.IsNullOrEmpty(o.Author) ? p.Designer : o.Author)},$,$,$,$,$,$)");
        int org = w.Add($"IFCORGANIZATION($,{Str(string.IsNullOrEmpty(o.Organization) ? "Viet Const" : o.Organization)},$,$,$)");
        int po = w.Add($"IFCPERSONANDORGANIZATION({Ref(person)},{Ref(org)},$)");
        int app = w.Add($"IFCAPPLICATION({Ref(org)},'1.0','Viet Const','VietConst')");
        long t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int owner = w.Add($"IFCOWNERHISTORY({Ref(po)},{Ref(app)},$,.ADDED.,$,$,$,{t})");
        int lenU = w.Add("IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.)");
        int areaU = w.Add("IFCSIUNIT(*,.AREAUNIT.,$,.SQUARE_METRE.)");
        int volU = w.Add("IFCSIUNIT(*,.VOLUMEUNIT.,$,.CUBIC_METRE.)");
        int angU = w.Add("IFCSIUNIT(*,.PLANEANGLEUNIT.,$,.RADIAN.)");
        int units = w.Add($"IFCUNITASSIGNMENT({RefList(new[] { lenU, areaU, volU, angU })})");
        int origin = w.Add("IFCCARTESIANPOINT((0.,0.,0.))");
        int dirZ = w.Add("IFCDIRECTION((0.,0.,1.))");
        int dirX = w.Add("IFCDIRECTION((1.,0.,0.))");
        int wcs = w.Add($"IFCAXIS2PLACEMENT3D({Ref(origin)},{Ref(dirZ)},{Ref(dirX)})");
        int ctx = w.Add($"IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,{Ref(wcs)},$)");
        int project = w.Add($"IFCPROJECT('{GlobalId("PROJECT:" + p.Name)}',{Ref(owner)},{Str(p.Name)},$,$,$,$,({Ref(ctx)}),{Ref(units)})");
        int sitePl = w.Add($"IFCLOCALPLACEMENT($,{Ref(wcs)})");
        int site = w.Add($"IFCSITE('{GlobalId("SITE:" + p.Name)}',{Ref(owner)},{Str(string.IsNullOrEmpty(p.Location) ? "Khu đất" : p.Location)},$,$,{Ref(sitePl)},$,$,.ELEMENT.,$,$,$,$,$)");
        int bldPl = w.Add($"IFCLOCALPLACEMENT({Ref(sitePl)},{Ref(wcs)})");
        int bld = w.Add($"IFCBUILDING('{GlobalId("BLD:" + p.Name)}',{Ref(owner)},{Str(p.Name)},$,$,{Ref(bldPl)},$,$,.ELEMENT.,$,$,$)");
        w.Add($"IFCRELAGGREGATES('{GlobalId("AGG:PS")}',{Ref(owner)},$,$,{Ref(project)},({Ref(site)}))");
        w.Add($"IFCRELAGGREGATES('{GlobalId("AGG:SB")}',{Ref(owner)},$,$,{Ref(site)},({Ref(bld)}))");

        // --- cao độ ---
        var levels = Levels.All(p).Where(l => l.Kind != LevelKind.MatMong).ToList();
        var storeyId = new Dictionary<string, int>();
        var storeyPl = new Dictionary<string, int>();
        foreach (var l in levels)
        {
            int pt = w.Add($"IFCCARTESIANPOINT((0.,0.,{F(l.Elevation)}))");
            int ax = w.Add($"IFCAXIS2PLACEMENT3D({Ref(pt)},$,$)");
            int pl = w.Add($"IFCLOCALPLACEMENT({Ref(bldPl)},{Ref(ax)})");
            int st = w.Add($"IFCBUILDINGSTOREY('{GlobalId("STOREY:" + l.Name)}',{Ref(owner)},{Str(l.Title)},$,$,{Ref(pl)},$,$,.ELEMENT.,{F(l.Elevation)})");
            storeyId[l.Name] = st; storeyPl[l.Name] = pl;
        }
        w.Add($"IFCRELAGGREGATES('{GlobalId("AGG:BS")}',{Ref(owner)},$,$,{Ref(bld)},{RefList(storeyId.Values)})");
        string StoreyOf(string story)
        {
            if (!string.IsNullOrEmpty(story) && storeyId.ContainsKey(story)) return story;
            if (story == "Base") return "DAY_MONG";
            return "NEN";
        }

        // --- đối tượng ---
        var solids = ModelSolids.Build(p, o.Disciplines);
        var contained = new Dictionary<string, List<int>>();
        var entityOf = new Dictionary<string, int>();
        foreach (var s in solids.Where(x => !x.IsVoid))
        {
            int shape = Shape(w, s, ctx);
            int pl = Placement(w, s, bldPl);
            string ifc = s.IfcType.ToUpperInvariant();
            string gid = GlobalId("EL:" + s.Id);
            string name = Str(s.Name), desc = Str(s.Category);
            int e = ifc switch
            {
                "IFCDOOR" => w.Add($"IFCDOOR('{gid}',{Ref(owner)},{name},{desc},$,{Ref(pl)},{Ref(shape)},$,{F(Height(s))},{F(Width(s))})"),
                "IFCWINDOW" => w.Add($"IFCWINDOW('{gid}',{Ref(owner)},{name},{desc},$,{Ref(pl)},{Ref(shape)},$,{F(Height(s))},{F(Width(s))})"),
                "IFCSLAB" => w.Add($"IFCSLAB('{gid}',{Ref(owner)},{name},{desc},$,{Ref(pl)},{Ref(shape)},$,.FLOOR.)"),
                "IFCROOF" => w.Add($"IFCROOF('{gid}',{Ref(owner)},{name},{desc},$,{Ref(pl)},{Ref(shape)},$,.NOTDEFINED.)"),
                "IFCCOVERING" => w.Add($"IFCCOVERING('{gid}',{Ref(owner)},{name},{desc},$,{Ref(pl)},{Ref(shape)},$,.CEILING.)"),
                "IFCCOLUMN" or "IFCBEAM" or "IFCWALLSTANDARDCASE" or "IFCFURNISHINGELEMENT" or "IFCFLOWSEGMENT" or "IFCFLOWTERMINAL" or "IFCFLOWFITTING"
                    => w.Add($"{ifc}('{gid}',{Ref(owner)},{name},{desc},$,{Ref(pl)},{Ref(shape)},$)"),
                _ => w.Add($"IFCBUILDINGELEMENTPROXY('{gid}',{Ref(owner)},{name},{desc},$,{Ref(pl)},{Ref(shape)},$,$)")
            };
            entityOf[s.Id] = e;
            string st = StoreyOf(s.Story);
            if (!contained.TryGetValue(st, out var lst)) contained[st] = lst = new List<int>();
            lst.Add(e);
        }
        // lỗ cửa + cửa lấp
        foreach (var v in solids.Where(x => x.IsVoid))
        {
            if (!entityOf.TryGetValue(v.HostId, out int host)) continue;
            int shape = Shape(w, v, ctx);
            int pl = Placement(w, v, bldPl);
            int op = w.Add($"IFCOPENINGELEMENT('{GlobalId("OP:" + v.Id)}',{Ref(owner)},{Str(v.Name)},$,$,{Ref(pl)},{Ref(shape)},$)");
            w.Add($"IFCRELVOIDSELEMENT('{GlobalId("VO:" + v.Id)}',{Ref(owner)},$,$,{Ref(host)},{Ref(op)})");
            string fillId = "CUA-" + v.Id.Substring(3);
            if (entityOf.TryGetValue(fillId, out int fill))
                w.Add($"IFCRELFILLSELEMENT('{GlobalId("FI:" + v.Id)}',{Ref(owner)},$,$,{Ref(op)},{Ref(fill)})");
        }
        // phòng
        if (o.IncludeRooms && (o.Disciplines == null || o.Disciplines.Contains(Discipline.KienTruc)))
        {
            var spaces = new Dictionary<string, List<int>>();
            foreach (var r in p.Arch?.Rooms ?? new List<Room>())
            {
                if (r.Outline.Count < 3) continue;
                var prof = r.Outline.Select(q => new[] { q[0], q[1] }).ToList();
                if (Geo2.SignedArea(prof) < 0) prof.Reverse();
                var sol = new Solid { Id = "PHONG-" + r.Id, Origin = new Vec3(0, 0, ModelSolids.StoryZ(p, r.Story)), Profile = prof, Depth = r.CeilingHeight > 0 ? r.CeilingHeight : 2.8 };
                int shape = Shape(w, sol, ctx);
                int pl = Placement(w, sol, bldPl);
                int sp = w.Add($"IFCSPACE('{GlobalId("SP:" + r.Id)}',{Ref(owner)},{Str(r.Number)},{Str(r.Name)},$,{Ref(pl)},{Ref(shape)},{Str(r.Name)},.ELEMENT.,.INTERNAL.,$)");
                string st = StoreyOf(r.Story);
                if (!spaces.TryGetValue(st, out var lst)) spaces[st] = lst = new List<int>();
                lst.Add(sp);
            }
            foreach (var kv in spaces)
                w.Add($"IFCRELAGGREGATES('{GlobalId("AGG:SP:" + kv.Key)}',{Ref(owner)},$,$,{Ref(storeyId[kv.Key])},{RefList(kv.Value)})");
        }
        foreach (var kv in contained)
            w.Add($"IFCRELCONTAINEDINSPATIALSTRUCTURE('{GlobalId("CT:" + kv.Key)}',{Ref(owner)},$,$,{RefList(kv.Value)},{Ref(storeyId[kv.Key])})");

        var hdr = new StringBuilder();
        hdr.Append("ISO-10303-21;\nHEADER;\n");
        hdr.Append("FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');\n");
        hdr.Append($"FILE_NAME({Str(p.Name + ".ifc")},'{now}',({Str(p.Designer)}),({Str("Viet Const")}),'Viet Const IFC','Viet Const','');\n");
        hdr.Append("FILE_SCHEMA(('IFC2X3'));\nENDSEC;\nDATA;\n");
        hdr.Append(w.Sb);
        hdr.Append("ENDSEC;\nEND-ISO-10303-21;\n");
        return hdr.ToString();
    }

    private static double Width(Solid s) => s.Profile.Count == 0 ? 0 : s.Profile.Max(q => q[0]) - s.Profile.Min(q => q[0]);
    private static double Height(Solid s) => s.Depth;

    private static int Placement(W w, Solid s, int rel)
    {
        int pt = w.Add($"IFCCARTESIANPOINT(({F(s.Origin.X)},{F(s.Origin.Y)},{F(s.Origin.Z)}))");
        int z = w.Add($"IFCDIRECTION(({F(s.ZDir.X)},{F(s.ZDir.Y)},{F(s.ZDir.Z)}))");
        int x = w.Add($"IFCDIRECTION(({F(s.XDir.X)},{F(s.XDir.Y)},{F(s.XDir.Z)}))");
        int ax = w.Add($"IFCAXIS2PLACEMENT3D({Ref(pt)},{Ref(z)},{Ref(x)})");
        return w.Add($"IFCLOCALPLACEMENT({Ref(rel)},{Ref(ax)})");
    }

    private static int Shape(W w, Solid s, int ctx)
    {
        var pts = s.Profile.Select(q => w.Add($"IFCCARTESIANPOINT(({F(q[0])},{F(q[1])}))")).ToList();
        if (pts.Count > 0) pts.Add(pts[0]);
        int poly = w.Add($"IFCPOLYLINE({RefList(pts)})");
        int prof = w.Add($"IFCARBITRARYCLOSEDPROFILEDEF(.AREA.,$,{Ref(poly)})");
        int o = w.Add("IFCCARTESIANPOINT((0.,0.,0.))");
        int pos = w.Add($"IFCAXIS2PLACEMENT3D({Ref(o)},$,$)");
        int dir = w.Add("IFCDIRECTION((0.,0.,1.))");
        int solid = w.Add($"IFCEXTRUDEDAREASOLID({Ref(prof)},{Ref(pos)},{Ref(dir)},{F(Math.Max(1e-4, s.Depth))})");
        int rep = w.Add($"IFCSHAPEREPRESENTATION({Ref(ctx)},'Body','SweptSolid',({Ref(solid)}))");
        return w.Add($"IFCPRODUCTDEFINITIONSHAPE($,$,({Ref(rep)}))");
    }
}
