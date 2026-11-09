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
    private readonly CheckBox _chkPsi, _chkGravity;
    private readonly StackPanel _summary = new();
    private readonly DataGrid _gStory, _gCol, _gIso, _gStrip, _gPile;

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
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Tải từng sàn", Content = _gStory });
        tabs.Items.Add(new TabItem { Header = "Tải chân cột", Content = _gCol });
        tabs.Items.Add(new TabItem { Header = "Móng đơn / số cọc từng cột", Content = _gIso });
        tabs.Items.Add(new TabItem { Header = "Móng băng", Content = _gStrip });
        tabs.Items.Add(new TabItem { Header = "Móng cọc", Content = _gPile });
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
            _adv = TakedownAdvisor.Advise(_p, _r, new TakedownOptions { LiveReduction = _chkPsi.IsChecked == true });
        }
        catch (Exception ex) { MessageBox.Show(this, "Không truyền tải được: " + ex.Message, "Việt Const", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        finally { Cursor = null; }
        var t = _adv.T;
        _summary.Children.Clear();
        var ex1 = DialogUi.Label(_adv.Explanation, double.NaN, true);
        ex1.Foreground = Brushes.DarkGreen;
        _summary.Children.Add(ex1);
        _summary.Children.Add(DialogUi.Hint(_adv.SoilNote));
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
            G = _adv.BestPile != null ? $"{c.Piles} ({pileName})" : "–", H = c.Note
        }).ToList();
        _gStrip.ItemsSource = _adv.Strips.Select(s => new Row
        {
            A = s.Line, B = s.Cols.ToString(), C = F(s.Length), D = F(s.SumNtc, "0"), E = F(s.B, "0.00"), Fv = F(s.Hf, "0.00"), G = $"{F(s.RibB, "0.00")} × {F(s.RibH, "0.00")}"
        }).ToList();
        _gPile.ItemsSource = _adv.Piles.Select(x => new Row
        {
            A = x.Name + (ReferenceEquals(x, _adv.BestPile) ? " ★" : ""), B = F(x.Length), C = F(x.Capacity, "0"),
            D = x.FromBorehole ? "Hố khoan (SPT), mũi trong lớp " + x.TipLayer : "Nhập tay (Móng → thông số cọc)", E = x.Total.ToString(), Fv = F(x.Volume, "0.0"),
            G = string.Join(", ", x.PerColumn.GroupBy(n => n).OrderBy(g => g.Key).Select(g => $"{g.Count()} đài × {g.Key} cọc"))
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
