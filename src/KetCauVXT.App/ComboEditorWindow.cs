using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Dòng danh sách tổ hợp.</summary>
internal sealed class ComboListRow
{
    public LoadCombination C { get; init; }
    public string Name => C.Name;
    public string Type => ComboEditorWindow.TypeName(C.Type);
    public string Description => ComboGenerator.Describe(C);
}

/// <summary>Dòng thành phần tổ hợp (trường hợp tải / tổ hợp × hệ số).</summary>
internal sealed class ComboItemRow : RowBase
{
    private string _case = "";
    private double _factor = 1;
    public string Case { get => _case; set { _case = value ?? ""; Raise(); } }
    public double Factor { get => _factor; set { _factor = value; Raise(); } }
}

/// <summary>Khai báo → Tổ hợp tải trọng… (Define → Load Combinations của ETABS), mở không chặn: danh sách tổ hợp; sửa tên, loại
/// (TTGH I / TTGH II / Bao), thành phần (trường hợp tải hoặc tổ hợp khác × hệ số); thêm / sao chép / xoá; tự sinh theo TCVN 2737:2023
/// (giữ hoặc bỏ tổ hợp tự đặt). Mọi thao tác qua <see cref="ComboEditor"/>, báo cửa sổ chính để lưu hoàn tác.</summary>
internal sealed class ComboEditorWindow : Window
{
    private readonly Func<Project> _getP;
    private readonly Func<string> _snapshot;
    private readonly Action<string, string> _commit;
    private readonly DataGrid _list = DialogUi.NewGrid();
    private readonly DataGrid _items = DialogUi.NewGrid();
    private readonly TextBox _name = new() { Width = 160 };
    private readonly ComboBox _type = new() { Width = 120 };
    private readonly TextBlock _status = new() { Foreground = Brushes.DarkSlateBlue, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _desc = new() { Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
    private readonly CheckBox _keepUser = new() { Content = "giữ tổ hợp tự đặt", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
    private readonly DataGridComboBoxColumn _caseCol;
    private ObservableCollection<ComboItemRow> _rows = new();
    private string _editing;   // tên tổ hợp đang sửa; null = tổ hợp mới
    private bool _reloading;

    internal static readonly ComboType[] Types = { ComboType.ULS, ComboType.SLS, ComboType.Envelope };
    internal static string TypeName(ComboType t) => t switch { ComboType.ULS => "TTGH I", ComboType.SLS => "TTGH II", _ => "Bao" };

    private Project P => _getP();

    public ComboEditorWindow(Func<Project> getP, Func<string> snapshot, Action<string, string> commit)
    {
        _getP = getP; _snapshot = snapshot; _commit = commit;
        DialogUi.Setup(this, "Tổ hợp tải trọng (Load Combinations)", 1080, 600);
        _list.IsReadOnly = true;
        _list.SelectionMode = DataGridSelectionMode.Single;
        _list.Columns.Add(DialogUi.Text("Tên", "Name", true, 80));
        _list.Columns.Add(DialogUi.Text("Loại", "Type", true, 64));
        _list.Columns.Add(DialogUi.Text("Thành phần", "Description", true, 330));
        _list.SelectionChanged += (s, e) => { if (!_reloading && _list.SelectedItem is ComboListRow r) Edit(r.C); };

        _type.ItemsSource = Types.Select(TypeName).ToList();
        _type.SelectedIndex = 0;
        _caseCol = DialogUi.Combo("Trường hợp / tổ hợp", "Case", new List<string>(), 170);
        _items.Columns.Add(_caseCol);
        _items.Columns.Add(DialogUi.Num("Hệ số", "Factor", "0.####", false, 80));
        _items.CellEditEnding += (s, e) => Dispatcher.BeginInvoke(new Action(UpdateDesc), System.Windows.Threading.DispatcherPriority.Background);

        // ---- trái: danh sách ----
        var left = new DockPanel { Margin = new Thickness(0, 0, 8, 0) };
        var lbar = DialogUi.Row(
            DialogUi.Btn("＋ Mới", (s, e) => New(), true),
            DialogUi.Btn("Sao chép", (s, e) => Copy()),
            DialogUi.Btn("Xoá", (s, e) => DeleteSel()));
        DockPanel.SetDock(lbar, Dock.Top); left.Children.Add(lbar);
        var gen = DialogUi.Row(DialogUi.Btn("Tự sinh TCVN 2737:2023", (s, e) => Generate()), _keepUser);
        DockPanel.SetDock(gen, Dock.Bottom); left.Children.Add(gen);
        left.Children.Add(_list);

        // ---- phải: sửa ----
        var right = new DockPanel();
        var head = new StackPanel();
        head.Children.Add(DialogUi.Row(DialogUi.Label("Tên tổ hợp", 90), _name, DialogUi.Label("  Loại"), _type));
        head.Children.Add(DialogUi.Row(
            DialogUi.Btn("＋ Dòng", (s, e) => { _rows.Add(new ComboItemRow { Case = P.Patterns.FirstOrDefault()?.Name ?? "", Factor = 1 }); UpdateDesc(); }),
            DialogUi.Btn("Xoá dòng", (s, e) => { foreach (var r in _items.SelectedItems.OfType<ComboItemRow>().ToList()) _rows.Remove(r); UpdateDesc(); }),
            DialogUi.Btn("Thêm mọi trường hợp tải", (s, e) => { foreach (var pt in P.Patterns.Where(x => _rows.All(r => r.Case != x.Name))) _rows.Add(new ComboItemRow { Case = pt.Name, Factor = pt.Gamma }); UpdateDesc(); })));
        DockPanel.SetDock(head, Dock.Top); right.Children.Add(head);
        var foot = new StackPanel();
        foot.Children.Add(_desc);
        foot.Children.Add(DialogUi.Row(DialogUi.Btn("Lưu tổ hợp", (s, e) => Save(), true), DialogUi.Btn("Bỏ sửa", (s, e) => { if (_editing != null) Edit(P.Combinations.FirstOrDefault(c => c.Name == _editing)); else New(); })));
        DockPanel.SetDock(foot, Dock.Bottom); right.Children.Add(foot);
        right.Children.Add(_items);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(left, 0); grid.Children.Add(left);
        Grid.SetColumn(right, 1); grid.Children.Add(right);

        var dock = new DockPanel { Margin = new Thickness(10) };
        var bottom = new StackPanel();
        bottom.Children.Add(DialogUi.Hint("TTGH I: tổ hợp tính toán (thiết kế cường độ); TTGH II: tổ hợp tiêu chuẩn (độ võng, nứt, lún); Bao: lấy max/min của các thành phần (tổ hợp bao không chứa được trong tổ hợp tuyến tính). "
                                          + "Thành phần có thể là trường hợp tải hoặc tổ hợp khác. Tự sinh: tổ hợp cơ bản 1, 2 và đặc biệt (động đất) theo TCVN 2737:2023 / TCVN 9386-1:2025."));
        bottom.Children.Add(_status);
        bottom.Children.Add(DialogUi.Row(DialogUi.Btn("Đóng", (s, e) => Close())));
        DockPanel.SetDock(bottom, Dock.Bottom); dock.Children.Add(bottom);
        dock.Children.Add(grid);
        Content = dock;
        Activated += (s, e) => { if (!_reloading) Reload(_editing); };
        string first = P?.Combinations.FirstOrDefault()?.Name;
        if (first != null) Reload(first); else { Reload(null); New(); }
    }

    /// <summary>Nạp lại danh sách (giữ tổ hợp đang chọn theo tên).</summary>
    public void Reload(string select)
    {
        var p = P;
        if (p == null) return;
        _reloading = true;
        try
        {
            DialogUi.Commit(_items);
            var rows = p.Combinations.Select(c => new ComboListRow { C = c }).ToList();
            _list.ItemsSource = rows;
            _caseCol.ItemsSource = p.Patterns.Select(x => x.Name).Concat(p.Combinations.Select(c => c.Name)).ToList();
            string want = select ?? _editing;
            var r = want != null ? rows.FirstOrDefault(x => x.Name == want) : null;
            if (r != null)
            {
                _list.SelectedItem = r; _list.ScrollIntoView(r);
                if (r.Name != _editing) Edit(r.C);
            }
            else if (want != null)
            {
                // tổ hợp đang sửa không còn (bị xoá / hoàn tác) → tổ hợp đầu tiên hoặc tổ hợp mới
                var f = rows.FirstOrDefault();
                if (f != null) { _list.SelectedItem = f; Edit(f.C); } else New();
            }
            // want == null: đang soạn tổ hợp mới – giữ nguyên nội dung đang soạn
        }
        finally { _reloading = false; }
    }

    /// <summary>Chọn tổ hợp theo tên (dùng cho tự kiểm tra).</summary>
    public void SelectCombo(string name)
    {
        var c = P.Combinations.FirstOrDefault(x => x.Name == name);
        if (c == null) return;
        Reload(name);
        Edit(c);
    }

    private void Edit(LoadCombination c)
    {
        if (c == null) { New(); return; }
        _editing = c.Name;
        _name.Text = c.Name;
        _type.SelectedIndex = Array.IndexOf(Types, c.Type);
        _rows = new ObservableCollection<ComboItemRow>(c.Items.Select(i => new ComboItemRow { Case = i.Case, Factor = i.Factor }));
        _items.ItemsSource = _rows;
        UpdateDesc();
    }

    private void New()
    {
        _editing = null;
        _name.Text = ComboEditor.UniqueName(P, "THM");
        _type.SelectedIndex = 0;
        _rows = new ObservableCollection<ComboItemRow>(P.Patterns.Where(x => x.Type is LoadType.Dead or LoadType.SuperDead or LoadType.Live)
                                                        .Select(x => new ComboItemRow { Case = x.Name, Factor = x.Gamma }));
        _items.ItemsSource = _rows;
        _list.SelectedItem = null;
        UpdateDesc();
        _status.Text = "Tổ hợp mới – sửa tên, loại, thành phần rồi bấm 'Lưu tổ hợp'.";
    }

    private List<ComboItem> Items() => _rows.Where(r => !string.IsNullOrWhiteSpace(r.Case)).Select(r => new ComboItem(r.Case, r.Factor)).ToList();

    private ComboType SelType() => Types[Math.Max(0, _type.SelectedIndex)];

    private void UpdateDesc()
    {
        var c = new LoadCombination { Name = _name.Text, Type = SelType(), Items = Items() };
        _desc.Text = $"{c.Name} = {ComboGenerator.Describe(c)}";
    }

    /// <summary>Chạy thao tác sửa có hoàn tác.</summary>
    private bool Do(Func<Project, EditResult> op, string select)
    {
        string snap = _snapshot();
        EditResult r;
        try { r = op(P); }
        catch (Exception ex) { _status.Text = "Lỗi: " + ex.Message; return false; }
        _status.Text = r.Message;
        if (!r.Ok) return false;
        _commit(snap, r.Message);
        Reload(select);
        return true;
    }

    private void Save()
    {
        DialogUi.Commit(_items);
        string name = _name.Text.Trim();
        var items = Items();
        var t = SelType();
        if (_editing == null) Do(p => ComboEditor.Add(p, name, t, items), name);
        else { string old = _editing; Do(p => ComboEditor.Update(p, old, name, t, items), name); }
    }

    private void Copy()
    {
        if (_editing == null) return;
        string src = _editing;
        string before = string.Join("|", P.Combinations.Select(c => c.Name));
        Do(p => ComboEditor.Duplicate(p, src), null);
        var added = P.Combinations.Select(c => c.Name).FirstOrDefault(n => !before.Split('|').Contains(n));
        if (added != null) SelectCombo(added);
    }

    private void DeleteSel()
    {
        if (_editing == null) return;
        string n = _editing;
        if (MessageBox.Show(this, $"Xoá tổ hợp {n}?", Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        if (Do(p => ComboEditor.Delete(p, n), "\0"))
        {
            string first = P.Combinations.FirstOrDefault()?.Name;
            if (first != null) SelectCombo(first); else New();
        }
    }

    private void Generate()
    {
        bool keep = _keepUser.IsChecked == true;
        if (MessageBox.Show(this, keep ? "Tạo lại các tổ hợp TH…, TC…, BAO theo TCVN 2737:2023 (giữ tổ hợp tự đặt)?" : "Xoá mọi tổ hợp và tự sinh lại theo TCVN 2737:2023?",
                Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        Do(p => { int n = ComboEditor.Generate(p, keep); return new EditResult { Ok = true, Message = $"Đã tự sinh {n} tổ hợp theo TCVN 2737:2023" + (keep ? " (giữ tổ hợp tự đặt)." : ".") }; }, "BAO");
    }
}
