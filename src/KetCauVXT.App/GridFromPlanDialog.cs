using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;
using Microsoft.Win32;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>"Tải mặt bằng để lấy kích thước" cho ô Lưới trục và tầng:
/// • bản vẽ CAD (DWG/DXF): đọc trục tự động (layer nhóm Trục / tên trục; không có thì theo tim tường), chọn mặt bằng tầng, xem trước;
/// • ảnh mặt bằng (JPG/PNG/BMP, ảnh chụp / scan bản vẽ): hiệu chỉnh tỷ lệ bằng 1 kích thước đã biết rồi bấm vị trí các trục.
/// Kết quả điền vào Bước cột X, Y và Chiều cao các tầng (sửa lại được trước khi đồng ý). Lõi: Core/Import/Arch/GridFromPlan.cs.</summary>
public sealed class GridFromPlanDialog : Window
{
    public string SpansX => _tx.Text.Trim();
    public string SpansY => _ty.Text.Trim();
    public string Heights => _th.Text.Trim();

    private readonly TextBox _tx = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBox _ty = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBox _th = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBlock _notes = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 4, 6, 4), FontWeight = FontWeights.SemiBold };
    private readonly ComboBox _floor = new() { Width = 220, Margin = new Thickness(4, 0, 12, 0), IsEnabled = false };
    private readonly ComboBox _round = new() { Width = 90, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Grid _host = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Canvas _overlay = new() { Background = Brushes.Transparent };
    private readonly Image _img = new() { Stretch = Stretch.Fill };
    private readonly Slider _zoom = new() { Minimum = 0.2, Maximum = 4, Value = 1, Width = 140, Margin = new Thickness(4, 0, 0, 0) };
    private readonly RadioButton _mCal = new() { Content = "① Hiệu chỉnh tỷ lệ (bấm 2 điểm)", Margin = new Thickness(0, 0, 10, 0), IsChecked = true };
    private readonly RadioButton _mX = new() { Content = "② Bấm trục X (trục đứng 1, 2, 3…)", Margin = new Thickness(0, 0, 10, 0) };
    private readonly RadioButton _mY = new() { Content = "③ Bấm trục Y (trục ngang A, B, C…)", Margin = new Thickness(0, 0, 10, 0) };
    private readonly StackPanel _imgTools = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 2, 6, 2), Visibility = Visibility.Collapsed };

    // dữ liệu
    private ArchDrawing _dwg;
    private BitmapSource _bmp;
    private double _mPerPx;
    private readonly List<(double X, double Y)> _cal = new();
    private readonly List<double> _pxX = new(), _pxY = new();
    private double RoundM => _round.SelectedIndex switch { 0 => 0.01, 1 => 0.05, _ => 0.10 };

    public GridFromPlanDialog(string spansX, string spansY, string heights)
    {
        DialogUi.Setup(this, "Tải mặt bằng để lấy kích thước lưới trục và tầng", 1200, 800);
        ResizeMode = ResizeMode.CanResize;
        _tx.Text = spansX ?? ""; _ty.Text = spansY ?? ""; _th.Text = heights ?? "";
        _round.ItemsSource = new[] { "10 mm", "50 mm", "100 mm" }; _round.SelectedIndex = 0;
        _round.SelectionChanged += (s, e) => Recompute();

        var dock = new DockPanel();
        var bar = new WrapPanel { Margin = new Thickness(6) };
        var bCad = DialogUi.Btn("Mở bản vẽ CAD (DWG / DXF)…", (s, e) => OpenCad(), true);
        var bImg = DialogUi.Btn("Mở ảnh mặt bằng (JPG / PNG)…", (s, e) => OpenImage());
        bar.Children.Add(bCad); bar.Children.Add(bImg);
        bar.Children.Add(new TextBlock { Text = "  Mặt bằng:", VerticalAlignment = VerticalAlignment.Center }); bar.Children.Add(_floor);
        bar.Children.Add(new TextBlock { Text = "Làm tròn:", VerticalAlignment = VerticalAlignment.Center }); bar.Children.Add(_round);
        bar.Children.Add(new TextBlock { Text = "Thu phóng:", VerticalAlignment = VerticalAlignment.Center }); bar.Children.Add(_zoom);
        DockPanel.SetDock(bar, Dock.Top); dock.Children.Add(bar);

        _imgTools.Children.Add(_mCal); _imgTools.Children.Add(_mX); _imgTools.Children.Add(_mY);
        _imgTools.Children.Add(DialogUi.Btn("Xoá trục vừa bấm", (s, e) => { var l = _mY.IsChecked == true ? _pxY : _pxX; if (l.Count > 0) l.RemoveAt(l.Count - 1); Recompute(); }));
        _imgTools.Children.Add(DialogUi.Btn("Xoá hết", (s, e) => { _pxX.Clear(); _pxY.Clear(); _cal.Clear(); _mPerPx = 0; Recompute(); }));
        DockPanel.SetDock(_imgTools, Dock.Top); dock.Children.Add(_imgTools);
        DockPanel.SetDock(_hint, Dock.Top); dock.Children.Add(_hint);
        foreach (var rb in new[] { _mCal, _mX, _mY }) rb.Checked += (s, e) => UpdateHint();

        // phải: kết quả
        var right = new StackPanel { Width = 330, Margin = new Thickness(8) };
        right.Children.Add(DialogUi.Label("Bước cột theo X (trục 1, 2, 3…) – m", double.NaN, true)); right.Children.Add(_tx);
        right.Children.Add(DialogUi.Label("Bước cột theo Y (trục A, B, C…) – m", double.NaN, true)); right.Children.Add(_ty);
        right.Children.Add(DialogUi.Label("Chiều cao các tầng (dưới → trên) – m", double.NaN, true)); right.Children.Add(_th);
        right.Children.Add(DialogUi.Hint("Định dạng như ô nhập: 3.6 4.2 hoặc 3*3.3. Sửa được trước khi đồng ý."));
        right.Children.Add(_notes);
        var ok = DialogUi.Btn("Đồng ý – điền vào Lưới trục và tầng", (s, e) => DialogResult = true, true);
        ok.Margin = new Thickness(0, 14, 0, 0);
        right.Children.Add(ok);
        right.Children.Add(DialogUi.Btn("Huỷ", (s, e) => DialogResult = false));
        DockPanel.SetDock(right, Dock.Right); dock.Children.Add(right);

        _host.Children.Add(_img);
        _host.Children.Add(_overlay);
        var sv = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.WhiteSmoke, Content = _host };
        dock.Children.Add(sv);
        Content = dock;
        _zoom.ValueChanged += (s, e) => Redraw();
        _overlay.MouseLeftButtonDown += OnClick;
        UpdateHint();
    }

    private void UpdateHint()
    {
        if (_dwg == null && _bmp == null) { _hint.Text = "Mở bản vẽ CAD mặt bằng (chương trình tự đọc trục) hoặc ảnh chụp / scan mặt bằng (bấm vị trí trục)."; return; }
        if (_dwg != null) { _hint.Text = "Đã đọc trục từ bản vẽ (nét đỏ). Chọn mặt bằng tầng khác nếu cần, kiểm tra / sửa kích thước bên phải rồi bấm Đồng ý."; return; }
        _hint.Text = _mCal.IsChecked == true
            ? (_cal.Count == 0 ? "Bước ①: bấm điểm đầu của một kích thước đã biết trên ảnh (vd. 2 trục đầu – cuối có ghi kích thước)." : "Bấm điểm thứ hai, rồi nhập chiều dài thật (mm).")
            : _mX.IsChecked == true ? "Bước ②: bấm lần lượt lên từng trục ĐỨNG (trục 1, 2, 3…) – bấm ở vị trí bất kỳ dọc theo trục."
            : "Bước ③: bấm lần lượt lên từng trục NGANG (trục A, B, C…).";
        if (_mPerPx <= 0 && _mCal.IsChecked != true) _hint.Text += "  (Chưa hiệu chỉnh tỷ lệ – làm bước ① trước.)";
    }

    // ------------------------------------------------------------------
    // CAD
    // ------------------------------------------------------------------
    private void OpenCad()
    {
        var dlg = new OpenFileDialog { Filter = "Bản vẽ AutoCAD (*.dwg;*.dxf)|*.dwg;*.dxf|Tất cả (*.*)|*.*", Title = "Chọn bản vẽ mặt bằng kiến trúc" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            Cursor = Cursors.Wait;
            _dwg = ArchPlanReader.Read(dlg.FileName, new ArchImportOptions());
            _bmp = null; _img.Source = null; _imgTools.Visibility = Visibility.Collapsed;
            _floor.ItemsSource = _dwg.Floors.Select((f, i) => (string.IsNullOrWhiteSpace(f.Title) ? "Mặt bằng " + (i + 1) : f.Title) + " – " + f.Grids.Count + " trục").ToList();
            _floor.IsEnabled = _dwg.Floors.Count > 1;
            _floor.SelectionChanged -= FloorChanged; _floor.SelectedIndex = -1;
            var r = GridFromPlan.FromDrawing(_dwg, -1, RoundM);
            _floor.SelectedIndex = Math.Max(0, PickedIndex());
            _floor.SelectionChanged += FloorChanged;
            Apply(r, true);
        }
        catch (Exception ex) { MessageBox.Show(this, "Không đọc được bản vẽ:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { Cursor = null; }
        UpdateHint();
    }

    private int PickedIndex()
    {
        if (_dwg == null || _dwg.Floors.Count == 0) return -1;
        var floors = _dwg.Floors.OrderBy(f => f.FloorNo).ToList();
        var pick = floors.FirstOrDefault(f => f.Grids.Count(g => !g.Auto) >= 4) ?? floors[0];
        return _dwg.Floors.IndexOf(pick);
    }

    private void FloorChanged(object s, SelectionChangedEventArgs e) { if (_dwg != null && _floor.SelectedIndex >= 0) Apply(GridFromPlan.FromDrawing(_dwg, _floor.SelectedIndex, RoundM), false); }

    private GridFromPlan.Result _last;
    private void Apply(GridFromPlan.Result r, bool withHeights)
    {
        _last = r;
        if (r.SpansX.Count > 0) _tx.Text = r.SpansXText;
        if (r.SpansY.Count > 0) _ty.Text = r.SpansYText;
        if (withHeights && r.Heights.Count >= 2) _th.Text = r.HeightsText;
        _notes.Text = string.Join("\n", r.Notes.Concat(_dwg?.Warnings ?? new List<string>()).Distinct().Take(8))
                      + (r.Heights.Count >= 2 ? "\nChiều cao tầng lấy theo các mặt bằng tầng trên bản vẽ – kiểm tra lại với mặt cắt." : "");
        Redraw();
    }

    private void Recompute()
    {
        if (_dwg != null) { Apply(GridFromPlan.FromDrawing(_dwg, _floor.SelectedIndex, RoundM), false); return; }
        if (_bmp != null && _mPerPx > 0)
        {
            var sx = GridFromPlan.SpansFromPixels(_pxX, _mPerPx, RoundM);
            var sy = GridFromPlan.SpansFromPixels(_pxY, _mPerPx, RoundM);
            if (sx.Count > 0) _tx.Text = GridFromPlan.SpansText(sx);
            if (sy.Count > 0) _ty.Text = GridFromPlan.SpansText(sy);
            _notes.Text = $"Tỷ lệ: 1 điểm ảnh = {_mPerPx * 1000:0.##} mm. Trục X: {_pxX.Count}, trục Y: {_pxY.Count}. Bấm tay nên làm tròn 50 mm; kiểm tra lại với kích thước ghi trên bản vẽ.";
        }
        Redraw();
        UpdateHint();
    }

    // ------------------------------------------------------------------
    // ẢNH
    // ------------------------------------------------------------------
    private void OpenImage()
    {
        var dlg = new OpenFileDialog { Filter = "Ảnh mặt bằng (*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|Tất cả (*.*)|*.*", Title = "Chọn ảnh mặt bằng" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.UriSource = new Uri(dlg.FileName); bi.EndInit(); bi.Freeze();
            _bmp = bi; _dwg = null; _last = null;
            _img.Source = bi;
            _cal.Clear(); _pxX.Clear(); _pxY.Clear(); _mPerPx = 0;
            _floor.ItemsSource = null; _floor.IsEnabled = false;
            _imgTools.Visibility = Visibility.Visible; _mCal.IsChecked = true;
            _zoom.Value = Math.Clamp(900.0 / Math.Max(bi.PixelWidth, 1), 0.2, 4);
            _notes.Text = "Ảnh " + bi.PixelWidth + " × " + bi.PixelHeight + " điểm ảnh. Ảnh nên chụp thẳng, không méo phối cảnh.";
            Redraw(); UpdateHint();
        }
        catch (Exception ex) { MessageBox.Show(this, "Không mở được ảnh:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        if (_bmp == null) return;
        var p = e.GetPosition(_overlay);
        double k = _zoom.Value;
        double px = p.X / k, py = p.Y / k;   // điểm ảnh gốc
        if (_mCal.IsChecked == true)
        {
            if (_cal.Count >= 2) _cal.Clear();
            _cal.Add((px, py));
            if (_cal.Count == 2)
            {
                Redraw();
                string v = DialogUi.InputBox(this, "Hiệu chỉnh tỷ lệ", "Chiều dài thật giữa 2 điểm vừa bấm (mm):", "3600");
                if (v != null && double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double mm) && mm > 0)
                {
                    _mPerPx = GridFromPlan.MetresPerPixel(_cal[0], _cal[1], mm / 1000);
                    _mX.IsChecked = true;
                }
                else _cal.Clear();
            }
        }
        else if (_mX.IsChecked == true) _pxX.Add(px);
        else _pxY.Add(py);
        Recompute();
    }

    // ------------------------------------------------------------------
    // VẼ
    // ------------------------------------------------------------------
    private void Redraw()
    {
        _overlay.Children.Clear();
        double k = _zoom.Value;
        if (_bmp != null)
        {
            double w = _bmp.PixelWidth * k, h = _bmp.PixelHeight * k;
            _img.Width = w; _img.Height = h; _overlay.Width = w; _overlay.Height = h; _host.Width = w; _host.Height = h;
            foreach (var x in _pxX) AddLine(x * k, 0, x * k, h, Brushes.Red, 1.5);
            foreach (var y in _pxY) AddLine(0, y * k, w, y * k, Brushes.Blue, 1.5);
            for (int i = 0; i < _pxX.Count; i++) AddText((i + 1).ToString(), _pxX[i] * k + 3, 4, Brushes.Red);
            for (int i = 0; i < _pxY.Count; i++) AddText(((char)('A' + Math.Min(i, 25))).ToString(), 4, _pxY[i] * k + 2, Brushes.Blue);
            foreach (var c in _cal) AddDot(c.X * k, c.Y * k, Brushes.Orange);
            if (_cal.Count == 2) AddLine(_cal[0].X * k, _cal[0].Y * k, _cal[1].X * k, _cal[1].Y * k, Brushes.Orange, 2);
            return;
        }
        _img.Source = null;
        if (_dwg == null || _dwg.Floors.Count == 0) { _host.Width = 600; _host.Height = 400; _overlay.Width = 600; _overlay.Height = 400; return; }
        var f = _dwg.Floors[Math.Clamp(_floor.SelectedIndex < 0 ? PickedIndex() : _floor.SelectedIndex, 0, _dwg.Floors.Count - 1)];
        var lines = f.Preview;
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var l in lines) { x0 = Math.Min(x0, Math.Min(l.X1, l.X2)); x1 = Math.Max(x1, Math.Max(l.X1, l.X2)); y0 = Math.Min(y0, Math.Min(l.Y1, l.Y2)); y1 = Math.Max(y1, Math.Max(l.Y1, l.Y2)); }
        foreach (var g in f.Grids) { if (g.Dir == KetCauVXT.Model.GridDir.X) { x0 = Math.Min(x0, g.Coord); x1 = Math.Max(x1, g.Coord); } else { y0 = Math.Min(y0, g.Coord); y1 = Math.Max(y1, g.Coord); } }
        if (x1 <= x0 || y1 <= y0) return;
        double pad = 40, sc = 850.0 / Math.Max(x1 - x0, y1 - y0) * k;
        double W = (x1 - x0) * sc + 2 * pad, H = (y1 - y0) * sc + 2 * pad;
        _host.Width = W; _host.Height = H; _overlay.Width = W; _overlay.Height = H;
        double X(double x) => pad + (x - x0) * sc;
        double Y(double y) => H - pad - (y - y0) * sc;
        foreach (var l in lines.Take(20000))
            AddLine(X(l.X1), Y(l.Y1), X(l.X2), Y(l.Y2), l.Kind == 2 ? Brushes.IndianRed : Brushes.DimGray, l.Kind == 1 ? 1.2 : 0.6);
        foreach (var g in f.Grids)
        {
            if (g.Dir == KetCauVXT.Model.GridDir.X) { AddLine(X(g.Coord), 6, X(g.Coord), H - 6, Brushes.Red, 1.4, true); AddText(g.Name ?? "", X(g.Coord) + 3, 4, Brushes.Red); }
            else { AddLine(6, Y(g.Coord), W - 6, Y(g.Coord), Brushes.Red, 1.4, true); AddText(g.Name ?? "", 4, Y(g.Coord) - 16, Brushes.Red); }
        }
    }

    private void AddLine(double x1, double y1, double x2, double y2, Brush b, double t, bool dash = false)
    {
        var l = new Shapes.Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = b, StrokeThickness = t, IsHitTestVisible = false };
        if (dash) l.StrokeDashArray = new DoubleCollection { 8, 4 };
        _overlay.Children.Add(l);
    }

    private void AddText(string s, double x, double y, Brush b)
    {
        var t = new TextBlock { Text = s, Foreground = b, FontWeight = FontWeights.Bold, Background = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), IsHitTestVisible = false };
        Canvas.SetLeft(t, x); Canvas.SetTop(t, y); _overlay.Children.Add(t);
    }

    private void AddDot(double x, double y, Brush b)
    {
        var e = new Shapes.Ellipse { Width = 9, Height = 9, Fill = b, IsHitTestVisible = false };
        Canvas.SetLeft(e, x - 4.5); Canvas.SetTop(e, y - 4.5); _overlay.Children.Add(e);
    }
}
