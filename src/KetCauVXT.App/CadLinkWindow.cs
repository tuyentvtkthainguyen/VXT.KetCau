using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using KetCauVXT.Import.Link;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Cửa sổ "Bản vẽ liên kết" (không chặn): danh sách bản vẽ AutoCAD liên kết vào dự án (thêm / bỏ / đọc lại / mở thư mục /
/// mở bằng AutoCAD / chọn lại file), thuộc tính từng bản vẽ (tầng, điểm gốc, điểm chèn, góc xoay, tỷ lệ, đơn vị, độ đậm, màu,
/// chữ, bắt điểm, tự cập nhật), căn theo 2 điểm, bảng layer (ẩn/hiện + gán nhóm cấu kiện) và "Cập nhật mô hình từ link…".
/// Mọi thao tác đi qua cửa sổ chính (MainWindow.CadLink.cs) để có hoàn tác và vẽ lại mặt bằng.</summary>
internal sealed class CadLinkWindow : Window
{
    private readonly MainWindow _main;
    private readonly ListBox _list;
    private readonly TextBlock _status, _file, _info;
    private readonly ComboBox _cbStory, _cbUnit;
    private readonly TextBox _tBaseX, _tBaseY, _tOffX, _tOffY, _tRot, _tScale;
    private readonly CheckBox _chkVisible, _chkByLayer, _chkText, _chkSnap, _chkAuto, _chkGlobalSnap;
    private readonly Slider _slOpacity;
    private readonly DataGrid _layers;
    private readonly Panel _props;
    private ObservableCollection<LayerRow> _rows = new();
    private CheckAllBar _layerAll;
    private string _selId;
    private bool _loading;

    private static readonly (string Text, double Value)[] Units =
    {
        ("Tự động ($INSUNITS)", 0), ("mm", 0.001), ("cm", 0.01), ("m", 1.0), ("inch", 0.0254), ("feet", 0.3048)
    };

    internal static readonly string[] CategoryChoices = new[] { "(theo chuẩn layer)" }.Concat(CadCategory.All.Select(CadCategory.Label)).ToArray();

    public CadLinkWindow(MainWindow main)
    {
        _main = main;
        DialogUi.Setup(this, "Bản vẽ liên kết (AutoCAD DWG/DXF)", 1080, 680);
        ShowInTaskbar = false;

        // ---- trái: danh sách ----
        _list = new ListBox { MinWidth = 250, Margin = new Thickness(0, 4, 0, 4) };
        _list.SelectionChanged += (s, e) => { if (!_loading) { _selId = (_list.SelectedItem as ListBoxItem)?.Tag as string; LoadProps(); } };
        var btnAdd = DialogUi.Btn("Thêm bản vẽ…", async (s, e) => { var l = await _main.CadLinkAddInteractive(); if (l != null) { _selId = l.Id; Reload(); } }, true);
        var btnDel = DialogUi.Btn("Bỏ liên kết", (s, e) => { var l = Sel(); if (l != null && Confirm($"Bỏ liên kết bản vẽ \"{l.DisplayName}\"? (file bản vẽ không bị xoá)")) { _main.CadLinkRemove(l); _selId = null; Reload(); } });
        var btnReload = DialogUi.Btn("Đọc lại", async (s, e) => { var l = Sel(); if (l != null) await _main.CadLinkReload(l); });
        var btnRelink = DialogUi.Btn("Chọn lại file…", async (s, e) => { var l = Sel(); if (l != null) await _main.CadLinkRelink(l); });
        var btnFolder = DialogUi.Btn("Mở thư mục", (s, e) => _main.CadLinkOpenFolder(Sel()));
        var btnAcad = DialogUi.Btn("Mở bằng AutoCAD", (s, e) => _main.CadLinkOpenInAutoCad(Sel()));
        var leftBtns = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var b in new[] { btnAdd, btnDel, btnReload, btnRelink, btnFolder, btnAcad }) { b.Margin = new Thickness(0, 0, 4, 4); leftBtns.Children.Add(b); }
        _chkGlobalSnap = new CheckBox { Content = "Bắt điểm bản vẽ link (đầu nét, trung điểm, giao nét, tâm)", IsChecked = _main.CadLinkSnapEnabled, Margin = new Thickness(0, 6, 0, 2),
            ToolTip = "Bật/tắt bắt điểm vào nét bản vẽ link cho ＋ Dầm, công cụ dầm ETABS, tường (nút / giao trục của mô hình vẫn ưu tiên)" };
        _chkGlobalSnap.Checked += (s, e) => _main.CadLinkSnapEnabled = true;
        _chkGlobalSnap.Unchecked += (s, e) => _main.CadLinkSnapEnabled = false;
        var left = new DockPanel { Margin = new Thickness(0, 0, 10, 0) };
        var lh = DialogUi.Label("Bản vẽ đã liên kết:", double.NaN, true); DockPanel.SetDock(lh, Dock.Top); left.Children.Add(lh);
        DockPanel.SetDock(_chkGlobalSnap, Dock.Bottom); left.Children.Add(_chkGlobalSnap);
        DockPanel.SetDock(leftBtns, Dock.Bottom); left.Children.Add(leftBtns);
        left.Children.Add(_list);

        // ---- phải: thuộc tính ----
        _file = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 2) };
        _info = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 4) };
        _cbStory = new ComboBox { Width = 160 };
        _cbStory.SelectionChanged += (s, e) => { if (!_loading) Edit(l => l.Story = _cbStory.SelectedItem as string ?? CadLink.AllStories); };
        _cbUnit = new ComboBox { Width = 160 };
        foreach (var u in Units) _cbUnit.Items.Add(u.Text);
        _cbUnit.SelectionChanged += (s, e) => { if (!_loading && _cbUnit.SelectedIndex >= 0) Edit(l => l.UnitScale = Units[_cbUnit.SelectedIndex].Value, true); };
        TextBox Num(string tip) { var t = new TextBox { Width = 80, ToolTip = tip, Margin = new Thickness(0, 0, 8, 0) }; t.LostKeyboardFocus += (s, e) => ApplyNumbers(); t.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) ApplyNumbers(); }; return t; }
        _tBaseX = Num("Điểm gốc trên bản vẽ X (m, sau khi đổi đơn vị)"); _tBaseY = Num("Điểm gốc trên bản vẽ Y (m)");
        _tOffX = Num("Điểm chèn trên mô hình X (m) – độ dời dx khi điểm gốc = 0"); _tOffY = Num("Điểm chèn trên mô hình Y (m) – độ dời dy");
        _tRot = Num("Góc xoay bản vẽ quanh điểm gốc (độ, ngược chiều kim đồng hồ)"); _tScale = Num("Hệ số phóng thêm (1 = đúng kích thước)");
        CheckBox Chk(string text, Action<CadLink, bool> set, string tip, bool reread = false)
        {
            var c = new CheckBox { Content = text, Margin = new Thickness(0, 2, 14, 2), ToolTip = tip };
            c.Checked += (s, e) => { if (!_loading) Edit(l => set(l, true), reread); };
            c.Unchecked += (s, e) => { if (!_loading) Edit(l => set(l, false), reread); };
            return c;
        }
        _chkVisible = Chk("Hiện", (l, v) => l.Visible = v, "Hiện bản vẽ làm nền mặt bằng");
        _chkByLayer = Chk("Theo màu layer", (l, v) => l.ByLayerColor = v, "Vẽ nét theo màu layer (nhạt theo độ đậm); bỏ chọn = xám nhạt");
        _chkText = Chk("Hiện chữ", (l, v) => l.ShowText = v, "Hiện chữ TEXT/MTEXT của bản vẽ (tối đa 800 chữ trong khung nhìn)");
        _chkSnap = Chk("Bắt điểm", (l, v) => l.Snap = v, "Bắt điểm vào nét bản vẽ này");
        _chkAuto = Chk("Tự cập nhật khi lưu trong AutoCAD", (l, v) => l.AutoReload = v, "Theo dõi file: AutoCAD lưu bản vẽ → đọc lại nền, báo thay đổi");
        _slOpacity = new Slider { Minimum = 0.05, Maximum = 1, Width = 180, SmallChange = 0.05, LargeChange = 0.1, TickFrequency = 0.05, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
        _slOpacity.ValueChanged += (s, e) => { if (!_loading) Edit(l => l.Opacity = Math.Round(_slOpacity.Value, 2)); };

        var btnAlign = DialogUi.Btn("Căn theo 2 điểm", (s, e) => { var l = Sel(); if (l != null) _main.CadLinkStartAlign(l); });
        btnAlign.ToolTip = "Bấm một điểm trên bản vẽ link (đầu nét, giao nét) rồi bấm giao trục / nút tương ứng của mô hình → bản vẽ dời cho trùng";
        var btnZoom = DialogUi.Btn("Phóng tới bản vẽ", (s, e) => _main.CadLinkZoom(Sel()));
        var btnZoomAll = DialogUi.Btn("Vừa mô hình + bản vẽ", (s, e) => _main.CadLinkZoom(null));
        var btnReset = DialogUi.Btn("Đặt lại vị trí", (s, e) => Edit(l => { l.BaseX = l.BaseY = l.OffsetX = l.OffsetY = l.Rotation = 0; l.Scale = 1; }, false, true));
        var btnSync = DialogUi.Btn("Cập nhật mô hình từ link…", (s, e) => { var l = Sel(); if (l != null) _main.CadLinkUpdateModel(l); }, true);
        btnSync.ToolTip = "Nhận dạng tường / cột / dầm / trục trên bản vẽ (theo nhóm layer bên dưới) và so với mô hình của tầng: thêm mới, dời, không còn trên bản vẽ – đánh dấu rồi áp dụng (có hoàn tác)";

        _layers = DialogUi.NewGrid();
        _layers.SelectionMode = DataGridSelectionMode.Extended;
        _layers.Columns.Add(new DataGridCheckBoxColumn { Header = "Hiện", Binding = new Binding("Shown") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = 44 });
        var sw = new FrameworkElementFactory(typeof(Border));
        sw.SetBinding(Border.BackgroundProperty, new Binding("Swatch"));
        sw.SetValue(FrameworkElement.WidthProperty, 26.0); sw.SetValue(FrameworkElement.HeightProperty, 12.0);
        sw.SetValue(Border.BorderBrushProperty, Brushes.Gray); sw.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        _layers.Columns.Add(new DataGridTemplateColumn { Header = "Màu", CellTemplate = new DataTemplate { VisualTree = sw }, Width = 40 });
        _layers.Columns.Add(DialogUi.Text("Layer", "Name", true, 200));
        _layers.Columns.Add(DialogUi.Text("Thực thể", "Counts", true, 210));
        _layers.Columns.Add(DialogUi.Combo("Nhóm cấu kiện (nhận dạng)", "Category", CategoryChoices, 170));
        _layers.Columns.Add(DialogUi.Text("Lý do", "Reason", true, 180));
        var btnShowAll = DialogUi.Btn("Hiện tất cả", (s, e) => SetAllShown(true));
        var btnHideSel = DialogUi.Btn("Ẩn layer đã chọn", (s, e) => SetSelectedShown(false));
        var btnOnly = DialogUi.Btn("Chỉ hiện layer đã chọn", (s, e) => { SetAllShown(false, false); SetSelectedShown(true); });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int row = 0;
        void Row(string label, UIElement el)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var t = DialogUi.Label(label); Grid.SetRow(t, row); Grid.SetColumn(t, 0); grid.Children.Add(t);
            if (el is FrameworkElement fe) fe.Margin = new Thickness(0, 2, 0, 2);
            Grid.SetRow(el, row); Grid.SetColumn(el, 1); grid.Children.Add(el);
            row++;
        }
        Row("Tầng hiển thị:", DialogUi.Row(_cbStory, DialogUi.Hint("  (\"Tất cả tầng\" = nền chung mọi tầng; cập nhật mô hình dùng tầng đang xem)")));
        Row("Đơn vị bản vẽ:", _cbUnit);
        Row("Điểm gốc bản vẽ:", DialogUi.Row(DialogUi.Label("X"), _tBaseX, DialogUi.Label("Y"), _tBaseY, DialogUi.Hint("m")));
        Row("Điểm chèn mô hình:", DialogUi.Row(DialogUi.Label("X"), _tOffX, DialogUi.Label("Y"), _tOffY, DialogUi.Hint("m (dx, dy)")));
        Row("Xoay / tỷ lệ:", DialogUi.Row(DialogUi.Label("Góc"), _tRot, DialogUi.Label("°   Tỷ lệ ×"), _tScale));
        Row("Hiển thị:", DialogUi.Row(_chkVisible, _chkByLayer, _chkText, _chkSnap));
        Row("Độ đậm nét:", DialogUi.Row(_slOpacity));
        Row("Liên kết sống:", _chkAuto);
        Row("", new WrapPanel { Children = { btnAlign, btnReset, btnZoom, btnZoomAll, btnSync } });

        var props = new DockPanel();
        var top = new StackPanel();
        top.Children.Add(_file); top.Children.Add(_info); top.Children.Add(grid);
        var lt = DialogUi.Label("Layer của bản vẽ (bỏ chọn \"Hiện\" để ẩn trên nền; nhóm cấu kiện dùng khi cập nhật mô hình):", double.NaN, true);
        lt.Margin = new Thickness(0, 8, 0, 0);
        top.Children.Add(lt);
        // ô "Tất cả" / Chọn hết / Bỏ hết / Đảo cho cột "Hiện" – áp một lần cho cả lô, vẽ lại nền một lần
        _layerAll = new CheckAllBar(() => _layers.Items.OfType<LayerRow>().Select(r => r.Shown).ToList(), ApplyShownBatch, "Hiện tất cả layer");
        _layerAll.Watch(_layers, false);
        top.Children.Add(_layerAll);
        DockPanel.SetDock(top, Dock.Top); props.Children.Add(top);
        var lb = DialogUi.Row(btnShowAll, btnHideSel, btnOnly); DockPanel.SetDock(lb, Dock.Bottom); props.Children.Add(lb);
        props.Children.Add(_layers);
        _props = props;

        _status = new TextBlock { Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        var close = DialogUi.Btn("Đóng", (s, e) => Close());
        var bottom = new DockPanel();
        DockPanel.SetDock(close, Dock.Right); bottom.Children.Add(close); bottom.Children.Add(_status);

        var main0 = new Grid { Margin = new Thickness(10) };
        main0.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        main0.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        main0.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        main0.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(left, 0); main0.Children.Add(left);
        Grid.SetColumn(props, 1); main0.Children.Add(props);
        Grid.SetRow(bottom, 1); Grid.SetColumnSpan(bottom, 2); main0.Children.Add(bottom);
        Content = main0;
        Reload();
    }

    private CadLink Sel() => _selId == null ? null : _main.CadLinkProject?.CadLinks?.FirstOrDefault(q => q.Id == _selId);

    private bool Confirm(string msg) => MessageBox.Show(this, msg, Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <summary>Nạp lại danh sách + thuộc tính (dự án có thể đã thay sau hoàn tác / mở file).</summary>
    public void Reload()
    {
        var p = _main.CadLinkProject;
        _loading = true;
        try
        {
            _list.Items.Clear();
            ListBoxItem selItem = null;
            foreach (var l in p?.CadLinks ?? new List<CadLink>())
            {
                if (l == null) continue;
                var g = CadLinkCache.Get(l);
                bool missing = _main.CadLinkIsMissing(l);
                string state = missing ? "⚠ Không tìm thấy bản vẽ link – chọn lại" : g == null ? "đang đọc…" : $"{g.SegmentCount:N0} nét · {g.Layers.Count} layer";
                var sp = new StackPanel { Margin = new Thickness(2) };
                sp.Children.Add(new TextBlock { Text = l.DisplayName + (l.Visible ? "" : "  (ẩn)"), FontWeight = FontWeights.SemiBold });
                sp.Children.Add(new TextBlock { Text = $"Tầng: {(l.IsAllStories ? CadLink.AllStories : l.Story)} · {state}", Foreground = missing ? Brushes.Firebrick : Brushes.DimGray, FontSize = 11 });
                var it = new ListBoxItem { Content = sp, Tag = l.Id };
                _list.Items.Add(it);
                if (l.Id == _selId) selItem = it;
            }
            if (selItem == null && _list.Items.Count > 0) { selItem = (ListBoxItem)_list.Items[0]; _selId = selItem.Tag as string; }
            if (selItem == null) _selId = null;
            _list.SelectedItem = selItem;
            _chkGlobalSnap.IsChecked = _main.CadLinkSnapEnabled;
        }
        finally { _loading = false; }
        LoadProps();
        int n = p?.CadLinks?.Count ?? 0;
        _status.Text = n == 0 ? "Chưa có bản vẽ liên kết – bấm \"Thêm bản vẽ…\" để chọn file DWG/DXF (bản vẽ hiện làm nền mặt bằng, tự cập nhật khi lưu trong AutoCAD)."
            : $"{n} bản vẽ liên kết. Sửa vị trí: gõ số rồi Enter, hoặc \"Căn theo 2 điểm\" trên mặt bằng.";
    }

    private void LoadProps()
    {
        var l = Sel();
        _props.IsEnabled = l != null;
        _loading = true;
        try
        {
            _cbStory.Items.Clear();
            _cbStory.Items.Add(CadLink.AllStories);
            foreach (var s in _main.CadLinkProject?.Stories ?? new List<Story>()) _cbStory.Items.Add(s.Name);
            if (l == null) { _file.Text = ""; _info.Text = ""; _rows = new ObservableCollection<LayerRow>(); _layers.ItemsSource = _rows; return; }
            if (!l.IsAllStories && !_cbStory.Items.Contains(l.Story)) _cbStory.Items.Add(l.Story);
            _cbStory.SelectedItem = l.IsAllStories ? CadLink.AllStories : l.Story;
            string path = l.Resolve(_main.CadLinkProjectPath);
            _file.Text = (path ?? l.FilePath) + (string.IsNullOrEmpty(l.RelativePath) ? "" : $"   (tương đối: {l.RelativePath})");
            _file.Foreground = path == null ? Brushes.Firebrick : Brushes.DimGray;
            var g = CadLinkCache.Get(l);
            _info.Text = path == null ? "Không tìm thấy bản vẽ link – chọn lại (nút \"Chọn lại file…\")."
                : g == null ? "Đang đọc bản vẽ…"
                : string.Format(CultureInfo.InvariantCulture, "$INSUNITS = {0} → ×{1:0.######} m/đơn vị · {2:N0} nét, {3} chữ, {4} layer · phạm vi {5:0.0} × {6:0.0} m · sửa lần cuối {7:dd/MM/yyyy HH:mm:ss}",
                    g.InsUnits, g.UnitScale, g.SegmentCount, g.Texts.Count, g.Layers.Count, g.HasExtent ? (g.X1 - g.X0) : 0, g.HasExtent ? (g.Y1 - g.Y0) : 0, g.WriteUtc.ToLocalTime())
                  + (!string.IsNullOrEmpty(l.SyncedHash) && l.SyncedHash != g.Hash ? " · bản vẽ đã đổi từ lần cập nhật mô hình trước" : "")
                  + (g.Warnings.Count > 0 ? " · " + g.Warnings[0] : "");
            int ui = Array.FindIndex(Units, u => Math.Abs(u.Value - l.UnitScale) < 1e-12);
            _cbUnit.SelectedIndex = ui >= 0 ? ui : 0;
            _tBaseX.Text = F(l.BaseX); _tBaseY.Text = F(l.BaseY); _tOffX.Text = F(l.OffsetX); _tOffY.Text = F(l.OffsetY);
            _tRot.Text = F(l.Rotation); _tScale.Text = F(l.Scale);
            _chkVisible.IsChecked = l.Visible; _chkByLayer.IsChecked = l.ByLayerColor; _chkText.IsChecked = l.ShowText; _chkSnap.IsChecked = l.Snap; _chkAuto.IsChecked = l.AutoReload;
            _slOpacity.Value = Math.Clamp(l.Opacity, 0.05, 1);
            LoadLayers(l, g);
        }
        finally { _loading = false; }
    }

    private static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    private void LoadLayers(CadLink l, CadLinkGeometry g)
    {
        _rows = new ObservableCollection<LayerRow>();
        if (g != null)
        {
            var scan = CadLinkSync.ScanLayers(_main.CadLinkProject, l, g);
            var hidden = new HashSet<string>(l.HiddenLayers ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in (l.LayerMap ?? new List<LayerMapEntry>()).Where(m => m != null && !string.IsNullOrEmpty(m.Layer))) map[m.Layer] = m.Category ?? "";
            foreach (var li in scan.Layers)
            {
                var r = new LayerRow(this, li.Name)
                {
                    Swatch = new SolidColorBrush(MainWindow.AciColor(li.Color)),
                    Counts = li.CountsText,
                    Reason = map.ContainsKey(li.Name) ? "gán trong bản vẽ link"
                        : (string.IsNullOrEmpty(li.Category) ? "(không nhận)" : CadCategory.Label(li.Category)) + (string.IsNullOrEmpty(li.Reason) ? "" : " – " + li.Reason),
                };
                r.Init(!hidden.Contains(li.Name), map.TryGetValue(li.Name, out var mc) ? CadCategory.Label(CadCategory.Normalize(mc)) : CategoryChoices[0],
                       string.IsNullOrEmpty(li.Category) ? "" : CadCategory.Label(li.Category));
                _rows.Add(r);
            }
        }
        _layers.ItemsSource = _rows;
        _layerAll?.Refresh();
    }

    /// <summary>Đổi cột "Hiện" cho cả lô các dòng đang hiện trong bảng (một lần cập nhật nền link).</summary>
    private void ApplyShownBatch(Func<int, bool, bool> f)
    {
        var l = Sel();
        if (l == null) return;
        var list = _layers.Items.OfType<LayerRow>().ToList();
        _loading = true;
        try { for (int i = 0; i < list.Count; i++) list[i].Shown = f(i, list[i].Shown); }
        finally { _loading = false; }
        l.HiddenLayers ??= new List<string>();
        var names = new HashSet<string>(list.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
        l.HiddenLayers.RemoveAll(h => names.Contains(h));
        l.HiddenLayers.AddRange(list.Where(r => !r.Shown).Select(r => r.Name));
        _main.CadLinkEdited(l);
    }

    private void ApplyNumbers()
    {
        if (_loading) return;
        var l = Sel();
        if (l == null) return;
        double bx = DialogUi.ParseNum(_tBaseX.Text, l.BaseX), by = DialogUi.ParseNum(_tBaseY.Text, l.BaseY);
        double ox = DialogUi.ParseNum(_tOffX.Text, l.OffsetX), oy = DialogUi.ParseNum(_tOffY.Text, l.OffsetY);
        double rot = DialogUi.ParseNum(_tRot.Text, l.Rotation), sc = DialogUi.ParseNum(_tScale.Text, l.Scale);
        if (sc <= 0) sc = l.Scale;
        if (bx == l.BaseX && by == l.BaseY && ox == l.OffsetX && oy == l.OffsetY && rot == l.Rotation && sc == l.Scale) return;
        Edit(q => { q.BaseX = bx; q.BaseY = by; q.OffsetX = ox; q.OffsetY = oy; q.Rotation = rot; q.Scale = sc; }, false, true);
    }

    /// <summary>Sửa thuộc tính link; undo = true: lưu bước hoàn tác (đổi vị trí); reread: đọc lại file (đổi đơn vị).</summary>
    private void Edit(Action<CadLink> change, bool reread = false, bool undo = false)
    {
        var l = Sel();
        if (l == null) return;
        string snap = undo ? _main.CadLinkSnapshot() : null;
        change(l);
        if (undo) _main.CadLinkPushUndo(snap);
        _main.CadLinkEdited(Sel() ?? l, reread);
        if (undo || reread) LoadProps();
        else if (!_loading) RefreshListLine();
    }

    private void RefreshListLine()
    {
        // tên / tầng / (ẩn) trên danh sách
        var keep = _selId;
        _loading = true;
        try
        {
            var l = Sel();
            if (l == null || _list.SelectedItem is not ListBoxItem it || it.Content is not StackPanel sp || sp.Children.Count < 2) return;
            ((TextBlock)sp.Children[0]).Text = l.DisplayName + (l.Visible ? "" : "  (ẩn)");
            var g = CadLinkCache.Get(l);
            ((TextBlock)sp.Children[1]).Text = $"Tầng: {(l.IsAllStories ? CadLink.AllStories : l.Story)} · " + (g == null ? "đang đọc…" : $"{g.SegmentCount:N0} nét · {g.Layers.Count} layer");
        }
        finally { _loading = false; _selId = keep; }
    }

    // ---- layer ----
    internal void LayerShownChanged(LayerRow r)
    {
        if (_loading) return;
        var l = Sel();
        if (l == null) return;
        l.HiddenLayers ??= new List<string>();
        l.HiddenLayers.RemoveAll(h => string.Equals(h, r.Name, StringComparison.OrdinalIgnoreCase));
        if (!r.Shown) l.HiddenLayers.Add(r.Name);
        _main.CadLinkEdited(l);
        _layerAll?.Refresh();
    }

    internal void LayerCategoryChanged(LayerRow r)
    {
        if (_loading) return;
        var l = Sel();
        if (l == null) return;
        l.LayerMap ??= new List<LayerMapEntry>();
        l.LayerMap.RemoveAll(m => string.Equals(m.Layer, r.Name, StringComparison.OrdinalIgnoreCase));
        string cat = CadCategory.Normalize(r.Category);
        if (r.Category != CategoryChoices[0] && cat.Length > 0) l.LayerMap.Add(new LayerMapEntry(r.Name, cat));
        r.Reason = r.Category == CategoryChoices[0] ? "theo chuẩn layer" : "gán trong bản vẽ link";
        _main.CadLinkEdited(l);
    }

    private void SetAllShown(bool shown, bool apply = true)
    {
        var l = Sel();
        if (l == null) return;
        _loading = true;
        try { foreach (var r in _rows) r.Shown = shown; }
        finally { _loading = false; }
        l.HiddenLayers ??= new List<string>();
        l.HiddenLayers.Clear();
        if (!shown) l.HiddenLayers.AddRange(_rows.Select(r => r.Name));
        if (apply) _main.CadLinkEdited(l);
        _layerAll?.Refresh();
    }

    private void SetSelectedShown(bool shown)
    {
        var l = Sel();
        if (l == null) return;
        var sel = _layers.SelectedItems.OfType<LayerRow>().ToList();
        if (sel.Count == 0) { _status.Text = "Chọn (bôi đen) các dòng layer trước."; return; }
        _loading = true;
        try { foreach (var r in sel) r.Shown = shown; }
        finally { _loading = false; }
        l.HiddenLayers ??= new List<string>();
        foreach (var r in sel)
        {
            l.HiddenLayers.RemoveAll(h => string.Equals(h, r.Name, StringComparison.OrdinalIgnoreCase));
            if (!shown) l.HiddenLayers.Add(r.Name);
        }
        _main.CadLinkEdited(l);
        _layerAll?.Refresh();
    }

    /// <summary>Một dòng bảng layer.</summary>
    internal sealed class LayerRow : RowBase
    {
        private readonly CadLinkWindow _w;
        private bool _shown;
        private string _category = "", _reason = "";
        public LayerRow(CadLinkWindow w, string name) { _w = w; Name = name; }
        public string Name { get; }
        public Brush Swatch { get; init; }
        public string Counts { get; init; } = "";
        public string Auto { get; private set; } = "";
        public void Init(bool shown, string category, string auto) { _shown = shown; _category = category; Auto = auto; }
        public bool Shown { get => _shown; set { if (_shown == value) return; _shown = value; Raise(); _w.LayerShownChanged(this); } }
        public string Category
        {
            get => _category;
            set { if (_category == value) return; _category = value ?? CategoryChoices[0]; Raise(); _w.LayerCategoryChanged(this); }
        }
        public string Reason { get => _reason; set { _reason = value ?? ""; Raise(); } }
    }
}
