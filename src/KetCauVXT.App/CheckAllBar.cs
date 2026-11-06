using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Ui;

namespace KetCauVXT.App;

/// <summary>Hàng điều khiển "☐ Tất cả (3/10) · Chọn hết · Bỏ hết · Đảo" đặt trên một danh sách có ô tích (layer, nhóm nét, thẻ…).
/// Ô "Tất cả" ba trạng thái phản ánh danh sách (tích hết / không ô nào / một phần). Mọi thao tác áp dụng MỘT LẦN cho cả lô:
/// hàm <c>apply</c> nhận bộ đổi giá trị (chỉ số, giá trị cũ) → giá trị mới, bên gọi tự tạm dừng cập nhật từng dòng rồi vẽ lại một lần.
/// Chỉ các dòng đang hiện (sau lọc) được tính và đổi.</summary>
internal sealed class CheckAllBar : WrapPanel
{
    private readonly Func<IReadOnlyList<bool>> _states;
    private readonly Action<Func<int, bool, bool>> _apply;
    private readonly CheckBox _all;
    private readonly TextBlock _count;
    private bool _busy;

    /// <param name="states">Giá trị ô tích của các dòng đang hiện (theo thứ tự).</param>
    /// <param name="apply">Áp cho cả lô: gọi f(i, cũ) cho từng dòng đang hiện i để lấy giá trị mới; bên gọi gán, rồi vẽ lại MỘT lần.</param>
    public CheckAllBar(Func<IReadOnlyList<bool>> states, Action<Func<int, bool, bool>> apply, string allLabel = "Tất cả")
    {
        _states = states; _apply = apply;
        Orientation = Orientation.Horizontal;
        Margin = new Thickness(0, 2, 0, 2);
        _all = new CheckBox { Content = allLabel, IsThreeState = true, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 0, 4, 0),
                              ToolTip = "Tích / bỏ tích tất cả (đang tích hết → bỏ hết; còn lại → tích hết). Ctrl+A trong danh sách: như bấm ô này." };
        _all.Click += (s, e) => { var st = CheckAll.State(_states()); Run(CheckAll.ToggleTarget(st) ? "all" : "none"); };
        _count = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 8, 0) };
        Children.Add(_all);
        Children.Add(_count);
        Children.Add(Btn("☑ Chọn hết", "all", "Tích tất cả (các dòng đang hiện)"));
        Children.Add(Btn("☐ Bỏ hết", "none", "Bỏ tích tất cả (các dòng đang hiện)"));
        Children.Add(Btn("⇄ Đảo", "invert", "Đảo chọn: dòng đang tích thành bỏ, dòng bỏ thành tích"));
        Loaded += (s, e) => Refresh();
    }

    private Button Btn(string text, string mode, string tip)
    {
        var b = new Button { Content = text, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 4, 0), ToolTip = tip, VerticalAlignment = VerticalAlignment.Center };
        b.Click += (s, e) => Run(mode);
        return b;
    }

    /// <summary>Thực hiện lệnh "all" / "none" / "invert" / "toggle".</summary>
    public void Run(string mode)
    {
        if (_busy) return;
        var cur = _states() ?? Array.Empty<bool>();
        var next = CheckAll.Apply(cur, mode);
        _busy = true;
        try { _apply((i, old) => i >= 0 && i < next.Length ? next[i] : old); }
        finally { _busy = false; }
        Refresh();
    }

    /// <summary>Cập nhật ô "Tất cả" và số đếm theo danh sách hiện tại (gọi khi một dòng đổi / danh sách nạp lại / đổi bộ lọc).</summary>
    public void Refresh()
    {
        if (_busy) return;
        var cur = _states() ?? Array.Empty<bool>();
        _busy = true;
        try { _all.IsChecked = CheckAll.State(cur); }
        finally { _busy = false; }
        _count.Text = "(" + CheckAll.CountText(cur) + ")";
        IsEnabled = cur.Count > 0;
    }

    /// <summary>Theo dõi một vùng chứa ô tích (WrapPanel / StackPanel / DataGrid): ô tích bên trong đổi → cập nhật ô "Tất cả";
    /// Ctrl+A khi đang ở trong vùng → như bấm ô "Tất cả" (DataGrid giữ Ctrl+A chọn dòng mặc định nếu <paramref name="ctrlA"/> = false).</summary>
    public CheckAllBar Watch(UIElement container, bool ctrlA = true)
    {
        if (container == null) return this;
        RoutedEventHandler h = (s, e) => { if (!_busy) Dispatcher.BeginInvoke(new Action(Refresh)); };
        container.AddHandler(ToggleButton.CheckedEvent, h);
        container.AddHandler(ToggleButton.UncheckedEvent, h);
        container.AddHandler(ToggleButton.IndeterminateEvent, h);
        if (ctrlA)
            container.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) != 0 && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
                { Run("toggle"); e.Handled = true; }
            };
        return this;
    }

    /// <summary>Dựng thanh cho một tập ô tích WPF (CheckBox) có trình xử lý Checked/Unchecked riêng:
    /// <paramref name="suspend"/>(true) trước khi đổi cả lô (để trình xử lý từng ô bỏ qua), (false) sau đó; rồi <paramref name="after"/> chạy một lần.</summary>
    public static CheckAllBar ForBoxes(Func<IEnumerable<CheckBox>> boxes, Action<bool> suspend, Action after, string allLabel = "Tất cả")
    {
        List<CheckBox> Live() => (boxes?.Invoke() ?? Enumerable.Empty<CheckBox>()).Where(b => b != null).ToList();
        return new CheckAllBar(
            () => Live().Select(b => b.IsChecked == true).ToList(),
            f =>
            {
                var list = Live();
                suspend?.Invoke(true);
                try { for (int i = 0; i < list.Count; i++) list[i].IsChecked = f(i, list[i].IsChecked == true); }
                finally { suspend?.Invoke(false); }
                after?.Invoke();
            }, allLabel);
    }
}
