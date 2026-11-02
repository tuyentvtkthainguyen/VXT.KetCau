using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Cửa sổ "Mẫu định dạng (template)": thư viện mẫu của người dùng (%AppData%\KetCauVXT\Templates) –
/// tạo từ dự án hiện tại (chọn phần), áp dụng vào dự án (gộp / thay thế), đặt làm mặc định cho dự án mới, nhập / xuất file, đổi tên, xoá.</summary>
internal sealed class TemplateWindow : Window
{
    private readonly Project _p;
    private readonly DesignSummary _d;
    private readonly Action<TemplateParts> _applied;
    private readonly DataGrid _grid;
    private readonly TextBlock _info;
    private readonly TextBlock _status;
    private List<TemplateLibraryEntry> _items = new();

    /// <summary>Đã áp dụng mẫu vào dự án (ít nhất một lần).</summary>
    public bool Changed { get; private set; }

    public TemplateWindow(Project p, DesignSummary d, Action<TemplateParts> applied)
    {
        _p = p ?? throw new ArgumentNullException(nameof(p));
        _d = d;
        _applied = applied;
        DialogUi.Setup(this, "Mẫu định dạng (template) – thư viện mẫu của người dùng", 980, 600);

        var root = new DockPanel { Margin = new Thickness(10) };
        var head = new StackPanel();
        head.Children.Add(DialogUi.Label("Mẫu định dạng chuẩn gồm tuỳ ý các phần: chuẩn CAD (layer, font, kiểu bản vẽ), khung tên người dùng, tờ mẫu, " +
                                          "thông tin khung tên, tuỳ chọn thuyết minh, định mức khối lượng, tuỳ chọn thép. Lưu thành file *.kcvxt-mau để dùng lại / gửi cho đồng nghiệp."));
        head.Children.Add(DialogUi.Hint("Thư mục thư viện: " + TemplateService.LibraryFolder));
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);

        var bar = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        bar.Children.Add(DialogUi.Btn("Tạo từ dự án hiện tại…", (s, e) => CreateFromProjectDialog(), true));
        bar.Children.Add(DialogUi.Btn("Áp dụng vào dự án…", (s, e) => ApplyDialog(), true));
        bar.Children.Add(DialogUi.Btn("Đặt làm mặc định", (s, e) => SetDefaultSelected(true)));
        bar.Children.Add(DialogUi.Btn("Bỏ mặc định", (s, e) => SetDefaultSelected(false)));
        bar.Children.Add(DialogUi.Btn("Nhập file…", (s, e) => ImportFile()));
        bar.Children.Add(DialogUi.Btn("Xuất file…", (s, e) => ExportFile()));
        bar.Children.Add(DialogUi.Btn("Đổi tên…", (s, e) => RenameSelected()));
        bar.Children.Add(DialogUi.Btn("Xoá", (s, e) => DeleteSelected()));
        bar.Children.Add(DialogUi.Btn("Mở thư mục", (s, e) => OpenFolder()));
        var close = DialogUi.Btn("Đóng", (s, e) => Close());
        close.IsCancel = true;
        bar.Children.Add(close);
        DockPanel.SetDock(bar, Dock.Bottom);
        root.Children.Add(bar);

        _status = new TextBlock { Margin = new Thickness(0, 4, 0, 0), Foreground = Brushes.DarkSlateGray, TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(_status);

        _info = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), MinHeight = 40 };
        DockPanel.SetDock(_info, Dock.Bottom);
        root.Children.Add(_info);

        _grid = DialogUi.NewGrid();
        _grid.IsReadOnly = true;
        _grid.SelectionMode = DataGridSelectionMode.Single;
        _grid.Columns.Add(DialogUi.Text("Mặc định", nameof(TemplateRow.DefaultMark), true, 70));
        _grid.Columns.Add(DialogUi.Text("Tên mẫu", nameof(TemplateRow.Name), true, 230));
        _grid.Columns.Add(DialogUi.Text("Mô tả", nameof(TemplateRow.Description), true, 230));
        _grid.Columns.Add(DialogUi.Text("Các phần", nameof(TemplateRow.PartsText), true, 290));
        _grid.Columns.Add(DialogUi.Text("Ngày sửa", nameof(TemplateRow.Modified), true, 120));
        _grid.SelectionChanged += (s, e) => ShowInfo();
        _grid.MouseDoubleClick += (s, e) => { if (Selected != null) ApplyDialog(); };
        root.Children.Add(_grid);
        Content = root;
        Reload();
    }

    internal sealed class TemplateRow
    {
        public TemplateLibraryEntry E { get; init; }
        public string DefaultMark => E.IsDefault ? "✔" : "";
        public string Name => E.Name;
        public string Description => E.Description;
        public string PartsText => E.PartsText + (E.SheetTemplateCount > 0 ? $" ({E.SheetTemplateCount} tờ mẫu)" : "");
        public string Modified => E.Modified;
    }

    private TemplateLibraryEntry Selected => (_grid.SelectedItem as TemplateRow)?.E;

    private void SetStatus(string s) => _status.Text = s;

    /// <summary>Đọc lại thư viện; chọn mẫu theo đường dẫn (nếu có).</summary>
    internal void Reload(string select = null)
    {
        string keep = select ?? Selected?.Path;
        _items = TemplateService.List();
        var rows = _items.Select(x => new TemplateRow { E = x }).ToList();
        _grid.ItemsSource = rows;
        var sel = rows.FirstOrDefault(r => string.Equals(r.E.Path, keep, StringComparison.OrdinalIgnoreCase)) ?? rows.FirstOrDefault();
        _grid.SelectedItem = sel;
        if (rows.Count == 0) _info.Text = "Thư viện chưa có mẫu nào – bấm \"Tạo từ dự án hiện tại…\" hoặc \"Nhập file…\".";
        SetStatus($"{rows.Count} mẫu trong thư viện." + (TemplateService.DefaultTemplatePath != null ? " Mẫu mặc định: " + Path.GetFileNameWithoutExtension(TemplateService.DefaultTemplatePath) + " (áp dụng khi tạo dự án mới)." : " Chưa đặt mẫu mặc định."));
    }

    private void ShowInfo()
    {
        var e = Selected;
        if (e == null) { _info.Text = ""; return; }
        try
        {
            var t = TemplateService.Load(e.Path);
            var lines = new List<string> { $"{t.Name}" + (string.IsNullOrEmpty(t.Author) ? "" : $" – {t.Author}") + (string.IsNullOrEmpty(t.Created) ? "" : $" – tạo {t.Created}") };
            foreach (var part in TemplateService.PartList)
            {
                if (!t.Parts.HasFlag(part)) continue;
                string extra = part switch
                {
                    TemplateParts.CadStandard => $": {t.CadStandard.Name}, font {t.CadStandard.FontName}, {t.CadStandard.OutputLayers?.Count ?? 0} layer",
                    TemplateParts.TitleBlockDef => $": {t.TitleBlockDef.Name} ({(t.TitleBlockDef.Kind == TitleBlockKind.Image ? "ảnh" : "vector")}, {t.TitleBlockDef.W:0}×{t.TitleBlockDef.H:0} mm, {(t.TitleBlockDef.Mode == TitleBlockMode.WholeFrame ? "khung bao toàn tờ" : "ô khung tên")})",
                    TemplateParts.SheetTemplates => ": " + string.Join(", ", t.SheetTemplates.Select(x => $"{x.Name} [{x.Layout.Paper}, {x.Layout.Views.Count} ô]")),
                    TemplateParts.TitleBlockInfo => $": {t.TitleBlock.Company}" + (string.IsNullOrEmpty(t.TitleBlock.Approver) ? "" : $", duyệt {t.TitleBlock.Approver}"),
                    _ => ""
                };
                lines.Add(" • " + TemplateService.PartName(part) + extra);
            }
            if (!string.IsNullOrWhiteSpace(t.Description)) lines.Add(t.Description);
            _info.Text = string.Join("\n", lines);
        }
        catch (Exception ex) { _info.Text = "Không đọc được mẫu: " + ex.Message; }
    }

    // ==================================================================
    // Tạo từ dự án
    // ==================================================================
    /// <summary>Tạo mẫu từ dự án hiện tại (không hỏi) vào thư viện; trả đường dẫn.</summary>
    internal string CreateFromProject(string name, TemplateParts parts, string description = "")
    {
        var t = TemplateService.FromProject(_p, parts, name, description);
        string path = TemplateService.SaveToLibrary(t);
        Reload(path);
        SetStatus($"Đã lưu mẫu \"{t.Name}\" ({TemplateService.PartsText(t.Parts)}) vào thư viện.");
        return path;
    }

    private void CreateFromProjectDialog()
    {
        var w = new Window { Owner = this, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
        DialogUi.Setup(w, "Tạo mẫu từ dự án hiện tại", 520, 420);
        w.MinHeight = 0;
        var sp = new StackPanel { Margin = new Thickness(10) };
        var name = new TextBox { Text = string.IsNullOrWhiteSpace(_p.TitleBlock?.Company) ? "Mẫu " + _p.Name : "Mẫu " + _p.TitleBlock.Company };
        var desc = new TextBox { Text = "", Height = 44, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        sp.Children.Add(DialogUi.Label("Tên mẫu"));
        sp.Children.Add(name);
        sp.Children.Add(DialogUi.Label("Mô tả"));
        sp.Children.Add(desc);
        sp.Children.Add(DialogUi.Label("Các phần đưa vào mẫu:", double.NaN, true));
        var checks = new Dictionary<TemplateParts, CheckBox>();
        foreach (var part in TemplateService.PartList)
        {
            bool avail = part switch
            {
                TemplateParts.TitleBlockDef => _p.TitleBlock?.Custom != null,
                TemplateParts.SheetTemplates => _p.Sheets != null && _p.Sheets.Count > 0,
                _ => true
            };
            string extra = part switch
            {
                TemplateParts.TitleBlockDef when !avail => " – dự án chưa có khung tên người dùng",
                TemplateParts.SheetTemplates => avail ? $" – {_p.Sheets.Count} tờ (bỏ tham số khung nhìn)" : " – dự án chưa có tờ",
                TemplateParts.TitleBlockInfo => " – không gồm tên/địa điểm/chủ đầu tư",
                _ => ""
            };
            var cb = new CheckBox { Content = TemplateService.PartName(part) + extra, IsEnabled = avail, IsChecked = avail && part != TemplateParts.SheetTemplates, Margin = new Thickness(4, 2, 0, 2) };
            checks[part] = cb;
            sp.Children.Add(cb);
        }
        bool ok = false;
        var bOk = DialogUi.Btn("Lưu vào thư viện", (s, e) => { ok = true; w.DialogResult = true; }, true);
        bOk.IsDefault = true;
        var bCancel = DialogUi.Btn("Huỷ", (s, e) => w.DialogResult = false);
        bCancel.IsCancel = true;
        var bs = DialogUi.Row(bOk, bCancel); bs.HorizontalAlignment = HorizontalAlignment.Right; bs.Margin = new Thickness(0, 8, 0, 0);
        sp.Children.Add(bs);
        w.Content = sp;
        if (w.ShowDialog() != true || !ok) return;
        var parts = TemplateParts.None;
        foreach (var kv in checks) if (kv.Value.IsChecked == true) parts |= kv.Key;
        if (parts == TemplateParts.None) { SetStatus("Chưa chọn phần nào – không tạo mẫu."); return; }
        try { CreateFromProject(name.Text, parts, desc.Text.Trim()); }
        catch (Exception ex) { MessageBox.Show(this, "Không lưu được mẫu: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    // ==================================================================
    // Áp dụng
    // ==================================================================
    /// <summary>Áp dụng mẫu (không hỏi). Trả các phần đã áp dụng.</summary>
    internal TemplateParts ApplyPath(string path, TemplateParts parts, TemplateApplyMode mode)
    {
        var t = TemplateService.Load(path);
        var done = TemplateService.ApplyTo(_p, t, parts, mode, _d);
        if (done != TemplateParts.None) { Changed = true; _applied?.Invoke(done); }
        SetStatus(done == TemplateParts.None ? "Không có phần nào được áp dụng." : $"Đã áp dụng \"{t.Name}\" ({(mode == TemplateApplyMode.Merge ? "gộp" : "thay thế")}): {TemplateService.PartsText(done)}.");
        return done;
    }

    private void ApplyDialog()
    {
        var e = Selected;
        if (e == null) { SetStatus("Chọn một mẫu trong danh sách."); return; }
        TemplatePackage t;
        try { t = TemplateService.Load(e.Path); }
        catch (Exception ex) { MessageBox.Show(this, "Không đọc được mẫu: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); return; }
        var w = new Window { Owner = this, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
        DialogUi.Setup(w, "Áp dụng mẫu: " + t.Name, 540, 420);
        w.MinHeight = 0;
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(DialogUi.Label("Các phần áp dụng vào dự án \"" + _p.Name + "\":", double.NaN, true));
        var checks = new Dictionary<TemplateParts, CheckBox>();
        foreach (var part in TemplateService.PartList)
        {
            if (!t.Parts.HasFlag(part)) continue;
            string extra = part == TemplateParts.SheetTemplates ? $" – tạo {t.SheetTemplates.Count} tờ từ tờ mẫu" : "";
            var cb = new CheckBox { Content = TemplateService.PartName(part) + extra, IsChecked = part != TemplateParts.SheetTemplates, Margin = new Thickness(4, 2, 0, 2) };
            checks[part] = cb;
            sp.Children.Add(cb);
        }
        var rMerge = new RadioButton { Content = "Gộp: chỉ ghi đè ô có giá trị trong mẫu, giữ phần còn lại của dự án; tờ mẫu thêm vào cuối", IsChecked = true, Margin = new Thickness(0, 8, 0, 2) };
        var rReplace = new RadioButton { Content = "Thay thế: phần đã chọn thay toàn bộ (tờ mẫu thay toàn bộ danh sách tờ!)", Margin = new Thickness(0, 2, 0, 2) };
        sp.Children.Add(rMerge);
        sp.Children.Add(rReplace);
        sp.Children.Add(DialogUi.Hint("Tên công trình, địa điểm, chủ đầu tư của dự án luôn được giữ."));
        bool ok = false;
        var bOk = DialogUi.Btn("Áp dụng", (s, e2) => { ok = true; w.DialogResult = true; }, true);
        bOk.IsDefault = true;
        var bCancel = DialogUi.Btn("Huỷ", (s, e2) => w.DialogResult = false);
        bCancel.IsCancel = true;
        var bs = DialogUi.Row(bOk, bCancel); bs.HorizontalAlignment = HorizontalAlignment.Right; bs.Margin = new Thickness(0, 8, 0, 0);
        sp.Children.Add(bs);
        w.Content = sp;
        if (w.ShowDialog() != true || !ok) return;
        var parts = TemplateParts.None;
        foreach (var kv in checks) if (kv.Value.IsChecked == true) parts |= kv.Key;
        var mode = rReplace.IsChecked == true ? TemplateApplyMode.Replace : TemplateApplyMode.Merge;
        if (mode == TemplateApplyMode.Replace && parts.HasFlag(TemplateParts.SheetTemplates) && _p.Sheets.Count > 0 &&
            MessageBox.Show(this, $"Thay thế sẽ xoá {_p.Sheets.Count} tờ hiện có của dự án và tạo lại từ tờ mẫu. Tiếp tục?", Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;
        try { ApplyPath(e.Path, parts, mode); }
        catch (Exception ex) { MessageBox.Show(this, "Không áp dụng được mẫu: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    // ==================================================================
    // Mặc định, nhập/xuất, đổi tên, xoá
    // ==================================================================
    private void SetDefaultSelected(bool on)
    {
        var e = Selected;
        if (on && e == null) { SetStatus("Chọn một mẫu trong danh sách."); return; }
        string path = TemplateService.SetDefault(on ? e.Path : null);
        Reload(path ?? e?.Path);
        SetStatus(on ? $"Đã đặt \"{e.Name}\" làm mẫu mặc định – áp dụng (gộp, không tạo tờ) mỗi khi tạo dự án mới (Tệp → Mới)." : "Đã bỏ mẫu mặc định.");
    }

    private void ImportFile()
    {
        var dlg = new OpenFileDialog { Filter = TemplateService.FileFilter, Title = "Nhập mẫu định dạng vào thư viện", Multiselect = true };
        if (dlg.ShowDialog(this) != true) return;
        string last = null;
        var errs = new List<string>();
        foreach (var f in dlg.FileNames)
        {
            try { last = TemplateService.Import(f); }
            catch (Exception ex) { errs.Add(Path.GetFileName(f) + ": " + ex.Message); }
        }
        Reload(last);
        if (errs.Count > 0) MessageBox.Show(this, "Không nhập được:\n" + string.Join("\n", errs), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        else SetStatus($"Đã nhập {dlg.FileNames.Length} mẫu vào thư viện.");
    }

    private void ExportFile()
    {
        var e = Selected;
        if (e == null) { SetStatus("Chọn một mẫu trong danh sách."); return; }
        var dlg = new SaveFileDialog { Filter = TemplateService.FileFilter, FileName = TemplateService.SafeFileName(e.Name) + TemplateService.Extension, Title = "Xuất mẫu định dạng" };
        if (dlg.ShowDialog(this) != true) return;
        try { TemplateService.Export(e.Path, dlg.FileName); SetStatus("Đã xuất " + dlg.FileName); }
        catch (Exception ex) { MessageBox.Show(this, "Không xuất được: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void RenameSelected()
    {
        var e = Selected;
        if (e == null) { SetStatus("Chọn một mẫu trong danh sách."); return; }
        string name = DialogUi.InputBox(this, "Đổi tên mẫu", "Tên mới:", e.Name);
        if (string.IsNullOrWhiteSpace(name) || name == e.Name) return;
        try { Reload(TemplateService.Rename(e.Path, name)); SetStatus($"Đã đổi tên thành \"{name}\"."); }
        catch (Exception ex) { MessageBox.Show(this, "Không đổi tên được: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void DeleteSelected()
    {
        var e = Selected;
        if (e == null) return;
        if (MessageBox.Show(this, $"Xoá mẫu \"{e.Name}\" khỏi thư viện?", Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        bool ok = TemplateService.Delete(e.Path);
        Reload();
        SetStatus(ok ? $"Đã xoá \"{e.Name}\"." : "Không xoá được file mẫu.");
    }

    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(TemplateService.LibraryFolder);
            Process.Start(new ProcessStartInfo { FileName = TemplateService.LibraryFolder, UseShellExecute = true });
        }
        catch (Exception ex) { SetStatus("Không mở được thư mục: " + ex.Message); }
    }
}
