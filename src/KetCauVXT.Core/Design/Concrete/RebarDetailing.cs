using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Neo và nối chồng cốt thép theo TCVN 5574:2018 mục 10.3.5 – 10.3.6 (đơn vị m, kN/m²).
/// <list type="bullet">
/// <item>Chiều dài neo cơ sở (10.3.5.4): l0,an = Rs·As/(Rbond·us) = Rs·d/(4·Rbond), Rbond = η1·η2·Rbt;
/// η1 = 1,5 thép trơn (CB240-T), 2,0 thép gân cán nguội, 2,5 thép gân cán nóng (CB300-V … CB500-V); η2 = 1,0 khi d ≤ 32 mm, 0,9 khi d = 36; 40 mm.</item>
/// <item>Chiều dài neo (10.3.5.5): lan = α·l0,an·As,cal/As,ef ≥ max(0,3·l0,an; 15d; 200 mm); α = 1,0 thanh chịu kéo, 0,75 thanh chịu nén.</item>
/// <item>Chiều dài nối chồng (10.3.6.2): ll = α·l0,an·As,cal/As,ef ≥ max(0,4·α·l0,an; 20d; 250 mm); α = 1,2 vùng kéo, 0,9 vùng nén.</item>
/// <item>Các mối nối cách nhau (tim–tim) nhỏ hơn 1,3·ll coi là cùng một tiết diện – nối so le để không quá 50% số thanh tại một tiết diện.</item>
/// </list>
/// Lấy As,cal/As,ef = 1 (thiên về an toàn); kết quả làm tròn lên 10 mm.</summary>
public static class RebarDetailing
{
    /// <summary>Hệ số η1 theo bề mặt thanh: thép trơn (mác "-T" hoặc CB240) 1,5; thép gân 2,5.</summary>
    public static double Eta1(Material steel)
    {
        string n = steel?.Name ?? "";
        if (n.EndsWith("-T", StringComparison.OrdinalIgnoreCase) || n.StartsWith("CB240", StringComparison.OrdinalIgnoreCase)) return 1.5;
        return 2.5;
    }

    /// <summary>Hệ số η2 theo đường kính.</summary>
    public static double Eta2(int d) => d <= 32 ? 1.0 : 0.9;

    /// <summary>Cường độ bám dính Rbond = η1·η2·Rbt (kN/m²).</summary>
    public static double BondStrength(Material conc, Material steel, int d) => Eta1(steel) * Eta2(d) * conc.Rbt;

    /// <summary>Chiều dài neo cơ sở l0,an (m) = Rs·d/(4·Rbond).</summary>
    public static double BasicAnchorage(Material conc, Material steel, int d)
    {
        double rb = BondStrength(conc, steel, d);
        if (rb <= 0) return 30 * d / 1000.0;
        return steel.Rs * (d / 1000.0) / (4 * rb);
    }

    /// <summary>Chiều dài neo lan (m), làm tròn lên 10 mm.</summary>
    public static double AnchorageLength(Material conc, Material steel, int d, bool tension = true, double asRatio = 1.0)
    {
        double l0 = BasicAnchorage(conc, steel, d);
        double a = tension ? 1.0 : 0.75;
        double l = a * l0 * Math.Max(asRatio, 0);
        l = Math.Max(l, Math.Max(0.3 * l0, Math.Max(15 * d / 1000.0, 0.200)));
        return RcSection.CeilTo(l, 0.01);
    }

    /// <summary>Chiều dài nối chồng ll (m), làm tròn lên 10 mm.</summary>
    /// <param name="alpha">hệ số α (0 = mặc định 1,2 vùng kéo / 0,9 vùng nén; RebarPreferences.LapAlphaTension/Compression)</param>
    public static double LapLength(Material conc, Material steel, int d, bool tension = true, double asRatio = 1.0, double alpha = 0)
    {
        double l0 = BasicAnchorage(conc, steel, d);
        double a = alpha > 0 ? alpha : tension ? 1.2 : 0.9;
        double l = a * l0 * Math.Max(asRatio, 0);
        l = Math.Max(l, Math.Max(0.4 * a * l0, Math.Max(20 * d / 1000.0, 0.250)));
        return RcSection.CeilTo(l, 0.01);
    }

    /// <summary>Khoảng so le tối thiểu giữa tâm hai mối nối (m) = 1,3·ll, làm tròn lên 10 mm.</summary>
    public static double StaggerDistance(double lap) => RcSection.CeilTo(1.3 * lap, 0.01);
}
