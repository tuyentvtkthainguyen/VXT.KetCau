using System.IO;
using System.Windows;
using KetCauVXT.Report;

namespace KetCauVXT.App;

/// <summary>Menu Xuất → "Bảng khối lượng vật tư…" và "Lập thuyết minh tự động…".</summary>
public partial class MainWindow
{
    private QuantityWindow _qtyWin;
    private AutoReportDialog _autoReportDlg;

    private async void MnuQuantities_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        ApplyUiToProject(false);
        if (_design == null)
        {
            var ans = MessageBox.Show(this,
                "Chưa có kết quả thiết kế (hoặc mô hình đã sửa sau lần tính trước).\n\n"
                + "Chạy phân tích + thiết kế ngay để có khối lượng cốt thép và kích thước móng?\n"
                + "(Chọn “No” để chỉ tính khối lượng bê tông, ván khuôn, tường theo mô hình.)",
                AppTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (ans == MessageBoxResult.Cancel) return;
            if (ans == MessageBoxResult.Yes && !await EnsureDesign()) return;
        }
        ShowQuantityWindow();
    }

    /// <summary>Mở (không modal) cửa sổ bảng khối lượng với kết quả thiết kế hiện tại.</summary>
    private QuantityWindow ShowQuantityWindow()
    {
        if (_qtyWin != null && _qtyWin.IsLoaded) _qtyWin.Close();
        _qtyWin = new QuantityWindow(_p, _design, () => MarkDirty(false)) { Owner = this };
        _qtyWin.Show();
        return _qtyWin;
    }

    private void MnuAutoReport_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        ApplyUiToProject(false);
        ShowAutoReportDialog();
    }

    /// <summary>Mở (không modal) hộp thoại lập thuyết minh tự động.</summary>
    private AutoReportDialog ShowAutoReportDialog()
    {
        if (_autoReportDlg != null && _autoReportDlg.IsLoaded) _autoReportDlg.Close();
        _autoReportDlg = new AutoReportDialog(_p, ReportOptions.FromProject(_p), RunAutoReport) { Owner = this };
        _autoReportDlg.Show();
        return _autoReportDlg;
    }

    /// <summary>Lập thuyết minh theo tuỳ chọn: tự chạy phân tích + thiết kế nếu kết quả chưa có / đã cũ
    /// (mọi thao tác sửa mô hình xoá _res/_design), hỏi nơi lưu rồi ghi file .docx.</summary>
    private async Task RunAutoReport(ReportOptions o)
    {
        if (_busy || o == null) return;
        _p.ReportOptions = ReportOptions.Clone(o);
        MarkDirty(false);
        if (!await EnsureDesign()) return;
        string path = AskSave("Word (*.docx)|*.docx", "ThuyetMinhTinhToan.docx", "Lưu thuyết minh tính toán");
        if (path == null) return;
        var p = _p; var res = _res; var ds = _design; var opt = ReportOptions.Clone(o);
        if (await RunBusy("Lỗi lập thuyết minh", prog => { prog.Report("Đang lập thuyết minh tự động..."); CalculationReport.Export(p, res, ds, path, opt); }))
            Done("Đã lập thuyết minh tính toán:\n" + path + "\n\nKhi mở bằng Word, chọn “Yes” để cập nhật mục lục và số trang.");
    }

    /// <summary>Lập thuyết minh tự động ra file (không hộp thoại) với kết quả hiện có – dùng cho tự kiểm tra.</summary>
    internal void GenerateAutoReport(ReportOptions o, string path) =>
        CalculationReport.Export(_p, _res, _design, path, o ?? ReportOptions.FromProject(_p));

    /// <summary>Bước "Xuất tất cả": bảng khối lượng vật tư BangKhoiLuong.xlsx.</summary>
    private static void ExportQuantitiesTo(KetCauVXT.Model.Project p, KetCauVXT.Design.DesignSummary ds, string folder) =>
        QuantityReport.ExportExcel(p, QuantityCalculator.Compute(p, ds), Path.Combine(folder, QuantityReport.DefaultFileName));
}
