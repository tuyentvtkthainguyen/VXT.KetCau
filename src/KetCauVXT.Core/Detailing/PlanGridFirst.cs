using System.Globalization;

namespace KetCauVXT.Detailing;

/// <summary>[Hệ trục trước] Triển khai mặt bằng bắt đầu bằng hệ trục: suy bước trục từ cột / dầm đã có (nhập AutoCAD, vẽ tay),
/// hoặc người dùng nhập bước trục. Toạ độ trục luôn tính từ 0 → khi trục đầu không ở gốc, dời toàn bộ mặt bằng (mọi tầng,
/// ánh xạ kiến trúc, đường cắt) để trục 1 – A trùng gốc.</summary>
public static class PlanGridFirst
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Toạ độ các đường trục suy từ mặt bằng: X của tâm cột / móng và tim dầm phương Y; Y tương tự.
    /// Một đường trục cần ≥ minHits cấu kiện nằm trên (gộp trong tol).</summary>
    public static (List<double> X, List<double> Y) Lines(IEnumerable<PlanLevel> levels, double tol = 0.05, int minHits = 2)
    {
        var xs = new List<double>(); var ys = new List<double>();
        foreach (var lv in levels ?? Enumerable.Empty<PlanLevel>())
            foreach (var it in lv.Items)
            {
                if (PlanKinds.IsPoint(it.Kind)) { xs.Add(it.X1); ys.Add(it.Y1); }
                else if (it.Kind == PlanKinds.Beam && !it.IsCurved && it.Length > 0.3)
                {
                    if (Math.Abs(it.X1 - it.X2) < 0.02) { xs.Add(it.X1); xs.Add(it.X2); }
                    if (Math.Abs(it.Y1 - it.Y2) < 0.02) { ys.Add(it.Y1); ys.Add(it.Y2); }
                }
            }
        return (Cluster(xs, tol, minHits), Cluster(ys, tol, minHits));
    }

    private static List<double> Cluster(List<double> v, double tol, int minHits)
    {
        var r = new List<double>();
        v.Sort();
        int i = 0;
        while (i < v.Count)
        {
            int j = i;
            while (j + 1 < v.Count && v[j + 1] - v[i] <= tol) j++;
            if (j - i + 1 >= minHits) r.Add(Math.Round(v.Skip(i).Take(j - i + 1).Average(), 3));
            i = j + 1;
        }
        return r;
    }

    /// <summary>Chuỗi bước trục "3.6 4.2" từ toạ độ đường trục (bỏ bước &lt; 0,3 m).</summary>
    public static string Spans(IList<double> c)
    {
        var s = new List<string>();
        double last = c.Count > 0 ? c[0] : 0;
        for (int i = 1; i < c.Count; i++)
        {
            double d = Math.Round(c[i] - last, 3);
            if (d < 0.3) continue;
            s.Add(d.ToString("0.###", Inv)); last = c[i];
        }
        return string.Join(" ", s);
    }

    /// <summary>Dời toàn bộ mặt bằng (mọi tầng, ánh xạ kiến trúc, đường cắt) một vector.</summary>
    public static void ShiftAll(StructuralPlan plan, double dx, double dy)
    {
        if (plan == null || (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)) return;
        foreach (var lv in plan.Levels)
        {
            foreach (var it in lv.Items) PlanSnap.Move(it, dx, dy);
            lv.UnderlayDx = Math.Round(lv.UnderlayDx + dx, 4); lv.UnderlayDy = Math.Round(lv.UnderlayDy + dy, 4);
        }
        foreach (var s in plan.Sections ?? new())
        {
            s.X1 = Math.Round(s.X1 + dx, 4); s.X2 = Math.Round(s.X2 + dx, 4); s.Y1 = Math.Round(s.Y1 + dy, 4); s.Y2 = Math.Round(s.Y2 + dy, 4);
        }
    }

    /// <summary>Tạo hệ trục từ cấu kiện đã có trên các mặt bằng (khi chưa có trục). Trả mô tả, hoặc null nếu không đủ 2 trục mỗi phương.</summary>
    public static string Infer(StructuralPlan plan, bool force = false)
    {
        if (plan == null || (plan.HasGrid && !force)) return null;
        var (x, y) = Lines(plan.Levels);
        string sx = Spans(x), sy = Spans(y);
        if (string.IsNullOrEmpty(sx) || string.IsNullOrEmpty(sy)) return null;
        double ox = x[0], oy = y[0];
        ShiftAll(plan, -ox, -oy);
        plan.SpansX = sx; plan.SpansY = sy;
        return $"Đã tạo hệ trục từ cột / dầm: trục X ({plan.Xs.Length} trục) bước {sx} m; trục Y ({plan.Ys.Length} trục) bước {sy} m"
               + (Math.Abs(ox) + Math.Abs(oy) > 1e-6 ? $" – dời mặt bằng ({(-ox).ToString("0.###", Inv)}; {(-oy).ToString("0.###", Inv)}) m để trục 1-A ở gốc." : ".");
    }
}
