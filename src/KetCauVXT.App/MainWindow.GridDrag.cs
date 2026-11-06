using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// KÉO TRỤC TRỰC TIẾP TRÊN MẶT BẰNG (chế độ Chọn): bấm giữ bong bóng trục rồi kéo – kích thước tạm tới trục kề,
// bắt 10 mm; thả chuột → dời trục kéo theo cột, đầu dầm, góc sàn, đầu tường nằm trên trục (giữ Ctrl khi thả: chỉ dời trục).
// Chuột phải lên bong bóng: bong bóng hai đầu / một đầu / không, ẩn trục ở tầng đang xem.
// Lõi: Model/InteractionOps.cs (GridDragMath, GridDisplay), Model/GridLevelOps.cs (GridOps.MoveGrid).
// =====================================================================
public partial class MainWindow
{
    private GridLine _gdGrid;
    private bool _gdDrag;
    private double _gdCoord;
    private const double GridBubblePx = 11;

    /// <summary>Tâm bong bóng (màn hình) của các trục đang hiện – cùng công thức vẽ trong RedrawPlan.</summary>
    private IEnumerable<(GridLine G, Point C)> GridBubbleCenters()
    {
        if (_p == null) yield break;
        string st = CurrentStory == AllStories ? null : CurrentStory;
        var gxs = _p.GridsX.Where(g => GridDisplay.Visible(g, st)).ToList();
        var gys = _p.GridsY.Where(g => GridDisplay.Visible(g, st)).ToList();
        double gyMin = gys.Count > 0 ? gys.First().Coord : 0, gyMax = gys.Count > 0 ? gys.Last().Coord : 10;
        double gxMin = gxs.Count > 0 ? gxs.First().Coord : 0, gxMax = gxs.Count > 0 ? gxs.Last().Coord : 10;
        const double ext = 1.2;
        foreach (var g in gxs)
        {
            var a = W2S(g.Coord, gyMin - ext); var b = W2S(g.Coord, gyMax + ext);
            if (g.BubbleStart) yield return (g, new Point(a.X, a.Y + GridBubblePx));
            if (g.BubbleEnd) yield return (g, new Point(b.X, b.Y - GridBubblePx));
        }
        foreach (var g in gys)
        {
            var a = W2S(gxMin - ext, g.Coord); var b = W2S(gxMax + ext, g.Coord);
            if (g.BubbleStart) yield return (g, new Point(a.X - GridBubblePx, a.Y));
            if (g.BubbleEnd) yield return (g, new Point(b.X + GridBubblePx, b.Y));
        }
    }

    private GridLine GridAtBubble(Point s) =>
        GridBubbleCenters().Where(t => (t.C - s).Length <= GridBubblePx + 2).Select(t => t.G).FirstOrDefault();

    /// <summary>Bấm giữ bong bóng trục ở chế độ Chọn → bắt đầu kéo trục.</summary>
    private bool GridDragDown(Point s)
    {
        if (!GripsEnabled) return false;
        var g = GridAtBubble(s);
        if (g == null && _p != null && _scale > 1e-9)
        {
            // nắm giữa đường trục: phần đuôi ngoài khung lưới, hoặc bất kỳ đâu khi giữ Alt
            var (wx, wy) = S2W(s);
            string stG = CurrentStory == AllStories ? null : CurrentStory;
            bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
            g = GridDragMath.HitGridLine(_p, stG, wx, wy, 5 / _scale, 1.2, alt);
        }
        if (g == null) return false;
        _gdGrid = g; _gdDrag = true; _gdCoord = g.Coord;
        planHost.Focus();
        planHost.CaptureMouse();
        SetStatus($"Kéo trục {g.Name}: thả chuột tại vị trí mới (bắt 10 mm). Giữ Ctrl khi thả: chỉ dời trục, không kéo theo cấu kiện.");
        return true;
    }

    private bool GridDragMove(Point s)
    {
        if (!_gdDrag || _gdGrid == null) return false;
        var (x, y) = S2W(s);
        _gdCoord = GridDragMath.Snap(_gdGrid.Dir == GridDir.X ? x : y);
        DrawGridDragOverlay();
        return true;
    }

    private bool GridDragUp(Point s)
    {
        if (!_gdDrag) return false;
        _gdDrag = false;
        planHost.ReleaseMouseCapture();
        _panning = false;
        var g = _gdGrid; _gdGrid = null;
        if (g == null || _p == null) return true;
        if (Math.Abs(_gdCoord - g.Coord) < 1e-6) { DrawHover(); SetStatus("Trục: chưa kéo."); return true; }
        if (_busy) { SetStatus("Chương trình đang tính toán – chờ xong rồi sửa mô hình."); DrawHover(); return true; }
        bool members = (Keyboard.Modifiers & ModifierKeys.Control) == 0;
        string snap = Snapshot();
        double old = g.Coord;
        var (c, n) = GridDragMath.Drop(_p, g, _gdCoord, members);
        AfterModelEdit($"Đã dời trục {g.Name}: {old:0.###} → {c:0.###} m" + (members ? $", kéo theo {n} đối tượng." : " (không kéo theo cấu kiện)."), snap);
        return true;
    }

    /// <summary>Đường trục đang kéo (nét đứt) + kích thước tạm tới trục kề.</summary>
    private void DrawGridDragOverlay()
    {
        planOverlay.Children.Clear();
        if (_gdGrid == null || _p == null) return;
        var br = Brushes.DodgerBlue;
        var gx = _p.GridsX.ToList(); var gy = _p.GridsY.ToList();
        double yMin = (gy.Count > 0 ? gy.First().Coord : 0) - 1.2, yMax = (gy.Count > 0 ? gy.Last().Coord : 10) + 1.2;
        double xMin = (gx.Count > 0 ? gx.First().Coord : 0) - 1.2, xMax = (gx.Count > 0 ? gx.Last().Coord : 10) + 1.2;
        bool isX = _gdGrid.Dir == GridDir.X;
        Point a = isX ? W2S(_gdCoord, yMin) : W2S(xMin, _gdCoord), b = isX ? W2S(_gdCoord, yMax) : W2S(xMax, _gdCoord);
        var l = CLine(planOverlay, a, b, br, 1.5, true); l.IsHitTestVisible = false;
        var (toPrev, toNext) = GridDragMath.TempDims(_p, _gdGrid, _gdCoord);
        var (prev, next) = GridDragMath.Neighbours(_p, _gdGrid);
        void Dim(double c0, double c1, double d)
        {
            if (double.IsNaN(d)) return;
            double mid = (c0 + c1) / 2;
            var at = isX ? W2S(mid, yMax - 0.6) : W2S(xMax - 0.6, mid);
            var p0 = isX ? W2S(c0, yMax - 0.6) : W2S(xMax - 0.6, c0);
            var p1 = isX ? W2S(c1, yMax - 0.6) : W2S(xMax - 0.6, c1);
            var dl = CLine(planOverlay, p0, p1, br, 1, false); dl.IsHitTestVisible = false;
            var tb = new TextBlock { Text = (d * 1000).ToString("0"), Foreground = br, Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), FontSize = 12, IsHitTestVisible = false };
            Canvas.SetLeft(tb, at.X + 3); Canvas.SetTop(tb, at.Y - 16);
            planOverlay.Children.Add(tb);
        }
        if (prev != null) Dim(prev.Coord, _gdCoord, toPrev);
        if (next != null) Dim(_gdCoord, next.Coord, toNext);
        SetStatus($"Kéo trục {_gdGrid.Name}: {(isX ? "X" : "Y")} = {_gdCoord:0.000} m"
                  + (prev != null ? $" · cách {prev.Name} {toPrev * 1000:0} mm" : "") + (next != null ? $" · cách {next.Name} {toNext * 1000:0} mm" : ""));
    }

    /// <summary>Chuột phải lên bong bóng trục: menu bong bóng / ẩn trên tầng. true = đã mở menu.</summary>
    private bool GridBubbleContextMenu(Point s)
    {
        if (_p == null) return false;
        var g = GridAtBubble(s);
        if (g == null) return false;
        var m = new ContextMenu();
        void Add(string h, Action a, bool chk = false)
        {
            var mi = new MenuItem { Header = h, IsCheckable = chk, IsChecked = chk };
            mi.Click += (o, e) => { try { a(); } catch (Exception ex) { ShowError(h, ex); } };
            m.Items.Add(mi);
        }
        var cur = GridDisplay.Mode(g);
        for (int i = 0; i < GridDisplay.BubbleTitles.Length; i++)
        {
            var md = (GridBubbleMode)i;
            Add("Bong bóng: " + GridDisplay.BubbleTitles[i], () =>
            {
                string snap = Snapshot();
                GridDisplay.SetMode(g, md);
                AfterModelEdit($"Trục {g.Name}: bong bóng {GridDisplay.BubbleTitles[(int)md].ToLowerInvariant()}.", snap);
            }, md == cur);
        }
        m.Items.Add(new Separator());
        string st = CurrentStory == AllStories ? null : CurrentStory;
        if (st != null)
            Add($"Ẩn trục {g.Name} trên tầng {st}", () =>
            {
                string snap = Snapshot();
                GridDisplay.SetHidden(g, st, true);
                AfterModelEdit($"Đã ẩn trục {g.Name} trên tầng {st} (hiện lại: chuột phải bong bóng trục bất kỳ → Hiện lại mọi trục đã ẩn trên tầng này).", snap);
            });
        Add("Hiện lại mọi trục đã ẩn" + (st != null ? " trên tầng này" : ""), () =>
        {
            string snap = Snapshot();
            foreach (var x in _p.Grids) { if (st == null) x.HiddenStories?.Clear(); else GridDisplay.SetHidden(x, st, false); }
            AfterModelEdit("Đã hiện lại trục.", snap);
        });
        m.PlacementTarget = planHost;
        m.IsOpen = true;
        return true;
    }
}
