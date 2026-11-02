using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KetCauVXT.Model;

// =====================================================================
// THIẾT LẬP BẢN VẼ: tỷ lệ theo loại bản vẽ, cỡ chữ (mm trên giấy) cho kích thước, ghi chú, nhãn thép, tên hình,
// đầu trục, bảng thống kê; hệ số rộng chữ; bật/tắt các chú thích tuỳ chọn.
// Lưu trong CadStandard.Drawing → cùng file mặc định của người dùng (%AppData%\KetCauVXT\CadStandard.json)
// và theo dự án (Project.CadStandard). File JSON cũ không có mục "Drawing" → dùng giá trị mặc định
// (mặc định = đúng các giá trị chương trình vẫn dùng: chữ 2,5 mm, mặt đứng 1/50, mặt cắt 1/25, mặt bằng 1/100…).
// =====================================================================

/// <summary>Loại bản vẽ có tỷ lệ riêng.</summary>
public enum DrawingKind
{
    /// <summary>Mặt bằng kết cấu tầng</summary>
    FramingPlan,
    /// <summary>Mặt bằng móng</summary>
    FoundationPlan,
    /// <summary>Mặt bằng thép sàn</summary>
    SlabPlan,
    /// <summary>Mặt đứng khung trục (cả biểu đồ nội lực)</summary>
    FrameElevation,
    /// <summary>Chi tiết dầm – mặt đứng (cả mặt cắt dọc móng băng)</summary>
    BeamElevation,
    /// <summary>Mặt cắt dầm / cột</summary>
    MemberSection,
    /// <summary>Chi tiết cột – mặt đứng</summary>
    ColumnElevation,
    /// <summary>Chi tiết móng (mặt bằng + mặt cắt móng đơn, đài cọc, mặt cắt ngang móng băng)</summary>
    FootingDetail,
    /// <summary>Cầu thang – mặt bằng</summary>
    StairPlan,
    /// <summary>Cầu thang – mặt cắt</summary>
    StairSection,
    /// <summary>Gia công thép (shop drawing): sơ đồ không theo tỷ lệ, vẽ theo mm giấy</summary>
    Shop
}

/// <summary>Vai trò của chữ trên bản vẽ (mỗi vai trò một cỡ chữ).</summary>
public enum TextRole
{
    /// <summary>Ghi chú, tên cấu kiện trên mặt bằng, cao độ, dòng tỷ lệ "tl: 1/50" (đồng thời là đơn vị bố trí khoảng cách)</summary>
    Note,
    /// <summary>Chữ số kích thước</summary>
    Dimension,
    /// <summary>Nhãn thép "3Ø22 ①", "Ø6a100 ④"</summary>
    Tag,
    /// <summary>Tên hình cấu kiện / mặt bằng "D2-8 (SL=2; L=20300)"</summary>
    MemberTitle,
    /// <summary>Tên mặt cắt "1-1", "MẶT CẮT M1"</summary>
    SectionTitle,
    /// <summary>Tên trục trong vòng tròn đầu trục</summary>
    Axis,
    /// <summary>Chữ trong bảng thống kê thép</summary>
    Schedule
}

/// <summary>Thiết lập bản vẽ (tỷ lệ, cỡ chữ, chú thích tuỳ chọn). Mọi kích thước tính bằng mm trên giấy in.</summary>
public class DrawingStyle
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Danh sách tỷ lệ chọn nhanh (mẫu số).</summary>
    public static readonly double[] StandardScales = { 10, 20, 25, 30, 50, 75, 100, 150, 200 };

    /// <summary>Thứ tự hiển thị các loại bản vẽ trên bảng thiết lập.</summary>
    public static readonly DrawingKind[] Kinds =
    {
        DrawingKind.FramingPlan, DrawingKind.FoundationPlan, DrawingKind.SlabPlan, DrawingKind.FrameElevation,
        DrawingKind.BeamElevation, DrawingKind.MemberSection, DrawingKind.ColumnElevation, DrawingKind.FootingDetail,
        DrawingKind.StairPlan, DrawingKind.StairSection, DrawingKind.Shop
    };

    public static readonly TextRole[] Roles =
    {
        TextRole.Dimension, TextRole.Note, TextRole.Tag, TextRole.MemberTitle, TextRole.SectionTitle, TextRole.Axis, TextRole.Schedule
    };

    // ---------------- tỷ lệ (mẫu số; 0 = tự chọn với mặt bằng thép sàn) ----------------
    public double ScaleFramingPlan { get; set; } = 100;
    public double ScaleFoundationPlan { get; set; } = 100;
    /// <summary>0 = tự chọn: 1/50 nếu mặt bằng ≤ 25 m, ngược lại 1/100</summary>
    public double ScaleSlabPlan { get; set; }
    public double ScaleFrameElevation { get; set; } = 100;
    public double ScaleBeamElevation { get; set; } = 50;
    public double ScaleMemberSection { get; set; } = 25;
    public double ScaleColumnElevation { get; set; } = 50;
    public double ScaleFootingDetail { get; set; } = 25;
    public double ScaleStairPlan { get; set; } = 50;
    public double ScaleStairSection { get; set; } = 25;

    // ---------------- cỡ chữ, ký hiệu (mm trên giấy) ----------------
    /// <summary>Chữ số kích thước</summary>
    public double DimTextHeight { get; set; } = 2.5;
    /// <summary>Chiều dài gạch xiên đầu đường kích thước</summary>
    public double DimTickSize { get; set; } = 2.5;
    /// <summary>Khoảng hở từ điểm đo tới đầu đường gióng</summary>
    public double DimExtOffset { get; set; } = 0.625;
    /// <summary>Đường gióng vượt qua đường kích thước</summary>
    public double DimExtBeyond { get; set; } = 1.25;
    /// <summary>Đường kích thước kéo dài qua đường gióng</summary>
    public double DimLineExtend { get; set; } = 1.0;
    /// <summary>Chữ số cách đường kích thước</summary>
    public double DimTextGap { get; set; } = 0.75;
    /// <summary>Ghi chú (đồng thời là đơn vị khoảng cách bố trí hình)</summary>
    public double NoteHeight { get; set; } = 2.5;
    /// <summary>Nhãn thép</summary>
    public double TagHeight { get; set; } = 2.5;
    /// <summary>Đường kính vòng tròn số hiệu thép (chữ số bên trong = 0,9 × chữ nhãn)</summary>
    public double MarkDiameter { get; set; } = 5.5;
    /// <summary>Tên hình cấu kiện / mặt bằng</summary>
    public double MemberTitleHeight { get; set; } = 3.5;
    /// <summary>Tên mặt cắt</summary>
    public double SectionTitleHeight { get; set; } = 3.5;
    /// <summary>Đường kính vòng tròn đầu trục</summary>
    public double AxisBubbleDiameter { get; set; } = 7.5;
    /// <summary>Tên trục</summary>
    public double AxisTextHeight { get; set; } = 3.0;
    /// <summary>Chữ bảng thống kê thép</summary>
    public double ScheduleTextHeight { get; set; } = 2.5;
    /// <summary>Hệ số rộng chữ (1 = chuẩn; 0,8 = chữ hẹp)</summary>
    public double WidthFactor { get; set; } = 1.0;

    // ---------------- chú thích tuỳ chọn ----------------
    /// <summary>Tên phần tử nội bộ (số hiệu thanh B12, C5, S7…) cho cấu kiện chưa có tên thiết kế</summary>
    public bool ShowMemberIds { get; set; } = true;
    /// <summary>Giá trị nội lực trên biểu đồ và dòng "Giá trị lớn nhất…"</summary>
    public bool ShowLoadValues { get; set; } = true;
    /// <summary>Giá trị thiết kế (Tmax dầm cong, sức chịu tải cọc [P]…)</summary>
    public bool ShowDesignValues { get; set; } = true;
    /// <summary>Ký hiệu cao độ ▽</summary>
    public bool ShowElevationMarks { get; set; } = true;
    /// <summary>Chuỗi kích thước lưới trục trên mặt bằng, mặt đứng khung</summary>
    public bool ShowGridDims { get; set; } = true;
    /// <summary>Khối "GHI CHÚ CHUNG" (một khối cho mỗi bản vẽ)</summary>
    public bool ShowGeneralNotes { get; set; } = true;

    // ==================================================================
    public static DrawingStyle Default() => new();

    private static readonly DrawingStyle _builtin = new();

    /// <summary>Thiết lập tạm (xem trước, kiểm thử) – ưu tiên hơn chuẩn đang áp dụng.</summary>
    private static DrawingStyle _override;

    /// <summary>Thiết lập đang áp dụng: tạm (Use) → CadStandard.Active.Drawing → mặc định chương trình.</summary>
    public static DrawingStyle Current => _override ?? CadStandard.Active?.Drawing ?? _builtin;

    /// <summary>Tạm dùng thiết lập khác; Dispose → trả lại. <code>using (DrawingStyle.Use(st)) …</code></summary>
    public static IDisposable Use(DrawingStyle s)
    {
        var prev = _override;
        _override = s?.Clone();
        return new Restore(prev);
    }

    private sealed class Restore : IDisposable
    {
        private DrawingStyle _prev; private bool _done;
        public Restore(DrawingStyle prev) { _prev = prev; }
        public void Dispose() { if (_done) return; _done = true; _override = _prev; _prev = null; }
    }

    // ------------------------------------------------------------------ tỷ lệ
    /// <summary>Mẫu số tỷ lệ của loại bản vẽ (mặt bằng thép sàn có thể là 0 = tự chọn; gia công thép luôn 1).</summary>
    public double ScaleOf(DrawingKind k) => k switch
    {
        DrawingKind.FramingPlan => ScaleFramingPlan,
        DrawingKind.FoundationPlan => ScaleFoundationPlan,
        DrawingKind.SlabPlan => ScaleSlabPlan,
        DrawingKind.FrameElevation => ScaleFrameElevation,
        DrawingKind.BeamElevation => ScaleBeamElevation,
        DrawingKind.MemberSection => ScaleMemberSection,
        DrawingKind.ColumnElevation => ScaleColumnElevation,
        DrawingKind.FootingDetail => ScaleFootingDetail,
        DrawingKind.StairPlan => ScaleStairPlan,
        DrawingKind.StairSection => ScaleStairSection,
        _ => 1
    };

    public void SetScale(DrawingKind k, double v)
    {
        switch (k)
        {
            case DrawingKind.FramingPlan: ScaleFramingPlan = v; break;
            case DrawingKind.FoundationPlan: ScaleFoundationPlan = v; break;
            case DrawingKind.SlabPlan: ScaleSlabPlan = v; break;
            case DrawingKind.FrameElevation: ScaleFrameElevation = v; break;
            case DrawingKind.BeamElevation: ScaleBeamElevation = v; break;
            case DrawingKind.MemberSection: ScaleMemberSection = v; break;
            case DrawingKind.ColumnElevation: ScaleColumnElevation = v; break;
            case DrawingKind.FootingDetail: ScaleFootingDetail = v; break;
            case DrawingKind.StairPlan: ScaleStairPlan = v; break;
            case DrawingKind.StairSection: ScaleStairSection = v; break;
        }
    }

    /// <summary>Tên tiếng Việt của loại bản vẽ.</summary>
    public static string KindLabel(DrawingKind k) => k switch
    {
        DrawingKind.FramingPlan => "Mặt bằng kết cấu",
        DrawingKind.FoundationPlan => "Mặt bằng móng",
        DrawingKind.SlabPlan => "Mặt bằng thép sàn",
        DrawingKind.FrameElevation => "Mặt đứng khung, biểu đồ nội lực",
        DrawingKind.BeamElevation => "Chi tiết dầm (mặt đứng)",
        DrawingKind.MemberSection => "Mặt cắt dầm / cột",
        DrawingKind.ColumnElevation => "Chi tiết cột (mặt đứng)",
        DrawingKind.FootingDetail => "Chi tiết móng",
        DrawingKind.StairPlan => "Cầu thang – mặt bằng",
        DrawingKind.StairSection => "Cầu thang – mặt cắt",
        DrawingKind.Shop => "Gia công thép (shop drawing)",
        _ => k.ToString()
    };

    /// <summary>Loại bản vẽ có tỷ lệ cố định (không đổi được).</summary>
    public static bool FixedScale(DrawingKind k) => k == DrawingKind.Shop;

    /// <summary>"1/50"; 0 → "Tự chọn"; Shop → "Không theo tỷ lệ".</summary>
    public static string ScaleLabel(DrawingKind k, double v) =>
        FixedScale(k) ? "Không theo tỷ lệ (sơ đồ)" : v <= 0 ? "Tự chọn" : "1/" + v.ToString("0.##", Inv);

    /// <summary>Đọc "1/50", "50", "Tự chọn" (→ 0). false nếu sai.</summary>
    public static bool TryParseScale(string s, out double v)
    {
        v = 0;
        string t = (s ?? "").Trim();
        if (t.Length == 0 || t.StartsWith("Tự", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Tu", StringComparison.OrdinalIgnoreCase)) return true;
        if (t.StartsWith("1/", StringComparison.Ordinal) || t.StartsWith("1:", StringComparison.Ordinal)) t = t.Substring(2);
        return double.TryParse(t.Replace(',', '.'), NumberStyles.Float, Inv, out v) && v > 0 && v <= 2000;
    }

    // ------------------------------------------------------------------ cỡ chữ
    /// <summary>Chiều cao chữ của vai trò (mm giấy).</summary>
    public double Height(TextRole r) => r switch
    {
        TextRole.Dimension => DimTextHeight,
        TextRole.Tag => TagHeight,
        TextRole.MemberTitle => MemberTitleHeight,
        TextRole.SectionTitle => SectionTitleHeight,
        TextRole.Axis => AxisTextHeight,
        TextRole.Schedule => ScheduleTextHeight,
        _ => NoteHeight
    };

    public void SetHeight(TextRole r, double v)
    {
        switch (r)
        {
            case TextRole.Dimension: DimTextHeight = v; break;
            case TextRole.Tag: TagHeight = v; break;
            case TextRole.MemberTitle: MemberTitleHeight = v; break;
            case TextRole.SectionTitle: SectionTitleHeight = v; break;
            case TextRole.Axis: AxisTextHeight = v; break;
            case TextRole.Schedule: ScheduleTextHeight = v; break;
            default: NoteHeight = v; break;
        }
    }

    public static string RoleLabel(TextRole r) => r switch
    {
        TextRole.Dimension => "Chữ kích thước (dim)",
        TextRole.Note => "Ghi chú, cao độ, tên cấu kiện trên mặt bằng",
        TextRole.Tag => "Nhãn thép (3Ø22, Ø6a100)",
        TextRole.MemberTitle => "Tên hình cấu kiện / mặt bằng",
        TextRole.SectionTitle => "Tên mặt cắt (1-1)",
        TextRole.Axis => "Tên trục",
        TextRole.Schedule => "Bảng thống kê thép",
        _ => r.ToString()
    };

    // ------------------------------------------------------------------ chuẩn hoá, JSON
    private static double Clamp(double v, double lo, double hi, double def) =>
        double.IsNaN(v) || double.IsInfinity(v) || v <= 0 ? def : Math.Clamp(v, lo, hi);

    /// <summary>Sửa giá trị sai (file sửa tay, nhập ô trống): cỡ chữ 0,5…20 mm, tỷ lệ 1…2000.</summary>
    public DrawingStyle Normalize()
    {
        var d = _builtin;
        foreach (var k in Kinds)
        {
            if (FixedScale(k)) continue;
            double v = ScaleOf(k);
            if (k == DrawingKind.SlabPlan && v <= 0) { SetScale(k, 0); continue; }
            SetScale(k, Clamp(v, 1, 2000, d.ScaleOf(k)));
        }
        DimTextHeight = Clamp(DimTextHeight, 0.5, 20, d.DimTextHeight);
        DimTickSize = Clamp(DimTickSize, 0.2, 20, d.DimTickSize);
        DimExtOffset = double.IsNaN(DimExtOffset) || DimExtOffset < 0 ? d.DimExtOffset : Math.Min(DimExtOffset, 20);
        DimExtBeyond = double.IsNaN(DimExtBeyond) || DimExtBeyond < 0 ? d.DimExtBeyond : Math.Min(DimExtBeyond, 20);
        DimLineExtend = double.IsNaN(DimLineExtend) || DimLineExtend < 0 ? d.DimLineExtend : Math.Min(DimLineExtend, 20);
        DimTextGap = double.IsNaN(DimTextGap) || DimTextGap < 0 ? d.DimTextGap : Math.Min(DimTextGap, 20);
        NoteHeight = Clamp(NoteHeight, 0.5, 20, d.NoteHeight);
        TagHeight = Clamp(TagHeight, 0.5, 20, d.TagHeight);
        MarkDiameter = Clamp(MarkDiameter, 1, 40, d.MarkDiameter);
        MemberTitleHeight = Clamp(MemberTitleHeight, 0.5, 30, d.MemberTitleHeight);
        SectionTitleHeight = Clamp(SectionTitleHeight, 0.5, 30, d.SectionTitleHeight);
        AxisBubbleDiameter = Clamp(AxisBubbleDiameter, 2, 40, d.AxisBubbleDiameter);
        AxisTextHeight = Clamp(AxisTextHeight, 0.5, 20, d.AxisTextHeight);
        ScheduleTextHeight = Clamp(ScheduleTextHeight, 0.5, 20, d.ScheduleTextHeight);
        WidthFactor = Clamp(WidthFactor, 0.3, 2, d.WidthFactor);
        return this;
    }

    /// <summary>Đúng các giá trị mặc định của chương trình.</summary>
    [JsonIgnore]
    public bool IsDefault => ToJson() == _builtin.ToJson();

    public string ToJson() => JsonSerializer.Serialize(this, CadStandard.JsonOptions);

    public static DrawingStyle FromJson(string json) =>
        (JsonSerializer.Deserialize<DrawingStyle>(string.IsNullOrWhiteSpace(json) ? "{}" : json, CadStandard.JsonOptions) ?? new DrawingStyle()).Normalize();

    public DrawingStyle Clone() => FromJson(ToJson());
}
