using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Ui;

namespace KetCauVXT.App;

// =====================================================================
// GIAO DIỆN GỌN – dùng chung cho MainWindow (Thiết kế), DetailingWindow (Triển khai hồ sơ), DetailSheetWindow (Dàn trang in):
//  • Menu + thanh công cụ dồn vào MỘT hàng mảnh (~28 px): nút ☰ ẩn / hiện menu (Alt hiện tạm), nút lệnh dạng biểu tượng
//    (Segoe MDL2 Assets / Segoe Fluent Icons) có chú giải tên + phím tắt; lệnh ít dùng tự vào "»" (tràn thanh công cụ).
//  • Khung bên (cây cấu kiện, bảng nhập, thuộc tính…) có nút « » thu gọn → dải 22 px chữ dọc, bấm để mở lại; nhớ độ rộng.
//  • F11 / nút ⛶: mở rộng vùng làm việc (ẩn menu, thanh công cụ, thanh trạng thái, các khung bên) – thanh nổi nhỏ góc phải;
//    Esc / F11 khôi phục. Ctrl+F1: ẩn / hiện thanh công cụ (như ribbon Office).
//  • Ctrl+Q / Ctrl+Shift+P: tìm lệnh nhanh (gõ không dấu được) – mọi mục menu và nút công cụ.
//  • Bố cục lưu cục bộ %AppData%\VietConst\ui.json (UiLayoutSettings) – không gửi đi đâu.
// =====================================================================

/// <summary>Một khung bên thu gọn được (phần tử nằm trong một ô cột của Grid).</summary>
public sealed class CompactPanel
{
    public FrameworkElement Content;
    public string Key, Title;
    /// <summary>true: khung bên trái (nút « ở mép phải khung); false: khung bên phải.</summary>
    public bool AtLeft;
    public GridSplitter Splitter;
    /// <summary>Cột của thanh chia (nếu thanh chia có cột cố định riêng) – thu về 0 khi khung thu gọn.</summary>
    public ColumnDefinition SplitCol;
    internal ColumnDefinition Col;
    internal double SavedWidth, SavedMin, SavedSplitW;
    internal bool Collapsed;
    internal Border Strip;
    internal Button Toggle;
    public CompactPanel(FrameworkElement content, string key, string title, bool atLeft, GridSplitter splitter = null, ColumnDefinition splitCol = null)
    { Content = content; Key = key; Title = title; AtLeft = atLeft; Splitter = splitter; SplitCol = splitCol; }
}

public sealed class CompactShell
{
    /// <summary>Chế độ tự kiểm (CI): không đọc / ghi ui.json, dùng mặc định.</summary>
    public static bool NoPersist;
    private static UiLayoutSettings _settings;
    public static UiLayoutSettings Settings => _settings ??= NoPersist ? new UiLayoutSettings() : UiLayoutSettings.Load();
    public static void SaveSettings() { if (!NoPersist) Settings.Save(); }

    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    private readonly Window _w;
    private readonly string _key;
    private Menu _menu;
    private FrameworkElement _toolbar;
    private readonly List<ToolBar> _bars = new();
    private FrameworkElement _status;
    private DockPanel _row;
    private ToggleButton _ham;
    private readonly List<CompactPanel> _panels = new();
    private readonly List<FrameworkElement> _fullHide = new();
    private readonly List<(FrameworkElement El, Visibility V)> _fullSaved = new();
    private readonly List<CompactPanel> _fullCollapsed = new();
    private Border _mini;
    private StackPanel _right;   // nút góc phải hàng công cụ (🔍 ⛶ ⋯) – AddRightElement thêm nút (vd. "Cập nhật")
    private bool _full, _menuTemp;
    /// <summary>Lệnh không nằm trên menu / thanh công cụ (vd. nút "＋ …" đã gộp) – vẫn tìm được bằng Ctrl+Q.</summary>
    private readonly List<CommandEntry> _extraCommands = new();
    /// <summary>Gọi khi bật / tắt chế độ mở rộng vùng làm việc (MainWindow dùng để ẩn cây mô hình…).</summary>
    public event Action<bool> FullModeChanged;
    public bool IsFull => _full;

    /// <summary>Thêm phần tử vào đầu cụm nút góc phải của hàng công cụ (luôn hiện, không bị đẩy vào ô tràn). Trả về false nếu chưa có hàng gọn.</summary>
    public bool AddRightElement(UIElement e)
    {
        if (_right == null || e == null) return false;
        _right.Children.Insert(0, e);
        return true;
    }
    private WindowLayout L => Settings.For(_key);

    private CompactShell(Window w, string key) { _w = w; _key = key; }

    /// <summary>Gắn giao diện gọn vào cửa sổ đã dựng xong (Content đã đặt). Lỗi bất kỳ → giữ giao diện cũ.</summary>
    public static CompactShell Attach(Window w, string key, Menu menu, FrameworkElement toolbar, FrameworkElement status,
        IEnumerable<CompactPanel> panels, IEnumerable<FrameworkElement> fullHide = null,
        IEnumerable<(string Text, Action Run)> miniActions = null, string groupAddTitle = null)
    {
        var sh = new CompactShell(w, key);
        try { sh.Install(menu, toolbar, status, panels, fullHide, miniActions, groupAddTitle); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("CompactShell: " + ex); }
        return sh;
    }

    private void Install(Menu menu, FrameworkElement toolbar, FrameworkElement status, IEnumerable<CompactPanel> panels,
        IEnumerable<FrameworkElement> fullHide, IEnumerable<(string Text, Action Run)> miniActions, string groupAddTitle)
    {
        var st = Settings;
        _menu = menu; _toolbar = toolbar; _status = status;
        if (toolbar is ToolBarTray tray) _bars.AddRange(tray.ToolBars);
        else if (toolbar is ToolBar tb) _bars.Add(tb);
        if (fullHide != null) _fullHide.AddRange(fullHide.Where(e => e != null));

        // ---- 1 hàng: ☰ | menu | thanh công cụ ……… | ⋯ 🔍 ⛶ ----
        var host = (menu?.Parent ?? toolbar?.Parent) as Panel;
        if (host != null)
        {
            int idx = host.Children.IndexOf((UIElement)menu ?? toolbar);
            _row = new DockPanel { LastChildFill = true, Background = SystemColors.MenuBarBrush, MinHeight = st.ToolbarHeight };
            if (menu != null) host.Children.Remove(menu);
            if (toolbar != null && toolbar.Parent == host) host.Children.Remove(toolbar);
            if (host is DockPanel) DockPanel.SetDock(_row, Dock.Top);
            host.Children.Insert(Math.Max(0, Math.Min(idx, host.Children.Count)), _row);

            if (menu != null)
            {
                _ham = new ToggleButton
                {
                    Content = Icon(UiGlyphs.Hamburger, 15), Width = 30, Height = 26, Margin = new Thickness(2, 1, 0, 1), Padding = new Thickness(0),
                    ToolTip = "Ẩn / hiện thanh menu (Alt: hiện tạm thời; Ctrl+Q: tìm lệnh)", Background = Brushes.Transparent, BorderThickness = new Thickness(0)
                };
                _ham.IsChecked = !L.MenuHidden;
                _ham.Click += (s, e) => SetMenuHidden(_ham.IsChecked != true, true);
                DockPanel.SetDock(_ham, Dock.Left); _row.Children.Add(_ham);
                menu.Background = Brushes.Transparent; menu.VerticalAlignment = VerticalAlignment.Center;
                if (st.Compact) menu.Padding = new Thickness(0);
                DockPanel.SetDock(menu, Dock.Left); _row.Children.Add(menu);
                menu.AddHandler(MenuItem.ClickEvent, new RoutedEventHandler((s, e) => { if (_menuTemp) _w.Dispatcher.BeginInvoke(new Action(EndTempMenu)); }));
                menu.LostKeyboardFocus += (s, e) => { if (_menuTemp) _w.Dispatcher.BeginInvoke(new Action(() => { if (!menu.IsKeyboardFocusWithin && !AnySubmenuOpen()) EndTempMenu(); }), System.Windows.Threading.DispatcherPriority.Background); };
            }
            // nút phải
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(RowButton(UiGlyphs.Search, "Tìm lệnh nhanh (Ctrl+Q hoặc Ctrl+Shift+P) – gõ không dấu được", ShowPalette));
            right.Children.Add(RowButton(UiGlyphs.FullScreen, "Mở rộng vùng làm việc (F11; Esc để khôi phục)", () => SetFull(true)));
            var more = RowButton(UiGlyphs.More, "Giao diện: ẩn menu / thanh công cụ, mật độ gọn / tiêu chuẩn, khôi phục bố cục", null);
            more.Click += (s, e) => { var cm = BuildViewMenu(); cm.PlacementTarget = more; cm.Placement = PlacementMode.Bottom; cm.IsOpen = true; };
            right.Children.Add(more);
            DockPanel.SetDock(right, Dock.Right); _row.Children.Add(right);
            _right = right;
            if (toolbar != null)
            {
                if (toolbar is ToolBarTray tt) { tt.Background = Brushes.Transparent; tt.IsLocked = true; }
                foreach (var b in _bars) { ToolBarTray.SetIsLocked(b, true); b.Background = Brushes.Transparent; b.Margin = new Thickness(4, 0, 0, 0); }
                toolbar.VerticalAlignment = VerticalAlignment.Center;
                _row.Children.Add(toolbar);
            }
            else _row.Children.Add(new Border());
        }

        // ---- nút công cụ gọn ----
        foreach (var b in _bars) CompactToolbar(b, st.Compact, groupAddTitle);

        // ---- thanh trạng thái 1 dòng ----
        if (status is Control sc)
        {
            sc.Height = st.StatusBarHeight; sc.Padding = new Thickness(0); sc.FontSize = st.Compact ? 11 : 12;
        }

        // ---- khung bên ----
        foreach (var p in panels ?? Enumerable.Empty<CompactPanel>()) if (p?.Content != null) AddPanel(p, st);

        // ---- thanh nổi khi mở rộng vùng làm việc ----
        BuildMini(miniActions);

        SetMenuHidden(L.MenuHidden, false);
        SetToolbarHidden(L.ToolbarHidden, false);

        _w.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnKey), true);
        _w.PreviewMouseDown += (s, e) => { if (_menuTemp && _menu != null && !_menu.IsMouseOver) EndTempMenu(); };
        _w.Closing += (s, e) => { Remember(); SaveSettings(); };
    }

    // ==================================================================
    private static TextBlock Icon(string glyph, double size = 16) => new()
    {
        Text = glyph, FontFamily = IconFont, FontSize = size, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
    };

    private static Button RowButton(string glyph, string tip, Action a)
    {
        var b = new Button { Content = Icon(glyph, 14), Width = 28, Height = 26, Padding = new Thickness(0), Margin = new Thickness(1, 1, 1, 1), ToolTip = tip, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        if (a != null) b.Click += (s, e) => a();
        return b;
    }

    private void CompactToolbar(ToolBar bar, bool compact, string groupAddTitle)
    {
        var items = bar.Items.Cast<object>().ToList();
        Button addBtn = null; ContextMenu addMenu = null; int addIdx = -1;
        foreach (var it in items)
        {
            if (it is TextBlock tbx && compact) { tbx.Visibility = Visibility.Collapsed; continue; }
            if (it is ComboBox cb && compact) { if (double.IsNaN(cb.Width) || cb.Width > 130) cb.Width = 130; cb.ToolTip ??= "Định dạng / lựa chọn"; continue; }
            if (it is not Button b || b.Content is not string text) continue;
            string tip = b.ToolTip as string ?? "";
            string full = tip.Length == 0 ? text : tip.StartsWith(text, StringComparison.Ordinal) ? tip : text + " – " + tip;
            b.Tag ??= text;   // tên gốc (tìm lệnh)
            if (groupAddTitle != null && text.StartsWith("＋ ", StringComparison.Ordinal) && text.Length > 2)
            {
                if (addBtn == null)
                {
                    addMenu = new ContextMenu();
                    addBtn = new Button { ToolTip = groupAddTitle + " (chọn loại)", Padding = new Thickness(4, 1, 4, 1) };
                    var sp = new StackPanel { Orientation = Orientation.Horizontal };
                    sp.Children.Add(Icon(UiGlyphs.Add, 15));
                    if (!compact) sp.Children.Add(new TextBlock { Text = " " + groupAddTitle, VerticalAlignment = VerticalAlignment.Center });
                    sp.Children.Add(new TextBlock { Text = " ▾", FontSize = 10, VerticalAlignment = VerticalAlignment.Center });
                    addBtn.Content = sp;
                    var cm = addMenu;
                    addBtn.Click += (s, e) => { cm.PlacementTarget = addBtn; cm.Placement = PlacementMode.Bottom; cm.IsOpen = true; };
                    addIdx = bar.Items.IndexOf(b);
                }
                var orig = b;
                var mi = new MenuItem { Header = text.Substring(2), ToolTip = tip };
                mi.Click += (s, e) => Invoke(orig);
                addMenu.Items.Add(mi);
                _extraCommands.Add(new CommandEntry { Title = (tip.Length > 0 ? tip : text.Substring(2)), Path = groupAddTitle, Tag = orig });
                bar.Items.Remove(b);
                continue;
            }
            string glyph = UiGlyphs.For(text);
            if (compact && glyph.Length > 0)
            {
                bool keepLabel = b.FontWeight == FontWeights.Bold;   // lệnh chính (Phân tích, Xuất tất cả) giữ chữ
                if (keepLabel)
                {
                    var sp = new StackPanel { Orientation = Orientation.Horizontal };
                    sp.Children.Add(Icon(glyph, 15));
                    sp.Children.Add(new TextBlock { Text = " " + CommandSearch.CleanTitle(text), VerticalAlignment = VerticalAlignment.Center });
                    b.Content = sp;
                }
                else b.Content = Icon(glyph, 16);
                b.Padding = new Thickness(5, 2, 5, 2);
                b.MinWidth = 28;
            }
            else if (!compact && glyph.Length > 0)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(Icon(glyph, 14));
                sp.Children.Add(new TextBlock { Text = " " + CommandSearch.CleanTitle(text), VerticalAlignment = VerticalAlignment.Center });
                b.Content = sp;
            }
            else if (compact) b.Padding = new Thickness(5, 1, 5, 1);
            b.ToolTip = full;
        }
        if (addBtn != null) bar.Items.Insert(Math.Max(0, Math.Min(addIdx, bar.Items.Count)), addBtn);
    }

    // ==================================================================
    // Khung bên
    // ==================================================================
    private void AddPanel(CompactPanel p, UiLayoutSettings st)
    {
        if (p.Content.Parent is not Grid g) return;
        int c = Grid.GetColumn(p.Content);
        if (c < 0 || c >= g.ColumnDefinitions.Count) return;
        p.Col = g.ColumnDefinitions[c];
        _panels.Add(p);
        if (st.Compact) System.Windows.Documents.TextElement.SetFontSize(p.Content, st.PanelFontSize);

        // nút «
        var tg = new Button
        {
            Content = Icon(p.AtLeft ? UiGlyphs.CollapseLeft : UiGlyphs.ExpandRight, 10), Width = 18, Height = 18, Padding = new Thickness(0),
            HorizontalAlignment = p.AtLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(1), ToolTip = "Thu gọn khung \"" + p.Title + "\" (mở rộng vùng làm việc)", Opacity = 0.75,
            Background = new SolidColorBrush(Color.FromArgb(0xE0, 0xF4, 0xF4, 0xF4)), BorderBrush = Brushes.Silver
        };
        tg.Click += (s, e) => SetCollapsed(p, true, true);
        Grid.SetColumn(tg, c); Grid.SetRow(tg, Grid.GetRow(p.Content)); Grid.SetRowSpan(tg, Math.Max(1, Grid.GetRowSpan(p.Content)));
        Panel.SetZIndex(tg, 50);
        g.Children.Add(tg);
        p.Toggle = tg;

        // dải thu gọn
        var label = new TextBlock { Text = p.Title, Margin = new Thickness(0, 6, 0, 6), FontSize = 11, LayoutTransform = new RotateTransform(p.AtLeft ? 90 : -90) };
        var spS = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        spS.Children.Add(Icon(p.AtLeft ? UiGlyphs.ExpandRight : UiGlyphs.CollapseLeft, 10));
        spS.Children.Add(label);
        var strip = new Border
        {
            Child = spS, Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)), BorderBrush = Brushes.Silver,
            BorderThickness = p.AtLeft ? new Thickness(0, 0, 1, 0) : new Thickness(1, 0, 0, 0), Cursor = Cursors.Hand,
            ToolTip = "Mở khung \"" + p.Title + "\"", Visibility = Visibility.Collapsed, Padding = new Thickness(0, 4, 0, 0)
        };
        strip.MouseLeftButtonUp += (s, e) => SetCollapsed(p, false, true);
        Grid.SetColumn(strip, c); Grid.SetRow(strip, Grid.GetRow(p.Content)); Grid.SetRowSpan(strip, Math.Max(1, Grid.GetRowSpan(p.Content)));
        Panel.SetZIndex(strip, 49);
        g.Children.Add(strip);
        p.Strip = strip;

        if (L.PanelWidths.TryGetValue(p.Key, out double wd) && wd >= 60 && wd < 3000 && p.Col.Width.IsAbsolute)
            p.Col.Width = new GridLength(Math.Max(wd, p.Col.MinWidth));
        if (L.Collapsed.Contains(p.Key)) SetCollapsed(p, true, false);
    }

    private void SetCollapsed(CompactPanel p, bool collapse, bool persist)
    {
        if (p.Col == null || p.Collapsed == collapse) return;
        if (collapse)
        {
            p.SavedWidth = p.Col.Width.IsAbsolute ? p.Col.Width.Value : Math.Max(p.Col.ActualWidth, 120);
            p.SavedMin = p.Col.MinWidth;
            p.Col.MinWidth = 0; p.Col.Width = new GridLength(22);
            p.Content.Visibility = Visibility.Collapsed; p.Toggle.Visibility = Visibility.Collapsed; p.Strip.Visibility = Visibility.Visible;
            if (p.Splitter != null) p.Splitter.Visibility = Visibility.Collapsed;
            if (p.SplitCol != null) { p.SavedSplitW = p.SplitCol.Width.IsAbsolute ? p.SplitCol.Width.Value : 0; p.SplitCol.Width = new GridLength(0); }
        }
        else
        {
            p.Col.Width = new GridLength(p.SavedWidth > 40 ? p.SavedWidth : 240);
            p.Col.MinWidth = p.SavedMin;
            p.Content.Visibility = Visibility.Visible; p.Toggle.Visibility = Visibility.Visible; p.Strip.Visibility = Visibility.Collapsed;
            if (p.Splitter != null) p.Splitter.Visibility = Visibility.Visible;
            if (p.SplitCol != null && p.SavedSplitW > 0) p.SplitCol.Width = new GridLength(p.SavedSplitW);
        }
        p.Collapsed = collapse;
        if (persist)
        {
            L.Collapsed.Remove(p.Key);
            if (collapse) L.Collapsed.Add(p.Key);
            SaveSettings();
        }
    }

    private void Remember()
    {
        foreach (var p in _panels)
            if (!p.Collapsed && p.Col != null && p.Col.Width.IsAbsolute && p.Col.ActualWidth > 40) L.PanelWidths[p.Key] = Math.Round(p.Col.ActualWidth);
        L.Collapsed.Clear();
        foreach (var p in _panels) if (p.Collapsed && !_fullCollapsed.Contains(p)) L.Collapsed.Add(p.Key);
    }

    // ==================================================================
    // Menu / thanh công cụ
    // ==================================================================
    private void SetMenuHidden(bool hidden, bool persist)
    {
        if (_menu == null) return;
        _menuTemp = false;
        _menu.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        if (_ham != null) _ham.IsChecked = !hidden;
        if (persist) { L.MenuHidden = hidden; SaveSettings(); }
    }

    private void EndTempMenu()
    {
        if (!_menuTemp || _menu == null) return;
        _menuTemp = false;
        if (L.MenuHidden) _menu.Visibility = Visibility.Collapsed;
    }

    private bool AnySubmenuOpen() => _menu != null && _menu.Items.OfType<MenuItem>().Any(m => m.IsSubmenuOpen);

    private void SetToolbarHidden(bool hidden, bool persist)
    {
        if (_toolbar == null) return;
        _toolbar.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        if (persist) { L.ToolbarHidden = hidden; SaveSettings(); }
    }

    private ContextMenu BuildViewMenu()
    {
        var cm = new ContextMenu();
        MenuItem C(string h, bool chk, Action a, string g = "") { var m = new MenuItem { Header = h, IsCheckable = true, IsChecked = chk, InputGestureText = g }; m.Click += (s, e) => a(); return m; }
        MenuItem I(string h, Action a, string g = "") { var m = new MenuItem { Header = h, InputGestureText = g }; m.Click += (s, e) => a(); return m; }
        if (_menu != null) cm.Items.Add(C("Ẩn thanh menu (Alt: hiện tạm)", L.MenuHidden, () => SetMenuHidden(!L.MenuHidden, true)));
        if (_toolbar != null) cm.Items.Add(C("Ẩn thanh công cụ", L.ToolbarHidden, () => SetToolbarHidden(!L.ToolbarHidden, true), "Ctrl+F1"));
        foreach (var p in _panels) { var pp = p; cm.Items.Add(C("Thu gọn khung \"" + p.Title + "\"", p.Collapsed, () => SetCollapsed(pp, !pp.Collapsed, true))); }
        cm.Items.Add(new Separator());
        cm.Items.Add(I("Mở rộng vùng làm việc", () => SetFull(true), "F11"));
        cm.Items.Add(I("Tìm lệnh nhanh…", ShowPalette, "Ctrl+Q"));
        cm.Items.Add(new Separator());
        var st = Settings;
        cm.Items.Add(C("Giao diện gọn (nút biểu tượng, chữ 12)", st.Compact, () => SetDensity(UiLayoutSettings.DensityCompact)));
        cm.Items.Add(C("Giao diện tiêu chuẩn (biểu tượng + chữ)", !st.Compact, () => SetDensity(UiLayoutSettings.DensityStandard)));
        cm.Items.Add(I("Khôi phục bố cục mặc định", ResetLayout));
        return cm;
    }

    private void SetDensity(string d)
    {
        Settings.Density = d; SaveSettings();
        MessageBox.Show(_w, "Mật độ giao diện \"" + d + "\" áp dụng khi mở lại cửa sổ.", "Việt Const", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ResetLayout()
    {
        foreach (var p in _panels.ToList()) SetCollapsed(p, false, false);
        Settings.Windows.Remove(_key);
        SetMenuHidden(false, false); SetToolbarHidden(false, false);
        SaveSettings();
    }

    // ==================================================================
    // Mở rộng vùng làm việc
    // ==================================================================
    private void BuildMini(IEnumerable<(string Text, Action Run)> actions)
    {
        if (_w.Content is not UIElement root) return;
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(RowButton(UiGlyphs.Restore, "Khôi phục giao diện (Esc / F11)", () => SetFull(false)));
        sp.Children.Add(RowButton(UiGlyphs.Search, "Tìm lệnh nhanh (Ctrl+Q)", ShowPalette));
        foreach (var (t, a) in actions ?? Enumerable.Empty<(string, Action)>())
        {
            string g = UiGlyphs.For(t);
            var b = g.Length > 0 ? RowButton(g, t, a) : new Button { Content = t, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(1), ToolTip = t };
            if (g.Length == 0) b.Click += (s, e) => a();
            sp.Children.Add(b);
        }
        _mini = new Border
        {
            Child = sp, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 18, 0),
            Background = new SolidColorBrush(Color.FromArgb(0xE8, 0xF7, 0xF7, 0xF7)), BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3), Padding = new Thickness(2), Visibility = Visibility.Collapsed
        };
        Panel.SetZIndex(_mini, 100);
        _w.Content = null;
        var g0 = new Grid();
        g0.Children.Add(root);
        g0.Children.Add(_mini);
        _w.Content = g0;
    }

    public void SetFull(bool on)
    {
        if (_full == on) return;
        _full = on;
        if (on)
        {
            _fullSaved.Clear(); _fullCollapsed.Clear();
            foreach (var el in new[] { (FrameworkElement)_row, _status }.Concat(_fullHide))
                if (el != null) { _fullSaved.Add((el, el.Visibility)); el.Visibility = Visibility.Collapsed; }
            foreach (var p in _panels.Where(p => !p.Collapsed)) { Remember(); SetCollapsed(p, true, false); _fullCollapsed.Add(p); }
            foreach (var p in _panels) if (p.Strip != null) p.Strip.Visibility = Visibility.Collapsed;
            foreach (var p in _panels) if (p.Col != null) p.Col.Width = new GridLength(0);
            if (_mini != null) _mini.Visibility = Visibility.Visible;
        }
        else
        {
            foreach (var (el, v) in _fullSaved) el.Visibility = v;
            foreach (var p in _panels)
            {
                if (_fullCollapsed.Contains(p)) SetCollapsed(p, false, false);
                else if (p.Collapsed) { p.Col.Width = new GridLength(22); p.Strip.Visibility = Visibility.Visible; }
            }
            _fullCollapsed.Clear();
            if (_mini != null) _mini.Visibility = Visibility.Collapsed;
        }
        FullModeChanged?.Invoke(on);
    }

    // ==================================================================
    // Phím
    // ==================================================================
    private void OnKey(object sender, KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;
        bool ctrl = (mods & ModifierKeys.Control) != 0, shift = (mods & ModifierKeys.Shift) != 0;
        if (e.Key == Key.F11 && !e.Handled) { SetFull(!_full); e.Handled = true; }
        else if (e.Key == Key.Escape && _full) { SetFull(false); e.Handled = true; }
        else if (e.Key == Key.F1 && ctrl && !e.Handled) { SetToolbarHidden(_toolbar != null && _toolbar.Visibility == Visibility.Visible, true); e.Handled = true; }
        else if (!e.Handled && ctrl && (e.Key == Key.Q || (shift && e.Key == Key.P))) { ShowPalette(); e.Handled = true; }
        else if (e.Key == Key.System && (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt) && _menu != null && _menu.Visibility != Visibility.Visible && !_full)
        {
            _menu.Visibility = Visibility.Visible; _menuTemp = true;   // Alt: hiện menu tạm thời (WPF kích hoạt menu khi nhả Alt)
        }
    }

    // ==================================================================
    // Tìm lệnh nhanh
    // ==================================================================
    public List<CommandEntry> Commands()
    {
        var list = new List<CommandEntry>();
        void Walk(ItemsControl ic, string path)
        {
            foreach (var o in ic.Items)
            {
                if (o is not MenuItem mi || mi.Visibility == Visibility.Collapsed) continue;
                string h = CommandSearch.CleanTitle(mi.Header as string ?? (mi.Header as TextBlock)?.Text ?? "");
                if (h.Length == 0) continue;
                string pth = path.Length == 0 ? h : path + " › " + h;
                if (mi.Items.Count > 0) Walk(mi, pth);
                else list.Add(new CommandEntry { Title = h, Path = path, Gesture = mi.InputGestureText ?? "", Tag = mi });
            }
        }
        if (_menu != null) Walk(_menu, "");
        foreach (var bar in _bars)
            foreach (var o in bar.Items)
                if (o is Button b && b.Tag is string t)
                {
                    string title = CommandSearch.CleanTitle(t);
                    if (title.Length <= 1) title = CommandSearch.CleanTitle((b.ToolTip as string ?? "").Split('–')[0]);
                    if (title.Length > 0 && !list.Any(c => string.Equals(c.Title, title, StringComparison.OrdinalIgnoreCase)))
                        list.Add(new CommandEntry { Title = title, Path = "Thanh công cụ", Tag = b });
                }
        list.AddRange(_extraCommands);
        list.Add(new CommandEntry { Title = "Mở rộng vùng làm việc", Path = "Giao diện", Gesture = "F11", Tag = (Action)(() => SetFull(!_full)) });
        if (_toolbar != null) list.Add(new CommandEntry { Title = "Ẩn / hiện thanh công cụ", Path = "Giao diện", Gesture = "Ctrl+F1", Tag = (Action)(() => SetToolbarHidden(_toolbar.Visibility == Visibility.Visible, true)) });
        if (_menu != null) list.Add(new CommandEntry { Title = "Ẩn / hiện thanh menu", Path = "Giao diện", Tag = (Action)(() => SetMenuHidden(_menu.Visibility == Visibility.Visible, true)) });
        return list;
    }

    private static readonly MethodInfo MenuClick = typeof(MenuItem).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
    private static readonly MethodInfo ButtonClick = typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

    /// <summary>Chạy lệnh như khi bấm (kể cả Command gắn với mục menu).</summary>
    public static void Invoke(object target)
    {
        try
        {
            switch (target)
            {
                case Action a: a(); break;
                case MenuItem mi when mi.IsEnabled: if (MenuClick != null) MenuClick.Invoke(mi, null); else mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, mi)); break;
                case ButtonBase bb when bb.IsEnabled: if (ButtonClick != null) ButtonClick.Invoke(bb, null); else bb.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, bb)); break;
            }
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            MessageBox.Show(ex.InnerException.Message, "Việt Const", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void ShowPalette()
    {
        var all = Commands();
        var box = new TextBox { FontSize = 14, Padding = new Thickness(4, 3, 4, 3), Margin = new Thickness(6, 6, 6, 4) };
        var lst = new ListBox { Margin = new Thickness(6, 0, 6, 6), BorderThickness = new Thickness(0) };
        var hint = new TextBlock { Text = "Gõ tên lệnh (không dấu được, vd. \"mat bang\", \"xuat\"). Enter: chạy · Esc: đóng", Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(8, 0, 8, 4) };
        var dp = new DockPanel();
        DockPanel.SetDock(box, Dock.Top); dp.Children.Add(box);
        DockPanel.SetDock(hint, Dock.Top); dp.Children.Add(hint);
        dp.Children.Add(lst);
        var win = new Window
        {
            Owner = _w, Content = new Border { Child = dp, BorderBrush = Brushes.SteelBlue, BorderThickness = new Thickness(1), Background = Brushes.White },
            Width = 560, Height = 420, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Title = "Tìm lệnh"
        };
        try
        {
            var tl = _w.PointToScreen(new Point(0, 0));
            var src = PresentationSource.FromVisual(_w);
            double sx = src?.CompositionTarget?.TransformFromDevice.M11 ?? 1, sy = src?.CompositionTarget?.TransformFromDevice.M22 ?? 1;
            win.Left = tl.X * sx + Math.Max(0, (_w.ActualWidth - win.Width) / 2); win.Top = tl.Y * sy + 60;
        }
        catch { win.WindowStartupLocation = WindowStartupLocation.CenterOwner; }

        void Fill()
        {
            var r = CommandSearch.Filter(all, box.Text);
            lst.ItemsSource = r.Select(c => new ListBoxItem
            {
                Content = new TextBlock
                {
                    Inlines =
                    {
                        new System.Windows.Documents.Run(c.Title),
                        new System.Windows.Documents.Run((c.Gesture.Length > 0 ? "   " + c.Gesture : "") + (c.Path.Length > 0 ? "   · " + c.Path : "")) { Foreground = Brushes.Gray, FontSize = 11 }
                    }
                },
                Tag = c, IsEnabled = c.Tag is not UIElement ue || ue.IsEnabled
            }).ToList();
            if (lst.Items.Count > 0) lst.SelectedIndex = 0;
        }
        bool shut = false;
        void Shut() { if (shut) return; shut = true; try { win.Close(); } catch { } }
        void Run()
        {
            if (lst.SelectedItem is not ListBoxItem it || it.Tag is not CommandEntry ce || !it.IsEnabled) return;
            Shut();
            _w.Dispatcher.BeginInvoke(new Action(() => Invoke(ce.Tag)), System.Windows.Threading.DispatcherPriority.Input);
        }
        box.TextChanged += (s, e) => Fill();
        box.PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Down && lst.Items.Count > 0) { lst.SelectedIndex = Math.Min(lst.SelectedIndex + 1, lst.Items.Count - 1); lst.ScrollIntoView(lst.SelectedItem); e.Handled = true; }
            else if (e.Key == Key.Up && lst.Items.Count > 0) { lst.SelectedIndex = Math.Max(lst.SelectedIndex - 1, 0); lst.ScrollIntoView(lst.SelectedItem); e.Handled = true; }
            else if (e.Key == Key.Enter) { Run(); e.Handled = true; }
        };
        lst.MouseDoubleClick += (s, e) => Run();
        win.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { Shut(); e.Handled = true; } };
        win.Deactivated += (s, e) => Shut();
        win.Loaded += (s, e) => { box.Focus(); Keyboard.Focus(box); };
        Fill();
        win.Show();
    }
}
