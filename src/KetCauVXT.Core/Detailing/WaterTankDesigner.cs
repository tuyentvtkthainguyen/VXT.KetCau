using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// BỂ NƯỚC BTCT HÌNH CHỮ NHẬT (bể ngầm / bể đặt trên mái, sàn) – chế độ triển khai hồ sơ.
// Thành bể: bản chịu áp lực thuỷ tĩnh (bể đầy) và áp lực đất + nước ngầm (bể ngầm rỗng), tính theo phương pháp dải
// (Grashof – chia tải theo điều kiện cùng độ võng): phương đứng (công xôn / ngàm–khớp), phương ngang (ngàm hai đầu tại góc bể).
// Bề rộng vết nứt TCVN 5574:2018 mục 8.2.2.2, giới hạn acrc,ult theo Bảng 17 (yêu cầu hạn chế thấm: 0,2 mm dài hạn).
// Bản đáy, bản nắp: bản kê 4 cạnh chia tải Grashof. Bể ngầm: kiểm tra chống đẩy nổi G ≥ 1,1·U.
// Đơn vị: m, kN, kN/m², kN.m/m; đường kính thép mm.
// =====================================================================

/// <summary>Một mặt bố trí thép (thành, đáy, nắp): mô men thiết kế / tiêu chuẩn trên 1 m, thép yêu cầu, thép chọn, kiểm tra nứt.</summary>
public class TankFace
{
    public string Name { get; set; } = "";
    /// <summary>Chiều dày cấu kiện (m)</summary>
    public double H { get; set; }
    /// <summary>Mô men tính toán / tiêu chuẩn (kN.m/m)</summary>
    public double M { get; set; }
    public double Mser { get; set; }
    /// <summary>Lực kéo dọc trục tính toán / tiêu chuẩn (kN/m) – thép ngang thành (phản lực thành kề)</summary>
    public double N { get; set; }
    public double Nser { get; set; }
    public double AsReq { get; set; }
    public int D { get; set; }
    public double S { get; set; }
    public double AsProv => Dt.AreaPerM(D, S);
    public double Mu { get; set; }
    /// <summary>Bề rộng vết nứt dài hạn (m)</summary>
    public double Crack { get; set; }
    public double SigmaS { get; set; }
    public bool UserChoice { get; set; }
    /// <summary>Đã tăng thép so với yêu cầu cường độ để thoả mãn bề rộng vết nứt</summary>
    public bool IncreasedForCrack { get; set; }
    public string Text => Dt.Spaced(D, S);
}

/// <summary>Kết quả bể nước.</summary>
public class WaterTankResult : DetailResult
{
    public bool Underground { get; set; }
    public bool Covered { get; set; }
    /// <summary>Kích thước ngoài thành Lox × Loy (m), bản đáy Lbx × Lby (m), chiều cao ngoài Hout (m)</summary>
    public double Lox { get; set; }
    public double Loy { get; set; }
    public double Lbx { get; set; }
    public double Lby { get; set; }
    public double Hout { get; set; }
    /// <summary>Áp lực nước tại đáy (tiêu chuẩn) γw·Hw (kN/m²)</summary>
    public double PWater { get; set; }
    public double Ka { get; set; }
    /// <summary>Áp lực đất + nước ngầm tiêu chuẩn tại đỉnh / chân thành (kN/m²)</summary>
    public double PSoilTop { get; set; }
    public double PSoilBase { get; set; }
    /// <summary>Tỷ lệ tải truyền theo phương đứng của thành dài / thành ngắn</summary>
    public double AlphaVLong { get; set; }
    public double AlphaVShort { get; set; }
    public TankFace WallVertIn { get; set; }
    public TankFace WallVertOut { get; set; }
    public TankFace WallHor { get; set; }
    public TankFace BottomTop { get; set; }
    public TankFace BottomBot { get; set; }
    public TankFace CoverBot { get; set; }
    /// <summary>Chống đẩy nổi: trọng lượng giữ G, lực đẩy nổi U (kN)</summary>
    public double BuoyG { get; set; }
    public double BuoyU { get; set; }
    public IEnumerable<TankFace> Faces => new[] { WallVertIn, WallVertOut, WallHor, BottomTop, BottomBot, CoverBot }.Where(f => f != null);
}

/// <summary>Bể nước BTCT chữ nhật – dữ liệu nhập (giao diện tự sinh theo [Field]).</summary>
public class WaterTankInput : IDetailMember
{
    public const string PosUnderground = "Bể ngầm";
    public const string PosRoof = "Bể trên mái / sàn";

    [Field("Tên bể", Group = "Chung")] public string Name { get; set; } = "BN1";
    [Field("Số lượng", Group = "Chung")] public int Quantity { get; set; } = 1;
    [Field("Vị trí", Group = "Chung", Choices = PosUnderground + "|" + PosRoof)] public string Position { get; set; } = PosUnderground;

    [Field("Chiều dài trong Lx", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Lx { get; set; } = 3.0;
    [Field("Chiều rộng trong Ly", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Ly { get; set; } = 2.0;
    [Field("Chiều cao thông thuỷ thành H", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double H { get; set; } = 1.8;
    [Field("Chiều cao mực nước Hw", Unit = "mm", Scale = 1000, Group = "Kích thước", Hint = "≤ H")] public double Hw { get; set; } = 1.6;
    [Field("Chiều dày thành", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Tw { get; set; } = 0.20;
    [Field("Chiều dày bản đáy", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Tb { get; set; } = 0.25;
    [Field("Chiều dày bản nắp (0 = bể hở)", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Tc { get; set; } = 0.12;
    [Field("Mở rộng bản đáy ra ngoài thành", Unit = "mm", Scale = 1000, Group = "Kích thước", Hint = "chống đẩy nổi bể ngầm")] public double Toe { get; set; } = 0;
    [Field("Cạnh lỗ thăm (0 = không)", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Manhole { get; set; } = 0.6;

    [Field("Hoạt tải nắp", Unit = "kN/m²", Group = "Tải trọng")] public double CoverLive { get; set; } = 1.5;
    [Field("Trọng lượng riêng nước γw", Unit = "kN/m³", Group = "Tải trọng")] public double GammaW { get; set; } = 10;
    [Field("Hệ số tin cậy áp lực nước", Group = "Tải trọng")] public double FactorWater { get; set; } = 1.1;
    [Field("Hệ số tin cậy áp lực đất", Group = "Tải trọng")] public double FactorSoil { get; set; } = 1.15;
    [Field("Hệ số tin cậy tĩnh tải", Group = "Tải trọng")] public double FactorDead { get; set; } = 1.1;
    [Field("Hệ số tin cậy hoạt tải", Group = "Tải trọng")] public double FactorLive { get; set; } = 1.3;

    [Field("Trọng lượng riêng đất γ", Unit = "kN/m³", Group = "Đất (bể ngầm)")] public double GammaS { get; set; } = 18;
    [Field("Góc ma sát trong φ", Unit = "độ", Group = "Đất (bể ngầm)")] public double Phi { get; set; } = 20;
    [Field("Phụ tải mặt đất q", Unit = "kN/m²", Group = "Đất (bể ngầm)")] public double Surcharge { get; set; } = 10;
    [Field("Độ sâu mực nước ngầm (từ mặt nắp)", Unit = "mm", Scale = 1000, Group = "Đất (bể ngầm)", Hint = "lớn = không có nước ngầm")] public double Hgw { get; set; } = 99;

    [Field("Bề rộng vết nứt giới hạn", Unit = "mm", Scale = 1000, Group = "Kiểm tra", Hint = "TCVN 5574:2018 Bảng 17 – hạn chế thấm: 0,2 mm dài hạn")] public double CrackLimit { get; set; } = 0.0002;

    [Field("Thép đứng thành mặt trong Ø (0 = tự chọn)", Unit = "mm", Group = "Chọn thép")] public int WallVInD { get; set; }
    [Field("  bước", Unit = "mm", Scale = 1000, Group = "Chọn thép")] public double WallVInS { get; set; }
    [Field("Thép đứng thành mặt ngoài Ø", Unit = "mm", Group = "Chọn thép")] public int WallVOutD { get; set; }
    [Field("  bước", Unit = "mm", Scale = 1000, Group = "Chọn thép")] public double WallVOutS { get; set; }
    [Field("Thép ngang thành (2 mặt) Ø", Unit = "mm", Group = "Chọn thép")] public int WallHD { get; set; }
    [Field("  bước", Unit = "mm", Scale = 1000, Group = "Chọn thép")] public double WallHS { get; set; }
    [Field("Thép bản đáy Ø (2 lớp)", Unit = "mm", Group = "Chọn thép")] public int BottomD { get; set; }
    [Field("  bước", Unit = "mm", Scale = 1000, Group = "Chọn thép")] public double BottomS { get; set; }
    [Field("Thép bản nắp Ø", Unit = "mm", Group = "Chọn thép")] public int CoverD { get; set; }
    [Field("  bước", Unit = "mm", Scale = 1000, Group = "Chọn thép")] public double CoverS { get; set; }

    public bool IsUnderground => (Position ?? "").Contains("ngầm");

    public DetailResult Run(DetailSettings s) => WaterTankDesigner.Design(this, s ?? new DetailSettings());

    public List<Figure> Draw(DetailResult r, DetailSettings s) =>
        r is WaterTankResult t ? WaterTankDrawing.Build(this, t, s ?? new DetailSettings()) : new List<Figure>();
}

/// <summary>Công thức dùng chung (công khai để kiểm thử, thuyết minh).</summary>
public static class WaterTankFormulas
{
    /// <summary>Hệ số áp lực đất chủ động Rankine Ka = tan²(45° − φ/2).</summary>
    public static double Ka(double phiDeg)
    {
        double t = Math.Tan((45 - phiDeg / 2) * Math.PI / 180);
        return t * t;
    }

    /// <summary>Áp lực ngang đất + nước ngầm tại độ sâu z (từ mặt đất = mặt nắp):
    /// σv = q + γ·min(z, hn) + (γ − γw)·max(0, z − hn); p = Ka·σv + γw·max(0, z − hn).</summary>
    public static double SoilPressure(double z, double ka, double gammaS, double gammaW, double q, double hgw)
    {
        z = Math.Max(z, 0);
        double above = Math.Min(z, hgw), below = Math.Max(0, z - hgw);
        double sv = q + gammaS * above + (gammaS - gammaW) * below;
        return ka * sv + gammaW * below;
    }

    /// <summary>Dải đứng 1 m chịu tải hình thang (phần đều u + tam giác tăng dần t về chân), chân ngàm:
    /// đỉnh tự do (công xôn): Mchân = u·H²/2 + t·H²/6, Mnhịp = 0;
    /// đỉnh khớp (có nắp): Mchân = u·H²/8 + t·H²/15, Mnhịp = 9u·H²/128 + 0,0298·t·H² (cộng hai cực trị – thiên về an toàn).</summary>
    public static (double MBase, double MSpan) Vertical(double u, double t, double H, bool covered)
    {
        if (H <= 0) return (0, 0);
        if (!covered) return (u * H * H / 2 + t * H * H / 6, 0);
        return (u * H * H / 8 + t * H * H / 15, 9 * u * H * H / 128 + 0.0298 * t * H * H);
    }

    /// <summary>Dải ngang 1 m ngàm hai đầu (góc bể) chịu tải đều p: Mgối = p·L²/12, Mnhịp = p·L²/24.</summary>
    public static (double MSupport, double MSpan) Horizontal(double p, double L) => (p * L * L / 12, p * L * L / 24);

    /// <summary>Tỷ lệ tải truyền theo phương đứng (phương pháp dải Grashof, cùng độ võng tại tâm dải):
    /// L/H ≥ 2 → 1 (bản một phương đứng); H/L ≥ 2 → 0 (một phương ngang);
    /// ở giữa αv = L⁴/(L⁴ + k·H⁴) với k = 384/8 = 48 (đỉnh tự do: công xôn ↔ ngàm 2 đầu) hoặc k = 384/185 ≈ 2,08 (có nắp: ngàm–khớp ↔ ngàm 2 đầu).</summary>
    public static double ShareVertical(double L, double H, bool covered)
    {
        if (H <= 1e-9) return 0;
        if (L <= 1e-9) return 1;
        if (L / H >= 2) return 1;
        if (H / L >= 2) return 0;
        double k = covered ? 384.0 / 185.0 : 48.0;
        double l4 = Math.Pow(L, 4), h4 = Math.Pow(H, 4);
        return l4 / (l4 + k * h4);
    }

    /// <summary>Hệ số chia tải bản kê 4 cạnh theo phương ngắn L1 (Grashof): L2/L1 &gt; 2 → 1; ngược lại α = L2⁴/(L1⁴ + L2⁴).</summary>
    public static double ShareShort(double L1, double L2)
    {
        if (L1 <= 1e-9) return 0;
        if (L2 / L1 > 2) return 1;
        double a = Math.Pow(L1, 4), b = Math.Pow(L2, 4);
        return b / (a + b);
    }

    /// <summary>Bề rộng vết nứt dài hạn TCVN 5574:2018 mục 8.2.2.2 (dải b = 1 m):
    /// acrc = φ1·φ2·φ3·ψs·(σs/Es)·ls, φ1 = 1,4 (dài hạn), φ2 = 0,5 (thép có gờ), φ3 = 1,0 (uốn), ψs = 1;
    /// σs = M/(z·As) + N/(2·As) với z = 0,9·h0 (gần đúng); ls = 0,5·(Abt/As)·ds, Abt = b·0,5h (vùng kéo lấy ≈ nửa tiết diện),
    /// giới hạn max(10ds; 100 mm) ≤ ls ≤ min(40ds; 400 mm). Trả về (acrc m, σs kN/m², ls m).</summary>
    public static (double Acrc, double SigmaS, double Ls) CrackWidth(double mSer, double nSer, double h, double cover, int d, double s, double es)
    {
        double As = Dt.AreaPerM(d, s);
        double ds = d / 1000.0;
        double h0 = h - cover - ds / 2;
        if (As <= 1e-12 || h0 <= 1e-6) return (double.PositiveInfinity, double.PositiveInfinity, 0);
        double z = 0.9 * h0;
        double sigma = Math.Max(0, Math.Abs(mSer) / (z * As) + Math.Max(0, nSer) / (2 * As));
        double abt = 1.0 * 0.5 * h;
        double ls = 0.5 * abt / As * ds;
        double lo = Math.Max(10 * ds, 0.10), hi = Math.Min(40 * ds, 0.40);
        if (hi < lo) hi = lo;
        ls = Math.Clamp(ls, lo, hi);
        double a = 1.4 * 0.5 * 1.0 * 1.0 * sigma / es * ls;
        return (a, sigma, ls);
    }

    /// <summary>Thể tích bê tông (m³): bản đáy Lbx·Lby·Tb + bản nắp Lox·Loy·Tc + thành (Lox·Loy − Lx·Ly)·H (bỏ qua lỗ thăm).</summary>
    public static double ConcreteVolume(double lx, double ly, double h, double tw, double tb, double tc, double toe)
    {
        double lox = lx + 2 * tw, loy = ly + 2 * tw;
        return (lox + 2 * toe) * (loy + 2 * toe) * tb + lox * loy * Math.Max(tc, 0) + (lox * loy - lx * ly) * h;
    }
}

public static class WaterTankDesigner
{
    public const double GammaConcrete = 25;
    private static readonly int[] WallDias = { 10, 12, 14, 16 };
    private static readonly int[] CoverDias = { 8, 10, 12 };

    public static WaterTankResult Design(WaterTankInput i, DetailSettings s)
    {
        var r = new WaterTankResult { Name = i.Name ?? "BN", Quantity = Math.Max(1, i.Quantity), Kind = "Bể nước", ConcreteClass = s.Concrete };
        try
        {
            if (!Validate(i, r)) return r;
            DesignCore(i, s, r);
        }
        catch (Exception ex)
        {
            r.Messages.Add("LỖI tính bể nước: " + ex.Message);
        }
        return r;
    }

    private static bool Validate(WaterTankInput i, WaterTankResult r)
    {
        var err = new List<string>();
        if (!(i.Lx > 0) || !(i.Ly > 0)) err.Add("kích thước trong Lx, Ly phải > 0");
        if (!(i.H > 0)) err.Add("chiều cao thành H phải > 0");
        if (!(i.Tw >= 0.10)) err.Add("chiều dày thành ≥ 100 mm");
        if (!(i.Tb >= 0.10)) err.Add("chiều dày bản đáy ≥ 100 mm");
        if (i.Tc < 0 || (i.Tc > 0 && i.Tc < 0.06)) err.Add("chiều dày bản nắp = 0 (bể hở) hoặc ≥ 60 mm");
        if (i.Hw < 0) err.Add("chiều cao mực nước Hw ≥ 0");
        if (i.Toe < 0) err.Add("phần mở rộng bản đáy ≥ 0");
        if (!(i.CrackLimit > 0)) err.Add("bề rộng vết nứt giới hạn > 0");
        if (err.Count == 0) return true;
        r.Messages.Add("LỖI dữ liệu bể " + r.Name + ": " + string.Join("; ", err) + ".");
        return false;
    }

    private static void DesignCore(WaterTankInput i, DetailSettings s, WaterTankResult r)
    {
        var conc = s.Conc();
        double cov = Math.Max(s.CoverTank, 0.02);
        bool ug = i.IsUnderground, covered = i.Tc > 1e-9;
        double hw = Math.Min(i.Hw, i.H);
        if (i.Hw > i.H + 1e-9) r.Messages.Add("Mực nước Hw > H – lấy Hw = H (bể đầy tràn).");
        r.Underground = ug; r.Covered = covered;
        r.Lox = i.Lx + 2 * i.Tw; r.Loy = i.Ly + 2 * i.Tw;
        r.Lbx = r.Lox + (ug ? 2 * i.Toe : 0); r.Lby = r.Loy + (ug ? 2 * i.Toe : 0);
        double tc = covered ? i.Tc : 0;
        r.Hout = i.Tb + i.H + tc;
        double gw = i.GammaW;

        r.Calc.Add("## Số liệu bể " + r.Name);
        r.Calc.Add($"{(ug ? "Bể ngầm" : "Bể đặt trên mái / sàn")}{(covered ? ", có nắp" : ", hở")}: trong Lx × Ly × H = {Dt.Mm(i.Lx)} × {Dt.Mm(i.Ly)} × {Dt.Mm(i.H)} mm; "
                   + $"mực nước Hw = {Dt.Mm(hw)} mm; thành {Dt.Mm(i.Tw)}, đáy {Dt.Mm(i.Tb)}{(covered ? ", nắp " + Dt.Mm(tc) : "")} mm; bê tông {s.Concrete}, thép {s.RebarMain}; lớp bảo vệ {Dt.Mm(cov)} mm.");

        // ---------------- áp lực ----------------
        r.PWater = gw * hw;
        r.Calc.Add("## Áp lực lên thành");
        r.Calc.Add($"Bể đầy: áp lực thuỷ tĩnh phân bố tam giác, tại đáy pw = γw·Hw = {Dt.F(gw)}·{Dt.F(hw, "0.###")} = {Dt.F(r.PWater, "0.##")} kN/m² (tiêu chuẩn), hệ số tin cậy {Dt.F(i.FactorWater)}.");
        double uS = 0, tS = 0;
        if (ug)
        {
            r.Ka = WaterTankFormulas.Ka(i.Phi);
            r.PSoilTop = WaterTankFormulas.SoilPressure(tc, r.Ka, i.GammaS, gw, i.Surcharge, i.Hgw);
            r.PSoilBase = WaterTankFormulas.SoilPressure(tc + i.H, r.Ka, i.GammaS, gw, i.Surcharge, i.Hgw);
            uS = r.PSoilTop; tS = Math.Max(0, r.PSoilBase - r.PSoilTop);
            r.Calc.Add($"Bể ngầm rỗng: áp lực đất chủ động Ka = tan²(45° − φ/2) = tan²(45° − {Dt.F(i.Phi)}°/2) = {Dt.F(r.Ka, "0.###")}; "
                       + $"p(z) = Ka·(q + γ·z) {(i.Hgw < r.Hout ? "+ γw·(z − hn) với γ' = γ − γw dưới mực nước ngầm hn = " + Dt.F(i.Hgw, "0.##") + " m" : "(không có nước ngầm trong phạm vi bể)")}.");
            r.Calc.Add($"Tại đỉnh thành (z = {Dt.F(tc, "0.###")} m): p = {Dt.F(r.PSoilTop, "0.##")} kN/m²; tại chân thành (z = {Dt.F(tc + i.H, "0.###")} m): p = {Dt.F(r.PSoilBase, "0.##")} kN/m² (tiêu chuẩn), hệ số tin cậy {Dt.F(i.FactorSoil)}.");
        }

        // ---------------- thành bể: phương pháp dải ----------------
        r.Calc.Add("## Thành bể – phương pháp dải (Grashof)");
        r.Calc.Add(covered
            ? "Dải đứng: chân ngàm vào bản đáy, đỉnh khớp (bản nắp). Tải tam giác đỉnh p: Mchân = p·H²/15, Mnhịp = 0,0298·p·H²; tải đều u: Mchân = u·H²/8, Mnhịp = 9u·H²/128 (nước: tam giác đỉnh γw·Hw trên toàn H – thiên về an toàn)."
            : "Dải đứng: công xôn ngàm tại bản đáy, đỉnh tự do: bể đầy Mchân = γw·Hw³/6; tải đất u, t: Mchân = u·H²/2 + t·H²/6.");
        r.Calc.Add("Dải ngang tại chân thành: ngàm hai đầu ở góc bể, tải đều p: Mgối = p·L²/12, Mnhịp = p·L²/24; phản lực thành kề gây kéo dọc N = αh·p·L'/2 (L' = chiều dài thành vuông góc).");
        r.Calc.Add($"Chia tải: L/H ≥ 2 → một phương đứng; H/L ≥ 2 → một phương ngang; ở giữa αv = L⁴/(L⁴ + k·H⁴), k = {(covered ? "384/185 ≈ 2,08 (ngàm–khớp ↔ ngàm hai đầu)" : "384/8 = 48 (công xôn ↔ ngàm hai đầu)")}, αh = 1 − αv.");

        double Lc1 = i.Lx + i.Tw, Lc2 = i.Ly + i.Tw;   // nhịp tim thành
        double mVinD = 0, mVinS = 0, mVoutD = 0, mVoutS = 0, mHD = 0, mHS = 0, nHD = 0, nHS = 0, qWall = 0, qWallS = 0;
        foreach (var (L, Lo, label) in new[] { (Lc1, Lc2, "thành theo Lx"), (Lc2, Lc1, "thành theo Ly") })
        {
            double av = WaterTankFormulas.ShareVertical(L, i.H, covered), ah = 1 - av;
            if (label.EndsWith("Lx")) { if (Lc1 >= Lc2) r.AlphaVLong = av; else r.AlphaVShort = av; }
            else { if (Lc2 > Lc1) r.AlphaVLong = av; else r.AlphaVShort = av; }
            // bể đầy (tiêu chuẩn)
            double mbW, msW;
            if (covered) (mbW, msW) = WaterTankFormulas.Vertical(0, r.PWater, i.H, true);
            else { mbW = gw * hw * hw * hw / 6; msW = 0; }
            var (mhsW, mhpW) = WaterTankFormulas.Horizontal(r.PWater, L);
            double nW = ah * r.PWater * Lo / 2;
            // bể ngầm rỗng (tiêu chuẩn)
            var (mbS, msS) = ug ? WaterTankFormulas.Vertical(uS, tS, i.H, covered) : (0.0, 0.0);
            var (mhsS, mhpS) = ug ? WaterTankFormulas.Horizontal(r.PSoilBase, L) : (0.0, 0.0);
            double fw = i.FactorWater, fs = i.FactorSoil;
            r.Calc.Add($"{char.ToUpper(label[0]) + label[1..]}: L = {Dt.F(L, "0.###")} m, L/H = {Dt.F(L / i.H, "0.##")} → αv = {Dt.F(av, "0.###")}, αh = {Dt.F(ah, "0.###")}. "
                       + $"Bể đầy (tiêu chuẩn): Mchân = {Dt.F(mbW, "0.##")}, Mnhịp đứng = {Dt.F(msW, "0.##")}, Mgối ngang = {Dt.F(mhsW, "0.##")}, Mnhịp ngang = {Dt.F(mhpW, "0.##")} kN.m/m"
                       + (ug ? $"; bể rỗng – đất: Mchân = {Dt.F(mbS, "0.##")}, Mnhịp đứng = {Dt.F(msS, "0.##")}, Mgối ngang = {Dt.F(mhsS, "0.##")}, Mnhịp ngang = {Dt.F(mhpS, "0.##")} kN.m/m" : "") + ".");
            // mặt trong: bể đầy – chân thành (đứng), góc (ngang); bể rỗng – nhịp
            mVinS = Math.Max(mVinS, Math.Max(av * mbW, av * msS));
            mVinD = Math.Max(mVinD, Math.Max(av * mbW * fw, av * msS * fs));
            mVoutS = Math.Max(mVoutS, Math.Max(av * msW, av * mbS));
            mVoutD = Math.Max(mVoutD, Math.Max(av * msW * fw, av * mbS * fs));
            double mhS = Math.Max(Math.Max(ah * mhsW, ah * mhpW), Math.Max(ah * mhsS, ah * mhpS));
            double mhD = Math.Max(Math.Max(ah * mhsW, ah * mhpW) * fw, Math.Max(ah * mhsS, ah * mhpS) * fs);
            if (mhD > mHD) { mHD = mhD; mHS = mhS; }
            nHS = Math.Max(nHS, nW); nHD = Math.Max(nHD, nW * fw);
            // cắt chân thành (lấy toàn bộ tải dải đứng – an toàn)
            double qW = covered ? r.PWater * i.H / 2 : gw * hw * hw / 2;
            double qS = ug ? (uS * i.H + tS * i.H / 2) : 0;
            qWall = Math.Max(qWall, Math.Max(qW * fw, qS * fs)); qWallS = Math.Max(qWallS, Math.Max(qW, qS));
        }

        var main = s.Main();
        r.WallVertIn = DesignFace("Thành – thép đứng mặt trong", i.Tw, cov, mVinD, mVinS, 0, 0, i.WallVInD, i.WallVInS, WallDias, conc, main, i.CrackLimit, r);
        r.WallVertOut = DesignFace("Thành – thép đứng mặt ngoài", i.Tw, cov, mVoutD, mVoutS, 0, 0, i.WallVOutD, i.WallVOutS, WallDias, conc, main, i.CrackLimit, r);
        r.WallHor = DesignFace("Thành – thép ngang (2 mặt)", i.Tw, cov, mHD, mHS, nHD, nHS, i.WallHD, i.WallHS, WallDias, conc, main, i.CrackLimit, r);

        // cắt chân thành – bê tông không cốt đai Qb,min = 0,5·Rbt·b·h0
        double h0w = i.Tw - cov - 0.006;
        double qb = 0.5 * conc.Rbt * 1.0 * h0w;
        r.Checks.Add(Dt.Check("Thành – lực cắt chân thành Q ≤ 0,5·Rbt·b·h0", qWall, qb, "kN/m"));
        r.Calc.Add($"Lực cắt chân thành (toàn bộ tải dải đứng, an toàn): Q = {Dt.F(qWall, "0.##")} kN/m ≤ Qb = 0,5·Rbt·b·h0 = 0,5·{Dt.F(conc.Rbt / 1000, "0.##")}·1000·{Dt.F(h0w * 1000, "0")}·10⁻³ = {Dt.F(qb, "0.#")} kN/m → {(qWall <= qb ? "đạt" : "KHÔNG ĐẠT – tăng chiều dày thành")}.");

        // ---------------- bản đáy ----------------
        r.Calc.Add("## Bản đáy");
        double L1 = Math.Min(Lc1, Lc2), L2 = Math.Max(Lc1, Lc2);
        double alpha = WaterTankFormulas.ShareShort(L1, L2);
        double gWalls = (r.Lox * r.Loy - i.Lx * i.Ly) * i.H * GammaConcrete;
        double gCover = r.Lox * r.Loy * tc * GammaConcrete;
        double qB, qBs; bool upward;
        if (!ug)
        {
            upward = false;
            qBs = gw * hw + GammaConcrete * i.Tb;
            qB = i.FactorWater * gw * hw + i.FactorDead * GammaConcrete * i.Tb;
            r.Calc.Add($"Bể trên mái / sàn: bản đáy kê lên dầm dưới chân thành, ngàm với thành; tải xuống q = {Dt.F(i.FactorWater)}·γw·Hw + {Dt.F(i.FactorDead)}·25·hb = {Dt.F(qB, "0.##")} kN/m² (tiêu chuẩn {Dt.F(qBs, "0.##")}).");
        }
        else
        {
            upward = true;
            double aB = r.Lbx * r.Lby;
            double qFull = (gWalls + gCover + i.CoverLive * r.Lox * r.Loy) / aB;      // phản lực đất do thành + nắp (nước, bản đáy tự cân bằng)
            double qFullD = (i.FactorDead * (gWalls + gCover) + i.FactorLive * i.CoverLive * r.Lox * r.Loy) / aB;
            double hsub = Math.Max(0, r.Hout - i.Hgw);
            double qUp = Math.Max(0, gw * hsub - 0.9 * GammaConcrete * i.Tb);
            double qUpD = Math.Max(0, i.FactorWater * gw * hsub - 0.9 * GammaConcrete * i.Tb);
            qBs = Math.Max(qFull, qUp); qB = Math.Max(qFullD, qUpD);
            r.Calc.Add($"Bể ngầm: tải hướng lên bản đáy = max(phản lực đất do thành + nắp: {Dt.F(qFullD, "0.##")}; đẩy nổi khi rỗng γw·hsub − 0,9·25·hb: {Dt.F(qUpD, "0.##")}) = {Dt.F(qB, "0.##")} kN/m² (tính toán).");
        }
        double mSupB = alpha * qB * L1 * L1 / 12, mSpanB = alpha * qB * L1 * L1 / 24;
        double mSupBs = alpha * qBs * L1 * L1 / 12, mSpanBs = alpha * qBs * L1 * L1 / 24;
        r.Calc.Add($"Bản kê 4 cạnh ngàm (Grashof): L1 = {Dt.F(L1, "0.###")} m, L2 = {Dt.F(L2, "0.###")} m, α = L2⁴/(L1⁴ + L2⁴) = {Dt.F(alpha, "0.###")}; "
                   + $"Mgối = α·q·L1²/12 = {Dt.F(mSupB, "0.##")}, Mnhịp = α·q·L1²/24 = {Dt.F(mSpanB, "0.##")} kN.m/m (thép phương dài lấy như phương ngắn).");
        // mặt trên đáy = mặt trong (tiếp xúc nước)
        double mTopD = upward ? mSpanB : mSupB, mTopS = upward ? mSpanBs : mSupBs;
        double mBotD = upward ? mSupB : mSpanB, mBotS = upward ? mSupBs : mSpanBs;
        r.BottomTop = DesignFace("Bản đáy – lớp trên", i.Tb, cov, mTopD, mTopS, 0, 0, i.BottomD, i.BottomS, WallDias, conc, main, i.CrackLimit, r);
        r.BottomBot = DesignFace("Bản đáy – lớp dưới", i.Tb, Math.Max(cov, ug ? s.CoverFooting * 0.7 : cov), mBotD, mBotS, 0, 0, i.BottomD, i.BottomS, WallDias, conc, main, i.CrackLimit, r);

        // ---------------- bản nắp ----------------
        if (covered)
        {
            r.Calc.Add("## Bản nắp");
            double qC = i.FactorDead * (GammaConcrete * tc + 0.5) + i.FactorLive * i.CoverLive;
            double qCs = GammaConcrete * tc + 0.5 + i.CoverLive;
            double mC = alpha * qC * L1 * L1 / 8, mCs = alpha * qCs * L1 * L1 / 8;
            r.Calc.Add($"Bản nắp kê tự do lên thành: q = {Dt.F(i.FactorDead)}·(25·hn + 0,5) + {Dt.F(i.FactorLive)}·{Dt.F(i.CoverLive)} = {Dt.F(qC, "0.##")} kN/m²; M = α·q·L1²/8 = {Dt.F(mC, "0.##")} kN.m/m."
                       + (i.Manhole > 0 ? $" Lỗ thăm {Dt.Mm(i.Manhole)} × {Dt.Mm(i.Manhole)} mm: gia cường 2Ø12 mỗi cạnh, nắp đậy inox/composite kín nước." : ""));
            int[] dias = CoverDias;
            r.CoverBot = DesignFace("Bản nắp – lớp dưới", tc, Math.Max(s.CoverSlab, 0.02), mC, mCs, 0, 0, i.CoverD, i.CoverS, dias, conc, main, Math.Max(i.CrackLimit, 0.0003), r);
        }

        // ---------------- chống đẩy nổi ----------------
        if (ug)
        {
            double vol = WaterTankFormulas.ConcreteVolume(i.Lx, i.Ly, i.H, i.Tw, i.Tb, tc, i.Toe);
            double hs = tc + i.H;   // đất đè trên phần mở rộng bản đáy
            double gSoil = (r.Lbx * r.Lby - r.Lox * r.Loy) * (i.GammaS * Math.Min(hs, i.Hgw) + (i.GammaS - gw) * Math.Max(0, hs - Math.Max(i.Hgw, 0)));
            r.BuoyG = vol * GammaConcrete + Math.Max(0, gSoil);
            double hsub = Math.Max(0, r.Hout - i.Hgw);
            r.BuoyU = gw * hsub * r.Lbx * r.Lby;
            r.Calc.Add("## Chống đẩy nổi (bể rỗng)");
            r.Calc.Add($"U = γw·hsub·A = {Dt.F(gw)}·{Dt.F(hsub, "0.###")}·{Dt.F(r.Lbx * r.Lby, "0.###")} = {Dt.F(r.BuoyU, "0.#")} kN; G = trọng lượng bê tông {Dt.F(vol * GammaConcrete, "0.#")} + đất trên phần mở rộng {Dt.F(Math.Max(0, gSoil), "0.#")} = {Dt.F(r.BuoyG, "0.#")} kN; yêu cầu G ≥ 1,1·U = {Dt.F(1.1 * r.BuoyU, "0.#")} kN.");
            if (r.BuoyU > 1e-9)
            {
                r.Checks.Add(Dt.Check("Chống đẩy nổi G ≥ 1,1·U", 1.1 * r.BuoyU, r.BuoyG, "kN"));
                if (r.BuoyG < 1.1 * r.BuoyU)
                    r.Messages.Add($"LỖI bể {r.Name} bị đẩy nổi khi rỗng: G = {Dt.F(r.BuoyG, "0.#")} kN < 1,1·U = {Dt.F(1.1 * r.BuoyU, "0.#")} kN – tăng chiều dày bản đáy, mở rộng bản đáy ra ngoài thành (đất đè) hoặc neo chống đẩy nổi; không để bể rỗng mùa mưa.");
            }
        }

        // ---------------- thép, khối lượng ----------------
        BuildBars(i, s, r, cov);
        RebarNumbering.Renumber(r.Bars);
        r.ConcreteVolume = WaterTankFormulas.ConcreteVolume(i.Lx, i.Ly, i.H, i.Tw, i.Tb, tc, ug ? i.Toe : 0);
        double perimIn = 2 * (i.Lx + i.Ly), perimOut = 2 * (r.Lox + r.Loy), perimB = 2 * (r.Lbx + r.Lby);
        r.FormworkArea = perimIn * i.H + perimOut * (i.H + tc) + perimB * i.Tb + (covered ? i.Lx * i.Ly : 0) ;
        r.LeanVolume = (r.Lbx + 0.2) * (r.Lby + 0.2) * s.LeanThickness;
        double perimC = 2 * (Lc1 + Lc2);
        r.Extra.Add(("Băng cản nước PVC (waterstop) mạch ngừng chân thành" + (covered ? " và đỉnh thành" : ""), "m", perimC * (covered ? 2 : 1)));
        r.Extra.Add(("Chống thấm mặt trong bể (thành + đáy" + (covered ? " + mặt dưới nắp" : "") + ")", "m²", perimIn * i.H + i.Lx * i.Ly * (covered ? 2 : 1)));
        if (ug) r.Extra.Add(("Chống thấm mặt ngoài thành, đáy (bể ngầm)", "m²", perimOut * (i.H + tc + i.Tb) + r.Lbx * r.Lby));
        r.Extra.Add(("Ống chờ cấp, thoát, tràn, xả cặn (chôn sẵn có mặt bích chống thấm)", "bộ", 1));
        if (covered && i.Manhole > 0) r.Extra.Add(("Nắp thăm kín nước " + Dt.Mm(i.Manhole) + "×" + Dt.Mm(i.Manhole), "cái", 1));

        if (hw > 0 && i.H - hw < 0.15) r.Messages.Add("Khoảng không trên mặt nước < 150 mm – bố trí ống tràn, ống thông hơi đúng cao độ.");
        if (i.Tw < 0.15 && i.H > 1.5) r.Messages.Add("Thành dày < 150 mm với H > 1,5 m – khó đổ bê tông 2 lớp thép, đề nghị ≥ 200 mm.");
        r.Calc.Add("## Kết luận");
        r.Calc.Add(r.Ok ? "Bể đạt các kiểm tra cường độ, bề rộng vết nứt" + (ug ? ", chống đẩy nổi" : "") + "." : "Bể có hạng mục KHÔNG ĐẠT – xem các thông báo LỖI.");
    }

    /// <summary>Tính thép một mặt (dải 1 m): As yêu cầu cường độ (+ N/(2Rs) khi có kéo), chọn thép (người dùng hoặc tự chọn ≥ Ø10a200),
    /// kiểm tra nứt; tự chọn thì tăng thép tới khi acrc ≤ giới hạn.</summary>
    private static TankFace DesignFace(string name, double h, double cover, double m, double mSer, double n, double nSer,
        int userD, double userS, int[] dias, Material conc, Material steel, double crackLim, WaterTankResult r)
    {
        var f = new TankFace { Name = name, H = h, M = m, Mser = mSer, N = n, Nser = nSer };
        int dRef = dias.Min();
        double h0 = h - cover - dRef / 2000.0;
        var fl = RcSection.DesignRect(m, 1.0, h0, cover + 0.006, conc, steel, 0.1);
        double asMin = Dt.AreaPerM(dias.Min() < 10 ? 8 : 10, 0.20);
        f.AsReq = Math.Max(fl.As + n / (2 * steel.Rs), asMin);
        if (!fl.Ok) r.Messages.Add("LỖI " + name + ": " + fl.Note);

        bool Pass(int d, double sp)
        {
            if (Dt.AreaPerM(d, sp) < f.AsReq - 1e-12) return false;
            var cw = WaterTankFormulas.CrackWidth(mSer, nSer, h, cover, d, sp, steel.E);
            return cw.Acrc <= crackLim + 1e-12;
        }

        if (userD > 0 && userS > 0)
        {
            f.UserChoice = true; f.D = userD; f.S = userS;
        }
        else
        {
            var (d0, s0) = Dt.ChooseSpaced(f.AsReq, dias, 0.20, 0.10);
            f.D = d0; f.S = s0;
            if (!Pass(f.D, f.S))
            {
                // tăng thép: duyệt (Ø, bước) theo diện tích tăng dần
                var cands = new List<(int D, double S, double A)>();
                foreach (int d in dias)
                    for (int k = 20; k >= 10; k--) { double sp = k / 100.0; cands.Add((d, sp, Dt.AreaPerM(d, sp))); }
                var ok = cands.OrderBy(c => c.A).FirstOrDefault(c => c.A >= f.AsReq - 1e-12 && Pass(c.D, c.S));
                if (ok.D > 0) { f.IncreasedForCrack = Dt.AreaPerM(ok.D, ok.S) > Dt.AreaPerM(f.D, f.S) + 1e-12 || ok.D != f.D; f.D = ok.D; f.S = ok.S; }
                else { var mx = cands.OrderByDescending(c => c.A).First(); f.D = mx.D; f.S = mx.S; }
            }
        }
        double h0p = h - cover - f.D / 2000.0;
        f.Mu = RcSection.MomentCapacity(1.0, h0p, Math.Max(0, f.AsProv - n / (2 * steel.Rs)), 0, cover, conc, steel);
        var crack = WaterTankFormulas.CrackWidth(mSer, nSer, h, cover, f.D, f.S, steel.E);
        f.Crack = crack.Acrc; f.SigmaS = crack.SigmaS;

        r.Checks.Add(Dt.Check(name + " – uốn M ≤ Mu (" + f.Text + ")", m, f.Mu, "kN.m/m"));
        r.Checks.Add(Dt.Check(name + " – bề rộng vết nứt acrc ≤ " + Dt.F(crackLim * 1000, "0.0#") + " mm", f.Crack * 1000, crackLim * 1000, "mm"));
        r.Calc.Add($"{name}: h = {Dt.Mm(h)} mm, h0 = {Dt.Mm(h0p)} mm; M = {Dt.F(m, "0.##")} kN.m/m{(n > 0 ? ", N = " + Dt.F(n, "0.##") + " kN/m" : "")} → As,yc = {Dt.F(f.AsReq * 1e4, "0.##")} cm²/m; "
                   + $"chọn {f.Text} (As = {Dt.F(f.AsProv * 1e4, "0.##")} cm²/m{(f.UserChoice ? ", người dùng chọn" : "")}{(f.IncreasedForCrack ? ", tăng thép để hạn chế nứt" : "")}), Mu = {Dt.F(f.Mu, "0.##")} kN.m/m.");
        r.Calc.Add($"  Nứt: Mser = {Dt.F(mSer, "0.##")} kN.m/m, σs = Mser/(0,9h0·As){(nSer > 0 ? " + N/(2As)" : "")} = {Dt.F(f.SigmaS / 1000, "0.#")} MPa, ls = {Dt.Mm(crack.Ls)} mm, "
                   + $"acrc = 1,4·0,5·1·1·σs/Es·ls = {Dt.F(f.Crack * 1000, "0.###")} mm {(f.Crack <= crackLim + 1e-12 ? "≤" : ">")} {Dt.F(crackLim * 1000, "0.0#")} mm.");
        if (f.AsProv < f.AsReq - 1e-12)
            r.Messages.Add($"LỖI {name}: thép chọn {f.Text} ({Dt.F(f.AsProv * 1e4, "0.##")} cm²/m) < yêu cầu {Dt.F(f.AsReq * 1e4, "0.##")} cm²/m.");
        if (f.Crack > crackLim + 1e-12)
        {
            string sug = Suggest(f, dias, mSer, nSer, h, cover, steel, crackLim);
            r.Messages.Add($"LỖI {name}: bề rộng vết nứt {Dt.F(f.Crack * 1000, "0.###")} mm > {Dt.F(crackLim * 1000, "0.0#")} mm" + (sug != null ? " – đề xuất " + sug : " – tăng chiều dày") + ".");
        }
        else if (f.IncreasedForCrack)
            r.Messages.Add($"{name}: đã tăng thép lên {f.Text} để bề rộng vết nứt ≤ {Dt.F(crackLim * 1000, "0.0#")} mm (yêu cầu cường độ {Dt.F(f.AsReq * 1e4, "0.##")} cm²/m).");
        return f;
    }

    private static string Suggest(TankFace f, int[] dias, double mSer, double nSer, double h, double cover, Material steel, double lim)
    {
        foreach (var c in dias.SelectMany(d => Enumerable.Range(10, 11).Select(k => (D: d, S: k / 100.0))).OrderBy(c => Dt.AreaPerM(c.D, c.S)))
            if (Dt.AreaPerM(c.D, c.S) >= f.AsReq && WaterTankFormulas.CrackWidth(mSer, nSer, h, cover, c.D, c.S, steel.E).Acrc <= lim) return Dt.Spaced(c.D, c.S);
        return null;
    }

    // ------------------------------------------------------------------
    // Thép
    // ------------------------------------------------------------------
    private static void BuildBars(WaterTankInput i, DetailSettings s, WaterTankResult r, double c)
    {
        bool covered = r.Covered;
        double tc = covered ? i.Tc : 0;
        double perimIn = 2 * (i.Lx + i.Ly), perimOut = 2 * (r.Lox + r.Loy);
        // thép đứng: chân bẻ vào bản đáy (L), đỉnh bẻ vào nắp (U) nếu có nắp
        double vert = r.Hout - 2 * c;
        var vi = r.WallVertIn; var vo = r.WallVertOut; var vh = r.WallHor;
        double footIn = Dt.R10(Math.Max(0.20, 15 * vi.D / 1000.0)), footOut = Dt.R10(Math.Max(0.20, 15 * vo.D / 1000.0));
        double topIn = covered ? Dt.R10(Math.Max(0.10, 10 * vi.D / 1000.0)) : 0, topOut = covered ? Dt.R10(Math.Max(0.10, 10 * vo.D / 1000.0)) : 0;
        r.Bars.Add(Dt.Hooked("Thép đứng thành – mặt trong", vi.D, (int)Math.Ceiling(perimIn / vi.S), vert, footIn, topIn, false, vi.S));
        r.Bars.Add(Dt.Hooked("Thép đứng thành – mặt ngoài", vo.D, (int)Math.Ceiling(perimOut / vo.S), vert, footOut, topOut, false, vo.S));
        // thép ngang: thanh thẳng từng thành mỗi mặt + thép góc chữ L nối hai thành
        int rows = (int)Math.Floor((i.H - 2 * 0.05) / vh.S + 1e-6) + 1;
        double lap = Dt.R10(Math.Max(40 * vh.D / 1000.0, 0.30));
        double lenInX = i.Lx - 2 * 0.0, lenInY = i.Ly;
        double lenOutX = r.Lox - 2 * c, lenOutY = r.Loy - 2 * c;
        r.Bars.Add(Dt.Straight("Thép ngang thành – mặt trong (thành Lx)", vh.D, 2 * rows, lenInX, false, vh.S));
        r.Bars.Add(Dt.Straight("Thép ngang thành – mặt trong (thành Ly)", vh.D, 2 * rows, lenInY, false, vh.S));
        r.Bars.Add(Dt.Straight("Thép ngang thành – mặt ngoài (thành Lx)", vh.D, 2 * rows, lenOutX, false, vh.S));
        r.Bars.Add(Dt.Straight("Thép ngang thành – mặt ngoài (thành Ly)", vh.D, 2 * rows, lenOutY, false, vh.S));
        r.Bars.Add(Dt.Hooked("Thép góc thành (chữ L, 2 mặt)", vh.D, 4 * 2 * rows, lap, lap, 0, false, vh.S));
        foreach (var (len, nm) in new[] { (lenOutX, "Lx"), (lenOutY, "Ly") })
            if (len > s.StockLength)
                r.Messages.Add($"Thép ngang thành {nm} dài {Dt.F(len, "0.##")} m > {Dt.F(s.StockLength, "0.#")} m – nối chồng ll ≥ {Dt.Mm(lap)} mm, so le, tránh vùng góc bể.");
        if (vert > s.StockLength) r.Messages.Add("Thép đứng thành dài hơn chiều dài cây thép – nối chồng ở 1/3 giữa chiều cao.");

        // bản đáy: 2 lớp × 2 phương, bẻ móc lên/xuống ở mép
        var bt = r.BottomTop; var bb = r.BottomBot;
        double leg = Dt.R10(Math.Max(i.Tb - 2 * c, 0.10));
        double bx = r.Lbx - 2 * c, by = r.Lby - 2 * c;
        r.Bars.Add(Dt.Hooked("Lớp dưới bản đáy – phương X", bb.D, Dt.CountAcross(r.Lby, c, bb.S), bx, leg, leg, false, bb.S));
        r.Bars.Add(Dt.Hooked("Lớp dưới bản đáy – phương Y", bb.D, Dt.CountAcross(r.Lbx, c, bb.S), by, leg, leg, false, bb.S));
        r.Bars.Add(Dt.Hooked("Lớp trên bản đáy – phương X", bt.D, Dt.CountAcross(r.Lby, c, bt.S), bx, leg, leg, true, bt.S));
        r.Bars.Add(Dt.Hooked("Lớp trên bản đáy – phương Y", bt.D, Dt.CountAcross(r.Lbx, c, bt.S), by, leg, leg, true, bt.S));
        if (bx > s.StockLength || by > s.StockLength) r.Messages.Add("Thép bản đáy dài hơn chiều dài cây thép – nối chồng so le 50%, lớp dưới nối ở giữa nhịp, lớp trên nối gần thành.");
        // thép chờ đai giá đỡ lưới trên (chân chó) – cấu tạo
        int chairs = Math.Max(4, (int)Math.Ceiling(r.Lbx * r.Lby));
        r.Bars.Add(Dt.Hooked("Thép kê (chân chó) đỡ lưới trên bản đáy", 12, chairs, Dt.R10(i.Tb - 2 * c), 0.20, 0.20));

        if (covered && r.CoverBot != null)
        {
            var cb = r.CoverBot;
            double cc = Math.Max(s.CoverSlab, 0.02);
            double cxl = r.Lox - 2 * cc, cyl = r.Loy - 2 * cc;
            r.Bars.Add(Dt.Straight("Lớp dưới bản nắp – phương X", cb.D, Dt.CountAcross(r.Loy, cc, cb.S), cxl, false, cb.S));
            r.Bars.Add(Dt.Straight("Lớp dưới bản nắp – phương Y", cb.D, Dt.CountAcross(r.Lox, cc, cb.S), cyl, false, cb.S));
            // mũ bản nắp (cấu tạo ≥ 50% lớp dưới, vươn L1/4 + bẻ xuống thành)
            int dTop = Math.Max(8, cb.D - 2); double sTop = 0.20;
            double reach = Dt.R10(Math.Min(i.Lx, i.Ly) / 4 + i.Tw - cc);
            double down = Dt.R10(Math.Max(tc - 2 * cc, 0.06) + 0.15);
            r.Bars.Add(Dt.Hooked("Mũ bản nắp – cạnh dài", dTop, 2 * Dt.CountAcross(r.Lox, cc, sTop), reach, down, Dt.R10(Math.Max(tc - 2 * cc, 0.05)), true, sTop));
            r.Bars.Add(Dt.Hooked("Mũ bản nắp – cạnh ngắn", dTop, 2 * Dt.CountAcross(r.Loy, cc, sTop), reach, down, Dt.R10(Math.Max(tc - 2 * cc, 0.05)), true, sTop));
            if (i.Manhole > 0)
                r.Bars.Add(Dt.Straight("Tăng cường lỗ thăm", 12, 8, Dt.R10(i.Manhole + 2 * 0.40)));
        }
    }
}

/// <summary>Bản vẽ bể nước: mặt bằng (kích thước, lỗ thăm, vạch cắt) và mặt cắt 1-1 (thành, đáy, nắp, mực nước, thép, nhãn).</summary>
public static class WaterTankDrawing
{
    public const double SheetScale = 50;
    public const double PlanScale = 50;
    public const double SectionScale = 25;

    public static List<Figure> Build(WaterTankInput i, WaterTankResult r, DetailSettings s)
    {
        var list = new List<Figure>();
        if (r == null || r.Lox <= 0 || r.WallVertIn == null) return list;
        if (r.Bars.Any(b => b.Mark <= 0)) RebarNumbering.Renumber(r.Bars);
        list.Add(new Figure(Plan(i, r), "MẶT BẰNG " + r.Name));
        list.Add(new Figure(Section(i, r, s), "MẶT CẮT 1-1 " + r.Name));
        return list;
    }

    private static int Mark(WaterTankResult r, string rolePrefix) => r.Bars.FirstOrDefault(b => b.Role.StartsWith(rolePrefix, StringComparison.Ordinal))?.Mark ?? 0;

    public static DxfWriter Plan(WaterTankInput i, WaterTankResult r)
    {
        double S = SheetScale, k = S / PlanScale;
        var w = new DxfWriter(S);
        double th = w.Th;
        double tw = i.Tw * 1000 * k, lox = r.Lox * 1000 * k, loy = r.Loy * 1000 * k;
        double toe = r.Underground ? i.Toe * 1000 * k : 0;
        if (toe > 0) w.Rect(Lay.BeTong, -toe, -toe, lox + toe, loy + toe, "DASHED");
        w.Rect(Lay.BeTong, 0, 0, lox, loy);
        w.Rect(Lay.BeTong, tw, tw, lox - tw, loy - tw);
        if (r.Covered && i.Manhole > 0)
        {
            double m = i.Manhole * 1000 * k;
            double x0 = lox - tw - m - 150 * k, y0 = loy - tw - m - 150 * k;
            w.Rect(Lay.BeTong, x0, y0, x0 + m, y0 + m);
            w.Line(Lay.BeTong, x0, y0, x0 + m, y0 + m);
            w.Line(Lay.BeTong, x0, y0 + m, x0 + m, y0);
            w.Text(Lay.Chu, x0 + m / 2, y0 - 0.6 * th, 0.8 * th, "LỖ THĂM " + Dt.Mm(i.Manhole) + "×" + Dt.Mm(i.Manhole), 0, 1, 2);
        }
        // trục tim thành
        w.Line(Lay.Truc, tw / 2, -3 * th, tw / 2, loy + 3 * th, "CENTER");
        w.Line(Lay.Truc, lox - tw / 2, -3 * th, lox - tw / 2, loy + 3 * th, "CENTER");
        // vạch cắt 1-1 (theo phương X qua giữa bể)
        double yc = loy / 2;
        w.CutMark(-2.5 * th - toe, yc, -1.0 * th - toe, yc, "1", -3.2 * th - toe, yc + 0.9 * th);
        w.CutMark(lox + 1.0 * th + toe, yc, lox + 2.5 * th + toe, yc, "1", lox + 3.2 * th + toe, yc + 0.9 * th);
        // kích thước
        w.DimChainH(new List<double> { 0, tw, lox - tw, lox }, 0, -3 * th - toe, k);
        w.Dim(0, 0, lox, 0, -6 * th - toe, k);
        w.DimChainV(new List<double> { 0, tw, loy - tw, loy }, 0, -3 * th - toe, k);
        w.Dim(0, 0, 0, loy, 6 * th + toe, k);
        w.Text(Lay.Chu, lox / 2, loy / 2, th, (r.Covered ? "BỂ NƯỚC CÓ NẮP" : "BỂ NƯỚC HỞ") + " – THÀNH " + Dt.Mm(i.Tw), 0, 1, 2);
        w.Title(lox / 2, w.Extents.Y2 + 4 * th, $"MẶT BẰNG {r.Name} (SL={Math.Max(1, r.Quantity)})", DrawUtil.ScaleText(PlanScale));
        return w;
    }

    public static DxfWriter Section(WaterTankInput i, WaterTankResult r, DetailSettings s)
    {
        double S = SheetScale, k = S / SectionScale;
        var w = new DxfWriter(S);
        double th = w.Th;
        double mm = 1000 * k;
        double tw = i.Tw * mm, tb = i.Tb * mm, tc = r.Covered ? i.Tc * mm : 0, H = i.H * mm;
        double lox = r.Lox * mm, toe = r.Underground ? i.Toe * mm : 0;
        double c = Math.Max(s.CoverTank, 0.02) * mm;
        double hw = Math.Min(i.Hw, i.H) * mm;
        double yTopB = tb, yTopW = tb + H, yTop = tb + H + tc;

        // hatch (nếu bật)
        if (w.HatchOn)
        {
            w.HatchMatRect(HatchMaterial.LeanConcrete, -toe - 100 * k, -100 * k, lox + toe + 100 * k, 0);
            w.HatchMatRect(HatchMaterial.Concrete, -toe, 0, lox + toe, tb);
            w.HatchMatRect(HatchMaterial.Concrete, 0, tb, tw, yTopW);
            w.HatchMatRect(HatchMaterial.Concrete, lox - tw, tb, lox, yTopW);
            if (tc > 0) w.HatchMatRect(HatchMaterial.Concrete, 0, yTopW, lox, yTop);
        }
        // bê tông lót
        w.Rect(Lay.BeTong, -toe - 100 * k, -100 * k, lox + toe + 100 * k, 0);
        // bản đáy, thành, nắp
        w.Rect(Lay.BeTong, -toe, 0, lox + toe, tb);
        w.Polyline(Lay.BeTong, new List<(double X, double Y)> { (0, tb), (0, yTop), (lox, yTop), (lox, tb) }, false);
        w.Polyline(Lay.BeTong, new List<(double X, double Y)> { (tw, tb), (tw, yTopW), (lox - tw, yTopW), (lox - tw, tb) }, false);
        if (tc <= 0) { w.Line(Lay.BeTong, 0, yTopW, tw, yTopW); w.Line(Lay.BeTong, lox - tw, yTopW, lox, yTopW); }
        // mặt đất (bể ngầm)
        if (r.Underground)
        {
            double xg0 = -toe - 6 * th, xg1 = lox + toe + 6 * th;
            w.Line(Lay.KyHieu, xg0, yTop, -toe - 0.5 * th, yTop);
            w.Line(Lay.KyHieu, lox + toe + 0.5 * th, yTop, xg1, yTop);
            for (double x = xg0; x < -toe - th; x += 1.2 * th) w.Line(Lay.KyHieu, x, yTop, x - 0.6 * th, yTop - 0.6 * th);
            w.Text(Lay.Chu, xg0, yTop + 0.4 * th, 0.8 * th, "MẶT ĐẤT", 0, 0, 0);
        }
        // mực nước
        if (hw > 0)
        {
            double yw = tb + hw;
            w.Line(Lay.KyHieu, tw, yw, lox - tw, yw, "DASHED");
            w.Text(Lay.Chu, lox / 2, yw + 0.4 * th, 0.9 * th, "MỰC NƯỚC MAX", 0, 1, 0);
            w.ElevMark(lox * 0.70, yw, "MN max +" + Math.Min(i.Hw, i.H).ToString("0.000", Dt.Inv), 1);
        }

        // ---- thép ----
        double dvi = r.WallVertIn.D * k, dvo = r.WallVertOut.D * k;
        double xIn = tw - c - dvi / 2, xOut = c + dvo / 2;
        double yb0 = c, yb1 = tb - c;
        double footIn = Math.Max(200, 15 * r.WallVertIn.D) * k, footOut = Math.Max(200, 15 * r.WallVertOut.D) * k;
        double yTopBar = yTop - c;
        // tường trái và phải (đối xứng)
        foreach (int side in new[] { 0, 1 })
        {
            double X(double x) => side == 0 ? x : lox - x;
            int dir = side == 0 ? 1 : -1;
            var inn = new List<(double X, double Y)> { (X(xIn + dir * footIn), yb0 + dvi), (X(xIn), yb0 + dvi), (X(xIn), yTopBar) };
            if (tc > 0) inn.Add((X(xIn + dir * Math.Max(100, 10 * r.WallVertIn.D) * k), yTopBar));
            w.Polyline(Lay.Thep, inn, false, 0.35 * S);
            var outer = new List<(double X, double Y)> { (X(xOut + dir * footOut), yb0 + dvo), (X(xOut), yb0 + dvo), (X(xOut), yTopBar) };
            if (tc > 0) outer.Add((X(xOut + dir * Math.Max(100, 10 * r.WallVertOut.D) * k), yTopBar));
            w.Polyline(Lay.Thep, outer, false, 0.35 * S);
            // thép ngang thành: chấm hai mặt
            double sh = r.WallHor.S * mm, dh = Math.Max(r.WallHor.D * k / 2, 0.12 * th);
            for (double y = tb + 50 * k; y <= yTopW - 50 * k + 1e-6; y += sh)
            {
                w.Dot(Lay.Thep, X(xIn - dir * (dvi / 2 + dh)), y, dh);
                w.Dot(Lay.Thep, X(xOut + dir * (dvo / 2 + dh)), y, dh);
            }
        }
        // bản đáy: 2 lớp
        double bL = -toe + c, bR = lox + toe - c;
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (bL, yb1), (bL, yb0), (bR, yb0), (bR, yb1) }, false, 0.35 * S);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (bL, yb0), (bL, yb1), (bR, yb1), (bR, yb0) }, false, 0.35 * S);
        double sB = Math.Max(50 * k, r.BottomBot.S * mm);
        for (double x = bL + sB; x < bR - 1e-6; x += sB)
        {
            w.Dot(Lay.Thep, x, yb0 + r.BottomBot.D * k * 1.5, Math.Max(r.BottomBot.D * k / 2, 0.12 * th));
            w.Dot(Lay.Thep, x, yb1 - r.BottomTop.D * k * 1.5, Math.Max(r.BottomTop.D * k / 2, 0.12 * th));
        }
        // bản nắp
        if (tc > 0 && r.CoverBot != null)
        {
            double cc = Math.Max(s.CoverSlab, 0.02) * mm;
            double yc0 = yTopW + cc;
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (cc, yc0), (lox - cc, yc0) }, false, 0.35 * S);
            double reach = (Math.Min(i.Lx, i.Ly) / 4) * mm + tw;
            double yc1 = yTop - cc;
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (cc, yTopW - 150 * k), (cc, yc1), (reach, yc1), (reach, yc1 - Math.Max(tc - 2 * cc, 30 * k)) }, false, 0.3 * S);
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (lox - cc, yTopW - 150 * k), (lox - cc, yc1), (lox - reach, yc1), (lox - reach, yc1 - Math.Max(tc - 2 * cc, 30 * k)) }, false, 0.3 * S);
            w.LeaderTag(lox * 0.55, yc0, lox * 0.55 + 2 * th, yTop + 3.5 * th, r.CoverBot.Text, Mark(r, "Lớp dưới bản nắp"), 1);
            int mt = Mark(r, "Mũ bản nắp");
            var top = r.Bars.FirstOrDefault(b => b.Role.StartsWith("Mũ bản nắp", StringComparison.Ordinal));
            if (top != null) w.LeaderTag(reach * 0.6, yc1, reach * 0.6 + 2 * th, yTop + 6 * th, Dt.Spaced(top.Dia, top.Spacing), mt, 1);
        }
        // nhãn thép
        double yTag = tb + H * 0.55;
        w.LeaderTag(xIn, yTag, tw + 3 * th, yTag + 1.5 * th, r.WallVertIn.Text, Mark(r, "Thép đứng thành – mặt trong"), 1);
        w.LeaderTag(xOut, yTag - 2.5 * th, -toe - 3 * th, yTag - 2.5 * th, r.WallVertOut.Text, Mark(r, "Thép đứng thành – mặt ngoài"), -1);
        w.LeaderTag(lox - xIn + dvi, tb + H * 0.30, lox - tw - 3 * th, tb + H * 0.30, r.WallHor.Text, Mark(r, "Thép ngang thành – mặt trong"), -1);
        w.LeaderTag(lox * 0.30, yb0, lox * 0.30 + 2 * th, -100 * k - 3.5 * th, r.BottomBot.Text, Mark(r, "Lớp dưới bản đáy"), 1);
        w.LeaderTag(lox * 0.62, yb1, lox * 0.62 + 2 * th, tb + 3 * th, r.BottomTop.Text, Mark(r, "Lớp trên bản đáy"), 1);

        // kích thước
        var ys = new List<double> { 0, tb, yTopW };
        if (tc > 0) ys.Add(yTop);
        w.DimChainV(ys, lox + toe, lox + toe + 4 * th, k);
        w.Dim(lox + toe, -100 * k, lox + toe, 0, -(4 * th), k);
        w.DimChainH(new List<double> { 0, tw, lox - tw, lox }, 0, -100 * k - 7 * th, k);
        if (toe > 0) w.DimChainH(new List<double> { -toe, 0, lox, lox + toe }, 0, -100 * k - 10 * th, k);
        // cao độ
        w.ElevMark(-toe - 2 * th, tb, "±0.000", -1);
        w.ElevMark(-toe - 2 * th, yTop, "+" + (i.H + (r.Covered ? i.Tc : 0)).ToString("0.000", Dt.Inv), -1);
        w.Title(lox / 2, w.Extents.Y1 - 1.2 * th, "MẶT CẮT 1-1 " + r.Name, DrawUtil.ScaleText(SectionScale), true);
        return w;
    }
}
