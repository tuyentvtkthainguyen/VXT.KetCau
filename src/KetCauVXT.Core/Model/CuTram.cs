namespace KetCauVXT.Model;

// =====================================================================
// [CừTràm] GIA CỐ NỀN BẰNG CỪ TRÀM DƯỚI MÓNG ĐƠN / MÓNG BĂNG + [LùiCọcRanh] LÙI CỌC KHỎI RANH GIỚI ĐỂ ÉP CỌC
// Thông số lưu trong FoundationSettings.CuTram (file dự án cũ không có mục này vẫn mở được – mặc định tắt).
// Các giá trị mặc định là giá trị KINH NGHIỆM thực hành (Nam Bộ / đồng bằng sông Hồng), người dùng phải kiểm tra lại theo hồ sơ khảo sát.
// =====================================================================

/// <summary>Phương pháp tính sức chịu tải nền gia cố cừ tràm dùng để chọn kích thước móng.</summary>
public enum CuTramMethod
{
    /// <summary>Lấy giá trị nhỏ hơn của hai phương pháp (thiên về an toàn) – mặc định</summary>
    Conservative = 0,
    /// <summary>(1) Cừ tràm làm việc như cọc ma sát – nền phức hợp: [p]1 = (η·n·Qa − Gcừ)/A + αs·(1 − m)·R0, Qa = min(Qc/FS; Pvl)</summary>
    PileFriction = 1,
    /// <summary>(2) Nền tương đương sau gia cố (hệ số rỗng giảm) + khối móng quy ước tại mũi cừ</summary>
    EquivalentSoil = 2
}

/// <summary>Lựa chọn cừ tràm riêng một chân cột (ghi đè thiết lập chung FoundationSettings.CuTram.Enabled).</summary>
public class CuTramOverride
{
    public int NodeId { get; set; }
    /// <summary>true = gia cố cừ tràm dưới móng tại chân cột này; false = không gia cố (dù thiết lập chung bật)</summary>
    public bool Enabled { get; set; } = true;
    public CuTramOverride Clone() => new() { NodeId = NodeId, Enabled = Enabled };
}

/// <summary>Thông số gia cố nền bằng cừ tràm (FoundationSettings.CuTram).</summary>
public class CuTramSettings
{
    /// <summary>true = gia cố cừ tràm dưới mọi móng đơn (và móng băng nếu ApplyToStrip) của công trình; từng chân cột ghi đè bằng Overrides</summary>
    public bool Enabled { get; set; }
    /// <summary>Áp dụng cả cho móng băng (khi thiết lập chung bật)</summary>
    public bool ApplyToStrip { get; set; } = true;
    /// <summary>Chiều dài cây cừ (m), thực hành 3–5 m, mặc định 4,0 m</summary>
    public double Length { get; set; } = 4.0;
    /// <summary>Đường kính đầu cừ (gốc) D1 (m), thực hành 8–10 cm, mặc định 0,08 m</summary>
    public double TopDiameter { get; set; } = 0.08;
    /// <summary>Đường kính ngọn cừ (mũi) D2 (m), mặc định 0,045 m</summary>
    public double TipDiameter { get; set; } = 0.045;
    /// <summary>Mật độ cừ (cây/m²), thực hành 16–25, mặc định 25 (lưới ≈ 0,2 × 0,2 m)</summary>
    public double Density { get; set; } = 25;
    /// <summary>Chiều dày bê tông lót dưới đáy móng (m) – đầu cừ nằm sát đáy lớp bê tông lót</summary>
    public double LeanThickness { get; set; } = 0.10;
    /// <summary>Chiều dày lớp cát đệm phủ đầu cừ (m), 0 = không có; thực hành 0,10–0,20 m</summary>
    public double SandCushion { get; set; } = 0.10;
    /// <summary>Trọng lượng riêng gỗ tràm bão hoà nước (kN/m³) – kinh nghiệm ≈ 9–11</summary>
    public double TimberUnitWeight { get; set; } = 10;
    /// <summary>Cường độ chịu nén dọc thớ tính toán của gỗ tràm (kPa) – kinh nghiệm ≈ 10 MPa, người dùng kiểm tra</summary>
    public double TimberStrength { get; set; } = 10000;
    /// <summary>Hệ số an toàn sức chịu tải 1 cây cừ FS (2,0–2,5)</summary>
    public double SafetyFactor { get; set; } = 2.0;
    /// <summary>Hệ số nhóm η; 0 = tự tính theo Converse–Labarre</summary>
    public double GroupEfficiency { get; set; }
    /// <summary>Hệ số kể sức chịu tải của đất giữa các cây cừ αs (0…1) trong công thức nền phức hợp
    /// [p]1 = η·n·Qa/A + αs·(1 − m)·R0; mặc định 0,8 (kinh nghiệm, tương tự hệ số β của nền phức hợp cọc ma sát); 0 = không kể đất (an toàn nhất)</summary>
    public double SoilShare { get; set; } = 0.8;
    /// <summary>Phương pháp dùng để chọn kích thước móng</summary>
    public CuTramMethod Method { get; set; } = CuTramMethod.Conservative;
    /// <summary>Độ sâu mực nước ngầm THẤP NHẤT (mùa khô) tính từ mặt đất (m); −1 = lấy theo hố khoan (cảnh báo: MNN khi khảo sát chưa chắc là thấp nhất)</summary>
    public double MinWaterDepth { get; set; } = -1;
    /// <summary>Góc ma sát trong của đất sau gia cố φ1 (độ); 0 = tự ước lượng (kinh nghiệm)</summary>
    public double ImprovedPhi { get; set; }
    /// <summary>Lực dính của đất sau gia cố c1 (kPa); 0 = tự ước lượng c1 = c0·e0/e1 (kinh nghiệm)</summary>
    public double ImprovedC { get; set; }
    /// <summary>Hệ số rỗng ban đầu dùng khi lớp đất chưa có số liệu e0</summary>
    public double DefaultVoidRatio { get; set; } = 1.2;
    /// <summary>Độ lún giới hạn Sgh (m), mặc định 0,08 (TCVN 9362:2012 Bảng 16, nhà khung BTCT)</summary>
    public double SettlementLimit { get; set; } = 0.08;
    /// <summary>Lực nén tiêu chuẩn lớn nhất 1 chân cột nên dùng cừ tràm (kN) – vượt quá thì khuyến nghị cọc BTCT (kinh nghiệm)</summary>
    public double MaxColumnLoad { get; set; } = 600;
    /// <summary>Ghi đè từng chân cột</summary>
    public List<CuTramOverride> Overrides { get; set; } = new();

    public CuTramSettings Clone()
    {
        var c = (CuTramSettings)MemberwiseClone();
        c.Overrides = (Overrides ?? new List<CuTramOverride>()).Where(o => o != null).Select(o => o.Clone()).ToList();
        return c;
    }

    public static string MethodName(CuTramMethod m) => m switch
    {
        CuTramMethod.PileFriction => "(1) Cừ tràm như cọc ma sát – nền phức hợp",
        CuTramMethod.EquivalentSoil => "(2) Nền tương đương sau gia cố + khối móng quy ước",
        _ => "Giá trị nhỏ hơn của (1) và (2) – thiên về an toàn"
    };
}

/// <summary>Phương pháp ép cọc (chỉ để ghi chú khoảng lùi cần thiết – chương trình dùng một giá trị khoảng lùi).</summary>
public enum PilePressMethod
{
    /// <summary>Ép neo (máy ép nhỏ, neo xoắn) – thường lùi ≥ 0,5 m</summary>
    Anchor = 0,
    /// <summary>Ép tải (dàn ép + khối đối trọng) – thường lùi ≥ 0,5–0,7 m</summary>
    Weight = 1,
    /// <summary>Ép robot (máy ép tĩnh tự hành) – cần lùi lớn hơn, thường ≥ 1,0–1,5 m tuỳ máy (có giá ép biên)</summary>
    Robot = 2
}
