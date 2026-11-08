using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Analysis;
using KetCauVXT.Import.Csi;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Hộp thoại "Nhập kết quả nội lực (ETABS/SAP/SAFE/khác)": chọn nhiều file, danh sách bảng nhận diện (số dòng, đơn vị),
/// phân loại trường hợp/tổ hợp (TTGH I / TTGH II / trường hợp tải / bỏ qua, tự nhận theo tên), tóm tắt khớp cấu kiện (khớp/không khớp + lý do),
/// ánh xạ cột cho bảng "phần mềm khác", tuỳ chọn SAFE, xem trước biểu đồ M3 của một thanh; Áp dụng → bộ nội lực hiện hành → thiết kế.</summary>
internal sealed class ResultImportDialog : Window
{
    // ------------------------------------------------------------------ dòng bảng
    internal sealed class TableRow
    {
        public ExtTable T { get; init; }
        public string File => System.IO.Path.GetFileName(T.File);
        public string Name => T.Name;
        public string Kind => KindText(T.Kind);
        public int Rows => T.Rows.Count;
        public string Units => T.UnitsText();
        public string Program => T.Program;
    }

    internal sealed class CaseRow : RowBase
    {
        public ImportedCase C { get; init; }
        public string Name => C.Name;
        public string CaseType => C.CaseType;
        public string MaxMin => C.HasMaxMin ? "Max/Min" : "";
        public int Rows => C.Rows;
        public string Note => C.Note;
        public string UseText
        {
            get => UseNames[(int)C.Use];
            set { int i = Array.IndexOf(UseNames, value); if (i >= 0) { C.Use = (CaseUse)i; Raise(); } }
        }
        public void Refresh() => RaiseAll();
    }

    internal sealed class MatchRow
    {
        public FrameMatchInfo M { get; init; }
        public string External => M.External;
        public string Story => M.Story;
        public string Frame => M.Matched ? $"{M.Label} (#{M.FrameId})" : "—";
        public string Method => M.Matched ? M.Method + (M.Reversed ? ", ngược chiều" : "") : "KHÔNG KHỚP";
        public string Reason => M.Reason;
    }

    internal static readonly string[] UseNames = { "Bỏ qua", "Trường hợp tải", "TTGH I (thiết kế)", "TTGH II (tiêu chuẩn)", "TTGH I + II" };
    private static readonly string[] Softwares = { "(tự nhận diện)", "ETABS", "SAP2000", "SAFE", "ETABS + SAFE", "Phần mềm khác" };
    private static readonly string[] Forces = { "kN", "Tonf", "kgf", "N", "kip" };
    private static readonly string[] Lengths = { "m", "cm", "mm", "ft", "in" };
    private const string AutoStory = "(tự chọn tầng chứa nhiều điểm nhất)";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    internal static string KindText(ExtTableKind k) => k switch
    {
        ExtTableKind.FrameForces => "Nội lực thanh",
        ExtTableKind.JointReactions => "Phản lực nút",
        ExtTableKind.JointDisplacements => "Chuyển vị nút",
        ExtTableKind.BaseReactions => "Phản lực tổng (kiểm tra)",
        ExtTableKind.StoryDrifts => "Chuyển vị lệch tầng",
        ExtTableKind.JointCoordinates => "Toạ độ nút (khớp hình học)",
        ExtTableKind.FrameConnectivity => "Nối thanh (khớp hình học)",
        ExtTableKind.StripForces => "Nội lực dải sàn (SAFE)",
        ExtTableKind.SlabForces => "Mô men phần tử sàn",
        ExtTableKind.SoilPressures => "Áp lực đất nền",
        ExtTableKind.PunchingShear => "Chọc thủng",
        ExtTableKind.Unknown => "Chưa nhận diện – ánh xạ cột",
        _ => "Bỏ qua (bảng mô hình)"
    };

    private readonly Project _p;
    private readonly AnalysisResults _own;
    private readonly Func<AnalysisResults, bool, Task<bool>> _apply;
    private ResultImportSession _ses;
    private AnalysisResults _built;
    private bool _working;

    private readonly TabControl _tabs = new() { Margin = new Thickness(0, 6, 0, 6) };
    private readonly TextBlock _files = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 2, 0, 2) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) };
    private readonly ComboBox _cbSoft = new() { Width = 150, ItemsSource = Softwares, SelectedIndex = 0 };
    private readonly ComboBox _cbForce = new() { Width = 70, ItemsSource = Forces, SelectedIndex = 0 };
    private readonly ComboBox _cbLength = new() { Width = 60, ItemsSource = Lengths, SelectedIndex = 0 };
    private readonly DataGrid _gTables = DialogUi.NewGrid();
    private readonly DataGrid _gCases = DialogUi.NewGrid();
    private readonly DataGrid _gMatch = DialogUi.NewGrid();
    private readonly TextBlock _matchSummary = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 4) };
    private readonly CheckBox _chkFill = new() { Content = "Thanh không có trong file: lấy nội lực của chương trình (cùng tên trường hợp/tổ hợp)", IsChecked = true, Margin = new Thickness(0, 2, 12, 2) };
    private readonly TextBox _txtTol = new() { Width = 60, Text = "0.02" };
    // ánh xạ cột
    private readonly ComboBox _cbGenTable = new() { Width = 360 };
    private readonly Dictionary<string, ComboBox> _genCols = new();
    private readonly ComboBox _cbGenForce = new() { Width = 70, ItemsSource = Forces, SelectedIndex = 0 };
    private readonly ComboBox _cbGenLength = new() { Width = 60, ItemsSource = Lengths, SelectedIndex = 0 };
    private readonly CheckBox _chkRel = new() { Content = "Trạm đo tương đối 0…1", Margin = new Thickness(0, 2, 12, 2) };
    private readonly CheckBox _chkFlipM = new() { Content = "Đổi dấu M3 (phần mềm quy ước M dương căng trên)", Margin = new Thickness(0, 2, 12, 2) };
    private readonly CheckBox _chkFlipP = new() { Content = "Đổi dấu P (nén dương)", Margin = new Thickness(0, 2, 12, 2) };
    // SAFE
    private readonly ComboBox _cbSafeStory = new() { Width = 260 };
    private readonly TextBox _txtStripW = new() { Width = 60, Text = "1.0" };
    private readonly TextBox _txtOffX = new() { Width = 60, Text = "0" }, _txtOffY = new() { Width = 60, Text = "0" };
    private readonly CheckBox _chkWood = new() { Content = "Kể mô men xoắn M12 theo Wood–Armer (M ± |M12|) khi dùng mô men phần tử sàn", IsChecked = true, Margin = new Thickness(0, 2, 0, 2) };
    // xem trước
    private readonly ComboBox _cbPrevFrame = new() { Width = 300 };
    private readonly ComboBox _cbPrevCase = new() { Width = 220 };
    private readonly Canvas _canvas = new() { Background = Brushes.White, ClipToBounds = true, MinHeight = 240 };
    private readonly TextBlock _prevNote = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Button _btnApply, _btnApplyDesign;

    public ResultImportDialog(Project p, AnalysisResults own, Func<AnalysisResults, bool, Task<bool>> apply)
    {
        ZoomPan.Wrap(_canvas);   // thu phóng / di chuyển hình vẽ – ZoomPan.cs
        _p = p; _own = own; _apply = apply;
        _ses = new ResultImportSession(p, own);
        DialogUi.Setup(this, "Nhập kết quả nội lực – ETABS / SAP2000 / SAFE / phần mềm khác", 1250, 760);
        Help.Attach(this, "phan-tich.nhap-ket-qua");   // nút "?" + F1 – Help.cs

        // ---- đầu: file, phần mềm, đơn vị ----
        var top = new StackPanel();
        top.Children.Add(DialogUi.Row(
            DialogUi.Btn("Thêm file…", (s, e) => PickFiles(), true),
            DialogUi.Btn("Xoá danh sách", (s, e) => ResetSession()),
            DialogUi.Label("Phần mềm:"), _cbSoft,
            DialogUi.Label("   Đơn vị khi bảng không ghi đơn vị:"), _cbForce, _cbLength));
        top.Children.Add(_files);
        top.Children.Add(DialogUi.Hint("ETABS/SAP2000: Display → Show Tables → chọn bảng kết quả (Element Forces - Beams/Columns/Frames, Joint Reactions, Joint Displacements; "
                                       + "thêm Point/Frame Object Connectivity để khớp theo toạ độ) → File → Export to Excel (.xlsx) hoặc CSV/Text. "
                                       + "SAFE: Strip Forces (kèm toạ độ GlobalX/GlobalY), Slab Forces, Soil Pressures, Punching Shear Design."));
        _cbSoft.SelectionChanged += (s, e) => Reanalyze();
        _cbForce.SelectionChanged += (s, e) => Reanalyze();
        _cbLength.SelectionChanged += (s, e) => Reanalyze();

        // ---- tab bảng ----
        _gTables.IsReadOnly = true;
        _gTables.Columns.Add(DialogUi.Text("File", nameof(TableRow.File), true, 170));
        _gTables.Columns.Add(DialogUi.Text("Bảng", nameof(TableRow.Name), true, 280));
        _gTables.Columns.Add(DialogUi.Text("Nhận diện", nameof(TableRow.Kind), true, 210));
        _gTables.Columns.Add(DialogUi.Text("Số dòng", nameof(TableRow.Rows), true, 70));
        _gTables.Columns.Add(DialogUi.Text("Đơn vị", nameof(TableRow.Units), true, 230));
        _gTables.Columns.Add(DialogUi.Text("Chương trình", nameof(TableRow.Program), true, 90));
        _tabs.Items.Add(new TabItem { Header = "Bảng dữ liệu", Content = _gTables });

        // ---- tab trường hợp ----
        _gCases.CanUserSortColumns = true;
        _gCases.Columns.Add(DialogUi.Text("Trường hợp / tổ hợp", nameof(CaseRow.Name), true, 220));
        _gCases.Columns.Add(DialogUi.Text("Loại", nameof(CaseRow.CaseType), true, 110));
        _gCases.Columns.Add(DialogUi.Text("Bao", nameof(CaseRow.MaxMin), true, 60));
        _gCases.Columns.Add(DialogUi.Text("Số dòng", nameof(CaseRow.Rows), true, 70));
        _gCases.Columns.Add(DialogUi.Combo("Dùng cho", nameof(CaseRow.UseText), UseNames, 170));
        _gCases.Columns.Add(DialogUi.Text("Ghi chú", nameof(CaseRow.Note), true, 380));
        var casePanel = new DockPanel();
        var caseTop = DialogUi.Row(
            DialogUi.Btn("Tự phân loại lại", (s, e) => AutoClassify()),
            DialogUi.Btn("Chọn: TTGH I", (s, e) => SetSelectedUse(CaseUse.ULS)),
            DialogUi.Btn("TTGH II", (s, e) => SetSelectedUse(CaseUse.SLS)),
            DialogUi.Btn("Trường hợp tải", (s, e) => SetSelectedUse(CaseUse.Load)),
            DialogUi.Btn("Bỏ qua", (s, e) => SetSelectedUse(CaseUse.Ignore)),
            DialogUi.Label("  Tự nhận theo tên: ULS, TTGH1, COMB, ENVE, BAO, TH1… → TTGH I; SLS, TTGH2, SERV, TC1… → TTGH II; Modal → bỏ qua."));
        DockPanel.SetDock(caseTop, Dock.Top);
        casePanel.Children.Add(caseTop);
        casePanel.Children.Add(_gCases);
        _tabs.Items.Add(new TabItem { Header = "Trường hợp / tổ hợp", Content = casePanel });

        // ---- tab khớp cấu kiện ----
        _gMatch.IsReadOnly = true;
        _gMatch.Columns.Add(DialogUi.Text("Thanh trong file", nameof(MatchRow.External), true, 160));
        _gMatch.Columns.Add(DialogUi.Text("Tầng", nameof(MatchRow.Story), true, 80));
        _gMatch.Columns.Add(DialogUi.Text("Thanh dự án", nameof(MatchRow.Frame), true, 130));
        _gMatch.Columns.Add(DialogUi.Text("Cách khớp", nameof(MatchRow.Method), true, 150));
        _gMatch.Columns.Add(DialogUi.Text("Ghi chú / lý do", nameof(MatchRow.Reason), true, 520));
        _gMatch.LoadingRow += (s, e) => { if (e.Row.Item is MatchRow m && !m.M.Matched) e.Row.Background = new SolidColorBrush(Color.FromRgb(246, 210, 210)); else e.Row.Background = Brushes.White; };
        var matchPanel = new DockPanel();
        var mt = new StackPanel();
        mt.Children.Add(_matchSummary);
        mt.Children.Add(DialogUi.Row(_chkFill, DialogUi.Label("Sai số khớp toạ độ (m):"), _txtTol, DialogUi.Btn("Khớp lại", (s, e) => Reanalyze())));
        mt.Children.Add(DialogUi.Hint("Thứ tự khớp: (1) tên gốc khi mô hình nhập từ cùng file .e2k/.s2k; (2) toạ độ hai đầu thanh khi có bảng toạ độ nút + nối thanh "
                                      + "(thanh chia/gộp khác nhau vẫn khớp); (3) tên do chương trình xuất .e2k/.s2k (C12, B12, D12, 12, 12_3) có kiểm tra loại thanh và tầng."));
        DockPanel.SetDock(mt, Dock.Top);
        matchPanel.Children.Add(mt);
        matchPanel.Children.Add(_gMatch);
        _tabs.Items.Add(new TabItem { Header = "Khớp cấu kiện", Content = matchPanel });

        // ---- tab ánh xạ cột ----
        var gen = new StackPanel { Margin = new Thickness(4) };
        gen.Children.Add(DialogUi.Hint("Dùng cho bảng nội lực của phần mềm khác (CSV/Excel tự do): chọn bảng, chỉ cột tên cấu kiện (tên thanh của dự án hoặc Id), "
                                       + "trạm đo, trường hợp/tổ hợp và các thành phần nội lực theo quy ước ETABS (P kéo dương, M3 dương căng dưới)."));
        gen.Children.Add(DialogUi.Row(DialogUi.Label("Bảng:", 120), _cbGenTable));
        _cbGenTable.SelectionChanged += (s, e) => FillGenColumns();
        foreach (var (key, label) in new[] { ("E", "Tên cấu kiện *"), ("S", "Trạm đo"), ("C", "Trường hợp *"), ("P", "P"), ("V2", "V2"), ("V3", "V3"), ("T", "T"), ("M2", "M2"), ("M3", "M3") })
        {
            var cb = new ComboBox { Width = 260 };
            _genCols[key] = cb;
            gen.Children.Add(DialogUi.Row(DialogUi.Label(label, 120), cb));
        }
        gen.Children.Add(DialogUi.Row(DialogUi.Label("Đơn vị:", 120), _cbGenForce, _cbGenLength));
        gen.Children.Add(DialogUi.Row(_chkRel, _chkFlipM, _chkFlipP));
        gen.Children.Add(DialogUi.Row(DialogUi.Btn("Áp dụng ánh xạ", (s, e) => ApplyGeneric(), true)));
        _tabs.Items.Add(new TabItem { Header = "Ánh xạ cột (phần mềm khác)", Content = new ScrollViewer { Content = gen, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });

        // ---- tab SAFE ----
        var safe = new StackPanel { Margin = new Thickness(4) };
        safe.Children.Add(DialogUi.Hint("Mô men dải (Strip Forces, chia cho bề rộng dải) hoặc mô men phần tử sàn (M11, M22) được gán vào ô sàn của dự án theo toạ độ; "
                                        + "thiết kế sàn lấy mô men nhịp dương lớn nhất và mô men gối âm nhỏ nhất trong ô thay cho phương pháp hệ số. "
                                        + "Phản lực/lò xo điểm (Joint Reactions) khớp nút gối theo toạ độ mặt bằng; áp lực đất nền và chọc thủng ghi vào thuyết minh."));
        var stories = new List<string> { AutoStory };
        stories.AddRange(p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name));
        _cbSafeStory.ItemsSource = stories; _cbSafeStory.SelectedIndex = 0;
        safe.Children.Add(DialogUi.Row(DialogUi.Label("Tầng áp dụng (bảng không có cao độ):", 260), _cbSafeStory));
        safe.Children.Add(DialogUi.Row(DialogUi.Label("Bề rộng dải khi bảng không ghi (m):", 260), _txtStripW));
        safe.Children.Add(DialogUi.Row(DialogUi.Label("Dời toạ độ SAFE về dự án ΔX, ΔY (m):", 260), _txtOffX, _txtOffY));
        safe.Children.Add(_chkWood);
        safe.Children.Add(DialogUi.Row(DialogUi.Btn("Cập nhật", (s, e) => Reanalyze())));
        _tabs.Items.Add(new TabItem { Header = "SAFE / sàn, móng", Content = safe });

        // ---- tab xem trước ----
        var prev = new DockPanel();
        var pt = DialogUi.Row(DialogUi.Label("Thanh:"), _cbPrevFrame, DialogUi.Label("  Trường hợp:"), _cbPrevCase, DialogUi.Btn("Xem trước", (s, e) => Preview(true)));
        DockPanel.SetDock(pt, Dock.Top);
        prev.Children.Add(pt);
        DockPanel.SetDock(_prevNote, Dock.Bottom);
        prev.Children.Add(_prevNote);
        prev.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Child = _canvas, Margin = new Thickness(0, 4, 0, 0) });
        _cbPrevFrame.ItemsSource = p.Frames.OrderBy(f => f.Story).ThenBy(f => f.Id).Select(f => FrameItem(f)).ToList();
        if (p.Frames.Count > 0) _cbPrevFrame.SelectedItem = FrameItem(p.Frames.FirstOrDefault(f => f.Kind == FrameKind.Beam) ?? p.Frames[0]);
        _cbPrevFrame.SelectionChanged += (s, e) => Preview(false);
        _cbPrevCase.SelectionChanged += (s, e) => Preview(false);
        _canvas.SizeChanged += (s, e) => Preview(false);
        _tabs.Items.Add(new TabItem { Header = "Xem trước M3", Content = prev });

        // ---- chân ----
        _btnApply = DialogUi.Btn("Áp dụng", async (s, e) => await ApplyAsync(false));
        _btnApplyDesign = DialogUi.Btn("Áp dụng và thiết kế", async (s, e) => await ApplyAsync(true), true);
        var close = DialogUi.Btn("Đóng", (s, e) => Close());
        close.IsCancel = true;
        var bottom = new StackPanel();
        bottom.Children.Add(_status);
        bottom.Children.Add(DialogUi.Hint("Mô hình phân tích (sơ đồ, tải trọng, tổ hợp) là trách nhiệm của người lập mô hình trong phần mềm nguồn; thuyết minh sẽ ghi chương \"SỐ LIỆU NỘI LỰC NHẬP TỪ …\". "
                                          + "Chạy lại phân tích (F5) sẽ hỏi trước khi thay thế nội lực nhập."));
        var btns = DialogUi.Row(_btnApply, _btnApplyDesign, close);
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

    private static string FrameItem(FrameElement f) => $"#{f.Id} {f.Label} – {f.Kind} – tầng {f.Story}";

    private int SelectedFrameId()
    {
        if (_cbPrevFrame.SelectedItem is not string s || !s.StartsWith("#")) return -1;
        int sp = s.IndexOf(' ');
        return int.TryParse(s.Substring(1, sp > 1 ? sp - 1 : s.Length - 1), NumberStyles.Integer, Inv, out int id) ? id : -1;
    }

    // ------------------------------------------------------------------ API (cũng dùng cho tự kiểm tra)
    public void AddFiles(IEnumerable<string> files)
    {
        var errs = new List<string>();
        foreach (var f in files)
        {
            if (_ses.Files.Contains(f)) continue;
            try { _ses.AddFile(f); }
            catch (Exception ex) { errs.Add(System.IO.Path.GetFileName(f) + ": " + ex.Message); }
        }
        Reanalyze();
        if (errs.Count > 0) MessageBox.Show(this, "Không đọc được:\n" + string.Join("\n", errs), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void SelectTab(int i) { if (i >= 0 && i < _tabs.Items.Count) _tabs.SelectedIndex = i; }

    /// <summary>Chọn thanh để xem trước biểu đồ M3.</summary>
    public void PreviewFrame(int frameId)
    {
        var f = _p.Frames.FirstOrDefault(x => x.Id == frameId);
        if (f == null) return;
        _cbPrevFrame.SelectedItem = FrameItem(f);
        Preview(true);
    }

    public AnalysisResults Built => _built;

    /// <summary>Dựng bộ nội lực theo lựa chọn hiện tại và giao cho cửa sổ chính.</summary>
    public async Task<bool> ApplyAsync(bool design)
    {
        if (_working) return false;
        DialogUi.Commit(_gCases);
        if (_ses.Files.Count == 0) { _status.Text = "Chưa chọn file kết quả."; return false; }
        _working = true;
        IsEnabled = false;
        try
        {
            ReadOptions();
            var res = _ses.Build();
            _built = res;
            if (res.Cases.Count == 0 && res.SlabMoments.Count == 0)
            {
                MessageBox.Show(this, "Không có số liệu nội lực nào được nhập (kiểm tra bảng, phân loại trường hợp và khớp cấu kiện).", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            bool ok = await _apply(res, design);
            _status.Text = ok ? $"Đã áp dụng nội lực {res.Source}: {res.External.MatchedFrames} thanh, {res.Cases.Count} trường hợp/tổ hợp." + (design ? " Đã thiết kế lại." : "")
                              : "Áp dụng chưa xong (xem thanh trạng thái cửa sổ chính).";
            return ok;
        }
        catch (Exception ex)
        {
            _status.Text = "Lỗi: " + ex.Message;
            MessageBox.Show(this, "Lỗi nhập kết quả:\n\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally { _working = false; IsEnabled = true; }
    }

    // ------------------------------------------------------------------ thao tác
    private void PickFiles()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Title = "Chọn bảng kết quả xuất từ ETABS / SAP2000 / SAFE / phần mềm khác",
            Filter = "Bảng kết quả (*.xlsx;*.xlsm;*.csv;*.txt;*.tsv;*.s2k;*.$2k;*.$et;*.$sf)|*.xlsx;*.xlsm;*.csv;*.txt;*.tsv;*.s2k;*.$2k;*.$et;*.$sf|Excel (*.xlsx)|*.xlsx;*.xlsm|CSV / văn bản (*.csv;*.txt)|*.csv;*.txt;*.tsv|Tất cả (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;
        SetWait(true);
        try { AddFiles(dlg.FileNames); }
        finally { SetWait(false); }
    }

    private void SetWait(bool wait) => Cursor = wait ? System.Windows.Input.Cursors.Wait : null;

    private void ResetSession()
    {
        _ses = new ResultImportSession(_p, _own);
        _built = null;
        RefreshAll();
    }

    private void ReadOptions()
    {
        var o = _ses.Options;
        o.Software = _cbSoft.SelectedIndex > 0 ? (string)_cbSoft.SelectedItem : "";
        o.ForceUnit = (string)_cbForce.SelectedItem ?? "";
        o.LengthUnit = (string)_cbLength.SelectedItem ?? "";
        o.FillUnmatchedFromOwn = _chkFill.IsChecked == true;
        o.Tolerance = Math.Max(1e-4, DialogUi.ParseNum(_txtTol.Text, 0.02));
        o.SafeStory = _cbSafeStory.SelectedIndex > 0 ? (string)_cbSafeStory.SelectedItem : "";
        o.DefaultStripWidth = Math.Max(0.05, DialogUi.ParseNum(_txtStripW.Text, 1.0));
        o.OffsetX = DialogUi.ParseNum(_txtOffX.Text, 0);
        o.OffsetY = DialogUi.ParseNum(_txtOffY.Text, 0);
        o.WoodArmer = _chkWood.IsChecked == true;
    }

    private void Reanalyze()
    {
        if (_ses == null || _gCases == null) return;
        try
        {
            DialogUi.Commit(_gCases);
            ReadOptions();
            if (_ses.Files.Count > 0) _ses.Analyze();
            _built = null;
        }
        catch (Exception ex) { _status.Text = "Lỗi phân tích bảng: " + ex.Message; }
        RefreshAll();
    }

    private void AutoClassify()
    {
        foreach (var c in _ses.Cases) { c.Use = _ses.AutoUse(c, out string n); c.Note = n; }
        _built = null;
        RefreshCases();
    }

    private void SetSelectedUse(CaseUse u)
    {
        foreach (var r in _gCases.SelectedItems.OfType<CaseRow>()) { r.C.Use = u; r.Refresh(); }
        _built = null;
    }

    private void RefreshAll()
    {
        _files.Text = _ses.Files.Count == 0 ? "Chưa chọn file." : "File: " + string.Join("; ", _ses.Files.Select(System.IO.Path.GetFileName));
        _gTables.ItemsSource = _ses.Tables.Select(t => new TableRow { T = t }).ToList();
        RefreshCases();
        _gMatch.ItemsSource = _ses.Matches.OrderBy(m => m.Matched ? 1 : 0).ThenBy(m => m.Story).ThenBy(m => m.External).Select(m => new MatchRow { M = m }).ToList();
        _matchSummary.Text = _ses.Matches.Count == 0 ? "Chưa có bảng nội lực thanh." : _ses.MatchSummary();
        var unknown = _ses.Tables.Where(t => t.Kind == ExtTableKind.Unknown).Select(t => t.Name).Distinct().ToList();
        string cur = _cbGenTable.SelectedItem as string;
        _cbGenTable.ItemsSource = unknown;
        _cbGenTable.SelectedItem = cur != null && unknown.Contains(cur) ? cur : unknown.FirstOrDefault();
        int nT = _ses.Tables.Count(t => t.Kind is not (ExtTableKind.Ignored or ExtTableKind.Unknown));
        string sw = _ses.Files.Count > 0 ? _ses.DetectSoftware() : "–";
        _status.Text = _ses.Files.Count == 0 ? "Thêm file bảng kết quả để bắt đầu."
            : $"Phần mềm: {sw}. {nT} bảng dùng được, {unknown.Count} bảng chưa nhận diện, {_ses.Cases.Count} trường hợp/tổ hợp "
              + $"({_ses.Cases.Count(c => c.Use is CaseUse.ULS or CaseUse.Both)} TTGH I, {_ses.Cases.Count(c => c.Use is CaseUse.SLS or CaseUse.Both)} TTGH II). "
              + string.Join(" ", _ses.Messages);
        var cases = _ses.Cases.Where(c => c.Use != CaseUse.Ignore).SelectMany(c => c.HasMaxMin ? new[] { c.Name + " (Max)", c.Name + " (Min)" } : new[] { c.Name }).ToList();
        string cc = _cbPrevCase.SelectedItem as string;
        _cbPrevCase.ItemsSource = cases;
        _cbPrevCase.SelectedItem = cc != null && cases.Contains(cc) ? cc : cases.FirstOrDefault(n => _ses.Cases.Any(c => c.Name == n && c.Use is CaseUse.ULS or CaseUse.Both)) ?? cases.FirstOrDefault();
        _btnApply.IsEnabled = _btnApplyDesign.IsEnabled = _ses.Files.Count > 0;
        Preview(false);
    }

    private void RefreshCases() => _gCases.ItemsSource = _ses.Cases.Select(c => new CaseRow { C = c }).ToList();

    // ------------------------------------------------------------------ ánh xạ cột
    private ExtTable GenTable() => _ses.Tables.FirstOrDefault(t => t.Kind == ExtTableKind.Unknown && t.Name == _cbGenTable.SelectedItem as string);

    private void FillGenColumns()
    {
        var t = GenTable();
        var items = new List<string> { "(không có)" };
        if (t != null) items.AddRange(t.Headers.Select((h, i) => $"{i + 1}: {h}"));
        foreach (var kv in _genCols)
        {
            kv.Value.ItemsSource = items;
            int guess = t == null ? -1 : kv.Key switch
            {
                "E" => t.Col("Frame", "Element", "Member", "Beam", "Column", "Label", "Name", "PhanTu", "Phần tử", "Ten", "Tên"),
                "S" => t.Col("Station", "Distance", "X", "ViTri", "Vị trí", "Loc"),
                "C" => t.Col("Case", "Combo", "LoadCase", "Load Case", "TH", "To hop", "Tổ hợp"),
                "P" => t.Col("P", "N", "Axial"),
                "V2" => t.Col("V2", "Q", "Qz", "Vz", "Shear"),
                "V3" => t.Col("V3", "Qy", "Vy"),
                "T" => t.Col("T", "Mx", "Torsion"),
                "M2" => t.Col("M2", "My"),
                "M3" => t.Col("M3", "Mz", "M", "Moment"),
                _ => -1
            };
            kv.Value.SelectedIndex = guess + 1;
        }
    }

    private int GenCol(string key) => _genCols[key].SelectedIndex - 1;

    private void ApplyGeneric()
    {
        var t = GenTable();
        if (t == null) { _status.Text = "Không có bảng chưa nhận diện để ánh xạ."; return; }
        try
        {
            _ses.ApplyGeneric(new GenericMapping
            {
                Table = t.Name, File = t.File, ColElement = GenCol("E"), ColStation = GenCol("S"), ColCase = GenCol("C"),
                ColP = GenCol("P"), ColV2 = GenCol("V2"), ColV3 = GenCol("V3"), ColT = GenCol("T"), ColM2 = GenCol("M2"), ColM3 = GenCol("M3"),
                ForceUnit = (string)_cbGenForce.SelectedItem ?? "kN", LengthUnit = (string)_cbGenLength.SelectedItem ?? "m",
                StationRelative = _chkRel.IsChecked == true, FlipM3 = _chkFlipM.IsChecked == true, FlipP = _chkFlipP.IsChecked == true
            });
            Reanalyze();
            _status.Text = $"Đã ánh xạ bảng \"{t.Name}\" thành bảng nội lực thanh. " + _status.Text;
        }
        catch (Exception ex) { _status.Text = "Lỗi ánh xạ: " + ex.Message; }
    }

    // ------------------------------------------------------------------ xem trước
    private void Preview(bool build)
    {
        _canvas.Children.Clear();
        if (_ses == null || _ses.Files.Count == 0) { _prevNote.Text = "Chưa có file."; return; }
        int id = SelectedFrameId();
        string cs = _cbPrevCase.SelectedItem as string;
        if (id < 0 || string.IsNullOrEmpty(cs)) { _prevNote.Text = "Chọn thanh và trường hợp."; return; }
        if (_built == null)
        {
            if (!build) { _prevNote.Text = "Bấm \"Xem trước\" để dựng nội lực theo lựa chọn hiện tại."; return; }
            try { DialogUi.Commit(_gCases); ReadOptions(); _built = _ses.Build(); }
            catch (Exception ex) { _prevNote.Text = "Lỗi: " + ex.Message; return; }
        }
        if (!_built.Cases.TryGetValue(cs, out var cr) || !cr.Frames.TryGetValue(id, out var ff) || ff.Count < 2)
        {
            _prevNote.Text = $"Thanh #{id} không có nội lực của \"{cs}\" trong file nhập.";
            return;
        }
        double w = Math.Max(200, _canvas.ActualWidth), h = Math.Max(200, _canvas.ActualHeight);
        double L = Math.Max(ff.Stations[^1], 1e-6);
        double mmax = Math.Max(1e-9, ff.F.Max(v => Math.Abs(v[5])));
        double x0 = 50, x1 = w - 50, yc = h / 2, k = (h / 2 - 30) / mmax;
        double X(double s) => x0 + (x1 - x0) * s / L;
        var axis = new System.Windows.Shapes.Line { X1 = x0, X2 = x1, Y1 = yc, Y2 = yc, Stroke = Brushes.Black, StrokeThickness = 2 };
        _canvas.Children.Add(axis);
        // M3 dương (căng dưới) vẽ về phía dưới trục – thớ chịu kéo
        var poly = new System.Windows.Shapes.Polygon { Fill = new SolidColorBrush(Color.FromArgb(70, 220, 60, 40)), Stroke = Brushes.Firebrick, StrokeThickness = 1.5 };
        poly.Points.Add(new Point(X(ff.Stations[0]), yc));
        for (int i = 0; i < ff.Count; i++) poly.Points.Add(new Point(X(ff.Stations[i]), yc + ff.F[i][5] * k));
        poly.Points.Add(new Point(X(ff.Stations[^1]), yc));
        _canvas.Children.Add(poly);
        int imax = 0, imin = 0;
        for (int i = 1; i < ff.Count; i++) { if (ff.F[i][5] > ff.F[imax][5]) imax = i; if (ff.F[i][5] < ff.F[imin][5]) imin = i; }
        void PutLabel(int i)
        {
            var tb = new TextBlock { Text = ff.F[i][5].ToString("0.00", Inv), FontSize = 12, Foreground = Brushes.Firebrick, FontWeight = FontWeights.SemiBold };
            Canvas.SetLeft(tb, Math.Min(w - 60, X(ff.Stations[i]) + 3));
            Canvas.SetTop(tb, yc + ff.F[i][5] * k + (ff.F[i][5] >= 0 ? 2 : -18));
            _canvas.Children.Add(tb);
        }
        PutLabel(imax);
        if (imin != imax) PutLabel(imin);
        var f = _p.Frames.First(x => x.Id == id);
        var mi = _ses.Matches.FirstOrDefault(m => m.FrameId == id);
        _prevNote.Text = $"M3 (kN.m) thanh {f.Label} #{id}, {cs}: max {ff.F[imax][5]:0.00} tại x = {ff.Stations[imax]:0.00} m, min {ff.F[imin][5]:0.00} tại x = {ff.Stations[imin]:0.00} m; "
                         + $"{ff.Count} trạm; L = {L:0.00} m. Khớp: {(mi != null ? mi.Method + (mi.Reversed ? " (ngược chiều)" : "") : "lấy theo chương trình")}. "
                         + "Vẽ về phía thớ chịu kéo (M3 dương = căng dưới).";
    }
}
