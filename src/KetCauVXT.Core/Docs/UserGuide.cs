using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KetCauVXT.Report;

namespace KetCauVXT.Docs;

// =====================================================================
// HƯỚNG DẪN SỬ DỤNG KẾT CẤU VXT – nội dung dạng dữ liệu có cấu trúc (chương / mục / đoạn / gạch đầu dòng / bước / bảng / ghi chú)
//   • UserGuide.cs          : mô hình dữ liệu, bảng đường dẫn menu (DUY NHẤT một chỗ – sửa ở đây khi menu thay đổi), xuất Word / Markdown, tìm kiếm
//   • UserGuide.Content.cs  : nội dung các chương
//   • UserGuide.Help.cs     : trợ giúp theo ngữ cảnh (F1 → HelpId) và "Bắt đầu nhanh"
// Đánh dấu trong chữ: **đậm**, {m:id} = đường dẫn menu (bảng Menus), {s:help-id} = tham chiếu "mục n.k (tên mục)".
// =====================================================================

/// <summary>Loại khối nội dung.</summary>
public enum GuideBlockKind { Paragraph, Bullets, Steps, Table, Note, Tip, Warning }

/// <summary>Một khối nội dung của mục hướng dẫn.</summary>
public sealed class GuideBlock
{
    public GuideBlockKind Kind { get; set; }
    public string Text { get; set; } = "";
    public List<string> Items { get; set; } = new();
    public string[] Headers { get; set; } = Array.Empty<string>();
    public List<string[]> Rows { get; set; } = new();

    /// <summary>Toàn bộ chữ (đã giải đánh dấu) – dùng tìm kiếm.</summary>
    public string PlainText => string.Join(" ", new[] { UserGuide.Plain(Text) }
        .Concat(Items.Select(UserGuide.Plain)).Concat(Headers.Select(UserGuide.Plain))
        .Concat(Rows.SelectMany(r => r).Select(UserGuide.Plain)));
}

/// <summary>Chương (Level 1), mục (2), tiểu mục (3) của hướng dẫn. HelpId là khoá ổn định dùng cho trợ giúp theo ngữ cảnh.</summary>
public sealed class GuideSection
{
    public string HelpId { get; set; } = "";
    public string Title { get; set; } = "";
    public int Level { get; set; } = 1;
    /// <summary>Số mục "6.5" (đánh tự động theo thứ tự)</summary>
    public string Number { get; set; } = "";
    /// <summary>Tính năng đang được phát triển song song / sắp có – nội dung cần cập nhật khi tính năng hoàn thành.</summary>
    public bool Pending { get; set; }
    public string PendingNote { get; set; } = "";
    public List<GuideBlock> Blocks { get; } = new();
    public List<GuideSection> Children { get; } = new();
    public GuideSection Parent { get; set; }

    public string FullTitle => (string.IsNullOrEmpty(Number) ? "" : Number + ". ") + Title;
    public GuideSection Chapter => Parent == null ? this : Parent.Chapter;
    public override string ToString() => FullTitle;

    // ---- trình dựng nội dung (gọi nối tiếp) ----
    public GuideSection Sec(string helpId, string title, bool pending = false, string pendingNote = "")
    {
        var s = new GuideSection { HelpId = helpId ?? "", Title = title ?? "", Level = Level + 1, Pending = pending, PendingNote = pendingNote ?? "", Parent = this };
        Children.Add(s);
        return s;
    }
    public GuideSection P(params string[] paragraphs) { foreach (var t in paragraphs) Blocks.Add(new GuideBlock { Kind = GuideBlockKind.Paragraph, Text = t }); return this; }
    public GuideSection Bullets(params string[] items) { Blocks.Add(new GuideBlock { Kind = GuideBlockKind.Bullets, Items = items.ToList() }); return this; }
    public GuideSection Steps(params string[] items) { Blocks.Add(new GuideBlock { Kind = GuideBlockKind.Steps, Items = items.ToList() }); return this; }
    public GuideSection Table(string[] headers, params string[][] rows) { Blocks.Add(new GuideBlock { Kind = GuideBlockKind.Table, Headers = headers, Rows = rows.ToList() }); return this; }
    public GuideSection Note(string t) { Blocks.Add(new GuideBlock { Kind = GuideBlockKind.Note, Text = t }); return this; }
    public GuideSection Tip(string t) { Blocks.Add(new GuideBlock { Kind = GuideBlockKind.Tip, Text = t }); return this; }
    public GuideSection Warn(string t) { Blocks.Add(new GuideBlock { Kind = GuideBlockKind.Warning, Text = t }); return this; }

    /// <summary>Mục này và mọi mục con (duyệt trước).</summary>
    public IEnumerable<GuideSection> Flatten()
    {
        yield return this;
        foreach (var c in Children) foreach (var x in c.Flatten()) yield return x;
    }

    /// <summary>Chữ của riêng mục (tiêu đề + khối, không gồm mục con).</summary>
    public string OwnText => FullTitle + " " + string.Join(" ", Blocks.Select(b => b.PlainText)) + (Pending ? " " + PendingNote : "");
}

/// <summary>Một mục menu được hướng dẫn nhắc tới.</summary>
public sealed class GuideMenu
{
    public string Id { get; set; } = "";
    /// <summary>Các cấp menu đúng như Header trong MainWindow.xaml (bỏ dấu '_' phím tắt, "&amp;amp;" viết là "&amp;").</summary>
    public string[] Path { get; set; } = Array.Empty<string>();
    /// <summary>Tính năng / menu đang phát triển (chưa có trong MainWindow.xaml) – kiểm thử không coi là thiếu.</summary>
    public bool Planned { get; set; }
    public string Display => string.Join(" → ", Path);
}

public static partial class UserGuide
{
    public const string DocTitle = "HƯỚNG DẪN SỬ DỤNG PHẦN MỀM KẾT CẤU VXT";
    public const string FileBaseName = "HuongDanSuDung";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ==================================================================
    // BẢNG ĐƯỜNG DẪN MENU – sửa tại đây khi menu được sắp xếp lại (một chỗ duy nhất).
    // Mục Planned = true: menu của tính năng đang được phát triển song song (cây mô hình, kiểm tra mô hình, bảng kết quả,
    // trình sửa tổ hợp, gán tải / giải phóng liên kết) – đường dẫn là DỰ KIẾN theo bố cục kiểu ETABS, cập nhật khi hoàn thành.
    // ==================================================================
    private static GuideMenu M(string id, params string[] path) => new() { Id = id, Path = path };
    private static GuideMenu Planned(string id, params string[] path) => new() { Id = id, Path = path, Planned = true };

    public static readonly IReadOnlyList<GuideMenu> Menus = new List<GuideMenu>
    {
        // Tệp
        M("new", "Tệp", "Mới"),
        M("open", "Tệp", "Mở .kcvxt..."),
        M("save", "Tệp", "Lưu"),
        M("saveAs", "Tệp", "Lưu thành..."),
        M("importDxf", "Tệp", "Nhập mặt bằng kết cấu DWG/DXF..."),
        M("importArch", "Tệp", "Nhập bản vẽ kiến trúc (triển khai kết cấu)..."),
        M("importRevit", "Tệp", "Nhập từ Revit (JSON)..."),
        M("importCsi", "Tệp", "Nhập mô hình ETABS/SAP (.e2k/.s2k)…"),
        M("acad", "Tệp", "Nhận dữ liệu từ AutoCAD…"),
        M("cadStd", "Tệp", "Chuẩn layer CAD…"),
        M("drawStyle", "Tệp", "Thiết lập bản vẽ (tỷ lệ, cỡ chữ)…"),
        M("templates", "Tệp", "Mẫu định dạng (template)…"),
        M("exit", "Tệp", "Thoát"),
        // Mô hình
        M("buildGrid", "Mô hình", "Tạo khung BTCT từ lưới trục"),
        M("portal", "Mô hình", "Tạo khung thép tiền chế..."),
        M("split", "Mô hình", "Chia thanh tại nút"),
        M("sections", "Mô hình", "Khai báo tiết diện dầm/cột…"),
        M("wallTypes", "Mô hình", "Loại tường…"),
        M("walls", "Mô hình", "Tường đã khai báo…"),
        M("buildUps", "Mô hình", "Cấu tạo sàn…"),
        M("locations", "Mô hình", "Danh mục địa danh (QCVN 02:2022)…"),
        // Phân tích
        M("combos", "Phân tích", "Tạo tổ hợp tải"),
        M("run", "Phân tích", "Chạy phân tích"),
        M("modal", "Phân tích", "Dao động riêng…"),
        M("importResults", "Phân tích", "Nhập kết quả nội lực (ETABS/SAP/SAFE/khác)…"),
        M("crossCheck", "Phân tích", "So sánh với ETABS/SAP/SAFE…"),
        // Thiết kế
        M("designRc", "Thiết kế", "Thiết kế BTCT"),
        M("checkSteel", "Thiết kế", "Kiểm tra thép"),
        M("autoResize", "Thiết kế", "Tự tăng tiết diện đến khi đạt"),
        M("designFnd", "Thiết kế", "Thiết kế móng…"),
        M("rebarChoice", "Thiết kế", "Chọn bố trí thép…"),
        M("rebarPrefs", "Thiết kế", "Tuỳ chọn chọn thép & gia công…"),
        // Xuất
        M("expDxf", "Xuất", "Bản vẽ DXF..."),
        M("drawEdit", "Xuất", "Hiệu chỉnh bản vẽ…"),
        M("drawAttach", "Xuất", "Nạp lại bản vẽ đã chỉnh sửa (DXF/DWG)…"),
        M("expWord", "Xuất", "Thuyết minh Word..."),
        M("expExcel", "Xuất", "Bảng tính Excel..."),
        M("quantities", "Xuất", "Bảng khối lượng vật tư…"),
        M("autoReport", "Xuất", "Lập thuyết minh tự động…"),
        M("shop", "Xuất", "Xuất shop drawing thép…"),
        M("csv", "Xuất", "Nội lực CSV..."),
        M("e2k", "Xuất", "Mô hình ETABS .e2k..."),
        M("s2k", "Xuất", "Mô hình SAP2000 .s2k..."),
        M("expAll", "Xuất", "Xuất tất cả vào thư mục..."),
        M("sheets", "Xuất", "Dàn trang & in bản vẽ…"),
        // Trợ giúp
        M("guide", "Trợ giúp", "Hướng dẫn sử dụng…"),
        M("quickStart", "Trợ giúp", "Bắt đầu nhanh…"),
        M("appendix", "Trợ giúp", "Phụ lục tính toán & hướng dẫn…"),
        M("regs", "Trợ giúp", "Văn bản pháp lý & tiêu chuẩn áp dụng…"),
        M("verify", "Trợ giúp", "Kiểm chứng phần mềm…"),
        M("about", "Trợ giúp", "Giới thiệu"),
        // [SẮP CÓ] tính năng đang phát triển song song – đường dẫn dự kiến, cập nhật khi hoàn thành
        Planned("explorer", "Xem", "Cây mô hình…"),
        Planned("modelCheck", "Phân tích", "Kiểm tra mô hình…"),
        Planned("resultTables", "Hiển thị", "Bảng kết quả…"),
        Planned("comboEditor", "Khai báo", "Tổ hợp tải trọng…"),
        Planned("assignFrameLoad", "Gán", "Tải trọng thanh…"),
        Planned("assignAreaLoad", "Gán", "Tải trọng sàn…"),
        Planned("assignRelease", "Gán", "Giải phóng liên kết đầu thanh…"),
    };

    /// <summary>Đường dẫn menu theo mã ("Tệp → Mới"); mã không có → "[menu ?id]".</summary>
    public static string MenuPath(string id)
    {
        var m = Menus.FirstOrDefault(x => x.Id == id);
        return m == null ? "[menu ?" + id + "]" : m.Display;
    }

    // ==================================================================
    // NỘI DUNG
    // ==================================================================
    private static List<GuideSection> _cache;
    private static readonly object _lock = new();

    /// <summary>Các chương (đã đánh số). Nội dung dựng một lần rồi dùng lại.</summary>
    public static List<GuideSection> Chapters()
    {
        lock (_lock)
        {
            if (_cache != null) return _cache;
            var list = BuildChapters();   // UserGuide.Content.cs
            for (int i = 0; i < list.Count; i++) Number(list[i], (i + 1).ToString(Inv));
            _cache = list;
            return list;
        }
    }

    private static void Number(GuideSection s, string no)
    {
        s.Number = no;
        for (int i = 0; i < s.Children.Count; i++) Number(s.Children[i], no + "." + (i + 1).ToString(Inv));
    }

    public static IEnumerable<GuideSection> AllSections() => Chapters().SelectMany(c => c.Flatten());

    /// <summary>Mục theo HelpId (null nếu không có).</summary>
    public static GuideSection Find(string helpId) =>
        string.IsNullOrWhiteSpace(helpId) ? null : AllSections().FirstOrDefault(s => string.Equals(s.HelpId, helpId.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool Exists(string helpId) => Find(helpId) != null;

    /// <summary>Mọi HelpId của hướng dẫn.</summary>
    public static List<string> HelpIds() => AllSections().Select(s => s.HelpId).Where(h => !string.IsNullOrEmpty(h)).ToList();

    // ==================================================================
    // ĐÁNH DẤU TRONG CHỮ
    // ==================================================================
    private static readonly Regex MarkRx = new(@"\*\*(.+?)\*\*|\{m:([A-Za-z0-9_]+)\}|\{s:([a-z0-9\.\-]+)\}", RegexOptions.Compiled);

    /// <summary>Tách chữ có đánh dấu thành các đoạn (chữ, đậm, nghiêng).</summary>
    public static List<(string Text, bool Bold, bool Italic)> Runs(string text)
    {
        var l = new List<(string, bool, bool)>();
        if (string.IsNullOrEmpty(text)) return l;
        int at = 0;
        foreach (Match m in MarkRx.Matches(text))
        {
            if (m.Index > at) l.Add((text.Substring(at, m.Index - at), false, false));
            if (m.Groups[1].Success) l.Add((m.Groups[1].Value, true, false));
            else if (m.Groups[2].Success) l.Add((MenuPath(m.Groups[2].Value), true, false));
            else l.Add((SectionRef(m.Groups[3].Value), false, true));
            at = m.Index + m.Length;
        }
        if (at < text.Length) l.Add((text.Substring(at), false, false));
        return l;
    }

    /// <summary>Chữ thường (bỏ đánh dấu, thay đường dẫn menu, tham chiếu mục).</summary>
    public static string Plain(string text) => string.Concat(Runs(text).Select(r => r.Text));

    /// <summary>"mục 6.5 (Tên mục)".</summary>
    public static string SectionRef(string helpId)
    {
        var s = _cache == null ? null : Find(helpId);   // khi đang dựng nội dung (chưa đánh số) → chỉ ghi mã
        if (s == null) return _cache == null ? "mục [" + helpId + "]" : "[mục ?" + helpId + "]";
        return (s.Level == 1 ? "chương " : "mục ") + s.Number + " (" + s.Title + ")";
    }

    /// <summary>Mọi mã menu {m:…} được nội dung nhắc tới.</summary>
    public static List<string> UsedMenuIds() => AllTexts().SelectMany(t => MarkRx.Matches(t).Where(m => m.Groups[2].Success).Select(m => m.Groups[2].Value)).Distinct().ToList();

    /// <summary>Mọi mã mục {s:…} được nội dung tham chiếu.</summary>
    public static List<string> UsedSectionRefs() => AllTexts().SelectMany(t => MarkRx.Matches(t).Where(m => m.Groups[3].Success).Select(m => m.Groups[3].Value)).Distinct().ToList();

    private static IEnumerable<string> AllTexts() => AllSections().SelectMany(s =>
        new[] { s.Title, s.PendingNote }.Concat(s.Blocks.SelectMany(b => new[] { b.Text }.Concat(b.Items).Concat(b.Headers).Concat(b.Rows.SelectMany(r => r)))));

    // ==================================================================
    // TÌM KIẾM (không dấu)
    // ==================================================================
    /// <summary>Các mục có chứa mọi từ của câu tìm (không phân biệt dấu, hoa thường).</summary>
    public static List<GuideSection> Search(string query)
    {
        var words = Appendix.Normalize(query ?? "").Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return new List<GuideSection>();
        return AllSections().Where(s =>
        {
            string t = Appendix.Normalize(s.OwnText);
            return words.All(w => t.Contains(w, StringComparison.Ordinal));
        }).ToList();
    }

    /// <summary>Khối có chứa mọi từ tìm (để tô sáng).</summary>
    public static bool BlockMatches(GuideBlock b, string query)
    {
        var words = Appendix.Normalize(query ?? "").Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return false;
        string t = Appendix.Normalize(b.PlainText);
        return words.All(w => t.Contains(w, StringComparison.Ordinal));
    }

    private static string BlockLabel(GuideBlockKind k) => k switch
    {
        GuideBlockKind.Note => "Lưu ý: ",
        GuideBlockKind.Tip => "Mẹo: ",
        GuideBlockKind.Warning => "Cảnh báo: ",
        _ => ""
    };

    private const string PendingLabel = "[SẮP CÓ – cập nhật sau] ";

    // ==================================================================
    // WORD (.docx)
    // ==================================================================
    /// <summary>Ghi hướng dẫn ra file Word: trang bìa, mục lục (trường TOC), số trang ở chân trang.</summary>
    public static void SaveDocx(string path)
    {
        var d = new DocxWriter { UpdateFieldsOnOpen = true, FooterText = "Hướng dẫn sử dụng Kết cấu VXT", FooterSkipFirstPage = true };
        // ---- trang bìa ----
        for (int i = 0; i < 5; i++) d.EmptyLine();
        d.Title("HƯỚNG DẪN SỬ DỤNG", 24);
        d.Title("PHẦN MỀM KẾT CẤU VXT", 20);
        d.Paragraph("Phân tích – thiết kế kết cấu nhà dân dụng dưới 7 tầng (khung BTCT) và khung thép tiền chế", false, true, ParaAlign.Center);
        d.EmptyLine();
        d.Paragraph("Từ cài đặt, khởi tạo dự án, lập mô hình, tải trọng, phân tích, thiết kế đến triển khai hồ sơ và kiểm chứng kết quả", false, false, ParaAlign.Center);
        for (int i = 0; i < 8; i++) d.EmptyLine();
        d.Paragraph("Tác giả phần mềm: Vũ Xuân Tuyến", false, false, ParaAlign.Center);
        d.Paragraph("Bản hướng dẫn lập ngày " + DateTime.Now.ToString("dd/MM/yyyy", Inv) + " – nội dung theo phiên bản chương trình đang chạy", false, true, ParaAlign.Center);
        d.PageBreak();
        d.TableOfContents("MỤC LỤC", 2);
        d.PageBreak();
        bool first = true;
        foreach (var ch in Chapters())
        {
            if (!first) d.PageBreak();
            first = false;
            WriteDocx(d, ch);
        }
        d.Save(path);
    }

    private static (string, bool, bool)[] R(string text) => Runs(text).Select(r => (r.Text, r.Bold, r.Italic)).ToArray();

    private static void WriteDocx(DocxWriter d, GuideSection s)
    {
        d.Heading(s.FullTitle, Math.Min(s.Level, 3));
        if (s.Pending)
            d.RichBlock(0, 0, "FCE4D6", new[] { (PendingLabel, true, false) }.Concat(R(string.IsNullOrWhiteSpace(s.PendingNote) ? "Tính năng đang được hoàn thiện; nội dung mục này sẽ được cập nhật." : s.PendingNote)).ToArray());
        foreach (var b in s.Blocks)
        {
            switch (b.Kind)
            {
                case GuideBlockKind.Paragraph:
                    d.RichParagraph(ParaAlign.Justify, R(b.Text));
                    break;
                case GuideBlockKind.Bullets:
                    foreach (var it in b.Items) d.RichBlock(567, 284, null, new[] { ("–\t", false, false) }.Concat(R(it)).ToArray());
                    break;
                case GuideBlockKind.Steps:
                    for (int i = 0; i < b.Items.Count; i++)
                        d.RichBlock(567, 397, null, new[] { ((i + 1).ToString(Inv) + ".\t", true, false) }.Concat(R(b.Items[i])).ToArray());
                    break;
                case GuideBlockKind.Table:
                    d.Table(b.Headers.Select(Plain).ToArray(), b.Rows.Select(r => r.Select(Plain).ToArray()), null, 10.5);
                    break;
                default:
                    string fill = b.Kind == GuideBlockKind.Warning ? "FCE4D6" : b.Kind == GuideBlockKind.Tip ? "E2EFDA" : "FFF2CC";
                    d.RichBlock(0, 0, fill, new[] { (BlockLabel(b.Kind), true, false) }.Concat(R(b.Text)).ToArray());
                    break;
            }
        }
        foreach (var c in s.Children) WriteDocx(d, c);
    }

    // ==================================================================
    // MARKDOWN
    // ==================================================================
    private static string Md(string text) => string.Concat(Runs(text).Select(r =>
        r.Bold ? "**" + r.Text.Trim() + "**" + (r.Text.EndsWith(" ") ? " " : "") : r.Italic ? "*" + r.Text + "*" : r.Text));

    private static string MdCell(string text) => Md(text).Replace("|", "\\|").Replace("\r", "").Replace("\n", "<br>");

    /// <summary>Hướng dẫn dạng Markdown (README / HuongDanSuDung.md).</summary>
    public static string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Hướng dẫn sử dụng phần mềm Kết cấu VXT").AppendLine();
        sb.AppendLine("*Phân tích – thiết kế kết cấu nhà dân dụng dưới 7 tầng (khung BTCT) và khung thép tiền chế. Tác giả: Vũ Xuân Tuyến.*").AppendLine();
        sb.AppendLine("Trong chương trình: **" + MenuPath("guide") + "** (hoặc phím **F1** – mở đúng mục đang làm).").AppendLine();
        sb.AppendLine("## Mục lục").AppendLine();
        foreach (var ch in Chapters())
        {
            sb.AppendLine($"- {ch.FullTitle}");
            foreach (var s in ch.Children) sb.AppendLine($"  - {s.FullTitle}");
        }
        sb.AppendLine();
        foreach (var ch in Chapters()) WriteMd(sb, ch);
        return sb.ToString();
    }

    private static void WriteMd(StringBuilder sb, GuideSection s)
    {
        sb.Append(new string('#', Math.Min(s.Level + 1, 6))).Append(' ').AppendLine(s.FullTitle).AppendLine();
        if (s.Pending) sb.AppendLine("> **" + PendingLabel.Trim() + "** " + Md(string.IsNullOrWhiteSpace(s.PendingNote) ? "Tính năng đang được hoàn thiện." : s.PendingNote)).AppendLine();
        foreach (var b in s.Blocks)
        {
            switch (b.Kind)
            {
                case GuideBlockKind.Paragraph: sb.AppendLine(Md(b.Text)).AppendLine(); break;
                case GuideBlockKind.Bullets: foreach (var it in b.Items) sb.AppendLine("- " + Md(it)); sb.AppendLine(); break;
                case GuideBlockKind.Steps: for (int i = 0; i < b.Items.Count; i++) sb.AppendLine($"{i + 1}. " + Md(b.Items[i])); sb.AppendLine(); break;
                case GuideBlockKind.Table:
                    sb.AppendLine("| " + string.Join(" | ", b.Headers.Select(MdCell)) + " |");
                    sb.AppendLine("|" + string.Concat(b.Headers.Select(_ => "---|")));
                    foreach (var r in b.Rows) sb.AppendLine("| " + string.Join(" | ", Enumerable.Range(0, b.Headers.Length).Select(i => i < r.Length ? MdCell(r[i]) : "")) + " |");
                    sb.AppendLine();
                    break;
                default: sb.AppendLine("> **" + BlockLabel(b.Kind).Trim() + "** " + Md(b.Text)).AppendLine(); break;
            }
        }
        foreach (var c in s.Children) WriteMd(sb, c);
    }

    /// <summary>Ghi cả .docx và .md vào thư mục (tên HuongDanSuDung.*). Trả về (docx, md).</summary>
    public static (string Docx, string Md) SaveAll(string folder)
    {
        Directory.CreateDirectory(folder);
        string docx = Path.Combine(folder, FileBaseName + ".docx"), md = Path.Combine(folder, FileBaseName + ".md");
        SaveDocx(docx);
        File.WriteAllText(md, ToMarkdown(), new UTF8Encoding(false));
        return (docx, md);
    }
}
