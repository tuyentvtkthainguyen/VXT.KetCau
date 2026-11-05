using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// HỐ PÍT THANG MÁY + VÁCH (GIẾNG) THANG MÁY BTCT – chế độ Triển khai hồ sơ.
//  • Thành hố pít: bản chịu áp lực đất chủ động + nước ngầm (WaterTankFormulas – phương pháp dải Grashof),
//    chân ngàm vào đáy hố pít, đỉnh khớp tại sàn / dầm tầng 1; bề rộng vết nứt ≤ 0,2 mm (chống thấm).
//  • Đáy hố pít: bản kê 4 cạnh ngàm vào thành, chịu đẩy nổi / phản lực nền; giảm chấn cabin, đối trọng: lực tập trung
//    R = k·(P + Q)·g (EN 81-20 5.8.2: k = 4) – kiểm tra chọc thủng, áp lực nền dưới đáy hố.
//  • Chống đẩy nổi giai đoạn thi công (chưa có kết cấu bên trên): G ≥ 1,1·U.
//  • Vách thang máy: vách BTCT dày t, thép 2 lớp mỗi phương, hàm lượng μ ≥ 0,25 % mỗi phương (TCVN 5574:2018 mục 10.3.3 – vách),
//    lỗ cửa tầng: thép gia cường mép lỗ, thép xiên góc, dầm lanh tô cửa thang máy.
// Đơn vị: m, kN, kN/m², kN.m/m; đường kính thép mm.
// =====================================================================

/// <summary>Kết quả hố pít – vách thang máy.</summary>
public class LiftPitResult : DetailResult
{
    public double Ka { get; set; }
    public double PTop { get; set; }
    public double PBase { get; set; }
    public double AlphaV { get; set; }
    public double MWallBase { get; set; }
    public double MWallSpan { get; set; }
    public double MWallHor { get; set; }
    public TankFace WallIn { get; set; }
    public TankFace WallOut { get; set; }
    public TankFace WallHor { get; set; }
    public TankFace SlabTop { get; set; }
    public TankFace SlabBot { get; set; }
    public double QSlab { get; set; }
    public double BufferCar { get; set; }
    public double BufferCwt { get; set; }
    public double PunchCap { get; set; }
    public double SoilP { get; set; }
    public double BuoyG { get; set; }
    public double BuoyU { get; set; }
    public double ShaftHeight { get; set; }
    public double Mu { get; set; }
    public int ShaftD { get; set; }
    public double ShaftS { get; set; }
    public double Lox { get; set; }
    public double Loy { get; set; }
}

public class ManualLiftPit : IDetailMember
{
    [Field("Tên hố pít / vách thang máy", Group = "Chung")] public string Name { get; set; } = "HTM1";
    [Field("Số lượng", Group = "Chung")] public int Quantity { get; set; } = 1;

    [Field("Giếng thang – bề rộng thông thuỷ W", Unit = "mm", Scale = 1000, Group = "Kích thước", Hint = "theo catalogue thang máy (cabin 450 kg ≈ 1600 × 1700)")] public double ShaftW { get; set; } = 1.80;
    [Field("Giếng thang – chiều sâu thông thuỷ D", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double ShaftD { get; set; } = 1.80;
    [Field("Chiều dày vách / thành hố pít", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Wall { get; set; } = 0.20;
    [Field("Chiều sâu hố pít (từ mặt sàn tầng 1 tới mặt đáy)", Unit = "mm", Scale = 1000, Group = "Kích thước", Hint = "theo catalogue, thường 1200…1500")] public double PitDepth { get; set; } = 1.40;
    [Field("Chiều dày đáy hố pít", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double PitSlab { get; set; } = 0.30;
    [Field("Số tầng phục vụ (số cửa tầng)", Group = "Kích thước")] public int Storeys { get; set; } = 4;
    [Field("Chiều cao tầng", Unit = "m", Group = "Kích thước")] public double StoreyHeight { get; set; } = 3.3;
    [Field("Chiều cao đỉnh giếng OH (trên tầng trên cùng)", Unit = "m", Group = "Kích thước")] public double Overhead { get; set; } = 4.0;
    [Field("Cửa tầng – bề rộng lỗ", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double DoorW { get; set; } = 0.90;
    [Field("Cửa tầng – chiều cao lỗ", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double DoorH { get; set; } = 2.20;
    [Field("Dầm lanh tô cửa – chiều cao", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double LintelH { get; set; } = 0.30;

    [Field("Trọng lượng riêng đất γ", Unit = "kN/m³", Group = "Đất, nước ngầm")] public double GammaS { get; set; } = 18;
    [Field("Góc ma sát trong φ", Unit = "độ", Group = "Đất, nước ngầm")] public double Phi { get; set; } = 20;
    [Field("Phụ tải mặt đất q", Unit = "kN/m²", Group = "Đất, nước ngầm")] public double Surcharge { get; set; } = 10;
    [Field("Độ sâu mực nước ngầm (từ mặt sàn tầng 1)", Unit = "mm", Scale = 1000, Group = "Đất, nước ngầm", Hint = "lớn = không có nước ngầm")] public double Hgw { get; set; } = 0.80;
    [Field("Vách giếng thang đặt trên", Group = "Đất, nước ngầm", Choices = "Dầm móng / móng công trình|Hố pít (đáy hố là móng của vách)",
        Hint = "nhà phố: vách giếng thường đỡ bởi dầm móng – đáy hố chỉ chịu thành hố, giảm chấn, nước ngầm")]
    public string ShaftSupport { get; set; } = "Dầm móng / móng công trình";
    public bool ShaftOnPit => (ShaftSupport ?? "").StartsWith("Hố pít");
    [Field("Sức chịu tải nền dưới đáy hố R", Unit = "kN/m²", Group = "Đất, nước ngầm")] public double SoilR { get; set; } = 120;

    [Field("Tải định mức thang Q", Unit = "kg", Group = "Thang máy")] public double RatedLoad { get; set; } = 450;
    [Field("Khối lượng cabin P", Unit = "kg", Group = "Thang máy")] public double CarMass { get; set; } = 600;
    [Field("Hệ số giảm chấn (EN 81-20: 4)", Group = "Thang máy")] public double BufferFactor { get; set; } = 4;
    [Field("Bề rộng đế giảm chấn", Unit = "mm", Scale = 1000, Group = "Thang máy")] public double BufferBase { get; set; } = 0.30;

    [Field("Bề rộng vết nứt giới hạn", Unit = "mm", Scale = 1000, Group = "Kiểm tra")] public double CrackLimit { get; set; } = 0.0002;
    [Field("Thép vách thang máy Ø (2 lớp, mỗi phương)", Unit = "mm", Group = "Chọn thép")] public int ShaftBarD { get; set; } = 10;
    [Field("  bước", Unit = "mm", Scale = 1000, Group = "Chọn thép")] public double ShaftBarS { get; set; } = 0.20;

    public static ManualLiftPit Sample() => new();

    public DetailResult Run(DetailSettings s) => LiftPitDesigner.Design(this, s ?? new DetailSettings());
    public List<Figure> Draw(DetailResult r, DetailSettings s) => r is LiftPitResult lr ? LiftPitDrawing.Build(this, lr, s ?? new DetailSettings()) : new List<Figure>();
}

public static class LiftPitDesigner
{
    public const double Gc = 25, Gw = 10, G = 9.81;
    private static readonly int[] Dias = { 10, 12, 14, 16 };

    /// <summary>Lực giảm chấn (kN): R = k·m·g/1000 (m kg).</summary>
    public static double BufferForce(double massKg, double k) => k * massKg * G / 1000;

    public static LiftPitResult Design(ManualLiftPit i, DetailSettings s)
    {
        var r = new LiftPitResult { Name = i.Name ?? "HTM", Quantity = Math.Max(1, i.Quantity), Kind = "Hố pít – vách thang máy", ConcreteClass = s.Concrete };
        try
        {
            var err = new List<string>();
            if (!(i.ShaftW >= 0.8) || !(i.ShaftD >= 0.8)) err.Add("kích thước giếng ≥ 800 mm");
            if (!(i.Wall >= 0.15)) err.Add("chiều dày vách ≥ 150 mm");
            if (!(i.PitDepth >= 0.3) || i.PitDepth > 4) err.Add("chiều sâu hố pít 0,3…4 m");
            if (!(i.PitSlab >= 0.15)) err.Add("đáy hố pít ≥ 150 mm");
            if (i.Storeys < 1 || !(i.StoreyHeight > 1.5)) err.Add("số tầng ≥ 1, chiều cao tầng > 1,5 m");
            if (!(i.DoorW > 0.5) || i.DoorW > Math.Min(i.ShaftW, i.ShaftD) || !(i.DoorH > 1.8)) err.Add("lỗ cửa tầng: rộng 0,5 m…bề rộng giếng, cao > 1,8 m");
            if (err.Count > 0) { r.Messages.Add("LỖI dữ liệu " + r.Name + ": " + string.Join("; ", err) + "."); return r; }
            Core(i, s, r);
        }
        catch (Exception ex) { r.Messages.Add("LỖI tính hố pít: " + ex.Message); }
        return r;
    }

    private static void Core(ManualLiftPit i, DetailSettings s, LiftPitResult r)
    {
        var conc = s.Conc(); var main = s.Main();
        double cov = Math.Max(s.CoverTank, 0.02);
        double t = i.Wall, H = i.PitDepth, ts = i.PitSlab;
        r.Lox = i.ShaftW + 2 * t; r.Loy = i.ShaftD + 2 * t;
        double Lc1 = i.ShaftW + t, Lc2 = i.ShaftD + t;
        r.ShaftHeight = i.Storeys * i.StoreyHeight + i.Overhead;
        r.Calc.Add("## Hố pít – vách thang máy " + r.Name);
        r.Calc.Add($"Giếng thông thuỷ W × D = {Dt.Mm(i.ShaftW)} × {Dt.Mm(i.ShaftD)} mm, vách dày {Dt.Mm(t)} mm; hố pít sâu {Dt.Mm(H)} mm, đáy dày {Dt.Mm(ts)} mm; "
                   + $"{i.Storeys} tầng × {Dt.F(i.StoreyHeight)} m + OH {Dt.F(i.Overhead)} m → chiều cao vách {Dt.F(r.ShaftHeight, "0.##")} m; bê tông {s.Concrete} (chống thấm B6 cho hố pít), thép {s.RebarMain}.");

        // ---------------- thành hố pít ----------------
        r.Ka = WaterTankFormulas.Ka(i.Phi);
        r.PTop = WaterTankFormulas.SoilPressure(0, r.Ka, i.GammaS, Gw, i.Surcharge, i.Hgw);
        r.PBase = WaterTankFormulas.SoilPressure(H, r.Ka, i.GammaS, Gw, i.Surcharge, i.Hgw);
        double u = r.PTop, tri = Math.Max(0, r.PBase - r.PTop);
        var (mb, ms) = WaterTankFormulas.Vertical(u, tri, H, true);
        double L = Math.Max(Lc1, Lc2);
        r.AlphaV = WaterTankFormulas.ShareVertical(L, H, true);
        double ah = 1 - r.AlphaV;
        var (mhs, mhp) = WaterTankFormulas.Horizontal(r.PBase, L);
        r.MWallBase = r.AlphaV * mb; r.MWallSpan = r.AlphaV * ms; r.MWallHor = ah * Math.Max(mhs, mhp);
        const double fs = 1.15;
        r.Calc.Add("## Thành hố pít – áp lực đất + nước ngầm");
        r.Calc.Add($"Ka = tan²(45° − φ/2) = {Dt.F(r.Ka, "0.###")}; p(z) = Ka·(q + γ·z) + γw·(z − hn) dưới mực nước ngầm hn = {Dt.F(i.Hgw, "0.##")} m: p đỉnh = {Dt.F(r.PTop, "0.##")}, p chân = {Dt.F(r.PBase, "0.##")} kN/m² (tiêu chuẩn), hệ số 1,15.");
        r.Calc.Add($"Dải đứng chân ngàm (đáy hố), đỉnh khớp (sàn tầng 1): Mchân = u·H²/8 + t·H²/15 = {Dt.F(mb, "0.##")}, Mnhịp = {Dt.F(ms, "0.##")} kN.m/m; L/H = {Dt.F(L / H, "0.##")} → αv = {Dt.F(r.AlphaV, "0.###")}; "
                   + $"dải ngang ngàm tại góc Mgối = p·L²/12 = {Dt.F(mhs, "0.##")} kN.m/m.");
        r.WallOut = Face("Thành hố pít – thép đứng mặt ngoài (chân thành)", t, cov, fs * r.MWallBase, r.MWallBase, conc, main, i.CrackLimit, r);
        r.WallIn = Face("Thành hố pít – thép đứng mặt trong (nhịp)", t, cov, fs * r.MWallSpan, r.MWallSpan, conc, main, i.CrackLimit, r);
        r.WallHor = Face("Thành hố pít – thép ngang (2 mặt)", t, cov, fs * r.MWallHor, r.MWallHor, conc, main, i.CrackLimit, r);
        double qW = fs * (u * H / 2 + tri * H / 3) * 1.25;   // phản lực chân dải ngàm–khớp (gần đúng 5/8·qH, an toàn ×1,25)
        double h0w = t - cov - 0.006;
        r.Checks.Add(Dt.Check("Thành hố pít – cắt chân thành Q ≤ 0,5·Rbt·b·h0", qW, 0.5 * conc.Rbt * h0w, "kN/m"));

        // ---------------- đáy hố pít ----------------
        double hsub = Math.Max(0, H + ts - i.Hgw);
        double aOut = r.Lox * r.Loy;
        double perimC = 2 * (Lc1 + Lc2);
        double gPitWalls = perimC * t * H * Gc;
        double gSlab = aOut * ts * Gc;
        double gShaft = perimC * t * r.ShaftHeight * Gc - i.Storeys * i.DoorW * i.DoorH * t * Gc;
        r.BufferCar = BufferForce(i.CarMass + i.RatedLoad, i.BufferFactor);
        r.BufferCwt = BufferForce(i.CarMass + 0.5 * i.RatedLoad, i.BufferFactor);
        double qUp = Math.Max(0, 1.1 * Gw * hsub - 0.9 * Gc * ts);
        double gOnPit = gPitWalls + (i.ShaftOnPit ? gShaft : 0);
        double qSoil = Math.Max(0, 1.1 * gOnPit / aOut);     // phản lực nền do thành (+ vách giếng nếu đặt trên hố) – bản thân đáy tự cân bằng
        r.QSlab = Math.Max(qUp, qSoil);
        double L1 = Math.Min(Lc1, Lc2), L2 = Math.Max(Lc1, Lc2);
        double alpha = WaterTankFormulas.ShareShort(L1, L2);
        double mSup = alpha * r.QSlab * L1 * L1 / 12, mSpan = alpha * r.QSlab * L1 * L1 / 24;
        r.Calc.Add("## Đáy hố pít");
        r.Calc.Add($"Tải hướng lên = max(đẩy nổi 1,1·γw·hsub − 0,9·25·hđ = {Dt.F(qUp, "0.##")}; phản lực nền do " + (i.ShaftOnPit ? "vách giếng + thành hố" : "thành hố") + $" 1,1·G/A = {Dt.F(qSoil, "0.##")}) = {Dt.F(r.QSlab, "0.##")} kN/m²; "
                   + $"bản kê 4 cạnh ngàm L1 = {Dt.F(L1, "0.###")} m, α = {Dt.F(alpha, "0.###")}: Mgối = α·q·L1²/12 = {Dt.F(mSup, "0.##")}, Mnhịp = {Dt.F(mSpan, "0.##")} kN.m/m.");
        r.SlabTop = Face("Đáy hố pít – lớp trên", ts, cov, mSpan, mSpan / 1.1, conc, main, i.CrackLimit, r);
        r.SlabBot = Face("Đáy hố pít – lớp dưới", ts, Math.Max(cov, 0.035), mSup, mSup / 1.1, conc, main, i.CrackLimit, r);
        // giảm chấn: chọc thủng
        double h0s = ts - cov - 0.012;
        double ub = 4 * (i.BufferBase + h0s);
        r.PunchCap = conc.Rbt * ub * h0s;
        double pBuf = Math.Max(r.BufferCar, r.BufferCwt) * 1.2;
        r.Checks.Add(Dt.Check("Đáy hố pít – chọc thủng dưới giảm chấn F ≤ Rbt·u·h0", pBuf, r.PunchCap, "kN"));
        r.Calc.Add($"Giảm chấn cabin R = k·(P + Q)·g = {Dt.F(i.BufferFactor)}·({Dt.F(i.CarMass, "0")} + {Dt.F(i.RatedLoad, "0")})·9,81 = {Dt.F(r.BufferCar, "0.#")} kN; đối trọng {Dt.F(r.BufferCwt, "0.#")} kN; "
                   + $"chọc thủng F = 1,2·R = {Dt.F(pBuf, "0.#")} kN ≤ Rbt·u·h0 = {Dt.F(r.PunchCap, "0.#")} kN (u = 4·(a + h0) = {Dt.F(ub, "0.###")} m).");
        // áp lực nền
        r.SoilP = (1.0 * (gOnPit + gSlab) + r.BufferCar + r.BufferCwt) / aOut - Gw * hsub;
        r.Checks.Add(Dt.Check("Áp lực nền dưới đáy hố pít p ≤ R", Math.Max(r.SoilP, 0), i.SoilR, "kN/m²"));
        r.Calc.Add($"Áp lực nền (tiêu chuẩn" + (i.ShaftOnPit ? ", vách giếng đặt trên hố pít" : ", vách giếng đỡ bởi dầm móng") + $"): p = (G thành" + (i.ShaftOnPit ? " + vách" : "") + $" + đáy + R giảm chấn)/A − γw·hsub = {Dt.F(r.SoilP, "0.#")} kN/m² ≤ R = {Dt.F(i.SoilR, "0")} kN/m². "
                   + (i.ShaftOnPit ? "" : "Vách giếng truyền tải xuống dầm móng – kiểm tra ở mục Dầm móng / Móng."));
        // đẩy nổi (thi công)
        r.BuoyG = gPitWalls + gSlab;
        r.BuoyU = Gw * hsub * aOut;
        if (r.BuoyU > 1e-9)
        {
            r.Checks.Add(Dt.Check("Chống đẩy nổi hố pít (thi công) G ≥ 1,1·U", 1.1 * r.BuoyU, r.BuoyG, "kN"));
            r.Calc.Add($"Chống đẩy nổi khi chưa có kết cấu bên trên: U = γw·hsub·A = 10·{Dt.F(hsub, "0.###")}·{Dt.F(aOut, "0.###")} = {Dt.F(r.BuoyU, "0.#")} kN; G = thành + đáy = {Dt.F(r.BuoyG, "0.#")} kN ≥ 1,1·U = {Dt.F(1.1 * r.BuoyU, "0.#")} kN"
                       + (r.BuoyG >= 1.1 * r.BuoyU ? " → đạt." : " → KHÔNG ĐẠT: hạ mực nước ngầm khi thi công, tăng đáy / mở rộng đáy ra ngoài thành (đất đè)."));
        }
        else r.Calc.Add("Mực nước ngầm thấp hơn đáy hố pít – không có đẩy nổi.");

        // ---------------- vách thang máy ----------------
        r.ShaftD = i.ShaftBarD > 0 ? i.ShaftBarD : 10;
        r.ShaftS = i.ShaftBarS > 0 ? i.ShaftBarS : 0.20;
        double mu = 2 * Dt.AreaPerM(r.ShaftD, r.ShaftS) / t;
        r.Mu = mu;
        r.Checks.Add(Dt.Check("Vách thang máy – hàm lượng thép mỗi phương μ ≥ 0,25 %", 0.25, mu * 100, "%"));
        double nWall = 1.1 * Gc * t * r.ShaftHeight;
        double nCap = 0.85 * conc.Rb * t;
        r.Checks.Add(Dt.Check("Vách thang máy – nén chân vách N ≤ 0,85·Rb·t (bản thân)", nWall, nCap, "kN/m"));
        r.Calc.Add("## Vách thang máy");
        r.Calc.Add($"Vách BTCT dày {Dt.Mm(t)} mm, cao {Dt.F(r.ShaftHeight, "0.##")} m: thép 2 lớp mỗi phương {Dt.Spaced(r.ShaftD, r.ShaftS)} → μ = 2·As/t = {Dt.F(mu * 100, "0.###")} % ≥ 0,25 % (TCVN 5574:2018 – vách); "
                   + "thép đứng nối chồng tại mỗi sàn ll ≥ 40d, thép ngang móc 135° bao góc vách, đai C Ø6 a400×400 liên kết 2 lớp. Vách giếng chịu tải ray dẫn hướng – bản mã chôn sẵn theo catalogue.");
        r.Calc.Add($"Lỗ cửa tầng {Dt.Mm(i.DoorW)} × {Dt.Mm(i.DoorH)} mm: mỗi cạnh lỗ 2Ø14 vươn qua mép ≥ 40d, thép xiên góc 2Ø12 dài 1000, dầm lanh tô cửa {Dt.Mm(t)} × {Dt.Mm(i.LintelH)} (2Ø14 + 2Ø14, đai Ø6a150); "
                   + $"bậu cửa (sill) BTCT chôn bản mã ray cửa. Mỗi tầng 1 cửa – {i.Storeys} cửa.");
        double lLint = i.DoorW + 2 * 0.25;
        double qLint = 1.1 * Gc * t * Math.Max(lLint / 3, 0.3) + 1.1 * Gc * t * i.LintelH;
        double mLint = qLint * lLint * lLint / 8;
        double muLint = RcSection.MomentCapacity(t, i.LintelH - s.CoverBeam - 0.013, 2 * Dt.Area(14), 0, s.CoverBeam, conc, main);
        r.Checks.Add(Dt.Check("Lanh tô cửa thang máy – uốn M ≤ Mu (2Ø14)", mLint, muLint, "kN.m"));

        // ---------------- thép ----------------
        Bars(i, s, r, cov);
        RebarNumbering.Renumber(r.Bars);

        // ---------------- khối lượng ----------------
        double vPitWall = perimC * t * H, vSlab = aOut * ts, vShaft = perimC * t * r.ShaftHeight - i.Storeys * i.DoorW * i.DoorH * t;
        r.ConcreteVolume = vPitWall + vSlab + vShaft;
        double perimIn = 2 * (i.ShaftW + i.ShaftD), perimOut = 2 * (r.Lox + r.Loy);
        r.FormworkArea = (perimIn + perimOut) * (H + r.ShaftHeight) + 2 * (r.Lox + r.Loy) * ts - 2 * i.Storeys * i.DoorW * i.DoorH + i.Storeys * (2 * i.DoorH + i.DoorW) * t;
        r.LeanVolume = (r.Lox + 0.2) * (r.Loy + 0.2) * s.LeanThickness;
        r.Extra.Add(("Băng cản nước PVC (waterstop) mạch ngừng đáy – thành hố pít", "m", perimC));
        r.Extra.Add(("Chống thấm mặt ngoài thành + đáy hố pít (màng khò nóng / tinh thể)", "m²", perimOut * (H + ts) + aOut));
        r.Extra.Add(("Chống thấm mặt trong hố pít (vữa xi măng – polymer)", "m²", perimIn * H + i.ShaftW * i.ShaftD));
        r.Extra.Add(("Đào đất hố pít (mở taluy 0,3 m mỗi bên)", "m³", (r.Lox + 0.6) * (r.Loy + 0.6) * (H + ts + s.LeanThickness)));
        r.Extra.Add(("Bản mã chôn sẵn liên kết ray dẫn hướng (theo catalogue)", "bộ", 2 * (i.Storeys + 1)));
        r.Calc.Add($"Bê tông: thành hố {Dt.F(vPitWall, "0.###")} + đáy {Dt.F(vSlab, "0.###")} + vách giếng {Dt.F(vShaft, "0.###")} (đã trừ lỗ cửa) = {Dt.F(r.ConcreteVolume, "0.###")} m³.");
        r.Calc.Add("## Kết luận");
        r.Calc.Add(r.Ok ? "Hố pít, vách thang máy đạt các kiểm tra." : "Có hạng mục KHÔNG ĐẠT – xem thông báo LỖI.");
        foreach (var k in r.Checks.Where(x => !x.Ok))
            if (!r.Messages.Any(x => x.Contains(k.Name))) r.Messages.Add("LỖI " + k.Name + ": " + Dt.F(k.Demand, "0.00") + " > " + Dt.F(k.Capacity, "0.00") + " " + k.Unit);
    }

    /// <summary>Một mặt thép (dải 1 m): As cường độ (≥ Ø10a200), chọn Ø/bước, tăng thép tới khi acrc ≤ giới hạn (WaterTankFormulas.CrackWidth).</summary>
    private static TankFace Face(string name, double h, double cover, double m, double mSer, Material conc, Material steel, double lim, DetailResult r)
    {
        var f = new TankFace { Name = name, H = h, M = m, Mser = mSer };
        double h0 = h - cover - 0.005;
        var fl = RcSection.DesignRect(m, 1.0, h0, cover + 0.006, conc, steel, 0.1);
        if (!fl.Ok) r.Messages.Add("LỖI " + name + ": " + fl.Note);
        f.AsReq = Math.Max(fl.As, Dt.AreaPerM(10, 0.20));
        var cands = new List<(int D, double S, double A)>();
        foreach (int dd in Dias) for (int k = 20; k >= 10; k--) cands.Add((dd, k / 100.0, Dt.AreaPerM(dd, k / 100.0)));
        var pick = cands.OrderBy(x => x.A).FirstOrDefault(x => x.A >= f.AsReq - 1e-12 && WaterTankFormulas.CrackWidth(mSer, 0, h, cover, x.D, x.S, steel.E).Acrc <= lim + 1e-12);
        if (pick.D == 0) pick = cands.OrderByDescending(x => x.A).First();
        f.D = pick.D; f.S = pick.S;
        f.Mu = RcSection.MomentCapacity(1.0, h - cover - f.D / 2000.0, f.AsProv, 0, cover, conc, steel);
        var cw = WaterTankFormulas.CrackWidth(mSer, 0, h, cover, f.D, f.S, steel.E);
        f.Crack = cw.Acrc; f.SigmaS = cw.SigmaS;
        r.Checks.Add(Dt.Check(name + " – uốn M ≤ Mu (" + f.Text + ")", m, f.Mu, "kN.m/m"));
        r.Checks.Add(Dt.Check(name + " – bề rộng vết nứt ≤ " + Dt.F(lim * 1000, "0.0#") + " mm", f.Crack * 1000, lim * 1000, "mm"));
        r.Calc.Add($"{name}: M = {Dt.F(m, "0.##")} kN.m/m → As = {Dt.F(f.AsReq * 1e4, "0.##")} cm²/m, chọn {f.Text} ({Dt.F(f.AsProv * 1e4, "0.##")} cm²/m), Mu = {Dt.F(f.Mu, "0.##")}; acrc = {Dt.F(f.Crack * 1000, "0.###")} mm.");
        return f;
    }

    private static void Bars(ManualLiftPit i, DetailSettings s, LiftPitResult r, double c)
    {
        double t = i.Wall, H = i.PitDepth, ts = i.PitSlab;
        double perimIn = 2 * (i.ShaftW + i.ShaftD), perimOut = 2 * (r.Lox + r.Loy);
        var wi = r.WallIn; var wo = r.WallOut; var wh = r.WallHor;
        double vert = H + ts - c + Dt.R10(Math.Max(40 * wo.D / 1000.0, 0.40));   // thép đứng thành hố, chờ lên vách ≥ 40d
        double foot = Dt.R10(Math.Max(0.20, 15 * wo.D / 1000.0));
        r.Bars.Add(Dt.Hooked("Thành hố pít – thép đứng mặt ngoài", wo.D, (int)Math.Ceiling(perimOut / wo.S), Dt.R10(vert), foot, 0, false, wo.S));
        r.Bars.Add(Dt.Hooked("Thành hố pít – thép đứng mặt trong", wi.D, (int)Math.Ceiling(perimIn / wi.S), Dt.R10(vert), foot, 0, false, wi.S));
        int rows = (int)Math.Floor((H - 0.10) / wh.S + 1e-6) + 1;
        double lap = Dt.R10(Math.Max(40 * wh.D / 1000.0, 0.30));
        r.Bars.Add(Dt.Straight("Thành hố pít – thép ngang mặt ngoài", wh.D, 4 * rows, Dt.R10(Math.Max(r.Lox, r.Loy) - 2 * c), false, wh.S));
        r.Bars.Add(Dt.Straight("Thành hố pít – thép ngang mặt trong", wh.D, 4 * rows, Dt.R10(Math.Max(i.ShaftW, i.ShaftD)), false, wh.S));
        r.Bars.Add(Dt.Hooked("Thành hố pít – thép góc chữ L (2 mặt)", wh.D, 8 * rows, lap, lap, 0, false, wh.S));
        var st = r.SlabTop; var sb = r.SlabBot;
        double leg = Dt.R10(Math.Max(ts - 2 * c, 0.10));
        double bx = r.Lox - 2 * c, by = r.Loy - 2 * c;
        r.Bars.Add(Dt.Hooked("Đáy hố pít – lớp dưới phương X", sb.D, Dt.CountAcross(r.Loy, c, sb.S), Dt.R10(bx), leg, leg, false, sb.S));
        r.Bars.Add(Dt.Hooked("Đáy hố pít – lớp dưới phương Y", sb.D, Dt.CountAcross(r.Lox, c, sb.S), Dt.R10(by), leg, leg, false, sb.S));
        r.Bars.Add(Dt.Hooked("Đáy hố pít – lớp trên phương X", st.D, Dt.CountAcross(r.Loy, c, st.S), Dt.R10(bx), leg, leg, true, st.S));
        r.Bars.Add(Dt.Hooked("Đáy hố pít – lớp trên phương Y", st.D, Dt.CountAcross(r.Lox, c, st.S), Dt.R10(by), leg, leg, true, st.S));
        r.Bars.Add(Dt.Hooked("Đáy hố pít – thép kê (chân chó)", 12, Math.Max(4, (int)Math.Ceiling(r.Lox * r.Loy)), Dt.R10(ts - 2 * c), 0.20, 0.20));
        // vách giếng
        double cs = Math.Max(s.CoverColumn, 0.02);
        double lapV = Dt.R10(Math.Max(40 * r.ShaftD / 1000.0, 0.40));
        int nV = (int)Math.Ceiling(perimIn / r.ShaftS) + (int)Math.Ceiling(perimOut / r.ShaftS);
        double lenV = Dt.R10(i.StoreyHeight + lapV);
        r.Bars.Add(Dt.Straight("Vách thang máy – thép đứng 2 lớp (mỗi tầng, nối chồng 40d)", r.ShaftD, nV * i.Storeys, lenV, false, r.ShaftS));
        r.Bars.Add(Dt.Straight("Vách thang máy – thép đứng tầng OH", r.ShaftD, nV, Dt.R10(i.Overhead - cs), false, r.ShaftS));
        int nH = (int)Math.Ceiling(r.ShaftHeight / r.ShaftS);
        double lenH1 = Dt.R10(Math.Max(r.Lox, r.Loy) - 2 * cs);
        r.Bars.Add(Dt.Straight("Vách thang máy – thép ngang mặt ngoài", r.ShaftD, 4 * nH, lenH1, false, r.ShaftS));
        r.Bars.Add(Dt.Straight("Vách thang máy – thép ngang mặt trong", r.ShaftD, 4 * nH, Dt.R10(Math.Max(i.ShaftW, i.ShaftD)), false, r.ShaftS));
        r.Bars.Add(Dt.Hooked("Vách thang máy – thép góc chữ L", r.ShaftD, 8 * nH, lapV, lapV, 0, false, r.ShaftS));
        int nC = (int)Math.Ceiling(perimIn * r.ShaftHeight / (0.4 * 0.4));
        r.Bars.Add(Dt.Hooked("Vách thang máy – đai C liên kết 2 lớp Ø6", 6, nC, Dt.R10(t - 2 * cs + 0.02), 0.06, 0.06));
        // lỗ cửa
        int n = i.Storeys;
        double la = Dt.R10(40 * 14 / 1000.0);
        r.Bars.Add(Dt.Straight("Lỗ cửa tầng – thép gia cường cạnh đứng 2Ø14", 14, 2 * 2 * 2 * n, Dt.R10(i.DoorH + 2 * la)));
        r.Bars.Add(Dt.Straight("Lỗ cửa tầng – thép gia cường cạnh ngang dưới (bậu) 2Ø14", 14, 2 * 2 * n, Dt.R10(i.DoorW + 2 * la)));
        r.Bars.Add(Dt.Straight("Lỗ cửa tầng – thép xiên góc 2Ø12", 12, 2 * 2 * 2 * n, 1.0));
        r.Bars.Add(Dt.Straight("Lanh tô cửa thang máy – thép dưới 2Ø14", 14, 2 * n, Dt.R10(i.DoorW + 2 * la)));
        r.Bars.Add(Dt.Straight("Lanh tô cửa thang máy – thép trên 2Ø14", 14, 2 * n, Dt.R10(i.DoorW + 2 * la), true));
        r.Bars.Add(Dt.Stirrup("Lanh tô cửa thang máy – đai Ø6a150", 6, n * ((int)Math.Ceiling((i.DoorW + 0.2) / 0.15) + 1), t - 2 * s.CoverBeam + 0.012, i.LintelH - 2 * s.CoverBeam + 0.012, 0.15));
    }
}

/// <summary>Bản vẽ hố pít – vách thang máy: mặt bằng giếng, mặt cắt 1-1 qua hố pít và tầng 1, chi tiết thép lỗ cửa tầng.</summary>
public static class LiftPitDrawing
{
    public const double PlanScale = 25, SectionScale = 25, DoorScale = 25;

    private static int Mk(DetailResult r, string prefix) => r.Bars.FirstOrDefault(b => b.Role.StartsWith(prefix, StringComparison.Ordinal))?.Mark ?? 0;

    public static List<Figure> Build(ManualLiftPit i, LiftPitResult r, DetailSettings s)
    {
        var list = new List<Figure>();
        if (r == null || r.WallIn == null || r.SlabTop == null) return list;
        if (r.Bars.Any(b => b.Mark <= 0)) RebarNumbering.Renumber(r.Bars);
        list.Add(new Figure(Plan(i, r, s), "MẶT BẰNG HỐ PÍT – VÁCH THANG MÁY " + r.Name));
        list.Add(new Figure(Section(i, r, s), "MẶT CẮT 1-1 " + r.Name));
        list.Add(new Figure(Door(i, r, s), "CHI TIẾT THÉP LỖ CỬA TẦNG " + r.Name));
        return list;
    }

    public static DxfWriter Plan(ManualLiftPit i, LiftPitResult r, DetailSettings s)
    {
        var w = new DxfWriter(PlanScale);
        double th = w.Th;
        double t = i.Wall * 1000, lx = r.Lox * 1000, ly = r.Loy * 1000, c = Math.Max(s.CoverTank, 0.02) * 1000;
        double dw = i.DoorW * 1000, x0d = (lx - dw) / 2;
        // vách (2 đường) + lỗ cửa ở vách trước (y = 0)
        var outer = new List<(double X, double Y)> { (x0d, 0), (0, 0), (0, ly), (lx, ly), (lx, 0), (x0d + dw, 0) };
        var inner = new List<(double X, double Y)> { (x0d, t), (t, t), (t, ly - t), (lx - t, ly - t), (lx - t, t), (x0d + dw, t) };
        if (w.HatchOn)
        {
            w.HatchMatRect(HatchMaterial.Concrete, 0, t, t, ly);
            w.HatchMatRect(HatchMaterial.Concrete, lx - t, t, lx, ly);
            w.HatchMatRect(HatchMaterial.Concrete, t, ly - t, lx - t, ly);
            w.HatchMatRect(HatchMaterial.Concrete, 0, 0, x0d, t);
            w.HatchMatRect(HatchMaterial.Concrete, x0d + dw, 0, lx, t);
        }
        w.Polyline(Lay.BeTong, outer, false);
        w.Polyline(Lay.BeTong, inner, false);
        w.Line(Lay.BeTong, x0d, 0, x0d, t); w.Line(Lay.BeTong, x0d + dw, 0, x0d + dw, t);
        // thép 2 lớp (nét mảnh trong vách)
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (x0d - 40, c), (c, c), (c, ly - c), (lx - c, ly - c), (lx - c, c), (x0d + dw + 40, c) }, false, 0.25 * w.Scale);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (x0d - 40, t - c), (t - c, t - c), (t - c, ly - t + c), (lx - t + c, ly - t + c), (lx - t + c, t - c), (x0d + dw + 40, t - c) }, false, 0.25 * w.Scale);
        // cabin, đối trọng (nét đứt)
        double cw = Math.Max(i.ShaftW * 1000 - 400, 600), cd = Math.Max(i.ShaftD * 1000 - 450, 600);
        w.Rect(Lay.KyHieu, t + 100, t + 150, t + 100 + cw, t + 150 + cd, "DASHED");
        w.Text(Lay.Chu, t + 100 + cw / 2, t + 150 + cd / 2, th, "CABIN", 0, 1, 2);
        w.Rect(Lay.KyHieu, t + 100, ly - t - 180, t + 100 + Math.Min(cw, 800), ly - t - 50, "DASHED");
        w.Text(Lay.Chu, t + 100 + Math.Min(cw, 800) / 2, ly - t - 115, 0.8 * th, "ĐỐI TRỌNG", 0, 1, 2);
        // giảm chấn
        double bb = i.BufferBase * 1000;
        w.Rect(Lay.KyHieu, t + 100 + cw / 2 - bb / 2, t + 150 + cd / 2 - bb / 2 - 2 * th, t + 100 + cw / 2 + bb / 2, t + 150 + cd / 2 + bb / 2 - 2 * th);
        // vạch cắt 1-1 qua giữa giếng theo phương Y
        double xc = lx / 2 + 120;
        w.CutMark(xc, -2.5 * th, xc, -1.0 * th, "1", xc + 0.9 * th, -3.2 * th);
        w.CutMark(xc, ly + 1.0 * th, xc, ly + 2.5 * th, "1", xc + 0.9 * th, ly + 3.2 * th);
        // nhãn thép
        w.LeaderTag(c, ly * 0.55, -3 * th, ly * 0.55 + 2 * th, Dt.Spaced(r.ShaftD, r.ShaftS) + " (2 LỚP)", Mk(r, "Vách thang máy – thép ngang mặt ngoài"), -1);
        w.LeaderTag(lx - t + c, ly * 0.35, lx + 3 * th, ly * 0.30, Dt.Spaced(r.ShaftD, r.ShaftS), Mk(r, "Vách thang máy – thép ngang mặt trong"), 1);
        w.LeaderTag(x0d, t / 2, x0d - 2 * th, -4.5 * th, "2Ø14 MÉP LỖ", Mk(r, "Lỗ cửa tầng – thép gia cường cạnh đứng"), -1);
        // kích thước
        w.DimChainH(new List<double> { 0, x0d, x0d + dw, lx }, 0, -6 * th);
        w.DimChainH(new List<double> { 0, t, lx - t, lx }, ly, ly + 5 * th);
        w.DimChainV(new List<double> { 0, t, ly - t, ly }, 0, -5 * th);
        w.Text(Lay.Chu, lx / 2, -8.5 * th, 0.9 * th, $"GIẾNG THANG {Dt.Mm(i.ShaftW)}×{Dt.Mm(i.ShaftD)} – VÁCH BTCT {Dt.Mm(i.Wall)} – CỬA TẦNG {Dt.Mm(i.DoorW)}×{Dt.Mm(i.DoorH)}", 0, 1, 0);
        w.Title(lx / 2, w.Extents.Y2 + 3 * th, "MẶT BẰNG HỐ PÍT – VÁCH THANG MÁY " + r.Name, DrawUtil.ScaleText(PlanScale));
        return w;
    }

    public static DxfWriter Section(ManualLiftPit i, LiftPitResult r, DetailSettings s)
    {
        var w = new DxfWriter(SectionScale);
        double th = w.Th;
        double t = i.Wall * 1000, ly = r.Loy * 1000, H = i.PitDepth * 1000, ts = i.PitSlab * 1000, c = Math.Max(s.CoverTank, 0.02) * 1000;
        double hUp = Math.Min(i.StoreyHeight * 1000, 3300) + 300;   // vẽ tới hết tầng 1
        double y0 = -(H + ts), yTopPit = -H;
        double lean = s.LeanThickness * 1000;
        // bê tông lót, đáy, thành
        w.Rect(Lay.BeTong, -100, y0 - lean, ly + 100, y0);
        if (w.HatchOn)
        {
            w.HatchMatRect(HatchMaterial.LeanConcrete, -100, y0 - lean, ly + 100, y0);
            w.HatchMatRect(HatchMaterial.Concrete, 0, y0, ly, yTopPit);
            w.HatchMatRect(HatchMaterial.Concrete, 0, yTopPit, t, hUp);
            w.HatchMatRect(HatchMaterial.Concrete, ly - t, yTopPit, ly, hUp);
        }
        w.Polyline(Lay.BeTong, new List<(double X, double Y)> { (0, hUp), (0, y0), (ly, y0), (ly, hUp) }, false);
        w.Polyline(Lay.BeTong, new List<(double X, double Y)> { (t, hUp), (t, yTopPit), (ly - t, yTopPit), (ly - t, hUp) }, false);
        w.BreakH(Lay.BeTong, -100, t + 100, hUp);
        w.BreakH(Lay.BeTong, ly - t - 100, ly + 100, hUp);
        // cửa tầng 1 ở vách trái (vách trước – y = 0)
        double dh = i.DoorH * 1000;
        w.Line(Lay.KyHieu, 0, 0, t, 0); w.Line(Lay.KyHieu, 0, dh, t, dh);
        w.Text(Lay.Chu, t + 0.5 * th, dh / 2, 0.85 * th, "LỖ CỬA TẦNG " + Dt.Mm(i.DoorW) + "×" + Dt.Mm(i.DoorH), 90, 1, 0);
        w.Rect(Lay.BeTong, 0, dh, t, dh + i.LintelH * 1000, "DASHED");
        // mặt đất / sàn tầng 1
        w.Line(Lay.KyHieu, -4 * th, 0, -0.5 * th, 0);
        w.Line(Lay.KyHieu, ly + 0.5 * th, 0, ly + 4 * th, 0);
        w.ElevMark(-2 * th, 0, "±0.000", -1);
        w.ElevMark(-2 * th, yTopPit, (-(i.PitDepth)).ToString("0.000", Dt.Inv), -1);
        if (i.Hgw < i.PitDepth + i.PitSlab)
        {
            double yg = -i.Hgw * 1000;
            w.Line(Lay.KyHieu, ly + 0.5 * th, yg, ly + 5 * th, yg, "DASHED");
            w.Text(Lay.Chu, ly + 0.6 * th, yg + 0.3 * th, 0.8 * th, "MNN", 0, 0, 0);
        }
        // thép thành (2 mặt) – chân bẻ vào đáy, chờ lên vách
        double yb0 = y0 + Math.Max(c, 35), yb1 = yTopPit - c;
        double foot = Math.Max(200, 15 * r.WallOut.D);
        double lapUp = Math.Max(400, 40 * r.WallOut.D);
        foreach (int side in new[] { 0, 1 })
        {
            double X(double x) => side == 0 ? x : ly - x;
            int dir = side == 0 ? 1 : -1;
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (X(c + dir * 0 + foot * 1), yb0 + 10), (X(c), yb0 + 10), (X(c), lapUp) }, false, 0.35 * w.Scale);
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (X(t - c + foot), yb1 - 10), (X(t - c), yb1 - 10), (X(t - c), lapUp) }, false, 0.35 * w.Scale);
            double dh2 = Math.Max(r.WallHor.D / 2.0, 0.12 * th);
            for (double y = yTopPit + 50; y < -50; y += r.WallHor.S * 1000)
            {
                w.Dot(Lay.Thep, X(c + 2 * dh2), y, dh2);
                w.Dot(Lay.Thep, X(t - c - 2 * dh2), y, dh2);
            }
            // vách phía trên: thép 2 lớp
            w.Line(Lay.Thep, X(c + 15), 0, X(c + 15), hUp - 50);
            w.Line(Lay.Thep, X(t - c - 15), 0, X(t - c - 15), hUp - 50);
        }
        // đáy: 2 lớp
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (c, yb1), (c, yb0), (ly - c, yb0), (ly - c, yb1) }, false, 0.35 * w.Scale);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (c, yb0), (c, yb1), (ly - c, yb1), (ly - c, yb0) }, false, 0.35 * w.Scale);
        double sB = Math.Max(r.SlabBot.S * 1000, 50);
        for (double x = c + sB; x < ly - c - 1e-6; x += sB)
        {
            w.Dot(Lay.Thep, x, yb0 + 1.5 * r.SlabBot.D, Math.Max(r.SlabBot.D / 2.0, 0.12 * th));
            w.Dot(Lay.Thep, x, yb1 - 1.5 * r.SlabTop.D, Math.Max(r.SlabTop.D / 2.0, 0.12 * th));
        }
        // giảm chấn
        double bb = i.BufferBase * 1000, xb = ly / 2;
        w.Rect(Lay.KyHieu, xb - bb / 2, yTopPit, xb + bb / 2, yTopPit + 400);
        w.Text(Lay.Chu, xb, yTopPit + 400 + 0.4 * th, 0.8 * th, "GIẢM CHẤN", 0, 1, 0);
        // waterstop
        foreach (double xw in new[] { t / 2, ly - t / 2 }) w.Rect(Lay.KyHieu, xw - 5, yTopPit - 60, xw + 5, yTopPit + 60);
        // nhãn
        w.LeaderTag(c, yTopPit / 2, -4 * th, yTopPit / 2 - 1.5 * th, r.WallOut.Text, Mk(r, "Thành hố pít – thép đứng mặt ngoài"), -1);
        w.LeaderTag(ly - t + c, yTopPit * 0.4, ly - t - 3 * th, yTopPit * 0.4 + 3 * th, r.WallIn.Text, Mk(r, "Thành hố pít – thép đứng mặt trong"), -1);
        w.LeaderTag(ly * 0.3, yb0, ly * 0.3 + 2 * th, y0 - lean - 3.5 * th, r.SlabBot.Text, Mk(r, "Đáy hố pít – lớp dưới"), 1);
        w.LeaderTag(ly * 0.68, yb1, ly * 0.68 + 2 * th, yTopPit + 6 * th, r.SlabTop.Text, Mk(r, "Đáy hố pít – lớp trên"), 1);
        w.LeaderTag(ly - c - 15, hUp * 0.55, ly + 3 * th, hUp * 0.55 + 2 * th, Dt.Spaced(r.ShaftD, r.ShaftS) + " (2 LỚP)", Mk(r, "Vách thang máy – thép đứng 2 lớp"), 1);
        w.LeaderTag(t / 2, yTopPit, -4 * th, yTopPit - 4 * th, "WATERSTOP PVC", 0, -1);
        // kích thước
        w.DimChainV(new List<double> { y0 - lean, y0, yTopPit, 0 }, ly, ly + 7 * th);
        w.DimChainH(new List<double> { 0, t, ly - t, ly }, y0 - lean, y0 - lean - 7 * th);
        w.Title(ly / 2, w.Extents.Y1 - 1.2 * th, "MẶT CẮT 1-1 " + r.Name, DrawUtil.ScaleText(SectionScale), true);
        return w;
    }

    public static DxfWriter Door(ManualLiftPit i, LiftPitResult r, DetailSettings s)
    {
        var w = new DxfWriter(DoorScale);
        double th = w.Th;
        double dw = i.DoorW * 1000, dh = i.DoorH * 1000, lh = i.LintelH * 1000;
        double la = 40 * 14;
        double ex = 700, x0 = -ex, x1 = dw + ex, yT = dh + lh + 400;
        // mặt đứng vách quanh lỗ cửa
        w.Rect(Lay.BeTong, x0, -300, x1, yT);
        w.Rect(Lay.BeTong, 0, 0, dw, dh);
        w.Rect(Lay.BeTong, -50, dh, dw + 50, dh + lh, "DASHED");
        w.Line(Lay.BeTong, 0, 0, dw, dh); w.Line(Lay.BeTong, 0, dh, dw, 0);
        // thép gia cường cạnh đứng
        foreach (double x in new[] { -60.0, dw + 60 }) w.Line(Lay.Thep, x, -la, x, dh + la);
        // bậu (dưới) + lanh tô (trên)
        w.Line(Lay.Thep, -la, -60, dw + la, -60);
        w.Line(Lay.Thep, -la, dh + 40, dw + la, dh + 40);
        w.Line(Lay.Thep, -la, dh + lh - 40, dw + la, dh + lh - 40);
        // thép xiên góc
        double dd = 500 / Math.Sqrt(2);
        foreach (var (cx, cy, sx, sy) in new[] { (0.0, 0.0, -1, -1), (dw, 0.0, 1, -1), (0.0, dh + lh, -1, 1), (dw, dh + lh, 1, 1) })
            w.Line(Lay.Thep, cx + sx * 80, cy + sy * 80, cx + sx * (80 + dd), cy + sy * (80 + dd));
        // đai lanh tô
        for (double x = 0; x <= dw + 1e-6; x += 150) w.Line(Lay.Dai, x, dh + 30, x, dh + lh - 30);
        w.LeaderTag(-60, dh * 0.5, x0 - 2 * th, dh * 0.6, "2Ø14", Mk(r, "Lỗ cửa tầng – thép gia cường cạnh đứng"), -1);
        w.LeaderTag(dw * 0.3, -60, dw * 0.3 + 2 * th, -300 - 3 * th, "2Ø14", Mk(r, "Lỗ cửa tầng – thép gia cường cạnh ngang"), 1);
        w.LeaderTag(dw * 0.25, dh + 40, x0 - 2 * th, yT + 2 * th, "2Ø14", Mk(r, "Lanh tô cửa thang máy – thép dưới"), -1);
        w.LeaderTag(dw * 0.6, dh + lh - 40, dw * 0.6 + 2 * th, yT + 3.5 * th, "2Ø14", Mk(r, "Lanh tô cửa thang máy – thép trên"), 1);
        w.LeaderTag(dw + 80 + dd * 0.5, dh + lh + 80 + dd * 0.5, x1 + 2 * th, yT + 1 * th, "2Ø12 L=1000", Mk(r, "Lỗ cửa tầng – thép xiên góc"), 1);
        w.LeaderTag(dw * 0.8, dh + lh / 2, x1 + 2 * th, dh + lh / 2 - 2 * th, "Ø6a150", Mk(r, "Lanh tô cửa thang máy – đai"), 1);
        w.DimChainH(new List<double> { x0, 0, dw, x1 }, -300, -300 - 6 * th);
        w.DimChainV(new List<double> { 0, dh, dh + lh }, x1, x1 + 4 * th);
        w.Text(Lay.Chu, dw / 2, dh / 2, th, "LỖ CỬA", 0, 1, 2);
        w.Title(dw / 2, w.Extents.Y1 - 1.2 * th, "CHI TIẾT THÉP LỖ CỬA TẦNG " + r.Name, DrawUtil.ScaleText(DoorScale), true);
        return w;
    }
}
