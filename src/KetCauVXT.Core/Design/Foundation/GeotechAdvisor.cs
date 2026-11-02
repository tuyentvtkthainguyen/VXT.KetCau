using System.Globalization;
using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

/// <summary>Phương án móng được xét.</summary>
public enum FoundationChoice { Isolated, Strip, DrivenPile, BoredPile }

/// <summary>Một phương án móng trong bảng gợi ý.</summary>
public sealed class FoundationOption
{
    public FoundationChoice Choice { get; set; }
    public string Name { get; set; } = "";
    public bool Feasible { get; set; }
    public bool Recommended { get; set; }
    /// <summary>Lý do khả thi / không khả thi (tiếng Việt, nối bằng "; ")</summary>
    public string Reason { get; set; } = "";
    /// <summary>Các số liệu chính (mỗi dòng "tên: giá trị")</summary>
    public List<string> Details { get; set; } = new();
    /// <summary>Sức chịu tải tiêu chuẩn nền R tại Df (kN/m²) – móng nông</summary>
    public double R { get; set; } = double.NaN;
    /// <summary>Cạnh móng đơn vuông / bề rộng móng băng (m)</summary>
    public double Size { get; set; } = double.NaN;
    /// <summary>Độ lún ước tính (m)</summary>
    public double Settlement { get; set; } = double.NaN;
    /// <summary>Móng băng: true = dải dọc trục X</summary>
    public bool StripAlongX { get; set; }
    // ---- cọc ----
    public double PileSize { get; set; }
    public bool PileRound { get; set; }
    public bool Bored { get; set; }
    public double TipDepth { get; set; } = double.NaN;
    public double PileLength { get; set; } = double.NaN;
    public double PileCapacity { get; set; } = double.NaN;
    public int PilesPerColumn { get; set; }
    public PileCapacityResult Pile { get; set; }
    public SettlementResult SettlementInfo { get; set; }
    /// <summary>true = [P] tính từ hố khoan (false = lấy [P] nhập tay)</summary>
    public bool FromBorehole { get; set; }

    public FoundationType Type => Choice switch
    {
        FoundationChoice.Isolated => FoundationType.Isolated,
        FoundationChoice.Strip => FoundationType.Strip,
        _ => FoundationType.Pile
    };

    /// <summary>Áp dụng phương án vào dự án: Foundation.Type và (móng cọc) kích thước, loại cọc, mũi cọc, chiều dài, [P]; (móng băng) phương dải.</summary>
    public void ApplyTo(Project p)
    {
        if (p == null) return;
        p.Foundation ??= new FoundationSettings();
        var fs = p.Foundation;
        fs.Type = Type;
        if (Choice == FoundationChoice.Strip) fs.StripDirection = StripAlongX ? 1 : 2;
        if (Type != FoundationType.Pile) return;
        fs.PileSize = PileSize; fs.PileRound = PileRound; fs.PileBored = Bored;
        if (double.IsFinite(PileLength) && PileLength > 0) fs.PileLength = Math.Round(PileLength, 2);
        if (double.IsFinite(PileCapacity) && PileCapacity > 0) fs.PileCapacity = Math.Max(10, Math.Floor(PileCapacity / 5) * 5);
        if (FromBorehole)
        {
            fs.PileCapacityFromBorehole = true;
            if (double.IsFinite(TipDepth)) fs.PileTipDepth = Math.Round(TipDepth, 2);
        }
    }
}

/// <summary>Kết quả gợi ý phương án móng.</summary>
public sealed class FoundationAdvice
{
    public List<FoundationOption> Options { get; set; } = new();
    public FoundationOption Recommended { get; set; }
    /// <summary>Giải thích chung + lý do chọn</summary>
    public List<string> Notes { get; set; } = new();
    public string Explanation { get; set; } = "";
    public string Borehole { get; set; } = "";
    /// <summary>Lực nén chân cột lớn nhất – tính toán và tiêu chuẩn (kN)</summary>
    public double NMax { get; set; }
    public double NtcMax { get; set; }
    public bool LoadsFromAnalysis { get; set; }
    /// <summary>Bước cột nhỏ nhất theo X, Y (m)</summary>
    public double SpacingX { get; set; }
    public double SpacingY { get; set; }
    public double Df { get; set; }
    public int Columns { get; set; }
}

/// <summary>Gợi ý phương án móng (móng đơn, móng băng, cọc ép/đóng, cọc khoan nhồi) từ hố khoan và tải chân cột.
/// <list type="bullet">
/// <item>Tải chân cột: N lớn nhất từ kết quả phân tích (tổ hợp TTGH I); chưa phân tích → ước tính N = q·Atb·số tầng, q = 12 kN/m²/sàn (tính toán), Atb = diện tích truyền tải theo lưới cột. Ntc = N/LoadFactorAvg.</item>
/// <item>Móng đơn vuông b: b = √(Ntc/(R(b) − γtb·Df)) lặp, làm tròn 0,1 m, ≥ 0,8 m; không khả thi khi b &gt; 0,5·bước cột nhỏ nhất (→ móng băng), b &gt; 4 m, lún S &gt; 8 cm, đáy móng trên đất đắp / đất yếu, hoặc có lớp yếu (N &lt; 4, bùn) trong phạm vi 2B dưới đáy móng.</item>
/// <item>Móng băng: dải dọc X: q = Ntc/sx, B = q/(R(B) − γtb·Df) (làm tròn 0,05 m, ≥ 0,6 m), cần B ≤ 0,4·sy; tương tự dọc Y; cả hai phương B &gt; 0,4·bước → móng bè / cọc. Lún tính với L = max(chiều dài dãy cột + 1,2 m, 5B).</item>
/// <item>Cọc ép/đóng (vuông 250/300/350/400 theo N ≤ 500/1200/2500/&gt;): mũi tự chọn, [P] theo SPT; số cọc n = ⌈1,2·Nmax/[P]⌉; khả thi khi n ≤ 9.
/// Cọc khoan nhồi D600/800/1000 (N ≤ 3000/6000/&gt;): như trên với qb = 150·Np.</item>
/// <item>Thứ tự ưu tiên: móng đơn → móng băng → cọc ép → cọc khoan nhồi (phương án khả thi đầu tiên).</item>
/// </list></summary>
public static class FoundationAdvisor
{
    /// <summary>Tải sàn ước tính (tính toán) khi chưa phân tích, kN/m² mỗi sàn</summary>
    public const double FloorLoad = 12;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v, string fmt = "0.##") => double.IsFinite(v) ? v.ToString(fmt, Inv) : "-";

    public static FoundationAdvice Recommend(Project p, AnalysisResults r, Borehole bh)
    {
        var adv = new FoundationAdvice();
        if (p == null) return adv;
        var soil0 = p.Soil ?? new SoilSettings();
        double Df = Math.Max(soil0.Depth, 0);
        double kt = Math.Max(soil0.LoadFactorAvg, 1.0), gtb = soil0.GammaAvg;
        adv.Df = Df; adv.Borehole = bh?.Name ?? "";

        // ---- vị trí cột, bước cột ----
        var cols = new List<(double X, double Y, int Node)>();
        try { cols.AddRange(FoundationUtil.BaseColumns(p).Select(c => (c.Node.X, c.Node.Y, c.Node.Id))); }
        catch (Exception) { }
        if (cols.Count == 0)
            foreach (var gx in p.GridsX) foreach (var gy in p.GridsY) cols.Add((gx.Coord, gy.Coord, 0));
        adv.Columns = cols.Count;
        var xs = cols.Select(c => Math.Round(c.X, 2)).Distinct().OrderBy(v => v).ToList();
        var ys = cols.Select(c => Math.Round(c.Y, 2)).Distinct().OrderBy(v => v).ToList();
        double MinStep(List<double> v)
        {
            double m = double.MaxValue;
            for (int i = 0; i + 1 < v.Count; i++) if (v[i + 1] - v[i] > 0.5) m = Math.Min(m, v[i + 1] - v[i]);
            return m == double.MaxValue ? 4.0 : m;
        }
        adv.SpacingX = MinStep(xs); adv.SpacingY = MinStep(ys);
        double sMin = Math.Min(adv.SpacingX, adv.SpacingY);

        // ---- tải chân cột ----
        double nmax = 0;
        if (r != null && cols.Any(c => c.Node > 0))
        {
            var combos = r.DesignCombos();
            foreach (var c in cols.Where(q => q.Node > 0))
            {
                var loads = FoundationUtil.Loads(r, c.Node, combos);
                if (loads.Count > 0) nmax = Math.Max(nmax, loads.Max(l => l.N));
            }
            adv.LoadsFromAnalysis = nmax > 0;
        }
        if (nmax <= 0)
        {
            int floors = Math.Max(p.Stories?.Count ?? 0, 1);
            double Trib(List<double> v, double x)
            {
                if (v.Count < 2) return 3.0;
                int i = v.FindIndex(q => Math.Abs(q - Math.Round(x, 2)) < 1e-6);
                if (i < 0) return 3.0;
                double t = 0;
                if (i > 0) t += (v[i] - v[i - 1]) / 2;
                if (i + 1 < v.Count) t += (v[i + 1] - v[i]) / 2;
                return t;
            }
            foreach (var c in cols) nmax = Math.Max(nmax, FloorLoad * Trib(xs, c.X) * Trib(ys, c.Y) * floors);
            if (cols.Count == 0) nmax = FloorLoad * 16 * floors;
            adv.Notes.Add($"Chưa có kết quả phân tích – tải chân cột ước tính N = {F(FloorLoad, "0")} kN/m²/sàn × diện tích truyền tải × {floors} sàn = {F(nmax, "0")} kN (cột chịu tải lớn nhất)");
        }
        else adv.Notes.Add($"Tải chân cột lớn nhất theo kết quả phân tích (TTGH I): Nmax = {F(nmax, "0")} kN");
        adv.NMax = nmax; adv.NtcMax = nmax / kt;
        double ntc = adv.NtcMax;
        adv.Notes.Add($"Ntc = Nmax/{F(kt)} = {F(ntc, "0")} kN; bước cột nhỏ nhất sx = {F(adv.SpacingX)} m, sy = {F(adv.SpacingY)} m; Df = {F(Df)} m");

        // ---- nền cho móng nông ----
        SoilSettings soil;
        Borehole sbh;
        if (bh != null)
        {
            soil = GeotechCalc.CopySoil(soil0);
            adv.Notes.AddRange(GeotechCalc.ApplyToSoil(soil, bh, Df));
            sbh = bh;
        }
        else
        {
            soil = soil0;
            sbh = GeotechCalc.FromSoilSettings(soil0);
            adv.Notes.Add(soil0.AssumeGoodSoil
                ? $"Chưa có hố khoan – dùng nền đất tốt giả định / chỉ tiêu nhập tay: φ = {F(soil0.Phi, "0.#")}°, c = {F(soil0.C, "0.#")} kPa, γ = {F(soil0.Gamma, "0.#")} kN/m³, E = {F(soil0.E, "0.#")} MPa"
                : "CHƯA CÓ KHẢO SÁT ĐỊA CHẤT – gợi ý chỉ mang tính tham khảo, cần khoan khảo sát trước khi chọn phương án móng");
        }
        var baseLayer = GeotechCalc.LayerAt(sbh, Df);
        bool badBase = bh != null && baseLayer != null && (baseLayer.Kind == SoilKind.Fill || baseLayer.Kind == SoilKind.Organic || baseLayer.N < 4);

        // ---- móng đơn ----
        var iso = new FoundationOption { Choice = FoundationChoice.Isolated, Name = "Móng đơn" };
        {
            var why = new List<string>();
            double b = 1.0, R = 0, net = 0;
            bool ok = true;
            for (int it = 0; it < 40; it++)
            {
                R = FootingDesigner.BearingR(soil, b);
                net = R - gtb * Df;
                if (net <= 5) { ok = false; break; }
                double bn = Math.Max(RcSection.CeilTo(Math.Sqrt(ntc / net), 0.1), FootingDesigner.MinSize);
                if (Math.Abs(bn - b) < 1e-9) break;
                b = bn;
            }
            iso.R = R; iso.Size = b;
            iso.Details.Add($"R(b = {F(b, "0.0")} m) = {F(R, "0")} kN/m²");
            if (!ok) why.Add("R − γtb·Df ≤ 0: nền không đủ chịu lực");
            else
            {
                double pr = ntc / (b * b) + gtb * Df;
                var st = GeotechCalc.Settlement(sbh, b, b, Df, pr);
                iso.Settlement = st.S; iso.SettlementInfo = st;
                iso.Details.Add($"Móng {F(b, "0.0")}×{F(b, "0.0")} m, ptb = {F(pr, "0")} kN/m²");
                iso.Details.Add($"Lún S = {F(st.S * 100, "0.0")} cm (Sgh = 8 cm)");
                iso.Details.Add($"b/bước cột = {F(b / sMin, "0.00")} (≤ 0,5)");
                if (b > 0.5 * sMin + 1e-9) why.Add($"b = {F(b, "0.0")} m > 0,5·bước cột = {F(0.5 * sMin, "0.0")} m → nên chuyển móng băng");
                if (b > 4.0 + 1e-9) why.Add("móng quá lớn (b > 4 m)");
                if (!st.Ok) why.Add($"lún {F(st.S * 100, "0.0")} cm > 8 cm");
                var weak = GeotechCalc.WeakLayerBelow(sbh, Df, b);
                if (weak != null) why.Add($"có lớp yếu \"{weak.Name}\" (N = {F(weak.N, "0.#")}) ở {F(weak.Top)} m, trong phạm vi 2B = {F(2 * b, "0.0")} m dưới đáy móng → nên dùng cọc");
            }
            if (badBase) why.Add($"đáy móng nằm trên lớp \"{baseLayer.Name}\" (đất đắp / đất yếu)");
            iso.Feasible = why.Count == 0;
            iso.Reason = iso.Feasible ? "Nền đủ chịu lực, lún nhỏ, móng không chạm nhau – phương án kinh tế nhất" : string.Join("; ", why);
        }
        adv.Options.Add(iso);

        // ---- móng băng ----
        var strip = new FoundationOption { Choice = FoundationChoice.Strip, Name = "Móng băng" };
        {
            var why = new List<string>();
            (double B, double R, bool ok) Width(double along)
            {
                double q = ntc / Math.Max(along, 0.5);
                double B = 1.0, R = 0;
                for (int it = 0; it < 40; it++)
                {
                    R = FootingDesigner.BearingR(soil, B);
                    double net = R - gtb * Df;
                    if (net <= 5) return (B, R, false);
                    double bn = Math.Max(RcSection.CeilTo(q / net, 0.05), 0.6);
                    if (Math.Abs(bn - B) < 1e-9) break;
                    B = bn;
                }
                return (B, R, true);
            }
            var wx = Width(adv.SpacingX);   // dải dọc X, cột cách nhau sx, các dải cách nhau sy
            var wy = Width(adv.SpacingY);
            bool okX = wx.ok && wx.B <= 0.4 * adv.SpacingY + 1e-9, okY = wy.ok && wy.B <= 0.4 * adv.SpacingX + 1e-9;
            strip.Details.Add($"Dải dọc X: B = {F(wx.B)} m ({(okX ? "≤" : ">")} 0,4·sy = {F(0.4 * adv.SpacingY)} m)");
            strip.Details.Add($"Dải dọc Y: B = {F(wy.B)} m ({(okY ? "≤" : ">")} 0,4·sx = {F(0.4 * adv.SpacingX)} m)");
            bool alongX = okX && (!okY || wx.B / adv.SpacingY <= wy.B / adv.SpacingX);
            if (!okX && !okY) alongX = wx.B / adv.SpacingY <= wy.B / adv.SpacingX;
            var w = alongX ? wx : wy;
            strip.StripAlongX = alongX; strip.Size = w.B; strip.R = w.R;
            if (!wx.ok && !wy.ok) why.Add("R − γtb·Df ≤ 0: nền không đủ chịu lực");
            else
            {
                if (!okX && !okY) why.Add("bề rộng móng băng > 0,4·bước cột cả hai phương → xem xét móng bè hoặc móng cọc");
                double ext = alongX ? (xs.Count > 1 ? xs[^1] - xs[0] : 4) : (ys.Count > 1 ? ys[^1] - ys[0] : 4);
                double Ls = Math.Max(ext + 1.2, 5 * w.B);
                double q = ntc / Math.Max(alongX ? adv.SpacingX : adv.SpacingY, 0.5);
                double pr = q / w.B + gtb * Df;
                var st = GeotechCalc.Settlement(sbh, Ls, w.B, Df, pr);
                strip.Settlement = st.S; strip.SettlementInfo = st;
                strip.Details.Add($"Chọn dải dọc {(alongX ? "X" : "Y")}: B = {F(w.B)} m, R = {F(w.R, "0")} kN/m², ptb = {F(pr, "0")} kN/m²");
                strip.Details.Add($"Lún S = {F(st.S * 100, "0.0")} cm (Sgh = 8 cm)");
                if (!st.Ok) why.Add($"lún {F(st.S * 100, "0.0")} cm > 8 cm");
                var weak = GeotechCalc.WeakLayerBelow(sbh, Df, w.B);
                if (weak != null) why.Add($"có lớp yếu \"{weak.Name}\" (N = {F(weak.N, "0.#")}) ở {F(weak.Top)} m, trong phạm vi 2B = {F(2 * w.B)} m dưới đáy móng → nên dùng cọc");
            }
            if (badBase) why.Add($"đáy móng nằm trên lớp \"{baseLayer.Name}\" (đất đắp / đất yếu)");
            strip.Feasible = why.Count == 0;
            strip.Reason = strip.Feasible ? "Móng băng đủ chịu lực, phân bố tải đều, giảm lún lệch giữa các cột" : string.Join("; ", why);
        }
        adv.Options.Add(strip);

        // ---- cọc ----
        var fs0 = p.Foundation ?? new FoundationSettings();
        FoundationOption Pile(bool bored)
        {
            var o = new FoundationOption
            {
                Choice = bored ? FoundationChoice.BoredPile : FoundationChoice.DrivenPile, Name = bored ? "Cọc khoan nhồi" : "Móng cọc ép / đóng",
                Bored = bored, PileRound = bored
            };
            double d = bored ? (nmax <= 3000 ? 0.6 : nmax <= 6000 ? 0.8 : 1.0)
                             : (nmax <= 500 ? 0.25 : nmax <= 1200 ? 0.30 : nmax <= 2500 ? 0.35 : 0.40);
            o.PileSize = d;
            var why = new List<string>();
            double P, tip;
            if (bh != null)
            {
                var notes = new List<string>();
                tip = GeotechCalc.AutoTipDepth(bh, d, Df, notes);
                var pr = GeotechCalc.PileCapacity(bh, d, bored, tip, Df, bored, fs0.PileSafetyFactor > 0 ? fs0.PileSafetyFactor : 2.5, 0, "B25", p.Concrete?.RebarMain ?? "CB400-V");
                pr.Notes.InsertRange(0, notes);
                o.Pile = pr; o.FromBorehole = true;
                P = pr.Allowable;
                o.Details.Add($"Mũi cọc {F(tip)} m trong lớp {pr.TipLayer}");
                o.Details.Add($"Rc,u = {F(pr.Rcu, "0")} kN, [P]đn = {F(pr.Pdn, "0")} kN, Pvl = {F(pr.Pvl, "0")} kN");
            }
            else
            {
                if (bored) { o.Feasible = false; o.Reason = "chưa có hố khoan – không đánh giá được sức chịu tải cọc khoan nhồi"; o.Size = d; return o; }
                d = fs0.PileSize; o.PileSize = d; o.PileRound = fs0.PileRound;
                tip = Df + fs0.PileLength; P = fs0.PileCapacity;
                o.Details.Add($"Chưa có hố khoan – dùng [P] nhập tay = {F(P, "0")} kN, L = {F(fs0.PileLength)} m");
                why.Add("cần khảo sát địa chất để xác định chiều dài và [P] của cọc");
            }
            o.TipDepth = tip; o.PileLength = tip - Df; o.PileCapacity = P;
            int n = P > 0 ? Math.Max(1, (int)Math.Ceiling(1.2 * nmax / P - 1e-9)) : 99;
            o.PilesPerColumn = n;
            string shape = bored ? "D" + F(d * 1000, "0") : F(d * 1000, "0") + "×" + F(d * 1000, "0");
            o.Details.Insert(0, $"Cọc {shape} mm, L = {F(o.PileLength, "0.0")} m, [P] = {F(P, "0")} kN");
            o.Details.Add($"Số cọc / cột n = ⌈1,2·{F(nmax, "0")}/{F(P, "0")}⌉ = {n}");
            if (P <= 0) why.Add("[P] ≤ 0");
            if (o.PileLength < 3) why.Add("cọc quá ngắn (< 3 m) – lớp tốt nằm nông, nên dùng móng nông");
            if (!bored && n > 9) why.Add($"cần {n} cọc/cột (> 9) → nên dùng cọc khoan nhồi đường kính lớn");
            if (!bored && o.PileLength > 30) o.Details.Add("Cọc dài > 30 m: nhiều đoạn nối, cần thiết bị ép lớn");
            o.Feasible = why.Count == 0 || (bh == null && !bored && why.Count == 1);
            o.Reason = why.Count == 0
                ? (bored ? "Sức chịu tải lớn, xuyên qua lớp yếu; chi phí và yêu cầu thi công cao – hợp với nhà nhiều tầng, tải lớn"
                         : "Cọc xuyên qua lớp yếu, tựa vào lớp tốt; thi công phổ biến cho nhà phố")
                : string.Join("; ", why);
            return o;
        }
        adv.Options.Add(Pile(false));
        adv.Options.Add(Pile(true));

        // ---- chọn ----
        var drv = adv.Options[2]; var bor = adv.Options[3];
        FoundationOption best = iso.Feasible ? iso : strip.Feasible ? strip : drv.Feasible ? drv : bor.Feasible ? bor : drv;
        best.Recommended = true;
        adv.Recommended = best;
        string pre = bh == null && !soil0.AssumeGoodSoil ? "(Tham khảo – chưa có khảo sát) " : "";
        adv.Explanation = pre + (best.Choice switch
        {
            FoundationChoice.Isolated => $"Đề xuất MÓNG ĐƠN {F(iso.Size, "0.0")}×{F(iso.Size, "0.0")} m trên nền thiên nhiên: R = {F(iso.R, "0")} kN/m², lún {F(iso.Settlement * 100, "0.0")} cm ≤ 8 cm, móng không chạm nhau.",
            FoundationChoice.Strip => $"Đề xuất MÓNG BĂNG dọc trục {(strip.StripAlongX ? "X" : "Y")}, B ≈ {F(strip.Size)} m: móng đơn không phù hợp ({iso.Reason}).",
            FoundationChoice.DrivenPile => $"Đề xuất MÓNG CỌC ÉP {F(drv.PileSize * 1000, "0")}×{F(drv.PileSize * 1000, "0")} mm, L ≈ {F(drv.PileLength, "0.0")} m, [P] ≈ {F(drv.PileCapacity, "0")} kN, {drv.PilesPerColumn} cọc/cột: móng nông không phù hợp ({(iso.Feasible ? "" : iso.Reason)}{(strip.Feasible ? "" : "; móng băng: " + strip.Reason)}).",
            _ => $"Đề xuất CỌC KHOAN NHỒI D{F(bor.PileSize * 1000, "0")}, L ≈ {F(bor.PileLength, "0.0")} m, [P] ≈ {F(bor.PileCapacity, "0")} kN, {bor.PilesPerColumn} cọc/cột: {drv.Reason}."
        });
        adv.Notes.Add(adv.Explanation);
        adv.Notes.Add("Số liệu chỉ để chọn phương án sơ bộ – thiết kế chi tiết theo kết quả khảo sát địa chất, thí nghiệm nén tĩnh cọc và tính toán đầy đủ.");
        return adv;
    }
}
