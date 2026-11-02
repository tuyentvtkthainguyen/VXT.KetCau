namespace KetCauVXT.Model;

// =====================================================================
// [MóngLệchTâm] RANH GIỚI ĐẤT + LỆCH TÂM MÓNG TỪNG CHÂN CỘT
// Nhà phố thường có cột sát ranh giới đất: móng không được vượt ra ngoài ranh giới → móng lệch tâm.
// =====================================================================

/// <summary>Phía ranh giới đất trên mặt bằng: Trái = phía X nhỏ, Phải = phía X lớn, Trước = phía Y nhỏ, Sau = phía Y lớn.</summary>
public enum BoundarySide { Left = 0, Right = 1, Front = 2, Back = 3 }

/// <summary>Một cạnh ranh giới đất (đường thẳng song song trục X hoặc Y).
/// Toạ độ = toạ độ trục Grid (nếu khai báo và có trong dự án) + Offset ra phía ngoài lô đất, hoặc Coord khi Grid rỗng.</summary>
public class PropertyBoundary
{
    public BoundarySide Side { get; set; }
    /// <summary>Tên trục làm gốc (vd. "1", "A"); rỗng = dùng Coord</summary>
    public string Grid { get; set; } = "";
    /// <summary>Khoảng cách từ trục ra ranh giới, dương = ra phía ngoài lô đất (m). Cột 220 tim trục sát ranh: Offset = 0,11.</summary>
    public double Offset { get; set; }
    /// <summary>Toạ độ tuyệt đối (X với Trái/Phải, Y với Trước/Sau), m – dùng khi Grid rỗng</summary>
    public double Coord { get; set; }
    public bool Enabled { get; set; } = true;

    public bool IsVertical => Side == BoundarySide.Left || Side == BoundarySide.Right;
    /// <summary>+1: phía trong lô đất là chiều dương của trục (Trái, Trước); −1: Phải, Sau</summary>
    public int Inward => Side == BoundarySide.Left || Side == BoundarySide.Front ? 1 : -1;

    public static string SideName(BoundarySide s) => s switch
    {
        BoundarySide.Left => "Ranh giới trái", BoundarySide.Right => "Ranh giới phải", BoundarySide.Front => "Ranh giới trước", _ => "Ranh giới sau"
    };

    public PropertyBoundary Clone() => new() { Side = Side, Grid = Grid, Offset = Offset, Coord = Coord, Enabled = Enabled };
}

/// <summary>Phương án móng lệch tâm.</summary>
public enum EccentricScheme
{
    /// <summary>Chương trình tự chọn (ưu tiên dầm móng cân bằng khi có cột trong cách ≤ StrapMaxSpan trên cùng trục)</summary>
    Auto = 0,
    /// <summary>(a) Móng lệch tâm có dầm móng cân bằng (dầm đỡ / strap footing) nối với móng cột trong</summary>
    Strap = 1,
    /// <summary>(b) Móng lệch tâm tự cân bằng: đáy móng chịu toàn bộ mô men N·e0</summary>
    SelfBalanced = 2,
    /// <summary>(c) Móng kết hợp (móng phối hợp) cột biên + cột trong, hợp lực tại trọng tâm đáy móng</summary>
    Combined = 3,
    /// <summary>Không lệch tâm (đặt tâm móng tại tim cột, bỏ qua ranh giới)</summary>
    None = 4
}

/// <summary>Lựa chọn lệch tâm riêng cho một chân cột (FoundationSettings.Eccentric). Mọi giá trị 0 = tự xác định.</summary>
public class EccentricOverride
{
    public int NodeId { get; set; }
    /// <summary>true = độ lệch tâm tự tính theo ranh giới (móng sát ranh), false = dùng OffsetX/OffsetY</summary>
    public bool AutoOffset { get; set; } = true;
    /// <summary>Độ lệch tâm móng = tâm đáy móng − tim cột theo X, Y (m) khi AutoOffset = false</summary>
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public EccentricScheme Scheme { get; set; } = EccentricScheme.Auto;
    /// <summary>Nút chân cột trong để nối dầm cân bằng / gộp móng kết hợp theo X, Y; 0 = tự tìm</summary>
    public int PartnerX { get; set; }
    public int PartnerY { get; set; }
    /// <summary>Tiết diện dầm móng cân bằng b × h (m); 0 = tự chọn</summary>
    public double StrapB { get; set; }
    public double StrapH { get; set; }
    public string Note { get; set; } = "";

    public EccentricOverride Clone() => new()
    {
        NodeId = NodeId, AutoOffset = AutoOffset, OffsetX = OffsetX, OffsetY = OffsetY, Scheme = Scheme, PartnerX = PartnerX, PartnerY = PartnerY,
        StrapB = StrapB, StrapH = StrapH, Note = Note
    };

    public static string SchemeName(EccentricScheme s) => s switch
    {
        EccentricScheme.Auto => "Tự chọn phương án",
        EccentricScheme.Strap => "(a) Có dầm móng cân bằng",
        EccentricScheme.SelfBalanced => "(b) Lệch tâm tự cân bằng",
        EccentricScheme.Combined => "(c) Móng kết hợp",
        _ => "Không lệch tâm (tâm tại tim cột)"
    };
}
