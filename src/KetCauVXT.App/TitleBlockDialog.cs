using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Drawing;
using KetCauVXT.Import;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Một chữ của khung tên nhập từ file (bảng gán trường).</summary>
internal sealed class TitleTextRow : RowBase
{
    public TitleBlockEntity E { get; }
    private readonly Action _changed;
    public TitleTextRow(TitleBlockEntity e, Action changed) { E = e; _changed = changed; }

    public string Text => E.Text;
    public string Tag => E.Tag;
    /// <summary>Trường gán (tên hiển thị); "" = giữ chữ gốc</summary>
    public string Field
    {
        get => TitleBlockDialog.FieldDisplay(E.Field);
        set { E.Field = TitleBlockDialog.FieldKey(value); Raise(); Raise(nameof(Placeholders)); _changed?.Invoke(); }
    }
    public string Placeholders => string.Join(", ", TitleFields.Placeholders(E.Text));
    public double H { get => E.H; set { if (value > 0) { E.H = value; Raise(); _changed?.Invoke(); } } }
    public double MaxW { get => E.MaxW; set { if (value >= 0) { E.MaxW = value; Raise(); _changed?.Invoke(); } } }
    public string Align
    {
        get => TitleBlockDialog.AlignNames[Math.Clamp(E.Ha, 0, 2)];
        set { int i = Array.IndexOf(TitleBlockDialog.AlignNames, value); if (i >= 0) { E.Ha = i; Raise(); _changed?.Invoke(); } }
    }
}

/// <summary>Chữ trường đặt thêm (khung tên ảnh / bổ sung cho khung tên vector).</summary>
internal sealed class TitleFieldRow : RowBase
{
    public TitleBlockField F { get; }
    private readonly Action _changed;
    public TitleFieldRow(TitleBlockField f, Action changed) { F = f; _changed = changed; }
    private void Done([System.Runtime.CompilerServices.CallerMemberName] string n = null) { Raise(n); _changed?.Invoke(); }

    public string Field { get => TitleBlockDialog.FieldDisplay(F.Key); set { F.Key = TitleBlockDialog.FieldKey(value); Done(); } }
    public string Format { get => F.Format; set { F.Format = value ?? ""; Done(); } }
    public double X { get => F.X; set { F.X = value; Done(); } }
    public double Y { get => F.Y; set { F.Y = value; Done(); } }
    public double H { get => F.H; set { if (value > 0) { F.H = value; Done(); } } }
    public double MaxW { get => F.MaxW; set { if (value >= 0) { F.MaxW = value; Done(); } } }
    public string Align
    {
        get => TitleBlockDialog.AlignNames[Math.Clamp(F.Ha, 0, 2)];
        set { int i = Array.IndexOf(TitleBlockDialog.AlignNames, value); if (i >= 0) { F.Ha = i; Done(); } }
    }
}

/// <summary>Hộp thoại "Tải khung tên từ file (DWG/DXF/ảnh)": chọn file, block, hệ số đơn vị, chế độ đặt (ô khung tên / khung bao toàn tờ),
/// điểm gốc; xem trước (nhấp vào chữ để chọn dòng gán trường; khung tên ảnh: nhấp để đặt vị trí chữ trường đang chọn);
/// bảng gán chữ → trường; chữ trường đặt thêm; ô logo.</summary>
internal sealed class TitleBlockDialog : Window
{
    public static readonly string[] AlignNames = { "Trái", "Giữa", "Phải" };
    private const string NoField = "(giữ chữ gốc)";

    public static List<string> FieldChoices()
    {
        var l = new List<string> { NoField };
        l.AddRange(TitleFields.All.Select(a => $"{a.Label} {{{a.Key}}}"));
        return l;
    }

    public static string FieldDisplay(string key)
    {
        if (string.IsNullOrEmpty(key)) return NoField;
        foreach (var a in TitleFields.All) if (a.Key == key) return $"{a.Label} {{{a.Key}}}";
        return $"{{{key}}}";
    }

    public static string FieldKey(string display)
    {
        if (string.IsNullOrWhiteSpace(display) || display == NoField) return "";
        int i = display.LastIndexOf('{'), j = display.LastIndexOf('}');
        string k = i >= 0 && j > i ? display.Substring(i + 1, j - i - 1) : display;
        return TitleFields.KeyOf(k);
    }

    private readonly Project _p;
    private readonly Sheet _sheet;
    private string _path;
    private TitleBlockDef _def;
    private TitleBlockImportResult _res;

    private readonly TextBox _txtFile;
    private readonly ComboBox _cbBlock, _cbMode, _cbBase;
    private readonly TextBox _txtUnit;
    private readonly CheckBox _chkFills, _chkValues, _chkInner;
    private readonly TextBlock _info;
    private readonly DrawingHost _host;
    private readonly Viewbox _view;
    private readonly DataGrid _gridTexts, _gridFields;
    private readonly TabControl _tabs;
    private readonly TextBox _txtName, _logoX, _logoY, _logoW, _logoH;
    private readonly ObservableCollection<TitleTextRow> _texts = new();
    private readonly ObservableCollection<TitleFieldRow> _fields = new();
    private double _ox, _oy;
    private bool _loading;

    /// <summary>Khung tên kết quả (khi Đồng ý).</summary>
    public TitleBlockDef Result { get; private set; }

    /// <summary>existing: khung tên đang dùng (sửa trường); path: mở ngay file này.</summary>
    public TitleBlockDialog(Project p, Sheet previewSheet, TitleBlockDef existing = null, string path = null)
    {
        _p = p;
        _sheet = previewSheet ?? p?.Sheets?.FirstOrDefault() ?? new Sheet { Number = "KC-01", Name = "TÊN BẢN VẼ" };
        DialogUi.Setup(this, "Khung tên người dùng – tải từ file DWG / DXF / ảnh", 1180, 760);

        var root = new DockPanel { Margin = new Thickness(10) };

        // ---- file + tuỳ chọn ----
        var top = new StackPanel();
        _txtFile = new TextBox { IsReadOnly = true, Width = 640, VerticalContentAlignment = VerticalAlignment.Center };
        top.Children.Add(DialogUi.Row(DialogUi.Label("File:", 70), _txtFile, DialogUi.Btn("Chọn file…", (s, e) => PickFile(), true)));
        _cbBlock = new ComboBox { Width = 220 };
        _txtUnit = new TextBox { Width = 70, Text = "0", ToolTip = "Số mm giấy ứng với 1 đơn vị bản vẽ; 0 = theo $INSUNITS (mm)" };
        _cbMode = new ComboBox { Width = 260, ItemsSource = new[] { "Chỉ ô khung tên (đặt ở góc phải dưới)", "Khung bao toàn tờ (co theo khổ giấy)" }, SelectedIndex = 0 };
        _cbBase = new ComboBox { Width = 140, ItemsSource = new[] { "Tự động", "Góc phải dưới", "Góc trái dưới", "Góc phải trên", "Góc trái trên", "Gốc toạ độ (0,0)" }, SelectedIndex = 0 };
        _chkFills = new CheckBox { Content = "Lấy miền tô (viền)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        top.Children.Add(DialogUi.Row(DialogUi.Label("Block:", 70), _cbBlock, DialogUi.Label("  mm / đơn vị:"), _txtUnit, DialogUi.Label("  Đặt:"), _cbMode,
            DialogUi.Label("  Điểm gốc:"), _cbBase));
        _chkInner = new CheckBox { Content = "Vẽ khung viền trong của chương trình (chế độ ô khung tên)", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        _chkInner.Click += (s, e) => { if (_def != null) _def.DrawInnerFrame = _chkInner.IsChecked == true; };
        _txtName = new TextBox { Width = 220 };
        top.Children.Add(DialogUi.Row(DialogUi.Label("Tên:", 70), _txtName, DialogUi.Label("  "), _chkFills, _chkInner,
            DialogUi.Btn("Nhập lại theo tuỳ chọn", (s, e) => Reimport())));
        _info = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkSlateGray, Margin = new Thickness(0, 2, 0, 4) };
        top.Children.Add(_info);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        // ---- nút ----
        var ok = DialogUi.Btn("Đồng ý – dùng khung tên này", (s, e) => Accept(), true);
        ok.IsDefault = true;
        var cancel = DialogUi.Btn("Huỷ", (s, e) => DialogResult = false);
        cancel.IsCancel = true;
        var bottom = DialogUi.Row(ok, cancel);
        bottom.HorizontalAlignment = HorizontalAlignment.Right;
        bottom.Margin = new Thickness(0, 6, 0, 0);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        // ---- giữa: xem trước | bảng ----
        var mid = new Grid();
        mid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        mid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new DockPanel { Margin = new Thickness(0, 0, 8, 0) };
        _chkValues = new CheckBox { Content = "Xem với giá trị của dự án / tờ hiện tại", IsChecked = true, Margin = new Thickness(0, 0, 0, 4) };
        _chkValues.Click += (s, e) => RenderPreview();
        var lp = new StackPanel();
        lp.Children.Add(_chkValues);
        lp.Children.Add(DialogUi.Hint("Nhấp vào chữ trên hình để chọn dòng gán trường. Khung tên ảnh: chọn dòng ở bảng \"Chữ trường đặt thêm\" rồi nhấp lên hình để đặt vị trí."));
        DockPanel.SetDock(lp, Dock.Top);
        left.Children.Add(lp);
        var logoRow = DialogUi.Row(DialogUi.Label("Ô logo (mm): x"), _logoX = NumBox(), DialogUi.Label(" y"), _logoY = NumBox(), DialogUi.Label(" rộng"), _logoW = NumBox(),
            DialogUi.Label(" cao"), _logoH = NumBox(), DialogUi.Btn("Áp dụng", (s, e) => ApplyLogo()));
        DockPanel.SetDock(logoRow, Dock.Bottom);
        left.Children.Add(logoRow);
        _host = new DrawingHost { Width = 200, Height = 60, Background = Brushes.White, Cursor = Cursors.Cross };
        _host.MouseLeftButtonDown += Host_MouseDown;
        _view = new Viewbox { Child = _host, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both };
        left.Children.Add(new Border { Child = _view, BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), Background = new SolidColorBrush(Color.FromRgb(236, 238, 241)), Padding = new Thickness(6) });
        Grid.SetColumn(left, 0);
        mid.Children.Add(left);

        _tabs = new TabControl();
        _gridTexts = DialogUi.NewGrid();
        _gridTexts.Columns.Add(DialogUi.Text("Chữ trong file", nameof(TitleTextRow.Text), true, 170));
        _gridTexts.Columns.Add(DialogUi.Text("Thẻ ATTRIB", nameof(TitleTextRow.Tag), true, 90));
        _gridTexts.Columns.Add(DialogUi.Combo("Gán trường", nameof(TitleTextRow.Field), FieldChoices(), 190));
        _gridTexts.Columns.Add(DialogUi.Text("Chỗ trống {…}", nameof(TitleTextRow.Placeholders), true, 110));
        _gridTexts.Columns.Add(DialogUi.Num("Cao chữ", nameof(TitleTextRow.H), "0.##", false, 60));
        _gridTexts.Columns.Add(DialogUi.Num("Rộng tối đa", nameof(TitleTextRow.MaxW), "0.#", false, 75));
        _gridTexts.Columns.Add(DialogUi.Combo("Căn", nameof(TitleTextRow.Align), AlignNames, 60));
        _gridTexts.ItemsSource = _texts;
        _gridTexts.SelectionChanged += (s, e) => RenderPreview();
        var t1 = new DockPanel();
        var h1 = DialogUi.Hint("Chữ có chỗ trống {TEN_CONG_TRINH}, {SO_HIEU}… hoặc thẻ ATTRIB/ATTDEF trùng tên trường được nhận tự động. Chọn \"Gán trường\" để thay cả dòng chữ bằng giá trị trường.");
        DockPanel.SetDock(h1, Dock.Top);
        t1.Children.Add(h1);
        t1.Children.Add(_gridTexts);
        _tabs.Items.Add(new TabItem { Header = "Chữ trong file → trường", Content = t1 });

        _gridFields = DialogUi.NewGrid();
        _gridFields.Columns.Add(DialogUi.Combo("Trường", nameof(TitleFieldRow.Field), FieldChoices().Skip(1).ToList(), 180));
        _gridFields.Columns.Add(DialogUi.Text("Mẫu chữ (trống = giá trị)", nameof(TitleFieldRow.Format), false, 150));
        _gridFields.Columns.Add(DialogUi.Num("x", nameof(TitleFieldRow.X), "0.#", false, 55));
        _gridFields.Columns.Add(DialogUi.Num("y", nameof(TitleFieldRow.Y), "0.#", false, 55));
        _gridFields.Columns.Add(DialogUi.Num("Cao", nameof(TitleFieldRow.H), "0.##", false, 50));
        _gridFields.Columns.Add(DialogUi.Num("Rộng tối đa", nameof(TitleFieldRow.MaxW), "0.#", false, 75));
        _gridFields.Columns.Add(DialogUi.Combo("Căn", nameof(TitleFieldRow.Align), AlignNames, 60));
        _gridFields.ItemsSource = _fields;
        _gridFields.SelectionChanged += (s, e) => RenderPreview();
        var t2 = new DockPanel();
        var fb = DialogUi.Row(DialogUi.Btn("Thêm trường", (s, e) => AddField()), DialogUi.Btn("Xoá trường", (s, e) => RemoveField()),
            DialogUi.Btn("Bố cục mặc định", (s, e) => DefaultFields()));
        DockPanel.SetDock(fb, Dock.Top);
        t2.Children.Add(fb);
        var h2 = DialogUi.Hint("Toạ độ mm so với điểm gốc của khung tên. Mẫu chữ ví dụ: \"TỶ LỆ: {TY_LE}\", \"Tờ {TRANG}/{TONG_TRANG}\".");
        DockPanel.SetDock(h2, Dock.Top);
        t2.Children.Add(h2);
        t2.Children.Add(_gridFields);
        _tabs.Items.Add(new TabItem { Header = "Chữ trường đặt thêm", Content = t2 });
        Grid.SetColumn(_tabs, 1);
        mid.Children.Add(_tabs);
        root.Children.Add(mid);
        Content = root;

        if (existing != null) SetDef(JsonClone(existing), null);
        else RenderPreview();
        if (!string.IsNullOrEmpty(path)) Loaded += (s, e) => { if (_def == null) LoadFile(path); };
    }

    private static TextBox NumBox() => new() { Width = 48, Text = "0", Margin = new Thickness(0, 0, 4, 0) };

    private static TitleBlockDef JsonClone(TitleBlockDef d) =>
        TemplateService.FromJson(TemplateService.ToJson(new TemplatePackage { TitleBlockDef = d })).TitleBlockDef;

    // ==================================================================
    // Nhập
    // ==================================================================
    private void PickFile()
    {
        var dlg = new OpenFileDialog { Filter = TitleBlockImporter.FileFilter, Title = "Chọn file khung tên (DWG, DXF hoặc ảnh PNG/JPG)" };
        if (dlg.ShowDialog(this) != true) return;
        _cbBlock.ItemsSource = null;
        LoadFile(dlg.FileName);
    }

    private TitleBlockImportOptions Options()
    {
        var o = new TitleBlockImportOptions
        {
            MmPerUnit = Math.Max(0, DialogUi.ParseNum(_txtUnit.Text, 0)),
            Mode = _cbMode.SelectedIndex == 1 ? TitleBlockMode.WholeFrame : TitleBlockMode.TitleBox,
            Base = _cbBase.SelectedIndex switch
            {
                1 => TitleBlockBase.BottomRight, 2 => TitleBlockBase.BottomLeft, 3 => TitleBlockBase.TopRight, 4 => TitleBlockBase.TopLeft,
                5 => TitleBlockBase.Origin, _ => TitleBlockBase.Auto
            },
            IncludeFills = _chkFills.IsChecked == true,
            Name = string.IsNullOrWhiteSpace(_txtName.Text) ? null : _txtName.Text.Trim()
        };
        int bi = _cbBlock.SelectedIndex;
        if (_cbBlock.ItemsSource != null && bi >= 0)
            o.Block = bi == 0 ? null : bi == 1 ? "*" : _cbBlock.SelectedItem as string;
        return o;
    }

    /// <summary>Mở file khung tên với tuỳ chọn hiện tại (gọi được từ tự kiểm tra).</summary>
    internal bool LoadFile(string path)
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var res = TitleBlockImporter.FromFile(path, Options());
            _path = path;
            _res = res;
            _txtFile.Text = path;
            _loading = true;
            try
            {
                if (_cbBlock.ItemsSource == null)
                {
                    var items = new List<string> { "(tự chọn)", "(toàn bộ model space)" };
                    items.AddRange(res.Blocks);
                    _cbBlock.ItemsSource = items;
                    _cbBlock.SelectedIndex = string.IsNullOrEmpty(res.UsedBlock) ? 0 : Math.Max(0, items.IndexOf(res.UsedBlock));
                }
                if (DialogUi.ParseNum(_txtUnit.Text, 0) <= 0) _txtUnit.Text = DialogUi.F(res.MmPerUnit, "0.####");
            }
            finally { _loading = false; }
            SetDef(res.Def, res);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Không đọc được khung tên: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private void Reimport()
    {
        if (string.IsNullOrEmpty(_path)) { PickFile(); return; }
        LoadFile(_path);
    }

    private void SetDef(TitleBlockDef def, TitleBlockImportResult res)
    {
        _def = def;
        _texts.Clear();
        _fields.Clear();
        if (def != null)
        {
            foreach (var e in def.Entities.Where(x => x.Type == "TEXT")) _texts.Add(new TitleTextRow(e, RenderPreview));
            foreach (var f in def.Fields) _fields.Add(new TitleFieldRow(f, RenderPreview));
            if (!_loading)
            {
                _txtName.Text = def.Name;
                _cbMode.SelectedIndex = def.Mode == TitleBlockMode.WholeFrame ? 1 : 0;
                _chkInner.IsChecked = def.DrawInnerFrame;
            }
            _logoX.Text = DialogUi.F(def.LogoX, "0.#"); _logoY.Text = DialogUi.F(def.LogoY, "0.#");
            _logoW.Text = DialogUi.F(def.LogoW, "0.#"); _logoH.Text = DialogUi.F(def.LogoH, "0.#");
            _tabs.SelectedIndex = def.Kind == TitleBlockKind.Image ? 1 : 0;
        }
        string warn = res != null && res.Warnings.Count > 0 ? "\n! " + string.Join("\n! ", res.Warnings.Take(4)) : "";
        _info.Text = def == null ? "Chưa có khung tên – bấm \"Chọn file…\"." :
            (res?.Info ?? $"{def.Name}: {def.Entities.Count} thực thể, {def.Fields.Count} chữ trường, {def.W:0.#}×{def.H:0.#} mm") +
            "\nTrường đang dùng: " + string.Join(", ", def.UsedFields().Select(TitleFields.Label)) + warn;
        RenderPreview();
    }

    private void ApplyLogo()
    {
        if (_def == null) return;
        _def.LogoX = DialogUi.ParseNum(_logoX.Text, 0); _def.LogoY = DialogUi.ParseNum(_logoY.Text, 0);
        _def.LogoW = Math.Max(0, DialogUi.ParseNum(_logoW.Text, 0)); _def.LogoH = Math.Max(0, DialogUi.ParseNum(_logoH.Text, 0));
        RenderPreview();
    }

    private void AddField()
    {
        if (_def == null) return;
        var f = new TitleBlockField { Key = TitleFields.TenCongTrinh, X = _def.MinX + 0.2 * _def.W, Y = _def.MinY + 0.5 * _def.H, H = 3 };
        _def.Fields.Add(f);
        var row = new TitleFieldRow(f, RenderPreview);
        _fields.Add(row);
        _tabs.SelectedIndex = 1;
        _gridFields.SelectedItem = row;
        RenderPreview();
    }

    private void RemoveField()
    {
        if (_def == null || _gridFields.SelectedItem is not TitleFieldRow r) return;
        _def.Fields.Remove(r.F);
        _fields.Remove(r);
        RenderPreview();
    }

    private void DefaultFields()
    {
        if (_def == null) return;
        var fs = TitleBlockImporter.DefaultImageFields(_def.W, _def.H);
        foreach (var f in fs) { f.X += _def.MinX; f.Y += _def.MinY; }
        _def.Fields = fs;
        SetDef(_def, _res);
    }

    // ==================================================================
    // Xem trước
    // ==================================================================
    private void RenderPreview()
    {
        if (_def == null)
        {
            _host.Width = 200; _host.Height = 60;
            _host.Drawing = null;
            _host.InvalidateVisual();
            return;
        }
        double pad = 3, w = Math.Max(10, _def.W), h = Math.Max(5, _def.H);
        _ox = _def.MinX - pad; _oy = _def.MaxY + pad;
        var values = _chkValues.IsChecked == true ? TitleFields.Values(_p, _sheet, "1/100") : null;
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w + 2 * pad, h + 2 * pad));
            if (_def.Kind == TitleBlockKind.Image)
            {
                var img = SheetRender.DecodeImage(_def.ImageData);
                if (img != null) dc.DrawImage(img, new Rect(_def.MinX - _ox, _oy - _def.MaxY, w, h));
            }
            var fig = SheetBuilder.TitleDefFigure(_def, values);
            dc.DrawDrawing(SheetRender.Render(new[] { (fig, 1.0, 0.0, 0.0) }, _ox, _oy, 0.12, x => x.GetPrimitives()));
            if (_def.LogoW > 0 && _def.LogoH > 0)
            {
                var pen = new Pen(Brushes.SteelBlue, 0.3) { DashStyle = DashStyles.Dash };
                dc.DrawRectangle(null, pen, new Rect(_def.LogoX - _ox, _oy - _def.LogoY - _def.LogoH, _def.LogoW, _def.LogoH));
                var logo = SheetRender.DecodeImage(_p?.TitleBlock?.LogoData);
                if (logo != null) dc.DrawImage(logo, new Rect(_def.LogoX - _ox, _oy - _def.LogoY - _def.LogoH, _def.LogoW, _def.LogoH));
            }
            var hi = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 140, 0)), 0.5);
            if (_gridTexts.SelectedItem is TitleTextRow tr && tr.E.Pts.Count >= 2)
                dc.DrawEllipse(null, hi, new Point(tr.E.Pts[0] - _ox, _oy - tr.E.Pts[1]), Math.Max(1.5, tr.E.H), Math.Max(1.5, tr.E.H));
            if (_gridFields.SelectedItem is TitleFieldRow fr)
                dc.DrawEllipse(null, hi, new Point(fr.F.X - _ox, _oy - fr.F.Y), Math.Max(1.5, fr.F.H), Math.Max(1.5, fr.F.H));
        }
        dg.Freeze();
        _host.Width = w + 2 * pad;
        _host.Height = h + 2 * pad;
        _host.Drawing = dg;
        _host.InvalidateVisual();
    }

    private void Host_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_def == null) return;
        var pt = e.GetPosition(_host);
        double x = pt.X + _ox, y = _oy - pt.Y;
        if (_tabs.SelectedIndex == 1 && _gridFields.SelectedItem is TitleFieldRow fr)
        {
            fr.X = Math.Round(x, 1);
            fr.Y = Math.Round(y, 1);
            return;
        }
        TitleTextRow best = null;
        double bd = double.MaxValue;
        foreach (var r in _texts)
        {
            if (r.E.Pts.Count < 2) continue;
            double tw = DxfWriter.TextWidth(r.E.Text, r.E.H);
            double cx = r.E.Pts[0] + (r.E.Ha == 0 ? tw / 2 : r.E.Ha == 2 ? -tw / 2 : 0), cy = r.E.Pts[1] + r.E.H / 2;
            double d = Math.Abs(cx - x) / Math.Max(1, tw / 2 + 1) + Math.Abs(cy - y) / Math.Max(1, r.E.H);
            if (d < bd) { bd = d; best = r; }
        }
        if (best != null && bd < 3)
        {
            _tabs.SelectedIndex = 0;
            _gridTexts.SelectedItem = best;
            _gridTexts.ScrollIntoView(best);
        }
    }

    // ==================================================================
    // Kết quả
    // ==================================================================
    private void Accept()
    {
        DialogUi.Commit(_gridTexts);
        DialogUi.Commit(_gridFields);
        if (_def == null) { MessageBox.Show(this, "Chưa tải khung tên.", Title, MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (!string.IsNullOrWhiteSpace(_txtName.Text)) _def.Name = _txtName.Text.Trim();
        _def.DrawInnerFrame = _chkInner.IsChecked == true;
        var mode = _cbMode.SelectedIndex == 1 ? TitleBlockMode.WholeFrame : TitleBlockMode.TitleBox;
        if (mode != _def.Mode)
        {
            _def.Mode = mode;
            _def.FitToPaper = mode == TitleBlockMode.WholeFrame;
        }
        ApplyLogo();
        _def.UpdateExtents();
        Result = _def;
        DialogResult = true;
    }

    /// <summary>Khung tên đang hiển thị (tự kiểm tra).</summary>
    internal TitleBlockDef Current => _def;
}
