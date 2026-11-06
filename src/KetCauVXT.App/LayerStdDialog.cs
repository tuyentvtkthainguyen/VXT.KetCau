using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;
using Microsoft.Win32;
using CadStandard = KetCauVXT.Model.CadStandard;
using LayerMapEntry = KetCauVXT.Model.LayerMapEntry;

namespace KetCauVXT.App;

/// <summary>"Chuẩn hoá layer…" trong chương trình (không cần AutoCAD): bảng layer gốc → layer chuẩn VXT-… (tự nhận theo tên + nội dung, sửa được),
/// bộ ánh xạ lưu trên máy (%AppData%\VietConst\layer-profiles), xuất DXF đã chuẩn hoá. Không sửa file gốc; không gửi dữ liệu đi đâu.</summary>
public sealed class LayerStdDialog : Window
{
    internal sealed class Row : RowBase
    {
        private string _target = "";
        public string Layer { get; set; } = "";
        public int Count { get; set; }
        public string Confidence { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Target
        {
            get => _target;
            set { if (_target == value) return; _target = value ?? ""; Reason = "người dùng chọn"; Raise(); Raise(nameof(Reason)); }
        }
        internal void SetAuto(LayerSuggestion s)
        {
            _target = s.Key.Length == 0 ? Keep : s.Target;
            Confidence = Math.Round(s.Confidence * 100).ToString(CultureInfo.InvariantCulture) + "%";
            Reason = s.Reason;
            RaiseAll();
        }
        public string Key => StdLayers.Of(Target)?.Key ?? "";
    }

    public const string Keep = "(giữ nguyên)";

    private readonly string _path;
    private readonly double _scale;
    private DxfReader _dxf;
    private Dictionary<string, LayerStats> _stats = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<Row> _rows = new();
    private readonly DataGrid _grid = DialogUi.NewGrid();
    private readonly ComboBox _profiles = new() { Width = 220 };
    private readonly TextBlock _status = new() { Margin = new Thickness(6, 2, 6, 2), TextWrapping = TextWrapping.Wrap };
    private LayerProfile _profile;

    /// <summary>Gán layer → nhóm cấu kiện để đọc bản vẽ (ArchImportOptions.LayerMap).</summary>
    public List<LayerMapEntry> Map { get; private set; } = new();
    public LayerProfile Profile => _profile;

    public LayerStdDialog(string cadPath, double scaleOverride, LayerProfile profile = null)
    {
        _path = cadPath; _scale = scaleOverride; _profile = profile;
        DialogUi.Setup(this, "Chuẩn hoá layer bản vẽ → layer chuẩn Việt Const (VXT-…)", 1100, 680);
        ResizeMode = ResizeMode.CanResize;

        var dock = new DockPanel { Margin = new Thickness(8) };
        var top = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        top.Children.Add(new TextBlock { Text = "Bộ ánh xạ:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        top.Children.Add(_profiles);
        top.Children.Add(new TextBlock { Width = 8 });
        top.Children.Add(DialogUi.Btn("Tự nhận lại", (s, e) => Suggest()));
        top.Children.Add(DialogUi.Btn("Mở bộ ánh xạ…", (s, e) => OpenProfile()));
        top.Children.Add(DialogUi.Btn("Lưu bộ ánh xạ…", (s, e) => SaveProfile()));
        top.Children.Add(DialogUi.Btn("Xuất DXF đã chuẩn hoá…", (s, e) => ExportDxf()));
        DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
        var hint = DialogUi.Hint("Chương trình đoán layer chuẩn theo TÊN layer (tiếng Việt / Anh, có / không dấu, A-WALL, S-COLS…) và NỘI DUNG (cặp nét cách 110 / 220 = tường, "
                                 + "chữ nhật nhỏ kín = cột, nét gạch chấm + vòng tròn = trục, block cửa, chữ tên phòng). Sửa cột \"Layer chuẩn\" nếu chưa đúng. "
                                 + "File gốc không bị sửa; bấm Đồng ý để đọc bản vẽ theo bảng này.");
        DockPanel.SetDock(hint, Dock.Top); dock.Children.Add(hint);

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
        bottom.Children.Add(DialogUi.Btn("Đồng ý – đọc bản vẽ theo layer chuẩn", (s, e) => { Finish(); DialogResult = true; }, true));
        bottom.Children.Add(DialogUi.Btn("Huỷ", (s, e) => DialogResult = false));
        DockPanel.SetDock(bottom, Dock.Bottom); dock.Children.Add(bottom);
        DockPanel.SetDock(_status, Dock.Bottom); dock.Children.Add(_status);

        var targets = new List<string> { Keep };
        targets.AddRange(StdLayers.All.Select(s => s.Name));
        _grid.Columns.Add(DialogUi.Text("Layer gốc", "Layer", true, 260));
        _grid.Columns.Add(DialogUi.Text("Số đối tượng", "Count", true, 90));
        _grid.Columns.Add(DialogUi.Combo("Layer chuẩn", "Target", targets, 170));
        _grid.Columns.Add(DialogUi.Text("Tin cậy", "Confidence", true, 70));
        _grid.Columns.Add(DialogUi.Text("Căn cứ", "Reason", true, 460));
        _grid.ItemsSource = _rows;
        dock.Children.Add(_grid);
        Content = dock;

        FillProfiles();
        _profiles.SelectionChanged += (s, e) =>
        {
            if (_profiles.SelectedItem is ComboBoxItem it)
            {
                string f = it.Tag as string;
                _profile = string.IsNullOrEmpty(f) ? null : LayerConvertIo.LoadProfile(f);
                Suggest();
            }
        };
        Loaded += (s, e) => Scan();
    }

    private void FillProfiles()
    {
        _profiles.Items.Clear();
        _profiles.Items.Add(new ComboBoxItem { Content = "(không dùng – tự nhận)", Tag = "" });
        try
        {
            if (Directory.Exists(LayerConvertIo.ProfileDir))
                foreach (var f in Directory.GetFiles(LayerConvertIo.ProfileDir, "*.json").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    _profiles.Items.Add(new ComboBoxItem { Content = Path.GetFileNameWithoutExtension(f), Tag = f });
        }
        catch (Exception) { }
        _profiles.SelectedIndex = 0;
    }

    private void Scan()
    {
        try
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            _dxf = LayerConvertIo.ReadFile(_path, _scale, out string err);
            if (_dxf == null) { _status.Text = "Không đọc được bản vẽ: " + err; return; }
            _stats = LayerConvertIo.StatsFrom(_dxf);
            Suggest();
        }
        catch (Exception ex) { _status.Text = "Lỗi: " + ex.Message; }
        finally { Cursor = null; }
    }

    private void Suggest()
    {
        if (_stats.Count == 0) return;
        CadStandard std = null;
        try { std = CadStandard.Active; } catch (Exception) { }
        var sugg = LayerConvertIo.Suggest(_stats, _profile, std);
        _rows.Clear();
        foreach (var s in sugg)
        {
            var r = new Row { Layer = s.Layer, Count = _stats.TryGetValue(s.Layer, out var st) ? st.Total : 0 };
            r.SetAuto(s);
            _rows.Add(r);
        }
        int known = _rows.Count(r => r.Key.Length > 0);
        _status.Text = $"{_rows.Count} layer có đối tượng – nhận được {known}, giữ nguyên {_rows.Count - known}." + (_profile != null ? $" Đang dùng bộ ánh xạ \"{_profile.Name}\"." : "");
    }

    private List<LayerSuggestion> Current()
    {
        DialogUi.Commit(_grid);
        return _rows.Select(r => new LayerSuggestion { Layer = r.Layer, Key = r.Key, Confidence = 1 }).ToList();
    }

    private void Finish() => Map = LayerConvertIo.ToLayerMap(Current());

    private void OpenProfile()
    {
        var dlg = new OpenFileDialog { Filter = "Bộ ánh xạ layer (*.json)|*.json", Title = "Mở bộ ánh xạ layer" };
        try { Directory.CreateDirectory(LayerConvertIo.ProfileDir); dlg.InitialDirectory = LayerConvertIo.ProfileDir; } catch (Exception) { }
        if (dlg.ShowDialog(this) != true) return;
        _profile = LayerConvertIo.LoadProfile(dlg.FileName);
        if (_profile == null) { MessageBox.Show(this, "Không đọc được bộ ánh xạ.", Title); return; }
        Suggest();
    }

    private void SaveProfile()
    {
        var dlg = new SaveFileDialog { Filter = "Bộ ánh xạ layer (*.json)|*.json", Title = "Lưu bộ ánh xạ layer", FileName = (_profile?.Name ?? Path.GetFileNameWithoutExtension(_path ?? "BoAnhXa")) + ".json" };
        try { Directory.CreateDirectory(LayerConvertIo.ProfileDir); dlg.InitialDirectory = LayerConvertIo.ProfileDir; } catch (Exception) { }
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            LayerConvertIo.SaveProfile(LayerConvertIo.ToProfile(Path.GetFileNameWithoutExtension(dlg.FileName), Current()), dlg.FileName);
            FillProfiles();
            _status.Text = "Đã lưu bộ ánh xạ: " + dlg.FileName;
        }
        catch (Exception ex) { MessageBox.Show(this, "Không lưu được: " + ex.Message, Title); }
    }

    private void ExportDxf()
    {
        if (_dxf == null) return;
        var dlg = new SaveFileDialog { Filter = "Bản vẽ DXF (*.dxf)|*.dxf", Title = "Xuất DXF đã chuẩn hoá layer", FileName = Path.GetFileNameWithoutExtension(_path ?? "BanVe") + "_VXT.dxf" };
        try { dlg.InitialDirectory = Path.GetDirectoryName(_path); } catch (Exception) { }
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var keys = Current().ToDictionary(s => s.Layer, s => s.Key, StringComparer.OrdinalIgnoreCase);
            var n = LayerConvertIo.ExportStandardDxf(_dxf, l => keys.TryGetValue(l, out var k) ? k : "", dlg.FileName);
            _status.Text = $"Đã xuất {n.Values.Sum()} đối tượng: {dlg.FileName}";
        }
        catch (Exception ex) { MessageBox.Show(this, "Không xuất được DXF: " + ex.Message, Title); }
    }
}
