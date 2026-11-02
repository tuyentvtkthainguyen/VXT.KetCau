using System.Globalization;
using System.Text.RegularExpressions;
using KetCauVXT.Analysis;
using KetCauVXT.Export;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Csi;

/// <summary>Cách dùng một trường hợp/tổ hợp nhập về.</summary>
public enum CaseUse
{
    /// <summary>Bỏ qua (không nhập)</summary>
    Ignore,
    /// <summary>Trường hợp tải: nhập, không dùng trực tiếp để thiết kế (tổ hợp của dự án có thể tham chiếu)</summary>
    Load,
    /// <summary>Tổ hợp TTGH I – thiết kế cường độ</summary>
    ULS,
    /// <summary>Tổ hợp TTGH II – võng, nứt, nền móng theo tải tiêu chuẩn</summary>
    SLS,
    /// <summary>Dùng cho cả TTGH I và TTGH II</summary>
    Both
}

/// <summary>Một trường hợp/tổ hợp tìm thấy trong các bảng nhập.</summary>
public class ImportedCase
{
    public string Name { get; set; } = "";
    public string CaseType { get; set; } = "";
    /// <summary>Có dòng Step Type = Max/Min (tổ hợp bao của ETABS/SAP) → tách thành "Tên (Max)" và "Tên (Min)".</summary>
    public bool HasMaxMin { get; set; }
    public int Rows { get; set; }
    public CaseUse Use { get; set; } = CaseUse.Load;
    public string Note { get; set; } = "";
}

/// <summary>Kết quả khớp một thanh của file nhập với thanh của dự án.</summary>
public class FrameMatchInfo
{
    public string External { get; set; } = "";
    public string Story { get; set; } = "";
    public int FrameId { get; set; }
    public string Label { get; set; } = "";
    /// <summary>"tên gốc", "tên xuất VXT", "toạ độ"; rỗng = không khớp</summary>
    public string Method { get; set; } = "";
    public bool Reversed { get; set; }
    public string Reason { get; set; } = "";
    public bool Matched => FrameId > 0;
}

/// <summary>Ánh xạ cột cho bảng nội lực của "phần mềm khác" (CSV tự do). Chỉ số cột −1 = không có (lấy 0).</summary>
public class GenericMapping
{
    public string Table { get; set; } = "";
    public string File { get; set; } = "";
    public int ColElement { get; set; } = -1;
    public int ColStation { get; set; } = -1;
    public int ColCase { get; set; } = -1;
    public int ColP { get; set; } = -1;
    public int ColV2 { get; set; } = -1;
    public int ColV3 { get; set; } = -1;
    public int ColT { get; set; } = -1;
    public int ColM2 { get; set; } = -1;
    public int ColM3 { get; set; } = -1;
    public string ForceUnit { get; set; } = "kN";
    public string LengthUnit { get; set; } = "m";
    /// <summary>Trạm đo là vị trí tương đối 0…1 thay vì khoảng cách (m).</summary>
    public bool StationRelative { get; set; }
    /// <summary>Đổi dấu M3 (phần mềm quy ước M dương căng trên).</summary>
    public bool FlipM3 { get; set; }
    /// <summary>Đổi dấu P (phần mềm quy ước nén dương).</summary>
    public bool FlipP { get; set; }
}

public class ResultImportOptions
{
    /// <summary>Lấy nội lực của chương trình (cùng tên trường hợp/tổ hợp) cho thanh không có trong file nhập.</summary>
    public bool FillUnmatchedFromOwn { get; set; } = true;
    /// <summary>Sai số khớp toạ độ (m).</summary>
    public double Tolerance { get; set; } = 0.02;
    /// <summary>Đơn vị lực / chiều dài khi bảng không có dòng đơn vị và file không ghi đơn vị (rỗng = kN, m).</summary>
    public string ForceUnit { get; set; } = "";
    public string LengthUnit { get; set; } = "";
    /// <summary>Tên phần mềm hiển thị; rỗng = tự nhận diện.</summary>
    public string Software { get; set; } = "";
    /// <summary>Tầng áp dụng kết quả SAFE (bảng không có cao độ/tầng); rỗng = tự chọn tầng chứa nhiều điểm nhất.</summary>
    public string SafeStory { get; set; } = "";
    /// <summary>Bề rộng dải SAFE khi bảng không ghi bề rộng (m).</summary>
    public double DefaultStripWidth { get; set; } = 1.0;
    /// <summary>Kể mô men xoắn M12 theo Wood–Armer (Mx ± |Mxy|) khi dùng mô men phần tử sàn.</summary>
    public bool WoodArmer { get; set; } = true;
    /// <summary>Dời toạ độ bảng SAFE về hệ toạ độ dự án (m).</summary>
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
}

/// <summary>Nhập kết quả phân tích từ ETABS / SAP2000 / SAFE / phần mềm khác thành <see cref="AnalysisResults"/> dùng cho thiết kế,
/// biểu đồ, bản vẽ, thuyết minh như kết quả của chương trình.
/// <para>Bảng hỗ trợ (Excel .xlsx "Export to Excel", CSV/TXT, .s2k/.$2k/.$et dạng Khoá=Giá trị):
/// ETABS "Element Forces - Beams/Columns/Braces", SAP2000 "Element Forces - Frames", "Joint Reactions", "Joint Displacements",
/// "Base Reactions" (kiểm tra), "Story Drifts", SAFE "Strip Forces", "Slab Forces"/"Element Forces - Area Shells" (M11, M22, M12),
/// "Soil Pressures", "Punching Shear Design"; bảng hình học "Joint Coordinates"/"Point Object Connectivity" +
/// "Connectivity - Frame"/"Beam/Column Object Connectivity" để khớp thanh theo toạ độ.</para>
/// <para>Khớp thanh: (a) tên gốc lưu khi nhập mô hình .e2k/.s2k (FrameElement.ExternalName/ExternalStory); (b) toạ độ hai đầu thanh khi có
/// bảng toạ độ nút + bảng nối thanh (cho cả thanh bị chia/gộp khác nhau – đoạn thẳng hàng chồng lên nhau); (c) tên do bộ xuất .e2k/.s2k
/// của chương trình đặt (C{Id}, B{Id}, D{Id}, {Id}, {Id}_k) có kiểm tra loại thanh và tầng.</para>
/// <para>Quy ước dấu: trục địa phương 1-2-3 của chương trình trùng ETABS/SAP2000 (thanh đứng: 2 ∥ +X; thanh ngang/xiên: 2 hướng lên);
/// nội lực "mặt dương" P kéo dương, M3 dương căng thớ −2 (dầm căng dưới) ở cả hai → giữ nguyên dấu khi thanh cùng chiều.
/// Thanh ngược chiều (đầu I/J đảo – vd. cột ETABS luôn có đầu I ở dưới): x = L − trạm, nội lực chuyển qua hệ toàn cục
/// f = −R_VXT·R_ngoàiᵀ·f_ngoài (góc xoay tiết diện 0: P, V3, T, M3 giữ dấu; V2, M2 đổi dấu).
/// Phản lực, chuyển vị nút: hệ toàn cục, phản lực = lực gối tác dụng lên kết cấu (giống chương trình).</para>
/// <para>Đơn vị: theo dòng đơn vị của từng cột ("kN", "Tonf", "kgf-cm"…) hoặc đơn vị chung của file (CurrUnits/UNITS) → kN, m.</para></summary>
public class ResultImportSession
{
    public Project Project { get; }
    /// <summary>Kết quả của chương trình (có thể null) – dùng bù thanh không khớp.</summary>
    public AnalysisResults Own { get; set; }
    public List<string> Files { get; } = new();
    public List<ExtTable> Tables { get; } = new();
    public List<ImportedCase> Cases { get; } = new();
    public List<FrameMatchInfo> Matches { get; } = new();
    /// <summary>Thanh BTCT/thép của dự án không có số liệu trong file nhập.</summary>
    public List<int> FramesWithoutData { get; } = new();
    public List<string> Messages { get; } = new();
    public ResultImportOptions Options { get; set; } = new();
    public List<GenericMapping> GenericMappings { get; } = new();

    public ResultImportSession(Project p, AnalysisResults own = null)
    {
        Project = p ?? throw new ArgumentNullException(nameof(p));
        Own = own;
    }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ==================================================================
    // ĐỌC FILE + NHẬN DIỆN BẢNG
    // ==================================================================
    public void AddFile(string path)
    {
        _analyzed = false;
        var tabs = ExternalTableReader.ReadFile(path);
        Files.Add(path);
        foreach (var t in tabs) { t.Kind = Classify(t); Tables.Add(t); }
    }

    /// <summary>Thêm bảng đã đọc sẵn (kiểm thử / tự kiểm tra).</summary>
    public void AddTables(IEnumerable<ExtTable> tabs, string path = "")
    {
        if (!string.IsNullOrEmpty(path) && !Files.Contains(path)) Files.Add(path);
        foreach (var t in tabs) { t.Kind = Classify(t); Tables.Add(t); }
    }

    /// <summary>Nhận diện loại bảng theo tên và cột.</summary>
    public static ExtTableKind Classify(ExtTable t)
    {
        string n = ExtTable.Norm(t.Name);
        // bảng định nghĩa mô hình trong file .s2k/.$2k/.$et (không phải kết quả)
        if (n.Contains("DEFINITIONS") || n.Contains("ASSIGNMENTS") || n.Contains("PROPERTIES") || n.StartsWith("CASE") || n.Contains("LOADCASE")
            || n.Contains("FRAMELOADS") || n.Contains("JOINTLOADS") || n.Contains("AREALOADS") || n.Contains("PREFERENCES") || n.Contains("OPTIONS")
            || n.Contains("PROGRAMCONTROL") || n.Contains("GRIDLINES") || n.Contains("COORDINATESYSTEMS") || n.Contains("SECTIONDESIGNER"))
            return n.Contains("JOINTCOORDINATES") ? ExtTableKind.JointCoordinates : ExtTableKind.Ignored;
        bool hasCase = t.Has("Output Case", "OutputCase", "Load Case/Combo", "LoadCase", "Load Case", "Combo", "Case", "Load Combo");
        bool hasStation = t.Has("Station");
        bool hasM3 = t.Has("M3");
        if (n.Contains("PUNCHING")) return ExtTableKind.PunchingShear;
        if (n.Contains("SOILPRESSURE")) return ExtTableKind.SoilPressures;
        if (n.Contains("STRIP") && hasStation) return ExtTableKind.StripForces;
        if (n.Contains("BASEREACTION")) return ExtTableKind.BaseReactions;
        if (n.Contains("DRIFT")) return ExtTableKind.StoryDrifts;
        if ((n.Contains("REACTION") || n.Contains("SPRINGFORCE")) && hasCase) return ExtTableKind.JointReactions;
        if (n.Contains("DISPLACEMENT") && hasCase && (t.Has("U1", "UX"))) return ExtTableKind.JointDisplacements;
        bool shellName = n.Contains("AREA") || n.Contains("SHELL") || n.Contains("SLAB") || n.Contains("WALL") || n.Contains("PIER") || n.Contains("SPANDREL");
        if (shellName && hasCase && (t.Has("M11", "MXX", "Mx") || t.Has("M22", "MYY", "My"))) return ExtTableKind.SlabForces;
        if (!shellName && hasCase && hasStation && hasM3) return ExtTableKind.FrameForces;
        if (!hasCase)
        {
            bool joints = n.Contains("JOINTCOORDINATES") || n.Contains("POINTOBJECTCONNECTIVITY") || n.Contains("POINTCOORDINATES")
                          || n.Contains("POINTCONNECTIVITY") || n.Contains("OBJECTGEOMETRYPOINT")
                          || (t.Has("Joint", "Point", "Unique Name", "Label") && t.Has("X", "XorR", "GlobalX") && t.Has("Y", "GlobalY") && t.Has("Z", "GlobalZ") && !t.Has("JointI", "UniquePtI"));
            if (joints) return ExtTableKind.JointCoordinates;
            if (t.Has("JointI", "UniquePtI", "PointI", "Point I", "UniquePointI") && t.Has("JointJ", "UniquePtJ", "PointJ", "Point J", "UniquePointJ")
                && !shellName && !n.Contains("LINK"))
                return ExtTableKind.FrameConnectivity;
            // bảng CSV/Excel tự do có ≥ 3 cột số → "phần mềm khác" (người dùng ánh xạ cột); bảng mô hình .s2k → bỏ qua
            if (!t.KeyValue && t.Rows.Count > 0 && t.Rows.Take(5).Max(r => r.Count(c => ExtTable.TryNum(c, out _))) >= 3) return ExtTableKind.Unknown;
            return ExtTableKind.Ignored;
        }
        return ExtTableKind.Unknown;
    }

    /// <summary>Đặt ánh xạ cột cho một bảng "phần mềm khác" → bảng nội lực chuẩn.</summary>
    public void ApplyGeneric(GenericMapping m)
    {
        var src = Tables.FirstOrDefault(t => t.Name == m.Table && (string.IsNullOrEmpty(m.File) || t.File == m.File));
        if (src == null) throw new InvalidOperationException("Không tìm thấy bảng " + m.Table);
        if (m.ColElement < 0 || m.ColCase < 0) throw new InvalidOperationException("Cần chọn cột tên cấu kiện và cột trường hợp/tổ hợp tải.");
        var t = new ExtTable
        {
            Name = "Nội lực (phần mềm khác) – " + src.Name, File = src.File, Program = "Khác", Kind = ExtTableKind.FrameForces,
            Headers = new List<string> { "Frame", "Station", "OutputCase", "P", "V2", "V3", "T", "M2", "M3" },
            Units = new List<string> { "", m.StationRelative ? "" : m.LengthUnit, "", m.ForceUnit, m.ForceUnit, m.ForceUnit,
                m.ForceUnit + "-" + m.LengthUnit, m.ForceUnit + "-" + m.LengthUnit, m.ForceUnit + "-" + m.LengthUnit },
            StationRelative = m.StationRelative
        };
        string V(string[] r, int c, double sign = 1) =>
            c < 0 ? "0" : ExtTable.TryNum(ExtTable.Cell(r, c), out double v) ? (sign * v).ToString("R", Inv) : "";
        foreach (var r in src.Rows)
        {
            if (m.ColStation >= 0 && !ExtTable.TryNum(ExtTable.Cell(r, m.ColStation), out _)) continue;
            t.Rows.Add(new[]
            {
                ExtTable.Cell(r, m.ColElement), m.ColStation < 0 ? "0" : V(r, m.ColStation), ExtTable.Cell(r, m.ColCase),
                V(r, m.ColP, m.FlipP ? -1 : 1), V(r, m.ColV2), V(r, m.ColV3), V(r, m.ColT), V(r, m.ColM2), V(r, m.ColM3, m.FlipM3 ? -1 : 1)
            });
        }
        src.Kind = ExtTableKind.Ignored;
        Tables.Add(t);
        GenericMappings.Add(m);
    }

    /// <summary>Tên phần mềm nhận diện từ các bảng.</summary>
    public string DetectSoftware()
    {
        if (!string.IsNullOrWhiteSpace(Options.Software)) return Options.Software.Trim();
        var set = new List<string>();
        void Add(string s) { if (!set.Contains(s)) set.Add(s); }
        foreach (var t in Tables.Where(x => x.Kind is not (ExtTableKind.Ignored or ExtTableKind.Unknown or ExtTableKind.JointCoordinates or ExtTableKind.FrameConnectivity)))
        {
            string pr = (t.Program ?? "").ToUpperInvariant();
            if (pr.Contains("ETABS")) Add("ETABS");
            else if (pr.Contains("SAFE")) Add("SAFE");
            else if (pr.Contains("SAP")) Add("SAP2000");
            else if (pr == "KHÁC") Add("Phần mềm khác");
            else if (t.Kind is ExtTableKind.StripForces or ExtTableKind.SoilPressures or ExtTableKind.PunchingShear) Add("SAFE");
            else if (t.Kind == ExtTableKind.FrameForces)
            {
                string n = ExtTable.Norm(t.Name);
                if (n.Contains("BEAMS") || n.Contains("COLUMNS") || n.Contains("BRACES") || t.Has("Story")) Add("ETABS");
                else Add("SAP2000");
            }
            else if (t.Has("Story")) Add("ETABS");
        }
        return set.Count == 0 ? "Phần mềm khác" : string.Join(" + ", set);
    }

    // ==================================================================
    // PHÂN TÍCH: trường hợp tải + khớp thanh
    // ==================================================================
    private ExtTable[] Of(ExtTableKind k) => Tables.Where(t => t.Kind == k).ToArray();

    private static int CaseCol(ExtTable t) => t.Col("Output Case", "OutputCase", "Load Case/Combo", "LoadCase", "Load Case", "Combo", "Load Combo", "Case");
    private static int CaseTypeCol(ExtTable t) => t.Col("Case Type", "CaseType");
    private static int StepCol(ExtTable t) => t.Col("Step Type", "StepType");

    private void ApplyFileUnits(ExtTable t)
    {
        if (string.IsNullOrEmpty(t.FileForceUnit) && !string.IsNullOrEmpty(Options.ForceUnit)) t.FileForceUnit = Options.ForceUnit;
        if (string.IsNullOrEmpty(t.FileLengthUnit) && !string.IsNullOrEmpty(Options.LengthUnit)) t.FileLengthUnit = Options.LengthUnit;
    }

    /// <summary>Đọc danh sách trường hợp/tổ hợp, tự phân loại; khớp thanh. Gọi lại sau khi thêm file hoặc đổi tuỳ chọn.</summary>
    private bool _analyzed;

    public void Analyze()
    {
        _analyzed = true;
        Messages.Clear();
        foreach (var t in Tables) ApplyFileUnits(t);
        // ---- trường hợp ----
        var old = Cases.ToDictionary(c => c.Name, c => c.Use);
        Cases.Clear();
        var map = new Dictionary<string, ImportedCase>();
        foreach (var t in Tables.Where(x => x.Kind is ExtTableKind.FrameForces or ExtTableKind.JointReactions or ExtTableKind.JointDisplacements
                                             or ExtTableKind.StripForces or ExtTableKind.SlabForces or ExtTableKind.SoilPressures))
        {
            int cc = CaseCol(t), ct = CaseTypeCol(t), cs = StepCol(t);
            if (cc < 0) continue;
            foreach (var r in t.Rows)
            {
                string name = ExtTable.Cell(r, cc);
                if (name.Length == 0) continue;
                if (!map.TryGetValue(name, out var ic)) { ic = new ImportedCase { Name = name, CaseType = ExtTable.Cell(r, ct) }; map[name] = ic; Cases.Add(ic); }
                ic.Rows++;
                string st = ExtTable.Cell(r, cs);
                if (st.Equals("Max", StringComparison.OrdinalIgnoreCase) || st.Equals("Min", StringComparison.OrdinalIgnoreCase)) ic.HasMaxMin = true;
            }
        }
        foreach (var c in Cases)
        {
            c.Use = AutoUse(c, out string note);
            c.Note = note;
            if (old.TryGetValue(c.Name, out var u)) c.Use = u;
        }
        // nếu không có tổ hợp TTGH I nào → dùng tổ hợp của dự án (nếu đủ trường hợp tải) hoặc coi các trường hợp tải là tổ hợp thiết kế
        if (!Cases.Any(c => c.Use is CaseUse.ULS or CaseUse.Both) && Cases.Count > 0)
        {
            var loads = Cases.Where(c => c.Use == CaseUse.Load).Select(c => c.Name).ToHashSet();
            bool projectOk = Project.Combinations.Any(c => c.Type == ComboType.ULS) &&
                             Project.Combinations.Where(c => c.Type == ComboType.ULS).All(c => LeavesIn(c.Name, loads, 0));
            if (projectOk) Messages.Add("File chỉ có các trường hợp tải – sẽ tổ hợp theo các tổ hợp TTGH I/II của dự án.");
            else Messages.Add("Chưa có tổ hợp nào được phân loại TTGH I – hãy đánh dấu các tổ hợp thiết kế (nếu không, các trường hợp tải sẽ được dùng trực tiếp).");
        }
        MatchFrames();
    }

    private bool LeavesIn(string name, HashSet<string> cases, int depth)
    {
        if (cases.Contains(name)) return true;
        if (depth > 8) return false;
        var c = Project.Combinations.FirstOrDefault(x => x.Name == name);
        return c != null && c.Items.Count > 0 && c.Items.All(i => LeavesIn(i.Case, cases, depth + 1));
    }

    /// <summary>Phân loại tự động theo tên / loại trường hợp: trùng tổ hợp dự án → theo loại; "ULS", "TTGH1", "COMB", "ENVE", "BAO", "TH1"… → TTGH I;
    /// "SLS", "TTGH2", "SERV", "TC1"… → TTGH II; Modal → bỏ qua; trường hợp tĩnh → trường hợp tải.</summary>
    public CaseUse AutoUse(ImportedCase c, out string note)
    {
        note = "";
        var pc = Project.Combinations.FirstOrDefault(x => x.Name == c.Name);
        if (pc != null)
        {
            note = "trùng tổ hợp của dự án";
            if (pc.Type == ComboType.Envelope)
            {
                // tổ hợp bao (Max/Min từng thành phần, không đồng thời) – dùng vẽ biểu đồ; thiết kế theo các tổ hợp thành phần
                note = "tổ hợp bao của dự án – dùng vẽ biểu đồ, thiết kế theo các tổ hợp thành phần";
                return CaseUse.Load;
            }
            return pc.Type switch { ComboType.ULS => CaseUse.ULS, ComboType.SLS => CaseUse.SLS, _ => CaseUse.ULS };
        }
        if (Project.Pattern(c.Name) != null) { note = "trùng trường hợp tải của dự án"; return CaseUse.Load; }
        string ty = ExtTable.Norm(c.CaseType);
        if (ty.Contains("MODAL") || ty.Contains("EIGEN") || ty.Contains("RITZ")) { note = "dao động riêng – bỏ qua"; return CaseUse.Ignore; }
        string n = " " + Regex.Replace(RemoveDiacritics(c.Name).ToUpperInvariant(), "[^A-Z0-9]+", " ") + " ";
        string nc = n.Replace(" ", "");
        if (Regex.IsMatch(n, @" (SLS|SERV|SERVICE|DEFL|TC|TCTC|TTGH ?II|TTGH ?2|TIEU ?CHUAN)\d*( |$)") || nc.Contains("TTGH2") || nc.Contains("TTGHII")
            || Regex.IsMatch(nc, @"^(SLS|SERV|TC|TCTC)\d") || nc.StartsWith("SLS") || nc.StartsWith("SERV"))
        { note = "tên gợi ý TTGH II"; return CaseUse.SLS; }
        if (nc.Contains("ULS") || nc.Contains("TTGH1") || nc.Contains("TTGHI") || nc.Contains("COMB") || nc.Contains("ENVE") || nc.Contains("BAO")
            || Regex.IsMatch(nc, @"^(TH|THT|TT?TH|UDCON|DCON|DSTL|DWAL|DCMP|ULT|STRENGTH|COMBO|CB)\d") || nc.Contains("TINHTOAN"))
        { note = "tên gợi ý TTGH I"; return CaseUse.ULS; }
        if (ty.Contains("COMB") || ty.Contains("COMBO")) { note = "tổ hợp – mặc định TTGH I"; return CaseUse.ULS; }
        if (ty.Contains("RESPSPEC") || ty.Contains("SPECTRUM")) { note = "phổ phản ứng (giá trị tuyệt đối) – chỉ dùng qua tổ hợp"; return CaseUse.Load; }
        if (c.HasMaxMin) { note = "có Max/Min – tổ hợp bao, mặc định TTGH I"; return CaseUse.ULS; }
        return CaseUse.Load;
    }

    public static string RemoveDiacritics(string s)
    {
        var norm = (s ?? "").Replace('đ', 'd').Replace('Đ', 'D').Normalize(System.Text.NormalizationForm.FormD);
        return new string(norm.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(System.Text.NormalizationForm.FormC);
    }

    // ------------------------------------------------------------------ khớp thanh
    private sealed class ExtFrame
    {
        public string Key = "", Label = "", Story = "", Unique = "", TableName = "";
        public bool Etabs;
        public bool HasGeom;
        public Vec3 I, J;
        public List<Contribution> Contribs = new();
    }

    private sealed class Contribution
    {
        public int FrameId;
        public string Method = "";
        public double Offset, Dir = 1, Scale = 1;
        public bool Reversed, Curved;
        public bool Relative;
        /// <summary>Khoảng trạm của thanh ngoài ứng với đầu I/J của thanh dự án (để nội suy tại đầu thanh); NaN = không có</summary>
        public double SAtI = double.NaN, SAtJ = double.NaN;
    }

    private readonly Dictionary<string, ExtFrame> _ext = new();
    private Dictionary<string, Vec3> _joints = new();

    private static string FrameLabelOf(ExtTable t, string[] r) =>
        ExtTable.Cell(r, t.Col("Beam", "Column", "Brace", "Frame", "Label", "Line", "Element", "Member", "Frame Name", "Line Object"));

    private static string Key(string story, string label, string unique) =>
        unique.Length > 0 ? "U:" + unique : story.Length > 0 ? "S:" + story + "|" + label : "L:" + label;

    private void CollectExtFrames()
    {
        _ext.Clear();
        foreach (var t in Of(ExtTableKind.FrameForces))
        {
            int cu = t.Col("Unique Name", "UniqueName"), cs = t.Col("Story");
            bool etabs = (t.Program ?? "").ToUpperInvariant().Contains("ETABS") || cs >= 0
                         || ExtTable.Norm(t.Name).Contains("BEAMS") || ExtTable.Norm(t.Name).Contains("COLUMNS");
            foreach (var r in t.Rows)
            {
                string lab = FrameLabelOf(t, r), st = ExtTable.Cell(r, cs), un = ExtTable.Cell(r, cu);
                if (lab.Length == 0 && un.Length == 0) continue;
                string k = Key(st, lab, un);
                if (!_ext.ContainsKey(k)) _ext[k] = new ExtFrame { Key = k, Label = lab.Length > 0 ? lab : un, Story = st, Unique = un, TableName = t.Name, Etabs = etabs };
            }
        }
    }

    private void CollectGeometry()
    {
        _joints = new Dictionary<string, Vec3>();
        foreach (var t in Of(ExtTableKind.JointCoordinates))
        {
            int cu = t.Col("Unique Name", "UniqueName"), cs = t.Col("Story"), cl = t.Col("Joint", "Point", "Label", "Point Label", "Joint Label", "Name");
            int cx = t.Col("GlobalX", "X", "XorR"), cy = t.Col("GlobalY", "Y"), cz = t.Col("GlobalZ", "Z");
            if (cx < 0 || cy < 0) continue;
            double fx = t.Factor(cx, Qty.Length), fy = t.Factor(cy, Qty.Length), fz = cz >= 0 ? t.Factor(cz, Qty.Length) : 1;
            foreach (var r in t.Rows)
            {
                if (!ExtTable.TryNum(ExtTable.Cell(r, cx), out double x) || !ExtTable.TryNum(ExtTable.Cell(r, cy), out double y)) continue;
                double z = 0;
                if (cz >= 0) ExtTable.TryNum(ExtTable.Cell(r, cz), out z);
                var v = new Vec3(x * fx, y * fy, z * fz);
                string un = ExtTable.Cell(r, cu), lab = ExtTable.Cell(r, cl), st = ExtTable.Cell(r, cs);
                if (un.Length > 0) _joints["U:" + un] = v;
                if (lab.Length > 0)
                {
                    if (st.Length > 0) _joints["S:" + st + "|" + lab] = v;
                    else _joints["L:" + lab] = v;
                }
            }
        }
        if (_joints.Count == 0) return;
        foreach (var t in Of(ExtTableKind.FrameConnectivity))
        {
            int cu = t.Col("Unique Name", "UniqueName"), cs = t.Col("Story");
            int ci = t.Col("UniquePtI", "Unique Pt I", "UniquePointI", "JointI", "PointI", "Point I");
            int cj = t.Col("UniquePtJ", "Unique Pt J", "UniquePointJ", "JointJ", "PointJ", "Point J");
            bool uniqueRefs = ExtTable.Norm(t.Headers[ci]).StartsWith("UNIQUE");
            foreach (var r in t.Rows)
            {
                string lab = FrameLabelOf(t, r), st = ExtTable.Cell(r, cs), un = ExtTable.Cell(r, cu);
                Vec3? a = JointXyz(ExtTable.Cell(r, ci), st, uniqueRefs), b = JointXyz(ExtTable.Cell(r, cj), st, uniqueRefs);
                if (a == null || b == null) continue;
                foreach (var k in new[] { un.Length > 0 ? "U:" + un : null, lab.Length > 0 && st.Length > 0 ? "S:" + st + "|" + lab : null, lab.Length > 0 ? "L:" + lab : null })
                {
                    if (k == null || !_ext.TryGetValue(k, out var ef)) continue;
                    ef.HasGeom = true; ef.I = a.Value; ef.J = b.Value;
                    break;
                }
            }
        }
    }

    private Vec3? JointXyz(string name, string story, bool unique)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (unique && _joints.TryGetValue("U:" + name, out var v)) return v;
        if (story.Length > 0 && _joints.TryGetValue("S:" + story + "|" + name, out v)) return v;
        if (_joints.TryGetValue("L:" + name, out v)) return v;
        if (_joints.TryGetValue("U:" + name, out v)) return v;
        return null;
    }

    private void MatchFrames()
    {
        Matches.Clear();
        FramesWithoutData.Clear();
        CollectExtFrames();
        CollectGeometry();
        var p = Project;
        p.InvalidateIndex();
        // tên gốc: một đối tượng ETABS/SAP có thể ứng với nhiều thanh của dự án (đã chia tại nút) → các đoạn thẳng hàng
        var byExt = new Dictionary<string, List<FrameElement>>(StringComparer.OrdinalIgnoreCase);
        var byExtLabel = new Dictionary<string, List<FrameElement>>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in p.Frames.Where(x => !string.IsNullOrEmpty(x.ExternalName)))
        {
            string k1 = (f.ExternalStory ?? "") + "|" + f.ExternalName;
            if (!byExt.TryGetValue(k1, out var l1)) byExt[k1] = l1 = new List<FrameElement>();
            l1.Add(f);
            if (!byExtLabel.TryGetValue(f.ExternalName, out var l)) byExtLabel[f.ExternalName] = l = new List<FrameElement>();
            l.Add(f);
        }
        var byId = p.Frames.ToDictionary(f => f.Id);
        double tol = Math.Max(1e-4, Options.Tolerance);

        foreach (var ef in _ext.Values)
        {
            var mi = new FrameMatchInfo { External = ef.Label + (ef.Unique.Length > 0 && ef.Unique != ef.Label ? " (" + ef.Unique + ")" : ""), Story = ef.Story };
            ef.Contribs.Clear();
            // (a) tên gốc
            List<FrameElement> fa = null;
            if (byExt.Count > 0)
            {
                if (ef.Story.Length > 0) byExt.TryGetValue(ef.Story + "|" + ef.Label, out fa);
                if (fa == null && ef.Story.Length == 0) byExt.TryGetValue("|" + ef.Label, out fa);
                if (fa == null && byExtLabel.TryGetValue(ef.Label, out var lst) && lst.Select(x => x.ExternalStory).Distinct().Count() == 1) fa = lst;
                if (fa == null && ef.Unique.Length > 0 && byExtLabel.TryGetValue(ef.Unique, out var l2) && l2.Select(x => x.ExternalStory).Distinct().Count() == 1) fa = l2;
            }
            if (fa != null && fa.Count > 0)
            {
                ef.Contribs.AddRange(PieceContribs(fa));
                Fill(mi, fa[0], "tên gốc", false);
                if (fa.Count > 1) mi.Reason = "đối tượng gồm " + fa.Count + " thanh của dự án (đã chia tại nút): " + string.Join(", ", fa.Select(x => x.Id));
                Matches.Add(mi);
                continue;
            }
            // (b) toạ độ
            if (ef.HasGeom)
            {
                var cs = GeometricContribs(ef, tol);
                if (cs.Count > 0)
                {
                    ef.Contribs.AddRange(cs);
                    var f0 = byId[cs[0].FrameId];
                    Fill(mi, f0, "toạ độ", cs[0].Reversed);
                    if (cs.Count > 1) mi.Reason = "trùng " + cs.Count + " thanh: " + string.Join(", ", cs.Select(c => c.FrameId));
                    Matches.Add(mi);
                    continue;
                }
                mi.Reason = "toạ độ hai đầu không trùng thanh nào của dự án (sai số " + DesignF(tol) + " m)";
            }
            // (c) tên do bộ xuất .e2k/.s2k của chương trình đặt
            string why = null;
            // (tên duy nhất "Unique Name" của ETABS là số do ETABS tự đánh – không dùng để suy ra Id)
            if (TryId(ef.Label, out int id, out int seg, out char prefix))
            {
                if (!byId.TryGetValue(id, out var f)) why = "không có thanh Id " + id;
                else
                {
                    string ln = E2kExporter.LineName(f, p);
                    if (prefix != '\0' && char.ToUpperInvariant(prefix) != ln[0]) why = $"tên {ef.Label} khác loại thanh {ln} của dự án";
                    else if (ef.Story.Length > 0 && !StoryConsistent(f, ef.Story)) why = $"tầng {ef.Story} không khớp cao độ thanh {f.Id}";
                    else
                    {
                        var c = new Contribution { FrameId = f.Id, Method = "tên xuất VXT" };
                        double L = p.Length(f);
                        // ETABS: đầu I của cột/thanh xiên ở dưới; dầm theo thứ tự điểm như khi xuất
                        bool etabsLower = ef.Etabs && ln[0] != 'B';
                        bool rev = etabsLower && p.Node(f.NodeI).Z > p.Node(f.NodeJ).Z + 1e-6;
                        double segOff = 0;
                        if (seg > 0)
                        {
                            var csegs = f.IsCurved ? CurvedBeam.Segments(p, f) : null;
                            if (csegs != null && seg <= csegs.Count) { segOff = csegs[seg - 1].T0 * L; c.Scale = csegs[seg - 1].LoadScale; c.Curved = true; }
                            else { int n = Math.Max(2, p.Steel.TaperSegments); segOff = (seg - 1) * L / n; }
                        }
                        c.Reversed = rev;
                        c.Offset = rev ? L - segOff : segOff;
                        c.Dir = rev ? -1 : 1;
                        ef.Contribs.Add(c);
                        Fill(mi, f, "tên xuất VXT", rev);
                        Matches.Add(mi);
                        continue;
                    }
                }
            }
            mi.Reason = string.IsNullOrEmpty(mi.Reason) ? (why ?? "không nhận diện được tên thanh") : mi.Reason + (why != null ? "; " + why : "");
            Matches.Add(mi);
        }
        var have = new HashSet<int>(_ext.Values.SelectMany(e => e.Contribs).Select(c => c.FrameId));
        FramesWithoutData.AddRange(p.Frames.Where(f => !have.Contains(f.Id)).Select(f => f.Id));
        if (_ext.Count > 0)
        {
            int um = Matches.Count(m => !m.Matched);
            Messages.Add($"Khớp {Matches.Count(m => m.Matched)}/{Matches.Count} thanh của file nhập; {um} không khớp; {FramesWithoutData.Count}/{p.Frames.Count} thanh của dự án không có số liệu.");
        }
    }

    private static string DesignF(double v) => v.ToString("0.###", Inv);

    /// <summary>Các thanh của dự án cùng tên gốc (một đối tượng ngoài đã chia tại nút, giữ chiều I→J): trạm ngoài đo từ đầu I xa nhất.</summary>
    private List<Contribution> PieceContribs(List<FrameElement> pieces)
    {
        var p = Project;
        if (pieces.Count == 1) return new List<Contribution> { new() { FrameId = pieces[0].Id, Method = "tên gốc" } };
        var f0 = pieces[0];
        var u = (p.Node(f0.NodeJ).P - p.Node(f0.NodeI).P).Normalize();
        var o = pieces.Select(f => p.Node(f.NodeI).P).OrderBy(v => v.Dot(u)).First();
        var res = new List<Contribution>();
        foreach (var f in pieces)
        {
            var a = p.Node(f.NodeI).P; var b = p.Node(f.NodeJ).P;
            double sa = (a - o).Dot(u), sb = (b - o).Dot(u);
            bool rev = sb < sa;
            res.Add(new Contribution
            {
                FrameId = f.Id, Method = "tên gốc", Reversed = rev, Dir = rev ? -1 : 1, Offset = rev ? sa : -sa,
                SAtI = sa, SAtJ = sb
            });
        }
        return res;
    }

    private static void Fill(FrameMatchInfo mi, FrameElement f, string method, bool rev)
    {
        mi.FrameId = f.Id; mi.Label = f.Label; mi.Method = method; mi.Reversed = rev;
        if (rev) mi.Reason = "ngược chiều I–J (x = L − trạm, đổi dấu V2, M2)";
    }

    /// <summary>Các thanh dự án thẳng hàng và chồng lên đoạn thanh ngoài (khớp đúng, thanh ngoài bị chia nhỏ hoặc gộp dài hơn).</summary>
    private List<Contribution> GeometricContribs(ExtFrame ef, double tol)
    {
        var res = new List<Contribution>();
        var p = Project;
        var A = ef.I; var B = ef.J;
        double Le = (B - A).Length;
        if (Le < 1e-6) return res;
        var ue = (B - A) * (1 / Le);
        foreach (var f in p.Frames)
        {
            if (f.IsCurved) continue;
            var P = p.Node(f.NodeI).P; var Q = p.Node(f.NodeJ).P;
            double Lf = (Q - P).Length;
            if (Lf < 1e-6) continue;
            var uf = (Q - P) * (1 / Lf);
            double dot = ue.Dot(uf);
            if (Math.Abs(Math.Abs(dot) - 1) > 1e-4) continue;
            // P, Q trên đường thẳng AB
            if (Dist(P, A, ue) > tol || Dist(Q, A, ue) > tol) continue;
            double tP = (P - A).Dot(ue), tQ = (Q - A).Dot(ue);
            double lo = Math.Max(Math.Min(tP, tQ), 0), hi = Math.Min(Math.Max(tP, tQ), Le);
            if (hi - lo < Math.Max(tol, 0.05 * Math.Min(Lf, Le))) continue;
            bool rev = dot < 0;
            var c = new Contribution
            {
                FrameId = f.Id, Method = "toạ độ", Reversed = rev, Dir = rev ? -1 : 1,
                Offset = (A - P).Dot(uf)
            };
            // trạm của thanh ngoài tại đầu I/J của thanh dự án (nếu nằm trong thanh ngoài)
            if (tP >= -tol && tP <= Le + tol) c.SAtI = Math.Clamp(tP, 0, Le);
            if (tQ >= -tol && tQ <= Le + tol) c.SAtJ = Math.Clamp(tQ, 0, Le);
            res.Add(c);
        }
        return res;
    }

    private static double Dist(Vec3 x, Vec3 a, Vec3 u)
    {
        var d = x - a;
        var perp = d - u * d.Dot(u);
        return perp.Length;
    }

    private bool StoryConsistent(FrameElement f, string story)
    {
        var p = Project;
        double z = Math.Max(p.Node(f.NodeI).Z, p.Node(f.NodeJ).Z);
        if (!TryStoryElevation(story, out double ze)) return true;   // tầng không xác định → không kiểm tra
        return Math.Abs(ze - z) < 2e-3;
    }

    /// <summary>Cao độ của tên tầng ETABS: tầng của dự án, "Base", hoặc "Z{mm}" do bộ xuất .e2k đặt.</summary>
    public bool TryStoryElevation(string story, out double z)
    {
        z = 0;
        var p = Project;
        var st = p.Stories.FirstOrDefault(s => s.Name.Equals(story, StringComparison.OrdinalIgnoreCase));
        if (st != null) { z = st.Elevation; return true; }
        if (story.Equals("Base", StringComparison.OrdinalIgnoreCase)) { z = p.BaseElevation; return true; }
        var m = Regex.Match(story, @"^Z(-?\d+)$");
        if (m.Success) { z = int.Parse(m.Groups[1].Value, Inv) / 1000.0; return true; }
        return false;
    }

    /// <summary>"C12" → 12 ; "12" → 12 ; "12_3" → (12, đoạn 3) ; "B5_2" → (5, 2).</summary>
    public static bool TryId(string name, out int id, out int seg, out char prefix)
    {
        id = 0; seg = 0; prefix = '\0';
        if (string.IsNullOrWhiteSpace(name)) return false;
        string s = name.Trim();
        int i = 0;
        while (i < s.Length && char.IsLetter(s[i])) i++;
        if (i > 1) return false;              // tên kiểu "COT12" – không phải tên do chương trình xuất
        if (i == 1) prefix = s[0];
        int j = i;
        while (j < s.Length && char.IsDigit(s[j])) j++;
        if (j == i || !int.TryParse(s.Substring(i, j - i), NumberStyles.Integer, Inv, out id)) return false;
        if (j == s.Length) return true;
        if (s[j] == '_' && int.TryParse(s.Substring(j + 1), NumberStyles.Integer, Inv, out int k)) { seg = k; return true; }
        return false;
    }

    // ==================================================================
    // DỰNG KẾT QUẢ
    // ==================================================================
    /// <summary>Tên trường hợp lưu trong kết quả: "Tên (Max)"/"Tên (Min)" với dòng Step Type Max/Min.</summary>
    public static string CaseKey(string name, string step) =>
        step.Equals("Max", StringComparison.OrdinalIgnoreCase) ? name + " (Max)" :
        step.Equals("Min", StringComparison.OrdinalIgnoreCase) ? name + " (Min)" : name;

    private Dictionary<string, CaseUse> UseMap() => Cases.ToDictionary(c => c.Name, c => c.Use);

    /// <summary>Dựng bộ kết quả từ các bảng đã đọc theo phân loại hiện tại.</summary>
    public AnalysisResults Build()
    {
        if (!_analyzed) Analyze();
        var p = Project;
        p.InvalidateIndex();
        var res = new AnalysisResults(p);
        string sw = DetectSoftware();
        var info = new ExternalResultInfo { Software = sw, ImportedAt = DateTime.Now };
        foreach (var f in Files.Distinct())
            info.Files.Add(new ExternalFileInfo { Path = f, Modified = File.Exists(f) ? File.GetLastWriteTime(f) : DateTime.MinValue });
        res.External = info;
        var use = UseMap();
        bool Keep(string name) => !use.TryGetValue(name, out var u) || u != CaseUse.Ignore;
        CaseResult CaseOf(string key)
        {
            if (!res.Cases.TryGetValue(key, out var cr)) res.Cases[key] = cr = new CaseResult { Name = key };
            return cr;
        }

        // ---------------- nội lực thanh ----------------
        var extData = new Dictionary<string, Dictionary<string, List<(double s, double[] f)>>>();   // khoá thanh ngoài → trường hợp → dòng
        var units = new List<string>();
        // chỉ nhập mô men sàn (SAFE) – nội lực khung, phản lực lấy theo kết quả hiện có của chương trình
        bool frameTables = Of(ExtTableKind.FrameForces).Length > 0 || Of(ExtTableKind.JointReactions).Length > 0;
        if (!frameTables && Own != null && Own.External == null)
        {
            foreach (var (k, c) in Own.Cases)
                res.Cases[k] = new CaseResult
                {
                    Name = c.Name, Disp = new Dictionary<int, double[]>(c.Disp), Reactions = new Dictionary<int, double[]>(c.Reactions),
                    Frames = new Dictionary<int, FrameForces>(c.Frames)
                };
            res.LateralForces.AddRange(Own.LateralForces);
            Note(info, "File chỉ có số liệu sàn/móng – nội lực khung, phản lực giữ theo kết quả phân tích của chương trình; tổ hợp theo dự án.");
            _ownBase = true;
        }
        else _ownBase = false;
        foreach (var t in Of(ExtTableKind.FrameForces))
        {
            int cc = CaseCol(t), cs = StepCol(t), csta = t.Col("Station"), cu = t.Col("Unique Name", "UniqueName"), cst = t.Col("Story");
            int[] cf = { t.Col("P"), t.Col("V2"), t.Col("V3"), t.Col("T"), t.Col("M2"), t.Col("M3") };
            var qty = new[] { Qty.Force, Qty.Force, Qty.Force, Qty.Moment, Qty.Moment, Qty.Moment };
            var fac = new double[6];
            for (int k = 0; k < 6; k++) fac[k] = cf[k] >= 0 ? t.Factor(cf[k], qty[k]) : 0;
            double fs = t.StationRelative ? 1 : t.Factor(csta, Qty.Length);
            info.Tables.Add(new ExternalTableUse { Table = t.Name, File = t.File, Rows = t.Rows.Count, Units = t.UnitsText() });
            if (cf[5] >= 0) units.Add(UnitLabel(t, cf[1], cf[5]));
            foreach (var r in t.Rows)
            {
                string cname = ExtTable.Cell(r, cc);
                if (cname.Length == 0 || !Keep(cname)) continue;
                if (!ExtTable.TryNum(ExtTable.Cell(r, csta), out double s)) continue;
                string k = Key(ExtTable.Cell(r, cst), FrameLabelOf(t, r), ExtTable.Cell(r, cu));
                if (!_ext.TryGetValue(k, out var ef) || ef.Contribs.Count == 0) continue;
                var f = new double[6];
                for (int c = 0; c < 6; c++) if (cf[c] >= 0 && ExtTable.TryNum(ExtTable.Cell(r, cf[c]), out double v)) f[c] = v * fac[c];
                string key = CaseKey(cname, ExtTable.Cell(r, cs));
                if (!extData.TryGetValue(k, out var byCase)) extData[k] = byCase = new Dictionary<string, List<(double, double[])>>();
                if (!byCase.TryGetValue(key, out var lst)) byCase[key] = lst = new List<(double, double[])>();
                lst.Add((t.StationRelative ? -1 - s : s * fs, f));    // trạm tương đối mã hoá âm: −1 − s
            }
        }
        // gom theo thanh dự án
        var frameRows = new Dictionary<(string cs, int fid), List<(double x, double[] f)>>();
        var byId = p.Frames.ToDictionary(f => f.Id);
        foreach (var (k, byCase) in extData)
        {
            var ef = _ext[k];
            foreach (var (cname, rows0) in byCase)
            {
                var ff = ToForces(rows0, ef, byId);
                foreach (var c in ef.Contribs)
                {
                    var f = byId[c.FrameId];
                    double L = p.Length(f);
                    var axesOur = AxesOf(f, false); var axesRev = AxesOf(f, true);
                    if (!frameRows.TryGetValue((cname, f.Id), out var dst)) frameRows[(cname, f.Id)] = dst = new List<(double, double[])>();
                    double extLen = ff.Count > 0 ? ff.Stations[^1] : 0;
                    void Put(double s, double[] fe)
                    {
                        double x = c.Offset + c.Dir * s * c.Scale;
                        if (x < -1e-3 || x > L + 1e-3) return;
                        x = Math.Clamp(x, 0, L);
                        dst.Add((x, c.Reversed && !c.Curved ? Reverse(fe, axesOur, axesRev) : (double[])fe.Clone()));
                    }
                    for (int i = 0; i < ff.Count; i++) Put(ff.Stations[i], ff.F[i]);
                    if (!double.IsNaN(c.SAtI) && ff.Count > 0) Put(c.SAtI, ff.At(c.SAtI));
                    if (!double.IsNaN(c.SAtJ) && ff.Count > 0) Put(c.SAtJ, ff.At(c.SAtJ));
                }
            }
        }
        foreach (var ((cname, fid), rows) in frameRows)
        {
            var ff = Merge(rows);
            if (ff.Count == 0) continue;
            CaseOf(cname).Frames[fid] = ff;
        }
        var matchedIds = new HashSet<int>(frameRows.Keys.Select(x => x.fid));
        if (_ownBase) foreach (var f in p.Frames) matchedIds.Add(f.Id);
        info.MatchedFrames = matchedIds.Count;
        int extUnmatched = Matches.Count(m => !m.Matched);
        if (extUnmatched > 0)
            Note(info, $"{extUnmatched} thanh trong file nhập không khớp với dự án (xem danh sách khớp thanh) – nội lực của chúng bị bỏ qua.");

        // ---------------- phản lực, chuyển vị ----------------
        ReadJointTable(res, info, ExtTableKind.JointReactions, Keep, CaseOf);
        ReadJointTable(res, info, ExtTableKind.JointDisplacements, Keep, CaseOf);
        foreach (var t in Of(ExtTableKind.BaseReactions)) BaseReactionCheck(t, res, info, Keep);
        foreach (var t in Of(ExtTableKind.StoryDrifts)) ReadDrifts(t, info, Keep);

        // ---------------- SAFE ----------------
        foreach (var t in Of(ExtTableKind.StripForces)) ReadStrips(t, res, info, Keep);
        foreach (var t in Of(ExtTableKind.SlabForces)) ReadSlabForces(t, res, info, Keep);
        foreach (var t in Of(ExtTableKind.SoilPressures)) ReadSoil(t, info, Keep);
        foreach (var t in Of(ExtTableKind.PunchingShear)) ReadPunching(t, info);
        info.SlabPanels = res.SlabMoments.Count;
        foreach (var t in Of(ExtTableKind.Unknown))
            Note(info, $"Bảng \"{t.Name}\" ({Path.GetFileName(t.File)}, {t.Rows.Count} dòng) chưa nhận diện – dùng \"Ánh xạ cột (phần mềm khác)\" nếu là bảng nội lực.");

        // ---------------- bù thanh không có số liệu ----------------
        var unmatchedFrames = p.Frames.Where(f => !matchedIds.Contains(f.Id)).ToList();
        info.UnmatchedFrames = unmatchedFrames.Count;
        if (unmatchedFrames.Count > 0 && Options.FillUnmatchedFromOwn && Own != null && Own.External == null)
        {
            int filled = 0;
            foreach (var f in unmatchedFrames)
            {
                bool any = false;
                foreach (var cname in res.Cases.Keys.ToList())
                {
                    CaseResult own;
                    try { own = Own.Get(cname); } catch (Exception) { continue; }
                    if (own.Frames.TryGetValue(f.Id, out var off)) { res.Cases[cname].Frames[f.Id] = off.CloneScaled(1); any = true; }
                }
                if (any) filled++;
            }
            info.FilledFromOwn = filled;
            if (filled > 0) Note(info, $"{filled} thanh không có trong file nhập: lấy nội lực của chương trình (cùng tên trường hợp/tổ hợp).");
        }
        if (unmatchedFrames.Count - info.FilledFromOwn > 0)
            Note(info, $"{unmatchedFrames.Count - info.FilledFromOwn} thanh của dự án KHÔNG có nội lực (vd. {string.Join(", ", unmatchedFrames.Take(8).Select(f => f.Label + "#" + f.Id))}) – sẽ không được thiết kế theo nội lực.");

        // ---------------- đồng bộ trạm đo giữa các trường hợp ----------------
        UnifyStations(res);

        // ---------------- tổ hợp ----------------
        BuildCombos(res, info, use, sw);
        info.SourceUnits = string.Join("; ", units.Distinct().Take(3));
        if (info.SourceUnits.Length > 0) Note(info, "Đơn vị trong file: " + info.SourceUnits + " → đã đổi về kN, m.");
        foreach (var m in Messages) if (!info.Notes.Contains(m)) info.Notes.Insert(0, m);
        Note(info, "Mô hình phân tích, tải trọng và tổ hợp lấy theo file của " + sw + " – người lập mô hình chịu trách nhiệm về sơ đồ tính.");
        res.Warnings.AddRange(info.Notes.Select(n => "[Nhập " + sw + "] " + n));
        return res;
    }

    private static void Note(ExternalResultInfo info, string s) { if (!info.Notes.Contains(s)) info.Notes.Add(s); }

    private static string UnitLabel(ExtTable t, int cForce, int cMoment)
    {
        string a = t.UnitOf(cForce), b = t.UnitOf(cMoment);
        if (a.Length > 0 || b.Length > 0) return (a.Length > 0 ? a : "?") + ", " + (b.Length > 0 ? b : "?");
        string f = string.IsNullOrEmpty(t.FileForceUnit) ? "kN" : t.FileForceUnit, l = string.IsNullOrEmpty(t.FileLengthUnit) ? "m" : t.FileLengthUnit;
        return f + ", " + l;
    }

    /// <summary>Dòng (trạm, nội lực) của một thanh ngoài → FrameForces tăng dần theo trạm (trạm trùng: lệch 10⁻⁵ m giữ bước nhảy).</summary>
    private FrameForces ToForces(List<(double s, double[] f)> rows, ExtFrame ef, Dictionary<int, FrameElement> byId)
    {
        // trạm tương đối (bảng "phần mềm khác"): đổi theo chiều dài thanh dự án khớp đầu tiên
        if (rows.Any(r => r.s <= -1 + 1e-12) && ef.Contribs.Count > 0)
        {
            double L = Project.Length(byId[ef.Contribs[0].FrameId]);
            rows = rows.Select(r => (r.s <= -1 + 1e-12 ? (-1 - r.s) * L : r.s, r.f)).ToList();
        }
        var sorted = rows.Select((r, i) => (r.s, r.f, i)).OrderBy(r => r.s).ThenBy(r => r.i).ToList();
        var st = new List<double>(); var F = new List<double[]>();
        foreach (var (s, f, _) in sorted)
        {
            double x = s;
            if (st.Count > 0 && x <= st[^1] + 1e-7) x = st[^1] + 1e-5;
            st.Add(x); F.Add(f);
        }
        return new FrameForces { Stations = st.ToArray(), F = F.ToArray() };
    }

    private static FrameForces Merge(List<(double x, double[] f)> rows)
    {
        var sorted = rows.Select((r, i) => (r.x, r.f, i)).OrderBy(r => r.x).ThenBy(r => r.i).ToList();
        var st = new List<double>(); var F = new List<double[]>();
        foreach (var (x0, f, _) in sorted)
        {
            double x = x0;
            if (st.Count > 0 && x <= st[^1] + 1e-7)
            {
                // trùng trạm: cùng giá trị (điểm nối 2 đoạn) → bỏ; khác (bước nhảy lực cắt) → lệch nhẹ
                if (F[^1].Zip(f, (a, b) => Math.Abs(a - b)).Max() < 1e-6 * (1 + f.Max(v => Math.Abs(v)))) continue;
                x = st[^1] + 1e-5;
            }
            st.Add(x); F.Add(f);
        }
        return new FrameForces { Stations = st.ToArray(), F = F.ToArray() };
    }

    private double[,] AxesOf(FrameElement f, bool reversed)
    {
        var a = Project.Node(f.NodeI).P; var b = Project.Node(f.NodeJ).P;
        return reversed ? ElementMatrices.LocalAxes(b, a, f.Angle) : ElementMatrices.LocalAxes(a, b, f.Angle);
    }

    /// <summary>Nội lực trên mặt dương của thanh ngược chiều → mặt dương của thanh dự án: f = −R·Rngᵀ·f_ng (lực và mô men riêng).</summary>
    public static double[] Reverse(double[] fe, double[,] rOur, double[,] rExt)
    {
        var r = new double[6];
        for (int blk = 0; blk < 2; blk++)
        {
            int o = 3 * blk;
            var g = new double[3];
            for (int j = 0; j < 3; j++) g[j] = rExt[0, j] * fe[o] + rExt[1, j] * fe[o + 1] + rExt[2, j] * fe[o + 2];
            for (int i = 0; i < 3; i++) r[o + i] = -(rOur[i, 0] * g[0] + rOur[i, 1] * g[1] + rOur[i, 2] * g[2]);
        }
        return r;
    }

    private static void UnifyStations(AnalysisResults res)
    {
        var ids = res.Cases.Values.SelectMany(c => c.Frames.Keys).Distinct().ToList();
        foreach (int id in ids)
        {
            var all = new SortedSet<double>();
            foreach (var c in res.Cases.Values)
                if (c.Frames.TryGetValue(id, out var ff)) foreach (var s in ff.Stations) all.Add(Math.Round(s, 6));
            var st = all.ToArray();
            foreach (var c in res.Cases.Values)
            {
                if (!c.Frames.TryGetValue(id, out var ff)) continue;
                bool same = ff.Count == st.Length && ff.Stations.Select((s, i) => Math.Abs(Math.Round(s, 6) - st[i]) < 1e-9).All(b => b);
                if (!same) c.Frames[id] = AnalysisResults.Resample(ff, st);
            }
        }
    }

    // ------------------------------------------------------------------ nút
    private Dictionary<(double, double), int> _exportPoints;

    /// <summary>Số hiệu điểm mặt bằng như E2kExporter (thứ tự xuất hiện của toạ độ (x, y) trong danh sách nút).</summary>
    private int ExporterPoint(Node n)
    {
        if (_exportPoints == null)
        {
            _exportPoints = new Dictionary<(double, double), int>();
            foreach (var q in Project.Nodes)
            {
                var k = (Math.Round(q.X * 1000), Math.Round(q.Y * 1000));
                if (!_exportPoints.ContainsKey(k)) _exportPoints[k] = _exportPoints.Count + 1;
            }
        }
        return _exportPoints[(Math.Round(n.X * 1000), Math.Round(n.Y * 1000))];
    }

    private Dictionary<string, int> _nodeMap;

    private void BuildNodeMap()
    {
        _nodeMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var p = Project;
        foreach (var n in p.Nodes)
        {
            if (!string.IsNullOrEmpty(n.ExternalName))
            {
                _nodeMap["X:" + (n.Story ?? "") + "|" + n.ExternalName] = n.Id;
                _nodeMap.TryAdd("XL:" + n.ExternalName, n.Id);
            }
            _nodeMap["ID:" + n.Id.ToString(Inv)] = n.Id;
        }
    }

    private int MatchNode(ExtTable t, string[] r, int cLabel, int cStory, int cUnique, int cx, int cy, int cz, double fL, bool supportsOnly)
    {
        var p = Project;
        string lab = ExtTable.Cell(r, cLabel), st = ExtTable.Cell(r, cStory), un = ExtTable.Cell(r, cUnique);
        // (a) tên gốc
        if (lab.Length > 0 && _nodeMap.TryGetValue("X:" + st + "|" + lab, out int id)) return id;
        if (st.Length == 0 && lab.Length > 0 && _nodeMap.TryGetValue("XL:" + lab, out id)) return id;
        if (un.Length > 0 && _nodeMap.TryGetValue("XL:" + un, out id)) return id;
        // (b) toạ độ trong bảng hoặc bảng toạ độ nút
        Vec3? xyz = null;
        if (cx >= 0 && cy >= 0 && ExtTable.TryNum(ExtTable.Cell(r, cx), out double x) && ExtTable.TryNum(ExtTable.Cell(r, cy), out double y))
        {
            double z = double.NaN;
            if (cz >= 0 && ExtTable.TryNum(ExtTable.Cell(r, cz), out double zz)) z = zz * fL;
            xyz = new Vec3(x * fL + Options.OffsetX, y * fL + Options.OffsetY, z);
        }
        else xyz = JointXyz(un.Length > 0 ? un : lab, st, un.Length > 0);
        double tol = Math.Max(1e-4, Options.Tolerance);
        if (xyz != null)
        {
            var v = xyz.Value;
            Node best = null; double bd = double.MaxValue;
            foreach (var n in p.Nodes)
            {
                if (supportsOnly && !n.HasRestraint) continue;
                double d = double.IsNaN(v.Z) ? Math.Sqrt((n.X - v.X) * (n.X - v.X) + (n.Y - v.Y) * (n.Y - v.Y)) : (n.P - v).Length;
                if (d < bd) { bd = d; best = n; }
            }
            if (best != null && bd <= tol) return best.Id;
            if (_joints.Count > 0 || cx >= 0) return 0;
        }
        // (c) tên do bộ xuất đặt: SAP2000 Joint = Id nút; ETABS điểm mặt bằng + tầng
        if (st.Length == 0 && int.TryParse(lab, NumberStyles.Integer, Inv, out int jid) && _nodeMap.TryGetValue("ID:" + jid.ToString(Inv), out id)) return id;
        if (st.Length > 0 && int.TryParse(lab, NumberStyles.Integer, Inv, out int pt) && TryStoryElevation(st, out double ze))
            foreach (var n in p.Nodes)
                if (Math.Abs(n.Z - ze) < 2e-3 && ExporterPoint(n) == pt) return n.Id;
        return 0;
    }

    private void ReadJointTable(AnalysisResults res, ExternalResultInfo info, ExtTableKind kind, Func<string, bool> keep, Func<string, CaseResult> caseOf)
    {
        if (_nodeMap == null) BuildNodeMap();
        foreach (var t in Of(kind))
        {
            int cc = CaseCol(t), cs = StepCol(t);
            int cl = t.Col("Joint Label", "Joint", "Label", "Point Label", "Point", "Joint Object", "Point Object"), cst = t.Col("Story"), cu = t.Col("Unique Name", "UniqueName");
            int cx = t.Col("GlobalX", "X"), cy = t.Col("GlobalY", "Y"), cz = t.Col("GlobalZ", "Z");
            double fL = cx >= 0 ? t.Factor(cx, Qty.Length) : 1;
            bool reac = kind == ExtTableKind.JointReactions;
            int[] col = reac
                ? new[] { t.Col("FX", "F1"), t.Col("FY", "F2"), t.Col("FZ", "F3"), t.Col("MX", "M1"), t.Col("MY", "M2"), t.Col("MZ", "M3") }
                : new[] { t.Col("UX", "U1"), t.Col("UY", "U2"), t.Col("UZ", "U3"), t.Col("RX", "R1"), t.Col("RY", "R2"), t.Col("RZ", "R3") };
            var fac = new double[6];
            for (int k = 0; k < 6; k++)
                fac[k] = col[k] < 0 ? 0 : t.Factor(col[k], reac ? (k < 3 ? Qty.Force : Qty.Moment) : (k < 3 ? Qty.Length : Qty.Rotation));
            int ok = 0, bad = 0;
            var nodes = new HashSet<int>();
            foreach (var r in t.Rows)
            {
                string cname = ExtTable.Cell(r, cc);
                if (cname.Length == 0 || !keep(cname)) continue;
                int nid = MatchNode(t, r, cl, cst, cu, cx, cy, cz, fL, reac);
                if (nid == 0) { bad++; continue; }
                var v = new double[6];
                for (int k = 0; k < 6; k++) if (col[k] >= 0 && ExtTable.TryNum(ExtTable.Cell(r, col[k]), out double x)) v[k] = x * fac[k];
                var cr = caseOf(CaseKey(cname, ExtTable.Cell(r, cs)));
                var dict = reac ? cr.Reactions : cr.Disp;
                if (reac && dict.TryGetValue(nid, out var prev))
                    for (int k = 0; k < 6; k++) prev[k] += v[k];   // nhiều lò xo/điểm SAFE về cùng một nút
                else dict[nid] = v;
                ok++; nodes.Add(nid);
            }
            info.Tables.Add(new ExternalTableUse { Table = t.Name, File = t.File, Rows = t.Rows.Count, Units = t.UnitsText() });
            info.MatchedNodes = Math.Max(info.MatchedNodes, nodes.Count);
            if (bad > 0) Note(info, $"Bảng \"{t.Name}\": {bad} dòng không khớp được nút của dự án (đã bỏ qua).");
            if (reac && ok > 0 && Project.Nodes.Any(n => n.IsSupport && !nodes.Contains(n.Id)))
                Note(info, $"Bảng \"{t.Name}\": {Project.Nodes.Count(n => n.IsSupport && !nodes.Contains(n.Id))} gối của dự án không có phản lực nhập về.");
        }
    }

    private void BaseReactionCheck(ExtTable t, AnalysisResults res, ExternalResultInfo info, Func<string, bool> keep)
    {
        int cc = CaseCol(t), cs = StepCol(t), cz = t.Col("FZ", "GlobalFZ", "F3");
        if (cc < 0 || cz < 0) return;
        double f = t.Factor(cz, Qty.Force);
        int n = 0;
        foreach (var r in t.Rows)
        {
            string cname = ExtTable.Cell(r, cc);
            if (!keep(cname) || !ExtTable.TryNum(ExtTable.Cell(r, cz), out double fz)) continue;
            string key = CaseKey(cname, ExtTable.Cell(r, cs));
            if (!res.Cases.TryGetValue(key, out var cr) || cr.Reactions.Count == 0) continue;
            double sum = cr.Reactions.Values.Sum(v => v[2]);
            double d = Math.Abs(sum - fz * f);
            if (d > 0.01 * Math.Max(1, Math.Abs(fz * f)) && n++ < 5)
                Note(info, $"Tổng phản lực đứng {key}: các nút nhập {sum:0.0} kN ≠ Base Reactions {fz * f:0.0} kN – thiếu nút gối?");
        }
        info.Tables.Add(new ExternalTableUse { Table = t.Name, File = t.File, Rows = t.Rows.Count, Units = t.UnitsText() });
    }

    private void ReadDrifts(ExtTable t, ExternalResultInfo info, Func<string, bool> keep)
    {
        int cc = CaseCol(t), cs = t.Col("Story"), cd = t.Col("Direction", "Dir"), cv = t.Col("Drift", "DriftX", "Drift Ratio");
        if (cc < 0 || cs < 0 || cv < 0) return;
        var best = new Dictionary<(string, string, string), double>();
        foreach (var r in t.Rows)
        {
            string cname = ExtTable.Cell(r, cc);
            if (!keep(cname) || !ExtTable.TryNum(ExtTable.Cell(r, cv), out double d)) continue;
            var k = (ExtTable.Cell(r, cs), cname, ExtTable.Cell(r, cd));
            best[k] = best.TryGetValue(k, out double o) ? Math.Max(o, Math.Abs(d)) : Math.Abs(d);
        }
        foreach (var kv in best) info.Drifts.Add(new ExternalDrift { Story = kv.Key.Item1, Case = kv.Key.Item2, Direction = kv.Key.Item3, Drift = kv.Value });
        info.Tables.Add(new ExternalTableUse { Table = t.Name, File = t.File, Rows = t.Rows.Count, Units = "" });
    }

    // ------------------------------------------------------------------ SAFE / sàn
    /// <summary>Ô sàn chứa điểm (x, y) (kể cả nằm trên cạnh, sai số tol) trong tập ô cho trước.</summary>
    private List<Slab> SlabsAt(IEnumerable<Slab> slabs, double x, double y, double tol)
    {
        var res = new List<Slab>();
        foreach (var s in slabs)
        {
            var pts = s.Nodes.Select(id => Project.Node(id)).Select(n => (n.X, n.Y)).ToList();
            if (pts.Count < 3) continue;
            if (Inside(pts, x, y) || OnEdge(pts, x, y, tol)) res.Add(s);
        }
        return res;
    }

    private static bool Inside(List<(double X, double Y)> pts, double x, double y)
    {
        bool c = false;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            if ((pts[i].Y > y) != (pts[j].Y > y) && x < (pts[j].X - pts[i].X) * (y - pts[i].Y) / (pts[j].Y - pts[i].Y) + pts[i].X) c = !c;
        return c;
    }

    private static bool OnEdge(List<(double X, double Y)> pts, double x, double y, double tol)
    {
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % pts.Count];
            var (t, d) = ImportGeometry.ProjectPoint(new Seg2(a.X, a.Y, b.X, b.Y), x, y);
            if (d <= tol && t >= -1e-6 && t <= 1 + 1e-6) return true;
        }
        return false;
    }

    private double SlabZ(Slab s) => s.Nodes.Count == 0 ? 0 : Project.Node(s.Nodes[0]).Z;

    /// <summary>Chọn các ô sàn ứng với dòng: theo cao độ/tầng nếu bảng có, ngược lại theo tầng áp dụng SAFE.</summary>
    private IEnumerable<Slab> CandidateSlabs(string story, double z, Dictionary<string, List<Slab>> autoStory)
    {
        var p = Project;
        if (!double.IsNaN(z)) return p.Slabs.Where(s => Math.Abs(SlabZ(s) - z) < 0.05);
        if (!string.IsNullOrEmpty(story) && TryStoryElevation(story, out double ze)) return p.Slabs.Where(s => Math.Abs(SlabZ(s) - ze) < 0.05);
        if (!string.IsNullOrEmpty(Options.SafeStory))
        {
            var st = p.Stories.FirstOrDefault(s => s.Name == Options.SafeStory);
            if (st != null) return p.Slabs.Where(s => Math.Abs(SlabZ(s) - st.Elevation) < 0.05);
        }
        return autoStory.TryGetValue("", out var l) ? l : p.Slabs;
    }

    /// <summary>Tự chọn tầng (cao độ sàn) chứa nhiều điểm nhất.</summary>
    private Dictionary<string, List<Slab>> AutoStory(IEnumerable<(double x, double y)> pts)
    {
        var d = new Dictionary<string, List<Slab>>();
        var groups = Project.Slabs.GroupBy(s => Math.Round(SlabZ(s), 3)).ToList();
        if (groups.Count == 0) return d;
        var sample = pts.Take(400).ToList();
        var best = groups.OrderByDescending(g => sample.Count(q => SlabsAt(g, q.x, q.y, 0.01).Count > 0)).ThenByDescending(g => g.Key).First();
        d[""] = best.ToList();
        return d;
    }

    private void AddSlabMoment(AnalysisResults res, Slab s, string cs, bool alongX, double m, string src)
    {
        if (!res.SlabMoments.TryGetValue(s.Id, out var byCase)) res.SlabMoments[s.Id] = byCase = new Dictionary<string, SlabMomentSet>();
        if (!byCase.TryGetValue(cs, out var set)) byCase[cs] = set = new SlabMomentSet { Source = src };
        set.Add(alongX, m);
    }

    private void ReadStrips(ExtTable t, AnalysisResults res, ExternalResultInfo info, Func<string, bool> keep)
    {
        int cc = CaseCol(t), cs = StepCol(t), cstrip = t.Col("Strip", "Strip Name", "Label"), csta = t.Col("Station");
        int cm = t.Col("M3", "Moment", "M2", "M"), cx = t.Col("GlobalX", "X"), cy = t.Col("GlobalY", "Y"), cz = t.Col("GlobalZ", "Z"), cst = t.Col("Story");
        int cwl = t.Col("WidthLeft", "Width Left"), cwr = t.Col("WidthRight", "Width Right"), cw = t.Col("StripWidth", "Width", "WidthTotal", "Strip Width");
        info.Tables.Add(new ExternalTableUse { Table = t.Name, File = t.File, Rows = t.Rows.Count, Units = t.UnitsText() });
        if (cc < 0 || cm < 0) { Note(info, $"Bảng \"{t.Name}\": thiếu cột tổ hợp hoặc mô men – bỏ qua."); return; }
        if (cx < 0 || cy < 0) { Note(info, $"Bảng \"{t.Name}\": không có toạ độ GlobalX/GlobalY của trạm dải – không gán được vào ô sàn. Hãy xuất bảng Strip Forces kèm toạ độ."); return; }
        string mu = t.UnitOf(cm);
        bool perWidth = mu.Contains('/');
        double fm = t.Factor(cm, perWidth ? Qty.MomentPerLength : Qty.Moment);
        double fL = t.Factor(cx, Qty.Length);
        double fw = cw >= 0 ? t.Factor(cw, Qty.Length) : cwl >= 0 ? t.Factor(cwl, Qty.Length) : 1;
        // phương của từng dải theo toạ độ trạm đầu – cuối
        var rows = t.Rows.Where(r => ExtTable.TryNum(ExtTable.Cell(r, cx), out _) && ExtTable.TryNum(ExtTable.Cell(r, cy), out _)).ToList();
        var dirX = new Dictionary<string, bool>();
        foreach (var g in rows.GroupBy(r => ExtTable.Cell(r, cstrip)))
        {
            var pts = g.Select(r => (x: Num(r, cx), y: Num(r, cy))).ToList();
            double dx = pts.Max(q => q.x) - pts.Min(q => q.x), dy = pts.Max(q => q.y) - pts.Min(q => q.y);
            dirX[g.Key] = dx >= dy;
        }
        var auto = cz < 0 && cst < 0 ? AutoStory(rows.Select(r => (Num(r, cx) * fL + Options.OffsetX, Num(r, cy) * fL + Options.OffsetY))) : new Dictionary<string, List<Slab>>();
        int used = 0, outside = 0, noWidth = 0;
        foreach (var r in rows)
        {
            string cname = ExtTable.Cell(r, cc);
            if (cname.Length == 0 || !keep(cname) || !ExtTable.TryNum(ExtTable.Cell(r, cm), out double m)) continue;
            double w = Options.DefaultStripWidth;
            if (cw >= 0 && ExtTable.TryNum(ExtTable.Cell(r, cw), out double ww) && ww > 0) w = ww * fw;
            else if (cwl >= 0 && cwr >= 0 && ExtTable.TryNum(ExtTable.Cell(r, cwl), out double wl) && ExtTable.TryNum(ExtTable.Cell(r, cwr), out double wr) && wl + wr > 0) w = (wl + wr) * fw;
            else noWidth++;
            double mpm = perWidth ? m * fm : m * fm / Math.Max(w, 1e-6);
            double x = Num(r, cx) * fL + Options.OffsetX, y = Num(r, cy) * fL + Options.OffsetY;
            double z = cz >= 0 && ExtTable.TryNum(ExtTable.Cell(r, cz), out double zz) ? zz * fL : double.NaN;
            var slabs = SlabsAt(CandidateSlabs(ExtTable.Cell(r, cst), z, auto), x, y, Math.Max(0.05, Options.Tolerance));
            if (slabs.Count == 0) { outside++; continue; }
            string key = CaseKey(cname, ExtTable.Cell(r, cs));
            bool ax = dirX.TryGetValue(ExtTable.Cell(r, cstrip), out bool b) && b;
            foreach (var s in slabs) AddSlabMoment(res, s, key, ax, mpm, "SAFE (dải " + ExtTable.Cell(r, cstrip) + ")");
            used++;
        }
        Note(info, $"Bảng \"{t.Name}\": {used} trạm dải gán vào ô sàn, {outside} trạm ngoài các ô sàn của dự án.");
        if (noWidth > 0 && !perWidth) Note(info, $"Bảng \"{t.Name}\": {noWidth} dòng không có bề rộng dải – lấy {Options.DefaultStripWidth:0.##} m.");
    }

    private static double Num(string[] r, int c) => ExtTable.TryNum(ExtTable.Cell(r, c), out double v) ? v : 0;

    private void ReadSlabForces(ExtTable t, AnalysisResults res, ExternalResultInfo info, Func<string, bool> keep)
    {
        int cc = CaseCol(t), cs = StepCol(t), cst = t.Col("Story");
        int c11 = t.Col("M11", "MXX", "Mx"), c22 = t.Col("M22", "MYY", "My"), c12 = t.Col("M12", "MXY", "Mxy");
        int cx = t.Col("GlobalX", "X"), cy = t.Col("GlobalY", "Y"), cz = t.Col("GlobalZ", "Z");
        int cj = t.Col("Element Joint", "Joint", "Point", "Node", "ElemJoint");
        info.Tables.Add(new ExternalTableUse { Table = t.Name, File = t.File, Rows = t.Rows.Count, Units = t.UnitsText() });
        double f11 = c11 >= 0 ? t.Factor(c11, Qty.MomentPerLength) : 0, f22 = c22 >= 0 ? t.Factor(c22, Qty.MomentPerLength) : 0, f12 = c12 >= 0 ? t.Factor(c12, Qty.MomentPerLength) : 0;
        double fL = cx >= 0 ? t.Factor(cx, Qty.Length) : 1;
        var pts = new List<(string[] r, double x, double y, double z)>();
        foreach (var r in t.Rows)
        {
            if (cx >= 0 && cy >= 0 && ExtTable.TryNum(ExtTable.Cell(r, cx), out double x) && ExtTable.TryNum(ExtTable.Cell(r, cy), out double y))
            {
                double z = cz >= 0 && ExtTable.TryNum(ExtTable.Cell(r, cz), out double zz) ? zz * fL : double.NaN;
                pts.Add((r, x * fL + Options.OffsetX, y * fL + Options.OffsetY, z));
            }
            else if (cj >= 0 && JointXyz(ExtTable.Cell(r, cj), ExtTable.Cell(r, cst), false) is Vec3 v)
                pts.Add((r, v.X + Options.OffsetX, v.Y + Options.OffsetY, v.Z));
        }
        if (pts.Count == 0) { Note(info, $"Bảng \"{t.Name}\": không xác định được toạ độ điểm (cần cột GlobalX/GlobalY hoặc bảng toạ độ nút) – bỏ qua."); return; }
        var auto = pts.All(q => double.IsNaN(q.z)) && cst < 0 ? AutoStory(pts.Select(q => (q.x, q.y))) : new Dictionary<string, List<Slab>>();
        int used = 0;
        foreach (var (r, x, y, z) in pts)
        {
            string cname = ExtTable.Cell(r, cc);
            if (cname.Length == 0 || !keep(cname)) continue;
            var slabs = SlabsAt(CandidateSlabs(ExtTable.Cell(r, cst), z, auto), x, y, Math.Max(0.05, Options.Tolerance));
            if (slabs.Count == 0) continue;
            double mx = c11 >= 0 ? Num(r, c11) * f11 : double.NaN, my = c22 >= 0 ? Num(r, c22) * f22 : double.NaN, mxy = c12 >= 0 ? Math.Abs(Num(r, c12) * f12) : 0;
            string key = CaseKey(cname, ExtTable.Cell(r, cs));
            foreach (var s in slabs)
            {
                // trục địa phương phần tử sàn mặc định: 1 ∥ X, 2 ∥ Y (ETABS/SAFE/SAP) – M11 cần thép theo X
                if (Options.WoodArmer && c12 >= 0)
                {
                    if (!double.IsNaN(mx)) { AddSlabMoment(res, s, key, true, mx + mxy, "phần tử sàn"); AddSlabMoment(res, s, key, true, mx - mxy, "phần tử sàn"); }
                    if (!double.IsNaN(my)) { AddSlabMoment(res, s, key, false, my + mxy, "phần tử sàn"); AddSlabMoment(res, s, key, false, my - mxy, "phần tử sàn"); }
                }
                else
                {
                    if (!double.IsNaN(mx)) AddSlabMoment(res, s, key, true, mx, "phần tử sàn");
                    if (!double.IsNaN(my)) AddSlabMoment(res, s, key, false, my, "phần tử sàn");
                }
            }
            used++;
        }
        Note(info, $"Bảng \"{t.Name}\": {used} điểm mô men sàn gán vào ô sàn" + (Options.WoodArmer && c12 >= 0 ? " (kể M12 theo Wood–Armer: M ± |M12|)." : "."));
    }

    private void ReadSoil(ExtTable t, ExternalResultInfo info, Func<string, bool> keep)
    {
        int cc = CaseCol(t), cs = StepCol(t), cp = t.Col("Soil Pressure", "SoilPressure", "Pressure", "Press");
        int cpt = t.Col("Point", "Point Label", "Joint", "Label"), cx = t.Col("GlobalX", "X"), cy = t.Col("GlobalY", "Y");
        info.Tables.Add(new ExternalTableUse { Table = t.Name, File = t.File, Rows = t.Rows.Count, Units = t.UnitsText() });
        if (cc < 0 || cp < 0) return;
        double f = t.Factor(cp, Qty.Pressure), fL = cx >= 0 ? t.Factor(cx, Qty.Length) : 1;
        var best = new Dictionary<string, ExternalSoilPressure>();
        foreach (var r in t.Rows)
        {
            string cname = ExtTable.Cell(r, cc);
            if (!keep(cname) || !ExtTable.TryNum(ExtTable.Cell(r, cp), out double pr)) continue;
            string key = CaseKey(cname, ExtTable.Cell(r, cs));
            double a = Math.Abs(pr * f);
            if (!best.TryGetValue(key, out var b) || a > b.MaxPressure)
                best[key] = new ExternalSoilPressure { Case = key, MaxPressure = a, Point = ExtTable.Cell(r, cpt), X = Num(r, cx) * fL, Y = Num(r, cy) * fL };
        }
        info.SoilPressures.AddRange(best.Values);
        if (best.Count > 0)
        {
            var mx = best.Values.OrderByDescending(v => v.MaxPressure).First();
            Note(info, $"Áp lực đất nền lớn nhất (SAFE): {mx.MaxPressure:0.0} kN/m² ({mx.Case}, điểm {mx.Point}) – so sánh với sức chịu tải nền R khi thiết kế móng bè.");
        }
    }

    private void ReadPunching(ExtTable t, ExternalResultInfo info)
    {
        int cr = t.Col("Ratio", "Stress Ratio", "StressRatio", "Shear Ratio", "DCRatio", "Ratio(D/C)", "PunchRatio", "D/C Ratio");
        int cpt = t.Col("Point", "Point Label", "Column", "Label", "Joint"), cx = t.Col("GlobalX", "X"), cy = t.Col("GlobalY", "Y");
        int cc = t.Col("Combo", "Design Combo", "OutputCase", "Output Case", "Load Combo"), cst = t.Col("Status", "Note");
        info.Tables.Add(new ExternalTableUse { Table = t.Name, File = t.File, Rows = t.Rows.Count, Units = "" });
        if (cr < 0) { Note(info, $"Bảng \"{t.Name}\": không có cột tỷ số chọc thủng (Ratio)."); return; }
        double fL = cx >= 0 ? t.Factor(cx, Qty.Length) : 1;
        foreach (var r in t.Rows)
        {
            if (!ExtTable.TryNum(ExtTable.Cell(r, cr), out double ratio)) continue;
            info.Punching.Add(new ExternalPunching
            {
                Point = ExtTable.Cell(r, cpt), X = Num(r, cx) * fL, Y = Num(r, cy) * fL, Case = ExtTable.Cell(r, cc), Ratio = ratio, Status = ExtTable.Cell(r, cst)
            });
        }
        var badP = info.Punching.Where(x => x.Ratio > 1 + 1e-9).ToList();
        Note(info, $"Chọc thủng (SAFE): {info.Punching.Count} điểm, tỷ số lớn nhất {(info.Punching.Count > 0 ? info.Punching.Max(x => x.Ratio) : 0):0.00}"
                   + (badP.Count > 0 ? $" – {badP.Count} điểm KHÔNG ĐẠT (tỷ số > 1): {string.Join(", ", badP.Take(6).Select(x => x.Point))}" : " – đạt."));
    }

    private bool _ownBase;

    // ------------------------------------------------------------------ tổ hợp
    private void BuildCombos(AnalysisResults res, ExternalResultInfo info, Dictionary<string, CaseUse> use, string sw)
    {
        var p = Project;
        // trường hợp có số liệu: nội lực/phản lực (res.Cases) hoặc chỉ mô men sàn
        var slabCases = new HashSet<string>(res.SlabMoments.Values.SelectMany(d => d.Keys));
        bool Known(string k) => res.Cases.ContainsKey(k) || slabCases.Contains(k);
        foreach (var c in Cases)
            if (c.Use is CaseUse.ULS or CaseUse.Both)
                foreach (var k in new[] { c.Name, c.Name + " (Max)", c.Name + " (Min)" })
                    if (slabCases.Contains(k) && !info.SlabDesignCases.Contains(k)) info.SlabDesignCases.Add(k);
        if (_ownBase)
        {
            // nội lực khung của chương trình: giữ tổ hợp của dự án cho thiết kế khung
            string env0 = p.Combinations.FirstOrDefault(c => c.Type == ComboType.Envelope)?.Name ?? "";
            info.EnvelopeName = env0;
            return;
        }
        var maxMin = Cases.Where(c => c.HasMaxMin).ToList();
        foreach (var c in maxMin)
        {
            string a = c.Name + " (Max)", b = c.Name + " (Min)";
            if (!res.Cases.ContainsKey(a) && !res.Cases.ContainsKey(b)) continue;
            var env = new LoadCombination { Name = c.Name, Type = ComboType.Envelope };
            if (res.Cases.ContainsKey(a)) env.Items.Add(new ComboItem(a, 1));
            if (res.Cases.ContainsKey(b)) env.Items.Add(new ComboItem(b, 1));
            if (res.Cases.ContainsKey(c.Name)) env.Items.Add(new ComboItem(c.Name, 1));
            res.ExternalCombos.Add(env);
        }
        IEnumerable<string> Leaves(ImportedCase c)
        {
            foreach (var k in new[] { c.Name, c.Name + " (Max)", c.Name + " (Min)" })
                if (Known(k)) yield return k;
        }
        foreach (var c in Cases)
        {
            if (c.Use is CaseUse.ULS or CaseUse.Both) info.UlsCases.AddRange(Leaves(c));
            if (c.Use is CaseUse.SLS or CaseUse.Both) info.SlsCases.AddRange(Leaves(c));
        }
        info.UlsCases = info.UlsCases.Distinct().ToList();
        info.SlsCases = info.SlsCases.Distinct().ToList();
        bool Resolvable(string name, int depth = 0)
        {
            if (res.Cases.ContainsKey(name)) return true;
            if (depth > 8) return false;
            var cb = res.FindCombo(name);
            return cb != null && cb.Items.Count > 0 && cb.Items.All(i => Resolvable(i.Case, depth + 1));
        }
        if (info.UlsCases.Count == 0)
        {
            var uls = p.Combinations.Where(c => c.Type == ComboType.ULS).Select(c => c.Name).ToList();
            if (uls.Count > 0 && uls.All(n => Resolvable(n)))
                Note(info, "TTGH I: dùng " + uls.Count + " tổ hợp của dự án, tổ hợp từ các trường hợp tải nhập về.");
            else
            {
                info.UlsCases = res.Cases.Keys.Where(k => !use.TryGetValue(BaseName(k), out var u) || u != CaseUse.SLS).ToList();
                Note(info, "CHÚ Ý: không có tổ hợp TTGH I – thiết kế theo trực tiếp " + info.UlsCases.Count + " trường hợp nhập về. Nên nhập các tổ hợp tải từ " + sw + ".");
            }
        }
        if (info.SlsCases.Count == 0)
        {
            var sls = p.Combinations.Where(c => c.Type == ComboType.SLS).Select(c => c.Name).ToList();
            if (sls.Count > 0 && sls.All(n => Resolvable(n)))
                Note(info, "TTGH II: dùng " + sls.Count + " tổ hợp tiêu chuẩn của dự án.");
            else
            {
                var basis = info.UlsCases.Count > 0 ? info.UlsCases : p.Combinations.Where(c => c.Type == ComboType.ULS).Select(c => c.Name).ToList();
                info.SlsCases = basis.ToList();
                Note(info, "CHÚ Ý: không có tổ hợp TTGH II (tiêu chuẩn) – kiểm tra võng/nứt/nền theo tổ hợp TTGH I (thiên về an toàn).");
            }
        }
        // tổ hợp bao TTGH I để vẽ biểu đồ
        var ulsList = info.UlsCases.Count > 0 ? info.UlsCases : p.Combinations.Where(c => c.Type == ComboType.ULS).Select(c => c.Name).ToList();
        string swShort = sw.Split(' ')[0].Replace("+", "").Trim();
        string envName = "BAO-" + (swShort.Length > 0 ? swShort : "NGOAI");
        if (ulsList.Count > 0 && !res.Cases.ContainsKey(envName) && res.ExternalCombos.All(c => c.Name != envName))
        {
            res.ExternalCombos.Add(new LoadCombination { Name = envName, Type = ComboType.Envelope, Items = ulsList.Select(n => new ComboItem(n, 1)).ToList() });
            info.EnvelopeName = envName;
        }
    }

    private static string BaseName(string key) =>
        key.EndsWith(" (Max)") ? key.Substring(0, key.Length - 6) : key.EndsWith(" (Min)") ? key.Substring(0, key.Length - 6) : key;

    /// <summary>Tóm tắt khớp thanh để hiển thị.</summary>
    public string MatchSummary()
    {
        int ok = Matches.Count(m => m.Matched);
        var byMethod = Matches.Where(m => m.Matched).GroupBy(m => m.Method).Select(g => g.Key + ": " + g.Count());
        return $"Khớp {ok}/{Matches.Count} thanh ({string.Join(", ", byMethod)}); thanh dự án không có số liệu: {FramesWithoutData.Count}/{Project.Frames.Count}.";
    }
}
