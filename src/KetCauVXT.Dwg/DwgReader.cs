using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ACadSharp;
using ACadSharp.Blocks;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CSMath;
using KetCauVXT.Import;
using AcIO = ACadSharp.IO;

namespace KetCauVXT.Dwg;

/// <summary>Đọc bản vẽ DWG (AutoCAD R14 … 2018+, mã AC1014 – AC1032) bằng thư viện ACadSharp (MIT) và đổi sang cùng dạng dữ liệu
/// mà bộ đọc DXF của lõi (<see cref="KetCauVXT.Import.DxfReader"/>) tạo ra:
/// <list type="bullet">
/// <item>toạ độ m (theo $INSUNITS, mặc định mm; có thể ghi đè hệ số), chỉ model space, bỏ Z;</item>
/// <item>LINE, LWPOLYLINE, POLYLINE 2D/3D (cung lồi bulge chia đoạn 15°), ARC (→ đa tuyến), CIRCLE, TEXT, MTEXT (bỏ mã định dạng), ATTRIB;</item>
/// <item>INSERT/MINSERT phân rã về toạ độ thế giới (kể cả block lồng nhau, tối đa 8 cấp), thực thể mang tên block ngoài cùng
/// (block động *U đổi về tên block gốc), thực thể lớp "0" trong block lấy lớp của INSERT, kiểu đường BYLAYER/BYBLOCK được thay;</item>
/// <item>HATCH (biên), SOLID → miền tô (FillEntities); DIMENSION, ATTDEF, ELLIPSE, SPLINE… bỏ qua;</item>
/// <item>lớp đóng băng (frozen): bỏ thực thể; lớp tắt (off): vẫn đọc, ghi vào OffLayers để người gọi tự lọc; xref: bỏ, có cảnh báo.</item>
/// </list>
/// Đăng ký vào lõi: <c>KetCauVXT.Import.CadFileReaders.Dwg = DwgReader.Read;</c></summary>
public static class DwgReader
{
    /// <summary>Lời khuyên khi không đọc được DWG.</summary>
    public const string SaveAsDxfHint = "Hãy mở bản vẽ bằng AutoCAD và lưu sang DXF (SAVEAS → DXF, định dạng ASCII) rồi nhập lại.";

    private const int MaxDepth = 8;
    private const int MaxEntities = 5_000_000;
    private const int MaxMinsertCells = 10_000;
    private const double ArcStepDeg = 15.0;
    private const int MaxNotices = 5;

    /// <summary>Đăng ký bộ đọc DWG này vào lõi (CadFileReaders.Dwg). Gọi một lần khi khởi động.</summary>
    public static void Register() => CadFileReaders.Dwg = Read;

    // ==================================================================
    // PHIÊN BẢN
    // ==================================================================

    /// <summary>Chữ ký 6 byte đầu file DWG ("AC1032"…); "" nếu không đọc được.</summary>
    public static string ReadSignature(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var b = new byte[6];
            int n = fs.Read(b, 0, 6);
            if (n < 6) return "";
            var s = Encoding.ASCII.GetString(b, 0, 6);
            return s.All(c => c >= 32 && c < 127) ? s : "";
        }
        catch (Exception) { return ""; }
    }

    /// <summary>Tên phiên bản AutoCAD ứng với mã DWG.</summary>
    public static string VersionName(string sig) => sig switch
    {
        "AC1012" => "AutoCAD R13",
        "AC1014" => "AutoCAD R14",
        "AC1015" => "AutoCAD 2000",
        "AC1018" => "AutoCAD 2004",
        "AC1021" => "AutoCAD 2007",
        "AC1024" => "AutoCAD 2010",
        "AC1027" => "AutoCAD 2013",
        "AC1032" => "AutoCAD 2018",
        "AC1009" => "AutoCAD R11/R12",
        "AC1006" => "AutoCAD R10",
        "AC1004" => "AutoCAD R9",
        "AC1003" => "AutoCAD 2.6",
        "AC1002" => "AutoCAD 2.5",
        _ => sig ?? ""
    };

    /// <summary>Các mã phiên bản DWG đọc được.</summary>
    public static readonly string[] SupportedVersions = { "AC1012", "AC1014", "AC1015", "AC1018", "AC1021", "AC1024", "AC1027", "AC1032" };

    private static Exception Fail(string file, string sig, string reason, Exception inner = null)
    {
        string ver = string.IsNullOrEmpty(sig) ? "" : $" ({VersionName(sig)}, {sig})";
        return new InvalidDataException($"Không đọc được DWG '{file}'{ver}: {reason}. {SaveAsDxfHint}", inner);
    }

    // ==================================================================
    // MỞ FILE
    // ==================================================================

    /// <summary>Mở DWG thành CadDocument của ACadSharp. warnings: nhận các thông báo lỗi/cảnh báo đáng kể của thư viện (rút gọn).
    /// Ném InvalidDataException (tiếng Việt) khi file hỏng, có mật khẩu hoặc phiên bản chưa hỗ trợ.</summary>
    public static CadDocument OpenDocument(string path, List<string> warnings = null)
    {
        string file = Path.GetFileName(path ?? "");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException($"Không tìm thấy file '{path}'.", path);
        string sig = ReadSignature(path);
        if (sig.Length != 6 || !sig.StartsWith("AC", StringComparison.Ordinal))
            throw Fail(file, "", "file không phải bản vẽ DWG hợp lệ (thiếu chữ ký AC10xx) hoặc đã bị hỏng");
        if (string.CompareOrdinal(sig, "AC1012") < 0)
            throw Fail(file, sig, "phiên bản quá cũ, chỉ đọc được DWG từ AutoCAD R14 (AC1014) trở lên");
        if (!SupportedVersions.Contains(sig))
            throw Fail(file, sig, "phiên bản DWG chưa được hỗ trợ (đọc được AC1014 … AC1032 – AutoCAD R14 tới 2018 và mới hơn)");

        byte[] bytes;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var ms = new MemoryStream(fs.Length > 0 && fs.Length < int.MaxValue ? (int)fs.Length : 0);
            fs.CopyTo(ms);
            bytes = ms.ToArray();
        }
        catch (Exception ex) { throw Fail(file, sig, "không mở được file (" + ex.Message + ")", ex); }

        int nErr = 0, nWarn = 0;
        var firstErrors = new List<string>();
        void OnNotice(object sender, AcIO.NotificationEventArgs e)
        {
            if (e == null) return;
            if (e.NotificationType == AcIO.NotificationType.Error)
            {
                nErr++;
                if (firstErrors.Count < MaxNotices) firstErrors.Add(e.Message + (e.Exception != null ? " – " + e.Exception.Message : ""));
            }
            else if (e.NotificationType == AcIO.NotificationType.Warning) nWarn++;
        }

        CadDocument doc;
        try
        {
            var cfg = new AcIO.DwgReaderConfiguration { Failsafe = true, ReadSummaryInfo = false };
            using var ms = new MemoryStream(bytes, false);
            doc = AcIO.DwgReader.Read(ms, cfg, OnNotice);
        }
        catch (Exception ex)
        {
            throw Fail(file, sig, "bản vẽ hỏng, có mật khẩu hoặc chứa dữ liệu thư viện ACadSharp chưa đọc được (" + ex.GetType().Name + ": " + ex.Message + ")", ex);
        }
        if (doc == null) throw Fail(file, sig, "thư viện không trả về bản vẽ");
        if (warnings != null && nErr > 0)
            warnings.Add($"Bộ đọc DWG gặp {nErr} lỗi nhỏ (đã bỏ qua phần hỏng): " + string.Join("; ", firstErrors) + (nErr > firstErrors.Count ? "…" : ""));
        return doc;
    }

    // ==================================================================
    // ĐỌC → DxfReader (cùng quy ước bộ đọc DXF)
    // ==================================================================

    /// <summary>Đọc DWG → DxfReader của lõi (Entities, FillEntities, bảng layer, InsUnits, Scale, Warnings).
    /// scaleOverride &gt; 0: hệ số đơn vị bản vẽ → m thay cho $INSUNITS. Đúng chữ ký của <see cref="CadFileReaders.Dwg"/>.</summary>
    public static KetCauVXT.Import.DxfReader Read(string path, double scaleOverride = 0)
    {
        var warnings = new List<string>();
        var doc = OpenDocument(path, warnings);
        string sig = ReadSignature(path);
        var r = FromDocument(doc, scaleOverride, sig);
        r.Warnings.InsertRange(0, warnings);
        return r;
    }

    /// <summary>Đổi CadDocument (đọc từ DWG hoặc DXF bằng ACadSharp) → DxfReader của lõi.</summary>
    public static KetCauVXT.Import.DxfReader FromDocument(CadDocument doc, double scaleOverride = 0, string acadVersion = null)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        var r = new KetCauVXT.Import.DxfReader();
        int ins = 0;
        try { ins = (int)doc.Header.InsUnits; } catch (Exception) { ins = 0; }
        double scale = scaleOverride > 0 ? scaleOverride : KetCauVXT.Import.DxfReader.UnitScale(ins);
        string ver = acadVersion;
        if (string.IsNullOrEmpty(ver)) { try { ver = doc.Header.Version.ToString(); } catch (Exception) { ver = ""; } }
        string cp = "";
        try { cp = doc.Header.CodePage ?? ""; } catch (Exception) { cp = ""; }
        r.SetSourceInfo(ins, scale, ver, cp, "dwg");
        new Mapper(r, doc).Run(scale);
        return r;
    }

    /// <summary>Đọc DWG → danh sách thực thể (m). info: phiên bản, $INSUNITS, hệ số đơn vị, số thực thể, cảnh báo.</summary>
    public static List<DxfEntity> ReadEntities(string path, out string info) => ReadEntities(path, out info, out _);

    /// <summary>Như trên, kèm hệ số đơn vị bản vẽ → m đã dùng.</summary>
    public static List<DxfEntity> ReadEntities(string path, out string info, out double unitsToM)
    {
        var r = Read(path);
        unitsToM = r.Scale;
        info = Describe(r, Path.GetFileName(path));
        return r.Entities;
    }

    /// <summary>Mô tả ngắn kết quả đọc (một dòng + cảnh báo).</summary>
    public static string Describe(KetCauVXT.Import.DxfReader r, string file = "")
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(file)) sb.Append(file).Append(": ");
        sb.Append($"DWG {VersionName(r.AcadVersion)}, $INSUNITS = {r.InsUnits} → ×{r.Scale.ToString(CultureInfo.InvariantCulture)} (đơn vị → m), ");
        sb.Append($"{r.Entities.Count} thực thể, {r.FillEntities.Count} miền tô, {r.LayerColors.Count} layer");
        foreach (var w in r.Warnings) sb.Append("\n! ").Append(w);
        return sb.ToString();
    }

    // ==================================================================
    // CHUYỂN ĐỔI
    // ==================================================================

    /// <summary>DWG → DXF (ASCII, cùng phiên bản với DWG) bằng DxfWriter của ACadSharp. Trả mô tả ngắn.</summary>
    public static string ConvertToDxf(string dwgPath, string dxfOut)
    {
        var warnings = new List<string>();
        var doc = OpenDocument(dwgPath, warnings);
        string dir = Path.GetDirectoryName(Path.GetFullPath(dxfOut));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        try { AcIO.DxfWriter.Write(dxfOut, doc, false); }
        catch (Exception ex) { throw new InvalidDataException($"Không ghi được DXF từ '{Path.GetFileName(dwgPath)}': {ex.Message}", ex); }
        return $"Đã chuyển {Path.GetFileName(dwgPath)} → {Path.GetFileName(dxfOut)}" + (warnings.Count > 0 ? " (" + string.Join("; ", warnings) + ")" : "");
    }

    /// <summary>Ghi CadDocument ra DWG (phiên bản theo doc.Header.Version; ACadSharp ghi được AC1014, AC1015, AC1018, AC1024, AC1027, AC1032).</summary>
    public static void WriteDwg(CadDocument doc, string dwgOut)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(dwgOut));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        AcIO.DwgWriter.Write(dwgOut, doc);
    }

    /// <summary>DXF → DWG (AutoCAD 2018, AC1032) bằng ACadSharp. Nếu ghi trực tiếp lỗi (vd. DXF R12 tối giản thiếu bảng/đối tượng),
    /// dựng bản vẽ mới rồi chép layer + thực thể model space sang. Trả mô tả ngắn.</summary>
    public static string ConvertDxfToDwg(string dxfPath, string dwgOut)
    {
        CadDocument src;
        try { src = AcIO.DxfReader.Read(dxfPath); }
        catch (Exception ex) { throw new InvalidDataException($"ACadSharp không đọc được DXF '{Path.GetFileName(dxfPath)}': {ex.Message}", ex); }
        try
        {
            src.Header.Version = ACadVersion.AC1032;
            WriteDwg(src, dwgOut);
            return "ghi trực tiếp";
        }
        catch (Exception ex1)
        {
            try { if (File.Exists(dwgOut)) File.Delete(dwgOut); } catch (Exception) { /* bỏ qua */ }
            var dst = new CadDocument();
            try { dst.Header.InsUnits = src.Header.InsUnits; } catch (Exception) { /* giữ mặc định */ }
            foreach (var l in src.Layers.ToList())
            {
                try { if (!dst.Layers.Contains(l.Name)) dst.Layers.Add((Layer)l.Clone()); } catch (Exception) { /* bỏ lớp lỗi */ }
            }
            int copied = 0, skipped = 0;
            foreach (var e in src.Entities.ToList())
            {
                try { dst.Entities.Add((Entity)e.Clone()); copied++; }
                catch (Exception) { skipped++; }
            }
            WriteDwg(dst, dwgOut);
            return $"dựng lại ({ex1.GetType().Name}: {ex1.Message}) – chép {copied} thực thể" + (skipped > 0 ? $", bỏ {skipped}" : "");
        }
    }

    // ==================================================================
    // ÁNH XẠ THỰC THỂ
    // ==================================================================

    /// <summary>Phép biến đổi affine 2D: x' = A x + B y + Tx; y' = C x + D y + Ty (giống DxfReader của lõi).</summary>
    private readonly struct Aff
    {
        public readonly double A, B, C, D, Tx, Ty;
        public Aff(double a, double b, double c, double d, double tx, double ty) { A = a; B = b; C = c; D = d; Tx = tx; Ty = ty; }
        public static Aff Translate(double x, double y) => new(1, 0, 0, 1, x, y);
        public static Aff Scaling(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);
        public static Aff Rotation(double deg)
        {
            double r = deg * Math.PI / 180.0, c = Math.Cos(r), s = Math.Sin(r);
            return new(c, -s, s, c, 0, 0);
        }
        /// <summary>this ∘ m (áp m trước, rồi this)</summary>
        public Aff Then(Aff m) => new(
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

    /// <summary>Thực thể thô trong hệ toạ độ của block chứa nó (trước phép biến đổi).</summary>
    private sealed class Raw
    {
        public DxfEntityType Type;
        public List<(double X, double Y)> Pts = new();
        public bool Closed;
        public double Radius;
        public string Text = "";
        public string Tag = "";
        public double Height;
        public string Source = "";
        public double Width;
        public string Style = "";
        public string FontHint = "";
        public bool IsFill;
    }

    private static readonly Regex MTextFontRx = new(@"\\[fF]([^|;]*)", RegexOptions.Compiled);

    private sealed class Mapper
    {
        private readonly KetCauVXT.Import.DxfReader _r;
        private readonly CadDocument _doc;
        private readonly HashSet<string> _missing = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _xrefs = new(StringComparer.OrdinalIgnoreCase);
        private bool _warnDepth, _warnCycle, _warnLimit, _warnMinsert;
        private int _badEntities;

        public Mapper(KetCauVXT.Import.DxfReader r, CadDocument doc) { _r = r; _doc = doc; }

        public void Run(double scale)
        {
            ReadTables();
            var root = Aff.Scaling(scale, scale);
            BlockRecord ms = null;
            try { ms = _doc.ModelSpace; } catch (Exception) { ms = null; }
            if (ms == null) { _r.Warnings.Add("Bản vẽ DWG không có model space."); return; }
            foreach (var e in ms.Entities.ToList())
                if (!Walk(e, root, 0, "", null, null, null)) break;

            if (_missing.Count > 0)
                _r.Warnings.Add($"Có {_missing.Count} block được chèn nhưng không có định nghĩa: {string.Join(", ", _missing.Take(5))}{(_missing.Count > 5 ? "…" : "")}");
            if (_xrefs.Count > 0)
                _r.Warnings.Add($"Bản vẽ có tham chiếu ngoài (xref) không được nạp: {string.Join(", ", _xrefs.Take(5))}{(_xrefs.Count > 5 ? "…" : "")}. Hãy BIND xref trong AutoCAD trước khi nhập.");
            if (_badEntities > 0)
                _r.Warnings.Add($"Bỏ qua {_badEntities} thực thể có dữ liệu không đọc được.");
        }

        // ---------------------------------------------------------- bảng
        private void ReadTables()
        {
            try
            {
                foreach (var l in _doc.Layers)
                {
                    string n = l?.Name;
                    if (string.IsNullOrEmpty(n)) continue;
                    _r.LayerLineTypes[n] = l.LineType?.Name ?? "";
                    if ((l.Flags & LayerFlags.Frozen) != 0) _r.FrozenLayers.Add(n);
                    if (!l.IsOn) _r.OffLayers.Add(n);
                    int aci = 7;
                    try
                    {
                        var c = l.Color;
                        int idx = c.IsTrueColor ? c.GetApproxIndex() : Math.Abs((int)c.Index);
                        aci = idx is >= 1 and <= 255 ? idx : 7;
                    }
                    catch (Exception) { aci = 7; }
                    _r.LayerColors[n] = aci;
                }
            }
            catch (Exception ex) { _r.Warnings.Add("Không đọc được bảng layer: " + ex.Message); }

            try
            {
                foreach (var s in _doc.TextStyles)
                {
                    if (string.IsNullOrEmpty(s?.Name)) continue;
                    string hint = KetCauVXT.Import.DxfReader.FontHintOfName((s.Filename ?? "") + "|" + (s.BigFontFilename ?? ""));
                    if (hint.Length > 0) _r.StyleFontHints[s.Name] = hint;
                }
            }
            catch (Exception) { /* bỏ qua kiểu chữ */ }
        }

        // ---------------------------------------------------------- tiện ích
        private static string LayerOf(Entity e)
        {
            string s = null;
            try { s = e.Layer?.Name; } catch (Exception) { s = null; }
            return string.IsNullOrEmpty(s) ? "0" : s;
        }

        private static string LineTypeOf(Entity e)
        {
            try { return e.LineType?.Name ?? ""; } catch (Exception) { return ""; }
        }

        private static bool Flipped(XYZ n) => n.Z < 0;

        private string ResolveLineType(string own, string effLayer, string parentLt)
        {
            string lt = own ?? "";
            if (lt.Equals("BYBLOCK", StringComparison.OrdinalIgnoreCase) && parentLt != null) return parentLt;
            if (lt.Length == 0 || lt.Equals("BYLAYER", StringComparison.OrdinalIgnoreCase) || lt.Equals("BYBLOCK", StringComparison.OrdinalIgnoreCase))
                return _r.LayerLineTypes.TryGetValue(effLayer, out string l) ? l : "";
            return lt;
        }

        private static bool IsLayoutBlock(string name) =>
            name.StartsWith("*Model_Space", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase);

        private static string EffectiveBlockName(BlockRecord br)
        {
            if (br == null) return "";
            string name = br.Name ?? "";
            try
            {
                if (name.StartsWith("*", StringComparison.Ordinal) && br.IsAnonymous)
                {
                    var src = br.Source;
                    if (src != null && !string.IsNullOrEmpty(src.Name)) return src.Name;
                }
            }
            catch (Exception) { /* không tra được block gốc */ }
            return name;
        }

        private static bool IsXref(BlockRecord br)
        {
            try
            {
                var f = br.BlockEntity?.Flags ?? BlockTypeFlags.None;
                return (f & (BlockTypeFlags.XRef | BlockTypeFlags.XRefOverlay)) != 0;
            }
            catch (Exception) { return false; }
        }

        // ---------------------------------------------------------- duyệt
        /// <summary>Xuất 1 thực thể (hoặc phân rã INSERT). Trả false khi vượt giới hạn số thực thể.</summary>
        private bool Walk(Entity e, Aff m, int depth, string outerBlock, string parentLayer, string parentLt, HashSet<BlockRecord> stack)
        {
            if (e == null) return true;
            if (_r.Entities.Count >= MaxEntities)
            {
                if (!_warnLimit) { _warnLimit = true; _r.Warnings.Add($"Bản vẽ quá lớn – chỉ đọc {MaxEntities:N0} thực thể đầu tiên."); }
                return false;
            }
            string own = LayerOf(e);
            string layer = own == "0" && parentLayer != null ? parentLayer : own;
            if (_r.FrozenLayers.Contains(layer)) return true;
            string lt = ResolveLineType(LineTypeOf(e), layer, parentLt);

            if (e is Insert ins) return Explode(ins, m, depth, outerBlock, layer, lt, stack);

            List<Raw> raws;
            try { raws = Convert(e); }
            catch (Exception) { _badEntities++; return true; }
            if (raws == null) return true;
            foreach (var re in raws) Emit(re, m, layer, lt, outerBlock);
            return true;
        }

        private bool Explode(Insert ins, Aff m, int depth, string outerBlock, string layer, string lt, HashSet<BlockRecord> stack)
        {
            BlockRecord br = null;
            try { br = ins.Block; } catch (Exception) { br = null; }
            string blockLabel = outerBlock.Length > 0 ? outerBlock : EffectiveBlockName(br);

            // thuộc tính ATTRIB: toạ độ đã ở không gian chứa INSERT → dùng phép biến đổi cha
            try
            {
                if (ins.Attributes != null)
                    foreach (var a in ins.Attributes)
                    {
                        string aOwn = LayerOf(a);
                        string al = aOwn == "0" ? layer : aOwn;
                        if (_r.FrozenLayers.Contains(al)) continue;
                        var raw = ConvertAttrib(a);
                        if (raw == null) continue;
                        var ent = Emit(raw, m, al, ResolveLineType(LineTypeOf(a), al, lt), blockLabel);
                        if (ent != null) ent.AttributeTag = raw.Tag;
                    }
            }
            catch (Exception) { _badEntities++; }

            if (br == null) { _missing.Add("(không tên)"); return true; }
            string name = br.Name ?? "";
            if (name.Length == 0 || IsLayoutBlock(name)) return true;
            List<Entity> children;
            try { children = br.Entities.ToList(); } catch (Exception) { _missing.Add(name); return true; }
            if (IsXref(br)) { _xrefs.Add(name); if (children.Count == 0) return true; }
            if (depth >= MaxDepth)
            {
                if (!_warnDepth) { _warnDepth = true; _r.Warnings.Add($"Block lồng nhau quá {MaxDepth} cấp – bỏ phần sâu hơn."); }
                return true;
            }
            stack ??= new HashSet<BlockRecord>(ReferenceEqualityComparer.Instance);
            if (!stack.Add(br))
            {
                if (!_warnCycle) { _warnCycle = true; _r.Warnings.Add($"Block '{name}' tự tham chiếu (vòng lặp) – bỏ qua."); }
                return true;
            }

            try
            {
                int cols = Math.Max(1, (int)ins.ColumnCount), rows = Math.Max(1, (int)ins.RowCount);
                if ((long)cols * rows > MaxMinsertCells)
                {
                    if (!_warnMinsert) { _warnMinsert = true; _r.Warnings.Add($"MINSERT '{name}' có {cols}×{rows} ô – chỉ phân rã 1 ô."); }
                    cols = 1; rows = 1;
                }
                double sx = ins.XScale, sy = ins.YScale;
                if (sx == 0 || double.IsNaN(sx)) sx = 1;
                if (sy == 0 || double.IsNaN(sy)) sy = 1;
                XYZ bp = XYZ.Zero;
                try { if (br.BlockEntity != null) bp = br.BlockEntity.BasePoint; } catch (Exception) { bp = XYZ.Zero; }
                var ip = ins.InsertPoint;
                var pre = m;
                if (Flipped(ins.Normal)) pre = pre.Then(Aff.Scaling(-1, 1));
                pre = pre.Then(Aff.Translate(ip.X, ip.Y)).Then(Aff.Rotation(ins.Rotation * 180.0 / Math.PI));
                var post = Aff.Scaling(sx, sy).Then(Aff.Translate(-bp.X, -bp.Y));
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        var mc = pre.Then(Aff.Translate(c * ins.ColumnSpacing, r * ins.RowSpacing)).Then(post);
                        foreach (var child in children)
                            if (!Walk(child, mc, depth + 1, blockLabel, layer, lt, stack)) return false;
                    }
            }
            finally { stack.Remove(br); }
            return true;
        }

        private DxfEntity Emit(Raw re, Aff m, string layer, string lt, string block)
        {
            if (re.Pts.Count == 0) return null;
            var e = new DxfEntity
            {
                Type = re.Type, Layer = layer, LineType = lt ?? "", Block = block ?? "", Closed = re.Closed, SourceType = re.Source ?? "",
                Width = re.Width * m.LinearScale,
                FontHint = re.FontHint.Length > 0 ? re.FontHint : _r.StyleFontHints.TryGetValue(re.Style ?? "", out var fh) ? fh : ""
            };
            switch (re.Type)
            {
                case DxfEntityType.Circle:
                    if (!m.IsUniform)
                    {
                        // block co giãn không đều → hình tròn thành elip: xấp xỉ bằng đa tuyến kín
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
            if (re.IsFill) _r.FillEntities.Add(e); else _r.Entities.Add(e);
            return e;
        }

        // ---------------------------------------------------------- từng loại thực thể
        private List<Raw> Convert(Entity e)
        {
            switch (e)
            {
                case Line ln:
                {
                    var r = new Raw { Type = DxfEntityType.Line, Source = "LINE" };
                    r.Pts.Add((ln.StartPoint.X, ln.StartPoint.Y));   // LINE ở WCS, bỏ Z
                    r.Pts.Add((ln.EndPoint.X, ln.EndPoint.Y));
                    return One(r);
                }
                case LwPolyline lw:
                {
                    var r = new Raw { Type = DxfEntityType.Polyline, Source = "LWPOLYLINE", Closed = lw.IsClosed, Width = Math.Abs(lw.ConstantWidth) };
                    var verts = lw.Vertices.Select(v => (v.Location.X, v.Location.Y, v.Bulge)).ToList();
                    BuildPolyline(r, verts, Flipped(lw.Normal));
                    return r.Pts.Count >= 2 ? One(r) : null;
                }
                case Polyline2D p2:
                {
                    var r = new Raw { Type = DxfEntityType.Polyline, Source = "POLYLINE", Closed = p2.IsClosed };
                    double w0 = Math.Abs(p2.StartWidth), w1 = Math.Abs(p2.EndWidth);
                    if (w0 > 0 && Math.Abs(w0 - w1) <= 1e-9 * Math.Max(1, w0)) r.Width = w0;
                    var verts = new List<(double X, double Y, double Bulge)>();
                    foreach (var v in p2.Vertices)
                    {
                        if ((v.Flags & VertexFlags.SplineFrameControlPoint) != 0) continue; // điểm điều khiển spline
                        verts.Add((v.Location.X, v.Location.Y, v.Bulge));
                    }
                    BuildPolyline(r, verts, Flipped(p2.Normal));
                    return r.Pts.Count >= 2 ? One(r) : null;
                }
                case Polyline3D p3:
                {
                    var r = new Raw { Type = DxfEntityType.Polyline, Source = "POLYLINE", Closed = p3.IsClosed };
                    var verts = new List<(double X, double Y, double Bulge)>();
                    foreach (var v in p3.Vertices)
                    {
                        if ((v.Flags & VertexFlags.SplineFrameControlPoint) != 0) continue;
                        verts.Add((v.Location.X, v.Location.Y, 0));
                    }
                    BuildPolyline(r, verts, false);
                    return r.Pts.Count >= 2 ? One(r) : null;
                }
                case Arc arc:
                {
                    double rad = arc.Radius;
                    if (rad <= 0) return null;
                    var r = new Raw { Type = DxfEntityType.Polyline, Source = "ARC" };
                    double a1 = arc.StartAngle * 180.0 / Math.PI, a2 = arc.EndAngle * 180.0 / Math.PI;
                    double sweep = ((a2 - a1) % 360 + 360) % 360;
                    if (sweep < 1e-9) sweep = 360;
                    int seg = Math.Max(1, (int)Math.Ceiling(sweep / ArcStepDeg - 1e-9));
                    bool flip = Flipped(arc.Normal);
                    double cx = arc.Center.X, cy = arc.Center.Y;
                    for (int s = 0; s <= seg; s++)
                    {
                        double a = (a1 + sweep * s / seg) * Math.PI / 180.0;
                        double x = cx + rad * Math.Cos(a), y = cy + rad * Math.Sin(a);
                        r.Pts.Add((flip ? -x : x, y));
                    }
                    return One(r);
                }
                case Circle cir:
                {
                    var r = new Raw { Type = DxfEntityType.Circle, Source = "CIRCLE", Radius = cir.Radius };
                    double x = cir.Center.X, y = cir.Center.Y;
                    r.Pts.Add((Flipped(cir.Normal) ? -x : x, y));
                    return One(r);
                }
                case AttributeDefinition _:
                    return null;   // ATTDEF: bỏ (giá trị thật nằm ở ATTRIB của INSERT)
                case TextEntity te:
                {
                    var r = new Raw { Type = DxfEntityType.Text, Source = "TEXT", Height = te.Height, Style = StyleName(te.Style) };
                    var p = TextPoint(te);
                    r.Pts.Add((Flipped(te.Normal) ? -p.X : p.X, p.Y));
                    r.Text = CleanText(te.Value);
                    return r.Text.Length > 0 ? One(r) : null;
                }
                case MText mt:
                {
                    var r = new Raw { Type = DxfEntityType.Text, Source = "MTEXT", Height = mt.Height, Style = StyleName(mt.Style) };
                    r.Pts.Add((mt.InsertPoint.X, mt.InsertPoint.Y));   // MTEXT: điểm chèn ở WCS
                    string raw = mt.Value ?? "";
                    var fm = MTextFontRx.Match(raw);
                    if (fm.Success) r.FontHint = KetCauVXT.Import.DxfReader.FontHintOfName(fm.Groups[1].Value);
                    r.Text = CleanText(raw);
                    return r.Text.Length > 0 ? One(r) : null;
                }
                case Solid so:
                {
                    // SOLID: 4 đỉnh theo thứ tự 1-2-3-4, chu vi là 1→2→4→3
                    var r = new Raw { Type = DxfEntityType.Polyline, Source = "SOLID", IsFill = true, Closed = true };
                    bool flip = Flipped(so.Normal);
                    foreach (var q in new[] { so.FirstCorner, so.SecondCorner, so.FourthCorner, so.ThirdCorner })
                    {
                        var v = (X: flip ? -q.X : q.X, Y: q.Y);
                        if (r.Pts.Count == 0 || Math.Abs(r.Pts[^1].X - v.X) > 1e-9 || Math.Abs(r.Pts[^1].Y - v.Y) > 1e-9) r.Pts.Add(v);
                    }
                    if (r.Pts.Count > 2 && Math.Abs(r.Pts[0].X - r.Pts[^1].X) < 1e-9 && Math.Abs(r.Pts[0].Y - r.Pts[^1].Y) < 1e-9) r.Pts.RemoveAt(r.Pts.Count - 1);
                    return r.Pts.Count >= 3 ? One(r) : null;
                }
                case Hatch h:
                    return ConvertHatch(h);
                default:
                    return null;   // DIMENSION, ELLIPSE, SPLINE, POINT, VIEWPORT… bỏ qua (như bộ đọc DXF)
            }
        }

        private static List<Raw> One(Raw r) => new() { r };

        /// <summary>Bỏ mã định dạng MTEXT / %%c… (như bộ đọc DXF), chuẩn hoá Unicode dựng sẵn (DWG cũ mã cp1258 ghi dấu tổ hợp).</summary>
        private static string CleanText(string s)
        {
            s = KetCauVXT.Import.DxfReader.CleanMText(s ?? "");
            try { if (!s.IsNormalized(NormalizationForm.FormC)) s = s.Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { /* giữ nguyên */ }
            return s;
        }

        private static string StyleName(TextStyle s)
        {
            try { return s?.Name ?? ""; } catch (Exception) { return ""; }
        }

        /// <summary>TEXT/ATTRIB: có căn lề (khác trái + chân chữ) thì điểm căn là AlignmentPoint (mã 11/21), ngược lại điểm chèn.</summary>
        private static XYZ TextPoint(TextEntity te)
        {
            bool aligned = te.HorizontalAlignment != TextHorizontalAlignment.Left || te.VerticalAlignment != TextVerticalAlignmentType.Baseline;
            return aligned ? te.AlignmentPoint : te.InsertPoint;
        }

        private Raw ConvertAttrib(AttributeEntity a)
        {
            if ((a.Flags & AttributeFlags.Hidden) != 0) return null; // thuộc tính ẩn
            var r = new Raw { Type = DxfEntityType.Text, Source = "ATTRIB", Height = a.Height, Style = StyleName(a.Style), Tag = a.Tag ?? "" };
            var p = TextPoint(a);
            r.Pts.Add((Flipped(a.Normal) ? -p.X : p.X, p.Y));
            string v = a.Value ?? "";
            if (a.AttributeType != AttributeType.SingleLine && a.MText != null && !string.IsNullOrEmpty(a.MText.Value)) v = a.MText.Value;
            r.Text = CleanText(v);
            return r.Text.Length > 0 ? r : null;
        }

        /// <summary>HATCH: mỗi vòng biên → 1 đa tuyến kín (miền tô). Cạnh thẳng, cung tròn, đa tuyến (bulge); vòng có elip/spline lấy điểm do thư viện chia.</summary>
        private static List<Raw> ConvertHatch(Hatch h)
        {
            var res = new List<Raw>();
            bool flip = Flipped(h.Normal);
            foreach (var path in h.Paths)
            {
                var r = new Raw { Type = DxfEntityType.Polyline, Source = "HATCH", IsFill = true, Closed = true };
                bool ok = true;
                var poly = path.Edges.OfType<Hatch.BoundaryPath.Polyline>().FirstOrDefault();
                if (poly != null)
                {
                    var verts = poly.Vertices.Select(v => (v.X, v.Y, v.Z)).ToList();   // Z = bulge
                    BuildPolyline(r, verts, flip);
                }
                else
                {
                    foreach (var edge in path.Edges)
                    {
                        switch (edge)
                        {
                            case Hatch.BoundaryPath.Line l:
                                AddPt(r.Pts, l.Start.X, l.Start.Y, flip); AddPt(r.Pts, l.End.X, l.End.Y, flip);
                                break;
                            case Hatch.BoundaryPath.Arc a:
                            {
                                double a1 = a.StartAngle * 180 / Math.PI, a2 = a.EndAngle * 180 / Math.PI;
                                bool ccw = a.CounterClockWise;
                                double sweep = ((a2 - a1) % 360 + 360) % 360;
                                if (!ccw) sweep = -sweep;
                                if (Math.Abs(sweep) < 1e-9) sweep = ccw ? 360 : -360;
                                int seg = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / ArcStepDeg));
                                // cung theo chiều kim đồng hồ: góc ghi theo hệ lật (như DXF), quy về góc thực
                                double s0 = ccw ? a1 : -a1;
                                for (int q = 0; q <= seg; q++)
                                {
                                    double ang = (s0 + sweep * q / seg) * Math.PI / 180;
                                    AddPt(r.Pts, a.Center.X + a.Radius * Math.Cos(ang), a.Center.Y + a.Radius * Math.Sin(ang), flip);
                                }
                                break;
                            }
                            default:
                                ok = false;
                                break;
                        }
                        if (!ok) break;
                    }
                    if (!ok)
                    {
                        // elip / spline: dùng điểm chia của thư viện
                        r.Pts.Clear();
                        try
                        {
                            foreach (var p in path.GetPoints(24)) AddPt(r.Pts, p.X, p.Y, flip);
                            ok = true;
                        }
                        catch (Exception) { ok = false; }
                    }
                    CloseIfRepeated(r);
                }
                if (ok && r.Pts.Count >= 3) res.Add(r);
            }
            return res;
        }

        private static void AddPt(List<(double X, double Y)> pts, double x, double y, bool flip)
        {
            if (flip) x = -x;
            if (pts.Count > 0 && Math.Abs(pts[^1].X - x) < 1e-9 && Math.Abs(pts[^1].Y - y) < 1e-9) return;
            pts.Add((x, y));
        }

        /// <summary>Dựng đỉnh đa tuyến (có cung lồi – bulge) trong OCS rồi đổi sang WCS (giống bộ đọc DXF).</summary>
        private static void BuildPolyline(Raw e, List<(double X, double Y, double Bulge)> v, bool flip)
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

        private static void CloseIfRepeated(Raw e)
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
    }
}
