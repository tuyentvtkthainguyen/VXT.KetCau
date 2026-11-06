using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// MÓNG ĐƠN – MÓNG BĂNG – MÓNG CỌC (đài cọc) nhập tay: tải chân cột do người dùng nhập.
// Móng đơn: TCVN 9362:2012 (áp lực nền) + TCVN 5574:2018 (chọc thủng 8.1.6, uốn). Móng băng: dầm trên nền đàn hồi Winkler.
// Đài cọc: TCVN 10304:2014 (lực tác dụng lên cọc), TCVN 5574:2018 (chọc thủng, cắt, uốn).
// =====================================================================

public class ManualFooting : IDetailMember
{
    [Field("Tên móng", Group = "Chung")] public string Name { get; set; } = "M1";
    [Field("Số lượng", Group = "Chung")] public int Quantity { get; set; } = 1;
    [Field("Cột bc (phương L)", Unit = "mm", Scale = 1000, Group = "Cột")] public double ColL { get; set; } = 0.22;
    [Field("Cột hc (phương B)", Unit = "mm", Scale = 1000, Group = "Cột")] public double ColB { get; set; } = 0.30;
    [Field("N tính toán", Unit = "kN", Group = "Tải chân cột")] public double N { get; set; } = 600;
    [Field("M theo phương L", Unit = "kN.m", Group = "Tải chân cột")] public double ML { get; set; } = 20;
    [Field("M theo phương B", Unit = "kN.m", Group = "Tải chân cột")] public double MB { get; set; } = 10;
    [Field("N tiêu chuẩn (0 = N/1,15)", Unit = "kN", Group = "Tải chân cột")] public double Ntc { get; set; }
    [Field("Phản lực dầm móng / giằng cộng thêm vào N", Unit = "kN", Group = "Tải chân cột",
        Hint = "ΣR các giằng móng tựa lên móng (dầm móng: chọn \"Giằng tựa lên móng\" để tính R) – cộng vào lực dọc chân cột")] public double BeamReaction { get; set; }
    [Field("Sức chịu tải nền R (tiêu chuẩn)", Unit = "kN/m²", Group = "Nền")] public double R { get; set; } = 150;
    [Field("Độ sâu chôn móng Df", Unit = "m", Group = "Nền")] public double Df { get; set; } = 1.5;
    [Field("γtb đất + móng", Unit = "kN/m³", Group = "Nền")] public double GammaTb { get; set; } = 20;
    [Field("Cạnh L (0 = tự chọn)", Unit = "m", Group = "Kích thước")] public double L { get; set; }
    [Field("Cạnh B (0 = tự chọn)", Unit = "m", Group = "Kích thước")] public double B { get; set; }
    [Field("Chiều cao móng H (0 = tự chọn)", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double H { get; set; }
    [Field("Chiều cao mép (móng vát, 0 = móng phẳng)", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Hb { get; set; }
    [Field("Hình dạng đế móng", Group = "Kích thước", Choices = "Vát|Bậc", Hint = "Bậc: Hb = chiều cao bậc dưới, bậc trên kích thước StepL × StepB – kiểm tra thêm chọc thủng từ mép bậc")]
    public string FootShape { get; set; } = "Vát";
    [Field("Móng bậc – cạnh bậc trên phương L (0 = tự chọn)", Unit = "m", Group = "Kích thước")] public double StepL { get; set; }
    [Field("Móng bậc – cạnh bậc trên phương B (0 = tự chọn)", Unit = "m", Group = "Kích thước")] public double StepB { get; set; }
    /// <summary>Phản lực giằng / dầm móng tự chuyển từ các dầm móng có ghi tên móng này ở gối (DetailingProject.RunAll); &gt; 0 thì dùng thay ô nhập tay.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double AutoBeamReaction { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string AutoBeamReactionText { get; set; } = "";
    [Field("Thép phương L – Ø (0 = tự chọn)", Unit = "mm", Group = "Thép")] public int DiaL { get; set; }
    [Field("Thép phương L – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double SL { get; set; }
    [Field("Thép phương B – Ø", Unit = "mm", Group = "Thép")] public int DiaB { get; set; }
    [Field("Thép phương B – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double SB { get; set; }

    [Field("Phương L – bên giáp nhà hàng xóm (hết đất)", Group = "Bê tông lót, giáp nhà hàng xóm (móng lệch tâm)", Choices = "Không|Giáp nhà hàng xóm bên trái (hết đất)|Giáp nhà hàng xóm bên phải (hết đất)",
        Hint = "phía giáp nhà hàng xóm: bê tông móng và bê tông lót không vươn sang đất nhà bên – cột lệch về mép móng")] public string BoundaryL { get; set; } = "Không";
    [Field("Phương B – bên giáp nhà hàng xóm (hết đất)", Group = "Bê tông lót, giáp nhà hàng xóm (móng lệch tâm)", Choices = "Không|Giáp nhà hàng xóm phía dưới (hết đất)|Giáp nhà hàng xóm phía trên (hết đất)")] public string BoundaryB { get; set; } = "Không";
    [Field("Mép ngoài cột cách ranh đất", Unit = "mm", Scale = 1000, Group = "Bê tông lót, giáp nhà hàng xóm (móng lệch tâm)")] public double Setback { get; set; }
    [Field("Có dầm móng cân bằng mô men lệch tâm N·e", Group = "Bê tông lót, giáp nhà hàng xóm (móng lệch tâm)", Hint = "bỏ chọn: áp lực đáy móng kể thêm mô men N·e")] public bool BalanceBeam { get; set; } = true;
    [Field("Bê tông lót vươn ra (phía không giáp nhà bên)", Unit = "mm", Scale = 1000, Group = "Bê tông lót, giáp nhà hàng xóm (móng lệch tâm)")] public double LeanOverhang { get; set; } = 0.10;

    public static readonly int[] Dias = { 10, 12, 14, 16, 18, 20 };

    /// <summary>Áp lực đáy móng tiêu chuẩn: ptb = Ntc/A + γtb·Df; pmax,min = ptb ± 6ML/(B·L²) ± 6MB/(L·B²).</summary>
    public static (double Ptb, double Pmax, double Pmin) Pressure(double ntc, double mL, double mB, double L, double B, double gtb, double df)
    {
        double ptb = ntc / (L * B) + gtb * df;
        double d = 6 * Math.Abs(mL) / (B * L * L) + 6 * Math.Abs(mB) / (L * B * B);
        return (ptb, ptb + d, ptb - d);
    }

    public DetailResult Run(DetailSettings s)
    {
        var f = new FootingDesign { Name = Name, Quantity = Math.Max(1, Quantity), ColumnB = ColB, ColumnH = ColL, Depth = Df, R = R };
        var r = new DetailResult { Kind = "Móng đơn", Name = Name, Quantity = f.Quantity, Source = f };
        // lực dọc kể phản lực giằng / dầm móng tựa lên móng (che thuộc tính N trong phạm vi hàm)
        double beamR = TieReaction.Use(AutoBeamReaction, BeamReaction);
        double N = this.N + beamR;
        r.Messages = f.Messages; r.Checks = f.Checks; r.Bars = f.Bars;
        TieReaction.Report(r, this.N, N, AutoBeamReaction, AutoBeamReactionText, BeamReaction);
        var conc = DetailShim.TryConcrete(s.Concrete, r.Messages);
        var main = DetailShim.TryRebar(s.RebarMain, r.Messages);
        r.ConcreteClass = conc.Name;
        if (N <= 0 || R <= GammaTb * Df + 5 || ColL <= 0 || ColB <= 0)
        {
            r.Messages.Add("LỖI tải N > 0, kích thước cột > 0 và R > γtb·Df (nền đủ chịu trọng lượng đất đắp)");
            return r;
        }
        double ntc = Ntc > 0 ? Ntc : N / 1.15, kTc = ntc / N;
        double mLt = ML * kTc, mBt = MB * kTc;
        // ---- sát ranh: cột lệch về mép móng; e = L/2 − (bc/2 + khoảng lùi) ----
        bool bL = Dt.IsNeighbour(BoundaryL), bB = Dt.IsNeighbour(BoundaryB);
        double EL(double ll) => bL ? Math.Max(ll / 2 - ColL / 2 - Math.Max(Setback, 0), 0) : 0;
        double EB(double bb) => bB ? Math.Max(bb / 2 - ColB / 2 - Math.Max(Setback, 0), 0) : 0;
        double AddL(double ll) => BalanceBeam ? 0 : ntc * EL(ll);
        double AddB(double bb) => BalanceBeam ? 0 : ntc * EB(bb);
        // ---- kích thước đáy ----
        double l = L, b = B;
        if (l <= 0 || b <= 0)
        {
            double a0 = 1.2 * ntc / (R - GammaTb * Df);
            double ratio = Math.Clamp(ColL / ColB, 0.6, 1.6);
            if (Math.Abs(ML) > 1.5 * Math.Abs(MB)) ratio = Math.Max(ratio, 1.2);
            b = b > 0 ? b : RcSection.CeilTo(Math.Sqrt(a0 / ratio), 0.1);
            l = l > 0 ? l : RcSection.CeilTo(a0 / b, 0.1);
            for (int it = 0; it < 60; it++)
            {
                var pp = Pressure(ntc, mLt + AddL(l), mBt + AddB(b), l, b, GammaTb, Df);
                if (pp.Ptb <= R && pp.Pmax <= 1.2 * R && pp.Pmin >= 0) break;
                if (L <= 0) l += 0.1;
                if (B <= 0 && (it % 2 == 1 || L > 0)) b += 0.1;
                l = Math.Round(l, 2); b = Math.Round(b, 2);
            }
        }
        var (ptb, pmax, pmin) = Pressure(ntc, mLt + AddL(l), mBt + AddB(b), l, b, GammaTb, Df);
        if (bL || bB)
        {
            var sides = new List<string>();
            if (bL) sides.Add((BoundaryL ?? "").Contains("trái") ? "trái" : "phải");
            if (bB) sides.Add((BoundaryB ?? "").Contains("dưới") ? "dưới" : "trên");
            f.Eccentric = new KetCauVXT.Design.Foundation.EccentricInfo
            {
                Boundary = true, Sides = string.Join(", ", sides),
                OffsetX = bL ? ((BoundaryL ?? "").Contains("trái") ? 1 : -1) * EL(l) : 0,
                OffsetY = bB ? ((BoundaryB ?? "").Contains("dưới") ? 1 : -1) * EB(b) : 0
            };
            double me = N * Math.Sqrt(EL(l) * EL(l) + EB(b) * EB(b));
            r.Calc.Add("Móng giáp nhà hàng xóm – hết đất (" + f.Eccentric.Sides + "): bê tông móng và bê tông lót không vươn sang đất nhà bên; tim cột lệch tâm móng eL = " + Dt.Mm(EL(l)) + " mm, eB = "
                       + Dt.Mm(EB(b)) + " mm → mô men lệch tâm M = N·e = " + Dt.F(me, "0.0") + " kN.m"
                       + (BalanceBeam ? " do dầm móng cân bằng chịu (tính dầm móng với mô men này ở đầu dầm)" : " – kể vào áp lực đáy móng"));
            if (BalanceBeam) r.Messages.Add("Dầm móng cân bằng nối móng " + Name + " với móng bên trong phải chịu M = N·e = " + Dt.F(me, "0.0") + " kN.m (nhập vào dầm móng: Vị trí = Dầm móng)");
        }
        f.L = l; f.B = b; f.Ptb = ptb; f.Pmax = pmax; f.Pmin = pmin; f.N = N; f.Mx = MB; f.My = ML; f.Ntc = ntc;
        r.Calc.Add("## Móng đơn " + Name + " – cột " + Dt.Mm(ColL) + "x" + Dt.Mm(ColB) + ", N = " + Dt.F(N, "0.0") + " kN, ML = " + Dt.F(ML, "0.0") + ", MB = " + Dt.F(MB, "0.0") + " kN.m");
        r.Calc.Add("Đáy móng L × B = " + Dt.F(l) + " × " + Dt.F(b) + " m; Ntc = " + Dt.F(ntc, "0.0") + " kN (TCVN 9362:2012)");
        r.Calc.Add("ptb = Ntc/A + γtb·Df = " + Dt.F(ptb, "0.0") + " kN/m²; pmax, min = ptb ± 6M/(B·L²) = " + Dt.F(pmax, "0.0") + " / " + Dt.F(pmin, "0.0") + " kN/m²");
        r.Checks.Add(Dt.Check("Áp lực trung bình ptb ≤ R", ptb, R, "kN/m²"));
        r.Checks.Add(Dt.Check("Áp lực lớn nhất pmax ≤ 1,2R", pmax, 1.2 * R, "kN/m²"));
        r.Checks.Add(Dt.Check("Không tách đáy móng (pmin ≥ 0): phần âm của pmin", Math.Max(-pmin, 0), 0, "kN/m²"));

        // ---- chiều cao: chọc thủng (TCVN 5574:2018 8.1.6.3.1): F ≤ Rbt·u·h0, u = 2(bc + hc + 2h0) ----
        double c = Math.Max(s.CoverFooting, 0.035);
        double pnet = N / (l * b);
        double pnetMax = pnet + 6 * Math.Abs(ML) / (b * l * l) + 6 * Math.Abs(MB) / (l * b * b);
        (double F, double Fu) Punch(double h0)
        {
            // tháp chọc thủng bị cắt ở cạnh sát ranh (cột sát mép móng)
            double sbk = Math.Max(Setback, 0);
            double ax = bL ? ColL + sbk + h0 : ColL + 2 * h0, ay = bB ? ColB + sbk + h0 : ColB + 2 * h0;
            double ap = Math.Min(ax * ay, l * b);
            double lenL = bL ? sbk + ColL + h0 / 2 : ColL + h0, lenB = bB ? sbk + ColB + h0 / 2 : ColB + h0;
            double u = (bB ? 1 : 2) * lenL + (bL ? 1 : 2) * lenB;
            return (N - pnet * ap, conc.Rbt * u * h0);
        }
        // chọc thủng có kể mô men + cắt theo mép móng cho một chiều cao (dùng cả khi tự chọn H)
        (PunchingResult Pr, bool Cut, List<(string Dir, double Q, double Qb, double C, double L1, double H0)> Shear) ExtraChecks(double h0e0, double hbEff)
        {
            double sbk = Math.Max(Setback, 0);
            bool leftL = (BoundaryL ?? "").Contains("trái"), lowB = (BoundaryB ?? "").Contains("dưới");
            bool cutL = bL && sbk < h0e0 / 2, cutB = bB && sbk < h0e0 / 2;
            var ct = PunchingContour.Rect(ColL, ColB, h0e0,
                cutXNeg: cutL && leftL, cutXPos: cutL && !leftL, cutYNeg: cutB && lowB, cutYPos: cutB && !lowB,
                extXNeg: ColL / 2 + sbk, extXPos: ColL / 2 + sbk, extYNeg: ColB / 2 + sbk, extYPos: ColB / 2 + sbk);
            double mAboutY = ML + (BalanceBeam ? 0 : N * EL(l)), mAboutX = MB + (BalanceBeam ? 0 : N * EB(b));
            var pf = Punch(h0e0);
            var pr = Punching.Check(ct, pf.F, mAboutY, mAboutX, h0e0, conc.Rbt, 0.5, pf.Fu);
            var list = new List<(string, double, double, double, double, double)>();
            double hEdge0 = Math.Max(hbEff - c - 0.012, 0.05);
            foreach (bool alongL in new[] { true, false })
            {
                double cant = alongL ? (bL ? l - ColL - sbk : (l - ColL) / 2) : (bB ? b - ColB - sbk : (b - ColB) / 2);
                if (cant <= 1e-6) continue;
                double wid = alongL ? b : l;
                double pEdge = N / (l * b) + 6 * Math.Abs(alongL ? ML : MB) / (wid * Math.Pow(alongL ? l : b, 2));
                double h0s = hEdge0 < h0e0 - 1e-6 ? (h0e0 + hEdge0) / 2 : h0e0;   // móng vát: h0 trung bình trên đoạn công xôn (gần đúng)
                var es = Punching.EdgeShear(pEdge, wid, cant, h0s, conc.Rbt);
                list.Add((alongL ? "L" : "B", es.Q, es.Qb, es.C, cant, h0s));
            }
            return (pr, cutL || cutB, list);
        }
        double hh = H;
        if (hh <= 0)
        {
            hh = 0.30;
            while (hh < 2.0 && Punch(hh - c - 0.012).F > Punch(hh - c - 0.012).Fu) hh = Math.Round(hh + 0.05, 2);
            // móng cứng: h ≥ (L − bc)/6 để áp lực phân bố gần tuyến tính
            hh = Math.Max(hh, RcSection.CeilTo(Math.Max(l - ColL, b - ColB) / 6, 0.05));
            // tăng H tới khi đạt chọc thủng có kể mô men và cắt theo mép móng
            for (int it = 0; it < 40 && hh < 2.0; it++)
            {
                double hbT = Hb > 0 && Hb < hh ? Hb : hh;
                var exT = ExtraChecks(hh - c - 0.012, hbT);
                if (exT.Pr.Ok && exT.Shear.All(z => z.Q <= z.Qb + 1e-9)) break;
                hh = Math.Round(hh + 0.05, 2);
            }
        }
        f.H = hh; f.Hb = Hb > 0 && Hb < hh ? Hb : hh;
        double h0 = hh - c - 0.012;
        var (fP, fuP) = Punch(h0);
        r.Calc.Add("Chọc thủng: h0 = " + Dt.Mm(h0) + " mm, u = " + (bL || bB ? "chu vi tháp chọc thủng bị cắt ở cạnh sát ranh" : "2(bc + hc + 2h0)") + "; F = N − pnet·Atháp = "
                   + Dt.F(fP, "0.0") + " kN ≤ Rbt·u·h0 = " + Dt.F(fuP, "0.0") + " kN");
        r.Checks.Add(Dt.Check("Chọc thủng cột – móng F ≤ Rbt·u·h0", fP, fuP, "kN", "TCVN 5574:2018 mục 8.1.6.3"));
        // ---- chọc thủng có kể mô men (8.1.6.2) + cắt theo mép móng (8.1.3.3) ----
        var ex = ExtraChecks(h0, f.Hb);
        r.Calc.Add("Chọc thủng có kể mô men (TCVN 5574:2018 mục 8.1.6.2): " + ex.Pr.Text + (ex.Cut ? " – đường bao bị cắt ở cạnh sát ranh (3 cạnh / 2 cạnh khi cột góc)" : "")
                   + "; M lấy bằng 0,5·M chân cột (phần còn lại truyền qua áp lực đáy móng)");
        r.Checks.Add(Dt.Check("Chọc thủng có kể mô men F/Fb,u + ΣM/Mb,u ≤ 1", ex.Pr.Ratio, 1, "", "TCVN 5574:2018 mục 8.1.6.2"));
        foreach (var sh in ex.Shear)
        {
            r.Calc.Add("Cắt theo mép móng phương " + sh.Dir + ": Q = p·b·(l1 − c) = " + Dt.F(sh.Q, "0.0") + " kN ≤ Qb = 1,5·Rbt·b·h0²/c = " + Dt.F(sh.Qb, "0.0")
                       + " kN (c = " + Dt.Mm(sh.C) + " mm, l1 = " + Dt.Mm(sh.L1) + " mm, h0 = " + Dt.Mm(sh.H0) + " mm; 0,5 ≤ Qb/(Rbt·b·h0) ≤ 2,5)");
            r.Checks.Add(Dt.Check("Cắt theo mép móng phương " + sh.Dir + " Q ≤ Qb", sh.Q, sh.Qb, "kN", "TCVN 5574:2018 mục 8.1.3.3"));
        }
        // ---- móng bậc: chọc thủng từ mép bậc trên với h0 bậc dưới (TCVN 5574:2018 mục 8.1.6.3) ----
        bool stepped = (FootShape ?? "").StartsWith("Bậc") && f.Hb < f.H - 1e-6;
        if (stepped)
        {
            double stL = StepL > 0 ? StepL : RcSection.CeilTo(ColL + 2 * Math.Max(0.20, (l - ColL) / 4), 0.05);
            double stB = StepB > 0 ? StepB : RcSection.CeilTo(ColB + 2 * Math.Max(0.20, (b - ColB) / 4), 0.05);
            stL = Math.Min(stL, l); stB = Math.Min(stB, b);
            f.StepL = stL; f.StepB = stB;
            double h0b = f.Hb - c - 0.012;
            var sp = Punching.StepPunching(N, pnet, stL, stB, h0b, l, b, conc.Rbt);
            r.Calc.Add("Móng bậc: bậc trên " + Dt.Mm(stL) + " × " + Dt.Mm(stB) + " mm, bậc dưới cao " + Dt.Mm(f.Hb) + " mm → chọc thủng tại bậc: h0b = " + Dt.Mm(h0b)
                       + " mm, u = 2(Lb + Bb + 2h0b) = " + Dt.Mm(sp.U) + " mm; F = N − pnet·(Lb + 2h0b)(Bb + 2h0b) = " + Dt.F(sp.F, "0.0") + " kN ≤ Rbt·u·h0b = " + Dt.F(sp.Fu, "0.0") + " kN");
            r.Checks.Add(Dt.Check("Chọc thủng tại bậc móng F ≤ Rbt·u·h0b", sp.F, Math.Max(sp.Fu, 0), "kN", "TCVN 5574:2018 mục 8.1.6.3"));
            if (sp.F > sp.Fu + 1e-9) r.Messages.Add("LỖI chọc thủng tại bậc móng – tăng chiều cao bậc dưới Hb hoặc mở rộng bậc trên");
        }
        // ---- thép: mô men tại mép cột với áp lực hình thang ----
        double l1 = bL ? l - ColL - Math.Max(Setback, 0) : (l - ColL) / 2, b1 = bB ? b - ColB - Math.Max(Setback, 0) : (b - ColB) / 2;
        double pL1 = pnetMax - (pnetMax - (2 * pnet - pnetMax)) * l1 / l;
        double mL = b * l1 * l1 * (2 * pnetMax + pL1) / 6;
        double mB = l * b1 * b1 * pnet / 2 * (1 + 2 * Math.Abs(MB) / Math.Max(N * b, 1e-9));
        f.ML = mL; f.MB = mB;
        double asL = RcSection.DesignRect(mL, b, h0, c, conc, main, 0.05).As;
        double asB = RcSection.DesignRect(mB, l, h0 - 0.012, c, conc, main, 0.05).As;
        f.AsL = asL; f.AsB = asB;
        var sl = DiaL > 0 && SL > 0 ? (DiaL, SL) : Dt.ChooseSpaced(asL / b, Dias, 0.20, 0.10);
        var sb = DiaB > 0 && SB > 0 ? (DiaB, SB) : Dt.ChooseSpaced(asB / l, Dias, 0.20, 0.10);
        f.DiaL = sl.Item1; f.SpacingL = sl.Item2; f.DiaB = sb.Item1; f.SpacingB = sb.Item2;
        f.BarsL = Dt.Spaced(sl.Item1, sl.Item2); f.BarsB = Dt.Spaced(sb.Item1, sb.Item2);
        r.Calc.Add("Mô men mép cột phương L: M = B·l1²·(2pmax + p1)/6 = " + Dt.F(mL, "0.0") + " kN.m → As = " + Dt.F(asL * 1e4, "0.00") + " cm² → " + f.BarsL);
        r.Calc.Add("Mô men mép cột phương B: M = " + Dt.F(mB, "0.0") + " kN.m → As = " + Dt.F(asB * 1e4, "0.00") + " cm² → " + f.BarsB);
        double muL = RcSection.MomentCapacity(b, h0, Dt.AreaPerM(sl.Item1, sl.Item2) * b, 0, c, conc, main);
        double muB = RcSection.MomentCapacity(l, h0 - 0.012, Dt.AreaPerM(sb.Item1, sb.Item2) * l, 0, c, conc, main);
        r.Checks.Add(Dt.Check("Chịu uốn phương L " + f.BarsL, mL, muL, "kN.m"));
        r.Checks.Add(Dt.Check("Chịu uốn phương B " + f.BarsB, mB, muB, "kN.m"));
        foreach (var k in r.Checks.Where(x => !x.Ok)) r.Messages.Add("LỖI " + k.Name + ": " + Dt.F(k.Demand, "0.0") + " > " + Dt.F(k.Capacity, "0.0") + " " + k.Unit);

        double hookL = Dt.R10(Math.Max(10 * sl.Item1 / 1000.0, 0.10)), hookB = Dt.R10(Math.Max(10 * sb.Item1 / 1000.0, 0.10));
        double lenL = Dt.R10(l - 2 * c), lenB = Dt.R10(b - 2 * c);
        var bl = Dt.Hooked("Lớp dưới phương L", sl.Item1, (int)Math.Ceiling((b - 2 * c) / sl.Item2 - 1e-9) + 1, lenL, hookL, hookL, false, sl.Item2);
        bl.Start = c; bl.End = l - c;
        var bb = Dt.Hooked("Lớp dưới phương B", sb.Item1, (int)Math.Ceiling((l - 2 * c) / sb.Item2 - 1e-9) + 1, lenB, hookB, hookB, false, sb.Item2);
        bb.Start = c; bb.End = b - c;
        f.Bars.Add(bl); f.Bars.Add(bb);
        RebarNumbering.Renumber(f.Bars);
        double vol = stepped ? l * b * f.Hb + f.StepL * f.StepB * (f.H - f.Hb)
                             : l * b * f.Hb + (f.H - f.Hb) / 3 * (l * b + ColL * ColB + Math.Sqrt(l * b * ColL * ColB));
        r.ConcreteVolume = vol;
        r.FormworkArea = 2 * (l + b) * f.Hb + (stepped ? 2 * (f.StepL + f.StepB) * (f.H - f.Hb) : 0);
        double ov = Math.Max(LeanOverhang, 0);
        r.LeanVolume = (l + (bL ? 1 : 2) * ov) * (b + (bB ? 1 : 2) * ov) * s.LeanThickness;
        if (bL || bB) r.Extra.Add(("Xây gạch / tấm khuôn để lại cho thành móng giáp nhà hàng xóm", "m²", ((bL ? b : 0) + (bB ? l : 0)) * f.Hb));
        r.Extra.Add(("Đào đất hố móng (mở taluy 1:0,5 mỗi bên 0,3 m)", "m³", (l + 0.6 + 0.5 * Df) * (b + 0.6 + 0.5 * Df) * Df));
        return r;
    }

    public List<Figure> Draw(DetailResult r, DetailSettings s)
    {
        if (r?.Source is not FootingDesign f || f.L <= 0) return new List<Figure>();
        var p = DetailShim.Project(s);
        double S = FootingDrawing.CurrentDetailScale;
        return new List<Figure>
        {
            new(FootingDrawing.DetailPlan(p, f, f.Quantity, S), f.Name, true),
            new(FootingDrawing.DetailSection(p, f, S), f.Name + " mặt cắt")
        };
    }
}

// ---------------------------------------------------------------------
// MÓNG BĂNG (dầm móng chữ T ngược dưới dãy cột) – dầm trên nền đàn hồi
// ---------------------------------------------------------------------
public class StripColumn
{
    [Field("Tên cột")] public string Name { get; set; } = "C";
    [Field("Vị trí X từ đầu băng", Unit = "m")] public double X { get; set; }
    [Field("Cạnh cột dọc băng", Unit = "mm", Scale = 1000)] public double Bc { get; set; } = 0.22;
    [Field("Cạnh cột ngang băng", Unit = "mm", Scale = 1000)] public double BcT { get; set; } = 0.22;
    [Field("N tính toán", Unit = "kN")] public double N { get; set; }
    [Field("M quanh trục ngang băng", Unit = "kN.m")] public double M { get; set; }
    [Field("N tiêu chuẩn (0 = N/1,15)", Unit = "kN")] public double Ntc { get; set; }
}

public class ManualStrip : IDetailMember
{
    [Field("Tên móng băng", Group = "Chung")] public string Name { get; set; } = "MBG1";
    [Field("Số lượng", Group = "Chung")] public int Quantity { get; set; } = 1;
    [Field("Chiều dài băng", Unit = "m", Group = "Kích thước")] public double Length { get; set; } = 13.0;
    [Field("Bề rộng bản đáy B (0 = tự chọn)", Unit = "m", Group = "Kích thước")] public double B { get; set; }
    [Field("Chiều dày mép bản", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Hf { get; set; } = 0.25;
    [Field("Chiều dày bản tại chân sườn – đoạn vát (0 = bản phẳng)", Unit = "mm", Scale = 1000, Group = "Kích thước",
        Hint = "móng băng có đoạn vát: bản dày dần từ mép (Hf) tới chân sườn (Hv); tiết diện tính uốn bản cánh tại mép sườn dùng Hv")] public double Hv { get; set; }
    /// <summary>Chiều dày bản tại chân sườn dùng tính toán (≥ Hf).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double HRoot => Hv > Hf ? Hv : Hf;
    [Field("Bề rộng sườn (dầm móng)", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Bs { get; set; } = 0.35;
    [Field("Chiều cao sườn tổng (0 = tự chọn)", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Hs { get; set; }
    [Field("Bố trí cánh / bên giáp nhà hàng xóm", Group = "Bê tông lót, giáp nhà hàng xóm", Choices = "Cánh 2 bên|Cánh 1 bên – giáp nhà hàng xóm (hết đất)",
        Hint = "giáp nhà hàng xóm: bản đáy và bê tông lót không vươn sang đất nhà bên, sườn đặt sát ranh đất")] public string FlangeSide { get; set; } = "Cánh 2 bên";
    [Field("Bê tông lót vươn ra (phía không giáp nhà bên)", Unit = "mm", Scale = 1000, Group = "Bê tông lót, giáp nhà hàng xóm")] public double LeanOverhang { get; set; } = 0.10;
    [Field("Sức chịu tải nền R", Unit = "kN/m²", Group = "Nền")] public double R { get; set; } = 150;
    [Field("Hệ số nền k (0 = 120·R theo Bowles)", Unit = "kN/m³", Group = "Nền")] public double K { get; set; }
    [Field("Độ sâu chôn móng Df", Unit = "m", Group = "Nền")] public double Df { get; set; } = 1.5;
    [Field("γtb đất + móng", Unit = "kN/m³", Group = "Nền")] public double GammaTb { get; set; } = 20;
    [Field("Thép sườn dưới – số thanh (0 = tự chọn)", Group = "Thép")] public int BotN { get; set; }
    [Field("Thép sườn dưới – Ø", Unit = "mm", Group = "Thép")] public int BotD { get; set; } = 20;
    [Field("Thép sườn trên – số thanh (0 = tự chọn)", Group = "Thép")] public int TopN { get; set; }
    [Field("Thép sườn trên – Ø", Unit = "mm", Group = "Thép")] public int TopD { get; set; } = 20;
    [Field("Đai – Ø", Unit = "mm", Group = "Thép")] public int StirD { get; set; } = 8;
    [Field("Bước đai (0 = tự chọn Ø, bước, số nhánh)", Unit = "mm", Scale = 1000, Group = "Thép")] public double StirS { get; set; }
    [Field("Số nhánh đai", Group = "Thép")] public int Legs { get; set; } = 2;
    [Field("Thép ngang bản đáy – Ø (0 = tự chọn)", Unit = "mm", Group = "Thép")] public int FlangeD { get; set; }
    [Field("Thép ngang bản đáy – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double FlangeS { get; set; }
    public List<StripColumn> Columns { get; set; } = new()
    {
        new() { Name = "C1", X = 0.5, N = 650 }, new() { Name = "C2", X = 4.5, N = 1100 },
        new() { Name = "C3", X = 8.5, N = 1100 }, new() { Name = "C4", X = 12.5, N = 650 }
    };

    // kết quả phân tích (để vẽ, thuyết minh)
    [System.Text.Json.Serialization.JsonIgnore]
    public double[] Xs { get; private set; } = Array.Empty<double>();
    [System.Text.Json.Serialization.JsonIgnore]
    public double[] Moment { get; private set; } = Array.Empty<double>();
    [System.Text.Json.Serialization.JsonIgnore]
    public double[] Shear { get; private set; } = Array.Empty<double>();
    [System.Text.Json.Serialization.JsonIgnore]
    public double[] PressureTc { get; private set; } = Array.Empty<double>();
    [System.Text.Json.Serialization.JsonIgnore]
    public double BUsed { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double HUsed { get; private set; }

    public DetailResult Run(DetailSettings s)
    {
        var r = new DetailResult { Kind = "Móng băng", Name = Name, Quantity = Math.Max(1, Quantity) };
        var conc = DetailShim.TryConcrete(s.Concrete, r.Messages);
        var main = DetailShim.TryRebar(s.RebarMain, r.Messages);
        var small = DetailShim.TryRebar(s.RebarSmall, r.Messages);
        r.ConcreteClass = conc.Name;
        var cols = (Columns ?? new()).Where(cl => cl.N > 0 && cl.X >= 0 && cl.X <= Length).OrderBy(cl => cl.X).ToList();
        if (Length < 1 || cols.Count == 0 || R <= GammaTb * Df + 5)
        {
            r.Messages.Add("LỖI chiều dài băng ≥ 1 m, ≥ 1 cột có N > 0 nằm trong chiều dài băng, R > γtb·Df");
            return r;
        }
        double sumN = cols.Sum(x => x.N), sumNtc = cols.Sum(x => x.Ntc > 0 ? x.Ntc : x.N / 1.15);
        double b = B > 0 ? B : RcSection.CeilTo(1.15 * sumNtc / (Length * (R - GammaTb * Df)), 0.1);
        b = Math.Max(b, Bs + 0.2);
        double maxSpan = cols.Count > 1 ? cols.Zip(cols.Skip(1), (a, c2) => c2.X - a.X).Max() : Length / 2;
        double hs = Hs > 0 ? Hs : RcSection.CeilTo(Math.Max(maxSpan / 7, 0.5), 0.05);
        BUsed = b; HUsed = hs;
        double k = K > 0 ? K : 120 * R;
        // ---- dầm trên nền Winkler (tiết diện T ngược: cánh = bản đáy) ----
        var xs = new List<double>();
        int nseg = Math.Max(20, (int)Math.Ceiling(Length / 0.10));
        for (int i = 0; i <= nseg; i++) xs.Add(Length * i / nseg);
        foreach (var cl in cols) if (!xs.Any(x => Math.Abs(x - cl.X) < 1e-6)) xs.Add(cl.X);
        xs.Sort();
        double I = StripFootingDesigner.InertiaT(b, Hf, Bs, hs);
        double EI = conc.E * I * 0.85;
        var beam = new StripFootingDesigner.WinklerBeam(xs, EI, k, b);
        double[] Load(bool tc)
        {
            var fz = new double[xs.Count];
            foreach (var cl in cols) { int i = xs.FindIndex(x => Math.Abs(x - cl.X) < 1e-6); fz[i] -= tc ? (cl.Ntc > 0 ? cl.Ntc : cl.N / 1.15) : cl.N; }
            return fz;
        }
        double[] Mt(bool tc)
        {
            var mt = new double[xs.Count];
            foreach (var cl in cols) { int i = xs.FindIndex(x => Math.Abs(x - cl.X) < 1e-6); mt[i] += cl.M * (tc ? (cl.Ntc > 0 ? cl.Ntc / cl.N : 1 / 1.15) : 1); }
            return mt;
        }
        var solD = beam.Solve(Load(false), Mt(false));
        var solT = beam.Solve(Load(true), Mt(true));
        Xs = xs.ToArray(); Moment = solD.M; Shear = solD.V; PressureTc = solT.Pressure;
        double gD = GammaTb * Df;
        double pMax = solT.Pressure.Max() + gD, pTb = sumNtc / (b * Length) + gD, pMin = solT.Pressure.Min() + gD;
        double mPos = solD.M.Concat(solD.MLeft).Max(), mNeg = -solD.M.Concat(solD.MLeft).Min();
        double vMax = solD.V.Select(Math.Abs).Max();
        r.Calc.Add("## Móng băng " + Name + ": L = " + Dt.F(Length) + " m, B = " + Dt.F(b) + " m, sườn " + Dt.Mm(Bs) + "x" + Dt.Mm(hs) + ", bản mép " + Dt.Mm(Hf) + " mm; ΣN = " + Dt.F(sumN, "0") + " kN");
        r.Calc.Add("Dầm trên nền đàn hồi Winkler (phần tử dầm 0,1 m, lò xo k·B·Δx): k = " + Dt.F(k, "0") + " kN/m³" + (K > 0 ? "" : " (ks ≈ 40·FS·qa = 120·R – Bowles)")
                   + ", EI = 0,85·Eb·I = " + Dt.F(EI, "0") + " kN.m², I(T ngược) = " + Dt.F(I, "0.00000") + " m⁴");
        r.Calc.Add("Áp lực nền tiêu chuẩn: ptb = ΣNtc/(B·L) + γtb·Df = " + Dt.F(pTb, "0.0") + "; pmax = " + Dt.F(pMax, "0.0") + "; pmin = " + Dt.F(pMin, "0.0") + " kN/m²");
        r.Calc.Add("Nội lực tính toán sườn: M+ (căng dưới, tại cột) = " + Dt.F(mPos, "0.0") + " kN.m; M− (căng trên, giữa nhịp) = " + Dt.F(mNeg, "0.0") + " kN.m; Qmax = " + Dt.F(vMax, "0.0") + " kN");
        r.Checks.Add(Dt.Check("Áp lực trung bình ptb ≤ R", pTb, R, "kN/m²"));
        r.Checks.Add(Dt.Check("Áp lực lớn nhất pmax ≤ 1,2R", pMax, 1.2 * R, "kN/m²"));
        r.Checks.Add(Dt.Check("Không tách đáy móng (pmin ≥ 0): phần âm của pmin", Math.Max(-pMin, 0), 0, "kN/m²"));

        // ---- thép sườn ----
        double c = s.CoverFooting, cb = s.CoverBeam;
        double h0 = hs - c - BotD / 2000.0, h0t = hs - cb - TopD / 2000.0;
        double asBot = RcSection.DesignRect(mPos, Bs, h0, cb, conc, main, 0.10).As;
        double asTop = RcSection.DesignRect(mNeg, Bs, h0t, c, conc, main, 0.10).As;   // cánh nén ở dưới – lấy chữ nhật sườn, thiên về an toàn
        int nb = BotN > 0 ? BotN : Math.Max(2, (int)Math.Ceiling(asBot / Dt.Area(BotD) - 1e-9));
        int nt = TopN > 0 ? TopN : Math.Max(2, (int)Math.Ceiling(asTop / Dt.Area(TopD) - 1e-9));
        double muB = RcSection.MomentCapacity(Bs, h0, nb * Dt.Area(BotD), nt * Dt.Area(TopD), cb + TopD / 2000.0, conc, main);
        double muT = RcSection.MomentCapacity(Bs, h0t, nt * Dt.Area(TopD), nb * Dt.Area(BotD), c + BotD / 2000.0, conc, main);
        r.Checks.Add(Dt.Check("Sườn chịu M+ tại cột (thép dưới " + Dt.Bars(nb, BotD) + ")", mPos, muB, "kN.m"));
        r.Checks.Add(Dt.Check("Sườn chịu M− giữa nhịp (thép trên " + Dt.Bars(nt, TopD) + ")", mNeg, muT, "kN.m"));
        int sD = StirD > 0 ? StirD : 8, sL = Math.Max(Legs, 2);
        double sS = StirS;
        if (sS <= 0)
        {
            // tự chọn: Ø8 → Ø10 → Ø12, bước 200 → 100, 2 → 4 nhánh; bước ≤ min(h0/2; 300)
            double sMaxS = Math.Min(h0 / 2, 0.30);
            bool found = false;
            foreach (int legs in new[] { sL, 4 })
            {
                foreach (int dd in new[] { 8, 10, 12 })
                {
                    foreach (double ss in new[] { 0.20, 0.15, 0.10 })
                    {
                        if (ss > sMaxS + 1e-9) continue;
                        if (RcSection.ShearCapacity(Bs, h0, conc, small, dd, legs, ss) >= vMax) { sD = dd; sS = ss; sL = legs; found = true; break; }
                    }
                    if (found) break;
                }
                if (found) break;
            }
            if (!found) { sD = 12; sS = 0.10; sL = 4; }
        }
        double qu = RcSection.ShearCapacity(Bs, h0, conc, small, sD, sL, sS);
        r.Checks.Add(Dt.Check("Sườn chịu cắt Q ≤ Qb + Qsw (đai " + Dt.Spaced(sD, sS) + ", " + sL + " nhánh)", vMax, qu, "kN"));
        if (sL > 2) r.Messages.Add("Đai sườn " + sL + " nhánh: đai kín ngoài + đai kín trong (thống kê 2 bộ đai)");
        if (!Dt.FitsInLayer(Bs, cb, StirD, nb, BotD, false)) r.Messages.Add("Thép dưới sườn " + Dt.Bars(nb, BotD) + " không đủ khe hở 1 lớp – đặt 2 lớp hoặc tăng bề rộng sườn");
        // ---- bản cánh: console từ mép sườn, M = p·l²/2 ----
        bool oneSide = (FlangeSide ?? "").Contains("1 bên");
        double lc = oneSide ? b - Bs : (b - Bs) / 2, pn = sumN / (b * Length) * (pMax - gD) / Math.Max(pTb - gD, 1e-9);
        double mF = pn * lc * lc / 2, h0f = HRoot - c - 0.006;   // tiết diện tại mép sườn (đoạn vát: dày Hv)
        if (Hv > Hf) r.Calc.Add("Bản cánh có đoạn vát: dày " + Dt.Mm(Hf) + " mm ở mép, " + Dt.Mm(HRoot) + " mm ở chân sườn → h0 tại mép sườn = " + Dt.Mm(h0f) + " mm"
                                + (Math.Atan2(HRoot - Hf, Math.Max(lc, 1e-6)) * 180 / Math.PI > 30 ? " (độ dốc > 30°: cần ván khuôn mặt vát)" : " (độ dốc ≤ 30°: đổ không cần ván khuôn mặt vát)"));
        double asF = RcSection.DesignRect(mF, 1.0, h0f, c, conc, main, 0.10).As;
        var fl = FlangeD > 0 && FlangeS > 0 ? (FlangeD, FlangeS) : Dt.ChooseSpaced(asF, new[] { 10, 12, 14, 16 }, 0.20, 0.10);
        double muF = RcSection.MomentCapacity(1.0, h0f, Dt.AreaPerM(fl.Item1, fl.Item2), 0, c, conc, main);
        if (oneSide)
        {
            double et = (b - Bs) / 2;
            r.Calc.Add("Móng băng giáp nhà hàng xóm – hết đất (cánh 1 bên): tải cột lệch so với tâm bản đáy e = (B − bs)/2 = " + Dt.Mm(et) + " mm → mô men xoắn phân bố mt = q·e; cần dầm móng ngang (giằng) kéo về móng trong để cân bằng, hoặc tăng độ cứng sườn");
            r.Messages.Add("Móng băng giáp nhà hàng xóm: bố trí giằng móng ngang nối sang móng bên trong (cân bằng mô men lệch tâm ΣN·e = " + Dt.F(sumN * et / Math.Max(cols.Count, 1), "0.0") + " kN.m/cột)");
        }
        r.Calc.Add("Bản cánh console l = " + (oneSide ? "B − bs" : "(B − bs)/2") + " = " + Dt.F(lc) + " m, p = " + Dt.F(pn, "0.0") + " kN/m² → M = p·l²/2 = " + Dt.F(mF, "0.00") + " kN.m/m → " + Dt.Spaced(fl.Item1, fl.Item2));
        r.Checks.Add(Dt.Check("Bản cánh chịu uốn (thép ngang " + Dt.Spaced(fl.Item1, fl.Item2) + ")", mF, muF, "kN.m/m"));
        // cắt bản cánh tại mép sườn (không đặt cốt đai): Q ≤ 0,5·Rbt·b·h0·(2,5)… lấy Qb,min = 0,5·Rbt·b·h0 (mục 8.1.3.3)
        double qF = pn * Math.Max(lc - h0f, 0), qbF = 0.5 * conc.Rbt * 1.0 * h0f * 2.5 / 1.0;
        r.Checks.Add(Dt.Check("Bản cánh chịu cắt Q ≤ Qb (không cốt đai)", qF, Math.Min(qbF, 2.5 * conc.Rbt * h0f), "kN/m"));
        // ---- cột chọc thủng qua móng băng: đường bao cách mép cột h0/2, bị cắt bởi mép bản đáy / đầu băng (TCVN 5574:2018 mục 8.1.6.2) ----
        double pnetS = sumN / (b * Length);
        foreach (var cl in cols)
        {
            double bt = cl.BcT > 0 ? cl.BcT : Bs;
            var pc = Punching.StripColumn(cl.N, cl.M, Math.Max(cl.Bc, 0.1), bt, h0, b, cl.X, Length - cl.X, pnetS, conc.Rbt);
            r.Calc.Add("Cột " + cl.Name + " chọc thủng móng băng (h0 = " + Dt.Mm(h0) + " mm, đường bao cắt theo mép bản / đầu băng): " + pc.Text);
            r.Checks.Add(Dt.Check("Cột " + cl.Name + " chọc thủng móng băng F/Fb,u + M/Mb,u ≤ 1", pc.Ratio, 1, "", "TCVN 5574:2018 mục 8.1.6.2"));
        }
        foreach (var kx in r.Checks.Where(x => !x.Ok)) r.Messages.Add("LỖI " + kx.Name + ": " + Dt.F(kx.Demand, "0.0") + " > " + Dt.F(kx.Capacity, "0.0") + " " + kx.Unit);

        // ---- thép ----
        double legB = Dt.R10(hs - 2 * c), lenRib = Dt.R10(Length - 2 * c);
        r.Bars.Add(Dt.Hooked("Dưới", BotD, nb, lenRib, legB / 2, legB / 2));
        r.Bars.Add(Dt.Hooked("Trên", TopD, nt, lenRib, legB / 2, legB / 2, true));
        if (hs > 0.7)
        {
            int pairs = Math.Max((int)Math.Ceiling((hs - 2 * c) / 0.4) - 1, 1);
            r.Bars.Add(Dt.Straight("Giá", 12, 2 * pairs, lenRib));
        }
        double cs = Math.Max(cb - StirD / 1000.0, 0.015);
        r.Bars.Add(Dt.Stirrup("Đai", sD, ((int)Math.Floor(lenRib / sS) + 1) * (sL > 2 ? 2 : 1), Bs - 2 * cs, hs - c - cs, sS));
        double hk = Dt.R10(Math.Max(Hf - 2 * c, 0.10));
        r.Bars.Add(Dt.Hooked("Lớp dưới phương ngang", fl.Item1, (int)Math.Floor(lenRib / fl.Item2) + 1, Dt.R10(b - 2 * c), hk, hk, false, fl.Item2));
        var dist = Dt.ChooseSpaced(0.2 * Dt.AreaPerM(fl.Item1, fl.Item2), new[] { 10, 12 }, 0.25, 0.15);
        r.Bars.Add(Dt.Straight("Lớp dưới phương dọc", dist.D, (oneSide ? 1 : 2) * Dt.CountAcross(lc + 0.05, 0.05, dist.S), lenRib, false, dist.S));
        if (lenRib > s.StockLength) r.Messages.Add("Thép dọc dài " + Dt.F(lenRib) + " m > " + Dt.F(s.StockLength) + " m: nối thép dưới tại giữa nhịp, thép trên tại vị trí cột (ngoài vùng mô men lớn), so le 50 %");
        RebarNumbering.Renumber(r.Bars);
        double area = (b - Bs) * (Hf + HRoot) / 2 + Bs * hs;   // bản cánh hình thang (đoạn vát) + sườn
        r.ConcreteVolume = area * Length;
        bool steep = Math.Atan2(HRoot - Hf, Math.Max(lc, 1e-6)) * 180 / Math.PI > 30;
        double slopeLen = Math.Sqrt(lc * lc + (HRoot - Hf) * (HRoot - Hf));
        r.FormworkArea = ((oneSide ? 1 : 2) * Hf + (oneSide ? 1 : 2) * (hs - HRoot) + (oneSide ? hs : 0) + (steep ? (oneSide ? 1 : 2) * slopeLen : 0)) * Length + 2 * area;
        double ovs = Math.Max(LeanOverhang, 0);
        r.LeanVolume = (b + (oneSide ? 1 : 2) * ovs) * (Length + 2 * ovs) * s.LeanThickness;
        if (oneSide) r.Extra.Add(("Xây gạch / tấm khuôn để lại cho thành móng băng giáp nhà hàng xóm", "m²", hs * Length));
        r.Extra.Add(("Đào đất hố móng", "m³", (b + 0.6 + 0.5 * Df) * (Length + 0.6) * Df));
        return r;
    }

    /// <summary>Mặt đứng sườn móng băng (thép trên / dưới, vị trí cột, biểu đồ M sơ lược) và mặt cắt ngang chữ T ngược.</summary>
    public List<Figure> Draw(DetailResult r, DetailSettings s)
    {
        var figs = new List<Figure>();
        if (r == null || r.Bars.Count == 0 || BUsed <= 0) return figs;
        double S = 50;
        var w = new DxfWriter(S);
        double L = Length * 1000, hs = HUsed * 1000, hf = Hf * 1000, c = s.CoverFooting * 1000;
        w.Rect(Lay.BeTong, 0, 0, L, hs);
        w.Line(Lay.BeTong, 0, hf, L, hf, "DASHED");
        w.Rect(Lay.KyHieu, -100, -s.LeanThickness * 1000, L + 100, 0, "DASHED");
        foreach (var cl in Columns.Where(x => x.N > 0))
        {
            double x = cl.X * 1000, bc = cl.Bc * 1000;
            w.Rect(Lay.BeTong, x - bc / 2, hs, x + bc / 2, hs + 600);
            w.BreakH(Lay.BeTong, x - bc / 2 - 100, x + bc / 2 + 100, hs + 600);
            w.Text(Lay.Chu, x, hs + 750, w.Th, cl.Name, 0, 1, 0);
        }
        var bot = r.Bars.FirstOrDefault(b => b.Role == "Dưới"); var top = r.Bars.FirstOrDefault(b => b.Role == "Trên");
        if (bot != null)
        {
            double leg = bot.Segments.Count > 2 ? bot.Segments[0] : 0;
            w.Polyline(Lay.Thep, new List<(double, double)> { (c, c + leg), (c, c), (L - c, c), (L - c, c + leg) }, false);
            w.LeaderTag(L * 0.3, c, L * 0.3 + 2.4 * w.Th, -700, Dt.Bars(bot.Count, bot.Dia), bot.Mark);
        }
        if (top != null)
        {
            double leg = top.Segments.Count > 2 ? top.Segments[0] : 0;
            w.Polyline(Lay.Thep, new List<(double, double)> { (c, hs - c - leg), (c, hs - c), (L - c, hs - c), (L - c, hs - c - leg) }, false);
            w.LeaderTag(L * 0.6, hs - c, L * 0.6 + 2.4 * w.Th, hs + 8.8 * w.Th, Dt.Bars(top.Count, top.Dia), top.Mark);
        }
        var st = r.Bars.FirstOrDefault(b => b.Role == "Đai");
        if (st != null)
        {
            for (double x = 2 * c; x < L - c; x += Math.Max(st.Spacing * 1000, 100) * 4) w.Line(Lay.Dai, x, c, x, hs - c);
            w.LeaderTag(L * 0.45, hs / 2, L * 0.45 + 3.2 * w.Th, hs / 2 + 2.4 * w.Th, Dt.Spaced(st.Dia, st.Spacing), st.Mark);
        }
        var xsCol = new List<double> { 0 };
        xsCol.AddRange(Columns.Where(x => x.N > 0).Select(x => x.X * 1000).OrderBy(x => x));
        xsCol.Add(L);
        w.DimChainH(xsCol, 0, -3.0 * w.Th);
        w.Dim(0, 0, L, 0, -4.5 * w.Th);
        w.Dim(L, 0, L, hs, 2.0 * w.Th);
        w.Title(L / 2, -2500, RebarSchedule.MemberLabel("MẶT ĐỨNG MÓNG BĂNG " + Name, r.Quantity), DrawUtil.ScaleText(S));
        figs.Add(new Figure(w, Name, true));

        // mặt cắt ngang 1-1 (chữ T ngược)
        double Ss = 25;
        var x2 = new DxfWriter(Ss);
        double B = BUsed * 1000, bs = Bs * 1000, hr = HRoot * 1000;
        bool one = (FlangeSide ?? "").Contains("1 bên");
        double ovl = Math.Max(LeanOverhang, 0) * 1000;
        if (one)
        {
            // sườn sát ranh bên trái: bản đáy chỉ vươn sang phải
            x2.Polyline(Lay.BeTong, new List<(double, double)> { (-B / 2, 0), (B / 2, 0), (B / 2, hf), (-B / 2 + bs, hr), (-B / 2 + bs, hs), (-B / 2, hs) }, true);
            x2.Rect(Lay.KyHieu, -B / 2, -s.LeanThickness * 1000, B / 2 + ovl, 0);
            x2.Line(Lay.KyHieu, -B / 2, -s.LeanThickness * 1000 - 300, -B / 2, hs + 300, "CENTER");
            x2.Text(Lay.Chu, -B / 2 - 0.6 * x2.Th, -s.LeanThickness * 1000 - 300, 0.8 * x2.Th, "RANH ĐẤT", 90, 0, 2);
        }
        else
        {
            x2.Polyline(Lay.BeTong, new List<(double, double)> { (-B / 2, 0), (B / 2, 0), (B / 2, hf), (bs / 2, hr), (bs / 2, hs), (-bs / 2, hs), (-bs / 2, hr), (-B / 2, hf) }, true);
            x2.Rect(Lay.KyHieu, -B / 2 - ovl, -s.LeanThickness * 1000, B / 2 + ovl, 0);
        }
        double rx = one ? -B / 2 + bs / 2 : 0;   // tim sườn
        var fl = r.Bars.FirstOrDefault(b => b.Role == "Lớp dưới phương ngang");
        if (fl != null)
        {
            x2.Polyline(Lay.Thep, new List<(double, double)> { (-B / 2 + c, hf - c), (-B / 2 + c, c), (B / 2 - c, c), (B / 2 - c, hf - c) }, false);
            x2.LeaderTag(-B / 4, c, -B / 4 - 1.6 * x2.Th, -600, Dt.Spaced(fl.Dia, fl.Spacing), fl.Mark, -1);
        }
        if (st != null)
        {
            double cs = s.CoverBeam * 1000;
            x2.Rect(Lay.Dai, rx - bs / 2 + cs, c + 10, rx + bs / 2 - cs, hs - cs);
            x2.LeaderTag(rx + bs / 2 - cs, hs * 0.55, rx + bs / 2 + 3.2 * x2.Th, hs * 0.55 + 1.6 * x2.Th, Dt.Spaced(st.Dia, st.Spacing), st.Mark);
        }
        void Row(BarMark b, double y, bool up)
        {
            if (b == null) return;
            double cs = s.CoverBeam * 1000 + b.Dia / 2.0;
            int n = Math.Max(b.Count, 2);
            for (int i = 0; i < n; i++) x2.Dot(Lay.Thep, rx - bs / 2 + cs + i * (bs - 2 * cs) / (n - 1), y, b.Dia / 2.0);
            x2.LeaderTag(rx, y, rx + 2.4 * x2.Th, up ? hs + 4.0 * x2.Th : -900, Dt.Bars(b.Count, b.Dia), b.Mark);
        }
        Row(top, hs - s.CoverBeam * 1000 - (top?.Dia ?? 0) / 2.0, true);
        Row(bot, c + 20 + (bot?.Dia ?? 0) / 2.0, false);
        x2.Dim(-B / 2, 0, B / 2, 0, -3.0 * x2.Th);
        x2.Dim(rx - bs / 2, hs, rx + bs / 2, hs, 1.5 * x2.Th);
        x2.Dim(B / 2, 0, B / 2, hf, 1.5 * x2.Th);
        if (hr > hf + 1) x2.Dim(rx + (one ? bs / 2 : bs / 2), 0, rx + bs / 2, hr, -1.2 * x2.Th);
        x2.Dim(B / 2, 0, B / 2, hs, 2.5 * x2.Th);
        x2.Title(0, -1800, "1-1", DrawUtil.ScaleText(Ss), true);
        figs.Add(new Figure(x2, Name + " mặt cắt"));
        return figs;
    }
}

// ---------------------------------------------------------------------
// MÓNG CỌC (đài cọc dưới 1 cột)
// ---------------------------------------------------------------------
public class ManualPileCap : IDetailMember
{
    [Field("Tên đài", Group = "Chung")] public string Name { get; set; } = "Đ1";
    [Field("Số lượng", Group = "Chung")] public int Quantity { get; set; } = 1;
    [Field("Cột bc (phương X)", Unit = "mm", Scale = 1000, Group = "Cột")] public double ColX { get; set; } = 0.30;
    [Field("Cột hc (phương Y)", Unit = "mm", Scale = 1000, Group = "Cột")] public double ColY { get; set; } = 0.30;
    [Field("N tính toán", Unit = "kN", Group = "Tải chân cột")] public double N { get; set; } = 1200;
    [Field("Mx (quanh trục X)", Unit = "kN.m", Group = "Tải chân cột")] public double Mx { get; set; } = 30;
    [Field("My (quanh trục Y)", Unit = "kN.m", Group = "Tải chân cột")] public double My { get; set; } = 40;
    [Field("Cạnh / đường kính cọc D", Unit = "mm", Scale = 1000, Group = "Cọc")] public double PileD { get; set; } = 0.25;
    [Field("Cọc tròn (không = cọc vuông)", Group = "Cọc")] public bool Round { get; set; }
    [Field("Sức chịu tải thiết kế cọc chịu nén [P]", Unit = "kN", Group = "Cọc")] public double Pa { get; set; } = 400;
    [Field("Sức chịu tải nhổ [Pk] (0 = không cho nhổ)", Unit = "kN", Group = "Cọc")] public double Pt { get; set; }
    [Field("Ma sát thành Σ(fi·li) để tính [Pk] (0 = dùng [Pk] nhập)", Unit = "kN/m", Group = "Cọc chịu nhổ (TCVN 10304:2014)",
        Hint = "tổng fi·li các lớp đất dọc thân cọc; [Pk] = 0,8·u·Σfi·li/1,4 + 0,9·Gcọc")] public double SkinFriction { get; set; }
    [Field("Chiều dài cọc (trọng lượng cọc chống nhổ)", Unit = "m", Group = "Cọc chịu nhổ (TCVN 10304:2014)")] public double PileLength { get; set; } = 15;

    // ---- cọc khoan nhồi mini / sức chịu tải theo địa chất (TCVN 10304:2014) – Design/Foundation/BoredPile.cs ----
    public const string TypePrecast = "Cọc ép BTCT đúc sẵn", TypeMiniBored = "Cọc khoan nhồi mini (D300–600)", TypeBored = "Cọc khoan nhồi";
    [Field("Loại cọc", Group = "Cọc", Choices = TypePrecast + "|" + TypeMiniBored + "|" + TypeBored,
        Hint = "cọc khoan nhồi: tiết diện tròn; Pvl kể γcb·γ'cb = 0,85·0,7; thép dọc = râu thép đầu cọc (số thanh, Ø)")] public string PileType { get; set; } = TypePrecast;
    [Field("Tính [P] cọc theo địa chất (bảng lớp đất)", Group = "Cọc khoan nhồi – sức chịu tải theo đất nền (TCVN 10304:2014)",
        Hint = "bật: [P] = min(γ0·Rc,u/(γn·γk); Pvl) thay ô [P] nhập tay")] public bool CapacityFromSoil { get; set; }
    [Field("Phương pháp", Group = "Cọc khoan nhồi – sức chịu tải theo đất nền (TCVN 10304:2014)", Choices = BoredPile.MethodTable + "|" + BoredPile.MethodSpt)] public string CapacityMethod { get; set; } = BoredPile.MethodTable;
    [Field("Công nghệ khoan", Group = "Cọc khoan nhồi – sức chịu tải theo đất nền (TCVN 10304:2014)", Choices = BoredPile.DrillSlurry + "|" + BoredPile.DrillCasing + "|" + BoredPile.DrillDry)] public string DrillMethod { get; set; } = BoredPile.DrillSlurry;
    [Field("γcf (0 = theo công nghệ khoan)", Group = "Cọc khoan nhồi – sức chịu tải theo đất nền (TCVN 10304:2014)")] public double GammaCfUser { get; set; }
    [Field("γ0", Group = "Cọc khoan nhồi – sức chịu tải theo đất nền (TCVN 10304:2014)")] public double Gamma0 { get; set; } = 1.15;
    [Field("γn", Group = "Cọc khoan nhồi – sức chịu tải theo đất nền (TCVN 10304:2014)")] public double GammaN { get; set; } = 1.15;
    [Field("γk", Group = "Cọc khoan nhồi – sức chịu tải theo đất nền (TCVN 10304:2014)", Hint = "1,4 theo bảng tra; 1,75 khi ít hố khoan / không thử tải")] public double GammaK { get; set; } = 1.4;
    [Field("Bê tông cọc", Group = "Cọc khoan nhồi – sức chịu tải theo đất nền (TCVN 10304:2014)", Choices = "B20|B22.5|B25|B30|B35")] public string PileConcrete { get; set; } = "B25";
    [Field("Tải ngang tại chân cột H (0 = không kiểm tra)", Unit = "kN", Group = "Cọc khoan nhồi – tải ngang, lồng thép")] public double HLat { get; set; }
    [Field("Hệ số tỉ lệ K của nền (Phụ lục A)", Unit = "kN/m⁴", Group = "Cọc khoan nhồi – tải ngang, lồng thép")] public double KLat { get; set; } = 5000;
    [Field("Chiều dài lồng thép (0 = tự chọn)", Unit = "m", Group = "Cọc khoan nhồi – tải ngang, lồng thép")] public double CageLen { get; set; }
    [Field("Đai xoắn – Ø (0 = Ø6 khi D ≤ 400, Ø8)", Unit = "mm", Group = "Cọc khoan nhồi – tải ngang, lồng thép")] public int SpiralD { get; set; }
    [Field("Đai xoắn – bước thân cọc", Unit = "mm", Scale = 1000, Group = "Cọc khoan nhồi – tải ngang, lồng thép")] public double SpiralS { get; set; } = 0.20;
    [Field("Hệ số hao hụt bê tông cọc (mở rộng thành lỗ)", Group = "Cọc khoan nhồi – tải ngang, lồng thép")] public double Overcut { get; set; } = 1.15;
    [Field("Góc ma sát trung bình thân cọc φtb (lún khối quy ước)", Unit = "°", Group = "Cọc khoan nhồi – sức chịu tải theo đất nền (TCVN 10304:2014)")] public double PhiAvg { get; set; } = 20;
    [Field("Tự chọn chiều dài cọc (L nhỏ nhất để [P] ≥ ô [P] và mũi vào lớp tốt)", Group = "Cọc khoan nhồi – nâng cao",
        Hint = "bước 0,5 m; cần bật Tính [P] theo địa chất; ô [P] là sức chịu tải yêu cầu")] public bool AutoLength { get; set; }
    [Field("Kể hệ số nhóm cọc ma sát (Converse–Labarre)", Group = "Cọc khoan nhồi – nâng cao")] public bool GroupEfficiency { get; set; }
    /// <summary>Đường cong sức chịu tải Rc,u(L) (khi tính theo địa chất) – dùng vẽ biểu đồ.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public List<CapacityCurvePoint> CapacityCurve { get; private set; } = new();
    [System.Text.Json.Serialization.JsonIgnore] public NegativeFrictionResult NegFriction { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public StrutTieResult StrutTieResult { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public CageSpliceResult CageSplice { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public int SonicTubeCount => IsBored ? BoredPileDesign.SonicTubes(PileD) : 0;
    /// <summary>Bảng lớp đất (từ mặt đất tự nhiên) – dùng khi "Tính [P] theo địa chất".</summary>
    public List<PileSoilLayer> SoilLayers { get; set; } = new()
    {
        new() { Name = "Đất lấp", Thickness = 1.5, Kind = BoredPile.KindClay, IL = 0.8, Gamma = 17, E = 3000 },
        new() { Name = "Sét pha dẻo mềm", Thickness = 6, Kind = BoredPile.KindClay, IL = 0.6, NSpt = 5, Gamma = 18.5, E = 6000 },
        new() { Name = "Cát hạt vừa chặt vừa", Thickness = 8, Kind = BoredPile.KindSandCoarse, NSpt = 18, Gamma = 19, E = 20000 },
        new() { Name = "Sét nửa cứng", Thickness = 10, Kind = BoredPile.KindClay, IL = 0.2, NSpt = 22, Gamma = 19.5, E = 25000 },
    };
    [System.Text.Json.Serialization.JsonIgnore] public bool IsBored => (PileType ?? "").Contains("khoan nhồi");
    [System.Text.Json.Serialization.JsonIgnore] public bool RoundEff => Round || IsBored;
    [System.Text.Json.Serialization.JsonIgnore] public BoredPileResult BoredResult { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public PileLateralResult LateralResult { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public PileSettlementResult SettlementResult { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public double CageLenUsed { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore] public double PaUsed { get; private set; }
    [Field("Phản lực dầm móng / giằng cộng thêm vào N", Unit = "kN", Group = "Tải chân cột",
        Hint = "ΣR các giằng móng tựa lên đài – cộng vào lực dọc chân cột")] public double BeamReaction { get; set; }
    /// <summary>Phản lực giằng tự chuyển từ dầm móng (DetailingProject.RunAll); &gt; 0 thì dùng thay ô nhập tay.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double AutoBeamReaction { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string AutoBeamReactionText { get; set; } = "";
    [Field("Số cọc (0 = tự chọn)", Group = "Cọc")] public int NumPiles { get; set; }
    [Field("Khoảng cách tim cọc (0 = 3D)", Unit = "m", Group = "Cọc")] public double Spacing { get; set; }
    [Field("Tim cọc tới mép đài (0 = max(D; 0,7D + 0,15))", Unit = "m", Group = "Cọc")] public double Edge { get; set; }
    [Field("Đầu cọc ngàm vào đài", Unit = "mm", Scale = 1000, Group = "Cọc")] public double Embed { get; set; } = 0.10;
    [Field("Chiều cao đài H (0 = tự chọn)", Unit = "mm", Scale = 1000, Group = "Đài")] public double H { get; set; }
    [Field("Độ sâu đáy đài Df", Unit = "m", Group = "Đài")] public double Df { get; set; } = 1.5;
    [Field("γtb đất + đài", Unit = "kN/m³", Group = "Đài")] public double GammaTb { get; set; } = 20;
    [Field("Phương X – bên giáp nhà hàng xóm (hết đất)", Group = "Bê tông lót, giáp nhà hàng xóm", Choices = "Không|Giáp nhà hàng xóm bên trái (hết đất)|Giáp nhà hàng xóm bên phải (hết đất)",
        Hint = "đài và bê tông lót không vươn sang đất nhà bên; tim cọc cách ranh ≥ khoảng lùi ép cọc")] public string BoundaryX { get; set; } = "Không";
    [Field("Phương Y – bên giáp nhà hàng xóm (hết đất)", Group = "Bê tông lót, giáp nhà hàng xóm", Choices = "Không|Giáp nhà hàng xóm phía dưới (hết đất)|Giáp nhà hàng xóm phía trên (hết đất)")] public string BoundaryY { get; set; } = "Không";
    [Field("Tim cọc cách ranh đất tối thiểu (máy ép)", Unit = "mm", Scale = 1000, Group = "Bê tông lót, giáp nhà hàng xóm")] public double PileSetback { get; set; } = 0.50;
    [Field("Mép ngoài cột cách ranh đất", Unit = "mm", Scale = 1000, Group = "Bê tông lót, giáp nhà hàng xóm")] public double ColSetback { get; set; }
    [Field("Có dầm móng cân bằng mô men lệch tâm N·e", Group = "Bê tông lót, giáp nhà hàng xóm")] public bool BalanceBeam { get; set; } = true;
    [Field("Bê tông lót vươn ra (phía không giáp nhà bên)", Unit = "mm", Scale = 1000, Group = "Bê tông lót, giáp nhà hàng xóm")] public double LeanOverhang { get; set; } = 0.10;
    [Field("Thép phương X – Ø (0 = tự chọn)", Unit = "mm", Group = "Thép")] public int DiaX { get; set; }
    [Field("Thép phương X – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double SX { get; set; }
    [Field("Thép phương Y – Ø", Unit = "mm", Group = "Thép")] public int DiaY { get; set; }
    [Field("Thép phương Y – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double SY { get; set; }
    [Field("Bố trí thép đài", Group = "Thép", Choices = "Chỉ lớp dưới|Lớp dưới + lớp trên|Lồng thép (lớp dưới + lớp trên + thép đứng chống chọc thủng)",
        Hint = "Chỉ lớp dưới: đài cao ≥ 800 mm vẫn tự thêm lưới trên cấu tạo")] public string RebarLayout { get; set; } = "Chỉ lớp dưới";
    [Field("Lớp trên – Ø (0 = tự chọn)", Unit = "mm", Group = "Thép")] public int TopD { get; set; }
    [Field("Lớp trên – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double TopS { get; set; }
    [Field("Lồng thép: thép đứng – Ø (0 = Ø10)", Unit = "mm", Group = "Thép")] public int CageD { get; set; }
    [Field("Lồng thép: bước thép đứng (0 = 200)", Unit = "mm", Scale = 1000, Group = "Thép")] public double CageS { get; set; }
    [Field("Râu thép đầu cọc – số thanh mỗi cọc", Group = "Đầu cọc", Hint = "thép dọc cọc chừa lại sau khi đập đầu cọc, neo vào đài")] public int PileBarN { get; set; } = 4;
    [Field("Râu thép đầu cọc – Ø", Unit = "mm", Group = "Đầu cọc")] public int PileBarD { get; set; } = 16;
    [Field("Râu thép đầu cọc – chiều dài neo vào đài (0 = max(30d; 400))", Unit = "mm", Scale = 1000, Group = "Đầu cọc")] public double AnchorLen { get; set; }
    /// <summary>Chiều dài neo râu thép đầu cọc dùng tính toán (m).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public double AnchorUsed => AnchorLen > 0 ? AnchorLen : Math.Max(30 * Math.Max(PileBarD, 10) / 1000.0, 0.40);
    [System.Text.Json.Serialization.JsonIgnore] public bool HasTop => (RebarLayout ?? "").Contains("lớp trên") || (RebarLayout ?? "").StartsWith("Lồng");
    [System.Text.Json.Serialization.JsonIgnore] public bool HasCage => (RebarLayout ?? "").StartsWith("Lồng");

    /// <summary>Khả năng chọc thủng có cốt thép ngang (TCVN 5574:2018 mục 8.1.6.3.3): Fsw = 0,8·qsw·u, qsw = Rsw·Asw/sw;
    /// chỉ kể khi Fsw ≥ 0,25·Fb; Fb + Fsw ≤ 2·Fb. Trả về (Fsw dùng, khả năng tổng).</summary>
    public static (double Fsw, double Fult) PunchWithLinks(double fb, double rsw, double aswPerRow, int rows, double sw, double u)
    {
        if (sw <= 1e-9 || rows <= 0) return (0, fb);
        double qsw = rsw * aswPerRow * rows / sw;
        double fsw = 0.8 * qsw * u;
        if (fsw < 0.25 * fb) return (0, fb);
        return (Math.Min(fsw, fb), fb + Math.Min(fsw, fb));
    }

    // kết quả để vẽ
    [System.Text.Json.Serialization.JsonIgnore]
    public List<(double X, double Y)> Piles { get; private set; } = new();
    [System.Text.Json.Serialization.JsonIgnore]
    public double[] PileForces { get; private set; } = Array.Empty<double>();
    [System.Text.Json.Serialization.JsonIgnore]
    public double CapL { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double CapB { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double CapH { get; private set; }
    /// <summary>Tâm đài so với tim cột (m) – khác 0 khi đài sát ranh.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double CapCx { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double CapCy { get; private set; }

    /// <summary>Kích thước đài theo nhóm cọc (toạ độ quanh trọng tâm nhóm), có xét sát ranh: mép đài trùng ranh, tim cọc gần ranh cách ranh ≥ max(edge; PileSetback).
    /// Trả về L, B, toạ độ tim cột (xc, yc) và tâm đài (cx, cy) so với trọng tâm nhóm cọc.</summary>
    public void Geometry(List<(double X, double Y)> piles, double edge, out double L, out double B, out double xc, out double yc, out double cx, out double cy)
    {
        double x0 = piles.Min(p => p.X), x1 = piles.Max(p => p.X), y0 = piles.Min(p => p.Y), y1 = piles.Max(p => p.Y);
        bool left = (BoundaryX ?? "").Contains("trái"), right = (BoundaryX ?? "").Contains("phải");
        bool down = (BoundaryY ?? "").Contains("dưới"), up = (BoundaryY ?? "").Contains("trên");
        double eB = Math.Max(edge, PileSetback);
        double cx0 = x0 - (left ? eB : edge), cx1 = x1 + (right ? eB : edge);
        double cy0 = y0 - (down ? eB : edge), cy1 = y1 + (up ? eB : edge);
        double cs = Math.Max(ColSetback, 0);
        xc = left ? cx0 + cs + ColX / 2 : right ? cx1 - cs - ColX / 2 : 0;
        yc = down ? cy0 + cs + ColY / 2 : up ? cy1 - cs - ColY / 2 : 0;
        // đài phải bao cột (+150 mm mỗi phía không sát ranh)
        cx0 = Math.Min(cx0, xc - ColX / 2 - (left ? cs : 0.15)); cx1 = Math.Max(cx1, xc + ColX / 2 + (right ? cs : 0.15));
        cy0 = Math.Min(cy0, yc - ColY / 2 - (down ? cs : 0.15)); cy1 = Math.Max(cy1, yc + ColY / 2 + (up ? cs : 0.15));
        L = cx1 - cx0; B = cy1 - cy0; cx = (cx0 + cx1) / 2; cy = (cy0 + cy1) / 2;
    }

    /// <summary>Lực cọc: Pi = (N + G)/n + Mx·yi/Σy² + My·xi/Σx² (TCVN 10304:2014, đài cứng).</summary>
    public static double[] Forces(double n, double mx, double my, double g, IList<(double X, double Y)> piles)
    {
        double sx = piles.Sum(p => p.X * p.X), sy = piles.Sum(p => p.Y * p.Y);
        return piles.Select(p => (n + g) / piles.Count + (sy > 1e-9 ? mx * p.Y / sy : 0) + (sx > 1e-9 ? my * p.X / sx : 0)).ToArray();
    }

    public DetailResult Run(DetailSettings s)
    {
        var r = new DetailResult { Kind = "Móng cọc", Name = Name, Quantity = Math.Max(1, Quantity) };
        var conc = DetailShim.TryConcrete(s.Concrete, r.Messages);
        var main = DetailShim.TryRebar(s.RebarMain, r.Messages);
        r.ConcreteClass = conc.Name;
        // lực dọc kể phản lực giằng / dầm móng tựa lên đài (che thuộc tính N trong phạm vi hàm)
        double N = this.N + TieReaction.Use(AutoBeamReaction, BeamReaction);
        TieReaction.Report(r, this.N, N, AutoBeamReaction, AutoBeamReactionText, BeamReaction);
        // sức kháng nhổ cho phép: theo ma sát thành (TCVN 10304:2014) nếu nhập Σfi·li, ngược lại [Pk] người dùng
        double perim = RoundEff ? Math.PI * PileD : 4 * PileD, areaP = RoundEff ? Math.PI * PileD * PileD / 4 : PileD * PileD;
        // ---- cọc khoan nhồi: sức chịu tải theo đất nền + vật liệu (TCVN 10304:2014) ----
        double Pa = this.Pa;
        BoredResult = null; LateralResult = null; SettlementResult = null; CageLenUsed = 0;
        CapacityCurve = new(); NegFriction = null; StrutTieResult = null; CageSplice = null;
        double skinF = SkinFriction;
        Material concP = conc;
        if (IsBored)
        {
            concP = DetailShim.TryConcrete(string.IsNullOrWhiteSpace(PileConcrete) ? "B25" : PileConcrete, r.Messages);
            double asP = Math.Max(PileBarN, 0) * Dt.Area(Math.Max(PileBarD, 10));
            double pvl = BoredPile.MaterialCapacity(PileD, concP.Rb, main.Rsc, asP, DrillMethod != BoredPile.DrillDry);
            r.Calc.Add("## Cọc khoan nhồi D" + Dt.Mm(PileD) + " (" + PileType + "), bê tông " + concP.Name + ", thép dọc " + Dt.Bars(Math.Max(PileBarN, 0), Math.Max(PileBarD, 10)));
            r.Calc.Add("Sức chịu tải theo vật liệu: Pvl = φ·(γcb·γ'cb·Rb·Ab + Rsc·As) = 1·(" + Dt.F(BoredPile.GammaCb, "0.00") + "·" + Dt.F(DrillMethod != BoredPile.DrillDry ? BoredPile.GammaCbWet : 1, "0.0") + "·"
                       + Dt.F(concP.Rb, "0") + "·" + Dt.F(areaP, "0.0000") + " + " + Dt.F(main.Rsc, "0") + "·" + Dt.F(asP, "0.000000") + ") = " + Dt.F(pvl, "0.0") + " kN (TCVN 10304:2014 mục 7.1.9; φ = 1 cọc ngàm hoàn toàn trong đất)");
            if (CapacityFromSoil && SoilLayers != null && SoilLayers.Count > 0)
            {
                if (AutoLength)
                {
                    // tự chọn chiều dài: L nhỏ nhất (bước 0,5 m) để [P] ≥ ô [P] (yêu cầu) và mũi trong lớp tốt
                    var al = BoredPileDesign.AutoLength(SoilLayers, PileD, Df, this.Pa, pvl, CapacityMethod, DrillMethod, GammaCfUser, Gamma0, GammaN, GammaK);
                    if (al.Length > 0) PileLength = al.Length;
                    r.Calc.Add(al.Text);
                    if (!al.Ok) r.Messages.Add(al.Text);
                    CapacityCurve = al.Curve;
                }
                var bp = BoredPile.Capacity(SoilLayers, PileD, Df, PileLength, CapacityMethod, DrillMethod, GammaCfUser, Gamma0, GammaN, GammaK);
                bp.As = asP; bp.Pvl = pvl;
                BoredResult = bp;
                r.Calc.AddRange(bp.Calc);
                foreach (var nt in bp.Notes) r.Messages.Add(nt);
                double psoil = bp.Psoil;
                var nf = BoredPileDesign.NegativeFriction(bp, SoilLayers);
                if (nf != null) { NegFriction = nf; psoil = nf.PsoilNet; r.Calc.Add(nf.Text); }
                if (CapacityCurve.Count == 0)
                    CapacityCurve = BoredPileDesign.Curve(SoilLayers, PileD, Df, PileD < 0.8 ? 6 : 10, Math.Max(BoredPileDesign.TotalDepth(SoilLayers) - Df - 0.5, PileD < 0.8 ? 6 : 10), 1.0,
                        CapacityMethod, DrillMethod, pvl, GammaCfUser, Gamma0, GammaN, GammaK);
                Pa = Math.Floor(Math.Min(psoil, pvl));
                r.Calc.Add("[P] = min([P]đ" + (nf != null ? ",net" : "") + "; Pvl) = min(" + Dt.F(psoil, "0.0") + "; " + Dt.F(pvl, "0.0") + ") = " + Dt.F(Pa, "0") + " kN");
                if (skinF <= 0) skinF = bp.SumFiLi;
            }
            else
            {
                r.Checks.Add(Dt.Check("[P] nhập tay ≤ Pvl (sức chịu tải theo vật liệu cọc)", Pa, pvl, "kN", "TCVN 10304:2014 mục 7.1.9"));
            }
        }
        PaUsed = Pa;
        double Pt = skinF > 0 ? PileGroup.TensionCapacity(perim, skinF, areaP, PileLength).Pk : this.Pt;
        if (N <= 0 || Pa <= 0 || PileD <= 0.1)
        {
            r.Messages.Add("LỖI cần N > 0, [P] > 0, D cọc ≥ 100 mm");
            return r;
        }
        // cọc khoan nhồi D ≥ 0,8 m: tim cọc ≥ max(2,5D; D + 1 m), mép cọc – mép đài ≥ max(250; D/3) (BoredPileDesign)
        bool large = IsBored && PileD >= 0.8 - 1e-9;
        double sp = Spacing > 0 ? Spacing : large ? Math.Ceiling(BoredPileDesign.MinSpacing(PileD) * 20 - 1e-9) / 20 : 3 * PileD;
        double edge = Edge > 0 ? Edge : large ? PileD / 2 + BoredPileDesign.MinEdgeClear(PileD) : Math.Max(PileD, 0.7 * PileD + 0.15);
        bool longX = Math.Abs(My) >= Math.Abs(Mx);
        int n = NumPiles > 0 ? NumPiles : Math.Max(1, (int)Math.Ceiling(1.15 * N / Pa));
        double[] P = Array.Empty<double>(); List<(double X, double Y)> piles = new(); double L = 0, B = 0, G = 0, xc = 0, yc = 0, capCx = 0, capCy = 0;
        for (int it = 0; it < 30; it++)
        {
            piles = PileCapDesigner.Layout(n, sp, longX);
            Geometry(piles, edge, out L, out B, out xc, out yc, out capCx, out capCy);
            G = 1.1 * GammaTb * L * B * Df;
            // cột lệch so với trọng tâm nhóm cọc (sát ranh): kể N·e vào lực cọc nếu không có dầm cân bằng
            P = Forces(N, Mx + (BalanceBeam ? 0 : N * yc), My + (BalanceBeam ? 0 : N * xc), G, piles);
            if (NumPiles > 0 || (P.Max() <= Pa && P.Min() >= -Pt) || n >= 25) break;
            n++;
        }
        bool bX = Dt.IsNeighbour(BoundaryX), bY = Dt.IsNeighbour(BoundaryY);
        if (bX || bY)
        {
            double me = N * Math.Sqrt(xc * xc + yc * yc);
            r.Calc.Add("Đài giáp nhà hàng xóm (hết đất): đài và bê tông lót không vươn sang đất nhà bên; tim cọc cách ranh ≥ " + Dt.Mm(PileSetback) + " mm (vị trí máy ép); tim cột lệch trọng tâm nhóm cọc ex = "
                       + Dt.Mm(Math.Abs(xc)) + ", ey = " + Dt.Mm(Math.Abs(yc)) + " mm → M = N·e = " + Dt.F(me, "0.0") + " kN.m" + (BalanceBeam ? " do dầm móng cân bằng chịu" : " – kể vào lực cọc"));
            if (BalanceBeam && me > 1e-6) r.Messages.Add("Dầm móng cân bằng nối đài " + Name + " với đài bên trong phải chịu M = N·e = " + Dt.F(me, "0.0") + " kN.m");
        }
        // toạ độ cọc tính từ tim cột (để kiểm tra chọc thủng, cắt, mô men mép cột và vẽ)
        piles = piles.Select(pp => (pp.X - xc, pp.Y - yc)).ToList();
        CapCx = capCx - xc; CapCy = capCy - yc;
        Piles = piles; PileForces = P; CapL = L; CapB = B;
        r.Calc.Add("## Đài cọc " + Name + ": " + n + " cọc " + (RoundEff ? "D" : "") + Dt.Mm(PileD) + (RoundEff ? "" : "x" + Dt.Mm(PileD)) + ", khoảng cách " + Dt.F(sp) + " m, đài "
                   + Dt.F(L) + " × " + Dt.F(B) + " m");
        r.Calc.Add("Pi = (N + G)/n ± Mx·yi/Σyi² ± My·xi/Σxi², G = 1,1·γtb·Lđ·Bđ·Df = " + Dt.F(G, "0.0") + " kN (TCVN 10304:2014)");
        r.Calc.Add("Pmax = " + Dt.F(P.Max(), "0.0") + " kN; Pmin = " + Dt.F(P.Min(), "0.0") + " kN; [P] = " + Dt.F(Pa, "0") + " kN");
        r.Checks.Add(Dt.Check("Lực lớn nhất lên cọc Pmax ≤ [P]", P.Max(), Pa, "kN"));
        r.Checks.Add(Dt.Check("Cọc chịu nhổ: lực nhổ max(−Pmin; 0) ≤ [Pk]", Math.Max(-P.Min(), 0), Pt, "kN"));
        if (P.Min() < -1e-9)
        {
            // cọc chịu nhổ: sức kháng đất, thép cọc chịu kéo, neo râu thép vào đài (TCVN 10304:2014; TCVN 5574:2018 mục 10.3.5)
            var up = PileGroup.Uplift(P.Min(), this.Pt, perim, skinF, areaP, PileLength, Math.Max(PileBarN, 0), Math.Max(PileBarD, 10), main.Rs, conc.Rbt, AnchorUsed);
            r.Calc.Add("## Cọc chịu nhổ: Nt = " + Dt.F(up.Nt, "0.0") + " kN");
            r.Calc.AddRange(up.Calc);
            if (PileBarN > 0)
            {
                r.Checks.Add(Dt.Check("Cọc chịu nhổ: thép cọc chịu kéo Nt ≤ Rs·As", up.Nt, up.SteelCap, "kN", "TCVN 5574:2018"));
                r.Checks.Add(Dt.Check("Cọc chịu nhổ: neo râu thép vào đài La ≥ lan (chịu kéo)", up.Lan, AnchorUsed, "m", "TCVN 5574:2018 mục 10.3.5"));
            }
            r.Messages.AddRange(up.Notes);
            // nhổ khối nhóm cọc (≥ 2 cọc, có Σfi·li): khối đất bao nhóm cọc + đài cùng bị nhổ lên
            if (piles.Count >= 2 && skinF > 0)
            {
                double sumT = P.Where(v => v < 0).Sum(v => -v);
                double blL = piles.Max(q => q.X) - piles.Min(q => q.X) + PileD, blB = piles.Max(q => q.Y) - piles.Min(q => q.Y) + PileD;
                var blk = BoredPile.BlockUplift(blL, blB, PileLength, 18, skinF, G / 1.1);
                r.Calc.Add("Nhổ khối nhóm cọc: khối " + Dt.F(blL) + " × " + Dt.F(blB) + " × " + Dt.F(PileLength) + " m, W đất = " + Dt.F(blk.W, "0.0") + " kN (γ = 18 kN/m³), Gđài = " + Dt.F(G / 1.1, "0.0")
                           + " kN; [Pk,khối] = 0,9·(W + Gđài) + 0,8·u_khối·Σfi·li/γk = " + Dt.F(blk.Cap, "0.0") + " kN ≥ ΣNt = " + Dt.F(sumT, "0.0") + " kN");
                r.Checks.Add(Dt.Check("Nhổ khối nhóm cọc ΣNt ≤ [Pk,khối]", sumT, blk.Cap, "kN", "TCVN 10304:2014"));
            }
        }
        if (IsBored && PileD >= 0.8 - 1e-9)
            r.Checks.Add(Dt.Check("Khoảng cách tim cọc khoan nhồi D ≥ 800 ≥ max(2,5D; D + 1 m) (TCVN 10304:2014 mục 8.13)", BoredPileDesign.MinSpacing(PileD), sp, "m"));
        else r.Checks.Add(Dt.Check("Khoảng cách tim cọc ≥ 3D (TCVN 10304:2014 mục 8.13)", 3 * PileD, sp, "m"));

        // ---- chiều cao đài: chọc thủng cột ----
        double c = Math.Max(s.CoverFooting, 0.05) + Embed;
        (double F, double Fu) Punch(double h0)
        {
            double ax = ColX / 2 + h0, ay = ColY / 2 + h0;
            double inside = 0;
            for (int i = 0; i < piles.Count; i++) if (Math.Abs(piles[i].X) <= ax && Math.Abs(piles[i].Y) <= ay) inside += P[i];
            double u = 2 * (ColX + ColY + 2 * h0);
            return (N - inside * N / Math.Max(N + G, 1e-9), conc.Rbt * u * h0);
        }
        // chọc thủng có kể mô men: mô men không cân bằng của phản lực các cọc ngoài tháp 45° (TCVN 5574:2018 mục 8.1.6.2)
        PunchingResult PunchM(double h0m)
        {
            double axP = ColX / 2 + h0m, ayP = ColY / 2 + h0m, kN = N / Math.Max(N + G, 1e-9), mOutY = 0, mOutX = 0;
            for (int i = 0; i < piles.Count; i++)
                if (!(Math.Abs(piles[i].X) <= axP && Math.Abs(piles[i].Y) <= ayP)) { mOutY += P[i] * kN * piles[i].X; mOutX += P[i] * kN * piles[i].Y; }
            var pm = Punch(h0m);
            return Punching.Check(PunchingContour.Rect(ColX, ColY, h0m), pm.F, mOutY, mOutX, h0m, conc.Rbt, 0.5, pm.Fu);
        }
        double hh = H;
        if (hh <= 0)
        {
            hh = Math.Max(0.5, 2 * PileD);
            while (hh < 2.5 && (Punch(hh - c).F > Punch(hh - c).Fu || (!HasCage && !PunchM(hh - c).Ok))) hh = Math.Round(hh + 0.05, 2);
        }
        CapH = hh;
        double h0x = hh - c;
        var (fP, fuP) = Punch(h0x);
        r.Calc.Add("Chọc thủng cột – đài: h0 = H − a = " + Dt.Mm(h0x) + " mm; F = N − Σ(cọc trong tháp chọc thủng) = " + Dt.F(fP, "0.0") + " kN ≤ Rbt·u·h0 = " + Dt.F(fuP, "0.0") + " kN");
        int cageD = CageD > 0 ? CageD : 10; double cageS = CageS > 0 ? CageS : 0.20;
        int cageRows = Math.Max(2, (int)Math.Floor(h0x / Math.Max(cageS, 0.05) + 1e-9));
        double uP = 2 * (ColX + ColY + 2 * h0x);
        if (HasCage)
        {
            var smallSt = DetailShim.TryRebar(s.RebarSmall, r.Messages);
            var (fsw, fult) = PunchWithLinks(fuP, (cageD <= 10 ? smallSt : main).Rsw, Dt.Area(cageD), cageRows, cageS, uP);
            r.Calc.Add("Lồng thép chống chọc thủng: thép đứng Ø" + cageD + " bước " + Dt.Mm(cageS) + " mm, " + cageRows + " hàng trong vùng h0 quanh cột; qsw = Rsw·Asw/sw, Fsw = 0,8·qsw·u = "
                       + Dt.F(fsw, "0.0") + " kN (kể khi ≥ 0,25Fb; Fb + Fsw ≤ 2Fb – TCVN 5574:2018 mục 8.1.6.3.3)");
            r.Checks.Add(Dt.Check("Chọc thủng cột – đài có lồng thép F ≤ Fb + Fsw", fP, fult, "kN", "TCVN 5574:2018 mục 8.1.6.3.3"));
        }
        else r.Checks.Add(Dt.Check("Chọc thủng cột – đài F ≤ Rbt·u·h0", fP, fuP, "kN", "TCVN 5574:2018 mục 8.1.6"));
        if (!HasCage)
        {
            var prc = PunchM(h0x);
            r.Calc.Add("Chọc thủng cột – đài có kể mô men (M = Σ Pi·xi các cọc ngoài tháp 45°, lấy 0,5M): " + prc.Text);
            if (prc.F > 1e-6) r.Checks.Add(Dt.Check("Chọc thủng cột – đài có kể mô men F/Fb,u + ΣM/Mb,u ≤ 1", prc.Ratio, 1, "", "TCVN 5574:2018 mục 8.1.6.2"));
        }
        double dAnc = Math.Max(PileBarD, 10) / 1000.0;
        r.Calc.Add("Đầu cọc ngàm vào đài " + Dt.Mm(Embed) + " mm; đập bỏ bê tông đầu cọc, chừa râu thép " + Math.Max(PileBarN, 0) + "Ø" + PileBarD + " neo vào đài La = " + Dt.Mm(AnchorUsed) + " mm (≥ 30d = " + Dt.Mm(30 * dAnc) + " mm)");
        if (PileBarN > 0) r.Checks.Add(Dt.Check("Râu thép đầu cọc neo vào đài La ≥ 30d", 30 * dAnc, AnchorUsed, "m"));
        // chọc thủng cọc góc: P ≤ Rbt·h0·(u góc) với u = 2·(D/2 + edge) gần đúng (2 mặt về phía mép đài)
        int iMax = Array.IndexOf(P, P.Max());
        double uCorner = 2 * (edge + PileD / 2 + h0x / 2);
        r.Checks.Add(Dt.Check("Chọc thủng cọc góc (cọc chịu lực lớn nhất)", P.Max() * N / Math.Max(N + G, 1e-9), conc.Rbt * uCorner * h0x / 2 * 2, "kN"));
        // cắt trên tiết diện nghiêng mép cột: Q = Σ Pi phía ngoài; Qb = 1,5·Rbt·b·h0²/c ≤ 2,5·Rbt·b·h0
        double ShearCheck(bool alongX, out double q, out double cc)
        {
            double face = (alongX ? ColX : ColY) / 2;
            q = 0; cc = double.MaxValue;
            for (int i = 0; i < piles.Count; i++)
            {
                double d = alongX ? piles[i].X : piles[i].Y;
                if (d - PileD / 2 > face) { q += P[i] * N / Math.Max(N + G, 1e-9); cc = Math.Min(cc, d - PileD / 2 - face); }
            }
            double negQ = 0;
            for (int i = 0; i < piles.Count; i++)
            {
                double d = -(alongX ? piles[i].X : piles[i].Y);
                if (d - PileD / 2 > face) negQ += P[i] * N / Math.Max(N + G, 1e-9);
            }
            q = Math.Max(q, negQ);
            double bw = alongX ? B : L;
            if (cc == double.MaxValue) cc = h0x;
            cc = Math.Clamp(cc, 0.3 * h0x, 3 * h0x);   // c ≥ 0,3h0 khi cọc sát mép cột (mục 8.1.3.3 – chặn trên 2,5Rbt·b·h0)
            return Math.Min(1.5 * conc.Rbt * bw * h0x * h0x / cc, 2.5 * conc.Rbt * bw * h0x);
        }
        double qbX = ShearCheck(true, out double qX, out _), qbY = ShearCheck(false, out double qY, out _);
        r.Checks.Add(Dt.Check("Cắt tiết diện nghiêng mép cột phương X", qX, qbX, "kN"));
        r.Checks.Add(Dt.Check("Cắt tiết diện nghiêng mép cột phương Y", qY, qbY, "kN"));
        // ---- mô men mép cột ----
        double Mface(bool alongX)
        {
            double face = (alongX ? ColX : ColY) / 2, m1 = 0, m2 = 0;
            for (int i = 0; i < piles.Count; i++)
            {
                double d = alongX ? piles[i].X : piles[i].Y;
                double pi = P[i] * N / Math.Max(N + G, 1e-9);
                if (d > face) m1 += pi * (d - face);
                if (-d > face) m2 += pi * (-d - face);
            }
            return Math.Max(m1, m2);
        }
        double mX = Mface(true), mY = Mface(false);
        double asX = RcSection.DesignRect(mX, B, h0x, c, conc, main, 0.05).As, asY = RcSection.DesignRect(mY, L, h0x - 0.016, c, conc, main, 0.05).As;
        var bx = DiaX > 0 && SX > 0 ? (DiaX, SX) : Dt.ChooseSpaced(asX / B, ManualFooting.Dias.Concat(new[] { 22, 25 }).ToArray(), 0.20, 0.10);
        var by = DiaY > 0 && SY > 0 ? (DiaY, SY) : Dt.ChooseSpaced(asY / L, ManualFooting.Dias.Concat(new[] { 22, 25 }).ToArray(), 0.20, 0.10);
        r.Calc.Add("Mô men mép cột: MX = Σ Pi·(xi − bc/2) = " + Dt.F(mX, "0.0") + " kN.m → As = " + Dt.F(asX * 1e4, "0.00") + " cm² → " + Dt.Spaced(bx.Item1, bx.Item2)
                   + "; MY = " + Dt.F(mY, "0.0") + " kN.m → As = " + Dt.F(asY * 1e4, "0.00") + " cm² → " + Dt.Spaced(by.Item1, by.Item2));
        double muX = RcSection.MomentCapacity(B, h0x, Dt.AreaPerM(bx.Item1, bx.Item2) * B, 0, c, conc, main);
        double muY = RcSection.MomentCapacity(L, h0x - 0.016, Dt.AreaPerM(by.Item1, by.Item2) * L, 0, c, conc, main);
        r.Checks.Add(Dt.Check("Chịu uốn phương X " + Dt.Spaced(bx.Item1, bx.Item2), mX, muX, "kN.m"));
        r.Checks.Add(Dt.Check("Chịu uốn phương Y " + Dt.Spaced(by.Item1, by.Item2), mY, muY, "kN.m"));
        if (n == 1) r.Messages.Add("Đài 1 cọc: thép đài cấu tạo, cọc đặt đúng tim cột; cần giằng móng 2 phương để chịu mô men");
        if (n == 1 && IsBored)
        {
            var mn = BoredPileDesign.SinglePileCapMin(PileD, Math.Max(ColX, ColY), AnchorUsed, Embed);
            r.Calc.Add("Đài đơn cọc: cạnh ≥ max(D + 2·mép; cạnh cột + 200) = " + Dt.Mm(mn.Side) + " mm; H ≥ max(600; La + ngàm + 100) = " + Dt.Mm(mn.H) + " mm");
            r.Checks.Add(Dt.Check("Đài đơn cọc: cạnh nhỏ nhất ≥ cạnh tối thiểu", mn.Side, Math.Min(L, B), "m"));
            r.Checks.Add(Dt.Check("Đài đơn cọc: chiều cao ≥ chiều cao tối thiểu", mn.H, hh, "m"));
        }
        if (IsBored && n >= 2 && n <= 4)
        {
            // đài sâu / cọc lớn: mô hình thanh chống – giằng (Blévot)
            double rr = piles.Average(q => Math.Sqrt(q.X * q.X + q.Y * q.Y)), cEq = Math.Sqrt(Math.Max(ColX * ColY, 1e-6));
            bool deep = Math.Max(rr - cEq / 2, 0) / Math.Max(h0x, 1e-6) <= 1.0 + 1e-9;
            if (PileD >= 0.8 - 1e-9 || deep)
            {
                double apl = Math.PI * PileD * PileD / 4;
                var stt = BoredPileDesign.StrutTie(N, n, rr, cEq, h0x, ColX * ColY, apl, conc.Rb, main.Rs, conc.Rbn / 1000);
                StrutTieResult = stt;
                r.Calc.AddRange(stt.Calc);
                double band = Math.Min(Math.Min(L, B), PileD + h0x);
                bool tieX = Math.Abs(My) >= Math.Abs(Mx) || n != 2;
                double asBand = Dt.AreaPerM(tieX ? bx.Item1 : by.Item1, tieX ? bx.Item2 : by.Item2) * band;
                r.Calc.Add("Thép giằng trên dải đầu cọc rộng ≈ min(D + h0; cạnh đài) = " + Dt.Mm(band) + " mm: As = " + Dt.F(asBand * 1e4, "0.00") + " cm² ≥ T/Rs = " + Dt.F(stt.AsReq * 1e4, "0.00") + " cm²");
                r.Checks.Add(Dt.Check("Thanh chống – giằng: thép giằng trên dải đầu cọc As ≥ T/Rs", stt.AsReq * 1e4, asBand * 1e4, "cm²", "Blévot"));
                r.Checks.Add(Dt.Check("Thanh chống – giằng: góc thanh chống θ ≥ 40°", 40, stt.ThetaDeg, "°", "Blévot 45°–55°"));
                r.Checks.Add(Dt.Check("Thanh chống – giằng: ứng suất nút cột ≤ ν'·Rb", stt.SigmaColNode / 1000, stt.LimitColNode / 1000, "MPa", "EN 1992-1-1 mục 6.5.4 (tham khảo)"));
                r.Checks.Add(Dt.Check("Thanh chống – giằng: ứng suất nút đầu cọc ≤ 0,85·ν'·Rb", stt.SigmaPileNode / 1000, stt.LimitPileNode / 1000, "MPa", "EN 1992-1-1 mục 6.5.4 (tham khảo)"));
                if (stt.ThetaDeg > 55 + 1e-9) r.Messages.Add("Góc thanh chống θ = " + Dt.F(stt.ThetaDeg, "0.0") + "° > 55°: đài rất dày so với khoảng cách cọc – có thể giảm H");
            }
        }
        if (GroupEfficiency && n >= 2)
        {
            var (gnx, gny) = BoredPileDesign.Rows(piles);
            double eta = BoredPileDesign.GroupEfficiency(gnx, gny, PileD, sp);
            r.Calc.Add("Hệ số nhóm Converse–Labarre: θ = arctan(D/s) = " + Dt.F(Math.Atan(PileD / sp) * 180 / Math.PI, "0.0") + "°, η = 1 − θ·[(n1−1)n2 + (n2−1)n1]/(90·n1·n2) = " + Dt.F(eta, "0.000") + " (" + gnx + "×" + gny + ")");
            r.Checks.Add(Dt.Check("Nhóm cọc ma sát: N + G ≤ η·n·[P]", N + G, eta * n * Pa, "kN"));
        }
        foreach (var k in r.Checks.Where(x => !x.Ok)) r.Messages.Add("LỖI " + k.Name + ": " + Dt.F(k.Demand, "0.0") + " > " + Dt.F(k.Capacity, "0.0") + " " + k.Unit);

        // ---- thép ----
        double cb = Math.Max(s.CoverFooting, 0.05);
        double legX = Dt.R10(Math.Max(hh - 2 * cb, 0.15) / 2), lenX = Dt.R10(L - 2 * cb), lenY = Dt.R10(B - 2 * cb);
        r.Bars.Add(Dt.Hooked("Lớp dưới phương X", bx.Item1, Dt.CountAcross(B, cb, bx.Item2), lenX, legX, legX, false, bx.Item2));
        r.Bars.Add(Dt.Hooked("Lớp dưới phương Y", by.Item1, Dt.CountAcross(L, cb, by.Item2), lenY, legX, legX, false, by.Item2));
        if (HasTop || hh >= 0.8)
        {
            // lớp trên: Ø12a200 cấu tạo; cọc chịu nhổ → mô men âm tại mép cột do lực nhổ
            double mTop = 0;
            for (int i = 0; i < piles.Count; i++)
            {
                if (P[i] >= 0) continue;
                double dxp = Math.Abs(piles[i].X) - ColX / 2, dyp = Math.Abs(piles[i].Y) - ColY / 2;
                mTop = Math.Max(mTop, -P[i] * Math.Max(Math.Max(dxp, dyp), 0));
            }
            (int D, double S) tp;
            if (TopD > 0 && TopS > 0) tp = (TopD, TopS);
            else if (mTop > 1e-6) tp = Dt.ChooseSpaced(RcSection.DesignRect(mTop, Math.Min(L, B), hh - cb - 0.01, cb, conc, main, 0.05).As / Math.Min(L, B), new[] { 12, 14, 16 }, 0.20, 0.10);
            else tp = (12, 0.20);
            if (mTop > 1e-6) r.Calc.Add("Lớp trên: cọc chịu nhổ gây mô men âm tại mép cột M = " + Dt.F(mTop, "0.0") + " kN.m → " + Dt.Spaced(tp.D, tp.S));
            r.Bars.Add(Dt.Hooked("Lớp trên phương X", tp.D, Dt.CountAcross(B, cb, tp.S), lenX, legX, legX, true, tp.S));
            r.Bars.Add(Dt.Hooked("Lớp trên phương Y", tp.D, Dt.CountAcross(L, cb, tp.S), lenY, legX, legX, true, tp.S));
            if (!HasTop) r.Messages.Add("Đài cao ≥ 800 mm: thêm lưới thép trên " + Dt.Spaced(tp.D, tp.S) + " và thép cạnh đài chống nứt co ngót");
        }
        if (HasCage)
        {
            // thép đứng chữ U (chân chim) quanh cột, nhiều hàng trong vùng h0 – móc ôm lưới trên và lưới dưới
            int perRing = Math.Max(4, (int)Math.Ceiling(uP / cageS - 1e-9));
            double hkc = Dt.R10(Math.Max(10 * cageD / 1000.0, 0.10));
            r.Bars.Add(Dt.Hooked("Thép đứng lồng chống chọc thủng", cageD, perRing * cageRows, Dt.R10(hh - 2 * cb), hkc, hkc, false, cageS));
        }
        if (PileBarN > 0)
        {
            // râu thép đầu cọc: đoạn thẳng trong đài + bẻ ngang phần vượt chiều cao đài
            double room = Math.Max(hh - Embed - cb - 0.03, 0.15);
            double upLen = Math.Min(AnchorUsed, room), bendLen = Dt.R10(Math.Max(AnchorUsed - upLen, 0));
            r.Bars.Add(Dt.Hooked("Râu thép đầu cọc", PileBarD, PileBarN * n, Dt.R10(upLen), 0, bendLen));
        }
        if (IsBored) BoredExtras(r, s, n, main, concP, P);
        RebarNumbering.Renumber(r.Bars);
        r.ConcreteVolume = L * B * hh;
        r.FormworkArea = 2 * (L + B) * hh;
        double ov = Math.Max(LeanOverhang, 0);
        r.LeanVolume = (L + (bX ? 1 : 2) * ov) * (B + (bY ? 1 : 2) * ov) * s.LeanThickness;
        r.Extra.Add(((IsBored ? "Cọc khoan nhồi D" : "Cọc " + (RoundEff ? "D" : "")) + Dt.Mm(PileD) + (RoundEff ? "" : "x" + Dt.Mm(PileD)), "cọc", n));
        r.Extra.Add(("Đập đầu cọc", "cọc", n));
        return r;
    }

    /// <summary>Cọc khoan nhồi: hàm lượng thép, tải ngang (Phụ lục A), lồng thép (thép dọc, đai xoắn, đai gia cường), lún khối quy ước,
    /// khối lượng (bê tông cọc kể hao hụt, khoan tạo lỗ, dung dịch), ghi chú thi công & thử tải.</summary>
    void BoredExtras(DetailResult r, DetailSettings s, int n, Material main, Material concP, double[] P)
    {
        double d = PileD, ab = Math.PI * d * d / 4;
        int nb = Math.Max(PileBarN, 0), db = Math.Max(PileBarD, 10);
        double asP = nb * Dt.Area(db), mu = BoredPile.SteelRatio(d, asP);
        r.Calc.Add("Hàm lượng thép dọc cọc μ = As/Ab = " + Dt.F(mu, "0.00") + " % (≥ 0,4 % cọc khoan nhồi – TCVN 10304:2014 mục 8.12 / TCVN 9395:2012)");
        r.Checks.Add(Dt.Check("Hàm lượng thép dọc cọc khoan nhồi μ ≥ 0,4 %", 0.4, mu, "%", "TCVN 10304:2014"));
        if (nb < 6) r.Messages.Add("Cọc khoan nhồi nên có ≥ 6 thanh thép dọc (hiện " + nb + " thanh)");
        double cover = 0.05, rsDist = d / 2 - cover - db / 2000.0;
        double mu0 = BoredPile.MomentCapacity(main.Rs, asP, rsDist);
        double alpha = 0;
        if (HLat > 1e-9 && n > 0)
        {
            var lat = BoredPile.Lateral(HLat / n, d, PileLength, KLat > 0 ? KLat : 5000, concP.E);
            LateralResult = lat; alpha = lat.AlphaBd;
            r.Calc.Add(lat.Text + "; Mu ≈ Rs·As·2rs/π = " + Dt.F(mu0, "0.0") + " kN.m (bỏ qua bê tông – thiên an toàn)");
            r.Checks.Add(Dt.Check("Cọc chịu tải ngang: M đầu cọc ≤ Mu", lat.M0, mu0, "kN.m", "TCVN 10304:2014 Phụ lục A"));
            r.Checks.Add(Dt.Check("Cọc chịu tải ngang: chuyển vị đầu cọc y0 ≤ 10 mm", lat.Y0 * 1000, 10, "mm", "TCVN 10304:2014 Phụ lục A"));
        }
        bool tension = P.Length > 0 && P.Min() < -1e-9;
        double cage = CageLen > 0 ? Math.Min(CageLen, PileLength) : BoredPile.CageLength(PileLength, alpha, tension);
        CageLenUsed = cage;
        r.Calc.Add("Lồng thép cọc dài " + Dt.F(cage) + " m" + (tension ? " (toàn chiều dài – cọc chịu nhổ)" : CageLen > 0 ? "" : " (≥ max(L/3; 6 m" + (alpha > 0 ? "; 4/αbd + 1 m" : "") + "))")
                   + "; phần dưới lồng là bê tông không cốt thép (cọc chịu nén)");
        int sd = SpiralD > 0 ? SpiralD : (d <= 0.4 + 1e-9 ? 6 : 8);
        double ss = SpiralS > 0 ? SpiralS : 0.20, sTop = Math.Min(0.10, ss), lTop = Math.Min(Math.Max(2.0, 3 * d), cage);
        double dsp = d - 2 * cover + sd / 1000.0;
        int turns = (int)Math.Ceiling(lTop / sTop + (cage - lTop) / ss);
        double spiralLen = Dt.R10(turns * Math.Sqrt(Math.Pow(Math.PI * dsp, 2) + ss * ss));
        r.Calc.Add("Đai xoắn Ø" + sd + " a" + Dt.Mm(sTop) + " đoạn đầu cọc " + Dt.F(lTop) + " m, a" + Dt.Mm(ss) + " thân cọc; đai gia cường Ø16 mỗi 2 m; con kê " + BoredPileDesign.SpacersPerLevel(d) + " cái/tầng mỗi 2 m");
        // lồng thép dài > 11,7 m: chia đoạn, nối chồng 40d so le 50 %
        var spl = BoredPileDesign.CageSplices(cage, db, s.StockLength > 0 ? s.StockLength : BoredPileDesign.StockLength);
        CageSplice = spl;
        if (spl.Splices > 0)
            r.Calc.Add("Lồng thép dài " + Dt.F(cage) + " m > cây thép " + Dt.F(s.StockLength > 0 ? s.StockLength : BoredPileDesign.StockLength) + " m: chia " + spl.Segments.Count + " đoạn ("
                       + string.Join(" + ", spl.Segments.Select(x => Dt.F(x))) + " m), nối chồng lap = 40d = " + Dt.Mm(spl.Lap) + " mm tại độ sâu " + string.Join("; ", spl.SpliceDepths.Select(x => Dt.F(x)))
                       + " m; so le 50 % số thanh, lệch " + Dt.Mm(spl.Stagger) + " mm (≥ 1,3·lap); hàn / buộc nối lồng khi hạ");
        var bl = Dt.Straight("Cọc – thép dọc", db, nb * n, Dt.R10((spl.Splices > 0 ? spl.BarLength : cage) + Embed));
        r.Bars.Add(bl);
        r.Bars.Add(new BarMark { Role = "Cọc – đai xoắn", Dia = sd, Count = n, Shape = "Xoắn", Length = spiralLen, Spacing = ss, Segments = new List<double> { Math.Round(dsp * 1000), Math.Round(ss * 1000) } });
        int rings = Math.Max(1, (int)Math.Floor(cage / 2.0) + 1);
        double ringD = d - 2 * cover - 2 * db / 1000.0;
        r.Bars.Add(new BarMark { Role = "Cọc – đai gia cường", Dia = 16, Count = rings * n, Shape = "Vòng", Length = Dt.R10(Math.PI * ringD + 0.20), Segments = new List<double> { Math.Round(ringD * 1000) } });
        // con kê, ống siêu âm, ống khoan lõi mũi cọc, hệ số hao hụt gợi ý
        int spLv = BoredPileDesign.SpacerLevels(cage), spPer = BoredPileDesign.SpacersPerLevel(d);
        r.Extra.Add(("Con kê lồng thép (bê tông / nhựa)", "cái", (double)(n * spLv * spPer)));
        int tubes = BoredPileDesign.SonicTubes(d);
        double tubeLen = PileLength + Embed + 0.5;
        if (tubes > 0)
        {
            r.Calc.Add("Ống siêu âm kiểm tra bê tông cọc (TCVN 9396:2012): " + tubes + " ống thép D60×2 mỗi cọc D" + Dt.Mm(d) + ", dài " + Dt.F(tubeLen) + " m (từ mũi cọc tới trên đầu cọc 0,5 m), hàn vào mặt trong lồng thép, nút kín đầu dưới, bơm đầy nước trước khi đổ bê tông; sau thí nghiệm bơm vữa lấp");
            r.Extra.Add(("Ống siêu âm thép D60×2", "m", n * tubes * tubeLen));
        }
        if (d >= 1.0 - 1e-9)
        {
            r.Calc.Add("Cọc D ≥ 1000: đặt 1 ống thép D114 tới cách mũi cọc 1,0 m để khoan lấy lõi kiểm tra tiếp xúc mũi cọc (lắng cặn ≤ 50 mm)");
            r.Extra.Add(("Ống khoan lõi mũi cọc D114", "m", n * Math.Max(PileLength - 1.0 + Embed + 0.5, 0)));
        }
        r.Calc.Add("Móc cẩu lồng thép Ø" + (d >= 1.0 ? 25 : 20) + " (2–4 móc mỗi đoạn lồng) – thép tròn trơn; con kê " + spPer + " cái/tầng × " + spLv + " tầng (a2000)");
        if (SoilLayers != null && SoilLayers.Count > 0)
        {
            double ocs = BoredPileDesign.OvercutFactor(SoilLayers, Df, PileLength);
            r.Calc.Add("Hệ số hao hụt bê tông gợi ý theo địa chất thân cọc (đất yếu 1,20; cát 1,15; đất dính cứng 1,10 – bình quân bề dày) = " + Dt.F(ocs) + "; đang dùng " + Dt.F(Math.Max(Overcut, 1.0)));
        }
        double vPile = n * ab * PileLength * Math.Max(Overcut, 1.0);
        r.Extra.Add(("Bê tông cọc khoan nhồi " + concP.Name + " (kể hao hụt ×" + Dt.F(Math.Max(Overcut, 1.0)) + ")", "m³", vPile));
        r.Extra.Add(("Khoan tạo lỗ cọc D" + Dt.Mm(d) + " (từ mặt đất)", "m", n * (PileLength + Df)));
        if (DrillMethod == BoredPile.DrillSlurry) r.Extra.Add(("Dung dịch bentonite (≈ 1,2 thể tích lỗ khoan)", "m³", 1.2 * n * ab * (PileLength + Df)));
        if (DrillMethod == BoredPile.DrillCasing) r.Extra.Add(("Ống vách tạm (lắp / rút)", "m", n * Math.Min(6.0, PileLength + Df)));
        if (BoredResult != null)
        {
            double bgx = Piles.Count > 0 ? Piles.Max(q => q.X) - Piles.Min(q => q.X) + d : d, bgy = Piles.Count > 0 ? Piles.Max(q => q.Y) - Piles.Min(q => q.Y) + d : d;
            double ntc = (P.Sum()) / 1.15;
            var st = BoredPile.Settlement(SoilLayers, Math.Min(bgx, bgy), Math.Max(bgx, bgy), PileLength, Df + PileLength, ntc, PhiAvg);
            SettlementResult = st;
            r.Calc.AddRange(st.Calc);
            r.Checks.Add(Dt.Check("Lún khối móng quy ước S ≤ 80 mm", st.S * 1000, 80, "mm", "TCVN 10304:2014 / TCVN 9362:2012"));
            if (n >= 2)
            {
                var se = BoredPileDesign.SettlementEquivalentRaft(SoilLayers, Math.Min(bgx, bgy), Math.Max(bgx, bgy), PileLength, Df, ntc);
                r.Calc.AddRange(se.Calc);
                r.Calc.Add("So sánh: khối móng quy ước S = " + Dt.F(st.S * 1000, "0.0") + " mm; móng tương đương 2L/3 S = " + Dt.F(se.S * 1000, "0.0") + " mm (tham khảo)");
            }
        }
        r.Messages.AddRange(BoredPile.ConstructionNotes(n * Math.Max(1, Quantity), d));
    }

    public List<Figure> Draw(DetailResult r, DetailSettings s)
    {
        var figs = new List<Figure>();
        if (r == null || Piles.Count == 0 || CapL <= 0) return figs;
        double S = 50;
        var w = new DxfWriter(S);
        double L = CapL * 1000, B = CapB * 1000, d = PileD * 1000;
        double ox = CapCx * 1000, oy = CapCy * 1000;
        w.Rect(Lay.BeTong, ox - L / 2, oy - B / 2, ox + L / 2, oy + B / 2);
        w.Rect(Lay.BeTong, -ColX * 500, -ColY * 500, ColX * 500, ColY * 500);
        if (Dt.IsNeighbour(BoundaryX)) { double bx0 = (BoundaryX.Contains("trái") ? ox - L / 2 : ox + L / 2); w.Line(Lay.KyHieu, bx0, oy - B / 2 - 600, bx0, oy + B / 2 + 600, "CENTER"); w.Text(Lay.Chu, bx0 - 0.6 * w.Th, oy, 0.8 * w.Th, "RANH ĐẤT", 90, 1, 2); }
        if (Dt.IsNeighbour(BoundaryY)) { double by0 = (BoundaryY.Contains("dưới") ? oy - B / 2 : oy + B / 2); w.Line(Lay.KyHieu, ox - L / 2 - 600, by0, ox + L / 2 + 600, by0, "CENTER"); w.Text(Lay.Chu, ox, by0 - 0.6 * w.Th, 0.8 * w.Th, "RANH ĐẤT", 0, 1, 3); }
        w.Line(Lay.Truc, 0, -B / 2 - 800, 0, B / 2 + 800, "CENTER");
        w.Line(Lay.Truc, -L / 2 - 800, 0, L / 2 + 800, 0, "CENTER");
        for (int i = 0; i < Piles.Count; i++)
        {
            double x = Piles[i].X * 1000, y = Piles[i].Y * 1000;
            if (RoundEff) w.Circle(Lay.BeTong, x, y, d / 2); else w.Rect(Lay.BeTong, x - d / 2, y - d / 2, x + d / 2, y + d / 2, "DASHED");
            w.Line(Lay.KyHieu, x - d / 2, y - d / 2, x + d / 2, y + d / 2);
            w.Line(Lay.KyHieu, x - d / 2, y + d / 2, x + d / 2, y - d / 2);
            if (i < PileForces.Length) w.Text(Lay.Chu, x, y + d / 2 + 60, 0.8 * w.Th, "P=" + Dt.F(PileForces[i], "0"), 0, 1, 0);
        }
        var bx = r.Bars.FirstOrDefault(b => b.Role == "Lớp dưới phương X"); var by = r.Bars.FirstOrDefault(b => b.Role == "Lớp dưới phương Y");
        double c = Math.Max(s.CoverFooting, 0.05) * 1000;
        double hkp = 0.9 * w.Th;
        if (bx != null) PlanBarDraw.Bar(w, true, ox - L / 2 + c, ox + L / 2 - c, oy - B / 4, Dt.Spaced(bx.Dia, bx.Spacing), bx.Mark, hkp, hkp, 1, 0.62, (oy - B / 2 + c, oy + B / 2 - c), 0.12);
        if (by != null) PlanBarDraw.Bar(w, false, oy - B / 2 + c, oy + B / 2 - c, ox + L / 4, Dt.Spaced(by.Dia, by.Spacing), by.Mark, hkp, hkp, -1, 0.62, (ox - L / 2 + c, ox + L / 2 - c), 0.12);
        // lớp trên (nét đứt quy ước thép lớp trên trên mặt bằng)
        var tx = r.Bars.FirstOrDefault(b => b.Role == "Lớp trên phương X"); var ty = r.Bars.FirstOrDefault(b => b.Role == "Lớp trên phương Y");
        if (tx != null) PlanBarDraw.Bar(w, true, ox - L / 2 + c, ox + L / 2 - c, oy + B / 4, Dt.Spaced(tx.Dia, tx.Spacing) + " (trên)", tx.Mark, hkp, hkp, -1, 0.38, (oy - B / 2 + c, oy + B / 2 - c), 0.88);
        if (ty != null) PlanBarDraw.Bar(w, false, oy - B / 2 + c, oy + B / 2 - c, ox - L / 4, Dt.Spaced(ty.Dia, ty.Spacing) + " (trên)", ty.Mark, hkp, hkp, 1, 0.38, (ox - L / 2 + c, ox + L / 2 - c), 0.88);
        // lồng thép chống chọc thủng: thép đứng quanh cột (vòng chấm)
        var cgp = r.Bars.FirstOrDefault(b => b.Role == "Thép đứng lồng chống chọc thủng");
        if (cgp != null)
        {
            double hh0 = Math.Max(CapH * 1000 - c - Embed * 1000, 200), sp = Math.Max(cgp.Spacing * 1000, 100);
            for (double rr = 0.5 * sp; rr <= hh0 + 1e-6; rr += sp)
            {
                double ax = ColX * 500 + rr, ay = ColY * 500 + rr;
                for (double t = -ax; t <= ax + 1e-6; t += sp) { w.Dot(Lay.Thep, t, -ay, cgp.Dia * 0.6); w.Dot(Lay.Thep, t, ay, cgp.Dia * 0.6); }
                for (double t = -ay + sp; t <= ay - sp + 1e-6; t += sp) { w.Dot(Lay.Thep, -ax, t, cgp.Dia * 0.6); w.Dot(Lay.Thep, ax, t, cgp.Dia * 0.6); }
            }
            w.LeaderTag(ColX * 500 + 0.5 * sp, -ColY * 500 - 0.5 * sp, ColX * 500 + 2.5 * w.Th, -ColY * 500 - 3.5 * w.Th, Dt.Spaced(cgp.Dia, cgp.Spacing) + " thép đứng", cgp.Mark);
        }
        var xs = new List<double> { ox - L / 2 }; xs.AddRange(Piles.Select(p => p.X * 1000).Distinct().OrderBy(x => x)); xs.Add(ox + L / 2);
        var ys = new List<double> { oy - B / 2 }; ys.AddRange(Piles.Select(p => p.Y * 1000).Distinct().OrderBy(x => x)); ys.Add(oy + B / 2);
        w.DimChainH(xs, oy - B / 2, oy - B / 2 - 2.5 * w.Th);
        w.Dim(ox - L / 2, oy - B / 2, ox + L / 2, oy - B / 2, -4.0 * w.Th);
        w.DimChainV(ys, ox + L / 2, ox + L / 2 + 2.5 * w.Th);
        w.Dim(ox + L / 2, oy - B / 2, ox + L / 2, oy + B / 2, 4.0 * w.Th);
        w.Title(ox, oy - B / 2 - 2100, RebarSchedule.MemberLabel("MẶT BẰNG ĐÀI " + Name, r.Quantity), DrawUtil.ScaleText(S));
        figs.Add(new Figure(w, Name, true));

        // mặt cắt 1-1 phương X
        var x2 = new DxfWriter(S);
        double H = CapH * 1000, em = Embed * 1000;
        x2.Rect(Lay.BeTong, ox - L / 2, 0, ox + L / 2, H);
        double lo = Math.Max(LeanOverhang, 0) * 1000;
        x2.Rect(Lay.KyHieu, ox - L / 2 - ((BoundaryX ?? "").Contains("trái") ? 0 : lo), -s.LeanThickness * 1000, ox + L / 2 + ((BoundaryX ?? "").Contains("phải") ? 0 : lo), 0, "DASHED");
        x2.Rect(Lay.BeTong, -ColX * 500, H, ColX * 500, H + 700);
        x2.BreakH(Lay.BeTong, -ColX * 500 - 100, ColX * 500 + 100, H + 700);
        foreach (var px in Piles.Select(p => p.X * 1000).Distinct())
        {
            x2.Rect(Lay.BeTong, px - d / 2, -1200, px + d / 2, em);
            x2.BreakH(Lay.BeTong, px - d / 2 - 80, px + d / 2 + 80, -1200);
        }
        if (bx != null)
        {
            double leg = bx.Segments.Count > 2 ? bx.Segments[0] : 0;
            x2.Polyline(Lay.Thep, new List<(double, double)> { (ox - L / 2 + c, em + c + leg), (ox - L / 2 + c, em + c), (ox + L / 2 - c, em + c), (ox + L / 2 - c, em + c + leg) }, false);
            x2.LeaderTag(0, em + c, 400, -700, Dt.Spaced(bx.Dia, bx.Spacing), bx.Mark);
        }
        // lớp trên, lồng thép, râu thép đầu cọc
        var tX = r.Bars.FirstOrDefault(b => b.Role == "Lớp trên phương X");
        if (tX != null)
        {
            double legT = tX.Segments.Count > 2 ? tX.Segments[0] : 0;
            x2.Polyline(Lay.Thep, new List<(double, double)> { (ox - L / 2 + c, H - c - legT), (ox - L / 2 + c, H - c), (ox + L / 2 - c, H - c), (ox + L / 2 - c, H - c - legT) }, false);
            x2.LeaderTag(ox + L / 4, H - c, ox + L / 4 + 2.0 * x2.Th, H + 3.0 * x2.Th, Dt.Spaced(tX.Dia, tX.Spacing), tX.Mark);
        }
        var cg = r.Bars.FirstOrDefault(b => b.Role == "Thép đứng lồng chống chọc thủng");
        if (cg != null)
        {
            double sp = Math.Max(cg.Spacing * 1000, 100), h0d = H - c - em, yb0 = em + c, yt0 = H - c;
            double hkd = cg.Segments.Count > 2 ? cg.Segments[0] : 100;
            for (double rr = 0.5 * sp; rr <= h0d + 1e-6; rr += sp)
                foreach (double sg in new[] { -1.0, 1.0 })
                {
                    double xv = sg * (ColX * 500 + rr);
                    x2.Polyline(Lay.Dai, new List<(double, double)> { (xv + hkd, yt0), (xv, yt0), (xv, yb0), (xv + hkd, yb0) }, false);
                }
            x2.LeaderTag(ColX * 500 + 0.5 * sp, (yb0 + yt0) / 2, ColX * 500 + 0.5 * sp + 3.0 * x2.Th, (yb0 + yt0) / 2 + 1.5 * x2.Th, Dt.Spaced(cg.Dia, cg.Spacing), cg.Mark);
        }
        var an = r.Bars.FirstOrDefault(b => b.Role == "Râu thép đầu cọc");
        if (an != null)
        {
            double upL = an.Segments.Count > 0 ? an.Segments[0] : 400, bendL = an.Segments.Count > 1 ? an.Segments[1] : 0;
            bool firstTag = true;
            foreach (var px in Piles.Select(p => p.X * 1000).Distinct())
            {
                foreach (double sg in new[] { -1.0, 1.0 })
                {
                    double xa = px + sg * (d / 2 - 50);
                    var pts = new List<(double, double)> { (xa, em - 150), (xa, em + upL) };
                    if (bendL > 1) pts.Add((xa + sg * bendL, em + upL));
                    x2.Polyline(Lay.Thep, pts, false);
                }
                x2.Text(Lay.KyHieu, px, em + 0.3 * x2.Th, 0.6 * x2.Th, "ĐẬP ĐẦU CỌC", 0, 1, 0);
                if (firstTag)
                {
                    x2.LeaderTag(px + d / 2 - 50, em + upL * 0.5, px - L / 2 - 1.0 * x2.Th, em + upL * 0.5 + 1.0 * x2.Th, an.Count / Math.Max(Piles.Count, 1) + "Ø" + an.Dia + " râu cọc", an.Mark, -1);
                    firstTag = false;
                }
            }
        }
        // tháp chọc thủng 45° từ mép cột tới đầu cọc (nét đứt) – cọc có tim trong đáy tháp không gây chọc thủng
        {
            double yTop = H, yBot = em, dz = yTop - yBot;
            if (dz > 0)
            {
                double xr = Math.Min(ColX * 500 + dz, ox + L / 2), xl = Math.Max(-ColX * 500 - dz, ox - L / 2);
                x2.Line(Lay.KyHieu, ColX * 500, yTop, xr, yTop - (xr - ColX * 500), "DASHED");
                x2.Line(Lay.KyHieu, -ColX * 500, yTop, xl, yTop - (-ColX * 500 - xl), "DASHED");
                x2.Text(Lay.KyHieu, xr + 0.3 * x2.Th, (yTop + yBot) / 2, 0.6 * x2.Th, "THÁP CHỌC THỦNG 45°", 0, 0, 2);
            }
        }
        x2.Dim(ox + L / 2, 0, ox + L / 2, H, 2.0 * x2.Th);
        x2.Dim(ox - L / 2, 0, ox + L / 2, 0, -4.0 * x2.Th);
        x2.ElevMark(ox - L / 2 - 300, 0, DxfWriter.ElevText(-Df));
        x2.Title(0, -2300, "1-1", DrawUtil.ScaleText(S), true);
        figs.Add(new Figure(x2, Name + " mặt cắt"));
        if (IsBored)
        {
            try { figs.AddRange(BoredPileDrawing.Figures(this, r, s)); }
            catch (Exception ex) { r.Messages.Add("LỖI vẽ chi tiết cọc khoan nhồi: " + ex.Message); }
        }
        return figs;
    }
}
