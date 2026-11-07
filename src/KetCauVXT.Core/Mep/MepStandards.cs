using System.Globalization;

namespace KetCauVXT.Mep;

// =====================================================================
// Bảng thông số điện nước – ĐHKK theo tiêu chuẩn Việt Nam (một nơi duy nhất).
// Mỗi giá trị có nhãn nguồn "Theo TCVN xxx Bảng y" và có thể "tuỳ chỉnh" (ghi đè) trong Cài đặt điện nước.
// Verified = false: giá trị trích theo thực hành / tài liệu thứ cấp – cần đối chiếu bản gốc tiêu chuẩn (đánh dấu (*)).
// Không đọc / gửi dữ liệu ra ngoài – bảng nằm trong mã nguồn.
// =====================================================================

/// <summary>Loại công trình (chọn hệ số α của công thức lưu lượng cấp nước TCVN 4513).</summary>
public enum MepBuildingKind { NhaO, NhaTre, BenhVien, CoQuan, TruongHoc, AnDuong, KhachSan }

/// <summary>Tuyến ống gen điện: đi trong sàn / trần (khuyến nghị) hoặc đi ngang trong tường (cách cũ).</summary>
public enum ConduitRouteMode { TranSan, TrongTuong }

/// <summary>Một thông số tiêu chuẩn.</summary>
public sealed record MepStdItem(string Key, string Group, string Name, double Value, string Unit, string Source, bool Verified = true)
{
    /// <summary>Nhãn nguồn hiển thị (có (*) khi cần đối chiếu bản gốc).</summary>
    public string Label => "Theo " + Source + (Verified ? "" : " (*)");
}

/// <summary>Giá trị tuỳ chỉnh (ghi đè giá trị tiêu chuẩn).</summary>
public sealed class MepStdOverride
{
    public string Key { get; set; } = "";
    public double Value { get; set; }
}

/// <summary>Lựa chọn áp dụng tiêu chuẩn của dự án (MepSettings.Standards).</summary>
public class MepStdOptions
{
    /// <summary>Loại công trình (TCVN 4513: nhà ở dùng q = 0,2·a·√N + K·N; công trình công cộng q = 0,2·α·√N)</summary>
    public MepBuildingKind Building { get; set; } = MepBuildingKind.NhaO;
    /// <summary>Tiêu chuẩn dùng nước để chọn hệ số a (l/người·ngày đêm). 125 → a = 2,15.</summary>
    public double WaterNorm { get; set; } = 125;
    /// <summary>Nhiệt độ môi trường quanh ống gen (°C) – hệ số hiệu chỉnh IEC 60364-5-52 Bảng B.52.14 (chuẩn 30 °C)</summary>
    public double AmbientC { get; set; } = 30;
    /// <summary>Số mạch đi chung một ống / bó – hệ số nhóm IEC 60364-5-52 Bảng B.52.17</summary>
    public int GroupedCircuits { get; set; } = 1;
    /// <summary>Địa phương lấy thông số khí hậu ngoài nhà (TCVN 5687:2010 Phụ lục B): "Hà Nội" / "TP. Hồ Chí Minh"</summary>
    public string Climate { get; set; } = MepStandards.HaNoi;
    /// <summary>Giá trị tuỳ chỉnh theo khoá</summary>
    public List<MepStdOverride> Overrides { get; set; } = new();
}

public static class MepStandards
{
    public const string HaNoi = "Hà Nội", Hcm = "TP. Hồ Chí Minh";
    public const string T9206 = "TCVN 9206:2012", T9207 = "TCVN 9207:2012", Iec = "IEC 60364-5-52", T4513 = "TCVN 4513:1988",
        T4474 = "TCVN 4474:1987", T5687 = "TCVN 5687:2010";
    public const string GDien = "Điện", GNuoc = "Cấp nước", GThoat = "Thoát nước", GDhkk = "ĐHKK", GOngGen = "Ống gen trong sàn";

    /// <summary>Danh mục thông số (khoá → giá trị, đơn vị, nguồn).</summary>
    public static readonly IReadOnlyList<MepStdItem> Items = new List<MepStdItem>
    {
        // ---- điện ----
        new("dien.h_ocam", GDien, "Cao độ ổ cắm (nhà ở)", 0.4, "m", T9206 + " mục đặt ổ cắm", false),
        new("dien.h_congtac", GDien, "Cao độ công tắc", 1.25, "m", T9206 + " mục đặt công tắc", false),
        new("dien.h_tudien", GDien, "Cao độ tủ / bảng điện", 1.5, "m", T9206 + " mục đặt bảng điện", false),
        new("dien.kc_cs", GDien, "Hệ số nhu cầu Kc chiếu sáng", 0.9, "", T9206 + " (hệ số yêu cầu)", false),
        new("dien.kc_oc", GDien, "Hệ số nhu cầu Kc ổ cắm", 0.5, "", T9206 + " (hệ số yêu cầu)", false),
        new("dien.kc_ac", GDien, "Hệ số nhu cầu Kc điều hoà", 0.8, "", T9206 + " (hệ số yêu cầu)", false),
        new("dien.kc_nong", GDien, "Hệ số nhu cầu Kc bình nóng lạnh", 0.7, "", T9206 + " (hệ số yêu cầu)", false),
        new("dien.max_cs", GDien, "Số điểm chiếu sáng / mạch (thiết kế)", 10, "điểm", T9206 + " (giới hạn ≤ 20 bóng / nhóm) – chọn 10", false),
        new("dien.max_oc", GDien, "Số ổ cắm / mạch (thiết kế)", 8, "điểm", T9207 + " (thực hành)", false),
        new("dien.s_min_cs", GDien, "Tiết diện dây tối thiểu chiếu sáng (Cu)", 1.5, "mm²", T9207 + " (dây Cu trong nhà)", false),
        new("dien.s_min_oc", GDien, "Tiết diện dây tối thiểu ổ cắm / điều hoà (Cu)", 2.5, "mm²", T9207 + " (dây Cu trong nhà)", false),
        new("dien.cos", GDien, "Hệ số công suất cosφ", 0.85, "", T9206 + " (phụ tải sinh hoạt)", false),
        new("dien.du_cs", GDien, "Sụt áp cho phép mạch chiếu sáng", 3, "%", Iec + " Phụ lục G Bảng G.52.1"),
        new("dien.du_khac", GDien, "Sụt áp cho phép mạch khác", 5, "%", Iec + " Phụ lục G Bảng G.52.1"),
        new("dien.rho", GDien, "Điện trở suất Cu ở nhiệt độ làm việc", 0.0225, "Ω·mm²/m", Iec + " Phụ lục G (1,25·ρ20)"),
        new("dien.fill", GDien, "Hệ số lấp đầy ống gen", 0.40, "", "TCVN 9208:2012 / thực hành (≤ 40 %)", false),
        // ---- cấp nước ----
        new("cap.v_max", GNuoc, "Vận tốc lớn nhất ống nhánh / phân phối", 1.5, "m/s", T4513 + " mục tính toán mạng lưới", false),
        new("cap.q_nguoi", GNuoc, "Tiêu chuẩn dùng nước (bể / bơm)", 150, "l/người·ngđ", T4513 + " Bảng 1 (100…150 nhà có tắm, xí)", false),
        new("cap.kngay", GNuoc, "Hệ số không điều hoà ngày Kngày", 1.3, "", T4513 + " (1,2…1,4)", false),
        new("cap.n_lavabo", GNuoc, "Đương lượng chậu rửa mặt", 0.33, "", T4513 + " Bảng đương lượng thiết bị", false),
        new("cap.n_bep", GNuoc, "Đương lượng chậu rửa bếp / vòi", 1.0, "", T4513 + " Bảng đương lượng thiết bị", false),
        new("cap.n_sen", GNuoc, "Đương lượng vòi sen", 0.67, "", T4513 + " Bảng đương lượng thiết bị", false),
        new("cap.n_xi", GNuoc, "Đương lượng xí bệt có két", 0.5, "", T4513 + " Bảng đương lượng thiết bị", false),
        new("cap.n_bontam", GNuoc, "Đương lượng bồn tắm", 1.0, "", T4513 + " Bảng đương lượng thiết bị", false),
        new("cap.n_tieu", GNuoc, "Đương lượng tiểu treo", 0.17, "", T4513 + " Bảng đương lượng thiết bị", false),
        // ---- thoát nước ----
        new("thoat.d_xi", GThoat, "Ø nhỏ nhất ống có xí (danh nghĩa 100 → uPVC Ø110)", 110, "mm", T4474 + " mục ống thoát xí", false),
        new("thoat.i_50", GThoat, "Độ dốc tiêu chuẩn Ø50", 0.035, "", T4474 + " Bảng độ dốc ống thoát", false),
        new("thoat.imin_50", GThoat, "Độ dốc nhỏ nhất Ø50", 0.025, "", T4474 + " Bảng độ dốc ống thoát", false),
        new("thoat.i_100", GThoat, "Độ dốc tiêu chuẩn Ø100", 0.02, "", T4474 + " Bảng độ dốc ống thoát", false),
        new("thoat.imin_100", GThoat, "Độ dốc nhỏ nhất Ø100", 0.012, "", T4474 + " Bảng độ dốc ống thoát", false),
        new("thoat.h_thonghoi", GThoat, "Ống thông hơi nhô trên mái", 0.7, "m", T4474 + " mục ống thông hơi", false),
        // ---- ĐHKK ----
        new("dhkk.t_trong", GDhkk, "Nhiệt độ trong nhà mùa hè (nghỉ ngơi tĩnh tại 25…28)", 25, "°C", T5687 + " Phụ lục A Bảng A.1"),
        new("dhkk.phi_trong", GDhkk, "Độ ẩm trong nhà mùa hè (60…70)", 60, "%", T5687 + " Phụ lục A Bảng A.1"),
        new("dhkk.t_hn", GDhkk, "Nhiệt độ ngoài nhà Hà Nội (m = 35 h/năm)", 37.8, "°C", T5687 + " Phụ lục B"),
        new("dhkk.phi_hn", GDhkk, "Độ ẩm ngoài nhà Hà Nội (m = 35 h/năm)", 53.4, "%", T5687 + " Phụ lục B"),
        new("dhkk.t_hcm", GDhkk, "Nhiệt độ ngoài nhà TP.HCM (m = 35 h/năm)", 36.8, "°C", T5687 + " Phụ lục B"),
        new("dhkk.phi_hcm", GDhkk, "Độ ẩm ngoài nhà TP.HCM (m = 35 h/năm)", 56.0, "%", T5687 + " Phụ lục B"),
        new("dhkk.gio_tuoi", GDhkk, "Gió tươi tối thiểu phòng ở / văn phòng", 25, "m³/h·người", T5687 + " Phụ lục F", false),
        new("dhkk.v_chinh", GDhkk, "Vận tốc gió ống chính (5…8)", 6, "m/s", T5687 + " (công trình công cộng – thực hành)", false),
        new("dhkk.v_nhanh", GDhkk, "Vận tốc gió ống nhánh (3…5)", 4, "m/s", T5687 + " (công trình công cộng – thực hành)", false),
        // ---- ống gen trong sàn ----
        new("gen.san_min", GOngGen, "Sàn dày tối thiểu cho ống gen qua dầm", 0.10, "m", T9207 + " / TCVN 5574:2018 (cấu tạo)", false),
        new("gen.d_qua_dam", GOngGen, "Ø ống gen lớn nhất được đi qua dầm trong chiều dày sàn", 25, "mm", T9207 + " (thực hành)", false),
        new("gen.ty_san", GOngGen, "Ø ống gen / chiều dày sàn lớn nhất", 1.0 / 3.0, "", "TCVN 5574:2018 (cấu tạo – ống chôn ≤ 1/3 h sàn)", false),
        new("gen.cach_cot", GOngGen, "Khoảng hở ống gen tới mép cột", 0.05, "m", "Thực hành thi công", false),
    };

    private static readonly Dictionary<string, MepStdItem> _byKey = Items.ToDictionary(i => i.Key);

    public static MepStdItem Item(string key) => _byKey.TryGetValue(key, out var it) ? it : null;

    /// <summary>Giá trị tiêu chuẩn (không xét tuỳ chỉnh).</summary>
    public static double Val(string key) => _byKey.TryGetValue(key, out var it) ? it.Value : double.NaN;

    /// <summary>Giá trị áp dụng: tuỳ chỉnh nếu có, ngược lại theo tiêu chuẩn.</summary>
    public static double Get(MepStdOptions o, string key)
    {
        var ov = o?.Overrides?.LastOrDefault(x => x != null && x.Key == key);
        return ov != null ? ov.Value : Val(key);
    }

    public static bool IsOverridden(MepStdOptions o, string key) => o?.Overrides?.Any(x => x != null && x.Key == key) == true;

    /// <summary>Số kiểu Việt Nam (dấu phẩy thập phân) – không phụ thuộc văn hoá máy.</summary>
    public static string Num(double v) => v.ToString("0.####", CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Ghi "giá trị – nguồn" để in trong thuyết minh tính toán.</summary>
    public static string Cite(MepStdOptions o, string key)
    {
        var it = Item(key); if (it == null) return key;
        double v = Get(o, key);
        string sv = Num(v);
        return IsOverridden(o, key) ? $"{it.Name} = {sv} {it.Unit} (tuỳ chỉnh; tiêu chuẩn {Num(it.Value)} – {it.Label})"
                                    : $"{it.Name} = {sv} {it.Unit} ({it.Label})";
    }

    // =================================================================
    // TCVN 4513:1988 – lưu lượng cấp nước tính toán
    // =================================================================
    /// <summary>Hệ số a theo tiêu chuẩn dùng nước (l/người·ngđ) – nhà ở (TCVN 4513 bảng hệ số a).</summary>
    public static readonly (double Norm, double A)[] AlphaByNorm = { (100, 2.2), (125, 2.15), (150, 2.1), (200, 2.05), (250, 2.0), (300, 1.9), (350, 1.85), (400, 1.8) };
    /// <summary>Hệ số K theo tổng đương lượng N – nhà ở (TCVN 4513 bảng trị số K).</summary>
    public static readonly (double NMax, double K)[] KByN = { (300, 0.002), (500, 0.003), (800, 0.004), (1200, 0.005), (double.MaxValue, 0.006) };
    /// <summary>Hệ số α công trình công cộng: q = 0,2·α·√N (TCVN 4513 bảng hệ số α).</summary>
    public static readonly (MepBuildingKind Kind, double Alpha)[] AlphaPublic =
        { (MepBuildingKind.NhaTre, 1.2), (MepBuildingKind.BenhVien, 1.4), (MepBuildingKind.CoQuan, 1.5), (MepBuildingKind.TruongHoc, 1.8), (MepBuildingKind.AnDuong, 2.0), (MepBuildingKind.KhachSan, 2.5) };
    public const string SrcFlow = T4513 + " (q = 0,2·a·√N + K·N nhà ở; q = 0,2·α·√N công trình công cộng; bảng a, K, α) (*)";

    public static string BuildingName(MepBuildingKind k) => k switch
    {
        MepBuildingKind.NhaO => "Nhà ở",
        MepBuildingKind.NhaTre => "Nhà trẻ, mẫu giáo",
        MepBuildingKind.BenhVien => "Bệnh viện đa khoa",
        MepBuildingKind.CoQuan => "Cơ quan hành chính, cửa hàng",
        MepBuildingKind.TruongHoc => "Trường học",
        MepBuildingKind.AnDuong => "Nhà an dưỡng, nhà nghỉ",
        _ => "Khách sạn, nhà ở tập thể"
    };

    /// <summary>Hệ số a (nhà ở) – nội suy tuyến tính theo tiêu chuẩn dùng nước, kẹp hai đầu bảng.</summary>
    public static double AlphaFor(double norm)
    {
        var t = AlphaByNorm;
        if (norm <= t[0].Norm) return t[0].A;
        for (int i = 1; i < t.Length; i++)
        {
            if (Math.Abs(norm - t[i].Norm) < 1e-9) return t[i].A;
            if (norm < t[i].Norm)
                return t[i - 1].A + (t[i].A - t[i - 1].A) * (norm - t[i - 1].Norm) / (t[i].Norm - t[i - 1].Norm);
        }
        return t[^1].A;
    }

    public static double KFor(double n)
    {
        foreach (var (nmax, k) in KByN) if (n <= nmax + 1e-9) return k;
        return KByN[^1].K;
    }

    /// <summary>Lưu lượng tính toán q (l/s) theo tổng đương lượng N và loại công trình.</summary>
    public static double SupplyFlow(double n, MepStdOptions o)
    {
        if (n <= 0) return 0;
        o ??= new MepStdOptions();
        if (o.Building == MepBuildingKind.NhaO) return 0.2 * AlphaFor(o.WaterNorm) * Math.Sqrt(n) + KFor(n) * n;
        double al = AlphaPublic.FirstOrDefault(x => x.Kind == o.Building).Alpha;
        return 0.2 * (al > 0 ? al : 1.5) * Math.Sqrt(n);
    }

    // =================================================================
    // TCVN 4474:1987 – độ dốc ống thoát (tiêu chuẩn / nhỏ nhất)
    // =================================================================
    /// <summary>(Ø danh nghĩa mm, độ dốc tiêu chuẩn, độ dốc nhỏ nhất).</summary>
    public static readonly (double Dn, double Std, double Min)[] DrainSlopes = { (50, 0.035, 0.025), (100, 0.02, 0.012), (125, 0.015, 0.010), (150, 0.01, 0.007), (200, 0.008, 0.005) };
    public const string SrcSlope = T4474 + " Bảng độ dốc ống thoát (tiêu chuẩn / nhỏ nhất) (*)";

    /// <summary>Đổi Ø ngoài uPVC (mm) sang Ø danh nghĩa tra bảng (≤ 60 → 50; ≤ 110 → 100; ≤ 140 → 125; ≤ 160 → 150; còn lại 200).</summary>
    public static double NominalDrain(double od) => od <= 60 + 1e-9 ? 50 : od <= 114 ? 100 : od <= 140 ? 125 : od <= 168 ? 150 : 200;

    public static double DrainSlopeStd(double od) { double dn = NominalDrain(od); return DrainSlopes.First(x => x.Dn >= dn - 1e-9).Std; }
    public static double DrainSlopeMin(double od) { double dn = NominalDrain(od); return DrainSlopes.First(x => x.Dn >= dn - 1e-9).Min; }

    // =================================================================
    // IEC 60364-5-52 – dòng cho phép dây Cu cách điện PVC, 2 dây mang tải (Bảng B.52.4)
    // =================================================================
    public static readonly double[] Sections = { 1.5, 2.5, 4, 6, 10, 16, 25, 35 };
    /// <summary>Cách lắp A1 – dây trong ống trong tường cách nhiệt.</summary>
    public static readonly double[] IzA1 = { 14.5, 19.5, 26, 34, 46, 61, 80, 99 };
    /// <summary>Cách lắp B1 – dây trong ống gen chôn tường gạch / sàn bê tông (cách lắp chuẩn của chương trình).</summary>
    public static readonly double[] IzB1 = { 17.5, 24, 32, 41, 57, 76, 101, 125 };
    /// <summary>Cách lắp C – cáp đi nổi trên tường.</summary>
    public static readonly double[] IzC = { 19.5, 27, 36, 46, 63, 85, 112, 138 };
    public const string SrcIz = Iec + " Bảng B.52.4 (Cu/PVC, 2 dây mang tải, cách lắp B1 – ống gen chôn tường / sàn)";
    /// <summary>Hệ số hiệu chỉnh nhiệt độ không khí, cách điện PVC (Bảng B.52.14).</summary>
    public static readonly (double T, double K)[] TempFactor = { (10, 1.22), (15, 1.17), (20, 1.12), (25, 1.06), (30, 1.00), (35, 0.94), (40, 0.87), (45, 0.79), (50, 0.71), (55, 0.61), (60, 0.50) };
    /// <summary>Hệ số nhóm mạch đi chung bó / chôn chung (Bảng B.52.17, dòng 1).</summary>
    public static readonly double[] GroupFactor = { 1.00, 0.80, 0.70, 0.65, 0.60, 0.57, 0.54, 0.52, 0.50, 0.48, 0.45, 0.43, 0.41, 0.39, 0.38 };
    public const string SrcCorr = Iec + " Bảng B.52.14 (nhiệt độ) & B.52.17 (nhóm mạch)";

    public static double TempK(double t)
    {
        var a = TempFactor;
        if (t <= a[0].T) return a[0].K;
        for (int i = 1; i < a.Length; i++)
        {
            if (Math.Abs(t - a[i].T) < 1e-9) return a[i].K;
            if (t < a[i].T) return a[i - 1].K + (a[i].K - a[i - 1].K) * (t - a[i - 1].T) / (a[i].T - a[i - 1].T);
        }
        return a[^1].K;
    }

    public static double GroupK(int n) => n <= 1 ? 1.0 : GroupFactor[Math.Min(n, GroupFactor.Length) - 1];

    /// <summary>Iz (A) của tiết diện s theo cách lắp B1 đã hiệu chỉnh nhiệt độ và nhóm mạch.</summary>
    public static double Iz(double s, MepStdOptions o = null)
    {
        int i = Array.FindIndex(Sections, x => Math.Abs(x - s) < 1e-9);
        if (i < 0) return 0;
        o ??= new MepStdOptions();
        return IzB1[i] * TempK(o.AmbientC) * GroupK(o.GroupedCircuits);
    }

    /// <summary>Sụt áp 1 pha (%) – IEC 60364-5-52 Phụ lục G: ΔU = 2·L·I·(ρ/S·cosφ + λ·sinφ), λ = 0,08 mΩ/m.</summary>
    public static double VoltageDropPct(double current, double length, double section, double cosPhi = 0.85, double u = 220, MepStdOptions o = null)
    {
        if (current <= 0 || length <= 0 || section <= 0 || u <= 0) return 0;
        double rho = Get(o, "dien.rho");
        double sin = Math.Sqrt(Math.Max(0, 1 - cosPhi * cosPhi));
        double du = 2 * length * current * (rho / section * cosPhi + 0.08e-3 * sin);
        return du / u * 100;
    }

    /// <summary>Sụt áp cho phép (%) theo hệ thống.</summary>
    public static double VoltageDropLimit(MepSystemKind sys, MepStdOptions o = null) => Get(o, sys == MepSystemKind.DienChieuSang ? "dien.du_cs" : "dien.du_khac");

    // =================================================================
    // TCVN 5687:2010 – thông số trong / ngoài nhà
    // =================================================================
    public static (double T, double Phi) Indoor(MepStdOptions o = null) => (Get(o, "dhkk.t_trong"), Get(o, "dhkk.phi_trong"));

    public static (double T, double Phi) Outdoor(MepStdOptions o = null)
    {
        bool hcm = (o?.Climate ?? HaNoi).Contains("Hồ Chí Minh") || (o?.Climate ?? "").Contains("HCM");
        return hcm ? (Get(o, "dhkk.t_hcm"), Get(o, "dhkk.phi_hcm")) : (Get(o, "dhkk.t_hn"), Get(o, "dhkk.phi_hn"));
    }

    /// <summary>Lưu lượng gió tươi (m³/h) cho số người.</summary>
    public static double FreshAir(int people, MepStdOptions o = null) => Math.Max(0, people) * Get(o, "dhkk.gio_tuoi");

    /// <summary>Bảng "Thông số tiêu chuẩn áp dụng" (xuất Excel / thuyết minh): nhóm, thông số, tiêu chuẩn, áp dụng, đơn vị, nguồn.</summary>
    public static KetCauVXT.Report.QtyTable Table(MepStdOptions o)
    {
        var t = new KetCauVXT.Report.QtyTable
        {
            Title = "Thông số điện nước – ĐHKK theo tiêu chuẩn",
            Headers = new[] { "Nhóm", "Thông số", "Tiêu chuẩn", "Áp dụng", "Đơn vị", "Nguồn" },
            Widths = new double[] { 12, 40, 10, 10, 10, 50 }
        };
        foreach (var it in Items)
            t.Add(it.Group, it.Name, it.Value, Get(o, it.Key), it.Unit, it.Label + (IsOverridden(o, it.Key) ? " – tuỳ chỉnh" : ""));
        return t;
    }

    /// <summary>Thuyết minh tóm tắt các bảng đang áp dụng (in kèm kết quả tính).</summary>
    public static List<string> Summary(MepStdOptions o)
    {
        o ??= new MepStdOptions();
        var L = new List<string>
        {
            $"Cấp nước: {BuildingName(o.Building)} – " + (o.Building == MepBuildingKind.NhaO
                ? $"q = 0,2·{AlphaFor(o.WaterNorm):0.###}·√N + K·N (a theo {o.WaterNorm:0} l/người·ngđ; K = 0,002…0,006 theo N) – {SrcFlow}"
                : $"q = 0,2·{AlphaPublic.FirstOrDefault(x => x.Kind == o.Building).Alpha:0.##}·√N – {SrcFlow}"),
            Cite(o, "cap.v_max"),
            $"Dây dẫn: {SrcIz}; hiệu chỉnh nhiệt độ {o.AmbientC:0} °C → k1 = {TempK(o.AmbientC):0.00}, nhóm {Math.Max(1, o.GroupedCircuits)} mạch → k2 = {GroupK(o.GroupedCircuits):0.00} ({SrcCorr})",
            Cite(o, "dien.du_cs"), Cite(o, "dien.du_khac"),
            $"Thoát nước: {SrcSlope}: Ø50 {DrainSlopes[0].Std * 100:0.#} % (min {DrainSlopes[0].Min * 100:0.#} %), Ø100 {DrainSlopes[1].Std * 100:0.#} % (min {DrainSlopes[1].Min * 100:0.#} %)",
            Cite(o, "dhkk.t_trong"), Cite(o, "dhkk.phi_trong"),
            $"Ngoài nhà {o.Climate}: t = {Outdoor(o).T:0.0} °C, φ = {Outdoor(o).Phi:0.#} % (Theo {T5687} Phụ lục B)",
            Cite(o, "dhkk.gio_tuoi"),
        };
        if (Items.Any(i => !i.Verified)) L.Add("(*) Giá trị trích theo thực hành / tài liệu thứ cấp – cần đối chiếu bản gốc tiêu chuẩn; có thể tuỳ chỉnh trong Cài đặt điện nước.");
        return L;
    }
}
