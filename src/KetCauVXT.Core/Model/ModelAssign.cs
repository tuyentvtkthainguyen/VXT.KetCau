namespace KetCauVXT.Model;

/// <summary>Cách gán tải trọng (như ETABS: Add to existing / Replace existing / Delete existing).</summary>
public enum LoadAssignMode
{
    /// <summary>Cộng thêm vào tải đã có</summary>
    Add,
    /// <summary>Thay thế tải nhập tay cùng trường hợp tải trên đối tượng</summary>
    Replace,
    /// <summary>Xoá tải nhập tay cùng trường hợp tải trên đối tượng</summary>
    Delete
}

public partial class Project
{
    // ---- [Workflow ETABS: sàn cứng theo tầng – Model/ModelAssign.cs, Analysis/FrameAnalysis.cs] ----
    /// <summary>Tên các tầng TẮT sàn cứng (diaphragm) dù <see cref="UseRigidDiaphragm"/> bật (Assign → Diaphragm của ETABS).
    /// null/rỗng = mọi cao trình sàn dùng sàn cứng khi bật. File cũ không có mục này vẫn mở được.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<string> DiaphragmOff { get; set; }

    /// <summary>Cao trình z thuộc tầng đã tắt sàn cứng.</summary>
    public bool IsDiaphragmOff(double z)
    {
        if (DiaphragmOff == null || DiaphragmOff.Count == 0) return false;
        foreach (var s in Stories)
            if (Math.Abs(s.Elevation - z) < 1e-3 && DiaphragmOff.Contains(s.Name)) return true;
        return false;
    }
    // ---- [hết] ----
}

/// <summary>Gán thuộc tính / tải trọng cho các đối tượng đang chọn (menu Gán – như Assign của ETABS).
/// Mọi hàm trả về số đối tượng đã thay đổi; tải lưu vào FrameLoads / AreaLoads / NodeLoads với Source = "" (nhập tay).</summary>
public static class ModelAssign
{
    /// <summary>Thứ tự hệ số độ cứng thanh (FrameElement.StiffnessModifiers), giống "Frame Property Modifiers" của ETABS.</summary>
    public static readonly string[] ModifierNames = { "Diện tích A", "Diện tích cắt As2", "Diện tích cắt As3", "Xoắn J", "Quán tính I22", "Quán tính I33" };

    /// <summary>Hệ số độ cứng thứ i của thanh (1 khi chưa gán).</summary>
    public static double Modifier(this FrameElement f, int i)
    {
        var m = f?.StiffnessModifiers;
        if (m == null || i < 0 || i >= m.Length) return 1.0;
        double v = m[i];
        return double.IsNaN(v) || v <= 0 ? 1e-6 : v;
    }

    private static List<FrameElement> Frames(Project p, IEnumerable<int> ids)
    {
        var set = new HashSet<int>(ids ?? Enumerable.Empty<int>());
        return p.Frames.Where(f => set.Contains(f.Id)).ToList();
    }

    private static void ClearUser(Project p, FrameElement f, string pattern)
        => p.FrameLoads.RemoveAll(l => l.Element == f.Id && l.Pattern == pattern && l.Source == "");

    private static void CheckPattern(Project p, string pattern)
    {
        if (p.Pattern(pattern) == null) throw new ArgumentException("Chưa khai báo trường hợp tải " + pattern);
    }

    // ------------------------------------------------------------------
    // Tải trên thanh
    // ------------------------------------------------------------------
    /// <summary>Tải phân bố đều q (kN/m) suốt chiều dài thanh.</summary>
    public static int FrameUniform(Project p, IEnumerable<int> frames, string pattern, double q, LoadDir dir = LoadDir.Gravity,
                                   LoadAssignMode mode = LoadAssignMode.Add, string note = "Gán tải đều")
    {
        CheckPattern(p, pattern);
        int n = 0;
        foreach (var f in Frames(p, frames))
        {
            if (mode != LoadAssignMode.Add) ClearUser(p, f, pattern);
            if (mode != LoadAssignMode.Delete) p.FrameLoads.Add(FrameLoad.Uniform(f.Id, pattern, q, dir, note));
            n++;
        }
        return n;
    }

    /// <summary>Tải hình thang / tam giác: từ vị trí d1 (cường độ q1) tới d2 (q2). relative = true: d theo tỷ lệ 0..1 chiều dài;
    /// false: d tính bằng m từ đầu I (như "Relative/Absolute distance from End-I" của ETABS).</summary>
    public static int FrameTrapezoid(Project p, IEnumerable<int> frames, string pattern, double d1, double q1, double d2, double q2,
                                     LoadDir dir = LoadDir.Gravity, bool relative = true, LoadAssignMode mode = LoadAssignMode.Add, string note = "Gán tải hình thang")
    {
        CheckPattern(p, pattern);
        int n = 0;
        foreach (var f in Frames(p, frames))
        {
            if (mode != LoadAssignMode.Add) ClearUser(p, f, pattern);
            if (mode == LoadAssignMode.Delete) { n++; continue; }
            double L = p.Length(f);
            if (L < 1e-6) continue;
            double r1 = relative ? d1 : d1 / L, r2 = relative ? d2 : d2 / L;
            r1 = Math.Clamp(r1, 0, 1); r2 = Math.Clamp(r2, 0, 1);
            double a = q1, b = q2;
            if (r2 < r1) { (r1, r2) = (r2, r1); (a, b) = (b, a); }
            if (r2 - r1 < 1e-9) continue;
            p.FrameLoads.Add(new FrameLoad
            {
                Element = f.Id, Pattern = pattern, Dir = dir, Note = note,
                Points = new List<double[]> { new[] { r1, a }, new[] { r2, b } }
            });
            n++;
        }
        return n;
    }

    /// <summary>Lực tập trung P (kN) tại khoảng cách d từ đầu I (tỷ lệ hoặc m).</summary>
    public static int FramePoint(Project p, IEnumerable<int> frames, string pattern, double d, double P, LoadDir dir = LoadDir.Gravity,
                                 bool relative = true, LoadAssignMode mode = LoadAssignMode.Add, string note = "Gán lực tập trung")
    {
        CheckPattern(p, pattern);
        int n = 0;
        foreach (var f in Frames(p, frames))
        {
            if (mode != LoadAssignMode.Add) ClearUser(p, f, pattern);
            if (mode == LoadAssignMode.Delete) { n++; continue; }
            double L = p.Length(f);
            if (L < 1e-6) continue;
            double r = Math.Clamp(relative ? d : d / L, 0, 1);
            p.FrameLoads.Add(new FrameLoad
            {
                Element = f.Id, Pattern = pattern, Dir = dir, Note = note,
                PointLoads = new List<double[]> { new[] { r, P } }
            });
            n++;
        }
        return n;
    }

    // ------------------------------------------------------------------
    // Tải trên sàn, tại nút
    // ------------------------------------------------------------------
    /// <summary>Tải phân bố đều trên ô sàn q (kN/m², dương hướng xuống).</summary>
    public static int AreaUniform(Project p, IEnumerable<int> slabs, string pattern, double q, LoadAssignMode mode = LoadAssignMode.Add, string note = "Gán tải sàn")
    {
        CheckPattern(p, pattern);
        var set = new HashSet<int>(slabs ?? Enumerable.Empty<int>());
        int n = 0;
        foreach (var s in p.Slabs.Where(x => set.Contains(x.Id)))
        {
            if (mode != LoadAssignMode.Add) p.AreaLoads.RemoveAll(l => l.Slab == s.Id && l.Pattern == pattern && l.Source == "");
            if (mode != LoadAssignMode.Delete) p.AreaLoads.Add(new AreaLoad { Slab = s.Id, Pattern = pattern, Q = q, Note = note });
            n++;
        }
        return n;
    }

    /// <summary>Tải tại nút (kN, kN.m) theo hệ toạ độ tổng thể; v = {Fx, Fy, Fz, Mx, My, Mz}.</summary>
    public static int JointLoad(Project p, IEnumerable<int> nodes, string pattern, double[] v, LoadAssignMode mode = LoadAssignMode.Add)
    {
        CheckPattern(p, pattern);
        var set = new HashSet<int>(nodes ?? Enumerable.Empty<int>());
        v ??= new double[6];
        int n = 0;
        foreach (var nd in p.Nodes.Where(x => set.Contains(x.Id)))
        {
            if (mode != LoadAssignMode.Add) p.NodeLoads.RemoveAll(l => l.Node == nd.Id && l.Pattern == pattern && l.Source == "");
            if (mode != LoadAssignMode.Delete)
                p.NodeLoads.Add(new NodeLoad { Node = nd.Id, Pattern = pattern, Fx = V(v, 0), Fy = V(v, 1), Fz = V(v, 2), Mx = V(v, 3), My = V(v, 4), Mz = V(v, 5) });
            n++;
        }
        return n;
    }

    private static double V(double[] v, int i) => i < v.Length ? v[i] : 0;

    // ------------------------------------------------------------------
    // Liên kết, giải phóng, tiết diện, hệ số độ cứng, sàn cứng
    // ------------------------------------------------------------------
    /// <summary>Liên kết nút: r = {UX, UY, UZ, RX, RY, RZ} (true = giữ).</summary>
    public static int Restraint(Project p, IEnumerable<int> nodes, bool[] r)
    {
        var set = new HashSet<int>(nodes ?? Enumerable.Empty<int>());
        int n = 0;
        foreach (var nd in p.Nodes.Where(x => set.Contains(x.Id)))
        {
            var a = new bool[6];
            for (int i = 0; i < 6 && r != null && i < r.Length; i++) a[i] = r[i];
            nd.Restraint = a;
            n++;
        }
        return n;
    }

    /// <summary>Kiểm tra tổ hợp giải phóng gây biến hình (như cảnh báo của ETABS). null = hợp lệ.</summary>
    public static string ValidateReleases(bool[] relI, bool[] relJ)
    {
        string[] dof = { "P (dọc trục)", "V2", "V3", "T (xoắn)" };
        for (int k = 0; k < 4; k++)
            if (relI != null && relJ != null && k < relI.Length && k < relJ.Length && relI[k] && relJ[k])
                return $"Giải phóng {dof[k]} ở cả hai đầu thanh làm thanh biến hình – chỉ giải phóng ở một đầu.";
        if (relI != null && relJ != null && relI.Length > 5 && relJ.Length > 5)
        {
            if (relI[2] && relJ[4] && relI[4]) return "Giải phóng V3 cùng M2 hai đầu làm thanh biến hình.";
            if (relI[1] && relJ[5] && relI[5]) return "Giải phóng V2 cùng M3 hai đầu làm thanh biến hình.";
        }
        return null;
    }

    /// <summary>Giải phóng liên kết đầu thanh [P, V2, V3, T, M2, M3] ở đầu I và J (Frame Releases của ETABS).</summary>
    public static EditResult Releases(Project p, IEnumerable<int> frames, bool[] relI, bool[] relJ)
    {
        string err = ValidateReleases(relI, relJ);
        if (err != null) return EditResult.Fail(err);
        int n = 0;
        foreach (var f in Frames(p, frames))
        {
            f.ReleaseI = Copy6(relI); f.ReleaseJ = Copy6(relJ);
            n++;
        }
        return new EditResult { Ok = n > 0, Message = n > 0 ? $"Đã gán giải phóng đầu thanh cho {n} thanh." : "Chưa chọn thanh nào." };
    }

    private static bool[] Copy6(bool[] a)
    {
        var r = new bool[6];
        for (int i = 0; i < 6 && a != null && i < a.Length; i++) r[i] = a[i];
        return r;
    }

    /// <summary>Gán tiết diện cho các thanh.</summary>
    public static EditResult Section(Project p, IEnumerable<int> frames, string section)
    {
        if (string.IsNullOrEmpty(section) || p.FrameSections.All(s => s.Name != section)) return EditResult.Fail("Chưa khai báo tiết diện " + section);
        int n = 0;
        foreach (var f in Frames(p, frames)) { f.Section = section; n++; }
        p.InvalidateIndex();
        return new EditResult { Ok = n > 0, Message = n > 0 ? $"Đã gán tiết diện {section} cho {n} thanh." : "Chưa chọn thanh nào." };
    }

    /// <summary>Hệ số độ cứng thanh {A, As2, As3, J, I22, I33}; null hoặc toàn 1 = bỏ gán (dùng hệ số của tiết diện).</summary>
    public static int Modifiers(Project p, IEnumerable<int> frames, double[] mods)
    {
        double[] m = null;
        if (mods != null && mods.Any(v => Math.Abs(v - 1) > 1e-12))
        {
            m = new double[6];
            for (int i = 0; i < 6; i++) m[i] = i < mods.Length && mods[i] > 0 ? mods[i] : 1.0;
        }
        int n = 0;
        foreach (var f in Frames(p, frames)) { f.StiffnessModifiers = m == null ? null : (double[])m.Clone(); n++; }
        return n;
    }

    /// <summary>Bật / tắt sàn cứng cho một tầng (bật cả tuỳ chọn chung khi bật cho tầng).</summary>
    public static void Diaphragm(Project p, string story, bool on)
    {
        if (on)
        {
            p.DiaphragmOff?.Remove(story);
            if (p.DiaphragmOff != null && p.DiaphragmOff.Count == 0) p.DiaphragmOff = null;
            p.UseRigidDiaphragm = true;
        }
        else
        {
            p.DiaphragmOff ??= new List<string>();
            if (!p.DiaphragmOff.Contains(story)) p.DiaphragmOff.Add(story);
        }
    }

    /// <summary>Tầng đang dùng sàn cứng.</summary>
    public static bool DiaphragmOn(Project p, Story s) => p.UseRigidDiaphragm && !(p.DiaphragmOff?.Contains(s.Name) ?? false);

    /// <summary>Các nút đầu của thanh (để gán tải nút / liên kết cho thanh đang chọn): cột → nút dưới, dầm → hai nút.</summary>
    public static List<int> NodesOf(Project p, IEnumerable<int> frames, bool columnBaseOnly = false)
    {
        var r = new List<int>();
        foreach (var f in Frames(p, frames))
        {
            if (columnBaseOnly && f.IsVerticalKind)
            {
                var a = p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var b = p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
                if (a != null && b != null) r.Add(a.Z <= b.Z ? a.Id : b.Id);
                continue;
            }
            r.Add(f.NodeI); r.Add(f.NodeJ);
        }
        return r.Distinct().ToList();
    }

    /// <summary>Mô tả liên kết nút: "Ngàm", "Khớp", "Tự do" hoặc "UX UY…".</summary>
    public static string RestraintName(bool[] r)
    {
        if (r == null || r.All(x => !x)) return "Tự do";
        if (r.All(x => x)) return "Ngàm";
        if (r.Length >= 6 && r[0] && r[1] && r[2] && !r[3] && !r[4] && !r[5]) return "Khớp";
        string[] n = { "UX", "UY", "UZ", "RX", "RY", "RZ" };
        return string.Join(" ", Enumerable.Range(0, Math.Min(6, r.Length)).Where(i => r[i]).Select(i => n[i]));
    }

    /// <summary>Mô tả giải phóng: "I: M2 M3 / J: M3", rỗng = không giải phóng.</summary>
    public static string ReleaseName(FrameElement f)
    {
        string[] n = { "P", "V2", "V3", "T", "M2", "M3" };
        string S(bool[] a) => a == null ? "" : string.Join(" ", Enumerable.Range(0, Math.Min(6, a.Length)).Where(i => a[i]).Select(i => n[i]));
        string i1 = S(f.ReleaseI), j1 = S(f.ReleaseJ);
        if (i1 == "" && j1 == "") return "";
        return $"I: {(i1 == "" ? "–" : i1)} / J: {(j1 == "" ? "–" : j1)}";
    }
}
