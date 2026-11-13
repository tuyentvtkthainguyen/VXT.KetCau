using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KetCauVXT.App;

/// <summary>[Thẻ thông tin] Bấm vào đối tượng trên mặt bằng (2D) hoặc 3D → thẻ nổi ở góc trên phải khung nhìn hiện thông tin đối tượng
/// (cùng nội dung ô thông tin phía dưới: cột, dầm, sàn, móng, tường, cửa, mái tôn – xà gồ – vì kèo…). Tắt/bật bằng ô "Thẻ thông tin".</summary>
public partial class MainWindow
{
    private bool _infoCardOn = true;
    private string _infoCardDismissed;
    private readonly List<(Border Card, TextBlock Text)> _infoCards = new();
    private bool _infoHooked;

    private void EnsureInfoCards()
    {
        if (!_infoHooked && txtInfo != null)
        {
            _infoHooked = true;
            DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock))?.AddValueChanged(txtInfo, (s, e) => UpdateInfoCards());
        }
        if (planHost?.Child is Grid pg && !_infoCards.Any(c => ReferenceEquals(c.Card.Parent, pg))) pg.Children.Add(MakeInfoCard());
        if (_view3DInit && view3DHost?.Child is Grid g3 && !_infoCards.Any(c => ReferenceEquals(c.Card.Parent, g3))) g3.Children.Add(MakeInfoCard());
    }

    private Border MakeInfoCard()
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(30, 35, 45)) };
        var close = new Button { Content = "✕", Padding = new Thickness(5, 0, 5, 0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, ToolTip = "Đóng thẻ (bấm đối tượng khác để hiện lại)", Cursor = Cursors.Hand };
        var title = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(close, Dock.Right);
        title.Children.Add(close);
        title.Children.Add(new TextBlock { Text = "ⓘ  Thông tin đối tượng", FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0, 90, 160)) });
        var body = new DockPanel();
        DockPanel.SetDock(title, Dock.Top);
        body.Children.Add(title);
        body.Children.Add(new ScrollViewer { Content = text, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 210 });
        var card = new Border
        {
            Child = body, Width = 300, Padding = new Thickness(8, 6, 8, 8), Margin = new Thickness(0, 34, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(238, 255, 255, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(120, 150, 190)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Visibility = Visibility.Collapsed,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.35 }
        };
        // chuột trên thẻ không xuống khung nhìn (không chọn / xoay / kéo)
        card.MouseLeftButtonDown += (s, e) => e.Handled = true;
        card.MouseLeftButtonUp += (s, e) => e.Handled = true;
        card.MouseRightButtonDown += (s, e) => e.Handled = true;
        card.MouseDown += (s, e) => e.Handled = true;
        card.MouseWheel += (s, e) => e.Handled = true;
        close.Click += (s, e) => { _infoCardDismissed = txtInfo.Text; UpdateInfoCards(); };
        _infoCards.Add((card, text));
        UpdateInfoCards();
        return card;
    }

    private bool InfoCardHasObject =>
        HasSelection || _selRoof != null || _rvSelOpen != null || _v3SelId != null;

    private void UpdateInfoCards()
    {
        string t = txtInfo?.Text ?? "";
        bool show = _infoCardOn && InfoCardHasObject && t.Length > 0 && t != _infoCardDismissed;
        if (t != _infoCardDismissed) _infoCardDismissed = null;
        foreach (var (card, text) in _infoCards)
        {
            text.Text = t;
            card.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private CheckBox InfoCardCheck(Brush fg)
    {
        var cb = new CheckBox { Content = "Thẻ thông tin", IsChecked = _infoCardOn, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
                                ToolTip = "Bấm vào đối tượng trên mặt bằng / 3D hiện thẻ thông tin nổi ở góc khung nhìn" };
        if (fg != null) cb.Foreground = fg;
        cb.Checked += (s, e) => { _infoCardOn = true; SyncInfoChecks(); UpdateInfoCards(); };
        cb.Unchecked += (s, e) => { _infoCardOn = false; SyncInfoChecks(); UpdateInfoCards(); };
        _infoChecks.Add(cb);
        return cb;
    }

    private readonly List<CheckBox> _infoChecks = new();
    private void SyncInfoChecks() { foreach (var c in _infoChecks) if (c.IsChecked != _infoCardOn) c.IsChecked = _infoCardOn; }
}
