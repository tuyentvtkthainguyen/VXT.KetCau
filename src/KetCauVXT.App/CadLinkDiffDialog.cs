using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using KetCauVXT.Import.Link;

namespace KetCauVXT.App;

/// <summary>Hộp thoại "Cập nhật mô hình từ link": danh sách khác biệt bản vẽ ↔ mô hình của một tầng (thêm / dời / không còn trên bản vẽ),
/// người dùng đánh dấu mục cần áp dụng. Mục "xoá" và "dời cột" mặc định không đánh dấu. Mặt bằng phía sau vẽ xem trước
/// (xanh = thêm, cam = dời, đỏ = xoá; nét đậm = đã đánh dấu).</summary>
internal sealed class CadLinkDiffDialog : Window
{
    private readonly CadLinkDiff _diff;
    private readonly Action _preview;
    private readonly ObservableCollection<DiffRow> _rows = new();
    private readonly TextBlock _count;

    public CadLinkDiffDialog(CadLinkDiff diff, Action preview)
    {
        _diff = diff; _preview = preview;
        DialogUi.Setup(this, $"Cập nhật mô hình từ bản vẽ link – tầng {diff.Story}", 860, 560);
        foreach (var it in diff.Items) _rows.Add(new DiffRow(it, Changed));
        var grid = DialogUi.NewGrid();
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Áp dụng", Binding = new Binding("Apply") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = 62 });
        grid.Columns.Add(DialogUi.Text("Thao tác", "Action", true, 70));
        grid.Columns.Add(DialogUi.Text("Cấu kiện", "Element", true, 70));
        grid.Columns.Add(DialogUi.Text("Mô tả", "Description", true, 560));
        grid.ItemsSource = _rows;
        var st = new Style(typeof(DataGridRow));
        st.Setters.Add(new Setter(DataGridRow.ForegroundProperty, new Binding("Color")));
        grid.RowStyle = st;

        var head = new StackPanel();
        head.Children.Add(DialogUi.Label(diff.Summary, double.NaN, true));
        head.Children.Add(DialogUi.Hint("Đánh dấu các thay đổi cần áp dụng vào mô hình (Ctrl+Z để hoàn tác sau khi áp dụng). Mục \"không còn trên bản vẽ\" và \"dời cột\" mặc định không đánh dấu – kiểm tra rồi tự chọn. Mặt bằng phía sau: xanh = thêm, cam = dời, đỏ = xoá."));
        if (diff.Warnings.Count > 0)
            head.Children.Add(new TextBlock { Text = "Lưu ý: " + string.Join(" · ", diff.Warnings.Take(4)), Foreground = Brushes.DarkOrange, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });

        void SetAll(Func<DiffRow, bool> pick, bool v) { foreach (var r in _rows.Where(pick)) r.SetApply(v); _preview?.Invoke(); Changed(); }
        var bAdd = DialogUi.Btn("Chọn mọi mục thêm", (s, e) => SetAll(r => r.Item.Action == CadDiffAction.Add, true));
        var bAll = DialogUi.Btn("Chọn tất cả", (s, e) => SetAll(r => true, true));
        var bNone = DialogUi.Btn("Bỏ chọn tất cả", (s, e) => SetAll(r => true, false));
        _count = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray, Margin = new Thickness(8, 0, 0, 0) };
        var ok = DialogUi.Btn("Áp dụng", (s, e) => { DialogUi.Commit(grid); if (_diff.Items.Any(i => i.Apply)) DialogResult = true; else MessageBox.Show(this, "Chưa đánh dấu thay đổi nào.", Title); }, true);
        ok.IsDefault = true;
        var cancel = DialogUi.Btn("Huỷ", (s, e) => DialogResult = false);
        cancel.IsCancel = true;
        var bar = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        var right = DialogUi.Row(ok, cancel);
        DockPanel.SetDock(right, Dock.Right); bar.Children.Add(right);
        bar.Children.Add(DialogUi.Row(bAdd, bAll, bNone, _count));

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);
        DockPanel.SetDock(bar, Dock.Bottom); root.Children.Add(bar);
        root.Children.Add(grid);
        Content = root;
        Changed();
    }

    private void Changed()
    {
        if (_count != null) _count.Text = $"Đã đánh dấu {_diff.Items.Count(i => i.Apply)} / {_diff.Items.Count} mục.";
    }

    internal sealed class DiffRow : RowBase
    {
        private readonly Action _changed;
        public CadDiffItem Item { get; }
        public DiffRow(CadDiffItem it, Action changed) { Item = it; _changed = changed; }
        public bool Apply { get => Item.Apply; set { if (Item.Apply == value) return; Item.Apply = value; Raise(); _changed?.Invoke(); } }
        public void SetApply(bool v) { Item.Apply = v; Raise(nameof(Apply)); }
        public string Action => Item.ActionText;
        public string Element => Item.ElementText;
        public string Description => Item.Description;
        public Brush Color => Item.Action switch
        {
            CadDiffAction.Add => Brushes.DarkGreen,
            CadDiffAction.Move => Brushes.DarkOrange,
            _ => Brushes.Firebrick
        };
    }
}
