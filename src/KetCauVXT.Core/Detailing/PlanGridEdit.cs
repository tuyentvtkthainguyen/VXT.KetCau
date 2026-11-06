using System.Globalization;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

/// <summary>Sửa lưới trục và quét chọn trên không gian vẽ mặt bằng kết cấu (chế độ Triển khai hồ sơ).
/// Lưới lưu dạng bước trục SpansX / SpansY (toạ độ tính từ trục đầu = 0): thêm / xoá / dời trục cập nhật chuỗi bước;
/// trục đầu thay đổi → mọi vị trí cấu kiện dời theo để trục đầu vẫn ở 0.</summary>
public static class PlanGridEdit
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public const double Tol = 0.005;

    public static string SpansOf(IReadOnlyList<double> coords)
    {
        var c = coords.OrderBy(v => v).ToList();
        if (c.Count < 2) return "";
        return string.Join(" ", c.Zip(c.Skip(1), (a, b) => Math.Round(b - a, 4).ToString("0.####", Inv)));
    }

    private static List<double> Get(StructuralPlan pl, bool x) => (x ? pl.Xs : pl.Ys).ToList();

    private static List<string> Names(List<double> cs, bool x) => cs.Select((_, i) => StructuralPlan.GridName(x, i)).ToList();

    /// <summary>Giữ kiểu hiển thị (bong bóng / ẩn) theo trục khi thứ tự trục đổi.</summary>
    private static void Remap(StructuralPlan pl, bool x, List<double> cs, List<string> names)
    {
        var order = cs.Select((v, i) => (V: v, N: names[i])).OrderBy(t => t.V).Select(t => t.N).ToList();
        pl.RemapStyles(x, order);
    }

    private static void Set(StructuralPlan pl, bool x, List<double> c)
    {
        c = c.OrderBy(v => v).ToList();
        double shift = c.Count > 0 ? c[0] : 0;
        if (Math.Abs(shift) > 1e-9)
        {
            // giữ trục đầu tại 0: dời mọi vị trí cấu kiện, mặt cắt
            foreach (var lv in pl.Levels)
                foreach (var it in lv.Items)
                {
                    if (x) { it.X1 -= shift; it.X2 -= shift; } else { it.Y1 -= shift; it.Y2 -= shift; }
                }
            foreach (var s in pl.Sections ?? new List<SectionMark>())
            {
                if (x) { s.X1 -= shift; s.X2 -= shift; } else { s.Y1 -= shift; s.Y2 -= shift; }
            }
        }
        string sp = SpansOf(c.Select(v => v - shift).ToList());
        if (x) pl.SpansX = sp; else pl.SpansY = sp;
    }

    /// <summary>Thêm trục tại toạ độ c (m, hệ toạ độ hiện tại). false nếu trùng trục có sẵn.</summary>
    public static bool Insert(StructuralPlan pl, bool xDir, double c)
    {
        var cs = Get(pl, xDir);
        if (cs.Any(v => Math.Abs(v - c) < Tol)) return false;
        var names = Names(cs, xDir);
        cs.Add(Math.Round(c, 4)); names.Add(null);
        Set(pl, xDir, cs);
        Remap(pl, xDir, cs, names);
        return true;
    }

    /// <summary>Xoá trục thứ index (0 = trục đầu). Cần còn ≥ 2 trục.</summary>
    public static bool Remove(StructuralPlan pl, bool xDir, int index)
    {
        var cs = Get(pl, xDir);
        if (index < 0 || index >= cs.Count || cs.Count <= 2) return false;
        var names = Names(cs, xDir);
        cs.RemoveAt(index); names.RemoveAt(index);
        Set(pl, xDir, cs);
        Remap(pl, xDir, cs, names);
        return true;
    }

    /// <summary>Dời trục index tới toạ độ c. drag = kéo theo đầu cấu kiện nằm trên trục. Trả số vị trí đã dời.</summary>
    public static int Move(StructuralPlan pl, bool xDir, int index, double c, bool drag = true)
    {
        var cs = Get(pl, xDir);
        if (index < 0 || index >= cs.Count) return 0;
        double old = cs[index];
        if (cs.Where((v, i) => i != index).Any(v => Math.Abs(v - c) < Tol)) return 0;
        int n = 0;
        if (drag)
            foreach (var lv in pl.Levels)
                foreach (var it in lv.Items)
                {
                    bool m = false;
                    if (xDir)
                    {
                        if (Math.Abs(it.X1 - old) < Tol) { it.X1 = c; m = true; }
                        if (Math.Abs(it.X2 - old) < Tol) { it.X2 = c; m = true; }
                    }
                    else
                    {
                        if (Math.Abs(it.Y1 - old) < Tol) { it.Y1 = c; m = true; }
                        if (Math.Abs(it.Y2 - old) < Tol) { it.Y2 = c; m = true; }
                    }
                    if (m) n++;
                }
        var names = Names(cs, xDir);
        cs[index] = Math.Round(c, 4);
        Set(pl, xDir, cs);
        Remap(pl, xDir, cs, names);
        return n;
    }

    /// <summary>Trục gần điểm (x, y) nhất trong dung sai: (phương X?, chỉ số) hoặc null.</summary>
    public static (bool XDir, int Index)? GridAt(StructuralPlan pl, double x, double y, double tol)
    {
        var xs = pl.Xs; var ys = pl.Ys;
        int ix = -1, iy = -1; double dx = double.MaxValue, dy = double.MaxValue;
        for (int i = 0; i < xs.Length; i++) if (Math.Abs(xs[i] - x) < dx) { dx = Math.Abs(xs[i] - x); ix = i; }
        for (int i = 0; i < ys.Length; i++) if (Math.Abs(ys[i] - y) < dy) { dy = Math.Abs(ys[i] - y); iy = i; }
        if (dx <= tol && dx <= dy && ix >= 0) return (true, ix);
        if (dy <= tol && iy >= 0) return (false, iy);
        return null;
    }

    /// <summary>Quét chọn vị trí cấu kiện trên mặt bằng: window (trọn trong khung) / crossing (chạm khung).</summary>
    public static List<PlanItem> Select(PlanLevel lv, double xa, double ya, double xb, double yb, bool crossing)
    {
        var r = SelectionRules.Box.Of(xa, ya, xb, yb);
        var res = new List<PlanItem>();
        if (lv == null) return res;
        foreach (var it in lv.Items)
        {
            bool hit;
            if (PlanKinds.IsPoint(it.Kind)) hit = r.Contains(it.X1, it.Y1);
            else if (PlanKinds.IsRect(it.Kind))
            {
                double x0 = Math.Min(it.X1, it.X2), x1 = Math.Max(it.X1, it.X2), y0 = Math.Min(it.Y1, it.Y2), y1 = Math.Max(it.Y1, it.Y2);
                hit = SelectionRules.Polygon(r, new List<(double X, double Y)> { (x0, y0), (x1, y0), (x1, y1), (x0, y1) }, crossing);
            }
            else hit = SelectionRules.Polyline(r, new[] { (it.X1, it.Y1), (it.X2, it.Y2) }, crossing);
            if (hit) res.Add(it);
        }
        return res;
    }
}
