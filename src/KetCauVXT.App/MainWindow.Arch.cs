using System.Windows;
using System.Windows.Input;
using KetCauVXT.Design;
using KetCauVXT.Import.Arch;

namespace KetCauVXT.App;

/// <summary>Nhập bản vẽ kiến trúc sơ bộ → triển khai kết cấu (lưới cột, dầm, sàn, tải) → thay thế mô hình hiện tại.</summary>
public partial class MainWindow
{
    private async void MnuImportArch_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        ApplyUiToProject(false);
        var dlg = new ArchImportDialog { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Proposal == null || dlg.Drawing == null) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var rep = ArchToStructure.Apply(_p, dlg.Drawing, dlg.Proposal, dlg.Options);
            Mouse.OverrideCursor = null;
            _res = null; _design = null; _sel = null;
            MarkDirty();
            LoadUiFromProject();
            RefreshAll(true);
            SetStatus($"Đã triển khai kết cấu từ bản vẽ kiến trúc: {rep.Columns} cột, {rep.Beams} dầm, {rep.Slabs} ô sàn – cần chạy lại phân tích.");
            MessageBox.Show(this, rep.ToString(), "Kết quả triển khai kết cấu từ bản vẽ kiến trúc", MessageBoxButton.OK,
                rep.Warnings.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            if (MessageBox.Show(this, "Tiết diện hiện là kích thước sơ bộ.\n\nChạy phân tích + thiết kế và TỰ TĂNG tiết diện các dầm, cột, sàn không đạt (lặp tới khi đạt)?",
                    AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                await RunAutoResize();
        }
        catch (NotImplementedException)
        {
            Mouse.OverrideCursor = null;
            MessageBox.Show(this, "Chức năng tạo mô hình từ bản vẽ kiến trúc chưa được cài đặt trong phiên bản hiện tại (đang phát triển).",
                AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            Mouse.OverrideCursor = null;
            ShowError("Lỗi tạo mô hình từ bản vẽ kiến trúc", ex);
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private async void MnuAutoResize_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (!_p.Frames.Any(f => f.IsConcrete))
        {
            MessageBox.Show(this, "Mô hình không có cấu kiện BTCT.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await RunAutoResize();
    }

    /// <summary>Phân tích → thiết kế → tăng tiết diện cấu kiện không đạt → lặp (tối đa 10 lần).</summary>
    private async Task RunAutoResize()
    {
        ApplyUiToProject(false);
        AutoResize.Result r = null;
        bool ok = await RunBusy("Lỗi tự tăng tiết diện", prog => { r = AutoResize.Run(_p, 10, prog); });
        if (!ok || r == null) return;
        _sel = null;
        MarkDirty(false);
        LoadUiFromProject();
        RefreshAll(true);
        _res = r.Analysis; _design = r.Design;
        RefreshLoads();
        RefreshCaseCombos();
        RefreshResults();
        RefreshDesign();
        SetStatus($"Tự tăng tiết diện: {r.Iterations} lần lặp – " + (r.AllOk ? "tất cả dầm, cột, sàn đạt." : "vẫn còn cấu kiện không đạt."));
        MessageBox.Show(this, r.ToString(), "Tự tăng tiết diện", MessageBoxButton.OK, r.AllOk ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
