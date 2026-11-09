using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

/// <summary>Thiết kế đài cọc BTCT dưới chân cột (móng cọc):
/// <list type="bullet">
/// <item>Số cọc n ≥ 1,2·Nmax/[P], tăng dần theo các sơ đồ chuẩn 1, 2, 3, 4, 5, 6, 8, 9 (lưới m×k khi &gt; 9) tới khi Pmax ≤ [P], Pmin ≥ −[Pk].</item>
/// <item>Phản lực cọc Pi = (N + G)/n + My'·xi/Σx² + Mx'·yi/Σy² (đài tuyệt đối cứng), mô men kể cả lực cắt × chiều cao đài.</item>
/// <item>Chiều cao đài theo chọc thủng của cột (tháp 45°) và của cọc góc, cắt trên tiết diện nghiêng tại mép cột (TCVN 5574:2018 mục 8.1.3, 8.1.6 – đơn giản hoá).</item>
/// <item>Thép đáy theo mô men tại mép cột M = Σ Pi·(khoảng cách tim cọc – mép cột).</item>
/// </list></summary>
public static class PileCapDesigner
{
    /// <summary>Hệ số vượt tải trọng lượng đài + đất đắp</summary>
    public const double WeightFactor = 1.1;
    /// <summary>Chiều cao đài tối đa khi tăng dần (m)</summary>
    public const double MaxHeight = 3.0;

    private static readonly int[] Counts = { 1, 2, 3, 4, 5, 6, 8, 9, 12, 15, 16, 20, 25, 30, 36 };

    /// <summary>Thiết kế đài cọc cho mọi chân cột có loại móng hiệu lực là Pile. Đài giống hệt được gộp (Quantity, Locations).</summary>
    public static List<PileCapDesign> Design(Project p, AnalysisResults r, List<string> warnings)
    {
        warnings ??= new List<string>();
        var res = new List<PileCapDesign>();
        if (p == null || r == null) return res;
        var fs = p.Foundation ?? new FoundationSettings();
        var combos = FoundationUtil.Combos(p, r);
        var all = new List<(PileCapDesign d, string sig)>();
        // [MóngLệchTâm] đài lệch tâm do ranh giới đất: kế hoạch lệch tâm, đài biên có dầm cân bằng thiết kế trước (null = như cũ)
        var bcols = FoundationUtil.BaseColumns(p).Where(x => FoundationUtil.TypeOf(p, x.Node.Id) == FoundationType.Pile).ToList();
        var plan = EccentricFooting.Plan(p, r, combos, bcols);
        var rank = bcols.Select((x, i) => (x.Node.Id, i)).ToDictionary(t => t.Id, t => t.i);
        if (plan != null)
        {
            bcols = bcols.OrderBy(x => EccentricFooting.Order(plan, x.Node.Id)).ThenBy(x => rank[x.Node.Id]).ToList();
            foreach (var w in plan.Notes) warnings.Add("Cảnh báo: " + w);
        }
        // ---- hết [MóngLệchTâm] ----
        // [CặpĐàiBiên] hai đài biên đối diện nối chung một dầm cân bằng: độ lệch tâm e2 của đài đối diện lấy theo kết quả thiết kế thật
        // (lượt đầu dùng ước lượng) – lặp tới khi e2 hội tụ (≤ 5 mm), tối đa 4 lượt (EccentricPairs.Sync)
        for (int pass = 0; pass < 4; pass++)
        {
            all.Clear();
            var passWarn = new List<string>();
            foreach (var (n, col) in bcols)
            {
                if (FoundationUtil.TypeOf(p, n.Id) != FoundationType.Pile) continue;
                try
                {
                    var d = DesignOne(p, r, n, col, combos, fs, plan);
                    all.Add((d, Signature(d)));
                }
                catch (Exception ex)
                {
                    passWarn.Add("LỖI thiết kế đài cọc tại " + DesignUtil.GridName(p, n.X, n.Y) + ": " + ex.Message);
                }
            }
            if (pass == 3 || plan == null || !EccentricPairs.Sync(plan)) { warnings.AddRange(passWarn); break; }
        }
        if (plan != null) all = all.OrderBy(x => rank.TryGetValue(x.d.NodeIds[0], out int k) ? k : 0).ToList();   // [MóngLệchTâm] thứ tự đặt tên như cũ
        foreach (var g in all.GroupBy(x => x.sig))
        {
            var items = g.Select(x => x.d).ToList();
            var keep = items.OrderByDescending(x => x.PMax).First();
            keep.Quantity = items.Count;
            keep.RebarKeys = items.SelectMany(x => x.RebarKeys).Distinct().ToList();
            keep.NodeIds = items.SelectMany(x => x.NodeIds).Distinct().ToList();
            keep.Locations = items.Select(x => (x.X, x.Y)).ToList();
            keep.PMin = items.Min(x => x.PMin);
            foreach (var o in items)
            {
                if (ReferenceEquals(o, keep)) continue;
                for (int i = 0; i < keep.Checks.Count && i < o.Checks.Count; i++)
                    if (o.Checks[i].Name == keep.Checks[i].Name && o.Checks[i].Ratio > keep.Checks[i].Ratio) keep.Checks[i] = o.Checks[i];
                foreach (var m in o.Messages.Where(m => m.StartsWith("LỖI") && !keep.Messages.Contains(m))) keep.Messages.Add(m);
                EccentricFooting.Merge(keep.Eccentric, o.Eccentric);   // [MóngLệchTâm]
                keep.Messages.Add("Gộp đài giống hệt tại " + DesignUtil.GridName(p, o.X, o.Y));
            }
            res.Add(keep);
        }
        int no = 1;
        foreach (var d in res) d.Name = "ĐC" + no++;
        return res;
    }

    // ------------------------------------------------------------------
    /// <summary>Sơ đồ bố trí cọc chuẩn (toạ độ tim cọc so với tâm đài). longX: cạnh dài (phương chịu mô men lớn) theo X.</summary>
    public static List<(double X, double Y)> Layout(int n, double s, bool longX)
    {
        var pts = new List<(double X, double Y)>();
        void Grid(int m, int k)
        {
            for (int j = 0; j < k; j++)
                for (int i = 0; i < m; i++)
                    pts.Add(((i - (m - 1) / 2.0) * s, (j - (k - 1) / 2.0) * s));
        }
        switch (n)
        {
            case <= 1: pts.Add((0, 0)); break;
            case 2: Grid(2, 1); break;
            case 3:
            {
                double h = s * Math.Sqrt(3) / 2;
                pts.Add((-s / 2, -h / 3)); pts.Add((s / 2, -h / 3)); pts.Add((0, 2 * h / 3));
                break;
            }
            case 4: Grid(2, 2); break;
            case 5:
            {
                double a = RcSection.CeilTo(s / Math.Sqrt(2), 0.05);
                pts.Add((-a, -a)); pts.Add((a, -a)); pts.Add((0, 0)); pts.Add((-a, a)); pts.Add((a, a));
                break;
            }
            case 6: Grid(3, 2); break;
            case 8:
                Grid(3, 3);
                pts.RemoveAt(4);
                break;
            case 9: Grid(3, 3); break;
            default:
            {
                int k = (int)Math.Floor(Math.Sqrt(n) + 1e-9);
                int m = (int)Math.Ceiling(n / (double)k - 1e-9);
                Grid(m, k);
                break;
            }
        }
        if (!longX) pts = pts.Select(q => (q.Y, q.X)).ToList();
        // khử −0
        return pts.Select(q => (Math.Round(q.X, 6) + 0.0, Math.Round(q.Y, 6) + 0.0)).ToList();
    }

    /// <summary>Phản lực các cọc (kN, nén dương) của một tổ hợp: Pi = (N + G)/n + ax·(xi − x̄) + ay·(yi − ȳ),
    /// Σxi·Pi = −My − H·Qx; Σyi·Pi = Mx − H·Qy (mô men phản lực chân cột + lực cắt × chiều cao đài, đối với tim cột = gốc toạ độ;
    /// nhóm cọc lệch tâm x̄, ȳ ≠ 0 tự kể thêm mô men (N + G)·e).</summary>
    public static double[] PileForces(BaseLoad l, IList<(double X, double Y)> piles, double G, double H)
    {
        int np = piles.Count;
        var res = new double[np];
        if (np == 0) return res;
        double cx = piles.Average(q => q.X), cy = piles.Average(q => q.Y);
        double sx2 = piles.Sum(q => (q.X - cx) * (q.X - cx)), sy2 = piles.Sum(q => (q.Y - cy) * (q.Y - cy));
        double nt = l.N + G;
        // [FoundationChecks] mô men lấy đối với trọng tâm nhóm cọc (x̄, ȳ): kể thêm −(N + G)·x̄ khi nhóm cọc lệch tâm so với tim cột,
        // và tích quán tính Sxy = Σ(xi − x̄)(yi − ȳ) khi trục nhóm cọc không phải trục chính (nhóm đối xứng: x̄ = ȳ = Sxy = 0 → công thức cũ)
        double sxy = piles.Sum(q => (q.X - cx) * (q.Y - cy));
        double rx = -l.My - H * l.Qx - nt * cx, ry = l.Mx - H * l.Qy - nt * cy;
        double ax, ay;
        double det = sx2 * sy2 - sxy * sxy;
        if (sx2 > 1e-9 && sy2 > 1e-9 && det > 1e-12 * Math.Max(sx2 * sy2, 1e-12))
        {
            ax = (rx * sy2 - ry * sxy) / det;
            ay = (ry * sx2 - rx * sxy) / det;
        }
        else
        {
            ax = sx2 > 1e-9 ? rx / sx2 : 0;
            ay = sy2 > 1e-9 ? ry / sy2 : 0;
        }
        for (int i = 0; i < np; i++) res[i] = nt / np + ax * (piles[i].X - cx) + ay * (piles[i].Y - cy);
        return res;
    }

    private sealed class Eval
    {
        public double PMax = double.MinValue, PMin = double.MaxValue;
        public BaseLoad Gov;
        public double[] GovForces = Array.Empty<double>();
        public double FCol, FPile, QL, QB, ML, MB, MTopL, MTopB, CL = 1, CB = 1;
    }

    private static Eval Evaluate(List<BaseLoad> loads, List<(double X, double Y)> piles, double G, double H, double hc, double bc, double h0)
    {
        var e = new Eval();
        int np = piles.Count;
        double g1 = np > 0 ? G / np : 0;
        double axP = hc / 2 + h0, ayP = bc / 2 + h0;
        e.CL = double.MaxValue; e.CB = double.MaxValue;
        foreach (var l in loads)
        {
            var P = PileForces(l, piles, G, H);
            double mx = P.Length > 0 ? P.Max() : 0, mn = P.Length > 0 ? P.Min() : 0;
            if (mx > e.PMax) { e.PMax = mx; e.Gov = l; e.GovForces = P; }
            e.PMin = Math.Min(e.PMin, mn);
            double fcol = 0, qLp = 0, qLn = 0, qBp = 0, qBn = 0, mLp = 0, mLn = 0, mBp = 0, mBn = 0, tL = 0, tB = 0;
            for (int i = 0; i < np; i++)
            {
                double x = piles[i].X, y = piles[i].Y, pn = P[i] - g1;   // phản lực ròng (không kể trọng lượng đài)
                if (Math.Abs(x) > axP + 1e-9 || Math.Abs(y) > ayP + 1e-9) fcol += pn;
                if (np > 1) e.FPile = Math.Max(e.FPile, pn);
                double ox = Math.Abs(x) - hc / 2, oy = Math.Abs(y) - bc / 2;
                if (ox > 1e-6)
                {
                    if (x > 0) { qLp += pn; mLp += pn * ox; } else { qLn += pn; mLn += pn * ox; }
                    if (P[i] < 0) tL += -P[i] * ox;
                }
                if (oy > 1e-6)
                {
                    if (y > 0) { qBp += pn; mBp += pn * oy; } else { qBn += pn; mBn += pn * oy; }
                    if (P[i] < 0) tB += -P[i] * oy;
                }
            }
            e.FCol = Math.Max(e.FCol, fcol);
            e.QL = Math.Max(e.QL, Math.Max(qLp, qLn)); e.QB = Math.Max(e.QB, Math.Max(qBp, qBn));
            e.ML = Math.Max(e.ML, Math.Max(mLp, mLn)); e.MB = Math.Max(e.MB, Math.Max(mBp, mBn));
            e.MTopL = Math.Max(e.MTopL, tL); e.MTopB = Math.Max(e.MTopB, tB);
        }
        return e;
    }

    private static PileCapDesign DesignOne(Project p, AnalysisResults r, Node n, FrameElement col, List<string> combos, FoundationSettings fs, EccPlan plan = null)
    {
        var cs = p.Concrete; var soil = p.Soil;
        var conc = DesignUtil.ConcreteOf(p, cs.ConcreteFooting, cs.ConcreteFooting);
        var main = p.Material(cs.RebarMain);
        double hc = DesignUtil.ColumnDimAlong(p, col, Vec3.UnitX);
        double bc = DesignUtil.ColumnDimAlong(p, col, Vec3.UnitY);
        var loads = FoundationUtil.Loads(r, n.Id, combos);
        if (loads.Count == 0) throw new InvalidOperationException("không có phản lực tại nút " + n.Id);
        // [P] và chiều dài cọc theo SPT hố khoan gần nhất (GeotechCalc.PileCapacity) khi PileCapacityFromBorehole
        PileCapacityResult geo = null;
        if (fs.PileCapacityFromBorehole)
        {
            var bh = GeotechCalc.SelectBorehole(p, n.X, n.Y);
            if (bh != null) geo = GeotechCalc.PileCapacity(p, bh, fs);
        }
        if (geo == null && !(fs.PileCapacity > 0)) throw new InvalidOperationException("chưa khai báo sức chịu tải cọc [P] > 0");

        double d = Math.Max(fs.PileSize, 0.10);
        double P = geo != null ? Math.Floor(geo.Allowable) : fs.PileCapacity, Pt = Math.Max(fs.PileTension, 0);
        if (!(P > 0)) throw new InvalidOperationException("sức chịu tải cọc tính từ hố khoan [P] ≤ 0");
        double edge = Math.Max(fs.CapEdge, 0.05);
        // [FoundationChecks] quy định cấu tạo: mép cọc – mép đài ≥ max(150 mm; 0,7d − d/2) (PileLayout.CheckRules)
        double edgeRule = Math.Max(PileLayout.MinEdgeClear, PileLayout.MinEdgeCentre * d - d / 2);
        bool edgeRaised = edge < edgeRule - 1e-9;
        if (edgeRaised) edge = edgeRule;
        // cọc khoan nhồi D ≥ 0,8 m: mép cọc – mép đài ≥ max(250; D/3), tim cọc ≥ max(2,5D; D + 1 m) (BoredPileDesign)
        if (fs.PileBored && d >= 0.8 - 1e-9)
        {
            double eB = BoredPileDesign.MinEdgeClear(d);
            if (edge < eB - 1e-9) { edge = eB; edgeRaised = true; }
        }
        double s = RcSection.CeilTo(Math.Max(fs.PileSpacingFactor, 1.5) * d, 0.05);
        if (fs.PileBored) s = Math.Max(s, RcSection.CeilTo(BoredPileDesign.MinSpacing(d), 0.05));
        double embed = Math.Clamp(fs.PileEmbed, 0, 0.5);
        double Df = Math.Max(soil.Depth, 0);
        double gtb = soil.GammaAvg;
        double Hmin = Math.Max(fs.CapMinHeight, embed + 0.25);
        var given = p.Footings.FirstOrDefault(x => x.NodeId == n.Id);
        double Hgiven = given != null && given.Height > 0 ? Math.Max(given.Height, embed + 0.2) : 0;
        double rbt = conc.Rbt;

        var dsn = new PileCapDesign
        {
            NodeIds = new List<int> { n.Id }, X = n.X, Y = n.Y, ColumnB = bc, ColumnH = hc, Depth = Df, Story = "Móng",
            PileSize = d, PileRound = fs.PileRound, PileCapacity = P, PileLength = geo != null ? Math.Round(geo.Length, 2) : fs.PileLength, Spacing = s
        };
        dsn.Locations.Add((n.X, n.Y));
        string key = MemberKey(n.Id);
        dsn.RebarKeys.Add(key);
        if (geo != null)
            dsn.Messages.Add("[P] theo SPT hố khoan " + geo.Borehole + " (TCVN 10304:2014 G.3.2): mũi cọc " + DesignUtil.F(geo.TipDepth, "0.0#") + " m, L = "
                             + DesignUtil.F(geo.Length, "0.0#") + " m, Rc,u = " + DesignUtil.F(geo.Rcu, "0") + " kN, [P]đn = " + DesignUtil.F(geo.Pdn, "0")
                             + " kN, Pvl = " + DesignUtil.F(geo.Pvl, "0") + " kN → [P] = " + DesignUtil.F(P, "0") + " kN");
        else if (fs.PileCapacityFromBorehole) dsn.Messages.Add("Chưa có hố khoan – dùng [P] nhập tay = " + DesignUtil.F(P, "0") + " kN");
        double Nmax = loads.Max(l => l.N);
        if (Nmax <= 0) dsn.Messages.Add("Chân cột chịu kéo ở mọi tổ hợp – kiểm tra sức chịu nhổ của cọc");
        else if (loads.Any(l => l.N <= 0)) dsn.Messages.Add("Có tổ hợp chân cột chịu nhổ – kiểm tra sức chịu nhổ của cọc");

        double myMax = loads.Max(l => Math.Abs(l.My) + Math.Abs(l.Qx) * Hmin);
        double mxMax = loads.Max(l => Math.Abs(l.Mx) + Math.Abs(l.Qy) * Hmin);
        bool longX = myMax >= mxMax;
        // [LùiCọcRanh] cột sát một cạnh ranh giới + lùi ép cọc: hàng cọc đặt song song ranh giới (giảm độ lệch tâm nhóm cọc, mô men lệch tâm do dầm cân bằng tiếp nhận)
        var epRow = plan?.Get(n.Id);
        if (epRow != null && epRow.Scheme != EccentricScheme.None && !epRow.Manual && (epRow.Limits.EdgeX ^ epRow.Limits.EdgeY) && PileSetback.Active(p)) longX = epRow.Limits.EdgeY;

        int n0 = Math.Max(1, (int)Math.Ceiling(1.2 * Math.Max(Nmax, 0) / P - 1e-9));
        bool singleOk = d + 2 * edge >= Math.Max(hc, bc) + 0.10 - 1e-9;
        if (n0 == 1 && !singleOk) n0 = 2;

        List<(double X, double Y)> piles = null;
        double L = 0, B = 0, H = 0, h0 = 0, G = 0;
        Eval ev = null;
        bool found = false;
        var cands = Counts.Where(cc => cc >= n0).ToList();
        if (cands.Count == 0) cands.Add(Counts[^1]);
        // [FoundationChecks] bố trí cọc / kích thước đài do người dùng chọn (FoundationSettings.CapOverrides) – thay cho dãy sơ đồ tự chọn
        var ovr = PileLayout.Find(fs, n.Id);
        bool user = PileLayout.IsActive(ovr), userPat = user && ovr.Pattern != PilePattern.Auto;
        var layouts = new List<List<(double X, double Y)>>();
        if (userPat)
        {
            double sxU = ovr.SpacingX > 0 ? ovr.SpacingX : s, syU = ovr.SpacingY > 0 ? ovr.SpacingY : sxU;
            layouts.Add(PileLayout.Generate(ovr.Pattern, sxU, syU, ovr.Nx, ovr.Ny, ovr.Rotate.HasValue ? !ovr.Rotate.Value : longX, ovr.CustomPiles));
            if (layouts[0].Count == 0) layouts[0] = Layout(cands[0], s, longX);
            dsn.Spacing = layouts[0].Count > 1 ? PileLayout.MinSpacing(layouts[0]) : s;
        }
        else layouts.AddRange(cands.Select(np => Layout(np, s, longX)));
        double Hfix = user && ovr.H > 0 ? Math.Max(ovr.H, embed + 0.2) : Hgiven;
        // [MóngLệchTâm] đài lệch tâm: dịch đài + nhóm cọc vào trong ranh giới (cox, coy = tâm đài − tim cột); tải quy đổi khi có dầm cân bằng
        var ep = plan?.Get(n.Id);
        if (ep != null && ep.Scheme == EccentricScheme.None) ep = null;
        double cox = 0, coy = 0;
        PileSetbackInfo sbInfo = null;   // [LùiCọcRanh]
        bool sbOn = ep != null && PileSetback.Active(p);
        List<BaseLoad> EvLoads() => ep == null || piles == null || piles.Count == 0 ? loads
            : EccentricFooting.CapLoads(plan, ep, loads, piles.Average(q => q.X), piles.Average(q => q.Y), G, H);   // e = trọng tâm nhóm cọc so với tim cột
        foreach (var lay in layouts)
        {
            piles = lay;
            (L, B) = PileLayout.CapSize(piles, d, edge, hc, bc);
            if (user && ovr.L > 0) L = ovr.L;
            if (user && ovr.B > 0) B = ovr.B;
            if (ep != null)
            {
                (cox, coy) = EccentricFooting.OffsetFor(ep, L, B);
                if (Math.Abs(cox) > 1e-9 || Math.Abs(coy) > 1e-9) piles = lay.Select(q => (Math.Round(q.X + cox, 6) + 0.0, Math.Round(q.Y + coy, 6) + 0.0)).ToList();
                // [LùiCọcRanh] tim cọc cách ranh giới ≥ khoảng lùi ép cọc: dịch nhóm cọc vào trong, lập lại kích thước đài
                if (sbOn) { var sa = PileSetback.Adjust(p, n.X, n.Y, hc, bc, d, edge, piles, L, B, cox, coy); piles = sa.Piles; L = sa.L; B = sa.B; cox = sa.Ox; coy = sa.Oy; sbInfo = sa.Info; }
            }
            H = Hfix > 0 ? Hfix : Hmin;
            for (int it = 0; it < 100; it++)
            {
                h0 = H - embed - 0.05;
                G = WeightFactor * gtb * L * B * Math.Max(Df, H);
                ev = Evaluate(EvLoads(), piles, G, H, hc, bc, h0);
                if (Hfix > 0 || H >= MaxHeight - 1e-9) break;
                if (PunchShearOk(ev, piles, L, B, hc, bc, h0, edge, d, rbt, cox, coy)) break;
                H = Math.Round(H + 0.05, 3);
            }
            if (ev.PMax <= P + 1e-6 && ev.PMin >= -Pt - 1e-6) { found = true; break; }
        }
        if (!found && userPat)
            dsn.Messages.Add("LỖI bố trí cọc do người dùng chọn (" + PileLayout.Name(ovr.Pattern) + ", " + piles.Count + " cọc) không thoả [P] / [Pk]: Pmax = "
                             + DesignUtil.F(ev.PMax, "0") + " kN, Pmin = " + DesignUtil.F(ev.PMin, "0") + " kN – tăng số cọc hoặc khoảng cách cọc");
        else if (!found) dsn.Messages.Add("LỖI không chọn được số cọc thoả [P] với sơ đồ tới " + Counts[^1] + " cọc – tăng sức chịu tải cọc / tiết diện cọc");
        if (edgeRaised) dsn.Messages.Add("Mép cọc – mép đài tăng từ " + DesignUtil.F(Math.Max(fs.CapEdge, 0.05) * 1000, "0") + " lên " + DesignUtil.F(edge * 1000, "0")
                                         + " mm theo quy định cấu tạo (≥ 150 mm và tim cọc – mép đài ≥ 0,7d)");
        // ---- hết [FoundationChecks] ----
        if (piles.Count == 1 && loads.Any(l => Math.Abs(l.Mx) + Math.Abs(l.My) > 1e-3))
            dsn.Messages.Add("Đài 1 cọc: mô men chân cột do hệ giằng móng tiếp nhận (không kể vào phản lực cọc)");

        dsn.Piles = piles; dsn.L = L; dsn.B = B; dsn.H = H; dsn.H0 = h0; dsn.Weight = G;
        dsn.OffsetX = cox; dsn.OffsetY = coy;   // [MóngLệchTâm]
        dsn.PMax = ev.PMax; dsn.PMin = ev.PMin;
        var gv = ev.Gov;
        dsn.Combo = gv.Combo; dsn.N = gv.N; dsn.Mx = gv.Mx; dsn.My = gv.My; dsn.Qx = gv.Qx; dsn.Qy = gv.Qy;
        dsn.Reactions = ev.GovForces.ToList();
        string sch = piles.Count + " cọc " + (fs.PileRound ? "D" : "") + DesignUtil.F(d * 1000, "0") + (fs.PileRound ? "" : "x" + DesignUtil.F(d * 1000, "0"));

        dsn.Checks.Add(new CheckItem { Name = "Phản lực cọc lớn nhất Pmax ≤ [P]", Demand = ev.PMax, Capacity = P, Unit = "kN", Note = sch + ", tổ hợp " + gv.Combo });
        dsn.Checks.Add(new CheckItem
        {
            Name = "Phản lực cọc nhỏ nhất Pmin ≥ −[Pk]", Demand = ev.PMin < 0 ? -ev.PMin : 0, Capacity = ev.PMin < 0 ? Pt : Math.Max(Pt, 1), Unit = "kN",
            Note = "Pmin = " + DesignUtil.F(ev.PMin, "0.0") + " kN"
        });

        // ---- chọc thủng, cắt ----
        dsn.Checks.Add(new CheckItem
        {
            Name = "Chọc thủng đài do cột F ≤ Rbt·u·h0", Demand = ev.FCol, Capacity = PileCapChecks.ColumnPunchCapacityCut(piles, hc, bc, h0, d, rbt, L, B, cox, coy), Unit = "kN",   // [FoundationChecks] kể k = h0/c
            Note = "H = " + DesignUtil.F(H * 1000, "0") + " mm, h0 = " + DesignUtil.F(h0 * 1000, "0") + " mm"
        });
        if (piles.Count > 1)
            dsn.Checks.Add(new CheckItem { Name = "Chọc thủng đài do cọc góc", Demand = ev.FPile, Capacity = PilePunchCap(edge, d, h0, rbt), Unit = "kN" });
        double qbL = ShearCap(B, h0, MinClear(piles, true, hc, d), rbt), qbB = ShearCap(L, h0, MinClear(piles, false, bc, d), rbt);
        if (ev.QL > 1e-9) dsn.Checks.Add(new CheckItem { Name = "Cắt tiết diện nghiêng tại mép cột phương L", Demand = ev.QL, Capacity = qbL, Unit = "kN" });
        if (ev.QB > 1e-9) dsn.Checks.Add(new CheckItem { Name = "Cắt tiết diện nghiêng tại mép cột phương B", Demand = ev.QB, Capacity = qbB, Unit = "kN" });

        // ---- cọc khoan nhồi: hệ số nhóm (Converse–Labarre), thanh chống – giằng cho đài 2–4 cọc (D ≥ 0,8 m hoặc đài sâu) ----
        if (fs.PileGroupEfficiency && piles.Count >= 2)
        {
            var (gnx, gny) = BoredPileDesign.Rows(piles);
            double eta = BoredPileDesign.GroupEfficiency(gnx, gny, d, dsn.Spacing);
            dsn.Checks.Add(new CheckItem { Name = "Nhóm cọc ma sát: N + G ≤ η·n·[P] (Converse–Labarre)", Demand = Math.Max(gv.N, 0) + G, Capacity = eta * piles.Count * P, Unit = "kN",
                Note = "η = " + DesignUtil.F(eta, "0.000") + " (" + gnx + "×" + gny + " cọc, s = " + DesignUtil.F(dsn.Spacing, "0.00") + " m)" });
        }
        if (fs.PileBored && piles.Count >= 2 && piles.Count <= 4)
        {
            double rr = piles.Average(q => Math.Sqrt(q.X * q.X + q.Y * q.Y));   // cọc tính từ tim cột
            double cEq = Math.Sqrt(Math.Max(hc * bc, 1e-6));
            if (d >= 0.8 - 1e-9 || Math.Max(rr - cEq / 2, 0) / Math.Max(h0, 1e-6) <= 1.0)
            {
                double ap = fs.PileRound ? Math.PI * d * d / 4 : d * d;
                var stt = BoredPileDesign.StrutTie(Math.Max(Nmax, 0), piles.Count, rr, cEq, h0, hc * bc, ap, conc.Rb, main.Rs, conc.Rbn / 1000);
                dsn.Messages.AddRange(stt.Calc);
                if (d >= 0.8 - 1e-9)
                {
                dsn.Checks.Add(new CheckItem { Name = "Thanh chống – giằng: góc thanh chống θ ≥ 40°", Demand = 40, Capacity = stt.ThetaDeg, Unit = "°", Note = "Blévot 45°–55°" });
                dsn.Checks.Add(new CheckItem { Name = "Thanh chống – giằng: ứng suất nút cột ≤ ν'·Rb", Demand = stt.SigmaColNode, Capacity = stt.LimitColNode, Unit = "kN/m²" });
                dsn.Checks.Add(new CheckItem { Name = "Thanh chống – giằng: ứng suất nút đầu cọc ≤ 0,85·ν'·Rb", Demand = stt.SigmaPileNode, Capacity = stt.LimitPileNode, Unit = "kN/m²" });
                }
                dsn.Messages.Add("Thép giằng đáy đài tập trung trên dải đầu cọc (rộng ≈ D + h0): As ≥ " + DesignUtil.F(stt.AsReq * 1e4, "0.00") + " cm² mỗi giằng");
            }
        }

        // ---- cốt thép đáy ----
        int[] dias = DesignUtil.Sorted(cs.FootingBars, new[] { 12, 14, 16 }).Union(new[] { 16, 18, 20, 22, 25 }).Distinct().OrderBy(x => x).ToArray();
        double cover = Math.Max(cs.CoverFooting, 0.03);
        dsn.MomentL = ev.ML; dsn.MomentB = ev.MB;
        dsn.MomentTopL = ev.MTopL; dsn.MomentTopB = ev.MTopB;
        // lựa chọn thép của người dùng (Project.RebarChoices) – kiểm tra lại uốn bằng các CheckItem bên dưới
        string basis = FootingDesigner.Basis(L, B, H);
        int nUser = 0;
        SpacingChoice Ovr(string zone, SpacingChoice auto, double req)
        {
            var ch = p.ValidRebarChoice(key, zone, -1, basis);
            if (ch == null || ch.Dia <= 0 || ch.Spacing <= 0) return auto;
            nUser++;
            var sc = new SpacingChoice { Dia = ch.Dia, Spacing = ch.Spacing, AsReqPerM = req, Ok = true };
            if (ch.Spacing < 0.10 - 1e-9 || ch.Spacing > 0.20 + 1e-9)
                dsn.Messages.Add("LỖI bố trí thép do người dùng chọn không đạt – " + zone + " " + sc.Text + ": bước ngoài phạm vi 100…200 mm");
            if (sc.AsPerM < req * (1 - 1e-6))
                dsn.Messages.Add("LỖI bố trí thép do người dùng chọn không đạt – " + zone + " " + sc.Text + ": As = " + DesignUtil.F(sc.AsPerM * 1e4, "0.00")
                                 + " < As yêu cầu " + DesignUtil.F(req * 1e4, "0.00") + " cm²/m");
            return sc;
        }
        var sL = Ovr(RebarZones.FootingL, RcSection.ChooseSpacing(dia => AsPerM(ev.ML, B, h0, conc, main), dias, 0.20, 0.10, 0.15, 12), AsPerM(ev.ML, B, h0, conc, main));
        double h0B = h0 - sL.Dia / 1000.0;
        var sB = Ovr(RebarZones.FootingB, RcSection.ChooseSpacing(dia => AsPerM(ev.MB, L, h0B, conc, main), dias, 0.20, 0.10, 0.15, 12), AsPerM(ev.MB, L, h0B, conc, main));
        dsn.AsL = sL.AsReqPerM * B; dsn.AsB = sB.AsReqPerM * L;
        dsn.DiaL = sL.Dia; dsn.DiaB = sB.Dia; dsn.SpacingL = sL.Spacing; dsn.SpacingB = sB.Spacing;
        dsn.BarsL = sL.Text; dsn.BarsB = sB.Text;
        if (!sL.Ok || !sB.Ok) dsn.Messages.Add("LỖI không bố trí đủ thép đáy đài với bước ≥ 100 mm – tăng chiều cao đài");
        dsn.Checks.Add(new CheckItem
        {
            Name = "Uốn phương L tại mép cột", Demand = ev.ML, Unit = "kN.m", Note = sL.Text,
            Capacity = ev.ML > 1e-9 ? RcSection.MomentCapacity(B, h0, BarArea(sL) * B, 0, cover, conc, main) : 1
        });
        dsn.Checks.Add(new CheckItem
        {
            Name = "Uốn phương B tại mép cột", Demand = ev.MB, Unit = "kN.m", Note = sB.Text,
            Capacity = ev.MB > 1e-9 ? RcSection.MomentCapacity(L, h0B, BarArea(sB) * L, 0, cover, conc, main) : 1
        });

        // ---- thép lớp trên (cấu tạo Ø12a200, hoặc theo tính toán khi có cọc chịu nhổ) ----
        double h0t = H - cover - 0.01;
        double reqTL = Math.Max(AsPerM(ev.MTopL, B, h0t, conc, main), RcSection.BarArea(12) / 0.20);
        var tL = Ovr(RebarZones.CapTopL, RcSection.ChooseSpacing(dia => reqTL, dias, 0.20, 0.10, 0.20, 12), reqTL);
        double reqTB = Math.Max(AsPerM(ev.MTopB, L, h0t - tL.Dia / 1000.0, conc, main), RcSection.BarArea(12) / 0.20);
        var tB = Ovr(RebarZones.CapTopB, RcSection.ChooseSpacing(dia => reqTB, dias, 0.20, 0.10, 0.20, 12), reqTB);
        dsn.BarsTop = tL.Text == tB.Text ? tL.Text : tL.Text + " / " + tB.Text;
        dsn.BarsTopL = tL.Text; dsn.BarsTopB = tB.Text;
        dsn.UserRebarZones = nUser;
        if (nUser > 0) dsn.Messages.Add("Bố trí thép theo lựa chọn của người dùng tại " + nUser + " vùng (đã kiểm tra lại khả năng chịu lực)");
        if (ev.MTopL > 1e-9)
            dsn.Checks.Add(new CheckItem
            {
                Name = "Uốn lớp trên phương L do cọc chịu nhổ", Demand = ev.MTopL, Unit = "kN.m", Note = tL.Text,
                Capacity = RcSection.MomentCapacity(B, h0t, BarArea(tL) * B, 0, cover, conc, main)
            });
        if (ev.MTopB > 1e-9)
            dsn.Checks.Add(new CheckItem
            {
                Name = "Uốn lớp trên phương B do cọc chịu nhổ", Demand = ev.MTopB, Unit = "kN.m", Note = tB.Text,
                Capacity = RcSection.MomentCapacity(L, h0t - tL.Dia / 1000.0, BarArea(tB) * L, 0, cover, conc, main)
            });

        // ---- bố trí thép ----
        double c = cover, zb = embed + 0.03;
        double lenL = DesignUtil.R10(L - 2 * c), lenB = DesignUtil.R10(B - 2 * c);
        double legB = DesignUtil.R10(Math.Max(Math.Min(H - zb - c, Math.Max(15 * sL.Dia / 1000.0, 0.20)), 0.10));
        double legB2 = DesignUtil.R10(Math.Max(Math.Min(H - zb - c, Math.Max(15 * sB.Dia / 1000.0, 0.20)), 0.10));
        int Cnt(double width, double sp) => (int)Math.Ceiling((width - 2 * c) / Math.Max(sp, 0.05) - 1e-9) + 1;
        dsn.Bars.Add(new BarMark
        {
            Role = "Lớp dưới phương L", Dia = sL.Dia, Spacing = sL.Spacing, Count = Cnt(B, sL.Spacing), Shape = "U",
            Length = DesignUtil.R10(lenL + 2 * legB), Start = c, End = L - c,
            Segments = new List<double> { DesignUtil.Mm(legB), DesignUtil.Mm(lenL), DesignUtil.Mm(legB) }
        });
        dsn.Bars.Add(new BarMark
        {
            Role = "Lớp dưới phương B", Dia = sB.Dia, Spacing = sB.Spacing, Count = Cnt(L, sB.Spacing), Shape = "U",
            Length = DesignUtil.R10(lenB + 2 * legB2), Start = c, End = B - c,
            Segments = new List<double> { DesignUtil.Mm(legB2), DesignUtil.Mm(lenB), DesignUtil.Mm(legB2) }
        });
        double legT = DesignUtil.R10(Math.Max(Math.Min(H - 2 * c - 0.10, 0.20), 0.10));
        dsn.Bars.Add(new BarMark
        {
            Role = "Lớp trên phương L", Dia = tL.Dia, Spacing = tL.Spacing, Count = Cnt(B, tL.Spacing), Shape = "U", IsTop = true,
            Length = DesignUtil.R10(lenL + 2 * legT), Start = c, End = L - c,
            Segments = new List<double> { DesignUtil.Mm(legT), DesignUtil.Mm(lenL), DesignUtil.Mm(legT) }
        });
        dsn.Bars.Add(new BarMark
        {
            Role = "Lớp trên phương B", Dia = tB.Dia, Spacing = tB.Spacing, Count = Cnt(L, tB.Spacing), Shape = "U", IsTop = true,
            Length = DesignUtil.R10(lenB + 2 * legT), Start = c, End = B - c,
            Segments = new List<double> { DesignUtil.Mm(legT), DesignUtil.Mm(lenB), DesignUtil.Mm(legT) }
        });
        RebarNumbering.Renumber(dsn.Bars);
        // [FoundationChecks] kiểm tra an toàn bổ sung (phản lực từng cọc, nhóm cọc, tải ngang, lún, bố trí, chọc thủng chi tiết) → dsn.Safety
        try { PileCapChecks.Attach(p, dsn, EvLoads(), fs, geo, ovr, Hmin, Hfix > 0, conc); }
        catch (Exception ex) { dsn.Messages.Add("Cảnh báo: không lập được bảng kiểm tra an toàn đài – " + ex.Message); }
        bool sbShift = sbInfo != null && sbInfo.Shifted;   // [LùiCọcRanh] độ lệch tâm đòn bẩy = trọng tâm nhóm cọc (đài vươn sát ranh: tâm đài ≠ trọng tâm cọc)
        if (ep != null) AttachEccentric(plan, ep, dsn, sbShift ? piles.Average(q => q.X) : cox, sbShift ? piles.Average(q => q.Y) : coy);   // [MóngLệchTâm]
        if (sbInfo != null)   // [LùiCọcRanh] kiểm tra khoảng lùi, phản lực có / không dầm cân bằng, công xôn đài, xoắn, phương án
        {
            try { PileSetback.Attach(p, dsn, sbInfo, plan, ep, loads, P, Pt, conc, main, cover); dsn.Setback = sbInfo; }
            catch (Exception ex) { dsn.Messages.Add("Cảnh báo: không lập được kiểm tra lùi cọc khỏi ranh giới – " + ex.Message); }
        }
        return dsn;
    }

    /// <summary>[MóngLệchTâm] Ghi thông tin đài lệch tâm (ranh giới), liên kết dầm cân bằng và ΔR cho đài cột trong.</summary>
    private static void AttachEccentric(EccPlan plan, EccNode ep, PileCapDesign dsn, double ox, double oy)
    {
        EccentricFooting.Record(plan, ep, ox, oy, dsn.L, dsn.B, "", true);
        bool off = Math.Abs(ox) > 1e-6 || Math.Abs(oy) > 1e-6;
        if (!off && !ep.StrapX && !ep.StrapY && ep.RelievedBy.Count == 0) return;
        var info = EccentricFooting.Info(plan, ep, ox, oy, new PressureResult { Contact = 1 }, dsn.L, dsn.B);
        info.PileCap = true;
        if (ep.Scheme == EccentricScheme.Combined) { info.Scheme = EccentricScheme.SelfBalanced; info.Recommendation += " (đài cọc: móng kết hợp chưa hỗ trợ – dùng đài lệch tâm)"; }
        info.Straps.AddRange(EccentricFooting.Links(plan, ep, dsn.L, dsn.B, "", true));
        dsn.Eccentric = info;
        if (off)
            dsn.Messages.Add($"Đài lệch tâm do ranh giới đất ({info.Sides}): tâm đài lệch so với tim cột ex = {ox * 1000:0} mm, ey = {oy * 1000:0} mm, cọc bố trí trong ranh giới – "
                             + (ep.StrapX || ep.StrapY ? $"có dầm móng cân bằng: lực quy đổi N' = R1 = {info.R1:0.0} kN tại trọng tâm nhóm cọc (đòn bẩy), phản lực cọc đều theo phương dầm"
                                                       : "nhóm cọc lệch tâm tự cân bằng: N sinh thêm mô men N·e (đã kể trong phản lực cọc), mô men lệch tâm do hệ giằng móng / cột tiếp nhận một phần"));
        if (ep.RelievedBy.Count > 0 && info.Relief > 1e-6)
            dsn.Messages.Add($"Đài cột trong được giảm tải do dầm cân bằng từ nút {string.Join(", ", info.ReliefFrom)}: N' = N − β·ΔR = {info.N1 - info.Relief:0.0} kN (β = {plan.Fs.StrapReliefFactor:0.##})");
    }

    /// <summary>[FoundationChecks] Thiết kế thử đài cọc tại một nút chân cột với thông số fs (vd. bản sao có bố trí cọc thử) – dùng cho hộp thoại "Bố trí cọc…".
    /// Không gộp đài, không đánh tên. Ném lỗi nếu nút không phải chân cột BTCT hoặc không có phản lực.</summary>
    public static PileCapDesign DesignNode(Project p, AnalysisResults r, int nodeId, FoundationSettings fs = null)
    {
        if (p == null || r == null) throw new ArgumentNullException(p == null ? nameof(p) : nameof(r));
        var bc = FoundationUtil.BaseColumns(p).FirstOrDefault(x => x.Node.Id == nodeId);
        if (bc.Node == null) throw new InvalidOperationException("nút " + nodeId + " không phải chân cột BTCT");
        var plan = EccentricFooting.Plan(p, r, FoundationUtil.Combos(p, r), FoundationUtil.BaseColumns(p).Where(x => FoundationUtil.TypeOf(p, x.Node.Id) == FoundationType.Pile || x.Node.Id == nodeId));   // [MóngLệchTâm]
        var fsT = fs ?? p.Foundation ?? new FoundationSettings();
        // [CặpĐàiBiên] đài biên đối diện: thiết kế trước đài đối diện để có e2 thật (không dùng ước lượng)
        var en = plan?.Get(nodeId);
        if (EccentricPairs.Opposite(en))
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (int pid in new[] { en.StrapX && en.PartnerEX > 0 ? en.PartnerX : 0, en.StrapY && en.PartnerEY > 0 ? en.PartnerY : 0 }.Where(x => x != 0).Distinct())
                {
                    var pc = FoundationUtil.BaseColumns(p).FirstOrDefault(x => x.Node.Id == pid);
                    if (pc.Node == null) continue;
                    try { DesignOne(p, r, pc.Node, pc.Column, FoundationUtil.Combos(p, r), p.Foundation ?? fsT, plan); } catch (Exception) { }
                }
                try { DesignOne(p, r, bc.Node, bc.Column, FoundationUtil.Combos(p, r), fsT, plan); } catch (Exception) { }
                if (!EccentricPairs.Sync(plan)) break;
            }
        var d = DesignOne(p, r, bc.Node, bc.Column, FoundationUtil.Combos(p, r), fsT, plan);
        d.Name = "ĐC";
        return d;
    }

    private static double BarArea(SpacingChoice sc) => sc.Spacing > 0 ? RcSection.BarArea(sc.Dia) / sc.Spacing : 0;

    /// <summary>Khoá cấu kiện vật lý của đài cọc (lựa chọn thép): "P:{id nút chân cột}".</summary>
    internal static string MemberKey(int nodeId) => "P:" + nodeId.ToString(DesignUtil.Inv);

    /// <summary>Phương án Ø–bước cho thép đài cọc đã thiết kế: lớp dưới L/B (FootingL/FootingB), lớp trên L/B (CapTopL/CapTopB).</summary>
    internal static List<RebarAlternative> Alternatives(Project p, PileCapDesign d, string zone, int max = 12)
    {
        var cs = p.Concrete;
        var conc = DesignUtil.ConcreteOf(p, cs.ConcreteFooting, cs.ConcreteFooting);
        var main = p.Material(cs.RebarMain);
        int[] dias = DesignUtil.Sorted(cs.FootingBars, new[] { 12, 14, 16 }).Union(new[] { 16, 18, 20, 22, 25 }).Distinct().OrderBy(x => x).ToArray();
        double cover = Math.Max(cs.CoverFooting, 0.03), h0 = d.H0 > 0 ? d.H0 : d.H - cover - 0.05, h0t = d.H - cover - 0.01;
        double req; string cur;
        if (zone == RebarZones.FootingL) { req = AsPerM(d.MomentL, d.B, h0, conc, main); cur = d.BarsL; }
        else if (zone == RebarZones.FootingB) { req = AsPerM(d.MomentB, d.L, h0 - d.DiaL / 1000.0, conc, main); cur = d.BarsB; }
        else if (zone == RebarZones.CapTopL) { req = Math.Max(AsPerM(d.MomentTopL, d.B, h0t, conc, main), RcSection.BarArea(12) / 0.20); cur = d.BarsTopL; }
        else { req = Math.Max(AsPerM(d.MomentTopB, d.L, h0t, conc, main), RcSection.BarArea(12) / 0.20); cur = d.BarsTopB; }
        return RebarOptions.SpacingAlternatives("Móng", zone, _ => req, dias.Where(x => x >= 12).ToArray(), 0.10, 0.20, cur, max);
    }

    /// <summary>As yêu cầu trên 1 m bề rộng đài: As = DesignRect(M, b, h0)/b, tối thiểu 0,05%·h0.</summary>
    private static double AsPerM(double M, double b, double h0, Material conc, Material steel)
    {
        if (b <= 0 || h0 <= 0) return 1;
        var f = RcSection.DesignRect(Math.Max(M, 0), b, h0, 0.05, conc, steel, 0.05);
        double As = f.Doubly ? Math.Max(f.As, M / (0.9 * steel.Rs * h0)) : f.As;
        return Math.Max(As / b, 0.0005 * h0);
    }

    /// <summary>Chọc thủng của cọc góc: u = 2·(e + d + h0/2) (2 cạnh tháp trong đài), Fu = Rbt·u·h0.</summary>
    private static double PilePunchCap(double edge, double d, double h0, double rbt) =>
        rbt * Math.Min(2 * (edge + d + h0 / 2), 4 * (d + h0)) * Math.Max(h0, 0);

    /// <summary>Khả năng chịu cắt của bê tông đài không cốt đai: Qb = 1,5·Rbt·b·h0²/c, 0,5 ≤ Qb/(Rbt·b·h0) ≤ 2,5
    /// với c = khoảng cách từ mép cột tới mép trong của hàng cọc (TCVN 5574:2018 8.1.3.3).</summary>
    private static double ShearCap(double b, double h0, double clear, double rbt)
    {
        if (h0 <= 0 || b <= 0) return 0;
        double c = Math.Max(clear, 1e-3);
        return Math.Clamp(1.5 * rbt * b * h0 * h0 / c, 0.5 * rbt * b * h0, 2.5 * rbt * b * h0);
    }

    private static double MinClear(List<(double X, double Y)> piles, bool alongX, double colDim, double d)
    {
        double best = double.MaxValue;
        foreach (var q in piles)
        {
            double o = Math.Abs(alongX ? q.X : q.Y) - colDim / 2;
            if (o > 1e-6) best = Math.Min(best, Math.Max(o - d / 2, 0));
        }
        return best == double.MaxValue ? 0 : best;
    }

    private static bool PunchShearOk(Eval e, List<(double X, double Y)> piles, double L, double B, double hc, double bc, double h0, double edge, double d, double rbt,
        double ox = 0, double oy = 0)
    {
        if (h0 <= 0.05) return false;
        if (e.FCol > PileCapChecks.ColumnPunchCapacityCut(piles, hc, bc, h0, d, rbt, L, B, ox, oy) + 1e-6) return false;   // [FoundationChecks] kể k = h0/c; [MóngLệchTâm] cột sát mép đài
        if (piles.Count > 1 && e.FPile > PilePunchCap(edge, d, h0, rbt) + 1e-6) return false;
        if (e.QL > ShearCap(B, h0, MinClear(piles, true, hc, d), rbt) + 1e-6) return false;
        if (e.QB > ShearCap(L, h0, MinClear(piles, false, bc, d), rbt) + 1e-6) return false;
        return true;
    }

    private static string Signature(PileCapDesign d) =>
        Math.Round(d.L * 100) + "x" + Math.Round(d.B * 100) + "x" + Math.Round(d.H * 100) + "|" + Math.Round(d.ColumnB * 1000) + "x" + Math.Round(d.ColumnH * 1000)
        + "|" + string.Join(";", d.Piles.Select(q => Math.Round(q.X * 1000) + "," + Math.Round(q.Y * 1000)))
        + "|" + d.BarsL + "|" + d.BarsB + "|" + d.BarsTop + "|" + RebarNumbering.Signature(d.Bars)
        + (Math.Abs(d.OffsetX) > 1e-6 || Math.Abs(d.OffsetY) > 1e-6 ? "|O" + Math.Round(d.OffsetX * 1000) + "," + Math.Round(d.OffsetY * 1000) : "")   // [MóngLệchTâm]
        + EccentricFooting.Sig(d.Eccentric);
}
