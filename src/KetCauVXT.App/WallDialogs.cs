using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using KetCauVXT.Model;

namespace KetCauVXT.App;

// =====================================================================
// HỘP THOẠI TƯỜNG: thư viện "Loại tường" và "Danh sách tường đã khai báo" (menu Mô hình).
// Sửa trực tiếp trên Project; MainWindow chụp ảnh hoàn tác trước khi mở và làm mới sau khi đóng nếu Changed.
// =====================================================================

/// <summary>Một dòng bảng loại tường.</summary>
internal sealed class WallTypeRow : RowBase
{
    private readonly Project _p;
    private readonly Action _changed;
    public WallType T { get; }

    public WallTypeRow(Project p, WallType t, Action changed) { _p = p; T = t; _changed = changed; }

    private void Done() { RaiseAll(); _changed(); }

    public string Name
    {
        get => T.Name;
        set
        {
            var n = (value ?? "").Trim();
            if (n.Length == 0 || n == T.Name) return;
            if (_p.WallTypes.Any(q => q != T && q.Name == n))
            {
                MessageBox.Show($"Đã có loại tường tên \"{n}\".", "Loại tường", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            foreach (var w in _p.Walls.Where(w => w.Type == T.Name)) w.Type = n; // đổi tên theo
            T.Name = n;
            Done();
        }
    }

    public double ThicknessMm { get => T.Thickness * 1000; set { if (value >= 20 && value <= 1000) { T.Thickness = value / 1000; Done(); } } }
    public double UnitWeight { get => T.UnitWeight; set { if (value > 0 && value <= 30) { T.UnitWeight = value; Done(); } } }
    public string Finish { get => WallMath.FinishText(T.Finish); set { T.Finish = WallMath.ParseFinish(value); Done(); } }
    public double PlasterMm { get => T.PlasterThickness * 1000; set { if (value >= 0 && value <= 60) { T.PlasterThickness = value / 1000; Done(); } } }
    public double PlasterUnitWeight { get => T.PlasterUnitWeight; set { if (value > 0 && value <= 30) { T.PlasterUnitWeight = value; Done(); } } }
    public double OpeningPct { get => T.OpeningRatio * 100; set { if (value >= 0 && value <= 90) { T.OpeningRatio = value / 100; Done(); } } }
    public double Load => Math.Round(T.WeightPerM2, 3);
    public int Uses => _p.Walls.Count(w => w.Type == T.Name);
}

/// <summary>Quản lý thư viện loại tường (Project.WallTypes).</summary>
internal sealed class WallTypesWindow : Window
{
    private readonly Project _p;
    private readonly DataGrid _grid;
    private readonly ObservableCollection<WallTypeRow> _rows = new();
    public bool Changed { get; private set; }

    public WallTypesWindow(Project p)
    {
        _p = p;
        DialogUi.Setup(this, "Loại tường", 980, 520);
        _grid = DialogUi.NewGrid();
        _grid.Columns.Add(DialogUi.Text("Tên loại tường", nameof(WallTypeRow.Name), false, 210));
        _grid.Columns.Add(DialogUi.Num("Dày (mm)", nameof(WallTypeRow.ThicknessMm), "0"));
        _grid.Columns.Add(DialogUi.Num("γ xây (kN/m³)", nameof(WallTypeRow.UnitWeight), "0.##"));
        _grid.Columns.Add(DialogUi.Combo("Hoàn thiện", nameof(WallTypeRow.Finish), WallMath.FinishNames, 100));
        _grid.Columns.Add(DialogUi.Num("Trát mỗi mặt (mm)", nameof(WallTypeRow.PlasterMm), "0.#"));
        _grid.Columns.Add(DialogUi.Num("γ trát (kN/m³)", nameof(WallTypeRow.PlasterUnitWeight), "0.##"));
        _grid.Columns.Add(DialogUi.Num("Tỷ lệ cửa (%)", nameof(WallTypeRow.OpeningPct), "0.#"));
        _grid.Columns.Add(DialogUi.Num("Tải tường đặc (kN/m²)", nameof(WallTypeRow.Load), "0.000", true));
        _grid.Columns.Add(DialogUi.Num("Số đoạn dùng", nameof(WallTypeRow.Uses), "0", true));
        _grid.ItemsSource = _rows;
        Reload();

        var dock = new DockPanel { Margin = new Thickness(8) };
        var top = DialogUi.Row(
            DialogUi.Btn("＋ Thêm", (s, e) => Add(null)),
            DialogUi.Btn("Nhân bản", (s, e) => { var r = Current(); if (r != null) Add(r.T); }),
            DialogUi.Btn("Xoá", (s, e) => Delete()),
            DialogUi.Btn("Khôi phục thư viện mặc định", (s, e) => AddDefaults()));
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);
        var hint = DialogUi.Hint("Tải 1 m² tường đặc = dày × γ xây + số mặt trát × dày trát × γ trát. Tải lên dầm = tải 1 m² × chiều cao × (1 − tỷ lệ cửa). " +
                                 "Tường 110 vẽ nhạt, 220 trung bình, 330 đậm; tường không trát vẽ viền nét đứt. Loại tường đang dùng không xoá được.");
        DockPanel.SetDock(hint, Dock.Top);
        dock.Children.Add(hint);
        var close = DialogUi.Btn("Đóng", (s, e) => { DialogUi.Commit(_grid); Close(); }, true);
        close.IsCancel = true;
        var bottom = DialogUi.Row(close);
        bottom.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(bottom);
        dock.Children.Add(_grid);
        Content = dock;
        Closing += (s, e) => DialogUi.Commit(_grid);
    }

    private void MarkChanged() => Changed = true;

    private void Reload()
    {
        _rows.Clear();
        foreach (var t in _p.WallTypes) _rows.Add(new WallTypeRow(_p, t, MarkChanged));
    }

    private WallTypeRow Current() => _grid.SelectedItem as WallTypeRow;

    private string UniqueName(string b)
    {
        string n = b; int k = 2;
        while (_p.WallTypes.Any(t => t.Name == n)) n = $"{b} ({k++})";
        return n;
    }

    private void Add(WallType from)
    {
        DialogUi.Commit(_grid);
        var t = from == null
            ? new WallType { Name = UniqueName("Tường mới"), Thickness = 0.11 }
            : new WallType
            {
                Name = UniqueName(from.Name + " – bản sao"), Thickness = from.Thickness, UnitWeight = from.UnitWeight, Finish = from.Finish,
                PlasterThickness = from.PlasterThickness, PlasterUnitWeight = from.PlasterUnitWeight, OpeningRatio = from.OpeningRatio
            };
        _p.WallTypes.Add(t);
        var row = new WallTypeRow(_p, t, MarkChanged);
        _rows.Add(row);
        _grid.SelectedItem = row;
        _grid.ScrollIntoView(row);
        Changed = true;
    }

    private void Delete()
    {
        DialogUi.Commit(_grid);
        var sel = _grid.SelectedItems.OfType<WallTypeRow>().ToList();
        if (sel.Count == 0) return;
        var used = sel.Where(r => r.Uses > 0).ToList();
        if (used.Count > 0)
        {
            MessageBox.Show(this, "Không xoá được loại tường đang dùng:\n" + string.Join("\n", used.Select(r => $"  • {r.Name} – {r.Uses} đoạn tường")) +
                                  "\n\nĐổi loại cho các đoạn tường đó trước (Vẽ → Tường đã khai báo…).", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show(this, $"Xoá {sel.Count} loại tường?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        foreach (var r in sel) { _p.WallTypes.Remove(r.T); _rows.Remove(r); }
        Changed = true;
    }

    private void AddDefaults()
    {
        DialogUi.Commit(_grid);
        int n = 0;
        foreach (var t in WallType.Defaults())
            if (!_p.WallTypes.Any(q => q.Name == t.Name)) { _p.WallTypes.Add(t); n++; }
        if (n == 0) { MessageBox.Show(this, "Thư viện đã có đủ các loại tường mặc định.", Title); return; }
        Reload();
        Changed = true;
    }
}

/// <summary>Một dòng bảng tường đã khai báo.</summary>
internal sealed class WallRow : RowBase
{
    private readonly Project _p;
    private readonly Action _changed;
    public WallLine W { get; }

    public WallRow(Project p, WallLine w, Action changed) { _p = p; W = w; _changed = changed; }

    private void Done() { RaiseAll(); _changed(); }

    public int Id => W.Id;
    public string Story { get => W.Story; set { if (!string.IsNullOrEmpty(value) && _p.Stories.Any(s => s.Name == value)) { W.Story = value; Done(); } } }
    public double X1 { get => W.X1; set { W.X1 = value; Done(); } }
    public double Y1 { get => W.Y1; set { W.Y1 = value; Done(); } }
    public double X2 { get => W.X2; set { W.X2 = value; Done(); } }
    public double Y2 { get => W.Y2; set { W.Y2 = value; Done(); } }
    public string Type { get => W.Type; set { if (!string.IsNullOrEmpty(value) && _p.WallTypes.Any(t => t.Name == value)) { W.Type = value; Done(); } } }
    /// <summary>0 = tự động</summary>
    public double Height { get => W.Height; set { W.Height = Math.Max(0, value); Done(); } }
    /// <summary>−1 = theo loại tường</summary>
    public double OpeningArea { get => W.OpeningArea; set { W.OpeningArea = value < 0 ? -1 : value; Done(); } }
    public string Note { get => W.Note; set { W.Note = value ?? ""; Done(); } }
    public double Length => Math.Round(W.Length, 3);
    public string HUsed { get { var c = WallMath.Calc(_p, W); return c?.Type == null ? "?" : DialogUi.F(c.H, "0.00") + (c.AutoHeight ? " (tự động)" : ""); } }
    public double Q { get { var c = WallMath.Calc(_p, W); return c?.Type == null ? 0 : Math.Round(c.Q, 2); } }
    public string Support
    {
        get
        {
            var c = WallMath.Calc(_p, W);
            if (c?.Type == null) return "loại tường không có";
            return c.Covered >= c.Length - 0.05 ? "trên dầm" : c.Covered > 0 ? $"dầm {c.Covered:0.00} m, còn lại lên sàn" : "lên sàn";
        }
    }
}

/// <summary>Danh sách tường đã khai báo (Project.Walls): sửa, xoá, chọn trên mặt bằng.</summary>
internal sealed class WallListWindow : Window
{
    private const string All = "(Tất cả các tầng)";
    private readonly Project _p;
    private readonly DataGrid _grid;
    private readonly ComboBox _cbStory;
    private readonly TextBlock _sum;
    private readonly ObservableCollection<WallRow> _rows = new();
    public bool Changed { get; private set; }
    /// <summary>Tường cần chọn trên mặt bằng sau khi đóng (nút "Chọn trên mặt bằng").</summary>
    public WallLine SelectedWall { get; private set; }

    public WallListWindow(Project p, string story)
    {
        _p = p;
        DialogUi.Setup(this, "Tường đã khai báo", 1180, 560);
        var stories = p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name).ToList();
        _grid = DialogUi.NewGrid();
        _grid.Columns.Add(DialogUi.Num("#", nameof(WallRow.Id), "0", true));
        _grid.Columns.Add(DialogUi.Combo("Tầng", nameof(WallRow.Story), stories, 70));
        _grid.Columns.Add(DialogUi.Num("X1 (m)", nameof(WallRow.X1), "0.00#"));
        _grid.Columns.Add(DialogUi.Num("Y1 (m)", nameof(WallRow.Y1), "0.00#"));
        _grid.Columns.Add(DialogUi.Num("X2 (m)", nameof(WallRow.X2), "0.00#"));
        _grid.Columns.Add(DialogUi.Num("Y2 (m)", nameof(WallRow.Y2), "0.00#"));
        _grid.Columns.Add(DialogUi.Combo("Loại tường", nameof(WallRow.Type), p.WallTypes.Select(t => t.Name).ToList(), 190));
        _grid.Columns.Add(DialogUi.Num("Cao (m, 0 = tự động)", nameof(WallRow.Height), "0.00"));
        _grid.Columns.Add(DialogUi.Num("Cửa (m², −1 = theo loại)", nameof(WallRow.OpeningArea), "0.00"));
        _grid.Columns.Add(DialogUi.Num("Dài (m)", nameof(WallRow.Length), "0.00", true));
        _grid.Columns.Add(DialogUi.Text("H tính (m)", nameof(WallRow.HUsed), true));
        _grid.Columns.Add(DialogUi.Num("Tải (kN/m)", nameof(WallRow.Q), "0.00", true));
        _grid.Columns.Add(DialogUi.Text("Đỡ bởi", nameof(WallRow.Support), true));
        _grid.Columns.Add(DialogUi.Text("Ghi chú", nameof(WallRow.Note), false, 120));
        _grid.ItemsSource = _rows;
        _grid.MouseDoubleClick += (s, e) => { if (e.OriginalSource is FrameworkElement fe && fe.DataContext is WallRow && _grid.CurrentColumn?.IsReadOnly == true) PickOnPlan(); };

        _cbStory = new ComboBox { Width = 150, Margin = new Thickness(0, 0, 12, 0) };
        var items = new List<string> { All }; items.AddRange(stories);
        _cbStory.ItemsSource = items;
        _cbStory.SelectedItem = story != null && stories.Contains(story) ? story : All;
        _cbStory.SelectionChanged += (s, e) => Reload();
        _sum = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = System.Windows.Media.Brushes.DimGray };

        var dock = new DockPanel { Margin = new Thickness(8) };
        var top = DialogUi.Row(DialogUi.Label("Tầng:"), _cbStory,
            DialogUi.Btn("Xoá dòng chọn", (s, e) => Delete()),
            DialogUi.Btn("Chọn trên mặt bằng", (s, e) => PickOnPlan(), true),
            _sum);
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);
        var hint = DialogUi.Hint("Tầng = cao trình tường đứng (dầm/sàn đỡ tường). Chiều cao 0 = chiều cao tầng trên − chiều cao dầm (tầng mái: 0,9 m). " +
                                 "Diện tích cửa −1 = theo tỷ lệ cửa của loại tường; 0 = tường đặc. Tải tường tự sinh khi chạy phân tích.");
        DockPanel.SetDock(hint, Dock.Top);
        dock.Children.Add(hint);
        var close = DialogUi.Btn("Đóng", (s, e) => { DialogUi.Commit(_grid); Close(); }, true);
        close.IsCancel = true;
        var bottom = DialogUi.Row(close);
        bottom.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(bottom);
        dock.Children.Add(_grid);
        Content = dock;
        Closing += (s, e) => DialogUi.Commit(_grid);
        Reload();
    }

    private void MarkChanged() { Changed = true; UpdateSum(); }

    private void Reload()
    {
        DialogUi.Commit(_grid);
        string st = _cbStory.SelectedItem as string;
        _rows.Clear();
        foreach (var w in _p.Walls.Where(w => st == null || st == All || w.Story == st).OrderBy(w => w.Story).ThenBy(w => w.Id))
            _rows.Add(new WallRow(_p, w, MarkChanged));
        UpdateSum();
    }

    private void UpdateSum()
    {
        double L = _rows.Sum(r => r.W.Length);
        _sum.Text = $"{_rows.Count} đoạn, tổng dài {L:0.00} m" + (_rows.Count == 0 ? " – khai báo tường bằng chế độ '＋ Tường' / 'Gán tường' trên mặt bằng" : "");
    }

    private void Delete()
    {
        DialogUi.Commit(_grid);
        var sel = _grid.SelectedItems.OfType<WallRow>().ToList();
        if (sel.Count == 0) return;
        if (MessageBox.Show(this, $"Xoá {sel.Count} đoạn tường?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        foreach (var r in sel) { ModelEdit.DeleteWall(_p, r.W); _rows.Remove(r); }
        Changed = true;
        UpdateSum();
    }

    private void PickOnPlan()
    {
        DialogUi.Commit(_grid);
        if (_grid.SelectedItem is not WallRow r) { MessageBox.Show(this, "Chọn một dòng tường trước.", Title); return; }
        SelectedWall = r.W;
        Close();
    }
}
