using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Detailing;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>[Nhập chiều dài cạnh] Khi vẽ biên sàn (Sàn polyline, Sàn theo biên → Vẽ đa giác): gõ thẳng số trên bàn phím (không cần bấm vào ô)
/// → hiện cạnh con trỏ; Enter = cạnh dài đúng số đó theo hướng con trỏ (hướng gần 0° / 90° / 180° / 270° tự bắt thẳng, Shift = vuông góc).
/// Gõ được cả @dx,dy · dài&lt;góc · x,y. "Nhập dãy cạnh…" nhập liền cả biên: 6&lt;0; 3&lt;90; 6&lt;180 … rồi tự khép kín.</summary>
public partial class DetailingPlanCanvas
{
    private string _typed = "";

    private bool TypingTool => _tool == ToolSlabPline || (_tool == ToolSlabOutline && OutlineMode == 2);
    private int TypingCount => _tool == ToolSlabPline ? _plPts.Count : _polyPts.Count;

    /// <summary>Phím số / ký hiệu khi đang vẽ biên sàn; true = đã xử lý.</summary>
    private bool TypedKey(KeyEventArgs e)
    {
        if (!TypingTool) return false;
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        char c = e.Key switch
        {
            >= Key.D0 and <= Key.D9 when !shift => (char)('0' + (e.Key - Key.D0)),
            >= Key.NumPad0 and <= Key.NumPad9 => (char)('0' + (e.Key - Key.NumPad0)),
            Key.D2 when shift => '@',
            Key.OemPeriod or Key.Decimal => '.',
            Key.OemComma when shift => '<',
            Key.OemComma => ',',
            Key.OemMinus or Key.Subtract => '-',
            _ => '\0'
        };
        if (c != '\0') { _typed += c; Redraw(); UpdateHint(); return true; }
        if (_typed.Length == 0) return false;
        switch (e.Key)
        {
            case Key.Back: _typed = _typed[..^1]; Redraw(); UpdateHint(); return true;
            case Key.Escape: _typed = ""; Redraw(); UpdateHint(); return true;
            case Key.Enter: case Key.Space:
                string t = _typed; _typed = "";
                ApplyTyped(t);
                return true;
        }
        return false;
    }

    /// <summary>Hướng con trỏ từ điểm cuối, bắt thẳng về 0° / 90° / 180° / 270° khi lệch ≤ 8°.</summary>
    private (double X, double Y) TypedDir((double X, double Y) last)
    {
        double dx = _mouseW.X - last.X, dy = _mouseW.Y - last.Y;
        if (Math.Abs(dx) + Math.Abs(dy) < 1e-9)
        {
            if (_tool == ToolSlabPline && _plTan is { } t) return t;
            return (1, 0);
        }
        double a = Math.Atan2(dy, dx) * 180 / Math.PI;
        double q = Math.Round(a / 90) * 90;
        if (Math.Abs(a - q) <= 8) { double r = q * Math.PI / 180; return (Math.Round(Math.Cos(r), 12), Math.Round(Math.Sin(r), 12)); }
        return (dx, dy);
    }

    private void ApplyTyped(string t)
    {
        var lv = Level; if (lv == null) return;
        var pts = _tool == ToolSlabPline ? _plPts : _polyPts;
        var last = pts.Count > 0 ? pts[^1] : (0.0, 0.0);
        t = NormTyped(t, pts.Count);
        var q = PlanOutline.ParsePoint(t, last, TypedDir(last));
        if (q == null) { _status.Text = $"Không đọc được \"{t}\" – gõ chiều dài (m) rồi Enter, hoặc @dx,dy · dài<góc · x,y."; Redraw(); return; }
        if (pts.Count == 0 && !t.Contains(',') && !t.Contains('<')) { _status.Text = "Bấm điểm đầu biên sàn trước, rồi gõ chiều dài từng cạnh."; Redraw(); return; }
        if (_tool == ToolSlabPline)
        {
            if (_plPts.Count == 0) { _plPts.Add(q.Value); _first = q.Value; UpdateHint(); Redraw(); }
            else PlTo(q.Value);
        }
        else
        {
            if (_polyPts.Count >= 3 && Math.Abs(q.Value.X - _polyPts[0].X) + Math.Abs(q.Value.Y - _polyPts[0].Y) < 0.005) { PolyClose(lv); return; }
            _polyPts.Add(q.Value); _first = q.Value; UpdateHint(); Redraw();
        }
        double len = Math.Sqrt((q.Value.X - last.X) * (q.Value.X - last.X) + (q.Value.Y - last.Y) * (q.Value.Y - last.Y));
        if (pts.Count > 1) _status.Text = $"Cạnh {Dim(len)} mm → đỉnh ({q.Value.X:0.###}; {q.Value.Y:0.###}). Gõ chiều dài cạnh tiếp rồi Enter; C / Enter (ô trống) khép kín.";
    }

    /// <summary>Đang vẽ (đã có điểm): "3,6" không có @ / &lt; hiểu là 3,6 m (dấu phẩy thập phân kiểu Việt Nam); toạ độ tương đối dùng @dx,dy.</summary>
    private static string NormTyped(string t, int count) =>
        count > 0 && System.Text.RegularExpressions.Regex.IsMatch(t ?? "", @"^-?\d+,\d+$") ? t.Replace(',', '.') : t;

    /// <summary>Ô gõ số cạnh con trỏ + đoạn xem trước đúng chiều dài đang gõ.</summary>
    private void DrawTypedPreview()
    {
        if (!TypingTool || _typed.Length == 0) return;
        var pts = _tool == ToolSlabPline ? _plPts : _polyPts;
        var c = ToS(_mouseW.X, _mouseW.Y);
        var tb = new TextBlock { Text = _typed + (double.TryParse(NormTyped(_typed, pts.Count), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) ? " m" : ""),
            Background = Brushes.LightYellow, Foreground = Brushes.Black, Padding = new Thickness(4, 1, 4, 1), FontSize = 13, IsHitTestVisible = false };
        Canvas.SetLeft(tb, c.X + 14); Canvas.SetTop(tb, c.Y + 12); _cv.Children.Add(tb);
        if (pts.Count == 0) return;
        var last = pts[^1];
        var q = PlanOutline.ParsePoint(NormTyped(_typed, pts.Count), last, TypedDir(last));
        if (q == null) return;
        var a = ToS(last.X, last.Y); var b = ToS(q.Value.X, q.Value.Y);
        _cv.Children.Add(new Shapes.Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = Brushes.OrangeRed, StrokeThickness = 2, IsHitTestVisible = false });
        var d = new Shapes.Ellipse { Width = 7, Height = 7, Fill = Brushes.OrangeRed, IsHitTestVisible = false };
        Canvas.SetLeft(d, b.X - 3.5); Canvas.SetTop(d, b.Y - 3.5); _cv.Children.Add(d);
    }

    /// <summary>Nhập liền cả dãy cạnh (dài&lt;góc / @dx,dy / dài theo hướng cạnh trước), tự khép kín.</summary>
    private void TypedSequence()
    {
        var lv = Level; if (lv == null) return;
        var pts = _tool == ToolSlabPline ? _plPts : _polyPts;
        string start = pts.Count > 0 ? "" : "0,0; ";
        string t = DialogUi.InputBox(_owner, "Nhập dãy cạnh biên sàn",
            "Các cạnh nối tiếp (m), cách nhau dấu chấm phẩy hoặc xuống dòng:\n" +
            "  dài<góc  (góc so với trục X, độ: 0 phải, 90 lên, 180 trái, 270 xuống)\n" +
            "  @dx,dy   (tương đối) · x,y (tuyệt đối)\n" +
            (pts.Count > 0 ? "Bắt đầu từ điểm đã bấm. " : "Mục đầu là điểm đầu (x,y). ") + "Hết dãy → tự khép kín về điểm đầu.\n" +
            "vd. sàn chữ L: 6<0; 3<90; 3<180; 3<90; 3<180", start + "6<0; 3<90; 3<180; 3<90; 3<180");
        if (string.IsNullOrWhiteSpace(t)) return;
        var items = t.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        var seq = new List<(double X, double Y)>(pts);
        (double X, double Y) dir = (1, 0);
        foreach (var it in items)
        {
            var last = seq.Count > 0 ? seq[^1] : (0.0, 0.0);
            var q = seq.Count == 0 ? PlanOutline.ParsePoint(it.StartsWith("@") ? it : it, (0, 0), (1, 0)) : PlanOutline.ParsePoint(it, last, dir);
            if (q == null) { _status.Text = $"Không đọc được cạnh \"{it}\"."; return; }
            if (seq.Count > 0) dir = (q.Value.X - last.X, q.Value.Y - last.Y);
            seq.Add(q.Value);
        }
        if (seq.Count >= 2 && Math.Abs(seq[^1].X - seq[0].X) + Math.Abs(seq[^1].Y - seq[0].Y) < 0.005) seq.RemoveAt(seq.Count - 1);
        var poly = seq.Select(q => new[] { Math.Round(q.X, 4), Math.Round(q.Y, 4) }).ToList();
        if (PlacePolySlab(lv, poly))
        {
            if (_tool == ToolSlabPline) { _plPts.Clear(); _plSegs.Clear(); _plTan = null; _plMid = null; }
            else _polyPts.Clear();
            _first = null; UpdateHint(); Redraw();
        }
    }
}
