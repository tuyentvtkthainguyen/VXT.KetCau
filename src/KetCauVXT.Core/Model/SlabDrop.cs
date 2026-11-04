using System.Globalization;

namespace KetCauVXT.Model;

// =====================================================================
// SÀN HẠ CỐT (hạ cos): ô sàn có mặt trên thấp hơn cao độ sàn tầng một khoảng Δ – sàn vệ sinh (lớp chống thấm + vữa tạo dốc,
// WC hạ sâu đặt xí bệt / ống thoát âm sàn), ban công, lô gia, sân thượng có lớp chống thấm dày, sàn bếp…
// Khai báo: Slab.LevelOffset (m, âm = hạ) + Slab.DropReason (ghi chú). Mặc định theo công năng: Project.SlabDrop (null = mặc định),
// chỉ áp dụng khi người dùng ĐỔI công năng ô sàn (ModelEdit.SetSlabUse / thêm ô sàn, ban công) – mở dự án cũ không đổi gì.
//
// Ảnh hưởng kết cấu (xem thêm Design/Concrete/SlabDropChecks.cs):
//  • Phân tích: tải sàn giữ nguyên (vẫn dồn về dầm đỡ); nút ô sàn vẫn ở cao độ tầng (Δ chỉ là thông tin hình học / cấu tạo).
//  • Thiết kế sàn: cạnh chung giữa hai ô chênh cốt > StepThreshold tính KHỚP (không liên tục mô men qua bậc); mỗi ô có thép mũ
//    cấu tạo riêng neo vào dầm, không có thép mũ / lưới trên vắt liên tục qua bậc.
//  • Dầm biên ô hạ cốt: kiểm tra h ≥ hs1 + Δ + hs2 (cảnh báo + đề nghị chiều cao dầm).
//  • Bản vẽ: mặt cắt dầm vẽ bậc (BeamSlabContext.LevelOffsetHook), mặt bằng kết cấu đánh dấu "SÀN HẠ COS −50" (Drawing/SlabDropDrawing.cs).
// =====================================================================

/// <summary>Một dòng bảng "Hạ cốt mặc định theo công năng".</summary>
public class SlabDropDefault
{
    public SlabUse Use { get; set; }
    /// <summary>Hạ cốt mặc định (m, âm = mặt sàn thấp hơn sàn tầng; 0 = không hạ).</summary>
    public double Offset { get; set; }
    public string Note { get; set; } = "";
}

/// <summary>Thiết lập sàn hạ cốt của dự án. Project.SlabDrop = null → dùng <see cref="Defaults"/> (file cũ không ghi gì).</summary>
public class SlabDropSettings
{
    /// <summary>Tự đặt hạ cốt mặc định khi đổi công năng ô sàn (Sàn vệ sinh, Ban công…).</summary>
    public bool ApplyOnUse { get; set; } = true;
    /// <summary>Chênh cốt (m) giữa hai ô kề nhau lớn hơn ngưỡng này → cạnh chung coi là bậc: tính khớp, thép mũ không liên tục.</summary>
    public double StepThreshold { get; set; } = 0.03;
    /// <summary>Bảng hạ cốt mặc định theo công năng.</summary>
    public List<SlabDropDefault> Rows { get; set; } = DefaultRows();

    /// <summary>Thực hành phổ biến: WC hạ 50 mm (WC hạ sâu đặt bệt âm / ống thoát sàn: 100…300 mm – sửa tay);
    /// ban công / lô gia hạ 50 mm; mái, sân thượng tạo dốc bằng lớp vữa / bê tông xỉ – không hạ.</summary>
    public static List<SlabDropDefault> DefaultRows() => new()
    {
        new() { Use = SlabUse.Normal, Offset = 0, Note = "Phòng ở – không hạ" },
        new() { Use = SlabUse.WC, Offset = -0.05, Note = "Sàn vệ sinh: chống thấm + vữa tạo dốc (WC hạ sâu đặt bệt âm: 100…300 mm)" },
        new() { Use = SlabUse.Balcony, Offset = -0.05, Note = "Ban công, lô gia: chống thấm, chặn nước tràn vào phòng" },
        new() { Use = SlabUse.Corridor, Offset = 0, Note = "Sảnh, hành lang – không hạ" },
        new() { Use = SlabUse.Storage, Offset = 0, Note = "Kho – không hạ" },
        new() { Use = SlabUse.Roof, Offset = 0, Note = "Mái, sân thượng: tạo dốc bằng lớp vữa / bê tông xỉ – không hạ" },
    };

    public static SlabDropSettings Defaults() => new();

    /// <summary>Thiết lập của dự án (null → mặc định, không gán vào dự án).</summary>
    public static SlabDropSettings Of(Project p) => p?.SlabDrop ?? Defaults();

    /// <summary>Hạ cốt mặc định (m) của công năng; công năng không có trong bảng → 0.</summary>
    public double For(SlabUse u) => Rows?.FirstOrDefault(r => r.Use == u)?.Offset ?? 0;
}

/// <summary>Thao tác khai báo hạ cốt sàn.</summary>
public static class SlabDrop
{
    /// <summary>Giới hạn khai báo (m): hạ tối đa 0,50 m; không cho nâng cốt (dương).</summary>
    public const double MinOffset = -0.50, MaxOffset = 0.0;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Ô sàn có hạ cốt (|Δ| ≥ 0,5 mm).</summary>
    public static bool IsLowered(Slab s) => s != null && s.LevelOffset < -0.0005;

    /// <summary>Δ (mm, làm tròn, âm) dạng chữ "−50".</summary>
    public static string MmText(double offsetM)
    {
        double mm = Math.Round(offsetM * 1000);
        return mm < 0 ? "-" + (-mm).ToString("0", Inv) : mm.ToString("0", Inv);
    }

    /// <summary>Cao độ so với sàn tầng "-0.050".</summary>
    public static string LevelText(double offsetM)
    {
        double r = Math.Round(offsetM, 3);
        if (Math.Abs(r) < 0.0005) return "±0.000";
        return (r > 0 ? "+" : "-") + Math.Abs(r).ToString("0.000", Inv);
    }

    /// <summary>Đặt hạ cốt cho ô sàn (m, âm = hạ). Ngoài phạm vi [−0,50; 0] → lỗi.</summary>
    public static EditResult SetDrop(Project p, Slab s, double offsetM, string reason = null)
    {
        if (s == null) return EditResult.Fail("Chưa chọn ô sàn.");
        if (!double.IsFinite(offsetM) || offsetM < MinOffset - 1e-9 || offsetM > MaxOffset + 1e-9)
            return EditResult.Fail("Hạ cốt sàn phải từ −500 đến 0 mm (số âm = mặt sàn thấp hơn sàn tầng).");
        s.LevelOffset = Math.Round(offsetM, 4);
        if (Math.Abs(s.LevelOffset) < 1e-6) s.LevelOffset = 0;
        if (reason != null) s.DropReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (s.LevelOffset == 0) s.DropReason = null;
        string lbl = string.IsNullOrEmpty(s.Label) ? "S" + s.Id.ToString(Inv) : s.Label;
        return new EditResult
        {
            Ok = true,
            Message = s.LevelOffset == 0 ? $"{lbl}: bỏ hạ cốt (mặt sàn bằng sàn tầng)."
                : $"{lbl}: hạ cốt Δ = {MmText(s.LevelOffset)} mm (▽ {LevelText(s.LevelOffset)} so với sàn tầng)" + (string.IsNullOrEmpty(s.DropReason) ? "." : " – " + s.DropReason + ".")
        };
    }

    /// <summary>Hạ cốt mặc định khi đổi công năng oldUse → newUse: chỉ khi thiết lập bật và ô chưa được chỉnh tay
    /// (Δ hiện tại = 0 hoặc = mặc định của công năng cũ). Trả về đoạn thông báo ("" = không đổi).</summary>
    public static string ApplyDefault(Project p, Slab s, SlabUse newUse, SlabUse oldUse)
    {
        if (s == null) return "";
        var set = SlabDropSettings.Of(p);
        if (!set.ApplyOnUse) return "";
        double cur = s.LevelOffset, oldDef = set.For(oldUse), def = set.For(newUse);
        bool untouched = Math.Abs(cur) < 1e-6 || Math.Abs(cur - oldDef) < 1e-6;
        if (!untouched)
            return Math.Abs(cur - def) < 1e-6 ? "" : $" Giữ hạ cốt đã khai báo Δ = {MmText(cur)} mm.";
        if (Math.Abs(cur - def) < 1e-6) return "";
        def = Math.Clamp(def, MinOffset, MaxOffset);
        s.LevelOffset = Math.Round(def, 4);
        if (s.LevelOffset == 0) { s.DropReason = null; return " Bỏ hạ cốt (công năng mới không hạ)."; }
        s.DropReason = ModelEdit.Loads(newUse).Name;
        return $" Hạ cốt mặc định Δ = {MmText(def)} mm (sửa ở 'Hạ cốt sàn…').";
    }

    /// <summary>Các ô sàn hạ cốt (theo tầng nếu story khác null).</summary>
    public static List<Slab> Lowered(Project p, string story = null) =>
        (p?.Slabs ?? new List<Slab>()).Where(s => IsLowered(s) && (story == null || s.Story == story)).ToList();
}
