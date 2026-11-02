using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Kiểu căn hàng các đối tượng đang chọn.</summary>
public enum AlignMode { Left, Right, Top, Bottom, CenterH, CenterV }

/// <summary>Kết quả bắt điểm khi kéo: độ dịch bổ sung và đường gióng (NaN = không có).</summary>
public struct SnapResult
{
    public double Dx, Dy;
    public double GuideX, GuideY;
    public bool SnappedX => !double.IsNaN(GuideX);
    public bool SnappedY => !double.IsNaN(GuideY);
}

/// <summary>Toán học dàn trang thủ công (mm giấy, y hướng lên): phạm vi đối tượng, bắt lưới/bắt đối tượng,
/// căn hàng, phân bố đều, đổi cỡ theo tay nắm, thứ tự trên dưới, chọn bằng khung.</summary>
public static class SheetLayoutMath
{
    /// <summary>Các bước lưới cho phép (mm).</summary>
    public static readonly double[] GridSteps = { 1, 2.5, 5, 10 };
    /// <summary>Danh sách tỷ lệ chọn nhanh cho khung nhìn.</summary>
    public static readonly double[] ScaleList = { 20, 25, 30, 50, 75, 100, 125, 150, 200 };
    /// <summary>Khoảng cách dòng chữ (× chiều cao chữ).</summary>
    public const double LineSpacing = 1.5;

    // ==================================================================
    // Phạm vi
    // ==================================================================
    public static Rect2 Bounds(SheetView v) => new(v.X, v.Y, v.X + v.W, v.Y + v.H);

    public static Rect2 Bounds(SheetItem it)
    {
        if (it == null) return Rect2.Empty;
        switch (it.Kind)
        {
            case SheetItemKind.Rect:
            case SheetItemKind.Image:
            case SheetItemKind.Table:
                return new Rect2(it.X, it.Y, it.X + Math.Max(0, it.W), it.Y + Math.Max(0, it.H));
            case SheetItemKind.Cloud:
                return new Rect2(it.X, it.Y, it.X + Math.Max(0, it.W), it.Y + Math.Max(0, it.H)).Inflate(Math.Max(0.5, it.ArcLength / 2));
            case SheetItemKind.Circle:
                return new Rect2(it.X - it.R, it.Y - it.R, it.X + it.R, it.Y + it.R);
        }
        var e = SheetBuilder.ItemFigure(it).Extents;
        if (e.IsEmpty)
        {
            double h = Math.Max(1, it.TextHeight);
            if (it.Pts != null && it.Pts.Count >= 2) return new Rect2(it.Pts[0], it.Pts[1], it.Pts[0], it.Pts[1]).Inflate(h / 2);
            return new Rect2(it.X, it.Y - h, it.X + h, it.Y);
        }
        return e;
    }

    public static Rect2 Union(IEnumerable<Rect2> rs)
    {
        var u = Rect2.Empty;
        foreach (var r in rs) u = u.Union(r);
        return u;
    }

    // ==================================================================
    // Dịch chuyển
    // ==================================================================
    public static void Move(SheetItem it, double dx, double dy)
    {
        it.X += dx; it.Y += dy;
        if (it.Pts != null)
            for (int i = 0; i + 1 < it.Pts.Count; i += 2) { it.Pts[i] += dx; it.Pts[i + 1] += dy; }
    }

    public static void Move(SheetView v, double dx, double dy) { v.X += dx; v.Y += dy; }

    public static void Move(object o, double dx, double dy)
    {
        if (o is SheetItem it) Move(it, dx, dy);
        else if (o is SheetView v) Move(v, dx, dy);
    }

    public static Rect2 Bounds(object o) => o is SheetItem it ? Bounds(it) : o is SheetView v ? Bounds(v) : Rect2.Empty;
    public static bool IsLocked(object o) => o is SheetItem it ? it.Locked : o is SheetView v && v.Locked;

    // ==================================================================
    // Bắt điểm
    // ==================================================================
    public static double SnapGrid(double v, double step) => step > 0 ? Math.Round(v / step) * step : v;

    private static double BestDelta(double[] mine, IEnumerable<double> targets, double tol, out double guide)
    {
        guide = double.NaN;
        double best = double.PositiveInfinity, bd = 0;
        foreach (double t in targets)
            foreach (double m in mine)
            {
                double d = t - m;
                if (Math.Abs(d) <= tol && Math.Abs(d) < Math.Abs(best)) { best = d; bd = d; guide = t; }
            }
        return double.IsInfinity(best) ? 0 : bd;
    }

    private static IEnumerable<double> Xs(IEnumerable<Rect2> rs) { foreach (var r in rs) if (!r.IsEmpty) { yield return r.X1; yield return r.Cx; yield return r.X2; } }
    private static IEnumerable<double> Ys(IEnumerable<Rect2> rs) { foreach (var r in rs) if (!r.IsEmpty) { yield return r.Y1; yield return r.Cy; yield return r.Y2; } }

    /// <summary>Bắt điểm cho đối tượng đang kéo có phạm vi m: ưu tiên mép/tâm đối tượng khác (trong tol),
    /// không có thì bắt mép trái/dưới vào lưới (grid &gt; 0).</summary>
    public static SnapResult SnapMove(Rect2 m, IList<Rect2> targets, double tol, double grid)
    {
        var res = new SnapResult { GuideX = double.NaN, GuideY = double.NaN };
        if (m.IsEmpty) return res;
        targets ??= new List<Rect2>();
        res.Dx = BestDelta(new[] { m.X1, m.Cx, m.X2 }, Xs(targets), tol, out res.GuideX);
        res.Dy = BestDelta(new[] { m.Y1, m.Cy, m.Y2 }, Ys(targets), tol, out res.GuideY);
        if (!res.SnappedX && grid > 0) res.Dx = SnapGrid(m.X1, grid) - m.X1;
        if (!res.SnappedY && grid > 0) res.Dy = SnapGrid(m.Y1, grid) - m.Y1;
        return res;
    }

    /// <summary>Bắt một điểm (khi vẽ hoặc kéo tay nắm): mép/tâm đối tượng khác, ngược lại lưới.</summary>
    public static (double X, double Y, double GuideX, double GuideY) SnapPoint(double x, double y, IList<Rect2> targets, double tol, double grid)
    {
        targets ??= new List<Rect2>();
        double dx = BestDelta(new[] { x }, Xs(targets), tol, out double gx);
        double dy = BestDelta(new[] { y }, Ys(targets), tol, out double gy);
        double nx = double.IsNaN(gx) ? SnapGrid(x, grid) : x + dx;
        double ny = double.IsNaN(gy) ? SnapGrid(y, grid) : y + dy;
        return (nx, ny, gx, gy);
    }

    // ==================================================================
    // Căn hàng, phân bố
    // ==================================================================
    /// <summary>Độ dịch (dx, dy) cho từng hình để căn theo mép/tâm của phạm vi chung.</summary>
    public static List<(double Dx, double Dy)> Align(IList<Rect2> rs, AlignMode mode)
    {
        var res = new List<(double, double)>();
        var u = Union(rs);
        foreach (var r in rs)
        {
            double dx = 0, dy = 0;
            switch (mode)
            {
                case AlignMode.Left: dx = u.X1 - r.X1; break;
                case AlignMode.Right: dx = u.X2 - r.X2; break;
                case AlignMode.Bottom: dy = u.Y1 - r.Y1; break;
                case AlignMode.Top: dy = u.Y2 - r.Y2; break;
                case AlignMode.CenterH: dx = u.Cx - r.Cx; break;
                case AlignMode.CenterV: dy = u.Cy - r.Cy; break;
            }
            res.Add((dx, dy));
        }
        return res;
    }

    /// <summary>Phân bố đều khoảng hở giữa các hình (giữ hai hình ngoài cùng). horizontal = theo phương X.</summary>
    public static List<(double Dx, double Dy)> Distribute(IList<Rect2> rs, bool horizontal)
    {
        var res = rs.Select(_ => (0.0, 0.0)).ToList();
        int n = rs.Count;
        if (n < 3) return res;
        var order = Enumerable.Range(0, n).OrderBy(i => horizontal ? rs[i].Cx : rs[i].Cy).ToList();
        double lo = horizontal ? rs[order[0]].X1 : rs[order[0]].Y1;
        double hi = horizontal ? rs[order[n - 1]].X2 : rs[order[n - 1]].Y2;
        double sum = order.Sum(i => horizontal ? rs[i].W : rs[i].H);
        double gap = (hi - lo - sum) / (n - 1);
        double pos = lo;
        foreach (int i in order)
        {
            double cur = horizontal ? rs[i].X1 : rs[i].Y1;
            res[i] = horizontal ? (pos - cur, 0.0) : (0.0, pos - cur);
            pos += (horizontal ? rs[i].W : rs[i].H) + gap;
        }
        return res;
    }

    // ==================================================================
    // Đổi cỡ
    // ==================================================================
    /// <summary>Tay nắm: 0 trái dưới, 1 giữa dưới, 2 phải dưới, 3 giữa phải, 4 phải trên, 5 giữa trên, 6 trái trên, 7 giữa trái.</summary>
    public static (double X, double Y) HandlePos(Rect2 r, int h) => h switch
    {
        0 => (r.X1, r.Y1), 1 => (r.Cx, r.Y1), 2 => (r.X2, r.Y1), 3 => (r.X2, r.Cy),
        4 => (r.X2, r.Y2), 5 => (r.Cx, r.Y2), 6 => (r.X1, r.Y2), _ => (r.X1, r.Cy)
    };

    /// <summary>Hình chữ nhật mới khi kéo tay nắm h tới điểm (x, y); giữ kích thước tối thiểu min.</summary>
    public static Rect2 ResizeRect(Rect2 r, int h, double x, double y, double min = 2)
    {
        double x1 = r.X1, y1 = r.Y1, x2 = r.X2, y2 = r.Y2;
        bool left = h is 0 or 6 or 7, right = h is 2 or 3 or 4, bottom = h is 0 or 1 or 2, top = h is 4 or 5 or 6;
        if (left) x1 = Math.Min(x, x2 - min);
        if (right) x2 = Math.Max(x, x1 + min);
        if (bottom) y1 = Math.Min(y, y2 - min);
        if (top) y2 = Math.Max(y, y1 + min);
        return new Rect2(x1, y1, x2, y2);
    }

    /// <summary>Đặt đối tượng vào phạm vi mới nb (từ phạm vi cũ ob): khung/ảnh/bảng đổi W×H, tròn đổi bán kính,
    /// chữ đổi chiều cao chữ, đường/kích thước co giãn toạ độ điểm.</summary>
    public static void SetBounds(SheetItem it, Rect2 ob, Rect2 nb)
    {
        switch (it.Kind)
        {
            case SheetItemKind.Rect:
            case SheetItemKind.Image:
            case SheetItemKind.Table:
                it.X = nb.X1; it.Y = nb.Y1; it.W = nb.W; it.H = nb.H;
                break;
            case SheetItemKind.Cloud:
            {
                double p = Math.Max(0.5, it.ArcLength / 2);
                it.X = nb.X1 + p; it.Y = nb.Y1 + p; it.W = Math.Max(1, nb.W - 2 * p); it.H = Math.Max(1, nb.H - 2 * p);
                break;
            }
            case SheetItemKind.Circle:
                it.X = nb.Cx; it.Y = nb.Cy; it.R = Math.Max(0.5, Math.Min(nb.W, nb.H) / 2);
                break;
            case SheetItemKind.Text:
            {
                double f = ob.H > 1e-6 ? nb.H / ob.H : 1;
                it.TextHeight = Math.Clamp(it.TextHeight * f, 0.5, 200);
                var b = Bounds(it);
                Move(it, nb.X1 - b.X1, nb.Y1 - b.Y1);
                break;
            }
            default:
            {
                double fx = ob.W > 1e-6 ? nb.W / ob.W : 1, fy = ob.H > 1e-6 ? nb.H / ob.H : 1;
                for (int i = 0; i + 1 < it.Pts.Count; i += 2)
                {
                    it.Pts[i] = nb.X1 + (it.Pts[i] - ob.X1) * fx;
                    it.Pts[i + 1] = nb.Y1 + (it.Pts[i + 1] - ob.Y1) * fy;
                }
                break;
            }
        }
    }

    // ==================================================================
    // Chọn
    // ==================================================================
    public static double DistToSegment(double px, double py, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1, l2 = dx * dx + dy * dy;
        double t = l2 < 1e-12 ? 0 : Math.Clamp(((px - x1) * dx + (py - y1) * dy) / l2, 0, 1);
        double cx = x1 + t * dx - px, cy = y1 + t * dy - py;
        return Math.Sqrt(cx * cx + cy * cy);
    }

    private static double DistToRectEdge(Rect2 r, double x, double y) => Math.Min(
        Math.Min(DistToSegment(x, y, r.X1, r.Y1, r.X2, r.Y1), DistToSegment(x, y, r.X2, r.Y1, r.X2, r.Y2)),
        Math.Min(DistToSegment(x, y, r.X2, r.Y2, r.X1, r.Y2), DistToSegment(x, y, r.X1, r.Y2, r.X1, r.Y1)));

    /// <summary>Điểm (x, y) có chạm đối tượng không (tol mm). Khung/tròn/đám mây chỉ bắt theo nét viền
    /// để vẫn chọn được khung nhìn bên dưới; chữ/ảnh/bảng bắt cả vùng trong.</summary>
    public static bool Hit(SheetItem it, double x, double y, double tol)
    {
        switch (it.Kind)
        {
            case SheetItemKind.Rect:
            case SheetItemKind.Cloud:
                return DistToRectEdge(Bounds(it).Inflate(it.Kind == SheetItemKind.Cloud ? -Math.Max(0.5, it.ArcLength / 2) : 0), x, y) <= tol + (it.Kind == SheetItemKind.Cloud ? it.ArcLength / 2 : 0);
            case SheetItemKind.Circle:
                return Math.Abs(Math.Sqrt((x - it.X) * (x - it.X) + (y - it.Y) * (y - it.Y)) - it.R) <= tol;
            case SheetItemKind.Line:
            {
                var p = it.Pts;
                for (int i = 0; i + 3 < p.Count; i += 2)
                    if (DistToSegment(x, y, p[i], p[i + 1], p[i + 2], p[i + 3]) <= tol) return true;
                if (it.Closed && p.Count >= 6 && DistToSegment(x, y, p[^2], p[^1], p[0], p[1]) <= tol) return true;
                return false;
            }
            case SheetItemKind.Text:
            {
                // đổi về hệ toạ độ chữ chưa xoay
                double a = -it.Rotation * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
                double lx = (x - it.X) * c - (y - it.Y) * s, ly = (x - it.X) * s + (y - it.Y) * c;
                var tmp = new SheetItem { Kind = SheetItemKind.Text, Text = it.Text, TextHeight = it.TextHeight, Align = it.Align };
                return Bounds(tmp).Inflate(tol).Intersects(new Rect2(lx - 1e-6, ly - 1e-6, lx + 1e-6, ly + 1e-6));
            }
            default:
            {
                var b = Bounds(it).Inflate(tol);
                return x >= b.X1 && x <= b.X2 && y >= b.Y1 && y <= b.Y2;
            }
        }
    }

    /// <summary>Chọn bằng khung: kéo trái→phải chỉ lấy đối tượng nằm trọn trong khung; phải→trái lấy cả đối tượng giao khung.</summary>
    public static bool InWindow(Rect2 obj, Rect2 window, bool crossing) =>
        crossing ? obj.Inflate(1e-3).Intersects(window)
                 : obj.X1 >= window.X1 && obj.X2 <= window.X2 && obj.Y1 >= window.Y1 && obj.Y2 <= window.Y2;

    // ==================================================================
    // Thứ tự trên/dưới
    // ==================================================================
    public static void BringToFront<T>(List<T> list, ICollection<T> sel)
    {
        var moved = list.Where(sel.Contains).ToList();
        if (moved.Count == 0) return;
        list.RemoveAll(sel.Contains);
        list.AddRange(moved);
    }

    public static void SendToBack<T>(List<T> list, ICollection<T> sel)
    {
        var moved = list.Where(sel.Contains).ToList();
        if (moved.Count == 0) return;
        list.RemoveAll(sel.Contains);
        list.InsertRange(0, moved);
    }

    // ==================================================================
    // Bảng
    // ==================================================================
    /// <summary>Nội dung bảng dạng văn bản: mỗi hàng một dòng, các ô ngăn bởi '|' (hoặc Tab).</summary>
    public static string TableToText(SheetItem t)
    {
        var lines = new List<string>();
        for (int r = 0; r < t.Rows; r++)
            lines.Add(string.Join(" | ", Enumerable.Range(0, t.Cols).Select(c => t.Cell(r, c))));
        return string.Join("\n", lines);
    }

    public static void TableFromText(SheetItem t, string text)
    {
        var rows = (text ?? "").Replace("\r", "").Split('\n').ToList();
        while (rows.Count > 1 && rows[^1].Trim().Length == 0) rows.RemoveAt(rows.Count - 1);
        var cells = rows.Select(r => r.Split(r.Contains('\t') ? '\t' : '|').Select(x => x.Trim()).ToList()).ToList();
        int cols = Math.Max(1, cells.Max(c => c.Count));
        t.Rows = Math.Max(1, cells.Count);
        t.Cols = cols;
        t.Cells = new List<string>();
        foreach (var r in cells)
            for (int c = 0; c < cols; c++) t.Cells.Add(c < r.Count ? r[c] : "");
        if (t.ColWidths != null && t.ColWidths.Count != cols) t.ColWidths = new List<double>();
    }
}

/// <summary>Ngăn xếp hoàn tác/làm lại theo ảnh chụp trạng thái (chuỗi JSON).</summary>
public sealed class UndoStack
{
    private readonly List<string> _undo = new(), _redo = new();
    public int Limit { get; set; } = 100;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Ghi trạng thái TRƯỚC khi sửa. Trùng trạng thái đỉnh ngăn xếp thì bỏ qua. Xoá nhánh làm lại.</summary>
    public void Push(string state)
    {
        if (state == null) return;
        _redo.Clear();
        if (_undo.Count > 0 && _undo[^1] == state) return;
        _undo.Add(state);
        while (_undo.Count > Math.Max(1, Limit)) _undo.RemoveAt(0);
    }

    /// <summary>Trả về trạng thái trước đó (null nếu hết); current được đẩy sang ngăn làm lại.</summary>
    public string Undo(string current)
    {
        if (_undo.Count == 0) return null;
        string s = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        if (current != null) _redo.Add(current);
        return s;
    }

    public string Redo(string current)
    {
        if (_redo.Count == 0) return null;
        string s = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        if (current != null) _undo.Add(current);
        return s;
    }

    public void Clear() { _undo.Clear(); _redo.Clear(); }
}
