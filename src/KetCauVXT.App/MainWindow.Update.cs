using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace KetCauVXT.App;

// =====================================================================
// CẬP NHẬT: nút "Cập nhật" cuối thanh công cụ chính + menu Trợ giúp → Kiểm tra & tải bản cập nhật…
// Tham số dòng lệnh --update (nút "Cập nhật Việt Const" trên bảng lệnh VXT trong AutoCAD) → mở ngay cửa sổ cập nhật.
// =====================================================================
public partial class MainWindow
{
    private void MnuUpdate_Click(object sender, RoutedEventArgs e) => UpdateService.ShowWindow(this);

    private void InitUpdateUi()
    {
        // giao diện gọn (CompactShell): nút đặt ở cụm góc phải hàng công cụ – luôn hiện; giao diện cổ điển: cuối thanh công cụ chính
        bool placed = false;
        try { placed = _shell != null && _shell.AddRightElement(UpdateService.MakeToolbarButton(() => this)); } catch (Exception) { }
        if (!placed)
        {
            try
            {
                if (mainToolTray.ToolBars.Count > 0)
                {
                    var bar = mainToolTray.ToolBars[0];
                    bar.Items.Add(new Separator());
                    bar.Items.Add(UpdateService.MakeToolbarButton(() => this));
                }
            }
            catch (Exception) { /* bỏ qua */ }
        }
        if (SelfTestOut == null && Environment.GetCommandLineArgs().Any(a => a.Equals("--update", StringComparison.OrdinalIgnoreCase)))
            Dispatcher.BeginInvoke(new Action(() => UpdateService.ShowWindow(this)), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }
}
