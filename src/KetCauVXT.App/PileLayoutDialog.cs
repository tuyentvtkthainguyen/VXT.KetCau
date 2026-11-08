using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using KetCauVXT.Analysis;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Một dòng bảng kiểm tra an toàn móng (DataGrid) – nền màu theo trạng thái.</summary>
internal sealed class SafetyRowView
{
    public SafetyItem I { get; init; }
    public string Group => I.Group;
    public string Name => I.Name;
    public string Value => I.Status == CheckStatus.Info && Math.Abs(I.Value) < 1e-12 ? "–" : DialogUi.F(I.Value, "0.##");
    public string Limit => I.Status == CheckStatus.Info && Math.Abs(I.Limit) < 1e-12 ? "–" : (I.Inverse ? "≥ " : "≤ ") + DialogUi.F(I.Limit, "0.##");
    public string Unit => I.Unit;
    public string Util => I.Status == CheckStatus.Info ? "–" : DialogUi.F(I.Utilisation, "0.00");
    public string Status => I.StatusText;
    public string Explanation => I.Explanation + (string.IsNullOrEmpty(I.Formula) ? "" : "  [" + I.Formula + "]");
    public Brush Back => FoundationSafetyView.BrushOf(I.Status, I.Utilisation);
}

/// <summary>Khối giao diện dùng chung: bảng kiểm tra an toàn + giải thích sự làm việc + cảnh báo của một móng.</summary>
internal static class FoundationSafetyView
{
    private static Brush Fr(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }
    public static readonly Brush OkBrush = Fr(226, 244, 226), HighBrush = Fr(255, 246, 214), WarnBrush = Fr(255, 233, 196), FailBrush = Fr(255, 214, 214), InfoBrush = Fr(238, 242, 250);

    /// <summary>Màu nền: đạt (xanh; ≥ 90% vàng nhạt), cảnh báo (cam), không đạt (đỏ), thông tin (xám xanh).</summary>
    public static Brush BrushOf(CheckStatus st, double util) => st switch
    {
        CheckStatus.Fail => FailBrush, CheckStatus.Warning => WarnBrush, CheckStatus.Info => InfoBrush,
        _ => util >= 0.9 ? HighBrush : OkBrush
    };

    public static DataGrid NewGrid()
    {
        var g = DialogUi.NewGrid();
        g.IsReadOnly = true;
        g.Columns.Add(DialogUi.Text("Nhóm", "Group", true, 90));
        g.Columns.Add(DialogUi.Text("Hạng mục", "Name", true, 260));
        g.Columns.Add(DialogUi.Text("Giá trị", "Value", true, 70));
        g.Columns.Add(DialogUi.Text("Giới hạn", "Limit", true, 80));
        g.Columns.Add(DialogUi.Text("Đơn vị", "Unit", true, 55));
        g.Columns.Add(DialogUi.Text("Hệ số", "Util", true, 50));
        g.Columns.Add(DialogUi.Text("Kết luận", "Status", true, 85));
        g.Columns.Add(DialogUi.Text("Giải thích", "Explanation", true, 520));
        var rs = new Style(typeof(DataGridRow));
        rs.Setters.Add(new Setter(Control.BackgroundProperty, new Binding("Back")));
        g.RowStyle = rs;
        return g;
    }

    public static void Fill(DataGrid g, TextBlock behaviour, TextBlock warnings, FoundationCheckReport r)
    {
        g.ItemsSource = r == null ? null : r.Items.Select(i => new SafetyRowView { I = i }).ToList();
        if (behaviour != null) behaviour.Text = r == null ? "Chưa có bảng kiểm tra – bấm 'Thiết kế móng'." : (string.IsNullOrEmpty(r.Classification) ? "" : r.Classification + ": ") + r.Behaviour;
        if (warnings != null)
        {
            warnings.Text = r == null || r.Warnings.Count == 0 ? "Không có cảnh báo." : string.Join("\n", r.Warnings.Distinct().Select(w => "• " + w));
            warnings.Foreground = r != null && r.Warnings.Any(w => w.StartsWith("LỖI")) ? Brushes.DarkRed : r != null && r.Warnings.Count > 0 ? Brushes.DarkOrange : Brushes.DarkGreen;
        }
    }

    /// <summary>Cửa sổ xem bảng kiểm tra an toàn của một móng bất kỳ.</summary>
    public static Window ShowWindow(Window owner, string title, FoundationCheckReport r, IEnumerable<string> messages = null)
    {
        var w = new Window { Owner = owner };
        DialogUi.Setup(w, title, 1150, 620);
        var root = new DockPanel { Margin = new Thickness(10) };
        var beh = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
        var warn = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
        var g = NewGrid();
        Fill(g, beh, warn, r);
        if (messages != null)
        {
            var extra = messages.Where(m => !(r?.Warnings.Contains(m) ?? false)).ToList();
            if (extra.Count > 0) warn.Text += "\n" + string.Join("\n", extra.Select(m => "• " + m));
        }
        DockPanel.SetDock(beh, Dock.Top);
        root.Children.Add(beh);
        var close = DialogUi.Btn("Đóng", (s, e) => w.Close());
        close.IsCancel = true;
        var bottom = new StackPanel();
        bottom.Children.Add(new ScrollViewer { Content = warn, MaxHeight = 140, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var br = DialogUi.Row(close);
        br.HorizontalAlignment = HorizontalAlignment.Right;
        bottom.Children.Add(br);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(g);
        w.Content = root;
        w.Show();
        return w;
    }
}

/// <summary>Hộp thoại "Bố trí cọc…" cho một đài cọc: danh sách sơ đồ chuẩn (có hình phác), số cọc lưới n×m, khoảng cách cọc, kích thước / chiều cao đài;
/// thiết kế thử trực tiếp (PileCapDesigner.DesignNode với bản sao thông số móng) → bảng kiểm tra an toàn (Pmax/Pmin/[P], chọc thủng, cắt, uốn, nhóm cọc…)
/// tô màu theo hệ số sử dụng + cảnh báo; "Áp dụng" ghi FoundationSettings.CapOverrides (có hoàn tác) rồi thiết kế lại.</summary>
internal sealed class PileLayoutDialog : Window
{
    private readonly Project _p;
    private readonly Func<AnalysisResults> _res;
    private readonly int _node;
    private readonly List<int> _nodes;
    private readonly Func<List<int>, PileCapOverride, Task> _apply;
    private readonly ListBox _lst = new() { Width = 210 };
    private readonly TextBox _tNx, _tNy, _tSx, _tSy, _tL, _tB, _tH;
    private readonly ComboBox _cbRot = new() { ItemsSource = new[] { "Tự chọn theo mô men", "Cạnh dài theo X", "Cạnh dài theo Y" }, SelectedIndex = 0, Width = 160 };
    private readonly CheckBox _chkAll = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly Canvas _plan = new() { Width = 300, Height = 300, Background = Brushes.White, ClipToBounds = true };
    private readonly TextBlock _sum = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
    private readonly TextBlock _beh = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 4) };
    private readonly TextBlock _warn = new() { TextWrapping = TextWrapping.Wrap };
    private readonly DataGrid _grid = FoundationSafetyView.NewGrid();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private bool _loading, _working;

    /// <summary>Kết quả thiết kế thử gần nhất (null nếu lỗi)</summary>
    public PileCapDesign Trial { get; private set; }
    /// <summary>Tóm tắt một dòng của kết quả thử (ghi nhật ký tự kiểm tra)</summary>
    public string Summary => _sum.Text;

    public PileLayoutDialog(Project p, Func<AnalysisResults> res, PileCapDesign cap, Func<List<int>, PileCapOverride, Task> apply,
        IEnumerable<int> extraNodes = null, int focusNode = 0)
    {
        ZoomPan.Wrap(_plan);   // thu phóng / di chuyển hình vẽ – ZoomPan.cs
        _p = p; _res = res; _apply = apply;
        _nodes = cap.NodeIds.ToList();
        // [Gán nhiều móng] các chân cột chọn thêm bằng Ctrl + bấm trên mặt bằng móng
        int nSame = _nodes.Count;
        if (extraNodes != null) foreach (int x in extraNodes) if (!_nodes.Contains(x)) _nodes.Add(x);
        _node = focusNode != 0 && _nodes.Contains(focusNode) ? focusNode : _nodes.FirstOrDefault();
        DialogUi.Setup(this, "Bố trí cọc – " + (string.IsNullOrEmpty(cap.Name) ? "đài cọc" : cap.Name) + " (nút " + _node + ")", 1320, 760);
        Help.Attach(this, "mong.coc");   // nút "?" + F1 – Help.cs

        var cur = PileLayout.Find(p.Foundation, _node) ?? new PileCapOverride { NodeId = _node };
        _tNx = Box(cur.Nx); _tNy = Box(cur.Ny);
        _tSx = Box(cur.SpacingX * 1000); _tSy = Box(cur.SpacingY * 1000);
        _tL = Box(cur.L * 1000); _tB = Box(cur.B * 1000); _tH = Box(cur.H * 1000);
        _cbRot.SelectedIndex = cur.Rotate == null ? 0 : cur.Rotate.Value ? 2 : 1;
        _chkAll.Content = _nodes.Count > nSame
            ? $"Áp dụng cho tất cả {_nodes.Count} vị trí: {nSame} đài giống hệt {cap.Name} + {_nodes.Count - nSame} chân cột chọn thêm (Ctrl + bấm)"
            : $"Áp dụng cho tất cả {_nodes.Count} vị trí đài giống hệt ({cap.Name} ×{Math.Max(cap.Quantity, 1)})";
        _chkAll.IsChecked = true;
        _chkAll.Visibility = _nodes.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        // ---- danh sách sơ đồ có hình phác ----
        _loading = true;
        foreach (var pat in new[] { PilePattern.Auto }.Concat(PileLayout.Standard))
            _lst.Items.Add(new ListBoxItem { Content = PatternItem(pat), Tag = pat, Padding = new Thickness(2) });
        _lst.SelectedIndex = Math.Max(0, _lst.Items.Cast<ListBoxItem>().ToList().FindIndex(i => (PilePattern)i.Tag == cur.Pattern));
        _loading = false;
        _lst.SelectionChanged += (s, e) => { UpdateEnabled(); Schedule(); };

        // ---- thông số ----
        var par = new StackPanel { Margin = new Thickness(10, 0, 10, 0), Width = 320 };
        par.Children.Add(DialogUi.Label("Thông số bố trí (0 = tự chọn)", double.NaN, true));
        par.Children.Add(Line("Lưới n×m: số cọc theo X", _tNx));
        par.Children.Add(Line("Lưới n×m: số cọc theo Y", _tNy));
        par.Children.Add(Line("Khoảng cách tim cọc theo X (mm)", _tSx));
        par.Children.Add(Line("Khoảng cách tim cọc theo Y (mm, lưới)", _tSy));
        par.Children.Add(Line("Hướng sơ đồ", _cbRot));
        par.Children.Add(Line("Cạnh đài theo X – L (mm)", _tL));
        par.Children.Add(Line("Cạnh đài theo Y – B (mm)", _tB));
        par.Children.Add(Line("Chiều cao đài H (mm, 0 = theo chọc thủng)", _tH));
        par.Children.Add(_chkAll);
        double d = p.Foundation.PileSize;
        par.Children.Add(DialogUi.Hint($"Quy định (TCVN 10304:2014 mục 8.13): khoảng cách tim cọc ≥ 3d = {d * 3000:0} mm (cọc ma sát; ≥ 1,5d cọc chống), " +
                                       $"mép cọc – mép đài ≥ 150 mm và tim cọc – mép đài ≥ 0,7d. Khi H cố định mà chọc thủng không đạt, chương trình đề xuất cốt thép chống chọc thủng " +
                                       "(TCVN 5574:2018 mục 8.1.6.3) hoặc yêu cầu tăng chiều cao đài."));
        par.Children.Add(new Border { Child = _plan, BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left });
        foreach (var tb in new[] { _tNx, _tNy, _tSx, _tSy, _tL, _tB, _tH }) tb.TextChanged += (s, e) => Schedule();
        _cbRot.SelectionChanged += (s, e) => Schedule();

        // ---- kết quả ----
        var right = new DockPanel();
        var head = new StackPanel();
        head.Children.Add(_sum);
        head.Children.Add(_beh);
        DockPanel.SetDock(head, Dock.Top);
        right.Children.Add(head);
        var btnApply = DialogUi.Btn("Áp dụng", async (s, e) => await ApplyAsync(), true);
        var btnAuto = DialogUi.Btn("Tự chọn (bỏ bố trí riêng)", async (s, e) => await ApplyAsync(clear: true));
        var btnClose = DialogUi.Btn("Đóng", (s, e) => Close());
        btnClose.IsCancel = true;
        var bottom = new StackPanel();
        bottom.Children.Add(new ScrollViewer { Content = _warn, MaxHeight = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var br = DialogUi.Row(btnApply, btnAuto, btnClose);
        br.HorizontalAlignment = HorizontalAlignment.Right;
        br.Margin = new Thickness(0, 6, 0, 0);
        bottom.Children.Add(br);
        DockPanel.SetDock(bottom, Dock.Bottom);
        right.Children.Add(bottom);
        right.Children.Add(_grid);

        var root = new DockPanel { Margin = new Thickness(10) };
        var left = new DockPanel();
        var lt = DialogUi.Label("Sơ đồ bố trí cọc", double.NaN, true);
        DockPanel.SetDock(lt, Dock.Top);
        left.Children.Add(lt);
        left.Children.Add(_lst);
        DockPanel.SetDock(left, Dock.Left);
        DockPanel.SetDock(par, Dock.Left);
        root.Children.Add(left);
        root.Children.Add(par);
        root.Children.Add(right);
        Content = root;

        _timer.Tick += (s, e) => { _timer.Stop(); Evaluate(); };
        Loaded += (s, e) => { UpdateEnabled(); Evaluate(); };
        Closed += (s, e) => _timer.Stop();
    }

    private static TextBox Box(double v) => new() { Text = DialogUi.F(v, "0"), Width = 80, VerticalContentAlignment = VerticalAlignment.Center };

    private static DockPanel Line(string label, FrameworkElement ctl)
    {
        var d = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
        d.Children.Add(DialogUi.Label(label, 200));
        ctl.HorizontalAlignment = HorizontalAlignment.Left;
        d.Children.Add(ctl);
        return d;
    }

    private static FrameworkElement PatternItem(PilePattern pat)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        var cv = new Canvas { Width = 46, Height = 46, Background = Brushes.White, Margin = new Thickness(0, 0, 6, 0) };
        var pts = pat == PilePattern.Auto ? new List<(double X, double Y)>() : PileLayout.Generate(pat, 1, 1, 3, 2, true);
        var outline = new Shapes.Rectangle { Width = 42, Height = 42, Stroke = Brushes.Gray, StrokeThickness = 1 };
        Canvas.SetLeft(outline, 2); Canvas.SetTop(outline, 2);
        cv.Children.Add(outline);
        if (pts.Count == 0)
            cv.Children.Add(new TextBlock { Text = "AUTO", FontSize = 10, Foreground = Brushes.DimGray, Margin = new Thickness(8, 15, 0, 0) });
        else
        {
            double ext = Math.Max(pts.Max(q => Math.Max(Math.Abs(q.X), Math.Abs(q.Y))), 0.5);
            double sc = 15 / ext;
            foreach (var (x, y) in pts)
            {
                var e = new Shapes.Ellipse { Width = 7, Height = 7, Fill = Brushes.DimGray };
                Canvas.SetLeft(e, 23 + x * sc - 3.5); Canvas.SetTop(e, 23 - y * sc - 3.5);
                cv.Children.Add(e);
            }
        }
        sp.Children.Add(cv);
        sp.Children.Add(new TextBlock { Text = PileLayout.Name(pat), VerticalAlignment = VerticalAlignment.Center });
        return sp;
    }

    private PilePattern Pattern => _lst.SelectedItem is ListBoxItem li && li.Tag is PilePattern pp ? pp : PilePattern.Auto;

    private void UpdateEnabled()
    {
        bool grid = Pattern == PilePattern.Grid;
        _tNx.IsEnabled = grid; _tNy.IsEnabled = grid; _tSy.IsEnabled = grid;
    }

    private void Schedule()
    {
        if (_loading) return;
        _timer.Stop(); _timer.Start();
    }

    /// <summary>Bố trí theo các ô nhập hiện tại.</summary>
    public PileCapOverride Current()
    {
        double N(TextBox t) => Math.Max(0, DialogUi.ParseNum(t.Text, 0));
        return new PileCapOverride
        {
            NodeId = _node, Pattern = Pattern,
            Nx = Math.Clamp((int)Math.Round(N(_tNx)), 1, 12), Ny = Math.Clamp((int)Math.Round(N(_tNy)), 1, 12),
            SpacingX = N(_tSx) / 1000, SpacingY = N(_tSy) / 1000,
            Rotate = _cbRot.SelectedIndex switch { 1 => false, 2 => true, _ => (bool?)null },
            L = N(_tL) / 1000, B = N(_tB) / 1000, H = N(_tH) / 1000
        };
    }

    /// <summary>Chọn sơ đồ (dùng cho tự kiểm tra) và kiểm tra ngay.</summary>
    public void SelectPattern(PilePattern pat)
    {
        int k = _lst.Items.Cast<ListBoxItem>().ToList().FindIndex(i => (PilePattern)i.Tag == pat);
        if (k >= 0) _lst.SelectedIndex = k;
        _timer.Stop();
        Evaluate();
    }

    /// <summary>Thiết kế thử với bản sao thông số móng có bố trí đang chọn, cập nhật bảng kiểm tra và hình phác.</summary>
    public void Evaluate()
    {
        var res = _res();
        if (res == null)
        {
            _sum.Text = "Chưa có kết quả phân tích – chạy phân tích / thiết kế móng trước.";
            FoundationSafetyView.Fill(_grid, _beh, _warn, null);
            return;
        }
        try
        {
            var fs = JsonSerializer.Deserialize<FoundationSettings>(JsonSerializer.Serialize(_p.Foundation, Project.JsonOptions), Project.JsonOptions);
            var o = Current();
            PileLayout.Set(fs, new[] { _node }, PileLayout.IsActive(o) ? o : null);
            Trial = PileCapDesigner.DesignNode(_p, res, _node, fs);
            var t = Trial;
            _sum.Text = $"{t.Piles.Count} cọc {(t.PileRound ? "D" : "")}{t.PileSize * 1000:0}{(t.PileRound ? "" : "×" + (t.PileSize * 1000).ToString("0"))} – đài {t.L * 1000:0}×{t.B * 1000:0}×{t.H * 1000:0} mm; "
                        + $"Pmax = {t.PMax:0.0} / [P] = {t.PileCapacity:0} kN ({(t.PileCapacity > 0 ? t.PMax / t.PileCapacity * 100 : 0):0}%), Pmin = {t.PMin:0.0} kN; thép đáy {t.BarsL} / {t.BarsB} → "
                        + (t.Ok ? "ĐẠT" : "KHÔNG ĐẠT");
            _sum.Foreground = t.Ok ? Brushes.DarkGreen : Brushes.DarkRed;
            FoundationSafetyView.Fill(_grid, _beh, _warn, t.Safety);
            var lErr = t.Messages.Where(m => m.StartsWith("LỖI") && !(t.Safety?.Warnings.Contains(m) ?? false)).ToList();
            if (lErr.Count > 0) _warn.Text = string.Join("\n", lErr.Select(m => "• " + m)) + "\n" + _warn.Text;
            DrawPlan(t);
        }
        catch (Exception ex)
        {
            Trial = null;
            _sum.Text = "Lỗi thiết kế thử: " + ex.Message;
            _sum.Foreground = Brushes.DarkRed;
            FoundationSafetyView.Fill(_grid, _beh, _warn, null);
            _plan.Children.Clear();
        }
    }

    private void DrawPlan(PileCapDesign t)
    {
        _plan.Children.Clear();
        double w = _plan.Width, h = _plan.Height;
        double ext = Math.Max(t.L, t.B) * 1.15;
        if (ext <= 0) return;
        double sc = (Math.Min(w, h) - 30) / ext, cx = w / 2, cy = h / 2;
        Point S(double x, double y) => new(cx + x * sc, cy - y * sc);
        void Rect(double x0, double y0, double x1, double y1, Brush fill, Brush stroke, double th, DoubleCollection dash = null)
        {
            var a = S(x0, y1); var b = S(x1, y0);
            var r = new Shapes.Rectangle { Width = Math.Max(1, b.X - a.X), Height = Math.Max(1, b.Y - a.Y), Fill = fill, Stroke = stroke, StrokeThickness = th };
            if (dash != null) r.StrokeDashArray = dash;
            Canvas.SetLeft(r, a.X); Canvas.SetTop(r, a.Y);
            _plan.Children.Add(r);
        }
        Rect(-t.L / 2, -t.B / 2, t.L / 2, t.B / 2, new SolidColorBrush(Color.FromRgb(236, 236, 230)), t.Ok ? Brushes.DarkGreen : Brushes.DarkRed, 1.6);
        double h0 = t.H0 > 0 ? t.H0 : 0;
        if (t.Piles.Count > 1 && h0 > 0)
            Rect(-t.ColumnH / 2 - h0, -t.ColumnB / 2 - h0, t.ColumnH / 2 + h0, t.ColumnB / 2 + h0, null, Brushes.SteelBlue, 1, new DoubleCollection { 4, 3 });
        Rect(-t.ColumnH / 2, -t.ColumnB / 2, t.ColumnH / 2, t.ColumnB / 2, Brushes.DimGray, Brushes.Black, 1);
        double ds = Math.Max(5, t.PileSize * sc);
        var rows = t.Safety?.Piles;
        double pa = Math.Max(t.PileCapacity, 1);
        for (int i = 0; i < t.Piles.Count; i++)
        {
            var c = S(t.Piles[i].X, t.Piles[i].Y);
            double util = rows != null && i < rows.Count ? rows[i].PMax / pa : 0;
            Brush fill = rows != null && i < rows.Count && rows[i].PMin < 0 ? FoundationSafetyView.WarnBrush
                       : util > 1 + 1e-6 ? FoundationSafetyView.FailBrush : util >= 0.9 ? FoundationSafetyView.HighBrush : FoundationSafetyView.OkBrush;
            Shapes.Shape sh = t.PileRound ? new Shapes.Ellipse { Width = ds, Height = ds } : new Shapes.Rectangle { Width = ds, Height = ds };
            sh.Fill = fill; sh.Stroke = Brushes.Black; sh.StrokeThickness = 1;
            Canvas.SetLeft(sh, c.X - ds / 2); Canvas.SetTop(sh, c.Y - ds / 2);
            _plan.Children.Add(sh);
            var tb = new TextBlock
            {
                Text = "C" + (i + 1) + (rows != null && i < rows.Count ? "\n" + rows[i].PMax.ToString("0") : ""), FontSize = 9.5, Foreground = Brushes.Black
            };
            Canvas.SetLeft(tb, c.X + ds / 2 + 1); Canvas.SetTop(tb, c.Y - ds / 2 - 2);
            _plan.Children.Add(tb);
        }
        PileSetbackUi.DrawOnPlan(_plan, t, S);   // [LùiCọcRanh] ranh giới + giới hạn tim cọc (cọc tự lùi khi thiết kế thử)
        var cap = new TextBlock { Text = "Nét đứt xanh: đáy tháp chọc thủng 45°;  số dưới tên cọc: Pmax (kN)", FontSize = 9.5, Foreground = Brushes.DimGray };
        Canvas.SetLeft(cap, 4); Canvas.SetTop(cap, h - 16);
        _plan.Children.Add(cap);
    }

    /// <summary>Áp dụng bố trí đang chọn (clear = true: bỏ bố trí riêng, trở về tự chọn) rồi thiết kế lại; bảng kiểm tra cập nhật theo kết quả mới.</summary>
    public async Task ApplyAsync(bool clear = false)
    {
        if (_working) return;
        _working = true;
        try
        {
            var o = Current();
            var nodes = _chkAll.IsChecked == true ? _nodes : new List<int> { _node };
            await _apply(nodes, clear || !PileLayout.IsActive(o) ? null : o);
            if (clear)
            {
                _loading = true;
                _lst.SelectedIndex = 0;
                foreach (var tb in new[] { _tSx, _tSy, _tL, _tB, _tH }) tb.Text = "0";
                _loading = false;
            }
            Evaluate();
        }
        finally { _working = false; }
    }
}
