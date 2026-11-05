using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// MÓNG BÈ (bản phẳng) – PHƯƠNG PHÁP MÓNG CỨNG (ACI 336.2R, thực hành thiết kế Việt Nam)
//  • Áp lực đáy móng (tiêu chuẩn): p = ΣNtc/A + γtb·Df ± My·(x − xc)/Iy ± Mx·(y − yc)/Ix
//    kiểm tra ptb ≤ R, pmax ≤ 1,2R, pmin ≥ 0 (TCVN 9362:2012).
//  • Dải tính toán: mỗi hàng cột theo X (và theo Y) là một dải, bề rộng tới giữa hai hàng cột / mép bè.
//    Phản lực dải hiệu chỉnh (ACI 336.2R "modified"): tải trung bình = (ΣN dải + q dải·B·L)/2;
//    cột nhân hệ số F = tải TB/ΣN, áp lực q' = tải TB/(B·L). Mô men dải tĩnh định (dầm tự do hai đầu):
//    M(x) = w·x²/2 − ΣF·Nj·(x − xj), w = q'·B; M &gt; 0 căng thớ dưới (dưới chân cột), M &lt; 0 căng thớ trên (giữa nhịp).
//  • Thép: TCVN 5574:2018 mục 8.1.2.2 (tiết diện chữ nhật b = 1 m), μmin = 0,1 %.
//  • Chọc thủng: TCVN 5574:2018 mục 8.1.6.3.1: F ≤ Fb,ult = Rbt·u·h0; u – chu vi tháp chọc thủng tại h0/2,
//    F = N − p·Ab (Ab đáy tháp tại h0); cột biên / góc: chu vi cắt cụt tại mép bè.
// =====================================================================

/// <summary>Một cột đứng trên móng bè.</summary>
public class RaftColumn
{
    [Field("X tim cột", Unit = "m", Hint = "Từ góc trái dưới của bè")] public double X { get; set; }
    [Field("Y tim cột", Unit = "m")] public double Y { get; set; }
    [Field("Cạnh cột theo X (b)", Unit = "mm", Scale = 1000)] public double B { get; set; } = 0.40;
    [Field("Cạnh cột theo Y (h)", Unit = "mm", Scale = 1000)] public double H { get; set; } = 0.40;
    [Field("N tính toán", Unit = "kN", Hint = "Nén dương")] public double N { get; set; }
    [Field("N tiêu chuẩn", Unit = "kN", Hint = "0 = N/1,15")] public double Ntc { get; set; }
    [Field("Mx", Unit = "kN.m", Hint = "Làm tăng áp lực về phía +Y")] public double Mx { get; set; }
    [Field("My", Unit = "kN.m", Hint = "Làm tăng áp lực về phía +X")] public double My { get; set; }

    public double NtcEff => Ntc > 1e-9 ? Ntc : N / 1.15;
    /// <summary>Tỷ số tải tiêu chuẩn / tính toán (dùng quy đổi mô men).</summary>
    public double ServiceRatio => N > 1e-9 ? NtcEff / N : 1 / 1.15;
}

/// <summary>Kết quả một dải tính toán.</summary>
public class RaftStrip
{
    /// <summary>true: dải chạy theo X (thép phương X)</summary>
    public bool AlongX { get; set; }
    /// <summary>Toạ độ hàng cột (Y với dải X, X với dải Y), m</summary>
    public double Line { get; set; }
    public double From { get; set; }
    public double To { get; set; }
    public double Width => To - From;
    public double Length { get; set; }
    public double SumN { get; set; }
    /// <summary>Áp lực tính toán trung bình của dải theo phân bố cứng (kN/m²)</summary>
    public double Q { get; set; }
    /// <summary>Hệ số hiệu chỉnh tải cột F và áp lực hiệu chỉnh q' (kN/m²)</summary>
    public double F { get; set; }
    public double QAdj { get; set; }
    /// <summary>Mô men lớn nhất căng thớ dưới / trên (kN.m, toàn dải) và vị trí</summary>
    public double MBot { get; set; }
    public double XBot { get; set; }
    public double MTop { get; set; }
    public double XTop { get; set; }
    public double MBotPerM => Width > 1e-9 ? MBot / Width : 0;
    public double MTopPerM => Width > 1e-9 ? MTop / Width : 0;
    /// <summary>Toạ độ (dọc dải) và tải cột (kN, chưa hiệu chỉnh)</summary>
    public List<(double Pos, double N)> Loads { get; set; } = new();

    /// <summary>Mô men tại vị trí x (kN.m, dương căng dưới).</summary>
    public double MomentAt(double x)
    {
        double w = QAdj * Width;
        double m = w * x * x / 2;
        foreach (var (p, n) in Loads) if (p < x) m -= F * n * (x - p);
        return m;
    }
}

/// <summary>Kiểm tra chọc thủng tại một cột.</summary>
public class RaftPunch
{
    public int Column { get; set; }
    public string Position { get; set; } = "giữa";
    public double H0 { get; set; }
    public double U { get; set; }
    public double Ab { get; set; }
    public double P { get; set; }
    public double F { get; set; }
    public double Fu { get; set; }
}

public class RaftResult : DetailResult
{
    public double A { get; set; }
    public double Ix { get; set; }
    public double Iy { get; set; }
    public double SumNtc { get; set; }
    public double SumN { get; set; }
    /// <summary>Mô men tổng quanh trọng tâm bè (tiêu chuẩn), kN.m</summary>
    public double MxTot { get; set; }
    public double MyTot { get; set; }
    public double Ex { get; set; }
    public double Ey { get; set; }
    public double Ptb { get; set; }
    public double Pmax { get; set; }
    public double Pmin { get; set; }
    public double R { get; set; }
    public List<RaftStrip> Strips { get; set; } = new();
    public List<RaftPunch> Punch { get; set; } = new();
    /// <summary>Mô men thiết kế trên 1 m (kN.m/m) – lớn nhất các dải</summary>
    public double MBotX { get; set; }
    public double MTopX { get; set; }
    public double MBotY { get; set; }
    public double MTopY { get; set; }
    public double AsBotXReq { get; set; }
    public double AsTopXReq { get; set; }
    public double AsBotYReq { get; set; }
    public double AsTopYReq { get; set; }
    public (int D, double S) BotX { get; set; }
    public (int D, double S) BotY { get; set; }
    public (int D, double S) TopX { get; set; }
    public (int D, double S) TopY { get; set; }
}

public class RaftInput : IDetailMember
{
    [Field("Tên móng", Group = "Chung")] public string Name { get; set; } = "MB1";
    [Field("Số lượng", Group = "Chung")] public int Quantity { get; set; } = 1;
    [Field("Chiều dài bè Lx", Unit = "m", Group = "Kích thước")] public double Lx { get; set; } = 9.0;
    [Field("Chiều rộng bè Ly", Unit = "m", Group = "Kích thước")] public double Ly { get; set; } = 9.0;
    [Field("Chiều dày bè", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double T { get; set; } = 0.50;
    [Field("Độ sâu chôn móng Df", Unit = "m", Group = "Nền")] public double Df { get; set; } = 1.5;
    [Field("Bê tông lót – cạnh giáp nhà hàng xóm (hết đất)", Group = "Nền", Choices = "Không|Giáp nhà bên trái|Giáp nhà bên phải|Giáp nhà 2 bên trái + phải (nhà phố)|Giáp nhà trái + phải + phía sau",
        Hint = "cạnh giáp nhà hàng xóm: bê tông lót và bản móng không vươn sang đất nhà bên")] public string Boundary { get; set; } = "Không";
    [Field("Sức chịu tải tiêu chuẩn R", Unit = "kN/m²", Group = "Nền")] public double R { get; set; } = 150;
    [Field("γtb móng + đất", Unit = "kN/m³", Group = "Nền")] public double GammaTb { get; set; } = 20;

    [Field("Thép dưới X – Ø", Unit = "mm", Group = "Thép (0 = tự chọn)")] public int BotDiaX { get; set; }
    [Field("Thép dưới X – bước", Unit = "mm", Scale = 1000, Group = "Thép (0 = tự chọn)")] public double BotSX { get; set; }
    [Field("Thép dưới Y – Ø", Unit = "mm", Group = "Thép (0 = tự chọn)")] public int BotDiaY { get; set; }
    [Field("Thép dưới Y – bước", Unit = "mm", Scale = 1000, Group = "Thép (0 = tự chọn)")] public double BotSY { get; set; }
    [Field("Thép trên X – Ø", Unit = "mm", Group = "Thép (0 = tự chọn)")] public int TopDiaX { get; set; }
    [Field("Thép trên X – bước", Unit = "mm", Scale = 1000, Group = "Thép (0 = tự chọn)")] public double TopSX { get; set; }
    [Field("Thép trên Y – Ø", Unit = "mm", Group = "Thép (0 = tự chọn)")] public int TopDiaY { get; set; }
    [Field("Thép trên Y – bước", Unit = "mm", Scale = 1000, Group = "Thép (0 = tự chọn)")] public double TopSY { get; set; }
    [Field("Có con kê thép trên", Group = "Thép (0 = tự chọn)")] public bool Chairs { get; set; } = true;

    public List<RaftColumn> Columns { get; set; } = new();

    public static readonly int[] Dias = { 12, 14, 16, 18, 20, 22, 25 };

    /// <summary>Mẫu: bè 9 × 9 × 0,5 m, lưới cột 3 × 3 bước 4 m, vươn 0,5 m.</summary>
    public static RaftInput Sample()
    {
        var r = new RaftInput();
        double[] n = { 800, 1100, 800, 1100, 1600, 1100, 800, 1100, 800 };
        int k = 0;
        for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
                r.Columns.Add(new RaftColumn { X = 0.5 + 4 * i, Y = 0.5 + 4 * j, B = 0.40, H = 0.40, N = n[k++] });
        return r;
    }

    // ------------------------------------------------------------------
    // Tính toán
    // ------------------------------------------------------------------
    public DetailResult Run(DetailSettings s) => Design(s ?? new DetailSettings());

    public RaftResult Design(DetailSettings s)
    {
        s ??= new DetailSettings();
        var r = new RaftResult { Name = Name, Quantity = Math.Max(1, Quantity), Kind = "Móng bè", ConcreteClass = s.Concrete, R = R };
        double c = s.CoverFooting;
        r.ConcreteVolume = Math.Max(0, Lx * Ly * T);
        r.FormworkArea = Math.Max(0, 2 * (Lx + Ly) * T);
        string bd = Boundary ?? "";
        string bdl = bd.ToLowerInvariant();
        int nxB = (bdl.Contains("trái") ? 1 : 0) + (bdl.Contains("phải") ? 1 : 0), nyB = bdl.Contains("sau") ? 1 : 0;
        r.LeanVolume = Math.Max(0, (Lx + (2 - nxB) * 0.1) * (Ly + (2 - nyB) * 0.1) * s.LeanThickness);
        if (nxB + nyB > 0)
        {
            r.Calc.Add("Móng bè giáp nhà hàng xóm – hết đất (" + bd + "): bản móng và bê tông lót không vươn sang đất bên cạnh; thành móng phía ranh dùng gạch xây / tấm khuôn để lại; cột biên sát ranh kiểm tra chọc thủng theo chu vi cắt (cột biên / góc).");
            r.Extra.Add(("Xây gạch / tấm khuôn để lại cho thành móng bè giáp nhà hàng xóm", "m²", (nxB * Ly + nyB * Lx) * T));
        }
        if (Lx <= 0 || Ly <= 0 || T <= 2 * c + 0.05)
        {
            r.Messages.Add("LỖI kích thước bè không hợp lệ (Lx, Ly > 0; chiều dày > 2 lớp bảo vệ + 50 mm)");
            return r;
        }
        var cols = (Columns ?? new List<RaftColumn>()).Where(x => x != null).ToList();
        if (cols.Count == 0)
        {
            r.Messages.Add("LỖI chưa khai báo cột trên móng bè – nhập bảng cột (X, Y, b, h, N)");
            return r;
        }
        Material conc, main;
        try { conc = s.Conc(); main = s.Main(); }
        catch (Exception ex) { r.Messages.Add("LỖI vật liệu: " + ex.Message); return r; }
        foreach (var col in cols.Where(x => x.X < -1e-6 || x.X > Lx + 1e-6 || x.Y < -1e-6 || x.Y > Ly + 1e-6))
            r.Messages.Add($"LỖI cột tại ({Dt.F(col.X)}; {Dt.F(col.Y)}) nằm ngoài bè {Dt.F(Lx)} × {Dt.F(Ly)} m");

        double xc = Lx / 2, yc = Ly / 2;
        r.A = Lx * Ly; r.Iy = Ly * Lx * Lx * Lx / 12; r.Ix = Lx * Ly * Ly * Ly / 12;
        r.SumNtc = cols.Sum(x => x.NtcEff);
        r.SumN = cols.Sum(x => x.N);
        r.MyTot = cols.Sum(x => x.NtcEff * (x.X - xc) + x.My * x.ServiceRatio);
        r.MxTot = cols.Sum(x => x.NtcEff * (x.Y - yc) + x.Mx * x.ServiceRatio);
        r.Ex = r.SumNtc > 1e-9 ? r.MyTot / r.SumNtc : 0;
        r.Ey = r.SumNtc > 1e-9 ? r.MxTot / r.SumNtc : 0;
        double g = GammaTb * Df;
        r.Ptb = r.SumNtc / r.A + g;
        double dp = Math.Abs(r.MyTot) * (Lx / 2) / r.Iy + Math.Abs(r.MxTot) * (Ly / 2) / r.Ix;
        r.Pmax = r.Ptb + dp; r.Pmin = r.Ptb - dp;

        r.Calc.Add("## 1. Áp lực đáy móng (tải tiêu chuẩn, phương pháp móng cứng)");
        r.Calc.Add($"Diện tích đáy A = Lx·Ly = {Dt.F(Lx)} × {Dt.F(Ly)} = {Dt.F(r.A, "0.00")} m²; Iy = Ly·Lx³/12 = {Dt.F(r.Iy, "0.00")} m⁴; Ix = Lx·Ly³/12 = {Dt.F(r.Ix, "0.00")} m⁴");
        r.Calc.Add($"ΣNtc = {Dt.F(r.SumNtc, "0.0")} kN; My = Σ[Ntc·(x − {Dt.F(xc)}) + My,tc] = {Dt.F(r.MyTot, "0.0")} kN.m; Mx = Σ[Ntc·(y − {Dt.F(yc)}) + Mx,tc] = {Dt.F(r.MxTot, "0.0")} kN.m");
        r.Calc.Add($"Độ lệch tâm ex = {Dt.F(r.Ex, "0.000")} m (Lx/6 = {Dt.F(Lx / 6, "0.000")}), ey = {Dt.F(r.Ey, "0.000")} m (Ly/6 = {Dt.F(Ly / 6, "0.000")})");
        r.Calc.Add($"ptb = ΣNtc/A + γtb·Df = {Dt.F(r.SumNtc, "0.0")}/{Dt.F(r.A, "0.00")} + {Dt.F(GammaTb)}×{Dt.F(Df)} = {Dt.F(r.Ptb, "0.00")} kN/m²");
        r.Calc.Add($"pmax,min = ptb ± |My|·Lx/(2Iy) ± |Mx|·Ly/(2Ix) = {Dt.F(r.Ptb, "0.00")} ± {Dt.F(dp, "0.00")} → pmax = {Dt.F(r.Pmax, "0.00")}; pmin = {Dt.F(r.Pmin, "0.00")} kN/m²");
        r.Checks.Add(Dt.Check("Áp lực trung bình ptb ≤ R", r.Ptb, R, "kN/m²", "TCVN 9362:2012"));
        r.Checks.Add(Dt.Check("Áp lực lớn nhất pmax ≤ 1,2R", r.Pmax, 1.2 * R, "kN/m²", "TCVN 9362:2012"));
        if (r.Pmin < 0) r.Messages.Add($"Cảnh báo: pmin = {Dt.F(r.Pmin, "0.0")} kN/m² < 0 – đáy móng bị tách một phần; nên mở rộng bè về phía lệch tâm hoặc điều chỉnh vị trí bè");
        if (Math.Abs(r.Ex) > Lx / 6 || Math.Abs(r.Ey) > Ly / 6) r.Messages.Add("Cảnh báo: hợp lực nằm ngoài lõi tiết diện (e > L/6)");
        if (r.Ptb > R + 1e-6 || r.Pmax > 1.2 * R + 1e-6) r.Messages.Add("LỖI nền không đủ khả năng chịu lực – tăng kích thước bè, gia cố nền hoặc chuyển móng cọc");

        // ---- dải tính toán ----
        double sumN = r.SumN;
        double MyD = cols.Sum(x => x.N * (x.X - xc) + x.My), MxD = cols.Sum(x => x.N * (x.Y - yc) + x.Mx);
        double Pd(double x, double y) => sumN / r.A + MyD * (x - xc) / r.Iy + MxD * (y - yc) / r.Ix;
        r.Calc.Add("## 2. Dải tính toán (tải tính toán, phản lực hiệu chỉnh theo ACI 336.2R)");
        r.Calc.Add($"Áp lực tính toán p(x,y) = ΣN/A + My·(x − xc)/Iy + Mx·(y − yc)/Ix với ΣN = {Dt.F(sumN, "0.0")} kN; My = {Dt.F(MyD, "0.0")}; Mx = {Dt.F(MxD, "0.0")} kN.m");
        r.Calc.Add("Dải: tải TB = (ΣN dải + q·B·L)/2; F = tải TB/ΣN dải; q' = tải TB/(B·L); M(x) = q'·B·x²/2 − ΣF·Nj·(x − xj) (dương: căng thớ dưới)");
        foreach (bool alongX in new[] { true, false })
        {
            var lines = Rows(cols.Select(x => alongX ? x.Y : x.X));
            double W = alongX ? Ly : Lx, L = alongX ? Lx : Ly;
            for (int i = 0; i < lines.Count; i++)
            {
                double from = i == 0 ? 0 : (lines[i - 1] + lines[i]) / 2;
                double to = i == lines.Count - 1 ? W : (lines[i] + lines[i + 1]) / 2;
                var st = new RaftStrip { AlongX = alongX, Line = lines[i], From = from, To = to, Length = L };
                foreach (var col in cols.Where(x => Math.Abs((alongX ? x.Y : x.X) - lines[i]) <= RowTol + 1e-9))
                    st.Loads.Add((alongX ? col.X : col.Y, col.N));
                st.SumN = st.Loads.Sum(x => x.N);
                double mid = (from + to) / 2;
                st.Q = alongX ? Pd(xc, mid) : Pd(mid, yc);
                double area = st.Width * L;
                double avg = (st.SumN + st.Q * area) / 2;
                st.F = st.SumN > 1e-9 ? avg / st.SumN : 1;
                st.QAdj = area > 1e-9 ? avg / area : 0;
                // các điểm ứng viên: đầu, cuối, tại cột, chỗ lực cắt bằng 0 trong từng đoạn
                double w = st.QAdj * st.Width;
                var cand = new List<double> { 0, L };
                var sorted = st.Loads.OrderBy(x => x.Pos).ToList();
                double acc = 0, prev = 0;
                foreach (var (p, n) in sorted)
                {
                    cand.Add(p);
                    if (w > 1e-12) { double x0 = acc / w; if (x0 > prev && x0 < p) cand.Add(x0); }
                    acc += st.F * n; prev = p;
                }
                if (w > 1e-12) { double x0 = acc / w; if (x0 > prev && x0 < L) cand.Add(x0); }
                st.MBot = 0; st.MTop = 0;
                foreach (double x in cand)
                {
                    double m = st.MomentAt(x);
                    if (m > st.MBot) { st.MBot = m; st.XBot = x; }
                    if (m < st.MTop) { st.MTop = m; st.XTop = x; }
                }
                r.Strips.Add(st);
                r.Calc.Add($"Dải {(alongX ? "X" : "Y")} tại {(alongX ? "y" : "x")} = {Dt.F(st.Line)} m (B = {Dt.F(st.Width, "0.00")} m): ΣN = {Dt.F(st.SumN, "0.0")} kN; q = {Dt.F(st.Q, "0.00")} kN/m²; "
                           + $"F = {Dt.F(st.F, "0.000")}; q' = {Dt.F(st.QAdj, "0.00")} kN/m²; M căng dưới = {Dt.F(st.MBot, "0.0")} kN.m ({Dt.F(st.MBotPerM, "0.0")}/m), "
                           + $"M căng trên = {Dt.F(st.MTop, "0.0")} kN.m ({Dt.F(st.MTopPerM, "0.0")}/m)");
            }
        }
        r.MBotX = r.Strips.Where(x => x.AlongX).Select(x => x.MBotPerM).DefaultIfEmpty(0).Max();
        r.MTopX = r.Strips.Where(x => x.AlongX).Select(x => -x.MTopPerM).DefaultIfEmpty(0).Max();
        r.MBotY = r.Strips.Where(x => !x.AlongX).Select(x => x.MBotPerM).DefaultIfEmpty(0).Max();
        r.MTopY = r.Strips.Where(x => !x.AlongX).Select(x => -x.MTopPerM).DefaultIfEmpty(0).Max();

        // ---- thép ----
        r.Calc.Add("## 3. Cốt thép (TCVN 5574:2018 mục 8.1.2.2, b = 1 m, μmin = 0,1 %)");
        // thứ tự lớp: dưới X (ngoài cùng), dưới Y; trên X (ngoài cùng), trên Y
        int dGuess = 16;
        double dgx = (BotDiaX > 0 ? BotDiaX : dGuess) / 1000.0, dgy = (BotDiaY > 0 ? BotDiaY : dGuess) / 1000.0;
        double tgx = (TopDiaX > 0 ? TopDiaX : dGuess) / 1000.0, tgy = (TopDiaY > 0 ? TopDiaY : dGuess) / 1000.0;
        r.BotX = Layer(r, "Thép dưới X", r.MBotX, T - c - dgx / 2, BotDiaX, BotSX, conc, main, v => r.AsBotXReq = v);
        dgx = r.BotX.D / 1000.0;
        r.BotY = Layer(r, "Thép dưới Y", r.MBotY, T - c - dgx - dgy / 2, BotDiaY, BotSY, conc, main, v => r.AsBotYReq = v);
        r.TopX = Layer(r, "Thép trên X", r.MTopX, T - c - tgx / 2, TopDiaX, TopSX, conc, main, v => r.AsTopXReq = v);
        tgx = r.TopX.D / 1000.0;
        r.TopY = Layer(r, "Thép trên Y", r.MTopY, T - c - tgx - tgy / 2, TopDiaY, TopSY, conc, main, v => r.AsTopYReq = v);

        // ---- chọc thủng ----
        r.Calc.Add("## 4. Chọc thủng tại chân cột (TCVN 5574:2018 mục 8.1.6.3.1): F ≤ Rbt·u·h0");
        double h0p = T - c - Math.Max(r.BotX.D, r.BotY.D) / 1000.0;
        for (int i = 0; i < cols.Count; i++)
        {
            var col = cols[i];
            double p = Pd(col.X, col.Y);
            double hx = col.B / 2 + h0p / 2, hy = col.H / 2 + h0p / 2;           // nửa cạnh chu vi tính toán
            double ax = col.B / 2 + h0p, ay = col.H / 2 + h0p;                   // nửa cạnh đáy tháp
            bool l = col.X - hx <= 1e-9, rr = col.X + hx >= Lx - 1e-9, b = col.Y - hy <= 1e-9, t = col.Y + hy >= Ly - 1e-9;
            double ux0 = Math.Max(0, col.X - hx), ux1 = Math.Min(Lx, col.X + hx), uy0 = Math.Max(0, col.Y - hy), uy1 = Math.Min(Ly, col.Y + hy);
            double u = (b ? 0 : ux1 - ux0) + (t ? 0 : ux1 - ux0) + (l ? 0 : uy1 - uy0) + (rr ? 0 : uy1 - uy0);
            double ab = (Math.Min(Lx, col.X + ax) - Math.Max(0, col.X - ax)) * (Math.Min(Ly, col.Y + ay) - Math.Max(0, col.Y - ay));
            int edges = (l ? 1 : 0) + (rr ? 1 : 0) + (b ? 1 : 0) + (t ? 1 : 0);
            var pu = new RaftPunch
            {
                Column = i, H0 = h0p, U = u, Ab = ab, P = p, F = Math.Max(0, col.N - Math.Max(p, 0) * ab),
                Position = edges == 0 ? "giữa" : edges == 1 ? "biên" : "góc"
            };
            pu.Fu = conc.Rbt * u * h0p;
            r.Punch.Add(pu);
            string nm = $"cột {i + 1} ({Dt.F(col.X)}; {Dt.F(col.Y)})";
            r.Calc.Add($"Chọc thủng {nm}, cột {pu.Position}: h0 = {Dt.Mm(h0p)} mm; u = {Dt.F(u, "0.000")} m; Ab = {Dt.F(ab, "0.000")} m²; p = {Dt.F(p, "0.00")} kN/m²; "
                       + $"F = N − p·Ab = {Dt.F(col.N, "0.0")} − {Dt.F(p, "0.00")}×{Dt.F(ab, "0.000")} = {Dt.F(pu.F, "0.0")} kN; Fb,ult = Rbt·u·h0 = {Dt.F(conc.Rbt, "0")}×{Dt.F(u, "0.000")}×{Dt.F(h0p, "0.000")} = {Dt.F(pu.Fu, "0.0")} kN");
            r.Checks.Add(Dt.Check("Chọc thủng " + nm, pu.F, pu.Fu, "kN", "TCVN 5574:2018 8.1.6.3.1"));
            if (pu.F > pu.Fu + 1e-6)
            {
                double need = T + 0.05 * Math.Ceiling((pu.F / pu.Fu - 1) * 4 + 1e-9);
                r.Messages.Add($"LỖI chọc thủng tại {nm}: F = {Dt.F(pu.F, "0")} kN > Fb,ult = {Dt.F(pu.Fu, "0")} kN – tăng chiều dày bè (thử ≥ {Dt.Mm(need)} mm), mở rộng mũ cột hoặc đặt cốt đai chịu chọc thủng");
            }
        }

        // ---- thép bố trí ----
        BuildBars(r, s);
        RebarNumbering.Renumber(r.Bars);
        return r;
    }

    private const double RowTol = 0.30;

    /// <summary>Gom toạ độ thành hàng (sai lệch ≤ 0,3 m), trả về toạ độ trung bình tăng dần.</summary>
    private static List<double> Rows(IEnumerable<double> vals)
    {
        var res = new List<List<double>>();
        foreach (double v in vals.OrderBy(x => x))
        {
            if (res.Count > 0 && v - res[^1][0] <= RowTol + 1e-9) res[^1].Add(v);
            else res.Add(new List<double> { v });
        }
        return res.Select(x => x.Average()).ToList();
    }

    private (int D, double S) Layer(RaftResult r, string name, double mPerM, double h0, int userD, double userS,
        Material conc, Material main, Action<double> setReq)
    {
        var fl = RcSection.DesignRect(mPerM, 1.0, h0, 0.05, conc, main, 0.1);
        setReq(fl.As);
        (int D, double S) ch;
        bool user = userD > 0 && userS > 1e-6;
        if (user) ch = (userD, userS);
        else ch = Dt.ChooseSpaced(fl.As, Dias, 0.20, 0.10);
        double asProv = Dt.AreaPerM(ch.D, ch.S);
        double mu = RcSection.MomentCapacity(1.0, h0, asProv, 0, 0.05, conc, main);
        r.Calc.Add($"{name}: M = {Dt.F(mPerM, "0.0")} kN.m/m; h0 = {Dt.Mm(h0)} mm; αm = {Dt.F(fl.AlphaM, "0.000")}; As,yc = {Dt.F(fl.As * 1e4, "0.00")} cm²/m → "
                   + $"{(user ? "chọn (người dùng)" : "chọn")} {Dt.Spaced(ch.D, ch.S)} (As = {Dt.F(asProv * 1e4, "0.00")} cm²/m; [M] = {Dt.F(mu, "0.0")} kN.m/m)");
        r.Checks.Add(Dt.Check(name + $" {Dt.Spaced(ch.D, ch.S)}: M ≤ [M]", mPerM, mu, "kN.m/m", "TCVN 5574:2018 8.1.2.2"));
        if (asProv + 1e-12 < fl.As) r.Messages.Add($"LỖI {name} {Dt.Spaced(ch.D, ch.S)}: As = {Dt.F(asProv * 1e4, "0.00")} < As,yc = {Dt.F(fl.As * 1e4, "0.00")} cm²/m – tăng Ø, giảm bước hoặc tăng chiều dày bè");
        if (!fl.Ok) r.Messages.Add("LỖI " + name + ": " + fl.Note);
        else if (fl.Doubly) r.Messages.Add($"Cảnh báo {name}: αm > αR – nên tăng chiều dày bè");
        if (ch.S > 0.20 + 1e-9) r.Messages.Add($"Cảnh báo {name}: bước {Dt.Mm(ch.S)} mm > 200 mm (cấu tạo)");
        return ch;
    }

    private void BuildBars(RaftResult r, DetailSettings s)
    {
        double c = s.CoverFooting;
        double leg = Dt.R10(Math.Max(T - 2 * c, 0.1));
        void Mesh(string role, (int D, double S) ch, bool alongX, bool top)
        {
            if (ch.D <= 0 || ch.S <= 1e-9) return;
            double L = alongX ? Lx : Ly, W = alongX ? Ly : Lx;
            int n = Dt.CountAcross(W, c, ch.S);
            var bm = Dt.Hooked(role, ch.D, n, Dt.R10(L - 2 * c), leg, leg, top, ch.S);
            r.Bars.Add(bm);
            if (bm.Length > s.StockLength + 1e-9)
            {
                double lap = Dt.R10(Math.Max(RebarDetailing.LapLength(s.Conc(), s.Main(), ch.D), 30 * ch.D / 1000.0));
                r.Messages.Add($"{role} {Dt.Spaced(ch.D, ch.S)} dài {Dt.F(bm.Length, "0.00")} m > {Dt.F(s.StockLength, "0.0")} m – nối chồng ll = {Dt.Mm(lap)} mm, so le ≥ 1,3·ll, không quá 50 % số thanh tại một tiết diện; "
                               + (top ? "thép trên nối tại vùng chân cột" : "thép dưới nối tại giữa nhịp giữa các cột"));
            }
        }
        Mesh("Lớp dưới X", r.BotX, true, false);
        Mesh("Lớp dưới Y", r.BotY, false, false);
        Mesh("Lớp trên X", r.TopX, true, true);
        Mesh("Lớp trên Y", r.TopY, false, true);
        if (Chairs)
        {
            int d = T > 0.8 ? 16 : T > 0.5 ? 14 : 12;
            double hc = Dt.R10(Math.Max(T - 2 * c - (r.BotX.D + r.BotY.D + r.TopX.D + r.TopY.D) / 1000.0, 0.05));
            int n = (int)Math.Ceiling(Lx / 1.0 - 1e-9) * (int)Math.Ceiling(Ly / 1.0 - 1e-9);
            var ch = new BarMark
            {
                Role = "Con kê", Dia = d, Count = n, Shape = "U",
                Segments = new List<double> { 200, Math.Round(hc * 1000), 300, Math.Round(hc * 1000), 200 },
                Length = Dt.R10(0.2 + hc + 0.3 + hc + 0.2), Start = 0, End = 0.3
            };
            r.Bars.Add(ch);
        }
    }

    // ------------------------------------------------------------------
    // Bản vẽ
    // ------------------------------------------------------------------
    public List<Figure> Draw(DetailResult res, DetailSettings s)
    {
        s ??= new DetailSettings();
        var r = res as RaftResult ?? Design(s);
        var figs = new List<Figure>();
        if (Lx <= 0 || Ly <= 0 || T <= 0) return figs;
        if (r.Bars.Count > 0 && r.Bars.Any(b => b.Mark <= 0)) RebarNumbering.Renumber(r.Bars);
        figs.Add(new Figure(Plan(r, s, 100), "Mặt bằng " + Name));
        figs.Add(new Figure(Section(r, s, 100, 50), "Mặt cắt A-A " + Name));
        return figs;
    }

    private int MarkOf(RaftResult r, string role) => r.Bars.FirstOrDefault(b => b.Role == role)?.Mark ?? 0;

    private DxfWriter Plan(RaftResult r, DetailSettings s, double S)
    {
        var w = new DxfWriter(S);
        double th = w.Th;
        double L = Lx * 1000, B = Ly * 1000, c = s.CoverFooting * 1000;
        var cols = (Columns ?? new List<RaftColumn>()).Where(x => x != null).ToList();
        w.Rect(Lay.BeTong, 0, 0, L, B);
        // lưới trục qua các hàng cột
        var xs = Rows(cols.Select(x => x.X)).Select(x => x * 1000).ToList();
        var ys = Rows(cols.Select(x => x.Y)).Select(x => x * 1000).ToList();
        double ext = 8.5 * th;   // kích thước (−4th, −7th) nằm trong phạm vi đầu trục
        for (int i = 0; i < xs.Count; i++)
        {
            w.Line(Lay.Truc, xs[i], -ext, xs[i], B + ext, "CENTER");
            w.GridBubble(xs[i], B + ext + w.BubbleR, (i + 1).ToString(Dt.Inv));
        }
        for (int j = 0; j < ys.Count; j++)
        {
            w.Line(Lay.Truc, -ext, ys[j], L + ext, ys[j], "CENTER");
            w.GridBubble(-ext - w.BubbleR, ys[j], ((char)('A' + Math.Min(j, 25))).ToString());
        }
        foreach (var col in cols)
        {
            double x = col.X * 1000, y = col.Y * 1000, b = col.B * 1000, h = col.H * 1000;
            w.Rect(Lay.BeTong, x - b / 2, y - h / 2, x + b / 2, y + h / 2);
            w.Line(Lay.BeTong, x - b / 2, y - h / 2, x + b / 2, y + h / 2);
            w.Line(Lay.BeTong, x - b / 2, y + h / 2, x + b / 2, y - h / 2);
        }
        // thanh điển hình: lớp dưới nét liền, lớp trên nét đứt
        double yb = B * 0.20, xb = L * 0.20, yt = B * 0.80, xt = L * 0.80;
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (c, yb), (L - c, yb) }, false, 0.4 * S);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xb, c), (xb, B - c) }, false, 0.4 * S);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (c, yt), (L - c, yt) }, false, 0.4 * S, "DASHED");
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xt, c), (xt, B - c) }, false, 0.4 * S, "DASHED");
        w.LeaderTag(L * 0.55, yb, L * 0.55 + 2 * th, yb + 2.5 * th, Dt.Spaced(r.BotX.D, r.BotX.S) + " (dưới)", MarkOf(r, "Lớp dưới X"), 1);
        w.LeaderTag(xb, B * 0.45, xb + 2.5 * th, B * 0.45 + 2 * th, Dt.Spaced(r.BotY.D, r.BotY.S) + " (dưới)", MarkOf(r, "Lớp dưới Y"), 1);
        w.LeaderTag(L * 0.35, yt, L * 0.35 + 2 * th, yt - 2.5 * th, Dt.Spaced(r.TopX.D, r.TopX.S) + " (trên)", MarkOf(r, "Lớp trên X"), 1);
        w.LeaderTag(xt, B * 0.60, xt - 2.5 * th, B * 0.60 + 2 * th, Dt.Spaced(r.TopY.D, r.TopY.S) + " (trên)", MarkOf(r, "Lớp trên Y"), -1);
        // vạch cắt A-A qua hàng cột đầu
        double ya = ys.Count > 0 ? ys[0] : B / 2;
        w.CutMark(-2 * th, ya, -0.5 * th, ya, "A", -2.8 * th, ya + 1.2 * th);
        w.CutMark(L + 0.5 * th, ya, L + 2 * th, ya, "A", L + 2.8 * th, ya + 1.2 * th);
        // kích thước
        var chainX = new List<double> { 0 }; chainX.AddRange(xs); chainX.Add(L);
        var chainY = new List<double> { 0 }; chainY.AddRange(ys); chainY.Add(B);
        w.DimChainH(chainX.Distinct().OrderBy(x => x).ToList(), 0, -4 * th);
        w.Dim(0, 0, L, 0, -7 * th);
        w.DimChainV(chainY.Distinct().OrderBy(x => x).ToList(), L, L + 4 * th);
        w.Dim(L, 0, L, B, -(7 * th));
        w.Title(L / 2, w.Extents.Y2 + 4 * th, $"MẶT BẰNG MÓNG BÈ {Name} (SL={Math.Max(1, Quantity)})", DrawUtil.ScaleText(S));
        return w;
    }

    private DxfWriter Section(RaftResult r, DetailSettings s, double S, double det)
    {
        var w = new DxfWriter(S);
        double th = w.Th, k = S / det;
        double L = Lx * 1000 * k, H = T * 1000 * k, c = s.CoverFooting * 1000 * k, lot = s.LeanThickness * 1000 * k;
        var cols = (Columns ?? new List<RaftColumn>()).Where(x => x != null).ToList();
        var ys = Rows(cols.Select(x => x.Y));
        double ya = ys.Count > 0 ? ys[0] : Ly / 2;
        var row = cols.Where(x => Math.Abs(x.Y - ya) <= RowTol + 1e-9).OrderBy(x => x.X).ToList();
        if (w.HatchOn)
        {
            w.HatchMatRect(HatchMaterial.Earth, -lot - 2 * th, -lot - 1.2 * th, L + lot + 2 * th, -lot);
            w.HatchMatRect(HatchMaterial.LeanConcrete, -lot, -lot, L + lot, 0);
            w.HatchMatRect(HatchMaterial.Concrete, 0, 0, L, H);
        }
        w.Rect(Lay.BeTong, -lot, -lot, L + lot, 0);
        w.Rect(Lay.BeTong, 0, 0, L, H);
        double neck = 500 * k;
        foreach (var col in row)
        {
            double x = col.X * 1000 * k, b = col.B * 1000 * k;
            w.Line(Lay.BeTong, x - b / 2, H, x - b / 2, H + neck);
            w.Line(Lay.BeTong, x + b / 2, H, x + b / 2, H + neck);
            w.BreakH(Lay.BeTong, x - b / 2, x + b / 2, H + neck);
            w.Line(Lay.Truc, x, -lot - 2 * th, x, H + neck + 2 * th, "CENTER");
        }
        double dbx = r.BotX.D * k, dby = r.BotY.D * k, dtx = r.TopX.D * k, dty = r.TopY.D * k;
        double leg = Math.Max(H - 2 * c, 0);
        // dưới X (vẽ dọc mặt cắt) và trên X
        double y1 = c + dbx / 2, y2 = H - c - dtx / 2;
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (c, y1 + leg - dbx), (c, y1), (L - c, y1), (L - c, y1 + leg - dbx) }, false, 0.4 * S);
        w.Polyline(Lay.Thep, new List<(double X, double Y)> { (c + dtx, y2 - leg + dtx), (c + dtx, y2), (L - c - dtx, y2), (L - c - dtx, y2 - leg + dtx) }, false, 0.4 * S);
        // chấm thép phương Y
        double yby = c + dbx + dby / 2, yty = H - c - dtx - dty / 2;
        double sb = Math.Max(50, r.BotY.S * 1000) * k, stp = Math.Max(50, r.TopY.S * 1000) * k;
        for (double x = c + dbx; x <= L - c - dbx + 1e-6; x += sb) w.Dot(Lay.Thep, x, yby, Math.Max(dby / 2, 0.12 * th));
        for (double x = c + dtx; x <= L - c - dtx + 1e-6; x += stp) w.Dot(Lay.Thep, x, yty, Math.Max(dty / 2, 0.12 * th));
        // nhãn: trên ghi lên, dưới ghi xuống
        w.LeaderTag(L * 0.18, y1, L * 0.18 + 2 * th, -lot - 3.5 * th, Dt.Spaced(r.BotX.D, r.BotX.S), MarkOf(r, "Lớp dưới X"), 1);
        w.LeaderTag(L * 0.62 + sb / 2, yby, L * 0.62 + 2 * th, -lot - 6.2 * th, Dt.Spaced(r.BotY.D, r.BotY.S), MarkOf(r, "Lớp dưới Y"), 1);
        w.LeaderTag(L * 0.18, y2, L * 0.18 + 2 * th, H + neck + 3 * th, Dt.Spaced(r.TopX.D, r.TopX.S), MarkOf(r, "Lớp trên X"), 1);
        w.LeaderTag(L * 0.62 + stp / 2, yty, L * 0.62 + 2 * th, H + neck + 5.8 * th, Dt.Spaced(r.TopY.D, r.TopY.S), MarkOf(r, "Lớp trên Y"), 1);
        // kích thước
        w.Dim(0, 0, 0, H, 2.5 * th, k);
        w.Dim(-lot, -lot, -lot, 0, 2.5 * th + lot, k);
        var chain = new List<double> { 0 }; chain.AddRange(row.Select(x => x.X * 1000 * k)); chain.Add(L);
        w.DimChainH(chain.Distinct().OrderBy(x => x).ToList(), -lot, -lot - 9 * th, k);
        w.Dim(0, -lot, L, -lot, -12 * th, k);
        w.ElevMark(L + 3 * th, H, DxfWriter.ElevText(-Df + T), 1);
        w.ElevMark(L + 3 * th, 0, DxfWriter.ElevText(-Df), 1);
        w.Title(L / 2, w.Extents.Y1 - 1.2 * th, "MẶT CẮT A-A", DrawUtil.ScaleText(det), true);
        return w;
    }
}
