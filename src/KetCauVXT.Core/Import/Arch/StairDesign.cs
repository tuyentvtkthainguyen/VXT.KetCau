#nullable enable
namespace KetCauVXT.Import.Arch;

/// <summary>
/// Thiết kế cầu thang theo quy ước nhà ở thông thường:
///  • mỗi tầng 21 bậc đứng (h = chiều cao tầng / 21; tầng 3,3–3,9 m → h ≈ 157–186 mm);
///  • ưu tiên thang chữ U 3 vế: vế 1 – vế giữa (2–4 bậc, chạy ngang giữa hai chiếu nghỉ góc) – vế 3, hai vế dài bằng nhau (hoặc lệch 1 bậc);
///  • mặt bậc b lấy theo chiều dài lồng thang, 250–300 mm, 2h + b trong 580–670 mm.
/// Toạ độ m, cùng hệ toạ độ phương án. Lồng thang = hình chữ nhật mặt trong tường (mặt tường, không phải tim tường).
/// </summary>
public static class StairDesign
{
    public const int DefaultRisers = 21;

    public sealed class U3Options
    {
        public int Risers { get; set; } = DefaultRisers;
        /// <summary>Số bậc vế giữa (2–4)</summary>
        public int Middle { get; set; } = 3;
        /// <summary>Bề rộng vế (m); 0 = tự tính (≤ nửa bề rộng lồng, 0,9–1,2 m)</summary>
        public double Width { get; set; }
        /// <summary>Hướng đi lên của vế 1 dọc cạnh dài: "+X", "-X", "+Y", "-Y"</summary>
        public string Run1Dir { get; set; } = "-Y";
        /// <summary>Vế 1 nằm sát cạnh có toạ độ lớn (true) hay nhỏ (false) theo phương ngang</summary>
        public bool Run1AtMax { get; set; } = true;
        /// <summary>Mặt bậc mong muốn tối đa (m)</summary>
        public double MaxTread { get; set; } = 0.30;
    }

    private static double R(double v) => Math.Round(v, 3);

    /// <summary>Lập thang U 3 vế trong lồng thang [x0, x1] × [y0, y1]. Trả về thang + các ghi chú (lỗi bắt đầu bằng "Lỗi:").</summary>
    public static (RvStair stair, List<string> notes) U3(string id, RvLevel baseLv, RvLevel topLv, double x0, double y0, double x1, double y1, U3Options? o = null)
    {
        o ??= new U3Options();
        var notes = new List<string>();
        if (x1 < x0) (x0, x1) = (x1, x0);
        if (y1 < y0) (y0, y1) = (y1, y0);
        string dir = (o.Run1Dir ?? "-Y").Replace("−", "-").Trim().ToUpperInvariant();
        bool alongY = dir.EndsWith("Y");
        double L = alongY ? y1 - y0 : x1 - x0;   // chiều dài lồng theo phương vế 1, 3
        double W = alongY ? x1 - x0 : y1 - y0;   // chiều ngang lồng
        double H = topLv.Elevation - baseLv.Elevation;
        int n = Math.Max(6, o.Risers), m = Math.Clamp(o.Middle, 2, 4);
        int n1 = (n - m + 1) / 2, n3 = n - m - n1;
        double w = o.Width > 0.3 ? o.Width : Math.Clamp(Math.Floor((W - 0.05) / 2 / 0.05) * 0.05, 0.8, 1.1);
        if (2 * w > W + 1e-6) { notes.Add($"Lỗi: lồng thang rộng {W:0.00} m không đủ hai vế rộng {w:0.00} m."); w = W / 2; }
        double rise = H / n;
        // mặt bậc: vế dài + chiếu nghỉ góc (sâu ≥ bề rộng vế) nằm trong chiều dài lồng
        double b = Math.Min(o.MaxTread, (L - w) / (Math.Max(n1, n3) - 1));
        b = Math.Floor(b * 200) / 200; // làm tròn xuống 5 mm
        if (b < 0.25 - 1e-6) notes.Add($"Lỗi: lồng thang dài {L:0.00} m chỉ cho mặt bậc {b * 1000:0} mm (< 250) với {n} bậc – tăng chiều dài lồng hoặc dùng vế giữa nhiều bậc hơn.");
        double g = 2 * rise + b;
        if (rise > 0.19 + 1e-6 || rise < 0.13) notes.Add($"Lỗi: chiều cao bậc {rise * 1000:0} mm ngoài 130–190 mm.");
        else if (g < 0.58 || g > 0.67) notes.Add($"Cảnh báo: 2h + b = {g * 1000:0} mm (nên 600–650 mm).");
        // vế giữa căn giữa phần ngang còn lại giữa hai vế
        double midLen = (m - 1) * b;
        double gap = W - 2 * w;
        if ((W - midLen) / 2 < w - 1e-6) notes.Add("Cảnh báo: vế giữa dài, chiếu nghỉ góc hẹp hơn bề rộng vế – giảm số bậc vế giữa.");
        double landD = Math.Max(w, L - (Math.Max(n1, n3) - 1) * b); // chiều sâu chiếu nghỉ theo phương dọc

        // hệ toạ độ cục bộ: u dọc (theo hướng đi lên của vế 1), v ngang (vế 1 tại phía Run1AtMax)
        bool neg = dir.StartsWith("-");
        // điểm (u, v) → (x, y); u = 0 tại đầu chân vế 1 (mép lồng phía chân), tăng theo hướng đi lên vế 1
        (double, double) P(double u, double v)
        {
            if (alongY)
            {
                double y = neg ? y1 - u : y0 + u;
                double x = o.Run1AtMax ? x1 - v : x0 + v;
                return (R(x), R(y));
            }
            else
            {
                double x = neg ? x1 - u : x0 + u;
                double y = o.Run1AtMax ? y1 - v : y0 + v;
                return (R(x), R(y));
            }
        }
        double uTop = L - landD;                  // cuối vế 1 / đầu chiếu nghỉ
        double uStart1 = uTop - (n1 - 1) * b;     // bậc đầu vế 1
        double uStart3 = uTop - (n3 - 1) * b;     // vế 3 đi ngược lại
        // vế giữa chạy từ phía vế 1 (v nhỏ) sang phía vế 3 (v lớn): v từ (W − midLen)/2 tới (W + midLen)/2
        double va = (W - midLen) / 2, vb = (W + midLen) / 2;
        double uMid = L - landD / 2;
        var s = new RvStair { Id = id, BaseLevel = baseLv.Name, TopLevel = topLv.Name, Shape = "3 vế chữ U", Width = R(w) };
        var c0 = P(0, 0); var c1 = P(L, W);
        s.Outline = new List<double[]> { new[] { Math.Min(c0.Item1, c1.Item1), Math.Min(c0.Item2, c1.Item2) }, new[] { Math.Max(c0.Item1, c1.Item1), Math.Min(c0.Item2, c1.Item2) }, new[] { Math.Max(c0.Item1, c1.Item1), Math.Max(c0.Item2, c1.Item2) }, new[] { Math.Min(c0.Item1, c1.Item1), Math.Max(c0.Item2, c1.Item2) } };
        var a1 = P(uStart1, w / 2); var b1 = P(uTop, w / 2);
        var a2 = P(uMid, va); var b2 = P(uMid, vb);
        var a3 = P(uTop, W - w / 2); var b3 = P(uStart3, W - w / 2);
        s.Runs.Add(new RvStairRun { X1 = a1.Item1, Y1 = a1.Item2, X2 = b1.Item1, Y2 = b1.Item2, Width = R(w), Risers = n1 });
        s.Runs.Add(new RvStairRun { X1 = a2.Item1, Y1 = a2.Item2, X2 = b2.Item1, Y2 = b2.Item2, Width = R(landD), Risers = m });
        s.Runs.Add(new RvStairRun { X1 = a3.Item1, Y1 = a3.Item2, X2 = b3.Item1, Y2 = b3.Item2, Width = R(w), Risers = n3 });
        List<double[]> Rect(double ua, double ub, double v0, double v1)
        {
            var p = P(ua, v0); var q = P(ub, v1);
            return new List<double[]> { new[] { Math.Min(p.Item1, q.Item1), Math.Min(p.Item2, q.Item2) }, new[] { Math.Max(p.Item1, q.Item1), Math.Min(p.Item2, q.Item2) }, new[] { Math.Max(p.Item1, q.Item1), Math.Max(p.Item2, q.Item2) }, new[] { Math.Min(p.Item1, q.Item1), Math.Max(p.Item2, q.Item2) } };
        }
        s.Landings.Add(Rect(uTop, L, 0, va));
        s.Landings.Add(Rect(uTop, L, vb, W));
        notes.Add($"{id}: {n} bậc ({n1} + {m} + {n3}), h = {rise * 1000:0.0} mm, b = {b * 1000:0} mm, 2h + b = {g * 1000:0} mm, vế rộng {w * 1000:0} mm, chiếu nghỉ sâu {landD * 1000:0} mm.");
        return (s, notes);
    }
}
