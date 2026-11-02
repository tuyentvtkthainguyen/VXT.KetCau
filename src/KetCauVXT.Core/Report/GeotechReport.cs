using System.Globalization;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Mục "ĐỊA CHẤT VÀ NỀN MÓNG" của thuyết minh (khi dự án có hố khoan): bảng hố khoan, lớp đặt móng, R, lún, sức chịu tải cọc theo SPT.</summary>
internal static class GeotechReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string N(double v, string fmt = "0.##") => double.IsFinite(v) ? v.ToString(fmt, Inv) : "-";

    public static void Section(Project p, DesignSummary s, DocxWriter d, int n)
    {
        var holes = p.Boreholes?.Where(b => b != null).ToList() ?? new List<Borehole>();
        if (holes.Count == 0) return;
        s ??= new DesignSummary();
        var so = p.Soil ?? new SoilSettings();
        d.Heading($"{n}. ĐỊA CHẤT VÀ NỀN MÓNG", 1);
        d.Paragraph($"Số liệu địa chất theo {holes.Count} hố khoan khảo sát. " +
                    (so.UseBorehole
                        ? "Chỉ tiêu nền dùng thiết kế móng lấy theo hố khoan " + (string.IsNullOrWhiteSpace(so.Borehole) ? "gần chân cột nhất" : so.Borehole) + "."
                        : "Thiết kế móng chưa áp dụng hố khoan – chỉ tiêu nền lấy theo số liệu nhập tay" + (so.AssumeGoodSoil ? " (nền đất tốt giả định)." : ".")));

        int k = 1;
        foreach (var bh in holes)
        {
            d.Heading($"{n}.{k++}. Hố khoan {bh.Name} (X = {N(bh.X)} m, Y = {N(bh.Y)} m, cao độ miệng {N(bh.GroundElevation, "0.00")} m, " +
                      (bh.WaterDepth < 0 ? "không gặp nước ngầm" : "mực nước ngầm sâu " + N(bh.WaterDepth) + " m") + ")", 2);
            d.Table(new[] { "Lớp", "Độ sâu (m)", "Loại đất", "γ (kN/m³)", "γsat", "φ (°)", "c (kPa)", "E (MPa)", "N", "IL", "e" },
                bh.Sorted().Select(l => new[]
                {
                    l.Name, $"{N(l.Top)}–{N(l.Bottom)}", l.KindText, N(l.Gamma, "0.0"), N(l.GammaSatOrDefault, "0.0"), N(l.Phi, "0.#"), N(l.C, "0.#"),
                    N(l.E, "0.#"), N(l.N, "0.#"), l.IL < 0 ? "–" : N(l.IL, "0.##"), l.VoidRatio > 0 ? N(l.VoidRatio, "0.##") : "–"
                }), new[] { 3.2, 1.6, 2.0, 1.2, 1.0, 1.0, 1.0, 1.0, 0.8, 0.8, 0.8 }, 9);
        }

        // ---- lớp đặt móng, R ----
        var bh0 = GeotechCalc.SelectBorehole(p, s.Footings.FirstOrDefault()?.X ?? holes[0].X, s.Footings.FirstOrDefault()?.Y ?? holes[0].Y) ?? holes[0];
        d.Heading($"{n}.{k++}. Nền dưới đáy móng (hố khoan {bh0.Name}, Df = {N(so.Depth)} m)", 2);
        var sa = GeotechCalc.CopySoil(so);
        foreach (var t in GeotechCalc.ApplyToSoil(sa, bh0, so.Depth)) d.Paragraph(t);
        double b1 = s.Footings.Count > 0 ? s.Footings.Min(f => Math.Min(f.L, f.B)) : 1.0;
        d.Equation($"R = (m1·m2/ktc)·(A·b·γII + B·Df·γ'II + D·cII) = {N(FootingDesigner.BearingR(sa, b1), "0.0")} kN/m² (b = {N(b1)} m)");

        // ---- lún ----
        var withS = s.Footings.Where(f => f.Checks.Any(c => c.Name.StartsWith("Độ lún"))).ToList();
        if (withS.Count > 0)
        {
            d.Heading($"{n}.{k++}. Độ lún móng đơn (TCVN 9362:2012, phương pháp cộng lún từng lớp)", 2);
            d.Equation("σgl = ptb − σ'bt(Df);   σz,i = 4·kc(L/2, B/2, zi)·σgl;   hi ≤ 0,4B;   dừng khi σz ≤ 0,2σ'bt (0,1σ'bt nếu E < 5 MPa)");
            d.Equation("S = β·Σ σz,i·hi/Ei,  β = 0,8;   S ≤ Sgh = 8 cm (Bảng 16, nhà khung BTCT)");
            d.Table(new[] { "Móng", "SL", "L×B (m)", "ptb (kN/m²)", "S (cm)", "Sgh (cm)", "KL" },
                withS.Select(f =>
                {
                    var c = f.Checks.Last(x => x.Name.StartsWith("Độ lún"));
                    return new[] { f.Name, f.Quantity.ToString(Inv), $"{N(f.L)}×{N(f.B)}", N(f.Ptb, "0.0"), N(c.Demand, "0.00"), N(c.Capacity, "0"), c.Ok ? "Đạt" : "KHÔNG ĐẠT" };
                }), new[] { 1.5, 1.0, 2.2, 2.0, 1.6, 1.6, 2.0 }, 9);
            var f0 = withS.OrderByDescending(f => f.Ptb).First();
            var bhf = GeotechCalc.SelectBorehole(p, f0.X, f0.Y) ?? bh0;
            var st = GeotechCalc.Settlement(bhf, f0.L, f0.B, so.Depth, f0.Ptb);
            d.Paragraph($"Ví dụ: móng {f0.Name} {N(f0.L)}×{N(f0.B)} m, hố khoan {bhf.Name}:", false, true);
            foreach (var t in st.Notes) d.Paragraph(t, false, true);
            d.Table(new[] { "zi (m)", "hi (m)", "Lớp", "σz (kPa)", "σ'bt (kPa)", "E (MPa)", "Si (mm)" },
                st.Rows.Select(r => new[] { N(r.Z, "0.00"), N(r.H, "0.00"), r.Layer, N(r.SigmaZ, "0.0"), N(r.SigmaBt, "0.0"), N(r.E, "0.#"), N(r.Ds * 1000, "0.00") }),
                new[] { 1.4, 1.4, 4.0, 1.8, 1.8, 1.4, 1.6 }, 9);
        }

        // ---- cọc ----
        var fs = p.Foundation ?? new FoundationSettings();
        bool pileUsed = s.PileCaps.Count > 0 || fs.Type == FoundationType.Pile;
        if (pileUsed)
        {
            var bhp = s.PileCaps.Count > 0 ? GeotechCalc.SelectBorehole(p, s.PileCaps[0].X, s.PileCaps[0].Y) ?? bh0 : bh0;
            var pr = GeotechCalc.PileCapacity(p, bhp, fs);
            d.Heading($"{n}.{k++}. Sức chịu tải cọc theo SPT (TCVN 10304:2014 Phụ lục G.3.2) – hố khoan {bhp.Name}", 2);
            d.Equation("Rc,u = qb·Ab + u·Σ(fc,i·lc,i + fs,i·ls,i);   đất rời: fs,i = 10·Ns,i/3;   qb = 300·Np (cọc ép) / 150·Np (khoan nhồi)");
            d.Equation("đất dính: cu,i = 6,25·Nc,i;  fc,i = αp·fL·cu,i,  fL = 1,  αp = 0,5·ψ^−0,5 (ψ = cu/σ'v ≤ 1) hoặc 0,5·ψ^−0,25 (ψ > 1), αp ≤ 1;  qb = 9·cu,p");
            d.Equation($"[P]đn = Rc,u/FS, FS = {N(pr.SafetyFactor)} (hệ số an toàn tổng thể, tương đương γk·γn/γ0);   Pvl = φ·(Rb·Ab + Rsc·As), φ = 1;   [P] = min([P]đn, Pvl)");
            d.Table(new[] { "Độ sâu (m)", "Lớp đất", "Loại", "N", "cu (kPa)", "αp", "f / qb (kPa)", "Q (kN)" },
                pr.Rows.Select(r => new[]
                {
                    $"{N(r.From)}–{N(r.To)}", r.Soil, r.IsTip ? "mũi" : (r.Cohesive ? "dính" : "rời"), N(r.N, "0.#"),
                    r.Cohesive ? N(r.Cu, "0.#") : "–", r.Cohesive && !r.IsTip ? N(r.Alpha, "0.###") : "–", N(r.F, "0.0"), N(r.Q, "0.0")
                }), new[] { 1.8, 4.6, 1.0, 0.9, 1.3, 1.0, 1.6, 1.5 }, 9);
            foreach (var t in pr.Notes) d.Paragraph(t, false, true);
            if (!fs.PileCapacityFromBorehole)
                d.Paragraph($"Ghi chú: thiết kế đài cọc đang dùng [P] nhập tay = {N(fs.PileCapacity, "0")} kN (chưa chọn tính [P] từ hố khoan).", false, true);
        }
        d.Paragraph(SoilPresets.Disclaimer + ": các chỉ tiêu tham khảo / tương quan SPT chỉ dùng chọn phương án sơ bộ.", false, true);
    }
}
