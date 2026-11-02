using System.Text.Json;
using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm tra dàn trang thủ công: đối tượng chú thích (lưu/mở JSON, xuất DXF đúng lớp), khổ giấy tự chọn,
/// lề, cắt khung nhìn, toán bắt điểm/căn hàng/phân bố, chọn, thứ tự, bảng, đánh số tờ, hoàn tác/làm lại.</summary>
public static class ManualSheetTests
{
    // ảnh PNG 2×1 điểm ảnh (đỏ, xanh) – để kiểm tra nhúng base64
    private const string TinyPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAIAAAB7QOjdAAAAD0lEQVR4nGP4z8DAwPAfAAcAAf9+CLHQAAAAAElFTkSuQmCC";

    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---------------- mô hình nhỏ để có khung nhìn thật ----------------
        var p = ModelBuilder.NewProject("Dàn trang thủ công");
        p.Location = "Phường Quyết Thắng, Thái Nguyên";
        p.Designer = "Vũ Xuân Tuyến";
        ModelBuilder.SetGrid(p, new[] { 5.0 }, new[] { 4.5, 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
        {
            ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5
        });
        var r = FrameAnalysis.Run(p);
        var d = ConcreteDesigner.Design(p, r);

        // ---------------- khổ giấy, lề ----------------
        var cs = new Sheet { Paper = Sheet.CustomPaper, PaperW = 500, PaperH = 350 };
        check("Trang thủ công: khổ tự chọn W = 500", cs.PaperSize().W, 500, 0);
        check("Trang thủ công: khổ tự chọn H = 350", cs.PaperSize().H, 350, 0);
        var bad = new Sheet { Paper = Sheet.CustomPaper };
        check("Trang thủ công: khổ tự chọn chưa nhập → A3 ngang (420)", bad.PaperSize().W, 420, 0);
        check("Trang thủ công: A4 dọc → W = 210", new Sheet { Paper = "A4", Landscape = false }.PaperSize().W, 210, 0);
        var ms = new Sheet { Paper = "A3", MarginLeft = 30, MarginRight = 7, MarginTop = 8, MarginBottom = 9 };
        var (inner, tbr) = SheetBuilder.Frame(ms);
        check("Trang thủ công: lề trái tự chọn 30", inner.X1, 30, 1e-9);
        check("Trang thủ công: lề phải tự chọn 7", 420 - inner.X2, 7, 1e-9);
        check("Trang thủ công: lề trên tự chọn 8", 297 - inner.Y2, 8, 1e-9);
        check("Trang thủ công: lề dưới tự chọn 9 (khung tên đặt trên lề dưới)", tbr.Y1, 9, 1e-9);
        var (inner0, _) = SheetBuilder.Frame(new Sheet { Paper = "A3" });
        check("Trang thủ công: lề mặc định không đổi (25/10)", inner0.X1 + (420 - inner0.X2), 35, 1e-9);

        // ---------------- tờ trắng với đủ loại đối tượng ----------------
        var s = SheetBuilder.BlankSheet(p, "KC-20", "A3", true);
        s.Name = "DÀN TRANG THỦ CÔNG";
        var txt = SheetBuilder.NewText(40, 270, "GHI CHÚ CHUNG\nDòng thứ hai Ø16a200", 5);
        txt.Bold = true; txt.Rotation = 0; txt.Align = 0;
        var line = SheetBuilder.NewLine(40, 200, 120, 200, true);
        line.Color = "#FF0000"; line.LineWeight = 0.5;
        var rect = SheetBuilder.NewRect(30, 30, 100, 60);
        rect.LineType = "DASHED";
        var circ = SheetBuilder.NewCircle(200, 100, 15);
        var cloud = SheetBuilder.NewCloud(250, 150, 60, 40);
        var img = SheetBuilder.NewImage(300, 220, 40, 20, TinyPng, "logo.png");
        var tab = SheetBuilder.NewTable(150, 200, 120, 30, 3, 3);
        SheetLayoutMath.TableFromText(tab, "Ký hiệu | Tiết diện | Số lượng\nD1 | 220x400 | 4\nC1 | 220x300 |");
        var dim = SheetBuilder.NewDim(40, 150, 90, 150, 8, 100);
        s.Items.AddRange(new[] { txt, line, rect, circ, cloud, img, tab, dim });
        check("Trang thủ công: bảng 3 hàng × 3 cột", tab.Rows * 10 + tab.Cols, 33, 0);
        check("Trang thủ công: ô trống cuối bảng", B(tab.Cell(2, 2) == ""), 1, 0);
        check("Trang thủ công: bảng → chữ → bảng giữ nguyên",
            B(SheetLayoutMath.TableToText(tab) == "Ký hiệu | Tiết diện | Số lượng\nD1 | 220x400 | 4\nC1 | 220x300 | "), 1, 0);
        check("Trang thủ công: kích thước 50 mm giấy ở 1/100 ghi 5000", B(SheetBuilder.DimText(dim) == "5000"), 1, 0);

        // khung nhìn bị cắt (crop): mặt bằng tỷ lệ cố định 1/50 trong khung nhỏ + dịch hình
        var st = p.Stories.OrderBy(x => x.Elevation).Last();
        var view = new SheetView { Kind = SheetViewKind.PlanStory, Param = st.Name, Title = "MẶT BẰNG (cắt)", X = 150, Y = 40, W = 120, H = 90, Scale = 50, PanX = -15, PanY = 10, Border = true };
        s.Views.Add(view);
        var figs = SheetBuilder.Figures(p, r, d, view);
        var lay = SheetBuilder.Layout(figs, view);
        check("Trang thủ công: khung nhìn 1/50 trong khung nhỏ cần cắt", B(SheetBuilder.NeedsClip(view, lay)), 1, 0);
        var view0 = SheetBuilder.Clone(view); view0.PanX = 0; view0.PanY = 0;
        var lay0 = SheetBuilder.Layout(figs, view0);
        check("Trang thủ công: dịch hình PanX = −15 mm", lay.Content.X1 - lay0.Content.X1, -15, 1e-9);
        check("Trang thủ công: dịch hình PanY = +10 mm", lay.Content.Y2 - lay0.Content.Y2, 10, 1e-9);
        check("Trang thủ công: có tên hình khi không ẩn", B(lay.Caption != null), 1, 0);
        var vh = SheetBuilder.Clone(view); vh.HideTitle = true;
        check("Trang thủ công: ẩn tên hình", B(SheetBuilder.Layout(figs, vh).Caption == null), 1, 0);

        // ---------------- lưu / mở JSON ----------------
        p.TitleBlock = new TitleBlockInfo { Company = "Công ty TNHH Xây dựng VXT", Approver = "Nguyễn Văn A", LogoData = TinyPng, NumberPrefix = "KC-" };
        p.Sheets = new List<Sheet> { s, new Sheet { Number = "KC-21", Name = "KHỔ TỰ CHỌN", Paper = Sheet.CustomPaper, PaperW = 600, PaperH = 400, MarginLeft = 20, Revision = "B" } };
        string dir = Path.Combine(outDir, "DanTrangThuCong");
        Directory.CreateDirectory(dir);
        string pf = Path.Combine(dir, "dan_trang_thu_cong.kcvxt");
        p.Save(pf);
        var q = Project.Load(pf);
        var qs = q.Sheets[0];
        check("Trang thủ công: JSON giữ số đối tượng", qs.Items.Count, 8, 0);
        check("Trang thủ công: JSON giữ đúng loại theo thứ tự",
            B(qs.Items.Select(i => i.Kind).SequenceEqual(new[] { SheetItemKind.Text, SheetItemKind.Line, SheetItemKind.Rect, SheetItemKind.Circle,
                SheetItemKind.Cloud, SheetItemKind.Image, SheetItemKind.Table, SheetItemKind.Dim })), 1, 0);
        check("Trang thủ công: JSON giữ chữ nhiều dòng", B(qs.Items[0].Text == txt.Text && qs.Items[0].Bold && qs.Items[0].TextHeight == 5), 1, 0);
        check("Trang thủ công: JSON giữ màu, mũi tên, nét", B(qs.Items[1].Color == "#FF0000" && qs.Items[1].ArrowEnd && qs.Items[1].LineWeight == 0.5 && qs.Items[1].Pts.Count == 4), 1, 0);
        check("Trang thủ công: JSON giữ kiểu nét", B(qs.Items[2].LineType == "DASHED"), 1, 0);
        check("Trang thủ công: JSON giữ ảnh base64", B(qs.Items[5].ImageData == TinyPng && qs.Items[5].Text == "logo.png"), 1, 0);
        check("Trang thủ công: JSON giữ ô bảng", B(qs.Items[6].Cells.SequenceEqual(tab.Cells) && qs.Items[6].Rows == 3), 1, 0);
        check("Trang thủ công: JSON giữ kích thước (tỷ lệ, lệch)", B(qs.Items[7].DimScale == 100 && qs.Items[7].Offset == 8), 1, 0);
        check("Trang thủ công: JSON giữ dịch hình/viền khung nhìn", B(qs.Views[0].PanX == -15 && qs.Views[0].PanY == 10 && qs.Views[0].Border && qs.Views[0].Scale == 50), 1, 0);
        check("Trang thủ công: JSON giữ tờ trắng (không khung tên)", B(!qs.ShowTitleBlock && !qs.ShowFrame), 1, 0);
        check("Trang thủ công: JSON giữ khổ tự chọn 600×400", q.Sheets[1].PaperSize().W * 1000 + q.Sheets[1].PaperSize().H, 600400, 0);
        check("Trang thủ công: JSON giữ lề, lần sửa", B(q.Sheets[1].MarginLeft == 20 && q.Sheets[1].MarginRight < 0 && q.Sheets[1].Revision == "B"), 1, 0);
        check("Trang thủ công: JSON giữ khung tên công trình + logo", B(q.TitleBlock.Company == p.TitleBlock.Company && q.TitleBlock.LogoData == TinyPng), 1, 0);
        check("Trang thủ công: file dự án có tên enum dạng chữ", B(File.ReadAllText(pf).Contains("\"Cloud\"")), 1, 0);

        // tương thích file cũ (không có Items, TitleBlock…)
        var old = JsonSerializer.Deserialize<Sheet>("{\"Number\":\"KC-01\",\"Paper\":\"A2\",\"Views\":[{\"Kind\":0,\"X\":10,\"Y\":10,\"W\":50,\"H\":40}]}");
        check("Trang thủ công: file cũ → danh sách đối tượng rỗng", B(old.Items != null && old.Items.Count == 0), 1, 0);
        check("Trang thủ công: file cũ → có khung viền, khung tên, lề mặc định", B(old.ShowFrame && old.ShowTitleBlock && old.MarginLeft < 0), 1, 0);
        check("Trang thủ công: file cũ → khung nhìn không dịch, không khoá", B(old.Views[0].PanX == 0 && !old.Views[0].Locked && !old.Views[0].HideTitle), 1, 0);
        var oldP = JsonSerializer.Deserialize<Project>("{\"Name\":\"Cũ\"}", Project.JsonOptions);
        check("Trang thủ công: file dự án cũ → khung tên mặc định", B(oldP.TitleBlock != null && oldP.TitleBlock.Company == ""), 1, 0);

        // ---------------- xuất DXF ----------------
        var w = SheetBuilder.Compose(p, r, d, s);
        check("Trang thủ công: DXF chữ trên lớp CHU (2 dòng chữ + bảng)", B(w.CountEntities("TEXT", Lay.Chu) >= 2 + 7), 1, 0);
        check("Trang thủ công: DXF đường/mũi tên trên lớp KY_HIEU", B(w.CountEntities("POLYLINE", Lay.KyHieu) >= 2 && w.CountEntities("SOLID", Lay.KyHieu) >= 1), 1, 0);
        check("Trang thủ công: DXF đường tròn trên lớp KY_HIEU", B(w.CountEntities("CIRCLE", Lay.KyHieu) >= 1 && SheetBuilder.ItemFigure(circ).CountEntities("CIRCLE", Lay.KyHieu) == 1), 1, 0);
        check("Trang thủ công: DXF đám mây = các cung tròn (lớp THEP)", B(w.CountEntities("ARC", Lay.Thep) >= 4 * 4), 1, 0);
        check("Trang thủ công: DXF kích thước trên lớp KICH_THUOC (≥ 5 nét + chữ)", B(w.CountEntities("LINE", Lay.KichThuoc) >= 5 && w.CountEntities("TEXT", Lay.KichThuoc) >= 1), 1, 0);
        string dxfPath = Path.Combine(dir, SheetBuilder.FileName(s));
        SheetBuilder.ExportDxf(p, r, d, s, dxfPath);
        string dxf = File.ReadAllText(dxfPath);
        check("Trang thủ công: DXF chứa chữ tiếng Việt mã hoá", B(dxf.Contains(DxfWriter.Encode("Dòng thứ hai Ø16a200")) && !dxf.Any(ch => ch > 127)), 1, 0);
        check("Trang thủ công: DXF ghi chiều dài thực 5000", B(dxf.Contains("\r\n5000\r\n")), 1, 0);
        check("Trang thủ công: DXF ảnh → khung + tên ảnh", B(dxf.Contains(DxfWriter.Encode("ẢNH: logo.png"))), 1, 0);
        check("Trang thủ công: DXF màu đỏ → ACI 1 (mã 62)", B(SheetBuilder.ItemFigure(line).ToDxfString().Contains(" 62\r\n1\r\n")), 1, 0);
        check("Trang thủ công: DXF nét đứt → mã 6 DASHED", B(SheetBuilder.ItemFigure(rect).ToDxfString().Contains("  6\r\nDASHED\r\n")), 1, 0);
        check("Trang thủ công: DXF nét in 0,5 mm → đa tuyến có bề rộng", B(SheetBuilder.ItemFigure(line).GetPrimitives().Any(pr => pr.Kind == PrimKind.Polyline && Math.Abs(pr.Width - 0.5) < 1e-9)), 1, 0);
        check("Trang thủ công: tờ trắng không có khung tên (chỉ mép giấy)", SheetBuilder.TitleBlock(p, s).EntityCount, 1, 0);
        check("Trang thủ công: có ô logo trong khung tên", B(!SheetBuilder.TitleLogoRect(p, q.Sheets[1]).IsEmpty), 1, 0);
        var tbText = SheetBuilder.TitleBlock(p, q.Sheets[1]).GetPrimitives().Where(pr => pr.Kind == PrimKind.Text).Select(pr => pr.Text).ToList();
        check("Trang thủ công: khung tên có đơn vị thiết kế + người duyệt", B(tbText.Contains("CÔNG TY TNHH XÂY DỰNG VXT") && tbText.Contains("Nguyễn Văn A")), 1, 0);

        // khung nhìn cắt: mọi nét của hình nằm trong khung
        var only = SheetBuilder.BlankSheet(p, "KC-99");
        only.Views.Add(SheetBuilder.Clone(view));
        only.Views[0].Border = false; only.Views[0].HideTitle = true;
        var wc = SheetBuilder.Compose(p, r, d, only);
        var fr = SheetLayoutMath.Bounds(only.Views[0]).Inflate(0.01);
        int outsidePts = 0, nPts = 0;
        foreach (var pr in wc.GetPrimitives())
        {
            if (pr.Layer == Lay.KhungTen) continue;
            if (pr.Kind is PrimKind.Line or PrimKind.Polyline or PrimKind.Text)
                for (int i = 0; i + 1 < pr.Pts.Length; i += 2)
                {
                    nPts++;
                    if (pr.Pts[i] < fr.X1 || pr.Pts[i] > fr.X2 || pr.Pts[i + 1] < fr.Y1 || pr.Pts[i + 1] > fr.Y2) outsidePts++;
                }
        }
        check("Trang thủ công: khung nhìn cắt có nét vẽ", B(nPts > 20), 1, 0);
        check("Trang thủ công: DXF cắt nét theo khung nhìn (không điểm nào ra ngoài)", outsidePts, 0, 0);
        double cx1 = -10, cy1 = 5, cx2 = 30, cy2 = 5;
        bool clipOk = DxfWriter.ClipSegment(new Rect2(0, 0, 20, 10), ref cx1, ref cy1, ref cx2, ref cy2);
        check("Trang thủ công: cắt đoạn thẳng (Liang–Barsky)", B(clipOk && Math.Abs(cx1) < 1e-9 && Math.Abs(cx2 - 20) < 1e-9), 1, 0);

        // ---------------- bắt điểm ----------------
        check("Trang thủ công: bắt lưới 5 mm (12,6 → 15)", SheetLayoutMath.SnapGrid(12.6, 5), 15, 1e-12);
        check("Trang thủ công: bắt lưới 2,5 mm (12,4 → 12,5)", SheetLayoutMath.SnapGrid(12.4, 2.5), 12.5, 1e-12);
        var tg = new List<Rect2> { new(50, 0, 80, 100) };
        var sr1 = SheetLayoutMath.SnapMove(new Rect2(10.7, 20.2, 30.7, 40.2), tg, 1, 5);
        check("Trang thủ công: không gần đối tượng → bắt lưới dx", sr1.Dx, -0.7, 1e-9);
        check("Trang thủ công: không gần đối tượng → bắt lưới dy", sr1.Dy, -0.2, 1e-9);
        check("Trang thủ công: không có đường gióng khi chỉ bắt lưới", B(!sr1.SnappedX && !sr1.SnappedY), 1, 0);
        var sr2 = SheetLayoutMath.SnapMove(new Rect2(49.4, 10, 59.4, 20), tg, 1, 5);
        check("Trang thủ công: bắt mép trái đối tượng khác (dx = 0,6)", sr2.Dx, 0.6, 1e-9);
        check("Trang thủ công: đường gióng tại x = 50", sr2.GuideX, 50, 1e-9);
        var sr3 = SheetLayoutMath.SnapMove(new Rect2(54.6, 10, 74.6, 20), tg, 1, 0);
        check("Trang thủ công: bắt tâm đối tượng (tâm 64,6 → 65)", sr3.Dx, 0.4, 1e-9);
        var sp = SheetLayoutMath.SnapPoint(81.2, 33.3, tg, 2, 0);
        check("Trang thủ công: bắt điểm vào mép phải x = 80", sp.X, 80, 1e-9);
        check("Trang thủ công: bắt điểm không lưới giữ y", sp.Y, 33.3, 1e-9);

        // ---------------- căn hàng, phân bố ----------------
        var rs = new List<Rect2> { new(0, 0, 10, 10), new(15, 5, 25, 25), new(90, 2, 100, 12) };
        var al = SheetLayoutMath.Align(rs, AlignMode.Left);
        check("Trang thủ công: căn trái → hình 3 dịch −90", al[2].Dx, -90, 1e-9);
        var at = SheetLayoutMath.Align(rs, AlignMode.Top);
        check("Trang thủ công: căn trên → hình 1 dịch +15", at[0].Dy, 15, 1e-9);
        var ac = SheetLayoutMath.Align(rs, AlignMode.CenterH);
        check("Trang thủ công: căn giữa ngang → tâm hình 2 tới 50", rs[1].Cx + ac[1].Dx, 50, 1e-9);
        var db = SheetLayoutMath.Distribute(rs, true);
        check("Trang thủ công: phân bố đều ngang → hình giữa dịch +30", db[1].Dx, 30, 1e-9);
        check("Trang thủ công: phân bố đều giữ hai hình ngoài", Math.Abs(db[0].Dx) + Math.Abs(db[2].Dx), 0, 0);
        var dv = SheetLayoutMath.Distribute(new List<Rect2> { new(0, 0, 10, 10), new(0, 12, 10, 22), new(0, 50, 10, 60) }, false);
        check("Trang thủ công: phân bố đều đứng → hình giữa dịch +13", dv[1].Dy, 13, 1e-9);

        // ---------------- đổi cỡ, chọn ----------------
        var rr = SheetLayoutMath.ResizeRect(new Rect2(0, 0, 20, 20), 4, 50, 60);
        check("Trang thủ công: kéo tay nắm phải trên", rr.W * 1000 + rr.H, 50060, 1e-9);
        var rr2 = SheetLayoutMath.ResizeRect(new Rect2(0, 0, 20, 20), 0, 25, 25, 2);
        check("Trang thủ công: tay nắm giữ cỡ tối thiểu 2 mm", rr2.X1, 18, 1e-9);
        var c2 = SheetBuilder.NewCircle(0, 0, 5);
        SheetLayoutMath.SetBounds(c2, SheetLayoutMath.Bounds(c2), new Rect2(0, 0, 30, 20));
        check("Trang thủ công: đổi cỡ đường tròn theo cạnh nhỏ", c2.R, 10, 1e-9);
        var t2 = SheetBuilder.NewText(0, 0, "ABC", 2.5);
        var tb0 = SheetLayoutMath.Bounds(t2);
        SheetLayoutMath.SetBounds(t2, tb0, new Rect2(tb0.X1, tb0.Y2 - 2 * tb0.H, tb0.X1 + 2 * tb0.W, tb0.Y2));
        check("Trang thủ công: phóng chữ ×2 → cao chữ 5", t2.TextHeight, 5, 1e-9);
        var mvl = SheetBuilder.NewLine(0, 0, 10, 0);
        SheetLayoutMath.Move(mvl, 5, 3);
        check("Trang thủ công: dịch đường dịch mọi điểm", mvl.Pts[2] * 100 + mvl.Pts[3], 1503, 1e-9);
        check("Trang thủ công: chọn đường khi nhấp gần", B(SheetLayoutMath.Hit(line, 80, 200.8, 1)), 1, 0);
        check("Trang thủ công: nhấp giữa khung không chọn khung (chọn được hình bên dưới)", B(!SheetLayoutMath.Hit(rect, 80, 60, 1)), 1, 0);
        check("Trang thủ công: nhấp mép khung chọn khung", B(SheetLayoutMath.Hit(rect, 30.5, 60, 1)), 1, 0);
        var rt = SheetBuilder.NewText(100, 100, "XOAY CHỮ", 4); rt.Rotation = 90;
        check("Trang thủ công: chọn chữ xoay 90°", B(SheetLayoutMath.Hit(rt, 102, 110, 0.5)), 1, 0);
        check("Trang thủ công: chữ xoay không bắt điểm ngang", B(!SheetLayoutMath.Hit(rt, 110, 98, 0.5)), 1, 0);
        var win = new Rect2(0, 0, 50, 50);
        check("Trang thủ công: chọn khung trái→phải cần nằm trọn", B(!SheetLayoutMath.InWindow(new Rect2(40, 40, 60, 60), win, false)), 1, 0);
        check("Trang thủ công: chọn khung phải→trái lấy cả giao", B(SheetLayoutMath.InWindow(new Rect2(40, 40, 60, 60), win, true)), 1, 0);

        // ---------------- thứ tự, đánh số ----------------
        var zl = new List<string> { "a", "b", "c", "d" };
        SheetLayoutMath.BringToFront(zl, new List<string> { "b" });
        check("Trang thủ công: đưa lên trên cùng", B(string.Concat(zl) == "acdb"), 1, 0);
        SheetLayoutMath.SendToBack(zl, new List<string> { "d", "b" });
        check("Trang thủ công: đưa xuống dưới cùng (giữ thứ tự nhóm)", B(string.Concat(zl) == "dbac"), 1, 0);
        var rn = new List<Sheet> { new() { Number = "X" }, new() { Number = "KC-07" }, new() { Number = "" } };
        SheetBuilder.Renumber(rn, "KC-");
        check("Trang thủ công: đánh số lại KC-01…KC-03", B(string.Join(",", rn.Select(x => x.Number)) == "KC-01,KC-02,KC-03"), 1, 0);

        // ---------------- màu ----------------
        check("Trang thủ công: #FF0000 → ACI 1", SheetBuilder.Aci("#FF0000"), 1, 0);
        check("Trang thủ công: #0000FF → ACI 5", SheetBuilder.Aci("#0000ff"), 5, 0);
        check("Trang thủ công: màu trống → theo lớp", SheetBuilder.Aci(""), 0, 0);

        // ---------------- hoàn tác / làm lại ----------------
        var u = new UndoStack { Limit = 3 };
        u.Push("A"); u.Push("B");
        check("Trang thủ công: hoàn tác về B", B(u.Undo("C") == "B"), 1, 0);
        check("Trang thủ công: hoàn tác về A", B(u.Undo("B") == "A"), 1, 0);
        check("Trang thủ công: hết bước hoàn tác", B(u.Undo("A") == null && !u.CanUndo), 1, 0);
        check("Trang thủ công: làm lại → B", B(u.Redo("A") == "B"), 1, 0);
        check("Trang thủ công: làm lại → C", B(u.Redo("B") == "C" && !u.CanRedo), 1, 0);
        u.Undo("C");
        u.Push("D");
        check("Trang thủ công: sửa mới xoá nhánh làm lại", B(!u.CanRedo), 1, 0);
        u.Push("D");
        check("Trang thủ công: không ghi trùng trạng thái", u.UndoCount, 2, 0);
        u.Push("E"); u.Push("F");
        check("Trang thủ công: giới hạn số bước", u.UndoCount, 3, 0);

        string snap = SheetBuilder.Snapshot(p);
        p.Sheets[0].Items.RemoveAt(0);
        p.Sheets[0].Views[0].X = 999;
        p.TitleBlock.Company = "khác";
        SheetBuilder.Restore(p, snap);
        check("Trang thủ công: khôi phục ảnh chụp → đủ đối tượng", p.Sheets[0].Items.Count, 8, 0);
        check("Trang thủ công: khôi phục ảnh chụp → vị trí khung nhìn", p.Sheets[0].Views[0].X, 150, 0);
        check("Trang thủ công: khôi phục ảnh chụp → khung tên", B(p.TitleBlock.Company == "Công ty TNHH Xây dựng VXT"), 1, 0);

        var clip = SheetBuilder.FromClipboard(SheetBuilder.ToClipboard(p.Sheets[0].Views.Take(1), p.Sheets[0].Items.Take(2), "KC-20"));
        check("Trang thủ công: bộ nhớ tạm sao chép 1 khung + 2 đối tượng", clip.Views.Count * 10 + clip.Items.Count, 12, 0);
        check("Trang thủ công: bộ nhớ tạm lỗi → null", B(SheetBuilder.FromClipboard("{không phải json") == null), 1, 0);

        // ---------------- bộ tờ mặc định không bị ảnh hưởng ----------------
        var def = SheetBuilder.DefaultSet(p, d, r);
        check("Trang thủ công: bộ tờ mặc định không có đối tượng chú thích", def.Sum(x => x.Items.Count), 0, 0);
        int clipped = def.Sum(x => x.Views.Count(v => SheetBuilder.NeedsClip(v, SheetBuilder.Layout(SheetBuilder.Figures(p, r, d, v), v))));
        check("Trang thủ công: bộ tờ mặc định không cần cắt hình", clipped, 0, 0);
        Console.WriteLine($"   · DXF tờ thủ công: {dxfPath} ({w.EntityCount} thực thể)");
    }
}
