using System.Windows;
using KetCauVXT.Detailing;

namespace KetCauVXT.App;

/// <summary>Lanh tô từ cửa gắn tường của mô hình (cửa sổ chế độ Thiết kế đang mở) – Detailing/ModelToDetailing.cs.</summary>
public sealed partial class DetailingPlanCanvas
{
    private void LintelsFromModel()
    {
        var model = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault()?.CadLinkProject;
        if (model == null)
        {
            MessageBox.Show(_owner, "Mở mô hình ở chế độ Thiết kế (cửa sổ chính) có tường và cửa gắn tường trước.", "Lanh tô từ cửa mô hình");
            return;
        }
        _undo.Push(Plan);
        int nLv = Plan.Levels.Count, idx = Math.Max(0, _level.SelectedIndex);
        var log = ModelToDetailing.LintelsFromModel(model, P);
        if (Plan.Levels.Count != nLv) RefreshLevels(idx);
        _changed(); FillMembers(); UpdateHint(); _fitted = false; Redraw();
        _status.Text = string.Join(" ", log);
    }

    private void PenetrationsFromModel()
    {
        var model = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault()?.CadLinkProject;
        if (model == null)
        {
            MessageBox.Show(_owner, "Mở mô hình ở chế độ Thiết kế (cửa sổ chính) có bố trí điện nước trước.", "Lỗ xuyên từ điện nước");
            return;
        }
        var log = KetCauVXT.Detailing.ModelPenetrations.Apply(model, P);
        _changed(); FillMembers(); UpdateHint(); Redraw();
        _status.Text = string.Join(" ", log);
    }
}
