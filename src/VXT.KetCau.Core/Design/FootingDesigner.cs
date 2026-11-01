using System;
using System.Collections.Generic;
using VXT.KetCau.Core.Codes;

namespace VXT.KetCau.Core.Design
{
    public class FootingDesignResult
    {
        public double B { get; set; }
        public double L { get; set; }
        public double H { get; set; }
        public double Pressure { get; set; }
        public SlabBar BarsL { get; set; }
        public SlabBar BarsB { get; set; }
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Móng đơn BTCT dưới cột (sơ bộ): kích thước đáy theo sức chịu tải nền R, chiều cao theo chọc thủng (8.1.6),
    /// thép đáy theo mô men công-xôn tại mép cột.
    /// </summary>
    public class FootingDesigner
    {
        readonly ConcreteGrade C; readonly SteelGrade S; readonly double cover;
        /// <summary>Sức chịu tải tính toán của nền R (kPa) — lấy theo hồ sơ khảo sát địa chất.</summary>
        public double SoilR { get; set; } = 150;
        /// <summary>Chiều sâu chôn móng (m).</summary>
        public double Depth { get; set; } = 1.5;
        public FootingDesigner(ConcreteGrade c, SteelGrade s, double cover) { C = c; S = s; this.cover = cover; }

        /// <param name="N">lực dọc tính toán chân cột (kN)</param>
        /// <param name="M">mô men chân cột lớn nhất (kN·m)</param>
        public FootingDesignResult Design(double N, double M, double bc, double hc)
        {
            var r = new FootingDesignResult();
            double Ntc = N / 1.15; // quy đổi về tiêu chuẩn
            double A = Ntc / (SoilR - 20 * Depth) * 1.2; // m², kể lệch tâm
            double side = Math.Max(1.0, Math.Ceiling(Math.Sqrt(A) * 10) / 10);
            r.B = side * 1000; r.L = side * 1000;
            double p = N / (side * side);
            double pMax = p + 6 * Math.Abs(M) / Math.Pow(side, 3);
            r.Pressure = pMax;
            if (pMax > 1.2 * SoilR * 1.15) r.Warnings.Add("pmax vượt 1.2R — tăng kích thước đáy móng");

            // chọc thủng: F ≤ Rbt·u·h0, F = N − p·(bc+2h0)(hc+2h0)
            double h0 = 250;
            for (; h0 < 1500; h0 += 50)
            {
                double ab = (bc + 2 * h0) / 1000 * (hc + 2 * h0) / 1000;
                double F = (N - p * Math.Min(ab, side * side)) * 1e3; // N
                double u = 2 * (bc + hc + 2 * h0);
                if (F <= C.Rbt * u * h0) break;
            }
            r.H = Math.Max(300, Math.Ceiling((h0 + cover + 10) / 50) * 50);
            double hEff = r.H - cover - 10;

            // thép: M = p·B·(L−hc)²/8 tại mép cột
            double cant = (r.L - hc) / 2000;
            double Mx = pMax * side * cant * cant / 2; // kN·m cho toàn bề rộng
            double perM = Mx / side;
            var sd = new SlabDesigner(C, S, cover);
            double as1 = perM * 1e6 / (0.9 * S.Rs * hEff); // mm²/m
            r.BarsL = Pick(as1, new[] { 12, 14, 16 });
            r.BarsB = Pick(as1, new[] { 12, 14, 16 });
            return r;
        }

        static SlabBar Pick(double asReq, int[] dias)
        {
            foreach (var d in dias)
                foreach (var s in new double[] { 200, 180, 150, 120, 100 })
                    if (Tcvn5574.BarArea(d) * 1000 / s >= asReq) return new SlabBar { D = d, S = s, AsReq = asReq };
            return new SlabBar { D = dias[dias.Length - 1], S = 100, AsReq = asReq };
        }
    }
}
