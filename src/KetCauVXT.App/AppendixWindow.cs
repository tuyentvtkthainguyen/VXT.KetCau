using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Docs;
using KetCauVXT.Model;
using KetCauVXT.Report;
using Microsoft.Win32;

namespace KetCauVXT.App;

/// <summary>Cửa sổ "Phụ lục tính toán &amp; hướng dẫn": cây mục bên trái, nội dung (thẻ từng hạng mục, bảng tra) bên phải,
/// ô tìm kiếm không dấu trên toàn bộ phụ lục, xuất Word và in.</summary>
internal sealed class AppendixWindow : Window
{
    private readonly Project _p;
    private readonly List<AppendixSection> _sections;
    private readonly TreeView _tree;
    private readonly TextBox _txtSearch;
    private readonly TextBlock _txtStatus;
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _detail;
    private AppendixSection _current;
    private List<(AppendixSection Section, AppendixItem Item)> _results;
    private bool _suppressSearch;

    private static readonly Brush CardBorder = Frozen(Color.FromRgb(200, 208, 220));
    private static readonly Brush CardBack = Frozen(Color.FromRgb(252, 252, 254));
    private static readonly Brush CardHighlight = Frozen(Color.FromRgb(255, 248, 214));
    private static readonly Brush LabelBrush = Frozen(Color.FromRgb(90, 98, 112));
    private static readonly Brush FormulaBrush = Frozen(Color.FromRgb(20, 50, 120));
    private static readonly Brush SafetyBrush = Frozen(Color.FromRgb(150, 40, 20));
    private static readonly Brush HeaderBack = Frozen(Color.FromRgb(217, 226, 243));

    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    public AppendixWindow(Project p)
    {
        _p = p;
        _sections = Appendix.Sections(p);
        DialogUi.Setup(this, "Phụ lục tính toán & hướng dẫn – Kết cấu VXT", 1180, 760);
        ShowInTaskbar = true;
        ResizeMode = ResizeMode.CanResizeWithGrip;

        // ---- thanh trên: tìm kiếm + nút ----
        _txtSearch = new TextBox { Width = 320, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0),
            ToolTip = "Tìm trên toàn bộ phụ lục, không phân biệt dấu (vd. \"he so an toan\", \"k(z)\", \"SPT\", \"chọc thủng\")" };
        _txtSearch.TextChanged += (s, e) => RunSearch();
        var top = new DockPanel { Margin = new Thickness(8, 8, 8, 4), LastChildFill = false };
        var left = DialogUi.Row(DialogUi.Label("Tìm (không dấu):"), _txtSearch, DialogUi.Btn("Xoá", (s, e) => { _txtSearch.Text = ""; _txtSearch.Focus(); }));
        DockPanel.SetDock(left, Dock.Left); top.Children.Add(left);
        var right = DialogUi.Row(DialogUi.Btn("Xuất Word…", (s, e) => ExportWord(), true), DialogUi.Btn("In…", (s, e) => PrintDoc()), DialogUi.Btn("Đóng", (s, e) => Close()));
        DockPanel.SetDock(right, Dock.Right); top.Children.Add(right);

        // ---- trái: cây mục ----
        _tree = new TreeView { Margin = new Thickness(8, 0, 0, 0) };
        foreach (var sec in _sections)
        {
            var node = new TreeViewItem { Header = sec.FullTitle, Tag = sec, FontWeight = FontWeights.SemiBold };
            foreach (var it in sec.Items)
                node.Items.Add(new TreeViewItem { Header = it.Name, Tag = (sec, it), FontWeight = FontWeights.Normal });
            foreach (var tb in sec.Tables)
                node.Items.Add(new TreeViewItem { Header = "Bảng: " + tb.Title, Tag = (sec, tb), FontWeight = FontWeights.Normal, FontStyle = FontStyles.Italic });
            _tree.Items.Add(node);
        }
        _tree.SelectedItemChanged += (s, e) => OnTreeSelect();

        // ---- phải: nội dung ----
        _detail = new StackPanel { Margin = new Thickness(12, 4, 16, 16) };
        _scroll = new ScrollViewer { Content = _detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = Brushes.White };

        var grid = new Grid { Margin = new Thickness(0, 0, 8, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330), MinWidth = 180 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_tree, 0); grid.Children.Add(_tree);
        var split = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent };
        Grid.SetColumn(split, 1); grid.Children.Add(split);
        var border = new Border { BorderBrush = CardBorder, BorderThickness = new Thickness(1), Child = _scroll };
        Grid.SetColumn(border, 2); grid.Children.Add(border);

        _txtStatus = new TextBlock { Margin = new Thickness(8, 4, 8, 6), Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap };

        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        DockPanel.SetDock(_txtStatus, Dock.Bottom); root.Children.Add(_txtStatus);
        root.Children.Add(grid);
        Content = root;

        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { _txtSearch.Focus(); _txtSearch.SelectAll(); e.Handled = true; }
        };
        Loaded += (s, e) =>
        {
            if (_tree.Items.Count > 0 && _tree.Items[0] is TreeViewItem first) { first.IsSelected = true; first.IsExpanded = true; }
            _txtSearch.Focus();
        };
        SetStatus();
    }

    private void SetStatus(string extra = null)
    {
        int n = _sections.Sum(s => s.Items.Count);
        _txtStatus.Text = $"{_sections.Count} mục, {n} hạng mục" + (_p != null ? " – có giá trị của công trình \"" + _p.Name + "\"" : "")
                          + ". Ctrl+F: tìm kiếm. Nội dung mô tả đúng công thức chương trình đang dùng; chỗ ghi \"đơn giản hoá\" cần đối chiếu tiêu chuẩn."
                          + (string.IsNullOrEmpty(extra) ? "" : "  " + extra);
    }

    // ------------------------------------------------------------------
    private void OnTreeSelect()
    {
        if (_tree.SelectedItem is not TreeViewItem tvi) return;
        if (!string.IsNullOrWhiteSpace(_txtSearch.Text))
        {
            // chọn mục trên cây → bỏ chế độ tìm kiếm (không chạy lại tìm kiếm)
            _suppressSearch = true;
            _txtSearch.Text = "";
            _suppressSearch = false;
            SetStatus();
        }
        switch (tvi.Tag)
        {
            case AppendixSection sec:
                ShowSection(sec, null);
                break;
            case ValueTuple<AppendixSection, AppendixItem> si:
                ShowSection(si.Item1, si.Item2);
                break;
            case ValueTuple<AppendixSection, AppendixTable> st:
                ShowSection(st.Item1, st.Item2);
                break;
        }
    }

    private void ShowSection(AppendixSection sec, object focus)
    {
        _current = sec; _results = null;
        _detail.Children.Clear();
        _detail.Children.Add(new TextBlock { Text = sec.FullTitle, FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 6), TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(sec.Intro))
            _detail.Children.Add(new TextBlock { Text = sec.Intro, TextWrapping = TextWrapping.Wrap, FontStyle = FontStyles.Italic, Margin = new Thickness(0, 0, 0, 10), Foreground = Brushes.DarkSlateGray });
        FrameworkElement target = null;
        foreach (var it in sec.Items)
        {
            var card = Card(it, null, ReferenceEquals(it, focus));
            if (ReferenceEquals(it, focus)) target = card;
            _detail.Children.Add(card);
        }
        foreach (var tb in sec.Tables)
        {
            var t = TableView(tb, ReferenceEquals(tb, focus));
            if (ReferenceEquals(tb, focus)) target = t;
            _detail.Children.Add(t);
        }
        if (target != null) Dispatcher.BeginInvoke(new Action(() => target.BringIntoView()), System.Windows.Threading.DispatcherPriority.Loaded);
        else _scroll.ScrollToTop();
    }

    private void RunSearch()
    {
        if (_suppressSearch) return;
        string q = _txtSearch.Text ?? "";
        if (string.IsNullOrWhiteSpace(q))
        {
            if (_current != null) ShowSection(_current, null);
            SetStatus();
            return;
        }
        _results = Appendix.Search(_sections, q);
        _detail.Children.Clear();
        _detail.Children.Add(new TextBlock
        {
            Text = $"{_results.Count} kết quả cho \"{q.Trim()}\"", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 8)
        });
        if (_results.Count == 0)
            _detail.Children.Add(DialogUi.Hint("Không tìm thấy. Thử từ khoá ngắn hơn hoặc gõ không dấu (vd. \"vong\", \"coc\", \"gio\")."));
        foreach (var (sec, it) in _results.Take(300)) _detail.Children.Add(Card(it, sec, false));
        if (_results.Count > 300) _detail.Children.Add(DialogUi.Hint("… chỉ hiện 300 kết quả đầu."));
        _scroll.ScrollToTop();
        SetStatus($"Tìm thấy {_results.Count} hạng mục.");
    }

    // ------------------------------------------------------------------
    private static FrameworkElement Card(AppendixItem it, AppendixSection fromSection, bool highlight)
    {
        var sp = new StackPanel();
        var head = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        if (fromSection != null) head.Inlines.Add(new Run(fromSection.Code + " · ") { Foreground = LabelBrush });
        head.Inlines.Add(new Run(it.Name));
        if (!string.IsNullOrWhiteSpace(it.Symbol)) head.Inlines.Add(new Run("   " + it.Symbol) { FontStyle = FontStyles.Italic, Foreground = FormulaBrush, FontWeight = FontWeights.Normal });
        sp.Children.Add(head);

        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int row = 0;
        void Add(string label, string text, Brush fg = null, FontFamily font = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Foreground = LabelBrush, Margin = new Thickness(0, 1, 6, 1), TextWrapping = TextWrapping.Wrap };
            var v = new TextBox
            {
                Text = text, TextWrapping = TextWrapping.Wrap, IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                Margin = new Thickness(0, 1, 0, 1), Padding = new Thickness(0)
            };
            if (fg != null) v.Foreground = fg;
            if (font != null) { v.FontFamily = font; v.FontSize = 14; }
            Grid.SetRow(l, row); Grid.SetColumn(l, 0);
            Grid.SetRow(v, row); Grid.SetColumn(v, 1);
            g.Children.Add(l); g.Children.Add(v);
            row++;
        }
        Add("Công thức", it.Formula, FormulaBrush, new FontFamily("Cambria Math, Cambria, Times New Roman"));
        Add("Hệ số / giá trị", it.Values);
        Add("Hệ số an toàn", it.SafetyFactor, SafetyBrush);
        Add("Giải thích", it.Explanation);
        Add("Tiêu chuẩn", it.Standard, Brushes.DimGray);
        sp.Children.Add(g);
        return new Border
        {
            Child = sp, BorderBrush = CardBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Background = highlight ? CardHighlight : CardBack, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 8)
        };
    }

    private static FrameworkElement TableView(AppendixTable t, bool highlight)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 6, 0, 12) };
        sp.Children.Add(new TextBlock { Text = "Bảng tra: " + t.Title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap });
        var g = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        int nc = Math.Max(t.Headers.Length, t.Rows.Count == 0 ? 0 : t.Rows.Max(r => r.Length));
        for (int c = 0; c < nc; c++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MaxWidth = c == 1 && nc == 4 && t.Rows.Any(r => r.Length > 1 && r[1].Length > 60) ? 420 : 260 });
        void Cell(string text, int r, int c, bool header)
        {
            var tb = new TextBlock { Text = text ?? "", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(5, 2, 5, 2), FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal };
            var b = new Border { Child = tb, BorderBrush = CardBorder, BorderThickness = new Thickness(0, 0, 1, 1), Background = header ? HeaderBack : (highlight ? CardHighlight : Brushes.White) };
            Grid.SetRow(b, r); Grid.SetColumn(b, c);
            g.Children.Add(b);
        }
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int c = 0; c < t.Headers.Length; c++) Cell(t.Headers[c], 0, c, true);
        for (int r = 0; r < t.Rows.Count; r++)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < t.Rows[r].Length; c++) Cell(t.Rows[r][c], r + 1, c, false);
        }
        sp.Children.Add(new Border { Child = g, BorderBrush = CardBorder, BorderThickness = new Thickness(1, 1, 0, 0), HorizontalAlignment = HorizontalAlignment.Left });
        if (!string.IsNullOrWhiteSpace(t.Note)) sp.Children.Add(new TextBlock { Text = "Ghi chú: " + t.Note, FontStyle = FontStyles.Italic, Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        return sp;
    }

    // ------------------------------------------------------------------
    private void ExportWord()
    {
        var dlg = new SaveFileDialog { Filter = "Word (*.docx)|*.docx", FileName = "PhuLucTinhToan.docx", Title = "Xuất phụ lục tính toán" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            AppendixReport.ExportDocx(_p, dlg.FileName);
            MessageBox.Show(this, "Đã xuất phụ lục tính toán:\n" + dlg.FileName, Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Lỗi xuất Word:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PrintDoc()
    {
        var pick = MessageBox.Show(this, "In TOÀN BỘ phụ lục?\n\nCó: toàn bộ phụ lục\nKhông: chỉ mục / kết quả tìm kiếm đang xem",
            Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (pick == MessageBoxResult.Cancel) return;
        var pd = new PrintDialog();
        if (pd.ShowDialog() != true) return;
        try
        {
            FlowDocument doc;
            if (pick == MessageBoxResult.Yes) doc = BuildFlow(_sections, null);
            else if (_results != null) doc = BuildFlow(null, _results);
            else doc = BuildFlow(_current != null ? new List<AppendixSection> { _current } : _sections, null);
            doc.PageWidth = pd.PrintableAreaWidth; doc.PageHeight = pd.PrintableAreaHeight;
            doc.ColumnWidth = pd.PrintableAreaWidth;
            doc.PagePadding = new Thickness(56, 48, 40, 48);
            pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Phụ lục tính toán – Kết cấu VXT");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Lỗi in:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static FlowDocument BuildFlow(List<AppendixSection> sections, List<(AppendixSection Section, AppendixItem Item)> results)
    {
        var doc = new FlowDocument { FontFamily = new FontFamily("Times New Roman"), FontSize = 11.5 };
        doc.Blocks.Add(new Paragraph(new Run(AppendixReport.ChapterTitle)) { FontSize = 16, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center });
        void ItemBlock(AppendixItem it, string prefix)
        {
            var para = new Paragraph { Margin = new Thickness(0, 4, 0, 2), KeepTogether = true };
            para.Inlines.Add(new Bold(new Run(prefix + it.Name)));
            if (!string.IsNullOrWhiteSpace(it.Symbol)) para.Inlines.Add(new Italic(new Run("  (" + it.Symbol + ")")));
            void Line(string label, string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                para.Inlines.Add(new LineBreak());
                para.Inlines.Add(new Run(label + ": ") { Foreground = Brushes.DimGray });
                para.Inlines.Add(new Run(text));
            }
            Line("Công thức", it.Formula);
            Line("Hệ số / giá trị", it.Values);
            Line("Hệ số an toàn", it.SafetyFactor);
            Line("Giải thích", it.Explanation);
            Line("Tiêu chuẩn", it.Standard);
            doc.Blocks.Add(para);
        }
        if (results != null)
        {
            foreach (var (sec, it) in results) ItemBlock(it, sec.Code + " · ");
            return doc;
        }
        foreach (var s in sections)
        {
            doc.Blocks.Add(new Paragraph(new Run(s.FullTitle)) { FontSize = 13.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 12, 0, 4), KeepWithNext = true });
            if (!string.IsNullOrWhiteSpace(s.Intro)) doc.Blocks.Add(new Paragraph(new Run(s.Intro)) { FontStyle = FontStyles.Italic, Margin = new Thickness(0, 0, 0, 4) });
            foreach (var it in s.Items) ItemBlock(it, "");
            foreach (var t in s.Tables)
            {
                doc.Blocks.Add(new Paragraph(new Run("Bảng tra: " + t.Title)) { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2), KeepWithNext = true });
                var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) };
                int nc = Math.Max(t.Headers.Length, t.Rows.Count == 0 ? 0 : t.Rows.Max(r => r.Length));
                for (int c = 0; c < nc; c++) table.Columns.Add(new TableColumn());
                var grp = new TableRowGroup();
                TableRow MakeRow(string[] cells, bool header)
                {
                    var tr = new TableRow();
                    for (int c = 0; c < nc; c++)
                    {
                        string txt = c < cells.Length ? cells[c] ?? "" : "";
                        var para = new Paragraph(new Run(txt)) { Margin = new Thickness(2), FontSize = 10 };
                        if (header) para.FontWeight = FontWeights.Bold;
                        tr.Cells.Add(new TableCell(para) { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5), Background = header ? HeaderBack : null });
                    }
                    return tr;
                }
                grp.Rows.Add(MakeRow(t.Headers, true));
                foreach (var r in t.Rows) grp.Rows.Add(MakeRow(r, false));
                table.RowGroups.Add(grp);
                doc.Blocks.Add(table);
                if (!string.IsNullOrWhiteSpace(t.Note)) doc.Blocks.Add(new Paragraph(new Run("Ghi chú: " + t.Note)) { FontStyle = FontStyles.Italic, FontSize = 10 });
            }
        }
        return doc;
    }
}
