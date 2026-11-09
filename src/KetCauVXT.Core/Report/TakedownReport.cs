using System.Globalization;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>[Truyền tải] Thuyết minh "Truyền tải trọng xuống chân cột và gợi ý phương án móng" (Word).</summary>
public static class TakedownReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string N(double v, string f = "0.#") => double.IsFinite(v) ? v.ToString(f, Inv) : "-";

    public static DocxWriter Build(Project p, TakedownAdvice adv)
    {
        var d = new DocxWriter();
        var t = adv.T;
        d.Title("TRUYỀN TẢI TRỌNG XUỐNG CHÂN CỘT VÀ GỢI Ý PHƯƠNG ÁN MÓNG");
        d.Paragraph("Công trình: " + (p?.Name ?? ""), true);
        d.Heading("1. Phương pháp", 1);
        d.Paragraph("Tải trọng lấy theo mô hình đã chất (trọng lượng bản thân sàn, dầm, cột; lớp hoàn thiện; tường; cầu thang; hoạt tải). "
                    + "Mỗi phần tải dồn về cột gần nhất đỡ sàn (diện chịu tải), cộng dồn từ mái xuống chân cột. Ntc = G + ψ·P; Ntt = Σγf·tải.");
        foreach (var n in t.Notes) d.Bullet(n);
        foreach (var w in t.Warnings) d.Bullet("Lưu ý: " + w);
        d.Heading("2. Tải trọng từng sàn", 1);
        var srcs = t.Stories.SelectMany(s => s.Sources.Keys).Distinct().ToList();
        d.Table(new[] { "Sàn", "Cao độ (m)", "Diện tích (m²)", "G (kN)", "P (kN)", "(G+P)/m²" }.Concat(srcs.Select(s => s + " (kN)")).ToArray(),
            t.Stories.Select(s => new[] { s.Story, N(s.Elevation, "0.00"), N(s.FloorArea), N(s.G, "0"), N(s.P, "0"), N(s.Intensity, "0.0") }
                .Concat(srcs.Select(k => N(s.Sources.TryGetValue(k, out var v) ? v : 0, "0"))).ToArray()), null, 8);
        d.Heading("3. Tải trọng chân cột", 1);
        d.Table(new[] { "Cột", "Diện chịu tải (m²)", "Số sàn HT", "G (kN)", "P (kN)", "ψ", "Ntc (kN)", "Ntt (kN)", "Khung Ntt max (kN)" },
            t.Columns.Select(c => new[] { c.Name, N(c.Area), c.LiveFloors.ToString(Inv), N(c.G, "0"), N(c.P, "0"), N(c.Psi, "0.00"), N(c.Ntc, "0"), N(c.Ntt, "0"), N(c.FeNtt, "0") }), null, 9);
        d.Paragraph($"Tổng: G = {N(t.TotalG, "0")} kN, P = {N(t.TotalP, "0")} kN, ΣNtc = {N(t.TotalNtc, "0")} kN, ΣNtt = {N(t.TotalNtt, "0")} kN.");
        d.Heading("4. Gợi ý phương án móng", 1);
        d.Paragraph(adv.SoilNote);
        d.Paragraph(adv.Explanation, true);
        foreach (var n in adv.Notes) d.Bullet(n);
        d.Heading("4.1. Móng đơn", 2);
        d.Paragraph((adv.IsoFeasible ? "Khả thi: " : "Không phù hợp: ") + adv.IsoReason);
        d.Table(new[] { "Cột", "Ntc (kN)", "Ntt (kN)", "R (kN/m²)", "b × b (m)", "H (m)", "Ghi chú" },
            adv.Columns.Select(c => new[] { c.T.Name, N(c.T.Ntc, "0"), N(c.T.Ntt, "0"), N(c.R, "0"), double.IsFinite(c.B) ? $"{N(c.B, "0.0")} × {N(c.B, "0.0")}" : "-", N(c.H, "0.00"), c.Note }), null, 9);
        d.Heading("4.2. Móng băng", 2);
        d.Paragraph((adv.StripFeasible ? "Khả thi: " : "Không phù hợp: ") + adv.StripReason);
        d.Table(new[] { "Dải", "Số cột", "Dài (m)", "ΣNtc (kN)", "B (m)", "Bản móng hb (m)", "Sườn b × h (m)" },
            adv.Strips.Select(s => new[] { s.Line, s.Cols.ToString(Inv), N(s.Length, "0.0"), N(s.SumNtc, "0"), N(s.B, "0.00"), N(s.Hf, "0.00"), $"{N(s.RibB, "0.00")} × {N(s.RibH, "0.00")}" }), null, 9);
        d.Heading("4.3. Móng cọc ép", 2);
        d.Table(new[] { "Loại cọc", "L (m)", "[P] (kN)", "Nguồn [P]", "Tổng số cọc", "Bê tông cọc (m³)" },
            adv.Piles.Select(x => new[] { x.Name, N(x.Length, "0.0"), N(x.Capacity, "0"), x.FromPress ? "Lực ép đầu cọc Pép/k" : x.FromBorehole ? "Hố khoan (SPT), mũi " + x.TipLayer : "Nhập tay", x.Total.ToString(Inv), N(x.Volume, "0.0") }), null, 9);
        if (adv.Press != null)
        {
            d.Paragraph($"Sức chịu tải sơ bộ cọc ép theo lực ép đầu cọc Pép = {N(adv.Press.ForceT, "0")} t (TCVN 9394:2012):", true);
            foreach (var pn in adv.Press.Notes) d.Paragraph("– " + pn);
        }
        if (adv.BestPile != null && adv.Pits.Count > 0)
        {
            d.Paragraph($"Số cọc từng hố móng ({adv.BestPile.Name}, [P] = {N(adv.BestPile.Capacity, "0")} kN): n = ⌈(1,1·ΣNtt·R1/N + Gđài)/[P]⌉; cột sát ranh – đài lệch tâm + dầm móng cân bằng R1/N = Ls/(Ls − e). Tổng {adv.PitPiles} cọc / {adv.Pits.Count} hố móng.");
            d.Table(new[] { "Hố móng", "Cột", "Vị trí", "ΣNtt (kN)", "e (m)", "R1/N", "N tính cọc (kN)", "Số cọc", "Đài Lx × Ly (m)", "Ghi chú" },
                adv.Pits.Select(x => new[] { x.Name, x.Names, x.EdgeText, N(x.Ntt, "0"), x.Ex > 0 || x.Ey > 0 ? $"{N(x.Ex, "0.00")} / {N(x.Ey, "0.00")}" : "0", N(x.Factor, "0.00"),
                    N(x.Ndesign, "0"), x.Piles.ToString(Inv), $"{N(x.CapLx, "0.00")} × {N(x.CapLy, "0.00")}", x.Note }), null, 8);
        }
        d.Paragraph("Kết quả là gợi ý sơ bộ để chọn phương án; thiết kế chi tiết móng, giằng móng theo nội lực và khảo sát địa chất.", false, true);
        return d;
    }
}
