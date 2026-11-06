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
using GridDir = KetCauVXT.Model.GridDir;
using CadCategory = KetCauVXT.Model.CadCategory;
using LayerMapEntry = KetCauVXT.Model.LayerMapEntry;

namespace KetCauVXT.App;

/// <summary>"Tải mặt bằng để lấy kích thước / dữ liệu" (chế độ Thiết kế: ô Lưới trục và tầng; chế độ Triển khai hồ sơ: mặt bằng kết cấu):
/// • bản vẽ CAD (DWG/DXF): hiện bản vẽ (cuộn chuột thu phóng, giữ nút giữa để kéo; bật / tắt nhóm nét theo màu), chọn NGUỒN TRỤC
///   (tự nhận theo layer trục – chọn được layer; tự nhận theo cột; người dùng bấm chọn nét trục / cột), TRỤC ĐI QUA tim cột hay mép cột
///   (mép trái/dưới, phải/trên, mép ngoài nhà), đơn vị bản vẽ, sai số; đọc ra bước trục, cột (tiết diện + lệch tâm), tường trên trục,
///   công năng ô sàn theo tên phòng, lỗ cầu thang / thông tầng; bảng "Áp dụng" chọn hạng mục cập nhật vào mô hình;
/// • ảnh mặt bằng (JPG/PNG/BMP): hiệu chỉnh tỷ lệ bằng 1 kích thước đã biết rồi bấm vị trí các trục.
/// Lõi: Core/Import/Arch/GridFromPlan.cs, PlanAxisPicker.cs.</summary>
public sealed class GridFromPlanDialog : Window
{
    public string SpansX => _tx.Text.Trim();
    public string SpansY => _ty.Text.Trim();
    public string Heights => _th.Text.Trim();
    /// <summary>Kết quả đọc bản vẽ CAD theo lựa chọn trục (null = ảnh / chưa mở bản vẽ).</summary>
    public AxisPickResult PickResult => _dwg != null ? _pick : null;
    /// <summary>Các hạng mục người dùng chọn áp dụng.</summary>
    public AxisApplyOptions ApplyOptions => new()
    {
        Grid = true, ColumnSections = _aSec.IsChecked == true, ColumnOffsets = _aOff.IsChecked == true, RemoveColumnsWithoutCad = _aDel.IsChecked == true,
        Walls = _aWall.IsChecked == true, RoomUses = _aRoom.IsChecked == true, Openings = _aOpen.IsChecked == true
    };
    /// <summary>Thiết kế: dựng lại mô hình theo lưới mới rồi áp dụng. Triển khai hồ sơ: đặt cột lên mặt bằng đang xem.</summary>
    public bool Rebuild => _aRebuild.IsChecked == true;
    public string CadPath { get; private set; }

    private readonly bool _detailing;
    private readonly TextBox _tx = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBox _ty = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBox _th = new() { Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBlock _notes = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 4, 6, 4), FontWeight = FontWeights.SemiBold };
    private readonly ComboBox _floor = new() { Width = 200, Margin = new Thickness(4, 0, 12, 0), IsEnabled = false };
    private readonly ComboBox _round = new() { Width = 80, Margin = new Thickness(4, 0, 12, 0) };
    private readonly Grid _host = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Canvas _overlay = new() { Background = Brushes.Transparent };
    private readonly Image _img = new() { Stretch = Stretch.Fill };
    private readonly ScrollViewer _sv = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.White };
    private readonly Slider _zoom = new() { Minimum = 0.2, Maximum = 8, Value = 1, Width = 120, Margin = new Thickness(4, 0, 0, 0) };
    private readonly RadioButton _mCal = new() { Content = "① Hiệu chỉnh tỷ lệ (bấm 2 điểm)", Margin = new Thickness(0, 0, 10, 0), IsChecked = true };
    private readonly RadioButton _mX = new() { Content = "② Bấm trục X (trục đứng 1, 2, 3…)", Margin = new Thickness(0, 0, 10, 0) };
    private readonly RadioButton _mY = new() { Content = "③ Bấm trục Y (trục ngang A, B, C…)", Margin = new Thickness(0, 0, 10, 0) };
    private readonly StackPanel _imgTools = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 2, 6, 2), Visibility = Visibility.Collapsed };

    // ---- lựa chọn CAD ----
    private readonly StackPanel _cadPanel = new() { Margin = new Thickness(0, 4, 0, 4), Visibility = Visibility.Collapsed };
    private readonly ComboBox _src = new() { Margin = new Thickness(0, 2, 0, 4), ItemsSource = new[] { "Tự nhận theo layer trục", "Tự nhận theo cột", "Người dùng chọn trục" }, SelectedIndex = 0 };
    private readonly ComboBox _through = new() { Margin = new Thickness(0, 2, 0, 4), ItemsSource = new[] { "Tim cột", "Mép cột – mép trái / dưới", "Mép cột – mép phải / trên", "Mép cột – mép ngoài nhà (trục biên), tim cột (trục giữa)" }, SelectedIndex = 0 };
    private readonly ComboBox _unit = new() { Width = 90, ItemsSource = new[] { "Tự động", "mm", "cm", "m" }, SelectedIndex = 0 };
    private readonly TextBox _tol = new() { Width = 50, Text = "50" };
    private readonly RadioButton _pickX = new() { Content = "trục đứng (X: 1, 2, 3…)", IsChecked = true, Margin = new Thickness(0, 0, 8, 0) };
    private readonly RadioButton _pickY = new() { Content = "trục ngang (Y: A, B, C…)" };
    private readonly StackPanel _userPanel = new() { Visibility = Visibility.Collapsed };
    private readonly ListBox _axisList = new() { Height = 110, Margin = new Thickness(0, 2, 0, 2) };
    private readonly TextBox _axisName = new() { Width = 60, Margin = new Thickness(0, 0, 6, 0) };
    private readonly WrapPanel _layerPanel = new() { Margin = new Thickness(0, 2, 0, 2) };
    private readonly WrapPanel _showPanel = new() { Margin = new Thickness(0, 2, 0, 2) };
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) };
    private readonly CheckBox _aSec = new() { Content = "Tiết diện cột theo bản vẽ", IsChecked = true };
    private readonly CheckBox _aOff = new() { Content = "Lệch tâm cột (trục qua mép cột)", IsChecked = true };
    private readonly CheckBox _aDel = new() { Content = "Xoá cột ở giao trục bản vẽ không có cột", IsChecked = false };
    private readonly CheckBox _aWall = new() { Content = "Tường trên trục → tải tường lên dầm", IsChecked = true };
    private readonly CheckBox _aRoom = new() { Content = "Công năng ô sàn theo tên phòng", IsChecked = true };
    private readonly CheckBox _aOpen = new() { Content = "Lỗ sàn cầu thang / thông tầng", IsChecked = true };
    private readonly CheckBox _aRebuild = new() { IsChecked = true, FontWeight = FontWeights.SemiBold };
    // nhóm nét hiển thị: 0 khác, 1 tường, 2 trục, 3 cột, 4 cửa, 5 thang, 6 dầm, 7 sàn
    private static readonly string[] KindNames = { "Khác", "Tường", "Trục", "Cột", "Cửa", "Thang", "Dầm", "Sàn" };
    private static readonly Brush[] KindBrush = { Brushes.Silver, Brushes.DimGray, Brushes.IndianRed, Brushes.DarkOrange, Brushes.SeaGreen, Brushes.MediumPurple, Brushes.SteelBlue, Brushes.CadetBlue };
    private readonly bool[] _show = { true, true, true, true, true, true, true, true };

    // dữ liệu
    private ArchDrawing _dwg;
    private ArchFloorPlan _planUsed;
    private AxisPickResult _pick;
    private readonly List<PickedAxis> _userAxes = new();
    private BitmapSource _bmp;
    private double _mPerPx;
    private readonly List<(double X, double Y)> _cal = new();
    private readonly List<double> _pxX = new(), _pxY = new();
    private double RoundM => _round.SelectedIndex switch { 0 => 0.01, 1 => 0.05, _ => 0.10 };
    private double TolM => Math.Clamp(DialogUi.ParseNum(_tol.Text, 50), 1, 500) / 1000.0;
    // ánh xạ bản vẽ → hình
    private double _x0, _y0, _sc = 1, _pad = 40, _H;
    private Point? _pan;

    public GridFromPlanDialog(string spansX, string spansY, string heights, bool detailing = false)
    {
        _detailing = detailing;
        DialogUi.Setup(this, "Mở mặt bằng để lấy dữ liệu lưới trục, cột, tường, công năng", 1320, 860);
        ResizeMode = ResizeMode.CanResize;
        _tx.Text = spansX ?? ""; _ty.Text = spansY ?? ""; _th.Text = heights ?? "";
        _round.ItemsSource = new[] { "10 mm", "50 mm", "100 mm" }; _round.SelectedIndex = 0;
        _round.SelectionChanged += (s, e) => Recompute();
        _aRebuild.Content = detailing ? "Đặt cột lên mặt bằng kết cấu đang xem (vị trí thật = trục + lệch tâm)" : "Dựng lại mô hình theo lưới mới rồi áp dụng các mục trên";
        if (detailing) { _aWall.Visibility = _aRoom.Visibility = _aOpen.Visibility = _aDel.Visibility = Visibility.Collapsed; _th.IsEnabled = false; }

        var dock = new DockPanel();
        var bar = new WrapPanel { Margin = new Thickness(6) };
        bar.Children.Add(DialogUi.Btn("Mở bản vẽ CAD (DWG / DXF)…", (s, e) => OpenCad(), true));
        bar.Children.Add(DialogUi.Btn("Mở ảnh mặt bằng (JPG / PNG)…", (s, e) => OpenImage()));
        bar.Children.Add(DialogUi.Btn("Chuẩn hoá layer…", (s, e) => StandardizeLayers()));
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

        // ---- phải: lựa chọn + kết quả ----
        var right = new StackPanel { Margin = new Thickness(8) };
        BuildCadPanel();
        right.Children.Add(_cadPanel);
        right.Children.Add(DialogUi.Label("Bước cột theo X (trục 1, 2, 3…) – m", double.NaN, true)); right.Children.Add(_tx);
        right.Children.Add(DialogUi.Label("Bước cột theo Y (trục A, B, C…) – m", double.NaN, true)); right.Children.Add(_ty);
        if (!detailing) { right.Children.Add(DialogUi.Label("Chiều cao các tầng (dưới → trên) – m", double.NaN, true)); right.Children.Add(_th); }
        right.Children.Add(DialogUi.Hint("Định dạng như ô nhập: 3.6 4.2 hoặc 3*3.3. Sửa được trước khi đồng ý."));
        right.Children.Add(_notes);
        var ok = DialogUi.Btn(detailing ? "Đồng ý – cập nhật mặt bằng kết cấu" : "Đồng ý – cập nhật lưới trục và mô hình", (s, e) => DialogResult = true, true);
        ok.Margin = new Thickness(0, 12, 0, 4);
        right.Children.Add(ok);
        right.Children.Add(DialogUi.Btn("Huỷ", (s, e) => DialogResult = false));
        var rsv = new ScrollViewer { Width = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = right };
        DockPanel.SetDock(rsv, Dock.Right); dock.Children.Add(rsv);

        _host.Children.Add(_img);
        _host.Children.Add(_overlay);
        _sv.Content = _host;
        dock.Children.Add(_sv);
        Content = dock;
        _zoom.ValueChanged += (s, e) => Redraw();
        _overlay.MouseLeftButtonDown += OnClick;
        _overlay.MouseRightButtonDown += OnRightClick;
        _sv.PreviewMouseWheel += (s, e) =>
        {
            if (_dwg == null && _bmp == null) return;
            _zoom.Value = Math.Clamp(_zoom.Value * (e.Delta > 0 ? 1.15 : 1 / 1.15), _zoom.Minimum, _zoom.Maximum);
            e.Handled = true;
        };
        _sv.PreviewMouseDown += (s, e) => { if (e.ChangedButton == MouseButton.Middle) { _pan = e.GetPosition(_sv); _sv.CaptureMouse(); e.Handled = true; } };
        _sv.PreviewMouseMove += (s, e) =>
        {
            if (_pan is not Point p0) return;
            var p = e.GetPosition(_sv);
            _sv.ScrollToHorizontalOffset(_sv.HorizontalOffset - (p.X - p0.X)); _sv.ScrollToVerticalOffset(_sv.VerticalOffset - (p.Y - p0.Y));
            _pan = p;
        };
        _sv.PreviewMouseUp += (s, e) => { if (e.ChangedButton == MouseButton.Middle && _pan != null) { _pan = null; _sv.ReleaseMouseCapture(); } };
        UpdateHint();
    }

    private void BuildCadPanel()
    {
        var p = _cadPanel;
        p.Children.Add(DialogUi.Label("NGUỒN TRỤC", double.NaN, true));
        p.Children.Add(_src);
        p.Children.Add(DialogUi.Label("Trục đi qua", double.NaN, true));
        p.Children.Add(_through);
        p.Children.Add(DialogUi.Hint("Khi trục qua MÉP cột, tâm cột lệch khỏi trục ±b/2: chương trình lưu lệch tâm cho từng cột (mặt bằng, móng vẽ đúng vị trí thật; phân tích theo trục)."));
        var row = new WrapPanel();
        row.Children.Add(DialogUi.Label("Đơn vị bản vẽ:")); row.Children.Add(_unit);
        row.Children.Add(DialogUi.Label("  Sai số (mm):")); row.Children.Add(_tol);
        p.Children.Add(row);

        _userPanel.Children.Add(DialogUi.Hint("Bấm lên NÉT trục trên hình → thêm trục theo phương nét. Bấm lên CỘT → thêm trục qua cột (tim / mép theo \"Trục đi qua\") theo phương:"));
        _userPanel.Children.Add(DialogUi.Row(_pickX, _pickY));
        _userPanel.Children.Add(DialogUi.Hint("Chuột phải lên trục để xoá. \"Lấy trục tự nhận\" chép trục layer / cột vào danh sách để sửa tay."));
        _userPanel.Children.Add(DialogUi.Row(DialogUi.Btn("Lấy trục tự nhận", (s, e) => CopyAutoAxes()), DialogUi.Btn("Xoá hết", (s, e) => { _userAxes.Clear(); Recompute(); })));
        p.Children.Add(_userPanel);
        p.Children.Add(DialogUi.Label("Các trục (chọn để đổi tên / xoá)", double.NaN, true));
        p.Children.Add(_axisList);
        p.Children.Add(DialogUi.Row(DialogUi.Label("Tên:"), _axisName, DialogUi.Btn("Đổi tên", (s, e) => RenameAxis()), DialogUi.Btn("Xoá trục", (s, e) => RemoveAxis()),
                                    DialogUi.Btn("Tên tự động", (s, e) => { EnsureUser(); PlanAxisPicker.AutoName(_userAxes.Where(a => a.Dir == GridDir.X).ToList()); PlanAxisPicker.AutoName(_userAxes.Where(a => a.Dir == GridDir.Y).ToList()); Recompute(); })));
        _axisList.SelectionChanged += (s, e) => { if (_axisList.SelectedItem is PickedAxis a) _axisName.Text = a.Name; };

        var lx = new Expander { Header = "Layer trục (chọn layer rồi đọc lại)", Margin = new Thickness(0, 4, 0, 0) };
        var lp = new StackPanel();
        lp.Children.Add(new ScrollViewer { MaxHeight = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _layerPanel });
        lp.Children.Add(DialogUi.Btn("Đọc lại với layer trục đã chọn", (s, e) => Reload()));
        lx.Content = lp;
        p.Children.Add(lx);
        p.Children.Add(DialogUi.Label("Hiển thị nét", double.NaN, true));
        for (int k = 0; k < KindNames.Length; k++)
        {
            int kk = k;
            var cb = new CheckBox { Content = KindNames[k], IsChecked = true, Foreground = KindBrush[k], FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 8, 2) };
            cb.Checked += (s, e) => { _show[kk] = true; Redraw(); };
            cb.Unchecked += (s, e) => { _show[kk] = false; Redraw(); };
            _showPanel.Children.Add(cb);
        }
        p.Children.Add(_showPanel);
        p.Children.Add(DialogUi.Label("THÔNG SỐ ĐỌC ĐƯỢC", double.NaN, true));
        p.Children.Add(_summary);
        p.Children.Add(DialogUi.Label("ÁP DỤNG", double.NaN, true));
        foreach (var cb in new[] { _aSec, _aOff, _aDel, _aWall, _aRoom, _aOpen, _aRebuild }) { cb.Margin = new Thickness(0, 1, 0, 1); p.Children.Add(cb); }

        _src.SelectionChanged += (s, e) =>
        {
            _userPanel.Visibility = _src.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            if (_src.SelectedIndex == 2 && _userAxes.Count == 0) CopyAutoAxes(false);
            Recompute(); UpdateHint();
        };
        _through.SelectionChanged += (s, e) => Recompute();
        _tol.LostFocus += (s, e) => Recompute();
        _unit.SelectionChanged += (s, e) => { if (CadPath != null) Reload(); };
    }

    private AxisThrough Through => _through.SelectedIndex switch { 1 => AxisThrough.EdgeMin, 2 => AxisThrough.EdgeMax, 3 => AxisThrough.Outside, _ => AxisThrough.Center };
    private AxisSource Source => _src.SelectedIndex switch { 1 => AxisSource.Columns, 2 => AxisSource.User, _ => AxisSource.Layer };

    private void UpdateHint()
    {
        if (_dwg == null && _bmp == null) { _hint.Text = "Mở bản vẽ CAD mặt bằng (chương trình tự đọc trục, cột, tường, tên phòng) hoặc ảnh chụp / scan mặt bằng (bấm vị trí trục)."; return; }
        if (_dwg != null)
        {
            _hint.Text = Source switch
            {
                AxisSource.Columns => "Trục lập theo hàng cột (nét đỏ) – chọn \"Trục đi qua\" tim / mép cột. Cột cam = lệch tâm so với trục, xanh = đúng tim.",
                AxisSource.User => "Bấm lên nét trục hoặc lên cột để thêm trục; chuột phải lên trục để xoá. Cuộn chuột thu phóng, giữ nút giữa để kéo.",
                _ => "Trục đọc từ layer trục (nét đỏ). Chọn layer khác ở mục \"Layer trục\" nếu cần; kiểm tra thông số bên phải rồi bấm Đồng ý."
            };
            return;
        }
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
        var dlg = new OpenFileDialog { Filter = "Bản vẽ AutoCAD (*.dwg;*.dxf)|*.dwg;*.dxf|Tất cả (*.*)|*.*", Title = "Chọn bản vẽ mặt bằng" };
        if (dlg.ShowDialog(this) != true) return;
        CadPath = dlg.FileName;
        _stdMap = new(); _stdProfile = null;
        _userAxes.Clear();
        _layerPanel.Children.Clear();
        Reload();
    }

    // ---- chuẩn hoá layer (không sửa file gốc): layer → nhóm cấu kiện đưa vào khi đọc ----
    private List<LayerMapEntry> _stdMap = new();
    private LayerProfile _stdProfile;

    private void StandardizeLayers()
    {
        if (CadPath == null) { MessageBox.Show(this, "Mở bản vẽ CAD (DWG / DXF) trước.", Title); return; }
        var dlg = new LayerStdDialog(CadPath, _unit.SelectedIndex switch { 1 => 0.001, 2 => 0.01, 3 => 1, _ => 0 }, _stdProfile) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _stdMap = dlg.Map;
        _stdProfile = dlg.Profile;
        _layerPanel.Children.Clear();   // dựng lại danh sách layer trục theo nhóm mới
        Reload();
    }

    /// <summary>Đọc (lại) bản vẽ với đơn vị và layer trục đang chọn.</summary>
    private void Reload()
    {
        if (CadPath == null) return;
        try
        {
            Cursor = Cursors.Wait;
            var o = new ArchImportOptions { ScaleOverride = _unit.SelectedIndex switch { 1 => 0.001, 2 => 0.01, 3 => 1, _ => 0 } };
            var chosen = _layerPanel.Children.OfType<CheckBox>().ToList();
            if (chosen.Count > 0)
                foreach (var cb in chosen)
                    o.LayerMap.Add(new LayerMapEntry((string)cb.Tag, cb.IsChecked == true ? CadCategory.Truc : (cb.ToolTip as string == CadCategory.Truc ? CadCategory.BoQua : "")));
            // layer đã chuẩn hoá ("Chuẩn hoá layer…") – sau lựa chọn layer trục ở trên
            foreach (var m in _stdMap) if (!o.LayerMap.Any(q => string.Equals(q.Layer, m.Layer, StringComparison.OrdinalIgnoreCase))) o.LayerMap.Add(new LayerMapEntry(m.Layer, m.Category));
            o.LayerMap.RemoveAll(m => string.IsNullOrEmpty(m.Category));
            _dwg = ArchPlanReader.Read(CadPath, o);
            _bmp = null; _img.Source = null; _imgTools.Visibility = Visibility.Collapsed; _cadPanel.Visibility = Visibility.Visible;
            _floor.SelectionChanged -= FloorChanged;
            _floor.ItemsSource = _dwg.Floors.Select((f, i) => (string.IsNullOrWhiteSpace(f.Title) ? "Mặt bằng " + (i + 1) : f.Title) + " – " + f.Grids.Count + " trục, " + f.Columns.Count + " cột").ToList();
            _floor.IsEnabled = _dwg.Floors.Count > 1;
            _floor.SelectedIndex = Math.Max(0, PickedIndex());
            _floor.SelectionChanged += FloorChanged;
            if (chosen.Count == 0)
                foreach (var kv in _dwg.LayerCategories.OrderBy(k => k.Value == CadCategory.Truc ? 0 : 1).ThenBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    _layerPanel.Children.Add(new CheckBox { Content = kv.Key + (string.IsNullOrEmpty(kv.Value) ? "" : " (" + CadCategory.Label(kv.Value) + ")"), Tag = kv.Key, ToolTip = kv.Value,
                                                            IsChecked = kv.Value == CadCategory.Truc, Margin = new Thickness(0, 0, 10, 2) });
            var r = GridFromPlan.FromDrawing(_dwg, -1, RoundM);
            if (r.Heights.Count >= 2 && !_detailing) _th.Text = r.HeightsText;
            _zoom.Value = 1;
            Recompute();
        }
        catch (Exception ex) { MessageBox.Show(this, "Không đọc được bản vẽ:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { Cursor = null; }
        UpdateHint();
    }

    private int PickedIndex()
    {
        if (_dwg == null || _dwg.Floors.Count == 0) return -1;
        var floors = _dwg.Floors.OrderBy(f => f.FloorNo).ToList();
        var pick = floors.FirstOrDefault(f => f.Grids.Count(g => !g.Auto) >= 4) ?? floors.FirstOrDefault(f => f.Columns.Count >= 4) ?? floors[0];
        return _dwg.Floors.IndexOf(pick);
    }

    private void FloorChanged(object s, SelectionChangedEventArgs e) { if (_dwg != null) { _userAxes.Clear(); Recompute(); } }

    private ArchFloorPlan CurrentPlan => _dwg == null || _dwg.Floors.Count == 0 ? null
        : _dwg.Floors[Math.Clamp(_floor.SelectedIndex < 0 ? PickedIndex() : _floor.SelectedIndex, 0, _dwg.Floors.Count - 1)];

    private AxisPickOptions Options => new() { Source = Source, Through = Through, Tolerance = TolM, RoundM = RoundM };

    private void CopyAutoAxes(bool recompute = true)
    {
        var p = CurrentPlan; if (p == null) return;
        var src = Source == AxisSource.User ? AxisSource.Layer : Source;
        var r = PlanAxisPicker.Read(p, new AxisPickOptions { Source = src, Through = Through, Tolerance = TolM, RoundM = RoundM });
        if (r.AxesX.Count + r.AxesY.Count == 0 && src == AxisSource.Layer) r = PlanAxisPicker.Read(p, new AxisPickOptions { Source = AxisSource.Columns, Through = Through, Tolerance = TolM });
        _userAxes.Clear();
        _userAxes.AddRange(r.AxesX.Concat(r.AxesY).Select(a => new PickedAxis { Name = a.Name, Dir = a.Dir, Coord = a.Coord, Through = a.Through, Source = a.Source }));
        if (recompute) Recompute();
    }

    /// <summary>Sửa tay trục tự nhận → chuyển sang "Người dùng chọn" với các trục hiện có.</summary>
    private void EnsureUser()
    {
        if (Source == AxisSource.User) return;
        CopyAutoAxes(false);
        _src.SelectedIndex = 2;
    }

    private void RenameAxis()
    {
        if (_axisList.SelectedItem is not PickedAxis a || string.IsNullOrWhiteSpace(_axisName.Text)) return;
        double c = a.Coord; var d = a.Dir;
        EnsureUser();
        var u = _userAxes.FirstOrDefault(q => q.Dir == d && Math.Abs(q.Coord - c) < 1e-6);
        if (u != null) u.Name = _axisName.Text.Trim();
        Recompute();
    }

    private void RemoveAxis()
    {
        if (_axisList.SelectedItem is not PickedAxis a) return;
        double c = a.Coord; var d = a.Dir;
        EnsureUser();
        _userAxes.RemoveAll(q => q.Dir == d && Math.Abs(q.Coord - c) < 1e-6);
        Recompute();
    }

    private void Recompute()
    {
        if (_dwg != null)
        {
            var p = CurrentPlan;
            _planUsed = p;
            _pick = PlanAxisPicker.Read(p, Options, _userAxes);
            if (_pick.SpansX.Count > 0) _tx.Text = _pick.SpansXText;
            if (_pick.SpansY.Count > 0) _ty.Text = _pick.SpansYText;
            _axisList.ItemsSource = _pick.AxesX.Concat(_pick.AxesY).ToList();
            _summary.Text = string.Join("\n", _pick.Summary().Select(t => "• " + t.Item + ": " + t.Detail));
            _notes.Text = string.Join("\n", _pick.Notes.Concat(p?.Notes ?? new List<string>()).Concat(_dwg.Warnings).Concat(_dwg.Info.Take(1)).Distinct().Take(8));
            Redraw();
            return;
        }
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
            _bmp = bi; _dwg = null; _pick = null; CadPath = null;
            _img.Source = bi;
            _cal.Clear(); _pxX.Clear(); _pxY.Clear(); _mPerPx = 0;
            _floor.ItemsSource = null; _floor.IsEnabled = false;
            _imgTools.Visibility = Visibility.Visible; _cadPanel.Visibility = Visibility.Collapsed; _mCal.IsChecked = true;
            _zoom.Value = Math.Clamp(900.0 / Math.Max(bi.PixelWidth, 1), 0.2, 4);
            _notes.Text = "Ảnh " + bi.PixelWidth + " × " + bi.PixelHeight + " điểm ảnh. Ảnh nên chụp thẳng, không méo phối cảnh.";
            Redraw(); UpdateHint();
        }
        catch (Exception ex) { MessageBox.Show(this, "Không mở được ảnh:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private (double X, double Y) ToDwg(Point p) => (_x0 + (p.X - _pad) / _sc, _y0 + (_H - _pad - p.Y) / _sc);

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        if (_dwg != null) { OnCadClick(e.GetPosition(_overlay)); return; }
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

    /// <summary>Bấm trên bản vẽ CAD (chế độ người dùng chọn): cột → trục qua cột; nét → trục theo nét.</summary>
    private void OnCadClick(Point pt)
    {
        var plan = _planUsed; if (plan == null) return;
        if (Source != AxisSource.User) { _hint.Text = "Chọn nguồn trục \"Người dùng chọn trục\" để bấm thêm trục trên hình (trục tự nhận được chép sẵn để sửa)."; return; }
        var (x, y) = ToDwg(pt);
        double tolM = 8 / _sc;
        var col = PlanAxisPicker.ColumnAt(plan.Columns, x, y, tolM);
        PickedAxis a = null;
        if (col != null) a = PlanAxisPicker.AxisThroughColumn(col, _pickY.IsChecked == true ? GridDir.Y : GridDir.X, Through, plan.Columns);
        else
        {
            PreviewLine? best = null; double bd = tolM;
            foreach (var l in plan.Preview)
            {
                if (!_show[Math.Clamp(l.Kind, 0, 7)]) continue;
                double d = SegDist(x, y, l.X1, l.Y1, l.X2, l.Y2);
                if (d < bd) { bd = d; best = l; }
            }
            if (best is PreviewLine bl) a = PlanAxisPicker.AxisFromSegment(bl.X1, bl.Y1, bl.X2, bl.Y2);
        }
        if (a == null) { _hint.Text = "Không bấm trúng nét hoặc cột – phóng to rồi bấm lại."; return; }
        if (!PlanAxisPicker.AddAxis(_userAxes, a, TolM)) { _hint.Text = "Trục này đã có."; return; }
        var same = _userAxes.Where(q => q.Dir == a.Dir).ToList();
        if (same.Any(q => string.IsNullOrWhiteSpace(q.Name)) || same.Select(q => q.Name).Distinct().Count() != same.Count) PlanAxisPicker.AutoName(same);
        Recompute();
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        if (_dwg == null || _pick == null) return;
        var (x, y) = ToDwg(e.GetPosition(_overlay));
        double tolM = 8 / _sc;
        var a = _pick.AxesX.Concat(_pick.AxesY).Where(q => Math.Abs((q.Dir == GridDir.X ? x : y) - q.Coord) <= tolM)
                     .OrderBy(q => Math.Abs((q.Dir == GridDir.X ? x : y) - q.Coord)).FirstOrDefault();
        if (a == null) return;
        _axisList.SelectedItem = a;
        RemoveAxis();
    }

    private static double SegDist(double px, double py, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1, l2 = dx * dx + dy * dy;
        double t = l2 < 1e-12 ? 0 : Math.Clamp(((px - x1) * dx + (py - y1) * dy) / l2, 0, 1);
        double qx = x1 + t * dx - px, qy = y1 + t * dy - py;
        return Math.Sqrt(qx * qx + qy * qy);
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
            for (int i = 0; i < _pxY.Count; i++) AddText(PlanAxisPicker.LetterName(i), 4, _pxY[i] * k + 2, Brushes.Blue);
            foreach (var c in _cal) AddDot(c.X * k, c.Y * k, Brushes.Orange);
            if (_cal.Count == 2) AddLine(_cal[0].X * k, _cal[0].Y * k, _cal[1].X * k, _cal[1].Y * k, Brushes.Orange, 2);
            return;
        }
        _img.Source = null;
        var f = CurrentPlan;
        if (f == null) { _host.Width = 600; _host.Height = 400; _overlay.Width = 600; _overlay.Height = 400; return; }
        var lines = f.Preview;
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var l in lines) { x0 = Math.Min(x0, Math.Min(l.X1, l.X2)); x1 = Math.Max(x1, Math.Max(l.X1, l.X2)); y0 = Math.Min(y0, Math.Min(l.Y1, l.Y2)); y1 = Math.Max(y1, Math.Max(l.Y1, l.Y2)); }
        foreach (var c in f.Columns) { x0 = Math.Min(x0, c.X); x1 = Math.Max(x1, c.X); y0 = Math.Min(y0, c.Y); y1 = Math.Max(y1, c.Y); }
        if (_pick != null)
        {
            foreach (var a in _pick.AxesX) { x0 = Math.Min(x0, a.Coord); x1 = Math.Max(x1, a.Coord); }
            foreach (var a in _pick.AxesY) { y0 = Math.Min(y0, a.Coord); y1 = Math.Max(y1, a.Coord); }
        }
        if (x1 <= x0 || y1 <= y0) return;
        _pad = 50; _sc = 850.0 / Math.Max(x1 - x0, y1 - y0) * k; _x0 = x0; _y0 = y0;
        double W = (x1 - x0) * _sc + 2 * _pad; _H = (y1 - y0) * _sc + 2 * _pad;
        _host.Width = W; _host.Height = _H; _overlay.Width = W; _overlay.Height = _H;
        double X(double x) => _pad + (x - _x0) * _sc;
        double Y(double y) => _H - _pad - (y - _y0) * _sc;
        foreach (var l in lines.Take(30000))
        {
            int kind = Math.Clamp(l.Kind, 0, 7);
            if (!_show[kind]) continue;
            AddLine(X(l.X1), Y(l.Y1), X(l.X2), Y(l.Y2), KindBrush[kind], kind == 1 ? 1.1 : 0.6);
        }
        // cột: xanh = đúng tim trục, cam = lệch tâm, xám = ngoài giao trục
        if (_show[3] && _pick != null)
            foreach (var c in _pick.Columns)
            {
                var fill = !c.OnGrid ? Brushes.LightGray : Math.Abs(c.OffX) + Math.Abs(c.OffY) > 1e-6 ? Brushes.Orange : Brushes.MediumSeaGreen;
                var r = new Shapes.Rectangle { Width = Math.Max(3, c.Bx * _sc), Height = Math.Max(3, c.By * _sc), Fill = fill, Stroke = Brushes.Black, StrokeThickness = 0.6, Opacity = 0.85, IsHitTestVisible = false };
                Canvas.SetLeft(r, X(c.X - c.Bx / 2)); Canvas.SetTop(r, Y(c.Y + c.By / 2)); _overlay.Children.Add(r);
            }
        if (_pick == null) return;
        double H = _H;
        foreach (var a in _pick.AxesX)
        {
            AddLine(X(a.Coord), 22, X(a.Coord), H - 6, Brushes.Red, 1.4, true);
            AddBubble(a.Name, X(a.Coord), 12);
        }
        foreach (var a in _pick.AxesY)
        {
            AddLine(22, Y(a.Coord), W - 6, Y(a.Coord), Brushes.Red, 1.4, true);
            AddBubble(a.Name, 12, Y(a.Coord));
        }
    }

    private void AddLine(double x1, double y1, double x2, double y2, Brush b, double t, bool dash = false)
    {
        var l = new Shapes.Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = b, StrokeThickness = t, IsHitTestVisible = false };
        if (dash) l.StrokeDashArray = new DoubleCollection { 10, 3, 2, 3 };
        _overlay.Children.Add(l);
    }

    private void AddBubble(string s, double cx, double cy)
    {
        var e = new Shapes.Ellipse { Width = 20, Height = 20, Stroke = Brushes.Red, StrokeThickness = 1.2, Fill = Brushes.White, IsHitTestVisible = false };
        Canvas.SetLeft(e, cx - 10); Canvas.SetTop(e, cy - 10); _overlay.Children.Add(e);
        var t = new TextBlock { Text = s ?? "", Foreground = Brushes.Red, FontWeight = FontWeights.Bold, FontSize = 11, IsHitTestVisible = false };
        t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(t, cx - t.DesiredSize.Width / 2); Canvas.SetTop(t, cy - t.DesiredSize.Height / 2); _overlay.Children.Add(t);
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
