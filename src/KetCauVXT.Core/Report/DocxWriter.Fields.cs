using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace KetCauVXT.Report;

/// <summary>Phần mở rộng DocxWriter cho thuyết minh tự động: mục lục (trường TOC tự cập nhật khi mở),
/// số trang ở chân trang (trường PAGE/NUMPAGES), tên bảng đánh số "Bảng n.k". Mặc định tắt hết → file như cũ.</summary>
public partial class DocxWriter
{
    private const string TocMarker = "<!--KCVXT_TOC-->";

    /// <summary>Ghi &lt;w:updateFields/&gt; vào settings.xml: Word cập nhật mục lục, số trang khi mở file.</summary>
    public bool UpdateFieldsOnOpen { get; set; }
    /// <summary>Chân trang có số trang "… – Trang x/y"; null = không có chân trang.</summary>
    public string FooterText { get; set; }
    /// <summary>Trang đầu (trang bìa) không có chân trang.</summary>
    public bool FooterSkipFirstPage { get; set; } = true;
    /// <summary>Tự chèn tên bảng "Bảng n.k: …" (lấy theo tiêu đề mục gần nhất) trước mỗi bảng.</summary>
    public bool AutoCaptions { get; set; }

    private bool _hasToc;
    private string _tocTitle = "MỤC LỤC";
    private int _tocLevels = 2;
    private int _scanPos;
    private string _capChapter = "";
    private string _capHeading = "";
    private int _capIndex;
    private bool _captionPending;

    // ------------------------------------------------------------------
    /// <summary>Chèn mục lục tại vị trí hiện tại (trường TOC \o "1-levels"). Nội dung tạm là danh sách tiêu đề;
    /// Word tính lại số trang khi mở (UpdateFieldsOnOpen) hoặc khi nhấn F9.</summary>
    public DocxWriter TableOfContents(string title = "MỤC LỤC", int levels = 2)
    {
        _hasToc = true; _tocTitle = title ?? ""; _tocLevels = Math.Clamp(levels, 1, 3);
        _body.Append(TocMarker);
        return this;
    }

    /// <summary>Tên bảng / hình (căn giữa, nghiêng) – không đánh số.</summary>
    public DocxWriter Caption(string text)
    {
        _body.Append("<w:p><w:pPr><w:keepNext/><w:spacing w:before=\"120\" w:after=\"60\"/><w:jc w:val=\"center\"/></w:pPr>")
             .Append(Run(text, true, true, 12)).Append("</w:p>");
        _captionPending = true;
        return this;
    }

    /// <summary>Tên bảng đánh số tự động theo chương: "Bảng n.k. title".</summary>
    public DocxWriter TableCaption(string title)
    {
        ScanHeadings();
        _capIndex++;
        string no = string.IsNullOrEmpty(_capChapter) ? _capIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{_capChapter}.{_capIndex}";
        return Caption($"Bảng {no}. {title}");
    }

    /// <summary>Tên hình đánh số tự động theo chương: "Hình n.k. title" (dùng chung bộ đếm với bảng).</summary>
    public DocxWriter FigureCaption(string title)
    {
        ScanHeadings();
        _capIndex++;
        string no = string.IsNullOrEmpty(_capChapter) ? _capIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{_capChapter}.{_capIndex}";
        Caption($"Hình {no}. {title}");
        _captionPending = false;
        return this;
    }

    private void AutoCaption()
    {
        if (_captionPending) { _captionPending = false; return; }
        ScanHeadings();
        string t = string.IsNullOrWhiteSpace(_capHeading) ? "Bảng số liệu" : _capHeading;
        if (t.StartsWith("Bảng ", StringComparison.OrdinalIgnoreCase) && t.Length > 5) t = char.ToUpperInvariant(t[5]) + t.Substring(6);
        TableCaption(t);
        _captionPending = false;
    }

    private static readonly Regex HeadRx = new("<w:p><w:pPr><w:pStyle w:val=\"Heading(\\d)\"/></w:pPr>(.*?)</w:p>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex TextRx = new("<w:t xml:space=\"preserve\">(.*?)</w:t>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex NumRx = new("^\\s*(\\d+(?:\\.\\d+)*)\\.?\\s+", RegexOptions.Compiled);

    private static string Unescape(string s) => (s ?? "").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");

    /// <summary>Cập nhật chương/mục hiện tại từ phần thân mới ghi thêm kể từ lần quét trước.</summary>
    private void ScanHeadings()
    {
        if (_scanPos >= _body.Length) return;
        string tail = _body.ToString(_scanPos, _body.Length - _scanPos);
        _scanPos = _body.Length;
        foreach (Match m in HeadRx.Matches(tail))
        {
            int lvl = m.Groups[1].Value[0] - '0';
            string text = Unescape(string.Concat(TextRx.Matches(m.Groups[2].Value).Select(x => x.Groups[1].Value))).Trim();
            var nm = NumRx.Match(text);
            string bare = nm.Success ? text.Substring(nm.Length).Trim() : text;
            if (lvl == 1)
            {
                string chap = nm.Success ? nm.Groups[1].Value.Split('.')[0] : "PL";
                if (chap != _capChapter) { _capChapter = chap; _capIndex = 0; }
            }
            _capHeading = bare.Length > 0 ? char.ToUpperInvariant(bare[0]) + (bare.Length > 1 ? LowerIfUpper(bare.Substring(1)) : "") : bare;
        }
    }

    /// <summary>Tiêu đề chương viết HOA → viết thường cho tên bảng.</summary>
    private static string LowerIfUpper(string s) => s.Any(char.IsLower) ? s : s.ToLowerInvariant();

    /// <summary>Danh sách tiêu đề (cấp, chữ) đã ghi – dùng cho mục lục tĩnh và kiểm thử.</summary>
    public List<(int Level, string Text)> Headings()
    {
        var l = new List<(int, string)>();
        foreach (Match m in HeadRx.Matches(_body.ToString()))
        {
            int lvl = m.Groups[1].Value[0] - '0';
            l.Add((lvl, Unescape(string.Concat(TextRx.Matches(m.Groups[2].Value).Select(x => x.Groups[1].Value))).Trim()));
        }
        return l;
    }

    // ------------------------------------------------------------------
    private string BodyXml()
    {
        if (!_hasToc) return _body.ToString();
        string body = _body.ToString();
        int at = body.IndexOf(TocMarker, StringComparison.Ordinal);
        if (at < 0) return body;
        string after = body.Substring(at + TocMarker.Length);
        var heads = new List<(int Level, string Text)>();
        foreach (Match m in HeadRx.Matches(after))
        {
            int lvl = m.Groups[1].Value[0] - '0';
            if (lvl > _tocLevels) continue;
            heads.Add((lvl, string.Concat(TextRx.Matches(m.Groups[2].Value).Select(x => x.Groups[1].Value))));   // giữ dạng đã escape
        }
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(_tocTitle))
            sb.Append("<w:p><w:pPr><w:jc w:val=\"center\"/><w:spacing w:before=\"120\" w:after=\"240\"/></w:pPr>").Append(Run(_tocTitle, true, false, 14)).Append("</w:p>");
        string instr = $" TOC \\o \"1-{_tocLevels}\" \\h \\z \\u ";
        string tabs = $"<w:tabs><w:tab w:val=\"right\" w:leader=\"dot\" w:pos=\"{TextWidthTwips}\"/></w:tabs>";
        string begin = "<w:r><w:fldChar w:fldCharType=\"begin\" w:dirty=\"true\"/></w:r><w:r><w:instrText xml:space=\"preserve\">" + Esc(instr) + "</w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>";
        string end = "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";
        if (heads.Count == 0)
        {
            sb.Append("<w:p><w:pPr>").Append(tabs).Append("</w:pPr>").Append(begin)
              .Append("<w:r><w:t xml:space=\"preserve\">Nhấn F9 để cập nhật mục lục.</w:t></w:r>").Append(end).Append("</w:p>");
        }
        else
        {
            for (int i = 0; i < heads.Count; i++)
            {
                var (lvl, text) = heads[i];
                sb.Append("<w:p><w:pPr>").Append(tabs).Append($"<w:spacing w:before=\"0\" w:after=\"40\"/><w:ind w:left=\"{(lvl - 1) * 284}\"/></w:pPr>");
                if (i == 0) sb.Append(begin);
                sb.Append("<w:r>").Append(lvl == 1 ? "<w:rPr><w:b/><w:bCs/></w:rPr>" : "").Append("<w:t xml:space=\"preserve\">").Append(text).Append("</w:t></w:r>");
                if (i == heads.Count - 1) sb.Append(end);
                sb.Append("</w:p>");
            }
        }
        return body.Substring(0, at) + sb + after;
    }

    private bool HasFooter => FooterText != null;

    private string ContentTypesXml() => !HasFooter ? ContentTypes :
        ContentTypes.Replace("</Types>", "<Override PartName=\"/word/footer1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml\"/></Types>");

    private string DocRelsXml() => !HasFooter ? DocRels :
        DocRels.Replace("</Relationships>", "<Relationship Id=\"rIdFtr1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer\" Target=\"footer1.xml\"/></Relationships>");

    private string SettingsXml() => !UpdateFieldsOnOpen ? Settings :
        Settings.Replace("<w:compat>", "<w:updateFields w:val=\"true\"/><w:compat>");

    private string FooterRefXml() => HasFooter ? "<w:footerReference w:type=\"default\" r:id=\"rIdFtr1\"/>" : "";

    private string TitlePgXml() => HasFooter && FooterSkipFirstPage ? "<w:titlePg/>" : "";

    private void PutExtraParts(ZipArchive zip)
    {
        if (!HasFooter) return;
        static string Fld(string instr, string cached) =>
            "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> " + instr + " </w:instrText></w:r>" +
            "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>" + cached + "</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";
        string rpr = "<w:rPr><w:i/><w:iCs/><w:sz w:val=\"20\"/><w:szCs w:val=\"20\"/></w:rPr>";
        string pre = string.IsNullOrWhiteSpace(FooterText) ? "Trang " : FooterText.Trim() + " – Trang ";
        string xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<w:ftr xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<w:p><w:pPr><w:pBdr><w:top w:val=\"single\" w:sz=\"4\" w:space=\"1\" w:color=\"auto\"/></w:pBdr><w:jc w:val=\"right\"/></w:pPr>" +
            "<w:r>" + rpr + "<w:t xml:space=\"preserve\">" + Esc(pre) + "</w:t></w:r>" +
            Fld("PAGE", "1") + "<w:r>" + rpr + "<w:t xml:space=\"preserve\">/</w:t></w:r>" + Fld("NUMPAGES", "1") +
            "</w:p></w:ftr>";
        Put(zip, "word/footer1.xml", xml);
    }
}
