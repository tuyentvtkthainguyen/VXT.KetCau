using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>THUYẾT MINH TÍNH TOÁN TỰ ĐỘNG: chọn chương, trang bìa, mục lục (trường TOC tự cập nhật), số trang,
/// tên bảng đánh số, số ví dụ tính toán mỗi loại cấu kiện, bảng nội lực bao, chương khối lượng vật tư và
/// chương "KẾT LUẬN VÀ KIẾN NGHỊ" lập từ kết quả. Export(p, r, s, path) cũ giữ nguyên (không truyền tuỳ chọn).</summary>
public static partial class CalculationReport
{
    /// <summary>Tuỳ chọn của lần xuất tự động đang chạy (null = thuyết minh mặc định).</summary>
    [ThreadStatic] private static ReportOptions _auto;

    /// <summary>Có in mục tải trọng gió / động đất không (hook trong LoadsSection).</summary>
    private static bool LateralOn => _auto == null || _auto.Chapters == null || _auto.Chapters.WindSeismic;

    /// <summary>Số ví dụ bổ sung (ngoài ví dụ đầu tiên) cho mỗi loại cấu kiện.</summary>
    private static int ExtraExamples => _auto == null ? 0 : _auto.Examples - 1;

    /// <summary>Giới hạn chuyển vị ngang lệch tầng tham khảo cho nhà khung BTCT nhiều tầng: Δ/h ≤ 1/500.</summary>
    public const double DriftLimitRc = 1.0 / 500;

    /// <summary>Lập thuyết minh theo tuỳ chọn. o = null → như <see cref="Export(Project, AnalysisResults, DesignSummary, string)"/>.</summary>
    public static void Export(Project p, AnalysisResults r, DesignSummary s, string path, ReportOptions o)
    {
        if (o == null) { Export(p, r, s, path); return; }
        if (p == null) throw new ArgumentNullException(nameof(p));
        s ??= new DesignSummary();
        var c = o.Chapters ?? new ReportChapters();
        string prevFont = DocxWriter.DefaultFont;
        string font = p.CadStandard?.ReportFont;
        try
        {
            _auto = o;
            if (!string.IsNullOrWhiteSpace(font)) DocxWriter.DefaultFont = font;
            string name = string.IsNullOrWhiteSpace(o.ProjectName) ? p.Name : o.ProjectName;
            var d = new DocxWriter
            {
                UpdateFieldsOnOpen = o.TableOfContents,
                FooterText = o.PageNumbers ? "Thuyết minh tính toán kết cấu – " + name : null,
                FooterSkipFirstPage = true
            };
            CoverAuto(p, o, d);
            if (o.TableOfContents) { d.TableOfContents("MỤC LỤC", 2); d.PageBreak(); }
            d.AutoCaptions = o.TableCaptions;

            QuantityResult q = null;
            QuantityResult Q()
            {
                if (q == null) { try { q = QuantityCalculator.Compute(p, s); } catch (Exception) { q = null; } }
                return q;
            }

            int sec = 1;
            if (c.Intro) Intro(p, d, sec++);
            if (c.Standards) Standards(d, sec++);
            if (c.Materials) Materials(p, d, sec++);
            if (c.Loads) { ModelSection(p, r, d, sec++); LoadsSection(p, r, d, sec++); }
            else if (c.WindSeismic) LateralOnlySection(p, r, d, sec++);
            if (c.Forces && r != null)
            {
                int fn = sec++;
                ForcesSummary(p, r, d, fn);
                if (o.IncludeForceTables) ForceEnvelopeSection(p, r, d, fn, o.MaxForceRows);
            }
            if (c.Beams) BeamsSection(p, s, d, sec++);
            if (c.Columns) ColumnsSection(p, s, d, sec++);
            if (c.Slabs) SlabsSection(p, s, d, sec++);
            if (c.Stairs && s.Stairs != null && s.Stairs.Count > 0) StairsSection(p, s, d, sec++);   // cầu thang (CalculationReport.Stair.cs)
            if (c.Geotech && p.Boreholes != null && p.Boreholes.Count > 0) GeotechReport.Section(p, s, d, sec++);
            if (c.Foundation)
            {
                if (s.Footings.Count > 0 || (s.PileCaps.Count == 0 && s.StripFootings.Count == 0)) FootingsSection(p, s, d, sec++);
                if (s.PileCaps.Count > 0) PileCapsSection(p, s, d, sec++);
                if (s.StripFootings.Count > 0) StripFootingsSection(p, s, d, sec++);
            }
            if (c.Steel)
            {
                RebarStatsSection(p, s, Q(), d, sec++);
                if (s.Steel.Count > 0) SteelSection(p, s, d, sec++);
            }
            if (c.Quantities)
            {
                var qq = Q();
                if (qq != null) QuantityReport.WriteWordChapter(p, qq, d, sec++);
            }
            ConclusionAuto(p, r, s, d, sec++);
            Appendix(r, s, d);
            if (c.Appendix) AppendixReport.Write(p, d);
            d.Save(path);
        }
        finally
        {
            _auto = null;
            DocxWriter.DefaultFont = prevFont;
        }
    }

    // ------------------------------------------------------------------
    private static void CoverAuto(Project p, ReportOptions o, DocxWriter d)
    {
        string name = string.IsNullOrWhiteSpace(o.ProjectName) ? p.Name : o.ProjectName;
        if (!string.IsNullOrWhiteSpace(o.DesignCompany)) d.Paragraph(o.DesignCompany.ToUpperInvariant(), true, false, ParaAlign.Center);
        d.EmptyLine().EmptyLine().EmptyLine().EmptyLine();
        d.Title("THUYẾT MINH TÍNH TOÁN KẾT CẤU", 20);
        d.EmptyLine();
        d.Title("CÔNG TRÌNH: " + (name ?? "").ToUpperInvariant(), 15);
        d.Paragraph("HẠNG MỤC: KẾT CẤU", true, false, ParaAlign.Center);
        if (!string.IsNullOrWhiteSpace(o.Location)) d.Paragraph("Địa điểm xây dựng: " + o.Location, false, false, ParaAlign.Center);
        if (!string.IsNullOrWhiteSpace(o.Owner)) d.Paragraph("Chủ đầu tư: " + o.Owner, false, false, ParaAlign.Center);
        d.EmptyLine().EmptyLine().EmptyLine();
        if (!string.IsNullOrWhiteSpace(o.ChiefDesigner) || !string.IsNullOrWhiteSpace(o.Preparer) || !string.IsNullOrWhiteSpace(o.Checker))
            d.Table(new[] { "CHỦ TRÌ THIẾT KẾ", "NGƯỜI LẬP", "KIỂM TRA" },
                new[] { new[] { "\n\n\n" + (o.ChiefDesigner ?? ""), "\n\n\n" + (o.Preparer ?? ""), "\n\n\n" + (o.Checker ?? "") } }, new[] { 5.5, 5.5, 5.5 }, 12);
        d.EmptyLine().EmptyLine();
        if (!string.IsNullOrWhiteSpace(o.DesignCompany)) d.Paragraph("Đơn vị thiết kế: " + o.DesignCompany, false, false, ParaAlign.Center);
        string place = !string.IsNullOrWhiteSpace(p.LocationProvince) ? p.LocationProvince : "";
        string date = o.DateText();
        var parts = date.Split('/', '-', '.');
        string dateLine = parts.Length == 3 ? $"ngày {parts[0]} tháng {parts[1]} năm {parts[2]}" : "ngày " + date;
        d.Paragraph((place.Length > 0 ? place + ", " : "") + dateLine, false, true, ParaAlign.Center);
        d.Paragraph("Phần mềm tính toán: Kết cấu VXT", false, true, ParaAlign.Center);
        d.PageBreak();
    }

    // ------------------------------------------------------------------
    /// <summary>Chương riêng gió + động đất khi không chọn chương tải trọng.</summary>
    private static void LateralOnlySection(Project p, AnalysisResults r, DocxWriter d, int n)
    {
        d.Heading($"{n}. TẢI TRỌNG GIÓ VÀ ĐỘNG ĐẤT", 1);
        var w = p.Wind;
        d.Heading($"{n}.1. Tải trọng gió", 2);
        d.Paragraph($"Gió tĩnh theo TCVN 2737:2023: W0 = {N(w.W0, "0.00")} kN/m² (QCVN 02:2022/BXD), dạng địa hình {w.Terrain}, hệ số khí động c = {N(w.Cp, "0.##")}, γf = {N(w.Gamma, "0.##")}.");
        var lf = r?.LateralForces ?? new List<StoryLateralForce>();
        if (lf.Count > 0)
            d.Table(new[] { "Trường hợp", "Tầng", "Cao độ (m)", "Fx (kN)", "Fy (kN)", "Thông số" },
                lf.Select(x => new[] { x.Pattern, x.Story, N(x.Elevation), N(x.Fx), N(x.Fy), x.Note }), new[] { 2.2, 1.8, 1.8, 1.8, 1.8, 7.1 }, 10);
        if (p.Seismic.Enabled)
        {
            d.Heading($"{n}.2. Tải trọng động đất", 2);
            var sm = p.Seismic;
            d.Paragraph($"Theo TCVN 9386:2012 (tĩnh lực ngang tương đương): agR = {N(sm.AgR, "0.0000")}g, nền loại {sm.Soil}, γI = {N(sm.ImportanceFactor, "0.##")}, q = {N(sm.Q, "0.##")}.");
        }
    }

    // ------------------------------------------------------------------
    /// <summary>Bảng nội lực bao (TTGH I) từng thanh – mục con cuối của chương nội lực.</summary>
    private static void ForceEnvelopeSection(Project p, AnalysisResults r, DocxWriter d, int n, int maxRows)
    {
        d.Heading($"{n}.4. Bảng nội lực bao các thanh (TTGH I)", 2);
        var combos = r.DesignCombos();
        var rows = new List<string[]>();
        int total = 0;
        var order = p.Stories.OrderBy(t => t.Elevation).Select(t => t.Name).ToList();
        foreach (var f in p.Frames.OrderBy(f => order.IndexOf(f.Story)).ThenBy(f => f.Kind).ThenBy(f => f.Label, StringComparer.Ordinal))
        {
            FrameForces mx = null, mn = null;
            try { (mx, mn) = r.Envelope(f.Id, combos); } catch (Exception) { }
            if (mx == null || mn == null || mx.Count == 0) continue;
            total++;
            if (rows.Count >= Math.Max(10, maxRows)) continue;
            double pMin = double.MaxValue, pMax = double.MinValue, v2 = 0, m3n = double.MaxValue, m3x = double.MinValue, m2 = 0, t = 0;
            for (int i = 0; i < mx.Count && i < mn.Count; i++)
            {
                pMin = Math.Min(pMin, mn.P(i)); pMax = Math.Max(pMax, mx.P(i));
                v2 = Math.Max(v2, Math.Max(Math.Abs(mx.V2(i)), Math.Abs(mn.V2(i))));
                m3n = Math.Min(m3n, mn.M3(i)); m3x = Math.Max(m3x, mx.M3(i));
                m2 = Math.Max(m2, Math.Max(Math.Abs(mx.M2(i)), Math.Abs(mn.M2(i))));
                t = Math.Max(t, Math.Max(Math.Abs(mx.T(i)), Math.Abs(mn.T(i))));
            }
            rows.Add(new[] { f.Story, string.IsNullOrEmpty(f.Label) ? f.Id.ToString(Inv) : f.Label, KindName(f.Kind), f.Section, N(pMin, "0.0"), N(pMax, "0.0"), N(v2, "0.0"), N(m3n, "0.0"), N(m3x, "0.0"), N(m2, "0.0"), N(t, "0.0") });
        }
        d.Paragraph("Giá trị bao lớn nhất / nhỏ nhất trên các mặt cắt của thanh theo các tổ hợp TTGH I (kN, kN.m). P âm là nén.");
        d.Table(new[] { "Tầng", "Thanh", "Loại", "Tiết diện", "P min", "P max", "|V2| max", "M3 min", "M3 max", "|M2| max", "|T| max" },
            rows, new[] { 1.3, 1.3, 1.6, 1.6, 1.3, 1.3, 1.3, 1.4, 1.4, 1.4, 1.3 }, 8);
        if (total > rows.Count)
            d.Paragraph($"Bảng trích {rows.Count}/{total} thanh; bảng đầy đủ xem file Excel bảng tính (Element Forces - Frames).", false, true);
    }

    // ------------------------------------------------------------------
    private static void RebarStatsSection(Project p, DesignSummary s, QuantityResult q, DocxWriter d, int n)
    {
        d.Heading($"{n}. THỐNG KÊ CỐT THÉP", 1);
        var rows = RebarSchedule.BuildRows(s);
        if (rows.Count == 0) { d.Paragraph("Chưa có bố trí cốt thép (chưa thiết kế cấu kiện BTCT)."); return; }
        d.Paragraph("Khối lượng cốt thép lấy theo bảng thống kê cốt thép của bộ bản vẽ (số hiệu, hình dạng, chiều dài từng thanh); trọng lượng đơn vị 0,00617·Ø² kg/m. Chưa kể hao hụt, thép chờ, thép cọc.");
        var byDia = RebarSchedule.ByDiameter(rows);
        d.Table(new[] { "Đường kính", "Tổng chiều dài (m)", "Trọng lượng (kg)" },
            byDia.Select(x => new[] { "Ø" + x.Dia.ToString(Inv), N(x.LengthM), N(x.WeightKg) }).Concat(new[] { new[] { "TỔNG", N(byDia.Sum(x => x.LengthM)), N(byDia.Sum(x => x.WeightKg)) } }),
            new[] { 4.0, 6.0, 6.0 });
        if (q != null && q.RebarByCategory.Count > 0) QuantityReport.WordTable(d, QuantityReport.RebarByCategory(q), 10);
        else
        {
            var g = RebarSchedule.Groups(rows);
            d.Paragraph($"Nhóm Ø≤10: {N(g.WeightLe10)} kg; 10<Ø≤18: {N(g.Weight10To18)} kg; Ø>18: {N(g.WeightGt18)} kg; tổng {N(g.Total)} kg.");
        }
    }

    // ------------------------------------------------------------------
    // VÍ DỤ BỔ SUNG (ExamplesPerType = 2…3)
    // ------------------------------------------------------------------
    private static List<T> PickDistinct<T>(IEnumerable<T> ordered, Func<T, string> key, int count)
    {
        var list = ordered.ToList();
        var res = new List<T>();
        var seen = new HashSet<string>();
        foreach (var x in list) { if (res.Count >= count) break; if (seen.Add(key(x) ?? "")) res.Add(x); }
        foreach (var x in list) { if (res.Count >= count) break; if (!res.Contains(x)) res.Add(x); }
        return res;
    }

    private static void FlexureLines(DocxWriter d, string label, double M, double b, double h0, double Rb, double Rs, double asReq, double asProv, string text, string combo)
    {
        double Mabs = Math.Abs(M);
        d.Paragraph($"{label}: M = {N(Mabs)} kN.m{(string.IsNullOrEmpty(combo) ? "" : $" (tổ hợp {combo})")}.", true);
        if (Mabs < 1e-6 || Rb <= 0 || Rs <= 0 || b <= 0 || h0 <= 0) { d.Equation("M ≈ 0 → đặt thép cấu tạo."); return; }
        double am = Mabs / (Rb * b * h0 * h0);
        d.Equation($"αm = M/(Rb·b·h0²) = {N(Mabs)}/({MPa(Rb)}×10³ × {N(b, "0.000")} × {N(h0, "0.000")}²) = {N(am, "0.0000")}");
        if (am < 0.5)
        {
            double xi = 1 - Math.Sqrt(1 - 2 * am);
            double As = xi * Rb * b * h0 / Rs;
            d.Equation($"ξ = 1 − √(1 − 2αm) = {N(xi, "0.0000")};  As = ξ·Rb·b·h0/Rs = {N(As * 1e6, "0")} mm²");
        }
        if (asReq <= 0 && asProv <= 0) { if (!string.IsNullOrEmpty(text)) d.Equation("Bố trí: " + text); return; }
        d.Equation($"As yêu cầu = {Cm2(asReq)} cm²; bố trí {(string.IsNullOrEmpty(text) ? "-" : text)} (As = {Cm2(asProv)} cm²) → {(asProv + 1e-9 >= asReq ? "đạt" : "KHÔNG ĐẠT")}");
    }

    private static void ExtraBeamExamples(Project p, DesignSummary s, DocxWriter d, int n)
    {
        int k = ExtraExamples;
        if (k <= 0 || s.Beams.Count < 2) return;
        var cs = p.Concrete;
        Material conc = SafeMat(p, cs.ConcreteBeam), steel = SafeMat(p, cs.RebarMain);
        if (conc == null || steel == null) return;
        static double Mx(BeamLineDesign b) => b.Spans.Count == 0 ? 0 : b.Spans.Max(x => Math.Max(Math.Abs(x.MSpan), Math.Max(Math.Abs(x.MLeft), Math.Abs(x.MRight))));
        var picks = PickDistinct(s.Beams.Skip(1).Where(b => b.Spans.Count > 0).OrderByDescending(Mx), b => b.Section, k);
        int i = 2;
        foreach (var b in picks)
        {
            var sp = b.Spans.OrderByDescending(x => Math.Max(Math.Abs(x.MSpan), Math.Max(Math.Abs(x.MLeft), Math.Abs(x.MRight)))).First();
            d.Heading($"{n}.1.{i}. Ví dụ {i}: dầm {b.Name} (tiết diện {Mm(b.B)}×{Mm(b.H)} mm, nhịp {sp.LeftSupport} – {sp.RightSupport}, L = {N(sp.L)} m)", 3);
            double a = cs.CoverBeam + 0.010, h0 = b.H - a;
            d.Equation($"Bê tông {conc.Name}: Rb = {MPa(conc.Rb)} MPa; thép {steel.Name}: Rs = {MPa(steel.Rs)} MPa;  h0 = {Mm(b.H)} − {Mm(a)} = {Mm(h0)} mm");
            bool left = Math.Abs(sp.MLeft) >= Math.Abs(sp.MRight);
            FlexureLines(d, left ? "Gối trái (căng thớ trên)" : "Gối phải (căng thớ trên)", left ? sp.MLeft : sp.MRight, b.B, h0, conc.Rb, steel.Rs,
                left ? sp.AsTopLeftReq : sp.AsTopRightReq, left ? sp.AsTopLeftProv : sp.AsTopRightProv, left ? sp.TopLeftText : sp.TopRightText, left ? sp.ComboMLeft : sp.ComboMRight);
            FlexureLines(d, "Giữa nhịp (căng thớ dưới)", sp.MSpan, b.B, h0, conc.Rb, steel.Rs, sp.AsBotReq, sp.AsBotProv, sp.BotText, sp.ComboMSpan);
            double Qult = 0.3 * conc.Rb * b.B * h0, Qb1 = 0.5 * conc.Rbt * b.B * h0;
            d.Equation($"Cắt: Qmax = {N(sp.QMax)} kN ≤ 0.3·Rb·b·h0 = {N(Qult)} kN → {(sp.QMax <= Qult ? "thoả mãn" : "KHÔNG thoả mãn")};  Qb,min = 0.5·Rbt·b·h0 = {N(Qb1)} kN"
                       + (sp.StirrupDia > 0 ? $";  đai Ø{sp.StirrupDia}a{Mm(sp.StirrupSupport)}/{Mm(sp.StirrupMid)}" : ""));
            if (sp.DeflectionLimit > 0)
                d.Equation($"Độ võng f = {N(sp.Deflection * 1000, "0.0")} mm {(sp.Deflection <= sp.DeflectionLimit ? "≤" : ">")} [f] = {N(sp.DeflectionLimit * 1000, "0.0")} mm");
            d.Paragraph($"Kết luận: dầm {b.Name} {(b.Ok ? "đạt" : "KHÔNG ĐẠT")} (tỷ số lớn nhất {N(b.MaxRatio, "0.00")}).", false, true);
            i++;
        }
    }

    private static void ExtraColumnExamples(Project p, DesignSummary s, DocxWriter d, int n)
    {
        int k = ExtraExamples;
        if (k <= 0 || s.Columns.Count < 2) return;
        var cs = p.Concrete;
        var picks = PickDistinct(s.Columns.Skip(1).Where(c => c.Stories.Count > 0).OrderByDescending(c => c.Stories.Max(x => x.N)),
            c => { var x = c.Stories.OrderByDescending(t => t.N).First(); return $"{Mm(x.B)}x{Mm(x.H)}"; }, k);
        int i = 2;
        foreach (var c in picks)
        {
            var st = c.Stories.OrderByDescending(x => x.N).First();
            d.Heading($"{n}.1.{i}. Ví dụ {i}: cột {c.Name}, tầng {st.Story} (tiết diện {Mm(st.B)}×{Mm(st.H)} mm)", 3);
            double l0 = cs.ColumnLengthFactor * st.Length;
            d.Equation($"N = {N(st.N)} kN;  M2 = {N(st.M2)} kN.m;  M3 = {N(st.M3)} kN.m (tổ hợp {st.Combo});  η2 = {N(st.Eta2, "0.000")}, η3 = {N(st.Eta3, "0.000")}");
            d.Equation($"l0 = ψ·H = {N(cs.ColumnLengthFactor, "0.##")} × {N(st.Length)} = {N(l0)} m;  λh = {N(st.H > 0 ? l0 / st.H : 0, "0.0")};  λb = {N(st.B > 0 ? l0 / st.B : 0, "0.0")}");
            d.Equation($"As yêu cầu = {Cm2(st.AsReq)} cm²; bố trí {st.TotalBars}Ø{st.Dia} (As = {Cm2(st.AsProv)} cm², μ = {N(st.Ratio, "0.00")}%; μmin = {N(cs.MinRatioColumn, "0.##")}%, μmax = {N(cs.MaxRatioColumn, "0.##")}%)");
            d.Equation($"Hệ số sử dụng (biểu đồ tương tác) = {N(st.Utilization, "0.000")} {(st.Utilization <= 1 + 1e-6 ? "≤ 1 → đạt" : "> 1 → KHÔNG ĐẠT")}"
                       + (st.StirrupDia > 0 ? $";  đai Ø{st.StirrupDia}a{Mm(st.StirrupSpacing)} (đầu cột a{Mm(st.StirrupSpacingEnd)})" : ""));
            i++;
        }
    }

    private static void ExtraSlabExamples(Project p, DesignSummary s, DocxWriter d, int n)
    {
        int k = ExtraExamples;
        if (k <= 0 || s.Slabs.Count < 2) return;
        Material conc = SafeMat(p, p.Concrete.ConcreteSlab), st = SafeMat(p, p.Concrete.RebarStirrup);
        if (conc == null || st == null) return;
        var first = s.Slabs.OrderByDescending(x => Math.Abs(x.M1)).First();
        var picks = PickDistinct(s.Slabs.Where(x => !ReferenceEquals(x, first)).OrderByDescending(x => Math.Abs(x.M1)), x => $"{N(x.Lx)}x{N(x.Ly)}x{Mm(x.Thickness)}", k);
        int i = 2;
        foreach (var x in picks)
        {
            d.Heading($"{n}.1.{i}. Ví dụ {i}: ô sàn {x.Name} ({N(x.Lx)} × {N(x.Ly)} m, hb = {Mm(x.Thickness)} mm)", 3);
            double h0 = x.Thickness - p.Concrete.CoverSlab - 0.004;
            d.Equation($"{(x.TwoWay ? "Bản kê bốn cạnh" : "Bản dầm")}, liên kết {x.EdgeCondition}; q = {N(x.Q)} kN/m²;  M1 = {N(x.M1)}, M2 = {N(x.M2)}, MI = {N(x.MI)}, MII = {N(x.MII)} kN.m/m");
            FlexureLines(d, "Nhịp phương ngắn (dải 1 m)", x.M1, 1.0, h0, conc.Rb, st.Rs, 0, 0, x.BotX, "");
            if (Math.Abs(x.MI) > 1e-6) FlexureLines(d, "Gối phương ngắn (dải 1 m)", x.MI, 1.0, h0, conc.Rb, st.Rs, 0, 0, x.TopX, "");
            d.Equation($"Bố trí: lớp dưới {x.BotX} / {x.BotY}; mũ gối {x.TopX} / {x.TopY} → {(x.Ok ? "đạt" : "KHÔNG ĐẠT")}");
            i++;
        }
    }

    private static void ExtraFootingExamples(Project p, DesignSummary s, DocxWriter d, int n)
    {
        int k = ExtraExamples;
        if (k <= 0 || s.Footings.Count < 2) return;
        var first = s.Footings.OrderByDescending(x => x.N).First();
        var picks = PickDistinct(s.Footings.Where(x => !ReferenceEquals(x, first)).OrderByDescending(x => x.N), x => $"{N(x.L)}x{N(x.B)}", k);
        int i = 2;
        foreach (var f in picks)
        {
            d.Heading($"{n}.1.{i}. Ví dụ {i}: móng {f.Name} (nút {f.NodeId})", 3);
            d.Equation($"N = {N(f.N)} kN; Mx = {N(f.Mx)} kN.m; My = {N(f.My)} kN.m (tổ hợp {f.Combo});  L × B × H = {N(f.L)} × {N(f.B)} × {N(f.H)} m");
            d.Equation($"Ptb = {N(f.Ptb, "0.0")} {(f.Ptb <= f.R ? "≤" : ">")} R = {N(f.R, "0.0")};  Pmax = {N(f.Pmax, "0.0")} {(f.Pmax <= 1.2 * f.R ? "≤" : ">")} 1.2R = {N(1.2 * f.R, "0.0")};  Pmin = {N(f.Pmin, "0.0")} kN/m²");
            d.Equation($"Thép đáy: phương L {f.BarsL} (As = {Cm2(f.AsL)} cm²), phương B {f.BarsB} (As = {Cm2(f.AsB)} cm²)");
            foreach (var c in f.Checks)
                d.Equation($"{c.Name}: {N(c.Demand)} / {N(c.Capacity)} {c.Unit} → {N(c.Ratio, "0.000")} – {OkText(c.Ok)}");
            i++;
        }
    }

    // ------------------------------------------------------------------
    // KẾT LUẬN VÀ KIẾN NGHỊ
    // ------------------------------------------------------------------
    /// <summary>Lệch tầng lớn nhất theo tầng: (tầng, Δ/h, tổ hợp, cột, Δ mm).</summary>
    public static List<(string Story, double Ratio, string Combo, string Member, double Delta, double H)> StoryDrifts(Project p, AnalysisResults r)
    {
        var res = new Dictionary<string, (string Story, double Ratio, string Combo, string Member, double Delta, double H)>();
        if (r == null) return new();
        var names = r.ServiceCombos().ToList();
        if (names.Count == 0) names = p.Patterns.Where(t => t.Type == LoadType.Wind || t.Type == LoadType.Seismic).Select(t => t.Name).ToList();
        foreach (var cn in names)
        {
            CaseResult cr;
            try { cr = r.Get(cn); } catch (Exception) { continue; }
            if (cr == null) continue;
            foreach (var f in p.Frames.Where(f => f.IsVerticalKind))
            {
                Node a, b;
                try { a = p.Node(f.NodeI); b = p.Node(f.NodeJ); } catch (KeyNotFoundException) { continue; }
                var lo = a.Z <= b.Z ? a : b; var hi = a.Z <= b.Z ? b : a;
                double h = hi.Z - lo.Z;
                if (h < 0.5 || !cr.Disp.TryGetValue(hi.Id, out var dh)) continue;
                cr.Disp.TryGetValue(lo.Id, out var dl);
                double dx = Math.Abs(dh[0] - (dl?[0] ?? 0)), dy = Math.Abs(dh[1] - (dl?[1] ?? 0));
                double delta = Math.Max(dx, dy), ratio = delta / h;
                string st = string.IsNullOrEmpty(f.Story) ? hi.Story : f.Story;
                if (!res.TryGetValue(st, out var cur) || ratio > cur.Ratio)
                    res[st] = (st, ratio, cn, string.IsNullOrEmpty(f.Label) ? f.Id.ToString(Inv) : f.Label, delta, h);
            }
        }
        var order = p.Stories.OrderBy(t => t.Elevation).Select(t => t.Name).ToList();
        return res.Values.OrderBy(v => order.IndexOf(v.Story)).ToList();
    }

    private static void ConclusionAuto(Project p, AnalysisResults r, DesignSummary s, DocxWriter d, int n)
    {
        d.Heading($"{n}. KẾT LUẬN VÀ KIẾN NGHỊ", 1);
        var all = s.All.ToList();
        var bad = all.Where(m => !m.Ok).ToList();
        d.Heading($"{n}.1. Kết quả kiểm tra cấu kiện", 2);
        d.Table(new[] { "Loại cấu kiện", "Số lượng", "Không đạt", "Tỷ số lớn nhất" }, new[]
        {
            Row("Dầm", s.Beams), Row("Cột", s.Columns), Row("Sàn", s.Slabs), Row("Móng", s.Footings), Row("Đài cọc", s.PileCaps), Row("Móng băng", s.StripFootings), Row("Kết cấu thép", s.Steel)
        }.Where(x => x != null), new[] { 5.0, 3.5, 3.5, 4.5 });
        var gov = all.OrderByDescending(m => m.MaxRatio).FirstOrDefault();
        if (all.Count == 0) d.Paragraph("Chưa có kết quả thiết kế cấu kiện.");
        else
        {
            d.Paragraph($"Tổng số {all.Count} cấu kiện được kiểm tra; {bad.Count} cấu kiện không đạt." +
                        (gov != null ? $" Cấu kiện có tỷ số sử dụng lớn nhất: {gov.Name}{(string.IsNullOrEmpty(gov.Story) ? "" : " (" + gov.Story + ")")} – {N(gov.MaxRatio, "0.00")}." : ""));
            foreach (var m in bad.Take(60))
            {
                var bc = string.Join("; ", m.Checks.Where(c => !c.Ok).Select(c => $"{c.Name} ({N(c.Ratio, "0.00")})"));
                var err = m.Messages.Where(x => x.StartsWith("LỖI")).ToList();
                d.Bullet($"{m.Name}{(string.IsNullOrEmpty(m.Story) ? "" : " – " + m.Story)}: {bc}{(err.Count > 0 ? " " + string.Join(" ", err) : "")}");
            }
            if (bad.Count > 60) d.Paragraph($"… và {bad.Count - 60} cấu kiện khác (xem bảng tổng hợp từng chương).", false, true);
        }

        // ---- chuyển vị ngang ----
        d.Heading($"{n}.2. Chuyển vị ngang và độ lún", 2);
        bool steelFrame = p.Frames.Count(f => !f.IsConcrete) > p.Frames.Count(f => f.IsConcrete);
        double lim = steelFrame ? 1.0 / Math.Max(1, p.Steel.ColumnDrift) : DriftLimitRc;
        var drifts = StoryDrifts(p, r);
        double maxDrift = drifts.Count > 0 ? drifts.Max(x => x.Ratio) : 0;
        if (drifts.Count > 0)
        {
            d.Paragraph($"Chuyển vị ngang lệch tầng lớn nhất theo các tổ hợp tiêu chuẩn (TTGH II), giới hạn tham khảo Δ/h ≤ 1/{N(1 / lim, "0")}" +
                        (steelFrame ? " (đỉnh cột khung thép, TCVN 5575)." : " (nhà khung BTCT nhiều tầng, TCVN 5574:2018 / TCVN 2737:2023)."));
            d.Table(new[] { "Tầng", "Cột", "Tổ hợp", "Δ (mm)", "h (m)", "Δ/h", "Giới hạn", "Kết luận" },
                drifts.Select(x => new[] { x.Story, x.Member, x.Combo, N(x.Delta * 1000, "0.00"), N(x.H), x.Ratio > 1e-12 ? "1/" + N(1 / x.Ratio, "0") : "0", "1/" + N(1 / lim, "0"), OkText(x.Ratio <= lim + 1e-12) }),
                new[] { 1.8, 1.6, 2.4, 1.8, 1.6, 2.0, 2.0, 2.3 }, 10);
        }
        else d.Paragraph(r == null ? "Chưa có kết quả phân tích – chưa kiểm tra chuyển vị ngang." : "Không có kết quả chuyển vị ngang theo tổ hợp tiêu chuẩn.");
        var lun = s.Footings.Cast<MemberDesignBase>().Concat(s.StripFootings).Concat(s.PileCaps)
            .SelectMany(m => m.Checks.Where(c => c.Name.StartsWith("Độ lún")).Select(c => (m, c))).ToList();
        double sMax = 0, sLim = 8; string sMem = "";
        if (lun.Count > 0)
        {
            var w = lun.OrderByDescending(x => x.c.Demand).First();
            sMax = w.c.Demand; sLim = w.c.Capacity > 0 ? w.c.Capacity : 8; sMem = w.m.Name;
            d.Paragraph($"Độ lún lớn nhất: S = {N(sMax, "0.00")} cm tại {sMem} {(sMax <= sLim ? "≤" : ">")} Sgh = {N(sLim, "0.0")} cm (TCVN 9362:2012) → {OkText(sMax <= sLim)}.");
        }
        else d.Paragraph("Chưa tính độ lún móng (cần số liệu hố khoan địa chất để tính lún theo phương pháp cộng lún từng lớp).");

        // ---- kết luận ----
        d.Heading($"{n}.3. Kết luận", 2);
        bool driftOk = maxDrift <= lim + 1e-12, setOk = lun.Count == 0 || sMax <= sLim;
        if (all.Count > 0 && bad.Count == 0 && driftOk && setOk)
            d.Paragraph("Với các số liệu đầu vào và giả thiết tính toán nêu trong thuyết minh, tất cả cấu kiện được kiểm tra đều thoả mãn điều kiện khả năng chịu lực (TTGH I) và điều kiện sử dụng (TTGH II); chuyển vị ngang và độ lún nằm trong giới hạn cho phép. Kết cấu đảm bảo đủ khả năng chịu lực.");
        else
        {
            if (bad.Count > 0) d.Paragraph($"Có {bad.Count} cấu kiện KHÔNG ĐẠT – cần điều chỉnh tiết diện, cấp bê tông hoặc bố trí lại cốt thép trước khi phát hành hồ sơ.", true);
            if (!driftOk) d.Paragraph($"Chuyển vị ngang lệch tầng lớn nhất 1/{N(1 / Math.Max(maxDrift, 1e-12), "0")} vượt giới hạn 1/{N(1 / lim, "0")} – cần tăng độ cứng ngang (tăng tiết diện cột, dầm khung hoặc bổ sung vách).", true);
            if (!setOk) d.Paragraph($"Độ lún {N(sMax, "0.00")} cm vượt giới hạn {N(sLim, "0.0")} cm – cần tăng kích thước móng hoặc chuyển phương án móng cọc.", true);
            if (all.Count == 0) d.Paragraph("Chưa có kết quả thiết kế – cần chạy phân tích và thiết kế trước khi lập thuyết minh.", true);
        }

        // ---- kiến nghị ----
        d.Heading($"{n}.4. Kiến nghị", 2);
        var notes = new List<string>();
        if (p.Boreholes == null || p.Boreholes.Count == 0)
            notes.Add(p.Soil != null && p.Soil.AssumeGoodSoil
                ? "Thiết kế móng đang dùng chỉ tiêu “nền đất tốt giả định”: cần khảo sát địa chất (hố khoan, SPT) và kiểm tra lại sức chịu tải, độ lún trước khi thi công."
                : "Chưa có số liệu khảo sát địa chất: phương án móng chỉ mang tính tham khảo, cần khảo sát địa chất công trình.");
        if (!p.Seismic.Enabled) notes.Add("Chưa xét tải trọng động đất – kiểm tra yêu cầu kháng chấn của địa điểm xây dựng theo TCVN 9386:2012 (gia tốc nền agR).");
        if (r != null && r.Warnings.Count > 0) notes.Add($"Có {r.Warnings.Count} cảnh báo phân tích (xem phụ lục) – kiểm tra lại mô hình, liên kết và tải trọng.");
        if (s.Warnings.Count > 0) notes.Add($"Có {s.Warnings.Count} cảnh báo thiết kế (xem phụ lục).");
        notes.Add("Người thiết kế kiểm tra lại số liệu đầu vào (kích thước, tải trọng, vật liệu), sơ đồ tính và kết quả trước khi sử dụng; đối chiếu điều khoản của phiên bản tiêu chuẩn đang áp dụng.");
        notes.Add("Vật liệu (bê tông, cốt thép) phải được thí nghiệm kiểm tra cường độ theo quy định; cấp độ bền bê tông và mác thép thi công không thấp hơn thiết kế.");
        notes.Add("Thi công và nghiệm thu theo TCVN 4453:1995 (kết cấu BTCT toàn khối) và TCVN 9361:2012 (công tác nền móng); bảo dưỡng bê tông, tháo ván khuôn đúng thời gian quy định.");
        notes.Add("Khi thay đổi công năng, tải trọng hoặc kiến trúc so với hồ sơ, phải tính toán kiểm tra lại kết cấu.");
        foreach (var t in notes) d.Bullet(t);
        d.Paragraph("Thuyết minh được lập tự động bằng chương trình Kết cấu VXT.", false, true);
    }
}
