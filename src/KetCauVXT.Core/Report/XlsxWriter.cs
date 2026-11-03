using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace KetCauVXT.Report;

/// <summary>Kiểu định dạng ô (chỉ số trong cellXfs của styles.xml).</summary>
public enum XlStyle
{
    Default = 0,
    Header = 1,
    Number2 = 2,
    Bold = 3,
    Number3 = 4,
    Integer = 5,
    Fail = 6,
    Ok = 7,
    Title = 8,
    // ---- [So sánh ETABS/SAP – ô số 0.00 có nền màu: xanh ≤ ngưỡng cảnh báo, cam cảnh báo, đỏ vượt ngưỡng] ----
    FillOk = 9,
    FillWarn = 10,
    FillFail = 11
    // ---- [hết] ----
}

/// <summary>Giá trị ô kèm định dạng riêng: <c>new XlCell("KHÔNG ĐẠT", XlStyle.Fail)</c>.</summary>
public readonly struct XlCell
{
    public readonly object Value;
    public readonly XlStyle Style;
    public XlCell(object value, XlStyle style) { Value = value; Style = style; }
}

// ---- [Công thức – dùng cho bảng khối lượng vật tư] ----
/// <summary>Ô công thức Excel (không có dấu "=" đầu), kèm giá trị tính sẵn để hiển thị khi chưa tính lại:
/// <c>new XlFormula("B2*E2", 123.4)</c>.</summary>
public readonly struct XlFormula
{
    public readonly string Formula;
    public readonly double Cached;
    public readonly XlStyle? Style;
    public XlFormula(string formula, double cached, XlStyle? style = null) { Formula = formula; Cached = cached; Style = style; }
    public override string ToString() => Cached.ToString("0.###", CultureInfo.InvariantCulture);
}
// ---- [hết] ----

/// <summary>Một trang tính.</summary>
public class XlsxSheet
{
    public string Name { get; }
    internal readonly List<(object[] cells, XlStyle? rowStyle)> Rows = new();
    internal double[] Widths;
    /// <summary>Số hàng tiêu đề được cố định (freeze). Mặc định 1. Đặt 0 để bỏ.</summary>
    public int FreezeRows { get; set; } = 1;
    /// <summary>Hàng đầu tiên (hàng đầu bảng) in đậm có nền.</summary>
    public bool FirstRowIsHeader { get; set; } = true;
    /// <summary>Định dạng số mặc định cho các ô số thực.</summary>
    public XlStyle NumberStyle { get; set; } = XlStyle.Number2;

    internal XlsxSheet(string name) { Name = name; }

    public int RowCount => Rows.Count;

    /// <summary>Thêm một hàng. string → chuỗi; double/float/decimal → số (0.00); int/long → số nguyên; XlCell → giá trị + định dạng; null → ô trống.</summary>
    public XlsxSheet AddRow(params object[] cells)
    {
        Rows.Add((cells ?? Array.Empty<object>(), null));
        return this;
    }

    /// <summary>Thêm hàng tiêu đề (in đậm, nền xanh, viền) ở vị trí bất kỳ.</summary>
    public XlsxSheet AddHeaderRow(params object[] cells)
    {
        Rows.Add((cells ?? Array.Empty<object>(), XlStyle.Header));
        return this;
    }

    /// <summary>Thêm hàng in đậm (tổng cộng, tiêu đề mục).</summary>
    public XlsxSheet AddBoldRow(params object[] cells)
    {
        Rows.Add((cells ?? Array.Empty<object>(), XlStyle.Bold));
        return this;
    }

    public XlsxSheet AddEmptyRow()
    {
        Rows.Add((Array.Empty<object>(), null));
        return this;
    }

    /// <summary>Độ rộng cột (đơn vị ký tự Excel).</summary>
    public XlsxSheet SetColumnWidths(params double[] widths)
    {
        Widths = widths;
        return this;
    }
}

/// <summary>Trình ghi file Excel (.xlsx) tối giản: nhiều trang, chuỗi inline (t="inlineStr"), số, cố định hàng tiêu đề.</summary>
public class XlsxWriter
{
    private readonly List<XlsxSheet> _sheets = new();
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public IReadOnlyList<XlsxSheet> Sheets => _sheets;

    /// <summary>Thêm trang tính. Tên tối đa 31 ký tự, bỏ các ký tự : \ / ? * [ ], không trùng tên.</summary>
    public XlsxSheet AddSheet(string name)
    {
        string n = CleanName(name);
        string baseName = n; int k = 2;
        while (_sheets.Any(s => string.Equals(s.Name, n, StringComparison.OrdinalIgnoreCase)))
        {
            string suf = $" ({k++})";
            n = (baseName.Length + suf.Length > 31 ? baseName.Substring(0, 31 - suf.Length) : baseName) + suf;
        }
        var sh = new XlsxSheet(n);
        _sheets.Add(sh);
        return sh;
    }

    public static string CleanName(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name ?? "")
        {
            if (c is ':' or '\\' or '/' or '?' or '*' or '[' or ']') continue;
            if (c < 0x20) continue;
            sb.Append(c);
        }
        string n = sb.ToString().Trim().Trim('\'');
        if (n.Length == 0) n = "Sheet";
        if (n.Length > 31) n = n.Substring(0, 31);
        return n;
    }

    public void Save(string path)
    {
        if (_sheets.Count == 0) AddSheet("Sheet1");
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        if (File.Exists(path)) File.Delete(path);
        using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var ct = new StringBuilder();
        ct.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")
          .Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">")
          .Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>")
          .Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>")
          .Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>")
          .Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        for (int i = 0; i < _sheets.Count; i++)
            ct.Append($"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        ct.Append("</Types>");
        Put(zip, "[Content_Types].xml", ct.ToString());

        Put(zip, "_rels/.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>");

        var wb = new StringBuilder();
        wb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")
          .Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">")
          .Append("<bookViews><workbookView activeTab=\"0\"/></bookViews><sheets>");
        for (int i = 0; i < _sheets.Count; i++)
            wb.Append($"<sheet name=\"{DocxWriter.Esc(_sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
        wb.Append("</sheets></workbook>");
        Put(zip, "xl/workbook.xml", wb.ToString());

        var wr = new StringBuilder();
        wr.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")
          .Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (int i = 0; i < _sheets.Count; i++)
            wr.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
        wr.Append($"<Relationship Id=\"rId{_sheets.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        wr.Append("</Relationships>");
        Put(zip, "xl/_rels/workbook.xml.rels", wr.ToString());

        Put(zip, "xl/styles.xml", StylesXml);
        for (int i = 0; i < _sheets.Count; i++)
            Put(zip, $"xl/worksheets/sheet{i + 1}.xml", SheetXml(_sheets[i], i == 0));
    }

    private static void Put(ZipArchive zip, string name, string content)
    {
        var e = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var s = e.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        s.Write(bytes, 0, bytes.Length);
    }

    public static string ColName(int col) // col 0-based
    {
        var sb = new StringBuilder();
        int c = col + 1;
        while (c > 0)
        {
            int m = (c - 1) % 26;
            sb.Insert(0, (char)('A' + m));
            c = (c - 1) / 26;
        }
        return sb.ToString();
    }

    private static string SheetXml(XlsxSheet sh, bool selected)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")
          .Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"").Append(selected ? " tabSelected=\"1\"" : "").Append(">");
        if (sh.FreezeRows > 0 && sh.Rows.Count > sh.FreezeRows)
        {
            string top = "A" + (sh.FreezeRows + 1).ToString(Inv);
            sb.Append($"<pane ySplit=\"{sh.FreezeRows}\" topLeftCell=\"{top}\" activePane=\"bottomLeft\" state=\"frozen\"/>")
              .Append($"<selection pane=\"bottomLeft\" activeCell=\"{top}\" sqref=\"{top}\"/>");
        }
        sb.Append("</sheetView></sheetViews>");
        sb.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
        if (sh.Widths != null && sh.Widths.Length > 0)
        {
            sb.Append("<cols>");
            for (int i = 0; i < sh.Widths.Length; i++)
            {
                double w = sh.Widths[i] > 0 ? sh.Widths[i] : 10;
                sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{w.ToString("0.##", Inv)}\" customWidth=\"1\"/>");
            }
            sb.Append("</cols>");
        }
        sb.Append("<sheetData>");
        for (int r = 0; r < sh.Rows.Count; r++)
        {
            var (cells, rowStyle) = sh.Rows[r];
            XlStyle? rs = rowStyle ?? (r == 0 && sh.FirstRowIsHeader ? XlStyle.Header : (XlStyle?)null);
            int rn = r + 1;
            sb.Append($"<row r=\"{rn}\">");
            for (int c = 0; c < cells.Length; c++)
            {
                object v = cells[c];
                XlStyle? st = rs;
                if (v is XlCell xc) { v = xc.Value; st = xc.Style; }
                if (v == null && st == null) continue;
                string addr = ColName(c) + rn.ToString(Inv);
                switch (v)
                {
                    case null:
                        sb.Append($"<c r=\"{addr}\" s=\"{(int)st.Value}\"/>");
                        break;
                    case XlFormula fx:   // [công thức]
                        {
                            var fst = fx.Style ?? st ?? sh.NumberStyle;
                            double cv = double.IsFinite(fx.Cached) ? fx.Cached : 0;
                            sb.Append($"<c r=\"{addr}\" s=\"{(int)fst}\"><f>{DocxWriter.Esc(fx.Formula)}</f><v>{cv.ToString("R", Inv)}</v></c>");
                        }
                        break;
                    case double d:
                        AppendNum(sb, addr, d, st ?? sh.NumberStyle);
                        break;
                    case float fl:
                        AppendNum(sb, addr, fl, st ?? sh.NumberStyle);
                        break;
                    case decimal de:
                        AppendNum(sb, addr, (double)de, st ?? sh.NumberStyle);
                        break;
                    case int i:
                        AppendNum(sb, addr, i, st ?? XlStyle.Integer);
                        break;
                    case long l:
                        AppendNum(sb, addr, l, st ?? XlStyle.Integer);
                        break;
                    case short s16:
                        AppendNum(sb, addr, s16, st ?? XlStyle.Integer);
                        break;
                    case bool b:
                        AppendStr(sb, addr, b ? "Có" : "Không", st);
                        break;
                    default:
                        AppendStr(sb, addr, Convert.ToString(v, Inv) ?? "", st);
                        break;
                }
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData>");
        sb.Append("<pageMargins left=\"0.7\" right=\"0.5\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static void AppendNum(StringBuilder sb, string addr, double v, XlStyle st)
    {
        if (double.IsNaN(v) || double.IsInfinity(v))
        {
            AppendStr(sb, addr, double.IsNaN(v) ? "" : (v > 0 ? "∞" : "-∞"), st);
            return;
        }
        sb.Append($"<c r=\"{addr}\" s=\"{(int)st}\"><v>{v.ToString("R", Inv)}</v></c>");
    }

    private static void AppendStr(StringBuilder sb, string addr, string s, XlStyle? st)
    {
        string sAttr = st.HasValue ? $" s=\"{(int)st.Value}\"" : "";
        sb.Append($"<c r=\"{addr}\"{sAttr} t=\"inlineStr\"><is><t xml:space=\"preserve\">{DocxWriter.Esc(s)}</t></is></c>");
    }

    // numFmt 164 = 0.00 ; 165 = 0.000 ; built-in 1 = 0
    // cellXfs: 0 mặc định; 1 tiêu đề (đậm, nền, viền, giữa, xuống dòng); 2 số 0.00; 3 đậm; 4 số 0.000; 5 số nguyên;
    //          6 chữ đỏ đậm (không đạt); 7 chữ xanh (đạt); 8 tiêu đề lớn; 9/10/11 nền xanh/cam/đỏ (so sánh ETABS)
    private const string StylesXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
        "<numFmts count=\"2\"><numFmt numFmtId=\"164\" formatCode=\"0.00\"/><numFmt numFmtId=\"165\" formatCode=\"0.000\"/></numFmts>" +
        "<fonts count=\"5\">" +
        "<font><sz val=\"11\"/><name val=\"Times New Roman\"/><family val=\"1\"/></font>" +
        "<font><b/><sz val=\"11\"/><name val=\"Times New Roman\"/><family val=\"1\"/></font>" +
        "<font><b/><sz val=\"11\"/><color rgb=\"FFC00000\"/><name val=\"Times New Roman\"/><family val=\"1\"/></font>" +
        "<font><sz val=\"11\"/><color rgb=\"FF00703C\"/><name val=\"Times New Roman\"/><family val=\"1\"/></font>" +
        "<font><b/><sz val=\"14\"/><name val=\"Times New Roman\"/><family val=\"1\"/></font>" +
        "</fonts>" +
        "<fills count=\"6\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD9E2F3\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
        // [So sánh ETABS/SAP] nền xanh / cam / đỏ (fillId 3, 4, 5)
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFC6EFCE\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFD8A8\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFC7CE\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
        "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border>" +
        "<border><left style=\"thin\"><color auto=\"1\"/></left><right style=\"thin\"><color auto=\"1\"/></right>" +
        "<top style=\"thin\"><color auto=\"1\"/></top><bottom style=\"thin\"><color auto=\"1\"/></bottom><diagonal/></border></borders>" +
        "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
        "<cellXfs count=\"12\">" +
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
        "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
        "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
        "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"1\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
        "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
        "<xf numFmtId=\"0\" fontId=\"4\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
        // [So sánh ETABS/SAP] 9 xanh, 10 cam, 11 đỏ – số 0.00, viền mảnh
        "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +
        "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +
        "<xf numFmtId=\"164\" fontId=\"2\" fillId=\"5\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>" +
        "</cellXfs>" +
        "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
        "</styleSheet>";
}
