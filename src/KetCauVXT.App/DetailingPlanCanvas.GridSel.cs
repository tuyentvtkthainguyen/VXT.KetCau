using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Detailing;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>Mặt bằng kết cấu (Triển khai hồ sơ): quét chọn nhiều vị trí cấu kiện (trái → phải: khung bao; phải → trái: khung cắt;
/// Shift + bấm / Shift + kéo: thêm – bớt), Delete xoá cả nhóm; thêm / dời / xoá trục (bước trục SpansX/SpansY tự cập nhật,
/// Core/Detailing/PlanGridEdit.cs).</summary>
public sealed partial class DetailingPlanCanvas
{
    private readonly List<PlanItem> _multi = new();
    private Point? _winFrom;
    private Point _winTo;

    /// <summary>Bấm trái ở công cụ Chọn: bấm trống → bắt đầu khung chọn; Shift + bấm lên cấu kiện → thêm / bớt khỏi nhóm.</summary>
    private bool ChonMultiClick(Point p, PlanItem hit)
    {
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        if (hit != null && shift)
        {
            if (!_multi.Remove(hit)) _multi.Add(hit);
            if (_sel != null && !_multi.Contains(_sel) && _sel != hit) _multi.Add(_sel);
            _sel = null;
            Redraw(); MultiStatus();
            return true;
        }
        if (hit != null) { _multi.Clear(); return false; }
        if (!shift) _multi.Clear();
        _sel = null;
        _winFrom = p; _winTo = p;
        _cv.CaptureMouse();
        Redraw();
        return true;
    }

    private bool WinMove(Point p)
    {
        if (_winFrom == null) return false;
        _winTo = p;
        Redraw();
        return true;
    }

    private void WinUp(MouseButtonEventArgs e)
    {
        if (_winFrom == null) return;
        var a = _winFrom.Value; var b = e.GetPosition(_cv);
        _winFrom = null;
        _cv.ReleaseMouseCapture();
        if (Math.Abs(a.X - b.X) < 3 && Math.Abs(a.Y - b.Y) < 3) { Redraw(); UpdateHint(); return; }
        bool crossing = b.X < a.X;
        var wa = ToW(a); var wb = ToW(b);
        var got = PlanGridEdit.Select(Level, wa.X, wa.Y, wb.X, wb.Y, crossing);
        foreach (var it in got) if (!_multi.Contains(it)) _multi.Add(it);
        Redraw(); MultiStatus();
    }

    private void MultiStatus()
    {
        if (_multi.Count == 0) { UpdateHint(); return; }
        var parts = _multi.GroupBy(i => i.Kind).Select(g => g.Count() + " " + PlanKinds.Title(g.Key).ToLowerInvariant());
        _status.Text = "Đã chọn: " + string.Join(", ", parts) + ". Delete: xoá khỏi mặt bằng · Shift + bấm / kéo: thêm · Esc: bỏ chọn.";
        _status.Foreground = Brushes.Black;
    }

    private void DeleteMulti()
    {
        var lv = Level; if (lv == null || _multi.Count == 0) return;
        _undo.Push(Plan);
        int n = 0;
        foreach (var it in _multi) if (lv.Items.Remove(it)) n++;
        _multi.Clear(); _sel = null;
        _changed(); UpdateHint(); Redraw();
        _status.Text = "Đã xoá " + n + " vị trí cấu kiện khỏi mặt bằng (Ctrl + Z hoàn tác).";
    }

    private void DrawMulti()
    {
        var lv = Level;
        _multi.RemoveAll(i => lv == null || !lv.Items.Contains(i));
        foreach (var it in _multi) DrawItem(it, true);
        if (_winFrom != null)
        {
            var a = _winFrom.Value; var b = _winTo;
            bool crossing = b.X < a.X;
            var r = new Shapes.Rectangle
            {
                Width = Math.Abs(b.X - a.X), Height = Math.Abs(b.Y - a.Y), IsHitTestVisible = false, StrokeThickness = 1,
                Stroke = crossing ? Brushes.ForestGreen : Brushes.RoyalBlue,
                Fill = new SolidColorBrush(crossing ? Color.FromArgb(30, 0x22, 0x8B, 0x22) : Color.FromArgb(30, 0x41, 0x69, 0xE1))
            };
            if (crossing) r.StrokeDashArray = new DoubleCollection { 5, 3 };
            Canvas.SetLeft(r, Math.Min(a.X, b.X)); Canvas.SetTop(r, Math.Min(a.Y, b.Y));
            _cv.Children.Add(r);
        }
    }

    // ------------------------------------------------------------------
    // lưới trục
    // ------------------------------------------------------------------
    private void GridChanged(string msg)
    {
        _sx.Text = Plan.SpansX ?? ""; _sy.Text = Plan.SpansY ?? "";
        _changed(); Redraw(); UpdateHint();
        _status.Text = msg + " Bước trục X: " + (Plan.SpansX ?? "") + " · Y: " + (Plan.SpansY ?? "");
    }

    private void AddGridLine()
    {
        string d = DialogUi.InputBox(_owner, "Thêm trục", "Phương trục (X = trục số đứng 1, 2…; Y = trục chữ ngang A, B…):", "X");
        if (string.IsNullOrWhiteSpace(d)) return;
        bool x = d.Trim().ToUpperInvariant().StartsWith("X");
        var cs = x ? Plan.Xs : Plan.Ys;
        string v = DialogUi.InputBox(_owner, "Thêm trục", "Toạ độ trục mới (m, trục đầu = 0; âm = trước trục đầu):", (cs.Length > 0 ? cs[^1] + 3.6 : 0).ToString("0.###", CultureInfo.InvariantCulture));
        if (v == null) return;
        double c = DialogUi.ParseNum(v, double.NaN);
        if (double.IsNaN(c)) return;
        _undo.Push(Plan);
        if (!PlanGridEdit.Insert(Plan, x, c)) { _status.Text = "Đã có trục tại toạ độ này."; return; }
        GridChanged("Đã thêm trục.");
    }

    /// <summary>Chuột phải gần đường trục (không trúng cấu kiện): menu dời / xoá / chèn trục.</summary>
    private bool GridMenuAt((double X, double Y) w, double tol)
    {
        var g = PlanGridEdit.GridAt(Plan, w.X, w.Y, tol);
        if (g == null) return false;
        var (xDir, idx) = g.Value;
        string name = xDir ? StructuralPlan.NameX(idx) : StructuralPlan.NameY(idx);
        double cur = (xDir ? Plan.Xs : Plan.Ys)[idx];
        var cm = new ContextMenu();
        void Item(string h, Action a) { var mi = new MenuItem { Header = h }; mi.Click += (s, e) => a(); cm.Items.Add(mi); }
        Item("Dời trục " + name + "… (kéo theo cấu kiện)", () => MoveGridLine(xDir, idx, name, cur, true));
        Item("Dời trục " + name + "… (giữ cấu kiện)", () => MoveGridLine(xDir, idx, name, cur, false));
        Item("Thêm trục song song…", () =>
        {
            string v = DialogUi.InputBox(_owner, "Thêm trục", "Khoảng cách từ trục " + name + " (m, âm = phía trước):", "3.6");
            if (v == null) return;
            double d = DialogUi.ParseNum(v, double.NaN);
            if (double.IsNaN(d) || Math.Abs(d) < 1e-3) return;
            _undo.Push(Plan);
            if (!PlanGridEdit.Insert(Plan, xDir, cur + d)) { _status.Text = "Đã có trục tại toạ độ này."; return; }
            GridChanged("Đã thêm trục.");
        });
        cm.Items.Add(new Separator());
        // bong bóng từng đầu + ẩn trục trên mặt bằng đang xem (StructuralPlan.GridStyles)
        var gst = Plan.StyleOf(xDir, idx);
        string sA = xDir ? "dưới" : "trái", sB = xDir ? "trên" : "phải";
        Item("Bong bóng: cả hai đầu" + (gst.BubbleStart && gst.BubbleEnd ? " ✓" : ""), () => { _undo.Push(Plan); Plan.SetBubbles(xDir, idx, true, true); GridChanged("Trục " + name + ": bong bóng hai đầu."); });
        Item("Bong bóng: chỉ đầu " + sA + (gst.BubbleStart && !gst.BubbleEnd ? " ✓" : ""), () => { _undo.Push(Plan); Plan.SetBubbles(xDir, idx, true, false); GridChanged("Trục " + name + ": bong bóng đầu " + sA + "."); });
        Item("Bong bóng: chỉ đầu " + sB + (!gst.BubbleStart && gst.BubbleEnd ? " ✓" : ""), () => { _undo.Push(Plan); Plan.SetBubbles(xDir, idx, false, true); GridChanged("Trục " + name + ": bong bóng đầu " + sB + "."); });
        Item("Không bong bóng" + (!gst.BubbleStart && !gst.BubbleEnd ? " ✓" : ""), () => { _undo.Push(Plan); Plan.SetBubbles(xDir, idx, false, false); GridChanged("Trục " + name + ": không bong bóng."); });
        var lvG = Level;
        if (lvG != null)
            Item("Ẩn trục " + name + " trên mặt bằng " + lvG.Name, () => { _undo.Push(Plan); Plan.SetGridHidden(xDir, idx, lvG.Name, true); GridChanged("Đã ẩn trục " + name + " trên " + lvG.Name + "."); });
        if ((Plan.GridStyles ?? new()).Any(q => q.HiddenLevels.Count > 0))
            Item("Hiện lại mọi trục đã ẩn", () => { _undo.Push(Plan); int k = Plan.ShowAllGrids(); GridChanged("Đã hiện lại " + k + " trục ẩn."); });
        cm.Items.Add(new Separator());
        Item("Xoá trục " + name, () =>
        {
            _undo.Push(Plan);
            if (!PlanGridEdit.Remove(Plan, xDir, idx)) { _status.Text = "Cần giữ ít nhất 2 trục mỗi phương."; return; }
            GridChanged("Đã xoá trục " + name + " (vị trí cấu kiện giữ nguyên, các trục sau đánh số lại).");
        });
        cm.IsOpen = true;
        return true;
    }

    private void MoveGridLine(bool xDir, int idx, string name, double cur, bool drag)
    {
        string v = DialogUi.InputBox(_owner, "Dời trục " + name, "Toạ độ mới của trục " + name + " (m):", cur.ToString("0.###", CultureInfo.InvariantCulture));
        if (v == null) return;
        double c = DialogUi.ParseNum(v, double.NaN);
        if (double.IsNaN(c) || Math.Abs(c - cur) < 1e-6) return;
        _undo.Push(Plan);
        int n = PlanGridEdit.Move(Plan, xDir, idx, c, drag);
        GridChanged("Đã dời trục " + name + (drag ? " – " + n + " vị trí cấu kiện dời theo." : "."));
    }
}
