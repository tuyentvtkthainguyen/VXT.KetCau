using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Design;
using KetCauVXT.Docs;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Bảng "Bắt đầu nhanh" (không chặn): danh sách bước quy trình (Docs/QuickStart), trạng thái tự nhận biết từ dự án,
/// nút "Làm bước này" (cửa sổ chính chuyển tới tab / công cụ) và "Xem hướng dẫn". Tuỳ chọn "Không hiện lại" lưu ở
/// %AppData%\KetCauVXT\BatDauNhanh.json.</summary>
internal sealed class QuickStartWindow : Window
{
    private readonly Func<(Project P, bool HasResults, DesignSummary Design)> _state;
    private readonly Action<string> _doStep;
    private readonly StackPanel _list = new() { Margin = new Thickness(10, 4, 10, 4) };
    private readonly TextBlock _summary = new() { Margin = new Thickness(10, 8, 10, 2), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold };
    private readonly CheckBox _chkHide = new() { Content = "Không hiện lại khi mở chương trình / tạo dự án mới", VerticalAlignment = VerticalAlignment.Center };

    private static readonly Brush DoneBrush = Frozen(Color.FromRgb(30, 130, 60));
    private static readonly Brush TodoBrush = Frozen(Color.FromRgb(150, 150, 150));
    private static readonly Brush AskBrush = Frozen(Color.FromRgb(200, 120, 20));
    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    /// <param name="state">Trạng thái hiện hành của cửa sổ chính (dự án, có kết quả phân tích, kết quả thiết kế).</param>
    /// <param name="doStep">Thực hiện bước theo Id (QuickStartStep.Id).</param>
    public QuickStartWindow(Func<(Project, bool, DesignSummary)> state, Action<string> doStep)
    {
        _state = () => state();
        _doStep = doStep;
        DialogUi.Setup(this, "Bắt đầu nhanh – Kết cấu VXT", 760, 640);
        ShowInTaskbar = true;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Help.SetId(this, "quy-trinh.bat-dau-nhanh");

        var intro = DialogUi.Hint("Các bước làm việc từ khởi tạo đến hồ sơ. ✓ đã làm, ○ chưa làm, ? bạn tự kiểm tra. "
                                  + "\"Làm bước này\" chuyển tới tab / công cụ tương ứng; \"Xem hướng dẫn\" mở mục hướng dẫn (F1 cũng mở hướng dẫn theo ngữ cảnh).");
        intro.Margin = new Thickness(10, 0, 10, 4);

        _chkHide.IsChecked = !ShouldShowOnStart();
        _chkHide.Checked += (s, e) => SaveHide(true);
        _chkHide.Unchecked += (s, e) => SaveHide(false);
        var bottom = new DockPanel { Margin = new Thickness(10, 4, 10, 8), LastChildFill = false };
        DockPanel.SetDock(_chkHide, Dock.Left); bottom.Children.Add(_chkHide);
        var btns = DialogUi.Row(DialogUi.Btn("Làm mới", (s, e) => Refresh()), DialogUi.Btn("Hướng dẫn đầy đủ…", (s, e) => Help.Show("quy-trinh")), DialogUi.Btn("Đóng", (s, e) => Close()));
        DockPanel.SetDock(btns, Dock.Right); bottom.Children.Add(btns);

        var root = new DockPanel();
        DockPanel.SetDock(_summary, Dock.Top); root.Children.Add(_summary);
        DockPanel.SetDock(intro, Dock.Top); root.Children.Add(intro);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        root.Children.Add(new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
        Activated += (s, e) => Refresh();
        Refresh();
    }

    /// <summary>Đọc lại trạng thái dự án và vẽ lại danh sách bước.</summary>
    public void Refresh()
    {
        var (p, hasRes, ds) = _state();
        _list.Children.Clear();
        int done = 0, i = 0;
        foreach (var step in QuickStart.Steps)
        {
            i++;
            bool? st = QuickStart.Evaluate(step, p, hasRes, ds);
            if (st == true) done++;
            var mark = new TextBlock
            {
                Text = st == true ? "✓" : st == false ? "○" : "?", FontSize = 18, FontWeight = FontWeights.Bold, Width = 26, VerticalAlignment = VerticalAlignment.Top,
                Foreground = st == true ? DoneBrush : st == false ? TodoBrush : AskBrush,
                ToolTip = st == true ? "Đã làm (tự nhận biết từ dự án)" : st == false ? "Chưa làm" : "Không tự nhận biết được – bạn tự kiểm tra"
            };
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = $"{i}. {step.Title}", FontWeight = FontWeights.SemiBold, FontSize = 13.5 });
            text.Children.Add(new TextBlock { Text = step.Description, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 1, 0, 0) });
            var id = step.Id; var help = step.HelpId;
            var go = DialogUi.Btn("Làm bước này", (s, e) => { try { _doStep?.Invoke(id); } catch (Exception ex) { MessageBox.Show(this, ex.Message, Title); } Refresh(); }, st != true);
            var see = DialogUi.Btn("Xem hướng dẫn", (s, e) => Help.Show(help));
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            actions.Children.Add(go); actions.Children.Add(see);
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            DockPanel.SetDock(mark, Dock.Left); row.Children.Add(mark);
            DockPanel.SetDock(actions, Dock.Right); row.Children.Add(actions);
            row.Children.Add(text);
            _list.Children.Add(new Border { Child = row, BorderBrush = new SolidColorBrush(Color.FromRgb(225, 228, 234)), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 2, 0, 4) });
        }
        _summary.Text = $"Dự án \"{p?.Name}\": đã làm {done}/{QuickStart.Steps.Count} bước.";
    }

    // ------------------------------------------------------------------ tuỳ chọn "Không hiện lại"
    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KetCauVXT", "BatDauNhanh.json");

    /// <summary>true nếu người dùng chưa chọn "Không hiện lại".</summary>
    public static bool ShouldShowOnStart()
    {
        try { return !File.Exists(SettingsPath) || !File.ReadAllText(SettingsPath).Contains("\"HienKhiMo\": false", StringComparison.Ordinal); }
        catch { return true; }
    }

    private static void SaveHide(bool hide)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, "{\n  \"HienKhiMo\": " + (hide ? "false" : "true") + "\n}\n");
        }
        catch { /* không ghi được thiết lập: bỏ qua */ }
    }
}
