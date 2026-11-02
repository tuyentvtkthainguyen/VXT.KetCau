using System.Globalization;
using System.Text;

namespace KetCauVXT.Import;

public enum DxfEntityType { Line, Polyline, Circle, Text }

/// <summary>Thực thể DXF đã đơn giản hoá (toạ độ đã quy đổi về m, hệ toạ độ thế giới WCS).</summary>
public class DxfEntity
{
    public DxfEntityType Type { get; set; }
    public string Layer { get; set; } = "0";
    /// <summary>Line: 2 điểm; Polyline: các đỉnh; Circle/Text: 1 điểm (tâm / điểm chèn). Đơn vị m.</summary>
    public List<(double X, double Y)> Points { get; set; } = new();
    public bool Closed { get; set; }
    public double Radius { get; set; }
    public string Text { get; set; } = "";
    public double TextHeight { get; set; }
    /// <summary>Tên block ngoài cùng chứa thực thể (đã phân rã từ INSERT); "" = vẽ trực tiếp trong model space.
    /// Block động (*U..) được đổi về tên block gốc khi tra được.</summary>
    public string Block { get; set; } = "";
    /// <summary>Kiểu đường (mã 6). BYLAYER/rỗng đã được thay bằng kiểu đường của lớp; "" nếu không xác định.</summary>
    public string LineType { get; set; } = "";
    /// <summary>Thẻ (tag) của thuộc tính khi thực thể Text đến từ ATTRIB; "" với TEXT/MTEXT thường.</summary>
    public string AttributeTag { get; set; } = "";
    /// <summary>Loại thực thể gốc trong file (LINE, LWPOLYLINE, ARC, MTEXT, HATCH, SOLID…) – dùng thống kê layer.</summary>
    public string SourceType { get; set; } = "";
    /// <summary>Bề rộng nét đa tuyến (mã 43 / 40=41), m; 0 = nét mảnh. Tường vẽ bằng 1 đa tuyến có bề rộng = chiều dày tường.</summary>
    public double Width { get; set; }
    /// <summary>Bảng mã chữ suy từ font (STYLE / mã \f trong MTEXT): "TCVN3" (.VnTime…), "VNI" (VNI-Times…) hoặc "".</summary>
    public string FontHint { get; set; } = "";

    public override string ToString() => $"{Type} [{Layer}] {Points.Count} điểm" + (Block.Length > 0 ? $" <{Block}>" : "");
}

/// <summary>Bộ đọc DXF dạng ASCII (R12 … 2018).
/// Đọc HEADER ($INSUNITS, $ACADVER, $DWGCODEPAGE), TABLES/LAYER (kiểu đường, đóng băng/tắt),
/// BLOCKS (định nghĩa block) và ENTITIES (chỉ model space): LINE, LWPOLYLINE, POLYLINE/VERTEX/SEQEND, CIRCLE, ARC,
/// TEXT, MTEXT, INSERT/MINSERT (phân rã block về toạ độ thế giới, kể cả block lồng nhau) + ATTRIB.
/// Bỏ qua HATCH, DIMENSION, ELLIPSE, SPLINE… Đơn vị bản vẽ lấy theo $INSUNITS (4 = mm, 5 = cm, 6 = m), mặc định mm.</summary>
public class DxfReader
{
    public List<DxfEntity> Entities { get; } = new();
    /// <summary>Giá trị $INSUNITS (0 nếu không khai báo)</summary>
    public int InsUnits { get; private set; }
    /// <summary>Hệ số nhân để đổi đơn vị bản vẽ → m</summary>
    public double Scale { get; private set; } = 0.001;
    public List<string> Warnings { get; } = new();

    /// <summary>Phiên bản DXF ($ACADVER, vd. "AC1027"); "" nếu không có.</summary>
    public string AcadVersion { get; private set; } = "";
    /// <summary>Bảng mã ($DWGCODEPAGE, vd. "ANSI_1258"); "" nếu không có.</summary>
    public string CodePage { get; private set; } = "";
    /// <summary>Tên bảng mã thực tế đã dùng để giải mã file (vd. "utf-8", "windows-1258").</summary>
    public string EncodingUsed { get; private set; } = "";
    /// <summary>Kiểu đường của từng lớp (tên lớp không phân biệt hoa thường).</summary>
    public Dictionary<string, string> LayerLineTypes { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Các lớp đóng băng (frozen) – thực thể trên các lớp này bị bỏ qua.</summary>
    public HashSet<string> FrozenLayers { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Các lớp đang tắt (off, màu âm) – vẫn đọc thực thể, chỉ ghi nhận để người gọi tự lọc.</summary>
    public HashSet<string> OffLayers { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Màu ACI của từng lớp (giá trị tuyệt đối, lớp tắt có màu âm trong file).</summary>
    public Dictionary<string, int> LayerColors { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Miền tô: biên HATCH, SOLID/TRACE (đa tuyến kín, toạ độ m). Không gộp vào Entities để bộ nhập DXF kết cấu không đổi;
    /// bộ đọc kiến trúc dùng để nhận cột vẽ bằng miền tô.</summary>
    public List<DxfEntity> FillEntities { get; } = new();
    /// <summary>Kiểu chữ (STYLE) → gợi ý bảng mã ("TCVN3"/"VNI"/"").</summary>
    public Dictionary<string, string> StyleFontHints { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Tên các block đã định nghĩa trong mục BLOCKS.</summary>
    public IReadOnlyCollection<string> BlockNames => _blocks.Keys;

    // ------------------------------------------------------------------ nội bộ
    private const int MaxDepth = 8;
    private const int MaxEntities = 5_000_000;
    private const int MaxMinsertCells = 10_000;
    private const double ArcStepDeg = 15.0;

    private readonly List<(int code, string value)> _pairs = new();
    private int _pos;

    private sealed class RawEnt
    {
        public DxfEntityType Type;
        public bool IsInsert;
        public bool Paper;
        public string Layer = "0";
        public string LineType = "";
        public List<(double X, double Y)> Pts = new();
        public bool Closed;
        public double Radius;
        public string Text = "";
        public string Tag = "";
        public double Height;
        // INSERT / MINSERT
        public string BlockName = "";
        public double Ix, Iy, Sx = 1, Sy = 1, Rot;
        public int Cols = 1, Rows = 1;
        public double ColSp, RowSp;
        public bool Flip;
        public List<RawEnt> Attribs;
        public string Source = "";
        public double Width;
        public string Style = "";
        public string FontHint = "";
        public bool IsFill;
    }

    private sealed class BlockDef
    {
        public string Name = "";
        public double Bx, By;
        public bool Xref;
        public List<RawEnt> Ents = new();
    }

    /// <summary>Phép biến đổi affine 2D: x' = A x + B y + Tx; y' = C x + D y + Ty.</summary>
    private readonly struct Affine
    {
        public readonly double A, B, C, D, Tx, Ty;
        public Affine(double a, double b, double c, double d, double tx, double ty) { A = a; B = b; C = c; D = d; Tx = tx; Ty = ty; }
        public static Affine Translate(double x, double y) => new(1, 0, 0, 1, x, y);
        public static Affine Scaling(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);
        public static Affine Rotation(double deg)
        {
            double r = deg * Math.PI / 180.0, c = Math.Cos(r), s = Math.Sin(r);
            return new(c, -s, s, c, 0, 0);
        }
        /// <summary>this ∘ m (áp m trước, rồi this)</summary>
        public Affine Then(Affine m) => new(
            A * m.A + B * m.C, A * m.B + B * m.D,
            C * m.A + D * m.C, C * m.B + D * m.D,
            A * m.Tx + B * m.Ty + Tx, C * m.Tx + D * m.Ty + Ty);
        public (double X, double Y) Apply((double X, double Y) p) => (A * p.X + B * p.Y + Tx, C * p.X + D * p.Y + Ty);
        public double LinearScale => Math.Sqrt(Math.Abs(A * D - B * C));
        public bool IsUniform
        {
            get
            {
                double sx = Math.Sqrt(A * A + C * C), sy = Math.Sqrt(B * B + D * D);
                return Math.Abs(sx - sy) <= 0.01 * Math.Max(sx, sy);
            }
        }
    }

    private readonly Dictionary<string, BlockDef> _blocks = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>handle BLOCK_RECORD → tên block (để tra tên gốc của block động *U)</summary>
    private readonly Dictionary<string, string> _brHandleToName = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>tên block ẩn danh → handle BLOCK_RECORD của block động gốc (XDATA AcDbBlockRepBTag)</summary>
    private readonly Dictionary<string, string> _anonToSrcHandle = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missingBlocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _xrefBlocks = new(StringComparer.OrdinalIgnoreCase);
    private bool _warnDepth, _warnCycle, _warnLimit, _warnMinsert;

    private static int _codePagesRegistered;

    private static void EnsureCodePages()
    {
        if (Interlocked.Exchange(ref _codePagesRegistered, 1) == 1) return;
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); } catch (Exception) { /* bỏ qua */ }
    }

    // ------------------------------------------------------------------ API

    /// <summary>Đọc file. scaleOverride &gt; 0: dùng hệ số này (đơn vị bản vẽ → m) thay cho $INSUNITS.</summary>
    public static DxfReader Read(string path, double scaleOverride = 0)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return FromBytes(bytes, scaleOverride);
    }

    /// <summary>Đọc từ nội dung file (byte). Tự nhận dạng bảng mã.</summary>
    public static DxfReader FromBytes(byte[] bytes, double scaleOverride = 0)
    {
        var r = new DxfReader();
        bytes ??= Array.Empty<byte>();
        if (StartsWith(bytes, "AutoCAD Binary DXF"))
        {
            r.Warnings.Add("File DXF dạng nhị phân (Binary DXF) – chưa hỗ trợ. Trong AutoCAD hãy SAVEAS → DXF, chọn Options → định dạng ASCII.");
            return r;
        }
        if (bytes.Length >= 6 && StartsWith(bytes, "AC10") && bytes[4] >= (byte)'0' && bytes[4] <= (byte)'9')
        {
            r.Warnings.Add("File này là bản vẽ DWG (không phải DXF). Không đọc trực tiếp được DWG – Lưu bản vẽ dạng DXF (SaveAs DXF) hoặc dùng add-in AutoCAD VXT; hoặc cài ODA File Converter (miễn phí, opendesign.com).");
            return r;
        }
        string text = r.Decode(bytes);
        r.Parse(SplitLines(text), scaleOverride);
        return r;
    }

    public static DxfReader FromLines(string[] lines, double scaleOverride = 0)
    {
        var r = new DxfReader();
        r.Parse(lines ?? Array.Empty<string>(), scaleOverride);
        return r;
    }

    // ------------------------------------------------------------------ giải mã

    private static bool StartsWith(byte[] b, string ascii)
    {
        if (b.Length < ascii.Length) return false;
        for (int i = 0; i < ascii.Length; i++) if (b[i] != (byte)ascii[i]) return false;
        return true;
    }

    private static string[] SplitLines(string s)
    {
        var list = new List<string>(s.Length / 8 + 16);
        int start = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\n')
            {
                int end = i;
                if (end > start && s[end - 1] == '\r') end--;
                list.Add(s.Substring(start, end - start));
                start = i + 1;
            }
        }
        if (start < s.Length)
        {
            int end = s.Length;
            if (end > start && s[end - 1] == '\r') end--;
            list.Add(s.Substring(start, end - start));
        }
        return list.ToArray();
    }

    /// <summary>Tìm giá trị biến HEADER trong phần đầu file (đọc dạng ASCII).</summary>
    private static string SniffHeaderVar(string head, string name)
    {
        int i = head.IndexOf(name, StringComparison.Ordinal);
        if (i < 0) return "";
        // sau tên biến: xuống dòng, mã nhóm, xuống dòng, giá trị
        int nl1 = head.IndexOf('\n', i);
        if (nl1 < 0) return "";
        int nl2 = head.IndexOf('\n', nl1 + 1);
        if (nl2 < 0) return "";
        int nl3 = head.IndexOf('\n', nl2 + 1);
        if (nl3 < 0) nl3 = head.Length;
        return head.Substring(nl2 + 1, nl3 - nl2 - 1).Trim();
    }

    private static int VersionNumber(string acadver)
    {
        // "AC1021" → 1021
        if (string.IsNullOrEmpty(acadver) || acadver.Length < 3) return 0;
        return int.TryParse(acadver.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
    }

    private static Encoding CodePageEncoding(string dwgCodePage)
    {
        EnsureCodePages();
        int cp = 1252;
        if (!string.IsNullOrEmpty(dwgCodePage))
        {
            string s = dwgCodePage.Trim().ToUpperInvariant();
            if (s.StartsWith("ANSI_") || s.StartsWith("DOS"))
            {
                string digits = new string(s.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0) cp = v;
            }
            else if (s == "BIG5") cp = 950;
            else if (s == "GB2312") cp = 936;
            else if (s == "KSC5601") cp = 949;
            else if (s == "JOHAB") cp = 1361;
            else if (s.StartsWith("UTF")) return new UTF8Encoding(false, false);
        }
        try { return Encoding.GetEncoding(cp); }
        catch (Exception)
        {
            try { return Encoding.GetEncoding(1252); } catch (Exception) { return Encoding.Latin1; }
        }
    }

    private string Decode(byte[] bytes)
    {
        int offset = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            EncodingUsed = "utf-8";
            return new UTF8Encoding(false, false).GetString(bytes, 3, bytes.Length - 3);
        }
        string head = Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 1 << 20));
        string ver = SniffHeaderVar(head, "$ACADVER");
        string cpName = SniffHeaderVar(head, "$DWGCODEPAGE");
        bool utf8BySpec = VersionNumber(ver) >= 1021;

        var strict = new UTF8Encoding(false, true);
        try
        {
            string s = strict.GetString(bytes, offset, bytes.Length - offset);
            EncodingUsed = "utf-8";
            return s;
        }
        catch (DecoderFallbackException)
        {
            var enc = CodePageEncoding(cpName);
            EncodingUsed = enc.WebName;
            if (utf8BySpec)
                Warnings.Add($"File DXF {ver} lẽ ra mã hoá UTF-8 nhưng có byte không hợp lệ – đọc theo bảng mã {enc.WebName}; chữ tiếng Việt có thể bị lỗi.");
            string s;
            try { s = enc.GetString(bytes, offset, bytes.Length - offset); }
            catch (Exception) { EncodingUsed = "iso-8859-1"; return Encoding.Latin1.GetString(bytes, offset, bytes.Length - offset); }
            // cp1258 ghi tiếng Việt bằng dấu tổ hợp (o + U+0300) → chuẩn hoá về ký tự dựng sẵn
            try { if (!s.IsNormalized(NormalizationForm.FormC)) s = s.Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { /* giữ nguyên */ }
            return s;
        }
    }

    // ------------------------------------------------------------------ phân tích

    private void Parse(string[] lines, double scaleOverride)
    {
        if (lines.Length > 0 && lines[0] != null && lines[0].StartsWith("AutoCAD Binary DXF", StringComparison.Ordinal))
        {
            Warnings.Add("File DXF dạng nhị phân (Binary DXF) – chưa hỗ trợ. Trong AutoCAD hãy SAVEAS → DXF, chọn Options → định dạng ASCII.");
            return;
        }

        _pairs.Capacity = Math.Max(_pairs.Capacity, lines.Length / 2 + 1);
        for (int i = 0; i + 1 < lines.Length; i += 2)
        {
            if (!int.TryParse((lines[i] ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
            {
                // dòng lệch cặp – thử dịch 1 dòng
                i -= 1;
                continue;
            }
            if (code == 999) continue; // chú thích
            _pairs.Add((code, (lines[i + 1] ?? "").Trim()));
        }

        // vị trí các SECTION
        var sections = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i + 1 < _pairs.Count; i++)
            if (_pairs[i].code == 0 && _pairs[i].value == "SECTION" && _pairs[i + 1].code == 2)
            {
                if (!sections.ContainsKey(_pairs[i + 1].value)) sections[_pairs[i + 1].value] = i + 2;
                i++;
            }

        if (sections.TryGetValue("HEADER", out int hp)) ReadHeader(hp);
        Scale = InsUnits switch { 1 => 0.0254, 2 => 0.3048, 4 => 0.001, 5 => 0.01, 6 => 1.0, 7 => 1000.0, 8 => 0.0254e-6, 9 => 0.0254e-3, 10 => 0.9144, 14 => 0.1, _ => 0.001 };
        if (scaleOverride > 0) Scale = scaleOverride;

        if (sections.TryGetValue("TABLES", out int tp)) ReadTables(tp);
        if (sections.TryGetValue("BLOCKS", out int bp)) ReadBlocks(bp);

        if (!sections.TryGetValue("ENTITIES", out int ep)) { Warnings.Add("File DXF không có mục ENTITIES"); return; }
        _pos = ep;
        var model = ReadEntityList();

        var root = Affine.Scaling(Scale, Scale);
        foreach (var re in model)
        {
            if (re.Paper) continue;
            if (!EmitOrExplode(re, root, 0, "", null, null, null)) break;
        }

        if (_missingBlocks.Count > 0)
            Warnings.Add($"Có {_missingBlocks.Count} block được chèn nhưng không có định nghĩa: {string.Join(", ", _missingBlocks.Take(5))}{(_missingBlocks.Count > 5 ? "…" : "")}");
        if (_xrefBlocks.Count > 0)
            Warnings.Add($"Bản vẽ có tham chiếu ngoài (xref) không được nạp: {string.Join(", ", _xrefBlocks.Take(5))}{(_xrefBlocks.Count > 5 ? "…" : "")}. Hãy BIND xref trước khi xuất DXF.");
    }

    private void ReadHeader(int start)
    {
        for (int i = start; i + 1 < _pairs.Count; i++)
        {
            var (code, value) = _pairs[i];
            if (code == 0) break; // ENDSEC
            if (code != 9) continue;
            var next = _pairs[i + 1];
            switch (value)
            {
                case "$INSUNITS": if (next.code == 70) InsUnits = ToInt(next.value); break;
                case "$ACADVER": if (next.code == 1) AcadVersion = next.value; break;
                case "$DWGCODEPAGE": if (next.code == 3) CodePage = next.value; break;
            }
        }
    }

    private void ReadTables(int start)
    {
        _pos = start;
        while (_pos < _pairs.Count)
        {
            var (code, value) = _pairs[_pos];
            if (code != 0) { _pos++; continue; }
            if (value == "ENDSEC" || value == "EOF") break;
            if (value == "LAYER")
            {
                var b = Body();
                string name = Find(b, 2);
                if (string.IsNullOrEmpty(name)) continue;
                LayerLineTypes[name] = Find(b, 6) ?? "";
                if ((ToInt(Find(b, 70)) & 1) != 0) FrozenLayers.Add(name);
                int aci = ToInt(Find(b, 62));
                if (aci < 0) OffLayers.Add(name);
                LayerColors[name] = Math.Abs(aci) is >= 1 and <= 255 ? Math.Abs(aci) : 7;
            }
            else if (value == "STYLE")
            {
                var b = Body();
                string name = Find(b, 2);
                if (string.IsNullOrEmpty(name)) continue;
                // tệp font (mã 3), tên họ font TrueType (XDATA 1000 sau 1001 ACAD)
                string font = (Find(b, 3) ?? "") + "|" + string.Join("|", b.Where(x => x.code == 1000).Select(x => x.value));
                string hint = FontHintOf(font);
                if (hint.Length > 0) StyleFontHints[name] = hint;
            }
            else if (value == "BLOCK_RECORD")
            {
                var b = Body();
                string name = Find(b, 2);
                string handle = Find(b, 5);
                if (string.IsNullOrEmpty(name)) continue;
                if (!string.IsNullOrEmpty(handle)) _brHandleToName[handle] = name;
                // XDATA của block động ẩn danh: 1001 AcDbBlockRepBTag, 1070 …, 1005 <handle block gốc>
                for (int k = 0; k < b.Count; k++)
                {
                    if (b[k].code == 1001 && b[k].value == "AcDbBlockRepBTag")
                    {
                        for (int m = k + 1; m < b.Count && b[m].code != 1001; m++)
                            if (b[m].code == 1005) { _anonToSrcHandle[name] = b[m].value; break; }
                        break;
                    }
                }
            }
            else Body();
        }
    }

    private void ReadBlocks(int start)
    {
        _pos = start;
        while (_pos < _pairs.Count)
        {
            var (code, value) = _pairs[_pos];
            if (code != 0) { _pos++; continue; }
            if (value == "ENDSEC" || value == "EOF") break;
            if (value != "BLOCK") { Body(); continue; }

            var b = Body();
            var def = new BlockDef
            {
                Name = Find(b, 2) ?? Find(b, 3) ?? "",
                Bx = Get(b, 10),
                By = Get(b, 20),
                Xref = (ToInt(Find(b, 70)) & (4 | 8)) != 0,
            };
            def.Ents = ReadEntityList();
            if (_pos < _pairs.Count && _pairs[_pos].code == 0 && _pairs[_pos].value == "ENDBLK") Body();
            if (def.Name.Length > 0) _blocks[def.Name] = def;
        }
    }

    /// <summary>Đọc danh sách thực thể từ _pos tới ENDSEC / ENDBLK / EOF (không tiêu thụ dấu kết thúc).</summary>
    private List<RawEnt> ReadEntityList()
    {
        var list = new List<RawEnt>();
        while (_pos < _pairs.Count)
        {
            var (code, value) = _pairs[_pos];
            if (code != 0) { _pos++; continue; }
            if (value == "ENDSEC" || value == "EOF" || value == "ENDBLK") break;
            RawEnt e = null;
            try
            {
                switch (value)
                {
                    case "LINE": e = ReadLine(); break;
                    case "LWPOLYLINE": e = ReadLwPolyline(); break;
                    case "POLYLINE": e = ReadPolyline(); break;
                    case "CIRCLE": e = ReadCircle(); break;
                    case "ARC": e = ReadArc(); break;
                    case "TEXT": e = ReadText(false); break;
                    case "MTEXT": e = ReadText(true); break;
                    case "INSERT":
                    case "MINSERT": e = ReadInsert(); break;
                    case "SOLID":
                    case "TRACE": e = ReadSolid(); break;
                    case "HATCH": list.AddRange(ReadHatch()); break;
                    default: Body(); break; // ATTDEF, DIMENSION, ELLIPSE, SPLINE, … bỏ qua
                }
            }
            catch (Exception)
            {
                // dữ liệu hỏng – bỏ thực thể, nhảy tới thực thể kế tiếp
                SkipToNextEntity();
                e = null;
            }
            if (e != null) list.Add(e);
        }
        return list;
    }

    private void SkipToNextEntity()
    {
        while (_pos < _pairs.Count && _pairs[_pos].code != 0) _pos++;
    }

    /// <summary>Đọc các cặp mã của một thực thể (từ sau mã 0 tới trước mã 0 kế tiếp).</summary>
    private List<(int code, string value)> Body()
    {
        _pos++;
        var b = new List<(int, string)>();
        while (_pos < _pairs.Count && _pairs[_pos].code != 0) { b.Add(_pairs[_pos]); _pos++; }
        return b;
    }

    private bool AtEntity(string name) => _pos < _pairs.Count && _pairs[_pos].code == 0 && _pairs[_pos].value == name;

    private static double ToD(string s) =>
        s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && !double.IsNaN(v) && !double.IsInfinity(v) ? v : 0;

    private static int ToInt(string s)
    {
        if (s == null) return 0;
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
        // một số chương trình ghi số nguyên dạng "1.0"
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && Math.Abs(d) < int.MaxValue ? (int)d : 0;
    }

    /// <summary>Giá trị đầu tiên có mã code, null nếu không có.</summary>
    private static string Find(List<(int code, string value)> b, int code)
    {
        for (int i = 0; i < b.Count; i++) if (b[i].code == code) return b[i].value;
        return null;
    }

    private static string Layer(List<(int code, string value)> b)
    {
        string s = Find(b, 8);
        return string.IsNullOrEmpty(s) ? "0" : s;
    }

    private static double Get(List<(int code, string value)> b, int code) => ToD(Find(b, code));

    private static double GetOr(List<(int code, string value)> b, int code, double def)
    {
        string s = Find(b, code);
        return s == null ? def : (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && !double.IsNaN(v) && !double.IsInfinity(v) ? v : def);
    }

    /// <summary>Hướng đùn (210/220/230) ngược trục Z → hệ OCS bị lật trục X so với WCS.</summary>
    private static bool Flipped(List<(int code, string value)> b) => GetOr(b, 230, 1) < 0;

    private static RawEnt Common(List<(int code, string value)> b, DxfEntityType t) => new()
    {
        Type = t,
        Layer = Layer(b),
        LineType = Find(b, 6) ?? "",
        Paper = ToInt(Find(b, 67)) == 1,
    };

    private RawEnt ReadLine()
    {
        var b = Body();
        var e = Common(b, DxfEntityType.Line);
        e.Source = "LINE";
        e.Pts.Add((Get(b, 10), Get(b, 20)));   // LINE ở WCS, bỏ Z
        e.Pts.Add((Get(b, 11), Get(b, 21)));
        return e;
    }

    private RawEnt ReadLwPolyline()
    {
        var b = Body();
        var e = Common(b, DxfEntityType.Polyline);
        e.Source = "LWPOLYLINE";
        e.Closed = (ToInt(Find(b, 70)) & 1) == 1;
        e.Width = Math.Abs(Get(b, 43));
        bool flip = Flipped(b);
        var verts = new List<(double X, double Y, double Bulge)>();
        double? vx = null;
        foreach (var (code, value) in b)
        {
            if (code == 10) vx = ToD(value);
            else if (code == 20 && vx.HasValue) { verts.Add((vx.Value, ToD(value), 0)); vx = null; }
            else if (code == 42 && verts.Count > 0) { var v = verts[^1]; verts[^1] = (v.X, v.Y, ToD(value)); }
        }
        BuildPolyline(e, verts, flip);
        return e.Pts.Count >= 2 ? e : null;
    }

    private RawEnt ReadPolyline()
    {
        var b = Body();
        var e = Common(b, DxfEntityType.Polyline);
        int flag = ToInt(Find(b, 70));
        e.Source = "POLYLINE";
        e.Closed = (flag & 1) == 1;
        double w0 = Math.Abs(Get(b, 40)), w1 = Math.Abs(Get(b, 41));
        if (w0 > 0 && Math.Abs(w0 - w1) <= 1e-9 * Math.Max(1, w0)) e.Width = w0;
        bool mesh = (flag & (16 | 64)) != 0;
        bool is3d = (flag & 8) != 0;
        bool flip = !is3d && Flipped(b);
        var verts = new List<(double X, double Y, double Bulge)>();
        while (AtEntity("VERTEX"))
        {
            var v = Body();
            int vf = ToInt(Find(v, 70));
            if ((vf & 128) != 0 && (vf & 64) == 0) continue; // đỉnh mặt lưới – bỏ
            if ((vf & 16) != 0) continue; // điểm điều khiển spline
            verts.Add((Get(v, 10), Get(v, 20), Get(v, 42)));
        }
        if (AtEntity("SEQEND")) Body();
        if (mesh) return null;
        BuildPolyline(e, verts, flip);
        return e.Pts.Count >= 2 ? e : null;
    }

    /// <summary>Dựng đỉnh polyline (có cung lồi – bulge) trong OCS rồi đổi sang WCS.</summary>
    private static void BuildPolyline(RawEnt e, List<(double X, double Y, double Bulge)> v, bool flip)
    {
        int n = v.Count;
        for (int i = 0; i < n; i++)
        {
            e.Pts.Add((v[i].X, v[i].Y));
            double bulge = v[i].Bulge;
            bool hasNext = i + 1 < n || e.Closed;
            if (Math.Abs(bulge) > 1e-9 && hasNext && n >= 2)
            {
                var q = v[(i + 1) % n];
                AddBulgePoints(e.Pts, v[i].X, v[i].Y, q.X, q.Y, bulge);
            }
        }
        if (flip) for (int i = 0; i < e.Pts.Count; i++) e.Pts[i] = (-e.Pts[i].X, e.Pts[i].Y);
        CloseIfRepeated(e);
    }

    /// <summary>Thêm các điểm trung gian (không gồm 2 đầu) của cung bulge từ p1 tới p2.</summary>
    private static void AddBulgePoints(List<(double X, double Y)> pts, double x1, double y1, double x2, double y2, double bulge)
    {
        double dx = x2 - x1, dy = y2 - y1, c = Math.Sqrt(dx * dx + dy * dy);
        if (c < 1e-12) return;
        bulge = Math.Clamp(bulge, -1000, 1000);
        double theta = 4 * Math.Atan(bulge);              // góc ở tâm, dương = ngược chiều kim đồng hồ
        double k = (1 - bulge * bulge) / (4 * bulge);     // khoảng cách tâm–trung điểm / dây cung
        double cx = (x1 + x2) / 2 - dy * k, cy = (y1 + y2) / 2 + dx * k;
        double r = Math.Sqrt((x1 - cx) * (x1 - cx) + (y1 - cy) * (y1 - cy));
        double a0 = Math.Atan2(y1 - cy, x1 - cx);
        int seg = Math.Max(1, (int)Math.Ceiling(Math.Abs(theta) * 180 / Math.PI / ArcStepDeg));
        for (int s = 1; s < seg; s++)
        {
            double a = a0 + theta * s / seg;
            pts.Add((cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
        }
    }

    private static void CloseIfRepeated(RawEnt e)
    {
        if (e.Pts.Count > 2)
        {
            var a = e.Pts[0]; var z = e.Pts[^1];
            double tol = 1e-6 * Math.Max(1.0, Math.Max(Math.Abs(a.X), Math.Abs(a.Y)));
            if (Math.Abs(a.X - z.X) < tol && Math.Abs(a.Y - z.Y) < tol)
            {
                e.Pts.RemoveAt(e.Pts.Count - 1);
                e.Closed = true;
            }
        }
    }

    private RawEnt ReadCircle()
    {
        var b = Body();
        var e = Common(b, DxfEntityType.Circle);
        e.Source = "CIRCLE";
        e.Radius = Get(b, 40);
        double x = Get(b, 10), y = Get(b, 20);
        e.Pts.Add((Flipped(b) ? -x : x, y));
        return e;
    }

    private RawEnt ReadArc()
    {
        var b = Body();
        var e = Common(b, DxfEntityType.Polyline);
        e.Source = "ARC";
        double cx = Get(b, 10), cy = Get(b, 20), r = Get(b, 40);
        double a1 = Get(b, 50), a2 = Get(b, 51);
        if (r <= 0) return null;
        double sweep = ((a2 - a1) % 360 + 360) % 360;   // không dùng vòng lặp: an toàn với giá trị hỏng rất lớn
        if (sweep < 1e-9) sweep = 360;
        int seg = Math.Max(1, (int)Math.Ceiling(sweep / ArcStepDeg - 1e-9));
        bool flip = Flipped(b);
        for (int s = 0; s <= seg; s++)
        {
            double a = (a1 + sweep * s / seg) * Math.PI / 180.0;
            double x = cx + r * Math.Cos(a), y = cy + r * Math.Sin(a);
            e.Pts.Add((flip ? -x : x, y));
        }
        return e;
    }

    private RawEnt ReadText(bool mtext)
    {
        var b = Body();
        TruncateEmbedded(b);
        var e = Common(b, DxfEntityType.Text);
        e.Source = mtext ? "MTEXT" : "TEXT";
        e.Style = Find(b, 7) ?? "";
        e.Height = Get(b, 40);
        // TEXT: nếu có căn lề (72/73 ≠ 0) thì điểm căn là 11/21
        bool aligned = !mtext && b.Any(x => (x.code == 72 || x.code == 73) && ToInt(x.value) != 0) && Find(b, 11) != null;
        double px = aligned ? Get(b, 11) : Get(b, 10);
        double py = aligned ? Get(b, 21) : Get(b, 20);
        // MTEXT: điểm chèn ở WCS; TEXT: ở OCS
        e.Pts.Add((!mtext && Flipped(b) ? -px : px, py));
        var sb = new StringBuilder();
        foreach (var (code, value) in b) if (code == 3) sb.Append(value);
        foreach (var (code, value) in b) if (code == 1) sb.Append(value);
        // font ghi ngay trong MTEXT: {\fVNI-Times|b0|i0|c0|p18;...} hoặc {\F.VnTime.shx;...}
        var fm = System.Text.RegularExpressions.Regex.Match(sb.ToString(), @"\\[fF]([^|;]*)");
        if (fm.Success) e.FontHint = FontHintOf(fm.Groups[1].Value);
        e.Text = CleanMText(sb.ToString());
        return e.Text.Length > 0 ? e : null;
    }

    /// <summary>Cắt bỏ phần "Embedded Object" (mã 101) của MTEXT/ATTRIB nhiều dòng (bản sao dữ liệu).</summary>
    private static void TruncateEmbedded(List<(int code, string value)> b)
    {
        for (int i = 0; i < b.Count; i++)
            if (b[i].code == 101) { b.RemoveRange(i, b.Count - i); return; }
    }

    private RawEnt ReadInsert()
    {
        var b = Body();
        var e = Common(b, DxfEntityType.Line);
        e.Source = "INSERT";
        e.IsInsert = true;
        e.BlockName = Find(b, 2) ?? "";
        e.Ix = Get(b, 10); e.Iy = Get(b, 20);
        e.Sx = GetOr(b, 41, 1); e.Sy = GetOr(b, 42, 1);
        if (e.Sx == 0) e.Sx = 1;
        if (e.Sy == 0) e.Sy = 1;
        e.Rot = Get(b, 50);
        e.Cols = Math.Max(1, ToInt(Find(b, 70)));
        e.Rows = Math.Max(1, ToInt(Find(b, 71)));
        e.ColSp = Get(b, 44); e.RowSp = Get(b, 45);
        e.Flip = Flipped(b);
        bool hasAttribs = ToInt(Find(b, 66)) == 1;
        // ATTRIB … SEQEND (đọc cả khi thiếu cờ 66 nhưng ATTRIB đi ngay sau)
        if (hasAttribs || AtEntity("ATTRIB"))
        {
            while (AtEntity("ATTRIB"))
            {
                var a = ReadAttrib();
                if (a != null) (e.Attribs ??= new List<RawEnt>()).Add(a);
            }
            if (AtEntity("SEQEND")) Body();
        }
        return e;
    }

    private RawEnt ReadAttrib()
    {
        var b = Body();
        TruncateEmbedded(b);
        var e = Common(b, DxfEntityType.Text);
        e.Source = "ATTRIB";
        e.Style = Find(b, 7) ?? "";
        if ((ToInt(Find(b, 70)) & 1) != 0) return null; // thuộc tính ẩn
        e.Height = Get(b, 40);
        bool aligned = b.Any(x => (x.code == 72 || x.code == 74) && ToInt(x.value) != 0) && Find(b, 11) != null;
        double px = aligned ? Get(b, 11) : Get(b, 10);
        double py = aligned ? Get(b, 21) : Get(b, 20);
        e.Pts.Add((Flipped(b) ? -px : px, py));
        e.Tag = Find(b, 2) ?? "";
        e.Text = CleanMText(Find(b, 1) ?? "");
        return e.Text.Length > 0 ? e : null;
    }

    /// <summary>"TCVN3" với font .VnTime/.VnArial… (hoặc VN3, ABC), "VNI" với VNI-Times…, "" nếu không rõ.</summary>
    internal static string FontHintOf(string font)
    {
        string f = (font ?? "").Trim().ToUpperInvariant();
        if (f.Length == 0) return "";
        if (f.Contains("VNI")) return "VNI";
        if (f.Contains(".VN") || f.StartsWith("VN", StringComparison.Ordinal) || f.Contains("|VN") || f.Contains("TCVN3") || f.Contains("ABC")) return "TCVN3";
        return "";
    }

    /// <summary>SOLID / TRACE: 4 đỉnh theo thứ tự DXF 1-2-3-4, chu vi là 1→2→4→3.</summary>
    private RawEnt ReadSolid()
    {
        var b = Body();
        var e = Common(b, DxfEntityType.Polyline);
        e.Source = "SOLID";
        e.IsFill = true;
        e.Closed = true;
        bool flip = Flipped(b);
        var p = new List<(double X, double Y)> { (Get(b, 10), Get(b, 20)), (Get(b, 11), Get(b, 21)), (Get(b, 13), Get(b, 23)), (Get(b, 12), Get(b, 22)) };
        foreach (var q in p)
        {
            var v = flip ? (-q.X, q.Y) : q;
            if (e.Pts.Count == 0 || Math.Abs(e.Pts[^1].X - v.Item1) > 1e-9 || Math.Abs(e.Pts[^1].Y - v.Item2) > 1e-9) e.Pts.Add(v);
        }
        if (e.Pts.Count > 2 && Math.Abs(e.Pts[0].X - e.Pts[^1].X) < 1e-9 && Math.Abs(e.Pts[0].Y - e.Pts[^1].Y) < 1e-9) e.Pts.RemoveAt(e.Pts.Count - 1);
        return e.Pts.Count >= 3 ? e : null;
    }

    /// <summary>HATCH: mỗi vòng biên (dạng đa tuyến, hoặc gồm cạnh thẳng/cung tròn) → 1 đa tuyến kín.
    /// Vòng có cạnh elip/spline bỏ qua. Đọc tuần tự theo đặc tả DXF (91 số vòng, 92 cờ vòng, 93 số đỉnh/cạnh, 72 loại cạnh…).</summary>
    private List<RawEnt> ReadHatch()
    {
        var b = Body();
        var res = new List<RawEnt>();
        bool flip = Flipped(b);
        int j = b.FindIndex(x => x.code == 91);
        if (j < 0) return res;
        int nLoops = Math.Clamp(ToInt(b[j].value), 0, 10000);
        j++;
        // giá trị kế tiếp có mã code (bỏ qua mã khác xen giữa); -1 = hết
        string Next(int code)
        {
            while (j < b.Count && b[j].code != code)
            {
                if (b[j].code == 75 || b[j].code == 98) return null;   // hết phần biên (kiểu tô / điểm hạt giống)
                j++;
            }
            if (j >= b.Count) return null;
            return b[j++].value;
        }
        double D(int code) => ToD(Next(code));
        for (int l = 0; l < nLoops; l++)
        {
            string fl = Next(92);
            if (fl == null) break;
            int flags = ToInt(fl);
            var e = Common(b, DxfEntityType.Polyline);
            e.Source = "HATCH"; e.IsFill = true; e.Closed = true;
            bool ok = true;
            if ((flags & 2) != 0)
            {
                bool hasBulge = ToInt(Next(72)) != 0;
                Next(73);
                int nv = Math.Clamp(ToInt(Next(93)), 0, 100000);
                var verts = new List<(double X, double Y, double Bulge)>();
                for (int k = 0; k < nv; k++)
                {
                    double x = D(10), y = D(20), bu = 0;
                    if (hasBulge && j < b.Count && b[j].code == 42) bu = ToD(b[j++].value);
                    verts.Add((x, y, bu));
                }
                BuildPolyline(e, verts, flip);
            }
            else
            {
                int ne = Math.Clamp(ToInt(Next(93)), 0, 100000);
                for (int k = 0; k < ne && ok; k++)
                {
                    int et = ToInt(Next(72));
                    switch (et)
                    {
                        case 1:
                        {
                            double x1 = D(10), y1 = D(20), x2 = D(11), y2 = D(21);
                            AddPt(e.Pts, x1, y1, flip); AddPt(e.Pts, x2, y2, flip);
                            break;
                        }
                        case 2:
                        {
                            double cx = D(10), cy = D(20), r = D(40), a1 = D(50), a2 = D(51);
                            bool ccw = ToInt(Next(73)) != 0;
                            double sweep = ((a2 - a1) % 360 + 360) % 360;
                            if (!ccw) sweep = -sweep;
                            if (Math.Abs(sweep) < 1e-9) sweep = ccw ? 360 : -360;
                            int seg = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / ArcStepDeg));
                            // cung theo chiều kim đồng hồ: góc ghi theo hệ lật (DXF), quy về góc thực
                            double s0 = ccw ? a1 : -a1;
                            for (int q = 0; q <= seg; q++)
                            {
                                double a = (s0 + sweep * q / seg) * Math.PI / 180;
                                AddPt(e.Pts, cx + r * Math.Cos(a), cy + r * Math.Sin(a), flip);
                            }
                            break;
                        }
                        default: ok = false; break;   // elip, spline – bỏ vòng này
                    }
                }
                CloseIfRepeated(e);
            }
            if (ok && e.Pts.Count >= 3) res.Add(e);
            if (!ok) break;   // không đọc tiếp được đúng vị trí các vòng sau
        }
        return res;
    }

    private static void AddPt(List<(double X, double Y)> pts, double x, double y, bool flip)
    {
        if (flip) x = -x;
        if (pts.Count > 0 && Math.Abs(pts[^1].X - x) < 1e-9 && Math.Abs(pts[^1].Y - y) < 1e-9) return;
        pts.Add((x, y));
    }

    // ------------------------------------------------------------------ phân rã block

    private static bool IsLayoutBlock(string name) =>
        name.StartsWith("*Model_Space", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase);

    private string EffectiveBlockName(string name)
    {
        if (name.StartsWith("*", StringComparison.Ordinal) &&
            _anonToSrcHandle.TryGetValue(name, out string h) && _brHandleToName.TryGetValue(h, out string src) && src.Length > 0)
            return src;
        return name;
    }

    private string ResolveLayer(string own, string parentLayer) =>
        own == "0" && parentLayer != null ? parentLayer : own;

    private string ResolveLineType(string own, string effLayer, string parentLt)
    {
        string lt = own ?? "";
        if (lt.Equals("BYBLOCK", StringComparison.OrdinalIgnoreCase) && parentLt != null) return parentLt;
        if (lt.Length == 0 || lt.Equals("BYLAYER", StringComparison.OrdinalIgnoreCase) || lt.Equals("BYBLOCK", StringComparison.OrdinalIgnoreCase))
            return LayerLineTypes.TryGetValue(effLayer, out string l) ? l : "";
        return lt;
    }

    /// <summary>Xuất 1 thực thể (hoặc phân rã INSERT). Trả false khi vượt giới hạn số thực thể.</summary>
    private bool EmitOrExplode(RawEnt re, Affine m, int depth, string outerBlock, string parentLayer, string parentLt, HashSet<string> stack)
    {
        if (Entities.Count >= MaxEntities)
        {
            if (!_warnLimit) { _warnLimit = true; Warnings.Add($"Bản vẽ quá lớn – chỉ đọc {MaxEntities:N0} thực thể đầu tiên."); }
            return false;
        }
        string layer = ResolveLayer(re.Layer, parentLayer);
        if (FrozenLayers.Contains(layer)) return true;
        string lt = ResolveLineType(re.LineType, layer, parentLt);

        if (!re.IsInsert)
        {
            Emit(re, m, layer, lt, outerBlock);
            return true;
        }

        // thuộc tính ATTRIB: toạ độ đã ở không gian chứa INSERT → dùng phép biến đổi cha
        string blockLabel = outerBlock.Length > 0 ? outerBlock : EffectiveBlockName(re.BlockName);
        if (re.Attribs != null)
            foreach (var a in re.Attribs)
            {
                string al = a.Layer == "0" ? layer : a.Layer;
                if (FrozenLayers.Contains(al)) continue;
                var ent = Emit(a, m, al, ResolveLineType(a.LineType, al, lt), blockLabel);
                if (ent != null) ent.AttributeTag = a.Tag;
            }

        string name = re.BlockName;
        if (name.Length == 0 || IsLayoutBlock(name)) return true;
        if (!_blocks.TryGetValue(name, out var def)) { _missingBlocks.Add(name); return true; }
        if (def.Xref) { _xrefBlocks.Add(name); if (def.Ents.Count == 0) return true; }
        if (depth >= MaxDepth)
        {
            if (!_warnDepth) { _warnDepth = true; Warnings.Add($"Block lồng nhau quá {MaxDepth} cấp – bỏ phần sâu hơn."); }
            return true;
        }
        stack ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!stack.Add(name))
        {
            if (!_warnCycle) { _warnCycle = true; Warnings.Add($"Block '{name}' tự tham chiếu (vòng lặp) – bỏ qua."); }
            return true;
        }

        try
        {
            int cols = re.Cols, rows = re.Rows;
            if ((long)cols * rows > MaxMinsertCells)
            {
                if (!_warnMinsert) { _warnMinsert = true; Warnings.Add($"MINSERT '{name}' có {cols}×{rows} ô – chỉ phân rã 1 ô."); }
                cols = 1; rows = 1;
            }
            var pre = m;
            if (re.Flip) pre = pre.Then(Affine.Scaling(-1, 1));
            pre = pre.Then(Affine.Translate(re.Ix, re.Iy)).Then(Affine.Rotation(re.Rot));
            var post = Affine.Scaling(re.Sx, re.Sy).Then(Affine.Translate(-def.Bx, -def.By));
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    var mc = pre.Then(Affine.Translate(c * re.ColSp, r * re.RowSp)).Then(post);
                    foreach (var child in def.Ents)
                        if (!EmitOrExplode(child, mc, depth + 1, blockLabel, layer, lt, stack)) return false;
                }
        }
        finally { stack.Remove(name); }
        return true;
    }

    private DxfEntity Emit(RawEnt re, Affine m, string layer, string lt, string block)
    {
        var e = new DxfEntity
        {
            Type = re.Type, Layer = layer, LineType = lt, Block = block, Closed = re.Closed, SourceType = re.Source ?? "",
            Width = re.Width * m.LinearScale,
            FontHint = re.FontHint.Length > 0 ? re.FontHint : StyleFontHints.TryGetValue(re.Style ?? "", out var fh) ? fh : ""
        };
        switch (re.Type)
        {
            case DxfEntityType.Circle:
                if (!m.IsUniform)
                {
                    // block co giãn không đều → hình tròn thành elip: xấp xỉ bằng polyline kín
                    e.Type = DxfEntityType.Polyline;
                    e.Closed = true;
                    var c0 = re.Pts[0];
                    int nseg = (int)Math.Ceiling(360 / ArcStepDeg);
                    for (int s = 0; s < nseg; s++)
                    {
                        double a = 2 * Math.PI * s / nseg;
                        e.Points.Add(m.Apply((c0.X + re.Radius * Math.Cos(a), c0.Y + re.Radius * Math.Sin(a))));
                    }
                }
                else
                {
                    e.Points.Add(m.Apply(re.Pts[0]));
                    e.Radius = re.Radius * m.LinearScale;
                }
                break;
            case DxfEntityType.Text:
                e.Points.Add(m.Apply(re.Pts[0]));
                e.Text = re.Text;
                e.TextHeight = re.Height * m.LinearScale;
                break;
            default:
                e.Points.Capacity = re.Pts.Count;
                foreach (var p in re.Pts) e.Points.Add(m.Apply(p));
                break;
        }
        if (e.Points.Count == 0) return null;
        if (re.IsFill) FillEntities.Add(e); else Entities.Add(e);
        return e;
    }

    // ------------------------------------------------------------------ văn bản

    /// <summary>Giải mã \U+XXXX (Unicode) và \M+nXXXX (mã đa byte theo bảng mã) trong chuỗi DXF.</summary>
    public static string DecodeDxfEscapes(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0) return s ?? "";
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 2 < s.Length && s[i + 2] == '+')
            {
                char k = s[i + 1];
                if ((k == 'U' || k == 'u') && TryHex(s, i + 3, 4, out int u))
                {
                    sb.Append((char)u);
                    i += 6;
                    continue;
                }
                if ((k == 'M' || k == 'm') && i + 3 < s.Length && s[i + 3] >= '1' && s[i + 3] <= '5' && TryHex(s, i + 4, 4, out int mb))
                {
                    int cp = s[i + 3] switch { '1' => 932, '2' => 950, '3' => 949, '4' => 1361, _ => 936 };
                    string dec = null;
                    try
                    {
                        EnsureCodePages();
                        dec = Encoding.GetEncoding(cp).GetString(new[] { (byte)(mb >> 8), (byte)(mb & 0xFF) });
                    }
                    catch (Exception) { dec = null; }
                    if (!string.IsNullOrEmpty(dec)) { sb.Append(dec); i += 7; continue; }
                }
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static bool TryHex(string s, int start, int len, out int v)
    {
        v = 0;
        if (start < 0 || start + len > s.Length) return false;
        for (int i = start; i < start + len; i++)
        {
            char c = s[i];
            int d = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
            if (d < 0) return false;
            v = v * 16 + d;
        }
        return true;
    }

    /// <summary>Bỏ mã định dạng MTEXT (\P, \f...;, {}, \S..;, %%c...), giải mã \U+XXXX.</summary>
    public static string CleanMText(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = DecodeDxfEscapes(s);
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '{' || c == '}') continue;
            if (c == '\\' && i + 1 < s.Length)
            {
                char k = s[i + 1];
                // \p…; (chữ thường) là định dạng đoạn văn (thụt lề, căn lề, tab) – không phải xuống dòng
                if (k == 'p' && s.IndexOf(';', i + 2) > 0 && (i + 2 < s.Length && "xilqtcrjd*0123456789-,.".IndexOf(s[i + 2]) >= 0))
                { i = s.IndexOf(';', i + 2); continue; }
                if (k == 'P' || k == 'p' || k == 'X' || k == 'N') { sb.Append(' '); i++; continue; }
                if (k == '~') { sb.Append(' '); i++; continue; }
                if (k == '\\' || k == '{' || k == '}') { sb.Append(k); i++; continue; }
                if (k == 'S')
                {
                    // phân số xếp chồng \Sa^b; \Sa/b; \Sa#b;
                    int semi = s.IndexOf(';', i + 2);
                    if (semi > 0)
                    {
                        sb.Append(s.Substring(i + 2, semi - i - 2).Replace('^', '/').Replace('#', '/'));
                        i = semi;
                        continue;
                    }
                }
                if ("fFHhCcTtQqWwAa".IndexOf(k) >= 0)
                {
                    int semi = s.IndexOf(';', i);
                    if (semi > 0) { i = semi; continue; }
                }
                if (k == 'L' || k == 'l' || k == 'O' || k == 'o' || k == 'K' || k == 'k') { i++; continue; }
            }
            sb.Append(c);
        }
        return ReplacePercentCodes(sb.ToString()).Trim();
    }

    /// <summary>Mã điều khiển kiểu TEXT: %%c Ø, %%d °, %%p ±, %%u/%%o/%%k (gạch chân… – bỏ), %%nnn (mã ký tự), %%% → %.</summary>
    private static string ReplacePercentCodes(string s)
    {
        if (s.IndexOf("%%", StringComparison.Ordinal) < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '%' && i + 2 < s.Length && s[i + 1] == '%')
            {
                char k = s[i + 2];
                switch (k)
                {
                    case 'c': case 'C': sb.Append('Ø'); i += 2; continue;
                    case 'd': case 'D': sb.Append('°'); i += 2; continue;
                    case 'p': case 'P': sb.Append('±'); i += 2; continue;
                    case 'u': case 'U': case 'o': case 'O': case 'k': case 'K': i += 2; continue;
                    case '%': sb.Append('%'); i += 2; continue;
                }
                if (char.IsDigit(k))
                {
                    int j = i + 2, v = 0;
                    while (j < s.Length && j < i + 5 && char.IsDigit(s[j])) { v = v * 10 + (s[j] - '0'); j++; }
                    if (v > 0 && v < 65536) { sb.Append((char)v); i = j - 1; continue; }
                }
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }
}
