using System.Windows;

namespace KetCauVXT.App;

// [Cỡ chữ] cỡ chữ nhãn cấu kiện trên mặt bằng chế độ Thiết kế (px màn hình) – lưu trong ui.json trên máy.
public partial class MainWindow
{
    /// <summary>Cỡ nhãn cấu kiện trên mặt bằng (px màn hình), 7…30.</summary>
    private static double PlanLabelPx => Math.Clamp(CompactShell.Settings?.PlanLabelPx is double v && v > 0 ? v : 11, 7, 30);

    private void MnuPlanLabelSize_Click(object sender, RoutedEventArgs e)
    {
        string cur = PlanLabelPx.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        string s = DialogUi.InputBox(this, "Cỡ chữ nhãn mặt bằng", "Cỡ chữ tên cột, dầm, sàn… trên mặt bằng (px màn hình, 7 – 30; mặc định 11):", cur);
        if (s == null) return;
        if (!double.TryParse(s.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) || v < 7 || v > 30)
        {
            MessageBox.Show(this, "Nhập số từ 7 đến 30.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (CompactShell.Settings != null) { CompactShell.Settings.PlanLabelPx = v; CompactShell.SaveSettings(); }
        RedrawPlan();
    }
}
