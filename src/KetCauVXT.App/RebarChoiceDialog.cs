using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Hộp thoại "Chọn bố trí thép…": các vùng thép của cấu kiện đang chọn (trái), danh sách phương án thoả As và khoảng hở
/// (giữa, DataGrid), phác mặt cắt phương án (phải). Áp dụng / bỏ chọn ghi vào Project.RebarChoices (có hoàn tác) rồi thiết kế lại.</summary>
internal sealed class RebarChoiceDialog : Window
{
    /// <summary>Một dòng phương án trong bảng.</summary>
    internal sealed class AltRow
    {
        public RebarAlternative A { get; init; }
        public string Text => (A.IsCurrent ? "● " : "") + A.Text;
        public string AsReq => A.AsReq > 0 ? F(A.PerMeter ? A.AsReq * 1e4 : A.Kind == "Dầm" && A.Zone == RebarZones.Stirrup ? A.AsReq : A.AsReq * 1e4) : "–";
        public string AsProv => F(A.PerMeter ? A.AsProv * 1e4 : A.Kind == "Dầm" && A.Zone == RebarZones.Stirrup ? A.AsProv : A.AsProv * 1e4);
        public string Over => A.AsReq > 0 ? F(A.OverPct, "0") : "–";
        public int Layers => A.Layers;
        public string Clear => F(A.ClearMm, "0") + " ≥ " + F(A.ClearReqMm, "0");
        public string Util => A.Utilization > 0 ? F(A.Utilization, "0.00") : "";
        public string Note => A.Note;
        public string Weight => A.WeightKg > 0 ? F(A.WeightKg, "0.0") : "";
        public string PerM2 => A.WeightPerM2 > 0 ? F(A.WeightPerM2, "0.00") : "";
        public string DCur => double.IsNaN(A.DeltaVsCurrentKg) ? "" : (A.DeltaVsCurrentKg > 0 ? "+" : "") + F(A.DeltaVsCurrentKg, "0.0");
        public string DBest => A.WeightKg > 0 ? (A.DeltaVsBestKg > 0 ? "+" : "") + F(A.DeltaVsBestKg, "0.0") : "";
        public string Warning => A.Warning;
        private static string F(double v, string fmt = "0.00") => v.ToString(fmt, CultureInfo.InvariantCulture);
    }

    private readonly Func<MemberDesignBase> _member;
    private readonly Func<MemberDesignBase, List<RebarZoneInfo>> _zones;
    private readonly Func<MemberDesignBase, RebarZoneInfo, List<RebarAlternative>> _alts;
    private readonly Func<RebarZoneInfo, RebarAlternative, Task> _apply;
    private readonly Func<RebarZoneInfo, Task> _clear;

    private readonly ComboBox _cbMember = new() { MinWidth = 260, Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Collapsed };
    private readonly TextBlock _title = new() { FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0, 0, 0, 2) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
    private readonly ListBox _lstZones = new() { MinWidth = 230 };
    private readonly DataGrid _grid = DialogUi.NewGrid();
    private readonly Canvas _sketch = new() { Width = 280, Height = 250, Background = Brushes.White, ClipToBounds = true };
    private readonly TextBlock _sketchNote = new() { TextWrapping = TextWrapping.Wrap, Width = 280, Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Button _btnApply, _btnClear, _btnClearAll;
    private List<RebarZoneInfo> _zoneList = new();
    private bool _working;

    public RebarChoiceDialog(Func<MemberDesignBase> member, Func<MemberDesignBase, List<RebarZoneInfo>> zones,
        Func<MemberDesignBase, RebarZoneInfo, List<RebarAlternative>> alts, Func<RebarZoneInfo, RebarAlternative, Task> apply,
        Func<RebarZoneInfo, Task> clear)
    {
        _member = member; _zones = zones; _alts = alts; _apply = apply; _clear = clear;
        DialogUi.Setup(this, "Chọn bố trí thép", 1450, 640);

        _grid.IsReadOnly = true;
        _grid.SelectionMode = DataGridSelectionMode.Single;
        _grid.Columns.Add(DialogUi.Text("Phương án", nameof(AltRow.Text), true, 150));
        _grid.Columns.Add(DialogUi.Text("As yêu cầu", nameof(AltRow.AsReq), true, 80));
        _grid.Columns.Add(DialogUi.Text("As bố trí", nameof(AltRow.AsProv), true, 80));
        _grid.Columns.Add(DialogUi.Text("% thừa", nameof(AltRow.Over), true, 55));
        _grid.Columns.Add(DialogUi.Text("Số hàng", nameof(AltRow.Layers), true, 55));
        _grid.Columns.Add(DialogUi.Text("Khe hở ≥ yêu cầu (mm)", nameof(AltRow.Clear), true, 130));
        _grid.Columns.Add(DialogUi.Text("Tỷ số", nameof(AltRow.Util), true, 50));
        _grid.Columns.Add(DialogUi.Text("KL (kg)", nameof(AltRow.Weight), true, 65));
        _grid.Columns.Add(DialogUi.Text("kg/m²", nameof(AltRow.PerM2), true, 55));
        _grid.Columns.Add(DialogUi.Text("± hiện tại (kg)", nameof(AltRow.DCur), true, 85));
        _grid.Columns.Add(DialogUi.Text("± tiết kiệm nhất (kg)", nameof(AltRow.DBest), true, 110));
        _grid.Columns.Add(DialogUi.Text("Cảnh báo", nameof(AltRow.Warning), true, 300));
        _grid.Columns.Add(DialogUi.Text("Ghi chú", nameof(AltRow.Note), true, 220));
        // màu cảnh báo: cam = vượt / dư thép, đỏ = thiếu / không đạt, xanh = tiết kiệm
        _grid.LoadingRow += (s, e) =>
        {
            if (e.Row.Item is not AltRow r) return;
            e.Row.Background = r.A.WarnLevel switch
            {
                1 => new SolidColorBrush(Color.FromRgb(255, 236, 204)),
                2 => new SolidColorBrush(Color.FromRgb(246, 200, 200)),
                3 => new SolidColorBrush(Color.FromRgb(214, 240, 214)),
                _ => Brushes.White
            };
            e.Row.Foreground = r.A.WarnLevel == 2 ? Brushes.DarkRed : Brushes.Black;
        };
        _grid.SelectionChanged += (s, e) => DrawSketch();
        _grid.MouseDoubleClick += async (s, e) => await ApplySelected();
        _lstZones.SelectionChanged += (s, e) => LoadAlternatives();

        _btnApply = DialogUi.Btn("Áp dụng phương án", async (s, e) => await ApplySelected(), true);
        _btnClear = DialogUi.Btn("Bỏ chọn vùng này (tự động)", async (s, e) => await ClearZone(false));
        _btnClearAll = DialogUi.Btn("Bỏ chọn cả cấu kiện", async (s, e) => await ClearZone(true));
        var close = DialogUi.Btn("Đóng", (s, e) => Close());
        close.IsCancel = true;

        var left = new DockPanel { Margin = new Thickness(0, 0, 8, 0) };
        var lz = DialogUi.Label("Vùng thép (★ = đang dùng lựa chọn của người dùng)", double.NaN, true);
        DockPanel.SetDock(lz, Dock.Top);
        left.Children.Add(lz);
        left.Children.Add(_lstZones);

        var right = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        right.Children.Add(DialogUi.Label("Mặt cắt phương án", double.NaN, true));
        right.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Child = _sketch });
        right.Children.Add(_sketchNote);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(left, 0); Grid.SetColumn(_grid, 1); Grid.SetColumn(right, 2);
        body.Children.Add(left); body.Children.Add(_grid); body.Children.Add(right);

        var root = new DockPanel { Margin = new Thickness(10) };
        var top = new StackPanel();
        top.Children.Add(_cbMember);
        top.Children.Add(_title);
        top.Children.Add(_status);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var bottom = new StackPanel();
        bottom.Children.Add(DialogUi.Hint("Mọi phương án đều thoả As yêu cầu và khoảng hở TCVN 5574:2018 mục 10.3.5 (dầm: ≥ max(d; 25 mm) thép dưới, ≥ max(d; 30 mm) thép trên; "
                                          + "cột: ≥ max(d; 50 mm), tim ≤ 400 mm). Thép dầm là TỔNG số thanh tại tiết diện, gồm 2 thanh góc chạy suốt. "
                                          + "Sau khi áp dụng chương trình thiết kế lại, kiểm tra khả năng chịu lực với thép chọn và báo \"KHÔNG ĐẠT\" nếu thiếu. Hoàn tác bằng Ctrl+Z. "
                                          + "KL = khối lượng thép của vùng (ước tính để so sánh; phương án sàn: thép sàn cả tầng); cam = vượt / dư thép (As/As,yc > 1,3), đỏ = thiếu, xanh = tiết kiệm."));
        var bs = DialogUi.Row(_btnApply, _btnClear, _btnClearAll, close);
        bs.HorizontalAlignment = HorizontalAlignment.Right;
        bottom.Children.Add(bs);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(body);
        Content = root;
        Loaded += (s, e) => Refresh();
    }

    /// <summary>Danh sách cấu kiện chọn được (vd. móng đơn, đài cọc, móng băng); onPick nhận khoá cấu kiện được chọn.</summary>
    public void SetMemberPicker(IList<(string Label, string Key)> items, string currentKey, Action<string> onPick)
    {
        if (items == null || items.Count < 2) return;
        _cbMember.ItemsSource = items.Select(x => x.Label).ToList();
        int i = items.ToList().FindIndex(x => x.Key == currentKey);
        _cbMember.SelectedIndex = Math.Max(i, 0);
        _cbMember.Visibility = Visibility.Visible;
        _cbMember.SelectionChanged += (s, e) =>
        {
            int k = _cbMember.SelectedIndex;
            if (k < 0 || k >= items.Count) return;
            onPick(items[k].Key);
            _lstZones.SelectedIndex = -1;
            Refresh();
        };
    }

    /// <summary>Số phương án của vùng đang chọn.</summary>
    public int AlternativeCount => _grid.Items.Count;
    public int ZoneCount => _zoneList.Count;

    /// <summary>Chọn vùng đầu tiên có tên vùng zone (tự kiểm tra); trả về false nếu không có.</summary>
    public bool SelectZone(string zone)
    {
        int i = _zoneList.FindIndex(z => z.Zone == zone);
        if (i < 0) return false;
        _lstZones.SelectedIndex = i;
        return true;
    }

    /// <summary>Chọn vùng thứ i (tự kiểm tra).</summary>
    public void SelectZone(int i)
    {
        if (i >= 0 && i < _lstZones.Items.Count) _lstZones.SelectedIndex = i;
    }

    /// <summary>Áp dụng phương án thứ index của vùng đang chọn (tự kiểm tra). Trả về phương án đã áp dụng (null nếu không có).</summary>
    public async Task<RebarAlternative> ApplyAlternative(int index)
    {
        if (index < 0 || index >= _grid.Items.Count) return null;
        _grid.SelectedIndex = index;
        var a = (_grid.SelectedItem as AltRow)?.A;
        await ApplySelected();
        return a;
    }

    /// <summary>Nạp lại tên cấu kiện, vùng thép và phương án (sau khi thiết kế lại).</summary>
    public void Refresh()
    {
        var m = _member();
        int keep = Math.Max(_lstZones.SelectedIndex, 0);
        if (m == null)
        {
            _title.Text = "Không tìm thấy cấu kiện (cần chạy thiết kế)";
            _zoneList = new List<RebarZoneInfo>();
            _lstZones.ItemsSource = null; _grid.ItemsSource = null;
            return;
        }
        string kind = m switch
        {
            BeamLineDesign => "Dầm", ColumnDesign => "Cột", SlabDesign => "Sàn", FootingDesign => "Móng đơn", PileCapDesign => "Đài cọc",
            StripFootingDesign => "Móng băng", _ => ""
        };
        _title.Text = $"{kind} {m.Name}" + (m.Quantity > 1 ? $" (×{m.Quantity})" : "") + (m is BeamLineDesign b ? $" – tiết diện {b.Section}" : "");
        Title = "Chọn bố trí thép – " + m.Name;
        var errs = m.Messages.Where(x => x.StartsWith("LỖI", StringComparison.Ordinal)).Take(3).ToList();
        _status.Text = (m.Ok ? "ĐẠT" : "KHÔNG ĐẠT") + $" – tỷ số lớn nhất {m.MaxRatio.ToString("0.00", CultureInfo.InvariantCulture)}"
                       + (m.UserRebarZones > 0 ? $"; {m.UserRebarZones} vùng dùng thép chọn" : "; thép tự chọn")
                       + (errs.Count > 0 ? "\n" + string.Join("\n", errs) : "");
        _status.Foreground = m.Ok ? Brushes.DarkGreen : Brushes.DarkRed;
        _zoneList = _zones(m) ?? new List<RebarZoneInfo>();
        _lstZones.ItemsSource = null;
        _lstZones.ItemsSource = _zoneList;
        if (_zoneList.Count > 0) _lstZones.SelectedIndex = Math.Min(keep, _zoneList.Count - 1);
        LoadAlternatives();
    }

    private RebarZoneInfo Zone => _lstZones.SelectedItem as RebarZoneInfo;

    private void LoadAlternatives()
    {
        var z = Zone;
        var m = _member();
        if (z == null || m == null) { _grid.ItemsSource = null; DrawSketch(); return; }
        List<RebarAlternative> alts;
        try { alts = _alts(m, z) ?? new List<RebarAlternative>(); }
        catch (Exception ex) { alts = new List<RebarAlternative>(); _status.Text += "\nLỗi lập phương án: " + ex.Message; }
        var rows = alts.Select(a => new AltRow { A = a }).ToList();
        _grid.ItemsSource = rows;
        int cur = rows.FindIndex(r => r.A.IsCurrent);
        _grid.SelectedIndex = rows.Count == 0 ? -1 : Math.Max(cur, 0);
        _sketchNote.Foreground = Brushes.DimGray;
        _btnClear.IsEnabled = z.User;
        _btnApply.IsEnabled = rows.Count > 0;
        string unit = z.Kind is "Sàn" or "Móng" ? "cm²/m" : z.Kind == "Dầm" && z.Zone == RebarZones.Stirrup ? "kN (Q / Qu)" : "cm²";
        _grid.Columns[1].Header = "As yêu cầu (" + unit + ")";
        _grid.Columns[2].Header = "As bố trí (" + unit + ")";
        DrawSketch();
    }

    private async Task ApplySelected()
    {
        if (_working) return;
        var z = Zone;
        var a = (_grid.SelectedItem as AltRow)?.A;
        if (z == null || a == null) return;
        _working = true;
        try { await _apply(z, a); }
        finally { _working = false; }
        Refresh();
    }

    private async Task ClearZone(bool all)
    {
        if (_working) return;
        var z = Zone;
        if (!all && z == null) return;
        _working = true;
        try { await _clear(all ? null : z); }
        finally { _working = false; }
        Refresh();
    }

    // ------------------------------------------------------------------
    // Phác mặt cắt
    // ------------------------------------------------------------------
    private void DrawSketch()
    {
        _sketch.Children.Clear();
        _sketchNote.Text = "";
        var z = Zone;
        var a = (_grid.SelectedItem as AltRow)?.A;
        if (z == null) return;
        double W = _sketch.Width, H = _sketch.Height, pad = 24;
        double bw = Math.Max(z.B, 0.05), bh = Math.Max(z.H, 0.05);
        bool strip = z.Kind is "Sàn" or "Móng";
        if (strip) bh = Math.Max(bh, 0.08);
        double k = Math.Min((W - 2 * pad) / bw, (H - 2 * pad) / bh);
        if (strip) k = Math.Min((W - 2 * pad) / bw, 4 * (H - 2 * pad) / Math.Max(bh * 4, 0.4));
        double ox = (W - bw * k) / 2, oy = (H - bh * k) / 2;
        // toạ độ: x từ trái, y từ dưới (m) → canvas
        (double X, double Y) P(double x, double y) => (ox + x * k, oy + (bh - y) * k);
        var conc = new Rectangle { Width = bw * k, Height = bh * k, Stroke = Brushes.Black, StrokeThickness = 1.5, Fill = new SolidColorBrush(Color.FromRgb(236, 236, 236)) };
        Canvas.SetLeft(conc, ox); Canvas.SetTop(conc, oy);
        _sketch.Children.Add(conc);
        void Dot(double x, double y, int d, Brush br)
        {
            double r = Math.Max(d / 2000.0 * k, 2.2);
            var (cx, cy) = P(x, y);
            var e = new Ellipse { Width = 2 * r, Height = 2 * r, Fill = br };
            Canvas.SetLeft(e, cx - r); Canvas.SetTop(e, cy - r);
            _sketch.Children.Add(e);
        }
        void Label(string t, double x, double y)
        {
            var tb = new TextBlock { Text = t, FontSize = 11, Foreground = Brushes.DarkBlue };
            Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y);
            _sketch.Children.Add(tb);
        }
        double c = z.Cover;
        if (!strip)
        {
            int sd = 8;
            double cs = Math.Max(c - sd / 1000.0, 0.012);
            var (x1, y1) = P(cs, bh - cs);
            var st = new Rectangle { Width = (bw - 2 * cs) * k, Height = (bh - 2 * cs) * k, Stroke = Brushes.ForestGreen, StrokeThickness = 1.2 };
            Canvas.SetLeft(st, x1); Canvas.SetTop(st, y1);
            _sketch.Children.Add(st);
        }
        Label(DialogUi.F(z.B * 1000, "0") + (strip ? " (dải 1 m)" : "") + " × " + DialogUi.F(z.H * 1000, "0"), 4, 2);
        if (a == null) return;

        if (z.Kind == "Dầm" && a.Bars.Count > 0)
        {
            var groups = a.Bars.Select(g => (g.Dia, g.Count)).ToList();
            var fit = RebarOptions.CheckFit(groups, z.B, c, z.Top, 2, 0);
            var bars = groups.SelectMany(g => Enumerable.Repeat(g.Dia, g.Count)).OrderByDescending(x => x).ToList();
            int per = Math.Max(fit.PerLayer, 2);
            double dmax = bars.Count > 0 ? bars[0] / 1000.0 : 0.02;
            for (int layer = 0; layer * per < bars.Count; layer++)
            {
                var row = bars.Skip(layer * per).Take(per).ToList();
                double y = z.Top ? bh - c - dmax / 2 - layer * (dmax + 0.025) : c + dmax / 2 + layer * (dmax + 0.025);
                double xa = c + dmax / 2, xb = z.B - c - dmax / 2;
                var order = BeamOrder(row.Count);
                for (int i = 0; i < row.Count; i++)
                {
                    double x = row.Count == 1 ? (xa + xb) / 2 : xa + (xb - xa) * order[i] / (row.Count - 1);
                    Dot(x, y, row[i], i < 2 && layer == 0 ? Brushes.Firebrick : Brushes.Black);
                }
            }
            _sketchNote.Text = a.Text + (z.Top ? " (thép trên)" : " (thép dưới)") + $" – {fit.Layers} hàng, khe hở {DialogUi.F(fit.ClearMm, "0")} mm ≥ {DialogUi.F(fit.ClearReqMm, "0")} mm. Đỏ = 2 thanh góc chạy suốt.";
        }
        else if (z.Kind == "Cột" && a.NB >= 2 && a.NH >= 2)
        {
            double d = a.Dia / 1000.0, e = c + d / 2;
            for (int i = 0; i < a.NB; i++)
            {
                double x = e + (bw - 2 * e) * i / (a.NB - 1);
                Dot(x, e, a.Dia, Brushes.Black); Dot(x, bh - e, a.Dia, Brushes.Black);
            }
            for (int j = 1; j < a.NH - 1; j++)
            {
                double y = e + (bh - 2 * e) * j / (a.NH - 1);
                Dot(e, y, a.Dia, Brushes.Black); Dot(bw - e, y, a.Dia, Brushes.Black);
            }
            _sketchNote.Text = a.Text + $" – mỗi cạnh b {a.NB} thanh, mỗi cạnh h {a.NH} thanh; khe hở {DialogUi.F(a.ClearMm, "0")} mm.";
        }
        else if (a.Spacing > 0 && strip)
        {
            double y = z.Top ? bh - c - a.Dia / 2000.0 : c + a.Dia / 2000.0;
            int n = (int)Math.Floor(bw / a.Spacing + 1e-9);
            for (int i = 0; i <= n; i++) Dot(Math.Min(i * a.Spacing + 0.02, bw - 0.02), y, a.Dia, Brushes.Black);
            _sketchNote.Text = a.Text + $" – {n + 1} thanh/1 m, As = {DialogUi.F(a.AsProv * 1e4, "0.00")} cm²/m.";
        }
        else if (a.Scheme != null)
        {
            _sketchNote.Text = a.Text + " – thép sàn cả tầng " + DialogUi.F(a.WeightKg, "0.0") + " kg (" + DialogUi.F(a.WeightPerM2, "0.00") + " kg/m²). "
                               + (a.Warning.Length > 0 ? a.Warning : "Phương án tiết kiệm nhất.");
        }
        else if (a.Spacing > 0)
        {
            _sketchNote.Text = "Đai " + a.Text + " (vùng gối / giữa nhịp) – " + a.Note;
        }
        if (a.Warning.Length > 0 && a.Scheme == null) _sketchNote.Text += "\n⚠ " + a.Warning;
    }

    /// <summary>Thứ tự vị trí trong hàng: 2 góc trước, sau đó từ giữa ra (giống BeamDrawing).</summary>
    private static List<int> BeamOrder(int n)
    {
        var o = new List<int>();
        if (n <= 0) return o;
        o.Add(0);
        if (n > 1) o.Add(n - 1);
        o.AddRange(Enumerable.Range(1, Math.Max(0, n - 2)).OrderBy(i => Math.Abs(i - (n - 1) / 2.0)).ThenBy(i => i));
        return o;
    }
}

/// <summary>Hộp thoại tuỳ chọn chung khi tự chọn thép và triển khai gia công (Project.RebarPrefs).</summary>
internal sealed class RebarPrefsDialog : Window
{
    private readonly TextBox _tDias, _tMaxPer, _tStirDia, _tSMin, _tSMax, _tStock, _tReuse;
    private readonly CheckBox _chkSame, _chkTwo, _chkStagger;
    private readonly ComboBox _cbScheme;
    public RebarPreferences Result { get; private set; }

    public RebarPrefsDialog(RebarPreferences p)
    {
        p ??= new RebarPreferences();
        DialogUi.Setup(this, "Tuỳ chọn chọn thép & gia công", 560, 520);
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        TextBox Box(string v) => new() { Text = v, Width = 160 };
        _tDias = Box(string.Join(" ", p.PreferredDias ?? Array.Empty<int>()));
        _tMaxPer = Box(p.MaxBarsPerLayer.ToString(CultureInfo.InvariantCulture));
        _tStirDia = Box(p.StirrupDia.ToString(CultureInfo.InvariantCulture));
        _tSMin = Box(DialogUi.F(p.MinStirrupSpacing * 1000, "0"));
        _tSMax = Box(DialogUi.F(p.MaxStirrupSpacing * 1000, "0"));
        _tStock = Box(DialogUi.F(p.StockLength, "0.0##"));
        _tReuse = Box(DialogUi.F(p.MinReuseOffcut, "0.0##"));
        _chkSame = new CheckBox { Content = "Ưu tiên thép tăng cường / mũ gối cùng Ø thép chạy suốt", IsChecked = p.PreferSameDia };
        _chkTwo = new CheckBox { Content = "Cho phép đặt thép dầm 2 hàng", IsChecked = p.AllowTwoLayers };
        _chkStagger = new CheckBox { Content = "Nối so le (≤ 50% số thanh tại một tiết diện, tim nối cách ≥ 1,3·ll)", IsChecked = p.StaggerSplices };
        _cbScheme = new ComboBox { Width = 200, ItemsSource = Enum.GetValues<SlabRebarScheme>().Select(RebarPreferences.SchemeName).ToList(), SelectedIndex = (int)p.SlabScheme };
        var root = new StackPanel { Margin = new Thickness(10) };
        StackPanel Line(string label, UIElement c) => DialogUi.Row(DialogUi.Label(label, 300), c);
        root.Children.Add(DialogUi.Label("Chọn thép", double.NaN, true));
        root.Children.Add(Line("Ø ưu tiên thép dọc (cách nhau dấu cách; trống = tất cả)", _tDias));
        root.Children.Add(Line("Số thanh tối đa một hàng dầm (0 = theo bề rộng)", _tMaxPer));
        root.Children.Add(_chkSame);
        root.Children.Add(_chkTwo);
        root.Children.Add(Line("Ø đai cố định (0 = tự chọn)", _tStirDia));
        root.Children.Add(Line("Bước đai nhỏ nhất, mm (0 = không giới hạn)", _tSMin));
        root.Children.Add(Line("Bước đai lớn nhất, mm (0 = theo cấu tạo)", _tSMax));
        root.Children.Add(Line("Phương án thép sàn mặc định (từng ô chọn riêng được)", _cbScheme));
        root.Children.Add(DialogUi.Label("Gia công thép (shop drawing)", double.NaN, true));
        root.Children.Add(Line("Chiều dài cây thép thương phẩm, m", _tStock));
        root.Children.Add(Line("Đầu thừa tái sử dụng khi ≥ (m)", _tReuse));
        root.Children.Add(_chkStagger);
        root.Children.Add(DialogUi.Hint("Đổi tuỳ chọn sẽ thiết kế lại BTCT (nếu đã có kết quả phân tích). Lựa chọn thép riêng từng cấu kiện vẫn được giữ."));
        var ok = DialogUi.Btn("Đồng ý", (s, e) => { Result = Read(); DialogResult = true; }, true);
        ok.IsDefault = true;
        var cancel = DialogUi.Btn("Huỷ", (s, e) => DialogResult = false);
        cancel.IsCancel = true;
        var bs = DialogUi.Row(ok, cancel); bs.HorizontalAlignment = HorizontalAlignment.Right;
        root.Children.Add(bs);
        Content = root;
    }

    private RebarPreferences Read()
    {
        var dias = (_tDias.Text ?? "").Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x.Trim().TrimStart('Ø', 'ø'), NumberStyles.Integer, CultureInfo.InvariantCulture, out int d) ? d : 0)
            .Where(d => d >= 6 && d <= 40).Distinct().OrderBy(d => d).ToArray();
        return new RebarPreferences
        {
            PreferredDias = dias,
            MaxBarsPerLayer = Math.Max(0, (int)DialogUi.ParseNum(_tMaxPer.Text, 0)),
            PreferSameDia = _chkSame.IsChecked == true,
            AllowTwoLayers = _chkTwo.IsChecked == true,
            StirrupDia = Math.Max(0, (int)DialogUi.ParseNum(_tStirDia.Text, 0)),
            MinStirrupSpacing = Math.Max(0, DialogUi.ParseNum(_tSMin.Text, 0)) / 1000.0,
            MaxStirrupSpacing = Math.Max(0, DialogUi.ParseNum(_tSMax.Text, 0)) / 1000.0,
            StockLength = Math.Clamp(DialogUi.ParseNum(_tStock.Text, 11.7), 3, 24),
            MinReuseOffcut = Math.Clamp(DialogUi.ParseNum(_tReuse.Text, 1.0), 0, 6),
            StaggerSplices = _chkStagger.IsChecked == true,
            SlabScheme = _cbScheme.SelectedIndex >= 0 ? (SlabRebarScheme)_cbScheme.SelectedIndex : SlabRebarScheme.TopBarsBottomMesh
        };
    }
}
