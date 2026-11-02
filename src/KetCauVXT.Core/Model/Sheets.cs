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
    View3D,           // ảnh phối cảnh 3D
    RebarShop         // bản vẽ gia công thép – shop drawing (Param = "", "Dầm", "Cột", "Sàn", "Móng") – thêm cuối để giữ số thứ tự JSON cũ
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

    // ---- dàn trang thủ công ----
    /// <summary>Dịch nội dung hình bên trong khung (mm giấy) – cùng kích thước khung tạo thành vùng cắt (crop).</summary>
    public double PanX { get; set; }
    public double PanY { get; set; }
    /// <summary>Ẩn dòng tên hình + tỷ lệ ở mép dưới khung.</summary>
    public bool HideTitle { get; set; }
    /// <summary>Vẽ đường viền quanh khung nhìn (in ra giấy và DXF).</summary>
    public bool Border { get; set; }
    /// <summary>Khoá: không cho kéo/đổi cỡ/xoá nhầm.</summary>
    public bool Locked { get; set; }
}

/// <summary>Loại đối tượng chú thích tự do đặt trên tờ (toạ độ mm giấy).</summary>
public enum SheetItemKind
{
    Text,    // chữ nhiều dòng
    Line,    // đường/đa tuyến, có thể có mũi tên
    Rect,    // hình chữ nhật/khung
    Circle,  // đường tròn
    Cloud,   // đám mây sửa đổi (revision cloud)
    Image,   // ảnh PNG/JPG nhúng base64 (logo, sơ đồ vị trí)
    Table,   // bảng lưới đơn giản (ghi chú, bảng tổng hợp)
    Dim      // kích thước thẳng 2 điểm
}

/// <summary>Đối tượng chú thích tự do trên tờ. Toạ độ mm giấy, gốc trái dưới, y hướng lên.
/// Text: (X,Y) = điểm neo đỉnh dòng đầu. Rect/Cloud/Image/Table: (X,Y) = góc trái dưới, W×H.
/// Circle: (X,Y) = tâm, R. Line/Dim: Pts = x0,y0,x1,y1,… (tuyệt đối).</summary>
public class SheetItem
{
    public SheetItemKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public double R { get; set; }
    public List<double> Pts { get; set; } = new();
    /// <summary>Text: nội dung (xuống dòng bằng \n). Dim: chữ ghi đè (trống = tự đo). Image: tên file.</summary>
    public string Text { get; set; } = "";
    /// <summary>Chiều cao chữ (mm giấy)</summary>
    public double TextHeight { get; set; } = 3.5;
    /// <summary>Góc xoay chữ (độ, ngược chiều kim đồng hồ)</summary>
    public double Rotation { get; set; }
    public bool Bold { get; set; }
    /// <summary>Căn chữ: 0 trái, 1 giữa, 2 phải</summary>
    public int Align { get; set; }
    public bool ArrowStart { get; set; }
    public bool ArrowEnd { get; set; }
    /// <summary>Đa tuyến khép kín</summary>
    public bool Closed { get; set; }
    /// <summary>Lớp (layer) DXF; trống = theo loại đối tượng</summary>
    public string Layer { get; set; } = "";
    /// <summary>Màu "#RRGGBB"; trống = theo lớp</summary>
    public string Color { get; set; } = "";
    /// <summary>Bề dày nét in (mm); 0 = theo lớp</summary>
    public double LineWeight { get; set; }
    /// <summary>Kiểu nét: "" liền, "DASHED" nét đứt, "CENTER" nét trục</summary>
    public string LineType { get; set; } = "";
    public bool Locked { get; set; }
    /// <summary>Ảnh nhúng (base64 PNG/JPG) – đi cùng file dự án</summary>
    public string ImageData { get; set; } = "";
    /// <summary>Bảng: số hàng, số cột, nội dung ô theo hàng (hàng 0 ở trên), tỷ lệ bề rộng cột (trống = đều)</summary>
    public int Rows { get; set; }
    public int Cols { get; set; }
    public List<string> Cells { get; set; } = new();
    public List<double> ColWidths { get; set; } = new();
    /// <summary>Bảng: hàng đầu là tiêu đề (chữ đậm)</summary>
    public bool HeaderRow { get; set; } = true;
    /// <summary>Kích thước: khoảng cách đường kích thước tới điểm đo (mm giấy, dương về bên trái p1→p2)</summary>
    public double Offset { get; set; } = 8;
    /// <summary>Kích thước: 0 = ghi chiều dài mm trên giấy; &gt; 0 = mẫu số tỷ lệ, ghi chiều dài thực = mm giấy × DimScale</summary>
    public double DimScale { get; set; }
    /// <summary>Đám mây: chiều dài cung (mm)</summary>
    public double ArcLength { get; set; } = 8;

    public string Cell(int r, int c)
    {
        int i = r * Math.Max(1, Cols) + c;
        return Cells != null && i >= 0 && i < Cells.Count ? Cells[i] ?? "" : "";
    }
}

/// <summary>Thông tin khung tên mặc định của công trình (dùng cho mọi tờ khi ô trên tờ để trống).</summary>
public class TitleBlockInfo
{
    /// <summary>Tên đơn vị thiết kế (công ty)</summary>
    public string Company { get; set; } = "";
    /// <summary>Tên công trình hiển thị (trống = tên dự án)</summary>
    public string ProjectName { get; set; } = "";
    /// <summary>Địa điểm (trống = địa điểm dự án)</summary>
    public string Location { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Designer { get; set; } = "";
    public string Checker { get; set; } = "";
    public string Approver { get; set; } = "";
    /// <summary>Logo công ty (base64 PNG/JPG) đặt ở ô góc khung tên</summary>
    public string LogoData { get; set; } = "";
    /// <summary>Tiền tố đánh số tờ</summary>
    public string NumberPrefix { get; set; } = "KC-";
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

    // ---- dàn trang thủ công ----
    /// <summary>Đối tượng chú thích tự do (chữ, đường, khung, ảnh, bảng, kích thước…), vẽ trên các khung nhìn.</summary>
    public List<SheetItem> Items { get; set; } = new();
    /// <summary>Khổ giấy tự chọn (Paper = "Custom"), mm – không phụ thuộc hướng giấy.</summary>
    public double PaperW { get; set; }
    public double PaperH { get; set; }
    /// <summary>Lề khung viền (mm); &lt; 0 = mặc định (trái 25, còn lại 10; khổ nhỏ 20/5).</summary>
    public double MarginLeft { get; set; } = -1;
    public double MarginRight { get; set; } = -1;
    public double MarginTop { get; set; } = -1;
    public double MarginBottom { get; set; } = -1;
    /// <summary>Vẽ khung viền trong / khung tên (tờ "Trang trắng": cả hai tắt).</summary>
    public bool ShowFrame { get; set; } = true;
    public bool ShowTitleBlock { get; set; } = true;
    public string Approver { get; set; } = "";
    /// <summary>Lần sửa đổi (A, B, 01…)</summary>
    public string Revision { get; set; } = "";
    /// <summary>Ghi chú tỷ lệ trong khung tên; trống = tự lấy theo khung nhìn</summary>
    public string ScaleNote { get; set; } = "";

    public const string CustomPaper = "Custom";

    public (double W, double H) PaperSize()
    {
        if (Paper == CustomPaper && PaperW >= 10 && PaperH >= 10) return (PaperW, PaperH);
        (double w, double h) = Paper switch { "A0" => (1189.0, 841.0), "A1" => (841.0, 594.0), "A2" => (594.0, 420.0), "A4" => (297.0, 210.0), _ => (420.0, 297.0) };
        return Landscape ? (w, h) : (h, w);
    }
}
