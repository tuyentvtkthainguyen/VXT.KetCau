using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Analysis;
using KetCauVXT.Loads;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// Menu Khai báo (Define của ETABS): vật liệu, trường hợp tải (load patterns), trường hợp phân tích (load cases),
// phổ phản ứng / động đất. Tổ hợp tải: ComboEditorWindow.cs; tiết diện: SectionManagerWindow.cs; nguồn khối lượng: ModalDialog.cs.
// =====================================================================

/// <summary>Dòng bảng vật liệu: hiển thị MPa, kN/m³; sửa trực tiếp vào Material của dự án.</summary>
internal sealed class MaterialRow : RowBase
{
    private readonly Project _p;
    public Material M { get; }
    public MaterialRow(Project p, Material m) { _p = p; M = m; }
    private static double Mpa(double v) => v / U.MPa;
    public string Name => M.Name;
    public string Kind => M.Kind switch { MaterialKind.Concrete => "Bê tông", MaterialKind.Rebar => "Cốt thép", _ => "Thép hình" };
    public double Gamma { get => M.UnitWeight; set { M.UnitWeight = value; Raise(); } }
    public double E { get => Mpa(M.E); set { M.E = value * U.MPa; Raise(); } }
    public double Nu { get => M.Poisson; set { M.Poisson = value; Raise(); } }
    public double Alpha { get => M.Alpha * 1e5; set { M.Alpha = value * 1e-5; Raise(); } }
    public double Rb { get => Mpa(M.Rb); set { M.Rb = value * U.MPa; Raise(); } }
    public double Rbt { get => Mpa(M.Rbt); set { M.Rbt = value * U.MPa; Raise(); } }
    public double Rbn { get => Mpa(M.Rbn); set { M.Rbn = value * U.MPa; Raise(); } }
    public double Rs { get => Mpa(M.Rs); set { M.Rs = value * U.MPa; Raise(); } }
    public double Rsc { get => Mpa(M.Rsc); set { M.Rsc = value * U.MPa; Raise(); } }
    public double Rsw { get => Mpa(M.Rsw); set { M.Rsw = value * U.MPa; Raise(); } }
    public double Fy { get => Mpa(M.Fy); set { M.Fy = value * U.MPa; Raise(); } }
    public double F { get => Mpa(M.F); set { M.F = value * U.MPa; Raise(); } }
    public double Fu { get => Mpa(M.Fu); set { M.Fu = value * U.MPa; Raise(); } }
    public string Used => MaterialsWindow.Usage(_p, M.Name);
}

/// <summary>Khai báo → Vật liệu… (Define → Material Properties): bảng bê tông / cốt thép / thép hình của dự án với γ, E, ν, α,
/// Rb, Rbt, Rb,n (TCVN 5574:2018), Rs, Rsc, Rsw, fy, f, fu; thêm từ thư viện, đặt lại theo thư viện, xoá vật liệu không dùng.</summary>
internal sealed class MaterialsWindow : Window
{
    private readonly Project _p;
    private readonly DataGrid _grid = DialogUi.NewGrid();
    private readonly TextBlock _status = new() { Foreground = Brushes.DarkSlateBlue, TextWrapping = TextWrapping.Wrap };
    public bool Changed { get; private set; }

    public MaterialsWindow(Project p)
    {
        _p = p;
        DialogUi.Setup(this, "Khai báo vật liệu (Material Properties)", 1180, 520);
        // mọi vật liệu đang được tham chiếu phải có trong danh sách (Project.Material tự thêm từ thư viện)
        foreach (var n in ReferencedNames(p)) { try { p.Material(n); } catch (KeyNotFoundException) { } }
        _grid.Columns.Add(DialogUi.Text("Tên", "Name", true, 80));
        _grid.Columns.Add(DialogUi.Text("Loại", "Kind", true, 72));
        _grid.Columns.Add(DialogUi.Num("γ (kN/m³)", "Gamma", "0.##", false, 66));
        _grid.Columns.Add(DialogUi.Num("E (MPa)", "E", "0", false, 72));
        _grid.Columns.Add(DialogUi.Num("ν", "Nu", "0.##", false, 44));
        _grid.Columns.Add(DialogUi.Num("α (×10⁻⁵/°C)", "Alpha", "0.##", false, 76));
        _grid.Columns.Add(DialogUi.Num("Rb (MPa)", "Rb", "0.##", false, 62));
        _grid.Columns.Add(DialogUi.Num("Rbt (MPa)", "Rbt", "0.###", false, 64));
        _grid.Columns.Add(DialogUi.Num("Rb,n (MPa)", "Rbn", "0.##", false, 68));
        _grid.Columns.Add(DialogUi.Num("Rs (MPa)", "Rs", "0", false, 60));
        _grid.Columns.Add(DialogUi.Num("Rsc (MPa)", "Rsc", "0", false, 64));
        _grid.Columns.Add(DialogUi.Num("Rsw (MPa)", "Rsw", "0", false, 64));
        _grid.Columns.Add(DialogUi.Num("fy (MPa)", "Fy", "0", false, 58));
        _grid.Columns.Add(DialogUi.Num("f (MPa)", "F", "0", false, 56));
        _grid.Columns.Add(DialogUi.Num("fu (MPa)", "Fu", "0", false, 58));
        _grid.Columns.Add(DialogUi.Text("Đang dùng", "Used", true, 160));
        _grid.CellEditEnding += (s, e) => { Changed = true; };

        var cbC = new ComboBox { Width = 70, ItemsSource = Model.Material.ConcreteClasses, SelectedIndex = 2, Margin = new Thickness(0, 0, 4, 0) };
        var cbR = new ComboBox { Width = 86, ItemsSource = Model.Material.RebarGrades, SelectedIndex = 2, Margin = new Thickness(0, 0, 4, 0) };
        var cbS = new ComboBox { Width = 80, ItemsSource = Model.Material.SteelGrades, SelectedIndex = 2, Margin = new Thickness(0, 0, 4, 0) };
        var bar = DialogUi.Row(
            DialogUi.Label("Bê tông"), cbC, DialogUi.Btn("＋", (s, e) => AddLib(cbC.SelectedItem as string)),
            DialogUi.Label("Cốt thép"), cbR, DialogUi.Btn("＋", (s, e) => AddLib(cbR.SelectedItem as string)),
            DialogUi.Label("Thép hình"), cbS, DialogUi.Btn("＋", (s, e) => AddLib(cbS.SelectedItem as string)),
            new Border { Width = 16 },
            DialogUi.Btn("Đặt lại theo thư viện", (s, e) => ResetSel()),
            DialogUi.Btn("Xoá (không dùng)", (s, e) => DeleteSel()),
            DialogUi.Btn("Đóng", (s, e) => Close(), true));
        var dock = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(bar, Dock.Top); dock.Children.Add(bar);
        var bottom = new StackPanel();
        bottom.Children.Add(DialogUi.Hint("Giá trị tính toán theo TCVN 5574:2018 (bê tông Bảng 7, 10; cốt thép Bảng 13) và TCVN 5575:2024 (thép hình f = fy/γM). "
                                          + "Sửa trực tiếp trong bảng – phân tích dùng E, ν, γ; thiết kế dùng cường độ. Cấp vật liệu cho cấu kiện chọn ở thẻ 'Công trình' và trong Tiết diện."));
        bottom.Children.Add(_status);
        DockPanel.SetDock(bottom, Dock.Bottom); dock.Children.Add(bottom);
        dock.Children.Add(_grid);
        Content = dock;
        Reload();
    }

    internal static IEnumerable<string> ReferencedNames(Project p)
    {
        var c = p.Concrete;
        return new[] { c.ConcreteColumn, c.ConcreteBeam, c.ConcreteSlab, c.ConcreteFooting, c.RebarMain, c.RebarStirrup }
            .Concat(p.FrameSections.Select(s => s.Material)).Concat(p.SlabSections.Select(s => s.Material))
            .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct();
    }

    internal static string Usage(Project p, string name)
    {
        var l = new List<string>();
        int fs = p.FrameSections.Count(s => s.Material == name), ss = p.SlabSections.Count(s => s.Material == name);
        if (fs > 0) l.Add($"{fs} tiết diện");
        if (ss > 0) l.Add($"{ss} TD sàn");
        var c = p.Concrete;
        if (c.ConcreteColumn == name) l.Add("cột");
        if (c.ConcreteBeam == name) l.Add("dầm");
        if (c.ConcreteSlab == name) l.Add("sàn");
        if (c.ConcreteFooting == name) l.Add("móng");
        if (c.RebarMain == name) l.Add("thép dọc");
        if (c.RebarStirrup == name) l.Add("thép đai");
        return string.Join(", ", l);
    }

    private void Reload()
    {
        DialogUi.Commit(_grid);
        var order = new[] { MaterialKind.Concrete, MaterialKind.Rebar, MaterialKind.Steel };
        _grid.ItemsSource = new ObservableCollection<MaterialRow>(_p.Materials.OrderBy(m => Array.IndexOf(order, m.Kind)).ThenBy(m => m.Name).Select(m => new MaterialRow(_p, m)));
        _p.InvalidateIndex();
    }

    private static Material Lib(string name)
    {
        if (Model.Material.ConcreteClasses.Contains(name)) return Model.Material.Concrete(name);
        if (Model.Material.RebarGrades.Contains(name)) return Model.Material.Rebar(name);
        if (Model.Material.SteelGrades.Contains(name)) return Model.Material.Steel(name);
        return null;
    }

    private void AddLib(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (_p.Materials.Any(m => m.Name == name)) { _status.Text = $"Vật liệu {name} đã có."; return; }
        _p.Materials.Add(Lib(name));
        Changed = true; Reload();
        _status.Text = $"Đã thêm {name} từ thư viện.";
    }

    private void ResetSel()
    {
        var rows = _grid.SelectedItems.OfType<MaterialRow>().ToList();
        if (rows.Count == 0) { _status.Text = "Chọn vật liệu cần đặt lại."; return; }
        int n = 0;
        foreach (var r in rows)
        {
            var lib = Lib(r.Name);
            if (lib == null) continue;
            int i = _p.Materials.IndexOf(r.M);
            if (i >= 0) { _p.Materials[i] = lib; n++; }
        }
        Changed = n > 0; Reload();
        _status.Text = $"Đã đặt lại {n} vật liệu theo thư viện TCVN.";
    }

    private void DeleteSel()
    {
        var rows = _grid.SelectedItems.OfType<MaterialRow>().ToList();
        var used = rows.Where(r => r.Used.Length > 0).Select(r => r.Name).ToList();
        foreach (var r in rows.Where(r => r.Used.Length == 0)) _p.Materials.Remove(r.M);
        Changed |= rows.Count > used.Count; Reload();
        _status.Text = used.Count > 0 ? $"Không xoá vật liệu đang dùng: {string.Join(", ", used)}." : "Đã xoá.";
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        DialogUi.Commit(_grid);
        base.OnClosing(e);
    }
}

/// <summary>Dòng bảng trường hợp tải (đổi tên qua LoadPatternEditor để cập nhật mọi tham chiếu).</summary>
internal sealed class PatternDefRow : RowBase
{
    private readonly LoadPatternsWindow _w;
    public LoadPattern Pat { get; }
    public PatternDefRow(LoadPatternsWindow w, LoadPattern pat) { _w = w; Pat = pat; }
    public string Name
    {
        get => Pat.Name;
        set { if (value != null && value.Trim() != Pat.Name) _w.Rename(Pat.Name, value.Trim()); Raise(); }
    }
    public LoadType Type { get => Pat.Type; set { Pat.Type = value; _w.Touch(); Raise(); } }
    public double SelfWeight { get => Pat.SelfWeight; set { Pat.SelfWeight = value; _w.Touch(); Raise(); } }
    public double Gamma { get => Pat.Gamma; set { Pat.Gamma = value; _w.Touch(); Raise(); } }
    public string Loads
    {
        get
        {
            var (f, a, n) = LoadPatternEditor.LoadCounts(_w.P, Pat.Name);
            return $"{f} thanh, {a} sàn, {n} nút";
        }
    }
    public string Auto => Pat.Type switch
    {
        LoadType.Wind => _w.P.Wind.Enabled ? "Gió tự sinh (TCVN 2737)" : "",
        LoadType.Seismic => _w.P.Seismic.Enabled ? "Động đất tự sinh (TCVN 9386)" : "",
        _ => Pat.SelfWeight != 0 ? "Trọng lượng bản thân" : ""
    };
}

/// <summary>Khai báo → Trường hợp tải (Define → Load Patterns): tên, loại, hệ số trọng lượng bản thân, hệ số vượt tải γf.</summary>
internal sealed class LoadPatternsWindow : Window
{
    internal Project P { get; }
    private readonly DataGrid _grid = DialogUi.NewGrid();
    private readonly TextBlock _status = new() { Foreground = Brushes.DarkSlateBlue, TextWrapping = TextWrapping.Wrap };
    public bool Changed { get; private set; }

    public LoadPatternsWindow(Project p)
    {
        P = p;
        DialogUi.Setup(this, "Trường hợp tải (Load Patterns)", 860, 460);
        _grid.Columns.Add(DialogUi.Text("Tên", "Name", false, 100));
        _grid.Columns.Add(DialogUi.Combo("Loại", "Type", Enum.GetValues(typeof(LoadType)), 100));
        _grid.Columns.Add(DialogUi.Num("Hệ số TLBT", "SelfWeight", "0.###", false, 80));
        _grid.Columns.Add(DialogUi.Num("γf (TTGH I)", "Gamma", "0.###", false, 76));
        _grid.Columns.Add(DialogUi.Text("Tải đã gán", "Loads", true, 170));
        _grid.Columns.Add(DialogUi.Text("Tự sinh", "Auto", true, 200));
        var tName = new TextBox { Width = 90, Text = "TH_MOI", Margin = new Thickness(0, 0, 4, 0) };
        var cbType = new ComboBox { Width = 100, ItemsSource = Enum.GetValues(typeof(LoadType)), SelectedItem = LoadType.Other, Margin = new Thickness(0, 0, 4, 0) };
        var bar = DialogUi.Row(
            DialogUi.Label("Tên"), tName, DialogUi.Label("Loại"), cbType,
            DialogUi.Btn("＋ Thêm", (s, e) =>
            {
                var t = cbType.SelectedItem is LoadType lt ? lt : LoadType.Other;
                var r = LoadPatternEditor.Add(P, ComboEditor.UniqueName(P, tName.Text), t, t == LoadType.Dead ? 1 : 0, t is LoadType.Wind or LoadType.Seismic ? 1.2 : 1.3);
                _status.Text = r.Message; if (r.Ok) { Changed = true; Reload(); }
            }, true),
            DialogUi.Btn("Xoá", (s, e) => DeleteSel()),
            new Border { Width = 16 },
            DialogUi.Btn("Đóng", (s, e) => Close()));
        var dock = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(bar, Dock.Top); dock.Children.Add(bar);
        var bottom = new StackPanel();
        bottom.Children.Add(DialogUi.Hint("Loại: Dead = tĩnh tải (TLBT hệ số 1), SuperDead = hoàn thiện/tường, Live/RoofLive = hoạt tải, Wind = gió, Seismic = động đất. "
                                          + "γf dùng khi tự sinh tổ hợp TCVN 2737:2023. Đổi tên cập nhật tải gán, tổ hợp, nguồn khối lượng. Xoá trường hợp xoá luôn tải gán và thành phần tổ hợp."));
        bottom.Children.Add(_status);
        DockPanel.SetDock(bottom, Dock.Bottom); dock.Children.Add(bottom);
        dock.Children.Add(_grid);
        Content = dock;
        Reload();
    }

    internal void Touch() => Changed = true;

    internal void Rename(string oldName, string newName)
    {
        var r = LoadPatternEditor.Rename(P, oldName, newName);
        _status.Text = r.Message;
        if (r.Ok) Changed = true;
    }

    private void Reload()
    {
        DialogUi.Commit(_grid);
        _grid.ItemsSource = new ObservableCollection<PatternDefRow>(P.Patterns.Select(x => new PatternDefRow(this, x)));
    }

    private void DeleteSel()
    {
        var rows = _grid.SelectedItems.OfType<PatternDefRow>().ToList();
        if (rows.Count == 0) return;
        if (MessageBox.Show(this, $"Xoá {rows.Count} trường hợp tải cùng tải gán và thành phần tổ hợp tham chiếu?", Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        foreach (var r in rows) _status.Text = LoadPatternEditor.Delete(P, r.Pat.Name).Message;
        Changed = true; Reload();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        DialogUi.Commit(_grid);
        base.OnClosing(e);
    }
}

/// <summary>Dòng bảng trường hợp phân tích.</summary>
internal sealed class LoadCaseRow
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Method { get; set; } = "";
    public string Loads { get; set; } = "";
    public string State { get; set; } = "";
}

/// <summary>Khai báo → Trường hợp phân tích (Define → Load Cases): tĩnh tuyến tính cho từng trường hợp tải, dao động riêng (modal),
/// phổ phản ứng (khi chọn phương pháp phổ). Chỉ xem – thiết lập ở trường hợp tải / nguồn khối lượng / phổ phản ứng.</summary>
internal sealed class LoadCasesWindow : Window
{
    public LoadCasesWindow(Project p, AnalysisResults res, Action openModal, Action openSpectrum)
    {
        DialogUi.Setup(this, "Trường hợp phân tích (Load Cases)", 900, 440);
        var g = DialogUi.NewGrid();
        g.IsReadOnly = true;
        g.Columns.Add(DialogUi.Text("Tên", "Name", true, 90));
        g.Columns.Add(DialogUi.Text("Loại phân tích", "Type", true, 170));
        g.Columns.Add(DialogUi.Text("Phương pháp", "Method", true, 230));
        g.Columns.Add(DialogUi.Text("Tải áp dụng", "Loads", true, 230));
        g.Columns.Add(DialogUi.Text("Trạng thái", "State", true, 110));
        var rows = new List<LoadCaseRow>();
        foreach (var pat in p.Patterns)
        {
            string loads = pat.SelfWeight != 0 ? $"{pat.SelfWeight:0.##} × TLBT" : "";
            var (f, a, n) = LoadPatternEditor.LoadCounts(p, pat.Name);
            loads += (loads.Length > 0 ? " + " : "") + $"{f} tải thanh, {a} tải sàn, {n} tải nút";
            string method = "Tuyến tính, độ cứng ban đầu";
            if (pat.Type == LoadType.Wind) method = p.Wind.Enabled ? "Gió tĩnh tự sinh theo tầng (TCVN 2737:2023)" : "Gió (tự sinh đang tắt)";
            if (pat.Type == LoadType.Seismic)
                method = !p.Seismic.Enabled ? "Động đất (đang tắt)" : p.Seismic.IsResponseSpectrum ? "Phổ phản ứng dạng dao động (" + (p.Modal?.Combination ?? "CQC") + ")" : "Tĩnh lực ngang tương đương (TCVN 9386)";
            rows.Add(new LoadCaseRow
            {
                Name = pat.Name, Type = pat.Type == LoadType.Seismic && p.Seismic.IsResponseSpectrum && p.Seismic.Enabled ? "Phổ phản ứng" : "Tĩnh tuyến tính",
                Method = method, Loads = loads, State = res == null ? "Chưa chạy" : res.Cases.ContainsKey(pat.Name) ? "Đã giải" : "Không có"
            });
        }
        var ms = p.Modal ?? new ModalSettings();
        rows.Add(new LoadCaseRow
        {
            Name = "MODAL", Type = "Dao động riêng (Modal)",
            Method = $"{ms.Solver}, {ms.RequestedModes(p.Stories.Count)} dạng" + (p.UseRigidDiaphragm ? ", sàn cứng" : ""),
            Loads = "Nguồn khối lượng: " + ms.Describe(p),
            State = !ms.Enabled ? "Tắt" : res?.Modal != null ? $"{res.Modal.Modes.Count} dạng, T1 = {res.Modal.First?.Period:0.000} s" : "Chưa chạy"
        });
        g.ItemsSource = rows;
        var bar = DialogUi.Row(
            DialogUi.Btn("Nguồn khối lượng & dao động…", (s, e) => openModal?.Invoke()),
            DialogUi.Btn("Phổ phản ứng / động đất…", (s, e) => openSpectrum?.Invoke()),
            new Border { Width = 16 },
            DialogUi.Btn("Đóng", (s, e) => Close(), true));
        var dock = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(bar, Dock.Bottom); dock.Children.Add(bar);
        var hint = DialogUi.Hint("Mỗi trường hợp tải (load pattern) được giải bằng một trường hợp phân tích tĩnh tuyến tính cùng tên (như ETABS). "
                                 + "Tổ hợp tải cộng tuyến tính kết quả các trường hợp. Phân tích dao động riêng chạy cùng khi bật.");
        DockPanel.SetDock(hint, Dock.Top); dock.Children.Add(hint);
        dock.Children.Add(g);
        Content = dock;
    }
}

/// <summary>Khai báo → Phổ phản ứng / động đất…: thông số TCVN 9386-1:2025 (agR, loại nền, γI, q, ψE, phương pháp) và đồ thị phổ thiết kế Sd(T).</summary>
internal sealed class SpectrumDialog : Window
{
    private readonly Project _p;
    private readonly SeismicSettings _s;
    private readonly CheckBox _chk = new() { Content = "Tính động đất (trường hợp DDX, DDY)", Margin = new Thickness(0, 2, 0, 4) };
    private readonly TextBox _ag = new() { Width = 70 }, _gi = new() { Width = 70 }, _q = new() { Width = 70 }, _psi = new() { Width = 70 };
    private readonly ComboBox _soil = new() { Width = 70, ItemsSource = new[] { "A", "B", "C", "D", "E" } };
    private readonly ComboBox _method = new() { Width = 240, ItemsSource = SeismicMethods.All };
    private readonly Canvas _plot = new() { Height = 230, Background = Brushes.White, ClipToBounds = true };
    private readonly TextBlock _info = new() { Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap };

    public SpectrumDialog(Project p)
    {
        _p = p;
        _s = new SeismicSettings
        {
            Enabled = p.Seismic.Enabled, AgR = p.Seismic.AgR, Soil = p.Seismic.Soil, ImportanceFactor = p.Seismic.ImportanceFactor,
            Q = p.Seismic.Q, PsiLive = p.Seismic.PsiLive, Method = p.Seismic.Method
        };
        DialogUi.Setup(this, "Phổ phản ứng / động đất (TCVN 9386-1:2025)", 640, 600);
        _chk.IsChecked = _s.Enabled;
        _ag.Text = DialogUi.F(_s.AgR, "0.####"); _gi.Text = DialogUi.F(_s.ImportanceFactor); _q.Text = DialogUi.F(_s.Q); _psi.Text = DialogUi.F(_s.PsiLive);
        _soil.SelectedItem = _s.Soil; if (_soil.SelectedIndex < 0) _soil.SelectedIndex = 2;
        _method.SelectedItem = _s.Method; if (_method.SelectedIndex < 0) _method.SelectedIndex = 0;
        foreach (var t in new[] { _ag, _gi, _q, _psi }) t.TextChanged += (s, e) => Draw();
        _soil.SelectionChanged += (s, e) => Draw();
        _plot.SizeChanged += (s, e) => Draw();
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(_chk);
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Gia tốc nền agR (g)", 170), _ag, DialogUi.Label("   Loại nền", 80), _soil));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Hệ số tầm quan trọng γI", 170), _gi, DialogUi.Label("   Hệ số ứng xử q", 110), _q));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Hệ số tổ hợp hoạt tải ψE", 170), _psi));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Phương pháp", 170), _method));
        sp.Children.Add(new Border { BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), Margin = new Thickness(0, 8, 0, 4), Child = _plot });
        sp.Children.Add(_info);
        var ok = DialogUi.Btn("Đồng ý", (s, e) => { Apply(); DialogResult = true; }, true); ok.IsDefault = true;
        var cancel = DialogUi.Btn("Huỷ", (s, e) => DialogResult = false); cancel.IsCancel = true;
        var bs = DialogUi.Row(ok, cancel); bs.HorizontalAlignment = HorizontalAlignment.Right;
        sp.Children.Add(bs);
        Content = sp;
        Loaded += (s, e) => Draw();
    }

    private void Read()
    {
        _s.AgR = DialogUi.ParseNum(_ag.Text, _s.AgR);
        _s.ImportanceFactor = DialogUi.ParseNum(_gi.Text, _s.ImportanceFactor);
        _s.Q = Math.Max(1.0, DialogUi.ParseNum(_q.Text, _s.Q));
        _s.PsiLive = DialogUi.ParseNum(_psi.Text, _s.PsiLive);
        _s.Soil = _soil.SelectedItem as string ?? "C";
        _s.Method = _method.SelectedItem as string ?? SeismicMethods.Static;
        _s.Enabled = _chk.IsChecked == true;
    }

    private void Apply()
    {
        Read();
        var t = _p.Seismic;
        t.Enabled = _s.Enabled; t.AgR = _s.AgR; t.Soil = _s.Soil; t.ImportanceFactor = _s.ImportanceFactor; t.Q = _s.Q; t.PsiLive = _s.PsiLive; t.Method = _s.Method;
        if (t.Enabled)
        {
            if (_p.Pattern("DDX") == null) _p.Patterns.Add(new LoadPattern { Name = "DDX", Type = LoadType.Seismic, Gamma = 1.0 });
            if (_p.Pattern("DDY") == null) _p.Patterns.Add(new LoadPattern { Name = "DDY", Type = LoadType.Seismic, Gamma = 1.0 });
        }
    }

    private void Draw()
    {
        if (!IsLoaded) return;
        Read();
        _plot.Children.Clear();
        double W = _plot.ActualWidth, H = _plot.ActualHeight;
        if (W < 50 || H < 50) return;
        const double l = 46, r = 12, t = 10, b = 28, Tmax = 4.0;
        int n = 200;
        var pts = new PointCollection();
        double ymax = 1e-6;
        var vals = new double[n + 1];
        for (int i = 0; i <= n; i++) { vals[i] = LateralLoadGenerator.Sd(Tmax * i / n, _s); ymax = Math.Max(ymax, vals[i]); }
        ymax *= 1.15;
        Point P(double T, double v) => new(l + (W - l - r) * T / Tmax, H - b - (H - t - b) * v / ymax);
        for (int i = 0; i <= n; i++) pts.Add(P(Tmax * i / n, vals[i]));
        _plot.Children.Add(new Shapes.Line { X1 = l, Y1 = H - b, X2 = W - r, Y2 = H - b, Stroke = Brushes.Gray });
        _plot.Children.Add(new Shapes.Line { X1 = l, Y1 = t, X2 = l, Y2 = H - b, Stroke = Brushes.Gray });
        for (int k = 0; k <= 4; k++)
        {
            var q = P(k, 0);
            Txt(_plot, $"{k}", q.X - 4, H - b + 4);
            _plot.Children.Add(new Shapes.Line { X1 = q.X, Y1 = t, X2 = q.X, Y2 = H - b, Stroke = new SolidColorBrush(Color.FromRgb(235, 235, 235)) });
        }
        for (int k = 0; k <= 4; k++)
        {
            double v = ymax / 1.15 * k / 4;
            var q = P(0, v);
            Txt(_plot, DialogUi.F(v, "0.###"), 2, q.Y - 7);
        }
        Txt(_plot, "T (s)", W - r - 30, H - b - 16);
        Txt(_plot, "Sd/g", l + 4, t);
        _plot.Children.Add(new Shapes.Polyline { Points = pts, Stroke = new SolidColorBrush(Color.FromRgb(200, 30, 50)), StrokeThickness = 2 });
        double plateau = LateralLoadGenerator.Sd(0.4, _s);
        _info.Text = $"Phổ thiết kế loại 1 (TCVN 9386 mục 3.2.2.5): ag = γI·agR = {_s.ImportanceFactor * _s.AgR:0.####} g; nền {_s.Soil}; q = {_s.Q:0.##}; "
                     + $"Sd (đoạn nằm ngang) ≈ {plateau:0.####} g; cận dưới β·ag = {0.2 * _s.ImportanceFactor * _s.AgR:0.####} g. Phương pháp: {_s.Method}.";
    }

    private static void Txt(Canvas c, string s, double x, double y)
    {
        var tb = new TextBlock { Text = s, FontSize = 10, Foreground = Brushes.DimGray };
        Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y);
        c.Children.Add(tb);
    }
}
