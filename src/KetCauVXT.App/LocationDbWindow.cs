using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Một dòng địa danh trong bảng (sửa trực tiếp bản ghi CSDL).</summary>
internal sealed class LocationRow : RowBase
{
    private readonly Action _changed;
    public LocationEntry E { get; }
    public LocationRow(LocationEntry e, Action changed) { E = e; _changed = changed; }
    private void Done() { E.Updated = DateTime.Now; RaiseAll(); _changed(); }

    public string Province
    {
        get => E.Province;
        set
        {
            var t = (value ?? "").Trim();
            var p = LocationDb.FindProvince(t);
            // gõ tên tỉnh cũ → đổi sang tỉnh mới và ghi nhận tỉnh cũ
            var old = LocationDb.FindFormerProvinceName(t);
            if (p != null && old != null && LocationDb.ProvinceKey(old) != LocationDb.ProvinceKey(p.Name) && string.IsNullOrWhiteSpace(E.FormerProvince))
                E.FormerProvince = old;
            E.Province = p?.Name ?? t; Done();
        }
    }
    public string FormerProvince { get => E.FormerProvince; set { var t = (value ?? "").Trim(); E.FormerProvince = LocationDb.FindFormerProvinceName(t) ?? t; Done(); } }
    public string FormerDistrict { get => E.FormerDistrict; set { E.FormerDistrict = (value ?? "").Trim(); Done(); } }
    public string Name { get => E.Name; set { var t = (value ?? "").Trim(); if (t.Length > 0) { E.Name = t; Done(); } } }
    public string WindZone { get => E.WindZone ?? ""; set { E.WindZone = WindZones.Normalize(value); Done(); } }
    public double W0 { get => E.W0; set { if (value >= 0 && value < 10) { E.W0 = value; Done(); } } }
    public double EffectiveW0 => E.EffectiveW0;
    public double AgR { get => E.AgR; set { if (value >= 0 && value < 1) { E.AgR = value; Done(); } } }
    public string GroundType { get => E.GroundType ?? ""; set { E.GroundType = (value ?? "").Trim().ToUpperInvariant(); Done(); } }
    public string Source { get => E.Source; set { E.Source = (value ?? "").Trim(); Done(); } }
    public string Note { get => E.Note; set { E.Note = (value ?? "").Trim(); Done(); } }
    public string Updated => E.Updated.ToString("dd/MM/yyyy");
}

/// <summary>Danh mục địa danh (QCVN 02:2022/BXD): xem, lọc, sửa, nhập Excel/CSV, xuất CSV mẫu.</summary>
internal sealed class LocationDbWindow : Window
{
    private readonly LocationDb _db;
    private readonly string _path;
    private readonly ComboBox _cbProv;
    private readonly TextBox _txtSearch;
    private readonly DataGrid _grid;
    private readonly TextBlock _txtCount;
    private readonly ObservableCollection<LocationRow> _rows = new();
    private bool _changed;
    private const string AllProvinces = "(Tất cả tỉnh/thành)";

    /// <summary>Địa danh người dùng bấm "Dùng cho công trình" (null nếu chỉ đóng).</summary>
    public LocationEntry PickedEntry { get; private set; }

    public LocationDbWindow(LocationDb db, string path, string province)
    {
        _db = db; _path = path;
        DialogUi.Setup(this, "Danh mục địa danh – vùng gió, agR (QCVN 02:2022/BXD)", 1180, 680);

        // ---- bảng vùng gió chính thức ----
        var zoneGrid = DialogUi.NewGrid();
        zoneGrid.IsReadOnly = true; zoneGrid.SelectionMode = DataGridSelectionMode.Single;
        zoneGrid.Columns.Add(DialogUi.Text("Vùng gió", nameof(WindZoneInfo.Zone), true, 80));
        zoneGrid.Columns.Add(DialogUi.Num("W0 (kN/m²)", nameof(WindZoneInfo.W0), "0.00", true, 100));
        zoneGrid.Columns.Add(DialogUi.Num("W0 (daN/m²)", nameof(WindZoneInfo.W0daN), "0", true, 100));
        zoneGrid.Columns.Add(DialogUi.Num("V3s,50 (m/s)", nameof(WindZoneInfo.V3s50), "0", true, 100));
        zoneGrid.Columns.Add(DialogUi.Num("V10m,50 (m/s)", nameof(WindZoneInfo.V10m50), "0", true, 110));
        zoneGrid.ItemsSource = WindZones.All;
        zoneGrid.MaxHeight = 160;
        var zoneBox = new GroupBox { Header = "Áp lực gió theo vùng – " + WindZones.Source, Content = zoneGrid, Margin = new Thickness(0, 0, 0, 6) };

        // ---- lọc ----
        _cbProv = new ComboBox { Width = 230, Margin = new Thickness(0, 0, 10, 0) };
        _cbProv.ItemsSource = new[] { AllProvinces }.Concat(LocationDb.Provinces.Select(p => p.Name).OrderBy(LocationDb.ProvinceKey, StringComparer.Ordinal)).ToList();
        _cbProv.SelectedItem = province != null && LocationDb.FindProvince(province) is { } pi ? pi.Name : AllProvinces;
        _cbProv.SelectionChanged += (s, e) => Reload();
        _txtSearch = new TextBox { Width = 220, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Gõ không dấu cũng được: tên xã/phường, huyện cũ, tỉnh cũ (vd \"bac kan\")" };
        _txtSearch.TextChanged += (s, e) => Reload();
        _txtCount = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray, Margin = new Thickness(10, 0, 0, 0) };
        var filter = DialogUi.Row(DialogUi.Label("Tỉnh/Thành:"), _cbProv, DialogUi.Label("Tìm:"), _txtSearch, _txtCount);

        // ---- bảng địa danh ----
        _grid = DialogUi.NewGrid();
        _grid.Columns.Add(DialogUi.Text("Tỉnh/Thành (mới)", nameof(LocationRow.Province), false, 130));
        _grid.Columns.Add(DialogUi.Text("Tỉnh cũ", nameof(LocationRow.FormerProvince), false, 110));
        _grid.Columns.Add(DialogUi.Text("Huyện cũ", nameof(LocationRow.FormerDistrict), false, 120));
        _grid.Columns.Add(DialogUi.Text("Địa danh (xã, phường, huyện)", nameof(LocationRow.Name), false, 190));
        _grid.Columns.Add(DialogUi.Combo("Vùng gió", nameof(LocationRow.WindZone), new[] { "", "I", "II", "III", "IV", "V" }, 70));
        _grid.Columns.Add(DialogUi.Num("W0 riêng (kN/m²)", nameof(LocationRow.W0), "0.###", false, 95));
        _grid.Columns.Add(DialogUi.Num("W0 dùng", nameof(LocationRow.EffectiveW0), "0.00", true, 65));
        _grid.Columns.Add(DialogUi.Num("agR (g)", nameof(LocationRow.AgR), "0.####", false, 70));
        _grid.Columns.Add(DialogUi.Combo("Nền", nameof(LocationRow.GroundType), new[] { "", "A", "B", "C", "D", "E" }, 50));
        _grid.Columns.Add(DialogUi.Text("Nguồn", nameof(LocationRow.Source), false, 140));
        _grid.Columns.Add(DialogUi.Text("Ghi chú", nameof(LocationRow.Note), false, 160));
        _grid.Columns.Add(DialogUi.Text("Cập nhật", nameof(LocationRow.Updated), true, 75));
        _grid.ItemsSource = _rows;
        _grid.MouseDoubleClick += (s, e) => { if (_grid.SelectedItem is LocationRow r && Keyboard_NotEditing()) Pick(r); };

        var hint = DialogUi.Hint("Chương trình chỉ có sẵn bảng giá trị theo vùng gió I–V và danh sách 34 tỉnh/thành (từ 01/7/2025). "
            + "Phân vùng theo từng địa danh (Bảng 5.1) và agR (Bảng 6.1, 6.2) của QCVN 02:2022/BXD do người dùng nhập: dùng \"Xuất CSV mẫu…\", điền theo văn bản gốc rồi \"Nhập từ Excel/CSV…\". "
            + "W0 riêng để trống (0) = lấy theo vùng gió. agR theo đơn vị g (vd 0,0797). Nguồn danh mục: " + LocationDb.SourceLabel + ". Dữ liệu lưu tại: " + _path);

        // ---- nút ----
        var bAdd = DialogUi.Btn("＋ Thêm", (s, e) => Add());
        var bDel = DialogUi.Btn("Xoá", (s, e) => Delete());
        var bImp = DialogUi.Btn("Nhập từ Excel/CSV…", (s, e) => Import());
        var bTpl = DialogUi.Btn("Xuất CSV mẫu…", (s, e) => Template());
        var bExp = DialogUi.Btn("Xuất CSV…", (s, e) => Export());
        var bUse = DialogUi.Btn("Dùng cho công trình", (s, e) => { if (_grid.SelectedItem is LocationRow r) Pick(r); }, true);
        var close = DialogUi.Btn("Đóng", (s, e) => Close());
        close.IsCancel = true;
        var bottom = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        var right = DialogUi.Row(bUse, close);
        DockPanel.SetDock(right, Dock.Right);
        bottom.Children.Add(right);
        bottom.Children.Add(DialogUi.Row(bAdd, bDel, bImp, bTpl, bExp));

        var dock = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(zoneBox, Dock.Top); dock.Children.Add(zoneBox);
        DockPanel.SetDock(filter, Dock.Top); dock.Children.Add(filter);
        DockPanel.SetDock(bottom, Dock.Bottom); dock.Children.Add(bottom);
        DockPanel.SetDock(hint, Dock.Bottom); dock.Children.Add(hint);
        dock.Children.Add(_grid);
        Content = dock;
        Closing += (s, e) => { DialogUi.Commit(_grid); if (_changed) Save(); };

        Reload();
    }

    private bool Keyboard_NotEditing() => !(_grid.CurrentCell.Column?.GetCellContent(_grid.CurrentItem) is TextBox);

    private void Reload(LocationEntry select = null)
    {
        DialogUi.Commit(_grid);
        var prov = _cbProv.SelectedItem as string;
        IEnumerable<LocationEntry> list = prov == null || prov == AllProvinces ? _db.Entries : _db.ForProvince(prov);
        var q = _txtSearch.Text;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var hits = new HashSet<LocationEntry>(_db.Search(q));
            list = list.Where(hits.Contains);
        }
        _rows.Clear();
        foreach (var e in list.OrderBy(e => LocationDb.ProvinceKey(e.Province), StringComparer.Ordinal)
                     .ThenBy(e => LocationDb.PlaceKey(e.Name), StringComparer.Ordinal))
            _rows.Add(new LocationRow(e, () => _changed = true));
        _txtCount.Text = $"{_rows.Count} / {_db.Entries.Count} địa danh";
        if (select != null)
        {
            var r = _rows.FirstOrDefault(x => x.E == select);
            if (r != null) { _grid.SelectedItem = r; _grid.ScrollIntoView(r); }
        }
    }

    private bool Save()
    {
        try { _db.Save(_path); _changed = false; return true; }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Không lưu được danh mục địa danh:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void Add()
    {
        var prov = _cbProv.SelectedItem as string;
        var e = new LocationEntry
        {
            Province = prov == null || prov == AllProvinces ? "" : prov,
            Name = "Địa danh mới " + (_db.Entries.Count + 1),
            Source = LocationDb.SourceUser
        };
        _db.Entries.Add(e);
        _changed = true;
        _txtSearch.Text = "";
        Reload(e);
    }

    private void Delete()
    {
        DialogUi.Commit(_grid);
        var sel = _grid.SelectedItems.OfType<LocationRow>().ToList();
        if (sel.Count == 0) return;
        if (MessageBox.Show(this, $"Xoá {sel.Count} địa danh đã chọn khỏi danh mục?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        foreach (var r in sel) _db.Remove(r.E);
        _changed = true;
        Save();
        Reload();
    }

    private void Import()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Nhập bảng địa danh (QCVN 02:2022 Bảng 5.1 / 6.1 / 6.2)",
            Filter = "Bảng tính (*.xlsx;*.csv;*.txt)|*.xlsx;*.xlsm;*.csv;*.txt|Excel (*.xlsx)|*.xlsx;*.xlsm|CSV (*.csv;*.txt)|*.csv;*.txt|Tất cả (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var res = _db.ImportFile(dlg.FileName);
            _changed = true;
            Save();
            _txtSearch.Text = "";
            _cbProv.SelectedItem = AllProvinces;
            Reload();
            var msg = res.ToString();
            if (res.Messages.Count > 0)
                msg += "\n\n" + string.Join("\n", res.Messages.Take(20)) + (res.Messages.Count > 20 ? $"\n… và {res.Messages.Count - 20} ghi chú khác." : "");
            msg += "\n\nHãy đối chiếu lại với văn bản QCVN 02:2022/BXD gốc trước khi dùng.";
            MessageBox.Show(this, msg, "Kết quả nhập địa danh", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Không nhập được file:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Template()
    {
        var dlg = new SaveFileDialog { Title = "Xuất CSV mẫu", Filter = "CSV (*.csv)|*.csv", FileName = "DiaDanh_mau.csv" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            LocationDb.WriteTemplateCsv(dlg.FileName);
            MessageBox.Show(this, "Đã ghi file mẫu:\n" + dlg.FileName + "\n\nHai dòng ví dụ (tên giả, ghi chú \"" + LocationDb.ExampleMark + "\") sẽ được bỏ qua khi nhập. "
                + "Điền mỗi dòng một địa danh theo Bảng 5.1 (vùng gió) và Bảng 6.1/6.2 (agR) của QCVN 02:2022/BXD.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Export()
    {
        DialogUi.Commit(_grid);
        var dlg = new SaveFileDialog { Title = "Xuất danh mục địa danh", Filter = "CSV (*.csv)|*.csv", FileName = "DiaDanh.csv" };
        if (dlg.ShowDialog(this) != true) return;
        try { LocationDb.ExportCsv(dlg.FileName, _rows.Select(r => r.E)); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Pick(LocationRow r)
    {
        DialogUi.Commit(_grid);
        if (LocationDb.FindProvince(r.E.Province) == null)
        {
            MessageBox.Show(this, $"Tỉnh/thành \"{r.E.Province}\" không thuộc danh sách 34 đơn vị – sửa cột Tỉnh/Thành trước.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        PickedEntry = r.E;
        Close();
    }
}
