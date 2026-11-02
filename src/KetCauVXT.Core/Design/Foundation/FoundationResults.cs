namespace KetCauVXT.Design.Foundation;

/// <summary>Kết quả thiết kế đài cọc dưới 1 chân cột (hoặc đài chung nhiều cột).</summary>
public class PileCapDesign : MemberDesignBase
{
    public List<int> NodeIds { get; set; } = new();
    /// <summary>Tâm đài (m)</summary>
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Kích thước đài theo X, Y và chiều cao (m)</summary>
    public double L { get; set; }
    public double B { get; set; }
    public double H { get; set; }
    /// <summary>Toạ độ tim cọc tương đối so với tâm đài (m)</summary>
    public List<(double X, double Y)> Piles { get; set; } = new();
    public double PileSize { get; set; }
    public bool PileRound { get; set; }
    public double PileCapacity { get; set; }
    /// <summary>Phản lực cọc lớn nhất / nhỏ nhất (kN) và tổ hợp gây ra</summary>
    public double PMax { get; set; }
    public double PMin { get; set; }
    public string Combo { get; set; } = "";
    public double N { get; set; }
    public double Mx { get; set; }
    public double My { get; set; }
    /// <summary>Thép đáy đài theo phương L, B</summary>
    public string BarsL { get; set; } = "";
    public string BarsB { get; set; } = "";
    public double AsL { get; set; }
    public double AsB { get; set; }
    public double ColumnB { get; set; }
    public double ColumnH { get; set; }
    public double Depth { get; set; }

    // ---- bổ sung ----
    /// <summary>Tâm các đài giống hệt đã gộp vào thiết kế này (gồm cả X, Y), m</summary>
    public List<(double X, double Y)> Locations { get; set; } = new();
    /// <summary>Lực cắt chân cột tổ hợp bất lợi (kN)</summary>
    public double Qx { get; set; }
    public double Qy { get; set; }
    /// <summary>Trọng lượng tính toán đài + đất trên đài = 1,1·γtb·L·B·Df (kN)</summary>
    public double Weight { get; set; }
    /// <summary>Phản lực từng cọc (kN, nén dương) của tổ hợp bất lợi Combo, cùng thứ tự với Piles</summary>
    public List<double> Reactions { get; set; } = new();
    /// <summary>Khoảng cách tim cọc (m) và chiều cao làm việc h0 (m)</summary>
    public double Spacing { get; set; }
    public double H0 { get; set; }
    /// <summary>Chiều dài cọc (m)</summary>
    public double PileLength { get; set; }
    /// <summary>Mô men uốn tại mép cột theo phương L, B (kN.m)</summary>
    public double MomentL { get; set; }
    public double MomentB { get; set; }
    /// <summary>Mô men lớp trên do cọc chịu nhổ theo phương L, B (kN.m)</summary>
    public double MomentTopL { get; set; }
    public double MomentTopB { get; set; }
    /// <summary>Thép lớp trên phương L / B (Ø–bước)</summary>
    public string BarsTopL { get; set; } = "";
    public string BarsTopB { get; set; } = "";
    /// <summary>Thép lớp trên đài (cấu tạo)</summary>
    public string BarsTop { get; set; } = "";
    public int DiaL { get; set; }
    public int DiaB { get; set; }
    public double SpacingL { get; set; }
    public double SpacingB { get; set; }
    /// <summary>[FoundationChecks] Bảng kiểm tra an toàn chi tiết (phản lực từng cọc, chọc thủng, nhóm cọc, lún…) – PileCapChecks.Attach</summary>
    public FoundationCheckReport Safety { get; set; }
    // ---- [MóngLệchTâm] đài lệch tâm do ranh giới đất ----
    /// <summary>Tâm đài − tim cột theo X, Y (m); Piles vẫn tính so với tim cột (nhóm cọc lệch tâm). 0 = đài đặt tâm tại tim cột.</summary>
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    /// <summary>Thông tin lệch tâm (null = đài đặt tâm tại tim cột)</summary>
    public EccentricInfo Eccentric { get; set; }
    // ---- hết [MóngLệchTâm] ----
}

/// <summary>Kết quả thiết kế một dải móng băng (dầm trên nền đàn hồi, tiết diện chữ T ngược).</summary>
public class StripFootingDesign : MemberDesignBase
{
    public List<int> NodeIds { get; set; } = new();
    /// <summary>true = dải dọc trục X (toạ độ Y không đổi)</summary>
    public bool AlongX { get; set; }
    /// <summary>Toạ độ trục dải (Y nếu AlongX, X nếu không) và phạm vi theo phương dải (m)</summary>
    public double C { get; set; }
    public double Start { get; set; }
    public double End { get; set; }
    /// <summary>Bề rộng bản đáy, chiều dày bản (mép / sát sườn), sườn b×h (m)</summary>
    public double Width { get; set; }
    public double FlangeEdge { get; set; }
    public double FlangeRoot { get; set; }
    public double RibB { get; set; }
    public double RibH { get; set; }
    public double Depth { get; set; }
    public double R { get; set; }
    public double PMax { get; set; }
    /// <summary>Biểu đồ dọc dải: toạ độ (m, cùng hệ với Start/End), mô men (kN.m, dương căng dưới) và lực cắt (kN) của tổ hợp TTGH I Combo,
    /// áp lực nền (kN/m², kể cả trọng lượng móng + đất) của tổ hợp tiêu chuẩn ComboService</summary>
    public List<double> Stations { get; set; } = new();
    public List<double> M { get; set; } = new();
    public List<double> V { get; set; } = new();
    public List<double> Pressure { get; set; } = new();
    public string TopBars { get; set; } = "";
    public string BottomBars { get; set; } = "";
    public string Stirrups { get; set; } = "";
    public string FlangeBars { get; set; } = "";
    public string Grid { get; set; } = "";

    // ---- bổ sung ----
    /// <summary>Tổ hợp TTGH I quyết định mô men (biểu đồ M, V) và tổ hợp tiêu chuẩn quyết định áp lực nền (biểu đồ Pressure)</summary>
    public string Combo { get; set; } = "";
    public string ComboService { get; set; } = "";
    /// <summary>Biểu đồ bao TTGH I tại các trạm Stations: mô men lớn nhất / nhỏ nhất (kN.m), |lực cắt| lớn nhất (kN)</summary>
    public List<double> MMax { get; set; } = new();
    public List<double> MMin { get; set; } = new();
    public List<double> VMax { get; set; } = new();
    /// <summary>Áp lực trung bình đáy móng (tiêu chuẩn), kN/m²</summary>
    public double PAvg { get; set; }
    /// <summary>Tổng lực nén chân cột truyền vào dải ở tổ hợp ComboService (kN) và trọng lượng móng + đất trên móng γtb·Df·B·L (kN)</summary>
    public double SumN { get; set; }
    public double SelfWeight { get; set; }
    /// <summary>Hệ số nền Winkler đã dùng (kN/m³)</summary>
    public double K { get; set; }
    /// <summary>Toạ độ cột dọc dải (m, cùng hệ toạ độ với Start/End) và kích thước cột theo phương dải (m)</summary>
    public List<double> ColumnPositions { get; set; } = new();
    public List<double> ColumnSizes { get; set; } = new();
    /// <summary>Kích thước cột vuông góc phương dải (m)</summary>
    public List<double> ColumnWidths { get; set; } = new();
    /// <summary>Tỷ lệ tải cột chia cho dải này (móng băng giao thoa: 0,5 tại nút giao)</summary>
    public List<double> LoadShares { get; set; } = new();
    /// <summary>Nội lực thiết kế: mô men dương (căng dưới) lớn nhất, mô men âm lớn nhất (≤ 0), lực cắt lớn nhất (TTGH I)</summary>
    public double MPos { get; set; }
    public double MNeg { get; set; }
    public double VDesign { get; set; }
    /// <summary>Diện tích thép yêu cầu dưới / trên sườn (m²) và thép bản cánh (m²/m)</summary>
    public double AsBottom { get; set; }
    public double AsTop { get; set; }
    public double AsFlange { get; set; }
    /// <summary>Mô men và lực cắt bản cánh tại chân sườn (trên 1 m dài)</summary>
    public double FlangeM { get; set; }
    public double FlangeV { get; set; }
    public int StirrupDia { get; set; }
    public double StirrupSpacing { get; set; }
    public int StirrupLegs { get; set; } = 2;
    /// <summary>Chiều cao làm việc sườn h0 và bản cánh h0f (m) – dùng khi lập phương án thép</summary>
    public double RibH0 { get; set; }
    public double FlangeH0 { get; set; }
    public double Length => End - Start;
    /// <summary>[FoundationChecks] Phân loại sự làm việc (dầm cứng / hữu hạn / mềm trên nền Winkler) và các kiểm tra bổ sung – FootingBehaviour.AnalyzeStrip</summary>
    public FoundationCheckReport Safety { get; set; }
    // ---- [MóngLệchTâm] móng băng lệch tâm trên ranh giới đất (bản cánh một phía) ----
    /// <summary>0 = bản cánh hai phía (đối xứng); +1 / −1 = bản cánh chỉ vươn về phía toạ độ tăng / giảm (sườn sát ranh giới)</summary>
    public int FlangeSide { get; set; }
    /// <summary>Toạ độ tim sườn (vuông góc phương dải, cùng hệ với C); C là tim bản đáy</summary>
    public double RibC { get; set; }
    /// <summary>Toạ độ tim cột (trục dải) vuông góc phương dải</summary>
    public double ColumnLine { get; set; }
    /// <summary>Độ lệch tâm tim cột – tâm bản đáy e = |ColumnLine − C| (m), mô men xoắn phân bố t = q·e (kN.m/m) và xoắn lớn nhất trong sườn T = t·l/2 (kN.m)</summary>
    public double Eccentricity { get; set; }
    public double TorsionPerM { get; set; }
    public double TorsionMax { get; set; }
    /// <summary>Mô men lệch tâm tại từng chân cột N·e (kN.m) – giằng ngang tiếp nhận</summary>
    public List<double> ColumnTorsion { get; set; } = new();
    public string Boundary { get; set; } = "";
    // ---- hết [MóngLệchTâm] ----
}
