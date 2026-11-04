using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Phân tích → Kiểm tra mô hình… (Analyze → Check Model của ETABS), mở không chặn: danh sách vấn đề (mức độ, mã, tầng, nội dung)
/// từ <see cref="ModelCheck.Run"/>; bấm đúp → chọn và phóng tới đối tượng trên mặt bằng; "Sửa mục đang chọn" / "Sửa tự động tất cả"
/// (<see cref="ModelCheck.Fix"/>) báo cửa sổ chính để lưu hoàn tác (Ctrl+Z).</summary>
internal sealed class ModelCheckWindow : Window
{
    private readonly Func<Project> _getP;
    private readonly Func<string> _snapshot;
    private readonly Action<string, string> _commit;
    private readonly Action<ModelIssue> _zoom;
    private readonly DataGrid _grid = DialogUi.NewGrid();
    private readonly TextBlock _summary = new() { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 2), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { Foreground = Brushes.DarkSlateBlue, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _tol = new() { Width = 50, Text = "5" };
    private readonly CheckBox _chkSlab = new() { Content = "Cạnh sàn không có dầm", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
    private readonly CheckBox _chkPat = new() { Content = "Trường hợp tải rỗng", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
    private readonly CheckBox _chkInfo = new() { Content = "Hiện mục thông tin", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };

    public ModelCheckReport Report { get; private set; }

    public ModelCheckWindow(Func<Project> getP, Func<string> snapshot, Action<string, string> commit, Action<ModelIssue> zoom)
    {
        _getP = getP; _snapshot = snapshot; _commit = commit; _zoom = zoom;
        DialogUi.Setup(this, "Kiểm tra mô hình (Check Model)", 1000, 560);
        _grid.IsReadOnly = true;
        _grid.Columns.Add(DialogUi.Text("Mức độ", "SeverityName", true, 76));
        _grid.Columns.Add(DialogUi.Text("Mã", "Code", true, 150));
        _grid.Columns.Add(DialogUi.Text("Tầng", "Story", true, 60));
        _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Sửa được", Binding = new Binding("Fixable") { Mode = BindingMode.OneWay }, IsReadOnly = true, Width = 64 });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Nội dung", Binding = new Binding("Message") { Mode = BindingMode.OneWay }, IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            ElementStyle = WrapStyle()
        });
        var rs = new Style(typeof(DataGridRow));
        rs.Triggers.Add(Trig(IssueSeverity.Error, Color.FromRgb(255, 214, 214)));
        rs.Triggers.Add(Trig(IssueSeverity.Warning, Color.FromRgb(255, 239, 200)));
        rs.Triggers.Add(Trig(IssueSeverity.Info, Color.FromRgb(232, 240, 252)));
        _grid.RowStyle = rs;
        _grid.MouseDoubleClick += (s, e) => { if (_grid.SelectedItem is ModelIssue i) _zoom?.Invoke(i); };
        _chkInfo.Checked += (s, e) => Show(); _chkInfo.Unchecked += (s, e) => Show();

        var bar = DialogUi.Row(
            DialogUi.Btn("Kiểm tra lại", (s, e) => RunCheck(), true),
            DialogUi.Btn("Phóng tới", (s, e) => { if (_grid.SelectedItem is ModelIssue i) _zoom?.Invoke(i); }),
            DialogUi.Btn("Sửa mục đang chọn", (s, e) => FixSelected()),
            DialogUi.Btn("Sửa tự động tất cả", (s, e) => FixAll()),
            new Border { Width = 12 },
            DialogUi.Label("Dung sai nút (mm)"), _tol, new Border { Width = 8 }, _chkSlab, _chkPat, _chkInfo);
        var dock = new DockPanel { Margin = new Thickness(10) };
        var top = new StackPanel();
        top.Children.Add(bar); top.Children.Add(_summary);
        DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
        var bottom = new StackPanel();
        bottom.Children.Add(DialogUi.Hint("Sửa tự động: gộp nút trùng, xoá thanh dài 0 / thanh trùng (chuyển tải sang thanh giữ lại), thêm nút tại giao điểm dầm và chia thanh, "
                                          + "chia thanh tại nút nằm giữa, xoá nút tự do, xoá tải mồ côi, bỏ thành phần tổ hợp thiếu. Hoàn tác bằng Ctrl+Z trên cửa sổ chính. "
                                          + "Bấm đúp một dòng để chọn và phóng tới đối tượng."));
        bottom.Children.Add(_status);
        bottom.Children.Add(DialogUi.Row(DialogUi.Btn("Đóng", (s, e) => Close())));
        DockPanel.SetDock(bottom, Dock.Bottom); dock.Children.Add(bottom);
        dock.Children.Add(_grid);
        Content = dock;
        RunCheck();
    }

    private static Style WrapStyle()
    {
        var st = new Style(typeof(TextBlock));
        st.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        return st;
    }

    private static DataTrigger Trig(IssueSeverity sev, Color c)
    {
        var t = new DataTrigger { Binding = new Binding("Severity"), Value = sev };
        t.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(c)));
        return t;
    }

    private ModelCheckOptions Options() => new()
    {
        NodeTolerance = Math.Clamp(DialogUi.ParseNum(_tol.Text, 5), 0.1, 100) / 1000.0,
        CheckSlabEdges = _chkSlab.IsChecked == true,
        CheckEmptyPatterns = _chkPat.IsChecked == true
    };

    /// <summary>Chạy kiểm tra trên dự án hiện hành và hiện kết quả.</summary>
    public ModelCheckReport RunCheck()
    {
        var p = _getP();
        if (p == null) return null;
        try { Report = ModelCheck.Run(p, Options()); }
        catch (Exception ex) { _status.Text = "Lỗi khi kiểm tra: " + ex.Message; return null; }
        Show();
        return Report;
    }

    private void Show()
    {
        if (Report == null) return;
        bool info = _chkInfo.IsChecked == true;
        _grid.ItemsSource = Report.Issues.Where(i => info || i.Severity != IssueSeverity.Info)
            .OrderBy(i => i.Severity).ThenBy(i => i.Code).ToList();
        _summary.Text = Report.Summary;
        _summary.Foreground = Report.Errors > 0 ? Brushes.DarkRed : Report.Warnings > 0 ? Brushes.DarkGoldenrod : Brushes.DarkGreen;
    }

    private void Fix(IEnumerable<string> codes)
    {
        var p = _getP();
        if (p == null) return;
        string snap = _snapshot();
        ModelFixResult r;
        try { r = ModelCheck.Fix(p, codes, Options()); }
        catch (Exception ex) { _status.Text = "Không sửa được: " + ex.Message; return; }
        if (r.Total == 0) { _status.Text = "Không có gì cần sửa."; RunCheck(); return; }
        string msg = "Kiểm tra mô hình – đã sửa: " + r;
        _commit(snap, msg);
        _status.Text = msg + ". (Ctrl+Z để hoàn tác)";
        RunCheck();
    }

    private void FixSelected()
    {
        var codes = _grid.SelectedItems.OfType<ModelIssue>().Where(i => i.Fixable).Select(i => i.Code).Distinct().ToList();
        if (codes.Count == 0) { _status.Text = "Chọn mục có cột 'Sửa được' để sửa."; return; }
        Fix(codes);
    }

    /// <summary>Sửa mọi mục sửa được.</summary>
    public void FixAll() => Fix(null);
}
