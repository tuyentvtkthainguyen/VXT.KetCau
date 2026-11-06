namespace KetCauVXT.Model;

/// <summary>
/// Hệ đơn vị nội bộ của toàn bộ chương trình: kN, m, °C.
/// Ứng suất lưu bằng kN/m² (1 MPa = 1000 kN/m²). Kích thước tiết diện lưu bằng m.
/// Hiển thị trên giao diện / bản vẽ quy đổi sang mm, MPa, kN.m tại lớp trình bày.
/// </summary>
public static class U
{
    public const double MPa = 1000.0;          // kN/m²
    public const double mm = 0.001;            // m
    public const double g = 9.81;
    public static double ToMPa(double kNm2) => kNm2 / 1000.0;
    public static double ToMm(double m) => m * 1000.0;
    public static double Mm2ToCm2(double m2) => m2 * 1e4;
}

public enum MaterialKind { Concrete, Rebar, Steel }

/// <summary>Vật liệu. Giá trị cường độ tính toán theo TCVN 5574:2018 (bê tông, cốt thép) và TCVN 5575 (thép hình).</summary>
public class Material
{
    public string Name { get; set; } = "";
    public MaterialKind Kind { get; set; }
    /// <summary>Trọng lượng riêng, kN/m³</summary>
    public double UnitWeight { get; set; }
    /// <summary>Mô đun đàn hồi, kN/m²</summary>
    public double E { get; set; }
    public double Poisson { get; set; } = 0.2;
    public double Alpha { get; set; } = 1e-5;

    // --- Bê tông (TCVN 5574:2018, Bảng 7, Bảng 10) ---
    /// <summary>Cường độ chịu nén tính toán Rb, kN/m²</summary>
    public double Rb { get; set; }
    /// <summary>Cường độ chịu kéo tính toán Rbt, kN/m²</summary>
    public double Rbt { get; set; }
    /// <summary>Cường độ nén tiêu chuẩn Rb,n (dùng xuất sang ETABS fc'), kN/m²</summary>
    public double Rbn { get; set; }

    // --- Cốt thép (TCVN 5574:2018, Bảng 13) ---
    public double Rs { get; set; }
    public double Rsc { get; set; }
    public double Rsw { get; set; }
    /// <summary>Giới hạn chảy tiêu chuẩn (cốt thép, thép hình), kN/m²</summary>
    public double Fy { get; set; }

    // --- Thép hình (TCVN 5575) ---
    /// <summary>Cường độ tính toán f = fy/γM, kN/m²</summary>
    public double F { get; set; }
    /// <summary>Giới hạn bền fu, kN/m²</summary>
    public double Fu { get; set; }

    public double G => E / (2 * (1 + Poisson));

    public override string ToString() => Name;

    // ------------------------------------------------------------------
    // Thư viện vật liệu mặc định
    // ------------------------------------------------------------------
    public static Material Concrete(string cls)
    {
        // Rb, Rbt (MPa) – TCVN 5574:2018 Bảng 7 ; Eb (MPa) – Bảng 10 ; Rb,n – Bảng 6
        (double rb, double rbt, double eb, double rbn) v = cls switch
        {
            "B15" => (8.5, 0.75, 24000, 11.0),
            "B20" => (11.5, 0.90, 27500, 15.0),
            // B22,5: TCVN 5574:2018 Bảng 7 không liệt kê – nội suy tuyến tính B20…B25 (thường dùng cho cọc khoan nhồi theo TCVN 9395 / 5574:2012)
            "B22.5" or "B22,5" => (13.0, 0.975, 28750, 16.75),
            "B25" => (14.5, 1.05, 30000, 18.5),
            "B30" => (17.0, 1.15, 32500, 22.0),
            "B35" => (19.5, 1.30, 34500, 25.5),
            "B40" => (22.0, 1.40, 36000, 29.0),
            _ => throw new ArgumentException("Cấp bê tông không có trong thư viện: " + cls)
        };
        return new Material
        {
            Name = cls, Kind = MaterialKind.Concrete, UnitWeight = 25.0,
            Rb = v.rb * U.MPa, Rbt = v.rbt * U.MPa, E = v.eb * U.MPa, Rbn = v.rbn * U.MPa,
            Poisson = 0.2, Alpha = 1e-5
        };
    }

    public static Material Rebar(string grade)
    {
        // Rs, Rsc, Rsw, fy (MPa) – TCVN 5574:2018 Bảng 13 ; mác thép theo TCVN 1651:2018
        (double rs, double rsc, double rsw, double fy) v = grade switch
        {
            "CB240-T" => (210, 210, 170, 240),
            "CB300-V" => (260, 260, 210, 300),
            "CB400-V" => (350, 350, 280, 400),
            "CB500-V" => (435, 400, 300, 500),
            _ => throw new ArgumentException("Mác thép không có trong thư viện: " + grade)
        };
        return new Material
        {
            Name = grade, Kind = MaterialKind.Rebar, UnitWeight = 78.5, E = 200000 * U.MPa,
            Rs = v.rs * U.MPa, Rsc = v.rsc * U.MPa, Rsw = v.rsw * U.MPa, Fy = v.fy * U.MPa, Poisson = 0.3
        };
    }

    public static Material Steel(string grade)
    {
        // fy, fu (MPa) ; f = fy/γM với γM = 1.05 (TCVN 5575). Người dùng có thể sửa trực tiếp.
        (double fy, double fu) v = grade switch
        {
            "CCT34" => (220, 340),
            "CCT38" => (240, 380),
            "SS400" => (245, 400),
            "Q345B" => (345, 470),
            "G350" => (350, 420),   // thép mạ kẽm cán nguội làm xà gồ
            _ => throw new ArgumentException("Mác thép hình không có trong thư viện: " + grade)
        };
        return new Material
        {
            Name = grade, Kind = MaterialKind.Steel, UnitWeight = 78.5, E = 210000 * U.MPa, Poisson = 0.3,
            Fy = v.fy * U.MPa, Fu = v.fu * U.MPa, F = Math.Round(v.fy / 1.05) * U.MPa, Alpha = 1.2e-5
        };
    }

    public static readonly string[] ConcreteClasses = { "B15", "B20", "B22.5", "B25", "B30", "B35", "B40" };
    public static readonly string[] RebarGrades = { "CB240-T", "CB300-V", "CB400-V", "CB500-V" };
    public static readonly string[] SteelGrades = { "CCT34", "CCT38", "SS400", "Q345B", "G350" };
}
