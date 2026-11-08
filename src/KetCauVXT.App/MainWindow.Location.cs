using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KetCauVXT.Model;

namespace KetCauVXT.App;

// =====================================================================
// ĐỊA ĐIỂM XÂY DỰNG → VÙNG GIÓ (W0), agR, LOẠI NỀN (QCVN 02:2022/BXD)
// Tab "Công trình": chọn Tỉnh/Thành (34 đơn vị từ 01/7/2025, tìm được theo tên tỉnh cũ),
// Địa danh (xã/phường/huyện cũ từ CSDL của người dùng), Vùng gió I…V → điền W0.
// CSDL địa danh: %AppData%\KetCauVXT\DiaDanh.json (người dùng nhập từ Excel/CSV Bảng 5.1 hoặc lưu dần).
// =====================================================================
public partial class MainWindow
{
    private LocationDb _locDb = new();
    private bool _locUpdating;
    /// <summary>Chuỗi hiển thị trong ô Tỉnh/Thành → tỉnh mới (gồm cả mục "tỉnh cũ → tỉnh mới")</summary>
    private readonly Dictionary<string, ProvinceInfo> _provChoices = new();
    /// <summary>Chuỗi hiển thị trong ô Địa danh → bản ghi CSDL</summary>
    private readonly Dictionary<string, LocationEntry> _nameChoices = new();

    internal static string LocationDbPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KetCauVXT", "DiaDanh.json");

    private void InitLocationUi()
    {
        try { _locDb = LocationDb.Load(LocationDbPath); }
        catch (Exception ex)
        {
            // giữ lại file lỗi để không ghi đè dữ liệu của người dùng
            try { File.Copy(LocationDbPath, LocationDbPath + ".loi-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak", true); } catch { }
            _locDb = new LocationDb();
            MessageBox.Show("Không đọc được cơ sở dữ liệu địa danh:\n" + LocationDbPath + "\n\n" + ex.Message +
                "\n\nĐã lưu bản sao file lỗi (.bak) cạnh file gốc; chương trình dùng danh mục rỗng.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _provChoices.Clear();
        var items = new List<string>();
        foreach (var p in LocationDb.Provinces.OrderBy(p => LocationDb.ProvinceKey(p.Name), StringComparer.Ordinal))
        { items.Add(p.Name); _provChoices[p.Name] = p; }
        var olds = new List<(string key, string text, ProvinceInfo p)>();
        foreach (var p in LocationDb.Provinces)
        {
            foreach (var f in p.Former.Where(f => LocationDb.ProvinceKey(f) != LocationDb.ProvinceKey(p.Name)))
                olds.Add((LocationDb.ProvinceKey(f), $"{f} (tỉnh cũ) → {p.Name}", p));
            foreach (var a in p.Aliases.Take(1))
                olds.Add((LocationDb.ProvinceKey(a), $"{a} (tên cũ) → {p.Name}", p));
        }
        foreach (var o in olds.OrderBy(o => o.key, StringComparer.Ordinal)) { items.Add(o.text); _provChoices[o.text] = o.p; }
        cbLocProvince.ItemsSource = items;

        cbWindZone.ItemsSource = new[] { "(chưa chọn)" }.Concat(WindZones.All.Select(z => z.Label)).ToList();
        cbWindZone.SelectedIndex = 0;
        cbWindZone.ToolTip = string.Join("\n", WindZones.All.Select(z => $"Vùng {z.Zone}: {z.Describe()}")) + "\nNguồn: " + WindZones.Source;
        cbTerrain.ToolTip = TerrainInfo.HelpText();
        UpdateZoneInfo(); UpdateTerrainInfo(); UpdateLocStatus();
    }

    // ------------------------------------------------------------------
    private ProvinceInfo CurrentProvince()
    {
        var t = (cbLocProvince.SelectedItem as string ?? cbLocProvince.Text ?? "").Trim();
        if (t.Length == 0) return null;
        return _provChoices.TryGetValue(t, out var p) ? p : LocationDb.FindProvince(t);
    }

    private string SelectedZone() =>
        cbWindZone.SelectedIndex >= 1 && cbWindZone.SelectedIndex <= WindZones.All.Count ? WindZones.All[cbWindZone.SelectedIndex - 1].Zone : "";

    private void SelectZone(string zone)
    {
        var z = WindZones.Find(zone);
        cbWindZone.SelectedIndex = z == null ? 0 : WindZones.All.ToList().IndexOf(z) + 1;
    }

    private LocationEntry CurrentEntry()
    {
        var t = (cbLocName.Text ?? "").Trim();
        if (t.Length == 0) return null;
        if (_nameChoices.TryGetValue(t, out var e)) return e;
        var p = CurrentProvince();
        return p == null ? null : _locDb.Find(p.Name, t);
    }

    private void RefreshNameChoices()
    {
        _nameChoices.Clear();
        var p = CurrentProvince();
        var names = new List<string>();
        if (p != null)
            foreach (var e in _locDb.ForProvince(p.Name))
            {
                var d = e.Display;
                if (_nameChoices.ContainsKey(d)) continue;
                _nameChoices[d] = e; names.Add(d);
            }
        // danh mục xã / phường / đặc khu sau sắp xếp (Model/AdminUnits.cs) – chưa có số liệu gió thì chọn vùng gió để lưu lại
        if (p != null)
        {
            var have = new HashSet<string>(names.Select(n => LocationDb.PlaceKey(_nameChoices[n].Name)));
            foreach (var c in AdminUnits.Of(p.Name).OrderBy(c => c.Type == "Phường" ? 0 : c.Type == "Xã" ? 1 : 2).ThenBy(c => LocationDb.Norm(c.ShortName), StringComparer.Ordinal))
                if (have.Add(LocationDb.PlaceKey(c.FullName))) names.Add(c.FullName);
        }
        string keep = cbLocName.Text;
        bool was = _locUpdating; _locUpdating = true;
        try { cbLocName.ItemsSource = names; cbLocName.Text = keep; }
        finally { _locUpdating = was; }
    }

    /// <summary>Hiện vị trí từ dự án (gọi trong LoadUiFromProject, sau khi đã điền W0/agR).</summary>
    private void LoadLocationUi()
    {
        _locUpdating = true;
        try
        {
            var p = LocationDb.FindProvince(_p.LocationProvince);
            if (p != null) cbLocProvince.SelectedItem = p.Name;
            else { cbLocProvince.SelectedItem = null; cbLocProvince.Text = _p.LocationProvince ?? ""; }
            RefreshNameChoices();
            cbLocName.Text = _p.LocationName ?? "";
            SelectZone(_p.Wind.Zone);
        }
        finally { _locUpdating = false; }
        UpdateZoneInfo(); UpdateTerrainInfo(); UpdateLocStatus();
    }

    /// <summary>Ghi vị trí vào dự án (gọi trong ApplyUiToProject).</summary>
    private void ApplyLocationUi()
    {
        var p = CurrentProvince();
        _p.LocationProvince = p?.Name ?? (cbLocProvince.Text ?? "").Trim();
        _p.LocationName = (cbLocName.Text ?? "").Trim();
        _p.Wind.Zone = SelectedZone();
    }

    // ------------------------------------------------------------------
    // Sự kiện
    // ------------------------------------------------------------------
    private void CbLocProvince_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_locUpdating || _loadingUi) return;
        var text = cbLocProvince.SelectedItem as string;
        if (text == null) return;
        // đang gõ (tự hoàn thành) → xử lý khi rời ô
        if (cbLocProvince.IsKeyboardFocusWithin && !cbLocProvince.IsDropDownOpen) return;
        var p = CurrentProvince();
        if (p != null && text != p.Name)
        {
            // chọn theo tên tỉnh cũ → chuyển về tên tỉnh mới
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _locUpdating = true;
                try { cbLocProvince.SelectedItem = p.Name; } finally { _locUpdating = false; }
                OnProvinceChanged();
            }));
            return;
        }
        OnProvinceChanged();
    }

    /// <summary>Phần tử nhận focus mới có nằm trong ô chọn (ô gõ, danh sách thả xuống) không.</summary>
    private static bool FocusStaysIn(DependencyObject owner, IInputElement newFocus)
    {
        var d = newFocus as DependencyObject;
        while (d != null)
        {
            if (d == owner) return true;
            d = (d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : null) ?? LogicalTreeHelper.GetParent(d);
        }
        return false;
    }

    private void CbLocProvince_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_locUpdating || _loadingUi) return;
        if (FocusStaysIn(cbLocProvince, e.NewFocus)) return; // focus chỉ chuyển giữa ô gõ và danh sách thả xuống
        var t = (cbLocProvince.Text ?? "").Trim();
        if (t.Length == 0) { RefreshNameChoices(); UpdateLocStatus(); return; }
        var p = CurrentProvince() ?? LocationDb.SearchProvinces(t).FirstOrDefault();
        if (p == null) { UpdateLocStatus(); return; }
        if ((cbLocProvince.SelectedItem as string) != p.Name || cbLocProvince.Text != p.Name || p.Name != (_p.LocationProvince ?? ""))
        {
            _locUpdating = true;
            try { cbLocProvince.SelectedItem = p.Name; } finally { _locUpdating = false; }
            _p.LocationProvince = p.Name;
            OnProvinceChanged();
        }
    }

    private void OnProvinceChanged()
    {
        if (CurrentProvince() is { } cp) _p.LocationProvince = cp.Name;
        RefreshNameChoices();
        var en = CurrentEntry();
        if (en != null) ApplyEntry(en); else UpdateLocStatus();
        MarkDirty(false);
    }

    private void CbLocName_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_locUpdating || _loadingUi) return;
        if (cbLocName.IsKeyboardFocusWithin && !cbLocName.IsDropDownOpen) return;
        if (cbLocName.SelectedItem is string s && _nameChoices.TryGetValue(s, out var en)) { ApplyEntry(en); MarkDirty(false); }
    }

    private void CbLocName_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_locUpdating || _loadingUi) return;
        if (FocusStaysIn(cbLocName, e.NewFocus)) return;
        var en = CurrentEntry();
        if (en != null) ApplyEntry(en);
        else { FillLocationText(); UpdateLocStatus(); }
        if ((cbLocName.Text ?? "").Trim() != (_p.LocationName ?? "")) MarkDirty(false);
    }

    private void CbWindZone_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateZoneInfo();
        if (_locUpdating || _loadingUi || _p == null) return;
        var z = WindZones.Find(SelectedZone());
        if (z == null) { UpdateLocStatus(); return; }
        txtW0.Text = F(z.W0);
        _p.Wind.W0 = z.W0; _p.Wind.Zone = z.Zone;
        // người dùng tự tra Bảng 5.1 cho địa danh chưa có số liệu → ghi nhớ cho lần sau
        var p = CurrentProvince();
        string name = (cbLocName.Text ?? "").Trim();
        var en = CurrentEntry();
        if (p != null && name.Length > 0 && (en == null || !en.HasWind))
        {
            if (en == null) en = new LocationEntry { Province = p.Name, Name = name, Source = LocationDb.SourceUser };
            en.WindZone = z.Zone; en.W0 = 0; en.Updated = DateTime.Now;
            _locDb.Upsert(en);
            if (SaveLocDb()) { RefreshNameChoices(); SetStatus($"Đã lưu vùng gió {z.Zone} cho \"{name}, {p.Name}\" vào danh mục địa danh."); }
        }
        UpdateLocStatus();
        MarkDirty();
    }

    private void CbTerrain_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateTerrainInfo();

    /// <summary>Áp dụng số liệu địa danh: vùng gió → W0; agR, loại nền nếu có.</summary>
    private void ApplyEntry(LocationEntry en)
    {
        // có thay đổi so với giá trị đang nhập không (tránh xoá kết quả phân tích khi chỉ rời ô)
        bool changed = (en.HasWind && (Math.Abs(Num(txtW0, -1) - en.EffectiveW0) > 1e-9 || (en.WindZone.Length > 0 && SelectedZone() != WindZones.Normalize(en.WindZone))))
            || (en.AgR > 0 && Math.Abs(Num(txtAgR, -1) - en.AgR) > 1e-9)
            || (!string.IsNullOrEmpty(en.GroundType) && "ABCDE".Contains(en.GroundType) && (cbSeisSoil.SelectedItem as string) != en.GroundType);
        _locUpdating = true;
        try
        {
            if (!string.IsNullOrEmpty(en.WindZone)) SelectZone(en.WindZone);
            if (en.HasWind) { txtW0.Text = F(en.EffectiveW0); _p.Wind.W0 = en.EffectiveW0; _p.Wind.Zone = WindZones.Normalize(en.WindZone); }
            if (en.AgR > 0) { txtAgR.Text = F(en.AgR); _p.Seismic.AgR = en.AgR; }
            if (!string.IsNullOrEmpty(en.GroundType) && "ABCDE".Contains(en.GroundType)) { cbSeisSoil.SelectedItem = en.GroundType; _p.Seismic.Soil = en.GroundType; }
        }
        finally { _locUpdating = false; }
        FillLocationText();
        UpdateZoneInfo(); UpdateLocStatus();
        if (en.HasData && changed)
        {
            var parts = new List<string>();
            if (en.HasWind) parts.Add($"W0 = {F(en.EffectiveW0)} kN/m²" + (en.WindZone.Length > 0 ? $" (vùng {en.WindZone})" : ""));
            if (en.AgR > 0) parts.Add($"agR = {F(en.AgR)}g");
            if (!string.IsNullOrEmpty(en.GroundType)) parts.Add($"nền {en.GroundType}");
            SetStatus($"Địa danh {en.Display}, {en.Province}: áp dụng {string.Join(", ", parts)}.");
            MarkDirty();
        }
    }

    /// <summary>Ô "Địa điểm xây dựng" còn trống → điền "Xã/phường, Tỉnh".</summary>
    private void FillLocationText()
    {
        if (!string.IsNullOrWhiteSpace(txtLocation.Text)) return;
        var p = CurrentProvince();
        string name = (cbLocName.Text ?? "").Trim();
        var en = CurrentEntry();
        if (en != null) name = en.Name;
        if (p == null) return;
        txtLocation.Text = name.Length > 0 ? $"{name}, {p.Name}" : p.Name;
    }

    private void UpdateZoneInfo()
    {
        if (txtZoneInfo == null) return;
        var z = WindZones.Find(SelectedZone());
        txtZoneInfo.Text = z == null ? "Chưa chọn vùng gió – W0 lấy theo ô nhập bên dưới." : $"Vùng {z.Zone}: {z.Describe()}";
    }

    private void UpdateTerrainInfo()
    {
        if (txtTerrainInfo == null) return;
        txtTerrainInfo.Text = TerrainInfo.Describe(cbTerrain.SelectedItem as string);
    }

    private void UpdateLocStatus()
    {
        if (txtLocStatus == null) return;
        var p = CurrentProvince();
        string name = (cbLocName.Text ?? "").Trim();
        int n = p == null ? 0 : _locDb.ForProvince(p.Name).Count;
        int nc = p == null ? 0 : AdminUnits.Of(p.Name).Count;
        string count = p == null ? "" : $" ({p.Name}: {nc} xã/phường/đặc khu sau sắp xếp, {n} địa danh đã có số liệu gió)";
        Brush color = Brushes.DimGray;
        string text;
        if (p == null)
            text = (cbLocProvince.Text ?? "").Trim().Length > 0
                ? "Không nhận ra tỉnh/thành phố – chọn trong danh sách (34 đơn vị từ 01/7/2025; gõ được tên tỉnh cũ)."
                : "Chọn tỉnh/thành phố và xã/phường nơi xây dựng để áp dụng vùng gió theo QCVN 02:2022/BXD.";
        else if (name.Length == 0)
            text = "Chọn xã/phường/đặc khu (tên sau sắp xếp 01/7/2025) hoặc gõ tên" + count + ".";
        else
        {
            var en = CurrentEntry();
            if (en != null && en.HasData)
            {
                string src = string.IsNullOrWhiteSpace(en.Source) ? LocationDb.SourceUser : en.Source;
                text = $"Nguồn: {src} (đã nhập)" + (en.AgR > 0 ? "" : " – chưa có agR, nhập tay theo Bảng 6.1/6.2 QCVN 02:2022")
                    + (string.IsNullOrWhiteSpace(en.Note) ? "" : " – " + en.Note) + ".";
                color = Brushes.DarkGreen;
            }
            else
            {
                text = "Chưa có số liệu cho địa danh này – tra Bảng 5.1 QCVN 02:2022 rồi chọn vùng gió; lựa chọn sẽ được lưu cho lần sau" + count + ".";
                color = Brushes.DarkOrange;
            }
            var zSel = WindZones.Find(SelectedZone());
            double w0 = Num(txtW0, double.NaN);
            if (zSel != null && !double.IsNaN(w0) && Math.Abs(w0 - zSel.W0) > 1e-6)
                text += $" Lưu ý: W0 đang nhập ({F(w0)}) khác W0 của vùng {zSel.Zone} ({F(zSel.W0)}).";
        }
        txtLocStatus.Text = text;
        txtLocStatus.Foreground = color;
    }

    private bool SaveLocDb()
    {
        try { _locDb.Save(LocationDbPath); return true; }
        catch (Exception ex) { ShowError("Lỗi lưu danh mục địa danh", ex); return false; }
    }

    private void BtnLocSave_Click(object sender, RoutedEventArgs e)
    {
        var p = CurrentProvince();
        string name = (cbLocName.Text ?? "").Trim();
        if (p == null || name.Length == 0)
        {
            MessageBox.Show("Chọn tỉnh/thành phố và nhập tên xã/phường (hoặc huyện cũ) trước khi lưu.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        string zone = SelectedZone();
        double w0 = Num(txtW0, 0), agr = Num(txtAgR, 0);
        string soil = cbSeisSoil.SelectedItem as string ?? "";
        if (zone.Length == 0 && agr <= 0)
        {
            MessageBox.Show("Chưa chọn vùng gió và chưa có agR – không có số liệu để lưu.\nTra Bảng 5.1 (vùng gió) và Bảng 6.1/6.2 (agR) của QCVN 02:2022/BXD.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var en = CurrentEntry();
        if (en != null && !string.IsNullOrEmpty(en.Source) && en.Source != LocationDb.SourceUser
            && ((en.WindZone.Length > 0 && en.WindZone != zone) || (en.AgR > 0 && Math.Abs(en.AgR - agr) > 1e-6)))
        {
            var r = MessageBox.Show($"Địa danh \"{en.Display}\" đã có số liệu từ \"{en.Source}\" (vùng {en.WindZone}, agR = {F(en.AgR)}).\nGhi đè bằng số liệu đang nhập (vùng {zone}, agR = {F(agr)})?",
                AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
        }
        en ??= new LocationEntry { Province = p.Name, Name = name };
        en.WindZone = zone;
        en.W0 = zone.Length > 0 && Math.Abs(WindZones.W0(zone) - w0) < 1e-6 ? 0 : (zone.Length == 0 ? 0 : w0);
        en.AgR = agr;
        en.GroundType = soil;
        en.Source = LocationDb.SourceUser;
        en.Updated = DateTime.Now;
        _locDb.Upsert(en);
        if (!SaveLocDb()) return;
        RefreshNameChoices();
        UpdateLocStatus();
        SetStatus($"Đã lưu địa danh \"{en.Display}, {en.Province}\" (vùng gió {(zone.Length > 0 ? zone : "–")}, agR = {F(agr)}g) vào {LocationDbPath}.");
    }

    private void BtnLocDb_Click(object sender, RoutedEventArgs e) => ShowLocationDb();
    private void MnuLocationDb_Click(object sender, RoutedEventArgs e) => ShowLocationDb();

    private void ShowLocationDb()
    {
        var w = new LocationDbWindow(_locDb, LocationDbPath, CurrentProvince()?.Name) { Owner = this };
        w.ShowDialog();
        if (w.PickedEntry != null)
        {
            var en = w.PickedEntry;
            var p = LocationDb.FindProvince(en.Province);
            _locUpdating = true;
            try
            {
                if (p != null) cbLocProvince.SelectedItem = p.Name;
                RefreshNameChoices();
                cbLocName.Text = en.Display;
            }
            finally { _locUpdating = false; }
            ApplyEntry(en);
            MarkDirty(false);
        }
        else RefreshNameChoices();
        UpdateLocStatus();
    }
}
