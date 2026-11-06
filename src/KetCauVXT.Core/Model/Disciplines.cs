namespace KetCauVXT.Model;

// =====================================================================
// BỘ MÔN (DISCIPLINE) – môi trường làm việc riêng: Kiến trúc | Kết cấu | Điện nước.
// Mô hình dùng chung một Project; mỗi đối tượng thuộc một bộ môn (Owner). Khi mở một bộ môn,
// đối tượng của bộ môn khác hiện như bản vẽ tham chiếu (xref): mờ (halftone), không chọn/sửa được,
// bật/tắt riêng từng bộ môn trong "Bộ môn hiển thị".
// API ổn định (bộ điện nước – Model/Mep – dùng):
//   Project.ActiveDiscipline, Project.DisciplineDisplay,
//   DisciplineView.Visible(p, d), DisciplineView.Halftone(p, d), DisciplineView.Editable(p, d),
//   DisciplineView.OwnerOf(kind).
// =====================================================================

/// <summary>Bộ môn thiết kế.</summary>
public enum Discipline
{
    /// <summary>Kiến trúc: tường, cửa, phòng, hoàn thiện, trần, mái, đồ đạc</summary>
    KienTruc,
    /// <summary>Kết cấu: cột, dầm, sàn, móng, cầu thang BTCT</summary>
    KetCau,
    /// <summary>Điện nước (MEP): ống, phụ kiện, thiết bị vệ sinh, điện, chiếu sáng</summary>
    DienNuoc
}

/// <summary>Cách hiện các bộ môn khi đang làm việc ở một bộ môn.</summary>
public class DisciplineSettings
{
    /// <summary>Hiện bộ môn Kiến trúc (khi không phải bộ môn đang mở)</summary>
    public bool ShowKienTruc { get; set; } = true;
    /// <summary>Hiện bộ môn Kết cấu (khi không phải bộ môn đang mở)</summary>
    public bool ShowKetCau { get; set; } = true;
    /// <summary>Hiện bộ môn Điện nước (khi không phải bộ môn đang mở)</summary>
    public bool ShowDienNuoc { get; set; } = true;
    /// <summary>Bộ môn khác vẽ mờ (halftone) như xref</summary>
    public bool HalftoneOthers { get; set; } = true;
    /// <summary>Độ mờ (0…1) của bộ môn tham chiếu khi vẽ mờ</summary>
    public double HalftoneOpacity { get; set; } = 0.35;

    public bool Shown(Discipline d) => d switch
    {
        Discipline.KienTruc => ShowKienTruc,
        Discipline.KetCau => ShowKetCau,
        _ => ShowDienNuoc
    };

    public void SetShown(Discipline d, bool on)
    {
        switch (d)
        {
            case Discipline.KienTruc: ShowKienTruc = on; break;
            case Discipline.KetCau: ShowKetCau = on; break;
            default: ShowDienNuoc = on; break;
        }
    }
}

/// <summary>Quy tắc hiện / sửa theo bộ môn.</summary>
public static class DisciplineView
{
    public static readonly Discipline[] All = { Discipline.KienTruc, Discipline.KetCau, Discipline.DienNuoc };

    /// <summary>Tên hiển thị tiếng Việt.</summary>
    public static string Title(Discipline d) => d switch
    {
        Discipline.KienTruc => "Kiến trúc",
        Discipline.KetCau => "Kết cấu",
        _ => "Điện nước"
    };

    /// <summary>Tiền tố số hiệu bản vẽ của bộ môn: KT-, KC-, DN-.</summary>
    public static string Prefix(Discipline d) => d switch
    {
        Discipline.KienTruc => "KT",
        Discipline.KetCau => "KC",
        _ => "DN"
    };

    /// <summary>Đối tượng của bộ môn d có hiện không (bộ môn đang mở luôn hiện).</summary>
    public static bool Visible(Project p, Discipline d)
    {
        if (p == null) return true;
        if (d == p.ActiveDiscipline) return true;
        return (p.DisciplineDisplay ?? new DisciplineSettings()).Shown(d);
    }

    /// <summary>Đối tượng của bộ môn d có vẽ mờ (tham chiếu) không.</summary>
    public static bool Halftone(Project p, Discipline d)
    {
        if (p == null || d == p.ActiveDiscipline) return false;
        return (p.DisciplineDisplay ?? new DisciplineSettings()).HalftoneOthers;
    }

    /// <summary>Độ mờ khi vẽ đối tượng của bộ môn d (1 = rõ, 0 = ẩn).</summary>
    public static double Opacity(Project p, Discipline d)
    {
        if (!Visible(p, d)) return 0;
        return Halftone(p, d) ? Math.Clamp((p.DisciplineDisplay ?? new DisciplineSettings()).HalftoneOpacity, 0.05, 1) : 1;
    }

    /// <summary>Chỉ đối tượng của bộ môn đang mở mới chọn / sửa được.</summary>
    public static bool Editable(Project p, Discipline d) => p == null || d == p.ActiveDiscipline;

    /// <summary>Bộ môn sở hữu theo loại đối tượng (tên loại tiếng Anh/Việt không phân biệt hoa thường):
    /// tường, cửa, phòng, trần, mái, hoàn thiện, đồ đạc → Kiến trúc; cột, dầm, sàn, móng, cầu thang, lỗ sàn → Kết cấu;
    /// ống, phụ kiện, thiết bị, điện, đèn, dây, ổ cắm, quạt, vệ sinh → Điện nước.</summary>
    public static Discipline OwnerOf(string kind)
    {
        string k = (kind ?? "").Trim().ToLowerInvariant();
        string[] arch = { "wall", "tuong", "tường", "door", "cua", "cửa", "window", "room", "phong", "phòng", "ceiling", "tran", "trần",
                          "roof", "mai", "mái", "finish", "hoan thien", "hoàn thiện", "furniture", "do dac", "đồ đạc", "groundslab", "san nen", "sàn nền" };
        string[] mep = { "pipe", "ong", "ống", "fitting", "phu kien", "phụ kiện", "fixture", "thiet bi", "thiết bị", "dien", "điện", "light",
                         "den", "đèn", "wire", "day", "dây", "outlet", "o cam", "ổ cắm", "fan", "quat", "quạt", "conduit", "duct", "mep",
                         "sanitary", "ve sinh", "vệ sinh", "lavabo", "bon cau", "bồn cầu", "sen", "voi", "vòi", "thoat san", "thoát sàn" };
        if (k.Length == 0) return Discipline.KetCau;
        // ưu tiên cụm dài (vd. "sàn nền" thuộc kiến trúc dù có "sàn")
        if (arch.Any(a => k == a || k.StartsWith(a + " ") || k.Contains(a) && a.Length > 3)) return Discipline.KienTruc;
        if (mep.Any(a => k == a || k.StartsWith(a + " ") || k.Contains(a) && a.Length > 3)) return Discipline.DienNuoc;
        return Discipline.KetCau;
    }
}
