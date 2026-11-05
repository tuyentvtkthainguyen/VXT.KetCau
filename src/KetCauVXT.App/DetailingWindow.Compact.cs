using System.Windows;
using System.Windows.Controls;

namespace KetCauVXT.App;

// =====================================================================
// GIAO DIỆN GỌN – chế độ Triển khai hồ sơ (CompactShell.cs): menu + thanh công cụ một hàng, các nút "＋ Móng đơn / Dầm / …"
// gộp vào một nút "＋ Thêm cấu kiện ▾", cây cấu kiện và bảng nhập thu gọn được (« »), F11 mở rộng vùng làm việc,
// Ctrl+Q tìm lệnh nhanh.
// =====================================================================
public sealed partial class DetailingWindow
{
    private CompactShell _shell;

    private void InstallCompactUi(Menu menu, ToolBar bar, FrameworkElement status, FrameworkElement tree, GridSplitter sp1, FrameworkElement form, GridSplitter sp2)
    {
        try
        {
            _shell = CompactShell.Attach(this, "DetailingWindow", menu, bar, status,
                new[]
                {
                    new CompactPanel(tree, "cay", "Cấu kiện hồ sơ", true, sp1),
                    new CompactPanel(form, "nhap", "Thông số cấu kiện", true, sp2),
                },
                null,
                new (string, Action)[]
                {
                    ("Vừa khung", () => { _zoom = 1; RefreshPreview(); }),
                    ("Tính toàn bộ (F5)", RecalcAll),
                },
                groupAddTitle: "Thêm cấu kiện");
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Giao diện gọn: " + ex.Message); }
    }
}
