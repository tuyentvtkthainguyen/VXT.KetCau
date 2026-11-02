using System.Globalization;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

/// <summary>[CừTràm] + [LùiCọcRanh] Bổ sung cho FoundationAdvisor.Recommend:
/// <list type="bullet">
/// <item>Phương án "Móng nông trên nền gia cố cừ tràm": xét khi lớp đất nông yếu (N trung bình dọc thân cừ &lt; 5 hoặc bùn) và mực nước ngầm cao
/// (đầu cừ nằm dưới MNN thấp nhất); móng vuông b lặp theo [p] của CuTramCalc; không khả thi khi b &gt; 0,5·bước cột, b &gt; 3 m, lún &gt; Sgh,
/// cừ nằm trên MNN, tải chân cột &gt; Nmax kinh nghiệm. Được đề xuất khi móng đơn và móng băng trên nền thiên nhiên đều không khả thi
/// (thứ tự ưu tiên: móng đơn → móng băng → cừ tràm → cọc).</item>
/// <item>Cột sát ranh giới + móng cọc: tim cọc lùi ≥ khoảng lùi ép cọc → nhóm cọc lệch tâm e ≥ s − hc/2; ghi chú dầm móng cân bằng R1 = N·L/(L − e).</item>
/// </list></summary>
public static class GroundAdvisor
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v, string fmt = "0.##") => double.IsFinite(v) ? v.ToString(fmt, Inv) : "-";

    public static void Augment(Project p, FoundationAdvice adv, Borehole bh, SoilSettings soil, double Df, double ntc, double gtb, double sMin)
    {
        if (p == null || adv == null) return;
        CuTramOption(p, adv, bh, soil ?? p.Soil ?? new SoilSettings(), Df, ntc, gtb, sMin);
        SetbackNotes(p, adv, sMin);
    }

    /// <summary>Đánh giá phương án móng nông trên nền cừ tràm (thêm vào cuối danh sách phương án).</summary>
    public static FoundationOption CuTramOption(Project p, FoundationAdvice adv, Borehole bh, SoilSettings soil, double Df, double ntc, double gtb, double sMin)
    {
        var cs = (p.Foundation?.CuTram ?? new CuTramSettings()).Clone();
        var o = new FoundationOption { Choice = FoundationChoice.CuTramFooting, Name = "Móng nông trên nền gia cố cừ tràm" };
        adv.Options.Add(o);
        if (bh == null)
        {
            o.Feasible = false;
            o.Reason = "chưa có hố khoan – không đánh giá được nền yếu và mực nước ngầm (điều kiện bắt buộc của cừ tràm)";
            return o;
        }
        var why = new List<string>();
        double top = Df + Math.Max(cs.LeanThickness, 0), tip = top + cs.Length;
        double nAvg = GeotechCalc.AverageN(bh, top + Math.Max(cs.SandCushion, 0), tip);
        var lay = GeotechCalc.LayerAt(bh, top + Math.Max(cs.SandCushion, 0));
        bool soft = nAvg < CuTramCalc.SoftN || lay?.Kind == SoilKind.Organic;
        double wl = cs.MinWaterDepth >= 0 ? cs.MinWaterDepth : bh.Water;
        bool high = double.IsFinite(wl) && wl <= top + 1e-9;
        o.Details.Add($"Cừ tràm L = {F(cs.Length)} m, {F(cs.Density, "0")} cây/m²; đầu cừ {F(top)} m, mũi {F(tip)} m; N tb dọc thân = {F(nAvg, "0.#")}");
        o.Details.Add($"MNN {(cs.MinWaterDepth >= 0 ? "thấp nhất (nhập)" : "theo hố khoan")} = {(double.IsFinite(wl) ? F(wl) + " m" : "không gặp")}");
        if (!soft) why.Add($"lớp đất nông không yếu (N tb = {F(nAvg, "0.#")} ≥ {F(CuTramCalc.SoftN, "0")}) – đóng cừ khó, ít hiệu quả");
        if (!high) why.Add("đầu cừ nằm trên mực nước ngầm – cừ tràm sẽ mục");
        double b = 1.0;
        CuTramResult r = null;
        bool ok = true;
        for (int it = 0; it < 40; it++)
        {
            r = CuTramCalc.Evaluate(cs, soil, bh, false, b, b, Df);
            double net = r.Allowable - gtb * Df;
            if (net <= 5) { ok = false; break; }
            double bn = Math.Max(RcSection.CeilTo(Math.Sqrt(Math.Max(ntc, 0) / net), 0.1), FootingDesigner.MinSize);
            if (Math.Abs(bn - b) < 1e-9) break;
            b = bn;
        }
        if (!ok) why.Add("[p] − γtb·Df ≤ 0: nền gia cố không đủ chịu lực");
        else
        {
            double pr = ntc / (b * b) + gtb * Df;
            r = CuTramCalc.Evaluate(cs, soil, bh, false, b, b, Df, pr, double.NaN, ntc);
            o.Size = b; o.R = r.Allowable; o.Settlement = r.Settlement?.S ?? double.NaN; o.SettlementInfo = r.Settlement;
            o.Details.Add($"[p]1 (cọc ma sát – nền phức hợp) = {F(r.P1, "0")} kN/m², [p]2 (nền tương đương) = {F(r.P2, "0")} kN/m² → [p] = {F(r.Allowable, "0")} kN/m²");
            o.Details.Add($"Móng {F(b, "0.0")}×{F(b, "0.0")} m, ptb = {F(pr, "0")} kN/m², {r.Count} cây cừ/móng, lún khối quy ước S = {F((r.Settlement?.S ?? double.NaN) * 100, "0.0")} cm");
            if (b > 0.5 * sMin + 1e-9) why.Add($"b = {F(b, "0.0")} m > 0,5·bước cột – móng gần chạm nhau (xem móng băng trên cừ tràm / cọc)");
            if (b > 3.0 + 1e-9) why.Add("móng quá lớn (b > 3 m)");
            if (r.Settlement != null && !r.Settlement.Ok) why.Add($"lún {F(r.Settlement.S * 100, "0.0")} cm > {F(r.Settlement.Limit * 100, "0")} cm");
            if (ntc > cs.MaxColumnLoad + 1e-9) why.Add($"tải chân cột Ntc = {F(ntc, "0")} kN > {F(cs.MaxColumnLoad, "0")} kN (kinh nghiệm) → nên dùng cọc BTCT");
        }
        o.Feasible = why.Count == 0;
        o.Reason = o.Feasible
            ? "Lớp đất nông yếu, mực nước ngầm cao: cừ tràm nén chặt nền, chi phí thấp – phù hợp nhà thấp tầng tải nhỏ (đầu cừ luôn dưới MNN)"
            : string.Join("; ", why);
        // thứ tự ưu tiên: móng đơn → móng băng → cừ tràm → cọc
        if (o.Feasible && adv.Recommended != null && adv.Recommended.Type == FoundationType.Pile)
        {
            adv.Recommended.Recommended = false;
            o.Recommended = true;
            adv.Recommended = o;
            adv.Explanation = $"Đề xuất MÓNG NÔNG {F(o.Size, "0.0")}×{F(o.Size, "0.0")} m TRÊN NỀN GIA CỐ CỪ TRÀM ({F(cs.Density, "0")} cây/m², L = {F(cs.Length)} m): "
                              + $"[p] = {F(o.R, "0")} kN/m², lún {F(o.Settlement * 100, "0.0")} cm; móng nông trên nền thiên nhiên không phù hợp; cọc BTCT là phương án dự phòng khi tải tăng.";
            adv.Notes.Add(adv.Explanation);
        }
        return o;
    }

    /// <summary>Ghi chú lùi cọc khỏi ranh giới cho các phương án cọc khi có ranh giới đất.</summary>
    public static void SetbackNotes(Project p, FoundationAdvice adv, double sMin)
    {
        var lines = EccentricFooting.Lines(p);
        if (lines.Count == 0) return;
        var fs = p.Foundation ?? new FoundationSettings();
        double hc = 0.22;
        try
        {
            var dims = FoundationUtil.BaseColumns(p).Select(c => { var s = p.Section(c.Column.Section); return Math.Min(s.B, s.H); }).Where(v => v > 0).ToList();
            if (dims.Count > 0) hc = dims.Max();
        }
        catch (Exception) { }
        double sb = lines.Max(l => PileSetback.SetbackOf(fs, l.B));
        foreach (var o in adv.Options.Where(x => x.Type == FoundationType.Pile))
        {
            double add = fs.PileSetbackToFace ? o.PileSize / 2 : 0;
            double e = Math.Max(sb + add - hc / 2, 0);
            double L = Math.Max(sMin, 1.0), k = e < L ? L / (L - e) : double.PositiveInfinity;
            o.Details.Add($"Cột sát ranh ({lines.Count} cạnh ranh giới): {(fs.PileSetbackEnabled ? "" : "nếu nhà bên cạnh đã xây (chưa bật lùi cọc), ")}tim cọc lùi ≥ {F(sb * 1000, "0")} mm "
                          + $"→ nhóm cọc lệch tâm e ≥ {F(e, "0.00")} m; dầm móng cân bằng: R1 = N·L/(L − e) ≈ {F(k, "0.00")}·N (L ≈ {F(L)} m)"
                          + (e / L > 0.25 ? " – lệch tâm lớn, nên đài chung với cột trong" : ""));
        }
        if (!fs.PileSetbackEnabled)
            adv.Notes.Add($"Có {lines.Count} cạnh ranh giới đất: nếu nhà giáp ranh đã xây, bật 'Lùi tim cọc khỏi ranh để ép cọc' (≥ {F(sb * 1000, "0")} mm) trong hộp thoại Ranh giới đất – đài biên lệch tâm cần dầm móng cân bằng.");
    }
}
