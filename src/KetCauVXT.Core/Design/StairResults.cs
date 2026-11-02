using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Design;

// =====================================================================
// KẾT QUẢ THIẾT KẾ CẦU THANG (Design/Concrete/StairDesigner.cs). DesignSummary.Stairs.
// Mỗi bộ phận (bản thang, chiếu nghỉ, dầm cốn, dầm chiếu nghỉ) là một cấu kiện riêng – số hiệu thép đánh từ 1.
// =====================================================================

/// <summary>Một bộ phận cầu thang đã thiết kế.</summary>
public class StairPart : MemberDesignBase
{
    /// <summary>"Bản thang", "Chiếu nghỉ", "Dầm cốn", "Dầm chiếu nghỉ"</summary>
    public string Kind { get; set; } = "";
    /// <summary>Số thứ tự vế (bản thang, dầm cốn); 0 = không theo vế</summary>
    public int Flight { get; set; }
    /// <summary>Các vế cùng dùng bộ phận này (gộp vế giống nhau)</summary>
    public List<int> Flights { get; set; } = new();
    /// <summary>Nhịp tính toán (m, hình chiếu bằng với bản thang chịu lực / dầm cốn)</summary>
    public double Span { get; set; }
    /// <summary>Chiều dài cấu kiện thật (m): chiều dài nghiêng với bản thang / dầm cốn</summary>
    public double Length { get; set; }
    /// <summary>Bề rộng tính toán b (dải 1 m với bản) và chiều cao h, h0 (m)</summary>
    public double B { get; set; }
    public double H { get; set; }
    public double H0 { get; set; }
    /// <summary>Bề rộng thật của bản (m) – để rải thép</summary>
    public double Width { get; set; }
    /// <summary>Tải tính toán: kN/m² (bản) hoặc kN/m (dầm)</summary>
    public double Q { get; set; }
    public double M { get; set; }
    public double MSupport { get; set; }
    public double V { get; set; }
    public string MomentFormula { get; set; } = "";
    public double AsReq { get; set; }
    public double AsProv { get; set; }
    public string MainText { get; set; } = "";
    public double AsTopReq { get; set; }
    public double AsTopProv { get; set; }
    public string TopText { get; set; } = "";
    public string DistText { get; set; } = "";
    public string StirrupText { get; set; } = "";
    public double CosA { get; set; } = 1;
    /// <summary>Thép chịu lực chạy theo phương vế (bản chịu lực) hay ngang vế (bản tựa dầm cốn)</summary>
    public bool MainAlongFlight { get; set; } = true;
}

/// <summary>Kết quả thiết kế một cầu thang.</summary>
public class StairDesign : MemberDesignBase
{
    public int StairId { get; set; }
    public int OpeningId { get; set; }
    /// <summary>Bản sao khai báo cầu thang (vẽ lại hình học: Stair.Layout)</summary>
    public Stair Stair { get; set; }
    /// <summary>true = cầu thang mặc định suy ra từ lỗ sàn (chưa khai báo)</summary>
    public bool IsDefault { get; set; }
    public string TypeText { get; set; } = "";
    public string SupportText { get; set; } = "";
    public double Height { get; set; }
    public int Risers { get; set; }
    public double Riser { get; set; }
    public double Tread { get; set; }
    public double Check2hb => 2 * Riser + Tread;
    public double FlightWidth { get; set; }
    public double WaistT { get; set; }
    public double LandingT { get; set; }
    public double CosA { get; set; } = 1;
    public double ZBottom { get; set; }
    public double ZTop { get; set; }
    public StairLoadSet Loads { get; set; }
    public List<StairPart> Parts { get; set; } = new();
}
