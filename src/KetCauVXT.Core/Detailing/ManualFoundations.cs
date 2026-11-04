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
    [Field("Sức chịu tải nền R (tiêu chuẩn)", Unit = "kN/m²", Group = "Nền")] public double R { get; set; } = 150;
    [Field("Độ sâu chôn móng Df", Unit = "m", Group = "Nền")] public double Df { get; set; } = 1.5;
    [Field("γtb đất + móng", Unit = "kN/m³", Group = "Nền")] public double GammaTb { get; set; } = 20;
    [Field("Cạnh L (0 = tự chọn)", Unit = "m", Group = "Kích thước")] public double L { get; set; }
    [Field("Cạnh B (0 = tự chọn)", Unit = "m", Group = "Kích thước")] public double B { get; set; }
    [Field("Chiều cao móng H (0 = tự chọn)", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double H { get; set; }
    [Field("Chiều cao mép (móng vát, 0 = móng phẳng)", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Hb { get; set; }
    [Field("Thép phương L – Ø (0 = tự chọn)", Unit = "mm", Group = "Thép")] public int DiaL { get; set; }
    [Field("Thép phương L – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double SL { get; set; }
    [Field("Thép phương B – Ø", Unit = "mm", Group = "Thép")] public int DiaB { get; set; }
    [Field("Thép phương B – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double SB { get; set; }

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
        r.Messages = f.Messages; r.Checks = f.Checks; r.Bars = f.Bars;
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
                var pp = Pressure(ntc, mLt, mBt, l, b, GammaTb, Df);
                if (pp.Ptb <= R && pp.Pmax <= 1.2 * R && pp.Pmin >= 0) break;
                if (L <= 0) l += 0.1;
                if (B <= 0 && (it % 2 == 1 || L > 0)) b += 0.1;
                l = Math.Round(l, 2); b = Math.Round(b, 2);
            }
        }
        var (ptb, pmax, pmin) = Pressure(ntc, mLt, mBt, l, b, GammaTb, Df);
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
            double ap = Math.Min((ColL + 2 * h0) * (ColB + 2 * h0), l * b);
            double u = 2 * (ColL + ColB + 2 * h0);
            return (N - pnet * ap, conc.Rbt * u * h0);
        }
        double hh = H;
        if (hh <= 0)
        {
            hh = 0.30;
            while (hh < 2.0 && Punch(hh - c - 0.012).F > Punch(hh - c - 0.012).Fu) hh = Math.Round(hh + 0.05, 2);
            // móng cứng: h ≥ (L − bc)/6 để áp lực phân bố gần tuyến tính
            hh = Math.Max(hh, RcSection.CeilTo(Math.Max(l - ColL, b - ColB) / 6, 0.05));
        }
        f.H = hh; f.Hb = Hb > 0 && Hb < hh ? Hb : hh;
        double h0 = hh - c - 0.012;
        var (fP, fuP) = Punch(h0);
        r.Calc.Add("Chọc thủng: h0 = " + Dt.Mm(h0) + " mm, u = 2(bc + hc + 2h0) = " + Dt.F(2 * (ColL + ColB + 2 * h0), "0.00") + " m; F = N − pnet·(bc + 2h0)(hc + 2h0) = "
                   + Dt.F(fP, "0.0") + " kN ≤ Rbt·u·h0 = " + Dt.F(fuP, "0.0") + " kN");
        r.Checks.Add(Dt.Check("Chọc thủng cột – móng F ≤ Rbt·u·h0", fP, fuP, "kN", "TCVN 5574:2018 mục 8.1.6.3"));
        // ---- thép: mô men tại mép cột với áp lực hình thang ----
        double l1 = (l - ColL) / 2, b1 = (b - ColB) / 2;
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
        double vol = l * b * f.Hb + (f.H - f.Hb) / 3 * (l * b + ColL * ColB + Math.Sqrt(l * b * ColL * ColB));
        r.ConcreteVolume = vol;
        r.FormworkArea = 2 * (l + b) * f.Hb;
        r.LeanVolume = (l + 0.2) * (b + 0.2) * s.LeanThickness;
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
    [Field("Bề rộng sườn (dầm móng)", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Bs { get; set; } = 0.35;
    [Field("Chiều cao sườn tổng (0 = tự chọn)", Unit = "mm", Scale = 1000, Group = "Kích thước")] public double Hs { get; set; }
    [Field("Sức chịu tải nền R", Unit = "kN/m²", Group = "Nền")] public double R { get; set; } = 150;
    [Field("Hệ số nền k (0 = 120·R theo Bowles)", Unit = "kN/m³", Group = "Nền")] public double K { get; set; }
    [Field("Độ sâu chôn móng Df", Unit = "m", Group = "Nền")] public double Df { get; set; } = 1.5;
    [Field("γtb đất + móng", Unit = "kN/m³", Group = "Nền")] public double GammaTb { get; set; } = 20;
    [Field("Thép sườn dưới – số thanh (0 = tự chọn)", Group = "Thép")] public int BotN { get; set; }
    [Field("Thép sườn dưới – Ø", Unit = "mm", Group = "Thép")] public int BotD { get; set; } = 20;
    [Field("Thép sườn trên – số thanh (0 = tự chọn)", Group = "Thép")] public int TopN { get; set; }
    [Field("Thép sườn trên – Ø", Unit = "mm", Group = "Thép")] public int TopD { get; set; } = 20;
    [Field("Đai – Ø", Unit = "mm", Group = "Thép")] public int StirD { get; set; } = 8;
    [Field("Bước đai", Unit = "mm", Scale = 1000, Group = "Thép")] public double StirS { get; set; } = 0.15;
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
        double qu = RcSection.ShearCapacity(Bs, h0, conc, small, StirD, Math.Max(Legs, 2), StirS);
        r.Checks.Add(Dt.Check("Sườn chịu cắt Q ≤ Qb + Qsw (đai " + Dt.Spaced(StirD, StirS) + ")", vMax, qu, "kN"));
        if (!Dt.FitsInLayer(Bs, cb, StirD, nb, BotD, false)) r.Messages.Add("Thép dưới sườn " + Dt.Bars(nb, BotD) + " không đủ khe hở 1 lớp – đặt 2 lớp hoặc tăng bề rộng sườn");
        // ---- bản cánh: console từ mép sườn, M = p·l²/2 ----
        double lc = (b - Bs) / 2, pn = sumN / (b * Length) * (pMax - gD) / Math.Max(pTb - gD, 1e-9);
        double mF = pn * lc * lc / 2, h0f = Hf - c - 0.006;
        double asF = RcSection.DesignRect(mF, 1.0, h0f, c, conc, main, 0.10).As;
        var fl = FlangeD > 0 && FlangeS > 0 ? (FlangeD, FlangeS) : Dt.ChooseSpaced(asF, new[] { 10, 12, 14, 16 }, 0.20, 0.10);
        double muF = RcSection.MomentCapacity(1.0, h0f, Dt.AreaPerM(fl.Item1, fl.Item2), 0, c, conc, main);
        r.Calc.Add("Bản cánh console l = (B − bs)/2 = " + Dt.F(lc) + " m, p = " + Dt.F(pn, "0.0") + " kN/m² → M = p·l²/2 = " + Dt.F(mF, "0.00") + " kN.m/m → " + Dt.Spaced(fl.Item1, fl.Item2));
        r.Checks.Add(Dt.Check("Bản cánh chịu uốn (thép ngang " + Dt.Spaced(fl.Item1, fl.Item2) + ")", mF, muF, "kN.m/m"));
        // cắt bản cánh tại mép sườn (không đặt cốt đai): Q ≤ 0,5·Rbt·b·h0·(2,5)… lấy Qb,min = 0,5·Rbt·b·h0 (mục 8.1.3.3)
        double qF = pn * Math.Max(lc - h0f, 0), qbF = 0.5 * conc.Rbt * 1.0 * h0f * 2.5 / 1.0;
        r.Checks.Add(Dt.Check("Bản cánh chịu cắt Q ≤ Qb (không cốt đai)", qF, Math.Min(qbF, 2.5 * conc.Rbt * h0f), "kN/m"));
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
        r.Bars.Add(Dt.Stirrup("Đai", StirD, (int)Math.Floor(lenRib / StirS) + 1, Bs - 2 * cs, hs - c - cs, StirS));
        double hk = Dt.R10(Math.Max(Hf - 2 * c, 0.10));
        r.Bars.Add(Dt.Hooked("Lớp dưới phương ngang", fl.Item1, (int)Math.Floor(lenRib / fl.Item2) + 1, Dt.R10(b - 2 * c), hk, hk, false, fl.Item2));
        var dist = Dt.ChooseSpaced(0.2 * Dt.AreaPerM(fl.Item1, fl.Item2), new[] { 10, 12 }, 0.25, 0.15);
        r.Bars.Add(Dt.Straight("Lớp dưới phương dọc", dist.D, 2 * Dt.CountAcross(lc + 0.05, 0.05, dist.S), lenRib, false, dist.S));
        if (lenRib > s.StockLength) r.Messages.Add("Thép dọc dài " + Dt.F(lenRib) + " m > " + Dt.F(s.StockLength) + " m: nối thép dưới tại giữa nhịp, thép trên tại vị trí cột (ngoài vùng mô men lớn), so le 50 %");
        RebarNumbering.Renumber(r.Bars);
        double area = b * Hf + Bs * (hs - Hf) + (b - Bs) / 2 * 0;   // bỏ qua vát cánh
        r.ConcreteVolume = area * Length;
        r.FormworkArea = (2 * Hf + 2 * (hs - Hf)) * Length + 2 * area;
        r.LeanVolume = (b + 0.2) * (Length + 0.2) * s.LeanThickness;
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
            w.LeaderTag(L * 0.3, c, L * 0.3 + 300, -700, Dt.Bars(bot.Count, bot.Dia), bot.Mark);
        }
        if (top != null)
        {
            double leg = top.Segments.Count > 2 ? top.Segments[0] : 0;
            w.Polyline(Lay.Thep, new List<(double, double)> { (c, hs - c - leg), (c, hs - c), (L - c, hs - c), (L - c, hs - c - leg) }, false);
            w.LeaderTag(L * 0.6, hs - c, L * 0.6 + 300, hs + 1100, Dt.Bars(top.Count, top.Dia), top.Mark);
        }
        var st = r.Bars.FirstOrDefault(b => b.Role == "Đai");
        if (st != null)
        {
            for (double x = 2 * c; x < L - c; x += Math.Max(st.Spacing * 1000, 100) * 4) w.Line(Lay.Dai, x, c, x, hs - c);
            w.LeaderTag(L * 0.45, hs / 2, L * 0.45 + 400, hs / 2 + 300, Dt.Spaced(st.Dia, st.Spacing), st.Mark);
        }
        var xsCol = new List<double> { 0 };
        xsCol.AddRange(Columns.Where(x => x.N > 0).Select(x => x.X * 1000).OrderBy(x => x));
        xsCol.Add(L);
        w.DimChainH(xsCol, 0, -1300);
        w.Dim(0, 0, L, 0, -1900);
        w.Dim(L, 0, L, hs, 600);
        w.Title(L / 2, -2500, RebarSchedule.MemberLabel("MẶT ĐỨNG MÓNG BĂNG " + Name, r.Quantity), DrawUtil.ScaleText(S));
        figs.Add(new Figure(w, Name, true));

        // mặt cắt ngang 1-1 (chữ T ngược)
        double Ss = 25;
        var x2 = new DxfWriter(Ss);
        double B = BUsed * 1000, bs = Bs * 1000;
        x2.Polyline(Lay.BeTong, new List<(double, double)> { (-B / 2, 0), (B / 2, 0), (B / 2, hf), (bs / 2, hf), (bs / 2, hs), (-bs / 2, hs), (-bs / 2, hf), (-B / 2, hf) }, true);
        x2.Rect(Lay.KyHieu, -B / 2 - 100, -s.LeanThickness * 1000, B / 2 + 100, 0);
        var fl = r.Bars.FirstOrDefault(b => b.Role == "Lớp dưới phương ngang");
        if (fl != null)
        {
            x2.Polyline(Lay.Thep, new List<(double, double)> { (-B / 2 + c, hf - c), (-B / 2 + c, c), (B / 2 - c, c), (B / 2 - c, hf - c) }, false);
            x2.LeaderTag(-B / 4, c, -B / 4 - 200, -600, Dt.Spaced(fl.Dia, fl.Spacing), fl.Mark, -1);
        }
        if (st != null)
        {
            double cs = s.CoverBeam * 1000;
            x2.Rect(Lay.Dai, -bs / 2 + cs, c + 10, bs / 2 - cs, hs - cs);
            x2.LeaderTag(bs / 2 - cs, hs * 0.55, bs / 2 + 400, hs * 0.55 + 200, Dt.Spaced(st.Dia, st.Spacing), st.Mark);
        }
        void Row(BarMark b, double y, bool up)
        {
            if (b == null) return;
            double cs = s.CoverBeam * 1000 + b.Dia / 2.0;
            int n = Math.Max(b.Count, 2);
            for (int i = 0; i < n; i++) x2.Dot(Lay.Thep, -bs / 2 + cs + i * (bs - 2 * cs) / (n - 1), y, b.Dia / 2.0);
            x2.LeaderTag(0, y, 300, up ? hs + 500 : -900, Dt.Bars(b.Count, b.Dia), b.Mark);
        }
        Row(top, hs - s.CoverBeam * 1000 - (top?.Dia ?? 0) / 2.0, true);
        Row(bot, c + 20 + (bot?.Dia ?? 0) / 2.0, false);
        x2.Dim(-B / 2, 0, B / 2, 0, -1300);
        x2.Dim(-bs / 2, hs, bs / 2, hs, 300);
        x2.Dim(B / 2, 0, B / 2, hf, 400);
        x2.Dim(B / 2, 0, B / 2, hs, 900);
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
    [Field("Số cọc (0 = tự chọn)", Group = "Cọc")] public int NumPiles { get; set; }
    [Field("Khoảng cách tim cọc (0 = 3D)", Unit = "m", Group = "Cọc")] public double Spacing { get; set; }
    [Field("Tim cọc tới mép đài (0 = max(D; 0,7D + 0,15))", Unit = "m", Group = "Cọc")] public double Edge { get; set; }
    [Field("Đầu cọc ngàm vào đài", Unit = "mm", Scale = 1000, Group = "Cọc")] public double Embed { get; set; } = 0.10;
    [Field("Chiều cao đài H (0 = tự chọn)", Unit = "mm", Scale = 1000, Group = "Đài")] public double H { get; set; }
    [Field("Độ sâu đáy đài Df", Unit = "m", Group = "Đài")] public double Df { get; set; } = 1.5;
    [Field("γtb đất + đài", Unit = "kN/m³", Group = "Đài")] public double GammaTb { get; set; } = 20;
    [Field("Thép phương X – Ø (0 = tự chọn)", Unit = "mm", Group = "Thép")] public int DiaX { get; set; }
    [Field("Thép phương X – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double SX { get; set; }
    [Field("Thép phương Y – Ø", Unit = "mm", Group = "Thép")] public int DiaY { get; set; }
    [Field("Thép phương Y – bước", Unit = "mm", Scale = 1000, Group = "Thép")] public double SY { get; set; }

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
        if (N <= 0 || Pa <= 0 || PileD <= 0.1)
        {
            r.Messages.Add("LỖI cần N > 0, [P] > 0, D cọc ≥ 100 mm");
            return r;
        }
        double sp = Spacing > 0 ? Spacing : 3 * PileD;
        double edge = Edge > 0 ? Edge : Math.Max(PileD, 0.7 * PileD + 0.15);
        bool longX = Math.Abs(My) >= Math.Abs(Mx);
        int n = NumPiles > 0 ? NumPiles : Math.Max(1, (int)Math.Ceiling(1.15 * N / Pa));
        double[] P = Array.Empty<double>(); List<(double X, double Y)> piles = new(); double L = 0, B = 0, G = 0;
        for (int it = 0; it < 30; it++)
        {
            piles = PileCapDesigner.Layout(n, sp, longX);
            L = piles.Max(p => p.X) - piles.Min(p => p.X) + 2 * edge;
            B = piles.Max(p => p.Y) - piles.Min(p => p.Y) + 2 * edge;
            L = Math.Max(L, ColX + 0.3); B = Math.Max(B, ColY + 0.3);
            G = 1.1 * GammaTb * L * B * Df;
            P = Forces(N, Mx, My, G, piles);
            if (NumPiles > 0 || (P.Max() <= Pa && P.Min() >= -Pt) || n >= 25) break;
            n++;
        }
        Piles = piles; PileForces = P; CapL = L; CapB = B;
        r.Calc.Add("## Đài cọc " + Name + ": " + n + " cọc " + (Round ? "D" : "") + Dt.Mm(PileD) + (Round ? "" : "x" + Dt.Mm(PileD)) + ", khoảng cách " + Dt.F(sp) + " m, đài "
                   + Dt.F(L) + " × " + Dt.F(B) + " m");
        r.Calc.Add("Pi = (N + G)/n ± Mx·yi/Σyi² ± My·xi/Σxi², G = 1,1·γtb·Lđ·Bđ·Df = " + Dt.F(G, "0.0") + " kN (TCVN 10304:2014)");
        r.Calc.Add("Pmax = " + Dt.F(P.Max(), "0.0") + " kN; Pmin = " + Dt.F(P.Min(), "0.0") + " kN; [P] = " + Dt.F(Pa, "0") + " kN");
        r.Checks.Add(Dt.Check("Lực lớn nhất lên cọc Pmax ≤ [P]", P.Max(), Pa, "kN"));
        r.Checks.Add(Dt.Check("Cọc chịu nhổ: lực nhổ max(−Pmin; 0) ≤ [Pk]", Math.Max(-P.Min(), 0), Pt, "kN"));
        r.Checks.Add(Dt.Check("Khoảng cách tim cọc ≥ 3D (TCVN 10304:2014 mục 8.13)", 3 * PileD, sp, "m"));

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
        double hh = H;
        if (hh <= 0)
        {
            hh = Math.Max(0.5, 2 * PileD);
            while (hh < 2.5 && Punch(hh - c).F > Punch(hh - c).Fu) hh = Math.Round(hh + 0.05, 2);
        }
        CapH = hh;
        double h0x = hh - c;
        var (fP, fuP) = Punch(h0x);
        r.Calc.Add("Chọc thủng cột – đài: h0 = H − a = " + Dt.Mm(h0x) + " mm; F = N − Σ(cọc trong tháp chọc thủng) = " + Dt.F(fP, "0.0") + " kN ≤ Rbt·u·h0 = " + Dt.F(fuP, "0.0") + " kN");
        r.Checks.Add(Dt.Check("Chọc thủng cột – đài F ≤ Rbt·u·h0", fP, fuP, "kN", "TCVN 5574:2018 mục 8.1.6"));
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
        foreach (var k in r.Checks.Where(x => !x.Ok)) r.Messages.Add("LỖI " + k.Name + ": " + Dt.F(k.Demand, "0.0") + " > " + Dt.F(k.Capacity, "0.0") + " " + k.Unit);

        // ---- thép ----
        double cb = Math.Max(s.CoverFooting, 0.05);
        double legX = Dt.R10(Math.Max(hh - 2 * cb, 0.15) / 2), lenX = Dt.R10(L - 2 * cb), lenY = Dt.R10(B - 2 * cb);
        r.Bars.Add(Dt.Hooked("Lớp dưới phương X", bx.Item1, Dt.CountAcross(B, cb, bx.Item2), lenX, legX, legX, false, bx.Item2));
        r.Bars.Add(Dt.Hooked("Lớp dưới phương Y", by.Item1, Dt.CountAcross(L, cb, by.Item2), lenY, legX, legX, false, by.Item2));
        if (hh >= 0.8)
        {
            var tp = Dt.ChooseSpaced(0, new[] { 12 }, 0.20, 0.20);
            r.Bars.Add(Dt.Hooked("Lớp trên phương X", tp.D, Dt.CountAcross(B, cb, tp.S), lenX, legX, legX, true, tp.S));
            r.Bars.Add(Dt.Hooked("Lớp trên phương Y", tp.D, Dt.CountAcross(L, cb, tp.S), lenY, legX, legX, true, tp.S));
            r.Messages.Add("Đài cao ≥ 800 mm: thêm lưới thép trên Ø12a200 và thép cạnh đài chống nứt co ngót");
        }
        RebarNumbering.Renumber(r.Bars);
        r.ConcreteVolume = L * B * hh;
        r.FormworkArea = 2 * (L + B) * hh;
        r.LeanVolume = (L + 0.2) * (B + 0.2) * s.LeanThickness;
        r.Extra.Add(("Cọc " + (Round ? "D" : "") + Dt.Mm(PileD) + (Round ? "" : "x" + Dt.Mm(PileD)), "cọc", n));
        r.Extra.Add(("Đập đầu cọc", "cọc", n));
        return r;
    }

    public List<Figure> Draw(DetailResult r, DetailSettings s)
    {
        var figs = new List<Figure>();
        if (r == null || Piles.Count == 0 || CapL <= 0) return figs;
        double S = 50;
        var w = new DxfWriter(S);
        double L = CapL * 1000, B = CapB * 1000, d = PileD * 1000;
        w.Rect(Lay.BeTong, -L / 2, -B / 2, L / 2, B / 2);
        w.Rect(Lay.BeTong, -ColX * 500, -ColY * 500, ColX * 500, ColY * 500);
        w.Line(Lay.Truc, 0, -B / 2 - 800, 0, B / 2 + 800, "CENTER");
        w.Line(Lay.Truc, -L / 2 - 800, 0, L / 2 + 800, 0, "CENTER");
        for (int i = 0; i < Piles.Count; i++)
        {
            double x = Piles[i].X * 1000, y = Piles[i].Y * 1000;
            if (Round) w.Circle(Lay.BeTong, x, y, d / 2); else w.Rect(Lay.BeTong, x - d / 2, y - d / 2, x + d / 2, y + d / 2, "DASHED");
            w.Line(Lay.KyHieu, x - d / 2, y - d / 2, x + d / 2, y + d / 2);
            w.Line(Lay.KyHieu, x - d / 2, y + d / 2, x + d / 2, y - d / 2);
            if (i < PileForces.Length) w.Text(Lay.Chu, x, y + d / 2 + 60, 0.8 * w.Th, "P=" + Dt.F(PileForces[i], "0"), 0, 1, 0);
        }
        var bx = r.Bars.FirstOrDefault(b => b.Role == "Lớp dưới phương X"); var by = r.Bars.FirstOrDefault(b => b.Role == "Lớp dưới phương Y");
        double c = Math.Max(s.CoverFooting, 0.05) * 1000;
        if (bx != null) { w.Line(Lay.Thep, -L / 2 + c, -B / 4, L / 2 - c, -B / 4); w.LeaderTag(L / 6, -B / 4, L / 6 + 300, -B / 2 - 500, Dt.Spaced(bx.Dia, bx.Spacing), bx.Mark); }
        if (by != null) { w.Line(Lay.Thep, -L / 4, -B / 2 + c, -L / 4, B / 2 - c); w.LeaderTag(-L / 4, B / 6, -L / 2 - 300, B / 6 + 300, Dt.Spaced(by.Dia, by.Spacing), by.Mark, -1); }
        var xs = new List<double> { -L / 2 }; xs.AddRange(Piles.Select(p => p.X * 1000).Distinct().OrderBy(x => x)); xs.Add(L / 2);
        var ys = new List<double> { -B / 2 }; ys.AddRange(Piles.Select(p => p.Y * 1000).Distinct().OrderBy(x => x)); ys.Add(B / 2);
        w.DimChainH(xs, -B / 2, -B / 2 - 900);
        w.Dim(-L / 2, -B / 2, L / 2, -B / 2, -1500);
        w.DimChainV(ys, L / 2, L / 2 + 900);
        w.Dim(L / 2, -B / 2, L / 2, B / 2, 1500);
        w.Title(0, -B / 2 - 2100, RebarSchedule.MemberLabel("MẶT BẰNG ĐÀI " + Name, r.Quantity), DrawUtil.ScaleText(S));
        figs.Add(new Figure(w, Name, true));

        // mặt cắt 1-1 phương X
        var x2 = new DxfWriter(S);
        double H = CapH * 1000, em = Embed * 1000;
        x2.Rect(Lay.BeTong, -L / 2, 0, L / 2, H);
        x2.Rect(Lay.KyHieu, -L / 2 - 100, -s.LeanThickness * 1000, L / 2 + 100, 0, "DASHED");
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
            x2.Polyline(Lay.Thep, new List<(double, double)> { (-L / 2 + c, em + c + leg), (-L / 2 + c, em + c), (L / 2 - c, em + c), (L / 2 - c, em + c + leg) }, false);
            x2.LeaderTag(0, em + c, 400, -700, Dt.Spaced(bx.Dia, bx.Spacing), bx.Mark);
        }
        x2.Dim(L / 2, 0, L / 2, H, 600);
        x2.Dim(-L / 2, 0, L / 2, 0, -1600);
        x2.ElevMark(-L / 2 - 300, 0, DxfWriter.ElevText(-Df));
        x2.Title(0, -2300, "1-1", DrawUtil.ScaleText(S), true);
        figs.Add(new Figure(x2, Name + " mặt cắt"));
        return figs;
    }
}
