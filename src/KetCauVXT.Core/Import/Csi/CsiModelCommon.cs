using System.Globalization;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Csi;

/// <summary>Hàm dùng chung cho bộ nhập mô hình ETABS (.e2k) và SAP2000 (.s2k): ánh xạ vật liệu, tiết diện, trường hợp tải, tổ hợp;
/// lưới nút gộp theo toạ độ (1 mm).</summary>
internal sealed class CsiModelBuilder
{
    public readonly Project P;
    public readonly ImportReport Rep;
    private readonly Dictionary<(long, long, long), Node> _nodes = new();
    private readonly Dictionary<string, string> _matMap = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, string> MaterialKindOf = new(StringComparer.OrdinalIgnoreCase);
    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private int _nextNode;

    public CsiModelBuilder(Project p, ImportReport rep)
    {
        P = p; Rep = rep;
        foreach (var n in p.Nodes) _nodes[KeyOf(n.X, n.Y, n.Z)] = n;
    }

    private static (long, long, long) KeyOf(double x, double y, double z) =>
        ((long)Math.Round(x * 1000), (long)Math.Round(y * 1000), (long)Math.Round(z * 1000));

    /// <summary>Dự án mới cho mô hình nhập: giữ thư viện vật liệu/tiết diện mặc định, xoá trường hợp tải mặc định (thay bằng của file),
    /// tắt sinh tải gió/động đất tự động (tải lấy theo file).</summary>
    public static Project NewProject(string name)
    {
        var p = ModelBuilder.NewProject(name);
        p.Grids.Clear(); p.Stories.Clear(); p.Patterns.Clear(); p.Combinations.Clear();
        ImportGeometry.ClearModel(p);
        p.Wind.Enabled = false;
        p.Seismic.Enabled = false;
        return p;
    }

    public Node NodeAt(double x, double y, double z, string story, string extName)
    {
        var k = KeyOf(x, y, z);
        if (_nodes.TryGetValue(k, out var n))
        {
            if (string.IsNullOrEmpty(n.ExternalName) && !string.IsNullOrEmpty(extName)) n.ExternalName = extName;
            return n;
        }
        if (_nextNode == 0) _nextNode = P.NextNodeId();
        n = new Node { Id = _nextNode++, X = x, Y = y, Z = z, Story = story ?? "", ExternalName = extName ?? "" };
        P.Nodes.Add(n);
        _nodes[k] = n;
        return n;
    }

    // ------------------------------------------------------------------ vật liệu
    /// <summary>Ánh xạ vật liệu của file sang vật liệu thư viện TCVN của chương trình: trùng tên → giữ; bê tông → cấp có Rb,n gần f'c nhất;
    /// cốt thép → mác có fy gần nhất; thép hình → mác có fy gần nhất.</summary>
    public string MapMaterial(string name, string type, double fcKNm2, double fyKNm2)
    {
        if (string.IsNullOrEmpty(name)) return "B25";
        if (_matMap.TryGetValue(name, out var m)) return m;
        string t = (type ?? "").ToUpperInvariant();
        string res;
        if (Material.ConcreteClasses.Contains(name) || Material.RebarGrades.Contains(name) || Material.SteelGrades.Contains(name)) res = name;
        else if (t.Contains("CONC"))
        {
            double fc = fcKNm2 / 1000.0;
            res = fc > 0 ? Material.ConcreteClasses.OrderBy(c => Math.Abs(Material.Concrete(c).Rbn / 1000.0 - fc)).First() : "B25";
        }
        else if (t.Contains("REBAR"))
        {
            double fy = fyKNm2 / 1000.0;
            res = fy > 0 ? Material.RebarGrades.OrderBy(c => Math.Abs(Material.Rebar(c).Fy / 1000.0 - fy)).First() : "CB400-V";
        }
        else if (t.Contains("STEEL") || t.Contains("COLDFORMED") || t.Contains("ALUM"))
        {
            double fy = fyKNm2 / 1000.0;
            res = fy > 0 ? Material.SteelGrades.OrderBy(c => Math.Abs(Material.Steel(c).Fy / 1000.0 - fy)).First() : "SS400";
        }
        else res = "B25";
        _matMap[name] = res;
        MaterialKindOf[name] = t.Contains("CONC") ? "C" : t.Contains("REBAR") ? "R" : t.Contains("STEEL") || t.Contains("COLD") ? "S" : "C";
        if (res != name) Rep.Info.Add($"Vật liệu \"{name}\" ({type}) → {res} (thư viện TCVN).");
        P.Material(res);   // thêm từ thư viện nếu chưa có
        return res;
    }

    public bool IsSteelMaterial(string extName) => MaterialKindOf.TryGetValue(extName ?? "", out var k) && k == "S";

    // ------------------------------------------------------------------ tiết diện
    public void AddRect(string name, string mat, double b, double h)
    {
        if (P.FrameSections.Any(s => s.Name == name)) P.FrameSections.RemoveAll(s => s.Name == name);
        P.FrameSections.Add(new FrameSection { Name = name, Shape = SectionShape.RectConcrete, Material = mat, B = b, H = h });
        P.InvalidateIndex();
    }

    public void AddI(string name, string mat, double d, double bf, double tf, double tw, double bfb, double tfb)
    {
        if (P.FrameSections.Any(s => s.Name == name)) P.FrameSections.RemoveAll(s => s.Name == name);
        P.FrameSections.Add(new FrameSection
        {
            Name = name, Shape = SectionShape.WeldedI, Material = mat, HStart = d, HEnd = d, BfTop = bf, TfTop = tf, Tw = tw,
            BfBot = bfb > 0 ? bfb : bf, TfBot = tfb > 0 ? tfb : tf
        });
        P.InvalidateIndex();
    }

    public void AddGeneral(string name, string mat, double d, double b, double a, double i33, double i22, double j, double s33, double s22)
    {
        if (P.FrameSections.Any(s => s.Name == name)) P.FrameSections.RemoveAll(s => s.Name == name);
        P.FrameSections.Add(new FrameSection
        {
            Name = name, Shape = SectionShape.General, Material = mat, GA = a, GI33 = i33, GI22 = i22, GJ = j,
            GW33 = s33 > 0 ? s33 : (d > 0 ? i33 / (d / 2) : 0), GW22 = s22 > 0 ? s22 : (b > 0 ? i22 / (b / 2) : 0), GDepth = d, GWidth = b
        });
        P.InvalidateIndex();
    }

    public string EnsureSlabSection(string name, double t, string mat)
    {
        if (t <= 0) t = 0.1;
        var ex = P.SlabSections.FirstOrDefault(s => s.Name == name);
        if (ex != null) { ex.Thickness = t; ex.Material = mat; return name; }
        P.SlabSections.Add(new SlabSection { Name = name, Thickness = t, Material = mat });
        return name;
    }

    // ------------------------------------------------------------------ tải
    public static LoadType PatternType(string t)
    {
        string s = ExtTable.Norm(t);
        if (s.Contains("SUPERDEAD")) return LoadType.SuperDead;
        if (s.Contains("DEAD")) return LoadType.Dead;
        if (s.Contains("ROOFLIVE")) return LoadType.RoofLive;
        if (s.Contains("LIVE")) return LoadType.Live;
        if (s.Contains("WIND")) return LoadType.Wind;
        if (s.Contains("QUAKE") || s.Contains("SEISMIC")) return LoadType.Seismic;
        return LoadType.Other;
    }

    public static double Gamma(LoadType t) => t switch
    {
        LoadType.Dead => 1.1, LoadType.SuperDead => 1.3, LoadType.Live => 1.3, LoadType.RoofLive => 1.3, LoadType.Wind => 1.2, _ => 1.0
    };

    public void AddPattern(string name, string type, double selfWeight)
    {
        if (string.IsNullOrEmpty(name) || P.Pattern(name) != null) return;
        var lt = PatternType(type);
        P.Patterns.Add(new LoadPattern { Name = name, Type = lt, SelfWeight = selfWeight, Gamma = Gamma(lt) });
    }

    /// <summary>Loại tổ hợp theo kiểu và tên: Envelope → bao; tên gợi ý tiêu chuẩn (SLS, TC, SERV, TTGH2) → TTGH II; còn lại TTGH I.</summary>
    public static ComboType ComboTypeOf(string type, string name)
    {
        if (ExtTable.Norm(type).Contains("ENVELOPE")) return ComboType.Envelope;
        string n = ResultImportSession.RemoveDiacritics(name ?? "").ToUpperInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
        if (n.StartsWith("SLS") || n.StartsWith("SERV") || n.Contains("TTGH2") || n.Contains("TTGHII") || System.Text.RegularExpressions.Regex.IsMatch(n, @"^(TC|TCTC|DEFL)\d"))
            return ComboType.SLS;
        return ComboType.ULS;
    }

    /// <summary>Mã giải phóng "M2I M3J TI…" → mảng [P, V2, V3, T, M2, M3] đầu I và J.</summary>
    public static void ParseReleases(string code, bool[] ri, bool[] rj)
    {
        string[] names = { "P", "V2", "V3", "T", "M2", "M3" };
        foreach (var tok in (code ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string t = tok.Trim().ToUpperInvariant();
            if (t.Length < 2) continue;
            char end = t[^1];
            string comp = t.Substring(0, t.Length - 1);
            int k = Array.IndexOf(names, comp);
            if (k < 0) continue;
            if (end == 'I') ri[k] = true; else if (end == 'J') rj[k] = true;
        }
    }

    /// <summary>Loại thanh theo hướng và vật liệu.</summary>
    public FrameKind KindOf(Vec3 a, Vec3 b, string section, string hint)
    {
        var sec = P.FrameSections.FirstOrDefault(s => s.Name == section);
        bool steel = sec != null && sec.Shape != SectionShape.RectConcrete && P.Materials.FirstOrDefault(m => m.Name == sec.Material)?.Kind == MaterialKind.Steel;
        double horiz = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
        string h = (hint ?? "").ToUpperInvariant();
        if (h == "BRACE") return FrameKind.Brace;
        if (h == "COLUMN" || horiz < 1e-3) return steel ? FrameKind.SteelColumn : FrameKind.Column;
        if (Math.Abs(b.Z - a.Z) < 1e-3 || h == "BEAM") return steel ? FrameKind.Rafter : FrameKind.Beam;
        return steel ? FrameKind.Rafter : FrameKind.Brace;
    }

    /// <summary>Gối: nếu file không gán liên kết nào → ngàm mọi nút ở cao độ thấp nhất (ghi cảnh báo).</summary>
    public void EnsureSupports()
    {
        if (P.Nodes.Any(n => n.IsSupport)) return;
        if (P.Nodes.Count == 0) return;
        double zmin = P.Nodes.Min(n => n.Z);
        int k = 0;
        foreach (var n in P.Nodes.Where(n => Math.Abs(n.Z - zmin) < 1e-3)) { n.Restraint = Node.Fixed; k++; }
        Rep.Warnings.Add($"File không khai báo liên kết gối – đã ngàm {k} nút ở cao độ thấp nhất {zmin:0.###} m.");
    }

    /// <summary>Hoàn tất: chia thanh tại nút trung gian, đếm, kiểm tra.</summary>
    public void Finish(bool split = true)
    {
        P.InvalidateIndex();
        EnsureSupports();
        if (split)
        {
            int n = ModelBuilder.SplitFramesAtNodes(P);
            Rep.SplitCount = n;
            if (n > 0) Rep.Info.Add($"Đã chia {n} thanh tại nút trung gian (dầm đi qua cột/dầm khác) – tên gốc được giữ để khớp nội lực.");
        }
        P.InvalidateIndex();
        Rep.Nodes = P.Nodes.Count;
        Rep.Frames = P.Frames.Count;
        Rep.Columns = P.Frames.Count(f => f.IsVerticalKind);
        Rep.Beams = P.Frames.Count(f => f.Kind is FrameKind.Beam or FrameKind.Rafter);
        Rep.Slabs = P.Slabs.Count;
        Rep.Grids = P.Grids.Count;
        Rep.Stories = P.Stories.Count;
        foreach (var e in P.Validate()) Rep.Warnings.Add(e);
        if (P.Combinations.Count == 0 && P.Patterns.Count > 0)
        {
            KetCauVXT.Loads.ComboGenerator.Generate(P);
            Rep.Info.Add($"File không có tổ hợp – đã tự sinh {P.Combinations.Count} tổ hợp theo TCVN 2737:2023 từ các trường hợp tải.");
        }
        if (P.Patterns.Count == 0)
        {
            ImportGeometry.EnsurePatterns(P);
            Rep.Warnings.Add("File không có trường hợp tải – đã thêm TT, HTHT, HT mặc định.");
        }
    }
}
