using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KetCauVXT.Model;

// =====================================================================
// DANH MỤC VĂN BẢN PHÁP LÝ, QUY CHUẨN, TIÊU CHUẨN, ĐỊNH MỨC ÁP DỤNG CHO THIẾT KẾ
//
// Danh mục có sẵn trong chương trình chỉ gồm các văn bản đã được kiểm tra trên trang chính thức /
// trang pháp luật (cột "Nguồn"). Thông tin chưa xác minh được ghi rõ ở "Cần kiểm tra" (VerifyNote)
// và trạng thái "Kiểm tra". Người dùng cập nhật được: thêm/sửa/đánh dấu hết hiệu lực, nhập/xuất JSON,
// lưu làm mặc định (%AppData%\KetCauVXT\VanBanPhapLy.json – chỉ lưu phần khác với danh mục có sẵn).
// Thuyết minh, phụ lục, ghi chú bản vẽ, bảng khối lượng lấy trích dẫn qua RegulationService / Std
// (tra theo "chủ đề" – Topic) nên khi văn bản thay đổi chỉ cần cập nhật danh mục.
// =====================================================================

/// <summary>Loại văn bản.</summary>
public enum RegulationKind { Luat, NghiDinh, ThongTu, QCVN, TCVN, CongVan }

/// <summary>Trạng thái hiệu lực.</summary>
public enum RegulationStatus { HieuLuc, DuThao, HetHieuLuc, KiemTra }

/// <summary>Phạm vi áp dụng.</summary>
public enum RegulationScope { PhapLy, TaiTrong, BTCT, Thep, Mong, DongDat, DinhMuc, ChiPhi, CapCongTrinh, ThamKhao }

/// <summary>Một văn bản trong danh mục.</summary>
public sealed class RegulationDoc
{
    /// <summary>Số hiệu: "135/2025/QH15", "217/2026/NĐ-CP", "34/2026/TT-BXD", "9947/BXD-VPB", "QCVN 02:2022/BXD", "TCVN 9386-1:2025"…</summary>
    public string Code { get; set; } = "";
    /// <summary>Tên / trích yếu</summary>
    public string Title { get; set; } = "";
    public RegulationKind Kind { get; set; } = RegulationKind.TCVN;
    /// <summary>Ngày ban hành (chuỗi dd/MM/yyyy; "" = chưa ghi)</summary>
    public string IssuedDate { get; set; } = "";
    /// <summary>Ngày hiệu lực (chuỗi dd/MM/yyyy; "theo văn bản" = chưa xác minh)</summary>
    public string EffectiveDate { get; set; } = "";
    public RegulationStatus Status { get; set; } = RegulationStatus.HieuLuc;
    /// <summary>Số hiệu các văn bản bị văn bản này thay thế</summary>
    public List<string> Replaces { get; set; } = new();
    /// <summary>Số hiệu văn bản thay thế (nhiều văn bản: phân cách bằng dấu ";")</summary>
    public string ReplacedBy { get; set; } = "";
    public RegulationScope Scope { get; set; } = RegulationScope.PhapLy;
    /// <summary>Chủ đề (khoá tra trích dẫn trong chương trình): "luat-xd", "dong-dat", "dinh-muc"…; "" = không dùng để tra</summary>
    public string Topic { get; set; } = "";
    public string Note { get; set; } = "";
    /// <summary>Thông tin cần kiểm tra lại (chưa xác minh)</summary>
    public string VerifyNote { get; set; } = "";
    /// <summary>Đường dẫn nguồn đã đối chiếu</summary>
    public string SourceUrl { get; set; } = "";
    /// <summary>Cách viết khác dùng để dò trích dẫn trong văn bản của dự án (vd. "Định mức 1776")</summary>
    public List<string> Aliases { get; set; } = new();

    /// <summary>Cách trích dẫn đầy đủ: "Luật Xây dựng số 135/2025/QH15", "Nghị định 217/2026/NĐ-CP", "Thông tư 34/2026/TT-BXD", "TCVN 9386-1:2025".</summary>
    [JsonIgnore]
    public string Citation => Kind switch
    {
        RegulationKind.Luat => (string.IsNullOrWhiteSpace(Title) ? "Luật" : Title.Trim()) + " số " + Code,
        RegulationKind.NghiDinh => "Nghị định " + Code,
        RegulationKind.ThongTu => "Thông tư " + Code,
        RegulationKind.CongVan => "Công văn " + Code,
        _ => Code
    };

    /// <summary>Đang áp dụng: còn hiệu lực, hoặc đang kiểm tra nhưng chưa ghi văn bản thay thế.</summary>
    [JsonIgnore]
    public bool IsCurrent => Status == RegulationStatus.HieuLuc || (Status == RegulationStatus.KiemTra && string.IsNullOrWhiteSpace(ReplacedBy));

    /// <summary>Đã/có thể đã bị thay thế: hết hiệu lực, hoặc đang kiểm tra và có ghi văn bản thay thế.</summary>
    [JsonIgnore]
    public bool IsSuperseded => Status == RegulationStatus.HetHieuLuc || (Status == RegulationStatus.KiemTra && !string.IsNullOrWhiteSpace(ReplacedBy));

    /// <summary>Danh sách số hiệu văn bản thay thế (tách theo ";").</summary>
    public List<string> ReplacedByList() =>
        (ReplacedBy ?? "").Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

    [JsonIgnore] public string KindLabel => Regulations.KindLabel(Kind);
    [JsonIgnore] public string StatusLabel => Regulations.StatusLabel(Status);
    [JsonIgnore] public string ScopeLabel => Regulations.ScopeLabel(Scope);
    [JsonIgnore] public string ReplacesText => string.Join("; ", Replaces ?? new List<string>());

    public RegulationDoc Clone()
    {
        var c = (RegulationDoc)MemberwiseClone();
        c.Replaces = new List<string>(Replaces ?? new List<string>());
        c.Aliases = new List<string>(Aliases ?? new List<string>());
        return c;
    }

    internal void Sanitize()
    {
        Code = (Code ?? "").Trim(); Title ??= ""; IssuedDate ??= ""; EffectiveDate ??= ""; ReplacedBy ??= ""; Topic = (Topic ?? "").Trim();
        Note ??= ""; VerifyNote ??= ""; SourceUrl ??= "";
        Replaces = (Replaces ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        Aliases = (Aliases ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
    }

    public override string ToString() => Citation + (string.IsNullOrWhiteSpace(Title) || Kind == RegulationKind.Luat ? "" : " – " + Title);
}

/// <summary>Một cảnh báo khi kiểm tra dự án theo danh mục văn bản.</summary>
public sealed class RegulationWarning
{
    public const string LevelWarning = "Cảnh báo", LevelNote = "Lưu ý";
    /// <summary>"Cảnh báo" (trích dẫn văn bản đã hết hiệu lực) hoặc "Lưu ý"</summary>
    public string Level { get; set; } = LevelWarning;
    /// <summary>Vị trí trong dự án (vd. "Bảng khối lượng – nguồn định mức")</summary>
    public string Where { get; set; } = "";
    /// <summary>Số hiệu văn bản liên quan</summary>
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsWarning => Level == LevelWarning;
    public override string ToString() => $"[{Level}] {Where}: {Message}";
}

/// <summary>File danh mục của người dùng (JSON).</summary>
public sealed class RegulationFile
{
    public int Version { get; set; } = 1;
    public string Updated { get; set; } = "";
    /// <summary>Văn bản thêm mới / sửa so với danh mục có sẵn (khi xuất: toàn bộ danh mục)</summary>
    public List<RegulationDoc> Docs { get; set; } = new();
    /// <summary>Số hiệu văn bản có sẵn bị người dùng xoá khỏi danh mục</summary>
    public List<string> Removed { get; set; } = new();
}

/// <summary>Danh mục văn bản (có sẵn + phần người dùng cập nhật).</summary>
public sealed class Regulations
{
    public List<RegulationDoc> Docs { get; set; } = new();
    /// <summary>Số hiệu văn bản có sẵn bị xoá</summary>
    public List<string> Removed { get; set; } = new();
    /// <summary>Thông báo khi đọc file người dùng lỗi (null = không lỗi)</summary>
    [JsonIgnore] public string LoadMessage { get; set; }

    // ------------------------------------------------------------------
    // Nhãn tiếng Việt
    // ------------------------------------------------------------------
    public static string KindLabel(RegulationKind k) => k switch
    {
        RegulationKind.Luat => "Luật",
        RegulationKind.NghiDinh => "Nghị định",
        RegulationKind.ThongTu => "Thông tư",
        RegulationKind.QCVN => "QCVN",
        RegulationKind.TCVN => "TCVN",
        RegulationKind.CongVan => "Công văn",
        _ => k.ToString()
    };

    public static string StatusLabel(RegulationStatus s) => s switch
    {
        RegulationStatus.HieuLuc => "Còn hiệu lực",
        RegulationStatus.DuThao => "Dự thảo",
        RegulationStatus.HetHieuLuc => "Hết hiệu lực",
        RegulationStatus.KiemTra => "Kiểm tra",
        _ => s.ToString()
    };

    public static string ScopeLabel(RegulationScope s) => s switch
    {
        RegulationScope.PhapLy => "Pháp lý",
        RegulationScope.TaiTrong => "Tải trọng",
        RegulationScope.BTCT => "BTCT",
        RegulationScope.Thep => "Thép",
        RegulationScope.Mong => "Móng",
        RegulationScope.DongDat => "Động đất",
        RegulationScope.DinhMuc => "Định mức",
        RegulationScope.ChiPhi => "Chi phí",
        RegulationScope.CapCongTrinh => "Cấp công trình",
        RegulationScope.ThamKhao => "Tham khảo",
        _ => s.ToString()
    };

    /// <summary>Khoá so sánh số hiệu: bỏ dấu, chữ hoa, bỏ khoảng trắng, "Đ" → "D", "–" → "-".</summary>
    public static string Key(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "";
        var s = code.Replace('Đ', 'D').Replace('đ', 'd').Replace('–', '-').Replace('—', '-').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c)) continue;
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Truy vấn
    // ------------------------------------------------------------------
    public RegulationDoc Find(string code)
    {
        string k = Key(code);
        return k.Length == 0 ? null : Docs.FirstOrDefault(d => Key(d.Code) == k);
    }

    /// <summary>Văn bản đang áp dụng của một chủ đề: văn bản hiện hành không bị văn bản hiện hành khác cùng chủ đề thay thế (nhiều → văn bản sau cùng).</summary>
    public RegulationDoc ByTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic)) return null;
        var cands = Docs.Where(d => d.IsCurrent && string.Equals(d.Topic, topic, StringComparison.OrdinalIgnoreCase)).ToList();
        if (cands.Count == 0) return null;
        var replaced = new HashSet<string>(cands.SelectMany(d => d.Replaces ?? new List<string>()).Select(Key));
        var top = cands.Where(d => !replaced.Contains(Key(d.Code))).ToList();
        return (top.Count > 0 ? top : cands)[^1];
    }

    /// <summary>Các văn bản đang áp dụng (scope = null: tất cả).</summary>
    public List<RegulationDoc> Current(RegulationScope? scope = null) =>
        Docs.Where(d => d.IsCurrent && (scope == null || d.Scope == scope.Value)).ToList();

    /// <summary>Văn bản thay thế (đang áp dụng) của một văn bản đã bị thay thế; theo chuỗi thay thế nhiều cấp.</summary>
    public List<RegulationDoc> Successors(RegulationDoc d)
    {
        var res = new List<RegulationDoc>();
        if (d == null) return res;
        var seen = new HashSet<string> { Key(d.Code) };
        var queue = new Queue<string>(d.ReplacedByList());
        // văn bản khác ghi "thay thế" văn bản này
        foreach (var o in Docs.Where(x => (x.Replaces ?? new List<string>()).Any(r => Key(r) == Key(d.Code)))) queue.Enqueue(o.Code);
        while (queue.Count > 0)
        {
            string c = queue.Dequeue();
            if (!seen.Add(Key(c))) continue;
            var x = Find(c);
            if (x == null) continue;
            if (x.IsCurrent) { if (!res.Contains(x)) res.Add(x); }
            else foreach (var n in x.ReplacedByList()) queue.Enqueue(n);
        }
        return res;
    }

    // ------------------------------------------------------------------
    // Sửa danh mục
    // ------------------------------------------------------------------
    /// <summary>Thêm hoặc thay văn bản cùng số hiệu. true = thêm mới.</summary>
    public bool Upsert(RegulationDoc d)
    {
        if (d == null || string.IsNullOrWhiteSpace(d.Code)) return false;
        d.Sanitize();
        Removed?.RemoveAll(r => Key(r) == Key(d.Code));
        var old = Find(d.Code);
        if (old != null) { Docs[Docs.IndexOf(old)] = d; return false; }
        Docs.Add(d);
        return true;
    }

    public bool Remove(string code)
    {
        var d = Find(code);
        if (d == null) return false;
        Docs.Remove(d);
        if (BuiltIn().Find(code) != null && !Removed.Any(r => Key(r) == Key(code))) Removed.Add(d.Code);
        return true;
    }

    /// <summary>Đánh dấu hết hiệu lực, ghi văn bản thay thế (nếu có) và thêm vào danh sách "Thay thế" của văn bản mới.</summary>
    public void MarkSuperseded(string code, string replacedBy)
    {
        var d = Find(code);
        if (d == null) return;
        d.Status = RegulationStatus.HetHieuLuc;
        if (!string.IsNullOrWhiteSpace(replacedBy)) d.ReplacedBy = replacedBy.Trim();
        foreach (var n in d.ReplacedByList())
        {
            var nd = Find(n);
            if (nd != null && !nd.Replaces.Any(r => Key(r) == Key(d.Code))) nd.Replaces.Add(d.Code);
        }
    }

    public Regulations Clone() => new()
    {
        Docs = Docs.Select(d => d.Clone()).ToList(),
        Removed = new List<string>(Removed ?? new List<string>()),
        LoadMessage = LoadMessage
    };

    // ------------------------------------------------------------------
    // JSON
    // ------------------------------------------------------------------
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Đọc file JSON: dạng { "Docs": [...], "Removed": [...] } hoặc mảng văn bản [...].</summary>
    public static RegulationFile ReadFile(string path)
    {
        string txt = File.ReadAllText(path, Encoding.UTF8).TrimStart('﻿').Trim();
        RegulationFile f;
        if (txt.StartsWith("[")) f = new RegulationFile { Docs = JsonSerializer.Deserialize<List<RegulationDoc>>(txt, JsonOptions) ?? new List<RegulationDoc>() };
        else f = JsonSerializer.Deserialize<RegulationFile>(txt, JsonOptions) ?? new RegulationFile();
        f.Docs = (f.Docs ?? new List<RegulationDoc>()).Where(d => d != null && !string.IsNullOrWhiteSpace(d.Code)).ToList();
        foreach (var d in f.Docs) d.Sanitize();
        f.Removed ??= new List<string>();
        return f;
    }

    /// <summary>Gộp một file vào danh mục: văn bản cùng số hiệu được thay, văn bản mới được thêm, "Removed" bị xoá. Trả về số văn bản thêm/cập nhật.</summary>
    public (int Added, int Updated, int Removed) Merge(RegulationFile f)
    {
        int a = 0, u = 0, r = 0;
        if (f == null) return (0, 0, 0);
        foreach (var d in f.Docs) { if (Upsert(d.Clone())) a++; else u++; }
        foreach (var code in f.Removed ?? new List<string>())
        {
            var d = Find(code);
            if (d != null) { Docs.Remove(d); r++; }
            if (!Removed.Any(x => Key(x) == Key(code))) Removed.Add(code);
        }
        return (a, u, r);
    }

    public (int Added, int Updated, int Removed) ImportJson(string path) => Merge(ReadFile(path));

    /// <summary>Xuất toàn bộ danh mục ra JSON.</summary>
    public void ExportJson(string path) => WriteFile(path, new RegulationFile { Docs = Docs.Select(d => d.Clone()).ToList(), Removed = new List<string>(Removed) });

    /// <summary>Phần khác với danh mục có sẵn (để lưu làm mặc định của người dùng).</summary>
    public RegulationFile DiffFromBuiltIn()
    {
        var b = BuiltIn();
        var f = new RegulationFile();
        foreach (var d in Docs)
        {
            var o = b.Find(d.Code);
            if (o == null || JsonSerializer.Serialize(o, JsonOptions) != JsonSerializer.Serialize(d, JsonOptions)) f.Docs.Add(d.Clone());
        }
        foreach (var o in b.Docs) if (Find(o.Code) == null) f.Removed.Add(o.Code);
        return f;
    }

    public static void WriteFile(string path, RegulationFile f)
    {
        f.Updated = DateTime.Now.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(f, JsonOptions), new UTF8Encoding(false));
        File.Move(tmp, path, true);
    }

    // ==================================================================
    // DANH MỤC CÓ SẴN (cập nhật 02/11/2026 – chỉ thông tin đã kiểm tra; phần chưa xác minh ghi ở "Cần kiểm tra")
    // ==================================================================
    private const string UrlLuat135 = "https://qlda.gxd.vn/van-ban/luat/luat-xay-dung-135-2025.html";
    private const string UrlNd217 = "https://cdn.haiphong.gov.vn/gov-hpg/6866/tintuc/2026/7/nghi-dinh-217_2026_nd-cp639189458744453590.pdf";
    private const string UrlNd212 = "https://qlda.gxd.vn/van-ban/qlda/nghi-dinh-212-2026.html";
    private const string UrlNd206 = "https://qlda.gxd.vn/van-ban/chi-phi/nghi-dinh-206-2026.html";
    private const string UrlTt34 = "https://luatvietnam.vn/xay-dung/thong-tu-34-2026-tt-bxd-quy-dinh-chi-tiet-cap-cong-trinh-xay-dung-hieu-luc-01-07-2026-438787-d1.html";
    private const string UrlTt37 = "https://qlda.gxd.vn/van-ban/chi-phi/thong-tu-37-2026-TT-BXD.html";
    private const string UrlTt38 = "https://datafiles.chinhphu.vn/cpp/files/vbpq/2026/7/38-bxd.signed.pdf";
    private const string UrlCv9947 = "https://thuviennhadat.vn/phap-luat/tai-file-cong-van-9947-bxd-vpb-thay-the-dinh-muc-xay-dung-tai-thong-tu-38-2026-tt-bxd-742705.html";
    private const string UrlTcvn9386 = "https://hethongphapluat.com/tieu-chuan-quoc-gia-tcvn-9386-5-2025-ve-thiet-ke-ket-cau-chiu-dong-dat-phan-5-nen-mong-ket-cau-chan-va-cac-van-de-dia-ky-thuat.html";
    private const string UrlQcvn02Draft = "https://ibst.vn/upload/documents/file_upload/1770624538Thuyet-minh-sua-doi-QCVN02-v2.pdf";

    /// <summary>Ngày hiệu lực chưa xác minh.</summary>
    public const string ByDocument = "theo văn bản";

    private static RegulationDoc D(RegulationKind kind, string code, string title, string issued, string effective, RegulationStatus status,
        RegulationScope scope, string topic, string[] replaces = null, string replacedBy = "", string note = "", string verify = "", string url = "", string[] aliases = null) => new()
    {
        Kind = kind, Code = code, Title = title, IssuedDate = issued ?? "", EffectiveDate = effective ?? "", Status = status, Scope = scope, Topic = topic ?? "",
        Replaces = replaces?.ToList() ?? new List<string>(), ReplacedBy = replacedBy ?? "", Note = note ?? "", VerifyNote = verify ?? "", SourceUrl = url ?? "",
        Aliases = aliases?.ToList() ?? new List<string>()
    };

    /// <summary>Danh mục có sẵn của chương trình.</summary>
    public static Regulations BuiltIn()
    {
        const RegulationStatus HL = RegulationStatus.HieuLuc, HHL = RegulationStatus.HetHieuLuc, KT = RegulationStatus.KiemTra, DT = RegulationStatus.DuThao;
        var l = new List<RegulationDoc>
        {
            // ---------------- PHÁP LÝ ----------------
            D(RegulationKind.Luat, "135/2025/QH15", "Luật Xây dựng", "10/12/2025", "01/7/2026", HL, RegulationScope.PhapLy, Topics.Law,
                new[] { "50/2014/QH13", "62/2020/QH14" }, "",
                "Thay thế Luật Xây dựng số 50/2014/QH13 và Luật số 62/2020/QH14 sửa đổi, bổ sung một số điều của Luật Xây dựng.",
                "Ngày hiệu lực 01/7/2026 theo tài liệu phổ biến của Bộ Xây dựng – đối chiếu văn bản gốc.", UrlLuat135),
            D(RegulationKind.NghiDinh, "217/2026/NĐ-CP", "Quy định chi tiết một số điều của Luật Xây dựng về quản lý hoạt động xây dựng", "19/6/2026", ByDocument, HL,
                RegulationScope.PhapLy, Topics.Decree, new[] { "15/2021/NĐ-CP", "175/2024/NĐ-CP" }, "",
                "Bước thiết kế (Điều 16); nội dung thiết kế (Điều 19–23); chỉ dẫn kỹ thuật (Điều 24 – không bắt buộc với nhà ở riêng lẻ); quản lý thiết kế (Điều 25).",
                "Ngày hiệu lực: theo văn bản (chưa xác minh). Việc thay thế NĐ 15/2021/NĐ-CP, NĐ 175/2024/NĐ-CP: kiểm tra điều khoản thi hành.", UrlNd217),
            D(RegulationKind.NghiDinh, "212/2026/NĐ-CP", "Quy định về điều kiện năng lực hoạt động xây dựng, Hệ thống thông tin và cơ sở dữ liệu quốc gia về hoạt động xây dựng",
                "17/6/2026", "01/7/2026", HL, RegulationScope.PhapLy, Topics.Capacity, null, "",
                "Điều kiện năng lực tổ chức, cá nhân thiết kế.", "", UrlNd212),
            D(RegulationKind.NghiDinh, "206/2026/NĐ-CP", "Quản lý chi phí đầu tư xây dựng", "", ByDocument, HL, RegulationScope.ChiPhi, Topics.CostDecree,
                new[] { "10/2021/NĐ-CP" }, "", "Thay thế NĐ 10/2021/NĐ-CP – kiểm tra.",
                "Thông tin thay thế theo báo chí; ngày ban hành, hiệu lực: theo văn bản – kiểm tra.", UrlNd206),
            D(RegulationKind.ThongTu, "34/2026/TT-BXD", "Quy định chi tiết cấp công trình xây dựng phục vụ quản lý hoạt động xây dựng", "", "01/7/2026", HL,
                RegulationScope.CapCongTrinh, Topics.Grade, new[] { "06/2021/TT-BXD", "02/2025/TT-BXD" }, "",
                "Thay thế TT 06/2021/TT-BXD và TT 02/2025/TT-BXD (sửa đổi TT 06/2021).", "", UrlTt34),
            D(RegulationKind.ThongTu, "36/2026/TT-BXD", "Hướng dẫn xác định và quản lý chi phí đầu tư xây dựng", "", ByDocument, HL, RegulationScope.ChiPhi, Topics.CostCircular,
                null, "", "", "Ngày ban hành, hiệu lực: theo văn bản – kiểm tra."),
            D(RegulationKind.ThongTu, "37/2026/TT-BXD", "Hướng dẫn phương pháp xác định định mức dự toán và các chỉ tiêu kinh tế kỹ thuật", "26/6/2026", "01/7/2026", HL,
                RegulationScope.DinhMuc, Topics.NormMethod, new[] { "13/2021/TT-BXD", "01/2025/TT-BXD" }, "",
                "Thay thế TT 13/2021/TT-BXD, TT 01/2025/TT-BXD, Điều 12 TT 09/2025/TT-BXD, khoản 6 Điều 1 và Điều 3 TT 60/2025/TT-BXD.", "", UrlTt37),
            D(RegulationKind.ThongTu, "38/2026/TT-BXD", "Ban hành định mức xây dựng", "", "01/7/2026", HL, RegulationScope.DinhMuc, Topics.Norms,
                new[] { "12/2021/TT-BXD", "09/2024/TT-BXD", "08/2025/TT-BXD" }, "",
                "Áp dụng từ 01/7/2026. Phụ lục I–VIII (định mức khảo sát; dự toán xây dựng công trình; lắp đặt hệ thống kỹ thuật; lắp đặt máy và thiết bị công nghệ; "
                + "thí nghiệm chuyên ngành; sửa chữa và bảo dưỡng; vật liệu xây dựng (cấp phối); chi phí QLDA và tư vấn) được thay thế theo Công văn 9947/BXD-VPB ngày 29/6/2026.",
                "Thay thế hệ thống định mức theo TT 12/2021/TT-BXD – kiểm tra phụ lục theo CV 9947/BXD-VPB.", UrlTt38),
            D(RegulationKind.CongVan, "9947/BXD-VPB", "Thay thế các Phụ lục I–VIII định mức xây dựng ban hành kèm Thông tư 38/2026/TT-BXD", "29/6/2026", ByDocument, HL,
                RegulationScope.DinhMuc, Topics.NormLetter, null, "",
                "Phụ lục VII – định mức vật liệu xây dựng (cấp phối bê tông, vữa) dùng cho bảng phân tích vật tư.", "", UrlCv9947),
            D(RegulationKind.ThongTu, "39/2026/TT-BXD", "Về cơ sở dữ liệu (tên đầy đủ theo văn bản)", "", ByDocument, HL, RegulationScope.ChiPhi, "",
                null, "", "Văn bản liên quan, ít ảnh hưởng tới thiết kế kết cấu.", "Tên, ngày ban hành, hiệu lực: theo văn bản – kiểm tra."),
            D(RegulationKind.ThongTu, "40/2026/TT-BXD", "Về chi phí bảo trì (tên đầy đủ theo văn bản)", "", ByDocument, HL, RegulationScope.ChiPhi, "",
                null, "", "Văn bản liên quan, ít ảnh hưởng tới thiết kế kết cấu.", "Tên, ngày ban hành, hiệu lực: theo văn bản – kiểm tra."),

            // ---------------- QUY CHUẨN, TIÊU CHUẨN ----------------
            D(RegulationKind.QCVN, "QCVN 02:2022/BXD", "Quy chuẩn kỹ thuật quốc gia về số liệu điều kiện tự nhiên dùng trong xây dựng", "", "", HL,
                RegulationScope.TaiTrong, Topics.NaturalData, null, "",
                "Vùng áp lực gió (Bảng 5.1), gia tốc nền (Bảng 6.1, 6.2). Đang dự thảo sửa đổi (IBST, 2026) – tính lại Bảng 5.1 và bản đồ động đất theo xã/phường mới sau sắp xếp đơn vị hành chính 2025.",
                "Theo dõi bản sửa đổi để cập nhật bảng địa danh.", ""),
            D(RegulationKind.QCVN, "QCVN 02:2022/BXD – Sửa đổi (dự thảo)", "Dự thảo sửa đổi QCVN 02:2022/BXD: tính lại vùng áp lực gió Bảng 5.1 và bản đồ động đất theo đơn vị hành chính mới",
                "", "", DT, RegulationScope.TaiTrong, Topics.NaturalDataDraft, null, "",
                "Đang dự thảo sửa đổi – theo dõi để cập nhật bảng địa danh. Chưa ban hành: chưa dùng để thiết kế.", "", UrlQcvn02Draft),
            D(RegulationKind.TCVN, "TCVN 2737:2023", "Tải trọng và tác động – Tiêu chuẩn thiết kế", "", "", HL, RegulationScope.TaiTrong, Topics.Loads,
                new[] { "TCVN 2737:1995" }),
            D(RegulationKind.TCVN, "TCVN 9386-1:2025", "Thiết kế kết cấu chịu động đất – Phần 1: Quy định chung, tác động động đất và quy định cho nhà", "", "27/10/2026", KT,
                RegulationScope.DongDat, Topics.Seismic, new[] { "TCVN 9386:2012" }, "",
                "Cùng TCVN 9386-5:2025 thay thế TCVN 9386:2012 (theo lời nói đầu). Chương trình vẫn tính theo phương pháp tĩnh lực ngang tương đương, phổ thiết kế loại 1 – cùng cơ sở EN 1998-1 với bản 2012.",
                "Hiệu lực 27/10/2026 theo các trang pháp luật – kiểm tra. Nội dung bản 2025 chưa được đối chiếu trong chương trình: người dùng đối chiếu phổ phản ứng, hệ số tầm quan trọng, gia tốc nền theo bản 2025.",
                UrlTcvn9386),
            D(RegulationKind.TCVN, "TCVN 9386-5:2025", "Thiết kế kết cấu chịu động đất – Phần 5: Nền móng, kết cấu chắn và các vấn đề địa kỹ thuật", "", "27/10/2026", KT,
                RegulationScope.DongDat, Topics.SeismicGeo, new[] { "TCVN 9386:2012" }, "",
                "Cùng TCVN 9386-1:2025 thay thế TCVN 9386:2012 (theo lời nói đầu).",
                "Hiệu lực 27/10/2026 theo các trang pháp luật – kiểm tra.", UrlTcvn9386),
            D(RegulationKind.TCVN, "TCVN 5574:2018", "Thiết kế kết cấu bê tông và bê tông cốt thép", "", "", HL, RegulationScope.BTCT, Topics.Concrete),
            D(RegulationKind.TCVN, "TCVN 1651:2018", "Thép cốt bê tông", "", "", HL, RegulationScope.BTCT, Topics.Rebar),
            D(RegulationKind.TCVN, "TCVN 5575:2024", "Thiết kế kết cấu thép", "", "", HL, RegulationScope.Thep, Topics.Steel),
            D(RegulationKind.TCVN, "TCVN 10304:2014", "Móng cọc – Tiêu chuẩn thiết kế", "", "", HL, RegulationScope.Mong, Topics.Piles),
            D(RegulationKind.TCVN, "TCVN 9362:2012", "Tiêu chuẩn thiết kế nền nhà và công trình", "", "", HL, RegulationScope.Mong, Topics.Soil),
            D(RegulationKind.TCVN, "TCVN 9379:2012", "Kết cấu xây dựng và nền – Nguyên tắc cơ bản về tính toán", "", "", HL, RegulationScope.TaiTrong, Topics.Basis),
            D(RegulationKind.QCVN, "QCVN 06:2022/BXD", "Quy chuẩn kỹ thuật quốc gia về an toàn cháy cho nhà và công trình", "", "", HL, RegulationScope.ThamKhao, Topics.Fire,
                null, "", "Chỉ tham khảo (yêu cầu chịu lửa của kết cấu)."),
            D(RegulationKind.QCVN, "QCVN 18:2021/BXD", "Quy chuẩn kỹ thuật quốc gia về an toàn trong thi công xây dựng", "", "", HL, RegulationScope.ThamKhao, Topics.Safety,
                null, "", "Chỉ tham khảo.", "Kiểm tra phiên bản hiện hành."),

            // ---------------- ĐÃ HẾT HIỆU LỰC / ĐƯỢC THAY THẾ ----------------
            D(RegulationKind.Luat, "50/2014/QH13", "Luật Xây dựng", "", "", HHL, RegulationScope.PhapLy, Topics.Law, null, "135/2025/QH15",
                "Được thay thế bởi Luật Xây dựng số 135/2025/QH15.", "", "", new[] { "Luật Xây dựng 2014", "Luật Xây dựng năm 2014" }),
            D(RegulationKind.Luat, "62/2020/QH14", "Luật sửa đổi, bổ sung một số điều của Luật Xây dựng", "", "", HHL, RegulationScope.PhapLy, Topics.Law, null, "135/2025/QH15",
                "Được thay thế bởi Luật Xây dựng số 135/2025/QH15."),
            D(RegulationKind.NghiDinh, "15/2021/NĐ-CP", "Quy định chi tiết một số nội dung về quản lý dự án đầu tư xây dựng", "", "", HHL, RegulationScope.PhapLy, Topics.Decree,
                null, "217/2026/NĐ-CP", "", "Văn bản thay thế trực tiếp và thời điểm hết hiệu lực: kiểm tra."),
            D(RegulationKind.NghiDinh, "175/2024/NĐ-CP", "Quy định chi tiết Luật Xây dựng về quản lý hoạt động xây dựng (trước NĐ 217/2026/NĐ-CP)", "", "", KT, RegulationScope.PhapLy, Topics.Decree,
                null, "217/2026/NĐ-CP", "", "Chưa xác minh NĐ 217/2026/NĐ-CP có thay thế NĐ 175/2024/NĐ-CP – kiểm tra điều khoản thi hành của NĐ 217/2026/NĐ-CP."),
            D(RegulationKind.NghiDinh, "10/2021/NĐ-CP", "Quản lý chi phí đầu tư xây dựng", "", "", HHL, RegulationScope.ChiPhi, Topics.CostDecree, null, "206/2026/NĐ-CP",
                "", "Thay thế bởi NĐ 206/2026/NĐ-CP theo báo chí – kiểm tra."),
            D(RegulationKind.ThongTu, "06/2021/TT-BXD", "Quy định về phân cấp công trình xây dựng", "", "", HHL, RegulationScope.CapCongTrinh, Topics.Grade, null, "34/2026/TT-BXD"),
            D(RegulationKind.ThongTu, "02/2025/TT-BXD", "Sửa đổi, bổ sung Thông tư 06/2021/TT-BXD", "", "", HHL, RegulationScope.CapCongTrinh, Topics.Grade, null, "34/2026/TT-BXD"),
            D(RegulationKind.ThongTu, "12/2021/TT-BXD", "Ban hành định mức xây dựng", "", "", HHL, RegulationScope.DinhMuc, Topics.Norms, null, "38/2026/TT-BXD",
                "Thay thế hệ thống định mức theo TT 12/2021 – kiểm tra phụ lục theo CV 9947/BXD-VPB."),
            D(RegulationKind.ThongTu, "09/2024/TT-BXD", "Sửa đổi, bổ sung định mức xây dựng ban hành kèm Thông tư 12/2021/TT-BXD", "", "", HHL, RegulationScope.DinhMuc, Topics.Norms, null, "38/2026/TT-BXD"),
            D(RegulationKind.ThongTu, "08/2025/TT-BXD", "Sửa đổi, bổ sung định mức xây dựng ban hành kèm Thông tư 12/2021/TT-BXD", "", "", HHL, RegulationScope.DinhMuc, Topics.Norms, null, "38/2026/TT-BXD"),
            D(RegulationKind.ThongTu, "13/2021/TT-BXD", "Hướng dẫn phương pháp xác định các chỉ tiêu kinh tế kỹ thuật", "", "", HHL, RegulationScope.DinhMuc, Topics.NormMethod, null, "37/2026/TT-BXD"),
            D(RegulationKind.ThongTu, "01/2025/TT-BXD", "Sửa đổi liên quan phương pháp xác định định mức, chỉ tiêu kinh tế kỹ thuật (tên theo văn bản)", "", "", HHL, RegulationScope.DinhMuc, Topics.NormMethod,
                null, "37/2026/TT-BXD"),
            D(RegulationKind.CongVan, "1776/BXD-VP", "Định mức dự toán xây dựng công trình – Phần xây dựng (2007)", "", "", HHL, RegulationScope.DinhMuc, "", null, "38/2026/TT-BXD",
                "Định mức cũ, không dùng để lập dự toán mới.", "Không phải văn bản thay thế trực tiếp: hệ định mức hiện hành là Thông tư 38/2026/TT-BXD.", "",
                new[] { "Định mức 1776", "1776 AE", "1776 AF", "1776 AK" }),
            D(RegulationKind.TCVN, "TCVN 9386:2012", "Thiết kế công trình chịu động đất", "", "", HHL, RegulationScope.DongDat, Topics.Seismic, null, "TCVN 9386-1:2025; TCVN 9386-5:2025",
                "Được thay thế bởi TCVN 9386-1:2025 và TCVN 9386-5:2025.", "Ngày hết hiệu lực theo ngày hiệu lực của bản 2025 (27/10/2026) – kiểm tra.", UrlTcvn9386),
            D(RegulationKind.TCVN, "TCVN 2737:1995", "Tải trọng và tác động – Tiêu chuẩn thiết kế", "", "", HHL, RegulationScope.TaiTrong, Topics.Loads, null, "TCVN 2737:2023",
                "Được thay thế bởi TCVN 2737:2023 (ký hiệu vùng gió IA, IIB… không còn dùng)."),
        };
        return new Regulations { Docs = l };
    }
}

/// <summary>Chủ đề (khoá tra trích dẫn) dùng trong chương trình.</summary>
public static class Topics
{
    public const string Law = "luat-xd", Decree = "nd-qlhdxd", Capacity = "nd-nang-luc", CostDecree = "nd-chi-phi", Grade = "cap-cong-trinh",
        CostCircular = "tt-chi-phi", NormMethod = "pp-dinh-muc", Norms = "dinh-muc", NormLetter = "cv-dinh-muc",
        NaturalData = "dieu-kien-tu-nhien", NaturalDataDraft = "dieu-kien-tu-nhien-du-thao", Loads = "tai-trong", Seismic = "dong-dat",
        SeismicGeo = "dong-dat-nen-mong", Concrete = "btct", Rebar = "thep-cot", Steel = "ket-cau-thep", Piles = "mong-coc", Soil = "nen",
        Basis = "nguyen-tac-tinh", Fire = "an-toan-chay", Safety = "an-toan-thi-cong";
}

/// <summary>Danh mục đang dùng + tra cứu, chọn văn bản áp dụng cho dự án, kiểm tra trích dẫn của dự án.</summary>
public static class RegulationService
{
    public const string FileName = "VanBanPhapLy.json";

    /// <summary>Thư mục lưu danh mục của người dùng (null = %AppData%\KetCauVXT). Đặt khác để kiểm thử.</summary>
    public static string UserFolderOverride { get; set; }

    public static string UserFolder => !string.IsNullOrEmpty(UserFolderOverride) ? UserFolderOverride
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KetCauVXT");

    public static string UserFilePath => Path.Combine(UserFolder, FileName);

    private static Regulations _reg;

    /// <summary>Danh mục đang dùng: có sẵn + file người dùng (đọc lần đầu khi cần).</summary>
    public static Regulations Registry => _reg ??= LoadDefault();

    /// <summary>Danh mục có sẵn gộp file người dùng; file lỗi → danh mục có sẵn + LoadMessage.</summary>
    public static Regulations LoadDefault()
    {
        var r = Regulations.BuiltIn();
        try { if (File.Exists(UserFilePath)) r.Merge(Regulations.ReadFile(UserFilePath)); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            r.LoadMessage = "Không đọc được " + UserFilePath + ": " + ex.Message;
        }
        return r;
    }

    /// <summary>Đọc lại danh mục (sau khi đổi file người dùng / thư mục).</summary>
    public static void Reload() => _reg = null;

    /// <summary>Dùng danh mục r cho phiên làm việc (không lưu file).</summary>
    public static void Use(Regulations r) => _reg = r?.Clone();

    /// <summary>Lưu phần khác danh mục có sẵn vào file người dùng và dùng ngay.</summary>
    public static void SaveUserDefault(Regulations r)
    {
        Regulations.WriteFile(UserFilePath, (r ?? Regulations.BuiltIn()).DiffFromBuiltIn());
        _reg = (r ?? Regulations.BuiltIn()).Clone();
    }

    /// <summary>Xoá file người dùng → trở về danh mục có sẵn.</summary>
    public static void ResetUserDefault()
    {
        try { if (File.Exists(UserFilePath)) File.Delete(UserFilePath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        _reg = null;
    }

    // ------------------------------------------------------------------
    // Tra cứu
    // ------------------------------------------------------------------
    public static RegulationDoc Find(string code) => Registry.Find(code);

    /// <summary>Trích dẫn văn bản đang áp dụng của chủ đề; không có → fallback.</summary>
    public static string Cite(string topic, string fallback) => Registry.ByTopic(topic)?.Citation ?? fallback;

    /// <summary>Các văn bản đang áp dụng theo phạm vi (null = tất cả).</summary>
    public static List<RegulationDoc> Current(RegulationScope? scope = null) => Registry.Current(scope);

    /// <summary>Danh sách văn bản thuyết minh cần trích dẫn cho dự án: pháp lý (Luật, NĐ quản lý hoạt động xây dựng, TT cấp công trình;
    /// + định mức khi có chương khối lượng) và tiêu chuẩn theo nội dung dự án (BTCT, thép, động đất, móng nông/cọc).</summary>
    public static List<RegulationDoc> Applied(Project p, bool includeQuantities = false)
    {
        var reg = Registry;
        var topics = new List<string> { Topics.Law, Topics.Decree, Topics.Grade };
        if (includeQuantities) { topics.Add(Topics.Norms); topics.Add(Topics.NormLetter); }
        topics.Add(Topics.Loads);
        topics.Add(Topics.NaturalData);
        bool hasFrames = p != null && p.Frames.Count > 0;
        bool concrete = p == null || !hasFrames || p.Frames.Any(f => f.IsConcrete);
        bool steel = p != null && p.Frames.Any(f => !f.IsConcrete);
        if (concrete) { topics.Add(Topics.Concrete); topics.Add(Topics.Rebar); }
        if (steel) topics.Add(Topics.Steel);
        if (p != null && p.Seismic != null && p.Seismic.Enabled) { topics.Add(Topics.Seismic); topics.Add(Topics.SeismicGeo); }
        var ft = p?.Foundation?.Type ?? FoundationType.Isolated;
        bool piles = ft == FoundationType.Pile || (p?.Footings.Any(f => f.Type == FoundationType.Pile) ?? false);
        bool shallow = ft != FoundationType.Pile || (p?.Footings.Any(f => f.Type != null && f.Type != FoundationType.Pile) ?? false);
        if (shallow) topics.Add(Topics.Soil);
        if (piles) topics.Add(Topics.Piles);
        var res = new List<RegulationDoc>();
        foreach (var t in topics)
        {
            var d = reg.ByTopic(t);
            if (d != null && !res.Contains(d)) res.Add(d);
        }
        return res;
    }

    /// <summary>Văn bản pháp lý (Luật, Nghị định, Thông tư, Công văn) trong danh sách.</summary>
    public static bool IsLegal(RegulationDoc d) => d.Kind is RegulationKind.Luat or RegulationKind.NghiDinh or RegulationKind.ThongTu or RegulationKind.CongVan;

    /// <summary>Câu "X được thay thế bởi Y" cho các văn bản đã hết hiệu lực liên quan tới danh sách đang áp dụng.</summary>
    public static List<string> ReplacementSentences(IEnumerable<RegulationDoc> applied)
    {
        var reg = Registry;
        var res = new List<string>();
        foreach (var d in applied ?? Enumerable.Empty<RegulationDoc>())
        {
            var olds = (d.Replaces ?? new List<string>()).Select(reg.Find).Where(o => o != null && o.Status == RegulationStatus.HetHieuLuc).ToList();
            if (olds.Count == 0) continue;
            res.Add($"{d.Citation} thay thế {string.Join(", ", olds.Select(o => o.Citation + (string.IsNullOrWhiteSpace(o.VerifyNote) ? "" : " (kiểm tra)")))}.");
        }
        return res;
    }

    // ------------------------------------------------------------------
    // Kiểm tra dự án
    // ------------------------------------------------------------------
    /// <summary>Mẫu dò trích dẫn của một văn bản trong văn bản tự do.</summary>
    public static List<Regex> Matchers(RegulationDoc d)
    {
        var l = new List<Regex>();
        if (d == null || string.IsNullOrWhiteSpace(d.Code)) return l;
        string code = d.Code.Trim();
        const RegexOptions o = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        // "TCVN 9386:2012" – khoảng trắng tuỳ ý; không khớp "TCVN 9386-1:2025"
        string flex = string.Join(@"\s*", code.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape))
            .Replace("Đ", "[ĐD]").Replace("đ", "[đd]");
        l.Add(new Regex(@"(?<![\w/.-])" + flex + @"(?![\w-])", o));
        // "TT 12/2021", "Thông tư số 12/2021", "NĐ 15/2021", "Nghị định 10/2021"
        var m = Regex.Match(code, @"^(\d+/\d{4})/");
        if (m.Success && (d.Kind == RegulationKind.ThongTu || d.Kind == RegulationKind.NghiDinh))
        {
            string pre = d.Kind == RegulationKind.ThongTu ? @"(TT|Thông\s*tư)" : @"(NĐ|ND|Nghị\s*định)";
            l.Add(new Regex(pre + @"\s*(số\s*)?" + Regex.Escape(m.Groups[1].Value) + @"(?!\d)", o));
        }
        foreach (var a in d.Aliases ?? new List<string>())
            l.Add(new Regex(@"(?<!\w)" + string.Join(@"\s*", a.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape)) + @"(?!\w)", o));
        return l;
    }

    /// <summary>Các văn bản đã/có thể đã bị thay thế được trích dẫn trong text.</summary>
    public static List<RegulationDoc> SupersededCitedIn(string text)
    {
        var res = new List<RegulationDoc>();
        if (string.IsNullOrWhiteSpace(text)) return res;
        foreach (var d in Registry.Docs.Where(x => x.IsSuperseded))
            if (Matchers(d).Any(rx => rx.IsMatch(text))) res.Add(d);
        return res;
    }

    /// <summary>Các đoạn chữ tự do của dự án mà người dùng có thể ghi trích dẫn văn bản.</summary>
    public static List<(string Where, string Text)> ProjectTexts(Project p)
    {
        var l = new List<(string, string)>();
        if (p == null) return l;
        void Add(string where, string t) { if (!string.IsNullOrWhiteSpace(t)) l.Add((where, t)); }
        void AddObj(string where, object o)
        {
            if (o == null) return;
            foreach (var pi in o.GetType().GetProperties())
            {
                if (pi.PropertyType != typeof(string) || !pi.CanRead || pi.GetIndexParameters().Length > 0) continue;
                if (pi.Name.Contains("Data") || pi.Name.Contains("Image") || pi.Name.Contains("Logo")) continue;
                Add(where, pi.GetValue(o) as string);
            }
        }
        Add("Thông tin công trình – tên", p.Name);
        Add("Thông tin công trình – địa điểm", p.Location);
        var qs = p.QuantitySettings;
        if (qs != null)
        {
            if (!QuantitySettings.IsLegacyDefaultSource(qs.NormSource)) Add("Bảng khối lượng – nguồn định mức", qs.NormSource);
            foreach (var n in qs.ConcreteNorms ?? new List<ConcreteMixNorm>()) Add($"Định mức – cấp phối bê tông {n.Grade}", n.Note);
            foreach (var n in qs.MortarNorms ?? new List<MortarMixNorm>()) Add($"Định mức – cấp phối vữa {n.Grade}", n.Note);
            foreach (var n in qs.MasonryNorms ?? new List<MasonryNorm>()) Add($"Định mức – xây tường \"{n.Name}\"", n.Note);
        }
        AddObj("Thuyết minh tự động – tuỳ chọn", p.ReportOptions);
        AddObj("Khung tên công trình", p.TitleBlock);
        foreach (var s in p.Sheets ?? new List<Sheet>())
        {
            string w = "Tờ in " + s.Number;
            AddObj(w, s);
            foreach (var v in s.Views ?? new List<SheetView>()) { Add(w + " – khung nhìn/ghi chú", v.Param); Add(w + " – tiêu đề khung nhìn", v.Title); }
            foreach (var it in s.Items ?? new List<SheetItem>()) Add(w + " – chữ", it.Text);
        }
        return l;
    }

    /// <summary>Kiểm tra dự án: chữ tự do trích dẫn văn bản đã hết hiệu lực, thiết lập theo tiêu chuẩn cũ, lưu ý văn bản cần kiểm tra.</summary>
    public static List<RegulationWarning> CheckProject(Project p)
    {
        var res = new List<RegulationWarning>();
        if (p == null) return res;
        var reg = Registry;
        foreach (var (where, text) in ProjectTexts(p))
            foreach (var d in SupersededCitedIn(text))
            {
                var succ = reg.Successors(d);
                string repl = succ.Count > 0 ? " – thay bằng " + string.Join(", ", succ.Select(x => x.Citation)) : "";
                bool sure = d.Status == RegulationStatus.HetHieuLuc;
                res.Add(new RegulationWarning
                {
                    Level = sure ? RegulationWarning.LevelWarning : RegulationWarning.LevelNote,
                    Where = where, Code = d.Code,
                    Message = $"trích dẫn \"{d.Citation}\" ({(sure ? "đã hết hiệu lực / được thay thế" : "có thể đã được thay thế – kiểm tra")}){repl}."
                });
            }
        // vùng gió ký hiệu cũ (IA, IIB… theo TCVN 2737:1995)
        string z = (p.Wind?.Zone ?? "").Trim();
        if (z.Length > 0 && WindZones.Normalize(z).Length == 0 && Regex.IsMatch(z, @"^(vùng\s*)?(I|II|III|IV|V)\s*[AB]$", RegexOptions.IgnoreCase))
        {
            var old = reg.Find("TCVN 2737:1995");
            res.Add(new RegulationWarning
            {
                Where = "Tải gió – vùng gió", Code = old?.Code ?? "TCVN 2737:1995",
                Message = $"vùng gió \"{z}\" ghi theo ký hiệu của TCVN 2737:1995 (đã được thay thế bởi {Std.Loads}) – chọn lại vùng I…V theo {Std.NaturalData}."
            });
        }
        if (p.Seismic != null && p.Seismic.Enabled)
            res.Add(new RegulationWarning { Level = RegulationWarning.LevelNote, Where = "Tải động đất", Code = reg.ByTopic(Topics.Seismic)?.Code ?? "", Message = Std.SeismicMethodNote });
        var draft = reg.ByTopic(Topics.NaturalDataDraft) ?? reg.Docs.FirstOrDefault(d => d.Topic == Topics.NaturalDataDraft);
        if (draft != null && draft.Status == RegulationStatus.DuThao && (!string.IsNullOrWhiteSpace(p.LocationProvince) || !string.IsNullOrWhiteSpace(p.Wind?.Zone)))
            res.Add(new RegulationWarning
            {
                Level = RegulationWarning.LevelNote, Where = "Địa điểm xây dựng – vùng gió, agR", Code = draft.Code,
                Message = $"{Std.NaturalData} đang dự thảo sửa đổi (tính lại Bảng 5.1 và bản đồ động đất theo đơn vị hành chính mới) – theo dõi để cập nhật bảng địa danh."
            });
        return res;
    }
}

/// <summary>Trích dẫn tiêu chuẩn / văn bản đang áp dụng (tra danh mục theo chủ đề; danh mục trống → giá trị mặc định).</summary>
public static class Std
{
    public static string Law => RegulationService.Cite(Topics.Law, "Luật Xây dựng số 135/2025/QH15");
    public static string Decree => RegulationService.Cite(Topics.Decree, "Nghị định 217/2026/NĐ-CP");
    public static string Grade => RegulationService.Cite(Topics.Grade, "Thông tư 34/2026/TT-BXD");
    public static string Norms => RegulationService.Cite(Topics.Norms, "Thông tư 38/2026/TT-BXD");
    public static string NormLetter => RegulationService.Cite(Topics.NormLetter, "Công văn 9947/BXD-VPB");
    public static string Loads => RegulationService.Cite(Topics.Loads, "TCVN 2737:2023");
    public static string NaturalData => RegulationService.Cite(Topics.NaturalData, "QCVN 02:2022/BXD");
    public static string Concrete => RegulationService.Cite(Topics.Concrete, "TCVN 5574:2018");
    public static string Rebar => RegulationService.Cite(Topics.Rebar, "TCVN 1651:2018");
    public static string Steel => RegulationService.Cite(Topics.Steel, "TCVN 5575:2024");
    public static string Seismic => RegulationService.Cite(Topics.Seismic, "TCVN 9386-1:2025");
    public static string SeismicGeo => RegulationService.Cite(Topics.SeismicGeo, "TCVN 9386-5:2025");
    public static string Piles => RegulationService.Cite(Topics.Piles, "TCVN 10304:2014");
    public static string Soil => RegulationService.Cite(Topics.Soil, "TCVN 9362:2012");

    /// <summary>Trích dẫn điều khoản động đất: chương trình lập theo số điều của bản 2012 (cùng cơ sở EN 1998-1) → ghi "đối chiếu".
    /// Vd. "TCVN 9386-1:2025 – đối chiếu (số điều theo bản 2012: mục 4.3.3.2.2)".</summary>
    public static string SeismicClause(string clause2012) =>
        string.IsNullOrWhiteSpace(clause2012) ? Seismic : $"{Seismic} – đối chiếu (số điều theo bản 2012: {clause2012})";

    /// <summary>Ghi chú phương pháp động đất của chương trình (một câu, có chữ "thay thế" khi nhắc bản 2012).</summary>
    public static string SeismicMethodNote =>
        $"Tải trọng động đất tính theo {Seismic} (thay thế TCVN 9386:2012; cùng cơ sở EN 1998-1 với TCVN 9386:2012) bằng phương pháp tĩnh lực ngang tương đương, phổ thiết kế loại 1 – "
        + "người dùng đối chiếu phổ phản ứng, hệ số tầm quan trọng, gia tốc nền theo bản 2025.";
}
