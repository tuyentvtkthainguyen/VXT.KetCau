using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Docs;
using Microsoft.Win32;

namespace KetCauVXT.App;

/// <summary>Cửa sổ "Hướng dẫn sử dụng": cây chương/mục bên trái, nội dung (FlowDocument) bên phải, ô tìm kiếm không dấu, "Xuất Word…".
/// Nội dung lấy từ Docs/UserGuide (dữ liệu có cấu trúc); ShowHelp(helpId) mở đúng mục (F1 theo ngữ cảnh).</summary>
internal sealed class UserGuideWindow : Window
{
    private readonly List<GuideSection> _chapters;
    private readonly TreeView _tree;
    private readonly TextBox _txtSearch;
    private readonly TextBlock _txtStatus;
    private readonly FlowDocumentScrollViewer _viewer;
    private readonly Dictionary<GuideSection, TreeViewItem> _nodes = new();
    private GuideSection _current;
    private bool _suppressTree;

    private static readonly Brush NoteBack = Frozen(Color.FromRgb(255, 242, 204));
    private static readonly Brush TipBack = Frozen(Color.FromRgb(226, 239, 218));
    private static readonly Brush WarnBack = Frozen(Color.FromRgb(252, 228, 214));
    private static readonly Brush HeadBack = Frozen(Color.FromRgb(217, 226, 243));
    private static readonly Brush HitBack = Frozen(Color.FromRgb(255, 251, 170));
    private static readonly Brush BorderBr = Frozen(Color.FromRgb(190, 196, 206));
    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    public UserGuideWindow()
    {
        _chapters = UserGuide.Chapters();
        DialogUi.Setup(this, "Hướng dẫn sử dụng – Việt Const", 1200, 780);
        ShowInTaskbar = true;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Help.SetId(this, "giao-dien.tro-giup");

        _txtSearch = new TextBox { Width = 320, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0),
            ToolTip = "Tìm trong toàn bộ hướng dẫn, không phân biệt dấu (vd. \"dam phu\", \"coc\", \"smartscreen\", \"to hop\")" };
        _txtSearch.TextChanged += (s, e) => RunSearch();
        var top = new DockPanel { Margin = new Thickness(8, 8, 8, 4), LastChildFill = false };
        var left = DialogUi.Row(DialogUi.Label("Tìm (không dấu):"), _txtSearch, DialogUi.Btn("Xoá", (s, e) => { _txtSearch.Text = ""; _txtSearch.Focus(); }));
        DockPanel.SetDock(left, Dock.Left); top.Children.Add(left);
        var right = DialogUi.Row(DialogUi.Btn("Xuất Word…", (s, e) => ExportWord(), true), DialogUi.Btn("Đóng", (s, e) => Close()));
        DockPanel.SetDock(right, Dock.Right); top.Children.Add(right);

        _tree = new TreeView { Margin = new Thickness(8, 0, 0, 0) };
        foreach (var ch in _chapters) _tree.Items.Add(Node(ch));
        _tree.SelectedItemChanged += (s, e) => OnTreeSelect();

        _viewer = new FlowDocumentScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsToolBarVisible = false, Background = Brushes.White };

        var grid = new Grid { Margin = new Thickness(0, 0, 8, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340), MinWidth = 180 });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_tree, 0); grid.Children.Add(_tree);
        var split = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent };
        Grid.SetColumn(split, 1); grid.Children.Add(split);
        var border = new Border { BorderBrush = BorderBr, BorderThickness = new Thickness(1), Child = _viewer };
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
        SetStatus();
    }

    private TreeViewItem Node(GuideSection s)
    {
        var n = new TreeViewItem { Header = s.FullTitle + (s.Pending ? "  [sắp có]" : ""), Tag = s, FontWeight = s.Level == 1 ? FontWeights.SemiBold : FontWeights.Normal };
        if (s.Pending) n.Foreground = Brushes.SaddleBrown;
        _nodes[s] = n;
        foreach (var c in s.Children) n.Items.Add(Node(c));
        return n;
    }

    private void SetStatus(string extra = null)
    {
        int n = _chapters.Sum(c => c.Flatten().Count());
        _txtStatus.Text = $"{_chapters.Count} chương, {n} mục. F1 ở bất kỳ đâu trong chương trình mở đúng mục đang làm; Ctrl+F: tìm kiếm."
                          + (string.IsNullOrEmpty(extra) ? "" : "  " + extra);
    }

    // ------------------------------------------------------------------ chọn / mở mục
    /// <summary>Mở chương thứ index (0 = chương 1).</summary>
    public void SelectChapter(int index)
    {
        if (index < 0 || index >= _chapters.Count) return;
        ShowSection(_chapters[index]);
    }

    /// <summary>Mở mục theo HelpId; không có → mục mặc định. Trả về mục đã mở.</summary>
    public GuideSection ShowHelp(string helpId)
    {
        var s = UserGuide.Find(helpId) ?? UserGuide.Find(HelpContext.DefaultId) ?? _chapters.FirstOrDefault();
        if (s != null) ShowSection(s);
        return s;
    }

    public GuideSection Current => _current;

    private void ShowSection(GuideSection s)
    {
        if (!string.IsNullOrEmpty(_txtSearch.Text)) { _suppressTree = true; _txtSearch.Text = ""; _suppressTree = false; }
        if (_nodes.TryGetValue(s, out var node))
        {
            _suppressTree = true;
            for (var p = node.Parent as TreeViewItem; p != null; p = p.Parent as TreeViewItem) p.IsExpanded = true;
            node.IsExpanded = true;
            node.IsSelected = true;
            node.BringIntoView();
            _suppressTree = false;
        }
        Render(s);
    }

    private void OnTreeSelect()
    {
        if (_suppressTree || _tree.SelectedItem is not TreeViewItem tvi || tvi.Tag is not GuideSection s) return;
        if (!string.IsNullOrEmpty(_txtSearch.Text)) { _suppressTree = true; _txtSearch.Text = ""; _suppressTree = false; SetStatus(); }
        Render(s);
    }

    /// <summary>Hiện cả chương chứa mục s, cuộn tới mục s.</summary>
    private void Render(GuideSection s)
    {
        _current = s;
        var ch = s.Chapter;
        var doc = NewDoc();
        Paragraph target = null;
        AddSection(doc, ch, s, null, ref target);
        _viewer.Document = doc;
        if (target != null && !ReferenceEquals(s, ch))
        {
            var t = target;
            Dispatcher.BeginInvoke(new Action(() => t.BringIntoView()), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private static FlowDocument NewDoc() => new()
    {
        FontFamily = new FontFamily("Segoe UI"), FontSize = 13.5, PagePadding = new Thickness(18, 10, 22, 24), TextAlignment = TextAlignment.Left,
        LineHeight = 20
    };

    // ------------------------------------------------------------------ dựng nội dung
    private static void AddInlines(InlineCollection ic, string text)
    {
        foreach (var (t, bold, italic) in UserGuide.Runs(text))
        {
            Inline r = new Run(t);
            if (bold) r = new Bold(r);
            if (italic) r = new Italic(r);
            ic.Add(r);
        }
    }

    private static Paragraph Para(string text, double size = 0, bool bold = false)
    {
        var p = new Paragraph { Margin = new Thickness(0, 3, 0, 5) };
        AddInlines(p.Inlines, text);
        if (size > 0) p.FontSize = size;
        if (bold) p.FontWeight = FontWeights.Bold;
        return p;
    }

    private void AddSection(FlowDocument doc, GuideSection s, GuideSection target, string query, ref Paragraph targetPara)
    {
        double size = s.Level == 1 ? 21 : s.Level == 2 ? 17 : 15;
        var head = new Paragraph(new Run(s.FullTitle)) { FontSize = size, FontWeight = FontWeights.Bold, Margin = new Thickness(0, s.Level == 1 ? 2 : 14, 0, 4), KeepWithNext = true,
            Foreground = s.Level == 1 ? new SolidColorBrush(Color.FromRgb(25, 55, 110)) : Brushes.Black };
        doc.Blocks.Add(head);
        if (ReferenceEquals(s, target)) targetPara = head;
        if (s.Pending)
            doc.Blocks.Add(Box("[SẮP CÓ – cập nhật sau] " + (string.IsNullOrWhiteSpace(s.PendingNote) ? "Tính năng đang được hoàn thiện." : s.PendingNote), WarnBack));
        foreach (var b in s.Blocks) AddBlock(doc, b, query);
        foreach (var c in s.Children) AddSection(doc, c, target, query, ref targetPara);
    }

    private static Block Box(string text, Brush back)
    {
        var p = Para(text);
        p.Margin = new Thickness(0);
        return new Section(p) { Background = back, BorderBrush = BorderBr, BorderThickness = new Thickness(1), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 4, 0, 8) };
    }

    private static void AddBlock(FlowDocument doc, GuideBlock b, string query)
    {
        bool hit = !string.IsNullOrWhiteSpace(query) && UserGuide.BlockMatches(b, query);
        Block blk;
        switch (b.Kind)
        {
            case GuideBlockKind.Paragraph:
                blk = Para(b.Text);
                break;
            case GuideBlockKind.Bullets:
            case GuideBlockKind.Steps:
            {
                var list = new List { MarkerStyle = b.Kind == GuideBlockKind.Steps ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Margin = new Thickness(0, 2, 0, 6), Padding = new Thickness(24, 0, 0, 0) };
                foreach (var it in b.Items)
                {
                    var p = Para(it); p.Margin = new Thickness(0, 1, 0, 2);
                    list.ListItems.Add(new ListItem(p));
                }
                blk = list;
                break;
            }
            case GuideBlockKind.Table:
            {
                var t = new Table { CellSpacing = 0, BorderBrush = BorderBr, BorderThickness = new Thickness(0.5), Margin = new Thickness(0, 4, 0, 8), FontSize = 12.5 };
                int nc = Math.Max(b.Headers.Length, b.Rows.Count == 0 ? 0 : b.Rows.Max(r => r.Length));
                for (int c = 0; c < nc; c++) t.Columns.Add(new TableColumn { Width = new GridLength(c == nc - 1 && nc > 1 ? 2 : 1, GridUnitType.Star) });
                var grp = new TableRowGroup();
                TableRow Row(string[] cells, bool header)
                {
                    var tr = new TableRow();
                    for (int c = 0; c < nc; c++)
                    {
                        var p = Para(c < cells.Length ? cells[c] ?? "" : ""); p.Margin = new Thickness(3, 2, 3, 2);
                        if (header) p.FontWeight = FontWeights.Bold;
                        tr.Cells.Add(new TableCell(p) { BorderBrush = BorderBr, BorderThickness = new Thickness(0.5), Background = header ? HeadBack : null });
                    }
                    return tr;
                }
                if (b.Headers.Length > 0) grp.Rows.Add(Row(b.Headers, true));
                foreach (var r in b.Rows) grp.Rows.Add(Row(r, false));
                t.RowGroups.Add(grp);
                blk = t;
                break;
            }
            default:
            {
                string label = b.Kind == GuideBlockKind.Warning ? "Cảnh báo: " : b.Kind == GuideBlockKind.Tip ? "Mẹo: " : "Lưu ý: ";
                blk = Box("**" + label + "**" + b.Text, b.Kind == GuideBlockKind.Warning ? WarnBack : b.Kind == GuideBlockKind.Tip ? TipBack : NoteBack);
                break;
            }
        }
        if (hit) blk.Background = HitBack;
        doc.Blocks.Add(blk);
    }

    // ------------------------------------------------------------------ tìm kiếm
    private void RunSearch()
    {
        if (_suppressTree) return;
        string q = _txtSearch.Text ?? "";
        if (string.IsNullOrWhiteSpace(q))
        {
            if (_current != null) Render(_current);
            SetStatus();
            return;
        }
        var hits = UserGuide.Search(q);
        var doc = NewDoc();
        doc.Blocks.Add(new Paragraph(new Run($"{hits.Count} mục có \"{q.Trim()}\"")) { FontSize = 17, FontWeight = FontWeights.Bold });
        if (hits.Count == 0) doc.Blocks.Add(Para("Không tìm thấy. Thử từ khoá ngắn hơn hoặc gõ không dấu (vd. \"dam\", \"coc\", \"gio\")."));
        foreach (var s in hits.Take(60))
        {
            var head = new Paragraph(new Run(s.FullTitle)) { FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 12, 0, 3), Foreground = Brushes.Navy, Cursor = Cursors.Hand,
                ToolTip = "Bấm để mở mục này trong chương" };
            var sec = s;
            head.MouseLeftButtonUp += (o, e) => ShowSection(sec);
            doc.Blocks.Add(head);
            if (s.Pending) doc.Blocks.Add(Box("[SẮP CÓ] " + s.PendingNote, WarnBack));
            foreach (var b in s.Blocks) AddBlock(doc, b, q);
        }
        if (hits.Count > 60) doc.Blocks.Add(Para("… chỉ hiện 60 mục đầu."));
        _viewer.Document = doc;
        SetStatus($"Tìm thấy {hits.Count} mục.");
    }

    // ------------------------------------------------------------------ xuất Word
    private void ExportWord()
    {
        var dlg = new SaveFileDialog { Filter = "Word (*.docx)|*.docx", FileName = UserGuide.FileBaseName + ".docx", Title = "Xuất hướng dẫn sử dụng" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            UserGuide.SaveDocx(dlg.FileName);
            MessageBox.Show(this, "Đã xuất hướng dẫn sử dụng:\n" + dlg.FileName + "\n\nMở bằng Word, chọn Yes để cập nhật mục lục và số trang.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Lỗi xuất Word:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
