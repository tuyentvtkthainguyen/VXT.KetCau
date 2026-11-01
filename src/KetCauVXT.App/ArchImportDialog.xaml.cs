using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Import.Arch;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Nhập bản vẽ kiến trúc sơ bộ (DXF/DWG) → nhận diện mặt bằng tầng → đề xuất kết cấu (xem trước, sửa) → tạo mô hình.
/// Cửa sổ chỉ lập phương án; việc ghi vào Project (ArchToStructure.Apply) do cửa sổ chính thực hiện khi DialogResult = true.</summary>
public partial class ArchImportDialog : Window
{
    /// <summary>Thông số dùng lần trước (nhớ trong phiên làm việc).</summary>
    private static ArchImportOptions _lastOptions;
    private static List<string> _lastFiles;

    private readonly List<string> _files = new();
    private bool _busy;

    public ArchImportOptions Options { get; private set; }
    public ArchDrawing Drawing { get; private set; }
    public StructuralProposal Proposal { get; private set; }

    // ---- xem trước ----
    // thế giới (m) → màn hình: sx = _ox + x·_scale ; sy = _oy − y·_scale (trục Y hướng lên)
    private double _scale = 40, _ox = 60, _oy = 400;
    private bool _fitPending = true;
    private bool _panning, _dragged;
    private MouseButton _panButton;
    private Point _panStart;
    private double _panOx, _panOy;

    private static readonly Brush ArchOther = Frozen(Color.FromRgb(205, 205, 205));
    private static readonly Brush ArchWallBr = Frozen(Color.FromRgb(140, 140, 140));
    private static readonly Brush ArchGridBr = Frozen(Color.FromRgb(220, 90, 90));
    private static readonly Brush ArchDoorBr = Frozen(Color.FromRgb(150, 100, 50));
    private static readonly Brush GridBr = Frozen(Color.FromRgb(200, 40, 40));
    private static readonly Brush MainBr = Frozen(Color.FromRgb(30, 80, 210));
    private static readonly Brush SecBr = Frozen(Color.FromRgb(0, 150, 150));
    private static readonly Brush CantBr = Frozen(Color.FromRgb(240, 130, 0));
    private static readonly Brush EdgeBr = Frozen(Color.FromRgb(140, 50, 170));
    private static readonly Brush OffBr = Frozen(Color.FromRgb(170, 170, 170));
    private static readonly Brush HoleBr = Frozen(Color.FromRgb(120, 60, 60));
    private static readonly Brush LabelBr = Frozen(Color.FromRgb(50, 50, 50));
    private static readonly Brush ColFill = Frozen(Color.FromRgb(20, 20, 20));

    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    private static readonly Dictionary<RoomKind, Color> SlabColors = new()
    {
        [RoomKind.Unknown] = Color.FromRgb(150, 200, 150),
        [RoomKind.Living] = Color.FromRgb(120, 200, 120),
        [RoomKind.Bedroom] = Color.FromRgb(120, 170, 230),
        [RoomKind.Kitchen] = Color.FromRgb(240, 200, 100),
        [RoomKind.WC] = Color.FromRgb(100, 210, 230),
        [RoomKind.Corridor] = Color.FromRgb(200, 200, 140),
        [RoomKind.Stair] = Color.FromRgb(180, 140, 120),
        [RoomKind.Balcony] = Color.FromRgb(240, 160, 120),
        [RoomKind.Storage] = Color.FromRgb(170, 150, 200),
        [RoomKind.Garage] = Color.FromRgb(160, 160, 160),
        [RoomKind.Worship] = Color.FromRgb(230, 140, 160),
        [RoomKind.Office] = Color.FromRgb(140, 200, 190),
        [RoomKind.Shop] = Color.FromRgb(240, 140, 90),
        [RoomKind.RoofTerrace] = Color.FromRgb(190, 170, 130),
        [RoomKind.Void] = Color.FromRgb(230, 230, 230),
    };

    public ArchImportDialog()
    {
        InitializeComponent();
        Options = _lastOptions ?? new ArchImportOptions();
        if (Options.Rooms == null || Options.Rooms.Count == 0) Options.Rooms = RoomCatalog.Default();
        OptionsToUi();
        if (_lastFiles != null)
            foreach (var f in _lastFiles) if (System.IO.File.Exists(f)) _files.Add(f);
        RefreshFiles();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        try { TryUiToOptions(out _); } catch (Exception) { /* nhớ thông số kể cả khi đóng – bỏ qua lỗi */ }
        _lastOptions = Options;
        _lastFiles = _files.ToList();
    }

    // ==================================================================
    // THÔNG SỐ
    // ==================================================================
    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private void OptionsToUi()
    {
        var o = Options;
        cbUnit.SelectedIndex = o.ScaleOverride <= 0 ? 0
            : Math.Abs(o.ScaleOverride - 0.001) < 1e-9 ? 1
            : Math.Abs(o.ScaleOverride - 0.01) < 1e-9 ? 2
            : Math.Abs(o.ScaleOverride - 1) < 1e-9 ? 3 : 0;
        txtMaxBeamSpan.Text = F(o.MaxBeamSpan);
        txtMaxSlabSpan.Text = F(o.MaxSlabSpan);
        txtMaxCantilever.Text = F(o.MaxCantilever);
        txtBeamWidth.Text = F(o.BeamWidth);
        txtMinColumnSize.Text = F(o.MinColumnSize);
        txtWallUnitWeight.Text = F(o.WallUnitWeight);
        txtBaseElevation.Text = F(o.BaseElevation);
        txtGroundHeight.Text = F(o.GroundStoreyHeight);
        txtTypicalHeight.Text = F(o.TypicalStoreyHeight);
        string conc = string.IsNullOrWhiteSpace(o.Concrete) ? "B25" : o.Concrete.Trim();
        var item = cbConcrete.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (i.Content as string) == conc);
        if (item == null) { item = new ComboBoxItem { Content = conc }; cbConcrete.Items.Add(item); }
        cbConcrete.SelectedItem = item;
        chkBeamsUnderWalls.IsChecked = o.BeamsUnderWalls;
        chkFixedBase.IsChecked = o.FixedBase;
        txtWallLayers.Text = o.WallLayers; txtGridLayers.Text = o.GridLayers; txtColumnLayers.Text = o.ColumnLayers;
        txtDoorLayers.Text = o.DoorLayers; txtStairLayers.Text = o.StairLayers;
        dgRooms.ItemsSource = o.Rooms.Select(r => new ArchRoomRow(r)).ToList();
    }

    private static bool TryNum(TextBox tb, out double v) =>
        double.TryParse(tb.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    /// <summary>Đọc giao diện vào Options; trả false (kèm lỗi) nếu có ô số không hợp lệ.</summary>
    private bool TryUiToOptions(out string error)
    {
        error = null;
        try { dgRooms.CommitEdit(DataGridEditingUnit.Row, true); } catch (InvalidOperationException) { }
        var o = Options;
        var bad = new List<string>();
        void Num(TextBox tb, string name, bool positive, Action<double> set)
        {
            if (TryNum(tb, out double v) && (!positive || v > 0)) set(v);
            else bad.Add(name);
        }
        Num(txtMaxBeamSpan, "Nhịp dầm lớn nhất", true, v => o.MaxBeamSpan = v);
        Num(txtMaxSlabSpan, "Cạnh ngắn ô sàn lớn nhất", true, v => o.MaxSlabSpan = v);
        Num(txtMaxCantilever, "Công xôn lớn nhất", false, v => o.MaxCantilever = Math.Max(0, v));
        Num(txtBeamWidth, "Bề rộng dầm", true, v => o.BeamWidth = v);
        Num(txtMinColumnSize, "Cạnh cột nhỏ nhất", true, v => o.MinColumnSize = v);
        Num(txtWallUnitWeight, "Trọng lượng riêng khối xây", true, v => o.WallUnitWeight = v);
        Num(txtBaseElevation, "Cao trình chân cột", false, v => o.BaseElevation = v);
        Num(txtGroundHeight, "Chiều cao tầng 1", true, v => o.GroundStoreyHeight = v);
        Num(txtTypicalHeight, "Chiều cao tầng điển hình", true, v => o.TypicalStoreyHeight = v);
        o.ScaleOverride = cbUnit.SelectedIndex switch { 1 => 0.001, 2 => 0.01, 3 => 1.0, _ => 0 };
        o.Concrete = (cbConcrete.SelectedItem as ComboBoxItem)?.Content as string ?? "B25";
        o.BeamsUnderWalls = chkBeamsUnderWalls.IsChecked == true;
        o.FixedBase = chkFixedBase.IsChecked == true;
        o.WallLayers = txtWallLayers.Text.Trim(); o.GridLayers = txtGridLayers.Text.Trim();
        o.ColumnLayers = txtColumnLayers.Text.Trim(); o.DoorLayers = txtDoorLayers.Text.Trim();
        o.StairLayers = txtStairLayers.Text.Trim();
        if (bad.Count > 0) { error = "Giá trị không hợp lệ: " + string.Join(", ", bad) + "."; return false; }
        return true;
    }

    // ==================================================================
    // FILE + NHẬN DIỆN
    // ==================================================================
    private void RefreshFiles()
    {
        lstFiles.ItemsSource = null;
        lstFiles.ItemsSource = _files.Select(f => System.IO.Path.GetFileName(f)).ToList();
        lstFiles.ToolTip = _files.Count > 0 ? string.Join("\n", _files) : null;
        btnRead.IsEnabled = _files.Count > 0 && !_busy;
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Bản vẽ CAD (*.dxf;*.dwg)|*.dxf;*.dwg",
            Title = "Chọn bản vẽ kiến trúc (có thể chọn nhiều file)",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != true) return;
        foreach (var f in dlg.FileNames)
            if (!_files.Contains(f, StringComparer.OrdinalIgnoreCase)) _files.Add(f);
        RefreshFiles();
        SetStatus($"Đã chọn {_files.Count} file. Bấm “Nhận diện” để đọc các mặt bằng tầng.");
    }

    private void BtnClearFiles_Click(object sender, RoutedEventArgs e)
    {
        _files.Clear();
        RefreshFiles();
    }

    private async void BtnRead_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _files.Count == 0) return;
        if (!TryUiToOptions(out string err)) { Warn(err); return; }
        var paths = _files.ToList();
        var o = Options;
        ArchDrawing d = null;
        bool ok = await RunBusy("Đang đọc bản vẽ và nhận diện mặt bằng tầng...", () => d = ArchPlanReader.ReadMany(paths, o), "Lỗi đọc bản vẽ kiến trúc");
        if (!ok || d == null) return;
        Drawing = d;
        Proposal = null;
        dgFloors.ItemsSource = null;
        dgFloors.ItemsSource = d.Floors;
        btnPropose.IsEnabled = d.Floors.Count > 0;
        UpdateAfterProposal();
        SetStatus($"Nhận diện được {d.Floors.Count} mặt bằng tầng" +
                  (d.Warnings.Count > 0 ? $", {d.Warnings.Count} cảnh báo." : "."));
        if (d.Floors.Count == 0) { Warn("Không nhận diện được mặt bằng tầng nào. Kiểm tra từ khoá tên lớp và đơn vị bản vẽ."); return; }
        await DoPropose();
    }

    // ==================================================================
    // ĐỀ XUẤT
    // ==================================================================
    private async void BtnPropose_Click(object sender, RoutedEventArgs e) => await DoPropose();

    private async Task DoPropose()
    {
        if (_busy || Drawing == null) return;
        try { dgFloors.CommitEdit(DataGridEditingUnit.Row, true); } catch (InvalidOperationException) { }
        if (!TryUiToOptions(out string err)) { Warn(err); return; }
        if (!Drawing.Floors.Any(f => f.Include)) { Warn("Chọn ít nhất một mặt bằng tầng để triển khai."); return; }
        var d = Drawing; var o = Options;
        StructuralProposal s = null;
        bool ok = await RunBusy("Đang lập phương án kết cấu...", () => s = ArchToStructure.Propose(d, o), "Lỗi đề xuất kết cấu");
        if (!ok) return;
        Proposal = s;
        _fitPending = true;
        UpdateAfterProposal();
        if (s != null)
        {
            int beams = s.Levels.Sum(l => l.Beams.Count), slabs = s.Levels.Sum(l => l.Slabs.Count);
            SetStatus($"Phương án: {s.Levels.Count} cao trình, {s.Columns.Count} cột, {beams} dầm, {slabs} ô sàn" +
                      (s.Warnings.Count > 0 ? $" – {s.Warnings.Count} cảnh báo (xem tab Ghi chú)." : "."));
        }
    }

    private void UpdateAfterProposal()
    {
        btnApply.IsEnabled = Proposal != null && !_busy;
        // danh sách cao trình
        var items = new List<string>();
        if (Proposal != null)
            foreach (var l in Proposal.Levels)
                items.Add(string.IsNullOrEmpty(l.FromPlan) ? $"{l.Name}  (+{l.Elevation:0.00})" : $"{l.Name}  (+{l.Elevation:0.00})  –  {l.FromPlan}");
        int old = cbLevel.SelectedIndex;
        cbLevel.ItemsSource = items;
        cbLevel.SelectedIndex = items.Count == 0 ? -1 : (old >= 0 && old < items.Count ? old : 0);
        dgColumns.ItemsSource = Proposal?.Columns.Select(c => new ArchColumnRow(c)).ToList();
        txtNotes.Text = BuildNotes();
        RedrawPlan();
    }

    private string BuildNotes()
    {
        var sb = new StringBuilder();
        void Sec(string title, IEnumerable<string> lines)
        {
            var l = lines?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (l == null || l.Count == 0) return;
            sb.AppendLine("== " + title + " ==");
            foreach (var s in l) sb.AppendLine("- " + s);
            sb.AppendLine();
        }
        if (Drawing != null)
        {
            if (!string.IsNullOrEmpty(Drawing.Source)) sb.AppendLine("Nguồn: " + Drawing.Source).AppendLine();
            Sec("Thông tin nhận diện", Drawing.Info);
            Sec("Cảnh báo nhận diện", Drawing.Warnings);
        }
        if (Proposal != null)
        {
            Sec("Ghi chú phương án kết cấu", Proposal.Notes);
            Sec("Cảnh báo phương án kết cấu", Proposal.Warnings);
        }
        if (Drawing != null)
            foreach (var f in Drawing.Floors)
                Sec("Mặt bằng: " + f.Title, f.Notes);
        return sb.Length == 0 ? "(Chưa có ghi chú.)" : sb.ToString();
    }

    // ==================================================================
    // TẠO MÔ HÌNH
    // ==================================================================
    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || Proposal == null || Drawing == null) return;
        if (!TryUiToOptions(out string err)) { Warn(err); return; }
        if (!Proposal.Columns.Any(c => c.Include))
        { Warn("Phương án không còn cột nào được chọn."); return; }
        var r = MessageBox.Show(this, "Mô hình hiện tại sẽ bị thay thế. Tiếp tục?", Title,
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        _lastOptions = Options;
        DialogResult = true;
    }

    // ==================================================================
    // TIỆN ÍCH
    // ==================================================================
    private void SetStatus(string s) => txtStatus.Text = s;

    private void Warn(string s) => MessageBox.Show(this, s, Title, MessageBoxButton.OK, MessageBoxImage.Warning);

    /// <summary>Chạy việc nặng ở luồng nền, khoá giao diện, đổi con trỏ; bắt lỗi và báo bằng tiếng Việt.</summary>
    private async Task<bool> RunBusy(string status, Action work, string errTitle)
    {
        _busy = true;
        leftPanel.IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait;
        SetStatus(status);
        try
        {
            await Task.Run(work);
            return true;
        }
        catch (Exception ex)
        {
            var inner = ex;
            while (inner is AggregateException ag && ag.InnerException != null) inner = ag.InnerException;
            while (inner is System.Reflection.TargetInvocationException ti && ti.InnerException != null) inner = ti.InnerException;
            string msg = inner is NotImplementedException
                ? "Chức năng này chưa được cài đặt trong phiên bản hiện tại (đang phát triển)."
                : inner.Message;
            SetStatus(errTitle + ": " + msg);
            Mouse.OverrideCursor = null;
            MessageBox.Show(this, errTitle + ":\n\n" + msg, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            Mouse.OverrideCursor = null;
            leftPanel.IsEnabled = true;
            _busy = false;
            btnRead.IsEnabled = _files.Count > 0;
            btnPropose.IsEnabled = Drawing != null && Drawing.Floors.Count > 0;
            btnApply.IsEnabled = Proposal != null;
        }
    }

    private void TabRight_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, tabRight)) return; // bỏ qua sự kiện nổi lên từ DataGrid/ComboBox con
        if (tabRight.SelectedIndex == 0) RedrawPlan(); // cột có thể đã bỏ/chọn ở tab "Cột"
    }

    // ==================================================================
    // XEM TRƯỚC MẶT BẰNG
    // ==================================================================
    private ProposalLevel CurrentLevel =>
        Proposal != null && cbLevel.SelectedIndex >= 0 && cbLevel.SelectedIndex < Proposal.Levels.Count
            ? Proposal.Levels[cbLevel.SelectedIndex] : null;

    private ArchFloorPlan PlanOf(ProposalLevel l) =>
        l == null || Drawing == null ? null : Drawing.Floors.FirstOrDefault(f => f.Title == l.FromPlan);

    private int LevelIndex(string name)
    {
        if (Proposal == null) return -1;
        int i = Proposal.Levels.FindIndex(l => l.Name == name);
        return i < 0 ? Proposal.Levels.Count - 1 : i; // tên lạ → coi như lên đến đỉnh
    }

    private IEnumerable<PropColumn> ColumnsAt(int levelIndex) =>
        Proposal == null ? Enumerable.Empty<PropColumn>() : Proposal.Columns.Where(c => LevelIndex(c.TopLevel) >= levelIndex);

    private Point W2S(double x, double y) => new(_ox + x * _scale, _oy - y * _scale);
    private (double x, double y) S2W(Point s) => ((s.X - _ox) / _scale, (_oy - s.Y) / _scale);

    private void CbLevel_SelectionChanged(object sender, SelectionChangedEventArgs e) => RedrawPlan();
    private void ChkView_Changed(object sender, RoutedEventArgs e) { if (IsLoaded) RedrawPlan(); }
    private void BtnFit_Click(object sender, RoutedEventArgs e) { _fitPending = true; RedrawPlan(); }
    private void PlanHost_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawPlan();

    private bool FitPlan()
    {
        double w = planHost.ActualWidth, h = planHost.ActualHeight;
        if (w < 10 || h < 10) return false;
        var lv = CurrentLevel;
        var xs = new List<double>(); var ys = new List<double>();
        if (Proposal != null)
        {
            xs.AddRange(Proposal.GridX); ys.AddRange(Proposal.GridY);
            foreach (var c in Proposal.Columns) { xs.Add(c.X); ys.Add(c.Y); }
        }
        if (lv != null)
        {
            foreach (var b in lv.Beams) { xs.Add(b.Ax); xs.Add(b.Bx); ys.Add(b.Ay); ys.Add(b.By); }
            foreach (var s in lv.Slabs) { xs.Add(s.X0); xs.Add(s.X1); ys.Add(s.Y0); ys.Add(s.Y1); }
            var pl = PlanOf(lv);
            if (pl != null && chkArch.IsChecked == true)
                foreach (var p in pl.Preview) { xs.Add(p.X1); xs.Add(p.X2); ys.Add(p.Y1); ys.Add(p.Y2); }
        }
        if (xs.Count == 0) xs.AddRange(new[] { 0.0, 10.0 });
        if (ys.Count == 0) ys.AddRange(new[] { 0.0, 10.0 });
        double x0 = xs.Min(), x1 = xs.Max(), y0 = ys.Min(), y1 = ys.Max();
        double dx = Math.Max(x1 - x0, 1), dy = Math.Max(y1 - y0, 1);
        const double margin = 70;
        _scale = Math.Max(1, Math.Min((w - 2 * margin) / dx, (h - 2 * margin) / dy));
        _ox = (w - dx * _scale) / 2 - x0 * _scale;
        _oy = h - (h - dy * _scale) / 2 + y0 * _scale;
        _fitPending = false;
        return true;
    }

    private void RedrawPlan()
    {
        if (planCanvas == null) return;
        planCanvas.Children.Clear();
        var lv = CurrentLevel;
        if (Proposal == null || lv == null)
        {
            AddText(Drawing == null ? "Chưa có bản vẽ – chọn file và bấm “Nhận diện”." : "Chưa có phương án kết cấu – bấm “Đề xuất kết cấu”.",
                new Point(10, 8), Brushes.Gray, 12, false);
            return;
        }
        if (_fitPending && !FitPlan()) return;
        bool labels = chkLabels.IsChecked == true;
        int li = cbLevel.SelectedIndex;

        // ---- nền kiến trúc ----
        var plan = PlanOf(lv);
        if (plan != null && chkArch.IsChecked == true && plan.Preview.Count > 0)
        {
            AddPreview(plan.Preview, k => k != 1 && k != 2 && k != 4, ArchOther, 0.7, null);
            AddPreview(plan.Preview, k => k == 4, ArchDoorBr, 0.6, null);
            AddPreview(plan.Preview, k => k == 2, ArchGridBr, 0.5, new DoubleCollection { 12, 3, 2, 3 });
            AddPreview(plan.Preview, k => k == 1, ArchWallBr, 1.0, null);
        }

        // ---- ô sàn ----
        foreach (var s in lv.Slabs)
        {
            var a = W2S(Math.Min(s.X0, s.X1), Math.Max(s.Y0, s.Y1));
            var b = W2S(Math.Max(s.X0, s.X1), Math.Min(s.Y0, s.Y1));
            var c = SlabColors.TryGetValue(s.Kind, out var cc) ? cc : SlabColors[RoomKind.Unknown];
            var r = new Shapes.Rectangle
            {
                Width = Math.Max(1, b.X - a.X), Height = Math.Max(1, b.Y - a.Y),
                Fill = new SolidColorBrush(Color.FromArgb(s.Include ? (byte)80 : (byte)20, c.R, c.G, c.B)),
                Stroke = s.Include ? Brushes.Transparent : OffBr, StrokeThickness = 0.8,
                ToolTip = $"Ô sàn {(string.IsNullOrEmpty(s.RoomName) ? s.Kind.ToString() : s.RoomName)}\n" +
                          $"{Math.Abs(s.X1 - s.X0):0.00} × {Math.Abs(s.Y1 - s.Y0):0.00} m, t = {Mm(s.Thickness)} mm\n" +
                          $"Hoàn thiện {s.Finish:0.##} kN/m², hoạt tải {s.Live:0.##} kN/m²" +
                          (s.Include ? "" : "\n(ĐÃ BỎ)") + "\nBấm để bỏ/chọn lại."
            };
            if (!s.Include) r.StrokeDashArray = new DoubleCollection { 4, 3 };
            Canvas.SetLeft(r, a.X); Canvas.SetTop(r, a.Y);
            planCanvas.Children.Add(r);
            if (labels && b.X - a.X > 50 && b.Y - a.Y > 26)
            {
                var m = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                if (!string.IsNullOrEmpty(s.RoomName)) AddText(s.RoomName, new Point(m.X, m.Y - 8), Brushes.DarkGreen, 9, true);
                AddText($"S t={Mm(s.Thickness)} / q={s.Live:0.##}", new Point(m.X, m.Y + 5), Brushes.DarkGreen, 9, true);
            }
        }

        // ---- lỗ sàn ----
        foreach (var z in lv.Holes)
        {
            var a = W2S(Math.Min(z.X0, z.X1), Math.Max(z.Y0, z.Y1));
            var b = W2S(Math.Max(z.X0, z.X1), Math.Min(z.Y0, z.Y1));
            var r = new Shapes.Rectangle
            {
                Width = Math.Max(1, b.X - a.X), Height = Math.Max(1, b.Y - a.Y),
                Fill = new SolidColorBrush(Color.FromArgb(25, 120, 60, 60)), Stroke = HoleBr, StrokeThickness = 1,
                ToolTip = HoleName(z) + (string.IsNullOrEmpty(z.Note) ? "" : "\n" + z.Note)
            };
            Canvas.SetLeft(r, a.X); Canvas.SetTop(r, a.Y);
            planCanvas.Children.Add(r);
            AddLine(a, b, HoleBr, 0.7, null);
            AddLine(new Point(a.X, b.Y), new Point(b.X, a.Y), HoleBr, 0.7, null);
            if (labels) AddText(HoleName(z), new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2), HoleBr, 10, true, true, true);
        }

        // ---- trục ----
        DrawGrids();

        // ---- dầm ----
        foreach (var bm in lv.Beams)
        {
            var a = W2S(bm.Ax, bm.Ay); var b = W2S(bm.Bx, bm.By);
            Brush br = !bm.Include ? OffBr : bm.Role switch
            {
                BeamRole.Main => MainBr, BeamRole.Secondary => SecBr, BeamRole.Cantilever => CantBr, _ => EdgeBr
            };
            double th = !bm.Include ? 1.2 : bm.Role == BeamRole.Main ? 3.0 : bm.Role == BeamRole.Secondary ? 2.0 : 2.5;
            AddLine(a, b, br, th, bm.Include ? null : new DoubleCollection { 4, 3 });
            // nét trong suốt rộng để dễ rê chuột xem thông tin
            var hit = new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = Brushes.Transparent, StrokeThickness = 9, ToolTip = BeamTip(bm) };
            planCanvas.Children.Add(hit);
            if (labels && !string.IsNullOrEmpty(bm.Section))
            {
                var m = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                bool vertical = Math.Abs(b.X - a.X) < Math.Abs(b.Y - a.Y);
                AddText(bm.Section, vertical ? new Point(m.X + 5, m.Y - 6) : new Point(m.X, m.Y - 13),
                    bm.Include ? br : OffBr, 9, !vertical);
            }
        }

        // ---- cột ----
        foreach (var c in ColumnsAt(li))
        {
            var p = W2S(c.X, c.Y);
            double rw = Math.Max(6, c.Bx * _scale), rh = Math.Max(6, c.By * _scale);
            string sec = c.SectionByLevel != null && c.SectionByLevel.TryGetValue(lv.Name, out var sn) ? sn : "";
            var r = new Shapes.Rectangle
            {
                Width = rw, Height = rh,
                Fill = c.Include ? ColFill : Brushes.White,
                Stroke = c.Include ? Brushes.Black : OffBr, StrokeThickness = c.Include ? 0.8 : 1.2,
                ToolTip = ColumnTip(c, sec)
            };
            if (!c.Include) r.StrokeDashArray = new DoubleCollection { 2, 2 };
            Canvas.SetLeft(r, p.X - rw / 2); Canvas.SetTop(r, p.Y - rh / 2);
            planCanvas.Children.Add(r);
            if (labels && c.Include && !string.IsNullOrEmpty(sec))
                AddText(sec, new Point(p.X + rw / 2 + 2, p.Y + rh / 2 + 1), Brushes.Black, 9, false);
        }

        AddText($"Cao trình {lv.Name}  +{lv.Elevation:0.00} m" + (lv.Roof ? " (mái)" : "") +
                (string.IsNullOrEmpty(lv.FromPlan) ? "" : $"   –   nền: {lv.FromPlan}") +
                $"   –   tỷ lệ {_scale:0.#} px/m", new Point(8, 6), Brushes.Gray, 11, false, false, true);
    }

    private static string Mm(double t) => (t < 2 ? t * 1000 : t).ToString("0", CultureInfo.InvariantCulture);

    private static string HoleName(ArchZone z) => z.Kind switch
    {
        RoomKind.Stair => "Cầu thang",
        RoomKind.Void => "Thông tầng",
        _ => string.IsNullOrEmpty(z.Note) ? "Lỗ sàn" : z.Note
    };

    private static string RoleName(BeamRole r) => r switch
    {
        BeamRole.Main => "Dầm chính",
        BeamRole.Secondary => "Dầm phụ",
        BeamRole.Cantilever => "Dầm công xôn",
        _ => "Dầm biên"
    };

    private static string BeamTip(PropBeam b) =>
        $"{RoleName(b.Role)} {b.Section}" + (b.B > 0 && b.H > 0 ? $" ({b.B * 1000:0}×{b.H * 1000:0})" : "") +
        $"\nL = {b.Length:0.00} m\nTải tường: {b.WallLoad:0.0} kN/m" +
        (string.IsNullOrEmpty(b.Note) ? "" : "\n" + b.Note) +
        (b.Include ? "" : "\n(ĐÃ BỎ)") + "\nBấm để bỏ/chọn lại.";

    private static string ColumnTip(PropColumn c, string sec) =>
        $"Cột {(string.IsNullOrEmpty(sec) ? $"{c.Bx * 1000:0}×{c.By * 1000:0}" : sec)}" +
        $"\nTâm ({c.X:0.000}; {c.Y:0.000}) m, lên đến {c.TopLevel}" +
        $"\nDiện chịu tải: {c.TributaryArea:0.0} m²" +
        (c.SectionByLevel != null && c.SectionByLevel.Count > 0 ? "\nTiết diện: " + ArchColumnRow.JoinSections(c) : "") +
        (string.IsNullOrEmpty(c.Note) ? "" : "\n" + c.Note) +
        (c.Include ? "" : "\n(ĐÃ BỎ)") + "\nBấm để bỏ/chọn lại.";

    private void DrawGrids()
    {
        var gx = Proposal.GridX; var gy = Proposal.GridY;
        if (gx.Count == 0 && gy.Count == 0) return;
        double bub = 11, ext = 1.2;
        double yMin = gy.Count > 0 ? gy.Min() : 0, yMax = gy.Count > 0 ? gy.Max() : 10;
        double xMin = gx.Count > 0 ? gx.Min() : 0, xMax = gx.Count > 0 ? gx.Max() : 10;
        var dash = new DoubleCollection { 12, 3, 2, 3 };
        for (int i = 0; i < gx.Count; i++)
        {
            var a = W2S(gx[i], yMin - ext); var b = W2S(gx[i], yMax + ext);
            AddLine(a, b, GridBr, 0.8, dash);
            string n = i < Proposal.GridXNames.Count ? Proposal.GridXNames[i] : (i + 1).ToString();
            AddBubble(n, new Point(a.X, a.Y + bub), bub);
            AddBubble(n, new Point(b.X, b.Y - bub), bub);
        }
        for (int i = 0; i < gy.Count; i++)
        {
            var a = W2S(xMin - ext, gy[i]); var b = W2S(xMax + ext, gy[i]);
            AddLine(a, b, GridBr, 0.8, dash);
            string n = i < Proposal.GridYNames.Count ? Proposal.GridYNames[i] : ((char)('A' + Math.Min(i, 25))).ToString();
            AddBubble(n, new Point(a.X - bub, a.Y), bub);
            AddBubble(n, new Point(b.X + bub, b.Y), bub);
        }
        // kích thước bước trục
        var sx = gx.OrderBy(v => v).ToList(); var sy = gy.OrderBy(v => v).ToList();
        for (int i = 0; i + 1 < sx.Count; i++)
        {
            var a = W2S((sx[i] + sx[i + 1]) / 2, yMin - ext);
            AddText($"{(sx[i + 1] - sx[i]) * 1000:0}", new Point(a.X, a.Y + 2 * bub + 10), GridBr, 10, true);
        }
        for (int i = 0; i + 1 < sy.Count; i++)
        {
            var a = W2S(xMin - ext, (sy[i] + sy[i + 1]) / 2);
            AddText($"{(sy[i + 1] - sy[i]) * 1000:0}", new Point(a.X - 2 * bub - 22, a.Y), GridBr, 10, true);
        }
    }

    /// <summary>Gộp các nét nền kiến trúc cùng loại vào một Path (nhanh hơn hàng nghìn Line).</summary>
    private void AddPreview(List<PreviewLine> lines, Func<int, bool> kind, Brush br, double th, DoubleCollection dash)
    {
        var g = new StreamGeometry();
        int n = 0;
        using (var ctx = g.Open())
        {
            foreach (var l in lines)
            {
                if (!kind(l.Kind)) continue;
                ctx.BeginFigure(W2S(l.X1, l.Y1), false, false);
                ctx.LineTo(W2S(l.X2, l.Y2), true, false);
                n++;
            }
        }
        if (n == 0) return;
        g.Freeze();
        var path = new Shapes.Path { Data = g, Stroke = br, StrokeThickness = th, IsHitTestVisible = false };
        if (dash != null) path.StrokeDashArray = dash;
        planCanvas.Children.Add(path);
    }

    private void AddLine(Point a, Point b, Brush br, double th, DoubleCollection dash)
    {
        var l = new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = br, StrokeThickness = th, IsHitTestVisible = false };
        if (dash != null) l.StrokeDashArray = dash;
        planCanvas.Children.Add(l);
    }

    private void AddBubble(string name, Point c, double r)
    {
        var e = new Shapes.Ellipse { Width = 2 * r, Height = 2 * r, Stroke = GridBr, StrokeThickness = 1, Fill = Brushes.White, IsHitTestVisible = false };
        Canvas.SetLeft(e, c.X - r); Canvas.SetTop(e, c.Y - r);
        planCanvas.Children.Add(e);
        AddText(name, c, GridBr, 11, true, true);
    }

    private void AddText(string s, Point p, Brush br, double size, bool center, bool vcenter = false, bool background = false)
    {
        var t = new TextBlock { Text = s, Foreground = br, FontSize = size, IsHitTestVisible = false };
        if (background) t.Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
        t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double x = center ? p.X - t.DesiredSize.Width / 2 : p.X;
        double y = vcenter || center ? p.Y - t.DesiredSize.Height / 2 : p.Y;
        Canvas.SetLeft(t, x); Canvas.SetTop(t, y);
        planCanvas.Children.Add(t);
    }

    // ------------------------------------------------------------------
    // Chuột: con lăn thu phóng quanh con trỏ; kéo (phải/giữa/trái) di chuyển; bấm trái chọn/bỏ cấu kiện
    // ------------------------------------------------------------------
    private void PlanHost_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Proposal == null) return;
        var m = e.GetPosition(planHost);
        var (wx, wy) = S2W(m);
        double k = e.Delta > 0 ? 1.2 : 1 / 1.2;
        _scale = Math.Clamp(_scale * k, 0.5, 5000);
        _ox = m.X - wx * _scale;
        _oy = m.Y + wy * _scale;
        _fitPending = false;
        RedrawPlan();
        e.Handled = true;
    }

    private void PlanHost_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_panning) return;
        if (e.ChangedButton != MouseButton.Left && e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Right) return;
        _panning = true; _dragged = false; _panButton = e.ChangedButton;
        _panStart = e.GetPosition(planHost);
        _panOx = _ox; _panOy = _oy;
        planHost.CaptureMouse();
        e.Handled = true;
    }

    private void PlanHost_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_panning) return;
        var p = e.GetPosition(planHost);
        double dx = p.X - _panStart.X, dy = p.Y - _panStart.Y;
        if (!_dragged && Math.Abs(dx) + Math.Abs(dy) < 4) return;
        _dragged = true;
        _fitPending = false;
        _ox = _panOx + dx; _oy = _panOy + dy;
        RedrawPlan();
    }

    private void PlanHost_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_panning || e.ChangedButton != _panButton) return;
        _panning = false;
        planHost.ReleaseMouseCapture();
        e.Handled = true;
        if (!_dragged && _panButton == MouseButton.Left) ToggleAt(e.GetPosition(planHost));
    }

    /// <summary>Bỏ/chọn lại cột, dầm hoặc ô sàn tại điểm bấm (ưu tiên cột → dầm → ô sàn).</summary>
    private void ToggleAt(Point s)
    {
        var lv = CurrentLevel;
        if (lv == null) return;
        int li = cbLevel.SelectedIndex;
        foreach (var c in ColumnsAt(li))
        {
            var p = W2S(c.X, c.Y);
            double hw = Math.Max(6, c.Bx * _scale) / 2 + 3, hh = Math.Max(6, c.By * _scale) / 2 + 3;
            if (Math.Abs(s.X - p.X) <= hw && Math.Abs(s.Y - p.Y) <= hh)
            {
                c.Include = !c.Include;
                SetStatus($"Cột tại ({c.X:0.00}; {c.Y:0.00}): {(c.Include ? "dùng" : "bỏ")} (áp dụng cho mọi tầng).");
                dgColumns.ItemsSource = Proposal.Columns.Select(q => new ArchColumnRow(q)).ToList();
                RedrawPlan();
                return;
            }
        }
        PropBeam best = null; double bd = 7;
        foreach (var b in lv.Beams)
        {
            double d = DistToSeg(s, W2S(b.Ax, b.Ay), W2S(b.Bx, b.By));
            if (d < bd) { bd = d; best = b; }
        }
        if (best != null)
        {
            best.Include = !best.Include;
            SetStatus($"{RoleName(best.Role)} {best.Section} L={best.Length:0.00} m: {(best.Include ? "dùng" : "bỏ")}.");
            RedrawPlan();
            return;
        }
        var (wx, wy) = S2W(s);
        var slab = lv.Slabs.FirstOrDefault(q => wx >= Math.Min(q.X0, q.X1) && wx <= Math.Max(q.X0, q.X1) && wy >= Math.Min(q.Y0, q.Y1) && wy <= Math.Max(q.Y0, q.Y1));
        if (slab != null)
        {
            slab.Include = !slab.Include;
            SetStatus($"Ô sàn {slab.RoomName}: {(slab.Include ? "dùng" : "bỏ")}.");
            RedrawPlan();
        }
    }

    private static double DistToSeg(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double l2 = dx * dx + dy * dy;
        double t = l2 < 1e-9 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        double qx = a.X + t * dx - p.X, qy = a.Y + t * dy - p.Y;
        return Math.Sqrt(qx * qx + qy * qy);
    }
}

/// <summary>Dòng bảng tải theo công năng phòng (RoomType.Keys hiển thị dạng chuỗi cách dấu phẩy).</summary>
public class ArchRoomRow
{
    private readonly RoomType _r;
    public ArchRoomRow(RoomType r) { _r = r; }
    public string Name { get => _r.Name; set => _r.Name = value ?? ""; }
    public double Finish { get => _r.Finish; set => _r.Finish = Math.Max(0, value); }
    public double Live { get => _r.Live; set => _r.Live = Math.Max(0, value); }
    public bool NoSlab { get => _r.NoSlab; set => _r.NoSlab = value; }
    public string KeysText
    {
        get => string.Join(", ", _r.Keys ?? Array.Empty<string>());
        set => _r.Keys = (value ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    }
}

/// <summary>Dòng bảng cột đề xuất (PropColumn dùng field nên cần lớp bọc để DataGrid gắn kết).</summary>
public class ArchColumnRow
{
    private readonly PropColumn _c;
    public ArchColumnRow(PropColumn c) { _c = c; }
    public bool Include { get => _c.Include; set => _c.Include = value; }
    public double X => _c.X;
    public double Y => _c.Y;
    public double Bx => _c.Bx;
    public double By => _c.By;
    public string TopLevel => _c.TopLevel;
    public double TributaryArea => _c.TributaryArea;
    public string Note => _c.Note;
    public string Sections => JoinSections(_c);

    public static string JoinSections(PropColumn c) =>
        c.SectionByLevel == null ? "" : string.Join(", ", c.SectionByLevel.Select(kv => $"{kv.Key}: {kv.Value}"));
}
