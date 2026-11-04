using System.Collections;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Design;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.App;

/// <summary>Một thông số dạng chữ/số trong bảng "Định mức, thông số khác".</summary>
internal sealed class ParamRow : RowBase
{
    private readonly Func<string> _get;
    private readonly Action<string> _set;
    public string Name { get; }
    public string Unit { get; }
    public ParamRow(string name, string unit, Func<string> get, Action<string> set) { Name = name; Unit = unit; _get = get; _set = set; }
    public string Value
    {
        get => _get();
        set { try { _set((value ?? "").Trim()); } catch (FormatException) { } Raise(); }
    }
}

/// <summary>Một hàng hiển thị của bảng khối lượng (ô đã định dạng + cờ hàng tổng).</summary>
internal sealed class QtyDisplayRow
{
    public string[] Cells { get; init; } = Array.Empty<string>();
    public bool Total { get; init; }
}

/// <summary>Cửa sổ BẢNG KHỐI LƯỢNG VẬT TƯ: các trang Tổng hợp, Bê tông, Ván khuôn, Cốt thép, Tường-Trát, Hoàn thiện sàn,
/// Phân tích vật tư (chỉ xem) và trang Định mức (sửa định mức, đơn giá → lưu vào Project.QuantitySettings).</summary>
internal sealed class QuantityWindow : Window
{
    private const int MaxDisplayRows = 1500;
    private readonly Project _p;
    private readonly DesignSummary _ds;
    private readonly Action _changed;
    private readonly TabControl _tabs = new() { Margin = new Thickness(0, 4, 0, 4) };
    private readonly TextBlock _status;
    private TabItem _normTab;
    private readonly List<DataGrid> _editGrids = new();

    /// <summary>Kết quả tính gần nhất.</summary>
    public QuantityResult Result { get; private set; }

    public QuantityWindow(Project p, DesignSummary ds, Action changed)
    {
        _p = p; _ds = ds; _changed = changed ?? (() => { });
        _p.QuantitySettings ??= new QuantitySettings();
        DialogUi.Setup(this, "Bảng khối lượng vật tư – " + p.Name, 1280, 820);
        Help.Attach(this, "ho-so.khoi-luong");   // nút "?" + F1 – Help.cs
        ShowInTaskbar = true;
        var root = new DockPanel { Margin = new Thickness(8) };
        var top = DialogUi.Row(
            DialogUi.Btn("Tính lại", (s, e) => Recalc(), true),
            DialogUi.Btn("Xuất Excel…", (s, e) => AskExportExcel()),
            DialogUi.Btn("Xuất Word…", (s, e) => AskExportWord()),
            DialogUi.Btn("Đóng", (s, e) => Close()));
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var hint = DialogUi.Hint(ds == null
            ? "Chưa có kết quả thiết kế: chưa có khối lượng cốt thép và kích thước móng thiết kế (chạy Thiết kế BTCT / Thiết kế móng rồi mở lại)."
            : "Khối lượng tính từ mô hình và kết quả thiết kế hiện tại. Sửa định mức, đơn giá ở trang “Định mức” rồi bấm “Tính lại”.");
        DockPanel.SetDock(hint, Dock.Top); root.Children.Add(hint);
        _status = DialogUi.Hint("");
        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
        root.Children.Add(_tabs);
        Content = root;
        Recalc();
    }

    // ------------------------------------------------------------------
    public void Recalc()
    {
        foreach (var g in _editGrids) DialogUi.Commit(g);
        int sel = Math.Max(0, _tabs.SelectedIndex);
        try
        {
            Result = QuantityCalculator.Compute(_p, _ds, _p.QuantitySettings);
        }
        catch (Exception ex)
        {
            _status.Text = "Lỗi tính khối lượng: " + ex.Message;
            return;
        }
        var sheets = QuantityReport.BuildSheets(_p, Result);
        _normTab ??= BuildNormTab();
        _tabs.Items.Clear();
        foreach (var s in sheets.Where(s => s.Name != "Định mức"))
            _tabs.Items.Add(new TabItem { Header = s.Name, Content = SheetView(s) });
        _tabs.Items.Add(_normTab);
        _tabs.SelectedIndex = Math.Min(sel, _tabs.Items.Count - 1);
        var q = Result;
        _status.Text = $"Bê tông {q.ConcreteTotal:0.00} m³ (+ lót {q.LeanTotal:0.00} m³) · ván khuôn {q.FormworkTotal:0.0} m² · cốt thép {q.RebarTotal / 1000:0.000} tấn · xây {q.MasonryTotal:0.00} m³ · trát {q.PlasterTotal:0.0} m²"
                       + (q.Warnings.Count > 0 ? $" · {q.Warnings.Count} lưu ý (xem trang Tổng hợp)" : "");
    }

    private static UIElement SheetView(QtySheet s)
    {
        var sp = new StackPanel { Margin = new Thickness(4) };
        foreach (var t in s.Tables)
        {
            sp.Children.Add(DialogUi.Label(t.Title, double.NaN, true));
            sp.Children.Add(TableGrid(t));
        }
        return new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private static DataGrid TableGrid(QtyTable t)
    {
        var g = DialogUi.NewGrid();
        g.IsReadOnly = true; g.MaxHeight = 460; g.CanUserSortColumns = false;
        for (int i = 0; i < t.Headers.Length; i++)
        {
            var c = new DataGridTextColumn { Header = t.Headers[i], Binding = new Binding($"Cells[{i}]") { Mode = BindingMode.OneWay } };
            if (i == t.Headers.Length - 1 || t.Headers[i].Contains('(')) c.ElementStyle = RightStyle();
            g.Columns.Add(c);
        }
        var rows = new List<QtyDisplayRow>();
        for (int r = 0; r < t.Rows.Count && r < MaxDisplayRows; r++)
        {
            var cells = new string[t.Headers.Length];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = i < t.Rows[r].Length ? QuantityReport.Fmt(t.Rows[r][i], t.Rows[r][i] is double d && Math.Abs(d) >= 1000 ? "0.0" : "0.00") : "";
            rows.Add(new QtyDisplayRow { Cells = cells, Total = t.TotalRows.Contains(r) });
        }
        if (t.Rows.Count > MaxDisplayRows)
            rows.Add(new QtyDisplayRow { Cells = new[] { $"… còn {t.Rows.Count - MaxDisplayRows} hàng – xem đầy đủ trong file Excel" }.Concat(Enumerable.Repeat("", Math.Max(0, t.Headers.Length - 1))).ToArray() });
        var st = new Style(typeof(DataGridRow));
        var trig = new DataTrigger { Binding = new Binding(nameof(QtyDisplayRow.Total)), Value = true };
        trig.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        trig.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(232, 239, 250))));
        st.Triggers.Add(trig);
        g.RowStyle = st;
        g.ItemsSource = rows;
        return g;
    }

    private static Style RightStyle()
    {
        var st = new Style(typeof(TextBlock));
        st.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        return st;
    }

    // ------------------------------------------------------------------
    // ĐỊNH MỨC (sửa được)
    // ------------------------------------------------------------------
    private TabItem BuildNormTab()
    {
        var qs = _p.QuantitySettings;
        var sp = new StackPanel { Margin = new Thickness(4) };
        sp.Children.Add(DialogUi.Hint($"Định mức hiện hành: Định mức xây dựng ban hành kèm {Std.Norms} (Phụ lục VII – định mức vật liệu, đã được thay thế theo {Std.NormLetter}). "
            + "Giá trị mặc định trong chương trình là THAM KHẢO (số liệu hệ định mức cũ) – người dùng phải đối chiếu và nhập lại theo định mức hiện hành: sửa trực tiếp trong bảng "
            + "hoặc bấm “Nhập định mức từ Excel…” (cột: mã hiệu, tên, đơn vị, vật liệu, định mức). Đơn giá để trống (0) → bảng không có cột thành tiền."));
        var src = new TextBox { Text = qs.NormSource ?? "", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) };
        src.LostFocus += (s, e) => { qs.NormSource = src.Text.Trim(); _changed(); };
        sp.Children.Add(DialogUi.Label("Ghi chú nguồn định mức (để trống = nguồn mặc định theo danh mục văn bản):", double.NaN, true));
        sp.Children.Add(src);

        var gc = EditGrid(qs.ConcreteNorms);
        gc.Columns.Add(DialogUi.Text("Cấp độ bền", nameof(ConcreteMixNorm.Grade), false, 90));
        gc.Columns.Add(DialogUi.Text("Mác tương đương", nameof(ConcreteMixNorm.Mark), false, 110));
        gc.Columns.Add(DialogUi.Num("Xi măng (kg/m³)", nameof(ConcreteMixNorm.Cement), "0.##", false, 110));
        gc.Columns.Add(DialogUi.Num("Cát vàng (m³/m³)", nameof(ConcreteMixNorm.Sand), "0.###", false, 110));
        gc.Columns.Add(DialogUi.Num("Đá dăm (m³/m³)", nameof(ConcreteMixNorm.Stone), "0.###", false, 110));
        gc.Columns.Add(DialogUi.Num("Nước (lít/m³)", nameof(ConcreteMixNorm.Water), "0.#", false, 90));
        gc.Columns.Add(DialogUi.Text("Ghi chú", nameof(ConcreteMixNorm.Note), false, 220));
        sp.Children.Add(DialogUi.Label("Cấp phối 1 m³ bê tông", double.NaN, true)); sp.Children.Add(gc);

        var gm = EditGrid(qs.MortarNorms);
        gm.Columns.Add(DialogUi.Text("Mác vữa", nameof(MortarMixNorm.Grade), false, 90));
        gm.Columns.Add(DialogUi.Num("Xi măng (kg/m³)", nameof(MortarMixNorm.Cement), "0.##", false, 110));
        gm.Columns.Add(DialogUi.Num("Cát vàng (m³/m³)", nameof(MortarMixNorm.Sand), "0.###", false, 110));
        gm.Columns.Add(DialogUi.Num("Nước (lít/m³)", nameof(MortarMixNorm.Water), "0.#", false, 90));
        gm.Columns.Add(DialogUi.Text("Ghi chú", nameof(MortarMixNorm.Note), false, 220));
        sp.Children.Add(DialogUi.Label("Cấp phối 1 m³ vữa xi măng – cát vàng", double.NaN, true)); sp.Children.Add(gm);

        var gb = EditGrid(qs.MasonryNorms);
        gb.Columns.Add(DialogUi.Text("Áp dụng", nameof(MasonryNorm.Name), false, 160));
        gb.Columns.Add(DialogUi.Num("Tường dày ≤ (m)", nameof(MasonryNorm.MaxThickness), "0.###", false, 110));
        gb.Columns.Add(DialogUi.Num("Gạch (viên/m³)", nameof(MasonryNorm.Bricks), "0.#", false, 110));
        gb.Columns.Add(DialogUi.Num("Vữa (m³/m³)", nameof(MasonryNorm.Mortar), "0.###", false, 100));
        gb.Columns.Add(DialogUi.Text("Ghi chú", nameof(MasonryNorm.Note), false, 220));
        sp.Children.Add(DialogUi.Label("Định mức xây 1 m³ tường", double.NaN, true)); sp.Children.Add(gb);

        var gp = DialogUi.NewGrid();
        gp.Columns.Add(DialogUi.Text("Thông số", nameof(ParamRow.Name), true, 380));
        gp.Columns.Add(DialogUi.Text("Giá trị", nameof(ParamRow.Value), false, 160));
        gp.Columns.Add(DialogUi.Text("Đơn vị", nameof(ParamRow.Unit), true, 140));
        gp.ItemsSource = ParamRows(qs);
        gp.CellEditEnding += (s, e) => _changed();
        _editGrids.Add(gp);
        sp.Children.Add(DialogUi.Label("Định mức, thông số khác", double.NaN, true)); sp.Children.Add(gp);

        var gpr = EditGrid(qs.Prices);
        gpr.Columns.Add(DialogUi.Text("Vật tư (trùng tên trong bảng tổng hợp vật tư)", nameof(UnitPrice.Item), false, 320));
        gpr.Columns.Add(DialogUi.Text("Đơn vị", nameof(UnitPrice.Unit), false, 80));
        gpr.Columns.Add(DialogUi.Num("Đơn giá (đ)", nameof(UnitPrice.Price), "0.##", false, 130));
        sp.Children.Add(DialogUi.Label("Đơn giá vật tư (tuỳ chọn)", double.NaN, true));
        sp.Children.Add(DialogUi.Row(
            DialogUi.Btn("Lấy danh mục vật tư từ bảng", (s, e) => FillPriceItems(gpr)),
            DialogUi.Btn("Khôi phục định mức mặc định", (s, e) => RestoreDefaults(gc, gm, gb)),
            DialogUi.Btn("Nhập định mức từ Excel…", (s, e) => ImportNorms(gc, gm, src)),
            DialogUi.Btn("Xuất file mẫu định mức…", (s, e) => NormTemplate())));
        sp.Children.Add(gpr);
        return new TabItem { Header = "Định mức", Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
    }

    private DataGrid EditGrid(IList items)
    {
        var g = DialogUi.NewGrid();
        g.CanUserAddRows = true; g.CanUserDeleteRows = true; g.MaxHeight = 300;
        g.ItemsSource = items;
        g.CellEditEnding += (s, e) => _changed();
        _editGrids.Add(g);
        return g;
    }

    private static double Num(string s, double def) => DialogUi.ParseNum(s, def);

    private List<ParamRow> ParamRows(QuantitySettings qs) => new()
    {
        new("Tên gạch xây", "", () => qs.BrickName, v => qs.BrickName = v),
        new("Mác vữa xây tường", "", () => qs.MasonryMortar, v => qs.MasonryMortar = v),
        new("Mác vữa trát tường", "", () => qs.PlasterMortar, v => qs.PlasterMortar = v),
        new("Mác vữa lót, láng, trát trần (cấu tạo sàn)", "", () => qs.FloorMortar, v => qs.FloorMortar = v),
        new("Vữa trát cho lớp dày 15 mm", "m³/m²", () => DialogUi.F(qs.PlasterMortarPerM2), v => qs.PlasterMortarPerM2 = Math.Max(0, Num(v, qs.PlasterMortarPerM2))),
        new("Dây thép buộc", "kg/tấn thép", () => DialogUi.F(qs.TieWirePerTon), v => qs.TieWirePerTon = Math.Max(0, Num(v, qs.TieWirePerTon))),
        new("Gỗ ván khuôn", "m³/100 m²", () => DialogUi.F(qs.FormworkWoodPer100), v => qs.FormworkWoodPer100 = Math.Max(0, Num(v, qs.FormworkWoodPer100))),
        new("Đinh", "kg/100 m²", () => DialogUi.F(qs.FormworkNailsPer100), v => qs.FormworkNailsPer100 = Math.Max(0, Num(v, qs.FormworkNailsPer100))),
        new("Bê tông lót: cấp độ bền", "", () => qs.LeanGrade, v => qs.LeanGrade = v),
        new("Bê tông lót: chiều dày", "m", () => DialogUi.F(qs.LeanThickness), v => qs.LeanThickness = Math.Clamp(Num(v, qs.LeanThickness), 0, 0.5)),
        new("Bê tông lót: mở rộng mỗi phía", "m", () => DialogUi.F(qs.LeanOffset), v => qs.LeanOffset = Math.Clamp(Num(v, qs.LeanOffset), 0, 1)),
        new("Tính cầu thang (Có/Không)", "", () => qs.IncludeStairs ? "Có" : "Không", v => qs.IncludeStairs = Yes(v)),
        new("Cầu thang mặc định (lỗ sàn chưa khai báo): chiều dày bản", "m", () => DialogUi.F(qs.StairSlabThickness), v => qs.StairSlabThickness = Math.Clamp(Num(v, qs.StairSlabThickness), 0.06, 0.4)),
        new("Cầu thang mặc định: bậc (Gạch / BTCT)", "", () => qs.StairSteps == StepConstruction.Concrete ? "BTCT" : "Gạch",
            v => qs.StairSteps = (v ?? "").Trim().ToUpperInvariant().StartsWith("B") ? StepConstruction.Concrete : StepConstruction.Brick),
        new("Cầu thang mặc định: ốp bậc (Granite / Gỗ / Gạch / Không)", "", () => Stair.FinishName(qs.StairFinish),
            v => { string t = (v ?? "").Trim().ToLowerInvariant(); qs.StairFinish = t.StartsWith("gỗ") || t.StartsWith("go") ? StepFinish.Wood : t.StartsWith("gạch") || t.StartsWith("gach") ? StepFinish.Tile : t.StartsWith("k") ? StepFinish.None : StepFinish.Granite; }),
        new("Xây bậc thang gạch: gạch", "viên/m³", () => DialogUi.F(qs.StepBricks), v => qs.StepBricks = Math.Max(0, Num(v, qs.StepBricks))),
        new("Xây bậc thang gạch: vữa xây", "m³/m³", () => DialogUi.F(qs.StepMortar), v => qs.StepMortar = Math.Max(0, Num(v, qs.StepMortar))),
        new("Tính lanh tô (Có/Không)", "", () => qs.IncludeLintels ? "Có" : "Không", v => qs.IncludeLintels = Yes(v)),
        new("Lanh tô: chiều cao tiết diện", "m", () => DialogUi.F(qs.LintelHeight), v => qs.LintelHeight = Math.Clamp(Num(v, qs.LintelHeight), 0, 1)),
        new("Lanh tô: đoạn gác mỗi bên", "m", () => DialogUi.F(qs.LintelBearing), v => qs.LintelBearing = Math.Clamp(Num(v, qs.LintelBearing), 0, 1)),
        new("Chiều cao cửa quy ước (đổi diện tích cửa → bề rộng)", "m", () => DialogUi.F(qs.DoorHeight), v => qs.DoorHeight = Math.Clamp(Num(v, qs.DoorHeight), 0.5, 5)),
    };

    private static bool Yes(string v)
    {
        string s = (v ?? "").Trim().ToLowerInvariant();
        return s is "có" or "co" or "c" or "1" or "true" or "yes" or "y" or "x";
    }

    private void FillPriceItems(DataGrid g)
    {
        DialogUi.Commit(g);
        var qs = _p.QuantitySettings;
        if (Result == null) Recalc();
        int added = 0;
        foreach (var m in Result?.Materials ?? new List<MaterialTotal>())
        {
            if (qs.Prices.Any(x => string.Equals((x.Item ?? "").Trim(), m.Material, StringComparison.OrdinalIgnoreCase))) continue;
            qs.Prices.Add(new UnitPrice { Item = m.Material, Unit = m.Unit });
            added++;
        }
        g.ItemsSource = null; g.ItemsSource = qs.Prices;
        if (added > 0) _changed();
        _status.Text = $"Đã thêm {added} vật tư vào bảng đơn giá – nhập đơn giá rồi bấm “Tính lại”.";
    }

    private void RestoreDefaults(DataGrid gc, DataGrid gm, DataGrid gb)
    {
        if (MessageBox.Show(this, "Khôi phục cấp phối bê tông, vữa và định mức xây về giá trị mặc định?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var qs = _p.QuantitySettings;
        qs.ConcreteNorms = QuantitySettings.DefaultConcreteNorms();
        qs.MortarNorms = QuantitySettings.DefaultMortarNorms();
        qs.MasonryNorms = QuantitySettings.DefaultMasonryNorms();
        gc.ItemsSource = qs.ConcreteNorms; gm.ItemsSource = qs.MortarNorms; gb.ItemsSource = qs.MasonryNorms;
        _changed();
        Recalc();
    }

    /// <summary>Nhập bảng định mức hiện hành từ Excel/CSV (Model/NormImport.cs): áp cấp phối bê tông, vữa; lưu bảng tham khảo.</summary>
    private void ImportNorms(DataGrid gc, DataGrid gm, TextBox src)
    {
        foreach (var g in _editGrids) DialogUi.Commit(g);
        var dlg = new OpenFileDialog { Filter = "Bảng định mức (*.xlsx;*.csv)|*.xlsx;*.xlsm;*.csv;*.txt|Tất cả|*.*", Title = "Nhập định mức từ Excel (mã hiệu, tên, đơn vị, vật liệu, định mức)" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var qs = _p.QuantitySettings;
            var r = NormImport.ImportFile(qs, dlg.FileName);
            gc.ItemsSource = null; gc.ItemsSource = qs.ConcreteNorms;
            gm.ItemsSource = null; gm.ItemsSource = qs.MortarNorms;
            src.Text = qs.NormSource ?? "";
            if (r.Rows > 0) { _changed(); Recalc(); }
            string msg = r.ToString();
            if (r.Messages.Count > 0) msg += "\n\n" + string.Join("\n", r.Messages.Take(15)) + (r.Messages.Count > 15 ? "\n…" : "");
            msg += "\n\nĐối chiếu lại cấp phối với văn bản định mức gốc trước khi dùng.";
            MessageBox.Show(this, msg, "Nhập định mức từ Excel", MessageBoxButton.OK, r.Rows > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            _status.Text = r.ToString();
        }
        catch (Exception ex) { MessageBox.Show(this, "Không đọc được file:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void NormTemplate()
    {
        string path = AskPath("Excel (*.xlsx)|*.xlsx", "MauDinhMuc.xlsx", "Xuất file mẫu định mức");
        if (path == null) return;
        try { NormImport.WriteTemplate(path); _status.Text = "Đã xuất file mẫu: " + path + " – điền theo bảng định mức hiện hành rồi “Nhập định mức từ Excel…”."; }
        catch (Exception ex) { MessageBox.Show(this, "Không ghi được file:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    // ------------------------------------------------------------------
    private string AskPath(string filter, string name, string title)
    {
        var dlg = new SaveFileDialog { Filter = filter, FileName = name, Title = title };
        return dlg.ShowDialog(this) == true ? dlg.FileName : null;
    }

    /// <summary>Xuất Excel (không hộp thoại) – dùng cho tự kiểm tra.</summary>
    public void ExportExcelTo(string path)
    {
        if (Result == null) Recalc();
        QuantityReport.ExportExcel(_p, Result, path);
    }

    private void AskExportExcel()
    {
        Recalc();
        string path = AskPath("Excel (*.xlsx)|*.xlsx", QuantityReport.DefaultFileName, "Lưu bảng khối lượng vật tư");
        if (path == null) return;
        try { ExportExcelTo(path); _status.Text = "Đã xuất " + path; MessageBox.Show(this, "Đã xuất bảng khối lượng:\n" + path, Title, MessageBoxButton.OK, MessageBoxImage.Information); }
        catch (Exception ex) { MessageBox.Show(this, "Lỗi xuất Excel: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void AskExportWord()
    {
        Recalc();
        string path = AskPath("Word (*.docx)|*.docx", "BangKhoiLuong.docx", "Lưu bảng khối lượng (Word)");
        if (path == null) return;
        try { QuantityReport.ExportWord(_p, Result, path); _status.Text = "Đã xuất " + path; MessageBox.Show(this, "Đã xuất bảng khối lượng:\n" + path, Title, MessageBoxButton.OK, MessageBoxImage.Information); }
        catch (Exception ex) { MessageBox.Show(this, "Lỗi xuất Word: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}
