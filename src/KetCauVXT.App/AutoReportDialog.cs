using System.Windows;
using System.Windows.Controls;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.App;

/// <summary>Hộp thoại "Lập thuyết minh tự động": chọn chương, thông tin trang bìa, số ví dụ tính toán, bảng nội lực.
/// Bấm "Lập thuyết minh…" → gọi <c>run(Options)</c> (MainWindow tự chạy phân tích + thiết kế nếu kết quả chưa có / đã cũ).</summary>
internal sealed class AutoReportDialog : Window
{
    private readonly Func<ReportOptions, Task> _run;
    private readonly List<CheckBox> _chapterBoxes = new();
    private readonly Button _btnRun;

    public ReportOptions Options { get; }

    public AutoReportDialog(Project p, ReportOptions o, Func<ReportOptions, Task> run)
    {
        Options = o ?? ReportOptions.FromProject(p);
        Options.Chapters ??= new ReportChapters();
        _run = run;
        DialogUi.Setup(this, "Lập thuyết minh tính toán tự động", 980, 640);
        Help.Attach(this, "ho-so.thuyet-minh");   // nút "?" + F1 – Help.cs
        ShowInTaskbar = true;
        var ch = Options.Chapters;

        // ---- trái: chương ----
        var left = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        left.Children.Add(DialogUi.Label("Chương đưa vào thuyết minh", double.NaN, true));
        void Chk(string text, Func<bool> get, Action<bool> set)
        {
            var cb = new CheckBox { Content = text, IsChecked = get(), Margin = new Thickness(0, 3, 0, 3) };
            cb.Checked += (s, e) => set(true);
            cb.Unchecked += (s, e) => set(false);
            _chapterBoxes.Add(cb);
            left.Children.Add(cb);
        }
        Chk("Giới thiệu công trình", () => ch.Intro, v => ch.Intro = v);
        Chk("Căn cứ pháp lý và tiêu chuẩn áp dụng", () => ch.Standards, v => ch.Standards = v);
        Chk("Vật liệu, tiết diện", () => ch.Materials, v => ch.Materials = v);
        Chk("Sơ đồ tính, tải trọng, tổ hợp", () => ch.Loads, v => ch.Loads = v);
        Chk("Tải trọng gió, động đất", () => ch.WindSeismic, v => ch.WindSeismic = v);
        Chk("Kết quả nội lực", () => ch.Forces, v => ch.Forces = v);
        Chk("Thiết kế dầm", () => ch.Beams, v => ch.Beams = v);
        Chk("Thiết kế cột", () => ch.Columns, v => ch.Columns = v);
        Chk("Thiết kế sàn", () => ch.Slabs, v => ch.Slabs = v);
        Chk("Thiết kế cầu thang", () => ch.Stairs, v => ch.Stairs = v);
        Chk("Thiết kế móng (móng đơn, đài cọc, móng băng)", () => ch.Foundation, v => ch.Foundation = v);
        Chk("Địa chất (khi có hố khoan)", () => ch.Geotech, v => ch.Geotech = v);
        Chk("Thống kê cốt thép, kết cấu thép", () => ch.Steel, v => ch.Steel = v);
        Chk("Khối lượng vật tư", () => ch.Quantities, v => ch.Quantities = v);
        Chk("Phụ lục công thức tính toán", () => ch.Appendix, v => ch.Appendix = v);
        left.Children.Add(DialogUi.Row(
            DialogUi.Btn("Chọn tất cả", (s, e) => { foreach (var c in _chapterBoxes) c.IsChecked = true; }),
            DialogUi.Btn("Bỏ chọn", (s, e) => { foreach (var c in _chapterBoxes) c.IsChecked = false; })));
        left.Children.Add(DialogUi.Hint("Luôn có: trang bìa, mục lục, chương KẾT LUẬN VÀ KIẾN NGHỊ, phụ lục cảnh báo."));

        // ---- phải: trang bìa + tuỳ chọn ----
        var right = new StackPanel();
        right.Children.Add(DialogUi.Label("Thông tin trang bìa", double.NaN, true));
        void Txt(string label, Func<string> get, Action<string> set)
        {
            var tb = new TextBox { Text = get() ?? "", Width = 420, Margin = new Thickness(0, 2, 0, 2) };
            tb.TextChanged += (s, e) => set(tb.Text);
            right.Children.Add(DialogUi.Row(DialogUi.Label(label, 150), tb));
        }
        Txt("Tên công trình", () => Options.ProjectName, v => Options.ProjectName = v);
        Txt("Chủ đầu tư", () => Options.Owner, v => Options.Owner = v);
        Txt("Địa điểm xây dựng", () => Options.Location, v => Options.Location = v);
        Txt("Đơn vị thiết kế", () => Options.DesignCompany, v => Options.DesignCompany = v);
        Txt("Chủ trì thiết kế", () => Options.ChiefDesigner, v => Options.ChiefDesigner = v);
        Txt("Người lập", () => Options.Preparer, v => Options.Preparer = v);
        Txt("Kiểm tra", () => Options.Checker, v => Options.Checker = v);
        Txt("Ngày lập (dd/MM/yyyy)", () => string.IsNullOrWhiteSpace(Options.Date) ? Options.DateText() : Options.Date, v => Options.Date = v);

        right.Children.Add(DialogUi.Label("Tuỳ chọn", double.NaN, true));
        var cbEx = new ComboBox { Width = 70, ItemsSource = new[] { 1, 2, 3 }, SelectedItem = Options.Examples };
        cbEx.SelectionChanged += (s, e) => { if (cbEx.SelectedItem is int k) Options.ExamplesPerType = k; };
        right.Children.Add(DialogUi.Row(DialogUi.Label("Số ví dụ tính toán chi tiết cho mỗi loại cấu kiện", 330), cbEx));
        void Opt(string text, Func<bool> get, Action<bool> set)
        {
            var cb = new CheckBox { Content = text, IsChecked = get(), Margin = new Thickness(0, 3, 0, 3) };
            cb.Checked += (s, e) => set(true);
            cb.Unchecked += (s, e) => set(false);
            right.Children.Add(cb);
        }
        Opt("Đưa bảng nội lực bao từng thanh vào chương nội lực", () => Options.IncludeForceTables, v => Options.IncludeForceTables = v);
        Opt("Mục lục tự động (Word cập nhật khi mở file)", () => Options.TableOfContents, v => Options.TableOfContents = v);
        Opt("Đánh số trang ở chân trang", () => Options.PageNumbers, v => Options.PageNumbers = v);
        Opt("Đánh số tên bảng (Bảng n.k)", () => Options.TableCaptions, v => Options.TableCaptions = v);
        string font = p?.CadStandard?.ReportFont;
        right.Children.Add(DialogUi.Hint($"Font thuyết minh: {(string.IsNullOrWhiteSpace(font) ? DocxWriter.DefaultFont : font)} (theo chuẩn CAD/thuyết minh). "
            + "Chương trình tự chạy phân tích và thiết kế nếu chưa có kết quả hoặc mô hình đã sửa sau lần tính trước. "
            + "Khi mở bằng Word, chọn “Yes” để cập nhật mục lục và số trang."));

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(left, 0); Grid.SetColumn(right, 1);
        body.Children.Add(left); body.Children.Add(right);

        _btnRun = DialogUi.Btn("Lập thuyết minh…", async (s, e) => await RunAsync(), true);
        _btnRun.IsDefault = true;
        var close = DialogUi.Btn("Đóng", (s, e) => Close());
        close.IsCancel = true;
        var buttons = DialogUi.Row(_btnRun, close);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;

        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }

    private async Task RunAsync()
    {
        if (_run == null) return;
        _btnRun.IsEnabled = false;
        try { await _run(Options); }
        finally { _btnRun.IsEnabled = true; }
    }
}
