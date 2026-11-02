using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Cửa sổ "Văn bản pháp lý &amp; tiêu chuẩn áp dụng": danh mục văn bản (Model/Regulations.cs) – lọc theo phạm vi,
/// màu theo trạng thái, chi tiết (thay thế / được thay thế, ghi chú, cần kiểm tra, nguồn), Thêm/Sửa/Xoá, đánh dấu hết hiệu lực,
/// nhập/xuất JSON, lưu làm mặc định (%AppData%\KetCauVXT\VanBanPhapLy.json), kiểm tra trích dẫn của dự án.</summary>
internal sealed class RegulationWindow : Window
{
    private const string AllScopes = "(Tất cả phạm vi)";
    private readonly Project _p;
    private readonly Regulations _reg;
    private readonly DataGrid _grid = DialogUi.NewGrid();
    private readonly ComboBox _cbScope = new() { Width = 170, Margin = new Thickness(0, 0, 8, 0) };
    private readonly CheckBox _chkCurrent = new() { Content = "Chỉ văn bản đang áp dụng", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
    private readonly TextBox _details = new()
    {
        IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontFamily = new FontFamily("Segoe UI"), Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
    };
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
    private bool _dirty;

    /// <summary>Danh mục đã thay đổi trong phiên (đã áp dụng cho phiên làm việc).</summary>
    public bool Changed { get; private set; }

    private static readonly SolidColorBrush BrCurrent = Frozen(230, 244, 234), BrCheck = Frozen(255, 246, 213),
        BrDraft = Frozen(227, 240, 251), BrOld = Frozen(248, 225, 225);

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    public static Brush StatusBrush(RegulationStatus s) => s switch
    {
        RegulationStatus.HieuLuc => BrCurrent,
        RegulationStatus.KiemTra => BrCheck,
        RegulationStatus.DuThao => BrDraft,
        _ => BrOld
    };

    public RegulationWindow(Project p)
    {
        _p = p;
        _reg = RegulationService.Registry.Clone();
        DialogUi.Setup(this, "Văn bản pháp lý & tiêu chuẩn áp dụng", 1280, 760);
        ShowInTaskbar = true;

        var root = new DockPanel { Margin = new Thickness(8) };

        // ---- thanh lọc ----
        _cbScope.ItemsSource = new[] { AllScopes }.Concat(Enum.GetValues(typeof(RegulationScope)).Cast<RegulationScope>().Select(Regulations.ScopeLabel)).ToList();
        _cbScope.SelectedIndex = 0;
        _cbScope.SelectionChanged += (s, e) => Refill();
        _chkCurrent.Checked += (s, e) => Refill();
        _chkCurrent.Unchecked += (s, e) => Refill();
        var legend = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var st in new[] { RegulationStatus.HieuLuc, RegulationStatus.KiemTra, RegulationStatus.DuThao, RegulationStatus.HetHieuLuc })
            legend.Children.Add(new Border
            {
                Background = StatusBrush(st), BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 4, 0), Child = new TextBlock { Text = Regulations.StatusLabel(st) }
            });
        var top = DialogUi.Row(DialogUi.Label("Phạm vi:"), _cbScope, _chkCurrent, DialogUi.Label("Màu trạng thái:"), legend);
        var hint = DialogUi.Hint("Danh mục có sẵn chỉ gồm văn bản đã kiểm tra nguồn (cột Nguồn trong phần chi tiết). Mục \"Cần kiểm tra\" ghi thông tin chưa xác minh – "
            + "người dùng đối chiếu văn bản gốc. Thuyết minh, phụ lục, ghi chú bản vẽ và bảng khối lượng trích dẫn theo danh mục này (văn bản đang áp dụng của từng chủ đề). "
            + "File mặc định của người dùng: " + RegulationService.UserFilePath
            + (string.IsNullOrEmpty(_reg.LoadMessage) ? "" : "\n" + _reg.LoadMessage));
        var topPanel = new StackPanel();
        topPanel.Children.Add(top);
        topPanel.Children.Add(hint);
        DockPanel.SetDock(topPanel, Dock.Top);
        root.Children.Add(topPanel);

        // ---- nút ----
        var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var b in new UIElement[]
        {
            DialogUi.Btn("＋ Thêm…", (s, e) => AddDoc()),
            DialogUi.Btn("Sửa…", (s, e) => EditDoc()),
            DialogUi.Btn("Xoá", (s, e) => DeleteDoc()),
            DialogUi.Btn("Đánh dấu hết hiệu lực…", (s, e) => MarkOld()),
            DialogUi.Btn("Nhập JSON…", (s, e) => ImportJson()),
            DialogUi.Btn("Xuất JSON…", (s, e) => ExportJson()),
            DialogUi.Btn("Lưu làm mặc định", (s, e) => SaveDefault(), true),
            DialogUi.Btn("Khôi phục danh mục có sẵn", (s, e) => ResetDefault()),
            DialogUi.Btn("Kiểm tra dự án", (s, e) => CheckProject(), true),
            DialogUi.Btn("Đóng", (s, e) => Close())
        }) { if (b is FrameworkElement fe) fe.Margin = new Thickness(0, 0, 6, 4); buttons.Children.Add(b); }
        var bottom = new StackPanel();
        bottom.Children.Add(buttons);
        bottom.Children.Add(_status);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        // ---- bảng + chi tiết ----
        _grid.IsReadOnly = true;
        _grid.SelectionMode = DataGridSelectionMode.Single;
        _grid.Columns.Add(DialogUi.Text("Số hiệu", nameof(RegulationDoc.Code), true, 190));
        _grid.Columns.Add(DialogUi.Text("Loại", nameof(RegulationDoc.KindLabel), true, 75));
        _grid.Columns.Add(DialogUi.Text("Tên / trích yếu", nameof(RegulationDoc.Title), true, 360));
        _grid.Columns.Add(DialogUi.Text("Ban hành", nameof(RegulationDoc.IssuedDate), true, 80));
        _grid.Columns.Add(DialogUi.Text("Hiệu lực", nameof(RegulationDoc.EffectiveDate), true, 85));
        _grid.Columns.Add(DialogUi.Text("Trạng thái", nameof(RegulationDoc.StatusLabel), true, 90));
        _grid.Columns.Add(DialogUi.Text("Phạm vi", nameof(RegulationDoc.ScopeLabel), true, 95));
        _grid.Columns.Add(DialogUi.Text("Được thay thế bởi", nameof(RegulationDoc.ReplacedBy), true, 200));
        _grid.LoadingRow += (s, e) =>
        {
            if (e.Row.Item is RegulationDoc d)
            {
                e.Row.Background = StatusBrush(d.Status);
                e.Row.Foreground = d.Status == RegulationStatus.HetHieuLuc ? Brushes.DimGray : Brushes.Black;
            }
        };
        _grid.SelectionChanged += (s, e) => ShowDetails(_grid.SelectedItem as RegulationDoc);
        _grid.MouseDoubleClick += (s, e) => { if (_grid.SelectedItem is RegulationDoc) EditDoc(); };

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
        Grid.SetColumn(_grid, 0);
        var split = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent };
        Grid.SetColumn(split, 1);
        var detBox = new GroupBox { Header = "Chi tiết", Content = _details, Margin = new Thickness(0, 4, 0, 4) };
        Grid.SetColumn(detBox, 2);
        body.Children.Add(_grid); body.Children.Add(split); body.Children.Add(detBox);
        root.Children.Add(body);

        Content = root;
        Refill();
        _status.Text = $"{_reg.Docs.Count} văn bản, {_reg.Current().Count} đang áp dụng. Dự án đang trích dẫn: "
                       + string.Join("; ", RegulationService.Applied(_p).Select(x => x.Citation)) + ".";
        Closing += OnClosing;
    }

    // ------------------------------------------------------------------
    private void Refill(RegulationDoc select = null)
    {
        select ??= _grid.SelectedItem as RegulationDoc;
        IEnumerable<RegulationDoc> q = _reg.Docs;
        int si = _cbScope.SelectedIndex;
        if (si > 0)
        {
            var scope = Enum.GetValues(typeof(RegulationScope)).Cast<RegulationScope>().ElementAt(si - 1);
            q = q.Where(d => d.Scope == scope);
        }
        if (_chkCurrent.IsChecked == true) q = q.Where(d => d.IsCurrent);
        var list = q.OrderBy(d => d.Status == RegulationStatus.HetHieuLuc ? 1 : 0).ThenBy(d => (int)d.Scope).ToList();
        _grid.ItemsSource = list;
        if (select != null && list.Contains(select)) _grid.SelectedItem = select;
        else if (list.Count > 0) _grid.SelectedIndex = 0;
        if (_grid.SelectedItem == null) ShowDetails(null);
    }

    private void ShowDetails(RegulationDoc d)
    {
        if (d == null) { _details.Text = ""; return; }
        var lines = new List<string>
        {
            d.Citation,
            d.Title,
            "",
            "Loại: " + d.KindLabel + "    Phạm vi: " + d.ScopeLabel,
            "Trạng thái: " + d.StatusLabel,
            "Ngày ban hành: " + (string.IsNullOrWhiteSpace(d.IssuedDate) ? "–" : d.IssuedDate) + "    Hiệu lực: " + (string.IsNullOrWhiteSpace(d.EffectiveDate) ? "–" : d.EffectiveDate),
        };
        if (d.Replaces.Count > 0) lines.Add("Thay thế: " + d.ReplacesText);
        if (!string.IsNullOrWhiteSpace(d.ReplacedBy)) lines.Add("Được thay thế bởi: " + d.ReplacedBy);
        if (d.IsSuperseded)
        {
            var succ = _reg.Successors(d);
            if (succ.Count > 0) lines.Add("Văn bản đang áp dụng thay cho văn bản này: " + string.Join("; ", succ.Select(x => x.Citation)));
        }
        var by = _reg.Docs.Where(o => o != d && o.Replaces.Any(r => Regulations.Key(r) == Regulations.Key(d.Code))).Select(o => o.Citation).ToList();
        if (by.Count > 0 && string.IsNullOrWhiteSpace(d.ReplacedBy)) lines.Add("Được ghi thay thế trong: " + string.Join("; ", by));
        if (!string.IsNullOrWhiteSpace(d.Topic)) lines.Add("Chủ đề tra trích dẫn: " + d.Topic);
        if (!string.IsNullOrWhiteSpace(d.Note)) { lines.Add(""); lines.Add("Ghi chú: " + d.Note); }
        if (!string.IsNullOrWhiteSpace(d.VerifyNote)) { lines.Add(""); lines.Add("Cần kiểm tra: " + d.VerifyNote); }
        if (d.Aliases.Count > 0) lines.Add("Cách viết khác (dò trích dẫn): " + string.Join("; ", d.Aliases));
        lines.Add("");
        lines.Add("Nguồn: " + (string.IsNullOrWhiteSpace(d.SourceUrl) ? "(chưa ghi)" : d.SourceUrl));
        _details.Text = string.Join("\n", lines);
    }

    private void Changes(string msg, RegulationDoc select = null)
    {
        _dirty = true; Changed = true;
        RegulationService.Use(_reg);   // áp dụng ngay cho phiên làm việc
        Refill(select);
        _status.Text = msg + " (đã áp dụng cho phiên làm việc – bấm \"Lưu làm mặc định\" để dùng cho các lần sau).";
    }

    // ------------------------------------------------------------------
    private void AddDoc()
    {
        var d = new RegulationDoc { Status = RegulationStatus.HieuLuc, Kind = RegulationKind.TCVN, Scope = RegulationScope.TaiTrong };
        if (!RegulationEditDialog.Edit(this, d, "Thêm văn bản")) return;
        if (_reg.Find(d.Code) != null &&
            MessageBox.Show(this, $"Đã có văn bản số hiệu \"{d.Code}\". Thay bằng nội dung mới?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _reg.Upsert(d);
        Changes("Đã thêm " + d.Citation, d);
    }

    private void EditDoc()
    {
        if (_grid.SelectedItem is not RegulationDoc sel) { _status.Text = "Chọn một văn bản trong bảng."; return; }
        var d = sel.Clone();
        string oldCode = sel.Code;
        if (!RegulationEditDialog.Edit(this, d, "Sửa văn bản")) return;
        if (Regulations.Key(oldCode) != Regulations.Key(d.Code)) _reg.Remove(oldCode);
        _reg.Upsert(d);
        Changes("Đã sửa " + d.Citation, d);
    }

    private void DeleteDoc()
    {
        if (_grid.SelectedItem is not RegulationDoc sel) { _status.Text = "Chọn một văn bản trong bảng."; return; }
        if (MessageBox.Show(this, $"Xoá \"{sel.Citation}\" khỏi danh mục?\n(Văn bản hết hiệu lực nên giữ lại và đánh dấu \"Hết hiệu lực\" để chương trình còn dò được trích dẫn cũ.)",
                Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _reg.Remove(sel.Code);
        Changes("Đã xoá " + sel.Citation);
    }

    private void MarkOld()
    {
        if (_grid.SelectedItem is not RegulationDoc sel) { _status.Text = "Chọn một văn bản trong bảng."; return; }
        string by = DialogUi.InputBox(this, "Đánh dấu hết hiệu lực", $"\"{sel.Citation}\" được thay thế bởi (số hiệu; nhiều văn bản cách nhau bằng dấu ;):", sel.ReplacedBy);
        if (by == null) return;
        _reg.MarkSuperseded(sel.Code, by);
        var missing = sel.ReplacedByList().Where(c => _reg.Find(c) == null).ToList();
        Changes($"Đã đánh dấu hết hiệu lực: {sel.Citation}" + (missing.Count > 0 ? $" – chưa có trong danh mục: {string.Join(", ", missing)} (bấm \"Thêm…\" để khai báo)" : ""), sel);
    }

    private void ImportJson()
    {
        var dlg = new OpenFileDialog { Filter = "Danh mục văn bản (*.json)|*.json|Tất cả|*.*", Title = "Nhập danh mục văn bản (JSON)" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var (a, u, r) = _reg.ImportJson(dlg.FileName);
            Changes($"Nhập {Path.GetFileName(dlg.FileName)}: thêm {a}, cập nhật {u}, xoá {r} văn bản");
        }
        catch (Exception ex) { MessageBox.Show(this, "Không đọc được file:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ExportJson()
    {
        var dlg = new SaveFileDialog { Filter = "Danh mục văn bản (*.json)|*.json", FileName = "VanBanPhapLy.json", Title = "Xuất danh mục văn bản (JSON)" };
        if (dlg.ShowDialog(this) != true) return;
        try { _reg.ExportJson(dlg.FileName); _status.Text = "Đã xuất " + dlg.FileName; }
        catch (Exception ex) { MessageBox.Show(this, "Không ghi được file:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private bool SaveDefault()
    {
        try
        {
            RegulationService.SaveUserDefault(_reg);
            _dirty = false; Changed = true;
            _status.Text = "Đã lưu làm mặc định: " + RegulationService.UserFilePath;
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Không lưu được:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void ResetDefault()
    {
        if (MessageBox.Show(this, "Bỏ mọi thay đổi của người dùng (xoá " + RegulationService.UserFilePath + ") và trở về danh mục có sẵn của chương trình?",
                Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        RegulationService.ResetUserDefault();
        var b = RegulationService.Registry;
        _reg.Docs = b.Docs.Select(x => x.Clone()).ToList();
        _reg.Removed = new List<string>();
        _dirty = false; Changed = true;
        Refill();
        _status.Text = "Đã khôi phục danh mục có sẵn.";
    }

    private void CheckProject()
    {
        var ws = RegulationService.CheckProject(_p);
        var lines = new List<string> { "KIỂM TRA DỰ ÁN" + (_p == null ? "" : " – " + _p.Name), "" };
        if (ws.Count == 0) lines.Add("Không thấy trích dẫn văn bản đã hết hiệu lực trong các ghi chú, thuyết minh, khung tên, tờ in và bảng định mức của dự án.");
        else
        {
            int nw = ws.Count(w => w.IsWarning);
            lines.Add($"{nw} cảnh báo, {ws.Count - nw} lưu ý:");
            lines.Add("");
            foreach (var w in ws) lines.Add("• " + w);
        }
        lines.Add("");
        lines.Add("Văn bản dự án trích dẫn (theo nội dung dự án): " + string.Join("; ", RegulationService.Applied(_p).Select(x => x.Citation)) + ".");
        _details.Text = string.Join("\n", lines);
        _status.Text = ws.Count == 0 ? "Kiểm tra dự án: không có cảnh báo." : $"Kiểm tra dự án: {ws.Count(w => w.IsWarning)} cảnh báo, {ws.Count(w => !w.IsWarning)} lưu ý (xem khung Chi tiết).";
    }

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_dirty) return;
        var r = MessageBox.Show(this, "Danh mục đã thay đổi (đang áp dụng cho phiên làm việc). Lưu làm mặc định cho các lần sau?", Title,
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Cancel) { e.Cancel = true; return; }
        if (r == MessageBoxResult.Yes && !SaveDefault()) e.Cancel = true;
    }
}

/// <summary>Hộp thoại thêm/sửa một văn bản.</summary>
internal static class RegulationEditDialog
{
    public static bool Edit(Window owner, RegulationDoc d, string title)
    {
        var w = new Window { Owner = owner };
        DialogUi.Setup(w, title, 720, 640);
        var g = new Grid { Margin = new Thickness(10) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int row = 0;
        T AddRow<T>(string label, T ctl) where T : FrameworkElement
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var lb = DialogUi.Label(label);
            Grid.SetRow(lb, row); Grid.SetColumn(lb, 0);
            ctl.Margin = new Thickness(0, 2, 0, 2);
            Grid.SetRow(ctl, row); Grid.SetColumn(ctl, 1);
            g.Children.Add(lb); g.Children.Add(ctl);
            row++;
            return ctl;
        }
        TextBox Tb(string v, bool multi = false) => new()
        {
            Text = v ?? "", TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, AcceptsReturn = multi,
            MinHeight = multi ? 48 : 0, VerticalScrollBarVisibility = multi ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
        };
        ComboBox Cb<TE>(Func<TE, string> label, TE value) where TE : struct, Enum
        {
            var vals = Enum.GetValues(typeof(TE)).Cast<TE>().ToList();
            var cb = new ComboBox { ItemsSource = vals.Select(label).ToList(), SelectedIndex = Math.Max(0, vals.IndexOf(value)) };
            return cb;
        }
        TE Val<TE>(ComboBox cb) where TE : struct, Enum
        {
            var vals = Enum.GetValues(typeof(TE)).Cast<TE>().ToList();
            return cb.SelectedIndex >= 0 && cb.SelectedIndex < vals.Count ? vals[cb.SelectedIndex] : vals[0];
        }

        var tCode = AddRow("Số hiệu (*)", Tb(d.Code));
        var tTitle = AddRow("Tên / trích yếu", Tb(d.Title, true));
        var cKind = AddRow("Loại", Cb<RegulationKind>(Regulations.KindLabel, d.Kind));
        var tIssued = AddRow("Ngày ban hành", Tb(d.IssuedDate));
        var tEff = AddRow("Ngày hiệu lực", Tb(d.EffectiveDate));
        var cStatus = AddRow("Trạng thái", Cb<RegulationStatus>(Regulations.StatusLabel, d.Status));
        var cScope = AddRow("Phạm vi", Cb<RegulationScope>(Regulations.ScopeLabel, d.Scope));
        var tTopic = AddRow("Chủ đề tra trích dẫn", Tb(d.Topic));
        var tRepl = AddRow("Thay thế (số hiệu; …)", Tb(d.ReplacesText));
        var tBy = AddRow("Được thay thế bởi", Tb(d.ReplacedBy));
        var tNote = AddRow("Ghi chú", Tb(d.Note, true));
        var tVerify = AddRow("Cần kiểm tra", Tb(d.VerifyNote, true));
        var tUrl = AddRow("Nguồn (URL)", Tb(d.SourceUrl));
        var tAlias = AddRow("Cách viết khác (; …)", Tb(string.Join("; ", d.Aliases)));
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var hint = DialogUi.Hint("Chủ đề: khoá để chương trình tự trích dẫn (vd. dong-dat, tai-trong, btct, ket-cau-thep, mong-coc, nen, luat-xd, nd-qlhdxd, cap-cong-trinh, dinh-muc, cv-dinh-muc, dieu-kien-tu-nhien). "
            + "Khi có văn bản mới cùng chủ đề: thêm văn bản mới (ghi \"Thay thế\") và đánh dấu văn bản cũ hết hiệu lực.");
        Grid.SetRow(hint, row); Grid.SetColumn(hint, 0); Grid.SetColumnSpan(hint, 2);
        g.Children.Add(hint);

        bool ok = false;
        var bOk = DialogUi.Btn("Đồng ý", (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(tCode.Text)) { MessageBox.Show(w, "Nhập số hiệu văn bản.", title, MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            d.Code = tCode.Text.Trim(); d.Title = tTitle.Text.Trim(); d.Kind = Val<RegulationKind>(cKind);
            d.IssuedDate = tIssued.Text.Trim(); d.EffectiveDate = tEff.Text.Trim(); d.Status = Val<RegulationStatus>(cStatus); d.Scope = Val<RegulationScope>(cScope);
            d.Topic = tTopic.Text.Trim();
            d.Replaces = tRepl.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            d.ReplacedBy = tBy.Text.Trim(); d.Note = tNote.Text.Trim(); d.VerifyNote = tVerify.Text.Trim(); d.SourceUrl = tUrl.Text.Trim();
            d.Aliases = tAlias.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            ok = true; w.DialogResult = true;
        }, true);
        bOk.IsDefault = true;
        var bCancel = DialogUi.Btn("Huỷ", (s, e) => w.DialogResult = false);
        bCancel.IsCancel = true;
        var bs = DialogUi.Row(bOk, bCancel);
        bs.HorizontalAlignment = HorizontalAlignment.Right;
        var dock = new DockPanel();
        DockPanel.SetDock(bs, Dock.Bottom);
        bs.Margin = new Thickness(10, 0, 10, 10);
        dock.Children.Add(bs);
        dock.Children.Add(new ScrollViewer { Content = g, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        w.Content = dock;
        w.ShowDialog();
        return ok;
    }
}
