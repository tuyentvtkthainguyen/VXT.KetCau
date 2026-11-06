using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Mep;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>ĐHKK – sửa từng đối tượng trên mặt bằng điện nước: bấm chọn ống gió / miệng gió / dàn lạnh – nóng / ống đồng – nước ngưng
/// (Ctrl + bấm: thêm / bớt), quét chọn (trái → phải: nằm trọn; phải → trái: chạm), kéo để dời (kéo đỉnh ống gió để nắn tuyến),
/// bảng thuộc tính (cỡ, lưu lượng, hệ, cao độ, công suất kW / BTU), Delete xoá, hạ ống qua dầm, vẽ lại tuyến, chọn máy theo phòng.
/// Mỗi thao tác là một bước hoàn tác. Lõi: Mep/MepHvacEdit.cs.</summary>
public sealed partial class MepWindow
{
    private readonly List<(HvacElemKind Kind, int Id)> _hvSel = new();
    private HvacPick _hvDrag;
    private (double X, double Y) _hvDragFrom, _hvDragDone;
    private Point _hvDragPx;
    private bool _hvDragging;
    private string _hvDragSnap;
    private Point? _hvBoxFrom;
    private Shapes.Rectangle _hvBox;
    private int _hvReroute;
    private readonly CheckBox _hvAutoDrop = new() { Content = "Tự hạ ống qua dầm (2 cút 45°)", Margin = new Thickness(6, 6, 4, 0), IsChecked = true };
    private readonly TextBox _hvPeople = new() { Width = 30, Text = "2", Margin = new Thickness(2), ToolTip = "Số người trong phòng (tính tải lạnh)" };

    private static double Dbl(string s, double def) =>
        double.TryParse((s ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;

    /// <summary>Hàng lệnh sửa / kiểm tra ĐHKK (gắn vào tab Điều hoà – thông gió).</summary>
    private WrapPanel BuildHvacEditRow()
    {
        var r3 = new WrapPanel();
        r3.Children.Add(new TextBlock { Text = "Sửa:", FontWeight = FontWeights.Bold, Margin = new Thickness(4, 6, 6, 0) });
        r3.Children.Add(Btn("Chọn / sửa", () => { SetTool("select"); SetStatus("Chọn ĐHKK: bấm đối tượng (Ctrl + bấm thêm / bớt), quét khung để chọn nhiều, kéo để dời, Delete xoá."); },
            "Bấm chọn, quét chọn, kéo dời, sửa thuộc tính bên trái"));
        r3.Children.Add(Btn("Hạ ống qua dầm", HvacDropCommand, "Ống gió đã chọn (hoặc mọi ống gió tầng này) va dầm → hạ dưới đáy dầm bằng 2 cút 45° mỗi phía nếu trần đủ chỗ; không đủ → cảnh báo"));
        r3.Children.Add(Btn("Vẽ lại tuyến", () =>
        {
            var d = _hvSel.Where(s => s.Kind == HvacElemKind.Duct).Select(s => s.Id).FirstOrDefault();
            if (d == 0) { SetStatus("Chọn một ống gió trước."); return; }
            _tool = "hvac-reroute"; _hvReroute = d; _hvacFrom = null; SetStatus("Vẽ lại tuyến ống gió: bấm điểm đầu rồi điểm cuối (giữ cỡ, hệ, cao độ). Esc để thôi.");
        }, "Vẽ lại tuyến ống gió đang chọn"));
        r3.Children.Add(_hvAutoDrop);
        _hvAutoDrop.IsChecked = _p.Mep?.Hvac?.Settings.AutoDropUnderBeams ?? true;
        _hvAutoDrop.Click += (_, _) => Edit(() => { _p.Mep.Hvac ??= new HvacModel(); _p.Mep.Hvac.Settings.AutoDropUnderBeams = _hvAutoDrop.IsChecked == true; });
        r3.Children.Add(new TextBlock { Text = "Máy theo phòng:", FontWeight = FontWeights.Bold, Margin = new Thickness(10, 6, 6, 0) });
        r3.Children.Add(new TextBlock { Text = "Số người", Margin = new Thickness(2, 6, 0, 0) });
        r3.Children.Add(_hvPeople);
        r3.Children.Add(Btn("Đặt máy cho phòng", () => HvacTool("hvac-ac", "Chọn máy theo phòng: bấm vào trong một phòng (tính tải lạnh, đặt dàn lạnh + dàn nóng, nối ống)."),
            "Tải lạnh theo diện tích → máy 9000/12000/18000/24000 BTU; dàn lạnh tường đối diện cửa, ưu tiên tường ngoài; dàn nóng tường ngoài"));
        r3.Children.Add(Btn("Tải lạnh các phòng", HvacLoadReport, "Bảng tải lạnh, máy chọn, ống đồng, aptomat / dây cho các phòng tầng này"));
        AddRound4HvacButtons(r3);   // MepWindow.Round4.cs
        return r3;
    }

    // ------------------------------------------------------------------
    // Chọn / kéo / quét
    // ------------------------------------------------------------------

    /// <summary>Gọi từ OnDown (công cụ chọn). true = đã bắt một đối tượng ĐHKK.</summary>
    private bool HvacSelectDown(Point pt, double x, double y)
    {
        if (_p.Mep?.Hvac == null) return false;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (HitDevice(pt) != null || HitRun(pt) != null) { _hvSel.Clear(); return false; }
        var pick = HvacEdit.Hit(_p, Story, x, y, Math.Max(0.02, 6 / _scale));
        if (pick == null)
        {
            if (!ctrl) _hvSel.Clear();
            _hvBoxFrom = pt; _cv.CaptureMouse();
            return false;
        }
        var key = (pick.Kind, pick.Id);
        if (ctrl) { if (!_hvSel.Remove(key)) _hvSel.Add(key); }
        else if (!_hvSel.Contains(key)) { _hvSel.Clear(); _hvSel.Add(key); }
        _selDevice = 0; _selRun = 0;
        _hvDrag = ctrl ? null : pick; _hvDragging = false;
        _hvDragFrom = (x, y); _hvDragDone = (0, 0); _hvDragPx = pt;
        if (_hvDrag != null) { _hvDragSnap = _snapshot?.Invoke(); _cv.CaptureMouse(); }
        ShowProps(); Redraw();
        SetStatus(HvacLabel(pick.Kind, pick.Id) + (pick.Kind == HvacElemKind.Duct && pick.Vertex >= 0 ? $" – đỉnh {pick.Vertex + 1}: kéo để nắn tuyến" : " – kéo để dời, Delete xoá"));
        return true;
    }

    /// <summary>Gọi từ OnMove. true = đang kéo / quét.</summary>
    private bool HvacDragMove(MouseEventArgs e)
    {
        var pt = e.GetPosition(_cv);
        if (_hvDrag != null && e.LeftButton == MouseButtonState.Pressed)
        {
            if (!_hvDragging && (pt - _hvDragPx).Length < 4) return true;
            _hvDragging = true;
            var (x, y) = W(pt);
            const double step = 0.05;
            if (_hvDrag.Kind == HvacElemKind.Duct && _hvDrag.Vertex >= 0 && _hvSel.Count == 1)
            {
                var d = _p.Mep.Hvac.Duct(_hvDrag.Id);
                if (d != null) HvacEdit.MoveVertex(_p, d, _hvDrag.Vertex, Math.Round(x / step) * step, Math.Round(y / step) * step);
            }
            else
            {
                double tx = Math.Round((x - _hvDragFrom.X) / step) * step, ty = Math.Round((y - _hvDragFrom.Y) / step) * step;
                double dx = tx - _hvDragDone.X, dy = ty - _hvDragDone.Y;
                if (Math.Abs(dx) + Math.Abs(dy) < 1e-9) return true;
                HvacEdit.MoveMany(_p, _hvSel, dx, dy);
                _hvDragDone = (tx, ty);
                SetStatus($"Dời {_hvSel.Count} đối tượng ĐHKK: Δx = {tx:0.00}, Δy = {ty:0.00} m");
            }
            Redraw();
            return true;
        }
        if (_hvBoxFrom is Point a && e.LeftButton == MouseButtonState.Pressed)
        {
            if ((pt - a).Length < 4) return true;
            if (_hvBox == null || !_cv.Children.Contains(_hvBox))
            {
                _hvBox = new Shapes.Rectangle { StrokeThickness = 1, IsHitTestVisible = false };
                _cv.Children.Add(_hvBox);
            }
            bool crossing = pt.X < a.X;
            _hvBox.Stroke = crossing ? Brushes.Green : Brushes.RoyalBlue;
            _hvBox.StrokeDashArray = crossing ? new DoubleCollection { 4, 2 } : null;
            _hvBox.Fill = new SolidColorBrush(crossing ? Color.FromArgb(30, 0, 160, 0) : Color.FromArgb(30, 30, 80, 220));
            Canvas.SetLeft(_hvBox, Math.Min(a.X, pt.X)); Canvas.SetTop(_hvBox, Math.Min(a.Y, pt.Y));
            _hvBox.Width = Math.Abs(pt.X - a.X); _hvBox.Height = Math.Abs(pt.Y - a.Y);
            return true;
        }
        return false;
    }

    private void HvacMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var pt = e.GetPosition(_cv);
        if (_hvDrag != null)
        {
            bool moved = _hvDragging;
            _hvDrag = null; _hvDragging = false; _cv.ReleaseMouseCapture();
            if (moved)
            {
                HvacRouting.RebuildFittings(_p);
                MepUpdater.Reconcile(_p);
                if (_hvDragSnap != null) _pushUndo?.Invoke(_hvDragSnap);
                _changed?.Invoke();
                RefreshWarnings(); Redraw(); ShowProps();
            }
            _hvDragSnap = null;
            return;
        }
        if (_hvBoxFrom is Point a)
        {
            _hvBoxFrom = null; _cv.ReleaseMouseCapture();
            if (_hvBox != null) { _cv.Children.Remove(_hvBox); _hvBox = null; }
            if ((pt - a).Length < 4) return;
            var (x1, y1) = W(a); var (x2, y2) = W(pt);
            bool crossing = pt.X < a.X;
            foreach (var it in HvacEdit.Window(_p, Story, x1, y1, x2, y2, crossing)) if (!_hvSel.Contains(it)) _hvSel.Add(it);
            if (_hvSel.Count > 0) { _selDevice = 0; _selRun = 0; }
            SetStatus($"Đã chọn {_hvSel.Count} đối tượng ĐHKK ({(crossing ? "chạm khung" : "nằm trọn trong khung")}).");
            ShowProps(); Redraw();
        }
    }

    private string HvacLabel(HvacElemKind k, int id)
    {
        var h = _p.Mep.Hvac;
        switch (k)
        {
            case HvacElemKind.Duct: { var d = h.Duct(id); return d == null ? "" : $"Ống gió {d.Id} {HvacSizing.SystemName(d.System)} {d.SizeText}"; }
            case HvacElemKind.Unit: { var u = h.Unit(id); return u == null ? "" : $"{u.Name} (#{u.Id})"; }
            case HvacElemKind.Terminal: { var t = h.Terminals.FirstOrDefault(q => q.Id == id); return t == null ? "" : $"{t.Name} (#{t.Id})"; }
            default: { var q = h.Pipes.FirstOrDefault(x => x.Id == id); return q == null ? "" : (q.Kind == HvacPipeKind.OngDong ? "Ống đồng " : "Ống nước ngưng ") + q.SizeText; }
        }
    }

    /// <summary>Viền đỏ quanh đối tượng ĐHKK đang chọn (gọi cuối DrawHvac).</summary>
    private void DrawHvacSelection()
    {
        var h = _p.Mep?.Hvac;
        if (h == null || _hvSel.Count == 0) return;
        _hvSel.RemoveAll(s => HvacEdit.Points(_p, s.Kind, s.Id).Count == 0);
        foreach (var (k, id) in _hvSel)
        {
            var pts = HvacEdit.Points(_p, k, id);
            if (k is HvacElemKind.Duct or HvacElemKind.Pipe)
            {
                var pl = new Shapes.Polyline { Stroke = Brushes.Red, StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 2 }, IsHitTestVisible = false };
                foreach (var (x, y) in pts) pl.Points.Add(S(x, y));
                _cv.Children.Add(pl);
                foreach (var (x, y) in pts)
                {
                    var s = S(x, y);
                    var g = new Shapes.Rectangle { Width = 7, Height = 7, Fill = Brushes.White, Stroke = Brushes.Red, IsHitTestVisible = false };
                    Canvas.SetLeft(g, s.X - 3.5); Canvas.SetTop(g, s.Y - 3.5); _cv.Children.Add(g);
                }
            }
            else
            {
                double r = k == HvacElemKind.Unit ? Math.Max(0.3, (h.Unit(id)?.W ?? 0.9) / 2) : Math.Max(0.2, (h.Terminals.FirstOrDefault(q => q.Id == id)?.W ?? 600) / 2000);
                var s = S(pts[0].X, pts[0].Y); double rp = Math.Max(8, r * _scale) + 4;
                var c = new Shapes.Rectangle { Width = 2 * rp, Height = 2 * rp, Stroke = Brushes.Red, StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 2 }, IsHitTestVisible = false };
                Canvas.SetLeft(c, s.X - rp); Canvas.SetTop(c, s.Y - rp); _cv.Children.Add(c);
            }
        }
    }

    /// <summary>Delete khi đang chọn ĐHKK. true = đã xử lý.</summary>
    private bool DeleteHvacSelected()
    {
        if (_hvSel.Count == 0 || _p.Mep?.Hvac == null) return false;
        var items = _hvSel.ToList();
        Edit(() => { int n = HvacEdit.DeleteMany(_p, items); _hvSel.Clear(); SetStatus($"Đã xoá {n} đối tượng điều hoà – thông gió (dàn lạnh / nóng xoá kèm ống nối)."); });
        return true;
    }

    private void HvacDropCommand()
    {
        if (_p.Mep?.Hvac == null) return;
        var ids = _hvSel.Where(s => s.Kind == HvacElemKind.Duct).Select(s => s.Id).ToList();
        string st = Story;
        var res = new List<HvacDropResult>();
        Edit(() =>
        {
            var ducts = ids.Count > 0 ? ids.Select(i => _p.Mep.Hvac.Duct(i)).Where(d => d != null).ToList() : _p.Mep.Hvac.Ducts.Where(d => d.Story == st).ToList();
            foreach (var d in ducts) res.Add(HvacBeamAvoid.DropUnderBeams(_p, d));
            SetStatus($"Hạ ống qua dầm: {res.Sum(r => r.Drops)} chỗ hạ (2 cút 45° mỗi phía), {res.Count(r => r.WholeDuctLowered)} tuyến hạ cả tuyến, {res.Sum(r => r.Warnings.Count)} chỗ không đủ trần.");
        });
        var w = res.SelectMany(r => r.Warnings).ToList();
        if (w.Count > 0)
            MessageBox.Show(this, string.Join("\n", w.Take(20).Select(q => "• " + q.Message + "\n   → " + q.Fix)) + (w.Count > 20 ? $"\n… ({w.Count})" : ""),
                "Ống gió không qua được dầm", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void HvacLoadReport()
    {
        string st = Story;
        var rooms = (_p.Arch?.Rooms ?? new List<Room>()).Where(r => r.Story == st && r.Outline.Count >= 3).ToList();
        if (rooms.Count == 0) { MessageBox.Show(this, "Tầng này chưa có phòng (vẽ / nhận diện phòng ở môi trường Kiến trúc).", "Tải lạnh", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        int people = (int)Math.Max(0, Dbl(_hvPeople.Text, 2));
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(_p.Mep?.Hvac?.Settings?.CoolingMethod == HvacMount.DetailMethod ? HvacMount.DetailMethod + ": " + HvacCoolingDetailed.MethodText : HvacMount.QuickMethod + ": " + HvacLoad.MethodText).AppendLine();
        foreach (var r in rooms)
        {
            var l = HvacLoad.RoomLoad(_p, r, people);
            var cp = HvacSizing.CopperPair(l.UnitBtu); var pw = HvacLoad.Power(l.UnitBtu);
            sb.AppendLine($"• {l.Room}: A = {l.Area:0.0} m², h = {l.Height:0.00} m{(l.Exterior ? ", có tường ngoài" : "")} → Q = {l.LoadBtu:0} BTU/h ({l.LoadKw:0.00} kW) → {l.UnitCount} × {l.UnitBtu:0} BTU; "
                + $"ống đồng {HvacSizing.Mm(cp.Liquid)}/{HvacSizing.Mm(cp.Gas)}; điện {pw.PowerW:0} W, aptomat {pw.BreakerA:0} A, dây {HvacSizing.Mm(pw.WireMm2)} mm²");
        }
        MessageBox.Show(this, sb.ToString(), $"Tải lạnh – tầng {st}", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>Công cụ bấm của phần sửa ĐHKK (gọi từ HvacClick). true = đã xử lý.</summary>
    private bool HvacEditClick(double x, double y)
    {
        switch (_tool)
        {
            case "hvac-reroute":
            {
                if (_hvacFrom == null) { _hvacFrom = (x, y); SetStatus("Bấm điểm cuối tuyến mới…"); return true; }
                var (x0, y0) = _hvacFrom.Value; int id = _hvReroute;
                Edit(() =>
                {
                    var d = _p.Mep.Hvac.Duct(id);
                    if (d == null) return;
                    HvacEdit.Reroute(_p, d, x0, y0, x, y);
                    var dr = _p.Mep.Hvac.Settings.AutoDropUnderBeams ? HvacBeamAvoid.DropUnderBeams(_p, d) : null;
                    SetStatus($"Đã vẽ lại tuyến ống gió {d.SizeText} dài {d.Length:0.00} m" + (dr != null && dr.Drops > 0 ? $", hạ qua {dr.Drops} dầm" : "")
                        + (dr != null && dr.Warnings.Count > 0 ? $" – {dr.Warnings.Count} chỗ không đủ trần (xem cảnh báo)" : "") + ".");
                });
                _hvacFrom = null; _tool = "select";
                return true;
            }
            case "hvac-ac":
            {
                string st = Story;
                var room = (_p.Arch?.Rooms ?? new List<Room>()).FirstOrDefault(r => r.Story == st && r.Outline.Count >= 3 && Geo2.Contains(r.Outline, x, y));
                if (room == null) { SetStatus("Bấm vào trong một phòng (phòng vẽ / nhận diện ở môi trường Kiến trúc)."); return true; }
                int people = (int)Math.Max(0, Dbl(_hvPeople.Text, 2));
                HvacAcPlacement res = null;
                Edit(() =>
                {
                    res = HvacLoad.PlaceAc(_p, room, people);
                    _hvSel.Clear();
                    foreach (var u in res.Indoor.Concat(res.Outdoor)) _hvSel.Add((HvacElemKind.Unit, u.Id));
                    SetStatus($"{res.Load.Room}: " + string.Join(" ", res.Notes));
                });
                if (res != null && res.Notes.Count > 1)
                    MessageBox.Show(this, string.Join("\n", res.Notes), "Đặt máy điều hoà", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------
    // Thuộc tính
    // ------------------------------------------------------------------

    private void HvField(string label, string value, Action<double> set)
    {
        _props.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 4, 0, 0) });
        var tb = new TextBox { Text = value ?? "" };
        tb.LostFocus += (_, _) =>
        {
            if (tb.Text == (value ?? "")) return;
            double v = Dbl(tb.Text, double.NaN);
            if (double.IsNaN(v)) { SetStatus($"{label}: giá trị không hợp lệ."); return; }
            Edit(() => set(v));
        };
        tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Keyboard.ClearFocus(); _cv.Focus(); } };
        _props.Children.Add(tb);
    }

    private void HvText(string text, bool bold = false) =>
        _props.Children.Add(new TextBlock { Text = text, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) });

    /// <summary>Bảng thuộc tính đối tượng ĐHKK đang chọn. true = đã hiển thị.</summary>
    private bool ShowHvacProps()
    {
        var h = _p.Mep?.Hvac;
        if (h == null || _hvSel.Count == 0) return false;
        _hvSel.RemoveAll(s => HvacEdit.Points(_p, s.Kind, s.Id).Count == 0);
        if (_hvSel.Count == 0) return false;
        _props.Children.Clear();
        if (_hvSel.Count > 1)
        {
            HvText($"Đã chọn {_hvSel.Count} đối tượng ĐHKK", true);
            HvText(string.Join("\n", _hvSel.Take(12).Select(s => "• " + HvacLabel(s.Kind, s.Id))) + (_hvSel.Count > 12 ? "\n…" : ""));
            if (_hvSel.Any(s => s.Kind == HvacElemKind.Duct)) _props.Children.Add(Btn("Hạ ống gió đã chọn qua dầm", HvacDropCommand));
            _props.Children.Add(Btn("Xoá các đối tượng đã chọn", () => DeleteHvacSelected()));
            return true;
        }
        var (k, id) = _hvSel[0];
        string floorTxt(string st) => $"(sàn tầng {MepRouting.StoryElevation(_p, st):0.000} m)";
        switch (k)
        {
            case HvacElemKind.Duct:
            {
                var d = h.Duct(id);
                double z = d.Path.Count > 0 ? d.Path.Max(q => q.Z) : 0, top = z + d.Depth / 2000, floor = MepRouting.StoryElevation(_p, d.Story);
                double v = d.Flow <= 0 ? 0 : d.Round ? HvacSizing.VelocityRound(d.Flow, d.Dia) : HvacSizing.Velocity(d.Flow, d.W, d.H);
                var fits = HvacRouting.RebuildFittings(_p).Where(f => f.DuctId == d.Id).ToList();
                HvText($"Ống gió {d.Id} – {d.SizeText}", true);
                HvText($"Dài {d.Length:0.00} m · tôn {d.SheetArea:0.00} m² dày {HvacSizing.SheetThickness(d.MaxSide):0.##} mm" + (d.Flow > 0 ? $"\nQ = {d.Flow:0} m³/h → v = {v:0.0} m/s" : "")
                    + $"\nPhụ kiện: {fits.Count(f => f.Kind == HvacFittingKind.Cut)} cút ({fits.Count(f => f.Name.StartsWith("Cút 45°"))} cút 45°), {fits.Count(f => f.Kind == HvacFittingKind.Te)} tê, {fits.Count(f => f.Kind == HvacFittingKind.Con)} côn"
                    + $"\nĐỉnh ống cao {top - floor:0.000} m so với sàn {floorTxt(d.Story)}");
                var sys = new ComboBox { Margin = new Thickness(0, 2, 0, 2) };
                foreach (var s in Enum.GetValues<HvacDuctSystem>()) sys.Items.Add(new ComboBoxItem { Content = HvacSizing.SystemName(s), Tag = s, IsSelected = s == d.System });
                sys.SelectionChanged += (_, _) => { if (sys.SelectedItem is ComboBoxItem ci && ci.Tag is HvacDuctSystem hs && hs != d.System) Edit(() => d.System = hs); };
                _props.Children.Add(new TextBlock { Text = "Hệ ống gió", Margin = new Thickness(0, 4, 0, 0) });
                _props.Children.Add(sys);
                var round = new CheckBox { Content = "Ống tròn", IsChecked = d.Round, Margin = new Thickness(0, 4, 0, 2) };
                round.Click += (_, _) => Edit(() =>
                {
                    bool r = round.IsChecked == true;
                    if (r) HvacEdit.Resize(_p, d, 0, 0, true, HvacSizing.RoundSizes.FirstOrDefault(s => s >= HvacSizing.EquivalentDia(d.W, d.H) - 1e-6, HvacSizing.RoundSizes[^1]));
                    else HvacEdit.Resize(_p, d, d.Dia, Math.Max(150, d.Dia / 2));
                });
                _props.Children.Add(round);
                if (d.Round) HvField("Đường kính Ø (mm)", d.Dia.ToString("0"), x => HvacEdit.Resize(_p, d, 0, 0, true, x));
                else
                {
                    HvField("Rộng w (mm)", d.W.ToString("0"), x => HvacEdit.Resize(_p, d, x, d.H));
                    HvField("Cao h (mm) – giữ cao độ đỉnh ống", d.H.ToString("0"), x => HvacEdit.Resize(_p, d, d.W, x));
                }
                HvField("Lưu lượng Q (m³/h) – tự chọn lại cỡ", d.Flow.ToString("0"), x => HvacEdit.SetFlow(_p, d, x));
                HvField("Cao độ đỉnh ống so với sàn (m) – san phẳng cả tuyến", (top - floor).ToString("0.000"), x => HvacEdit.SetTop(_p, d, floor + x));
                _props.Children.Add(Btn("Hạ ống qua dầm", HvacDropCommand));
                _props.Children.Add(Btn("Đưa lại sát trần", () => Edit(() => HvacEdit.SetTop(_p, d, HvacRouting.DuctZ(_p, d.Story, d.Depth) + d.Depth / 2000))));
                _props.Children.Add(Btn("Vẽ lại tuyến", () => { _tool = "hvac-reroute"; _hvReroute = d.Id; _hvacFrom = null; SetStatus("Bấm điểm đầu rồi điểm cuối tuyến mới…"); }));
                break;
            }
            case HvacElemKind.Terminal:
            {
                var t = h.Terminals.First(q => q.Id == id);
                HvText($"{t.Name} (#{t.Id})", true);
                var sup = new CheckBox { Content = "Miệng gió cấp (bỏ chọn = hồi)", IsChecked = t.Supply, Margin = new Thickness(0, 4, 0, 2) };
                sup.Click += (_, _) => Edit(() =>
                {
                    t.Supply = sup.IsChecked == true;
                    if (t.Name.StartsWith("Miệng gió")) t.Name = (t.Supply ? "Miệng gió cấp " : "Miệng gió hồi ") + $"{t.W:0}x{t.H:0}";
                });
                _props.Children.Add(sup);
                HvField("Kích thước w (mm)", t.W.ToString("0"), x => { t.W = Math.Max(100, x); if (t.Name.StartsWith("Miệng gió")) t.Name = (t.Supply ? "Miệng gió cấp " : "Miệng gió hồi ") + $"{t.W:0}x{t.H:0}"; });
                HvField("Kích thước h (mm)", t.H.ToString("0"), x => { t.H = Math.Max(100, x); if (t.Name.StartsWith("Miệng gió")) t.Name = (t.Supply ? "Miệng gió cấp " : "Miệng gió hồi ") + $"{t.W:0}x{t.H:0}"; });
                HvField("Lưu lượng (m³/h)", t.Flow.ToString("0"), x => t.Flow = Math.Max(0, x));
                HvField($"Cao độ so với sàn (m) {floorTxt(t.Story)}", t.Height.ToString("0.000"), x => t.Height = x);
                HvField("X (m)", t.X.ToString("0.000"), x => t.X = x);
                HvField("Y (m)", t.Y.ToString("0.000"), x => t.Y = x);
                break;
            }
            case HvacElemKind.Unit:
            {
                var u = h.Unit(id);
                var pw = HvacLoad.Power(u.CapacityBtu);
                HvText($"{u.Name} (#{u.Id})", true);
                HvText($"{u.CapacityBtu:0} BTU/h = {u.CapacityBtu * HvacLoad.BtuToKw:0.00} kW lạnh"
                    + (u.Kind == HvacUnitKind.DanLanh ? $"\nGió ≈ {HvacSizing.UnitAirflow(u.CapacityBtu):0} m³/h · ống đồng {HvacSizing.Mm(HvacSizing.CopperPair(u.CapacityBtu).Liquid)}/{HvacSizing.Mm(HvacSizing.CopperPair(u.CapacityBtu).Gas)} mm"
                        + $"\nĐiện: {pw.PowerW:0} W, I = {pw.CurrentA:0.0} A (thiết kế {pw.DesignA:0.0} A) → aptomat {pw.BreakerA:0} A, dây {HvacSizing.Mm(pw.WireMm2)} mm²" : ""));
                HvField("Công suất (BTU/h)", u.CapacityBtu.ToString("0"), x => HvacEdit.SetCapacity(_p, u, x));
                HvField("Công suất (kW lạnh)", (u.CapacityBtu * HvacLoad.BtuToKw).ToString("0.00"), x => HvacEdit.SetCapacity(_p, u, x / HvacLoad.BtuToKw));
                HvField($"Cao độ tâm so với sàn (m) {floorTxt(u.Story)}", u.Height.ToString("0.000"), x => { u.Height = x; HvacEdit.Reconnect(_p, u); });
                HvField("Hướng (độ)", u.Angle.ToString("0.#"), x => u.Angle = x);
                HvField("X (m)", u.X.ToString("0.000"), x => HvacEdit.Move(_p, HvacElemKind.Unit, u.Id, x - u.X, 0));
                HvField("Y (m)", u.Y.ToString("0.000"), x => HvacEdit.Move(_p, HvacElemKind.Unit, u.Id, 0, x - u.Y));
                var nm = new TextBox { Text = u.Name };
                nm.LostFocus += (_, _) => { if (nm.Text != u.Name) Edit(() => u.Name = nm.Text); };
                _props.Children.Add(new TextBlock { Text = "Tên", Margin = new Thickness(0, 4, 0, 0) });
                _props.Children.Add(nm);
                break;
            }
            default:
            {
                var q = h.Pipes.First(x => x.Id == id);
                HvText((q.Kind == HvacPipeKind.OngDong ? "Ống đồng gas " : "Ống nước ngưng ") + q.SizeText, true);
                HvText($"Dài {q.Length:0.00} m" + (q.Kind == HvacPipeKind.NuocNgung ? $" · dốc thực {HvacRouting.ActualSlope(q) * 100:0.##} %" : ""));
                if (q.Kind == HvacPipeKind.OngDong)
                {
                    HvField("Ø ống lỏng (mm)", q.LiquidDia.ToString("0.##"), x => q.LiquidDia = x);
                    HvField("Ø ống gas (mm)", q.GasDia.ToString("0.##"), x => q.GasDia = x);
                    _props.Children.Add(Btn("Chọn lại theo công suất dàn lạnh", () => Edit(() =>
                    {
                        var a = h.Unit(q.FromUnit); if (a != null) (q.LiquidDia, q.GasDia) = HvacSizing.CopperPair(a.CapacityBtu);
                    })));
                }
                else
                {
                    HvField("Ø (mm)", q.Dia.ToString("0"), x => q.Dia = Math.Max(16, x));
                    HvField("Độ dốc (%) – tối thiểu 1 %", (q.Slope * 100).ToString("0.##"), x =>
                    {
                        q.Slope = Math.Max(0.01, x / 100);
                        var a = h.Unit(q.FromUnit); if (a != null) HvacEdit.Reconnect(_p, a);
                    });
                }
                break;
            }
        }
        _props.Children.Add(Btn("Xoá", () => DeleteHvacSelected()));
        return true;
    }
}
