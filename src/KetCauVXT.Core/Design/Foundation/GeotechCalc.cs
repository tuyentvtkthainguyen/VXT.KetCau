using System.Globalization;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

/// <summary>Một lớp phân tố khi tính lún (độ sâu tính từ đáy móng, m).</summary>
public sealed class SettlementRow
{
    public double Top { get; set; }
    public double Bottom { get; set; }
    /// <summary>Độ sâu giữa lớp phân tố dưới đáy móng zi (m)</summary>
    public double Z { get; set; }
    public double H { get; set; }
    /// <summary>Ứng suất gây lún σz,i (kPa)</summary>
    public double SigmaZ { get; set; }
    /// <summary>Ứng suất hữu hiệu do trọng lượng bản thân σ'bt tại Df + zi (kPa)</summary>
    public double SigmaBt { get; set; }
    /// <summary>Mô đun biến dạng (MPa)</summary>
    public double E { get; set; }
    /// <summary>Độ lún lớp phân tố β·σz·h/E (m)</summary>
    public double Ds { get; set; }
    public string Layer { get; set; } = "";
}

/// <summary>Kết quả tính lún móng chữ nhật L×B theo phương pháp cộng lún từng lớp (TCVN 9362:2012 Phụ lục C).</summary>
public sealed class SettlementResult
{
    public double L { get; set; }
    public double B { get; set; }
    public double Df { get; set; }
    /// <summary>Áp lực trung bình đáy móng (tiêu chuẩn) p (kPa)</summary>
    public double P { get; set; }
    /// <summary>σ'bt tại đáy móng (kPa)</summary>
    public double SigmaBtDf { get; set; }
    /// <summary>Áp lực gây lún σgl = p − σ'bt(Df) (kPa)</summary>
    public double SigmaGl { get; set; }
    /// <summary>Chiều dày vùng chịu nén Hc dưới đáy móng (m)</summary>
    public double Hc { get; set; }
    /// <summary>Độ lún S (m)</summary>
    public double S { get; set; }
    /// <summary>Độ lún giới hạn Sgh (m)</summary>
    public double Limit { get; set; } = GeotechCalc.SettlementLimit;
    public bool Ok => S <= Limit + 1e-9;
    public List<SettlementRow> Rows { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

/// <summary>Một dòng bảng sức chịu tải cọc theo SPT (đoạn thân cọc trong một lớp đất, hoặc mũi cọc).</summary>
public sealed class PileLayerRow
{
    public double From { get; set; }
    public double To { get; set; }
    public string Soil { get; set; } = "";
    public bool Cohesive { get; set; }
    /// <summary>N dùng tính (≤ 50)</summary>
    public double N { get; set; }
    /// <summary>cu = 6,25·N (kPa) – đất dính</summary>
    public double Cu { get; set; }
    /// <summary>σ'v giữa đoạn (kPa) – đất dính</summary>
    public double SigmaV { get; set; }
    /// <summary>αp – đất dính</summary>
    public double Alpha { get; set; }
    /// <summary>fs (đất rời) hoặc fc (đất dính) (kPa); với dòng mũi cọc: qb (kPa)</summary>
    public double F { get; set; }
    /// <summary>Đóng góp sức chịu tải (kN)</summary>
    public double Q { get; set; }
    public bool IsTip { get; set; }
    public double Length => To - From;
}

/// <summary>Sức chịu tải cọc theo SPT (TCVN 10304:2014 Phụ lục G.3.2) và theo vật liệu.</summary>
public sealed class PileCapacityResult
{
    public string Borehole { get; set; } = "";
    public double Size { get; set; }
    public bool Round { get; set; }
    public bool Bored { get; set; }
    /// <summary>Độ sâu mũi cọc và đầu cọc (cao độ cắt cọc / đáy đài) tính từ mặt đất (m)</summary>
    public double TipDepth { get; set; }
    public double CutoffDepth { get; set; }
    public double Length => TipDepth - CutoffDepth;
    /// <summary>Diện tích mũi Ab (m²), chu vi u (m)</summary>
    public double Ab { get; set; }
    public double U { get; set; }
    /// <summary>N trung bình vùng mũi (1d trên – 4d dưới mũi, ≤ 50)</summary>
    public double Np { get; set; }
    public bool TipCohesive { get; set; }
    /// <summary>Cường độ sức kháng mũi qb (kPa)</summary>
    public double Qb { get; set; }
    /// <summary>Sức kháng mũi qb·Ab và ma sát u·Σ(f·l) (kN)</summary>
    public double TipResistance { get; set; }
    public double ShaftResistance { get; set; }
    /// <summary>Sức chịu tải cực hạn Rc,u (kN)</summary>
    public double Rcu => TipResistance + ShaftResistance;
    public double SafetyFactor { get; set; }
    /// <summary>[P]đn = Rc,u/FS (kN)</summary>
    public double Pdn => Rcu / Math.Max(SafetyFactor, 1e-6);
    /// <summary>Sức chịu tải theo vật liệu Pvl = φ·(Rb·Ab + Rsc·As) (kN)</summary>
    public double Pvl { get; set; }
    public double As { get; set; }
    public double Rb { get; set; }
    public double Rsc { get; set; }
    /// <summary>[P] = min([P]đn, Pvl) (kN)</summary>
    public double Allowable => Math.Min(Pdn, Pvl);
    public string TipLayer { get; set; } = "";
    public List<PileLayerRow> Rows { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

/// <summary>Tính toán địa kỹ thuật: ứng suất bản thân, áp dụng hố khoan cho móng nông, độ lún, sức chịu tải cọc theo SPT.</summary>
public static class GeotechCalc
{
    /// <summary>Trọng lượng riêng của nước (kN/m³)</summary>
    public const double GammaW = 10;
    /// <summary>Hệ số β trong công thức lún S = β·Σ σz,i·hi/Ei</summary>
    public const double SettlementBeta = 0.8;
    /// <summary>Độ lún giới hạn Sgh = 8 cm (TCVN 9362:2012 Bảng 16, nhà khung BTCT)</summary>
    public const double SettlementLimit = 0.08;
    /// <summary>Giới hạn trên của N trong công thức SPT</summary>
    public const double MaxN = 50;
    /// <summary>Độ sâu mũi cọc lớn nhất khi tự chọn (m)</summary>
    public const double MaxTipDepth = 40;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v, string fmt = "0.##") => double.IsFinite(v) ? v.ToString(fmt, Inv) : "-";

    // ------------------------------------------------------------------
    // Lớp đất, ứng suất
    // ------------------------------------------------------------------
    /// <summary>Lớp đất tại độ sâu z (m): lớp có Top ≤ z &lt; Bottom; trên lớp đầu → lớp đầu; khe hở / dưới đáy hố khoan → lớp gần nhất phía trên.
    /// null nếu hố khoan không có lớp.</summary>
    public static SoilLayer LayerAt(Borehole bh, double depth)
    {
        if (bh == null) return null;
        var ls = bh.Sorted();
        if (ls.Count == 0) return null;
        foreach (var l in ls)
            if (l.Top <= depth + 1e-9 && depth < l.Bottom - 1e-9) return l;
        if (depth < ls[0].Top) return ls[0];
        SoilLayer above = ls[0];
        foreach (var l in ls) if (l.Top <= depth + 1e-9) above = l;
        return above;
    }

    /// <summary>Trọng lượng riêng hữu hiệu tại độ sâu z: γ trên mực nước ngầm, γ' = γsat − γw dưới mực nước ngầm.</summary>
    public static double EffectiveUnitWeight(Borehole bh, double z)
    {
        var l = LayerAt(bh, z);
        if (l == null) return 18;
        return z >= (bh?.Water ?? double.PositiveInfinity) ? Math.Max(l.GammaSatOrDefault - GammaW, 0) : l.Gamma;
    }

    /// <summary>Các điểm chia (ranh giới lớp, mực nước ngầm) trong khoảng (a, b).</summary>
    private static List<double> Breaks(Borehole bh, double a, double b)
    {
        var pts = new List<double> { a, b };
        if (bh != null)
        {
            foreach (var l in bh.Layers.Where(l => l != null))
            {
                if (l.Top > a + 1e-9 && l.Top < b - 1e-9) pts.Add(l.Top);
                if (l.Bottom > a + 1e-9 && l.Bottom < b - 1e-9) pts.Add(l.Bottom);
            }
            double w = bh.Water;
            if (w > a + 1e-9 && w < b - 1e-9) pts.Add(w);
        }
        return pts.Distinct().OrderBy(v => v).ToList();
    }

    /// <summary>Ứng suất hữu hiệu do trọng lượng bản thân σ'bt(z) = Σ γi·hi (kPa), γ trên mực nước ngầm, γsat − 10 dưới mực nước ngầm.
    /// Lớp cuối kéo dài xuống dưới đáy hố khoan.</summary>
    public static double EffectiveStress(Borehole bh, double z)
    {
        if (z <= 0) return 0;
        var pts = Breaks(bh, 0, z);
        double s = 0;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            double a = pts[i], b = pts[i + 1];
            s += EffectiveUnitWeight(bh, (a + b) / 2) * (b - a);
        }
        return s;
    }

    /// <summary>Hố khoan áp dụng tại (x, y): theo tên SoilSettings.Borehole nếu có, ngược lại hố khoan (có lớp đất) gần nhất. null nếu không có.</summary>
    public static Borehole SelectBorehole(Project p, double x, double y)
    {
        var list = p?.Boreholes?.Where(b => b != null && b.Layers.Count > 0).ToList();
        if (list == null || list.Count == 0) return null;
        string name = p.Soil?.Borehole ?? "";
        if (!string.IsNullOrWhiteSpace(name))
        {
            var byName = list.FirstOrDefault(b => string.Equals(b.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byName != null) return byName;
        }
        return list.OrderBy(b => (b.X - x) * (b.X - x) + (b.Y - y) * (b.Y - y)).First();
    }

    /// <summary>Gán chỉ tiêu nền từ hố khoan vào SoilSettings cho độ sâu chôn móng Df:
    /// φ, c, E của lớp tại Df (lớp đặt đáy móng); γ = γ của lớp đó (γ' = γsat − 10 nếu Df ≥ mực nước ngầm);
    /// γ' trên đáy móng = σ'bt(Df)/Df (trung bình gia quyền, đẩy nổi dưới mực nước); Depth = Df; ROverride = 0 (R tính theo công thức).
    /// Trả về các dòng giải thích.</summary>
    public static List<string> ApplyToSoil(SoilSettings s, Borehole bh, double Df)
    {
        var notes = new List<string>();
        if (s == null || bh == null) return notes;
        Df = Math.Max(Df, 0);
        var l = LayerAt(bh, Df);
        if (l == null) { notes.Add($"Hố khoan {bh.Name} chưa có lớp đất – giữ chỉ tiêu nền đang nhập"); return notes; }
        bool sub = Df >= bh.Water;
        s.Phi = l.Phi; s.C = l.C; s.E = l.E > 0 ? l.E : s.E;
        s.Gamma = sub ? Math.Max(l.GammaSatOrDefault - GammaW, 0) : l.Gamma;
        double sig = EffectiveStress(bh, Df);
        s.GammaAbove = Df > 1e-6 ? sig / Df : s.Gamma;
        s.Depth = Df;
        s.ROverride = 0;
        notes.Add($"Hố khoan {bh.Name}: đáy móng Df = {F(Df)} m đặt trên lớp \"{l.Name}\" ({l.KindText}, {F(l.Top)}–{F(l.Bottom)} m, N = {F(l.N, "0.#")})");
        notes.Add($"φ = {F(l.Phi, "0.#")}°, c = {F(l.C, "0.#")} kPa, γII = {F(s.Gamma, "0.##")} kN/m³{(sub ? " (đẩy nổi γsat − 10, dưới mực nước ngầm)" : "")}, E = {F(s.E, "0.#")} MPa");
        notes.Add($"γ'II trên đáy móng = σ'bt(Df)/Df = {F(sig, "0.#")}/{F(Df)} = {F(s.GammaAbove, "0.##")} kN/m³");
        if (l.Kind == SoilKind.Fill) notes.Add("CẢNH BÁO: đáy móng nằm trong lớp đất đắp – cần hạ sâu móng hoặc xử lý nền");
        if (l.Kind == SoilKind.Organic || l.N < 4) notes.Add("CẢNH BÁO: đáy móng nằm trên lớp đất yếu (bùn / N < 4) – xem xét móng cọc hoặc gia cố nền");
        return notes;
    }

    /// <summary>Gán chỉ tiêu từ hố khoan vào project.Soil (độ sâu chôn móng Df).</summary>
    public static List<string> ApplyToSoil(Project p, Borehole bh, double Df) => ApplyToSoil(p?.Soil, bh, Df);

    /// <summary>Bản sao SoilSettings (mọi thuộc tính).</summary>
    public static SoilSettings CopySoil(SoilSettings s) =>
        System.Text.Json.JsonSerializer.Deserialize<SoilSettings>(System.Text.Json.JsonSerializer.Serialize(s ?? new SoilSettings()));

    /// <summary>Chỉ tiêu nền hiệu lực tại chân cột (x, y) dùng cho FootingDesigner / StripFootingDesigner:
    /// khi SoilSettings.UseBorehole và có hố khoan → bản sao SoilSettings đã ApplyToSoil(hố khoan áp dụng, Df), bh = hố khoan đó;
    /// ngược lại → chính project.Soil, bh = null (giữ nguyên kết quả như trước).</summary>
    public static SoilSettings SoilFor(Project p, double x, double y, out Borehole bh)
    {
        bh = null;
        var s = p?.Soil ?? new SoilSettings();
        if (p == null || !s.UseBorehole) return s;
        bh = SelectBorehole(p, x, y);
        if (bh == null) return s;
        var c = CopySoil(s);
        ApplyToSoil(c, bh, s.Depth);
        return c;
    }

    /// <summary>Hố khoan "giả định" một lớp đồng nhất 0–40 m từ SoilSettings (dùng ước tính lún / gợi ý khi chưa có khảo sát).</summary>
    public static Borehole FromSoilSettings(SoilSettings s)
    {
        s ??= new SoilSettings();
        return new Borehole
        {
            Name = "Nền giả định", WaterDepth = -1,
            Layers =
            {
                new SoilLayer
                {
                    Name = "Nền đất tốt giả định", Kind = SoilKind.SiltyClay, Top = 0, Bottom = 40, Gamma = s.Gamma, Phi = s.Phi, C = s.C,
                    E = s.E > 0 ? s.E : SoilPresets.GoodE, N = 10, IL = 0.35, Description = "Theo chỉ tiêu nền nhập tay (chưa có hố khoan)"
                }
            }
        };
    }

    /// <summary>Lớp đất yếu (N &lt; 4 hoặc bùn / hữu cơ) nằm trong phạm vi 2B dưới đáy móng (Df, Df + 2B]; null nếu không có.</summary>
    public static SoilLayer WeakLayerBelow(Borehole bh, double Df, double B)
    {
        if (bh == null) return null;
        double z1 = Df + 2 * Math.Max(B, 0);
        return bh.Sorted().FirstOrDefault(l => l.Bottom > Df + 1e-6 && l.Top < z1 - 1e-6 && (l.N < 4 || l.Kind == SoilKind.Organic));
    }

    // ------------------------------------------------------------------
    // Lún
    // ------------------------------------------------------------------
    /// <summary>Hệ số ứng suất tại độ sâu z dưới góc hình chữ nhật chịu tải đều a × b (Boussinesq):
    /// kc = 1/(2π)·[atan(a·b/(z·R)) + a·b·z/R·(1/(a² + z²) + 1/(b² + z²))], R = √(a² + b² + z²); z → 0: kc = 1/4.</summary>
    public static double CornerFactor(double a, double b, double z)
    {
        if (a <= 0 || b <= 0) return 0;
        if (z <= 1e-9) return 0.25;
        double R = Math.Sqrt(a * a + b * b + z * z);
        return (Math.Atan(a * b / (z * R)) + a * b * z / R * (1 / (a * a + z * z) + 1 / (b * b + z * z))) / (2 * Math.PI);
    }

    /// <summary>Độ lún móng chữ nhật L × B, đáy ở độ sâu Df, áp lực trung bình tiêu chuẩn p (kPa) – phương pháp cộng lún từng lớp:
    /// σgl = p − σ'bt(Df); chia lớp phân tố hi ≤ 0,4B (chia tại ranh giới lớp và mực nước ngầm);
    /// σz,i = 4·kc(L/2, B/2, zi)·σgl tại giữa lớp phân tố zi; dừng khi σz,i ≤ 0,2·σ'bt(Df + zi) (0,1 nếu Ei &lt; 5 MPa);
    /// S = β·Σ σz,i·hi/Ei, β = 0,8; Sgh = 8 cm (TCVN 9362:2012 Bảng 16).</summary>
    public static SettlementResult Settlement(Borehole bh, double L, double B, double Df, double p)
    {
        L = Math.Max(L, 0.1); B = Math.Max(B, 0.1);
        if (B > L) (L, B) = (B, L);
        var res = new SettlementResult { L = L, B = B, Df = Df, P = p };
        res.SigmaBtDf = EffectiveStress(bh, Df);
        res.SigmaGl = p - res.SigmaBtDf;
        if (res.SigmaGl <= 0)
        {
            res.Notes.Add($"σgl = p − σ'bt = {F(p, "0.#")} − {F(res.SigmaBtDf, "0.#")} ≤ 0 → không phát sinh lún do tải công trình");
            return res;
        }
        double a = L / 2, b = B / 2, hmax = 0.4 * B;
        double z = 0, S = 0;
        bool stopped = false;
        for (int it = 0; it < 2000 && z < 60; it++)
        {
            double top = Df + z;
            double end = top + hmax;
            foreach (double v in Breaks(bh, top, end)) if (v > top + 1e-9) { end = Math.Min(end, v); break; }
            double h = end - top;
            double zm = z + h / 2;
            var l = LayerAt(bh, Df + zm);
            double E = Math.Max(l?.E ?? 10, 0.1);
            double sz = 4 * CornerFactor(a, b, zm) * res.SigmaGl;
            double sbt = EffectiveStress(bh, Df + zm);
            double lim = (E < 5 ? 0.1 : 0.2) * sbt;
            if (sz <= lim) { stopped = true; break; }
            double ds = SettlementBeta * sz * h / (E * 1000);
            S += ds;
            res.Rows.Add(new SettlementRow { Top = z, Bottom = z + h, Z = zm, H = h, SigmaZ = sz, SigmaBt = sbt, E = E, Ds = ds, Layer = l?.Name ?? "" });
            z += h;
        }
        res.S = S; res.Hc = z;
        res.Notes.Add($"σ'bt(Df) = {F(res.SigmaBtDf, "0.#")} kPa; σgl = p − σ'bt = {F(p, "0.#")} − {F(res.SigmaBtDf, "0.#")} = {F(res.SigmaGl, "0.#")} kPa");
        res.Notes.Add($"Chia {res.Rows.Count} lớp phân tố hi ≤ 0,4B = {F(hmax, "0.###")} m; vùng chịu nén Hc = {F(z, "0.##")} m dưới đáy móng" +
                      (stopped ? " (σz ≤ 0,2σ'bt, hoặc 0,1σ'bt khi E < 5 MPa)" : " (đạt giới hạn tính 60 m)"));
        res.Notes.Add($"S = β·Σσz,i·hi/Ei = 0,8 × … = {F(S * 100, "0.##")} cm {(res.Ok ? "≤" : ">")} Sgh = {F(res.Limit * 100, "0")} cm → {(res.Ok ? "đạt" : "KHÔNG ĐẠT")}");
        return res;
    }

    // ------------------------------------------------------------------
    // Cọc
    // ------------------------------------------------------------------
    /// <summary>Diện tích thép dọc cọc mặc định: 4Ø16 khi d ≤ 300 mm, ngược lại 6Ø18 (m²).</summary>
    public static double DefaultRebarArea(double size) =>
        size <= 0.30 + 1e-9 ? 4 * RcSection.BarArea(16) : 6 * RcSection.BarArea(18);

    /// <summary>Độ sâu mũi cọc tự chọn: mặt lớp đầu tiên (đáy lớp sâu hơn đầu cọc) có N ≥ 30 (đất rời: cát, sỏi, đá) hoặc N ≥ 20 (đất dính) cộng 3d,
    /// không quá 40 m; không gặp lớp nào → đáy hố khoan (≤ 40 m). Tối thiểu đầu cọc + 1 m.</summary>
    public static double AutoTipDepth(Borehole bh, double size, double cutoffDepth, List<string> notes = null)
    {
        double d = Math.Max(size, 0.1);
        var ls = bh?.Sorted() ?? new List<SoilLayer>();
        foreach (var l in ls)
        {
            if (l.Bottom <= cutoffDepth + 1e-6) continue;
            bool good = l.IsCohesive ? l.N >= 20 : l.N >= 30;
            if (!good) continue;
            double tip = Math.Max(l.Top, cutoffDepth) + 3 * d;
            if (tip > MaxTipDepth) { notes?.Add($"Lớp tốt \"{l.Name}\" ở sâu {F(l.Top)} m – giới hạn mũi cọc {F(MaxTipDepth, "0")} m"); tip = MaxTipDepth; }
            notes?.Add($"Mũi cọc tự chọn: mặt lớp \"{l.Name}\" ({F(l.Top)} m, N = {F(l.N, "0.#")} ≥ {(l.IsCohesive ? "20" : "30")}) + 3d = {F(tip)} m");
            return Math.Max(tip, cutoffDepth + 1);
        }
        double t = Math.Min(Math.Max(bh?.Depth ?? 0, cutoffDepth + 1), MaxTipDepth);
        notes?.Add($"Không gặp lớp có N ≥ 30 (đất rời) / N ≥ 20 (đất dính) – mũi cọc tạm đặt tại {F(t)} m (đáy hố khoan, ≤ {F(MaxTipDepth, "0")} m); cần khoan sâu hơn");
        return t;
    }

    /// <summary>N trung bình gia quyền theo chiều dài trong khoảng [a, b] (mỗi lớp N ≤ 50).</summary>
    public static double AverageN(Borehole bh, double a, double b)
    {
        a = Math.Max(a, 0);
        if (b <= a + 1e-9) return Math.Min(LayerAt(bh, a)?.N ?? 0, MaxN);
        var pts = Breaks(bh, a, b);
        double s = 0;
        for (int i = 0; i + 1 < pts.Count; i++)
            s += Math.Clamp(LayerAt(bh, (pts[i] + pts[i + 1]) / 2)?.N ?? 0, 0, MaxN) * (pts[i + 1] - pts[i]);
        return s / (b - a);
    }

    /// <summary>Sức chịu tải nén của cọc đơn theo SPT – công thức Nhật Bản (TCVN 10304:2014 Phụ lục G.3.2):
    /// Rc,u = qb·Ab + u·Σ(fc,i·lc,i + fs,i·ls,i).
    /// Đất rời: fs,i = 10·Ns,i/3 (kPa); mũi qb = 300·Np (cọc đóng/ép) hoặc 150·Np (khoan nhồi), Np = N trung bình 1d trên – 4d dưới mũi.
    /// Đất dính: cu,i = 6,25·Nc,i; fc,i = αp·fL·cu,i, fL = 1, αp = 0,5·ψ^−0,5 (ψ = cu/σ'v ≤ 1) hoặc 0,5·ψ^−0,25 (ψ &gt; 1), αp ≤ 1; mũi qb = 9·cu,p, cu,p = 6,25·Np.
    /// Mọi N ≤ 50. [P]đn = Rc,u/FS; Pvl = φ·(Rb·Ab + Rsc·As), φ = 1; [P] = min([P]đn, Pvl).
    /// size: cạnh (cọc vuông) / đường kính (cọc tròn), m; tipDepth, cutoffDepth: độ sâu mũi / đầu cọc từ mặt đất (m).</summary>
    public static PileCapacityResult PileCapacity(Borehole bh, double size, bool round, double tipDepth, double cutoffDepth, bool bored,
        double safetyFactor = 2.5, double rebarArea = 0, string concrete = "B25", string rebar = "CB400-V")
    {
        double d = Math.Max(size, 0.1);
        cutoffDepth = Math.Max(cutoffDepth, 0);
        if (tipDepth <= cutoffDepth + 0.1) tipDepth = cutoffDepth + 0.1;
        var r = new PileCapacityResult
        {
            Borehole = bh?.Name ?? "", Size = d, Round = round, Bored = bored, TipDepth = tipDepth, CutoffDepth = cutoffDepth,
            Ab = round ? Math.PI * d * d / 4 : d * d, U = round ? Math.PI * d : 4 * d,
            SafetyFactor = safetyFactor > 0 ? safetyFactor : 2.5
        };
        // ---- ma sát thân cọc ----
        var pts = Breaks(bh, cutoffDepth, tipDepth);
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            double a = pts[i], b = pts[i + 1], zm = (a + b) / 2;
            var l = LayerAt(bh, zm);
            double n = Math.Clamp(l?.N ?? 0, 0, MaxN);
            bool coh = l?.IsCohesive ?? false;
            var row = new PileLayerRow { From = a, To = b, Soil = l == null ? "?" : l.Name + " (" + l.KindText + ")", Cohesive = coh, N = n };
            if (coh)
            {
                double cu = 6.25 * n, sv = EffectiveStress(bh, zm);
                double alpha = 1;
                if (cu > 0)
                {
                    double psi = sv > 1e-9 ? cu / sv : double.PositiveInfinity;
                    alpha = psi <= 1 ? 0.5 * Math.Pow(psi, -0.5) : 0.5 * Math.Pow(psi, -0.25);
                    alpha = Math.Min(alpha, 1);
                }
                row.Cu = cu; row.SigmaV = sv; row.Alpha = alpha; row.F = alpha * 1.0 * cu;
            }
            else row.F = 10 * n / 3;
            row.Q = r.U * row.F * (b - a);
            // nối các đoạn liên tiếp cùng lớp (khi có mực nước ngầm chia đôi lớp) – giữ riêng để σ'v đúng
            r.Rows.Add(row);
        }
        r.ShaftResistance = r.Rows.Sum(x => x.Q);
        // ---- mũi cọc ----
        var tl = LayerAt(bh, tipDepth);
        r.TipLayer = tl == null ? "?" : tl.Name + " (" + tl.KindText + ")";
        r.Np = Math.Min(AverageN(bh, tipDepth - d, tipDepth + 4 * d), MaxN);
        r.TipCohesive = tl?.IsCohesive ?? false;
        r.Qb = r.TipCohesive ? 9 * 6.25 * r.Np : (bored ? 150 : 300) * r.Np;
        r.TipResistance = r.Qb * r.Ab;
        r.Rows.Add(new PileLayerRow
        {
            From = Math.Max(tipDepth - d, 0), To = tipDepth + 4 * d, Soil = "Mũi cọc: " + r.TipLayer, Cohesive = r.TipCohesive, N = r.Np,
            Cu = r.TipCohesive ? 6.25 * r.Np : 0, F = r.Qb, Q = r.TipResistance, IsTip = true
        });
        // ---- vật liệu ----
        Material conc, st;
        try { conc = Material.Concrete(string.IsNullOrWhiteSpace(concrete) ? "B25" : concrete); } catch (Exception) { conc = Material.Concrete("B25"); }
        try { st = Material.Rebar(string.IsNullOrWhiteSpace(rebar) ? "CB400-V" : rebar); } catch (Exception) { st = Material.Rebar("CB400-V"); }
        r.As = rebarArea > 0 ? rebarArea : DefaultRebarArea(d);
        r.Rb = conc.Rb; r.Rsc = st.Rsc;
        // cọc khoan nhồi: bê tông đổ trong dung dịch / dưới nước – γcb·γ'cb = 0,85·0,7 (TCVN 10304:2014 mục 7.1.9)
        double kb = bored ? KetCauVXT.Design.Foundation.BoredPile.GammaCb * KetCauVXT.Design.Foundation.BoredPile.GammaCbWet : 1.0;
        r.Pvl = 1.0 * (kb * conc.Rb * r.Ab + st.Rsc * r.As);

        string shape = round ? "cọc tròn D" + F(d * 1000, "0") : "cọc vuông " + F(d * 1000, "0") + "×" + F(d * 1000, "0");
        r.Notes.Add($"{(bored ? "Cọc khoan nhồi" : "Cọc đóng / ép")} {shape} mm, đầu cọc {F(cutoffDepth)} m, mũi {F(tipDepth)} m (L = {F(r.Length)} m), hố khoan {r.Borehole}");
        r.Notes.Add($"Ab = {F(r.Ab, "0.####")} m², u = {F(r.U, "0.###")} m");
        r.Notes.Add($"Ma sát thân u·Σf·l = {F(r.ShaftResistance, "0.0")} kN; mũi: Np = {F(r.Np, "0.#")}, qb = {(r.TipCohesive ? "9·6,25·Np" : (bored ? "150·Np" : "300·Np"))} = {F(r.Qb, "0")} kPa → qb·Ab = {F(r.TipResistance, "0.0")} kN");
        r.Notes.Add($"Rc,u = {F(r.Rcu, "0.0")} kN; [P]đn = Rc,u/FS = {F(r.Rcu, "0.0")}/{F(r.SafetyFactor)} = {F(r.Pdn, "0.0")} kN");
        r.Notes.Add($"Pvl = φ·({(bored ? "γcb·γ'cb·" : "")}Rb·Ab + Rsc·As) = 1·({(bored ? "0,85·0,7·" : "")}{F(conc.Rb / 1000, "0.##")}·10³·{F(r.Ab, "0.####")} + {F(st.Rsc / 1000, "0")}·10³·{F(r.As * 1e4, "0.##")}·10⁻⁴) = {F(r.Pvl, "0.0")} kN");
        r.Notes.Add($"[P] = min([P]đn; Pvl) = {F(r.Allowable, "0.0")} kN");
        if (tipDepth > (bh?.Depth ?? 0) + 1e-6) r.Notes.Add("CẢNH BÁO: mũi cọc sâu hơn đáy hố khoan – lớp cuối được ngoại suy");
        return r;
    }

    /// <summary>Sức chịu tải cọc theo thông số FoundationSettings (mũi tự chọn khi PileTipDepth = 0, đầu cọc = Df) cho hố khoan bh.</summary>
    public static PileCapacityResult PileCapacity(Project p, Borehole bh, FoundationSettings fs = null)
    {
        fs ??= p?.Foundation ?? new FoundationSettings();
        // cọc khoan nhồi theo TCVN 10304:2014 (bảng lớp đất từ hố khoan, Bảng 3/7 hoặc SPT, ma sát âm, tự chọn chiều dài)
        if (fs.PileBored && !string.IsNullOrWhiteSpace(fs.BoredMethod) && bh != null)
            return KetCauVXT.Design.Foundation.BoredPileDesign.DesignCapacity(p, bh, fs);
        double cut = Math.Max(p?.Soil?.Depth ?? 1.5, 0);
        var notes = new List<string>();
        double tip = fs.PileTipDepth > 0 ? fs.PileTipDepth : AutoTipDepth(bh, fs.PileSize, cut, notes);
        var r = PileCapacity(bh, fs.PileSize, fs.PileRound, tip, cut, fs.PileBored, fs.PileSafetyFactor, fs.PileRebarArea,
            "B25", p?.Concrete?.RebarMain ?? "CB400-V");
        r.Notes.InsertRange(0, notes);
        return r;
    }
}
