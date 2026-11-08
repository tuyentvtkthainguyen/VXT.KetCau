using System.Globalization;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

// =====================================================================
// CHỈ TIÊU CƠ LÝ CỦA ĐẤT THEO BẢNG TRA TIÊU CHUẨN – TCVN 9362:2012 "Tiêu chuẩn thiết kế nền nhà và công trình"
// (Phụ lục: giá trị tiêu chuẩn của lực dính c, góc ma sát trong φ, mô đun biến dạng E của đất cát và đất dính kỷ Đệ tứ
//  theo hệ số rỗng e và độ sệt IL; áp lực tính toán quy ước R0 – dùng khi chưa có thí nghiệm, nhà và công trình cấp III, IV,
//  chọn sơ bộ kích thước móng). Bảng có cùng số liệu với SNiP 2.02.01-83 Phụ lục 1 và 3.
// Khi chỉ tiêu lấy từ bảng tra: hệ số độ tin cậy ktc = 1,1 trong công thức R (TCVN 9362:2012); lấy từ thí nghiệm trực tiếp: ktc = 1,0.
// Có hố khoan + thí nghiệm: người dùng nhập trực tiếp chỉ tiêu (SoilLayer.Source = Test).
// =====================================================================

/// <summary>Nhóm đất trong bảng tra TCVN 9362:2012.</summary>
public enum SoilStdClass
{
    /// <summary>Cát sỏi và cát thô (hạt to)</summary>
    GravelCoarseSand,
    /// <summary>Cát hạt vừa (hạt trung)</summary>
    MediumSand,
    /// <summary>Cát hạt nhỏ</summary>
    FineSand,
    /// <summary>Cát bụi</summary>
    SiltySand,
    /// <summary>Cát pha (á cát)</summary>
    SandyLoam,
    /// <summary>Sét pha (á sét)</summary>
    Loam,
    /// <summary>Sét</summary>
    Clay,
    /// <summary>Không có trong bảng tra (đất đắp, bùn, đá…) – phải khảo sát</summary>
    None
}

/// <summary>Một lớp đất cấu tạo tiêu chuẩn: loại đất + trạng thái → e, IL, Sr mặc định để tra bảng.</summary>
public sealed class StdSoil
{
    public string Name { get; init; } = "";
    public SoilKind Kind { get; init; }
    public SandGrain Grain { get; init; } = SandGrain.Medium;
    /// <summary>Trạng thái: "chặt", "chặt vừa", "rời", "cứng", "nửa cứng", "dẻo cứng", "dẻo mềm", "dẻo chảy", "dẻo"…</summary>
    public string State { get; init; } = "";
    /// <summary>Hệ số rỗng e mặc định của trạng thái</summary>
    public double E0 { get; init; }
    /// <summary>Độ sệt IL mặc định (−1 = đất rời)</summary>
    public double IL { get; init; } = -1;
    /// <summary>Độ bão hoà Sr mặc định</summary>
    public double Sr { get; init; } = 0.8;
    /// <summary>Chỉ số SPT ước lượng theo trạng thái (tham khảo cho tính cọc theo SPT – phải theo khảo sát)</summary>
    public double N { get; init; }
    public string Note { get; init; } = "";
    public override string ToString() => Name;
}

/// <summary>Kết quả tra bảng cho một lớp đất.</summary>
public sealed class StdSoilResult
{
    public SoilStdClass Class { get; set; }
    public double C { get; set; } = double.NaN;
    public double Phi { get; set; } = double.NaN;
    public double E { get; set; } = double.NaN;
    public double R0 { get; set; } = double.NaN;
    public double Gamma { get; set; } = double.NaN;
    public double GammaSat { get; set; } = double.NaN;
    /// <summary>Trạng thái suy ra (chặt / chặt vừa / rời; cứng / nửa cứng / dẻo cứng…)</summary>
    public string State { get; set; } = "";
    /// <summary>Nguồn / bảng tra</summary>
    public List<string> Sources { get; } = new();
    /// <summary>Ghi chú: ngoài phạm vi bảng, lấy giá trị biên, không có trong bảng…</summary>
    public List<string> Warnings { get; } = new();
    public bool InTable => Class != SoilStdClass.None && double.IsFinite(C) && double.IsFinite(Phi);
}

public static class SoilStandard
{
    public const string Std = "TCVN 9362:2012";
    public const double GammaW = 10.0;
    private const double N_ = double.NaN;

    // ---------------- Bảng giá trị tiêu chuẩn c (kPa) và φ (độ) của đất cát theo e ----------------
    private static readonly double[] SandE = { 0.45, 0.55, 0.65, 0.75 };
    private static readonly Dictionary<SoilStdClass, (double[] C, double[] Phi, double[] E)> Sand = new()
    {
        [SoilStdClass.GravelCoarseSand] = (new[] { 2.0, 1, 0, N_ }, new[] { 43.0, 40, 38, N_ }, new[] { 50.0, 40, 30, N_ }),
        [SoilStdClass.MediumSand] = (new[] { 3.0, 2, 1, N_ }, new[] { 40.0, 38, 35, N_ }, new[] { 50.0, 40, 30, N_ }),
        [SoilStdClass.FineSand] = (new[] { 6.0, 4, 2, 0 }, new[] { 38.0, 36, 32, 28 }, new[] { 48.0, 38, 28, 18 }),
        [SoilStdClass.SiltySand] = (new[] { 8.0, 6, 4, 2 }, new[] { 36.0, 34, 30, 26 }, new[] { 39.0, 28, 18, 11 }),
    };

    // ---------------- Bảng c (kPa) / φ (độ) của đất dính kỷ Đệ tứ theo IL (dải) và e ----------------
    private static readonly double[] ClayE = { 0.45, 0.55, 0.65, 0.75, 0.85, 0.95, 1.05 };
    private sealed record Band(double IL0, double IL1, double[] C, double[] Phi);
    private static readonly Dictionary<SoilStdClass, Band[]> ClayCPhi = new()
    {
        [SoilStdClass.SandyLoam] = new[]
        {
            new Band(0, 0.25, new[] { 21.0, 17, 15, 13, N_, N_, N_ }, new[] { 30.0, 29, 27, 24, N_, N_, N_ }),
            new Band(0.25, 0.75, new[] { 19.0, 15, 13, 11, 9, N_, N_ }, new[] { 28.0, 26, 24, 21, 18, N_, N_ }),
        },
        [SoilStdClass.Loam] = new[]
        {
            new Band(0, 0.25, new[] { 47.0, 37, 31, 25, 22, 19, N_ }, new[] { 26.0, 25, 24, 23, 22, 20, N_ }),
            new Band(0.25, 0.5, new[] { 39.0, 34, 28, 23, 18, 15, N_ }, new[] { 24.0, 23, 22, 21, 19, 17, N_ }),
            new Band(0.5, 0.75, new[] { N_, N_, 25.0, 20, 16, 14, 12 }, new[] { N_, N_, 19.0, 18, 16, 14, 12 }),
        },
        [SoilStdClass.Clay] = new[]
        {
            new Band(0, 0.25, new[] { N_, 81.0, 68, 54, 47, 41, 36 }, new[] { N_, 21.0, 20, 19, 18, 16, 14 }),
            new Band(0.25, 0.5, new[] { N_, N_, 57.0, 50, 43, 37, 32 }, new[] { N_, N_, 18.0, 17, 16, 14, 11 }),
            new Band(0.5, 0.75, new[] { N_, N_, 45.0, 41, 36, 33, 29 }, new[] { N_, N_, 15.0, 14, 12, 10, 7 }),
        },
    };

    // ---------------- Bảng mô đun biến dạng E (MPa) của đất dính kỷ Đệ tứ ----------------
    private static readonly double[] ClayEE = { 0.35, 0.45, 0.55, 0.65, 0.75, 0.85, 0.95, 1.05, 1.2 };
    private sealed record EBand(double IL0, double IL1, double[] E);
    private static readonly Dictionary<SoilStdClass, EBand[]> ClayMod = new()
    {
        [SoilStdClass.SandyLoam] = new[] { new EBand(0, 0.75, new[] { N_, 32.0, 24, 16, 10, 7, N_, N_, N_ }) },
        [SoilStdClass.Loam] = new[]
        {
            new EBand(0, 0.25, new[] { N_, 34.0, 27, 22, 17, 14, 11, N_, N_ }),
            new EBand(0.25, 0.5, new[] { N_, 32.0, 25, 19, 14, 11, 8, N_, N_ }),
            new EBand(0.5, 0.75, new[] { N_, N_, N_, 17.0, 12, 8, 6, 5, N_ }),
        },
        [SoilStdClass.Clay] = new[]
        {
            new EBand(0, 0.25, new[] { N_, N_, 28.0, 24, 21, 18, 15, 12, N_ }),
            new EBand(0.25, 0.5, new[] { N_, N_, N_, 21.0, 18, 15, 12, 9, N_ }),
            new EBand(0.5, 0.75, new[] { N_, N_, N_, N_, 15.0, 12, 9, 7, N_ }),
        },
    };

    // ---------------- Áp lực tính toán quy ước R0 (kPa) ----------------
    /// <summary>Đất dính: các mốc e và R0 tại IL = 0 / IL = 1 (nội suy tuyến tính theo e và IL).</summary>
    private static readonly Dictionary<SoilStdClass, (double e, double Il0, double Il1)[]> ClayR0 = new()
    {
        [SoilStdClass.SandyLoam] = new[] { (0.5, 300.0, 300.0), (0.7, 250.0, 200.0) },
        [SoilStdClass.Loam] = new[] { (0.5, 300.0, 250.0), (0.7, 250.0, 180.0), (1.0, 200.0, 100.0) },
        [SoilStdClass.Clay] = new[] { (0.5, 600.0, 400.0), (0.6, 500.0, 300.0), (0.8, 300.0, 200.0), (1.1, 250.0, 100.0) },
    };

    /// <summary>Tỷ trọng hạt Gs điển hình (để tính γ từ e, Sr)</summary>
    public static double Gs(SoilStdClass c) => c switch
    {
        SoilStdClass.GravelCoarseSand or SoilStdClass.MediumSand => 2.65,
        SoilStdClass.FineSand => 2.66,
        SoilStdClass.SiltySand => 2.67,
        SoilStdClass.SandyLoam => 2.68,
        SoilStdClass.Loam => 2.70,
        SoilStdClass.Clay => 2.74,
        _ => 2.68
    };

    public static string ClassName(SoilStdClass c) => c switch
    {
        SoilStdClass.GravelCoarseSand => "Cát sỏi, cát thô",
        SoilStdClass.MediumSand => "Cát hạt vừa",
        SoilStdClass.FineSand => "Cát hạt nhỏ",
        SoilStdClass.SiltySand => "Cát bụi",
        SoilStdClass.SandyLoam => "Cát pha",
        SoilStdClass.Loam => "Sét pha",
        SoilStdClass.Clay => "Sét",
        _ => "Không có trong bảng tra"
    };

    /// <summary>Nhóm bảng tra của một lớp đất.</summary>
    public static SoilStdClass ClassOf(SoilKind k, SandGrain g) => k switch
    {
        SoilKind.Sand => g switch
        {
            SandGrain.Coarse or SandGrain.Gravelly => SoilStdClass.GravelCoarseSand,
            SandGrain.Fine => SoilStdClass.FineSand,
            SandGrain.Silty => SoilStdClass.SiltySand,
            _ => SoilStdClass.MediumSand
        },
        SoilKind.Gravel => SoilStdClass.GravelCoarseSand,
        SoilKind.SandyClay => SoilStdClass.SandyLoam,
        SoilKind.SiltyClay => SoilStdClass.Loam,
        SoilKind.Clay => SoilStdClass.Clay,
        _ => SoilStdClass.None
    };

    public static bool IsSandClass(SoilStdClass c) => c is SoilStdClass.GravelCoarseSand or SoilStdClass.MediumSand or SoilStdClass.FineSand or SoilStdClass.SiltySand;

    /// <summary>Trạng thái chặt của cát theo e (TCVN 9362:2012): chặt / chặt vừa / rời.</summary>
    public static string SandDensity(SoilStdClass c, double e)
    {
        double a, b;
        switch (c)
        {
            case SoilStdClass.GravelCoarseSand: case SoilStdClass.MediumSand: a = 0.55; b = 0.70; break;
            case SoilStdClass.FineSand: a = 0.60; b = 0.75; break;
            default: a = 0.60; b = 0.80; break;
        }
        return e < a ? "chặt" : e <= b ? "chặt vừa" : "rời";
    }

    /// <summary>Trạng thái đất dính theo độ sệt IL (TCVN 9362:2012).</summary>
    public static string ClayConsistency(SoilStdClass c, double il)
    {
        if (c == SoilStdClass.SandyLoam) return il < 0 ? "cứng" : il <= 1 ? "dẻo" : "chảy";
        if (il < 0) return "cứng";
        if (il <= 0.25) return "nửa cứng";
        if (il <= 0.5) return "dẻo cứng";
        if (il <= 0.75) return "dẻo mềm";
        if (il <= 1) return "dẻo chảy";
        return "chảy";
    }

    /// <summary>Độ ẩm của cát theo Sr: ít ẩm (≤ 0,5), ẩm (0,5–0,8), bão hoà nước (&gt; 0,8).</summary>
    public static string Moisture(double sr) => sr <= 0.5 ? "ít ẩm" : sr <= 0.8 ? "ẩm" : "bão hoà nước";

    // ------------------------------------------------------------------
    /// <summary>Danh mục lớp đất cấu tạo tiêu chuẩn (chọn nhanh: loại đất + trạng thái → tra bảng).</summary>
    public static readonly IReadOnlyList<StdSoil> Catalog = new List<StdSoil>
    {
        new() { Name = "Cát sỏi, cát thô – chặt", Kind = SoilKind.Sand, Grain = SandGrain.Coarse, State = "chặt", E0 = 0.50, Sr = 0.7, N = 40 },
        new() { Name = "Cát sỏi, cát thô – chặt vừa", Kind = SoilKind.Sand, Grain = SandGrain.Coarse, State = "chặt vừa", E0 = 0.62, Sr = 0.7, N = 22 },
        new() { Name = "Cát hạt vừa – chặt", Kind = SoilKind.Sand, Grain = SandGrain.Medium, State = "chặt", E0 = 0.50, Sr = 0.7, N = 35 },
        new() { Name = "Cát hạt vừa – chặt vừa", Kind = SoilKind.Sand, Grain = SandGrain.Medium, State = "chặt vừa", E0 = 0.62, Sr = 0.7, N = 20 },
        new() { Name = "Cát hạt nhỏ – chặt", Kind = SoilKind.Sand, Grain = SandGrain.Fine, State = "chặt", E0 = 0.55, Sr = 0.7, N = 32 },
        new() { Name = "Cát hạt nhỏ – chặt vừa", Kind = SoilKind.Sand, Grain = SandGrain.Fine, State = "chặt vừa", E0 = 0.68, Sr = 0.7, N = 18 },
        new() { Name = "Cát hạt nhỏ – rời", Kind = SoilKind.Sand, Grain = SandGrain.Fine, State = "rời", E0 = 0.78, Sr = 0.7, N = 7, Note = "Cát rời: bảng không cho R0 – không dùng làm nền khi chưa thí nghiệm" },
        new() { Name = "Cát bụi – chặt", Kind = SoilKind.Sand, Grain = SandGrain.Silty, State = "chặt", E0 = 0.55, Sr = 0.7, N = 28 },
        new() { Name = "Cát bụi – chặt vừa", Kind = SoilKind.Sand, Grain = SandGrain.Silty, State = "chặt vừa", E0 = 0.70, Sr = 0.7, N = 14 },
        new() { Name = "Cát bụi – rời", Kind = SoilKind.Sand, Grain = SandGrain.Silty, State = "rời", E0 = 0.82, Sr = 0.8, N = 5, Note = "Cát rời: bảng không cho R0 – không dùng làm nền khi chưa thí nghiệm" },
        new() { Name = "Cát pha – cứng", Kind = SoilKind.SandyClay, State = "cứng", E0 = 0.55, IL = -0.1, Sr = 0.85, N = 15 },
        new() { Name = "Cát pha – dẻo", Kind = SoilKind.SandyClay, State = "dẻo", E0 = 0.65, IL = 0.5, Sr = 0.9, N = 8 },
        new() { Name = "Sét pha – cứng", Kind = SoilKind.SiltyClay, State = "cứng", E0 = 0.60, IL = -0.1, Sr = 0.9, N = 25 },
        new() { Name = "Sét pha – nửa cứng", Kind = SoilKind.SiltyClay, State = "nửa cứng", E0 = 0.65, IL = 0.15, Sr = 0.9, N = 18 },
        new() { Name = "Sét pha – dẻo cứng", Kind = SoilKind.SiltyClay, State = "dẻo cứng", E0 = 0.75, IL = 0.38, Sr = 0.92, N = 10 },
        new() { Name = "Sét pha – dẻo mềm", Kind = SoilKind.SiltyClay, State = "dẻo mềm", E0 = 0.85, IL = 0.62, Sr = 0.95, N = 5 },
        new() { Name = "Sét pha – dẻo chảy", Kind = SoilKind.SiltyClay, State = "dẻo chảy", E0 = 0.95, IL = 0.85, Sr = 0.97, N = 3, Note = "IL > 0,75: ngoài bảng tra – đất yếu, phải thí nghiệm" },
        new() { Name = "Sét – cứng", Kind = SoilKind.Clay, State = "cứng", E0 = 0.65, IL = -0.1, Sr = 0.92, N = 30 },
        new() { Name = "Sét – nửa cứng", Kind = SoilKind.Clay, State = "nửa cứng", E0 = 0.75, IL = 0.15, Sr = 0.93, N = 20 },
        new() { Name = "Sét – dẻo cứng", Kind = SoilKind.Clay, State = "dẻo cứng", E0 = 0.85, IL = 0.38, Sr = 0.95, N = 11 },
        new() { Name = "Sét – dẻo mềm", Kind = SoilKind.Clay, State = "dẻo mềm", E0 = 0.95, IL = 0.62, Sr = 0.97, N = 6 },
        new() { Name = "Sét – dẻo chảy", Kind = SoilKind.Clay, State = "dẻo chảy", E0 = 1.05, IL = 0.85, Sr = 0.98, N = 3, Note = "IL > 0,75: ngoài bảng tra – đất yếu, phải thí nghiệm" },
        new() { Name = "Sỏi sạn, cuội lẫn cát", Kind = SoilKind.Gravel, Grain = SandGrain.Coarse, State = "chặt", E0 = 0.50, Sr = 0.6, N = 45, Note = "Tra theo nhóm cát sỏi, cát thô" },
        new() { Name = "Đất đắp", Kind = SoilKind.Fill, State = "", E0 = 0.9, IL = -1, N = 4, Note = "Không có trong bảng tra – giá trị tham khảo, phải khảo sát" },
        new() { Name = "Bùn sét, bùn sét pha", Kind = SoilKind.Organic, State = "chảy", E0 = 1.5, IL = 1.1, Sr = 1, N = 1, Note = "Không có trong bảng tra – đất rất yếu, phải khảo sát" },
        new() { Name = "Đá phong hoá", Kind = SoilKind.Rock, State = "", E0 = 0.3, N = 50, Note = "Không có trong bảng tra – phải khảo sát" },
    };

    public static StdSoil Find(string name) =>
        string.IsNullOrWhiteSpace(name) ? null : Catalog.FirstOrDefault(s => s.Name == name) ?? Catalog.FirstOrDefault(s => LocationDb.Norm(s.Name) == LocationDb.Norm(name));

    /// <summary>Lớp tiêu chuẩn phù hợp nhất với loại đất / e / IL của lớp.</summary>
    public static StdSoil Nearest(SoilLayer l)
    {
        if (l == null) return Find("Sét pha – dẻo cứng");
        var same = Catalog.Where(s => s.Kind == l.Kind && (l.Kind != SoilKind.Sand || s.Grain == l.Grain)).ToList();
        if (same.Count == 0) same = Catalog.Where(s => s.Kind == l.Kind).ToList();
        if (same.Count == 0) return Find("Sét pha – dẻo cứng");
        return same.OrderBy(s => l.IL >= -0.5 && s.IL >= -0.5 ? Math.Abs(s.IL - l.IL) : 0).ThenBy(s => l.VoidRatio > 0 ? Math.Abs(s.E0 - l.VoidRatio) : 0).First();
    }

    // ------------------------------------------------------------------
    private static double Interp(double[] xs, double[] ys, double x, out bool clamped, out bool blank)
    {
        clamped = false; blank = false;
        // bỏ ô trống ("–"): chỉ nội suy trên các ô có số liệu
        var pts = xs.Zip(ys).Where(t => double.IsFinite(t.Second)).ToList();
        if (pts.Count == 0) { blank = true; return double.NaN; }
        if (x <= pts[0].First) { clamped = x < pts[0].First - 1e-9; if (clamped && xs[0] < pts[0].First - 1e-9 && x >= xs[0]) blank = true; return pts[0].Second; }
        if (x >= pts[^1].First) { clamped = x > pts[^1].First + 1e-9; if (clamped && xs[^1] > pts[^1].First + 1e-9 && x <= xs[^1]) blank = true; return pts[^1].Second; }
        for (int i = 0; i + 1 < pts.Count; i++)
            if (x <= pts[i + 1].First)
            {
                double t = (x - pts[i].First) / (pts[i + 1].First - pts[i].First);
                return pts[i].Second + t * (pts[i + 1].Second - pts[i].Second);
            }
        return pts[^1].Second;
    }

    private static string F(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);

    /// <summary>Tra bảng TCVN 9362:2012 cho loại đất (kind, grain), hệ số rỗng e, độ sệt IL, độ bão hoà Sr.</summary>
    public static StdSoilResult Lookup(SoilKind kind, SandGrain grain, double e, double il, double sr = 0.8)
    {
        var r = new StdSoilResult { Class = ClassOf(kind, grain) };
        var c = r.Class;
        if (c == SoilStdClass.None)
        {
            r.Warnings.Add($"{SoilLayer.KindName(kind, grain)}: không có trong bảng tra {Std} – dùng giá trị tham khảo, phải khảo sát / thí nghiệm");
            return r;
        }
        if (!(e > 0)) e = Catalog.FirstOrDefault(s => ClassOf(s.Kind, s.Grain) == c)?.E0 ?? 0.7;
        if (!(sr > 0)) sr = IsSandClass(c) ? 0.7 : 0.9;
        double gs = Gs(c);
        r.Gamma = Math.Round(GammaW * (gs + Math.Min(sr, 1) * e) / (1 + e), 2);
        r.GammaSat = Math.Round(GammaW * (gs + e) / (1 + e), 2);
        r.Sources.Add($"γ = γw·(Gs + Sr·e)/(1 + e) = 10·({F(gs)} + {F(Math.Min(sr, 1))}·{F(e)})/(1 + {F(e)}) = {F(r.Gamma)} kN/m³; γsat = 10·(Gs + e)/(1 + e) = {F(r.GammaSat)} kN/m³");
        if (IsSandClass(c))
        {
            var t = Sand[c];
            r.State = SandDensity(c, e) + ", " + Moisture(sr);
            r.C = Interp(SandE, t.C, e, out bool cl1, out bool bl1);
            r.Phi = Interp(SandE, t.Phi, e, out bool cl2, out _);
            r.E = Interp(SandE, t.E, e, out bool cl3, out _);
            if (cl1 || cl2 || cl3 || bl1) r.Warnings.Add($"e = {F(e)} ngoài phạm vi bảng tra cát ({ClassName(c)}: e = 0,45…{(double.IsFinite(t.C[^1]) ? "0,75" : "0,65")}) – lấy giá trị biên, nên thí nghiệm");
            r.Sources.Add($"{Std} Phụ lục – bảng c, φ, E của đất cát ({ClassName(c)}, e = {F(e)}): c = {F(r.C, "0.#")} kPa, φ = {F(r.Phi, "0.#")}°, E = {F(r.E, "0.#")} MPa");
            // R0 cát
            string dens = SandDensity(c, e);
            double R0 = double.NaN;
            if (dens != "rời")
            {
                bool dense = dens == "chặt";
                R0 = c switch
                {
                    SoilStdClass.GravelCoarseSand => dense ? 600 : 500,
                    SoilStdClass.MediumSand => dense ? 500 : 400,
                    SoilStdClass.FineSand => sr <= 0.8 ? (dense ? 400 : 300) : (dense ? 300 : 200),
                    _ => sr <= 0.5 ? (dense ? 300 : 250) : sr <= 0.8 ? (dense ? 200 : 150) : (dense ? 150 : 100)
                };
                r.Sources.Add($"{Std} – R0 đất cát ({ClassName(c)} {dens}, {Moisture(sr)}): R0 = {F(R0, "0")} kPa");
            }
            else r.Warnings.Add($"Cát {ClassName(c).ToLowerInvariant()} trạng thái rời: bảng tra không cho R0 – phải thí nghiệm (không đặt móng trực tiếp khi chưa đầm chặt / gia cố)");
            r.R0 = R0;
            return r;
        }
        // đất dính
        r.State = ClayConsistency(c, il);
        double ilT = il;
        if (il < 0) ilT = 0;
        if (il > 0.75)
        {
            r.Warnings.Add($"IL = {F(il)} > 0,75 ({r.State}): ngoài bảng tra c, φ, E – lấy theo dải 0,5 < IL ≤ 0,75 (thiên về không an toàn), BẮT BUỘC thí nghiệm; đất yếu – xem xét móng cọc / gia cố");
            ilT = 0.75;
        }
        var bands = ClayCPhi[c];
        var band = bands.FirstOrDefault(b => ilT >= b.IL0 - 1e-9 && ilT <= b.IL1 + 1e-9 && !(ilT <= b.IL0 + 1e-9 && b.IL0 > 0)) ?? bands[^1];
        r.C = Interp(ClayE, band.C, e, out bool c1, out bool b1);
        r.Phi = Interp(ClayE, band.Phi, e, out bool c2, out _);
        var eb = ClayMod[c].FirstOrDefault(b => ilT >= b.IL0 - 1e-9 && ilT <= b.IL1 + 1e-9 && !(ilT <= b.IL0 + 1e-9 && b.IL0 > 0)) ?? ClayMod[c][^1];
        r.E = Interp(ClayEE, eb.E, e, out bool c3, out _);
        if (c1 || c2 || c3 || b1) r.Warnings.Add($"e = {F(e)} ngoài phạm vi bảng tra ({ClassName(c)}, {F(band.IL0)} < IL ≤ {F(band.IL1)}) – lấy giá trị biên của bảng, nên thí nghiệm");
        if (il < 0) r.Sources.Add("IL < 0 (cứng): tra theo dải 0 ≤ IL ≤ 0,25");
        r.Sources.Add($"{Std} Phụ lục – bảng c, φ của đất dính ({ClassName(c)}, {F(band.IL0)} < IL ≤ {F(band.IL1)}, e = {F(e)}): c = {F(r.C, "0.#")} kPa, φ = {F(r.Phi, "0.#")}°");
        r.Sources.Add($"{Std} Phụ lục – bảng E của đất dính kỷ Đệ tứ ({ClassName(c)}, e = {F(e)}): E = {F(r.E, "0.#")} MPa");
        // R0 đất dính: nội suy theo e và IL (0…1)
        var rt = ClayR0[c];
        double il01 = Math.Clamp(il, 0, 1);
        double R00 = Interp(rt.Select(x => x.e).ToArray(), rt.Select(x => x.Il0).ToArray(), e, out bool rc, out _);
        double R01 = Interp(rt.Select(x => x.e).ToArray(), rt.Select(x => x.Il1).ToArray(), e, out _, out _);
        r.R0 = Math.Round(R00 + il01 * (R01 - R00), 0);
        if (rc) r.Warnings.Add($"e = {F(e)} ngoài phạm vi bảng R0 ({ClassName(c)}: e = {F(rt[0].e)}…{F(rt[^1].e)}) – lấy giá trị biên");
        if (il > 1) r.Warnings.Add("IL > 1 (chảy): không dùng R0 bảng tra");
        r.Sources.Add($"{Std} – R0 đất dính ({ClassName(c)}, e = {F(e)}, IL = {F(il01)}): R0 = {F(r.R0, "0")} kPa (nội suy theo e và IL)");
        return r;
    }

    /// <summary>Tra bảng cho lớp đất (theo loại đất, e, IL, Sr của lớp).</summary>
    public static StdSoilResult Lookup(SoilLayer l) => Lookup(l.Kind, l.Grain, l.VoidRatio, l.IL, l.Sr);

    /// <summary>Gán chỉ tiêu tra bảng vào lớp đất (Source = Table): γ, γsat, φ, c, E, R0. Lớp không có trong bảng: giá trị tham khảo (SoilPresets).
    /// Trả về kết quả tra (nguồn, cảnh báo).</summary>
    public static StdSoilResult ApplyTo(SoilLayer l)
    {
        if (l == null) return null;
        var r = Lookup(l);
        l.Source = SoilPropSource.Table;
        if (r.InTable)
        {
            l.C = Math.Round(r.C, 1); l.Phi = Math.Round(r.Phi, 1);
            if (double.IsFinite(r.E)) l.E = Math.Round(r.E, 1);
            l.Gamma = r.Gamma; l.GammaSat = r.GammaSat;
            l.R0 = double.IsFinite(r.R0) ? r.R0 : 0;
        }
        else
        {
            var pr = SoilPresets.Nearest(l);
            l.Gamma = pr.Gamma; l.GammaSat = 0; l.Phi = pr.Phi; l.C = pr.C; l.E = pr.E; l.R0 = 0;
            r.Sources.Add($"Giá trị tham khảo \"{pr.Name}\" (không có trong bảng tra {Std})");
        }
        return r;
    }

    /// <summary>Tạo lớp đất từ lớp cấu tạo tiêu chuẩn (tra bảng).</summary>
    public static SoilLayer NewLayer(StdSoil s, double top, double bottom)
    {
        var l = new SoilLayer { Top = top, Bottom = bottom };
        ApplyStd(l, s);
        return l;
    }

    /// <summary>Gán loại đất + trạng thái tiêu chuẩn vào lớp (giữ độ sâu, tên nếu đã có) rồi tra bảng.</summary>
    public static StdSoilResult ApplyStd(SoilLayer l, StdSoil s)
    {
        if (l == null || s == null) return null;
        if (string.IsNullOrWhiteSpace(l.Name) || Catalog.Any(c => c.Name == l.Name) || l.Name == SoilLayer.KindName(l.Kind, l.Grain)) l.Name = s.Name;
        l.Kind = s.Kind; l.Grain = s.Grain;
        l.VoidRatio = s.E0; l.IL = s.IL; l.Sr = s.Sr; l.N = s.N;
        l.StdName = s.Name;
        var r = ApplyTo(l);
        l.Description = "Chỉ tiêu theo bảng tra " + Std + " (lớp tiêu chuẩn \"" + s.Name + "\") – thay bằng kết quả thí nghiệm khi có hố khoan";
        return r;
    }

    /// <summary>Tóm tắt một dòng (bảng chọn lớp tiêu chuẩn).</summary>
    public static string Summary(StdSoil s)
    {
        var r = Lookup(s.Kind, s.Grain, s.E0, s.IL, s.Sr);
        if (!r.InTable) return "Không có trong bảng tra – phải khảo sát";
        return $"e = {F(s.E0)}{(s.IL >= -0.5 ? ", IL = " + F(s.IL) : "")}: γ = {F(r.Gamma, "0.0")}, φ = {F(r.Phi, "0.#")}°, c = {F(r.C, "0.#")} kPa, E = {F(r.E, "0.#")} MPa"
               + (double.IsFinite(r.R0) ? $", R0 = {F(r.R0, "0")} kPa" : "");
    }

    /// <summary>Gán chỉ tiêu tra bảng vào SoilSettings (nền nhập tay khi chưa có hố khoan): φ, c, γ, E; ktc = 1,1.</summary>
    public static StdSoilResult ApplyTo(SoilSettings s, StdSoil std)
    {
        if (s == null || std == null) return null;
        var l = NewLayer(std, 0, 1);
        var r = Lookup(l);
        s.Phi = l.Phi; s.C = l.C; s.Gamma = l.Gamma; s.E = l.E;
        s.StdName = std.Name;
        s.Ktc = 1.1;
        s.ROverride = 0;
        return r;
    }
}
