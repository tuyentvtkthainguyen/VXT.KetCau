using System.Windows;

namespace KetCauVXT.App;

/// <summary>Phân tích → "So sánh với ETABS/SAP/SAFE…": đối chiếu kết quả phân tích của chương trình với bảng kết quả xuất từ ETABS/SAP2000/SAFE
/// (CrossCheckWindow, Export/CrossCheck.cs). Không thay kết quả hiện hành; cần kết quả của chương trình – chạy phân tích nếu chưa có
/// hoặc nếu kết quả hiện hành là kết quả nhập.</summary>
public partial class MainWindow
{
    private async void MnuCrossCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_p.Frames.Count == 0)
        {
            MessageBox.Show(this, "Dự án chưa có mô hình khung để so sánh.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ApplyUiToProject(false);
        if (_res == null || _res.External != null)
        {
            string why = _res == null ? "Chưa có kết quả phân tích của chương trình." : $"Kết quả hiện hành được nhập từ {_res.External.Software}.";
            var r = MessageBox.Show(this, why + "\nChạy phân tích của Việt Const (F5) trước khi so sánh?", AppTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (r != MessageBoxResult.OK) return;
            bool ok;
            _suppressImportedConfirm = true;   // đã hỏi ở trên
            try { ok = await RunAnalysis(); }
            finally { _suppressImportedConfirm = false; }
            if (!ok || _res == null || _res.External != null) return;
        }
        try
        {
            var w = new CrossCheckWindow(_p, _res) { Owner = this };
            w.ShowDialog();
            if (w.Report != null)
                SetStatus($"So sánh với {w.Report.Software}: {w.Report.Members.Count(m => m.CaseCount > 0)} cấu kiện – {CrossCheckWindowLevel(w.Report.Overall)}.");
        }
        catch (Exception ex) { ShowError("Lỗi so sánh với ETABS/SAP", ex); }
    }

    private static string CrossCheckWindowLevel(KetCauVXT.Export.CrossLevel l) => KetCauVXT.Export.CrossCheckReport.LevelText(l).ToLowerInvariant();
}
