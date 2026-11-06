using KetCauVXT.Detailing;
using KetCauVXT.Drawing;

namespace KetCauVXT.Tests;

/// <summary>Test 78 – Bản vẽ kiểu Revit ở chế độ Triển khai hồ sơ: cỡ chữ in (DetailTextSizes) áp vào chữ DXF, cỡ chữ riêng khung nhìn,
/// trình duyệt khung nhìn theo mặt bằng, tạo bản vẽ (số hiệu nhảy tiếp, xoá / chèn), đổi tên, đặt khung nhìn (mỗi khung một tờ, bảng nhiều tờ),
/// đổi tỷ lệ khung nhìn, danh mục bản vẽ cập nhật, kích đúp → cấu kiện, lưu / mở, xuất theo bộ bản vẽ người dùng.</summary>
public static class RevitSheetsTests
{
    private static double B(bool b) => b ? 1 : 0;

    private static double TlHeight(DxfWriter w) =>
        w.GetPrimitives().Where(p => p.Kind == PrimKind.Text && (p.Text ?? "").StartsWith("tl: ")).Select(p => p.H).DefaultIfEmpty(0).Max();

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "TrienKhai");
        Directory.CreateDirectory(dir);

        // ---- 1. Cỡ chữ in ----
        var w0 = new DxfWriter(50);
        double th0 = w0.Th, tag0 = w0.TagTh, mark0 = w0.MarkR;
        using (DetailTextSizes.Scope(new DetailTextSizes { Note = 3.0 }))
            check("Cỡ chữ ghi chú 3,0 mm ở 1/50 → chữ mô hình 150", new DxfWriter(50).Th, 150, 1e-9);
        check("Ra khỏi phạm vi: trả lại cỡ cũ", new DxfWriter(50).Th, th0, 1e-9);
        using (DetailTextSizes.Scope(new DetailTextSizes { Factor = 1.2 }))
        {
            var w1 = new DxfWriter(50);
            check("Hệ số 1,2: nhãn thép = cỡ cũ × 1,2", w1.TagTh, tag0 * 1.2, 1e-9);
            check("Hệ số 1,2: vòng số hiệu × 1,2", w1.MarkR, mark0 * 1.2, 1e-9);
        }
        using (DetailTextSizes.Scope(new DetailTextSizes { Tag = 2.0, BubbleDiameter = 10 }))
        {
            var w2 = new DxfWriter(25);
            check("Nhãn thép 2,0 mm ở 1/25 → 50", w2.TagTh, 50, 1e-9);
            check("Đầu trục Ø10 ở 1/25 → bán kính 125", w2.BubbleR, 125, 1e-9);
        }
        check("Mặc định: không đổi gì (IsDefault)", B(new DetailTextSizes().IsDefault), 1, 0);
        using (DetailTextSizes.Scope(null)) check("Phạm vi null không đổi cỡ", new DxfWriter(50).Th, th0, 1e-9);

        // cỡ chữ hồ sơ áp vào hình cấu kiện (dòng "tl: 1/…" dùng cỡ ghi chú)
        var p = DetailingProject.Sample();
        var res = p.RunAll();
        var log = new List<string>();
        var cat0 = SheetLayoutEngine.Catalog(p, res, log);
        var beamF0 = cat0.First(f => f.Member != "" && f.Kind == "Dầm" && TlHeight(f.F.W) > 0);
        double tlDefault = TlHeight(beamF0.F.W);
        p.Settings.Text.Note = 3.0;
        var cat1 = SheetLayoutEngine.Catalog(p, res, log);
        var beamF1 = cat1.First(f => f.Id == beamF0.Id);
        check("Cỡ chữ hồ sơ 3,0: chữ \"tl:\" của hình dầm = 3,0 × tỷ lệ", TlHeight(beamF1.F.W), 3.0 * beamF1.F.W.Scale, 1e-6);
        check("Cỡ chữ hồ sơ lớn hơn mặc định", B(TlHeight(beamF1.F.W) > tlDefault + 1e-9), 1, 0);
        // cỡ chữ riêng khung nhìn (×1,5) – vẽ lại
        var big = beamF1.At(0, 1.5);
        check("Khung nhìn hệ số chữ 1,5: \"tl:\" = 4,5 × tỷ lệ", TlHeight(big.W), 4.5 * big.W.Scale, 1e-6);
        check("Hệ số chữ 1 → hình gốc (bộ đệm)", B(ReferenceEquals(beamF1.At(0, 1), beamF1.F)), 1, 0);
        // vẽ lại ở tỷ lệ khác vẫn giữ cỡ chữ hồ sơ
        if (beamF1.CanRescale)
        {
            var at100 = beamF1.At(100);
            check("Vẽ lại 1/100: \"tl:\" = 3,0 × 100", TlHeight(at100.W), 3.0 * at100.W.Scale, 1e-6);
        }
        p.Settings.Text.Note = 0;

        // ---- 2. Trình duyệt: khung nhìn theo mặt bằng ----
        var set = new DetailSheetSet();
        p.Sheets = set;
        var cat = SheetViews.WithIndexView(SheetLayoutEngine.Catalog(p, res, log, set), set);
        var map = cat.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
        var groups = SheetViews.Groups(cat);
        check("Trình duyệt: có nhóm theo mặt bằng kết cấu", B(groups.Any(g => g.Kind == "plan")), 1, 0);
        check("Trình duyệt: nhóm mặt bằng chứa hình mặt bằng + chi tiết cấu kiện", B(groups.Where(g => g.Kind == "plan").Any(g => g.Views.Any(v => v.Part == 0) && g.Views.Any(v => v.Part == 1))), 1, 0);
        var sch = groups.FirstOrDefault(g => g.Kind == "schedule");
        check("Trình duyệt: nhóm bảng thống kê có thống kê thép + danh mục bản vẽ", B(sch != null && sch.Views.Any(v => v.Id.StartsWith("TKTHEP:")) && sch.Views.Any(v => v.Id == SheetViews.IndexViewId)), 1, 0);
        check("Trình duyệt: mọi khung nhìn nằm trong một nhóm", groups.Sum(g => g.Views.Count), cat.Count, 0);

        // ---- 3. Tạo bản vẽ: số hiệu nhảy tiếp ----
        check("Bộ trống: số hiệu đầu KC-01", B(SheetViews.NextNo(set) == "KC-01"), 1, 0);
        var s1 = SheetViews.CreateSheet(set, "MAT BANG KET CAU TANG 2");
        var s2 = SheetViews.CreateSheet(set, "CHI TIET DAM");
        var s3 = SheetViews.CreateSheet(set, "CHI TIET COT", "A2", true);
        check("Tạo 3 bản vẽ: KC-01, KC-02, KC-03", B(s1.No == "KC-01" && s2.No == "KC-02" && s3.No == "KC-03"), 1, 0);
        check("Tên do người dùng đặt được khoá", B(s1.NameLocked && s1.Title == "MAT BANG KET CAU TANG 2"), 1, 0);
        check("Khổ / hướng tờ mới theo lựa chọn", B(s3.Paper == "A2" && s3.Portrait), 1, 0);
        SheetViews.DeleteSheet(set, s2, renumber: false);
        check("Xoá KC-02 (không đánh lại): tờ kế tiếp KC-04", B(SheetViews.NextNo(set) == "KC-04"), 1, 0);
        SheetViews.RenumberSequential(set);
        check("Đánh số lại liên tục: KC-01, KC-02", B(set.Sheets[0].No == "KC-01" && set.Sheets[1].No == "KC-02"), 1, 0);
        var s0 = SheetViews.CreateSheet(set, "GHI CHU CHUNG", at: 0);
        check("Chèn bản vẽ ở đầu: đánh số lại liền mạch", B(set.Sheets[0] == s0 && s0.No == "KC-01" && set.Sheets[1].No == "KC-02" && set.Sheets[2].No == "KC-03"), 1, 0);
        var gset = new DetailSheetSet { NumberStyle = DetailSheetSet.StyleGroup };
        SheetViews.CreateSheet(gset, "A"); SheetViews.CreateSheet(gset, "B");
        check("Mẫu theo nhóm: KC-1.01, KC-1.02", B(gset.Sheets[0].No == "KC-1.01" && gset.Sheets[1].No == "KC-1.02"), 1, 0);
        SheetViews.Rename(s0, "GHI CHU CHUNG KET CAU");
        check("Đổi tên bản vẽ", B(s0.Title == "GHI CHU CHUNG KET CAU" && s0.NameLocked), 1, 0);
        SheetViews.Renumber(s0, "KC-00");
        check("Sửa số hiệu tay: khoá số", B(s0.No == "KC-00" && s0.NumberLocked), 1, 0);
        SheetViews.RenumberSequential(set);
        check("Đánh số lại giữ số hiệu đã khoá", B(s0.No == "KC-00"), 1, 0);

        // ---- 4. Đặt khung nhìn ----
        var plan = cat.First(f => f.Part == 0 && f.Id.StartsWith("MB:"));
        var det = cat.First(f => f.Part == 1 && f.Kind == "Dầm");
        var tk = cat.First(f => f.Id.StartsWith("TKTHEP:"));
        var (pw, ph) = s1.Size;
        var vp = SheetViews.PlaceView(set, s1, plan, map, pw / 2, ph / 2);
        check("Đặt khung nhìn: tâm khung tại điểm thả", vp.X + plan.WAt(vp.Scale) / 2, pw / 2, 0.2);
        check("Đặt khung nhìn: tờ khoá sau khi đặt tay", B(s1.Locked), 1, 0);
        SheetViews.PlaceView(set, s3, plan, map, pw / 3, ph / 3);
        check("Mỗi khung nhìn chỉ trên MỘT tờ (kéo sang tờ khác → chuyển tờ)", set.Sheets.Count(s => s.Figures.Any(f => f.Id == plan.Id)), 1, 0);
        check("Khung nhìn giờ thuộc tờ mới", B(SheetViews.SheetOf(set, plan.Id) == s3), 1, 0);
        SheetViews.PlaceView(set, s1, tk, map, 100, 100);
        SheetViews.PlaceView(set, s3, tk, map, 100, 100);
        check("Bảng thống kê đặt được nhiều tờ", set.Sheets.Count(s => s.Figures.Any(f => f.Id == tk.Id)), 2, 0);
        SheetViews.PlaceView(set, s3, tk, map, 120, 120);
        check("Bảng không trùng trên cùng một tờ", s3.Figures.Count(f => f.Id == tk.Id), 1, 0);
        var vd = SheetViews.PlaceView(set, s1, det, map);
        check("Đặt vào chỗ trống đầu tiên (không cho điểm)", B(vd != null), 1, 0);
        check("ViewportAt: điểm giữa khung → đúng khung", B(vd != null && SheetViews.ViewportAt(s1, map, vd.X + 1, vd.Y + 1)?.Id == det.Id), 1, 0);
        // đổi tỷ lệ khung nhìn – giữ tâm
        if (det.CanRescale && vd != null)
        {
            double cx0 = vd.X + det.WAt(vd.Scale) / 2;
            double ns = SheetGeometry.StdScales.First(x => x > det.ScaleAt(vd.Scale) + 1);
            SheetViews.SetViewportScale(s1, vd, det, ns);
            check("Đổi tỷ lệ khung nhìn → hình vẽ lại đúng tỷ lệ", det.ScaleAt(vd.Scale), ns, 1e-6);
            check("Đổi tỷ lệ khung nhìn giữ tâm", vd.X + det.WAt(vd.Scale) / 2, cx0, 0.2);
        }
        check("Gỡ khung nhìn khỏi tờ", B(SheetViews.RemoveView(s1, det.Id) && SheetViews.SheetOf(set, det.Id) == null), 1, 0);
        vd = SheetViews.PlaceView(set, s1, det, map);

        // ---- 5. Danh mục bản vẽ ----
        var rows = SheetViews.IndexRows(set, map);
        check("Danh mục: một dòng mỗi bản vẽ", rows.Count, set.Sheets.Count, 0);
        var idxW = SheetViews.IndexTable(set, map);
        check("Danh mục: có tên các bản vẽ", B(set.Sheets.All(s => idxW.Texts().Contains(s.Title))), 1, 0);
        var idxVp = SheetViews.PlaceView(set, s0, map[SheetViews.IndexViewId], map, 150, 200);
        SheetViews.Rename(s3, "CHI TIET COT VA VACH");
        var comp = SheetLayoutEngine.Compose(p, set, s0, map);
        check("Danh mục trên tờ cập nhật khi đổi tên bản vẽ", B(comp.Texts().Contains("CHI TIET COT VA VACH")), 1, 0);
        var s9 = SheetViews.CreateSheet(set, "BAN VE BO SUNG");
        check("Danh mục cập nhật khi thêm bản vẽ", B(SheetLayoutEngine.Compose(p, set, s0, map).Texts().Contains("BAN VE BO SUNG")), 1, 0);
        check("Danh mục: Prune không gỡ khung danh mục", B(SheetLayoutEngine.Prune(set, cat.Where(f => f.Id != SheetViews.IndexViewId).ToList()) >= 0 && s0.Figures.Any(f => f.Id == SheetViews.IndexViewId)), 1, 0);

        // tiêu đề khung nhìn kiểu Revit
        vp = s3.Figures.First(f => f.Id == plan.Id);
        vp.ShowTitle = true; vp.ViewTitle = "MB TANG 2";
        var c3 = SheetLayoutEngine.Compose(p, set, s3, map);
        check("Tiêu đề khung nhìn (tên + tỷ lệ) dưới hình", B(c3.Texts().Any(t => t.StartsWith("MB TANG 2") && t.Contains("1/"))), 1, 0);
        vp.TextFactor = 1.5;
        var tsh = SheetViews.CreateSheet(set, "THU CO CHU");
        var tvp = SheetViews.PlaceView(set, tsh, det, map, 300, 300);
        double TlOf(DxfWriter w) => w.GetPrimitives().Where(q => q.Kind == PrimKind.Text && (q.Text ?? "").StartsWith("tl: ")).Select(q => q.H).DefaultIfEmpty(0).Max();
        double tl10 = TlOf(SheetLayoutEngine.Compose(p, set, tsh, map));
        tvp.TextFactor = 1.5;
        double tl15 = TlOf(SheetLayoutEngine.Compose(p, set, tsh, map));
        check("Cỡ chữ riêng khung nhìn ×1,5 trên tờ ghép", tl10 > 0 ? tl15 / tl10 : 0, 1.5, 0.02);

        // ---- 6. Kích đúp → cấu kiện / mặt bằng ----
        var tgt = SheetViews.TargetOf(det);
        var mem = SheetViews.MemberOf(p, tgt);
        check("Kích đúp khung nhìn chi tiết → cấu kiện", B(tgt.Kind == "member" && mem != null && mem.Name == det.Member), 1, 0);
        check("Kích đúp khung nhìn mặt bằng → mặt bằng", B(SheetViews.TargetOf(plan).Kind == "plan"), 1, 0);
        check("Kích đúp bảng → không sửa (bảng)", B(SheetViews.TargetOf(tk).Kind == "schedule"), 1, 0);

        // ---- 7. Lưu / mở ----
        p.Settings.Text.Tag = 2.2;
        var p2 = DetailingProject.FromJson(p.ToJson());
        var s3b = p2.Sheets.Sheets.First(s => s.Title == "CHI TIET COT VA VACH");
        var vp2 = s3b.Figures.First(f => f.Id == plan.Id);
        check("Lưu / mở: khung nhìn giữ tên, tiêu đề, hệ số chữ", B(vp2.ViewTitle == "MB TANG 2" && vp2.ShowTitle && Math.Abs(vp2.TextFactor - 1.5) < 1e-9), 1, 0);
        check("Lưu / mở: cỡ chữ in của hồ sơ", p2.Settings.Text.Tag, 2.2, 1e-9);
        check("Lưu / mở: số bản vẽ + khung danh mục", B(p2.Sheets.Sheets.Count == set.Sheets.Count && p2.Sheets.Sheets.Any(s => s.Figures.Any(f => f.Id == SheetViews.IndexViewId))), 1, 0);
        var pOld = DetailingProject.FromJson("{\"ProjectName\":\"Cu\"}");
        check("Mở hồ sơ cũ (không có cỡ chữ / bản vẽ)", B(pOld.Settings != null && (pOld.Settings.Text == null || pOld.Settings.Text.IsDefault)), 1, 0);

        // ---- 8. Xuất theo bộ bản vẽ người dùng ----
        var (eset, ecat) = SheetLayoutEngine.ExportSet(p, res, log);
        check("Xuất: dùng bộ bản vẽ người dùng (tên bản vẽ đặt tay)", B(eset.Sheets.Any(s => s.Title == "MAT BANG KET CAU TANG 2") && eset.Sheets.Any(s => s.Title == "CHI TIET COT VA VACH")), 1, 0);
        check("Xuất: khung nhìn ở đúng tờ người dùng đặt", B(eset.Sheets.First(s => s.Title == "CHI TIET COT VA VACH").Figures.Any(f => f.Id == plan.Id)), 1, 0);
        try
        {
            var emap = ecat.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
            var sh = eset.Sheets.First(s => s.Title == "CHI TIET COT VA VACH");
            File.WriteAllText(Path.Combine(dir, "BanVeRevit_Sheet.dxf"), SheetLayoutEngine.Compose(p, eset, sh, emap).ToDxfString());
            check("Xuất DXF bản vẽ kiểu Revit", B(File.Exists(Path.Combine(dir, "BanVeRevit_Sheet.dxf"))), 1, 0);
        }
        catch (Exception ex) { check("Xuất DXF bản vẽ kiểu Revit: " + ex.Message, 0, 1, 0); }
        var st = SheetViews.Stats(set, cat);
        check("Thống kê: số tờ", st.Sheets, set.Sheets.Count, 0);
        check("Thống kê: đã đặt + chưa đặt = số khung nhìn (không kể bảng)", st.Unplaced + cat.Count(f => !SheetViews.IsSchedule(f) && SheetViews.SheetOf(set, f.Id) != null), cat.Count(f => !SheetViews.IsSchedule(f)), 0);
    }
}
