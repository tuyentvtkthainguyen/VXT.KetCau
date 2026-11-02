using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

// =====================================================================
// [MóngLệchTâm] Kết quả móng lệch tâm: thông tin lệch tâm của móng đơn / đài cọc / móng băng,
// dầm móng cân bằng (DM) nối móng biên với móng cột trong.
// =====================================================================

/// <summary>Một vị trí móng giống hệt đã gộp (nút chân cột, tim cột, độ lệch tâm tâm móng so với tim cột).</summary>
public class EccentricLocation
{
    public int NodeId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
}

/// <summary>Liên kết dầm móng cân bằng từ móng biên (A) tới móng cột trong / móng biên đối diện (B) – dữ liệu để thiết kế dầm DM.</summary>
public class StrapLink
{
    public int NodeA { get; set; }
    public int NodeB { get; set; }
    /// <summary>true = dầm chạy theo phương X</summary>
    public bool AlongX { get; set; }
    public double XA { get; set; }
    public double YA { get; set; }
    public double XB { get; set; }
    public double YB { get; set; }
    /// <summary>Khoảng cách tim cột A–B (m)</summary>
    public double Span { get; set; }
    /// <summary>Độ lệch tâm móng A (tâm đáy móng − tim cột, trị tuyệt đối, m) và móng B (0 nếu B là móng cột trong)</summary>
    public double E1 { get; set; }
    public double E2 { get; set; }
    /// <summary>Kích thước cột A theo phương dầm / vuông góc phương dầm (m)</summary>
    public double ColAlong { get; set; }
    public double ColAcross { get; set; }
    /// <summary>Chiều cao tầng 1 (chiều dài cột A, m) – kiểm tra độ cứng dầm so với cột</summary>
    public double ColHeight { get; set; }
    /// <summary>Mép móng A phía trong theo phương dầm, mép móng B phía A (m, toạ độ dọc dầm tính từ tim cột A)</summary>
    public double FaceA { get; set; }
    public double FaceB { get; set; }
    /// <summary>Lực dọc tính toán N1, N2 và mô men chân cột cùng mặt phẳng dầm M0 (A, B) theo từng tổ hợp</summary>
    public List<BaseLoad> LoadsA { get; set; } = new();
    public List<BaseLoad> LoadsB { get; set; } = new();
    /// <summary>true = móng cọc (đài lệch tâm)</summary>
    public bool PileCap { get; set; }
    public string FootingA { get; set; } = "";
    public string FootingB { get; set; } = "";
    /// <summary>Tiết diện do người dùng chọn (0 = tự chọn)</summary>
    public double UserB { get; set; }
    public double UserH { get; set; }
}

/// <summary>Móng kết hợp (móng phối hợp) cột biên + cột trong.</summary>
public class CombinedFootingInfo
{
    public int NodeA { get; set; }
    public int NodeB { get; set; }
    public bool AlongX { get; set; }
    /// <summary>Chiều dài móng theo phương nối 2 cột, bề rộng đầu phía ranh giới B1 và đầu kia B2 (m); hình chữ nhật: B1 = B2</summary>
    public double Length { get; set; }
    public double B1 { get; set; }
    public double B2 { get; set; }
    /// <summary>Toạ độ mép móng phía ranh giới theo phương nối (m, tuyệt đối) và hướng vào trong (+1 / −1)</summary>
    public double Start { get; set; }
    public int Dir { get; set; } = 1;
    /// <summary>Toạ độ trục vuông góc (tim 2 cột) – Y nếu AlongX</summary>
    public double Axis { get; set; }
    /// <summary>Vị trí hợp lực N1 + N2 so với mép phía ranh giới (m) và trọng tâm đáy móng (m)</summary>
    public double Resultant { get; set; }
    public double Centroid { get; set; }
    public double XB { get; set; }
    public double YB { get; set; }
    public double N1 { get; set; }
    public double N2 { get; set; }
    public bool Trapezoid => Math.Abs(B1 - B2) > 1e-6;
    public double Area => Length * (B1 + B2) / 2;
    /// <summary>Mô men dọc móng lớn nhất dương (căng dưới) / âm (căng trên) và lực cắt (TTGH I)</summary>
    public double MPos { get; set; }
    public double MNeg { get; set; }
    public double V { get; set; }
    /// <summary>Đa giác đáy móng (toạ độ tuyệt đối, m)</summary>
    public List<PilePoint> Outline { get; set; } = new();
}

/// <summary>Thông tin lệch tâm gắn với một móng đơn (FootingDesign.Eccentric), đài cọc (PileCapDesign.Eccentric).</summary>
public class EccentricInfo
{
    /// <summary>Phương án áp dụng và phương án chương trình đề xuất</summary>
    public EccentricScheme Scheme { get; set; }
    public EccentricScheme Recommended { get; set; }
    /// <summary>Lý do đề xuất / ghi chú phương án</summary>
    public string Recommendation { get; set; } = "";
    /// <summary>Các cạnh ranh giới làm móng lệch tâm, vd. "trái + trước"</summary>
    public string Sides { get; set; } = "";
    /// <summary>true = lệch tâm do ranh giới đất (false = người dùng nhập độ lệch tâm)</summary>
    public bool Boundary { get; set; }
    /// <summary>Tâm đáy móng − tim cột theo X, Y (m)</summary>
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double E0 => Math.Sqrt(OffsetX * OffsetX + OffsetY * OffsetY);
    /// <summary>Nút cột nối dầm cân bằng / gộp móng kết hợp theo X, Y (0 = không)</summary>
    public int PartnerX { get; set; }
    public int PartnerY { get; set; }
    public double SpanX { get; set; }
    public double SpanY { get; set; }
    /// <summary>Lực dọc tính toán N1 ở tổ hợp bất lợi, phản lực tại trọng tâm đáy móng R1 = N1 + ΔR (đòn bẩy) và ΔR (kN)</summary>
    public double N1 { get; set; }
    public double R1 { get; set; }
    public double DeltaR { get; set; }
    /// <summary>Móng cột trong: phần giảm tải đã kể β·ΣΔR (kN, tổ hợp bất lợi) và các nút móng biên gây giảm tải</summary>
    public double Relief { get; set; }
    public List<int> ReliefFrom { get; set; } = new();
    /// <summary>(b) mô men lệch tâm truyền vào chân cột / hệ giằng ΔM = N·e0 theo phương X (quay quanh Y) và Y (kN.m)</summary>
    public double ColumnMomentX { get; set; }
    public double ColumnMomentY { get; set; }
    /// <summary>Áp lực tiêu chuẩn ptb, pmax, pmin (kN/m²), tỷ lệ diện tích tiếp xúc (0…1) và độ lệch tâm hợp lực e (m)</summary>
    public double Ptb { get; set; }
    public double Pmax { get; set; }
    public double Pmin { get; set; }
    public double Contact { get; set; } = 1;
    public double ResultantE { get; set; }
    /// <summary>So sánh nhanh các phương án (một dòng mỗi phương án)</summary>
    public List<string> Options { get; set; } = new();
    /// <summary>Các vị trí móng giống hệt đã gộp (kể cả móng này)</summary>
    public List<EccentricLocation> Locations { get; set; } = new();
    /// <summary>Dầm cân bằng xuất phát từ móng này (mọi vị trí gộp)</summary>
    public List<StrapLink> Straps { get; set; } = new();
    /// <summary>Móng kết hợp (null nếu không phải)</summary>
    public CombinedFootingInfo Combined { get; set; }
    /// <summary>Đài cọc: phản lực quy đổi N' = R1 khi có dầm cân bằng</summary>
    public bool PileCap { get; set; }
}

/// <summary>Dầm móng cân bằng DM (strap beam) nối móng lệch tâm với móng cột trong: M = N1·e (lớn nhất phía cột biên), Q = R1 − N1.</summary>
public class StrapBeamDesign : MemberDesignBase
{
    public List<StrapLink> Links { get; set; } = new();
    public bool AlongX { get; set; }
    public double Span { get; set; }
    public double B { get; set; }
    public double H { get; set; }
    public double H0 { get; set; }
    /// <summary>Nội lực thiết kế: mô men âm (căng trên) lớn nhất M = N1·e1 + |M0| (kN.m), lực cắt Q = |R1 − N1| (kN), tổ hợp</summary>
    public double M { get; set; }
    public double V { get; set; }
    public string Combo { get; set; } = "";
    public double N1 { get; set; }
    public double R1 { get; set; }
    public double E1 { get; set; }
    public double E2 { get; set; }
    public double AsTop { get; set; }
    public double AsBottom { get; set; }
    public string TopBars { get; set; } = "";
    public string BottomBars { get; set; } = "";
    public string Stirrups { get; set; } = "";
    public int StirrupDia { get; set; }
    public double StirrupSpacing { get; set; }
    /// <summary>Độ cứng tương đối: (I/L) dầm và 2·(I/H) cột</summary>
    public double StiffBeam { get; set; }
    public double StiffColumn { get; set; }
    /// <summary>Chiều dài dầm thông thuỷ giữa 2 mép móng (m) – tính bê tông, ván khuôn</summary>
    public double ClearLength { get; set; }
    public bool PileCap { get; set; }
}
