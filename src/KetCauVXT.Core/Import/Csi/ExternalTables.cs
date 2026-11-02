using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace KetCauVXT.Import.Csi;

/// <summary>Một bảng số liệu đọc từ file xuất của ETABS / SAP2000 / SAFE / phần mềm khác.
/// Nguồn: sheet Excel (.xlsx – mỗi sheet một bảng), khối "TABLE:" trong CSV/TXT, hoặc bảng dạng Khoá=Giá trị của file .s2k/.$2k/.$et.</summary>
public class ExtTable
{
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
    public List<string> Headers { get; set; } = new();
    /// <summary>Đơn vị từng cột (dòng đơn vị dưới dòng tiêu đề của ETABS/SAP); null = không có dòng đơn vị.</summary>
    public List<string> Units { get; set; }
    public List<string[]> Rows { get; set; } = new();
    /// <summary>Đơn vị chung của file (từ "CurrUnits"/"UNITS" hoặc người dùng chọn): lực, chiều dài. Rỗng = chưa biết (mặc định kN, m).</summary>
    public string FileForceUnit { get; set; } = "";
    public string FileLengthUnit { get; set; } = "";
    /// <summary>Chương trình ghi file (nếu đọc được): "ETABS", "SAP2000", "SAFE".</summary>
    public string Program { get; set; } = "";
    /// <summary>Loại bảng nhận diện (ResultImporter.Classify).</summary>
    public ExtTableKind Kind { get; set; }

    public int RowCount => Rows.Count;
    public override string ToString() => $"{Name} ({Rows.Count} dòng)";

    /// <summary>Chuẩn hoá tên cột để so sánh: bỏ khoảng trắng, gạch dưới, gạch nối; chữ hoa.</summary>
    public static string Norm(string s) =>
        new string((s ?? "").Where(ch => !char.IsWhiteSpace(ch) && ch != '_' && ch != '-' && ch != '"').ToArray()).ToUpperInvariant();

    /// <summary>Chỉ số cột theo các tên có thể có (không phân biệt hoa thường, khoảng trắng); −1 nếu không có.</summary>
    public int Col(params string[] names)
    {
        foreach (var n in names)
        {
            string k = Norm(n);
            int i = Headers.FindIndex(h => Norm(h) == k);
            if (i >= 0) return i;
        }
        return -1;
    }

    public bool Has(params string[] names) => Col(names) >= 0;

    public static string Cell(string[] row, int col) => col >= 0 && col < row.Length ? (row[col] ?? "").Trim() : "";

    public static bool TryNum(string s, out double v)
    {
        s = (s ?? "").Trim().Trim('"');
        if (s.Length == 0) { v = 0; return false; }
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return true;
        // dấu phẩy thập phân (Excel/CSV kiểu Việt Nam)
        return s.Count(c => c == ',') == 1 && !s.Contains('.') &&
               double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }

    /// <summary>Đơn vị ghi trên dòng đơn vị của cột (rỗng nếu không có).</summary>
    public string UnitOf(int col) => Units != null && col >= 0 && col < Units.Count ? (Units[col] ?? "").Trim() : "";

    /// <summary>Hệ số đổi giá trị cột về kN, m: theo dòng đơn vị của cột nếu đọc được, ngược lại theo đơn vị chung của file và loại đại lượng.</summary>
    public double Factor(int col, Qty q)
    {
        string u = UnitOf(col);
        if (u.Length > 0 && UnitConv.TryFactor(u, out double f)) return f;
        return UnitConv.ByQuantity(q, FileForceUnit, FileLengthUnit);
    }

    /// <summary>Mô tả đơn vị đã dùng của bảng (để hiển thị).</summary>
    public string UnitsText()
    {
        if (Units != null)
        {
            var u = Units.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().Take(5).ToList();
            if (u.Count > 0) return string.Join(", ", u);
        }
        return (string.IsNullOrEmpty(FileForceUnit) ? "kN" : FileForceUnit) + ", " + (string.IsNullOrEmpty(FileLengthUnit) ? "m" : FileLengthUnit) + " (mặc định)";
    }
}

public enum ExtTableKind
{
    Unknown,
    FrameForces, JointReactions, JointDisplacements, BaseReactions, StoryDrifts,
    JointCoordinates, FrameConnectivity,
    StripForces, SlabForces, SoilPressures, PunchingShear,
    /// <summary>Bảng mô hình / thiết kế không dùng khi nhập nội lực</summary>
    Ignored
}

/// <summary>Loại đại lượng của cột – quyết định hệ số đổi đơn vị khi bảng không có dòng đơn vị.</summary>
public enum Qty { None, Force, Moment, Length, Rotation, ForcePerLength, MomentPerLength, Pressure }

/// <summary>Đổi đơn vị về hệ kN – m. Hiểu các dạng "kN", "Tonf", "tf", "kgf", "N", "kip", "lb"; "m", "cm", "mm", "ft", "in";
/// tích/thương: "kN-m", "kN.m", "Tonf-m", "kgf-cm", "kN-m/m", "kN/m2", "kgf/cm²"; góc "Radians", "rad", "deg".</summary>
public static class UnitConv
{
    private static readonly Dictionary<string, double> ForceU = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kn"] = 1, ["n"] = 1e-3, ["mn"] = 1e3, ["dan"] = 1e-2,
        ["kgf"] = 9.80665e-3, ["kg"] = 9.80665e-3, ["kgs"] = 9.80665e-3,
        ["tonf"] = 9.80665, ["tf"] = 9.80665, ["ton"] = 9.80665, ["t"] = 9.80665, ["tonne"] = 9.80665,
        ["kip"] = 4.4482216, ["kips"] = 4.4482216, ["k"] = 4.4482216, ["lb"] = 4.4482216e-3, ["lbf"] = 4.4482216e-3, ["lbs"] = 4.4482216e-3
    };
    private static readonly Dictionary<string, double> LengthU = new(StringComparer.OrdinalIgnoreCase)
    {
        ["m"] = 1, ["cm"] = 0.01, ["mm"] = 0.001, ["dm"] = 0.1, ["ft"] = 0.3048, ["feet"] = 0.3048, ["in"] = 0.0254, ["inch"] = 0.0254
    };
    private static readonly Dictionary<string, double> OtherU = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rad"] = 1, ["radians"] = 1, ["radian"] = 1, ["deg"] = Math.PI / 180, ["degrees"] = Math.PI / 180, ["degree"] = Math.PI / 180,
        ["c"] = 1, ["f"] = 1, ["unitless"] = 1, ["sec"] = 1, ["s"] = 1, ["%"] = 1
    };

    public static bool IsForce(string u) => ForceU.ContainsKey((u ?? "").Trim());
    public static bool IsLength(string u) => LengthU.ContainsKey((u ?? "").Trim());

    /// <summary>Hệ số đổi một chuỗi đơn vị về kN – m; false nếu không hiểu.</summary>
    public static bool TryFactor(string unit, out double f)
    {
        f = 1;
        string s = (unit ?? "").Trim().Trim('"', '(', ')', '[', ']');
        if (s.Length == 0) return false;
        s = s.Replace('²', '2').Replace('³', '3').Replace('·', '-').Replace('*', '-').Replace('.', '-').Replace('×', '-');
        var parts = s.Split('/');
        if (parts.Length > 2) return false;
        if (!TryProduct(parts[0], out double num)) return false;
        double den = 1;
        if (parts.Length == 2 && !TryProduct(parts[1], out den)) return false;
        f = num / den;
        return f > 0 && double.IsFinite(f);
    }

    private static bool TryProduct(string s, out double f)
    {
        f = 1;
        var toks = s.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (toks.Length == 0) return false;
        foreach (var t0 in toks)
        {
            string t = t0;
            int exp = 1;
            int k = t.Length;
            while (k > 0 && char.IsDigit(t[k - 1])) k--;
            if (k < t.Length && k > 0) { exp = int.Parse(t.Substring(k), CultureInfo.InvariantCulture); t = t.Substring(0, k); }
            double b;
            if (ForceU.TryGetValue(t, out b) || LengthU.TryGetValue(t, out b) || OtherU.TryGetValue(t, out b)) f *= Math.Pow(b, exp);
            else return false;
        }
        return true;
    }

    /// <summary>Hệ số theo loại đại lượng từ đơn vị lực / chiều dài chung (rỗng = kN, m).</summary>
    public static double ByQuantity(Qty q, string force, string length)
    {
        double fF = !string.IsNullOrWhiteSpace(force) && TryFactor(force, out double a) ? a : 1;
        double fL = !string.IsNullOrWhiteSpace(length) && TryFactor(length, out double b) ? b : 1;
        return q switch
        {
            Qty.Force => fF,
            Qty.Moment => fF * fL,
            Qty.Length => fL,
            Qty.ForcePerLength => fF / fL,
            Qty.MomentPerLength => fF,
            Qty.Pressure => fF / (fL * fL),
            _ => 1
        };
    }

    /// <summary>Tách "KN, m, C" / "Tonf, m, C" / "kgf, cm, C" → (lực, chiều dài).</summary>
    public static (string force, string length) SplitUnits(string s)
    {
        var p = (s ?? "").Trim().Trim('"').Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim('"')).ToList();
        string f = p.FirstOrDefault(IsForce) ?? "", l = p.FirstOrDefault(IsLength) ?? "";
        return (f, l);
    }
}

/// <summary>Đọc bảng từ file xuất: .xlsx/.xlsm (mọi sheet), .csv/.txt/.tsv (một hoặc nhiều khối "TABLE:"),
/// .s2k/.$2k/.$et/.$sf/.txt dạng Khoá=Giá trị (SAP2000/ETABS/SAFE "Export to text").</summary>
public static class ExternalTableReader
{
    public static List<ExtTable> ReadFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".xls") throw new InvalidDataException("Định dạng .xls (Excel 97-2003) không được hỗ trợ – hãy xuất lại bảng thành .xlsx hoặc .csv.");
        List<ExtTable> res;
        if (ext is ".xlsx" or ".xlsm") res = ReadXlsx(path);
        else
        {
            var lines = ReadLines(path);
            res = LooksKeyValue(lines) ? ReadKeyValue(lines, path) : ReadDelimited(lines, path);
        }
        foreach (var t in res) { t.File = path; if (string.IsNullOrEmpty(t.Name)) t.Name = Path.GetFileNameWithoutExtension(path); }
        return res;
    }

    /// <summary>Đọc văn bản: UTF-8 (có/không BOM); nếu lỗi mã hoá thì Latin-1 (file ETABS/SAP cũ ghi ANSI).</summary>
    public static List<string> ReadLines(string path)
    {
        var bytes = File.ReadAllBytes(path);
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { text = Encoding.Latin1.GetString(bytes); }
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
    }

    private static bool LooksKeyValue(List<string> lines)
    {
        int tables = 0, kv = 0;
        foreach (var l in lines.Take(400))
        {
            var t = l.TrimStart();
            if (t.StartsWith("TABLE:", StringComparison.OrdinalIgnoreCase)) tables++;
            else if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^[A-Za-z][A-Za-z0-9_]*=")) kv++;
        }
        return tables > 0 && kv > 0;
    }

    // ------------------------------------------------------------------ Khoá=Giá trị
    /// <summary>Tách bảng dạng s2k: TABLE:  "TÊN" rồi các dòng "Khoá=Giá trị" (giá trị có khoảng trắng đặt trong ngoặc kép; " _" cuối dòng = dòng tiếp).</summary>
    public static List<ExtTable> ReadKeyValue(List<string> lines, string path = "")
    {
        var res = new List<ExtTable>();
        ExtTable cur = null;
        var rows = new List<Dictionary<string, string>>();
        string force = "", length = "", program = "";
        void Flush()
        {
            if (cur == null) return;
            foreach (var r in rows) foreach (var k in r.Keys) if (!cur.Headers.Contains(k)) cur.Headers.Add(k);
            foreach (var r in rows) cur.Rows.Add(cur.Headers.Select(h => r.TryGetValue(h, out var v) ? v : "").ToArray());
            res.Add(cur);
            cur = null; rows = new List<Dictionary<string, string>>();
        }
        string pending = "";
        foreach (var raw in lines)
        {
            string l = raw.TrimEnd();
            if (l.EndsWith(" _")) { pending += l.Substring(0, l.Length - 2) + " "; continue; }
            if (pending.Length > 0) { l = pending + l; pending = ""; }
            string t = l.Trim();
            if (t.Length == 0) continue;
            if (t.StartsWith("TABLE:", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                cur = new ExtTable { Name = t.Substring(6).Trim().Trim('"').Trim(), File = path };
                continue;
            }
            if (t.StartsWith("END TABLE DATA", StringComparison.OrdinalIgnoreCase)) { Flush(); break; }
            if (cur == null) continue;
            var kv = ParseKeyValues(t);
            if (kv.Count == 0) continue;
            rows.Add(kv);
            if (Norm(cur.Name) == "PROGRAMCONTROL")
            {
                if (kv.TryGetValue("CurrUnits", out var cu)) (force, length) = UnitConv.SplitUnits(cu);
                if (kv.TryGetValue("ProgramName", out var pn)) program = pn;
            }
        }
        Flush();
        foreach (var tb in res) { tb.FileForceUnit = force; tb.FileLengthUnit = length; tb.Program = program; }
        return res;
    }

    private static string Norm(string s) => ExtTable.Norm(s);

    /// <summary>"Joint=1   CoordSys=GLOBAL   XorR=0" → từ điển (giữ thứ tự xuất hiện).</summary>
    public static Dictionary<string, string> ParseKeyValues(string line)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int i = 0, n = line.Length;
        while (i < n)
        {
            while (i < n && char.IsWhiteSpace(line[i])) i++;
            int k0 = i;
            while (i < n && line[i] != '=' && !char.IsWhiteSpace(line[i])) i++;
            if (i >= n || line[i] != '=') { while (i < n && !char.IsWhiteSpace(line[i])) i++; continue; }
            string key = line.Substring(k0, i - k0);
            i++;
            string val;
            if (i < n && line[i] == '"')
            {
                int j = line.IndexOf('"', i + 1);
                if (j < 0) j = n;
                val = line.Substring(i + 1, j - i - 1);
                i = Math.Min(n, j + 1);
            }
            else
            {
                int v0 = i;
                while (i < n && !char.IsWhiteSpace(line[i])) i++;
                val = line.Substring(v0, i - v0);
            }
            if (key.Length > 0) d[key] = val;
        }
        return d;
    }

    // ------------------------------------------------------------------ CSV / TXT
    public static List<ExtTable> ReadDelimited(List<string> lines, string path = "")
    {
        var nonEmpty = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Take(30).ToList();
        char sep = nonEmpty.Any(l => l.Contains('\t')) ? '\t' : nonEmpty.Count(l => l.Contains(';')) > nonEmpty.Count(l => l.Contains(',')) ? ';' : ',';
        var rows = lines.Select(l => string.IsNullOrWhiteSpace(l) ? null : SplitCsv(l, sep)).ToList();
        return FromRows(rows, Path.GetFileNameWithoutExtension(path));
    }

    /// <summary>Tách dòng CSV có ngoặc kép ("" = dấu ngoặc kép trong ô).</summary>
    public static string[] SplitCsv(string line, char sep)
    {
        var res = new List<string>();
        var cur = new StringBuilder();
        bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                if (q && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                else q = !q;
                continue;
            }
            if (ch == sep && !q) { res.Add(cur.ToString().Trim()); cur.Clear(); continue; }
            cur.Append(ch);
        }
        res.Add(cur.ToString().Trim());
        return res.ToArray();
    }

    /// <summary>Tách các khối bảng từ dãy dòng (null = dòng trống): [dòng "TABLE: tên"] → dòng tiêu đề → [dòng đơn vị] → dữ liệu.
    /// Dòng tiêu đề = dòng đầu có ≥ 2 ô chữ và không có ô số; dòng đơn vị = dòng ngay sau tiêu đề không chứa ô số nào.</summary>
    public static List<ExtTable> FromRows(List<string[]> rows, string defaultName)
    {
        var res = new List<ExtTable>();
        ExtTable cur = null;
        string pendingName = null;
        bool expectUnits = false;
        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row == null || row.All(string.IsNullOrWhiteSpace))
            {
                // dòng trống: kết thúc bảng nếu đã có dữ liệu (ETABS ghi các bảng nối tiếp, cách nhau dòng trống)
                if (cur != null && cur.Rows.Count > 0) { res.Add(cur); cur = null; }
                continue;
            }
            string first = row.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim() ?? "";
            if (first.StartsWith("TABLE:", StringComparison.OrdinalIgnoreCase))
            {
                if (cur != null && (cur.Rows.Count > 0 || cur.Headers.Count > 0)) res.Add(cur);
                cur = null;
                pendingName = first.Substring(6).Trim().Trim('"').Trim();
                continue;
            }
            int nonEmpty = row.Count(c => !string.IsNullOrWhiteSpace(c));
            int numeric = row.Count(c => ExtTable.TryNum(c, out _));
            if (cur == null)
            {
                if (nonEmpty >= 2 && numeric == 0)
                {
                    cur = new ExtTable { Name = pendingName ?? defaultName, Headers = row.Select(c => (c ?? "").Trim()).ToList() };
                    pendingName = null;
                    expectUnits = true;
                }
                else if (nonEmpty == 1 && numeric == 0) pendingName = first.Trim('"');   // dòng tiêu đề bảng không có "TABLE:"
                continue;
            }
            if (expectUnits)
            {
                expectUnits = false;
                if (numeric == 0 && row.Any(c => UnitConv.TryFactor(c, out _)))
                {
                    cur.Units = row.Select(c => (c ?? "").Trim()).ToList();
                    continue;
                }
            }
            if (numeric == 0 && nonEmpty >= 2 && cur.Rows.Count > 0 && row.Length >= cur.Headers.Count - 1
                && row.Count(c => cur.Headers.Contains((c ?? "").Trim())) >= Math.Max(2, nonEmpty / 2))
            {
                // dòng tiêu đề mới (bảng tiếp theo không có dòng trống ngăn cách)
                res.Add(cur);
                cur = new ExtTable { Name = pendingName ?? defaultName, Headers = row.Select(c => (c ?? "").Trim()).ToList() };
                pendingName = null; expectUnits = true;
                continue;
            }
            cur.Rows.Add(row);
        }
        if (cur != null && (cur.Rows.Count > 0 || cur.Headers.Count > 0)) res.Add(cur);
        return res;
    }

    // ------------------------------------------------------------------ XLSX
    /// <summary>Đọc mọi sheet của .xlsx (Open XML) bằng System.IO.Compression – không cần thư viện ngoài.
    /// Tên bảng = dòng "TABLE: …" đầu sheet nếu có, ngược lại tên sheet.</summary>
    public static List<ExtTable> ReadXlsx(string path)
    {
        var res = new List<ExtTable>();
        foreach (var (name, rows) in ReadXlsxSheets(path))
        {
            var tabs = FromRows(rows.Select(r => r.Length == 0 ? null : r).ToList(), name);
            res.AddRange(tabs);
        }
        return res;
    }

    public static List<(string Name, List<string[]> Rows)> ReadXlsxSheets(string path)
    {
        var result = new List<(string, List<string[]>)>();
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
        var shared = new List<string>();
        var ss = Doc("xl/sharedStrings.xml");
        if (ss?.Root != null)
            foreach (var si in ss.Root.Elements(m + "si"))
                shared.Add(string.Concat(si.Descendants(m + "t").Where(t => t.Parent?.Name != m + "rPh").Select(t => t.Value)));

        var wb = Doc("xl/workbook.xml");
        var rels = Doc("xl/_rels/workbook.xml.rels");
        var sheets = wb?.Root?.Element(m + "sheets")?.Elements(m + "sheet").ToList() ?? new List<XElement>();
        int idx = 0;
        foreach (var sh in sheets)
        {
            idx++;
            string sheetName = (string)sh.Attribute("name") ?? ("Sheet" + idx);
            string sheetPath = "xl/worksheets/sheet" + idx + ".xml";
            var rid = (string)sh.Attribute(r + "id");
            if (rid != null && rels?.Root != null)
            {
                var target = (string)rels.Root.Elements(pr + "Relationship").FirstOrDefault(x => (string)x.Attribute("Id") == rid)?.Attribute("Target");
                if (!string.IsNullOrEmpty(target))
                    sheetPath = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
            }
            var doc = Doc(sheetPath);
            if (doc?.Root == null) continue;
            result.Add((sheetName, SheetRows(doc, m, shared)));
        }
        return result;
    }

    private static List<string[]> SheetRows(XDocument sheet, XNamespace m, List<string> shared)
    {
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
}
