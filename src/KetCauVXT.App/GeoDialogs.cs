using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Analysis;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Chọn lớp đất theo bảng đất tham khảo (SoilPresets): khoảng giá trị điển hình, giá trị đại diện, tương quan SPT.</summary>
internal sealed class SoilPresetWindow : Window
{
    public SoilPreset Selected { get; private set; }
    /// <summary>true = thêm lớp mới cuối hố khoan; false = điền vào lớp đang chọn</summary>
    public bool AddNew { get; private set; }

    private sealed class Row
    {
        public SoilPreset P { get; init; }
        public string Name => P.Name;
        public string Gamma => P.GammaText;
        public string Phi => P.PhiText;
        public string C => P.CText;
        public string E => P.EText;
        public string N => P.NText;
        public string R0 => P.R0Text;
        public string Rep => $"γ {F(P.Gamma)}, φ {F(P.Phi)}°, c {F(P.C)}, E {F(P.E)}, N {F(P.N)}";
        public string Note => P.Note;
        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public SoilPresetWindow(SoilLayer current)
    {
        DialogUi.Setup(this, "Chọn theo đất tham khảo", 1080, 560);
        var root = new DockPanel { Margin = new Thickness(10) };
        var head = new StackPanel();
        var warn = DialogUi.Label("THAM KHẢO – PHẢI THEO KẾT QUẢ KHẢO SÁT THỰC TẾ. Giá trị điển hình để chọn sơ bộ phương án móng; khi có báo cáo khảo sát, nhập đúng chỉ tiêu của từng lớp.", double.NaN, true);
        warn.Foreground = Brushes.DarkRed;
        head.Children.Add(warn);
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);

        var g = DialogUi.NewGrid();
        g.SelectionMode = DataGridSelectionMode.Single;
        g.IsReadOnly = true;
        g.Columns.Add(DialogUi.Text("Loại đất", "Name", true, 230));
        g.Columns.Add(DialogUi.Text("γ (kN/m³)", "Gamma", true));
        g.Columns.Add(DialogUi.Text("φ (°)", "Phi", true));
        g.Columns.Add(DialogUi.Text("c (kPa)", "C", true));
        g.Columns.Add(DialogUi.Text("E (MPa)", "E", true));
        g.Columns.Add(DialogUi.Text("N (SPT)", "N", true));
        g.Columns.Add(DialogUi.Text("R0 (kPa)", "R0", true));
        g.Columns.Add(DialogUi.Text("Giá trị đại diện điền vào", "Rep", true, 230));
        g.Columns.Add(DialogUi.Text("Nhận xét / phương án móng", "Note", true, 300));
        var rows = SoilPresets.All.Select(p => new Row { P = p }).ToList();
        g.ItemsSource = rows;
        if (current != null) g.SelectedItem = rows.FirstOrDefault(r => ReferenceEquals(r.P, SoilPresets.Nearest(current)));
        if (g.SelectedItem == null) g.SelectedIndex = 3;

        // tương quan SPT
        var tN = new TextBox { Width = 60, Text = current != null ? current.N.ToString("0.#", CultureInfo.InvariantCulture) : "10" };
        var corr = DialogUi.Hint("");
        void Upd()
        {
            double n = DialogUi.ParseNum(tN.Text, 0);
            corr.Text = "Đất rời: φ ≈ √(20·N) + 15 = " + SoilPresets.PhiFromN(n).ToString("0.#", CultureInfo.InvariantCulture) + "° (Ohsaki);  E ≈ (0,7…1,0)·N = "
                        + SoilPresets.EFromN(n).Min.ToString("0.#", CultureInfo.InvariantCulture) + "…" + SoilPresets.EFromN(n).Max.ToString("0.#", CultureInfo.InvariantCulture)
                        + " MPa.   Đất dính: cu ≈ 6,25·N = " + SoilPresets.CuFromN(n).ToString("0.#", CultureInfo.InvariantCulture) + " kPa (TCVN 10304:2014 G.3.2).";
        }
        tN.TextChanged += (s, e) => Upd();
        Upd();
        var bottom = new StackPanel();
        bottom.Children.Add(DialogUi.Row(DialogUi.Label("Tương quan SPT – N ="), tN));
        bottom.Children.Add(corr);
        var bFill = DialogUi.Btn(current != null ? "Điền vào lớp đang chọn" : "Thêm lớp", (s, e) => { Selected = (g.SelectedItem as Row)?.P; AddNew = current == null; DialogResult = Selected != null; }, true);
        bFill.IsDefault = true;
        var bAdd = DialogUi.Btn("Thêm lớp mới", (s, e) => { Selected = (g.SelectedItem as Row)?.P; AddNew = true; DialogResult = Selected != null; });
        var bCancel = DialogUi.Btn("Huỷ", (s, e) => DialogResult = false);
        bCancel.IsCancel = true;
        var bs = current != null ? DialogUi.Row(bFill, bAdd, bCancel) : DialogUi.Row(bFill, bCancel);
        bs.HorizontalAlignment = HorizontalAlignment.Right;
        bottom.Children.Add(bs);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        g.MouseDoubleClick += (s, e) => { if (g.SelectedItem is Row r) { Selected = r.P; AddNew = current == null; DialogResult = true; } };
        root.Children.Add(g);
        Content = root;
    }
}

/// <summary>Cửa sổ "Gợi ý phương án móng": bảng FoundationAdvisor (khả thi, lý do, số liệu chính), giải thích, bảng sức chịu tải cọc / lún của phương án chọn,
/// nút "Áp dụng phương án này".</summary>
internal sealed class GeoAdvisorWindow : Window
{
    private readonly Project _p;
    private readonly FoundationAdvice _adv;
    private readonly DataGrid _g, _gPile;
    private readonly TextBlock _detail;

    public bool Applied { get; private set; }
    public string AppliedName { get; private set; } = "";

    private sealed class OptRow
    {
        public FoundationOption O { get; init; }
        public string Name => O.Name;
        public string Ok => O.Feasible ? "Khả thi" : "Không";
        public string Rec => O.Recommended ? "★ Đề xuất" : "";
        public string Reason => O.Reason;
        public string Details => string.Join("\n", O.Details);
    }

    private sealed class PileRow
    {
        public PileLayerRow R { get; init; }
        private static string F(double v, string f) => double.IsFinite(v) ? v.ToString(f, CultureInfo.InvariantCulture) : "-";
        public string Depth => $"{F(R.From, "0.0#")}–{F(R.To, "0.0#")}";
        public string Soil => R.Soil;
        public string Kind => R.IsTip ? "mũi" : R.Cohesive ? "dính" : "rời";
        public string N => F(R.N, "0.#");
        public string Cu => R.Cohesive ? F(R.Cu, "0.#") : "–";
        public string Alpha => R.Cohesive && !R.IsTip ? F(R.Alpha, "0.###") : "–";
        public string Fv => F(R.F, "0.0");
        public string Q => F(R.Q, "0.0");
    }

    public GeoAdvisorWindow(Project p, AnalysisResults r, Borehole bh)
    {
        _p = p;
        DialogUi.Setup(this, "Gợi ý phương án móng" + (bh != null ? " – hố khoan " + bh.Name : " – chưa có hố khoan"), 1120, 760);
        _adv = FoundationAdvisor.Recommend(p, r, bh);

        var root = new DockPanel { Margin = new Thickness(10) };
        var head = new StackPanel();
        var expl = DialogUi.Label(_adv.Explanation, double.NaN, true);
        expl.Foreground = Brushes.DarkGreen;
        head.Children.Add(expl);
        foreach (var n in _adv.Notes.Where(n => n != _adv.Explanation)) head.Children.Add(DialogUi.Hint("• " + n));
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);

        _g = DialogUi.NewGrid();
        _g.SelectionMode = DataGridSelectionMode.Single;
        _g.IsReadOnly = true;
        _g.Height = 230;
        var wrap = new Style(typeof(TextBlock));
        wrap.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        DataGridTextColumn Col(string h, string path, double w)
        {
            var c = DialogUi.Text(h, path, true, w);
            c.ElementStyle = wrap;
            return c;
        }
        _g.Columns.Add(Col("Phương án", "Name", 150));
        _g.Columns.Add(Col("Khả thi", "Ok", 70));
        _g.Columns.Add(Col("", "Rec", 80));
        _g.Columns.Add(Col("Lý do", "Reason", 380));
        _g.Columns.Add(Col("Số liệu chính", "Details", 380));
        var rows = _adv.Options.Select(o => new OptRow { O = o }).ToList();
        _g.ItemsSource = rows;
        _g.SelectionChanged += (s, e) => ShowSelected();
        DockPanel.SetDock(_g, Dock.Top);

        var bApply = DialogUi.Btn("Áp dụng phương án này", (s, e) => Apply(), true);
        var bClose = DialogUi.Btn("Đóng", (s, e) => Close());
        bClose.IsCancel = true;
        var bs = DialogUi.Row(bApply, bClose);
        bs.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(bs, Dock.Bottom);
        root.Children.Add(bs);
        root.Children.Add(_g);

        var lower = new DockPanel();
        _detail = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 4) };
        DockPanel.SetDock(_detail, Dock.Top);
        lower.Children.Add(_detail);
        _gPile = DialogUi.NewGrid();
        _gPile.IsReadOnly = true;
        _gPile.Columns.Add(DialogUi.Text("Độ sâu (m)", "Depth", true, 90));
        _gPile.Columns.Add(DialogUi.Text("Lớp đất", "Soil", true, 260));
        _gPile.Columns.Add(DialogUi.Text("Loại", "Kind", true, 50));
        _gPile.Columns.Add(DialogUi.Text("N", "N", true, 50));
        _gPile.Columns.Add(DialogUi.Text("cu (kPa)", "Cu", true, 70));
        _gPile.Columns.Add(DialogUi.Text("αp", "Alpha", true, 60));
        _gPile.Columns.Add(DialogUi.Text("fs / fc / qb (kPa)", "Fv", true, 110));
        _gPile.Columns.Add(DialogUi.Text("Đóng góp (kN)", "Q", true, 100));
        lower.Children.Add(_gPile);
        root.Children.Add(lower);
        Content = root;

        _g.SelectedItem = rows.FirstOrDefault(x => x.O.Recommended) ?? rows.FirstOrDefault();
        ShowSelected();
    }

    private FoundationOption Sel => (_g.SelectedItem as OptRow)?.O;

    private void ShowSelected()
    {
        var o = Sel;
        if (o == null) { _detail.Text = ""; _gPile.ItemsSource = null; return; }
        var lines = new List<string> { $"{o.Name}: {(o.Feasible ? "khả thi" : "không khả thi")} – {o.Reason}" };
        if (o.Pile != null)
        {
            lines.Add("Sức chịu tải cọc theo SPT (TCVN 10304:2014 Phụ lục G.3.2): Rc,u = qb·Ab + u·Σ(fc,i·lc,i + fs,i·ls,i); fs = 10·N/3, cu = 6,25·N, fc = αp·cu; qb = "
                      + (o.Bored ? "150" : "300") + "·Np (đất rời) / 9·cu (đất dính).");
            lines.AddRange(o.Pile.Notes);
            _gPile.ItemsSource = o.Pile.Rows.Select(x => new PileRow { R = x }).ToList();
            _gPile.Visibility = Visibility.Visible;
        }
        else
        {
            _gPile.ItemsSource = null;
            _gPile.Visibility = Visibility.Collapsed;
            if (o.SettlementInfo != null)
            {
                lines.Add("Độ lún (TCVN 9362:2012, cộng lún từng lớp, β = 0,8):");
                lines.AddRange(o.SettlementInfo.Notes);
            }
        }
        _detail.Text = string.Join("\n", lines);
    }

    private void Apply()
    {
        var o = Sel;
        if (o == null) return;
        if (!o.Feasible && MessageBox.Show(this, $"Phương án '{o.Name}' được đánh giá không khả thi:\n{o.Reason}\n\nVẫn áp dụng?", Title,
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        o.ApplyTo(_p);
        Applied = true;
        AppliedName = o.Name;
        Close();
    }
}
