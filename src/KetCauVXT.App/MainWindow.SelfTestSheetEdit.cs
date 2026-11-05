using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Tự kiểm tra dàn trang thủ công: mở cửa sổ dàn trang, thêm trang trắng với chữ, mũi tên, khung, bảng,
/// kích thước, đám mây, logo (ảnh PNG tạo sẵn) và một khung nhìn bị cắt; thử xoá – hoàn tác – làm lại;
/// chụp ảnh cửa sổ, xuất DXF + PDF của tờ đó vào thư mục tự kiểm tra.</summary>
public partial class MainWindow
{
    /// <summary>Logo PNG nhỏ (base64) vẽ bằng WPF – dùng cho tự kiểm tra.</summary>
    private static string MakeSelfTestLogo()
    {
        const int w = 240, h = 120;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(20, 70, 140)), null, new Rect(0, 0, w, h), 18, 18);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 200, 40)), null, new Point(52, 60), 34, 34);
            var ft = new FormattedText("VXT", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 54, Brushes.White, 1.0);
            dc.DrawText(ft, new Point(100, (h - ft.Height) / 2));
        }
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    private async Task SelfTestManualSheet(string tag)
    {
        var dsg = _design ?? (_res != null ? ConcreteDesigner.Design(_p, _res) : null);
        if (_p.Sheets == null || _p.Sheets.Count == 0) _p.Sheets = SheetBuilder.DefaultSet(_p, dsg, _res);
        var sw = new SheetWindow(_p, _res, dsg, () => MarkDirty(false)) { Owner = this, Width = 1500, Height = 900 };
        sw.Show();
        try
        {
            sw.SelectSheet(0);
            await Pause(800);

            // khung tên công trình + logo
            string logo = MakeSelfTestLogo();
            sw.SetTitleBlock(new TitleBlockInfo
            {
                Company = "Công ty TNHH Việt Const", Designer = _p.Designer ?? "", Checker = "Kiểm tra tự động",
                Approver = "Vũ Xuân Tuyến", LogoData = logo, NumberPrefix = "KC-"
            });

            // trang trắng
            var s = sw.AddBlankSheet();
            s.Name = "DÀN TRANG THỦ CÔNG";
            var (pw, ph) = s.PaperSize();
            StLog($"dàn trang thủ công: trang trắng {s.Number} {s.Paper} {pw}×{ph} mm");

            var title = SheetBuilder.NewText(20, ph - 12, "DÀN TRANG THỦ CÔNG – BỐ TRÍ TỰ DO\nChữ nhiều dòng, cao 6 mm, đậm, căn trái", 6);
            title.Bold = true;
            sw.AddItem(title);
            var rot = SheetBuilder.NewText(12, 60, "CHỮ XOAY 90°", 4);
            rot.Rotation = 90; rot.Color = "#0040FF";
            sw.AddItem(rot);
            var frame = SheetBuilder.NewRect(15, 15, pw - 30, ph - 45);
            frame.LineWeight = 0.5;
            sw.AddItem(frame);
            var arrow = SheetBuilder.NewLine(200, ph - 70, 245, ph - 100, true);
            arrow.Color = "#E00000"; arrow.LineWeight = 0.35;
            sw.AddItem(arrow);
            sw.AddItem(SheetBuilder.NewText(150, ph - 66, "Vùng cần chú ý", 3.5));
            var cloud = SheetBuilder.NewCloud(235, ph - 135, 60, 35);
            sw.AddItem(cloud);
            sw.AddItem(SheetBuilder.NewCircle(330, ph - 90, 12));
            var tab = SheetBuilder.NewTable(pw - 145, 25, 120, 36, 4, 3);
            SheetLayoutMath.TableFromText(tab, "Ký hiệu | Tiết diện (mm) | Số lượng\nD1 | 220×400 | 6\nD2 | 220×300 | 4\nC1 | 220×300 | 6");
            tab.ColWidths = new List<double> { 1, 2, 1 };
            sw.AddItem(tab);
            var dim = SheetBuilder.NewDim(20, 60, 120, 60, -8, 50);
            sw.AddItem(dim);
            var img = SheetBuilder.NewImage(pw - 75, ph - 52, 50, 25, logo, "logo_vxt.png");
            sw.AddItem(img);

            // khung nhìn bị cắt: mặt bằng tầng ở tỷ lệ 1/50 trong khung nhỏ, dịch hình
            var story = _p.Stories.OrderBy(x => x.Elevation).Skip(1).FirstOrDefault() ?? _p.Stories.FirstOrDefault();
            var view = sw.AddView(SheetViewKind.PlanStory, story?.Name ?? "");
            if (view != null)
                sw.EditView(view, v =>
                {
                    v.Scale = 50; v.X = 130; v.Y = 75; v.W = 150; v.H = 105;
                    v.PanX = -25; v.PanY = 20; v.Border = true; v.Title = "MẶT BẰNG (VÙNG CẮT)";
                });
            sw.RefreshPage();
            int idx = _p.Sheets.IndexOf(sw.CurrentSheet);
            sw.SelectSheet(idx);
            await Pause(800);
            var cur = sw.CurrentSheet;
            sw.SetSelection(cur.Items.FirstOrDefault(i => i.Kind == SheetItemKind.Table));
            await Pause(1500);
            ShotWindow(sw, $"{tag}_19_DanTrangThuCong");
            StLog($"dàn trang thủ công: {cur.Items.Count} đối tượng, {cur.Views.Count} khung nhìn");

            // căn hàng + xoá → hoàn tác → làm lại → hoàn tác
            int n = cur.Items.Count;
            var circle = cur.Items.FirstOrDefault(i => i.Kind == SheetItemKind.Circle);
            sw.SetSelection(circle);
            sw.DeleteSelection();
            int afterDel = sw.CurrentSheet.Items.Count;
            sw.Undo();
            int afterUndo = sw.CurrentSheet.Items.Count;
            sw.Redo();
            int afterRedo = sw.CurrentSheet.Items.Count;
            sw.Undo();
            int final = sw.CurrentSheet.Items.Count;
            bool undoOk = afterDel == n - 1 && afterUndo == n && afterRedo == n - 1 && final == n;
            StLog($"dàn trang thủ công: xoá/hoàn tác/làm lại {n}→{afterDel}→{afterUndo}→{afterRedo}→{final} – {(undoOk ? "đúng" : "SAI")}");
            if (!undoOk) throw new Exception("Hoàn tác/làm lại dàn trang thủ công sai");

            cur = sw.CurrentSheet;
            string dxf = Path.Combine(SelfTestOut, tag + "_DanTrangThuCong.dxf");
            SheetBuilder.ExportDxf(_p, _res, dsg, cur, dxf);
            string pdf = Path.Combine(SelfTestOut, tag + "_DanTrangThuCong.pdf");
            sw.ExportPdf(pdf, new List<Sheet> { cur }, 150);
            StLog($"dàn trang thủ công: DXF {Path.GetFileName(dxf)} ({new FileInfo(dxf).Length / 1024} KB), PDF {Path.GetFileName(pdf)} ({new FileInfo(pdf).Length / 1024} KB)");
        }
        finally
        {
            sw.Close();
        }
    }
}
