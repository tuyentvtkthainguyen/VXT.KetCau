using System.Windows;
using System.Windows.Controls;

namespace KetCauVXT.App;

// =====================================================================
// GIAO DIỆN GỌN – chế độ Thiết kế (CompactShell.cs): menu + thanh công cụ một hàng, khung nhập bên trái thu gọn được,
// F11 mở rộng vùng làm việc (ẩn cả cây mô hình, bảng thông tin dưới), Ctrl+Q tìm lệnh.
// =====================================================================
public partial class MainWindow
{
    private CompactShell _shell;
    private GridLength _fullExplorerW, _fullExplorerSplitW;
    private Visibility _fullExplorerVis, _fullExplorerSplitVis;

    /// <summary>Gọi cuối Window_Loaded.</summary>
    private void InitCompactUi()
    {
        try
        {
            if (SelfTestOut != null) CompactShell.NoPersist = true;   // tự kiểm CI: bố cục mặc định, không ghi ui.json
            _shell = CompactShell.Attach(this, "MainWindow", mainMenu, mainToolTray, mainStatus,
                new[] { new CompactPanel(tabLeft, "trai", "Nhập liệu – kết quả", true, leftSplitter, colLeftSplit) },
                new FrameworkElement[] { infoPanel },
                new (string, Action)[]
                {
                    ("Vừa màn hình", () => BtnFit_Click(this, new RoutedEventArgs())),
                    ("Phân tích (F5)", () => MnuRun_Click(this, new RoutedEventArgs())),
                });
            _shell.FullModeChanged += on =>
            {
                // cây mô hình (MainWindow.ModelExplorer.cs) dùng cột riêng – ẩn / trả lại khi mở rộng vùng làm việc
                if (on)
                {
                    _fullExplorerW = colExplorer.Width; _fullExplorerSplitW = colExplorerSplit.Width;
                    _fullExplorerVis = explorerHost.Visibility; _fullExplorerSplitVis = explorerSplitter.Visibility;
                    colExplorer.Width = new GridLength(0); colExplorerSplit.Width = new GridLength(0);
                    explorerHost.Visibility = Visibility.Collapsed; explorerSplitter.Visibility = Visibility.Collapsed;
                }
                else
                {
                    colExplorer.Width = _fullExplorerW; colExplorerSplit.Width = _fullExplorerSplitW;
                    explorerHost.Visibility = _fullExplorerVis; explorerSplitter.Visibility = _fullExplorerSplitVis;
                }
            };
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Giao diện gọn: " + ex.Message); }
    }
}
