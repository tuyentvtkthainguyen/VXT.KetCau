using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using KetCauVXT.Mep;

namespace KetCauVXT.App;

/// <summary>Hộp thoại "Cài đặt điện nước": độ dốc theo hệ / dải Ø (kèm giới hạn min / max), cao độ mặc định theo nhóm thiết bị,
/// chiều dài cây ống / ống gen, cao độ tuyến, không gian trần, nắp thăm, quang treo, thông số ĐHKK. Sửa trên bản sao – bấm
/// "Áp dụng" mới ghi vào dự án (một bước hoàn tác) và đi lại tuyến.</summary>
public sealed partial class MepWindow
{
    /// <summary>Dòng bảng luật độ dốc (đơn vị % cho độ dốc).</summary>
    public sealed class SlopeRuleRow
    {
        public MepSystemKind System { get; set; } = MepSystemKind.ThoatNuocThai;
        public double DiaMin { get; set; }
        public double DiaMax { get; set; }
        public double SlopePct { get; set; } = 2;
        public double MinPct { get; set; }
        public double MaxPct { get; set; }
    }

    /// <summary>Dòng bảng cao độ theo nhóm.</summary>
    public sealed class CatHeightRow
    {
        public MepCategory Category { get; set; } = MepCategory.OCam;
        public double Height { get; set; } = 0.4;
    }

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static double P(string s, double def) =>
        double.TryParse((s ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : def;

    private void ShowSettings()
    {
        var cur = _p.Mep.Settings ??= new MepSettings();
        var hv = (_p.Mep.Hvac ??= new HvacModel()).Settings ??= new HvacSettings();
        var fields = new List<(string Label, TextBox Box, Action<MepSettings, HvacSettings, double> Set)>();
        var grid = new Grid { Margin = new Thickness(6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        void Add(string label, double value, Action<MepSettings, HvacSettings, double> set)
        {
            int row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var tl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 6, 2) };
            var tb = new TextBox { Text = F(value), Margin = new Thickness(0, 2, 0, 2) };
            Grid.SetRow(tl, row); Grid.SetRow(tb, row); Grid.SetColumn(tb, 1);
            grid.Children.Add(tl); grid.Children.Add(tb);
            fields.Add((label, tb, set));
        }
        Add("Cao độ tuyến ngang chôn tường (m)", cur.RunHeight, (s, _, v) => s.RunHeight = v);
        Add("Tuyến chiếu sáng: cách trần (m)", cur.CeilingDrop, (s, _, v) => s.CeilingDrop = v);
        Add("Thoát trong lớp sàn: cao độ so với sàn (m, nhập dương = dưới sàn)", -cur.DrainLevel, (s, _, v) => s.DrainLevel = -v);
        Add("Chiều dài cây ống nước (m)", cur.StockPipe, (s, _, v) => s.StockPipe = Math.Max(0.5, v));
        Add("Chiều dài cây ống gen (m)", cur.StockConduit, (s, _, v) => s.StockConduit = Math.Max(0.5, v));
        Add("Chiều cao tầng mặc định (m)", cur.DefaultStoryHeight, (s, _, v) => s.DefaultStoryHeight = Math.Max(2, v));
        Add("Độ dốc mặc định Ø ≤ 50 (%)", cur.DrainSlopeSmall * 100, (s, _, v) => s.DrainSlopeSmall = v / 100);
        Add("Độ dốc mặc định Ø60…Ø90 (%)", cur.DrainSlopeMedium * 100, (s, _, v) => s.DrainSlopeMedium = v / 100);
        Add("Độ dốc mặc định Ø ≥ 110 (%)", cur.DrainSlopeLarge * 100, (s, _, v) => s.DrainSlopeLarge = v / 100);
        Add("Độ dốc lớn nhất khuyến nghị (%)", cur.DrainSlopeMax * 100, (s, _, v) => s.DrainSlopeMax = v / 100);
        Add("Khe đáy sàn – đỉnh ống treo (m)", cur.DrainHangerGap, (s, _, v) => s.DrainHangerGap = v);
        Add("Chiều sâu không gian trần (m)", cur.CeilingVoid, (s, _, v) => s.CeilingVoid = v);
        Add("Khoảng cách ống kiểm tra / nắp thăm (m)", cur.CleanoutSpacing, (s, _, v) => s.CleanoutSpacing = Math.Max(1, v));
        Add("Khoảng cách quang treo ống (m, 0 = theo Ø)", cur.HangerSpacing, (s, _, v) => s.HangerSpacing = v);
        Add("ĐHKK – vận tốc gió ống chính (m/s)", hv.VelocityMain, (_, h, v) => h.VelocityMain = Math.Max(1, v));
        Add("ĐHKK – vận tốc gió ống nhánh (m/s)", hv.VelocityBranch, (_, h, v) => h.VelocityBranch = Math.Max(1, v));
        Add("ĐHKK – tỷ lệ cạnh ống chữ nhật lớn nhất", hv.MaxAspect, (_, h, v) => h.MaxAspect = Math.Max(1, v));
        Add("ĐHKK – khe đáy sàn – đỉnh ống gió (m)", hv.DuctGap, (_, h, v) => h.DuctGap = v);
        Add("ĐHKK – khoảng cách giá treo ống gió (m)", hv.DuctHangerSpacing, (_, h, v) => h.DuctHangerSpacing = Math.Max(0.5, v));
        Add("ĐHKK – độ dốc ống nước ngưng (%, ≥ 1)", hv.CondensateSlope * 100, (_, h, v) => h.CondensateSlope = Math.Max(0.01, v / 100));
        Add("ĐHKK – Ø ống nước ngưng (mm)", hv.CondensateDia, (_, h, v) => h.CondensateDia = Math.Max(16, v));
        Add("ĐHKK – dàn nóng giá treo: cao độ tâm so với sàn (m)", hv.OutdoorBracketHeight, (_, h, v) => h.OutdoorBracketHeight = Math.Max(0.3, v));
        Add("ĐHKK – dò tuyến ống gió: bước lưới (m)", hv.RouteStep, (_, h, v) => h.RouteStep = Math.Max(0.02, v));
        Add("ĐHKK – dò tuyến: phạt mỗi lần đổi hướng (m)", hv.BendPenalty, (_, h, v) => h.BendPenalty = v);
        Add("ĐHKK – dò tuyến: phạt mỗi lần cắt dầm (m)", hv.BeamPenalty, (_, h, v) => h.BeamPenalty = v);
        Add("ĐHKK – dò tuyến: phạt mỗi lần xuyên tường (m)", hv.WallPenalty, (_, h, v) => h.WallPenalty = v);
        var pn = cur.Panel ??= new PanelOptions(); var wt = cur.Water ??= new WaterTankOptions();
        Add("Tủ điện – số điểm chiếu sáng tối đa / mạch", pn.MaxLightingPoints, (s, _, v) => s.Panel.MaxLightingPoints = Math.Max(1, (int)Math.Round(v)));
        Add("Tủ điện – số ổ cắm tối đa / mạch", pn.MaxSocketPoints, (s, _, v) => s.Panel.MaxSocketPoints = Math.Max(1, (int)Math.Round(v)));
        Add("Tủ điện – Kc chiếu sáng", pn.KcLighting, (s, _, v) => s.Panel.KcLighting = Math.Min(1, v));
        Add("Tủ điện – Kc ổ cắm", pn.KcSocket, (s, _, v) => s.Panel.KcSocket = Math.Min(1, v));
        Add("Tủ điện – Kc điều hoà", pn.KcAc, (s, _, v) => s.Panel.KcAc = Math.Min(1, v));
        Add("Tủ điện – Kc bình nóng lạnh", pn.KcHeater, (s, _, v) => s.Panel.KcHeater = Math.Min(1, v));
        Add("Tủ điện – tự chuyển 3 pha khi Ptt vượt (kW)", pn.ThreePhaseAboveKw, (s, _, v) => s.Panel.ThreePhaseAboveKw = v);
        Add("Cấp nước – số người dùng (0 = không lập bảng bể / bơm)", wt.Occupants, (s, _, v) => s.Water.Occupants = (int)Math.Round(v));
        Add("Cấp nước – tiêu chuẩn (l/người·ngày)", wt.LitersPerPerson, (s, _, v) => s.Water.LitersPerPerson = v);
        Add("Cấp nước – bể mái dự trữ (ngày)", wt.RoofDays, (s, _, v) => s.Water.RoofDays = v);
        Add("Cấp nước – bể ngầm dự trữ (ngày)", wt.GroundDays, (s, _, v) => s.Water.GroundDays = v);
        Add("Cấp nước – dự trữ chữa cháy bể ngầm (m³)", wt.FireReserve, (s, _, v) => s.Water.FireReserve = v);
        Add("Cấp nước – số giờ bơm / ngày", wt.PumpHours, (s, _, v) => s.Water.PumpHours = Math.Max(0.5, v));
        Add("Cấp nước – mực nước thấp bể ngầm dưới ±0,00 (m, nhập dương)", -wt.GroundTankLevel, (s, _, v) => s.Water.GroundTankLevel = -v);
        Add("Cấp nước – chiều dài ống ngang thêm (m)", wt.HorizontalLength, (s, _, v) => s.Water.HorizontalLength = v);
        ComboBox Combo(string label, string[] items, string value)
        {
            int row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var tl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 6, 2) };
            var cb = new ComboBox { ItemsSource = items, SelectedItem = items.Contains(value) ? value : items[0], Margin = new Thickness(0, 2, 0, 2), MinWidth = 150 };
            Grid.SetRow(tl, row); Grid.SetRow(cb, row); Grid.SetColumn(cb, 1);
            grid.Children.Add(tl); grid.Children.Add(cb);
            return cb;
        }
        var cbMount = Combo("ĐHKK – cách lắp dàn nóng mặc định", new[] { HvacMount.Floor, HvacMount.Bracket }, hv.OutdoorMount);
        var cbCool = Combo("ĐHKK – phương pháp tải lạnh", new[] { HvacMount.QuickMethod, HvacMount.DetailMethod }, hv.CoolingMethod);
        var cbPhase = Combo("Tủ điện – số pha", new[] { PanelOptions.Auto, PanelOptions.One, PanelOptions.Three }, pn.Phases);
        var below = new CheckBox { Content = "Ống thoát đi DƯỚI sàn (treo dưới đáy sàn) tới ống đứng", IsChecked = cur.DrainBelowSlab, Margin = new Thickness(6, 4, 6, 4) };

        var rules = new ObservableCollection<SlopeRuleRow>((cur.SlopeRules ?? new()).Where(r => r != null).Select(r => new SlopeRuleRow
        {
            System = r.System, DiaMin = r.DiaMin, DiaMax = r.DiaMax, SlopePct = r.Slope * 100, MinPct = r.SlopeMin * 100, MaxPct = r.SlopeMax * 100
        }));
        var gr = new DataGrid { ItemsSource = rules, AutoGenerateColumns = false, CanUserAddRows = true, CanUserDeleteRows = true, Height = 170, Margin = new Thickness(6) };
        gr.Columns.Add(new DataGridComboBoxColumn { Header = "Hệ thống", ItemsSource = MepSystems.All.Where(MepSystems.IsDrain).ToList(), SelectedItemBinding = new Binding(nameof(SlopeRuleRow.System)), Width = 150 });
        void NumCol(DataGrid g, string h, string path) => g.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(path) { ConverterCulture = CultureInfo.InvariantCulture }, Width = 80 });
        NumCol(gr, "Ø từ (mm)", nameof(SlopeRuleRow.DiaMin)); NumCol(gr, "Ø đến (mm)", nameof(SlopeRuleRow.DiaMax)); NumCol(gr, "Độ dốc (%)", nameof(SlopeRuleRow.SlopePct));
        NumCol(gr, "Min (%)", nameof(SlopeRuleRow.MinPct)); NumCol(gr, "Max (%)", nameof(SlopeRuleRow.MaxPct));

        var heights = new ObservableCollection<CatHeightRow>((cur.CategoryHeights ?? new()).Where(c => c != null).Select(c => new CatHeightRow { Category = c.Category, Height = c.Height }));
        var gh = new DataGrid { ItemsSource = heights, AutoGenerateColumns = false, CanUserAddRows = true, CanUserDeleteRows = true, Height = 140, Margin = new Thickness(6) };
        gh.Columns.Add(new DataGridComboBoxColumn { Header = "Nhóm thiết bị", ItemsSource = Enum.GetValues<MepCategory>().ToList(), SelectedItemBinding = new Binding(nameof(CatHeightRow.Category)), Width = 160 });
        NumCol(gh, "Cao độ (m)", nameof(CatHeightRow.Height));

        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Thông số chung", FontWeight = FontWeights.Bold, Margin = new Thickness(6, 6, 6, 0) });
        sp.Children.Add(grid);
        sp.Children.Add(below);
        sp.Children.Add(new TextBlock { Text = "Độ dốc theo hệ thống + dải Ø (ghi đè độ dốc mặc định; Ø đến = 0 → mọi Ø; Min / Max = 0 → không kẹp; kẹp cả độ dốc nhập tay)", TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.Bold, Margin = new Thickness(6, 8, 6, 0) });
        sp.Children.Add(gr);
        sp.Children.Add(new TextBlock { Text = "Cao độ lắp mặc định theo nhóm thiết bị (m so với sàn; áp dụng khi đặt mới, thiết bị trên trần vẫn theo trần)", TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.Bold, Margin = new Thickness(6, 8, 6, 0) });
        sp.Children.Add(gh);
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(6) };
        var ok = new Button { Content = "Áp dụng", IsDefault = true, Padding = new Thickness(12, 3, 12, 3), Margin = new Thickness(4) };
        var cancel = new Button { Content = "Huỷ", IsCancel = true, Padding = new Thickness(12, 3, 12, 3), Margin = new Thickness(4) };
        bar.Children.Add(ok); bar.Children.Add(cancel);
        sp.Children.Add(bar);
        var dlg = new Window
        {
            Title = "Cài đặt điện nước", Owner = this, Width = 620, Height = 760, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        ok.Click += (_, _) => dlg.DialogResult = true;
        if (dlg.ShowDialog() != true) return;
        gr.CommitEdit(); gh.CommitEdit();

        Edit(() =>
        {
            var s = _p.Mep.Settings; var h = _p.Mep.Hvac.Settings;
            foreach (var (label, box, set) in fields) set(s, h, P(box.Text, 0));
            s.DrainBelowSlab = below.IsChecked == true;
            h.OutdoorMount = cbMount.SelectedItem as string ?? HvacMount.Floor;
            h.CoolingMethod = cbCool.SelectedItem as string ?? HvacMount.QuickMethod;
            s.Panel.Phases = cbPhase.SelectedItem as string ?? PanelOptions.Auto;
            s.SlopeRules = RulesFrom(rules);
            s.CategoryHeights = heights.Where(c => c != null && c.Height >= 0).GroupBy(c => c.Category).Select(g => new MepCategoryHeight { Category = g.Key, Height = g.Last().Height }).ToList();
            foreach (var r in _p.Mep.Runs) MepRouting.Route(_p, r);
            MepRouting.RebuildAll(_p);
            SetStatus($"Đã áp dụng cài đặt điện nước: {s.SlopeRules.Count} luật độ dốc, {s.CategoryHeights.Count} cao độ theo nhóm – đã đi lại tuyến.");
        });
    }

    /// <summary>Đổi bảng luật (%) thành MepSlopeRule (bỏ dòng độ dốc ≤ 0).</summary>
    public static List<MepSlopeRule> RulesFrom(IEnumerable<SlopeRuleRow> rows) => rows.Where(r => r != null && r.SlopePct > 0).Select(r => new MepSlopeRule
    {
        System = r.System, DiaMin = Math.Max(0, r.DiaMin), DiaMax = Math.Max(0, r.DiaMax), Slope = r.SlopePct / 100,
        SlopeMin = Math.Max(0, r.MinPct) / 100, SlopeMax = Math.Max(0, r.MaxPct) / 100
    }).ToList();
}
