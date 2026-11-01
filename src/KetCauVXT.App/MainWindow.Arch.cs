using System.Windows;
using System.Windows.Input;
using KetCauVXT.Import.Arch;

namespace KetCauVXT.App;

/// <summary>Nhập bản vẽ kiến trúc sơ bộ → triển khai kết cấu (lưới cột, dầm, sàn, tải) → thay thế mô hình hiện tại.</summary>
public partial class MainWindow
{
    private void MnuImportArch_Click(object sender, RoutedEventArgs e)
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
}
