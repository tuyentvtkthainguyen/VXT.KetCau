using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KetCauVXT.Mep;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Điều hoà – thông gió trong môi trường Điện nước: đặt dàn lạnh / dàn nóng / miệng gió, vẽ ống gió (2 điểm, tự chọn cỡ theo
/// lưu lượng), nối ống đồng gas (dàn lạnh → dàn nóng), ống nước ngưng có độ dốc (dàn lạnh → điểm xả). Lõi: Mep/MepHvac.cs.</summary>
public sealed partial class MepWindow
{
    private readonly ComboBox _ductSys = new() { Width = 110, Margin = new Thickness(4, 2, 4, 2) };
    private readonly TextBox _ductFlow = new() { Width = 60, Text = "1200", Margin = new Thickness(2), ToolTip = "Lưu lượng gió (m³/h) – tự chọn cỡ ống theo vận tốc ống chính" };
    private readonly TextBox _unitBtu = new() { Width = 60, Text = "12000", Margin = new Thickness(2), ToolTip = "Công suất lạnh (BTU/h)" };
    private readonly CheckBox _ductRound = new() { Content = "Ống tròn", Margin = new Thickness(4, 6, 4, 0) };
    private (double X, double Y)? _hvacFrom;
    private int _hvacUnitFrom;

    private static double Num(string s, double def) =>
        double.TryParse((s ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0 ? v : def;

    private StackPanel BuildHvacRibbon()
    {
        foreach (var s in Enum.GetValues<HvacDuctSystem>()) _ductSys.Items.Add(new ComboBoxItem { Content = HvacSizing.SystemName(s), Tag = s });
        _ductSys.SelectedIndex = 0;
        var sp = new StackPanel();
        var r1 = new WrapPanel();
        r1.Children.Add(new TextBlock { Text = "Thiết bị:", FontWeight = FontWeights.Bold, Margin = new Thickness(4, 6, 6, 0) });
        r1.Children.Add(new TextBlock { Text = "BTU/h", Margin = new Thickness(4, 6, 0, 0) });
        r1.Children.Add(_unitBtu);
        r1.Children.Add(Btn("Dàn lạnh", () => HvacTool("hvac-in", "Đặt dàn lạnh: bấm vị trí trên mặt bằng.")));
        r1.Children.Add(Btn("Dàn nóng", () => HvacTool("hvac-out", "Đặt dàn nóng: bấm vị trí (ban công / mái).")));
        r1.Children.Add(Btn("Miệng gió cấp", () => HvacTool("hvac-ts", "Đặt miệng gió cấp: bấm vị trí trên trần.")));
        r1.Children.Add(Btn("Miệng gió hồi", () => HvacTool("hvac-tr", "Đặt miệng gió hồi: bấm vị trí trên trần.")));
        sp.Children.Add(r1);
        var r2 = new WrapPanel();
        r2.Children.Add(new TextBlock { Text = "Ống gió:", FontWeight = FontWeights.Bold, Margin = new Thickness(4, 6, 6, 0) });
        r2.Children.Add(_ductSys);
        r2.Children.Add(new TextBlock { Text = "Q (m³/h)", Margin = new Thickness(4, 6, 0, 0) });
        r2.Children.Add(_ductFlow);
        r2.Children.Add(_ductRound);
        r2.Children.Add(Btn("Vẽ ống gió", () => HvacTool("hvac-duct", "Ống gió: bấm điểm đầu rồi điểm cuối (đi trực giao sát trần, A = Q/(3600·v))."),
            "Cỡ ống tự chọn theo lưu lượng và vận tốc (Cài đặt ĐHKK); phụ kiện cút / tê / côn tự sinh"));
        r2.Children.Add(Btn("Ống đồng gas", () => HvacTool("hvac-cu", "Ống đồng: bấm dàn lạnh rồi dàn nóng."), "Cặp ống lỏng / gas chọn theo công suất dàn lạnh"));
        r2.Children.Add(Btn("Ống nước ngưng", () => HvacTool("hvac-cond", "Nước ngưng: bấm dàn lạnh rồi điểm xả (độ dốc ≥ 1 %)."), "uPVC, cao độ giảm dần theo độ dốc cài đặt"));
        r2.Children.Add(Btn("Va chạm ống gió – dầm", () =>
        {
            var c = HvacClashCheck.Beams(_p);
            MessageBox.Show(this, c.Count == 0 ? "Không có ống gió va dầm." : string.Join("\n", c.Take(30).Select(q => "• " + q.Message)) + (c.Count > 30 ? $"\n… ({c.Count} va chạm)" : ""),
                "Va chạm ống gió – dầm", MessageBoxButton.OK, c.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }));
        r2.Children.Add(Btn("Xoá ĐHKK tầng này", () => Edit(() =>
        {
            var h = _p.Mep.Hvac; string st = Story;
            int n = h.Ducts.RemoveAll(q => q.Story == st) + h.Units.RemoveAll(q => q.Story == st) + h.Terminals.RemoveAll(q => q.Story == st) + h.Pipes.RemoveAll(q => q.Story == st);
            SetStatus($"Đã xoá {n} phần tử điều hoà – thông gió tầng {st}.");
        })));
        sp.Children.Add(r2);
        return sp;
    }

    private void HvacTool(string t, string msg) { _tool = t; _hvacFrom = null; _hvacUnitFrom = 0; SetStatus(msg + " Esc để thôi."); }

    private HvacUnit HitUnit(double x, double y, HvacUnitKind? kind = null) =>
        _p.Mep.Hvac.Units.Where(u => u.Story == Story && (kind == null || u.Kind == kind))
            .Select(u => (u, d: MepGeom.Dist2D(u.X, u.Y, x, y))).Where(q => q.d <= Math.Max(0.5, 12 / _scale)).OrderBy(q => q.d).Select(q => q.u).FirstOrDefault();

    private void HvacClick(double x, double y)
    {
        _p.Mep.Hvac ??= new HvacModel();
        double btu = Num(_unitBtu.Text, 12000);
        switch (_tool)
        {
            case "hvac-in":
            case "hvac-out":
            {
                var kind = _tool == "hvac-in" ? HvacUnitKind.DanLanh : HvacUnitKind.DanNong;
                Edit(() => { var u = HvacRouting.AddUnit(_p, Story, kind, x, y, btu); SetStatus($"Đã đặt {u.Name}. Bấm tiếp để đặt thêm."); });
                break;
            }
            case "hvac-ts":
            case "hvac-tr":
            {
                bool sup = _tool == "hvac-ts";
                Edit(() => { var t = HvacRouting.AddTerminal(_p, Story, x, y, sup); SetStatus($"Đã đặt {t.Name}."); });
                break;
            }
            case "hvac-duct":
            {
                if (_hvacFrom == null) { _hvacFrom = (x, y); SetStatus("Bấm điểm cuối ống gió…"); return; }
                var (x0, y0) = _hvacFrom.Value;
                var sys = _ductSys.SelectedItem is ComboBoxItem ci && ci.Tag is HvacDuctSystem hs ? hs : HvacDuctSystem.GioCap;
                double q = Num(_ductFlow.Text, 0);
                bool round = _ductRound.IsChecked == true;
                Edit(() =>
                {
                    var d = HvacRouting.AddDuct(_p, Story, x0, y0, x, y, sys, q, round);
                    HvacRouting.RebuildFittings(_p);
                    double v = d.Round ? HvacSizing.VelocityRound(q, d.Dia) : HvacSizing.Velocity(q, d.W, d.H);
                    SetStatus($"Đã vẽ ống gió {d.SizeText} dài {d.Length:0.00} m" + (q > 0 ? $", v = {v:0.0} m/s" : "") + ". Bấm điểm tiếp theo (nối tiếp) hoặc Esc…");
                });
                _hvacFrom = (x, y);   // vẽ nối tiếp từ điểm cuối
                break;
            }
            case "hvac-cu":
            {
                if (_hvacUnitFrom == 0)
                {
                    var u0 = HitUnit(x, y, HvacUnitKind.DanLanh);
                    if (u0 == null) { SetStatus("Bấm vào một dàn lạnh."); return; }
                    _hvacUnitFrom = u0.Id; SetStatus("Bấm dàn nóng…"); return;
                }
                var u1 = HitUnit(x, y, HvacUnitKind.DanNong);
                if (u1 == null) { SetStatus("Bấm vào một dàn nóng."); return; }
                int from = _hvacUnitFrom;
                Edit(() =>
                {
                    var pp = HvacRouting.ConnectRefrigerant(_p, _p.Mep.Hvac.Unit(from), u1);
                    SetStatus($"Đã nối ống đồng {pp.SizeText} dài {pp.Length:0.00} m. Bấm dàn lạnh khác…");
                });
                _hvacUnitFrom = 0;
                break;
            }
            case "hvac-cond":
            {
                if (_hvacUnitFrom == 0)
                {
                    var u0 = HitUnit(x, y, HvacUnitKind.DanLanh);
                    if (u0 == null) { SetStatus("Bấm vào một dàn lạnh."); return; }
                    _hvacUnitFrom = u0.Id; SetStatus("Bấm điểm xả nước ngưng…"); return;
                }
                int from = _hvacUnitFrom;
                Edit(() =>
                {
                    var pp = HvacRouting.AddCondensate(_p, _p.Mep.Hvac.Unit(from), x, y);
                    SetStatus($"Đã vẽ ống nước ngưng Ø{pp.Dia:0} dài {pp.Length:0.00} m, i = {pp.Slope * 100:0.#} % (cuối thấp hơn đầu {(pp.Path[0].Z - pp.Path[^1].Z) * 1000:0} mm).");
                });
                _hvacUnitFrom = 0;
                break;
            }
        }
    }

    private void DrawHvac()
    {
        var h = _p.Mep?.Hvac;
        if (h == null) return;
        string st = Story;
        foreach (var d in h.Ducts.Where(q => q.Story == st))
        {
            var pl = new Shapes.Polyline { Stroke = Br(HvacSizing.SystemColor(d.System)), StrokeThickness = Math.Max(2, d.Width / 1000 * _scale), Opacity = 0.45,
                StrokeLineJoin = PenLineJoin.Miter, ToolTip = $"Ống gió {HvacSizing.SystemName(d.System)} {d.SizeText} – {d.Length:0.00} m – tôn {d.SheetArea:0.00} m²" };
            foreach (var q in d.Path) pl.Points.Add(S(q.X, q.Y));
            _cv.Children.Add(pl);
        }
        foreach (var t in h.Terminals.Where(q => q.Story == st))
        {
            var s = S(t.X, t.Y); double a = Math.Max(8, t.W / 1000 * _scale);
            var r = new Shapes.Rectangle { Width = a, Height = a, Stroke = Br(t.Supply ? "#1E88E5" : "#43A047"), StrokeThickness = 1.5, ToolTip = t.Name };
            Canvas.SetLeft(r, s.X - a / 2); Canvas.SetTop(r, s.Y - a / 2); _cv.Children.Add(r);
            _cv.Children.Add(new Shapes.Line { X1 = s.X - a / 2, Y1 = s.Y - a / 2, X2 = s.X + a / 2, Y2 = s.Y + a / 2, Stroke = r.Stroke, IsHitTestVisible = false });
            if (t.Supply) _cv.Children.Add(new Shapes.Line { X1 = s.X - a / 2, Y1 = s.Y + a / 2, X2 = s.X + a / 2, Y2 = s.Y - a / 2, Stroke = r.Stroke, IsHitTestVisible = false });
        }
        foreach (var q in h.Pipes.Where(q => q.Story == st))
        {
            var pl = new Shapes.Polyline { Stroke = Br(q.Kind == HvacPipeKind.OngDong ? "#B87333" : "#607D8B"), StrokeThickness = 1.6,
                ToolTip = (q.Kind == HvacPipeKind.OngDong ? "Ống đồng " : "Ống nước ngưng ") + q.SizeText + $" – {q.Length:0.00} m" };
            pl.StrokeDashArray = q.Kind == HvacPipeKind.OngDong ? new DoubleCollection { 6, 2, 1, 2 } : new DoubleCollection { 3, 2 };
            foreach (var v in q.Path) pl.Points.Add(S(v.X, v.Y));
            _cv.Children.Add(pl);
        }
        foreach (var u in h.Units.Where(q => q.Story == st))
        {
            var s = S(u.X, u.Y); double w = Math.Max(14, u.W * _scale), d = Math.Max(8, u.D * _scale);
            bool sel = u.Id == _hvacUnitFrom;
            var r = new Shapes.Rectangle { Width = w, Height = d, Fill = Br(u.Kind == HvacUnitKind.DanLanh ? "#E0F7FA" : "#CFD8DC"), Stroke = sel ? Brushes.Red : Brushes.Black,
                StrokeThickness = sel ? 2 : 1, ToolTip = $"{u.Name} – {u.CapacityBtu:0} BTU/h" };
            Canvas.SetLeft(r, s.X - w / 2); Canvas.SetTop(r, s.Y - d / 2); _cv.Children.Add(r);
            var tb = new TextBlock { Text = (u.Kind == HvacUnitKind.DanLanh ? "DL" : "DN") + u.Id, FontSize = 10, IsHitTestVisible = false };
            Canvas.SetLeft(tb, s.X - w / 2 + 2); Canvas.SetTop(tb, s.Y - 7); _cv.Children.Add(tb);
        }
    }
}
