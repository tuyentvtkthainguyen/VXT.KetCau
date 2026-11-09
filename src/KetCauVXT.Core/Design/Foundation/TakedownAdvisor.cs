using System.Globalization;
using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

// =====================================================================
// [Truyền tải] GỢI Ý MÓNG THEO TẢI TRUYỀN XUỐNG TỪNG CHÂN CỘT (LoadTakedown) – sơ bộ, minh bạch, sát thực tế nhà phố:
//   • Móng đơn: b × b từ Ntc của TỪNG cột, R theo TCVN 9362 (nền tại chân cột: hố khoan gần nhất / nền nhập tay), H theo chọc thủng
//     với Ntt; kiểm tra móng chạm nhau, cột biên sát ranh.
//   • Móng băng: theo dãy cột dọc phương dài nhà; B = ΣNtc/(Lb·(R − γtb·Df)); sườn (dầm móng) b × h sơ bộ.
//   • Móng cọc ép: [P] từ hố khoan (SPT, mũi tự chọn) cho cọc 200/250/300, hoặc [P] nhập tay; n = ⌈k·Ntt/[P]⌉ cho TỪNG cột (k = 1,1 –
//     tải đứng, giằng móng chịu mô men); chọn loại cọc tổng chiều dài × tiết diện nhỏ nhất.
//   • Thứ tự ưu tiên: móng đơn → móng băng → cọc ép; nền yếu (R &lt; 100 kPa, đáy móng trên đất đắp / bùn, lớp yếu dưới đáy) → cọc.
// =====================================================================

public sealed class ColumnFoundationSuggestion
{
    public ColumnTakedown T { get; set; }
    public double R { get; set; }
    /// <summary>Móng đơn vuông: cạnh b, chiều cao H (m)</summary>
    public double B { get; set; }
    public double H { get; set; }
    /// <summary>Móng đơn chạm móng bên cạnh (khe hở &lt; 0,2 m)</summary>
    public bool Overlap { get; set; }
    /// <summary>Số cọc của loại cọc đề xuất</summary>
    public int Piles { get; set; }
    public string Note { get; set; } = "";
}

public sealed class PileSuggestion
{
    public double Size { get; set; }
    public double Capacity { get; set; }
    public double Length { get; set; }
    public double TipDepth { get; set; } = double.NaN;
    public bool FromBorehole { get; set; }
    public string TipLayer { get; set; } = "";
    public List<int> PerColumn { get; } = new();
    public int Total => PerColumn.Sum();
    /// <summary>Khối lượng bê tông cọc (m³)</summary>
    public double Volume => Total * Length * Size * Size;
    public string Name => $"Cọc ép {Size * 1000:0}×{Size * 1000:0}";
}

public sealed class TakedownAdvice
{
    public TakedownResult T { get; set; }
    public List<ColumnFoundationSuggestion> Columns { get; } = new();
    public double Df { get; set; }
    public double Rmin { get; set; }
    public bool WeakSoil { get; set; }
    public string SoilNote { get; set; } = "";
    // móng đơn
    public bool IsoFeasible { get; set; }
    public string IsoReason { get; set; } = "";
    public double IsoConcrete { get; set; }
    // móng băng
    public bool StripFeasible { get; set; }
    public bool StripAlongX { get; set; }
    public string StripReason { get; set; } = "";
    public List<(string Line, int Cols, double Length, double SumNtc, double B, double Hf, double RibB, double RibH)> Strips { get; } = new();
    public double StripConcrete { get; set; }
    // cọc
    public List<PileSuggestion> Piles { get; } = new();
    public PileSuggestion BestPile { get; set; }
    /// <summary>"Isolated" | "Strip" | "Pile"</summary>
    public string Recommended { get; set; } = "";
    public string Explanation { get; set; } = "";
    public List<string> Notes { get; } = new();
    public FoundationType RecommendedType => Recommended == "Strip" ? FoundationType.Strip : Recommended == "Pile" ? FoundationType.Pile : FoundationType.Isolated;
}

public static class TakedownAdvisor
{
    /// <summary>Hệ số kể mô men nhỏ / lệch tâm khi chọn số cọc theo tải đứng</summary>
    public const double PileK = 1.1;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v, string f = "0.##") => double.IsFinite(v) ? v.ToString(f, Inv) : "-";

    public static TakedownAdvice Advise(Project p, AnalysisResults r = null, TakedownOptions opt = null, TakedownResult t = null)
    {
        var adv = new TakedownAdvice { T = t ?? LoadTakedown.Run(p, r, opt) };
        var cols = adv.T.Columns;
        if (p == null || cols.Count == 0) { adv.Explanation = "Chưa có cột / tải để gợi ý móng."; return adv; }
        var soil0 = p.Soil ?? new SoilSettings();
        double Df = Math.Max(soil0.Depth, 0.5);
        adv.Df = Df;
        double gtb = soil0.GammaAvg > 0 ? soil0.GammaAvg : 20;
        var cs = p.Concrete;
        var conc = DesignUtil.ConcreteOf(p, cs?.ConcreteFooting, cs?.ConcreteFooting);
        double rbt = conc?.Rbt > 0 ? conc.Rbt : 900;

        // ---- nền (đại diện + từng chân cột) ----
        double cx = cols.Average(c => c.X), cy = cols.Average(c => c.Y);
        var soilC = GeotechCalc.SoilFor(p, cx, cy, out var bhC);
        var sbh = bhC ?? GeotechCalc.FromSoilSettings(soil0);
        var baseLayer = GeotechCalc.LayerAt(sbh, Df);
        bool badBase = bhC != null && baseLayer != null && (baseLayer.Kind is SoilKind.Fill or SoilKind.Organic || baseLayer.N > 0 && baseLayer.N < 4
                                                           || baseLayer.IsCohesive && baseLayer.IL > 0.75);
        double r1 = FootingDesigner.BearingR(soilC, 1.0);
        adv.SoilNote = bhC != null
            ? $"Nền theo hố khoan {bhC.Name}: lớp đặt móng \"{baseLayer?.Name}\" tại Df = {F(Df)} m, R(b = 1 m) = {F(r1, "0")} kN/m²"
            : $"Nền nhập tay (chưa dùng hố khoan): φ = {F(soil0.Phi, "0.#")}°, c = {F(soil0.C, "0.#")} kPa, γ = {F(soil0.Gamma, "0.#")} kN/m³, Df = {F(Df)} m, R(b = 1 m) = {F(r1, "0")} kN/m²"
              + (string.IsNullOrEmpty(soil0.StdName) ? "" : $" (lớp tiêu chuẩn \"{soil0.StdName}\", bảng tra TCVN 9362)");
        adv.WeakSoil = r1 < 100 || badBase;
        if (r1 < 100) adv.Notes.Add($"R(b = 1 m) = {F(r1, "0")} kN/m² < 100 kN/m² – nền yếu cho móng nông");
        if (badBase) adv.Notes.Add($"Đáy móng nằm trên lớp \"{baseLayer.Name}\" (đất đắp / bùn / đất yếu)");

        // ---- móng đơn từng cột ----
        double rmin = double.MaxValue, maxB = 0;
        foreach (var c in cols)
        {
            var s = GeotechCalc.SoilFor(p, c.X, c.Y, out _);
            var sg = new ColumnFoundationSuggestion { T = c };
            double b = 1.0, R = 0;
            bool ok = true;
            for (int it = 0; it < 40; it++)
            {
                R = FootingDesigner.BearingR(s, b);
                double net = R - gtb * Df;
                if (net <= 5) { ok = false; break; }
                double bn = Math.Max(RcSection.CeilTo(Math.Sqrt(c.Ntc / net), 0.1), FootingDesigner.MinSize);
                if (Math.Abs(bn - b) < 1e-9) break;
                b = bn;
            }
            sg.R = R; sg.B = ok ? b : double.NaN;
            rmin = Math.Min(rmin, R);
            if (ok)
            {
                maxB = Math.Max(maxB, b);
                // chọc thủng sơ bộ: F = Ntt − pnet·(bc + 2h0)(hc + 2h0) ≤ Rbt·2(bc + hc + 2h0)·h0
                double bc = Math.Max(c.Bx, 0.2), hc = Math.Max(c.By, 0.2), H = 0.3;
                for (int it = 0; it < 40; it++)
                {
                    double h0 = H - 0.065;
                    double pn = c.Ntt / (b * b);
                    double Ab = Math.Min(b, bc + 2 * h0) * Math.Min(b, hc + 2 * h0);
                    double Fp = c.Ntt - pn * Ab, Fu = rbt * 2 * (bc + hc + 2 * h0) * h0;
                    if (Fp <= Fu || bc + 2 * h0 >= b) break;
                    H = Math.Round(H + 0.05, 3);
                }
                sg.H = H;
            }
            adv.Columns.Add(sg);
        }
        adv.Rmin = rmin;
        // chạm nhau: các cặp cột kề nhau cùng hàng / cùng cột
        foreach (var a in adv.Columns)
            foreach (var b in adv.Columns)
            {
                if (ReferenceEquals(a, b) || !double.IsFinite(a.B) || !double.IsFinite(b.B)) continue;
                double dx = Math.Abs(a.T.X - b.T.X), dy = Math.Abs(a.T.Y - b.T.Y);
                double gap = Math.Max(dx, dy) - (a.B + b.B) / 2;
                if (Math.Min(dx, dy) < (a.B + b.B) / 2 && gap < 0.2) { a.Overlap = true; a.Note = "chạm móng bên cạnh"; }
            }
        var why = new List<string>();
        if (adv.Columns.Any(c => !double.IsFinite(c.B))) why.Add("nền không đủ chịu lực (R − γtb·Df ≤ 0)");
        if (maxB > 2.6 + 1e-9) why.Add($"móng lớn nhất {F(maxB, "0.0")} m > 2,6 m");
        int nOv = adv.Columns.Count(c => c.Overlap);
        if (nOv > 0) why.Add($"{nOv} móng chạm móng bên cạnh (khe hở < 0,2 m)");
        if (adv.WeakSoil) why.Add("nền yếu");
        var bigC = adv.Columns.Where(c => double.IsFinite(c.B)).OrderByDescending(c => c.B).FirstOrDefault();
        if (bigC != null)
        {
            double pr = bigC.T.Ntc / (bigC.B * bigC.B) + gtb * Df;
            try
            {
                var st = GeotechCalc.Settlement(sbh, bigC.B, bigC.B, Df, pr);
                if (!st.Ok) why.Add($"lún móng lớn nhất {F(st.S * 100, "0.0")} cm > 8 cm");
                else adv.Notes.Add($"Lún móng đơn lớn nhất ({bigC.T.Name}, {F(bigC.B, "0.0")}×{F(bigC.B, "0.0")} m): S = {F(st.S * 100, "0.0")} cm ≤ 8 cm");
                var weak = GeotechCalc.WeakLayerBelow(sbh, Df, bigC.B);
                if (weak != null && bhC != null) why.Add($"có lớp yếu \"{weak.Name}\" ở {F(weak.Top)} m trong phạm vi 2B dưới đáy móng");
            }
            catch (Exception) { }
        }
        adv.IsoFeasible = why.Count == 0;
        adv.IsoReason = adv.IsoFeasible ? $"móng lớn nhất {F(maxB, "0.0")}×{F(maxB, "0.0")} m, các móng không chạm nhau" : string.Join("; ", why);
        adv.IsoConcrete = adv.Columns.Where(c => double.IsFinite(c.B)).Sum(c => c.B * c.B * c.H);

        // ---- móng băng theo dãy cột dọc phương dài nhà ----
        var xs = cols.Select(c => Math.Round(c.X, 1)).Distinct().OrderBy(v => v).ToList();
        var ys = cols.Select(c => Math.Round(c.Y, 1)).Distinct().OrderBy(v => v).ToList();
        double lenX = xs.Count > 1 ? xs[^1] - xs[0] : 0, lenY = ys.Count > 1 ? ys[^1] - ys[0] : 0;
        bool alongX = lenX >= lenY;
        adv.StripAlongX = alongX;
        var lines = cols.GroupBy(c => Math.Round(alongX ? c.Y : c.X, 1)).OrderBy(g => g.Key).ToList();
        var swhy = new List<string>();
        double prevKey = double.NaN, prevB = 0, stripV = 0, maxSB = 0;
        foreach (var g in lines)
        {
            var mem = g.OrderBy(c => alongX ? c.X : c.Y).ToList();
            double ext = mem.Count > 1 ? (alongX ? mem[^1].X - mem[0].X : mem[^1].Y - mem[0].Y) : 0;
            double Lb = ext + 2 * 0.6;
            double sum = mem.Sum(c => c.Ntc);
            var s = GeotechCalc.SoilFor(p, mem.Average(c => c.X), mem.Average(c => c.Y), out _);
            double B = 0.8, R = 0;
            bool ok = true;
            for (int it = 0; it < 40; it++)
            {
                R = FootingDesigner.BearingR(s, B);
                double net = R - gtb * Df;
                if (net <= 5) { ok = false; break; }
                double bn = Math.Max(RcSection.CeilTo(sum / Lb / net, 0.05), 0.6);
                if (Math.Abs(bn - B) < 1e-9) break;
                B = bn;
            }
            if (!ok) { swhy.Add("nền không đủ chịu lực"); break; }
            double span = mem.Count > 1 ? ext / (mem.Count - 1) : 3;
            double bc = mem.Max(c => alongX ? c.By : c.Bx);
            double ribB = Math.Max(0.25, Math.Round(bc + 0.05, 2)), ribH = Math.Max(0.5, RcSection.CeilTo(span / 7, 0.05));
            double hf = Math.Max(0.25, RcSection.CeilTo(0.25 + 0.15 * Math.Max(B - ribB, 0), 0.05));
            adv.Strips.Add(((alongX ? "Dãy y = " : "Dãy x = ") + F(g.Key, "0.0#"), mem.Count, Lb, sum, B, hf, ribB, ribH));
            stripV += B * hf * Lb + ribB * Math.Max(ribH - hf, 0) * Lb;
            maxSB = Math.Max(maxSB, B);
            if (double.IsFinite(prevKey) && g.Key - prevKey - (B + prevB) / 2 < 0.3) swhy.Add($"dải {F(prevKey, "0.0#")} và {F(g.Key, "0.0#")} chạm nhau");
            prevKey = g.Key; prevB = B;
        }
        if (maxSB > 2.2 + 1e-9) swhy.Add($"bề rộng móng băng {F(maxSB)} m > 2,2 m");
        if (adv.WeakSoil) swhy.Add("nền yếu");
        adv.StripFeasible = swhy.Count == 0 && adv.Strips.Count > 0;
        adv.StripReason = adv.StripFeasible ? $"{adv.Strips.Count} dải dọc phương {(alongX ? "X" : "Y")}, B lớn nhất {F(maxSB)} m" : string.Join("; ", swhy.Distinct());
        adv.StripConcrete = stripV;

        // ---- cọc ép ----
        var fs = p.Foundation ?? new FoundationSettings();
        double sf = fs.PileSafetyFactor > 0 ? fs.PileSafetyFactor : 2.5;
        if (bhC != null)
        {
            foreach (double d in new[] { 0.20, 0.25, 0.30 })
            {
                try
                {
                    var notes = new List<string>();
                    double tip = GeotechCalc.AutoTipDepth(bhC, d, Df, notes);
                    var pr = GeotechCalc.PileCapacity(bhC, d, false, tip, Df, false, sf, 0, "B25", cs?.RebarMain ?? "CB400-V");
                    if (!(pr.Allowable > 0)) continue;
                    adv.Piles.Add(new PileSuggestion { Size = d, Capacity = Math.Floor(pr.Allowable / 5) * 5, Length = Math.Round(pr.Length, 1), TipDepth = tip, FromBorehole = true, TipLayer = pr.TipLayer });
                }
                catch (Exception) { }
            }
        }
        if (adv.Piles.Count == 0 && fs.PileCapacity > 0)
            adv.Piles.Add(new PileSuggestion { Size = fs.PileSize, Capacity = fs.PileCapacity, Length = fs.PileLength, FromBorehole = false });
        foreach (var ps in adv.Piles)
            foreach (var c in cols)
            {
                // kể trọng lượng đài + đất trên đài ≈ 1,1·20·Df·n·(3d)²
                int n = Math.Max(1, (int)Math.Ceiling(PileK * c.Ntt / ps.Capacity - 1e-9));
                double gcap = 1.1 * 20 * Df * n * Math.Pow(3 * ps.Size, 2);
                n = Math.Max(n, (int)Math.Ceiling((PileK * c.Ntt + gcap) / ps.Capacity - 1e-9));
                ps.PerColumn.Add(n);
            }
        adv.BestPile = adv.Piles.Where(x => x.PerColumn.All(n => n <= 9)).OrderBy(x => x.Volume).FirstOrDefault() ?? adv.Piles.OrderBy(x => x.Volume).FirstOrDefault();
        if (adv.BestPile != null)
            for (int i = 0; i < cols.Count; i++) adv.Columns[i].Piles = adv.BestPile.PerColumn[i];

        // ---- chọn ----
        if (adv.IsoFeasible) adv.Recommended = "Isolated";
        else if (adv.StripFeasible) adv.Recommended = "Strip";
        else adv.Recommended = "Pile";
        double nmax = cols.Max(c => c.Ntt), ntcMax = cols.Max(c => c.Ntc);
        string loadTxt = $"tải chân cột lớn nhất Ntc = {F(ntcMax, "0")} kN, Ntt = {F(nmax, "0")} kN (truyền tải theo diện chịu tải, tải đứng)";
        adv.Explanation = adv.Recommended switch
        {
            "Isolated" => $"Đề xuất MÓNG ĐƠN BTCT + giằng móng: {loadTxt}; {adv.IsoReason}. Bê tông móng ≈ {F(adv.IsoConcrete, "0.0")} m³.",
            "Strip" => $"Đề xuất MÓNG BĂNG dọc phương {(alongX ? "X" : "Y")} (móng đơn không hợp: {adv.IsoReason}); {loadTxt}; {adv.StripReason}. Bê tông ≈ {F(adv.StripConcrete, "0.0")} m³.",
            _ => adv.BestPile == null
                ? $"Móng nông không phù hợp ({adv.IsoReason}; móng băng: {adv.StripReason}) – cần móng cọc: khai báo [P] cọc hoặc hố khoan để tính số cọc."
                : $"Đề xuất MÓNG CỌC {adv.BestPile.Name}, L ≈ {F(adv.BestPile.Length, "0.0")} m, [P] = {F(adv.BestPile.Capacity, "0")} kN"
                  + (adv.BestPile.FromBorehole ? $" (mũi trong lớp {adv.BestPile.TipLayer})" : " (nhập tay)")
                  + $": {adv.BestPile.Total} cọc cho {cols.Count} chân cột ({string.Join(", ", adv.BestPile.PerColumn.GroupBy(n => n).OrderBy(g => g.Key).Select(g => $"{g.Count()} đài {g.Key} cọc"))}); "
                  + $"móng nông không phù hợp: {adv.IsoReason}."
        };
        if (bhC == null && !soil0.AssumeGoodSoil && string.IsNullOrEmpty(soil0.StdName))
            adv.Notes.Add("CHƯA CÓ KHẢO SÁT ĐỊA CHẤT – gợi ý chỉ để tham khảo; cần hố khoan / thí nghiệm trước khi thi công.");
        if (cols.Count > 0 && (lenX > 0 && lenY > 0) && Math.Min(lenX, lenY) <= 6.5)
            adv.Notes.Add("Nhà phố hẹp: cột biên thường sát ranh đất – móng biên dùng móng lệch tâm có dầm móng cân bằng (Bố trí móng… → dóng mép cột) hoặc đài cọc lùi khỏi ranh.");
        adv.Notes.Add($"Số cọc n = ⌈{F(PileK, "0.0")}·Ntt/[P]⌉ từng chân cột (tải đứng; mô men chân cột do giằng móng chịu). Móng đơn: b = √(Ntc/(R − γtb·Df)), H theo chọc thủng với Ntt (bê tông móng Rbt = {F(rbt, "0")} kPa).");
        return adv;
    }

    /// <summary>Áp dụng phương án: loại móng + (móng đơn) chốt kích thước từng chân cột / (cọc) loại cọc, [P], số cọc từng đài;
    /// tuỳ chọn thiết kế móng theo tải đứng (giằng móng chịu mô men, bỏ tổ hợp gió).</summary>
    public static string Apply(Project p, TakedownAdvice adv, string choice, bool gravityDesign)
    {
        if (p == null || adv == null) return "";
        p.Foundation ??= new FoundationSettings();
        var fs = p.Foundation;
        if (gravityDesign) { fs.BaseMomentFactor = 0; fs.GravityCombosOnly = true; }
        int n = 0;
        switch (choice)
        {
            case "Isolated":
                fs.Type = FoundationType.Isolated;
                // bỏ loại móng / kích thước chốt riêng của từng chân cột → chương trình tự chọn kích thước theo nội lực (tải đứng khi gravityDesign)
                foreach (var c in adv.Columns) { FoundationReset.Clear(p, null, new[] { c.T.Node }, false); n++; }
                return $"Móng đơn cho {n} chân cột (kích thước tự chọn theo nội lực{(gravityDesign ? " tải đứng, giằng móng chịu mô men" : "")}; gợi ý lớn nhất {F(adv.Columns.Where(c => double.IsFinite(c.B)).Select(c => c.B).DefaultIfEmpty(0).Max(), "0.0")} m)";
            case "Strip":
                fs.Type = FoundationType.Strip;
                fs.StripDirection = adv.StripAlongX ? 1 : 2;
                return $"Móng băng dọc phương {(adv.StripAlongX ? "X" : "Y")}";
            default:
                var ps = adv.BestPile;
                fs.Type = FoundationType.Pile;
                if (ps == null) return "Móng cọc (chưa có [P])";
                fs.PileSize = ps.Size; fs.PileRound = false; fs.PileBored = false;
                fs.PileCapacity = ps.Capacity; fs.PileLength = ps.Length;
                if (ps.FromBorehole) { fs.PileCapacityFromBorehole = false; if (double.IsFinite(ps.TipDepth)) fs.PileTipDepth = ps.TipDepth; }
                foreach (var c in adv.Columns) { FoundationReset.Clear(p, null, new[] { c.T.Node }, false); n++; }
                return $"Móng cọc {ps.Name}, [P] = {F(ps.Capacity, "0")} kN, L = {F(ps.Length, "0.0")} m cho {n} chân cột (số cọc tự chọn theo nội lực{(gravityDesign ? " tải đứng" : "")}; gợi ý {ps.Total} cọc)";
        }
    }
}
