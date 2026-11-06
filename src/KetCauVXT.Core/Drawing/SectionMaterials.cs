using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

// =====================================================================
// VẬT LIỆU TRÊN MẶT CẮT (quy ước ký hiệu vật liệu bản vẽ xây dựng – TCVN 7 / thực hành hồ sơ VN):
//   BTCT: gạch chéo 45° + chấm đá (ANSI31 + AR-CONC); bê tông lót: chấm đá (AR-CONC); gạch đặc: gạch chéo 45° (ANSI31);
//   gạch rỗng: gạch chéo thưa; gạch AAC: lưới chéo nét đứt; vữa trát / vữa lót: chấm mịn; đá ốp: gạch chéo + nét đứt;
//   gạch men / gạch lát: nét song song mịn; đất tự nhiên: EARTH; đất đắp / cát: AR-SAND; thép hình: tô đặc;
//   gỗ: vân gỗ; kính: nét xiên thưa; thạch cao: nét đứng đứt; chống thấm: tô đặc mảnh.
// Mẫu định nghĩa theo mm giấy (như acadiso.pat) → tỷ lệ mô hình = tỷ lệ bản vẽ × hệ số.
// =====================================================================

public enum SectionMaterial
{
    BTCT, BeTongLot, GachDac, GachRong, GachAAC, VuaTrat, Da, GachMen, DatTuNhien, DatDap, Cat, ThepHinh, Go, Kinh, ThachCao, ChongTham
}

public static class SectionMaterials
{
    public const string Aac = "VXT_AAC", Stone = "VXT_DA", Wood = "VXT_GO", Glass = "VXT_KINH", Tile = "VXT_MEN",
        Mortar = "VXT_VUA", Gypsum = "VXT_THACHCAO", Hollow = "VXT_GACHRONG";

    private static bool _reg;

    /// <summary>Đăng ký các mẫu tô riêng (một lần).</summary>
    public static void EnsurePatterns()
    {
        if (_reg) return;
        _reg = true;
        if (!HatchPatterns.Has(Aac)) HatchPatterns.Custom(Aac, (45, 1.6, new[] { 0.8, -0.8 }), (135, 1.6, new[] { 0.8, -0.8 }));
        if (!HatchPatterns.Has(Stone)) HatchPatterns.Custom(Stone, (45, 3.2, null), (135, 6.4, new[] { 2.0, -2.0 }));
        if (!HatchPatterns.Has(Wood)) HatchPatterns.Custom(Wood, (0, 1.2, new[] { 6.0, -1.5 }), (0, 3.6, new[] { 2.0, -4.0 }));
        if (!HatchPatterns.Has(Glass)) HatchPatterns.Custom(Glass, (60, 4.0, new[] { 1.5, -2.5 }));
        if (!HatchPatterns.Has(Tile)) HatchPatterns.Custom(Tile, (0, 0.6, null));
        if (!HatchPatterns.Has(Mortar)) HatchPatterns.Custom(Mortar, (0, 0.8, new[] { 0.0, -0.8 }), (90, 0.8, new[] { 0.0, -1.6 }));
        if (!HatchPatterns.Has(Gypsum)) HatchPatterns.Custom(Gypsum, (90, 1.5, new[] { 1.0, -0.8 }));
        if (!HatchPatterns.Has(Hollow)) HatchPatterns.Custom(Hollow, (45, 2.4, null), (45, 2.4, new[] { 0.0, -1.2 }));
    }

    /// <summary>Các lớp mẫu (tên mẫu, hệ số tỷ lệ, góc) của vật liệu – BTCT dùng 2 lớp (ANSI31 + AR-CONC).</summary>
    public static (string Pattern, double Factor, double Angle)[] Patterns(SectionMaterial m)
    {
        EnsurePatterns();
        return m switch
        {
            SectionMaterial.BTCT => new[] { (HatchPatterns.Ansi31, 1.0, 0.0), (HatchPatterns.ArConc, 0.5, 0.0) },
            SectionMaterial.BeTongLot => new[] { (HatchPatterns.ArConc, 0.5, 0.0) },
            SectionMaterial.GachDac => new[] { (HatchPatterns.Ansi31, 0.6, 0.0) },
            SectionMaterial.GachRong => new[] { (Hollow, 1.0, 0.0) },
            SectionMaterial.GachAAC => new[] { (Aac, 1.0, 0.0) },
            SectionMaterial.VuaTrat => new[] { (Mortar, 0.5, 0.0) },
            SectionMaterial.Da => new[] { (Stone, 0.5, 0.0) },
            SectionMaterial.GachMen => new[] { (Tile, 0.5, 0.0) },
            SectionMaterial.DatTuNhien => new[] { (HatchPatterns.Earth, 0.5, 45.0) },
            SectionMaterial.DatDap => new[] { (HatchPatterns.ArSand, 1.0, 0.0) },
            SectionMaterial.Cat => new[] { (HatchPatterns.ArSand, 0.6, 0.0) },
            SectionMaterial.ThepHinh => new[] { (HatchPatterns.Solid, 1.0, 0.0) },
            SectionMaterial.Go => new[] { (Wood, 0.6, 0.0) },
            SectionMaterial.Kinh => new[] { (Glass, 0.6, 0.0) },
            SectionMaterial.ThachCao => new[] { (Gypsum, 0.5, 0.0) },
            SectionMaterial.ChongTham => new[] { (HatchPatterns.Solid, 1.0, 0.0) },
            _ => new[] { (HatchPatterns.Ansi31, 1.0, 0.0) }
        };
    }

    public static string Name(SectionMaterial m) => m switch
    {
        SectionMaterial.BTCT => "Bê tông cốt thép",
        SectionMaterial.BeTongLot => "Bê tông lót",
        SectionMaterial.GachDac => "Gạch đặc xây",
        SectionMaterial.GachRong => "Gạch rỗng xây",
        SectionMaterial.GachAAC => "Gạch bê tông khí AAC",
        SectionMaterial.VuaTrat => "Vữa trát / vữa lót",
        SectionMaterial.Da => "Đá ốp / đá lát",
        SectionMaterial.GachMen => "Gạch lát / gạch ốp men",
        SectionMaterial.DatTuNhien => "Đất tự nhiên",
        SectionMaterial.DatDap => "Đất đắp đầm chặt",
        SectionMaterial.Cat => "Cát đệm / cát tôn nền",
        SectionMaterial.ThepHinh => "Thép hình",
        SectionMaterial.Go => "Gỗ",
        SectionMaterial.Kinh => "Kính",
        SectionMaterial.ThachCao => "Trần thạch cao",
        SectionMaterial.ChongTham => "Lớp chống thấm",
        _ => m.ToString()
    };

    /// <summary>Vật liệu từ tên vật liệu lớp tường (WallMaterials…) / chuỗi tự do.</summary>
    public static SectionMaterial FromName(string material)
    {
        string s = (material ?? "").ToLowerInvariant();
        if (s.Contains("aac") || s.Contains("khí")) return SectionMaterial.GachAAC;
        if (s.Contains("rỗng") || s.Contains("block") || s.Contains("lỗ")) return SectionMaterial.GachRong;
        if (s.Contains("cốt thép") || s.Contains("btct") || s.Contains("bê tông")) return SectionMaterial.BTCT;
        if (s.Contains("vữa") || s.Contains("sơn") || s.Contains("matit") || s.Contains("trát")) return SectionMaterial.VuaTrat;
        if (s.Contains("đá") || s.Contains("granite") || s.Contains("marble")) return SectionMaterial.Da;
        if (s.Contains("men") || s.Contains("ceramic") || s.Contains("porcelain") || s.Contains("lát")) return SectionMaterial.GachMen;
        if (s.Contains("gỗ")) return SectionMaterial.Go;
        if (s.Contains("kính")) return SectionMaterial.Kinh;
        if (s.Contains("thạch cao")) return SectionMaterial.ThachCao;
        if (s.Contains("thép")) return SectionMaterial.ThepHinh;
        if (s.Contains("cách nhiệt") || s.Contains("bông")) return SectionMaterial.Cat;
        return SectionMaterial.GachDac;
    }

    /// <summary>Tô vật liệu cho một vòng biên (mm bản vẽ) – luôn ghi (không phụ thuộc thiết lập bật / tắt hatch).</summary>
    public static int Hatch(DxfWriter w, SectionMaterial m, IList<(double X, double Y)> loop)
    {
        int n = 0;
        foreach (var (pat, f, ang) in Patterns(m))
            if (w.Hatch(new List<IList<(double X, double Y)>> { loop }, pat, w.HatchScale(f), ang, Lay.Hatch)) n++;
        return n;
    }

    public static int HatchRect(DxfWriter w, SectionMaterial m, double x1, double y1, double x2, double y2)
    {
        double a = Math.Min(x1, x2), b = Math.Max(x1, x2), c = Math.Min(y1, y2), d = Math.Max(y1, y2);
        if (b - a < 1e-6 || d - c < 1e-6) return 0;
        return Hatch(w, m, new List<(double X, double Y)> { (a, c), (b, c), (b, d), (a, d) });
    }

    /// <summary>Bảng chú thích vật liệu: mỗi dòng ô mẫu tô + tên. (x, y) = góc trên trái (mm bản vẽ). Trả về chiều cao bảng.</summary>
    public static double Legend(DxfWriter w, double x, double y, IEnumerable<SectionMaterial> mats)
    {
        var list = mats.Distinct().OrderBy(m => (int)m).ToList();
        if (list.Count == 0) return 0;
        double k = w.Scale, th = w.Th, sw = 12 * k, sh = 6 * k, rh = 9 * k;
        w.Text(Lay.Chu, x, y, th * 1.1, "CHÚ THÍCH VẬT LIỆU", 0, 0, 0);
        double yy = y - 1.5 * th;
        foreach (var m in list)
        {
            double y0 = yy - sh;
            w.Polyline(Lay.KyHieu, new List<(double X, double Y)> { (x, y0), (x + sw, y0), (x + sw, yy), (x, yy) }, true);
            HatchRect(w, m, x, y0, x + sw, yy);
            w.Text(Lay.Chu, x + sw + 2 * k, y0 + sh / 2 - th / 2, th, Name(m), 0, 0, 0);
            yy -= rh;
        }
        return y - yy;
    }
}
