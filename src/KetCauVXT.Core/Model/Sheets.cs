namespace KetCauVXT.Model;

/// <summary>Loại khung nhìn đặt lên tờ in.</summary>
public enum SheetViewKind
{
    PlanStory,        // mặt bằng kết cấu tầng (Param = tên tầng)
    SlabRebarPlan,    // mặt bằng thép sàn tầng
    Elevation,        // mặt đứng khung theo trục (Param = tên trục)
    ForceDiagram,     // biểu đồ nội lực (Param = "tầng|trục;đại lượng;tổ hợp")
    FoundationPlan,   // mặt bằng móng
    FoundationDetail, // chi tiết móng (Param = nhãn móng)
    BeamDetail,       // chi tiết dầm (Param = tên dải dầm)
    ColumnDetail,     // chi tiết cột (Param = tên cột)
    RebarSchedule,    // bảng thống kê thép
    Notes,            // ghi chú chung (Param = văn bản)
    View3D            // ảnh phối cảnh 3D
}

/// <summary>Khung nhìn trên tờ in: vị trí/kích thước theo mm giấy, tỷ lệ 1/Scale.</summary>
public class SheetView
{
    public SheetViewKind Kind { get; set; }
    public string Param { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>Góc trái dưới khung trên giấy (mm)</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; } = 200;
    public double H { get; set; } = 150;
    /// <summary>Mẫu số tỷ lệ (50 → 1/50); 0 = tự co vừa khung</summary>
    public double Scale { get; set; }
}

/// <summary>Tờ bản vẽ: khổ giấy, khung tên, danh sách khung nhìn.</summary>
public class Sheet
{
    public string Number { get; set; } = "KC-01";
    public string Name { get; set; } = "";
    /// <summary>A0, A1, A2, A3, A4</summary>
    public string Paper { get; set; } = "A3";
    public bool Landscape { get; set; } = true;
    public string Designer { get; set; } = "";
    public string Checker { get; set; } = "";
    public string Date { get; set; } = "";
    public List<SheetView> Views { get; set; } = new();

    public (double W, double H) PaperSize()
    {
        (double w, double h) = Paper switch { "A0" => (1189.0, 841.0), "A1" => (841.0, 594.0), "A2" => (594.0, 420.0), "A4" => (297.0, 210.0), _ => (420.0, 297.0) };
        return Landscape ? (w, h) : (h, w);
    }
}
