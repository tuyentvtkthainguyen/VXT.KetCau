using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Dàn trang ↔ mẫu định dạng: "Lưu tờ làm mẫu", "Tạo tờ từ mẫu" (điền ô khung nhìn bằng hộp chọn nhanh),
/// khung tên người dùng trong hộp thoại "Khung tên công trình…" (tải từ DWG/DXF/ảnh, dùng khung tên mặc định, lưu vào thư viện mẫu).</summary>
public partial class SheetWindow
{
    // ==================================================================
    // Tờ mẫu
    // ==================================================================
    private void BtnSaveSheetTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_sheet == null) { SetStatus("Chưa chọn tờ."); return; }
        string name = DialogUi.InputBox(this, "Lưu tờ làm mẫu", "Tên tờ mẫu (lưu vào gói \"" + TemplateService.SheetLibraryName + "\" của thư viện mẫu):",
            string.IsNullOrWhiteSpace(_sheet.Name) ? _sheet.Number : _sheet.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            string path = SaveSheetAsTemplate(name);
            SetStatus($"Đã lưu tờ mẫu \"{name}\" ({_sheet.Paper}, {_sheet.Views.Count} ô khung nhìn, {_sheet.Items.Count} đối tượng) vào {System.IO.Path.GetFileName(path)}.");
        }
        catch (Exception ex) { MessageBox.Show(this, "Không lưu được tờ mẫu: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    /// <summary>Lưu tờ hiện tại làm tờ mẫu trong thư viện; trả đường dẫn gói.</summary>
    internal string SaveSheetAsTemplate(string name) => TemplateService.AddSheetTemplateToLibrary(_sheet, name);

    private void BtnSheetFromTemplate_Click(object sender, RoutedEventArgs e)
    {
        var all = TemplateService.LibrarySheetTemplates();
        if (all.Count == 0)
        {
            MessageBox.Show(this, "Thư viện chưa có tờ mẫu nào.\nDàn một tờ rồi bấm \"Lưu tờ làm mẫu\", hoặc nhập gói mẫu ở Tệp → Mẫu định dạng (template)…", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var w = new Window { Owner = this };
        DialogUi.Setup(w, "Tạo tờ từ mẫu", 760, 520);
        var root = new DockPanel { Margin = new Thickness(10) };
        var list = new ListBox { Width = 300, Margin = new Thickness(0, 0, 8, 0) };
        foreach (var (pkg, t) in all)
            list.Items.Add(new ListBoxItem { Content = $"{t.Name}  [{t.Layout.Paper}{(t.Layout.Landscape ? "" : " dọc")}, {t.Layout.Views.Count} ô]  – {pkg.Name}", Tag = t });
        DockPanel.SetDock(list, Dock.Left);
        var slots = new StackPanel();
        var combos = new List<ComboBox>();
        void ShowSlots()
        {
            slots.Children.Clear();
            combos.Clear();
            if ((list.SelectedItem as ListBoxItem)?.Tag is not SheetTemplate t) return;
            slots.Children.Add(DialogUi.Label("Ô khung nhìn → nội dung:", double.NaN, true));
            var defs = TemplateService.DefaultSlotParams(_p, _d, t);
            for (int i = 0; i < t.Layout.Views.Count; i++)
            {
                var v = t.Layout.Views[i];
                var cb = new ComboBox { Width = 230, IsEditable = true };
                var opts = SheetBuilder.ParamOptions(_p, _d, v.Kind);
                foreach (var o in opts) cb.Items.Add(o);
                cb.Text = i < defs.Count ? defs[i] ?? "" : "";
                if (v.Kind == SheetViewKind.Notes) { cb.IsEnabled = false; cb.Text = "(giữ ghi chú của mẫu)"; }
                combos.Add(cb);
                string sc = v.Scale > 0 ? $"1/{v.Scale:0}" : "vừa khung";
                slots.Children.Add(DialogUi.Row(DialogUi.Label($"{i + 1}. {SheetBuilder.KindName(v.Kind)} ({sc})", 260), cb));
            }
            if (t.Layout.Views.Count == 0) slots.Children.Add(DialogUi.Hint("Tờ mẫu không có ô khung nhìn – chỉ có khung, chữ, logo, bảng…"));
            if (!string.IsNullOrWhiteSpace(t.Description)) slots.Children.Add(DialogUi.Hint(t.Description));
        }
        list.SelectionChanged += (s, e2) => ShowSlots();
        root.Children.Add(list);
        bool ok = false;
        var bOk = DialogUi.Btn("Tạo tờ", (s, e2) => { ok = true; w.DialogResult = true; }, true);
        bOk.IsDefault = true;
        var bCancel = DialogUi.Btn("Huỷ", (s, e2) => w.DialogResult = false);
        bCancel.IsCancel = true;
        var bs = DialogUi.Row(bOk, bCancel); bs.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(bs, Dock.Bottom);
        root.Children.Add(bs);
        root.Children.Add(new ScrollViewer { Content = slots, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        w.Content = root;
        list.SelectedIndex = 0;
        if (w.ShowDialog() != true || !ok) return;
        if ((list.SelectedItem as ListBoxItem)?.Tag is not SheetTemplate chosen) return;
        var pars = new List<string>();
        for (int i = 0; i < chosen.Layout.Views.Count; i++)
            pars.Add(chosen.Layout.Views[i].Kind == SheetViewKind.Notes || i >= combos.Count ? null : (combos[i].Text ?? "").Trim());
        var sh = AddSheetFromTemplate(chosen, pars);
        SetStatus($"Đã tạo tờ {sh.Number} từ tờ mẫu \"{chosen.Name}\".");
    }

    /// <summary>Thêm tờ mới từ tờ mẫu (ghi hoàn tác) sau tờ hiện tại và chọn nó.</summary>
    internal Sheet AddSheetFromTemplate(SheetTemplate t, IList<string> slotParams = null)
    {
        var s = TemplateService.SheetFromTemplate(_p, t, NextNumber(), slotParams ?? TemplateService.DefaultSlotParams(_p, _d, t));
        _figCache.Clear();
        InsertSheet(s);
        return s;
    }

    // ==================================================================
    // Khung tên người dùng trong hộp thoại "Khung tên công trình…"
    // ==================================================================
    /// <summary>Nhóm điều khiển khung tên người dùng. get/set: khung tên đang chọn trong hộp thoại (chỉ ghi vào dự án khi bấm Đồng ý);
    /// info: thông tin khung tên đang nhập trên hộp thoại (để lưu vào thư viện mẫu).</summary>
    private UIElement BuildCustomTitlePanel(Window dlg, Func<TitleBlockDef> get, Action<TitleBlockDef> set, Func<TitleBlockInfo> info)
    {
        var gb = new GroupBox { Header = "Khung tên người dùng (tải từ DWG / DXF / ảnh)", Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(6) };
        var sp = new StackPanel();
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        void Show()
        {
            var d = get();
            if (d == null) { status.Text = "Đang dùng khung tên mặc định của chương trình."; status.Foreground = Brushes.DimGray; return; }
            status.Foreground = Brushes.DarkGreen;
            status.Text = $"Đang dùng: {d.Name} ({(d.Kind == TitleBlockKind.Image ? "ảnh" : "vector")}, {d.W:0.#}×{d.H:0.#} mm, " +
                          $"{(d.Mode == TitleBlockMode.WholeFrame ? "khung bao toàn tờ" : "ô khung tên góc phải dưới")}" +
                          (string.IsNullOrEmpty(d.Source) ? "" : $", từ {d.Source}") + ")\nTrường: " + string.Join(", ", d.UsedFields().Select(TitleFields.Label));
        }
        Show();
        sp.Children.Add(status);
        var wp = new WrapPanel();
        wp.Children.Add(DialogUi.Btn("Tải khung tên từ file (DWG/DXF/ảnh)…", (s, e) =>
        {
            var w = new TitleBlockDialog(_p, _sheet) { Owner = dlg };
            if (w.ShowDialog() == true && w.Result != null) { set(w.Result); Show(); }
        }, true));
        wp.Children.Add(DialogUi.Btn("Sửa trường…", (s, e) =>
        {
            if (get() == null) return;
            var w = new TitleBlockDialog(_p, _sheet, get()) { Owner = dlg };
            if (w.ShowDialog() == true && w.Result != null) { set(w.Result); Show(); }
        }));
        wp.Children.Add(DialogUi.Btn("Dùng khung tên mặc định của chương trình", (s, e) => { set(null); Show(); }));
        wp.Children.Add(DialogUi.Btn("Lưu vào thư viện mẫu", (s, e) =>
        {
            var d = get();
            string name = DialogUi.InputBox(dlg, "Lưu vào thư viện mẫu", "Tên mẫu (gồm khung tên + thông tin khung tên):", d != null ? "Khung tên " + d.Name : "Thông tin khung tên");
            if (string.IsNullOrWhiteSpace(name)) return;
            try
            {
                var tb = info();
                tb.ProjectName = ""; tb.Location = ""; tb.Owner = ""; tb.Custom = null;
                var pkg = new TemplatePackage { Name = name, Description = "Lưu từ hộp thoại Khung tên công trình", TitleBlock = tb, TitleBlockDef = d, Author = tb.Company ?? "" };
                string path = TemplateService.SaveToLibrary(pkg);
                MessageBox.Show(dlg, "Đã lưu vào thư viện mẫu:\n" + path, Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show(dlg, "Không lưu được: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        }));
        sp.Children.Add(wp);
        gb.Content = sp;
        return gb;
    }

    /// <summary>Trường tự đặt (TitleBlockInfo.Extra) ↔ chữ nhiều dòng "KHOA = giá trị".</summary>
    private static string ExtraToText(Dictionary<string, string> extra) =>
        extra == null ? "" : string.Join("\n", extra.Select(kv => $"{kv.Key} = {kv.Value}"));

    private static Dictionary<string, string> ExtraFromText(string text)
    {
        var d = new Dictionary<string, string>();
        foreach (var line in (text ?? "").Replace("\r", "").Split('\n'))
        {
            int i = line.IndexOf('=');
            if (i <= 0) continue;
            string k = TitleFields.KeyOf(line[..i].Trim().Trim('{', '}'));
            if (k.Length > 0) d[k] = line[(i + 1)..].Trim();
        }
        return d;
    }

    /// <summary>Đặt khung tên người dùng bằng mã (ghi hoàn tác) – tự kiểm tra.</summary>
    internal void SetCustomTitle(TitleBlockDef def)
    {
        Record();
        _p.TitleBlock ??= new TitleBlockInfo();
        _p.TitleBlock.Custom = def;
        RebuildTitleBlock();
        Dirty();
    }
}
