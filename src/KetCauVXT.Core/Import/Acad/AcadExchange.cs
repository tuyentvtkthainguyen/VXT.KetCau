using System.Text.Json;
using System.Text.Json.Serialization;

namespace KetCauVXT.Import.Acad;

// =====================================================================
// FILE TRAO ĐỔI AutoCAD → Việt Const (acad_*.json), do add-in KetCauVXT.AcadLink ghi (lệnh VXT_GUI).
// Mô tả đầy đủ: docs/acad-link.md
//   { "Version":1, "Source":"AutoCAD 2025", "Dwg":"D:\\...\\NhaPho.dwg", "Units":"m",
//     "Storeys":[ { "Name":"Tầng 1", "Elevation":0, "Height":3.6, "BaseX":..., "BaseY":...,
//                   "Items":[ { "Category":"Tuong", "Layer":"KT-TUONG", "Kind":"Line", "Points":[[x,y],[x,y]], ... } ] } ] }
// Toạ độ (Points, BaseX/BaseY, X0..Y1, Radius, Height chữ) theo đơn vị "Units" (add-in luôn ghi "m");
// Elevation, Height tầng luôn là m.
// =====================================================================

/// <summary>Các nhóm đối tượng người dùng gán trong AutoCAD (chuỗi cố định, dùng chung với add-in và CadStandard).</summary>
public static class AcadCategory
{
    public const string Tuong = "Tuong";
    public const string Cot = "Cot";
    public const string Dam = "Dam";
    public const string San = "San";
    public const string Truc = "Truc";
    public const string Cua = "Cua";
    public const string Thang = "Thang";
    public const string TenPhong = "TenPhong";
    public const string BoQua = "BoQua";

    public static readonly string[] All = { Tuong, Cot, Dam, San, Truc, Cua, Thang, TenPhong, BoQua };

    /// <summary>Lớp tổng hợp dùng khi chuyển dữ liệu AutoCAD sang bộ nhận diện bản vẽ kiến trúc (ArchPlanReader).</summary>
    public static string SyntheticLayer(string category) => category switch
    {
        Tuong => "VXT_TUONG",
        Cot => "VXT_COT",
        Dam => "VXT_DAM",
        San => "VXT_SAN",
        Truc => "VXT_TRUC",
        Cua => "VXT_CUA",
        Thang => "VXT_THANG",
        TenPhong => "VXT_TENPHONG",
        _ => "VXT_KHAC",
    };

    /// <summary>Tên tiếng Việt để hiển thị.</summary>
    public static string Display(string category) => category switch
    {
        Tuong => "Tường",
        Cot => "Cột",
        Dam => "Dầm",
        San => "Sàn",
        Truc => "Lưới trục",
        Cua => "Cửa",
        Thang => "Cầu thang",
        TenPhong => "Tên phòng",
        BoQua => "Bỏ qua",
        _ => category ?? "",
    };

    /// <summary>Chuẩn hoá chuỗi nhóm (không phân biệt hoa thường) về đúng tên cố định; null nếu không nhận ra.</summary>
    public static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        foreach (var c in All) if (string.Equals(c, s.Trim(), StringComparison.OrdinalIgnoreCase)) return c;
        return null;
    }
}

/// <summary>Gốc file trao đổi.</summary>
public class AcadExchange
{
    public int Version { get; set; } = 1;
    /// <summary>Phần mềm nguồn, vd "AutoCAD 2025"</summary>
    public string Source { get; set; } = "";
    /// <summary>Đường dẫn bản vẽ DWG</summary>
    public string Dwg { get; set; } = "";
    /// <summary>Đơn vị toạ độ: "m" (mặc định), "cm", "mm"</summary>
    public string Units { get; set; } = "m";
    /// <summary>Thời điểm xuất (ISO 8601) – chỉ để tham khảo</summary>
    public string Created { get; set; } = "";
    public List<AcadStorey> Storeys { get; set; } = new();

    private static readonly JsonSerializerOptions ReadOpt = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static readonly JsonSerializerOptions WriteOpt = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static AcadExchange Parse(string json)
    {
        var x = JsonSerializer.Deserialize<AcadExchange>(json, ReadOpt) ?? throw new InvalidDataException("File dữ liệu AutoCAD rỗng.");
        x.Storeys ??= new List<AcadStorey>();
        foreach (var s in x.Storeys)
        {
            s.Items ??= new List<AcadItem>();
            foreach (var it in s.Items) it.Points ??= new List<double[]>();
        }
        return x;
    }

    public static AcadExchange Load(string path) => Parse(File.ReadAllText(path));

    public string ToJson() => JsonSerializer.Serialize(this, WriteOpt);

    public void Save(string path) => File.WriteAllText(path, ToJson(), new System.Text.UTF8Encoding(false));
}

/// <summary>Một tầng: tên, cao độ sàn, chiều cao tầng, điểm gốc (giao trục chọn trong AutoCAD) và các đối tượng thuộc tầng.</summary>
public class AcadStorey
{
    public string Name { get; set; } = "";
    /// <summary>Cao độ mặt sàn, m</summary>
    public double Elevation { get; set; }
    /// <summary>Chiều cao tầng (đến mặt sàn tầng trên), m; 0 = lấy theo cao độ tầng trên hoặc mặc định</summary>
    public double Height { get; set; }
    /// <summary>Điểm gốc của tầng trên bản vẽ (cùng đơn vị Points) – các tầng vẽ cạnh nhau được dời về chung gốc này</summary>
    public double BaseX { get; set; }
    public double BaseY { get; set; }
    /// <summary>Phạm vi tầng trên bản vẽ (tham khảo)</summary>
    public double X0 { get; set; }
    public double Y0 { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public List<AcadItem> Items { get; set; } = new();
}

/// <summary>Một đối tượng hình học. Kind: Line | Polyline | Circle | Arc | Text | Block.</summary>
public class AcadItem
{
    /// <summary>Tuong, Cot, Dam, San, Truc, Cua, Thang, TenPhong, BoQua</summary>
    public string Category { get; set; } = "";
    /// <summary>Lớp gốc trong AutoCAD</summary>
    public string Layer { get; set; } = "";
    public string Kind { get; set; } = "Line";
    /// <summary>Line: 2 điểm; Polyline: các đỉnh; Circle/Arc: tâm; Text/Block: điểm chèn</summary>
    public List<double[]> Points { get; set; } = new();
    public bool Closed { get; set; }
    /// <summary>Polyline: độ phình (bulge) từng đỉnh = tan(góc ở tâm/4), dương = ngược chiều kim đồng hồ; null = toàn đoạn thẳng</summary>
    public List<double> Bulges { get; set; }
    public string Text { get; set; } = "";
    /// <summary>Chiều cao chữ</summary>
    public double Height { get; set; }
    /// <summary>Góc xoay chữ / block, độ</summary>
    public double Rotation { get; set; }
    /// <summary>Tên block chứa đối tượng (block đã phân rã) hoặc tên block (Kind = Block)</summary>
    public string Block { get; set; } = "";
    /// <summary>Circle/Arc: bán kính</summary>
    public double Radius { get; set; }
    /// <summary>Arc: góc đầu, góc cuối (độ, ngược chiều kim đồng hồ)</summary>
    public double StartAngle { get; set; }
    public double EndAngle { get; set; }

    public override string ToString() => $"{Category} {Kind} [{Layer}] {Points?.Count ?? 0} điểm";
}
