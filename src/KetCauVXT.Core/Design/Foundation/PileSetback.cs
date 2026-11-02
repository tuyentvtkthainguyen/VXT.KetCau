using System.Globalization;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

// =====================================================================
// [LùiCọcRanh] LÙI CỌC KHỎI RANH GIỚI ĐẤT ĐỂ THI CÔNG ÉP CỌC
// Nhà giáp ranh (nhà bên cạnh đã xây) không ép cọc sát ranh được: máy ép (neo / tải / robot) cần khoảng trống → tim cọc phải lùi vào
// trong lô đất ≥ s (mặc định 500 mm – thực hành ép neo; ép robot thường cần lớn hơn, người dùng kiểm tra theo máy ép).
// Nhóm cọc lệch tâm lớn so với cột sát ranh → dầm móng cân bằng (đòn bẩy R1 = N1·L/(L − e), EccentricFooting), hoặc đài chung với
// cột trong, hoặc đài console (công xôn) kiểm tra uốn / cắt / xoắn. Đài có thể vươn ra sát ranh (phần công xôn ngoài hàng cọc)
// hoặc dừng ở mép cọc + khoảng mép (FoundationSettings.CapToBoundary).
// Công thức: phản lực cọc theo đài tuyệt đối cứng (PileCapDesigner.PileForces); công xôn M = N·a, Q = N (a = tim cột → mép trong hàng cọc gần nhất);
// cắt Qb = 1,5·Rbt·b·h0²/c (TCVN 5574:2018 mục 8.1.3.3); xoắn kiểm tra điều kiện bê tông chịu nén T ≤ 0,1·Rb·b²·h (TCVN 5574:2018 mục 8.1.4 – dạng đơn giản hoá).
// =====================================================================

/// <summary>Khoảng lùi cọc tới một cạnh ranh giới.</summary>
public sealed class SetbackSide
{
    public BoundarySide Side { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Toạ độ ranh giới (X với trái/phải, Y với trước/sau), m</summary>
    public double Boundary { get; set; }
    /// <summary>Khoảng lùi yêu cầu (m) và độ dịch cọc đã thực hiện (m, theo trục)</summary>
    public double Setback { get; set; }
    public double Shift { get; set; }
    /// <summary>Khoảng cách nhỏ nhất ranh giới → tim (hoặc mép) cọc sau khi bố trí (m)</summary>
    public double Distance { get; set; }
    public bool IsVertical => Side == BoundarySide.Left || Side == BoundarySide.Right;
    /// <summary>+1 / −1: hướng vào trong lô đất</summary>
    public int Inward => Side == BoundarySide.Left || Side == BoundarySide.Front ? 1 : -1;
}

/// <summary>Kết quả lùi cọc khỏi ranh giới của một đài (PileCapDesign.Setback).</summary>
public sealed class PileSetbackInfo
{
    public double Setback { get; set; }
    public bool ToFace { get; set; }
    public bool CapToBoundary { get; set; }
    public PilePressMethod PressMethod { get; set; }
    public List<SetbackSide> Sides { get; set; } = new();
    /// <summary>Độ dịch nhóm cọc thêm vào trong lô đất do khoảng lùi (m)</summary>
    public double ShiftX { get; set; }
    public double ShiftY { get; set; }
    public bool Shifted => Math.Abs(ShiftX) > 1e-6 || Math.Abs(ShiftY) > 1e-6;
    /// <summary>Trọng tâm nhóm cọc − tim cột (m)</summary>
    public double GroupEx { get; set; }
    public double GroupEy { get; set; }
    /// <summary>Tay đòn công xôn tim cột → mép trong hàng cọc gần nhất theo X / Y (m), 0 = cột nằm trong phạm vi nhóm cọc</summary>
    public double CantileverX { get; set; }
    public double CantileverY { get; set; }
    /// <summary>Công xôn: mô men M = N·a (kN.m), khả năng Mu (thép lớp trên), lực cắt Q = N, khả năng Qb (kN)</summary>
    public double MCant { get; set; }
    public double MCantCap { get; set; }
    public double VCant { get; set; }
    public double VCantCap { get; set; }
    /// <summary>Xoắn do lệch tâm vuông góc phương dầm cân bằng T = N·e⊥ (kN.m) và giới hạn 0,1·Rb·b²·h</summary>
    public double Torsion { get; set; }
    public double TorsionCap { get; set; }
    /// <summary>Thép lớp trên đã tăng theo công xôn (rỗng = không tăng)</summary>
    public string TopBarsRaised { get; set; } = "";
    /// <summary>Phản lực cọc lớn nhất / nhỏ nhất khi KHÔNG có dầm cân bằng (nhóm cọc lệch tâm tự cân bằng), và khi có dầm (kN)</summary>
    public double PmaxNoStrap { get; set; }
    public double PminNoStrap { get; set; }
    public double PmaxStrap { get; set; } = double.NaN;
    public bool HasStrap { get; set; }
    public double N1 { get; set; }
    public double R1 { get; set; } = double.NaN;
    /// <summary>[CặpĐàiBiên] true = dầm cân bằng chung với đài biên đối diện; e2 của đài đối diện (m); R1 nhỏ nhất qua các tổ hợp (kN)</summary>
    public bool OppositePair { get; set; }
    public double E2 { get; set; }
    public double R1Min { get; set; } = double.NaN;
    public double Span { get; set; }
    public string Recommended { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public List<SafetyItem> Items { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

public static class PileSetback
{
    public const string Group = "Lùi cọc";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v, string fmt = "0.##") => double.IsFinite(v) ? v.ToString(fmt, Inv) : "-";

    /// <summary>Khoảng lùi áp dụng cho cạnh ranh giới b: PileSetback của cạnh (≥ 0) hoặc thiết lập chung.</summary>
    public static double SetbackOf(FoundationSettings fs, PropertyBoundary b) =>
        b != null && b.PileSetback >= 0 ? b.PileSetback : Math.Max(fs?.PileSetback ?? 0.5, 0);

    /// <summary>Các cạnh ranh giới có khoảng lùi &gt; 0 (khi bật FoundationSettings.PileSetbackEnabled).</summary>
    public static List<(PropertyBoundary B, double Coord, double Setback)> Lines(Project p)
    {
        var fs = p?.Foundation;
        if (fs == null || !fs.PileSetbackEnabled) return new List<(PropertyBoundary B, double Coord, double Setback)>();
        return EccentricFooting.Lines(p).Select(t => (B: t.B, Coord: t.Coord, Setback: SetbackOf(fs, t.B))).Where(t => t.Setback > 1e-6).ToList();
    }

    public static bool Active(Project p) => Lines(p).Count > 0;

    /// <summary>Ghi chú phương pháp ép cọc.</summary>
    public static string MethodNote(PilePressMethod m) => m switch
    {
        PilePressMethod.Robot => "Ép robot (máy ép tĩnh tự hành): cần khoảng lùi lớn hơn, thường ≥ 1,0–1,5 m tuỳ máy (giá ép biên) – kiểm tra với đơn vị thi công",
        PilePressMethod.Weight => "Ép tải (dàn ép + đối trọng): khoảng lùi thường ≥ 0,5–0,7 m – kiểm tra với đơn vị thi công",
        _ => "Ép neo: khoảng lùi thường ≥ 0,5 m – kiểm tra với đơn vị thi công"
    };

    /// <summary>Dịch nhóm cọc (toạ độ so với tim cột) vào trong lô đất để tim (mép) cọc cách ranh giới ≥ khoảng lùi; lập lại kích thước đài:
    /// mép đài = mép cọc + khoảng mép, tối thiểu phủ cột; "Đài vươn ra sát ranh" → mép đài phía ranh giới kéo tới ranh giới (trừ khoảng hở).
    /// Không phải dịch → trả lại nguyên giá trị (Info.Shifted = false). Info = null nếu không có cạnh ranh giới liên quan.</summary>
    public static (List<(double X, double Y)> Piles, double L, double B, double Ox, double Oy, PileSetbackInfo Info) Adjust(Project p, double x, double y, double hc, double bc,
        double d, double edge, List<(double X, double Y)> piles, double L, double B, double ox, double oy)
    {
        var lines = Lines(p);
        if (lines.Count == 0 || piles == null || piles.Count == 0) return (piles, L, B, ox, oy, null);
        var fs = p.Foundation;
        double tol = Math.Max(fs.BoundaryTolerance, 0), cl = Math.Max(fs.BoundaryClearance, 0), add = fs.PileSetbackToFace ? d / 2 : 0;
        var info = new PileSetbackInfo { Setback = Math.Max(fs.PileSetback, 0), ToFace = fs.PileSetbackToFace, CapToBoundary = fs.CapToBoundary, PressMethod = fs.PilePressMethod };
        double loX = double.NegativeInfinity, hiX = double.PositiveInfinity, loY = double.NegativeInfinity, hiY = double.PositiveInfinity;
        double minX = piles.Min(q => q.X), maxX = piles.Max(q => q.X), minY = piles.Min(q => q.Y), maxY = piles.Max(q => q.Y);
        var rel = new List<(PropertyBoundary B, double C, double S, double Need)>();
        foreach (var (b, c, s) in lines)
        {
            double need;
            switch (b.Side)
            {
                case BoundarySide.Left:
                    if (x < c - tol) continue;
                    need = c + s + add - (x + minX); loX = Math.Max(loX, need); break;
                case BoundarySide.Right:
                    if (x > c + tol) continue;
                    need = c - s - add - (x + maxX); hiX = Math.Min(hiX, need); break;
                case BoundarySide.Front:
                    if (y < c - tol) continue;
                    need = c + s + add - (y + minY); loY = Math.Max(loY, need); break;
                default:
                    if (y > c + tol) continue;
                    need = c - s - add - (y + maxY); hiY = Math.Min(hiY, need); break;
            }
            // chỉ ghi cạnh liên quan: phải dịch, hoặc nhóm cọc nằm gần ranh (≤ khoảng lùi + 1 m)
            if (b.Inward * need > -1.0) rel.Add((b, c, s, need));
        }
        if (rel.Count == 0) return (piles, L, B, ox, oy, null);
        double Pick(double lo, double hi, string axis)
        {
            if (lo <= 1e-9 && hi >= -1e-9) return 0;
            if (lo > hi + 1e-9)
            {
                info.Notes.Add($"Cảnh báo: khoảng lùi hai phía ranh giới theo {axis} không thoả đồng thời (lô đất hẹp) – đặt nhóm cọc giữa hai giới hạn");
                return (lo + hi) / 2;
            }
            return lo > 1e-9 ? lo : hi;
        }
        double sx = RoundMm(Pick(loX, hiX, "X")), sy = RoundMm(Pick(loY, hiY, "Y"));
        info.ShiftX = sx; info.ShiftY = sy;
        var np = sx == 0 && sy == 0 ? piles : piles.Select(q => (X: Math.Round(q.X + sx, 6) + 0.0, Y: Math.Round(q.Y + sy, 6) + 0.0)).ToList();
        minX = np.Min(q => q.X); maxX = np.Max(q => q.X); minY = np.Min(q => q.Y); maxY = np.Max(q => q.Y);
        foreach (var (b, c, s, need) in rel)
        {
            double dist = b.Side switch
            {
                BoundarySide.Left => x + minX - c, BoundarySide.Right => c - (x + maxX),
                BoundarySide.Front => y + minY - c, _ => c - (y + maxY)
            } - add;
            info.Sides.Add(new SetbackSide
            {
                Side = b.Side, Name = PropertyBoundary.SideName(b.Side), Boundary = c, Setback = s, Distance = Math.Round(dist, 6),
                Shift = b.IsVertical ? sx : sy
            });
        }
        if (!info.Shifted) return (piles, L, B, ox, oy, info);
        // ---- đài mới (toạ độ so với tim cột) ----
        double xl = Math.Min(minX - d / 2 - edge, -hc / 2), xr = Math.Max(maxX + d / 2 + edge, hc / 2);
        double yl = Math.Min(minY - d / 2 - edge, -bc / 2), yr = Math.Max(maxY + d / 2 + edge, bc / 2);
        // giữ phía không dịch như đài cũ (không thu nhỏ đài về phía trong)
        xr = Math.Max(xr, sx > 0 ? ox + L / 2 : xr); xl = Math.Min(xl, sx < 0 ? ox - L / 2 : xl);
        yr = Math.Max(yr, sy > 0 ? oy + B / 2 : yr); yl = Math.Min(yl, sy < 0 ? oy - B / 2 : yl);
        foreach (var (b, c, s, need) in rel)
        {
            switch (b.Side)
            {
                case BoundarySide.Left:
                    if (fs.CapToBoundary && sx > 0) xl = Math.Min(xl, c + cl - x);
                    xl = Math.Max(xl, Math.Min(c + cl - x, -hc / 2)); break;
                case BoundarySide.Right:
                    if (fs.CapToBoundary && sx < 0) xr = Math.Max(xr, c - cl - x);
                    xr = Math.Min(xr, Math.Max(c - cl - x, hc / 2)); break;
                case BoundarySide.Front:
                    if (fs.CapToBoundary && sy > 0) yl = Math.Min(yl, c + cl - y);
                    yl = Math.Max(yl, Math.Min(c + cl - y, -bc / 2)); break;
                default:
                    if (fs.CapToBoundary && sy < 0) yr = Math.Max(yr, c - cl - y);
                    yr = Math.Min(yr, Math.Max(c - cl - y, bc / 2)); break;
            }
        }
        double nL = Math.Round(xr - xl, 3), nB = Math.Round(yr - yl, 3);
        double nox = Math.Round((xl + xr) / 2, 4) + 0.0, noy = Math.Round((yl + yr) / 2, 4) + 0.0;
        info.Notes.Add($"Nhóm cọc dịch vào trong lô đất Δx = {F(sx * 1000, "0")} mm, Δy = {F(sy * 1000, "0")} mm để {(info.ToFace ? "mép" : "tim")} cọc cách ranh giới ≥ "
                       + string.Join("; ", info.Sides.Select(q => $"{q.Name.ToLowerInvariant()} {F(q.Setback * 1000, "0")} mm")) + $" – đài {F(nL * 1000, "0")}×{F(nB * 1000, "0")} mm, "
                       + (info.CapToBoundary ? "đài vươn ra sát ranh (phần công xôn ngoài hàng cọc)" : "mép đài theo cọc (tối thiểu phủ cột)"));
        return (np, nL, nB, nox, noy, info);
    }

    private static double PartnerN(EccPlan plan, EccNode ep, string combo)
    {
        var q = plan?.Get(ep.StrapX ? ep.PartnerX : ep.PartnerY);
        if (q == null) return 0;
        foreach (var l in q.Loads) if (l.Combo == combo) return l.N;
        return 0;
    }

    private static double RoundMm(double v) => Math.Abs(v) < 1e-9 ? 0 : v > 0 ? Math.Ceiling(v * 1000 - 1e-6) / 1000 : -Math.Ceiling(-v * 1000 - 1e-6) / 1000;

    /// <summary>Kiểm tra bổ sung cho đài đã lùi cọc: khoảng lùi, phản lực cọc có / không dầm cân bằng, cọc chịu kéo, công xôn đài (uốn – tăng thép lớp trên nếu cần, cắt),
    /// xoắn, chọc thủng chu vi bị cắt; đề xuất phương án. Ghi vào info.Items và dsn.Safety.</summary>
    public static void Attach(Project p, PileCapDesign dsn, PileSetbackInfo info, EccPlan plan, EccNode ep, List<BaseLoad> loads, double P, double Pt,
        Material conc, Material main, double cover)
    {
        if (p == null || dsn == null || info == null || loads == null || loads.Count == 0) return;
        var piles = dsn.Piles;
        if (piles.Count == 0) return;
        double d = dsn.PileSize, H = dsn.H, h0 = dsn.H0, G = dsn.Weight;
        info.GroupEx = Math.Round(piles.Average(q => q.X), 6); info.GroupEy = Math.Round(piles.Average(q => q.Y), 6);
        var it = info.Items;
        foreach (var sd in info.Sides)
            it.Add(SafetyItem.Make(Group, $"Khoảng lùi {(info.ToFace ? "mép" : "tim")} cọc – {sd.Name.ToLowerInvariant()} ≥ {F(sd.Setback * 1000, "0")} mm", sd.Distance, sd.Setback, "m",
                "Thi công ép cọc cạnh nhà giáp ranh: " + MethodNote(info.PressMethod), "Đủ khoảng trống cho máy ép", "Cọc quá sát ranh giới – không ép được, phải dịch cọc vào trong", true));
        // ---- phản lực cọc: không dầm cân bằng (nhóm cọc lệch tâm tự cân bằng) ----
        double pmx = double.MinValue, pmn = double.MaxValue;
        foreach (var l in loads)
        {
            var f = PileCapDesigner.PileForces(l, piles, G, H);
            pmx = Math.Max(pmx, f.Max()); pmn = Math.Min(pmn, f.Min());
        }
        info.PmaxNoStrap = pmx; info.PminNoStrap = pmn;
        info.N1 = loads.Max(l => l.N);
        info.HasStrap = ep != null && (ep.StrapX || ep.StrapY);
        if (info.HasStrap)
        {
            info.PmaxStrap = dsn.PMax;
            info.Span = ep.StrapX ? ep.SpanX : ep.SpanY;
            var g = loads.OrderByDescending(l => l.N).First();
            // [CặpĐàiBiên] đài đối diện lệch tâm e2: R1 = [N1·(L − e2) − N2·e2]/(L − e1 − e2) với N2 cùng tổ hợp (e2 = 0: R1 = N1·L/(L − e1))
            info.E2 = ep.StrapX ? ep.PartnerEX : ep.PartnerEY;
            info.R1 = EccentricFooting.LeverReaction(g.N, info.E2 > 0 ? PartnerN(plan, ep, g.Combo) : 0, info.Span, Math.Abs(ep.StrapX ? info.GroupEx : info.GroupEy), info.E2);
        }
        if (info.HasStrap && EccentricPairs.Opposite(ep))
        {
            info.OppositePair = true;
            double e1 = Math.Abs(ep.StrapX ? info.GroupEx : info.GroupEy), e2 = info.E2, Lsp = Math.Max(info.Span, 0.1);
            double rmin = double.PositiveInfinity; string cmin = "";
            foreach (var l in loads)
            {
                double r1 = EccentricFooting.LeverReaction(l.N, PartnerN(plan, ep, l.Combo), Lsp, e1, e2);
                if (r1 < rmin) { rmin = r1; cmin = l.Combo; }
            }
            info.R1Min = rmin;
            double ratio = (e1 + e2) / Lsp;
            var st = rmin >= 0.25 * Math.Max(info.N1, 1e-6) && ratio <= 0.5 ? CheckStatus.Ok : rmin > 0 ? CheckStatus.Warning : CheckStatus.Fail;
            it.Add(new SafetyItem
            {
                Group = Group, Name = "Hai đài biên đối diện chung dầm cân bằng: R1 > 0 mọi tổ hợp (hợp lực trong nhóm cọc)", Value = rmin, Limit = 0.25 * info.N1, Unit = "kN", Inverse = true, Status = st,
                Formula = $"R1 = [N1·(L − e2) − N2·e2]/(L − e1 − e2), L = {F(Lsp)} m, e1 = {F(e1, "0.000")}, e2 = {F(e2, "0.000")} m; (e1 + e2)/L = {F(ratio, "0.00")}; tổ hợp {cmin}",
                Explanation = st == CheckStatus.Ok ? "Dầm cân bằng chung giữa hai đài biên cân bằng mô men lệch tâm cả hai phía; mỗi đài nhận hợp lực R1 đặt tại trọng tâm nhóm cọc"
                    : "Lệch tâm hai phía lớn so với nhịp (R1 nhỏ / âm: cọc gần như không chịu tải hoặc bị nhổ) – dùng đài chung / băng cọc ngang nhịp, hoặc bố trí 3–4 cọc dưới cột giảm e"
            });
        }
        it.Add(SafetyItem.Make(Group, "Phản lực cọc Pmax – KHÔNG dầm cân bằng (mô men (N + G)·e do nhóm cọc)", pmx, P, "kN", "Pi = (N + G)/n + ax·(xi − x̄) + ay·(yi − ȳ)",
            "Nhóm cọc lệch tâm tự cân bằng được", "Cọc phía cột quá tải khi không có dầm cân bằng – cần dầm móng cân bằng / đài chung", false, info.HasStrap ? CheckStatus.Info : CheckStatus.Fail));
        it.Add(SafetyItem.Make(Group, "Cọc chịu kéo Pmin ≥ −[Pk] – KHÔNG dầm cân bằng", pmn < 0 ? -pmn : 0, pmn < 0 ? Pt : Math.Max(Pt, 1), "kN", "Pmin = " + F(pmn, "0.0") + " kN",
            "Không có cọc bị nhổ quá sức chịu kéo", "Cọc xa cột bị nhổ – cần dầm cân bằng / tăng [Pk]", false, info.HasStrap ? CheckStatus.Info : CheckStatus.Fail));
        if (info.HasStrap)
            it.Add(SafetyItem.Make(Group, "Phản lực cọc Pmax – CÓ dầm cân bằng (R1 = N1·L/(L − e))", dsn.PMax, P, "kN",
                $"R1 = {F(info.R1, "0.0")} kN (N1 = {F(info.N1, "0.0")}, L = {F(info.Span)} m, e = {F(Math.Abs(ep.StrapX ? info.GroupEx : info.GroupEy), "0.000")} m)",
                "Phản lực cọc gần đều theo phương dầm", "Quá tải cọc – tăng số cọc"));

        // ---- công xôn đài (cột nằm ngoài phạm vi hàng cọc) ----
        double Arm(bool ax)
        {
            double mn = piles.Min(q => ax ? q.X : q.Y) - d / 2, mx = piles.Max(q => ax ? q.X : q.Y) + d / 2, half = (ax ? dsn.ColumnH : dsn.ColumnB) / 2;
            if (mn > half - 1e-9) return mn;
            if (mx < -half + 1e-9) return -mx;
            return 0;
        }
        info.CantileverX = Arm(true); info.CantileverY = Arm(false);
        double Nmax = Math.Max(info.N1, 0);
        foreach (bool ax in new[] { true, false })
        {
            double a = ax ? info.CantileverX : info.CantileverY;
            if (a <= 1e-6) continue;
            double width = ax ? dsn.B : dsn.L;
            string role = ax ? "Lớp trên phương L" : "Lớp trên phương B";
            var bar = dsn.Bars.FirstOrDefault(b => b.Role == role);
            double M = Nmax * a;
            int dia = bar?.Dia > 0 ? bar.Dia : 12;
            double sp = bar?.Spacing > 0 ? bar.Spacing : 0.2;
            double h0t = H - cover - dia / 2000.0;
            double Mu = RcSection.MomentCapacity(width, h0t, RcSection.BarArea(dia) / sp * width, 0, cover, conc, main);
            if (M > Mu + 1e-6 && bar != null)
            {
                // tăng thép lớp trên theo mô men công xôn
                var f = RcSection.DesignRect(M, width, h0t, 0.05, conc, main, 0.05);
                double req = Math.Max(f.As, M / (0.9 * main.Rs * h0t)) / width;
                var sc = RcSection.ChooseSpacing(_ => req, new[] { 12, 14, 16, 18, 20, 22, 25 }, 0.20, 0.10, 0.15, 12);
                bar.Dia = sc.Dia; bar.Spacing = sc.Spacing;
                bar.Count = (int)Math.Ceiling((width - 2 * cover) / Math.Max(sc.Spacing, 0.05) - 1e-9) + 1;
                if (ax) dsn.BarsTopL = sc.Text; else dsn.BarsTopB = sc.Text;
                dsn.BarsTop = dsn.BarsTopL == dsn.BarsTopB ? dsn.BarsTopL : dsn.BarsTopL + " / " + dsn.BarsTopB;
                info.TopBarsRaised += (info.TopBarsRaised.Length > 0 ? "; " : "") + (ax ? "phương L " : "phương B ") + sc.Text;
                dia = sc.Dia; sp = sc.Spacing; h0t = H - cover - dia / 2000.0;
                Mu = RcSection.MomentCapacity(width, h0t, RcSection.BarArea(dia) / sp * width, 0, cover, conc, main);
                dsn.Messages.Add($"Thép lớp trên {(ax ? "phương L" : "phương B")} tăng lên {sc.Text} theo mô men công xôn đài M = N·a = {F(M, "0.0")} kN.m (cột sát ranh nằm ngoài hàng cọc)");
            }
            double Qb = PileCapChecks.ShearCapacity(width, h0, a, conc.Rbt);
            if (Math.Abs(M) > Math.Abs(info.MCant)) { info.MCant = M; info.MCantCap = Mu; info.VCant = Nmax; info.VCantCap = Qb; }
            it.Add(SafetyItem.Make(Group, $"Uốn công xôn đài tại mép hàng cọc ({(ax ? "phương L" : "phương B")}, thép lớp trên)", M, Mu, "kN.m",
                $"M = Nmax·a = {F(Nmax, "0.0")}×{F(a, "0.000")}; Mu với {(bar != null ? "Ø" + dia + "a" + F(sp * 1000, "0") : "thép lớp trên")} trên bề rộng {F(width)} m",
                "Phần đài công xôn đỡ cột sát ranh đủ chịu uốn", "Không đủ thép lớp trên công xôn – tăng thép / chiều cao đài"));
            it.Add(SafetyItem.Make(Group, $"Cắt công xôn đài tại hàng cọc ({(ax ? "phương L" : "phương B")})", Nmax, Qb, "kN",
                $"Q = Nmax ≤ Qb = 1,5·Rbt·b·h0²/c, c = a = {F(a, "0.000")} m (TCVN 5574:2018 8.1.3.3)",
                "Bê tông đài đủ chịu cắt", "Không đủ chịu cắt – tăng chiều cao đài / đặt cốt đai"));
        }
        if (info.TopBarsRaised.Length > 0) RebarNumbering.Renumber(dsn.Bars);
        // ---- xoắn: lệch tâm vuông góc phương dầm cân bằng ----
        if (info.HasStrap)
        {
            double eP = ep.StrapX && !ep.StrapY ? Math.Abs(info.GroupEy) : ep.StrapY && !ep.StrapX ? Math.Abs(info.GroupEx) : 0;
            double bw = ep.StrapX ? dsn.B : dsn.L;
            double b0 = Math.Min(bw, H), h1 = Math.Max(bw, H);
            info.Torsion = Nmax * eP;
            info.TorsionCap = 0.1 * conc.Rb * b0 * b0 * h1;
            it.Add(SafetyItem.Make(Group, "Xoắn đài do lệch tâm vuông góc dầm cân bằng", info.Torsion, info.TorsionCap, "kN.m",
                "T = N·e⊥ ≤ 0,1·Rb·b²·h (TCVN 5574:2018 8.1.4 – điều kiện bê tông chịu nén, đơn giản hoá)",
                eP > 1e-6 ? "Đài đủ chịu xoắn" : "Không có lệch tâm vuông góc phương dầm – không xoắn", "Đài chịu xoắn lớn – thêm dầm cân bằng phương kia / tăng tiết diện", false, CheckStatus.Fail));
        }
        // ---- chọc thủng cột sát mép đài ----
        var pc = dsn.Checks.FirstOrDefault(c => c.Name.StartsWith("Chọc thủng đài do cột"));
        if (pc != null)
            it.Add(SafetyItem.Make(Group, "Chọc thủng do cột – chu vi bị cắt bởi mép đài", pc.Demand, pc.Capacity, "kN", "PileCapChecks.ColumnPunchCapacityCut (tháp 45° bị cắt bởi mép đài sát ranh)",
                "Đạt chọc thủng", "Không đạt chọc thủng – tăng chiều cao đài"));
        if (info.HasStrap)
            it.Add(new SafetyItem
            {
                Group = Group, Name = "Dầm móng cân bằng: M = N1·e, Q = R1 − N1", Value = info.N1 * Math.Abs(ep.StrapX ? info.GroupEx : info.GroupEy), Unit = "kN.m", Status = CheckStatus.Info,
                Formula = "Thiết kế chi tiết ở mục dầm móng cân bằng DM", Explanation = $"Q ≈ {F(info.R1 - info.N1, "0.0")} kN; dầm phải cứng hơn cột (I/L ≥ 2·Ic/H)"
            });

        // ---- phương án ----
        double e = Math.Max(Math.Abs(info.GroupEx), Math.Abs(info.GroupEy));
        bool partner = ep != null && (ep.PartnerX != 0 || ep.PartnerY != 0);
        double span = partner ? Math.Max(ep.PartnerX != 0 ? ep.SpanX : ep.SpanY, 0.1) : 0;
        bool selfOk = pmx <= P + 1e-6 && pmn >= -Pt - 1e-6;
        bool pairBad = info.OppositePair && (info.R1Min < 0.25 * info.N1 || (e + info.E2) / Math.Max(span, 0.1) > 0.5);
        info.Options.Add(info.OppositePair
            ? $"(a) Một dầm cân bằng chung giữa hai đài biên đối diện (L = {F(span)} m, e1 = {F(e, "0.000")}, e2 = {F(info.E2, "0.000")} m): R1 = [N1(L − e2) − N2·e2]/(L − e1 − e2) = {F(info.R1, "0.0")} kN (nhỏ nhất {F(info.R1Min, "0.0")} kN), dầm chịu mô men ≈ N·e, lực cắt nhỏ"
            : partner
            ? $"(a) Dầm móng cân bằng tới cột trong (L = {F(span)} m): R1 = N1·L/(L − e) = {F(EccentricFooting.LeverReaction(info.N1, 0, span, e), "0.0")} kN, phản lực cọc gần đều theo phương dầm"
            : "(a) Dầm móng cân bằng: không có cột trong cùng trục trong phạm vi nối dầm");
        info.Options.Add($"(b) Đài console tự cân bằng: Pmax = {F(pmx, "0.0")} kN, Pmin = {F(pmn, "0.0")} kN so với [P] = {F(P, "0")}, [Pk] = {F(Pt, "0")} → {(selfOk ? "đạt" : "KHÔNG ĐẠT")}; "
                         + $"công xôn M = {F(info.MCant, "0.0")} kN.m, Q = {F(info.VCant, "0.0")} kN; mô men N·e = {F(info.N1 * e, "0.0")} kN.m do nhóm cọc + giằng móng");
        info.Options.Add(pairBad
            ? "(c) Đài chung / băng cọc ngang nhịp giữa hai cột biên (hoặc 3–4 cọc dưới cột giảm lệch tâm): nên dùng – R1 nhỏ / âm hoặc (e1 + e2)/L > 0,5"
            : !info.OppositePair && partner && e / span > 0.25
            ? $"(c) Đài chung với cột trong: nên dùng – e/L = {F(e / span, "0.00")} > 0,25 (dầm cân bằng rất lớn)"
            : "(c) Đài chung với cột trong: dùng khi hai đài gần chạm nhau hoặc e/L > 0,25 (khai báo bằng bố trí cọc tuỳ chọn cho đài chung)");
        if (e <= 0.05) { info.Recommended = "Đài thường"; info.Recommendation = "Độ lệch tâm nhỏ (≤ 50 mm) – đài làm việc như đài thường"; }
        else if (pairBad) { info.Recommended = "(c) Đài chung / băng cọc ngang nhịp"; info.Recommendation = "Hai đài biên đối diện lệch tâm lớn so với nhịp – dầm cân bằng chung không hiệu quả (R1 nhỏ / âm); đề xuất đài chung hoặc băng cọc ngang nhịp, hoặc 3–4 cọc dưới cột"; }
        else if (info.OppositePair) { info.Recommended = "(a) Dầm cân bằng chung hai đài biên"; info.Recommendation = "Nhà một nhịp, ranh giới hai phía: một dầm móng cân bằng giữa hai đài biên đối diện cân bằng mô men lệch tâm cả hai phía"; }
        else if (!info.OppositePair && partner && e / span > 0.25) { info.Recommended = "(c) Đài chung"; info.Recommendation = "Lệch tâm lớn so với nhịp tới cột trong – đề xuất gộp đài với cột trong (đài chung)"; }
        else if (partner) { info.Recommended = "(a) Dầm móng cân bằng"; info.Recommendation = "Có cột trong cùng trục – đề xuất dầm móng cân bằng tiếp nhận mô men N·e (đòn bẩy)"; }
        else if (selfOk) { info.Recommended = "(b) Đài console"; info.Recommendation = "Không có cột trong để nối dầm – đài console tự cân bằng đạt phản lực cọc; giằng móng tiếp nhận mô men lệch tâm"; }
        else { info.Recommended = "Tăng số cọc / đài chung"; info.Recommendation = "Không có cột trong để nối dầm và đài console không đạt phản lực cọc – tăng số cọc hoặc dùng đài chung"; }
        dsn.Messages.Add($"Lùi cọc khỏi ranh giới để ép cọc (≥ {F(info.Setback * 1000, "0")} mm{(info.ToFace ? " tới mép cọc" : " tới tim cọc")}): nhóm cọc lệch tâm e = {F(e * 1000, "0")} mm so với tim cột – đề xuất {info.Recommended}");
        foreach (var x in it.Where(q => q.Status == CheckStatus.Fail)) dsn.Messages.Add("Cảnh báo: lùi cọc – " + x.Name + ": " + x.Explanation);
        foreach (var nt in info.Notes) dsn.Messages.Add(nt.StartsWith("Cảnh báo") ? nt : "Lùi cọc: " + nt);
        if (dsn.Safety != null)
        {
            dsn.Safety.Items.AddRange(it);
            foreach (var x in it.Where(q => q.Status == CheckStatus.Fail)) dsn.Safety.Warnings.Add("Lùi cọc – " + x.Name + ": " + x.Explanation);
        }
    }
}

/// <summary>[CặpĐàiBiên] Hai móng / đài biên ĐỐI DIỆN (nhà 1 nhịp, ranh giới hai phía) nối chung một dầm móng cân bằng.
/// Đòn bẩy: R1 = [N1·(L − e2) − N2·e2]/(L − e1 − e2), R2 = [N2·(L − e1) − N1·e1]/(L − e1 − e2) (R1 + R2 = N1 + N2; đối xứng N1 = N2, e1 = e2 → R1 = N1:
/// dầm chịu mô men không đổi N·e, lực cắt ≈ 0). Kế hoạch lệch tâm chỉ có e2 ƯỚC LƯỢNG (theo móng đơn sơ bộ) – với đài cọc lùi khỏi ranh giới
/// ước lượng này có thể sai rất nhiều (e2 → L/2 làm R1 → 0, cọc gần như không chịu lực / bị nhổ). Sync thay e2 bằng độ lệch tâm thật của móng đối diện
/// sau mỗi lượt thiết kế; trả về true nếu có thay đổi &gt; 5 mm (cần thiết kế lại).</summary>
public static class EccentricPairs
{
    public const double Tolerance = 0.005;

    public static bool Sync(EccPlan plan)
    {
        if (plan == null) return false;
        bool changed = false;
        foreach (var n in plan.Nodes.Values)
        {
            if (n.StrapX && n.PartnerEX > 0)
            {
                var q = plan.Get(n.PartnerX);
                if (q != null && q.Designed && Math.Abs(Math.Abs(q.FinalOx) - n.PartnerEX) > Tolerance) { n.PartnerEX = Math.Abs(q.FinalOx); changed = true; }
            }
            if (n.StrapY && n.PartnerEY > 0)
            {
                var q = plan.Get(n.PartnerY);
                if (q != null && q.Designed && Math.Abs(Math.Abs(q.FinalOy) - n.PartnerEY) > Tolerance) { n.PartnerEY = Math.Abs(q.FinalOy); changed = true; }
            }
        }
        return changed;
    }

    /// <summary>true nếu nút nối dầm cân bằng với móng biên đối diện (theo X hoặc Y).</summary>
    public static bool Opposite(EccNode n) => n != null && ((n.StrapX && n.PartnerEX > 0) || (n.StrapY && n.PartnerEY > 0));
}
