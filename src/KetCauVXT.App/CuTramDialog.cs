using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>[CừTràm] Hộp thoại "Cừ tràm…" (không chặn): thông số cừ tràm (chiều dài, Ø gốc / ngọn, mật độ, bê tông lót, cát đệm, MNN thấp nhất,
/// FS, η, αs, chỉ tiêu sau gia cố, phương pháp), phạm vi áp dụng (cả công trình / riêng chân cột đang chọn); kết quả tức thời của hai phương pháp
/// cho móng đang chọn (hoặc móng thử 1,2 × 1,2 m) + bảng kiểm tra tô màu + cảnh báo; "Áp dụng" ghi thông số (có hoàn tác) rồi thiết kế lại.</summary>
internal sealed class CuTramDialog : Window
{
    private readonly Project _p;
    private readonly Func<DesignSummary> _design;
    private readonly int _node;
    private readonly Func<CuTramSettings, int, bool?, Task> _apply;
    private readonly CheckBox _chkAll;
    private readonly ComboBox _cbNode = new() { Width = 200, ItemsSource = new[] { "Theo thiết lập công trình", "Có gia cố cừ tràm", "Không gia cố" } };
    private readonly ComboBox _cbMethod = new() { Width = 330 };
    private readonly TextBox _tLen, _tD1, _tD2, _tDen, _tLean, _tSand, _tGt, _tRg, _tFs, _tEta, _tAs, _tMnn, _tPhi, _tC, _tE0, _tSgh, _tNmax;
    private readonly TextBlock _sum = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
    private readonly TextBlock _beh = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 4) };
    private readonly TextBlock _warn = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly DataGrid _grid = FoundationSafetyView.NewGrid();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private bool _working;

    /// <summary>Kết quả tính thử gần nhất</summary>
    public CuTramResult Last { get; private set; }
    public string Summary => _sum.Text;

    public CuTramDialog(Project p, Func<DesignSummary> design, int node, Func<CuTramSettings, int, bool?, Task> apply)
    {
        _p = p; _design = design; _node = node; _apply = apply;
        var cs = p.Foundation?.CuTram ?? new CuTramSettings();
        DialogUi.Setup(this, "Gia cố nền bằng cừ tràm" + (node != 0 ? $" – chân cột nút {node}" : ""), 1220, 760);
        TextBox Box(double v, string fmt = "0.###") { var t = new TextBox { Width = 80, Text = DialogUi.F(v, fmt) }; t.TextChanged += (s, e) => Kick(); return t; }
        _tLen = Box(cs.Length, "0.##"); _tD1 = Box(cs.TopDiameter * 100, "0.#"); _tD2 = Box(cs.TipDiameter * 100, "0.#"); _tDen = Box(cs.Density, "0.#");
        _tLean = Box(cs.LeanThickness * 1000, "0"); _tSand = Box(cs.SandCushion * 1000, "0"); _tGt = Box(cs.TimberUnitWeight, "0.#"); _tRg = Box(cs.TimberStrength / 1000, "0.#");
        _tFs = Box(cs.SafetyFactor, "0.##"); _tEta = Box(cs.GroupEfficiency, "0.###"); _tAs = Box(cs.SoilShare, "0.##"); _tMnn = Box(cs.MinWaterDepth, "0.##");
        _tPhi = Box(cs.ImprovedPhi, "0.#"); _tC = Box(cs.ImprovedC, "0.#"); _tE0 = Box(cs.DefaultVoidRatio, "0.###"); _tSgh = Box(cs.SettlementLimit * 100, "0.#"); _tNmax = Box(cs.MaxColumnLoad, "0");
        _cbMethod.ItemsSource = new[] { CuTramMethod.Conservative, CuTramMethod.PileFriction, CuTramMethod.EquivalentSoil }.Select(CuTramSettings.MethodName).ToList();
        _cbMethod.SelectedIndex = (int)cs.Method;
        _cbMethod.SelectionChanged += (s, e) => Kick();
        _chkAll = new CheckBox { Content = "Gia cố cừ tràm cho cả công trình (móng đơn + móng băng)", IsChecked = cs.Enabled, Margin = new Thickness(0, 2, 0, 2) };
        var ovr = cs.Overrides?.FirstOrDefault(o => o != null && o.NodeId == node);
        _cbNode.SelectedIndex = ovr == null ? 0 : ovr.Enabled ? 1 : 2;
        _cbNode.IsEnabled = node != 0;

        var left = new StackPanel { Width = 430 };
        left.Children.Add(DialogUi.Label("Phạm vi áp dụng", double.NaN, true));
        left.Children.Add(_chkAll);
        left.Children.Add(Line(node != 0 ? $"Chân cột đang chọn (nút {node})" : "Chân cột đang chọn (chưa chọn)", _cbNode));
        left.Children.Add(DialogUi.Label("Cây cừ", double.NaN, true));
        left.Children.Add(Line("Chiều dài cừ Lc (m, 3–5)", _tLen));
        left.Children.Add(Line("Ø gốc D1 (cm, 8–10)", _tD1));
        left.Children.Add(Line("Ø ngọn D2 (cm)", _tD2));
        left.Children.Add(Line("Mật độ (cây/m², 16–25)", _tDen));
        left.Children.Add(Line("Bê tông lót (mm) – đầu cừ dưới lớp lót", _tLean));
        left.Children.Add(Line("Cát đệm phủ đầu cừ (mm, 0 = không)", _tSand));
        left.Children.Add(Line("γ gỗ tràm bão hoà (kN/m³)", _tGt));
        left.Children.Add(Line("Cường độ nén gỗ Rgỗ (MPa)", _tRg));
        left.Children.Add(DialogUi.Label("Tính toán", double.NaN, true));
        left.Children.Add(Line("Phương pháp chọn kích thước móng", _cbMethod));
        left.Children.Add(Line("FS sức chịu tải 1 cây (2–2,5)", _tFs));
        left.Children.Add(Line("Hệ số nhóm η (0 = Converse–Labarre)", _tEta));
        left.Children.Add(Line("αs kể đất giữa cừ (0…1)", _tAs));
        left.Children.Add(Line("φ1 sau gia cố (°, 0 = tự ước lượng)", _tPhi));
        left.Children.Add(Line("c1 sau gia cố (kPa, 0 = c0·e0/e1)", _tC));
        left.Children.Add(Line("e0 khi lớp đất chưa có số liệu", _tE0));
        left.Children.Add(Line("MNN thấp nhất (m từ mặt đất, −1 = hố khoan)", _tMnn));
        left.Children.Add(Line("Độ lún giới hạn Sgh (cm)", _tSgh));
        left.Children.Add(Line("Ntc lớn nhất nên dùng cừ tràm (kN)", _tNmax));
        left.Children.Add(DialogUi.Hint("Giá trị mặc định là kinh nghiệm thực hành – người thiết kế kiểm tra. Đầu cừ PHẢI nằm dưới mực nước ngầm thấp nhất (mùa khô); "
                                        + "cừ tràm hiệu quả trong bùn / sét dẻo chảy (N < 5)."));

        var bApply = DialogUi.Btn("Áp dụng + thiết kế lại", async (s, e) => await ApplyAsync(), true);
        var bClose = DialogUi.Btn("Đóng", (s, e) => Close());
        bClose.IsCancel = true;
        var br = DialogUi.Row(bApply, bClose);
        br.HorizontalAlignment = HorizontalAlignment.Right;
        br.Margin = new Thickness(0, 8, 0, 0);

        var right = new DockPanel { Margin = new Thickness(10, 0, 0, 0) };
        var head = new StackPanel();
        head.Children.Add(_sum);
        head.Children.Add(new ScrollViewer { Content = _beh, MaxHeight = 210, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        DockPanel.SetDock(head, Dock.Top);
        right.Children.Add(head);
        var bottom = new ScrollViewer { Content = _warn, MaxHeight = 130, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        DockPanel.SetDock(bottom, Dock.Bottom);
        right.Children.Add(bottom);
        right.Children.Add(_grid);

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(br, Dock.Bottom);
        root.Children.Add(br);
        var sl = new ScrollViewer { Content = left, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        DockPanel.SetDock(sl, Dock.Left);
        root.Children.Add(sl);
        root.Children.Add(right);
        Content = root;
        _timer.Tick += (s, e) => { _timer.Stop(); Evaluate(); };
        Loaded += (s, e) => Evaluate();
    }

    private static DockPanel Line(string label, FrameworkElement ctl)
    {
        var d = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
        d.Children.Add(DialogUi.Label(label, 260));
        ctl.HorizontalAlignment = HorizontalAlignment.Left;
        d.Children.Add(ctl);
        return d;
    }

    private void Kick() { if (!IsLoaded) return; _timer.Stop(); _timer.Start(); }

    /// <summary>Thông số theo các ô nhập (bản sao, giữ ghi đè chân cột hiện có).</summary>
    public CuTramSettings Build()
    {
        var cs = (_p.Foundation?.CuTram ?? new CuTramSettings()).Clone();
        double G(TextBox t, double def, double min, double max) { double v = DialogUi.ParseNum(t.Text, def); return double.IsNaN(v) ? def : Math.Clamp(v, min, max); }
        cs.Enabled = _chkAll.IsChecked == true;
        cs.Length = G(_tLen, 4, 1, 12);
        cs.TopDiameter = G(_tD1, 8, 2, 30) / 100;
        cs.TipDiameter = Math.Min(G(_tD2, 4.5, 1, 30) / 100, cs.TopDiameter);
        cs.Density = G(_tDen, 25, 1, 100);
        cs.LeanThickness = G(_tLean, 100, 0, 500) / 1000;
        cs.SandCushion = G(_tSand, 100, 0, 500) / 1000;
        cs.TimberUnitWeight = G(_tGt, 10, 0, 20);
        cs.TimberStrength = G(_tRg, 10, 0, 60) * 1000;
        cs.SafetyFactor = G(_tFs, 2, 1, 5);
        cs.GroupEfficiency = G(_tEta, 0, 0, 1);
        cs.SoilShare = G(_tAs, 0.8, 0, 1);
        cs.ImprovedPhi = G(_tPhi, 0, 0, 45);
        cs.ImprovedC = G(_tC, 0, 0, 500);
        cs.DefaultVoidRatio = G(_tE0, 1.2, 0.3, 5);
        double mnn = DialogUi.ParseNum(_tMnn.Text, -1);
        cs.MinWaterDepth = double.IsNaN(mnn) || mnn < 0 ? -1 : Math.Min(mnn, 50);
        cs.SettlementLimit = G(_tSgh, 8, 1, 50) / 100;
        cs.MaxColumnLoad = G(_tNmax, 600, 10, 1e5);
        cs.Method = (CuTramMethod)Math.Clamp(_cbMethod.SelectedIndex, 0, 2);
        return cs;
    }

    /// <summary>Lựa chọn riêng chân cột: null = theo công trình.</summary>
    public bool? NodeChoice => _cbNode.SelectedIndex switch { 1 => true, 2 => false, _ => null };

    /// <summary>Tính thử cừ tràm cho móng đang chọn (hoặc móng thử 1,2 × 1,2 m) và cập nhật bảng.</summary>
    public void Evaluate()
    {
        try
        {
            var cs = Build();
            var ds = _design?.Invoke();
            string key = "M:" + _node;
            var f = _node == 0 ? ds?.Footings.FirstOrDefault(x => x.CuTram != null) ?? ds?.Footings.FirstOrDefault()
                               : ds?.Footings.FirstOrDefault(x => x.NodeId == _node || x.RebarKeys.Contains(key));
            double x = f?.X ?? 0, y = f?.Y ?? 0, L = f?.L > 0 ? f.L : 1.2, B = f?.B > 0 ? f.B : 1.2;
            double ptb = f?.Ptb ?? double.NaN, pmax = f?.Pmax ?? double.NaN, ntc = f?.Ntc ?? double.NaN;
            if (f == null && _node != 0) { var n = _p.Nodes.FirstOrDefault(q => q.Id == _node); if (n != null) { x = n.X; y = n.Y; } }
            var soil = GeotechCalc.SoilFor(_p, x, y, out _);
            var bh = CuTramCalc.BoreholeFor(_p, x, y, out bool assumed);
            var r = CuTramCalc.Evaluate(cs, soil, bh, assumed, L, B, soil.Depth, ptb, pmax, ntc);
            Last = r;
            string where = f != null ? $"móng {f.Name} {L:0.00}×{B:0.00} m (ptb = {ptb:0.0} kN/m²)" : $"móng thử {L:0.00}×{B:0.00} m";
            _sum.Text = $"{where}, hố khoan {r.Borehole}{(assumed ? " (GIẢ ĐỊNH)" : "")}: {r.Count} cây; PP(1) [p]1 = {r.P1:0.0} kN/m², PP(2) [p]2 = {r.P2:0.0} kN/m² → [p] = {r.Allowable:0.0} kN/m²; "
                        + $"lún khối quy ước {(r.Settlement?.S ?? double.NaN) * 100:0.00} cm; đầu cừ {r.TopDepth:0.00} m / MNN {(double.IsFinite(r.WaterDepth) ? r.WaterDepth.ToString("0.00") + " m" : "không gặp")} → "
                        + (f?.CuTram != null && !f.CuTram.Feasible ? f.CuTram.Conclusion : r.AnyFail ? "KHÔNG ĐẠT" : "ĐẠT");
            if (f?.CuTram != null && !f.CuTram.Feasible) foreach (var it in f.CuTram.Items.Where(i => i.Name.StartsWith("Khả thi")).Take(1)) r.Items.Insert(0, it);
            _sum.Foreground = r.AnyFail ? Brushes.DarkRed : Brushes.DarkGreen;
            var rep = new FoundationCheckReport
            {
                Kind = "Cừ tràm", Classification = CuTramSettings.MethodName(cs.Method), Behaviour = string.Join("\n", r.Notes), Items = r.Items, Warnings = r.Warnings
            };
            FoundationSafetyView.Fill(_grid, _beh, _warn, rep);
        }
        catch (Exception ex)
        {
            Last = null;
            _sum.Text = "Lỗi tính thử cừ tràm: " + ex.Message;
            _sum.Foreground = Brushes.DarkRed;
            FoundationSafetyView.Fill(_grid, _beh, _warn, null);
        }
    }

    public async Task ApplyAsync()
    {
        if (_working) return;
        _working = true;
        try
        {
            await _apply(Build(), _node, NodeChoice);
            Evaluate();
        }
        finally { _working = false; }
    }

    /// <summary>Đặt MNN thấp nhất (tự kiểm tra).</summary>
    public void SetWater(double depth) => _tMnn.Text = DialogUi.F(depth, "0.##");
}
