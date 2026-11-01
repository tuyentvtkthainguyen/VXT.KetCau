using System;
using System.Collections.Generic;
using System.Linq;

namespace VXT.KetCau.Core.Codes
{
    /// <summary>
    /// Tải trọng theo TCVN 2737:2023 — giá trị mặc định cho nhà dân dụng.
    /// LƯU Ý: kỹ sư chịu trách nhiệm kiểm tra lại các giá trị theo bản tiêu chuẩn hiện hành trước khi phát hành hồ sơ.
    /// </summary>
    public static class Tcvn2737
    {
        /// <summary>Hoạt tải tiêu chuẩn toàn phần (kN/m²) theo công năng (Bảng hoạt tải sàn).</summary>
        static readonly List<(string[] keys, double q, string name)> LiveTable = new List<(string[], double, string)>
        {
            (new[]{"ngủ","khách","ăn","bếp","sinh hoạt","căn hộ","ở","wc","vệ sinh","tắm","thờ"}, 1.5, "Phòng ở, căn hộ"),
            (new[]{"làm việc","văn phòng","office"}, 2.0, "Văn phòng"),
            (new[]{"ban công","lô gia","logia"}, 2.0, "Ban công, lô gia"),
            (new[]{"sảnh","hành lang","cầu thang","thang"}, 3.0, "Sảnh, hành lang, cầu thang"),
            (new[]{"kho"}, 4.0, "Kho"),
            (new[]{"gara","để xe","garage"}, 5.0, "Gara ô tô con"),
            (new[]{"cửa hàng","kinh doanh","shop"}, 4.0, "Cửa hàng"),
            (new[]{"hội trường","nhà hàng","phòng họp"}, 4.0, "Hội trường, nhà hàng"),
            (new[]{"sân thượng","mái bằng có sử dụng"}, 1.5, "Mái bằng có sử dụng"),
            (new[]{"mái"}, 0.75, "Mái bằng không sử dụng"),
            (new[]{"kỹ thuật","máy"}, 7.5, "Phòng kỹ thuật"),
        };

        public static (double q, string name) LiveLoad(string usage, bool isRoof = false)
        {
            var u = (usage ?? "").ToLowerInvariant();
            foreach (var (keys, q, name) in LiveTable)
                if (keys.Any(k => u.Contains(k))) return (q, name);
            return isRoof ? (0.75, "Mái bằng không sử dụng") : (1.5, "Phòng ở (mặc định)");
        }

        /// <summary>Hệ số độ tin cậy γf của hoạt tải: 1.3 khi q &lt; 2 kN/m², 1.2 khi q ≥ 2.</summary>
        public static double LiveFactor(double q) => q < 2.0 ? 1.3 : 1.2;

        /// <summary>γf tĩnh tải: BTCT 1.1, lớp hoàn thiện/tường xây 1.2 (vữa, gạch thủ công có thể 1.3).</summary>
        public const double DeadFactor = 1.1;
        public const double SuperDeadFactor = 1.2;
        public const double WindFactor = 1.2;
        /// <summary>Hệ số tổ hợp tải tạm thời thứ hai trở đi.</summary>
        public const double Psi2 = 0.9;

        /// <summary>Tĩnh tải hoàn thiện sàn mặc định (kN/m²): gạch lát + vữa + trát trần + kỹ thuật.</summary>
        public static double FloorFinish(bool isRoof, bool isWet) => isRoof ? 2.5 : (isWet ? 2.0 : 1.5);

        /// <summary>
        /// Tải tường xây trên dầm (kN/m): γ·t·h + trát 2 mặt, nhân hệ số giảm lỗ cửa.
        /// </summary>
        /// <param name="t">chiều dày tường (mm)</param>
        /// <param name="h">chiều cao tường thông thuỷ (mm)</param>
        /// <param name="gamma">kN/m³: gạch đặc 18, gạch rỗng 15, gạch nhẹ AAC 7</param>
        /// <param name="openingRatio">tỉ lệ diện tích lỗ cửa (0..1)</param>
        public static double WallLineLoad(double t, double h, double gamma = 18, double openingRatio = 0)
        {
            double wall = gamma * t / 1000 * h / 1000;
            double plaster = 2 * 0.015 * 18 * h / 1000;
            return (wall + plaster) * (1 - Math.Min(0.6, Math.Max(0, openingRatio)));
        }

        public static double WallGamma(string material)
        {
            var m = (material ?? "").ToLowerInvariant();
            if (m.Contains("nhẹ") || m.Contains("aac") || m.Contains("bê tông khí")) return 7;
            if (m.Contains("rỗng") || m.Contains("lỗ")) return 15;
            return 18;
        }

        /// <summary>
        /// Áp lực gió sơ bộ lên công trình tại cao độ z (kN/m²) = W0·k(z)·c·γf.
        /// W0 nhập theo phân vùng của TCVN 2737:2023 (Phụ lục) — người dùng tự khai báo.
        /// k(z) lấy dạng hàm mũ cho địa hình A/B/C.
        /// </summary>
        public static double WindPressure(double w0, double zMm, string terrain, double cTotal = 1.4)
        {
            double z = Math.Max(zMm / 1000.0, 3.0);
            double zg, mt;
            switch ((terrain ?? "B").ToUpperInvariant())
            {
                case "A": zg = 250; mt = 0.07; break;
                case "C": zg = 400; mt = 0.14; break;
                default: zg = 300; mt = 0.09; break;
            }
            double k = 1.844 * Math.Pow(z / zg, 2 * mt);
            return w0 * k * cTotal;
        }
    }

    /// <summary>Phổ thiết kế động đất TCVN 9386:2012 (phổ loại 1, phương ngang).</summary>
    public static class Tcvn9386
    {
        public static (double S, double TB, double TC, double TD) GroundParams(string ground)
        {
            switch ((ground ?? "C").ToUpperInvariant())
            {
                case "A": return (1.00, 0.15, 0.4, 2.0);
                case "B": return (1.20, 0.15, 0.5, 2.0);
                case "D": return (1.35, 0.20, 0.8, 2.0);
                case "E": return (1.40, 0.15, 0.5, 2.0);
                default: return (1.15, 0.20, 0.6, 2.0);
            }
        }

        /// <summary>Phổ thiết kế Sd(T)/g.</summary>
        /// <param name="agR">gia tốc nền tham chiếu theo g (vd 0.0951)</param>
        /// <param name="gammaI">hệ số tầm quan trọng</param>
        /// <param name="q">hệ số ứng xử (khung BTCT DCM ≈ 3.9)</param>
        public static double Sd(double T, double agR, string ground, double q = 3.9, double gammaI = 1.0, double beta = 0.2)
        {
            var (S, TB, TC, TD) = GroundParams(ground);
            double ag = agR * gammaI;
            if (T <= TB) return ag * S * (2.0 / 3 + T / TB * (2.5 / q - 2.0 / 3));
            if (T <= TC) return ag * S * 2.5 / q;
            if (T <= TD) return Math.Max(ag * S * 2.5 / q * TC / T, beta * ag);
            return Math.Max(ag * S * 2.5 / q * TC * TD / (T * T), beta * ag);
        }

        /// <summary>Bảng (T, Sd/g) 0..4 s để khai báo hàm phổ trong ETABS/SAP2000.</summary>
        public static List<(double T, double Sa)> Table(double agR, string ground, double q = 3.9, double gammaI = 1.0)
        {
            var list = new List<(double, double)>();
            for (double t = 0; t <= 4.0001; t += 0.05) list.Add((Math.Round(t, 3), Sd(t, agR, ground, q, gammaI)));
            return list;
        }
    }
}
