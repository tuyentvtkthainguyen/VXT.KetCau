using System.Globalization;
using System.Text;
using KetCauVXT.Model;

namespace KetCauVXT.Import;

/// <summary>Điểm gốc (điểm chèn) của khung tên khi nhập.</summary>
public enum TitleBlockBase { Auto, BottomRight, BottomLeft, TopRight, TopLeft, Origin, Custom }

/// <summary>Tuỳ chọn nhập khung tên từ DWG/DXF.</summary>
public sealed class TitleBlockImportOptions
{
    /// <summary>null/"" = tự chọn (block tên KHUNG/TITLE…; không có thì toàn bộ model space); "*" = toàn bộ model space; tên block = chỉ block đó.</summary>
    public string Block { get; set; }
    /// <summary>mm giấy trên một đơn vị bản vẽ; 0 = theo $INSUNITS (mm = 1).</summary>
    public double MmPerUnit { get; set; }
    public TitleBlockMode Mode { get; set; } = TitleBlockMode.TitleBox;
    public TitleBlockBase Base { get; set; } = TitleBlockBase.Auto;
    /// <summary>Điểm gốc tự chọn (đơn vị bản vẽ) khi Base = Custom</summary>
    public double BaseX { get; set; }
    public double BaseY { get; set; }
    /// <summary>Chỉ lấy thực thể nằm trong vùng (đơn vị bản vẽ)</summary>
    public bool UseRegion { get; set; }
    public double RegionX1 { get; set; }
    public double RegionY1 { get; set; }
    public double RegionX2 { get; set; }
    public double RegionY2 { get; set; }
    /// <summary>Lấy cả đường bao miền tô (HATCH/SOLID)</summary>
    public bool IncludeFills { get; set; }
    /// <summary>Giữ bề rộng nét đa tuyến</summary>
    public bool KeepWidths { get; set; } = true;
    public string Name { get; set; }
}

/// <summary>Kết quả nhập khung tên.</summary>
public sealed class TitleBlockImportResult
{
    public TitleBlockDef Def { get; set; }
    /// <summary>Các block có trong model space (để người dùng chọn)</summary>
    public List<string> Blocks { get; set; } = new();
    /// <summary>Block đã dùng ("" = toàn bộ model space)</summary>
    public string UsedBlock { get; set; } = "";
    public double MmPerUnit { get; set; } = 1;
    /// <summary>Phạm vi hình trong file (đơn vị bản vẽ) trước khi đổi gốc</summary>
    public double SrcMinX, SrcMinY, SrcMaxX, SrcMaxY;
    public List<string> Warnings { get; set; } = new();
    /// <summary>Số chữ đã nhận ra trường (thẻ ATTRIB/ATTDEF hoặc chỗ trống {…})</summary>
    public int FieldCount { get; set; }
    public string Info { get; set; } = "";
}

/// <summary>Nhập khung tên / khung bản vẽ của người dùng từ DXF, DWG (qua <see cref="CadFileReaders"/>) hoặc ảnh PNG/JPG
/// → <see cref="TitleBlockDef"/> (mm giấy). Nhận trường từ chỗ trống {TEN_CONG_TRINH}… trong chữ và thẻ ATTRIB/ATTDEF.
/// Với DXF (và DWG đã chuyển sang DXF) đọc thêm: ATTDEF trong model space, góc xoay + căn lề chữ TEXT/MTEXT/ATTRIB.</summary>
public static class TitleBlockImporter
{
    public const string FileFilter = "Khung tên (*.dwg;*.dxf;*.png;*.jpg;*.jpeg)|*.dwg;*.dxf;*.png;*.jpg;*.jpeg|Bản vẽ AutoCAD (*.dwg;*.dxf)|*.dwg;*.dxf|Ảnh (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static bool IsImage(string path)
    {
        string ext = Path.GetExtension(path ?? "").ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp";
    }

    /// <summary>Tên block gợi ý là khung tên.</summary>
    public static bool LooksLikeTitleBlock(string name)
    {
        string n = TitleFields.Normalize(name).Replace("_", "");
        if (n.Length == 0) return false;
        foreach (var k in new[] { "KHUNGTEN", "KHUNG", "TITLE", "FRAME", "BORDER", "KHUNGBANVE", "KHUNGBV" })
            if (n.Contains(k)) return true;
        return n is "KT" or "TB" or "KTEN";
    }

    /// <summary>Block tự chọn: block tên giống khung tên (ưu tiên KHUNG TÊN/TITLE), hoặc "" (toàn bộ).</summary>
    public static string AutoBlock(IEnumerable<string> blocks)
    {
        var l = (blocks ?? Enumerable.Empty<string>()).Where(LooksLikeTitleBlock).ToList();
        return l.FirstOrDefault(b => TitleFields.Normalize(b).Replace("_", "").Contains("KHUNGTEN") || TitleFields.Normalize(b).Contains("TITLE"))
               ?? l.FirstOrDefault() ?? "";
    }

    // ==================================================================
    // File
    // ==================================================================
    /// <summary>Nhập từ file .dxf / .dwg / ảnh. Ném InvalidDataException (thông báo tiếng Việt) khi không đọc được.</summary>
    public static TitleBlockImportResult FromFile(string path, TitleBlockImportOptions o = null)
    {
        o ??= new TitleBlockImportOptions();
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException("Không tìm thấy file khung tên", path);
        if (IsImage(path))
        {
            var b64 = Convert.ToBase64String(File.ReadAllBytes(path));
            var (w, h) = ImageSizeMm(File.ReadAllBytes(path));
            var def = FromImage(b64, w, h, o.Mode, o.Name ?? Path.GetFileNameWithoutExtension(path));
            def.Source = Path.GetFileName(path);
            return new TitleBlockImportResult { Def = def, Info = $"Ảnh {Path.GetFileName(path)}: {w:0.#}×{h:0.#} mm" };
        }

        DxfReader r = null;
        DxfTextScan scan = null;
        var warn = new List<string>();
        string dxfPath = path;
        string tmp = null;
        try
        {
            if (DwgConverter.IsDwg(path) && CadFileReaders.DwgToDxf != null)
            {
                try
                {
                    tmp = Path.Combine(Path.GetTempPath(), "kcvxt_khungten_" + Guid.NewGuid().ToString("N") + ".dxf");
                    CadFileReaders.DwgToDxf(path, tmp);
                    var rd = DxfReader.Read(tmp);
                    if (rd.Entities.Count > 0) { r = rd; dxfPath = tmp; }
                }
                catch (Exception ex) { warn.Add("Không chuyển được DWG → DXF để đọc thuộc tính/căn lề chữ: " + ex.Message); }
            }
            if (r == null)
            {
                r = CadFileReaders.Read(path, 0, out string err);
                if (r == null) throw new InvalidDataException(string.IsNullOrEmpty(err) ? "Không đọc được bản vẽ " + Path.GetFileName(path) : err);
                if (DwgConverter.IsDwg(path)) dxfPath = null;
            }
            if (dxfPath != null)
            {
                try { scan = DxfTextScan.FromFile(dxfPath); }
                catch (Exception ex) { warn.Add("Không đọc thêm được thuộc tính chữ: " + ex.Message); }
            }
        }
        finally
        {
            if (tmp != null) { try { File.Delete(tmp); } catch (Exception) { /* bỏ qua */ } }
        }
        var res = FromReader(r, o, scan);
        res.Warnings.InsertRange(0, warn);
        res.Def.Source = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(o.Name)) res.Def.Name = Path.GetFileNameWithoutExtension(path);
        return res;
    }

    // ==================================================================
    // Từ thực thể đã đọc
    // ==================================================================
    private static bool InRegion(DxfEntity e, TitleBlockImportOptions o, double unitM)
    {
        if (!o.UseRegion) return true;
        double x1 = Math.Min(o.RegionX1, o.RegionX2) * unitM, x2 = Math.Max(o.RegionX1, o.RegionX2) * unitM;
        double y1 = Math.Min(o.RegionY1, o.RegionY2) * unitM, y2 = Math.Max(o.RegionY1, o.RegionY2) * unitM;
        double tol = 1e-9 + 1e-6 * Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        foreach (var (x, y) in e.Points)
            if (x < x1 - tol || x > x2 + tol || y < y1 - tol || y > y2 + tol) return false;
        return true;
    }

    /// <summary>Dựng khung tên từ thực thể của bộ đọc DXF/DWG (toạ độ m). scan: thông tin chữ bổ sung đọc thẳng từ DXF (có thể null).</summary>
    public static TitleBlockImportResult FromReader(DxfReader r, TitleBlockImportOptions o = null, DxfTextScan scan = null)
    {
        o ??= new TitleBlockImportOptions();
        if (r == null) throw new ArgumentNullException(nameof(r));
        var res = new TitleBlockImportResult();
        res.Warnings.AddRange(r.Warnings);
        double unitM = r.Scale > 0 ? r.Scale : 0.001;              // đơn vị bản vẽ → m
        double mmPerUnit = o.MmPerUnit > 0 ? o.MmPerUnit : unitM * 1000;
        double k = mmPerUnit / unitM;                                // m (bộ đọc) → mm giấy
        res.MmPerUnit = mmPerUnit;

        res.Blocks = r.Entities.Select(e => e.Block ?? "").Where(b => b.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(b => b, StringComparer.OrdinalIgnoreCase).ToList();
        string block = o.Block;
        if (string.IsNullOrEmpty(block)) block = AutoBlock(res.Blocks);
        if (block == "*") block = "";
        if (block.Length > 0 && !res.Blocks.Contains(block, StringComparer.OrdinalIgnoreCase))
        {
            res.Warnings.Add($"Không có block '{block}' trong model space – lấy toàn bộ.");
            block = "";
        }
        res.UsedBlock = block;
        bool Pick(DxfEntity e) => (block.Length == 0 || string.Equals(e.Block, block, StringComparison.OrdinalIgnoreCase)) && InRegion(e, o, unitM);

        var def = new TitleBlockDef
        {
            Name = string.IsNullOrWhiteSpace(o.Name) ? (block.Length > 0 ? block : "Khung tên người dùng") : o.Name.Trim(),
            Kind = TitleBlockKind.Vector, Mode = o.Mode, FitToPaper = o.Mode == TitleBlockMode.WholeFrame,
            Imported = DateTime.Now.ToString("yyyy-MM-dd HH:mm", Inv)
        };
        var used = new HashSet<DxfTextScan.TextInfo>();
        foreach (var e in r.Entities)
        {
            if (!Pick(e) || e.Points.Count == 0) continue;
            switch (e.Type)
            {
                case DxfEntityType.Line:
                    if (e.Points.Count < 2) break;
                    def.Entities.Add(new TitleBlockEntity { Type = "LINE", SourceLayer = e.Layer, Pts = Flat(e.Points.Take(2), k) });
                    break;
                case DxfEntityType.Polyline:
                    if (e.Points.Count < 2) break;
                    def.Entities.Add(new TitleBlockEntity
                    {
                        Type = "PLINE", SourceLayer = e.Layer, Closed = e.Closed, Pts = Flat(e.Points, k),
                        Width = o.KeepWidths ? e.Width * k : 0
                    });
                    break;
                case DxfEntityType.Circle:
                    def.Entities.Add(new TitleBlockEntity { Type = "CIRCLE", SourceLayer = e.Layer, R = e.Radius * k, Pts = Flat(e.Points.Take(1), k) });
                    break;
                case DxfEntityType.Text:
                {
                    var t = new TitleBlockEntity
                    {
                        Type = "TEXT", SourceLayer = e.Layer, Text = e.Text ?? "", H = Math.Max(0.1, e.TextHeight * k),
                        Pts = Flat(e.Points.Take(1), k), Tag = e.AttributeTag ?? ""
                    };
                    var info = scan?.Match(e, unitM, used);
                    if (info != null)
                    {
                        used.Add(info);
                        t.Rot = info.Rot; t.Ha = info.Ha; t.Va = info.Va;
                        if (string.IsNullOrEmpty(t.Tag)) t.Tag = info.Tag ?? "";
                    }
                    def.Entities.Add(t);
                    break;
                }
            }
        }
        if (o.IncludeFills)
            foreach (var e in r.FillEntities)
                if (Pick(e) && e.Points.Count >= 3)
                    def.Entities.Add(new TitleBlockEntity { Type = "PLINE", SourceLayer = e.Layer, Closed = true, Pts = Flat(e.Points, k) });

        // ATTDEF trong model space (khung tên vẽ dạng block mở trực tiếp) – bộ đọc chung bỏ qua
        if (scan != null)
            foreach (var a in scan.ModelAttDefs)
            {
                if (block.Length > 0 && !string.Equals(a.Block, block, StringComparison.OrdinalIgnoreCase)) continue;
                var pt = (a.X * unitM, a.Y * unitM);
                var probe = new DxfEntity { Type = DxfEntityType.Text, Points = { pt } };
                if (!InRegion(probe, o, unitM)) continue;
                def.Entities.Add(new TitleBlockEntity
                {
                    Type = "TEXT", SourceLayer = a.Layer, Text = string.IsNullOrEmpty(a.Text) ? a.Tag : a.Text, Tag = a.Tag ?? "",
                    H = Math.Max(0.1, a.Height * mmPerUnit), Rot = a.Rot, Ha = a.Ha, Va = a.Va,
                    Pts = new List<double> { a.X * mmPerUnit, a.Y * mmPerUnit }
                });
            }

        // trường: thẻ ATTRIB/ATTDEF → khoá chuẩn; chỗ trống {…} giữ trong chữ (thay khi vẽ)
        foreach (var t in def.Entities.Where(x => x.Type == "TEXT"))
        {
            if (!string.IsNullOrEmpty(t.Tag))
            {
                string key = TitleFields.Canonical(t.Tag);
                if (key != null && !TitleFields.HasPlaceholder(t.Text)) t.Field = key;
            }
            if (t.Field.Length > 0 || TitleFields.HasPlaceholder(t.Text)) res.FieldCount++;
        }

        def.UpdateExtents();
        res.SrcMinX = def.MinX / mmPerUnit; res.SrcMinY = def.MinY / mmPerUnit;
        res.SrcMaxX = def.MaxX / mmPerUnit; res.SrcMaxY = def.MaxY / mmPerUnit;
        if (def.Entities.Count == 0) res.Warnings.Add("Không có thực thể nào được nhập (kiểm tra block/vùng chọn).");

        // điểm gốc
        var bas = o.Base;
        if (bas == TitleBlockBase.Auto) bas = o.Mode == TitleBlockMode.WholeFrame ? TitleBlockBase.BottomLeft : TitleBlockBase.BottomRight;
        var (bx, by) = bas switch
        {
            TitleBlockBase.BottomRight => (def.MaxX, def.MinY),
            TitleBlockBase.BottomLeft => (def.MinX, def.MinY),
            TitleBlockBase.TopRight => (def.MaxX, def.MaxY),
            TitleBlockBase.TopLeft => (def.MinX, def.MaxY),
            TitleBlockBase.Custom => (o.BaseX * mmPerUnit, o.BaseY * mmPerUnit),
            _ => (0.0, 0.0)
        };
        def.Translate(-bx, -by);
        res.Def = def;
        res.Info = $"{def.Entities.Count} thực thể, {def.Entities.Count(x => x.Type == "TEXT")} chữ, {res.FieldCount} trường; " +
                   $"kích thước {def.W.ToString("0.#", Inv)}×{def.H.ToString("0.#", Inv)} mm (1 đơn vị = {mmPerUnit.ToString("0.####", Inv)} mm)" +
                   (block.Length > 0 ? $"; block {block}" : "; toàn bộ model space");
        if (o.Mode == TitleBlockMode.TitleBox && (def.W > 600 || def.H > 300))
            res.Warnings.Add($"Khung tên rộng {def.W:0} × {def.H:0} mm – có thể bản vẽ vẽ theo tỷ lệ; nhập lại với hệ số mm/đơn vị nhỏ hơn hoặc chọn \"khung bao toàn tờ\".");
        return res;
    }

    private static List<double> Flat(IEnumerable<(double X, double Y)> pts, double k)
    {
        var l = new List<double>();
        foreach (var (x, y) in pts) { l.Add(x * k); l.Add(y * k); }
        return l;
    }

    // ==================================================================
    // Ảnh
    // ==================================================================
    /// <summary>Kích thước ảnh PNG/JPG theo mm (theo DPI ghi trong ảnh, mặc định 96 dpi). Không đọc được → 180×40.</summary>
    public static (double W, double H) ImageSizeMm(byte[] b)
    {
        try
        {
            int pw = 0, ph = 0; double dpiX = 96, dpiY = 96;
            if (b.Length > 24 && b[0] == 0x89 && b[1] == (byte)'P' && b[2] == (byte)'N' && b[3] == (byte)'G')
            {
                pw = BE32(b, 16); ph = BE32(b, 20);
                for (int i = 8; i + 12 <= b.Length;)
                {
                    int len = BE32(b, i);
                    string type = Encoding.ASCII.GetString(b, i + 4, 4);
                    if (type == "pHYs" && len >= 9 && b[i + 16] == 1)
                    {
                        dpiX = BE32(b, i + 8) * 0.0254; dpiY = BE32(b, i + 12) * 0.0254;
                        break;
                    }
                    if (type == "IDAT" || len < 0) break;
                    i += 12 + len;
                }
            }
            else if (b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8)
            {
                for (int i = 2; i + 9 < b.Length;)
                {
                    if (b[i] != 0xFF) { i++; continue; }
                    int m = b[i + 1];
                    int len = (b[i + 2] << 8) | b[i + 3];
                    if (m == 0xE0 && i + 16 < b.Length && Encoding.ASCII.GetString(b, i + 4, 4) == "JFIF")
                    {
                        int unit = b[i + 11], dx = (b[i + 12] << 8) | b[i + 13], dy = (b[i + 14] << 8) | b[i + 15];
                        if (unit == 1 && dx > 0 && dy > 0) { dpiX = dx; dpiY = dy; }
                        else if (unit == 2 && dx > 0 && dy > 0) { dpiX = dx * 2.54; dpiY = dy * 2.54; }
                    }
                    if (m >= 0xC0 && m <= 0xCF && m != 0xC4 && m != 0xC8 && m != 0xCC)
                    {
                        ph = (b[i + 5] << 8) | b[i + 6]; pw = (b[i + 7] << 8) | b[i + 8];
                        break;
                    }
                    i += 2 + len;
                }
            }
            if (pw <= 0 || ph <= 0) return (180, 40);
            if (dpiX < 20 || dpiX > 2400) dpiX = 96;
            if (dpiY < 20 || dpiY > 2400) dpiY = 96;
            return (pw / dpiX * 25.4, ph / dpiY * 25.4);
        }
        catch (Exception) { return (180, 40); }
    }

    private static int BE32(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];

    /// <summary>Khung tên dạng ảnh: ảnh phủ wMm × hMm (gốc góc phải dưới, hoặc góc trái dưới khi khung bao toàn tờ)
    /// + chữ trường đặt sẵn theo bố cục khung tên 6 hàng (người dùng chỉnh vị trí sau).</summary>
    public static TitleBlockDef FromImage(string base64, double wMm, double hMm, TitleBlockMode mode = TitleBlockMode.TitleBox, string name = null)
    {
        double w = wMm > 1 ? wMm : 180, h = hMm > 1 ? hMm : 40;
        var def = new TitleBlockDef
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Khung tên ảnh" : name.Trim(), Kind = TitleBlockKind.Image, Mode = mode,
            FitToPaper = mode == TitleBlockMode.WholeFrame, ImageData = base64 ?? "",
            MinX = 0, MinY = 0, MaxX = w, MaxY = h, Imported = DateTime.Now.ToString("yyyy-MM-dd HH:mm", Inv)
        };
        def.Fields = DefaultImageFields(w, h);
        if (mode == TitleBlockMode.TitleBox) def.Translate(-w, 0);
        return def;
    }

    /// <summary>Chữ trường mặc định cho khung tên ảnh rộng w × cao h (mm, gốc trái dưới).</summary>
    public static List<TitleBlockField> DefaultImageFields(double w, double h)
    {
        double rh = h / 6, cl = 0.2 * w, cr = 0.72 * w;
        double th = Math.Clamp(rh * 0.45, 1.5, 4);
        double Row(int i) => (i + 0.5) * rh;
        return new List<TitleBlockField>
        {
            new() { Key = TitleFields.CongTy, X = cl, Y = Row(5), H = th, MaxW = cr - cl - 2 },
            new() { Key = TitleFields.TenCongTrinh, X = cl, Y = Row(4), H = th, MaxW = cr - cl - 2 },
            new() { Key = TitleFields.DiaDiem, X = cl, Y = Row(3), H = th * 0.85, MaxW = cr - cl - 2 },
            new() { Key = TitleFields.TenBanVe, X = cl, Y = Row(2), H = th, MaxW = cr - cl - 2 },
            new() { Key = TitleFields.ThietKe, X = cl, Y = Row(1), H = th * 0.85, MaxW = (cr - cl) / 2 - 2 },
            new() { Key = TitleFields.KiemTra, X = cl, Y = Row(0), H = th * 0.85, MaxW = (cr - cl) / 2 - 2 },
            new() { Key = TitleFields.TyLe, Format = "TỶ LỆ: {TY_LE}", X = cr + 1.5, Y = Row(3), H = th * 0.8, MaxW = w - cr - 3 },
            new() { Key = TitleFields.Ngay, Format = "NGÀY: {NGAY}", X = cr + 1.5, Y = Row(2), H = th * 0.8, MaxW = w - cr - 3 },
            new() { Key = TitleFields.SoHieu, X = (cr + w) / 2, Y = Row(0) + 0.25 * rh, H = Math.Min(4.5, rh * 0.9), Ha = 1, MaxW = w - cr - 3 },
        };
    }
}

/// <summary>Đọc nhanh thông tin chữ trực tiếp từ DXF ASCII mà bộ đọc chung không giữ: góc xoay, căn lề của TEXT/MTEXT/ATTRIB (model space),
/// ATTDEF trong model space, và ATTDEF của block được chèn mà INSERT thiếu ATTRIB. Toạ độ theo đơn vị bản vẽ.</summary>
public sealed class DxfTextScan
{
    public sealed class TextInfo
    {
        public string Type = "";
        public double X, Y, Height, Rot;
        public int Ha, Va;
        public string Tag = "", Text = "", Layer = "0";
        /// <summary>ATTDEF lấy từ block được chèn: tên block</summary>
        public string Block = "";
    }

    /// <summary>TEXT/MTEXT/ATTRIB nằm trực tiếp trong model space (ATTRIB của INSERT cấp ngoài cùng).</summary>
    public List<TextInfo> ModelTexts { get; } = new();
    /// <summary>ATTDEF nằm trực tiếp trong model space (và ATTDEF của block chèn mà INSERT không có ATTRIB, đã biến đổi)</summary>
    public List<TextInfo> ModelAttDefs { get; } = new();

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static DxfTextScan FromFile(string path) => FromBytes(File.ReadAllBytes(path));

    public static DxfTextScan FromBytes(byte[] bytes)
    {
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException)
        {
            try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); text = Encoding.GetEncoding(1258).GetString(bytes); }
            catch (Exception) { text = Encoding.Latin1.GetString(bytes); }
        }
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var pairs = new List<(int Code, string Val)>(lines.Length / 2);
        for (int i = 0; i + 1 < lines.Length; i += 2)
        {
            if (!int.TryParse(lines[i].Trim(), NumberStyles.Integer, Inv, out int c)) break;
            pairs.Add((c, lines[i + 1].TrimEnd()));
        }
        var s = new DxfTextScan();
        s.Parse(pairs);
        return s;
    }

    private sealed class BlockInfo { public double Bx, By; public List<TextInfo> AttDefs = new(); public List<TextInfo> Texts = new(); }

    private void Parse(List<(int Code, string Val)> p)
    {
        var blocks = new Dictionary<string, BlockInfo>(StringComparer.OrdinalIgnoreCase);
        var inserts = new List<(string Name, double X, double Y, double Sx, double Sy, double Rot, bool HasAttribs)>();
        string section = "";
        BlockInfo curBlock = null;
        int i = 0;
        bool lastWasInsert = false;
        while (i < p.Count)
        {
            if (p[i].Code != 0) { i++; continue; }
            string type = p[i].Val.Trim();
            int j = i + 1;
            while (j < p.Count && p[j].Code != 0) j++;
            var body = p.GetRange(i + 1, j - i - 1);
            i = j;
            if (type == "SECTION") { section = Find(body, 2) ?? ""; continue; }
            if (type == "ENDSEC") { section = ""; continue; }
            if (section == "BLOCKS")
            {
                if (type == "BLOCK")
                {
                    curBlock = new BlockInfo { Bx = D(body, 10), By = D(body, 20) };
                    string name = Find(body, 2) ?? "";
                    if (name.Length > 0) blocks[name] = curBlock;
                }
                else if (type == "ENDBLK") curBlock = null;
                else if (type == "ATTDEF" && curBlock != null)
                {
                    var a = ReadText(type, body);
                    if (a != null && (I(body, 70) & 1) == 0) curBlock.AttDefs.Add(a);
                }
                else if ((type == "TEXT" || type == "MTEXT") && curBlock != null)
                {
                    var a = ReadText(type, body);
                    if (a != null) curBlock.Texts.Add(a);
                }
                continue;
            }
            if (section != "ENTITIES") continue;
            if (I(body, 67) == 1) { lastWasInsert = false; continue; }   // paper space
            switch (type)
            {
                case "TEXT":
                case "MTEXT":
                    lastWasInsert = false;
                    { var t = ReadText(type, body); if (t != null) ModelTexts.Add(t); }
                    break;
                case "ATTRIB":
                    { var t = ReadText(type, body); if (t != null) ModelTexts.Add(t); }
                    if (lastWasInsert && inserts.Count > 0) { var x = inserts[^1]; x.HasAttribs = true; inserts[^1] = x; }
                    break;
                case "ATTDEF":
                    lastWasInsert = false;
                    {
                        var t = ReadText(type, body);
                        if (t != null && (I(body, 70) & 1) == 0) ModelAttDefs.Add(t);
                    }
                    break;
                case "INSERT":
                    inserts.Add((Find(body, 2) ?? "", D(body, 10), D(body, 20), DOr(body, 41, 1), DOr(body, 42, 1), D(body, 50), false));
                    lastWasInsert = true;
                    break;
                case "SEQEND":
                    break;
                default:
                    lastWasInsert = false;
                    break;
            }
        }
        // chữ trong block được chèn (cấp ngoài cùng) → toạ độ model space (để lấy góc xoay/căn lề);
        // INSERT không có ATTRIB nhưng block có ATTDEF → đặt ATTDEF theo phép biến đổi của INSERT
        foreach (var ins in inserts)
        {
            if (!blocks.TryGetValue(ins.Name, out var b)) continue;
            double a = ins.Rot * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            foreach (var d in b.Texts)
            {
                double lx = (d.X - b.Bx) * ins.Sx, ly = (d.Y - b.By) * ins.Sy;
                ModelTexts.Add(new TextInfo
                {
                    Type = d.Type, X = ins.X + lx * c - ly * s, Y = ins.Y + lx * s + ly * c, Height = d.Height * Math.Sqrt(Math.Abs(ins.Sx * ins.Sy)),
                    Rot = d.Rot + ins.Rot, Ha = d.Ha, Va = d.Va, Tag = d.Tag, Text = d.Text, Layer = d.Layer, Block = ins.Name
                });
            }
            if (ins.HasAttribs) continue;
            foreach (var d in b.AttDefs)
            {
                double lx = (d.X - b.Bx) * ins.Sx, ly = (d.Y - b.By) * ins.Sy;
                ModelAttDefs.Add(new TextInfo
                {
                    Type = "ATTDEF", X = ins.X + lx * c - ly * s, Y = ins.Y + lx * s + ly * c, Height = d.Height * Math.Sqrt(Math.Abs(ins.Sx * ins.Sy)),
                    Rot = d.Rot + ins.Rot, Ha = d.Ha, Va = d.Va, Tag = d.Tag, Text = d.Text, Layer = d.Layer, Block = ins.Name
                });
            }
        }
    }

    private static string Find(List<(int Code, string Val)> b, int code)
    {
        foreach (var (c, v) in b) if (c == code) return v;
        return null;
    }

    private static double D(List<(int Code, string Val)> b, int code) => DOr(b, code, 0);

    private static double DOr(List<(int Code, string Val)> b, int code, double def)
    {
        string s = Find(b, code);
        return s != null && double.TryParse(s.Trim(), NumberStyles.Float, Inv, out double v) && double.IsFinite(v) ? v : def;
    }

    private static int I(List<(int Code, string Val)> b, int code)
    {
        string s = Find(b, code);
        return s != null && int.TryParse(s.Trim(), NumberStyles.Integer, Inv, out int v) ? v : 0;
    }

    private static TextInfo ReadText(string type, List<(int Code, string Val)> b)
    {
        var t = new TextInfo { Type = type, Layer = Find(b, 8) ?? "0", Height = D(b, 40), Tag = Find(b, 2) ?? "" };
        if (type == "MTEXT")
        {
            t.X = D(b, 10); t.Y = D(b, 20);
            string dx = Find(b, 11);
            t.Rot = dx != null ? Math.Atan2(D(b, 21), D(b, 11)) * 180 / Math.PI : D(b, 50) * 180 / Math.PI;
            int att = I(b, 71);
            if (att < 1 || att > 9) att = 1;
            t.Ha = (att - 1) % 3;
            t.Va = 3 - (att - 1) / 3;
            var sb = new StringBuilder();
            foreach (var (c, v) in b) if (c == 3) sb.Append(v);
            foreach (var (c, v) in b) if (c == 1) sb.Append(v);
            t.Text = DxfReader.CleanMText(sb.ToString());
        }
        else
        {
            int ha = I(b, 72), va = type == "TEXT" ? I(b, 73) : I(b, 74);
            bool aligned = (ha != 0 || va != 0) && Find(b, 11) != null;
            t.X = aligned ? D(b, 11) : D(b, 10);
            t.Y = aligned ? D(b, 21) : D(b, 20);
            t.Rot = D(b, 50);
            // 72: 0 trái, 1 giữa, 2 phải, 3 aligned, 4 middle, 5 fit
            t.Ha = ha switch { 1 or 4 => 1, 2 => 2, 3 or 5 => 0, _ => 0 };
            t.Va = ha == 4 ? 2 : Math.Clamp(va, 0, 3);
            if (ha is 3 or 5 && aligned) { t.Ha = 2; t.Va = 0; }   // chữ căn hai đầu: bộ đọc lấy điểm cuối (11) → căn phải tại đó
            if (GetOr(b, 230, 1) < 0) t.X = -t.X;
            t.Text = DxfReader.CleanMText(Find(b, 1) ?? "");
        }
        if (Math.Abs(t.Rot) < 1e-9) t.Rot = 0;
        return t;
    }

    private static double GetOr(List<(int Code, string Val)> b, int code, double def) => DOr(b, code, def);

    /// <summary>Tìm thông tin chữ tương ứng với thực thể chữ của bộ đọc (cùng điểm chèn, cùng chiều cao). unitM: đơn vị bản vẽ → m.</summary>
    public TextInfo Match(DxfEntity e, double unitM, HashSet<TextInfo> used = null)
    {
        if (e == null || e.Type != DxfEntityType.Text || e.Points.Count == 0 || unitM <= 0) return null;
        var (px, py) = e.Points[0];
        TextInfo best = null;
        double bd = double.MaxValue;
        foreach (var t in ModelTexts)
        {
            if (used != null && used.Contains(t)) continue;
            double x = t.X * unitM, y = t.Y * unitM;
            double tol = 1e-7 + 1e-9 * (Math.Abs(x) + Math.Abs(y));
            double d = Math.Abs(x - px) + Math.Abs(y - py);
            if (d > tol) continue;
            if (t.Height > 0 && e.TextHeight > 0 && Math.Abs(t.Height * unitM - e.TextHeight) > 1e-6 * Math.Max(1e-3, e.TextHeight) + 1e-12) continue;
            if (!string.IsNullOrEmpty(e.AttributeTag) && !string.Equals(e.AttributeTag, t.Tag, StringComparison.OrdinalIgnoreCase)) continue;
            if (d < bd) { bd = d; best = t; }
        }
        return best;
    }
}
