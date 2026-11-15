using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[Khai báo từng bước] Bảng khai báo thông số: một khung liệt kê đủ các nhóm thông số theo thứ tự (công trình, vật liệu, tiết diện,
/// địa chất, tải trọng, gió, động đất, tổ hợp…), mỗi dòng có tình trạng (đủ / còn thiếu) và nút "Mở bảng…" bật ra MỘT hộp thoại dạng bảng
/// của riêng thông số đó; dấu ✓ "Đã kiểm" lưu trong dự án để không bỏ sót. Các nhóm ô nhập ở khung trái (Thông tin công trình, Lưới trục và tầng,
/// Tải mặc định, Gió, Động đất…) được dựng thành bảng Thông số | Giá trị tự động – sửa trong bảng = sửa ô ở khung trái.</summary>
public partial class MainWindow
{
    private sealed class DeclStep
    {
        public string Key = "", Group = "", Name = "";
        public Func<string> Status;           // tình trạng; bắt đầu bằng "⚠" = còn thiếu
        public Action Open;                   // mở bảng / hộp thoại
    }

    private Window _declWin;

    private void MnuDeclareHub_Click(object sender, RoutedEventArgs e) => OpenDeclareHub();

    private List<DeclStep> BuildDeclSteps()
    {
        var L = new List<DeclStep>();
        void D(string group, string name, Func<string> st, Action open) => L.Add(new DeclStep { Key = group + "|" + name, Group = group, Name = name, Status = st, Open = open });
        void Panel(string group, string tabHeader)
        {
            var tab = tabLeft.Items.OfType<TabItem>().FirstOrDefault(t => HeaderText(t) == tabHeader);
            if (tab == null) return;
            foreach (var gb in FindChildren<GroupBox>(tab.Content as DependencyObject))
            {
                var rows = FieldRows(gb);
                if (rows.Count == 0) continue;
                string title = gb.Header as string ?? "Thông số";
                var g = gb;
                D(group, title, () =>
                {
                    var rs = FieldRows(g);
                    int empty = rs.Count(r => r.Box is TextBox tb && string.IsNullOrWhiteSpace(tb.Text) || r.Box is ComboBox cb && cb.SelectedIndex <= 0 && string.IsNullOrWhiteSpace(cb.Text) && cb.Items.Count > 1);
                    return empty > 0 ? $"⚠ còn {empty}/{rs.Count} ô trống" : $"{rs.Count} thông số";
                }, () => ShowFieldTable(g, title));
            }
        }
        string Warn(bool bad, string ok, string warn) => bad ? "⚠ " + warn : ok;

        Panel("1. Công trình", "Công trình");
        D("1. Công trình", "Lưới trục + lưới cột + chiều cao tầng (bảng)", () => Warn(_p.Grids.Count == 0, $"{_p.GridsX.Count()} × {_p.GridsY.Count()} trục, {_p.Stories.Count} tầng", "chưa có lưới trục"), OpenGridColumnDialog);
        D("2. Vật liệu – tiết diện", "Vật liệu (bê tông, cốt thép, thép hình)", () => $"{_p.Materials.Count} vật liệu", () => MnuMaterials_Click(this, new RoutedEventArgs()));
        D("2. Vật liệu – tiết diện", "Tiết diện dầm / cột", () => Warn(_p.FrameSections.Count == 0, $"{_p.FrameSections.Count} tiết diện", "chưa có tiết diện"), OpenSectionManager);
        Panel("2. Vật liệu – tiết diện", "Tiết diện");
        D("2. Vật liệu – tiết diện", "Loại tường", () => $"{_p.WallTypes.Count} loại tường", () => MnuWallTypes_Click(this, new RoutedEventArgs()));
        D("2. Vật liệu – tiết diện", "Cấu tạo sàn", () => $"{_p.FloorBuildUps.Count} cấu tạo", () => MnuBuildUps_Click(this, new RoutedEventArgs()));
        D("3. Địa chất", "Hố khoan, lớp đất (thẻ Địa chất)", () => Warn(_p.Boreholes.Count == 0, $"{_p.Boreholes.Count} hố khoan, {_p.Boreholes.Sum(b => b.Layers?.Count ?? 0)} lớp đất", "chưa có hố khoan"), () => { _declWin?.Hide(); SelectLeft("Địa chất"); SetStatus("Nhập hố khoan / lớp đất ở thẻ Địa chất – mở lại Bảng khai báo (Khai báo → Bảng khai báo từng bước…) khi xong."); });
        Panel("4. Tải trọng", "Tải trọng");
        D("4. Tải trọng", "Gió – vùng áp lực, dạng địa hình (bảng chọn)", () => Warn(string.IsNullOrWhiteSpace(_p.Wind.Zone) && _p.Wind.Enabled, $"vùng {_p.Wind.Zone}, địa hình {_p.Wind.Terrain}, W0 = {_p.Wind.W0:0.###} kN/m²", $"chưa chọn vùng gió (W0 = {_p.Wind.W0:0.###})"), () => { if (SelfTestOut == null) AskSiteData(); });
        D("4. Tải trọng", "Trường hợp tải (Load Patterns)", () => $"{_p.Patterns.Count} trường hợp", () => MnuLoadPatterns_Click(this, new RoutedEventArgs()));
        D("4. Tải trọng", "Trường hợp phân tích (Load Cases)", () => "", () => MnuLoadCases_Click(this, new RoutedEventArgs()));
        D("4. Tải trọng", "Tổ hợp tải trọng", () => Warn(_p.Combinations.Count == 0, $"{_p.Combinations.Count} tổ hợp", "chưa có tổ hợp – bấm Tự tạo theo TCVN 2737:2023"), OpenComboEditor);
        D("5. Động đất – dao động", "Phổ phản ứng / động đất (agR, nền, q, γI)", () => _p.Seismic.Enabled ? $"agR = {_p.Seismic.AgR:0.####} g, nền {_p.Seismic.Soil}, q = {_p.Seismic.Q:0.##}, γI = {_p.Seismic.ImportanceFactor:0.##}" : "không tính động đất", () => MnuSpectrum_Click(this, new RoutedEventArgs()));
        D("5. Động đất – dao động", "Nguồn khối lượng & dao động riêng", () => "", () => MnuModal_Click(this, new RoutedEventArgs()));
        D("5. Động đất – dao động", "Sàn cứng (diaphragm) theo tầng", () => _p.UseRigidDiaphragm ? "dùng sàn cứng" : "không dùng sàn cứng", () => MnuDiaphragms_Click(this, new RoutedEventArgs()));
        return L;
    }

    internal Window OpenDeclareHub()
    {
        if (_p == null) return null;
        if (_declWin != null) { _declWin.Show(); _declWin.Activate(); return _declWin; }
        var steps = BuildDeclSteps();
        _p.DeclareChecked ??= new List<string>();
        var w = new Window { Owner = this, ShowInTaskbar = false };
        DialogUi.Setup(w, "Khai báo thông số – từng bước", 820, 680);
        var list = new StackPanel { Margin = new Thickness(8, 4, 8, 4) };
        var progress = new TextBlock { FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 6, 8, 4) };
        var rows = new List<(DeclStep S, CheckBox Chk, TextBlock St)>();

        void Refresh()
        {
            foreach (var (s, chk, st) in rows)
            {
                string t = "";
                try { t = s.Status?.Invoke() ?? ""; } catch (Exception) { }
                st.Text = t;
                st.Foreground = t.StartsWith("⚠") ? new SolidColorBrush(Color.FromRgb(190, 90, 0)) : Brushes.DimGray;
                chk.IsChecked = _p.DeclareChecked.Contains(s.Key);
            }
            int done = rows.Count(r => r.Chk.IsChecked == true), warn = rows.Count(r => r.St.Text.StartsWith("⚠"));
            progress.Text = $"Đã kiểm {done}/{rows.Count} mục" + (warn > 0 ? $" · {warn} mục còn thiếu (⚠)" : " · không mục nào thiếu");
        }

        void OpenStep(DeclStep s)
        {
            try { s.Open?.Invoke(); }
            catch (Exception ex) { ShowError("Khai báo – " + s.Name, ex); }
            if (!_p.DeclareChecked.Contains(s.Key)) { _p.DeclareChecked.Add(s.Key); MarkDirty(false); }
            if (_declWin != null && _declWin.IsVisible) Refresh();
        }

        string lastGroup = null; int no = 0;
        foreach (var s in steps)
        {
            if (s.Group != lastGroup)
            {
                lastGroup = s.Group;
                list.Children.Add(new TextBlock { Text = s.Group, FontWeight = FontWeights.Bold, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(11, 79, 156)), Margin = new Thickness(0, 10, 0, 3) });
            }
            no++;
            var g = new Grid { Margin = new Thickness(0, 1, 0, 1), Background = no % 2 == 0 ? new SolidColorBrush(Color.FromRgb(246, 248, 251)) : Brushes.White };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.4, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var step = s;
            var chk = new CheckBox { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, ToolTip = "Đã kiểm thông số này (lưu trong dự án)" };
            chk.Click += (o, e) =>
            {
                if (chk.IsChecked == true) { if (!_p.DeclareChecked.Contains(step.Key)) _p.DeclareChecked.Add(step.Key); }
                else _p.DeclareChecked.Remove(step.Key);
                MarkDirty(false); Refresh();
            };
            var num = new TextBlock { Text = no + ".", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };
            var name = new TextBlock { Text = s.Name, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 8, 4) };
            var st = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, FontSize = 11.5, Margin = new Thickness(0, 4, 8, 4) };
            var btn = new Button { Content = "Mở bảng…", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 2, 2, 2) };
            btn.Click += (o, e) => OpenStep(step);
            Grid.SetColumn(chk, 0); Grid.SetColumn(num, 1); Grid.SetColumn(name, 2); Grid.SetColumn(st, 3); Grid.SetColumn(btn, 4);
            g.Children.Add(chk); g.Children.Add(num); g.Children.Add(name); g.Children.Add(st); g.Children.Add(btn);
            name.MouseLeftButtonUp += (o, e) => OpenStep(step);
            name.Cursor = System.Windows.Input.Cursors.Hand;
            rows.Add((s, chk, st));
            list.Children.Add(g);
        }
        var next = DialogUi.Btn("Mục chưa kiểm tiếp theo ▶", (o, e) =>
        {
            var r = rows.FirstOrDefault(q => q.Chk.IsChecked != true) ;
            if (r.S == null) { progress.Text = "Đã kiểm đủ mọi mục ✓"; return; }
            OpenStep(r.S);
        }, true);
        var reset = DialogUi.Btn("Bỏ dấu đã kiểm", (o, e) => { _p.DeclareChecked.Clear(); MarkDirty(false); Refresh(); });
        var close = DialogUi.Btn("Đóng", (o, e) => w.Close());
        var hint = DialogUi.Hint("Bấm \"Mở bảng…\" (hoặc tên mục) để nhập thông số trong hộp thoại dạng bảng của riêng mục đó; mở xong mục được đánh dấu ✓ Đã kiểm. ⚠ = còn thiếu. Bảng này không chặn cửa sổ chính.");
        hint.TextWrapping = TextWrapping.Wrap; hint.Margin = new Thickness(8, 0, 8, 4);
        var top = new StackPanel(); top.Children.Add(progress); top.Children.Add(hint);
        var dock = new DockPanel();
        DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
        var foot = DialogUi.Row(next, reset, close); foot.Margin = new Thickness(8, 6, 8, 8);
        DockPanel.SetDock(foot, Dock.Bottom); dock.Children.Add(foot);
        dock.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        w.Content = dock;
        w.Activated += (o, e) => Refresh();
        w.Closed += (o, e) => { if (_declWin == w) _declWin = null; };
        _declWin = w;
        Refresh();
        w.Show();
        return w;
    }

    // ------------------------------------------------------------------
    // Nhóm ô nhập ở khung trái → bảng Thông số | Giá trị
    // ------------------------------------------------------------------
    private sealed record FieldRow(string Label, Control Box);

    private List<FieldRow> FieldRows(GroupBox gb)
    {
        var res = new List<FieldRow>();
        var fieldStyle = TryFindResource("Field") as Style;
        foreach (var dp in FindChildren<DockPanel>(gb))
        {
            if (fieldStyle != null && dp.Style != fieldStyle) continue;
            var lbl = dp.Children.OfType<TextBlock>().FirstOrDefault();
            if (lbl == null) continue;
            var box = FindChildren<Control>(dp).FirstOrDefault(c => c is TextBox or ComboBox or CheckBox);
            if (box == null || box.Visibility == Visibility.Collapsed) continue;
            res.Add(new FieldRow(lbl.Text, box));
        }
        return res;
    }

    private static IEnumerable<T> FindChildren<T>(DependencyObject root) where T : DependencyObject
    {
        if (root == null) yield break;
        foreach (var o in LogicalTreeHelper.GetChildren(root))
        {
            if (o is not DependencyObject d) continue;
            if (d is T t) yield return t;
            foreach (var c in FindChildren<T>(d)) yield return c;
        }
    }

    /// <summary>Hộp thoại bảng cho một nhóm ô nhập: cột Thông số | Giá trị; OK → ghi lại vào các ô ở khung trái và vào dự án.</summary>
    internal bool? ShowFieldTable(GroupBox gb, string title, bool modal = true)
    {
        var rows = FieldRows(gb);
        if (rows.Count == 0) return false;
        var w = new Window { Owner = _declWin ?? (Window)this };
        DialogUi.Setup(w, title, 640, Math.Min(760, 170 + 34 * rows.Count));
        var grid = new Grid { Margin = new Thickness(10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        var line = new SolidColorBrush(Color.FromRgb(200, 208, 220));
        void Cell(UIElement el, int r, int c, Brush bg = null)
        {
            var b = new Border { Child = el, BorderBrush = line, BorderThickness = new Thickness(c == 0 ? 1 : 0, r == 0 ? 1 : 0, 1, 1), Padding = new Thickness(6, 3, 6, 3), Background = bg ?? Brushes.White };
            Grid.SetRow(b, r); Grid.SetColumn(b, c); grid.Children.Add(b);
        }
        var head = new SolidColorBrush(Color.FromRgb(228, 235, 245));
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Cell(new TextBlock { Text = "TT", FontWeight = FontWeights.SemiBold }, 0, 0, head);
        Cell(new TextBlock { Text = "Thông số", FontWeight = FontWeights.SemiBold }, 0, 1, head);
        Cell(new TextBlock { Text = "Giá trị", FontWeight = FontWeights.SemiBold }, 0, 2, head);
        var editors = new List<(FieldRow F, Control E)>();
        for (int i = 0; i < rows.Count; i++)
        {
            var f = rows[i];
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var alt = i % 2 == 1 ? new SolidColorBrush(Color.FromRgb(248, 250, 252)) : null;
            Cell(new TextBlock { Text = (i + 1).ToString(), Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center }, i + 1, 0, alt);
            Cell(new TextBlock { Text = f.Label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, ToolTip = f.Box.ToolTip }, i + 1, 1, alt);
            Control ed;
            switch (f.Box)
            {
                case TextBox tb:
                    ed = new TextBox { Text = tb.Text, IsReadOnly = tb.IsReadOnly, ToolTip = tb.ToolTip, VerticalContentAlignment = VerticalAlignment.Center };
                    break;
                case ComboBox cb:
                    var c2 = new ComboBox { IsEditable = cb.IsEditable, ToolTip = cb.ToolTip };
                    foreach (var it in cb.Items) c2.Items.Add(it is ComboBoxItem ci ? ci.Content?.ToString() : it?.ToString());
                    c2.SelectedIndex = cb.SelectedIndex;
                    if (cb.IsEditable) c2.Text = cb.Text;
                    ed = c2;
                    break;
                case CheckBox ck:
                    ed = new CheckBox { IsChecked = ck.IsChecked, Content = ck.Content?.ToString(), VerticalAlignment = VerticalAlignment.Center };
                    break;
                default: continue;
            }
            ed.IsEnabled = f.Box.IsEnabled;
            Cell(ed, i + 1, 2, alt);
            editors.Add((f, ed));
        }
        bool? result = null;
        var ok = DialogUi.Btn("OK", (s, e) =>
        {
            bool heights = false;
            foreach (var (f, ed) in editors)
            {
                switch (f.Box)
                {
                    case TextBox tb when ed is TextBox e2:
                        if (tb.Text != e2.Text) { tb.Text = e2.Text; if (tb == txtHeights || tb == txtBaseElev) heights = true; }
                        break;
                    case ComboBox cb when ed is ComboBox e3:
                        if (e3.SelectedIndex >= 0 && e3.SelectedIndex != cb.SelectedIndex) cb.SelectedIndex = e3.SelectedIndex;
                        else if (cb.IsEditable && cb.Text != e3.Text) cb.Text = e3.Text;
                        break;
                    case CheckBox ck when ed is CheckBox e4:
                        if (ck.IsChecked != e4.IsChecked) ck.IsChecked = e4.IsChecked;
                        break;
                }
            }
            try { ApplyUiToProject(true); } catch (Exception) { }
            if (heights) { try { TxtHeights_LostFocus(txtHeights, new RoutedEventArgs()); } catch (Exception) { } }
            result = true;
            w.Close();
        }, true);
        ok.IsDefault = true;
        var body = new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        w.Content = DialogUi.ScrollBody(body, DialogUi.Row(ok, DialogUi.Btn("Huỷ", (s, e) => { result = false; w.Close(); })));
        if (modal) w.ShowDialog(); else w.Show();
        return result;
    }
}
