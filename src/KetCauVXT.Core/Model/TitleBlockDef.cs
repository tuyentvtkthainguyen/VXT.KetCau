using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KetCauVXT.Model;

/// <summary>Kiểu khung tên người dùng: hình vector (từ DWG/DXF) hoặc ảnh (PNG/JPG) có chữ trường đặt chồng lên.</summary>
public enum TitleBlockKind { Vector, Image }

/// <summary>Cách đặt khung tên người dùng lên tờ.</summary>
public enum TitleBlockMode
{
    /// <summary>Chỉ ô khung tên: điểm gốc (mặc định góc phải dưới) đặt vào góc phải dưới khung viền trong; vẫn vẽ khung viền của chương trình.</summary>
    TitleBox,
    /// <summary>Khung bao toàn tờ (khung viền + khung tên): góc trái dưới phạm vi hình đặt vào góc giấy, có thể co vừa khổ giấy.</summary>
    WholeFrame
}

/// <summary>Một thực thể hình của khung tên người dùng, toạ độ mm giấy cục bộ (gốc = điểm chèn của khung tên, y hướng lên).</summary>
public class TitleBlockEntity
{
    /// <summary>"LINE", "PLINE", "CIRCLE", "ARC", "TEXT"</summary>
    public string Type { get; set; } = "LINE";
    /// <summary>LINE/PLINE: x0,y0,x1,y1,…; CIRCLE/ARC: tâm x,y; TEXT: điểm chèn x,y.</summary>
    public List<double> Pts { get; set; } = new();
    public bool Closed { get; set; }
    /// <summary>Bán kính (CIRCLE/ARC)</summary>
    public double R { get; set; }
    /// <summary>Góc đầu/cuối cung (độ, ngược chiều kim đồng hồ)</summary>
    public double A1 { get; set; }
    public double A2 { get; set; }
    /// <summary>Bề rộng nét đa tuyến (mm giấy); 0 = nét mảnh</summary>
    public double Width { get; set; }
    /// <summary>TEXT: nội dung gốc, có thể chứa chỗ trống {TEN_CONG_TRINH}…; xuống dòng bằng \n</summary>
    public string Text { get; set; } = "";
    /// <summary>Chiều cao chữ (mm giấy)</summary>
    public double H { get; set; } = 2.5;
    /// <summary>Góc xoay chữ (độ)</summary>
    public double Rot { get; set; }
    /// <summary>Căn ngang 0 trái, 1 giữa, 2 phải</summary>
    public int Ha { get; set; }
    /// <summary>Căn đứng 0 chân chữ, 1 đáy, 2 giữa, 3 đỉnh</summary>
    public int Va { get; set; }
    /// <summary>Trường gán cho chữ này (vd. TEN_CONG_TRINH): cả dòng chữ được thay bằng giá trị trường. "" = giữ chữ gốc (chỉ thay chỗ trống {…}).</summary>
    public string Field { get; set; } = "";
    /// <summary>Thẻ ATTRIB/ATTDEF gốc (nếu có)</summary>
    public string Tag { get; set; } = "";
    /// <summary>Bề rộng tối đa của chữ sau khi thay trường (mm); &gt; 0 = co chiều cao chữ cho vừa.</summary>
    public double MaxW { get; set; }
    /// <summary>Layer trong file gốc (chỉ để tham khảo; khi xuất mọi thực thể nằm trên lớp KHUNG_TEN)</summary>
    public string SourceLayer { get; set; } = "";
}

/// <summary>Chữ trường đặt chồng lên khung tên dạng ảnh (hoặc thêm vào khung tên vector), toạ độ mm cục bộ.</summary>
public class TitleBlockField
{
    /// <summary>Khoá trường (TEN_CONG_TRINH, SO_HIEU…)</summary>
    public string Key { get; set; } = "";
    /// <summary>Mẫu chữ; trống = "{Key}". Ví dụ "TỶ LỆ: {TY_LE}".</summary>
    public string Format { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double H { get; set; } = 2.5;
    public double Rot { get; set; }
    public int Ha { get; set; }
    public int Va { get; set; } = 2;
    /// <summary>Bề rộng tối đa (mm); &gt; 0 = co chữ cho vừa</summary>
    public double MaxW { get; set; }
}

/// <summary>Khung tên / khung bản vẽ của người dùng (tải từ DWG/DXF/ảnh). Lưu trong TitleBlockInfo.Custom của dự án
/// và trong gói mẫu định dạng (*.kcvxt-mau). Toạ độ mm giấy cục bộ, gốc (0,0) = điểm chèn.</summary>
public class TitleBlockDef
{
    public string Name { get; set; } = "Khung tên người dùng";
    public TitleBlockKind Kind { get; set; } = TitleBlockKind.Vector;
    public TitleBlockMode Mode { get; set; } = TitleBlockMode.TitleBox;
    /// <summary>WholeFrame: co giãn đều cho vừa khổ giấy của từng tờ</summary>
    public bool FitToPaper { get; set; } = true;
    /// <summary>Hệ số phóng thêm khi đặt lên tờ (1 = đúng kích thước)</summary>
    public double Scale { get; set; } = 1;
    /// <summary>Phạm vi hình (mm cục bộ)</summary>
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public List<TitleBlockEntity> Entities { get; set; } = new();
    /// <summary>Chữ trường đặt thêm (khung tên ảnh: toàn bộ trường)</summary>
    public List<TitleBlockField> Fields { get; set; } = new();
    /// <summary>Khung tên ảnh: dữ liệu ảnh base64 PNG/JPG, phủ đúng phạm vi Min..Max</summary>
    public string ImageData { get; set; } = "";
    /// <summary>Ô đặt logo công ty (mm cục bộ); W = 0 = không có</summary>
    public double LogoX { get; set; }
    public double LogoY { get; set; }
    public double LogoW { get; set; }
    public double LogoH { get; set; }
    /// <summary>Vẽ thêm khung viền trong của chương trình (chế độ TitleBox)</summary>
    public bool DrawInnerFrame { get; set; } = true;
    /// <summary>File nguồn (tên file), ngày nhập</summary>
    public string Source { get; set; } = "";
    public string Imported { get; set; } = "";

    public double W => Math.Max(0, MaxX - MinX);
    public double H => Math.Max(0, MaxY - MinY);

    /// <summary>Tính lại phạm vi từ thực thể (khung tên ảnh giữ phạm vi đã đặt).</summary>
    public void UpdateExtents()
    {
        if (Kind == TitleBlockKind.Image) return;
        double x1 = double.MaxValue, y1 = double.MaxValue, x2 = double.MinValue, y2 = double.MinValue;
        void U(double x, double y) { x1 = Math.Min(x1, x); y1 = Math.Min(y1, y); x2 = Math.Max(x2, x); y2 = Math.Max(y2, y); }
        foreach (var e in Entities ?? new List<TitleBlockEntity>())
        {
            if (e.Pts == null || e.Pts.Count < 2) continue;
            if (e.Type is "CIRCLE" or "ARC") { U(e.Pts[0] - e.R, e.Pts[1] - e.R); U(e.Pts[0] + e.R, e.Pts[1] + e.R); }
            else if (e.Type == "TEXT") U(e.Pts[0], e.Pts[1]);
            else for (int i = 0; i + 1 < e.Pts.Count; i += 2) U(e.Pts[i], e.Pts[i + 1]);
        }
        if (x1 > x2) { x1 = y1 = x2 = y2 = 0; }
        MinX = x1; MinY = y1; MaxX = x2; MaxY = y2;
    }

    /// <summary>Tịnh tiến toàn bộ hình (đổi điểm gốc).</summary>
    public void Translate(double dx, double dy)
    {
        foreach (var e in Entities ?? new List<TitleBlockEntity>())
            if (e.Pts != null)
                for (int i = 0; i + 1 < e.Pts.Count; i += 2) { e.Pts[i] += dx; e.Pts[i + 1] += dy; }
        foreach (var f in Fields ?? new List<TitleBlockField>()) { f.X += dx; f.Y += dy; }
        if (LogoW > 0) { LogoX += dx; LogoY += dy; }
        MinX += dx; MaxX += dx; MinY += dy; MaxY += dy;
    }

    /// <summary>Các khoá trường đang dùng (chữ gán trường, chỗ trống {…}, chữ trường đặt thêm).</summary>
    public List<string> UsedFields()
    {
        var res = new List<string>();
        void Add(string k) { if (!string.IsNullOrEmpty(k) && !res.Contains(k)) res.Add(k); }
        foreach (var e in Entities ?? new List<TitleBlockEntity>())
        {
            if (e.Type != "TEXT") continue;
            Add(e.Field);
            foreach (var k in TitleFields.Placeholders(e.Text)) Add(k);
        }
        foreach (var f in Fields ?? new List<TitleBlockField>())
        {
            Add(f.Key);
            foreach (var k in TitleFields.Placeholders(f.Format)) Add(k);
        }
        return res;
    }
}

/// <summary>Danh mục trường khung tên: khoá chuẩn, tên hiển thị, bí danh (tiếng Việt có/không dấu, tiếng Anh, thẻ ATTRIB hay gặp),
/// tìm chỗ trống {KHOA} trong chữ và giá trị trường theo dự án + tờ.</summary>
public static class TitleFields
{
    public const string CongTy = "CONG_TY", TenCongTrinh = "TEN_CONG_TRINH", ChuDauTu = "CHU_DAU_TU", DiaDiem = "DIA_DIEM",
        TenBanVe = "TEN_BAN_VE", SoHieu = "SO_HIEU", TyLe = "TY_LE", Ngay = "NGAY", ThietKe = "THIET_KE", KiemTra = "KIEM_TRA",
        ChuTri = "CHU_TRI", Duyet = "DUYET", LanSua = "LAN_SUA", Trang = "TRANG", TongTrang = "TONG_TRANG";

    /// <summary>(khoá, tên hiển thị, bí danh)</summary>
    public static readonly (string Key, string Label, string[] Aliases)[] All =
    {
        (CongTy, "Đơn vị thiết kế", new[] { "CONGTY", "TEN_CONG_TY", "DON_VI", "DON_VI_THIET_KE", "DONVITHIETKE", "DVTK", "COMPANY", "FIRM", "TVTK", "TU_VAN_THIET_KE", "CO_QUAN" }),
        (TenCongTrinh, "Tên công trình", new[] { "CONG_TRINH", "CONGTRINH", "TEN_CT", "TENCT", "CT", "PROJECT", "PROJECT_NAME", "PROJECTNAME", "DU_AN", "TEN_DU_AN" }),
        (ChuDauTu, "Chủ đầu tư", new[] { "CHUDAUTU", "CDT", "OWNER", "CLIENT", "CHU_NHA" }),
        (DiaDiem, "Địa điểm", new[] { "DIADIEM", "DIA_DIEM_XD", "DIA_CHI", "DIACHI", "LOCATION", "ADDRESS", "SITE" }),
        (TenBanVe, "Tên bản vẽ", new[] { "TENBANVE", "TEN_BV", "BAN_VE", "BANVE", "TEN_TO", "DRAWING", "DRAWING_NAME", "DWG_NAME", "TITLE", "SHEET_NAME", "SHEETTITLE" }),
        (SoHieu, "Số hiệu bản vẽ", new[] { "SOHIEU", "SO_HIEU_BV", "SO_BV", "KY_HIEU", "KYHIEU", "KH_BV", "DRAWING_NO", "DWG_NO", "DWGNO", "SHEET_NO", "SHEETNO", "NUMBER", "SO_TO" }),
        (TyLe, "Tỷ lệ", new[] { "TYLE", "TI_LE", "TILE", "SCALE" }),
        (Ngay, "Ngày", new[] { "NGAY_THANG", "NGAYTHANG", "NGAY_HOAN_THANH", "DATE", "THOI_GIAN" }),
        (ThietKe, "Thiết kế", new[] { "THIETKE", "TK", "NGUOI_THIET_KE", "DESIGNER", "DESIGNED", "DESIGNED_BY", "VE", "NGUOI_VE", "DRAWN", "DRAWN_BY" }),
        (KiemTra, "Kiểm tra", new[] { "KIEMTRA", "KT", "NGUOI_KIEM_TRA", "CHECKER", "CHECKED", "CHECKED_BY" }),
        (ChuTri, "Chủ trì", new[] { "CHUTRI", "CHU_TRI_TK", "CHU_TRI_THIET_KE", "CNDA", "CHU_NHIEM", "CHU_NHIEM_DU_AN", "LEADER", "LEAD", "PROJECT_LEADER" }),
        (Duyet, "Duyệt", new[] { "PHE_DUYET", "PHEDUYET", "NGUOI_DUYET", "APPROVER", "APPROVED", "APPROVED_BY", "GIAM_DOC", "GD" }),
        (LanSua, "Lần sửa", new[] { "LANSUA", "LAN_SUA_DOI", "SUA_DOI", "REV", "REVISION", "PHIEN_BAN" }),
        (Trang, "Tờ số", new[] { "TO", "TO_SO", "TOSO", "SO_TRANG", "PAGE", "SHEET" }),
        (TongTrang, "Tổng số tờ", new[] { "TONGTRANG", "TONG_SO_TO", "TONG_TO", "SO_TO_TONG", "PAGES", "TOTAL", "SHEETS", "TOTAL_SHEETS" }),
    };

    public static IEnumerable<string> Keys => All.Select(a => a.Key);

    public static string Label(string key)
    {
        foreach (var a in All) if (a.Key == key) return a.Label;
        return key ?? "";
    }

    /// <summary>Bỏ dấu tiếng Việt, đổi đ → d, chữ hoa, ký tự khác chữ/số → "_", gộp "_".</summary>
    public static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        string d = s.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in d)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
        }
        return sb.ToString().Trim('_');
    }

    /// <summary>Khoá chuẩn của một tên trường / thẻ ATTRIB (không phân biệt hoa thường, có/không dấu, có/không "_"); null nếu không nhận ra.</summary>
    public static string Canonical(string name)
    {
        string n = Normalize(name);
        if (n.Length == 0) return null;
        string compact = n.Replace("_", "");
        foreach (var (key, label, aliases) in All)
        {
            if (n == key || compact == key.Replace("_", "") || Normalize(label) == n || Normalize(label).Replace("_", "") == compact) return key;
            foreach (var a in aliases)
                if (n == a || compact == a.Replace("_", "")) return key;
        }
        return null;
    }

    /// <summary>Khoá dùng cho chỗ trống {…}: khoá chuẩn nếu nhận ra, ngược lại tên đã chuẩn hoá (trường tự đặt, lấy ở TitleBlockInfo.Extra).</summary>
    public static string KeyOf(string name) => Canonical(name) ?? Normalize(name);

    private static readonly Regex Rx = new(@"\{([^{}\r\n]{1,60})\}", RegexOptions.Compiled);

    /// <summary>Các khoá chỗ trống {…} trong chữ.</summary>
    public static List<string> Placeholders(string text)
    {
        var res = new List<string>();
        if (string.IsNullOrEmpty(text)) return res;
        foreach (Match m in Rx.Matches(text))
        {
            string k = KeyOf(m.Groups[1].Value);
            if (k.Length > 0 && !res.Contains(k)) res.Add(k);
        }
        return res;
    }

    public static bool HasPlaceholder(string text) => !string.IsNullOrEmpty(text) && Rx.IsMatch(text);

    /// <summary>Thay mọi chỗ trống {…} bằng giá trị (khoá không có giá trị → chuỗi rỗng).</summary>
    public static string Fill(string text, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return Rx.Replace(text, m =>
        {
            string k = KeyOf(m.Groups[1].Value);
            return values != null && values.TryGetValue(k, out var v) ? v ?? "" : "";
        });
    }

    private static string Eff(params string[] v) => v.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";

    /// <summary>Giá trị mọi trường cho tờ s của dự án p. scaleText: chuỗi tỷ lệ (ghi chú tỷ lệ của tờ được ưu tiên).</summary>
    public static Dictionary<string, string> Values(Project p, Sheet s, string scaleText = null)
    {
        var info = p?.TitleBlock ?? new TitleBlockInfo();
        s ??= new Sheet();
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (info.Extra != null)
            foreach (var kv in info.Extra)
            {
                string k = KeyOf(kv.Key);
                if (k.Length > 0) d[k] = kv.Value ?? "";
            }
        int idx = p?.Sheets?.IndexOf(s) ?? -1;
        int total = p?.Sheets?.Count ?? 0;
        d[CongTy] = Eff(info.Company);
        d[TenCongTrinh] = Eff(info.ProjectName, p?.Name);
        d[ChuDauTu] = Eff(info.Owner, p?.Owner);
        d[DiaDiem] = Eff(info.Location, p?.Location);
        d[TenBanVe] = Eff(s.Name);
        d[SoHieu] = Eff(s.Number);
        d[TyLe] = Eff(s.ScaleNote, scaleText);
        d[Ngay] = Eff(s.Date);
        d[ThietKe] = Eff(s.Designer, info.Designer, p?.Designer);
        d[KiemTra] = Eff(s.Checker, info.Checker);
        d[ChuTri] = Eff(info.Leader);
        d[Duyet] = Eff(s.Approver, info.Approver);
        d[LanSua] = Eff(s.Revision);
        d[Trang] = idx >= 0 ? (idx + 1).ToString(CultureInfo.InvariantCulture) : "";
        d[TongTrang] = total > 0 && idx >= 0 ? total.ToString(CultureInfo.InvariantCulture) : "";
        return d;
    }
}
