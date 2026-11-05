using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Dòng bảng tỷ lệ theo loại bản vẽ.</summary>
internal sealed class StyleScaleRow : RowBase
{
    private readonly Func<DrawingStyle> _style;
    private readonly Action _changed;
    public DrawingKind Kind { get; }

    public StyleScaleRow(DrawingKind k, Func<DrawingStyle> style, Action changed) { Kind = k; _style = style; _changed = changed; }

    public string Name => DrawingStyle.KindLabel(Kind);
    public bool Fixed => DrawingStyle.FixedScale(Kind);

    public string Scale
    {
        get => DrawingStyle.ScaleLabel(Kind, _style().ScaleOf(Kind));
        set
        {
            if (!Fixed && DrawingStyle.TryParseScale(value, out double v) && (v > 0 || Kind == DrawingKind.SlabPlan))
            {
                _style().SetScale(Kind, v);
                _changed?.Invoke();
            }
            RaiseAll();
        }
    }

    public string Note => Kind switch
    {
        DrawingKind.SlabPlan => "Tự chọn: 1/50 nếu mặt bằng ≤ 25 m, ngược lại 1/100",
        DrawingKind.MemberSection => "Mặt cắt dầm và cột (vẽ phóng trên tờ mặt đứng)",
        DrawingKind.FootingDetail => "Móng đơn, đài cọc, mặt cắt ngang móng băng",
        DrawingKind.BeamElevation => "Kể cả mặt cắt dọc móng băng",
        DrawingKind.Shop => "Bảng và sơ đồ gia công vẽ theo mm giấy",
        DrawingKind.StairPlan or DrawingKind.StairSection => "Bản vẽ cầu thang",
        _ => ""
    };

    public void Refresh() => RaiseAll();
}

/// <summary>Dòng bảng cỡ chữ / kích thước ký hiệu (mm trên giấy).</summary>
internal sealed class StyleSizeRow : RowBase
{
    private readonly Func<DrawingStyle, double> _get;
    private readonly Action<DrawingStyle, double> _set;
    private readonly Func<DrawingStyle> _style;
    private readonly Action _changed;
    private readonly double _min, _max;

    public StyleSizeRow(string name, string unit, Func<DrawingStyle, double> get, Action<DrawingStyle, double> set,
        Func<DrawingStyle> style, Action changed, double min = 0.5, double max = 20)
    {
        Name = name; Unit = unit; _get = get; _set = set; _style = style; _changed = changed; _min = min; _max = max;
    }

    public string Name { get; }
    public string Unit { get; }

    public double Value
    {
        get => _get(_style());
        set
        {
            if (value >= _min && value <= _max) { _set(_style(), value); _changed?.Invoke(); }
            RaiseAll();
        }
    }

    public string Range => $"{DialogUi.F(_min)} … {DialogUi.F(_max)}";
    public void Refresh() => RaiseAll();
}

/// <summary>Dòng bảng kiểu kích thước theo tỷ lệ (VXT_1-25…). Ô trống = theo thông số chung.</summary>
internal sealed class DimStyleRow : RowBase
{
    private readonly Func<DrawingStyle> _style;
    private readonly Action _changed;
    public DimStyleDef D { get; }

    public DimStyleRow(DimStyleDef d, Func<DrawingStyle> style, Action changed) { D = d; _style = style; _changed = changed; }

    public static readonly string[] ArrowLabels = { "(theo chung)", "Gạch xiên", "Mũi tên đặc" };

    private static string S(double? v) => v.HasValue ? DialogUi.F(v.Value) : "";
    private void Set(string text, Action<double?> set, double min, double max)
    {
        string t = (text ?? "").Trim();
        if (t.Length == 0) set(null);
        else
        {
            double v = DialogUi.ParseNum(t, double.NaN);
            if (!double.IsNaN(v) && v >= min && v <= max) set(v);
        }
        _changed?.Invoke();
        RaiseAll();
    }

    public string Name => _style().DimStyleName(D.Scale);
    public string ScaleText => "1/" + DialogUi.F(D.Scale);
    public string TextHeight { get => S(D.TextHeight); set => Set(value, v => D.TextHeight = v, 0.5, 20); }
    public string TickSize { get => S(D.TickSize); set => Set(value, v => D.TickSize = v, 0.2, 20); }
    public string Arrow
    {
        get => D.Arrow == null ? ArrowLabels[0] : D.Arrow == DimArrow.ClosedArrow ? ArrowLabels[2] : ArrowLabels[1];
        set
        {
            D.Arrow = value == ArrowLabels[1] ? DimArrow.Tick : value == ArrowLabels[2] ? DimArrow.ClosedArrow : null;
            _changed?.Invoke(); RaiseAll();
        }
    }
    public string ExtOffset { get => S(D.ExtOffset); set => Set(value, v => D.ExtOffset = v, 0, 20); }
    public string ExtBeyond { get => S(D.ExtBeyond); set => Set(value, v => D.ExtBeyond = v, 0, 20); }
    public string LineExtend { get => S(D.LineExtend); set => Set(value, v => D.LineExtend = v, 0, 20); }
    public string TextGap { get => S(D.TextGap); set => Set(value, v => D.TextGap = v, 0, 20); }
    public string Decimals { get => D.Decimals.HasValue ? D.Decimals.Value.ToString() : ""; set => Set(value, v => D.Decimals = v.HasValue ? (int)Math.Round(v.Value) : null, 0, 4); }
    public string LinearFactor { get => S(D.LinearFactor); set => Set(value, v => D.LinearFactor = v, 1e-4, 1e4); }

    /// <summary>Thông số hiệu lực (sau khi lấy theo chung).</summary>
    public string Effective
    {
        get
        {
            var p = _style().DimFor(D.Scale);
            return $"chữ {DialogUi.F(p.TextHeight)} mm (mô hình {DialogUi.F(p.TextHeight * p.Scale)}), " +
                   (p.Arrow == DimArrow.Tick ? $"gạch {DialogUi.F(p.TickSize)}" : $"mũi tên {DialogUi.F(p.TickSize)}") + $", DIMSCALE {DialogUi.F(p.Scale)}";
        }
    }

    public void Refresh() => RaiseAll();
}

/// <summary>Cửa sổ "Thiết lập bản vẽ (tỷ lệ, cỡ chữ)": tỷ lệ theo loại bản vẽ, cỡ chữ kích thước / ghi chú / nhãn thép / tên hình /
/// đầu trục / bảng thống kê, hệ số rộng chữ, bật/tắt chú thích tuỳ chọn; xem trước mẫu (kích thước + nhãn + ghi chú) ở đúng cỡ in.
/// Lưu trong chuẩn CAD (CadStandard.Drawing): mặc định người dùng hoặc riêng dự án.</summary>
internal sealed class DrawingStyleWindow : Window
{
    private readonly Project _p;
    private DrawingStyle _st;
    private readonly ObservableCollection<StyleScaleRow> _scales = new();
    private readonly ObservableCollection<StyleSizeRow> _sizes = new();
    private readonly DataGrid _gScale, _gSize, _gDim;
    private readonly ObservableCollection<DimStyleRow> _dims = new();
    private readonly StackPanel _dimPreviews = new() { Margin = new Thickness(4) };
    private readonly TextBox _txtPrefix = new() { Width = 90, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly List<(CheckBox Box, Func<DrawingStyle, bool> Get, Action<DrawingStyle, bool> Set)> _checks = new();
    private readonly CheckBox _chkProject;
    private readonly ComboBox _cmbBreak = new() { Width = 110, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ComboBox _cmbHatch = new() { Width = 250, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBlock _txtStatus;
    private readonly DrawingHost _preview = new() { Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private bool _loading;
    private readonly TabControl _tabs = new() { Margin = new Thickness(0, 0, 0, 4) };

    public void SelectTab(int i) { if (i >= 0 && i < _tabs.Items.Count) _tabs.SelectedIndex = i; }

    /// <summary>Đã áp dụng hoặc lưu ít nhất một lần.</summary>
    public bool Changed { get; private set; }

    public static readonly string[] ScaleChoices =
        new[] { "Tự chọn" }.Concat(DrawingStyle.StandardScales.Select(s => "1/" + DialogUi.F(s))).ToArray();

    public DrawingStyleWindow(Project p)
    {
        _p = p;
        DialogUi.Setup(this, "Thiết lập bản vẽ (tỷ lệ, cỡ chữ)", 1180, 720);
        Help.Attach(this, "ho-so.thiet-lap-ban-ve");   // nút "?" + F1 – Help.cs
        _st = (CadStandard.Active?.Drawing ?? CadStandard.EffectiveFor(p).Drawing ?? DrawingStyle.Default()).Clone();

        // ---------------- bảng tỷ lệ ----------------
        _gScale = DialogUi.NewGrid();
        _gScale.Columns.Add(DialogUi.Text("Loại bản vẽ", nameof(StyleScaleRow.Name), true, 220));
        _gScale.Columns.Add(ScaleColumn());
        _gScale.Columns.Add(DialogUi.Text("Ghi chú", nameof(StyleScaleRow.Note), true, 260));
        _gScale.ItemsSource = _scales;

        // ---------------- bảng cỡ chữ ----------------
        _gSize = DialogUi.NewGrid();
        _gSize.Columns.Add(DialogUi.Text("Thành phần", nameof(StyleSizeRow.Name), true, 300));
        _gSize.Columns.Add(DialogUi.Num("Giá trị", nameof(StyleSizeRow.Value), "0.###", false, 80));
        _gSize.Columns.Add(DialogUi.Text("Đơn vị", nameof(StyleSizeRow.Unit), true, 110));
        _gSize.Columns.Add(DialogUi.Text("Phạm vi", nameof(StyleSizeRow.Range), true, 90));
        _gSize.ItemsSource = _sizes;

        // ---------------- chú thích tuỳ chọn ----------------
        var opt = new WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
        void Chk(string text, string tip, Func<DrawingStyle, bool> get, Action<DrawingStyle, bool> set)
        {
            var cb = new CheckBox { Content = text, ToolTip = tip, Margin = new Thickness(0, 2, 16, 2) };
            cb.Checked += (s, e) => { if (!_loading) { set(_st, true); Edited(); } };
            cb.Unchecked += (s, e) => { if (!_loading) { set(_st, false); Edited(); } };
            _checks.Add((cb, get, set));
            opt.Children.Add(cb);
        }
        Chk("Số hiệu phần tử nội bộ", "Tên phần tử (B12, C5…) cho dầm/cột chưa có tên thiết kế trên mặt bằng kết cấu", s => s.ShowMemberIds, (s, v) => s.ShowMemberIds = v);
        Chk("Giá trị nội lực", "Số trên biểu đồ nội lực và dòng \"Giá trị lớn nhất…\"", s => s.ShowLoadValues, (s, v) => s.ShowLoadValues = v);
        Chk("Giá trị thiết kế", "Tmax dầm cong, sức chịu tải cọc [P]", s => s.ShowDesignValues, (s, v) => s.ShowDesignValues = v);
        Chk("Ký hiệu cao độ ▽", "Cao độ đỉnh dầm, chân/đỉnh cột, cao độ tầng, cao độ móng", s => s.ShowElevationMarks, (s, v) => s.ShowElevationMarks = v);
        Chk("Kích thước lưới trục", "Chuỗi kích thước trục trên mặt bằng kết cấu, mặt bằng thép sàn, mặt bằng móng, mặt đứng khung", s => s.ShowGridDims, (s, v) => s.ShowGridDims = v);
        Chk("Khối GHI CHÚ CHUNG", "Một khối ghi chú chung cho mỗi bản vẽ KC-01…KC-06 (đơn vị, bê tông, thép, lớp bảo vệ, neo nối)", s => s.ShowGeneralNotes, (s, v) => s.ShowGeneralNotes = v);
        Chk("Cánh sàn trên mặt cắt dầm", "Mặt cắt dầm thể hiện cánh sàn liền khối theo mô hình (kiểu Kết cấu Soft / XSCad): chữ T – sàn hai bên, chữ L – dầm biên, " +
            "chữ nhật – không có sàn (lỗ cầu thang, giếng trời); kích thước hs, đường cắt ở đầu cánh; mặt đứng dầm có nét mép dưới sàn", s => s.ShowSlabFlanges, (s, v) => s.ShowSlabFlanges = v);
        Chk("Thép sàn trong cánh", "Thép lớp dưới, thép mũ vắt qua dầm và thép phân bố trong cánh sàn, nhãn \"Ø8a200 (sàn)\" theo kết quả thiết kế sàn " +
            "(chưa thiết kế sàn: thể hiện quy ước nét đứt)", s => s.ShowSlabRebarInFlange, (s, v) => s.ShowSlabRebarInFlange = v);
        var brkPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 16, 2) };
        brkPanel.Children.Add(new TextBlock { Text = "Đường cắt đầu cánh sàn:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _cmbBreak.Items.Add("Dích dắc");
        _cmbBreak.Items.Add("Nét thẳng");
        _cmbBreak.SelectionChanged += (s, e) =>
        {
            if (_loading) return;
            _st.SlabBreak = _cmbBreak.SelectedIndex == 1 ? SlabBreakStyle.Straight : SlabBreakStyle.ZigZag;
            Edited();
        };
        brkPanel.Children.Add(_cmbBreak);
        opt.Children.Add(brkPanel);
        // tô vật liệu (hatch) mặt cắt – Drawing/DxfWriter.Hatch.cs
        var hatchPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 16, 2) };
        hatchPanel.Children.Add(new TextBlock
        {
            Text = "Tô vật liệu (hatch) mặt cắt:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0),
            ToolTip = "Bê tông AR-CONC (mặt cắt dầm, cột, móng, bản thang), bê tông lót ANSI31, đất EARTH dưới móng, bậc gạch ANSI31 – trên lớp HATCH (màu 8). " +
                      "Tự động: bật khi xuất DXF 2018 / DWG (HATCH thật), tắt khi xuất DXF R12 để file nhẹ và giống bản vẽ cũ; Bật: cả R12 (mô phỏng bằng nét)."
        });
        foreach (var hm in new[] { HatchMode.Auto, HatchMode.On, HatchMode.Off }) _cmbHatch.Items.Add(DrawingStyle.HatchModeLabel(hm));
        _cmbHatch.SelectionChanged += (s, e) =>
        {
            if (_loading || _cmbHatch.SelectedIndex < 0) return;
            _st.HatchSections = _cmbHatch.SelectedIndex == 1 ? HatchMode.On : _cmbHatch.SelectedIndex == 2 ? HatchMode.Off : HatchMode.Auto;
            Edited();
        };
        hatchPanel.Children.Add(_cmbHatch);
        opt.Children.Add(hatchPanel);

        // ---------------- bố cục ----------------
        var left = new DockPanel();
        var hint = DialogUi.Hint("Tỷ lệ chọn trong danh sách hoặc gõ tỷ lệ tuỳ ý (vd. 1/40). Cỡ chữ tính bằng mm trên giấy in – trong mô hình nhân với mẫu số tỷ lệ. " +
                                 "Mặc định: chữ 2,5 mm, tên hình 3,5 mm, mặt đứng/mặt bằng chi tiết 1/50, mặt cắt 1/25 (theo quy tắc Kết cấu Soft).");
        DockPanel.SetDock(hint, Dock.Top); left.Children.Add(hint);
        var lbScale = DialogUi.Label("Tỷ lệ theo loại bản vẽ", double.NaN, true);
        DockPanel.SetDock(lbScale, Dock.Top); left.Children.Add(lbScale);
        _gScale.Height = 290;
        DockPanel.SetDock(_gScale, Dock.Top); left.Children.Add(_gScale);
        var lbOpt = DialogUi.Label("Chú thích hiển thị", double.NaN, true);
        DockPanel.SetDock(lbOpt, Dock.Top); left.Children.Add(lbOpt);
        DockPanel.SetDock(opt, Dock.Top); left.Children.Add(opt);
        var lbSize = DialogUi.Label("Cỡ chữ và ký hiệu (mm trên giấy)", double.NaN, true);
        DockPanel.SetDock(lbSize, Dock.Top); left.Children.Add(lbSize);
        left.Children.Add(_gSize);

        var right = new DockPanel { Margin = new Thickness(10, 0, 0, 0) };
        var lbPrev = DialogUi.Label("Xem trước (đúng cỡ in, phóng ×2)", double.NaN, true);
        DockPanel.SetDock(lbPrev, Dock.Top); right.Children.Add(lbPrev);
        var pvHint = DialogUi.Hint("Mẫu: mặt cắt dầm 220×300 với kích thước, nhãn thép 3Ø22 ①, đai Ø6a100 ④, đầu trục, cao độ, ghi chú và tên mặt cắt.");
        DockPanel.SetDock(pvHint, Dock.Bottom); right.Children.Add(pvHint);
        right.Children.Add(new Border
        {
            BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), Background = Brushes.White,
            Child = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _preview }
        });

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(640) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(left, 0); Grid.SetColumn(right, 1);
        body.Children.Add(left); body.Children.Add(right);

        _chkProject = new CheckBox { Content = "Lưu riêng cho dự án này", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), IsChecked = p?.CadStandard != null };
        _txtStatus = new TextBlock { Foreground = Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        var bApply = DialogUi.Btn("Áp dụng", (s, e) => Apply(), true);
        var bSave = DialogUi.Btn("Lưu làm mặc định", (s, e) => SaveDefault());
        var bReset = DialogUi.Btn("Khôi phục mặc định", (s, e) => RestoreDefault());
        var bClose = DialogUi.Btn("Đóng", (s, e) => Close());
        var bottom = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        var bl = DialogUi.Row(bApply, bSave, bReset, _chkProject);
        var br = DialogUi.Row(bClose);
        DockPanel.SetDock(bl, Dock.Left); DockPanel.SetDock(br, Dock.Right);
        bottom.Children.Add(bl); bottom.Children.Add(br); bottom.Children.Add(_txtStatus);

        // ---------------- tab kiểu kích thước ----------------
        _gDim = DialogUi.NewGrid();
        _gDim.Columns.Add(DialogUi.Text("Tên kiểu", nameof(DimStyleRow.Name), true, 95));
        _gDim.Columns.Add(DialogUi.Text("Tỷ lệ", nameof(DimStyleRow.ScaleText), true, 55));
        _gDim.Columns.Add(DialogUi.Text("Chữ (mm)", nameof(DimStyleRow.TextHeight), false, 62));
        _gDim.Columns.Add(DialogUi.Text("Gạch/mũi tên", nameof(DimStyleRow.TickSize), false, 80));
        _gDim.Columns.Add(DialogUi.Combo("Đầu kích thước", nameof(DimStyleRow.Arrow), DimStyleRow.ArrowLabels, 105));
        _gDim.Columns.Add(DialogUi.Text("Hở gióng", nameof(DimStyleRow.ExtOffset), false, 60));
        _gDim.Columns.Add(DialogUi.Text("Gióng vượt", nameof(DimStyleRow.ExtBeyond), false, 68));
        _gDim.Columns.Add(DialogUi.Text("Kéo dài", nameof(DimStyleRow.LineExtend), false, 56));
        _gDim.Columns.Add(DialogUi.Text("Chữ cách", nameof(DimStyleRow.TextGap), false, 60));
        _gDim.Columns.Add(DialogUi.Text("Số lẻ", nameof(DimStyleRow.Decimals), false, 45));
        _gDim.Columns.Add(DialogUi.Text("Hệ số dài", nameof(DimStyleRow.LinearFactor), false, 62));
        _gDim.Columns.Add(DialogUi.Text("Hiệu lực", nameof(DimStyleRow.Effective), true, 330));
        _gDim.ItemsSource = _dims;
        _txtPrefix.LostFocus += (s, e) => { if (_loading) return; _st.DimStylePrefix = DrawingStyle.SafePrefix(_txtPrefix.Text); _txtPrefix.Text = _st.DimStylePrefix; Edited(); };
        var dimTop = new StackPanel();
        dimTop.Children.Add(DialogUi.Hint("Mỗi tỷ lệ một kiểu kích thước (DIMSTYLE) – ghi vào bảng DIMSTYLE của file DXF và tạo trong AutoCAD bằng lệnh VXT_LAYER của add-in. " +
                                          "Thông số tính bằng mm trên giấy, DIMSCALE = mẫu số tỷ lệ. Ô trống = theo thông số chung (bảng \"Cỡ chữ và ký hiệu\"). " +
                                          "Kích thước trên mỗi hình dùng kiểu đúng tỷ lệ thể hiện của hình (mặt cắt 1/25 → VXT_1-25); tỷ lệ chưa có trong bảng được tạo tự động."));
        dimTop.Children.Add(DialogUi.Row(DialogUi.Label("Tiền tố tên"), _txtPrefix,
            DialogUi.Btn("＋ Thêm tỷ lệ…", (s, e) => AddDimStyle()), DialogUi.Btn("Xoá", (s, e) => DeleteDimStyles()),
            DialogUi.Btn("Xoá ô sửa riêng (theo chung)", (s, e) => ClearDimOverrides()),
            DialogUi.Btn("7 tỷ lệ mặc định", (s, e) => { DialogUi.Commit(_gDim); _st.DimStyles = DrawingStyle.DefaultDimStyles(); LoadDims(); Edited(); })));
        var dimTab = new DockPanel { Margin = new Thickness(6) };
        DockPanel.SetDock(dimTop, Dock.Top); dimTab.Children.Add(dimTop);
        var pvBox = new DockPanel { Height = 250 };
        var pvLb = DialogUi.Label("Xem trước kích thước ở từng tỷ lệ (đúng cỡ in, phóng ×2): đoạn 60 mm giấy", double.NaN, true);
        DockPanel.SetDock(pvLb, Dock.Top); pvBox.Children.Add(pvLb);
        pvBox.Children.Add(new Border
        {
            BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), Background = Brushes.White,
            Child = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _dimPreviews }
        });
        DockPanel.SetDock(pvBox, Dock.Bottom); dimTab.Children.Add(pvBox);
        dimTab.Children.Add(_gDim);

        _tabs.Items.Add(new TabItem { Header = "Tỷ lệ, cỡ chữ, chú thích", Content = body });
        _tabs.Items.Add(new TabItem { Header = "Kiểu kích thước (Dimension)", Content = dimTab });

        var root = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom); root.Children.Add(_tabs);
        Content = root;

        LoadAll();
        SetStatus((p?.CadStandard != null ? "Đang dùng thiết lập riêng của dự án." : "Đang dùng thiết lập mặc định của người dùng.") + " File mặc định: " + CadStandard.UserFilePath);
    }

    private void SetStatus(string s) => _txtStatus.Text = s;

    private DataGridTemplateColumn ScaleColumn()
    {
        var col = new DataGridTemplateColumn { Header = "Tỷ lệ", Width = 150 };
        var tb = new FrameworkElementFactory(typeof(TextBlock));
        tb.SetBinding(TextBlock.TextProperty, new Binding(nameof(StyleScaleRow.Scale)));
        tb.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0));
        col.CellTemplate = new DataTemplate { VisualTree = tb };
        var cb = new FrameworkElementFactory(typeof(ComboBox));
        cb.SetValue(ComboBox.IsEditableProperty, true);
        cb.SetValue(ItemsControl.ItemsSourceProperty, ScaleChoices);
        cb.SetBinding(ComboBox.TextProperty, new Binding(nameof(StyleScaleRow.Scale)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus });
        col.CellEditingTemplate = new DataTemplate { VisualTree = cb };
        return col;
    }

    // ------------------------------------------------------------------ nạp
    private void LoadAll()
    {
        _loading = true;
        try
        {
            _st.Normalize();
            _scales.Clear();
            foreach (var k in DrawingStyle.Kinds) _scales.Add(new StyleScaleRow(k, () => _st, Edited));
            _sizes.Clear();
            void Size(string name, string unit, Func<DrawingStyle, double> g, Action<DrawingStyle, double> s, double min = 0.5, double max = 20) =>
                _sizes.Add(new StyleSizeRow(name, unit, g, s, () => _st, Edited, min, max));
            foreach (var r in DrawingStyle.Roles)
                Size(DrawingStyle.RoleLabel(r), "mm chữ hoa", s => s.Height(r), (s, v) => s.SetHeight(r, v));
            Size("Kích thước: gạch xiên đầu đường kích thước", "mm dài", s => s.DimTickSize, (s, v) => s.DimTickSize = v, 0.2);
            Size("Kích thước: hở từ điểm đo tới đường gióng", "mm", s => s.DimExtOffset, (s, v) => s.DimExtOffset = v, 0);
            Size("Kích thước: đường gióng vượt đường kích thước", "mm", s => s.DimExtBeyond, (s, v) => s.DimExtBeyond = v, 0);
            Size("Kích thước: chiều dài chân gióng cố định (0 = kéo tới điểm đo)", "mm", s => s.DimFixedExtLength, (s, v) => s.DimFixedExtLength = v, 0, 50);
            Size("Kích thước: đường kích thước kéo qua đường gióng", "mm", s => s.DimLineExtend, (s, v) => s.DimLineExtend = v, 0);
            Size("Kích thước: chữ số cách đường kích thước", "mm", s => s.DimTextGap, (s, v) => s.DimTextGap = v, 0);
            Size("Vòng tròn số hiệu thép", "mm đường kính", s => s.MarkDiameter, (s, v) => s.MarkDiameter = v, 1, 40);
            Size("Vòng tròn đầu trục", "mm đường kính", s => s.AxisBubbleDiameter, (s, v) => s.AxisBubbleDiameter = v, 2, 40);
            Size("Hệ số rộng chữ", "× (1 = chuẩn)", s => s.WidthFactor, (s, v) => s.WidthFactor = v, 0.3, 2);
            Size("Chiều dài cánh sàn thể hiện (mỗi bên dầm)", "mm thật", s => s.SlabFlangeLength, (s, v) => s.SlabFlangeLength = v, 100, 3000);
            Size("Tô vật liệu: hệ số tỷ lệ mẫu (1 = ANSI31 cách 3,175 mm)", "× (2 = thưa gấp đôi)", s => s.HatchScaleFactor, (s, v) => s.HatchScaleFactor = v, 0.1, 20);
            foreach (var (box, get, _) in _checks) box.IsChecked = get(_st);
            _cmbBreak.SelectedIndex = _st.SlabBreak == SlabBreakStyle.Straight ? 1 : 0;
            _cmbHatch.SelectedIndex = _st.HatchSections == HatchMode.On ? 1 : _st.HatchSections == HatchMode.Off ? 2 : 0;
            _txtPrefix.Text = _st.DimStylePrefix;
            LoadDims();
        }
        finally { _loading = false; }
        RefreshPreview();
    }

    private void LoadDims()
    {
        _dims.Clear();
        foreach (var d in _st.DimStyles) _dims.Add(new DimStyleRow(d, () => _st, Edited));
        RefreshDimPreviews();
    }

    private void AddDimStyle()
    {
        DialogUi.Commit(_gDim);
        string t = DialogUi.InputBox(this, "Thêm kiểu kích thước", "Tỷ lệ (vd. 1/40 hoặc 40):", "1/40");
        if (t == null) return;
        if (!DrawingStyle.TryParseScale(t, out double v) || v <= 0) { SetStatus("Tỷ lệ không hợp lệ: " + t); return; }
        if (_st.FindDimStyle(v) != null) { SetStatus("Đã có kiểu " + _st.DimStyleName(v)); return; }
        _st.DimStyles.Add(new DimStyleDef { Scale = Math.Round(v, 2) });
        _st.DimStyles = _st.DimStyles.OrderBy(x => x.Scale).ToList();
        LoadDims();
        Edited();
    }

    private void DeleteDimStyles()
    {
        DialogUi.Commit(_gDim);
        foreach (var r in _gDim.SelectedItems.OfType<DimStyleRow>().ToList()) { _st.DimStyles.Remove(r.D); _dims.Remove(r); }
        Edited();
    }

    private void ClearDimOverrides()
    {
        DialogUi.Commit(_gDim);
        var sel = _gDim.SelectedItems.OfType<DimStyleRow>().ToList();
        if (sel.Count == 0) sel = _dims.ToList();
        foreach (var r in sel)
        {
            var d = r.D;
            d.TextHeight = d.TickSize = d.ExtOffset = d.ExtBeyond = d.LineExtend = d.TextGap = d.BaselineSpacing = d.LinearFactor = null;
            d.Arrow = null; d.Decimals = null;
        }
        Edited();
    }

    /// <summary>Mẫu kích thước một tỷ lệ: chuỗi 2 đoạn dài tổng 60 mm giấy + tổng, vẽ ở mô hình (mm thật) tỷ lệ 1/scale.</summary>
    internal static DxfWriter DimSample(DrawingStyle st, double scale)
    {
        var w = new DxfWriter(scale) { Style = st };
        var dp = st.DimFor(scale);
        double L = Math.Round(60 * scale / 10) * 10, a = Math.Round(L * 0.4 / 10) * 10, th = w.Th;
        w.Line(Lay.BeTong, 0, 0, L, 0);
        w.Line(Lay.BeTong, 0, 0, 0, 6 * th); w.Line(Lay.BeTong, a, 0, a, 6 * th); w.Line(Lay.BeTong, L, 0, L, 6 * th);
        w.Dim(0, 6 * th, a, 6 * th, 3 * th, 1, null, dp);
        w.Dim(a, 6 * th, L, 6 * th, 3 * th, 1, null, dp);
        w.Dim(0, 6 * th, L, 6 * th, 3 * th + Math.Max(dp.BaselineSpacing, 4 * dp.TextHeight) * scale, 1, null, dp);
        w.Text(Lay.Chu, L + 3 * th, 0, th, dp.Name + "  (1/" + DialogUi.F(scale) + ")");
        return w;
    }

    private void RefreshDimPreviews()
    {
        _dimPreviews.Children.Clear();
        try
        {
            foreach (var d in _st.DimStyles)
            {
                var w = DimSample(_st, d.Scale);
                var e = w.Extents;
                double K = 1 / d.Scale, pad = 3, zoom = 2;
                var dg = SheetRender.Render(new[] { (w, K, 0.0, 0.0) }, e.X1 * K - pad, e.Y2 * K + pad, 0.12, x => x.GetPrimitives());
                _dimPreviews.Children.Add(new DrawingHost
                {
                    Drawing = dg, Scale = SheetRender.DipPerMm * zoom, Background = Brushes.White,
                    Width = (e.W * K + 2 * pad) * SheetRender.DipPerMm * zoom, Height = (e.H * K + 2 * pad) * SheetRender.DipPerMm * zoom,
                    HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4)
                });
            }
        }
        catch (Exception ex) { SetStatus("Lỗi xem trước kích thước: " + ex.Message); }
    }

    private void Edited()
    {
        if (_loading) return;
        foreach (var r in _scales) r.Refresh();
        foreach (var r in _sizes) r.Refresh();
        foreach (var r in _dims) r.Refresh();
        RefreshPreview();
        RefreshDimPreviews();
        SetStatus("Đã sửa – bấm \"Áp dụng\" để dùng cho các bản vẽ xuất ra / tờ in, hoặc \"Lưu làm mặc định\".");
    }

    // ------------------------------------------------------------------ xem trước
    /// <summary>Mẫu vẽ ở tỷ lệ 1/1 trên giấy (mm in) bằng đúng các hàm vẽ của bộ ghi DXF.</summary>
    internal static DxfWriter Sample(DrawingStyle st)
    {
        var w = new DxfWriter(1) { Style = st };
        double th = w.Th;
        // mặt cắt dầm 220×300 ở tl 1/25 → 8,8 × 12 mm giấy
        double k = 1.0 / 25, B = 220 * k, H = 300 * k, c = 25 * k;
        w.Rect(Lay.BeTong, 0, 0, B, H);
        w.Polyline(Lay.Dai, new List<(double X, double Y)> { (c, c), (B - c, c), (B - c, H - c), (c, H - c) }, true, 0.25);
        double r = 0.4;
        foreach (double x in new[] { c + 0.6, B / 2, B - c - 0.6 }) { w.Dot(Lay.Thep, x, H - c - 0.6, r); w.Dot(Lay.Thep, x, c + 0.6, r); }
        w.Line(Lay.KyHieu, c + 0.6, H - c - 0.6, B - c - 0.6, H - c - 0.6);
        w.LeaderTag(B / 2, H - c - 0.6, B / 2, H + 2.2 * th, "3Ø22", 1, 1, false, false);
        w.Line(Lay.KyHieu, c + 0.6, c + 0.6, B - c - 0.6, c + 0.6);
        w.LeaderTag(B / 2, c + 0.6, B / 2, -2.6 * th, "3Ø18", 2, 1, false, false);
        w.LeaderTag(B - c, H * 0.6, B + 2 * th, H * 0.6, "Ø6a100", 4, 1);
        w.Dim(0, 0, B, 0, -4.6 * th, k);
        w.Dim(0, 0, 0, H, 2.6 * th, k);
        w.ElevMark(-6 * th, H, "+3.600", -1);
        w.GridBubble(B / 2, -9.5 * th, "A");
        w.Text(Lay.Chu, -14 * th, -14 * th, th, "Ghi chú: bê tông dầm B25, thép CB400-V.");
        w.Title(B / 2, -16.5 * th, "1-1", DrawUtil_ScaleText(25), true);
        return w;
    }

    private static string DrawUtil_ScaleText(double den) => "tl: 1/" + Math.Round(den).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    private void RefreshPreview()
    {
        try
        {
            var w = Sample(_st);
            var e = w.Extents;
            double pad = 4, zoom = 2;
            var dg = SheetRender.Render(new[] { (w, 1.0, 0.0, 0.0) }, e.X1 - pad, e.Y2 + pad, 0.12, x => x.GetPrimitives());
            _preview.Drawing = dg;
            _preview.Scale = SheetRender.DipPerMm * zoom;
            _preview.Width = (e.W + 2 * pad) * SheetRender.DipPerMm * zoom;
            _preview.Height = (e.H + 2 * pad) * SheetRender.DipPerMm * zoom;
            _preview.InvalidateVisual();
        }
        catch (Exception ex) { SetStatus("Lỗi xem trước: " + ex.Message); }
    }

    // ------------------------------------------------------------------ lưu
    private void Commit()
    {
        DialogUi.Commit(_gScale); DialogUi.Commit(_gSize); DialogUi.Commit(_gDim);
        _st.DimStylePrefix = DrawingStyle.SafePrefix(_txtPrefix.Text);
        _st.Normalize();
    }

    /// <summary>Áp dụng: dùng ngay cho bản vẽ DXF / tờ in; "Lưu riêng cho dự án này" → ghi vào dự án.</summary>
    private void Apply()
    {
        Commit();
        if (_p != null && _chkProject.IsChecked == true)
        {
            _p.CadStandard ??= CadStandard.EffectiveFor(_p);
            _p.CadStandard.Drawing = _st.Clone();
        }
        else if (_p != null && _p.CadStandard != null)
        {
            // bỏ chọn "lưu riêng": dự án trở về chuẩn mặc định của người dùng (giữ thiết lập vừa sửa cho phiên làm việc)
            _p.CadStandard = null;
        }
        var std = (CadStandard.Active ?? CadStandard.EffectiveFor(_p)).Clone();
        std.Drawing = _st.Clone();
        CadStandard.Activate(std);
        Changed = true;
        SetStatus(_p != null && _chkProject.IsChecked == true ? "Đã áp dụng và lưu riêng cho dự án (nhớ lưu dự án)." : "Đã áp dụng cho phiên làm việc (chưa lưu làm mặc định).");
    }

    private void SaveDefault()
    {
        Commit();
        try
        {
            var d = CadStandard.LoadUserDefault();
            d.Drawing = _st.Clone();
            CadStandard.SaveUserDefault(d);
            Apply();
            SetStatus("Đã lưu làm mặc định và áp dụng: " + CadStandard.UserFilePath);
        }
        catch (Exception ex) { MessageBox.Show(this, "Không lưu được: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void RestoreDefault()
    {
        if (MessageBox.Show(this, "Trở về thiết lập mặc định của chương trình (chữ 2,5 mm, tên hình 3,5 mm, mặt đứng 1/50, mặt cắt 1/25, mặt bằng 1/100)?\n" +
                                  "Bấm \"Áp dụng\" hoặc \"Lưu làm mặc định\" để dùng.", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _st = DrawingStyle.Default();
        LoadAll();
        SetStatus("Đã nạp thiết lập mặc định của chương trình – chưa áp dụng.");
    }
}
