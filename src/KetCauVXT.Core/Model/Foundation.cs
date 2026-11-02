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
