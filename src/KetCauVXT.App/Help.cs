using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KetCauVXT.App;

// =====================================================================
// TRỢ GIÚP THEO NGỮ CẢNH – dùng chung cho mọi cửa sổ:
//   • Help.Id (thuộc tính gắn kèm): HelpId của mục hướng dẫn (Docs/UserGuide.Content.cs) cho một phần tử / cửa sổ;
//   • Help.Attach(window, <HelpId>): đặt Help.Id và thêm dải "? Hướng dẫn (F1)" ở đầu cửa sổ (một dòng trong hàm dựng hộp thoại);
//   • HelpButton.Create(<HelpId>): nút "?" nhỏ để đặt vào thanh nút bất kỳ;
//   • F1 ở bất kỳ cửa sổ nào (MainWindow.UserGuide.cs đăng ký): Help.Id của phần tử đang có focus → của cửa sổ →
//     HelpContext.ForWindow(tên lớp cửa sổ) → ngữ cảnh cửa sổ chính.
// =====================================================================
public static class Help
{
    public static readonly DependencyProperty IdProperty = DependencyProperty.RegisterAttached(
        "Id", typeof(string), typeof(Help), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    public static string GetId(DependencyObject d) => d?.GetValue(IdProperty) as string;
    public static void SetId(DependencyObject d, string value) => d?.SetValue(IdProperty, value);

    /// <summary>Hàm mở cửa sổ hướng dẫn tại một HelpId (cửa sổ chính gán khi khởi động).</summary>
    public static Action<string> Opener { get; set; }

    /// <summary>Mở hướng dẫn tại mục helpId (null → mục mặc định).</summary>
    public static void Show(string helpId)
    {
        try { Opener?.Invoke(helpId); }
        catch (Exception ex) { MessageBox.Show("Không mở được hướng dẫn sử dụng:\n" + ex.Message, "Kết cấu VXT", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    /// <summary>Đặt Help.Id cho cửa sổ và chèn dải "? Hướng dẫn (F1)" ở trên cùng nội dung (khi cửa sổ nạp xong).</summary>
    public static void Attach(Window w, string helpId)
    {
        if (w == null) return;
        SetId(w, helpId);
        if (w.IsLoaded) Wrap(w, helpId);
        else w.Loaded += (s, e) => Wrap(w, helpId);
    }

    private static void Wrap(Window w, string helpId)
    {
        if (w.Content is not UIElement old || (old is FrameworkElement fe && fe.Tag as string == "kcvxt-help-wrap")) return;
        w.Content = null;
        var strip = new DockPanel { LastChildFill = false, Margin = new Thickness(6, 2, 6, 0) };
        var btn = HelpButton.Create(helpId, true);
        DockPanel.SetDock(btn, Dock.Right);
        strip.Children.Add(btn);
        var dock = new DockPanel { Tag = "kcvxt-help-wrap" };
        DockPanel.SetDock(strip, Dock.Top);
        dock.Children.Add(strip);
        dock.Children.Add(old);
        w.Content = dock;
    }

    /// <summary>HelpId cho phần tử đang focus trong cửa sổ w (theo cây phần tử), rồi của cửa sổ, rồi theo tên lớp cửa sổ.</summary>
    public static string Resolve(Window w)
    {
        DependencyObject d = Keyboard.FocusedElement as DependencyObject;
        while (d != null)
        {
            string id = d.ReadLocalValue(IdProperty) as string;
            if (!string.IsNullOrWhiteSpace(id)) return id;
            if (ReferenceEquals(d, w)) break;
            d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        string wid = w == null ? null : w.ReadLocalValue(IdProperty) as string;
        if (!string.IsNullOrWhiteSpace(wid)) return wid;
        return w == null ? null : KetCauVXT.Docs.HelpContext.ForWindow(w.GetType().Name);
    }
}

/// <summary>Nút "?" mở hướng dẫn tại một mục.</summary>
public static class HelpButton
{
    /// <summary>Nút "?" (withText = true: "? Hướng dẫn (F1)").</summary>
    public static Button Create(string helpId, bool withText = false)
    {
        var b = new Button
        {
            Content = withText ? "?  Hướng dẫn (F1)" : "?",
            Padding = withText ? new Thickness(8, 0, 8, 1) : new Thickness(6, 0, 6, 1),
            Margin = new Thickness(2, 0, 0, 2),
            MinWidth = 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 80, 160)),
            ToolTip = "Mở hướng dẫn sử dụng tại mục này (F1)",
            Focusable = false,
        };
        Help.SetId(b, helpId);
        b.Click += (s, e) => Help.Show(helpId);
        return b;
    }
}
