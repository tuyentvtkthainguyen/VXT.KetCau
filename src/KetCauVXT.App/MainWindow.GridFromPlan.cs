using System.Windows;
using KetCauVXT.Import.Arch;

namespace KetCauVXT.App;

/// <summary>Nút "Tải mặt bằng để lấy kích thước…" trong ô Lưới trục và tầng (GridFromPlanDialog.cs):
/// điền bước trục / chiều cao tầng; với bản vẽ CAD (đã chọn nguồn trục, trục qua tim / mép cột) còn cập nhật toàn bộ dữ liệu mặt bằng
/// vào mô hình: dựng lại khung theo lưới mới (tuỳ chọn), tên trục, tiết diện + lệch tâm cột, tường trên trục → tải tường,
/// công năng ô sàn theo tên phòng, lỗ cầu thang / thông tầng (PlanAxisPicker.ApplyToProject). Một bước hoàn tác (Ctrl+Z).</summary>
public partial class MainWindow
{
    private void BtnGridFromPlan_Click(object sender, RoutedEventArgs e)
    {
        var d = new GridFromPlanDialog(txtSpansX.Text, txtSpansY.Text, txtHeights.Text) { Owner = this };
        if (d.ShowDialog() != true) return;
        if (!string.IsNullOrWhiteSpace(d.SpansX)) txtSpansX.Text = d.SpansX;
        if (!string.IsNullOrWhiteSpace(d.SpansY)) txtSpansY.Text = d.SpansY;
        if (!string.IsNullOrWhiteSpace(d.Heights)) txtHeights.Text = d.Heights;
        var r = d.PickResult;
        if (r == null || r.AxesX.Count < 2 || r.AxesY.Count < 2)
        {
            MessageBox.Show(this, "Đã điền bước trục và chiều cao tầng từ mặt bằng.\nKiểm tra lại rồi bấm tạo / cập nhật mô hình như bình thường.", "Lưới trục và tầng",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var o = d.ApplyOptions;
        o.Concrete = _p.Concrete?.ConcreteColumn ?? "B25";
        bool gridMatches = _p.Grids.Count(g => g.Dir == KetCauVXT.Model.GridDir.X) == r.AxesX.Count && _p.Grids.Count(g => g.Dir == KetCauVXT.Model.GridDir.Y) == r.AxesY.Count
                           && _p.Frames.Count > 0 && SameSpans(r);
        bool rebuild = d.Rebuild || !gridMatches;
        var items = r.Summary().Where(t => t.Key == "grid" || (t.Key == "columns" && (o.ColumnSections || o.ColumnOffsets)) || (t.Key == "walls" && o.Walls)
                                        || (t.Key == "rooms" && o.RoomUses) || (t.Key == "openings" && o.Openings))
                     .Select(t => "• " + t.Item + ": " + t.Detail);
        string msg = "Cập nhật dữ liệu mặt bằng vào mô hình:\n" + string.Join("\n", items)
                     + (rebuild ? "\n\nMô hình hiện tại sẽ được DỰNG LẠI theo lưới trục mới (khung, sàn, tải mặc định) rồi áp dụng các mục trên." : "\n\nÁp dụng lên mô hình hiện có (lưới trục trùng khớp).")
                     + "\nHoàn tác được bằng Ctrl+Z.";
        if (MessageBox.Show(this, msg, "Cập nhật mặt bằng từ bản vẽ CAD", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            SetStatus("Đã điền bước trục; chưa cập nhật mô hình.");
            return;
        }
        string snap = Snapshot();
        try
        {
            if (rebuild)
            {
                BuildFromGridUi(false);
                if (_p.Grids.Count(g => g.Dir == KetCauVXT.Model.GridDir.X) != r.AxesX.Count) { SetStatus("Không dựng được mô hình theo lưới mới – kiểm tra ô nhập."); return; }
            }
            var log = PlanAxisPicker.ApplyToProject(_p, r, o);
            PushUndo(snap);
            _res = null; _design = null; _sel = null;
            MarkDirty();
            RefreshAll(true);
            SetStatus("Đã cập nhật mặt bằng từ bản vẽ CAD: " + string.Join(" ", log));
            MessageBox.Show(this, string.Join("\n", log.Select(l => "• " + l)), "Cập nhật mặt bằng từ bản vẽ CAD", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError("Không cập nhật được mô hình theo mặt bằng", ex); }
    }

    /// <summary>Lưới trục mô hình trùng bước trục đọc được (sai số 5 mm).</summary>
    private bool SameSpans(AxisPickResult r)
    {
        var gx = _p.Grids.Where(g => g.Dir == KetCauVXT.Model.GridDir.X).OrderBy(g => g.Coord).Select(g => g.Coord).ToList();
        var gy = _p.Grids.Where(g => g.Dir == KetCauVXT.Model.GridDir.Y).OrderBy(g => g.Coord).Select(g => g.Coord).ToList();
        bool Same(List<double> c, List<double> spans) => c.Count == spans.Count + 1 && spans.Select((s, i) => Math.Abs(c[i + 1] - c[i] - s) < 0.005).All(b => b);
        return Same(gx, r.SpansX) && Same(gy, r.SpansY);
    }
}
