using KetCauVXT.Ui;

namespace KetCauVXT.Tests;

/// <summary>77. Vừa màn hình mặt bằng (đúng cỡ vùng vẽ thật) + đặt nhãn theo ưu tiên, không chồng.</summary>
public static class PlanFitLabelTests
{
    private static double B(bool v) => v ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---- 1. Vừa màn hình: nhà 5 × 13,5 m (+ 1,2 m bong bóng trục mỗi bên) trong vùng vẽ 720 × 480 px ----
        double x0 = -1.2, x1 = 6.2, y0 = -1.2, y1 = 14.7;
        var f = PlanFit.Fit(x0, x1, y0, y1, 720, 480);
        double uh = 480 * (1 - 2 * PlanFit.DefaultMarginFrac) - 2 * PlanFit.DefaultPadPx;
        check("Vừa màn hình: tỷ lệ theo chiều hạn chế (cao)", f.Scale, uh / (y1 - y0), 1e-9);
        check("Vừa màn hình: nhà 13,5 m ≥ 15 px/m trên vùng 480 px (trước đây ≈ 11)", B(f.Scale >= 15), 1, 0);
        // tâm phạm vi nằm giữa vùng vẽ
        double scx = f.Ox + (x0 + x1) / 2 * f.Scale, scy = f.Oy - (y0 + y1) / 2 * f.Scale;
        check("Vừa màn hình: tâm X giữa vùng vẽ", scx, 360, 1e-6);
        check("Vừa màn hình: tâm Y giữa vùng vẽ", scy, 240, 1e-6);
        // phạm vi nằm trọn trong vùng vẽ, có lề
        double top = f.Oy - y1 * f.Scale, bottom = f.Oy - y0 * f.Scale, left = f.Ox + x0 * f.Scale, right = f.Ox + x1 * f.Scale;
        check("Vừa màn hình: lề trên ≥ lề cố định", B(top >= PlanFit.DefaultPadPx - 1e-6), 1, 0);
        check("Vừa màn hình: lề dưới ≥ lề cố định", B(480 - bottom >= PlanFit.DefaultPadPx - 1e-6), 1, 0);
        check("Vừa màn hình: trong bề rộng", B(left >= 0 && right <= 720), 1, 0);

        // vùng vẽ lớn hơn (thu gọn khung bên) → tỷ lệ lớn hơn
        var f2 = PlanFit.Fit(x0, x1, y0, y1, 1200, 800);
        check("Vùng vẽ lớn hơn → tỷ lệ lớn hơn", B(f2.Scale > f.Scale), 1, 0);
        // nhà dài theo X: chiều hạn chế là bề rộng
        var f3 = PlanFit.Fit(0, 40, 0, 5, 720, 480);
        double uw = 720 * (1 - 2 * PlanFit.DefaultMarginFrac) - 2 * PlanFit.DefaultPadPx;
        check("Nhà dài theo X: tỷ lệ theo bề rộng", f3.Scale, uw / 40, 1e-9);
        // phạm vi đảo ngược vẫn đúng
        var f4 = PlanFit.Fit(x1, x0, y1, y0, 720, 480);
        check("Phạm vi đảo ngược: cùng tỷ lệ", f4.Scale, f.Scale, 1e-9);
        // vùng vẽ chưa dựng xong
        check("Vùng vẽ 0 × 0: không vừa (chờ)", B(!PlanFit.Fit(0, 10, 0, 10, 0, 0).Ok), 1, 0);
        // vùng vẽ rất nhỏ: vẫn giữ ≥ 30 % vùng vẽ
        var f5 = PlanFit.Fit(0, 10, 0, 10, 200, 160);
        check("Vùng vẽ nhỏ: tỷ lệ > 0 (giữ ≥ 30 % vùng)", B(f5.Scale >= 0.3 * 160 / 10 - 1e-9), 1, 0);
        // giới hạn
        check("Phóng tối đa 400 px/m", PlanFit.Fit(0, 0.01, 0, 0.01, 4000, 4000).Scale, 400, 1e-9);

        // ---- 2. Tự vừa lại khi đổi cỡ vùng vẽ ----
        check("Đổi cỡ, chưa phóng tay → vừa lại", B(PlanFit.RefitOnResize(false, 720, 480, 900, 600)), 1, 0);
        check("Đổi cỡ, đã phóng tay → giữ nguyên", B(!PlanFit.RefitOnResize(true, 720, 480, 900, 600)), 1, 0);
        check("Không đổi cỡ → không vừa lại", B(!PlanFit.RefitOnResize(false, 720, 480, 720, 480)), 1, 0);
        check("Vùng vẽ ẩn (0) → không vừa", B(!PlanFit.RefitOnResize(false, 720, 480, 0, 0)), 1, 0);

        // ---- 3. Phân loại ưu tiên nhãn ----
        check("Ưu tiên: trục = 0", PlanLabelPlacer.GuessPriority("A", true), 0, 0);
        check("Ưu tiên: cột C2 = 1", PlanLabelPlacer.GuessPriority("C2", false), 1, 0);
        check("Ưu tiên: dầm D7 D22x40 = 2", PlanLabelPlacer.GuessPriority("D7 D22x40", false), 2, 0);
        check("Ưu tiên: dầm DT3-P1 = 2", PlanLabelPlacer.GuessPriority("DT3-P1 D22x30", false), 2, 0);
        check("Ưu tiên: sàn ST3-1 = 3", PlanLabelPlacer.GuessPriority("ST3-1", false), 3, 0);
        check("Ưu tiên: chữ khác (CẦU THANG) = 4", PlanLabelPlacer.GuessPriority("CẦU THANG", false), 4, 0);

        // ---- 4. Đặt nhãn: không chồng, ưu tiên cao đặt trước ----
        var pl = new PlanLabelPlacer();
        var axis = pl.Add(new PlanLabelPlacer.Box(100, 100, 30, 12), 0, movable: false);
        var slab = pl.Add(new PlanLabelPlacer.Box(100, 100, 40, 12), 3);   // thêm trước nhưng ưu tiên thấp
        var col = pl.Add(new PlanLabelPlacer.Box(100, 100, 20, 12), 1);
        var beam = pl.Add(new PlanLabelPlacer.Box(100, 100, 60, 12), 2);
        int dropped = pl.Solve();
        check("Đặt nhãn: không nhãn nào bị bỏ (đủ chỗ quanh)", dropped, 0, 0);
        check("Nhãn trục giữ nguyên vị trí", B(axis.Placed is { } ap && ap.X == 100 && ap.Y == 100), 1, 0);
        var all = new[] { axis, slab, col, beam };
        bool overlap = false;
        for (int i = 0; i < all.Length; i++)
            for (int j = i + 1; j < all.Length; j++)
                if (all[i].Placed is { } a && all[j].Placed is { } b && a.Hits(b, 0)) overlap = true;
        check("Đặt nhãn: không cặp nào chồng nhau", B(!overlap), 1, 0);
        // cột (ưu tiên 1) được thử trước sàn (3): cột nằm ở vị trí ứng viên sớm hơn sàn
        int idxCol = IndexOf(col), idxSlab = IndexOf(slab);
        check("Ưu tiên: cột lấy vị trí gần hơn sàn", B(idxCol <= idxSlab), 1, 0);

        // vật cản (thân cột) – nhãn không đè
        var pl2 = new PlanLabelPlacer();
        pl2.Obstacle(new PlanLabelPlacer.Box(0, 0, 50, 50));
        var lab = pl2.Add(new PlanLabelPlacer.Box(10, 10, 20, 10), 2);
        pl2.Solve();
        check("Vật cản: nhãn không đè thân cột", B(lab.Placed is { } lp && !lp.Hits(new PlanLabelPlacer.Box(0, 0, 50, 50), 0)), 1, 0);

        // hết chỗ: nhãn ưu tiên thấp bị bỏ, nhãn ưu tiên cao giữ
        var pl3 = new PlanLabelPlacer();
        pl3.Obstacle(new PlanLabelPlacer.Box(-1000, -1000, 2000, 990));   // trên
        pl3.Obstacle(new PlanLabelPlacer.Box(-1000, 22, 2000, 990));      // dưới
        pl3.Obstacle(new PlanLabelPlacer.Box(-1000, -10, 990, 40));       // trái
        pl3.Obstacle(new PlanLabelPlacer.Box(60, -10, 990, 40));          // phải → chỉ còn ô (−10..60, −10..22)
        var hi = pl3.Add(new PlanLabelPlacer.Box(0, 0, 40, 12), 1);
        var lo = pl3.Add(new PlanLabelPlacer.Box(0, 0, 40, 12), 4);
        int d3 = pl3.Solve();
        check("Hết chỗ: bỏ 1 nhãn", d3, 1, 0);
        check("Hết chỗ: giữ nhãn ưu tiên cao (cột)", B(hi.Placed != null && lo.Placed == null), 1, 0);
        check("Danh sách nhãn bị bỏ (để hiện chú giải khi rê chuột)", pl3.Dropped.Count(), 1, 0);

        // nhãn xoay dọc (dầm theo phương Y): hộp cao > rộng vẫn đặt được, không chồng nhãn ngang
        var pl4 = new PlanLabelPlacer();
        var hBeam = pl4.Add(new PlanLabelPlacer.Box(100, 100, 70, 14), 2);
        var vBeam = pl4.Add(new PlanLabelPlacer.Box(120, 80, 14, 70), 2);
        pl4.Solve();
        check("Nhãn dầm dọc (xoay 90°) không chồng nhãn dầm ngang", B(hBeam.Placed is { } h1 && vBeam.Placed is { } v1 && !h1.Hits(v1, 0)), 1, 0);
        check("Nhãn xoay giữ kích thước hộp dọc", B(vBeam.Placed is { } v2 && v2.H > v2.W), 1, 0);

        // nhiều nhãn chồng một chỗ (cảnh "chồng ở giữa"): 12 nhãn cùng tâm → không chồng nhau
        var pl5 = new PlanLabelPlacer();
        var many = Enumerable.Range(0, 12).Select(i => pl5.Add(new PlanLabelPlacer.Box(300, 300, 50, 12), 2 + i % 3)).ToList();
        pl5.Solve();
        var placed = many.Where(m => m.Placed != null).Select(m => m.Placed.Value).ToList();
        bool ov5 = false;
        for (int i = 0; i < placed.Count; i++) for (int j = i + 1; j < placed.Count; j++) if (placed[i].Hits(placed[j], 0)) ov5 = true;
        check("12 nhãn cùng chỗ: các nhãn đặt được không chồng nhau", B(!ov5), 1, 0);
        check("12 nhãn cùng chỗ: đặt được ≥ 8 nhãn", B(placed.Count >= 8), 1, 0);
    }

    private static int IndexOf(PlanLabelPlacer.Item it)
    {
        if (it.Placed is not { } p) return int.MaxValue;
        int k = 0;
        foreach (var c in PlanLabelPlacer.Candidates(it.Want))
        {
            if (Math.Abs(c.X - p.X) < 1e-9 && Math.Abs(c.Y - p.Y) < 1e-9) return k;
            k++;
        }
        return int.MaxValue;
    }
}
