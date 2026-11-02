using System.Globalization;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Xuất thuyết minh tính toán kết cấu (.docx) tiếng Việt.
/// Số thập phân dùng dấu chấm (định dạng invariant). Đơn vị: kN, kN.m, mm, cm², MPa.</summary>
public static partial class CalculationReport   // thuyết minh tự động: CalculationReport.Auto.cs
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string N(double v, string fmt = "0.00") => double.IsFinite(v) ? v.ToString(fmt, Inv) : "-";
    private static string Mm(double m) => N(m * 1000, "0");
    private static string Cm2(double m2) => N(m2 * 1e4, "0.00");
    private static string MPa(double kNm2) => N(kNm2 / 1000, "0.##");
    private static string OkText(bool ok) => ok ? "Đạt" : "KHÔNG ĐẠT";

    public static void Export(Project p, AnalysisResults r, DesignSummary s, string path)
    {
        if (p == null) throw new ArgumentNullException(nameof(p));
        s ??= new DesignSummary();
        var d = new DocxWriter();
        Cover(p, d);
        int sec = 1;
        Intro(p, d, sec++);
        Standards(d, sec++);
        Materials(p, d, sec++);
        ModelSection(p, r, d, sec++);
        LoadsSection(p, r, d, sec++);
        if (r != null) ForcesSummary(p, r, d, sec++);
        BeamsSection(p, s, d, sec++);
        ColumnsSection(p, s, d, sec++);
        SlabsSection(p, s, d, sec++);
        if (s.Stairs != null && s.Stairs.Count > 0) StairsSection(p, s, d, sec++);   // cầu thang (CalculationReport.Stair.cs)
        if (p.Boreholes != null && p.Boreholes.Count > 0) GeotechReport.Section(p, s, d, sec++);   // Report/GeotechReport.cs
        if (s.Footings.Count > 0 || (s.PileCaps.Count == 0 && s.StripFootings.Count == 0)) FootingsSection(p, s, d, sec++);
        if (s.PileCaps.Count > 0) PileCapsSection(p, s, d, sec++);
        if (s.StripFootings.Count > 0) StripFootingsSection(p, s, d, sec++);
        if (s.Steel.Count > 0) SteelSection(p, s, d, sec++);
        Conclusion(s, d, sec++);
        Appendix(r, s, d);
        AppendixReport.Write(p, d);   // PHỤ LỤC TÍNH TOÁN – Report/AppendixReport.cs
        d.Save(path);
    }

    // ------------------------------------------------------------------
    private static void Cover(Project p, DocxWriter d)
    {
        d.EmptyLine().EmptyLine().EmptyLine();
        d.Title("THUYẾT MINH TÍNH TOÁN KẾT CẤU", 20);
        d.EmptyLine();
        d.Title("CÔNG TRÌNH: " + (p.Name ?? "").ToUpperInvariant(), 15);
        if (!string.IsNullOrWhiteSpace(p.Location)) d.Paragraph("Địa điểm: " + p.Location, false, false, ParaAlign.Center);
        if (!string.IsNullOrWhiteSpace(p.Owner)) d.Paragraph("Chủ đầu tư: " + p.Owner, false, false, ParaAlign.Center);
        d.EmptyLine().EmptyLine();
        if (!string.IsNullOrWhiteSpace(p.Designer)) d.Paragraph("Người thiết kế: " + p.Designer, false, false, ParaAlign.Center);
        d.Paragraph("Phần mềm tính toán: Kết cấu VXT", false, true, ParaAlign.Center);
        d.Paragraph("Ngày lập: " + DateTime.Now.ToString("dd/MM/yyyy", Inv), false, true, ParaAlign.Center);
        d.PageBreak();
    }

    private static void Intro(Project p, DocxWriter d, int n)
    {
        d.Heading($"{n}. GIỚI THIỆU CÔNG TRÌNH", 1);
        d.Paragraph($"Công trình: {p.Name}." + (string.IsNullOrWhiteSpace(p.Location) ? "" : $" Địa điểm xây dựng: {p.Location}."));
        var stories = p.Stories.OrderBy(t => t.Elevation).ToList();
        int nConc = p.Frames.Count(f => f.IsConcrete), nSteel = p.Frames.Count - nConc;
        string kind = nSteel > nConc ? "khung thép tiền chế (khung zamil) tiết diện chữ I tổ hợp" : "khung bê tông cốt thép toàn khối";
        d.Paragraph($"Giải pháp kết cấu: {kind}. Mô hình gồm {p.Nodes.Count} nút, {p.Frames.Count} thanh, {p.Slabs.Count} ô sàn, {stories.Count} cao trình tầng.");
        if (stories.Count > 0)
        {
            double H = stories[^1].Elevation - p.BaseElevation;
            d.Paragraph($"Cao độ chân cột (mặt móng): {N(p.BaseElevation, "0.000")} m. Chiều cao công trình tính từ mặt móng: {N(H)} m.");
            d.Table(new[] { "Tầng", "Cao độ sàn (m)", "Chiều cao tầng (m)" },
                stories.Select(t => new[] { t.Name, N(t.Elevation, "0.000"), N(t.Height) }), new[] { 5.0, 5.5, 5.5 });
        }
        var gx = p.GridsX.ToList(); var gy = p.GridsY.ToList();
        if (gx.Count > 1 || gy.Count > 1)
        {
            string sx = gx.Count > 1 ? string.Join(" + ", gx.Zip(gx.Skip(1), (a, b) => N(b.Coord - a.Coord))) : "-";
            string sy = gy.Count > 1 ? string.Join(" + ", gy.Zip(gy.Skip(1), (a, b) => N(b.Coord - a.Coord))) : "-";
            d.Paragraph($"Lưới trục: phương X (trục {string.Join(",", gx.Select(g => g.Name))}): {sx} m; phương Y (trục {string.Join(",", gy.Select(g => g.Name))}): {sy} m.");
        }
    }

    private static void Standards(DocxWriter d, int n)
    {
        d.Heading($"{n}. TIÊU CHUẨN ÁP DỤNG", 1);
        foreach (var t in new[]
        {
            "TCVN 2737:2023 – Tải trọng và tác động – Tiêu chuẩn thiết kế.",
            "TCVN 5574:2018 – Thiết kế kết cấu bê tông và bê tông cốt thép.",
            "TCVN 5575:2024 – Thiết kế kết cấu thép.",
            "TCVN 9386:2012 – Thiết kế công trình chịu động đất.",
            "TCVN 9362:2012 – Tiêu chuẩn thiết kế nền nhà và công trình.",
            "TCVN 10304:2014 – Móng cọc – Tiêu chuẩn thiết kế.",
            "QCVN 02:2022/BXD – Quy chuẩn kỹ thuật quốc gia về số liệu điều kiện tự nhiên dùng trong xây dựng."
        }) d.Bullet(t);
        d.Paragraph("Ghi chú: một số công thức trong chương trình là dạng đơn giản hoá; người thiết kế chịu trách nhiệm đối chiếu với điều khoản cụ thể của phiên bản tiêu chuẩn đang áp dụng.", false, true);
    }

    private static void Materials(Project p, DocxWriter d, int n)
    {
        d.Heading($"{n}. VẬT LIỆU", 1);
        var cs = p.Concrete;
        d.Paragraph($"Bê tông: cột {cs.ConcreteColumn}, dầm {cs.ConcreteBeam}, sàn {cs.ConcreteSlab}, móng {cs.ConcreteFooting}. Cốt thép dọc {cs.RebarMain}, cốt đai/thép sàn {cs.RebarStirrup}.");
        d.Paragraph($"Lớp bê tông bảo vệ: dầm {Mm(cs.CoverBeam)} mm, cột {Mm(cs.CoverColumn)} mm, sàn {Mm(cs.CoverSlab)} mm, móng {Mm(cs.CoverFooting)} mm.");
        var used = new HashSet<string> { cs.ConcreteColumn, cs.ConcreteBeam, cs.ConcreteSlab, cs.ConcreteFooting, cs.RebarMain, cs.RebarStirrup };
        foreach (var fs in p.FrameSections) used.Add(fs.Material);
        foreach (var ss in p.SlabSections) used.Add(ss.Material);
        var mats = new List<Material>();
        foreach (var name in used)
        {
            try { mats.Add(p.Material(name)); } catch (Exception) { }
        }
        var conc = mats.Where(m => m.Kind == MaterialKind.Concrete).OrderBy(m => m.Name).ToList();
        if (conc.Count > 0)
        {
            d.Heading($"{n}.1. Bê tông (TCVN 5574:2018)", 2);
            d.Table(new[] { "Cấp độ bền", "Rb (MPa)", "Rbt (MPa)", "Rb,n (MPa)", "Eb (MPa)", "γ (kN/m³)" },
                conc.Select(m => new[] { m.Name, MPa(m.Rb), MPa(m.Rbt), MPa(m.Rbn), MPa(m.E), N(m.UnitWeight, "0.#") }));
        }
        var reb = mats.Where(m => m.Kind == MaterialKind.Rebar).OrderBy(m => m.Name).ToList();
        if (reb.Count > 0)
        {
            d.Heading($"{n}.2. Cốt thép (TCVN 5574:2018, TCVN 1651:2018)", 2);
            d.Table(new[] { "Mác thép", "Rs (MPa)", "Rsc (MPa)", "Rsw (MPa)", "fy (MPa)", "Es (MPa)" },
                reb.Select(m => new[] { m.Name, MPa(m.Rs), MPa(m.Rsc), MPa(m.Rsw), MPa(m.Fy), MPa(m.E) }));
        }
        var st = mats.Where(m => m.Kind == MaterialKind.Steel).OrderBy(m => m.Name).ToList();
        if (st.Count > 0)
        {
            d.Heading($"{n}.3. Thép hình, thép tấm (TCVN 5575:2024)", 2);
            d.Table(new[] { "Mác thép", "fy (MPa)", "fu (MPa)", "f (MPa)", "E (MPa)", "γ (kN/m³)" },
                st.Select(m => new[] { m.Name, MPa(m.Fy), MPa(m.Fu), MPa(m.F), MPa(m.E), N(m.UnitWeight, "0.#") }));
        }
        d.Heading($"{n}.4. Tiết diện sử dụng", 2);
        var usedSec = new HashSet<string>(p.Frames.Select(f => f.Section));
        d.Table(new[] { "Tiết diện", "Dạng", "Vật liệu", "Kích thước (mm)", "A (cm²)", "I33 (cm⁴)" },
            p.FrameSections.Where(x => usedSec.Contains(x.Name)).Select(x =>
            {
                var sp = x.Props(0);
                string dim = x.Shape switch
                {
                    SectionShape.RectConcrete => $"{Mm(x.B)}×{Mm(x.H)}",
                    SectionShape.WeldedI => $"H{Mm(x.HStart)}{(x.IsTapered ? "÷" + Mm(x.HEnd) : "")}; cánh {Mm(x.BfTop)}×{Mm(x.TfTop)}; bụng {Mm(x.Tw)}",
                    _ => $"h={Mm(x.GDepth)}; b={Mm(x.GWidth)}"
                };
                string shp = x.Shape switch { SectionShape.RectConcrete => "Chữ nhật", SectionShape.WeldedI => "I tổ hợp", _ => "Tổng quát" };
                return new[] { x.Name, shp, x.Material, dim, N(sp.A * 1e4, "0.0"), N(sp.I33 * 1e8, "0") };
            }), new[] { 2.6, 2.2, 2.0, 5.4, 2.0, 2.3 });
    }

    private static void ModelSection(Project p, AnalysisResults r, DocxWriter d, int n)
    {
        d.Heading($"{n}. SƠ ĐỒ TÍNH VÀ PHẦN MỀM", 1);
        d.Paragraph("Kết cấu được tính toán bằng chương trình Kết cấu VXT theo phương pháp phần tử hữu hạn (phương pháp độ cứng trực tiếp) với mô hình khung không gian:");
        d.Bullet("Cột, dầm, kèo được mô hình bằng phần tử thanh Timoshenko 12 bậc tự do (kể biến dạng cắt); giải phóng liên kết đầu thanh bằng khử tĩnh.");
        d.Bullet("Thanh tiết diện thay đổi (cột, kèo khung tiền chế) được chia thành nhiều đoạn lăng trụ.");
        d.Bullet(p.UseRigidDiaphragm
            ? "Sàn được giả thiết tuyệt đối cứng trong mặt phẳng (diaphragm) – ràng buộc nút chủ – nút phụ tại mỗi cao trình sàn."
            : "Không gán sàn tuyệt đối cứng (khung phẳng hoặc mái nhẹ).");
        d.Bullet("Tải trọng sàn được dồn về dầm theo đường phân giác 45° (hình thang trên cạnh dài, tam giác trên cạnh ngắn).");
        d.Bullet("Liên kết chân cột: " + (p.Nodes.Where(x => x.IsSupport).All(x => x.Restraint.All(b => b)) ? "ngàm tại mặt móng." : "theo khai báo (ngàm/khớp) tại mặt móng."));
        d.Bullet("Phân tích tĩnh tuyến tính từng trường hợp tải, sau đó tổ hợp tuyến tính theo các tổ hợp tải trọng.");
        if (r != null) d.Paragraph($"Số phương trình của hệ: {r.Equations}.");
    }

    // ------------------------------------------------------------------
    private static void LoadsSection(Project p, AnalysisResults r, DocxWriter d, int n)
    {
        d.Heading($"{n}. TẢI TRỌNG", 1);
        d.Heading($"{n}.1. Các trường hợp tải", 2);
        d.Table(new[] { "Tên", "Loại", "Hệ số TLBT", "Hệ số độ tin cậy γf" },
            p.Patterns.Select(t => new[] { t.Name, ExcelReport.LoadTypeName(t.Type), N(t.SelfWeight, "0.##"), N(t.Gamma, "0.##") }),
            new[] { 3.0, 5.5, 3.5, 4.5 });

        d.Heading($"{n}.2. Tĩnh tải", 2);
        d.Paragraph("Trọng lượng bản thân kết cấu (cột, dầm, sàn) do chương trình tự tính theo kích thước tiết diện và trọng lượng riêng vật liệu (bê tông cốt thép γ = 25 kN/m³, thép γ = 78.5 kN/m³).");
        var dead = p.Patterns.Where(t => t.Type is LoadType.Dead or LoadType.SuperDead).Select(t => t.Name).ToHashSet();
        var live = p.Patterns.Where(t => t.Type is LoadType.Live or LoadType.RoofLive).Select(t => t.Name).ToHashSet();
        AreaLoadTable(p, d, dead, "Tải phân bố trên sàn (tĩnh tải bổ sung: lớp hoàn thiện, trần, M&E):");
        FrameLoadTable(p, d, dead, "Tải phân bố trên dầm/kèo (tường, mái...):");

        d.Heading($"{n}.3. Hoạt tải", 2);
        d.Paragraph("Hoạt tải sử dụng lấy theo TCVN 2737:2023 tuỳ theo công năng phòng.");
        AreaLoadTable(p, d, live, "Hoạt tải phân bố trên sàn:");
        FrameLoadTable(p, d, live, "Hoạt tải trên thanh:");

        bool lateralOn = LateralOn;   // [AutoReport] chương gió/động đất có được chọn không (CalculationReport.Auto.cs)
        if (lateralOn) {
        d.Heading($"{n}.4. Tải trọng gió", 2);
        var w = p.Wind;
        var windPats = p.Patterns.Where(t => t.Type == LoadType.Wind).Select(t => t.Name).ToHashSet();
        d.Paragraph($"Gió tĩnh theo TCVN 2737:2023: W = W0·k(z)·c·hệ số hiệu chỉnh. Áp lực gió cơ sở W0 = {N(w.W0, "0.00")} kN/m² (theo QCVN 02:2022/BXD cho địa danh xây dựng); dạng địa hình {w.Terrain}; hệ số khí động tổng c = {N(w.Cp, "0.##")}; hệ số hiệu chỉnh {N(w.Factor, "0.##")}; hệ số độ tin cậy γf = {N(w.Gamma, "0.##")}.");
        d.Equation("k(z) = 1.844·(z/zg)^(2α);  A: α = 0.07, zg = 250 m;  B: α = 0.09, zg = 300 m;  C: α = 0.14, zg = 400 m");
        var wl = r?.LateralForces.Where(x => windPats.Contains(x.Pattern)).ToList() ?? new List<StoryLateralForce>();
        if (wl.Count > 0)
        {
            d.Paragraph("Lực gió tiêu chuẩn đặt tại cao trình sàn (bề rộng đón gió B, chiều cao đón gió h = ½ tầng dưới + ½ tầng trên):");
            d.Table(new[] { "Trường hợp", "Tầng", "Cao độ (m)", "Fx (kN)", "Fy (kN)", "Thông số" },
                wl.Select(x => new[] { x.Pattern, x.Story, N(x.Elevation), N(x.Fx), N(x.Fy), x.Note }),
                new[] { 2.2, 1.8, 1.8, 1.8, 1.8, 7.1 }, 10);
        }
        else if (windPats.Count > 0)
        {
            FrameLoadTable(p, d, windPats, "Tải gió gán trực tiếp lên khung (kN/m):");
        }
        else d.Paragraph("Không xét tải trọng gió.");
        }   // [AutoReport] hết if (lateralOn)

        var eqPats = p.Patterns.Where(t => t.Type == LoadType.Seismic).Select(t => t.Name).ToHashSet();
        if (lateralOn && p.Seismic.Enabled && eqPats.Count > 0)   // [AutoReport] lateralOn
        {
            d.Heading($"{n}.5. Tải trọng động đất", 2);
            var sm = p.Seismic;
            d.Paragraph($"Theo TCVN 9386:2012, phương pháp phân tích tĩnh lực ngang tương đương: agR = {N(sm.AgR, "0.0000")}g, nền loại {sm.Soil}, γI = {N(sm.ImportanceFactor, "0.##")}, hệ số ứng xử q = {N(sm.Q, "0.##")}, ψE = {N(sm.PsiLive, "0.##")}.");
            d.Equation("Fb = Sd(T1)·W·λ;   Fi = Fb·zi·Wi / Σ(zj·Wj);   T1 = Ct·H^(3/4)");
            var el = r?.LateralForces.Where(x => eqPats.Contains(x.Pattern)).ToList() ?? new List<StoryLateralForce>();
            if (el.Count > 0)
                d.Table(new[] { "Trường hợp", "Tầng", "Cao độ (m)", "Fx (kN)", "Fy (kN)", "Thông số" },
                    el.Select(x => new[] { x.Pattern, x.Story, N(x.Elevation), N(x.Fx), N(x.Fy), x.Note }),
                    new[] { 2.2, 1.8, 1.8, 1.8, 1.8, 7.1 }, 10);
            var eqw = r?.Warnings.FirstOrDefault(x => x.StartsWith("Động đất"));
            if (eqw != null) d.Paragraph(eqw);
        }

        d.Heading($"{n}.{(!lateralOn ? 4 : p.Seismic.Enabled && eqPats.Count > 0 ? 6 : 5)}. Tổ hợp tải trọng", 2);   // [AutoReport] !lateralOn
        d.Paragraph("TTGH I: tổ hợp cơ bản 1 (tĩnh tải + 1 tải trọng tạm thời) và tổ hợp cơ bản 2 (tĩnh tải + ≥ 2 tải trọng tạm thời nhân hệ số tổ hợp ψ = 0.9); tổ hợp đặc biệt có động đất G + ψE·Q ± E1 ± 0.3·E2. TTGH II: giá trị tiêu chuẩn.");
        d.Table(new[] { "Tên", "Loại", "Thành phần" },
            p.Combinations.Select(c => new[]
            {
                c.Name,
                c.Type switch { ComboType.ULS => "TTGH I", ComboType.SLS => "TTGH II", _ => "Bao" },
                c.Type == ComboType.Envelope ? "Bao các tổ hợp: " + string.Join(", ", c.Items.Select(i => i.Case)) : ComboGenerator.Describe(c)
            }), new[] { 2.0, 2.2, 12.3 }, 11);
    }

    private static void AreaLoadTable(Project p, DocxWriter d, HashSet<string> pats, string title)
    {
        var groups = p.AreaLoads.Where(a => pats.Contains(a.Pattern))
            .GroupBy(a => (a.Pattern, a.Q, a.Note)).ToList();
        if (groups.Count == 0) return;
        d.Paragraph(title);
        d.Table(new[] { "Trường hợp", "q tiêu chuẩn (kN/m²)", "Ghi chú", "Số ô sàn" },
            groups.Select(g => new[] { g.Key.Pattern, N(g.Key.Q, "0.###"), g.Key.Note, g.Count().ToString(Inv) }),
            new[] { 3.0, 4.0, 6.5, 3.0 });
    }

    private static void FrameLoadTable(Project p, DocxWriter d, HashSet<string> pats, string title)
    {
        var loads = p.FrameLoads.Where(l => pats.Contains(l.Pattern) && l.Source != SlabLoadDistributor.Source).ToList();
        if (loads.Count == 0) return;
        var groups = loads.GroupBy(l =>
        {
            double qmax = l.Points.Count > 0 ? l.Points.Max(q => Math.Abs(q[1])) * Math.Sign(l.Points.Sum(q => q[1])) : 0;
            return (l.Pattern, Dir: ExcelReport.DirName(l.Dir), Q: Math.Round(qmax, 3), l.Note);
        }).ToList();
        d.Paragraph(title);
        d.Table(new[] { "Trường hợp", "Phương", "q (kN/m)", "Ghi chú", "Số thanh" },
            groups.Select(g => new[] { g.Key.Pattern, g.Key.Dir, N(g.Key.Q, "0.###"), g.Key.Note, g.Count().ToString(Inv) }),
            new[] { 2.8, 2.8, 2.6, 6.0, 2.3 });
    }

    // ------------------------------------------------------------------
    private static void ForcesSummary(Project p, AnalysisResults r, DocxWriter d, int n)
    {
        d.Heading($"{n}. KẾT QUẢ NỘI LỰC", 1);
        d.Paragraph("Quy ước nội lực theo ETABS/SAP2000: P kéo dương; M3 – mô men uốn quanh trục địa phương 3 (dương khi căng thớ dưới đối với dầm). Bảng nội lực chi tiết tại các mặt cắt được xuất trong file Excel kèm theo (bảng “Element Forces - Frames”).");
        d.Heading($"{n}.1. Nội lực lớn nhất theo nhóm cấu kiện (TTGH I)", 2);
        var combos = r.DesignCombos();
        var results = new List<(string name, CaseResult cr)>();
        foreach (var c in combos) { try { results.Add((c, r.Get(c))); } catch (Exception) { } }
        var rows = new List<string[]>();
        foreach (var grp in p.Frames.GroupBy(fr => fr.Kind).OrderBy(z => z.Key))
        {
            string gname = KindName(grp.Key);
            // [P nén max, P kéo max, V2, M3, M2]
            double[] best = new double[5]; string[] loc = new string[5];
            for (int k = 0; k < 5; k++) loc[k] = "";
            foreach (var f in grp)
                foreach (var (cn, cr) in results)
                {
                    if (!cr.Frames.TryGetValue(f.Id, out var ff)) continue;
                    for (int i = 0; i < ff.Count; i++)
                    {
                        double[] v = { -ff.P(i), ff.P(i), Math.Abs(ff.V2(i)), Math.Abs(ff.M3(i)), Math.Abs(ff.M2(i)) };
                        for (int k = 0; k < 5; k++)
                            if (v[k] > best[k]) { best[k] = v[k]; loc[k] = $"{f.Label} / {cn}"; }
                    }
                }
            string[] nm = { "N nén max (kN)", "N kéo max (kN)", "|V2| max (kN)", "|M3| max (kN.m)", "|M2| max (kN.m)" };
            for (int k = 0; k < 5; k++)
                if (best[k] > 1e-6) rows.Add(new[] { gname, nm[k], N(best[k]), loc[k] });
        }
        d.Table(new[] { "Nhóm cấu kiện", "Đại lượng", "Giá trị", "Cấu kiện / tổ hợp" }, rows, new[] { 3.5, 4.0, 2.5, 6.5 }, 11);

        d.Heading($"{n}.2. Kiểm tra cân bằng tải trọng đứng", 2);
        d.Paragraph("So sánh tổng phản lực đứng ΣFZ với tổng tải trọng đứng tác dụng (trọng lượng bản thân thanh, tải sàn đã dồn về dầm, tải trên thanh và tải nút) cho từng trường hợp tải:");
        var bal = new List<string[]>();
        foreach (var pat in p.Patterns)
        {
            if (!r.Cases.TryGetValue(pat.Name, out var cr)) continue;
            double R = cr.Reactions.Values.Sum(v => v[2]);
            double W = AppliedVertical(p, pat);
            if (Math.Abs(R) < 1e-6 && Math.Abs(W) < 1e-6) continue;
            double diff = Math.Abs(W) > 1e-6 ? (R - W) / W * 100 : 0;
            bal.Add(new[] { pat.Name, N(W, "0.0"), N(R, "0.0"), N(diff, "0.00") });
        }
        if (bal.Count > 0)
            d.Table(new[] { "Trường hợp", "Tổng tải đứng (kN)", "ΣFZ phản lực (kN)", "Sai lệch (%)" }, bal, new[] { 3.5, 4.5, 4.5, 4.0 });
        d.Paragraph("Sai lệch nhỏ chứng tỏ tải trọng được truyền đầy đủ xuống gối tựa (tải không đứng như gió mái theo trục địa phương có thể gây sai khác).", false, true);

        d.Heading($"{n}.3. Phản lực chân cột lớn nhất", 2);
        var reac = new List<(int node, string combo, double fz, double fx, double fy, double mx, double my)>();
        foreach (var (cn, cr) in results)
            foreach (var kv in cr.Reactions)
                reac.Add((kv.Key, cn, kv.Value[2], kv.Value[0], kv.Value[1], kv.Value[3], kv.Value[4]));
        var top = reac.GroupBy(x => x.node).Select(g => g.OrderByDescending(x => x.fz).First()).OrderBy(x => x.node).ToList();
        if (top.Count > 0)
            d.Table(new[] { "Nút", "Tổ hợp", "FZ max (kN)", "FX (kN)", "FY (kN)", "MX (kN.m)", "MY (kN.m)" },
                top.Select(x => new[] { x.node.ToString(Inv), x.combo, N(x.fz), N(x.fx), N(x.fy), N(x.mx), N(x.my) }), null, 11);
    }

    /// <summary>Tổng tải trọng đứng (hướng xuống dương) của một trường hợp tải.</summary>
    private static double AppliedVertical(Project p, LoadPattern pat)
    {
        double W = 0;
        if (pat.SelfWeight != 0)
            foreach (var f in p.Frames)
            {
                try
                {
                    var sec = p.Section(f.Section);
                    double L = p.Length(f);
                    // tiết diện vát: lấy trung bình 3 điểm
                    double A = (sec.Props(0).A + 4 * sec.Props(0.5).A + sec.Props(1).A) / 6;
                    W += pat.SelfWeight * p.Material(sec.Material).UnitWeight * A * L;
                }
                catch (Exception) { }
            }
        foreach (var fl in p.FrameLoads.Where(l => l.Pattern == pat.Name))
        {
            if (fl.Dir != LoadDir.Gravity && fl.Dir != LoadDir.GlobalZ) continue;
            double sign = fl.Dir == LoadDir.Gravity ? 1 : -1;
            FrameElement f = p.Frames.FirstOrDefault(x => x.Id == fl.Element);
            if (f == null) continue;
            double L = p.Length(f), tot = 0;
            for (int i = 0; i + 1 < fl.Points.Count; i++)
                tot += (fl.Points[i][1] + fl.Points[i + 1][1]) / 2 * (fl.Points[i + 1][0] - fl.Points[i][0]) * L;
            tot += fl.PointLoads.Sum(x => x[1]);
            W += sign * tot;
        }
        foreach (var nl in p.NodeLoads.Where(l => l.Pattern == pat.Name)) W += -nl.Fz;
        return W;
    }

    private static string KindName(FrameKind k) => k switch
    {
        FrameKind.Column => "Cột BTCT",
        FrameKind.Beam => "Dầm BTCT",
        FrameKind.SteelColumn => "Cột thép",
        FrameKind.Rafter => "Kèo thép",
        FrameKind.Purlin => "Xà gồ",
        _ => "Giằng"
    };

    // ------------------------------------------------------------------
    private static void BeamsSection(Project p, DesignSummary s, DocxWriter d, int n)
    {
        d.Heading($"{n}. THIẾT KẾ DẦM", 1);
        if (s.Beams.Count == 0) { d.Paragraph("Không có dầm bê tông cốt thép."); return; }
        var cs = p.Concrete;
        Material conc = SafeMat(p, cs.ConcreteBeam), steel = SafeMat(p, cs.RebarMain), stir = SafeMat(p, cs.RebarStirrup);
        d.Paragraph("Dầm được tính toán cốt thép dọc theo TCVN 5574:2018 với tiết diện chữ nhật đặt cốt đơn tại các tiết diện: gối trái, giữa nhịp, gối phải (mô men bao TTGH I). Cốt đai tính theo khả năng chịu cắt trên tiết diện nghiêng. Độ võng kiểm tra theo TTGH II.");

        // ví dụ chi tiết
        var b0 = s.Beams[0];
        var sp0 = b0.Spans.OrderByDescending(x => Math.Abs(x.MSpan)).FirstOrDefault();
        if (sp0 != null && conc != null && steel != null && conc.Rb > 0 && steel.Rs > 0)
        {
            d.Heading($"{n}.1. Ví dụ tính toán: dầm {b0.Name} (tiết diện {Mm(b0.B)}×{Mm(b0.H)} mm)", 2);
            double b = b0.B, h = b0.H;
            double a = cs.CoverBeam + 0.010; // lớp bảo vệ + ½Ø (giả thiết Ø20)
            double h0 = h - a;
            double Rb = conc.Rb, Rs = steel.Rs;
            double xiR = 0.8 / (1 + Rs / 200000.0 / 1000 / 0.0035); // Rs (kN/m²) → MPa/Es; εb2 = 0.0035
            double alphaR = xiR * (1 - 0.5 * xiR);
            d.Paragraph($"Vật liệu: bê tông {conc.Name} – Rb = {MPa(Rb)} MPa; cốt thép {steel.Name} – Rs = {MPa(Rs)} MPa.");
            d.Equation($"a = {Mm(a)} mm;  h0 = h − a = {Mm(h)} − {Mm(a)} = {Mm(h0)} mm");
            d.Equation($"ξR = 0.8/(1 + εs,el/εb2) = 0.8/(1 + {N(Rs / 1000 / 200000, "0.00000")}/0.0035) = {N(xiR, "0.000")};  αR = ξR(1 − 0.5ξR) = {N(alphaR, "0.000")}");
            void Worked(string label, double M, double asReq, double asProv, string text, string combo)
            {
                double Mabs = Math.Abs(M);
                d.Paragraph($"{label}: M = {N(Mabs)} kN.m (tổ hợp {combo}).", true);
                if (Mabs < 1e-6) { d.Equation("M ≈ 0 → đặt thép cấu tạo."); return; }
                double am = Mabs / (Rb * b * h0 * h0);
                d.Equation($"αm = M/(Rb·b·h0²) = {N(Mabs)}/({MPa(Rb)}×10³ × {N(b, "0.000")} × {N(h0, "0.000")}²) = {N(am, "0.0000")}" +
                           (am <= alphaR ? $" ≤ αR = {N(alphaR, "0.000")} → đặt cốt đơn" : $" > αR = {N(alphaR, "0.000")} → cần tăng tiết diện hoặc đặt cốt kép"));
                if (am < 0.5)
                {
                    double xi = 1 - Math.Sqrt(1 - 2 * am);
                    double As = xi * Rb * b * h0 / Rs;
                    d.Equation($"ξ = 1 − √(1 − 2αm) = {N(xi, "0.0000")}");
                    d.Equation($"As = ξ·Rb·b·h0/Rs = {N(xi, "0.0000")} × {MPa(Rb)} × {Mm(b)} × {Mm(h0)} / {MPa(Rs)} = {N(As * 1e6, "0")} mm² = {Cm2(As)} cm²");
                }
                double mu = asProv / (b * h0) * 100;
                d.Equation($"As yêu cầu (chương trình) = {Cm2(asReq)} cm²;  bố trí {(string.IsNullOrEmpty(text) ? "-" : text)} có As = {Cm2(asProv)} cm²;  μ = {N(mu, "0.00")}% ≥ μmin = {N(cs.MinRatioBeam, "0.00")}%");
            }
            d.Paragraph($"Xét nhịp {sp0.LeftSupport} – {sp0.RightSupport}, L = {N(sp0.L)} m:");
            Worked("Tiết diện gối trái (căng thớ trên)", sp0.MLeft, sp0.AsTopLeftReq, sp0.AsTopLeftProv, sp0.TopLeftText, sp0.ComboMLeft);
            Worked("Tiết diện giữa nhịp (căng thớ dưới)", sp0.MSpan, sp0.AsBotReq, sp0.AsBotProv, sp0.BotText, sp0.ComboMSpan);
            Worked("Tiết diện gối phải (căng thớ trên)", sp0.MRight, sp0.AsTopRightReq, sp0.AsTopRightProv, sp0.TopRightText, sp0.ComboMRight);
            // cắt
            d.Paragraph($"Tính cốt đai: Qmax = {N(sp0.QMax)} kN (tổ hợp {sp0.ComboQ}).", true);
            double Rbt = conc.Rbt;
            double Qb1 = 0.5 * Rbt * b * h0;
            double Qbmax = 2.5 * Rbt * b * h0;
            double Qult = 0.3 * Rb * b * h0;
            d.Equation($"Kiểm tra dải nén giữa các vết nứt: Q ≤ 0.3·Rb·b·h0 = 0.3 × {MPa(Rb)}×10³ × {N(b, "0.000")} × {N(h0, "0.000")} = {N(Qult)} kN → {(sp0.QMax <= Qult ? "thoả mãn" : "KHÔNG thoả mãn")}");
            d.Equation($"Khả năng chịu cắt bê tông tối thiểu: Qb,min = 0.5·Rbt·b·h0 = {N(Qb1)} kN;  Qb,max = 2.5·Rbt·b·h0 = {N(Qbmax)} kN");
            if (sp0.StirrupDia > 0 && sp0.StirrupSupport > 0 && stir != null)
            {
                double Asw = 2 * Math.PI * Math.Pow(sp0.StirrupDia / 1000.0, 2) / 4;
                double qsw = stir.Rsw * Asw / sp0.StirrupSupport;
                double Mb = 1.5 * Rbt * b * h0 * h0;
                double c0 = Math.Min(Math.Max(Math.Sqrt(Mb / Math.Max(qsw * 0.75, 1e-9)), h0), 2 * h0);
                double Qb = Math.Clamp(Mb / c0, Qb1, Qbmax);
                double Qsw = 0.75 * qsw * c0;
                d.Equation($"Đai Ø{sp0.StirrupDia} hai nhánh, bước s = {Mm(sp0.StirrupSupport)} mm: qsw = Rsw·Asw/s = {MPa(stir.Rsw)} × {N(Asw * 1e6, "0.0")} / {Mm(sp0.StirrupSupport)} = {N(qsw)} kN/m");
                d.Equation($"Mb = 1.5·Rbt·b·h0² = {N(Mb)} kN.m;  c = {N(c0, "0.000")} m;  Qb = Mb/c = {N(Qb)} kN;  Qsw = 0.75·qsw·c = {N(Qsw)} kN");
                d.Equation($"Qb + Qsw = {N(Qb + Qsw)} kN {(Qb + Qsw >= sp0.QMax ? "≥" : "<")} Q = {N(sp0.QMax)} kN → {(Qb + Qsw >= sp0.QMax ? "đạt" : "kiểm tra lại")}");
                d.Equation($"Bố trí: vùng gối Ø{sp0.StirrupDia}a{Mm(sp0.StirrupSupport)} trên đoạn {Mm(sp0.SupportZone)} mm; giữa nhịp Ø{sp0.StirrupDia}a{Mm(sp0.StirrupMid)}.");
            }
            if (sp0.DeflectionLimit > 0)
                d.Equation($"Độ võng: f = {N(sp0.Deflection * 1000, "0.0")} mm {(sp0.Deflection <= sp0.DeflectionLimit ? "≤" : ">")} [f] = L/{N(cs.BeamDeflectionLimit, "0")} = {N(sp0.DeflectionLimit * 1000, "0.0")} mm");
            d.Paragraph("Ghi chú: phép tính minh hoạ dùng a = lớp bảo vệ + 10 mm; kết quả bố trí cuối cùng lấy theo chương trình (có xét đường kính thực tế, số lớp thép).", false, true);
        }

        ExtraBeamExamples(p, s, d, n);   // [AutoReport] ví dụ bổ sung (CalculationReport.Auto.cs)
        d.Heading($"{n}.2. Bảng tổng hợp thiết kế dầm", 2);
        var rows = new List<string[]>();
        foreach (var b in s.Beams)
        {
            int k = 1;
            foreach (var sp in b.Spans)
            {
                rows.Add(new[]
                {
                    k == 1 ? b.Name : "", k == 1 ? $"{Mm(b.B)}×{Mm(b.H)}" : "", (k++).ToString(Inv), N(sp.L),
                    N(sp.MLeft, "0.0"), N(sp.MSpan, "0.0"), N(sp.MRight, "0.0"), N(sp.QMax, "0.0"),
                    sp.TopLeftText, sp.BotText, sp.TopRightText,
                    sp.StirrupDia > 0 ? $"Ø{sp.StirrupDia}a{Mm(sp.StirrupSupport)}/{Mm(sp.StirrupMid)}" : "",
                    OkText(b.Ok)
                });
            }
        }
        d.Table(new[] { "Dầm", "b×h", "Nhịp", "L (m)", "M trái", "M nhịp", "M phải", "Q (kN)", "Thép trên trái", "Thép dưới", "Thép trên phải", "Đai", "KL" },
            rows, new[] { 1.4, 1.4, 0.8, 1.0, 1.1, 1.1, 1.1, 1.0, 1.8, 1.8, 1.8, 1.6, 1.1 }, 9);
        d.Paragraph("Mô men đơn vị kN.m; mô men âm tại gối là căng thớ trên.", false, true);

        // dầm cong: bán kính, chiều dài cung, mô men xoắn
        var curved = s.Beams.Where(b => b.IsCurved).ToList();
        if (curved.Count > 0)
        {
            d.Heading($"{n}.3. Dầm cong trên mặt bằng", 2);
            d.Paragraph("Dầm cong được mô hình bằng các đoạn thẳng nối các điểm trên cung (mỗi đoạn ≤ 10°), tải phân bố tính theo chiều dài cung; "
                        + "nội lực quy về hệ trục tiếp tuyến của cung. Dầm cong chịu uốn + cắt + XOẮN: kiểm tra dải bê tông chịu nén T ≤ 0,1·Rb·b²·h "
                        + "(tương tác tuyến tính với lực cắt), cốt đai kín theo mô hình giàn không gian θ = 45° (Asw1/s = T/(2·Ak·Rsw) + qsw/(2·Rsw)), "
                        + "cốt dọc chịu xoắn Asl = T·uk/(2·Ak·Rs) cộng ½ vào thép trên và ½ vào thép dưới (giản lược theo TCVN 5574:2018 mục 8.1.4). "
                        + "Mặt đứng dầm thể hiện trải theo cung; chiều dài thép tính theo chiều dài cung. Bắt buộc dùng đai kín móc 135°.");
            var crow = new List<string[]>();
            foreach (var b in curved)
            {
                var tc = b.Checks.Where(c => c.Name.StartsWith("Xoắn")).ToList();
                double ratio = tc.Count > 0 ? tc.Max(c => c.Ratio) : 0;
                var sp = b.Spans.FirstOrDefault();
                crow.Add(new[]
                {
                    b.Name, $"{Mm(b.B)}×{Mm(b.H)}", N(b.CurveRadius), N(b.ArcLength), N(b.Chord), N(b.Sagitta),
                    N(b.TorsionMax, "0.0"), sp != null ? Cm2(sp.AsTorsion) : "", sp != null && sp.StirrupDia > 0 ? $"Ø{sp.StirrupDia}a{Mm(sp.StirrupSupport)}" : "",
                    N(ratio, "0.00"), OkText(b.Ok)
                });
            }
            d.Table(new[] { "Dầm", "b×h", "R (m)", "L cung (m)", "Dây cung (m)", "Độ vồng (m)", "T max (kN.m)", "Asl (cm²)", "Đai kín", "Hệ số xoắn", "KL" },
                crow, new[] { 1.4, 1.4, 1.1, 1.2, 1.2, 1.2, 1.3, 1.1, 1.4, 1.2, 1.0 }, 9);
        }
    }

    private static Material SafeMat(Project p, string name)
    {
        try { return p.Material(name); } catch (Exception) { return null; }
    }

    // ------------------------------------------------------------------
    private static void ColumnsSection(Project p, DesignSummary s, DocxWriter d, int n)
    {
        d.Heading($"{n}. THIẾT KẾ CỘT", 1);
        if (s.Columns.Count == 0) { d.Paragraph("Không có cột bê tông cốt thép."); return; }
        var cs = p.Concrete;
        Material conc = SafeMat(p, cs.ConcreteColumn), steel = SafeMat(p, cs.RebarMain);
        d.Paragraph($"Cột được tính toán chịu nén lệch tâm xiên theo TCVN 5574:2018, có kể ảnh hưởng uốn dọc qua hệ số η. Chiều dài tính toán l0 = ψ·H với ψ = {N(cs.ColumnLengthFactor, "0.##")}. Cốt thép bố trí đối xứng; hàm lượng {N(cs.MinRatioColumn, "0.##")}% ≤ μ ≤ {N(cs.MaxRatioColumn, "0.##")}%.");

        var c0 = s.Columns[0];
        var st = c0.Stories.OrderByDescending(x => x.N).FirstOrDefault();
        if (st != null && conc != null && steel != null && conc.Rb > 0 && st.B > 0 && st.H > 0)
        {
            d.Heading($"{n}.1. Ví dụ tính toán: cột {c0.Name}, tầng {st.Story} (tiết diện {Mm(st.B)}×{Mm(st.H)} mm)", 2);
            double Rb = conc.Rb, Rs = steel.Rs, Rsc = steel.Rsc > 0 ? steel.Rsc : steel.Rs;
            double b = st.B, h = st.H, L = st.Length;
            double a = cs.CoverColumn + 0.009;
            double h0 = h - a;
            double l0 = cs.ColumnLengthFactor * L;
            d.Paragraph($"Nội lực tính toán (tổ hợp {st.Combo}): N = {N(st.N)} kN; M2 = {N(st.M2)} kN.m; M3 = {N(st.M3)} kN.m (đã kể uốn dọc: η2 = {N(st.Eta2, "0.000")}, η3 = {N(st.Eta3, "0.000")}).");
            d.Equation($"l0 = ψ·H = {N(cs.ColumnLengthFactor, "0.##")} × {N(L)} = {N(l0)} m;  λh = l0/h = {N(l0 / h, "0.0")};  λb = l0/b = {N(l0 / b, "0.0")}");
            // quy đổi lệch tâm xiên về lệch tâm phẳng (phương pháp gần đúng)
            double Mx = Math.Abs(st.M3), My = Math.Abs(st.M2);
            double hh = h, bb = b, M1 = Mx, M2 = My;
            if (My / Math.Max(b, 1e-6) > Mx / Math.Max(h, 1e-6)) { hh = b; bb = h; M1 = My; M2 = Mx; }
            double h0e = hh - a;
            double x1 = st.N / (Rb * bb);
            double m0 = x1 <= h0e ? 1 - 0.6 * x1 / h0e : 0.4;
            double Meq = M1 + m0 * M2 * hh / bb;
            double e0 = st.N > 1e-6 ? Meq / st.N : 0;
            double ea = Math.Max(Math.Max(L / 600, hh / 30), 0.01);
            e0 = Math.Max(e0, ea);
            double e = e0 + hh / 2 - a;
            d.Equation($"Quy đổi lệch tâm xiên → lệch tâm phẳng theo cạnh h = {Mm(hh)} mm: x1 = N/(Rb·b) = {Mm(x1)} mm;  m0 = {N(m0, "0.000")}");
            d.Equation($"M = M1 + m0·M2·h/b = {N(M1)} + {N(m0, "0.000")} × {N(M2)} × {Mm(hh)}/{Mm(bb)} = {N(Meq)} kN.m");
            d.Equation($"e0 = max(M/N; ea) = {Mm(e0)} mm (ea = {Mm(ea)} mm);  e = e0 + h/2 − a = {Mm(e)} mm");
            double xiR = 0.8 / (1 + Rs / 1000 / 200000 / 0.0035);
            double x = x1;
            double As;
            if (x <= xiR * h0e)
            {
                d.Equation($"x = {Mm(x)} mm ≤ ξR·h0 = {N(xiR, "0.000")} × {Mm(h0e)} = {Mm(xiR * h0e)} mm → lệch tâm lớn");
                As = (st.N * e - Rb * bb * x * (h0e - x / 2)) / (Rsc * (h0e - a));
                d.Equation($"As = As' = [N·e − Rb·b·x·(h0 − x/2)] / [Rsc·(h0 − a')] = {N(As * 1e4, "0.00")} cm² (mỗi phía)");
            }
            else
            {
                double xx = xiR * h0e + (1 - xiR) / (1 + 50 * Math.Pow(e0 / h0e, 2)) * h0e; // gần đúng
                xx = Math.Min(xx, h0e);
                d.Equation($"x = {Mm(x)} mm > ξR·h0 = {Mm(xiR * h0e)} mm → lệch tâm bé, lấy gần đúng x = {Mm(xx)} mm");
                As = (st.N * e - Rb * bb * xx * (h0e - xx / 2)) / (Rsc * (h0e - a));
                d.Equation($"As = As' = [N·e − Rb·b·x·(h0 − x/2)] / [Rsc·(h0 − a')] = {N(As * 1e4, "0.00")} cm² (mỗi phía)");
            }
            if (As < 0) d.Equation("As < 0 → bê tông đủ chịu lực, đặt thép theo hàm lượng tối thiểu.");
            d.Equation($"Kết quả chương trình: As,tổng yêu cầu = {Cm2(st.AsReq)} cm²; bố trí {st.TotalBars}Ø{st.Dia} (As = {Cm2(st.AsProv)} cm², μ = {N(st.Ratio, "0.00")}%); hệ số sử dụng = {N(st.Utilization, "0.000")}");
            if (st.StirrupDia > 0)
                d.Equation($"Cốt đai Ø{st.StirrupDia}a{Mm(st.StirrupSpacing)} (vùng nút/đầu cột a{Mm(st.StirrupSpacingEnd)}).");
            d.Paragraph("Ghi chú: phép tính minh hoạ theo phương pháp quy đổi gần đúng; kết quả bố trí lấy theo chương trình (kiểm tra bằng biểu đồ tương tác).", false, true);
        }

        ExtraColumnExamples(p, s, d, n);   // [AutoReport]
        d.Heading($"{n}.2. Bảng tổng hợp thiết kế cột", 2);
        var rows = new List<string[]>();
        foreach (var c in s.Columns)
            foreach (var x in c.Stories)
                rows.Add(new[]
                {
                    c.Name, x.Story, $"{Mm(x.B)}×{Mm(x.H)}", N(x.N, "0.0"), N(x.M2, "0.0"), N(x.M3, "0.0"), x.Combo,
                    Cm2(x.AsReq), x.Dia > 0 ? $"{x.TotalBars}Ø{x.Dia}" : "", N(x.Ratio, "0.00"),
                    x.StirrupDia > 0 ? $"Ø{x.StirrupDia}a{Mm(x.StirrupSpacing)}" : "", N(x.Utilization, "0.00"), OkText(c.Ok)
                });
        d.Table(new[] { "Cột", "Tầng", "b×h", "N (kN)", "M2", "M3", "TH", "As yc (cm²)", "Bố trí", "μ (%)", "Đai", "Hệ số SD", "KL" },
            rows, new[] { 1.2, 1.1, 1.4, 1.3, 1.1, 1.1, 1.0, 1.3, 1.5, 1.0, 1.5, 1.2, 1.3 }, 9);
    }

    // ------------------------------------------------------------------
    private static void SlabsSection(Project p, DesignSummary s, DocxWriter d, int n)
    {
        d.Heading($"{n}. THIẾT KẾ SÀN", 1);
        if (s.Slabs.Count == 0) { d.Paragraph("Không có ô sàn."); return; }
        d.Paragraph("Ô sàn có Ly/Lx ≤ 2 tính như bản kê bốn cạnh (tra hệ số mô men theo sơ đồ liên kết biên); Ly/Lx > 2 tính như bản dầm. Cắt dải bản rộng 1 m để tính cốt thép như cấu kiện chịu uốn tiết diện chữ nhật b = 1000 mm.");
        var s0 = s.Slabs.OrderByDescending(x => Math.Abs(x.M1)).First();
        Material conc = SafeMat(p, p.Concrete.ConcreteSlab), st = SafeMat(p, p.Concrete.RebarStirrup);
        if (conc != null && st != null && s0.Thickness > 0 && conc.Rb > 0 && st.Rs > 0)
        {
            d.Heading($"{n}.1. Ví dụ tính toán: ô sàn {s0.Name}", 2);
            double hb = s0.Thickness, a = p.Concrete.CoverSlab + 0.004, h0 = hb - a, b = 1.0;
            d.Paragraph($"Kích thước Lx × Ly = {N(s0.Lx)} × {N(s0.Ly)} m (Ly/Lx = {N(s0.Ly / Math.Max(s0.Lx, 1e-6), "0.00")}), chiều dày hb = {Mm(hb)} mm; {(s0.TwoWay ? "bản kê bốn cạnh" : "bản dầm")}; liên kết: {s0.EdgeCondition}. Tải tính toán q = {N(s0.Q)} kN/m².");
            d.Equation($"Mô men: M1 = {N(s0.M1)}; M2 = {N(s0.M2)}; MI = {N(s0.MI)}; MII = {N(s0.MII)} (kN.m/m)");
            double M = Math.Abs(s0.M1);
            double am = M / (conc.Rb * b * h0 * h0);
            d.Equation($"h0 = {Mm(hb)} − {Mm(a)} = {Mm(h0)} mm;  αm = M1/(Rb·b·h0²) = {N(M)}/({MPa(conc.Rb)}×10³ × 1 × {N(h0, "0.000")}²) = {N(am, "0.0000")}");
            if (am < 0.5)
            {
                double xi = 1 - Math.Sqrt(1 - 2 * am);
                double As = xi * conc.Rb * b * h0 / st.Rs;
                d.Equation($"ξ = {N(xi, "0.0000")};  As = ξ·Rb·b·h0/Rs = {N(As * 1e6, "0")} mm²/m = {Cm2(As)} cm²/m");
            }
            d.Equation($"Bố trí: lớp dưới phương ngắn {s0.BotX}, phương dài {s0.BotY}; mũ gối {s0.TopX} / {s0.TopY}.");
        }
        ExtraSlabExamples(p, s, d, n);   // [AutoReport]
        d.Heading($"{n}.2. Bảng tổng hợp thiết kế sàn", 2);
        d.Table(new[] { "Ô sàn", "Lx×Ly (m)", "hb (mm)", "q (kN/m²)", "M1", "M2", "MI", "MII", "Dưới X", "Dưới Y", "Mũ X", "Mũ Y", "KL" },
            s.Slabs.Select(x => new[]
            {
                x.Name, $"{N(x.Lx)}×{N(x.Ly)}", Mm(x.Thickness), N(x.Q), N(x.M1), N(x.M2), N(x.MI), N(x.MII),
                x.BotX, x.BotY, x.TopX, x.TopY, OkText(x.Ok)
            }), new[] { 1.4, 1.8, 1.0, 1.1, 1.0, 1.0, 1.0, 1.0, 1.4, 1.4, 1.4, 1.4, 1.6 }, 9);
        d.Paragraph("Mô men đơn vị kN.m/m.", false, true);
    }

    // ------------------------------------------------------------------
    private static void FootingsSection(Project p, DesignSummary s, DocxWriter d, int n)
    {
        d.Heading($"{n}. THIẾT KẾ MÓNG", 1);
        if (s.Footings.Count == 0) { d.Paragraph("Không có móng được thiết kế."); return; }
        var so = p.Soil;
        d.Paragraph($"Móng đơn trên nền thiên nhiên, tính theo TCVN 9362:2012. Chỉ tiêu nền: φ = {N(so.Phi, "0.#")}°, c = {N(so.C, "0.##")} kN/m², γ = {N(so.Gamma, "0.##")} kN/m³, γ' = {N(so.GammaAbove, "0.##")} kN/m³; độ sâu chôn móng Df = {N(so.Depth)} m; m1 = {N(so.M1, "0.##")}, m2 = {N(so.M2, "0.##")}, ktc = {N(so.Ktc, "0.##")}.");
        d.Equation("R = (m1·m2/ktc)·(A·b·γ + B·Df·γ' + D·c)");
        d.Equation("Ptb = Ntc/(L·B) + γtb·Df ≤ R;   Pmax = Ptb·(1 + 6eL/L + 6eB/B) ≤ 1.2R;   Pmin ≥ 0");
        var f0 = s.Footings.OrderByDescending(x => x.N).First();
        d.Heading($"{n}.1. Ví dụ tính toán: móng {f0.Name} (nút {f0.NodeId})", 2);
        d.Paragraph($"Nội lực tính toán tại chân cột (tổ hợp {f0.Combo}): N = {N(f0.N)} kN; Mx = {N(f0.Mx)} kN.m; My = {N(f0.My)} kN.m; Qx = {N(f0.Qx)} kN; Qy = {N(f0.Qy)} kN. Nội lực tiêu chuẩn Ntc = N/{N(so.LoadFactorAvg, "0.##")} = {N(f0.Ntc)} kN.");
        d.Equation($"Kích thước đáy móng L × B × H = {N(f0.L)} × {N(f0.B)} × {N(f0.H)} m;  Df = {N(f0.Depth)} m;  R = {N(f0.R, "0.0")} kN/m²");
        d.Equation($"Ptb = {N(f0.Ptb, "0.0")} kN/m² {(f0.Ptb <= f0.R ? "≤" : ">")} R = {N(f0.R, "0.0")};  Pmax = {N(f0.Pmax, "0.0")} {(f0.Pmax <= 1.2 * f0.R ? "≤" : ">")} 1.2R = {N(1.2 * f0.R, "0.0")};  Pmin = {N(f0.Pmin, "0.0")} kN/m²");
        d.Equation($"Cốt thép đáy: phương L As = {Cm2(f0.AsL)} cm² → {f0.BarsL};  phương B As = {Cm2(f0.AsB)} cm² → {f0.BarsB}");
        foreach (var c in f0.Checks)
            d.Equation($"{c.Name}: {N(c.Demand)} / {N(c.Capacity)} {c.Unit} → tỷ số {N(c.Ratio, "0.000")} – {OkText(c.Ok)}");

        ExtraFootingExamples(p, s, d, n);   // [AutoReport]
        d.Heading($"{n}.2. Bảng tổng hợp thiết kế móng", 2);
        d.Table(new[] { "Móng", "SL", "N (kN)", "Mx", "My", "L×B×H (m)", "R", "Ptb", "Pmax", "Pmin", "Thép L", "Thép B", "KL" },
            s.Footings.Select(x => new[]
            {
                x.Name, x.Quantity.ToString(Inv), N(x.N, "0.0"), N(x.Mx, "0.0"), N(x.My, "0.0"), $"{N(x.L)}×{N(x.B)}×{N(x.H)}",
                N(x.R, "0"), N(x.Ptb, "0"), N(x.Pmax, "0"), N(x.Pmin, "0"), x.BarsL, x.BarsB, OkText(x.Ok)
            }), new[] { 1.2, 0.8, 1.2, 1.0, 1.0, 2.4, 1.0, 1.0, 1.0, 1.0, 1.6, 1.6, 1.7 }, 9);
        d.Paragraph("Áp lực đáy móng đơn vị kN/m²; mô men kN.m.", false, true);
    }

    // ------------------------------------------------------------------
    private static void PileCapsSection(Project p, DesignSummary s, DocxWriter d, int n)
    {
        d.Heading($"{n}. THIẾT KẾ MÓNG CỌC – ĐÀI CỌC", 1);
        var fs = p.Foundation ?? new FoundationSettings();
        string pile = (fs.PileRound ? "cọc tròn D" : "cọc vuông ") + Mm(fs.PileSize) + (fs.PileRound ? "" : "×" + Mm(fs.PileSize)) + " mm";
        d.Paragraph($"Móng cọc BTCT: {pile}, chiều dài cọc {N(fs.PileLength, "0.0")} m, sức chịu tải cho phép của cọc [P] = {N(fs.PileCapacity, "0")} kN (theo TCVN 10304:2014 / thí nghiệm nén tĩnh – do người thiết kế cung cấp), sức chịu nhổ [Pk] = {N(fs.PileTension, "0")} kN. Khoảng cách tim cọc ≥ {N(fs.PileSpacingFactor, "0.#")}d, mép đài cách mép cọc {Mm(fs.CapEdge)} mm, cọc ngàm vào đài {Mm(fs.PileEmbed)} mm, chiều cao đài tối thiểu {Mm(fs.CapMinHeight)} mm.");
        d.Paragraph("Đài được coi là tuyệt đối cứng; phản lực cọc tính cho từng tổ hợp TTGH I, kể cả trọng lượng đài và đất trên đài (hệ số 1,1). Chiều cao đài chọn theo chọc thủng của cột (tháp 45°) và của cọc góc, cắt trên tiết diện nghiêng tại mép cột (TCVN 5574:2018 mục 8.1.3, 8.1.6 – dạng đơn giản hoá); cốt thép đáy theo mô men tại mép cột.");
        d.Equation("n ≥ 1,2·Nmax/[P];   Pi = (N + G)/n ± My'·xi/Σxi² ± Mx'·yi/Σyi² ≤ [P],  Pmin ≥ −[Pk];   My' = My + Qx·H, Mx' = Mx − Qy·H");
        d.Equation("F = Σ Pi (cọc ngoài tháp chọc thủng) ≤ Rbt·u·h0,  u = 2(hc + bc + 2h0);   M = Σ Pi·(xi − hc/2);   h0 = H − lngàm − 50 mm");
        var c0 = s.PileCaps.OrderByDescending(x => x.PMax).First();
        d.Heading($"{n}.1. Ví dụ tính toán: đài {c0.Name} ({c0.Piles.Count} cọc, nút {string.Join(", ", c0.NodeIds.Take(6))})", 2);
        d.Paragraph($"Nội lực tính toán tại chân cột (tổ hợp {c0.Combo}): N = {N(c0.N)} kN; Mx = {N(c0.Mx)} kN.m; My = {N(c0.My)} kN.m; Qx = {N(c0.Qx)} kN; Qy = {N(c0.Qy)} kN; trọng lượng đài + đất G = {N(c0.Weight)} kN.");
        d.Equation($"Đài L × B × H = {N(c0.L)} × {N(c0.B)} × {N(c0.H)} m;  h0 = {N(c0.H0, "0.000")} m;  khoảng cách cọc s = {Mm(c0.Spacing)} mm");
        if (c0.Reactions.Count > 0)
            d.Equation("Phản lực cọc (kN): " + string.Join("; ", c0.Reactions.Select((v, i) => $"P{i + 1} = {N(v, "0.0")}")) + $";  ΣP = {N(c0.Reactions.Sum(), "0.0")} = N + G");
        d.Equation($"Mô men mép cột: ML = {N(c0.MomentL)} kN.m → As = {Cm2(c0.AsL)} cm² → {c0.BarsL};  MB = {N(c0.MomentB)} kN.m → As = {Cm2(c0.AsB)} cm² → {c0.BarsB};  lớp trên {c0.BarsTop}");
        d.Table(new[] { "Hạng mục", "Giá trị", "Giới hạn", "Đơn vị", "Tỷ số", "Kết luận" },
            c0.Checks.Select(c => new[] { c.Name, N(c.Demand, "0.0"), N(c.Capacity, "0.0"), c.Unit, N(c.Ratio, "0.000"), OkText(c.Ok) }),
            new[] { 6.0, 1.8, 1.8, 1.3, 1.4, 1.8 }, 9);
        foreach (var msg in c0.Messages.Where(m => !m.StartsWith("Gộp"))) d.Paragraph(msg, false, true);

        d.Heading($"{n}.2. Bảng tổng hợp thiết kế đài cọc", 2);
        d.Table(new[] { "Đài", "SL", "Số cọc", "N (kN)", "L×B×H (m)", "Pmax", "Pmin", "[P]", "Thép L", "Thép B", "Lớp trên", "KL" },
            s.PileCaps.Select(x => new[]
            {
                x.Name, x.Quantity.ToString(Inv), x.Piles.Count.ToString(Inv), N(x.N, "0.0"), $"{N(x.L)}×{N(x.B)}×{N(x.H)}",
                N(x.PMax, "0"), N(x.PMin, "0"), N(x.PileCapacity, "0"), x.BarsL, x.BarsB, x.BarsTop, OkText(x.Ok)
            }), new[] { 1.1, 0.8, 1.0, 1.3, 2.6, 1.1, 1.1, 1.0, 1.6, 1.6, 1.8, 1.7 }, 9);
        d.Paragraph("Phản lực cọc đơn vị kN (nén dương).", false, true);
    }

    // ------------------------------------------------------------------
    private static void StripFootingsSection(Project p, DesignSummary s, DocxWriter d, int n)
    {
        d.Heading($"{n}. THIẾT KẾ MÓNG BĂNG", 1);
        var so = p.Soil; var fs = p.Foundation ?? new FoundationSettings();
        string dir = fs.StripDirection switch { 1 => "dọc trục X", 2 => "dọc trục Y", 3 => "giao thoa 2 phương", _ => "tự chọn theo phương có nhiều cột thẳng hàng" };
        d.Paragraph($"Móng băng BTCT dưới dãy cột, tiết diện chữ T ngược (bản đáy + sườn), phương dải: {dir}. Nền: φ = {N(so.Phi, "0.#")}°, c = {N(so.C, "0.##")} kN/m², Df = {N(so.Depth)} m{(so.ROverride > 0 ? ", R = " + N(so.ROverride, "0") + " kN/m² (nhập trực tiếp)" : "")}. Đầu dải vươn {Mm(fs.StripOverhang)} mm ra ngoài tim cột biên.");
        d.Paragraph("Nội lực dải tính theo mô hình dầm trên nền đàn hồi Winkler (phần tử dầm ~0,25 m, lò xo k·B·a tại nút), giải cho từng tổ hợp; áp lực nền kiểm tra với tổ hợp tiêu chuẩn, nội lực thiết kế lấy theo bao các tổ hợp TTGH I. Mô men dương làm căng thớ dưới.");
        d.Equation("R = (m1·m2/ktc)·(A·b·γ + B·Df·γ' + D·c);   ptb = (ΣNtc + γtb·Df·B·L)/(B·L) ≤ R;   pmax = k·wmax ≤ 1,2R");
        d.Equation("EI·d⁴w/dx⁴ + k·B·w = q(x);   k = hệ số nền (≈ 120·R nếu không khai báo);   bản cánh: M = pnet·c²/2, Q = pnet·(c − h0), c = (B − b)/2");
        var s0 = s.StripFootings.OrderByDescending(x => x.PMax / Math.Max(x.R, 1)).First();
        d.Heading($"{n}.1. Ví dụ tính toán: dải {s0.Name} ({s0.Grid}, dài {N(s0.Length)} m, {s0.NodeIds.Count} cột)", 2);
        d.Paragraph($"Tổng lực nén chân cột (tổ hợp tiêu chuẩn {s0.ComboService}): ΣN = {N(s0.SumN, "0.0")} kN; trọng lượng móng + đất G = {N(s0.SelfWeight, "0.0")} kN; hệ số nền k = {N(s0.K, "0")} kN/m³.");
        d.Equation($"B = {N(s0.Width)} m;  sườn b × h = {Mm(s0.RibB)} × {Mm(s0.RibH)} mm;  bản cánh {Mm(s0.FlangeEdge)}…{Mm(s0.FlangeRoot)} mm;  R = {N(s0.R, "0.0")} kN/m²");
        d.Equation($"ptb = {N(s0.PAvg, "0.0")} kN/m² {(s0.PAvg <= s0.R ? "≤" : ">")} R = {N(s0.R, "0.0")};  pmax = {N(s0.PMax, "0.0")} {(s0.PMax <= 1.2 * s0.R ? "≤" : ">")} 1,2R = {N(1.2 * s0.R, "0.0")} kN/m²");
        d.Equation($"Sườn (tổ hợp {s0.Combo}): M+ = {N(s0.MPos)} kN.m → As = {Cm2(s0.AsBottom)} cm² → {s0.BottomBars};  M− = {N(s0.MNeg)} kN.m → As = {Cm2(s0.AsTop)} cm² → {s0.TopBars};  Q = {N(s0.VDesign)} kN → đai {s0.Stirrups}");
        if (s0.Width > s0.RibB + 1e-6)
            d.Equation($"Bản cánh: M = {N(s0.FlangeM)} kN.m/m, Q = {N(s0.FlangeV)} kN/m → As = {Cm2(s0.AsFlange)} cm²/m → {s0.FlangeBars}");
        d.Table(new[] { "Hạng mục", "Giá trị", "Giới hạn", "Đơn vị", "Tỷ số", "Kết luận" },
            s0.Checks.Select(c => new[] { c.Name, N(c.Demand, "0.0"), N(c.Capacity, "0.0"), c.Unit, N(c.Ratio, "0.000"), OkText(c.Ok) }),
            new[] { 6.0, 1.8, 1.8, 1.3, 1.4, 1.8 }, 9);
        foreach (var msg in s0.Messages) d.Paragraph(msg, false, true);
        if (s0.Stations.Count > 1)
        {
            int step = Math.Max(1, s0.Stations.Count / 16);
            var idx = Enumerable.Range(0, s0.Stations.Count).Where(i => i % step == 0 || i == s0.Stations.Count - 1).ToList();
            d.Paragraph("Biểu đồ nội lực và áp lực nền dọc dải (trích):", false, true);
            d.Table(new[] { "x (m)", "M (kN.m)", "Mmax bao", "Mmin bao", "|Q|max bao (kN)", "p tiêu chuẩn (kN/m²)" },
                idx.Select(i => new[]
                {
                    N(s0.Stations[i] - s0.Start), N(At(s0.M, i), "0.0"), N(At(s0.MMax, i), "0.0"), N(At(s0.MMin, i), "0.0"),
                    N(At(s0.VMax, i), "0.0"), N(At(s0.Pressure, i), "0.0")
                }), new[] { 1.6, 2.2, 2.2, 2.2, 2.6, 3.0 }, 9);
        }

        d.Heading($"{n}.2. Bảng tổng hợp thiết kế móng băng", 2);
        d.Table(new[] { "Dải", "Trục", "Dài (m)", "B (m)", "b×h (mm)", "R", "ptb", "pmax", "Thép dưới", "Thép trên", "Đai", "Bản cánh", "KL" },
            s.StripFootings.Select(x => new[]
            {
                x.Name, x.Grid, N(x.Length), N(x.Width), $"{Mm(x.RibB)}×{Mm(x.RibH)}", N(x.R, "0"), N(x.PAvg, "0"), N(x.PMax, "0"),
                x.BottomBars, x.TopBars, x.Stirrups, x.FlangeBars, OkText(x.Ok)
            }), new[] { 1.0, 1.4, 1.1, 1.0, 1.8, 0.9, 0.9, 0.9, 1.6, 1.6, 1.4, 1.5, 1.6 }, 9);
        d.Paragraph("Áp lực nền đơn vị kN/m².", false, true);
    }

    private static double At(List<double> l, int i) => l != null && i >= 0 && i < l.Count ? l[i] : double.NaN;

    // ------------------------------------------------------------------
    private static void SteelSection(Project p, DesignSummary s, DocxWriter d, int n)
    {
        d.Heading($"{n}. KẾT CẤU THÉP", 1);
        d.Paragraph("Cấu kiện thép được kiểm tra theo TCVN 5575 (dạng đơn giản hoá – cần đối chiếu TCVN 5575:2024): bền, cắt, ổn định tổng thể trong và ngoài mặt phẳng, độ mảnh, ổn định cục bộ bản cánh/bản bụng, độ võng và chuyển vị ngang (TTGH II).");
        d.Equation("σ = |N|/A + |M3|/W33 + |M2|/W22 ≤ f·γc;     τ = V/(hw·tw) ≤ 0.58·f·γc");
        d.Equation("λ̄ = λ·√(f/E);  λ̄ ≤ 2.5: φ = 1 − (0.073 − 5.53·f/E)·λ̄^1.5");
        d.Equation("2.5 < λ̄ ≤ 4.5: φ = 1.47 − 13·f/E − (0.371 − 27.3·f/E)·λ̄ + (0.0275 − 5.53·f/E)·λ̄²;   λ̄ > 4.5: φ = 332/[λ̄²(51 − λ̄)]");
        d.Equation("Trong mặt phẳng: N/(φx·A·f·γc) + |M3|/[W33·f·γc·(1 − N/Ne)] ≤ 1,  Ne = π²EA/λx² (gần đúng)");
        d.Equation("Ngoài mặt phẳng: N/(φy·A·f·γc) + |M3|/(φb·W33·f·γc) ≤ 1");
        d.Equation($"Chiều dài tính toán: cột chân khớp μ = 2.0; kèo μ = 1.0 (tổng chiều dài dãy kèo); ngoài mặt phẳng l0y = khoảng cách giằng. γc = {N(p.Steel.GammaC, "0.##")}");

        var m0 = s.Steel.OrderByDescending(x => x.MaxRatio).First();
        d.Heading($"{n}.1. Cấu kiện bất lợi nhất: {m0.Name} (tiết diện {m0.Section})", 2);
        d.Paragraph($"Tổ hợp {m0.Combo}, mặt cắt x = {N(m0.Station)} m: N = {N(m0.N)} kN (nén dương), M3 = {N(m0.M)} kN.m, V2 = {N(m0.V)} kN, λmax = {N(m0.Lambda, "0.0")}.");
        d.Table(new[] { "Hạng mục", "Giá trị", "Giới hạn", "Đơn vị", "Tỷ số", "Kết luận", "Ghi chú" },
            m0.Checks.Select(c => new[] { c.Name, N(c.Demand, "0.###"), N(c.Capacity, "0.###"), c.Unit, N(c.Ratio, "0.000"), OkText(c.Ok), c.Note }),
            new[] { 3.6, 1.5, 1.5, 1.1, 1.2, 1.6, 6.0 }, 9);
        foreach (var msg in m0.Messages) d.Paragraph(msg, false, true);

        d.Heading($"{n}.2. Bảng tổng hợp cấu kiện thép", 2);
        d.Table(new[] { "Cấu kiện", "Tiết diện", "Tổ hợp", "N (kN)", "M3 (kN.m)", "λ", "Hạng mục quyết định", "Tỷ số", "KL" },
            s.Steel.Select(x =>
            {
                var gov = x.Checks.OrderByDescending(c => c.Ratio).FirstOrDefault();
                return new[] { x.Name, x.Section, x.Combo, N(x.N, "0.0"), N(x.M, "0.0"), N(x.Lambda, "0"), gov?.Name ?? "", N(x.MaxRatio, "0.000"), OkText(x.Ok) };
            }), new[] { 1.6, 2.2, 1.2, 1.4, 1.5, 1.0, 4.4, 1.3, 1.9 }, 9);
    }

    // ------------------------------------------------------------------
    private static void Conclusion(DesignSummary s, DocxWriter d, int n)
    {
        d.Heading($"{n}. KẾT LUẬN", 1);
        var all = s.All.ToList();
        int fail = all.Count(m => !m.Ok);
        d.Table(new[] { "Loại cấu kiện", "Số lượng", "Không đạt", "Tỷ số lớn nhất" }, new[]
        {
            Row("Dầm", s.Beams), Row("Cột", s.Columns), Row("Sàn", s.Slabs), Row("Móng", s.Footings), Row("Đài cọc", s.PileCaps), Row("Móng băng", s.StripFootings), Row("Kết cấu thép", s.Steel)
        }.Where(x => x != null), new[] { 5.0, 3.5, 3.5, 4.5 });
        if (all.Count == 0) d.Paragraph("Chưa có kết quả thiết kế cấu kiện.");
        else if (fail == 0)
            d.Paragraph("Tất cả cấu kiện được kiểm tra đều thoả mãn các điều kiện về khả năng chịu lực và điều kiện sử dụng theo các tiêu chuẩn hiện hành. Kết cấu đảm bảo đủ khả năng chịu lực.");
        else
        {
            d.Paragraph($"Có {fail} cấu kiện KHÔNG ĐẠT, cần điều chỉnh tiết diện hoặc bố trí lại cốt thép:", true);
            foreach (var m in all.Where(m => !m.Ok).Take(60))
            {
                var bad = string.Join("; ", m.Checks.Where(c => !c.Ok).Select(c => $"{c.Name} ({N(c.Ratio, "0.00")})"));
                var err = m.Messages.Where(x => x.StartsWith("LỖI")).ToList();
                d.Bullet($"{m.Name}{(string.IsNullOrEmpty(m.Story) ? "" : " – " + m.Story)}: {bad}{(err.Count > 0 ? " " + string.Join(" ", err) : "")}");
            }
        }
        d.Paragraph("Thuyết minh được lập tự động bằng chương trình Kết cấu VXT. Người thiết kế có trách nhiệm kiểm tra lại số liệu đầu vào, sơ đồ tính và kết quả trước khi sử dụng.", false, true);
    }

    private static string[] Row<T>(string name, List<T> list) where T : MemberDesignBase
    {
        if (list == null || list.Count == 0) return null;
        return new[] { name, list.Count.ToString(Inv), list.Count(m => !m.Ok).ToString(Inv), N(list.Max(m => m.MaxRatio), "0.000") };
    }

    private static void Appendix(AnalysisResults r, DesignSummary s, DocxWriter d)
    {
        var aw = r?.Warnings ?? new List<string>();
        if (aw.Count == 0 && s.Warnings.Count == 0) return;
        d.PageBreak();
        d.Heading("PHỤ LỤC: CẢNH BÁO VÀ GHI CHÚ", 1);
        if (aw.Count > 0)
        {
            d.Heading("Cảnh báo phân tích", 2);
            foreach (var w in aw) d.Bullet(w);
        }
        if (s.Warnings.Count > 0)
        {
            d.Heading("Cảnh báo thiết kế", 2);
            foreach (var w in s.Warnings) d.Bullet(w);
        }
    }
}
