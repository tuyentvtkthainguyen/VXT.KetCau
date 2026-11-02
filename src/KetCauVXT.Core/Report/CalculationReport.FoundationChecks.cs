using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;

namespace KetCauVXT.Report;

/// <summary>[FoundationChecks] Thuyết minh: kiểm tra an toàn chi tiết đài cọc (phản lực từng cọc, chọc thủng có kể h0/c, nhóm cọc, tải ngang, lún),
/// sự làm việc móng băng (dầm trên nền Winkler, phân loại cứng / hữu hạn / mềm) và móng đơn (móng cứng, lệch tâm, trượt, chọc thủng có mô men).
/// Được gọi từ các mục móng của CalculationReport.cs.</summary>
public static partial class CalculationReport
{
    private static string[] SafetyRow(SafetyItem i) => new[]
    {
        i.Group, i.Name, i.Status == CheckStatus.Info && Math.Abs(i.Value) < 1e-12 && Math.Abs(i.Limit) < 1e-12 ? "-" : N(i.Value, "0.###"),
        i.Status == CheckStatus.Info && Math.Abs(i.Limit) < 1e-12 ? "-" : (i.Inverse ? "≥ " : "≤ ") + N(i.Limit, "0.###"),
        i.Unit, i.Status == CheckStatus.Info ? "-" : N(i.Utilisation, "0.00"), i.StatusText
    };

    private static void SafetyTable(FoundationCheckReport r, DocxWriter d)
    {
        if (r == null || r.Items.Count == 0) return;
        d.Table(new[] { "Nhóm", "Hạng mục", "Giá trị", "Giới hạn", "Đơn vị", "Hệ số sử dụng", "Kết luận" },
            r.Items.Select(SafetyRow), new[] { 2.0, 6.0, 1.6, 1.8, 1.2, 1.4, 1.8 }, 9);
        foreach (var i in r.Items.Where(x => x.Status == CheckStatus.Fail || x.Status == CheckStatus.Warning))
            d.Bullet($"{i.Name} – {i.StatusText}: {i.Explanation}" + (string.IsNullOrEmpty(i.Formula) ? "" : " (" + i.Formula + ")"));
    }

    /// <summary>Đài cọc: bảng phản lực từng cọc, chọc thủng chi tiết, bảng kiểm tra an toàn, các đài do người dùng bố trí.</summary>
    private static void PileCapSafetyReport(DesignSummary s, PileCapDesign c0, DocxWriter d, int n)
    {
        var r = c0?.Safety;
        if (r == null) return;
        d.Paragraph($"Kiểm tra an toàn chi tiết đài {c0.Name} ({r.Classification}):", true);
        if (!string.IsNullOrEmpty(r.Behaviour)) d.Paragraph(r.Behaviour);
        d.Equation("Pi = (N + G)/n + ax·(xi − x̄) + ay·(yi − ȳ);  ax·Sxx + ay·Sxy = −My − Qx·H − (N + G)·x̄;  ax·Sxy + ay·Syy = Mx − Qy·H − (N + G)·ȳ");
        d.Paragraph("(nhóm cọc đối xứng: x̄ = ȳ = Sxy = 0 → Pi = (N + G)/n ± My'·xi/Σxi² ± Mx'·yi/Σyi²)", false, true);
        if (r.Piles.Count > 0)
            d.Table(new[] { "Cọc", "x (mm)", "y (mm)", "P tổ hợp bất lợi (kN)", "Pmax (kN)", "Pmin (kN)", "Tổ hợp Pmax", "Trong tháp 45°" },
                r.Piles.Select(pr => new[]
                {
                    "C" + pr.No, N(pr.X * 1000, "0"), N(pr.Y * 1000, "0"), N(pr.P, "0.0"), N(pr.PMax, "0.0"), N(pr.PMin, "0.0"), pr.ComboMax, pr.InsidePyramid ? "có" : "không"
                }), new[] { 1.0, 1.4, 1.4, 2.4, 1.6, 1.6, 2.6, 1.8 }, 9);
        var pd = r.Punching;
        if (pd != null && c0.Piles.Count > 1)
        {
            d.Paragraph("Chọc thủng đài do cột (TCVN 5574:2018 mục 8.1.6.2, có kể hệ số tăng k = h0/c cho đài cọc, 1 ≤ k ≤ 2,5):", false, true);
            if (pd.AllInside)
                d.Equation($"Mọi cọc nằm trong đáy tháp chọc thủng 45° (|xi| ≤ hc/2 + h0, |yi| ≤ bc/2 + h0, h0 = {Mm(pd.H0)} mm) → F = 0: đài không bị chọc thủng do cột.");
            else
            {
                d.Equation($"F = Σ(Pi − G/n) các cọc ngoài tháp = {N(pd.F, "0.0")} kN");
                d.Equation($"Fb,u = 2·Rbt·h0·[k1·(bc + c2) + k2·(hc + c1)];  c1 = {Mm(pd.C1)} mm, c2 = {Mm(pd.C2)} mm, k1 = {N(pd.K1)}, k2 = {N(pd.K2)} → Fb,u = {N(pd.Fbu, "0.0")} kN");
                d.Equation($"F = {N(pd.F, "0.0")} {(pd.F <= pd.Fbu + 1e-6 ? "≤" : ">")} Fb,u = {N(pd.Fbu, "0.0")} kN → {(pd.F <= pd.Fbu + 1e-6 ? "Đạt" : "KHÔNG ĐẠT (bê tông)")}");
                if (pd.AswPerM > 0)
                    d.Equation($"Cốt thép chống chọc thủng (8.1.6.3): qsw = max(F − Fb,u; 0,25Fb,u)/(0,8u) → Asw/sw = {N(pd.AswPerM * 1e4)} cm²/m; chọn {pd.Reinforcement}, Fsw,u = {N(pd.Fswu, "0.0")} kN; Fb,u + Fsw,u = {N(pd.Fbu + pd.Fswu, "0.0")} kN ≤ 2Fb,u");
                if (pd.NeedHeight) d.Paragraph("F > 2Fb,u (hoặc không đạt ở chiều cao tối đa): cần tăng chiều cao đài.", true);
            }
        }
        SafetyTable(r, d);
        foreach (var w in r.Warnings) d.Paragraph(w, false, true);

        var users = s.PileCaps.Where(x => x.Safety?.UserLayout == true).ToList();
        if (users.Count > 0)
        {
            d.Paragraph("Các đài bố trí cọc theo lựa chọn của người thiết kế:", true);
            d.Table(new[] { "Đài", "Sơ đồ", "Số cọc", "L×B×H (m)", "Pmax/[P]", "η nhóm", "Chọc thủng F/Fb,u", "KL" },
                users.Select(x => new[]
                {
                    x.Name, x.Safety.Classification, x.Piles.Count.ToString(Inv), $"{N(x.L)}×{N(x.B)}×{N(x.H)}",
                    N(x.PileCapacity > 0 ? x.PMax / x.PileCapacity : 0, "0.00"), N(x.Safety.GroupEta, "0.00"),
                    x.Safety.Punching == null || x.Safety.Punching.AllInside || x.Safety.Punching.Fbu <= 0 ? "-" : N(x.Safety.Punching.F / x.Safety.Punching.Fbu, "0.00"),
                    OkText(x.Ok)
                }), new[] { 1.2, 3.6, 1.2, 2.6, 1.6, 1.4, 2.2, 1.6 }, 9);
        }
    }

    /// <summary>Móng băng: phân loại sự làm việc (dầm trên nền Winkler), bảng λL các dải, bảng kiểm tra dải ví dụ.</summary>
    private static void StripBehaviourReport(DesignSummary s, StripFootingDesign s0, DocxWriter d, int n)
    {
        var r = s0?.Safety;
        if (r == null) return;
        d.Paragraph($"Sự làm việc của dải {s0.Name}: {r.Classification}", true);
        d.Equation("λ = (k·B/(4·E·I))^¼;   λL ≤ π/4: dầm cứng;   π/4 < λL < π: dầm hữu hạn;   λL ≥ π: dầm dài / mềm");
        d.Equation($"E·I = {N(r.EI, "0")} kN.m²;  λ = {N(r.Lambda, "0.0000")} 1/m;  1/λ = {N(r.Lambda > 0 ? 1 / r.Lambda : 0)} m;  λL = {N(r.LambdaL)}");
        d.Equation("Kiểm chứng lời giải: dầm vô hạn chịu lực tập trung P – M0 = P/(4λ), w0 = P·λ/(2·k·B)");
        if (!string.IsNullOrEmpty(r.Behaviour)) d.Paragraph(r.Behaviour);
        SafetyTable(r, d);
        var all = s.StripFootings.Where(x => x.Safety != null).ToList();
        if (all.Count > 1)
            d.Table(new[] { "Dải", "Trục", "L (m)", "k (kN/m³)", "λ (1/m)", "λL", "Phân loại" },
                all.Select(x => new[] { x.Name, x.Grid, N(x.Length), N(x.K, "0"), N(x.Safety.Lambda, "0.0000"), N(x.Safety.LambdaL), x.Safety.Classification }),
                new[] { 1.2, 1.8, 1.4, 1.8, 1.6, 1.2, 4.0 }, 9);
    }

    /// <summary>Móng đơn: sự làm việc (móng cứng, lệch tâm / tách nền, trượt, chọc thủng có mô men, cắt).</summary>
    private static void FootingBehaviourReport(FootingDesign f0, DocxWriter d)
    {
        var r = f0?.Safety;
        if (r == null) return;
        d.Paragraph($"Sự làm việc của móng {f0.Name}: {r.Classification}", true);
        d.Equation("Móng cứng: c ≤ 2,5H;  lõi tiết diện: eL/L + eB/B ≤ 1/6;  tách nền một phần: pmax = 2N/(3·B·c0), c0 = L/2 − e, 3c0 ≥ 0,75L");
        d.Equation("Chọc thủng có mô men (8.1.6.2): F/Fb,u + 0,5Mx/Mbx,u + 0,5My/Mby,u ≤ 1, Mb,u = Rbt·Wb·h0, Wb = Lx²/3 + Lx·Ly;  trượt: H ≤ [(N + 0,9G)·tgφ + c·A]/1,2");
        if (!string.IsNullOrEmpty(r.Behaviour)) d.Paragraph(r.Behaviour);
        SafetyTable(r, d);
    }
}
