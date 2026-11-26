using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Detailing;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>[Sàn polyline] Vẽ biên dạng sàn như lệnh PLINE của AutoCAD: đoạn thẳng (L), cung tiếp tuyến (A), cung 3 điểm (S)
/// nối liên tiếp; nhập điểm bằng bàn phím ("@dx,dy", "dài&lt;góc", "x,y", hoặc một số = dài theo hướng con trỏ); U lùi một đoạn,
/// C / Enter khép kín (đang ở chế độ cung → cung tiếp tuyến về điểm đầu), Esc huỷ. Có bắt điểm tâm cột, giao trục, mép / giữa dầm.</summary>
public partial class DetailingPlanCanvas
{
    public const string ToolSlabPline = "SanPline";

    private readonly List<(double X, double Y)> _plPts = new();
    private readonly List<(int Start, (double X, double Y)? Tan)> _plSegs = new();   // mỗi đoạn: chỉ số điểm đầu đoạn, tiếp tuyến trước đoạn
    private (double X, double Y)? _plTan;     // hướng tiếp tuyến tại điểm cuối
    private (double X, double Y)? _plMid;     // cung 3 điểm: điểm trên cung
    private char _plMode = 'L';
    private StackPanel _plPanel;
    private RadioButton _plL, _plA, _plS;
    private TextBox _plInput;
    private CheckBox _plDrop;

    private UIElement BuildPlinePanel()
    {
        _plPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _plPanel.Children.Add(new TextBlock { Text = "  Polyline:", VerticalAlignment = VerticalAlignment.Center });
        RadioButton R(string t, char m, string tip)
        {
            var r = new RadioButton { Content = t, GroupName = "plmode", Margin = new Thickness(4, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = tip };
            r.Checked += (s, e) => { _plMode = m; _plMid = null; UpdateHint(); Redraw(); _cv.Focus(); };
            _plPanel.Children.Add(r);
            return r;
        }
        _plL = R("Thẳng (L)", 'L', "Đoạn thẳng – bấm điểm cuối đoạn");
        _plA = R("Cung tiếp tuyến (A)", 'A', "Cung tiếp tuyến với đoạn trước – bấm điểm cuối cung (như PLINE → Arc của AutoCAD)");
        _plS = R("Cung 3 điểm (S)", 'S', "Cung qua 3 điểm – bấm 1 điểm trên cung rồi điểm cuối cung");
        _plL.IsChecked = true;
        _plInput = new TextBox { Width = 110, Margin = new Thickness(6, 0, 2, 0), ToolTip = "Nhập điểm rồi Enter: @dx,dy (tương đối) · dài<góc (độ) · x,y (tuyệt đối) · một số = dài theo hướng con trỏ (m)" };
        _plInput.KeyDown += (s, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            PlTyped(_plInput.Text);
        };
        _plPanel.Children.Add(new TextBlock { Text = " Nhập:", VerticalAlignment = VerticalAlignment.Center });
        _plPanel.Children.Add(_plInput);
        var bU = new Button { Content = "Lùi (U)", Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(2, 0, 2, 0) }; bU.Click += (s, e) => PlUndo();
        var bC = new Button { Content = "Khép kín (C)", Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(2, 0, 2, 0) }; bC.Click += (s, e) => PlClose();
        var bQ = new Button { Content = "Nhập dãy cạnh…", Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(2, 0, 2, 0), ToolTip = "Nhập chiều dài các cạnh biên sàn liền một lần: 6<0; 3<90; 6<180 …" };
        bQ.Click += (s, e) => TypedSequence();
        _plPanel.Children.Add(bU); _plPanel.Children.Add(bC); _plPanel.Children.Add(bQ);
        _plDrop = new CheckBox { Content = "Sàn hạ cốt 50", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 2, 0), ToolTip = "Ô sàn vẽ ra là sàn hạ cốt 50 mm (sửa độ hạ bằng công cụ Sàn hạ cốt)" };
        _plPanel.Children.Add(_plDrop);
        _plPanel.Visibility = Visibility.Collapsed;
        return _plPanel;
    }

    private void SyncPlinePanel()
    {
        if (_plPanel != null) _plPanel.Visibility = _tool == ToolSlabPline ? Visibility.Visible : Visibility.Collapsed;
        _plPts.Clear(); _plSegs.Clear(); _plTan = null; _plMid = null;
    }

    private string PlineHint()
    {
        string m = _plMode switch { 'A' => "cung tiếp tuyến", 'S' => "cung 3 điểm", _ => "đoạn thẳng" };
        if (_plPts.Count == 0) return "Sàn polyline: bấm điểm đầu biên sàn (bắt tâm cột, giao trục, mép / giữa dầm) hoặc gõ toạ độ x,y rồi Enter; \"Nhập dãy cạnh…\" nhập cả biên bằng chiều dài cạnh.";
        if (_plMode == 'S' && _plMid != null) return "Cung 3 điểm: bấm điểm CUỐI cung (Backspace bỏ điểm trên cung).";
        if (_typed.Length > 0) return $"Đang gõ: {_typed} – Enter / Space để vẽ cạnh dài đúng số này theo hướng con trỏ (gần ngang / đứng tự bắt thẳng); Backspace sửa, Esc bỏ.";
        return $"Sàn polyline ({_plSegs.Count} đoạn) – đang vẽ {m}: bấm điểm tiếp, hoặc GÕ CHIỀU DÀI cạnh (m) rồi Enter theo hướng con trỏ · @dx,dy · dài<góc. Phím L thẳng · A cung tiếp tuyến · S cung 3 điểm · U lùi · C / Enter / bấm điểm đầu khép kín · Esc huỷ."
               + (_plMode == 'S' ? " (cung 3 điểm: bấm điểm trên cung trước)" : "");
    }

    private void PlClick(PlanLevel lv, (double X, double Y) w, double tol, Point screen, int clicks)
    {
        var sp = PolySnap(lv, w, tol);
        if (_plPts.Count == 0) { _plPts.Add(sp); _plSegs.Clear(); _plTan = null; _first = sp; UpdateHint(); Redraw(); return; }
        if (_plPts.Count >= 3 && _plMid == null)
        {
            var f = ToS(_plPts[0].X, _plPts[0].Y);
            if (clicks >= 2 || (Math.Abs(f.X - screen.X) < 10 && Math.Abs(f.Y - screen.Y) < 10)) { PlClose(); return; }
        }
        PlTo(sp);
    }

    private void PlTyped(string t)
    {
        var lv = Level; if (lv == null) return;
        var last = _plPts.Count > 0 ? _plPts[^1] : (0.0, 0.0);
        var dir = (_mouseW.X - last.Item1, _mouseW.Y - last.Item2);
        if (Math.Abs(dir.Item1) + Math.Abs(dir.Item2) < 1e-9 && _plTan is { } tt) dir = tt;
        var q = PlanOutline.ParsePoint(t, last, dir);
        if (q == null) { _status.Text = "Không đọc được điểm – nhập @dx,dy · dài<góc · x,y · hoặc một số (m, dấu chấm thập phân)."; return; }
        _plInput.Text = "";
        if (_plPts.Count == 0) { _plPts.Add(q.Value); _first = q.Value; UpdateHint(); Redraw(); }
        else PlTo(q.Value);
        _cv.Focus();
    }

    private void PlTo((double X, double Y) p)
    {
        var last = _plPts[^1];
        if (Math.Abs(last.X - p.X) + Math.Abs(last.Y - p.Y) < 0.005) return;
        List<(double X, double Y)> seg; (double X, double Y) tan;
        char mode = _plMode == 'A' && _plTan == null ? 'S' : _plMode;   // đoạn đầu chưa có tiếp tuyến → cung 3 điểm
        if (mode == 'S')
        {
            if (_plMid == null) { _plMid = p; UpdateHint(); Redraw(); return; }
            seg = PlanOutline.Arc3(last, _plMid.Value, p, 16);
            var b = seg.Count >= 2 ? seg[^2] : last;
            tan = (p.X - b.X, p.Y - b.Y);
            _plMid = null;
        }
        else if (mode == 'A') (seg, tan) = PlanOutline.TangentArc(last, _plTan.Value, p, 16);
        else { seg = new() { p }; tan = (p.X - last.X, p.Y - last.Y); }
        _plSegs.Add((_plPts.Count, _plTan));
        _plPts.AddRange(seg);
        _plTan = tan;
        _first = p;
        UpdateHint(); Redraw();
    }

    private void PlUndo()
    {
        if (_plMid != null) { _plMid = null; UpdateHint(); Redraw(); return; }
        if (_plSegs.Count == 0) { _plPts.Clear(); _plTan = null; _first = null; UpdateHint(); Redraw(); return; }
        var (start, tan) = _plSegs[^1];
        _plSegs.RemoveAt(_plSegs.Count - 1);
        _plPts.RemoveRange(start, _plPts.Count - start);
        _plTan = tan;
        _first = _plPts.Count > 0 ? _plPts[^1] : null;
        UpdateHint(); Redraw();
    }

    private void PlClose()
    {
        var lv = Level;
        if (lv == null || _plPts.Count < 2) return;
        var pts = new List<(double X, double Y)>(_plPts);
        var a = pts[0];
        if (_plMode == 'A' && _plTan is { } t)
        {
            // khép kín bằng cung tiếp tuyến về điểm đầu
            var (arc, _) = PlanOutline.TangentArc(pts[^1], t, a, 16);
            pts.AddRange(arc);
        }
        if (pts.Count > 1 && Math.Abs(pts[^1].X - a.X) + Math.Abs(pts[^1].Y - a.Y) < 0.005) pts.RemoveAt(pts.Count - 1);
        var poly = pts.Select(q => new[] { Math.Round(q.X, 4), Math.Round(q.Y, 4) }).ToList();
        int before = lv.Items.Count;
        if (!PlacePolySlab(lv, poly)) { UpdateHint(); Redraw(); return; }
        if (_plDrop?.IsChecked == true && lv.Items.Count > before && lv.Items[^1].Kind == PlanKinds.Slab)
        {
            lv.Items[^1].Drop = 0.05;
            _changed(); Redraw();
            _status.Text += " Sàn hạ cốt 50 mm.";
        }
        _plPts.Clear(); _plSegs.Clear(); _plTan = null; _plMid = null; _first = null;
        UpdateHint(); Redraw();
    }

    /// <summary>Phím tắt khi vẽ polyline; true = đã xử lý.</summary>
    private bool PlKey(Key k)
    {
        switch (k)
        {
            case Key.L: _plL.IsChecked = true; return true;
            case Key.A: _plA.IsChecked = true; return true;
            case Key.S: _plS.IsChecked = true; return true;
            case Key.U: case Key.Back: PlUndo(); return true;
            case Key.C: case Key.Enter: PlClose(); return true;
            case Key.Escape:
                if (_plPts.Count == 0 && _plMid == null) return false;
                _plPts.Clear(); _plSegs.Clear(); _plTan = null; _plMid = null; _first = null; UpdateHint(); Redraw(); return true;
        }
        return false;
    }

    private void DrawPlinePreview()
    {
        if (_tool != ToolSlabPline || _plPts.Count == 0) return;
        var done = new Shapes.Polyline { Stroke = Brushes.DodgerBlue, StrokeThickness = 1.8, IsHitTestVisible = false };
        foreach (var q in _plPts) done.Points.Add(ToS(q.X, q.Y));
        _cv.Children.Add(done);
        var last = _plPts[^1];
        var rub = new Shapes.Polyline { Stroke = Brushes.DodgerBlue, StrokeThickness = 1.2, IsHitTestVisible = false, StrokeDashArray = new DoubleCollection { 5, 3 } };
        rub.Points.Add(ToS(last.X, last.Y));
        char mode = _plMode == 'A' && _plTan == null ? 'S' : _plMode;
        if (mode == 'A') foreach (var q in PlanOutline.TangentArc(last, _plTan.Value, _mouseW, 24).Pts) rub.Points.Add(ToS(q.X, q.Y));
        else if (mode == 'S' && _plMid is { } m) foreach (var q in PlanOutline.Arc3(last, m, _mouseW, 24)) rub.Points.Add(ToS(q.X, q.Y));
        else rub.Points.Add(ToS(_mouseW.X, _mouseW.Y));
        _cv.Children.Add(rub);
        if (_plMid is { } mm)
        {
            var c = ToS(mm.X, mm.Y);
            var d = new Shapes.Ellipse { Width = 7, Height = 7, Stroke = Brushes.DodgerBlue, StrokeThickness = 1.2, IsHitTestVisible = false };
            Canvas.SetLeft(d, c.X - 3.5); Canvas.SetTop(d, c.Y - 3.5); _cv.Children.Add(d);
        }
        // đỉnh đầu (bấm lại để khép kín)
        var f = ToS(_plPts[0].X, _plPts[0].Y);
        var r = new Shapes.Rectangle { Width = 9, Height = 9, Stroke = Brushes.DodgerBlue, StrokeThickness = 1.4, IsHitTestVisible = false };
        Canvas.SetLeft(r, f.X - 4.5); Canvas.SetTop(r, f.Y - 4.5); _cv.Children.Add(r);
        double len = Math.Sqrt((_mouseW.X - last.X) * (_mouseW.X - last.X) + (_mouseW.Y - last.Y) * (_mouseW.Y - last.Y));
        string lab = (mode == 'L' ? "" : mode == 'A' ? "cung · dây " : "cung 3đ · dây ") + Dim(len);
        Text((_mouseW.X + last.X) / 2, (_mouseW.Y + last.Y) / 2 + 0.15, lab, Brushes.DodgerBlue, 11);
        if (!string.IsNullOrEmpty(_snapKind) && _snapKind != PlanSnap.KindFree) Text(_mouseW.X + 0.2, _mouseW.Y - 0.2, _snapKind, Brushes.DarkOrange, 10, false);
    }
}
