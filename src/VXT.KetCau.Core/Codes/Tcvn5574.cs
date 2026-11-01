using System;
using System.Collections.Generic;

namespace VXT.KetCau.Core.Codes
{
    /// <summary>Bê tông theo TCVN 5574:2018 (Bảng 7, 8, 10). Đơn vị MPa.</summary>
    public class ConcreteGrade
    {
        public string Name { get; }
        /// <summary>Cường độ chịu nén tính toán Rb (MPa).</summary>
        public double Rb { get; }
        /// <summary>Cường độ chịu kéo tính toán Rbt (MPa).</summary>
        public double Rbt { get; }
        /// <summary>Cường độ chịu nén tiêu chuẩn Rb,n (MPa).</summary>
        public double Rbn { get; }
        public double Rbtn { get; }
        /// <summary>Mô đun đàn hồi Eb (MPa).</summary>
        public double Eb { get; }
        /// <summary>Trọng lượng riêng (kN/m³).</summary>
        public double Gamma => 25.0;

        public ConcreteGrade(string n, double rb, double rbt, double rbn, double rbtn, double eb)
        { Name = n; Rb = rb; Rbt = rbt; Rbn = rbn; Rbtn = rbtn; Eb = eb; }

        static readonly Dictionary<string, ConcreteGrade> Table = new Dictionary<string, ConcreteGrade>(StringComparer.OrdinalIgnoreCase)
        {
            ["B15"] = new ConcreteGrade("B15", 8.5, 0.75, 11.0, 1.10, 24000),
            ["B20"] = new ConcreteGrade("B20", 11.5, 0.90, 15.0, 1.35, 27500),
            ["B25"] = new ConcreteGrade("B25", 14.5, 1.05, 18.5, 1.55, 30000),
            ["B30"] = new ConcreteGrade("B30", 17.0, 1.15, 22.0, 1.75, 32500),
            ["B35"] = new ConcreteGrade("B35", 19.5, 1.30, 25.5, 1.95, 34500),
            ["B40"] = new ConcreteGrade("B40", 22.0, 1.40, 29.0, 2.10, 36000),
            ["B45"] = new ConcreteGrade("B45", 25.0, 1.50, 32.0, 2.25, 37000),
            ["B50"] = new ConcreteGrade("B50", 27.5, 1.60, 36.0, 2.45, 38000),
        };

        public static ConcreteGrade Get(string name)
        {
            if (Table.TryGetValue(name ?? "", out var g)) return g;
            throw new ArgumentException($"Không có cấp bê tông '{name}' trong bảng TCVN 5574:2018");
        }
        public static IEnumerable<string> Names => Table.Keys;
    }

    /// <summary>Cốt thép theo TCVN 5574:2018 (Bảng 13) / TCVN 1651:2018. Đơn vị MPa.</summary>
    public class SteelGrade
    {
        public string Name { get; }
        /// <summary>Cường độ chịu kéo tính toán Rs.</summary>
        public double Rs { get; }
        /// <summary>Cường độ chịu nén tính toán Rsc.</summary>
        public double Rsc { get; }
        /// <summary>Cường độ cốt ngang (đai) Rsw.</summary>
        public double Rsw { get; }
        public double Rsn { get; }
        public double Es => 200000;
        /// <summary>Hệ số η1 bám dính (10.3.5.4): thanh vằn cán nóng 2.5, trơn 1.5.</summary>
        public double Eta1 { get; }
        public bool Ribbed { get; }

        public SteelGrade(string n, double rs, double rsc, double rsw, double rsn, bool ribbed)
        { Name = n; Rs = rs; Rsc = rsc; Rsw = rsw; Rsn = rsn; Ribbed = ribbed; Eta1 = ribbed ? 2.5 : 1.5; }

        static readonly Dictionary<string, SteelGrade> Table = new Dictionary<string, SteelGrade>(StringComparer.OrdinalIgnoreCase)
        {
            ["CB240-T"] = new SteelGrade("CB240-T", 210, 210, 170, 240, false),
            ["CB300-V"] = new SteelGrade("CB300-V", 260, 260, 210, 300, true),
            ["CB400-V"] = new SteelGrade("CB400-V", 350, 350, 280, 400, true),
            ["CB500-V"] = new SteelGrade("CB500-V", 435, 400, 300, 500, true),
        };

        public static SteelGrade Get(string name)
        {
            if (Table.TryGetValue(name ?? "", out var g)) return g;
            throw new ArgumentException($"Không có mác thép '{name}' trong bảng TCVN 5574:2018");
        }
        public static IEnumerable<string> Names => Table.Keys;
    }

    /// <summary>Các công thức chung TCVN 5574:2018.</summary>
    public static class Tcvn5574
    {
        /// <summary>Biến dạng tới hạn của bê tông chịu nén εb2.</summary>
        public const double EpsB2 = 0.0035;
        public const double EpsB1Red = 0.0015;

        /// <summary>Chiều cao vùng nén giới hạn ξR = 0.8 / (1 + εs,el/εb2) (8.1.2.2.3).</summary>
        public static double XiR(SteelGrade s) => 0.8 / (1 + (s.Rs / s.Es) / EpsB2);

        public static double AlphaR(SteelGrade s) { var x = XiR(s); return x * (1 - 0.5 * x); }

        /// <summary>Diện tích một thanh (mm²).</summary>
        public static double BarArea(double d) => Math.PI * d * d / 4;

        /// <summary>Khối lượng 1 m thép (kg/m) = 0.00617·d².</summary>
        public static double UnitWeight(double d) => 0.00617 * d * d;

        /// <summary>
        /// Chiều dài neo cơ sở l0,an = Rs·As/(Rbond·us) = Rs·d/(4·Rbond), Rbond = η1·η2·Rbt (10.3.5.4).
        /// </summary>
        public static double BasicAnchorage(double d, SteelGrade s, ConcreteGrade c)
        {
            double eta2 = d <= 32 ? 1.0 : 0.9;
            double rbond = s.Eta1 * eta2 * c.Rbt;
            return s.Rs * d / (4 * rbond);
        }

        /// <summary>Chiều dài neo tính toán lan (10.3.5.5), làm tròn lên 10 mm.</summary>
        /// <param name="compression">thanh chịu nén (α = 0.75) hay kéo (α = 1.0).</param>
        /// <param name="ratio">As,cal / As,ef (≤ 1).</param>
        public static double Anchorage(double d, SteelGrade s, ConcreteGrade c, bool compression = false, double ratio = 1.0)
        {
            double l0 = BasicAnchorage(d, s, c);
            double alpha = compression ? 0.75 : 1.0;
            double lan = alpha * l0 * Math.Min(1.0, Math.Max(0.0, ratio));
            lan = Math.Max(lan, Math.Max(0.3 * l0, Math.Max(15 * d, 200)));
            return Math.Ceiling(lan / 10) * 10;
        }

        /// <summary>Chiều dài nối chồng ll (10.3.6.2): α = 1.2 kéo, 0.9 nén. Làm tròn lên 10 mm.</summary>
        public static double LapLength(double d, SteelGrade s, ConcreteGrade c, bool compression = false, double ratio = 1.0)
        {
            double l0 = BasicAnchorage(d, s, c);
            double alpha = compression ? 0.9 : 1.2;
            double ll = alpha * l0 * Math.Min(1.0, Math.Max(0.0, ratio));
            ll = Math.Max(ll, Math.Max(0.4 * alpha * l0, Math.Max(20 * d, 250)));
            return Math.Ceiling(ll / 10) * 10;
        }

        /// <summary>Hàm lượng cốt thép tối thiểu μmin (10.3.3.1) cho cấu kiện chịu uốn: 0.1%.</summary>
        public const double MuMinFlexure = 0.001;

        /// <summary>μmin cho cấu kiện nén lệch tâm theo độ mảnh λ = l0/i (Bảng 10.3.3.1).</summary>
        public static double MuMinColumn(double lambda)
        {
            if (lambda < 17) return 0.001;
            if (lambda <= 87) return 0.001 + (lambda - 17) / 70 * 0.0015;
            return 0.0025;
        }

        /// <summary>Móc đai 135°: max(75, 10d).</summary>
        public static double StirrupHook(double d) => Math.Max(75, 10 * d);
    }
}
