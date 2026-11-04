using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using KetCauVXT.Verification;
using Microsoft.Win32;

namespace KetCauVXT.App;

/// <summary>Trợ giúp → Kiểm chứng phần mềm: chạy bộ bài toán chuẩn (tự kiểm chứng), nhập giá trị kiểm chứng độc lập
/// (sách, ví dụ mẫu, phần mềm khác, đơn vị thẩm tra – mục 5 của báo cáo), nhập lại file Excel do bên thứ ba điền, xuất báo cáo Word/Excel.</summary>
internal sealed class VerificationWindow : Window
{
    /// <summary>Một dòng lưới (bọc BenchmarkCase để sửa các cột kiểm chứng độc lập).</summary>
    internal sealed class Row
    {
        public BenchmarkCase C { get; }
        public Row(BenchmarkCase c) { C = c; }
        public string Id => C.Id;
        public string Group => C.Group;
        public string Title => C.Title;
        public string Quantity => C.Quantity + (string.IsNullOrEmpty(C.Unit) ? "" : " (" + C.Unit + ")");
        public string Reference => BenchmarkCase.Fmt(C.Reference);
        public string Computed => BenchmarkCase.Fmt(C.Computed);
        public string Error => double.IsFinite(C.ErrorPercent) ? C.ErrorPercent.ToString("0.###", CultureInfo.InvariantCulture) : "-";
        public string Tol => C.Tolerance.ToString("0.###", CultureInfo.InvariantCulture);
        public string Verdict => C.Pass ? "Đạt" : "KHÔNG ĐẠT";
        public string BookValue
        {
            get => C.UserBookValue is double v ? v.ToString("G6", CultureInfo.InvariantCulture) : "";
            set => C.UserBookValue = double.TryParse((value ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : null;
        }
        public string BookSource { get => C.UserBookSource; set => C.UserBookSource = value ?? ""; }
        public string Kind { get => C.UserKind; set => C.UserKind = value ?? ""; }
        public string Verifier { get => C.UserVerifier; set => C.UserVerifier = value ?? ""; }
        public string Date { get => C.UserDate; set => C.UserDate = value ?? ""; }
        public string BookError => C.BookErrorPercent is double e ? e.ToString("0.###", CultureInfo.InvariantCulture) : "";
        public string BookVerdict => C.BookPass == true ? "Đạt" : C.BookPass == false ? "Không đạt" : "";
    }

    private readonly ObservableCollection<Row> _rows = new();
    private readonly DataGrid _grid;
    private readonly TextBlock _summary;
    private VerificationResult _res;
    private VerificationUserData _user;

    internal VerificationResult Result => _res;

    public VerificationWindow()
    {
        Title = "Kiểm chứng phần mềm – Kết cấu VXT";
        Help.Attach(this, "kiem-chung.bao-cao");   // nút "?" + F1 – Help.cs
        Width = 1400; Height = 820; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(8) };

        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        top.Children.Add(DialogUi.Btn("Chạy kiểm chứng", async (s, e) => await RunAsync(), true));
        top.Children.Add(DialogUi.Btn("Lưu giá trị kiểm chứng độc lập", (s, e) => SaveUser()));
        top.Children.Add(DialogUi.Btn("Nhập kết quả kiểm chứng độc lập từ Excel…", (s, e) => ImportExcel()));
        top.Children.Add(DialogUi.Btn("Xuất báo cáo Word + Excel…", (s, e) => ExportDialog()));
        top.Children.Add(DialogUi.Btn("Đóng", (s, e) => Close()));
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        var note = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6), Foreground = Brushes.DimGray,
            Text = "Mục 4 – TỰ KIỂM CHỨNG: tham chiếu là lời giải giải tích / tính tay độc lập theo công thức tiêu chuẩn. "
                 + "Mục 5 – KIỂM CHỨNG ĐỘC LẬP: nhập ở các cột màu vàng (giá trị theo sách, ví dụ mẫu, phần mềm khác hoặc do kỹ sư/đơn vị thẩm tra độc lập tính), "
                 + "hoặc gửi file Excel báo cáo cho bên kiểm chứng điền rồi nhập lại. Ngưỡng so với tài liệu = max(ngưỡng; 3%)."
        };
        DockPanel.SetDock(note, Dock.Top);
        root.Children.Add(note);

        _summary = new TextBlock { FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(_summary, Dock.Bottom);
        root.Children.Add(_summary);

        _grid = new DataGrid
        {
            ItemsSource = _rows, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            FrozenColumnCount = 2
        };
        void Ro(string h, string path, double w) => _grid.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(path) { Mode = BindingMode.OneWay }, IsReadOnly = true, Width = w });
        var yellow = new SolidColorBrush(Color.FromRgb(255, 248, 205));
        void Ed(string h, string path, double w)
        {
            var st = new Style(typeof(DataGridCell));
            st.Setters.Add(new Setter(BackgroundProperty, yellow));
            _grid.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = w, CellStyle = st });
        }
        Ro("Mã", nameof(Row.Id), 60); Ro("Bài toán", nameof(Row.Title), 220); Ro("Đại lượng", nameof(Row.Quantity), 170);
        Ro("Tham chiếu", nameof(Row.Reference), 90); Ro("Kết cấu VXT", nameof(Row.Computed), 90); Ro("Sai số %", nameof(Row.Error), 65);
        Ro("Ngưỡng %", nameof(Row.Tol), 60); Ro("Kết luận", nameof(Row.Verdict), 75);
        Ed("Giá trị theo tài liệu / bên thứ ba", nameof(Row.BookValue), 110); Ed("Nguồn tài liệu (tên, trang/ví dụ)", nameof(Row.BookSource), 200);
        var kindCol = new DataGridComboBoxColumn { Header = "Loại nguồn", ItemsSource = VerificationUserData.Kinds, SelectedItemBinding = new Binding(nameof(Row.Kind)) { Mode = BindingMode.TwoWay }, Width = 170 };
        _grid.Columns.Add(kindCol);
        Ed("Người/đơn vị xác nhận", nameof(Row.Verifier), 150); Ed("Ngày", nameof(Row.Date), 80);
        Ro("Sai số so tài liệu %", nameof(Row.BookError), 80); Ro("KL (tài liệu)", nameof(Row.BookVerdict), 75);
        _grid.LoadingRow += (s, e) =>
        {
            if (e.Row.Item is Row r) e.Row.Foreground = r.C.Pass ? Brushes.Black : Brushes.Firebrick;
        };
        _grid.CellEditEnding += (s, e) => Dispatcher.BeginInvoke(new Action(() => { _grid.CommitEdit(DataGridEditingUnit.Row, true); RefreshSummary(); }));
        root.Children.Add(_grid);
        Content = root;
        Loaded += async (s, e) => await RunAsync();
    }

    /// <summary>Chạy toàn bộ bài toán chuẩn (luồng nền), gán giá trị kiểm chứng độc lập đã lưu.</summary>
    internal async Task RunAsync()
    {
        _summary.Text = "Đang chạy kiểm chứng…";
        _user = VerificationUserData.Load(VerificationUserData.UserFilePath, out string msg);
        var user = _user;
        _res = await Task.Run(() => VerificationRunner.RunAll(null, user));
        _rows.Clear();
        foreach (var c in _res.Cases) _rows.Add(new Row(c));
        RefreshSummary();
        if (!string.IsNullOrEmpty(msg)) _summary.Text += "  " + msg;
    }

    private void RefreshSummary()
    {
        if (_res == null) return;
        var book = _res.Cases.Where(c => c.UserBookValue.HasValue).ToList();
        _summary.Text = $"Tự kiểm chứng: {_res.ProblemCount} bài toán, {_res.Count} đại lượng – {_res.Passed} đạt, {_res.Failed} không đạt; sai số lớn nhất {_res.MaxErrorPercent:0.###} %.   "
                        + $"Kiểm chứng độc lập: {book.Count} giá trị, {book.Count(c => c.BookPass == true)} đạt, {book.Count(c => c.BookPass == false)} không đạt.";
    }

    private void SaveUser()
    {
        if (_res == null) return;
        _grid.CommitEdit(DataGridEditingUnit.Row, true);
        _user ??= new VerificationUserData();
        _user.CaptureFrom(_res.Cases);
        try { _user.Save(); _summary.Text = "Đã lưu giá trị kiểm chứng độc lập vào " + VerificationUserData.UserFilePath; }
        catch (Exception ex) { MessageBox.Show(this, "Không lưu được: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void ImportExcel()
    {
        var dlg = new OpenFileDialog { Title = "Chọn file Excel báo cáo kiểm chứng đã được bên kiểm chứng độc lập điền", Filter = "Excel (*.xlsx)|*.xlsx" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            _user ??= new VerificationUserData();
            int n = _user.ImportXlsx(dlg.FileName, out string msg);
            if (n > 0) { _user.Save(); await RunAsync(); }
            MessageBox.Show(this, msg, Title, MessageBoxButton.OK, n > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex) { MessageBox.Show(this, "Không đọc được file: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ExportDialog()
    {
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục lưu Báo cáo kiểm chứng (Word + Excel)" };
        if (dlg.ShowDialog(this) != true) return;
        var (d, x) = Export(dlg.FolderName);
        if (d != null) MessageBox.Show(this, "Đã xuất:\n" + d + "\n" + x, Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>Lưu giá trị đang nhập rồi xuất báo cáo vào thư mục.</summary>
    internal (string Docx, string Xlsx) Export(string folder)
    {
        if (_res == null) return (null, null);
        SaveUser();
        try { return VerificationReport.SaveAll(_res, folder); }
        catch (Exception ex) { MessageBox.Show(this, "Không xuất được báo cáo: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); return (null, null); }
    }
}
