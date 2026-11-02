using System.Windows;

namespace KetCauVXT.App;

// =====================================================================
// TRỢ GIÚP: "Phụ lục tính toán & hướng dẫn…" (menu Trợ giúp, phím F1) – AppendixWindow.cs
// =====================================================================
public partial class MainWindow
{
    private AppendixWindow _appendixWin;

    private void MnuAppendix_Click(object sender, RoutedEventArgs e) => ShowAppendix();

    /// <summary>Mở cửa sổ phụ lục tính toán (một cửa sổ duy nhất; mở lại sẽ lập lại nội dung theo công trình hiện tại).</summary>
    private void ShowAppendix()
    {
        try
        {
            if (_appendixWin != null && _appendixWin.IsLoaded)
            {
                _appendixWin.Close();
                _appendixWin = null;
            }
            _appendixWin = new AppendixWindow(_p) { Owner = this };
            _appendixWin.Closed += (s, e) => _appendixWin = null;
            _appendixWin.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Không mở được phụ lục tính toán:\n" + ex.Message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
