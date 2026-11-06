using KetCauVXT.Design;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Export;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Test 76 – Hoàn thiện bản vẽ / xuất: cửa gắn tường trên mặt bằng DXF (cắt tường, cung cánh cửa, thẻ D1/S1) + bảng thống kê cửa,
/// lanh tô tự sinh từ cửa của mô hình (gộp LT, số lượng), kích thước cột lệch tâm + điểm chèn ETABS / SAP2000,
/// "XEM TIẾP ĐOẠN … – TỜ KC-xx" khi chia dầm, đánh dấu điểm cắt sai trên đoạn, chia đoạn dầm chế độ Thiết kế, danh mục mặt cắt.</summary>
public static class DrawExportFinishTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "TrienKhai", "HoanThienXuat");
        Directory.CreateDirectory(dir);

        // ================= 1. Mô hình có tường + cửa gắn tường =================
        var p = ModelBuilder.NewProject("Kiểm thử cửa gắn tường");
        ModelBuilder.SetGrid(p, new[] { 3.6, 4.2 }, new[] { 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        WallAssembly.Upgrade(p);
        var floors = p.Stories.OrderBy(s => s.Elevation).ToList();
        var st = floors.First(s => p.Frames.Any(f => f.Kind == FrameKind.Beam && f.Story == s.Name));
        var st2 = floors.Last(s => s.Name != st.Name && p.Frames.Any(f => f.Kind == FrameKind.Beam && f.Story == s.Name));
        var w1 = new WallLine { Id = 101, Story = st.Name, X1 = 0, Y1 = 0, X2 = 7.8, Y2 = 0, Type = "Gạch đặc 220 trát 2 mặt" };
        var w2 = new WallLine { Id = 102, Story = st.Name, X1 = 3.6, Y1 = 0, X2 = 3.6, Y2 = 4.5, Type = "Gạch đặc 110 trát 2 mặt" };
        var w3 = new WallLine { Id = 103, Story = st2.Name, X1 = 0, Y1 = 0, X2 = 7.8, Y2 = 0, Type = "Gạch đặc 220 trát 2 mặt" };
        p.Walls.AddRange(new[] { w1, w2, w3 });
        check("Đặt D1 trên tường 220", B(WallAssembly.PlaceOpening(p, w1, "D1 900x2200", 1.8).Ok), 1, 0);
        check("Đặt S1 trên tường 220", B(WallAssembly.PlaceOpening(p, w1, "S1 1200x1400", 5.7).Ok), 1, 0);
        check("Đặt D2 trên tường 110", B(WallAssembly.PlaceOpening(p, w2, "D2 800x2200", 2.0, flip: true).Ok), 1, 0);
        check("Đặt D1 trên tường 220 tầng trên", B(WallAssembly.PlaceOpening(p, w3, "D1 900x2200", 1.8).Ok), 1, 0);
        check("Mặt bằng có cửa gắn tường → vẽ tường", B(WallPlanDrawing.Enabled(p)), 1, 0);

        // ---- mặt bằng DXF: tường cắt tại cửa, cung cánh cửa, thẻ ----
        var wp = new DxfWriter(100);
        int nd = WallPlanDrawing.Draw(wp, p, st.Name);
        check("Vẽ 2 cửa đi (D1, D2) trên mặt bằng tầng " + st.Name, nd, 2, 0);
        var prim = wp.GetPrimitives();
        check("Mỗi cửa đi 1 cánh có 1 cung mở (2 cung)", prim.Count(q => q.Kind == PrimKind.Arc), 2, 0);
        // tường 220 dài 7,8 m có 2 ô cắt (D1, S1) → 3 đoạn đặc; tường 110 có 1 ô cắt → 2 đoạn
        check("Tường 220 cắt thành 3 đoạn đặc (D1, S1)", WallAssembly.SolidSegments(p, w1).Count, 3, 0);
        var fp = FramingPlan.Build(p, null, st, 100);
        var txt = fp.Texts();
        check("Mặt bằng kết cấu: có thẻ D1, D2, S1", B(txt.Contains("D1") && txt.Contains("D2") && txt.Contains("S1")), 1, 0);
        check("Mặt bằng kết cấu: có cung cánh cửa (ARC)", B(fp.GetPrimitives().Count(q => q.Kind == PrimKind.Arc) >= 2), 1, 0);
        string dxfPlan = Path.Combine(dir, "MatBang_CuaGanTuong.dxf");
        File.WriteAllText(dxfPlan, fp.ToDxfString());
        check("Ghi DXF mặt bằng có cửa", B(File.Exists(dxfPlan) && new FileInfo(dxfPlan).Length > 1000), 1, 0);

        // ---- bảng thống kê cửa ----
        var rows = WallPlanDrawing.Schedule(p);
        check("Thống kê cửa: 3 kiểu (D1, D2, S1)", rows.Count, 3, 0);
        var rD1 = rows.First(r => r.Type == "D1 900x2200");
        check("D1: tổng 2 cửa (2 tầng)", rD1.Total, 2, 0);
        check("D1: 1 cửa tầng " + st.Name, rD1.PerStory.TryGetValue(st.Name, out int n1) ? n1 : 0, 1, 0);
        check("Thứ tự: cửa đi trước cửa sổ", B(rows.Last().Type.StartsWith("S1")), 1, 0);
        var sf = WallPlanDrawing.ScheduleFigure(p);
        check("Hình BẢNG THỐNG KÊ CỬA", B(sf != null && sf.W.Texts().Contains(WallPlanDrawing.ScheduleTitle) && sf.W.Texts().Contains("TỔNG CỘNG")), 1, 0);
        var items = DrawingSet.BuildDrawings(p, new DesignSummary(), k => k.Contains("KC-09"));
        var door = items.FirstOrDefault(i => i.FileName == "KC-09_ThongKeCua.dxf");
        check("Bộ bản vẽ thiết kế có tờ KC-09 thống kê cửa", B(door?.W != null && door.W.Texts().Contains(WallPlanDrawing.ScheduleTitle)), 1, 0);
        var qr = KetCauVXT.Report.QuantityReport.BuildSheets(p, KetCauVXT.Report.QuantityCalculator.Compute(p, new DesignSummary()));
        check("Bảng khối lượng (Excel/Word): có bảng thống kê cửa", B(qr.SelectMany(s => s.Tables).Any(t => t.Title.StartsWith("Thống kê cửa"))), 1, 0);

        // ================= 2. Lanh tô từ cửa của mô hình =================
        var dp = new DetailingProject { ProjectName = "Lanh tô từ mô hình" };
        dp.Plan = new StructuralPlan { SpansX = "3.6 4.2", SpansY = "4.5" };
        dp.Plan.Levels.Clear();
        int ltBefore = dp.Lintels.Count;
        var log = ModelToDetailing.LintelsFromModel(p, dp);
        check("Lanh tô: 3 cấu kiện mới (D1 tường 220, D2 tường 110, S1)", dp.Lintels.Count - ltBefore, 3, 0);
        check("Lanh tô: tạo mặt bằng cho 2 tầng có cửa", dp.Plan.Levels.Count, 2, 0);
        var ltD1 = dp.Lintels.First(l => Math.Abs(l.Lo - 0.9) < 1e-6 && l.Bw > 0.2);
        check("LT của D1: Lo = 0,90, b = 220", ltD1.Bw * 1000, 220, 1e-6);
        check("LT của D1: số lượng 2 (2 tầng)", ltD1.Quantity, 2, 0);
        var ltD2 = dp.Lintels.First(l => Math.Abs(l.Lo - 0.8) < 1e-6);
        check("LT của D2 trên tường 110: b = 110", ltD2.Bw * 1000, 110, 1e-6);
        var lv1 = ModelToDetailing.LevelFor(dp.Plan, st, false);
        var itD1 = lv1.Items.First(i => i.Kind == PlanKinds.Lintel && i.Member == ltD1.Name);
        check("Vị trí LT D1 trên mặt bằng: tâm X = 1,80", (itD1.X1 + itD1.X2) / 2, 1.8, 1e-6);
        check("Chiều dài vị trí LT D1 = bề rộng ô cửa 0,90", itD1.Length, 0.9, 1e-6);
        int again = dp.Lintels.Count;
        ModelToDetailing.LintelsFromModel(p, dp);
        check("Chạy lại không nhân đôi lanh tô", dp.Lintels.Count + lv1.Items.Count(i => i.Kind == PlanKinds.Lintel) * 1000, again + 3 * 1000, 0);
        var resLt = dp.RunAll();
        var rD2 = resLt.First(t => ReferenceEquals(t.Member, ltD2)).Result;
        check("LT tường 110 tính có gợi ý / kiểm tra (không ném lỗi)", B(rD2 != null && rD2.Checks.Count > 0), 1, 0);
        check("Nhật ký lanh tô có tên tầng", B(log.Any(l => l.StartsWith(st.Name))), 1, 0);
        var lvU = new PlanLevel { Name = "KT" };
        check("Ánh xạ từ \"Mở mặt bằng CAD\": gắn file DXF + dời trục", B(ModelToDetailing.AttachUnderlayFromPick(lvU, dp.Plan, Path.Combine(dir, "MB_KT_DauVao.dxf"), 12.5, -3, dir)
                                                                      && Math.Abs(lvU.UnderlayDx + 12.5) < 1e-9 && Math.Abs(lvU.UnderlayDy - 3) < 1e-9 && lvU.UnderlayRel == "MB_KT_DauVao.dxf"), 1, 0);
        check("Ánh xạ: không ghi đè ánh xạ đã có", B(!ModelToDetailing.AttachUnderlayFromPick(lvU, dp.Plan, Path.Combine(dir, "Khac_DauVao.dxf"), 0, 0, dir)), 1, 0);
        check("Ánh xạ: bỏ qua file không phải DXF/DWG", B(!ModelToDetailing.AttachUnderlayFromPick(new PlanLevel(), dp.Plan, "anh.png", 0, 0, dir)), 1, 0);

        // ================= 3. Cột lệch tâm: kích thước + điểm chèn ETABS / SAP2000 =================
        var col = p.Frames.First(f => f.IsVerticalKind && Math.Abs(Math.Max(p.Node(f.NodeI).Z, p.Node(f.NodeJ).Z) - st.Elevation) < 1e-3);
        var si = DrawUtil.SectionOf(p, col.Section);
        col.ColOffsetX = si.H / 2;
        check("Điểm chèn: lệch +H/2 theo X → 4 (giữa trái)", ColumnInsertion.Cardinal(p, col).Cardinal, 4, 0);
        col.ColOffsetY = si.B / 2;
        check("Điểm chèn: lệch +H/2, +B/2 → 1 (dưới trái)", ColumnInsertion.Cardinal(p, col).Cardinal, 1, 0);
        col.ColOffsetX = -si.H / 2; col.ColOffsetY = -si.B / 2;
        check("Điểm chèn: lệch −H/2, −B/2 → 9 (trên phải)", ColumnInsertion.Cardinal(p, col).Cardinal, 9, 0);
        col.ColOffsetX = 0.05; col.ColOffsetY = 0;
        check("Điểm chèn: lệch 50 mm (không bằng nửa cạnh) → không biểu diễn được", B(!ColumnInsertion.Cardinal(p, col).Exact), 1, 0);
        col.ColOffsetX = si.H / 2;
        check("Cột không lệch → 10", ColumnInsertion.Cardinal(p, p.Frames.First(f => f.IsVerticalKind && f.Id != col.Id && Math.Abs(f.ColOffsetX) < 1e-9 && Math.Abs(f.ColOffsetY) < 1e-9)).Cardinal, 10, 0);
        var wpe = new DxfWriter(100);
        check("Mặt bằng: kích thước trục → mép cột lệch tâm", FramingPlan.EccentricDims(wpe, p, st), 1, 0);
        check("Kích thước lệch tâm ghi cạnh cột H", B(wpe.Texts().Contains(DxfWriter.N0(si.H * 1000))), 1, 0);
        string e2k = Path.Combine(dir, "LechTam.e2k");
        E2kExporter.Export(p, e2k);
        string nm = E2kExporter.LineName(col, p);
        var lines = File.ReadAllLines(e2k);
        check("E2K: cột lệch tâm có CARDINALPT 4", B(lines.Any(l => l.Contains("LINEASSIGN  \"" + nm + "\"") && l.Contains("CARDINALPT 4 "))), 1, 0);
        check("E2K: cột thường giữ CARDINALPT 10", B(lines.Any(l => l.Contains("LINEASSIGN") && l.Contains("CARDINALPT 10 "))), 1, 0);
        string s2k = Path.Combine(dir, "LechTam.s2k");
        S2kExporter.Export(p, s2k);
        string s2 = File.ReadAllText(s2k);
        string xi = "XI=" + (si.H / 2).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        check("S2K: bảng FRAME INSERTION POINT ASSIGNMENTS với độ dời XI = H/2", B(s2.Contains("FRAME INSERTION POINT ASSIGNMENTS") && s2.Contains(xi)), 1, 0);
        col.ColOffsetX = 0.05;
        E2kExporter.Export(p, e2k);
        check("E2K: lệch không biểu diễn được → ghi chú $ và giữ 10", B(File.ReadAllLines(e2k).Any(l => l.StartsWith("$ COLUMN") && l.Contains("OFFSET X=0.05"))), 1, 0);
        col.ColOffsetX = 0;

        // ================= 4. Chia dầm: tham chiếu tờ, điểm cắt sai trên đoạn, chế độ Thiết kế =================
        var beam = new ManualBeam { Name = "D5", Spans = "5*4,5" };
        var bp = new DetailingProject { ProjectName = "Thử tham chiếu tờ" };
        bp.Beams.Add(beam);
        var res = bp.RunAll();
        var d = (BeamLineDesign)res.First(t => t.Member == beam).Result.Source;
        var set = new DetailSheetSet { AutoPaper = false, Paper = "A3" };
        var cat = SheetLayoutEngine.Catalog(bp, res, null, set);
        var segs = cat.Where(f => f.Member == "D5" && f.Segment > 0).OrderBy(f => f.Segment).ToList();
        check("Dầm 5 nhịp trên A3: chia ≥ 2 đoạn", B(segs.Count >= 2), 1, 0);
        SheetLayoutEngine.AutoLayout(set, cat);
        var map = cat.ToDictionary(f => f.Id);
        string no2 = SheetLayoutEngine.SheetOfSegment(set, map, segs[0], 2);
        string no1 = SheetLayoutEngine.SheetOfSegment(set, map, segs[1], 1);
        check("Biết tờ chứa đoạn 2", B(!string.IsNullOrEmpty(no2) && no2.StartsWith("KC")), 1, 0);
        var pages = SheetLayoutEngine.Pages(bp, set, cat);
        var allTxt = pages.SelectMany(pg => pg.W.Texts()).ToList();
        string want = "XEM TIẾP ĐOẠN 2/" + segs.Count + SheetLayoutEngine.SheetRefSep + no2;
        check("Ghi \"" + want + "\"", B(allTxt.Contains(want)), 1, 0);
        check("Ghi \"TIẾP THEO ĐOẠN 1/… – TỜ " + no1 + "\"", B(allTxt.Contains("TIẾP THEO ĐOẠN 1/" + segs.Count + SheetLayoutEngine.SheetRefSep + no1)), 1, 0);
        SheetLayoutEngine.Pages(bp, set, cat);
        check("Ghép lại lần 2 không lặp tham chiếu tờ", B(SheetLayoutEngine.Pages(bp, set, cat).SelectMany(pg => pg.W.Texts()).Count(t => t.Contains(SheetLayoutEngine.SheetRefSep + no2 + SheetLayoutEngine.SheetRefSep)) == 0), 1, 0);
        // danh mục mặt cắt (hồ sơ)
        check("Hồ sơ: có hình danh mục mặt cắt dầm D5", B(cat.Any(f => f.Member == "D5" && f.F.Name == BeamDrawing.CatalogTitle + " D5")), 1, 0);
        check("Danh mục mặt cắt đã được dàn trang", SheetLayoutEngine.Unplaced(set, cat).Count, 0, 0);
        var proj = DetailShim.Project(bp.Settings);
        var crow = BeamDrawing.Catalog(proj, d);
        check("Danh mục: mỗi số mặt cắt 1 dòng", crow.Count, BeamDrawing.SectionCuts(d).Select(c => c.Num).Distinct().Count(), 0);
        check("Danh mục: đủ 15 vị trí (5 nhịp × 2 gối + giữa nhịp)", crow.Sum(r => r.Places.Count), 15, 0);
        check("Danh mục: b×h = 220×400", B(crow.All(r => r.Size == "220×400")), 1, 0);
        check("Danh mục: có thép trên, dưới, đai", B(crow.All(r => r.Top.Contains("Ø") && r.Bottom.Contains("Ø") && r.Stirrup.StartsWith("Ø"))), 1, 0);
        var setNo = new DetailSheetSet { AutoPaper = false, Paper = "A3", SectionCatalog = false };
        check("Tắt danh mục mặt cắt → không có hình", B(!SheetLayoutEngine.Catalog(bp, res, null, setNo).Any(f => f.F.Name.StartsWith(BeamDrawing.CatalogTitle))), 1, 0);

        // điểm nối sai trên đoạn (thép dưới nối giữa nhịp 3 – 1/3 giữa nhịp)
        var bad = new ManualBeam { Name = "D6", Spans = "5*4,5", BotSplices = "11,25" };
        var bp2 = new DetailingProject { ProjectName = "Điểm nối sai" };
        bp2.Beams.Add(bad);
        var res2 = bp2.RunAll();
        var d6 = (BeamLineDesign)res2.First(t => t.Member == bad).Result.Source;
        var brk = BeamDrawing.EvenBreaks(5, 2);
        var segF = BeamDrawing.BuildSegments(DetailShim.Project(bp2.Settings), d6, brk);
        int marks = SheetLayoutEngine.SegmentCutMarks(bad, bp2.Settings, d6, brk, segF);
        check("Đoạn dầm: có dấu điểm nối sai", B(marks >= 1), 1, 0);
        check("Dấu nối sai nằm trên đoạn chứa nhịp 3", B(segF.Any(f => f.W.Texts().Contains("!"))), 1, 0);
        check("Dầm đúng: không dấu", SheetLayoutEngine.SegmentCutMarks(beam, bp.Settings, d, brk, BeamDrawing.BuildSegments(proj, d, brk)), 0, 0);

        // chế độ Thiết kế: chia đoạn theo bề rộng khổ giấy
        var dfigs = BeamDrawing.BuildAllSplit(proj, new[] { d }, null, 300);
        var elev = dfigs.Where(f => (f.Name ?? "").Contains("ĐOẠN")).ToList();
        check("Thiết kế: dầm dài chia ≥ 2 đoạn", B(elev.Count >= 2), 1, 0);
        check("Thiết kế: đoạn vừa bề rộng 300 mm giấy", B(elev.All(f => f.W.Extents.W / f.W.Scale <= 300 + 1e-6)), 1, 0);
        check("Thiết kế: mặt cắt giữ hình riêng", B(dfigs.Any(f => (f.Name ?? "").StartsWith("D5 "))), 1, 0);
        check("Thiết kế: dầm ngắn không chia", BeamDrawing.BuildAllSplit(proj, new[] { d }, null, 5000).Count(f => (f.Name ?? "").Contains("ĐOẠN")), 0, 0);
        BeamDrawing.DesignSectionCatalog = true;
        try { check("Thiết kế: bật danh mục mặt cắt", B(BeamDrawing.BuildAllSplit(proj, new[] { d }, null, 0).Any(f => f.Name == BeamDrawing.CatalogTitle + " D5")), 1, 0); }
        finally { BeamDrawing.DesignSectionCatalog = false; }

        var files = SheetLayoutEngine.ExportCad(bp, set, cat, dir, CadOutputFormat.DxfR12);
        check("Xuất DXF các tờ (tham chiếu tờ, danh mục mặt cắt)", files.Count(File.Exists), set.Sheets.Count, 0);
    }
}
