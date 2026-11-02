using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

/// <summary>Sơ đồ bố trí cọc trong đài và các quy định cấu tạo (TCVN 10304:2014 mục 8.13 – dạng thực hành):
/// <list type="bullet">
/// <item>Khoảng cách tim cọc s ≥ 3d với cọc ma sát (cọc treo); cho phép ≥ 1,5d với cọc chống (chỉ cảnh báo); s &lt; 1,5d: không đạt.</item>
/// <item>Khoảng cách từ mép cọc tới mép đài ≥ 150 mm và từ tim cọc tới mép đài ≥ 0,7d.</item>
/// <item>Trọng tâm nhóm cọc nên trùng tim cột (lệch &gt; max(50 mm; 0,1s) → cảnh báo lệch tâm).</item>
/// <item>Hiệu ứng nhóm (Converse–Labarre) khi s &lt; 6d: η = 1 − θ·[(n−1)·m + (m−1)·n]/(90·m·n), θ = arctg(d/s) (độ).</item>
/// </list>
/// Toạ độ cọc luôn tính so với tim cột (= tâm đài).</summary>
public static class PileLayout
{
    /// <summary>Khoảng cách tim cọc tối thiểu (bội số d) với cọc ma sát</summary>
    public const double MinSpacingFriction = 3.0;
    /// <summary>Khoảng cách tim cọc tuyệt đối tối thiểu (bội số d) – cọc chống</summary>
    public const double MinSpacingEndBearing = 1.5;
    /// <summary>Khoảng cách tim cọc trên đó không xét hiệu ứng nhóm (bội số d)</summary>
    public const double GroupSpacingLimit = 6.0;
    /// <summary>Khoảng cách tối thiểu mép cọc – mép đài (m)</summary>
    public const double MinEdgeClear = 0.15;
    /// <summary>Khoảng cách tối thiểu tim cọc – mép đài (bội số d)</summary>
    public const double MinEdgeCentre = 0.7;

    /// <summary>Các sơ đồ cho hộp thoại "Bố trí cọc…" (theo thứ tự hiển thị).</summary>
    public static readonly PilePattern[] Standard =
    {
        PilePattern.P1, PilePattern.P2, PilePattern.P3, PilePattern.P4, PilePattern.P5, PilePattern.P6,
        PilePattern.P7, PilePattern.P8, PilePattern.P9, PilePattern.Grid
    };

    public static string Name(PilePattern p) => p switch
    {
        PilePattern.Auto => "Tự chọn (số cọc tối thiểu)",
        PilePattern.P1 => "1 cọc",
        PilePattern.P2 => "2 cọc thẳng hàng",
        PilePattern.P3 => "3 cọc tam giác",
        PilePattern.P4 => "4 cọc hình vuông",
        PilePattern.P5 => "5 cọc hoa mai",
        PilePattern.P6 => "6 cọc chữ nhật 2×3",
        PilePattern.P7 => "7 cọc lục giác",
        PilePattern.P8 => "8 cọc (3×3 bỏ giữa)",
        PilePattern.P9 => "9 cọc lưới 3×3",
        PilePattern.Grid => "Lưới n×m tuỳ chọn",
        PilePattern.Custom => "Toạ độ cọc tuỳ chọn",
        _ => p.ToString()
    };

    /// <summary>Số cọc của sơ đồ (Grid: Nx·Ny; Custom: số toạ độ).</summary>
    public static int Count(PilePattern p, int nx = 2, int ny = 2, int custom = 0) => p switch
    {
        PilePattern.Grid => Math.Max(1, nx) * Math.Max(1, ny),
        PilePattern.Custom => custom,
        PilePattern.Auto => 0,
        _ => (int)p
    };

    /// <summary>Toạ độ tim cọc (m, so với tim cột) của sơ đồ pat.
    /// sx, sy: khoảng cách tim cọc (sơ đồ chuẩn dùng sx); longX: cạnh dài theo X (chỉ áp dụng cho sơ đồ chuẩn, lưới Grid giữ nguyên Nx theo X).</summary>
    public static List<(double X, double Y)> Generate(PilePattern pat, double sx, double sy, int nx, int ny, bool longX, IList<PilePoint> custom = null)
    {
        sx = Math.Max(sx, 0.05);
        sy = sy > 0.05 ? sy : sx;
        List<(double X, double Y)> pts;
        switch (pat)
        {
            case PilePattern.Grid:
            {
                pts = new List<(double X, double Y)>();
                int m = Math.Max(1, nx), k = Math.Max(1, ny);
                for (int j = 0; j < k; j++)
                    for (int i = 0; i < m; i++)
                        pts.Add(((i - (m - 1) / 2.0) * sx, (j - (k - 1) / 2.0) * sy));
                break;
            }
            case PilePattern.Custom:
                pts = (custom ?? new List<PilePoint>()).Where(q => q != null).Select(q => (q.X, q.Y)).ToList();
                break;
            case PilePattern.P7:
            {
                // lục giác đều bán kính s quanh cọc giữa: mọi khoảng cách kề nhau = s
                pts = new List<(double X, double Y)> { (0, 0) };
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3;
                    pts.Add((sx * Math.Cos(a), sx * Math.Sin(a)));
                }
                if (!longX) pts = pts.Select(q => (q.Y, q.X)).ToList();
                break;
            }
            case PilePattern.Auto:
                pts = new List<(double X, double Y)>();
                break;
            default:
                pts = PileCapDesigner.Layout((int)pat, sx, longX);
                break;
        }
        return pts.Select(q => (Math.Round(q.X, 6) + 0.0, Math.Round(q.Y, 6) + 0.0)).ToList();
    }

    /// <summary>Bố trí do người dùng chọn tại nút chân cột (null nếu không có).</summary>
    public static PileCapOverride Find(FoundationSettings fs, int nodeId) =>
        fs?.CapOverrides?.FirstOrDefault(o => o != null && o.NodeId == nodeId);

    /// <summary>Ghi / thay bố trí cho các nút; ovr = null → xoá (trở về tự chọn).</summary>
    public static void Set(FoundationSettings fs, IEnumerable<int> nodeIds, PileCapOverride ovr)
    {
        if (fs == null) return;
        fs.CapOverrides ??= new List<PileCapOverride>();
        foreach (int id in nodeIds.Distinct())
        {
            fs.CapOverrides.RemoveAll(o => o == null || o.NodeId == id);
            if (ovr == null) continue;
            var c = ovr.Clone();
            c.NodeId = id;
            fs.CapOverrides.Add(c);
        }
    }

    /// <summary>true nếu bản ghi có thay đổi thật (sơ đồ khác Auto hoặc có kích thước đài).</summary>
    public static bool IsActive(PileCapOverride o) =>
        o != null && (o.Pattern != PilePattern.Auto || o.L > 0 || o.B > 0 || o.H > 0)
        && !(o.Pattern == PilePattern.Custom && (o.CustomPiles == null || o.CustomPiles.Count == 0));

    /// <summary>Kích thước đài tự chọn (đài đặt tâm tại tim cột): L = 2·max|x| + d + 2·mép, ≥ hc + 200 mm, làm tròn 50 mm.</summary>
    public static (double L, double B) CapSize(IList<(double X, double Y)> piles, double d, double edge, double hc, double bc)
    {
        double ax = piles.Count > 0 ? piles.Max(q => Math.Abs(q.X)) : 0, ay = piles.Count > 0 ? piles.Max(q => Math.Abs(q.Y)) : 0;
        double L = RcSection.CeilTo(Math.Max(2 * ax + d + 2 * edge, hc + 0.20), 0.05);
        double B = RcSection.CeilTo(Math.Max(2 * ay + d + 2 * edge, bc + 0.20), 0.05);
        return (L, B);
    }

    /// <summary>Khoảng cách tim cọc nhỏ nhất (m); 0 nếu &lt; 2 cọc.</summary>
    public static double MinSpacing(IList<(double X, double Y)> piles)
    {
        double best = double.MaxValue;
        for (int i = 0; i < piles.Count; i++)
            for (int j = i + 1; j < piles.Count; j++)
            {
                double dx = piles[i].X - piles[j].X, dy = piles[i].Y - piles[j].Y;
                best = Math.Min(best, Math.Sqrt(dx * dx + dy * dy));
            }
        return best == double.MaxValue ? 0 : best;
    }

    /// <summary>Khoảng cách nhỏ nhất từ tim cọc tới mép đài L × B (đài tâm tại gốc), m.</summary>
    public static double MinEdgeDistance(IList<(double X, double Y)> piles, double L, double B)
    {
        double best = double.MaxValue;
        foreach (var q in piles) best = Math.Min(best, Math.Min(L / 2 - Math.Abs(q.X), B / 2 - Math.Abs(q.Y)));
        return best == double.MaxValue ? 0 : best;
    }

    /// <summary>Số hàng m (theo các toạ độ Y khác nhau) và số cọc trung bình mỗi hàng n của nhóm cọc.</summary>
    public static (int Rows, int PerRow) RowsOf(IList<(double X, double Y)> piles)
    {
        if (piles.Count == 0) return (0, 0);
        int m = piles.Select(q => Math.Round(q.Y, 2)).Distinct().Count();
        int n = Math.Max(1, (int)Math.Round(piles.Count / (double)Math.Max(m, 1)));
        return (m, n);
    }

    /// <summary>Hệ số nhóm Converse–Labarre: η = 1 − θ·[(n−1)·m + (m−1)·n]/(90·m·n), θ = arctg(d/s) (độ);
    /// m hàng, n cọc mỗi hàng; s ≥ 6d hoặc 1 cọc → η = 1.</summary>
    public static double GroupEfficiency(int m, int n, double s, double d)
    {
        if (m * n <= 1 || s <= 0 || d <= 0 || s >= GroupSpacingLimit * d - 1e-9) return 1.0;
        double theta = Math.Atan(d / s) * 180 / Math.PI;
        double eta = 1 - theta * ((n - 1) * m + (m - 1) * n) / (90.0 * m * n);
        return Math.Clamp(eta, 0.3, 1.0);
    }

    /// <summary>Kiểm tra quy định cấu tạo bố trí cọc: khoảng cách tim cọc, khoảng cách tới mép đài, lệch tâm nhóm cọc so với tim cột.</summary>
    /// <param name="ox">[MóngLệchTâm] tâm đài − tim cột theo X (m); cọc vẫn tính so với tim cột</param>
    /// <param name="boundary">[MóngLệchTâm] true = nhóm cọc lệch tâm do ranh giới đất (trạng thái chủ ý: "Lệch tâm do ranh giới", không cảnh báo)</param>
    public static List<SafetyItem> CheckRules(IList<(double X, double Y)> piles, double d, double L, double B, double ox = 0, double oy = 0, bool boundary = false)
    {
        var res = new List<SafetyItem>();
        if (piles == null || piles.Count == 0)
        {
            res.Add(new SafetyItem
            {
                Group = "Bố trí cọc", Name = "Có cọc trong đài", Value = 0, Limit = 1, Unit = "cọc", Status = CheckStatus.Fail,
                Explanation = "Sơ đồ không có cọc nào – chọn lại sơ đồ bố trí."
            });
            return res;
        }
        if (piles.Count > 1)
        {
            double s = MinSpacing(piles);
            var st = s >= MinSpacingFriction * d - 1e-6 ? CheckStatus.Ok : s >= MinSpacingEndBearing * d - 1e-6 ? CheckStatus.Warning : CheckStatus.Fail;
            res.Add(new SafetyItem
            {
                Group = "Bố trí cọc", Name = "Khoảng cách tim cọc s ≥ 3d", Value = s * 1000, Limit = MinSpacingFriction * d * 1000, Unit = "mm",
                Status = st, Inverse = true, Formula = "s ≥ 3d (cọc ma sát); ≥ 1,5d (cọc chống) – TCVN 10304:2014 mục 8.13",
                Explanation = st switch
                {
                    CheckStatus.Ok => "Khoảng cách tim cọc đủ lớn để các cọc ma sát làm việc độc lập, thi công ép/đóng không làm trồi cọc bên cạnh.",
                    CheckStatus.Warning => "s < 3d: chỉ chấp nhận với cọc chống (mũi tựa lên đá / lớp rất chặt). Với cọc ma sát sức chịu tải nhóm giảm mạnh – nên tăng khoảng cách.",
                    _ => "s < 1,5d: cọc quá sát nhau, không thi công được và vùng ứng suất chồng lấn – bắt buộc tăng khoảng cách."
                }
            });
        }
        double clear = double.MaxValue, centre = double.MaxValue;
        foreach (var q in piles)
        {
            double e = Math.Min(L / 2 - Math.Abs(q.X - ox), B / 2 - Math.Abs(q.Y - oy));   // [MóngLệchTâm] so với tâm đài
            centre = Math.Min(centre, e);
            clear = Math.Min(clear, e - d / 2);
        }
        double need = Math.Max(MinEdgeClear, MinEdgeCentre * d - d / 2);
        bool okEdge = clear >= need - 1e-6;
        res.Add(new SafetyItem
        {
            Group = "Bố trí cọc", Name = "Mép cọc tới mép đài ≥ max(150 mm; 0,7d − d/2)", Value = clear * 1000, Limit = need * 1000, Unit = "mm",
            Status = okEdge ? CheckStatus.Ok : CheckStatus.Fail, Inverse = true,
            Formula = "a_mép ≥ 150 mm và khoảng cách tim cọc – mép đài ≥ 0,7d",
            Explanation = okEdge
                ? "Đủ lớp bê tông bao quanh đầu cọc, cọc biên không chọc thủng / phá vỡ mép đài."
                : "Cọc quá sát mép đài: mép đài dễ bị phá hoại do cọc biên (chọc thủng góc), không đủ chỗ neo thép – tăng kích thước đài."
        });
        double cx = piles.Average(q => q.X), cy = piles.Average(q => q.Y);
        double ecc = Math.Sqrt(cx * cx + cy * cy);
        double tol = Math.Max(0.05, 0.1 * Math.Max(MinSpacing(piles), d));
        if (boundary && ecc > tol + 1e-9)   // [MóngLệchTâm] lệch tâm chủ ý do ranh giới đất
        {
            res.Add(new SafetyItem
            {
                Group = "Bố trí cọc", Name = "Lệch tâm nhóm cọc so với tim cột – Lệch tâm do ranh giới", Value = ecc * 1000, Limit = tol * 1000, Unit = "mm",
                Status = CheckStatus.Info, Formula = $"e = √(x̄² + ȳ²) = {ecc * 1000:0} mm, x̄ = {cx * 1000:0}, ȳ = {cy * 1000:0} mm",
                Explanation = "Lệch tâm do ranh giới: đài và cọc phải nằm trong ranh giới đất nên trọng tâm nhóm cọc lệch khỏi tim cột (trạng thái chủ ý). "
                              + "Mô men N·e đã kể vào phản lực cọc (hoặc do dầm móng cân bằng tiếp nhận khi có dầm DM)."
            });
            return res;
        }
        res.Add(new SafetyItem
        {
            Group = "Bố trí cọc", Name = "Lệch tâm nhóm cọc so với tim cột", Value = ecc * 1000, Limit = tol * 1000, Unit = "mm",
            Status = ecc <= tol + 1e-9 ? CheckStatus.Ok : CheckStatus.Warning,
            Formula = "e = √(x̄² + ȳ²), x̄ = Σxi/n",
            Explanation = ecc <= tol + 1e-9
                ? "Trọng tâm nhóm cọc trùng (gần) tim cột – tải đứng chia đều cho các cọc."
                : "Trọng tâm nhóm cọc lệch khỏi tim cột: lực dọc N sinh thêm mô men N·e làm tăng phản lực cọc phía lệch (đã kể vào Pi). Nên bố trí đối xứng."
        });
        return res;
    }
}
