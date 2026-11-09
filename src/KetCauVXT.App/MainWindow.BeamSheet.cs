using System.Windows;
using KetCauVXT.Design;

namespace KetCauVXT.App;

/// <summary>[Bản vẽ từng dầm] Thiết kế → Dầm → "Bản vẽ từng dầm…": bản vẽ riêng từng dầm D1, D2… (xem, chọn thép, xuất).</summary>
public partial class MainWindow
{
    private async void BtnBeamSheet_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _p == null) return;
        if (!await EnsureDesign()) return;
        if (_design == null || _design.Beams.Count == 0)
        {
            MessageBox.Show("Chưa có dầm BTCT đã thiết kế.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var w = new BeamSheetWindow(_p, () => _design, d =>
            {
                try { CreateRebarDialog(d).ShowDialog(); }
                catch (Exception ex) { ShowError("Lỗi chọn bố trí thép", ex); }
            }) { Owner = this };
            var sel = SelectedDesignMember("Dầm");
            if (sel != null) w.Select(sel.Name);
            w.Show();
        }
        catch (Exception ex) { ShowError("Lỗi bản vẽ từng dầm", ex); }
    }
}
