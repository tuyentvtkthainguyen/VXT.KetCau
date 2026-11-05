using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Test 67 – Khổ giấy ↔ tỷ lệ (dàn trang in chế độ Triển khai hồ sơ): khổ chốt → tỷ lệ lớn nhất vừa khổ, tỷ lệ chốt → khổ nhỏ nhất,
/// vẽ lại hình ở tỷ lệ mới (chữ luôn cao 2,5 mm khi in, tiêu đề "tl: 1/xx" mới), đổi khổ / tỷ lệ một tờ, nhãn mặt bằng không đè nhau.</summary>
public static class PaperScaleTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "TrienKhai");
        Directory.CreateDirectory(dir);

        // ---- hồ sơ: mặt bằng tầng 7,8 × 4,5 m ----
        var p = new DetailingProject { ProjectName = "Thử khổ giấy" };
        p.Plan.SpansX = "4.2 3.6"; p.Plan.SpansY = "4.5";
        var pl = p.Plan;
        var t2 = pl.Levels[1];
        var c1 = pl.Place(p, t2, PlanKinds.Column, 0, 0, 0, 0);
        foreach (var (x, y) in new[] { (4.2, 0.0), (7.8, 0.0), (0.0, 4.5), (4.2, 4.5), (7.8, 4.5) }) pl.Place(p, t2, PlanKinds.Column, x, y, x, y, c1.Member);
        var dA = pl.Place(p, t2, PlanKinds.Beam, 0, 0, 7.8, 0);
        pl.Place(p, t2, PlanKinds.Beam, 0, 4.5, 7.8, 4.5, dA.Member);
        var d2 = pl.Place(p, t2, PlanKinds.Beam, 0, 0, 0, 4.5);
        pl.Place(p, t2, PlanKinds.Beam, 4.2, 0, 4.2, 4.5, d2.Member);
        pl.Place(p, t2, PlanKinds.Beam, 7.8, 0, 7.8, 4.5, d2.Member);
        pl.Place(p, t2, PlanKinds.Slab, 0, 0, 4.2, 4.5);
        pl.Place(p, t2, PlanKinds.Slab, 4.2, 0, 7.8, 4.5);
        var lt = pl.Place(p, t2, PlanKinds.Lintel, 1.5, 4.5, 2.7, 4.5);
        pl.Place(p, t2, PlanKinds.Canopy, 1.3, 4.5, 2.9, 5.1, lt.Member);
        var res = p.RunAll();
        var cat = SheetLayoutEngine.Catalog(p, res);
        var map = cat.ToDictionary(f => f.Id);
        var plan = cat.First(f => f.Part == 0 && f.Caption.StartsWith(StructuralPlanDrawing.TitleLevelPrefix) && !f.Caption.Contains("MÓNG"));
        check("Mặt bằng tầng: tỷ lệ gốc 1/100, vẽ lại được", B(Math.Abs(plan.BaseScale - 100) < 1e-9 && plan.CanRescale), 1, 0);

        // ---- vẽ lại ở tỷ lệ khác: chữ, tiêu đề đúng tỷ lệ mới ----
        var at50 = plan.At(50);
        check("Vẽ lại 1/50: DxfWriter.Scale = 50", at50.W.Scale, 50, 1e-9);
        double MaxH(DxfWriter w) => w.GetPrimitives().Where(q => q.Kind == PrimKind.Text).Select(q => q.H).DefaultIfEmpty(0).Max();
        check("Vẽ lại 1/50: chữ cao gấp 0,5 lần bản 1/100 (cùng cỡ in)", MaxH(at50.W) / MaxH(plan.F.W), 0.5, 1e-6);
        check("Vẽ lại 1/50: tiêu đề ghi \"tl: 1/50\"", B(DxfWriter.Decode(at50.W.ToDxfString()).Contains("tl: 1/50")), 1, 0);
        check("Vẽ lại 1/50: hình học giữ mm thật (bề rộng lưới trục không đổi)",
            at50.W.GetPrimitives().Where(q => q.Kind == PrimKind.Line && q.Layer == Lay.Truc).Select(q => Math.Abs(q.Pts[2] - q.Pts[0])).DefaultIfEmpty(0).Max() >= 7800 ? 1 : 0, 1, 0);
        var beamFig = cat.First(f => f.Part == 1 && f.BaseScale > 40 && f.BaseScale < 60);
        var b75 = beamFig.At(75);
        check("Chi tiết 1/50 vẽ lại 1/75: tỷ lệ và cỡ chữ theo tỷ lệ mới", b75.W.Scale * 1000 + Math.Round(MaxH(b75.W) / MaxH(beamFig.F.W) * 1000), 75 * 1000 + 1500, 1e-9);

        // ---- khổ chốt → tỷ lệ lớn nhất vừa khổ ----
        var setA3 = new DetailSheetSet { AutoPaper = false, Paper = "A3" };
        double sA3 = SheetLayoutEngine.ChooseScale(setA3, plan);
        check("Khổ A3: mặt bằng 7,8 × 4,5 m chọn 1/50 hoặc 1/75", B(sA3 == 50 || sA3 == 75), 1, 0);
        check("Khổ A3: hình vừa vùng vẽ (trừ khung tên)", B(SheetLayoutEngine.Fits(setA3, plan, sA3, "A3")), 1, 0);
        var setA1 = new DetailSheetSet { AutoPaper = false, Paper = "A1" };
        check("Khổ A1: mặt bằng chọn 1/50 (tỷ lệ chi tiết nhất cho mặt bằng)", SheetLayoutEngine.ChooseScale(setA1, plan), 50, 1e-9);
        var setA4 = new DetailSheetSet { AutoPaper = false, Paper = "A4" };
        double sA4 = SheetLayoutEngine.ChooseScale(setA4, plan);
        check("Khổ A4: mặt bằng phải thu (≥ 1/75) và vừa A4", B(sA4 >= 75 && SheetLayoutEngine.Fits(setA4, plan, sA4, "A4")), 1, 0);
        // độ lấp đầy chiều hạn chế trên A3
        var dA3 = SheetGeometry.Drawable("A3");
        double fill = Math.Max(plan.WAt(sA3) / dA3.W, plan.HAt(sA3) / dA3.H);
        check("Khổ A3: lấp đầy chiều hạn chế ≥ 45 %", B(fill >= 0.45), 1, 0);

        // ---- tỷ lệ chốt → khổ nhỏ nhất ----
        var setAuto = new DetailSheetSet { AutoPaper = true, PlanScale = 100, IndexSheet = false };
        SheetLayoutEngine.AutoLayout(setAuto, new List<SheetFigure> { plan });
        var shAuto = setAuto.Sheets.First();
        check("Tỷ lệ 1/100, khổ tự động → A4 hoặc A3", B(shAuto.Paper == "A4" || shAuto.Paper == "A3"), 1, 0);
        check("Tỷ lệ 1/100 giữ trên tờ", shAuto.Figures[0].Scale, 100, 1e-9);
        var setFull = new DetailSheetSet { AutoPaper = true, IndexSheet = false };
        SheetLayoutEngine.AutoLayout(setFull, new List<SheetFigure> { plan });
        check("Hoàn toàn tự động: mặt bằng nhỏ → 1/50 trên A3 trở xuống", B(setFull.Sheets[0].Figures[0].Scale == 50 && Array.IndexOf(SheetGeometry.Papers, setFull.Sheets[0].Paper) <= Array.IndexOf(SheetGeometry.Papers, "A3")), 1, 0);

        // ---- ghép tờ: chữ = cỡ in × mẫu tỷ lệ ----
        var setC = new DetailSheetSet { AutoPaper = false, Paper = "A3", IndexSheet = false };
        SheetLayoutEngine.AutoLayout(setC, new List<SheetFigure> { plan });
        var sh = setC.Sheets[0];
        var cmap = new Dictionary<string, SheetFigure> { [plan.Id] = plan };
        var wS = SheetLayoutEngine.Compose(p, setC, sh, cmap);
        check("Tờ A3: tỷ lệ tờ = tỷ lệ mặt bằng đã chọn", wS.Scale, sA3, 1e-9);
        string beamName = dA.Member;
        var beamLbl = wS.GetPrimitives().FirstOrDefault(q => q.Kind == PrimKind.Text && (q.Text ?? "").StartsWith(beamName + " ("));
        check("Tờ A3: chiều cao chữ nhãn dầm = 2,5 mm × mẫu tỷ lệ", beamLbl?.H ?? 0, new DxfWriter(sA3).Th, 1e-6);
        check("Tờ A3: chữ nhãn dầm in cao 2,5 mm", (beamLbl?.H ?? 0) / wS.Scale, new DxfWriter(1).Th, 1e-6);
        string dS = DxfWriter.Decode(wS.ToDxfString());
        check("Tờ A3: tiêu đề hình \"tl: 1/" + sA3 + "\", khung tên ghi tỷ lệ", B(dS.Contains("tl: 1/" + sA3)) + B(dS.Contains("Tỷ lệ: 1/" + sA3)), 2, 0);
        CadFileWriters.Write(wS, Path.Combine(dir, "KhoGiay_A3_MatBang.dxf"), CadOutputFormat.DxfR12);

        // ---- đổi khổ / tỷ lệ một tờ ----
        SheetLayoutEngine.RefitSheet(setC, sh, cmap, "A1", DetailSheetSet.OrientLandscape, 100);
        check("Đổi tờ sang A1 – 1/100: tỷ lệ hình, khổ, khoá tờ", B(sh.Paper == "A1") + B(sh.Figures.Count == 1 && sh.Figures[0].Scale == 100) + B(sh.Locked), 3, 0);
        SheetLayoutEngine.RefitSheet(setC, sh, cmap, DetailSheetSet.PaperAuto, DetailSheetSet.OrientLandscape, 100);
        check("Tờ tỷ lệ 1/100, khổ tự động → khổ nhỏ nhất (A4/A3)", B(sh.Paper == "A4" || sh.Paper == "A3"), 1, 0);
        SheetLayoutEngine.RefitSheet(setC, sh, cmap, "A2", DetailSheetSet.OrientLandscape, 0);
        check("Tờ A2, tỷ lệ tự động → 1/50", sh.Figures[0].Scale, 50, 1e-9);
        string d2s = DxfWriter.Decode(SheetLayoutEngine.Compose(p, setC, sh, cmap).ToDxfString());
        check("Tờ A2: tiêu đề hình tl: 1/50", B(d2s.Contains("tl: 1/50")), 1, 0);

        // ---- nhãn mặt bằng không đè nhau ----
        var wq = at50.W;
        var prims = wq.GetPrimitives().Where(q => q.Kind == PrimKind.Text).ToList();
        Rect2 Box(DrawPrim q) => wq.TextBox(q.Pts[0], q.Pts[1], q.H, q.Text, q.Rot, q.HA, q.VA);
        var colNames = new HashSet<string>(p.Columns.Select(c => c.Name));
        var beamNames = p.Beams.Select(b => b.Name + " (").ToList();
        var colBoxes = prims.Where(q => colNames.Contains(q.Text)).Select(Box).ToList();
        var beamBoxes = prims.Where(q => beamNames.Any(n => q.Text.StartsWith(n))).Select(Box).ToList();
        check("Mặt bằng: có nhãn cột và nhãn dầm", B(colBoxes.Count >= 6) + B(beamBoxes.Count >= 5), 2, 0);
        int hit = beamBoxes.Sum(bb => colBoxes.Count(cb => bb.Intersects(cb)));
        check("Mặt bằng: nhãn dầm không đè nhãn cột", hit, 0, 0);
        int bb2 = 0;
        for (int i = 0; i < beamBoxes.Count; i++) for (int j = i + 1; j < beamBoxes.Count; j++) if (beamBoxes[i].Intersects(beamBoxes[j])) bb2++;
        check("Mặt bằng: nhãn dầm không đè nhau", bb2, 0, 0);
        var ltBox = prims.Where(q => q.Text == lt.Member).Select(Box).ToList();
        var canBox = prims.Where(q => (q.Text ?? "").StartsWith("Ô VĂNG")).Select(Box).ToList();
        check("Mặt bằng: nhãn lanh tô / ô văng không đè nhãn dầm, cột",
            ltBox.Concat(canBox).Sum(a => beamBoxes.Count(b => a.Intersects(b)) + colBoxes.Count(b => a.Intersects(b))), 0, 0);
        var barBoxes = prims.Where(q => (q.Text ?? "").StartsWith("Ø")).Select(Box).ToList();
        int bOv = 0;
        for (int i = 0; i < barBoxes.Count; i++) for (int j = i + 1; j < barBoxes.Count; j++) if (barBoxes[i].Intersects(barBoxes[j])) bOv++;
        check("Thép sàn trên mặt bằng: chữ Ø..a.. không đè nhau", bOv, 0, 0);
        // chữ thép sàn ↔ vòng số hiệu: khe hở ≥ 0
        var circles = wq.GetPrimitives().Where(q => q.Kind == PrimKind.Circle && Math.Abs(q.R - wq.MarkR) < 1e-6).ToList();
        int tc = 0;
        foreach (var bx in barBoxes)
            foreach (var c in circles)
                if (bx.Intersects(new Rect2(c.Pts[0] - c.R, c.Pts[1] - c.R, c.Pts[0] + c.R, c.Pts[1] + c.R))) tc++;
        check("Thép sàn trên mặt bằng: chữ không đè vòng số hiệu", tc, 0, 0);
    }
}
