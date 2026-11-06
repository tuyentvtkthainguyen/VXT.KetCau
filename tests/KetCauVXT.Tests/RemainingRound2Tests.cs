using System.Text.Json;
using System.Text.Json.Serialization;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Import;
using KetCauVXT.Mep;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Phần còn lại (đợt 2): kéo cao độ trong khung mặt cắt (đổi toạ độ khung nhìn), nắm trục ở giữa đường trục,
/// bong bóng / ẩn trục trên mặt bằng kết cấu Triển khai (DXF), cài đặt điện nước (luật độ dốc min / max, cao độ theo nhóm, quang treo),
/// điều hoà – thông gió (chọn cỡ ống gió Q = v·A, m² tôn, ống đồng, nước ngưng có dốc, va chạm dầm, nhập Revit, mặt bằng),
/// kéo thiết bị điện nước trong 3D (bắt bước dọc tường / trên sàn).</summary>
public static class RemainingRound2Tests
{
    private static double B(bool b) => b ? 1 : 0;

    private static Project House()
    {
        var p = ModelBuilder.NewProject("Kiểm tra phần còn lại đợt 2");
        ModelBuilder.SetGrid(p, new[] { 5.0, 4.0 }, new[] { 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        WallAssembly.Upgrade(p);
        return p;
    }

    private static Project Wc()
    {
        var p = new Project();
        p.Stories = new List<Story>
        {
            new() { Name = "T1", Elevation = 0, Height = 3.3 }, new() { Name = "T2", Elevation = 3.3, Height = 3.3 }, new() { Name = "T3", Elevation = 6.6, Height = 3.3 }
        };
        if (!p.SlabSections.Any(s => s.Name == "S120")) p.SlabSections.Add(new SlabSection { Name = "S120", Thickness = 0.12 });
        p.Nodes.Add(new Node { Id = 1, X = 0, Y = 0, Z = 3.3, Story = "T2" });
        p.Nodes.Add(new Node { Id = 2, X = 4, Y = 0, Z = 3.3, Story = "T2" });
        p.Nodes.Add(new Node { Id = 3, X = 4, Y = 3, Z = 3.3, Story = "T2" });
        p.Nodes.Add(new Node { Id = 4, X = 0, Y = 3, Z = 3.3, Story = "T2" });
        p.Slabs.Add(new Slab { Id = 1, Label = "S1", Nodes = new() { 1, 2, 3, 4 }, Section = "S120", Story = "T2" });
        p.Walls = new List<WallLine>
        {
            new() { Id = 1, Story = "T2", X1 = 0, Y1 = 0, X2 = 4, Y2 = 0, Type = "Tường 220 trát 2 mặt" },
            new() { Id = 2, Story = "T2", X1 = 4, Y1 = 0, X2 = 4, Y2 = 3, Type = "Tường 220 trát 2 mặt" },
            new() { Id = 3, Story = "T2", X1 = 4, Y1 = 3, X2 = 0, Y2 = 3, Type = "Tường 220 trát 2 mặt" },
            new() { Id = 4, Story = "T2", X1 = 0, Y1 = 3, X2 = 0, Y2 = 0, Type = "Tường 220 trát 2 mặt" },
        };
        return p;
    }

    private static int Count(string dxf, string ent) =>
        dxf.Replace("\r", "").Split('\n').Count(l => l.Trim() == ent);

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "TrienKhai", "DienNuoc");
        Directory.CreateDirectory(dir);

        // ================= 1. Kéo cao độ trong khung mặt cắt (đổi toạ độ khung nhìn) =================
        var p = House();
        var mai = p.Stories.OrderBy(s => s.Elevation).Last();
        double z0 = mai.Elevation;
        double extY1 = -1000, extH = 9000, k = 0.01, bY = 0, bH = extH * k, imgH = 900;
        double py = LevelDragMath.ViewYFromModelZ(z0, imgH, bY, bH, extY1, extH, k);
        check("Mặt cắt: z → px → z (khứ hồi)", LevelDragMath.ModelZFromView(py, imgH, bY, bH, extY1, extH, k), z0, 1e-9);
        check("Mặt cắt: cao độ cao hơn → px nhỏ hơn (y hướng xuống)",
            B(LevelDragMath.ViewYFromModelZ(z0 + 1, imgH, bY, bH, extY1, extH, k) < py), 1, 0);
        var hitLv = LevelDragMath.NearestStory(p, LevelDragMath.ModelZFromView(py + 3, imgH, bY, bH, extY1, extH, k), 0.3);
        check("Mặt cắt: bấm gần đường cao độ (3 px) → nắm tầng mái", B(hitLv != null && hitLv.Name == mai.Name), 1, 0);
        double pyNew = LevelDragMath.ViewYFromModelZ(z0 + 0.2037, imgH, bY, bH, extY1, extH, k);
        double zSet = LevelDragMath.Drop(p, mai.Name, LevelDragMath.ModelZFromView(pyNew, imgH, bY, bH, extY1, extH, k));
        check("Mặt cắt: thả → cao độ mới bắt 10 mm", zSet, z0 + 0.2, 1e-6);
        check("Mặt cắt: tầng mái đã đổi cao độ", p.Stories.First(s => s.Name == mai.Name).Elevation, z0 + 0.2, 1e-6);

        // ================= 2. Nắm trục ở giữa đường trục =================
        var h = House();
        string st = h.Stories.OrderBy(s => s.Elevation).First().Name;
        var g1 = GridDragMath.HitGridLine(h, st, 5.05, -0.8, 0.2);
        check("Trục: bấm đuôi trục (ngoài khung lưới) → trục x = 5", B(g1 != null && g1.Dir == GridDir.X && Math.Abs(g1.Coord - 5) < 1e-9), 1, 0);
        check("Trục: bấm giữa khung lưới (không Alt) → không nắm", B(GridDragMath.HitGridLine(h, st, 5.05, 2.0, 0.2) == null), 1, 0);
        var g2 = GridDragMath.HitGridLine(h, st, 5.05, 2.0, 0.2, 1.2, true);
        check("Trục: giữ Alt → nắm cả đoạn trong khung", B(g2 != null && Math.Abs(g2.Coord - 5) < 1e-9), 1, 0);
        check("Trục: ngoài phạm vi vẽ trục → không nắm", B(GridDragMath.HitGridLine(h, st, 5.05, -2.0, 0.2) == null), 1, 0);
        var gy = GridDragMath.HitGridLine(h, st, 9.9, 4.45, 0.2);
        check("Trục: đuôi trục ngang phải → trục y = 4,5", B(gy != null && gy.Dir == GridDir.Y && Math.Abs(gy.Coord - 4.5) < 1e-9), 1, 0);
        g1.HiddenStories.Add(st);
        check("Trục: trục ẩn ở tầng → không nắm", B(GridDragMath.HitGridLine(h, st, 5.05, -0.8, 0.2) == null), 1, 0);

        // ================= 3. Mặt bằng kết cấu Triển khai: bong bóng / ẩn trục (DXF) =================
        var dp = new DetailingProject();
        dp.Plan.SpansX = "4.2 3.6"; dp.Plan.SpansY = "4.5";
        var pl = dp.Plan;
        var lvA = pl.Levels[1]; var lvB = pl.Levels[2];
        var map = new Dictionary<string, (IDetailMember M, DetailResult R)>();
        int c0 = Count(StructuralPlanDrawing.Level(pl, lvA, map, "A", false).ToDxfStringDec(), "CIRCLE");
        check("Triển khai: mặc định 2 bong bóng / trục (3 + 2 trục → 10)", pl.BubbleCount(lvA.Name), 10, 0);
        check("Triển khai: DXF có ≥ 10 vòng tròn (bong bóng)", B(c0 >= 10), 1, 0);
        pl.SetBubbles(true, 0, false, true);
        int c1 = Count(StructuralPlanDrawing.Level(pl, lvA, map, "A", false).ToDxfStringDec(), "CIRCLE");
        check("Triển khai: trục 1 chỉ đầu trên → bớt 1 vòng tròn", c1, c0 - 1, 0);
        pl.SetGridHidden(false, 1, lvA.Name, true);
        string dA = StructuralPlanDrawing.Level(pl, lvA, map, "A", false).ToDxfStringDec();
        check("Triển khai: ẩn trục B trên mặt bằng A → bớt 2 vòng tròn", Count(dA, "CIRCLE"), c0 - 3, 0);
        check("Triển khai: BubbleCount giảm 3", pl.BubbleCount(lvA.Name), 7, 0);
        int cB = Count(StructuralPlanDrawing.Level(pl, lvB, map, "B", false).ToDxfStringDec(), "CIRCLE");
        check("Triển khai: mặt bằng khác vẫn hiện trục B", cB, c0 - 1, 0);
        PlanGridEdit.Insert(pl, true, 2.0);   // thêm trục giữa 1 và 2 → trục "1" giữ kiểu, trục cũ "2" thành "3"
        check("Triển khai: chèn trục giữ kiểu trục 1 (chỉ đầu trên)", B(!pl.StyleOf(true, 0).BubbleStart && pl.StyleOf(true, 0).BubbleEnd), 1, 0);
        check("Triển khai: trục mới mặc định 2 bong bóng", B(pl.StyleOf(true, 1).BubbleStart && pl.StyleOf(true, 1).BubbleEnd), 1, 0);
        check("Triển khai: hiện lại mọi trục mặt bằng A", pl.ShowAllGrids(lvA.Name), 1, 0);
        var back = JsonSerializer.Deserialize<StructuralPlan>("{\"SpansX\":\"4\",\"SpansY\":\"3\"}");
        check("Triển khai: JSON cũ không có GridStyles → 4 trục × 2 bong bóng", back.BubbleCount("x"), 8, 0);

        // ================= 4. Cài đặt điện nước: luật độ dốc, cao độ theo nhóm, quang treo =================
        var set = new MepSettings();
        set.SlopeRules.Add(new MepSlopeRule { System = MepSystemKind.ThoatNuocThai, DiaMin = 100, Slope = 0.015, SlopeMin = 0.012 });
        set.SlopeRules.Add(new MepSlopeRule { System = MepSystemKind.ThoatNuocMua, Slope = 0.005, SlopeMin = 0.01, SlopeMax = 0.05 });
        check("Cài đặt: Ø110 thải theo luật 1,5 %", MepDrainage.DefaultSlope(set, 110, MepSystemKind.ThoatNuocThai), 0.015, 1e-12);
        check("Cài đặt: Ø42 thải ngoài dải Ø → mặc định 3 %", MepDrainage.DefaultSlope(set, 42, MepSystemKind.ThoatNuocThai), 0.03, 1e-12);
        check("Cài đặt: luật 0,5 % kẹp min 1 %", MepDrainage.DefaultSlope(set, 90, MepSystemKind.ThoatNuocMua), 0.01, 1e-12);
        check("Cài đặt: kẹp max 5 %", MepDrainage.ClampRule(set.SlopeRules[1], 0.2), 0.05, 1e-12);
        check("Cài đặt: quang treo theo Ø khi 0", MepDrainage.HangerSpacing(set, 110), 1.2, 1e-12);
        set.HangerSpacing = 0.9;
        check("Cài đặt: quang treo cố định 0,9 m", MepDrainage.HangerSpacing(set, 110), 0.9, 1e-12);

        var w = Wc();
        var wc = MepRouting.PlaceOnWall(w, "Bồn cầu", w.Walls[0], 1.0, 1);
        var lv = MepRouting.PlaceOnWall(w, "Lavabo", w.Walls[0], 2.5, 1);
        var runs = MepDrainage.ConnectToStack(w, "T2");
        MepRouting.RebuildAll(w);
        var rw = runs.First(r => r.FromId == wc.Id);
        double Lh = MepDrainage.HorizontalLength(rw);
        double drop0 = rw.Path[1].Z - rw.Path[^1].Z;
        check("Cài đặt: trước – bồn cầu dốc 2 %", drop0, 0.02 * Lh, 1e-9);
        w.Mep.Settings.SlopeRules.Add(new MepSlopeRule { System = MepSystemKind.ThoatNuocThai, DiaMin = 100, Slope = 0.015 });
        foreach (var r in w.Mep.Runs) MepRouting.Route(w, r);
        rw = w.Mep.Run(rw.Id);
        double Lh2 = MepDrainage.HorizontalLength(rw);
        check("Cài đặt: sau – đáy ống tụt theo luật 1,5 %", rw.Path[1].Z - rw.Path[^1].Z, 0.015 * Lh2, 1e-9);
        var rl = w.Mep.Runs.First(r => r.FromId == lv.Id);
        check("Cài đặt: lavabo Ø42 không theo luật Ø ≥ 100 (3 %)", rl.Path[1].Z - rl.Path[^1].Z, 0.03 * MepDrainage.HorizontalLength(rl), 1e-9);
        w.Mep.Settings.CategoryHeights.Add(new MepCategoryHeight { Category = MepCategory.OCam, Height = 0.3 });
        var oc = MepRouting.PlaceOnWall(w, "Ổ cắm đơn", w.Walls[1], 1.0, 1);
        check("Cài đặt: ổ cắm đặt mới cao 0,3 m theo nhóm", oc.Height, 0.3, 1e-12);
        var ct = MepRouting.PlaceOnWall(w, "Công tắc 1 hạt", w.Walls[1], 2.0, 1);
        check("Cài đặt: nhóm khác giữ cao độ họ (1,25 m)", ct.Height, 1.25, 1e-12);

        // ================= 5. Điều hoà – thông gió: chọn cỡ =================
        check("ĐHKK: A = Q/(3600·v) = 1800/3600/5 = 0,1 m²", HvacSizing.Area(1800, 5), 0.1, 1e-12);
        check("ĐHKK: ống tròn Ø chuẩn ≥ 356,8 → Ø400", HvacSizing.RoundDia(1800, 5), 400, 0);
        var rect = HvacSizing.RectSize(1800, 5, 0, 4);
        check("ĐHKK: ống chữ nhật 500 × 200 (h = 150 → w/h > 4)", rect.W * 1000 + rect.H, 500200, 0);
        check("ĐHKK: Q = v·A → v thực = 5 m/s", HvacSizing.Velocity(1800, rect.W, rect.H), 5, 1e-9);
        check("ĐHKK: h cho trước 250 → w = 400", HvacSizing.RectSize(1800, 5, 250).W, 400, 0);
        check("ĐHKK: Ø tương đương 500 × 200 ≈ 341 mm", HvacSizing.EquivalentDia(500, 200), 1.30 * Math.Pow(1e5, 0.625) / Math.Pow(700, 0.25), 1e-9);
        check("ĐHKK: tôn 0,75 mm cho cạnh 500", HvacSizing.SheetThickness(500), 0.75, 0);
        var cp = HvacSizing.CopperPair(18000);
        check("ĐHKK: ống đồng 18000 BTU = Ø6,35 / Ø12,7", cp.Liquid * 100 + cp.Gas, 635 + 12.7, 1e-9);

        // ================= 6. ĐHKK: khối lượng, ống đồng, nước ngưng, va chạm dầm =================
        var q = House();
        string s1 = q.Stories.OrderBy(s => s.Elevation).First().Name;
        double top = MepRouting.StoryElevation(q, s1) + MepRouting.StoryHeight(q, s1, q.Mep.Settings);
        var duct = HvacRouting.AddDuct(q, s1, 1, 1, 8, 3, HvacDuctSystem.GioCap, 1800);
        // ống chính dùng vận tốc mặc định VelocityMain = 6 m/s: A = 1800/3600/6 = 0,0833 m² → h = 150 cho w = 600 (w/h = 4 ≤ MaxAspect) –
        // quy tắc chọn h chuẩn NHỎ NHẤT (ống dẹt nhất cho phép) để giữ chiều cao trần; 500 × 200 chỉ đúng với v = 5 m/s (mục 5)
        check("ĐHKK: ống gió tự chọn (v = 6 m/s) 600 × 150", duct.W * 1000 + duct.H, 600150, 0);
        check("ĐHKK: v thực ≤ 6 m/s", B(HvacSizing.Velocity(1800, duct.W, duct.H) <= 6 + 1e-9), 1, 0);
        check("ĐHKK: tim ống sát trần = đỉnh tầng − 0,1 − 0,075", duct.Path[0].Z, top - 0.1 - 0.075, 1e-9);
        check("ĐHKK: tuyến trực giao dài 7 + 2 = 9 m", duct.Length, 9, 1e-9);
        check("ĐHKK: m² tôn = 2(0,6 + 0,15) × 9 = 13,5", duct.SheetArea, 13.5, 1e-9);
        var fits = HvacRouting.RebuildFittings(q);
        check("ĐHKK: 1 cút ở điểm đổi hướng", fits.Count(f => f.Kind == HvacFittingKind.Cut), 1, 0);
        var br = HvacRouting.AddDuct(q, s1, 4, 1, 4, -1.5, HvacDuctSystem.GioCap, 400, false, true);
        br.Path[0] = new MepPt { X = 4, Y = 1, Z = duct.Path[0].Z };
        fits = HvacRouting.RebuildFittings(q);
        check("ĐHKK: nhánh chạm thân ống chính → 1 tê", fits.Count(f => f.Kind == HvacFittingKind.Te), 1, 0);
        var rows = MepQuantities.Rows(q);
        var sheet = rows.Where(r => r.Group == "Ống gió" && r.Unit == "m²" && r.Item.StartsWith("Tôn")).Sum(r => r.Qty);
        check("ĐHKK: bảng vật tư – tổng m² tôn = Σ chu vi × L", sheet, Math.Round(duct.SheetArea, 2) + Math.Round(br.SheetArea, 2), 0.011);
        check("ĐHKK: HvacQuantities.SheetArea", HvacQuantities.SheetArea(q), duct.SheetArea + br.SheetArea, 1e-9);

        var dl = HvacRouting.AddUnit(q, s1, HvacUnitKind.DanLanh, 1, 4, 18000);
        var dn = HvacRouting.AddUnit(q, s1, HvacUnitKind.DanNong, 5, 4, 18000);
        var cu = HvacRouting.ConnectRefrigerant(q, dl, dn);
        check("ĐHKK: ống đồng theo công suất dàn lạnh Ø12,7", cu.GasDia, 12.7, 0);
        check("ĐHKK: chiều dài ống đồng = 4 ngang + 2 đứng", cu.Length, 6, 1e-9);
        check("ĐHKK: tổng m ống đồng", HvacQuantities.CopperLength(q), 6, 1e-9);
        rows = MepQuantities.Rows(q);
        check("ĐHKK: bảng vật tư – cặp ống đồng 6 m", rows.Where(r => r.Group == "Ống đồng" && r.Item.StartsWith("Cặp")).Sum(r => r.Qty), 6, 1e-9);
        var cond = HvacRouting.AddCondensate(q, dl, 1, 1);
        check("ĐHKK: nước ngưng dốc 1 %", HvacRouting.ActualSlope(cond), 0.01, 1e-9);
        check("ĐHKK: nước ngưng cuối thấp hơn đầu 3 cm", cond.Path[0].Z - cond.Path[^1].Z, 0.03, 1e-9);
        var steep = HvacRouting.AddCondensate(q, dl, 1, 2, 0.004);
        check("ĐHKK: độ dốc < 1 % nâng lên 1 %", steep.Slope, 0.01, 1e-12);

        var clashes = HvacClashCheck.Beams(q);
        check("ĐHKK: ống gió sát trần va dầm D22x30", B(clashes.Count(c => c.DuctId == duct.Id) >= 1), 1, 0);
        var cl = clashes.First(c => c.DuctId == duct.Id);
        check("ĐHKK: đỉnh ống cao hơn đáy dầm", B(cl.DuctTop > cl.BeamBottom), 1, 0);
        double lower = cl.BeamBottom - duct.H / 2000 - 0.01;
        for (int i = 0; i < duct.Path.Count; i++) duct.Path[i] = new MepPt { X = duct.Path[i].X, Y = duct.Path[i].Y, Z = lower };
        check("ĐHKK: hạ ống dưới đáy dầm → hết va chạm", HvacClashCheck.Beams(q).Count(c => c.DuctId == duct.Id), 0, 0);
        check("ĐHKK: cảnh báo VA_CHAM_ONG_GIO", B(HvacClashCheck.Warnings(q).All(x => x.Code == "VA_CHAM_ONG_GIO")), 1, 0);

        var figs = HvacDrawings.Figures(q);
        check("ĐHKK: 1 mặt bằng ĐHKK tầng", figs.Count, 1, 0);
        string dh = figs[0].W.ToDxfStringDec();
        check("ĐHKK: mặt bằng có tiêu đề + ký hiệu DL / DN + bảng ký hiệu",
            B(dh.Contains(HvacDrawings.TitlePrefix) && dh.Contains("DL") && dh.Contains("DN") && dh.Contains("KÝ HIỆU ĐIỀU HOÀ")), 1, 0);
        check("ĐHKK: gộp vào bản vẽ điện nước", B(MepDrawings.Figures(q).Any(f => f.Name.StartsWith(HvacDrawings.TitlePrefix))), 1, 0);
        check("ĐHKK: khối 3D ống gió / thiết bị", B(MepRegistration.Solids(q).Any(x => x.Category == "Ống gió") && MepRegistration.Solids(q).Any(x => x.Category == "Thiết bị điều hoà")), 1, 0);
        try { File.WriteAllText(Path.Combine(dir, "MB_DHKK.dxf"), figs[0].W.ToDxfString()); } catch (Exception) { }

        var opt = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var mb = JsonSerializer.Deserialize<MepModel>(JsonSerializer.Serialize(q.Mep, opt), opt);
        check("ĐHKK: lưu / mở giữ ống gió, thiết bị, ống", mb.Hvac.Ducts.Count * 100 + mb.Hvac.Units.Count * 10 + mb.Hvac.Pipes.Count, 2 * 100 + 2 * 10 + 3, 0);
        var old = JsonSerializer.Deserialize<MepModel>("{\"Devices\":[],\"Runs\":[]}", opt);
        check("ĐHKK: JSON cũ → mô hình ĐHKK rỗng, IsEmpty", B(old.Hvac != null && old.Hvac.IsEmpty && old.IsEmpty), 1, 0);

        // ================= 7. Nhập Revit ống gió / miệng gió / dàn lạnh =================
        var rv = House();
        string zst = rv.Stories.OrderBy(s => s.Elevation).First().Name;
        double ze = MepRouting.StoryElevation(rv, zst);
        var items = new List<RvMep>
        {
            new() { Id = "d1", Category = "Ducts", Name = "Rectangular Duct", SystemName = "Supply Air", X1 = 1, Y1 = 1, Z1 = ze + 2.9, X2 = 6, Y2 = 1, Z2 = ze + 2.9, W = 0.6, H = 0.3 },
            new() { Id = "d2", Category = "Ducts", Name = "Round Duct", SystemName = "Return Air", X1 = 6, Y1 = 1, Z1 = ze + 2.9, X2 = 6, Y2 = 4, Z2 = ze + 2.9, Diameter = 0.25 },
            new() { Id = "f1", Category = "DuctFitting", Name = "Elbow", X1 = 6, Y1 = 1, Z1 = ze + 2.9 },
            new() { Id = "t1", Category = "AirTerminal", Name = "Supply Diffuser 600x600", X1 = 3, Y1 = 1, Z1 = ze + 2.7, W = 0.6, D = 0.6 },
            new() { Id = "u1", Category = "MechanicalEquipment", Name = "Dàn lạnh cassette 24000 BTU", X1 = 2, Y1 = 3, Z1 = ze + 2.6 },
            new() { Id = "u2", Category = "MechanicalEquipment", Name = "Outdoor unit 24000BTU", X1 = 9.5, Y1 = 3, Z1 = ze + 0.3 },
            new() { Id = "p1", Category = "Pipes", Name = "PPR", SystemName = "Domestic Cold Water", X1 = 0.5, Y1 = 0.5, Z1 = ze + 0.3, X2 = 2.5, Y2 = 0.5, Z2 = ze + 0.3, Diameter = 0.025 },
        };
        MepRevitImport.Handle(rv, items, null, 0, 0);
        var hv = rv.Mep.Hvac;
        check("Revit: 2 ống gió thành đối tượng ĐHKK (không khối chung)", hv.Ducts.Count, 2, 0);
        var d1 = hv.Ducts.First(d => d.ExternalId == "d1");
        check("Revit: ống chữ nhật 600 × 300, gió cấp", B(!d1.Round && d1.W == 600 && d1.H == 300 && d1.System == HvacDuctSystem.GioCap), 1, 0);
        var d2 = hv.Ducts.First(d => d.ExternalId == "d2");
        check("Revit: ống tròn Ø250, gió hồi", B(d2.Round && d2.Dia == 250 && d2.System == HvacDuctSystem.GioHoi), 1, 0);
        check("Revit: m² tôn ống d1 = 2(0,6 + 0,3) × 5", d1.SheetArea, 9, 1e-9);
        check("Revit: 1 miệng gió cấp", hv.Terminals.Count(t => t.Supply), 1, 0);
        check("Revit: dàn lạnh 24000 BTU + dàn nóng", hv.Units.Count(u => u.Kind == HvacUnitKind.DanLanh && u.CapacityBtu == 24000) * 10 + hv.Units.Count(u => u.Kind == HvacUnitKind.DanNong), 11, 0);
        check("Revit: ống nước vẫn thành tuyến MEP", rv.Mep.Runs.Count, 1, 0);
        check("Revit: phụ kiện ống gió bỏ qua (tự sinh) – không khối chung", MepRevitImport.Last.Generic, 0, 0);
        check("Revit: côn tự sinh giữa 600 × 300 và Ø250", hv.Fittings.Count(f => f.Kind == HvacFittingKind.Con), 1, 0);
        MepRevitImport.Handle(rv, items, null, 0, 0);
        check("Revit: nhập lại không nhân đôi", rv.Mep.Hvac.Ducts.Count + rv.Mep.Hvac.Units.Count + rv.Mep.Hvac.Terminals.Count, 5, 0);

        // ================= 8. Kéo thiết bị điện nước trong 3D =================
        var dw = Wc();
        var oc2 = MepRouting.PlaceOnWall(dw, "Ổ cắm đơn", dw.Walls[0], 1.0, 1);
        check("Kéo 3D: dọc tường bắt 5 cm", MepDeviceDragMath.AlongWall(dw.Walls[0], 2.437, 0.6), 2.45, 1e-9);
        check("Kéo 3D: kẹp trong tường (nửa bề rộng)", MepDeviceDragMath.AlongWall(dw.Walls[0], 5.5, 0, 0.05, 0.05), 3.95, 1e-9);
        check("Kéo 3D: áp dụng đổi Offset thiết bị gắn tường", B(MepDeviceDragMath.Apply(dw, oc2, 3.012, 0.4)), 1, 0);
        check("Kéo 3D: Offset = 3,0", oc2.Offset, 3.0, 1e-9);
        check("Kéo 3D: vẫn trên tường 1", oc2.WallId, 1, 0);
        var free = MepRouting.PlaceFree(dw, "Thoát sàn (phễu thu)", "T2", 1, 1);
        MepDeviceDragMath.Apply(dw, free, 2.023, 1.488);
        check("Kéo 3D: thiết bị tự do bắt bước X", free.X, 2.0, 1e-9);
        check("Kéo 3D: thiết bị tự do bắt bước Y", free.Y, 1.5, 1e-9);
        check("Kéo 3D: không đổi → false", B(!MepDeviceDragMath.Apply(dw, free, 2.01, 1.49)), 1, 0);
    }
}
