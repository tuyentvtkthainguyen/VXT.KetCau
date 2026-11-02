using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Dòng bảng "Layer trong file": layer, màu, số thực thể theo loại, trạng thái, nhóm tự nhận và nhóm gán (sửa được).</summary>
internal sealed class ArchLayerRow : RowBase
{
    public const string Auto = "(theo quy tắc)";
    public static readonly List<string> Choices = new[] { Auto }.Concat(CadCategory.All.Select(CadCategory.Label)).Append("(không nhận)").ToList();

    private readonly Action<ArchLayerRow> _changed;
    public CadLayerInfo Info { get; }
    /// <summary>Nhóm gán trực tiếp ("" = theo quy tắc; "-" = không nhận)</summary>
    public string Assigned { get; private set; }

    public ArchLayerRow(CadLayerInfo info, string assigned, Action<ArchLayerRow> changed) { Info = info; Assigned = assigned ?? ""; _changed = changed; }

    public string Name => Info.Name;
    public int Color => Info.Color;
    public SolidColorBrush Swatch => Aci.Brush(Info.Color);
    public string Counts => Info.CountsText;
    public int Total => Info.Total;
    public string State => Info.Frozen ? "Đóng băng" : Info.Off ? "Tắt" : "";
    public string AutoLabel => Info.Category.Length == 0 ? "–" : CadCategory.Label(Info.Category);
    public string Reason => Info.Reason;
    public string Effective => Assigned == "-" ? "" : Assigned.Length > 0 ? Assigned : Info.Category;
    public string EffectiveLabel => Effective.Length == 0 ? "(không nhận)" : CadCategory.Label(Effective);

    public string Choice
    {
        get => Assigned.Length == 0 ? Auto : Assigned == "-" ? "(không nhận)" : CadCategory.Label(Assigned);
        set
        {
            Assigned = value == Auto || string.IsNullOrEmpty(value) ? "" : value == "(không nhận)" ? "-" : CadCategory.Normalize(value);
            RaiseAll();
            _changed?.Invoke(this);
        }
    }

    public void Refresh() => RaiseAll();
}

/// <summary>Tab "Layer trong file" của hộp nhập bản vẽ kiến trúc: liệt kê layer các file đã chọn, gán nhóm cấu kiện,
/// lưu làm mặc định (chuẩn layer CAD), xem trước làm nổi các layer của nhóm đang chọn.</summary>
public partial class ArchImportDialog
{
    private CadStandard _std;
    private readonly ObservableCollection<ArchLayerRow> _layerRows = new();
    /// <summary>Gán trực tiếp đang sửa trong hộp thoại: layer → nhóm ("-" = không nhận)</summary>
    private readonly Dictionary<string, string> _layerAssign = new(StringComparer.OrdinalIgnoreCase);
    private CadLayerScanResult _scan;
    private bool _scanStale = true;
    private DataGrid _gLayers;
    private Canvas _layerCanvas;
    private Border _layerHost;
    private TextBlock _layerInfo;
    private CheckBox _chkOnlyUsed;
    private TabItem _layerTab;

    /// <summary>Bảng gán layer đã dùng trong dự án (Project.ArchLayerMap) – nạp trước khi mở hộp thoại.</summary>
    public List<LayerMapEntry> InitialLayerMap
    {
        set
        {
            foreach (var m in value ?? new List<LayerMapEntry>())
                if (m != null && !string.IsNullOrEmpty(m.Layer) && CadCategory.Normalize(m.Category).Length > 0) _layerAssign[m.Layer] = CadCategory.Normalize(m.Category);
        }
    }

    /// <summary>Layer → nhóm đã dùng ở lần nhận diện gần nhất (để lưu vào Project.ArchLayerMap).</summary>
    public List<LayerMapEntry> UsedLayerMap =>
        (Drawing?.LayerCategories ?? new Dictionary<string, string>()).Where(kv => kv.Value.Length > 0)
        .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => new LayerMapEntry(kv.Key, kv.Value)).ToList();

    private void InitLayerTab()
    {
        try { _std = CadStandard.Active?.Clone() ?? CadStandard.LoadUserDefault(); } catch (Exception) { _std = CadStandard.Default(); }
        Options.SkipOffLayers = _std.SkipOffLayers;

        _gLayers = DialogUi.NewGrid();
        _gLayers.SelectionMode = DataGridSelectionMode.Single;
        var sw = new DataGridTemplateColumn { Header = "Màu", Width = 40 };
        var bf = new FrameworkElementFactory(typeof(Border));
        bf.SetValue(FrameworkElement.WidthProperty, 18.0); bf.SetValue(FrameworkElement.HeightProperty, 12.0);
        bf.SetValue(Border.BorderBrushProperty, Brushes.Gray); bf.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        bf.SetBinding(Border.BackgroundProperty, new Binding(nameof(ArchLayerRow.Swatch)));
        sw.CellTemplate = new DataTemplate { VisualTree = bf };
        _gLayers.Columns.Add(sw);
        _gLayers.Columns.Add(DialogUi.Text("Layer", nameof(ArchLayerRow.Name), true, 170));
        _gLayers.Columns.Add(DialogUi.Text("Thực thể theo loại", nameof(ArchLayerRow.Counts), true, 210));
        _gLayers.Columns.Add(DialogUi.Text("Trạng thái", nameof(ArchLayerRow.State), true, 70));
        var autoCol = DialogUi.Text("Theo quy tắc", nameof(ArchLayerRow.AutoLabel), true, 90);
        _gLayers.Columns.Add(autoCol);
        _gLayers.Columns.Add(DialogUi.Combo("Gán nhóm", nameof(ArchLayerRow.Choice), ArchLayerRow.Choices, 120));
        _gLayers.Columns.Add(DialogUi.Text("Nhóm dùng", nameof(ArchLayerRow.EffectiveLabel), true, 90));
        _gLayers.ItemsSource = _layerRows;
        _gLayers.SelectionChanged += (s, e) => DrawLayerPreview();
        _gLayers.LoadingRow += (s, e) => { if (e.Row.Item is ArchLayerRow r) e.Row.ToolTip = string.IsNullOrEmpty(r.Reason) ? null : "Quy tắc: " + r.Reason + (r.Info.Files.Count > 0 ? "\nFile: " + string.Join(", ", r.Info.Files) : ""); };

        _chkOnlyUsed = new CheckBox { Content = "Chỉ layer có thực thể", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        _chkOnlyUsed.Checked += (s, e) => FillLayerRows();
        _chkOnlyUsed.Unchecked += (s, e) => FillLayerRows();
        _layerInfo = new TextBlock { Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2) };

        var bar = DialogUi.Row(
            DialogUi.Btn("Quét layer", async (s, e) => await ScanLayers(true), true),
            DialogUi.Btn("Lưu làm mặc định", (s, e) => SaveLayerDefault()),
            DialogUi.Btn("Khôi phục mặc định", (s, e) => RestoreLayerDefault()),
            DialogUi.Btn("Chuẩn layer CAD…", (s, e) => OpenStandardWindow()),
            _chkOnlyUsed);

        _layerCanvas = new Canvas();
        _layerHost = new Border { ClipToBounds = true, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)), BorderThickness = new Thickness(1), Child = _layerCanvas, MinWidth = 200 };
        _layerHost.SizeChanged += (s, e) => DrawLayerPreview();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        Grid.SetColumn(_gLayers, 0); grid.Children.Add(_gLayers);
        var split = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Color.FromRgb(224, 224, 224)) };
        Grid.SetColumn(split, 1); grid.Children.Add(split);
        Grid.SetColumn(_layerHost, 2); grid.Children.Add(_layerHost);

        var dock = new DockPanel { Margin = new Thickness(2) };
        DockPanel.SetDock(bar, Dock.Top); dock.Children.Add(bar);
        var hint = DialogUi.Hint("Gán mỗi layer vào nhóm cấu kiện (Tường, Cột, Dầm, Sàn, Trục, Cửa, Cầu thang, Tên phòng, Bỏ qua). \"(theo quy tắc)\" = tự nhận theo quy tắc của Chuẩn layer CAD. " +
                                 "Layer Dầm: dầm vẽ sẵn (2 nét hoặc 1 nét) được dùng làm dầm; layer Sàn: đa tuyến kín = vùng sàn, nằm trong vùng sàn khác = lỗ sàn. Chọn dòng để tô đậm các layer cùng nhóm trên hình.");
        DockPanel.SetDock(hint, Dock.Top); dock.Children.Add(hint);
        DockPanel.SetDock(_layerInfo, Dock.Bottom); dock.Children.Add(_layerInfo);
        dock.Children.Add(grid);
        _layerTab = new TabItem { Header = "Layer trong file", Content = dock };
        tabRight.Items.Add(_layerTab);
        tabRight.SelectionChanged += async (s, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, tabRight) && tabRight.SelectedItem == _layerTab && _scanStale && _files.Count > 0) await ScanLayers(false);
        };
    }

    /// <summary>Gán quy tắc + bảng gán trực tiếp vào thông số nhận diện.</summary>
    private void ApplyLayerOptions(ArchImportOptions o)
    {
        if (_std == null) return;
        try { _gLayers?.CommitEdit(DataGridEditingUnit.Row, true); } catch (InvalidOperationException) { }
        o.LayerRules = _std.InputRules.Select(r => r.Clone()).ToList();
        var map = _std.InputLayerMap.Where(m => !_layerAssign.ContainsKey(m.Layer)).Select(m => new LayerMapEntry(m.Layer, m.Category)).ToList();
        foreach (var kv in _layerAssign)
            map.Add(new LayerMapEntry(kv.Key, kv.Value == "-" ? CadCategory.BoQua : kv.Value));
        o.LayerMap = map;
        o.SkipOffLayers = _std.SkipOffLayers;
    }

    private void MarkLayersStale() { _scanStale = true; if (tabRight.SelectedItem == _layerTab && _files.Count > 0) _ = ScanLayers(false); }

    private async Task ScanLayers(bool force)
    {
        if (_busy) return;
        if (_files.Count == 0) { SetStatus("Chưa chọn file bản vẽ."); return; }
        if (!force && !_scanStale) return;
        if (!TryUiToOptions(out string err)) { Warn(err); return; }
        var paths = _files.ToList();
        var o = Options;
        CadLayerScanResult r = null;
        bool ok = await RunBusy("Đang quét layer bản vẽ...", () => r = CadLayerScan.Scan(paths, o), "Lỗi quét layer");
        if (!ok || r == null) return;
        _scan = r; _scanStale = false;
        FillLayerRows();
        SetStatus($"{r.Layers.Count} layer trong {paths.Count} file; {r.Layers.Count(l => l.Category.Length > 0)} layer tự nhận được nhóm." +
                  (r.Warnings.Count > 0 ? " " + string.Join(" ", r.Warnings.Take(2)) : ""));
    }

    private void FillLayerRows()
    {
        _layerRows.Clear();
        if (_scan == null) { DrawLayerPreview(); return; }
        bool only = _chkOnlyUsed?.IsChecked == true;
        foreach (var li in _scan.Layers.Where(l => !only || l.Total > 0))
        {
            string a = _layerAssign.TryGetValue(li.Name, out var v) ? v : "";
            // layer đã gán trực tiếp trong chuẩn (InputLayerMap) hiện là "theo quy tắc" (Category đã gồm cả gán trực tiếp)
            _layerRows.Add(new ArchLayerRow(li, a, LayerAssigned));
        }
        _layerInfo.Text = string.Join("  ·  ", _scan.Info) + (_scan.Warnings.Count > 0 ? "\n" + string.Join("\n", _scan.Warnings) : "");
        DrawLayerPreview();
    }

    private void LayerAssigned(ArchLayerRow r)
    {
        if (r.Assigned.Length == 0) _layerAssign.Remove(r.Name); else _layerAssign[r.Name] = r.Assigned;
        SetStatus($"Layer \"{r.Name}\" → {r.EffectiveLabel}. Bấm “Nhận diện” để đọc lại bản vẽ; “Lưu làm mặc định” để dùng cho các lần sau.");
        DrawLayerPreview();
    }

    private void SaveLayerDefault()
    {
        try { _gLayers.CommitEdit(DataGridEditingUnit.Row, true); } catch (InvalidOperationException) { }
        try
        {
            var d = CadStandard.LoadUserDefault();
            d.InputRules = _std.InputRules.Select(q => q.Clone()).ToList();
            d.SkipOffLayers = _std.SkipOffLayers;
            foreach (var kv in _layerAssign) d.SetMapping(kv.Key, kv.Value == "-" ? CadCategory.BoQua : kv.Value);
            CadStandard.SaveUserDefault(d);
            _std.InputLayerMap = d.InputLayerMap.Select(m => new LayerMapEntry(m.Layer, m.Category)).ToList();
            SetStatus($"Đã lưu {_layerAssign.Count} gán layer vào chuẩn mặc định ({CadStandard.UserFilePath}).");
        }
        catch (Exception ex) { Warn("Không lưu được chuẩn mặc định: " + ex.Message); }
    }

    private void RestoreLayerDefault()
    {
        if (MessageBox.Show(this, "Bỏ mọi gán layer trực tiếp và dùng lại quy tắc nhận diện mặc định của chương trình?\n(Bấm “Lưu làm mặc định” để ghi lại cho các lần sau.)",
                Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _layerAssign.Clear();
        _std.InputRules = CadStandard.DefaultInputRules();
        _std.InputLayerMap = new List<LayerMapEntry>();
        _std.SkipOffLayers = true;
        Options.WallLayers = new ArchImportOptions().WallLayers; Options.GridLayers = new ArchImportOptions().GridLayers;
        Options.ColumnLayers = new ArchImportOptions().ColumnLayers; Options.DoorLayers = new ArchImportOptions().DoorLayers;
        Options.StairLayers = new ArchImportOptions().StairLayers;
        OptionsToUi();
        _scanStale = true;
        _ = ScanLayers(true);
    }

    private void OpenStandardWindow()
    {
        var w = new CadStandardWindow(null) { Owner = this };
        w.SelectTab(0);
        w.ShowDialog();
        if (!w.Changed) return;
        try { _std = CadStandard.LoadUserDefault(); } catch (Exception) { }
        _scanStale = true;
        _ = ScanLayers(true);
    }

    // ------------------------------------------------------------------ xem trước layer
    private static readonly Brush DimBr = MakeBr(Color.FromRgb(215, 215, 215));
    private static readonly Brush CatBr = MakeBr(Color.FromRgb(0, 120, 215));
    private static readonly Brush SelBr = MakeBr(Color.FromRgb(220, 30, 30));
    private static Brush MakeBr(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    private void DrawLayerPreview()
    {
        if (_layerCanvas == null) return;
        _layerCanvas.Children.Clear();
        if (_scan == null || _scan.Segments.Count == 0 || _scan.X1 <= _scan.X0)
        {
            _layerCanvas.Children.Add(new TextBlock { Text = _files.Count == 0 ? "Chọn file bản vẽ." : "Bấm “Quét layer”.", Foreground = Brushes.Gray, Margin = new Thickness(8) });
            return;
        }
        double w = _layerHost.ActualWidth, h = _layerHost.ActualHeight;
        if (w < 20 || h < 20) return;
        double dx = Math.Max(_scan.X1 - _scan.X0, 1e-3), dy = Math.Max(_scan.Y1 - _scan.Y0, 1e-3);
        double k = Math.Min((w - 20) / dx, (h - 20) / dy);
        double ox = (w - dx * k) / 2 - _scan.X0 * k, oy = h - (h - dy * k) / 2 + _scan.Y0 * k;
        var sel = _gLayers.SelectedItem as ArchLayerRow;
        string cat = sel?.Effective ?? "";
        // nhóm hiện dùng của từng layer (theo chỉ số trong _scan.Layers)
        var eff = _scan.Layers.Select(li =>
        {
            if (_layerAssign.TryGetValue(li.Name, out var a)) return a == "-" ? "" : a;
            return li.Category;
        }).ToArray();
        var geo = new[] { new StreamGeometry(), new StreamGeometry(), new StreamGeometry() };
        var ctx = geo.Select(g => g.Open()).ToArray();
        try
        {
            foreach (var s in _scan.Segments)
            {
                int g = sel != null && s.Layer >= 0 && s.Layer < _scan.Layers.Count && _scan.Layers[s.Layer] == sel.Info ? 2
                      : cat.Length > 0 && s.Layer >= 0 && s.Layer < eff.Length && eff[s.Layer] == cat ? 1 : 0;
                ctx[g].BeginFigure(new Point(ox + s.X1 * k, oy - s.Y1 * k), false, false);
                ctx[g].LineTo(new Point(ox + s.X2 * k, oy - s.Y2 * k), true, false);
            }
        }
        finally { foreach (var c in ctx) c.Close(); }
        var brushes = new[] { DimBr, CatBr, SelBr };
        var thick = new[] { 0.6, 1.2, 1.6 };
        for (int i = 0; i < 3; i++)
        {
            geo[i].Freeze();
            _layerCanvas.Children.Add(new Shapes.Path { Data = geo[i], Stroke = brushes[i], StrokeThickness = thick[i], IsHitTestVisible = false });
        }
        if (sel != null)
            _layerCanvas.Children.Add(new TextBlock
            {
                Text = $"Đỏ: layer {sel.Name}   ·   Xanh: các layer nhóm {sel.EffectiveLabel}",
                Foreground = Brushes.DimGray, FontSize = 11, Margin = new Thickness(6, 4, 0, 0), Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255))
            });
    }
}
