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
        var combos = r.DesignCombos();
        var all = new List<(PileCapDesign d, string sig)>();
        foreach (var (n, col) in FoundationUtil.BaseColumns(p))
        {
            if (FoundationUtil.TypeOf(p, n.Id) != FoundationType.Pile) continue;
            try
            {
                var d = DesignOne(p, r, n, col, combos, fs);
                all.Add((d, Signature(d)));
            }
            catch (Exception ex)
            {
                warnings.Add("LỖI thiết kế đài cọc tại " + DesignUtil.GridName(p, n.X, n.Y) + ": " + ex.Message);
            }
        }
        foreach (var g in all.GroupBy(x => x.sig))
        {
            var items = g.Select(x => x.d).ToList();
            var keep = items.OrderByDescending(x => x.PMax).First();
            keep.Quantity = items.Count;
            keep.NodeIds = items.SelectMany(x => x.NodeIds).Distinct().ToList();
            keep.Locations = items.Select(x => (x.X, x.Y)).ToList();
            keep.PMin = items.Min(x => x.PMin);
            foreach (var o in items)
            {
                if (ReferenceEquals(o, keep)) continue;
                for (int i = 0; i < keep.Checks.Count && i < o.Checks.Count; i++)
                    if (o.Checks[i].Name == keep.Checks[i].Name && o.Checks[i].Ratio > keep.Checks[i].Ratio) keep.Checks[i] = o.Checks[i];
                foreach (var m in o.Messages.Where(m => m.StartsWith("LỖI") && !keep.Messages.Contains(m))) keep.Messages.Add(m);
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

    /// <summary>Phản lực các cọc (kN, nén dương) của một tổ hợp: Pi = (N + G)/n + ax·xi + ay·yi,
    /// Σxi·Pi = −My − H·Qx; Σyi·Pi = Mx − H·Qy (mô men phản lực chân cột + lực cắt × chiều cao đài).</summary>
    public static double[] PileForces(BaseLoad l, IList<(double X, double Y)> piles, double G, double H)
    {
        int np = piles.Count;
        var res = new double[np];
        if (np == 0) return res;
        double cx = piles.Average(q => q.X), cy = piles.Average(q => q.Y);
        double sx2 = piles.Sum(q => (q.X - cx) * (q.X - cx)), sy2 = piles.Sum(q => (q.Y - cy) * (q.Y - cy));
        double nt = l.N + G;
        double ax = sx2 > 1e-9 ? (-l.My - H * l.Qx) / sx2 : 0;
        double ay = sy2 > 1e-9 ? (l.Mx - H * l.Qy) / sy2 : 0;
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

    private static PileCapDesign DesignOne(Project p, AnalysisResults r, Node n, FrameElement col, List<string> combos, FoundationSettings fs)
    {
        var cs = p.Concrete; var soil = p.Soil;
        var conc = DesignUtil.ConcreteOf(p, cs.ConcreteFooting, cs.ConcreteFooting);
        var main = p.Material(cs.RebarMain);
        double hc = DesignUtil.ColumnDimAlong(p, col, Vec3.UnitX);
        double bc = DesignUtil.ColumnDimAlong(p, col, Vec3.UnitY);
        var loads = FoundationUtil.Loads(r, n.Id, combos);
        if (loads.Count == 0) throw new InvalidOperationException("không có phản lực tại nút " + n.Id);
        if (!(fs.PileCapacity > 0)) throw new InvalidOperationException("chưa khai báo sức chịu tải cọc [P] > 0");

        double d = Math.Max(fs.PileSize, 0.10);
        double P = fs.PileCapacity, Pt = Math.Max(fs.PileTension, 0);
        double edge = Math.Max(fs.CapEdge, 0.05);
        double s = RcSection.CeilTo(Math.Max(fs.PileSpacingFactor, 1.5) * d, 0.05);
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
            PileSize = d, PileRound = fs.PileRound, PileCapacity = P, PileLength = fs.PileLength, Spacing = s
        };
        dsn.Locations.Add((n.X, n.Y));
        double Nmax = loads.Max(l => l.N);
        if (Nmax <= 0) dsn.Messages.Add("Chân cột chịu kéo ở mọi tổ hợp – kiểm tra sức chịu nhổ của cọc");
        else if (loads.Any(l => l.N <= 0)) dsn.Messages.Add("Có tổ hợp chân cột chịu nhổ – kiểm tra sức chịu nhổ của cọc");

        double myMax = loads.Max(l => Math.Abs(l.My) + Math.Abs(l.Qx) * Hmin);
        double mxMax = loads.Max(l => Math.Abs(l.Mx) + Math.Abs(l.Qy) * Hmin);
        bool longX = myMax >= mxMax;

        int n0 = Math.Max(1, (int)Math.Ceiling(1.2 * Math.Max(Nmax, 0) / P - 1e-9));
        bool singleOk = d + 2 * edge >= Math.Max(hc, bc) + 0.10 - 1e-9;
        if (n0 == 1 && !singleOk) n0 = 2;

        List<(double X, double Y)> piles = null;
        double L = 0, B = 0, H = 0, h0 = 0, G = 0;
        Eval ev = null;
        bool found = false;
        var cands = Counts.Where(cc => cc >= n0).ToList();
        if (cands.Count == 0) cands.Add(Counts[^1]);
        foreach (int np in cands)
        {
            piles = Layout(np, s, longX);
            double spanX = piles.Max(q => q.X) - piles.Min(q => q.X), spanY = piles.Max(q => q.Y) - piles.Min(q => q.Y);
            L = RcSection.CeilTo(Math.Max(spanX + d + 2 * edge, hc + 0.20), 0.05);
            B = RcSection.CeilTo(Math.Max(spanY + d + 2 * edge, bc + 0.20), 0.05);
            H = Hgiven > 0 ? Hgiven : Hmin;
            for (int it = 0; it < 100; it++)
            {
                h0 = H - embed - 0.05;
                G = WeightFactor * gtb * L * B * Math.Max(Df, H);
                ev = Evaluate(loads, piles, G, H, hc, bc, h0);
                if (Hgiven > 0 || H >= MaxHeight - 1e-9) break;
                if (PunchShearOk(ev, piles, L, B, hc, bc, h0, edge, d, rbt)) break;
                H = Math.Round(H + 0.05, 3);
            }
            if (ev.PMax <= P + 1e-6 && ev.PMin >= -Pt - 1e-6) { found = true; break; }
        }
        if (!found) dsn.Messages.Add("LỖI không chọn được số cọc thoả [P] với sơ đồ tới " + Counts[^1] + " cọc – tăng sức chịu tải cọc / tiết diện cọc");
        if (piles.Count == 1 && loads.Any(l => Math.Abs(l.Mx) + Math.Abs(l.My) > 1e-3))
            dsn.Messages.Add("Đài 1 cọc: mô men chân cột do hệ giằng móng tiếp nhận (không kể vào phản lực cọc)");

        dsn.Piles = piles; dsn.L = L; dsn.B = B; dsn.H = H; dsn.H0 = h0; dsn.Weight = G;
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
        double uCol = 2 * (hc + bc + 2 * h0);
        dsn.Checks.Add(new CheckItem
        {
            Name = "Chọc thủng đài do cột F ≤ Rbt·u·h0", Demand = ev.FCol, Capacity = rbt * uCol * h0, Unit = "kN",
            Note = "H = " + DesignUtil.F(H * 1000, "0") + " mm, h0 = " + DesignUtil.F(h0 * 1000, "0") + " mm"
        });
        if (piles.Count > 1)
            dsn.Checks.Add(new CheckItem { Name = "Chọc thủng đài do cọc góc", Demand = ev.FPile, Capacity = PilePunchCap(edge, d, h0, rbt), Unit = "kN" });
        double qbL = ShearCap(B, h0, MinClear(piles, true, hc, d), rbt), qbB = ShearCap(L, h0, MinClear(piles, false, bc, d), rbt);
        if (ev.QL > 1e-9) dsn.Checks.Add(new CheckItem { Name = "Cắt tiết diện nghiêng tại mép cột phương L", Demand = ev.QL, Capacity = qbL, Unit = "kN" });
        if (ev.QB > 1e-9) dsn.Checks.Add(new CheckItem { Name = "Cắt tiết diện nghiêng tại mép cột phương B", Demand = ev.QB, Capacity = qbB, Unit = "kN" });

        // ---- cốt thép đáy ----
        int[] dias = DesignUtil.Sorted(cs.FootingBars, new[] { 12, 14, 16 }).Union(new[] { 16, 18, 20, 22, 25 }).Distinct().OrderBy(x => x).ToArray();
        double cover = Math.Max(cs.CoverFooting, 0.03);
        dsn.MomentL = ev.ML; dsn.MomentB = ev.MB;
        var sL = RcSection.ChooseSpacing(dia => AsPerM(ev.ML, B, h0, conc, main), dias, 0.20, 0.10, 0.15, 12);
        double h0B = h0 - sL.Dia / 1000.0;
        var sB = RcSection.ChooseSpacing(dia => AsPerM(ev.MB, L, h0B, conc, main), dias, 0.20, 0.10, 0.15, 12);
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
        var tL = RcSection.ChooseSpacing(dia => Math.Max(AsPerM(ev.MTopL, B, h0t, conc, main), BarArea(12) / 0.20), dias, 0.20, 0.10, 0.20, 12);
        var tB = RcSection.ChooseSpacing(dia => Math.Max(AsPerM(ev.MTopB, L, h0t - tL.Dia / 1000.0, conc, main), BarArea(12) / 0.20), dias, 0.20, 0.10, 0.20, 12);
        dsn.BarsTop = tL.Text == tB.Text ? tL.Text : tL.Text + " / " + tB.Text;
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
        return dsn;
    }

    private static double BarArea(SpacingChoice sc) => sc.Spacing > 0 ? RcSection.BarArea(sc.Dia) / sc.Spacing : 0;

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

    private static bool PunchShearOk(Eval e, List<(double X, double Y)> piles, double L, double B, double hc, double bc, double h0, double edge, double d, double rbt)
    {
        if (h0 <= 0.05) return false;
        if (e.FCol > rbt * 2 * (hc + bc + 2 * h0) * h0 + 1e-6) return false;
        if (piles.Count > 1 && e.FPile > PilePunchCap(edge, d, h0, rbt) + 1e-6) return false;
        if (e.QL > ShearCap(B, h0, MinClear(piles, true, hc, d), rbt) + 1e-6) return false;
        if (e.QB > ShearCap(L, h0, MinClear(piles, false, bc, d), rbt) + 1e-6) return false;
        return true;
    }

    private static string Signature(PileCapDesign d) =>
        Math.Round(d.L * 100) + "x" + Math.Round(d.B * 100) + "x" + Math.Round(d.H * 100) + "|" + Math.Round(d.ColumnB * 1000) + "x" + Math.Round(d.ColumnH * 1000)
        + "|" + string.Join(";", d.Piles.Select(q => Math.Round(q.X * 1000) + "," + Math.Round(q.Y * 1000)))
        + "|" + d.BarsL + "|" + d.BarsB + "|" + d.BarsTop + "|" + RebarNumbering.Signature(d.Bars);
}
