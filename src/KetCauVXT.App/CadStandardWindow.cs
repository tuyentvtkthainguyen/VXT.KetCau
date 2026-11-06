using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Màu ACI (AutoCAD Color Index) → màu RGB gần đúng để hiện ô màu.</summary>
internal static class Aci
{
    private static readonly Color[] Basic =
    {
        Colors.Black, Color.FromRgb(255, 0, 0), Color.FromRgb(255, 255, 0), Color.FromRgb(0, 255, 0), Color.FromRgb(0, 255, 255),
        Color.FromRgb(0, 0, 255), Color.FromRgb(255, 0, 255), Color.FromRgb(40, 40, 40), Color.FromRgb(128, 128, 128), Color.FromRgb(192, 192, 192)
    };

    /// <summary>Các màu hay dùng trong danh sách chọn.</summary>
    public static readonly int[] Common = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 20, 30, 31, 40, 50, 60, 70, 90, 110, 130, 140, 150, 160, 170, 190, 210, 230, 250, 251, 252, 253, 254, 255 };

    public static Color ToColor(int aci)
    {
        if (aci >= 1 && aci <= 9) return Basic[aci];
        if (aci >= 250 && aci <= 255)
        {
            byte g = (byte)new[] { 51, 80, 105, 130, 190, 255 }[aci - 250];
            return Color.FromRgb(g, g, g);
        }
        if (aci < 10 || aci > 249) return Basic[7];
        double h = (aci / 10 - 1) * 15.0;
        int k = aci % 10;
        double v = new[] { 1.0, 1.0, 0.65, 0.65, 0.5, 0.5, 0.3, 0.3, 0.15, 0.15 }[k];
        double sat = k % 2 == 0 ? 1.0 : 0.5;
        // HSV → RGB
        double c = v * sat, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (double r, double g2, double b) = h < 60 ? (c, x, 0.0) : h < 120 ? (x, c, 0.0) : h < 180 ? (0.0, c, x) : h < 240 ? (0.0, x, c) : h < 300 ? (x, 0.0, c) : (c, 0.0, x);
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g2 + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    private static readonly Dictionary<int, SolidColorBrush> _br = new();

    public static SolidColorBrush Brush(int aci)
    {
        if (!_br.TryGetValue(aci, out var b)) { b = new SolidColorBrush(ToColor(aci)); b.Freeze(); _br[aci] = b; }
        return b;
    }

    /// <summary>Ô màu + số ACI (ô xem) / hộp chọn có thể gõ số (ô sửa) cho DataGrid.</summary>
    public static DataGridTemplateColumn Column(string header, string path)
    {
        var col = new DataGridTemplateColumn { Header = header, SortMemberPath = path, Width = 90 };
        var sp = new FrameworkElementFactory(typeof(StackPanel));
        sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var sw = new FrameworkElementFactory(typeof(Border));
        sw.SetValue(FrameworkElement.WidthProperty, 16.0); sw.SetValue(FrameworkElement.HeightProperty, 12.0);
        sw.SetValue(Border.BorderBrushProperty, Brushes.Gray); sw.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        sw.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 4, 0));
        sw.SetBinding(Border.BackgroundProperty, new Binding(path) { Converter = new AciBrushConverter() });
        var tb = new FrameworkElementFactory(typeof(TextBlock));
        tb.SetBinding(TextBlock.TextProperty, new Binding(path));
        sp.AppendChild(sw); sp.AppendChild(tb);
        col.CellTemplate = new DataTemplate { VisualTree = sp };

        var cb = new FrameworkElementFactory(typeof(ComboBox));
        cb.SetValue(ComboBox.IsEditableProperty, true);
        cb.SetValue(ItemsControl.ItemsSourceProperty, Common);
        cb.SetBinding(ComboBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus });
        var isp = new FrameworkElementFactory(typeof(StackPanel));
        isp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var isw = new FrameworkElementFactory(typeof(Border));
        isw.SetValue(FrameworkElement.WidthProperty, 16.0); isw.SetValue(FrameworkElement.HeightProperty, 12.0);
        isw.SetValue(Border.BorderBrushProperty, Brushes.Gray); isw.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        isw.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        isw.SetBinding(Border.BackgroundProperty, new Binding { Converter = new AciBrushConverter() });
        var itb = new FrameworkElementFactory(typeof(TextBlock));
        itb.SetBinding(TextBlock.TextProperty, new Binding());
        isp.AppendChild(isw); isp.AppendChild(itb);
        cb.SetValue(ItemsControl.ItemTemplateProperty, new DataTemplate { VisualTree = isp });
        col.CellEditingTemplate = new DataTemplate { VisualTree = cb };
        return col;
    }
}

internal sealed class AciBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        Aci.Brush(value is int i ? i : int.TryParse(value as string, out int j) ? j : 7);
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Dòng bảng layer xuất (sửa trực tiếp tên, màu, kiểu nét, nét in, cờ in).</summary>
internal sealed class OutputLayerRow : RowBase
{
    private readonly Action _changed;
    public OutputLayer L { get; }
    /// <summary>Layer do người dùng thêm (khoá sửa được, xoá được)</summary>
    public bool Custom { get; }
    public Func<OutputLayerRow, string> Validator { get; set; }

    public OutputLayerRow(OutputLayer l, bool custom, Action changed) { L = l; Custom = custom; _changed = changed; }
    private void Done() { RaiseAll(); _changed?.Invoke(); }

    public string Key { get => L.Key; set { if (Custom && !string.IsNullOrWhiteSpace(value)) { L.Key = value.Trim(); Done(); } } }
    public string Name { get => L.Name; set { L.Name = (value ?? "").Trim(); Done(); } }
    public int Color { get => L.Color; set { if (value >= 1 && value <= 255) { L.Color = value; Done(); } } }
    public string Linetype { get => CadStandard.NormalizeLinetype(L.Linetype); set { L.Linetype = CadStandard.NormalizeLinetype(value); Done(); } }
    public double Lineweight { get => L.Lineweight; set { if (value >= 0 && value <= 2.11) { L.Lineweight = value; Done(); } } }
    public bool Plot { get => L.Plot; set { L.Plot = value; Done(); } }
    public string Description { get => L.Description; set { L.Description = value ?? ""; Done(); } }
    public string Status => Validator?.Invoke(this) ?? "";

    public void Refresh() => RaiseAll();
}

/// <summary>Kiểm tra tên layer xuất: ký tự cấm DXF, rỗng, trùng tên (= gộp layer).</summary>
internal static class LayerNameCheck
{
    public const string Illegal = "<>/\\\":;?*|=`,";

    public static string BadChars(string name) => new string((name ?? "").Where(c => Illegal.IndexOf(c) >= 0 || c < 32).Distinct().ToArray());

    public static string Status(OutputLayerRow r, IEnumerable<OutputLayerRow> all)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "⚠ Tên rỗng";
        string bad = BadChars(r.Name);
        if (bad.Length > 0) return "⚠ Ký tự cấm: " + bad;
        if (r.Name.Length > 255) return "⚠ Tên dài quá 255 ký tự";
        var same = all.Where(o => o != r && string.Equals(o.Name, r.Name, StringComparison.OrdinalIgnoreCase)).Select(o => o.Key).ToList();
        return same.Count > 0 ? "Gộp với: " + string.Join(", ", same) : "";
    }

    /// <summary>Danh sách lỗi chặn khi lưu/xuất (không tính trùng tên vì trùng = gộp).</summary>
    public static List<string> Errors(IEnumerable<OutputLayerRow> rows) =>
        rows.Select(r => (r, s: Status(r, rows))).Where(t => t.s.StartsWith("⚠")).Select(t => $"{t.r.Key}: {t.s.TrimStart('⚠', ' ')}").ToList();

    public static void AutoFix(IEnumerable<OutputLayerRow> rows)
    {
        foreach (var r in rows) r.Name = CadStandard.SafeLayerName(string.IsNullOrWhiteSpace(r.Name) ? r.Key : r.Name);
    }

    /// <summary>Bảng layer xuất sửa trực tiếp.</summary>
    public static DataGrid NewGrid(bool showLineweight = true)
    {
        var g = DialogUi.NewGrid();
        g.Columns.Add(DialogUi.Text("Layer nội bộ", nameof(OutputLayerRow.Key), false, 110));
        g.Columns.Add(DialogUi.Text("Tên layer xuất", nameof(OutputLayerRow.Name), false, 150));
        g.Columns.Add(Aci.Column("Màu (ACI)", nameof(OutputLayerRow.Color)));
        g.Columns.Add(DialogUi.Combo("Kiểu nét", nameof(OutputLayerRow.Linetype), CadStandard.Linetypes, 110));
        if (showLineweight)
        {
            g.Columns.Add(DialogUi.Num("Nét in (mm)", nameof(OutputLayerRow.Lineweight), "0.00"));
            var plot = new DataGridCheckBoxColumn { Header = "In", Binding = new Binding(nameof(OutputLayerRow.Plot)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged } };
            g.Columns.Add(plot);
            g.Columns.Add(DialogUi.Text("Mô tả", nameof(OutputLayerRow.Description), false, 170));
        }
        var st = DialogUi.Text("Kiểm tra", nameof(OutputLayerRow.Status), true, 180);
        var es = new Style(typeof(TextBlock));
        es.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Firebrick));
        st.ElementStyle = es;
        g.Columns.Add(st);
        return g;
    }

    /// <summary>Đổi tên hàng loạt: tiền tố, hậu tố, tìm–thay (bỏ qua layer "0"). Trả số tên đã đổi; -1 = huỷ.</summary>
    public static int BatchRename(Window owner, IList<OutputLayerRow> rows)
    {
        var w = new Window { Owner = owner, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
        DialogUi.Setup(w, "Đổi tên layer hàng loạt", 440, 260);
        w.MinHeight = 0;
        TextBox T() => new() { Width = 220, Margin = new Thickness(0, 2, 0, 2) };
        var tPre = T(); var tSuf = T(); var tFind = T(); var tRep = T();
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Tìm", 110), tFind));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Thay bằng", 110), tRep));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Thêm tiền tố", 110), tPre));
        sp.Children.Add(DialogUi.Row(DialogUi.Label("Thêm hậu tố", 110), tSuf));
        sp.Children.Add(DialogUi.Hint("Ví dụ: tìm \"_\" thay \"-\", tiền tố \"KC-\" → BE_TONG thành KC-BE-TONG. Layer \"0\" giữ nguyên."));
        bool ok = false;
        var bOk = DialogUi.Btn("Đổi tên", (s, e) => { ok = true; w.DialogResult = true; }, true); bOk.IsDefault = true;
        var bNo = DialogUi.Btn("Huỷ", (s, e) => w.DialogResult = false); bNo.IsCancel = true;
        var br = DialogUi.Row(bOk, bNo); br.HorizontalAlignment = HorizontalAlignment.Right;
        sp.Children.Add(br);
        w.Content = sp;
        if (w.ShowDialog() != true || !ok) return -1;
        int n = 0;
        foreach (var r in rows)
        {
            if (r.Key == "0" || r.Name == "0") continue;
            string s = r.Name ?? "";
            if (tFind.Text.Length > 0) s = s.Replace(tFind.Text, tRep.Text, StringComparison.OrdinalIgnoreCase);
            s = tPre.Text + s + tSuf.Text;
            if (s != r.Name) { r.Name = s; n++; }
        }
        return n;
    }
}

/// <summary>Dòng bảng quy tắc nhận diện.</summary>
internal sealed class RuleRow : RowBase
{
    public LayerRule R { get; }
    public RuleRow(LayerRule r) { R = r; }
    public bool Enabled { get => R.Enabled; set { R.Enabled = value; RaiseAll(); } }
    public string Pattern { get => R.Pattern; set { R.Pattern = value ?? ""; RaiseAll(); } }
    public string Match { get => MatchLabel(R.Match); set { R.Match = MatchOf(value); RaiseAll(); } }
    public string Category { get => CadCategory.Label(R.Category); set { string c = CadCategory.Normalize(value); if (c.Length > 0) R.Category = c; RaiseAll(); } }
    public double Priority { get => R.Priority; set { R.Priority = (int)Math.Round(value); RaiseAll(); } }
    public string Note { get => R.Note; set { R.Note = value ?? ""; RaiseAll(); } }
    public string Status => R.Validate() ?? "";

    public static readonly string[] MatchLabels = { "Đúng tên", "Chứa", "Mẫu * ?", "Biểu thức chính quy" };
    public static string MatchLabel(LayerMatchKind k) => MatchLabels[(int)k];
    public static LayerMatchKind MatchOf(string s) { int i = Array.IndexOf(MatchLabels, s); return i < 0 ? LayerMatchKind.Contains : (LayerMatchKind)i; }
}

/// <summary>Dòng bảng gán trực tiếp layer → nhóm.</summary>
internal sealed class MapRow : RowBase
{
    public LayerMapEntry M { get; }
    public MapRow(LayerMapEntry m) { M = m; }
    public string Layer { get => M.Layer; set { M.Layer = (value ?? "").Trim(); RaiseAll(); } }
    public string Category { get => CadCategory.Label(M.Category); set { string c = CadCategory.Normalize(value); if (c.Length > 0) M.Category = c; RaiseAll(); } }
}

/// <summary>Cửa sổ "Chuẩn layer CAD": nhận diện layer kiến trúc (quy tắc + gán trực tiếp) và layer bản vẽ kết cấu (tên/màu/kiểu nét…),
/// font chữ; nhập/xuất JSON, lưu làm mặc định (%AppData%\KetCauVXT\CadStandard.json), lưu riêng cho dự án.</summary>
internal sealed class CadStandardWindow : Window
{
    private readonly Project _p;
    private CadStandard _std;
    private readonly TabControl _tabs = new() { Margin = new Thickness(0, 0, 0, 4) };
    private readonly ObservableCollection<RuleRow> _rules = new();
    private readonly ObservableCollection<MapRow> _maps = new();
    private readonly ObservableCollection<OutputLayerRow> _outs = new();
    private readonly CheckAllBar _rulesAll, _outAll;
    private readonly DataGrid _gRules, _gMaps, _gOut;
    private readonly ComboBox _cbPreset, _cbFont, _cbReportFont;
    private readonly ComboBox _cbFormat = new() { Width = 200, ToolTip = "Định dạng file khi xuất bản vẽ: DXF R12 (tương thích cũ, hatch mô phỏng bằng nét) | DXF 2018 / DWG 2018 (HATCH thật, LWPOLYLINE, nét in theo layer)" };
    private readonly CheckBox _chkSkipOff, _chkProject, _chkAsk;
    private readonly TextBox _txtTest;
    private readonly TextBlock _txtTestRes, _txtStatus;
    private bool _loading;

    /// <summary>Đã bấm "Đồng ý" và có thay đổi</summary>
    public bool Changed { get; private set; }

    public CadStandardWindow(Project p)
    {
        _p = p;
        DialogUi.Setup(this, "Chuẩn layer CAD", 1120, 680);
        Help.Attach(this, "ho-so.chuan-layer");   // nút "?" + F1 – Help.cs
        _std = CadStandard.EffectiveFor(p);

        // ================= TAB 1: nhận diện layer kiến trúc =================
        _gRules = DialogUi.NewGrid();
        _gRules.Columns.Add(new DataGridCheckBoxColumn { Header = "Dùng", Binding = new Binding(nameof(RuleRow.Enabled)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged } });
        _gRules.Columns.Add(DialogUi.Text("Mẫu tên layer", nameof(RuleRow.Pattern), false, 230));
        _gRules.Columns.Add(DialogUi.Combo("Cách so", nameof(RuleRow.Match), RuleRow.MatchLabels, 140));
        _gRules.Columns.Add(DialogUi.Combo("Nhóm cấu kiện", nameof(RuleRow.Category), CadCategory.All.Select(CadCategory.Label).ToList(), 120));
        _gRules.Columns.Add(DialogUi.Num("Ưu tiên", nameof(RuleRow.Priority), "0"));
        _gRules.Columns.Add(DialogUi.Text("Ghi chú", nameof(RuleRow.Note), false, 200));
        _gRules.Columns.Add(DialogUi.Text("Kiểm tra", nameof(RuleRow.Status), true, 160));
        _gRules.ItemsSource = _rules;

        _gMaps = DialogUi.NewGrid();
        _gMaps.Columns.Add(DialogUi.Text("Tên layer (đúng tên)", nameof(MapRow.Layer), false, 200));
        _gMaps.Columns.Add(DialogUi.Combo("Nhóm cấu kiện", nameof(MapRow.Category), CadCategory.All.Select(CadCategory.Label).ToList(), 120));
        _gMaps.ItemsSource = _maps;

        _txtTest = new TextBox { Width = 200, VerticalContentAlignment = VerticalAlignment.Center };
        _txtTestRes = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), FontWeight = FontWeights.Bold };
        _txtTest.TextChanged += (s, e) => TestLayer();
        _chkSkipOff = new CheckBox { Content = "Bỏ qua layer đang tắt (off)", Margin = new Thickness(0, 4, 0, 4) };

        var t1 = new DockPanel { Margin = new Thickness(6) };
        var t1Top = new StackPanel();
        t1Top.Children.Add(DialogUi.Hint("Mỗi layer bản vẽ kiến trúc được xếp vào một nhóm: Tường, Cột, Dầm, Sàn, Trục, Cửa, Cầu thang, Tên phòng hoặc Bỏ qua. " +
                                         "Gán trực tiếp (bảng dưới) được ưu tiên trước; sau đó là quy tắc có ưu tiên lớn nhất. So khớp không phân biệt hoa thường, bỏ dấu; " +
                                         "tên xref \"NHA|KT-TUONG\", \"NHA$0$KT-TUONG\" được so cả phần \"KT-TUONG\"."));
        t1Top.Children.Add(DialogUi.Row(DialogUi.Btn("＋ Quy tắc", (s, e) => AddRule()), DialogUi.Btn("Xoá", (s, e) => DeleteRules()),
            DialogUi.Btn("↑", (s, e) => MoveRule(-1)), DialogUi.Btn("↓", (s, e) => MoveRule(1)),
            DialogUi.Btn("Quy tắc mặc định", (s, e) => { _std.InputRules = CadStandard.DefaultInputRules(); LoadRules(); }),
            DialogUi.Label("   Thử tên layer:"), _txtTest, _txtTestRes));
        // ô "Tất cả" cho cột "Dùng" của quy tắc
        _rulesAll = new CheckAllBar(() => _gRules.Items.OfType<RuleRow>().Select(r => r.Enabled).ToList(), f =>
        {
            DialogUi.Commit(_gRules);
            var list = _gRules.Items.OfType<RuleRow>().ToList();
            for (int i = 0; i < list.Count; i++) list[i].Enabled = f(i, list[i].Enabled);
            TestLayer();
        }, "Dùng tất cả quy tắc").Watch(_gRules, false);
        t1Top.Children.Add(_rulesAll);
        DockPanel.SetDock(t1Top, Dock.Top); t1.Children.Add(t1Top);
        var mapBox = new DockPanel { Height = 170 };
        var mapTop = DialogUi.Row(DialogUi.Label("Gán trực tiếp theo tên layer", double.NaN, true),
            DialogUi.Btn("＋ Gán", (s, e) => { var m = new LayerMapEntry("TEN_LAYER", CadCategory.Tuong); _std.InputLayerMap.Add(m); _maps.Add(new MapRow(m)); }),
            DialogUi.Btn("Xoá", (s, e) => DeleteMaps()), _chkSkipOff);
        DockPanel.SetDock(mapTop, Dock.Top); mapBox.Children.Add(mapTop); mapBox.Children.Add(_gMaps);
        DockPanel.SetDock(mapBox, Dock.Bottom); t1.Children.Add(mapBox);
        t1.Children.Add(_gRules);
        _tabs.Items.Add(new TabItem { Header = "Nhận diện layer kiến trúc", Content = t1 });

        // ================= TAB 2: layer bản vẽ kết cấu =================
        _gOut = LayerNameCheck.NewGrid();
        _gOut.ItemsSource = _outs;
        _cbPreset = new ComboBox { Width = 200, ItemsSource = CadStandard.PresetNames.Append(CadStandard.PresetUserName).ToList() };
        _cbPreset.SelectionChanged += (s, e) => ApplyPreset();
        _cbFont = new ComboBox { Width = 150, ItemsSource = CadStandard.Fonts };
        _cbReportFont = new ComboBox { Width = 150, ItemsSource = CadStandard.Fonts };
        _cbFont.SelectionChanged += (s, e) => { if (!_loading && _cbFont.SelectedItem is string f) _std.FontName = f; };
        _cbReportFont.SelectionChanged += (s, e) => { if (!_loading && _cbReportFont.SelectedItem is string f) _std.ReportFont = f; };
        foreach (var f in CadOutputFormats.All) _cbFormat.Items.Add(CadOutputFormats.Label(f));
        _cbFormat.SelectionChanged += (s, e) => { if (!_loading && _cbFormat.SelectedIndex >= 0) _std.OutputFormat = CadOutputFormats.All[_cbFormat.SelectedIndex]; };
        _chkAsk = new CheckBox { Content = "Hỏi \"Xem/đổi tên layer\" mỗi lần xuất DXF", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        _chkAsk.Checked += (s, e) => { if (!_loading) _std.AskLayersBeforeExport = true; };
        _chkAsk.Unchecked += (s, e) => { if (!_loading) _std.AskLayersBeforeExport = false; };

        var t2 = new DockPanel { Margin = new Thickness(6) };
        var t2Top = new StackPanel();
        t2Top.Children.Add(DialogUi.Hint("Đổi tên / màu / kiểu nét layer khi ghi DXF. Sửa trực tiếp trong bảng (bấm đúp ô). Nhiều layer nội bộ cùng một tên xuất = gộp layer. " +
                                         "Nét in và cờ \"In\" được lưu trong chuẩn cho add-in AutoCAD / bảng nét in CTB (DXF R12 không lưu được)."));
        t2Top.Children.Add(DialogUi.Row(DialogUi.Label("Mẫu có sẵn"), _cbPreset, DialogUi.Label("   Font bản vẽ"), _cbFont,
            DialogUi.Label("   Font thuyết minh Word"), _cbReportFont, _chkAsk));
        t2Top.Children.Add(DialogUi.Row(DialogUi.Label("Định dạng xuất"), _cbFormat));
        t2Top.Children.Add(DialogUi.Row(DialogUi.Btn("＋ Thêm layer", (s, e) => AddOut()), DialogUi.Btn("Xoá layer thêm", (s, e) => DeleteOut()),
            DialogUi.Btn("Gộp layer…", (s, e) => MergeOut()), DialogUi.Btn("Đổi tên hàng loạt…", (s, e) => BatchRename()),
            DialogUi.Btn("Sửa ký tự cấm", (s, e) => { LayerNameCheck.AutoFix(_outs); RefreshOut(); })));
        // ô "Tất cả" cho cột "In" của layer xuất – đổi cả lô rồi cập nhật kiểm tra một lần
        _outAll = new CheckAllBar(() => _gOut.Items.OfType<OutputLayerRow>().Select(r => r.Plot).ToList(), f =>
        {
            DialogUi.Commit(_gOut);
            var list = _gOut.Items.OfType<OutputLayerRow>().ToList();
            for (int i = 0; i < list.Count; i++) list[i].L.Plot = f(i, list[i].L.Plot);
            OutChanged();
        }, "In tất cả layer").Watch(_gOut, false);
        t2Top.Children.Add(_outAll);
        DockPanel.SetDock(t2Top, Dock.Top); t2.Children.Add(t2Top);
        t2.Children.Add(_gOut);
        _tabs.Items.Add(new TabItem { Header = "Layer bản vẽ kết cấu", Content = t2 });

        // ================= dưới =================
        _chkProject = new CheckBox { Content = "Lưu riêng cho dự án này", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), IsChecked = p?.CadStandard != null };
        _txtStatus = new TextBlock { Foreground = Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        var ok = DialogUi.Btn("Đồng ý", (s, e) => Ok(), true);
        var cancel = DialogUi.Btn("Huỷ", (s, e) => Close());
        var bottom = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        var left = DialogUi.Row(DialogUi.Btn("Nhập JSON…", (s, e) => ImportJson()), DialogUi.Btn("Xuất JSON…", (s, e) => ExportJson()),
            DialogUi.Btn("Lưu làm mặc định", (s, e) => SaveDefault(), true), DialogUi.Btn("Khôi phục mặc định", (s, e) => RestoreDefault()), _chkProject);
        var right = DialogUi.Row(ok, cancel);
        DockPanel.SetDock(left, Dock.Left); DockPanel.SetDock(right, Dock.Right);
        bottom.Children.Add(left); bottom.Children.Add(right); bottom.Children.Add(_txtStatus);

        var root = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom); root.Children.Add(_tabs);
        Content = root;
        LoadAll();
        SetStatus((p?.CadStandard != null ? "Đang dùng chuẩn riêng của dự án." : "Đang dùng chuẩn mặc định của người dùng.") + " File mặc định: " + CadStandard.UserFilePath);
    }

    public void SelectTab(int i) { if (i >= 0 && i < _tabs.Items.Count) _tabs.SelectedIndex = i; }

    private void SetStatus(string s) => _txtStatus.Text = s;

    // ------------------------------------------------------------------ nạp
    private void LoadAll()
    {
        _loading = true;
        try
        {
            _std.Normalize();
            LoadRules();
            _maps.Clear();
            foreach (var m in _std.InputLayerMap) _maps.Add(new MapRow(m));
            _chkSkipOff.IsChecked = _std.SkipOffLayers;
            LoadOut();
            _cbPreset.SelectedItem = CadStandard.PresetNames.Contains(_std.OutputPreset) ? _std.OutputPreset : CadStandard.PresetUserName;
            _cbFont.SelectedItem = CadStandard.NormalizeFont(_std.FontName);
            _cbReportFont.SelectedItem = CadStandard.NormalizeFont(_std.ReportFont);
            _chkAsk.IsChecked = _std.AskLayersBeforeExport;
            _cbFormat.SelectedIndex = Math.Max(0, Array.IndexOf(CadOutputFormats.All, _std.OutputFormat));
        }
        finally { _loading = false; }
    }

    private void LoadRules()
    {
        _rules.Clear();
        foreach (var r in _std.InputRules) _rules.Add(new RuleRow(r));
        TestLayer();
        _rulesAll?.Refresh();
    }

    private void LoadOut()
    {
        _outs.Clear();
        foreach (var o in _std.OutputLayers)
        {
            var row = new OutputLayerRow(o, !CadStandard.InternalLayerKeys.Contains(o.Key), OutChanged);
            row.Validator = r => LayerNameCheck.Status(r, _outs);
            _outs.Add(row);
        }
    }

    private void OutChanged()
    {
        if (_loading) return;
        if (_std.OutputPreset != CadStandard.PresetUserName)
        {
            _std.OutputPreset = CadStandard.PresetUserName;
            _loading = true; _cbPreset.SelectedItem = CadStandard.PresetUserName; _loading = false;
        }
        RefreshOut();
    }

    private void RefreshOut()
    {
        foreach (var r in _outs) r.Refresh();
        _outAll?.Refresh();
        var err = LayerNameCheck.Errors(_outs);
        SetStatus(err.Count == 0 ? $"{_outs.Count} layer xuất, {_outs.Select(o => o.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()} tên khác nhau."
                                 : "Lỗi tên layer: " + string.Join("; ", err));
    }

    private void TestLayer()
    {
        if (_txtTestRes == null) return;
        string n = _txtTest.Text.Trim();
        if (n.Length == 0) { _txtTestRes.Text = ""; return; }
        string c = _std.Classify(n, out string why);
        _txtTestRes.Text = c.Length == 0 ? "→ (không nhận)" : $"→ {CadCategory.Label(c)}  ({why})";
    }

    // ------------------------------------------------------------------ tab 1
    private void AddRule()
    {
        var r = new LayerRule("TEN_LAYER", LayerMatchKind.Contains, CadCategory.Tuong, 50);
        _std.InputRules.Add(r); _rules.Add(new RuleRow(r));
        _gRules.SelectedIndex = _rules.Count - 1; _gRules.ScrollIntoView(_gRules.SelectedItem);
    }

    private void DeleteRules()
    {
        DialogUi.Commit(_gRules);
        foreach (var row in _gRules.SelectedItems.OfType<RuleRow>().ToList()) { _std.InputRules.Remove(row.R); _rules.Remove(row); }
        TestLayer();
    }

    private void MoveRule(int d)
    {
        DialogUi.Commit(_gRules);
        if (_gRules.SelectedItem is not RuleRow row) return;
        int i = _rules.IndexOf(row), j = i + d;
        if (j < 0 || j >= _rules.Count) return;
        _rules.Move(i, j);
        _std.InputRules.Remove(row.R); _std.InputRules.Insert(j, row.R);
        _gRules.SelectedItem = row;
    }

    private void DeleteMaps()
    {
        DialogUi.Commit(_gMaps);
        foreach (var row in _gMaps.SelectedItems.OfType<MapRow>().ToList()) { _std.InputLayerMap.Remove(row.M); _maps.Remove(row); }
    }

    // ------------------------------------------------------------------ tab 2
    private void ApplyPreset()
    {
        if (_loading || _cbPreset.SelectedItem is not string name || name == CadStandard.PresetUserName) return;
        var custom = _std.OutputLayers.Where(o => !CadStandard.InternalLayerKeys.Contains(o.Key)).ToList();
        _std.OutputLayers = CadStandard.Preset(name);
        _std.OutputLayers.AddRange(custom);
        _std.OutputPreset = name;
        _loading = true; LoadOut(); _loading = false;
        RefreshOut();
    }

    private void AddOut()
    {
        DialogUi.Commit(_gOut);
        string key = DialogUi.InputBox(this, "Thêm layer", "Tên layer nội bộ (layer thực thể đang dùng, vd. layer tự đặt của đối tượng chú thích trên tờ in):", "KC-MOI");
        if (key == null) return;
        if (_std.OutputLayers.Any(o => string.Equals(o.Key, key, StringComparison.Ordinal))) { SetStatus("Đã có layer nội bộ " + key); return; }
        var o = new OutputLayer(key, CadStandard.SafeLayerName(key), 7, "CONTINUOUS", 0, "Layer thêm");
        _std.OutputLayers.Add(o);
        var row = new OutputLayerRow(o, true, OutChanged) { Validator = r => LayerNameCheck.Status(r, _outs) };
        _outs.Add(row);
        OutChanged();
    }

    private void DeleteOut()
    {
        DialogUi.Commit(_gOut);
        var sel = _gOut.SelectedItems.OfType<OutputLayerRow>().ToList();
        if (sel.Any(r => !r.Custom)) SetStatus("Layer nội bộ của chương trình không xoá được – chỉ đổi tên/màu.");
        foreach (var r in sel.Where(r => r.Custom)) { _std.OutputLayers.Remove(r.L); _outs.Remove(r); }
        OutChanged();
    }

    private void MergeOut()
    {
        DialogUi.Commit(_gOut);
        var sel = _gOut.SelectedItems.OfType<OutputLayerRow>().ToList();
        if (sel.Count < 2) { MessageBox.Show(this, "Chọn ít nhất 2 dòng (giữ Ctrl để chọn nhiều) rồi bấm \"Gộp layer…\".", Title, MessageBoxButton.OK, MessageBoxImage.Information); return; }
        string name = DialogUi.InputBox(this, "Gộp layer", $"Tên layer xuất chung cho {string.Join(", ", sel.Select(r => r.Key))}:", sel[0].Name);
        if (name == null) return;
        foreach (var r in sel) r.Name = name;
        SetStatus($"Đã gộp {sel.Count} layer nội bộ vào \"{name}\" (màu/kiểu nét lấy theo dòng đầu tiên khi ghi DXF).");
    }

    private void BatchRename()
    {
        DialogUi.Commit(_gOut);
        int n = LayerNameCheck.BatchRename(this, _outs);
        if (n >= 0) { RefreshOut(); SetStatus($"Đã đổi {n} tên layer."); }
    }

    // ------------------------------------------------------------------ lưu
    private bool Collect()
    {
        DialogUi.Commit(_gRules); DialogUi.Commit(_gMaps); DialogUi.Commit(_gOut);
        _std.SkipOffLayers = _chkSkipOff.IsChecked == true;
        var bad = _rules.Select(r => r.R.Validate()).Where(s => s != null).ToList();
        if (bad.Count > 0) { MessageBox.Show(this, "Quy tắc lỗi:\n" + string.Join("\n", bad.Distinct()), Title, MessageBoxButton.OK, MessageBoxImage.Warning); _tabs.SelectedIndex = 0; return false; }
        var err = LayerNameCheck.Errors(_outs);
        if (err.Count > 0)
        {
            if (MessageBox.Show(this, "Tên layer xuất không hợp lệ:\n" + string.Join("\n", err) + "\n\nTự sửa (thay ký tự cấm bằng \"_\")?", Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { _tabs.SelectedIndex = 1; return false; }
            LayerNameCheck.AutoFix(_outs);
        }
        _std.Normalize();
        return true;
    }

    private void SaveDefault()
    {
        if (!Collect()) return;
        try { CadStandard.SaveUserDefault(_std); SetStatus("Đã lưu làm mặc định: " + CadStandard.UserFilePath); }
        catch (Exception ex) { MessageBox.Show(this, "Không lưu được: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void RestoreDefault()
    {
        if (MessageBox.Show(this, "Trở về chuẩn mặc định của chương trình (layer Việt Const, font Arial, quy tắc nhận diện mặc định)?\n" +
                                  "File mặc định của người dùng cũng bị xoá.", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        CadStandard.ResetUserDefault();
        var keepDrawing = _std.Drawing;   // giữ Thiết lập bản vẽ (tỷ lệ, cỡ chữ) – khôi phục riêng trong cửa sổ "Thiết lập bản vẽ"
        _std = CadStandard.Default();
        if (keepDrawing != null) _std.Drawing = keepDrawing;
        LoadAll();
        SetStatus("Đã khôi phục mặc định của chương trình.");
    }

    private void ImportJson()
    {
        var dlg = new OpenFileDialog { Filter = "Chuẩn layer CAD (*.json)|*.json", Title = "Nhập chuẩn layer CAD" };
        if (dlg.ShowDialog(this) != true) return;
        try { _std = CadStandard.Load(dlg.FileName); LoadAll(); SetStatus("Đã nhập " + dlg.FileName); }
        catch (Exception ex) { MessageBox.Show(this, "File không hợp lệ: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ExportJson()
    {
        if (!Collect()) return;
        var dlg = new SaveFileDialog { Filter = "Chuẩn layer CAD (*.json)|*.json", FileName = "CadStandard.json", Title = "Xuất chuẩn layer CAD" };
        if (dlg.ShowDialog(this) != true) return;
        try { _std.Save(dlg.FileName); SetStatus("Đã xuất " + dlg.FileName); }
        catch (Exception ex) { MessageBox.Show(this, "Không ghi được: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Ok()
    {
        if (!Collect()) return;
        if (_p != null)
        {
            if (_chkProject.IsChecked == true) _p.CadStandard = _std.Clone();
            else
            {
                _p.CadStandard = null;
                // không lưu riêng: thay đổi chỉ có hiệu lực khi đã lưu làm mặc định – hỏi nếu khác file mặc định
                if (_std.ToJson() != CadStandard.LoadUserDefault().ToJson() &&
                    MessageBox.Show(this, "Chuẩn đã sửa khác chuẩn mặc định. Lưu làm mặc định của người dùng?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    CadStandard.SaveUserDefault(_std);
            }
        }
        if (_p != null) CadStandard.Activate(_p.CadStandard ?? CadStandard.LoadUserDefault());
        else
        {
            // mở từ hộp nhập bản vẽ kiến trúc (không gắn dự án): Đồng ý = lưu làm mặc định của người dùng
            try { CadStandard.SaveUserDefault(_std); }
            catch (Exception ex) { MessageBox.Show(this, "Không lưu được: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); return; }
            if (CadStandard.Active == null || CadStandard.Active.ToJson() == CadStandard.Default().ToJson()) CadStandard.Activate(_std);
        }
        Changed = true;
        Close();
    }
}

/// <summary>"Xem/đổi tên layer trước khi xuất": liệt kê layer sẽ ghi trong lần xuất này, sửa tên/màu/kiểu nét;
/// áp dụng cho lần xuất này hoặc lưu làm mặc định; "Không hỏi lại" ghi vào chuẩn mặc định của người dùng.</summary>
internal sealed class LayerExportDialog : Window
{
    private readonly ObservableCollection<OutputLayerRow> _rows = new();
    private readonly DataGrid _grid;
    private readonly CheckBox _chkNoAsk;
    private readonly TextBlock _status;
    private readonly CadStandard _std;
    private bool _saveDefault;

    private LayerExportDialog(CadStandard std, IEnumerable<string> used, string what)
    {
        _std = std;
        DialogUi.Setup(this, "Xem/đổi tên layer trước khi xuất", 820, 520);
        var usedList = (used ?? CadStandard.InternalLayerKeys).Distinct().ToList();
        foreach (var key in usedList)
        {
            var o = _std.Output(key);
            if (o == null) { o = new OutputLayer(key, CadStandard.SafeLayerName(key), 7, "CONTINUOUS", 0, "Layer thêm"); _std.OutputLayers.Add(o); }
            var r = new OutputLayerRow(o, false, Changed);
            r.Validator = q => LayerNameCheck.Status(q, _rows);
            _rows.Add(r);
        }
        _grid = LayerNameCheck.NewGrid(false);
        _grid.ItemsSource = _rows;
        _chkNoAsk = new CheckBox { Content = "Không hỏi lại (đổi lại trong Tuỳ chọn → Chuẩn layer CAD…)", VerticalAlignment = VerticalAlignment.Center };
        _status = new TextBlock { Foreground = Brushes.DimGray, Margin = new Thickness(0, 2, 0, 2), TextWrapping = TextWrapping.Wrap };

        var top = new StackPanel();
        top.Children.Add(DialogUi.Hint($"{what}: {_rows.Count} layer sẽ được ghi. Sửa trực tiếp tên layer (bấm đúp ô), màu, kiểu nét. Trùng tên = gộp layer."));
        top.Children.Add(DialogUi.Row(DialogUi.Btn("Đổi tên hàng loạt…", (s, e) => { DialogUi.Commit(_grid); LayerNameCheck.BatchRename(this, _rows); Changed(); }),
            DialogUi.Btn("Sửa ký tự cấm", (s, e) => { LayerNameCheck.AutoFix(_rows); Changed(); }),
            DialogUi.Btn("Tên gốc chương trình", (s, e) => { foreach (var r in _rows) r.Name = CadStandard.PresetVxt().FirstOrDefault(x => x.Key == r.Key)?.Name ?? r.Key; Changed(); })));
        var go = DialogUi.Btn("Xuất (chỉ lần này)", (s, e) => Finish(false), true); go.IsDefault = true;
        var save = DialogUi.Btn("Lưu làm mặc định và xuất", (s, e) => Finish(true));
        var cancel = DialogUi.Btn("Huỷ", (s, e) => DialogResult = false); cancel.IsCancel = true;
        var bottom = new DockPanel();
        var br = DialogUi.Row(go, save, cancel);
        DockPanel.SetDock(br, Dock.Right); bottom.Children.Add(br); bottom.Children.Add(_chkNoAsk);

        var root = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(top, Dock.Top); DockPanel.SetDock(bottom, Dock.Bottom); DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(top); root.Children.Add(bottom); root.Children.Add(_status); root.Children.Add(_grid);
        Content = root;
        Changed();
    }

    private void Changed()
    {
        foreach (var r in _rows) r.Refresh();
        var err = LayerNameCheck.Errors(_rows);
        _status.Text = err.Count == 0 ? "" : "Lỗi: " + string.Join("; ", err);
        _status.Foreground = err.Count == 0 ? Brushes.DimGray : Brushes.Firebrick;
    }

    private void Finish(bool saveDefault)
    {
        DialogUi.Commit(_grid);
        var err = LayerNameCheck.Errors(_rows);
        if (err.Count > 0)
        {
            if (MessageBox.Show(this, "Tên layer không hợp lệ:\n" + string.Join("\n", err) + "\n\nTự sửa (thay ký tự cấm bằng \"_\") rồi xuất?", Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            LayerNameCheck.AutoFix(_rows);
        }
        if (_std.OutputLayers.Zip(CadStandard.PresetVxt()).Any(t => t.First.Name != t.Second.Name)) _std.OutputPreset = CadStandard.PresetUserName;
        _saveDefault = saveDefault;
        DialogResult = true;
    }

    /// <summary>Hỏi trước khi xuất DXF. usedLayers: tên layer nội bộ sẽ ghi (ExportLayerProbe.ForDrawingSet / ForSheets / ForWriters).
    /// Trả về chuẩn dùng cho lần xuất (dùng với <c>using (CadStandard.Use(std)) …</c>), null = người dùng huỷ.
    /// Không hỏi khi người dùng đã chọn "Không hỏi lại" hoặc đang tự kiểm tra (--selftest).</summary>
    public static CadStandard Review(Window owner, Project p, IEnumerable<string> usedLayers, string what = "Xuất bản vẽ DXF")
    {
        var cur = CadStandard.Active?.Clone() ?? CadStandard.EffectiveFor(p);
        var userDef = CadStandard.LoadUserDefault();
        if (!userDef.AskLayersBeforeExport || MainWindow.SelfTestOut != null) return cur;
        var dlg = new LayerExportDialog(cur.Clone().Normalize(), usedLayers, what) { Owner = owner };
        if (dlg.ShowDialog() != true) return null;
        var std = dlg._std.Normalize();
        try
        {
            if (dlg._saveDefault)
            {
                var d = p?.CadStandard ?? userDef;
                d.OutputLayers = std.OutputLayers.Select(o => o.Clone()).ToList();
                d.OutputPreset = std.OutputPreset;
                if (p?.CadStandard != null) p.CadStandard = d.Clone();
                else { userDef = d; }
            }
            if (dlg._chkNoAsk.IsChecked == true) userDef.AskLayersBeforeExport = false;
            if (dlg._saveDefault || dlg._chkNoAsk.IsChecked == true) CadStandard.SaveUserDefault(userDef);
            if (dlg._saveDefault) CadStandard.Activate(p?.CadStandard ?? userDef);
        }
        catch (Exception ex) { MessageBox.Show(owner, "Không lưu được chuẩn mặc định: " + ex.Message, "Chuẩn layer CAD", MessageBoxButton.OK, MessageBoxImage.Warning); }
        return std;
    }
}
