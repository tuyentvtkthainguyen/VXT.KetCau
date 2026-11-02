using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Chương "THIẾT KẾ CẦU THANG" của thuyết minh (ví dụ tính toán cầu thang đầu tiên + bảng tổng hợp mọi bộ phận).
/// Gọi từ Export (CalculationReport.cs) và Export tự động (CalculationReport.Auto.cs, chương ReportChapters.Stairs).</summary>
public static partial class CalculationReport
{
    private static void StairsSection(Project p, DesignSummary s, DocxWriter d, int n)
    {
        d.Heading($"{n}. THIẾT KẾ CẦU THANG", 1);
        if (s?.Stairs == null || s.Stairs.Count == 0) { d.Paragraph("Không có cầu thang."); return; }
        d.Paragraph("Cầu thang BTCT đổ toàn khối. Bản thang tựa dầm chiếu nghỉ và dầm sàn tính như dải bản nghiêng rộng 1 m, nhịp lấy theo hình chiếu bằng L0, tải theo m² mặt bằng (M = q·L0²/8 tương đương dầm nghiêng chịu tải đứng); " +
                    "bản tựa dầm cốn tính theo phương ngang vế với tải vuông góc q·cos²α. Chiếu nghỉ tính như bản một phương; dầm chiếu nghỉ, dầm cốn tính như dầm đơn giản. " +
                    "Bậc thang xây gạch (hoặc đổ BTCT) và lớp ốp chỉ kể là tải trọng; hoạt tải cầu thang 3,0 kN/m² theo TCVN 2737:2023. Tính cốt thép theo TCVN 5574:2018.");
        var sd = s.Stairs[0];
        var conc = SafeMat(p, p.Concrete.ConcreteSlab);
        d.Heading($"{n}.1. Ví dụ tính toán: cầu thang {sd.Name} (tầng {sd.Story})", 2);
        d.Paragraph($"{sd.TypeText}; {sd.SupportText}. Chiều cao tầng H = {N(sd.Height)} m, {sd.Risers} bậc h = {Mm(sd.Riser)} mm, b = {Mm(sd.Tread)} mm; " +
                    $"bề rộng vế {Mm(sd.FlightWidth)} mm; bản thang dày {Mm(sd.WaistT)} mm, chiếu nghỉ dày {Mm(sd.LandingT)} mm.");
        d.Equation($"Kiểm tra bậc: 2h + b = 2×{Mm(sd.Riser)} + {Mm(sd.Tread)} = {Mm(sd.Check2hb)} mm {(sd.Check2hb >= 0.6 - 1e-6 && sd.Check2hb <= 0.65 + 1e-6 ? "∈ [600; 650] – đạt" : "∉ [600; 650] – cần điều chỉnh")};  cosα = b/√(b² + h²) = {N(sd.CosA, "0.000")}");
        if (sd.Loads != null)
        {
            d.Paragraph("Tải trọng vế thang (trên 1 m² mặt bằng):", true);
            d.Table(new[] { "Lớp", "Dày (mm)", "γ (kN/m³)", "Quy đổi", "gtc (kN/m²)", "γf", "gtt (kN/m²)" },
                sd.Loads.Flight.Select(r => new[] { r.Name, r.Thickness > 0 ? Mm(r.Thickness) : "", r.Gamma > 0 ? N(r.Gamma, "0.#") : "", r.Factor, N(r.Gk, "0.000"), N(r.Gf, "0.0#"), N(r.G, "0.000") })
                    .Concat(new[] { new[] { "Tổng", "", "", "", N(sd.Loads.FlightStd, "0.000"), "", N(sd.Loads.FlightDesign, "0.000") } }),
                new[] { 4.6, 1.5, 1.6, 2.6, 1.9, 1.0, 1.9 }, 10);
            if (sd.Loads.Landing.Count > 0)
            {
                d.Paragraph("Tải trọng chiếu nghỉ:", true);
                d.Table(new[] { "Lớp", "Dày (mm)", "γ (kN/m³)", "gtc (kN/m²)", "γf", "gtt (kN/m²)" },
                    sd.Loads.Landing.Select(r => new[] { r.Name, r.Thickness > 0 ? Mm(r.Thickness) : "", r.Gamma > 0 ? N(r.Gamma, "0.#") : "", N(r.Gk, "0.000"), N(r.Gf, "0.0#"), N(r.G, "0.000") })
                        .Concat(new[] { new[] { "Tổng", "", "", N(sd.Loads.LandingStd, "0.000"), "", N(sd.Loads.LandingDesign, "0.000") } }),
                    new[] { 5.0, 1.6, 1.8, 2.2, 1.2, 2.2 }, 10);
            }
        }
        var slab = sd.Parts.FirstOrDefault(x => x.Kind == "Bản thang");
        if (slab != null && conc != null && conc.Rb > 0)
        {
            d.Paragraph($"Bản thang {slab.Name}: nhịp L0 = {N(slab.Span, "0.000")} m, q = {N(slab.Q)} kN/m².", true);
            var stl = SafeMat(p, p.Concrete.RebarStirrup);
            int dia = ParseDia(slab.MainText);
            if (dia >= 10) stl = SafeMat(p, p.Concrete.RebarMain);
            FlexureLines(d, slab.MomentFormula, slab.M, 1.0, slab.H0, conc.Rb, stl?.Rs ?? 0, slab.AsReq, slab.AsProv, slab.MainText, null);
            d.Equation($"Thép mũ tại gối: M = q·L0²/{(sd.Stair?.PartialFixity == true ? "16" : "24")} = {N(slab.MSupport)} kN.m/m → {slab.TopText}; thép phân bố {slab.DistText}");
            d.Equation($"Cắt: Q = q·L0/2 = {N(slab.V)} kN/m ≤ Qb = {N(slab.Checks.FirstOrDefault(c => c.Name.Contains("cắt"))?.Capacity ?? 0)} kN/m → {(slab.Checks.All(c => c.Ok) ? "đạt" : "KHÔNG ĐẠT")}");
        }
        foreach (var bm in sd.Parts.Where(x => x.Kind is "Dầm chiếu nghỉ" or "Dầm cốn").Take(2))
        {
            d.Paragraph($"{bm.Kind} {bm.Name} ({Mm(bm.B)}×{Mm(bm.H)} mm): nhịp L = {N(bm.Span, "0.000")} m, q = {N(bm.Q)} kN/m; {bm.MomentFormula} = {N(bm.M)} kN.m; Q = {N(bm.V)} kN.", true);
            d.Equation($"As yêu cầu = {Cm2(bm.AsReq)} cm² → thép dưới {bm.MainText} ({Cm2(bm.AsProv)} cm²); thép trên {bm.TopText}; đai {bm.StirrupText} → {(bm.Ok ? "đạt" : "KHÔNG ĐẠT")}");
        }
        d.Heading($"{n}.2. Bảng tổng hợp thiết kế cầu thang", 2);
        d.Table(new[] { "Bộ phận", "SL", "L0 (m)", "b×h (mm)", "q", "M (kN.m)", "Q (kN)", "Thép chịu lực", "Thép mũ / trên", "Phân bố / đai", "KL" },
            s.Stairs.SelectMany(x => x.Parts).Select(x => new[]
            {
                x.Name, x.Quantity.ToString(Inv), N(x.Span), x.Kind.StartsWith("Dầm") ? $"{Mm(x.B)}×{Mm(x.H)}" : $"1000×{Mm(x.H)}", N(x.Q), N(x.M), N(x.V),
                x.MainText, x.TopText, x.Kind.StartsWith("Dầm") ? x.StirrupText : x.DistText, OkText(x.Ok)
            }), new[] { 2.2, 0.7, 1.1, 1.6, 1.1, 1.2, 1.1, 1.7, 1.7, 1.7, 1.5 }, 9);
        d.Paragraph("q: kN/m² với bản, kN/m với dầm; mô men, lực cắt của bản tính trên 1 m bề rộng.", false, true);
        foreach (var m in s.Stairs.SelectMany(x => x.Messages).Distinct().Take(8)) d.Paragraph("Lưu ý: " + m, false, true);
    }

    private static int ParseDia(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int i = text.IndexOf('Ø');
        if (i < 0) return 0;
        int j = i + 1, v = 0;
        while (j < text.Length && char.IsDigit(text[j])) { v = v * 10 + (text[j] - '0'); j++; }
        return v;
    }
}
