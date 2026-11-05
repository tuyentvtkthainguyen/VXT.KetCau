using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Kích thước ĐỊNH VỊ DẦM NGOÀI TRỤC trên mặt bằng kết cấu: dầm phụ, dầm lệch trục, dầm công xôn song song lưới
/// được ghi khoảng cách từ tim dầm tới trục gần nhất (một kích thước cho mỗi đường tim, đặt ở 1/4 chiều dài dầm để tránh nhãn tên dầm).</summary>
public static class BeamLocateDims
{
    /// <summary>Thêm kích thước định vị; trả về số kích thước đã ghi. tol: sai số coi là nằm trên trục (m).</summary>
    public static int Add(DxfWriter w, Project p, Story st, double tol = 0.01)
    {
        if (w == null || p == null || st == null || !w.Style.ShowGridDims) return 0;   // theo tuỳ chọn "kích thước lưới trục"
        var gx = p.GridsX.Select(g => g.Coord).ToList();
        var gy = p.GridsY.Select(g => g.Coord).ToList();
        if (gx.Count == 0 && gy.Count == 0) return 0;
        var done = new HashSet<string>();
        int n = 0;
        foreach (var f in p.Frames.Where(fe => fe.Kind == FrameKind.Beam && fe.Story == st.Name))
        {
            Node a, b;
            try { a = p.Node(f.NodeI); b = p.Node(f.NodeJ); } catch { continue; }
            if (a == null || b == null) continue;
            double dx = b.X - a.X, dy = b.Y - a.Y;
            bool alongX = Math.Abs(dy) < 1e-4 && Math.Abs(dx) > 0.2, alongY = Math.Abs(dx) < 1e-4 && Math.Abs(dy) > 0.2;
            if (alongX && gy.Count > 0)
            {
                double y = a.Y, g = gy.OrderBy(c => Math.Abs(c - y)).First();
                if (Math.Abs(g - y) <= tol) continue;
                if (!done.Add("Y|" + Math.Round(y, 3) + "|" + Math.Round(g, 3))) continue;
                double xm = Math.Min(a.X, b.X) + 0.25 * Math.Abs(dx);
                w.Dim(xm * 1000, g * 1000, xm * 1000, y * 1000, 0);
                n++;
            }
            else if (alongY && gx.Count > 0)
            {
                double x = a.X, g = gx.OrderBy(c => Math.Abs(c - x)).First();
                if (Math.Abs(g - x) <= tol) continue;
                if (!done.Add("X|" + Math.Round(x, 3) + "|" + Math.Round(g, 3))) continue;
                double ym = Math.Min(a.Y, b.Y) + 0.25 * Math.Abs(dy);
                w.Dim(g * 1000, ym * 1000, x * 1000, ym * 1000, 0);
                n++;
            }
        }
        return n;
    }
}
