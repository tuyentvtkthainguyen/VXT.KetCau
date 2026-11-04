using System.IO;
using System.Windows;

namespace KetCauVXT.App;

/// <summary>Chuyển giữa 2 chế độ làm việc: Thiết kế – chạy nội lực (cửa sổ này) và Triển khai hồ sơ (DetailingWindow).</summary>
public partial class MainWindow
{
    private void MnuDetailingMode_Click(object sender, RoutedEventArgs e) => OpenDetailingMode();

    /// <summary>Mở (hoặc đưa lên trước) cửa sổ Triển khai hồ sơ; cửa sổ mô hình vẫn mở để đối chiếu nội lực.</summary>
    internal DetailingWindow OpenDetailingMode()
    {
        var w = Application.Current.Windows.OfType<DetailingWindow>().FirstOrDefault();
        if (w == null) { w = new DetailingWindow(); w.Show(); }
        else if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Activate();
        return w;
    }

    private void MnuStartMode_Click(object sender, RoutedEventArgs e)
    {
        var l = new LauncherWindow { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        if (l.ShowDialog() == true && l.Chosen == LauncherWindow.Mode.Detailing) OpenDetailingMode();
    }

    /// <summary>Bước tự kiểm tra (--selftest): chụp màn hình chọn chế độ, mở chế độ Triển khai hồ sơ với hồ sơ mẫu,
    /// chọn từng loại cấu kiện (chụp ảnh), xuất bản vẽ DXF / Excel / Word.</summary>
    private async Task SelfTestDetailing(string tag)
    {
        var l = new LauncherWindow { ShowInTaskbar = false };
        l.Show();
        await Pause(600);
        try
        {
            l.UpdateLayout();
            var src = (FrameworkElement)l.Content;
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Max(1, src.ActualWidth), (int)Math.Max(1, src.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(src);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using (var fs = File.Create(Path.Combine(SelfTestOut, tag + "_ChonCheDo.png"))) enc.Save(fs);
            StLog("ảnh " + tag + "_ChonCheDo");
        }
        finally { l.Close(); }
        var w = new DetailingWindow { ShowInTaskbar = false };
        w.Show();
        await Pause(800);
        foreach (var line in await w.SelfTest(SelfTestOut, tag)) StLog(line);
        w.Close();
    }
}
