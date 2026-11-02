using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace KetCauVXT.Model;

// =====================================================================
// ĐỊA DANH XÂY DỰNG – VÙNG ÁP LỰC GIÓ, GIA TỐC NỀN (QCVN 02:2022/BXD)
//
// Chỉ bảng giá trị theo vùng gió (I…V) và danh sách 34 đơn vị hành chính cấp tỉnh
// (sau sắp xếp từ 01/7/2025) là số liệu đã kiểm chứng và được đưa sẵn trong chương trình.
// Bảng phân vùng theo từng địa danh (QCVN 02:2022 Bảng 5.1) và agR (Bảng 6.1, 6.2) KHÔNG
// được gõ sẵn: người dùng nhập từ Excel/CSV hoặc khai báo từng địa danh; mọi địa danh đã xác
// nhận được lưu vào cơ sở dữ liệu riêng của người dùng (JSON) để dùng lại.
// =====================================================================

/// <summary>Thông số một vùng áp lực gió theo QCVN 02:2022/BXD (chu kỳ lặp 50 năm).</summary>
public sealed class WindZoneInfo
{
    /// <summary>Ký hiệu vùng: I, II, III, IV, V</summary>
    public string Zone { get; init; }
    /// <summary>Áp lực gió cơ sở W0, kN/m²</summary>
    public double W0 { get; init; }
    /// <summary>Vận tốc gió 3 giây, chu kỳ lặp 50 năm, m/s</summary>
    public double V3s50 { get; init; }
    /// <summary>Vận tốc gió trung bình 10 phút, chu kỳ lặp 50 năm, m/s</summary>
    public double V10m50 { get; init; }
    /// <summary>W0 theo daN/m²</summary>
    public double W0daN => Math.Round(W0 * 100, 1);

    private static string Vn(double v, string fmt = "0.##") => v.ToString(fmt, CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Vd: "W0 = 0,95 kN/m² (95 daN/m²), V3s,50 = 44 m/s, V10m,50 = 31 m/s"</summary>
    public string Describe() =>
        $"W0 = {Vn(W0)} kN/m² ({Vn(W0daN, "0")} daN/m²), V3s,50 = {Vn(V3s50, "0")} m/s, V10m,50 = {Vn(V10m50, "0")} m/s";

    /// <summary>Vd: "II – W0 = 0,95 kN/m²"</summary>
    public string Label => $"{Zone} – W0 = {Vn(W0)} kN/m²";

    public override string ToString() => Label;
}

/// <summary>Bảng vùng áp lực gió (QCVN 02:2022/BXD; dùng cho TCVN 2737:2023).</summary>
public static class WindZones
{
    public const string Source = "QCVN 02:2022/BXD – áp lực gió và vận tốc gió theo vùng";

    public static readonly IReadOnlyList<WindZoneInfo> All = new[]
    {
        new WindZoneInfo { Zone = "I",   W0 = 0.65, V3s50 = 36, V10m50 = 26 },
        new WindZoneInfo { Zone = "II",  W0 = 0.95, V3s50 = 44, V10m50 = 31 },
        new WindZoneInfo { Zone = "III", W0 = 1.25, V3s50 = 50, V10m50 = 36 },
        new WindZoneInfo { Zone = "IV",  W0 = 1.55, V3s50 = 55, V10m50 = 39 },
        new WindZoneInfo { Zone = "V",   W0 = 1.85, V3s50 = 61, V10m50 = 43 },
    };

    /// <summary>Chuẩn hoá ký hiệu vùng: "ii", "Vùng II", "2" → "II". Không nhận dạng được → "".
    /// Ký hiệu kiểu cũ có chữ A/B (IIA, IIIB… theo TCVN 2737:1995) không được quy đổi.</summary>
    public static string Normalize(string zone)
    {
        if (string.IsNullOrWhiteSpace(zone)) return "";
        var z = LocationDb.Norm(zone).Replace("VUNG", "").Replace(" ", "");
        return z switch
        {
            "I" or "1" => "I",
            "II" or "2" => "II",
            "III" or "3" => "III",
            "IV" or "4" => "IV",
            "V" or "5" => "V",
            _ => ""
        };
    }

    public static WindZoneInfo Find(string zone)
    {
        var z = Normalize(zone);
        return All.FirstOrDefault(i => i.Zone == z);
    }

    /// <summary>W0 (kN/m²) của vùng; 0 nếu không có vùng.</summary>
    public static double W0(string zone) => Find(zone)?.W0 ?? 0;
    public static double V3s(string zone) => Find(zone)?.V3s50 ?? 0;
    public static double V10m(string zone) => Find(zone)?.V10m50 ?? 0;

    /// <summary>Vùng có W0 đúng bằng giá trị cho trước (kN/m², hoặc daN/m² nếu &gt; 10); null nếu không khớp.</summary>
    public static WindZoneInfo FromW0(double w0)
    {
        if (w0 > 10) w0 /= 100;
        return All.FirstOrDefault(i => Math.Abs(i.W0 - w0) < 0.005);
    }

    /// <summary>Gán vùng gió cho dự án: Wind.Zone và Wind.W0 (giá trị bộ sinh tải gió dùng).</summary>
    public static bool Apply(Project p, string zone)
    {
        var z = Find(zone);
        if (p == null || z == null) return false;
        p.Wind.Zone = z.Zone;
        p.Wind.W0 = z.W0;
        return true;
    }
}

/// <summary>Dạng địa hình theo TCVN 2737:2023 (dùng cho hệ số k(z)).</summary>
public static class TerrainInfo
{
    public sealed record Item(string Code, string Title, string Description)
    {
        public override string ToString() => $"{Code} – {Title}";
    }

    public static readonly IReadOnlyList<Item> All = new[]
    {
        new Item("A", "Địa hình trống trải",
            "Trống trải, không có hoặc rất ít vật cản: bờ biển thoáng, mặt hồ lớn, cánh đồng, sa mạc."),
        new Item("B", "Địa hình tương đối trống trải",
            "Tương đối trống trải, có một số vật cản thưa thớt cao dưới 10 m: vùng ngoại ô ít nhà, làng mạc, rừng thưa hoặc rừng non."),
        new Item("C", "Địa hình bị che chắn mạnh",
            "Bị che chắn mạnh, có nhiều vật cản sát nhau cao từ 10 m trở lên: trong thành phố, vùng rừng rậm."),
    };

    public static Item Find(string code) => All.FirstOrDefault(i => string.Equals(i.Code, (code ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Describe(string code)
    {
        var i = Find(code);
        return i == null ? "" : $"Dạng {i.Code} – {i.Title}: {i.Description}";
    }

    /// <summary>Toàn bộ giải thích A/B/C (để làm tooltip).</summary>
    public static string HelpText() =>
        string.Join(Environment.NewLine, All.Select(i => $"{i.Code} – {i.Title}: {i.Description}"))
        + Environment.NewLine + "Chọn theo tính chất địa hình phía đón gió quanh công trình; đối chiếu điều khoản dạng địa hình của TCVN 2737:2023.";
}

/// <summary>Đơn vị hành chính cấp tỉnh (sau 01/7/2025) và các tỉnh cũ hợp nhất vào.</summary>
public sealed class ProvinceInfo
{
    /// <summary>Tên tỉnh/thành phố mới, vd "Thái Nguyên", "TP Hồ Chí Minh"</summary>
    public string Name { get; init; }
    /// <summary>Các tỉnh/thành phố trước 01/7/2025 hợp thành (gồm cả tên chính nó nếu có)</summary>
    public IReadOnlyList<string> Former { get; init; } = Array.Empty<string>();
    /// <summary>Tên gọi khác để tìm kiếm (vd "Thừa Thiên Huế", "HCM", "Sài Gòn")</summary>
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    public bool Merged => Former.Count > 1;
    public string Display => Merged ? $"{Name} (gồm {string.Join(", ", Former)})" : Name;
    public override string ToString() => Display;
}

/// <summary>Một địa danh (xã/phường hoặc huyện cũ) với vùng gió, agR, loại nền.</summary>
public sealed class LocationEntry
{
    /// <summary>Tỉnh/thành phố mới (sau 01/7/2025)</summary>
    public string Province { get; set; } = "";
    /// <summary>Tỉnh/thành phố cũ (trước sáp nhập), nếu có</summary>
    public string FormerProvince { get; set; } = "";
    /// <summary>Huyện/quận/thị xã cũ (khi bảng nguồn ghi theo huyện), nếu có</summary>
    public string FormerDistrict { get; set; } = "";
    /// <summary>Tên xã/phường/đặc khu (hoặc huyện cũ)</summary>
    public string Name { get; set; } = "";
    /// <summary>Vùng gió I…V; "" = chưa có</summary>
    public string WindZone { get; set; } = "";
    /// <summary>W0 nhập riêng (kN/m²); 0 = lấy theo vùng gió</summary>
    public double W0 { get; set; }
    /// <summary>Đỉnh gia tốc nền tham chiếu agR (đơn vị g, vd 0.0797); 0 = chưa có số liệu</summary>
    public double AgR { get; set; }
    /// <summary>Loại nền đất A…E (không bắt buộc)</summary>
    public string GroundType { get; set; } = "";
    /// <summary>Nguồn số liệu: "QCVN 02:2022 Bảng 5.1", "Người dùng"…</summary>
    public string Source { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime Updated { get; set; } = DateTime.Now;

    /// <summary>W0 dùng tính toán (kN/m²): W0 riêng nếu có, không thì theo vùng.</summary>
    [JsonIgnore] public double EffectiveW0 => W0 > 0 ? W0 : WindZones.W0(WindZone);
    [JsonIgnore] public bool HasWind => EffectiveW0 > 0;
    [JsonIgnore] public bool HasData => HasWind || AgR > 0;

    /// <summary>Tên hiển thị trong danh sách chọn của một tỉnh.</summary>
    [JsonIgnore]
    public string Display
    {
        get
        {
            var extra = new List<string>();
            if (!string.IsNullOrWhiteSpace(FormerDistrict)) extra.Add(FormerDistrict.Trim());
            if (!string.IsNullOrWhiteSpace(FormerProvince) && LocationDb.Norm(FormerProvince) != LocationDb.Norm(Province)) extra.Add(FormerProvince.Trim() + " cũ");
            return extra.Count == 0 ? Name : $"{Name} ({string.Join(", ", extra)})";
        }
    }

    public LocationEntry Clone() => (LocationEntry)MemberwiseClone();
    public override string ToString() => $"{Display} – {Province}";
}

/// <summary>Kết quả nhập bảng địa danh.</summary>
public sealed class LocationImportResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public List<string> Messages { get; } = new();
    public List<LocationEntry> Entries { get; } = new();
    public override string ToString() =>
        $"Thêm {Added}, cập nhật {Updated}, bỏ qua {Skipped} dòng" + (Messages.Count > 0 ? $" ({Messages.Count} ghi chú)" : "");
}

/// <summary>Cơ sở dữ liệu địa danh của người dùng (JSON) + danh mục 34 tỉnh/thành.</summary>
public sealed class LocationDb
{
    public const string SourceQcvn = "QCVN 02:2022 Bảng 5.1";
    public const string SourceUser = "Người dùng";
    public const string ExampleMark = "VÍ DỤ – xoá trước khi dùng";

    /// <summary>Nguồn của danh mục tỉnh/vùng gió có sẵn trong chương trình.</summary>
    public const string ProvinceListSource = "QCVN 02:2022/BXD (Bảng 5.1) – đang dự thảo sửa đổi theo đơn vị hành chính mới";

    /// <summary>Nhãn nguồn hiển thị: theo trạng thái bản sửa đổi QCVN 02 trong danh mục văn bản (Model/Regulations.cs).
    /// Khi người dùng đánh dấu bản sửa đổi "Còn hiệu lực" → nhắc nhập bảng địa danh mới (Nhập từ Excel/CSV, cột Nguồn ghi số hiệu mới).</summary>
    public static string SourceLabel
    {
        get
        {
            var reg = RegulationService.Registry;
            var draft = reg.Docs.FirstOrDefault(d => d.Topic == Topics.NaturalDataDraft);
            if (draft == null || draft.Status == RegulationStatus.DuThao) return ProvinceListSource;
            if (draft.IsCurrent)
                return $"{draft.Code} – đã ban hành: nhập bảng địa danh theo đơn vị hành chính mới (Nhập từ Excel/CSV) và đối chiếu số liệu cũ";
            return $"{Std.NaturalData} (Bảng 5.1)";
        }
    }

    public int Version { get; set; } = 1;
    public List<LocationEntry> Entries { get; set; } = new();

    // ------------------------------------------------------------------
    // 34 đơn vị hành chính cấp tỉnh từ 01/7/2025 (Nghị quyết 202/2025/QH15)
    // ------------------------------------------------------------------
    private static ProvinceInfo P(string name, params string[] former) => new() { Name = name, Former = former.Length == 0 ? new[] { name } : former };

    public static readonly IReadOnlyList<ProvinceInfo> Provinces = new[]
    {
        // 11 đơn vị không sắp xếp
        P("Hà Nội"),
        new ProvinceInfo { Name = "Huế", Former = new[] { "Huế" }, Aliases = new[] { "Thừa Thiên Huế", "Thừa Thiên - Huế" } },
        P("Lai Châu"), P("Điện Biên"), P("Sơn La"), P("Lạng Sơn"), P("Quảng Ninh"),
        P("Thanh Hóa"), P("Nghệ An"), P("Hà Tĩnh"), P("Cao Bằng"),
        // 23 đơn vị hình thành sau sắp xếp
        P("Tuyên Quang", "Tuyên Quang", "Hà Giang"),
        P("Lào Cai", "Lào Cai", "Yên Bái"),
        P("Thái Nguyên", "Thái Nguyên", "Bắc Kạn"),
        P("Phú Thọ", "Phú Thọ", "Vĩnh Phúc", "Hòa Bình"),
        P("Bắc Ninh", "Bắc Ninh", "Bắc Giang"),
        P("Hưng Yên", "Hưng Yên", "Thái Bình"),
        P("Hải Phòng", "Hải Phòng", "Hải Dương"),
        P("Ninh Bình", "Ninh Bình", "Hà Nam", "Nam Định"),
        P("Quảng Trị", "Quảng Trị", "Quảng Bình"),
        P("Đà Nẵng", "Đà Nẵng", "Quảng Nam"),
        P("Quảng Ngãi", "Quảng Ngãi", "Kon Tum"),
        P("Gia Lai", "Gia Lai", "Bình Định"),
        P("Khánh Hòa", "Khánh Hòa", "Ninh Thuận"),
        P("Lâm Đồng", "Lâm Đồng", "Đắk Nông", "Bình Thuận"),
        P("Đắk Lắk", "Đắk Lắk", "Phú Yên"),
        new ProvinceInfo { Name = "TP Hồ Chí Minh", Former = new[] { "TP Hồ Chí Minh", "Bình Dương", "Bà Rịa – Vũng Tàu" }, Aliases = new[] { "Hồ Chí Minh", "HCM", "TP HCM", "Sài Gòn" } },
        P("Đồng Nai", "Đồng Nai", "Bình Phước"),
        P("Tây Ninh", "Tây Ninh", "Long An"),
        P("Cần Thơ", "Cần Thơ", "Sóc Trăng", "Hậu Giang"),
        P("Vĩnh Long", "Vĩnh Long", "Bến Tre", "Trà Vinh"),
        P("Đồng Tháp", "Đồng Tháp", "Tiền Giang"),
        P("Cà Mau", "Cà Mau", "Bạc Liêu"),
        P("An Giang", "An Giang", "Kiên Giang"),
    };

    // ------------------------------------------------------------------
    // Chuẩn hoá chữ (bỏ dấu tiếng Việt, đ→d, chữ hoa, gộp khoảng trắng/dấu câu)
    // ------------------------------------------------------------------
    public static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var f = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(f.Length);
        foreach (char c in f)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : ' ');
        }
        // Hòa/Hoà, Thủy/Thuỷ… sau khi bỏ dấu đều trùng nhau
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private static readonly Regex ProvincePrefix = new(@"^(TINH|THANH PHO|TP)\s+", RegexOptions.Compiled);
    private static readonly Regex PlacePrefix = new(@"^(XA|PHUONG|THI TRAN|THI XA|HUYEN|QUAN|THANH PHO|TP|DAC KHU|TX|TT)\s+", RegexOptions.Compiled);

    /// <summary>Tên tỉnh chuẩn hoá, bỏ "Tỉnh", "Thành phố", "TP".</summary>
    public static string ProvinceKey(string s) => ProvincePrefix.Replace(Norm(s), "");
    /// <summary>Tên địa danh chuẩn hoá, bỏ tiền tố "Xã", "Phường", "Huyện"…</summary>
    public static string PlaceKey(string s) => PlacePrefix.Replace(Norm(s), "");

    /// <summary>Tìm tỉnh/thành mới theo tên mới, tên tỉnh cũ hoặc tên gọi khác (không phân biệt dấu).</summary>
    public static ProvinceInfo FindProvince(string text)
    {
        var k = ProvinceKey(text);
        if (k.Length == 0) return null;
        foreach (var p in Provinces) if (ProvinceKey(p.Name) == k) return p;
        foreach (var p in Provinces)
            if (p.Former.Any(f => ProvinceKey(f) == k) || p.Aliases.Any(a => ProvinceKey(a) == k)) return p;
        return null;
    }

    /// <summary>Tên tỉnh cũ đúng chính tả nếu văn bản là một tỉnh cũ (trước 01/7/2025); null nếu không phải.</summary>
    public static string FindFormerProvinceName(string text)
    {
        var k = ProvinceKey(text);
        foreach (var p in Provinces)
            foreach (var f in p.Former) if (ProvinceKey(f) == k) return f;
        return null;
    }

    /// <summary>Tìm tỉnh theo chuỗi (chứa các từ khoá; tên mới, cũ, tên gọi khác).</summary>
    public static List<ProvinceInfo> SearchProvinces(string text)
    {
        var toks = Tokens(text);
        if (toks.Length == 0) return Provinces.ToList();
        return Provinces.Where(p =>
        {
            var hay = " " + Norm(string.Join(" ", new[] { p.Name }.Concat(p.Former).Concat(p.Aliases))) + " ";
            return toks.All(t => hay.Contains(t));
        }).ToList();
    }

    private static string[] Tokens(string text) => Norm(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // ------------------------------------------------------------------
    // Truy vấn dữ liệu
    // ------------------------------------------------------------------
    /// <summary>Tìm địa danh theo từ khoá (không dấu): tên, tỉnh mới, tỉnh cũ, huyện cũ, ghi chú.</summary>
    public List<LocationEntry> Search(string text)
    {
        var toks = Tokens(text);
        if (toks.Length == 0) return Entries.ToList();
        return Entries.Where(e =>
        {
            var prov = FindProvince(e.Province);
            var hay = " " + Norm(string.Join(" ", e.Name, e.Province, e.FormerProvince, e.FormerDistrict, e.Note,
                prov == null ? "" : string.Join(" ", prov.Former.Concat(prov.Aliases)))) + " ";
            return toks.All(t => hay.Contains(t));
        }).ToList();
    }

    /// <summary>Các địa danh thuộc một tỉnh/thành mới (nhận cả tên tỉnh cũ).</summary>
    public List<LocationEntry> ForProvince(string province)
    {
        var p = FindProvince(province);
        string key = p != null ? ProvinceKey(p.Name) : ProvinceKey(province);
        return Entries.Where(e =>
        {
            var ep = FindProvince(e.Province) ?? FindProvince(e.FormerProvince);
            string ek = ep != null ? ProvinceKey(ep.Name) : ProvinceKey(e.Province);
            return ek == key;
        }).OrderBy(e => PlaceKey(e.Name), StringComparer.Ordinal).ThenBy(e => Norm(e.FormerDistrict), StringComparer.Ordinal).ToList();
    }

    /// <summary>Tìm đúng địa danh trong một tỉnh (so khớp không dấu, bỏ tiền tố Xã/Phường/Huyện…).</summary>
    public LocationEntry Find(string province, string name, string formerDistrict = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var list = ForProvince(province);
        string n = Norm(name), k = PlaceKey(name);
        bool District(LocationEntry e) => formerDistrict == null || Norm(e.FormerDistrict) == Norm(formerDistrict);
        return list.FirstOrDefault(e => Norm(e.Name) == n && District(e))
            ?? list.FirstOrDefault(e => Norm(e.Display) == n)
            ?? list.FirstOrDefault(e => PlaceKey(e.Name) == k && District(e))
            ?? list.FirstOrDefault(e => Norm(e.Name) == n || PlaceKey(e.Name) == k);
    }

    /// <summary>Thêm hoặc cập nhật (trùng tỉnh + tên + huyện cũ). Trả về true nếu là địa danh mới.</summary>
    public bool Upsert(LocationEntry e)
    {
        if (e == null || string.IsNullOrWhiteSpace(e.Name)) return false;
        e.Name = e.Name.Trim();
        var prov = FindProvince(e.Province);
        if (prov == null && !string.IsNullOrWhiteSpace(e.FormerProvince)) prov = FindProvince(e.FormerProvince);
        if (prov != null) e.Province = prov.Name;
        e.WindZone = WindZones.Normalize(e.WindZone);
        var old = Entries.FirstOrDefault(x => SameKey(x, e));
        if (old != null) { Entries[Entries.IndexOf(old)] = e; return false; }
        Entries.Add(e);
        return true;
    }

    private static bool SameKey(LocationEntry a, LocationEntry b) =>
        ProvinceKey(a.Province) == ProvinceKey(b.Province) && PlaceKey(a.Name) == PlaceKey(b.Name)
        && ProvinceKey(a.FormerProvince) == ProvinceKey(b.FormerProvince) && Norm(a.FormerDistrict) == Norm(b.FormerDistrict);

    public bool Remove(LocationEntry e) => Entries.Remove(e);

    // ------------------------------------------------------------------
    // Lưu / mở JSON
    // ------------------------------------------------------------------
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Mở CSDL; file chưa có → CSDL rỗng.</summary>
    public static LocationDb Load(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new LocationDb();
        var db = JsonSerializer.Deserialize<LocationDb>(File.ReadAllText(path, Encoding.UTF8), JsonOptions) ?? new LocationDb();
        db.Entries ??= new List<LocationEntry>();
        db.Entries.RemoveAll(e => e == null || string.IsNullOrWhiteSpace(e.Name));
        foreach (var e in db.Entries) { e.Province ??= ""; e.FormerProvince ??= ""; e.FormerDistrict ??= ""; e.WindZone ??= ""; e.GroundType ??= ""; e.Source ??= ""; e.Note ??= ""; }
        return db;
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions), new UTF8Encoding(false));
        File.Move(tmp, path, true);
    }

    // ------------------------------------------------------------------
    // Xuất CSV
    // ------------------------------------------------------------------
    public static readonly string[] CsvHeaders =
        { "Tỉnh/Thành phố", "Tỉnh cũ", "Huyện cũ", "Địa danh (xã, phường, huyện)", "Vùng gió", "W0 (kN/m²)", "agR (g)", "Loại nền", "Nguồn", "Ghi chú" };

    private static string Csv(string s)
    {
        s ??= "";
        return s.IndexOfAny(new[] { ';', '"', '\n', '\r', ',' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    private static string Num(double v) => v > 0 ? v.ToString("0.####", CultureInfo.InvariantCulture).Replace('.', ',') : "";

    private static string CsvLine(LocationEntry e) => string.Join(";", new[]
    {
        Csv(e.Province), Csv(e.FormerProvince), Csv(e.FormerDistrict), Csv(e.Name), Csv(e.WindZone),
        Csv(e.W0 > 0 ? Num(e.W0) : ""), Csv(Num(e.AgR)), Csv(e.GroundType), Csv(e.Source), Csv(e.Note)
    });

    /// <summary>Ghi CSV (UTF-8 có BOM, phân cách ';', số thập phân dấu phẩy – mở thẳng bằng Excel).</summary>
    public static void ExportCsv(string path, IEnumerable<LocationEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", CsvHeaders.Select(Csv)));
        foreach (var e in entries) sb.AppendLine(CsvLine(e));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    /// <summary>Ghi file CSV mẫu: dòng tiêu đề + 2 dòng ví dụ (tên giả, đánh dấu để xoá – chương trình bỏ qua khi nhập).</summary>
    public static void WriteTemplateCsv(string path)
    {
        ExportCsv(path, new[]
        {
            new LocationEntry { Province = "Thái Nguyên", FormerProvince = "Bắc Kạn", FormerDistrict = "", Name = "Xã Mẫu Một", WindZone = "I", Source = SourceQcvn, Note = ExampleMark },
            new LocationEntry { Province = "Thái Nguyên", FormerProvince = "Thái Nguyên", FormerDistrict = "Huyện Mẫu", Name = "Phường Mẫu Hai", WindZone = "II", W0 = 0.95, AgR = 0.05, GroundType = "C", Source = SourceQcvn, Note = ExampleMark },
        });
    }

    // ------------------------------------------------------------------
    // Nhập CSV / Excel
    // ------------------------------------------------------------------
    /// <summary>Đọc CSV (UTF-8 hoặc Windows-1258; phân cách ';' ',' hoặc tab) rồi gộp vào CSDL.</summary>
    public LocationImportResult ImportCsv(string path, string defaultSource = SourceQcvn) =>
        ImportRows(ReadCsvRows(File.ReadAllBytes(path)), defaultSource);

    /// <summary>Đọc sheet đầu tiên của file .xlsx rồi gộp vào CSDL.</summary>
    public LocationImportResult ImportXlsx(string path, string defaultSource = SourceQcvn) =>
        ImportRows(ReadXlsxRows(path), defaultSource);

    /// <summary>Nhập theo phần mở rộng (.xlsx/.xlsm → Excel, còn lại → CSV/TXT).</summary>
    public LocationImportResult ImportFile(string path, string defaultSource = SourceQcvn)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".xls") throw new InvalidDataException("Định dạng .xls (Excel 97-2003) không được hỗ trợ – hãy lưu lại thành .xlsx hoặc .csv.");
        return ext is ".xlsx" or ".xlsm" ? ImportXlsx(path, defaultSource) : ImportCsv(path, defaultSource);
    }

    public static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1258).GetString(bytes).Normalize(NormalizationForm.FormC);
        }
    }

    public static List<string[]> ReadCsvRows(byte[] bytes)
    {
        var text = DecodeText(bytes).Normalize(NormalizationForm.FormC);
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int first = 0;
        char sep = '\0';
        if (lines.Length > 0 && lines[0].Trim().StartsWith("sep=", StringComparison.OrdinalIgnoreCase))
        {
            var t = lines[0].Trim();
            if (t.Length > 4) sep = t[4];
            first = 1;
        }
        if (sep == '\0')
        {
            var sample = lines.Skip(first).Where(l => l.Trim().Length > 0).Take(10).ToList();
            int Count(char c) => sample.Sum(l => CountOutsideQuotes(l, c));
            int sc = Count(';'), cc = Count(','), tc = Count('\t');
            sep = tc >= sc && tc >= cc && tc > 0 ? '\t' : sc >= cc && sc > 0 ? ';' : ',';
        }
        return ParseCsv(string.Join("\n", lines.Skip(first)), sep);
    }

    private static int CountOutsideQuotes(string l, char c)
    {
        int n = 0; bool q = false;
        foreach (char ch in l) { if (ch == '"') q = !q; else if (ch == c && !q) n++; }
        return n;
    }

    private static List<string[]> ParseCsv(string text, char sep)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool q = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (q)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else q = false;
                }
                else cell.Append(c);
            }
            else if (c == '"') q = true;
            else if (c == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n') { row.Add(cell.ToString()); cell.Clear(); rows.Add(row.ToArray()); row.Clear(); }
            else cell.Append(c);
        }
        row.Add(cell.ToString());
        if (row.Count > 1 || row[0].Length > 0) rows.Add(row.ToArray());
        return rows;
    }

    /// <summary>Đọc sheet đầu tiên của .xlsx (Open XML) bằng System.IO.Compression – không cần thư viện ngoài.</summary>
    public static List<string[]> ReadXlsxRows(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        XNamespace m = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace pr = "http://schemas.openxmlformats.org/package/2006/relationships";
        XDocument Doc(string name)
        {
            var en = zip.GetEntry(name) ?? zip.Entries.FirstOrDefault(x => string.Equals(x.FullName, name, StringComparison.OrdinalIgnoreCase));
            if (en == null) return null;
            using var s = en.Open();
            return XDocument.Load(s);
        }

        // sheet đầu tiên theo workbook.xml
        string sheetPath = "xl/worksheets/sheet1.xml";
        var wb = Doc("xl/workbook.xml");
        var rels = Doc("xl/_rels/workbook.xml.rels");
        var firstSheet = wb?.Root?.Element(m + "sheets")?.Elements(m + "sheet").FirstOrDefault();
        var rid = (string)firstSheet?.Attribute(r + "id");
        if (rid != null && rels != null)
        {
            var target = (string)rels.Root.Elements(pr + "Relationship").FirstOrDefault(x => (string)x.Attribute("Id") == rid)?.Attribute("Target");
            if (!string.IsNullOrEmpty(target))
                sheetPath = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
        }

        var shared = new List<string>();
        var ss = Doc("xl/sharedStrings.xml");
        if (ss != null)
            foreach (var si in ss.Root.Elements(m + "si"))
                shared.Add(string.Concat(si.Descendants(m + "t").Where(t => t.Parent?.Name != m + "rPh").Select(t => t.Value)));

        var sheet = Doc(sheetPath) ?? throw new InvalidDataException("Không đọc được sheet đầu tiên trong file Excel.");
        var rows = new List<string[]>();
        var data = sheet.Root.Element(m + "sheetData");
        if (data == null) return rows;
        int lastRow = 0;
        foreach (var row in data.Elements(m + "row"))
        {
            int rn = int.TryParse((string)row.Attribute("r"), out int rv) ? rv : lastRow + 1;
            while (lastRow + 1 < rn) { rows.Add(Array.Empty<string>()); lastRow++; }
            lastRow = rn;
            var cells = new SortedDictionary<int, string>();
            int nextCol = 0;
            foreach (var c in row.Elements(m + "c"))
            {
                int col = ColIndex((string)c.Attribute("r"));
                if (col < 0) col = nextCol;
                nextCol = col + 1;
                string t = (string)c.Attribute("t");
                string v = c.Element(m + "v")?.Value;
                string val = t switch
                {
                    "s" => int.TryParse(v, out int si) && si >= 0 && si < shared.Count ? shared[si] : "",
                    "inlineStr" => string.Concat(c.Element(m + "is")?.Descendants(m + "t").Select(x => x.Value) ?? Enumerable.Empty<string>()),
                    "b" => v == "1" ? "TRUE" : "FALSE",
                    _ => v ?? ""
                };
                cells[col] = val;
            }
            int n = cells.Count == 0 ? 0 : cells.Keys.Max() + 1;
            var arr = new string[n];
            for (int i = 0; i < n; i++) arr[i] = cells.TryGetValue(i, out var s) ? s : "";
            rows.Add(arr);
        }
        return rows;
    }

    private static int ColIndex(string cellRef)
    {
        if (string.IsNullOrEmpty(cellRef)) return -1;
        int col = 0, i = 0;
        while (i < cellRef.Length && char.IsLetter(cellRef[i])) { col = col * 26 + (char.ToUpperInvariant(cellRef[i]) - 'A' + 1); i++; }
        return i == 0 ? -1 : col - 1;
    }

    private enum Col { None, Province, FormerProvince, District, Commune, Name, Zone, W0, AgR, AgRms2, Ground, Source, Note }

    private static Col Classify(string header)
    {
        var h = " " + Norm(header) + " ";
        if (h.Trim().Length == 0) return Col.None;
        var hx = h.Replace(" THI XA ", " THIXA "); // để " XA " chỉ là "xã"
        if (h.Contains(" GHI CHU ")) return Col.Note;
        if (h.Contains(" NGUON ")) return Col.Source;
        if (h.Contains(" AGR ") || h.Contains(" GIA TOC ") || h.Contains(" AG ") || h.Contains(" A GR "))
            return h.Contains(" M S") ? Col.AgRms2 : Col.AgR;
        if (h.Contains(" VUNG GIO ") || h.Contains(" VUNG AP LUC ") || h.Trim() == "VUNG" || h.Contains(" PHAN VUNG ")) return Col.Zone;
        if (h.Contains(" W0 ") || h.Contains(" WO ") || h.Contains(" AP LUC GIO ")) return Col.W0;
        if (h.Contains(" LOAI NEN ") || h.Contains(" NEN DAT ") || h.Trim() == "NEN") return Col.Ground;
        bool tinh = h.Contains(" TINH ") || h.Trim() == "TINH THANH PHO" || h.Trim() == "THANH PHO";
        bool old = h.Contains(" CU ") || h.Contains(" TRUOC ");
        if (tinh && old) return Col.FormerProvince;
        if (h.Contains(" DIA DANH ") || (hx.Contains(" XA ") && h.Contains(" HUYEN "))) return Col.Name;
        if (tinh) return Col.Province;
        if (h.Contains(" HUYEN ") || h.Contains(" QUAN ") || h.Contains(" THI XA ")) return old || !hx.Contains(" XA ") ? Col.District : Col.Name;
        if (hx.Contains(" XA ") || h.Contains(" PHUONG ") || h.Contains(" THI TRAN ") || h.Contains(" DAC KHU ")) return Col.Commune;
        if (h.Trim() == "TEN" || h.Contains(" TEN DIA ")) return Col.Name;
        return Col.None;
    }

    private static double ParseNum(string s)
    {
        s = (s ?? "").Trim().Replace(" ", "");
        if (s.Length == 0) return 0;
        if (s.Contains(',') && !s.Contains('.')) s = s.Replace(',', '.');
        else if (s.Contains(',') && s.Contains('.')) s = s.Replace(",", "");
        var mt = Regex.Match(s, @"-?\d+(\.\d+)?");
        return mt.Success && double.TryParse(mt.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;
    }

    /// <summary>Gộp các dòng bảng (dòng tiêu đề tự nhận dạng) vào CSDL.</summary>
    public LocationImportResult ImportRows(List<string[]> rows, string defaultSource = SourceQcvn)
    {
        var res = new LocationImportResult();
        // dòng tiêu đề: trong 15 dòng đầu, dòng có ≥ 2 cột nhận dạng được và có cột tên
        int hdr = -1; Col[] map = null;
        for (int i = 0; i < Math.Min(rows.Count, 15) && hdr < 0; i++)
        {
            var cls = rows[i].Select(Classify).ToArray();
            bool hasName = cls.Any(c => c is Col.Name or Col.Commune or Col.District);
            bool hasValue = cls.Any(c => c is Col.Zone or Col.W0 or Col.AgR or Col.AgRms2 or Col.Province);
            if (hasName && hasValue && cls.Count(c => c != Col.None) >= 2) { hdr = i; map = cls; }
        }
        if (hdr < 0)
            throw new InvalidDataException("Không tìm thấy dòng tiêu đề. Bảng cần các cột như: Tỉnh/Thành phố; Tỉnh cũ; Địa danh (xã, phường, huyện); Vùng gió; W0; agR; Loại nền; Ghi chú (dùng 'Xuất CSV mẫu' để có file đúng mẫu).");

        // chỉ dùng cột đầu tiên cho mỗi loại
        var seen = new HashSet<Col>();
        for (int j = 0; j < map.Length; j++) { if (map[j] == Col.None) continue; if (!seen.Add(map[j])) map[j] = Col.None; }

        string curProv = "", curFormer = "", curDistrict = "";
        bool hasCommune = map.Contains(Col.Commune) || map.Contains(Col.Name);
        for (int i = hdr + 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(c => string.IsNullOrWhiteSpace(c))) continue;
            if (row.Any(c => { var nc = Norm(c); return nc.StartsWith("VI DU") || nc.Contains("VI DU XOA"); })) { res.Skipped++; continue; }
            string Get(Col c) { int j = Array.IndexOf(map, c); return j >= 0 && j < row.Length ? (row[j] ?? "").Trim() : ""; }

            string prov = Get(Col.Province), former = Get(Col.FormerProvince), district = Get(Col.District);
            string commune = Get(Col.Commune); if (commune.Length == 0) commune = Get(Col.Name);
            // ô gộp (merged cell) để trống → lấy theo dòng trên
            if (prov.Length > 0) { curProv = prov; if (former.Length == 0) curFormer = ""; if (district.Length == 0) curDistrict = ""; }
            if (former.Length > 0) { curFormer = former; if (district.Length == 0) curDistrict = ""; }
            if (district.Length > 0) curDistrict = district;
            prov = curProv; former = curFormer; district = curDistrict;

            string name = commune;
            if (name.Length == 0 && !hasCommune) { name = district; district = ""; }
            else if (name.Length == 0 && Get(Col.District).Length > 0) { name = district; district = ""; }
            if (name.Length == 0) { res.Skipped++; continue; }

            string zoneRaw = Get(Col.Zone);
            string zone = WindZones.Normalize(zoneRaw);
            double w0 = ParseNum(Get(Col.W0));
            if (w0 > 10) w0 /= 100; // daN/m² → kN/m²
            if (zone.Length == 0 && w0 > 0) zone = WindZones.FromW0(w0)?.Zone ?? "";
            if (zoneRaw.Length > 0 && zone.Length == 0)
                res.Messages.Add($"Dòng {i + 1}: vùng gió '{zoneRaw}' không thuộc I…V – giữ W0 nếu có, cần kiểm tra lại.");
            if (zone.Length > 0 && w0 > 0 && Math.Abs(WindZones.W0(zone) - w0) < 0.005) w0 = 0; // trùng bảng vùng → không lưu riêng
            double agr = ParseNum(Get(Col.AgR));
            double agms = ParseNum(Get(Col.AgRms2));
            if (agr == 0 && agms > 0) agr = agms / 9.81;
            else if (agr > 1.5) { agr /= 9.81; res.Messages.Add($"Dòng {i + 1}: agR > 1,5 được hiểu là m/s² và đổi sang g."); }
            string ground = Get(Col.Ground).ToUpperInvariant();
            if (ground.Length > 1 || (ground.Length == 1 && "ABCDE".IndexOf(ground[0]) < 0)) ground = "";

            if (zone.Length == 0 && w0 <= 0 && agr <= 0) { res.Skipped++; res.Messages.Add($"Dòng {i + 1} ({name}): không có vùng gió/W0/agR – bỏ qua."); continue; }

            // tỉnh: nhận cả tên tỉnh cũ
            var pinfo = FindProvince(prov) ?? FindProvince(former);
            // tỉnh cũ: theo cột "Tỉnh cũ"; nếu không có, chỉ suy ra khi cột tỉnh ghi tên một tỉnh cũ khác tên tỉnh mới
            string formerName = former.Length > 0 ? (FindFormerProvinceName(former) ?? former)
                : FindFormerProvinceName(prov) is string fp && pinfo != null && ProvinceKey(fp) != ProvinceKey(pinfo.Name) ? fp : "";
            if (pinfo != null && !pinfo.Merged && ProvinceKey(formerName) == ProvinceKey(pinfo.Name)) formerName = "";
            if (pinfo == null) res.Messages.Add($"Dòng {i + 1} ({name}): không nhận ra tỉnh '{prov}{(former.Length > 0 ? " / " + former : "")}'.");

            var e = new LocationEntry
            {
                Province = pinfo?.Name ?? prov,
                FormerProvince = formerName,
                FormerDistrict = district,
                Name = name,
                WindZone = zone,
                W0 = w0,
                AgR = Math.Round(agr, 4),
                GroundType = ground,
                Source = Get(Col.Source) is { Length: > 0 } src ? src : defaultSource,
                Note = Get(Col.Note),
                Updated = DateTime.Now
            };
            if (Upsert(e)) res.Added++; else res.Updated++;
            res.Entries.Add(e);
        }
        return res;
    }
}
