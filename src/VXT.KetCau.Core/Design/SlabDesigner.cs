using System;
using System.Collections.Generic;
using System.Linq;
using VXT.KetCau.Core.Codes;

namespace VXT.KetCau.Core.Design
{
    /// <summary>Thép sàn: Ø d bước a (mm) cho 1 phương / 1 vị trí.</summary>
    public class SlabBar
    {
        public int D { get; set; }
        public double S { get; set; }
        public double AsReq { get; set; }
        public double AsProv => Tcvn5574.BarArea(D) * 1000 / S;
        public string Text(string sym = "Ø") => $"{sym}{D}a{S:0}";
        public override string ToString() => Text();
    }

    public class SlabDesignResult
    {
        public double Thickness { get; set; }
        public double L1 { get; set; }
        public double L2 { get; set; }
        public bool TwoWay { get; set; }
        public double M1Span, M2Span, M1Sup, M2Sup;
        public SlabBar Bottom1, Bottom2, Top1, Top2;
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Thiết kế sàn BTCT. Khi chưa có nội lực từ ETABS/SAP2000 dùng phương pháp tra bảng gần đúng
    /// (bản kê 4 cạnh ngàm: phân phối tải theo Grashof; bản loại dầm khi l2/l1 &gt; 2).
    /// </summary>
    public class SlabDesigner
    {
        readonly ConcreteGrade C;
        readonly SteelGrade S;
        readonly double cover;
        public SlabDesigner(ConcreteGrade c, SteelGrade s, double cover) { C = c; S = s; this.cover = cover; }

        /// <summary>Chiều dày sơ bộ hb = D·l1/m (D = 0.8..1.4; m = 30..35 bản dầm, 40..45 bản kê), làm tròn 10, ≥ 80.</summary>
        public static double PreliminaryThickness(double l1, double l2, double liveLoad)
        {
            bool twoWay = l2 / l1 <= 2;
            double D = liveLoad > 3 ? 1.1 : 1.0;
            double m = twoWay ? 42 : 32;
            return Math.Max(100, Math.Ceiling(D * l1 / m / 10) * 10);
        }

        /// <summary>Mô men gần đúng (kN·m/m). q = tải tính toán (kN/m²), l1 ≤ l2 (mm).</summary>
        public static (double m1s, double m2s, double m1g, double m2g, bool twoWay) Moments(double q, double l1, double l2)
        {
            if (l1 > l2) { var t = l1; l1 = l2; l2 = t; }
            double a = l1 / 1000, b = l2 / 1000;
            if (b / a > 2)
            {
                // bản loại dầm, hai đầu ngàm: Mnhịp = ql²/24, Mgối = ql²/12
                return (q * a * a / 24, 0, q * a * a / 12, 0, false);
            }
            double q1 = q * Math.Pow(b, 4) / (Math.Pow(a, 4) + Math.Pow(b, 4));
            double q2 = q - q1;
            return (q1 * a * a / 24, q2 * b * b / 24, q1 * a * a / 12, q2 * b * b / 12, true);
        }

        SlabBar Select(double M, double t, int[] dias, bool secondLayer)
        {
            double h0 = t - cover - (secondLayer ? 1.5 : 0.5) * dias.Min();
            double am = Math.Abs(M) * 1e6 / (C.Rb * 1000 * h0 * h0);
            if (am > Tcvn5574.AlphaR(S)) am = Tcvn5574.AlphaR(S);
            double xi = 1 - Math.Sqrt(1 - 2 * am);
            double asReq = Math.Max(C.Rb * 1000 * xi * h0 / S.Rs, Tcvn5574.MuMinFlexure * 1000 * h0);
            double sMax = t <= 150 ? 200 : Math.Min(1.5 * t, 400);
            foreach (var d in dias.OrderBy(x => x))
            {
                double s = Tcvn5574.BarArea(d) * 1000 / asReq;
                s = Math.Min(Math.Floor(s / 10) * 10, sMax);
                // bước thương phẩm: 100,120,150,170,200,250
                double[] steps = { 250, 200, 180, 150, 120, 100 };
                double pick = steps.FirstOrDefault(x => x <= s);
                if (pick >= 100) return new SlabBar { D = d, S = pick, AsReq = asReq };
            }
            return new SlabBar { D = dias.Max(), S = 100, AsReq = asReq };
        }

        public SlabDesignResult Design(double t, double l1, double l2, double qTotal, int[] dias, Model.SlabForces forces = null)
        {
            if (l1 > l2) { var x = l1; l1 = l2; l2 = x; }
            var r = new SlabDesignResult { Thickness = t, L1 = l1, L2 = l2 };
            if (forces != null && (forces.M1Span != 0 || forces.M1Support != 0))
            {
                r.M1Span = forces.M1Span; r.M2Span = forces.M2Span; r.M1Sup = forces.M1Support; r.M2Sup = forces.M2Support;
                r.TwoWay = l2 / l1 <= 2;
            }
            else
            {
                var m = Moments(qTotal, l1, l2);
                r.M1Span = m.m1s; r.M2Span = m.m2s; r.M1Sup = m.m1g; r.M2Sup = m.m2g; r.TwoWay = m.twoWay;
            }
            r.Bottom1 = Select(r.M1Span, t, dias, false);
            r.Bottom2 = r.TwoWay ? Select(r.M2Span, t, dias, true) : new SlabBar { D = dias.Min(), S = 250, AsReq = 0 }; // thép phân bố
            r.Top1 = Select(r.M1Sup, t, dias, false);
            r.Top2 = r.TwoWay ? Select(r.M2Sup, t, dias, false) : new SlabBar { D = dias.Min(), S = 200, AsReq = 0 };
            if (t < l1 / 50) r.Warnings.Add($"Sàn dày {t:0} mm có thể không đạt độ võng với l1 = {l1:0} mm — kiểm tra võng");
            return r;
        }
    }
}
