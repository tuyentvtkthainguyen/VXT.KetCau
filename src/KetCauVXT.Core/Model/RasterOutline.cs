namespace KetCauVXT.Model;

/// <summary>Đường bao (các vòng kín trực giao) của vùng "đặc" trên lưới ô vuông – dùng cho hợp các ô sàn (mái theo đường bao sàn),
/// mặt sàn của mô hình SketchUp (sàn, ban công, lỗ thang). Vòng ngoài ngược chiều kim đồng hồ, lỗ cùng chiều kim đồng hồ.</summary>
public static class RasterOutline
{
    /// <summary>Lưới ô: gốc (x0, y0), cạnh ô h, nx × ny ô, cells[i, j] = đặc.</summary>
    public static List<List<(double X, double Y)>> Loops(bool[,] cells, double x0, double y0, double h)
    {
        int nx = cells.GetLength(0), ny = cells.GetLength(1);
        bool In(int i, int j) => i >= 0 && j >= 0 && i < nx && j < ny && cells[i, j];
        // cạnh biên có hướng (vùng đặc bên trái): lưu theo điểm đầu
        var next = new Dictionary<(int, int), List<(int, int)>>();
        void Add((int, int) a, (int, int) b) { if (!next.TryGetValue(a, out var l)) next[a] = l = new List<(int, int)>(); l.Add(b); }
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                if (!cells[i, j]) continue;
                if (!In(i, j - 1)) Add((i, j), (i + 1, j));            // cạnh dưới → sang phải
                if (!In(i + 1, j)) Add((i + 1, j), (i + 1, j + 1));    // cạnh phải → lên
                if (!In(i, j + 1)) Add((i + 1, j + 1), (i, j + 1));    // cạnh trên → sang trái
                if (!In(i - 1, j)) Add((i, j + 1), (i, j));            // cạnh trái → xuống
            }
        var loops = new List<List<(double X, double Y)>>();
        while (next.Count > 0)
        {
            var start = next.Keys.First();
            var loop = new List<(int, int)>();
            var cur = start;
            (int, int) prevDir = (0, 0);
            int guard = 0;
            while (guard++ < 4_000_000)
            {
                loop.Add(cur);
                if (!next.TryGetValue(cur, out var outs) || outs.Count == 0) break;
                // ở đỉnh chéo (2 cạnh ra) ưu tiên rẽ trái để tách vòng đúng
                (int, int) pick = outs[0];
                if (outs.Count > 1 && prevDir != (0, 0))
                {
                    var left = (-prevDir.Item2, prevDir.Item1);
                    foreach (var o in outs) if ((o.Item1 - cur.Item1, o.Item2 - cur.Item2) == left) pick = o;
                }
                outs.Remove(pick);
                if (outs.Count == 0) next.Remove(cur);
                prevDir = (pick.Item1 - cur.Item1, pick.Item2 - cur.Item2);
                cur = pick;
                if (cur == start) break;
            }
            // bỏ đỉnh thẳng hàng
            var simp = new List<(int, int)>();
            int n = loop.Count;
            for (int k = 0; k < n; k++)
            {
                var a = loop[(k - 1 + n) % n]; var b = loop[k]; var c = loop[(k + 1) % n];
                bool col = (b.Item1 - a.Item1) * (c.Item2 - b.Item2) - (b.Item2 - a.Item2) * (c.Item1 - b.Item1) == 0;
                if (!col) simp.Add(b);
            }
            if (simp.Count >= 4) loops.Add(simp.Select(q => (Math.Round(x0 + q.Item1 * h, 4), Math.Round(y0 + q.Item2 * h, 4))).ToList());
        }
        return loops;
    }

    public static double SignedArea(IReadOnlyList<(double X, double Y)> p)
    {
        double a = 0;
        for (int i = 0; i < p.Count; i++) { var q = p[i]; var r = p[(i + 1) % p.Count]; a += q.X * r.Y - r.X * q.Y; }
        return a / 2;
    }

    /// <summary>Đường bao ngoài lớn nhất của hợp các đa giác (lưới h m). null nếu rỗng.</summary>
    public static List<(double X, double Y)> UnionOutline(IEnumerable<IReadOnlyList<(double X, double Y)>> polys, double h = 0.05)
    {
        var ps = polys.Where(q => q.Count >= 3).ToList();
        if (ps.Count == 0) return null;
        double x0 = ps.Min(q => q.Min(v => v.X)) - h, y0 = ps.Min(q => q.Min(v => v.Y)) - h;
        double x1 = ps.Max(q => q.Max(v => v.X)) + h, y1 = ps.Max(q => q.Max(v => v.Y)) + h;
        int nx = (int)Math.Ceiling((x1 - x0) / h), ny = (int)Math.Ceiling((y1 - y0) / h);
        if ((long)nx * ny > 4_000_000) { h = Math.Sqrt((x1 - x0) * (y1 - y0) / 4_000_000.0); nx = (int)Math.Ceiling((x1 - x0) / h); ny = (int)Math.Ceiling((y1 - y0) / h); }
        var cells = new bool[nx, ny];
        foreach (var poly in ps)
        {
            int i0 = Math.Max(0, (int)Math.Floor((poly.Min(v => v.X) - x0) / h)), i1 = Math.Min(nx - 1, (int)Math.Ceiling((poly.Max(v => v.X) - x0) / h));
            int j0 = Math.Max(0, (int)Math.Floor((poly.Min(v => v.Y) - y0) / h)), j1 = Math.Min(ny - 1, (int)Math.Ceiling((poly.Max(v => v.Y) - y0) / h));
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                    if (!cells[i, j] && Inside(poly, x0 + (i + 0.5) * h, y0 + (j + 0.5) * h)) cells[i, j] = true;
        }
        return Loops(cells, x0, y0, h).Where(l => SignedArea(l) > 0).OrderByDescending(SignedArea).FirstOrDefault();
    }

    public static bool Inside(IReadOnlyList<(double X, double Y)> poly, double x, double y)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) c = !c;
        }
        return c;
    }

    /// <summary>Chia đa giác trực giao thành hình chữ nhật (dải theo X rồi gộp các dải liền nhau cùng khoảng Y).</summary>
    public static List<(double X0, double Y0, double X1, double Y1)> Rectangles(IReadOnlyList<(double X, double Y)> poly)
    {
        var xs = poly.Select(v => Math.Round(v.X, 4)).Distinct().OrderBy(v => v).ToList();
        var ys = poly.Select(v => Math.Round(v.Y, 4)).Distinct().OrderBy(v => v).ToList();
        var strips = new List<(double X0, double X1, List<(double Y0, double Y1)> Iv)>();
        for (int i = 0; i + 1 < xs.Count; i++)
        {
            double xm = (xs[i] + xs[i + 1]) / 2;
            var iv = new List<(double, double)>();
            for (int j = 0; j + 1 < ys.Count; j++)
            {
                if (!Inside(poly, xm, (ys[j] + ys[j + 1]) / 2)) continue;
                if (iv.Count > 0 && Math.Abs(iv[^1].Item2 - ys[j]) < 1e-9) iv[^1] = (iv[^1].Item1, ys[j + 1]);
                else iv.Add((ys[j], ys[j + 1]));
            }
            strips.Add((xs[i], xs[i + 1], iv));
        }
        var res = new List<(double, double, double, double)>();
        var open = new List<(double X0, double Y0, double Y1)>();
        double lastX = xs.Count > 0 ? xs[0] : 0;
        foreach (var s in strips)
        {
            var keep = new List<(double, double, double)>();
            foreach (var o in open)
            {
                if (s.Iv.Any(v => Math.Abs(v.Y0 - o.Y0) < 1e-9 && Math.Abs(v.Y1 - o.Y1) < 1e-9)) keep.Add(o);
                else res.Add((o.X0, o.Y0, s.X0, o.Y1));
            }
            foreach (var v in s.Iv)
                if (!keep.Any(o => Math.Abs(o.Item2 - v.Y0) < 1e-9 && Math.Abs(o.Item3 - v.Y1) < 1e-9)) keep.Add((s.X0, v.Y0, v.Y1));
            open = keep.Select(k => (k.Item1, k.Item2, k.Item3)).ToList();
            lastX = s.X1;
        }
        foreach (var o in open) res.Add((o.X0, o.Y0, lastX, o.Y1));
        return res;
    }

    public static bool IsOrthogonal(IReadOnlyList<(double X, double Y)> p)
    {
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count];
            if (Math.Abs(a.X - b.X) > 1e-6 && Math.Abs(a.Y - b.Y) > 1e-6) return false;
        }
        return true;
    }
}
