using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Import;
using KetCauVXT.Loads;
using KetCauVXT.Model;
// Các module viết song song – dùng bí danh để tránh trùng tên với kiểu WPF
using ConcreteDesigner = KetCauVXT.Design.Concrete.ConcreteDesigner;
using SteelDesigner = KetCauVXT.Design.Steel.SteelDesigner;
using DrawingSet = KetCauVXT.Drawing.DrawingSet;
using CalculationReport = KetCauVXT.Report.CalculationReport;
using ExcelReport = KetCauVXT.Report.ExcelReport;
using ForceTables = KetCauVXT.Report.ForceTables;
using E2k = KetCauVXT.Export.E2kExporter;
using S2k = KetCauVXT.Export.S2kExporter;

namespace KetCauVXT.App;

public partial class MainWindow : Window
{
    private const string AppTitle = "Kết cấu VXT";
    private const string ProjectFilter = "Dự án Kết cấu VXT (*.kcvxt)|*.kcvxt|Tất cả (*.*)|*.*";

    private Project _p;
    private AnalysisResults _res;
    private DesignSummary _design;
    private string _path;
    private FrameElement _sel;
    private bool _busy, _dirty, _loadingUi;

    public MainWindow()
    {
        InitializeComponent();
    }

    // ==================================================================
    // KHỞI ĐỘNG
    // ==================================================================
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        cbTerrain.ItemsSource = new[] { "A", "B", "C" };
        cbSeisSoil.ItemsSource = new[] { "A", "B", "C", "D", "E" };
        InitLocationUi();
        foreach (var cb in new[] { cbConcCol, cbConcBeam, cbConcSlab, cbConcFoot }) cb.ItemsSource = Model.Material.ConcreteClasses;
        foreach (var cb in new[] { cbRebarMain, cbRebarStir }) cb.ItemsSource = Model.Material.RebarGrades;

        txtSpansX.Text = "3.6 4.2 3.6";
        txtSpansY.Text = "4.5 4.5";
        txtHeights.Text = "3.6 3.3 3.3 3.3";
        txtBaseElev.Text = "-1.0";
        txtFinish.Text = "1.2"; txtLive.Text = "1.5"; txtRoofLive.Text = "0.75"; txtWall.Text = "0";

        SetProject(ModelBuilder.NewProject(), null);
        SetStatus("Sẵn sàng – nhập lưới trục, chiều cao tầng rồi bấm 'Tạo mô hình', hoặc mở/nhập mô hình có sẵn.");
        HandleCommandLine();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_busy)
        {
            MessageBox.Show("Chương trình đang tính toán, vui lòng chờ.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            e.Cancel = true; return;
        }
        if (!ConfirmDiscard()) e.Cancel = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_busy) return;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (ctrl && e.Key == Key.N) { MnuNew_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (ctrl && e.Key == Key.O) { MnuOpen_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (ctrl && e.Key == Key.S) { MnuSave_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.F5) { MnuRun_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.F1) { ShowAppendix(); e.Handled = true; }   // MainWindow.Help.cs
        else if (HandlePlanEditKey(e.Key, ctrl)) e.Handled = true; // Delete / Esc / Ctrl+Z – MainWindow.Edit.cs
    }

    // ==================================================================
    // DỰ ÁN
    // ==================================================================
    private void SetProject(Project p, string path)
    {
        _p = p;
        _path = path;
        _res = null; _design = null; _sel = null;
        _dirty = false;
        _p.InvalidateIndex();
        ActivateCadStandard();   // chuẩn layer CAD của dự án (MainWindow.CadStandard.cs)
        LoadUiFromProject();
        RefreshAll(true);
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        string f = _path != null ? Path.GetFileName(_path) : "(chưa lưu)";
        Title = $"{AppTitle} – {_p?.Name} – {f}{(_dirty ? " *" : "")}";
        txtFile.Text = _path ?? "";
    }

    private void MarkDirty(bool invalidateResults = true)
    {
        _dirty = true;
        if (invalidateResults && _res != null)
        {
            _res = null; _design = null;
            RefreshResults(); RefreshDesign();
            SetStatus("Mô hình đã thay đổi – cần chạy lại phân tích.");
        }
        UpdateTitle();
    }

    private bool ConfirmDiscard()
    {
        if (!_dirty) return true;
        var r = MessageBox.Show("Dự án có thay đổi chưa lưu. Lưu trước khi tiếp tục?", AppTitle,
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Cancel) return false;
        if (r == MessageBoxResult.Yes) return SaveProject(false);
        return true;
    }

    private bool SaveProject(bool saveAs)
    {
        try
        {
            ApplyUiToProject(false);
            string path = _path;
            if (saveAs || string.IsNullOrEmpty(path))
            {
                var dlg = new SaveFileDialog { Filter = ProjectFilter, FileName = SafeName(_p.Name) + ".kcvxt", Title = "Lưu dự án" };
                if (dlg.ShowDialog(this) != true) return false;
                path = dlg.FileName;
            }
            _p.Save(path);
            _path = path; _dirty = false;
            UpdateTitle();
            SetStatus("Đã lưu " + path);
            return true;
        }
        catch (Exception ex) { ShowError("Không lưu được dự án", ex); return false; }
    }

    private static string SafeName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "CongTrinh";
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Trim();
    }

    private void MnuNew_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !ConfirmDiscard()) return;
        SetProject(ModelBuilder.NewProject(), null);
        SetStatus("Dự án mới.");
    }

    private void MnuOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = ProjectFilter, Title = "Mở dự án Kết cấu VXT" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            SetProject(Project.Load(dlg.FileName), dlg.FileName);
            SetStatus($"Đã mở {dlg.FileName}: {_p.Nodes.Count} nút, {_p.Frames.Count} thanh, {_p.Slabs.Count} ô sàn.");
        }
        catch (Exception ex) { ShowError("Không mở được file dự án", ex); }
    }

    private void MnuSave_Click(object sender, RoutedEventArgs e) { if (!_busy) SaveProject(false); }
    private void MnuSaveAs_Click(object sender, RoutedEventArgs e) { if (!_busy) SaveProject(true); }
    private void MnuExit_Click(object sender, RoutedEventArgs e) => Close();

    private void MnuAbout_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("Kết cấu VXT\nPhân tích, thiết kế kết cấu nhà dân dụng BTCT (≤ 7 tầng) và khung thép tiền chế.\n" +
                        "Tiêu chuẩn: TCVN 2737:2023, TCVN 5574:2018, TCVN 5575, TCVN 9386:2012, TCVN 9362:2012.\n\n" +
                        "Đơn vị: kN, m. Kích thước tiết diện nhập bằng mm.\n\nTác giả: Vũ Xuân Tuyến",
            AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);

    // ==================================================================
    // NHẬP
    // ==================================================================
    private void MnuImportDxf_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var dlg = new OpenFileDialog { Filter = "Bản vẽ DXF (*.dxf)|*.dxf", Title = "Chọn mặt bằng kết cấu DXF" };
        if (dlg.ShowDialog(this) != true) return;
        ApplyUiToProject(false);
        var opt = new DxfImportOptions
        {
            DefaultColumnSection = cbColSec.SelectedItem as string ?? "C22x22",
            DefaultBeamSection = cbBeamXSec.SelectedItem as string ?? "D22x30",
            SlabSection = cbSlabSec.SelectedItem as string ?? "S100",
            Concrete = _p.Concrete.ConcreteBeam,
            FinishLoad = Num(txtFinish, 1.2), LiveLoad = Num(txtLive, 1.5), RoofLiveLoad = Num(txtRoofLive, 0.75),
            FixedBase = chkFixedBase.IsChecked == true
        };
        if (_p.Stories.Count == 0)
        {
            try { ModelBuilder.SetStories(_p, ParseList(txtHeights.Text), Num(txtBaseElev, 0)); }
            catch (Exception) { /* để bộ nhập tự tạo 1 tầng */ }
        }
        var od = new DxfImportDialog(opt, _p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name).ToList()) { Owner = this };
        if (od.ShowDialog() != true) return;
        try
        {
            var rep = DxfImporter.Import(_p, dlg.FileName, opt);
            _res = null; _design = null; _sel = null;
            MarkDirty();
            LoadUiFromProject();
            RefreshAll(true);
            SetStatus($"Đã nhập DXF: {rep.Columns} cột, {rep.Beams} dầm, {rep.Slabs} ô sàn.");
            MessageBox.Show(rep.ToString(), "Kết quả nhập DXF", MessageBoxButton.OK,
                rep.Warnings.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError("Lỗi nhập DXF", ex); }
    }

    private void MnuImportRevit_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var dlg = new OpenFileDialog
        {
            Filter = "File trao đổi Revit (*.vxtrevit.json;*.json)|*.vxtrevit.json;*.json|Tất cả (*.*)|*.*",
            Title = "Chọn file JSON xuất từ add-in Revit"
        };
        if (dlg.ShowDialog(this) != true) return;
        var r = MessageBox.Show("Thay thế toàn bộ mô hình hiện tại bằng mô hình từ Revit?\n(Giữ lại vật liệu, tiết diện, trường hợp tải, thông số gió/động đất.)",
            AppTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (r != MessageBoxResult.OK) return;
        try
        {
            ApplyUiToProject(false);
            var opt = new RevitImportOptions
            {
                Concrete = _p.Concrete.ConcreteBeam, FinishLoad = Num(txtFinish, 1.2), LiveLoad = Num(txtLive, 1.5),
                RoofLiveLoad = Num(txtRoofLive, 0.75), FixedBase = chkFixedBase.IsChecked == true
            };
            var rep = RevitJsonImporter.Import(_p, dlg.FileName, opt);
            _res = null; _design = null; _sel = null;
            MarkDirty();
            LoadUiFromProject();
            RefreshAll(true);
            SetStatus($"Đã nhập từ Revit: {rep.Columns} cột, {rep.Beams} dầm, {rep.Slabs} ô sàn.");
            MessageBox.Show(rep.ToString(), "Kết quả nhập từ Revit", MessageBoxButton.OK,
                rep.Warnings.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError("Lỗi nhập file Revit", ex); }
    }

    // ==================================================================
    // TAB CÔNG TRÌNH ↔ PROJECT
    // ==================================================================
    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private void LoadUiFromProject()
    {
        _loadingUi = true;
        try
        {
            txtName.Text = _p.Name; txtLocation.Text = _p.Location; txtOwner.Text = _p.Owner; txtDesigner.Text = _p.Designer;
            var gx = _p.GridsX.Select(g => g.Coord).ToList();
            var gy = _p.GridsY.Select(g => g.Coord).ToList();
            if (gx.Count > 1) txtSpansX.Text = string.Join(" ", gx.Zip(gx.Skip(1), (a, b) => F(b - a)));
            if (gy.Count > 1) txtSpansY.Text = string.Join(" ", gy.Zip(gy.Skip(1), (a, b) => F(b - a)));
            var st = _p.Stories.OrderBy(s => s.Elevation).ToList();
            if (st.Count > 0)
            {
                var hs = new List<double>();
                double prev = _p.BaseElevation;
                foreach (var s in st) { hs.Add(s.Elevation - prev); prev = s.Elevation; }
                txtHeights.Text = string.Join(" ", hs.Select(v => F(v)));
                txtBaseElev.Text = F(_p.BaseElevation);
            }

            chkWind.IsChecked = _p.Wind.Enabled; txtW0.Text = F(_p.Wind.W0); cbTerrain.SelectedItem = _p.Wind.Terrain; txtCp.Text = F(_p.Wind.Cp);
            chkSeismic.IsChecked = _p.Seismic.Enabled; txtAgR.Text = F(_p.Seismic.AgR); cbSeisSoil.SelectedItem = _p.Seismic.Soil; txtQ.Text = F(_p.Seismic.Q);
            LoadLocationUi();
            cbConcCol.SelectedItem = _p.Concrete.ConcreteColumn; cbConcBeam.SelectedItem = _p.Concrete.ConcreteBeam;
            cbConcSlab.SelectedItem = _p.Concrete.ConcreteSlab; cbConcFoot.SelectedItem = _p.Concrete.ConcreteFooting;
            cbRebarMain.SelectedItem = _p.Concrete.RebarMain; cbRebarStir.SelectedItem = _p.Concrete.RebarStirrup;
            txtPhi.Text = F(_p.Soil.Phi); txtC.Text = F(_p.Soil.C); txtGamma.Text = F(_p.Soil.Gamma);
            txtDepth.Text = F(_p.Soil.Depth); txtROverride.Text = F(_p.Soil.ROverride);
            RefreshSectionCombos();
            LoadGeoUi();   // tab Địa chất – MainWindow.Geotech.cs
        }
        finally { _loadingUi = false; }
    }

    private void RefreshSectionCombos()
    {
        var rect = _p.FrameSections.Where(s => s.Shape == SectionShape.RectConcrete).Select(s => s.Name).ToList();
        void Set(ComboBox cb, List<string> items, string prefer)
        {
            string cur = cb.SelectedItem as string;
            cb.ItemsSource = items;
            cb.SelectedItem = cur != null && items.Contains(cur) ? cur : items.FirstOrDefault(i => i == prefer) ?? items.FirstOrDefault();
        }
        Set(cbColSec, rect, rect.FirstOrDefault(n => n.StartsWith("C")) ?? "C22x22");
        Set(cbBeamXSec, rect, "D22x30");
        Set(cbBeamYSec, rect, "D22x30");
        Set(cbSlabSec, _p.SlabSections.Select(s => s.Name).ToList(), "S100");
    }

    /// <summary>Ghi thông số trên tab Công trình vào dự án (không đổi hình học).</summary>
    private void ApplyUiToProject(bool showStatus)
    {
        if (_p == null || _loadingUi) return;
        _p.Name = txtName.Text.Trim(); _p.Location = txtLocation.Text.Trim(); _p.Owner = txtOwner.Text.Trim(); _p.Designer = txtDesigner.Text.Trim();
        _p.Wind.Enabled = chkWind.IsChecked == true;
        _p.Wind.W0 = Num(txtW0, _p.Wind.W0);
        _p.Wind.Terrain = cbTerrain.SelectedItem as string ?? "B";
        ApplyLocationUi();
        _p.Wind.Cp = Num(txtCp, _p.Wind.Cp);
        _p.Seismic.Enabled = chkSeismic.IsChecked == true;
        _p.Seismic.AgR = Num(txtAgR, _p.Seismic.AgR);
        _p.Seismic.Soil = cbSeisSoil.SelectedItem as string ?? "C";
        _p.Seismic.Q = Num(txtQ, _p.Seismic.Q);
        if (_p.Seismic.Enabled)
        {
            foreach (var n in new[] { "DDX", "DDY" })
                if (_p.Pattern(n) == null) _p.Patterns.Add(new LoadPattern { Name = n, Type = LoadType.Seismic, Gamma = 1.0 });
        }
        _p.Concrete.ConcreteColumn = cbConcCol.SelectedItem as string ?? "B25";
        _p.Concrete.ConcreteBeam = cbConcBeam.SelectedItem as string ?? "B25";
        _p.Concrete.ConcreteSlab = cbConcSlab.SelectedItem as string ?? "B25";
        _p.Concrete.ConcreteFooting = cbConcFoot.SelectedItem as string ?? "B25";
        _p.Concrete.RebarMain = cbRebarMain.SelectedItem as string ?? "CB400-V";
        _p.Concrete.RebarStirrup = cbRebarStir.SelectedItem as string ?? "CB240-T";
        _p.Soil.Phi = Num(txtPhi, _p.Soil.Phi); _p.Soil.C = Num(txtC, _p.Soil.C); _p.Soil.Gamma = Num(txtGamma, _p.Soil.Gamma);
        _p.Soil.Depth = Num(txtDepth, _p.Soil.Depth); _p.Soil.ROverride = Num(txtROverride, _p.Soil.ROverride);
        // vật liệu bê tông của tiết diện theo cấu kiện
        foreach (var f in _p.Frames.Where(f_ => f_.IsConcrete))
        {
            var s = _p.FrameSections.FirstOrDefault(q => q.Name == f.Section);
            if (s != null && s.Shape == SectionShape.RectConcrete)
                s.Material = f.Kind == FrameKind.Column ? _p.Concrete.ConcreteColumn : _p.Concrete.ConcreteBeam;
        }
        foreach (var s in _p.SlabSections) s.Material = _p.Concrete.ConcreteSlab;
        foreach (var m in new[] { _p.Concrete.ConcreteColumn, _p.Concrete.ConcreteBeam, _p.Concrete.ConcreteSlab, _p.Concrete.ConcreteFooting, _p.Concrete.RebarMain, _p.Concrete.RebarStirrup })
            _p.Material(m); // tự thêm từ thư viện nếu chưa có
        _p.InvalidateIndex();
        UpdateTitle();
        if (showStatus) SetStatus("Đã áp dụng thông số công trình.");
    }

    private static double Num(TextBox tb, double def)
    {
        var s = (tb.Text ?? "").Trim().Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : def;
    }

    /// <summary>Đọc danh sách số "3.6 4.2 3.6", "3.6;4.2", "3*3.3" (= 3.3 3.3 3.3).</summary>
    public static List<double> ParseList(string text)
    {
        var res = new List<double>();
        if (string.IsNullOrWhiteSpace(text)) return res;
        foreach (var tok in text.Split(new[] { ' ', ';', '\t', '\n', '\r', '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = tok.Trim().Replace(',', '.');
            int star = t.IndexOfAny(new[] { '*', 'x', 'X' });
            int count = 1;
            if (star > 0)
            {
                if (!int.TryParse(t.Substring(0, star), NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 1)
                    throw new FormatException($"Không hiểu '{tok}'");
                t = t.Substring(star + 1);
            }
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || v <= 0)
                throw new FormatException($"Giá trị không hợp lệ: '{tok}' (cần số dương, đơn vị m)");
            for (int i = 0; i < count; i++) res.Add(v);
        }
        return res;
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        ApplyUiToProject(true);
        MarkDirty();
        RefreshLoads();
    }

    private void BtnBuild_Click(object sender, RoutedEventArgs e) => BuildFromGridUi(true);
    private void MnuBuildGrid_Click(object sender, RoutedEventArgs e) => BuildFromGridUi(true);

    private void BuildFromGridUi(bool confirm)
    {
        if (_busy) return;
        try
        {
            var sx = ParseList(txtSpansX.Text);
            var sy = ParseList(txtSpansY.Text);
            var hs = ParseList(txtHeights.Text);
            if (hs.Count == 0) throw new FormatException("Chưa nhập chiều cao tầng.");
            if (hs.Count > 7) MessageBox.Show("Chương trình hướng tới nhà ≤ 7 tầng; kết quả với nhà cao hơn cần kiểm tra thêm.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            if (confirm && _p.Frames.Count > 0 &&
                MessageBox.Show("Xoá mô hình hiện tại và tạo lại khung BTCT từ lưới trục?", AppTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;
            ApplyUiToProject(false);
            ImportGeometry.ClearModel(_p);
            ModelBuilder.SetGrid(_p, sx, sy);
            ModelBuilder.SetStories(_p, hs, Num(txtBaseElev, 0));
            var o = new ModelBuilder.GridFrameOptions
            {
                ColumnSection = cbColSec.SelectedItem as string ?? "C22x22",
                BeamSectionX = cbBeamXSec.SelectedItem as string ?? "D22x30",
                BeamSectionY = cbBeamYSec.SelectedItem as string ?? "D22x30",
                SlabSection = cbSlabSec.SelectedItem as string ?? "S100",
                FinishLoad = Num(txtFinish, 1.2), LiveLoad = Num(txtLive, 1.5), RoofLiveLoad = Num(txtRoofLive, 0.75),
                PerimeterWallLoad = Num(txtWall, 0), FixedBase = chkFixedBase.IsChecked == true
            };
            ImportGeometry.EnsurePatterns(_p);
            ModelBuilder.BuildFromGrid(_p, o);
            _res = null; _design = null; _sel = null;
            MarkDirty();
            RefreshAll(true);
            SetStatus($"Đã tạo khung: {_p.Nodes.Count} nút, {_p.Frames.Count} thanh, {_p.Slabs.Count} ô sàn, {_p.Combinations.Count} tổ hợp.");
        }
        catch (Exception ex) { ShowError("Không tạo được mô hình", ex); }
    }

    private void MnuBuildPortal_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var dlg = new PortalFrameDialog { Owner = this };
        if (dlg.ShowDialog() != true) return;
        try
        {
            bool replace = _p.Frames.Count == 0 ||
                           MessageBox.Show("Xoá mô hình hiện có trước khi tạo khung tiền chế?\n(Chọn 'No' để thêm vào mô hình hiện có.)",
                               AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            if (replace) { ImportGeometry.ClearModel(_p); _p.Grids.Clear(); _p.Stories.Clear(); }
            var o = dlg.Options;
            foreach (var s in dlg.Sections)
            {
                _p.FrameSections.RemoveAll(q => q.Name == s.Name);
                _p.FrameSections.Add(s);
            }
            _p.InvalidateIndex();
            ModelBuilder.BuildPortalFrames(_p, o);
            if (_p.Grids.Count == 0)
            {
                _p.Grids.Add(new GridLine { Name = "A", Dir = GridDir.X, Coord = 0 });
                _p.Grids.Add(new GridLine { Name = "B", Dir = GridDir.X, Coord = o.Span });
                for (int k = 0; k <= o.Bays; k++)
                    _p.Grids.Add(new GridLine { Name = (k + 1).ToString(CultureInfo.InvariantCulture), Dir = GridDir.Y, Coord = k * o.BaySpacing });
            }
            _res = null; _design = null; _sel = null;
            MarkDirty();
            LoadUiFromProject();
            RefreshAll(true);
            SetStatus($"Đã tạo {o.Bays + 1} khung tiền chế nhịp {o.Span:0.##} m.");
        }
        catch (Exception ex) { ShowError("Không tạo được khung tiền chế", ex); }
    }

    private void MnuSplit_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        try
        {
            int n = ModelBuilder.SplitFramesAtNodes(_p);
            MarkDirty();
            RefreshAll(false);
            SetStatus($"Đã chia {n} thanh tại nút trung gian.");
        }
        catch (Exception ex) { ShowError("Lỗi chia thanh", ex); }
    }

    // ==================================================================
    // PHÂN TÍCH / THIẾT KẾ (chạy nền)
    // ==================================================================
    private void MnuCombos_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        ApplyUiToProject(false);
        ComboGenerator.Generate(_p);
        MarkDirty();
        RefreshLoads();
        RefreshCaseCombos();
        SetStatus($"Đã tạo {_p.Combinations.Count} tổ hợp tải trọng.");
    }

    private async Task<bool> RunBusy(string title, Action<IProgress<string>> work)
    {
        if (_busy) return false;
        _busy = true;
        SetBusyUi(true);
        var prog = new Progress<string>(s => SetStatus(s));
        try
        {
            await Task.Run(() => work(prog));
            return true;
        }
        catch (Exception ex)
        {
            ShowError(title, ex);
            return false;
        }
        finally
        {
            _busy = false;
            SetBusyUi(false);
        }
    }

    private void SetBusyUi(bool busy)
    {
        pbBusy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        tabLeft.IsEnabled = !busy;
        Cursor = busy ? Cursors.Wait : null;
    }

    private async Task<bool> RunAnalysis()
    {
        ApplyUiToProject(false);
        if (_p.Combinations.Count == 0) ComboGenerator.Generate(_p);
        AnalysisResults res = null;
        bool ok = await RunBusy("Lỗi phân tích", prog => { res = FrameAnalysis.Run(_p, prog); });
        if (!ok || res == null) return false;
        _res = res; _design = null;
        _dirty = true; UpdateTitle();
        RefreshLoads();
        RefreshCaseCombos();
        RefreshResults();
        RefreshDesign();
        ShowSelection();
        SetStatus($"Phân tích xong: {res.Equations} phương trình, {res.Cases.Count} trường hợp tải, {res.Warnings.Count} cảnh báo.");
        return true;
    }

    private async void MnuRun_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        await RunAnalysis();
    }

    private async Task<bool> EnsureAnalysis()
    {
        if (_res != null) return true;
        return await RunAnalysis();
    }

    private async void MnuDesignConcrete_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !await EnsureAnalysis()) return;
        if (!_p.Frames.Any(f => f.IsConcrete))
        {
            MessageBox.Show("Mô hình không có cấu kiện BTCT.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var res = _res;
        // giữ kết quả kiểm tra thép (nếu có), thiết kế lại toàn bộ BTCT
        var start = new DesignSummary();
        if (_design != null) start.Steel.AddRange(_design.Steel);
        DesignSummary ds = null;
        bool ok = await RunBusy("Lỗi thiết kế BTCT", prog =>
        {
            prog.Report("Thiết kế dầm, cột, sàn, móng BTCT...");
            ds = ConcreteDesigner.Design(_p, res, start);
        });
        if (!ok || ds == null) return;
        _design = ds;
        RefreshDesign();
        int bad = ds.All.Count(m => !m.Ok);
        SetStatus($"Thiết kế BTCT xong: {ds.Beams.Count} dầm, {ds.Columns.Count} cột, {ds.Slabs.Count} ô sàn, {ds.Footings.Count} móng – {bad} cấu kiện không đạt.");
    }

    private async void MnuCheckSteel_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !await EnsureAnalysis()) return;
        if (!_p.Frames.Any(f => !f.IsConcrete))
        {
            MessageBox.Show("Mô hình không có cấu kiện thép.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var res = _res;
        var ds = _design ?? new DesignSummary();
        bool ok = await RunBusy("Lỗi kiểm tra thép", prog =>
        {
            prog.Report("Kiểm tra cấu kiện thép...");
            ds.Steel.Clear();
            SteelDesigner.Check(_p, res, ds);
        });
        if (!ok) return;
        _design = ds;
        RefreshDesign();
        SetStatus($"Kiểm tra thép xong: {ds.Steel.Count} cấu kiện, {ds.Steel.Count(s => !s.Ok)} không đạt.");
    }

    /// <summary>Thiết kế toàn bộ (BTCT + thép) nếu chưa có.</summary>
    private async Task<bool> EnsureDesign()
    {
        if (!await EnsureAnalysis()) return false;
        if (_design != null) return true;
        var res = _res;
        DesignSummary ds = null;
        bool ok = await RunBusy("Lỗi thiết kế", prog =>
        {
            ds = new DesignSummary();
            if (_p.Frames.Any(f => f.IsConcrete)) { prog.Report("Thiết kế BTCT..."); ds = ConcreteDesigner.Design(_p, res, ds); }
            if (_p.Frames.Any(f => !f.IsConcrete)) { prog.Report("Kiểm tra thép..."); SteelDesigner.Check(_p, res, ds); }
        });
        if (!ok || ds == null) return false;
        _design = ds;
        RefreshDesign();
        return true;
    }

    // ==================================================================
    // XUẤT
    // ==================================================================
    private string AskSave(string filter, string defName, string title)
    {
        var dlg = new SaveFileDialog { Filter = filter, FileName = defName, Title = title };
        if (_path != null) dlg.InitialDirectory = Path.GetDirectoryName(_path);
        return dlg.ShowDialog(this) == true ? dlg.FileName : null;
    }

    private string AskFolder(string title)
    {
        var dlg = new OpenFolderDialog { Title = title };
        if (_path != null) dlg.InitialDirectory = Path.GetDirectoryName(_path);
        return dlg.ShowDialog(this) == true ? dlg.FolderName : null;
    }

    private async void MnuExpDxf_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !await EnsureDesign()) return;
        string folder = AskFolder("Chọn thư mục lưu bộ bản vẽ DXF");
        if (folder == null) return;
        using var layerScope = BeginDxfExport(KetCauVXT.Drawing.ExportLayerProbe.ForDrawingSet()); // xem/đổi tên layer trước khi xuất
        if (layerScope == null) return;
        List<string> files = null;
        var ds = _design;
        if (await RunBusy("Lỗi xuất bản vẽ", prog => { prog.Report("Đang vẽ..."); files = DrawingSet.ExportAll(_p, ds, folder); }))
            Done($"Đã xuất {files?.Count ?? 0} bản vẽ DXF vào\n{folder}");
    }

    private async void MnuExpWord_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !await EnsureDesign()) return;
        string path = AskSave("Word (*.docx)|*.docx", "ThuyetMinh.docx", "Lưu thuyết minh tính toán");
        if (path == null) return;
        var res = _res; var ds = _design;
        if (await RunBusy("Lỗi xuất thuyết minh", prog => { prog.Report("Đang lập thuyết minh..."); CalculationReport.Export(_p, res, ds, path); }))
            Done("Đã xuất thuyết minh:\n" + path);
    }

    private async void MnuExpExcel_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !await EnsureDesign()) return;
        string path = AskSave("Excel (*.xlsx)|*.xlsx", "BangTinh.xlsx", "Lưu bảng tính");
        if (path == null) return;
        var res = _res; var ds = _design;
        if (await RunBusy("Lỗi xuất bảng tính", prog => { prog.Report("Đang lập bảng tính..."); ExcelReport.Export(_p, res, ds, path); }))
            Done("Đã xuất bảng tính:\n" + path);
    }

    private async void MnuExpCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !await EnsureAnalysis()) return;
        string folder = AskFolder("Chọn thư mục lưu bảng nội lực CSV");
        if (folder == null) return;
        var res = _res;
        if (await RunBusy("Lỗi xuất nội lực", prog => { prog.Report("Đang ghi CSV..."); ForceTables.WriteCsv(res, folder); }))
            Done("Đã xuất bảng nội lực CSV vào\n" + folder);
    }

    private async void MnuExpE2k_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        string path = AskSave("ETABS text (*.e2k)|*.e2k", "MoHinh.e2k", "Xuất mô hình ETABS");
        if (path == null) return;
        ApplyUiToProject(false);
        if (await RunBusy("Lỗi xuất ETABS", prog => E2k.Export(_p, path))) Done("Đã xuất mô hình ETABS:\n" + path);
    }

    private async void MnuExpS2k_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        string path = AskSave("SAP2000 text (*.s2k)|*.s2k", "MoHinh.s2k", "Xuất mô hình SAP2000");
        if (path == null) return;
        ApplyUiToProject(false);
        if (await RunBusy("Lỗi xuất SAP2000", prog => S2k.Export(_p, path))) Done("Đã xuất mô hình SAP2000:\n" + path);
    }

    private async void MnuExpAll_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        string root = AskFolder("Chọn thư mục để xuất toàn bộ hồ sơ");
        if (root == null) return;
        if (!await EnsureDesign()) return;
        using var layerScope = BeginDxfExport(KetCauVXT.Drawing.ExportLayerProbe.ForDrawingSet(), "Xuất toàn bộ hồ sơ"); // xem/đổi tên layer trước khi xuất
        if (layerScope == null) return;
        string folder = Path.Combine(root, SafeName(_p.Name) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture));
        var res = _res; var ds = _design;
        var errors = new List<string>();
        var done = new List<string>();
        bool ok = await RunBusy("Lỗi xuất hồ sơ", prog =>
        {
            Directory.CreateDirectory(folder);
            void Step(string name, Action a)
            {
                prog.Report("Đang xuất " + name + "...");
                try { a(); done.Add(name); }
                catch (Exception ex) { errors.Add($"{name}: {ex.Message}"); }
            }
            Step("mô hình .kcvxt", () => _p.Save(Path.Combine(folder, "MoHinh.kcvxt")));
            Step("bản vẽ DXF", () =>
            {
                string d = Path.Combine(folder, "BanVe");
                Directory.CreateDirectory(d);
                DrawingSet.ExportAll(_p, ds, d);
            });
            Step("gia công thép (shop drawing)", () => ExportRebarShop(ds, Path.Combine(folder, "GiaCongThep")));   // MainWindow.Rebar.cs
            Step("thuyết minh Word", () => CalculationReport.Export(_p, res, ds, Path.Combine(folder, "ThuyetMinh.docx")));
            Step("bảng tính Excel", () => ExcelReport.Export(_p, res, ds, Path.Combine(folder, "BangTinh.xlsx")));
            Step("nội lực CSV", () =>
            {
                string d = Path.Combine(folder, "NoiLuc");
                Directory.CreateDirectory(d);
                ForceTables.WriteCsv(res, d);
            });
            Step("mô hình ETABS", () => E2k.Export(_p, Path.Combine(folder, "MoHinh.e2k")));
            Step("mô hình SAP2000", () => S2k.Export(_p, Path.Combine(folder, "MoHinh.s2k")));
        });
        if (!ok) return;
        string msg = $"Đã xuất vào thư mục:\n{folder}\n\nHoàn thành: {string.Join(", ", done)}";
        if (errors.Count > 0)
        {
            msg += "\n\nLỗi:\n- " + string.Join("\n- ", errors);
            MessageBox.Show(msg, AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            SetStatus($"Xuất hồ sơ xong với {errors.Count} lỗi.");
        }
        else Done(msg);
    }

    private void Done(string msg)
    {
        SetStatus(msg.Replace("\n", " "));
        MessageBox.Show(msg, AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ==================================================================
    // LÀM MỚI GIAO DIỆN
    // ==================================================================
    private void SetStatus(string s) => txtStatus.Text = s;

    private void ShowError(string title, Exception ex)
    {
        var inner = ex;
        while (inner is AggregateException ag && ag.InnerException != null) inner = ag.InnerException;
        while (inner is System.Reflection.TargetInvocationException ti && ti.InnerException != null) inner = ti.InnerException;
        SetStatus(title + ": " + inner.Message);
        MessageBox.Show(this, title + ":\n\n" + inner.Message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void RefreshAll(bool fit)
    {
        RefreshStories();
        RefreshSections();
        RefreshLoads();
        RefreshCaseCombos();
        RefreshResults();
        RefreshDesign();
        if (fit) FitPlan(); else RedrawPlan();
        Build3D();
        ShowSelection();
    }

    private void RefreshStories()
    {
        var names = new List<string> { AllStories };
        names.AddRange(_p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name));
        string cur = cbStory.SelectedItem as string;
        cbStory.ItemsSource = names;
        cbStory.SelectedItem = cur != null && names.Contains(cur) ? cur : (names.Count > 1 ? names[1] : names[0]);
        string cr = cbResStory.SelectedItem as string;
        cbResStory.ItemsSource = names;
        cbResStory.SelectedItem = cr != null && names.Contains(cr) ? cr : names[0];
    }

    private void RefreshSections()
    {
        dgSections.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<SectionRow>(_p.FrameSections.Select(s => new SectionRow(_p, s)));
        dgSlabSections.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<SlabSectionRow>(_p.SlabSections.Select(s => new SlabSectionRow(_p, s)));
        RefreshSectionCombos();
    }

    private void RefreshLoads()
    {
        dgPatterns.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<PatternRow>(_p.Patterns.Select(x => new PatternRow(_p, x)));
        dgCombos.ItemsSource = _p.Combinations.Select(c => ComboRow.From(c)).ToList();
        bool auto = chkShowAutoLoads.IsChecked == true;
        dgFrameLoads.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<FrameLoadRow>(
            _p.FrameLoads.Where(l => auto || l.Source == "").Select(l => new FrameLoadRow(_p, l)));
        dgAreaLoads.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<AreaLoadRow>(_p.AreaLoads.Select(l => new AreaLoadRow(_p, l)));
    }

    private void RefreshCaseCombos()
    {
        var names = _p.Patterns.Select(x => x.Name).Concat(_p.Combinations.Select(c => c.Name)).ToList();
        string cur = cbCase.SelectedItem as string;
        cbCase.ItemsSource = names;
        cbCase.SelectedItem = cur != null && names.Contains(cur) ? cur : names.FirstOrDefault(n => n == "BAO") ?? names.FirstOrDefault();
        string cd = cbDiagramCase.SelectedItem as string;
        cbDiagramCase.ItemsSource = names;
        cbDiagramCase.SelectedItem = cd != null && names.Contains(cd) ? cd : names.FirstOrDefault(n => n == "BAO") ?? names.FirstOrDefault();
    }

    private void RefreshResults()
    {
        RedrawSecondaryViews(); // biểu đồ nội lực, móng, 3D (vẽ trễ, gộp lần gọi)
        lstWarnings.ItemsSource = null;
        dgForces.ItemsSource = null;
        dgReactions.ItemsSource = null;
        if (_res == null) { lstWarnings.ItemsSource = new[] { "Chưa có kết quả phân tích." }; return; }
        var warn = new List<string>(_res.Warnings);
        foreach (var lf in _res.LateralForces)
            warn.Add($"Tải ngang {lf.Pattern} tầng {lf.Story} (+{lf.Elevation:0.00}): Fx = {lf.Fx:0.00} kN, Fy = {lf.Fy:0.00} kN – {lf.Note}");
        if (warn.Count == 0) warn.Add("Không có cảnh báo.");
        lstWarnings.ItemsSource = warn;

        string c = cbCase.SelectedItem as string;
        if (string.IsNullOrEmpty(c)) return;
        string st = cbResStory.SelectedItem as string ?? AllStories;
        try
        {
            var rows = new List<ForceRow>();
            var reac = new List<ReactionRow>();
            var combo = _p.Combinations.FirstOrDefault(x => x.Name == c);
            bool env = combo != null && combo.Type == ComboType.Envelope;
            var frames = _p.Frames.Where(f => st == AllStories || f.Story == st).ToList();
            if (env)
            {
                var list = _res.ExpandCombos(c);
                foreach (var f in frames)
                {
                    var (mx, mn) = _res.Envelope(f.Id, list);
                    if (mx == null) continue;
                    for (int i = 0; i < mx.Count; i++)
                    {
                        rows.Add(ForceRow.Make(f, "Max", mx.Stations[i], mx.F[i]));
                        rows.Add(ForceRow.Make(f, "Min", mn.Stations[i], mn.F[i]));
                    }
                }
                foreach (var n in _p.Nodes.Where(q => q.IsSupport))
                {
                    double[] vmax = null, vmin = null;
                    foreach (var name in list)
                    {
                        if (!_res.Get(name).Reactions.TryGetValue(n.Id, out var v)) continue;
                        if (vmax == null) { vmax = (double[])v.Clone(); vmin = (double[])v.Clone(); continue; }
                        for (int k = 0; k < 6; k++) { vmax[k] = Math.Max(vmax[k], v[k]); vmin[k] = Math.Min(vmin[k], v[k]); }
                    }
                    if (vmax != null) { reac.Add(ReactionRow.Make(n, "Max", vmax)); reac.Add(ReactionRow.Make(n, "Min", vmin)); }
                }
            }
            else
            {
                var r = _res.Get(c);
                foreach (var f in frames)
                {
                    if (!r.Frames.TryGetValue(f.Id, out var ff)) continue;
                    for (int i = 0; i < ff.Count; i++) rows.Add(ForceRow.Make(f, c, ff.Stations[i], ff.F[i]));
                }
                foreach (var n in _p.Nodes.Where(q => q.IsSupport))
                    if (r.Reactions.TryGetValue(n.Id, out var v)) reac.Add(ReactionRow.Make(n, c, v));
            }
            dgForces.ItemsSource = rows;
            dgReactions.ItemsSource = reac;
        }
        catch (Exception ex) { SetStatus("Không hiển thị được kết quả: " + ex.Message); }
    }

    private void RefreshDesign()
    {
        RedrawSecondaryViews(); // móng 3D + mô hình móng (vẽ trễ, gộp lần gọi)
        if (_design == null)
        {
            dgBeams.ItemsSource = null; dgColumns.ItemsSource = null; dgSlabs.ItemsSource = null;
            dgFootings.ItemsSource = null; dgSteel.ItemsSource = null;
            return;
        }
        dgBeams.ItemsSource = _design.Beams.SelectMany(b => BeamDesignRow.From(b)).ToList();
        dgColumns.ItemsSource = _design.Columns.SelectMany(c => ColumnDesignRow.From(c)).ToList();
        dgSlabs.ItemsSource = _design.Slabs.Select(s => SlabDesignRow.From(s)).ToList();
        dgFootings.ItemsSource = _design.Footings.Select(f => FootingDesignRow.From(f)).ToList();
        dgSteel.ItemsSource = _design.Steel.Select(s => SteelCheckRow.From(s)).ToList();
    }

    // ==================================================================
    // SỰ KIỆN LƯỚI DỮ LIỆU
    // ==================================================================
    private void Grid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.PropertyDescriptor is PropertyDescriptor pd)
        {
            if (!pd.IsBrowsable) { e.Cancel = true; return; }
            if (!string.IsNullOrEmpty(pd.DisplayName)) e.Column.Header = pd.DisplayName;
        }
        if (e.PropertyType == typeof(double) && e.Column is DataGridTextColumn tc && tc.Binding is System.Windows.Data.Binding b)
            b.StringFormat = "0.###";
    }

    private void Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        // chờ binding ghi xong rồi mới vẽ lại
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _p.InvalidateIndex();
            MarkDirty();
            if (ReferenceEquals(sender, dgSections) || ReferenceEquals(sender, dgSlabSections)) { RefreshSectionCombos(); Build3D(); }
            RedrawPlan();
        }));
    }

    private void BtnAddRect_Click(object sender, RoutedEventArgs e)
    {
        string name = UniqueSection("D22x45");
        _p.FrameSections.Add(FrameSection.Rect(name, 220, 450, _p.Concrete.ConcreteBeam));
        _p.InvalidateIndex(); MarkDirty(); RefreshSections();
    }

    private void BtnAddI_Click(object sender, RoutedEventArgs e)
    {
        string name = UniqueSection("I400x200");
        _p.FrameSections.Add(FrameSection.WeldedISection(name, 400, 400, 200, 10, 6));
        _p.InvalidateIndex(); MarkDirty(); RefreshSections();
    }

    private string UniqueSection(string baseName)
    {
        string n = baseName; int k = 2;
        while (_p.FrameSections.Any(s => s.Name == n)) n = baseName + "_" + k++;
        return n;
    }

    private void BtnDelSection_Click(object sender, RoutedEventArgs e)
    {
        if (dgSections.SelectedItem is not SectionRow r) return;
        int used = _p.Frames.Count(f => f.Section == r.Sec.Name);
        if (used > 0) { MessageBox.Show($"Tiết diện {r.Sec.Name} đang được {used} thanh sử dụng – không xoá được.", AppTitle); return; }
        _p.FrameSections.Remove(r.Sec);
        _p.InvalidateIndex(); MarkDirty(); RefreshSections();
    }

    private void BtnAddSlabSec_Click(object sender, RoutedEventArgs e)
    {
        string n = "S150"; int k = 2;
        while (_p.SlabSections.Any(s => s.Name == n)) n = "S150_" + k++;
        _p.SlabSections.Add(new SlabSection { Name = n, Thickness = 0.15, Material = _p.Concrete.ConcreteSlab });
        MarkDirty(); RefreshSections();
    }

    private void BtnDelSlabSec_Click(object sender, RoutedEventArgs e)
    {
        if (dgSlabSections.SelectedItem is not SlabSectionRow r) return;
        int used = _p.Slabs.Count(s => s.Section == r.Sec.Name);
        if (used > 0) { MessageBox.Show($"Tiết diện sàn {r.Sec.Name} đang được {used} ô sàn sử dụng – không xoá được.", AppTitle); return; }
        _p.SlabSections.Remove(r.Sec);
        MarkDirty(); RefreshSections();
    }

    private void BtnAddPattern_Click(object sender, RoutedEventArgs e)
    {
        string n = "TH_MOI"; int k = 2;
        while (_p.Pattern(n) != null) n = "TH_MOI" + k++;
        _p.Patterns.Add(new LoadPattern { Name = n, Type = LoadType.Other, Gamma = 1.2 });
        MarkDirty(); RefreshLoads(); RefreshCaseCombos();
    }

    private void BtnDelPattern_Click(object sender, RoutedEventArgs e)
    {
        if (dgPatterns.SelectedItem is not PatternRow r) return;
        string n = r.Pat.Name;
        int used = _p.FrameLoads.Count(l => l.Pattern == n && l.Source == "") + _p.AreaLoads.Count(l => l.Pattern == n) + _p.NodeLoads.Count(l => l.Pattern == n && l.Source == "");
        if (MessageBox.Show($"Xoá trường hợp tải {n}" + (used > 0 ? $" và {used} tải đã gán" : "") + "? Tổ hợp tải sẽ được tạo lại.",
                AppTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        _p.Patterns.Remove(r.Pat);
        _p.FrameLoads.RemoveAll(l => l.Pattern == n);
        _p.AreaLoads.RemoveAll(l => l.Pattern == n);
        _p.NodeLoads.RemoveAll(l => l.Pattern == n);
        ComboGenerator.Generate(_p);
        MarkDirty(); RefreshLoads(); RefreshCaseCombos();
    }

    private void BtnAddFrameLoad_Click(object sender, RoutedEventArgs e)
    {
        if (_sel == null) { MessageBox.Show("Chọn một thanh trên mặt bằng trước.", AppTitle); return; }
        string pat = _p.Pattern("HTHT") != null ? "HTHT" : _p.Patterns.FirstOrDefault()?.Name ?? "TT";
        _p.FrameLoads.Add(FrameLoad.Uniform(_sel.Id, pat, 0, LoadDir.Gravity, "Tường / tải nhập tay"));
        MarkDirty(); RefreshLoads();
        SetStatus($"Đã thêm tải cho thanh {_sel.Label} – sửa giá trị trong bảng.");
    }

    private void BtnDelFrameLoad_Click(object sender, RoutedEventArgs e)
    {
        var rows = dgFrameLoads.SelectedItems.OfType<FrameLoadRow>().ToList();
        if (rows.Count == 0) return;
        foreach (var r in rows) _p.FrameLoads.Remove(r.Load);
        MarkDirty(); RefreshLoads();
    }

    private void ChkShowAutoLoads_Changed(object sender, RoutedEventArgs e) { if (IsLoaded) RefreshLoads(); }

    private void BtnAddAreaLoad_Click(object sender, RoutedEventArgs e)
    {
        var slabs = PlanSlabs().ToList();
        if (slabs.Count == 0) { MessageBox.Show("Tầng đang xem không có ô sàn.", AppTitle); return; }
        string pat = _p.Pattern("HTHT") != null ? "HTHT" : _p.Patterns.FirstOrDefault()?.Name ?? "TT";
        foreach (var s in slabs) _p.AreaLoads.Add(new AreaLoad { Slab = s.Id, Pattern = pat, Q = 0, Note = "Tải bổ sung" });
        MarkDirty(); RefreshLoads();
        SetStatus($"Đã thêm {slabs.Count} dòng tải sàn – sửa giá trị trong bảng.");
    }

    private void BtnDelAreaLoad_Click(object sender, RoutedEventArgs e)
    {
        var rows = dgAreaLoads.SelectedItems.OfType<AreaLoadRow>().ToList();
        if (rows.Count == 0) return;
        foreach (var r in rows) _p.AreaLoads.Remove(r.Load);
        MarkDirty(); RefreshLoads();
    }

    // ==================================================================
    // CHỌN TẦNG / TRƯỜNG HỢP
    // ==================================================================
    private void CbStory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _p == null) return;
        RedrawPlan();
    }

    private void CbCase_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _p == null) return;
        RefreshResults();
    }

    private void CbResStory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _p == null) return;
        RefreshResults();
    }
}
