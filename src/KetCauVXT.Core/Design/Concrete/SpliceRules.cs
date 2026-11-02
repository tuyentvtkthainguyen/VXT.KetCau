using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Nguyên tắc nối thép – KHÔNG nối trong vùng nguy hiểm của cấu kiện (tham số trong RebarPreferences, mục "Nguyên tắc nối thép"):
/// <list type="bullet">
/// <item>Dầm: cấm nối trong nút khung (bề rộng gối) và vùng tới hạn lcr = k·h từ mép cột (k = 1; khung kháng chấn k = 2);
/// thép dưới cấm nối ở đoạn giữa nhịp (mô men dương lớn) → cho phép từ lcr đến Lo/4 tính từ mép gối;
/// thép trên cấm nối trên gối (mô men âm, tới Lo/4 / điểm cắt mũ) → cho phép đoạn ⅓ giữa nhịp; thép giá: ngoài nút và lcr.</item>
/// <item>Cột: cấm nối trong nút khung (chiều cao dầm đỉnh tầng) và lcr = max(hc; Hthông thuỷ/6; 450 mm) ở hai đầu mỗi tầng
/// (TCVN 9386:2012 5.4.3.2.2) → nối ở đoạn giữa chiều cao thông thuỷ, thường quanh giữa tầng.</item>
/// <item>Sàn: lưới dưới nối gần gối (trong Lo/4 cạnh dầm); lưới trên nối ở đoạn ⅓ giữa nhịp; không nối thép mũ trên gối.</item>
/// <item>Móng đơn, đài cọc: không nối (dùng thanh nguyên). Móng băng (dầm ngược): thép dưới nối ở ⅓ giữa khoảng cột, thép trên nối từ lcr tới L/4 cạnh cột.</item>
/// <item>Mối nối chồng toàn bộ chiều dài ll nằm trong vùng cho phép; không quá 50% số thanh nối tại một tiết diện (so le ≥ 1,3·ll);
/// vùng cho phép ngắn hơn ll → dời sang vùng khác / cây dài hơn / ống nối ren – không đặt lặng lẽ vào vùng cấm.</item>
/// </list></summary>
public static class SpliceRules
{
    /// <summary>Chiều dài vùng tới hạn dầm lcr (m).</summary>
    public static double BeamCritical(Project p, double h)
    {
        var pr = p?.RebarPrefs ?? new RebarPreferences();
        double k = p?.Seismic?.Enabled == true ? pr.BeamCriticalFactorSeismic : pr.BeamCriticalFactor;
        return Math.Max(k, 0) * h;
    }

    /// <summary>Chiều dài vùng tới hạn cột lcr = max(hc; Hc/tỷ số; min) (m).</summary>
    public static double ColumnCritical(Project p, double hc, double hClear)
    {
        var pr = p?.RebarPrefs ?? new RebarPreferences();
        return Math.Max(Math.Max(hc, hClear / Math.Max(pr.ColumnCriticalRatio, 1)), pr.ColumnCriticalMin);
    }

    /// <summary>Vùng CHO PHÉP nối thép dầm (m, toạ độ dọc dải từ tim gối đầu) theo vị trí thanh.</summary>
    public static List<(double A, double B)> BeamAllowed(Project p, BeamLineDesign d, bool top, string role)
    {
        var z = new List<(double, double)>();
        if (d?.Spans == null || d.Spans.Count == 0) return z;
        double lcr = BeamCritical(p, d.H);
        bool side = (role ?? "").StartsWith("Giá", StringComparison.Ordinal);
        double x = 0;
        foreach (var sp in d.Spans)
        {
            double f0 = x + sp.SupportWidthLeft / 2, f1 = x + sp.L - sp.SupportWidthRight / 2;
            double lc = Math.Max(f1 - f0, 0);
            if (side) Add(z, f0 + lcr, f1 - lcr);
            else if (top) Add(z, f0 + Math.Max(lc / 3, lcr), f1 - Math.Max(lc / 3, lcr));
            else
            {
                Add(z, f0 + lcr, f0 + lc / 4);
                Add(z, f1 - lc / 4, f1 - lcr);
            }
            x += sp.L;
        }
        return z;
    }

    /// <summary>Vùng CẤM nối thép dầm (tên + đoạn) – để ghi chú / kiểm tra.</summary>
    public static List<(string Name, double A, double B)> BeamForbidden(Project p, BeamLineDesign d, bool top)
    {
        var z = new List<(string, double, double)>();
        if (d?.Spans == null) return z;
        double lcr = BeamCritical(p, d.H);
        double x = 0;
        for (int k = 0; k < d.Spans.Count; k++)
        {
            var sp = d.Spans[k];
            double f0 = x + sp.SupportWidthLeft / 2, f1 = x + sp.L - sp.SupportWidthRight / 2, lc = Math.Max(f1 - f0, 0);
            z.Add(("nút khung", x - sp.SupportWidthLeft / 2, f0));
            z.Add(("vùng tới hạn", f0, f0 + lcr));
            z.Add(("vùng tới hạn", f1 - lcr, f1));
            if (top) { z.Add(("mô men âm gối", f0, f0 + lc / 4)); z.Add(("mô men âm gối", f1 - lc / 4, f1)); }
            else z.Add(("mô men dương giữa nhịp", f0 + lc / 3, f1 - lc / 3));
            if (k == d.Spans.Count - 1) z.Add(("nút khung", f1, x + sp.L + sp.SupportWidthRight / 2));
            x += sp.L;
        }
        return z;
    }

    /// <summary>Vùng cho phép nối của tầng si của cột (m, cao độ tính từ chân chồng cột) = [z0 + lcr; z1 − hnút − lcr].</summary>
    public static (double A, double B, double Lcr) ColumnAllowed(Project p, ColumnStoryResult s, double zBase)
    {
        double z0 = s.ZBottom - zBase, z1 = s.ZTop - zBase;
        double hClear = Math.Max(z1 - s.JointDepth - z0, 0.1);
        double lcr = ColumnCritical(p, Math.Max(s.B, s.H), hClear);
        return (z0 + lcr, z1 - s.JointDepth - lcr, lcr);
    }

    /// <summary>Vùng cho phép nối thép sườn móng băng (m, toạ độ từ đầu dải): thép dưới – ⅓ giữa khoảng cột; thép trên – từ lcr tới L/4 cạnh cột.</summary>
    public static List<(double A, double B)> StripAllowed(StripFootingDesign d, bool top)
    {
        var z = new List<(double, double)>();
        if (d == null || d.ColumnPositions.Count == 0) return z;
        double lcr = d.RibH;
        var cols = d.ColumnPositions.Select((x, i) => (X: x - d.Start, W: i < d.ColumnSizes.Count ? d.ColumnSizes[i] : 0.3)).OrderBy(c => c.X).ToList();
        for (int i = 0; i + 1 < cols.Count; i++)
        {
            double f0 = cols[i].X + cols[i].W / 2, f1 = cols[i + 1].X - cols[i + 1].W / 2, lc = f1 - f0;
            if (lc <= 0) continue;
            if (top) { Add(z, f0 + lcr, f0 + lc / 4); Add(z, f1 - lc / 4, f1 - lcr); }
            else Add(z, f0 + Math.Max(lc / 3, lcr), f1 - Math.Max(lc / 3, lcr));
        }
        return z;
    }

    private static void Add(List<(double, double)> z, double a, double b)
    {
        if (b - a > 1e-3) z.Add((a, b));
    }

    /// <summary>Chuỗi mô tả các vùng cho phép (m).</summary>
    public static string Describe(IEnumerable<(double A, double B)> zones) =>
        zones == null ? "mọi vị trí" : string.Join("; ", zones.Select(z => DesignUtil.F(z.A, "0.00") + "–" + DesignUtil.F(z.B, "0.00")));
}
