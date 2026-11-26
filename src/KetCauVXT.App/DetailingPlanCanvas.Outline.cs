using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Detailing;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>[Sàn theo biên] 3 cách lấy biên dạng sàn: dò vùng kín theo TIM dầm, theo MÉP dầm (trừ nửa bề rộng dầm),
/// hoặc vẽ đa giác bằng cách bấm các đỉnh trên bản vẽ (có bắt điểm) / nhập toạ độ.</summary>
public partial class DetailingPlanCanvas
{
    private readonly List<(double X, double Y)> _polyPts = new();
    private ComboBox _olMode;
    private CheckBox _olArc;
    private (double X, double Y)? _arcMid;   // điểm giữa cung đang vẽ
    private Button _olCoords;
    private StackPanel _olPanel;

    /// <summary>0 = tim dầm, 1 = mép dầm, 2 = vẽ đa giác.</summary>
    private int OutlineMode => Math.Max(0, _olMode?.SelectedIndex ?? 0);

    private UIElement BuildOutlineMode()
    {
        _olMode = new ComboBox { Width = 190, Margin = new Thickness(2, 0, 4, 0), ToolTip = "Cách lấy biên dạng sàn cho công cụ \"Sàn theo biên\"" };
        _olMode.Items.Add("Biên theo tim dầm");
        _olMode.Items.Add("Biên theo mép dầm");
        _olMode.Items.Add("Vẽ đa giác (bấm đỉnh)");
        _olMode.Items.Add("Vùng kín trên nền CAD (xref)");
        _olMode.SelectedIndex = 0;
        _olMode.SelectionChanged += (s, e) => { _polyPts.Clear(); _first = null; UpdateHint(); Redraw(); SyncOutlineMode(); };
        _olCoords = new Button { Content = "Nhập toạ độ…", Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(2, 0, 4, 0),
            ToolTip = "Nhập các đỉnh biên sàn theo toạ độ bản vẽ (m): x1,y1; x2,y2; …" };
        _olCoords.Click += (s, e) => PolyFromText();
        _olPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _olPanel.Children.Add(new TextBlock { Text = "  Biên sàn:", VerticalAlignment = VerticalAlignment.Center });
        _olArc = new CheckBox { Content = "Cạnh cong (3 điểm)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0),
            ToolTip = "Bật: cạnh tiếp theo là cung tròn – bấm 1 điểm trên cung rồi điểm cuối cung (phím C bật / tắt)" };
        _olArc.Checked += (s, e) => { _arcMid = null; UpdateHint(); };
        _olArc.Unchecked += (s, e) => { _arcMid = null; UpdateHint(); Redraw(); };
        _olPanel.Children.Add(_olMode); _olPanel.Children.Add(_olArc); _olPanel.Children.Add(_olCoords);
        SyncOutlineMode();
        return _olPanel;
    }

    private void SyncOutlineMode()
    {
        if (_olPanel == null) return;
        _arcMid = null;
        _olPanel.Visibility = _tool == ToolSlabOutline ? Visibility.Visible : Visibility.Collapsed;
        _olCoords.Visibility = OutlineMode == 2 ? Visibility.Visible : Visibility.Collapsed;
        _olArc.Visibility = OutlineMode == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool _polyDrag;   // bấm-kéo đỉnh đầu → ô chữ nhật

    /// <summary>Bắt điểm khi vẽ biên: tâm cột, đầu dầm, giao trục (PlanSnap) cùng góc mép dầm, giữa dầm, đầu / trên mép dầm – lấy điểm gần nhất.</summary>
    private (double X, double Y) PolySnap(PlanLevel lv, (double X, double Y) w, double tol)
    {
        bool ortho = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var ps = PlanSnap.Snap(Plan, lv, w.X, w.Y, tol, _first, ortho);
        if (ortho) { _snapKind = ps.Kind; return (ps.X, ps.Y); }
        var fs = PlanOutline.FaceSnap(P, lv, w.X, w.Y, tol);
        bool weak = ps.Kind == PlanSnap.KindFree || ps.Kind == PlanSnap.KindGridLine;
        double dp = Math.Sqrt((ps.X - w.X) * (ps.X - w.X) + (ps.Y - w.Y) * (ps.Y - w.Y));
        if (fs is { } f && (weak || Math.Sqrt((f.X - w.X) * (f.X - w.X) + (f.Y - w.Y) * (f.Y - w.Y)) < dp)) { _snapKind = f.Kind; return (f.X, f.Y); }
        _snapKind = ps.Kind;
        return (ps.X, ps.Y);
    }
    private string _snapKind;

    /// <summary>Thả chuột sau khi bấm-kéo từ đỉnh đầu: tạo ô sàn chữ nhật theo 2 góc.</summary>
    private bool PolyUp(Point screen)
    {
        if (!_polyDrag) return false;
        _polyDrag = false;
        var lv = Level;
        if (lv == null || _tool != ToolSlabOutline || OutlineMode != 2 || _polyPts.Count != 1) return false;
        var b = PolySnap(lv, ToW(screen), 12 / _k);
        var a = _polyPts[0];
        if (Math.Abs(b.X - a.X) < 0.3 || Math.Abs(b.Y - a.Y) < 0.3) return false;   // bấm thường → vẽ tiếp đa giác
        _polyPts.Clear(); _first = null;
        var rect = new List<double[]> { new[] { a.X, a.Y }, new[] { b.X, a.Y }, new[] { b.X, b.Y }, new[] { a.X, b.Y } };
        if (!PlacePolySlab(lv, rect)) { UpdateHint(); Redraw(); }
        return true;
    }

    private void PolyClick(PlanLevel lv, (double X, double Y) w, double tol, Point screen, int clicks)
    {
        var sp = PolySnap(lv, w, tol);
        _polyDrag = _polyPts.Count == 0;
        if (_polyPts.Count >= 3)
        {
            var f = ToS(_polyPts[0].X, _polyPts[0].Y);
            if (clicks >= 2 || (Math.Abs(f.X - screen.X) < 10 && Math.Abs(f.Y - screen.Y) < 10)) { PolyClose(lv); return; }
        }
        if (_polyPts.Count > 0 && Math.Abs(_polyPts[^1].X - sp.X) + Math.Abs(_polyPts[^1].Y - sp.Y) < 0.01) { UpdateHint(); return; }
        if (_olArc?.IsChecked == true && _polyPts.Count > 0)
        {
            // cạnh cong: bấm 1 = điểm trên cung, bấm 2 = điểm cuối cung
            if (_arcMid == null) { _arcMid = (sp.X, sp.Y); _polyDrag = false; UpdateHint(); Redraw(); return; }
            _polyPts.AddRange(PlanOutline.Arc3(_polyPts[^1], _arcMid.Value, (sp.X, sp.Y), 16));
            _arcMid = null;
            _first = sp;
            UpdateHint(); Redraw();
            return;
        }
        _polyPts.Add((sp.X, sp.Y));
        _first = sp;   // bắt điểm / Shift vuông góc tính từ đỉnh vừa bấm
        UpdateHint(); Redraw();
    }

    private void PolyKey(Key k)
    {
        var lv = Level;
        if (k == Key.C && _olArc != null) { _olArc.IsChecked = _olArc.IsChecked != true; return; }
        if (_arcMid != null && (k == Key.Back || k == Key.Escape)) { _arcMid = null; UpdateHint(); Redraw(); return; }
        if (k == Key.Enter && lv != null) { PolyClose(lv); return; }
        if (k == Key.Back) { _polyPts.RemoveAt(_polyPts.Count - 1); _first = _polyPts.Count > 0 ? _polyPts[^1] : null; }
        else { _polyPts.Clear(); _first = null; }
        UpdateHint(); Redraw();
    }

    private void PolyClose(PlanLevel lv)
    {
        var poly = _polyPts.Select(q => new[] { Math.Round(q.X, 4), Math.Round(q.Y, 4) }).ToList();
        _polyPts.Clear(); _first = null;
        if (!PlacePolySlab(lv, poly)) { UpdateHint(); Redraw(); }
    }

    private bool PlacePolySlab(PlanLevel lv, List<double[]> poly)
    {
        poly = poly.Count >= 3 ? PlanOutline.Simplify(poly) : poly;
        if (poly.Count < 3 || Math.Abs(PlanOutline.Area(poly)) < 0.05) { _status.Text = "Đa giác cần ít nhất 3 đỉnh không thẳng hàng (diện tích > 0,05 m²)."; return false; }
        if (PlanOutline.SelfIntersects(poly)) { _status.Text = "Đa giác tự cắt – vẽ lại theo thứ tự đi vòng quanh biên sàn."; return false; }
        _undo.Push(Plan);
        string member = _member.SelectedItem as string;
        if (member == NewMember) member = "";
        var it = PlanOutline.PlaceSlab(P, lv, poly, member ?? "");
        Plan.SyncQuantities(P);
        Done(it);
        _status.Text = $"Đã tạo sàn {it.Member} theo đa giác vẽ tay {poly.Count} đỉnh, diện tích {Math.Abs(PlanOutline.Area(poly)).ToString("0.##", CultureInfo.InvariantCulture)} m². Ctrl+Z để hoàn tác.";
        return true;
    }

    private void PolyFromText()
    {
        var lv = Level; if (lv == null) return;
        string t = DialogUi.InputBox(_owner, "Biên sàn theo toạ độ", "Các đỉnh (m, toạ độ bản vẽ), cách nhau dấu chấm phẩy hoặc xuống dòng:\nvd. 0,0; 6,0; 6,3; 3,3; 3,6; 0,6", "");
        if (string.IsNullOrWhiteSpace(t)) return;
        var poly = PlanOutline.ParseCoords(t);
        if (poly == null) { _status.Text = "Không đọc được toạ độ – nhập dạng x,y; x,y; … (dấu thập phân là dấu chấm)."; return; }
        PlacePolySlab(lv, poly);
    }

    /// <summary>[Vùng kín trên nền CAD] bấm trong vùng bao bởi nét của mặt bằng nền (xref kiến trúc / kết cấu) → biên sàn.</summary>
    private bool PickCadRegion(PlanLevel lv, (double X, double Y) w)
    {
        var u = ArchUnderlay.Load(lv, Dir);
        if (u == null || u.Lines.Count == 0) { _status.Text = "Tầng chưa gắn nền CAD (nút \"Ánh xạ KT (xref)…\") – hoặc chọn cách \"Vẽ đa giác\"."; return false; }
        double dx = lv.UnderlayDx, dy = lv.UnderlayDy;
        var poly = PlanOutline.RegionOf(u.Lines.Select(q => (q.X1 + dx, q.Y1 + dy, q.X2 + dx, q.Y2 + dy)), w.X, w.Y, 60);
        if (poly == null) { _status.Text = "Không thấy đường bao kín quanh điểm bấm trên nền CAD (nét hở / không giao nhau) – dùng \"Vẽ đa giác\" để vẽ tay."; return false; }
        return PlacePolySlab(lv, poly);
    }

    private void DrawPolyPreview()
    {
        if (_tool != ToolSlabOutline || _polyPts.Count == 0) return;
        var pl = new Shapes.Polyline { Stroke = Brushes.DodgerBlue, StrokeThickness = 1.6, IsHitTestVisible = false, StrokeDashArray = new DoubleCollection { 5, 3 } };
        foreach (var q in _polyPts) pl.Points.Add(ToS(q.X, q.Y));
        if (_arcMid is { } am)
            foreach (var q in PlanOutline.Arc3(_polyPts[^1], am, _mouseW, 16)) pl.Points.Add(ToS(q.X, q.Y));
        else pl.Points.Add(ToS(_mouseW.X, _mouseW.Y));
        _cv.Children.Add(pl);
        foreach (var q in _polyPts)
        {
            var c = ToS(q.X, q.Y);
            var r = new Shapes.Rectangle { Width = 7, Height = 7, Fill = Brushes.DodgerBlue, IsHitTestVisible = false };
            Canvas.SetLeft(r, c.X - 3.5); Canvas.SetTop(r, c.Y - 3.5); _cv.Children.Add(r);
        }
        var l = _polyPts[^1];
        double len = Math.Sqrt((_mouseW.X - l.X) * (_mouseW.X - l.X) + (_mouseW.Y - l.Y) * (_mouseW.Y - l.Y));
        Text((_mouseW.X + l.X) / 2, (_mouseW.Y + l.Y) / 2 + 0.15, Dim(len), Brushes.DodgerBlue, 11);
        if (!string.IsNullOrEmpty(_snapKind) && _snapKind != PlanSnap.KindFree) Text(_mouseW.X + 0.2, _mouseW.Y - 0.2, _snapKind, Brushes.DarkOrange, 10, false);
    }
}
