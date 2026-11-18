#nullable enable
namespace KetCauVXT.Import.Arch;

/// <summary>
/// THIẾT KẾ CẦU THANG BỘ theo quy ước nhà ở: mỗi tầng 21 bậc (tầng cao 2,9–3,8 m), ưu tiên thang chữ U 3 vế –
/// hai vế bên bằng nhau, vế giữa 2–4 bậc, hai chiếu nghỉ góc; mặt bậc 260 mm (tối thiểu 250), vế rộng theo ô thang.
/// Đường tim vế: từ tâm bậc đầu → tâm bậc cuối theo hướng đi lên ((Risers − 1) mặt bậc), Risers = số bậc đứng của vế.
/// </summary>
public static class PlanStairs
{
    public sealed class Options
    {
        /// <summary>Số bậc mỗi tầng; 0 = theo quy ước (21 với tầng 2,9–3,8 m, khác thì cao tầng / 0,17)</summary>
        public int Risers { get; set; }
        /// <summary>Số bậc vế giữa mong muốn (2–4)</summary>
        public int Middle { get; set; } = 3;
        public double Tread { get; set; } = 0.26;
        public double MinTread { get; set; } = 0.25;
        /// <summary>Bề rộng vế; 0 = theo ô thang (tối đa 1,2 m, tối thiểu 0,9 m)</summary>
        public double Width { get; set; }
    }

    private static double R(double v) => Math.Round(v, 3);

    public static int ConventionRisers(double storeyH) =>
        storeyH >= 2.9 && storeyH <= 3.8 ? 21 : Math.Max(12, (int)Math.Round(storeyH / 0.17));

    /// <summary>Thiết kế lại mọi cầu thang có ô thang chữ nhật đủ rộng thành U 3 vế; trả về nhật ký.</summary>
    public static List<string> DesignAll(RevitExchange ex, Options? o = null)
    {
        o ??= new Options();
        var log = new List<string>();
        foreach (var t in ex.Stairs) log.Add(DesignU3(ex, t, o));
        return log;
    }

    /// <summary>
    /// Thang chữ U 3 vế trong ô thang (khung bao Outline): hai vế bên dọc cạnh dài, vế giữa ngang ở đầu xa, lối lên / lối ra ở cạnh gần.
    /// Cạnh lối lên: giữ theo vế 1 cũ (nếu có), không thì cạnh có nhiều phòng / sàn tầng bên ngoài nhất (ưu tiên cạnh không có tường).
    /// </summary>
    public static string DesignU3(RevitExchange ex, RvStair t, Options o)
    {
        var bl = ex.Levels.FirstOrDefault(l => l.Name == t.BaseLevel); var tl = ex.Levels.FirstOrDefault(l => l.Name == t.TopLevel);
        if (bl == null || tl == null || t.Outline.Count < 3) return $"Thang {t.Id}: thiếu cao độ / ô thang.";
        double H = tl.Elevation - bl.Elevation;
        int N = o.Risers > 0 ? o.Risers : ConventionRisers(H);
        double x0 = t.Outline.Min(p => p[0]), x1 = t.Outline.Max(p => p[0]), y0 = t.Outline.Min(p => p[1]), y1 = t.Outline.Max(p => p[1]);
        bool alongY = y1 - y0 >= x1 - x0;
        double L = alongY ? y1 - y0 : x1 - x0, Wb = alongY ? x1 - x0 : y1 - y0;
        double w = o.Width > 0 ? o.Width : Math.Min(1.0, Math.Floor((Wb - 0.1) / 2 / 0.05) * 0.05);
        if (w < 0.9 - 1e-6) return $"Thang {t.Id}: ô thang rộng {Wb:0.00} m – không đủ cho thang U (cần ≥ {2 * 0.9 + 0.1:0.00} m); giữ nguyên.";
        double G = Wb - 2 * w;

        // hướng lối lên: theo vế 1 cũ
        bool entryAtMax = true, runAAtMaxV = true;
        if (t.Runs.Count > 0)
        {
            var r0 = t.Runs[0];
            double su = alongY ? r0.Y1 : r0.X1, eu = alongY ? r0.Y2 : r0.X2, sv = alongY ? r0.X1 : r0.Y1;
            if (Math.Abs(eu - su) > 0.1) entryAtMax = su > eu;
            runAAtMaxV = sv > (alongY ? (x0 + x1) / 2 : (y0 + y1) / 2);
        }

        foreach (int mid in new[] { 2, 3, 4 }.OrderBy(m => Math.Abs(m - o.Middle)).ThenBy(m => m))
        {
            int nSide = N - mid, n1 = (nSide + 1) / 2, n3 = nSide - n1;
            foreach (double tread in new[] { o.Tread, (o.Tread + o.MinTread) / 2, o.MinTread })
            {
                double run1 = (n1 - 1) * tread, run3 = (n3 - 1) * tread, midLen = (mid - 1) * tread;
                if (midLen > G + 1e-6) continue;
                double e = Math.Min(0.1, L - Math.Max(run1, run3) - w);
                if (e < 0.02) continue;
                double uL = e + Math.Max(run1, run3); // chiếu nghỉ từ uL tới L (sâu ≥ w)
                double depth = L - uL;
                double s = (G - midLen) / 2;
                // toạ độ: u từ cạnh lối lên vào trong, v ngang (0 = cạnh vế 3, Wb = cạnh vế 1)
                (double x, double y) P(double u, double v)
                {
                    double uu = entryAtMax ? (alongY ? y1 - u : x1 - u) : (alongY ? y0 + u : x0 + u);
                    double vv = runAAtMaxV ? (alongY ? x0 + v : y0 + v) : (alongY ? x1 - v : y1 - v);
                    return alongY ? (vv, uu) : (uu, vv);
                }
                var runs = new List<RvStairRun>();
                void Run(double ua, double va, double ub, double vb, int n) { var a = P(ua, va); var b = P(ub, vb); runs.Add(new RvStairRun { X1 = R(a.x), Y1 = R(a.y), X2 = R(b.x), Y2 = R(b.y), Width = w, Risers = n }); }
                double vA = Wb - w / 2, vB = w / 2, uMid = uL + depth / 2;
                // vế 1: lối lên → chiếu nghỉ, bên A; vế 2: ngang từ A sang B; vế 3: chiếu nghỉ → lối ra, bên B
                Run(uL - run1, vA, uL, vA, n1);
                Run(uMid, Wb - w - s, uMid, w + s, mid);
                Run(uL, vB, uL - run3, vB, n3);
                List<double[]> Rect(double ua, double ub, double va, double vb)
                {
                    var c = new[] { P(ua, va), P(ub, va), P(ub, vb), P(ua, vb) };
                    return c.Select(p => new[] { R(p.x), R(p.y) }).ToList();
                }
                t.Runs = runs;
                t.Landings = new List<List<double[]>> { Rect(uL, L, Wb - w - s, Wb), Rect(uL, L, 0, w + s) };
                t.Shape = $"Chữ U 3 vế {n1}+{mid}+{n3} bậc, 2 chiếu nghỉ";
                t.Width = w;
                double h = H / N;
                return $"Thang {t.Id}: U 3 vế {n1}+{mid}+{n3} = {N} bậc, h = {h * 1000:0} mm, b = {tread * 1000:0} mm, 2h+b = {(2 * h + tread) * 1000:0} mm, vế rộng {w:0.00} m, chiếu nghỉ sâu {depth:0.00} m.";
            }
        }
        return $"Thang {t.Id}: ô thang {Wb:0.00} × {L:0.00} m không đủ dài cho U 3 vế {N} bậc – cần mở rộng ô thang hoặc tăng số vế.";
    }

    /// <summary>Cảnh báo theo quy ước: 21 bậc / tầng thường, ưu tiên U 3 vế, vế giữa 2–4 bậc.</summary>
    public static List<PlanIssue> Check(RevitExchange ex)
    {
        var r = new List<PlanIssue>();
        foreach (var t in ex.Stairs.Where(t => t.Runs.Count > 0))
        {
            var bl = ex.Levels.FirstOrDefault(l => l.Name == t.BaseLevel); var tl = ex.Levels.FirstOrDefault(l => l.Name == t.TopLevel);
            if (bl == null || tl == null) continue;
            double H = tl.Elevation - bl.Elevation;
            int n = t.Runs.Sum(x => x.Risers), conv = ConventionRisers(H);
            if (n != conv) r.Add(new PlanIssue { Message = $"Thang {t.Id}: {n} bậc – quy ước {conv} bậc cho tầng cao {H:0.00} m (chạy --thiet-ke-thang)." });
            if (t.Runs.Count != 3) r.Add(new PlanIssue { Message = $"Thang {t.Id}: {t.Runs.Count} vế – ưu tiên thang chữ U 3 vế." });
            else if (t.Runs[1].Risers < 2 || t.Runs[1].Risers > 4) r.Add(new PlanIssue { Message = $"Thang {t.Id}: vế giữa {t.Runs[1].Risers} bậc – quy ước 2–4 bậc." });
        }
        return r;
    }
}
