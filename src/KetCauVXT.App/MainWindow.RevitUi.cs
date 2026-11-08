using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KetCauVXT.Model;
using KetCauVXT.Ui;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// GIAO DIỆN KIỂU REVIT – chế độ Thiết kế (mặc định; Quản lý → "Giao diện cổ điển" để trở lại khung nhập liệu cũ):
//   • Ribbon một hàng tab: Kiến trúc | Kết cấu | Tải trọng | Phân tích | Xem | Quản lý – nút lớn (biểu tượng + chữ dưới),
//     tab ngữ cảnh "Sửa | <đối tượng>" hiện khi chọn tường / cửa / dầm / cột / ô sàn;
//   • bên trái: THUỘC TÍNH (bộ chọn kiểu + xem trước, thông số đối tượng theo nhóm Ràng buộc / Kích thước / Kết cấu / Tải trọng)
//     và TRÌNH DUYỆT DỰ ÁN (mặt bằng các tầng, 3D, bảng thống kê, họ & kiểu tường / cửa / tiết diện, thông tin dự án);
//   • giữa: vùng vẽ lớn (các ô nhập liệu cũ chuyển vào "Thông tin dự án (nhập liệu)");
//   • dưới mặt bằng: THANH TUỲ CHỌN theo công cụ (kiểu tường, đường định vị, chiều cao, vẽ chuỗi; kiểu cửa, lật) + gợi ý thao tác.
// Vẽ / đặt cửa: MainWindow.RevitDraw.cs. Hộp thoại: RevitDialogs.cs. Lõi: Model/WallAssembly.cs.
// Tự kiểm CI chạy giao diện cổ điển (giữ các nút cũ) trừ khi bật riêng.
// =====================================================================
public partial class MainWindow
{
    private bool _revitOn;
    private Border _rvRibbon, _rvLeft, _rvOptions;
    private TabControl _rvTabs;
    private TabItem _rvCtxTab, _rvTabBeforeCtx;
    private ComboBox _rvTypeSel, _rvOpenTypeCombo;
    private Canvas _rvTypePreview;
    private StackPanel _rvProps, _rvOptPanel;
    private TextBlock _rvPropTitle, _rvHint;
    private TreeView _rvBrowser;
    private DispatcherTimer _rvTimer;
    private string _rvSig = "", _rvBrowserSig = "", _rvOptKind;
    private bool _rvInputShown, _rvSyncing;
    private GridLength _rvSavedLeftW = new(560);

    private static readonly Brush RvRibbonBack = Frozen(Color.FromRgb(245, 246, 247));
    private static readonly Brush RvCtxBack = Frozen(Color.FromRgb(214, 236, 214));

    /// <summary>Gọi cuối Window_Loaded (sau InitCompactUi).</summary>
    private void InitRevitUi()
    {
        try
        {
            BuildRevitRibbon();
            BuildRevitLeft();
            BuildRevitOptions();
            _rvTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _rvTimer.Tick += (s, e) => RevitTick();
            _rvTimer.Start();
            if (_shell != null) _shell.FullModeChanged += full => { if (_rvRibbon != null && _revitOn) _rvRibbon.Visibility = full ? Visibility.Collapsed : Visibility.Visible; };
            bool on = SelfTestOut == null && CompactShell.Settings.RevitLayout;
            ApplyRevitLayout(on, false);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Giao diện Revit: " + ex.Message); }
    }

    // ==================================================================
    // Bật / tắt bố cục
    // ==================================================================
    private void ApplyRevitLayout(bool on, bool persist = true)
    {
        if (_rvRibbon == null) return;
        _revitOn = on;
        _rvRibbon.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (_rvOptions != null) _rvOptions.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (_ribbonHost != null) _ribbonHost.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        // [Hoàn thiện] vùng vẽ chiếm hết chỗ: bảng thông tin dưới mặt bằng ẩn (thông tin đã có ở khung Thuộc tính)
        if (infoPanel != null) infoPanel.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        _fitPending = true;
        if (on)
        {
            if (!_rvInputShown) _rvSavedLeftW = colLeftPanel.Width;
            if (_p != null) WallAssembly.Upgrade(_p);
            ShowInputPanel(false);
        }
        else
        {
            if (_rvLeft != null) _rvLeft.Visibility = Visibility.Collapsed;
            tabLeft.Visibility = Visibility.Visible;
            if (!_rvInputShown) colLeftPanel.Width = _rvSavedLeftW.Value > 50 ? _rvSavedLeftW : new GridLength(560);
            _rvInputShown = false;
            StopPlaceOpening();
        }
        if (persist)
        {
            CompactShell.Settings.DesignLayout = on ? UiLayoutSettings.LayoutRevit : UiLayoutSettings.LayoutClassic;
            CompactShell.SaveSettings();
        }
        _rvSig = "";
        RedrawPlan();
        SetStatus(on ? "Giao diện kiểu Revit: ribbon Kiến trúc / Kết cấu / Tải trọng…, khung Thuộc tính và Trình duyệt dự án bên trái (Quản lý → Giao diện cổ điển để trở lại)."
                     : "Giao diện cổ điển: khung nhập liệu bên trái, thanh lệnh cấu kiện trên mặt bằng (Xem → Giao diện Revit để bật lại).");
    }

    /// <summary>Khung trái kiểu Revit: true = các tab nhập liệu cũ (Thông tin dự án), false = Thuộc tính + Trình duyệt dự án.</summary>
    private void ShowInputPanel(bool input)
    {
        if (!_revitOn || _rvLeft == null) return;
        if (input && !_rvInputShown) { colLeftPanel.Width = _rvSavedLeftW.Value > 50 ? _rvSavedLeftW : new GridLength(560); }
        if (!input) { if (_rvInputShown) _rvSavedLeftW = colLeftPanel.Width; colLeftPanel.Width = new GridLength(300); }
        _rvInputShown = input;
        tabLeft.Visibility = input ? Visibility.Visible : Visibility.Collapsed;
        _rvLeft.Visibility = input ? Visibility.Collapsed : Visibility.Visible;
    }

    // ==================================================================
    // Ribbon
    // ==================================================================
    private static FrameworkElement RvGroup(string title, params UIElement[] buttons)
    {
        var sp = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var b in buttons) sp.Children.Add(b);
        var g = new DockPanel { Margin = new Thickness(2, 0, 2, 0) };
        var lb = new TextBlock { Text = title, FontSize = 10.5, Foreground = Brushes.DimGray, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 1) };
        DockPanel.SetDock(lb, Dock.Bottom);
        g.Children.Add(lb);
        g.Children.Add(sp);
        return new Border { Child = g, BorderBrush = new SolidColorBrush(Color.FromRgb(214, 214, 214)), BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(2, 2, 4, 0) };
    }

    private static Button RvBtn(string label, string glyph, string tip, Action click)
    {
        var sp = new StackPanel { Width = 62 };
        string g = string.IsNullOrEmpty(glyph) ? UiGlyphs.For(label) : glyph;
        sp.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(g) ? "" : g, FontFamily = new FontFamily("Segoe MDL2 Assets, Segoe Fluent Icons, Segoe UI Symbol"),
            FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(40, 70, 120)), Margin = new Thickness(0, 2, 0, 1)
        });
        sp.Children.Add(new TextBlock { Text = label, FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
        var b = new Button
        {
            Content = sp, ToolTip = tip, Padding = new Thickness(1), Margin = new Thickness(1, 0, 1, 0), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Top, MinHeight = 58, Focusable = false
        };
        b.Click += (s, e) => { try { click(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); } };
        return b;
    }

    private void RvTab(string header, params FrameworkElement[] groups)
    {
        // [Giao diện] các nhóm lệnh xuống dòng khi khung hẹp – hiện đủ trong khung nhìn, không phải kéo thanh cuộn ngang
        var sp = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 2, 2, 0) };
        foreach (var g in groups) sp.Children.Add(g);
        var sv = new ScrollViewer { Content = sp, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _rvTabs.Items.Add(new TabItem { Header = header, Content = sv, Padding = new Thickness(10, 2, 10, 2) });
    }

    private void RvMode(string tag) { StopPlaceOpening(); SetPlanMode(tag); }

    private void RvWallTool()
    {
        StopPlaceOpening();
        if (_p != null) WallAssembly.Upgrade(_p);
        RefreshEditCombos();
        SetPlanMode("AddWall");
        ShowOptionsFor("Wall");
    }

    private void RvShowMemberRibbon(string tab)
    {
        if (_ribbonHost == null || _ribbon == null) return;
        ShowPlanTab();
        _ribbonHost.Visibility = Visibility.Visible;
        var ti = _ribbon.Items.OfType<TabItem>().FirstOrDefault(t => (t.Header as string) == tab);
        if (ti != null) _ribbon.SelectedItem = ti;
        SetStatus("Thanh lệnh cấu kiện (gán tải theo đối tượng chọn) đang hiện phía trên mặt bằng – bấm lại \"Thanh lệnh cấu kiện\" ở tab Xem để ẩn.");
    }

    private void RvViewTab(string header)
    {
        var tab = tabView.Items.OfType<TabItem>().FirstOrDefault(t => (t.Header as string) == header);
        if (tab != null) tabView.SelectedItem = tab;
    }

    private void BuildRevitRibbon()
    {
        if (tabLeft?.Parent is not Grid root || root.Parent is not DockPanel dock) return;
        _rvTabs = new TabControl { Padding = new Thickness(0), BorderThickness = new Thickness(0, 1, 0, 0), Background = RvRibbonBack };
        OneRowTabs.Apply(_rvTabs);   // [Hoàn thiện] ribbon một hàng tab, nút » khi hẹp
        RvTab("Kiến trúc",
            RvGroup("Xây dựng",
                RvBtn("Tường", "", "Vẽ tường theo kiểu tường đang chọn (vẽ chuỗi, đường định vị, kích thước tạm) – WA", RvWallTool),
                RvBtn("Cửa đi", "", "Đặt cửa đi lên tường: tường tự cắt ô cửa (Space lật hướng mở) – DR", () => StartPlaceOpening(WallOpeningKind.Door)),
                RvBtn("Cửa sổ", "", "Đặt cửa sổ lên tường (bậu, chiều cao theo kiểu cửa) – WN", () => StartPlaceOpening(WallOpeningKind.Window)),
                RvBtn("Ô thoáng", "", "Đặt ô thoáng / lỗ chờ trên tường", () => StartPlaceOpening(WallOpeningKind.Vent)),
                RvBtn("Tường theo dầm", "", "Bấm vào dầm để đặt tường suốt chiều dài dầm", () => { RvMode("AssignWall"); ShowOptionsFor("Wall"); })),
            RvGroup("Kiểu & vật liệu",
                RvBtn("Kiểu tường…", "", "Sửa kiểu tường nhiều lớp: lõi gạch / BT, vữa trát, ốp đá, gạch men – tải 1 m² tự tính", () => OpenWallTypeEditor(null)),
                RvBtn("Kiểu cửa…", "", "Kiểu cửa đi / cửa sổ / ô thoáng: rộng, cao, bậu, cách mở", OpenOpeningTypes),
                RvBtn("Vật liệu…", "", "Thư viện vật liệu tường (γ)", OpenMaterials),
                RvBtn("Danh sách tường…", "", "Bảng các đoạn tường đã vẽ", () => MnuWallList_Click(this, new RoutedEventArgs()))));
        RvTab("Kết cấu",
            RvGroup("Khung",
                RvBtn("Cột", "", "Đặt cột: bấm điểm / giao trục (lệnh C)", () => { StopPlaceOpening(); ShowPlanTab(); RunCmd(CadCmd.Column, ""); }),
                RvBtn("Dầm", "", "Vẽ dầm: bấm điểm đầu – điểm cuối, vẽ nối tiếp (L)", () => RvMode("AddBeam")),
                RvBtn("Dầm cong", "", "Dầm cong: điểm đầu, điểm cuối, một điểm trên cung", () => RvMode("AddCurvedBeam")),
                RvBtn("Dầm theo lưới", "", "Kéo khung để vẽ nhanh dầm trên các đường trục", () => RvMode("QuickGridBeam")),
                RvBtn("Tiết diện…", "", "Thêm / sửa tiết diện dầm, cột", () => MnuSectionManager_Click(this, new RoutedEventArgs()))),
            RvGroup("Sàn",
                RvBtn("Sàn", "", "Bấm vào ô kín bởi dầm để thêm ô sàn", () => RvMode("AddSlab")),
                RvBtn("Ban công", "", "Bấm dầm biên để thêm ban công công xôn", () => RvMode("Balcony")),
                RvBtn("Cầu thang", "", "Bấm ô để làm ô cầu thang", () => RvMode("Stair")),
                RvBtn("Giếng trời", "", "Bấm ô để làm giếng trời / thông tầng", () => RvMode("Void")),
                RvBtn("Sàn vệ sinh", "", "Bấm ô sàn để đổi thành sàn vệ sinh", () => RvMode("WC")),
                RvBtn("Hạ cốt sàn", "", "Hạ cốt ô sàn (vệ sinh, ban công)", () => RvMode("SlabDropMode"))));
        RvTab("Tải trọng",
            RvGroup("Gán tải theo cấu kiện",
                RvBtn("Tải dầm", "", "Gán tải phân bố / tập trung cho các dầm đang chọn", () => RvShowMemberRibbon(RtDam)),
                RvBtn("Tải sàn", "", "Gán tải ô sàn / theo công năng", () => RvShowMemberRibbon(RtSan)),
                RvBtn("Tải cột", "", "Gán tải đầu cột", () => RvShowMemberRibbon(RtCot)),
                RvBtn("Tải móng", "", "Tải chân cột / móng", () => RvShowMemberRibbon(RtMong)),
                RvBtn("Tải tường", "", "Tải tường lên dầm (tự trừ diện tích cửa đã đặt)", () => RvShowMemberRibbon(RtTuong))),
            RvGroup("Tự động",
                RvBtn("Tường CAD → tải dầm", "", "Tường trên bản vẽ CAD liên kết nằm trên dầm → tạo tường, tải dồn vào dầm", CadWallsToBeams),
                RvBtn("Cấu tạo sàn…", "", "Cấu tạo sàn (lớp hoàn thiện + hoạt tải)", () => MnuBuildUps_Click(this, new RoutedEventArgs())),
                RvBtn("Gợi ý lanh tô", "", "Lanh tô đề xuất cho mọi cửa đã đặt (gối, chiều cao, cảnh báo tường 110)", ShowLintelProposals)));
        RvTab("Phân tích",
            RvGroup("Tính toán",
                RvBtn("Phân tích (F5)", "", "Chạy phân tích kết cấu", () => MnuRun_Click(this, new RoutedEventArgs())),
                RvBtn("Bảng kết quả", "", "Chuyển vị, phản lực, nội lực… (Ctrl+T)", () => MnuTables_Click(this, new RoutedEventArgs())),
                RvBtn("Thiết kế BTCT", "", "Thiết kế thép dầm, cột, sàn, móng", () => MnuDesignConcrete_Click(this, new RoutedEventArgs())),
                RvBtn("Khối lượng", "", "Bảng khối lượng vật tư (xây, trát trừ cửa thật)", () => MnuQuantities_Click(this, new RoutedEventArgs()))));
        RvTab("Xem",
            RvGroup("Khung nhìn",
                RvBtn("Mặt bằng", "", "Mặt bằng tầng đang xem", () => RvViewTab("Mặt bằng")),
                RvBtn("3D", "", "Khung nhìn 3D", () => RvViewTab("3D")),
                RvBtn("Biểu đồ", "", "Biểu đồ nội lực", () => RvViewTab("Biểu đồ nội lực")),
                RvBtn("Vừa màn hình", "", "Phóng vừa khung (ZE)", FitPlan)),
            RvGroup("Đồ hoạ",
                RvBtn("Hiển thị / Đồ hoạ…", "", "Bật / tắt thẻ dầm, cột, sàn, tường, cửa; mức chi tiết; tránh chồng nhãn (VG)", OpenVisibility),
                RvBtn("Thanh lệnh cấu kiện", "", "Hiện / ẩn thanh lệnh theo cấu kiện (gán tải, sửa dầm, Link CAD)", ToggleMemberRibbon),
                RvBtn("Mở rộng (F11)", "", "Ẩn ribbon, khung bên – chỉ còn vùng vẽ", () => _shell?.SetFull(!_shell.IsFull))),
            QuickVgGroup());   // [Hoàn thiện] bật / tắt nhanh thẻ, kích thước, tải – MainWindow.RevitFinish.cs
        RvTab("Quản lý",
            RvGroup("Dự án",
                RvBtn("Thông tin dự án", "", "Các ô nhập liệu: công trình, lưới trục, tầng, tải, gió, động đất, vật liệu, địa chất, kết quả", () => ShowInputPanel(true)),
                RvBtn("Thuộc tính", "", "Khung Thuộc tính + Trình duyệt dự án", () => ShowInputPanel(false)),
                RvBtn("Link CAD", "", "Liên kết bản vẽ AutoCAD làm nền (xref)", () => RvShowMemberRibbon(RtCad))),
            RvGroup("Giao diện",
                RvBtn("Giao diện cổ điển", "", "Trở về khung nhập liệu bên trái và thanh lệnh cấu kiện", () => ApplyRevitLayout(false))));
        _rvRibbon = new Border
        {
            Child = _rvTabs, Background = RvRibbonBack, BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)), BorderThickness = new Thickness(0, 0, 0, 1),
            Tag = "RevitRibbon"
        };
        DockPanel.SetDock(_rvRibbon, Dock.Top);
        dock.Children.Insert(dock.Children.IndexOf(root), _rvRibbon);
        // nút trở về giao diện Revit trong menu "Xem" (giao diện cổ điển)
        var viewMenu = mainMenu.Items.OfType<MenuItem>().FirstOrDefault(m => ((m.Header as string) ?? "").Replace("_", "").StartsWith("Xem"));
        if (viewMenu != null)
        {
            var mi = new MenuItem { Header = "Giao diện kiểu _Revit (ribbon, Thuộc tính, Trình duyệt dự án)", IsCheckable = false };
            mi.Click += (s, e) => ApplyRevitLayout(!_revitOn);
            viewMenu.Items.Add(new Separator());
            viewMenu.Items.Add(mi);
        }
    }

    private void ToggleMemberRibbon()
    {
        if (_ribbonHost == null) return;
        _ribbonHost.Visibility = _ribbonHost.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    // ==================================================================
    // Khung trái: Thuộc tính + Trình duyệt dự án
    // ==================================================================
    private void BuildRevitLeft()
    {
        if (tabLeft?.Parent is not Grid root) return;
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });

        var top = new DockPanel { Margin = new Thickness(4, 4, 4, 2) };
        var btnInput = new Button { Content = "Thông tin dự án ►", Padding = new Thickness(6, 1, 6, 1), ToolTip = "Các ô nhập liệu cũ: công trình, lưới trục, tầng, tải, gió, động đất, vật liệu, địa chất, kết quả" };
        btnInput.Click += (s, e) => ShowInputPanel(true);
        DockPanel.SetDock(btnInput, Dock.Right);
        top.Children.Add(btnInput);
        _rvPropTitle = new TextBlock { Text = "Thuộc tính", FontWeight = FontWeights.Bold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        top.Children.Add(_rvPropTitle);
        Grid.SetRow(top, 0);
        grid.Children.Add(top);

        var props = new DockPanel { Margin = new Thickness(4, 0, 4, 0) };
        var typeRow = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        _rvTypePreview = new Canvas { Width = 54, Height = 44, Background = Brushes.White, ClipToBounds = true, Margin = new Thickness(0, 0, 6, 0) };
        DockPanel.SetDock(_rvTypePreview, Dock.Left);
        typeRow.Children.Add(new Border { Child = _rvTypePreview, BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1) });
        _rvTypeSel = new ComboBox { VerticalAlignment = VerticalAlignment.Center, ToolTip = "Bộ chọn kiểu (Type Selector): đổi kiểu cho đối tượng đang chọn / công cụ đang vẽ" };
        _rvTypeSel.SelectionChanged += (s, e) => RvTypeSelected();
        typeRow.Children.Add(_rvTypeSel);
        DockPanel.SetDock(typeRow, Dock.Top);
        props.Children.Add(typeRow);
        var editType = new Button { Content = "Sửa kiểu…", HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 2, 0, 4) };
        editType.Click += (s, e) => RvEditType();
        DockPanel.SetDock(editType, Dock.Top);
        props.Children.Add(editType);
        _rvProps = new StackPanel();
        props.Children.Add(new ScrollViewer { Content = _rvProps, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Grid.SetRow(props, 1);
        grid.Children.Add(props);

        var split = new GridSplitter { Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Color.FromRgb(224, 224, 224)) };
        Grid.SetRow(split, 2);
        grid.Children.Add(split);

        var br = new DockPanel { Margin = new Thickness(4, 2, 4, 4) };
        var bt = new TextBlock { Text = "Trình duyệt dự án", FontWeight = FontWeights.Bold, FontSize = 13, Margin = new Thickness(0, 0, 0, 2) };
        DockPanel.SetDock(bt, Dock.Top);
        br.Children.Add(bt);
        _rvBrowser = new TreeView();
        _rvBrowser.MouseDoubleClick += (s, e) => { if (_rvBrowser.SelectedItem is TreeViewItem ti && ti.Tag is Action a) { e.Handled = true; a(); } };
        _rvBrowser.SelectedItemChanged += (s, e) => { if (_rvBrowser.SelectedItem is TreeViewItem ti && ti.Tag is Action a && ti.Items.Count == 0 && !_rvSyncing) a(); };
        br.Children.Add(_rvBrowser);
        Grid.SetRow(br, 3);
        grid.Children.Add(br);

        _rvLeft = new Border { Child = grid, Background = Brushes.White, Visibility = Visibility.Collapsed, Tag = "RevitLeft" };
        Grid.SetColumn(_rvLeft, Grid.GetColumn(tabLeft));
        Grid.SetRow(_rvLeft, Grid.GetRow(tabLeft));
        Grid.SetRowSpan(_rvLeft, Math.Max(1, Grid.GetRowSpan(tabLeft)));
        root.Children.Add(_rvLeft);
    }

    private static TreeViewItem TI(string header, Action act = null, bool expanded = false) =>
        new() { Header = header, Tag = act, IsExpanded = expanded };

    private void RefreshBrowser()
    {
        if (_rvBrowser == null || _p == null) return;
        _rvSyncing = true;
        try
        {
            _rvBrowser.Items.Clear();
            var views = TI("Mặt bằng tầng", null, true);
            foreach (var st in _p.Stories.OrderBy(s => s.Elevation))
            {
                string name = st.Name;
                views.Items.Add(TI($"{name} (+{st.Elevation:0.000})", () => { ShowPlanTab(); if (cbStory.Items.Contains(name)) cbStory.SelectedItem = name; }));
            }
            _rvBrowser.Items.Add(views);
            AddLevelViewsToBrowser();   // mặt cắt ngang tầng theo bộ môn + mặt cắt đứng – MainWindow.Furnish.cs
            AddElevationViewsToBrowser();   // mặt đứng 4 hướng – MainWindow.SelectGridLevel.cs
            _rvBrowser.Items.Add(TI("Khung nhìn 3D", () => RvViewTab("3D")));
            _rvBrowser.Items.Add(TI("Biểu đồ nội lực", () => RvViewTab("Biểu đồ nội lực")));
            var sch = TI("Bảng thống kê");
            sch.Items.Add(TI("Khối lượng vật tư", () => MnuQuantities_Click(this, new RoutedEventArgs())));
            sch.Items.Add(TI("Bảng kết quả", () => MnuTables_Click(this, new RoutedEventArgs())));
            sch.Items.Add(TI("Gợi ý lanh tô cửa", ShowLintelProposals));
            _rvBrowser.Items.Add(sch);
            var fam = TI("Họ và kiểu", null, true);
            var walls = TI($"Tường cơ bản ({_p.WallTypes.Count})");
            foreach (var t in _p.WallTypes) { string n = t.Name; walls.Items.Add(TI(n, () => OpenWallTypeEditor(n))); }
            fam.Items.Add(walls);
            var doors = TI("Cửa đi"); var wins = TI("Cửa sổ / ô thoáng");
            foreach (var t in _p.OpeningTypes ?? new List<OpeningType>())
                (t.Kind == WallOpeningKind.Door ? doors : wins).Items.Add(TI(t.Name, OpenOpeningTypes));
            fam.Items.Add(doors); fam.Items.Add(wins);
            fam.Items.Add(TI("Tiết diện dầm / cột", () => MnuSectionManager_Click(this, new RoutedEventArgs())));
            _rvBrowser.Items.Add(fam);
            _rvBrowser.Items.Add(TI("Thông tin dự án (nhập liệu)", () => ShowInputPanel(true)));
        }
        finally { _rvSyncing = false; }
    }

    // ==================================================================
    // Thanh tuỳ chọn (Options Bar) dưới mặt bằng
    // ==================================================================
    private void BuildRevitOptions()
    {
        if (planHost?.Parent is not DockPanel dock) return;
        var g = new DockPanel { Margin = new Thickness(4, 2, 4, 2) };
        _rvHint = new TextBlock { Foreground = Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        _rvOptPanel = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(_rvOptPanel, Dock.Left);
        g.Children.Add(_rvOptPanel);
        g.Children.Add(_rvHint);
        _rvOptions = new Border
        {
            Child = g, Background = new SolidColorBrush(Color.FromRgb(236, 241, 247)), BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            BorderThickness = new Thickness(0, 1, 0, 0), Tag = "OptionsBar", Visibility = Visibility.Collapsed
        };
        DockPanel.SetDock(_rvOptions, Dock.Bottom);
        dock.Children.Insert(dock.Children.IndexOf(planHost), _rvOptions);
    }

    private static TextBlock OptLabel(string s) => new() { Text = s, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };

    /// <summary>Nội dung thanh tuỳ chọn theo công cụ: "Wall", "Opening" hoặc null (chỉ gợi ý).</summary>
    private void ShowOptionsFor(string kind)
    {
        if (_rvOptPanel == null || kind == _rvOptKind) return;
        _rvOptKind = kind;
        _rvOptPanel.Children.Clear();
        _rvOpenTypeCombo = null;
        if (_p == null) return;
        if (kind == "Wall")
        {
            _rvOptPanel.Children.Add(OptLabel("Kiểu tường:"));
            var cbType = new ComboBox { Width = 210, ItemsSource = _p.WallTypes.Select(t => t.Name).ToList(), SelectedItem = cbEditWallType.SelectedItem };
            cbType.SelectionChanged += (s, e) => { if (cbType.SelectedItem is string n) { cbEditWallType.SelectedItem = n; _rvSig = ""; } };
            _rvOptPanel.Children.Add(cbType);
            _rvOptPanel.Children.Add(OptLabel("Đường định vị:"));
            var cbLoc = new ComboBox { Width = 160, ItemsSource = LocationLines.All, SelectedItem = _rvLocation };
            cbLoc.SelectionChanged += (s, e) => { if (cbLoc.SelectedItem is string l) _rvLocation = l; };
            _rvOptPanel.Children.Add(cbLoc);
            _rvOptPanel.Children.Add(OptLabel("Cao (m, 0 = tới tầng trên):"));
            var tbH = new TextBox { Width = 44, Text = txtEditWallH.Text, VerticalContentAlignment = VerticalAlignment.Center };
            tbH.TextChanged += (s, e) => txtEditWallH.Text = tbH.Text;
            _rvOptPanel.Children.Add(tbH);
            var chain = new CheckBox { Content = "Chuỗi", IsChecked = _rvChain, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), ToolTip = "Vẽ nối tiếp: điểm cuối đoạn trước là điểm đầu đoạn sau" };
            chain.Checked += (s, e) => _rvChain = true;
            chain.Unchecked += (s, e) => _rvChain = false;
            _rvOptPanel.Children.Add(chain);
        }
        else if (kind == "Opening")
        {
            _rvOptPanel.Children.Add(OptLabel("Kiểu cửa:"));
            var kindNow = _rvPlaceType?.Kind ?? WallOpeningKind.Door;
            _rvOpenTypeCombo = new ComboBox
            {
                Width = 170, ItemsSource = _p.OpeningTypes.Where(t => t.Kind == kindNow || (kindNow != WallOpeningKind.Door && t.Kind != WallOpeningKind.Door)).ToList(),
                SelectedItem = _rvPlaceType
            };
            _rvOpenTypeCombo.SelectionChanged += (s, e) => { if (_rvOpenTypeCombo?.SelectedItem is OpeningType t && _rvPlaceType != null) _rvPlaceType = t; };
            _rvOptPanel.Children.Add(_rvOpenTypeCombo);
            var flip = new Button { Content = "Lật hướng mở (Space)", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(6, 0, 6, 0), Focusable = false };
            flip.Click += (s, e) => { _rvPlaceFlip = !_rvPlaceFlip; if (_lastMouse != null) RevitPlanMove(_lastMouse.Value); };
            var hand = new Button { Content = "Đổi bản lề (Shift+Space)", Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(6, 0, 6, 0), Focusable = false };
            hand.Click += (s, e) => { _rvPlaceHand = !_rvPlaceHand; if (_lastMouse != null) RevitPlanMove(_lastMouse.Value); };
            _rvOptPanel.Children.Add(flip);
            _rvOptPanel.Children.Add(hand);
        }
    }

    // ==================================================================
    // Đồng bộ theo lựa chọn / công cụ (đồng hồ 350 ms – rẻ)
    // ==================================================================
    private void RevitTick()
    {
        if (!_revitOn || !IsLoaded) return;
        EnforceLeftPanel();
        if (_p == null) return;
        string bsig = $"{_p.GetHashCode()}|{_p.Stories.Count}|{_p.WallTypes.Count}|{_p.OpeningTypes?.Count}|{string.Join(",", _p.Stories.Select(s => s.Name + "@" + s.Elevation))}|{_p.Arch?.Sections?.Count}";
        if (bsig != _rvBrowserSig) { _rvBrowserSig = bsig; WallAssembly.Upgrade(_p); RefreshBrowser(); }
        string sig = $"{_mode}|{_rvPlaceType?.Name}|{_sel?.Id}|{_selWall?.Id}|{_selSlab?.Id}|{_selOpening?.Id}|{_rvSelOpen?.Id}|{_rvSelOpen?.Type}|{_p.GetHashCode()}|{CurrentStory}|{_selWall?.Type}|{_sel?.Section}";
        if (sig != _rvSig) { _rvSig = sig; RevitSelectionChanged(); }
        if (_rvHint != null && txtStatus != null && _rvHint.Text != txtStatus.Text) _rvHint.Text = txtStatus.Text;
    }

    /// <summary>Khung trái dùng chung cột với khung nhập liệu (giao diện gọn có thể thu / mở cột): giữ đúng một khung hiện,
    /// ẩn cả hai khi cột bị thu gọn (≤ 30 px) hoặc đang mở rộng vùng làm việc.</summary>
    private void EnforceLeftPanel()
    {
        if (_rvLeft == null) return;
        bool collapsed = colLeftPanel.Width.IsAbsolute && colLeftPanel.Width.Value <= 30;
        var wantLeft = !collapsed && !_rvInputShown ? Visibility.Visible : Visibility.Collapsed;
        var wantTab = !collapsed && _rvInputShown ? Visibility.Visible : Visibility.Collapsed;
        if (_rvLeft.Visibility != wantLeft) _rvLeft.Visibility = wantLeft;
        if (tabLeft.Visibility != wantTab) tabLeft.Visibility = wantTab;
    }

    /// <summary>Cập nhật khung Thuộc tính, tab ngữ cảnh, thanh tuỳ chọn theo đối tượng chọn / công cụ.</summary>
    private void RevitSelectionChanged()
    {
        if (!_revitOn || _p == null || _rvProps == null) return;
        _rvSyncing = true;
        try
        {
            _rvProps.Children.Clear();
            _rvTypePreview.Children.Clear();
            string ctx = null;
            if (_rvPlaceType != null)
            {
                _rvPropTitle.Text = "Thuộc tính – " + _rvPlaceType.KindText + " (đang đặt)";
                SetTypeItems(_p.OpeningTypes.Select(t => t.Name), _rvPlaceType.Name);
                OpeningPreview(_rvPlaceType);
                OpeningTypeProps(_rvPlaceType);
                ShowOptionsFor("Opening");
            }
            else if (_rvSelOpen != null && _rvSelWall != null && _p.Walls.Contains(_rvSelWall))
            {
                var t = WallAssembly.TypeOf(_p, _rvSelOpen);
                _rvPropTitle.Text = "Thuộc tính – " + (t?.KindText ?? "Cửa");
                SetTypeItems(_p.OpeningTypes.Select(q => q.Name), _rvSelOpen.Type);
                if (t != null) { OpeningPreview(t); }
                var w = _rvSelWall; var o = _rvSelOpen;
                PropHeader("Ràng buộc");
                PropRow("Tường chủ", $"#{w.Id} ({w.Type})");
                PropRow("Tầng", w.Story);
                PropEdit("Tâm cách đầu tường (mm)", o.Offset * 1000, v =>
                {
                    string snap = Snapshot();
                    var r = WallAssembly.MoveOpening(_p, w, o, v / 1000);
                    if (!r.Ok) { SetStatus(r.Message); return; }
                    AfterEdit(snap, r, w); SelectHostedOpening(w, o);
                });
                if (t != null) OpeningTypeProps(t);
                PropRow("Hướng mở", o.Flip ? "Lật" : "Thường");
                ctx = "Cửa";
                ShowOptionsFor(null);
            }
            else if (_selWall != null && _p.Walls.Contains(_selWall))
            {
                var w = _selWall;
                var wt = _p.WallTypes.FirstOrDefault(t => t.Name == w.Type);
                _rvPropTitle.Text = "Thuộc tính – Tường cơ bản";
                SetTypeItems(_p.WallTypes.Select(t => t.Name), w.Type);
                WallPreview(wt);
                var c = WallMath.Calc(_p, w);
                PropHeader("Ràng buộc");
                PropRow("Tầng đặt", w.Story);
                PropEdit("Chiều cao (m, 0 = tự động)", w.Height, v =>
                {
                    string snap = Snapshot();
                    w.Height = Math.Max(0, v);
                    AfterEdit(snap, new EditResult { Ok = true, Message = $"Đã đổi chiều cao tường #{w.Id}." }, w);
                });
                PropHeader("Kích thước");
                PropRow("Chiều dài", $"{w.Length * 1000:0} mm");
                if (wt != null) PropRow("Chiều dày", WallAssembly.Describe(wt));
                PropHeader("Kết cấu");
                PropRow("Chức năng", wt?.Function ?? "");
                PropRow("Số cửa gắn", $"{w.Openings?.Count ?? 0}");
                PropHeader("Tải trọng");
                if (c != null)
                {
                    PropRow("Chiều cao tính tải", $"{c.H:0.00} m{(c.AutoHeight ? " (tự động)" : "")}");
                    PropRow("Diện tích cửa", $"{c.OpenArea:0.00} m² ({c.Ratio * 100:0}%)");
                    PropRow("Tải lên dầm q", $"{c.Q:0.00} kN/m");
                }
                ctx = "Tường";
                ShowOptionsFor(_mode == PlanEditMode.AddWall ? "Wall" : null);
            }
            else if (_sel != null && _p.Frames.FirstOrDefault(f => f.Id == _sel.Id) is FrameElement fr)
            {
                _rvPropTitle.Text = "Thuộc tính – " + (fr.IsVerticalKind ? "Cột" : "Dầm");
                SetTypeItems(_p.FrameSections.Select(s => s.Name), fr.Section);
                PropHeader("Ràng buộc");
                PropRow("Tầng", fr.Story);
                PropRow("Tên", fr.Label);
                PropHeader("Kích thước");
                var ni = _p.Nodes.FirstOrDefault(n => n.Id == fr.NodeI); var nj = _p.Nodes.FirstOrDefault(n => n.Id == fr.NodeJ);
                if (ni != null && nj != null) PropRow("Chiều dài", $"{Math.Sqrt((nj.X - ni.X) * (nj.X - ni.X) + (nj.Y - ni.Y) * (nj.Y - ni.Y) + (nj.Z - ni.Z) * (nj.Z - ni.Z)) * 1000:0} mm");
                PropRow("Tiết diện", fr.Section);
                ctx = fr.IsVerticalKind ? "Cột" : "Dầm";
                ShowOptionsFor(null);
            }
            else if (_selSlab != null && _p.Slabs.Contains(_selSlab))
            {
                _rvPropTitle.Text = "Thuộc tính – Sàn";
                SetTypeItems(_p.SlabSections.Select(s => s.Name), _selSlab.Section);
                PropHeader("Ràng buộc");
                PropRow("Tầng", _selSlab.Story);
                PropRow("Tên", _selSlab.Label);
                PropHeader("Kết cấu");
                PropRow("Công năng", ModelEdit.UseOf(_p, _selSlab).ToString());
                PropRow("Cấu tạo", string.IsNullOrEmpty(_selSlab.BuildUp) ? "(theo tải nhập)" : _selSlab.BuildUp);
                ctx = "Sàn";
                ShowOptionsFor(null);
            }
            else if (_mode == PlanEditMode.AddWall || _mode == PlanEditMode.AssignWall)
            {
                var wt = _p.WallTypes.FirstOrDefault(t => t.Name == cbEditWallType.SelectedItem as string);
                _rvPropTitle.Text = "Thuộc tính – Tường (đang vẽ)";
                SetTypeItems(_p.WallTypes.Select(t => t.Name), wt?.Name);
                WallPreview(wt);
                if (wt != null)
                {
                    PropHeader("Kiểu");
                    PropRow("Cấu tạo", WallAssembly.Describe(wt));
                    foreach (var l in wt.Layers) PropRow("  " + l.Function, $"{l.Material} {l.Thickness * 1000:0} mm");
                }
                ShowOptionsFor("Wall");
            }
            else
            {
                _rvPropTitle.Text = "Thuộc tính – Mặt bằng";
                SetTypeItems(Array.Empty<string>(), null);
                PropHeader("Khung nhìn");
                PropRow("Tầng", CurrentStory);
                PropRow("Tỷ lệ", $"{_scale:0.#} px/m");
                PropRow("Mức chi tiết", _vg.Detail);
                var vg = new Button { Content = "Hiển thị / Đồ hoạ…", Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 1, 8, 1) };
                vg.Click += (s, e) => OpenVisibility();
                _rvProps.Children.Add(vg);
                PropHeader("Số liệu dự án");
                PropRow("Tường / cửa", $"{_p.Walls.Count} / {_p.Walls.Sum(w => w.Openings?.Count ?? 0)}");
                PropRow("Kiểu tường", $"{_p.WallTypes.Count}");
                ShowOptionsFor(null);
            }
            SetContextTab(ctx);
        }
        finally { _rvSyncing = false; }
    }

    private void SetTypeItems(IEnumerable<string> items, string selected)
    {
        var list = items.ToList();
        _rvTypeSel.ItemsSource = list;
        _rvTypeSel.SelectedItem = selected != null && list.Contains(selected) ? selected : null;
        _rvTypeSel.IsEnabled = list.Count > 0;
    }

    private void PropHeader(string s) => _rvProps.Children.Add(new Border
    {
        Background = new SolidColorBrush(Color.FromRgb(232, 236, 242)), Margin = new Thickness(0, 6, 0, 2), Padding = new Thickness(4, 1, 4, 1),
        Child = new TextBlock { Text = s, FontWeight = FontWeights.SemiBold }
    });

    private void PropRow(string label, string value)
    {
        var g = new Grid { Margin = new Thickness(2, 1, 2, 1) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        g.Children.Add(new TextBlock { Text = label, Foreground = Brushes.DimGray, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = label });
        var v = new TextBlock { Text = value ?? "", TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = value };
        Grid.SetColumn(v, 1);
        g.Children.Add(v);
        _rvProps.Children.Add(g);
    }

    private void PropEdit(string label, double value, Action<double> apply)
    {
        var g = new Grid { Margin = new Thickness(2, 1, 2, 1) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        g.Children.Add(new TextBlock { Text = label, Foreground = Brushes.DimGray, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = label, VerticalAlignment = VerticalAlignment.Center });
        var tb = new TextBox { Text = value.ToString("0.###", Inv), ToolTip = "Nhập rồi Enter" };
        void Commit()
        {
            double v = DialogUi.ParseNum(tb.Text, double.NaN);
            if (double.IsNaN(v) || Math.Abs(v - value) < 1e-9) return;
            value = v;
            if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); return; }
            apply(v);
        }
        tb.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; Commit(); } };
        tb.LostFocus += (s, e) => Commit();
        Grid.SetColumn(tb, 1);
        g.Children.Add(tb);
        _rvProps.Children.Add(g);
    }

    private void OpeningTypeProps(OpeningType t)
    {
        PropHeader("Kích thước");
        PropRow("Rộng", $"{t.Width * 1000:0} mm");
        PropRow("Cao", $"{t.Height * 1000:0} mm");
        PropRow("Bậu", $"{t.Sill * 1000:0} mm");
        PropRow("Cách mở", t.Operation);
    }

    /// <summary>Ô xem trước kiểu tường: các lớp theo chiều dày.</summary>
    private void WallPreview(WallType wt)
    {
        _rvTypePreview.Children.Clear();
        if (wt == null) return;
        WallAssembly.ToLayered(wt);
        double total = Math.Max(wt.TotalThickness, 0.05), k = 40 / total, y = 2;
        foreach (var l in wt.Layers)
        {
            double h = Math.Max(1, l.Thickness * k);
            var r = new Shapes.Rectangle
            {
                Width = 50, Height = h, StrokeThickness = 0.4, Stroke = Brushes.Gray,
                Fill = l.IsCore ? new SolidColorBrush(l.Material == WallMaterials.Concrete ? Color.FromRgb(190, 190, 190) : Color.FromRgb(214, 150, 110))
                     : l.Function == WallLayerFn.Stone ? new SolidColorBrush(Color.FromRgb(150, 150, 165))
                     : l.Function == WallLayerFn.Tile ? new SolidColorBrush(Color.FromRgb(170, 210, 230)) : new SolidColorBrush(Color.FromRgb(236, 232, 220))
            };
            Canvas.SetLeft(r, 2); Canvas.SetTop(r, y);
            _rvTypePreview.Children.Add(r);
            y += h;
        }
    }

    private void OpeningPreview(OpeningType t)
    {
        _rvTypePreview.Children.Clear();
        double k = 38 / Math.Max(2.6, Math.Max(t.Width, t.Sill + t.Height));
        var floor = new Shapes.Line { X1 = 2, X2 = 52, Y1 = 42, Y2 = 42, Stroke = Brushes.Gray };
        _rvTypePreview.Children.Add(floor);
        var r = new Shapes.Rectangle { Width = Math.Max(2, t.Width * k), Height = Math.Max(2, t.Height * k), Stroke = RvBlue, Fill = RvBlueFill, StrokeThickness = 1 };
        Canvas.SetLeft(r, 27 - t.Width * k / 2); Canvas.SetTop(r, 42 - (t.Sill + t.Height) * k);
        _rvTypePreview.Children.Add(r);
    }

    /// <summary>Đổi kiểu ở bộ chọn kiểu (Type Selector).</summary>
    private void RvTypeSelected()
    {
        if (_rvSyncing || _p == null || _rvTypeSel.SelectedItem is not string name) return;
        if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); return; }
        if (_rvPlaceType != null)
        {
            var t = _p.OpeningTypes.FirstOrDefault(q => q.Name == name);
            if (t != null) { _rvPlaceType = t; _rvOptKind = null; ShowOptionsFor("Opening"); _rvSig = ""; }
            return;
        }
        if (_rvSelOpen != null && _rvSelWall != null)
        {
            var (w, o) = (_rvSelWall, _rvSelOpen);
            if (o.Type == name) return;
            string snap = Snapshot();
            string old = o.Type;
            o.Type = name;
            var r = WallAssembly.MoveOpening(_p, w, o, o.Offset);
            if (!r.Ok) { o.Type = old; SetStatus(r.Message); return; }
            AfterEdit(snap, new EditResult { Ok = true, Message = $"Đã đổi cửa sang kiểu {name}." }, w);
            SelectHostedOpening(w, o);
            return;
        }
        if (_selWall != null && _p.Walls.Contains(_selWall))
        {
            var w = _selWall;
            if (w.Type == name) return;
            string snap = Snapshot();
            w.Type = name;
            AfterEdit(snap, new EditResult { Ok = true, Message = $"Đã đổi tường #{w.Id} sang kiểu \"{name}\"." }, w);
            return;
        }
        if (_sel != null && _p.Frames.FirstOrDefault(f => f.Id == _sel.Id) is FrameElement fr)
        {
            if (fr.Section == name) return;
            string snap = Snapshot();
            fr.Section = name;
            AfterEdit(snap, new EditResult { Ok = true, Message = $"Đã đổi {fr.Label} sang tiết diện {name}." }, fr);
            return;
        }
        if (_selSlab != null && _p.Slabs.Contains(_selSlab))
        {
            var sl = _selSlab;
            if (sl.Section == name) return;
            string snap = Snapshot();
            sl.Section = name;
            AfterEdit(snap, new EditResult { Ok = true, Message = $"Đã đổi ô sàn {sl.Label} sang {name}." }, sl);
            return;
        }
        if (_mode == PlanEditMode.AddWall || _mode == PlanEditMode.AssignWall) { cbEditWallType.SelectedItem = name; _rvOptKind = null; ShowOptionsFor("Wall"); _rvSig = ""; }
    }

    private void RvEditType()
    {
        if (_rvPlaceType != null || _rvSelOpen != null) { OpenOpeningTypes(); return; }
        if (_selWall != null) { OpenWallTypeEditor(_selWall.Type); return; }
        if (_sel != null || _selSlab != null) { MnuSectionManager_Click(this, new RoutedEventArgs()); return; }
        OpenWallTypeEditor(cbEditWallType.SelectedItem as string);
    }

    // ==================================================================
    // Tab ngữ cảnh "Sửa | …"
    // ==================================================================
    private void SetContextTab(string what)
    {
        if (_rvTabs == null) return;
        if (_rvCtxTab != null)
        {
            bool wasSel = ReferenceEquals(_rvTabs.SelectedItem, _rvCtxTab);
            _rvTabs.Items.Remove(_rvCtxTab);
            _rvCtxTab = null;
            if (wasSel && what == null && _rvTabBeforeCtx != null && _rvTabs.Items.Contains(_rvTabBeforeCtx)) _rvTabs.SelectedItem = _rvTabBeforeCtx;
        }
        if (what == null) return;
        var groups = new List<FrameworkElement>();
        switch (what)
        {
            case "Tường":
                groups.Add(RvGroup("Tường",
                    RvBtn("Sửa kiểu", "", "Sửa cấu tạo kiểu tường đang chọn", () => OpenWallTypeEditor(_selWall?.Type)),
                    RvBtn("Đặt cửa đi", "", "Đặt cửa đi lên tường", () => StartPlaceOpening(WallOpeningKind.Door)),
                    RvBtn("Đặt cửa sổ", "", "Đặt cửa sổ lên tường", () => StartPlaceOpening(WallOpeningKind.Window)),
                    RvBtn("Xoá", "", "Xoá tường (cùng các cửa gắn trên tường) – Delete", DeleteSelection)));
                break;
            case "Cửa":
                groups.Add(RvGroup("Cửa",
                    RvBtn("Lật hướng mở", "", "Lật phía mở cửa (Space)", () => RevitFlip(false)),
                    RvBtn("Đổi bản lề", "", "Đổi bên bản lề (Shift+Space)", () => RevitFlip(true)),
                    RvBtn("Sửa kiểu cửa", "", "Kiểu cửa: rộng, cao, bậu, cách mở", OpenOpeningTypes),
                    RvBtn("Xoá", "", "Xoá cửa – tường liền lại (Delete)", () => RevitPlanKey(Key.Delete))));
                break;
            case "Dầm":
                groups.Add(RvGroup("Sửa dầm",
                    RvBtn("Di chuyển", "", "Dời dầm (MOVE)", () => RvMode("MoveBeam")),
                    RvBtn("Kéo giãn", "", "Kéo đầu dầm (STRETCH)", () => RvMode("StretchBeam")),
                    RvBtn("Cắt", "", "Cắt dầm tới dầm khác (TRIM)", () => RvMode("TrimBeam")),
                    RvBtn("Kéo dài", "", "Kéo dài dầm tới dầm khác (EXTEND)", () => RvMode("ExtendBeam")),
                    RvBtn("Tải dầm", "", "Gán tải cho dầm đang chọn", () => RvShowMemberRibbon(RtDam)),
                    RvBtn("Xoá", "", "Xoá dầm (Delete)", DeleteSelection)));
                break;
            case "Cột":
                groups.Add(RvGroup("Sửa cột",
                    RvBtn("Tải cột", "", "Gán tải đầu cột", () => RvShowMemberRibbon(RtCot)),
                    RvBtn("Xoá", "", "Xoá cột (Delete)", DeleteSelection)));
                break;
            case "Sàn":
                groups.Add(RvGroup("Sửa sàn",
                    RvBtn("Cấu tạo sàn…", "", "Cấu tạo sàn / hoạt tải", () => MnuBuildUps_Click(this, new RoutedEventArgs())),
                    RvBtn("Tải sàn", "", "Gán tải ô sàn", () => RvShowMemberRibbon(RtSan)),
                    RvBtn("Xoá", "", "Xoá ô sàn (Delete)", DeleteSelection)));
                break;
        }
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 2, 2, 0) };
        foreach (var g in groups) sp.Children.Add(g);
        _rvCtxTab = new TabItem { Header = "Sửa | " + what, Content = sp, Background = RvCtxBack, FontWeight = FontWeights.SemiBold, Padding = new Thickness(10, 2, 10, 2) };
        if (!(_rvTabs.SelectedItem is TabItem cur && cur.Header is string hs && hs.StartsWith("Sửa |"))) _rvTabBeforeCtx = _rvTabs.SelectedItem as TabItem;
        _rvTabs.Items.Add(_rvCtxTab);
        _rvTabs.SelectedItem = _rvCtxTab;
    }

    // ==================================================================
    // Hộp thoại
    // ==================================================================
    private void OpenWallTypeEditor(string select)
    {
        if (_p == null || !CanEditModel()) return;
        string snap = Snapshot();
        var dlg = new WallTypeEditorWindow(_p, select) { Owner = this };
        dlg.ShowDialog();
        if (dlg.Changed) AfterDialogEdit(snap, $"Đã cập nhật kiểu tường ({_p.WallTypes.Count} kiểu) – tải tường tính lại theo các lớp.");
        _rvBrowserSig = ""; _rvSig = ""; _rvOptKind = null;
        RefreshEditCombos();
    }

    private void OpenOpeningTypes()
    {
        if (_p == null || !CanEditModel()) return;
        string snap = Snapshot();
        var dlg = new OpeningTypesWindow(_p) { Owner = this };
        dlg.ShowDialog();
        if (dlg.Changed)
        {
            // kích thước kiểu đổi → giữ cửa trong tường, không chồng
            foreach (var w in _p.Walls)
                foreach (var o in (w.Openings ?? new List<WallOpening>()).ToList())
                    if (!WallAssembly.MoveOpening(_p, w, o, o.Offset).Ok) w.Openings.Remove(o);
            AfterDialogEdit(snap, $"Đã cập nhật kiểu cửa ({_p.OpeningTypes.Count} kiểu).");
        }
        _rvBrowserSig = ""; _rvSig = ""; _rvOptKind = null;
    }

    private void OpenMaterials()
    {
        if (_p == null || !CanEditModel()) return;
        WallAssembly.Upgrade(_p);
        string snap = Snapshot();
        var dlg = new MaterialLibraryWindow(_p) { Owner = this };
        dlg.ShowDialog();
        if (dlg.Changed) { WallAssembly.ApplyMaterials(_p); AfterDialogEdit(snap, "Đã cập nhật vật liệu tường – kiểu tường tính lại tải 1 m²."); }
    }

    private void OpenVisibility()
    {
        var dlg = new VisibilityGraphicsWindow(_vg, () => RedrawPlan()) { Owner = this };
        dlg.ShowDialog();
        _rvSig = "";
        SyncQuickVg();
    }

    private void ShowLintelProposals()
    {
        if (_p == null) return;
        var l = WallAssembly.ProposeLintels(_p);
        if (l.Count == 0) { MessageBox.Show(this, "Chưa có cửa nào gắn trên tường (Kiến trúc → Cửa đi / Cửa sổ).", "Gợi ý lanh tô"); return; }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{l.Count} lanh tô đề xuất (gối ≥ 200 mm, ≥ 250 mm khi L0 > 1,5 m; h ≥ max(L0/10; 150 mm)):");
        foreach (var q in l.OrderBy(q => q.Story).ThenBy(q => q.WallId))
            sb.AppendLine($"• Tầng {q.Story}, tường #{q.WallId}, cửa {q.OpeningType}: lanh tô {q.WallThickness * 1000:0}×{q.Depth * 1000:0}, dài {q.Length * 1000:0} mm (gối {q.Bearing * 1000:0})" + (q.Note.Length > 0 ? " – " + q.Note : ""));
        sb.AppendLine();
        sb.AppendLine("Chi tiết thép lanh tô: chế độ Triển khai hồ sơ → Lanh tô.");
        MessageBox.Show(this, sb.ToString(), "Gợi ý lanh tô", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
