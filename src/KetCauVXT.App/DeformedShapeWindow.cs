using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Analysis;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Hiển thị → Hình biến dạng… (Display → Deformed Shape của ETABS): khung dây biến dạng theo trường hợp tải / tổ hợp tuyến tính,
/// khung nhìn 3D (trục đo, kéo chuột để xoay) hoặc mặt bằng tầng, tỉ lệ phóng tự động × hệ số, hình chưa biến dạng mờ phía sau.
/// Đường đàn hồi mỗi thanh nội suy Hermite từ chuyển vị + góc xoay hai nút (đầu có giải phóng mô men: bỏ góc xoay đầu đó).</summary>
internal sealed class DeformedShapeWindow : Window
{
    private readonly Project _p;
    private readonly AnalysisResults _res;
    private readonly ComboBox _case = new() { Width = 150, Margin = new Thickness(0, 0, 8, 0) };
    private readonly ComboBox _view = new() { Width = 130, Margin = new Thickness(0, 0, 8, 0) };
    private readonly ComboBox _story = new() { Width = 110, Margin = new Thickness(0, 0, 8, 0) };
    private readonly Slider _scale = new() { Width = 140, Minimum = 0.1, Maximum = 5, Value = 1, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _scaleTxt = new() { Width = 40, VerticalAlignment = VerticalAlignment.Center };
    private readonly CheckBox _undeformed = new() { Content = "Hình chưa biến dạng", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Canvas _canvas = new() { ClipToBounds = true };
    private readonly Border _host;
    private readonly TextBlock _info = new() { Foreground = Brushes.WhiteSmoke, Margin = new Thickness(8, 6, 8, 6), TextWrapping = TextWrapping.Wrap, IsHitTestVisible = false };
    private double _yaw = -35, _pitch = 30, _yaw0, _pitch0;
    private Point _drag0;
    private bool _rot;

    private static readonly Brush Undef = Fz(Color.FromArgb(110, 170, 175, 185));
    private static readonly Brush DefBeam = Fz(Color.FromRgb(255, 190, 60));
    private static readonly Brush DefCol = Fz(Color.FromRgb(90, 200, 255));
    private static readonly Brush MaxBrush = Fz(Color.FromRgb(255, 70, 90));
    private static Brush Fz(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    public DeformedShapeWindow(Project p, AnalysisResults res, string initialCase)
    {
        ZoomPan.Wrap(_canvas);   // thu phóng / di chuyển hình vẽ – ZoomPan.cs
        _p = p; _res = res;
        DialogUi.Setup(this, "Hình biến dạng (Deformed Shape)", 1100, 760);
        var names = res.Cases.Keys.Concat(p.Combinations.Concat(res.ExternalCombos).Where(c => c.Type != ComboType.Envelope).Select(c => c.Name)).Distinct().ToList();
        _case.ItemsSource = names;
        _case.SelectedItem = initialCase != null && names.Contains(initialCase) ? initialCase : names.FirstOrDefault();
        _view.ItemsSource = new[] { "3D (trục đo)", "Mặt bằng tầng", "Mặt đứng XZ", "Mặt đứng YZ" };
        _view.SelectedIndex = 0;
        var st = p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name).ToList();
        _story.ItemsSource = st;
        _story.SelectedItem = st.LastOrDefault();
        foreach (var cb in new[] { _case, _view, _story }) cb.SelectionChanged += (s, e) => Draw();
        _scale.ValueChanged += (s, e) => Draw();
        _undeformed.Checked += (s, e) => Draw(); _undeformed.Unchecked += (s, e) => Draw();

        _host = new Border { Background = new SolidColorBrush(Color.FromRgb(0x20, 0x26, 0x2E)), ClipToBounds = true };
        var g = new Grid();
        g.Children.Add(_canvas); g.Children.Add(_info);
        _info.HorizontalAlignment = HorizontalAlignment.Left; _info.VerticalAlignment = VerticalAlignment.Top;
        _host.Child = g;
        _host.SizeChanged += (s, e) => Draw();
        _host.MouseLeftButtonDown += (s, e) => { _rot = true; _drag0 = e.GetPosition(_host); _yaw0 = _yaw; _pitch0 = _pitch; _host.CaptureMouse(); };
        _host.MouseMove += (s, e) =>
        {
            if (!_rot || _view.SelectedIndex != 0) return;
            var q = e.GetPosition(_host);
            _yaw = _yaw0 - (q.X - _drag0.X) * 0.4;
            _pitch = Math.Clamp(_pitch0 + (q.Y - _drag0.Y) * 0.4, -89, 89);
            Draw();
        };
        _host.MouseLeftButtonUp += (s, e) => { _rot = false; _host.ReleaseMouseCapture(); };

        var bar = DialogUi.Row(DialogUi.Label("Trường hợp / tổ hợp"), _case, DialogUi.Label("Khung nhìn"), _view, DialogUi.Label("Tầng"), _story,
                               DialogUi.Label("Tỉ lệ"), _scale, _scaleTxt, _undeformed, new Border { Width = 12 }, DialogUi.Btn("Đóng", (s, e) => Close()));
        var dock = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(bar, Dock.Top); dock.Children.Add(bar);
        dock.Children.Add(_host);
        Content = dock;
        Loaded += (s, e) => Draw();
    }

    public void SetCase(string name) { if (name != null) _case.SelectedItem = name; Draw(); }

    private Vec3 Project3(Vec3 v)
    {
        switch (_view.SelectedIndex)
        {
            case 1: return new Vec3(v.X, v.Y, v.Z);
            case 2: return new Vec3(v.X, v.Z, v.Y);
            case 3: return new Vec3(v.Y, v.Z, v.X);
        }
        double a = _yaw * Math.PI / 180, b = _pitch * Math.PI / 180;
        double x1 = v.X * Math.Cos(a) - v.Y * Math.Sin(a), y1 = v.X * Math.Sin(a) + v.Y * Math.Cos(a);
        // nhìn từ trên xuống nghiêng góc pitch: trục đứng màn hình = z·cos(b) + y1·sin(b)
        return new Vec3(x1, v.Z * Math.Cos(b) + y1 * Math.Sin(b), y1 * Math.Cos(b) - v.Z * Math.Sin(b));
    }

    private void Draw()
    {
        if (!IsLoaded) return;
        _canvas.Children.Clear();
        double W = _host.ActualWidth, H = _host.ActualHeight;
        _scaleTxt.Text = $"×{_scale.Value:0.0#}";
        _story.IsEnabled = _view.SelectedIndex == 1;
        string c = _case.SelectedItem as string;
        if (W < 50 || H < 50 || c == null) return;
        CaseResult cr;
        try { cr = _res.Get(c); } catch (Exception ex) { _info.Text = ex.Message; return; }
        var byId = _p.Nodes.ToDictionary(n => n.Id);

        // thanh hiển thị
        var frames = _p.Frames.Where(f => byId.ContainsKey(f.NodeI) && byId.ContainsKey(f.NodeJ)).ToList();
        if (_view.SelectedIndex == 1 && _story.SelectedItem is string stn)
        {
            var s = _p.Stories.FirstOrDefault(x => x.Name == stn);
            if (s != null)
                frames = frames.Where(f => !f.IsVerticalKind && Math.Abs(byId[f.NodeI].Z - s.Elevation) < 1e-3 && Math.Abs(byId[f.NodeJ].Z - s.Elevation) < 1e-3
                                           || f.IsVerticalKind && Math.Abs(Math.Max(byId[f.NodeI].Z, byId[f.NodeJ].Z) - s.Elevation) < 1e-3).ToList();
        }
        if (frames.Count == 0) { _info.Text = "Không có thanh để hiển thị."; return; }

        // tỉ lệ tự động: chuyển vị lớn nhất = 8 % kích thước mô hình
        var used = frames.SelectMany(f => new[] { f.NodeI, f.NodeJ }).Distinct().ToList();
        double dmax = 0; int nmax = 0;
        foreach (var id in used)
            if (cr.Disp.TryGetValue(id, out var d))
            {
                double m = _view.SelectedIndex == 1 ? Math.Sqrt(d[0] * d[0] + d[1] * d[1]) : Math.Sqrt(d[0] * d[0] + d[1] * d[1] + d[2] * d[2]);
                if (m > dmax) { dmax = m; nmax = id; }
            }
        double xs0 = used.Min(i => byId[i].X), xs1 = used.Max(i => byId[i].X), ys0 = used.Min(i => byId[i].Y), ys1 = used.Max(i => byId[i].Y);
        double zs0 = used.Min(i => byId[i].Z), zs1 = used.Max(i => byId[i].Z);
        double size = Math.Max(1, Math.Sqrt((xs1 - xs0) * (xs1 - xs0) + (ys1 - ys0) * (ys1 - ys0) + (zs1 - zs0) * (zs1 - zs0)));
        double k = dmax > 1e-12 ? 0.08 * size / dmax * _scale.Value : 0;

        // các đường: chưa biến dạng + biến dạng (toạ độ chiếu)
        var lines = new List<(List<Vec3> pts, Brush br, double th)>();
        foreach (var f in frames)
        {
            var a = byId[f.NodeI].P; var b = byId[f.NodeJ].P;
            if (_undeformed.IsChecked == true) lines.Add((new List<Vec3> { Project3(a), Project3(b) }, Undef, 1));
            lines.Add((Curve(f, a, b, cr, k).Select(Project3).ToList(), f.IsVerticalKind ? DefCol : DefBeam, 1.8));
        }
        var all = lines.SelectMany(l => l.pts).ToList();
        double px0 = all.Min(q => q.X), px1 = all.Max(q => q.X), py0 = all.Min(q => q.Y), py1 = all.Max(q => q.Y);
        double sc = Math.Min((W - 60) / Math.Max(px1 - px0, 1e-6), (H - 70) / Math.Max(py1 - py0, 1e-6));
        double ox = W / 2 - (px0 + px1) / 2 * sc, oy = H / 2 + 14 + (py0 + py1) / 2 * sc;
        Point S(Vec3 q) => new(ox + q.X * sc, oy - q.Y * sc);
        foreach (var (pts, br, th) in lines.OrderBy(l => l.br == Undef ? 0 : 1))
        {
            var pc = new PointCollection(pts.Select(S));
            _canvas.Children.Add(new Shapes.Polyline { Points = pc, Stroke = br, StrokeThickness = th, IsHitTestVisible = false });
        }
        if (nmax != 0 && cr.Disp.TryGetValue(nmax, out var dm))
        {
            var n = byId[nmax];
            var q = S(Project3(n.P + new Vec3(dm[0], dm[1], dm[2]) * k));
            var el = new Shapes.Ellipse { Width = 9, Height = 9, Fill = MaxBrush, IsHitTestVisible = false };
            Canvas.SetLeft(el, q.X - 4.5); Canvas.SetTop(el, q.Y - 4.5);
            _canvas.Children.Add(el);
            _info.Text = $"{c}: chuyển vị lớn nhất {dmax * 1000:0.##} mm tại nút {nmax} ({n.X:0.##}; {n.Y:0.##}; {n.Z:0.##}) – "
                         + $"UX = {dm[0] * 1000:0.##}, UY = {dm[1] * 1000:0.##}, UZ = {dm[2] * 1000:0.##} mm. Tỉ lệ phóng {k:0.#}×. "
                         + (_view.SelectedIndex == 0 ? "Kéo chuột để xoay." : "");
        }
        else _info.Text = $"{c}: chuyển vị bằng 0.";
    }

    /// <summary>Đường đàn hồi thanh: chuyển vị dọc trục nội suy tuyến tính, ngang trục nội suy Hermite (độ dốc = θ × e1).</summary>
    private static List<Vec3> Curve(FrameElement f, Vec3 a, Vec3 b, CaseResult cr, double k)
    {
        cr.Disp.TryGetValue(f.NodeI, out var di); cr.Disp.TryGetValue(f.NodeJ, out var dj);
        di ??= new double[6]; dj ??= new double[6];
        var e = b - a; double L = e.Length;
        if (L < 1e-9) return new List<Vec3> { a, b };
        var e1 = e * (1 / L);
        var ui = new Vec3(di[0], di[1], di[2]); var uj = new Vec3(dj[0], dj[1], dj[2]);
        bool relI = f.ReleaseI != null && f.ReleaseI.Length > 5 && (f.ReleaseI[4] || f.ReleaseI[5]);
        bool relJ = f.ReleaseJ != null && f.ReleaseJ.Length > 5 && (f.ReleaseJ[4] || f.ReleaseJ[5]);
        var si = relI ? new Vec3(0, 0, 0) : new Vec3(di[3], di[4], di[5]).Cross(e1);
        var sj = relJ ? new Vec3(0, 0, 0) : new Vec3(dj[3], dj[4], dj[5]).Cross(e1);
        var ti = ui - e1 * ui.Dot(e1); var tj = uj - e1 * uj.Dot(e1);
        double ai = ui.Dot(e1), aj = uj.Dot(e1);
        int n = 12;
        var pts = new List<Vec3>();
        for (int s = 0; s <= n; s++)
        {
            double t = (double)s / n;
            double h1 = 1 - 3 * t * t + 2 * t * t * t, h2 = L * (t - 2 * t * t + t * t * t), h3 = 3 * t * t - 2 * t * t * t, h4 = L * (-t * t + t * t * t);
            var tr = ti * h1 + si * h2 + tj * h3 + sj * h4;
            var ax = e1 * (ai + (aj - ai) * t);
            pts.Add(a + e * t + (tr + ax) * k);
        }
        return pts;
    }
}
