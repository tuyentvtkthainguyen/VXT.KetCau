using System.Globalization;
using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// CHẾ ĐỘ "TRIỂN KHAI HỒ SƠ" – người dùng nhập trực tiếp kích thước, thép, nội lực (không chạy mô hình).
// Đơn vị: chiều dài m, lực kN, mô men kN.m, ứng suất kN/m², đường kính thép mm.
// Mỗi bộ thiết kế trả về một DetailResult: kiểm tra (Checks), thông báo (Messages, "LỖI …" = không đạt),
// thép (Bars – quy tắc đánh số Kết cấu Soft), khối lượng 1 cấu kiện, các dòng thuyết minh (Calc).
// =====================================================================

/// <summary>Vật liệu, lớp bảo vệ dùng chung của hồ sơ triển khai.</summary>
public class DetailSettings
{
    public string Concrete { get; set; } = "B25";
    /// <summary>Thép dọc (Ø ≥ 10)</summary>
    public string RebarMain { get; set; } = "CB400-V";
    /// <summary>Thép đai, thép sàn Ø ≤ 10</summary>
    public string RebarSmall { get; set; } = "CB240-T";
    /// <summary>Lớp bê tông bảo vệ tới mép cốt dọc (m)</summary>
    public double CoverBeam { get; set; } = 0.025;
    public double CoverColumn { get; set; } = 0.025;
    public double CoverSlab { get; set; } = 0.015;
    public double CoverFooting { get; set; } = 0.050;
    /// <summary>Lớp bảo vệ kết cấu chứa nước (bể) – mặt tiếp xúc nước / đất</summary>
    public double CoverTank { get; set; } = 0.030;
    /// <summary>Chiều dày bê tông lót móng (m)</summary>
    public double LeanThickness { get; set; } = 0.10;
    public string LeanConcrete { get; set; } = "B7,5";
    /// <summary>Chiều dài cây thép thương phẩm (m)</summary>
    public double StockLength { get; set; } = 11.7;
    /// <summary>Cỡ chữ in của bản vẽ triển khai (0 = theo Thiết lập bản vẽ) – Detailing/SheetViews.cs.</summary>
    public DetailTextSizes Text { get; set; } = new();
    /// <summary>Xuất shop drawing (gia công, cắt thép từ cây) cho MỌI dầm, sàn (ngoài lựa chọn riêng từng cấu kiện).</summary>
    public bool ShopDrawingAll { get; set; }

    public Material Conc() => Material.Concrete(Concrete);
    public Material Main() => Material.Rebar(RebarMain);
    public Material Small() => Material.Rebar(RebarSmall);
    /// <summary>Thép theo đường kính: Ø ≤ 10 → RebarSmall, còn lại RebarMain.</summary>
    public Material SteelFor(int dia) => dia <= 10 ? Small() : Main();
}

/// <summary>Kết quả thiết kế / kiểm tra một cấu kiện ở chế độ triển khai hồ sơ.</summary>
public class DetailResult : MemberDesignBase
{
    /// <summary>Loại: "Dầm", "Cột", "Sàn", "Móng đơn", "Móng băng", "Móng cọc", "Móng bè", "Dự ứng lực", "Bể nước"</summary>
    public string Kind { get; set; } = "";
    public string ConcreteClass { get; set; } = "";
    /// <summary>Bê tông 1 cấu kiện (m³)</summary>
    public double ConcreteVolume { get; set; }
    /// <summary>Ván khuôn 1 cấu kiện (m²)</summary>
    public double FormworkArea { get; set; }
    /// <summary>Bê tông lót 1 cấu kiện (m³)</summary>
    public double LeanVolume { get; set; }
    /// <summary>Thép DƯL / cáp (kg, 1 cấu kiện) – không nằm trong Bars</summary>
    public double StrandWeight { get; set; }
    /// <summary>Các dòng thuyết minh tính toán (công thức, thay số) – mỗi dòng một đoạn; dòng bắt đầu "## " là tiêu đề nhỏ.</summary>
    public List<string> Calc { get; set; } = new();
    /// <summary>Khối lượng khác: (tên công tác, đơn vị, khối lượng 1 cấu kiện) – vd. ("Chống thấm thành bể", "m²", 42.0)</summary>
    public List<(string Item, string Unit, double Qty)> Extra { get; set; } = new();

    /// <summary>Đối tượng thiết kế gốc dùng cho bản vẽ có sẵn (BeamLineDesign, ColumnDesign, FootingDesign…); null nếu không có.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public MemberDesignBase Source { get; set; }

    public double SteelWeight => Bars.Sum(b => b.WeightPerBar * b.Count);
    public string Status => Ok ? "Đạt" : "Không đạt";
}

/// <summary>Tiện ích định dạng / làm tròn dùng chung.</summary>
public static class Dt
{
    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    /// <summary>Số kiểu Việt Nam: dấu phẩy thập phân.</summary>
    public static string F(double v, string fmt = "0.##") => v.ToString(fmt, Inv).Replace('.', ',');
    public static string Mm(double m) => Math.Round(m * 1000).ToString("0", Inv);
    public static double R10(double m) => Math.Round(m * 100.0, MidpointRounding.AwayFromZero) / 100.0;
    public static double Area(int d) => Math.PI * d * d / 4.0 * 1e-6;
    /// <summary>Diện tích thép rải Ø d a s trên 1 m (m²/m).</summary>
    public static double AreaPerM(int d, double s) => s > 1e-9 ? Area(d) / s : 0;
    public static string Spaced(int d, double s) => $"Ø{d}a{Mm(s)}";
    /// <summary>Lựa chọn "giáp nhà hàng xóm (hết đất)" / "sát ranh": khác rỗng và không bắt đầu bằng "Không".</summary>
    public static bool IsNeighbour(string choice) => !string.IsNullOrWhiteSpace(choice) && !choice.TrimStart().StartsWith("Không", StringComparison.OrdinalIgnoreCase);
    public static string Bars(int n, int d) => $"{n}Ø{d}";

    public static CheckItem Check(string name, double demand, double capacity, string unit, string note = "") =>
        new() { Name = name, Demand = demand, Capacity = capacity, Unit = unit, Note = note };

    /// <summary>Thanh thẳng / L / U rải theo bước s trên bề rộng w (số thanh = ⌊(w − 2c)/s⌋ + 1).</summary>
    public static int CountAcross(double w, double c, double s) => s <= 1e-9 ? 0 : (int)Math.Floor((w - 2 * c) / s + 1e-6) + 1;

    /// <summary>Chọn bước thép rải nhỏ nhất ≥ sMin để As/m ≥ asReq (bước làm tròn 10 mm xuống, ≤ sMax). Trả về (d, s).</summary>
    public static (int D, double S) ChooseSpaced(double asReqPerM, int[] dias, double sMax = 0.20, double sMin = 0.10)
    {
        foreach (int d in dias.OrderBy(x => x))
        {
            if (asReqPerM <= 1e-12) return (d, sMax);
            double s = Math.Floor(Area(d) / asReqPerM * 100 + 1e-9) / 100.0;
            s = Math.Min(s, sMax);
            if (s >= sMin - 1e-9) return (d, s);
        }
        int dm = dias.Max();
        return (dm, sMin);
    }

    public static BarMark Straight(string role, int dia, int count, double len, bool top = false, double spacing = 0) => new()
    {
        Role = role, Dia = dia, Count = count, Length = R10(len), Shape = "Thẳng", IsTop = top, Spacing = spacing,
        Segments = new List<double> { Math.Round(R10(len) * 1000) }, Start = 0, End = R10(len)
    };

    /// <summary>Thanh chữ U (2 móc bẻ đầu, đoạn giữa L, chân a) hoặc L (một chân).</summary>
    public static BarMark Hooked(string role, int dia, int count, double straight, double legA, double legB, bool top = false, double spacing = 0)
    {
        var segs = new List<double>();
        int legs = (legA > 1e-6 ? 1 : 0) + (legB > 1e-6 ? 1 : 0);
        if (legs == 2) { segs.Add(Math.Round(legA * 1000)); segs.Add(Math.Round(straight * 1000)); segs.Add(Math.Round(legB * 1000)); }
        else if (legs == 1) { segs.Add(Math.Round(straight * 1000)); segs.Add(Math.Round(Math.Max(legA, legB) * 1000)); }
        else segs.Add(Math.Round(straight * 1000));
        return new BarMark
        {
            Role = role, Dia = dia, Count = count, Length = R10(straight + legA + legB), IsTop = top, Spacing = spacing,
            Shape = legs == 2 ? "U" : legs == 1 ? "L" : "Thẳng", Segments = segs, Start = 0, End = R10(straight)
        };
    }

    /// <summary>Đai kín 2 cạnh s1 × s2 (mép ngoài đai), móc 135° mỗi đầu max(75 mm; 10d).</summary>
    public static BarMark Stirrup(string role, int dia, int count, double s1, double s2, double spacing) => new()
    {
        Role = role, Dia = dia, Count = count, Shape = "Đai", Spacing = spacing,
        Length = R10(2 * (s1 + s2) + 2 * Math.Max(0.075, 10 * dia / 1000.0)),
        Segments = new List<double> { Math.Round(s1 * 1000), Math.Round(s2 * 1000) }
    };

    /// <summary>Hàng thép dọc: số thanh n Ø d trên bề rộng b có đủ khe hở ≥ max(d; 25 mm) (dưới) / 30 mm (trên)?</summary>
    public static bool FitsInLayer(double b, double cover, int stirrupDia, int n, int d, bool top)
    {
        if (n <= 1) return true;
        double clear = (b - 2 * cover - n * d / 1000.0) / (n - 1);
        return clear >= Math.Max(d / 1000.0, top ? 0.030 : 0.025) - 1e-9;
    }
}

/// <summary>Nhãn ô nhập cho giao diện tự sinh (DetailingWindow): Label (tiếng Việt có dấu), Unit (hiển thị),
/// Scale = hệ số hiển thị (vd. 1000 để nhập mm cho thuộc tính lưu m), Group = nhóm ô, Hint = chú giải.
/// Áp dụng cho thuộc tính double / int / bool / string / enum của lớp *Input và lớp dòng (List&lt;T&gt; hiện thành bảng).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FieldAttribute : Attribute
{
    public string Label { get; }
    public string Unit { get; set; } = "";
    public double Scale { get; set; } = 1;
    public string Group { get; set; } = "";
    public string Hint { get; set; } = "";
    /// <summary>Danh sách lựa chọn cho string (phân cách bởi '|'), vd. "B20|B25|B30"</summary>
    public string Choices { get; set; } = "";
    /// <summary>Khoá hàng: các ô LIÊN TIẾP cùng Row hiện trên MỘT dòng (vd. "Ø [16] × số thanh/hàng [2] × số hàng [1]").</summary>
    public string Row { get; set; } = "";
    /// <summary>Tiêu đề dòng (đặt ở ô đầu tiên của hàng), vd. "Thép dưới chạy suốt".</summary>
    public string RowTitle { get; set; } = "";
    /// <summary>Nhãn ngắn của ô trong hàng, vd. "Ø", "số thanh/hàng", "neo".</summary>
    public string Short { get; set; } = "";
    public FieldAttribute(string label) { Label = label; }
}

/// <summary>Một bộ thiết kế cấu kiện của chế độ triển khai hồ sơ (để giao diện / xuất hồ sơ dùng chung).</summary>
public interface IDetailMember
{
    /// <summary>Tên cấu kiện (K1, D1, MB1, BN1…)</summary>
    string Name { get; set; }
    /// <summary>Số lượng cấu kiện giống hệt</summary>
    int Quantity { get; set; }
    /// <summary>Tính / kiểm tra, trả về kết quả (không ném ngoại lệ – lỗi ghi vào Messages "LỖI …").</summary>
    DetailResult Run(DetailSettings s);
    /// <summary>Hình vẽ DXF (mặt bằng, mặt cắt, chi tiết thép) – dùng DxfWriter như các bản vẽ khác.</summary>
    List<KetCauVXT.Drawing.Figure> Draw(DetailResult r, DetailSettings s);
}
