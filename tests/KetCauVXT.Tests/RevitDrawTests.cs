using System.Text.Json;
using KetCauVXT.Model;
using KetCauVXT.Ui;

namespace KetCauVXT.Tests;

/// <summary>Môi trường vẽ kiểu Revit: kiểu tường nhiều lớp (vữa trát, ốp đá, gạch men), thư viện vật liệu,
/// cửa gắn tường tự cắt tường, kích thước tạm, tải tường trừ cửa, gợi ý lanh tô, nối góc L/T, đường định vị.</summary>
public static class RevitDrawTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---- 1. Kiểu tường nhiều lớp ----
        var lib = WallAssembly.Library();
        var g220 = lib.First(t => t.Name == "Gạch đặc 220 trát 2 mặt");
        check("Gạch đặc 220 trát 2 mặt: 3 lớp", g220.Layers.Count, 3, 0);
        check("Gạch đặc 220 trát 2 mặt: tổng dày 250 mm", g220.TotalThickness * 1000, 250, 1e-6);
        check("Gạch đặc 220 trát 2 mặt: g = 0,22·18 + 2·0,015·18 = 4,50 kN/m²", g220.WeightPerM2, 0.22 * 18 + 2 * 0.015 * 18, 1e-9);
        check("Đồng bộ: chiều dày lõi 0,22 m (dầm đỡ tường, khối xây)", g220.Thickness, 0.22, 1e-9);
        check("Đồng bộ: trát 2 mặt", B(g220.Finish == WallFinish.Plaster2Sides && g220.PlasterSides == 2), 1, 0);
        var stone = lib.First(t => t.Name == "Tường 220 ốp đá granite mặt ngoài");
        check("Ốp đá: g = 0,02·28 + 0,02·18 + 0,22·18 + 0,015·18", stone.WeightPerM2, 0.02 * 28 + 0.02 * 18 + 0.22 * 18 + 0.015 * 18, 1e-9);
        check("Ốp đá: lớp ngoài cùng là đá granite", B(stone.Layers[0].Function == WallLayerFn.Stone && stone.Layers[0].Material == WallMaterials.Granite), 1, 0);
        check("Ốp đá: tổng dày 275 mm", stone.TotalThickness * 1000, 275, 1e-6);
        check("Ốp đá: 2 lớp vữa → trát 2 mặt", stone.PlasterSides, 2, 0);
        var wc = lib.First(t => t.Name == "Tường 110 ốp gạch men WC 1 mặt");
        check("Gạch men WC: g = 0,015·18 + 0,11·18 + 0,015·18 + 0,008·20", wc.WeightPerM2, 0.015 * 18 + 0.11 * 18 + 0.015 * 18 + 0.008 * 20, 1e-9);
        var aac = lib.First(t => t.Name == "Gạch AAC 100 trát 2 mặt");
        check("AAC 100: γ lõi 8 kN/m³, g = 0,1·8 + 2·0,01·18", aac.WeightPerM2, 0.1 * 8 + 2 * 0.01 * 18, 1e-9);
        var bt = lib.First(t => t.Name == "Tường BTCT 200");
        check("BTCT 200: không trát, γ 25", B(bt.Finish == WallFinish.NoPlaster && Math.Abs(bt.UnitWeight - 25) < 1e-9), 1, 0);
        check("Mô tả kiểu tường: \"15 + 220 + 15 = 250 mm\"", B(WallAssembly.Describe(g220).StartsWith("15 + 220 + 15 = 250 mm")), 1, 0);

        // ---- 2. Đổi kiểu cũ sang nhiều lớp: giữ tải ----
        var olds = WallType.Defaults();
        bool keep = true;
        foreach (var o in olds)
        {
            double before = o.WeightPerM2, thick = o.Thickness;
            WallAssembly.ToLayered(o);
            keep &= Math.Abs(o.WeightPerM2 - before) < 1e-9 && Math.Abs(o.Thickness - thick) < 1e-9 && o.IsLayered;
        }
        check("Đổi mọi kiểu tường cũ sang nhiều lớp: giữ nguyên tải 1 m² và chiều dày lõi", B(keep), 1, 0);
        var o1 = olds.First(t => t.Name == "Tường 110 trát 1 mặt");
        check("Kiểu cũ trát 1 mặt → 2 lớp (vữa + lõi)", o1.Layers.Count, 2, 0);
        check("Kiểu cũ gạch nhẹ AAC → vật liệu AAC", B(olds.First(t => t.Name == "Tường 110 gạch nhẹ AAC").Layers.Any(l => l.Material == WallMaterials.Aac)), 1, 0);

        var p = new Project();
        p.Stories.Add(new Story { Name = "T1", Elevation = 0, Height = 3.6 });
        p.Stories.Add(new Story { Name = "T2", Elevation = 3.6, Height = 3.3 });
        int nOld = p.WallTypes.Count;
        int added = WallAssembly.Upgrade(p);
        check("Nâng cấp dự án: thêm thư viện kiểu tường nhiều lớp", added, WallAssembly.Library().Count, 0);
        check("Nâng cấp dự án: giữ kiểu cũ (cùng tên)", p.WallTypes.Count, nOld + added, 0);
        check("Nâng cấp lần 2 không thêm trùng", WallAssembly.Upgrade(p), 0, 0);
        check("Vật liệu mặc định ≥ 10 (gạch đặc, rỗng, AAC, BT, vữa, đá, men…)", B(p.WallMaterials.Count >= 10), 1, 0);

        // sửa γ vật liệu → cập nhật kiểu tường
        p.WallMaterials.First(m => m.Name == WallMaterials.SolidBrick).UnitWeight = 19;
        WallAssembly.ApplyMaterials(p);
        var g220p = p.WallTypes.First(t => t.Name == "Gạch đặc 220 trát 2 mặt");
        check("Sửa γ gạch đặc = 19 → g = 0,22·19 + 2·0,015·18", g220p.WeightPerM2, 0.22 * 19 + 2 * 0.015 * 18, 1e-9);

        // sửa lõi ở bảng "Loại tường" cũ: giữ lớp ốp đá
        var st2 = WallAssembly.Make("Thử ốp đá", WallFunctionName.Exterior, stone.Layers.ToArray());
        WallAssembly.SetCore(st2, 0.11, null);
        check("Sửa dày lõi 110 (bảng cũ): giữ 4 lớp, lõi 0,11", B(st2.Layers.Count == 4 && Math.Abs(st2.Thickness - 0.11) < 1e-9), 1, 0);
        st2.Finish = WallFinish.Plaster1Side;
        WallAssembly.RebuildFromLegacy(st2);
        check("Đổi trát 1 mặt (bảng cũ): vẫn giữ lớp đá granite, còn 1 lớp vữa", B(st2.Layers.Any(l => l.Function == WallLayerFn.Stone) && st2.Layers.Count(l => l.Function == WallLayerFn.Plaster) == 1), 1, 0);

        // ---- 3. Cửa gắn tường ----
        var w = new WallLine { Id = 1, Story = "T1", X1 = 0, Y1 = 0, X2 = 5, Y2 = 0, Type = "Gạch đặc 220 trát 2 mặt", Height = 3.0 };
        p.Walls.Add(w);
        var r = WallAssembly.PlaceOpening(p, w, "D1 900x2200", 1.0);
        check("Đặt cửa D1 lên tường: thành công", B(r.Ok), 1, 0);
        check("Cửa D1 tâm cách đầu tường 1,00 m", w.Openings[0].Offset, 1.0, 1e-9);
        var segs = WallAssembly.SolidSegments(p, w);
        check("Tường tự cắt ô cửa: 2 đoạn đặc", segs.Count, 2, 0);
        check("Đoạn đặc 1: 0 → 0,55 m", segs[0].B, 0.55, 1e-9);
        check("Đoạn đặc 2: 1,45 → 5 m", segs[1].A, 1.45, 1e-9);
        var polys = WallAssembly.PlanPolygons(p, w, 0.25);
        check("Đa giác mặt bằng: 2 hình chữ nhật, bề rộng 250 mm", B(polys.Count == 2 && Math.Abs(polys[0][0].Y - 0.125) < 1e-9 && Math.Abs(polys[0][2].Y + 0.125) < 1e-9), 1, 0);

        // đặt sát đầu tường → chặn vào trong
        var r2 = WallAssembly.PlaceOpening(p, w, "S1 1200x1400", 4.9);
        check("Đặt cửa sổ sát đầu tường → tâm chặn ở 5 − 0,05 − 0,6 = 4,35", w.Openings[1].Offset, 4.35, 1e-9);
        check("Cửa sổ S1 (bậu 0,9, cao 1,4) cắt qua mặt phẳng 1,2 m → cắt tường trên mặt bằng", WallAssembly.SolidSegments(p, w).Count, 3, 0);
        // đặt chồng → tự dời chỗ trống
        var r3 = WallAssembly.PlaceOpening(p, w, "D2 800x2200", 1.2);
        double o3 = w.Openings[2].Offset;
        check("Đặt chồng lên D1 → tự dời tới chỗ trống, không chồng", B(r3.Ok && !WallAssembly.Overlaps(p, w, 0.8, o3, w.Openings[2].Id)), 1, 0);
        check("Dời tới sát mép phải D1 + khe 0,10: tâm 1,45 + 0,1 + 0,4 = 1,95", o3, 1.95, 1e-9);
        // ô thoáng cao không cắt mặt bằng
        var vent = p.OpeningTypes.First(t => t.Kind == WallOpeningKind.Vent);
        check("Ô thoáng bậu 2,0 m không cắt mặt bằng (trên mặt phẳng cắt)", B(!WallAssembly.CutsPlan(vent)), 1, 0);
        // tường quá ngắn
        var shortW = new WallLine { Id = 2, Story = "T1", X1 = 0, Y1 = 1, X2 = 0.6, Y2 = 1, Type = "Gạch đặc 110 trát 2 mặt" };
        p.Walls.Add(shortW);
        check("Tường 0,6 m không đặt được cửa 0,9 m", B(!WallAssembly.PlaceOpening(p, shortW, "D1 900x2200", 0.3).Ok), 1, 0);

        // ---- 4. Kích thước tạm ----
        var d1 = w.Openings[0];
        var td = WallAssembly.TempDims(p, w, 0.9, d1.Offset, d1.Id);
        check("Kích thước tạm trái D1 = 0,55 (tới đầu tường)", td.Left, 0.55, 1e-9);
        check("Kích thước tạm phải D1 = 1,55 − 1,45 = 0,10 (tới D2)", td.Right, 0.10, 1e-9);
        var rs = WallAssembly.SetTempDim(p, w, d1, true, 0.30);
        check("Sửa số kích thước tạm trái = 0,30 → tâm D1 = 0,75", d1.Offset, 0.75, 1e-9);
        check("Sửa kích thước tạm: báo thành công", B(rs.Ok), 1, 0);
        check("Sửa kích thước tạm âm → báo lỗi", B(!WallAssembly.SetTempDim(p, w, d1, true, -1).Ok), 1, 0);

        // ---- 5. Tải tường trừ cửa thật ----
        double H = 3.0, Lw = 5;
        var wt = p.WallTypes.First(t => t.Name == w.Type);
        double open = 0.9 * 2.2 + 1.2 * 1.4 + 0.8 * 2.2;
        check("Diện tích cửa gắn tường = Σ b·h", WallAssembly.HostedOpeningArea(p, w, H), open, 1e-9);
        check("Tải tường lên dầm = g·H·(1 − Acửa/(L·H))", WallAssembly.LineLoad(p, w, wt, H), wt.WeightPerM2 * H * (1 - open / (Lw * H)), 1e-9);
        check("Cửa cao hơn tường bị cắt theo H (H = 2,0 m)", WallAssembly.HostedOpeningArea(p, w, 2.0), 0.9 * 2.0 + 1.2 * 1.1 + 0.8 * 2.0, 1e-9);
        check("Tường có cửa gắn: bỏ qua diện tích nhập tay cũ", WallAssembly.EffectiveOpenArea(p, new WallLine { OpeningArea = 9, Openings = w.Openings }, wt, 15, 3), open, 1e-9);
        check("Tường không cửa gắn: dùng diện tích nhập tay", WallAssembly.EffectiveOpenArea(p, new WallLine { OpeningArea = 2.5 }, wt, 15, 3), 2.5, 1e-9);

        // tải sinh tự động (WallLoadGenerator) dùng diện tích cửa thật
        // (không có dầm đỡ → tải về sàn; kiểm qua LineLoad là đủ – WallLoadGenerator gọi cùng hàm EffectiveOpenArea)

        // ---- 6. Gợi ý lanh tô ----
        var lt = WallAssembly.ProposeLintels(p);
        check("Gợi ý lanh tô: 3 cửa → 3 lanh tô", lt.Count, 3, 0);
        var ltD1 = lt.First(q => q.OpeningType == "D1 900x2200");
        check("Lanh tô D1: gối 200 mm mỗi bên, dài 1,30 m", ltD1.Length, 1.3, 1e-9);
        check("Lanh tô D1: h = max(0,09; 0,15) = 0,15 m", ltD1.Depth, 0.15, 1e-9);
        check("Lanh tô: bề rộng = chiều dày lõi tường 0,22", ltD1.WallThickness, 0.22, 1e-9);
        var w110 = new WallLine { Id = 3, Story = "T1", X1 = 0, Y1 = 3, X2 = 6, Y2 = 3, Type = "Gạch đặc 110 trát 2 mặt" };
        p.Walls.Add(w110);
        WallAssembly.PlaceOpening(p, w110, "D5 2400x2400", 3);
        var lt5 = WallAssembly.ProposeLintels(p).First(q => q.WallId == 3);
        check("Cửa 2,4 m tường 110: gối 250, h = 0,25 (L0/10 làm tròn 50)", B(Math.Abs(lt5.Bearing - 0.25) < 1e-9 && Math.Abs(lt5.Depth - 0.25) < 1e-9), 1, 0);
        check("Cửa 2,4 m tường 110: cảnh báo bổ trụ BTCT", B(lt5.Note.Contains("bổ trụ")), 1, 0);

        // ---- 7. Xoá / dời tường mang theo cửa ----
        var del = WallAssembly.RemoveOpening(w110, w110.Openings[0]);
        check("Xoá cửa → tường liền lại (1 đoạn đặc)", B(del.Ok && WallAssembly.SolidSegments(p, w110).Count == 1), 1, 0);
        double off0 = w.Openings[0].Offset;
        w.X1 += 1; w.X2 += 1;   // dời tường – cửa đi theo (vị trí tính theo đầu tường)
        var (cx, _) = WallAssembly.PointAt(w, off0);
        check("Dời tường 1 m: cửa dời theo (tâm D1 x = 1,75)", cx, 1.75, 1e-9);
        int nOpen = p.Walls.Sum(q => q.Openings.Count);
        ModelEdit.DeleteWall(p, w);
        check("Xoá tường → cửa gắn trên tường bị xoá theo", p.Walls.Sum(q => q.Openings.Count), nOpen - 3, 0);

        // ---- 8. Chiếu điểm lên tường ----
        var wp = new WallLine { Id = 9, X1 = 0, Y1 = 0, X2 = 0, Y2 = 4 };
        double t = WallAssembly.ProjectPoint(wp, 0.3, 2.5, out double dist);
        check("Chiếu điểm lên tường đứng: t = 2,5", t, 2.5, 1e-9);
        check("Chiếu điểm lên tường đứng: cách tim 0,3", dist, 0.3, 1e-9);

        // ---- 9. Nối góc L / T ----
        var a = new WallLine { Id = 11, X1 = 0, Y1 = 0, X2 = 4, Y2 = 0 };
        var b = new WallLine { Id = 12, X1 = 4, Y1 = 0, X2 = 4, Y2 = 3 };
        var c = new WallLine { Id = 13, X1 = 4, Y1 = 3, X2 = 8, Y2 = 3 };   // thẳng hàng không – vuông góc với b
        var dcol = new WallLine { Id = 14, X1 = 8, Y1 = 3, X2 = 12, Y2 = 3 };   // thẳng hàng với c
        var j = WallAssembly.Joins(new List<WallLine> { a, b, c, dcol }, q => q.Id == 12 ? 0.22 : 0.11);
        check("Góc L: tường a kéo dài nửa dày tường b (0,11) ở đầu cuối", j[11].End, 0.11, 1e-9);
        check("Góc L: tường b kéo dài nửa dày tường a (0,055) ở đầu", j[12].Start, 0.055, 1e-9);
        check("Hai tường thẳng hàng: không kéo dài", B(j[13].End == 0 && j[14].Start == 0), 1, 0);

        // ---- 10. Vẽ: kích thước tạm, nhập chiều dài, đường định vị ----
        var (lenMm, ang) = WallAssembly.DrawDims(0, 0, 3, 4);
        check("Kích thước tạm khi vẽ: dài 5000 mm", lenMm, 5000, 1e-9);
        check("Kích thước tạm khi vẽ: góc 53,1°", ang, 53.1, 1e-9);
        var pe = WallAssembly.AlongCursor(1, 1, 1, 9, 2.5);
        check("Gõ chiều dài 2,5 theo hướng con trỏ (lên) → (1; 3,5)", B(Math.Abs(pe.X - 1) < 1e-9 && Math.Abs(pe.Y - 3.5) < 1e-9), 1, 0);
        check("Đường định vị tim tường: lệch 0", WallAssembly.LocationOffset(g220, LocationLines.WallCenter), 0, 1e-9);
        check("Đường định vị mặt hoàn thiện ngoài: tim lệch 0,125", WallAssembly.LocationOffset(g220, LocationLines.FinishExterior), 0.125, 1e-9);
        check("Đường định vị mặt lõi ngoài: tim lệch 0,125 − 0,015 = 0,11", WallAssembly.LocationOffset(g220, LocationLines.CoreExterior), 0.11, 1e-9);
        check("Đường định vị mặt hoàn thiện trong: −0,125", WallAssembly.LocationOffset(g220, LocationLines.FinishInterior), -0.125, 1e-9);
        check("Đường định vị tim lõi (ốp đá lệch): 0,1375 − (0,04 + 0,11) = −0,0125", WallAssembly.LocationOffset(stone, LocationLines.CoreCenter), 0.1375 - 0.15, 1e-9);

        // ---- 11. Lưu / mở (JSON) ----
        var w2 = new WallLine { Id = 21, Story = "T1", X1 = 0, Y1 = 5, X2 = 4, Y2 = 5, Type = "Gạch đặc 220 trát 2 mặt" };
        p.Walls.Add(w2);
        WallAssembly.PlaceOpening(p, w2, "S1 1200x1400", 2, flip: true);
        string json = JsonSerializer.Serialize(p, Project.JsonOptions);
        var p2 = JsonSerializer.Deserialize<Project>(json, Project.JsonOptions);
        var w2b = p2.Walls.First(q => q.Id == 21);
        check("Lưu / mở: cửa gắn tường giữ vị trí, kiểu, lật", B(w2b.Openings.Count == 1 && Math.Abs(w2b.Openings[0].Offset - 2) < 1e-9 && w2b.Openings[0].Flip && w2b.Openings[0].Type == "S1 1200x1400"), 1, 0);
        var g2 = p2.WallTypes.First(q => q.Name == "Tường 220 ốp đá granite mặt ngoài");
        check("Lưu / mở: kiểu tường giữ 4 lớp, tải 1 m²", B(g2.Layers.Count == 4 && Math.Abs(g2.WeightPerM2 - p.WallTypes.First(q => q.Name == g2.Name).WeightPerM2) < 1e-9), 1, 0);
        var pOld = JsonSerializer.Deserialize<Project>("{\"WallTypes\":[{\"Name\":\"T cũ\",\"Thickness\":0.22,\"UnitWeight\":18}],\"Walls\":[{\"Id\":1,\"Type\":\"T cũ\",\"X2\":3}]}", Project.JsonOptions);
        check("Mở file cũ (không có lớp, không có cửa): đọc được, kiểu một lớp", B(pOld.WallTypes[0].Layers.Count == 0 && pOld.Walls[0].Openings != null && pOld.OpeningTypes.Count > 0), 1, 0);
        check("File cũ: tải 1 m² kiểu một lớp = 0,22·18 + 2·0,015·18", pOld.WallTypes[0].WeightPerM2, 0.22 * 18 + 2 * 0.015 * 18, 1e-9);

        // ---- 12. Bố cục giao diện ----
        check("Bố cục mặc định: kiểu Revit", B(new UiLayoutSettings().RevitLayout && UiLayoutSettings.FromJson("{\"Density\":\"Gọn\"}").RevitLayout), 1, 0);
        var ui = new UiLayoutSettings { DesignLayout = UiLayoutSettings.LayoutClassic };
        check("Bố cục cổ điển lưu / đọc lại (ui.json)", B(!UiLayoutSettings.FromJson(ui.ToJson()).RevitLayout), 1, 0);
    }
}
