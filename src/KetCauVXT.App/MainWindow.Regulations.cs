using System.Windows;
using KetCauVXT.Model;

namespace KetCauVXT.App;

// =====================================================================
// TRỢ GIÚP: "Văn bản pháp lý & tiêu chuẩn áp dụng…" – RegulationWindow.cs (danh mục Model/Regulations.cs)
// Mở dự án: cảnh báo không chặn trên thanh trạng thái khi dự án trích dẫn văn bản đã hết hiệu lực.
// =====================================================================
public partial class MainWindow
{
    private RegulationWindow _regWin;

    private void MnuRegulations_Click(object sender, RoutedEventArgs e) => OpenRegulations();

    /// <summary>Mở cửa sổ danh mục văn bản (một cửa sổ). modal = false: không chặn (tự kiểm tra).</summary>
    private RegulationWindow OpenRegulations(bool modal = false)
    {
        try
        {
            if (_regWin != null && _regWin.IsLoaded)
            {
                _regWin.Close();
                _regWin = null;
            }
            var w = new RegulationWindow(_p) { Owner = this };
            w.Closed += (s, e) =>
            {
                if (ReferenceEquals(_regWin, w)) _regWin = null;
                if (w.Changed)
                    SetStatus("Đã cập nhật danh mục văn bản pháp lý & tiêu chuẩn – thuyết minh, phụ lục, ghi chú bản vẽ, bảng khối lượng lập lại sẽ trích dẫn theo danh mục mới."
                              + RegulationStatusSuffix());
            };
            _regWin = w;
            if (modal) w.ShowDialog(); else w.Show();
            return w;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Không mở được danh mục văn bản pháp lý:\n" + ex.Message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }

    /// <summary>Đuôi thông báo trạng thái khi dự án trích dẫn văn bản đã hết hiệu lực / được thay thế ("" = không có).</summary>
    private string RegulationStatusSuffix()
    {
        try
        {
            var ws = RegulationService.CheckProject(_p).Where(x => x.IsWarning).ToList();
            if (ws.Count == 0) return "";
            return $"   – Lưu ý: dự án có {ws.Count} chỗ trích dẫn văn bản đã hết hiệu lực/được thay thế (vd. {ws[0].Where}: {ws[0].Code}) – "
                   + "xem Trợ giúp → Văn bản pháp lý & tiêu chuẩn áp dụng → Kiểm tra dự án.";
        }
        catch (Exception) { return ""; }
    }
}
