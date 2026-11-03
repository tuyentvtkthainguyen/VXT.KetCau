using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Chương "PHÂN TÍCH DAO ĐỘNG RIÊNG" và mục "Phổ phản ứng" của thuyết minh (Analysis/FrameAnalysis.Modal.cs).</summary>
public static partial class CalculationReport
{
    /// <summary>Tên phương pháp động đất dùng trong câu văn thuyết minh.</summary>
    private static string SeismicMethodText(Project p) =>
        p?.Seismic != null && p.Seismic.IsResponseSpectrum
            ? "phương pháp phân tích phổ phản ứng dạng dao động (mục 4.3.3.3 – số điều theo bản 2012)"
            : "phương pháp phân tích tĩnh lực ngang tương đương";

    private static string Pct(double v) => N(v * 100, "0.0");

    /// <summary>Chuyển vị trung bình (UX, UY, RZ) các nút tại cao trình tầng của một dạng dao động, đã chuẩn hoá max = 1.</summary>
    private static double[] StoryShape(Project p, ModeResult m, double z)
    {
        var ids = p.Nodes.Where(n => Math.Abs(n.Z - z) < 1e-3 && m.Disp.ContainsKey(n.Id)).Select(n => n.Id).ToList();
        if (ids.Count == 0) return new double[3];
        double ux = ids.Average(i => m.Disp[i][0]), uy = ids.Average(i => m.Disp[i][1]), rz = ids.Average(i => m.Disp[i][5]);
        return new[] { ux * m.DisplayScale, uy * m.DisplayScale, rz * m.DisplayScale };
    }

    /// <summary>Chương phân tích dao động riêng (gọi khi r.Modal != null).</summary>
    private static void ModalChapter(Project p, AnalysisResults r, DocxWriter d, int n)
    {
        var mr = r?.Modal;
        if (mr == null || mr.Modes.Count == 0) return;
        var ms = p.Modal ?? new ModalSettings();
        d.Heading($"{n}. PHÂN TÍCH DAO ĐỘNG RIÊNG", 1);

        d.Heading($"{n}.1. Mô hình khối lượng", 2);
        d.Paragraph($"Khối lượng tham gia dao động lấy theo {Std.SeismicClause("mục 3.2.4")}: khối lượng = (G + ψE·Q)/g với g = {N(ModalResult.G, "0.00")} m/s², "
            + "ψE = φ·ψ2 (nhà ở: ψ2 = 0,3; φ = 0,8 cho các tầng sử dụng đồng thời → ψE = 0,24; hoạt tải mái ψ2 = 0). "
            + "Khối lượng tập trung tại nút: trọng lượng bản thân thanh chia đôi về hai đầu, tải tường/sàn/hoàn thiện trên dầm quy về hai đầu theo quy tắc đòn bẩy.");
        d.Paragraph("Nguồn khối lượng: " + mr.MassSource + (ms.UseDefaultMassSource ? " (mặc định theo loại trường hợp tải)." : " (người dùng khai báo)."));
        d.Table(new[] { "Trường hợp tải", "Loại", "Hệ số khối lượng" },
            ms.Effective(p).Select(x => new[] { x.pat.Name, ExcelReport.LoadTypeName(x.pat.Type), N(x.factor, "0.###") }), new[] { 4.0, 6.0, 4.0 });
        d.Paragraph((mr.Diaphragm
                ? "Sàn tuyệt đối cứng trong mặt phẳng: nút sàn mỗi tầng ràng buộc với nút chủ (UX, UY, RZ); khối lượng tầng và mô men quán tính khối lượng Σm·r² quy về nút chủ. "
                : "Không gán sàn cứng: khối lượng tịnh tiến tại từng nút (bỏ qua quán tính xoay của nút). ")
            + (mr.VerticalMass ? "Có kể khối lượng theo phương đứng. " : "Chỉ kể khối lượng theo phương ngang. ")
            + $"Phương pháp giải: {mr.Solver}; số bậc tự do có khối lượng {mr.DynamicDofs}; số dạng tính {mr.Modes.Count} (yêu cầu {mr.RequestedModes}).");
        d.Paragraph($"Tổng khối lượng tham gia: MX = {N(mr.TotalMass[0], "0.00")} t, MY = {N(mr.TotalMass[1], "0.00")} t (W = {N(mr.TotalMass[0] * ModalResult.G, "0.0")} kN); "
            + $"khối lượng tại gối (không tham gia) {N(mr.BaseMass, "0.00")} t; tâm khối lượng toàn công trình ({N(mr.Xc)}; {N(mr.Yc)}) m.");
        if (mr.Stories.Count > 0)
        {
            d.Paragraph("Bảng khối lượng tầng, tâm khối lượng (Xm, Ym) và tâm cứng (Xr, Yr):");
            d.Table(new[] { "Tầng", "Cao độ (m)", "M (t)", "W (kN)", "Xm (m)", "Ym (m)", "Xr (m)", "Yr (m)", "Im (t·m²)" },
                mr.Stories.Select(s => new[] { s.Story, N(s.Elevation, "0.000"), N(s.Mass), N(s.Weight, "0.0"), N(s.Xm), N(s.Ym), N(s.Xr), N(s.Yr), N(s.Inertia, "0.0") }),
                new[] { 1.6, 1.7, 1.6, 1.8, 1.6, 1.6, 1.6, 1.6, 2.0 }, 10);
        }

        d.Heading($"{n}.2. Chu kỳ và tỷ lệ khối lượng tham gia dao động", 2);
        d.Equation("K·φ = ω²·M·φ;   T = 2π/ω;   Γ = φᵀ·M·ι;   Meff = Γ² (φᵀ·M·φ = 1);   tỷ lệ = Meff / Mtổng");
        d.Table(new[] { "Dạng", "T (s)", "f (Hz)", "UX %", "UY %", "RZ %", "ΣUX %", "ΣUY %", "ΣRZ %", "Phương" },
            mr.Modes.Select(m => new[]
            {
                m.Index.ToString(Inv), N(m.Period, "0.000"), N(m.Frequency, "0.000"), Pct(m.Ratio[0]), Pct(m.Ratio[1]), Pct(m.Ratio[3]),
                Pct(m.Cumulative[0]), Pct(m.Cumulative[1]), Pct(m.Cumulative[3]), m.Dominant
            }), new[] { 1.2, 1.6, 1.6, 1.5, 1.5, 1.5, 1.6, 1.6, 1.6, 2.6 }, 10);
        var m1 = mr.First;
        d.Paragraph($"Chu kỳ dao động cơ bản T1 = {N(m1.Period, "0.000")} s (phương {m1.Dominant}); theo công thức gần đúng T1 = Ct·H^(3/4) = {N(mr.T1Formula, "0.000")} s (H = {N(mr.Height)} m).");
        foreach (int dd in new[] { 0, 1 })
        {
            int k = mr.ModesToReach(dd);
            string dir = dd == 0 ? "X" : "Y";
            d.Paragraph(k > 0
                ? $"Phương {dir}: tổng tỷ lệ khối lượng hữu hiệu đạt ≥ 90 % sau {k} dạng ({Pct(mr.Modes[k - 1].Cumulative[dd])} %) – thoả mãn {Std.SeismicClause("mục 4.3.3.3.1")}."
                : $"Phương {dir}: tổng tỷ lệ khối lượng hữu hiệu của {mr.Modes.Count} dạng = {Pct(mr.SumRatio(dd))} % < 90 % – cần tăng số dạng dao động ({Std.SeismicClause("mục 4.3.3.3.1")}).");
        }

        d.Heading($"{n}.3. Dạng dao động", 2);
        d.Paragraph("Chuyển vị trung bình các nút tại cao trình sàn của 3 dạng đầu (chuẩn hoá chuyển vị tịnh tiến lớn nhất = 1; RZ là góc xoay tương ứng, rad):");
        var stories = p.Stories.Where(s => s.Elevation > p.BaseElevation + 1e-3).OrderByDescending(s => s.Elevation).ToList();
        var first = mr.Modes.Take(3).ToList();
        if (stories.Count > 0)
        {
            var head = new List<string> { "Tầng" };
            foreach (var m in first) { head.Add($"D{m.Index} UX"); head.Add($"D{m.Index} UY"); head.Add($"D{m.Index} RZ"); }
            d.Table(head.ToArray(), stories.Select(s =>
            {
                var row = new List<string> { s.Name };
                foreach (var m in first) { var u = StoryShape(p, m, s.Elevation); row.Add(N(u[0], "0.000")); row.Add(N(u[1], "0.000")); row.Add(N(u[2], "0.0000")); }
                return row.ToArray();
            }), null, 9);
        }
        foreach (var note in mr.Notes) d.Bullet(note);

        if (mr.Spectrum.Count > 0) SpectrumSection(p, mr, d, n);
    }

    private static void SpectrumSection(Project p, ModalResult mr, DocxWriter d, int n)
    {
        var ss = p.Seismic; var ms = p.Modal ?? new ModalSettings();
        d.Heading($"{n}.4. Phân tích phổ phản ứng dạng dao động", 2);
        d.Paragraph($"Theo {Std.SeismicClause("mục 4.3.3.3")}: phổ thiết kế loại 1 (công thức 3.13 – 3.16), agR = {N(ss.AgR, "0.0000")}g, γI = {N(ss.ImportanceFactor, "0.##")}, "
            + $"nền loại {ss.Soil}, hệ số ứng xử q = {N(ss.Q, "0.##")}, cận dưới β = 0,2. Đáp ứng các dạng tổ hợp theo {mr.Spectrum[0].Combination} "
            + "(hệ số tương quan Der Kiureghian: ρij = 8ζ²(1 + r)r^1,5 / [(1 − r²)² + 4ζ²r(1 + r)²], r = ωj/ωi; SRSS: ρij = 0 khi i ≠ j).");
        d.Equation("ukd = Γkd·Sd(Tk)·g/ωk²·φk;   E = √(Σi Σj ρij·Ei·Ej);   Vk = Meff,k·Sd(Tk)·g");
        d.Paragraph("Kết quả phổ phản ứng là giá trị không dấu; tổ hợp đặc biệt G + ψE·Q ± E1 ± 0,3·E2 lấy cả hai dấu (cặp bao +/−). "
            + (ms.AccidentalTorsion ? $"Xoắn ngẫu nhiên: mô men xoắn tầng Mi = {N(ms.Eccentricity, "0.###")}·Li·Fi ({Std.SeismicClause("mục 4.3.3.3.3")}), Fi phân phối lực cắt đáy tĩnh theo zi·mi, cộng trị tuyệt đối vào đáp ứng." : "Không kể xoắn ngẫu nhiên."));
        foreach (var rc in mr.Spectrum)
        {
            d.Paragraph($"Trường hợp {rc.Pattern} (phương {rc.Dir}):", true);
            d.Table(new[] { "Dạng", "T (s)", "Sd/g", "Meff (t)", "Vk (kN)" },
                rc.Rows.Where(x => x.Meff / Math.Max(mr.TotalMass[rc.Dir == 'Y' ? 1 : 0], 1e-9) >= 0.001)
                    .Select(x => new[] { x.Mode.ToString(Inv), N(x.Period, "0.000"), N(x.Sd, "0.0000"), N(x.Meff, "0.00"), N(x.BaseShear, "0.0") }),
                new[] { 2.0, 3.0, 3.0, 3.5, 3.5 }, 10);
            double ratio = rc.StaticBaseShear > 0 ? rc.BaseShear / rc.StaticBaseShear : double.NaN;
            d.Paragraph($"Lực cắt đáy phổ phản ứng V = {N(rc.BaseShear, "0.0")} kN; tĩnh lực ngang tương đương với T = {N(rc.StaticPeriod, "0.000")} s: Fb = {N(rc.StaticBaseShear, "0.0")} kN "
                + $"(V/Fb = {N(ratio, "0.00")}); tổng khối lượng hữu hiệu {Pct(rc.MassRatio)} %."
                + (rc.ScaleFactor > 1 + 1e-9 ? $" Đã nhân kết quả phổ phản ứng với hệ số {N(rc.ScaleFactor, "0.000")} để V ≥ {N(ms.ScaleToStatic, "0.##")}·Fb (tuỳ chọn, không bắt buộc theo TCVN 9386)." : ""));
            if (rc.Torsion.Count > 0)
                d.Table(new[] { "Tầng", "Fi (kN)", "Li (m)", "Mi (kN.m)" },
                    rc.Torsion.Select(t => new[] { t.story, N(t.force, "0.0"), N(t.length), N(t.moment, "0.0") }), new[] { 3.0, 3.5, 3.5, 3.5 }, 10);
        }
    }
}
