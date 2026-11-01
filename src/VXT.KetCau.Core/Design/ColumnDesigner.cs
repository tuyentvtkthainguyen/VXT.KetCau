using System;
using System.Collections.Generic;
using System.Linq;
using VXT.KetCau.Core.Codes;
using VXT.KetCau.Core.Model;

namespace VXT.KetCau.Core.Design
{
    public class ColumnDesignResult
    {
        public double B { get; set; }
        public double H { get; set; }
        /// <summary>Số thanh theo cạnh b (kể cả thanh góc) và theo cạnh h (kể cả thanh góc).</summary>
        public int Nb { get; set; }
        public int Nh { get; set; }
        public int D { get; set; }
        public int TotalBars => 2 * Nb + 2 * (Nh - 2);
        public double As => TotalBars * Tcvn5574.BarArea(D);
        public double Mu => As / (B * H);
        public StirrupSpec Stirrup { get; set; }
        public StirrupSpec StirrupDense { get; set; }
        /// <summary>Hệ số sử dụng lớn nhất (≤ 1 là đạt).</summary>
        public double Ratio { get; set; }
        public string GoverningCombo { get; set; }
        public double Lambda { get; set; }
        public List<string> Warnings { get; } = new List<string>();
        public bool Ok => Ratio <= 1.0;
        public string BarsText(string sym = "Ø") => $"{TotalBars}{sym}{D}";
    }

    /// <summary>
    /// Thiết kế cột BTCT chữ nhật chịu nén lệch tâm xiên theo mô hình biến dạng phi tuyến
    /// (TCVN 5574:2018 mục 8.1.2.7, biểu đồ hai đoạn thẳng của bê tông, thép đàn dẻo).
    /// Kể uốn dọc bằng hệ số η (8.1.2.4) và độ lệch tâm ngẫu nhiên ea.
    /// </summary>
    public class ColumnDesigner
    {
        readonly ConcreteGrade C;
        readonly SteelGrade S;
        readonly double cover;
        /// <summary>Hệ số chiều dài tính toán ψ (khung nhiều tầng, sàn đổ toàn khối: 0.7).</summary>
        public double Psi { get; set; } = 0.7;
        public int FiberCount { get; set; } = 20;

        public ColumnDesigner(ConcreteGrade c, SteelGrade s, double cover) { C = c; S = s; this.cover = cover; }

        struct Bar { public double X, Y, A; }

        List<Bar> Layout(double b, double h, int nb, int nh, int d, int dsw)
        {
            var bars = new List<Bar>();
            double a = cover + dsw + d / 2.0;
            double x0 = -b / 2 + a, x1 = b / 2 - a, y0 = -h / 2 + a, y1 = h / 2 - a;
            double A = Tcvn5574.BarArea(d);
            for (int i = 0; i < nb; i++)
            {
                double x = nb == 1 ? 0 : x0 + (x1 - x0) * i / (nb - 1);
                bars.Add(new Bar { X = x, Y = y0, A = A });
                bars.Add(new Bar { X = x, Y = y1, A = A });
            }
            for (int j = 1; j < nh - 1; j++)
            {
                double y = y0 + (y1 - y0) * j / (nh - 1);
                bars.Add(new Bar { X = x0, Y = y, A = A });
                bars.Add(new Bar { X = x1, Y = y, A = A });
            }
            return bars;
        }

        double SigmaC(double eps)
        {
            // nén dương; biểu đồ hai đoạn thẳng
            if (eps <= 0) return 0;
            double e1 = Tcvn5574.EpsB1Red;
            if (eps < e1) return C.Rb * eps / e1;
            if (eps <= Tcvn5574.EpsB2) return C.Rb;
            return 0;
        }

        double SigmaS(double eps)
        {
            double s = eps * S.Es;
            return Math.Max(-S.Rs, Math.Min(S.Rsc, s));
        }

        /// <summary>
        /// Tích phân nội lực tiết diện với trục trung hoà góc θ, biến dạng thớ nén ngoài cùng εb2.
        /// c: khoảng cách từ thớ nén ngoài cùng tới trục trung hoà (mm). Trả về N (kN, nén +), Mx, My (kN·m).
        /// </summary>
        (double N, double Mx, double My) Integrate(double b, double h, List<Bar> bars, double theta, double c)
        {
            // trục u theo phương vuông góc trục trung hoà (hướng về thớ nén)
            double ux = Math.Cos(theta), uy = Math.Sin(theta);
            // toạ độ u lớn nhất của tiết diện
            double umax = Math.Abs(ux) * b / 2 + Math.Abs(uy) * h / 2;
            double uNA = umax - c;
            double curv = Tcvn5574.EpsB2 / Math.Max(c, 1e-6);
            int n = FiberCount;
            double dx = b / n, dy = h / n;
            double N = 0, Mx = 0, My = 0;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    double x = -b / 2 + dx * (i + 0.5), y = -h / 2 + dy * (j + 0.5);
                    double eps = curv * (x * ux + y * uy - uNA);
                    double sg = SigmaC(eps);
                    if (sg == 0) continue;
                    double f = sg * dx * dy;
                    N += f; Mx += f * y; My += f * x;
                }
            foreach (var br in bars)
            {
                double eps = curv * (br.X * ux + br.Y * uy - uNA);
                double sc = SigmaC(eps);
                double f = (SigmaS(eps) - sc) * br.A; // trừ phần bê tông bị thép chiếm chỗ
                N += f; Mx += f * br.Y; My += f * br.X;
            }
            return (N / 1e3, Mx / 1e6, My / 1e6);
        }

        /// <summary>
        /// Hệ số sử dụng khả năng chịu lực với bộ (N, Mx, My): tìm mô men giới hạn cùng phương với mô men tác dụng
        /// tại đúng lực dọc N. Ratio = |M| / |Mu|; nếu N vượt khả năng nén đúng tâm, trả về N/Nu.
        /// </summary>
        public double CapacityRatio(double b, double h, int nb, int nh, int d, int dsw, double N, double Mx, double My)
        {
            var bars = Layout(b, h, nb, nh, d, dsw);
            double As = bars.Sum(x => x.A);
            double N0 = (C.Rb * (b * h - As) + S.Rsc * As) / 1e3;
            double Nt = -S.Rs * As / 1e3;
            if (N >= N0) return N / N0;
            if (N <= Nt) return N / Nt;
            double Mapp = Math.Sqrt(Mx * Mx + My * My);
            if (Mapp < 1e-6) return Math.Max(N / N0, N / Nt);
            double target = Math.Atan2(Mx, My); // góc của vector mô men tác dụng trong hệ (My, Mx)

            Func<double, (double Mx, double My)> atTheta = theta =>
            {
                double umax = Math.Abs(Math.Cos(theta)) * b / 2 + Math.Abs(Math.Sin(theta)) * h / 2;
                double lo = 1e-3, hi = 20 * 2 * umax;
                for (int it = 0; it < 50; it++)
                {
                    double mid = (lo + hi) / 2;
                    var r = Integrate(b, h, bars, theta, mid);
                    if (r.N > N) hi = mid; else lo = mid;
                }
                var f = Integrate(b, h, bars, theta, (lo + hi) / 2);
                return (f.Mx, f.My);
            };

            // tìm θ để góc mô men kết quả trùng với mô men tác dụng
            double best = double.MaxValue, bestM = 0;
            for (int k = 0; k < 36; k++)
            {
                double th = k * Math.PI * 2 / 36;
                var m = atTheta(th);
                double ang = Math.Atan2(m.Mx, m.My);
                double diff = Math.Abs(Math.IEEERemainder(ang - target, 2 * Math.PI));
                if (diff < best) { best = diff; bestM = th; }
            }
            double a0 = bestM - Math.PI / 18, a1 = bestM + Math.PI / 18;
            for (int it = 0; it < 30; it++)
            {
                double m1 = a0 + (a1 - a0) / 3, m2 = a1 - (a1 - a0) / 3;
                var r1 = atTheta(m1); var r2 = atTheta(m2);
                double d1 = Math.Abs(Math.IEEERemainder(Math.Atan2(r1.Mx, r1.My) - target, 2 * Math.PI));
                double d2 = Math.Abs(Math.IEEERemainder(Math.Atan2(r2.Mx, r2.My) - target, 2 * Math.PI));
                if (d1 < d2) a1 = m2; else a0 = m1;
            }
            var mu = atTheta((a0 + a1) / 2);
            double Mu = Math.Sqrt(mu.Mx * mu.Mx + mu.My * mu.My);
            return Mu < 1e-6 ? 99 : Mapp / Mu;
        }

        /// <summary>
        /// Hệ số uốn dọc η = 1/(1 − N/Ncr), Ncr = π²D/l0², D = kb·Eb·I + ks·Es·Is (8.1.2.4).
        /// </summary>
        public double Eta(double N, double M, double b, double h, double l0, double As)
        {
            if (N <= 0) return 1;
            double I = b * h * h * h / 12;
            double a = cover + 25;
            double Is = As * Math.Pow(h / 2 - a, 2);
            double e0 = Math.Abs(M) * 1e3 / N; // mm
            double de = Math.Max(0.15, Math.Min(1.5, e0 / h));
            double phiL = 2.0; // thiên về an toàn: toàn bộ là tải dài hạn
            double kb = 0.15 / (phiL * (0.3 + de));
            double D = kb * C.Eb * I + 0.7 * S.Es * Is;
            double Ncr = Math.PI * Math.PI * D / (l0 * l0) / 1e3; // kN
            if (N >= Ncr) return 99;
            return 1 / (1 - N / Ncr);
        }

        /// <summary>Thiết kế cột với các tổ hợp nội lực, chọn số thanh/đường kính nhỏ nhất đạt yêu cầu.</summary>
        public ColumnDesignResult Design(double b, double h, double length, IList<ColumnForceSet> sets, int[] dias, int[] stirrupDias)
        {
            var res = new ColumnDesignResult { B = b, H = h };
            double l0 = Psi * length;
            double iMin = Math.Min(b, h) / Math.Sqrt(12);
            res.Lambda = l0 / iMin;
            if (res.Lambda > 120) res.Warnings.Add($"Độ mảnh λ = {res.Lambda:0} > 120 — vượt giới hạn (10.2.2)");
            double muMin = Math.Max(Tcvn5574.MuMinColumn(res.Lambda), 0.004); // thực hành: ≥ 0.4% với khung nhà
            int dsw = 8;

            var candidates = new List<(int nb, int nh, int d)>();
            foreach (var d in dias.OrderBy(x => x))
                for (int nb = 2; nb <= 8; nb++)
                    for (int nh = 2; nh <= 8; nh++)
                    {
                        double clear = Math.Max(50, d) ; // khoảng hở thanh cột ≥ 50 (đổ bê tông đứng)
                        double sb = (b - 2 * (cover + dsw) - d) / Math.Max(1, nb - 1);
                        double sh = (h - 2 * (cover + dsw) - d) / Math.Max(1, nh - 1);
                        if (nb > 1 && sb - d < clear) continue;
                        if (nh > 1 && sh - d < clear) continue;
                        if (sb > 400 || sh > 400) continue; // khoảng cách thanh dọc ≤ 400 (10.3.3)
                        // ưu tiên bố trí cân đối theo tỉ lệ cạnh
                        candidates.Add((nb, nh, d));
                    }
            var ordered = candidates
                .Select(c => (c.nb, c.nh, c.d, As: (2 * c.nb + 2 * (c.nh - 2)) * Tcvn5574.BarArea(c.d)))
                .Where(c => c.As >= muMin * b * h)
                .OrderBy(c => c.As).ThenBy(c => c.d).ToList();

            if (sets == null || sets.Count == 0)
            {
                var c0 = ordered.FirstOrDefault();
                res.Nb = c0.nb == 0 ? 2 : c0.nb; res.Nh = c0.nh == 0 ? 2 : c0.nh; res.D = c0.d == 0 ? dias.Min() : c0.d;
                res.Warnings.Add("Chưa có nội lực — bố trí theo hàm lượng tối thiểu");
            }
            else
            {
                bool found = false;
                // Lọc các cặp nội lực nguy hiểm để giảm khối lượng tính: Nmax, Nmin, |M3|max, |M2|max, e lớn nhất
                var crit = new HashSet<ColumnForceSet>
                {
                    sets.OrderByDescending(f => f.N).First(),
                    sets.OrderBy(f => f.N).First(),
                    sets.OrderByDescending(f => Math.Abs(f.M3)).First(),
                    sets.OrderByDescending(f => Math.Abs(f.M2)).First(),
                    sets.OrderByDescending(f => Math.Abs(f.M2) + Math.Abs(f.M3)).First(),
                    sets.OrderByDescending(f => (Math.Abs(f.M2) + Math.Abs(f.M3)) / Math.Max(1, Math.Abs(f.N))).First(),
                };
                foreach (var c in ordered)
                {
                    double worst = 0; string combo = null;
                    foreach (var f in crit)
                    {
                        // Kết cấu siêu tĩnh: e0 = max(M/N, ea) theo từng phương (8.1.2.2.2)
                        double eaH = Math.Max(length / 600, Math.Max(h / 30, 10)); // mm
                        double eaB = Math.Max(length / 600, Math.Max(b / 30, 10));
                        double nPos = Math.Max(0, f.N);
                        double m3 = Math.Max(Math.Abs(f.M3), nPos * eaH / 1e3);
                        double m2 = Math.Max(Math.Abs(f.M2), nPos * eaB / 1e3);
                        double eta3 = Eta(f.N, m3, b, h, l0, c.As / 2);          // uốn theo cạnh h
                        double eta2 = Eta(f.N, m2, h, b, l0, c.As / 2);          // uốn theo cạnh b
                        double mx = m3 * eta3;
                        double my = m2 * eta2;
                        double r = CapacityRatio(b, h, c.nb, c.nh, c.d, dsw, f.N, mx, my);
                        if (r > worst) { worst = r; combo = f.Combo + " " + f.Location; }
                    }
                    if (worst <= 1.0)
                    {
                        res.Nb = c.nb; res.Nh = c.nh; res.D = c.d; res.Ratio = worst; res.GoverningCombo = combo; found = true; break;
                    }
                }
                if (!found)
                {
                    var c = ordered.LastOrDefault();
                    res.Nb = c.nb == 0 ? 2 : c.nb; res.Nh = c.nh == 0 ? 2 : c.nh; res.D = c.d == 0 ? dias.Max() : c.d; res.Ratio = 9.99;
                    res.Warnings.Add("Không bố trí đủ thép trong giới hạn — tăng tiết diện cột");
                }
            }

            // Đai cột (10.3.4.3): d ≥ max(0.25·dmax, 6); s ≤ min(15d, 500); vùng nút / nối chồng: ≤ min(10d, 100…150)
            int dStir = Math.Max(stirrupDias.Min(), (int)Math.Ceiling(res.D / 4.0));
            dStir = stirrupDias.Where(x => x >= dStir).DefaultIfEmpty(stirrupDias.Max()).Min();
            double s = Math.Floor(Math.Min(15 * res.D, Math.Min(Math.Min(b, h), 400)) / 50) * 50;
            double sDense = Math.Max(100, Math.Floor(Math.Min(10.0 * res.D, 150) / 50) * 50);
            res.Stirrup = new StirrupSpec { D = dStir, Legs = 2, S = s };
            res.StirrupDense = new StirrupSpec { D = dStir, Legs = 2, S = Math.Min(sDense, s) };
            if (res.Mu > 0.03) res.Warnings.Add($"μ = {res.Mu * 100:0.00}% > 3% — nên tăng tiết diện");
            return res;
        }
    }
}
