using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// HỘP THOẠI KIỂU REVIT: "Sửa kiểu tường" (cấu tạo nhiều lớp: vữa trát, ốp đá, gạch men, lõi gạch / BT – xem trước mặt cắt),
// "Vật liệu tường" (γ), "Kiểu cửa" (cửa đi / cửa sổ / ô thoáng), "Hiển thị / Đồ hoạ" (bật tắt thẻ theo loại, mức chi tiết).
// Lõi: KetCauVXT.Model.WallAssembly (Model/WallAssembly.cs).
// =====================================================================

/// <summary>Một dòng lớp tường trong bảng "Sửa kiểu tường".</summary>
internal sealed class WallLayerRow : RowBase
{
    private readonly Project _p;
    private readonly Action _changed;
    public WallLayer L { get; }
    public WallLayerRow(Project p, WallLayer l, Action changed) { _p = p; L = l; _changed = changed; }
    private void Done() { RaiseAll(); _changed(); }

    public string Function { get => L.Function; set { if (!string.IsNullOrEmpty(value)) { L.Function = value; Done(); } } }
    public string Material
    {
        get => L.Material;
        set
        {
            if (string.IsNullOrEmpty(value)) return;
            L.Material = value;
            if (WallMaterials.Find(_p.WallMaterials, value) is WallMaterial m) L.UnitWeight = m.UnitWeight;
            Done();
        }
    }
    public double ThicknessMm { get => L.Thickness * 1000; set { if (value > 0 && value <= 1000) { L.Thickness = value / 1000; Done(); } } }
    public double UnitWeight { get => L.UnitWeight; set { if (value > 0 && value <= 40) { L.UnitWeight = value; Done(); } } }
    public double Load => Math.Round(L.WeightPerM2, 3);
}

/// <summary>"Sửa kiểu tường" kiểu Revit Edit Assembly: danh sách kiểu bên trái, bảng lớp (ngoài → trong) + xem trước mặt cắt bên phải.</summary>
internal sealed class WallTypeEditorWindow : Window
{
    private readonly Project _p;
    private readonly ListBox _types = new() { Width = 250, Margin = new Thickness(0, 0, 8, 0) };
    private readonly TextBox _name = new() { Width = 300 };
    private readonly ComboBox _fn = new() { Width = 200, ItemsSource = WallFunctionName.All };
    private readonly DataGrid _grid;
    private readonly ObservableCollection<WallLayerRow> _rows = new();
    private readonly Canvas _preview = new() { Height = 150, Background = Brushes.White, ClipToBounds = true };
    private readonly TextBlock _sum = new() { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 4) };
    private bool _loading;
    public bool Changed { get; private set; }
    public string SelectedType => (_types.SelectedItem as WallType)?.Name;

    public WallTypeEditorWindow(Project p, string select = null)
    {
        _p = p;
        WallAssembly.Upgrade(p);
        DialogUi.Setup(this, "Sửa kiểu tường – cấu tạo nhiều lớp", 1060, 640);
        _grid = DialogUi.NewGrid();
        _grid.Columns.Add(DialogUi.Combo("Chức năng lớp", nameof(WallLayerRow.Function), WallLayerFn.All, 150));
        _grid.Columns.Add(DialogUi.Combo("Vật liệu", nameof(WallLayerRow.Material), _p.WallMaterials.Select(m => m.Name).ToList(), 200));
        _grid.Columns.Add(DialogUi.Num("Dày (mm)", nameof(WallLayerRow.ThicknessMm), "0.#"));
        _grid.Columns.Add(DialogUi.Num("γ (kN/m³)", nameof(WallLayerRow.UnitWeight), "0.##"));
        _grid.Columns.Add(DialogUi.Num("Tải lớp (kN/m²)", nameof(WallLayerRow.Load), "0.000", true));
        _grid.ItemsSource = _rows;
        _grid.SelectionMode = DataGridSelectionMode.Single;

        _types.DisplayMemberPath = nameof(WallType.Name);
        _types.SelectionChanged += (s, e) => LoadType();

        var left = new DockPanel();
        var lb = DialogUi.Row(
            DialogUi.Btn("＋ Mới", (s, e) => NewType(null)),
            DialogUi.Btn("Nhân bản", (s, e) => NewType(_types.SelectedItem as WallType)),
            DialogUi.Btn("Xoá", (s, e) => DeleteType()));
        DockPanel.SetDock(lb, Dock.Bottom);
        left.Children.Add(lb);
        var lt = DialogUi.Label("Kiểu tường (Tường cơ bản)", double.NaN, true);
        DockPanel.SetDock(lt, Dock.Top);
        left.Children.Add(lt);
        left.Children.Add(_types);

        var right = new DockPanel();
        var head = new StackPanel();
        head.Children.Add(DialogUi.Row(DialogUi.Label("Tên kiểu", 80), _name));
        head.Children.Add(DialogUi.Row(DialogUi.Label("Chức năng", 80), _fn));
        head.Children.Add(DialogUi.Hint("Các lớp xếp từ MẶT NGOÀI (trên) vào MẶT TRONG (dưới), như Revit. Lớp \"Lõi kết cấu\" là khối xây / bê tông chịu lực; " +
                                        "lớp vữa trát, ốp đá, gạch men… cộng vào tải tường. Tải 1 m² = Σ dày × γ."));
        DockPanel.SetDock(head, Dock.Top);
        right.Children.Add(head);
        var btns = DialogUi.Row(
            DialogUi.Btn("＋ Chèn lớp", (s, e) => AddLayer()),
            DialogUi.Btn("Xoá lớp", (s, e) => RemoveLayer()),
            DialogUi.Btn("▲ Lên", (s, e) => MoveLayer(-1)),
            DialogUi.Btn("▼ Xuống", (s, e) => MoveLayer(1)),
            DialogUi.Btn("Vật liệu…", (s, e) => EditMaterials()));
        DockPanel.SetDock(btns, Dock.Top);
        right.Children.Add(btns);
        var prevBox = new StackPanel();
        prevBox.Children.Add(_sum);
        prevBox.Children.Add(new Border { Child = _preview, BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1) });
        DockPanel.SetDock(prevBox, Dock.Bottom);
        right.Children.Add(prevBox);
        right.Children.Add(_grid);

        var body = new DockPanel { Margin = new Thickness(8) };
        var close = DialogUi.Btn("Đóng", (s, e) => { Commit(); Close(); }, true);
        close.IsCancel = true;
        var bottom = DialogUi.Row(close);
        bottom.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(bottom, Dock.Bottom);
        body.Children.Add(bottom);
        DockPanel.SetDock(left, Dock.Left);
        body.Children.Add(left);
        body.Children.Add(right);
        Content = body;

        _name.LostFocus += (s, e) => Rename();
        _fn.SelectionChanged += (s, e) =>
        {
            if (_loading || _types.SelectedItem is not WallType t || _fn.SelectedItem is not string f) return;
            t.Function = f; Changed = true;
        };
        _preview.SizeChanged += (s, e) => DrawPreview();
        Closing += (s, e) => Commit();
        RefreshList(select);
    }

    private void Commit() { DialogUi.Commit(_grid); Rename(); }

    private void RefreshList(string select)
    {
        _types.ItemsSource = null;
        _types.ItemsSource = _p.WallTypes;
        _types.SelectedItem = _p.WallTypes.FirstOrDefault(t => t.Name == select) ?? _p.WallTypes.FirstOrDefault();
    }

    private WallType Cur => _types.SelectedItem as WallType;

    private void LoadType()
    {
        _loading = true;
        _rows.Clear();
        var t = Cur;
        if (t != null)
        {
            WallAssembly.ToLayered(t);
            _name.Text = t.Name;
            _fn.SelectedItem = WallFunctionName.All.Contains(t.Function) ? t.Function : null;
            foreach (var l in t.Layers) _rows.Add(new WallLayerRow(_p, l, OnLayerChanged));
        }
        _loading = false;
        Summarize();
    }

    private void OnLayerChanged()
    {
        Changed = true;
        Cur?.SyncFromLayers();
        Summarize();
    }

    private void Summarize()
    {
        var t = Cur;
        _sum.Text = t == null ? "" : $"Tổng dày {t.TotalThickness * 1000:0} mm · lõi {t.Thickness * 1000:0} mm · tải 1 m² tường đặc {t.WeightPerM2:0.00} kN/m²" +
                                     $" · dùng ở {_p.Walls.Count(w => w.Type == t.Name)} đoạn tường";
        DrawPreview();
    }

    private static Brush LayerBrush(WallLayer l) => l.Function switch
    {
        WallLayerFn.Core => l.Material == WallMaterials.Concrete ? new SolidColorBrush(Color.FromRgb(190, 190, 190)) : new SolidColorBrush(Color.FromRgb(214, 150, 110)),
        WallLayerFn.Plaster => new SolidColorBrush(Color.FromRgb(236, 232, 220)),
        WallLayerFn.Stone => new SolidColorBrush(Color.FromRgb(150, 150, 165)),
        WallLayerFn.Tile => new SolidColorBrush(Color.FromRgb(170, 210, 230)),
        WallLayerFn.Insulation => new SolidColorBrush(Color.FromRgb(250, 230, 120)),
        _ => new SolidColorBrush(Color.FromRgb(245, 245, 245))
    };

    /// <summary>Xem trước mặt cắt ngang tường (các lớp theo chiều dày, tỷ lệ thật, ghi chiều dày từng lớp).</summary>
    private void DrawPreview()
    {
        _preview.Children.Clear();
        var t = Cur;
        if (t == null || !t.IsLayered) return;
        double W = Math.Max(_preview.ActualWidth, 300), H = _preview.ActualHeight > 0 ? _preview.ActualHeight : 150;
        double total = Math.Max(t.TotalThickness, 0.05);
        double k = Math.Min((W - 120) / total, 1200);   // px / m
        double x = 60, y0 = 26, h = H - 52;
        _preview.Children.Add(new TextBlock { Text = "NGOÀI", Foreground = Brushes.Gray, FontSize = 10, Margin = new Thickness(6, H / 2 - 7, 0, 0) });
        foreach (var l in t.Layers)
        {
            double w = Math.Max(1, l.Thickness * k);
            var r = new Shapes.Rectangle { Width = w, Height = h, Fill = LayerBrush(l), Stroke = Brushes.DimGray, StrokeThickness = 0.6 };
            Canvas.SetLeft(r, x); Canvas.SetTop(r, y0);
            _preview.Children.Add(r);
            if (l.IsCore)
                for (double yy = y0 + 6; yy < y0 + h; yy += 8)
                    _preview.Children.Add(new Shapes.Line { X1 = x, Y1 = yy + Math.Min(w, 8), X2 = x + Math.Min(w, 8), Y2 = yy, Stroke = Brushes.SaddleBrown, StrokeThickness = 0.5 });
            var tb = new TextBlock { Text = $"{l.Thickness * 1000:0}", FontSize = 10 };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, x + w / 2 - tb.DesiredSize.Width / 2); Canvas.SetTop(tb, 6);
            _preview.Children.Add(tb);
            x += w;
        }
        var tin = new TextBlock { Text = "TRONG", Foreground = Brushes.Gray, FontSize = 10 };
        Canvas.SetLeft(tin, x + 6); Canvas.SetTop(tin, H / 2 - 7);
        _preview.Children.Add(tin);
        var tot = new TextBlock { Text = $"Σ {t.TotalThickness * 1000:0} mm", FontSize = 11, FontWeight = FontWeights.SemiBold };
        Canvas.SetLeft(tot, 60); Canvas.SetTop(tot, y0 + h + 6);
        _preview.Children.Add(tot);
    }

    private void Rename()
    {
        var t = Cur;
        if (t == null || _loading) return;
        string n = (_name.Text ?? "").Trim();
        if (n.Length == 0 || n == t.Name) return;
        if (_p.WallTypes.Any(q => q != t && q.Name == n)) { MessageBox.Show(this, $"Đã có kiểu tường \"{n}\".", Title); _name.Text = t.Name; return; }
        foreach (var w in _p.Walls.Where(w => w.Type == t.Name)) w.Type = n;
        t.Name = n;
        Changed = true;
        RefreshList(n);
    }

    private string Unique(string b)
    {
        string n = b; int k = 2;
        while (_p.WallTypes.Any(t => t.Name == n)) n = $"{b} ({k++})";
        return n;
    }

    private void NewType(WallType from)
    {
        Commit();
        var t = from == null
            ? WallAssembly.Make(Unique("Tường mới 110 trát 2 mặt"), WallFunctionName.Interior,
                new WallLayer { Function = WallLayerFn.Plaster, Material = WallMaterials.Mortar, Thickness = 0.015, UnitWeight = 18 },
                new WallLayer { Function = WallLayerFn.Core, Material = WallMaterials.SolidBrick, Thickness = 0.11, UnitWeight = 18 },
                new WallLayer { Function = WallLayerFn.Plaster, Material = WallMaterials.Mortar, Thickness = 0.015, UnitWeight = 18 })
            : WallAssembly.Make(Unique(from.Name + " – bản sao"), from.Function, WallAssembly.ToLayered(from).Layers.ToArray());
        t.OpeningRatio = from?.OpeningRatio ?? 0;
        _p.WallTypes.Add(t);
        Changed = true;
        RefreshList(t.Name);
    }

    private void DeleteType()
    {
        var t = Cur;
        if (t == null) return;
        int uses = _p.Walls.Count(w => w.Type == t.Name);
        if (uses > 0) { MessageBox.Show(this, $"Kiểu \"{t.Name}\" đang dùng ở {uses} đoạn tường – đổi kiểu các đoạn đó trước khi xoá.", Title); return; }
        _p.WallTypes.Remove(t);
        Changed = true;
        RefreshList(null);
    }

    private void AddLayer()
    {
        var t = Cur;
        if (t == null) return;
        DialogUi.Commit(_grid);
        int i = _grid.SelectedIndex >= 0 ? _grid.SelectedIndex : t.Layers.Count;
        var l = new WallLayer { Function = WallLayerFn.Plaster, Material = WallMaterials.Mortar, Thickness = 0.015, UnitWeight = 18 };
        t.Layers.Insert(i, l);
        OnLayerChanged();
        LoadType();
        _grid.SelectedIndex = i;
    }

    private void RemoveLayer()
    {
        var t = Cur;
        if (t == null || _grid.SelectedItem is not WallLayerRow r) return;
        if (t.Layers.Count <= 1) { MessageBox.Show(this, "Kiểu tường phải còn ít nhất một lớp.", Title); return; }
        t.Layers.Remove(r.L);
        OnLayerChanged();
        LoadType();
    }

    private void MoveLayer(int d)
    {
        var t = Cur;
        if (t == null || _grid.SelectedItem is not WallLayerRow r) return;
        int i = t.Layers.IndexOf(r.L), j = i + d;
        if (i < 0 || j < 0 || j >= t.Layers.Count) return;
        t.Layers.RemoveAt(i); t.Layers.Insert(j, r.L);
        OnLayerChanged();
        LoadType();
        _grid.SelectedIndex = j;
    }

    private void EditMaterials()
    {
        Commit();
        var w = new MaterialLibraryWindow(_p) { Owner = this };
        w.ShowDialog();
        if (w.Changed) { WallAssembly.ApplyMaterials(_p); Changed = true; }
        var sel = Cur?.Name;
        // cập nhật danh sách vật liệu của cột chọn
        if (_grid.Columns[1] is DataGridComboBoxColumn mc) mc.ItemsSource = _p.WallMaterials.Select(m => m.Name).ToList();
        RefreshList(sel);
    }
}

internal sealed class WallMaterialRow : RowBase
{
    private readonly Action _changed;
    public WallMaterial M { get; }
    public WallMaterialRow(WallMaterial m, Action changed) { M = m; _changed = changed; }
    public string Name { get => M.Name; set { var n = (value ?? "").Trim(); if (n.Length > 0) { M.Name = n; RaiseAll(); _changed(); } } }
    public double UnitWeight { get => M.UnitWeight; set { if (value > 0 && value <= 40) { M.UnitWeight = value; RaiseAll(); _changed(); } } }
}

/// <summary>Thư viện vật liệu tường (γ) – sửa γ cập nhật mọi kiểu tường dùng vật liệu đó.</summary>
internal sealed class MaterialLibraryWindow : Window
{
    private readonly Project _p;
    private readonly DataGrid _grid = DialogUi.NewGrid();
    private readonly ObservableCollection<WallMaterialRow> _rows = new();
    public bool Changed { get; private set; }

    public MaterialLibraryWindow(Project p)
    {
        _p = p;
        p.WallMaterials ??= WallMaterials.Defaults();
        DialogUi.Setup(this, "Vật liệu tường", 520, 460);
        _grid.Columns.Add(DialogUi.Text("Vật liệu", nameof(WallMaterialRow.Name), false, 260));
        _grid.Columns.Add(DialogUi.Num("γ (kN/m³)", nameof(WallMaterialRow.UnitWeight), "0.##"));
        foreach (var m in p.WallMaterials) _rows.Add(new WallMaterialRow(m, () => Changed = true));
        _grid.ItemsSource = _rows;
        var dock = new DockPanel { Margin = new Thickness(8) };
        var top = DialogUi.Row(
            DialogUi.Btn("＋ Thêm", (s, e) => { var m = new WallMaterial { Name = "Vật liệu mới", UnitWeight = 18 }; _p.WallMaterials.Add(m); _rows.Add(new WallMaterialRow(m, () => Changed = true)); Changed = true; }),
            DialogUi.Btn("Xoá", (s, e) =>
            {
                if (_grid.SelectedItem is not WallMaterialRow r) return;
                if (_p.WallTypes.Any(t => t.Layers.Any(l => l.Material == r.M.Name))) { MessageBox.Show(this, "Vật liệu đang dùng trong kiểu tường – không xoá.", Title); return; }
                _p.WallMaterials.Remove(r.M); _rows.Remove(r); Changed = true;
            }));
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);
        var hint = DialogUi.Hint("γ: gạch đặc 18, gạch rỗng 13,5–15, AAC 6–8, bê tông 25, vữa 18, đá granite 27–28, gạch ốp 20 kN/m³.");
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
}

internal sealed class OpeningTypeRow : RowBase
{
    private readonly Project _p;
    private readonly Action _changed;
    public OpeningType T { get; }
    public OpeningTypeRow(Project p, OpeningType t, Action changed) { _p = p; T = t; _changed = changed; }
    private void Done() { RaiseAll(); _changed(); }
    public static readonly string[] Kinds = { "Cửa đi", "Cửa sổ", "Ô thoáng", "Lỗ chờ" };
    public static readonly string[] Operations = { "1 cánh", "2 cánh", "Trượt", "Cố định" };
    public string Name
    {
        get => T.Name;
        set
        {
            var n = (value ?? "").Trim();
            if (n.Length == 0 || n == T.Name || _p.OpeningTypes.Any(q => q != T && q.Name == n)) return;
            foreach (var o in _p.Walls.SelectMany(w => w.Openings ?? new List<WallOpening>()).Where(o => o.Type == T.Name)) o.Type = n;
            T.Name = n; Done();
        }
    }
    public string Kind
    {
        get => Kinds[(int)T.Kind];
        set { int i = Array.IndexOf(Kinds, value); if (i >= 0) { T.Kind = (WallOpeningKind)i; Done(); } }
    }
    public double WidthMm { get => T.Width * 1000; set { if (value >= 100 && value <= 8000) { T.Width = value / 1000; Done(); } } }
    public double HeightMm { get => T.Height * 1000; set { if (value >= 100 && value <= 6000) { T.Height = value / 1000; Done(); } } }
    public double SillMm { get => T.Sill * 1000; set { if (value >= 0 && value <= 5000) { T.Sill = value / 1000; Done(); } } }
    public string Operation { get => T.Operation; set { if (!string.IsNullOrEmpty(value)) { T.Operation = value; Done(); } } }
    public int Uses => _p.Walls.Sum(w => (w.Openings ?? new List<WallOpening>()).Count(o => o.Type == T.Name));
}

/// <summary>Kiểu cửa (họ cửa): D1 900x2200, S1 1200x1400… – kích thước, bậu, cách mở.</summary>
internal sealed class OpeningTypesWindow : Window
{
    private readonly Project _p;
    private readonly DataGrid _grid = DialogUi.NewGrid();
    private readonly ObservableCollection<OpeningTypeRow> _rows = new();
    public bool Changed { get; private set; }

    public OpeningTypesWindow(Project p)
    {
        _p = p;
        WallAssembly.Upgrade(p);
        DialogUi.Setup(this, "Kiểu cửa – cửa đi, cửa sổ, ô thoáng", 900, 480);
        _grid.Columns.Add(DialogUi.Text("Tên kiểu (ký hiệu đầu = thẻ)", nameof(OpeningTypeRow.Name), false, 200));
        _grid.Columns.Add(DialogUi.Combo("Loại", nameof(OpeningTypeRow.Kind), OpeningTypeRow.Kinds, 90));
        _grid.Columns.Add(DialogUi.Num("Rộng (mm)", nameof(OpeningTypeRow.WidthMm), "0"));
        _grid.Columns.Add(DialogUi.Num("Cao (mm)", nameof(OpeningTypeRow.HeightMm), "0"));
        _grid.Columns.Add(DialogUi.Num("Bậu (mm)", nameof(OpeningTypeRow.SillMm), "0"));
        _grid.Columns.Add(DialogUi.Combo("Cách mở", nameof(OpeningTypeRow.Operation), OpeningTypeRow.Operations, 90));
        _grid.Columns.Add(DialogUi.Num("Số cửa đã đặt", nameof(OpeningTypeRow.Uses), "0", true));
        foreach (var t in p.OpeningTypes) _rows.Add(new OpeningTypeRow(p, t, () => Changed = true));
        _grid.ItemsSource = _rows;
        var dock = new DockPanel { Margin = new Thickness(8) };
        var top = DialogUi.Row(
            DialogUi.Btn("＋ Cửa đi", (s, e) => Add(WallOpeningKind.Door)),
            DialogUi.Btn("＋ Cửa sổ", (s, e) => Add(WallOpeningKind.Window)),
            DialogUi.Btn("Xoá", (s, e) =>
            {
                if (_grid.SelectedItem is not OpeningTypeRow r) return;
                if (r.Uses > 0) { MessageBox.Show(this, "Kiểu cửa đang được đặt trên tường – không xoá.", Title); return; }
                _p.OpeningTypes.Remove(r.T); _rows.Remove(r); Changed = true;
            }));
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);
        var hint = DialogUi.Hint("Đặt cửa lên tường: ribbon Kiến trúc → Cửa đi / Cửa sổ, rê chuột lên tường, Space để lật hướng mở, bấm để đặt – tường tự cắt ô cửa, tải tường trừ diện tích cửa.");
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

    private void Add(WallOpeningKind k)
    {
        DialogUi.Commit(_grid);
        string b = k == WallOpeningKind.Door ? "D" : "S";
        int n = 1;
        while (_p.OpeningTypes.Any(t => t.Tag == b + n)) n++;
        var t = k == WallOpeningKind.Door
            ? new OpeningType { Name = $"{b}{n} 900x2200", Kind = k, Width = 0.9, Height = 2.2 }
            : new OpeningType { Name = $"{b}{n} 1200x1400", Kind = k, Width = 1.2, Height = 1.4, Sill = 0.9, Operation = "2 cánh" };
        _p.OpeningTypes.Add(t);
        var r = new OpeningTypeRow(_p, t, () => Changed = true);
        _rows.Add(r);
        _grid.SelectedItem = r;
        Changed = true;
    }
}

/// <summary>Hiển thị / Đồ hoạ (Visibility/Graphics): bật tắt thẻ theo loại cấu kiện, mức chi tiết, tránh chồng nhãn.</summary>
internal sealed class VisibilityGraphicsWindow : Window
{
    public VisibilityGraphicsWindow(PlanVisibility v, Action apply)
    {
        DialogUi.Setup(this, "Hiển thị / Đồ hoạ mặt bằng", 420, 420);
        SizeToContent = SizeToContent.Height;
        var sp = new StackPanel { Margin = new Thickness(12) };
        sp.Children.Add(DialogUi.Label("Thẻ (nhãn) hiển thị", double.NaN, true));
        CheckBox C(string t, bool val, Action<bool> set)
        {
            var c = new CheckBox { Content = t, IsChecked = val, Margin = new Thickness(4, 3, 0, 3) };
            c.Checked += (s, e) => { set(true); apply(); };
            c.Unchecked += (s, e) => { set(false); apply(); };
            sp.Children.Add(c);
            return c;
        }
        C("Thẻ dầm (tên + tiết diện)", v.BeamTags, b => v.BeamTags = b);
        C("Chỉ tên dầm (bỏ tiết diện)", v.BeamNameOnly, b => v.BeamNameOnly = b);
        C("Thẻ cột", v.ColumnTags, b => v.ColumnTags = b);
        C("Thẻ ô sàn", v.SlabTags, b => v.SlabTags = b);
        C("Công năng phòng (WC, BC…)", v.RoomUse, b => v.RoomUse = b);
        C("Thẻ tường (T110, T220)", v.WallTags, b => v.WallTags = b);
        C("Thẻ cửa (D1, S1)", v.DoorTags, b => v.DoorTags = b);
        C("Tự tránh chồng nhãn (ẩn nhãn không còn chỗ)", v.AvoidOverlap, b => v.AvoidOverlap = b);
        sp.Children.Add(DialogUi.Label("Mức chi tiết tường", double.NaN, true));
        var cb = new ComboBox { ItemsSource = PlanVisibility.Details, SelectedItem = v.Detail, Width = 200, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(4, 2, 0, 6) };
        cb.SelectionChanged += (s, e) => { if (cb.SelectedItem is string d) { v.Detail = d; apply(); } };
        sp.Children.Add(cb);
        sp.Children.Add(DialogUi.Hint("Thô: tường tô đặc. Trung bình: tường gạch chéo, viền nối góc gọn. Chi tiết: thêm đường ranh lõi kết cấu / lớp hoàn thiện."));
        var close = DialogUi.Btn("Đóng", (s, e) => Close(), true);
        close.IsCancel = true;
        close.HorizontalAlignment = HorizontalAlignment.Right;
        sp.Children.Add(close);
        Content = sp;
    }
}

/// <summary>Cài đặt hiển thị mặt bằng (Hiển thị / Đồ hoạ).</summary>
internal sealed class PlanVisibility
{
    public bool BeamTags = true, BeamNameOnly, ColumnTags = true, SlabTags = true, RoomUse = true, WallTags = true, DoorTags = true, AvoidOverlap = true;
    public const string Coarse = "Thô", Medium = "Trung bình", Fine = "Chi tiết";
    public static readonly string[] Details = { Coarse, Medium, Fine };
    public string Detail = Medium;
}
