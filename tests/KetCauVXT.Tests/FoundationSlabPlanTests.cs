using KetCauVXT.Detailing;
using KetCauVXT.Drawing;

namespace KetCauVXT.Tests;

/// <summary>Test 62 – Đài cọc chọn bố trí thép (chỉ lớp dưới / lớp dưới + lớp trên / lồng thép chống chọc thủng TCVN 5574:2018 8.1.6.3.3),
/// râu thép đầu cọc neo trong đài, móng băng có đoạn vát, thép sàn trên mặt bằng kết cấu, đường cắt + mặt cắt thép sàn do người dùng đặt,
/// cây thư mục cấu kiện theo mặt bằng kết cấu (StructuralPlan.GroupForTree), lưu / mở đường cắt.</summary>
public static class FoundationSlabPlanTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        static int B(bool v) => v ? 1 : 0;
        var s = new DetailSettings();
        var figs = new List<Figure>();

        // ---- chọc thủng có cốt thép ngang (thép đứng lồng) – công thức đóng ----
        double asw = 8 * Dt.Area(10), rsw = 170000;
        var (fsw1, fu1) = ManualPileCap.PunchWithLinks(1000, rsw, asw, 2, 0.20, 0.5);
        double fswExp = 0.8 * (rsw * asw * 2 / 0.20) * 0.5;
        check("Chọc thủng có lồng thép: Fsw = 0,8·qsw·u (qsw = Rsw·Asw/sw)", fsw1, fswExp, 1e-9);
        check("Chọc thủng có lồng thép: Fult = Fb + Fsw", fu1, 1000 + fswExp, 1e-9);
        var (fsw2, fu2) = ManualPileCap.PunchWithLinks(1000, rsw, asw, 2, 0.20, 0.1);
        check("Chọc thủng: Fsw < 0,25Fb → bỏ qua cốt thép (Fult = Fb)", fsw2 * 1e6 + fu2, 1000, 1e-9);
        var (fsw3, fu3) = ManualPileCap.PunchWithLinks(1000, rsw, asw, 2, 0.20, 5.0);
        check("Chọc thủng: Fsw lấy ≤ Fb → Fult ≤ 2Fb", fsw3 + fu3, 3000, 1e-9);

        // ---- đài cọc: 3 cách bố trí thép ----
        ManualPileCap Cap(string layout) => new() { Name = "Đ1", N = 1200, H = 0.70, RebarLayout = layout };
        var c1 = Cap("Chỉ lớp dưới"); var r1 = c1.Run(s);
        int n1 = c1.Piles.Count;
        check("Đài chỉ lớp dưới (H = 700): không có lưới trên, không có lồng", B(!r1.Bars.Any(b => b.Role.StartsWith("Lớp trên")) && !r1.Bars.Any(b => b.Role.StartsWith("Thép đứng lồng"))), 1, 0);
        var anc = r1.Bars.FirstOrDefault(b => b.Role == "Râu thép đầu cọc");
        check("Râu thép đầu cọc: số thanh = 4 × số cọc", anc?.Count ?? -1, 4 * n1, 0);
        check("Râu thép đầu cọc: Ø16", anc?.Dia ?? 0, 16, 0);
        check("Râu thép đầu cọc: chiều dài neo mặc định max(30d; 400) = 480 mm", c1.AnchorUsed, 0.48, 1e-9);
        var ka = r1.Checks.FirstOrDefault(k => k.Name.StartsWith("Râu thép đầu cọc"));
        check("Kiểm tra neo râu cọc La ≥ 30d: đạt", B(ka != null && ka.Ok), 1, 0);

        var c2 = Cap("Lớp dưới + lớp trên"); var r2 = c2.Run(s);
        check("Đài lớp dưới + lớp trên: có thép lớp trên phương X và Y", B(r2.Bars.Any(b => b.Role == "Lớp trên phương X")) + B(r2.Bars.Any(b => b.Role == "Lớp trên phương Y")), 2, 0);
        var topX = r2.Bars.First(b => b.Role == "Lớp trên phương X");
        check("Lưới trên mặc định Ø12a200 (không có nhổ)", topX.Dia * 1000 + topX.Spacing * 1000, 12200, 1e-9);
        check("Lưới trên là thép trên (IsTop)", B(topX.IsTop), 1, 0);

        var c3 = Cap("Lồng thép (lớp dưới + lớp trên + thép đứng chống chọc thủng)"); var r3 = c3.Run(s);
        var cage = r3.Bars.FirstOrDefault(b => b.Role == "Thép đứng lồng chống chọc thủng");
        check("Đài lồng thép: có thép đứng Ø10 (mặc định)", cage?.Dia ?? 0, 10, 0);
        check("Đài lồng thép: kiểm tra chọc thủng F ≤ Fb + Fsw", B(r3.Checks.Any(k => k.Name.StartsWith("Chọc thủng cột – đài có lồng thép"))), 1, 0);
        check("Đài lồng thép: vẫn có lưới trên + râu cọc", B(r3.Bars.Any(b => b.Role.StartsWith("Lớp trên"))) + B(r3.Bars.Any(b => b.Role == "Râu thép đầu cọc")), 2, 0);
        check("Đài lồng thép: mọi thanh có số hiệu", B(r3.Bars.All(b => b.Mark > 0)), 1, 0);
        var f3 = c3.Draw(r3, s);
        string d3 = string.Concat(f3.Select(f => f.W.ToDxfString()));
        check("Bản vẽ đài lồng thép: có râu cọc, ĐẬP ĐẦU CỌC, thép đứng", B(d3.Contains("râu cọc")) + B(d3.Contains("ĐẬP ĐẦU CỌC")) + B(d3.Contains("thép đứng")), 3, 0);
        check("Bản vẽ đài lồng thép: thép trên có ghi chú (trên)", B(d3.Contains("(trên)")), 1, 0);
        figs.AddRange(f3);

        // ---- móng băng có đoạn vát ----
        var st = new ManualStrip { Name = "MB-V", B = 2.0, Hf = 0.25, Hv = 0.40, Hs = 0.80 };
        var rs = st.Run(s);
        double areaExp = (2.0 - st.Bs) * (0.25 + 0.40) / 2 + st.Bs * 0.80;
        check("Móng băng vát: HRoot = Hv = 400 mm", st.HRoot, 0.40, 1e-12);
        check("Móng băng vát: V = [(B − bs)(Hf + Hv)/2 + bs·hs]·L", rs.ConcreteVolume, areaExp * st.Length, 1e-9);
        check("Móng băng vát: thuyết minh h0 tại chân sườn theo Hv", B(rs.Calc.Any(l => l.Contains("đoạn vát"))), 1, 0);
        var stFlat = new ManualStrip { Name = "MB-P", B = 2.0, Hf = 0.25, Hv = 0, Hs = 0.80 };
        var rsp = stFlat.Run(s);
        check("Móng băng phẳng (Hv = 0): HRoot = Hf, V nhỏ hơn bản vát", B(Math.Abs(stFlat.HRoot - 0.25) < 1e-12 && rsp.ConcreteVolume < rs.ConcreteVolume), 1, 0);
        var fs = st.Draw(rs, s);
        check("Bản vẽ móng băng vát: có hình", B(fs.Count > 0 && fs.All(f => f.W.EntityCount > 10)), 1, 0);
        figs.AddRange(fs);

        // ---- hồ sơ mẫu: cây theo mặt bằng ----
        var p = DetailingProject.Sample();
        var groups = StructuralPlan.GroupForTree(p);
        PlanTreeGroup G(string kind) => groups.FirstOrDefault(g => g.Kind == kind);
        check("Cây: nhóm đầu là Mặt bằng móng", B(groups.Count > 0 && groups[0].Title == "Mặt bằng móng"), 1, 0);
        check("Cây: Mặt bằng móng chứa móng M1", B(G("Mong")?.Members.Any(m => m.Name == "M1") == true), 1, 0);
        check("Cây: Mặt bằng dầm móng chứa D1 (đặt trên mặt bằng móng)", B(G("DamMong")?.Members.Any(m => m.Name == "D1") == true), 1, 0);
        check("Cây: Mặt bằng định vị cột chứa C1", B(G("Cot")?.Members.Any(m => m.Name == "C1") == true), 1, 0);
        var t2g = groups.FirstOrDefault(g => g.Kind == "Tang" && g.Level?.Name == "Tầng 2");
        check("Cây: Mặt bằng kết cấu Tầng 2 chứa sàn S1, lanh tô LT1, cầu thang CT1, không chứa cột",
              B(t2g != null && t2g.Members.Any(m => m.Name == "S1") && t2g.Members.Any(m => m.Name == "LT1") && t2g.Members.Any(m => m.Name == "CT1") && !t2g.Members.Any(m => m is ManualColumn)), 1, 0);
        var union = groups.SelectMany(g => g.Members).Distinct().Count();
        check("Cây: mọi cấu kiện hồ sơ đều có trong cây (đặt hoặc chưa đặt)", union, p.All.Count(), 0);
        var un = G("ChuaDat");
        check("Cây: nhóm Chưa đặt trên mặt bằng nằm cuối và không trùng cấu kiện đã đặt",
              B(un == null || (groups[^1] == un && !un.Members.Any(m => groups.Take(groups.Count - 1).Any(g => g.Members.Contains(m))))), 1, 0);

        // ---- thép sàn trên mặt bằng tầng ----
        var res = p.RunAll();
        var map = StructuralPlanDrawing.Map(p, res);
        var sl = p.Slabs.First(x => x.Name == "S1");
        var rsl = map["S1"].R;
        var bS = rsl.Bars.First(b => b.Role == "Lớp dưới phương ngắn");
        var t2 = p.Plan.Levels.First(l => l.Name == "Tầng 2");
        string title2 = StructuralPlanDrawing.LevelTitle(t2);
        var planFigs = StructuralPlanDrawing.Build(p, res);
        string dPlan = planFigs.First(f => f.Name == title2).W.ToDxfString();
        check("Mặt bằng tầng: có thép sàn lớp dưới " + Dt.Spaced(bS.Dia, bS.Spacing), B(dPlan.Contains(Dt.Spaced(bS.Dia, bS.Spacing))), 1, 0);
        var tL = rsl.Bars.FirstOrDefault(b => b.Role == "Mũ gối cạnh dài");
        check("Mặt bằng tầng: có thép mũ gối", B(tL == null || dPlan.Contains(Dt.Spaced(tL.Dia, tL.Spacing))), 1, 0);
        check("Mặt bằng tầng: không có hình mặt cắt khi chưa đặt đường cắt", planFigs.Count(f => f.Name.StartsWith("MẶT CẮT ")), 0, 0);

        // ---- đường cắt do người dùng chọn → mặt cắt thép sàn ----
        string nm1 = p.Plan.NextSectionName();
        check("Tên mặt cắt đầu tiên 1-1", B(nm1 == "1-1"), 1, 0);
        p.Plan.Sections.Add(new SectionMark { Name = nm1, Level = "Tầng 2", X1 = 6.0, Y1 = -0.3, X2 = 6.0, Y2 = 4.8 });
        check("Tên mặt cắt kế tiếp 2-2", B(p.Plan.NextSectionName() == "2-2"), 1, 0);
        var figs2 = StructuralPlanDrawing.Build(p, res);
        int iPlan = figs2.FindIndex(f => f.Name == title2);
        check("Mặt cắt 1-1 xuất ngay sau mặt bằng Tầng 2", B(iPlan >= 0 && iPlan + 1 < figs2.Count && figs2[iPlan + 1].Name == "MẶT CẮT 1-1 (tl 1/25)"), 1, 0);
        var sec = figs2.First(f => f.Name == "MẶT CẮT 1-1 (tl 1/25)");
        check("Mặt cắt tỷ lệ 1/25", sec.W.Scale, 25, 0);
        string dSec = sec.W.ToDxfString();
        check("Mặt cắt: tiêu đề MẶT CẮT 1-1, tên dầm D1 bị cắt, tên ô S1", B(dSec.Contains("MẶT CẮT 1-1")) + B(dSec.Contains("D1")) + B(dSec.Contains("S1")), 3, 0);
        var bL = rsl.Bars.First(b => b.Role == "Lớp dưới phương dài");
        // ô S1: 3,6 × 4,5 → cạnh ngắn theo X; đường cắt dọc Y → thanh phương dài thể hiện là đường, phương ngắn là chấm
        check("Mặt cắt: có ghi thép lớp dưới hai phương", B(dSec.Contains(Dt.Spaced(bL.Dia, bL.Spacing))) + B(dSec.Contains(Dt.Spaced(bS.Dia, bS.Spacing))), 2, 0);
        string dPlan2 = figs2[iPlan].W.ToDxfString();
        check("Mặt bằng: có ký hiệu đường cắt (chữ 1)", B(dPlan2.Length > dPlan.Length), 1, 0);
        var gT2 = StructuralPlan.GroupForTree(p).First(g => g.Kind == "Tang" && g.Level?.Name == "Tầng 2");
        var pv = StructuralPlanDrawing.ForGroup(p, res, gT2);
        check("Xem trước nút mặt bằng Tầng 2: mặt bằng + mặt cắt 1-1", B(pv.Count == 2 && pv[0].Name == title2 && pv[1].Name.StartsWith("MẶT CẮT 1-1")), 1, 0);
        var pvF = StructuralPlanDrawing.ForGroup(p, res, StructuralPlan.GroupForTree(p)[0]);
        check("Xem trước nút Mặt bằng móng: mặt bằng kết cấu móng", B(pvF.Count >= 1 && pvF[0].Name == StructuralPlanDrawing.TitleFoundation), 1, 0);
        figs.Add(figs2[iPlan]); figs.Add(sec);

        // đường cắt không qua cấu kiện
        p.Plan.Sections.Add(new SectionMark { Name = "9-9", Level = "Tầng 2", X1 = 20, Y1 = 20, X2 = 25, Y2 = 20 });
        var empty = StructuralPlanDrawing.SlabSection(p.Plan, t2, p.Plan.Sections[^1], map, s);
        check("Đường cắt ngoài mặt bằng: ghi chú không cắt qua ô sàn", B(empty.ToDxfString().Contains("không qua ô sàn")), 1, 0);
        p.Plan.Sections.RemoveAt(p.Plan.Sections.Count - 1);

        // cắt hình chữ nhật (Liang–Barsky)
        bool hit = StructuralPlanDrawing.ClipRect(0, 1, 1, 0, 10, 2, 0, 5, 3, out double t0, out double t1);
        check("Cắt đoạn qua hình chữ nhật: t ∈ [2; 5]", B(hit) * 100 + t0 * 10 + t1, 125, 1e-9);
        check("Cắt đoạn ngoài hình chữ nhật: không giao", B(StructuralPlanDrawing.ClipRect(0, 4, 1, 0, 10, 2, 0, 5, 3, out _, out _)), 0, 0);

        // ---- lưu / mở giữ đường cắt ----
        var back = DetailingProject.FromJson(p.ToJson());
        var sb = back.Plan.Sections.FirstOrDefault();
        check("Lưu / mở: giữ đường cắt 1-1 (tên, mặt bằng, toạ độ)", B(back.Plan.Sections.Count == 1 && sb.Name == "1-1" && sb.Level == "Tầng 2" && Math.Abs(sb.Y2 - 4.8) < 1e-9), 1, 0);
        var legacy = DetailingProject.FromJson("{\"ProjectName\":\"cũ\",\"Plan\":{\"SpansX\":\"4\",\"Sections\":null}}");
        check("Mở hồ sơ cũ (Sections null) → danh sách rỗng", B(legacy.Plan.Sections != null && legacy.Plan.Sections.Count == 0), 1, 0);

        // ---- lưu DXF ----
        string dir = Path.Combine(outDir, "TrienKhai");
        Directory.CreateDirectory(dir);
        var (sc, paper) = DetailingExport.SheetFor(figs);
        string file = Path.Combine(dir, "MongSanMatCat.dxf");
        SheetComposer.Compose(figs, sc, new SheetComposer.SheetInfo { ProjectName = "Kiểm thử móng – sàn – mặt cắt", Title = "ĐÀI CỌC LỒNG THÉP – MÓNG BĂNG VÁT – MẶT CẮT SÀN", SheetNo = "KC-MS" }, paper).Save(file);
        check("Lưu DXF đài cọc – móng băng – mặt cắt sàn", B(File.Exists(file) && new FileInfo(file).Length > 1000), 1, 0);
    }
}
