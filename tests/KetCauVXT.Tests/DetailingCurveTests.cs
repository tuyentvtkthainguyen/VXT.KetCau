using KetCauVXT.Detailing;
using KetCauVXT.Import;

namespace KetCauVXT.Tests;

/// <summary>[Triển khai] Nhận diện layer cột / dầm, dầm cong từ cung tròn (ARC, cung lồi đa tuyến) của AutoCAD, tạo sàn từ dầm,
/// sao chép mặt bằng sang tầng có sẵn, xuất dầm cong thành ARC trên layer bê tông.</summary>
public static class DetailingCurveTests
{
    private static double B(bool x) => x ? 1 : 0;

    public static void Run(Action<string, double, double, double> check)
    {
        var cul = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;   // số trong JSON thử
        try { RunCore(check); } finally { System.Threading.Thread.CurrentThread.CurrentCulture = cul; }
    }

    private static void RunCore(Action<string, double, double, double> check)
    {
        // ---- hình học cung ----
        double s = PlanArc.SagittaFrom3(0, 0, 4, 0, 2, 1);
        check("Dầm cong: độ vồng qua 3 điểm (0,0)-(4,0)-(2,1) = +1", s, 1, 1e-9);
        var g = PlanArc.Geom(0, 0, 4, 0, 1).Value;
        check("Dầm cong: R = 2,5 m", g.R, 2.5, 1e-9);
        check("Dầm cong: chiều dài cung", PlanArc.Length(new PlanItem { X1 = 0, Y1 = 0, X2 = 4, Y2 = 0, Bulge = 1 }), 2.5 * 2 * Math.Asin(2 / 2.5), 1e-9);
        var fa = PlanArc.FromArc(0, 0, 3, 0, Math.PI / 2);
        check("Dầm cong: cung tâm (0,0) R3 90° → độ vồng 3·(1−cos45°)", Math.Abs(fa.Bulge), 3 * (1 - Math.Cos(Math.PI / 4)), 1e-9);

        // ---- dữ liệu VXT_MB_KC: lưới 2 nhịp 4 m × 5 m, cột, dầm 2 nét, dầm cong (ARC đôi + đa tuyến cung lồi) ----
        string L(string layer, double x1, double y1, double x2, double y2) => $"{{\"t\":\"line\",\"layer\":\"{layer}\",\"pts\":[[{x1},{y1}],[{x2},{y2}]]}}";
        string Col(double x, double y) => $"{{\"t\":\"line\",\"layer\":\"S-COT\",\"pts\":[[{x - 110},{y - 110}],[{x + 110},{y - 110}],[{x + 110},{y + 110}],[{x - 110},{y + 110}]],\"closed\":true}}";
        var ents = new List<string>();
        foreach (var x in new[] { 0.0, 4000, 8000 }) foreach (var y in new[] { 0.0, 5000 }) ents.Add(Col(x, y));
        // dầm X (y=0, y=5000) và Y (x=0, 4000, 8000), 2 nét cách 220
        foreach (var y in new[] { 0.0, 5000 }) { ents.Add(L("KC_DAM", 0, y - 110, 8000, y - 110)); ents.Add(L("KC_DAM", 0, y + 110, 8000, y + 110)); }
        foreach (var x in new[] { 0.0, 4000, 8000 }) { ents.Add(L("KC_DAM", x - 110, 0, x - 110, 5000)); ents.Add(L("KC_DAM", x + 110, 0, x + 110, 5000)); }
        // dầm cong ban công: tâm (4000, 5000), R 3890 / 4110, 30°…150° (ARC đôi)
        double cx = 4000, cy = 5000, a0 = Math.PI / 6, a1 = 5 * Math.PI / 6;
        string Arc(double r) { var pts = Enumerable.Range(0, 13).Select(i => { double a = a0 + (a1 - a0) * i / 12; return $"[{cx + r * Math.Cos(a)},{cy + r * Math.Sin(a)}]"; }); return $"{{\"t\":\"line\",\"layer\":\"KC_DAM\",\"pts\":[{string.Join(",", pts)}],\"arc\":[{cx},{cy},{r}]}}"; }
        ents.Add(Arc(3890)); ents.Add(Arc(4110));
        // dầm cong vẽ 1 nét đa tuyến có cung lồi: (0,-2000) → (8000,-2000) bulge −0,25 (CW, vồng xuống)
        ents.Add("{\"t\":\"line\",\"layer\":\"KC_DAM\",\"pts\":[[0,-2000],[8000,-2000]],\"bulges\":[-0.25,0]}");
        ents.Add("{\"t\":\"text\",\"layer\":\"CHU\",\"x\":2000,\"y\":5300,\"s\":\"D1 220x400\"}");
        ents.Add("{\"t\":\"text\",\"layer\":\"CHU\",\"x\":1500,\"y\":2500,\"s\":\"GIẾNG TRỜI\"}");      // ô 1 → giếng trời
        ents.Add("{\"t\":\"text\",\"layer\":\"CHU\",\"x\":6000,\"y\":2000,\"s\":\"SÀN HẠ COS -50\"}");  // ô 2 → sàn hạ cốt 50
        string json = "{\"kind\":\"matbang\",\"unit\":\"mm\",\"entities\":[" + string.Join(",", ents) + "]}";
        var log = new List<string>();
        var dx = StructuralPlanImport.ParseExchange(json, log);
        check("VXT_MB_KC: đọc đủ thực thể", dx.Count, ents.Count, 0);
        check("VXT_MB_KC: \"arc\" → đoạn cung (tâm, R đúng)", B(dx.Count(e => e.Arcs != null && Math.Abs(e.Arcs[0].R - 3.89) < 1e-9) == 1), 1, 0);
        check("VXT_MB_KC: \"bulges\" → điểm chia cung lồi", B(dx.Any(e => e.Arcs != null && e.Points.Count > 4 && Math.Abs(e.Points[0].Y + 2) < 1e-9)), 1, 0);

        // nhận diện layer
        var stats = PlanCadGeom.LayerStats(dx);
        Console.WriteLine("   · Layer: " + string.Join("; ", stats.Select(t => $"{t.Layer} [{t.Suggest}] nét {t.Lines}, kín cỡ cột {t.ColRects}, cung {t.Arcs}")));
        check("Nhận diện layer: KC_DAM → Dầm (từ khoá)", B(stats.First(t => t.Layer == "KC_DAM").Suggest == "Dầm"), 1, 0);
        check("Nhận diện layer: S-COT → Cột", B(stats.First(t => t.Layer == "S-COT").Suggest == "Cột"), 1, 0);

        var p = new DetailingProject();
        p.Plan.SpansX = "4 4"; p.Plan.SpansY = "5";
        var lv = p.Plan.Levels.First(l => l.Name == "Tầng 2");
        var keys = new ArchLayerKeys { BeamLayers = new(new[] { "KC_DAM" }, StringComparer.OrdinalIgnoreCase), ColumnLayers = new(new[] { "S-COT" }, StringComparer.OrdinalIgnoreCase), SlabLayers = new() };
        var r = StructuralPlanImport.Import(p, lv, dx, true, false, keys, (0, -0.0), true);
        Console.WriteLine("   · " + string.Join(" | ", r.Log));
        check("Nhập: 6 cột", r.Columns, 6, 0);
        check("Nhập: 2 dầm cong (ARC đôi + đa tuyến cung lồi)", r.CurvedBeams, 2, 0);
        var arcBeam = lv.Items.First(i => i.IsCurved && i.Y1 > 4);
        var ag = PlanArc.Geom(arcBeam).Value;
        check("Dầm cong ARC đôi: bán kính tim 4,0 m", ag.R, 4.0, 1e-3);
        var mb = p.All.OfType<ManualBeam>().First(m => m.Name == arcBeam.Member);
        check("Dầm cong ARC đôi: bề rộng = khoảng cách 2 cung 220 mm", mb.B, 0.22, 1e-6);
        check("Dầm cong: nhịp = chiều dài cung 4·(2π/3)", p.Plan.BeamSpans(arcBeam, lv)[0], Math.Round(4 * 2 * Math.PI / 3, 3), 2e-3);
        var bulgeBeam = lv.Items.First(i => i.IsCurved && i.Y1 < -1);
        check("Dầm cong đa tuyến: độ vồng = −b·c/2 → vồng xuống 1,0 m", bulgeBeam.Bulge, -1.0, 1e-3);
        check("Nhập: dầm thẳng 2 nét (5 dầm)", lv.Items.Count(i => i.Kind == PlanKinds.Beam && !i.IsCurved), 5, 0);
        check("Giếng trời: chữ \"GIẾNG TRỜI\" trong ô dầm → ô trống 4 × 5 m", lv.Items.Count(i => i.Kind == PlanKinds.Void && Math.Abs(i.MaxX - i.MinX - 4) < 1e-6), 1, 0);
        check("Tạo sàn từ dầm: bỏ ô giếng trời → 1 ô sàn", lv.Items.Count(i => i.Kind == PlanKinds.Slab), 1, 0);
        check("Sàn hạ cốt: chữ \"SÀN HẠ COS -50\" → hạ 50 mm", lv.Items.First(i => i.Kind == PlanKinds.Slab).Drop, 0.05, 1e-9);
        check("Giếng trời: không gán cấu kiện", B(lv.Items.Where(i => i.Kind == PlanKinds.Void).All(i => i.Member == "")), 1, 0);
        int again = PlanAutoSlab.Create(p, lv);
        check("Tạo sàn từ dầm lần 2: không tạo trùng", again, 0, 0);

        // sao chép sang tầng có sẵn
        var mai = p.Plan.Levels.First(l => l.Name == "Mái");
        int n = PlanCopy.CopyTo(p, lv, new[] { mai }, true);
        check("Sao chép sang tầng có sẵn: đủ vị trí", n, lv.Items.Count, 0);
        check("Sao chép: dầm cong giữ độ vồng", B(mai.Items.Count(i => i.IsCurved) == 2), 1, 0);
        var slabM = p.All.First(m => m.Name == lv.Items.First(i => i.Kind == PlanKinds.Slab).Member);
        check("Sao chép: SL sàn dùng chung = 2 vị trí", slabM.Quantity, 2, 0);
        check("Sao chép: giữ giếng trời + độ hạ sàn", B(mai.Items.Any(i => i.Kind == PlanKinds.Void) && mai.Items.First(i => i.Kind == PlanKinds.Slab).Drop > 0.04), 1, 0);
        // ô cầu thang trong ô dầm (chữ) + tạo sàn bỏ ô thang
        var t3 = new PlanLevel { Name = "Tầng 3", Elevation = 7.2 };
        p.Plan.Levels.Insert(2, t3);
        var dx2 = dx.Where(e => e.Type != DxfEntityType.Text).ToList();
        dx2.Add(new DxfEntity { Type = DxfEntityType.Text, Layer = "CHU", Text = "Ô CẦU THANG", Points = new() { (2.0, 2.5) } });
        var r3 = StructuralPlanImport.Import(p, t3, dx2, true, false, keys, (0, 0), true);
        check("Ô cầu thang: chữ trong ô dầm → vị trí cầu thang 4 × 5 m", t3.Items.Count(i => i.Kind == PlanKinds.Stair), 1, 0);
        check("Ô cầu thang: tạo sàn bỏ ô thang → 1 ô sàn", t3.Items.Count(i => i.Kind == PlanKinds.Slab), 1, 0);

        // [Dầm liên tục] 2 đoạn thẳng hàng (vẽ từng nhịp) → 1 dầm 2 nhịp; chia tại trục 2 → 2 dầm; gộp lại
        var t4 = new PlanLevel { Name = "Tầng 4", Elevation = 10.8 };
        p.Plan.Levels.Insert(3, t4);
        var dl = new List<DxfEntity>();
        foreach (var x in new[] { 0.0, 4.0, 8.0 })
            dl.Add(new DxfEntity { Type = DxfEntityType.Polyline, Layer = "S-COT", Closed = true, Points = new() { (x - 0.11, -0.11), (x + 0.11, -0.11), (x + 0.11, 0.11), (x - 0.11, 0.11) } });
        dl.Add(new DxfEntity { Type = DxfEntityType.Line, Layer = "KC_DAM", Points = new() { (0.0, 0.0), (4.0, 0.0) } });
        dl.Add(new DxfEntity { Type = DxfEntityType.Line, Layer = "KC_DAM", Points = new() { (4.0, 0.0), (8.0, 0.0) } });
        var r4 = StructuralPlanImport.Import(p, t4, dl, true, false, keys, (0, 0), false);
        Console.WriteLine("   · " + string.Join(" | ", r4.Log));
        var bm4 = t4.Items.Where(i => i.Kind == PlanKinds.Beam).ToList();
        check("Dầm liên tục: 2 đoạn thẳng hàng nối liền → 1 dầm", bm4.Count, 1, 0);
        check("Dầm liên tục: 2 nhịp 4 m (chia theo trục / cột)", p.Plan.BeamSpans(bm4[0], t4).Count, 2, 0);
        var sp2 = PlanBeamMerge.Split(p, t4, bm4[0], 4.0, 0.02);
        check("Chia dầm tại trục 2 → 2 dầm, tên khác nhau", B(sp2 != null && t4.Items.Count(i => i.Kind == PlanKinds.Beam) == 2 && sp2.Member != bm4[0].Member), 1, 0);
        check("Chia dầm: mỗi dầm 1 nhịp 4 m", p.Plan.BeamSpans(sp2, t4).Sum() + p.Plan.BeamSpans(bm4[0], t4).Sum(), 8, 1e-6);
        string m2 = sp2.Member;
        int nmg = PlanBeamMerge.Merge(p, t4, false);
        check("Gộp dầm thẳng hàng (cùng tiết diện) → lại 1 dầm, cấu kiện thừa bị xoá", B(nmg == 1 && t4.Items.Count(i => i.Kind == PlanKinds.Beam) == 1 && p.Beams.All(b => b.Name != m2)), 1, 0);

        // [Nắm kéo đầu dầm] kéo đầu 2 từ trục 3 (x=8) ra x=10 → dài 10 m, nhịp cập nhật; kéo về 8; dầm cùng tên kéo chạm → gộp
        var bg = t4.Items.First(i => i.Kind == PlanKinds.Beam);
        check("Nắm kéo: bắt đúng đầu 2 của dầm", PlanGrip.At(bg, 7.95, 0.03, 0.2), 2, 0);
        check("Nắm kéo: điểm giữa dầm không phải ô nắm", PlanGrip.At(bg, 4.0, 0.0, 0.2), 0, 0);
        PlanGrip.Drag(bg, 2, 10.0, 0.0); PlanGrip.Finish(p, t4, bg);
        check("Nắm kéo: dầm dài 10 m, tổng nhịp 10 m", p.Plan.BeamSpans(bg, t4).Sum(), 10, 1e-6);
        PlanGrip.Drag(bg, 2, 8.0, 0.0); PlanGrip.Finish(p, t4, bg);
        var bx = p.Plan.Place(p, t4, PlanKinds.Beam, 9.0, 0.0, 12.0, 0.0, bg.Member);
        PlanGrip.Drag(bx, 1, 8.0, 0.0);
        string gm = PlanGrip.Finish(p, t4, bx);
        var bgs = t4.Items.Where(i => i.Kind == PlanKinds.Beam).ToList();
        check("Nắm kéo chạm dầm thẳng hàng cùng tên → gộp 1 dầm dài 12 m", B(bgs.Count == 1 && Math.Abs(bgs[0].Length - 12) < 1e-6), 1, 0);
        var bc = new PlanItem { Kind = PlanKinds.Beam, X1 = 0, Y1 = 0, X2 = 4, Y2 = 0, Bulge = -1 };
        PlanGrip.Drag(bc, 2, 8, 0);
        check("Nắm kéo dầm cong: giữ tỷ lệ độ võng / dây cung (−1 → −2)", bc.Bulge, -2, 1e-9);

        // [Sàn theo biên dạng] mặt bằng chữ L và ô có cạnh cong: bấm trong vùng → dò đường bo viền theo tim dầm
        var t5 = new PlanLevel { Name = "Tầng 5", Elevation = 14.4 };
        p.Plan.Levels.Insert(4, t5);
        var Lp = new[] { (0.0, 0.0), (6.0, 0.0), (6.0, 3.0), (3.0, 3.0), (3.0, 6.0), (0.0, 6.0) };
        for (int i = 0; i < Lp.Length; i++)
            p.Plan.Place(p, t5, PlanKinds.Beam, Lp[i].Item1, Lp[i].Item2, Lp[(i + 1) % Lp.Length].Item1, Lp[(i + 1) % Lp.Length].Item2, "");
        var polyL = PlanOutline.Region(t5, 1.0, 1.0);
        check("Sàn chữ L: đường biên 6 đỉnh", polyL?.Count ?? 0, 6, 0);
        check("Sàn chữ L: diện tích 27 m²", Math.Abs(PlanOutline.Area(polyL)), 27, 1e-6);
        var sL = PlanOutline.PlaceSlab(p, t5, polyL, "");
        check("Sàn chữ L: ô sàn đa giác (Outline), không phải hình chữ nhật", B(sL.IsPolygon && t5.Items.Contains(sL)), 1, 0);
        check("Sàn chữ L: bấm vào chỗ khuyết (4.5; 4.5) không chọn được sàn", B(StructuralPlan.HitTest(t5, 4.5, 4.5, 0.1)?.Kind == PlanKinds.Slab), 0, 0);
        var bL0 = t5.Items.First(i => i.Kind == PlanKinds.Beam);
        double hL = ((p.All.FirstOrDefault(x => x.Name == bL0.Member) as ManualBeam)?.B ?? 0.22) / 2;
        var inL = PlanOutline.InsetToBeamFaces(p, t5, polyL);
        check("Biên theo mép dầm: chữ L vẫn 6 đỉnh", inL?.Count ?? 0, 6, 0);
        check("Biên theo mép dầm: diện tích = L thu vào nửa bề rộng dầm", inL == null ? 0 : Math.Abs(PlanOutline.Area(inL)), (6 - 2 * hL) * (3 - 2 * hL) + (3 - 2 * hL) * 3, 1e-6);
        check("Biên theo mép dầm: đỉnh góc trong (h; h)", B(inL != null && inL.Any(q => Math.Abs(q[0] - hL) < 1e-6 && Math.Abs(q[1] - hL) < 1e-6)), 1, 0);
        var fsC = PlanOutline.FaceSnap(p, t5, hL + 0.03, hL - 0.02, 0.2);
        check("Bắt góc mép dầm (mép trong tại nút 0;0)", B(fsC is { } q1 && q1.Kind == "góc mép dầm" && Math.Abs(q1.X - hL) < 1e-6 && Math.Abs(q1.Y - hL) < 1e-6), 1, 0);
        var fsM = PlanOutline.FaceSnap(p, t5, 3.05, 0.04, 0.2);
        check("Bắt giữa dầm (3; 0)", B(fsM is { } q2 && q2.Kind == "giữa dầm" && Math.Abs(q2.X - 3) < 1e-6 && Math.Abs(q2.Y) < 1e-6), 1, 0);
        var pc = PlanOutline.ParseCoords("0,0; 6,0\n6,3; 3,3; 3,6; 0,6");
        check("Nhập toạ độ đỉnh: 6 đỉnh, diện tích 27 m²", B(pc != null && pc.Count == 6 && Math.Abs(Math.Abs(PlanOutline.Area(pc)) - 27) < 1e-9), 1, 0);
        check("Nhập toạ độ sai → null", B(PlanOutline.ParseCoords("0,0; 6;0; a,b") == null), 1, 0);
        check("Đa giác tự cắt (hình nơ) bị phát hiện", B(PlanOutline.SelfIntersects(new() { new[] { 0.0, 0 }, new[] { 2.0, 2 }, new[] { 2.0, 0 }, new[] { 0.0, 2 } }) && !PlanOutline.SelfIntersects(pc)), 1, 0);
        check("Sàn ngoài vùng kín → không có biên", B(PlanOutline.Region(t5, 5.0, 5.0) == null), 1, 0);
        var t6 = new PlanLevel { Name = "Tầng 6", Elevation = 18.0 };
        p.Plan.Levels.Insert(5, t6);
        p.Plan.Place(p, t6, PlanKinds.Beam, 0, 0, 4, 0, ""); p.Plan.Place(p, t6, PlanKinds.Beam, 4, 0, 4, 4, ""); p.Plan.Place(p, t6, PlanKinds.Beam, 0, 4, 0, 0, "");
        var arcB = p.Plan.Place(p, t6, PlanKinds.Beam, 4, 4, 0, 4, ""); arcB.Bulge = -1;   // cung vồng lên trên
        var polyC = PlanOutline.Region(t6, 2.0, 4.5);
        double aC = polyC == null ? 0 : Math.Abs(PlanOutline.Area(polyC));
        Console.WriteLine($"   · Sàn cạnh cong: {polyC?.Count ?? 0} đỉnh, A = {aC:0.###} m² (chính xác 18.795)");
        check("Sàn cạnh cong: bấm trong phần vồng → diện tích ≈ 16 + 2,795 m²", aC, 18.795, 0.08);
        var sC = PlanOutline.PlaceSlab(p, t6, polyC, "");
        string dxf5 = StructuralPlanDrawing.Level(p.Plan, t5, StructuralPlanDrawing.Map(p, new()), StructuralPlanDrawing.LevelTitle(t5), false).ToDxfString();
        check("Xuất DXF: sàn biên dạng có ghi chú", B(dxf5.Contains(KetCauVXT.Drawing.DxfWriter.Encode("SÀN BIÊN DẠNG"))), 1, 0);

        // [Hệ trục trước] mặt bằng chưa có trục: nhận trục từ cột / dầm, dời để trục 1-A ở gốc (cả ánh xạ kiến trúc)
        var gp = new StructuralPlan();
        var g0 = gp.Levels[1];
        g0.UnderlayDx = 0.5;
        foreach (var gx in new[] { 2.0, 5.6, 9.8 }) foreach (var gy in new[] { 1.0, 5.5 }) g0.Items.Add(new PlanItem { Kind = PlanKinds.Column, X1 = gx, Y1 = gy, X2 = gx, Y2 = gy });
        g0.Items.Add(new PlanItem { Kind = PlanKinds.Beam, X1 = 2.0, Y1 = 1.0, X2 = 9.8, Y2 = 1.0 });
        g0.Items.Add(new PlanItem { Kind = PlanKinds.Column, X1 = 7.3, Y1 = 3.0, X2 = 7.3, Y2 = 3.0 });   // cột lẻ không thành trục
        check("Hệ trục trước: chưa có trục", B(gp.HasGrid), 0, 0);
        string gmsg = PlanGridFirst.Infer(gp);
        Console.WriteLine("   · " + gmsg);
        check("Hệ trục trước: bước trục X = 3.6 4.2", B(gp.SpansX == "3.6 4.2"), 1, 0);
        check("Hệ trục trước: bước trục Y = 4.5", B(gp.SpansY == "4.5"), 1, 0);
        check("Hệ trục trước: cột góc dời về giao trục 1-A (0; 0)", B(g0.Items.Any(i => i.Kind == PlanKinds.Column && Math.Abs(i.X1) < 1e-9 && Math.Abs(i.Y1) < 1e-9)), 1, 0);
        check("Hệ trục trước: ánh xạ kiến trúc dời theo (0,5 − 2 = −1,5)", g0.UnderlayDx, -1.5, 1e-9);
        check("Hệ trục trước: đã có trục → không tạo lại", B(PlanGridFirst.Infer(gp) == null), 1, 0);

        // [Trục từ AutoCAD] layer TRUC + trục chọn trong AutoCAD ("axis":true); tên trục trong bong bóng; dầm qua 3 cột = 1 dầm; sàn theo mép dầm
        {
            string Ln(string layer, double x1, double y1, double x2, double y2, bool ax = false) => $"{{\"t\":\"line\",\"layer\":\"{layer}\",\"pts\":[[{x1},{y1}],[{x2},{y2}]]{(ax ? ",\"axis\":true" : "")}}}";
            string Tx(string layer, double x, double y, string t, bool ax = false) => $"{{\"t\":\"text\",\"layer\":\"{layer}\",\"x\":{x},\"y\":{y},\"s\":\"{t}\"{(ax ? ",\"axis\":true" : "")}}}";
            string Cl(double x, double y) => $"{{\"t\":\"line\",\"layer\":\"S-COT\",\"pts\":[[{x - 110},{y - 110}],[{x + 110},{y - 110}],[{x + 110},{y + 110}],[{x - 110},{y + 110}]],\"closed\":true}}";
            var ae = new List<string>();
            var AX = new[] { 10000.0, 13600, 17800 }; var AY = new[] { 5000.0, 9500 };
            ae.Add(Ln("TRUC", 10000, 4000, 10000, 7000)); ae.Add(Ln("TRUC", 10000, 7100, 10000, 11000));   // trục nét đứt 2 đoạn
            ae.Add(Ln("TRUC", 13600, 4000, 13600, 11000)); ae.Add(Ln("TRUC", 17800, 4000, 17800, 11000));
            foreach (var y in AY) ae.Add(Ln("0", 9000, y, 19000, y, true));   // trục Y người dùng chọn trong AutoCAD
            ae.Add(Tx("TRUC", 10000, 3500, "1")); ae.Add(Tx("TRUC", 13600, 3500, "2")); ae.Add(Tx("TRUC", 17800, 3500, "3"));
            ae.Add(Tx("KYHIEU", 8500, 5000, "A", true)); ae.Add(Tx("KYHIEU", 8500, 9500, "B", true));
            foreach (var x in AX) foreach (var y in AY) ae.Add(Cl(x, y));
            ae.Add(Ln("KC_DAM", 10000, 4890, 17800, 4890)); ae.Add(Ln("KC_DAM", 10000, 5110, 17800, 5110));   // dầm trục A vẽ liền qua 3 cột
            foreach (var (xa, xb) in new[] { (10000.0, 13600.0), (13600.0, 17800.0) }) { ae.Add(Ln("KC_DAM", xa, 9390, xb, 9390)); ae.Add(Ln("KC_DAM", xa, 9610, xb, 9610)); }   // trục B vẽ từng nhịp
            foreach (var x in AX) { ae.Add(Ln("KC_DAM", x - 110, 5000, x - 110, 9500)); ae.Add(Ln("KC_DAM", x + 110, 5000, x + 110, 9500)); }
            var aents = StructuralPlanImport.ParseExchange("{\"unit\":\"mm\",\"entities\":[" + string.Join(",", ae) + "]}");
            check("Trục AutoCAD: trục chọn → layer trục đã chọn", aents.Count(e => e.Layer == ArchLayerKeys.PickedAxisLayer), 4, 0);
            var pa = new DetailingProject();
            var la = pa.Plan.Levels[1];
            var ka = new ArchLayerKeys { BeamLayers = new(new[] { "KC_DAM" }, StringComparer.OrdinalIgnoreCase), ColumnLayers = new(new[] { "S-COT" }, StringComparer.OrdinalIgnoreCase), SlabLayers = new() };
            var ra = StructuralPlanImport.Import(pa, la, aents, true, false, ka, null, true);
            Console.WriteLine("   · " + string.Join(" | ", ra.Log));
            check("Trục AutoCAD: 3 trục X, 2 trục Y", ra.AxesX * 10 + ra.AxesY, 32, 0);
            check("Trục AutoCAD: bước trục X 3.6 4.2, Y 4.5", B(pa.Plan.SpansX == "3.6 4.2" && pa.Plan.SpansY == "4.5"), 1, 0);
            check("Trục AutoCAD: tên trục 1 2 3 / A B từ bong bóng", B(string.Join(",", Enumerable.Range(0, 3).Select(i => pa.Plan.AxisLabel(true, i))) == "1,2,3" && pa.Plan.AxisLabel(false, 1) == "B"), 1, 0);
            check("Trục AutoCAD: gốc = giao trục 1-A → cột tại (0; 0)", B(la.Items.Any(i => i.Kind == PlanKinds.Column && Math.Abs(i.X1) < 1e-6 && Math.Abs(i.Y1) < 1e-6)), 1, 0);
            var bxs = la.Items.Where(i => i.Kind == PlanKinds.Beam && Math.Abs(i.Y1 - i.Y2) < 1e-6).ToList();
            check("Dầm qua 3 cột thẳng hàng = 1 dầm (trục A vẽ liền, trục B vẽ từng nhịp) → 2 dầm phương X", bxs.Count, 2, 0);
            check("Dầm qua 3 cột: mỗi dầm 2 nhịp (thép triển khai liên tục)", bxs.Sum(b => pa.Plan.BeamSpans(b, la).Count), 4, 0);
            var sl = la.Items.Where(i => i.Kind == PlanKinds.Slab).OrderBy(i => i.MinX).ToList();
            check("Sàn theo mép dầm: 2 ô sàn", sl.Count, 2, 0);
            check("Sàn theo mép dầm: ô 1 rộng 3,6 − 0,22 = 3,38 m (mép trong dầm)", sl.Count > 0 ? sl[0].MaxX - sl[0].MinX : 0, 3.38, 1e-6);
            check("Sàn theo mép dầm: ô đánh dấu Faces", B(sl.All(q => q.Faces)), 1, 0);
            var ms1 = pa.All.FirstOrDefault(m => m.Name == sl[0].Member) as ManualSlab;
            check("Sàn theo mép dầm: nhịp tính toán Lx = 3,6 m (tim dầm)", ms1?.Lx ?? 0, 3.6, 1e-6);
            // tên trục đi theo trục khi thêm trục
            PlanGridEdit.Insert(pa.Plan, true, 1.8);
            check("Thêm trục: trục cũ '2' giữ tên, trục chèn tên 1'", B(pa.Plan.AxisLabel(true, 2) == "2" && pa.Plan.AxisLabel(true, 3) == "3" && pa.Plan.AxisLabel(true, 1) == "1'"), 1, 0);
            pa.Plan.SetAxisLabel(true, 1, "1'");
            check("Đổi tên trục 1'", B(pa.Plan.AxisLabel(true, 1) == "1'"), 1, 0);
            // dầm vẽ thêm nối liền, cùng tiết diện, khác tên đang chọn → nhập vào dầm có sẵn
            var lt = new PlanLevel { Name = "Tầng thử", Elevation = 30 };
            pa.Plan.Levels.Add(lt);
            var d1 = pa.Plan.Place(pa, lt, PlanKinds.Beam, 0, 0, 3.6, 0, "");
            var d2 = pa.Plan.Place(pa, lt, PlanKinds.Beam, 3.6, 0, 7.8, 0, "");
            bool att = PlanBeamMerge.AttachNew(pa, lt, d2, false);
            check("Vẽ dầm nối tiếp cùng tiết diện (đang chọn tên khác) → 1 dầm 2 nhịp", B(att && lt.Items.Count(i => i.Kind == PlanKinds.Beam) == 1), 1, 0);
        }

        // [Dầm 2 nét – mép đứt đoạn] mép trên liền 0..8 m, mép dưới đứt tại cột / dầm giao → 1 dầm tim y = 0, không sinh dầm lấy mép làm tim
        {
            var sg = new List<CadSeg>
            {
                new() { X1 = 0, Y1 = 0.11, X2 = 8, Y2 = 0.11, Layer = "DAM" },
                new() { X1 = 0.11, Y1 = -0.11, X2 = 3.89, Y2 = -0.11, Layer = "DAM" },
                new() { X1 = 4.11, Y1 = -0.11, X2 = 7.89, Y2 = -0.11, Layer = "DAM" },
            };
            var sgl = new List<CadSeg>();
            var pr = PlanCadGeom.PairParallelLines(sg, 0.10, 0.60, sgl);
            check("Dầm mép đứt đoạn: 1 tim dầm, không nét lẻ", B(pr.Count == 1 && sgl.Count == 0), 1, 0);
            check("Dầm mép đứt đoạn: tim y = 0, dài 8 m, b = 0,22", B(pr.Count == 1 && Math.Abs(pr[0].Center.Y1) < 1e-6 && Math.Abs(pr[0].Center.Length - 8) < 1e-6 && Math.Abs(pr[0].T - 0.22) < 1e-6), 1, 0);
            // nhập: mép dưới đứt + nét lẻ trong bề rộng dầm → chỉ 1 dầm
            string L2(string ly, double x1, double y1, double x2, double y2) => $"{{\"t\":\"line\",\"layer\":\"{ly}\",\"pts\":[[{x1},{y1}],[{x2},{y2}]]}}";
            string C2(double x, double y) => $"{{\"t\":\"line\",\"layer\":\"S-COT\",\"pts\":[[{x - 110},{y - 110}],[{x + 110},{y - 110}],[{x + 110},{y + 110}],[{x - 110},{y + 110}]],\"closed\":true}}";
            var je = new List<string> { C2(0, 0), C2(4000, 0), C2(8000, 0),
                L2("KC_DAM", 0, 110, 8000, 110), L2("KC_DAM", 110, -110, 3890, -110), L2("KC_DAM", 4110, -110, 7890, -110), L2("KC_DAM", 500, 50, 3500, 50) };
            // sàn zic zắc trên layer sàn (đa tuyến kín) + tên S5
            je.Add("{\"t\":\"pline\",\"layer\":\"KC_SAN\",\"pts\":[[0,0],[8000,0],[8000,3000],[6000,4000],[4000,3000],[2000,4000],[0,3000]],\"closed\":true}");
            je.Add("{\"t\":\"text\",\"layer\":\"KC_SAN\",\"x\":4000,\"y\":1500,\"s\":\"S5\"}");
            var jents = StructuralPlanImport.ParseExchange("{\"unit\":\"mm\",\"entities\":[" + string.Join(",", je) + "]}");
            var pj = new DetailingProject(); var lj = pj.Plan.Levels[1];
            var kj = new ArchLayerKeys { BeamLayers = new(new[] { "KC_DAM" }, StringComparer.OrdinalIgnoreCase), ColumnLayers = new(new[] { "S-COT" }, StringComparer.OrdinalIgnoreCase), SlabLayers = new(new[] { "KC_SAN" }, StringComparer.OrdinalIgnoreCase) };
            var rj = StructuralPlanImport.Import(pj, lj, jents, true, false, kj, null, false);
            Console.WriteLine("   · " + string.Join(" | ", rj.Log));
            check("Nhập dầm mép đứt + nét lẻ: đúng 1 dầm (không dầm trùng)", lj.Items.Count(i => i.Kind == PlanKinds.Beam), 1, 0);
            var zs = lj.Items.FirstOrDefault(i => i.Kind == PlanKinds.Slab);
            check("Sàn zic zắc trên layer sàn: ô sàn đa giác 7 đỉnh tên S5", B(zs != null && zs.IsPolygon && zs.Outline.Count == 7 && zs.Member == "S5"), 1, 0);
            check("Sàn zic zắc: diện tích 8×3 + 4 tam giác = 28 m²", zs?.IsPolygon == true ? Math.Abs(PlanOutline.Area(zs.Outline)) : 0, 28, 1e-6);
            // vùng kín từ nét CAD bất kỳ (nền xref): hình zic zắc
            var zz = new[] { (0.0, 0.0), (8.0, 0.0), (8.0, 3.0), (6.0, 4.0), (4.0, 3.0), (2.0, 4.0), (0.0, 3.0) };
            var zsegs = zz.Select((q, i) => (q.Item1, q.Item2, zz[(i + 1) % zz.Length].Item1, zz[(i + 1) % zz.Length].Item2)).ToList();
            zsegs.Add((4.0, -2.0, 4.0, 10.0));   // nét khác cắt qua (trục) – vẫn tách đúng nửa trái
            var rz = PlanOutline.RegionOf(zsegs, 1.0, 1.0);
            check("Vùng kín trên nền CAD: nửa trái hình zic zắc = 14 m²", rz == null ? 0 : Math.Abs(PlanOutline.Area(rz)), 14, 1e-6);
            // [Sàn polyline] cung tiếp tuyến: từ (0;0) hướng +X đến (2;2) → tâm (0;2), R = 2, tiếp tuyến cuối +Y
            var ta = PlanOutline.TangentArc((0, 0), (1, 0), (2, 2), 16);
            check("Polyline – cung tiếp tuyến: điểm giữa (√2; 2−√2), hướng cuối +Y", B(Math.Abs(ta.Pts[7].X - Math.Sqrt(2)) < 1e-3 && Math.Abs(ta.Pts[7].Y - (2 - Math.Sqrt(2))) < 1e-3 && Math.Abs(ta.Tan.X) < 1e-9 && Math.Abs(ta.Tan.Y - 1) < 1e-9), 1, 0);
            check("Polyline – cung tiếp tuyến thẳng hàng → đoạn thẳng", PlanOutline.TangentArc((0, 0), (1, 0), (5, 0)).Pts.Count, 1, 0);
            // biên sàn: thẳng 6 m → cung tiếp tuyến nửa tròn về (6; 4) → thẳng về (0; 4) → khép: diện tích = 6×4 + π·2²/2
            var plp = new List<(double X, double Y)> { (0, 0), (6, 0) };
            plp.AddRange(PlanOutline.TangentArc((6, 0), (1, 0), (6, 4), 32).Pts);
            plp.Add((0, 4));
            double apl = Math.Abs(PlanOutline.Area(plp.Select(q => new[] { q.X, q.Y }).ToList()));
            check("Polyline thẳng + cung + thẳng: diện tích ≈ 24 + 2π", apl, 24 + 2 * Math.PI, 0.05);
            check("Nhập điểm @3.6,0 / 5<90 / 2,3 / 4 theo hướng", B(PlanOutline.ParsePoint("@3.6,0", (1, 1), (0, 0)) == (4.6, 1.0) && PlanOutline.ParsePoint("5<90", (0, 0), (0, 0)) is { } q90 && Math.Abs(q90.X) < 1e-9 && Math.Abs(q90.Y - 5) < 1e-9
                && PlanOutline.ParsePoint("2,3", (9, 9), (0, 0)) == (2.0, 3.0) && PlanOutline.ParsePoint("4", (1, 0), (0, 2)) == (1.0, 4.0) && PlanOutline.ParsePoint("a", (0, 0), (1, 0)) == null), 1, 0);
            // cạnh cong 3 điểm
            var a3 = PlanOutline.Arc3((0, 0), (1, 1), (2, 0), 16);
            check("Cạnh cong 3 điểm: điểm giữa cung = (1; 1), kết thúc (2; 0)", B(a3.Count == 16 && Math.Abs(a3[7].X - 1) < 1e-3 && Math.Abs(a3[7].Y - 1) < 1e-3 && a3[^1] == (2.0, 0.0)), 1, 0);
        }

        // xuất DXF: dầm cong = ARC trên layer bê tông
        var map = StructuralPlanDrawing.Map(p, new());
        string dxf = StructuralPlanDrawing.Level(p.Plan, lv, map, StructuralPlanDrawing.LevelTitle(lv), false).ToDxfString();
        var lines = dxf.Replace("\r", "").Split('\n').Select(q => q.Trim()).ToList();
        int arcs = 0;
        for (int i = 0; i + 1 < lines.Count; i++)
            if (lines[i] == "0" && lines[i + 1] == "ARC")
            {
                for (int j = i + 2; j + 1 < lines.Count && !(lines[j] == "0" && j > i + 2); j += 2)
                    if (lines[j] == "8") { if (lines[j + 1] == "BE_TONG") arcs++; break; }
            }
        Console.WriteLine($"   · DXF mặt bằng: {arcs} ARC trên BE_TONG");
        check("Xuất DXF: ghi sàn hạ cốt \"… HẠ COS -50\"", B(dxf.Contains("COS -50")), 1, 0);
        check("Xuất DXF: mỗi dầm cong 2 cung mép (ARC, layer BE_TONG)", arcs, 4, 0);
    }
}
