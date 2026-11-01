using System.IO.Compression;
using System.Security;
using System.Text;

namespace KetCauVXT.Report;

public enum ParaAlign { Left, Center, Right, Justify }

/// <summary>Trình ghi file Word (.docx) tối giản – tự sinh các phần OpenXML, không dùng thư viện ngoài.
/// Khổ A4 đứng, lề trên 2 cm, dưới 2 cm, trái 3 cm, phải 1.5 cm. Font Times New Roman 13 pt.</summary>
public class DocxWriter
{
    private readonly StringBuilder _body = new();
    /// <summary>Bề rộng vùng in: 21 − 3 − 1.5 = 16.5 cm ≈ 9355 twips</summary>
    public const int TextWidthTwips = 9355;

    /// <summary>1 cm = 567 twips</summary>
    private static int Cm(double cm) => (int)Math.Round(cm * 567.0);

    public static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length + 16);
        foreach (char ch in s)
        {
            // bỏ ký tự điều khiển không hợp lệ trong XML 1.0
            if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') continue;
            if (ch == '\uFFFE' || ch == '\uFFFF') continue;
            sb.Append(ch);
        }
        return SecurityElement.Escape(sb.ToString()) ?? "";
    }

    private static string AlignXml(ParaAlign a) => a switch
    {
        ParaAlign.Center => "<w:jc w:val=\"center\"/>",
        ParaAlign.Right => "<w:jc w:val=\"right\"/>",
        ParaAlign.Justify => "<w:jc w:val=\"both\"/>",
        _ => ""
    };

    /// <summary>Đoạn văn bản chạy (run). Xuống dòng trong chuỗi ('\n') được chuyển thành &lt;w:br/&gt;.</summary>
    private static string Run(string text, bool bold, bool italic, double sizePt = 0, string font = null)
    {
        var rpr = new StringBuilder();
        if (font != null) rpr.Append($"<w:rFonts w:ascii=\"{font}\" w:hAnsi=\"{font}\" w:cs=\"{font}\"/>");
        if (bold) rpr.Append("<w:b/><w:bCs/>");
        if (italic) rpr.Append("<w:i/><w:iCs/>");
        if (sizePt > 0) { int hs = (int)Math.Round(sizePt * 2); rpr.Append($"<w:sz w:val=\"{hs}\"/><w:szCs w:val=\"{hs}\"/>"); }
        string rp = rpr.Length > 0 ? "<w:rPr>" + rpr + "</w:rPr>" : "";
        var sb = new StringBuilder();
        var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
        sb.Append("<w:r>").Append(rp);
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append("<w:br/>");
            var parts = lines[i].Split('\t');
            for (int k = 0; k < parts.Length; k++)
            {
                if (k > 0) sb.Append("<w:tab/>");
                sb.Append("<w:t xml:space=\"preserve\">").Append(Esc(parts[k])).Append("</w:t>");
            }
        }
        sb.Append("</w:r>");
        return sb.ToString();
    }

    public DocxWriter Heading(string text, int level = 1)
    {
        level = Math.Clamp(level, 1, 3);
        _body.Append($"<w:p><w:pPr><w:pStyle w:val=\"Heading{level}\"/></w:pPr>")
             .Append(Run(text, false, false)).Append("</w:p>");
        return this;
    }

    public DocxWriter Paragraph(string text, bool bold = false, bool italic = false, ParaAlign align = ParaAlign.Justify)
    {
        _body.Append("<w:p><w:pPr>").Append(AlignXml(align)).Append("</w:pPr>")
             .Append(Run(text, bold, italic)).Append("</w:p>");
        return this;
    }

    /// <summary>Đoạn văn gồm nhiều run với định dạng riêng (text, bold, italic).</summary>
    public DocxWriter RichParagraph(ParaAlign align, params (string text, bool bold, bool italic)[] runs)
    {
        _body.Append("<w:p><w:pPr>").Append(AlignXml(align)).Append("</w:pPr>");
        foreach (var r in runs) _body.Append(Run(r.text, r.bold, r.italic));
        _body.Append("</w:p>");
        return this;
    }

    /// <summary>Tiêu đề lớn căn giữa (trang bìa).</summary>
    public DocxWriter Title(string text, double sizePt = 18)
    {
        _body.Append("<w:p><w:pPr><w:jc w:val=\"center\"/><w:spacing w:before=\"240\" w:after=\"240\"/></w:pPr>")
             .Append(Run(text, true, false, sizePt)).Append("</w:p>");
        return this;
    }

    /// <summary>Gạch đầu dòng đơn giản (ký tự "–" + thụt lề).</summary>
    public DocxWriter Bullet(string text)
    {
        _body.Append("<w:p><w:pPr><w:ind w:left=\"567\" w:hanging=\"284\"/><w:jc w:val=\"both\"/></w:pPr>")
             .Append(Run("–\t" + text, false, false)).Append("</w:p>");
        return this;
    }

    /// <summary>Dòng công thức dạng văn bản thường (font Cambria Math, thụt lề, nghiêng).</summary>
    public DocxWriter Equation(string text, bool italic = false)
    {
        _body.Append("<w:p><w:pPr><w:pStyle w:val=\"Formula\"/></w:pPr>")
             .Append(Run(text, false, italic)).Append("</w:p>");
        return this;
    }

    public DocxWriter Equations(IEnumerable<string> lines)
    {
        foreach (var l in lines) Equation(l);
        return this;
    }

    public DocxWriter PageBreak()
    {
        _body.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>");
        return this;
    }

    public DocxWriter EmptyLine()
    {
        _body.Append("<w:p/>");
        return this;
    }

    /// <summary>Bảng có lưới. colWidthsCm = null → chia đều bề rộng vùng in.</summary>
    public DocxWriter Table(string[] headers, IEnumerable<string[]> rows, double[] colWidthsCm = null, double fontPt = 11)
    {
        var rowList = rows?.ToList() ?? new List<string[]>();
        int nc = headers?.Length ?? 0;
        foreach (var r in rowList) nc = Math.Max(nc, r?.Length ?? 0);
        if (nc == 0) return this;

        int[] w = new int[nc];
        if (colWidthsCm != null && colWidthsCm.Length >= nc)
            for (int i = 0; i < nc; i++) w[i] = Math.Max(Cm(colWidthsCm[i]), 300);
        else
        {
            int each = TextWidthTwips / nc;
            for (int i = 0; i < nc; i++) w[i] = each;
        }
        int total = w.Sum();

        var sb = _body;
        sb.Append("<w:tbl><w:tblPr><w:tblStyle w:val=\"TableGrid\"/>")
          .Append($"<w:tblW w:w=\"{total}\" w:type=\"dxa\"/><w:jc w:val=\"center\"/><w:tblLayout w:type=\"fixed\"/>")
          .Append("<w:tblCellMar><w:left w:w=\"57\" w:type=\"dxa\"/><w:right w:w=\"57\" w:type=\"dxa\"/></w:tblCellMar>")
          .Append("<w:tblLook w:val=\"04A0\" w:firstRow=\"1\" w:lastRow=\"0\" w:firstColumn=\"1\" w:lastColumn=\"0\" w:noHBand=\"0\" w:noVBand=\"1\"/>")
          .Append("</w:tblPr><w:tblGrid>");
        foreach (int x in w) sb.Append($"<w:gridCol w:w=\"{x}\"/>");
        sb.Append("</w:tblGrid>");

        void AppendRow(string[] cells, bool header)
        {
            sb.Append("<w:tr>");
            if (header) sb.Append("<w:trPr><w:tblHeader/><w:cantSplit/></w:trPr>");
            else sb.Append("<w:trPr><w:cantSplit/></w:trPr>");
            for (int i = 0; i < nc; i++)
            {
                string t = cells != null && i < cells.Length ? cells[i] ?? "" : "";
                sb.Append($"<w:tc><w:tcPr><w:tcW w:w=\"{w[i]}\" w:type=\"dxa\"/>");
                if (header) sb.Append("<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"D9E2F3\"/>");
                sb.Append("<w:vAlign w:val=\"center\"/></w:tcPr>");
                bool numeric = !header && LooksNumeric(t);
                sb.Append("<w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/>")
                  .Append(header ? "<w:jc w:val=\"center\"/>" : numeric ? "<w:jc w:val=\"right\"/>" : "")
                  .Append("</w:pPr>")
                  .Append(Run(t, header, false, fontPt))
                  .Append("</w:p></w:tc>");
            }
            sb.Append("</w:tr>");
        }

        if (headers != null && headers.Length > 0) AppendRow(headers, true);
        foreach (var r in rowList) AppendRow(r, false);
        sb.Append("</w:tbl>");
        // đoạn trống sau bảng (Word yêu cầu đoạn giữa 2 bảng liền nhau)
        sb.Append("<w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"60\"/></w:pPr></w:p>");
        return this;
    }

    private static bool LooksNumeric(string t)
    {
        if (string.IsNullOrWhiteSpace(t)) return false;
        return double.TryParse(t.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    // ------------------------------------------------------------------
    public void Save(string path)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        if (File.Exists(path)) File.Delete(path);
        using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        Put(zip, "[Content_Types].xml", ContentTypes);
        Put(zip, "_rels/.rels", Rels);
        Put(zip, "word/_rels/document.xml.rels", DocRels);
        Put(zip, "word/styles.xml", Styles);
        Put(zip, "word/settings.xml", Settings);
        Put(zip, "word/document.xml", Document());
    }

    private static void Put(ZipArchive zip, string name, string content)
    {
        var e = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var s = e.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        s.Write(bytes, 0, bytes.Length);
    }

    private string Document()
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" ")
          .Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><w:body>");
        sb.Append(_body);
        // A4: 11906 x 16838 twips; lề trên 2, dưới 2, trái 3, phải 1.5 cm
        sb.Append("<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/>")
          .Append($"<w:pgMar w:top=\"{Cm(2)}\" w:right=\"{Cm(1.5)}\" w:bottom=\"{Cm(2)}\" w:left=\"{Cm(3)}\" w:header=\"567\" w:footer=\"567\" w:gutter=\"0\"/>")
          .Append("<w:cols w:space=\"708\"/></w:sectPr>");
        sb.Append("</w:body></w:document>");
        return sb.ToString();
    }

    private const string ContentTypes =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
        "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>" +
        "<Override PartName=\"/word/settings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml\"/>" +
        "</Types>";

    private const string Rels =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
        "</Relationships>";

    private const string DocRels =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings\" Target=\"settings.xml\"/>" +
        "</Relationships>";

    private const string Settings =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<w:settings xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
        "<w:defaultTabStop w:val=\"567\"/><w:compat><w:compatSetting w:name=\"compatibilityMode\" w:uri=\"http://schemas.microsoft.com/office/word\" w:val=\"15\"/></w:compat>" +
        "</w:settings>";

    private static string HeadingStyle(int level, int sizeHalfPt, bool italic) =>
        $"<w:style w:type=\"paragraph\" w:styleId=\"Heading{level}\"><w:name w:val=\"heading {level}\"/><w:basedOn w:val=\"Normal\"/><w:next w:val=\"Normal\"/><w:qFormat/>" +
        $"<w:pPr><w:keepNext/><w:keepLines/><w:spacing w:before=\"{(level == 1 ? 240 : 160)}\" w:after=\"120\"/><w:jc w:val=\"left\"/><w:outlineLvl w:val=\"{level - 1}\"/></w:pPr>" +
        $"<w:rPr><w:b/><w:bCs/>{(italic ? "<w:i/><w:iCs/>" : "")}<w:sz w:val=\"{sizeHalfPt}\"/><w:szCs w:val=\"{sizeHalfPt}\"/></w:rPr></w:style>";

    private static readonly string Styles =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
        "<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Times New Roman\" w:hAnsi=\"Times New Roman\" w:eastAsia=\"Times New Roman\" w:cs=\"Times New Roman\"/>" +
        "<w:sz w:val=\"26\"/><w:szCs w:val=\"26\"/><w:lang w:val=\"vi-VN\" w:eastAsia=\"en-US\" w:bidi=\"ar-SA\"/></w:rPr></w:rPrDefault>" +
        "<w:pPrDefault><w:pPr><w:spacing w:before=\"0\" w:after=\"80\" w:line=\"288\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>" +
        "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/><w:qFormat/>" +
        "<w:rPr><w:rFonts w:ascii=\"Times New Roman\" w:hAnsi=\"Times New Roman\" w:cs=\"Times New Roman\"/><w:sz w:val=\"26\"/><w:szCs w:val=\"26\"/></w:rPr></w:style>" +
        HeadingStyle(1, 28, false) + HeadingStyle(2, 26, false) + HeadingStyle(3, 26, true) +
        "<w:style w:type=\"paragraph\" w:customStyle=\"1\" w:styleId=\"Formula\"><w:name w:val=\"Formula\"/><w:basedOn w:val=\"Normal\"/>" +
        "<w:pPr><w:spacing w:before=\"0\" w:after=\"40\"/><w:ind w:left=\"851\"/></w:pPr>" +
        "<w:rPr><w:rFonts w:ascii=\"Cambria Math\" w:hAnsi=\"Cambria Math\" w:cs=\"Times New Roman\"/><w:sz w:val=\"24\"/><w:szCs w:val=\"24\"/></w:rPr></w:style>" +
        "<w:style w:type=\"table\" w:default=\"1\" w:styleId=\"TableNormal\"><w:name w:val=\"Normal Table\"/><w:uiPriority w:val=\"99\"/><w:semiHidden/>" +
        "<w:tblPr><w:tblInd w:w=\"0\" w:type=\"dxa\"/><w:tblCellMar><w:top w:w=\"0\" w:type=\"dxa\"/><w:left w:w=\"108\" w:type=\"dxa\"/>" +
        "<w:bottom w:w=\"0\" w:type=\"dxa\"/><w:right w:w=\"108\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr></w:style>" +
        "<w:style w:type=\"table\" w:styleId=\"TableGrid\"><w:name w:val=\"Table Grid\"/><w:basedOn w:val=\"TableNormal\"/>" +
        "<w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr>" +
        "<w:tblPr><w:tblBorders><w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/><w:left w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/>" +
        "<w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/><w:right w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/>" +
        "<w:insideH w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"auto\"/></w:tblBorders></w:tblPr></w:style>" +
        "</w:styles>";
}
