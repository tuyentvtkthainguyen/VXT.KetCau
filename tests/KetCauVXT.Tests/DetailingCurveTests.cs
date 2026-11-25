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
