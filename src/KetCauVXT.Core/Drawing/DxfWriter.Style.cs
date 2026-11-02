using System.Text;
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

    // ------------------------------------------------------------------
    // Kiểu kích thước (DIMSTYLE) – bảng ghi vào DXF
    // ------------------------------------------------------------------
    private readonly HashSet<double> _dimUsed = new();

    /// <summary>Tỷ lệ (mẫu số) của các kiểu kích thước đã dùng trong bộ ghi (kể cả mảnh hình ghép vào).</summary>
    public IReadOnlyCollection<double> DimScalesUsed => _dimUsed;

    private void UseDimStyle(double scale)
    {
        if (scale > 0 && !double.IsNaN(scale)) _dimUsed.Add(Math.Round(scale, 2));
    }

    /// <summary>Thêm kiểu kích thước của tỷ lệ 1/scale vào bảng DIMSTYLE (vd. tỷ lệ thực của khung nhìn trên tờ in).</summary>
    public void RegisterDimStyle(double scale) => UseDimStyle(scale);

    private void MergeDimStyles(DxfWriter other)
    {
        if (other == null || ReferenceEquals(other, this)) return;
        foreach (var v in other._dimUsed) _dimUsed.Add(v);
    }

    /// <summary>Các kiểu kích thước sẽ ghi vào bảng DIMSTYLE: danh sách trong Thiết lập bản vẽ + tỷ lệ phát sinh.</summary>
    public List<DimParams> DimStyleTable() => Style.DimTable(_dimUsed);

    /// <summary>Bảng DIMSTYLE (DXF R12 / AC1009): DIMSCALE = mẫu số tỷ lệ, các thông số mm giấy; gạch xiên DIMTSZ = ½ chiều dài gạch,
    /// mũi tên DIMTSZ = 0 và DIMASZ = chiều dài mũi tên; chữ trên đường kích thước (DIMTAD = 1), chữ song song đường (DIMTIH = DIMTOH = 0).</summary>
    private void WriteDimStyles(StringBuilder sb)
    {
        var list = DimStyleTable();
        if (list.Count == 0) return;
        G(sb, 0, "TABLE"); G(sb, 2, "DIMSTYLE"); G(sb, 70, list.Count);
        foreach (var d in list)
        {
            bool tick = d.Arrow == DimArrow.Tick;
            G(sb, 0, "DIMSTYLE"); G(sb, 2, d.Name); G(sb, 70, 0);
            // không ghi mã 3–7 (DIMPOST, DIMAPOST, DIMBLK…): để mặc định; mã 5 dễ bị đọc nhầm là handle
            G(sb, 40, d.Scale);                                   // DIMSCALE
            G(sb, 41, d.TickSize);                                // DIMASZ
            G(sb, 42, d.ExtOffset);                               // DIMEXO
            G(sb, 43, d.BaselineSpacing);                         // DIMDLI
            G(sb, 44, d.ExtBeyond);                               // DIMEXE
            G(sb, 45, 0.0);                                       // DIMRND
            G(sb, 46, tick ? d.LineExtend : 0.0);                 // DIMDLE
            G(sb, 47, 0.0); G(sb, 48, 0.0);                       // DIMTP, DIMTM
            G(sb, 140, d.TextHeight);                             // DIMTXT
            G(sb, 141, 0.0);                                      // DIMCEN
            G(sb, 142, tick ? d.TickSize / 2 : 0.0);              // DIMTSZ
            G(sb, 143, 25.4);                                     // DIMALTF
            G(sb, 144, d.LinearFactor);                           // DIMLFAC
            G(sb, 145, 0.0); G(sb, 146, 1.0);                     // DIMTVP, DIMTFAC
            G(sb, 147, d.TextGap);                                // DIMGAP
            G(sb, 71, 0); G(sb, 72, 0);                           // DIMTOL, DIMLIM
            G(sb, 73, 0); G(sb, 74, 0);                           // DIMTIH, DIMTOH
            G(sb, 75, 0); G(sb, 76, 0);                           // DIMSE1, DIMSE2
            G(sb, 77, 1); G(sb, 78, 8);                           // DIMTAD, DIMZIN
            G(sb, 170, 0); G(sb, 171, 2); G(sb, 172, 1); G(sb, 173, 0); G(sb, 174, 0); G(sb, 175, 0);
            G(sb, 176, 0); G(sb, 177, 0); G(sb, 178, 0);
        }
        G(sb, 0, "ENDTAB");
    }

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
