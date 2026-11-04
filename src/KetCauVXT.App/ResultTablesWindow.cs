using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Analysis;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.App;

/// <summary>Định dạng ô số của bảng kết quả.</summary>
internal sealed class TableCellConverter : IValueConverter
{
    public string Format { get; init; } = "0.###";
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double d ? d.ToString(Format, CultureInfo.InvariantCulture) : value?.ToString() ?? "";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Hiển thị → Bảng kết quả… (Display → Show Tables của ETABS), mở không chặn: chọn bảng (chuyển vị nút, phản lực, nội lực dầm/cột,
/// lệch tầng, chu kỳ & tỷ lệ khối lượng, phản lực đáy), lọc theo trường hợp / tổ hợp và tầng, xuất Excel (.xlsx) / CSV, sao chép.
/// Dữ liệu từ <see cref="ResultTables.Build"/>.</summary>
internal sealed class ResultTablesWindow : Window
{
    private readonly Project _p;
    private readonly AnalysisResults _res;
    private readonly ListBox _tables = new() { Width = 270, Margin = new Thickness(0, 0, 8, 0) };
    private readonly ComboBox _case = new() { Width = 220, Margin = new Thickness(0, 0, 8, 0) };
    private readonly ComboBox _story = new() { Width = 120, Margin = new Thickness(0, 0, 8, 0) };
    private readonly DataGrid _grid = DialogUi.NewGrid();
    private readonly TextBlock _info = new() { Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
    private const string AllCases = "(Mọi trường hợp + tổ hợp tuyến tính)";
    private const string AllStoriesText = "(Tất cả)";
    private bool _loading;

    public ResultTable Table { get; private set; }

    public ResultTablesWindow(Project p, AnalysisResults res, ResultTableKind initial = ResultTableKind.JointReactions)
    {
        _p = p; _res = res;
        DialogUi.Setup(this, $"Bảng kết quả (Show Tables) – nguồn: {res.Source}", 1280, 700);
        _grid.IsReadOnly = true;
        _grid.EnableRowVirtualization = true;
        _grid.EnableColumnVirtualization = true;
        _loading = true;
        foreach (var (k, t) in ResultTables.All) _tables.Items.Add(new ListBoxItem { Content = t, Tag = k });
        var cases = new List<string> { AllCases };
        cases.AddRange(ResultTables.SelectableCases(res));
        _case.ItemsSource = cases;
        _case.SelectedIndex = 0;
        var stories = new List<string> { AllStoriesText };
        stories.AddRange(p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name));
        stories.Add("Chân cột");
        _story.ItemsSource = stories;
        _story.SelectedIndex = 0;
        _loading = false;
        _tables.SelectionChanged += (s, e) => Rebuild();
        _case.SelectionChanged += (s, e) => Rebuild();
        _story.SelectionChanged += (s, e) => Rebuild();

        var bar = DialogUi.Row(
            DialogUi.Label("Trường hợp / tổ hợp"), _case, DialogUi.Label("Tầng"), _story,
            DialogUi.Btn("Xuất Excel…", (s, e) => SaveAs(true), true),
            DialogUi.Btn("Xuất CSV…", (s, e) => SaveAs(false)),
            DialogUi.Btn("Sao chép", (s, e) => { if (Table != null) { try { Clipboard.SetText(Table.ToCsv('\t')); _info.Text = "Đã sao chép bảng (Tab) – dán vào Excel."; } catch (Exception ex) { _info.Text = ex.Message; } } }),
            new Border { Width = 16 },
            DialogUi.Btn("Đóng", (s, e) => Close()));
        var right = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top); right.Children.Add(bar);
        DockPanel.SetDock(_info, Dock.Bottom); right.Children.Add(_info);
        right.Children.Add(_grid);
        var dock = new DockPanel { Margin = new Thickness(10) };
        var left = new DockPanel();
        var lt = DialogUi.Label("Bảng", double.NaN, true);
        DockPanel.SetDock(lt, Dock.Top); left.Children.Add(lt);
        left.Children.Add(_tables);
        DockPanel.SetDock(left, Dock.Left); dock.Children.Add(left);
        dock.Children.Add(right);
        Content = dock;
        SelectTable(initial);
    }

    /// <summary>Chọn bảng (dùng cho menu Phản lực gối… và tự kiểm tra).</summary>
    public void SelectTable(ResultTableKind k)
    {
        var it = _tables.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag is ResultTableKind kk && kk == k);
        if (it != null) _tables.SelectedItem = it;
        Rebuild();
    }

    /// <summary>Đặt bộ lọc trường hợp (null = mọi trường hợp).</summary>
    public void SetCase(string name)
    {
        _case.SelectedItem = name != null && ((List<string>)_case.ItemsSource).Contains(name) ? name : AllCases;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_loading) return;
        var k = (_tables.SelectedItem as ListBoxItem)?.Tag is ResultTableKind kk ? kk : ResultTableKind.JointReactions;
        string c = _case.SelectedItem as string;
        string st = _story.SelectedItem as string;
        var f = new ResultTableFilter
        {
            Cases = c == null || c == AllCases ? null : new List<string> { c },
            Story = st == null || st == AllStoriesText ? null : st
        };
        try { Table = ResultTables.Build(_res, k, f); }
        catch (Exception ex) { _info.Text = "Không lập được bảng: " + ex.Message; return; }
        _grid.Columns.Clear();
        for (int i = 0; i < Table.Columns.Count; i++)
        {
            string h = Table.Columns[i];
            string fmt = h.Contains("(rad)") ? "0.000000" : h.Contains("Tỷ số") || k == ResultTableKind.ModalPeriods ? "0.0000" : "0.###";
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = h, IsReadOnly = true,
                Binding = new Binding($"[{i}]") { Mode = BindingMode.OneWay, Converter = new TableCellConverter { Format = fmt } }
            });
        }
        _grid.ItemsSource = Table.Rows;
        _info.Text = $"{Table.Title}: {Table.Rows.Count} dòng. Đơn vị kN, m (chuyển vị mm, góc xoay rad)." + (Table.Note.Length > 0 ? " " + Table.Note : "");
    }

    /// <summary>Ghi bảng hiện tại ra file (.xlsx hoặc .csv). Trả về đường dẫn.</summary>
    public string Export(string path)
    {
        if (Table == null) return null;
        if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            File.WriteAllText(path, Table.ToCsv(), new System.Text.UTF8Encoding(true));
        else
        {
            var x = new XlsxWriter();
            var sh = x.AddSheet(XlsxWriter.CleanName(Table.Kind.ToString()));
            sh.NumberStyle = XlStyle.Number3;
            sh.AddHeaderRow(Table.Columns.Cast<object>().ToArray());
            foreach (var r in Table.Rows) sh.AddRow(r);
            x.Save(path);
        }
        return path;
    }

    private void SaveAs(bool excel)
    {
        if (Table == null) return;
        var dlg = new SaveFileDialog
        {
            Filter = excel ? "Excel (*.xlsx)|*.xlsx" : "CSV (*.csv)|*.csv",
            FileName = Table.Kind + (excel ? ".xlsx" : ".csv")
        };
        if (dlg.ShowDialog(this) != true) return;
        try { Export(dlg.FileName); _info.Text = "Đã ghi " + dlg.FileName; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
