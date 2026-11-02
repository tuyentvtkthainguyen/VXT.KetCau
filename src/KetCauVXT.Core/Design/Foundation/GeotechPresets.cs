using System.Globalization;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

/// <summary>Một loại đất tham khảo: khoảng giá trị điển hình và giá trị đại diện (dùng điền nhanh lớp đất).
/// THAM KHẢO – phải theo kết quả khảo sát thực tế.</summary>
public sealed class SoilPreset
{
    public string Name { get; init; } = "";
    public SoilKind Kind { get; init; }
    public SandGrain Grain { get; init; } = SandGrain.Medium;
    /// <summary>Khoảng giá trị (min, max); NaN = không quy định / không áp dụng; max = +∞ khi chỉ có cận dưới</summary>
    public (double Min, double Max) GammaRange { get; init; } = (double.NaN, double.NaN);
    public (double Min, double Max) PhiRange { get; init; } = (double.NaN, double.NaN);
    public (double Min, double Max) CRange { get; init; } = (double.NaN, double.NaN);
    public (double Min, double Max) ERange { get; init; } = (double.NaN, double.NaN);
    public (double Min, double Max) NRange { get; init; } = (double.NaN, double.NaN);
    /// <summary>Sức chịu tải quy ước R0 (kPa)</summary>
    public (double Min, double Max) R0Range { get; init; } = (double.NaN, double.NaN);
    // giá trị đại diện
    public double Gamma { get; init; }
    public double Phi { get; init; }
    public double C { get; init; }
    public double E { get; init; }
    public double N { get; init; }
    public double IL { get; init; } = -1;
    public double VoidRatio { get; init; }
    /// <summary>Nhận xét / khuyến nghị phương án móng</summary>
    public string Note { get; init; } = "";

    private static string Fmt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>"16–18", "&lt; 50", "&gt; 600", "0" hoặc "–".</summary>
    public static string RangeText((double Min, double Max) r)
    {
        if (double.IsNaN(r.Min) && double.IsNaN(r.Max)) return "–";
        if (double.IsNaN(r.Min)) return "< " + Fmt(r.Max);
        if (double.IsNaN(r.Max) || double.IsPositiveInfinity(r.Max)) return "> " + Fmt(r.Min);
        if (Math.Abs(r.Max - r.Min) < 1e-9) return Fmt(r.Min);
        return Fmt(r.Min) + "–" + Fmt(r.Max);
    }

    public string GammaText => RangeText(GammaRange);
    public string PhiText => RangeText(PhiRange);
    public string CText => RangeText(CRange);
    public string EText => RangeText(ERange);
    public string NText => RangeText(NRange);
    public string R0Text => RangeText(R0Range);

    /// <summary>Tạo lớp đất từ giá trị đại diện.</summary>
    public SoilLayer ToLayer(double top, double bottom) => new()
    {
        Name = Name, Kind = Kind, Grain = Grain, Top = top, Bottom = bottom,
        Gamma = Gamma, GammaSat = 0, Phi = Phi, C = C, E = E, N = N, IL = IL, VoidRatio = VoidRatio,
        Description = "Theo đất tham khảo \"" + Name + "\" – phải thay bằng kết quả khảo sát thực tế"
    };

    /// <summary>Gán giá trị đại diện vào lớp có sẵn (giữ tên nếu đã có, giữ độ sâu).</summary>
    public void FillLayer(SoilLayer l)
    {
        if (l == null) return;
        if (string.IsNullOrWhiteSpace(l.Name)) l.Name = Name;
        l.Kind = Kind; l.Grain = Grain;
        l.Gamma = Gamma; l.GammaSat = 0; l.Phi = Phi; l.C = C; l.E = E; l.N = N; l.IL = IL; l.VoidRatio = VoidRatio;
        if (string.IsNullOrWhiteSpace(l.Description)) l.Description = "Theo đất tham khảo \"" + Name + "\"";
    }
}

/// <summary>Bảng đất tham khảo (giá trị điển hình vùng đồng bằng / trung du Bắc Bộ, tổng hợp từ TCVN 9362:2012, TCVN 10304:2014
/// và kinh nghiệm khảo sát). Chỉ để gợi ý ban đầu – THAM KHẢO, PHẢI THEO KẾT QUẢ KHẢO SÁT THỰC TẾ.</summary>
public static class SoilPresets
{
    public const string Disclaimer = "Tham khảo – phải theo kết quả khảo sát thực tế";
    private const double Inf = double.PositiveInfinity;

    /// <summary>Nền đất tốt giả định khi chưa có khảo sát: sét pha dẻo cứng γ = 19 kN/m³, φ = 16°, c = 25 kPa, E = 10 MPa.</summary>
    public const double GoodGamma = 19, GoodPhi = 16, GoodC = 25, GoodE = 10;

    /// <summary>Đặt SoilSettings về nền đất tốt giả định (φ, c, γ, E; γ trên đáy móng = 18).</summary>
    public static void ApplyGoodSoil(SoilSettings s)
    {
        if (s == null) return;
        s.Phi = GoodPhi; s.C = GoodC; s.Gamma = GoodGamma; s.E = GoodE; s.GammaAbove = 18; s.ROverride = 0;
    }

    public static readonly IReadOnlyList<SoilPreset> All = new List<SoilPreset>
    {
        new()
        {
            Name = "Đất đắp / đất lấp", Kind = SoilKind.Fill, GammaRange = (16, 18), NRange = (2, 6),
            Gamma = 17, Phi = 10, C = 8, E = 3, N = 4, IL = -1, VoidRatio = 0.9,
            Note = "Không đặt móng trên đất đắp – đào bỏ hoặc đặt móng qua lớp này"
        },
        new()
        {
            Name = "Bùn sét / bùn sét pha (rất yếu)", Kind = SoilKind.Organic, GammaRange = (15, 16.5), PhiRange = (2, 5), CRange = (5, 10),
            ERange = (0.5, 2), NRange = (0, 2), R0Range = (double.NaN, 50),
            Gamma = 15.8, Phi = 3.5, C = 7.5, E = 1.2, N = 1, IL = 1.1, VoidRatio = 1.5,
            Note = "R0 < 50 kPa → cần gia cố nền hoặc dùng móng cọc xuyên qua"
        },
        new()
        {
            Name = "Sét, sét pha dẻo chảy – dẻo mềm (yếu)", Kind = SoilKind.SiltyClay, GammaRange = (17, 18.5), PhiRange = (5, 10), CRange = (10, 15),
            ERange = (2, 5), NRange = (2, 5), R0Range = (50, 100),
            Gamma = 17.8, Phi = 7.5, C = 12.5, E = 3.5, N = 3, IL = 0.8, VoidRatio = 1.0,
            Note = "Đất yếu – chỉ đặt móng nông cho nhà rất thấp tầng, kiểm tra lún; thường dùng móng cọc"
        },
        new()
        {
            Name = "Sét pha dẻo cứng", Kind = SoilKind.SiltyClay, GammaRange = (18.5, 19.5), PhiRange = (14, 18), CRange = (18, 28),
            ERange = (7, 12), NRange = (8, 15), R0Range = (150, 200),
            Gamma = 19, Phi = 16, C = 25, E = 10, N = 10, IL = 0.35, VoidRatio = 0.75,
            Note = "Nền tốt – móng nông (đơn / băng) cho nhà thấp tầng"
        },
        new()
        {
            Name = "Sét dẻo cứng – nửa cứng", Kind = SoilKind.Clay, GammaRange = (19, 20), PhiRange = (14, 20), CRange = (30, 50),
            ERange = (10, 20), NRange = (10, 25), R0Range = (200, 300),
            Gamma = 19.5, Phi = 17, C = 40, E = 15, N = 17, IL = 0.15, VoidRatio = 0.7,
            Note = "Nền tốt – móng nông; lớp mũi cọc khá cho nhà vừa"
        },
        new()
        {
            Name = "Cát pha dẻo", Kind = SoilKind.SandyClay, GammaRange = (18, 19), PhiRange = (18, 24), CRange = (5, 12),
            ERange = (6, 12), NRange = (5, 12), R0Range = (120, 200),
            Gamma = 18.5, Phi = 21, C = 8, E = 9, N = 8, IL = 0.5, VoidRatio = 0.65,
            Note = "Nền trung bình – móng nông cho nhà thấp tầng"
        },
        new()
        {
            Name = "Cát hạt nhỏ – vừa, rời xốp", Kind = SoilKind.Sand, Grain = SandGrain.Fine, GammaRange = (17, 18), PhiRange = (25, 28), CRange = (0, 2),
            ERange = (5, 10), NRange = (4, 10), R0Range = (100, 150),
            Gamma = 17.5, Phi = 26.5, C = 1, E = 7.5, N = 7, IL = -1, VoidRatio = 0.8,
            Note = "Nền trung bình – chú ý lún, hoá lỏng khi có động đất, nước ngầm"
        },
        new()
        {
            Name = "Cát hạt vừa, chặt vừa", Kind = SoilKind.Sand, Grain = SandGrain.Medium, GammaRange = (18.5, 19.5), PhiRange = (30, 34), CRange = (0, 2),
            ERange = (15, 30), NRange = (10, 30), R0Range = (200, 350),
            Gamma = 19, Phi = 32, C = 1, E = 20, N = 20, IL = -1, VoidRatio = 0.65,
            Note = "Nền tốt – móng nông; ma sát thân cọc tốt"
        },
        new()
        {
            Name = "Cát thô, sỏi sạn chặt", Kind = SoilKind.Gravel, Grain = SandGrain.Coarse, GammaRange = (19.5, 20.5), PhiRange = (34, 40), CRange = (0, 0),
            ERange = (30, 50), NRange = (30, 50), R0Range = (400, 600),
            Gamma = 20, Phi = 37, C = 0, E = 40, N = 40, IL = -1, VoidRatio = 0.55,
            Note = "Rất tốt – lớp đặt mũi cọc tốt"
        },
        new()
        {
            Name = "Đá phong hoá mạnh", Kind = SoilKind.Rock, GammaRange = (21, 23), NRange = (50, Inf), R0Range = (600, Inf),
            Gamma = 22, Phi = 35, C = 50, E = 100, N = 50, IL = -1, VoidRatio = 0.3,
            Note = "Rất tốt – móng nông hoặc mũi cọc tựa đá"
        }
    };

    // ------------------------------------------------------------------
    // Tương quan SPT (gợi ý)
    // ------------------------------------------------------------------
    /// <summary>Góc ma sát trong của cát theo Ohsaki: φ ≈ √(20·N) + 15 (độ).</summary>
    public static double PhiFromN(double n) => Math.Sqrt(20 * Math.Max(n, 0)) + 15;
    /// <summary>Sức kháng cắt không thoát nước của sét: cu ≈ 6,25·N (kPa) – TCVN 10304:2014 G.3.2.</summary>
    public static double CuFromN(double n) => 6.25 * Math.Max(n, 0);
    /// <summary>Mô đun biến dạng của cát: E ≈ (0,7…1,0)·N (MPa).</summary>
    public static (double Min, double Max) EFromN(double n) => (0.7 * Math.Max(n, 0), 1.0 * Math.Max(n, 0));

    /// <summary>Các dòng gợi ý tương quan cho một giá trị N (tiếng Việt).</summary>
    public static List<string> CorrelationHints(double n, bool cohesive)
    {
        var res = new List<string>();
        string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        if (cohesive)
            res.Add($"Đất dính: cu ≈ 6,25·N = {F(CuFromN(n))} kPa (TCVN 10304:2014 G.3.2)");
        else
        {
            res.Add($"Đất rời: φ ≈ √(20·N) + 15 = {F(PhiFromN(n))}° (Ohsaki)");
            var e = EFromN(n);
            res.Add($"Đất rời: E ≈ (0,7…1,0)·N = {F(e.Min)}…{F(e.Max)} MPa");
        }
        return res;
    }

    /// <summary>Loại đất tham khảo gần nhất với một lớp (theo loại đất rồi theo N).</summary>
    public static SoilPreset Nearest(SoilLayer l)
    {
        if (l == null) return All[3];
        var same = All.Where(p => p.Kind == l.Kind || (l.Kind == SoilKind.Clay && p.Kind == SoilKind.SiltyClay) || (l.Kind == SoilKind.SiltyClay && p.Kind == SoilKind.Clay)).ToList();
        if (same.Count == 0) same = All.ToList();
        return same.OrderBy(p => Math.Abs(p.N - l.N)).First();
    }
}
