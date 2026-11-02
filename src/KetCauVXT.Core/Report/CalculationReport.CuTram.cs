using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>[CừTràm] + [LùiCọcRanh] Thuyết minh: chương "Gia cố nền bằng cừ tràm" (hai phương pháp, công thức, ví dụ tính, kiểm tra độ bền / lún, bảng tổng hợp)
/// và mục "Lùi cọc khỏi ranh giới để ép cọc" trong chương đài cọc. Gọi từ CalculationReport.Export / Auto.</summary>
public static partial class CalculationReport
{
    private static bool HasCuTram(DesignSummary s) =>
        s != null && (s.Footings.Any(f => f.CuTram != null) || s.StripFootings.Any(x => x.CuTram != null));

    private static void CuTramSection(Project p, DesignSummary s, DocxWriter d, int n)
    {
        d.Heading($"{n}. GIA CỐ NỀN BẰNG CỪ TRÀM", 1);
        var cs = p.Foundation?.CuTram ?? new CuTramSettings();
        d.Paragraph("Nền đất yếu dưới móng nông được gia cố bằng cừ tràm (cây tràm đóng thẳng đứng, mật độ dày) nhằm nén chặt đất và truyền một phần tải trọng xuống sâu nhờ ma sát. "
                    + $"Thông số: chiều dài cừ Lc = {N(cs.Length, "0.0#")} m, đường kính gốc D1 = {N(cs.TopDiameter * 100, "0.#")} cm, ngọn D2 = {N(cs.TipDiameter * 100, "0.#")} cm, mật độ {N(cs.Density, "0")} cây/m²; "
                    + $"đầu cừ đặt sát đáy lớp bê tông lót dày {Mm(cs.LeanThickness)} mm" + (cs.SandCushion > 0 ? $", phủ cát đệm dày {Mm(cs.SandCushion)} mm" : "") + ". "
                    + $"Phương pháp dùng chọn kích thước móng: {CuTramSettings.MethodName(cs.Method)}.");
        d.Paragraph("Tài liệu áp dụng: TCVN 10304:2014 (ma sát fi của cọc đóng / ép theo độ sâu và độ sệt IL; sức kháng mũi đất dính qb = 9·cu, đất rời qb = 300·N theo SPT); "
                    + "TCVN 9362:2012 (sức chịu tải R, độ lún cộng lớp, Sgh = 8 cm); thực hành thiết kế cừ tràm ở Việt Nam (nền tương đương, khối móng quy ước). "
                    + "Các hệ số không quy định trong tiêu chuẩn (αs, quy luật tăng φ, c theo e, ngưỡng N < 5, tải lớn nhất) là giá trị KINH NGHIỆM – người thiết kế kiểm tra lại.", false, true);

        d.Heading($"{n}.1. Phương pháp (1) – cừ tràm làm việc như cọc ma sát, nền phức hợp", 2);
        d.Equations(new[]
        {
            "u = π·(D1 + D2)/2 (chu vi trung bình thân côn);   Ap = π·D2²/4;   Qc = u·Σ fi·li + qp·Ap",
            "fi theo TCVN 10304:2014 (bảng ma sát cọc đóng / ép) theo độ sâu trung bình lớp zi và IL (đất rời: cát thô–vừa ↔ IL 0,2; cát nhỏ ↔ 0,3); đoạn chia ≤ 1 m; không kể ma sát trong đất đắp",
            "Qa = min(Qc/FS; Pvl),  Pvl = Rgỗ·Ap;   n = ⌈mật độ × L × B⌉;   η = 1 − θ/90·[(k − 1)m + (m − 1)k]/(m·k), θ = arctg(d/s) (Converse–Labarre)",
            "[p]1 = (η·n·Qa − Gcừ)/A + αs·(1 − m)·R0   (m = n·Ap,tb/A – tỷ lệ thay thế; R0 – sức chịu tải nền thiên nhiên tại đáy móng; αs – hệ số kể đất giữa cừ)",
            "Điều kiện: ptb = Ntc/A + γtb·Df ≤ [p]1;   pmax ≤ 1,2·[p]1"
        });
        d.Heading($"{n}.2. Phương pháp (2) – nền tương đương sau gia cố + khối móng quy ước", 2);
        d.Equations(new[]
        {
            "Vc/V = mật độ·Vcừ/Lc,  Vcừ = π·Lc/12·(D1² + D1·D2 + D2²);   e1 = e0 − (1 + e0)·Vc/V",
            "φ1, c1 của đất sau gia cố: theo thí nghiệm (người dùng nhập) hoặc ước lượng kinh nghiệm φ1 = φ0 + 10°·(e0 − e1)/e0, c1 = c0·e0/e1",
            "R1 = m1·m2/ktc·(A·b·γ + B·Df·γ' + D·c1) (TCVN 9362:2012, A, B, D theo φ1) – điều kiện ptb ≤ R1",
            "Khối móng quy ước tại mũi cừ: α = φtb/4;  Lqu = L + 2·(z_mũi − Df)·tgα,  Bqu = B + 2·(z_mũi − Df)·tgα;  W = Lqu·Bqu·[σ'bt(z_mũi) − σ'bt(Df)]",
            "σ_mũi = (ptb·L·B + W)/(Lqu·Bqu) ≤ R_mũi (TCVN 9362:2012 với b = Bqu, h = z_mũi, chỉ tiêu lớp tại mũi cừ);   [p]2 = min(R1; (R_mũi·Aqu − W)/A)",
            "Lún: cộng lún từng lớp dưới mũi cừ của khối quy ước Lqu × Bqu với áp lực σ_mũi (GeotechCalc.Settlement) ≤ Sgh; lún lệch ΔS/l ≤ 0,002"
        });
        d.Paragraph("Độ bền (bắt buộc): cừ tràm chỉ bền khi luôn ngập nước – cao độ đầu cừ phải nằm dưới mực nước ngầm THẤP NHẤT (mùa khô). "
                    + "Nếu đầu cừ nằm trên MNN: KHÔNG ĐẠT – cừ tràm nằm trên mực nước ngầm sẽ mục, phải hạ cao độ đầu cừ hoặc dùng cọc BTCT. "
                    + "Cừ tràm chỉ hiệu quả trong đất yếu (bùn, sét dẻo chảy N < 5); trong đất chặt việc đóng cừ khó và ít tác dụng; tải chân cột lớn nên dùng cọc BTCT.", true);

        // ---- ví dụ ----
        var ex = s.Footings.Where(f => f.CuTram != null).OrderByDescending(f => f.N).FirstOrDefault();
        CuTramResult r = ex?.CuTram ?? s.StripFootings.FirstOrDefault(x => x.CuTram != null)?.CuTram;
        string nm = ex?.Name ?? s.StripFootings.FirstOrDefault(x => x.CuTram != null)?.Name ?? "";
        if (r != null)
        {
            d.Heading($"{n}.3. Ví dụ tính toán: móng {nm} ({N(r.L)} × {N(r.B)} m, Df = {N(r.Df)} m)", 2);
            foreach (var line in r.Notes) d.Equation(line);
            if (r.Rows.Count > 0)
                d.Table(new[] { "Đoạn (m)", "Lớp đất", "IL tra bảng", "fi (kPa)", "u·fi·li (kN)", "Ghi chú" },
                    r.Rows.Select(x => new[] { $"{N(x.From)}–{N(x.To)}", x.Soil, N(x.IL, "0.00"), N(x.F, "0.0"), N(x.Q, "0.000"), x.Note }),
                    new[] { 2.0, 4.5, 1.6, 1.4, 1.8, 4.5 }, 9);
            if (r.Settlement != null)
                d.Equation($"Lún khối quy ước: S = {N(r.Settlement.S * 100, "0.00")} cm {(r.Settlement.Ok ? "≤" : ">")} Sgh = {N(r.Settlement.Limit * 100, "0")} cm (vùng nén Hc = {N(r.Settlement.Hc, "0.0")} m dưới mũi cừ)");
            d.Table(new[] { "Hạng mục", "Giá trị", "Giới hạn", "Đơn vị", "Kết luận", "Giải thích" },
                r.Items.Select(i => new[] { i.Name, N(i.Value, "0.##"), (i.Inverse ? "≥ " : "≤ ") + N(i.Limit, "0.##"), i.Unit, i.StatusText, i.Explanation }),
                new[] { 4.6, 1.5, 1.6, 1.0, 1.6, 5.5 }, 9);
            foreach (var w in r.Warnings.Distinct()) d.Paragraph("• " + w, false, true);
        }

        d.Heading($"{n}.4. Bảng tổng hợp móng gia cố cừ tràm", 2);
        var rows = new List<string[]>();
        foreach (var f in s.Footings.Where(x => x.CuTram != null))
            rows.Add(CuTramRow(f.Name, f.Quantity, f.L, f.B, f.Ptb, f.CuTram, f.Ok));
        foreach (var st in s.StripFootings.Where(x => x.CuTram != null))
            rows.Add(CuTramRow(st.Name, st.Quantity, st.Length, st.Width, st.PAvg, st.CuTram, st.Ok));
        d.Table(new[] { "Móng", "SL", "L×B (m)", "Số cây", "[p]1", "[p]2", "[p]", "ptb", "S (cm)", "Đầu cừ / MNN (m)", "KL" }, rows,
            new[] { 1.2, 0.7, 1.8, 1.1, 1.0, 1.0, 1.0, 1.0, 1.1, 2.2, 1.6 }, 9);
        d.Paragraph("Áp lực đơn vị kN/m²; số cây cho 1 móng; MNN – mực nước ngầm thấp nhất dùng kiểm tra độ bền cừ.", false, true);
    }

    private static string[] CuTramRow(string name, int qty, double L, double B, double ptb, CuTramResult r, bool ok) => new[]
    {
        name, qty.ToString(Inv), $"{N(L)}×{N(B)}", r.Count.ToString(Inv), N(r.P1, "0"), N(r.P2, "0"), N(r.Allowable, "0"), N(ptb, "0"),
        N((r.Settlement?.S ?? double.NaN) * 100, "0.0"), $"{N(r.TopDepth)} / {(double.IsFinite(r.WaterDepth) ? N(r.WaterDepth) : "không gặp")}", OkText(ok && r.DurabilityOk)
    };

    /// <summary>[LùiCọcRanh] Mục "Lùi cọc khỏi ranh giới để ép cọc" (cuối chương đài cọc).</summary>
    private static void PileSetbackReport(Project p, DesignSummary s, DocxWriter d, int n)
    {
        var caps = s.PileCaps.Where(c => c.Setback != null).ToList();
        if (caps.Count == 0) return;
        var fs = p.Foundation ?? new FoundationSettings();
        d.Heading($"{n}.3. Lùi cọc khỏi ranh giới để ép cọc", 2);
        d.Paragraph($"Nhà giáp ranh không ép được cọc sát ranh giới: tim{(fs.PileSetbackToFace ? " (mép)" : "")} cọc lùi vào trong lô đất tối thiểu {Mm(fs.PileSetback)} mm "
                    + "(từng cạnh ranh giới có thể khai báo riêng; cạnh không có nhà bên cạnh lấy 0). " + PileSetback.MethodNote(fs.PilePressMethod) + ". "
                    + (fs.CapToBoundary ? "Đài vươn ra sát ranh giới (phần công xôn ngoài hàng cọc đỡ cột sát ranh)." : "Mép đài theo cọc (mép cọc + khoảng mép, tối thiểu phủ cột)."));
        d.Equations(new[]
        {
            "Nhóm cọc lệch tâm e = x̄_cọc − x_cột;  không dầm cân bằng: Pi = (N + G)/n + ax·(xi − x̄) + ay·(yi − ȳ) (mô men (N + G)·e do nhóm cọc), Pmin ≥ −[Pk]",
            "Có dầm móng cân bằng tới đài cột trong: R1 = N1·L/(L − e) đặt tại trọng tâm nhóm cọc (phản lực cọc đều theo phương dầm);  dầm DM: M = N1·e, Q = R1 − N1",
            "Công xôn đài (cột nằm ngoài hàng cọc): M = N·a, Q = N, a = tim cột → mép trong hàng cọc gần nhất;  Qb = 1,5·Rbt·b·h0²/c (c = a);  thép lớp trên chịu M",
            "Xoắn đài khi lệch tâm vuông góc dầm cân bằng: T = N·e⊥ ≤ 0,1·Rb·b²·h;  chọc thủng cột sát mép đài: chu vi / tháp 45° bị cắt bởi mép đài"
        });
        var c0 = caps.OrderByDescending(c => Math.Max(Math.Abs(c.Setback.GroupEx), Math.Abs(c.Setback.GroupEy))).First();
        var si = c0.Setback;
        d.Paragraph($"Ví dụ: đài {c0.Name} ({c0.Piles.Count} cọc, đài {N(c0.L)} × {N(c0.B)} × {N(c0.H)} m): nhóm cọc dịch Δx = {Mm(si.ShiftX)} mm, Δy = {Mm(si.ShiftY)} mm; "
                    + $"lệch tâm ex = {Mm(si.GroupEx)} mm, ey = {Mm(si.GroupEy)} mm. Đề xuất: {si.Recommended} – {si.Recommendation}.");
        foreach (var o in si.Options) d.Bullet(o);
        d.Table(new[] { "Hạng mục", "Giá trị", "Giới hạn", "Đơn vị", "Kết luận", "Ghi chú" },
            si.Items.Select(i => new[] { i.Name, N(i.Value, "0.##"), i.Status == CheckStatus.Info && Math.Abs(i.Limit) < 1e-12 ? "–" : (i.Inverse ? "≥ " : "≤ ") + N(i.Limit, "0.##"), i.Unit, i.StatusText, i.Formula }),
            new[] { 5.0, 1.4, 1.5, 1.0, 1.6, 5.3 }, 9);
        d.Table(new[] { "Đài", "SL", "Khoảng lùi nhỏ nhất (mm)", "ex / ey (mm)", "Pmax không DM", "Pmax có DM", "Công xôn M (kN.m)", "Đề xuất" },
            caps.Select(c => new[]
            {
                c.Name, c.Quantity.ToString(Inv), c.Setback.Sides.Count > 0 ? Mm(c.Setback.Sides.Min(q => q.Distance)) : "-", $"{Mm(c.Setback.GroupEx)} / {Mm(c.Setback.GroupEy)}",
                N(c.Setback.PmaxNoStrap, "0"), double.IsFinite(c.Setback.PmaxStrap) ? N(c.Setback.PmaxStrap, "0") : "-", N(c.Setback.MCant, "0.0"), c.Setback.Recommended
            }), new[] { 1.2, 0.7, 2.2, 2.0, 1.8, 1.8, 1.9, 3.4 }, 9);
    }
}
