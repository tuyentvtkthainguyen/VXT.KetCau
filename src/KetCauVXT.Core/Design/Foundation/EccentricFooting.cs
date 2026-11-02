using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

/// <summary>Giới hạn mép móng do ranh giới đất tại một chân cột (toạ độ tuyệt đối, đã trừ khoảng hở) và các cạnh ranh giới mà cột nằm sát.</summary>
public sealed class EdgeLimits
{
    public double XMin = double.NegativeInfinity, XMax = double.PositiveInfinity, YMin = double.NegativeInfinity, YMax = double.PositiveInfinity;
    /// <summary>Mép cột cách ranh giới ≤ dung sai (cột sát ranh)</summary>
    public bool EdgeLeft, EdgeRight, EdgeFront, EdgeBack;
    public bool EdgeX => EdgeLeft || EdgeRight;
    public bool EdgeY => EdgeFront || EdgeBack;
    /// <summary>Hướng vào trong lô đất theo X / Y của cạnh ranh giới cột sát (+1, −1; 0 = không sát / sát cả hai)</summary>
    public int InwardX => EdgeLeft && !EdgeRight ? 1 : EdgeRight && !EdgeLeft ? -1 : 0;
    public int InwardY => EdgeFront && !EdgeBack ? 1 : EdgeBack && !EdgeFront ? -1 : 0;
    public bool Any => !double.IsInfinity(XMin) || !double.IsInfinity(XMax) || !double.IsInfinity(YMin) || !double.IsInfinity(YMax);

    public string Sides()
    {
        var l = new List<string>();
        if (EdgeLeft) l.Add("trái");
        if (EdgeRight) l.Add("phải");
        if (EdgeFront) l.Add("trước");
        if (EdgeBack) l.Add("sau");
        return string.Join(" + ", l);
    }
}

/// <summary>Áp lực đáy móng chữ nhật chịu nén lệch tâm (tiêu chuẩn, kN/m²).</summary>
public struct PressureResult
{
    public double Ptb, Pmax, Pmin;
    /// <summary>Tỷ lệ diện tích tiếp xúc (1 = toàn bộ đáy móng tiếp xúc)</summary>
    public double Contact;
    /// <summary>Độ lệch tâm hợp lực theo L, B (m)</summary>
    public double EL, EB;
    /// <summary>true = hợp lực ra ngoài lõi, áp lực tam giác trên vùng tiếp xúc 3c0</summary>
    public bool Partial;
    public string Combo;
}

/// <summary>Kế hoạch lệch tâm của một chân cột trong một lượt thiết kế móng.</summary>
public sealed class EccNode
{
    public int NodeId;
    public Node Node;
    public FrameElement Column;
    public double X, Y, Hc, Bc, ColHeight;
    public EdgeLimits Limits = new();
    public EccentricOverride Ovr;
    public bool Manual;
    public double ManualOx, ManualOy;
    /// <summary>Phương án áp dụng / đề xuất</summary>
    public EccentricScheme Scheme = EccentricScheme.SelfBalanced, Recommended = EccentricScheme.SelfBalanced;
    public string Reason = "";
    public int PartnerX, PartnerY;
    public double SpanX, SpanY;
    /// <summary>Độ lệch tâm (ước lượng) của móng đối diện khi hai móng biên nối với nhau (nhà 1 nhịp)</summary>
    public double PartnerEX, PartnerEY;
    /// <summary>Dầm cân bằng theo X / Y tiếp nhận mô men trong mặt phẳng dầm</summary>
    public bool StrapX, StrapY;
    /// <summary>Móng kết hợp: nút cột còn lại; Secondary = nút này được thiết kế chung với nút chính</summary>
    public int CombinedWith;
    public bool Secondary;
    public List<(int From, bool AlongX)> RelievedBy = new();
    public List<BaseLoad> Loads = new();
    public double EstOx, EstOy;
    public double FinalOx, FinalOy;
    public bool Designed;
    public Dictionary<string, double> DeltaRX = new(), DeltaRY = new();
    public bool HasEccentricity => Manual || Limits.Any || StrapX || StrapY || CombinedWith != 0;
}

/// <summary>Kế hoạch lệch tâm cho một nhóm chân cột (một loại móng) – EccentricFooting.Plan.</summary>
public sealed class EccPlan
{
    public FoundationSettings Fs = new();
    public Dictionary<int, EccNode> Nodes = new();
    public List<string> Notes = new();
    public EccNode Get(int id) => Nodes.TryGetValue(id, out var n) ? n : null;

    /// <summary>Giảm tải β·ΣΔR cho móng cột trong ở tổ hợp combo (kN).</summary>
    public double Relief(EccNode n, string combo)
    {
        if (n == null || n.RelievedBy.Count == 0) return 0;
        double beta = Math.Clamp(Fs.StrapReliefFactor, 0, 1), s = 0;
        foreach (var (from, ax) in n.RelievedBy)
        {
            var e = Get(from);
            if (e == null || !e.Designed) continue;
            var d = ax ? e.DeltaRX : e.DeltaRY;
            if (d.TryGetValue(combo ?? "", out double v) && v > 0) s += v;
        }
        return beta * s;
    }
}

/// <summary>Móng lệch tâm do ranh giới đất (nhà phố – cột sát ranh): xác định cột sát ranh, độ lệch tâm, áp lực đáy móng lệch tâm,
/// phương án (a) dầm móng cân bằng (đòn bẩy), (b) móng lệch tâm tự cân bằng, (c) móng kết hợp; thiết kế dầm cân bằng.
/// <list type="bullet">
/// <item>Độ lệch tâm e0 = tâm đáy móng − tim cột: mép móng = ranh giới − khoảng hở (FoundationSettings.BoundaryClearance).</item>
/// <item>(b) M = N·e0 + M0 + Q·Df (cộng trị tuyệt đối – thiên về an toàn); p = N/A·(1 ± 6e/L); e &gt; L/6: pmax = 2N/(3B·c0), c0 = L/2 − e, tiếp xúc 3c0 ≥ 75%L
/// (TCVN 9362:2012 mục 4.6 – điều kiện pmax ≤ 1,2R, ptb ≤ R); mô men ΔM = N·e0 truyền vào chân cột / hệ giằng.</item>
/// <item>(a) Dầm cân bằng (đòn bẩy): R1 = N1·L/(L − e) (móng đối diện lệch tâm e2: R1 = [N1(L − e2) − N2·e2]/(L − e1 − e2));
/// móng cột trong N2' = N2 − β·(R1 − N1), β = 0,5 (thực hành: chỉ kể 50% phần giảm tải); dầm DM: M = N1·e1 (lớn nhất phía cột biên), Q = R1 − N1;
/// dầm phải cứng hơn cột: I_d/L ≥ 2·I_c/H.</item>
/// <item>(c) Móng kết hợp: hợp lực x̄ = (N1x1 + N2x2)/(N1 + N2) trùng trọng tâm đáy móng: chữ nhật L = 2·x̄ (tính từ ranh giới) hoặc hình thang
/// B1 + B2 = 2A/L, x̄ = L(B1 + 2B2)/(3(B1 + B2)).</item>
/// </list></summary>
public static class EccentricFooting
{
    /// <summary>Kích thước móng lệch tâm tự cân bằng lớn nhất khi tăng dần (m) – vượt quá thì kết luận phương án không phù hợp</summary>
    public const double MaxSelfSize = 4.0;
    /// <summary>Dung sai cùng trục khi tìm cột trong để nối dầm cân bằng (m)</summary>
    public const double LineTol = 0.30;
    /// <summary>Tỷ lệ cạnh tối thiểu móng biên (cạnh dọc ranh / cạnh vuông góc ranh)</summary>
    public const double EdgeAspect = 1.5;
    /// <summary>Khe hở tối thiểu giữa móng biên và móng cột trong để dùng móng riêng (m); nhỏ hơn → móng kết hợp</summary>
    public const double MinGap = 0.30;

    // ==================================================================
    // RANH GIỚI ĐẤT
    // ==================================================================
    /// <summary>Toạ độ cạnh ranh giới (X với Trái/Phải, Y với Trước/Sau): trục + Offset ra ngoài lô đất, hoặc Coord.</summary>
    public static double CoordOf(Project p, PropertyBoundary b)
    {
        if (b == null) return double.NaN;
        if (!string.IsNullOrWhiteSpace(b.Grid) && p?.Grids != null)
        {
            var dir = b.IsVertical ? GridDir.X : GridDir.Y;
            var g = p.Grids.FirstOrDefault(q => q.Dir == dir && string.Equals(q.Name?.Trim(), b.Grid.Trim(), StringComparison.OrdinalIgnoreCase));
            if (g != null) return g.Coord - b.Inward * b.Offset;
        }
        return b.Coord;
    }

    /// <summary>Các cạnh ranh giới đang dùng với toạ độ.</summary>
    public static List<(PropertyBoundary B, double Coord)> Lines(Project p) =>
        (p?.Foundation?.Boundaries ?? new List<PropertyBoundary>()).Where(b => b != null && b.Enabled)
            .Select(b => (b, CoordOf(p, b))).Where(t => !double.IsNaN(t.Item2)).ToList();

    /// <summary>true nếu dự án có ranh giới đất hoặc độ lệch tâm nhập tay → bật tính móng lệch tâm.</summary>
    public static bool Active(Project p)
    {
        var fs = p?.Foundation;
        if (fs == null) return false;
        if (Lines(p).Count > 0) return true;
        return fs.Eccentric != null && fs.Eccentric.Any(o => o != null && (!o.AutoOffset && (Math.Abs(o.OffsetX) > 1e-6 || Math.Abs(o.OffsetY) > 1e-6)));
    }

    public static EccentricOverride Find(FoundationSettings fs, int nodeId) => fs?.Eccentric?.FirstOrDefault(o => o != null && o.NodeId == nodeId);

    /// <summary>Ghi / xoá lựa chọn lệch tâm cho các nút; ovr = null → xoá (tự động theo ranh giới).</summary>
    public static void Set(FoundationSettings fs, IEnumerable<int> nodeIds, EccentricOverride ovr)
    {
        if (fs == null) return;
        fs.Eccentric ??= new List<EccentricOverride>();
        foreach (int id in nodeIds.Distinct())
        {
            fs.Eccentric.RemoveAll(o => o == null || o.NodeId == id);
            if (ovr == null) continue;
            var c = ovr.Clone();
            c.NodeId = id;
            fs.Eccentric.Add(c);
        }
    }

    /// <summary>Giới hạn mép móng tại chân cột (x, y) kích thước hc (theo X) × bc (theo Y).</summary>
    public static EdgeLimits Limits(Project p, double x, double y, double hc, double bc)
    {
        var lim = new EdgeLimits();
        var fs = p?.Foundation ?? new FoundationSettings();
        double tol = Math.Max(fs.BoundaryTolerance, 0), cl = Math.Max(fs.BoundaryClearance, 0);
        foreach (var (b, c) in Lines(p))
        {
            switch (b.Side)
            {
                case BoundarySide.Left:
                    if (x < c - tol) continue;
                    lim.XMin = Math.Max(lim.XMin, c + cl);
                    if (x - hc / 2 - c <= tol + 1e-9) lim.EdgeLeft = true;
                    break;
                case BoundarySide.Right:
                    if (x > c + tol) continue;
                    lim.XMax = Math.Min(lim.XMax, c - cl);
                    if (c - (x + hc / 2) <= tol + 1e-9) lim.EdgeRight = true;
                    break;
                case BoundarySide.Front:
                    if (y < c - tol) continue;
                    lim.YMin = Math.Max(lim.YMin, c + cl);
                    if (y - bc / 2 - c <= tol + 1e-9) lim.EdgeFront = true;
                    break;
                default:
                    if (y > c + tol) continue;
                    lim.YMax = Math.Min(lim.YMax, c - cl);
                    if (c - (y + bc / 2) <= tol + 1e-9) lim.EdgeBack = true;
                    break;
            }
        }
        return lim;
    }

    /// <summary>Dịch tâm cần thiết theo một phương: lo = dịch tối thiểu (dương) để mép dưới không vượt giới hạn, hi = dịch tối đa cho phép.</summary>
    private static double Shift(double lo, double hi)
    {
        if (lo <= 1e-9 && hi >= -1e-9) return 0;
        if (lo > hi + 1e-9) return (lo + hi) / 2;
        return lo > 1e-9 ? lo : hi;
    }

    /// <summary>Làm tròn độ dịch lên 1 mm về phía trong lô đất.</summary>
    private static double RoundMm(double v) => v >= 0 ? Math.Ceiling(v * 1000 - 1e-6) / 1000 : -Math.Ceiling(-v * 1000 - 1e-6) / 1000;

    /// <summary>Độ lệch tâm tâm móng so với tim cột (ox, oy) để móng L × B nằm trong ranh giới (cột vẫn nằm trên móng).</summary>
    public static (double Ox, double Oy) Offset(EdgeLimits lim, double x, double y, double L, double B, double hc, double bc)
    {
        if (lim == null) return (0, 0);
        double ox = Shift(lim.XMin - (x - L / 2), lim.XMax - (x + L / 2));
        double oy = Shift(lim.YMin - (y - B / 2), lim.YMax - (y + B / 2));
        double mx = Math.Max(L / 2 - hc / 2, 0), my = Math.Max(B / 2 - bc / 2, 0);
        ox = Math.Clamp(ox, -mx, mx); oy = Math.Clamp(oy, -my, my);
        return (RoundMm(ox) + 0.0, RoundMm(oy) + 0.0);
    }

    /// <summary>Độ lệch tâm áp dụng cho nút n với móng L × B (nhập tay hoặc theo ranh giới).</summary>
    public static (double Ox, double Oy) OffsetFor(EccNode n, double L, double B)
    {
        if (n == null) return (0, 0);
        if (n.Manual)
        {
            double mx = Math.Max(L / 2 - n.Hc / 2, 0), my = Math.Max(B / 2 - n.Bc / 2, 0);
            return (Math.Clamp(n.ManualOx, -mx, mx), Math.Clamp(n.ManualOy, -my, my));
        }
        return Offset(n.Limits, n.X, n.Y, L, B, n.Hc, n.Bc);
    }

    // ==================================================================
    // CÔNG THỨC
    // ==================================================================
    /// <summary>Áp lực đáy móng L × B dưới tổng lực Ntot (kể trọng lượng móng + đất) và mô men mL (biến thiên theo L), mB (theo B) đối với trọng tâm đáy móng:
    /// e/L + e/B ≤ 1/6: p = Ntot/A·(1 ± 6eL/L ± 6eB/B); ngược lại tiếp xúc một phần theo phương lệch tâm trội: pmax = 2Ntot/(3·b·c0)·(1 + 6e'/l'), c0 = l/2 − e,
    /// tiếp xúc 3c0/l (pmin ghi theo công thức tuyến tính, âm = vùng tách nền).</summary>
    public static PressureResult Pressure(double Ntot, double mL, double mB, double L, double B)
    {
        var r = new PressureResult { Contact = 1 };
        double A = L * B;
        if (A <= 1e-12) return r;
        mL = Math.Abs(mL); mB = Math.Abs(mB);
        r.Ptb = Ntot / A;
        double dp = 6 * mL / (B * L * L) + 6 * mB / (L * B * B);
        r.Pmin = r.Ptb - dp;
        if (Ntot <= 1e-9) { r.Pmax = r.Ptb + dp; return r; }
        double eL = mL / Ntot, eB = mB / Ntot;
        r.EL = eL; r.EB = eB;
        if (eL / L + eB / B <= 1.0 / 6 + 1e-12) { r.Pmax = r.Ptb + dp; return r; }
        bool alongL = eL / L >= eB / B;
        double e = alongL ? eL : eB, len = alongL ? L : B, wid = alongL ? B : L, eo = alongL ? eB : eL, lo = alongL ? B : L;
        double c0 = len / 2 - e;
        r.Partial = true;
        if (c0 <= 1e-6) { r.Pmax = 1e9; r.Contact = 0; return r; }
        r.Pmax = 2 * Ntot / (3 * wid * c0) * (1 + 6 * Math.Min(eo / lo, 1.0 / 6));
        r.Contact = Math.Min(1, 3 * c0 / len);
        return r;
    }

    /// <summary>Phản lực tại trọng tâm đáy móng biên theo nguyên lý đòn bẩy (dầm cân bằng tuyệt đối cứng):
    /// R1 = [N1·(L − e2) − N2·e2]/(L − e1 − e2); e2 = 0 → R1 = N1·L/(L − e1).</summary>
    public static double LeverReaction(double N1, double N2, double L, double e1, double e2 = 0)
    {
        double d = L - Math.Abs(e1) - Math.Abs(e2);
        if (d <= 1e-6) return N1;
        return (N1 * (L - Math.Abs(e2)) - N2 * Math.Abs(e2)) / d;
    }

    /// <summary>Lực dọc tính móng cột trong khi có dầm cân bằng: N2' = N2 − β·ΔR, ΔR = R1 − N1 (β ≤ 1; thực hành β = 0,5).</summary>
    public static double InteriorReduced(double N2, double deltaR, double beta) => N2 - Math.Clamp(beta, 0, 1) * Math.Max(deltaR, 0);

    /// <summary>Mô men lớn nhất trong dầm cân bằng (phía cột biên): M = N1·e1.</summary>
    public static double StrapMoment(double N1, double e1) => N1 * Math.Abs(e1);

    /// <summary>Vị trí hợp lực hai lực dọc: x̄ = (N1x1 + N2x2)/(N1 + N2).</summary>
    public static double CombinedResultant(double N1, double x1, double N2, double x2) =>
        Math.Abs(N1 + N2) > 1e-9 ? (N1 * x1 + N2 * x2) / (N1 + N2) : (x1 + x2) / 2;

    /// <summary>Móng hình thang diện tích A, dài L, trọng tâm cách đầu rộng x̄: B1 + B2 = 2A/L, x̄ = L(B1 + 2B2)/(3(B1 + B2)) → (B1, B2).
    /// Hợp lệ khi L/3 ≤ x̄ ≤ L/2 (B2 ≥ 0).</summary>
    public static (double B1, double B2) TrapezoidWidths(double A, double L, double xbar)
    {
        if (L <= 1e-9) return (0, 0);
        double S = 2 * A / L;
        double B2 = S * (3 * xbar / L - 1);
        return (S - B2, B2);
    }

    /// <summary>Trọng tâm hình thang (từ đầu rộng B1): x̄ = L(B1 + 2B2)/(3(B1 + B2)).</summary>
    public static double TrapezoidCentroid(double L, double B1, double B2) => B1 + B2 > 1e-12 ? L * (B1 + 2 * B2) / (3 * (B1 + B2)) : L / 2;

    // ==================================================================
    // KẾ HOẠCH LỆCH TÂM (cột sát ranh, đối tác, phương án)
    // ==================================================================
    /// <summary>Lập kế hoạch lệch tâm cho các chân cột cols (cùng một loại móng). null = dự án không có ranh giới / lệch tâm (giữ nguyên thiết kế cũ).</summary>
    public static EccPlan Plan(Project p, AnalysisResults r, IList<string> combos, IEnumerable<(Node Node, FrameElement Column)> cols)
    {
        if (!Active(p) || r == null) return null;
        var fs = p.Foundation ?? new FoundationSettings();
        var plan = new EccPlan { Fs = fs };
        foreach (var (n, col) in cols)
        {
            if (plan.Nodes.ContainsKey(n.Id)) continue;
            double hc, bc;
            try { hc = DesignUtil.ColumnDimAlong(p, col, Vec3.UnitX); bc = DesignUtil.ColumnDimAlong(p, col, Vec3.UnitY); }
            catch (Exception) { hc = bc = 0.22; }
            var e = new EccNode { NodeId = n.Id, Node = n, Column = col, X = n.X, Y = n.Y, Hc = hc, Bc = bc };
            try { e.ColHeight = p.Length(col); } catch (Exception) { e.ColHeight = 3.6; }
            e.Loads = FoundationUtil.Loads(r, n.Id, combos);
            e.Ovr = Find(fs, n.Id);
            if (e.Ovr != null && e.Ovr.Scheme == EccentricScheme.None) { e.Scheme = e.Recommended = EccentricScheme.None; plan.Nodes[n.Id] = e; continue; }
            e.Limits = Limits(p, n.X, n.Y, hc, bc);
            if (e.Ovr != null && !e.Ovr.AutoOffset)
            {
                e.Manual = true; e.ManualOx = e.Ovr.OffsetX; e.ManualOy = e.Ovr.OffsetY;
                e.Limits = new EdgeLimits
                {
                    EdgeLeft = e.ManualOx > 1e-6, EdgeRight = e.ManualOx < -1e-6, EdgeFront = e.ManualOy > 1e-6, EdgeBack = e.ManualOy < -1e-6
                };
            }
            plan.Nodes[n.Id] = e;
        }
        var all = plan.Nodes.Values.Where(x => x.Scheme != EccentricScheme.None).ToList();
        var soil = p.Soil ?? new SoilSettings();
        double kt = Math.Max(soil.LoadFactorAvg, 1.0), R0 = Math.Max(FootingDesigner.BearingR(soil, 1.5) - soil.GammaAvg * soil.Depth, 20);
        double NMax(EccNode x) => x.Loads.Count > 0 ? Math.Max(x.Loads.Max(l => l.N), 0) : 0;
        // ước lượng cạnh vuông góc ranh giới của móng biên (L⊥ = √(A/1,5)) và độ lệch tâm
        double EstPerp(EccNode x) => Math.Max(RcSection.CeilTo(Math.Sqrt(NMax(x) / kt / R0 / EdgeAspect), 0.1), FootingDesigner.MinSize);
        foreach (var e in all)
        {
            double lp = EstPerp(e);
            var (ox, oy) = OffsetFor(e, e.Limits.EdgeX ? lp : Math.Max(lp * EdgeAspect, lp), e.Limits.EdgeY ? lp : Math.Max(lp * EdgeAspect, lp));
            e.EstOx = ox; e.EstOy = oy;
        }

        EccNode Partner(EccNode e, bool alongX, int forced)
        {
            int dir = alongX ? (e.Manual ? Math.Sign(e.ManualOx) : e.Limits.InwardX) : (e.Manual ? Math.Sign(e.ManualOy) : e.Limits.InwardY);
            if (forced > 0) return all.FirstOrDefault(x => x.NodeId == forced && x.NodeId != e.NodeId);
            if (dir == 0) return null;
            double maxL = Math.Max(fs.StrapMaxSpan, 0.5);
            return all.Where(x => x.NodeId != e.NodeId)
                .Where(x => alongX ? Math.Abs(x.Y - e.Y) <= LineTol : Math.Abs(x.X - e.X) <= LineTol)
                .Select(x => (x, d: alongX ? (x.X - e.X) * dir : (x.Y - e.Y) * dir))
                .Where(t => t.d > 0.3 && t.d <= maxL + 1e-9)
                .OrderBy(t => t.d).Select(t => t.x).FirstOrDefault();
        }

        foreach (var e in all.OrderBy(x => x.Y).ThenBy(x => x.X))
        {
            bool edgeX = e.Limits.EdgeX, edgeY = e.Limits.EdgeY;
            if (!edgeX && !edgeY)
            {
                if (e.Secondary) continue;   // đã gộp vào móng kết hợp của cột biên
                e.Scheme = e.Recommended = EccentricScheme.SelfBalanced;
                e.Reason = "Cột không sát ranh giới: móng chỉ dịch (nếu cần) để không vượt ranh giới";
                continue;
            }
            var px = edgeX ? Partner(e, true, e.Ovr?.PartnerX ?? 0) : null;
            var py = edgeY ? Partner(e, false, e.Ovr?.PartnerY ?? 0) : null;
            // đề xuất
            EccentricScheme rec;
            string why;
            if (px == null && py == null)
            {
                rec = EccentricScheme.SelfBalanced;
                why = $"Không có cột trong cùng trục trong phạm vi {fs.StrapMaxSpan:0.#} m để nối dầm cân bằng – móng lệch tâm tự cân bằng (mô men N·e0 do đáy móng và hệ giằng / cột tiếp nhận)";
            }
            else
            {
                rec = EccentricScheme.Strap;
                why = "Có cột trong cùng trục (" + string.Join("; ", new[] { px, py }.Where(q => q != null).Select(q => $"nút {q.NodeId}, cách {Math.Abs(px == q ? q.X - e.X : q.Y - e.Y):0.00} m")) +
                      ") – ưu tiên dầm móng cân bằng: áp lực đáy móng biên phân bố đều, mô men lệch tâm do dầm tiếp nhận";
                var one = (edgeX ^ edgeY) ? (px ?? py) : null;
                if (one != null && !one.Limits.EdgeX && !one.Limits.EdgeY && !one.Secondary && one.CombinedWith == 0)
                {
                    bool ax = one == px;
                    double span = Math.Abs(ax ? one.X - e.X : one.Y - e.Y);
                    double dist = Math.Abs(ax ? e.X - (e.Limits.InwardX > 0 ? e.Limits.XMin : e.Limits.XMax) : e.Y - (e.Limits.InwardY > 0 ? e.Limits.YMin : e.Limits.YMax));
                    if (e.Manual) dist = (ax ? (e.Hc) : (e.Bc)) / 2;
                    double l1 = EstPerp(e), a2 = Math.Sqrt(NMax(one) / kt / R0) / 2;
                    double gap = span - (l1 - dist) - a2;
                    if (gap < MinGap)
                    {
                        rec = EccentricScheme.Combined;
                        why = $"Cột trong (nút {one.NodeId}) cách {span:0.00} m – móng biên và móng cột trong gần chạm nhau (khe hở ước lượng {gap:0.00} m < {MinGap:0.00} m): móng kết hợp";
                    }
                }
            }
            e.Recommended = rec;
            var want = e.Ovr != null && e.Ovr.Scheme != EccentricScheme.Auto ? e.Ovr.Scheme : (fs.EccentricDefault == EccentricScheme.None ? EccentricScheme.Auto : fs.EccentricDefault);
            if (want == EccentricScheme.Auto) want = rec;
            if (want == EccentricScheme.Combined)
            {
                var one = (edgeX ^ edgeY) ? (px ?? py) : null;
                if (one == null || one.Limits.EdgeX || one.Limits.EdgeY || one.Secondary || one.CombinedWith != 0)
                {
                    plan.Notes.Add($"Nút {e.NodeId}: không lập được móng kết hợp (cần đúng 1 phương sát ranh và 1 cột trong chưa gộp) – dùng " + ((px ?? py) == null ? "móng tự cân bằng" : "dầm cân bằng"));
                    want = px != null || py != null ? EccentricScheme.Strap : EccentricScheme.SelfBalanced;
                }
                else
                {
                    e.CombinedWith = one.NodeId;
                    one.CombinedWith = e.NodeId; one.Secondary = true;
                    one.Scheme = one.Recommended = EccentricScheme.Combined;
                    one.Reason = $"Thiết kế chung trong móng kết hợp với nút {e.NodeId}";
                    if (one == px) { e.PartnerX = one.NodeId; e.SpanX = Math.Abs(one.X - e.X); }
                    else { e.PartnerY = one.NodeId; e.SpanY = Math.Abs(one.Y - e.Y); }
                }
            }
            if (want == EccentricScheme.Strap && px == null && py == null) want = EccentricScheme.SelfBalanced;
            e.Scheme = want;
            e.Reason = why;
            if (want != EccentricScheme.Strap) continue;
            if (px != null)
            {
                e.StrapX = true; e.PartnerX = px.NodeId; e.SpanX = Math.Abs(px.X - e.X);
                if (px.Limits.EdgeX && Math.Sign(px.EstOx) == -Math.Sign(e.EstOx)) e.PartnerEX = Math.Abs(px.EstOx);
                else px.RelievedBy.Add((e.NodeId, true));
            }
            if (py != null)
            {
                e.StrapY = true; e.PartnerY = py.NodeId; e.SpanY = Math.Abs(py.Y - e.Y);
                if (py.Limits.EdgeY && Math.Sign(py.EstOy) == -Math.Sign(e.EstOy)) e.PartnerEY = Math.Abs(py.EstOy);
                else py.RelievedBy.Add((e.NodeId, false));
            }
        }
        // móng cột trong là móng phụ của móng kết hợp: không kể giảm tải
        foreach (var e in all.Where(x => x.Secondary)) e.RelievedBy.Clear();
        return plan;
    }

    /// <summary>Thứ tự thiết kế: móng biên có dầm cân bằng trước (để có ΔR giảm tải cho móng cột trong), móng phụ của móng kết hợp bỏ qua.</summary>
    public static int Order(EccPlan plan, int nodeId)
    {
        var n = plan?.Get(nodeId);
        if (n == null) return 1;
        if (n.StrapX || n.StrapY) return 0;
        return n.RelievedBy.Count > 0 ? 2 : 1;
    }

    // ==================================================================
    // TẢI QUY ĐỔI + ÁP LỰC MÓNG ĐƠN LỆCH TÂM
    // ==================================================================
    private static double PartnerN(EccPlan plan, int node, string combo)
    {
        var x = plan?.Get(node);
        if (x == null) return 0;
        foreach (var l in x.Loads) if (l.Combo == combo) return l.N;
        return 0;
    }

    /// <summary>Lực dọc quy đổi tại trọng tâm đáy móng: N + ΔRx + ΔRy (dầm cân bằng, đòn bẩy) − β·ΣΔR (móng cột trong được giảm tải).</summary>
    public static double EffectiveN(EccPlan plan, EccNode n, BaseLoad l, double ox, double oy, out double dRx, out double dRy)
    {
        dRx = dRy = 0;
        double N = l.N;
        if (n == null) return N;
        if (n.StrapX && n.SpanX > 0) dRx = LeverReaction(N, PartnerN(plan, n.PartnerX, l.Combo), n.SpanX, Math.Abs(ox), n.PartnerEX) - N;
        if (n.StrapY && n.SpanY > 0) dRy = LeverReaction(N, PartnerN(plan, n.PartnerY, l.Combo), n.SpanY, Math.Abs(oy), n.PartnerEY) - N;
        return N + dRx + dRy - plan.Relief(n, l.Combo);
    }

    /// <summary>Mô men đối với trọng tâm đáy móng (tính toán): theo L (X): |My| + |Qx|·arm + |N·ox| (không kể khi có dầm cân bằng theo X); theo B tương tự.</summary>
    private static (double mL, double mB) Moments(EccNode n, BaseLoad l, double ox, double oy, double arm)
    {
        double mL = n.StrapX ? 0 : Math.Abs(l.My) + Math.Abs(l.Qx) * arm + Math.Abs(l.N * ox);
        double mB = n.StrapY ? 0 : Math.Abs(l.Mx) + Math.Abs(l.Qy) * arm + Math.Abs(l.N * oy);
        return (mL, mB);
    }

    /// <summary>Áp lực tiêu chuẩn bất lợi nhất của móng đơn lệch tâm L × B qua các tổ hợp (nội lực tính toán / kt).</summary>
    public static PressureResult Evaluate(EccPlan plan, EccNode n, IList<BaseLoad> loads, double L, double B, double kt, double gtb, double Df,
        out double ox, out double oy)
    {
        (ox, oy) = OffsetFor(n, L, B);
        var w = new PressureResult { Ptb = double.MinValue, Pmax = double.MinValue, Pmin = double.MaxValue, Contact = 1, Combo = "" };
        kt = Math.Max(kt, 1.0);
        foreach (var l in loads)
        {
            double Ne = EffectiveN(plan, n, l, ox, oy, out _, out _);
            var (mL, mB) = Moments(n, l, ox, oy, Df);
            var pr = Pressure(Ne / kt + gtb * Df * L * B, mL / kt, mB / kt, L, B);
            if (pr.Ptb > w.Ptb) w.Ptb = pr.Ptb;
            if (pr.Pmax > w.Pmax) { w.Pmax = pr.Pmax; w.Combo = l.Combo; w.EL = pr.EL; w.EB = pr.EB; w.Partial = pr.Partial; }
            w.Pmin = Math.Min(w.Pmin, pr.Pmin);
            w.Contact = Math.Min(w.Contact, pr.Contact);
        }
        if (loads.Count == 0) w = new PressureResult { Contact = 1 };
        return w;
    }

    /// <summary>Điều kiện kích thước móng lệch tâm: ptb ≤ R, pmax ≤ 1,2R, (pmin ≥ 0 hoặc tiếp xúc ≥ 75%).</summary>
    public static bool PressureOk(PressureResult pr, double R) =>
        pr.Ptb <= R + 1e-6 && pr.Pmax <= 1.2 * R + 1e-6 && (pr.Pmin >= -1e-6 || pr.Contact >= 0.75 - 1e-9);

    /// <summary>Áp lực ròng tính toán tại toạ độ s (từ trọng tâm, theo phương len) do lực N và mô men M (đối với trọng tâm): tuyến tính hoặc tam giác (tách nền).</summary>
    private static double NetPressure(double N, double M, double len, double wid, double s)
    {
        if (N <= 1e-9 || len <= 1e-9 || wid <= 1e-9) return 0;
        double e = M / N;
        if (Math.Abs(e) <= len / 6 + 1e-12) return N / (len * wid) + 12 * M * s / (wid * len * len * len);
        double c0 = Math.Max(len / 2 - Math.Abs(e), 0.01);
        double pm = 2 * N / (3 * wid * c0);
        double t = len / 2 - Math.Sign(e) * s;   // khoảng cách tới mép chịu nén lớn nhất
        return Math.Max(0, pm * (1 - t / (3 * c0)));
    }

    /// <summary>Mô men công xôn bản móng tại mép cột (toàn bề rộng) theo một phương: cột tại xc (so với trọng tâm), cạnh cột a.</summary>
    private static double CantileverMoment(double N, double M, double len, double wid, double xc, double a)
    {
        double xr = xc + a / 2, xl = xc - a / 2, best = 0;
        const int steps = 60;
        if (len / 2 - xr > 1e-6)
        {
            double h = (len / 2 - xr) / steps, m = 0;
            for (int i = 0; i < steps; i++) { double s = xr + (i + 0.5) * h; m += NetPressure(N, M, len, wid, s) * (s - xr) * h; }
            best = Math.Max(best, m * wid);
        }
        if (xl + len / 2 > 1e-6)
        {
            double h = (xl + len / 2) / steps, m = 0;
            for (int i = 0; i < steps; i++) { double s = -len / 2 + (i + 0.5) * h; m += NetPressure(N, M, len, wid, s) * (xl - s) * h; }
            best = Math.Max(best, m * wid);
        }
        return best;
    }

    /// <summary>Mô men tính thép đáy móng lệch tâm tại mép cột theo phương L (X) và B (Y): áp lực ròng tính toán (không kể G) có hợp lực tại vị trí lực dọc
    /// (cột cách trọng tâm −ox) ± mô men chân cột (xét cả hai chiều); có dầm cân bằng: áp lực đều theo phương dầm.</summary>
    public static (double mL, double mB) Bending(EccPlan plan, EccNode n, IList<BaseLoad> loads, double L, double B, double hc, double bc, double ox, double oy, double H)
    {
        double mLmax = 0, mBmax = 0;
        foreach (var l in loads)
        {
            if (l.N <= 0) continue;
            double Ne = EffectiveN(plan, n, l, ox, oy, out _, out _);
            if (Ne <= 0) continue;
            foreach (int sg in new[] { 1, -1 })
            {
                double ML = n.StrapX ? 0 : l.N * (-ox) + sg * (Math.Abs(l.My) + Math.Abs(l.Qx) * H);
                double MB = n.StrapY ? 0 : l.N * (-oy) + sg * (Math.Abs(l.Mx) + Math.Abs(l.Qy) * H);
                mLmax = Math.Max(mLmax, CantileverMoment(Ne, ML, L, B, -ox, hc));
                mBmax = Math.Max(mBmax, CantileverMoment(Ne, MB, B, L, -oy, bc));
            }
        }
        return (mLmax, mBmax);
    }

    /// <summary>Độ vươn bản móng lớn nhất ngoài mép cột theo phương L (alongL) hoặc B của móng lệch tâm.</summary>
    public static double Cantilever(double len, double colDim, double off) => Math.Max(len / 2 + Math.Abs(off) - colDim / 2, 0);

    /// <summary>Chọc thủng móng lệch tâm (TCVN 5574:2018 mục 8.1.6.2): đáy tháp 45° và chu vi u (cách mép cột h0/2) bị cắt bởi mép móng
    /// (cột sát mép: chu vi 3 phía, cột góc: 2 phía); F = N − p·A_tháp, Fb,u = Rbt·u·h0.</summary>
    public static void Punching(double N, double L, double B, double hc, double bc, double ox, double oy, double h0, double rbt, out double F, out double Fu, out double u)
    {
        double cx = -ox, cy = -oy;
        double x1 = Math.Max(cx - hc / 2 - h0, -L / 2), x2 = Math.Min(cx + hc / 2 + h0, L / 2);
        double y1 = Math.Max(cy - bc / 2 - h0, -B / 2), y2 = Math.Min(cy + bc / 2 + h0, B / 2);
        double ap = Math.Max(x2 - x1, 0) * Math.Max(y2 - y1, 0);
        F = N > 0 ? Math.Max(N - N / (L * B) * ap, 0) : 0;
        double ux1 = cx - hc / 2 - h0 / 2, ux2 = cx + hc / 2 + h0 / 2, uy1 = cy - bc / 2 - h0 / 2, uy2 = cy + bc / 2 + h0 / 2;
        double wy = Math.Max(Math.Min(uy2, B / 2) - Math.Max(uy1, -B / 2), 0), wx = Math.Max(Math.Min(ux2, L / 2) - Math.Max(ux1, -L / 2), 0);
        u = 0;
        if (ux1 > -L / 2 + 1e-9) u += wy;
        if (ux2 < L / 2 - 1e-9) u += wy;
        if (uy1 > -B / 2 + 1e-9) u += wx;
        if (uy2 < B / 2 - 1e-9) u += wx;
        Fu = rbt * u * Math.Max(h0, 0);
    }

    /// <summary>Ghi ΔR theo tổ hợp của móng biên đã thiết kế (để giảm tải móng cột trong) và lập các liên kết dầm cân bằng.</summary>
    public static void Record(EccPlan plan, EccNode n, double ox, double oy, double L, double B, string footName, bool pileCap)
    {
        if (n == null) return;
        n.FinalOx = ox; n.FinalOy = oy; n.Designed = true;
        n.DeltaRX.Clear(); n.DeltaRY.Clear();
        foreach (var l in n.Loads)
        {
            EffectiveN(plan, n, l, ox, oy, out double dx, out double dy);
            n.DeltaRX[l.Combo ?? ""] = dx; n.DeltaRY[l.Combo ?? ""] = dy;
        }
    }

    /// <summary>Liên kết dầm cân bằng xuất phát từ nút n (theo X và/hoặc Y).</summary>
    public static List<StrapLink> Links(EccPlan plan, EccNode n, double L, double B, string footName, bool pileCap)
    {
        var res = new List<StrapLink>();
        if (n == null) return res;
        void Add(bool ax)
        {
            var q = plan.Get(ax ? n.PartnerX : n.PartnerY);
            if (q == null) return;
            double e1 = Math.Abs(ax ? n.FinalOx : n.FinalOy), e2 = ax ? n.PartnerEX : n.PartnerEY;
            double span = ax ? n.SpanX : n.SpanY;
            res.Add(new StrapLink
            {
                NodeA = n.NodeId, NodeB = q.NodeId, AlongX = ax, XA = n.X, YA = n.Y, XB = q.X, YB = q.Y, Span = span, E1 = e1, E2 = e2,
                ColAlong = ax ? n.Hc : n.Bc, ColAcross = ax ? n.Bc : n.Hc, ColHeight = n.ColHeight,
                FaceA = (ax ? L : B) / 2 + e1, FaceB = span - Math.Max((ax ? q.Hc : q.Bc) / 2 + 0.3, e2 > 0 ? 0 : 0.5),
                LoadsA = n.Loads.ToList(), LoadsB = q.Loads.ToList(), PileCap = pileCap, FootingA = footName,
                UserB = n.Ovr?.StrapB ?? 0, UserH = n.Ovr?.StrapH ?? 0
            });
        }
        if (n.StrapX) Add(true);
        if (n.StrapY) Add(false);
        return res;
    }

    /// <summary>Lập EccentricInfo cho móng đơn / đài sau khi thiết kế.</summary>
    public static EccentricInfo Info(EccPlan plan, EccNode n, double ox, double oy, PressureResult pr, double L, double B)
    {
        var info = new EccentricInfo
        {
            Scheme = n.Scheme, Recommended = n.Recommended, Recommendation = n.Reason, Sides = n.Manual ? "nhập tay" : n.Limits.Sides(),
            Boundary = !n.Manual, OffsetX = ox, OffsetY = oy, PartnerX = n.PartnerX, PartnerY = n.PartnerY, SpanX = n.SpanX, SpanY = n.SpanY,
            Ptb = pr.Ptb, Pmax = pr.Pmax, Pmin = pr.Pmin, Contact = pr.Contact, ResultantE = Math.Sqrt(pr.EL * pr.EL + pr.EB * pr.EB),
            ReliefFrom = n.RelievedBy.Select(x => x.From).Distinct().ToList()
        };
        if (n.Loads.Count > 0)
        {
            var g = n.Loads.OrderByDescending(l => l.N).First();
            info.N1 = g.N;
            info.R1 = EffectiveN(plan, n, g, ox, oy, out double dx, out double dy) + plan.Relief(n, g.Combo);
            info.DeltaR = dx + dy;
            info.Relief = plan.Relief(n, g.Combo);
            if (n.Scheme == EccentricScheme.SelfBalanced)
            {
                info.ColumnMomentX = Math.Abs(g.N * ox);
                info.ColumnMomentY = Math.Abs(g.N * oy);
            }
        }
        info.Locations.Add(new EccentricLocation { NodeId = n.NodeId, X = n.X, Y = n.Y, OffsetX = ox, OffsetY = oy });
        return info;
    }

    /// <summary>So sánh nhanh 3 phương án cho móng biên (dùng trong hộp thoại và thuyết minh): áp lực pmax/R và ghi chú.</summary>
    public static List<string> CompareOptions(EccPlan plan, EccNode n, double L, double B, double kt, double gtb, double Df, double R)
    {
        var res = new List<string>();
        if (n == null || n.Loads.Count == 0) return res;
        var saveX = n.StrapX; var saveY = n.StrapY;
        try
        {
            n.StrapX = false; n.StrapY = false;
            var pb = Evaluate(plan, n, n.Loads, L, B, kt, gtb, Df, out double ox, out double oy);
            res.Add($"(b) Tự cân bằng {L:0.00}×{B:0.00} m: e0 = {Math.Sqrt(ox * ox + oy * oy) * 1000:0} mm, pmax = {Fmt(pb.Pmax)} kN/m² ({Fmt(pb.Pmax / Math.Max(1.2 * R, 1e-6), "0.00")}·1,2R), "
                    + $"tiếp xúc {pb.Contact * 100:0}% → " + (PressureOk(pb, R) ? "đạt" : "KHÔNG ĐẠT (cần dầm cân bằng / móng kết hợp)"));
            if (n.PartnerX != 0 || n.PartnerY != 0)
            {
                n.StrapX = n.PartnerX != 0 && n.Limits.EdgeX; n.StrapY = n.PartnerY != 0 && n.Limits.EdgeY;
                var pa = Evaluate(plan, n, n.Loads, L, B, kt, gtb, Df, out _, out _);
                res.Add($"(a) Dầm cân bằng: áp lực gần đều ptb = {Fmt(pa.Ptb)}, pmax = {Fmt(pa.Pmax)} kN/m² ({Fmt(pa.Pmax / Math.Max(1.2 * R, 1e-6), "0.00")}·1,2R) → " + (PressureOk(pa, R) ? "đạt" : "cần tăng kích thước"));
                res.Add(n.CombinedWith != 0 || n.Recommended == EccentricScheme.Combined
                    ? "(c) Móng kết hợp: phù hợp (cột trong gần, hai móng riêng gần chạm nhau)"
                    : "(c) Móng kết hợp: không cần (khoảng cách tới cột trong đủ lớn cho hai móng riêng + dầm cân bằng)");
            }
            else res.Add("(a) / (c): không có cột trong cùng trục trong phạm vi nối dầm cân bằng");
        }
        finally { n.StrapX = saveX; n.StrapY = saveY; }
        return res;
    }

    private static string Fmt(double v, string f = "0") => v >= 1e8 ? "∞" : v.ToString(f, DesignUtil.Inv);

    /// <summary>Chuỗi thêm vào chữ ký gộp móng giống hệt (móng lệch tâm khác độ lệch tâm không gộp).</summary>
    public static string Sig(EccentricInfo e) =>
        e == null ? "" : "|E" + Math.Round(e.OffsetX * 1000) + "," + Math.Round(e.OffsetY * 1000) + "," + (int)e.Scheme + (e.Combined != null ? ",K" + e.Combined.NodeB : "");

    /// <summary>Gộp thông tin vị trí / dầm cân bằng của móng giống hệt vào móng giữ lại.</summary>
    public static void Merge(EccentricInfo keep, EccentricInfo other)
    {
        if (keep == null || other == null) return;
        keep.Locations.AddRange(other.Locations);
        keep.Straps.AddRange(other.Straps);
        keep.Pmax = Math.Max(keep.Pmax, other.Pmax);
        keep.Contact = Math.Min(keep.Contact, other.Contact);
    }

    // ==================================================================
    // ĐÀI CỌC LỆCH TÂM
    // ==================================================================
    /// <summary>Tải chân cột quy đổi cho đài có dầm cân bằng: N' = N + ΔR (đòn bẩy), mô men trong mặt phẳng dầm chọn sao cho hợp lực đi qua trọng tâm nhóm cọc
    /// (My' = −H·Qx − (N' + G)·x̄; Mx' = H·Qy + (N' + G)·ȳ – PileCapDesigner.PileForces cho phản lực đều theo phương dầm); không có dầm: giữ nguyên
    /// (nhóm cọc lệch tâm x̄, ȳ tự sinh mô men (N + G)·e).</summary>
    public static List<BaseLoad> CapLoads(EccPlan plan, EccNode n, List<BaseLoad> loads, double ox, double oy, double G, double H)
    {
        if (n == null || (!n.StrapX && !n.StrapY && n.RelievedBy.Count == 0)) return loads;
        var res = new List<BaseLoad>(loads.Count);
        foreach (var l in loads)
        {
            var q = l;
            double Ne = EffectiveN(plan, n, l, ox, oy, out _, out _);
            q.N = Ne;
            if (n.StrapX) q.My = -H * l.Qx - (Ne + G) * ox;
            if (n.StrapY) q.Mx = H * l.Qy + (Ne + G) * oy;
            res.Add(q);
        }
        return res;
    }

    // ==================================================================
    // MÓNG BĂNG LỆCH TÂM
    // ==================================================================
    /// <summary>Ranh giới song song phương dải tại trục cLine: trả về hướng bản cánh (+1 / −1, 0 = không sát ranh) và toạ độ giới hạn mép ngoài bản đáy.</summary>
    public static (int Side, double Limit, string Name) StripSide(Project p, bool alongX, double cLine, double colWidth)
    {
        var fs = p?.Foundation ?? new FoundationSettings();
        double tol = Math.Max(fs.BoundaryTolerance, 0), cl = Math.Max(fs.BoundaryClearance, 0);
        foreach (var (b, c) in Lines(p))
        {
            if (b.IsVertical == alongX) continue;   // dải dọc X ↔ ranh giới trước / sau (song song X)
            double face = cLine - b.Inward * colWidth / 2;
            double d = (face - c) * b.Inward;
            if (d >= -tol - colWidth && d <= tol + 1e-9) return (b.Inward, c + b.Inward * cl, PropertyBoundary.SideName(b.Side));
        }
        return (0, double.NaN, "");
    }

    // ==================================================================
    // DẦM MÓNG CÂN BẰNG
    // ==================================================================
    /// <summary>Thiết kế các dầm móng cân bằng từ móng đơn / đài cọc lệch tâm đã thiết kế (EccentricInfo.Straps). Dầm giống hệt được gộp (Quantity).</summary>
    public static List<StrapBeamDesign> DesignStraps(Project p, DesignSummary s, List<string> warnings)
    {
        var res = new List<StrapBeamDesign>();
        if (p == null || s == null) return res;
        var links = new List<StrapLink>();
        foreach (var f in s.Footings) if (f.Eccentric != null) links.AddRange(f.Eccentric.Straps);
        foreach (var c in s.PileCaps) if (c.Eccentric != null) links.AddRange(c.Eccentric.Straps);
        // dầm nối hai móng biên đối diện: chỉ thiết kế một lần
        var seen = new HashSet<string>();
        var uniq = new List<StrapLink>();
        foreach (var l in links)
        {
            string k = Math.Min(l.NodeA, l.NodeB) + "-" + Math.Max(l.NodeA, l.NodeB) + (l.AlongX ? "X" : "Y");
            if (!seen.Add(k)) { var o = uniq.First(u => (Math.Min(u.NodeA, u.NodeB) + "-" + Math.Max(u.NodeA, u.NodeB) + (u.AlongX ? "X" : "Y")) == k); o.E2 = Math.Max(o.E2, l.E1); continue; }
            uniq.Add(l);
        }
        var all = new List<(StrapBeamDesign d, string sig)>();
        foreach (var l in uniq)
        {
            try
            {
                // mép móng A, B dọc dầm theo kích thước thật của móng đã thiết kế
                var fa = Footprint(s, l.NodeA, l.AlongX); var fb = Footprint(s, l.NodeB, l.AlongX);
                double dir = l.AlongX ? Math.Sign(l.XB - l.XA) : Math.Sign(l.YB - l.YA);
                if (fa.Len > 0) l.FaceA = fa.Off * dir + fa.Len / 2;
                if (fb.Len > 0) l.FaceB = l.Span + fb.Off * dir - fb.Len / 2;
                if (l.FaceB <= l.FaceA + 0.1) l.FaceB = l.FaceA + 0.1;
                var d = DesignStrap(p, l);
                all.Add((d, StrapSig(d)));
            }
            catch (Exception ex) { warnings?.Add("LỖI thiết kế dầm móng cân bằng nút " + l.NodeA + "–" + l.NodeB + ": " + ex.Message); }
        }
        foreach (var g in all.GroupBy(x => x.sig))
        {
            var items = g.Select(x => x.d).ToList();
            var keep = items.OrderByDescending(x => x.M).First();
            keep.Quantity = items.Count;
            keep.Links = new[] { keep }.Concat(items.Where(x => !ReferenceEquals(x, keep))).SelectMany(x => x.Links).ToList();   // liên kết của dầm giữ lại đứng đầu
            keep.RebarKeys = items.SelectMany(x => x.RebarKeys).Distinct().ToList();
            res.Add(keep);
        }
        int no = 1;
        foreach (var d in res.OrderBy(x => x.Links[0].YA).ThenBy(x => x.Links[0].XA)) d.Name = "DM" + no++;
        return res.OrderBy(x => x.Name.Length).ThenBy(x => x.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>Kích thước móng (đơn / đài) chứa nút theo phương dầm và độ lệch tâm tâm móng – tim cột theo phương đó (Len = 0 nếu không tìm thấy).</summary>
    private static (double Len, double Off) Footprint(DesignSummary s, int node, bool alongX)
    {
        string key = FootingDesigner.MemberKey(node);
        foreach (var f in s.Footings)
        {
            if (f.NodeId != node && !f.RebarKeys.Contains(key) && !(f.Eccentric?.Locations.Any(q => q.NodeId == node) ?? false)) continue;
            if (f.Eccentric?.Combined != null)
            {
                var c = f.Eccentric.Combined;
                if (c.AlongX == alongX)
                {
                    double pos = alongX ? (node == c.NodeA ? f.X : c.XB) : (node == c.NodeA ? f.Y : c.YB);
                    double mid = c.Start + c.Dir * c.Length / 2;
                    return (c.Length, mid - pos);
                }
                return (Math.Max(c.B1, c.B2), 0);
            }
            var loc = f.Eccentric?.Locations.FirstOrDefault(q => q.NodeId == node);
            double off = loc != null ? (alongX ? loc.OffsetX : loc.OffsetY) : 0;
            return (alongX ? f.L : f.B, off);
        }
        foreach (var c in s.PileCaps)
        {
            if (!c.NodeIds.Contains(node)) continue;
            return (alongX ? c.L : c.B, alongX ? c.OffsetX : c.OffsetY);
        }
        return (0, 0);
    }

    private static string StrapSig(StrapBeamDesign d) =>
        Math.Round(d.B * 1000) + "x" + Math.Round(d.H * 1000) + "|" + Math.Round(d.Span * 100) + "|" + Math.Round(d.ClearLength * 100) + "|" + d.TopBars + "|" + d.BottomBars + "|" + d.Stirrups
        + "|" + RebarNumbering.Signature(d.Bars);

    /// <summary>Thiết kế một dầm cân bằng: M = N1·e1 + |M0| (căng trên, tại cột biên), Q = |R1 − N1|; b ≥ cạnh cột, h ≥ L/7 và đủ cứng I/L ≥ 2·Ic/Hc.</summary>
    public static StrapBeamDesign DesignStrap(Project p, StrapLink l)
    {
        var cs = p.Concrete;
        var conc = DesignUtil.ConcreteOf(p, cs.ConcreteFooting, cs.ConcreteFooting);
        var main = p.Material(cs.RebarMain);
        var stir = p.Material(cs.RebarStirrup);
        double cover = Math.Max(cs.CoverBeam, 0.025) + 0.01;
        var d = new StrapBeamDesign { AlongX = l.AlongX, Span = l.Span, Story = "Móng", E1 = l.E1, E2 = l.E2, PileCap = l.PileCap };
        d.Links.Add(l);
        d.RebarKeys.Add("DM:" + l.NodeA.ToString(DesignUtil.Inv) + "-" + l.NodeB.ToString(DesignUtil.Inv));
        // nội lực
        double M = 0, V = 0, N1g = 0, R1g = 0; string combo = "";
        foreach (var a in l.LoadsA)
        {
            double n2 = 0, m2 = 0;
            foreach (var b in l.LoadsB) if (b.Combo == a.Combo) { n2 = b.N; m2 = l.AlongX ? Math.Abs(b.My) : Math.Abs(b.Mx); break; }
            double r1 = LeverReaction(a.N, n2, l.Span, l.E1, l.E2);
            double m0 = l.AlongX ? Math.Abs(a.My) + Math.Abs(a.Qx) * 0.5 : Math.Abs(a.Mx) + Math.Abs(a.Qy) * 0.5;
            double ma = StrapMoment(a.N, l.E1) + m0;
            double mb = l.E2 > 0 ? StrapMoment(n2, l.E2) + m2 : 0;
            double mm = Math.Max(ma, mb), vv = Math.Abs(r1 - a.N);
            if (mm > M) { M = mm; N1g = a.N; R1g = r1; combo = a.Combo; }
            V = Math.Max(V, vv);
        }
        d.M = M; d.V = V; d.N1 = N1g; d.R1 = R1g; d.Combo = combo;
        d.ClearLength = Math.Max(l.FaceB - l.FaceA, 0.3);
        // tiết diện
        double b0 = l.UserB > 0 ? l.UserB : Math.Max(0.25, RcSection.CeilTo(l.ColAcross, 0.05));
        double h = l.UserH > 0 ? l.UserH : Math.Max(0.40, RcSection.CeilTo(l.Span / 7, 0.05));
        double Ic = l.ColAcross * Math.Pow(Math.Max(l.ColAlong, 0.1), 3) / 12, Hc = Math.Max(l.ColHeight, 2.5);
        FlexureResult fl = default; ShearResult sh = default;
        int[] sdias = DesignUtil.Sorted(cs.StirrupBars, new[] { 6, 8, 10 });
        for (int it = 0; it < 60; it++)
        {
            double h0 = h - cover - 0.01;
            fl = RcSection.DesignRect(M, b0, h0, cover, conc, main, cs.MinRatioBeam);
            sh = default;
            foreach (int sd in sdias.Where(x => x >= 8).DefaultIfEmpty(sdias[^1]))
            {
                sh = RcSection.Shear(V, b0, h0, conc, stir, sd, 2);
                if (sh.Ok) break;
            }
            double kb = b0 * h * h * h / 12 / l.Span, kc = Ic / Hc;
            bool stiff = kb >= 2 * kc - 1e-12;
            if ((fl.Ok && !fl.Doubly && sh.Ok && stiff) || l.UserH > 0 || h >= 1.5 - 1e-9) break;
            h = Math.Round(h + 0.05, 3);
        }
        d.B = b0; d.H = h; d.H0 = h - cover - 0.01;
        d.StiffBeam = b0 * h * h * h / 12 / l.Span; d.StiffColumn = 2 * Ic / Hc;
        int[] bdias = DesignUtil.Sorted(cs.BeamBars, new[] { 14, 16, 18, 20, 22, 25 });
        var top = RcSection.ChooseBars(fl.As, b0, cover, bdias, 2, 2);
        double asBotReq = Math.Max(0.3 * fl.As, fl.AsMin);
        var bot = RcSection.ChooseBars(asBotReq, b0, cover, bdias.Where(x => x >= 14).DefaultIfEmpty(bdias[0]).ToArray(), 2, 1);
        d.AsTop = fl.As; d.AsBottom = asBotReq;
        d.TopBars = top.Text; d.BottomBars = bot.Text;
        d.StirrupDia = sh.Dia; d.StirrupSpacing = sh.SSupport;
        d.Stirrups = "Ø" + sh.Dia + "a" + DesignUtil.F(sh.SSupport * 1000, "0");
        double asTop = top.Area, asBot = bot.Area;
        d.Checks.Add(new CheckItem
        {
            Name = "Uốn dầm cân bằng – mô men âm M = N1·e1 + M0", Demand = M, Unit = "kN.m", Note = d.TopBars + ", " + combo,
            Capacity = M > 1e-9 ? RcSection.MomentCapacity(b0, d.H0, asTop, 0, cover, conc, main) : 1
        });
        d.Checks.Add(new CheckItem
        {
            Name = "Cắt dầm cân bằng Q = R1 − N1 ≤ Qu", Demand = V, Unit = "kN", Note = d.Stirrups + (string.IsNullOrEmpty(sh.Note) ? "" : "; " + sh.Note),
            Capacity = sh.Capacity
        });
        d.Checks.Add(new CheckItem
        {
            Name = "Độ cứng dầm cân bằng I/L ≥ 2·Ic/Hc", Demand = d.StiffColumn, Capacity = d.StiffBeam, Unit = "m³",
            Note = $"I/L = {d.StiffBeam:0.00000}; 2Ic/Hc = {d.StiffColumn:0.00000} (dầm phải cứng hơn cột để coi là tuyệt đối cứng)"
        });
        if (fl.Doubly) d.Messages.Add("LỖI dầm cân bằng cần cốt kép – tăng chiều cao dầm (" + fl.Note + ")");
        if (!top.Ok || !bot.Ok) d.Messages.Add("LỖI không bố trí đủ thép dầm cân bằng trong bề rộng b – tăng tiết diện");
        d.Messages.Add($"Dầm cân bằng nối {(l.PileCap ? "đài" : "móng")} biên (nút {l.NodeA}) với {(l.E2 > 0 ? "móng biên đối diện" : "móng cột trong")} (nút {l.NodeB}), nhịp {l.Span:0.00} m: "
                       + $"e1 = {l.E1 * 1000:0} mm{(l.E2 > 0 ? $", e2 = {l.E2 * 1000:0} mm" : "")}, R1 = {R1g:0.0} kN, N1 = {N1g:0.0} kN");
        d.Messages.Add("Đáy dầm cân bằng không tựa lên nền (lót xốp / cát rời 50–100 mm) để dầm làm việc như đòn bẩy; thép trên chạy suốt neo vào móng 2 đầu.");
        // thép
        double la = 0.6, L = l.Span;
        foreach (var g in top.Groups)
            d.Bars.Add(new BarMark
            {
                Role = "Trên", Dia = g.Dia, Count = g.Count, Shape = "U", IsTop = true, Start = -0.2, End = L + 0.2,
                Length = DesignUtil.R10(L + 0.4 + 2 * Math.Max(15 * g.Dia / 1000.0, 0.2)),
                Segments = new List<double> { DesignUtil.Mm(Math.Max(15 * g.Dia / 1000.0, 0.2)), DesignUtil.Mm(L + 0.4), DesignUtil.Mm(Math.Max(15 * g.Dia / 1000.0, 0.2)) }
            });
        foreach (var g in bot.Groups)
            d.Bars.Add(new BarMark
            {
                Role = "Dưới", Dia = g.Dia, Count = g.Count, Shape = "Thẳng", Start = l.FaceA - la, End = l.FaceB + la,
                Length = DesignUtil.R10(d.ClearLength + 2 * la), Segments = new List<double> { DesignUtil.Mm(d.ClearLength + 2 * la) }
            });
        if (h >= 0.70 - 1e-9)
            d.Bars.Add(new BarMark { Role = "Giá", Dia = 12, Count = 2 * (int)Math.Max(1, Math.Floor((h - 0.3) / 0.4)), Shape = "Thẳng", Length = DesignUtil.R10(d.ClearLength + 0.4), Start = l.FaceA - 0.2, End = l.FaceB + 0.2, Segments = new List<double> { DesignUtil.Mm(d.ClearLength + 0.4) } });
        double s1 = b0 - 2 * (cover - 0.01), s2 = h - 2 * (cover - 0.01);
        d.Bars.Add(new BarMark
        {
            Role = "Đai", Dia = sh.Dia, Spacing = sh.SSupport, Shape = "Đai", Count = (int)Math.Ceiling(d.ClearLength / Math.Max(sh.SSupport, 0.05) - 1e-9) + 1,
            Length = DesignUtil.StirrupLength(s1, s2, sh.Dia), Segments = new List<double> { DesignUtil.Mm(s1), DesignUtil.Mm(s2) }, Start = l.FaceA, End = l.FaceB
        });
        RebarNumbering.Renumber(d.Bars);
        return d;
    }
}
