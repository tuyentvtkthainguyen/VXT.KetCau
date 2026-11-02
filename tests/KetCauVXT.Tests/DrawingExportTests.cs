using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng: tên cấu kiện kèm số lượng trong bảng thống kê "D2-1 (SL=2)"; xuất bản vẽ gộp 1 file / tách rời (bộ KC-xx, tờ dàn trang);
/// hiệu chỉnh bản vẽ sau khi xuất (dời nhãn, sửa chữ, xoá nét – áp lại sau khi thiết kế lại, qua lưu/mở JSON; báo chỉnh sửa không còn áp dụng);
/// khoá bản vẽ bằng DXF đã sửa trong CAD, bỏ khoá. check(tên, giá trị, kỳ vọng, sai số).</summary>
public static class DrawingExportTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Console.WriteLine("\n=== XUẤT BẢN VẼ: GỘP / TÁCH, HIỆU CHỈNH SAU KHI XUẤT, KHOÁ BẢN VẼ ===");
        double B(bool v) => v ? 1 : 0;
        string root = Path.Combine(outDir, "XuatBanVe");
        void Clean(string dir) { if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir)) File.Delete(f); Directory.CreateDirectory(dir); }

        Project NewP()
        {
            var p = ModelBuilder.NewProject("XuatBanVe_Mau");
            p.Location = "Thái Nguyên";
            p.Designer = "Vũ Xuân Tuyến";
            ModelBuilder.SetGrid(p, new[] { 4.0, 4.0 }, new[] { 5.0 });
            ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
            ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
            {
                ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5
            });
            return p;
        }

        var p = NewP();
        var r = FrameAnalysis.Run(p);
        var d = ConcreteDesigner.Design(p, r);

        // ================= 1. TÊN CẤU KIỆN (SL=n) TRONG BẢNG THỐNG KÊ =================
        {
            var dq = ConcreteDesigner.Design(p, r);
            var beam = dq.Beams.FirstOrDefault(b => b.Quantity == 2 && b.Bars.Count > 0);
            if (beam == null) { beam = dq.Beams.First(b => b.Bars.Count > 0); beam.Quantity = 2; Console.WriteLine("   · không có dầm SL=2 tự nhiên – đặt SL=2 cho " + beam.Name); }
            string lbl = beam.Name + " (SL=2)";
            var rows = RebarSchedule.BuildRows(dq).Where(x => x.Category == "Dầm" && x.Member == beam.Name).ToList();
            check("Thống kê: dầm 2 cấu kiện giống nhau – mọi hàng ghi \"tên (SL=2)\"", B(rows.Count > 0 && rows.All(x => x.MemberLabel == lbl && x.MemberCount == 2)), 1, 0);
            check("Thống kê: tổng số thanh = 2 × số thanh 1 CK", B(rows.All(x => x.BarsTotal == 2 * x.BarsPerMember)), 1, 0);
            var figTexts = RebarSchedule.BuildFigures(dq, 1).SelectMany(f => f.W.Texts()).ToList();
            check("Thống kê DXF: ô TÊN CẤU KIỆN có \"" + lbl + "\"", B(figTexts.Any(t => t.Contains(lbl))), 1, 0);
            var titles = BeamDrawing.BuildAll(p, new[] { beam }).SelectMany(f => f.W.Texts()).ToList();
            check("Thống kê: cùng số lượng với tên hình dầm \"(SL=2; L=…)\"", B(titles.Any(t => t.StartsWith(beam.Name + " (SL=2;", StringComparison.Ordinal))), 1, 0);
            var xr = ExcelReport.RebarSchedule(dq).Where(x => x.Member == beam.Name).ToList();
            check("Excel \"Thong ke thep\": cột cấu kiện \"tên (SL=2)\"", B(xr.Count > 0 && xr.All(x => x.MemberLabel == lbl)), 1, 0);
            var shop = RebarShop.Build(p, dq, ShopOptions.From(p.RebarPrefs));
            var sb = shop.Bars.Where(x => x.Member == beam.Name && x.Category == "Dầm").ToList();
            check("Gia công thép: tên cấu kiện \"tên (SL=2)\"", B(sb.Count > 0 && sb.All(x => x.MemberLabel == lbl)), 1, 0);
            var shopTexts = RebarShopDrawing.BuildFigures(p, dq, "Dầm", shop).SelectMany(f => f.W.Texts()).ToList();
            check("Gia công thép DXF: ô TÊN CẤU KIỆN có \"(SL=2)\"", B(shopTexts.Any(t => t.Contains(lbl))), 1, 0);
            var stairRow = new ScheduleRow { Member = "Bản thang CT1", MemberCount = 2 };
            check("Thống kê cầu thang: \"Bản thang CT1 (SL=2)\"", B(stairRow.MemberLabel == "Bản thang CT1 (SL=2)"), 1, 0);
            check("Thống kê: số lượng 0 → (SL=1)", B(RebarSchedule.MemberLabel("C1", 0) == "C1 (SL=1)"), 1, 0);
        }

        // ================= 2. XUẤT BỘ BẢN VẼ: TÁCH RỜI / GỘP 1 FILE =================
        var items = DrawingSet.BuildDrawings(p, d);
        int nItems = items.Count(i => i.W != null);
        string dSplit = Path.Combine(root, "TachRoi");
        Clean(dSplit);
        var es = DrawingSet.Export(p, d, dSplit, DrawingExportOptions.Split);
        check("Xuất tách rời: số file = số bản vẽ", es.Files.Count, nItems, 0);
        check("Xuất tách rời: tên file mặc định giữ nguyên (KC-01_MatBangKetCau.dxf)", B(es.Files.Any(f => Path.GetFileName(f) == "KC-01_MatBangKetCau.dxf")), 1, 0);
        check("Xuất tách rời = ExportAll cũ", DrawingSet.ExportAll(p, d, Path.Combine(root, "TachRoi_Cu")).Count, nItems, 0);
        string dPat = Path.Combine(root, "TachRoi_MauTen");
        Clean(dPat);
        var ep = DrawingSet.Export(p, d, dPat, new DrawingExportOptions { Mode = DrawingExportMode.Split, FilePattern = "{stt}_{so}" });
        check("Xuất tách rời theo mẫu tên {stt}_{so}: 01_KC-01.dxf", B(ep.Files.Any(f => Path.GetFileName(f) == "01_KC-01.dxf")), 1, 0);

        string dOne = Path.Combine(root, "Gop");
        Clean(dOne);
        var e1 = DrawingSet.Export(p, d, dOne, DrawingExportOptions.SingleFile("{duan}_BoBanVe"));
        check("Xuất gộp: đúng 1 file", e1.Files.Count, 1, 0);
        check("Xuất gộp: tên file theo mẫu {duan}", B(e1.Files.Count == 1 && Path.GetFileName(e1.Files[0]) == "XuatBanVe_Mau_BoBanVe.dxf"), 1, 0);
        string t1 = e1.Files.Count == 1 ? File.ReadAllText(e1.Files[0]) : "";
        check("Xuất gộp: có tên mọi bản vẽ (số hiệu – tên)", B(items.Where(i => i.W != null).All(i => t1.Contains(DxfWriter.Encode(i.Number + " – " + i.Title)))), 1, 0);
        check("Xuất gộp: có khung tên từng bản vẽ (số hiệu)", B(items.Where(i => i.W != null).All(i => t1.Contains(DxfWriter.Encode(i.Number)))), 1, 0);
        check("Xuất gộp: số vùng = số bản vẽ", e1.Regions.Count, nItems, 0);
        int overlaps = 0;
        for (int i = 0; i < e1.Regions.Count; i++)
            for (int j = i + 1; j < e1.Regions.Count; j++)
                if (e1.Regions[i].Region.Intersects(e1.Regions[j].Region)) overlaps++;
        check("Xuất gộp: các bản vẽ không chồng nhau", overlaps, 0, 0);
        check("Xuất gộp: DXF hợp lệ (EOF, chữ mã hoá \\U+)", B(t1.Contains("EOF") && !t1.Any(ch => ch > 127)), 1, 0);

        // ================= 3. TỜ DÀN TRANG: GỘP / TÁCH; PDF xử lý ở App =================
        p.Sheets = SheetBuilder.DefaultSet(p, d, r);
        string dSh = Path.Combine(root, "ToGop");
        Clean(dSh);
        var esh = SheetBuilder.ExportDxf(p, r, d, dSh, p.Sheets, DrawingExportOptions.SingleFile("CacTo"));
        check("Tờ dàn trang gộp: 1 file", esh.Files.Count, 1, 0);
        string tsh = esh.Files.Count == 1 ? File.ReadAllText(esh.Files[0]) : "";
        check("Tờ dàn trang gộp: có số hiệu mọi tờ", B(p.Sheets.All(s => tsh.Contains(DxfWriter.Encode(s.Number)))), 1, 0);
        int ovs = 0;
        for (int i = 0; i < esh.Regions.Count; i++)
            for (int j = i + 1; j < esh.Regions.Count; j++)
                if (esh.Regions[i].Region.Intersects(esh.Regions[j].Region)) ovs++;
        check("Tờ dàn trang gộp: các tờ đặt cạnh nhau không chồng", ovs, 0, 0);
        string dShS = Path.Combine(root, "ToTach");
        Clean(dShS);
        check("Tờ dàn trang tách rời: mỗi tờ một file", SheetBuilder.ExportDxf(p, r, d, dShS, p.Sheets, DrawingExportOptions.Split).Files.Count, p.Sheets.Count, 0);

        // ================= 4. HIỆU CHỈNH SAU KHI XUẤT =================
        var beamItem = items.First(i => i.Key.StartsWith("DS:KC-04", StringComparison.Ordinal) && i.W != null);
        string key = beamItem.Key;
        DxfWriter baseW;
        using (DrawingOverlay.Bypass()) baseW = DrawingSet.BuildDrawings(p, d, k => k == key).First(i => i.Key == key).W;
        var refs = baseW.EntityRefs();
        var tagText = refs.First(e => e.Type == "TEXT" && e.Group != null && e.Group.StartsWith("TAG|", StringComparison.Ordinal) && e.Owner != null);
        var leader = refs.FirstOrDefault(e => e.Type == "LINE" && e.Role == "leader" && e.Group == tagText.Group && e.Owner == tagText.Owner);
        var title = refs.First(e => e.Type == "TEXT" && e.Group != null && e.Group.StartsWith("TITLE|", StringComparison.Ordinal) && e.Owner != null && (e.Text ?? "").Contains("(SL="));
        var line = refs.First(e => (e.Type == "LINE" || e.Type == "POLYLINE") && e.Group == null && e.Owner != null && e.Layer != Lay.Chu && e.Layer != Lay.KhungTen);
        string newTitle = "TÊN HÌNH ĐÃ SỬA – KIỂM TRA";
        DrawingOverlay.RecordMove(p, key, tagText, true, 500, 300, baseW.Scale);
        DrawingOverlay.RecordText(p, key, title, newTitle);
        DrawingOverlay.RecordDelete(p, key, line, false);
        check("Hiệu chỉnh: ghi 3 chỉnh sửa (dời nhãn, sửa chữ, xoá nét)", p.DrawingEdits.Count, 3, 0);
        check("Hiệu chỉnh: chỉnh sửa gắn với hình chủ (BEAM:…)", B(p.DrawingEdits.All(x => x.Owner.StartsWith("BEAM:", StringComparison.Ordinal))), 1, 0);

        void Verify(string what, Project pp, DesignSummary dd)
        {
            var rep = new OverlayReport();
            DrawingItem it;
            using (DrawingOverlay.Collect(rep)) it = DrawingSet.BuildDrawings(pp, dd, k => k == key).First(i => i.Key == key);
            var er = it.W.EntityRefs();
            var mt = er.FirstOrDefault(e => e.Owner == tagText.Owner && e.Key == tagText.Key);
            check($"Hiệu chỉnh ({what}): nhãn thép dời +500 theo X", mt != null ? mt.X - tagText.X : -1, 500, 1e-6);
            check($"Hiệu chỉnh ({what}): nhãn thép dời +300 theo Y", mt != null ? mt.Y - tagText.Y : -1, 300, 1e-6);
            if (leader != null)
            {
                var ml = er.FirstOrDefault(e => e.Owner == leader.Owner && e.Key == leader.Key);
                check($"Hiệu chỉnh ({what}): đường dẫn nhãn giữ điểm tại thanh", ml != null ? Math.Abs(ml.X - leader.X) + Math.Abs(ml.Y - leader.Y) : -1, 0, 1e-6);
            }
            check($"Hiệu chỉnh ({what}): chữ tên hình đã sửa", B(it.W.Texts().Contains(newTitle)), 1, 0);
            check($"Hiệu chỉnh ({what}): nét đã xoá", er.Count(e => e.Owner == line.Owner && e.Key == line.Key), 0, 0);
            check($"Hiệu chỉnh ({what}): không có chỉnh sửa lỗi thời", rep.Stale.Count, 0, 0);
        }
        Verify("xuất lần đầu", p, d);
        string dEd = Path.Combine(root, "HieuChinh");
        Clean(dEd);
        var eEd = DrawingSet.Export(p, d, dEd, DrawingExportOptions.Split);
        string fBeam = eEd.Files.First(f => Path.GetFileNameWithoutExtension(f) == key.Substring(3));
        check("Hiệu chỉnh: file DXF xuất có chữ đã sửa", B(File.ReadAllText(fBeam).Contains(DxfWriter.Encode(newTitle))), 1, 0);
        var d2 = ConcreteDesigner.Design(p, r);   // thiết kế lại → dựng lại bản vẽ
        Verify("sau khi thiết kế lại", p, d2);
        string pj = Path.Combine(Path.GetTempPath(), "kcvxt_drawedit_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".kcvxt");
        p.Save(pj);
        var p2 = Project.Load(pj);
        File.Delete(pj);
        check("Hiệu chỉnh: lưu/mở JSON giữ 3 chỉnh sửa", p2.DrawingEdits.Count, 3, 0);
        Verify("sau lưu/mở JSON", p2, d2);
        // chỉnh sửa trên hình dầm cũng thấy ở tờ dàn trang (cùng khoá hình)
        string bname = title.Owner.Substring("BEAM:".Length);
        var sheetFigs = SheetBuilder.Figures(p, r, d, new SheetView { Kind = SheetViewKind.BeamDetail, Param = bname });
        check("Hiệu chỉnh: tờ dàn trang chi tiết dầm thấy chữ đã sửa", B(sheetFigs.Any(f => f.W.Texts().Contains(newTitle))), 1, 0);
        // đối tượng thêm (chữ ghi chú) theo mm giấy
        var an = DrawingOverlay.AddAnnotation(p, key, SheetBuilder.NewText(20, 20, "GHI CHÚ THÊM VXT", 3.5));
        DrawingItem itA;
        itA = DrawingSet.BuildDrawings(p, d, k => k == key).First(i => i.Key == key);
        var at = itA.W.EntityRefs().FirstOrDefault(e => e.Type == "TEXT" && e.Text == "GHI CHÚ THÊM VXT");
        check("Hiệu chỉnh: chữ thêm cao 3,5 mm khi in (× tỷ lệ)", at?.Height ?? 0, 3.5 * itA.W.Scale, 1e-6);
        p.DrawingNotes.Remove(an);
        // chỉnh sửa không còn khớp được báo, không mất âm thầm
        var bad = new DrawingEdit { Owner = tagText.Owner, Target = "TEXT|CHU|999999|999999|KHONG_CO", Op = DrawingEditOp.Delete, Drawing = key };
        p.DrawingEdits.Add(bad);
        string dBad = Path.Combine(root, "HieuChinhLoi");
        Clean(dBad);
        var eb = DrawingSet.Export(p, d, dBad, DrawingExportOptions.Split);
        check("Hiệu chỉnh: chỉnh sửa không khớp được liệt kê", B(eb.Report.Stale.Contains(bad)), 1, 0);
        check("Hiệu chỉnh: thông báo \"không còn áp dụng được\"", B(eb.Messages.Any(m => m.Contains("không còn áp dụng được"))), 1, 0);
        check("Hiệu chỉnh: chỉnh sửa không khớp vẫn giữ trong dự án", B(p.DrawingEdits.Contains(bad)), 1, 0);
        DrawingOverlay.RemoveEdits(p, new[] { bad });
        // hoàn tác bằng ảnh chụp lớp chỉnh sửa
        string snap = DrawingOverlay.Snapshot(p);
        DrawingOverlay.ClearEdits(p, key);
        check("Hiệu chỉnh: khôi phục bản gốc xoá chỉnh sửa của bản vẽ", p.DrawingEdits.Count, 0, 0);
        DrawingOverlay.Restore(p, snap);
        check("Hiệu chỉnh: hoàn tác (ảnh chụp) trả lại 3 chỉnh sửa", p.DrawingEdits.Count, 3, 0);

        // ================= 5. NẠP LẠI DXF ĐÃ SỬA TRONG CAD → KHOÁ =================
        const string marker = "BAN VE DA SUA TRONG CAD VXT-7731";
        string dCad = Path.Combine(root, "BanSuaCAD");
        Clean(dCad);
        string cadFile = Path.Combine(dCad, key.Substring(3) + ".dxf");
        var cw = new DxfWriter(beamItem.Scale);
        cw.Text(Lay.Chu, 0, 0, 5 * beamItem.Scale, marker);
        cw.Text(Lay.Chu, 0, -10 * beamItem.Scale, 3 * beamItem.Scale, "Đã sửa trong AutoCAD", 30, 1, 2);
        cw.Rect(Lay.KhungTen, -20 * beamItem.Scale, -40 * beamItem.Scale, 400 * beamItem.Scale, 20 * beamItem.Scale);
        cw.Circle(Lay.KyHieu, 100 * beamItem.Scale, -20 * beamItem.Scale, 5 * beamItem.Scale);
        cw.Save(cadFile);
        string gh;
        using (DrawingOverlay.IgnoreFrozen()) gh = DrawingSet.BuildDrawings(p, d, k => k == key).First(i => i.Key == key).W.ContentHash();
        var fz = DrawingOverlay.Freeze(p, key, beamItem.Title, cadFile, gh, beamItem.Scale, out string ferr);
        check("Khoá bản vẽ: nạp DXF đã sửa" + (ferr != null ? " (" + ferr + ")" : ""), B(fz != null), 1, 0);
        var fwr = DrawingOverlay.FrozenWriter(fz);
        check("Khoá bản vẽ: đọc lại chữ, chữ xoay 30°", B(fwr != null && fwr.Texts().Contains(marker) && fwr.EntityRefs().Any(e => e.Type == "TEXT" && Math.Abs(e.Rotation - 30) < 1e-6)), 1, 0);
        string dFz = Path.Combine(root, "KhoaTach");
        Clean(dFz);
        var ef = DrawingSet.Export(p, d, dFz, DrawingExportOptions.Split);
        string fFz = ef.Files.First(f => Path.GetFileNameWithoutExtension(f) == key.Substring(3));
        string tFz = File.ReadAllText(fFz);
        check("Khoá bản vẽ: xuất tách rời dùng file đã nạp (có chữ đánh dấu)", B(tFz.Contains(marker) && !tFz.Contains(DxfWriter.Encode(newTitle))), 1, 0);
        check("Khoá bản vẽ: thông báo đã khoá", B(ef.Messages.Any(m => m.Contains("đã khoá"))), 1, 0);
        check("Khoá bản vẽ: mô hình chưa đổi → không báo thay đổi", B(!ef.Messages.Any(m => m.Contains("mô hình thay đổi"))), 1, 0);
        string dFz1 = Path.Combine(root, "KhoaGop");
        Clean(dFz1);
        var ef1 = DrawingSet.Export(p, d, dFz1, DrawingExportOptions.SingleFile("BoBanVe_Khoa"));
        string tFz1 = File.ReadAllText(ef1.Files[0]);
        check("Khoá bản vẽ: xuất gộp có chữ đánh dấu + nhãn [ĐÃ KHOÁ]", B(tFz1.Contains(marker) && tFz1.Contains(DxfWriter.Encode("[ĐÃ KHOÁ]"))), 1, 0);
        fz.BaseHash = "KHAC";   // giả lập mô hình đổi sau khi khoá
        var ef2 = DrawingSet.Export(p, d, dFz1, DrawingExportOptions.SingleFile("BoBanVe_Khoa"));
        check("Khoá bản vẽ: mô hình đổi sau khi khoá → cảnh báo cần kiểm tra", B(ef2.Messages.Any(m => m.Contains("mô hình thay đổi sau ngày") && m.Contains("cần kiểm tra"))), 1, 0);
        fz.BaseHash = gh;
        string pj2 = Path.Combine(Path.GetTempPath(), "kcvxt_frozen_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".kcvxt");
        p.Save(pj2);
        var p3 = Project.Load(pj2);
        File.Delete(pj2);
        check("Khoá bản vẽ: lưu/mở JSON giữ bản khoá", B(p3.FrozenDrawings.Count == 1 && DrawingOverlay.FrozenWriter(p3.FrozenDrawings[0]).Texts().Contains(marker)), 1, 0);
        // tờ dàn trang khoá
        var sh0 = p.Sheets[0];
        var fzs = DrawingOverlay.Freeze(p, DrawingOverlay.SheetKey(sh0), sh0.Name, cadFile, SheetBuilder.GeneratedHash(p, r, d, sh0), 1, out _);
        check("Khoá tờ dàn trang: ghép tờ dùng bản đã nạp", B(fzs != null && SheetBuilder.Compose(p, r, d, sh0).Texts().Contains(marker)), 1, 0);
        DrawingOverlay.Unfreeze(p, DrawingOverlay.SheetKey(sh0));
        check("Bỏ khoá tờ: ghép tờ tự sinh", B(!SheetBuilder.Compose(p, r, d, sh0).Texts().Contains(marker)), 1, 0);
        // bỏ khoá → dựng lại (kèm chỉnh sửa)
        check("Bỏ khoá: xoá bản khoá", B(DrawingOverlay.Unfreeze(p, key)), 1, 0);
        string dUn = Path.Combine(root, "BoKhoa");
        Clean(dUn);
        var eu = DrawingSet.Export(p, d, dUn, DrawingExportOptions.Split);
        string tUn = File.ReadAllText(eu.Files.First(f => Path.GetFileNameWithoutExtension(f) == key.Substring(3)));
        check("Bỏ khoá: bản vẽ dựng lại từ mô hình (kèm chỉnh sửa)", B(!tUn.Contains(marker) && tUn.Contains(DxfWriter.Encode(newTitle))), 1, 0);

        // ================= 6. ĐỌC DXF HIỆN ĐẠI (LWPOLYLINE, MTEXT, INSERT) =================
        string modern = string.Join("\n", new[]
        {
            "0", "SECTION", "2", "BLOCKS",
            "0", "BLOCK", "8", "0", "2", "KHOI1", "70", "0", "10", "0.0", "20", "0.0",
            "0", "LINE", "8", "0", "10", "0.0", "20", "0.0", "11", "10.0", "21", "0.0",
            "0", "ENDBLK", "8", "0",
            "0", "ENDSEC",
            "0", "SECTION", "2", "ENTITIES",
            "0", "LWPOLYLINE", "8", "THEP", "90", "3", "70", "0", "10", "0.0", "20", "0.0", "42", "1.0", "10", "100.0", "20", "0.0", "10", "100.0", "20", "50.0",
            "0", "MTEXT", "8", "CHU", "10", "5.0", "20", "5.0", "40", "2.5", "71", "1", "1", "{\\fArial|b0;D\\U+1EA6M}\\PCH\\U+00CDNH",
            "0", "INSERT", "8", "KY_HIEU", "2", "KHOI1", "10", "200.0", "20", "100.0", "41", "2.0", "42", "2.0", "50", "90.0",
            "0", "HATCH", "8", "0",
            "0", "ENDSEC", "0", "EOF"
        });
        var mw = DxfWriter.FromDxfText(modern, 1, out var mwarn);
        var mrefs = mw.EntityRefs();
        check("Đọc DXF: LWPOLYLINE cung lồi → đa tuyến nhiều điểm", B(mrefs.Any(e => e.Type == "POLYLINE" && e.Layer == Lay.Thep && e.Box.Y1 < -40)), 1, 0);
        check("Đọc DXF: MTEXT bỏ mã định dạng", B(mw.Texts().Contains("DẦM CHÍNH")), 1, 0);
        var ins = mrefs.FirstOrDefault(e => e.Type == "LINE");
        check("Đọc DXF: INSERT xoay 90°, tỷ lệ 2 → đoạn dài 20 theo Y", ins != null ? ins.Box.H : 0, 20, 1e-6);
        check("Đọc DXF: thực thể chưa hỗ trợ được báo (HATCH)", B(mwarn.Any(w => w.Contains("HATCH"))), 1, 0);

        // ================= 7. TUỲ CHỌN XUẤT: MẪU TÊN, LƯU/MỞ =================
        check("Mẫu tên: {so}_{ten}", B(DrawingExportOptions.ApplyPattern("{so}_{ten}", "KC-03", "CHI TIẾT CỘT", "Nhà", 3) == "KC-03_CHI_TIẾT_CỘT"), 1, 0);
        string oldUser = CadStandard.UserFolderOverride;
        try
        {
            CadStandard.UserFolderOverride = Path.Combine(Path.GetTempPath(), "kcvxt_expopt_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            new DrawingExportOptions { Mode = DrawingExportMode.Single, SingleFileName = "{duan}_Gop", FilePattern = "{stt}" }.SaveUser();
            var lo = DrawingExportOptions.LoadUser();
            check("Tuỳ chọn xuất: nhớ lựa chọn \"Gộp 1 file\"", B(lo.Mode == DrawingExportMode.Single && lo.SingleFileName == "{duan}_Gop" && lo.FilePattern == "{stt}"), 1, 0);
            try { Directory.Delete(CadStandard.UserFolderOverride, true); } catch (IOException) { }
        }
        finally { CadStandard.UserFolderOverride = oldUser; }
        Console.WriteLine($"   · {nItems} bản vẽ; gộp: {Path.GetFileName(e1.Files.FirstOrDefault())}; {p.Sheets.Count} tờ dàn trang; chỉnh sửa: {p.DrawingEdits.Count}");
    }
}
