using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KetCauVXT.App;

/// <summary>Menu Xuất → "Dàn trang &amp; in bản vẽ…": mở cửa sổ dàn trang (các tờ lưu trong _p.Sheets).</summary>
public partial class MainWindow
{
    private void MnuSheets_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        try
        {
            var w = new SheetWindow(_p, _res, _design, () => MarkDirty(false)) { Owner = this };
            w.ShowDialog();
            SetStatus($"Dàn trang: {_p.Sheets.Count} tờ bản vẽ.");
        }
        catch (Exception ex) { ShowError("Lỗi dàn trang", ex); }
    }

    /// <summary>Chụp ảnh nội dung một cửa sổ khác (tự kiểm tra), giống Shot().</summary>
    private void ShotWindow(Window w, string name)
    {
        w.UpdateLayout();
        var src = (FrameworkElement)w.Content;
        int wd = (int)Math.Max(1, src.ActualWidth), ht = (int)Math.Max(1, src.ActualHeight);
        var rtb = new RenderTargetBitmap(wd, ht, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(242, 243, 245)), null, new Rect(0, 0, wd, ht));
        }
        rtb.Render(dv);
        rtb.Render(src);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(Path.Combine(SelfTestOut, name + ".png"));
        enc.Save(fs);
        StLog("ảnh " + name);
    }
}
