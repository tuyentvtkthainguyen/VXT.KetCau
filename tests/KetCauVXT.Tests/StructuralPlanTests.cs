using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Test 59 – Mặt bằng kết cấu chế độ Triển khai hồ sơ: lưới trục, đặt / bắt điểm / chọn cấu kiện, đếm số lượng,
/// đổi tên lan truyền, suy kích thước khi tạo cấu kiện từ mặt bằng, bản vẽ mặt bằng móng – định vị cột – các tầng (định vị dầm bằng kích thước),
/// lưu / mở hồ sơ, thứ tự bộ bản vẽ (mặt bằng → chi tiết → thống kê).</summary>
public static class StructuralPlanTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "TrienKhai");
        Directory.CreateDirectory(dir);

        // ---- lưới trục ----
        var g = new StructuralPlan { SpansX = "3.6 2*4.2", SpansY = "4.5 3" };
        check("Mặt bằng: lưới X '3.6 2*4.2' → 4 trục, trục cuối 12.0 m", g.Xs.Length * 100 + g.Xs[^1], 412.0, 1e-9);
        check("Mặt bằng: lưới Y → 3 trục A, B, C (trục C = 7.5 m)", g.Ys.Length * 100 + g.Ys[^1], 307.5, 1e-9);
        check("Mặt bằng: tên trục X 1…, Y A… (trục 27 = AA)", (StructuralPlan.NameX(0) == "1" ? 1 : 0) + (StructuralPlan.NameY(2) == "C" ? 1 : 0) + (StructuralPlan.NameY(26) == "AA" ? 1 : 0), 3, 0);
        var sn = g.Snap(3.65, 4.42);
        check("Mặt bằng: bắt điểm giao trục 2/B (3.6; 4.5)", sn.X * 10 + sn.Y, 36 + 4.5, 1e-9);
        var sn2 = g.Snap(5.93, 0.08);
        check("Mặt bằng: bắt đường trục A, làm tròn 50 mm dọc trục (5.95; 0)", sn2.X * 10 + sn2.Y, 59.5, 1e-9);

        // ---- đặt cấu kiện trên hồ sơ trống ----
        var p = new DetailingProject { ProjectName = "Test mặt bằng" };
        p.Plan.SpansX = "4.2 3.6"; p.Plan.SpansY = "4.5";
        var pl = p.Plan;
        var mong = pl.Foundation;
        check("Mặt bằng: mặc định 3 mặt bằng (Móng, Tầng 2, Mái), móng là mặt bằng móng", pl.Levels.Count * 10 + (mong != null && mong.IsFoundation ? 1 : 0), 31, 0);
        var t2 = pl.Levels[1];
        var c1 = pl.Place(p, t2, PlanKinds.Column, 0, 0, 0, 0);
        check("Mặt bằng: đặt cột chưa gán → tạo cột mới trong hồ sơ", p.Columns.Count * 10 + (c1.Member == p.Columns[0].Name ? 1 : 0), 11, 0);
        foreach (var (x, y) in new[] { (4.2, 0.0), (7.8, 0.0), (0.0, 4.5), (4.2, 4.5), (7.8, 4.5) }) pl.Place(p, t2, PlanKinds.Column, x, y, x, y, c1.Member);
        foreach (var (x, y) in new[] { (0.0, 0.0), (4.2, 0.0), (7.8, 0.0) }) pl.Place(p, pl.Levels[2], PlanKinds.Column, x, y, x, y, c1.Member);
        check("Mặt bằng: cột 2 tầng cùng vị trí đếm 1 → 6 cột", pl.Count(c1.Member), 6, 0);

        var d = pl.Place(p, t2, PlanKinds.Beam, 0, 0, 7.8, 0);
        var beam = p.Beams.First(b => b.Name == d.Member);
        var bsp = DetailShim.ParseList(beam.Spans);
        check("Mặt bằng: dầm qua 3 trục → 2 nhịp 4.2 + 3.6 m suy từ lưới", bsp.Count * 100 + bsp.Sum(), 207.8, 1e-9);
        check("Mặt bằng: cao độ đỉnh dầm = cao độ mặt bằng", beam.TopElevation, t2.Elevation, 1e-9);
        var s = pl.Place(p, t2, PlanKinds.Slab, 4.2, 0, 7.8, 4.5);
        var slab = p.Slabs.First(x => x.Name == s.Member);
        check("Mặt bằng: ô sàn 3.6 × 4.5 → Lx = 3.6, Ly = 4.5", slab.Lx * 10 + slab.Ly, 36 + 4.5, 1e-9);
        var lt = pl.Place(p, t2, PlanKinds.Lintel, 1.5, 4.5, 2.7, 4.5);
        var lint = p.Lintels.First(x => x.Name == lt.Member);
        check("Mặt bằng: lanh tô ô cửa 1.2 m", lint.Lo, 1.2, 1e-9);
        pl.Place(p, t2, PlanKinds.Canopy, 1.3, 4.5, 2.9, 5.1, lint.Name);
        check("Mặt bằng: ô văng gắn lanh tô không đếm thêm (LT = 1)", pl.Count(lint.Name), 1, 0);
        var ct = pl.Place(p, t2, PlanKinds.Stair, 0.2, 0.2, 2.6, 4.3);
        var st = p.Stairs.First(x => x.Name == ct.Member);
        check("Mặt bằng: cầu thang – ô thang 2.4 × 4.1 m suy từ hình chữ nhật", st.WellWidth * 10 + st.WellLength, 24 + 4.1, 1e-9);
        var f1 = pl.Place(p, mong, PlanKinds.Footing, 0, 0, 0, 0);
        check("Mặt bằng: đặt móng trên mặt bằng móng → tạo móng đơn", p.Footings.Count, 1, 0);
        pl.Place(p, mong, PlanKinds.Footing, 4.2, 0, 4.2, 0, f1.Member);

        // ---- chọn, số lượng ----
        var hit = StructuralPlan.HitTest(t2, 2.0, 0.05, 0.15);
        check("Mặt bằng: chọn (HitTest) gần dầm trục A → dầm", hit != null && hit.Kind == PlanKinds.Beam ? 1 : 0, 1, 0);
        var hitC = StructuralPlan.HitTest(t2, 4.25, 4.48, 0.15);
        check("Mặt bằng: chọn gần giao trục → ưu tiên cột", hitC != null && hitC.Kind == PlanKinds.Column ? 1 : 0, 1, 0);
        check("Mặt bằng: cột SL 1 ≠ 6 vị trí → cảnh báo", pl.QuantityWarnings(p).Count(w => w.StartsWith(c1.Member + ":")), 1, 0);
        int changed = pl.SyncQuantities(p);
        check("Mặt bằng: cập nhật SL theo mặt bằng → cột 6, móng 2, hết cảnh báo", p.Columns[0].Quantity * 100 + p.Footings[0].Quantity * 10 + pl.QuantityWarnings(p).Count, 620, 0);
        check("Mặt bằng: số cấu kiện đổi SL ≥ 2", changed >= 2 ? 1 : 0, 1, 0);

        // ---- đổi tên lan truyền, xoá ----
        string old = c1.Member;
        bool ok = pl.Rename(p, old, "C-A1");
        check("Mặt bằng: đổi tên cột → hồ sơ + mọi vị trí (9 vị trí)", (ok ? 1 : 0) * 100 + pl.Levels.Sum(l => l.Items.Count(i => i.Member == "C-A1")), 109, 0);
        check("Mặt bằng: đổi tên trùng tên có sẵn bị từ chối", pl.Rename(p, "C-A1", beam.Name) ? 1 : 0, 0, 0);
        int before = t2.Items.Count;
        t2.Items.Remove(hit);
        check("Mặt bằng: xoá vị trí không xoá cấu kiện khỏi hồ sơ", (t2.Items.Count == before - 1 ? 1 : 0) + (p.Beams.Any(b => b.Name == beam.Name) ? 1 : 0), 2, 0);
        t2.Items.Add(hit);

        // ---- bản vẽ mặt bằng ----
        var res = p.All.Select(m => (m, DetailingProject.RunSafe(m, p.Settings))).ToList();
        var figs = StructuralPlanDrawing.Build(p, res);
        check("Bản vẽ: mặt bằng móng + định vị cột + Tầng 2 + Mái = 4 hình", figs.Count, 4, 0);
        check("Bản vẽ: hình đầu = mặt bằng kết cấu móng", figs[0].Name == StructuralPlanDrawing.TitleFoundation ? 1 : 0, 1, 0);
        string dxf2 = figs.First(f => f.Name.StartsWith(StructuralPlanDrawing.TitleLevelPrefix) && f.Name.Contains("TẦNG 2")).W.ToDxfString();
        check("Bản vẽ tầng: có tên dầm, sàn, lanh tô, cầu thang", (dxf2.Contains(beam.Name) ? 1 : 0) + (dxf2.Contains(slab.Name) ? 1 : 0) + (dxf2.Contains(lint.Name) ? 1 : 0) + (dxf2.Contains(st.Name) ? 1 : 0), 4, 0);
        check("Bản vẽ tầng: có kích thước định vị (DIMENSION hoặc chuỗi kích thước 4200, 3600)", dxf2.Contains("4200") && dxf2.Contains("3600") ? 1 : 0, 1, 0);
        check("Bản vẽ tầng: có trục 1, 2, 3, A, B (layer TRUC)", dxf2.Contains(Lay.Truc) ? 1 : 0, 1, 0);
        string dxfM = figs[0].W.ToDxfString();
        check("Bản vẽ móng: có tên móng và lưới trục", (dxfM.Contains(f1.Member) ? 1 : 0) + (dxfM.Contains(Lay.Truc) ? 1 : 0), 2, 0);
        string dxfC = figs[1].W.ToDxfString();
        check("Bản vẽ định vị cột: có tên cột đã đổi C-A1", figs[1].Name == StructuralPlanDrawing.TitleColumns && dxfC.Contains("C-A1") ? 1 : 0, 1, 0);
        foreach (var f in figs) f.W.Save(Path.Combine(dir, "MatBangKetCau_" + Safe(f.Name) + ".dxf"));

        // dầm lệch trục → có kích thước định vị tới trục gần nhất
        var off = new DetailingProject(); off.Plan.SpansX = "4.2"; off.Plan.SpansY = "4.5";
        off.Plan.Place(off, off.Plan.Levels[1], PlanKinds.Beam, 0, 1.8, 4.2, 1.8);
        var offRes = off.All.Select(m => (m, DetailingProject.RunSafe(m, off.Settings))).ToList();
        string dOff = StructuralPlanDrawing.Build(off, offRes).Last().W.ToDxfString();
        check("Bản vẽ: dầm phụ lệch trục 1800 mm → ghi kích thước định vị 1800", dOff.Contains("1800") ? 1 : 0, 1, 0);

        // ---- hồ sơ mẫu, lưu / mở ----
        var sp = DetailingProject.Sample();
        check("Hồ sơ mẫu: có mặt bằng kết cấu, SL khớp mặt bằng (không cảnh báo)", (sp.Plan.Levels.Sum(l => l.Items.Count) > 10 ? 1 : 0) * 100 + sp.Plan.QuantityWarnings(sp).Count, 100, 0);
        string file = Path.Combine(dir, "MatBang_LuuMo" + DetailingProject.Extension);
        sp.Save(file);
        var back = DetailingProject.Load(file);
        check("Lưu / mở: giữ lưới trục, số mặt bằng và số vị trí", (back.Plan.SpansX == sp.Plan.SpansX ? 1000 : 0) + back.Plan.Levels.Count * 100 + back.Plan.Levels.Sum(l => l.Items.Count),
              1000 + sp.Plan.Levels.Count * 100 + sp.Plan.Levels.Sum(l => l.Items.Count), 0);
        var legacy = DetailingProject.FromJson("{\"ProjectName\":\"cũ\"}");
        check("Mở hồ sơ cũ không có mặt bằng → mặt bằng trống, không lỗi", legacy.Plan != null && legacy.Plan.IsEmpty ? 1 : 0, 1, 0);

        // ---- bộ bản vẽ: mặt bằng trước chi tiết, định dạng mặc định AutoCAD ----
        check("Xuất: định dạng mặc định là DWG (AutoCAD 2018)", DetailingExport.DefaultFormat == CadOutputFormat.Dwg2018 ? 1 : 0, 1, 0);
        var log = new List<string>();
        var spRes = sp.All.Select(m => (m, DetailingProject.RunSafe(m, sp.Settings))).ToList();
        var files = DetailingExport.Drawings(sp, spRes, Path.Combine(dir, "MatBang_BoBanVe"), true, log, CadOutputFormat.DxfR12);
        var names = files.Select(Path.GetFileName).ToList();
        int iPlan = names.FindIndex(n => n.Contains("MB_KetCauMong")), iDetail = names.FindIndex(n => n.Contains("_D1") || n.Contains("Dam"));
        check("Xuất: mặt bằng kết cấu đứng trước chi tiết cấu kiện", iPlan >= 0 && (iDetail < 0 || iPlan < iDetail) ? 1 : 0, 1, 0);
        check("Xuất: có mặt bằng định vị cột và thống kê vật liệu", (names.Any(n => n.Contains("MB_DinhViCot")) ? 1 : 0) + (names.Any(n => n.Contains("ThongKeVatLieu")) ? 1 : 0), 2, 0);
    }

    private static string Safe(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
