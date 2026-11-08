using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Detailing;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// HỘP THOẠI "LƯỚI TRỤC + LƯỚI CỘT + TẦNG" – một bảng cho: bước trục X/Y, tên trục, bố trí cột tại giao điểm
// (bấm giao điểm bật/tắt, bấm bong bóng trục đảo cả trục), bảng tầng (tên, chiều cao, cao độ tự tính).
// Lõi: Model/GridColumnSetup.cs. Gọi: MainWindow.GridColumn.cs.
// =====================================================================
internal sealed class GridColumnDialog : Window
{
    public sealed class StoryVm : INotifyPropertyChanged
    {
        private string _name = ""; private double _h = 3.3, _el;
        public string Name { get => _name; set { _name = value; On(); } }
        public double Height { get => _h; set { _h = value; On(); } }
        public double Elevation { get => _el; set { _el = value; On(); } }
        public event PropertyChangedEventHandler PropertyChanged;
        private void On([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    /// <summary>Một dòng bảng trục: tên, khoảng cách tới trục trước (dòng đầu = 0), toạ độ tự tính.</summary>
    public sealed class AxisVm : INotifyPropertyChanged
    {
        private string _name = ""; private double _sp, _c;
        public string Name { get => _name; set { _name = value; On(); } }
        public double Spacing { get => _sp; set { _sp = value; On(); } }
        public double Coord { get => _c; set { _c = value; On(); } }
        public event PropertyChangedEventHandler PropertyChanged;
        private void On([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public GridColumnPlan Plan { get; private set; }
    /// <summary>true = chỉ đổi chiều cao tầng, giữ mô hình; false = tạo lại khung từ lưới.</summary>
    public bool HeightsOnly { get; private set; }

    private readonly bool _hasModel;
    private readonly int _modelStories;
    private readonly string _origGridKey;
    private readonly TextBox _tBase;
    private readonly ObservableCollection<AxisVm> _ax = new(), _ay = new();
    private readonly DataGrid _gx, _gy;
    private readonly ObservableCollection<StoryVm> _rows = new();
    private readonly DataGrid _grid;
    private readonly Canvas _cv;
    private readonly TextBlock _info;
    private readonly RadioButton _rbRebuild, _rbHeights;
    private bool _recalc;

    public GridColumnDialog(GridColumnPlan plan, bool hasModel)
    {
        Plan = plan;
        _hasModel = hasModel;
        _modelStories = plan.Stories.Count;
        DialogUi.Setup(this, "Lưới trục + lưới cột + chiều cao tầng", 900, 640);
        ResizeMode = ResizeMode.CanResize;
        MaxHeight = Math.Max(420, SystemParameters.WorkArea.Height - 24);

        FillAxes(_ax, plan.Names(GridDir.X), plan.SpansX);
        FillAxes(_ay, plan.Names(GridDir.Y), plan.SpansY);
        _tBase = Box(DialogUi.F(plan.BaseElevation), 70);
        _origGridKey = GridKey();
        _tBase.LostFocus += (s, e) => Recalc();

        // ---------- cột trái: lưới trục + bảng tầng ----------
        var left = new StackPanel { Width = 360, Margin = new Thickness(0, 0, 10, 0) };
        _gx = AxisGrid(_ax, GridDir.X);
        _gy = AxisGrid(_ay, GridDir.Y);
        left.Children.Add(new GroupBox { Header = "Trục X (trục đứng trên mặt bằng: 1, 2, 3…)", Content = AxisPanel(_gx, _ax, GridDir.X), Padding = new Thickness(4) });
        left.Children.Add(new GroupBox { Header = "Trục Y (trục ngang trên mặt bằng: A, B, C…)", Content = AxisPanel(_gy, _ay, GridDir.Y), Padding = new Thickness(4), Margin = new Thickness(0, 6, 0, 0) });
        var gSt = new StackPanel();
        gSt.Children.Add(Line("Cao độ chân cột (m)", _tBase));
        _grid = DialogUi.NewGrid();
        _grid.ItemsSource = _rows;
        _grid.Height = 210;
        _grid.Columns.Add(DialogUi.Text("Tên tầng (sàn trên)", "Name", hasModel, 120));
        _grid.Columns.Add(DialogUi.Num("Chiều cao (m)", "Height", "0.###", false, 90));
        _grid.Columns.Add(DialogUi.Num("Cao độ sàn (m)", "Elevation", "+0.000;-0.000", true, 95));
        _grid.CellEditEnding += (s, e) => Dispatcher.BeginInvoke(new Action(Recalc));
        gSt.Children.Add(_grid);
        var tAll = Box("3.3", 50);
        gSt.Children.Add(DialogUi.Row(
            DialogUi.Btn("Thêm tầng trên", (s, e) => { DialogUi.Commit(_grid); _rows.Add(new StoryVm { Name = NewStoryName(), Height = _rows.Count > 0 ? _rows[^1].Height : 3.3 }); Recalc(); }),
            DialogUi.Btn("Xoá tầng chọn", (s, e) => { DialogUi.Commit(_grid); if (_grid.SelectedItem is StoryVm v && _rows.Count > 1) { _rows.Remove(v); Recalc(); } })));
        gSt.Children.Add(DialogUi.Row(DialogUi.Label("Đặt mọi tầng trên tầng 1 cao"), tAll, DialogUi.Label(" m "),
            DialogUi.Btn("Áp dụng", (s, e) => { DialogUi.Commit(_grid); double h = DialogUi.ParseNum(tAll.Text, 0); if (h >= 0.5) { for (int k = 1; k < _rows.Count; k++) _rows[k].Height = h; Recalc(); } })));
        gSt.Children.Add(DialogUi.Hint("Tầng 1: từ chân cột đến sàn tầng 2. Sửa chiều cao trong bảng – cao độ tự tính lại."));
        left.Children.Add(new GroupBox { Header = "Tầng – chiều cao tầng", Content = gSt, Padding = new Thickness(4), Margin = new Thickness(0, 6, 0, 0) });
        foreach (var r in plan.Stories) _rows.Add(new StoryVm { Name = r.Name, Height = r.Height });

        _rbRebuild = new RadioButton { Content = "Tạo lại khung từ lưới (xoá mô hình hiện có)", Margin = new Thickness(0, 2, 0, 2) };
        _rbHeights = new RadioButton { Content = "Chỉ đổi chiều cao tầng – giữ nguyên mô hình", Margin = new Thickness(0, 2, 0, 2) };
        var gMode = new StackPanel();
        gMode.Children.Add(_rbRebuild); gMode.Children.Add(_rbHeights);
        gMode.Children.Add(DialogUi.Hint("Giữ mô hình: cột, dầm, sàn, tường, ống điện nước dời theo cao độ mới. Đổi lưới trục / lưới cột / số tầng thì phải tạo lại khung."));
        left.Children.Add(new GroupBox { Header = "Áp dụng", Content = gMode, Padding = new Thickness(4), Margin = new Thickness(0, 6, 0, 0) });
        if (hasModel) _rbHeights.IsChecked = true; else { _rbRebuild.IsChecked = true; _rbHeights.IsEnabled = false; }

        // ---------- cột phải: lưới cột ----------
        var right = new DockPanel();
        var tools = DialogUi.Row(
            DialogUi.Btn("Mọi giao điểm", (s, e) => { Plan.SetAll(true); ColumnsChanged(); }),
            DialogUi.Btn("Chỉ cột biên", (s, e) => { Plan.SetPerimeter(); ColumnsChanged(); }),
            DialogUi.Btn("Bỏ hết", (s, e) => { Plan.SetAll(false); ColumnsChanged(); }));
        DockPanel.SetDock(tools, Dock.Top);
        right.Children.Add(tools);
        _info = new TextBlock { Margin = new Thickness(2, 4, 2, 2), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray };
        DockPanel.SetDock(_info, Dock.Bottom);
        right.Children.Add(_info);
        _cv = new Canvas { Background = Brushes.White, ClipToBounds = true, MinHeight = 300, Cursor = Cursors.Hand };
        _cv.SizeChanged += (s, e) => Draw();
        _cv.MouseLeftButtonDown += CanvasClick;
        right.Children.Add(new Border { Child = _cv, BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1) });
        var gCol = new GroupBox { Header = "Lưới cột – bấm giao điểm để đặt / bỏ cột, bấm tên trục để đảo cả trục", Content = right, Padding = new Thickness(4) };

        var body = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(left, Dock.Left);
        body.Children.Add(left);
        body.Children.Add(gCol);

        var bOk = DialogUi.Btn("Áp dụng", (s, e) => Ok(), true); bOk.IsDefault = true;
        var bCancel = DialogUi.Btn("Huỷ", (s, e) => DialogResult = false); bCancel.IsCancel = true;
        var bs = DialogUi.Row(bOk, bCancel);
        bs.HorizontalAlignment = HorizontalAlignment.Right; bs.Margin = new Thickness(10, 6, 10, 10);
        Content = DialogUi.ScrollBody(body, bs);
        Plan.ResizeMask();
        Recalc();
    }

    private static TextBox Box(string v, double w) => new() { Text = v, Width = w, VerticalContentAlignment = VerticalAlignment.Center };

    private static DockPanel Line(string label, FrameworkElement ctl)
    {
        var d = new DockPanel { Margin = new Thickness(2) };
        var l = new TextBlock { Text = label, Width = 120, VerticalAlignment = VerticalAlignment.Center };
        d.Children.Add(l); d.Children.Add(ctl);
        return d;
    }

    private string GridKey() => string.Join(";", _ax.Select(a => a.Name + "=" + DialogUi.F(a.Spacing))) + "|" + string.Join(";", _ay.Select(a => a.Name + "=" + DialogUi.F(a.Spacing)));

    private static void FillAxes(ObservableCollection<AxisVm> rows, IList<string> names, IList<double> spans)
    {
        rows.Clear();
        for (int i = 0; i <= spans.Count; i++)
            rows.Add(new AxisVm { Name = i < names.Count ? names[i] : (i + 1).ToString(), Spacing = i == 0 ? 0 : Math.Round(spans[i - 1], 4) });
    }

    private DataGrid AxisGrid(ObservableCollection<AxisVm> rows, GridDir d)
    {
        var g = DialogUi.NewGrid();
        g.ItemsSource = rows;
        g.Height = 150;
        g.Columns.Add(DialogUi.Text("Tên trục", "Name", false, 70));
        g.Columns.Add(DialogUi.Num("Khoảng cách tới trục trước (m)", "Spacing", "0.###", false, 150));
        g.Columns.Add(DialogUi.Num("Toạ độ (m)", "Coord", "0.000", true, 80));
        g.CellEditEnding += (s, e) => Dispatcher.BeginInvoke(new Action(() => AxesChanged(d)));
        g.BeginningEdit += (s, e) => { if (e.Row.GetIndex() == 0 && (e.Column.Header as string ?? "").StartsWith("Khoảng")) e.Cancel = true; };   // trục đầu: toạ độ 0
        return g;
    }

    private UIElement AxisPanel(DataGrid g, ObservableCollection<AxisVm> rows, GridDir d)
    {
        var sp = new StackPanel();
        sp.Children.Add(g);
        var quick = Box("", 120);
        quick.ToolTip = "Điền nhanh cả bảng: 3.6 4.2 3.6 hoặc 3*3.3 (tên trục tiếp nối từ tên trục đầu)";
        sp.Children.Add(DialogUi.Row(
            DialogUi.Btn("Thêm trục", (s, e) =>
            {
                DialogUi.Commit(g);
                double spc = rows.Count > 1 ? rows[^1].Spacing : 3.6;
                rows.Add(new AxisVm { Name = NextName(rows, d), Spacing = spc > 0 ? spc : 3.6 });
                Plan.InsertMaskAxis(d, rows.Count - 1);
                AxesChanged(d);
            }),
            DialogUi.Btn("Chèn trục giữa", (s, e) =>
            {   // chèn sau trục chọn, chia đôi khoảng tới trục kế tiếp – các trục khác giữ nguyên toạ độ
                DialogUi.Commit(g);
                int k = g.SelectedIndex;
                if (k < 0 || k >= rows.Count - 1) { MessageBox.Show(this, "Chọn một trục (không phải trục cuối) để chèn trục giữa nó và trục kế tiếp.", Title); return; }
                double half = Math.Round(rows[k + 1].Spacing / 2, 4);
                rows[k + 1].Spacing = Math.Round(rows[k + 1].Spacing - half, 4);
                rows.Insert(k + 1, new AxisVm { Name = rows[k].Name + "'", Spacing = half });
                Plan.InsertMaskAxis(d, k + 1);
                AxesChanged(d);
            }),
            DialogUi.Btn("Xoá trục", (s, e) =>
            {   // xoá trục chọn: khoảng cách của nó cộng vào trục sau → các trục khác giữ nguyên toạ độ
                DialogUi.Commit(g);
                int k = g.SelectedIndex;
                if (k < 0 || rows.Count <= 2) return;
                if (k + 1 < rows.Count) rows[k + 1].Spacing = Math.Round(rows[k + 1].Spacing + (k == 0 ? 0 : rows[k].Spacing), 4);
                rows.RemoveAt(k);
                rows[0].Spacing = 0;
                Plan.RemoveMaskAxis(d, k);
                AxesChanged(d);
            })));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Điền nhanh"), quick, DialogUi.Btn("Điền", (s, e) =>
        {
            var spans = DetailShim.ParseList(quick.Text);
            if (spans.Count == 0 || spans.Any(v => v <= 0)) { MessageBox.Show(this, "Nhập dạng 3.6 4.2 3.6 hoặc 3*3.3.", Title); return; }
            string first = rows.Count > 0 ? rows[0].Name : (d == GridDir.X ? "1" : "A");
            FillAxes(rows, GridColumnSetup.AxisNames(first, spans.Count + 1), spans);
            Plan.ResizeMask();
            AxesChanged(d);
        })));
        return sp;
    }

    private static string NextName(ObservableCollection<AxisVm> rows, GridDir d)
    {
        var used = new HashSet<string>(rows.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
        string last = rows.Count > 0 ? rows[^1].Name : (d == GridDir.X ? "0" : "");
        foreach (var n in GridColumnSetup.AxisNames(string.IsNullOrEmpty(last) ? "A" : last, 2).Skip(1)) if (!used.Contains(n)) return n;
        for (int k = rows.Count + 1; ; k++) { var n = (d == GridDir.X ? "" : "Y") + k; if (!used.Contains(n)) return n; }
    }

    /// <summary>Bảng trục → Plan (bước trục, tên), toạ độ tự tính, vẽ lại.</summary>
    private void AxesChanged(GridDir d)
    {
        SyncAxes(d);
        if (_hasModel && GridKey() != _origGridKey && _rbHeights.IsChecked == true) _rbRebuild.IsChecked = true;   // đổi lưới → tạo lại khung
        Recalc();
    }

    private void SyncAxes(GridDir d)
    {
        var rows = d == GridDir.X ? _ax : _ay;
        if (rows.Count > 0) rows[0].Spacing = 0;
        var spans = rows.Skip(1).Select(r => r.Spacing).ToList();
        if (d == GridDir.X) { Plan.SpansX = spans; Plan.NamesX = rows.Select(r => (r.Name ?? "").Trim()).ToList(); }
        else { Plan.SpansY = spans; Plan.NamesY = rows.Select(r => (r.Name ?? "").Trim()).ToList(); }
        Plan.ResizeMask();
    }

    private string NewStoryName()
    {
        var used = new HashSet<string>(_rows.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
        for (int k = _rows.Count + 1; ; k++) { var n = "T" + k; if (!used.Contains(n)) return n; }
    }

    private void ColumnsChanged()
    {
        if (_hasModel && _rbHeights.IsChecked == true) _rbRebuild.IsChecked = true;   // đổi lưới / cột → phải tạo lại khung
        Recalc();
    }

    private void Recalc()
    {
        if (_recalc) return;
        _recalc = true;
        try
        {
            double z = DialogUi.ParseNum(_tBase.Text, 0);
            foreach (var r in _rows) { z += r.Height; r.Elevation = Math.Round(z, 4); }
            foreach (var axes in new[] { _ax, _ay })
            {
                double c = 0;
                for (int k = 0; k < axes.Count; k++) { if (k > 0) c += axes[k].Spacing; axes[k].Coord = Math.Round(c, 4); }
            }
            bool sameCount = _rows.Count == _modelStories;
            _rbHeights.IsEnabled = _hasModel && sameCount;
            if (!_rbHeights.IsEnabled && _rbHeights.IsChecked == true) _rbRebuild.IsChecked = true;
            double lx = Plan.SpansX.Sum(), ly = Plan.SpansY.Sum();
            _info.Text = $"Lưới {Plan.NX} × {Plan.NY} trục ({DialogUi.F(lx, "0.##")} × {DialogUi.F(ly, "0.##")} m) · {Plan.ColumnCount}/{Plan.NX * Plan.NY} vị trí có cột · "
                       + $"{_rows.Count} tầng, đỉnh +{DialogUi.F(z, "0.000")} m."
                       + (Plan.ColumnCount < Plan.NX * Plan.NY ? " Giao điểm không cột: dầm đi qua thành dầm đỡ dầm." : "");
            Draw();
        }
        finally { _recalc = false; }
    }

    // ---------- vẽ lưới cột ----------
    private double _s, _ox, _oy;
    private List<double> _cx = new(), _cy = new();

    private void Draw()
    {
        _cv.Children.Clear();
        double W = _cv.ActualWidth, H = _cv.ActualHeight;
        if (W < 50 || H < 50) return;
        _cx = Plan.Coords(GridDir.X); _cy = Plan.Coords(GridDir.Y);
        double lx = Math.Max(_cx[^1], 1), ly = Math.Max(_cy[^1], 1);
        const double pad = 46;
        _s = Math.Min((W - 2 * pad) / lx, (H - 2 * pad) / ly);
        _ox = (W - lx * _s) / 2; _oy = (H + ly * _s) / 2;
        var nx = Plan.Names(GridDir.X); var ny = Plan.Names(GridDir.Y);
        var axis = new SolidColorBrush(Color.FromRgb(200, 60, 60));
        for (int i = 0; i < _cx.Count; i++)
        {
            double x = _ox + _cx[i] * _s;
            AddLine(x, _oy + 14, x, _oy - ly * _s - 14, axis, true);
            Bubble(x, _oy + 28, nx[i]);
            if (i > 0) Txt((x + _ox + _cx[i - 1] * _s) / 2, _oy + 6, DialogUi.F((_cx[i] - _cx[i - 1]) * 1000, "0"), 9, Brushes.DimGray);
        }
        for (int j = 0; j < _cy.Count; j++)
        {
            double y = _oy - _cy[j] * _s;
            AddLine(_ox - 14, y, _ox + lx * _s + 14, y, axis, true);
            Bubble(_ox - 28, y, ny[j]);
            if (j > 0) Txt(_ox - 8, (y + _oy - _cy[j - 1] * _s) / 2, DialogUi.F((_cy[j] - _cy[j - 1]) * 1000, "0"), 9, Brushes.DimGray, true);
        }
        double c = Math.Clamp(_s * 0.22, 8, 16);
        for (int i = 0; i < _cx.Count; i++)
            for (int j = 0; j < _cy.Count; j++)
            {
                bool on = i < Plan.Columns.GetLength(0) && j < Plan.Columns.GetLength(1) && Plan.Columns[i, j];
                var r = new Shapes.Rectangle
                {
                    Width = c, Height = c, StrokeThickness = 1.2,
                    Stroke = on ? Brushes.Black : Brushes.Gray, Fill = on ? new SolidColorBrush(Color.FromRgb(60, 60, 60)) : Brushes.White,
                    StrokeDashArray = on ? null : new DoubleCollection { 2, 2 }
                };
                Canvas.SetLeft(r, _ox + _cx[i] * _s - c / 2); Canvas.SetTop(r, _oy - _cy[j] * _s - c / 2);
                _cv.Children.Add(r);
            }
    }

    private void AddLine(double x1, double y1, double x2, double y2, Brush b, bool dash)
    {
        _cv.Children.Add(new Shapes.Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = b, StrokeThickness = 0.8, StrokeDashArray = dash ? new DoubleCollection { 8, 3, 1, 3 } : null, IsHitTestVisible = false });
    }

    private void Bubble(double x, double y, string name)
    {
        var e = new Shapes.Ellipse { Width = 22, Height = 22, Stroke = Brushes.Black, StrokeThickness = 1, Fill = Brushes.White };
        Canvas.SetLeft(e, x - 11); Canvas.SetTop(e, y - 11);
        _cv.Children.Add(e);
        Txt(x, y, name, 11, Brushes.Black);
    }

    private void Txt(double x, double y, string s, double size, Brush b, bool vertical = false)
    {
        var t = new TextBlock { Text = s, FontSize = size, Foreground = b, IsHitTestVisible = false };
        t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        if (vertical) { t.LayoutTransform = new RotateTransform(-90); t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); }
        Canvas.SetLeft(t, x - t.DesiredSize.Width / 2 - (vertical ? t.DesiredSize.Width / 2 : 0)); Canvas.SetTop(t, y - t.DesiredSize.Height / 2);
        _cv.Children.Add(t);
    }

    private void CanvasClick(object sender, MouseButtonEventArgs e)
    {
        if (_cx.Count == 0 || _cy.Count == 0) return;
        var p = e.GetPosition(_cv);
        Plan.ResizeMask();
        // bong bóng trục X (dưới) / trục Y (trái): đảo cả trục
        for (int i = 0; i < _cx.Count; i++)
            if (Math.Abs(p.X - (_ox + _cx[i] * _s)) < 12 && Math.Abs(p.Y - (_oy + 28)) < 12) { Plan.ToggleCol(i); ColumnsChanged(); return; }
        for (int j = 0; j < _cy.Count; j++)
            if (Math.Abs(p.X - (_ox - 28)) < 12 && Math.Abs(p.Y - (_oy - _cy[j] * _s)) < 12) { Plan.ToggleRow(j); ColumnsChanged(); return; }
        int bi = -1, bj = -1; double best = 18;
        for (int i = 0; i < _cx.Count; i++)
            for (int j = 0; j < _cy.Count; j++)
            {
                double d = Math.Abs(p.X - (_ox + _cx[i] * _s)) + Math.Abs(p.Y - (_oy - _cy[j] * _s));
                if (d < best) { best = d; bi = i; bj = j; }
            }
        if (bi < 0) return;
        Plan.Columns[bi, bj] = !Plan.Columns[bi, bj];
        ColumnsChanged();
    }

    private void Ok()
    {
        DialogUi.Commit(_grid);
        GridChangedSilently();
        Plan.BaseElevation = DialogUi.ParseNum(_tBase.Text, 0);
        Plan.Stories = _rows.Select(r => new StoryRow { Name = (r.Name ?? "").Trim(), Height = r.Height }).ToList();
        HeightsOnly = _rbHeights.IsChecked == true && _rbHeights.IsEnabled;
        if (HeightsOnly && GridKey() != _origGridKey)
        {
            MessageBox.Show(this, "Đã đổi lưới trục – chọn 'Tạo lại khung từ lưới' để áp dụng lưới mới.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            _rbRebuild.IsChecked = true; return;
        }
        var err = GridColumnSetup.Validate(Plan);
        if (err == null && !HeightsOnly && Plan.ColumnCount == 0) err = "Chưa đánh dấu giao điểm nào có cột.";
        if (err != null) { MessageBox.Show(this, err, Title, MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        DialogResult = true;
    }

    private void GridChangedSilently()
    {
        DialogUi.Commit(_gx); DialogUi.Commit(_gy);
        SyncAxes(GridDir.X); SyncAxes(GridDir.Y);
    }
}
