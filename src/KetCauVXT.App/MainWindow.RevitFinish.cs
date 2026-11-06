using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KetCauVXT.App;

// =====================================================================
// [KIỂU REVIT – HOÀN THIỆN] tab Xem → nhóm "Hiện nhanh": bật / tắt ngay (không mở hộp thoại) thẻ dầm, cột, sàn, cửa,
// kích thước bước trục, tải trọng đã gán trên mặt bằng.
// =====================================================================
public partial class MainWindow
{
    private readonly List<(CheckBox Box, Func<bool> Get)> _quickVg = new();

    private FrameworkElement QuickVgGroup()
    {
        var grid = new UniformGridLite(2);
        CheckBox C(string text, string tip, Func<bool> get, Action<bool> set)
        {
            var c = new CheckBox { Content = text, IsChecked = get(), ToolTip = tip, FontSize = 11, Margin = new Thickness(3, 1, 6, 1), Focusable = false };
            c.Checked += (s, e) => { set(true); RedrawPlan(); };
            c.Unchecked += (s, e) => { set(false); RedrawPlan(); };
            _quickVg.Add((c, get));
            grid.Add(c);
            return c;
        }
        C("Thẻ dầm", "Tên + tiết diện dầm trên mặt bằng", () => _vg.BeamTags, v => _vg.BeamTags = v);
        C("Thẻ cột", "Tên cột", () => _vg.ColumnTags, v => _vg.ColumnTags = v);
        C("Thẻ sàn", "Tên ô sàn", () => _vg.SlabTags, v => _vg.SlabTags = v);
        C("Thẻ cửa", "Tag cửa đi / cửa sổ D1, S1…", () => _vg.DoorTags, v => _vg.DoorTags = v);
        C("Kích thước", "Kích thước bước trục", () => _vg.GridDims, v => _vg.GridDims = v);
        C("Tải trọng", "Tải đã gán (mũi tên + giá trị)", () => _showAssignedLoads, v =>
        {
            _showAssignedLoads = v;
            if (mnuShowLoads != null) mnuShowLoads.IsChecked = v;
        });
        return RvGroup("Hiện nhanh", grid.Panel);
    }

    /// <summary>Đồng bộ ô tích "Hiện nhanh" sau khi đổi ở hộp Hiển thị / Đồ hoạ.</summary>
    private void SyncQuickVg()
    {
        foreach (var (box, get) in _quickVg) if (box.IsChecked != get()) box.IsChecked = get();
    }

    /// <summary>Lưới 2 cột gọn cho các ô tích trong ribbon.</summary>
    private sealed class UniformGridLite
    {
        public readonly System.Windows.Controls.Primitives.UniformGrid Panel;
        public UniformGridLite(int columns) => Panel = new System.Windows.Controls.Primitives.UniformGrid { Columns = columns, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(2, 2, 2, 0) };
        public void Add(UIElement e) => Panel.Children.Add(e);
    }
}
