namespace KetCauVXT.Ui;

/// <summary>Vừa màn hình mặt bằng: tỷ lệ (px/m) và gốc (ox, oy) để phạm vi [x0..x1] × [y0..y1] (m, trục Y hướng lên)
/// nằm giữa vùng vẽ w × h (px, trục Y màn hình hướng xuống), chừa lề tỷ lệ (marginFrac) + lề cố định (padPx) cho bong bóng trục
/// và đường kích thước. Màn hình: sx = ox + x·scale, sy = oy − y·scale.</summary>
public static class PlanFit
{
    public readonly record struct Result(double Scale, double Ox, double Oy, bool Ok);

    /// <summary>Lề cố định mặc định (px): bong bóng trục (Ø ≈ 22 px) + 2 dòng kích thước.</summary>
    public const double DefaultPadPx = 56;
    public const double DefaultMarginFrac = 0.08;

    public static Result Fit(double x0, double x1, double y0, double y1, double w, double h,
        double marginFrac = DefaultMarginFrac, double padPx = DefaultPadPx, double minScale = 0.5, double maxScale = 400)
    {
        if (w <= 0 || h <= 0 || double.IsNaN(w) || double.IsNaN(h)) return new Result(0, 0, 0, false);
        if (x1 < x0) (x0, x1) = (x1, x0);
        if (y1 < y0) (y0, y1) = (y1, y0);
        double dx = Math.Max(x1 - x0, 1), dy = Math.Max(y1 - y0, 1);
        double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
        // vùng dùng được: trừ lề tỷ lệ + lề cố định mỗi bên; luôn giữ ít nhất 30 % kích thước vùng vẽ
        double uw = Math.Max(w * (1 - 2 * marginFrac) - 2 * padPx, 0.3 * w);
        double uh = Math.Max(h * (1 - 2 * marginFrac) - 2 * padPx, 0.3 * h);
        double s = Math.Clamp(Math.Min(uw / dx, uh / dy), minScale, maxScale);
        double ox = w / 2 - cx * s;
        double oy = h / 2 + cy * s;
        return new Result(s, ox, oy, true);
    }

    /// <summary>Có nên tự vừa màn hình lại khi vùng vẽ đổi cỡ: chỉ khi người dùng chưa phóng / kéo tay.</summary>
    public static bool RefitOnResize(bool userZoomed, double oldW, double oldH, double newW, double newH) =>
        !userZoomed && newW > 0 && newH > 0 && (Math.Abs(newW - oldW) > 0.5 || Math.Abs(newH - oldH) > 0.5);
}

/// <summary>Đặt nhãn mặt bằng trong hệ toạ độ màn hình: xếp theo ưu tiên (0 = trục > 1 = cột > 2 = dầm > 3 = sàn > 4 = khác),
/// nhãn cố định (trục) giữ chỗ trước; nhãn di động thử vị trí ưu tiên rồi các vị trí lệch; hết chỗ thì bỏ (Dropped).</summary>
public sealed class PlanLabelPlacer
{
    public readonly record struct Box(double X, double Y, double W, double H)
    {
        public bool Hits(Box o, double gap = 1) =>
            X < o.X + o.W + gap && o.X < X + W + gap && Y < o.Y + o.H + gap && o.Y < Y + H + gap;
    }

    public sealed class Item
    {
        public int Priority;
        public bool Movable = true;
        public Box Want;
        public object Tag;
        /// <summary>Kết quả: vị trí đặt (null = bỏ)</summary>
        public Box? Placed;
        internal int Order;
    }

    private readonly List<Item> _items = new();
    private readonly List<Box> _obstacles = new();
    public IReadOnlyList<Item> Items => _items;
    public IEnumerable<Item> Dropped => _items.Where(i => i.Placed == null);

    /// <summary>Vùng cấm (vd. thân cột, bong bóng trục) – nhãn di động không được đè.</summary>
    public void Obstacle(Box b) => _obstacles.Add(b);

    public Item Add(Box want, int priority, bool movable = true, object tag = null)
    {
        var it = new Item { Want = want, Priority = movable ? priority : Math.Min(priority, 0), Movable = movable, Tag = tag, Order = _items.Count };
        _items.Add(it);
        return it;
    }

    /// <summary>Phân loại nhãn theo nội dung chữ (dùng khi người gọi không biết loại): C1 → cột, D…/ "220x400" → dầm, S…/ST… → sàn.</summary>
    public static int GuessPriority(string text, bool axis)
    {
        if (axis) return 0;
        if (string.IsNullOrWhiteSpace(text)) return 4;
        string t = text.TrimStart();
        if (t.Length >= 2 && (t[0] == 'C' || t[0] == 'K') && char.IsDigit(t[1])) return 1;
        if (t.Length >= 2 && t[0] == 'D' && (char.IsDigit(t[1]) || t[1] == 'T' || t[1] == 'P')) return 2;
        if (t.Contains('x') && t.Any(char.IsDigit) && t.Contains('D')) return 2;
        if (t.Length >= 2 && t[0] == 'S' && (char.IsDigit(t[1]) || t[1] == 'T')) return 3;
        return 4;
    }

    /// <summary>Đặt tất cả: trả về số nhãn bị bỏ.</summary>
    public int Solve()
    {
        var placed = new List<Box>(_obstacles);
        foreach (var it in _items.Where(i => !i.Movable).OrderBy(i => i.Order))
        {
            it.Placed = it.Want;
            placed.Add(it.Want);
        }
        foreach (var it in _items.Where(i => i.Movable).OrderBy(i => i.Priority).ThenBy(i => i.Order))
        {
            it.Placed = null;
            foreach (var c in Candidates(it.Want))
            {
                bool hit = false;
                foreach (var b in placed) if (c.Hits(b)) { hit = true; break; }
                if (hit) continue;
                it.Placed = c; placed.Add(c);
                break;
            }
        }
        return _items.Count(i => i.Placed == null);
    }

    /// <summary>Vị trí thử: chỗ muốn → trên / dưới → phải / trái → chéo → xa hơn.</summary>
    public static IEnumerable<Box> Candidates(Box b)
    {
        double w = b.W, h = b.H;
        var offs = new (double, double)[]
        {
            (0, 0), (0, -h - 2), (0, h + 2), (w * 0.6 + 2, 0), (-w * 0.6 - 2, 0),
            (w * 0.6 + 2, -h - 2), (-w * 0.6 - 2, -h - 2), (w * 0.6 + 2, h + 2), (-w * 0.6 - 2, h + 2),
            (0, -2 * h - 4), (0, 2 * h + 4), (w + 4, 0), (-w - 4, 0),
            (w + 4, -h - 2), (-w - 4, -h - 2), (w + 4, h + 2), (-w - 4, h + 2)
        };
        foreach (var (dx, dy) in offs) yield return new Box(b.X + dx, b.Y + dy, w, h);
    }
}
