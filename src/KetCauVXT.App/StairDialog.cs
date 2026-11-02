using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Loads;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// HỘP THOẠI "KHAI BÁO CẦU THANG" (MainWindow.Stair.cs): sửa Stair gắn lỗ sàn cầu thang, phác thảo trực tiếp
// mặt bằng + mặt cắt vế 1, kiểm tra 2h + b, tải trọng vế/chiếu nghỉ. Áp dụng gọi callback (MainWindow chụp hoàn tác).
// =====================================================================
internal sealed class StairDialog : Window
{
    private readonly Project _p;
    private readonly Stair _s;
    private readonly Func<Stair, bool> _apply;
    private readonly Func<Stair, bool> _remove;
    private readonly Canvas _plan = new() { Background = Brushes.White, ClipToBounds = true };
    private readonly Canvas _sec = new() { Background = Brushes.White, ClipToBounds = true };
    private readonly TextBlock _info = new() { TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(4) };
    private readonly Dictionary<string, FrameworkElement> _f = new();
    private bool _loading;

    public bool Applied { get; private set; }

    public StairDialog(Project p, Stair draft, bool exists, Func<Stair, bool> apply, Func<Stair, bool> remove)
    {
        _p = p; _s = draft; _apply = apply; _remove = remove;
        DialogUi.Setup(this, "Khai báo cầu thang " + draft.Name, 1180, 780);

        var form = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        void Hd(string t) => form.Children.Add(new TextBlock { Text = t, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 2) });
        void Txt(string key, string label, string val, string tip = null)
        {
            var tb = new TextBox { Text = val, Width = 90, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = tip };
            tb.TextChanged += (s, e) => Changed();
            _f[key] = tb;
            form.Children.Add(DialogUi.Row(DialogUi.Label(label, 210), tb));
        }
        void Cmb(string key, string label, string[] items, int sel, string tip = null)
        {
            var cb = new ComboBox { ItemsSource = items, SelectedIndex = sel, Width = 220, ToolTip = tip };
            cb.SelectionChanged += (s, e) => Changed();
            _f[key] = cb;
            form.Children.Add(DialogUi.Row(DialogUi.Label(label, 210), cb));
        }
        void Chk(string key, string label, bool val, string tip = null)
        {
            var c = new CheckBox { Content = label, IsChecked = val, ToolTip = tip, Margin = new Thickness(0, 3, 0, 3) };
            c.Checked += (s, e) => Changed(); c.Unchecked += (s, e) => Changed();
            _f[key] = c;
            form.Children.Add(c);
        }
        string Mm(double m) => m > 0 ? (m * 1000).ToString("0", CultureInfo.InvariantCulture) : "0";

        _loading = true;
        Hd("Hình học");
        Txt("name", "Tên cầu thang", draft.Name);
        Cmb("type", "Dạng thang", new[] { Stair.TypeName(StairType.TwoFlightU), Stair.TypeName(StairType.Straight), Stair.TypeName(StairType.ThreeFlightU) }, (int)draft.Type);
        Cmb("axis", "Phương vế", new[] { "Tự động (cạnh dài ô)", "Theo trục X", "Theo trục Y" }, (int)draft.Axis);
        Chk("startMax", "Chân thang ở phía toạ độ lớn của ô", draft.StartAtMax);
        Txt("risers", "Tổng số bậc (0 = tự động)", draft.Risers.ToString(CultureInfo.InvariantCulture), "Chiều cao tầng / ~170 mm");
        Txt("first", "Số bậc vế 1 (0 = chia đều)", draft.FirstFlightRisers.ToString(CultureInfo.InvariantCulture));
        Txt("tread", "Bề rộng bậc b (mm, 0 = tự động)", Mm(draft.Tread), "0,63 − 2h trong 260…300 mm, co lại nếu ô thang ngắn");
        Txt("width", "Bề rộng vế (mm, 0 = tự động)", Mm(draft.FlightWidth));
        Txt("gap", "Khe giữa 2 vế (mm)", Mm(draft.Gap));
        Txt("waist", "Bản thang dày (mm, 0 = L/30 ≥ 100)", Mm(draft.WaistThickness));
        Txt("ldDepth", "Chiều sâu chiếu nghỉ (mm, 0 = tự động)", Mm(draft.LandingDepth));
        Txt("ldT", "Chiếu nghỉ dày (mm, 0 = bằng bản)", Mm(draft.LandingThickness));
        Hd("Sơ đồ chịu lực");
        Cmb("support", "Bản thang", new[] { "Tựa dầm chiếu nghỉ / dầm sàn (bản chịu lực)", "Có dầm cốn (limon)" }, (int)draft.Support);
        Cmb("sides", "Số dầm cốn mỗi vế", new[] { "1 bên (bên kia tựa tường)", "2 bên" }, draft.StringerSides >= 2 ? 1 : 0);
        Txt("strB", "Dầm cốn b (mm)", Mm(draft.StringerB));
        Txt("strH", "Dầm cốn h (mm)", Mm(draft.StringerH));
        Chk("ldBeam", "Có dầm chiếu nghỉ", draft.LandingBeam);
        Txt("lbB", "Dầm chiếu nghỉ b (mm)", Mm(draft.LandingBeamB));
        Txt("lbH", "Dầm chiếu nghỉ h (mm)", Mm(draft.LandingBeamH));
        Chk("fix", "Gối bản thang ngàm một phần (M = qL²/10)", draft.PartialFixity);
        Hd("Bậc thang, hoàn thiện, tải");
        Cmb("steps", "Bậc thang", new[] { Stair.StepsName(StepConstruction.Brick), Stair.StepsName(StepConstruction.Concrete) }, (int)draft.Steps);
        Cmb("finish", "Ốp mặt bậc", new[] { "Đá granite", "Gỗ", "Gạch ốp", "Không" }, (int)draft.Finish);
        Txt("finT", "Dày lớp ốp (mm, 0 = mặc định)", Mm(draft.FinishThickness), "Granite 20, gỗ 25, gạch 10 mm");
        Txt("bed", "Vữa lót (mm)", Mm(draft.BeddingThickness));
        Txt("plaster", "Trát mặt dưới bản (mm)", Mm(draft.PlasterThickness));
        Chk("skirt", "Tính len chân tường vào khối lượng ốp", draft.Skirting);
        Cmb("rail", "Lan can", new[] { "Không", "Sắt / inox, tay vịn gỗ (0,3 kN/m)", "Kính cường lực (0,5 kN/m)", "Tường xây 110 (2,3 kN/m)" }, (int)draft.Railing);
        Txt("railQ", "Tải lan can (kN/m, 0 = theo loại)", draft.RailingLoad.ToString("0.##", CultureInfo.InvariantCulture));
        Txt("live", "Hoạt tải (kN/m²)", draft.LiveLoad.ToString("0.##", CultureInfo.InvariantCulture), "TCVN 2737:2023: cầu thang nhà ở 3,0 kN/m²");
        _loading = false;

        var btApply = DialogUi.Btn("Áp dụng", (s, e) => ApplyNow(), true);
        var btRemove = DialogUi.Btn("Xoá khai báo", (s, e) => { if (_remove != null && _remove(_s)) { Applied = true; Close(); } });
        btRemove.IsEnabled = exists && _remove != null;
        var btClose = DialogUi.Btn("Đóng", (s, e) => Close());
        btClose.IsCancel = true;

        var left = new DockPanel();
        var bar = DialogUi.Row(btApply, btRemove, btClose);
        DockPanel.SetDock(bar, Dock.Bottom);
        left.Children.Add(bar);
        left.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var bp = new Border { BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), Child = _plan, Margin = new Thickness(0, 0, 0, 4) };
        var bs = new Border { BorderBrush = Brushes.Silver, BorderThickness = new Thickness(1), Child = _sec, Margin = new Thickness(0, 0, 0, 4) };
        Grid.SetRow(bs, 1);
        var sv = new ScrollViewer { Content = _info, MaxHeight = 230, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(sv, 2);
        right.Children.Add(bp); right.Children.Add(bs); right.Children.Add(sv);
        _plan.SizeChanged += (s, e) => Redraw();
        _sec.SizeChanged += (s, e) => Redraw();

        var root = new Grid { Margin = new Thickness(8) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(470) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(right, 1);
        root.Children.Add(left); root.Children.Add(right);
        Content = root;
        Loaded += (s, e) => Redraw();
    }

    // ------------------------------------------------------------------
    private double Num(string key, double def)
    {
        if (_f.TryGetValue(key, out var fe) && fe is TextBox tb) return DialogUi.ParseNum(tb.Text, def);
        return def;
    }
    private int Sel(string key) => _f.TryGetValue(key, out var fe) && fe is ComboBox cb ? Math.Max(0, cb.SelectedIndex) : 0;
    private bool On(string key) => _f.TryGetValue(key, out var fe) && fe is CheckBox c && c.IsChecked == true;
    private static double M(double mm) => Math.Max(0, mm) / 1000.0;

    /// <summary>Cầu thang theo các ô nhập hiện tại (bản sao, chưa đưa vào dự án).</summary>
    public Stair Current
    {
        get
        {
            var s = _s.Clone();
            s.Name = (_f["name"] as TextBox)?.Text.Trim() ?? s.Name;
            s.Type = (StairType)Sel("type");
            s.Axis = (StairAxis)Sel("axis");
            s.StartAtMax = On("startMax");
            s.Risers = (int)Math.Max(0, Math.Round(Num("risers", 0)));
            s.FirstFlightRisers = (int)Math.Max(0, Math.Round(Num("first", 0)));
            s.Tread = M(Num("tread", 0));
            s.FlightWidth = M(Num("width", 0));
            s.Gap = M(Num("gap", 100));
            s.WaistThickness = M(Num("waist", 0));
            s.LandingDepth = M(Num("ldDepth", 0));
            s.LandingThickness = M(Num("ldT", 0));
            s.Support = (StairSupport)Sel("support");
            s.StringerSides = Sel("sides") == 1 ? 2 : 1;
            s.StringerB = Math.Max(0.08, M(Num("strB", 150)));
            s.StringerH = Math.Max(0.15, M(Num("strH", 300)));
            s.LandingBeam = On("ldBeam");
            s.LandingBeamB = Math.Max(0.1, M(Num("lbB", 220)));
            s.LandingBeamH = Math.Max(0.15, M(Num("lbH", 300)));
            s.PartialFixity = On("fix");
            s.Steps = (StepConstruction)Sel("steps");
            s.Finish = (StepFinish)Sel("finish");
            s.FinishThickness = M(Num("finT", 0));
            s.BeddingThickness = M(Num("bed", 20));
            s.PlasterThickness = M(Num("plaster", 15));
            s.Skirting = On("skirt");
            s.Railing = (RailingKind)Sel("rail");
            s.RailingLoad = Math.Max(0, Num("railQ", 0));
            s.LiveLoad = Math.Clamp(Num("live", StairUnits.LiveDefault), 0, 20);
            return s;
        }
    }

    /// <summary>Áp dụng vào dự án (qua callback MainWindow – có hoàn tác). Trả về true nếu thành công.</summary>
    public bool ApplyNow()
    {
        var s = Current;
        var L = s.Layout(_p);
        if (!L.Valid) { MessageBox.Show(this, string.Join("\n", L.Warnings), Title, MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        bool ok = _apply != null && _apply(s);
        if (ok) { Applied = true; Close(); }
        return ok;
    }

    private void Changed() { if (!_loading) Redraw(); }

    // ------------------------------------------------------------------
    // Phác thảo
    // ------------------------------------------------------------------
    private void Redraw()
    {
        if (_loading || !IsLoaded) return;
        _plan.Children.Clear(); _sec.Children.Clear();
        Stair s;
        StairLayout L;
        try { s = Current; L = s.Layout(_p); }
        catch (Exception ex) { _info.Text = "Lỗi: " + ex.Message; return; }
        var sb = new System.Text.StringBuilder();
        if (!L.Valid) { _info.Text = string.Join("\n", L.Warnings); return; }
        sb.AppendLine($"{Stair.TypeName(s.Type)} – ô {L.Lr:0.00} × {L.Wr:0.00} m, H = {L.H:0.000} m ({DxfText(L.ZBot)} → {DxfText(L.ZTop)})");
        sb.AppendLine($"{L.NRisers} bậc: h = {L.Riser * 1000:0} mm, b = {L.Tread * 1000:0} mm → 2h + b = {L.Check2hb * 1000:0} mm {(L.StepOk ? "(đạt 600–650)" : "(NGOÀI 600–650 mm)")}");
        sb.AppendLine($"Vế: {string.Join("; ", L.Flights.Select(f => $"V{f.Index} {f.Risers} bậc, L nghiêng {f.Inclined:0.00} m"))}; bề rộng vế {L.FlightWidth * 1000:0} mm");
        sb.AppendLine($"Bản thang t = {L.WaistT * 1000:0} mm; chiếu nghỉ t = {L.LandingT * 1000:0} mm; cosα = {L.Flights[0].CosA:0.000}");
        try
        {
            var set = StairLoadCalc.Compute(_p, L);
            sb.AppendLine($"Tải vế: gtc = {set.FlightStd:0.00}, gtt = {set.FlightDesign:0.00} kN/m² mặt bằng; chiếu nghỉ: {set.LandingStd:0.00} / {set.LandingDesign:0.00} kN/m²");
            sb.AppendLine("  " + string.Join("; ", set.Flight.Select(r => $"{r.Name} {r.Gk:0.00}")));
        }
        catch (Exception ex) { sb.AppendLine("Tải: " + ex.Message); }
        foreach (var w in L.Warnings) sb.AppendLine("⚠ " + w);
        _info.Text = sb.ToString();
        _info.Foreground = L.StepOk && L.Warnings.Count == 0 ? Brushes.Black : Brushes.DarkRed;
        DrawPlan(L);
        DrawSection(L);
    }

    private static string DxfText(double z) => KetCauVXT.Drawing.DxfWriter.ElevText(z);

    private void Add(Canvas c, UIElement e) => c.Children.Add(e);

    private void DrawPlan(StairLayout L)
    {
        double W = _plan.ActualWidth, H = _plan.ActualHeight;
        if (W < 20 || H < 20) return;
        double m = 24, k = Math.Min((W - 2 * m) / Math.Max(L.Lr, 0.1), (H - 2 * m) / Math.Max(L.Wr, 0.1));
        Point P(double u, double v) => new(m + u * k, H - m - v * k);
        void Line(double u1, double v1, double u2, double v2, Brush b, double t = 1)
        {
            var a = P(u1, v1); var c = P(u2, v2);
            Add(_plan, new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = c.X, Y2 = c.Y, Stroke = b, StrokeThickness = t });
        }
        void Rect(double u1, double v1, double u2, double v2, Brush b, Brush fill = null, double t = 1)
        {
            var a = P(Math.Min(u1, u2), Math.Max(v1, v2));
            var r = new Shapes.Rectangle { Width = Math.Abs(u2 - u1) * k, Height = Math.Abs(v2 - v1) * k, Stroke = b, StrokeThickness = t, Fill = fill };
            Canvas.SetLeft(r, a.X); Canvas.SetTop(r, a.Y);
            Add(_plan, r);
        }
        Rect(0, 0, L.Lr, L.Wr, Brushes.Black, null, 2);
        foreach (var ld in L.Landings) Rect(ld.Umin, ld.Vmin, ld.Umax, ld.Vmax, Brushes.Gray, new SolidColorBrush(Color.FromRgb(235, 240, 250)));
        int no = 0;
        foreach (var f in L.Flights)
        {
            Rect(f.Umin, f.Vmin, f.Umax, f.Vmax, Brushes.DimGray, new SolidColorBrush(Color.FromRgb(250, 245, 235)));
            for (int i = 0; i <= f.Treads; i++)
            {
                double t = i * L.Tread;
                if (f.DU != 0) { double u = f.U0 + f.DU * t; Line(u, f.Vmin, u, f.Vmax, Brushes.Gray); }
                else { double v = f.V0 + f.DV * t; Line(f.Umin, v, f.Umax, v, Brushes.Gray); }
            }
            double a0 = 0.5 * L.Tread, a1 = Math.Max(a0 + 0.05, f.Run - 0.3 * L.Tread);
            if (f.DU != 0)
            {
                double vm = (f.Vmin + f.Vmax) / 2;
                Line(f.U0 + f.DU * a0, vm, f.U0 + f.DU * a1, vm, Brushes.Firebrick, 1.5);
                Line(f.U0 + f.DU * a1, vm, f.U0 + f.DU * (a1 - 0.15), vm + 0.08, Brushes.Firebrick, 1.5);
                Line(f.U0 + f.DU * a1, vm, f.U0 + f.DU * (a1 - 0.15), vm - 0.08, Brushes.Firebrick, 1.5);
            }
            else
            {
                double um = (f.Umin + f.Umax) / 2;
                Line(um, f.V0 + f.DV * a0, um, f.V0 + f.DV * a1, Brushes.Firebrick, 1.5);
            }
            if (L.Stringers && f.DU != 0)
            {
                if (L.StringerCount >= 2 || f.Vmin > 1e-6) Rect(f.Umin, f.Vmin, f.Umax, f.Vmin + L.S.StringerB, Brushes.SteelBlue, Brushes.LightSteelBlue);
                if (L.StringerCount >= 2 || f.Vmin < 1e-6) Rect(f.Umin, f.Vmax - L.S.StringerB, f.Umax, f.Vmax, Brushes.SteelBlue, Brushes.LightSteelBlue);
            }
            var lb = new TextBlock { Text = $"V{f.Index}: {no + 1}…{no + f.Treads}", FontSize = 11, Foreground = Brushes.Firebrick };
            var pp = P(f.DU != 0 ? (f.Umin + f.Umax) / 2 : f.Umin, f.DU != 0 ? f.Vmin + 0.1 : (f.Vmin + f.Vmax) / 2);
            Canvas.SetLeft(lb, pp.X); Canvas.SetTop(lb, pp.Y - 16);
            Add(_plan, lb);
            no += f.Risers;
        }
        if (L.HasLandingBeam) Rect(L.LandingBeamU - L.S.LandingBeamB / 2, 0, L.LandingBeamU + L.S.LandingBeamB / 2, L.Wr, Brushes.SteelBlue, Brushes.LightSteelBlue);
        var tt = new TextBlock { Text = "MẶT BẰNG (chân thang bên trái)", FontWeight = FontWeights.Bold, FontSize = 11 };
        Canvas.SetLeft(tt, 4); Canvas.SetTop(tt, 2);
        Add(_plan, tt);
    }

    private void DrawSection(StairLayout L)
    {
        double W = _sec.ActualWidth, H = _sec.ActualHeight;
        if (W < 20 || H < 20 || L.Flights.Count == 0) return;
        var f = L.Flights[0];
        double ld = L.Landings.Count > 0 ? L.Landings[0].Span : 0.5;
        double sMin = -0.8, sMax = f.Run + Math.Max(0.8, ld);
        double zMin = f.ZBottom - 0.6, zMax = f.ZTop + 0.3;
        double m = 20, k = Math.Min((W - 2 * m) / (sMax - sMin), (H - 2 * m) / (zMax - zMin));
        Point P(double sx, double z) => new(m + (sx - sMin) * k, H - m - (z - zMin) * k);
        void Poly(IEnumerable<(double, double)> pts, Brush stroke, Brush fill = null, double t = 1)
        {
            var pg = new Shapes.Polygon { Stroke = stroke, StrokeThickness = t, Fill = fill };
            foreach (var (a, b) in pts) pg.Points.Add(P(a, b));
            Add(_sec, pg);
        }
        double b = L.Tread, h = L.Riser, t = L.WaistT, cos = f.CosA;
        double zB = f.ZBottom, zT = f.ZTop, run = f.Run;
        // bản thang
        Poly(new[] { (0.0, zB), (run, zB + f.Treads * h), (run, zB + f.Treads * h - t / cos), (0.0, zB - t / cos) }, Brushes.Black, new SolidColorBrush(Color.FromRgb(200, 200, 200)));
        // bậc
        var stepFill = L.S.Steps == StepConstruction.Brick ? new SolidColorBrush(Color.FromRgb(230, 170, 140)) : new SolidColorBrush(Color.FromRgb(200, 200, 200));
        for (int i = 1; i <= f.Treads; i++)
            Poly(new[] { ((i - 1) * b, zB + (i - 1) * h), ((i - 1) * b, zB + i * h), (i * b, zB + i * h) }, Brushes.DimGray, stepFill);
        // sàn dưới, chiếu nghỉ / sàn trên
        Poly(new[] { (sMin, zB), (0.0, zB), (0.0, zB - 0.1), (sMin, zB - 0.1) }, Brushes.Black, Brushes.LightGray);
        bool toLanding = Math.Abs(zT - L.ZTop) > 1e-6;
        double tt2 = toLanding ? L.LandingT : 0.1;
        Poly(new[] { (run, zT), (sMax, zT), (sMax, zT - tt2), (run, zT - tt2) }, Brushes.Black, Brushes.LightGray);
        if (toLanding && L.HasLandingBeam)
            Poly(new[] { (run - L.S.LandingBeamB / 2, zT), (run + L.S.LandingBeamB / 2, zT), (run + L.S.LandingBeamB / 2, zT - L.S.LandingBeamH), (run - L.S.LandingBeamB / 2, zT - L.S.LandingBeamH) }, Brushes.SteelBlue, Brushes.LightSteelBlue);
        var lbl = new TextBlock
        {
            Text = $"MẶT CẮT VẾ 1: {f.Risers}×{h * 1000:0} = {(zT - zB) * 1000:0} mm; {f.Treads}×{b * 1000:0} = {run * 1000:0} mm; bản {t * 1000:0} mm; " +
                   (L.S.Steps == StepConstruction.Brick ? "bậc xây gạch" : "bậc BTCT"),
            FontWeight = FontWeights.Bold, FontSize = 11
        };
        Canvas.SetLeft(lbl, 4); Canvas.SetTop(lbl, 2);
        Add(_sec, lbl);
    }
}
