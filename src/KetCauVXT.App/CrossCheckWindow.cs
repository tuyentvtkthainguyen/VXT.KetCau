using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using KetCauVXT.Analysis;
using KetCauVXT.Export;
using KetCauVXT.Import.Csi;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Cửa sổ "So sánh với ETABS/SAP/SAFE": chọn file bảng kết quả xuất từ ETABS/SAP2000/SAFE, xem bảng nhận diện (đơn vị), ghép trường hợp/tổ hợp
/// ngoài ↔ của chương trình (tự theo tên, sửa được), chạy so sánh với kết quả phân tích của chương trình (KHÔNG thay kết quả hiện hành),
/// bảng cấu kiện tô màu (xanh ≤ 5 %, cam ≤ 10 %, đỏ &gt; 10 %), lọc theo tầng/loại, nhấp đúp → biểu đồ M3 hai phía, xuất Excel + Word,
/// và xuất nhanh mô hình sang ETABS (.e2k) / SAP2000 (.s2k).</summary>
internal sealed class CrossCheckWindow : Window
{
    // ------------------------------------------------------------------ dòng bảng
    internal sealed class TableRow
    {
        public ExtTable T { get; init; }
        public string File => System.IO.Path.GetFileName(T.File);
        public string Name => T.Name;
        public string Kind => ResultImportDialog.KindText(T.Kind);
        public int Rows => T.Rows.Count;
        public string Units => T.UnitsText();
    }

    internal sealed class PairRow : RowBase
    {
        public CrossCasePair P { get; init; }
        public string External => P.External;
        public bool Enabled
        {
            get => P.Enabled;
            set { P.Enabled = value; Raise(); }
        }
        public string Ours
        {
            get => string.IsNullOrEmpty(P.Ours) ? NoCase : P.Ours;
            set
            {
                string v = value == NoCase ? "" : value ?? "";
                if (v == P.Ours) return;
                P.Ours = v; P.Method = "người dùng chọn"; P.Enabled = v.Length > 0;
                RaiseAll();
            }
        }
        public string Method => P.Method;
    }

    internal sealed class MemberRow
    {
        public CrossMember M { get; init; }
        public int Id => M.FrameId;
        public string Label => M.Label;
        public string Story => M.Story;
        public string Kind => M.Kind;
        public string External => M.ExternalName;
        public string Worst => double.IsNaN(M.WorstPercent) ? "—" : M.WorstPercent.ToString("0.00", Inv);
        public string WorstCase => M.WorstCase;
        public string Comp => M.WorstComponent;
        public string M3Ours => M.CaseCount > 0 ? M.OurM3.ToString("0.00", Inv) : "";
        public string M3Ext => M.CaseCount > 0 ? M.ExtM3.ToString("0.00", Inv) : "";
        public string M3Pct => M.CaseCount > 0 ? M.M3Percent.ToString("0.00", Inv) : "";
        public string V2Pct => M.CaseCount > 0 ? M.V2Percent.ToString("0.00", Inv) : "";
        public string PPct => M.CaseCount > 0 ? M.PPercent.ToString("0.00", Inv) : "";
        public string Level => CrossCheckReport.LevelText(M.Level);
    }

    internal sealed class OtherRow
    {
        public string Group { get; init; } = "";
        public string Item { get; init; } = "";
        public string Case { get; init; } = "";
        public string Ours { get; init; } = "";
        public string Ext { get; init; } = "";
        public string Pct { get; init; } = "";
        public CrossLevel Lv { get; init; }
        public string Level => CrossCheckReport.LevelText(Lv);
    }

    private const string NoCase = "(không so sánh)";
    private const string AllItems = "(tất cả)";
    private static readonly string[] Forces = { "kN", "Tonf", "kgf", "N", "kip" };
    private static readonly string[] Lengths = { "m", "cm", "mm", "ft", "in" };
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    internal static readonly Brush BrOk = Frozen(Color.FromRgb(0xC6, 0xEF, 0xCE));
    internal static readonly Brush BrWarn = Frozen(Color.FromRgb(0xFF, 0xD8, 0xA8));
    internal static readonly Brush BrFail = Frozen(Color.FromRgb(0xFF, 0xC7, 0xCE));
    internal static readonly Brush BrNone = Frozen(Color.FromRgb(0xEE, 0xEE, 0xEE));

    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    internal static Brush LevelBrush(CrossLevel l) => l switch
    {
        CrossLevel.Ok => BrOk,
        CrossLevel.Warn => BrWarn,
        CrossLevel.Fail => BrFail,
        _ => BrNone
    };

    private readonly Project _p;
    private readonly AnalysisResults _ours;
    private CrossCheckSession _ses;
    private CrossCheckReport _rep;

    private readonly TabControl _tabs = new() { Margin = new Thickness(0, 6, 0, 6) };
    private readonly TextBlock _files = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 2, 0, 2) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) };
    private readonly ComboBox _cbForce = new() { Width = 70, ItemsSource = Forces, SelectedIndex = 0 };
    private readonly ComboBox _cbLength = new() { Width = 60, ItemsSource = Lengths, SelectedIndex = 0 };
    private readonly TextBox _txtWarn = new() { Width = 45, Text = "5" };
    private readonly TextBox _txtFail = new() { Width = 45, Text = "10" };
    private readonly DataGrid _gTables = DialogUi.NewGrid();
    private readonly DataGrid _gPairs = DialogUi.NewGrid();
    private readonly DataGrid _gMembers = DialogUi.NewGrid();
    private readonly DataGrid _gOther = DialogUi.NewGrid();
    private readonly ComboBox _cbStory = new() { Width = 130 };
    private readonly ComboBox _cbKind = new() { Width = 110 };
    private readonly CheckBox _chkOnlyBad = new() { Content = "Chỉ hiện cấu kiện vượt ngưỡng cảnh báo", Margin = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox _cbPlotCase = new() { Width = 220 };
    private readonly ComboBox _cbPlotComp = new() { Width = 60, ItemsSource = new[] { "M3", "M2", "V2", "V3", "T", "P" }, SelectedIndex = 0 };
    private readonly Canvas _canvas = new() { Background = Brushes.White, ClipToBounds = true, MinHeight = 200 };
    private readonly TextBlock _plotNote = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6), FontSize = 13 };
    private readonly Button _btnRun, _btnExport;
    private int _plotId = -1;

    public CrossCheckWindow(Project p, AnalysisResults ours)
    {
        _p = p; _ours = ours;
        _ses = new CrossCheckSession(p, ours);
        DialogUi.Setup(this, "So sánh kết quả với ETABS / SAP2000 / SAFE", 1280, 800);
        Help.Attach(this, "phan-tich.so-sanh");   // nút "?" + F1 – Help.cs

        // ---- đầu ----
        _btnRun = DialogUi.Btn("So sánh", (s, e) => RunCompare(), true);
        var top = new StackPanel();
        top.Children.Add(DialogUi.Row(
            DialogUi.Btn("Xuất mô hình sang ETABS (.e2k)…", (s, e) => ExportModel(true)),
            DialogUi.Btn("SAP2000 (.s2k)…", (s, e) => ExportModel(false)),
            DialogUi.Label("  →  chạy phân tích trong ETABS/SAP, xuất bảng kết quả, rồi:"),
            DialogUi.Btn("Thêm file kết quả…", (s, e) => PickFiles(), true),
            DialogUi.Btn("Xoá danh sách", (s, e) => ResetSession())));
        top.Children.Add(DialogUi.Row(
            DialogUi.Label("Đơn vị khi bảng không ghi đơn vị:"), _cbForce, _cbLength,
            DialogUi.Label("   Ngưỡng cảnh báo (%):"), _txtWarn, DialogUi.Label(" không đạt (%):"), _txtFail,
            DialogUi.Label("  "), _btnRun));
        top.Children.Add(_files);
        top.Children.Add(DialogUi.Hint("ETABS/SAP2000: Display → Show Tables → Analysis Results: Element Forces - Beams/Columns (SAP: Frames), Joint Reactions, Base Reactions, "
                                       + "Joint Displacements, Story Drifts, Modal Periods → File → Export to Excel (.xlsx) hoặc CSV. Đặt cùng tên trường hợp tải/tổ hợp như khi xuất .e2k. "
                                       + "Kết quả của chương trình KHÔNG bị thay thế."));
        _cbForce.SelectionChanged += (s, e) => Reanalyze();
        _cbLength.SelectionChanged += (s, e) => Reanalyze();

        // ---- tab bảng ----
        _gTables.IsReadOnly = true;
        _gTables.Columns.Add(DialogUi.Text("File", nameof(TableRow.File), true, 180));
        _gTables.Columns.Add(DialogUi.Text("Bảng", nameof(TableRow.Name), true, 300));
        _gTables.Columns.Add(DialogUi.Text("Nhận diện", nameof(TableRow.Kind), true, 220));
        _gTables.Columns.Add(DialogUi.Text("Số dòng", nameof(TableRow.Rows), true, 70));
        _gTables.Columns.Add(DialogUi.Text("Đơn vị", nameof(TableRow.Units), true, 260));
        _tabs.Items.Add(new TabItem { Header = "Bảng dữ liệu", Content = _gTables });

        // ---- tab ghép trường hợp ----
        _gPairs.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "So sánh", Width = 60,
            Binding = new Binding(nameof(PairRow.Enabled)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }
        });
        _gPairs.Columns.Add(DialogUi.Text("Trường hợp / tổ hợp trong file ngoài", nameof(PairRow.External), true, 260));
        _gPairs.Columns.Add(DialogUi.Combo("Trường hợp / tổ hợp của Việt Const", nameof(PairRow.Ours), OurChoices(), 260));
        _gPairs.Columns.Add(DialogUi.Text("Cách ghép", nameof(PairRow.Method), true, 300));
        var pairPanel = new DockPanel();
        var pairHint = DialogUi.Hint("Tự ghép theo tên (trùng tên → không phân biệt hoa thường/dấu). \"Tên (Max)\"/\"Tên (Min)\" của tổ hợp bao ETABS so với Max/Min của tổ hợp bao cùng tên. "
                                     + "Chọn \"" + NoCase + "\" hoặc bỏ dấu để không so sánh.");
        DockPanel.SetDock(pairHint, Dock.Top);
        pairPanel.Children.Add(pairHint);
        pairPanel.Children.Add(_gPairs);
        _tabs.Items.Add(new TabItem { Header = "Ghép trường hợp", Content = pairPanel });

        // ---- tab cấu kiện ----
        _gMembers.IsReadOnly = true;
        _gMembers.CanUserSortColumns = true;
        _gMembers.SelectionMode = DataGridSelectionMode.Single;
        _gMembers.Columns.Add(DialogUi.Text("Id", nameof(MemberRow.Id), true, 50));
        _gMembers.Columns.Add(DialogUi.Text("Cấu kiện", nameof(MemberRow.Label), true, 80));
        _gMembers.Columns.Add(DialogUi.Text("Tầng", nameof(MemberRow.Story), true, 70));
        _gMembers.Columns.Add(DialogUi.Text("Loại", nameof(MemberRow.Kind), true, 60));
        _gMembers.Columns.Add(DialogUi.Text("Tên ngoài", nameof(MemberRow.External), true, 90));
        _gMembers.Columns.Add(DialogUi.Text("Lớn nhất %", nameof(MemberRow.Worst), true, 75));
        _gMembers.Columns.Add(DialogUi.Text("TP", nameof(MemberRow.Comp), true, 35));
        _gMembers.Columns.Add(DialogUi.Text("Trường hợp", nameof(MemberRow.WorstCase), true, 110));
        _gMembers.Columns.Add(DialogUi.Text("|M3| VXT", nameof(MemberRow.M3Ours), true, 70));
        _gMembers.Columns.Add(DialogUi.Text("|M3| ngoài", nameof(MemberRow.M3Ext), true, 70));
        _gMembers.Columns.Add(DialogUi.Text("ΔM3 %", nameof(MemberRow.M3Pct), true, 60));
        _gMembers.Columns.Add(DialogUi.Text("ΔV2 %", nameof(MemberRow.V2Pct), true, 60));
        _gMembers.Columns.Add(DialogUi.Text("ΔP %", nameof(MemberRow.PPct), true, 60));
        _gMembers.Columns.Add(DialogUi.Text("Đánh giá", nameof(MemberRow.Level), true, 90));
        _gMembers.LoadingRow += (s, e) => { if (e.Row.Item is MemberRow m) e.Row.Background = LevelBrush(m.M.Level); };
        _gMembers.MouseDoubleClick += (s, e) => { if (_gMembers.SelectedItem is MemberRow m) ShowMember(m.Id); };
        _gMembers.SelectionChanged += (s, e) => { if (_gMembers.SelectedItem is MemberRow m && m.Id != _plotId) ShowMember(m.Id); };
        _cbStory.SelectionChanged += (s, e) => FillMembers();
        _cbKind.SelectionChanged += (s, e) => FillMembers();
        _chkOnlyBad.Click += (s, e) => FillMembers();
        _cbPlotCase.SelectionChanged += (s, e) => Plot();
        _cbPlotComp.SelectionChanged += (s, e) => Plot();
        _canvas.SizeChanged += (s, e) => Plot();

        var filter = DialogUi.Row(DialogUi.Label("Tầng:"), _cbStory, DialogUi.Label("  Loại:"), _cbKind, _chkOnlyBad,
            DialogUi.Label("Màu: xanh ≤ ngưỡng cảnh báo, cam cảnh báo, đỏ vượt ngưỡng, xám thiếu số liệu. Nhấp đúp một dòng → biểu đồ."));
        var plotTop = DialogUi.Row(DialogUi.Label("Biểu đồ:"), _cbPlotComp, DialogUi.Label(" Trường hợp:"), _cbPlotCase);
        var plotPanel = new DockPanel { Margin = new Thickness(6, 0, 0, 0) };
        DockPanel.SetDock(plotTop, Dock.Top);
        plotPanel.Children.Add(plotTop);
        DockPanel.SetDock(_plotNote, Dock.Bottom);
        plotPanel.Children.Add(_plotNote);
        plotPanel.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Child = _canvas, Margin = new Thickness(0, 4, 0, 0) });
        var split = new Grid();
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        Grid.SetColumn(_gMembers, 0);
        var gs = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Color.FromRgb(224, 224, 224)) };
        Grid.SetColumn(gs, 1);
        Grid.SetColumn(plotPanel, 2);
        split.Children.Add(_gMembers);
        split.Children.Add(gs);
        split.Children.Add(plotPanel);
        var memPanel = new DockPanel();
        DockPanel.SetDock(filter, Dock.Top);
        memPanel.Children.Add(filter);
        memPanel.Children.Add(split);
        _tabs.Items.Add(new TabItem { Header = "Cấu kiện", Content = memPanel });

        // ---- tab phản lực, chuyển vị ----
        _gOther.IsReadOnly = true;
        _gOther.CanUserSortColumns = true;
        _gOther.Columns.Add(DialogUi.Text("Nhóm", nameof(OtherRow.Group), true, 140));
        _gOther.Columns.Add(DialogUi.Text("Mục", nameof(OtherRow.Item), true, 220));
        _gOther.Columns.Add(DialogUi.Text("Trường hợp", nameof(OtherRow.Case), true, 160));
        _gOther.Columns.Add(DialogUi.Text("VXT", nameof(OtherRow.Ours), true, 220));
        _gOther.Columns.Add(DialogUi.Text("Ngoài", nameof(OtherRow.Ext), true, 220));
        _gOther.Columns.Add(DialogUi.Text("Sai lệch %", nameof(OtherRow.Pct), true, 80));
        _gOther.Columns.Add(DialogUi.Text("Đánh giá", nameof(OtherRow.Level), true, 100));
        _gOther.LoadingRow += (s, e) => { if (e.Row.Item is OtherRow o) e.Row.Background = LevelBrush(o.Lv); };
        _tabs.Items.Add(new TabItem { Header = "Phản lực, chuyển vị, dao động", Content = _gOther });

        // ---- tab tổng hợp ----
        _tabs.Items.Add(new TabItem { Header = "Tổng hợp & nguyên nhân", Content = new ScrollViewer { Content = _summary, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });

        // ---- chân ----
        _btnExport = DialogUi.Btn("Xuất báo cáo Excel + Word…", (s, e) => ExportReports(null));
        var close = DialogUi.Btn("Đóng", (s, e) => Close());
        close.IsCancel = true;
        var bottom = new StackPanel();
        bottom.Children.Add(_status);
        var btns = DialogUi.Row(_btnExport, close);
        btns.HorizontalAlignment = HorizontalAlignment.Right;
        bottom.Children.Add(btns);

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(bottom);
        root.Children.Add(_tabs);
        Content = root;
        RefreshAll();
    }

    private List<string> OurChoices()
    {
        var l = new List<string> { NoCase };
        l.AddRange(_ses.OurNames());
        return l;
    }

    // ------------------------------------------------------------------ API (cũng dùng cho tự kiểm tra)
    public CrossCheckReport Report => _rep;

    public void AddFiles(IEnumerable<string> files)
    {
        var errs = new List<string>();
        foreach (var f in files)
        {
            if (_ses.Import.Files.Contains(f)) continue;
            try { _ses.AddFile(f); }
            catch (Exception ex) { errs.Add(System.IO.Path.GetFileName(f) + ": " + ex.Message); }
        }
        Reanalyze();
        if (errs.Count > 0) MessageBox.Show(this, "Không đọc được:\n" + string.Join("\n", errs), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void SelectTab(int i) { if (i >= 0 && i < _tabs.Items.Count) _tabs.SelectedIndex = i; }

    /// <summary>Chạy so sánh theo cặp trường hợp hiện tại; null nếu chưa có file hoặc lỗi.</summary>
    public CrossCheckReport RunCompare()
    {
        DialogUi.Commit(_gPairs);
        if (_ses.Import.Files.Count == 0) { _status.Text = "Chưa chọn file kết quả của ETABS/SAP2000/SAFE."; return null; }
        Cursor = System.Windows.Input.Cursors.Wait;
        try
        {
            ReadOptions();
            if (_ses.External == null) _ses.Analyze();
            _rep = _ses.Run();
            FillResults();
            var st = _rep.Stat(CrossCheckSession.GroupMembers);
            _status.Text = $"Đã so sánh {_rep.Pairs.Count(x => x.Active)} trường hợp/tổ hợp, {_rep.Members.Count(m => m.CaseCount > 0)} cấu kiện: "
                           + $"TB {st?.Mean:0.00} %, lớn nhất {st?.Max:0.00} % ({st?.MaxAt}); {st?.Warn} cảnh báo, {st?.Fail} sai lệch lớn. Đánh giá chung: {CrossCheckReport.LevelText(_rep.Overall)}.";
            return _rep;
        }
        catch (Exception ex)
        {
            _status.Text = "Lỗi so sánh: " + ex.Message;
            MessageBox.Show(this, "Lỗi so sánh:\n\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
        finally { Cursor = null; }
    }

    /// <summary>Chọn cấu kiện và vẽ biểu đồ hai phía.</summary>
    public void ShowMember(int frameId)
    {
        if (_rep == null) return;
        var m = _rep.Members.FirstOrDefault(x => x.FrameId == frameId);
        if (m == null) return;
        _plotId = frameId;
        var cases = m.Cases.Select(c => c.ExternalCase).ToList();
        _cbPlotCase.ItemsSource = cases;
        _cbPlotCase.SelectedItem = cases.Contains(m.WorstCase) ? m.WorstCase : cases.FirstOrDefault();
        var row = (_gMembers.ItemsSource as IEnumerable<MemberRow>)?.FirstOrDefault(x => x.Id == frameId);
        if (row != null && !ReferenceEquals(_gMembers.SelectedItem, row)) { _gMembers.SelectedItem = row; _gMembers.ScrollIntoView(row); }
        Plot();
    }

    /// <summary>Ghi SoSanh_ETABS.xlsx + BaoCaoSoSanh_ETABS.docx vào thư mục (null = hỏi thư mục); trả về đường dẫn Excel hoặc null.</summary>
    public string ExportReports(string dir)
    {
        if (_rep == null && RunCompare() == null) return null;
        if (dir == null)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Chọn thư mục lưu báo cáo so sánh" };
            if (dlg.ShowDialog(this) != true) return null;
            dir = dlg.FolderName;
        }
        try
        {
            var (xl, wd) = CrossCheckWriter.SaveAll(_rep, dir);
            _status.Text = "Đã xuất " + xl + " và " + wd;
            return xl;
        }
        catch (Exception ex)
        {
            _status.Text = "Lỗi xuất báo cáo: " + ex.Message;
            MessageBox.Show(this, "Lỗi xuất báo cáo:\n\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }

    // ------------------------------------------------------------------ thao tác
    private void ExportModel(bool etabs)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = etabs ? "ETABS text (*.e2k)|*.e2k" : "SAP2000 text (*.s2k)|*.s2k",
            FileName = etabs ? "MoHinh.e2k" : "MoHinh.s2k",
            Title = etabs ? "Xuất mô hình ETABS" : "Xuất mô hình SAP2000"
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            if (etabs) E2kExporter.Export(_p, dlg.FileName);
            else S2kExporter.Export(_p, dlg.FileName);
            _status.Text = "Đã xuất mô hình: " + dlg.FileName + ". Mở trong " + (etabs ? "ETABS" : "SAP2000") + ", chạy phân tích, xuất bảng kết quả rồi bấm \"Thêm file kết quả…\".";
        }
        catch (Exception ex)
        {
            _status.Text = "Lỗi xuất mô hình: " + ex.Message;
            MessageBox.Show(this, "Lỗi xuất mô hình:\n\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PickFiles()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Title = "Chọn bảng kết quả xuất từ ETABS / SAP2000 / SAFE",
            Filter = "Bảng kết quả (*.xlsx;*.xlsm;*.csv;*.txt;*.tsv;*.s2k;*.$2k;*.$et)|*.xlsx;*.xlsm;*.csv;*.txt;*.tsv;*.s2k;*.$2k;*.$et|Tất cả (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;
        Cursor = System.Windows.Input.Cursors.Wait;
        try { AddFiles(dlg.FileNames); }
        finally { Cursor = null; }
    }

    private void ResetSession()
    {
        _ses = new CrossCheckSession(_p, _ours);
        _rep = null;
        _plotId = -1;
        RefreshAll();
        FillResults();
    }

    private void ReadOptions()
    {
        var o = _ses.Options;
        o.WarnPercent = Math.Max(0.01, DialogUi.ParseNum(_txtWarn.Text, 5));
        o.FailPercent = Math.Max(o.WarnPercent, DialogUi.ParseNum(_txtFail.Text, 10));
        _ses.Import.Options.ForceUnit = (string)_cbForce.SelectedItem ?? "";
        _ses.Import.Options.LengthUnit = (string)_cbLength.SelectedItem ?? "";
    }

    private void Reanalyze()
    {
        if (_ses == null || _gPairs == null) return;
        try
        {
            DialogUi.Commit(_gPairs);
            ReadOptions();
            if (_ses.Import.Files.Count > 0) _ses.Analyze();
            _rep = null;
        }
        catch (Exception ex) { _status.Text = "Lỗi đọc bảng: " + ex.Message; }
        RefreshAll();
    }

    private void RefreshAll()
    {
        var files = _ses.Import.Files;
        _files.Text = files.Count == 0 ? "Chưa chọn file." : "File: " + string.Join("; ", files.Select(System.IO.Path.GetFileName));
        _gTables.ItemsSource = _ses.Import.Tables.Select(t => new TableRow { T = t }).ToList();
        _gPairs.ItemsSource = _ses.Pairs.Select(x => new PairRow { P = x }).ToList();
        if (_btnRun != null) _btnRun.IsEnabled = files.Count > 0;
        if (_btnExport != null) _btnExport.IsEnabled = files.Count > 0;
        if (files.Count == 0) { _status.Text = "Thêm file bảng kết quả của ETABS/SAP2000/SAFE để bắt đầu."; return; }
        string sw = _ses.Import.DetectSoftware();
        int nT = _ses.Import.Tables.Count(t => t.Kind is not (ExtTableKind.Ignored or ExtTableKind.Unknown));
        _status.Text = $"Phần mềm: {sw}. {nT} bảng dùng được; {_ses.Pairs.Count(x => x.Active)}/{_ses.Pairs.Count} trường hợp ghép được. {_ses.Import.MatchSummary()} Bấm \"So sánh\".";
    }

    // ------------------------------------------------------------------ hiển thị kết quả
    private void FillResults()
    {
        if (_rep == null)
        {
            _gMembers.ItemsSource = null; _gOther.ItemsSource = null; _summary.Text = ""; _canvas.Children.Clear(); _plotNote.Text = "";
            return;
        }
        var stories = new List<string> { AllItems };
        stories.AddRange(_rep.Members.Select(m => m.Story).Where(s => !string.IsNullOrEmpty(s)).Distinct()
            .OrderBy(s => _p.Stories.FirstOrDefault(x => x.Name == s)?.Elevation ?? 0));
        var kinds = new List<string> { AllItems };
        kinds.AddRange(_rep.Members.Select(m => m.Kind).Distinct());
        string cs = _cbStory.SelectedItem as string, ck = _cbKind.SelectedItem as string;
        _cbStory.ItemsSource = stories; _cbStory.SelectedItem = cs != null && stories.Contains(cs) ? cs : AllItems;
        _cbKind.ItemsSource = kinds; _cbKind.SelectedItem = ck != null && kinds.Contains(ck) ? ck : AllItems;
        FillMembers();
        FillOther();
        FillSummary();
        var first = _rep.Worst.FirstOrDefault();
        if (first != null) ShowMember(first.FrameId);
    }

    private void FillMembers()
    {
        if (_rep == null) return;
        string st = _cbStory.SelectedItem as string, kd = _cbKind.SelectedItem as string;
        bool bad = _chkOnlyBad.IsChecked == true;
        var rows = _rep.Members
            .Where(m => st == null || st == AllItems || m.Story == st)
            .Where(m => kd == null || kd == AllItems || m.Kind == kd)
            .Where(m => !bad || m.Level is CrossLevel.Warn or CrossLevel.Fail)
            .OrderByDescending(m => double.IsNaN(m.WorstPercent) ? -1 : m.WorstPercent)
            .Select(m => new MemberRow { M = m }).ToList();
        _gMembers.ItemsSource = rows;
    }

    private static string V6(double[] v, string fmt = "0.0") => string.Join("; ", v.Take(3).Select(x => x.ToString(fmt, Inv)));

    private void FillOther()
    {
        var rows = new List<OtherRow>();
        foreach (var b in _rep.BaseReactions)
            rows.Add(new OtherRow
            {
                Group = "Tổng phản lực", Item = "FX; FY; FZ (kN) – " + b.Source, Case = b.ExternalCase, Ours = V6(b.Ours), Ext = V6(b.Ext),
                Pct = b.WorstPercent.ToString("0.00", Inv), Lv = b.Level
            });
        foreach (var s in _rep.Stories.OrderBy(x => x.Quantity).ThenByDescending(x => x.Elevation))
        {
            bool d = s.Quantity == "Chuyển vị";
            rows.Add(new OtherRow
            {
                Group = s.Quantity + (d ? " (mm)" : ""), Item = $"{s.Story}{(s.Top ? " (mái)" : "")} – {s.Direction}", Case = s.ExternalCase,
                Ours = d ? (s.Ours * 1000).ToString("0.00", Inv) : s.Ours.ToString("0.00000", Inv), Ext = d ? (s.Ext * 1000).ToString("0.00", Inv) : s.Ext.ToString("0.00000", Inv),
                Pct = s.Percent.ToString("0.00", Inv), Lv = s.Level
            });
        }
        foreach (var r in _rep.Reactions.OrderByDescending(x => double.IsNaN(x.WorstPercent) ? 1e9 : x.WorstPercent))
            rows.Add(new OtherRow
            {
                Group = "Phản lực gối", Item = $"nút {r.NodeId} ({r.X:0.##}; {r.Y:0.##}; {r.Z:0.##}) – lớn nhất {r.WorstComponent}" + (r.Note.Length > 0 ? " – " + r.Note : ""),
                Case = r.ExternalCase, Ours = "FX; FY; FZ = " + V6(r.Ours), Ext = V6(r.Ext),
                Pct = double.IsNaN(r.WorstPercent) ? "—" : r.WorstPercent.ToString("0.00", Inv), Lv = r.Level
            });
        foreach (var m in _rep.Modal)
            rows.Add(new OtherRow
            {
                Group = "Chu kỳ (s)", Item = "Mode " + m.Mode + (m.Note.Length > 0 ? " – " + m.Note : ""), Case = "",
                Ours = double.IsNaN(m.OurPeriod) ? "—" : m.OurPeriod.ToString("0.000", Inv), Ext = m.ExtPeriod.ToString("0.000", Inv),
                Pct = double.IsNaN(m.Percent) ? "—" : m.Percent.ToString("0.00", Inv), Lv = CrossLevel.NoData
            });
        _gOther.ItemsSource = rows;
    }

    private void FillSummary()
    {
        var r = _rep;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"So sánh Việt Const ↔ {r.Software} – đánh giá chung: {CrossCheckReport.LevelText(r.Overall).ToUpperInvariant()}");
        sb.AppendLine($"Ngưỡng: ≤ {r.Options.WarnPercent:0.#} % phù hợp; {r.Options.WarnPercent:0.#}–{r.Options.FailPercent:0.#} % cảnh báo; > {r.Options.FailPercent:0.#} % sai lệch lớn.");
        sb.AppendLine();
        foreach (var s in r.Stats.Where(s => s.Count > 0))
            sb.AppendLine($"• {s.Group}: {s.Count} mục – TB {s.Mean:0.00} %, lớn nhất {s.Max:0.00} % ({s.MaxAt}); phù hợp {s.Ok}, cảnh báo {s.Warn}, sai lệch lớn {s.Fail}, thiếu số liệu {s.NoData}.");
        if (r.UnmatchedExternal.Count > 0)
            sb.AppendLine($"• {r.UnmatchedExternal.Count} thanh trong file ngoài không khớp: " + string.Join(", ", r.UnmatchedExternal.Take(10).Select(m => m.External)) + (r.UnmatchedExternal.Count > 10 ? ", …" : ""));
        sb.AppendLine();
        sb.AppendLine("Gợi ý nguyên nhân:");
        foreach (var h in r.Hints) sb.AppendLine("• " + h);
        sb.AppendLine();
        sb.AppendLine("Nguyên nhân sai lệch thường gặp:");
        foreach (var g in CrossCheckReport.Guidance) sb.AppendLine("• " + g);
        if (r.Messages.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Thông báo:");
            foreach (var m in r.Messages.Take(30)) sb.AppendLine("• " + m);
        }
        _summary.Text = sb.ToString();
    }

    // ------------------------------------------------------------------ biểu đồ
    private void Plot()
    {
        _canvas.Children.Clear();
        if (_rep == null || _plotId < 0) { _plotNote.Text = "Nhấp đúp một cấu kiện để xem biểu đồ."; return; }
        var m = _rep.Members.FirstOrDefault(x => x.FrameId == _plotId);
        string cs = _cbPlotCase.SelectedItem as string;
        var mc = m?.Cases.FirstOrDefault(c => c.ExternalCase == cs) ?? m?.Cases.FirstOrDefault();
        if (mc == null) { _plotNote.Text = m == null ? "" : $"Cấu kiện {m.Label} #{m.FrameId} không có số liệu trong file ngoài."; return; }
        int k = Math.Max(0, Array.IndexOf(new[] { "P", "V2", "V3", "T", "M2", "M3" }, _cbPlotComp.SelectedItem as string ?? "M3"));
        var o = mc.OurForces; var e = mc.ExtForces;
        if (o == null || e == null || o.Count < 1 || e.Count < 1) return;
        double w = Math.Max(200, _canvas.ActualWidth), h = Math.Max(160, _canvas.ActualHeight);
        double L = Math.Max(Math.Max(o.Stations[^1], e.Stations[^1]), 1e-6);
        double vmax = Math.Max(1e-9, Math.Max(o.F.Max(v => Math.Abs(v[k])), e.F.Max(v => Math.Abs(v[k]))));
        double x0 = 40, x1 = w - 40, yc = h / 2, sc = (h / 2 - 28) / vmax;
        double X(double s) => x0 + (x1 - x0) * s / L;
        // M dương (căng dưới) vẽ về phía dưới trục – thớ chịu kéo; lực vẽ dương lên trên
        double sign = k >= 4 ? 1 : -1;
        _canvas.Children.Add(new Line { X1 = x0, X2 = x1, Y1 = yc, Y2 = yc, Stroke = Brushes.Black, StrokeThickness = 1.5 });
        Polyline Curve(FrameForces f, Brush br, bool dash)
        {
            var pl = new Polyline { Stroke = br, StrokeThickness = dash ? 2 : 2.5 };
            if (dash) pl.StrokeDashArray = new DoubleCollection { 4, 3 };
            pl.Points.Add(new Point(X(f.Stations[0]), yc));
            for (int i = 0; i < f.Count; i++) pl.Points.Add(new Point(X(f.Stations[i]), yc + sign * f.F[i][k] * sc));
            pl.Points.Add(new Point(X(f.Stations[^1]), yc));
            return pl;
        }
        _canvas.Children.Add(Curve(o, Brushes.RoyalBlue, false));
        _canvas.Children.Add(Curve(e, Brushes.Firebrick, true));
        void Mark(FrameForces f, Brush br, bool below)
        {
            int imax = 0;
            for (int i = 1; i < f.Count; i++) if (Math.Abs(f.F[i][k]) > Math.Abs(f.F[imax][k])) imax = i;
            var tb = new TextBlock { Text = f.F[imax][k].ToString("0.00", Inv), FontSize = 12, Foreground = br, FontWeight = FontWeights.SemiBold };
            Canvas.SetLeft(tb, Math.Min(w - 60, X(f.Stations[imax]) + 3));
            Canvas.SetTop(tb, yc + sign * f.F[imax][k] * sc + (below ? 2 : -18));
            _canvas.Children.Add(tb);
        }
        Mark(o, Brushes.RoyalBlue, false);
        Mark(e, Brushes.Firebrick, true);
        var leg = new TextBlock { Text = "━ Việt Const    ┅ " + _rep.Software, FontSize = 12, Foreground = Brushes.DimGray };
        Canvas.SetLeft(leg, 8); Canvas.SetTop(leg, 4);
        _canvas.Children.Add(leg);
        string comp = new[] { "P", "V2", "V3", "T", "M2", "M3" }[k];
        _plotNote.Text = $"{comp} ({(k >= 3 ? "kN.m" : "kN")}) cấu kiện {mc.Label} #{mc.FrameId}, {mc.ExternalCase} ↔ {mc.OurCase}: |{comp}| VXT {mc.OurMax[k]:0.00}, ngoài {mc.ExtMax[k]:0.00}, "
                         + $"sai lệch lớn nhất {mc.Diff[k]:0.00} ({mc.Percent[k]:0.00} %); L = {L:0.00} m; {mc.Stations} trạm. Đánh giá cấu kiện: {CrossCheckReport.LevelText(mc.Level)} "
                         + $"(lớn nhất {mc.WorstPercent:0.00} % ở {mc.WorstComponent}).";
    }
}
