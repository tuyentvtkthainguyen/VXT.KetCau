using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KetCauVXT.Model;

// =====================================================================
// CHUẨN LAYER CAD (dùng chung với add-in AutoCAD VXT – cùng file JSON, cùng chuỗi nhóm cấu kiện)
//   1) Nhận diện layer bản vẽ kiến trúc → nhóm cấu kiện (InputRules + InputLayerMap)
//   2) Layer bản vẽ kết cấu xuất ra (OutputLayers: tên nội bộ → tên/màu/kiểu nét/nét in)
//   3) Font chữ bản vẽ (Arial | Times New Roman) và font thuyết minh Word
// File mặc định của người dùng: %AppData%\KetCauVXT\CadStandard.json; mỗi dự án có thể lưu riêng (Project.CadStandard).
// =====================================================================

/// <summary>Nhóm cấu kiện của một layer bản vẽ kiến trúc. Chuỗi CỐ ĐỊNH (add-in AutoCAD dùng đúng các chuỗi này).</summary>
public static class CadCategory
{
    public const string Tuong = "Tuong";
    public const string Cot = "Cot";
    public const string Dam = "Dam";
    public const string San = "San";
    public const string Truc = "Truc";
    public const string Cua = "Cua";
    public const string Thang = "Thang";
    public const string TenPhong = "TenPhong";
    public const string BoQua = "BoQua";

    /// <summary>Thứ tự hiển thị.</summary>
    public static readonly string[] All = { Tuong, Cot, Dam, San, Truc, Cua, Thang, TenPhong, BoQua };

    /// <summary>Tên tiếng Việt hiển thị trên giao diện.</summary>
    public static string Label(string c) => c switch
    {
        Tuong => "Tường",
        Cot => "Cột",
        Dam => "Dầm",
        San => "Sàn / lỗ sàn",
        Truc => "Trục",
        Cua => "Cửa",
        Thang => "Cầu thang",
        TenPhong => "Tên phòng",
        BoQua => "Bỏ qua",
        _ => "(không nhận)"
    };

    /// <summary>Đổi chuỗi bất kỳ (kể cả "tuong", "Tường") về chuỗi chuẩn; "" nếu không nhận ra.</summary>
    public static string Normalize(string c)
    {
        if (string.IsNullOrWhiteSpace(c)) return "";
        string s = c.Trim();
        foreach (var a in All)
            if (string.Equals(a, s, StringComparison.OrdinalIgnoreCase) || string.Equals(Label(a), s, StringComparison.OrdinalIgnoreCase)) return a;
        string n = CadStandard.NormName(s).Replace(" ", "");
        foreach (var a in All)
            if (string.Equals(a.ToUpperInvariant(), n, StringComparison.Ordinal) || CadStandard.NormName(Label(a)).Replace(" ", "") == n) return a;
        return "";
    }
}

/// <summary>Cách so khớp tên layer.</summary>
public enum LayerMatchKind
{
    /// <summary>Đúng tên (không phân biệt hoa thường, bỏ dấu; so cả tên đã bỏ tiền tố xref "XREF|", "XREF$0$")</summary>
    Exact,
    /// <summary>Tên chứa chuỗi</summary>
    Contains,
    /// <summary>Mẫu đại diện * (nhiều ký tự) và ? (một ký tự), vd. "A-WALL*", "KT?TUONG"</summary>
    Wildcard,
    /// <summary>Biểu thức chính quy .NET (so với tên đã chuẩn hoá: chữ hoa, bỏ dấu)</summary>
    Regex
}

/// <summary>Quy tắc nhận diện: layer khớp Pattern → nhóm Category. Nhiều quy tắc cùng khớp → lấy Priority lớn nhất
/// (bằng nhau: quy tắc đứng trước).</summary>
public class LayerRule
{
    public string Pattern { get; set; } = "";
    public LayerMatchKind Match { get; set; } = LayerMatchKind.Contains;
    /// <summary>Một trong CadCategory.All</summary>
    public string Category { get; set; } = CadCategory.Tuong;
    public int Priority { get; set; }
    public bool Enabled { get; set; } = true;
    public string Note { get; set; } = "";

    public LayerRule() { }
    public LayerRule(string pattern, LayerMatchKind match, string category, int priority, string note = "")
    { Pattern = pattern; Match = match; Category = category; Priority = priority; Note = note ?? ""; }

    [JsonIgnore] private Regex _rx;
    [JsonIgnore] private string _rxKey;

    /// <summary>Layer có khớp quy tắc không (so cả tên đầy đủ và tên đã bỏ tiền tố xref).</summary>
    public bool IsMatch(string layer)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(Pattern) || layer == null) return false;
        foreach (var v in CadStandard.LayerNameVariants(layer))
            if (IsMatchOne(v)) return true;
        return false;
    }

    private bool IsMatchOne(string layer)
    {
        string L = CadStandard.NormName(layer);
        string P = Match == LayerMatchKind.Regex ? Pattern.Trim() : CadStandard.NormName(Pattern);
        if (P.Length == 0) return false;
        switch (Match)
        {
            case LayerMatchKind.Exact: return L == P;
            case LayerMatchKind.Contains: return L.Contains(P, StringComparison.Ordinal);
            case LayerMatchKind.Wildcard:
            case LayerMatchKind.Regex:
            {
                string key = Match + "\u0001" + P;
                if (_rx == null || _rxKey != key)
                {
                    string src = Match == LayerMatchKind.Wildcard ? "^" + Regex.Escape(P).Replace("\\*", ".*").Replace("\\?", ".") + "$" : P;
                    try { _rx = new Regex(src, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)); }
                    catch (ArgumentException) { _rx = null; }
                    _rxKey = key;
                }
                if (_rx == null) return false;
                try { return _rx.IsMatch(L) || (Match == LayerMatchKind.Regex && _rx.IsMatch(layer)); }
                catch (RegexMatchTimeoutException) { return false; }
            }
        }
        return false;
    }

    /// <summary>Kiểm tra mẫu hợp lệ (biểu thức chính quy); null = hợp lệ.</summary>
    public string Validate()
    {
        if (string.IsNullOrWhiteSpace(Pattern)) return "Mẫu tên layer trống";
        if (CadCategory.Normalize(Category) == "") return $"Nhóm cấu kiện '{Category}' không hợp lệ";
        if (Match == LayerMatchKind.Regex)
            try { _ = new Regex(Pattern.Trim()); }
            catch (ArgumentException ex) { return "Biểu thức chính quy lỗi: " + ex.Message; }
        return null;
    }

    public LayerRule Clone() => new(Pattern, Match, Category, Priority, Note) { Enabled = Enabled };
    public override string ToString() => $"{Match} \"{Pattern}\" → {Category} (ưu tiên {Priority})";
}

/// <summary>Gán trực tiếp một layer (đúng tên) cho một nhóm – ưu tiên cao hơn mọi quy tắc.</summary>
public class LayerMapEntry
{
    public string Layer { get; set; } = "";
    public string Category { get; set; } = "";
    public LayerMapEntry() { }
    public LayerMapEntry(string layer, string category) { Layer = layer ?? ""; Category = category ?? ""; }
}

/// <summary>Một layer bản vẽ kết cấu xuất ra. Key = tên layer nội bộ chương trình đang dùng (BE_TONG, THEP, DAI…).
/// Nhiều Key có thể cùng một Name (gộp layer).</summary>
public class OutputLayer
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Màu ACI 1…255</summary>
    public int Color { get; set; } = 7;
    /// <summary>CONTINUOUS / DASHED / CENTER / HIDDEN / DASHDOT</summary>
    public string Linetype { get; set; } = "CONTINUOUS";
    /// <summary>Bề dày nét in, mm (0 = mặc định). DXF R12 không lưu được – dùng cho add-in AutoCAD / bảng nét in CTB.</summary>
    public double Lineweight { get; set; }
    /// <summary>In layer này (DXF R12 không lưu được – dùng cho add-in AutoCAD).</summary>
    public bool Plot { get; set; } = true;
    public string Description { get; set; } = "";

    public OutputLayer() { }
    public OutputLayer(string key, string name, int color, string lt, double lw, string desc)
    { Key = key; Name = name; Color = color; Linetype = lt; Lineweight = lw; Description = desc; }
    public OutputLayer Clone() => (OutputLayer)MemberwiseClone();
}

/// <summary>Chuẩn layer CAD: nhận diện layer kiến trúc, layer bản vẽ kết cấu, font chữ.</summary>
public class CadStandard
{
    public const int CurrentSchema = 1;
    public const string PresetVxtName = "Việt Const (mặc định)";
    public const string PresetTcvnName = "TCVN/Văn phòng";
    public const string PresetUserName = "Tự định nghĩa";
    public const string FontArial = "Arial";
    public const string FontTimes = "Times New Roman";

    public int SchemaVersion { get; set; } = CurrentSchema;
    public string Name { get; set; } = PresetVxtName;

    // ---- 1. nhận diện layer kiến trúc ----
    public List<LayerRule> InputRules { get; set; } = DefaultInputRules();
    /// <summary>Gán trực tiếp theo tên layer (bảng "Layer trong file")</summary>
    public List<LayerMapEntry> InputLayerMap { get; set; } = new();
    /// <summary>Bỏ qua thực thể trên layer đang tắt (off)</summary>
    public bool SkipOffLayers { get; set; } = true;

    // ---- 2. layer bản vẽ kết cấu ----
    public string OutputPreset { get; set; } = PresetVxtName;
    public List<OutputLayer> OutputLayers { get; set; } = PresetVxt();

    // ---- 3. font ----
    /// <summary>Font chữ bản vẽ: "Arial" (mặc định) hoặc "Times New Roman"</summary>
    public string FontName { get; set; } = FontArial;
    /// <summary>Font thuyết minh Word: mặc định "Times New Roman"</summary>
    public string ReportFont { get; set; } = FontTimes;

    /// <summary>Hỏi "Xem/đổi tên layer trước khi xuất" mỗi lần xuất DXF (false = "Không hỏi lại")</summary>
    public bool AskLayersBeforeExport { get; set; } = true;

    /// <summary>Định dạng file bản vẽ khi xuất: DXF R12 (mặc định, tương thích cũ) | DXF 2018 (có HATCH) | DWG 2018 (có HATCH).
    /// File JSON cũ không có mục này → R12 (Model/CadOutput.cs, Drawing/CadFileWriters.cs).</summary>
    public CadOutputFormat OutputFormat { get; set; } = CadOutputFormat.DxfR12;

    // ---- 4. thiết lập bản vẽ: tỷ lệ, cỡ chữ, chú thích tuỳ chọn (Model/DrawingStyle.cs) ----
    /// <summary>Tỷ lệ theo loại bản vẽ, cỡ chữ kích thước/ghi chú/nhãn thép/tên hình…; file JSON cũ không có mục này → mặc định.</summary>
    public DrawingStyle Drawing { get; set; } = new();

    // ==================================================================
    // Bản mặc định / mẫu có sẵn
    // ==================================================================
    public static CadStandard Default() => new();

    /// <summary>Tên layer nội bộ chương trình đang dùng khi vẽ (KetCauVXT.Drawing.Lay) – theo thứ tự bảng LAYER.</summary>
    public static readonly string[] InternalLayerKeys =
        { "0", KetCauVXT.Drawing.Lay.BeTong, KetCauVXT.Drawing.Lay.Thep, KetCauVXT.Drawing.Lay.Dai, KetCauVXT.Drawing.Lay.KichThuoc, KetCauVXT.Drawing.Lay.Chu, KetCauVXT.Drawing.Lay.KyHieu, KetCauVXT.Drawing.Lay.Truc, KetCauVXT.Drawing.Lay.KhungTen };

    public static readonly string[] PresetNames = { PresetVxtName, PresetTcvnName };
    public static readonly string[] Linetypes = { "CONTINUOUS", "DASHED", "CENTER", "HIDDEN", "DASHDOT" };
    public static readonly string[] Fonts = { FontArial, FontTimes };

    /// <summary>Mẫu "Việt Const": giữ nguyên tên/màu/kiểu nét hiện tại (DXF xuất ra giống hệt trước đây).</summary>
    public static List<OutputLayer> PresetVxt() => new()
    {
        new("0", "0", 7, "CONTINUOUS", 0.18, "Lớp 0"),
        new(KetCauVXT.Drawing.Lay.BeTong, "BE_TONG", 7, "CONTINUOUS", 0.35, "Đường bao bê tông"),
        new(KetCauVXT.Drawing.Lay.Thep, "THEP", 1, "CONTINUOUS", 0.35, "Cốt thép dọc"),
        new(KetCauVXT.Drawing.Lay.Dai, "DAI", 3, "CONTINUOUS", 0.25, "Cốt đai"),
        new(KetCauVXT.Drawing.Lay.KichThuoc, "KICH_THUOC", 4, "CONTINUOUS", 0.13, "Kích thước"),
        new(KetCauVXT.Drawing.Lay.Chu, "CHU", 2, "CONTINUOUS", 0.18, "Chữ, ghi chú"),
        new(KetCauVXT.Drawing.Lay.KyHieu, "KY_HIEU", 6, "CONTINUOUS", 0.18, "Ký hiệu, nhãn thép, đường dẫn"),
        new(KetCauVXT.Drawing.Lay.Truc, "TRUC", 8, "CENTER", 0.13, "Trục định vị"),
        new(KetCauVXT.Drawing.Lay.KhungTen, "KHUNG_TEN", 7, "CONTINUOUS", 0.25, "Khung tên, khung bản vẽ"),
    };

    /// <summary>Mẫu "TCVN/Văn phòng": tiền tố KC-, màu theo thói quen in nét theo màu (CTB).</summary>
    public static List<OutputLayer> PresetTcvn() => new()
    {
        new("0", "0", 7, "CONTINUOUS", 0.18, "Lớp 0"),
        new(KetCauVXT.Drawing.Lay.BeTong, "KC-BETONG", 7, "CONTINUOUS", 0.35, "Đường bao bê tông"),
        new(KetCauVXT.Drawing.Lay.Thep, "KC-THEP", 1, "CONTINUOUS", 0.40, "Cốt thép dọc"),
        new(KetCauVXT.Drawing.Lay.Dai, "KC-DAI", 3, "CONTINUOUS", 0.25, "Cốt đai"),
        new(KetCauVXT.Drawing.Lay.KichThuoc, "KC-KICHTHUOC", 4, "CONTINUOUS", 0.13, "Kích thước"),
        new(KetCauVXT.Drawing.Lay.Chu, "KC-CHU", 2, "CONTINUOUS", 0.18, "Chữ, ghi chú"),
        new(KetCauVXT.Drawing.Lay.KyHieu, "KC-KYHIEU", 6, "CONTINUOUS", 0.18, "Ký hiệu, nhãn thép"),
        new(KetCauVXT.Drawing.Lay.Truc, "KC-TRUC", 1, "CENTER", 0.13, "Trục định vị"),
        new(KetCauVXT.Drawing.Lay.KhungTen, "KC-KHUNGTEN", 7, "CONTINUOUS", 0.30, "Khung tên"),
    };

    public static List<OutputLayer> Preset(string name) => name == PresetTcvnName ? PresetTcvn() : PresetVxt();

    /// <summary>Quy tắc nhận diện mặc định (tương đương từ khoá mặc định của ArchImportOptions + dầm, sàn, tên phòng, bỏ qua).</summary>
    public static List<LayerRule> DefaultInputRules()
    {
        var r = RulesFromKeywords("TUONG,WALL,A-WALL,TƯỜNG,XAY", "TRUC,GRID,AXIS,A-GRID,TRỤC", "COT,COLUMN,A-COLS,S-COLS,CỘT",
                                  "CUA,DOOR,WINDOW,A-DOOR,A-GLAZ,CỬA", "THANG,STAIR,A-FLOR-STRS");
        r.Add(new LayerRule(@"(^|[^A-Z0-9])(DAM|BEAM|S-BEAM)([^A-Z0-9]|$)", LayerMatchKind.Regex, CadCategory.Dam, 60, "Dầm: từ DAM/BEAM đứng riêng (KC-DAM, S-BEAM…)"));
        r.Add(new LayerRule(@"(^|[^A-Z0-9])(SAN|SLAB|S-SLAB)([^A-Z0-9]|$)", LayerMatchKind.Regex, CadCategory.San, 60, "Sàn: từ SAN/SLAB đứng riêng (SAN-BTCT, S-SLAB…)"));
        r.Add(new LayerRule("TEN PHONG", LayerMatchKind.Contains, CadCategory.TenPhong, 70, "Chữ tên phòng"));
        r.Add(new LayerRule("TENPHONG", LayerMatchKind.Contains, CadCategory.TenPhong, 70));
        r.Add(new LayerRule("A-AREA-IDEN", LayerMatchKind.Contains, CadCategory.TenPhong, 70));
        r.Add(new LayerRule("DEFPOINTS", LayerMatchKind.Exact, CadCategory.BoQua, 100, "Lớp điểm kích thước"));
        r.Add(new LayerRule("A-FURN*", LayerMatchKind.Wildcard, CadCategory.BoQua, 80, "Nội thất"));
        r.Add(new LayerRule("NOI THAT", LayerMatchKind.Contains, CadCategory.BoQua, 80));
        r.Add(new LayerRule("NOITHAT", LayerMatchKind.Contains, CadCategory.BoQua, 80));
        return r;
    }

    /// <summary>Đổi chuỗi từ khoá kiểu cũ (cách nhau dấu phẩy, so "chứa") thành quy tắc.
    /// Ưu tiên giữ đúng thứ tự cũ: tường &gt; trục &gt; cột &gt; cửa &gt; cầu thang.</summary>
    public static List<LayerRule> RulesFromKeywords(string wall, string grid, string column, string door, string stair)
    {
        var res = new List<LayerRule>();
        void Add(string keys, string cat, int pri)
        {
            if (string.IsNullOrWhiteSpace(keys)) return;
            foreach (var k in keys.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (NormName(k).Length > 0 && !res.Any(q => q.Category == cat && NormName(q.Pattern) == NormName(k)))
                    res.Add(new LayerRule(k, LayerMatchKind.Contains, cat, pri, "từ khoá"));
        }
        Add(wall, CadCategory.Tuong, 50);
        Add(grid, CadCategory.Truc, 40);
        Add(column, CadCategory.Cot, 30);
        Add(door, CadCategory.Cua, 20);
        Add(stair, CadCategory.Thang, 10);
        return res;
    }

    // ==================================================================
    // Nhận diện
    // ==================================================================
    /// <summary>Tên layer chuẩn hoá: bỏ dấu tiếng Việt, chữ hoa, gộp khoảng trắng.</summary>
    public static string NormName(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var f = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(f.Length);
        foreach (char c in f)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant(), @"\s+", " ").Trim();
    }

    /// <summary>Tên đầy đủ + tên đã bỏ tiền tố xref: "NHA|KT-TUONG" → "KT-TUONG"; xref đã bind "NHA$0$KT-TUONG" → "KT-TUONG".</summary>
    public static IEnumerable<string> LayerNameVariants(string layer)
    {
        if (layer == null) yield break;
        yield return layer;
        string s = layer;
        int bar = s.LastIndexOf('|');
        if (bar >= 0 && bar + 1 < s.Length) { s = s.Substring(bar + 1); yield return s; }
        var m = Regex.Match(s, @"^.+?\$\d+\$(.+)$");
        if (m.Success) yield return m.Groups[1].Value;
    }

    /// <summary>Nhóm cấu kiện của layer: gán trực tiếp (InputLayerMap) trước, rồi quy tắc ưu tiên cao nhất; "" = không nhận.
    /// reason: mô tả căn cứ (để hiện trên bảng).</summary>
    public string Classify(string layer, out string reason) => Classify(layer, InputRules, InputLayerMap, out reason);

    public static string Classify(string layer, IEnumerable<LayerRule> rules, IEnumerable<LayerMapEntry> map, out string reason)
    {
        reason = "";
        if (layer == null) return "";
        if (map != null)
        {
            foreach (var v in LayerNameVariants(layer))
            {
                var e = map.FirstOrDefault(q => q != null && string.Equals(q.Layer, v, StringComparison.OrdinalIgnoreCase));
                if (e != null) { reason = "gán trực tiếp"; return CadCategory.Normalize(e.Category); }
            }
        }
        LayerRule best = null;
        if (rules != null)
            foreach (var r in rules)
                if (r != null && r.IsMatch(layer) && (best == null || r.Priority > best.Priority)) best = r;
        if (best == null) return "";
        reason = best.ToString();
        return CadCategory.Normalize(best.Category);
    }

    /// <summary>Đặt/xoá gán trực tiếp (category rỗng = xoá).</summary>
    public void SetMapping(string layer, string category)
    {
        if (string.IsNullOrEmpty(layer)) return;
        InputLayerMap ??= new List<LayerMapEntry>();
        InputLayerMap.RemoveAll(q => q == null || string.Equals(q.Layer, layer, StringComparison.OrdinalIgnoreCase));
        string c = CadCategory.Normalize(category);
        if (c.Length > 0) InputLayerMap.Add(new LayerMapEntry(layer, c));
    }

    // ==================================================================
    // Layer xuất
    // ==================================================================
    /// <summary>Layer xuất của tên nội bộ (null nếu chuẩn không khai báo).</summary>
    public OutputLayer Output(string key) =>
        OutputLayers?.FirstOrDefault(q => q != null && string.Equals(q.Key, key, StringComparison.Ordinal));

    /// <summary>Tên layer DXF hợp lệ: bỏ ký tự cấm &lt;&gt;/\":;?*|=` và khoảng trắng đầu/cuối; rỗng → "0".</summary>
    public static string SafeLayerName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "0";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s.Trim()) sb.Append("<>/\\\":;?*|=`,".IndexOf(c) >= 0 || c < 32 ? '_' : c);
        string r = sb.ToString();
        return r.Length > 255 ? r.Substring(0, 255) : r;
    }

    public static string NormalizeLinetype(string lt)
    {
        string s = (lt ?? "").Trim().ToUpperInvariant();
        return Linetypes.Contains(s) ? s : s == "" || s == "BYLAYER" || s == "SOLID" ? "CONTINUOUS" : s == "DASHDOTX2" || s == "DASHDOT2" ? "DASHDOT" : "CONTINUOUS";
    }

    /// <summary>Tệp font TrueType cho bảng STYLE của DXF.</summary>
    public static string FontFile(string fontName) =>
        NormName(fontName ?? "").StartsWith("TIMES", StringComparison.Ordinal) ? "times.ttf" : "arial.ttf";

    /// <summary>Chỉ hỗ trợ 2 font: "Times…" → Times New Roman, còn lại → Arial.</summary>
    public static string NormalizeFont(string f) =>
        NormName(f ?? "").StartsWith("TIMES", StringComparison.Ordinal) ? FontTimes : FontArial;

    /// <summary>Chuẩn giữ nguyên tên/màu/kiểu nét của chương trình (mẫu Việt Const) – DXF ghi giống hệt khi không có chuẩn.</summary>
    [JsonIgnore]
    public bool IsBuiltInOutput
    {
        get
        {
            var d = PresetVxt();
            if (OutputLayers == null || OutputLayers.Count != d.Count) return false;
            for (int i = 0; i < d.Count; i++)
            {
                var a = OutputLayers[i]; var b = d[i];
                if (a == null || a.Key != b.Key || a.Name != b.Name || a.Color != b.Color || NormalizeLinetype(a.Linetype) != b.Linetype) return false;
            }
            return true;
        }
    }

    /// <summary>Sửa dữ liệu thiếu/sai (file JSON cũ, sửa tay): thêm layer nội bộ còn thiếu, chuẩn hoá nhóm, kiểu nét, màu, font.</summary>
    public CadStandard Normalize()
    {
        if (SchemaVersion <= 0) SchemaVersion = CurrentSchema;
        Name ??= PresetUserName;
        InputRules ??= DefaultInputRules();
        InputRules.RemoveAll(r => r == null);
        foreach (var r in InputRules) { r.Pattern ??= ""; r.Note ??= ""; string c = CadCategory.Normalize(r.Category); r.Category = c.Length > 0 ? c : CadCategory.BoQua; }
        InputLayerMap ??= new List<LayerMapEntry>();
        InputLayerMap.RemoveAll(m => m == null || string.IsNullOrWhiteSpace(m.Layer) || CadCategory.Normalize(m.Category) == "");
        foreach (var m in InputLayerMap) m.Category = CadCategory.Normalize(m.Category);
        OutputLayers ??= PresetVxt();
        OutputLayers.RemoveAll(o => o == null || string.IsNullOrWhiteSpace(o.Key));
        var def = Preset(OutputPreset);
        foreach (var d in PresetVxt())
            if (!OutputLayers.Any(o => o.Key == d.Key)) OutputLayers.Add(def.FirstOrDefault(x => x.Key == d.Key) ?? d);
        foreach (var o in OutputLayers)
        {
            o.Name = SafeLayerName(string.IsNullOrWhiteSpace(o.Name) ? o.Key : o.Name);
            if (o.Color < 1 || o.Color > 255) o.Color = 7;
            o.Linetype = NormalizeLinetype(o.Linetype);
            if (o.Lineweight < 0 || o.Lineweight > 2.11) o.Lineweight = 0;
            o.Description ??= "";
        }
        OutputPreset ??= PresetUserName;
        FontName = NormalizeFont(FontName);
        ReportFont = string.IsNullOrWhiteSpace(ReportFont) ? FontTimes : NormalizeFont(ReportFont);
        Drawing ??= new DrawingStyle();
        Drawing.Normalize();
        if (!Enum.IsDefined(typeof(CadOutputFormat), OutputFormat)) OutputFormat = CadOutputFormat.DxfR12;
        return this;
    }

    // ==================================================================
    // JSON, lưu / mở
    // ==================================================================
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static CadStandard FromJson(string json)
    {
        var s = JsonSerializer.Deserialize<CadStandard>(json ?? "", JsonOptions) ?? throw new InvalidDataException("File chuẩn layer CAD rỗng hoặc lỗi");
        return s.Normalize();
    }

    public CadStandard Clone() => FromJson(ToJson());

    public void Save(string path)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, ToJson(), new UTF8Encoding(false));
    }

    public static CadStandard Load(string path) => FromJson(File.ReadAllText(path));

    /// <summary>Thư mục lưu mặc định của người dùng (null = %AppData%\KetCauVXT). Đặt khác để kiểm thử.</summary>
    public static string UserFolderOverride { get; set; }

    public static string UserFolder => !string.IsNullOrEmpty(UserFolderOverride) ? UserFolderOverride
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KetCauVXT");

    public static string UserFilePath => Path.Combine(UserFolder, "CadStandard.json");

    /// <summary>Chuẩn mặc định của người dùng (file CadStandard.json); không có/lỗi → bản mặc định của chương trình.</summary>
    public static CadStandard LoadUserDefault()
    {
        try { if (File.Exists(UserFilePath)) return Load(UserFilePath); }
        catch (Exception) { /* file hỏng → dùng mặc định */ }
        return Default();
    }

    public static void SaveUserDefault(CadStandard s) => (s ?? Default()).Clone().Save(UserFilePath);

    /// <summary>Xoá file mặc định của người dùng → trở về mặc định của chương trình.</summary>
    public static void ResetUserDefault()
    {
        try { if (File.Exists(UserFilePath)) File.Delete(UserFilePath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Chuẩn dùng cho dự án: Project.CadStandard nếu có, ngược lại chuẩn mặc định của người dùng.</summary>
    public static CadStandard EffectiveFor(Project p) => p?.CadStandard != null ? p.CadStandard.Clone() : LoadUserDefault();

    // ==================================================================
    // Chuẩn đang áp dụng khi ghi DXF / Word / vẽ tờ in
    // ==================================================================
    /// <summary>Chuẩn đang áp dụng (null = mặc định chương trình: layer KC VXT, Arial, thuyết minh Times New Roman).</summary>
    public static CadStandard Active { get; private set; }

    /// <summary>Font chữ bản vẽ đang áp dụng.</summary>
    public static string ActiveFontName => Active?.FontName ?? FontArial;

    /// <summary>Tạm áp dụng chuẩn khác (vd. chỉ cho một lần xuất); Dispose → trả lại chuẩn trước đó.
    /// <code>using (CadStandard.Use(std)) DrawingSet.ExportAll(...);</code></summary>
    public static IDisposable Use(CadStandard s)
    {
        var prev = Active;
        Activate(s);
        return new Restore(prev);
    }

    private sealed class Restore : IDisposable
    {
        private CadStandard _prev; private bool _done;
        public Restore(CadStandard prev) { _prev = prev; }
        public void Dispose() { if (_done) return; _done = true; Activate(_prev); _prev = null; }
    }

    /// <summary>Áp dụng chuẩn cho bộ ghi DXF (bảng LAYER/LTYPE/STYLE) và bộ ghi Word (font thuyết minh).</summary>
    public static void Activate(CadStandard s)
    {
        Active = s?.Clone();
        KetCauVXT.Report.DocxWriter.DefaultFont = Active?.ReportFont ?? FontTimes;
    }
}
