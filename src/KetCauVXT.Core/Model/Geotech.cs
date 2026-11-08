using System.Text.Json.Serialization;

namespace KetCauVXT.Model;

/// <summary>Loại đất (phân loại sơ bộ theo TCVN 5747 / TCVN 9362) – dùng để chọn công thức SPT (đất dính / đất rời) và tô hình trụ hố khoan.</summary>
public enum SoilKind
{
    /// <summary>Đất đắp, đất lấp</summary>
    Fill,
    /// <summary>Bùn, bùn sét, bùn sét pha, đất hữu cơ (rất yếu)</summary>
    Organic,
    /// <summary>Sét</summary>
    Clay,
    /// <summary>Sét pha (á sét)</summary>
    SiltyClay,
    /// <summary>Cát pha (á cát)</summary>
    SandyClay,
    /// <summary>Cát (hạt mịn / nhỏ / vừa / thô – xem SoilLayer.Grain)</summary>
    Sand,
    /// <summary>Sỏi, sạn, cuội</summary>
    Gravel,
    /// <summary>Đá (phong hoá)</summary>
    Rock
}

/// <summary>Cỡ hạt cát.</summary>
public enum SandGrain { Fine, Medium, Coarse, Silty, Gravelly }

/// <summary>Nguồn chỉ tiêu cơ lý của lớp đất.</summary>
public enum SoilPropSource
{
    /// <summary>Theo hố khoan khảo sát và thí nghiệm (nhập trực tiếp) – ktc = 1,0</summary>
    Test = 0,
    /// <summary>Theo bảng tra TCVN 9362:2012 từ loại đất + trạng thái (e, IL) – ktc = 1,1 (Design/Foundation/SoilStandard.cs)</summary>
    Table = 1
}

/// <summary>Một lớp đất trong hố khoan. Độ sâu tính từ miệng hố khoan (mặt đất tự nhiên), m.</summary>
public class SoilLayer
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Độ sâu mặt lớp (m, dương xuống dưới)</summary>
    public double Top { get; set; }
    /// <summary>Độ sâu đáy lớp (m)</summary>
    public double Bottom { get; set; }
    /// <summary>Trọng lượng riêng tự nhiên γ (kN/m³)</summary>
    public double Gamma { get; set; } = 18;
    /// <summary>Trọng lượng riêng bão hoà γsat (kN/m³); 0 = lấy γ + 1</summary>
    public double GammaSat { get; set; }
    /// <summary>Góc ma sát trong φ (độ)</summary>
    public double Phi { get; set; }
    /// <summary>Lực dính c (kPa = kN/m²)</summary>
    public double C { get; set; }
    /// <summary>Mô đun biến dạng E (MPa)</summary>
    public double E { get; set; } = 5;
    /// <summary>Chỉ số SPT trung bình của lớp (búa/30 cm)</summary>
    public double N { get; set; }
    /// <summary>Độ sệt IL; −1 = đất rời (không xác định)</summary>
    public double IL { get; set; } = -1;
    /// <summary>Hệ số rỗng e</summary>
    public double VoidRatio { get; set; }
    public SoilKind Kind { get; set; } = SoilKind.SiltyClay;
    /// <summary>Cỡ hạt (chỉ dùng khi Kind = Sand)</summary>
    public SandGrain Grain { get; set; } = SandGrain.Medium;
    /// <summary>Nguồn chỉ tiêu: thí nghiệm (mặc định cho dữ liệu cũ) hoặc bảng tra TCVN 9362:2012</summary>
    public SoilPropSource Source { get; set; } = SoilPropSource.Test;
    /// <summary>Lớp cấu tạo tiêu chuẩn đã chọn (SoilStandard.Catalog), "" = không</summary>
    public string StdName { get; set; } = "";
    /// <summary>Độ bão hoà Sr (0…1); 0 = mặc định theo loại đất</summary>
    public double Sr { get; set; }
    /// <summary>Áp lực tính toán quy ước R0 theo bảng tra (kPa); 0 = không có</summary>
    public double R0 { get; set; }

    [JsonIgnore]
    public double Thickness => Math.Max(Bottom - Top, 0);
    /// <summary>γsat hiệu lực: GammaSat nếu &gt; 0, ngược lại γ + 1 (kN/m³)</summary>
    [JsonIgnore]
    public double GammaSatOrDefault => GammaSat > 0 ? GammaSat : Gamma + 1;
    /// <summary>Đất dính (sét, sét pha, cát pha, bùn; đất đắp có IL ≥ 0) – dùng công thức cu = 6,25·N; ngược lại là đất rời.</summary>
    [JsonIgnore]
    public bool IsCohesive => Kind is SoilKind.Organic or SoilKind.Clay or SoilKind.SiltyClay or SoilKind.SandyClay
                              || (Kind == SoilKind.Fill && IL >= 0);

    public SoilLayer Copy() => (SoilLayer)MemberwiseClone();

    /// <summary>Tên loại đất tiếng Việt.</summary>
    public static string KindName(SoilKind k, SandGrain g = SandGrain.Medium) => k switch
    {
        SoilKind.Fill => "Đất đắp",
        SoilKind.Organic => "Bùn / đất hữu cơ",
        SoilKind.Clay => "Sét",
        SoilKind.SiltyClay => "Sét pha",
        SoilKind.SandyClay => "Cát pha",
        SoilKind.Sand => g switch { SandGrain.Fine => "Cát hạt nhỏ", SandGrain.Coarse => "Cát hạt thô", SandGrain.Silty => "Cát bụi", SandGrain.Gravelly => "Cát sỏi", _ => "Cát hạt vừa" },
        SoilKind.Gravel => "Sỏi sạn",
        SoilKind.Rock => "Đá phong hoá",
        _ => k.ToString()
    };

    [JsonIgnore]
    public string KindText => KindName(Kind, Grain);
}

/// <summary>Hố khoan khảo sát địa chất.</summary>
public class Borehole
{
    public string Name { get; set; } = "HK1";
    /// <summary>Toạ độ trên mặt bằng công trình (m, cùng hệ với lưới trục)</summary>
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Cao độ miệng hố khoan (m)</summary>
    public double GroundElevation { get; set; }
    /// <summary>Độ sâu mực nước ngầm (m); −1 = không gặp</summary>
    public double WaterDepth { get; set; } = -1;
    public List<SoilLayer> Layers { get; set; } = new();

    /// <summary>Độ sâu đáy hố khoan (đáy lớp sâu nhất), m</summary>
    [JsonIgnore]
    public double Depth => Layers.Count == 0 ? 0 : Layers.Max(l => l.Bottom);

    /// <summary>Mực nước ngầm hiệu lực (m); +∞ nếu không gặp</summary>
    [JsonIgnore]
    public double Water => WaterDepth < 0 ? double.PositiveInfinity : WaterDepth;

    /// <summary>Các lớp sắp theo độ sâu mặt lớp.</summary>
    public List<SoilLayer> Sorted() => Layers.Where(l => l != null).OrderBy(l => l.Top).ThenBy(l => l.Bottom).ToList();

    public Borehole Copy()
    {
        var b = (Borehole)MemberwiseClone();
        b.Layers = Layers.Select(l => l.Copy()).ToList();
        return b;
    }
}
