using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Analysis;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.App;

/// <summary>[Truyền tải] Cửa sổ "Truyền tải trọng & gợi ý móng": tải từng sàn, tải chân cột (so sánh phân tích khung),
/// móng đơn / móng băng / móng cọc theo tải từng chân cột, đề xuất, áp dụng, xuất Word.</summary>
internal sealed class TakedownWindow : Window
{
    private readonly Project _p;
    private readonly AnalysisResults _r;
    private TakedownAdvice _adv;
    private readonly CheckBox _chkPsi, _chkGravity, _chkAutoSides;
    private readonly CheckBox[] _chkSide = new CheckBox[4];
    private readonly TextBox _txtPress, _txtK;
    private readonly StackPanel _summary = new();
    private readonly DataGrid _gStory, _gCol, _gIso, _gStrip, _gPile, _gPit;

    /// <summary>Phương án đã áp dụng ("" = không)</summary>
    public string Applied { get; private set; } = "";
    public string AppliedText { get; private set; } = "";

    private static string F(double v, string f = "0.#") => double.IsFinite(v) ? v.ToString(f, CultureInfo.InvariantCulture) : "–";

    private sealed class Row
    {
        public string A { get; init; } = ""; public string B { get; init; } = ""; public string C { get; init; } = ""; public string D { get; init; } = "";
        public string E { get; init; } = ""; public string Fv { get; init; } = ""; public string G { get; init; } = ""; public string H { get; init; } = "";
        public string I { get; init; } = ""; public string J { get; init; } = ""; public string K { get; init; } = ""; public string L { get; init; } = "";
    }

    private static DataGrid Grid(params (string Head, string Path, double W)[] cols)
    {
        var g = DialogUi.NewGrid();
        g.IsReadOnly = true;
        var wrap = new Style(typeof(TextBlock));
        wrap.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        foreach (var (h, path, w) in cols)
        {
            var c = DialogUi.Text(h, path, true, w);
            c.ElementStyle = wrap;
            g.Columns.Add(c);
        }
        return g;
    }

    public TakedownWindow(Project p, AnalysisResults r)
    {
        _p = p; _r = r;
        DialogUi.Setup(this, "Truyền tải trọng xuống chân cột & gợi ý phương án móng", 1180, 800);
        var root = new DockPanel { Margin = new Thickness(10) };

        _chkPsi = new CheckBox { Content = "Giảm hoạt tải theo diện chịu tải và số tầng (ψA1, ψn1 – TCVN 2737)", IsChecked = true, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        var top = DialogUi.Row(_chkPsi, DialogUi.Btn("Tính lại", (s, e) => Compute(), true), DialogUi.Btn("Xuất thuyết minh Word…", (s, e) => ExportWord()));
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        // cọc ép theo lực ép + cột sát ranh
        var fs = p.Foundation ?? new FoundationSettings();
        _txtPress = new TextBox { Text = F(fs.PilePressForce, "0.#"), Width = 50, Margin = new Thickness(4, 0, 4, 0), VerticalContentAlignment = VerticalAlignment.Center };
        _txtK = new TextBox { Text = F(fs.PilePressFactor, "0.0#"), Width = 40, Margin = new Thickness(4, 0, 12, 0), VerticalContentAlignment = VerticalAlignment.Center };
        _chkAutoSides = new CheckBox { Content = "Ranh đất: tự động", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "Dùng ranh đất đã khai báo; nếu chưa có thì hai cạnh dài của nhà phố (giáp nhà bên)" };
        string[] sideNames = { "Trái", "Phải", "Trước", "Sau" };
        var row2 = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        row2.Children.Add(new TextBlock { Text = "Cọc ép: lực ép đầu cọc Pép (tấn)", VerticalAlignment = VerticalAlignment.Center });
        row2.Children.Add(_txtPress);
        row2.Children.Add(new TextBlock { Text = "k = Pép/[P]", VerticalAlignment = VerticalAlignment.Center, ToolTip = "TCVN 9394: Pép,min = (1,5–2)·[P], thường lấy k = 2" });
        row2.Children.Add(_txtK);
        row2.Children.Add(_chkAutoSides);
        for (int i = 0; i < 4; i++)
        {
            _chkSide[i] = new CheckBox { Content = "sát ranh " + sideNames[i], IsEnabled = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            row2.Children.Add(_chkSide[i]);
        }
        _chkAutoSides.Checked += (s, e) => { foreach (var c in _chkSide) c.IsEnabled = false; };
        _chkAutoSides.Unchecked += (s, e) => { foreach (var c in _chkSide) c.IsEnabled = true; };
        DockPanel.SetDock(row2, Dock.Top);
        root.Children.Add(row2);
        var sv = new ScrollViewer { Content = _summary, MaxHeight = 210, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 6, 0, 6) };
        DockPanel.SetDock(sv, Dock.Top);
        root.Children.Add(sv);

        var bottom = new StackPanel();
        _chkGravity = new CheckBox
        {
            Content = "Khi áp dụng: thiết kế móng theo tải đứng – mô men chân cột do giằng móng chịu, bỏ tổ hợp có gió / động đất (cách làm phổ biến cho nhà phố thấp tầng)",
            IsChecked = true, Margin = new Thickness(0, 4, 0, 4)
        };
        bottom.Children.Add(_chkGravity);
        var bs = DialogUi.Row(
            DialogUi.Btn("Áp dụng: Móng đơn", (s, e) => DoApply("Isolated")),
            DialogUi.Btn("Áp dụng: Móng băng", (s, e) => DoApply("Strip")),
            DialogUi.Btn("Áp dụng: Móng cọc", (s, e) => DoApply("Pile")),
            DialogUi.Btn("Áp dụng phương án đề xuất", (s, e) => DoApply(_adv?.Recommended ?? "Isolated"), true),
            DialogUi.Btn("Đóng", (s, e) => Close()));
        bs.HorizontalAlignment = HorizontalAlignment.Right;
        bottom.Children.Add(bs);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        _gStory = Grid(("Sàn", "A", 110), ("Cao độ (m)", "B", 70), ("Diện tích (m²)", "C", 85), ("Tĩnh tải G (kN)", "D", 90), ("Hoạt tải P (kN)", "E", 90),
            ("(G+P)/m² (kN/m²)", "Fv", 95), ("Thành phần (kN/m²)", "G", 540));
        _gCol = Grid(("Chân cột", "A", 85), ("Tiết diện", "B", 70), ("Diện chịu tải (m²)", "C", 90), ("Số sàn HT", "D", 60), ("G (kN)", "E", 70), ("P (kN)", "Fv", 70),
            ("ψ", "G", 45), ("Ntc (kN)", "H", 70), ("Ntt (kN)", "I", 70), ("Khung Ntt max (kN)", "J", 95), ("Khung/truyền tải", "K", 85), ("Thành phần (kN)", "L", 330));
        _gIso = Grid(("Chân cột", "A", 90), ("Ntc (kN)", "B", 75), ("Ntt (kN)", "C", 75), ("R (kN/m²)", "D", 75), ("Móng b × b (m)", "E", 100), ("H (m)", "Fv", 60),
            ("Số cọc (cọc đề xuất)", "G", 120), ("Ghi chú", "H", 300));
        _gStrip = Grid(("Dải", "A", 110), ("Số cột", "B", 60), ("Dài (m)", "C", 70), ("ΣNtc (kN)", "D", 90), ("B (m)", "E", 70), ("Bản móng hb (m)", "Fv", 100),
            ("Sườn b × h (m)", "G", 110));
        _gPile = Grid(("Loại cọc", "A", 120), ("Dài L (m)", "B", 70), ("[P] (kN)", "C", 70), ("Nguồn [P]", "D", 260), ("Tổng số cọc", "E", 85), ("Bê tông cọc (m³)", "Fv", 100),
            ("Số cọc từng chân cột", "G", 360));
        _gPit = Grid(("Hố móng", "A", 70), ("Chân cột", "B", 90), ("Vị trí so với ranh", "C", 200), ("ΣNtt (kN)", "D", 75), ("e x / y (m)", "E", 85), ("R1/N", "Fv", 50),
            ("N tính cọc (kN)", "G", 90), ("Số cọc", "H", 55), ("Bố trí", "I", 60), ("Đài Lx × Ly (m)", "J", 100), ("Ghi chú", "K", 330));
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Tải từng sàn", Content = _gStory });
        tabs.Items.Add(new TabItem { Header = "Tải chân cột", Content = _gCol });
        tabs.Items.Add(new TabItem { Header = "Móng đơn / số cọc từng cột", Content = _gIso });
        tabs.Items.Add(new TabItem { Header = "Móng băng", Content = _gStrip });
        tabs.Items.Add(new TabItem { Header = "Móng cọc", Content = _gPile });
        tabs.Items.Add(new TabItem { Header = "Hố móng – số cọc", Content = _gPit });
        tabs.SelectedIndex = 1;
        root.Children.Add(tabs);
        Content = root;
        Compute();
    }

    private void Compute()
    {
        try
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            double pt = double.TryParse(_txtPress.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v1) ? v1 : 50;
            double pk = double.TryParse(_txtK.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v2) && v2 > 1 ? v2 : 2;
            List<BoundarySide> sides = null;
            if (_chkAutoSides.IsChecked != true)
            {
                sides = new List<BoundarySide>();
                for (int i = 0; i < 4; i++) if (_chkSide[i].IsChecked == true) sides.Add((BoundarySide)i);
            }
            _adv = TakedownAdvisor.Advise(_p, _r, new TakedownOptions { LiveReduction = _chkPsi.IsChecked == true }, null,
                new AdviseOptions { PressForce = pt, PressFactor = pk, Sides = sides });
            if (_chkAutoSides.IsChecked == true)
                for (int i = 0; i < 4; i++) _chkSide[i].IsChecked = _adv.Boundaries.Any(b => (int)b.Side == i);
        }
        catch (Exception ex) { MessageBox.Show(this, "Không truyền tải được: " + ex.Message, "Việt Const", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        finally { Cursor = null; }
        var t = _adv.T;
        _summary.Children.Clear();
        var ex1 = DialogUi.Label(_adv.Explanation, double.NaN, true);
        ex1.Foreground = Brushes.DarkGreen;
        _summary.Children.Add(ex1);
        _summary.Children.Add(DialogUi.Hint(_adv.SoilNote));
        if (_adv.Press != null) foreach (var n in _adv.Press.Notes) _summary.Children.Add(DialogUi.Hint("• Cọc ép: " + n));
        foreach (var n in t.Notes.Concat(_adv.Notes)) _summary.Children.Add(DialogUi.Hint("• " + n));
        foreach (var w in t.Warnings)
        {
            var tb = DialogUi.Hint("⚠ " + w);
            tb.Foreground = Brushes.DarkOrange;
            _summary.Children.Add(tb);
        }
        _gStory.ItemsSource = t.Stories.Select(s => new Row
        {
            A = s.Story, B = F(s.Elevation, "0.00"), C = F(s.FloorArea), D = F(s.G, "0"), E = F(s.P, "0"), Fv = F(s.Intensity, "0.0"),
            G = s.FloorArea > 0 ? string.Join("; ", s.Sources.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {F(kv.Value / s.FloorArea, "0.00")}"))
                                : string.Join("; ", s.Sources.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {F(kv.Value, "0")} kN"))
        }).ToList();
        _gCol.ItemsSource = t.Columns.Select(c => new Row
        {
            A = c.Name, B = c.Section, C = F(c.Area), D = c.LiveFloors.ToString(), E = F(c.G, "0"), Fv = F(c.P, "0"), G = F(c.Psi, "0.00"), H = F(c.Ntc, "0"), I = F(c.Ntt, "0"),
            J = F(c.FeNtt, "0"), K = double.IsFinite(c.FeNtt) && c.Ntt > 1 ? F(c.FeNtt / c.Ntt, "0.00") : "–",
            L = string.Join("; ", c.Sources.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {F(kv.Value, "0")}"))
        }).ToList();
        string pileName = _adv.BestPile?.Name ?? "";
        _gIso.ItemsSource = _adv.Columns.Select(c => new Row
        {
            A = c.T.Name, B = F(c.T.Ntc, "0"), C = F(c.T.Ntt, "0"), D = F(c.R, "0"), E = double.IsFinite(c.B) ? $"{F(c.B, "0.0")} × {F(c.B, "0.0")}" : "–", Fv = F(c.H, "0.00"),
            G = _adv.BestPile != null ? $"{c.Piles} ({pileName})" : "–", H = c.Note.Length > 0 ? c.Note : c.EdgeText
        }).ToList();
        _gStrip.ItemsSource = _adv.Strips.Select(s => new Row
        {
            A = s.Line, B = s.Cols.ToString(), C = F(s.Length), D = F(s.SumNtc, "0"), E = F(s.B, "0.00"), Fv = F(s.Hf, "0.00"), G = $"{F(s.RibB, "0.00")} × {F(s.RibH, "0.00")}"
        }).ToList();
        _gPile.ItemsSource = _adv.Piles.Select(x => new Row
        {
            A = x.Name + (ReferenceEquals(x, _adv.BestPile) ? " ★" : ""), B = F(x.Length), C = F(x.Capacity, "0"),
            D = x.FromPress ? $"Lực ép đầu cọc {F(_adv.Press?.ForceT ?? 0, "0")} t / k = {F(_adv.Press?.K ?? 2, "0.0#")}" : x.FromBorehole ? "Hố khoan (SPT), mũi trong lớp " + x.TipLayer : "Nhập tay (Móng → thông số cọc)",
            E = x.Total.ToString(), Fv = F(x.Volume, "0.0"),
            G = string.Join(", ", x.PerColumn.GroupBy(n => n).OrderBy(g => g.Key).Select(g => $"{g.Count()} đài × {g.Key} cọc"))
        }).ToList();
        FillPits();
    }

    private void FillPits()
    {
        _gPit.ItemsSource = _adv.Pits.Select(x => new Row
        {
            A = x.Name, B = x.Names, C = x.EdgeText, D = F(x.Ntt, "0"), E = x.Ex > 0 || x.Ey > 0 ? $"{F(x.Ex, "0.00")} / {F(x.Ey, "0.00")}" : "0",
            Fv = F(x.Factor, "0.00"), G = F(x.Ndesign, "0"), H = x.Piles.ToString(), I = $"{x.Nx} × {x.Ny}", J = $"{F(x.CapLx, "0.00")} × {F(x.CapLy, "0.00")}", K = x.Note
        }).ToList();
    }

    private void DoApply(string choice)
    {
        if (_adv == null) return;
        if (choice == "Pile" && _adv.BestPile == null)
        {
            MessageBox.Show(this, "Chưa có [P] cọc: nhập sức chịu tải cọc (Móng → thông số cọc) hoặc hố khoan để tính.", "Việt Const", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Applied = choice;
        AppliedText = TakedownAdvisor.Apply(_p, _adv, choice, _chkGravity.IsChecked == true);
        DialogResult = true;
    }

    private void ExportWord()
    {
        if (_adv == null) return;
        var dlg = new SaveFileDialog { Filter = "Word (*.docx)|*.docx", FileName = "TruyenTai_GoiYMong.docx" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            TakedownReport.Build(_p, _adv).Save(dlg.FileName);
            MessageBox.Show(this, "Đã xuất " + dlg.FileName, "Việt Const", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, "Không xuất được: " + ex.Message, "Việt Const", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
