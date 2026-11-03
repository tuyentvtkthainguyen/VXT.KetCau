using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WPath = System.Windows.Shapes.Path;
using WRect = System.Windows.Shapes.Rectangle;
using WEllipse = System.Windows.Shapes.Ellipse;
using WLine = System.Windows.Shapes.Line;
using System.Windows.Threading;
using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Dòng bảng chu kỳ + tỷ lệ khối lượng tham gia (như "Modal Participating Mass Ratios" của ETABS).</summary>
internal sealed class ModalModeRow
{
    public int Mode { get; set; }
    public double T { get; set; }
    public double F { get; set; }
    public double UX { get; set; }
    public double UY { get; set; }
    public double RZ { get; set; }
    public double SumUX { get; set; }
    public double SumUY { get; set; }
    public double SumRZ { get; set; }
    public string Dir { get; set; } = "";
}

internal sealed class ModalStoryRow
{
    public string Story { get; set; } = "";
    public double Z { get; set; }
    public double M { get; set; }
    public double W { get; set; }
    public double Xm { get; set; }
    public double Ym { get; set; }
    public string Xr { get; set; } = "";
    public string Yr { get; set; } = "";
    public double Im { get; set; }
}

internal sealed class ModalRsRow
{
    public string Case { get; set; } = "";
    public int Mode { get; set; }
    public double T { get; set; }
    public double Sd { get; set; }
    public double Meff { get; set; }
    public double V { get; set; }
}

/// <summary>Khung nhìn "Dao động": bảng chu kỳ/tỷ lệ khối lượng, khối lượng tầng, phổ phản ứng; hình dạng dao động 3D dạng khung dây
/// (chiếu trục đo, kéo chuột để xoay) có hoạt hoạ (DispatcherTimer), mặt bằng tâm khối lượng – tâm cứng theo tầng.
/// Thiết lập: Phân tích → Dao động riêng… (ModalDialog.cs).</summary>
public partial class MainWindow
{
    private TabItem _modalTab;
    private DataGrid _gModes, _gModalStory, _gModalRs;
    private TextBox _txtModalNotes;
    private TextBlock _txtModalInfo;
    private ComboBox _cbMode, _cbModalView, _cbModalStory;
    private Slider _sldModalAmp;
    private Button _btnModalPlay;
    private Border _modalHost;
    private Canvas _modalCanvas;
    private DispatcherTimer _modalTimer;
    private double _modalPhase;
    private bool _modalPlaying = true, _modalRot, _modalUiBusy;
    private Point _modalRotStart;
    private double _mYaw = -35, _mPitch = 25, _mYaw0, _mPitch0;

    private static readonly CultureInfo InvM = CultureInfo.InvariantCulture;

    private void InitModalUi()
    {
        if (_modalTab != null || tabView == null) return;
        // ---- trái: bảng ----
        _gModes = DialogUi.NewGrid(); _gModes.IsReadOnly = true;
        _gModes.Columns.Add(DialogUi.Text("Dạng", "Mode", true, 46));
        _gModes.Columns.Add(DialogUi.Num("T (s)", "T", "0.000", true, 62));
        _gModes.Columns.Add(DialogUi.Num("f (Hz)", "F", "0.000", true, 62));
        _gModes.Columns.Add(DialogUi.Num("UX %", "UX", "0.0", true, 54));
        _gModes.Columns.Add(DialogUi.Num("UY %", "UY", "0.0", true, 54));
        _gModes.Columns.Add(DialogUi.Num("RZ %", "RZ", "0.0", true, 54));
        _gModes.Columns.Add(DialogUi.Num("ΣUX %", "SumUX", "0.0", true, 58));
        _gModes.Columns.Add(DialogUi.Num("ΣUY %", "SumUY", "0.0", true, 58));
        _gModes.Columns.Add(DialogUi.Num("ΣRZ %", "SumRZ", "0.0", true, 58));
        _gModes.Columns.Add(DialogUi.Text("Phương", "Dir", true, 60));
        _gModes.SelectionMode = DataGridSelectionMode.Single;
        _gModes.SelectionChanged += (s, e) =>
        {
            if (_modalUiBusy || _gModes.SelectedItem is not ModalModeRow r) return;
            _cbMode.SelectedIndex = r.Mode - 1;
        };
        _gModalStory = DialogUi.NewGrid(); _gModalStory.IsReadOnly = true;
        _gModalStory.Columns.Add(DialogUi.Text("Tầng", "Story", true, 60));
        _gModalStory.Columns.Add(DialogUi.Num("Cao độ", "Z", "0.000", true, 60));
        _gModalStory.Columns.Add(DialogUi.Num("M (t)", "M", "0.00", true, 62));
        _gModalStory.Columns.Add(DialogUi.Num("W (kN)", "W", "0.0", true, 66));
        _gModalStory.Columns.Add(DialogUi.Num("Xm", "Xm", "0.00", true, 54));
        _gModalStory.Columns.Add(DialogUi.Num("Ym", "Ym", "0.00", true, 54));
        _gModalStory.Columns.Add(DialogUi.Text("Xr", "Xr", true, 54));
        _gModalStory.Columns.Add(DialogUi.Text("Yr", "Yr", true, 54));
        _gModalStory.Columns.Add(DialogUi.Num("Im (t·m²)", "Im", "0.0", true, 70));
        _gModalRs = DialogUi.NewGrid(); _gModalRs.IsReadOnly = true;
        _gModalRs.Columns.Add(DialogUi.Text("Trường hợp", "Case", true, 70));
        _gModalRs.Columns.Add(DialogUi.Text("Dạng", "Mode", true, 46));
        _gModalRs.Columns.Add(DialogUi.Num("T (s)", "T", "0.000", true, 62));
        _gModalRs.Columns.Add(DialogUi.Num("Sd/g", "Sd", "0.0000", true, 66));
        _gModalRs.Columns.Add(DialogUi.Num("Meff (t)", "Meff", "0.00", true, 70));
        _gModalRs.Columns.Add(DialogUi.Num("Vk (kN)", "V", "0.0", true, 70));
        _txtModalNotes = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0) };
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Tỷ lệ khối lượng tham gia", Content = _gModes });
        tabs.Items.Add(new TabItem { Header = "Khối lượng tầng", Content = _gModalStory });
        tabs.Items.Add(new TabItem { Header = "Phổ phản ứng", Content = _gModalRs });
        tabs.Items.Add(new TabItem { Header = "Ghi chú", Content = _txtModalNotes });
        _txtModalInfo = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 3, 4, 3) };
        var left = new DockPanel();
        DockPanel.SetDock(_txtModalInfo, Dock.Top);
        left.Children.Add(_txtModalInfo);
        left.Children.Add(tabs);

        // ---- phải: hình dạng dao động ----
        _cbMode = new ComboBox { Width = 210, Margin = new Thickness(0, 0, 8, 0) };
        _cbMode.SelectionChanged += (s, e) => { SyncModeGrid(); DrawModal(); };
        _cbModalView = new ComboBox { Width = 190, Margin = new Thickness(0, 0, 8, 0), ItemsSource = new[] { "Dạng dao động 3D", "Mặt bằng: tâm khối lượng – tâm cứng" }, SelectedIndex = 0 };
        _cbModalView.SelectionChanged += (s, e) => DrawModal();
        _cbModalStory = new ComboBox { Width = 90, Margin = new Thickness(0, 0, 8, 0) };
        _cbModalStory.SelectionChanged += (s, e) => DrawModal();
        _sldModalAmp = new Slider { Width = 110, Minimum = 0.2, Maximum = 3, Value = 1, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Tỉ lệ biên độ hiển thị" };
        _sldModalAmp.ValueChanged += (s, e) => DrawModal();
        _btnModalPlay = new Button { Content = "⏸ Dừng", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 6, 0) };
        _btnModalPlay.Click += (s, e) => SetModalPlaying(!_modalPlaying);
        var btnSet = new Button { Content = "Thiết lập…", Padding = new Thickness(8, 2, 8, 2) };
        btnSet.Click += MnuModal_Click;
        var bar = new WrapPanel { Margin = new Thickness(4, 3, 4, 3) };
        bar.Children.Add(DialogUi.Label("Dạng:")); bar.Children.Add(_cbMode);
        bar.Children.Add(DialogUi.Label("Hiển thị:")); bar.Children.Add(_cbModalView);
        bar.Children.Add(DialogUi.Label("Tầng:")); bar.Children.Add(_cbModalStory);
        bar.Children.Add(DialogUi.Label("Biên độ:")); bar.Children.Add(_sldModalAmp);
        bar.Children.Add(_btnModalPlay); bar.Children.Add(btnSet);
        _modalCanvas = new Canvas { ClipToBounds = true };
        _modalHost = new Border { Background = new SolidColorBrush(Color.FromRgb(0x20, 0x26, 0x2E)), ClipToBounds = true, Child = _modalCanvas };
        _modalHost.SizeChanged += (s, e) => DrawModal();
        _modalHost.MouseLeftButtonDown += (s, e) => { _modalRot = true; _modalRotStart = e.GetPosition(_modalHost); _mYaw0 = _mYaw; _mPitch0 = _mPitch; _modalHost.CaptureMouse(); };
        _modalHost.MouseMove += (s, e) =>
        {
            if (!_modalRot) return;
            var q = e.GetPosition(_modalHost);
            _mYaw = _mYaw0 - (q.X - _modalRotStart.X) * 0.4;
            _mPitch = Math.Clamp(_mPitch0 + (q.Y - _modalRotStart.Y) * 0.4, -89, 89);
            if (!_modalPlaying) DrawModal();
        };
        _modalHost.MouseLeftButtonUp += (s, e) => { _modalRot = false; _modalHost.ReleaseMouseCapture(); };
        _modalHost.IsVisibleChanged += (s, e) => UpdateModalTimer();
        var right = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        right.Children.Add(bar);
        right.Children.Add(_modalHost);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(560), MinWidth = 260 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
        Grid.SetColumn(left, 0);
        var split = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)) };
        Grid.SetColumn(split, 1);
        Grid.SetColumn(right, 2);
        grid.Children.Add(left); grid.Children.Add(split); grid.Children.Add(right);
        _modalTab = new TabItem { Header = "Dao động", Content = grid };
        tabView.Items.Add(_modalTab);

        _modalTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _modalTimer.Tick += (s, e) => { _modalPhase += 0.15; DrawModal(); };
        RefreshModalView();
    }

    private void UpdateModalTimer()
    {
        if (_modalTimer == null) return;
        bool run = _modalPlaying && _modalHost.IsVisible && _res?.Modal != null && _cbModalView.SelectedIndex == 0;
        if (run && !_modalTimer.IsEnabled) _modalTimer.Start();
        else if (!run && _modalTimer.IsEnabled) _modalTimer.Stop();
    }

    private void SetModalPlaying(bool play)
    {
        _modalPlaying = play;
        if (_btnModalPlay != null) _btnModalPlay.Content = play ? "⏸ Dừng" : "▶ Chạy";
        UpdateModalTimer();
        DrawModal();
    }

    private void SyncModeGrid()
    {
        if (_gModes == null) return;
        _modalUiBusy = true;
        try
        {
            int i = _cbMode.SelectedIndex;
            var items = _gModes.ItemsSource as List<ModalModeRow>;
            _gModes.SelectedItem = items != null && i >= 0 && i < items.Count ? items[i] : null;
        }
        finally { _modalUiBusy = false; }
    }

    /// <summary>Cập nhật bảng và hình sau khi phân tích / khi kết quả bị xoá (hook trong RefreshResults).</summary>
    private void RefreshModalView()
    {
        if (_modalTab == null) return;
        var mr = _res?.Modal;
        _modalUiBusy = true;
        try
        {
            int keep = Math.Max(_cbMode.SelectedIndex, 0);
            if (mr == null || mr.Modes.Count == 0)
            {
                _gModes.ItemsSource = null; _gModalStory.ItemsSource = null; _gModalRs.ItemsSource = null;
                _cbMode.ItemsSource = null; _cbModalStory.ItemsSource = null;
                _txtModalNotes.Text = "";
                _txtModalInfo.Text = _res == null ? "Chưa có kết quả phân tích (F5)."
                    : "Không có kết quả dao động riêng – bật trong Phân tích → Dao động riêng… rồi chạy lại phân tích (F5).";
            }
            else
            {
                _gModes.ItemsSource = mr.Modes.Select(m => new ModalModeRow
                {
                    Mode = m.Index, T = m.Period, F = m.Frequency, UX = m.Ratio[0] * 100, UY = m.Ratio[1] * 100, RZ = m.Ratio[3] * 100,
                    SumUX = m.Cumulative[0] * 100, SumUY = m.Cumulative[1] * 100, SumRZ = m.Cumulative[3] * 100, Dir = m.Dominant
                }).ToList();
                _gModalStory.ItemsSource = mr.Stories.Select(s => new ModalStoryRow
                {
                    Story = s.Story, Z = s.Elevation, M = s.Mass, W = s.Weight, Xm = s.Xm, Ym = s.Ym,
                    Xr = double.IsNaN(s.Xr) ? "-" : s.Xr.ToString("0.00", InvM), Yr = double.IsNaN(s.Yr) ? "-" : s.Yr.ToString("0.00", InvM), Im = s.Inertia
                }).ToList();
                _gModalRs.ItemsSource = mr.Spectrum.SelectMany(rc => rc.Rows.Select(x => new ModalRsRow { Case = rc.Pattern, Mode = x.Mode, T = x.Period, Sd = x.Sd, Meff = x.Meff, V = x.BaseShear })).ToList();
                _cbMode.ItemsSource = mr.Modes.Select(m => $"Dạng {m.Index}: T = {m.Period.ToString("0.000", InvM)} s ({m.Dominant})").ToList();
                _cbMode.SelectedIndex = Math.Min(keep, mr.Modes.Count - 1);
                var stNames = mr.Stories.Select(s => s.Story).ToList();
                string curSt = _cbModalStory.SelectedItem as string;
                _cbModalStory.ItemsSource = stNames;
                _cbModalStory.SelectedItem = stNames.Contains(curSt) ? curSt : stNames.LastOrDefault();
                var m1 = mr.First;
                string rs = string.Join("; ", mr.Spectrum.Select(rc => $"{rc.Pattern}: V = {rc.BaseShear.ToString("0.0", InvM)} kN (tĩnh {rc.StaticBaseShear.ToString("0.0", InvM)} kN)"));
                _txtModalInfo.Text = $"{mr.Modes.Count} dạng, {mr.DynamicDofs} bậc tự do động – T1 = {m1.Period.ToString("0.000", InvM)} s (phương {m1.Dominant}), Ct·H^0,75 = {mr.T1Formula.ToString("0.000", InvM)} s; "
                    + $"Σ UX = {mr.SumRatio(0) * 100:0.0} %, Σ UY = {mr.SumRatio(1) * 100:0.0} %. Nguồn khối lượng: {mr.MassSource}."
                    + (rs.Length > 0 ? " Phổ phản ứng – " + rs + "." : "");
                var notes = new List<string> { "Phương pháp giải: " + mr.Solver, $"Tổng khối lượng MX = {mr.TotalMass[0].ToString("0.00", InvM)} t, MY = {mr.TotalMass[1].ToString("0.00", InvM)} t; tại gối {mr.BaseMass.ToString("0.00", InvM)} t." };
                notes.AddRange(mr.Notes);
                _txtModalNotes.Text = string.Join(Environment.NewLine + Environment.NewLine, notes);
            }
        }
        finally { _modalUiBusy = false; }
        SyncModeGrid();
        UpdateModalTimer();
        DrawModal();
    }

    private void MnuModal_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        ApplyUiToProject(false);
        var d = new ModalDialog(_p) { Owner = this };
        if (d.ShowDialog() != true) return;
        MarkDirty();
        SetStatus("Đã cập nhật thiết lập dao động riêng – chạy lại phân tích (F5) để có kết quả. Phương pháp động đất: " + _p.Seismic.Method + ".");
    }

    // ------------------------------------------------------------------
    // vẽ
    // ------------------------------------------------------------------
    private void DrawModal()
    {
        if (_modalCanvas == null) return;
        _modalCanvas.Children.Clear();
        double W = _modalHost.ActualWidth, H = _modalHost.ActualHeight;
        if (W < 20 || H < 20) return;
        var mr = _res?.Modal;
        if (mr == null || mr.Modes.Count == 0 || _p == null)
        {
            AddModalText("Chưa có kết quả dao động riêng.", 10, 10, Brushes.WhiteSmoke);
            return;
        }
        if (_cbModalView.SelectedIndex == 1) { DrawModalPlan(mr, W, H); return; }
        int k = Math.Clamp(_cbMode.SelectedIndex, 0, mr.Modes.Count - 1);
        var mode = mr.Modes[k];

        double yaw = _mYaw * Math.PI / 180, pitch = _mPitch * Math.PI / 180;
        double cy = Math.Cos(yaw), sy = Math.Sin(yaw), cp = Math.Cos(pitch), sp = Math.Sin(pitch);
        double x0 = _p.Nodes.Min(n => n.X), x1 = _p.Nodes.Max(n => n.X), y0 = _p.Nodes.Min(n => n.Y), y1 = _p.Nodes.Max(n => n.Y), z0 = _p.Nodes.Min(n => n.Z), z1 = _p.Nodes.Max(n => n.Z);
        double cx = (x0 + x1) / 2, cyy = (y0 + y1) / 2, cz = (z0 + z1) / 2;
        Point Proj(double x, double y, double z)
        {
            double dx = x - cx, dy = y - cyy, dz = z - cz;
            double sx = dx * cy - dy * sy;
            double depth = dx * sy + dy * cy;
            double up = dz * cp - depth * sp;
            return new Point(sx, -up);
        }
        double size = Math.Max(Math.Max(x1 - x0, y1 - y0), Math.Max(z1 - z0, 1));
        double amp = 0.08 * size * _sldModalAmp.Value * mode.DisplayScale * (_modalPlaying ? Math.Sin(_modalPhase) : 1.0);

        // polyline của từng thanh: (vị trí, chuyển vị)
        var undeformed = new List<Point[]>();
        var deformed = new List<(Point[] pts, bool col)>();
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (var f in _p.Frames)
        {
            var ni = _p.Node(f.NodeI); var nj = _p.Node(f.NodeJ);
            mode.Disp.TryGetValue(f.NodeI, out var ui); mode.Disp.TryGetValue(f.NodeJ, out var uj);
            mode.FrameInner.TryGetValue(f.Id, out var inner);
            int nseg = (inner?.Length ?? 0) + 1;
            var arc = f.IsCurved ? CurvedBeam.Arc(_p, f) : null;
            var pu = new Point[nseg + 1]; var pd = new Point[nseg + 1];
            for (int s = 0; s <= nseg; s++)
            {
                double t = (double)s / nseg;
                Vec3 q = s == 0 ? ni.P : s == nseg ? nj.P : arc != null ? arc.PointAt(t) : ni.P + (nj.P - ni.P) * t;
                double[] u = s == 0 ? ui : s == nseg ? uj : inner[s - 1];
                u ??= new double[3];
                pu[s] = Proj(q.X, q.Y, q.Z);
                pd[s] = Proj(q.X + amp * u[0], q.Y + amp * u[1], q.Z + amp * u[2]);
                foreach (var pt in new[] { pu[s], pd[s] })
                {
                    minX = Math.Min(minX, pt.X); maxX = Math.Max(maxX, pt.X); minY = Math.Min(minY, pt.Y); maxY = Math.Max(maxY, pt.Y);
                }
            }
            undeformed.Add(pu); deformed.Add((pd, f.IsVerticalKind));
        }
        if (undeformed.Count == 0) return;
        double bw = Math.Max(maxX - minX, 1e-6), bh = Math.Max(maxY - minY, 1e-6);
        double sc = Math.Min((W - 40) / bw, (H - 60) / bh);
        double ox = W / 2 - (minX + maxX) / 2 * sc, oy = (H + 20) / 2 - (minY + maxY) / 2 * sc;
        Point S(Point p) => new(ox + p.X * sc, oy + p.Y * sc);
        Geometry Geo(IEnumerable<Point[]> lines)
        {
            var g = new StreamGeometry();
            using (var ctx = g.Open())
                foreach (var l in lines)
                {
                    ctx.BeginFigure(S(l[0]), false, false);
                    for (int i = 1; i < l.Length; i++) ctx.LineTo(S(l[i]), true, false);
                }
            g.Freeze();
            return g;
        }
        _modalCanvas.Children.Add(new WPath { Data = Geo(undeformed), Stroke = new SolidColorBrush(Color.FromRgb(90, 100, 110)), StrokeThickness = 1 });
        _modalCanvas.Children.Add(new WPath { Data = Geo(deformed.Where(d => !d.col).Select(d => d.pts)), Stroke = new SolidColorBrush(Color.FromRgb(255, 170, 60)), StrokeThickness = 2 });
        _modalCanvas.Children.Add(new WPath { Data = Geo(deformed.Where(d => d.col).Select(d => d.pts)), Stroke = new SolidColorBrush(Color.FromRgb(90, 190, 255)), StrokeThickness = 2.2 });
        AddModalText($"Dạng {mode.Index}: T = {mode.Period.ToString("0.000", InvM)} s, f = {mode.Frequency.ToString("0.000", InvM)} Hz, ω = {mode.Omega.ToString("0.00", InvM)} rad/s – phương {mode.Dominant}; "
            + $"UX {mode.Ratio[0] * 100:0.0} %, UY {mode.Ratio[1] * 100:0.0} %, RZ {mode.Ratio[3] * 100:0.0} % (Σ UX {mode.Cumulative[0] * 100:0.0} %, Σ UY {mode.Cumulative[1] * 100:0.0} %)",
            8, 6, Brushes.WhiteSmoke);
        AddModalText("Kéo chuột: xoay  •  xám: không biến dạng; xanh: cột; cam: dầm", 8, H - 22, new SolidColorBrush(Color.FromRgb(170, 180, 190)));
    }

    private void DrawModalPlan(ModalResult mr, double W, double H)
    {
        var st = mr.Stories.FirstOrDefault(s => s.Story == _cbModalStory.SelectedItem as string) ?? mr.Stories.LastOrDefault();
        if (st == null) { AddModalText("Không có số liệu khối lượng tầng.", 10, 10, Brushes.WhiteSmoke); return; }
        var beams = _p.Frames.Where(f => !f.IsVerticalKind && Math.Abs(_p.Node(f.NodeI).Z - st.Elevation) < 1e-3 && Math.Abs(_p.Node(f.NodeJ).Z - st.Elevation) < 1e-3).ToList();
        var cols = _p.Frames.Where(f => f.IsVerticalKind).SelectMany(f => new[] { _p.Node(f.NodeI), _p.Node(f.NodeJ) })
            .Where(n => Math.Abs(n.Z - st.Elevation) < 1e-3).Distinct().ToList();
        var pts = beams.SelectMany(f => new[] { _p.Node(f.NodeI).P, _p.Node(f.NodeJ).P }).Concat(cols.Select(n => n.P)).ToList();
        pts.Add(new Vec3(st.Xm, st.Ym, 0));
        if (!double.IsNaN(st.Xr)) pts.Add(new Vec3(st.Xr, st.Yr, 0));
        double x0 = pts.Min(q => q.X), x1 = pts.Max(q => q.X), y0 = pts.Min(q => q.Y), y1 = pts.Max(q => q.Y);
        double sc = Math.Min((W - 80) / Math.Max(x1 - x0, 1e-3), (H - 100) / Math.Max(y1 - y0, 1e-3));
        double ox = W / 2 - (x0 + x1) / 2 * sc, oy = H / 2 + (y0 + y1) / 2 * sc;
        Point S(double x, double y) => new(ox + x * sc, oy - y * sc);
        var gb = new StreamGeometry();
        using (var ctx = gb.Open())
            foreach (var f in beams)
            {
                var a = _p.Node(f.NodeI); var b = _p.Node(f.NodeJ);
                ctx.BeginFigure(S(a.X, a.Y), false, false); ctx.LineTo(S(b.X, b.Y), true, false);
            }
        gb.Freeze();
        _modalCanvas.Children.Add(new WPath { Data = gb, Stroke = new SolidColorBrush(Color.FromRgb(255, 170, 60)), StrokeThickness = 2 });
        foreach (var n in cols)
        {
            var c = S(n.X, n.Y);
            var r = new WRect { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(90, 190, 255)) };
            Canvas.SetLeft(r, c.X - 4); Canvas.SetTop(r, c.Y - 4);
            _modalCanvas.Children.Add(r);
        }
        var cm = S(st.Xm, st.Ym);
        var e = new WEllipse { Width = 14, Height = 14, Fill = Brushes.OrangeRed, Stroke = Brushes.White, StrokeThickness = 1 };
        Canvas.SetLeft(e, cm.X - 7); Canvas.SetTop(e, cm.Y - 7);
        _modalCanvas.Children.Add(e);
        AddModalText($"CM ({st.Xm.ToString("0.00", InvM)}; {st.Ym.ToString("0.00", InvM)})", cm.X + 9, cm.Y - 18, Brushes.OrangeRed);
        if (!double.IsNaN(st.Xr))
        {
            var cr = S(st.Xr, st.Yr);
            _modalCanvas.Children.Add(new WLine { X1 = cr.X - 9, Y1 = cr.Y, X2 = cr.X + 9, Y2 = cr.Y, Stroke = Brushes.LimeGreen, StrokeThickness = 2.5 });
            _modalCanvas.Children.Add(new WLine { X1 = cr.X, Y1 = cr.Y - 9, X2 = cr.X, Y2 = cr.Y + 9, Stroke = Brushes.LimeGreen, StrokeThickness = 2.5 });
            AddModalText($"CR ({st.Xr.ToString("0.00", InvM)}; {st.Yr.ToString("0.00", InvM)})", cr.X + 9, cr.Y + 4, Brushes.LimeGreen);
        }
        string ecc = double.IsNaN(st.Xr) ? "tâm cứng: cần sàn cứng (diaphragm)" : $"độ lệch tâm ex = {(st.Xm - st.Xr).ToString("0.00", InvM)} m, ey = {(st.Ym - st.Yr).ToString("0.00", InvM)} m";
        AddModalText($"Tầng {st.Story} (+{st.Elevation.ToString("0.000", InvM)}): M = {st.Mass.ToString("0.00", InvM)} t, W = {st.Weight.ToString("0.0", InvM)} kN, Im = {st.Inertia.ToString("0.0", InvM)} t·m²; {ecc}",
            8, 6, Brushes.WhiteSmoke);
    }

    private void AddModalText(string s, double x, double y, Brush b)
    {
        var t = new TextBlock { Text = s, Foreground = b, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = Math.Max(200, _modalHost.ActualWidth - 16) };
        Canvas.SetLeft(t, x); Canvas.SetTop(t, y);
        _modalCanvas.Children.Add(t);
    }
}
