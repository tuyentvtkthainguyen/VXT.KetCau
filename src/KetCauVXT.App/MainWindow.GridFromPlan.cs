using System.Windows;

namespace KetCauVXT.App;

/// <summary>Nút "Tải mặt bằng để lấy kích thước…" trong ô Lưới trục và tầng (GridFromPlanDialog.cs).</summary>
public partial class MainWindow
{
    private void BtnGridFromPlan_Click(object sender, RoutedEventArgs e)
    {
        var d = new GridFromPlanDialog(txtSpansX.Text, txtSpansY.Text, txtHeights.Text) { Owner = this };
        if (d.ShowDialog() != true) return;
        if (!string.IsNullOrWhiteSpace(d.SpansX)) txtSpansX.Text = d.SpansX;
        if (!string.IsNullOrWhiteSpace(d.SpansY)) txtSpansY.Text = d.SpansY;
        if (!string.IsNullOrWhiteSpace(d.Heights)) txtHeights.Text = d.Heights;
        MessageBox.Show(this, "Đã điền bước trục và chiều cao tầng từ mặt bằng.\nKiểm tra lại rồi bấm tạo / cập nhật mô hình như bình thường.", "Lưới trục và tầng",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
