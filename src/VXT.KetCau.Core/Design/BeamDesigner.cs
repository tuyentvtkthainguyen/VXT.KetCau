using System;
using System.Collections.Generic;
using System.Linq;
using VXT.KetCau.Core.Codes;

namespace VXT.KetCau.Core.Design
{
    /// <summary>Phương án bố trí: n thanh Ø d.</summary>
    public class BarGroup
    {
        public int N { get; set; }
        public int D { get; set; }
        public double Area => N * Tcvn5574.BarArea(D);
        public BarGroup() { }
        public BarGroup(int n, int d) { N = n; D = d; }
        public string Text(string sym = "Ø") => N > 0 ? $"{N}{sym}{D}" : "-";
        public override string ToString() => Text();
    }

    /// <summary>Đai: Ø d, nhánh n, bước s.</summary>
    public class StirrupSpec
    {
        public int D { get; set; } = 6;
        public int Legs { get; set; } = 2;
        public double S { get; set; } = 150;
        public string Text(string sym = "Ø") => $"{sym}{D}a{S:0}";
        public override string ToString() => Text();
    }

    /// <summary>Kết quả tính thép tại một tiết diện.</summary>
    public class FlexureResult
    {
        public double M { get; set; }
        public double AlphaM { get; set; }
        public double Xi { get; set; }
        /// <summary>As yêu cầu vùng kéo (mm²).</summary>
        public double AsReq { get; set; }
        /// <summary>As' yêu cầu vùng nén (mm²), 0 nếu đặt đơn.</summary>
        public double AscReq { get; set; }
        public bool DoublyReinforced => AscReq > 0;
        public bool Failed { get; set; }
        public string Note { get; set; }
    }

    public class ShearResult
    {
        public double Q { get; set; }
        public double QbMin { get; set; }
        public double QStrut { get; set; }
        public double QswReq { get; set; }
        public StirrupSpec Stirrup { get; set; }
        public bool Failed { get; set; }
        public string Note { get; set; }
    }

    public class BeamDesignResult
    {
        public double B { get; set; }
        public double H { get; set; }
        public FlexureResult TopLeft, Bottom, TopRight;
        public BarGroup TopLeftBars, BottomBars, TopRightBars;
        public ShearResult ShearLeft, ShearRight;
        /// <summary>Đai vùng gối / giữa nhịp.</summary>
        public StirrupSpec StirrupSupport, StirrupSpan;
        public List<string> Warnings { get; } = new List<string>();
        public bool Ok => !(TopLeft?.Failed ?? false) && !(Bottom?.Failed ?? false) && !(TopRight?.Failed ?? false)
                          && !(ShearLeft?.Failed ?? false) && !(ShearRight?.Failed ?? false);
    }

    /// <summary>Tính toán dầm BTCT tiết diện chữ nhật theo TCVN 5574:2018.</summary>
    public class BeamDesigner
    {
        readonly ConcreteGrade C;
        readonly SteelGrade S;
        readonly SteelGrade Sw;
        readonly double cover;
        /// <summary>Hệ số điều kiện làm việc γb1 = 0.9 khi có tải dài hạn (6.1.2.3).</summary>
        public double GammaB1 { get; set; } = 1.0;

        public BeamDesigner(ConcreteGrade c, SteelGrade s, SteelGrade sw, double cover)
        { C = c; S = s; Sw = sw; this.cover = cover; }

        double Rb => C.Rb * GammaB1;

        /// <summary>Khoảng cách từ mép chịu kéo tới trọng tâm cốt thép a (mm), ước lượng ban đầu.</summary>
        public double AEstimate(int dBar = 20, int dStirrup = 8) => cover + dStirrup + dBar / 2.0;

        /// <summary>
        /// Tính cốt dọc chịu uốn (8.1.2.2): αm = M/(Rb·b·h0²); αm ≤ αR → đặt đơn, ngược lại đặt kép.
        /// </summary>
        /// <param name="M">kN·m (trị tuyệt đối)</param>
        public FlexureResult Flexure(double M, double b, double h, double a = -1, double aComp = -1)
        {
            if (a < 0) a = AEstimate();
            if (aComp < 0) aComp = a;
            double h0 = h - a;
            double Mn = Math.Abs(M) * 1e6; // N·mm
            var r = new FlexureResult { M = Math.Abs(M) };
            double alphaR = Tcvn5574.AlphaR(S);
            double xiR = Tcvn5574.XiR(S);
            double am = Mn / (Rb * b * h0 * h0);
            r.AlphaM = am;
            double asMin = Tcvn5574.MuMinFlexure * b * h0;
            if (am <= alphaR)
            {
                double xi = 1 - Math.Sqrt(1 - 2 * am);
                r.Xi = xi;
                r.AsReq = Math.Max(Rb * b * xi * h0 / S.Rs, Mn > 0 ? asMin : 0);
            }
            else
            {
                double asc = (Mn - alphaR * Rb * b * h0 * h0) / (S.Rsc * (h0 - aComp));
                r.Xi = xiR;
                r.AscReq = asc;
                r.AsReq = (xiR * Rb * b * h0 + S.Rsc * asc) / S.Rs;
                if (am > 0.5) { r.Failed = true; r.Note = $"αm = {am:0.000} quá lớn — tăng tiết diện"; }
                else r.Note = "Đặt cốt kép";
            }
            double mu = r.AsReq / (b * h0);
            if (mu > 0.03) { r.Failed = true; r.Note = $"μ = {mu * 100:0.00}% > 3% — tăng tiết diện"; }
            return r;
        }

        /// <summary>
        /// Kiểm tra/tính đai chịu cắt (8.1.3.3): Q ≤ Qb + Qsw;
        /// Qb = φb2·Rbt·b·h0²/c, 0.5Rbt·b·h0 ≤ Qb ≤ 2.5Rbt·b·h0; Qsw = 0.75·qsw·c0, c0 ≤ 2h0.
        /// Kiểm tra dải nghiêng: Q ≤ 0.3·Rb·b·h0.
        /// </summary>
        /// <param name="Q">kN</param>
        public ShearResult Shear(double Q, double b, double h, int[] stirrupDias, bool supportZone)
        {
            double a = AEstimate();
            double h0 = h - a;
            double Qn = Math.Abs(Q) * 1e3; // N
            var r = new ShearResult { Q = Math.Abs(Q) };
            r.QStrut = 0.3 * Rb * b * h0 / 1e3;
            r.QbMin = 0.5 * C.Rbt * b * h0 / 1e3;
            if (Qn > 0.3 * Rb * b * h0) { r.Failed = true; r.Note = "Không đủ khả năng chịu ứng suất nén chính — tăng tiết diện"; }

            // qsw yêu cầu (N/mm): bisection sao cho min_c(Qb(c)+Qsw(c)) ≥ Q
            double qswReq = 0;
            if (Qn > r.QbMin * 1e3)
            {
                double lo = 0, hi = 2000;
                for (int i = 0; i < 60; i++)
                {
                    double mid = (lo + hi) / 2;
                    if (ShearCapacity(mid, b, h0) >= Qn) hi = mid; else lo = mid;
                }
                qswReq = hi;
            }
            // Điều kiện tối thiểu qsw ≥ 0.25·Rbt·b (khi cần đai tính toán)
            if (qswReq > 0) qswReq = Math.Max(qswReq, 0.25 * C.Rbt * b);
            r.QswReq = qswReq;

            // Bước đai cấu tạo (10.3.4): vùng gối ≤ min(0.5h0, 300); giữa nhịp ≤ min(0.75h0, 500)
            double sMaxConstr = supportZone ? Math.Min(0.5 * h0, 300) : Math.Min(0.75 * h0, 500);
            // Bước đai lớn nhất để không phá hoại giữa 2 đai: sw,max = Rbt·b·h0²/Q
            double sMaxQ = Qn > 0 ? C.Rbt * b * h0 * h0 / Qn : double.MaxValue;
            double sMax = Math.Min(sMaxConstr, sMaxQ);

            int legs = b <= 350 ? 2 : (b <= 600 ? 4 : 6);
            foreach (var d in stirrupDias.OrderBy(x => x))
            {
                double asw = legs * Tcvn5574.BarArea(d);
                double s = qswReq > 0 ? Sw.Rsw * asw / qswReq : double.MaxValue;
                s = Math.Min(s, sMax);
                s = Math.Floor(s / 50) * 50;
                if (s >= 100)
                {
                    r.Stirrup = new StirrupSpec { D = Math.Max(d, MinStirrupDia(h)), Legs = legs, S = Math.Min(s, supportZone ? 200 : 300) };
                    break;
                }
            }
            if (r.Stirrup == null)
            {
                int d = stirrupDias.Max();
                r.Stirrup = new StirrupSpec { D = d, Legs = legs, S = 100 };
                if (!r.Failed) { r.Failed = true; r.Note = "Đai Ø" + d + "a100 vẫn chưa đủ — tăng tiết diện hoặc số nhánh"; }
            }
            return r;
        }

        /// <summary>Khả năng chịu cắt nhỏ nhất theo các vị trí tiết diện nghiêng c ∈ [h0, 3h0] (N).</summary>
        double ShearCapacity(double qsw, double b, double h0)
        {
            double min = double.MaxValue;
            for (double c = h0; c <= 3 * h0 + 1e-6; c += h0 / 20)
            {
                double qb = 1.5 * C.Rbt * b * h0 * h0 / c;
                qb = Math.Max(0.5 * C.Rbt * b * h0, Math.Min(2.5 * C.Rbt * b * h0, qb));
                double c0 = Math.Min(c, 2 * h0);
                double qs = 0.75 * qsw * c0;
                min = Math.Min(min, qb + qs);
            }
            return min;
        }

        /// <summary>Đường kính đai tối thiểu: Ø6 với h ≤ 800, Ø8 với h &gt; 800.</summary>
        public static int MinStirrupDia(double h) => h > 800 ? 8 : 6;

        /// <summary>Chọn n thanh Ø d đáp ứng As yêu cầu, xếp được trên 1 lớp (hoặc 2 lớp).</summary>
        public static BarGroup SelectBars(double asReq, double b, double cover, int dStirrup, int[] dias, int minBars = 2, int minDia = 14)
        {
            var oneLayer = new List<BarGroup>();
            var twoLayer = new List<BarGroup>();
            foreach (var d in dias.Where(x => x >= minDia).OrderBy(x => x))
            {
                // số thanh tối đa 1 lớp: khe hở thông thuỷ ≥ max(25, d) (10.3.2)
                double clear = Math.Max(25, d);
                int nMax1 = Math.Max(2, (int)Math.Floor((b - 2 * cover - 2 * dStirrup + clear) / (d + clear)));
                int n = Math.Max(minBars, (int)Math.Ceiling(asReq / Tcvn5574.BarArea(d) - 1e-9));
                if (n <= nMax1) oneLayer.Add(new BarGroup(n, d));
                else if (n <= 2 * nMax1) twoLayer.Add(new BarGroup(n, d));
            }
            // Chọn phương án ít dư diện tích nhất; ưu tiên 1 lớp, cùng diện tích thì ít thanh hơn
            var pick = (oneLayer.Count > 0 ? oneLayer : twoLayer)
                .OrderBy(g => Math.Round(g.Area - asReq, 0)).ThenBy(g => g.N).FirstOrDefault();
            return pick ?? new BarGroup(Math.Max(minBars, (int)Math.Ceiling(asReq / Tcvn5574.BarArea(dias.Max()))), dias.Max());
        }

        /// <summary>Thiết kế trọn một dầm theo bao nội lực 3 vùng.</summary>
        public BeamDesignResult Design(double b, double h, double mNegL, double mPos, double mNegR, double vL, double vR,
                                       int[] barDias, int[] stirrupDias, double torsion = 0)
        {
            var r = new BeamDesignResult { B = b, H = h };
            int dsw = Math.Max(stirrupDias.Min(), MinStirrupDia(h));
            r.TopLeft = Flexure(mNegL, b, h);
            r.Bottom = Flexure(mPos, b, h);
            r.TopRight = Flexure(mNegR, b, h);

            // Thép dọc tối thiểu 2 thanh mỗi mặt; đặt kép thì bổ sung vào mặt đối diện
            double asTopL = Math.Max(r.TopLeft.AsReq, r.Bottom.AscReq);
            double asTopR = Math.Max(r.TopRight.AsReq, r.Bottom.AscReq);
            double asBot = Math.Max(r.Bottom.AsReq, Math.Max(r.TopLeft.AscReq, r.TopRight.AscReq));
            r.TopLeftBars = SelectBars(asTopL, b, cover, dsw, barDias);
            r.TopRightBars = SelectBars(asTopR, b, cover, dsw, barDias);
            r.BottomBars = SelectBars(asBot, b, cover, dsw, barDias);

            r.ShearLeft = Shear(vL, b, h, stirrupDias, true);
            r.ShearRight = Shear(vR, b, h, stirrupDias, true);
            var sup = r.ShearLeft.Stirrup.S <= r.ShearRight.Stirrup.S ? r.ShearLeft.Stirrup : r.ShearRight.Stirrup;
            r.StirrupSupport = sup;
            var spanS = Shear(Math.Max(vL, vR) * 0.5, b, h, stirrupDias, false).Stirrup;
            r.StirrupSpan = new StirrupSpec { D = sup.D, Legs = sup.Legs, S = Math.Max(sup.S, Math.Min(spanS.S, 2 * sup.S)) };

            if (torsion > 0.05 * Math.Max(Math.Abs(mNegL), Math.Max(Math.Abs(mPos), Math.Abs(mNegR))) && torsion > 2)
                r.Warnings.Add($"Mô men xoắn T = {torsion:0.0} kN·m đáng kể — cần kiểm tra xoắn theo 8.1.4");
            if (h > 700) r.Warnings.Add("h > 700 mm: bố trí cốt giá (thép cấu tạo mặt bên) Ø12 mỗi mặt bên, khoảng cách ≤ 400");
            foreach (var f in new[] { r.TopLeft, r.Bottom, r.TopRight }) if (f.Failed) r.Warnings.Add(f.Note);
            foreach (var s in new[] { r.ShearLeft, r.ShearRight }) if (s.Failed) r.Warnings.Add(s.Note);
            return r;
        }
    }
}
