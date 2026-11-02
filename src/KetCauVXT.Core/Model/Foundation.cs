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

    // ---- móng băng ----
    /// <summary>Phương dải móng băng: 0 = tự chọn (theo phương có nhiều cột thẳng hàng), 1 = dọc trục X, 2 = dọc trục Y, 3 = giao thoa 2 phương</summary>
    public int StripDirection { get; set; }
    /// <summary>Bề rộng sườn móng băng (m)</summary>
    public double StripRibWidth { get; set; } = 0.30;
    /// <summary>Hệ số nền Winkler (kN/m³) dùng tính dầm trên nền đàn hồi; 0 = lấy theo sức chịu tải ≈ 120·R</summary>
    public double SubgradeModulus { get; set; }
    /// <summary>Độ vươn đầu móng băng ra ngoài tim cột biên (m)</summary>
    public double StripOverhang { get; set; } = 0.6;
}
