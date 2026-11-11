using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Tiện ích giao diện địa chất: tên loại đất, màu / ký hiệu tô hình trụ hố khoan.</summary>
internal static class GeoUi
{
    public static readonly (string Text, SoilKind Kind, SandGrain Grain)[] KindChoices =
    {
        ("Đất đắp", SoilKind.Fill, SandGrain.Medium),
        ("Bùn / đất hữu cơ", SoilKind.Organic, SandGrain.Medium),
        ("Sét", SoilKind.Clay, SandGrain.Medium),
        ("Sét pha", SoilKind.SiltyClay, SandGrain.Medium),
        ("Cát pha", SoilKind.SandyClay, SandGrain.Medium),
        ("Cát bụi", SoilKind.Sand, SandGrain.Silty),
        ("Cát hạt nhỏ", SoilKind.Sand, SandGrain.Fine),
        ("Cát hạt vừa", SoilKind.Sand, SandGrain.Medium),
        ("Cát hạt thô", SoilKind.Sand, SandGrain.Coarse),
        ("Sỏi sạn", SoilKind.Gravel, SandGrain.Coarse),
        ("Đá phong hoá", SoilKind.Rock, SandGrain.Coarse)
    };

    public static readonly string[] KindTexts = KindChoices.Select(k => k.Text).ToArray();

    public const string SrcTable = "Bảng tra TCVN", SrcTest = "Thí nghiệm";
    public static readonly string[] SourceTexts = { SrcTable, SrcTest };
    /// <summary>Lớp cấu tạo tiêu chuẩn (SoilStandard.Catalog) + "(nhập tay)"</summary>
    public const string StdNone = "(tự nhập)";
    public static readonly string[] StdTexts = new[] { StdNone }.Concat(SoilStandard.Catalog.Select(s => s.Name)).ToArray();

    public static (SoilKind Kind, SandGrain Grain) ParseKind(string text)
    {
        foreach (var k in KindChoices) if (k.Text == text) return (k.Kind, k.Grain);
        return (SoilKind.SiltyClay, SandGrain.Medium);
    }

    public static Color KindColor(SoilKind k) => k switch
    {
        SoilKind.Fill => Color.FromRgb(190, 170, 150),
        SoilKind.Organic => Color.FromRgb(120, 130, 120),
        SoilKind.Clay => Color.FromRgb(200, 150, 100),
        SoilKind.SiltyClay => Color.FromRgb(222, 184, 135),
        SoilKind.SandyClay => Color.FromRgb(236, 210, 160),
        SoilKind.Sand => Color.FromRgb(250, 232, 150),
        SoilKind.Gravel => Color.FromRgb(225, 200, 120),
        SoilKind.Rock => Color.FromRgb(170, 170, 185),
        _ => Colors.LightGray
    };

    private static readonly Dictionary<string, Brush> Cache = new();

    /// <summary>Ký hiệu tô lớp đất: nền màu theo loại + nét (đắp: ô chéo; bùn: gạch ngang; sét: gạch chéo; cát: chấm; sỏi: vòng tròn; đá: ô gạch).</summary>
    public static Brush KindBrush(SoilKind k, SandGrain g)
    {
        string key = k + "|" + g;
        if (Cache.TryGetValue(key, out var b)) return b;
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(70, 60, 50)), 0.7);
        pen.Freeze();
        var grp = new GeometryGroup();
        double s = 10;
        grp.Children.Add(new RectangleGeometry(new Rect(0, 0, s, s)));
        var lines = new GeometryGroup();
        switch (k)
        {
            case SoilKind.Fill:
                lines.Children.Add(new LineGeometry(new Point(0, 0), new Point(s, s)));
                lines.Children.Add(new LineGeometry(new Point(s, 0), new Point(0, s)));
                break;
            case SoilKind.Organic:
                lines.Children.Add(new LineGeometry(new Point(1, 3), new Point(5, 3)));
                lines.Children.Add(new LineGeometry(new Point(5, 8), new Point(9, 8)));
                break;
            case SoilKind.Clay:
                lines.Children.Add(new LineGeometry(new Point(0, s), new Point(s, 0)));
                lines.Children.Add(new LineGeometry(new Point(0, s / 2), new Point(s / 2, 0)));
                break;
            case SoilKind.SiltyClay:
                lines.Children.Add(new LineGeometry(new Point(0, s), new Point(s, 0)));
                break;
            case SoilKind.SandyClay:
                lines.Children.Add(new LineGeometry(new Point(0, s), new Point(s, 0)));
                lines.Children.Add(new EllipseGeometry(new Point(2.5, 2.5), 0.6, 0.6));
                break;
            case SoilKind.Sand:
                double r = g is SandGrain.Fine or SandGrain.Silty ? 0.5 : g is SandGrain.Coarse or SandGrain.Gravelly ? 1.0 : 0.75;
                lines.Children.Add(new EllipseGeometry(new Point(2.5, 2.5), r, r));
                lines.Children.Add(new EllipseGeometry(new Point(7.5, 7.5), r, r));
                break;
            case SoilKind.Gravel:
                lines.Children.Add(new EllipseGeometry(new Point(3, 3), 2, 1.6));
                lines.Children.Add(new EllipseGeometry(new Point(7.5, 7.5), 1.6, 1.3));
                break;
            case SoilKind.Rock:
                lines.Children.Add(new LineGeometry(new Point(0, s / 2), new Point(s, s / 2)));
                lines.Children.Add(new LineGeometry(new Point(s / 2, 0), new Point(s / 2, s / 2)));
                break;
        }
        var bg = new SolidColorBrush(KindColor(k)); bg.Freeze();
        var dg = new DrawingGroup();
        dg.Children.Add(new GeometryDrawing(bg, null, new RectangleGeometry(new Rect(0, 0, s, s))));
        dg.Children.Add(new GeometryDrawing(Brushes.Transparent, pen, lines));
        var brush = new DrawingBrush(dg)
        {
            TileMode = TileMode.Tile, Viewport = new Rect(0, 0, s, s), ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, s, s), ViewboxUnits = BrushMappingMode.Absolute
        };
        brush.Freeze();
        Cache[key] = brush;
        return brush;
    }
}

/// <summary>Dòng bảng lớp đất (sửa trực tiếp vào SoilLayer).</summary>
internal sealed class GeoLayerRow : RowBase
{
    public readonly SoilLayer L;
    private readonly Action _changed;

    public GeoLayerRow(SoilLayer l, Action changed) { L = l; _changed = changed; }

    private void Changed([CallerMemberName] string name = null) { Raise(name); _changed?.Invoke(); }

    public string Name { get => L.Name; set { L.Name = value ?? ""; Changed(); } }
    public double Top { get => L.Top; set { L.Top = Math.Max(value, 0); Changed(); } }
    public double Bottom { get => L.Bottom; set { L.Bottom = Math.Max(value, 0); Changed(); } }
    public string Kind
    {
        get => L.KindText;
        set { var k = GeoUi.ParseKind(value); L.Kind = k.Kind; L.Grain = k.Grain; L.StdName = ""; Recalc(); Changed(); }
    }
    /// <summary>[Bảng tra] Lớp cấu tạo tiêu chuẩn: chọn → loại đất, e, IL, Sr, N mặc định của trạng thái và chỉ tiêu theo bảng tra TCVN 9362:2012</summary>
    public string Std
    {
        get => string.IsNullOrEmpty(L.StdName) ? GeoUi.StdNone : L.StdName;
        set
        {
            var s = SoilStandard.Find(value);
            if (s == null) { L.StdName = ""; Changed(); return; }
            SoilStandard.ApplyStd(L, s);
            RaiseAll(); _changed?.Invoke();
        }
    }
    /// <summary>Nguồn chỉ tiêu: bảng tra (tính lại khi đổi loại đất / e / IL / Sr) hoặc thí nghiệm (nhập tay)</summary>
    public string Source
    {
        get => L.Source == SoilPropSource.Table ? GeoUi.SrcTable : GeoUi.SrcTest;
        set
        {
            var src = value == GeoUi.SrcTable ? SoilPropSource.Table : SoilPropSource.Test;
            if (src == L.Source) return;
            L.Source = src;
            if (src == SoilPropSource.Table) Recalc();
            RaiseAll(); _changed?.Invoke();
        }
    }
    /// <summary>Lớp theo bảng tra: tính lại γ, φ, c, E, R0 từ loại đất, e, IL, Sr</summary>
    private void Recalc()
    {
        if (L.Source != SoilPropSource.Table) return;
        SoilStandard.ApplyTo(L);
        RaiseAll();
    }
    /// <summary>Sửa trực tiếp chỉ tiêu cường độ / biến dạng → chuyển sang "Thí nghiệm" (có hố khoan khảo sát)</summary>
    private void Override(Action set)
    {
        set();
        if (L.Source == SoilPropSource.Table) { L.Source = SoilPropSource.Test; L.R0 = 0; RaiseAll(); }
        Changed();
    }
    public double Gamma { get => L.Gamma; set { if (Math.Abs(value - L.Gamma) > 1e-9) Override(() => L.Gamma = value); } }
    public double GammaSat { get => L.GammaSat; set { if (Math.Abs(value - L.GammaSat) > 1e-9) Override(() => L.GammaSat = Math.Max(value, 0)); } }
    public double Phi { get => L.Phi; set { if (Math.Abs(value - L.Phi) > 1e-9) Override(() => L.Phi = Math.Clamp(value, 0, 50)); } }
    public double C { get => L.C; set { if (Math.Abs(value - L.C) > 1e-9) Override(() => L.C = Math.Max(value, 0)); } }
    public double E { get => L.E; set { if (Math.Abs(value - L.E) > 1e-9) Override(() => L.E = Math.Max(value, 0)); } }
    public double N { get => L.N; set { L.N = Math.Max(value, 0); Changed(); } }
    public double IL { get => L.IL; set { L.IL = value; Recalc(); Changed(); } }
    public double VoidRatio { get => L.VoidRatio; set { L.VoidRatio = Math.Max(value, 0); Recalc(); Changed(); } }
    public double Sr { get => L.Sr; set { L.Sr = Math.Clamp(value, 0, 1); Recalc(); Changed(); } }
    public string R0 => L.R0 > 0 ? L.R0.ToString("0", System.Globalization.CultureInfo.InvariantCulture) : "–";
    public string Description { get => L.Description; set { L.Description = value ?? ""; Changed(); } }
    public void RefreshAll() => RaiseAll();
}

/// <summary>Tab "Địa chất": hố khoan, lớp đất, hình trụ hố khoan, áp dụng cho thiết kế móng, gợi ý phương án móng.
/// Giao diện dựng bằng mã trong TabItem tabGeo (MainWindow.xaml).</summary>
public partial class MainWindow
{
    private const string GeoNearest = "(Hố khoan gần chân cột nhất)";
    private bool _geoBuilt, _geoLoading;
    private ListBox _geoList;
    private TextBox _geoName, _geoX, _geoY, _geoElev, _geoWater;
    private DataGrid _geoGrid;
    private readonly ObservableCollection<GeoLayerRow> _geoRows = new();
    private Canvas _geoCanvas;
    private CheckBox _geoUse, _geoGood;
    private ComboBox _geoApplyHole;
    private TextBlock _geoHint;

    private static readonly CultureInfo GeoInv = CultureInfo.InvariantCulture;
    private static string GF(double v, string fmt = "0.##") => double.IsFinite(v) ? v.ToString(fmt, GeoInv) : "-";

    private Borehole GeoSel =>
        _p != null && _geoList != null && _geoList.SelectedIndex >= 0 && _geoList.SelectedIndex < _p.Boreholes.Count ? _p.Boreholes[_geoList.SelectedIndex] : null;

    private static string HoleLabel(Borehole b) =>
        $"{b.Name}  (X = {GF(b.X)}, Y = {GF(b.Y)}; {b.Layers.Count} lớp, sâu {GF(b.Depth, "0.0")} m{(b.WaterDepth >= 0 ? ", MNN " + GF(b.WaterDepth) + " m" : "")})";

    // ------------------------------------------------------------------
    private void BuildGeoTab()
    {
        _geoBuilt = true;
        var root = new StackPanel { Margin = new Thickness(6) };

        // ---- hố khoan ----
        _geoList = new ListBox { Height = 86, Margin = new Thickness(0, 2, 0, 4) };
        _geoList.SelectionChanged += (s, e) => { if (!_geoLoading) LoadGeoHole(); };
        TextBox Box() { var t = new TextBox { Width = 80, Margin = new Thickness(0, 0, 10, 0), VerticalContentAlignment = VerticalAlignment.Center }; t.LostFocus += (s, e) => GeoFieldsToHole(); return t; }
        _geoName = new TextBox { Width = 90, Margin = new Thickness(0, 0, 10, 0) };
        _geoName.LostFocus += (s, e) => GeoFieldsToHole();
        _geoX = Box(); _geoY = Box(); _geoElev = Box(); _geoWater = Box();
        var holes = new StackPanel();
        holes.Children.Add(DialogUi.Row(
            DialogUi.Btn("+ Hố khoan", (s, e) => GeoAddHole(false)),
            DialogUi.Btn("Nhân bản", (s, e) => GeoAddHole(true)),
            DialogUi.Btn("Xoá", (s, e) => GeoDeleteHole())));
        holes.Children.Add(_geoList);
        holes.Children.Add(DialogUi.Row(DialogUi.Label("Tên"), _geoName, DialogUi.Label("X (m)"), _geoX, DialogUi.Label("Y (m)"), _geoY));
        holes.Children.Add(DialogUi.Row(DialogUi.Label("Cao độ miệng (m)"), _geoElev, DialogUi.Label("Mực nước ngầm sâu (m, −1 = không gặp)"), _geoWater));
        root.Children.Add(new GroupBox { Header = "Hố khoan khảo sát địa chất", Content = holes });

        // ---- lớp đất ----
        _geoGrid = DialogUi.NewGrid();
        _geoGrid.Height = 230;
        DialogUi.HScroll(_geoGrid);   // thanh trượt ngang luôn hiện, cột "Tên lớp" cố định, Shift + lăn chuột cuộn ngang
        _geoGrid.ItemsSource = _geoRows;
        _geoGrid.Columns.Add(DialogUi.Text("Tên lớp", "Name", false, 120));
        _geoGrid.Columns.Add(DialogUi.Num("Từ (m)", "Top", "0.0#"));
        _geoGrid.Columns.Add(DialogUi.Num("Đến (m)", "Bottom", "0.0#"));
        _geoGrid.Columns.Add(DialogUi.Combo("Lớp tiêu chuẩn (tra bảng)", "Std", GeoUi.StdTexts, 170));
        _geoGrid.Columns.Add(DialogUi.Combo("Chỉ tiêu theo", "Source", GeoUi.SourceTexts, 100));
        _geoGrid.Columns.Add(DialogUi.Combo("Loại đất", "Kind", GeoUi.KindTexts, 100));
        _geoGrid.Columns.Add(DialogUi.Num("e", "VoidRatio", "0.##"));
        _geoGrid.Columns.Add(DialogUi.Num("IL (−1 = rời)", "IL", "0.##"));
        _geoGrid.Columns.Add(DialogUi.Num("Sr", "Sr", "0.##"));
        _geoGrid.Columns.Add(DialogUi.Num("γ (kN/m³)", "Gamma", "0.0#"));
        _geoGrid.Columns.Add(DialogUi.Num("γsat (0 = γ+1)", "GammaSat", "0.0#"));
        _geoGrid.Columns.Add(DialogUi.Num("φ (°)", "Phi", "0.#"));
        _geoGrid.Columns.Add(DialogUi.Num("c (kPa)", "C", "0.#"));
        _geoGrid.Columns.Add(DialogUi.Num("E (MPa)", "E", "0.#"));
        _geoGrid.Columns.Add(DialogUi.Text("R0 bảng (kPa)", "R0", true, 70));
        _geoGrid.Columns.Add(DialogUi.Num("N (SPT)", "N", "0.#"));
        _geoGrid.Columns.Add(DialogUi.Text("Mô tả", "Description", false, 160));
        _geoGrid.SelectionChanged += (s, e) => UpdateGeoHint();
        _geoHint = DialogUi.Hint("");
        var layers = new StackPanel();
        layers.Children.Add(DialogUi.Wrap(
            DialogUi.Btn("+ Lớp", (s, e) => GeoAddLayer()),
            DialogUi.Btn("Xoá lớp", (s, e) => GeoDeleteLayer()),
            DialogUi.Btn("Sắp xếp / nối liền", (s, e) => GeoSortLayers()),
            DialogUi.Btn("Chọn lớp đất theo bảng tra TCVN…", (s, e) => GeoPickPreset(), true),
            DialogUi.Btn("Tra lại bảng cho lớp chọn", (s, e) => GeoRetable())));
        layers.Children.Add(DialogUi.Hint("Mặc định mỗi lớp lấy chỉ tiêu theo lớp cấu tạo tiêu chuẩn và bảng tra TCVN 9362:2012 (c, φ, E theo e và IL; R0; γ từ e, Sr) – "
            + "đổi e, IL, Sr hoặc loại đất thì chỉ tiêu tự tra lại. Có hố khoan khảo sát và thí nghiệm: nhập trực tiếp γ, φ, c, E → cột \"Chỉ tiêu theo\" tự chuyển sang \"Thí nghiệm\". "
            + "Thiết kế móng nông: ktc = 1,1 khi chỉ tiêu tra bảng, 1,0 khi theo thí nghiệm."));
        layers.Children.Add(_geoGrid);
        layers.Children.Add(_geoHint);
        root.Children.Add(new GroupBox { Header = "Các lớp đất (độ sâu tính từ miệng hố khoan)", Content = layers });

        // ---- hình trụ ----
        _geoCanvas = new Canvas { Background = Brushes.White, ClipToBounds = true };
        var host = new Border { Height = 430, BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), Child = _geoCanvas };
        _geoCanvas.SizeChanged += (s, e) => RedrawGeoLog();
        root.Children.Add(new GroupBox { Header = "Hình trụ hố khoan", Content = host });

        // ---- áp dụng ----
        _geoUse = new CheckBox { Content = "Dùng hố khoan cho thiết kế móng (móng đơn, móng băng: φ, c, γ lớp đặt móng + kiểm tra lún)", Margin = new Thickness(0, 2, 0, 2) };
        _geoUse.Click += (s, e) =>
        {
            if (_p == null) return;
            _p.Soil.UseBorehole = _geoUse.IsChecked == true;
            if (_p.Soil.UseBorehole && _p.Boreholes.All(b => b.Layers.Count == 0))
                MessageBox.Show(this, "Chưa có hố khoan nào có lớp đất – thiết kế móng vẫn dùng chỉ tiêu nhập tay ở tab 'Công trình'.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            MarkDirty(false);
            SetStatus(_p.Soil.UseBorehole ? "Thiết kế móng sẽ lấy chỉ tiêu nền theo hố khoan – chạy lại 'Thiết kế móng'." : "Thiết kế móng dùng chỉ tiêu nền nhập tay (tab Công trình).");
        };
        _geoApplyHole = new ComboBox { Width = 260 };
        _geoApplyHole.SelectionChanged += (s, e) =>
        {
            if (_geoLoading || _p == null) return;
            string v = _geoApplyHole.SelectedItem as string;
            _p.Soil.Borehole = v == null || v == GeoNearest ? "" : v;
            MarkDirty(false);
        };
        _geoGood = new CheckBox { Content = "Mặc định nền đất tốt khi chưa có khảo sát", Margin = new Thickness(0, 6, 0, 2) };
        _geoGood.Click += (s, e) => { if (_p != null) { _p.Soil.AssumeGoodSoil = _geoGood.IsChecked == true; MarkDirty(false); } };
        var apply = new StackPanel();
        apply.Children.Add(_geoUse);
        apply.Children.Add(DialogUi.Row(DialogUi.Label("Hố khoan áp dụng"), _geoApplyHole));
        apply.Children.Add(_geoGood);
        apply.Children.Add(DialogUi.Hint(
            "• Dùng hố khoan: tại mỗi chân cột chương trình lấy hố khoan gần nhất (hoặc hố khoan chọn), lớp đất ở độ sâu đáy móng Df cho φ, c, γ " +
            "(dưới mực nước ngầm dùng γsat − 10), γ' trên đáy móng = σ'bt(Df)/Df; R theo TCVN 9362:2012 và thêm kiểm tra lún S ≤ 8 cm. Các ô φ, c, γ ở tab 'Công trình' khi đó không dùng.\n" +
            $"• Nền đất tốt giả định (khi chưa có khảo sát): sét pha dẻo cứng γ = {GF(SoilPresets.GoodGamma)} kN/m³, φ = {GF(SoilPresets.GoodPhi)}°, c = {GF(SoilPresets.GoodC)} kPa, E = {GF(SoilPresets.GoodE)} MPa – " +
            "dùng các giá trị ở tab 'Công trình'. Bỏ chọn nếu chưa đủ cơ sở: gợi ý móng sẽ chỉ mang tính tham khảo. Phải kiểm chứng bằng khảo sát thực tế."));
        apply.Children.Add(DialogUi.Row(
            DialogUi.Btn("Gán chỉ tiêu lớp đặt móng → tab Công trình", (s, e) => GeoApplyToSoil()),
            DialogUi.Btn("Đặt lại nền đất tốt giả định", (s, e) => GeoResetGoodSoil())));
        var adv = DialogUi.Btn("Gợi ý phương án móng…", (s, e) => GeoAdvise(), true);
        adv.Margin = new Thickness(0, 6, 0, 2);
        apply.Children.Add(DialogUi.Row(adv));
        root.Children.Add(new GroupBox { Header = "Áp dụng cho thiết kế móng", Content = apply });

        tabGeo.Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = root };
    }

    /// <summary>Nạp dữ liệu địa chất của dự án lên tab (gọi từ LoadUiFromProject).</summary>
    private void LoadGeoUi()
    {
        if (tabGeo == null || _p == null) return;
        _p.Boreholes ??= new List<Borehole>();
        if (!_geoBuilt) BuildGeoTab();
        _geoLoading = true;
        try
        {
            RefreshGeoList(_geoList.SelectedIndex);
            _geoUse.IsChecked = _p.Soil.UseBorehole;
            _geoGood.IsChecked = _p.Soil.AssumeGoodSoil;
            var names = new List<string> { GeoNearest };
            names.AddRange(_p.Boreholes.Select(b => b.Name).Distinct());
            _geoApplyHole.ItemsSource = names;
            _geoApplyHole.SelectedItem = !string.IsNullOrWhiteSpace(_p.Soil.Borehole) && names.Contains(_p.Soil.Borehole) ? _p.Soil.Borehole : GeoNearest;
        }
        finally { _geoLoading = false; }
        LoadGeoHole();
    }

    private void RefreshGeoList(int select)
    {
        bool was = _geoLoading;
        _geoLoading = true;
        try
        {
            _geoList.ItemsSource = _p.Boreholes.Select(HoleLabel).ToList();
            _geoList.SelectedIndex = _p.Boreholes.Count == 0 ? -1 : Math.Clamp(select, 0, _p.Boreholes.Count - 1);
        }
        finally { _geoLoading = was; }
    }

    private void RefreshGeoApplyCombo()
    {
        bool was = _geoLoading;
        _geoLoading = true;
        try
        {
            var names = new List<string> { GeoNearest };
            names.AddRange(_p.Boreholes.Select(b => b.Name).Distinct());
            _geoApplyHole.ItemsSource = names;
            _geoApplyHole.SelectedItem = !string.IsNullOrWhiteSpace(_p.Soil.Borehole) && names.Contains(_p.Soil.Borehole) ? _p.Soil.Borehole : GeoNearest;
        }
        finally { _geoLoading = was; }
    }

    private void LoadGeoHole()
    {
        var bh = GeoSel;
        _geoLoading = true;
        try
        {
            _geoName.Text = bh?.Name ?? ""; _geoX.Text = bh != null ? GF(bh.X, "0.###") : "";
            _geoY.Text = bh != null ? GF(bh.Y, "0.###") : ""; _geoElev.Text = bh != null ? GF(bh.GroundElevation, "0.###") : "";
            _geoWater.Text = bh != null ? GF(bh.WaterDepth, "0.##") : "";
            foreach (var t in new[] { _geoName, _geoX, _geoY, _geoElev, _geoWater }) t.IsEnabled = bh != null;
            _geoRows.Clear();
            if (bh != null) foreach (var l in bh.Layers) _geoRows.Add(new GeoLayerRow(l, GeoLayerChanged));
        }
        finally { _geoLoading = false; }
        UpdateGeoHint();
        RedrawGeoLog();
    }

    private void GeoLayerChanged()
    {
        if (_geoLoading) return;
        MarkDirty(false);
        UpdateGeoHint();
        RedrawGeoLog();
        RefreshGeoList(_geoList.SelectedIndex);
    }

    private void GeoFieldsToHole()
    {
        var bh = GeoSel;
        if (bh == null || _geoLoading) return;
        string name = (_geoName.Text ?? "").Trim();
        if (name.Length == 0) name = bh.Name;
        if (name != bh.Name && _p.Boreholes.Any(b => !ReferenceEquals(b, bh) && b.Name == name))
        {
            MessageBox.Show(this, "Đã có hố khoan tên '" + name + "'.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            name = bh.Name;
        }
        if (_p.Soil.Borehole == bh.Name && name != bh.Name) _p.Soil.Borehole = name;
        double x = DialogUi.ParseNum(_geoX.Text, bh.X), y = DialogUi.ParseNum(_geoY.Text, bh.Y);
        double z = DialogUi.ParseNum(_geoElev.Text, bh.GroundElevation), w = DialogUi.ParseNum(_geoWater.Text, bh.WaterDepth);
        if (w < 0) w = -1;
        bool changed = name != bh.Name || x != bh.X || y != bh.Y || z != bh.GroundElevation || w != bh.WaterDepth;
        if (!changed) return;
        bh.Name = name; bh.X = x; bh.Y = y; bh.GroundElevation = z; bh.WaterDepth = w;
        MarkDirty(false);
        RefreshGeoList(_geoList.SelectedIndex);
        RefreshGeoApplyCombo();
        LoadGeoHole();
        RedrawFoundation();
    }

    // ------------------------------------------------------------------
    // Thao tác hố khoan / lớp
    // ------------------------------------------------------------------
    private void GeoAddHole(bool duplicate)
    {
        if (_p == null || _busy) return;
        var src = GeoSel;
        Borehole bh;
        if (duplicate && src != null)
        {
            bh = src.Copy();
            bh.X += 3;
        }
        else
        {
            double cx = _p.GridsX.Any() ? _p.GridsX.Average(g => g.Coord) : 0, cy = _p.GridsY.Any() ? _p.GridsY.Average(g => g.Coord) : 0;
            bh = new Borehole { X = Math.Round(cx, 2), Y = Math.Round(cy, 2), WaterDepth = -1 };
            bh.Layers.Add(SoilPresets.All[0].ToLayer(0, 1.0));
            bh.Layers.Add(SoilPresets.All[3].ToLayer(1.0, 8.0));
        }
        int k = _p.Boreholes.Count + 1;
        while (_p.Boreholes.Any(b => b.Name == "HK" + k)) k++;
        bh.Name = "HK" + k;
        _p.Boreholes.Add(bh);
        MarkDirty(false);
        RefreshGeoList(_p.Boreholes.Count - 1);
        RefreshGeoApplyCombo();
        LoadGeoHole();
        RedrawFoundation();
        SetStatus($"Đã thêm hố khoan {bh.Name} – nhập toạ độ, mực nước ngầm và các lớp đất theo kết quả khảo sát.");
    }

    private void GeoDeleteHole()
    {
        var bh = GeoSel;
        if (bh == null || _busy) return;
        if (MessageBox.Show(this, $"Xoá hố khoan {bh.Name}?", AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        int i = _geoList.SelectedIndex;
        _p.Boreholes.Remove(bh);
        if (_p.Soil.Borehole == bh.Name) _p.Soil.Borehole = "";
        MarkDirty(false);
        RefreshGeoList(i - 1);
        RefreshGeoApplyCombo();
        LoadGeoHole();
        RedrawFoundation();
    }

    private void GeoAddLayer()
    {
        var bh = GeoSel;
        if (bh == null) { GeoAddHole(false); return; }
        DialogUi.Commit(_geoGrid);
        double top = bh.Layers.Count == 0 ? 0 : bh.Layers.Max(l => l.Bottom);
        var last = bh.Sorted().LastOrDefault();
        // [Bảng tra] lớp mới mặc định theo lớp cấu tạo tiêu chuẩn (tra bảng TCVN 9362:2012)
        var std = last != null ? SoilStandard.Nearest(last) : SoilStandard.Find("Sét pha – dẻo cứng");
        var l = SoilStandard.NewLayer(std, top, top + 3);
        l.Name = "Lớp " + (bh.Layers.Count + 1) + " – " + std.Name;
        bh.Layers.Add(l);
        MarkDirty(false);
        LoadGeoHole();
        RefreshGeoList(_geoList.SelectedIndex);
        _geoGrid.SelectedIndex = _geoRows.Count - 1;
    }

    private void GeoDeleteLayer()
    {
        var bh = GeoSel;
        if (bh == null) return;
        DialogUi.Commit(_geoGrid);
        var sel = _geoGrid.SelectedItems.OfType<GeoLayerRow>().ToList();
        if (sel.Count == 0) return;
        foreach (var r in sel) bh.Layers.Remove(r.L);
        MarkDirty(false);
        LoadGeoHole();
        RefreshGeoList(_geoList.SelectedIndex);
    }

    /// <summary>Sắp lớp theo độ sâu và nối liền: mặt lớp sau = đáy lớp trước (lớp đầu bắt đầu từ 0).</summary>
    private void GeoSortLayers()
    {
        var bh = GeoSel;
        if (bh == null) return;
        DialogUi.Commit(_geoGrid);
        var ls = bh.Sorted();
        double prev = 0;
        foreach (var l in ls)
        {
            double th = Math.Max(l.Bottom - l.Top, 0.1);
            l.Top = prev; l.Bottom = Math.Round(prev + th, 3); prev = l.Bottom;
        }
        bh.Layers = ls;
        MarkDirty(false);
        LoadGeoHole();
        RefreshGeoList(_geoList.SelectedIndex);
    }

    private void GeoPickPreset()
    {
        var bh = GeoSel;
        DialogUi.Commit(_geoGrid);
        var row = _geoGrid.SelectedItem as GeoLayerRow;
        var dlg = new SoilStandardWindow(row?.L) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Selected == null) return;
        if (bh == null) { GeoAddHole(false); bh = GeoSel; if (bh == null) return; bh.Layers.Clear(); row = null; }
        if (row != null && !dlg.AddNew) SoilStandard.ApplyStd(row.L, dlg.Selected);
        else
        {
            double top = bh.Layers.Count == 0 ? 0 : bh.Layers.Max(l => l.Bottom);
            var l = SoilStandard.NewLayer(dlg.Selected, top, top + 3);
            l.Name = "Lớp " + (bh.Layers.Count + 1) + " – " + dlg.Selected.Name;
            bh.Layers.Add(l);
        }
        MarkDirty(false);
        LoadGeoHole();
        RefreshGeoList(_geoList.SelectedIndex);
        SetStatus($"Đã gán lớp tiêu chuẩn \"{dlg.Selected.Name}\" – chỉ tiêu theo bảng tra {SoilStandard.Std}; có thí nghiệm thì nhập trực tiếp để thay.");
    }

    /// <summary>Tra lại bảng TCVN cho các lớp đang chọn (bỏ chỉ tiêu nhập tay, theo e, IL, Sr hiện có).</summary>
    private void GeoRetable()
    {
        DialogUi.Commit(_geoGrid);
        var rows = _geoGrid.SelectedItems.OfType<GeoLayerRow>().ToList();
        if (rows.Count == 0) { SetStatus("Chọn lớp đất cần tra lại bảng."); return; }
        foreach (var r in rows) { SoilStandard.ApplyTo(r.L); r.RefreshAll(); }
        MarkDirty(false);
        RedrawGeoLog();
        UpdateGeoHint();
        SetStatus($"Đã tra lại bảng {SoilStandard.Std} cho {rows.Count} lớp.");
    }

    private void UpdateGeoHint()
    {
        if (_geoHint == null) return;
        var row = _geoGrid?.SelectedItem as GeoLayerRow;
        if (row == null) { _geoHint.Text = "Chọn một lớp để xem gợi ý tương quan SPT và khoảng giá trị tham khảo. " + SoilPresets.Disclaimer + "."; return; }
        var l = row.L;
        var hints = SoilPresets.CorrelationHints(l.N, l.IsCohesive);
        var tb = SoilStandard.Lookup(l);
        string tab = tb.InTable
            ? $"Bảng tra {SoilStandard.Std} ({SoilStandard.ClassName(tb.Class)} {tb.State}, e = {GF(l.VoidRatio)}{(l.IsCohesive ? ", IL = " + GF(l.IL) : "")}): "
              + $"γ = {GF(tb.Gamma)}, φ = {GF(tb.Phi, "0.#")}°, c = {GF(tb.C, "0.#")} kPa, E = {GF(tb.E, "0.#")} MPa{(double.IsFinite(tb.R0) ? ", R0 = " + GF(tb.R0, "0") + " kPa" : "")}"
            : "Không có trong bảng tra TCVN – phải khảo sát";
        _geoHint.Text = $"Lớp \"{l.Name}\" – chỉ tiêu theo {(l.Source == SoilPropSource.Table ? "bảng tra" : "thí nghiệm")}. {tab}."
                        + (tb.Warnings.Count > 0 ? "\n⚠ " + string.Join("; ", tb.Warnings) : "")
                        + $"\nTương quan SPT (N = {GF(l.N, "0.#")}): " + string.Join("; ", hints) + ".";
    }

    private void GeoApplyToSoil()
    {
        if (_p == null || _busy) return;
        var bh = GeoSel;
        if (bh == null || bh.Layers.Count == 0) { MessageBox.Show(this, "Chọn hố khoan có lớp đất.", AppTitle, MessageBoxButton.OK, MessageBoxImage.Information); return; }
        ApplyUiToProject(false);
        var notes = GeotechCalc.ApplyToSoil(_p.Soil, bh, _p.Soil.Depth);
        _p.Soil.StdName = "";   // chỉ tiêu nay lấy từ hố khoan (ktc theo nguồn chỉ tiêu của lớp đặt móng)
        LoadUiFromProject();
        MarkDirty(false);
        MessageBox.Show(this, "Đã gán chỉ tiêu nền vào tab 'Công trình':\n\n" + string.Join("\n", notes.Select(n => "• " + n)), AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void GeoResetGoodSoil()
    {
        if (_p == null || _busy) return;
        ApplyUiToProject(false);
        SoilPresets.ApplyGoodSoil(_p.Soil);
        _p.Soil.AssumeGoodSoil = true;
        LoadUiFromProject();
        MarkDirty(false);
        SetStatus($"Nền đất tốt giả định: γ = {GF(SoilPresets.GoodGamma)} kN/m³, φ = {GF(SoilPresets.GoodPhi)}°, c = {GF(SoilPresets.GoodC)} kPa, E = {GF(SoilPresets.GoodE)} MPa.");
    }

    private void GeoAdvise()
    {
        if (_p == null || _busy) return;
        ApplyUiToProject(false);
        var bh = GeoSel;
        if (bh != null && bh.Layers.Count == 0) bh = null;
        var w = new GeoAdvisorWindow(_p, _res, bh) { Owner = this };
        string snap = Snapshot();
        w.ShowDialog();
        if (!w.Applied) return;
        PushUndo(snap);
        MarkDirty(false);
        SyncFoundationUi();
        LoadGeoUi();
        SetStatus($"Đã chọn phương án: {w.AppliedName} – vào 'Thiết kế → Thiết kế móng…' để tính chi tiết.");
    }

    // ------------------------------------------------------------------
    // Hình trụ hố khoan
    // ------------------------------------------------------------------
    private void RedrawGeoLog()
    {
        var cv = _geoCanvas;
        if (cv == null) return;
        cv.Children.Clear();
        double w = cv.ActualWidth, h = cv.ActualHeight;
        if (w < 80 || h < 80) return;
        var bh = GeoSel;
        if (bh == null) { CenterHint(cv, w, h, "Chưa có hố khoan – bấm '+ Hố khoan'"); return; }
        var ls = bh.Sorted();
        if (ls.Count == 0) { CenterHint(cv, w, h, "Hố khoan chưa có lớp đất"); return; }
        double depth = Math.Max(Math.Max(bh.Depth, 3), bh.WaterDepth >= 0 ? bh.WaterDepth + 0.5 : 0);
        double top = 40, bot = h - 16, sc = (bot - top) / depth;
        double Y(double z) => top + z * sc;
        double x0 = 56, x1 = 116;
        double xn0 = Math.Max(w * 0.64, x1 + 200), xn1 = w - 16;
        if (xn1 - xn0 < 60) xn0 = xn1 - 60;
        var title = CText(cv, $"HÌNH TRỤ HỐ KHOAN {bh.Name}   (cao độ miệng {GF(bh.GroundElevation, "0.00")} m)", new Point(w / 2, 12), Brushes.Black, 12.5, true);
        title.FontWeight = FontWeights.Bold;
        CText(cv, "Độ sâu (m)", new Point(4, top - 18), Brushes.DimGray, 9.5, false);

        // trục độ sâu
        CLine(cv, new Point(x0 - 8, top), new Point(x0 - 8, bot), Brushes.Gray, 0.8, false);
        double step = depth > 30 ? 5 : depth > 12 ? 2 : 1;
        for (double z = 0; z <= depth + 1e-9; z += step)
        {
            CLine(cv, new Point(x0 - 12, Y(z)), new Point(x0 - 8, Y(z)), Brushes.Gray, 0.8, false);
            CText(cv, GF(z, "0"), new Point(x0 - 30, Y(z)), Brushes.DimGray, 9, false, true);
        }

        // N chart khung
        double nw = xn1 - xn0;
        CText(cv, "N (SPT)", new Point((xn0 + xn1) / 2, top - 18), Brushes.DimGray, 9.5, true);
        for (int nv = 0; nv <= 50; nv += 10)
        {
            double x = xn0 + nw * nv / 50.0;
            CLine(cv, new Point(x, top), new Point(x, bot), nv == 0 ? Brushes.Gray : Brushes.Gainsboro, nv == 0 ? 0.8 : 0.6, false);
            CText(cv, nv.ToString(GeoInv), new Point(x, top - 6), Brushes.DimGray, 8.5, true);
        }

        foreach (var l in ls)
        {
            double y0 = Y(l.Top), y1 = Y(Math.Max(l.Bottom, l.Top));
            var r = new Shapes.Rectangle
            {
                Width = x1 - x0, Height = Math.Max(1, y1 - y0), Fill = GeoUi.KindBrush(l.Kind, l.Grain), Stroke = Brushes.Black, StrokeThickness = 0.8, IsHitTestVisible = false
            };
            Canvas.SetLeft(r, x0); Canvas.SetTop(r, y0);
            cv.Children.Add(r);
            CText(cv, GF(l.Bottom, "0.0#"), new Point(x1 + 3, y1 - 6), Brushes.Black, 8.5, false, true);
            double mid = (y0 + y1) / 2;
            if (y1 - y0 >= 12)
            {
                CText(cv, $"{l.Name} – {l.KindText}", new Point(x1 + 36, mid - (y1 - y0 >= 26 ? 7 : 0)), Brushes.Black, 10, false, true);
                if (y1 - y0 >= 26)
                    CText(cv, $"γ = {GF(l.Gamma, "0.0")}, φ = {GF(l.Phi, "0.#")}°, c = {GF(l.C, "0.#")} kPa, E = {GF(l.E, "0.#")} MPa", new Point(x1 + 36, mid + 7), Brushes.DimGray, 8.5, false, true);
            }
            // cột N
            double nn = Math.Clamp(l.N, 0, 50);
            var bar = new Shapes.Rectangle
            {
                Width = Math.Max(1, nw * nn / 50.0), Height = Math.Max(1, y1 - y0 - 2),
                Fill = new SolidColorBrush(l.N < 4 ? Color.FromRgb(220, 90, 70) : l.N >= 30 ? Color.FromRgb(40, 140, 70) : Color.FromRgb(70, 120, 200)),
                Opacity = 0.75, IsHitTestVisible = false
            };
            Canvas.SetLeft(bar, xn0); Canvas.SetTop(bar, y0 + 1);
            cv.Children.Add(bar);
            if (y1 - y0 >= 10) CText(cv, GF(l.N, "0.#") + (l.N > 50 ? " (>50)" : ""), new Point(xn0 + nw * nn / 50.0 + 3, mid), Brushes.Black, 9, false, true);
        }

        // đáy móng Df
        double df = _p.Soil.Depth;
        if (df > 0 && df < depth)
        {
            CLine(cv, new Point(x0 - 20, Y(df)), new Point(xn0 - 6, Y(df)), Brushes.SaddleBrown, 1.2, true);
            CText(cv, $"Đáy móng Df = {GF(df)} m", new Point(xn0 - 8 - MeasureText($"Đáy móng Df = {GF(df)} m", 9), Y(df) - 8), Brushes.SaddleBrown, 9, false, true, true);
        }
        // mũi cọc tự chọn (móng cọc)
        if (_p.Foundation?.Type == FoundationType.Pile)
        {
            double tip = _p.Foundation.PileTipDepth > 0 ? _p.Foundation.PileTipDepth : GeotechCalc.AutoTipDepth(bh, _p.Foundation.PileSize, df);
            if (tip < depth + 1e-9)
            {
                double px = x0 + (x1 - x0) * 0.5, pw = 8;
                var pile = new Shapes.Rectangle { Width = pw, Height = Math.Max(1, Y(tip) - Y(df)), Fill = Brushes.DimGray, Opacity = 0.8, IsHitTestVisible = false };
                Canvas.SetLeft(pile, px - pw / 2); Canvas.SetTop(pile, Y(df));
                cv.Children.Add(pile);
                CText(cv, $"Mũi cọc {GF(tip)} m", new Point(x0 - 54, Y(tip)), Brushes.Black, 8.5, false, true, true);
            }
        }
        // mực nước ngầm
        if (bh.WaterDepth >= 0 && bh.WaterDepth <= depth)
        {
            double yw = Y(bh.WaterDepth), xt = x1 + 18;
            CLine(cv, new Point(x0 - 4, yw), new Point(x1 + 30, yw), Brushes.RoyalBlue, 1.2, false);
            var tri = new Shapes.Polygon
            {
                Points = new PointCollection { new Point(xt - 6, yw - 10), new Point(xt + 6, yw - 10), new Point(xt, yw) },
                Fill = Brushes.RoyalBlue, IsHitTestVisible = false
            };
            cv.Children.Add(tri);
            CText(cv, $"MNN −{GF(bh.WaterDepth)}", new Point(xt + 8, yw - 9), Brushes.RoyalBlue, 9, false, true, true);
        }
    }

    // ------------------------------------------------------------------
    /// <summary>Ký hiệu hố khoan trên mặt bằng móng (gọi từ RedrawFoundationPlan).</summary>
    private void DrawBoreholeMarkers()
    {
        if (_p?.Boreholes == null) return;
        foreach (var bh in _p.Boreholes)
        {
            var c = FW2S(bh.X, bh.Y);
            const double r = 7;
            var e = new Shapes.Ellipse { Width = 2 * r, Height = 2 * r, Stroke = Brushes.DarkOrange, StrokeThickness = 1.6, Fill = Brushes.White, IsHitTestVisible = false };
            Canvas.SetLeft(e, c.X - r); Canvas.SetTop(e, c.Y - r);
            fndCanvas.Children.Add(e);
            var half = new Shapes.Path
            {
                Data = new PathGeometry(new[]
                {
                    new PathFigure(c, new PathSegment[] { new LineSegment(new Point(c.X, c.Y - r), true), new ArcSegment(new Point(c.X, c.Y + r), new Size(r, r), 0, false, SweepDirection.Clockwise, true) }, true)
                }),
                Fill = Brushes.DarkOrange, IsHitTestVisible = false
            };
            fndCanvas.Children.Add(half);
            CText(fndCanvas, bh.Name, new Point(c.X + r + 3, c.Y - r - 4), Brushes.DarkOrange, 10, false, false, true).FontWeight = FontWeights.Bold;
        }
    }
}
