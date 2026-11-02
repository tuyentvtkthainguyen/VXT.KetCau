using System.Globalization;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Các chương được chọn đưa vào thuyết minh tự động.</summary>
public class ReportChapters
{
    /// <summary>Giới thiệu công trình</summary>
    public bool Intro { get; set; } = true;
    /// <summary>Tiêu chuẩn áp dụng</summary>
    public bool Standards { get; set; } = true;
    /// <summary>Vật liệu, tiết diện</summary>
    public bool Materials { get; set; } = true;
    /// <summary>Sơ đồ tính + tải trọng (tĩnh tải, hoạt tải, tổ hợp)</summary>
    public bool Loads { get; set; } = true;
    /// <summary>Tải trọng gió, động đất (mục con của chương tải trọng)</summary>
    public bool WindSeismic { get; set; } = true;
    /// <summary>Kết quả nội lực</summary>
    public bool Forces { get; set; } = true;
    public bool Beams { get; set; } = true;
    public bool Columns { get; set; } = true;
    public bool Slabs { get; set; } = true;
    /// <summary>Móng đơn / đài cọc / móng băng</summary>
    public bool Foundation { get; set; } = true;
    /// <summary>Địa chất (khi có hố khoan)</summary>
    public bool Geotech { get; set; } = true;
    /// <summary>Thống kê cốt thép + kết cấu thép (nếu có)</summary>
    public bool Steel { get; set; } = true;
    /// <summary>Bảng khối lượng vật tư</summary>
    public bool Quantities { get; set; } = true;
    /// <summary>Phụ lục công thức tính toán</summary>
    public bool Appendix { get; set; } = true;
}

/// <summary>Tuỳ chọn lập thuyết minh tính toán tự động (CalculationReport.Export(..., ReportOptions)).
/// Lưu kèm dự án (Project.ReportOptions) để lần sau không phải nhập lại trang bìa.</summary>
public class ReportOptions
{
    public ReportChapters Chapters { get; set; } = new();

    // ---- trang bìa ----
    public string ProjectName { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Location { get; set; } = "";
    /// <summary>Đơn vị thiết kế</summary>
    public string DesignCompany { get; set; } = "";
    /// <summary>Chủ trì thiết kế</summary>
    public string ChiefDesigner { get; set; } = "";
    /// <summary>Người lập</summary>
    public string Preparer { get; set; } = "";
    /// <summary>Người kiểm tra</summary>
    public string Checker { get; set; } = "";
    /// <summary>Ngày lập (dd/MM/yyyy); rỗng = ngày hiện tại</summary>
    public string Date { get; set; } = "";

    /// <summary>Số ví dụ tính toán chi tiết cho mỗi loại cấu kiện (1–3)</summary>
    public int ExamplesPerType { get; set; } = 1;
    /// <summary>Đưa bảng nội lực bao từng thanh vào chương nội lực</summary>
    public bool IncludeForceTables { get; set; }
    /// <summary>Số hàng tối đa của bảng nội lực bao</summary>
    public int MaxForceRows { get; set; } = 400;

    /// <summary>Mục lục (trường TOC của Word, tự cập nhật khi mở)</summary>
    public bool TableOfContents { get; set; } = true;
    /// <summary>Số trang ở chân trang</summary>
    public bool PageNumbers { get; set; } = true;
    /// <summary>Tự đánh số tên bảng "Bảng n.k"</summary>
    public bool TableCaptions { get; set; } = true;

    public int Examples => Math.Clamp(ExamplesPerType, 1, 3);

    public string DateText() =>
        string.IsNullOrWhiteSpace(Date) ? DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : Date.Trim();

    /// <summary>Tuỳ chọn mặc định lấy thông tin trang bìa từ dự án (tên, chủ đầu tư, địa điểm, khung tên).</summary>
    public static ReportOptions FromProject(Project p)
    {
        var o = p?.ReportOptions != null ? Clone(p.ReportOptions) : new ReportOptions();
        if (p == null) return o;
        var tb = p.TitleBlock ?? new TitleBlockInfo();
        if (string.IsNullOrWhiteSpace(o.ProjectName)) o.ProjectName = First(tb.ProjectName, p.Name);
        if (string.IsNullOrWhiteSpace(o.Owner)) o.Owner = First(tb.Owner, p.Owner);
        if (string.IsNullOrWhiteSpace(o.Location)) o.Location = First(tb.Location, p.Location, string.Join(", ", new[] { p.LocationName, p.LocationProvince }.Where(s => !string.IsNullOrWhiteSpace(s))));
        if (string.IsNullOrWhiteSpace(o.DesignCompany)) o.DesignCompany = tb.Company ?? "";
        if (string.IsNullOrWhiteSpace(o.ChiefDesigner)) o.ChiefDesigner = tb.Approver ?? "";
        if (string.IsNullOrWhiteSpace(o.Preparer)) o.Preparer = First(tb.Designer, p.Designer);
        if (string.IsNullOrWhiteSpace(o.Checker)) o.Checker = tb.Checker ?? "";
        return o;
    }

    public static ReportOptions Clone(ReportOptions o) =>
        System.Text.Json.JsonSerializer.Deserialize<ReportOptions>(System.Text.Json.JsonSerializer.Serialize(o ?? new ReportOptions()));

    private static string First(params string[] v) => v.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim() ?? "";
}
