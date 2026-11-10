using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Docs;

namespace KetCauVXT.App;

// =====================================================================
// TRỢ GIÚP: "Hướng dẫn sử dụng…" (UserGuideWindow.cs), "Bắt đầu nhanh…" (QuickStartWindow.cs), F1 theo ngữ cảnh (Help.cs).
//   • F1 (không phím bổ trợ) ở mọi cửa sổ → hướng dẫn tại mục của ngữ cảnh: phần tử / hộp thoại có Help.Id, tên lớp cửa sổ
//     (HelpContext.Windows), hoặc ở cửa sổ chính: công cụ sửa mặt bằng đang bật, khung nhìn / tab trái đang dùng.
//     Shift+F1: phụ lục tính toán (MainWindow.xaml.cs, MainWindow.Help.cs).
//   • Bắt đầu nhanh: hiện khi mở chương trình / tạo dự án mới với dự án rỗng (trừ khi chọn "Không hiện lại", trừ --selftest).
//   • Thanh trạng thái: khi một công cụ sửa mặt bằng đang bật, gợi ý cách dùng kèm "(F1: hướng dẫn)".
// Đăng ký bằng hàm dựng tĩnh (class handler) – không cần sửa hàm dựng / Window_Loaded của MainWindow.
// =====================================================================
public partial class MainWindow
{
    private UserGuideWindow _userGuideWin;
    private QuickStartWindow _quickStartWin;
    private bool _userGuideInit;
    private const string F1Hint = "   (F1: hướng dẫn)";

    static MainWindow()
    {
        EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnAnyWindowPreviewKeyDown));
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, e) => (s as MainWindow)?.InitUserGuideUi()));
        Help.Opener = id => (Application.Current?.MainWindow as MainWindow)?.OpenUserGuide(id);
    }

    /// <summary>Chạy với tham số --selftest / --acad …: không tự hiện bảng Bắt đầu nhanh.</summary>
    private static bool IsSelfTestRun => Environment.GetCommandLineArgs().Skip(1).Any(a => a.StartsWith("--", StringComparison.Ordinal));

    /// <summary>F1 ở bất kỳ cửa sổ nào → hướng dẫn theo ngữ cảnh.</summary>
    private static void OnAnyWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.F1 || Keyboard.Modifiers != ModifierKeys.None || sender is not Window w) return;
        var main = Application.Current?.MainWindow as MainWindow;
        if (main == null) return;
        e.Handled = true;
        if (w is UserGuideWindow) return;
        string id = w is MainWindow mw ? mw.CurrentHelpId() : Help.Resolve(w) ?? main.CurrentHelpId();
        main.OpenUserGuide(id);
    }

    private void MnuUserGuide_Click(object sender, RoutedEventArgs e) => OpenUserGuide(null);
    private void MnuQuickStart_Click(object sender, RoutedEventArgs e) => ShowQuickStart();

    /// <summary>Mở (hoặc đưa lên trước) cửa sổ hướng dẫn tại mục helpId (null = giữ mục đang xem / mục mặc định).</summary>
    internal UserGuideWindow OpenUserGuide(string helpId, bool activate = true)
    {
        try
        {
            // cửa sổ cũ bị khoá do một hộp thoại modal mở sau nó → mở cửa sổ mới (không chủ) để dùng được ngay
            if (_userGuideWin != null && (!_userGuideWin.IsLoaded || !_userGuideWin.IsEnabled)) { var old = _userGuideWin; _userGuideWin = null; old.Close(); }
            if (_userGuideWin == null)
            {
                var w = new UserGuideWindow();
                if (IsEnabled && IsLoaded) w.Owner = this;
                w.Closed += (s, e) => { if (ReferenceEquals(_userGuideWin, s)) _userGuideWin = null; };
                _userGuideWin = w;
                w.Show();
                w.ShowHelp(helpId ?? HelpContext.DefaultId);
            }
            else
            {
                if (helpId != null) _userGuideWin.ShowHelp(helpId);
                if (_userGuideWin.WindowState == WindowState.Minimized) _userGuideWin.WindowState = WindowState.Normal;
            }
            if (activate) _userGuideWin.Activate();
            return _userGuideWin;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Không mở được hướng dẫn sử dụng:\n" + ex.Message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }

    // ------------------------------------------------------------------ ngữ cảnh của cửa sổ chính
    private static string Header(TabControl tc) => (tc?.SelectedItem as TabItem)?.Header as string;

    /// <summary>HelpId theo ngữ cảnh hiện tại của cửa sổ chính.</summary>
    internal string CurrentHelpId()
    {
        try
        {
            var focus = Keyboard.FocusedElement as DependencyObject;
            string own = focus != null ? Help.GetId(focus) : null;
            if (!string.IsNullOrWhiteSpace(own)) return own;
            bool inLeft = focus is Visual v1 && tabLeft.IsAncestorOf(v1);
            bool inView = focus is Visual v2 && tabView.IsAncestorOf(v2);
            string view = Header(tabView);
            if (!inLeft)
            {
                if (view == "Mặt bằng" && _mode != PlanEditMode.Select)
                {
                    var m = HelpContext.ForPlanMode(_mode.ToString());
                    if (m != null) return m;
                }
                if (inView) return HelpContext.ForViewTab(view) ?? HelpContext.DefaultId;
            }
            var left = tabLeft.SelectedItem as TabItem;
            string sub = left?.Content is TabControl inner ? Header(inner) : null;
            return HelpContext.ForLeftTab(left?.Header as string, sub) ?? HelpContext.ForViewTab(view) ?? HelpContext.DefaultId;
        }
        catch { return HelpContext.DefaultId; }
    }

    // ------------------------------------------------------------------ khởi tạo (sau Window_Loaded)
    private void InitUserGuideUi()
    {
        if (_userGuideInit) return;
        _userGuideInit = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                // gợi ý "(F1: hướng dẫn)" khi thanh trạng thái hiện cách dùng của công cụ sửa mặt bằng
                DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock))?.AddValueChanged(txtStatus, (s, e) => AppendF1Hint());
            }
            catch { /* không gắn được: bỏ qua */ }
            MaybeShowQuickStart();
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void AppendF1Hint()
    {
        if (_mode == PlanEditMode.Select) return;
        string t = txtStatus.Text ?? "";
        if (t.Length == 0 || t.EndsWith(F1Hint, StringComparison.Ordinal)) return;
        string hint = ModeHint(_mode);
        if (!string.IsNullOrEmpty(hint) && t.StartsWith(hint, StringComparison.Ordinal)) txtStatus.Text = t + F1Hint;
    }

    // ------------------------------------------------------------------ Bắt đầu nhanh
    /// <summary>Hiện bảng Bắt đầu nhanh khi dự án rỗng (mở chương trình, Tệp → Mới) nếu người dùng chưa tắt.</summary>
    private void MaybeShowQuickStart()
    {
        try
        {
            if (IsSelfTestRun || _p == null || !QuickStartWindow.ShouldShowOnStart()) return;
            if (_p.Frames.Count > 0) return;   // dự án đã có mô hình
            ShowQuickStart(false);
        }
        catch { /* không chặn chương trình vì bảng trợ giúp */ }
    }

    internal QuickStartWindow ShowQuickStart(bool activate = true)
    {
        try
        {
            if (_quickStartWin == null || !_quickStartWin.IsLoaded)
            {
                var w = new QuickStartWindow(() => (_p, _res != null, _design), DoQuickStep);
                if (IsEnabled && IsLoaded) w.Owner = this;
                w.Closed += (s, e) => { if (ReferenceEquals(_quickStartWin, s)) _quickStartWin = null; };
                _quickStartWin = w;
                w.Show();
                if (Owner == null && w.Owner == this)
                {
                    // đặt ở góc phải trên cửa sổ chính để không che mặt bằng
                    w.WindowStartupLocation = WindowStartupLocation.Manual;
                    w.Left = Math.Max(0, Left + ActualWidth - w.Width - 24);
                    w.Top = Math.Max(0, Top + 90);
                }
            }
            else _quickStartWin.Refresh();
            if (activate) _quickStartWin.Activate();
            return _quickStartWin;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Không mở được bảng Bắt đầu nhanh:\n" + ex.Message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }

    private static TabItem FindTab(TabControl tc, string header) => tc?.Items.OfType<TabItem>().FirstOrDefault(t => (t.Header as string) == header);

    private void SelectLeft(string header) { var t = FindTab(tabLeft, header); if (t != null) t.IsSelected = true; }
    private void SelectView(string header) { var t = FindTab(tabView, header); if (t != null) t.IsSelected = true; }

    /// <summary>Bật một chế độ sửa mặt bằng theo Tag của nút (vd. "AddWall").</summary>
    private void CheckPlanMode(string tag)
    {
        SelectView("Mặt bằng");
        UpdateLayout();
        var rb = FindVisual<RadioButton>(tabView, r => (r.Tag as string) == tag);
        if (rb != null) rb.IsChecked = true;
    }

    private static T FindVisual<T>(DependencyObject root, Func<T, bool> pred) where T : DependencyObject
    {
        if (root == null) return null;
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is T t && pred(t)) return t;
            var r = FindVisual(c, pred);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>"Làm bước này" của bảng Bắt đầu nhanh (Id = QuickStartStep.Id).</summary>
    private void DoQuickStep(string id)
    {
        Activate();
        var e = new RoutedEventArgs();
        switch (id)
        {
            case "luoi": SelectLeft("Công trình"); txtSpansX.Focus(); SetStatus("Nhập bước cột X, Y, chiều cao tầng, cao độ chân cột rồi bấm 'Tạo mô hình' (hoặc Tệp → nhập bản vẽ / Revit / ETABS)."); break;
            case "tiet-dien": SelectLeft("Tiết diện"); OpenSectionManager(); break;
            case "cau-kien": CheckPlanMode("QuickGridBeam"); break;
            case "tuong": CheckPlanMode("AddWall"); break;
            case "tai-trong": SelectLeft("Tải trọng"); break;
            case "mong": SelectLeft("Địa chất"); SelectView("Mô hình móng"); break;
            case "phan-tich": MnuRun_Click(this, e); break;
            case "kiem-tra": SelectLeft("Kiểm tra"); SelectView("Biểu đồ nội lực"); break;
            case "thiet-ke": MnuDesignConcrete_Click(this, e); break;
            case "ho-so": MnuExpAll_Click(this, e); break;
        }
    }
}
