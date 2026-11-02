using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using KetCauVXT.Design;
using KetCauVXT.Drawing;
using KetCauVXT.Report;

namespace KetCauVXT.Model;

/// <summary>Các phần có thể có trong một gói mẫu định dạng.</summary>
[Flags]
public enum TemplateParts
{
    None = 0,
    /// <summary>Chuẩn CAD: layer, font, kiểu bản vẽ (CadStandard – lưu nguyên khối)</summary>
    CadStandard = 1,
    /// <summary>Khung tên người dùng (TitleBlockDef)</summary>
    TitleBlockDef = 2,
    /// <summary>Tờ mẫu (khổ giấy, lề, khung, chữ/logo/bảng, ô khung nhìn)</summary>
    SheetTemplates = 4,
    /// <summary>Thông tin khung tên mặc định (đơn vị thiết kế, người ký, logo, tiền tố số hiệu)</summary>
    TitleBlockInfo = 8,
    /// <summary>Tuỳ chọn thuyết minh (chương, trang bìa)</summary>
    Report = 16,
    /// <summary>Định mức, đơn giá khối lượng</summary>
    Quantity = 32,
    /// <summary>Tuỳ chọn bố trí thép</summary>
    Rebar = 64,
    All = 127
}

/// <summary>Cách áp dụng mẫu vào dự án.</summary>
public enum TemplateApplyMode
{
    /// <summary>Gộp: giá trị khác rỗng của mẫu ghi đè, giữ phần dự án mẫu không có; tờ mẫu thêm vào cuối.</summary>
    Merge,
    /// <summary>Thay thế: phần có trong mẫu thay toàn bộ phần tương ứng của dự án (tờ mẫu thay toàn bộ danh sách tờ).</summary>
    Replace
}

/// <summary>Tờ mẫu: bố cục một tờ (khổ, lề, khung, đối tượng chú thích) + ô khung nhìn (loại, vị trí, tỷ lệ; tham số để trống).</summary>
public class SheetTemplate
{
    public string Name { get; set; } = "Tờ mẫu";
    public string Description { get; set; } = "";
    public Sheet Layout { get; set; } = new();
}

/// <summary>Gói mẫu định dạng chuẩn (*.kcvxt-mau): JSON (hoặc zip chứa template.json). Mọi phần đều tuỳ chọn (null = không có).</summary>
public class TemplatePackage
{
    public const string FormatId = "KetCauVXT.MauDinhDang";
    public const int CurrentSchema = 1;

    public string Format { get; set; } = FormatId;
    public int SchemaVersion { get; set; } = CurrentSchema;
    public string Name { get; set; } = "Mẫu định dạng";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    /// <summary>Ngày tạo / sửa (yyyy-MM-dd HH:mm)</summary>
    public string Created { get; set; } = "";
    public string Modified { get; set; } = "";

    public CadStandard CadStandard { get; set; }
    public TitleBlockDef TitleBlockDef { get; set; }
    public List<SheetTemplate> SheetTemplates { get; set; }
    public TitleBlockInfo TitleBlock { get; set; }
    public ReportOptions Report { get; set; }
    public QuantitySettings Quantity { get; set; }
    public RebarPreferences Rebar { get; set; }

    /// <summary>Đường dẫn file đã mở (không lưu)</summary>
    [JsonIgnore] public string FilePath { get; set; }

    /// <summary>Các phần đang có trong gói.</summary>
    [JsonIgnore]
    public TemplateParts Parts =>
        (CadStandard != null ? TemplateParts.CadStandard : 0)
        | (TitleBlockDef != null ? TemplateParts.TitleBlockDef : 0)
        | (SheetTemplates != null && SheetTemplates.Count > 0 ? TemplateParts.SheetTemplates : 0)
        | (TitleBlock != null ? TemplateParts.TitleBlockInfo : 0)
        | (Report != null ? TemplateParts.Report : 0)
        | (Quantity != null ? TemplateParts.Quantity : 0)
        | (Rebar != null ? TemplateParts.Rebar : 0);

    /// <summary>Bản sao chỉ giữ các phần đã chọn.</summary>
    public TemplatePackage Subset(TemplateParts parts)
    {
        var c = TemplateService.FromJson(TemplateService.ToJson(this));
        c.FilePath = FilePath;
        if (!parts.HasFlag(TemplateParts.CadStandard)) c.CadStandard = null;
        if (!parts.HasFlag(TemplateParts.TitleBlockDef)) c.TitleBlockDef = null;
        if (!parts.HasFlag(TemplateParts.SheetTemplates)) c.SheetTemplates = null;
        if (!parts.HasFlag(TemplateParts.TitleBlockInfo)) c.TitleBlock = null;
        if (!parts.HasFlag(TemplateParts.Report)) c.Report = null;
        if (!parts.HasFlag(TemplateParts.Quantity)) c.Quantity = null;
        if (!parts.HasFlag(TemplateParts.Rebar)) c.Rebar = null;
        return c;
    }
}

/// <summary>Một mục trong thư viện mẫu của người dùng.</summary>
public sealed class TemplateLibraryEntry
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Modified { get; set; } = "";
    public TemplateParts Parts { get; set; }
    public string Path { get; set; } = "";
    public bool IsDefault { get; set; }
    public int SheetTemplateCount { get; set; }
    public string PartsText => TemplateService.PartsText(Parts);
}

/// <summary>Lưu / mở / áp dụng mẫu định dạng; thư viện mẫu %AppData%\KetCauVXT\Templates; mẫu mặc định cho dự án mới;
/// tờ mẫu (lưu tờ làm mẫu, tạo tờ từ mẫu).</summary>
public static class TemplateService
{
    public const string Extension = ".kcvxt-mau";
    public const string FileFilter = "Mẫu định dạng Kết cấu VXT (*.kcvxt-mau)|*.kcvxt-mau|JSON (*.json)|*.json|Tất cả (*.*)|*.*";
    /// <summary>Tên gói thư viện chứa các tờ mẫu lưu từ cửa sổ dàn trang.</summary>
    public const string SheetLibraryName = "Tờ mẫu";
    /// <summary>Chỗ thay tham số khung nhìn trong tên khung nhìn của tờ mẫu.</summary>
    public const string ParamToken = "{THAM_SO}";
    private const string DefaultFile = "_mac_dinh.txt";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // ==================================================================
    // Thư viện
    // ==================================================================
    /// <summary>Thư mục thư viện mẫu (null = %AppData%\KetCauVXT\Templates). Đặt khác để kiểm thử / tự kiểm tra.</summary>
    public static string LibraryFolderOverride { get; set; }

    public static string LibraryFolder => !string.IsNullOrEmpty(LibraryFolderOverride) ? LibraryFolderOverride
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KetCauVXT", "Templates");

    /// <summary>true: ModelBuilder.NewProject áp dụng mẫu mặc định của người dùng (ứng dụng bật khi khởi động; lõi/kiểm thử mặc định tắt).</summary>
    public static bool AutoApplyDefault { get; set; }

    public static string PartsText(TemplateParts p)
    {
        var l = new List<string>();
        if (p.HasFlag(TemplateParts.CadStandard)) l.Add("chuẩn CAD");
        if (p.HasFlag(TemplateParts.TitleBlockDef)) l.Add("khung tên");
        if (p.HasFlag(TemplateParts.SheetTemplates)) l.Add("tờ mẫu");
        if (p.HasFlag(TemplateParts.TitleBlockInfo)) l.Add("thông tin khung tên");
        if (p.HasFlag(TemplateParts.Report)) l.Add("thuyết minh");
        if (p.HasFlag(TemplateParts.Quantity)) l.Add("định mức");
        if (p.HasFlag(TemplateParts.Rebar)) l.Add("tuỳ chọn thép");
        return l.Count == 0 ? "(trống)" : string.Join(", ", l);
    }

    public static string PartName(TemplateParts p) => p switch
    {
        TemplateParts.CadStandard => "Chuẩn CAD (layer, font, kiểu bản vẽ)",
        TemplateParts.TitleBlockDef => "Khung tên người dùng (DWG/DXF/ảnh)",
        TemplateParts.SheetTemplates => "Tờ mẫu (bố cục tờ, ô khung nhìn)",
        TemplateParts.TitleBlockInfo => "Thông tin khung tên (đơn vị, người ký, logo, tiền tố)",
        TemplateParts.Report => "Tuỳ chọn thuyết minh (chương, trang bìa)",
        TemplateParts.Quantity => "Định mức, đơn giá khối lượng",
        TemplateParts.Rebar => "Tuỳ chọn bố trí thép",
        _ => p.ToString()
    };

    public static readonly TemplateParts[] PartList =
    {
        TemplateParts.CadStandard, TemplateParts.TitleBlockDef, TemplateParts.SheetTemplates, TemplateParts.TitleBlockInfo,
        TemplateParts.Report, TemplateParts.Quantity, TemplateParts.Rebar
    };

    /// <summary>Danh sách mẫu trong thư viện (file *.kcvxt-mau), sắp theo tên. File hỏng bị bỏ qua.</summary>
    public static List<TemplateLibraryEntry> List()
    {
        var res = new List<TemplateLibraryEntry>();
        string dir = LibraryFolder;
        if (!Directory.Exists(dir)) return res;
        string def = DefaultTemplatePath;
        foreach (var f in Directory.GetFiles(dir, "*" + Extension))
        {
            try
            {
                var t = Load(f);
                res.Add(new TemplateLibraryEntry
                {
                    Name = t.Name ?? Path.GetFileNameWithoutExtension(f), Description = t.Description ?? "",
                    Modified = string.IsNullOrEmpty(t.Modified) ? File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm", Inv) : t.Modified,
                    Parts = t.Parts, Path = f, SheetTemplateCount = t.SheetTemplates?.Count ?? 0,
                    IsDefault = def != null && string.Equals(Path.GetFullPath(f), Path.GetFullPath(def), StringComparison.OrdinalIgnoreCase)
                });
            }
            catch (Exception) { /* file hỏng – bỏ qua */ }
        }
        return res.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Tên file an toàn từ tên mẫu.</summary>
    public static string SafeFileName(string name)
    {
        string s = string.IsNullOrWhiteSpace(name) ? "MauDinhDang" : name.Trim();
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Length > 80 ? s[..80] : s;
    }

    private static string UniquePath(string dir, string baseName, string except = null)
    {
        string path = Path.Combine(dir, baseName + Extension);
        for (int k = 2; File.Exists(path) && !string.Equals(path, except, StringComparison.OrdinalIgnoreCase); k++)
            path = Path.Combine(dir, $"{baseName} ({k}){Extension}");
        return path;
    }

    /// <summary>Lưu gói vào thư viện (tên file theo tên mẫu; trùng thì thêm số). overwrite: ghi đè file cùng tên. Trả đường dẫn.</summary>
    public static string SaveToLibrary(TemplatePackage t, bool overwrite = false)
    {
        if (t == null) throw new ArgumentNullException(nameof(t));
        Directory.CreateDirectory(LibraryFolder);
        string baseName = SafeFileName(t.Name);
        string path = overwrite ? Path.Combine(LibraryFolder, baseName + Extension) : UniquePath(LibraryFolder, baseName);
        Save(path, t);
        return path;
    }

    /// <summary>Nhập một file mẫu vào thư viện (kiểm tra đọc được). Trả đường dẫn trong thư viện.</summary>
    public static string Import(string file)
    {
        var t = Load(file);
        if (string.IsNullOrWhiteSpace(t.Name)) t.Name = Path.GetFileNameWithoutExtension(file);
        return SaveToLibrary(t);
    }

    /// <summary>Xuất mẫu trong thư viện ra file khác.</summary>
    public static void Export(string libraryPath, string dest)
    {
        var t = Load(libraryPath);
        Save(dest, t);
    }

    public static bool Delete(string libraryPath)
    {
        try
        {
            if (string.Equals(DefaultTemplatePath, libraryPath, StringComparison.OrdinalIgnoreCase)) SetDefault(null);
            if (!File.Exists(libraryPath)) return false;
            File.Delete(libraryPath);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <summary>Đổi tên mẫu (ghi tên mới vào gói và đổi tên file). Trả đường dẫn mới.</summary>
    public static string Rename(string libraryPath, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) throw new ArgumentException("Tên mẫu trống", nameof(newName));
        var t = Load(libraryPath);
        t.Name = newName.Trim();
        bool wasDefault = string.Equals(DefaultTemplatePath, libraryPath, StringComparison.OrdinalIgnoreCase);
        string dir = Path.GetDirectoryName(Path.GetFullPath(libraryPath)) ?? LibraryFolder;
        string dest = UniquePath(dir, SafeFileName(t.Name), Path.GetFullPath(libraryPath));
        Save(dest, t);
        if (!string.Equals(Path.GetFullPath(dest), Path.GetFullPath(libraryPath), StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(libraryPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        if (wasDefault) SetDefault(dest);
        return dest;
    }

    // ---- mẫu mặc định cho dự án mới ----
    /// <summary>Đường dẫn mẫu mặc định (null = chưa đặt hoặc file không còn).</summary>
    public static string DefaultTemplatePath
    {
        get
        {
            try
            {
                string f = Path.Combine(LibraryFolder, DefaultFile);
                if (!File.Exists(f)) return null;
                string name = File.ReadAllText(f, Encoding.UTF8).Trim();
                if (name.Length == 0) return null;
                string path = Path.IsPathRooted(name) ? name : Path.Combine(LibraryFolder, name);
                return File.Exists(path) ? path : null;
            }
            catch (Exception) { return null; }
        }
    }

    /// <summary>Đặt mẫu mặc định (null = bỏ). File phải nằm trong thư viện (ngoài thư viện thì nhập vào trước).</summary>
    public static string SetDefault(string path)
    {
        Directory.CreateDirectory(LibraryFolder);
        string f = Path.Combine(LibraryFolder, DefaultFile);
        if (string.IsNullOrEmpty(path)) { try { if (File.Exists(f)) File.Delete(f); } catch (IOException) { } return null; }
        string full = Path.GetFullPath(path);
        string lib = Path.GetFullPath(LibraryFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(lib, StringComparison.OrdinalIgnoreCase)) full = Import(path);
        File.WriteAllText(f, Path.GetFileName(full), new UTF8Encoding(false));
        return full;
    }

    public static TemplatePackage LoadDefault()
    {
        string p = DefaultTemplatePath;
        if (p == null) return null;
        try { return Load(p); } catch (Exception) { return null; }
    }

    /// <summary>Áp dụng mẫu mặc định (gộp, không tạo tờ) cho dự án mới. Trả các phần đã áp dụng.</summary>
    public static TemplateParts ApplyDefault(Project p)
    {
        var t = LoadDefault();
        if (t == null || p == null) return TemplateParts.None;
        return ApplyTo(p, t, TemplateParts.All & ~TemplateParts.SheetTemplates, TemplateApplyMode.Merge);
    }

    /// <summary>Điểm cắm trong ModelBuilder.NewProject: áp dụng mẫu mặc định khi <see cref="AutoApplyDefault"/> bật; không bao giờ ném lỗi.</summary>
    public static void OnNewProject(Project p)
    {
        if (!AutoApplyDefault || p == null) return;
        try { ApplyDefault(p); } catch (Exception) { /* mẫu hỏng – bỏ qua */ }
    }

    // ==================================================================
    // JSON, file
    // ==================================================================
    public static string ToJson(TemplatePackage t) => JsonSerializer.Serialize(t, JsonOptions);

    public static TemplatePackage FromJson(string json)
    {
        var t = JsonSerializer.Deserialize<TemplatePackage>(json ?? "", JsonOptions) ?? throw new InvalidDataException("File mẫu định dạng rỗng hoặc lỗi");
        if (!string.IsNullOrEmpty(t.Format) && t.Format != TemplatePackage.FormatId)
            throw new InvalidDataException($"Không phải file mẫu định dạng Kết cấu VXT (Format = '{t.Format}')");
        if (t.CadStandard != null) t.CadStandard = t.CadStandard.Clone();   // chuẩn hoá (Normalize)
        t.TitleBlockDef?.UpdateExtents();
        return t;
    }

    /// <summary>Lưu gói (JSON UTF-8). Cập nhật ngày sửa.</summary>
    public static void Save(string path, TemplatePackage t)
    {
        if (t == null) throw new ArgumentNullException(nameof(t));
        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm", Inv);
        if (string.IsNullOrEmpty(t.Created)) t.Created = now;
        t.Modified = now;
        t.Format = TemplatePackage.FormatId;
        t.SchemaVersion = TemplatePackage.CurrentSchema;
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, ToJson(t), new UTF8Encoding(false));
        t.FilePath = path;
    }

    /// <summary>Lưu chỉ các phần đã chọn.</summary>
    public static void Save(string path, TemplatePackage t, TemplateParts parts) => Save(path, t.Subset(parts));

    /// <summary>Mở gói: JSON, hoặc zip chứa template.json (hoặc file .json đầu tiên).</summary>
    public static TemplatePackage Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        string json;
        if (bytes.Length > 4 && bytes[0] == (byte)'P' && bytes[1] == (byte)'K')
        {
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("template.json") ?? zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidDataException("File zip mẫu không có template.json");
            using var sr = new StreamReader(entry.Open(), Encoding.UTF8);
            json = sr.ReadToEnd();
        }
        else json = Encoding.UTF8.GetString(bytes).TrimStart('﻿');
        var t = FromJson(json);
        t.FilePath = path;
        if (string.IsNullOrWhiteSpace(t.Name)) t.Name = Path.GetFileNameWithoutExtension(path);
        return t;
    }

    private static T Clone<T>(T o) where T : class =>
        o == null ? null : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(o, JsonOptions), JsonOptions);

    // ==================================================================
    // Tạo mẫu từ dự án
    // ==================================================================
    /// <summary>Tạo gói mẫu từ dự án: chỉ các phần đã chọn. Bỏ thông tin riêng của công trình (tên, địa điểm, chủ đầu tư, ngày).
    /// Tờ mẫu: mọi tờ của dự án (ô khung nhìn giữ loại/vị trí/tỷ lệ, bỏ tham số).</summary>
    public static TemplatePackage FromProject(Project p, TemplateParts parts, string name, string description = "")
    {
        if (p == null) throw new ArgumentNullException(nameof(p));
        var t = new TemplatePackage
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Mẫu từ " + p.Name : name.Trim(), Description = description ?? "",
            Author = p.TitleBlock?.Company ?? "", Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm", Inv)
        };
        if (parts.HasFlag(TemplateParts.CadStandard)) t.CadStandard = CadStandard.EffectiveFor(p);
        if (parts.HasFlag(TemplateParts.TitleBlockDef) && p.TitleBlock?.Custom != null) t.TitleBlockDef = Clone(p.TitleBlock.Custom);
        if (parts.HasFlag(TemplateParts.TitleBlockInfo))
        {
            var tb = Clone(p.TitleBlock ?? new TitleBlockInfo());
            tb.ProjectName = ""; tb.Location = ""; tb.Owner = ""; tb.Custom = null;
            t.TitleBlock = tb;
        }
        if (parts.HasFlag(TemplateParts.SheetTemplates) && p.Sheets != null && p.Sheets.Count > 0)
            t.SheetTemplates = p.Sheets.Select(s => SheetTemplateFrom(s, string.IsNullOrWhiteSpace(s.Name) ? s.Number : s.Name)).ToList();
        if (parts.HasFlag(TemplateParts.Report))
        {
            var r = ReportOptions.Clone(p.ReportOptions ?? new ReportOptions());
            r.ProjectName = ""; r.Owner = ""; r.Location = ""; r.Date = "";
            t.Report = r;
        }
        if (parts.HasFlag(TemplateParts.Quantity)) t.Quantity = Clone(p.QuantitySettings ?? new QuantitySettings());
        if (parts.HasFlag(TemplateParts.Rebar)) t.Rebar = Clone(p.RebarPrefs ?? new RebarPreferences());
        return t;
    }

    // ==================================================================
    // Áp dụng
    // ==================================================================
    /// <summary>Áp dụng các phần đã chọn (và có trong mẫu) vào dự án. Trả các phần đã áp dụng.
    /// Gộp: giá trị khác rỗng của mẫu ghi đè (đối tượng gộp sâu theo JSON), thông tin khung tên chỉ ghi ô có giá trị, tờ mẫu thêm tờ vào cuối;
    /// Thay thế: thay nguyên phần (giữ tên/địa điểm/chủ đầu tư của dự án), tờ mẫu thay toàn bộ danh sách tờ.
    /// Tờ tạo từ tờ mẫu: ô khung nhìn được điền tham số đầu tiên phù hợp (DesignSummary d có thể null).</summary>
    public static TemplateParts ApplyTo(Project p, TemplatePackage t, TemplateParts parts = TemplateParts.All, TemplateApplyMode mode = TemplateApplyMode.Merge, DesignSummary d = null)
    {
        if (p == null || t == null) return TemplateParts.None;
        var applied = TemplateParts.None;
        var use = parts & t.Parts;
        bool replace = mode == TemplateApplyMode.Replace;

        if (use.HasFlag(TemplateParts.CadStandard))
        {
            p.CadStandard = replace || p.CadStandard == null ? t.CadStandard.Clone() : MergeObj(p.CadStandard, t.CadStandard).Clone();
            applied |= TemplateParts.CadStandard;
        }
        if (use.HasFlag(TemplateParts.TitleBlockInfo))
        {
            var cur = p.TitleBlock ?? new TitleBlockInfo();
            var src = Clone(t.TitleBlock);
            if (replace)
            {
                src.ProjectName = cur.ProjectName; src.Location = cur.Location; src.Owner = cur.Owner; src.Custom = cur.Custom;
                p.TitleBlock = src;
            }
            else
            {
                static string Pick(string a, string b) => string.IsNullOrWhiteSpace(a) ? b : a;
                cur.Company = Pick(src.Company, cur.Company);
                cur.Designer = Pick(src.Designer, cur.Designer);
                cur.Checker = Pick(src.Checker, cur.Checker);
                cur.Approver = Pick(src.Approver, cur.Approver);
                cur.Leader = Pick(src.Leader, cur.Leader);
                cur.LogoData = Pick(src.LogoData, cur.LogoData);
                cur.NumberPrefix = Pick(src.NumberPrefix, cur.NumberPrefix);
                cur.ProjectName = Pick(cur.ProjectName, src.ProjectName);
                cur.Location = Pick(cur.Location, src.Location);
                cur.Owner = Pick(cur.Owner, src.Owner);
                cur.Extra ??= new Dictionary<string, string>();
                if (src.Extra != null) foreach (var kv in src.Extra) if (!string.IsNullOrWhiteSpace(kv.Value)) cur.Extra[kv.Key] = kv.Value;
                p.TitleBlock = cur;
            }
            applied |= TemplateParts.TitleBlockInfo;
        }
        if (use.HasFlag(TemplateParts.TitleBlockDef))
        {
            p.TitleBlock ??= new TitleBlockInfo();
            p.TitleBlock.Custom = Clone(t.TitleBlockDef);
            applied |= TemplateParts.TitleBlockDef;
        }
        if (use.HasFlag(TemplateParts.Report))
        {
            p.ReportOptions = replace || p.ReportOptions == null ? Clone(t.Report) : MergeObj(p.ReportOptions, t.Report);
            if (replace && p.ReportOptions != null)
            {
                // tên/địa điểm/chủ đầu tư lấy lại theo dự án khi lập thuyết minh (ReportOptions.FromProject)
                p.ReportOptions.ProjectName = ""; p.ReportOptions.Owner = ""; p.ReportOptions.Location = "";
            }
            applied |= TemplateParts.Report;
        }
        if (use.HasFlag(TemplateParts.Quantity))
        {
            p.QuantitySettings = replace || p.QuantitySettings == null ? Clone(t.Quantity) : MergeObj(p.QuantitySettings, t.Quantity);
            applied |= TemplateParts.Quantity;
        }
        if (use.HasFlag(TemplateParts.Rebar))
        {
            p.RebarPrefs = replace || p.RebarPrefs == null ? Clone(t.Rebar) : MergeObj(p.RebarPrefs, t.Rebar);
            applied |= TemplateParts.Rebar;
        }
        if (use.HasFlag(TemplateParts.SheetTemplates))
        {
            p.Sheets ??= new List<Sheet>();
            if (replace) p.Sheets.Clear();
            foreach (var st in t.SheetTemplates)
            {
                var s = SheetFromTemplate(p, st, NextNumber(p), DefaultSlotParams(p, d, st));
                p.Sheets.Add(s);
            }
            applied |= TemplateParts.SheetTemplates;
        }
        return applied;
    }

    /// <summary>Gộp sâu theo JSON: thuộc tính của overlay ghi đè target, trừ null / chuỗi rỗng / mảng rỗng; đối tượng con gộp đệ quy.</summary>
    public static T MergeObj<T>(T target, T overlay) where T : class
    {
        if (overlay == null) return Clone(target);
        if (target == null) return Clone(overlay);
        var a = JsonNode.Parse(JsonSerializer.Serialize(target, JsonOptions)) as JsonObject;
        var b = JsonNode.Parse(JsonSerializer.Serialize(overlay, JsonOptions)) as JsonObject;
        if (a == null || b == null) return Clone(overlay);
        MergeNode(a, b);
        return JsonSerializer.Deserialize<T>(a.ToJsonString(JsonOptions), JsonOptions);
    }

    private static void MergeNode(JsonObject a, JsonObject b)
    {
        foreach (var kv in b.ToList())
        {
            var v = kv.Value;
            if (v == null) continue;
            if (v is JsonValue jv && jv.TryGetValue(out string sv) && string.IsNullOrEmpty(sv)) continue;
            if (v is JsonArray arr && arr.Count == 0) continue;
            if (v is JsonObject vo && a[kv.Key] is JsonObject ao) { MergeNode(ao, vo); continue; }
            a[kv.Key] = v.DeepClone();
        }
    }

    // ==================================================================
    // Tờ mẫu
    // ==================================================================
    /// <summary>Khung nhìn có tham số phụ thuộc mô hình (tên tầng, trục, dầm, cột, móng) – bỏ tham số khi lưu làm mẫu.</summary>
    public static bool IsModelSlot(SheetViewKind k) => k is SheetViewKind.PlanStory or SheetViewKind.SlabRebarPlan or SheetViewKind.Elevation
        or SheetViewKind.ForceDiagram or SheetViewKind.FoundationDetail or SheetViewKind.BeamDetail or SheetViewKind.ColumnDetail;

    /// <summary>Tờ mẫu từ một tờ: giữ khổ, lề, khung, đối tượng chú thích, ô khung nhìn (loại, vị trí, tỷ lệ); tham số phụ thuộc mô hình để trống
    /// (tên khung nhìn chứa tham số → thay bằng {THAM_SO}). keepParams = true: giữ nguyên tham số.</summary>
    public static SheetTemplate SheetTemplateFrom(Sheet s, string name, bool keepParams = false, string description = "")
    {
        var c = SheetBuilder.Clone(s ?? new Sheet());
        c.Number = "";
        c.Date = "";
        if (!keepParams)
            foreach (var v in c.Views)
            {
                if (!IsModelSlot(v.Kind)) continue;
                string par = v.Param ?? "";
                if (par.Length > 0 && !string.IsNullOrEmpty(v.Title))
                {
                    string shown = v.Kind == SheetViewKind.ForceDiagram ? par.Split(';')[0] : par;
                    if (shown.Length > 0) v.Title = v.Title.Replace(shown, ParamToken);
                }
                v.Param = "";
            }
        return new SheetTemplate { Name = string.IsNullOrWhiteSpace(name) ? (s?.Name ?? "Tờ mẫu") : name.Trim(), Description = description ?? "", Layout = c };
    }

    /// <summary>Lưu tờ làm tờ mẫu vào gói thư viện "Tờ mẫu" (tạo mới nếu chưa có; trùng tên tờ mẫu thì thay). Trả đường dẫn gói.</summary>
    public static string AddSheetTemplateToLibrary(Sheet s, string name, string packageName = SheetLibraryName, bool keepParams = false)
    {
        var st = SheetTemplateFrom(s, name, keepParams);
        Directory.CreateDirectory(LibraryFolder);
        string path = Path.Combine(LibraryFolder, SafeFileName(packageName) + Extension);
        TemplatePackage pkg = null;
        if (File.Exists(path)) { try { pkg = Load(path); } catch (Exception) { pkg = null; } }
        pkg ??= new TemplatePackage { Name = packageName, Description = "Tờ mẫu lưu từ cửa sổ dàn trang" };
        pkg.SheetTemplates ??= new List<SheetTemplate>();
        pkg.SheetTemplates.RemoveAll(x => string.Equals(x.Name, st.Name, StringComparison.CurrentCultureIgnoreCase));
        pkg.SheetTemplates.Add(st);
        Save(path, pkg);
        return path;
    }

    /// <summary>Mọi tờ mẫu trong thư viện (gói, tờ mẫu).</summary>
    public static List<(TemplatePackage Package, SheetTemplate Template)> LibrarySheetTemplates()
    {
        var res = new List<(TemplatePackage, SheetTemplate)>();
        foreach (var e in List())
        {
            if (e.SheetTemplateCount == 0) continue;
            try
            {
                var pkg = Load(e.Path);
                foreach (var st in pkg.SheetTemplates ?? new List<SheetTemplate>()) res.Add((pkg, st));
            }
            catch (Exception) { /* bỏ qua */ }
        }
        return res;
    }

    /// <summary>Số hiệu tờ tiếp theo theo tiền tố khung tên.</summary>
    public static string NextNumber(Project p)
    {
        int max = 0;
        foreach (var s in p?.Sheets ?? new List<Sheet>())
        {
            var digits = new string((s.Number ?? "").SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int n)) max = Math.Max(max, n);
        }
        string prefix = string.IsNullOrEmpty(p?.TitleBlock?.NumberPrefix) ? "KC-" : p.TitleBlock.NumberPrefix;
        return prefix + (max + 1).ToString("00", Inv);
    }

    /// <summary>Tham số mặc định cho từng ô khung nhìn: giữ tham số sẵn có, ô trống lấy lần lượt các lựa chọn (không trùng nhau nếu được).</summary>
    public static List<string> DefaultSlotParams(Project p, DesignSummary d, SheetTemplate t)
    {
        var res = new List<string>();
        var used = new Dictionary<SheetViewKind, int>();
        foreach (var v in t?.Layout?.Views ?? new List<SheetView>())
        {
            if (!string.IsNullOrEmpty(v.Param) || !IsModelSlot(v.Kind)) { res.Add(v.Param ?? ""); continue; }
            var opts = SheetBuilder.ParamOptions(p, d, v.Kind).Where(o => !string.IsNullOrEmpty(o)).ToList();
            if (opts.Count == 0) { res.Add(""); continue; }
            used.TryGetValue(v.Kind, out int k);
            res.Add(opts[Math.Min(k, opts.Count - 1)]);
            used[v.Kind] = k + 1;
        }
        return res;
    }

    /// <summary>Tờ mới từ tờ mẫu: số hiệu, ngày, người thiết kế theo dự án; ô khung nhìn nhận tham số slotParams[i] (null/thiếu = giữ của mẫu).</summary>
    public static Sheet SheetFromTemplate(Project p, SheetTemplate t, string number, IList<string> slotParams = null)
    {
        var s = SheetBuilder.Clone(t?.Layout ?? new Sheet());
        s.Number = string.IsNullOrWhiteSpace(number) ? NextNumber(p) : number;
        if (string.IsNullOrWhiteSpace(s.Name)) s.Name = t?.Name ?? "BẢN VẼ MỚI";
        s.Date = DateTime.Today.ToString("dd/MM/yyyy", Inv);
        if (string.IsNullOrWhiteSpace(s.Designer)) s.Designer = p?.TitleBlock?.Designer is { Length: > 0 } dz ? dz : p?.Designer ?? "";
        for (int i = 0; i < s.Views.Count; i++)
        {
            var v = s.Views[i];
            if (slotParams != null && i < slotParams.Count && slotParams[i] != null) v.Param = slotParams[i];
            if (!string.IsNullOrEmpty(v.Title) && v.Title.Contains(ParamToken))
            {
                string shown = v.Kind == SheetViewKind.ForceDiagram ? (v.Param ?? "").Split(';')[0] : v.Param ?? "";
                v.Title = v.Title.Replace(ParamToken, shown).Trim();
            }
        }
        return s;
    }
}
