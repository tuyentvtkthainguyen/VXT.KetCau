using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace KetCauVXT.App;

/// <summary>Số thực ↔ chuỗi, chấp nhận cả dấu phẩy và dấu chấm thập phân; chuỗi sai → giữ giá trị cũ.</summary>
internal sealed class NumConverter : IValueConverter
{
    public string Format { get; init; } = "0.###";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double d ? d.ToString(Format, CultureInfo.InvariantCulture) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = (value as string ?? "").Trim().Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : Binding.DoNothing;
    }
}

/// <summary>Lớp cơ sở cho dòng bảng có thông báo thay đổi.</summary>
internal abstract class RowBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected void Raise([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    /// <summary>Báo mọi cột thay đổi (cột tính toán phụ thuộc nhiều ô).</summary>
    protected void RaiseAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}

/// <summary>Tiện ích dựng giao diện hộp thoại bằng mã (không XAML).</summary>
internal static class DialogUi
{
    /// <summary>Thân hộp thoại cuộn được (chuột giữa / thanh cuộn) + hàng nút cố định ở đáy – dùng cho hộp thoại dài
    /// để không tràn màn hình. Cửa sổ cần đặt MaxHeight (vd. vùng làm việc màn hình).</summary>
    public static DockPanel ScrollBody(UIElement body, UIElement buttons)
    {
        var dock = new DockPanel { LastChildFill = true };
        if (buttons != null)
        {
            var bar = new Border { Child = buttons, BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 210)), BorderThickness = new Thickness(0, 1, 0, 0) };
            DockPanel.SetDock(bar, Dock.Bottom);
            dock.Children.Add(bar);
        }
        var sv = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, PanningMode = PanningMode.VerticalOnly };
        WheelToScroll(sv);
        dock.Children.Add(sv);
        return dock;
    }

    /// <summary>Chuột giữa cuộn vùng nội dung kể cả khi con trỏ đang trên ô nhập / hộp chọn (ComboBox đóng không ăn lăn chuột).</summary>
    public static void WheelToScroll(ScrollViewer sv)
    {
        sv.PreviewMouseWheel += (s, e) =>
        {
            if (e.Handled) return;
            if (e.OriginalSource is DependencyObject d)
            {
                // ComboBox đang mở danh sách hoặc ô lưới / danh sách có cuộn riêng: để chúng tự cuộn
                for (var x = d; x != null && !ReferenceEquals(x, sv); x = x is Visual || x is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(x) : LogicalTreeHelper.GetParent(x))
                {
                    if (x is ComboBox cb && cb.IsDropDownOpen) return;
                    if (x is ScrollViewer inner && inner.ScrollableHeight > 0) return;
                }
            }
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        };
    }

    /// <summary>Gắn toàn cục (App.OnStartup): mọi cửa sổ tự co theo nội dung mà cao hơn màn hình → giới hạn chiều cao theo
    /// vùng làm việc, bọc nội dung vào vùng cuộn chuột giữa, kéo về trong màn hình.</summary>
    public static void RegisterFitToScreen()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) =>
        {
            if (s is not Window w || !ReferenceEquals(e.OriginalSource, w)) return;
            try { FitToScreen(w); } catch (Exception) { }
        }));
    }

    public static void FitToScreen(Window w)
    {
        var wa = SystemParameters.WorkArea;
        double maxH = Math.Max(300, wa.Height - 24);
        if (w.SizeToContent == SizeToContent.Manual || w.ActualHeight <= maxH + 1) return;
        if (w.Content is UIElement c && w.Content is not ScrollViewer)
        {
            w.Content = null;
            var sv = new ScrollViewer { Content = c, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            WheelToScroll(sv);
            w.Content = sv;
        }
        w.MaxHeight = maxH;
        if (w.ResizeMode == ResizeMode.NoResize) w.ResizeMode = ResizeMode.CanResize;
        w.UpdateLayout();
        if (w.Top + w.ActualHeight > wa.Bottom) w.Top = Math.Max(wa.Top, wa.Bottom - w.ActualHeight);
        if (w.Top < wa.Top) w.Top = wa.Top;
    }

    public static DataGrid NewGrid()
    {
        return new DataGrid
        {
            AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, CanUserReorderColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.All,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(216, 216, 216)),
            VerticalGridLinesBrush = new SolidColorBrush(Color.FromRgb(216, 216, 216)),
            SelectionMode = DataGridSelectionMode.Extended, SelectionUnit = DataGridSelectionUnit.FullRow,
            Margin = new Thickness(0, 4, 0, 4)
        };
    }

    public static DataGridTextColumn Text(string header, string path, bool readOnly = false, double width = double.NaN)
    {
        var c = new DataGridTextColumn
        {
            Header = header, IsReadOnly = readOnly,
            Binding = new Binding(path) { Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }
        };
        if (!double.IsNaN(width)) c.Width = width;
        return c;
    }

    public static DataGridTextColumn Num(string header, string path, string fmt = "0.###", bool readOnly = false, double width = double.NaN)
    {
        var c = new DataGridTextColumn
        {
            Header = header, IsReadOnly = readOnly,
            Binding = new Binding(path)
            {
                Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                Converter = new NumConverter { Format = fmt }
            }
        };
        if (!double.IsNaN(width)) c.Width = width;
        if (readOnly)
        {
            var st = new Style(typeof(TextBlock));
            st.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.DimGray));
            st.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            c.ElementStyle = st;
        }
        return c;
    }

    public static DataGridComboBoxColumn Combo(string header, string path, System.Collections.IEnumerable items, double width = double.NaN)
    {
        var c = new DataGridComboBoxColumn
        {
            Header = header, ItemsSource = items,
            SelectedItemBinding = new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }
        };
        if (!double.IsNaN(width)) c.Width = width;
        return c;
    }

    public static Button Btn(string text, RoutedEventHandler click, bool bold = false)
    {
        var b = new Button { Content = text, Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 6, 0), MinWidth = 60 };
        if (bold) b.FontWeight = FontWeights.Bold;
        b.Click += click;
        return b;
    }

    public static StackPanel Row(params UIElement[] items)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        foreach (var i in items) sp.Children.Add(i);
        return sp;
    }

    /// <summary>Hàng nút tự xuống dòng khi khung hẹp (không bị cắt).</summary>
    public static WrapPanel Wrap(params UIElement[] items)
    {
        var wp = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        foreach (var i in items)
        {
            if (i is FrameworkElement fe) fe.Margin = new Thickness(fe.Margin.Left, fe.Margin.Top, fe.Margin.Right, Math.Max(fe.Margin.Bottom, 3));
            wp.Children.Add(i);
        }
        return wp;
    }

    /// <summary>Bảng nhiều cột: thanh trượt ngang luôn hiện để kéo xem / chọn cột, cố định cột đầu,
    /// Shift + lăn chuột (hoặc lăn ngang của chuột / touchpad) để cuộn ngang.</summary>
    public static void HScroll(DataGrid g, int frozen = 1)
    {
        g.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
        g.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        g.FrozenColumnCount = frozen;
        g.HorizontalAlignment = HorizontalAlignment.Stretch;
        g.PreviewMouseWheel += (s, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0) return;
            var sv = FindChild<ScrollViewer>(g);
            if (sv == null) return;
            sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta / 2.0);
            e.Handled = true;
        };
    }

    private static T FindChild<T>(DependencyObject d) where T : DependencyObject
    {
        if (d == null) return null;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
        {
            var c = VisualTreeHelper.GetChild(d, i);
            if (c is T t) return t;
            var r = FindChild<T>(c);
            if (r != null) return r;
        }
        return null;
    }

    public static TextBlock Label(string text, double width = double.NaN, bool bold = false)
    {
        var t = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), TextWrapping = TextWrapping.Wrap };
        if (!double.IsNaN(width)) t.Width = width;
        if (bold) t.FontWeight = FontWeights.Bold;
        return t;
    }

    public static TextBlock Hint(string text) =>
        new() { Text = text, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) };

    /// <summary>Kết thúc sửa ô đang dở (để giá trị vào dữ liệu) trước khi thao tác nút.</summary>
    public static void Commit(DataGrid g)
    {
        try { g.CommitEdit(DataGridEditingUnit.Cell, true); g.CommitEdit(DataGridEditingUnit.Row, true); }
        catch (InvalidOperationException) { }
    }

    public static void Setup(Window w, string title, double width, double height)
    {
        w.Title = title; w.Width = width; w.Height = height;
        w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        w.ShowInTaskbar = false;
        w.MinWidth = 420; w.MinHeight = 300;
        w.Background = SystemColors.ControlBrush;
        w.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape && Keyboard.FocusedElement is not TextBox) w.Close(); };
    }

    public static double ParseNum(string text, double def)
    {
        var s = (text ?? "").Trim().Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : def;
    }

    public static string F(double v, string fmt = "0.###") => v.ToString(fmt, CultureInfo.InvariantCulture);

    /// <summary>Hộp nhập một dòng chữ; null = huỷ.</summary>
    public static string InputBox(Window owner, string title, string prompt, string value)
    {
        var w = new Window { Owner = owner, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
        Setup(w, title, 380, 160);
        w.MinHeight = 0;
        var tb = new TextBox { Text = value ?? "", Margin = new Thickness(0, 4, 0, 8) };
        string result = null;
        var ok = Btn("Đồng ý", (s, e) => { result = tb.Text.Trim(); w.DialogResult = true; }, true);
        ok.IsDefault = true;
        var cancel = Btn("Huỷ", (s, e) => w.DialogResult = false);
        cancel.IsCancel = true;
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(Label(prompt));
        sp.Children.Add(tb);
        var bs = Row(ok, cancel); bs.HorizontalAlignment = HorizontalAlignment.Right;
        sp.Children.Add(bs);
        w.Content = sp;
        w.Loaded += (s, e) => { tb.Focus(); tb.SelectAll(); };
        return w.ShowDialog() == true && !string.IsNullOrEmpty(result) ? result : null;
    }
}
