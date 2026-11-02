using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Thiết lập bản vẽ (Model/DrawingStyle.cs) cho bộ ghi DXF: cỡ chữ theo vai trò (mm giấy × Scale),
/// kích thước gạch xiên/đường gióng, vòng số hiệu, đầu trục, hệ số rộng chữ.
/// Mặc định = đúng giá trị cũ (chữ 2,5 mm, tên hình 3,5 mm, vòng số Ø5,5, đầu trục Ø7,5…) nên DXF ghi ra như trước.</summary>
public sealed partial class DxfWriter
{
    private DrawingStyle _style;

    /// <summary>Thiết lập riêng cho bộ ghi này; null = DrawingStyle.Current (tạm → CadStandard.Active.Drawing → mặc định).</summary>
    public DrawingStyle Style
    {
        get => _style ?? DrawingStyle.Current;
        set => _style = value;
    }

    /// <summary>Chiều cao chữ trong mô hình của vai trò r (= mm giấy × Scale).</summary>
    public double TextH(TextRole r) => Style.Height(r) * Scale;

    /// <summary>Chữ số kích thước (mô hình).</summary>
    public double DimTh => TextH(TextRole.Dimension);
    /// <summary>Chữ nhãn thép (mô hình).</summary>
    public double TagTh => TextH(TextRole.Tag);
    /// <summary>Tên hình cấu kiện (mô hình).</summary>
    public double TitleTh => TextH(TextRole.MemberTitle);
    /// <summary>Tên mặt cắt (mô hình).</summary>
    public double SectionTitleTh => TextH(TextRole.SectionTitle);
    /// <summary>Chữ bảng thống kê (mô hình).</summary>
    public double ScheduleTh => TextH(TextRole.Schedule);
    /// <summary>Bán kính vòng tròn đầu trục (mô hình).</summary>
    public double BubbleR => Style.AxisBubbleDiameter / 2 * Scale;
    /// <summary>Hệ số rộng chữ đang dùng.</summary>
    public double WidthFactor => Style.WidthFactor;

    /// <summary>Đếm chữ (TEXT) theo lớp – kiểm thử.</summary>
    public List<double> TextHeights(string layer = null)
    {
        var res = new List<double>();
        foreach (var e in _ents)
        {
            if (e.Type != "TEXT") continue;
            if (layer != null && !e.Codes.Any(c => c.Code == 8 && (c.Val as string) == layer)) continue;
            foreach (var (code, val) in e.Codes) if (code == 40 && val is double d) { res.Add(d); break; }
        }
        return res;
    }

    /// <summary>Nội dung các chữ (đã giải mã) – kiểm thử, tìm ghi chú trùng.</summary>
    public List<string> Texts()
    {
        var res = new List<string>();
        foreach (var e in _ents)
            if (e.Type == "TEXT")
                foreach (var (code, val) in e.Codes) if (code == 1 && val is string s) { res.Add(Decode(s)); break; }
        return res;
    }
}
