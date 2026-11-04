namespace KetCauVXT.Model;

public readonly struct Vec3
{
    public readonly double X, Y, Z;
    public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    public static Vec3 operator *(double k, Vec3 a) => a * k;
    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
    public Vec3 Cross(Vec3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public Vec3 Normalize() { double l = Length; return l < 1e-12 ? this : new Vec3(X / l, Y / l, Z / l); }
    public static readonly Vec3 UnitX = new(1, 0, 0), UnitY = new(0, 1, 0), UnitZ = new(0, 0, 1);
    public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
}

/// <summary>Trục định vị. Trục "theo X" là đường song song Oy tại hoành độ X = Coord (tên 1,2,3...).
/// Trục "theo Y" là đường song song Ox tại tung độ Y = Coord (tên A,B,C...).</summary>
public class GridLine
{
    public string Name { get; set; } = "";
    public GridDir Dir { get; set; }
    /// <summary>Toạ độ, m</summary>
    public double Coord { get; set; }
    public override string ToString() => $"{Name} ({Dir}={Coord:0.###})";
}

public enum GridDir { X, Y }

/// <summary>Tầng. Elevation là cao độ sàn tầng (đỉnh cột tầng dưới), m.</summary>
public class Story
{
    public string Name { get; set; } = "";
    public double Elevation { get; set; }
    public double Height { get; set; }
    public override string ToString() => $"{Name} (+{Elevation:0.000})";
}

/// <summary>Nút. Restraint theo thứ tự UX, UY, UZ, RX, RY, RZ.</summary>
public class Node
{
    public int Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public bool[] Restraint { get; set; } = new bool[6];
    public string Story { get; set; } = "";
    /// <summary>Tên điểm trong phần mềm khác (ETABS: nhãn điểm "12" + <see cref="Story"/>; SAP2000: tên Joint) khi mô hình
    /// được nhập từ .e2k/.s2k – dùng khớp bảng chuyển vị/phản lực nhập về. Rỗng = mô hình tạo trong chương trình.</summary>
    public string ExternalName { get; set; } = "";
    public Vec3 P => new(X, Y, Z);
    /// <summary>Nút có ít nhất một bậc tự do bị giữ (kể cả giữ ngoài mặt phẳng của khung phẳng).</summary>
    public bool HasRestraint => Restraint.Any(r => r);
    /// <summary>Gối thật (chân cột/móng): bị giữ chuyển vị đứng UZ.</summary>
    public bool IsSupport => Restraint[2];

    public static bool[] Fixed => new[] { true, true, true, true, true, true };
    public static bool[] Pinned => new[] { true, true, true, false, false, false };
}

public enum FrameKind
{
    Column, Beam,
    /// <summary>Cột thép (khung tiền chế)</summary>
    SteelColumn,
    /// <summary>Kèo thép (khung tiền chế)</summary>
    Rafter,
    /// <summary>Xà gồ</summary>
    Purlin,
    Brace
}

/// <summary>Phần tử thanh (cột, dầm, kèo...).</summary>
public class FrameElement
{
    public int Id { get; set; }
    /// <summary>Tên hiển thị / tên cấu kiện thiết kế (C1, D2-1, K1...)</summary>
    public string Label { get; set; } = "";
    public int NodeI { get; set; }
    public int NodeJ { get; set; }
    public string Section { get; set; } = "";
    public FrameKind Kind { get; set; }
    public string Story { get; set; } = "";
    /// <summary>Góc xoay tiết diện quanh trục 1 (độ), giống "Local axis angle" của ETABS.</summary>
    public double Angle { get; set; }
    /// <summary>Giải phóng liên kết đầu I/J: [P, V2, V3, T, M2, M3]</summary>
    public bool[] ReleaseI { get; set; } = new bool[6];
    public bool[] ReleaseJ { get; set; } = new bool[6];
    /// <summary>Nhóm thiết kế (các phần tử cùng nhóm được bố trí thép giống nhau, gộp SL trong bản vẽ).</summary>
    public string DesignGroup { get; set; } = "";
    /// <summary>Chiều dài tính toán ngoài mặt phẳng (thép) – khoảng cách giằng/xà gồ, m. 0 = lấy chiều dài thanh.</summary>
    public double UnbracedLength22 { get; set; }
    /// <summary>Dầm cong trên mặt bằng: độ vồng (mũi tên cung) f tại giữa dây cung I–J, m. Dương = cung vồng về bên trái
    /// hướng I→J nhìn từ trên xuống (+Z), âm = bên phải; 0 = thanh thẳng (mặc định, tương thích file cũ).
    /// Chỉ áp dụng cho thanh nằm ngang (hai nút cùng cao độ) – xem <see cref="CurvedBeam"/>.</summary>
    public double CurveMidOffset { get; set; }
    /// <summary>Số đoạn thẳng chia dầm cong khi phân tích/xuất ETABS; 0 = tự động (mỗi đoạn ≤ 10°).</summary>
    public int CurveSegments { get; set; }
    /// <summary>Tên đối tượng trong phần mềm khác khi mô hình nhập từ .e2k/.s2k (ETABS: nhãn "B12"/"C3"; SAP2000: tên Frame) –
    /// dùng khớp bảng nội lực nhập về (Import/External/ResultImporter.cs). Rỗng = thanh tạo trong chương trình.</summary>
    public string ExternalName { get; set; } = "";
    /// <summary>Tầng của đối tượng trong ETABS (nhãn ETABS lặp lại ở mỗi tầng: khoá khớp = tầng + nhãn). Rỗng với SAP2000.</summary>
    public string ExternalStory { get; set; } = "";
    // ---- [BeamTools: điểm chèn + dời ngang trên mặt bằng – Model/BeamTools.cs] ----
    /// <summary>Điểm chèn tiết diện (như "Cardinal point" của ETABS): điểm nào của tiết diện nằm trên đường nối nút I–J.
    /// Mặc định trọng tâm. Mép trái/phải tính theo hướng I→J nhìn từ trên xuống. Chỉ dùng cho bản vẽ/mặt bằng và xuất ETABS
    /// (CARDINALPT); phân tích của chương trình bỏ qua (đường trục thanh vẫn qua hai nút).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public BeamInsertion Insertion { get; set; }
    /// <summary>Dời ngang tiết diện trên mặt bằng, m, vuông góc trục thanh (dương = sang trái hướng I→J). Cộng thêm với dời do
    /// <see cref="Insertion"/> (mép trái/phải = ∓b/2). Chỉ dùng cho bản vẽ; phân tích bỏ qua.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double PlanOffset { get; set; }
    // ---- [hết BeamTools] ----

    /// <summary>Thanh có khai báo độ cong (|f| &gt; 0,1 mm). Hình học cung: <see cref="CurvedBeam.Arc(Project, FrameElement)"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsCurved => Math.Abs(CurveMidOffset) > 1e-4;

    public bool IsVerticalKind => Kind is FrameKind.Column or FrameKind.SteelColumn;
    public bool IsConcrete => Kind is FrameKind.Column or FrameKind.Beam;

    public void PinBothEnds()
    {
        ReleaseI[4] = ReleaseI[5] = true;
        ReleaseJ[4] = ReleaseJ[5] = true;
    }
}

/// <summary>Ô sàn (đa giác các nút cùng cao độ, thường là ô chữ nhật 4 nút).</summary>
public class Slab
{
    public int Id { get; set; }
    public string Label { get; set; } = "";
    public List<int> Nodes { get; set; } = new();
    public string Section { get; set; } = "S100";
    public string Story { get; set; } = "";
    /// <summary>Điều kiện biên mỗi cạnh (cạnh i nối Nodes[i]→Nodes[i+1]): true = ngàm (liên tục), false = khớp (biên).
    /// Rỗng = tự xác định (cạnh có ô sàn kề bên là ngàm).</summary>
    public List<bool> EdgeFixed { get; set; } = new();
    /// <summary>Cấu tạo sàn (tên trong Project.FloorBuildUps); rỗng = tải nhập tay</summary>
    public string BuildUp { get; set; } = "";
}

/// <summary>Móng (gán tại nút chân cột).</summary>
public class Footing
{
    public string Label { get; set; } = "M1";
    public int NodeId { get; set; }
    /// <summary>Kích thước đáy móng đã chọn (0 = để chương trình tự chọn), m</summary>
    public double L { get; set; }
    public double B { get; set; }
    public double Height { get; set; }
    /// <summary>Loại móng riêng cho chân cột này; null = theo Project.Foundation.Type</summary>
    public FoundationType? Type { get; set; }
}
