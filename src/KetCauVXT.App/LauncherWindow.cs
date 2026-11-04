using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KetCauVXT.App;

/// <summary>Màn hình chọn chế độ khi khởi động:
/// (1) THIẾT KẾ – CHẠY NỘI LỰC: giao diện mô hình tính như ETABS (MainWindow);
/// (2) TRIỂN KHAI HỒ SƠ: chỉ nhập kích thước dầm / sàn / cột, chọn thép, thiết kế móng đơn / băng / cọc / bè, dự ứng lực, bể nước, xuất khối lượng (DetailingWindow).
/// Lựa chọn "Luôn mở chế độ này" lưu tại %AppData%\KetCauVXT\CheDoKhoiDong.json.</summary>
public sealed class LauncherWindow : Window
{
    public enum Mode { None, Model, Detailing }

    public sealed class Pref
    {
        public string Mode { get; set; } = "";
        public bool Remember { get; set; }
    }

    public static string PrefPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KetCauVXT", "CheDoKhoiDong.json");

    public static Pref LoadPref()
    {
        try { return File.Exists(PrefPath) ? JsonSerializer.Deserialize<Pref>(File.ReadAllText(PrefPath)) ?? new Pref() : new Pref(); }
        catch { return new Pref(); }
    }

    public static void SavePref(Pref p)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(PrefPath)!); File.WriteAllText(PrefPath, JsonSerializer.Serialize(p)); } catch { }
    }

    public Mode Chosen { get; private set; } = Mode.None;
    private readonly CheckBox _remember = new() { Content = "Luôn mở chế độ đã chọn khi khởi động (đổi lại: Tệp → Chọn chế độ làm việc…)", Margin = new Thickness(0, 14, 0, 0) };

    public LauncherWindow()
    {
        Title = "Kết cấu VXT – chọn chế độ làm việc";
        Width = 940; Height = 600; MinWidth = 720; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xF8));
        try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/KetCauVXT.ico")); } catch { }

        var root = new DockPanel { Margin = new Thickness(28, 22, 28, 18) };
        var head = new StackPanel();
        head.Children.Add(new TextBlock { Text = "KẾT CẤU VXT", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x3A, 0x5F)) });
        head.Children.Add(new TextBlock { Text = "Phần mềm thiết kế kết cấu nhà dưới 7 tầng và nhà thép tiền chế – chọn cách làm việc:", FontSize = 14, Margin = new Thickness(0, 4, 0, 16), Foreground = Brushes.DimGray });
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);

        var bottom = new StackPanel();
        bottom.Children.Add(_remember);
        bottom.Children.Add(new TextBlock
        {
            Text = "Có thể chuyển chế độ bất cứ lúc nào: Tệp → Chuyển sang chế độ Triển khai hồ sơ / Thiết kế – chạy nội lực. F1: hướng dẫn sử dụng.",
            Foreground = Brushes.Gray, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap
        });
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        var cards = new UniformGrid2();
        cards.Children.Add(Card("1", "THIẾT KẾ – CHẠY NỘI LỰC", "Giao diện mô hình tính như ETABS",
            new[]
            {
                "Lưới trục, tầng, khai báo tiết diện, vẽ / gán cột, dầm, sàn, tường",
                "Tải trọng (gió theo tỉnh, động đất), tổ hợp, phân tích khung 3D, dao động riêng",
                "Thiết kế BTCT / thép, móng theo nội lực mô hình, đối chiếu ETABS / SAP",
                "Triển khai bản vẽ tự động, thuyết minh, khối lượng"
            }, Color.FromRgb(0x1F, 0x5F, 0xA8), () => Choose(Mode.Model)));
        cards.Children.Add(Card("2", "TRIỂN KHAI HỒ SƠ", "Nhập kích thước, chọn thép – không cần mô hình",
            new[]
            {
                "Dầm, cột, sàn: nhập kích thước, chọn số thanh, Ø, khoảng cách thép (kiểm tra nếu có nội lực)",
                "Móng đơn, móng băng, móng cọc (đài cọc), móng bè – nhập tải chân cột",
                "Bê tông dự ứng lực (dầm / sàn căng sau), bể nước BTCT",
                "Xuất bản vẽ DXF, bảng thống kê thép, khối lượng Excel, thuyết minh Word"
            }, Color.FromRgb(0x2E, 0x7D, 0x32), () => Choose(Mode.Detailing)));
        root.Children.Add(cards);
        Content = root;
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.D1 || e.Key == Key.NumPad1) Choose(Mode.Model);
            else if (e.Key == Key.D2 || e.Key == Key.NumPad2) Choose(Mode.Detailing);
            else if (e.Key == Key.Escape) Close();
        };
    }

    private void Choose(Mode m)
    {
        Chosen = m;
        SavePref(new Pref { Mode = m.ToString(), Remember = _remember.IsChecked == true });
        DialogResult = true;
    }

    private static Border Card(string key, string title, string sub, string[] bullets, Color color, Action click)
    {
        var sp = new StackPanel { Margin = new Thickness(18) };
        sp.Children.Add(new TextBlock { Text = title, FontSize = 19, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(color) });
        sp.Children.Add(new TextBlock { Text = sub, FontSize = 13.5, Margin = new Thickness(0, 2, 0, 12), Foreground = Brushes.DimGray });
        foreach (var b in bullets)
            sp.Children.Add(new TextBlock { Text = "•  " + b, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 3), FontSize = 13 });
        var btn = new Button
        {
            Content = "Mở chế độ này  (phím " + key + ")", Margin = new Thickness(0, 16, 0, 0), Padding = new Thickness(14, 7, 14, 7),
            HorizontalAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.SemiBold, Background = new SolidColorBrush(color), Foreground = Brushes.White
        };
        btn.Click += (s, e) => click();
        sp.Children.Add(btn);
        var card = new Border
        {
            Child = sp, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xD5, 0xDB, 0xE1)), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Margin = new Thickness(8), Cursor = Cursors.Hand
        };
        card.MouseLeftButtonUp += (s, e) => { if (e.OriginalSource is not Button) click(); };
        card.MouseEnter += (s, e) => card.BorderBrush = new SolidColorBrush(color);
        card.MouseLeave += (s, e) => card.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD5, 0xDB, 0xE1));
        return card;
    }

    /// <summary>Lưới 2 cột chia đều (không cần System.Windows.Controls.Primitives).</summary>
    private sealed class UniformGrid2 : System.Windows.Controls.Primitives.UniformGrid
    {
        public UniformGrid2() { Columns = 2; Rows = 1; }
    }

    // ------------------------------------------------------------------
    // Mở cửa sổ theo chế độ
    // ------------------------------------------------------------------
    /// <summary>Mở cửa sổ làm việc (đặt làm cửa sổ chính của ứng dụng). file: .kcvxt / .kcvxth cần mở (có thể null).</summary>
    public static Window Open(Mode m, string file = null)
    {
        Window w = m == Mode.Detailing ? new DetailingWindow(file) : new MainWindow();
        Application.Current.MainWindow = w;
        w.Show();
        return w;
    }
}
