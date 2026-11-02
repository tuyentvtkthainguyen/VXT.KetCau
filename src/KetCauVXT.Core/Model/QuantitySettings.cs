namespace KetCauVXT.Model;

// =====================================================================
// ĐỊNH MỨC & TUỲ CHỌN TÍNH BẢNG KHỐI LƯỢNG VẬT TƯ (Report/Quantities.cs)
// Giá trị mặc định chỉ để THAM KHẢO – trích theo Định mức dự toán 1776/BXD-VP (2007),
// Thông tư 12/2021/TT-BXD và bảng cấp phối vật liệu kèm định mức (xi măng PC30/PC40, cát vàng, đá 1×2).
// Người dùng kiểm tra, chỉnh sửa theo định mức / cấp phối thực tế của công trình.
// Lưu trong Project.QuantitySettings (JSON); file cũ không có mục này → dùng mặc định.
// =====================================================================

/// <summary>Cấp phối 1 m³ bê tông theo cấp độ bền.</summary>
public class ConcreteMixNorm
{
    /// <summary>Cấp độ bền: "B7.5", "B15", "B20", "B25", "B30"…</summary>
    public string Grade { get; set; } = "";
    /// <summary>Mác tương đương (ghi chú)</summary>
    public string Mark { get; set; } = "";
    /// <summary>Xi măng (kg/m³)</summary>
    public double Cement { get; set; }
    /// <summary>Cát vàng (m³/m³)</summary>
    public double Sand { get; set; }
    /// <summary>Đá dăm (m³/m³)</summary>
    public double Stone { get; set; }
    /// <summary>Nước (lít/m³)</summary>
    public double Water { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>Cấp phối 1 m³ vữa xi măng – cát vàng.</summary>
public class MortarMixNorm
{
    /// <summary>Mác vữa: "M50", "M75", "M100"</summary>
    public string Grade { get; set; } = "";
    public double Cement { get; set; }
    public double Sand { get; set; }
    public double Water { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>Định mức xây 1 m³ tường gạch theo chiều dày.</summary>
public class MasonryNorm
{
    public string Name { get; set; } = "";
    /// <summary>Áp dụng cho tường dày ≤ MaxThickness (m)</summary>
    public double MaxThickness { get; set; }
    /// <summary>Số viên gạch / m³ xây</summary>
    public double Bricks { get; set; }
    /// <summary>Vữa xây (m³ / m³ xây)</summary>
    public double Mortar { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>Đơn giá vật tư do người dùng nhập (tuỳ chọn). Tên vật tư trùng tên trong bảng phân tích vật tư.</summary>
public class UnitPrice
{
    public string Item { get; set; } = "";
    public string Unit { get; set; } = "";
    /// <summary>Đơn giá (đồng / đơn vị); 0 = chưa nhập</summary>
    public double Price { get; set; }
}

/// <summary>Tuỳ chọn + định mức tính bảng khối lượng vật tư.</summary>
public class QuantitySettings
{
    public const string DefaultSource = "Tham khảo Định mức 12/2021/TT-BXD và Định mức 1776/BXD-VP (cấp phối vật liệu kèm định mức) – người dùng kiểm tra, chỉnh sửa theo định mức, cấp phối áp dụng cho công trình.";

    /// <summary>Ghi chú nguồn định mức (in trong bảng Định mức)</summary>
    public string NormSource { get; set; } = DefaultSource;

    public List<ConcreteMixNorm> ConcreteNorms { get; set; } = DefaultConcreteNorms();
    public List<MortarMixNorm> MortarNorms { get; set; } = DefaultMortarNorms();
    public List<MasonryNorm> MasonryNorms { get; set; } = DefaultMasonryNorms();

    /// <summary>Tên gạch xây</summary>
    public string BrickName { get; set; } = "Gạch đặc 220×105×60";
    /// <summary>Mác vữa xây tường / vữa trát / vữa lót – láng sàn</summary>
    public string MasonryMortar { get; set; } = "M75";
    public string PlasterMortar { get; set; } = "M75";
    public string FloorMortar { get; set; } = "M75";
    /// <summary>Vữa trát (m³/m²) cho lớp trát dày 15 mm (tỷ lệ theo chiều dày trát của loại tường). 1776 AK.21: 0,017 m³/m²</summary>
    public double PlasterMortarPerM2 { get; set; } = 0.017;

    /// <summary>Dây thép buộc (kg / tấn cốt thép) – 1776 AF.61: 14,28 kg/t</summary>
    public double TieWirePerTon { get; set; } = 14.28;
    /// <summary>Gỗ ván khuôn (m³ / 100 m² ván khuôn)</summary>
    public double FormworkWoodPer100 { get; set; } = 0.792;
    /// <summary>Đinh (kg / 100 m² ván khuôn)</summary>
    public double FormworkNailsPer100 { get; set; } = 12.0;

    /// <summary>Bê tông lót móng: chiều dày (m), mở rộng mỗi phía so với đáy móng (m), cấp độ bền</summary>
    public double LeanThickness { get; set; } = 0.10;
    public double LeanOffset { get; set; } = 0.10;
    public string LeanGrade { get; set; } = "B7.5";

    /// <summary>Tính cầu thang (Project.Stairs; lỗ sàn cầu thang chưa khai báo → cầu thang 2 vế mặc định)</summary>
    public bool IncludeStairs { get; set; } = true;
    /// <summary>Chiều dày bản thang, chiếu nghỉ (m) của cầu thang mặc định (lỗ sàn chưa khai báo cầu thang)</summary>
    public double StairSlabThickness { get; set; } = 0.12;
    // ---- [cầu thang: mặc định cho lỗ sàn chưa khai báo cầu thang – Model/Stair.cs] ----
    /// <summary>Cấu tạo bậc mặc định: xây gạch (không tính bê tông) hoặc đổ BTCT</summary>
    public StepConstruction StairSteps { get; set; } = StepConstruction.Brick;
    /// <summary>Vật liệu ốp bậc mặc định</summary>
    public StepFinish StairFinish { get; set; } = StepFinish.Granite;
    /// <summary>Tên vật tư ốp bậc trong bảng phân tích vật tư (theo vật liệu)</summary>
    public string GraniteName { get; set; } = "Đá granite ốp bậc";
    public string WoodName { get; set; } = "Gỗ ốp bậc";
    public string StairTileName { get; set; } = "Gạch ốp bậc";
    /// <summary>Định mức xây bậc thang gạch (viên/m³, vữa m³/m³) – 1776 AE.6x xây bậc cầu thang ≈ 550 viên, 0,29 m³ vữa</summary>
    public double StepBricks { get; set; } = 550;
    public double StepMortar { get; set; } = 0.29;
    // ---- [hết cầu thang] ----

    /// <summary>Ước tính lanh tô trên lỗ cửa của tường</summary>
    public bool IncludeLintels { get; set; } = true;
    /// <summary>Chiều cao tiết diện lanh tô (m), đoạn gác mỗi bên (m), chiều cao cửa quy ước để đổi diện tích cửa ra bề rộng (m)</summary>
    public double LintelHeight { get; set; } = 0.20;
    public double LintelBearing { get; set; } = 0.20;
    public double DoorHeight { get; set; } = 2.2;

    /// <summary>Đơn giá (tuỳ chọn) – rỗng: bảng không có cột Đơn giá / Thành tiền</summary>
    public List<UnitPrice> Prices { get; set; } = new();

    public ConcreteMixNorm ConcreteNorm(string grade)
    {
        string g = Norm(grade);
        return ConcreteNorms?.FirstOrDefault(n => Norm(n.Grade) == g);
    }

    public MortarMixNorm MortarNorm(string grade)
    {
        string g = Norm(grade);
        return MortarNorms?.FirstOrDefault(n => Norm(n.Grade) == g);
    }

    /// <summary>Định mức xây theo chiều dày tường: định mức có MaxThickness nhỏ nhất ≥ t (không có → định mức dày nhất).</summary>
    public MasonryNorm MasonryNormFor(double t)
    {
        var l = (MasonryNorms ?? new List<MasonryNorm>()).OrderBy(n => n.MaxThickness).ToList();
        if (l.Count == 0) return null;
        return l.FirstOrDefault(n => t <= n.MaxThickness + 1e-6) ?? l[^1];
    }

    public double PriceOf(string item)
    {
        if (Prices == null || string.IsNullOrWhiteSpace(item)) return 0;
        var u = Prices.FirstOrDefault(x => string.Equals((x.Item ?? "").Trim(), item.Trim(), StringComparison.OrdinalIgnoreCase));
        return u?.Price ?? 0;
    }

    public bool HasPrices => Prices != null && Prices.Any(x => x.Price > 0);

    private static string Norm(string s) => (s ?? "").Trim().Replace(',', '.').ToUpperInvariant();

    // ------------------------------------------------------------------
    // Giá trị mặc định (tham khảo)
    // ------------------------------------------------------------------
    /// <summary>Cấp phối bê tông đá 1×2 (lót: đá 4×6), độ sụt 2–4 cm, xi măng PC30 (B7.5…B20) / PC40 (B22.5…B30).</summary>
    public static List<ConcreteMixNorm> DefaultConcreteNorms() => new()
    {
        new() { Grade = "B7.5", Mark = "M100", Cement = 200.85, Sand = 0.531, Stone = 0.936, Water = 170, Note = "Bê tông lót đá 4×6, PC30" },
        new() { Grade = "B12.5", Mark = "M150", Cement = 288.03, Sand = 0.505, Stone = 0.913, Water = 185, Note = "Đá 1×2, PC30" },
        new() { Grade = "B15", Mark = "M200", Cement = 350.55, Sand = 0.481, Stone = 0.900, Water = 185, Note = "Đá 1×2, PC30" },
        new() { Grade = "B20", Mark = "M250", Cement = 415.13, Sand = 0.455, Stone = 0.887, Water = 185, Note = "Đá 1×2, PC30" },
        new() { Grade = "B22.5", Mark = "M300", Cement = 384.0, Sand = 0.457, Stone = 0.879, Water = 185, Note = "Đá 1×2, PC40" },
        new() { Grade = "B25", Mark = "M350", Cement = 431.0, Sand = 0.441, Stone = 0.868, Water = 185, Note = "Đá 1×2, PC40" },
        new() { Grade = "B30", Mark = "M400", Cement = 470.0, Sand = 0.425, Stone = 0.857, Water = 185, Note = "Đá 1×2, PC40" },
    };

    /// <summary>Cấp phối vữa xi măng PC30 – cát vàng (mô đun độ lớn ML &gt; 2).</summary>
    public static List<MortarMixNorm> DefaultMortarNorms() => new()
    {
        new() { Grade = "M50", Cement = 230.02, Sand = 1.12, Water = 260, Note = "XM PC30, cát vàng" },
        new() { Grade = "M75", Cement = 320.03, Sand = 1.09, Water = 260, Note = "XM PC30, cát vàng" },
        new() { Grade = "M100", Cement = 410.0, Sand = 1.05, Water = 260, Note = "XM PC30, cát vàng" },
    };

    /// <summary>Xây tường thẳng gạch đặc (gạch chỉ) 6,5×10,5×22 cm – 1776 nhóm AE.22.</summary>
    public static List<MasonryNorm> DefaultMasonryNorms() => new()
    {
        new() { Name = "Tường dày ≤ 11 cm", MaxThickness = 0.11, Bricks = 643, Mortar = 0.23, Note = "1776 AE.221xx" },
        new() { Name = "Tường dày ≤ 33 cm", MaxThickness = 0.33, Bricks = 550, Mortar = 0.29, Note = "1776 AE.222xx" },
        new() { Name = "Tường dày > 33 cm", MaxThickness = 9.99, Bricks = 539, Mortar = 0.30, Note = "1776 AE.223xx" },
    };
}
