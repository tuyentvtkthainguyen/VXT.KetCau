using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Mep;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

/// <summary>[Kiểu Revit] Đặt thiết bị điện nước bằng bắt điểm: bóng thiết bị bám mặt tường gần nhất + kích thước tạm tới đầu tường / ô cửa;
/// thiết bị gắn tường đang chọn: kích thước tạm bấm số để sửa vị trí chính xác (một bước hoàn tác). Lõi: WallAssembly.TempDims.</summary>
public sealed partial class MepWindow
{
    private const double DevW = 0.08;
    private static readonly Brush SnapBlue = MakeBrush(0, 120, 215);

    private static Brush MakeBrush(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }

    private void ClearGhost()
    {
        for (int i = _cv.Children.Count - 1; i >= 0; i--)
            if (_cv.Children[i] is FrameworkElement fe && Equals(fe.Tag, "ghost")) _cv.Children.RemoveAt(i);
    }

    /// <summary>Đoạn kích thước song song tường từ a tới b (m dọc tường), cách tim across (m, có dấu = phía thiết bị).</summary>
    private void DimSeg(WallLine w, double a, double b, double across, string text, Action edit, bool ghost)
    {
        if (b - a < 1e-4) return;
        var p1 = MepRouting.PointOnWall(w, a); var p2 = MepRouting.PointOnWall(w, b);
        double nx = -p1.Uy, ny = p1.Ux;
        var s1 = S(p1.X + nx * across, p1.Y + ny * across); var s2 = S(p2.X + nx * across, p2.Y + ny * across);
        object tag = ghost ? "ghost" : null;
        _cv.Children.Add(new Shapes.Line { X1 = s1.X, Y1 = s1.Y, X2 = s2.X, Y2 = s2.Y, Stroke = SnapBlue, StrokeThickness = 1, IsHitTestVisible = false, Tag = tag });
        var tb = new TextBlock { Text = text, Foreground = SnapBlue, Background = Brushes.White, FontSize = 11, Padding = new Thickness(2, 0, 2, 0), Tag = tag };
        if (edit != null)
        {
            tb.TextDecorations = TextDecorations.Underline; tb.Cursor = Cursors.IBeam; tb.ToolTip = "Bấm để nhập khoảng cách mới (mm)";
            tb.MouseLeftButtonDown += (s, e) => { e.Handled = true; edit(); };
        }
        else tb.IsHitTestVisible = false;
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(tb, (s1.X + s2.X) / 2 - tb.DesiredSize.Width / 2); Canvas.SetTop(tb, (s1.Y + s2.Y) / 2 - tb.DesiredSize.Height - 1);
        _cv.Children.Add(tb);
    }

    private void DrawWallDims(WallLine w, double offset, int side, Action<bool> edit, bool ghost)
    {
        var d = WallAssembly.TempDims(_p, w, DevW, offset);
        double across = side * (MepRouting.WallInfo(_p, w).Total / 2 + 0.3);
        DimSeg(w, d.LeftRef, offset - DevW / 2, across, $"{d.Left * 1000:0}", edit == null ? null : () => edit(true), ghost);
        DimSeg(w, offset + DevW / 2, d.RightRef, across, $"{d.Right * 1000:0}", edit == null ? null : () => edit(false), ghost);
    }

    /// <summary>Di chuột khi đang đặt thiết bị: bóng + kích thước tạm (không cần nhập toạ độ).</summary>
    private void SnapPreview(Point pt)
    {
        ClearGhost();
        if (_tool != "place" || string.IsNullOrEmpty(_placeFamily)) return;
        var (x, y) = W(pt);
        var nw = MepRouting.NearestWall(_p, Story, x, y, 1.0);
        Point sp;
        string hint;
        if (nw.Wall != null)
        {
            var q = MepRouting.PointOnWall(nw.Wall, nw.Offset);
            double t = MepRouting.WallInfo(_p, nw.Wall).Total / 2;
            sp = S(q.X - q.Uy * nw.Side * t, q.Y + q.Ux * nw.Side * t);
            DrawWallDims(nw.Wall, nw.Offset, nw.Side, null, true);
            hint = $"{_placeFamily} · mặt tường #{nw.Wall.Id} (chôn ngầm)";
        }
        else { sp = pt; hint = $"{_placeFamily} · tự do (cách tường > 1 m)"; }
        var r = new Shapes.Rectangle { Width = 10, Height = 10, Stroke = SnapBlue, StrokeThickness = 1.5, Fill = new SolidColorBrush(Color.FromArgb(70, 0, 120, 215)), IsHitTestVisible = false, Tag = "ghost" };
        Canvas.SetLeft(r, sp.X - 5); Canvas.SetTop(r, sp.Y - 5); _cv.Children.Add(r);
        var tb = new TextBlock { Text = hint, Foreground = SnapBlue, FontSize = 11, IsHitTestVisible = false, Tag = "ghost" };
        Canvas.SetLeft(tb, pt.X + 14); Canvas.SetTop(tb, pt.Y + 12); _cv.Children.Add(tb);
    }

    /// <summary>Kích thước tạm sửa được của thiết bị gắn tường đang chọn (gọi cuối Redraw).</summary>
    private void DrawDeviceDims()
    {
        if (_selDevice <= 0) return;
        var d = _p.Mep.Devices.FirstOrDefault(q => q.Id == _selDevice);
        if (d == null || d.WallId <= 0 || d.Story != Story) return;
        var w = _p.Walls.FirstOrDefault(q => q.Id == d.WallId);
        if (w == null || w.IsCurved) return;
        DrawWallDims(w, d.Offset, d.Side, left => EditDeviceDim(d, w, left), false);
    }

    private void EditDeviceDim(MepDevice d, WallLine w, bool left)
    {
        var td = WallAssembly.TempDims(_p, w, DevW, d.Offset);
        string v = DialogUi.InputBox(this, "Kích thước tạm", $"Khoảng cách {(left ? "bên trái" : "bên phải")} từ {d.Family} tới {(left ? "đầu tường / ô cửa trái" : "cuối tường / ô cửa phải")} (mm):",
            ((left ? td.Left : td.Right) * 1000).ToString("0", System.Globalization.CultureInfo.InvariantCulture));
        if (v == null) return;
        double mm = DialogUi.ParseNum(v, double.NaN);
        if (double.IsNaN(mm) || mm < 0) { SetStatus("Số không hợp lệ (≥ 0)."); return; }
        double off = left ? td.LeftRef + mm / 1000 + DevW / 2 : td.RightRef - mm / 1000 - DevW / 2;
        off = Math.Clamp(off, DevW / 2, Math.Max(DevW / 2, w.Chord - DevW / 2));
        Edit(() => { d.Offset = off; MepRouting.RebuildAll(_p); SetStatus($"{d.Family}: cách {(left ? "trái" : "phải")} {mm:0} mm."); });
    }
}
