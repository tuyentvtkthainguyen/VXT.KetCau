namespace KetCauVXT.Model;

/// <summary>Phương án móng.</summary>
public enum FoundationType { Isolated, Pile, Strip }

/// <summary>Thông số thiết kế móng (dùng chung cho cả công trình; có thể đổi loại riêng từng chân cột qua Footing.Type).</summary>
public class FoundationSettings
{
    public FoundationType Type { get; set; } = FoundationType.Isolated;

    // ---- móng cọc (cọc BTCT đúc sẵn hoặc khoan nhồi) ----
    /// <summary>true = cọc tròn (đường kính PileSize), false = cọc vuông cạnh PileSize</summary>
    public bool PileRound { get; set; }
    /// <summary>Cạnh / đường kính cọc (m), vd. 0,25 / 0,30 / 0,50</summary>
    public double PileSize { get; set; } = 0.25;
    /// <summary>Chiều dài cọc (m)</summary>
    public double PileLength { get; set; } = 15;
    /// <summary>Sức chịu tải cho phép của 1 cọc khi nén [P] (kN) – từ thí nghiệm nén tĩnh / tính toán theo TCVN 10304:2014</summary>
    public double PileCapacity { get; set; } = 450;
    /// <summary>Sức chịu kéo cho phép của 1 cọc (kN), 0 = không cho phép kéo</summary>
    public double PileTension { get; set; }
    /// <summary>Khoảng cách tim cọc tối thiểu = hệ số × cạnh cọc (≥ 3)</summary>
    public double PileSpacingFactor { get; set; } = 3.0;
    /// <summary>Khoảng cách từ mép đài tới mép cọc (m)</summary>
    public double CapEdge { get; set; } = 0.15;
    /// <summary>Đoạn cọc ngàm vào đài (m)</summary>
    public double PileEmbed { get; set; } = 0.10;
    /// <summary>Chiều cao đài tối thiểu (m)</summary>
    public double CapMinHeight { get; set; } = 0.6;
    /// <summary>true = [P] và chiều dài cọc tính từ hố khoan theo SPT (TCVN 10304:2014 G.3.2, GeotechCalc.PileCapacity) cho từng chân cột (hố khoan gần nhất)</summary>
    public bool PileCapacityFromBorehole { get; set; }
    /// <summary>Độ sâu mũi cọc tính từ mặt đất (m); 0 = tự chọn: mặt lớp đầu tiên có N ≥ 30 (đất rời) hoặc N ≥ 20 (đất dính) + 3d, ≤ 40 m</summary>
    public double PileTipDepth { get; set; }
    /// <summary>Hệ số an toàn tổng thể FS: [P]đn = Rc,u/FS (tương đương γk·γn/γ0 của TCVN 10304:2014)</summary>
    public double PileSafetyFactor { get; set; } = 2.5;
    /// <summary>true = cọc khoan nhồi (qb = 150·Np), false = cọc đóng / ép (qb = 300·Np)</summary>
    public bool PileBored { get; set; }
    /// <summary>Diện tích thép dọc cọc As (m²) dùng tính Pvl; 0 = tự lấy 4Ø16 (d ≤ 300 mm) hoặc 6Ø18</summary>
    public double PileRebarArea { get; set; }
    // ---- cọc khoan nhồi theo TCVN 10304:2014 (Design/Foundation/BoredPileAdvanced.cs) ----
    /// <summary>Cọc khoan nhồi: phương pháp tính sức chịu tải theo bảng lớp đất chuyển từ hố khoan ("Chỉ tiêu cơ lý (Bảng 3, Bảng 7)" / "SPT (Phụ lục G.3.2)");
    /// rỗng = công thức SPT cũ (GeotechCalc.PileCapacity). File cũ không có mục này giữ nguyên kết quả.</summary>
    public string BoredMethod { get; set; } = "";
    /// <summary>Công nghệ khoan (γcf, γ'cb): dung dịch bentonite / ống vách / khoan khô</summary>
    public string BoredDrill { get; set; } = "";
    /// <summary>Tự chọn chiều dài cọc: L nhỏ nhất (bước 0,5 m) để [P] ≥ PileCapacity (yêu cầu) và mũi trong lớp tốt</summary>
    public bool PileAutoLength { get; set; }
    /// <summary>Bê tông cọc (rỗng = bê tông móng)</summary>
    public string PileConcrete { get; set; } = "";
    /// <summary>Kể hệ số nhóm Converse–Labarre (cọc ma sát): kiểm tra N + G ≤ η·n·[P]</summary>
    public bool PileGroupEfficiency { get; set; }

    // ---- [FoundationChecks] bố trí cọc do người dùng chọn + kiểm tra an toàn bổ sung ----
    /// <summary>Bố trí cọc / kích thước đài do người dùng chọn theo từng chân cột (PileCapOverride.NodeId); rỗng = tự chọn hoàn toàn.
    /// File dự án cũ không có mục này vẫn mở được (danh sách rỗng).</summary>
    public List<PileCapOverride> CapOverrides { get; set; } = new();
    /// <summary>Sức chịu tải ngang cho phép của 1 cọc [H] (kN); 0 = ước lượng sơ bộ 0,1·[P] (chỉ cảnh báo)</summary>
    public double PileLateralCapacity { get; set; }
    /// <summary>Bản vẽ: thể hiện tháp chọc thủng 45° (nét đứt) trên mặt cắt đài cọc</summary>
    public bool DrawPunchingPyramid { get; set; } = true;
    /// <summary>Bản vẽ: thể hiện biểu đồ mô men sườn dưới mặt cắt dọc móng băng</summary>
    public bool DrawStripMoment { get; set; }
    // ---- hết [FoundationChecks] ----

    // ---- [MóngLệchTâm] ranh giới đất + móng lệch tâm (Model/PropertyBoundary.cs, Design/Foundation/EccentricFooting.cs) ----
    /// <summary>Các cạnh ranh giới đất; rỗng = không giới hạn (móng đặt tâm tại tim cột như cũ). File cũ không có mục này vẫn mở được.</summary>
    public List<PropertyBoundary> Boundaries { get; set; } = new();
    /// <summary>Lựa chọn lệch tâm riêng từng chân cột (NodeId)</summary>
    public List<EccentricOverride> Eccentric { get; set; } = new();
    /// <summary>Khoảng hở từ mép móng tới ranh giới đất (m), mặc định 0 = móng sát ranh</summary>
    public double BoundaryClearance { get; set; }
    /// <summary>Dung sai nhận cột sát ranh: mép cột cách ranh giới ≤ giá trị này (m) thì móng phải lệch tâm</summary>
    public double BoundaryTolerance { get; set; } = 0.15;
    /// <summary>Phương án mặc định cho móng sát ranh</summary>
    public EccentricScheme EccentricDefault { get; set; } = EccentricScheme.Auto;
    /// <summary>Khoảng cách tim cột lớn nhất để nối dầm móng cân bằng tới cột trong (m)</summary>
    public double StrapMaxSpan { get; set; } = 6.0;
    /// <summary>Hệ số kể phần giảm tải cho móng cột trong do dầm cân bằng: N2' = N2 − β·(R1 − N1); thực hành β = 0,5 (không kể quá 50%)</summary>
    public double StrapReliefFactor { get; set; } = 0.5;
    // ---- hết [MóngLệchTâm] ----

    // ---- [LùiCọcRanh] lùi tim cọc khỏi ranh giới đất để thi công ép cọc (Design/Foundation/PileSetback.cs) ----
    /// <summary>true = bố trí cọc cách ranh giới đất ≥ PileSetback (nhà giáp ranh không ép sát ranh được). Mặc định tắt: file cũ giữ nguyên thiết kế.</summary>
    public bool PileSetbackEnabled { get; set; }
    /// <summary>Khoảng lùi tối thiểu từ ranh giới tới tim cọc (m), mặc định 0,5 m; từng cạnh ghi đè bằng PropertyBoundary.PileSetback</summary>
    public double PileSetback { get; set; } = 0.5;
    /// <summary>true = khoảng lùi đo tới MÉP cọc (thay vì tim cọc)</summary>
    public bool PileSetbackToFace { get; set; }

    // ---- [CọcÉp] sức chịu tải sơ bộ theo lực ép đầu cọc (TCVN 9394:2012): [P] = Pép/k ----
    /// <summary>Lực ép đầu cọc thực tế của máy ép (tấn), mặc định 50 t</summary>
    public double PilePressForce { get; set; } = 50;
    /// <summary>Hệ số k = Pép/[P] (TCVN 9394: Pép,min ≈ 1,5–2·[P], Pép,max ≈ 2–3·[P]); mặc định 2,0</summary>
    public double PilePressFactor { get; set; } = 2.0;
    /// <summary>Phương pháp ép cọc (ghi chú – robot cần khoảng lùi lớn hơn)</summary>
    public PilePressMethod PilePressMethod { get; set; } = PilePressMethod.Anchor;
    /// <summary>true = "Đài vươn ra sát ranh" (phần đài công xôn ngoài hàng cọc tới ranh giới); false = "Mép đài theo cọc" (mép đài = mép cọc + khoảng mép, tối thiểu phủ cột)</summary>
    public bool CapToBoundary { get; set; } = true;
    // ---- hết [LùiCọcRanh] ----

    // ---- [CừTràm] gia cố nền bằng cừ tràm dưới móng đơn / móng băng (Model/CuTram.cs, Design/Foundation/CuTramCalc.cs) ----
    /// <summary>Thông số cừ tràm; mặc định tắt (file cũ không có mục này vẫn mở được)</summary>
    public CuTramSettings CuTram { get; set; } = new();
    // ---- hết [CừTràm] ----

    // ---- [Truyền tải] tải dùng thiết kế móng (Design/Foundation/LoadTakedown.cs, FoundationUtil.Combos / MomentFactor) ----
    /// <summary>Hệ số mô men + lực cắt chân cột truyền vào móng: 1 = toàn bộ (mặc định, file cũ giữ nguyên); 0 = giằng móng chịu mô men (nhà phố thường dùng)</summary>
    public double BaseMomentFactor { get; set; } = 1.0;
    /// <summary>Chỉ dùng tổ hợp tải đứng (bỏ tổ hợp có gió / động đất) khi thiết kế móng</summary>
    public bool GravityCombosOnly { get; set; }
    // ---- hết [Truyền tải] ----
    // ---- [MóngHìnhDạng] bố trí móng (lệch tâm / xoay theo mép cột / đa giác) + đường chuẩn ranh đất xiên (Model/FootingPlacement.cs) ----
    /// <summary>Bố trí riêng từng móng đơn (NodeId); rỗng = móng chữ nhật đặt tâm cột như cũ</summary>
    public List<FootingPlacement> Placements { get; set; } = new();
    /// <summary>Đường chuẩn / ranh đất xiên: móng không vượt qua (cắt theo đường). Rỗng = không có</summary>
    public List<LotLine> LotLines { get; set; } = new();
    // ---- hết [MóngHìnhDạng] ----

    // ---- móng băng ----
    /// <summary>Phương dải móng băng: 0 = tự chọn (theo phương có nhiều cột thẳng hàng), 1 = dọc trục X, 2 = dọc trục Y, 3 = giao thoa 2 phương</summary>
    public int StripDirection { get; set; }
    /// <summary>Bề rộng sườn móng băng (m)</summary>
    public double StripRibWidth { get; set; } = 0.30;
    /// <summary>Hệ số nền Winkler (kN/m³) dùng tính dầm trên nền đàn hồi; 0 = lấy theo sức chịu tải ≈ 120·R</summary>
    public double SubgradeModulus { get; set; }
    /// <summary>Độ vươn đầu móng băng ra ngoài tim cột biên (m)</summary>
    public double StripOverhang { get; set; } = 0.6;

    // ---- móng bè (thể hiện trên mặt cắt – Drawing/SectionView.RaftShapes) ----
    /// <summary>Chiều dày bản móng bè (m); 0 = không dùng móng bè (mặt cắt vẽ móng đơn / băng / đài như cũ)</summary>
    public double RaftThickness { get; set; }
    /// <summary>Bè vươn ra ngoài tim cột biên (m)</summary>
    public double RaftOverhang { get; set; } = 0.5;
    /// <summary>Sườn bè: bề rộng và chiều cao toàn bộ (m); RaftBeamH ≤ RaftThickness = bè phẳng không sườn</summary>
    public double RaftBeamB { get; set; } = 0.4;
    public double RaftBeamH { get; set; }
}
