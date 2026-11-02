namespace KetCauVXT.Model;

/// <summary>Sơ đồ bố trí cọc trong đài (người dùng chọn hoặc chương trình tự chọn).</summary>
public enum PilePattern
{
    /// <summary>Chương trình tự chọn số cọc tối thiểu thoả [P]</summary>
    Auto = 0,
    /// <summary>1 cọc dưới tim cột</summary>
    P1 = 1,
    /// <summary>2 cọc thẳng hàng</summary>
    P2 = 2,
    /// <summary>3 cọc tam giác đều</summary>
    P3 = 3,
    /// <summary>4 cọc hình vuông 2×2</summary>
    P4 = 4,
    /// <summary>5 cọc hoa mai (4 góc + 1 giữa)</summary>
    P5 = 5,
    /// <summary>6 cọc chữ nhật 2×3</summary>
    P6 = 6,
    /// <summary>7 cọc lục giác (6 cọc quanh + 1 giữa)</summary>
    P7 = 7,
    /// <summary>8 cọc lưới 3×3 bỏ cọc giữa</summary>
    P8 = 8,
    /// <summary>9 cọc lưới 3×3</summary>
    P9 = 9,
    /// <summary>Lưới n×m tuỳ chọn (Nx cọc theo X, Ny cọc theo Y, bước SpacingX/SpacingY)</summary>
    Grid = 100,
    /// <summary>Toạ độ từng cọc do người dùng nhập (CustomPiles)</summary>
    Custom = 101
}

/// <summary>Toạ độ tim cọc so với tim cột (m) – lớp có thuộc tính để JSON lưu được (tuple không lưu được).</summary>
public class PilePoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public PilePoint() { }
    public PilePoint(double x, double y) { X = x; Y = y; }
}

/// <summary>Bố trí cọc / kích thước đài do người dùng chọn cho một chân cột (FoundationSettings.CapOverrides).
/// Mọi giá trị 0 = chương trình tự xác định. Đài luôn đặt tâm tại tim cột.</summary>
public class PileCapOverride
{
    /// <summary>Nút chân cột (khoá); đài gộp giống hệt có thể có nhiều nút – mỗi nút một bản ghi</summary>
    public int NodeId { get; set; }
    public PilePattern Pattern { get; set; } = PilePattern.Auto;
    /// <summary>Lưới n×m (Pattern = Grid): số cọc theo X và theo Y</summary>
    public int Nx { get; set; } = 2;
    public int Ny { get; set; } = 2;
    /// <summary>Khoảng cách tim cọc theo X / Y (m); 0 = PileSpacingFactor·d (≥ 3d). Sơ đồ chuẩn dùng SpacingX cho cả 2 phương.</summary>
    public double SpacingX { get; set; }
    public double SpacingY { get; set; }
    /// <summary>true = xoay sơ đồ 90° (cạnh dài theo Y); null = tự chọn theo phương mô men lớn</summary>
    public bool? Rotate { get; set; }
    /// <summary>Kích thước đài theo X, Y và chiều cao (m); 0 = tự chọn (L, B theo mép cọc; H theo chọc thủng / cắt)</summary>
    public double L { get; set; }
    public double B { get; set; }
    public double H { get; set; }
    /// <summary>Toạ độ cọc tuỳ chọn (Pattern = Custom), so với tim cột</summary>
    public List<PilePoint> CustomPiles { get; set; } = new();
    /// <summary>Ghi chú của người dùng</summary>
    public string Note { get; set; } = "";

    public PileCapOverride Clone() => new()
    {
        NodeId = NodeId, Pattern = Pattern, Nx = Nx, Ny = Ny, SpacingX = SpacingX, SpacingY = SpacingY, Rotate = Rotate,
        L = L, B = B, H = H, Note = Note, CustomPiles = (CustomPiles ?? new List<PilePoint>()).Select(q => new PilePoint(q.X, q.Y)).ToList()
    };
}
