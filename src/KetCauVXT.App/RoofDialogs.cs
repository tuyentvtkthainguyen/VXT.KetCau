using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>[Hệ mái tôn] Thông số hệ mái / thưng tường trước khi kéo chọn diện (hoặc sửa hệ đã vẽ).</summary>
internal sealed class RoofFramingDialog : Window
{
    private readonly Project _p;
    private readonly RoofFraming _r;
    public bool AutoDirection { get; private set; } = true;

    internal static readonly string[] PurlinSections =
    {
        "C100x50x15x1.8", "C125x50x20x2.0", "C150x50x20x1.8", "C150x50x20x2.0", "C180x50x20x2.0", "C200x50x20x2.0", "C200x50x20x2.5",
        "Z150x62x20x2.0", "Z175x62x20x2.0", "Z200x62x20x2.0", "Z200x72x20x2.5", "Z250x72x20x2.5",
        "□30x60x1.4", "□40x80x1.4", "□40x80x1.8", "□50x100x1.8", "□50x100x2.0",
    };
    internal static readonly string[] ChordSections = { "□40x80x1.4", "□40x80x1.8", "□50x100x1.8", "□60x120x2.0", "□50x50x1.8", "C150x50x20x2.0", "C200x50x20x2.0" };
    internal static readonly string[] WebSections = { "□30x30x1.2", "□30x60x1.2", "□40x40x1.4", "□40x80x1.4", "Ø34x1.6" };
    internal static readonly string[] PostSections = { "□100x100x2.0", "□90x90x1.8", "□75x75x1.8", "Trụ gạch 220x220", "Trụ gạch 220x330", "" };

    public RoofFramingDialog(Project p, RoofFraming r, bool isNew)
    {
        _p = p; _r = r;
        SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        DialogUi.Setup(this, isNew ? "Mái tôn / xà gồ / vì kèo – kéo chọn diện" : $"Sửa hệ mái {r.Name}", 600, 640);
        MinHeight = 0;
        var root = new StackPanel { Margin = new Thickness(12, 10, 12, 12) };

        TextBox Tx(string v, double w = 80) => new TextBox { Text = v, Width = w, Margin = new Thickness(0, 0, 10, 0), VerticalContentAlignment = VerticalAlignment.Center };
        ComboBox Cb(IEnumerable<string> items, string v, double w = 170) { var c = new ComboBox { IsEditable = true, Width = w, Margin = new Thickness(0, 0, 10, 0) }; foreach (var i in items) c.Items.Add(i); c.Text = v; return c; }
        string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        var cbKind = new ComboBox { Width = 260, Margin = new Thickness(0, 0, 10, 0) };
        foreach (RoofFramingKind k in Enum.GetValues(typeof(RoofFramingKind))) cbKind.Items.Add(new ComboBoxItem { Content = RoofFraming.KindText(k), Tag = k });
        cbKind.SelectedIndex = (int)r.Kind;
        cbKind.IsEnabled = isNew;
        var txName = Tx(r.Name, 90);
        root.Children.Add(DialogUi.Row(DialogUi.Label("Loại", 120), cbKind, DialogUi.Label("Tên"), txName));

        var cbStory = new ComboBox { Width = 140, Margin = new Thickness(0, 0, 10, 0) };
        foreach (var s in p.Stories.OrderBy(s => s.Elevation)) cbStory.Items.Add(s.Name);
        cbStory.SelectedItem = r.Story;
        var cbDir = new ComboBox { Width = 200 };
        cbDir.Items.Add("Tự động theo cạnh dài"); cbDir.Items.Add("Song song trục X"); cbDir.Items.Add("Song song trục Y");
        cbDir.SelectedIndex = isNew ? 0 : r.RidgeAlongX ? 1 : 2;
        var lbDir = DialogUi.Label("Phương nóc");
        root.Children.Add(DialogUi.Row(DialogUi.Label("Tầng đặt", 120), cbStory, lbDir, cbDir));

        // ---- tôn ----
        var cbProf = new ComboBox { Width = 260, Margin = new Thickness(0, 0, 10, 0) };
        void FillProf() { cbProf.Items.Clear(); foreach (var pf in p.SheetProfiles) cbProf.Items.Add(pf.Name); cbProf.SelectedItem = r.Profile; if (cbProf.SelectedIndex < 0 && cbProf.Items.Count > 0) cbProf.SelectedIndex = 0; }
        FillProf();
        var prev = new Canvas { Width = 540, Height = 54, Background = new SolidColorBrush(Color.FromRgb(248, 248, 244)), Margin = new Thickness(0, 4, 0, 4) };
        var btnProf = DialogUi.Btn("Profile tôn…", (s, e) =>
        {
            var d = new SheetProfileDialog(p) { Owner = this };
            if (d.ShowDialog() == true) { string cur = cbProf.SelectedItem as string; FillProf(); cbProf.SelectedItem = cur; if (cbProf.SelectedIndex < 0 && cbProf.Items.Count > 0) cbProf.SelectedIndex = 0; }
        });
        root.Children.Add(DialogUi.Row(DialogUi.Label("Profile tôn", 120), cbProf, btnProf));
        root.Children.Add(prev);
        void DrawPrev() { var pf = p.SheetProfiles.FirstOrDefault(q => q.Name == cbProf.SelectedItem as string); if (pf != null) SheetProfileDialog.DrawSection(prev, pf, 2); }
        cbProf.SelectionChanged += (s, e) => DrawPrev();

        // ---- hình học ----
        var txSlope = Tx(F(r.SlopePercent), 60); var chkTwo = new CheckBox { Content = "Hai dốc", IsChecked = r.TwoSlopes, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        var chkLow = new CheckBox { Content = "Một dốc: đầu thấp ở cạnh nhỏ (X0 / Y0)", IsChecked = r.LowAtStart, VerticalAlignment = VerticalAlignment.Center };
        var rowSlope = DialogUi.Row(DialogUi.Label("Độ dốc (%)", 120), txSlope, chkTwo, chkLow);
        root.Children.Add(rowSlope);
        var txBase = Tx(F(r.BaseOffset), 60); var txTop = Tx(F(r.TopOffset), 60); var txOver = Tx(F(r.Overhang), 60);
        var lbBase = DialogUi.Label("Chân mái (m)", 120); var lbTop = DialogUi.Label("Đỉnh thưng (m)"); var lbOver = DialogUi.Label("Đua mái (m)");
        root.Children.Add(DialogUi.Row(lbBase, txBase, lbTop, txTop, lbOver, txOver));
        root.Children.Add(DialogUi.Hint("Cao độ tính từ mặt sàn tầng đặt. Chống nóng: chân mái = chiều cao trụ đỡ trên sàn mái. Tiền chế: chân mái = đỉnh cột khung."));

        // ---- xà gồ ----
        var cbPur = Cb(PurlinSections, r.PurlinSection); var txPurS = Tx(F(r.PurlinSpacing), 60);
        root.Children.Add(DialogUi.Row(DialogUi.Label("Xà gồ", 120), cbPur, DialogUi.Label("Bước xà gồ (m)"), txPurS));
        var lbPurHint = DialogUi.Hint("");
        root.Children.Add(lbPurHint);

        // ---- vì kèo / cột sườn ----
        var chkTr = new CheckBox { Content = "Có vì kèo", IsChecked = r.IncludeTrusses, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        var txTrS = Tx(F(r.TrussSpacing), 60);
        var lbTrS = DialogUi.Label("Bước vì kèo = nhịp xà gồ (m)");
        root.Children.Add(DialogUi.Row(DialogUi.Label("", 120), chkTr, lbTrS, txTrS));
        var cbCh = Cb(ChordSections, r.ChordSection, 130); var cbWeb = Cb(WebSections, r.WebSection, 120);
        var lbCh = DialogUi.Label("Cánh vì kèo", 120); var lbWeb = DialogUi.Label("Thanh bụng");
        var rowTr = DialogUi.Row(lbCh, cbCh, lbWeb, cbWeb);
        root.Children.Add(rowTr);
        var cbPost = Cb(PostSections, r.PostSection, 170); var txLive = Tx(F(r.LiveLoad), 50);
        var lbPost = DialogUi.Label("Trụ đỡ chân vì kèo", 120);
        var rowPost = DialogUi.Row(lbPost, cbPost, DialogUi.Label("Hoạt tải mái (kN/m²)"), txLive);
        root.Children.Add(rowPost);

        void Mode()
        {
            var k = (RoofFramingKind)((cbKind.SelectedItem as ComboBoxItem)?.Tag ?? RoofFramingKind.ChongNong);
            bool wall = k == RoofFramingKind.ThungTuong;
            rowSlope.Visibility = wall ? Visibility.Collapsed : Visibility.Visible;
            txTop.IsEnabled = wall; lbTop.IsEnabled = wall; txOver.IsEnabled = !wall;
            lbBase.Text = wall ? "Chân thưng (m)" : "Chân mái (m)";
            lbDir.Text = wall ? "Phương tường" : "Phương nóc";
            chkTr.Content = wall ? "Có cột sườn tường" : "Có vì kèo";
            lbTrS.Text = wall ? "Bước cột sườn / khung = nhịp xà gồ (m)" : "Bước vì kèo / khung = nhịp xà gồ (m)";
            lbCh.Text = wall ? "Cột sườn" : "Cánh vì kèo";
            lbWeb.Visibility = cbWeb.Visibility = wall ? Visibility.Collapsed : Visibility.Visible;
            rowPost.Visibility = Visibility.Visible;
            lbPost.Visibility = cbPost.Visibility = k == RoofFramingKind.ChongNong ? Visibility.Visible : Visibility.Collapsed;
            var pf = p.SheetProfiles.FirstOrDefault(q => q.Name == cbProf.SelectedItem as string);
            lbPurHint.Text = wall ? "Xà gồ tường (đà tôn) đặt nằm ngang, bước theo phương đứng; kiểm tra với gió 0.83 kN/m² (tham khảo)."
                                  : $"Bước xà gồ đo theo mặt dốc{(pf != null ? $"; {pf.Name} khuyến nghị ≤ {pf.MaxPurlinSpacing:0.##} m" : "")}. Kiểm tra xà gồ: dầm đơn giản nhịp = bước vì kèo.";
        }
        cbKind.SelectionChanged += (s, e) =>
        {
            if (!isNew) { Mode(); return; }
            var k = (RoofFramingKind)((cbKind.SelectedItem as ComboBoxItem)?.Tag ?? RoofFramingKind.ChongNong);
            var d = RoofFramingBuilder.Defaults(k, p, cbStory.SelectedItem as string ?? r.Story);
            txSlope.Text = F(d.SlopePercent); chkTwo.IsChecked = d.TwoSlopes; txBase.Text = F(d.BaseOffset); txTop.Text = F(d.TopOffset); txOver.Text = F(d.Overhang);
            cbPur.Text = d.PurlinSection; txPurS.Text = F(d.PurlinSpacing); chkTr.IsChecked = d.IncludeTrusses; txTrS.Text = F(d.TrussSpacing);
            cbCh.Text = d.ChordSection; cbWeb.Text = d.WebSection; cbPost.Text = d.PostSection; cbProf.SelectedItem = d.Profile;
            Mode();
        };
        cbProf.SelectionChanged += (s, e) => Mode();
        Mode();
        Loaded += (s, e) => DrawPrev();

        var info = DialogUi.Hint(isNew ? "Bấm OK rồi KÉO KHUNG trên mặt bằng: mái – khung bao diện mái; thưng tường – khung bao đoạn tường (tường chạy theo cạnh dài). Góc khung bắt vào trục." : "");
        info.TextWrapping = TextWrapping.Wrap;
        root.Children.Add(info);
        if (!isNew)
        {
            try { var g = RoofFramingBuilder.Build(p, r); info.Text = string.Join("\n", g.Checks); } catch (Exception) { }
        }
        double N(TextBox t, double d) => DialogUi.ParseNum(t.Text, d);
        var ok = DialogUi.Btn("OK", (s, e) =>
        {
            _r.Kind = (RoofFramingKind)((cbKind.SelectedItem as ComboBoxItem)?.Tag ?? RoofFramingKind.ChongNong);
            _r.Name = txName.Text.Trim();
            _r.Story = cbStory.SelectedItem as string ?? _r.Story;
            AutoDirection = cbDir.SelectedIndex == 0;
            if (cbDir.SelectedIndex > 0) _r.RidgeAlongX = cbDir.SelectedIndex == 1;
            _r.Profile = cbProf.SelectedItem as string ?? _r.Profile;
            _r.SlopePercent = Math.Clamp(N(txSlope, _r.SlopePercent), 0, 100);
            _r.TwoSlopes = chkTwo.IsChecked == true; _r.LowAtStart = chkLow.IsChecked == true;
            _r.BaseOffset = N(txBase, _r.BaseOffset); _r.TopOffset = N(txTop, _r.TopOffset); _r.Overhang = Math.Max(0, N(txOver, _r.Overhang));
            _r.PurlinSection = cbPur.Text.Trim(); _r.PurlinSpacing = Math.Max(0.3, N(txPurS, _r.PurlinSpacing));
            _r.IncludeTrusses = chkTr.IsChecked == true; _r.TrussSpacing = Math.Max(0.5, N(txTrS, _r.TrussSpacing));
            _r.ChordSection = cbCh.Text.Trim(); _r.WebSection = cbWeb.Text.Trim(); _r.PostSection = cbPost.Text.Trim();
            _r.LiveLoad = Math.Max(0, N(txLive, _r.LiveLoad));
            if (ThinSection.Parse(_r.PurlinSection).Wx <= 0) { info.Text = $"Không đọc được tiết diện xà gồ \"{_r.PurlinSection}\" – dạng C150x50x20x2.0, Z200x62x20x2.0 hoặc □40x80x1.4."; return; }
            DialogResult = true;
        }, true);
        ok.IsDefault = true;
        root.Children.Add(DialogUi.Row(ok, DialogUi.Btn("Huỷ", (s, e) => DialogResult = false)));
        Content = root;
    }
}

/// <summary>[Hệ mái tôn] Thư viện profile tôn của dự án: sửa bước sóng, cao sóng, khổ hữu dụng, dày, trọng lượng; xem mặt cắt sóng.</summary>
internal sealed class SheetProfileDialog : Window
{
    public SheetProfileDialog(Project p)
    {
        DialogUi.Setup(this, "Profile mái tôn", 860, 560);
        var list = new ObservableCollection<SheetProfile>(p.SheetProfiles.Select(q => q.Clone()));
        var g = DialogUi.NewGrid();
        g.ItemsSource = list;
        g.CanUserAddRows = true;
        g.Columns.Add(DialogUi.Text("Tên profile", "Name", false, 230));
        g.Columns.Add(DialogUi.Combo("Dạng sóng", "Shape", new[] { "Sóng vuông", "Sóng tròn", "Cliplock", "Panel cách nhiệt" }, 120));
        g.Columns.Add(DialogUi.Num("Bước sóng (m)", "RibPitch"));
        g.Columns.Add(DialogUi.Num("Cao sóng (m)", "RibHeight"));
        g.Columns.Add(DialogUi.Num("Đỉnh sóng (m)", "RibTop"));
        g.Columns.Add(DialogUi.Num("Khổ hữu dụng (m)", "CoverWidth"));
        g.Columns.Add(DialogUi.Num("Dày (mm)", "Thickness"));
        g.Columns.Add(DialogUi.Num("kg/m²", "Weight"));
        g.Columns.Add(DialogUi.Num("Bước xà gồ max (m)", "MaxPurlinSpacing"));
        g.Height = 260;
        DialogUi.HScroll(g);
        var prev = new Canvas { Height = 120, Background = new SolidColorBrush(Color.FromRgb(248, 248, 244)), Margin = new Thickness(0, 6, 0, 6) };
        var lb = DialogUi.Hint("Chọn một dòng để xem mặt cắt sóng (một khổ tôn). Thêm dòng cuối để tạo profile mới.");
        void Draw() { if (g.SelectedItem is SheetProfile pf) { lb.Text = $"{pf.Name}: bước sóng {pf.RibPitch * 1000:0} mm, khổ hữu dụng {pf.CoverWidth:0.###} m, cao sóng {pf.RibHeight * 1000:0} mm, {pf.Weight:0.#} kg/m²"; DrawSection(prev, pf, 1); } }
        g.SelectionChanged += (s, e) => Draw();
        g.CellEditEnding += (s, e) => Dispatcher.BeginInvoke(new Action(Draw));
        prev.SizeChanged += (s, e) => Draw();
        var body = new StackPanel { Margin = new Thickness(10) };
        body.Children.Add(g); body.Children.Add(lb); body.Children.Add(prev);
        var ok = DialogUi.Btn("OK", (s, e) =>
        {
            DialogUi.Commit(g);
            var res = list.Where(q => q != null && !string.IsNullOrWhiteSpace(q.Name) && q.RibPitch > 0).GroupBy(q => q.Name.Trim()).Select(q => q.First()).ToList();
            if (res.Count == 0) { lb.Text = "Cần ít nhất một profile."; return; }
            p.SheetProfiles = res;
            DialogResult = true;
        }, true);
        var btnReset = DialogUi.Btn("Thêm profile mặc định", (s, e) => { foreach (var d in SheetProfile.Defaults()) if (list.All(q => q?.Name != d.Name)) list.Add(d); });
        Content = DialogUi.ScrollBody(body, DialogUi.Row(ok, DialogUi.Btn("Huỷ", (s, e) => DialogResult = false), btnReset));
        Loaded += (s, e) => { if (list.Count > 0) g.SelectedIndex = 0; };
    }

    /// <summary>Vẽ mặt cắt sóng: khổ tôn (×repeat) phóng vừa khung, phóng đại chiều cao để thấy rõ.</summary>
    internal static void DrawSection(Canvas cv, SheetProfile pf, int repeat)
    {
        cv.Children.Clear();
        double W = cv.ActualWidth > 10 ? cv.ActualWidth : (double.IsNaN(cv.Width) ? 500 : cv.Width), H = cv.ActualHeight > 10 ? cv.ActualHeight : cv.Height;
        double w = pf.CoverWidth * Math.Max(1, repeat);
        var pts = pf.Section(w);
        if (pts.Count < 2) return;
        double sx = (W - 20) / w;
        double minY = pts.Min(q => q.Y), maxY = pts.Max(q => q.Y);
        double sy = Math.Min(sx * 3, (H - 22) / Math.Max(1e-3, maxY - minY));
        var pl = new Shapes.Polyline { Stroke = new SolidColorBrush(Color.FromRgb(180, 50, 40)), StrokeThickness = 1.6 };
        foreach (var (x, y) in pts) pl.Points.Add(new Point(10 + x * sx, H - 12 - (y - minY) * sy));
        cv.Children.Add(pl);
        var t = new TextBlock { Text = $"{pf.Name} – bước sóng {pf.RibPitch * 1000:0} mm, cao {pf.RibHeight * 1000:0} mm (chiều cao phóng đại)", FontSize = 10.5, Foreground = Brushes.DimGray };
        Canvas.SetLeft(t, 10); Canvas.SetTop(t, 2);
        cv.Children.Add(t);
    }
}
