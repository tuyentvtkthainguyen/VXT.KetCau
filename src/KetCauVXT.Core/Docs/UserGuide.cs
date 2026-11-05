using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KetCauVXT.Report;

namespace KetCauVXT.Docs;

// =====================================================================
// HƯỚNG DẪN SỬ DỤNG VIỆT CONST – nội dung dạng dữ liệu có cấu trúc (chương / mục / đoạn / gạch đầu dòng / bước / bảng / ghi chú)
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
    public const string DocTitle = "HƯỚNG DẪN SỬ DỤNG PHẦN MỀM VIỆT CONST";
    public const string FileBaseName = "HuongDanSuDung";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ==================================================================
    // BẢNG ĐƯỜNG DẪN MENU – sửa tại đây khi menu được sắp xếp lại (một chỗ duy nhất). Theo bố cục kiểu ETABS của MainWindow.xaml:
    // Tệp, Sửa, Xem, Khai báo, Vẽ, Chọn, Gán, Phân tích, Hiển thị, Thiết kế, Triển khai hồ sơ, Tuỳ chọn, Trợ giúp.
    // Planned("id", …): menu của tính năng chưa có trong MainWindow.xaml – kiểm thử UserGuideTests chỉ liệt kê, không coi là thiếu.
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
        M("cadLinks", "Tệp", "Liên kết bản vẽ AutoCAD (DWG/DXF)…"),
        M("detailingMode", "Tệp", "Chuyển sang chế độ Triển khai hồ sơ…"),
        M("startMode", "Tệp", "Chọn chế độ làm việc khi khởi động…"),
        M("e2k", "Tệp", "Xuất mô hình", "Mô hình ETABS .e2k..."),
        M("s2k", "Tệp", "Xuất mô hình", "Mô hình SAP2000 .s2k..."),
        M("csv", "Tệp", "Xuất mô hình", "Nội lực CSV..."),
        M("exit", "Tệp", "Thoát"),
        // Sửa
        M("undo", "Sửa", "Hoàn tác"),
        M("deleteSel", "Sửa", "Xoá đối tượng đang chọn"),
        M("replicate", "Sửa", "Sao chép / Đối xứng dầm đã chọn…"),
        M("split", "Sửa", "Chia thanh tại nút"),
        M("mergeNodes", "Sửa", "Gộp nút trùng"),
        // Xem
        M("explorer", "Xem", "Cây mô hình"),
        M("viewPlan", "Xem", "Mặt bằng"),
        M("view3d", "Xem", "Khung nhìn 3D"),
        M("viewFnd", "Xem", "Mô hình móng"),
        M("fit", "Xem", "Vừa màn hình"),
        M("labels", "Xem", "Tên cấu kiện"),
        // Khai báo
        M("materials", "Khai báo", "Vật liệu…"),
        M("sections", "Khai báo", "Tiết diện dầm/cột…"),
        M("wallTypes", "Khai báo", "Loại tường…"),
        M("buildUps", "Khai báo", "Cấu tạo sàn…"),
        M("loadPatterns", "Khai báo", "Trường hợp tải (Load Patterns)…"),
        M("loadCases", "Khai báo", "Trường hợp phân tích (Load Cases)…"),
        M("comboEditor", "Khai báo", "Tổ hợp tải trọng…"),
        M("combos", "Khai báo", "Tự tạo tổ hợp theo TCVN 2737:2023"),
        M("modal", "Khai báo", "Nguồn khối lượng & dao động riêng…"),
        M("spectrum", "Khai báo", "Phổ phản ứng / động đất…"),
        M("diaphragms", "Khai báo", "Sàn cứng (diaphragm) theo tầng…"),
        // Vẽ
        M("buildGrid", "Vẽ", "Tạo khung BTCT từ lưới trục"),
        M("portal", "Vẽ", "Tạo khung thép tiền chế..."),
        M("drawBeam", "Vẽ", "Dầm (2 điểm)"),
        M("drawCurved", "Vẽ", "Dầm cong"),
        M("drawQuick", "Vẽ", "Dầm nhanh theo lưới"),
        M("drawSecondary", "Vẽ", "Dầm phụ"),
        M("drawCantilever", "Vẽ", "Dầm công xôn"),
        M("beamByPos", "Vẽ", "Dầm theo toạ độ / trục…"),
        M("stretchBeam", "Vẽ", "Kéo đầu dầm / kéo dầm"),
        M("moveBeam", "Vẽ", "Dời dầm song song"),
        M("trimBeam", "Vẽ", "Trim dầm (cắt tại dầm giao)"),
        M("extendBeam", "Vẽ", "Extend dầm (kéo dài tới dầm chọn / vuông góc)"),
        M("joinBeams", "Vẽ", "Nối dầm hở vào dầm vuông góc gần nhất"),
        M("pickCad", "Vẽ", "Pick đối tượng CAD: dầm / sàn / thang / lỗ"),
        M("drawSlab", "Vẽ", "Ô sàn"),
        M("drawBalcony", "Vẽ", "Ban công"),
        M("drawStair", "Vẽ", "Ô cầu thang"),
        M("drawVoid", "Vẽ", "Giếng trời"),
        M("stairDecl", "Vẽ", "Khai báo cầu thang…"),
        M("drawWall", "Vẽ", "Tường"),
        M("walls", "Vẽ", "Tường đã khai báo…"),
        // Chọn
        M("selOne", "Chọn", "Chọn từng đối tượng"),
        M("selMulti", "Chọn", "Chọn nhiều (bấm / kéo khung)"),
        M("selAll", "Chọn", "Mọi thanh tầng đang xem"),
        M("selBySection", "Chọn", "Theo tiết diện…"),
        M("selClear", "Chọn", "Bỏ chọn"),
        // Gán
        M("assignFrameLoad", "Gán", "Tải trên thanh (đều / hình thang / tập trung)…"),
        M("assignAreaLoad", "Gán", "Tải trên sàn…"),
        M("assignJointLoad", "Gán", "Tải tại nút…"),
        M("assignRestraint", "Gán", "Liên kết nút (ngàm / khớp / tự do)…"),
        M("assignRelease", "Gán", "Giải phóng đầu thanh…"),
        M("assignSection", "Gán", "Tiết diện thanh…"),
        M("assignModifiers", "Gán", "Hệ số độ cứng thanh…"),
        M("insertion", "Gán", "Điểm chèn / dời ngang…"),
        M("assignDiaphragm", "Gán", "Sàn cứng tầng đang xem…"),
        M("assignWall", "Gán", "Gán tường lên dầm"),
        M("assignBuildUp", "Gán", "Gán cấu tạo sàn"),
        M("assignWc", "Gán", "Sàn vệ sinh"),
        // Phân tích
        M("modelCheck", "Phân tích", "Kiểm tra mô hình…"),
        M("run", "Phân tích", "Chạy phân tích"),
        M("importResults", "Phân tích", "Nhập kết quả nội lực (ETABS/SAP/SAFE/khác)…"),
        M("crossCheck", "Phân tích", "So sánh với ETABS/SAP/SAFE…"),
        // Hiển thị
        M("deformed", "Hiển thị", "Hình biến dạng…"),
        M("diagrams", "Hiển thị", "Biểu đồ nội lực"),
        M("modes", "Hiển thị", "Dạng dao động"),
        M("reactions", "Hiển thị", "Phản lực gối…"),
        M("resultTables", "Hiển thị", "Bảng kết quả…"),
        M("showLoads", "Hiển thị", "Hiển thị tải gán"),
        // Thiết kế
        M("designRc", "Thiết kế", "Thiết kế BTCT"),
        M("checkSteel", "Thiết kế", "Kiểm tra thép"),
        M("autoResize", "Thiết kế", "Tự tăng tiết diện đến khi đạt"),
        M("designFnd", "Thiết kế", "Thiết kế móng…"),
        M("rebarChoice", "Thiết kế", "Chọn bố trí thép…"),
        M("rebarPrefs", "Thiết kế", "Tuỳ chọn chọn thép & gia công…"),
        // Triển khai hồ sơ
        M("expDxf", "Triển khai hồ sơ", "Bản vẽ DXF..."),
        M("drawEdit", "Triển khai hồ sơ", "Hiệu chỉnh bản vẽ…"),
        M("drawAttach", "Triển khai hồ sơ", "Nạp lại bản vẽ đã chỉnh sửa (DXF/DWG)…"),
        M("shop", "Triển khai hồ sơ", "Xuất shop drawing thép…"),
        M("sheets", "Triển khai hồ sơ", "Dàn trang & in bản vẽ…"),
        M("expWord", "Triển khai hồ sơ", "Thuyết minh Word..."),
        M("autoReport", "Triển khai hồ sơ", "Lập thuyết minh tự động…"),
        M("expExcel", "Triển khai hồ sơ", "Bảng tính Excel..."),
        M("quantities", "Triển khai hồ sơ", "Bảng khối lượng vật tư…"),
        M("expAll", "Triển khai hồ sơ", "Xuất tất cả vào thư mục..."),
        // Tuỳ chọn
        M("cadStd", "Tuỳ chọn", "Chuẩn layer CAD…"),
        M("drawStyle", "Tuỳ chọn", "Thiết lập bản vẽ (tỷ lệ, cỡ chữ)…"),
        M("templates", "Tuỳ chọn", "Mẫu định dạng (template)…"),
        M("locations", "Tuỳ chọn", "Danh mục địa danh (QCVN 02:2022)…"),
        // Trợ giúp
        M("guide", "Trợ giúp", "Hướng dẫn sử dụng…"),
        M("quickStart", "Trợ giúp", "Bắt đầu nhanh…"),
        M("appendix", "Trợ giúp", "Phụ lục tính toán & hướng dẫn…"),
        M("regs", "Trợ giúp", "Văn bản pháp lý & tiêu chuẩn áp dụng…"),
        M("verify", "Trợ giúp", "Kiểm chứng phần mềm…"),
        M("about", "Trợ giúp", "Giới thiệu"),
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
        var d = new DocxWriter { UpdateFieldsOnOpen = true, FooterText = "Hướng dẫn sử dụng Việt Const", FooterSkipFirstPage = true };
        // ---- trang bìa ----
        for (int i = 0; i < 5; i++) d.EmptyLine();
        d.Title("HƯỚNG DẪN SỬ DỤNG", 24);
        d.Title("PHẦN MỀM VIỆT CONST", 20);
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
        sb.AppendLine("# Hướng dẫn sử dụng phần mềm Việt Const").AppendLine();
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
