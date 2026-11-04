using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Cửa sổ "Khai báo tiết diện dầm/cột" (như Define → Frame Sections của ETABS), mở không chặn (non-modal):
/// danh sách tiết diện, thêm / sao chép / sửa (b, h, vật liệu, hệ số độ cứng, màu) / đổi tên / xoá có bảo vệ (gán lại), nhập từ dự án khác,
/// đặt làm tiết diện hiện hành cho công cụ vẽ. Mọi thay đổi đi qua <see cref="SectionLibrary"/> và báo về cửa sổ chính để lưu hoàn tác.</summary>
internal sealed class SectionManagerWindow : Window
{
    private readonly Func<Project> _getP;
    private readonly Func<string> _snapshot;
    private readonly Action<string, string> _commit;
    private readonly Action<string> _setCurrent;
    private readonly DataGrid _grid;
    private readonly TextBlock _status;
    private readonly CheckBox _chkAuto;
    private ObservableCollection<SectionMgrRow> _rows = new();
    private bool _reloading;

    internal bool AutoRename => _chkAuto.IsChecked == true;
    internal Project P => _getP();

    /// <param name="getP">Dự án hiện hành (có thể bị thay khi hoàn tác).</param>
    /// <param name="snapshot">Ảnh chụp dự án trước khi sửa (để hoàn tác).</param>
    /// <param name="commit">(ảnh chụp trước, thông báo) – cửa sổ chính lưu hoàn tác, đánh dấu thay đổi, vẽ lại.</param>
    /// <param name="setCurrent">Đặt tiết diện hiện hành cho công cụ vẽ dầm.</param>
    public SectionManagerWindow(Func<Project> getP, Func<string> snapshot, Action<string, string> commit, Action<string> setCurrent)
    {
        _getP = getP; _snapshot = snapshot; _commit = commit; _setCurrent = setCurrent;
        DialogUi.Setup(this, "Khai báo tiết diện dầm / cột (Frame Sections)", 1060, 560);
        Help.Attach(this, "mo-hinh.tiet-dien");   // nút "?" + F1 – Help.cs
        ShowInTaskbar = false;
        _grid = DialogUi.NewGrid();
        _grid.SelectionMode = DataGridSelectionMode.Single;
        var mats = Model.Material.ConcreteClasses.Concat(Model.Material.SteelGrades).ToList();
        _grid.Columns.Add(DialogUi.Text("Tên", "Name", true, 90));
        _grid.Columns.Add(DialogUi.Text("Loại", "Kind", true, 90));
        _grid.Columns.Add(DialogUi.Num("b (mm)", "B", "0", false, 62));
        _grid.Columns.Add(DialogUi.Num("h (mm)", "H", "0", false, 62));
        _grid.Columns.Add(DialogUi.Combo("Vật liệu", "Material", mats, 80));
        _grid.Columns.Add(DialogUi.Num("Hệ số I33", "ModI33", "0.###", false, 66));
        _grid.Columns.Add(DialogUi.Num("Hệ số I22", "ModI22", "0.###", false, 66));
        _grid.Columns.Add(DialogUi.Num("Hệ số J", "ModJ", "0.###", false, 60));
        _grid.Columns.Add(DialogUi.Num("Hệ số A", "ModA", "0.###", false, 60));
        var sw = new FrameworkElementFactory(typeof(Border));
        sw.SetBinding(Border.BackgroundProperty, new Binding("Swatch"));
        sw.SetValue(FrameworkElement.WidthProperty, 30.0);
        sw.SetValue(FrameworkElement.HeightProperty, 14.0);
        sw.SetValue(Border.BorderBrushProperty, Brushes.Gray);
        sw.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        _grid.Columns.Add(new DataGridTemplateColumn { Header = "Màu", CellTemplate = new DataTemplate { VisualTree = sw }, Width = 44 });
        _grid.Columns.Add(DialogUi.Num("A (cm²)", "AreaCm2", "0.#", true, 64));
        _grid.Columns.Add(DialogUi.Num("I33 (cm⁴)", "I33Cm4", "0", true, 80));
        _grid.Columns.Add(DialogUi.Num("Số thanh", "Used", "0", true, 60));
        _grid.MouseDoubleClick += (s, e) => { if (_grid.SelectedItem is SectionMgrRow r) SetCurrent(r); };

        _status = new TextBlock { Foreground = Brushes.DarkSlateBlue, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        _chkAuto = new CheckBox { Content = "Tự đổi tên theo kích thước khi sửa b, h (tên dạng D22x40)", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };

        var bar1 = DialogUi.Row(
            DialogUi.Btn("＋ Dầm chữ nhật…", (s, e) => Add("D"), true),
            DialogUi.Btn("＋ Cột chữ nhật…", (s, e) => Add("C")),
            DialogUi.Btn("Sao chép", (s, e) => CopySel()),
            DialogUi.Btn("Đổi tên…", (s, e) => RenameSel()),
            DialogUi.Btn("Tên tự động", (s, e) => AutoNameSel()),
            DialogUi.Btn("Màu…", (s, e) => ColorSel()),
            DialogUi.Btn("Xoá…", (s, e) => DeleteSel()),
            DialogUi.Btn("Nhập từ dự án…", (s, e) => ImportFrom()));
        var bar2 = DialogUi.Row(
            DialogUi.Btn("Dùng làm tiết diện hiện hành", (s, e) => { if (_grid.SelectedItem is SectionMgrRow r) SetCurrent(r); }, true),
            _chkAuto,
            new Border { Width = 20 },
            DialogUi.Btn("Đóng", (s, e) => Close()));
        var dock = new DockPanel { Margin = new Thickness(10) };
        var top = new StackPanel();
        top.Children.Add(bar1); top.Children.Add(bar2);
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);
        var bottom = new StackPanel();
        bottom.Children.Add(DialogUi.Hint("Sửa trực tiếp b, h, vật liệu, hệ số điều chỉnh độ cứng (như Property Modifiers của ETABS: dầm thường lấy I33 = 0,35, cột 0,7 khi phân tích có kể nứt) "
                                           + "trong bảng. Tiết diện chữ T/L: chương trình tính dầm theo chữ nhật b×h (bỏ qua cánh sàn – thiên về an toàn). "
                                           + "Tiết diện đang dùng chỉ xoá được khi gán lại các thanh sang tiết diện khác. Bấm đúp một dòng để đặt làm tiết diện hiện hành."));
        bottom.Children.Add(_status);
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(bottom);
        dock.Children.Add(_grid);
        Content = dock;
        Activated += (s, e) => { if (!_reloading) Reload(); };
        Reload();
    }

    /// <summary>Nạp lại bảng từ dự án hiện hành (giữ dòng đang chọn theo tên).</summary>
    public void Reload(string select = null)
    {
        var p = P;
        if (p == null) return;
        _reloading = true;
        try
        {
            string cur = select ?? (_grid.SelectedItem as SectionMgrRow)?.Name;
            DialogUi.Commit(_grid);
            _rows = new ObservableCollection<SectionMgrRow>(p.FrameSections.Select(s => new SectionMgrRow(this, s)));
            _grid.ItemsSource = _rows;
            var r = _rows.FirstOrDefault(q => q.Name == cur);
            if (r != null) { _grid.SelectedItem = r; _grid.ScrollIntoView(r); }
        }
        finally { _reloading = false; }
    }

    private void SetStatus(string s) => _status.Text = s;

    /// <summary>Thực hiện thao tác sửa: chụp ảnh trước, chạy, báo cửa sổ chính (hoàn tác) nếu thành công.</summary>
    internal bool Do(Func<Project, EditResult> op, string select = null)
    {
        var p = P;
        if (p == null) return false;
        string snap = _snapshot();
        EditResult r;
        try { r = op(p); }
        catch (Exception ex) { SetStatus("Lỗi: " + ex.Message); return false; }
        SetStatus(r.Message);
        if (!r.Ok) return false;
        _commit(snap, r.Message);
        Reload(select);
        return true;
    }

    private SectionMgrRow Sel()
    {
        DialogUi.Commit(_grid);
        if (_grid.SelectedItem is SectionMgrRow r) return r;
        SetStatus("Chọn một tiết diện trong bảng trước.");
        return null;
    }

    private void Add(string prefix)
    {
        var p = P;
        var a = BeamToolDialogs.AskNewSection(this, p, prefix, prefix == "C" ? p.Concrete.ConcreteColumn : p.Concrete.ConcreteBeam);
        if (a == null) return;
        string name = a.Value.Name.Length > 0 ? a.Value.Name : SectionLibrary.Unique(p, SectionLibrary.AutoName(prefix, a.Value.B / 1000, a.Value.H / 1000));
        Do(pp => SectionLibrary.AddRect(pp, name, a.Value.B, a.Value.H, a.Value.Material, prefix), name);
    }

    private void CopySel()
    {
        var r = Sel();
        if (r == null) return;
        string nn = SectionLibrary.Unique(P, r.Name);
        Do(pp => SectionLibrary.Copy(pp, r.Name, nn), nn);
    }

    private void RenameSel()
    {
        var r = Sel();
        if (r == null) return;
        string old = r.Name;
        var nn = DialogUi.InputBox(this, "Đổi tên tiết diện", $"Tên mới cho {old} (các thanh đang dùng được cập nhật):", old);
        if (nn == null || nn == old) return;
        Do(pp => SectionLibrary.Rename(pp, old, nn), nn);
    }

    private void AutoNameSel()
    {
        var r = Sel();
        if (r == null) return;
        if (r.Sec.Shape != SectionShape.RectConcrete) { SetStatus("Chỉ tiết diện chữ nhật BTCT có tên tự động theo kích thước."); return; }
        string old = r.Name;
        string prefix = old.Length > 0 && char.IsLetter(old[0]) ? old[..1].ToUpperInvariant() : "D";
        string nn = SectionLibrary.AutoName(prefix, r.Sec.B, r.Sec.H);
        if (nn == old) { SetStatus("Tên đã theo kích thước."); return; }
        if (P.FrameSections.Any(s => s.Name == nn)) nn = SectionLibrary.Unique(P, nn);
        Do(pp => SectionLibrary.Rename(pp, old, nn), nn);
    }

    private void ColorSel()
    {
        var r = Sel();
        if (r == null) return;
        var c = BeamToolDialogs.AskColor(this, r.Sec.DisplayColor);
        if (c == null) return;
        string name = r.Name;
        Do(pp =>
        {
            var s = pp.FrameSections.FirstOrDefault(q => q.Name == name);
            if (s == null) return EditResult.Fail("Không còn tiết diện " + name);
            s.DisplayColor = c.Length == 0 ? null : c;
            return new EditResult { Ok = true, Message = $"{name}: màu hiển thị {(c.Length == 0 ? "mặc định" : c)}." };
        }, name);
    }

    private void DeleteSel()
    {
        var r = Sel();
        if (r == null) return;
        string name = r.Name;
        int used = SectionLibrary.UsageCount(P, name);
        string to = null;
        if (used > 0)
        {
            to = BeamToolDialogs.AskReassign(this, P, name, used);
            if (to == null) { SetStatus($"Không xoá {name}: đang được {used} thanh sử dụng."); return; }
        }
        else if (MessageBox.Show(this, $"Xoá tiết diện {name}?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Do(pp => SectionLibrary.Delete(pp, name, to), to);
    }

    private void ImportFrom()
    {
        var dlg = new OpenFileDialog { Filter = "Dự án Kết cấu VXT (*.kcvxt)|*.kcvxt|Tất cả (*.*)|*.*", Title = "Nhập tiết diện từ dự án khác" };
        if (dlg.ShowDialog(this) != true) return;
        Project src;
        try { src = Project.Load(dlg.FileName); }
        catch (Exception ex) { SetStatus("Không mở được " + Path.GetFileName(dlg.FileName) + ": " + ex.Message); return; }
        var names = BeamToolDialogs.AskImport(this, src, P);
        if (names == null) return;
        Do(pp => SectionLibrary.Import(pp, src, names), names.FirstOrDefault());
    }

    private void SetCurrent(SectionMgrRow r)
    {
        _setCurrent?.Invoke(r.Name);
        SetStatus($"Tiết diện hiện hành cho công cụ vẽ dầm: {r.Name}.");
    }
}

/// <summary>Dòng bảng tiết diện; sửa ô → SectionLibrary.Modify qua cửa sổ (có hoàn tác).</summary>
internal sealed class SectionMgrRow : RowBase
{
    private readonly SectionManagerWindow _w;
    public FrameSection Sec { get; }
    public SectionMgrRow(SectionManagerWindow w, FrameSection s) { _w = w; Sec = s; }

    public string Name => Sec.Name;
    private bool IsRect => Sec.Shape == SectionShape.RectConcrete;
    public string Kind => Sec.Shape switch
    {
        SectionShape.RectConcrete => Sec.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase) ? "Cột BTCT" : "Dầm BTCT",
        SectionShape.WeldedI => Sec.IsTapered ? "Chữ I vát" : "Chữ I tổ hợp",
        _ => "Tổng quát"
    };

    public double B { get => (IsRect ? Sec.B : Sec.Props(0.5).Width) * 1000; set { if (IsRect) Apply(value, H, Material, ModI33, ModI22, ModJ, ModA); } }
    public double H { get => (IsRect ? Sec.H : Sec.Props(0.5).Depth) * 1000; set { if (IsRect) Apply(B, value, Material, ModI33, ModI22, ModJ, ModA); } }
    public string Material { get => Sec.Material; set => Apply(B, H, value, ModI33, ModI22, ModJ, ModA); }
    public double ModI33 { get => Sec.ModI33; set => Apply(B, H, Material, value, ModI22, ModJ, ModA); }
    public double ModI22 { get => Sec.ModI22; set => Apply(B, H, Material, ModI33, value, ModJ, ModA); }
    public double ModJ { get => Sec.ModJ; set => Apply(B, H, Material, ModI33, ModI22, value, ModA); }
    public double ModA { get => Sec.ModA; set => Apply(B, H, Material, ModI33, ModI22, ModJ, value); }
    public double AreaCm2 => Sec.Props(0.5).A * 1e4;
    public double I33Cm4 => Sec.Props(0.5).I33 * 1e8;
    public int Used => SectionLibrary.UsageCount(_w.P, Sec.Name);

    public Brush Swatch
    {
        get
        {
            if (string.IsNullOrEmpty(Sec.DisplayColor)) return Brushes.Transparent;
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(Sec.DisplayColor)); }
            catch (Exception) { return Brushes.Transparent; }
        }
    }

    private void Apply(double b, double h, string mat, double i33, double i22, double j, double a)
    {
        string name = Sec.Name, color = Sec.DisplayColor;
        bool auto = _w.AutoRename;
        // dispatcher: để DataGrid kết thúc sửa ô trước khi bảng được nạp lại
        _w.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_w.Do(pp => SectionLibrary.Modify(pp, name, b, h, mat, i33, i22, j, a, color, auto))) RaiseAll();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }
}
