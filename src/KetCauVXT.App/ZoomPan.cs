using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KetCauVXT.App;

/// <summary>Thu phóng / di chuyển cho các hình vẽ tĩnh (mặt cắt móng, biểu đồ cấu kiện, tờ dàn trang…):
/// con lăn = thu phóng vào điểm dưới con trỏ, kéo chuột giữa / phải (hoặc trái khi leftPan) = di chuyển,
/// bấm đúp chuột giữa / phím F, Home = về vừa khung. Gắn biến đổi lên phần tử con nên nội dung vẽ lại vẫn giữ mức thu phóng.</summary>
public static class ZoomPan
{
    private sealed class State
    {
        public ScaleTransform S = new();
        public TranslateTransform T = new();
        public bool Panning;
        public Point Start;
        public double Tx0, Ty0;
    }

    private static readonly DependencyProperty StateProp = DependencyProperty.RegisterAttached("ZoomPanState", typeof(object), typeof(ZoomPan));

    /// <summary>host: khung nhận chuột (Border, ClipToBounds); content: phần tử được thu phóng (Canvas con).</summary>
    public static void Attach(FrameworkElement host, FrameworkElement content, bool leftPan = false)
    {
        if (host == null || content == null || host.GetValue(StateProp) != null) return;
        var st = new State();
        host.SetValue(StateProp, st);
        var g = new TransformGroup();
        g.Children.Add(st.S); g.Children.Add(st.T);
        content.RenderTransform = g;
        content.RenderTransformOrigin = new Point(0, 0);
        host.Focusable = true; host.FocusVisualStyle = null;
        if (host is Border b && b.ClipToBounds == false) b.ClipToBounds = true;

        host.PreviewMouseWheel += (s, e) =>
        {
            var m = e.GetPosition(host);
            double k = e.Delta > 0 ? 1.2 : 1 / 1.2;
            double ns = Math.Clamp(st.S.ScaleX * k, 0.2, 40);
            k = ns / st.S.ScaleX;
            // giữ điểm dưới con trỏ đứng yên: t' = m − (m − t)·k
            st.T.X = m.X - (m.X - st.T.X) * k; st.T.Y = m.Y - (m.Y - st.T.Y) * k;
            st.S.ScaleX = st.S.ScaleY = ns;
            e.Handled = true;
        };
        host.PreviewMouseDown += (s, e) =>
        {
            host.Focus();
            if (e.ChangedButton == MouseButton.Middle && e.ClickCount == 2) { Reset(host); e.Handled = true; return; }
            bool pan = e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right || (leftPan && e.ChangedButton == MouseButton.Left);
            if (!pan) return;
            st.Panning = true; st.Start = e.GetPosition(host); st.Tx0 = st.T.X; st.Ty0 = st.T.Y;
            host.CaptureMouse();
            host.Cursor = Cursors.Hand;
            if (e.ChangedButton != MouseButton.Left) e.Handled = true;
        };
        host.PreviewMouseMove += (s, e) =>
        {
            if (!st.Panning) return;
            if (e.LeftButton != MouseButtonState.Pressed && e.MiddleButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed) { End(host, st); return; }
            var p = e.GetPosition(host);
            st.T.X = st.Tx0 + p.X - st.Start.X; st.T.Y = st.Ty0 + p.Y - st.Start.Y;
        };
        host.PreviewMouseUp += (s, e) =>
        {
            if (!st.Panning) return;
            var p = e.GetPosition(host);
            bool moved = Math.Abs(p.X - st.Start.X) + Math.Abs(p.Y - st.Start.Y) >= 4;
            End(host, st);
            if (moved || e.ChangedButton == MouseButton.Middle) e.Handled = true;
        };
        host.PreviewKeyDown += (s, e) =>
        {
            if (Keyboard.FocusedElement is TextBox) return;
            if ((e.Key == Key.F || e.Key == Key.Home) && Keyboard.Modifiers == ModifierKeys.None) { Reset(host); e.Handled = true; }
        };
    }

    /// <summary>Gắn thu phóng cho một Canvas vẽ tĩnh: khi nạp, đặt Canvas vào một khung Border (cắt mép) ở đúng chỗ cũ làm vùng nhận chuột.</summary>
    public static void Wrap(FrameworkElement content, bool leftPan = false)
    {
        if (content == null) return;
        void Do()
        {
            if (content.Parent is Border pb && pb.Tag as string == "ZoomPanHost") return;
            var parent = content.Parent;
            var host = new Border { ClipToBounds = true, Tag = "ZoomPanHost", Background = Brushes.Transparent,
                HorizontalAlignment = content.HorizontalAlignment, VerticalAlignment = content.VerticalAlignment, Margin = content.Margin };
            switch (parent)
            {
                case Panel pnl:
                    int i = pnl.Children.IndexOf(content);
                    if (i < 0) return;
                    pnl.Children.RemoveAt(i);
                    foreach (var dp in new[] { Grid.RowProperty, Grid.ColumnProperty, Grid.RowSpanProperty, Grid.ColumnSpanProperty, DockPanel.DockProperty })
                    { var v = content.ReadLocalValue(dp); if (v != DependencyProperty.UnsetValue) host.SetValue(dp, v); }
                    content.Margin = new Thickness(0);
                    host.Child = content;
                    pnl.Children.Insert(i, host);
                    break;
                case Decorator dec:
                    dec.Child = null; content.Margin = new Thickness(0); host.Child = content; dec.Child = host;
                    break;
                case ContentControl cc:
                    cc.Content = null; content.Margin = new Thickness(0); host.Child = content; cc.Content = host;
                    break;
                default:
                    return;
            }
            Attach(host, content, leftPan);
        }
        if (content.IsLoaded) Do();
        else
        {
            RoutedEventHandler h = null;
            h = (s, e) => { content.Loaded -= h; Do(); };
            content.Loaded += h;
        }
    }

    private static void End(FrameworkElement host, State st)
    {
        st.Panning = false;
        host.ReleaseMouseCapture();
        host.Cursor = null;
    }

    /// <summary>Về mức vừa khung (bỏ thu phóng / di chuyển).</summary>
    public static void Reset(FrameworkElement host)
    {
        if (host?.GetValue(StateProp) is not State st) return;
        st.S.ScaleX = st.S.ScaleY = 1; st.T.X = st.T.Y = 0;
    }
}
