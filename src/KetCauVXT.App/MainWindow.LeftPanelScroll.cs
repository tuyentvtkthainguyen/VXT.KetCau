using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Controls.Primitives;
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

    // ---- [Thẻ theo lớp] lớp 1: nhóm (Nhập liệu | Kết quả – hồ sơ), lớp 2: thẻ của nhóm – chia đều bề rộng khung, không tràn ----
    private static readonly (string Name, string Tip, string[] Tabs)[] LeftGroups =
    {
        ("① Nhập liệu", "Công trình, địa chất, tiết diện, tải trọng", new[] { "Công trình", "Địa chất", "Tiết diện", "Tải trọng" }),
        ("② Kết quả – hồ sơ", "Kiểm tra, thiết kế, triển khai bản vẽ – thuyết minh", new[] { "Kiểm tra", "Thiết kế", "Triển khai" }),
    };

    private const string Ns = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'";

    private static string TabTemplateXaml(bool groups) => $@"
<ControlTemplate TargetType='TabControl' {Ns}>
  <DockPanel KeyboardNavigation.TabNavigation='Local' SnapsToDevicePixels='True'>
    <Border DockPanel.Dock='Top' Background='#F2F5F9' BorderBrush='#C3CEDC' BorderThickness='0,0,0,1' Padding='2,2,2,0'>
      <DockPanel>
        {(groups ? "<UniformGrid x:Name='PART_Groups' DockPanel.Dock='Top' Rows='1' Margin='0,0,0,3'/>" : "")}
        <UniformGrid IsItemsHost='True' Rows='1' KeyboardNavigation.TabIndex='1'/>
      </DockPanel>
    </Border>
    <Border BorderBrush='{{TemplateBinding BorderBrush}}' BorderThickness='0' Background='{{TemplateBinding Background}}' KeyboardNavigation.TabIndex='2'>
      <ContentPresenter x:Name='PART_SelectedContentHost' ContentSource='SelectedContent'/>
    </Border>
  </DockPanel>
</ControlTemplate>";

    private const string TabItemStyleXaml = @"
<Style TargetType='TabItem' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Setter Property='FontSize' Value='11.5'/>
  <Setter Property='HeaderTemplate'>
    <Setter.Value>
      <DataTemplate><TextBlock Text='{Binding}' TextTrimming='CharacterEllipsis' ToolTip='{Binding}'/></DataTemplate>
    </Setter.Value>
  </Setter>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='TabItem'>
        <Border x:Name='Bd' Margin='1,0,1,0' Padding='4,4,4,4' Background='#E4E9F0' BorderBrush='#B8C3D2' BorderThickness='1,1,1,0' CornerRadius='3,3,0,0'>
          <ContentPresenter ContentSource='Header' HorizontalAlignment='Center' VerticalAlignment='Center' RecognizesAccessKey='True'/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Bd' Property='Background' Value='#F4F8FC'/></Trigger>
          <Trigger Property='IsSelected' Value='True'>
            <Setter TargetName='Bd' Property='Background' Value='White'/>
            <Setter TargetName='Bd' Property='BorderBrush' Value='#2F6FB5'/>
            <Setter TargetName='Bd' Property='BorderThickness' Value='1,2,1,0'/>
            <Setter Property='Foreground' Value='#0B4F9C'/>
            <Setter Property='FontWeight' Value='SemiBold'/>
          </Trigger>
          <Trigger Property='IsEnabled' Value='False'><Setter Property='Foreground' Value='Gray'/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";

    private Style _leftTabStyle;
    private readonly List<ToggleButtonGroup> _leftGroupBtns = new();
    private sealed record ToggleButtonGroup(System.Windows.Controls.Primitives.ToggleButton Button, string[] Tabs);
    private int _leftGroup = -1;

    private void SetupLeftPanelScroll()
    {
        if (tabLeft == null) return;
        try
        {
            _leftTabStyle = (Style)XamlReader.Parse(TabItemStyleXaml);
            tabLeft.Template = (ControlTemplate)XamlReader.Parse(TabTemplateXaml(true));
        }
        catch (Exception) { /* giữ mẫu mặc định nếu lỗi */ }
        tabLeft.ApplyTemplate();
        foreach (var ti in tabLeft.Items.OfType<TabItem>()) if (_leftTabStyle != null && ti.Style == null) ti.Style = _leftTabStyle;
        if (tabLeft.Template?.FindName("PART_Groups", tabLeft) is UniformGrid gbar)
        {
            for (int g = 0; g < LeftGroups.Length; g++)
            {
                int gi = g;
                var b = new System.Windows.Controls.Primitives.ToggleButton
                {
                    Content = new TextBlock { Text = LeftGroups[g].Name, TextTrimming = TextTrimming.CharacterEllipsis },
                    ToolTip = LeftGroups[g].Tip, Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(1, 0, 1, 0), FontWeight = FontWeights.SemiBold, Focusable = false
                };
                b.Click += (s, e) => ShowLeftGroup(gi, true);
                gbar.Children.Add(b);
                _leftGroupBtns.Add(new ToggleButtonGroup(b, LeftGroups[g].Tabs));
            }
        }
        var dpd = DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(TabItem));
        foreach (var ti in tabLeft.Items.OfType<TabItem>())
        {
            WrapLeftTab(ti);
            dpd?.AddValueChanged(ti, (s, e) => WrapLeftTab((TabItem)s));   // nội dung dựng lại bằng mã (Địa chất…) → bọc lại
        }
        tabLeft.SelectionChanged += (s, e) =>
        {
            if (e.OriginalSource != tabLeft || tabLeft.SelectedItem is not TabItem t) return;
            WrapLeftTab(t);
            int g = LeftGroupOf(t);
            if (g >= 0 && g != _leftGroup) ShowLeftGroup(g, false);
        };
        ShowLeftGroup(Math.Max(0, LeftGroupOf(tabLeft.SelectedItem as TabItem)), false);
        tabLeft.Loaded += (s, e) => CompactNestedTabs(tabLeft);
        // Shift + lăn chuột: cuộn ngang khung gần nhất còn cuộn được
        tabLeft.PreviewMouseWheel += (s, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0) return;
            for (var d = e.OriginalSource as DependencyObject; d != null && d != tabLeft; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
                if (d is ScrollViewer sv && sv.ScrollableWidth > 0)
                {
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta / 2.0);
                    e.Handled = true;
                    return;
                }
        };
    }

    private static string HeaderText(TabItem t) => t?.Header as string ?? (t?.Header as TextBlock)?.Text ?? "";

    private int LeftGroupOf(TabItem t)
    {
        string h = HeaderText(t);
        for (int g = 0; g < LeftGroups.Length; g++) if (LeftGroups[g].Tabs.Contains(h)) return g;
        return t == null ? -1 : LeftGroups.Length - 1;   // thẻ lạ → nhóm cuối
    }

    /// <summary>Hiện các thẻ của nhóm g (ẩn thẻ nhóm khác – hàng thẻ chia đều, không tràn khung).</summary>
    private void ShowLeftGroup(int g, bool selectFirst)
    {
        if (g < 0 || g >= LeftGroups.Length) return;
        _leftGroup = g;
        TabItem first = null;
        foreach (var ti in tabLeft.Items.OfType<TabItem>())
        {
            bool vis = LeftGroupOf(ti) == g;
            ti.Visibility = vis ? Visibility.Visible : Visibility.Collapsed;
            if (vis && first == null) first = ti;
        }
        for (int i = 0; i < _leftGroupBtns.Count; i++) _leftGroupBtns[i].Button.IsChecked = i == g;
        if (selectFirst || (tabLeft.SelectedItem is TabItem cur && LeftGroupOf(cur) != g)) { if (first != null) first.IsSelected = true; }
    }

    /// <summary>Thẻ con trong khung trái (Thiết kế: Dầm, Cột, Sàn…): cùng kiểu thẻ gọn, chia đều một hàng.</summary>
    private void CompactNestedTabs(DependencyObject root)
    {
        if (_leftTabStyle == null || root == null) return;
        foreach (var o in LogicalTreeHelper.GetChildren(root))
        {
            if (o is not DependencyObject d) continue;
            if (d is TabControl tc && tc != tabLeft && !Equals(tc.Tag, "compact"))
            {
                tc.Tag = "compact";
                try { tc.Template = (ControlTemplate)XamlReader.Parse(TabTemplateXaml(false)); } catch (Exception) { }
                foreach (var ti in tc.Items.OfType<TabItem>()) if (ti.Style == null) ti.Style = _leftTabStyle;
            }
            CompactNestedTabs(d);
        }
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
