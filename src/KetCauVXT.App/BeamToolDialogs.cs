using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Hộp thoại của công cụ dầm kiểu ETABS (MainWindow.BeamTools.cs): dầm phụ, dầm theo trục/toạ độ/công xôn, sao chép –
/// đối xứng, điểm chèn, chọn tầng, và các hộp thoại nhỏ của cửa sổ khai báo tiết diện. Dựng bằng mã (không XAML).</summary>
internal static class BeamToolDialogs
{
    private const string EdgeRef = "(mép ô)";

    private static Window Make(Window owner, string title, double width)
    {
        var w = new Window { Owner = owner, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
        DialogUi.Setup(w, title, width, 300);
        w.MinHeight = 0;
        return w;
    }

    private static TextBox Box(string text, double width = 70, string tip = null) =>
        new() { Text = text ?? "", Width = width, Margin = new Thickness(0, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = tip };

    private static ComboBox Combo(IEnumerable<string> items, string sel, double width = 90, bool editable = false)
    {
        var list = items.ToList();
        var c = new ComboBox { ItemsSource = list, Width = width, Margin = new Thickness(0, 0, 6, 0), IsEditable = editable };
        if (editable) c.Text = sel ?? list.FirstOrDefault() ?? "";
        else c.SelectedItem = sel != null && list.Contains(sel) ? sel : list.FirstOrDefault();
        return c;
    }

    private static string F(double v, string fmt = "0.###") => v.ToString(fmt, CultureInfo.InvariantCulture);
    private static double Mm(TextBox tb) => DialogUi.ParseNum(tb.Text, double.NaN) / 1000.0;

    /// <summary>Nút Đồng ý / Huỷ; onOk trả về true thì đóng hộp thoại (DialogResult = true).</summary>
    private static StackPanel OkCancel(Window w, Func<bool> onOk, string okText = "Đồng ý")
    {
        var ok = DialogUi.Btn(okText, (s, e) => { if (onOk()) w.DialogResult = true; }, true);
        ok.IsDefault = true;
        var cancel = DialogUi.Btn("Huỷ", (s, e) => w.DialogResult = false);
        cancel.IsCancel = true;
        var row = DialogUi.Row(ok, cancel);
        row.HorizontalAlignment = HorizontalAlignment.Right;
        row.Margin = new Thickness(0, 8, 0, 0);
        return row;
    }

    private static TextBlock Error() => new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };

    /// <summary>Tách danh sách số "1200, 2400; 3600" (mm) → m. Dấu thập phân: dấu chấm.</summary>
    public static List<double> ParseList(string text, out string error)
    {
        error = null;
        var res = new List<double>();
        foreach (var t in (text ?? "").Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!BeamTools.TryLength(t, CoordUnit.Mm, out double v) || v < 0) { error = $"Không đọc được khoảng cách \"{t}\"."; return new List<double>(); }
            res.Add(v);
        }
        return res;
    }

    // ==================================================================
    // Dầm phụ
    // ==================================================================
    public static SecondaryBeamOptions AskSecondary(Window owner, Project p, (double X0, double Y0, double X1, double Y1) bay, SecondaryBeamOptions init,
                                                    IList<string> sections, string section)
    {
        var w = Make(owner, "Dầm phụ theo số lượng / khoảng cách (Quick Draw Secondary Beams)", 520);
        var o = new SecondaryBeamOptions
        {
            AlongX = init?.AlongX ?? (bay.X1 - bay.X0 < bay.Y1 - bay.Y0), Count = Math.Max(1, init?.Count ?? 1), Spacing = init?.Spacing ?? 0,
            StartOffset = init?.StartOffset ?? 0, FromEnd = init?.FromEnd ?? false, Distances = init?.Distances?.ToList() ?? new List<double>(),
            RefGrid = init?.RefGrid ?? "", Section = section, SplitSlabs = init?.SplitSlabs ?? true
        };
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(DialogUi.Label($"Ô: X {F(bay.X0, "0.00")} … {F(bay.X1, "0.00")} m, Y {F(bay.Y0, "0.00")} … {F(bay.Y1, "0.00")} m  ({F(bay.X1 - bay.X0, "0.00")} × {F(bay.Y1 - bay.Y0, "0.00")} m)", double.NaN, true));
        var rbX = new RadioButton { Content = "Song song trục X (dầm ngang, khoảng cách đo theo Y)", GroupName = "sbdir", IsChecked = o.AlongX, Margin = new Thickness(0, 4, 0, 0) };
        var rbY = new RadioButton { Content = "Song song trục Y (dầm dọc, khoảng cách đo theo X)", GroupName = "sbdir", IsChecked = !o.AlongX };
        sp.Children.Add(rbX); sp.Children.Add(rbY);

        var rbN = new RadioButton { Content = "Chia đều – số dầm n:", GroupName = "sbm", Width = 230, VerticalAlignment = VerticalAlignment.Center };
        var tbN = Box(o.Count.ToString(CultureInfo.InvariantCulture), 50);
        var rbS = new RadioButton { Content = "Cách đều – khoảng cách s (mm):", GroupName = "sbm", Width = 230, VerticalAlignment = VerticalAlignment.Center };
        var tbS = Box(o.Spacing > 0 ? F(o.Spacing * 1000, "0") : "1200", 60);
        var tbS0 = Box(o.StartOffset > 0 ? F(o.StartOffset * 1000, "0") : "0", 50, "Khoảng cách dầm đầu tiên tới mốc (mm); 0 = bằng s");
        var rbD = new RadioButton { Content = "Danh sách khoảng cách tới mốc (mm):", GroupName = "sbm", Width = 230, VerticalAlignment = VerticalAlignment.Center };
        var tbD = Box(o.Distances.Count > 0 ? string.Join(", ", o.Distances.Select(d => F(d * 1000, "0"))) : "1200, 2400", 170, "Vd. 1200, 2400 – khoảng cách từ trục mốc (hoặc mép ô) tới từng dầm phụ");
        if (o.Distances.Count > 0) rbD.IsChecked = true; else if (o.Spacing > 0) rbS.IsChecked = true; else rbN.IsChecked = true;
        sp.Children.Add(new Separator { Margin = new Thickness(0, 6, 0, 4) });
        sp.Children.Add(DialogUi.Row(rbN, tbN));
        sp.Children.Add(DialogUi.Row(rbS, tbS, DialogUi.Label("khoảng đầu:"), tbS0));
        sp.Children.Add(DialogUi.Row(rbD, tbD));
        var cbRef = new ComboBox { Width = 110, Margin = new Thickness(0, 0, 6, 0) };
        var chkEnd = new CheckBox { Content = "Đo từ mép cuối ô (đảo chiều)", IsChecked = o.FromEnd, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Mốc đo (từ trục):", 120), cbRef, chkEnd));
        var cbSec = Combo(sections, section, 110);
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Tiết diện dầm phụ:", 120), cbSec));
        var chkSplit = new CheckBox { Content = "Chia lại ô sàn theo dầm phụ (tải sàn truyền vào dầm phụ như ETABS)", IsChecked = o.SplitSlabs, Margin = new Thickness(0, 4, 0, 0) };
        sp.Children.Add(chkSplit);
        var preview = new TextBlock { Foreground = Brushes.DarkSlateBlue, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(preview);
        var err = Error();
        sp.Children.Add(err);
        List<double> pos = new();

        void FillRef()
        {
            bool ax = rbX.IsChecked == true;
            var names = new List<string> { EdgeRef };
            names.AddRange((ax ? p.GridsY : p.GridsX).Select(g => g.Name));
            string cur = cbRef.SelectedItem as string ?? (string.IsNullOrEmpty(o.RefGrid) ? EdgeRef : o.RefGrid);
            cbRef.ItemsSource = names;
            cbRef.SelectedItem = names.Contains(cur) ? cur : EdgeRef;
        }
        bool Read()
        {
            err.Text = "";
            o.AlongX = rbX.IsChecked == true;
            o.Count = 0; o.Spacing = 0; o.Distances = new List<double>();
            if (rbN.IsChecked == true)
            {
                if (!int.TryParse(tbN.Text.Trim(), out int n) || n < 1 || n > 50) { err.Text = "Số dầm n phải từ 1 đến 50."; return false; }
                o.Count = n;
            }
            else if (rbS.IsChecked == true)
            {
                double s = Mm(tbS);
                if (double.IsNaN(s) || s < 0.2) { err.Text = "Khoảng cách s phải ≥ 200 mm."; return false; }
                o.Spacing = s;
                double s0 = string.IsNullOrWhiteSpace(tbS0.Text) ? 0 : Mm(tbS0);
                o.StartOffset = double.IsNaN(s0) || s0 < 0 ? 0 : s0;
            }
            else
            {
                o.Distances = ParseList(tbD.Text, out string e);
                if (e != null || o.Distances.Count == 0) { err.Text = e ?? "Nhập ít nhất một khoảng cách."; return false; }
            }
            o.FromEnd = chkEnd.IsChecked == true;
            o.RefGrid = cbRef.SelectedItem is string r && r != EdgeRef ? r : "";
            o.Section = cbSec.SelectedItem as string ?? "";
            o.SplitSlabs = chkSplit.IsChecked == true;
            double? rc = string.IsNullOrEmpty(o.RefGrid) ? null : BeamTools.FindGrid(p, o.RefGrid)?.Coord;
            double a = o.AlongX ? bay.Y0 : bay.X0, b = o.AlongX ? bay.Y1 : bay.X1;
            pos = BeamTools.SecondaryPositions(a, b, o, rc);
            preview.Text = pos.Count == 0 ? "Không có vị trí dầm phụ nào nằm trong ô."
                : $"{pos.Count} dầm phụ {(o.AlongX ? "y" : "x")} = {string.Join("; ", pos.Select(v => F(v, "0.000")))} m  (cách mép ô {string.Join("; ", pos.Select(v => F((v - a) * 1000, "0")))} mm)";
            return pos.Count > 0;
        }
        FillRef(); Read();
        rbX.Checked += (s, e) => { FillRef(); Read(); };
        rbY.Checked += (s, e) => { FillRef(); Read(); };
        foreach (var rb in new[] { rbN, rbS, rbD }) rb.Checked += (s, e) => Read();
        foreach (var tb in new[] { tbN, tbS, tbS0, tbD }) tb.TextChanged += (s, e) => Read();
        cbRef.SelectionChanged += (s, e) => Read();
        chkEnd.Checked += (s, e) => Read(); chkEnd.Unchecked += (s, e) => Read();
        sp.Children.Add(OkCancel(w, () =>
        {
            if (!Read()) { if (err.Text.Length == 0) err.Text = "Không có vị trí dầm phụ nào nằm trong ô."; return false; }
            if (string.IsNullOrEmpty(o.Section)) { err.Text = "Chọn tiết diện dầm phụ."; return false; }
            return true;
        }, "Thêm dầm phụ"));
        w.Content = sp;
        return w.ShowDialog() == true ? o : null;
    }

    // ==================================================================
    // Dầm theo trục + khoảng cách / toạ độ / công xôn
    // ==================================================================
    /// <summary>tab: 0 = theo trục + khoảng cách, 1 = toạ độ, 2 = công xôn. Trả về thao tác (project, tầng) → EditResult; null = huỷ.</summary>
    public static Func<Project, string, EditResult> AskBeamByPosition(Window owner, Project p, IList<string> sections, string section, CoordUnit unit, int tab,
                                                                      (double X, double Y)? start)
    {
        var w = Make(owner, "Thêm dầm theo vị trí / khoảng cách (như ETABS)", 560);
        string un = unit == CoordUnit.Mm ? "mm" : "m";
        var tabs = new TabControl { Margin = new Thickness(0, 0, 0, 4) };
        var allGrids = p.Grids.Select(g => g.Name).ToList();

        // --- tab 1: trục + khoảng cách ---
        var t1 = new StackPanel { Margin = new Thickness(8) };
        var cbRef = Combo(allGrids, p.GridsX.Skip(1).FirstOrDefault()?.Name ?? allGrids.FirstOrDefault(), 70);
        var tbOff = Box("1200", 70, "Khoảng cách từ trục chuẩn (mm), âm = về phía toạ độ nhỏ");
        var cbFrom = new ComboBox { Width = 70, Margin = new Thickness(0, 0, 6, 0) };
        var tbFromOff = Box("0", 60, "Kéo dài (+) / rút ngắn (−) đầu dầm so với trục (mm)");
        var cbTo = new ComboBox { Width = 70, Margin = new Thickness(0, 0, 6, 0) };
        var tbToOff = Box("0", 60);
        t1.Children.Add(DialogUi.Row(DialogUi.Label("Song song trục:", 130), cbRef, DialogUi.Label("cách (mm):"), tbOff));
        t1.Children.Add(DialogUi.Row(DialogUi.Label("Từ trục:", 130), cbFrom, DialogUi.Label("± (mm):"), tbFromOff));
        t1.Children.Add(DialogUi.Row(DialogUi.Label("Đến trục:", 130), cbTo, DialogUi.Label("± (mm):"), tbToOff));
        t1.Children.Add(DialogUi.Hint("Vd. dầm song song trục 2, cách trục 2 là +1200 mm, chạy từ trục A tới trục C (dầm phụ, dầm đỡ tường)."));
        void FillLimits()
        {
            var g = BeamTools.FindGrid(p, cbRef.SelectedItem as string);
            var perp = (g == null || g.Dir == GridDir.X ? p.GridsY : p.GridsX).Select(q => q.Name).ToList();
            cbFrom.ItemsSource = perp; cbTo.ItemsSource = perp;
            cbFrom.SelectedItem = perp.FirstOrDefault(); cbTo.SelectedItem = perp.Count > 1 ? perp[1] : perp.FirstOrDefault();
        }
        FillLimits();
        cbRef.SelectionChanged += (s, e) => FillLimits();
        tabs.Items.Add(new TabItem { Header = "Theo trục + khoảng cách", Content = t1 });

        // --- tab 2: toạ độ ---
        var t2 = new StackPanel { Margin = new Thickness(8) };
        var tbP1 = Box(start != null ? $"{F(unit == CoordUnit.Mm ? start.Value.X * 1000 : start.Value.X)},{F(unit == CoordUnit.Mm ? start.Value.Y * 1000 : start.Value.Y)}" : "0,0", 160);
        var tbP2 = Box(unit == CoordUnit.Mm ? "@3000,0" : "@3,0", 160);
        t2.Children.Add(DialogUi.Row(DialogUi.Label($"Điểm đầu x,y ({un}):", 150), tbP1));
        t2.Children.Add(DialogUi.Row(DialogUi.Label($"Điểm cuối ({un}):", 150), tbP2));
        t2.Children.Add(DialogUi.Hint("Điểm cuối nhận \"x,y\" tuyệt đối, \"@dx,dy\" tương đối điểm đầu, \"L<góc\" toạ độ cực (góc độ, ngược chiều kim đồng hồ từ trục X). Số kèm mm/cm/m theo đơn vị đó; phẩy thập phân thì ngăn hai số bằng \";\"."));
        tabs.Items.Add(new TabItem { Header = "Toạ độ tuyệt đối / tương đối", Content = t2 });

        // --- tab 3: công xôn ---
        var t3 = new StackPanel { Margin = new Thickness(8) };
        var tbC0 = Box(start != null ? $"{F(unit == CoordUnit.Mm ? start.Value.X * 1000 : start.Value.X)},{F(unit == CoordUnit.Mm ? start.Value.Y * 1000 : start.Value.Y)}" : "0,0", 160);
        var tbCL = Box("1200", 70, "Độ vươn công xôn (mm)");
        var cbAng = Combo(new[] { "0", "90", "180", "270" }, "90", 70, true);
        t3.Children.Add(DialogUi.Row(DialogUi.Label($"Điểm gốc x,y ({un}):", 150), tbC0));
        t3.Children.Add(DialogUi.Row(DialogUi.Label("Độ vươn L (mm):", 150), tbCL));
        var dirs = new WrapPanel();
        foreach (var (lab, ang) in new[] { ("→ +X (0°)", "0"), ("↑ +Y (90°)", "90"), ("← −X (180°)", "180"), ("↓ −Y (270°)", "270") })
        {
            string a = ang;
            dirs.Children.Add(DialogUi.Btn(lab, (s, e) => cbAng.Text = a));
        }
        t3.Children.Add(DialogUi.Row(DialogUi.Label("Hướng (độ):", 150), cbAng, dirs));
        t3.Children.Add(DialogUi.Hint("Dầm công xôn (ban công, ô văng): điểm gốc nên là nút có cột / nằm trên dầm. Đầu tự do không có gối."));
        tabs.Items.Add(new TabItem { Header = "Dầm công xôn", Content = t3 });
        tabs.SelectedIndex = Math.Clamp(tab, 0, 2);

        var cbSec = Combo(sections, section, 110);
        var err = Error();
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(tabs);
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Tiết diện:", 70), cbSec));
        sp.Children.Add(err);
        Func<Project, string, EditResult> op = null;
        sp.Children.Add(OkCancel(w, () =>
        {
            err.Text = "";
            string sec = cbSec.SelectedItem as string;
            if (string.IsNullOrEmpty(sec)) { err.Text = "Chọn tiết diện."; return false; }
            switch (tabs.SelectedIndex)
            {
                case 0:
                {
                    string g = cbRef.SelectedItem as string, gf = cbFrom.SelectedItem as string, gt = cbTo.SelectedItem as string;
                    double off = Mm(tbOff), fo = string.IsNullOrWhiteSpace(tbFromOff.Text) ? 0 : Mm(tbFromOff), to = string.IsNullOrWhiteSpace(tbToOff.Text) ? 0 : Mm(tbToOff);
                    if (g == null || gf == null || gt == null) { err.Text = "Chọn đủ trục chuẩn, trục đầu và trục cuối."; return false; }
                    if (double.IsNaN(off) || double.IsNaN(fo) || double.IsNaN(to)) { err.Text = "Khoảng cách không hợp lệ."; return false; }
                    if (gf == gt && Math.Abs(fo - to) < 1e-6) { err.Text = "Trục đầu và trục cuối trùng nhau."; return false; }
                    op = (pp, st) => BeamTools.BeamByGridOffset(pp, st, g, off, gf, gt, sec, fo, to);
                    return true;
                }
                case 1:
                {
                    var a = BeamTools.ParseCoordinateInput(tbP1.Text, null, unit);
                    if (!a.Ok) { err.Text = "Điểm đầu: " + a.Error; return false; }
                    var b = BeamTools.ParseCoordinateInput(tbP2.Text, (a.X, a.Y), unit);
                    if (!b.Ok) { err.Text = "Điểm cuối: " + b.Error; return false; }
                    op = (pp, st) => BeamTools.BeamByCoordinates(pp, st, a.X, a.Y, b.X, b.Y, sec);
                    return true;
                }
                default:
                {
                    var a = BeamTools.ParseCoordinateInput(tbC0.Text, null, unit);
                    if (!a.Ok) { err.Text = "Điểm gốc: " + a.Error; return false; }
                    double L = Mm(tbCL);
                    if (double.IsNaN(L) || L < 0.1) { err.Text = "Độ vươn phải ≥ 100 mm."; return false; }
                    if (!double.TryParse((cbAng.Text ?? "").Replace(',', '.').Replace("°", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double ang)) { err.Text = "Góc không hợp lệ."; return false; }
                    op = (pp, st) => BeamTools.Cantilever(pp, st, (a.X, a.Y), L, ang, sec);
                    return true;
                }
            }
        }, "Thêm dầm"));
        w.Content = sp;
        return w.ShowDialog() == true ? op : null;
    }

    // ==================================================================
    // Sao chép / đối xứng / sang tầng khác
    // ==================================================================
    public static Func<Project, List<int>, EditResult> AskReplicate(Window owner, Project p, int count, string currentStory)
    {
        var w = Make(owner, $"Sao chép / đối xứng {count} dầm đã chọn (Replicate)", 480);
        var tabs = new TabControl();
        var t1 = new StackPanel { Margin = new Thickness(8) };
        var tbDx = Box("3000", 70); var tbDy = Box("0", 70); var tbN = Box("1", 50);
        t1.Children.Add(DialogUi.Row(DialogUi.Label("Δx (mm):", 80), tbDx, DialogUi.Label("Δy (mm):"), tbDy));
        t1.Children.Add(DialogUi.Row(DialogUi.Label("Số bản sao:", 80), tbN));
        t1.Children.Add(DialogUi.Hint("Bản sao thứ k dời k·(Δx, Δy) trên cùng tầng của dầm gốc; đoạn đã có dầm được bỏ qua."));
        tabs.Items.Add(new TabItem { Header = "Tịnh tiến", Content = t1 });

        var t2 = new StackPanel { Margin = new Thickness(8) };
        var cbAxis = Combo(p.Grids.Select(g => g.Name), p.GridsX.Skip(1).FirstOrDefault()?.Name, 80);
        var chkKeep = new CheckBox { Content = "Giữ dầm gốc (bỏ chọn = lật dầm sang phía đối xứng)", IsChecked = true, Margin = new Thickness(0, 4, 0, 0) };
        t2.Children.Add(DialogUi.Row(DialogUi.Label("Đối xứng qua trục:", 130), cbAxis));
        t2.Children.Add(chkKeep);
        tabs.Items.Add(new TabItem { Header = "Đối xứng qua trục", Content = t2 });

        var t3 = new StackPanel { Margin = new Thickness(8) };
        var checks = new List<CheckBox>();
        var similar = BeamTools.StoriesSimilar(p, currentStory);
        foreach (var s in p.Stories.OrderBy(q => q.Elevation))
        {
            var c = new CheckBox { Content = $"{s.Name}  (+{F(s.Elevation, "0.000")})" + (similar.Contains(s.Name) && s.Name != currentStory ? "  – tương tự" : ""), Tag = s.Name, IsEnabled = s.Name != currentStory, Margin = new Thickness(0, 1, 0, 1) };
            checks.Add(c); t3.Children.Add(c);
        }
        t3.Children.Add(DialogUi.Row(DialogUi.Btn("Chọn tầng tương tự", (s, e) => { foreach (var c in checks) c.IsChecked = c.IsEnabled && similar.Contains((string)c.Tag); }),
                                     DialogUi.Btn("Tất cả", (s, e) => { foreach (var c in checks) c.IsChecked = c.IsEnabled; }),
                                     DialogUi.Btn("Bỏ chọn", (s, e) => { foreach (var c in checks) c.IsChecked = false; })));
        t3.Children.Add(DialogUi.Hint("Chép dầm đã chọn sang đúng vị trí trên các tầng đánh dấu."));
        tabs.Items.Add(new TabItem { Header = "Sang tầng khác", Content = new ScrollViewer { Content = t3, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });

        var err = Error();
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(tabs); sp.Children.Add(err);
        Func<Project, List<int>, EditResult> op = null;
        sp.Children.Add(OkCancel(w, () =>
        {
            err.Text = "";
            switch (tabs.SelectedIndex)
            {
                case 0:
                {
                    double dx = Mm(tbDx), dy = Mm(tbDy);
                    if (double.IsNaN(dx)) dx = 0;
                    if (double.IsNaN(dy)) dy = 0;
                    if (!int.TryParse(tbN.Text.Trim(), out int n) || n < 1 || n > 100) { err.Text = "Số bản sao phải từ 1 đến 100."; return false; }
                    if (Math.Abs(dx) + Math.Abs(dy) < 1e-6) { err.Text = "Nhập Δx hoặc Δy khác 0."; return false; }
                    op = (pp, ids) => BeamTools.Replicate(pp, ids, dx, dy, n);
                    return true;
                }
                case 1:
                {
                    string ax = cbAxis.SelectedItem as string;
                    if (ax == null) { err.Text = "Chọn trục đối xứng."; return false; }
                    bool keep = chkKeep.IsChecked == true;
                    op = (pp, ids) => BeamTools.Mirror(pp, ids, BeamTools.FindGrid(pp, ax), keep);
                    return true;
                }
                default:
                {
                    var sts = checks.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();
                    if (sts.Count == 0) { err.Text = "Đánh dấu ít nhất một tầng đích."; return false; }
                    op = (pp, ids) => BeamTools.Replicate(pp, ids, 0, 0, 1, sts);
                    return true;
                }
            }
        }, "Thực hiện"));
        w.Content = sp;
        return w.ShowDialog() == true ? op : null;
    }

    // ==================================================================
    // Điểm chèn
    // ==================================================================
    public static (BeamInsertion Insertion, double Offset)? AskInsertion(Window owner, int count, BeamInsertion cur, double offset, double width)
    {
        var w = Make(owner, $"Điểm chèn / dời ngang cho {count} dầm", 470);
        var kinds = Enum.GetValues(typeof(BeamInsertion)).Cast<BeamInsertion>().ToList();
        var cb = new ComboBox { ItemsSource = kinds.Select(BeamTools.InsertionName).ToList(), SelectedIndex = kinds.IndexOf(cur), Width = 210, Margin = new Thickness(0, 0, 6, 0) };
        var tb = Box(F(offset * 1000, "0"), 70, "Dời tim dầm vuông góc trục dầm (mm), dương = sang trái hướng I→J");
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Điểm chèn (cardinal point):", 170), cb));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Dời ngang thêm (mm):", 170), tb));
        sp.Children.Add(DialogUi.Row(
            DialogUi.Btn("Tim trùng trục", (s, e) => { cb.SelectedIndex = kinds.IndexOf(BeamInsertion.Centroid); tb.Text = "0"; }),
            DialogUi.Btn("Mép trái trùng trục", (s, e) => { cb.SelectedIndex = kinds.IndexOf(BeamInsertion.MiddleLeft); tb.Text = "0"; }),
            DialogUi.Btn("Mép phải trùng trục", (s, e) => { cb.SelectedIndex = kinds.IndexOf(BeamInsertion.MiddleRight); tb.Text = "0"; })));
        sp.Children.Add(DialogUi.Hint($"Mép trái/phải theo hướng I→J nhìn từ trên xuống: tim dầm lệch ∓b/2 (b = {F(width * 1000, "0")} mm). "
                                      + "Chỉ ảnh hưởng mặt bằng, bản vẽ và xuất ETABS (CARDINALPT); phân tích của chương trình vẫn lấy trục dầm qua hai nút."));
        var err = Error(); sp.Children.Add(err);
        (BeamInsertion, double)? res = null;
        sp.Children.Add(OkCancel(w, () =>
        {
            double off = string.IsNullOrWhiteSpace(tb.Text) ? 0 : Mm(tb);
            if (double.IsNaN(off) || Math.Abs(off) > 2) { err.Text = "Dời ngang phải trong khoảng ±2000 mm."; return false; }
            if (cb.SelectedIndex < 0) { err.Text = "Chọn điểm chèn."; return false; }
            res = (kinds[cb.SelectedIndex], off);
            return true;
        }));
        w.Content = sp;
        return w.ShowDialog() == true ? res : null;
    }

    // ==================================================================
    // Chọn tầng (phạm vi tầng)
    // ==================================================================
    public static List<string> AskStories(Window owner, Project p, string current, IEnumerable<string> ticked)
    {
        var w = Make(owner, "Tầng áp dụng cho công cụ vẽ dầm", 380);
        var similar = current != null && p.Stories.Any(s => s.Name == current) ? BeamTools.StoriesSimilar(p, current) : new List<string>();
        var tk = ticked?.ToList() ?? new List<string>();
        if (tk.Count == 0) tk = similar.ToList();
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(DialogUi.Hint("Đánh dấu các tầng có mặt bằng giống nhau (dùng cho phạm vi \"Các tầng tương tự\" và \"Tầng đánh dấu\"). "
                                      + "Tầng có cùng dầm – cột với tầng đang xem được ghi \"– tương tự\"."));
        var checks = new List<CheckBox>();
        var list = new StackPanel();
        foreach (var s in p.Stories.OrderByDescending(q => q.Elevation))
        {
            var c = new CheckBox { Content = $"{s.Name}  (+{F(s.Elevation, "0.000")})" + (s.Name == current ? "  – đang xem" : similar.Contains(s.Name) ? "  – tương tự" : ""), Tag = s.Name, IsChecked = tk.Contains(s.Name), Margin = new Thickness(0, 1, 0, 1) };
            checks.Add(c); list.Children.Add(c);
        }
        sp.Children.Add(new ScrollViewer { Content = list, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        sp.Children.Add(DialogUi.Row(DialogUi.Btn("Tự nhận tầng tương tự", (s, e) => { foreach (var c in checks) c.IsChecked = similar.Contains((string)c.Tag); }),
                                     DialogUi.Btn("Tất cả", (s, e) => { foreach (var c in checks) c.IsChecked = true; })));
        List<string> res = null;
        var err = Error(); sp.Children.Add(err);
        sp.Children.Add(OkCancel(w, () =>
        {
            res = checks.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();
            if (res.Count == 0) { err.Text = "Đánh dấu ít nhất một tầng."; return false; }
            return true;
        }));
        w.Content = sp;
        return w.ShowDialog() == true ? res : null;
    }

    // ==================================================================
    // Hộp thoại nhỏ cho cửa sổ khai báo tiết diện
    // ==================================================================
    public static (string Name, double B, double H, string Material)? AskNewSection(Window owner, Project p, string prefix, string material)
    {
        var w = Make(owner, prefix == "C" ? "Thêm tiết diện cột chữ nhật BTCT" : "Thêm tiết diện dầm chữ nhật BTCT", 420);
        var tbB = Box("220", 70); var tbH = Box(prefix == "C" ? "300" : "400", 70);
        var tbName = Box("", 120, "Để trống = tự đặt theo kích thước (vd. D22x40)");
        var mats = Model.Material.ConcreteClasses.Concat(p.Materials.Select(m => m.Name)).Distinct().ToList();
        var cbMat = Combo(mats, material, 90);
        var lbAuto = new TextBlock { Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center };
        void Upd()
        {
            double b = DialogUi.ParseNum(tbB.Text, double.NaN), h = DialogUi.ParseNum(tbH.Text, double.NaN);
            lbAuto.Text = double.IsNaN(b) || double.IsNaN(h) ? "" : "tên tự động: " + SectionLibrary.AutoName(prefix, b / 1000, h / 1000);
        }
        tbB.TextChanged += (s, e) => Upd(); tbH.TextChanged += (s, e) => Upd(); Upd();
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(DialogUi.Row(DialogUi.Label("b (mm):", 90), tbB, DialogUi.Label("h (mm):"), tbH));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Tên:", 90), tbName, lbAuto));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Bê tông:", 90), cbMat));
        var err = Error(); sp.Children.Add(err);
        (string, double, double, string)? res = null;
        sp.Children.Add(OkCancel(w, () =>
        {
            double b = DialogUi.ParseNum(tbB.Text, double.NaN), h = DialogUi.ParseNum(tbH.Text, double.NaN);
            if (double.IsNaN(b) || double.IsNaN(h) || b < 50 || h < 50 || b > 3000 || h > 3000) { err.Text = "b, h phải từ 50 đến 3000 mm."; return false; }
            string n = tbName.Text.Trim();
            if (n.Length > 0 && p.FrameSections.Any(s => s.Name == n)) { err.Text = $"Đã có tiết diện tên {n}."; return false; }
            res = (n, b, h, cbMat.SelectedItem as string ?? material);
            return true;
        }, "Thêm"));
        w.Content = sp;
        return w.ShowDialog() == true ? res : null;
    }

    public static string AskReassign(Window owner, Project p, string name, int used)
    {
        var w = Make(owner, "Xoá tiết diện đang được sử dụng", 420);
        var cur = p.FrameSections.FirstOrDefault(s => s.Name == name);
        var others = p.FrameSections.Where(s => s.Name != name && (cur == null || s.Shape == cur.Shape)).Select(s => s.Name).ToList();
        if (others.Count == 0) others = p.FrameSections.Where(s => s.Name != name).Select(s => s.Name).ToList();
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(DialogUi.Label($"Tiết diện {name} đang được {used} thanh sử dụng. Gán các thanh này sang tiết diện:"));
        var cb = Combo(others, others.FirstOrDefault(), 140);
        sp.Children.Add(DialogUi.Row(cb));
        string res = null;
        sp.Children.Add(OkCancel(w, () => { res = cb.SelectedItem as string; return res != null; }, "Gán lại và xoá"));
        w.Content = sp;
        return w.ShowDialog() == true ? res : null;
    }

    public static List<string> AskImport(Window owner, Project src, Project dst)
    {
        var w = Make(owner, $"Nhập tiết diện từ \"{src.Name}\"", 440);
        var sp = new StackPanel { Margin = new Thickness(10) };
        var checks = new List<CheckBox>();
        var list = new StackPanel();
        foreach (var s in src.FrameSections)
        {
            bool exists = dst.FrameSections.Any(q => q.Name == s.Name);
            string dims = s.Shape == SectionShape.RectConcrete ? $"{F(s.B * 1000, "0")}×{F(s.H * 1000, "0")} mm" : s.Shape.ToString();
            var c = new CheckBox { Content = $"{s.Name}  ({dims}, {s.Material})" + (exists ? "  – đã có, bỏ qua" : ""), Tag = s.Name, IsChecked = !exists, IsEnabled = !exists, Margin = new Thickness(0, 1, 0, 1) };
            checks.Add(c); list.Children.Add(c);
        }
        sp.Children.Add(new ScrollViewer { Content = list, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        List<string> res = null;
        var err = Error(); sp.Children.Add(err);
        sp.Children.Add(OkCancel(w, () =>
        {
            res = checks.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();
            if (res.Count == 0) { err.Text = "Chưa đánh dấu tiết diện nào."; return false; }
            return true;
        }, "Nhập"));
        w.Content = sp;
        return w.ShowDialog() == true ? res : null;
    }

    private static readonly string[] Palette =
    {
        "#1E5AC8", "#0E8A8A", "#2E9E3E", "#8BC34A", "#E0A800", "#F57C00", "#D32F2F", "#C2185B", "#7B1FA2", "#5D4037", "#455A64", "#000000"
    };

    /// <summary>Chọn màu hiển thị: trả về "#RRGGBB", "" = mặc định, null = huỷ.</summary>
    public static string AskColor(Window owner, string cur)
    {
        var w = Make(owner, "Màu hiển thị tiết diện", 360);
        var sp = new StackPanel { Margin = new Thickness(10) };
        var tb = Box(cur ?? "", 90, "Mã màu #RRGGBB; để trống = màu mặc định");
        var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        foreach (var hex in Palette)
        {
            string h = hex;
            var b = new Button { Width = 24, Height = 20, Margin = new Thickness(0, 0, 4, 4), ToolTip = h, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(h)) };
            b.Click += (s, e) => tb.Text = h;
            wrap.Children.Add(b);
        }
        sp.Children.Add(wrap);
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Mã màu:", 60), tb, DialogUi.Btn("Mặc định", (s, e) => tb.Text = "")));
        var err = Error(); sp.Children.Add(err);
        string res = null;
        sp.Children.Add(OkCancel(w, () =>
        {
            var t = tb.Text.Trim();
            if (t.Length > 0)
            {
                try { ColorConverter.ConvertFromString(t); }
                catch (Exception) { err.Text = "Mã màu không hợp lệ (dạng #RRGGBB)."; return false; }
            }
            res = t;
            return true;
        }));
        w.Content = sp;
        return w.ShowDialog() == true ? res : null;
    }
}
