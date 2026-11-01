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
    public Vec3 P => new(X, Y, Z);
    public bool IsSupport => Restraint.Any(r => r);

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
}
