using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Một lớp cấu tạo sàn trong bảng.</summary>
internal sealed class FloorLayerRow : RowBase
{
    private readonly Action _changed;
    public FloorLayer L { get; }
    public FloorLayerRow(FloorLayer l, Action changed) { L = l; _changed = changed; }
    private void Done() { RaiseAll(); _changed(); }

    public string Name { get => L.Name; set { L.Name = (value ?? "").Trim(); Done(); } }
    public double ThicknessMm { get => L.Thickness * 1000; set { if (value >= 0 && value <= 1000) { L.Thickness = value / 1000; Done(); } } }
    public double UnitWeight { get => L.UnitWeight; set { if (value >= 0 && value <= 80) { L.UnitWeight = value; Done(); } } }
    public double FixedLoad { get => L.FixedLoad; set { if (value >= 0 && value <= 50) { L.FixedLoad = value; Done(); } } }
    public double Load => Math.Round(L.Load, 4);
}

/// <summary>Quản lý thư viện cấu tạo sàn (Project.FloorBuildUps) và gán cho ô sàn.</summary>
internal sealed class FloorBuildUpWindow : Window
{
    private readonly Project _p;
    private readonly Slab _slab;
    private readonly string _story;
    private readonly ListBox _list;
    private readonly DataGrid _grid;
    private readonly TextBox _txtLive;
    private readonly TextBlock _txtTotal, _txtUse;
    private readonly ObservableCollection<FloorLayerRow> _rows = new();
    private bool _loading;

    public bool Changed { get; private set; }
    /// <summary>Thông báo của thao tác gán gần nhất (để hiện trên thanh trạng thái).</summary>
    public string LastMessage { get; private set; }

    public FloorBuildUpWindow(Project p, Slab selectedSlab, string story)
    {
        _p = p; _slab = selectedSlab; _story = story;
        DialogUi.Setup(this, "Cấu tạo sàn", 980, 560);

        // ---- trái: danh sách cấu tạo ----
        _list = new ListBox { Margin = new Thickness(0, 4, 0, 4) };
        _list.SelectionChanged += (s, e) => ShowCurrent();
        var left = new DockPanel { Margin = new Thickness(0, 0, 8, 0) };
        var lt = DialogUi.Label("Thư viện cấu tạo sàn", double.NaN, true);
        DockPanel.SetDock(lt, Dock.Top); left.Children.Add(lt);
        var lb1 = DialogUi.Row(DialogUi.Btn("＋ Thêm", (s, e) => Add(false)), DialogUi.Btn("Nhân bản", (s, e) => Add(true)));
        var lb2 = DialogUi.Row(DialogUi.Btn("Đổi tên", (s, e) => Rename()), DialogUi.Btn("Xoá", (s, e) => Delete()));
        DockPanel.SetDock(lb2, Dock.Bottom); DockPanel.SetDock(lb1, Dock.Bottom);
        left.Children.Add(lb2); left.Children.Add(lb1);
        left.Children.Add(_list);

        // ---- phải: các lớp ----
        _grid = DialogUi.NewGrid();
        _grid.Columns.Add(DialogUi.Text("Lớp cấu tạo", nameof(FloorLayerRow.Name), false, 230));
        _grid.Columns.Add(DialogUi.Num("Dày (mm)", nameof(FloorLayerRow.ThicknessMm), "0.#"));
        _grid.Columns.Add(DialogUi.Num("γ (kN/m³)", nameof(FloorLayerRow.UnitWeight), "0.##"));
        _grid.Columns.Add(DialogUi.Num("Tải cố định (kN/m²)", nameof(FloorLayerRow.FixedLoad), "0.###"));
        _grid.Columns.Add(DialogUi.Num("Tải lớp (kN/m²)", nameof(FloorLayerRow.Load), "0.000", true));
        _grid.ItemsSource = _rows;

        _txtLive = new TextBox { Width = 60, VerticalContentAlignment = VerticalAlignment.Center };
        _txtLive.TextChanged += (s, e) =>
        {
            if (_loading || Current() == null) return;
            double v = DialogUi.ParseNum(_txtLive.Text, double.NaN);
            if (double.IsNaN(v) || v < 0 || v > 50) return;
            Current().Live = v; Changed = true; UpdateTotal();
        };
        _txtTotal = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold, Margin = new Thickness(12, 0, 0, 0) };
        _txtUse = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.DimGray, Margin = new Thickness(12, 0, 0, 0) };

        var right = new DockPanel();
        var rt = DialogUi.Row(DialogUi.Btn("＋ Lớp", (s, e) => AddLayer()), DialogUi.Btn("Xoá lớp", (s, e) => DeleteLayer()),
            DialogUi.Btn("↑", (s, e) => MoveLayer(-1)), DialogUi.Btn("↓", (s, e) => MoveLayer(1)), _txtUse);
        DockPanel.SetDock(rt, Dock.Top); right.Children.Add(rt);
        var rl = DialogUi.Row(DialogUi.Label("Hoạt tải tiêu chuẩn (kN/m²):"), _txtLive, _txtTotal);
        DockPanel.SetDock(rl, Dock.Bottom); right.Children.Add(rl);
        var hint = DialogUi.Hint("Không khai báo bản BTCT – trọng lượng bản thân sàn tính theo tiết diện sàn. Tải hoàn thiện = Σ(dày × γ + tải cố định).");
        DockPanel.SetDock(hint, Dock.Bottom); right.Children.Add(hint);
        right.Children.Add(_grid);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(left, 0); Grid.SetColumn(right, 1);
        body.Children.Add(left); body.Children.Add(right);

        // ---- dưới: gán ----
        var bSlab = DialogUi.Btn("Gán cho ô sàn đang chọn" + (_slab != null ? $" ({_slab.Label})" : ""), (s, e) => AssignSlab());
        bSlab.IsEnabled = _slab != null;
        if (_slab == null) bSlab.ToolTip = "Chọn một ô sàn trên mặt bằng trước khi mở hộp thoại";
        var bStory = DialogUi.Btn("Gán cho mọi ô sàn của tầng đang xem" + (_story != null ? $" ({_story})" : ""), (s, e) => AssignStory());
        bStory.IsEnabled = _story != null;
        if (_story == null) bStory.ToolTip = "Chọn một tầng cụ thể ở ô 'Tầng đang xem'";
        var close = DialogUi.Btn("Đóng", (s, e) => { DialogUi.Commit(_grid); Close(); }, true);
        close.IsCancel = true;
        var bottom = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(close, Dock.Right);
        bottom.Children.Add(close);
        bottom.Children.Add(DialogUi.Row(bSlab, bStory));

        var dock = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(bottom);
        dock.Children.Add(body);
        Content = dock;
        Closing += (s, e) => DialogUi.Commit(_grid);

        ReloadList(_slab != null && !string.IsNullOrEmpty(_slab.BuildUp) ? _slab.BuildUp : null);
    }

    private FloorBuildUp Current() => _list.SelectedItem as FloorBuildUp;

    private void ReloadList(string select)
    {
        _list.ItemsSource = null;
        _list.ItemsSource = _p.FloorBuildUps.ToList();
        _list.SelectedItem = _p.FloorBuildUps.FirstOrDefault(b => b.Name == select) ?? _p.FloorBuildUps.FirstOrDefault();
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        DialogUi.Commit(_grid);
        _loading = true;
        try
        {
            _rows.Clear();
            var bu = Current();
            _grid.IsEnabled = _txtLive.IsEnabled = bu != null;
            if (bu == null) { _txtLive.Text = ""; _txtTotal.Text = ""; _txtUse.Text = ""; return; }
            foreach (var l in bu.Layers) _rows.Add(new FloorLayerRow(l, LayerChanged));
            _txtLive.Text = DialogUi.F(bu.Live, "0.##");
            int n = _p.Slabs.Count(s => s.BuildUp == bu.Name);
            _txtUse.Text = n > 0 ? $"đang gán cho {n} ô sàn" : "chưa gán ô sàn nào";
            UpdateTotal();
        }
        finally { _loading = false; }
    }

    private void LayerChanged() { Changed = true; UpdateTotal(); }

    private void UpdateTotal()
    {
        var bu = Current();
        _txtTotal.Text = bu == null ? "" : $"Hoàn thiện: {bu.Finish:0.000} kN/m²   •   Hoạt tải: {bu.Live:0.##} kN/m²";
    }

    private string UniqueName(string b)
    {
        string n = b; int k = 2;
        while (_p.FloorBuildUps.Any(t => t.Name == n)) n = $"{b} ({k++})";
        return n;
    }

    private void Add(bool duplicate)
    {
        DialogUi.Commit(_grid);
        var src = Current();
        if (duplicate && src == null) return;
        string name = DialogUi.InputBox(this, "Cấu tạo sàn", "Tên cấu tạo sàn mới:", UniqueName(duplicate ? src.Name + " – bản sao" : "Cấu tạo mới"));
        if (name == null) return;
        if (_p.FloorBuildUps.Any(b => b.Name == name)) { MessageBox.Show(this, $"Đã có cấu tạo \"{name}\".", Title); return; }
        var bu = new FloorBuildUp { Name = name, Live = duplicate ? src.Live : 1.5 };
        if (duplicate) foreach (var l in src.Layers) bu.Layers.Add(new FloorLayer { Name = l.Name, Thickness = l.Thickness, UnitWeight = l.UnitWeight, FixedLoad = l.FixedLoad });
        else { bu.Layers.Add(FloorBuildUp.L("Gạch lát", 10, 20)); bu.Layers.Add(FloorBuildUp.L("Vữa lót XM M75", 30, 18)); bu.Layers.Add(FloorBuildUp.L("Vữa trát trần", 15, 18)); }
        _p.FloorBuildUps.Add(bu);
        Changed = true;
        ReloadList(name);
    }

    private void Rename()
    {
        var bu = Current();
        if (bu == null) return;
        string name = DialogUi.InputBox(this, "Đổi tên cấu tạo sàn", "Tên mới:", bu.Name);
        if (name == null || name == bu.Name) return;
        if (_p.FloorBuildUps.Any(b => b.Name == name)) { MessageBox.Show(this, $"Đã có cấu tạo \"{name}\".", Title); return; }
        foreach (var s in _p.Slabs.Where(s => s.BuildUp == bu.Name)) s.BuildUp = name;
        bu.Name = name;
        Changed = true;
        ReloadList(name);
    }

    private void Delete()
    {
        var bu = Current();
        if (bu == null) return;
        int n = _p.Slabs.Count(s => s.BuildUp == bu.Name);
        if (n > 0)
        {
            MessageBox.Show(this, $"Cấu tạo \"{bu.Name}\" đang gán cho {n} ô sàn – gán cấu tạo khác cho các ô đó trước khi xoá.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show(this, $"Xoá cấu tạo \"{bu.Name}\"?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _p.FloorBuildUps.Remove(bu);
        Changed = true;
        ReloadList(null);
    }

    private void AddLayer()
    {
        var bu = Current();
        if (bu == null) return;
        DialogUi.Commit(_grid);
        var l = FloorBuildUp.L("Lớp mới", 10, 18);
        int at = _grid.SelectedItem is FloorLayerRow r ? bu.Layers.IndexOf(r.L) + 1 : bu.Layers.Count;
        bu.Layers.Insert(at, l);
        Changed = true;
        ShowCurrent();
        _grid.SelectedIndex = at;
    }

    private void DeleteLayer()
    {
        var bu = Current();
        if (bu == null) return;
        DialogUi.Commit(_grid);
        var sel = _grid.SelectedItems.OfType<FloorLayerRow>().ToList();
        if (sel.Count == 0) return;
        foreach (var r in sel) bu.Layers.Remove(r.L);
        Changed = true;
        ShowCurrent();
    }

    private void MoveLayer(int d)
    {
        var bu = Current();
        if (bu == null || _grid.SelectedItem is not FloorLayerRow r) return;
        DialogUi.Commit(_grid);
        int i = bu.Layers.IndexOf(r.L), j = i + d;
        if (i < 0 || j < 0 || j >= bu.Layers.Count) return;
        bu.Layers.RemoveAt(i); bu.Layers.Insert(j, r.L);
        Changed = true;
        ShowCurrent();
        _grid.SelectedIndex = j;
    }

    private void AssignSlab()
    {
        DialogUi.Commit(_grid);
        var bu = Current();
        if (bu == null || _slab == null) return;
        var r = ModelEdit.SetBuildUp(_p, _slab, bu.Name);
        LastMessage = r.Message;
        if (r.Ok) Changed = true;
        ShowCurrent();
        MessageBox.Show(this, r.Message, Title, MessageBoxButton.OK, r.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void AssignStory()
    {
        DialogUi.Commit(_grid);
        var bu = Current();
        if (bu == null || _story == null) return;
        var slabs = _p.Slabs.Where(s => s.Story == _story).ToList();
        if (slabs.Count == 0) { MessageBox.Show(this, $"Tầng {_story} chưa có ô sàn.", Title); return; }
        if (MessageBox.Show(this, $"Gán cấu tạo \"{bu.Name}\" cho {slabs.Count} ô sàn tầng {_story}?\n(Thay tải hoàn thiện và hoạt tải của các ô.)", Title,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        int ok = 0;
        foreach (var s in slabs) if (ModelEdit.SetBuildUp(_p, s, bu.Name).Ok) ok++;
        LastMessage = $"Đã gán cấu tạo \"{bu.Name}\" ({bu.Finish:0.00} kN/m², hoạt tải {bu.Live:0.##}) cho {ok} ô sàn tầng {_story}.";
        if (ok > 0) Changed = true;
        ShowCurrent();
        MessageBox.Show(this, LastMessage, Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
