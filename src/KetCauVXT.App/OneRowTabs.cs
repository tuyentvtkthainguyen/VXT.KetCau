using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;

namespace KetCauVXT.App;

/// <summary>Thanh tab MỘT HÀNG (kiểu ribbon Revit / Office): tiêu đề tab không xuống dòng khi khung hẹp – cuộn ngang bằng con lăn,
/// phần không đủ chỗ mở bằng nút "»" (danh sách tab). Áp cho thanh lệnh cấu kiện và ribbon kiểu Revit.</summary>
internal static class OneRowTabs
{
    private const string TemplateXaml =
        "<ControlTemplate TargetType=\"{x:Type TabControl}\" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
        "<DockPanel LastChildFill=\"True\">" +
        "<DockPanel DockPanel.Dock=\"Top\" LastChildFill=\"True\">" +
        "<Button x:Name=\"PART_More\" DockPanel.Dock=\"Right\" Content=\"»\" Padding=\"7,0,7,0\" Margin=\"2,2,2,0\" Visibility=\"Collapsed\" Focusable=\"False\" ToolTip=\"Các tab không đủ chỗ hiện\"/>" +
        "<ScrollViewer x:Name=\"PART_HeaderScroll\" HorizontalScrollBarVisibility=\"Hidden\" VerticalScrollBarVisibility=\"Disabled\" Focusable=\"False\">" +
        "<StackPanel Orientation=\"Horizontal\" IsItemsHost=\"True\" Margin=\"2,2,2,0\"/>" +
        "</ScrollViewer>" +
        "</DockPanel>" +
        "<Border Background=\"{TemplateBinding Background}\" BorderBrush=\"{TemplateBinding BorderBrush}\" BorderThickness=\"{TemplateBinding BorderThickness}\" Padding=\"{TemplateBinding Padding}\">" +
        "<ContentPresenter ContentSource=\"SelectedContent\"/>" +
        "</Border>" +
        "</DockPanel>" +
        "</ControlTemplate>";

    private static ControlTemplate _template;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TabControl, object> Wired = new();

    /// <summary>Đổi khuôn TabControl sang một hàng tiêu đề. Lỗi → giữ nguyên khuôn mặc định.</summary>
    public static void Apply(TabControl tc)
    {
        if (tc == null) return;
        try
        {
            _template ??= (ControlTemplate)XamlReader.Parse(TemplateXaml);
            tc.Template = _template;
            if (tc.IsLoaded) Wire(tc); else tc.Loaded += (s, e) => Wire(tc);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Tab một hàng: " + ex.Message); }
    }

    private static void Wire(TabControl tc)
    {
        if (Wired.TryGetValue(tc, out _)) return;
        tc.ApplyTemplate();
        if (tc.Template.FindName("PART_HeaderScroll", tc) is not ScrollViewer sv || tc.Template.FindName("PART_More", tc) is not Button more) return;
        Wired.Add(tc, null);
        void Update() => more.Visibility = sv.ExtentWidth > sv.ViewportWidth + 1 ? Visibility.Visible : Visibility.Collapsed;
        sv.ScrollChanged += (s, e) => Update();
        sv.SizeChanged += (s, e) => Update();
        sv.PreviewMouseWheel += (s, e) =>
        {
            if (sv.ExtentWidth <= sv.ViewportWidth + 1) return;
            sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta / 3.0);
            e.Handled = true;
        };
        tc.SelectionChanged += (s, e) =>
        {
            if (!ReferenceEquals(e.OriginalSource, tc)) return;
            (tc.SelectedItem as TabItem)?.BringIntoView();
        };
        more.Click += (s, e) =>
        {
            var cm = new ContextMenu { PlacementTarget = more, Placement = PlacementMode.Bottom };
            foreach (var ti in tc.Items.OfType<TabItem>().Where(q => q.Visibility == Visibility.Visible))
            {
                var mi = new MenuItem { Header = ti.Header as string ?? ti.Header?.ToString(), IsCheckable = true, IsChecked = ReferenceEquals(tc.SelectedItem, ti) };
                var target = ti;
                mi.Click += (s2, e2) => { tc.SelectedItem = target; target.BringIntoView(); };
                cm.Items.Add(mi);
            }
            cm.IsOpen = true;
        };
        Update();
    }
}
