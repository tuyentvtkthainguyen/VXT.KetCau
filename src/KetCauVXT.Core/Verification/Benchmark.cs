using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KetCauVXT.Verification;

// =====================================================================
// KIỂM CHỨNG ĐỘ TIN CẬY PHẦN MỀM (Software Verification)
// Mỗi bài toán chuẩn (benchmark) so sánh một đại lượng do Kết cấu VXT tính với giá trị tham chiếu:
//   – lời giải giải tích / dạng kín (sức bền vật liệu, cơ học kết cấu, lý thuyết dầm trên nền đàn hồi);
//   – hoặc phép tính tay ĐỘC LẬP viết lại trực tiếp từ công thức của tiêu chuẩn (không gọi mã thiết kế của chương trình);
//   – cột "Giá trị theo tài liệu" để người dùng nhập giá trị của sách/ví dụ mẫu (chưa có sẵn – chờ người dùng nhập).
// =====================================================================

/// <summary>Một đại lượng kiểm chứng của một bài toán chuẩn.</summary>
public sealed class BenchmarkCase
{
    /// <summary>Mã duy nhất (A01-1, B03-2…) – khoá lưu giá trị theo tài liệu của người dùng.</summary>
    public string Id { get; set; } = "";
    /// <summary>Nhóm (A. Phân tích, B. BTCT, C. Nền móng, D. Tải trọng).</summary>
    public string Group { get; set; } = "";
    /// <summary>Tên bài toán (ngắn).</summary>
    public string Title { get; set; } = "";
    /// <summary>Mô tả mô hình / số liệu đầu vào.</summary>
    public string Description { get; set; } = "";
    /// <summary>Nguồn lời giải tham chiếu (công thức giải tích, điều khoản tiêu chuẩn, bảng tra).</summary>
    public string ReferenceSource { get; set; } = "";
    /// <summary>Đại lượng so sánh (vd. "M3 giữa nhịp").</summary>
    public string Quantity { get; set; } = "";
    public string Unit { get; set; } = "";
    /// <summary>Giá trị tham chiếu (giải tích / tính tay độc lập).</summary>
    public double Reference { get; set; }
    /// <summary>Giá trị Kết cấu VXT tính được.</summary>
    public double Computed { get; set; } = double.NaN;
    /// <summary>Ngưỡng sai số cho phép (%).</summary>
    public double Tolerance { get; set; } = 1.0;
    /// <summary>Mẫu số tối thiểu khi tính sai số tương đối (cho đại lượng tham chiếu ≈ 0); 0 = dùng |tham chiếu|.</summary>
    public double Scale { get; set; }
    public string Notes { get; set; } = "";
    /// <summary>Lỗi khi chạy bài toán (ngoại lệ) – null nếu chạy được.</summary>
    public string Error { get; set; }

    // ---- người dùng nhập (sách / ví dụ mẫu) ----
    /// <summary>Giá trị theo tài liệu (sách, ví dụ mẫu) do người dùng nhập; null = chưa nhập.</summary>
    public double? UserBookValue { get; set; }
    /// <summary>Tên tài liệu, trang/ví dụ của giá trị theo tài liệu.</summary>
    public string UserBookSource { get; set; } = "";
    /// <summary>Loại nguồn kiểm chứng độc lập: "Sách/giáo trình", "Ví dụ mẫu tiêu chuẩn", "Phần mềm khác (ETABS/SAP…)", "Đơn vị thẩm tra/kỹ sư độc lập", "Tính tay độc lập".</summary>
    public string UserKind { get; set; } = "";
    /// <summary>Người / đơn vị xác nhận (kiểm chứng độc lập).</summary>
    public string UserVerifier { get; set; } = "";
    /// <summary>Ngày xác nhận (dd/MM/yyyy).</summary>
    public string UserDate { get; set; } = "";

    /// <summary>Ngưỡng khi so với tài liệu = max(ngưỡng bài toán, 3%) – tài liệu thường làm tròn 2–3 chữ số.</summary>
    [JsonIgnore]
    public double BookTolerance => Math.Max(Tolerance, 3.0);
    /// <summary>Kết luận so với tài liệu: null = chưa nhập.</summary>
    [JsonIgnore]
    public bool? BookPass => BookErrorPercent is double e ? e <= BookTolerance + 1e-12 : null;

    /// <summary>Sai số tương đối (%) của Kết cấu VXT so với tham chiếu: |VXT − TC| / max(|TC|, Scale)·100; NaN nếu lỗi.</summary>
    [JsonIgnore]
    public double ErrorPercent
    {
        get
        {
            if (Error != null || !double.IsFinite(Computed) || !double.IsFinite(Reference)) return double.NaN;
            double den = Math.Max(Math.Abs(Reference), Scale);
            if (den <= 1e-300) return Math.Abs(Computed) <= 1e-12 ? 0 : double.PositiveInfinity;
            return Math.Abs(Computed - Reference) / den * 100.0;
        }
    }

    /// <summary>Đạt: chạy không lỗi và sai số ≤ ngưỡng.</summary>
    [JsonIgnore]
    public bool Pass => double.IsFinite(ErrorPercent) && ErrorPercent <= Tolerance + 1e-12;

    /// <summary>Sai số (%) của Kết cấu VXT so với giá trị theo tài liệu; null khi chưa nhập.</summary>
    [JsonIgnore]
    public double? BookErrorPercent
    {
        get
        {
            if (UserBookValue is not double b || !double.IsFinite(b) || !double.IsFinite(Computed)) return null;
            double den = Math.Max(Math.Abs(b), Scale);
            if (den <= 1e-300) return null;
            return Math.Abs(Computed - b) / den * 100.0;
        }
    }

    [JsonIgnore]
    public string Verdict => Error != null ? "LỖI" : Pass ? "ĐẠT" : "KHÔNG ĐẠT";

    public override string ToString() =>
        $"{Id} {Title} – {Quantity}: TC = {Fmt(Reference)}, VXT = {Fmt(Computed)} {Unit}, sai số {Fmt(ErrorPercent, "0.###")}% ≤ {Fmt(Tolerance, "0.###")}% → {Verdict}";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Định dạng số gọn (4 chữ số có nghĩa, không mũ khi có thể).</summary>
    public static string Fmt(double v, string fmt = null)
    {
        if (double.IsNaN(v)) return "-";
        if (double.IsInfinity(v)) return v > 0 ? "∞" : "−∞";
        if (fmt != null) return v.ToString(fmt, Inv);
        double a = Math.Abs(v);
        if (a == 0) return "0";
        if (a >= 1e5 || a < 1e-4) return v.ToString("0.####E+0", Inv);
        int dec = a >= 1000 ? 1 : a >= 100 ? 2 : a >= 10 ? 3 : a >= 1 ? 4 : a >= 0.1 ? 5 : a >= 0.01 ? 6 : a >= 0.001 ? 7 : 8;
        return Math.Round(v, dec).ToString("0." + new string('#', dec), Inv);
    }
}

/// <summary>Kết quả chạy toàn bộ bộ kiểm chứng.</summary>
public sealed class VerificationResult
{
    public List<BenchmarkCase> Cases { get; } = new();
    public DateTime Started { get; set; } = DateTime.Now;
    public TimeSpan Elapsed { get; set; }
    /// <summary>Phiên bản Kết cấu VXT (assembly KetCauVXT.Core).</summary>
    public string Version { get; set; } = "";
    /// <summary>Môi trường chạy (.NET, hệ điều hành).</summary>
    public string RuntimeInfo { get; set; } = "";
    /// <summary>Các bài toán bỏ qua (chưa có chức năng tương ứng) kèm lý do.</summary>
    public List<string> Skipped { get; } = new();

    public int Count => Cases.Count;
    public int Passed => Cases.Count(c => c.Pass);
    public int Failed => Cases.Count - Passed;
    public int Errors => Cases.Count(c => c.Error != null);
    public bool AllPass => Cases.Count > 0 && Failed == 0;
    /// <summary>Số bài toán (theo mã bài toán, phần trước dấu "-" của Id).</summary>
    public int ProblemCount => Cases.Select(c => ProblemId(c.Id)).Distinct().Count();
    public double MaxErrorPercent => Cases.Where(c => double.IsFinite(c.ErrorPercent)).Select(c => c.ErrorPercent).DefaultIfEmpty(0).Max();
    public int BookValueCount => Cases.Count(c => c.UserBookValue.HasValue);

    public static string ProblemId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        int k = id.IndexOf('-');
        return k > 0 ? id.Substring(0, k) : id;
    }

    /// <summary>Các nhóm theo thứ tự xuất hiện.</summary>
    public List<string> Groups() => Cases.Select(c => c.Group).Distinct().ToList();

    /// <summary>Tóm tắt một dòng.</summary>
    public string Summary =>
        $"{ProblemCount} bài toán, {Count} đại lượng: {Passed} đạt, {Failed} không đạt{(Errors > 0 ? $" ({Errors} lỗi)" : "")}; "
        + $"sai số lớn nhất {BenchmarkCase.Fmt(MaxErrorPercent, "0.###")}%; thời gian {Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s";

    /// <summary>Tóm tắt theo nhóm: (nhóm, số đại lượng, số đạt, sai số lớn nhất %).</summary>
    public List<(string Group, int Count, int Passed, double MaxError)> GroupSummary() =>
        Groups().Select(g =>
        {
            var l = Cases.Where(c => c.Group == g).ToList();
            return (g, l.Count, l.Count(c => c.Pass), l.Where(c => double.IsFinite(c.ErrorPercent)).Select(c => c.ErrorPercent).DefaultIfEmpty(0).Max());
        }).ToList();
}

/// <summary>Giá trị theo tài liệu do người dùng nhập (lưu %AppData%\KetCauVXT\KiemChung.json, khoá theo Id bài toán).</summary>
public sealed class VerificationUserData
{
    public sealed class Entry
    {
        public double? Value { get; set; }
        public string Source { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Verifier { get; set; } = "";
        public string Date { get; set; } = "";
    }

    public const string FileName = "KiemChung.json";
    public Dictionary<string, Entry> Entries { get; set; } = new();

    /// <summary>Thư mục lưu (null = %AppData%\KetCauVXT). Đặt khác để kiểm thử.</summary>
    public static string UserFolderOverride { get; set; }

    public static string UserFolder => !string.IsNullOrEmpty(UserFolderOverride) ? UserFolderOverride
        : Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "KetCauVXT");

    public static string UserFilePath => Path.Combine(UserFolder, FileName);

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Đọc file của người dùng; file chưa có / hỏng → dữ liệu rỗng (message mô tả lỗi, "" nếu không lỗi).</summary>
    public static VerificationUserData Load(string path, out string message)
    {
        message = "";
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new VerificationUserData();
            var d = JsonSerializer.Deserialize<VerificationUserData>(File.ReadAllText(path, Encoding.UTF8), Json);
            d ??= new VerificationUserData();
            d.Entries ??= new Dictionary<string, Entry>();
            return d;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            message = "Không đọc được giá trị theo tài liệu (" + Path.GetFileName(path) + "): " + ex.Message;
            return new VerificationUserData();
        }
    }

    public static VerificationUserData Load() => Load(UserFilePath, out _);

    public void Save(string path)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json), new UTF8Encoding(false));
    }

    public void Save() => Save(UserFilePath);

    /// <summary>Loại nguồn kiểm chứng độc lập gợi ý.</summary>
    public static readonly string[] Kinds = { "Sách/giáo trình", "Ví dụ mẫu tiêu chuẩn", "Phần mềm khác (ETABS/SAP…)", "Đơn vị thẩm tra/kỹ sư độc lập", "Tính tay độc lập" };

    /// <summary>Nhập lại file Excel báo cáo kiểm chứng (sheet "Kết quả") do bên kiểm chứng độc lập đã điền các cột
    /// "Giá trị theo tài liệu", "Nguồn tài liệu", "Loại nguồn", "Người/đơn vị xác nhận", "Ngày xác nhận". Trả về số dòng nhận được.</summary>
    public int ImportXlsx(string path, out string message)
    {
        message = "";
        var sheets = KetCauVXT.Import.Csi.ExternalTableReader.ReadXlsxSheets(path);
        var sh = sheets.FirstOrDefault(x => x.Name == "Kết quả");
        if (sh.Rows == null) { message = "Không thấy sheet \"Kết quả\" trong " + Path.GetFileName(path) + "."; return 0; }
        int hi = sh.Rows.FindIndex(r => r.Any(c => (c ?? "").Trim() == "Mã"));
        if (hi < 0) { message = "Không thấy dòng tiêu đề (cột \"Mã\")."; return 0; }
        var h = sh.Rows[hi].Select(c => (c ?? "").Trim()).ToList();
        int Col(string prefix) => h.FindIndex(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        int cId = Col("Mã"), cVal = Col("Giá trị theo tài liệu"), cSrc = Col("Nguồn tài liệu"), cKind = Col("Loại nguồn"), cVer = Col("Người/đơn vị xác nhận"), cDate = Col("Ngày xác nhận");
        if (cId < 0 || cVal < 0) { message = "Thiếu cột \"Mã\" hoặc \"Giá trị theo tài liệu\"."; return 0; }
        string Cell(string[] r, int i) => i >= 0 && i < r.Length ? (r[i] ?? "").Trim() : "";
        int n = 0;
        foreach (var r in sh.Rows.Skip(hi + 1))
        {
            string id = Cell(r, cId);
            if (id.Length == 0) continue;
            string v = Cell(r, cVal).Replace(',', '.');
            double? val = double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv) ? dv : null;
            string src = Cell(r, cSrc), kind = Cell(r, cKind), ver = Cell(r, cVer), date = Cell(r, cDate);
            if (!val.HasValue && src.Length == 0 && ver.Length == 0) continue;
            Entries[id] = new Entry { Value = val, Source = src, Kind = kind, Verifier = ver, Date = date };
            n++;
        }
        message = n == 0 ? "File không có giá trị kiểm chứng độc lập nào." : $"Đã nhận {n} giá trị kiểm chứng độc lập.";
        return n;
    }

    /// <summary>Gán giá trị đã lưu vào các bài toán (theo Id).</summary>
    public void ApplyTo(IEnumerable<BenchmarkCase> cases)
    {
        foreach (var c in cases)
        {
            if (c == null) continue;
            if (Entries.TryGetValue(c.Id, out var e) && e != null)
            {
                c.UserBookValue = e.Value;
                c.UserBookSource = e.Source ?? "";
                c.UserKind = e.Kind ?? "";
                c.UserVerifier = e.Verifier ?? "";
                c.UserDate = e.Date ?? "";
            }
        }
    }

    /// <summary>Lấy giá trị người dùng đã nhập từ các bài toán (bỏ dòng trống, giữ mục của bài toán không có trong danh sách).</summary>
    public void CaptureFrom(IEnumerable<BenchmarkCase> cases)
    {
        foreach (var c in cases)
        {
            if (c == null || string.IsNullOrEmpty(c.Id)) continue;
            if (!c.UserBookValue.HasValue && string.IsNullOrWhiteSpace(c.UserBookSource) && string.IsNullOrWhiteSpace(c.UserVerifier)) { Entries.Remove(c.Id); continue; }
            Entries[c.Id] = new Entry
            {
                Value = c.UserBookValue, Source = (c.UserBookSource ?? "").Trim(), Kind = (c.UserKind ?? "").Trim(),
                Verifier = (c.UserVerifier ?? "").Trim(), Date = (c.UserDate ?? "").Trim()
            };
        }
    }
}
