using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace KetCauVXT.App;

/// <summary>[Khung trái] Khung nhập liệu bên trái (Công trình, Địa chất, Tiết diện, Tải trọng, Kiểm tra, Thiết kế, Triển khai) là MỘT khung cố định:
/// hàng thẻ một dòng (không nhảy 2 hàng khi khung hẹp) có thanh trượt ngang; nội dung mỗi thẻ giữ bề rộng tối thiểu
/// <see cref="LeftMinContentWidth"/> – khung hẹp hơn thì hiện thanh trượt ngang để kéo xem / chọn ô bị che (không bị cắt chữ, cắt ô nhập).
/// Shift + lăn chuột: cuộn ngang; lăn chuột trên hàng thẻ: cuộn hàng thẻ.</summary>
public partial class MainWindow
{
    internal const double LeftMinContentWidth = 540;
    private const string LeftScrollTag = "HScrollWrap";

    private const string LeftTabTemplate = @"
<ControlTemplate TargetType='TabControl' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <DockPanel KeyboardNavigation.TabNavigation='Local' SnapsToDevicePixels='True'>
    <ScrollViewer x:Name='PART_TabStripScroll' DockPanel.Dock='Top' HorizontalScrollBarVisibility='Auto' VerticalScrollBarVisibility='Disabled' Focusable='False' Margin='2,2,2,0'>
      <StackPanel Orientation='Horizontal' IsItemsHost='True' Panel.ZIndex='1' KeyboardNavigation.TabIndex='1'/>
    </ScrollViewer>
    <Border BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Background='{TemplateBinding Background}'
            Padding='{TemplateBinding Padding}' KeyboardNavigation.TabNavigation='Local' KeyboardNavigation.DirectionalNavigation='Contained' KeyboardNavigation.TabIndex='2'>
      <ContentPresenter x:Name='PART_SelectedContentHost' ContentSource='SelectedContent' Margin='0'/>
    </Border>
  </DockPanel>
</ControlTemplate>";

    private void SetupLeftPanelScroll()
    {
        if (tabLeft == null) return;
        try { tabLeft.Template = (ControlTemplate)XamlReader.Parse(LeftTabTemplate); }
        catch (Exception) { /* giữ mẫu mặc định nếu lỗi */ }
        tabLeft.ApplyTemplate();
        if (tabLeft.Template?.FindName("PART_TabStripScroll", tabLeft) is ScrollViewer strip)
            strip.PreviewMouseWheel += (s, e) => { strip.ScrollToHorizontalOffset(strip.HorizontalOffset - e.Delta / 3.0); e.Handled = true; };
        var dpd = DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(TabItem));
        foreach (var ti in tabLeft.Items.OfType<TabItem>())
        {
            WrapLeftTab(ti);
            dpd?.AddValueChanged(ti, (s, e) => WrapLeftTab((TabItem)s));   // nội dung dựng lại bằng mã (Địa chất…) → bọc lại
        }
        tabLeft.SelectionChanged += (s, e) => { if (e.OriginalSource == tabLeft && tabLeft.SelectedItem is TabItem t) { WrapLeftTab(t); t.BringIntoView(); } };
        // Shift + lăn chuột: cuộn ngang khung gần nhất còn cuộn được
        tabLeft.PreviewMouseWheel += (s, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0) return;
            for (var d = e.OriginalSource as DependencyObject; d != null && d != tabLeft; d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
                if (d is ScrollViewer sv && sv.ScrollableWidth > 0)
                {
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta / 2.0);
                    e.Handled = true;
                    return;
                }
        };
    }

    /// <summary>Nội dung thẻ: ScrollViewer dọc sẵn có → bật cuộn ngang + giữ bề rộng tối thiểu; nội dung khác → bọc ScrollViewer ngang.</summary>
    private void WrapLeftTab(TabItem ti)
    {
        if (ti?.Content is not FrameworkElement c) return;
        if (c is ScrollViewer sv0 && Equals(sv0.Tag, LeftScrollTag)) return;
        ScrollViewer sv;
        FrameworkElement inner;
        if (c is ScrollViewer existing && existing.Content is FrameworkElement ec)
        {
            sv = existing; inner = ec;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            if (sv.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled) sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
        else
        {
            ti.Content = null;
            sv = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = c, Focusable = false, Tag = LeftScrollTag };
            inner = c;
            ti.Content = sv;
        }
        sv.Tag = LeftScrollTag;
        inner.HorizontalAlignment = HorizontalAlignment.Left;
        void Fit()
        {
            double vw = sv.ViewportWidth > 0 ? sv.ViewportWidth : sv.ActualWidth;
            if (vw <= 0) return;
            double w = Math.Max(LeftMinContentWidth, Math.Floor(vw));
            if (Math.Abs(inner.Width - w) > 0.5 || double.IsNaN(inner.Width)) inner.Width = w;
        }
        sv.SizeChanged += (s, e) => Fit();
        sv.ScrollChanged += (s, e) => { if (e.ViewportWidthChange != 0) Fit(); };
        sv.Loaded += (s, e) => Fit();
        Fit();
    }
}
