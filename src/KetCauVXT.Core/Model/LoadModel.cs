namespace KetCauVXT.Model;

public enum LoadType { Dead, SuperDead, Live, RoofLive, Wind, Seismic, Other }

/// <summary>Trường hợp tải (Load pattern). Phân tích tuyến tính từng trường hợp rồi tổ hợp.</summary>
public class LoadPattern
{
    public string Name { get; set; } = "";
    public LoadType Type { get; set; }
    /// <summary>Hệ số trọng lượng bản thân (1 cho TT, 0 cho các trường hợp khác)</summary>
    public double SelfWeight { get; set; }
    /// <summary>Hệ số độ tin cậy γf dùng khi tự sinh tổ hợp TTGH I (TCVN 2737:2023)</summary>
    public double Gamma { get; set; } = 1.0;
    public override string ToString() => Name;
}

public enum LoadDir
{
    /// <summary>Trọng lực, dương hướng xuống (−Z)</summary>
    Gravity,
    GlobalX, GlobalY, GlobalZ,
    Local1, Local2, Local3
}

/// <summary>Tải phân bố trên thanh dạng tuyến tính từng đoạn. Points: (khoảng cách tương đối 0..1, cường độ kN/m).
/// Tải đều: hai điểm (0,q) và (1,q). Tải tam giác/hình thang: nhiều điểm.</summary>
public class FrameLoad
{
    public int Element { get; set; }
    public string Pattern { get; set; } = "";
    public LoadDir Dir { get; set; } = LoadDir.Gravity;
    public List<double[]> Points { get; set; } = new();
    /// <summary>Lực tập trung trên thanh: (khoảng cách tương đối, giá trị kN). Dùng chung Dir.</summary>
    public List<double[]> PointLoads { get; set; } = new();
    /// <summary>Ghi chú nguồn gốc (tường 220, sàn S1...)</summary>
    public string Note { get; set; } = "";
    /// <summary>Nguồn sinh tải: "" = người dùng nhập, "SLAB" = dồn từ sàn (tự sinh lại mỗi lần phân tích)</summary>
    public string Source { get; set; } = "";
    /// <summary>true: mômen phân bố (kN·m/m) / mômen tập trung (kN·m) quanh trục địa phương 1 của thanh (xoắn) – vd. tải ngang tay vịn
    /// lan can đặt cao hơn trục dầm biên. Dir bỏ qua (luôn trục 1).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Moment { get; set; }

    public static FrameLoad Uniform(int elem, string pattern, double q, LoadDir dir = LoadDir.Gravity, string note = "") => new()
    {
        Element = elem, Pattern = pattern, Dir = dir, Note = note,
        Points = new List<double[]> { new[] { 0.0, q }, new[] { 1.0, q } }
    };
}

/// <summary>Tải tập trung tại nút (kN, kN.m) theo hệ toạ độ tổng thể.</summary>
public class NodeLoad
{
    public int Node { get; set; }
    public string Pattern { get; set; } = "";
    public double Fx { get; set; }
    public double Fy { get; set; }
    public double Fz { get; set; }
    public double Mx { get; set; }
    public double My { get; set; }
    public double Mz { get; set; }
    /// <summary>"" = người dùng nhập, "WIND"/"EQ"/"SLAB" = tự sinh</summary>
    public string Source { get; set; } = "";
}

/// <summary>Tải phân bố đều trên ô sàn (kN/m², dương hướng xuống). Được dồn về dầm theo phương pháp đường phân giác 45°.</summary>
public class AreaLoad
{
    public int Slab { get; set; }
    public string Pattern { get; set; } = "";
    public double Q { get; set; }
    public string Note { get; set; } = "";
    /// <summary>"" = nhập tay; "BUILDUP" = từ cấu tạo sàn; "WALL" = tường không có dầm đỡ (tự sinh lại mỗi lần phân tích)</summary>
    public string Source { get; set; } = "";
}

public enum ComboType { ULS, SLS, Envelope }

public class ComboItem
{
    /// <summary>Tên trường hợp tải hoặc tên tổ hợp khác</summary>
    public string Case { get; set; } = "";
    public double Factor { get; set; } = 1.0;
    public ComboItem() { }
    public ComboItem(string c, double f) { Case = c; Factor = f; }
}

public class LoadCombination
{
    public string Name { get; set; } = "";
    public ComboType Type { get; set; } = ComboType.ULS;
    public List<ComboItem> Items { get; set; } = new();
    public override string ToString() => Name;
}

/// <summary>Thông số tải gió theo TCVN 2737:2023 (tính gần đúng cho nhà thấp tầng, tác dụng lên cao trình sàn).</summary>
public class WindSettings
{
    public bool Enabled { get; set; } = true;
    /// <summary>Áp lực gió cơ sở W0 (kN/m²) của vùng áp lực gió – lấy theo phụ lục TCVN 2737:2023 / QCVN 02:2022 cho địa danh.</summary>
    public double W0 { get; set; } = 0.95;
    /// <summary>Vùng áp lực gió I…V theo QCVN 02:2022/BXD ("" = nhập W0 trực tiếp). W0 vẫn là giá trị dùng tính toán.</summary>
    public string Zone { get; set; } = "";
    /// <summary>Dạng địa hình A, B, C</summary>
    public string Terrain { get; set; } = "B";
    /// <summary>Hệ số khí động tổng (đón gió + hút gió), mặc định 0.8 + 0.6 = 1.4</summary>
    public double Cp { get; set; } = 1.4;
    /// <summary>Hệ số độ tin cậy tải gió (TTGH I)</summary>
    public double Gamma { get; set; } = 1.2;
    /// <summary>Hệ số hiệu chỉnh theo thời gian sử dụng / hệ số gió giật (người dùng nhập theo tiêu chuẩn)</summary>
    public double Factor { get; set; } = 1.0;
    /// <summary>Bề rộng đón gió theo phương X (khi gió thổi theo Y), m; 0 = lấy theo kích thước mặt bằng</summary>
    public double WidthForWindY { get; set; }
    public double WidthForWindX { get; set; }
}

/// <summary>Thông số động đất theo TCVN 9386-1:2025 (thay thế TCVN 9386:2012; số điều theo bản 2012) (phương pháp tĩnh lực ngang tương đương).</summary>
public class SeismicSettings
{
    public bool Enabled { get; set; }
    /// <summary>Đỉnh gia tốc nền tham chiếu agR (đơn vị g), tra QCVN 02:2022/BXD Bảng 6.1, 6.2 theo địa danh (trước đây Phụ lục H TCVN 9386:2012 – đã được thay thế bởi TCVN 9386-1:2025)</summary>
    public double AgR { get; set; } = 0.0;
    /// <summary>Loại nền đất A, B, C, D, E</summary>
    public string Soil { get; set; } = "C";
    /// <summary>Hệ số tầm quan trọng γI</summary>
    public double ImportanceFactor { get; set; } = 1.0;
    /// <summary>Hệ số ứng xử q</summary>
    public double Q { get; set; } = 3.9;
    /// <summary>Hệ số tổ hợp hoạt tải khi tính khối lượng ψE = φ.ψ2</summary>
    public double PsiLive { get; set; } = 0.3;
    // ---- [Dao động riêng – Model/ModalSettings.cs] ----
    /// <summary>Phương pháp: SeismicMethods.Static (mặc định, tĩnh lực ngang tương đương) | SeismicMethods.ResponseSpectrum (phổ phản ứng dạng dao động).</summary>
    public string Method { get; set; } = SeismicMethods.Static;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsResponseSpectrum => Method == SeismicMethods.ResponseSpectrum;
    // ---- [hết] ----
}
