using KetCauVXT.Design.Concrete;

namespace KetCauVXT.Design.Foundation;

// =====================================================================
// NHÓM CỌC – PHẢN LỰC, CỌC CHỊU NHỔ (TCVN 10304:2014), NEO THÉP CỌC VÀO ĐÀI (TCVN 5574:2018 mục 10.3.5)
// Đơn vị: kN, m, kN/m².
// =====================================================================

/// <summary>Kết quả kiểm tra cọc chịu nhổ.</summary>
public sealed class PileUpliftResult
{
    /// <summary>Lực nhổ tính toán lớn nhất Nt = max(−Pmin; 0) (kN)</summary>
    public double Nt { get; set; }
    /// <summary>Sức kháng nhổ cho phép [Pk] (kN) – từ ma sát thành (TCVN 10304:2014) hoặc người dùng nhập</summary>
    public double Pk { get; set; }
    /// <summary>Sức kháng nhổ cực hạn Fu,t = γc·u·Σ(fi·li) (kN)</summary>
    public double Fut { get; set; }
    /// <summary>Trọng lượng bản thân cọc kể vào sức kháng nhổ 0,9·Gc (kN)</summary>
    public double Gp { get; set; }
    /// <summary>Khả năng chịu kéo thép dọc cọc / râu thép Rs·As (kN)</summary>
    public double SteelCap { get; set; }
    /// <summary>Chiều dài neo yêu cầu khi chịu kéo lan (m)</summary>
    public double Lan { get; set; }
    /// <summary>Chiều dài neo cơ sở l0,an (m)</summary>
    public double L0an { get; set; }
    public List<string> Calc { get; } = new();
    public List<string> Notes { get; } = new();
}

public static class PileGroup
{
    /// <summary>Hệ số điều kiện làm việc của cọc chịu nhổ γc (TCVN 10304:2014 mục 7.2.2.10 – cọc trong đất, độ sâu &gt; 4 m).</summary>
    public const double GammaCTension = 0.8;
    /// <summary>Hệ số tin cậy theo đất γk dùng chuyển sức kháng cực hạn sang cho phép.</summary>
    public const double GammaK = 1.4;

    /// <summary>Phản lực đầu cọc đài cứng: Pi = (N + G)/n + Mx·yi/Σy² + My·xi/Σx² (Mx quay quanh trục X → biến thiên theo y).</summary>
    public static double[] Reactions(double n, double mx, double my, double g, IList<(double X, double Y)> piles)
    {
        if (piles == null || piles.Count == 0) return Array.Empty<double>();
        double sx = piles.Sum(p => p.X * p.X), sy = piles.Sum(p => p.Y * p.Y);
        return piles.Select(p => (n + g) / piles.Count + (sy > 1e-9 ? mx * p.Y / sy : 0) + (sx > 1e-9 ? my * p.X / sx : 0)).ToArray();
    }

    /// <summary>Sức kháng nhổ cực hạn theo đất nền: Fu,t = γc·u·Σ(fi·li) với γc = 0,8 (TCVN 10304:2014 công thức 7.2.2.10).
    /// Sức kháng nhổ cho phép [Pk] = Fu,t/γk + 0,9·Gc (trọng lượng cọc kể giảm). u: chu vi cọc (m); sumFiLi: Σ fi·li (kN/m).</summary>
    public static (double Fut, double Pk, double Gp) TensionCapacity(double perimeter, double sumFiLi, double pileArea, double pileLength, double gammaPile = 25)
    {
        double fut = GammaCTension * Math.Max(perimeter, 0) * Math.Max(sumFiLi, 0);
        double gp = 0.9 * Math.Max(pileArea, 0) * Math.Max(pileLength, 0) * gammaPile;
        return (fut, fut / GammaK + gp, gp);
    }

    /// <summary>Chiều dài neo cơ sở cốt thép chịu kéo (TCVN 5574:2018 mục 10.3.5.4): l0,an = Rs·As/(Rbond·us) = Rs·d/(4·Rbond),
    /// Rbond = η1·η2·Rbt (η1 = 2,5 thép có gân; η2 = 1 với d ≤ 32). Chiều dài neo yêu cầu lan = α·l0,an·As,cal/As,ef,
    /// α = 1,0 (neo thẳng chịu kéo), lan ≥ max(0,3·l0,an; 15d; 200 mm).</summary>
    public static (double L0an, double Lan) AnchorageTension(double rs, double rbt, int dia, double asCal, double asEf)
    {
        double d = Math.Max(dia, 6) / 1000.0;
        double rbond = 2.5 * 1.0 * rbt;
        double l0 = rbond > 1e-9 ? rs * d / (4 * rbond) : 0;
        double ratio = asEf > 1e-12 ? Math.Clamp(asCal / asEf, 0, 1) : 1;
        double lan = Math.Max(1.0 * l0 * ratio, Math.Max(0.3 * l0, Math.Max(15 * d, 0.20)));
        return (l0, lan);
    }

    /// <summary>Kiểm tra cọc chịu nhổ đầy đủ: sức kháng đất (nếu có Σfi·li), thép cọc chịu kéo, neo râu thép vào đài.
    /// pk0: [Pk] người dùng nhập (dùng khi sumFiLi = 0). barN, barD: râu thép đầu cọc; anchorProvided: chiều dài neo bố trí (m).</summary>
    public static PileUpliftResult Uplift(double pmin, double pk0, double perimeter, double sumFiLi, double pileArea, double pileLength,
        int barN, int barD, double rs, double rbt, double anchorProvided)
    {
        var r = new PileUpliftResult { Nt = Math.Max(-pmin, 0) };
        if (sumFiLi > 0)
        {
            var (fut, pk, gp) = TensionCapacity(perimeter, sumFiLi, pileArea, pileLength);
            r.Fut = fut; r.Pk = pk; r.Gp = gp;
            r.Calc.Add($"Sức kháng nhổ (TCVN 10304:2014): Fu,t = γc·u·Σ(fi·li) = {GammaCTension:0.0}·{perimeter:0.000}·{sumFiLi:0.0} = {fut:0.0} kN; "
                       + $"[Pk] = Fu,t/γk + 0,9·Gc = {fut:0.0}/{GammaK:0.0} + {gp:0.0} = {pk:0.0} kN");
        }
        else
        {
            r.Pk = Math.Max(pk0, 0);
            r.Calc.Add($"Sức kháng nhổ cho phép [Pk] = {r.Pk:0.0} kN (người dùng nhập)");
        }
        double asEf = Math.Max(barN, 0) * Math.PI * barD * barD / 4 * 1e-6;
        r.SteelCap = rs * asEf;
        double asCal = rs > 1e-9 ? r.Nt / rs : 0;
        var (l0, lan) = AnchorageTension(rs, rbt, barD, asCal, asEf);
        r.L0an = l0; r.Lan = lan;
        if (r.Nt > 1e-6)
        {
            r.Calc.Add($"Thép cọc chịu kéo: Nt = {r.Nt:0.0} kN ≤ Rs·As = {rs / 1000:0}·{asEf * 1e4:0.00} cm² = {r.SteelCap:0.0} kN ({barN}Ø{barD})");
            r.Calc.Add($"Neo râu thép vào đài chịu kéo: l0,an = Rs·d/(4·2,5·Rbt) = {l0 * 1000:0} mm; lan = l0,an·As,cal/As,ef ≥ max(0,3l0,an; 15d; 200) = {lan * 1000:0} mm; bố trí {anchorProvided * 1000:0} mm");
            r.Notes.Add("Cọc chịu nhổ: mối nối cọc (bản mã hàn) phải đủ chịu kéo Nt = " + r.Nt.ToString("0.0") + " kN – kiểm tra đường hàn; đầu cọc đập bỏ bê tông, giữ râu thép đủ lan; đặt thép lớp trên đài.");
            if (anchorProvided + 1e-9 < lan) r.Notes.Add("CẢNH BÁO: chiều dài neo râu thép " + (anchorProvided * 1000).ToString("0") + " mm < lan = " + (lan * 1000).ToString("0") + " mm – tăng chiều dài neo (bẻ móc / hàn bản neo) hoặc tăng chiều cao đài.");
        }
        return r;
    }
}
