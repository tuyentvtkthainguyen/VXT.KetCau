using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using Microsoft.Win32;

namespace KetCauVXT.App;

/// <summary>Chế độ TRIỂN KHAI HỒ SƠ: danh sách cấu kiện bên trái (dầm, cột, sàn, móng đơn / băng / cọc / bè, dự ứng lực, bể nước),
/// bảng nhập thông số ở giữa (tự sinh từ [Field] của Core/Detailing), kết quả kiểm tra – thuyết minh – xem trước bản vẽ – khối lượng bên phải.
/// Mọi thay đổi tính lại ngay cấu kiện đang chọn. Xuất bản vẽ DXF (gộp / tách), Excel khối lượng, Word thuyết minh.</summary>
public sealed class DetailingWindow : Window
{
    public const string HelpId = "che-do-ho-so.giao-dien";
    private DetailingProject _prj = new();
    private string _path;
    private bool _dirty, _loading;
    private readonly Dictionary<IDetailMember, DetailResult> _res = new();

    private readonly TreeView _tree = new() { BorderThickness = new Thickness(0) };
    private readonly StackPanel _form = new() { Margin = new Thickness(10, 6, 10, 10) };
    private readonly TextBlock _status = new() { Margin = new Thickness(6, 2, 6, 2) };
    private readonly TextBlock _resHead = new() { FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap };
    private readonly DataGrid _checks = DialogUi.NewGrid();
    private readonly ListBox _msgs = new() { MaxHeight = 220, BorderThickness = new Thickness(0) };
    private readonly TextBox _calc = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), BorderThickness = new Thickness(0) };
    private readonly DrawingHost _preview = new() { Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly ScrollViewer _previewScroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.WhiteSmoke };
    private readonly DataGrid _qty = DialogUi.NewGrid();
    private readonly TabControl _tabs = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private double _zoom = 1;
    private IDetailMember _cur;

    public static string SelfTestOut { get; set; }
    private DateTime _lastPick = DateTime.Now;
    private readonly DispatcherTimer _pickTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    public DetailingWindow(string file = null)
    {
        Title = "Việt Const – Triển khai hồ sơ";
        Width = 1440; Height = 880; MinWidth = 980; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        try { Icon = BitmapFrame.Create(new Uri("pack://application:,,,/KetCauVXT.ico")); } catch { }
        Help.SetId(this, HelpId);
        if (Help.Opener == null || Application.Current?.Windows.OfType<MainWindow>().Any() != true)
            Help.Opener = id => OpenGuide(id);
        _timer.Tick += (s, e) => { _timer.Stop(); Recalc(); };
        BuildUi();
        if (!string.IsNullOrEmpty(file) && File.Exists(file)) OpenFile(file);
        else NewProject(true);
        Closing += (s, e) => { if (!ConfirmDiscard()) e.Cancel = true; else _pickTimer.Stop(); };
        // nhận kích thước AutoCAD tự động: lệnh VXT_KT ghi file JSON vào %AppData%\KetCauVXT\AcadPick
        _pickTimer.Tick += (s, e) =>
        {
            if (SelfTestOut != null || !IsActive && !IsVisible) return;
            AcadPickData d = null;
            try { d = AcadPick.Latest(_lastPick); } catch { }
            if (d == null) return;
            _lastPick = d.Time;
            _pickTimer.Stop();
            try { ApplyPick(d); } finally { _pickTimer.Start(); }
        };
        _pickTimer.Start();
        PreviewKeyDown += OnKey;
    }

    // ==================================================================
    // Giao diện
    // ==================================================================
    private void BuildUi()
    {
        var dock = new DockPanel();
        var menu = new Menu { Padding = new Thickness(2) };
        MenuItem M(string header, params object[] items)
        {
            var mi = new MenuItem { Header = header };
            foreach (var it in items) mi.Items.Add(it);
            return mi;
        }
        MenuItem I(string header, Action a, string gesture = null)
        {
            var mi = new MenuItem { Header = header, InputGestureText = gesture ?? "" };
            mi.Click += (s, e) => a();
            return mi;
        }
        var add = new MenuItem { Header = "_Thêm cấu kiện" };
        foreach (var (key, title, _) in _prj.Groups) { string k = key; add.Items.Add(I(title, () => AddMember(k))); }
        menu.Items.Add(M("_Tệp",
            I("_Mới", () => NewProject(false), "Ctrl+N"), I("_Mở hồ sơ .kcvxth…", OpenDialog, "Ctrl+O"), I("_Lưu", () => Save(false), "Ctrl+S"), I("Lưu _thành…", () => Save(true)),
            I("Mở hồ sơ _mẫu", OpenSample), new Separator(),
            I("Chuyển sang chế độ Thiết kế – _chạy nội lực (mô hình như ETABS)", SwitchToModel), I("Chọn chế độ làm việc khi khởi động…", ChooseStartMode),
            new Separator(), I("_Thoát", Close)));
        menu.Items.Add(M("_Cấu kiện", add, I("_Nhân bản cấu kiện đang chọn", Duplicate, "Ctrl+D"), I("_Xoá cấu kiện đang chọn", DeleteCurrent, "Delete"), new Separator(),
            I("Lấy kích thước từ _AutoCAD (lệnh VXT_KT)…", PickFromAcad, "Ctrl+K")));
        menu.Items.Add(M("Tính _toán", I("Tính cấu kiện đang chọn", Recalc, "F9"), I("Tính _toàn bộ hồ sơ", RecalcAll, "F5")));
        menu.Items.Add(M("_Xuất hồ sơ",
            I("Bản vẽ DXF – _gộp 1 file…", () => ExportDrawings(false)), I("Bản vẽ DXF – _tách từng cấu kiện…", () => ExportDrawings(true)),
            I("Bảng _khối lượng Excel…", ExportExcel), I("_Thuyết minh tính toán Word…", ExportWord), new Separator(),
            I("Xuất _tất cả vào thư mục…", ExportAll)));
        menu.Items.Add(M("Tuỳ _chọn", I("_Vật liệu, lớp bảo vệ…", EditSettings), I("Thông tin _công trình…", EditInfo)));
        menu.Items.Add(M("Trợ _giúp", I("Hướng dẫn chế độ Triển khai hồ sơ", () => OpenGuide(HelpId), "F1")));
        DockPanel.SetDock(menu, Dock.Top);
        dock.Children.Add(menu);

        var bar = new ToolBar();
        void B(string text, Action a, string tip) { var b = new Button { Content = text, ToolTip = tip, Padding = new Thickness(8, 2, 8, 2) }; b.Click += (s, e) => a(); bar.Items.Add(b); }
        B("Mới", () => NewProject(false), "Hồ sơ mới (Ctrl+N)"); B("Mở…", OpenDialog, "Mở hồ sơ .kcvxth (Ctrl+O)"); B("Lưu", () => Save(false), "Lưu (Ctrl+S)");
        bar.Items.Add(new Separator());
        foreach (var (key, title, _) in _prj.Groups)
        {
            string k = key;
            string shortName = key switch { "MongCoc" => "Móng cọc", "DuUngLuc" => "DƯL", "BeNuoc" => "Bể nước", "San" => "Ô sàn", "LanhTo" => "Lanh tô", _ => title };
            B("＋ " + shortName, () => AddMember(k), "Thêm " + title.ToLowerInvariant());
        }
        bar.Items.Add(new Separator());
        B("⇩ Từ AutoCAD", PickFromAcad, "Lấy kích thước từ AutoCAD: trong AutoCAD gõ VXT_KT, chọn đối tượng (dầm, cột, sàn, móng, móng băng)");
        B("Tính toàn bộ (F5)", RecalcAll, "Tính lại mọi cấu kiện");
        B("Xuất tất cả…", ExportAll, "Bản vẽ DXF (gộp + tách), Excel khối lượng, Word thuyết minh vào một thư mục");
        bar.Items.Add(new Separator());
        B("⇄ Thiết kế – chạy nội lực", SwitchToModel, "Mở giao diện mô hình tính như ETABS");
        DockPanel.SetDock(bar, Dock.Top);
        dock.Children.Add(bar);

        var sb = new System.Windows.Controls.Primitives.StatusBar(); sb.Items.Add(_status);
        DockPanel.SetDock(sb, Dock.Bottom);
        dock.Children.Add(sb);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210), MinWidth = 150 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(430), MinWidth = 320 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 300 });
        var left = new DockPanel();
        var lh = new TextBlock { Text = "CẤU KIỆN HỒ SƠ", FontWeight = FontWeights.Bold, Margin = new Thickness(8, 6, 8, 4) };
        DockPanel.SetDock(lh, Dock.Top);
        left.Children.Add(lh);
        var lb = new WrapPanel { Margin = new Thickness(6, 2, 6, 6) };
        Button Small(string t, Action a) { var b = new Button { Content = t, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(6, 1, 6, 1) }; b.Click += (s, e) => a(); return b; }
        lb.Children.Add(Small("Nhân bản", Duplicate)); lb.Children.Add(Small("Xoá", DeleteCurrent)); lb.Children.Add(Small("▲", () => Move(-1))); lb.Children.Add(Small("▼", () => Move(1)));
        DockPanel.SetDock(lb, Dock.Bottom);
        left.Children.Add(lb);
        left.Children.Add(_tree);
        _tree.SelectedItemChanged += (s, e) => { if (_tree.SelectedItem is TreeViewItem ti && ti.Tag is IDetailMember m) ShowMember(m); };
        grid.Children.Add(left);
        var sp1 = new GridSplitter { Width = 4, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Gainsboro };
        Grid.SetColumn(sp1, 1); grid.Children.Add(sp1);
        var formScroll = new ScrollViewer { Content = _form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetColumn(formScroll, 2); grid.Children.Add(formScroll);
        var sp2 = new GridSplitter { Width = 4, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Gainsboro };
        Grid.SetColumn(sp2, 3); grid.Children.Add(sp2);

        // ---- kết quả ----
        _checks.IsReadOnly = true;
        _checks.Columns.Add(DialogUi.Text("Kiểm tra", "Name", true, 230));
        _checks.Columns.Add(DialogUi.Num("Yêu cầu", "Demand", "0.0##", true, 80));
        _checks.Columns.Add(DialogUi.Num("Khả năng", "Capacity", "0.0##", true, 80));
        _checks.Columns.Add(DialogUi.Text("Đơn vị", "Unit", true, 60));
        _checks.Columns.Add(DialogUi.Num("Tỷ số", "Ratio", "0.00", true, 55));
        _checks.Columns.Insert(1, DialogUi.Text("Đạt", "OkText", true, 50));
        _checks.LoadingRow += (s, e) => { if (e.Row.Item is CheckRow r) e.Row.Background = r.Ok ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xFF, 0xE3, 0xE0)); };
        var resPanel = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(_resHead, Dock.Top); resPanel.Children.Add(_resHead);
        var mh = new Expander { Header = "Thông báo, cảnh báo", IsExpanded = true, Content = _msgs, Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(mh, Dock.Bottom); resPanel.Children.Add(mh);
        resPanel.Children.Add(_checks);
        _tabs.Items.Add(new TabItem { Header = "Kết quả kiểm tra", Content = resPanel });
        _tabs.Items.Add(new TabItem { Header = "Thuyết minh", Content = _calc });
        var pv = new DockPanel();
        var zb = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
        zb.Children.Add(Small("Thu nhỏ −", () => { _zoom /= 1.25; RefreshPreview(); }));
        zb.Children.Add(Small("Phóng to +", () => { _zoom *= 1.25; RefreshPreview(); }));
        zb.Children.Add(Small("Vừa khung", () => { _zoom = 1; RefreshPreview(); }));
        zb.Children.Add(Small("Xuất DXF cấu kiện này…", ExportCurrentDxf));
        DockPanel.SetDock(zb, Dock.Top); pv.Children.Add(zb);
        _previewScroll.Content = _preview; pv.Children.Add(_previewScroll);
        _tabs.Items.Add(new TabItem { Header = "Bản vẽ (xem trước)", Content = pv });
        _qty.IsReadOnly = true;
        _qty.Columns.Add(DialogUi.Text("Nhóm", "Group", true, 110));
        _qty.Columns.Add(DialogUi.Text("Công tác", "Item", true, 300));
        _qty.Columns.Add(DialogUi.Text("Đơn vị", "Unit", true, 55));
        _qty.Columns.Add(DialogUi.Num("Khối lượng", "Qty", "0.###", true, 90));
        _tabs.Items.Add(new TabItem { Header = "Khối lượng hồ sơ", Content = _qty });
        _tabs.SelectionChanged += (s, e) => { if (e.Source == _tabs) { if (_tabs.SelectedIndex == 2) RefreshPreview(); if (_tabs.SelectedIndex == 3) RefreshQuantities(); } };
        Grid.SetColumn(_tabs, 4); grid.Children.Add(_tabs);
        dock.Children.Add(grid);
        Content = dock;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        bool typing = Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is DataGridCell;
        if (ctrl && e.Key == Key.S) { Save(false); e.Handled = true; }
        else if (ctrl && e.Key == Key.O) { OpenDialog(); e.Handled = true; }
        else if (ctrl && e.Key == Key.N) { NewProject(false); e.Handled = true; }
        else if (ctrl && e.Key == Key.D) { Duplicate(); e.Handled = true; }
        else if (ctrl && e.Key == Key.K) { PickFromAcad(); e.Handled = true; }
        else if (e.Key == Key.F5) { RecalcAll(); e.Handled = true; }
        else if (e.Key == Key.F9) { Recalc(); e.Handled = true; }
        else if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None) { OpenGuide(HelpId); e.Handled = true; }
        else if (e.Key == Key.Delete && !typing && _tree.IsKeyboardFocusWithin) { DeleteCurrent(); e.Handled = true; }
    }

    private void SetStatus(string s) => _status.Text = s;

    private UserGuideWindow _guide;
    private void OpenGuide(string id)
    {
        try
        {
            var main = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
            if (main != null) { main.OpenUserGuide(id); return; }
            if (_guide == null || !_guide.IsLoaded) { _guide = new UserGuideWindow(); _guide.Closed += (s, e) => _guide = null; _guide.Show(); }
            _guide.ShowHelp(id ?? HelpId);
            _guide.Activate();
        }
        catch (Exception ex) { MessageBox.Show(this, "Không mở được hướng dẫn: " + ex.Message, Title); }
    }

    // ==================================================================
    // Cây cấu kiện
    // ==================================================================
    private void RefreshTree(IDetailMember select = null)
    {
        _tree.Items.Clear();
        TreeViewItem pick = null;
        foreach (var (key, title, list) in _prj.Groups)
        {
            var g = new TreeViewItem { Header = title + " (" + list.Count + ")", IsExpanded = list.Count > 0, FontWeight = FontWeights.SemiBold, Tag = key };
            foreach (IDetailMember m in list)
            {
                _res.TryGetValue(m, out var r);
                string mark = r == null ? "" : r.Ok ? "  ✓" : "  ✗";
                var ti = new TreeViewItem { Header = m.Name + (m.Quantity > 1 ? " (SL=" + m.Quantity + ")" : "") + mark, Tag = m, FontWeight = FontWeights.Normal, Foreground = r != null && !r.Ok ? Brushes.Firebrick : Brushes.Black };
                g.Items.Add(ti);
                if (ReferenceEquals(m, select)) pick = ti;
            }
            _tree.Items.Add(g);
        }
        if (pick != null) { pick.IsSelected = true; pick.BringIntoView(); }
        else if (select == null) ShowMember(null);
    }

    private (string Key, IList List)? GroupOf(IDetailMember m)
    {
        foreach (var (key, _, list) in _prj.Groups) if (list.Contains(m)) return (key, list);
        return null;
    }

    private void AddMember(string key)
    {
        var m = _prj.AddNew(key);
        _dirty = true;
        _res[m] = DetailingProject.RunSafe(m, _prj.Settings);
        RefreshTree(m);
        SetStatus("Đã thêm " + m.Name + " – nhập thông số ở bảng giữa; kết quả cập nhật ngay.");
    }

    private void Duplicate()
    {
        if (_cur == null) return;
        var g = GroupOf(_cur);
        if (g == null) return;
        var json = System.Text.Json.JsonSerializer.Serialize(_cur, _cur.GetType());
        var copy = (IDetailMember)System.Text.Json.JsonSerializer.Deserialize(json, _cur.GetType());
        string prefix = new string(copy.Name.TakeWhile(ch => !char.IsDigit(ch)).ToArray());
        var names = new HashSet<string>(_prj.All.Select(x => x.Name));
        int k = 1; while (names.Contains(prefix + k)) k++;
        copy.Name = prefix + k;
        g.Value.List.Insert(g.Value.List.IndexOf(_cur) + 1, copy);
        _dirty = true;
        _res[copy] = DetailingProject.RunSafe(copy, _prj.Settings);
        RefreshTree(copy);
    }

    private void DeleteCurrent()
    {
        if (_cur == null) return;
        if (SelfTestOut == null && MessageBox.Show(this, "Xoá cấu kiện " + _cur.Name + "?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var g = GroupOf(_cur);
        if (g == null) return;
        int i = g.Value.List.IndexOf(_cur);
        g.Value.List.Remove(_cur); _res.Remove(_cur);
        _dirty = true;
        var next = g.Value.List.Count > 0 ? (IDetailMember)g.Value.List[Math.Min(i, g.Value.List.Count - 1)] : null;
        _cur = null;
        RefreshTree(next);
    }

    private void Move(int d)
    {
        if (_cur == null) return;
        var g = GroupOf(_cur);
        if (g == null) return;
        int i = g.Value.List.IndexOf(_cur), j = i + d;
        if (j < 0 || j >= g.Value.List.Count) return;
        g.Value.List.RemoveAt(i); g.Value.List.Insert(j, _cur);
        _dirty = true;
        RefreshTree(_cur);
    }

    // ==================================================================
    // Lấy kích thước từ AutoCAD (add-in: lệnh VXT_KT → Detailing/AcadPick.cs)
    // ==================================================================
    private void PickFromAcad()
    {
        AcadPickData d = null;
        try { d = AcadPick.Latest(); } catch (Exception ex) { SetStatus("Không đọc được dữ liệu AutoCAD: " + ex.Message); }
        if (d == null)
        {
            MessageBox.Show(this, "Chưa có dữ liệu từ AutoCAD.\n\nTrong AutoCAD (đã nạp add-in Việt Const) gõ lệnh VXT_KT → chọn loại (Dầm / Cột / Sàn / Móng / Móng băng) → chọn đối tượng.\n"
                            + "Việt Const nhận kích thước tự động trong vài giây, hoặc bấm lại nút này.\n\nThư mục trao đổi: " + AcadPick.Folder, Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _lastPick = d.Time;
        ApplyPick(d);
    }

    private static bool Compatible(IDetailMember m, string kind) => (kind ?? "").ToLowerInvariant() switch
    {
        "dam" => m is ManualBeam, "cot" => m is ManualColumn, "san" => m is ManualSlab || m is ManualSlabPlan,
        "mong" => m is ManualFooting || m is ManualPileCap, "mongbang" => m is ManualStrip, _ => false
    };

    private void ApplyPick(AcadPickData d)
    {
        if (d.Items.Count == 0)
        {
            MessageBox.Show(this, "Dữ liệu AutoCAD không có đối tượng hợp lệ" + (d.Errors.Count > 0 ? ":\n" + string.Join("\n", d.Errors.Take(6)) : "."), Title);
            return;
        }
        Activate();
        bool canApply = _cur != null && Compatible(_cur, d.Kind);
        string head = "Nhận từ AutoCAD: " + d.KindTitle + " – " + d.Items.Count + " đối tượng (" + Path.GetFileName(d.File) + ").";
        var ans = MessageBox.Show(this, head + (canApply
                ? "\n\nCó: áp kích thước vào cấu kiện đang chọn " + _cur.Name + " (giữ thép, nội lực đã nhập)\nKhông: tạo " + d.Items.Count + " cấu kiện mới\nHuỷ: bỏ qua"
                : "\n\nTạo " + d.Items.Count + " cấu kiện mới?"), Title,
            canApply ? MessageBoxButton.YesNoCancel : MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (canApply && ans == MessageBoxResult.Yes)
        {
            var log = AcadPick.ApplyTo(_cur, d.Items[0]);
            _dirty = true; UpdateTitle();
            var m = _cur;
            Recalc(); ShowMember(m); RefreshTree(m);
            SetStatus("Đã áp kích thước AutoCAD vào " + m.Name + ": " + string.Join("; ", log));
            return;
        }
        if (!(ans == MessageBoxResult.No || (!canApply && ans == MessageBoxResult.OK))) return;
        IDetailMember first = null;
        var names = new HashSet<string>(_prj.All.Select(x => x.Name));
        foreach (var m in AcadPick.CreateMembers(d))
        {
            var g = _prj.Groups.FirstOrDefault(x => x.List.GetType().IsGenericType && x.List.GetType().GetGenericArguments()[0] == m.GetType());
            if (g.List == null) continue;
            if (string.IsNullOrWhiteSpace(m.Name) || names.Contains(m.Name))
            {
                string prefix = new string((m.Name ?? "").TakeWhile(ch => !char.IsDigit(ch)).ToArray());
                if (prefix.Length == 0) prefix = "CK";
                int k = 1; while (names.Contains(prefix + k)) k++;
                m.Name = prefix + k;
            }
            names.Add(m.Name);
            g.List.Add(m);
            _res[m] = DetailingProject.RunSafe(m, _prj.Settings);
            first ??= m;
        }
        _dirty = true; UpdateTitle();
        RefreshTree(first);
        SetStatus(first == null ? "Không tạo được cấu kiện từ dữ liệu AutoCAD." : "Đã tạo cấu kiện từ AutoCAD – bổ sung thép / nội lực rồi kiểm tra kết quả.");
    }

    // ==================================================================
    // Bảng nhập tự sinh từ [Field]
    // ==================================================================
    private void ShowMember(IDetailMember m)
    {
        _cur = m;
        _form.Children.Clear();
        if (m == null)
        {
            _form.Children.Add(new TextBlock
            {
                Text = "Chọn một cấu kiện ở danh sách bên trái, hoặc bấm ＋ trên thanh công cụ để thêm dầm, cột, sàn, móng, dự ứng lực, bể nước.\n\n"
                       + "Cách làm: nhập kích thước → chọn số thanh / Ø / khoảng cách thép (0 = chương trình tự chọn) → nhập nội lực (nếu có) để kiểm tra → xem kết quả, bản vẽ → Xuất hồ sơ.",
                TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 10, 0, 0)
            });
            ShowResult(null);
            return;
        }
        _loading = true;
        var type = m.GetType();
        var head = new TextBlock { Text = KindTitle(m) + " " + m.Name, FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) };
        _form.Children.Add(head);
        string lastGroup = null;
        foreach (var pi in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite))
        {
            var fa = pi.GetCustomAttribute<FieldAttribute>();
            if (fa != null)
            {
                if (fa.Group != lastGroup && !string.IsNullOrEmpty(fa.Group))
                {
                    _form.Children.Add(new TextBlock { Text = fa.Group.ToUpperInvariant(), FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x5F, 0xA8)), Margin = new Thickness(0, 10, 0, 3) });
                    lastGroup = fa.Group;
                }
                var editor = Editor(m, pi, fa);
                if (editor == null) continue;
                var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
                var lab = new TextBlock { Text = fa.Label + (string.IsNullOrEmpty(fa.Unit) ? "" : " (" + fa.Unit + ")"), Width = 215, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, ToolTip = string.IsNullOrEmpty(fa.Hint) ? null : fa.Hint };
                DockPanel.SetDock(lab, Dock.Left);
                row.Children.Add(lab);
                row.Children.Add(editor);
                _form.Children.Add(row);
                if (!string.IsNullOrEmpty(fa.Hint)) _form.Children.Add(new TextBlock { Text = fa.Hint, Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(215, 0, 0, 2), TextWrapping = TextWrapping.Wrap });
            }
            else if (typeof(IList).IsAssignableFrom(pi.PropertyType) && pi.PropertyType.IsGenericType)
            {
                var et = pi.PropertyType.GetGenericArguments()[0];
                if (!et.GetProperties().Any(p => p.GetCustomAttribute<FieldAttribute>() != null)) continue;
                _form.Children.Add(new TextBlock { Text = ListTitle(et), FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x5F, 0xA8)), Margin = new Thickness(0, 12, 0, 3) });
                _form.Children.Add(ListEditor((IList)pi.GetValue(m), et));
            }
        }
        _loading = false;
        if (!_res.ContainsKey(m)) _res[m] = DetailingProject.RunSafe(m, _prj.Settings);
        ShowResult(_res[m]);
    }

    private static string ListTitle(Type et) => et.Name switch
    {
        "RaftColumn" => "CỘT TRÊN MÓNG BÈ (toạ độ từ góc móng)",
        "StripColumn" => "CỘT TRÊN MÓNG BĂNG (vị trí từ đầu băng)",
        "SlabPanelRow" => "Ô SÀN KHÁC MẶC ĐỊNH (hạ cốt, chiều dày, công năng, lỗ thông tầng)",
        "SlabOpening" => "LỖ KHOÉT SÀN (toạ độ góc dưới trái từ giao trục đầu tiên)",
        "SlabBarRow" => "THÉP SÀN VẼ THỦ CÔNG (chỉ dùng khi Cách bố trí = Thủ công)",
        _ => "DANH SÁCH"
    };

    private static string KindTitle(IDetailMember m) => m switch
    {
        ManualBeam => "Dầm", ManualColumn => "Cột", ManualSlab => "Ô sàn", ManualSlabPlan => "Mặt bằng sàn", ManualLintel => "Lanh tô – ô văng", ManualFooting => "Móng đơn", ManualStrip => "Móng băng",
        ManualPileCap => "Đài cọc", RaftInput => "Móng bè", PrestressInput => "Cấu kiện dự ứng lực", WaterTankInput => "Bể nước", _ => "Cấu kiện"
    };

    private FrameworkElement Editor(object target, PropertyInfo pi, FieldAttribute fa)
    {
        var t = pi.PropertyType;
        if (t == typeof(bool))
        {
            var cb = new CheckBox { IsChecked = (bool)pi.GetValue(target), VerticalAlignment = VerticalAlignment.Center };
            cb.Click += (s, e) => { pi.SetValue(target, cb.IsChecked == true); Changed(); };
            return cb;
        }
        if (t.IsEnum)
        {
            var cbx = new ComboBox { ItemsSource = Enum.GetValues(t), SelectedItem = pi.GetValue(target) };
            cbx.SelectionChanged += (s, e) => { if (!_loading && cbx.SelectedItem != null) { pi.SetValue(target, cbx.SelectedItem); Changed(); } };
            return cbx;
        }
        if (t == typeof(string) && !string.IsNullOrEmpty(fa.Choices))
        {
            var cbx = new ComboBox { IsEditable = true, ItemsSource = fa.Choices.Split('|'), Text = pi.GetValue(target) as string ?? "" };
            cbx.AddHandler(TextBoxBase_TextChanged, new TextChangedEventHandler((s, e) => { if (!_loading) { pi.SetValue(target, cbx.Text); Changed(); } }));
            cbx.SelectionChanged += (s, e) => { if (!_loading && cbx.SelectedItem is string v) { pi.SetValue(target, v); Changed(); } };
            return cbx;
        }
        if (t != typeof(string) && t != typeof(double) && t != typeof(int)) return null;
        var tb = new TextBox { Text = Format(pi.GetValue(target), fa.Scale), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(2, 1, 2, 1) };
        tb.TextChanged += (s, e) =>
        {
            if (_loading) return;
            if (TryParse(tb.Text, t, fa.Scale, out object v)) { pi.SetValue(target, v); tb.ClearValue(Control.BackgroundProperty); Changed(); }
            else tb.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xE3, 0xE0));
        };
        tb.GotKeyboardFocus += (s, e) => tb.SelectAll();
        return tb;
    }

    private static readonly RoutedEvent TextBoxBase_TextChanged = System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent;

    private static string Format(object v, double scale) => v switch
    {
        double d => (d * scale).ToString("0.####", CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        _ => v?.ToString() ?? ""
    };

    private static bool TryParse(string text, Type t, double scale, out object v)
    {
        v = null;
        if (t == typeof(string)) { v = text ?? ""; return true; }
        var s = (text ?? "").Trim().Replace(',', '.').Replace('−', '-');
        if (s.Length == 0) s = "0";
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) || !double.IsFinite(d)) return false;
        if (t == typeof(int)) { v = (int)Math.Round(d); return true; }
        v = scale != 0 ? d / scale : d;
        return true;
    }

    /// <summary>Bảng con (cột móng bè, cột móng băng): cột theo [Field] của kiểu dòng, cho thêm / xoá dòng.</summary>
    private FrameworkElement ListEditor(IList list, Type et)
    {
        var g = DialogUi.NewGrid();
        g.MaxHeight = 320; g.CanUserAddRows = false; g.CanUserDeleteRows = false; g.IsReadOnly = false;
        foreach (var p in et.GetProperties().Where(p => p.CanWrite))
        {
            var fa = p.GetCustomAttribute<FieldAttribute>();
            if (fa == null) continue;
            string header = fa.Label + (string.IsNullOrEmpty(fa.Unit) ? "" : " (" + fa.Unit + ")");
            if (p.PropertyType == typeof(bool))
                g.Columns.Add(new DataGridCheckBoxColumn { Header = header, Binding = new Binding(p.Name) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged } });
            else
                g.Columns.Add(new DataGridTextColumn
                {
                    Header = header, Width = new DataGridLength(1, DataGridLengthUnitType.Auto), MinWidth = 60,
                    Binding = new Binding(p.Name) { Converter = new ScaleConverter(fa.Scale), ConverterCulture = CultureInfo.InvariantCulture, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }
                });
        }
        var view = new System.Collections.ObjectModel.ObservableCollection<object>(list.Cast<object>());
        g.ItemsSource = view;
        g.CellEditEnding += (s, e) => Dispatcher.BeginInvoke(new Action(() => { Sync(); }), DispatcherPriority.Background);
        void Sync()
        {
            list.Clear();
            foreach (var o in view) list.Add(o);
            Changed();
        }
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
        var add = new Button { Content = "＋ Thêm dòng", Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 4, 0) };
        add.Click += (s, e) =>
        {
            var o = Activator.CreateInstance(et);
            if (view.Count > 0)   // chép dòng cuối, dời vị trí
            {
                var last = view[^1];
                foreach (var p in et.GetProperties().Where(p => p.CanWrite && p.CanRead)) p.SetValue(o, p.GetValue(last));
                var px = et.GetProperty("X"); if (px != null && px.PropertyType == typeof(double)) px.SetValue(o, (double)px.GetValue(last) + 4.0);
            }
            view.Add(o); Sync();
        };
        var del = new Button { Content = "Xoá dòng đang chọn", Padding = new Thickness(6, 1, 6, 1) };
        del.Click += (s, e) => { foreach (var o in g.SelectedItems.Cast<object>().ToList()) view.Remove(o); Sync(); };
        bar.Children.Add(add); bar.Children.Add(del);
        var sp = new StackPanel();
        sp.Children.Add(g); sp.Children.Add(bar);
        return sp;
    }

    private sealed class ScaleConverter : IValueConverter
    {
        private readonly double _k;
        public ScaleConverter(double k) { _k = k == 0 ? 1 : k; }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
        {
            double d => (d * _k).ToString("0.####", CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            _ => value?.ToString() ?? ""
        };
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = (value as string ?? "").Trim().Replace(',', '.');
            if (targetType == typeof(string)) return value as string ?? "";
            if (!double.TryParse(s.Length == 0 ? "0" : s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return DependencyProperty.UnsetValue;
            if (targetType == typeof(int)) return (int)Math.Round(d);
            return d / _k;
        }
    }

    private void Changed()
    {
        if (_loading) return;
        _dirty = true;
        UpdateTitle();
        _timer.Stop(); _timer.Start();
    }

    // ==================================================================
    // Tính toán, kết quả
    // ==================================================================
    private void Recalc()
    {
        if (_cur == null) return;
        var r = DetailingProject.RunSafe(_cur, _prj.Settings);
        _res[_cur] = r;
        ShowResult(r);
        UpdateTreeLabel(_cur, r);
    }

    private void RecalcAll()
    {
        _res.Clear();
        foreach (var (m, r) in _prj.RunAll()) _res[m] = r;
        RefreshTree(_cur);
        if (_cur != null && _res.TryGetValue(_cur, out var rc)) ShowResult(rc);
        int bad = _res.Values.Count(r => !r.Ok);
        SetStatus("Đã tính " + _res.Count + " cấu kiện – " + (bad == 0 ? "tất cả đạt." : bad + " cấu kiện KHÔNG ĐẠT (✗ đỏ trong danh sách)."));
        RefreshQuantities();
    }

    private void UpdateTreeLabel(IDetailMember m, DetailResult r)
    {
        foreach (TreeViewItem g in _tree.Items)
            foreach (TreeViewItem ti in g.Items)
                if (ReferenceEquals(ti.Tag, m))
                {
                    ti.Header = m.Name + (m.Quantity > 1 ? " (SL=" + m.Quantity + ")" : "") + (r.Ok ? "  ✓" : "  ✗");
                    ti.Foreground = r.Ok ? Brushes.Black : Brushes.Firebrick;
                }
    }

    public sealed class CheckRow
    {
        public string Name { get; set; } = "";
        public double Demand { get; set; }
        public double Capacity { get; set; }
        public string Unit { get; set; } = "";
        public double Ratio { get; set; }
        public bool Ok { get; set; }
        public string OkText => Ok ? "Đạt" : "KHÔNG";
    }

    private void ShowResult(DetailResult r)
    {
        if (r == null) { _resHead.Text = ""; _checks.ItemsSource = null; _msgs.ItemsSource = null; _calc.Text = ""; _preview.Drawing = null; _preview.InvalidateVisual(); return; }
        double kg = r.SteelWeight;
        _resHead.Text = (r.Ok ? "✓ ĐẠT" : "✗ KHÔNG ĐẠT") + " – " + r.Kind + " " + r.Name + (r.Quantity > 1 ? " (SL=" + r.Quantity + ")" : "")
                        + "\nBê tông " + r.ConcreteClass + ": " + Dt.F(r.ConcreteVolume, "0.000") + " m³ · ván khuôn " + Dt.F(r.FormworkArea, "0.00") + " m² · thép " + Dt.F(kg, "0.0") + " kg"
                        + (r.ConcreteVolume > 0 ? " (" + Dt.F(kg / r.ConcreteVolume, "0") + " kg/m³)" : "") + (r.StrandWeight > 0 ? " · cáp DƯL " + Dt.F(r.StrandWeight, "0.0") + " kg" : "") + " – cho 1 cấu kiện";
        _resHead.Foreground = r.Ok ? new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)) : Brushes.Firebrick;
        _checks.ItemsSource = r.Checks.Select(c => new CheckRow { Name = c.Name, Demand = c.Demand, Capacity = c.Capacity, Unit = c.Unit, Ratio = c.Ratio, Ok = c.Ok }).ToList();
        _msgs.ItemsSource = r.Messages.Select(mm => new TextBlock { Text = mm, TextWrapping = TextWrapping.Wrap, MaxWidth = 640, Foreground = mm.StartsWith("LỖI") ? Brushes.Firebrick : Brushes.Black }).ToList();
        var sb = new System.Text.StringBuilder();
        foreach (var l in r.Calc) sb.AppendLine(l.StartsWith("## ") ? "\n" + l[3..].ToUpperInvariant() : "  " + l);
        if (r.Bars.Count > 0)
        {
            sb.AppendLine("\nTHỐNG KÊ THÉP 1 CẤU KIỆN");
            foreach (var b in r.Bars.OrderBy(b => b.Mark))
                sb.AppendLine($"  ({b.Mark}) {b.Role}: {b.Count} thanh Ø{b.Dia}, dài {Dt.Mm(b.Length)} mm, {b.Shape} – {Dt.F(b.WeightPerBar * b.Count, "0.0")} kg");
        }
        _calc.Text = sb.ToString();
        if (_tabs.SelectedIndex == 2) RefreshPreview();
        SetStatus(r.Ok ? "Cấu kiện " + r.Name + " đạt mọi kiểm tra." : "Cấu kiện " + r.Name + ": " + r.Messages.Count(x => x.StartsWith("LỖI")) + " lỗi – xem bảng kiểm tra / thông báo.");
    }

    private List<Figure> CurrentFigures()
    {
        if (_cur == null || !_res.TryGetValue(_cur, out var r)) return new List<Figure>();
        try { return _cur.Draw(r, _prj.Settings) ?? new List<Figure>(); }
        catch (Exception ex) { SetStatus("Không vẽ được: " + ex.Message); return new List<Figure>(); }
    }

    private void RefreshPreview()
    {
        try
        {
            var figs = CurrentFigures();
            if (figs.Count == 0) { _preview.Drawing = null; _preview.InvalidateVisual(); return; }
            // các hình xếp hàng ngang theo tỷ lệ riêng (mm giấy), xuống dòng khi rộng > 420 mm; căn đỉnh
            var items = new List<(DxfWriter W, double K, double Dx, double Dy)>();
            double cx = 0, rowTop = 0, rowH = 0, maxW = 0, gap = 12;
            foreach (var f in figs)
            {
                var e = f.W.Extents;
                if (e.W <= 0 || e.H <= 0) continue;
                double k = 1.0 / Math.Max(f.W.Scale, 1);
                double fw = e.W * k, fh = e.H * k;
                if (cx > 0 && cx + fw > 420) { rowTop -= rowH + gap; cx = 0; rowH = 0; }
                items.Add((f.W, k, cx - e.X1 * k, rowTop - e.Y2 * k));
                cx += fw + gap; rowH = Math.Max(rowH, fh); maxW = Math.Max(maxW, cx - gap);
            }
            if (items.Count == 0) { _preview.Drawing = null; _preview.InvalidateVisual(); return; }
            double totW = maxW, totH = -(rowTop - rowH), pad = 6;
            double fit = Math.Max(0.1, (_previewScroll.ActualWidth > 50 ? _previewScroll.ActualWidth - 24 : 700) / ((totW + 2 * pad) * SheetRender.DipPerMm));
            double zoom = fit * _zoom;
            _preview.Drawing = SheetRender.Render(items, -pad, pad, 0.12, x => x.GetPrimitives());
            _preview.Scale = SheetRender.DipPerMm * zoom;
            _preview.Width = (totW + 2 * pad) * SheetRender.DipPerMm * zoom;
            _preview.Height = (totH + 2 * pad) * SheetRender.DipPerMm * zoom;
            _preview.InvalidateVisual();
        }
        catch (Exception ex) { SetStatus("Lỗi xem trước: " + ex.Message); }
    }

    public sealed class QtyRow
    {
        public string Group { get; set; } = "";
        public string Item { get; set; } = "";
        public string Unit { get; set; } = "";
        public double Qty { get; set; }
    }

    private void RefreshQuantities()
    {
        var res = Results();
        _qty.ItemsSource = DetailingExport.Quantities(_prj, res).Select(q => new QtyRow { Group = q.Group, Item = q.Item, Unit = q.Unit, Qty = Math.Round(q.Qty, q.Unit == "kg" ? 1 : 3) }).ToList();
    }

    /// <summary>Kết quả mọi cấu kiện theo thứ tự hồ sơ (tính bổ sung cấu kiện chưa có kết quả).</summary>
    private List<(IDetailMember, DetailResult)> Results()
    {
        var list = new List<(IDetailMember, DetailResult)>();
        foreach (var m in _prj.All)
        {
            if (!_res.TryGetValue(m, out var r)) { r = DetailingProject.RunSafe(m, _prj.Settings); _res[m] = r; }
            list.Add((m, r));
        }
        return list;
    }

    private SheetComposer.SheetInfo Info(string title, string no) => new()
    {
        ProjectName = _prj.ProjectName, Owner = _prj.Owner, Location = _prj.Location, Designer = _prj.Designer, Title = title, SheetNo = no
    };

    // ==================================================================
    // Tệp
    // ==================================================================
    private void UpdateTitle() => Title = "Việt Const – Triển khai hồ sơ – " + (_path != null ? Path.GetFileName(_path) : _prj.ProjectName) + (_dirty ? " *" : "");

    private bool ConfirmDiscard()
    {
        if (!_dirty || SelfTestOut != null) return true;
        var a = MessageBox.Show(this, "Hồ sơ có thay đổi chưa lưu. Lưu trước khi đóng?", Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (a == MessageBoxResult.Cancel) return false;
        if (a == MessageBoxResult.Yes) return Save(false);
        return true;
    }

    private void NewProject(bool initial)
    {
        if (!initial && !ConfirmDiscard()) return;
        _prj = new DetailingProject();
        _path = null; _dirty = false; _res.Clear();
        RefreshTree();
        UpdateTitle();
        SetStatus("Hồ sơ mới – bấm ＋ Dầm, ＋ Cột, ＋ Móng… để thêm cấu kiện; Tệp → Mở hồ sơ mẫu để xem ví dụ đủ loại cấu kiện.");
    }

    private void OpenSample()
    {
        if (!ConfirmDiscard()) return;
        _prj = DetailingProject.Sample();
        _path = null; _dirty = false; _res.Clear();
        RecalcAll();
        RefreshTree(_prj.All.FirstOrDefault());
        UpdateTitle();
    }

    private void OpenDialog()
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = DetailingProject.FileFilter + "|Tất cả (*.*)|*.*", Title = "Mở hồ sơ triển khai" };
        if (dlg.ShowDialog(this) == true) OpenFile(dlg.FileName);
    }

    private void OpenFile(string file)
    {
        try
        {
            _prj = DetailingProject.Load(file);
            _path = file; _dirty = false; _res.Clear();
            RecalcAll();
            RefreshTree(_prj.All.FirstOrDefault());
            UpdateTitle();
        }
        catch (Exception ex) { MessageBox.Show(this, "Không mở được hồ sơ:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); NewProject(true); }
    }

    private bool Save(bool saveAs)
    {
        string p = _path;
        if (saveAs || p == null)
        {
            var dlg = new SaveFileDialog { Filter = DetailingProject.FileFilter, FileName = SafeName(_prj.ProjectName) + DetailingProject.Extension, Title = "Lưu hồ sơ triển khai" };
            if (dlg.ShowDialog(this) != true) return false;
            p = dlg.FileName;
        }
        try
        {
            _prj.Save(p);
            _path = p; _dirty = false; UpdateTitle();
            SetStatus("Đã lưu " + p);
            return true;
        }
        catch (Exception ex) { MessageBox.Show(this, "Không lưu được:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); return false; }
    }

    private static string SafeName(string s) => string.Concat((s ?? "HoSo").Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));

    private void SwitchToModel()
    {
        var main = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
        if (main != null) { main.Activate(); return; }
        if (!ConfirmDiscard()) return;
        _dirty = false;
        LauncherWindow.Open(LauncherWindow.Mode.Model);
        Close();
    }

    private void ChooseStartMode()
    {
        var l = new LauncherWindow { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        if (l.ShowDialog() == true && l.Chosen == LauncherWindow.Mode.Model) SwitchToModel();
    }

    // ==================================================================
    // Tuỳ chọn
    // ==================================================================
    private void EditSettings()
    {
        var s = _prj.Settings;
        var w = new Window { Owner = this };
        DialogUi.Setup(w, "Vật liệu, lớp bảo vệ – hồ sơ triển khai", 560, 520);
        var sp = new StackPanel { Margin = new Thickness(14) };
        ComboBox Combo(string[] items, string val) => new() { ItemsSource = items, SelectedItem = items.Contains(val) ? val : items[0], Width = 160 };
        var cC = Combo(KetCauVXT.Model.Material.ConcreteClasses, s.Concrete);
        var cM = Combo(KetCauVXT.Model.Material.RebarGrades, s.RebarMain);
        var cS = Combo(KetCauVXT.Model.Material.RebarGrades, s.RebarSmall);
        TextBox Num(double v) => new() { Text = DialogUi.F(v * 1000, "0"), Width = 80 };
        var tB = Num(s.CoverBeam); var tC = Num(s.CoverColumn); var tS = Num(s.CoverSlab); var tF = Num(s.CoverFooting); var tT = Num(s.CoverTank); var tL = Num(s.LeanThickness);
        var tStock = new TextBox { Text = DialogUi.F(s.StockLength, "0.0#"), Width = 80 };
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Cấp bê tông:", 230), cC));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Thép dọc (Ø ≥ 10):", 230), cM));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Thép đai, thép sàn (Ø ≤ 10):", 230), cS));
        sp.Children.Add(DialogUi.Label("Lớp bê tông bảo vệ tới mép cốt dọc (mm)", double.NaN, true));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Dầm:", 230), tB)); sp.Children.Add(DialogUi.Row(DialogUi.Label("Cột:", 230), tC));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Sàn:", 230), tS)); sp.Children.Add(DialogUi.Row(DialogUi.Label("Móng:", 230), tF));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Bể nước (mặt tiếp xúc nước / đất):", 230), tT));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Bê tông lót móng dày (mm):", 230), tL));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Chiều dài cây thép (m):", 230), tStock));
        var ok = DialogUi.Btn("Đồng ý", (o, e) =>
        {
            s.Concrete = cC.SelectedItem as string ?? s.Concrete; s.RebarMain = cM.SelectedItem as string ?? s.RebarMain; s.RebarSmall = cS.SelectedItem as string ?? s.RebarSmall;
            s.CoverBeam = DialogUi.ParseNum(tB.Text, s.CoverBeam * 1000) / 1000; s.CoverColumn = DialogUi.ParseNum(tC.Text, s.CoverColumn * 1000) / 1000;
            s.CoverSlab = DialogUi.ParseNum(tS.Text, s.CoverSlab * 1000) / 1000; s.CoverFooting = DialogUi.ParseNum(tF.Text, s.CoverFooting * 1000) / 1000;
            s.CoverTank = DialogUi.ParseNum(tT.Text, s.CoverTank * 1000) / 1000; s.LeanThickness = DialogUi.ParseNum(tL.Text, s.LeanThickness * 1000) / 1000;
            s.StockLength = DialogUi.ParseNum(tStock.Text, s.StockLength);
            w.DialogResult = true;
        }, true);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0), Children = { ok, DialogUi.Btn("Huỷ", (o, e) => w.DialogResult = false) } });
        w.Content = sp;
        if (w.ShowDialog() == true) { _dirty = true; RecalcAll(); UpdateTitle(); }
    }

    private void EditInfo()
    {
        var w = new Window { Owner = this };
        DialogUi.Setup(w, "Thông tin công trình", 560, 300);
        var sp = new StackPanel { Margin = new Thickness(14) };
        TextBox T(string v) => new() { Text = v ?? "", Width = 320 };
        var a = T(_prj.ProjectName); var b = T(_prj.Owner); var c = T(_prj.Location); var d = T(_prj.Designer);
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Tên công trình:", 150), a)); sp.Children.Add(DialogUi.Row(DialogUi.Label("Chủ đầu tư:", 150), b));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Địa điểm:", 150), c)); sp.Children.Add(DialogUi.Row(DialogUi.Label("Người thiết kế:", 150), d));
        var ok = DialogUi.Btn("Đồng ý", (o, e) => { _prj.ProjectName = a.Text; _prj.Owner = b.Text; _prj.Location = c.Text; _prj.Designer = d.Text; w.DialogResult = true; }, true);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0), Children = { ok, DialogUi.Btn("Huỷ", (o, e) => w.DialogResult = false) } });
        w.Content = sp;
        if (w.ShowDialog() == true) { _dirty = true; UpdateTitle(); }
    }

    // ==================================================================
    // Xuất hồ sơ
    // ==================================================================
    private string PickFolder(string title)
    {
        var dlg = new SaveFileDialog { Title = title + " – chọn thư mục (gõ tên bất kỳ)", FileName = "ChonThuMuc", Filter = "Thư mục|*.thumuc" };
        if (_path != null) dlg.InitialDirectory = Path.GetDirectoryName(_path);
        return dlg.ShowDialog(this) == true ? Path.GetDirectoryName(dlg.FileName) : null;
    }

    private void ExportDrawings(bool split)
    {
        string dir = PickFolder(split ? "Xuất bản vẽ tách từng cấu kiện" : "Xuất bản vẽ gộp 1 file");
        if (dir == null) return;
        var log = new List<string>();
        var files = DetailingExport.Drawings(_prj, Results(), Path.Combine(dir, split ? "BanVe_TachRoi" : "BanVe"), split, log);
        Report("Đã xuất " + files.Count + " file DXF", files, log);
    }

    private void ExportCurrentDxf()
    {
        var figs = CurrentFigures();
        if (figs.Count == 0) return;
        var dlg = new SaveFileDialog { Filter = "AutoCAD DXF (*.dxf)|*.dxf", FileName = SafeName(KindTitle(_cur) + "_" + _cur.Name) + ".dxf" };
        if (dlg.ShowDialog(this) != true) return;
        SheetComposer.Compose(figs, 100, Info(KindTitle(_cur).ToUpperInvariant() + " " + _cur.Name, "KC-01"), "A1").Save(dlg.FileName);
        SetStatus("Đã xuất " + dlg.FileName);
    }

    private void ExportExcel()
    {
        var dlg = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = SafeName(_prj.ProjectName) + "_KhoiLuong.xlsx" };
        if (dlg.ShowDialog(this) != true) return;
        try { DetailingExport.Excel(_prj, Results(), dlg.FileName); SetStatus("Đã xuất " + dlg.FileName); }
        catch (Exception ex) { MessageBox.Show(this, "Không xuất được Excel:\n" + ex.Message, Title); }
    }

    private void ExportWord()
    {
        var dlg = new SaveFileDialog { Filter = "Word (*.docx)|*.docx", FileName = SafeName(_prj.ProjectName) + "_ThuyetMinh.docx" };
        if (dlg.ShowDialog(this) != true) return;
        try { DetailingExport.Word(_prj, Results(), dlg.FileName); SetStatus("Đã xuất " + dlg.FileName); }
        catch (Exception ex) { MessageBox.Show(this, "Không xuất được Word:\n" + ex.Message, Title); }
    }

    private void ExportAll()
    {
        string dir = PickFolder("Xuất toàn bộ hồ sơ");
        if (dir == null) return;
        var (files, log) = ExportAllTo(dir);
        Report("Đã xuất hồ sơ: " + files.Count + " file", files, log);
    }

    /// <summary>Bản vẽ gộp + tách, Excel, Word vào thư mục dir. Trả về file đã ghi + nhật ký lỗi.</summary>
    public (List<string> Files, List<string> Log) ExportAllTo(string dir)
    {
        var log = new List<string>();
        var res = Results();
        var files = new List<string>();
        try { files.AddRange(DetailingExport.Drawings(_prj, res, Path.Combine(dir, "BanVe"), false, log)); } catch (Exception ex) { log.Add("Bản vẽ gộp: " + ex.Message); }
        try { files.AddRange(DetailingExport.Drawings(_prj, res, Path.Combine(dir, "BanVe_TachRoi"), true, log)); } catch (Exception ex) { log.Add("Bản vẽ tách: " + ex.Message); }
        try { string x = Path.Combine(dir, SafeName(_prj.ProjectName) + "_KhoiLuong.xlsx"); DetailingExport.Excel(_prj, res, x); files.Add(x); } catch (Exception ex) { log.Add("Excel: " + ex.Message); }
        try { string d = Path.Combine(dir, SafeName(_prj.ProjectName) + "_ThuyetMinh.docx"); DetailingExport.Word(_prj, res, d); files.Add(d); } catch (Exception ex) { log.Add("Word: " + ex.Message); }
        return (files, log);
    }

    private void Report(string head, List<string> files, List<string> log)
    {
        SetStatus(head + (log.Count > 0 ? " – " + log.Count + " cảnh báo" : ""));
        if (SelfTestOut != null) return;
        var msg = head + ":\n" + string.Join("\n", files.Take(14).Select(Path.GetFileName)) + (files.Count > 14 ? "\n…" : "")
                  + (log.Count > 0 ? "\n\nCảnh báo:\n" + string.Join("\n", log.Take(8)) : "") + "\n\nMở thư mục?";
        if (MessageBox.Show(this, msg, Title, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes && files.Count > 0)
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = Path.GetDirectoryName(files[0]), UseShellExecute = true }); } catch { }
    }

    // ==================================================================
    // Tự kiểm tra (gọi từ MainWindow --selftest)
    // ==================================================================
    /// <summary>Mở hồ sơ mẫu, chọn lần lượt từng nhóm cấu kiện, chụp ảnh tab kết quả / bản vẽ, xuất toàn bộ hồ sơ. Trả về nhật ký.</summary>
    public async Task<List<string>> SelfTest(string outDir, string tag)
    {
        var log = new List<string>();
        SelfTestOut = outDir;
        try
        {
            _prj = DetailingProject.Sample(); _res.Clear();
            RecalcAll();
            int i = 0;
            foreach (var (key, title, list) in _prj.Groups)
            {
                if (list.Count == 0) continue;
                var m = (IDetailMember)list[0];
                RefreshTree(m);
                ShowMember(m);
                _tabs.SelectedIndex = i % 2 == 0 ? 0 : 2;
                await Task.Delay(500);
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                if (_tabs.SelectedIndex == 2) RefreshPreview();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Shot(Path.Combine(outDir, $"{tag}_TrienKhai_{i + 1:00}_{key}.png"));
                var r = _res[m];
                log.Add($"triển khai {title} {m.Name}: {(r.Ok ? "đạt" : "không đạt")}, {r.Checks.Count} kiểm tra, {r.Bars.Count} số hiệu thép, {CurrentFigures().Count} hình");
                i++;
            }
            _tabs.SelectedIndex = 3; RefreshQuantities();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Shot(Path.Combine(outDir, $"{tag}_TrienKhai_KhoiLuong.png"));
            var (files, elog) = ExportAllTo(Path.Combine(outDir, "TrienKhai"));
            log.Add("triển khai: xuất " + files.Count + " file" + (elog.Count > 0 ? " – LỖI " + string.Join("; ", elog) : ""));
            _prj.Save(Path.Combine(outDir, "TrienKhai", "HoSoMau" + DetailingProject.Extension));
        }
        catch (Exception ex) { log.Add("LỖI triển khai: " + ex); }
        _dirty = false;
        return log;
    }

    private void Shot(string file)
    {
        UpdateLayout();
        var src = (FrameworkElement)Content;
        int w = (int)Math.Max(1, src.ActualWidth), h = (int)Math.Max(1, src.ActualHeight);
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(src);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(file);
        enc.Save(fs);
    }
}
