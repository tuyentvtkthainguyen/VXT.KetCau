using System.Windows;
using KetCauVXT.Detailing;
using KetCauVXT.Model;

namespace KetCauVXT.App;

// =====================================================================
// "Lưới trục + cột + tầng…": một hộp thoại tạo lưới trục mặt bằng, lưới cột (giao điểm có / không cột) và chiều cao tầng.
// Hộp thoại: GridColumnDialog.cs · lõi: Model/GridColumnSetup.cs. Có hoàn tác (Ctrl+Z).
// =====================================================================
public partial class MainWindow
{
    private void OpenGridColumnDialog()
    {
        if (_p == null || _busy) return;
        try
        {
            var plan = GridColumnSetup.FromProject(_p);
            if (plan.SpansX.Count == 0 && plan.SpansY.Count == 0)
            {
                // dự án chưa có lưới: lấy số liệu đang nhập ở tab Công trình
                plan.SpansX = SafeList(txtSpansX.Text); plan.SpansY = SafeList(txtSpansY.Text);
                plan.BaseElevation = Num(txtBaseElev, 0);
                var hs = SafeList(txtHeights.Text);
                if (plan.Stories.Count == 0)
                {
                    var names = GridColumnSetup.DefaultStoryNames(hs.Count);
                    for (int k = 0; k < hs.Count; k++) plan.Stories.Add(new StoryRow { Name = names[k], Height = hs[k] });
                }
                plan.ResizeMask();
                plan.SetAll(true);
            }
            if (plan.Stories.Count == 0) plan.Stories.Add(new StoryRow { Name = "T2", Height = 3.6 });
            bool hasModel = _p.Frames.Count > 0;
            var dlg = new GridColumnDialog(plan, hasModel) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            var pl = dlg.Plan;
            string snap = Snapshot();
            if (dlg.HeightsOnly)
            {
                int n = GridColumnSetup.ChangeStoryHeights(_p, pl.Stories.Select(s => s.Height).ToList(), pl.BaseElevation);
                AfterModelEdit($"Đã đổi chiều cao tầng: {string.Join(" / ", pl.Stories.Select(s => $"{s.Name} {s.Height:0.###} m"))} – dời {n} nút, giữ nguyên mô hình. "
                               + "Chạy lại phân tích để cập nhật nội lực.", snap);
                return;
            }
            if (hasModel && MessageBox.Show(this, "Xoá mô hình hiện tại và tạo lại khung BTCT theo lưới trục, lưới cột và tầng mới?", AppTitle,
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            ApplyUiToProject(false);
            var (_, msg) = GridColumnSetup.Rebuild(_p, pl, GridOptionsFromUi());
            AfterModelEdit(msg + $" {_p.Frames.Count} thanh, {_p.Slabs.Count} ô sàn.", snap);
        }
        catch (Exception ex) { ShowError("Không áp dụng được lưới trục / cột / tầng", ex); }
    }

    private void MnuGridColumn_Click(object sender, RoutedEventArgs e) => OpenGridColumnDialog();

    private static List<double> SafeList(string t) { try { return ParseList(t); } catch (FormatException) { return DetailShim.ParseList(t); } }
}
