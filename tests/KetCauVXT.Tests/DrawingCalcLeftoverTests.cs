using System.Text.Json;
using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Import;
using KetCauVXT.Mep;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Phần còn lại bản vẽ / tính toán: bậc trên móng bậc trên mặt bằng móng, mặt bằng định vị cọc + đường cong sức chịu tải cọc
/// chế độ thiết kế, móng bè / móng băng dọc mặt cắt, lớp hoàn thiện sàn theo từng phòng, mặt cắt dọc tuyến thoát (tụt = L·i),
/// độ dốc riêng theo hệ thống, sheet Excel "Vật tư điện nước", nhập ống / thiết bị Revit thành đối tượng điện nước.</summary>
public static class DrawingCalcLeftoverTests
{
    private static double B(bool x) => x ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "TrienKhai", "ConLai");
        Directory.CreateDirectory(dir);

        // ================= 1. Móng bậc: bậc trên + kích thước trên mặt bằng móng =================
        var st = new DetailSettings();
        var fStep = new ManualFooting { Name = "M1", N = 600, L = 2.0, B = 2.0, H = 0.6, Hb = 0.3, FootShape = "Bậc", StepL = 1.0, StepB = 1.0, R = 200, Df = 1.5 };
        var fFlat = new ManualFooting { Name = "M2", N = 600, L = 2.0, B = 2.0, H = 0.6, Hb = 0.6, R = 200, Df = 1.5 };
        var rStep = fStep.Run(st); var rFlat = fFlat.Run(st);
        var lvStep = new PlanLevel { Name = "Móng", Elevation = -1.5, IsFoundation = true, Items = { new PlanItem { Member = "M1", Kind = PlanKinds.Footing, X1 = 2, Y1 = 2, X2 = 2, Y2 = 2 } } };
        var lvFlat = new PlanLevel { Name = "Móng", Elevation = -1.5, IsFoundation = true, Items = { new PlanItem { Member = "M2", Kind = PlanKinds.Footing, X1 = 2, Y1 = 2, X2 = 2, Y2 = 2 } } };
        var map = new Dictionary<string, (IDetailMember M, DetailResult R)> { ["M1"] = (fStep, rStep), ["M2"] = (fFlat, rFlat) };
        var plan = new StructuralPlan();
        var wStep = StructuralPlanDrawing.Level(plan, lvStep, map, "MẶT BẰNG MÓNG BẬC", false);
        var wFlat = StructuralPlanDrawing.Level(plan, lvFlat, map, "MẶT BẰNG MÓNG PHẲNG", false);
        var tStep = wStep.Texts();
        check("Móng bậc: nguồn thiết kế giữ bậc trên 1000×1000", (rStep.Source as FootingDesign)?.StepL ?? 0, 1.0, 1e-9);
        check("Mặt bằng móng bậc: kích thước bậc trên 1000", B(tStep.Contains("1000")), 1, 0);
        check("Mặt bằng móng bậc: kích thước mép bậc → mép đế 500", B(tStep.Contains("500")), 1, 0);
        check("Mặt bằng móng bậc: nhiều nét hơn móng phẳng (đường bao bậc + 2 chuỗi kích thước)", B(wStep.EntityCount > wFlat.EntityCount + 4), 1, 0);
        wStep.Save(Path.Combine(dir, "MatBang_MongBac.dxf"));

        // ================= 2. Chế độ thiết kế: mặt bằng định vị cọc + đường cong sức chịu tải =================
        var p = ModelBuilder.NewProject("Còn lại – cọc");
        ModelBuilder.SetGrid(p, new[] { 4.0, 4.0 }, new[] { 5.0 });
        p.Foundation.Type = FoundationType.Pile;
        p.Foundation.PileLength = 12; p.Foundation.PileTipDepth = 0;
        p.Soil.Depth = 1.5;
        var ds = new DesignSummary();
        ds.PileCaps.Add(new PileCapDesign { Name = "ĐC1", X = 0, Y = 0, L = 2.0, B = 0.8, H = 0.8, Piles = new() { (-0.6, 0), (0.6, 0) }, PileSize = 0.4, PileCapacity = 600, PMax = 500, Depth = 1.5 });
        ds.PileCaps.Add(new PileCapDesign { Name = "ĐC2", X = 4, Y = 0, L = 2.0, B = 0.8, H = 0.8, Piles = new() { (-0.6, 0), (0.6, 0) }, PileSize = 0.4, PileCapacity = 600, PMax = 450, Depth = 1.5,
            Locations = new() { (4, 0), (8, 0) } });
        var rows = DesignPileDrawings.Rows(p, ds);
        check("Định vị cọc (thiết kế): 2 + 2×2 = 6 cọc", rows.Count, 6, 0);
        check("Định vị cọc: cọc đầu P1 tại x = −0,6", B(rows[0].No == "P1" && Math.Abs(rows[0].X + 0.6) < 1e-9), 1, 0);
        check("Định vị cọc: cọc cuối P6 tại x = 8,6", B(rows[^1].No == "P6" && Math.Abs(rows[^1].X - 8.6) < 1e-9), 1, 0);
        check("Định vị cọc: chiều dài cọc = PileLength 12 m", rows[0].Length, 12, 1e-9);
        check("Định vị cọc: cao độ mũi = −1,5 − 12", rows[0].TipElev, -13.5, 1e-9);
        var lp = DesignPileDrawings.LocationPlan(p, ds);
        string tLp = lp?.W.ToDxfStringDec() ?? "";
        check("Mặt bằng định vị cọc: có tiêu đề, bảng toạ độ, bảng thống kê, P6",
              B(tLp.Contains("MẶT BẰNG ĐỊNH VỊ CỌC") && tLp.Contains("BẢNG TOẠ ĐỘ CỌC") && tLp.Contains("BẢNG THỐNG KÊ CỌC") && tLp.Contains("P6")), 1, 0);
        check("Mặt bằng định vị cọc: không đài → null", B(DesignPileDrawings.LocationPlan(p, new DesignSummary()) == null), 1, 0);
        check("Đường cong sức chịu tải: không phương pháp TCVN 10304 → không vẽ", B(DesignPileDrawings.CapacityCurve(p) == null), 1, 0);
        var bh = new Borehole
        {
            Name = "HK1", WaterDepth = 5,
            Layers =
            {
                new SoilLayer { Name = "Đất đắp", Kind = SoilKind.Fill, Top = 0, Bottom = 2, Gamma = 17, N = 4, E = 3 },
                new SoilLayer { Name = "Sét pha", Kind = SoilKind.SiltyClay, Top = 2, Bottom = 18, Gamma = 19, GammaSat = 19.5, N = 12, E = 12 },
                new SoilLayer { Name = "Cát thô", Kind = SoilKind.Sand, Grain = SandGrain.Coarse, Top = 18, Bottom = 40, Gamma = 19, GammaSat = 20, N = 35, E = 40 }
            }
        };
        p.Boreholes.Add(bh);
        p.Foundation.PileBored = true; p.Foundation.BoredMethod = BoredPile.MethodTable; p.Foundation.PileSize = 0.8; p.Foundation.PileCapacity = 2000;
        var curve = DesignPileDrawings.Curve(p, bh, p.Foundation);
        check("Đường cong: bắt đầu L = 10 m (D ≥ 0,8)", curve.Count > 0 ? curve[0].Length : 0, 10, 1e-9);
        check("Đường cong: bước 1 m tới gần đáy hố khoan", B(curve.Count >= 10 && Math.Abs(curve[1].Length - curve[0].Length - 1) < 1e-9), 1, 0);
        check("Đường cong: sức chịu tải tăng theo chiều dài", B(curve.Count > 1 && curve[^1].Rcu > curve[0].Rcu), 1, 0);
        var figs = DesignPileDrawings.Figures(p, ds);
        check("Tờ KC-02.1: mặt bằng định vị + đường cong = 2 hình", figs.Count, 2, 0);
        check("Đường cong: tiêu đề có HỐ KHOAN HK1", B(figs.Count == 2 && figs[1].W.ToDxfStringDec().Contains("HỐ KHOAN HK1")), 1, 0);
        for (int k = 0; k < figs.Count; k++) figs[k].W.Save(Path.Combine(dir, $"ThietKe_Coc_{k + 1}.dxf"));

        // ================= 3. Móng bè / móng băng dọc mặt cắt =================
        var pf = ModelBuilder.NewProject("Còn lại – móng dọc mặt cắt");
        ModelBuilder.SetGrid(pf, new[] { 4.0, 4.0 }, new[] { 5.0 });
        Levels.ApplyWizard(pf, new LevelWizardOptions { Floors = 2, FirstFloorHeight = 3.6, FloorHeight = 3.3, FootingTopDepth = 0.9, FoundationDepth = 1.5 });
        var sX = new SectionLine { Name = "7-7", X1 = -1.5, Y1 = 0, X2 = 9.5, Y2 = 0 };
        var pr = pf.Clone();
        pr.Foundation.RaftThickness = 0.5; pr.Foundation.RaftOverhang = 0.5; pr.Foundation.RaftBeamB = 0.4; pr.Foundation.RaftBeamH = 1.0;
        var raft = SectionView.FoundationShapes(pr, sX);
        var slab = raft.FirstOrDefault(f => f.Kind == "Móng bè");
        check("Móng bè: 1 bản bè liên tục dọc mặt cắt", raft.Count(f => f.Kind == "Móng bè"), 1, 0);
        check("Móng bè: dài = 8 + 2×0,5 = 9 m", slab == null ? 0 : slab.Points.Max(q => q.U) - slab.Points.Min(q => q.U), 9.0, 1e-9);
        check("Móng bè: dày 0,5 m", slab == null ? 0 : slab.Points.Max(q => q.Z) - slab.Points.Min(q => q.Z), 0.5, 1e-9);
        check("Móng bè: không còn móng đơn", raft.Count(f => f.Kind == "Móng đơn"), 0, 0);
        var rib = raft.Where(f => f.Kind == "Sườn bè").ToList();
        check("Móng bè: sườn liên tục dọc hàng cột = 8 + 0,4", rib.Count == 1 ? rib[0].Points.Max(q => q.U) - rib[0].Points.Min(q => q.U) : 0, 8.4, 1e-9);
        check("Móng bè: lót bê tông liên tục", raft.Count(f => f.Kind == "Bê tông lót" && f.AlongCut), 1, 0);
        check("Móng bè: nhãn MÓNG BÈ dày 500, sườn 400×1000", B(slab?.Label == "MÓNG BÈ dày 500, sườn 400×1000"), 1, 0);
        var drawRaft = SectionView.Draw(pr, sX, 100, null, new SectionOptions { ShowFoundation = true });
        check("Mặt cắt móng bè: ghi nhãn MÓNG BÈ", B(drawRaft.Figure.W.ToDxfStringDec().Contains("MÓNG BÈ dày 500")), 1, 0);
        drawRaft.Figure.W.Save(Path.Combine(dir, "MatCat_MongBe.dxf"));

        var ps = pf.Clone();
        ps.Foundation.Type = FoundationType.Strip;
        var strip = SectionView.FoundationShapes(ps, sX);
        var band = strip.Where(f => !f.Beyond && f.Kind == "Móng băng").ToList();
        double ov = ps.Foundation.StripOverhang;
        check("Móng băng dọc mặt cắt: 1 dải liên tục (không còn chữ T rời)", band.Count, 1, 0);
        check("Móng băng dọc mặt cắt: đánh dấu AlongCut", B(band.Count == 1 && band[0].AlongCut), 1, 0);
        check("Móng băng dọc mặt cắt: dài ≥ 8 + 2·vươn", B(band.Count == 1 && band[0].Points.Max(q => q.U) - band[0].Points.Min(q => q.U) >= 8 + 2 * ov - 1e-9), 1, 0);
        check("Móng băng dọc mặt cắt: lót liên tục", strip.Count(f => f.Kind == "Bê tông lót" && f.AlongCut), 1, 0);
        var sY = new SectionLine { Name = "6-6", X1 = 4.0, Y1 = -1.5, X2 = 4.0, Y2 = 6.5 };
        var stripY = SectionView.FoundationShapes(ps, sY);
        check("Móng băng cắt ngang (2 cột cách 5 m): gộp thành dải dọc theo đường cắt", stripY.Count(f => !f.Beyond && f.Kind == "Móng băng"), 1, 0);
        SectionView.Draw(ps, sX, 100, null, new SectionOptions { ShowFoundation = true }).Figure.W.Save(Path.Combine(dir, "MatCat_MongBang_DocMatCat.dxf"));

        // ================= 4. Lớp hoàn thiện sàn theo từng phòng =================
        var pRoom = pf.Clone();
        pRoom.Arch ??= new ArchModel();
        string t2 = pRoom.Stories.OrderBy(q => q.Elevation).Skip(1).First().Name;
        pRoom.Arch.Rooms.Add(new Room { Id = 1, Story = t2, Name = "WC", Outline = new() { new[] { 0.0, 0.0 }, new[] { 4.0, 0.0 }, new[] { 4.0, 5.0 }, new[] { 0.0, 5.0 } } });
        pRoom.Arch.Rooms.Add(new Room { Id = 2, Story = t2, Name = "Phòng ngủ", Outline = new() { new[] { 4.0, 0.0 }, new[] { 8.0, 0.0 }, new[] { 8.0, 5.0 }, new[] { 4.0, 5.0 } } });
        var sR = new SectionLine { Name = "5-5", X1 = -1.5, Y1 = 2.5, X2 = 9.5, Y2 = 2.5 };
        var segs = SectionView.RoomFinishSegments(pRoom, sR, t2, 0, sR.Length);
        var wet = segs.Where(q => q.Layers.Any(l => l.M == SectionMaterial.ChongTham)).ToList();
        check("Hoàn thiện theo phòng: 2 đoạn (WC + phòng ngủ)", segs.Count, 2, 0);
        check("Hoàn thiện theo phòng: chỉ 1 đoạn có chống thấm", wet.Count, 1, 0);
        check("Chống thấm chỉ trong WC: u 1,5 → 5,5", wet.Count == 1 ? wet[0].U1 - wet[0].U0 : 0, 4.0, 1e-9);
        check("Chống thấm chỉ trong WC: bắt đầu u = 1,5", wet.Count == 1 ? wet[0].U0 : 0, 1.5, 1e-9);
        var dry = segs.FirstOrDefault(q => q.Rm.Name == "Phòng ngủ");
        check("Phòng ngủ: vữa 20 + gạch 10 (không chống thấm)", B(dry.Layers != null && dry.Layers.Count == 2 && dry.Layers.All(l => l.M != SectionMaterial.ChongTham)), 1, 0);
        check("Sàn gỗ theo FloorFinish", B(SectionView.RoomFinishLayers(new Room { Name = "Phòng khách", FloorFinish = "Sàn gỗ" }).Any(l => l.M == SectionMaterial.Go)), 1, 0);
        check("Không phòng → không lớp hoàn thiện", SectionView.RoomFinishSegments(pf, sR, t2, 0, sR.Length).Count, 0, 0);

        // ================= 5. Mặt cắt dọc tuyến thoát: tụt = L·i =================
        var pw = Wc();
        var wc = MepRouting.PlaceOnWall(pw, "Bồn cầu", pw.Walls[0], 1.0, 1);
        var lav = MepRouting.PlaceOnWall(pw, "Lavabo", pw.Walls[0], 2.5, 1);
        var runs = MepDrainage.ConnectToStack(pw, "T2");
        MepRouting.RebuildAll(pw);
        var rw = runs.First(r => r.FromId == wc.Id);
        var rl = runs.First(r => r.FromId == lav.Id);
        var chains = MepLongSection.Chains(pw);
        check("Mặt cắt dọc: 2 chuỗi tuyến thoát (mỗi thiết bị → ống đứng)", chains.Count, 2, 0);
        var chW = chains.First(c => c[0].Id == rw.Id);
        var sgW = MepLongSection.Segments(MepLongSection.Points(chW));
        double Lh = MepDrainage.HorizontalLength(rw);
        check("Mặt cắt dọc bồn cầu: tổng chiều dài ngang = L tuyến", sgW.Sum(q => q.Length), Lh, 1e-6);
        check("Mặt cắt dọc bồn cầu: tổng tụt đáy ống = L·2 %", sgW.Sum(q => q.Drop), 0.02 * Lh, 1e-6);
        check("Mặt cắt dọc bồn cầu: nhãn Ø110 – i=2%", B(sgW.All(q => q.Label.StartsWith("Ø110 – i=2% – L="))), 1, 0);
        var figW = MepLongSection.Build(pw, chW);
        string tW = figW?.W.ToDxfStringDec() ?? "";
        check("Mặt cắt dọc: tiêu đề MẶT CẮT DỌC TUYẾN THOÁT T2", B(tW.Contains("MẶT CẮT DỌC TUYẾN THOÁT T2")), 1, 0);
        check("Mặt cắt dọc: bảng độ dốc i=2%", B(tW.Contains("i=2%")), 1, 0);
        check("Mặt cắt dọc: bảng CAO ĐỘ ĐÁY ỐNG", B(tW.Contains("CAO ĐỘ ĐÁY ỐNG")), 1, 0);
        var chL = chains.First(c => c[0].Id == rl.Id);
        check("Mặt cắt dọc lavabo: tụt = L·3 %", MepLongSection.Segments(MepLongSection.Points(chL)).Sum(q => q.Drop), 0.03 * MepDrainage.HorizontalLength(rl), 1e-6);
        var mepFigs = MepDrawings.Figures(pw);
        check("Bộ bản vẽ điện nước có các mặt cắt dọc", mepFigs.Count(f => (f.Name ?? "").StartsWith(MepLongSection.TitlePrefix)), 2, 0);
        figW?.W.Save(Path.Combine(dir, "MatCatDoc_ThoatNuoc.dxf"));

        // ================= 6. Độ dốc riêng theo hệ thống =================
        var set = new MepSettings();
        check("Không luật riêng: thoát mưa Ø110 = 2 %", MepDrainage.DefaultSlope(set, 110, MepSystemKind.ThoatNuocMua), 0.02, 1e-12);
        set.SlopeRules.Add(new MepSlopeRule { System = MepSystemKind.ThoatNuocMua, Slope = 0.01 });
        set.SlopeRules.Add(new MepSlopeRule { System = MepSystemKind.ThoatNuocXam, DiaMax = 50, Slope = 0.025 });
        check("Luật riêng: thoát mưa mọi Ø = 1 %", MepDrainage.DefaultSlope(set, 110, MepSystemKind.ThoatNuocMua), 0.01, 1e-12);
        check("Luật riêng: thoát xám Ø42 ≤ 50 → 2,5 %", MepDrainage.DefaultSlope(set, 42, MepSystemKind.ThoatNuocXam), 0.025, 1e-12);
        check("Luật riêng: thoát xám Ø60 > 50 → mặc định 2 %", MepDrainage.DefaultSlope(set, 60, MepSystemKind.ThoatNuocXam), 0.02, 1e-12);
        check("Luật riêng: thoát thải không đổi (Ø42 → 3 %)", MepDrainage.DefaultSlope(set, 42, MepSystemKind.ThoatNuocThai), 0.03, 1e-12);
        pw.Mep.Settings.SlopeRules.Add(new MepSlopeRule { System = MepSystemKind.ThoatNuocThai, Slope = 0.01 });
        MepRouting.RebuildAll(pw);
        double LhW = MepDrainage.HorizontalLength(rw), LhL = MepDrainage.HorizontalLength(rl);
        check("Đi lại tuyến: thoát thải theo luật 1 % → tụt = 0,01·L", MepLongSection.Segments(MepLongSection.Points(new List<MepRun> { rw })).Sum(q => q.Drop), 0.01 * LhW, 1e-6);
        check("Đi lại tuyến: thoát xám (lavabo) vẫn 3 %", MepLongSection.Segments(MepLongSection.Points(new List<MepRun> { rl })).Sum(q => q.Drop), 0.03 * LhL, 1e-6);
        rw.Slope = 0.025; MepRouting.Route(pw, rw);
        check("Độ dốc nhập tay vẫn ưu tiên hơn luật hệ thống", MepLongSection.Segments(MepLongSection.Points(new List<MepRun> { rw })).Sum(q => q.Drop), 0.025 * MepDrainage.HorizontalLength(rw), 1e-6);
        rw.Slope = 0; MepRouting.RebuildAll(pw);

        // ================= 7. Sheet Excel "Vật tư điện nước" =================
        var mt = MepQuantities.Tables(pw);
        var mRows = MepQuantities.Rows(pw);
        check("Vật tư điện nước: bảng vật tư + bảng độ dốc tuyến thoát", mt.Count, 2, 0);
        check("Vật tư điện nước: số dòng = số mục thống kê", mt.Count > 0 ? mt[0].Rows.Count : -1, mRows.Count, 0);
        check("Vật tư điện nước: cột STT … Ghi chú (6 cột)", mt.Count > 0 ? mt[0].Headers.Length : 0, 6, 0);
        check("Bảng độ dốc: 1 dòng mỗi tuyến thoát", mt.Count > 1 ? mt[1].Rows.Count : 0, 2, 0);
        check("Bảng độ dốc: tuyến bồn cầu i = 1%", B(mt.Count > 1 && mt[1].Rows.Any(r => (string)r[0] == "T" + rw.Id && (string)r[4] == "1%")), 1, 0);
        check("Không điện nước → không bảng", MepQuantities.Tables(new Project()).Count, 0, 0);
        int nSheet = -1, nMepRows = -1;
        try
        {
            var sheets = QuantityReport.BuildSheets(pw, QuantityCalculator.Compute(pw, new DesignSummary()));
            var sh = sheets.FirstOrDefault(q => q.Name == MepQuantities.SheetName);
            nSheet = sheets.Count(q => q.Name == MepQuantities.SheetName);
            nMepRows = sh?.Tables.FirstOrDefault()?.Rows.Count ?? -1;
        }
        catch (Exception) { }
        check("Excel khối lượng: có sheet \"Vật tư điện nước\"", nSheet, 1, 0);
        check("Excel khối lượng: sheet điện nước đủ dòng vật tư", nMepRows, mRows.Count, 0);

        // ================= 8. Nhập ống / thiết bị Revit (JSON) thành đối tượng điện nước =================
        var ex = new RevitExchange
        {
            ProjectName = "Revit MEP",
            Levels = new() { new RvLevel { Name = "L1", Elevation = 0 }, new RvLevel { Name = "L2", Elevation = 3.3 } },
            Grids = new() { new RvGrid { Name = "1", X1 = 10, Y1 = 20, X2 = 10, Y2 = 30 }, new RvGrid { Name = "2", X1 = 14, Y1 = 20, X2 = 14, Y2 = 30 },
                            new RvGrid { Name = "A", X1 = 5, Y1 = 20, X2 = 20, Y2 = 20 }, new RvGrid { Name = "B", X1 = 5, Y1 = 25, X2 = 20, Y2 = 25 } },
            Columns = new()
        };
        foreach (var (cx, cy) in new[] { (10.0, 20.0), (14.0, 20.0), (14.0, 25.0), (10.0, 25.0) })
            ex.Columns.Add(new RvColumn { Id = $"c{cx}{cy}", X = cx, Y = cy, BaseLevel = "L1", TopLevel = "L2", B = 0.22, H = 0.22 });
        ex.Walls = new()
        {
            new RvWall { Id = "w1", Level = "L1", X1 = 10, Y1 = 20, X2 = 14, Y2 = 20, Height = 3.0, TypeName = "Tường 220 Revit" },
            new RvWall { Id = "w2", Level = "L1", X1 = 14, Y1 = 20, X2 = 14, Y2 = 25, Height = 3.0, TypeName = "Tường 220 Revit" },
            new RvWall { Id = "w3", Level = "L1", X1 = 14, Y1 = 25, X2 = 10, Y2 = 25, Height = 3.0, TypeName = "Tường 220 Revit" },
            new RvWall { Id = "w4", Level = "L1", X1 = 10, Y1 = 25, X2 = 10, Y2 = 20, Height = 3.0, TypeName = "Tường 220 Revit" }
        };
        ex.Mep = new()
        {
            new RvMep { Id = "m1", Category = "Pipe", Name = "Ống PPR D25", SystemName = "Domestic Cold Water", Level = "L1", X1 = 10.5, Y1 = 21, Z1 = 0.3, X2 = 13.5, Y2 = 21, Z2 = 0.3, Diameter = 0.025 },
            new RvMep { Id = "m3", Category = "Pipe", Name = "Ống PPR D25", SystemName = "Domestic Cold Water", Level = "L1", X1 = 13.5, Y1 = 21, Z1 = 0.3, X2 = 13.5, Y2 = 23, Z2 = 0.3, Diameter = 0.025 },
            new RvMep { Id = "m4", Category = "Pipe", Name = "uPVC D110", SystemName = "Sanitary", Level = "L1", X1 = 11, Y1 = 22, Z1 = 0.25, X2 = 12, Y2 = 22, Z2 = 0.23, Diameter = 0.11 },
            new RvMep { Id = "m2", Category = "PlumbingFixture", Name = "Lavabo", Level = "L1", X1 = 12, Y1 = 24, Z1 = 0.8, W = 0.5, D = 0.4, H = 0.2 },
            new RvMep { Id = "m5", Category = "ElectricalFixture", Name = "Duplex Receptacle", Level = "L1", X1 = 10.2, Y1 = 22, Z1 = 0.4, W = 0.1, D = 0.05, H = 0.1 },
            new RvMep { Id = "m6", Category = "PipeFitting", Name = "Elbow 90", Level = "L1", X1 = 13.5, Y1 = 21, Z1 = 0.3, W = 0.05, D = 0.05, H = 0.05 },
            new RvMep { Id = "m7", Category = "Duct", Name = "Ống gió 300x200", Level = "L1", X1 = 11, Y1 = 23, Z1 = 2.8, X2 = 13, Y2 = 23, Z2 = 2.8, Diameter = 0.3 }
        };
        string json = Path.Combine(dir, "RevitMEP_DauVao.json");
        File.WriteAllText(json, JsonSerializer.Serialize(ex, new JsonSerializerOptions { WriteIndented = true }));
        var exJ = RevitJsonImporter.ReadFile(json);
        bool prev = RevitArchImport.UseMepHandler;
        try
        {
            RevitArchImport.UseMepHandler = true;
            var pm = ModelBuilder.NewProject("Revit MEP");
            var rep = RevitJsonImporter.Build(pm, exJ);
            var mm = pm.Mep;
            var last = MepRevitImport.Last;
            check("Revit MEP: bộ môn điện nước đăng ký bộ nhập", B(RevitArchImport.MepHandler != null), 1, 0);
            check("Revit MEP: 3 tuyến ống (2 cấp nước + 1 thoát)", mm.Runs.Count(r => r.ExternalId != null), 3, 0);
            check("Revit MEP: 2 thiết bị theo thư viện (lavabo, ổ cắm đôi)", last.Devices, 2, 0);
            check("Revit MEP: đầu ống trùng nhau dùng chung điểm nối → 5 điểm nối", mm.Devices.Count(d => d.Family == MepRevitImport.NodeFamily), 5, 0);
            check("Revit MEP: tổng 7 thiết bị", mm.Devices.Count, 7, 0);
            check("Revit MEP: phụ kiện ống bỏ qua (tự sinh)", last.Skipped, 1, 0);
            check("Revit MEP: ống gió → khối chung bộ môn điện nước", pm.Arch.Furniture.Count(f => f.Discipline == Discipline.DienNuoc && f.ExternalId == "m7"), 1, 0);
            check("Revit MEP: không còn khối chung cho ống / thiết bị đã nhận", pm.Arch.Furniture.Count(f => f.Discipline == Discipline.DienNuoc && f.ExternalId != "m7"), 0, 0);
            var lavD = mm.Devices.FirstOrDefault(d => d.ExternalId == "m2");
            check("Revit MEP: lavabo → họ \"Lavabo\"", B(lavD?.Family == "Lavabo"), 1, 0);
            check("Revit MEP: lavabo cao 0,8 so với sàn", lavD?.Height ?? -1, 0.8, 1e-9);
            var soc = mm.Devices.FirstOrDefault(d => d.ExternalId == "m5");
            check("Revit MEP: Duplex Receptacle → Ổ cắm đôi, gắn tường gần (≤ 0,3 m)", B(soc?.Family == "Ổ cắm đôi" && soc.WallId > 0), 1, 0);
            var r1 = mm.Runs.FirstOrDefault(r => r.ExternalId == "m1");
            check("Revit MEP: ống m1 cấp nước lạnh Ø25 (dời gốc x = 0,5)", B(r1 != null && r1.System == MepSystemKind.CapNuocLanh && r1.Size == 25 && Math.Abs(r1.Path[0].X - 0.5) < 1e-9), 1, 0);
            check("Revit MEP: giữ nguyên hình học sau khi đi lại tuyến", r1?.Length ?? 0, 3.0, 1e-9);
            var r4 = mm.Runs.FirstOrDefault(r => r.ExternalId == "m4");
            check("Revit MEP: ống Sanitary → thoát nước thải Ø110", B(r4 != null && r4.System == MepSystemKind.ThoatNuocThai && r4.Size == 110), 1, 0);
            check("Revit MEP: độ dốc thoát lấy từ Revit (0,02/1 = 2 %)", r4?.Slope ?? 0, 0.02, 1e-9);
            var r3 = mm.Runs.FirstOrDefault(r => r.ExternalId == "m3");
            check("Revit MEP: m1 → m3 nối tiếp tại cùng một điểm nối", B(r1 != null && r3 != null && r1.ToId == r3.FromId), 1, 0);
            check("Revit MEP: báo cáo ghi phần điện nước", B(rep.Info.Any(i => i.StartsWith("Điện nước từ Revit (đối tượng MEP)"))), 1, 0);
            exJ.Mep.RemoveAll(q => q.Id == "m3");
            var up = RevitArchImport.Update(pm, exJ);
            check("Cập nhật Revit MEP: xoá tuyến m3", mm.Runs.Count(r => r.ExternalId != null), 2, 0);
            check("Cập nhật Revit MEP: điểm nối mồ côi bị xoá (còn 4)", mm.Devices.Count(d => d.Family == MepRevitImport.NodeFamily), 4, 0);
            check("Cập nhật Revit MEP: không thêm trùng", up.Added, 0, 0);
            MepDrawings.Figures(pm).FirstOrDefault()?.W.Save(Path.Combine(dir, "RevitMEP_MatBang.dxf"));
        }
        finally { RevitArchImport.UseMepHandler = prev; }
        var pOld = ModelBuilder.NewProject("Revit cũ");
        RevitJsonImporter.Build(pOld, RevitJsonImporter.ReadFile(json));
        check("Mặc định (không bật): vẫn nhập khối chung như cũ", pOld.Arch.Furniture.Count(f => f.Discipline == Discipline.DienNuoc), 7, 0);
        check("Mặc định: không tạo thiết bị điện nước", pOld.Mep.Devices.Count, 0, 0);

        // ================= 9. B22,5 trong danh sách bê tông chế độ thiết kế =================
        check("Danh sách bê tông thiết kế có B22.5", B(Material.ConcreteClasses.Contains("B22.5")), 1, 0);
        check("B22.5: Rb = 13,0 MPa (nội suy B20–B25)", Material.Concrete("B22.5").Rb / 1000.0, 13.0, 1e-6);
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
}
