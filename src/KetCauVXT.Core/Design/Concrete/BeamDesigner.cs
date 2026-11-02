using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Thiết kế dầm BTCT liên tục theo TCVN 5574:2018: gom phần tử thành dải dầm, tính thép dọc, đai, độ võng,
/// bố trí thép (BarMark) và vùng đai (StirrupZone) theo quy tắc thể hiện kiểu Kết cấu Soft.
/// <para>Dầm cong (FrameElement.IsCurved): mỗi phần tử dầm cong là một dải dầm riêng, toạ độ dọc dải = chiều dài cung
/// (nội lực của bộ phân tích đã theo cung, hệ trục tiếp tuyến). Ngoài uốn + cắt còn kiểm tra xoắn (RcTorsion – giản lược theo
/// TCVN 5574:2018 mục 8.1.4): cốt dọc chịu xoắn cộng ½ vào thép trên, ½ vào thép dưới; đai kín bước theo xoắn + cắt.</para></summary>
public static class BeamDesigner
{
    private const double Tol = 1e-3;
    /// <summary>Hệ số độ cứng khi nứt dùng ước lượng độ võng: hiện chỉ dùng để tham khảo – độ võng tính theo độ cứng chống uốn của tiết diện có vết nứt (8.2.3 TCVN 5574:2018).</summary>
    public const double CrackedStiffnessFactor = 0.3;
    /// <summary>Chiều dài cây thép thương phẩm, m – vượt quá phải nối chồng.</summary>
    public const double StockLength = 11.7;

    // ------------------------------------------------------------------
    // Cấu trúc trung gian
    // ------------------------------------------------------------------
    internal sealed class Seg
    {
        public FrameElement F;
        public bool Reversed;
        public double X0;
        public double Len;
    }

    internal sealed class SpanInfo
    {
        public List<Seg> Segs = new();
        public double X0;
        public double L;
        public int NodeL, NodeR;
        public double WL, WR;
        public bool SupL, SupR;
        public string NameL = "", NameR = "";
        public double FaceL => Math.Min(WL / 2, 0.45 * L);
        public double FaceR => L - Math.Min(WR / 2, 0.45 * L);
        public double Lclear => FaceR - FaceL;
    }

    internal sealed class LineInfo
    {
        public string Story = "";
        public string Group = "";
        public FrameSection Sec;
        public Vec3 U;
        public Vec3 Origin;
        public double Z;
        public List<Seg> Segs = new();
        public List<SpanInfo> Spans = new();
        public bool AlongX => Math.Abs(U.X) >= Math.Abs(U.Y);
        public double Perp => AlongX ? Origin.Y : Origin.X;
        /// <summary>Dầm cong: hình học cung (dải gồm đúng một phần tử); null = dải dầm thẳng.</summary>
        public ArcGeometry Arc;
    }

    // ------------------------------------------------------------------
    // API
    // ------------------------------------------------------------------

    /// <summary>Thiết kế toàn bộ dầm BTCT (Kind == Beam) của dự án. Lỗi từng dầm ghi vào warnings với tiền tố "LỖI".</summary>
    public static List<BeamLineDesign> Design(Project p, AnalysisResults r, List<string> warnings)
    {
        var result = new List<BeamLineDesign>();
        warnings ??= new List<string>();
        var byNode = FramesByNode(p);
        var lines = BuildLines(p, byNode, warnings);
        var combos = r.DesignCombos();
        var sls = r.ServiceCombos();

        var designed = new List<(BeamLineDesign d, LineInfo li)>();
        foreach (var li in lines)
        {
            try
            {
                designed.Add((DesignLine(p, r, li, combos, sls), li));
            }
            catch (Exception ex)
            {
                string ids = string.Join(",", li.Segs.Select(s => s.F.Id));
                warnings.Add("LỖI thiết kế dầm tầng " + li.Story + " (phần tử " + ids + "): " + ex.Message);
            }
        }

        // gộp dầm giống hệt nhau trong cùng tầng, đặt tên D{tầng}-{n}
        foreach (var st in designed.GroupBy(x => x.d.Story).OrderBy(g => DesignUtil.StoryOrder(p, g.Key)))
        {
            var ordered = st.OrderBy(x => x.li.AlongX ? 0 : 1).ThenBy(x => Math.Round(x.li.Perp, 3))
                            .ThenBy(x => Math.Round(x.li.AlongX ? x.li.Origin.X : x.li.Origin.Y, 3)).ToList();
            var merged = new List<(BeamLineDesign d, string sig)>();
            foreach (var (d, li) in ordered)
            {
                string sig = Signature(d);
                int k = merged.FindIndex(m => m.sig == sig);
                if (k >= 0)
                {
                    var keep = merged[k].d;
                    keep.Quantity++;
                    keep.RebarKeys.AddRange(d.RebarKeys.Where(x => !keep.RebarKeys.Contains(x)));
                    keep.Messages.Add("Gộp dầm giống hệt: phần tử " + string.Join(",", d.Frames));
                    // giữ hệ số kiểm tra bất lợi nhất
                    for (int i = 0; i < keep.Checks.Count && i < d.Checks.Count; i++)
                        if (d.Checks[i].Ratio > keep.Checks[i].Ratio) keep.Checks[i] = d.Checks[i];
                }
                else merged.Add((d, sig));
            }
            int n = 1;
            string sn = DesignUtil.StoryNo(st.Key);
            foreach (var (d, _) in merged)
            {
                d.Name = "D" + sn + "-" + n++;
                result.Add(d);
            }
        }
        return result;
    }

    // ------------------------------------------------------------------
    // Gom dải dầm
    // ------------------------------------------------------------------
    internal static Dictionary<int, List<FrameElement>> FramesByNode(Project p)
    {
        var d = new Dictionary<int, List<FrameElement>>();
        foreach (var f in p.Frames)
            foreach (int n in new[] { f.NodeI, f.NodeJ })
            {
                if (!d.TryGetValue(n, out var l)) d[n] = l = new List<FrameElement>();
                l.Add(f);
            }
        return d;
    }

    private static Vec3 Dir(Project p, FrameElement f) => (p.Node(f.NodeJ).P - p.Node(f.NodeI).P).Normalize();

    private static bool Parallel(Vec3 a, Vec3 b) => a.Cross(b).Length < 1e-3;

    /// <summary>Gom các phần tử dầm cùng DesignGroup, cùng tầng, cùng tiết diện, thẳng hàng và nối đầu–cuối thành dải dầm liên tục,
    /// rồi chia nhịp tại các nút có cột hoặc dầm giao.</summary>
    internal static List<LineInfo> BuildLines(Project p, Dictionary<int, List<FrameElement>> byNode, List<string> warnings)
    {
        var lines = new List<LineInfo>();
        var beams = p.Frames.Where(f => f.Kind == FrameKind.Beam).ToList();
        foreach (var g in beams.GroupBy(f => (f.Story ?? "") + "\u0001" + (f.DesignGroup ?? "") + "\u0001" + f.Section))
        {
            var pool = g.ToList();
            var used = new HashSet<int>();
            foreach (var f0 in pool)
            {
                if (used.Contains(f0.Id)) continue;
                FrameSection sec;
                try { sec = p.Section(f0.Section); }
                catch (KeyNotFoundException) { warnings.Add("LỖI dầm " + f0.Id + ": không có tiết diện " + f0.Section); used.Add(f0.Id); continue; }
                if (sec.Shape != SectionShape.RectConcrete || sec.B <= 0 || sec.H <= 0)
                {
                    warnings.Add("Dầm " + f0.Id + " (" + f0.Section + ") không phải tiết diện chữ nhật BTCT – bỏ qua thiết kế BTCT");
                    used.Add(f0.Id);
                    continue;
                }
                if (f0.IsCurved && CurvedBeam.Arc(p, f0) != null)
                {
                    // dầm cong: dải riêng một phần tử
                    used.Add(f0.Id);
                    var lc = MakeCurvedLine(p, f0, sec, byNode);
                    if (lc != null) lines.Add(lc);
                    continue;
                }
                var u = Dir(p, f0);
                var chain = new List<FrameElement> { f0 };
                var nodes = new HashSet<int> { f0.NodeI, f0.NodeJ };
                used.Add(f0.Id);
                bool grown = true;
                while (grown)
                {
                    grown = false;
                    foreach (var f in pool)
                    {
                        if (used.Contains(f.Id)) continue;
                        if (f.IsCurved) continue;   // dầm cong không gộp vào dải thẳng
                        if (!nodes.Contains(f.NodeI) && !nodes.Contains(f.NodeJ)) continue;
                        if (!Parallel(u, Dir(p, f))) continue;
                        chain.Add(f); used.Add(f.Id);
                        nodes.Add(f.NodeI); nodes.Add(f.NodeJ);
                        grown = true;
                    }
                }
                var li = MakeLine(p, chain, sec, u, byNode);
                if (li != null) lines.Add(li);
            }
        }
        return lines;
    }

    private static LineInfo MakeLine(Project p, List<FrameElement> chain, FrameSection sec, Vec3 u, Dictionary<int, List<FrameElement>> byNode)
    {
        // hướng chuẩn: theo +X (nếu dầm chủ yếu theo X) hoặc +Y
        if (Math.Abs(u.X) >= Math.Abs(u.Y)) { if (u.X < 0) u = u * -1; }
        else if (u.Y < 0) u = u * -1;
        var allNodes = chain.SelectMany(f => new[] { f.NodeI, f.NodeJ }).Distinct().Select(id => p.Node(id)).ToList();
        var o = allNodes.OrderBy(n => n.P.Dot(u)).First().P;
        var li = new LineInfo { Story = chain[0].Story ?? "", Group = chain[0].DesignGroup ?? "", Sec = sec, U = u, Origin = o, Z = allNodes.Max(n => n.Z) };
        foreach (var f in chain)
        {
            double ti = (p.Node(f.NodeI).P - o).Dot(u), tj = (p.Node(f.NodeJ).P - o).Dot(u);
            li.Segs.Add(new Seg { F = f, Reversed = ti > tj, X0 = Math.Min(ti, tj), Len = Math.Abs(tj - ti) });
        }
        li.Segs = li.Segs.OrderBy(s => s.X0).ToList();

        // nút dọc dải
        var nodeSeq = new List<int> { li.Segs[0].Reversed ? li.Segs[0].F.NodeJ : li.Segs[0].F.NodeI };
        foreach (var s in li.Segs) nodeSeq.Add(s.Reversed ? s.F.NodeI : s.F.NodeJ);
        var lineIds = new HashSet<int>(chain.Select(f => f.Id));

        SpanInfo cur = null;
        for (int k = 0; k < li.Segs.Count; k++)
        {
            var s = li.Segs[k];
            if (cur == null)
            {
                cur = new SpanInfo { X0 = s.X0, NodeL = nodeSeq[k] };
                cur.WL = SupportWidth(p, nodeSeq[k], u, sec.H, lineIds, byNode, out cur.SupL);
                cur.NameL = SupportName(p, nodeSeq[k], li, byNode);
            }
            cur.Segs.Add(s);
            int nr = nodeSeq[k + 1];
            double w = SupportWidth(p, nr, u, sec.H, lineIds, byNode, out bool sup);
            if (k == li.Segs.Count - 1 || sup)
            {
                cur.NodeR = nr; cur.WR = w; cur.SupR = sup;
                cur.NameR = SupportName(p, nr, li, byNode);
                cur.L = s.X0 + s.Len - cur.X0;
                li.Spans.Add(cur);
                cur = null;
            }
        }
        return li;
    }

    /// <summary>Dải dầm cong một phần tử: X dọc dải = chiều dài cung từ đầu I; bề rộng gối lấy theo tiếp tuyến cung tại mỗi đầu.</summary>
    private static LineInfo MakeCurvedLine(Project p, FrameElement f, FrameSection sec, Dictionary<int, List<FrameElement>> byNode)
    {
        var arc = CurvedBeam.Arc(p, f);
        if (arc == null) return null;
        var ni = p.Node(f.NodeI); var nj = p.Node(f.NodeJ);
        var u = (nj.P - ni.P).Normalize();
        var li = new LineInfo { Story = f.Story ?? "", Group = f.DesignGroup ?? "", Sec = sec, U = u, Origin = ni.P, Z = Math.Max(ni.Z, nj.Z), Arc = arc };
        li.Segs.Add(new Seg { F = f, Reversed = false, X0 = 0, Len = arc.Length });
        var ids = new HashSet<int> { f.Id };
        var sp = new SpanInfo { X0 = 0, L = arc.Length, NodeL = f.NodeI, NodeR = f.NodeJ };
        sp.Segs.Add(li.Segs[0]);
        sp.WL = SupportWidth(p, f.NodeI, arc.TangentAt(0), sec.H, ids, byNode, out sp.SupL);
        sp.WR = SupportWidth(p, f.NodeJ, arc.TangentAt(1), sec.H, ids, byNode, out sp.SupR);
        sp.NameL = SupportName(p, f.NodeI, li, byNode);
        sp.NameR = SupportName(p, f.NodeJ, li, byNode);
        li.Spans.Add(sp);
        return li;
    }

    /// <summary>Mô men xoắn bao lớn nhất |T| trong đoạn giữa hai mép gối của nhịp (kể cả tại mép gối).</summary>
    private static double SpanTorsion(CaseResult cr, SpanInfo sp)
    {
        double tm = 0;
        foreach (var s in sp.Segs)
        {
            if (!cr.Frames.TryGetValue(s.F.Id, out var ff)) continue;
            for (int i = 0; i < ff.Count; i++)
            {
                double st = Math.Clamp(ff.Stations[i], 0, s.Len);
                double x = s.X0 - sp.X0 + (s.Reversed ? s.Len - st : st);
                if (x >= sp.FaceL - 1e-9 && x <= sp.FaceR + 1e-9) tm = Math.Max(tm, Math.Abs(ff.T(i)));
            }
            double xl = sp.FaceL - (s.X0 - sp.X0), xr = sp.FaceR - (s.X0 - sp.X0);
            if (xl >= 0 && xl <= s.Len) tm = Math.Max(tm, Math.Abs(ff.At(s.Reversed ? s.Len - xl : xl)[3]));
            if (xr >= 0 && xr <= s.Len) tm = Math.Max(tm, Math.Abs(ff.At(s.Reversed ? s.Len - xr : xr)[3]));
        }
        return tm;
    }

    /// <summary>Bề rộng gối theo phương dầm tại nút: cột (H nếu trục 2 cột song song dầm, ngược lại B), hoặc bề rộng dầm giao
    /// (chỉ coi dầm giao là gối khi nó cao không kém dầm đang xét – dầm chính đỡ dầm phụ).</summary>
    private static double SupportWidth(Project p, int nodeId, Vec3 u, double ownH, HashSet<int> lineIds, Dictionary<int, List<FrameElement>> byNode, out bool isSupport)
    {
        isSupport = false;
        if (!byNode.TryGetValue(nodeId, out var fs)) return 0;
        double w = 0;
        foreach (var c in fs.Where(f => f.IsVerticalKind))
        {
            isSupport = true;
            try { w = Math.Max(w, DesignUtil.ColumnDimAlong(p, c, u)); } catch (KeyNotFoundException) { }
        }
        if (isSupport) return w;
        foreach (var bm in fs.Where(f => !lineIds.Contains(f.Id) && !f.IsVerticalKind))
        {
            if (Parallel(u, Dir(p, bm))) continue;
            FrameSection bs;
            try { bs = p.Section(bm.Section); } catch (KeyNotFoundException) { continue; }
            if (bs.Shape == SectionShape.RectConcrete && bs.H < ownH - 1e-6) continue;   // dầm phụ gác vào dầm này
            isSupport = true;
            w = Math.Max(w, bs.Shape == SectionShape.RectConcrete ? bs.B : 0);
        }
        if (!isSupport && p.Node(nodeId).IsSupport) isSupport = true;
        return w;
    }

    private static string SupportName(Project p, int nodeId, LineInfo li, Dictionary<int, List<FrameElement>> byNode)
    {
        var n = p.Node(nodeId);
        string g = li.AlongX ? DesignUtil.NearestGridName(p.GridsX, n.X) : DesignUtil.NearestGridName(p.GridsY, n.Y);
        if (g != null) return g;
        if (byNode.TryGetValue(nodeId, out var fs))
        {
            var c = fs.FirstOrDefault(f => f.IsVerticalKind);
            if (c != null && !string.IsNullOrEmpty(c.Label)) return c.Label;
        }
        return "N" + nodeId;
    }

    // ------------------------------------------------------------------
    // Nội lực dọc nhịp
    // ------------------------------------------------------------------
    private static List<(double X, double M, double V)> SpanSeries(CaseResult cr, SpanInfo sp)
    {
        var list = new List<(double X, double M, double V)>();
        foreach (var s in sp.Segs)
        {
            if (!cr.Frames.TryGetValue(s.F.Id, out var ff)) continue;
            for (int i = 0; i < ff.Count; i++)
            {
                double st = Math.Clamp(ff.Stations[i], 0, s.Len);
                double xl = s.Reversed ? s.Len - st : st;
                list.Add((s.X0 - sp.X0 + xl, ff.M3(i), ff.V2(i)));
            }
        }
        return list.OrderBy(t => t.X).ToList();
    }

    private static double Interp(List<(double X, double M, double V)> s, double x, bool moment)
    {
        if (s.Count == 0) return 0;
        if (x <= s[0].X) return moment ? s[0].M : s[0].V;
        for (int i = 1; i < s.Count; i++)
            if (x <= s[i].X + 1e-9)
            {
                double dx = s[i].X - s[i - 1].X;
                double t = dx < 1e-12 ? 1 : (x - s[i - 1].X) / dx;
                double a = moment ? s[i - 1].M : s[i - 1].V, b = moment ? s[i].M : s[i].V;
                return a + t * (b - a);
            }
        return moment ? s[^1].M : s[^1].V;
    }

    // ------------------------------------------------------------------
    // Thiết kế một dải dầm
    // ------------------------------------------------------------------
    private static BeamLineDesign DesignLine(Project p, AnalysisResults r, LineInfo li, List<string> combos, List<string> sls)
    {
        var cs = p.Concrete;
        var sec = li.Sec;
        var conc = DesignUtil.ConcreteOf(p, sec.Material, cs.ConcreteBeam);
        var main = p.Material(cs.RebarMain);
        var stl = p.Material(cs.RebarStirrup);
        double b = sec.B, h = sec.H, c = cs.CoverBeam;
        double a = c + 0.010, h0 = h - a;
        var pref = p.RebarPrefs ?? new RebarPreferences();
        int[] dias = pref.Filter(DesignUtil.Sorted(cs.BeamBars, new[] { 14, 16, 18, 20, 22, 25 }));
        int[] sdias = DesignUtil.Sorted(cs.StirrupBars, new[] { 6, 8, 10 }).Where(x => x >= 6).ToArray();
        if (sdias.Length == 0) sdias = new[] { 6 };
        if (pref.StirrupDia >= 6) sdias = new[] { pref.StirrupDia };
        var fitBot = new BeamFit { Top = false, MaxLayers = pref.AllowTwoLayers ? 2 : 1, Cap = Math.Max(pref.MaxBarsPerLayer, 0), SameDia = pref.PreferSameDia };
        var fitTop = new BeamFit { Top = true, MaxLayers = fitBot.MaxLayers, Cap = fitBot.Cap, SameDia = fitBot.SameDia };
        string key = MemberKey(li), basis = Basis(b, h);
        double Anchor(int dia) => Math.Max(30 * dia / 1000.0, RebarDetailing.AnchorageLength(conc, main, dia));

        var d = new BeamLineDesign
        {
            Story = li.Story, Section = sec.Name, B = b, H = h, TopElevation = li.Z,
            Frames = li.Segs.Select(s => s.F.Id).ToList()
        };
        d.RebarKeys.Add(key);
        int ns = li.Spans.Count;
        if (h0 <= 0) throw new InvalidOperationException("chiều cao dầm nhỏ hơn lớp bảo vệ");

        // ---- bao nội lực TTGH I tại mép gối và trong nhịp ----
        foreach (var sp in li.Spans)
        {
            var res = new BeamSpanResult
            {
                Frame = sp.Segs[0].F.Id, LeftSupport = sp.NameL, RightSupport = sp.NameR,
                L = sp.L, Lclear = sp.Lclear, SupportWidthLeft = sp.WL, SupportWidthRight = sp.WR
            };
            d.Spans.Add(res);
        }
        foreach (string cn in combos)
        {
            var cr = r.Get(cn);
            for (int k = 0; k < ns; k++)
            {
                var sp = li.Spans[k]; var res = d.Spans[k];
                var ser = SpanSeries(cr, sp);
                if (ser.Count == 0) continue;
                double mL = Interp(ser, sp.FaceL, true), mR = Interp(ser, sp.FaceR, true);
                if (mL < res.MLeft) { res.MLeft = mL; res.ComboMLeft = cn; }
                if (mR < res.MRight) { res.MRight = mR; res.ComboMRight = cn; }
                double mS = Math.Max(mL, mR);
                foreach (var t in ser) if (t.X >= sp.FaceL - 1e-9 && t.X <= sp.FaceR + 1e-9) mS = Math.Max(mS, t.M);
                if (mS > res.MSpan) { res.MSpan = mS; res.ComboMSpan = cn; }
                double q = Math.Max(Math.Abs(Interp(ser, sp.FaceL + 1e-6, false)), Math.Abs(Interp(ser, sp.FaceR - 1e-6, false)));
                if (q > res.QMax) { res.QMax = q; res.ComboQ = cn; }
                if (li.Arc != null)
                {
                    double tq = SpanTorsion(cr, sp);
                    if (tq > res.TMax) { res.TMax = tq; res.ComboT = cn; }
                }
            }
        }
        if (li.Arc != null)
        {
            d.IsCurved = true;
            d.CurveRadius = li.Arc.Radius; d.ArcLength = li.Arc.Length; d.Chord = li.Arc.Chord; d.Sagitta = Math.Abs(li.Arc.Sagitta);
            d.TorsionMax = d.Spans.Max(x => x.TMax);
        }

        // ---- thép dọc yêu cầu ----
        var fl = new FlexureResult[ns]; var fs = new FlexureResult[ns]; var fr = new FlexureResult[ns];
        for (int k = 0; k < ns; k++)
        {
            var res = d.Spans[k];
            fl[k] = RcSection.DesignRect(res.MLeft, b, h0, a, conc, main, cs.MinRatioBeam);
            fs[k] = RcSection.DesignRect(res.MSpan, b, h0, a, conc, main, cs.MinRatioBeam);
            fr[k] = RcSection.DesignRect(res.MRight, b, h0, a, conc, main, cs.MinRatioBeam);
            if (li.Arc != null && res.TMax > 1e-6)
            {
                // dầm cong: cốt dọc chịu xoắn Asl – ½ cộng vào thép trên, ½ vào thép dưới (RcTorsion)
                double asl = RcTorsion.LongitudinalArea(res.TMax, b, h, c, main);
                res.AsTorsion = asl;
                fl[k].As += asl / 2; fs[k].As += asl / 2; fr[k].As += asl / 2;
            }
            res.AsTopLeftReq = fl[k].As; res.AsBotReq = fs[k].As; res.AsTopRightReq = fr[k].As;
            foreach (var f in new[] { fl[k], fs[k], fr[k] })
                if (!string.IsNullOrEmpty(f.Note)) d.Messages.Add("Nhịp " + (k + 1) + ": " + f.Note);
        }
        var supReq = new double[ns + 1];
        var supComp = new double[ns + 1];
        for (int j = 0; j <= ns; j++)
        {
            double rl = j > 0 ? fr[j - 1].As : 0, rr = j < ns ? fl[j].As : 0;
            supReq[j] = Math.Max(rl, rr);
            supComp[j] = Math.Max(j > 0 ? fr[j - 1].AsComp : 0, j < ns ? fl[j].AsComp : 0);
        }
        // gối tự do (đầu công xôn): không cần mũ gối
        if (!li.Spans[0].SupL) supReq[0] = 0;
        if (!li.Spans[ns - 1].SupR) supReq[ns] = 0;

        double xEnd = li.Spans[ns - 1].X0 + li.Spans[ns - 1].L;
        double[] lcl = li.Spans.Select(s => s.Lclear).ToArray();
        double[] nodeX = new double[ns + 1];
        double[] nodeW = new double[ns + 1];
        for (int j = 0; j < ns; j++) { nodeX[j] = li.Spans[j].X0; nodeW[j] = li.Spans[j].WL; }
        nodeX[ns] = xEnd; nodeW[ns] = li.Spans[ns - 1].WR;
        for (int j = 1; j < ns; j++) nodeW[j] = Math.Max(li.Spans[j - 1].WR, li.Spans[j].WL);

        // ---- chọn thép chạy suốt + tăng cường / mũ gối (có xét lựa chọn của người dùng – Project.RebarChoices) ----
        double compSup = supComp.Max(), compSpan = fs.Max(f => f.AsComp);
        d.SupportAsReq = supReq.ToList(); d.ContBotAsReq = compSup; d.ContTopAsReq = compSpan;
        List<(int Dia, int Count)> Ovr(string zone, int idx)
        {
            var ch = p.ValidRebarChoice(key, zone, idx, basis);
            var g = ch?.Bars?.Where(x => x != null && x.Dia > 0 && x.Count > 0).Select(x => (x.Dia, x.Count)).ToList();
            return g != null && g.Count > 0 ? g : null;
        }
        var ovBot = Enumerable.Range(0, ns).Select(k => Ovr(RebarZones.BeamBottom, k)).ToArray();
        var ovTop = Enumerable.Range(0, ns + 1).Select(j => Ovr(RebarZones.BeamTopSupport, j)).ToArray();
        var ovBotC = Ovr(RebarZones.BeamBotCont, -1);
        var ovTopC = Ovr(RebarZones.BeamTopCont, -1);
        int nUser = ovBot.Count(x => x != null) + ovTop.Count(x => x != null) + (ovBotC != null ? 1 : 0) + (ovTopC != null ? 1 : 0);
        int dB = ChooseContinuous(dias, b, c, compSup, xEnd, k => fs[k].As, k => 0.8 * lcl[k], ns, out var extraBot,
            fitBot, ovBotC?.Max(g => g.Dia) ?? 0, ovBot, out var adjBot);
        int dT = ChooseContinuous(dias, b, c, compSpan, xEnd, j => supReq[j], j => SupportBarSpan(j, ns, nodeW, lcl), ns + 1, out var extraTop,
            fitTop, ovTopC?.Max(g => g.Dia) ?? 0, ovTop, out var adjTop);
        if (ovBotC != null && ovBotC.Sum(g => g.Count) != 2) d.Messages.Add("Thép dưới chạy suốt luôn gồm 2 thanh góc – lấy 2Ø" + dB);
        if (ovTopC != null && ovTopC.Sum(g => g.Count) != 2) d.Messages.Add("Thép trên chạy suốt luôn gồm 2 thanh góc – lấy 2Ø" + dT);
        for (int k = 0; k < ns; k++)
            if (adjBot[k]) d.Messages.Add("Nhịp " + (k + 1) + ": thép dưới chọn " + RcSection.BarText(ovBot[k]) + " không có 2 thanh Ø" + dB
                                          + " chạy suốt – đã điều chỉnh thành " + RcSection.BarText(new[] { (dB, 2) }.Concat(extraBot[k].Groups)));
        for (int j = 0; j <= ns; j++)
            if (adjTop[j]) d.Messages.Add("Gối " + (j + 1) + ": thép trên chọn " + RcSection.BarText(ovTop[j]) + " không có 2 thanh Ø" + dT
                                          + " chạy suốt – đã điều chỉnh thành " + RcSection.BarText(new[] { (dT, 2) }.Concat(extraTop[j].Groups)));
        double aCont = 2 * RcSection.BarArea(dB), aTop = 2 * RcSection.BarArea(dT);
        if (aCont < compSup * (1 - 1e-6)) d.Messages.Add("Thép dưới chạy suốt chưa đủ làm cốt chịu nén tại gối – kiểm tra lại");
        if (aTop < compSpan * (1 - 1e-6)) d.Messages.Add("Thép trên chạy suốt chưa đủ làm cốt chịu nén giữa nhịp – kiểm tra lại");

        // ---- cốt đai ----
        int sd = sdias[0];
        if (h > 0.8) sd = Math.Max(sd, sdias.FirstOrDefault(x => x >= 8, sdias[^1]));
        foreach (int dd in sdias)
        {
            if (dd < sd) continue;
            bool ok = d.Spans.All(res =>
            {
                var sh = RcSection.Shear(res.QMax, b, h0, conc, stl, dd);
                return sh.Ok && (pref.MinStirrupSpacing <= 0 || sh.SSupport >= pref.MinStirrupSpacing - 1e-9);
            });
            sd = dd;
            if (ok) break;
        }
        var shear = d.Spans.Select(res => RcSection.Shear(res.QMax, b, h0, conc, stl, sd)).ToArray();
        for (int k = 0; k < ns; k++) ClampStirrups(ref shear[k], pref);
        var sdk = Enumerable.Repeat(sd, ns).ToArray();
        var userShear = new bool[ns];
        if (li.Arc == null)
            for (int k = 0; k < ns; k++)
            {
                var ch = p.ValidRebarChoice(key, RebarZones.Stirrup, k, basis);
                if (ch == null || ch.Dia < 6 || ch.Spacing <= 0) continue;
                sdk[k] = ch.Dia;
                var sh = RcSection.Shear(d.Spans[k].QMax, b, h0, conc, stl, ch.Dia);
                sh.SSupport = ch.Spacing;
                sh.SMid = Math.Max(ch.Spacing2 > 0 ? ch.Spacing2 : sh.SMid, ch.Spacing);
                shear[k] = sh;
                userShear[k] = true;
                nUser++;
            }
        else if (Enumerable.Range(0, ns).Any(k => p.ValidRebarChoice(key, RebarZones.Stirrup, k, basis) != null))
            d.Messages.Add("Dầm cong: bỏ qua lựa chọn đai của người dùng – đai kín tính theo xoắn + cắt");
        TorsionResult[] tor = null;
        if (li.Arc != null)
        {
            // dầm cong: đai kín chịu xoắn + cắt – tăng Ø đai nếu bước yêu cầu < 100 mm
            foreach (int dd in sdias)
            {
                if (dd < sd) continue;
                var sh2 = d.Spans.Select(res => RcSection.Shear(res.QMax, b, h0, conc, stl, dd)).ToArray();
                var t2 = d.Spans.Select((res, k) => RcTorsion.Design(res.TMax, res.QMax, b, h, h0, c, conc, stl, dd, sh2[k].SSupport, main)).ToArray();
                sd = dd; shear = sh2; tor = t2;
                if (t2.All(t => t.Ok || !t.StrutOk)) break;
            }
            for (int k = 0; k < ns; k++)
            {
                shear[k].SSupport = Math.Min(shear[k].SSupport, tor[k].S);
                shear[k].SMid = Math.Max(Math.Min(shear[k].SMid, tor[k].S), shear[k].SSupport);
                tor[k] = RcTorsion.Design(d.Spans[k].TMax, d.Spans[k].QMax, b, h, h0, c, conc, stl, sd, shear[k].SSupport, main);
            }
            for (int k = 0; k < ns; k++) sdk[k] = sd;
        }

        // ---- kết quả từng nhịp + kiểm tra ----
        double I = b * h * h * h / 12.0;
        double[] mSls = new double[ns];
        double[] mSlsSup = new double[ns];
        double[] mDefl = new double[ns];   // Mgiữa − 0,1·(|M trái| + |M phải|) của cùng tổ hợp – dầm liên tục
        foreach (string cn in sls)
        {
            var cr = r.Get(cn);
            for (int k = 0; k < ns; k++)
            {
                var ser = SpanSeries(cr, li.Spans[k]);
                double mmax = 0;
                foreach (var t in ser) { mSls[k] = Math.Max(mSls[k], t.M); mmax = Math.Max(mmax, t.M); }
                if (ser.Count > 0)
                {
                    mSlsSup[k] = Math.Max(mSlsSup[k], Math.Max(-ser[0].M, -ser[^1].M));
                    double ml = Math.Max(0, -ser[0].M), mr = Math.Max(0, -ser[^1].M);
                    mDefl[k] = Math.Max(mDefl[k], mmax - 0.1 * (ml + mr));
                }
            }
        }

        for (int k = 0; k < ns; k++)
        {
            var res = d.Spans[k]; var sp = li.Spans[k];
            var userNames = new List<string>();
            var botGroups = new List<(int Dia, int Count)> { (dB, 2) };
            botGroups.AddRange(extraBot[k].Groups);
            var topLG = new List<(int Dia, int Count)> { (dT, 2) };
            topLG.AddRange(extraTop[k].Groups);
            var topRG = new List<(int Dia, int Count)> { (dT, 2) };
            topRG.AddRange(extraTop[k + 1].Groups);
            res.AsBotProv = aCont + extraBot[k].Area;
            res.AsTopLeftProv = aTop + extraTop[k].Area;
            res.AsTopRightProv = aTop + extraTop[k + 1].Area;
            res.BotText = RcSection.BarText(botGroups);
            res.TopLeftText = RcSection.BarText(topLG);
            res.TopRightText = RcSection.BarText(topRG);
            res.StirrupDia = sdk[k];
            res.StirrupSupport = shear[k].SSupport;
            res.StirrupMid = shear[k].SMid;

            // h0 thực tế theo số hàng thép
            double h0b = h - EffA(c, botGroups, b, fitBot);
            double h0tl = h - EffA(c, topLG, b, fitTop), h0tr = h - EffA(c, topRG, b, fitTop);
            double muL = RcSection.MomentCapacity(b, h0tl, res.AsTopLeftProv, aCont, a, conc, main);
            double muS = RcSection.MomentCapacity(b, h0b, res.AsBotProv, aTop, a, conc, main);
            double muR = RcSection.MomentCapacity(b, h0tr, res.AsTopRightProv, aCont, a, conc, main);
            string sk = " nhịp " + (k + 1);
            if (ovTop[k] != null || ovTopC != null) userNames.Add("Uốn gối trái" + sk);
            if (ovBot[k] != null || ovBotC != null) userNames.Add("Uốn giữa nhịp" + sk);
            if (ovTop[k + 1] != null || ovTopC != null) userNames.Add("Uốn gối phải" + sk);
            if (userShear[k]) userNames.Add("Cắt" + sk);
            d.Checks.Add(new CheckItem { Name = "Uốn gối trái" + sk, Demand = Math.Abs(res.MLeft), Capacity = muL, Unit = "kN.m", Note = res.TopLeftText + " – " + res.ComboMLeft });
            d.Checks.Add(new CheckItem { Name = "Uốn giữa nhịp" + sk, Demand = res.MSpan, Capacity = muS, Unit = "kN.m", Note = res.BotText + " – " + res.ComboMSpan });
            d.Checks.Add(new CheckItem { Name = "Uốn gối phải" + sk, Demand = Math.Abs(res.MRight), Capacity = muR, Unit = "kN.m", Note = res.TopRightText + " – " + res.ComboMRight });
            double qu = RcSection.ShearCapacity(b, h0, conc, stl, sdk[k], 2, shear[k].SSupport);
            d.Checks.Add(new CheckItem
            {
                Name = "Cắt" + sk, Demand = res.QMax, Capacity = qu, Unit = "kN",
                Note = "Ø" + sdk[k] + "a" + DesignUtil.F(shear[k].SSupport * 1000, "0") + " – " + res.ComboQ + (string.IsNullOrEmpty(shear[k].Note) ? "" : "; " + shear[k].Note)
            });
            if (!shear[k].Ok && !userShear[k]) d.Messages.Add("LỖI" + sk + ": " + shear[k].Note);
            foreach (var ci in d.Checks.Where(q => userNames.Contains(q.Name) && !q.Ok).ToList())
                d.Messages.Add("LỖI bố trí thép do người dùng chọn không đạt – " + ci.Name + ": " + ci.Note);
            if (tor != null)
            {
                var tr = tor[k];
                d.Checks.Add(new CheckItem
                {
                    Name = "Xoắn – dải bê tông chịu nén" + sk, Demand = tr.T, Capacity = tr.TStrutReduced, Unit = "kN.m",
                    Note = "T ≤ 0,1·Rb·b²·h·(1 − Q/0,3Rb·b·h0) = " + DesignUtil.F(tr.TStrutReduced, "0.0") + " – " + res.ComboT
                });
                d.Checks.Add(new CheckItem
                {
                    Name = "Xoắn + cắt – cốt đai kín" + sk, Demand = tr.T, Capacity = tr.TswCapacity, Unit = "kN.m",
                    Note = "Đai kín Ø" + sd + "a" + DesignUtil.F(tr.S * 1000, "0") + ": Tsw = 2·Ak·Rsw·(Asw1/s − qsw/2Rsw), Ak = " + DesignUtil.F(tr.Ak * 1e4, "0") + " cm²"
                });
                // phần thép dọc còn dư sau khi chịu uốn tại các tiết diện có yêu cầu (bỏ qua thép trên ở đầu tự do công xôn: T → 0)
                double spare = res.AsBotProv - (res.AsBotReq - res.AsTorsion / 2);
                if (k > 0 || sp.SupL) spare = Math.Min(spare, res.AsTopLeftProv - (res.AsTopLeftReq - res.AsTorsion / 2));
                if (k < ns - 1 || sp.SupR) spare = Math.Min(spare, res.AsTopRightProv - (res.AsTopRightReq - res.AsTorsion / 2));
                d.Checks.Add(new CheckItem
                {
                    Name = "Xoắn – cốt dọc" + sk, Demand = res.AsTorsion * 1e4, Capacity = Math.Max(0, spare * 2 * 1e4),
                    Unit = "cm²", Note = "Asl = T·uk/(2·Ak·Rs), ½ ở thép trên + ½ ở thép dưới (cộng vào thép chịu uốn)"
                });
                if (!tr.Ok && !string.IsNullOrEmpty(tr.Note)) d.Messages.Add("LỖI" + sk + " (xoắn): " + tr.Note);
            }

            // độ võng ước lượng
            bool cantilever = (k == 0 && !sp.SupL) || (k == ns - 1 && !sp.SupR);
            double f;
            if (cantilever)
            {
                double Bc = RcSection.FlexuralStiffness(b, h, h0, Math.Max(res.AsTopLeftProv, res.AsTopRightProv), conc, main.E, mSlsSup[k]);
                f = mSlsSup[k] * sp.L * sp.L / (4 * Bc);
            }
            else
            {
                double B = RcSection.FlexuralStiffness(b, h, h0b, res.AsBotProv, conc, main.E, mSls[k]);
                f = 5.0 / 48.0 * Math.Max(mDefl[k], 0) * sp.L * sp.L / B;
            }
            double lim = (cantilever ? 2 * sp.L : sp.L) / Math.Max(cs.BeamDeflectionLimit, 1);
            res.Deflection = f; res.DeflectionLimit = lim;
            d.Checks.Add(new CheckItem
            {
                Name = "Võng" + sk, Demand = f * 1000, Capacity = lim * 1000, Unit = "mm",
                Note = cantilever ? "Công xôn: f = M·L²/(4·B), B = Eb/(1+φcr)·Ieff (kể nứt + từ biến)" : "f = 5/48·[Mg − 0,1(Mt + Mp)]·L²/B, B = Eb/(1+φcr)·Ieff (kể nứt + từ biến)"
            });
        }
        if (extraBot.Concat(extraTop).Any(e => !e.Ok))
            d.Messages.Add("LỖI không bố trí đủ thép dọc trong bề rộng dầm b = " + DesignUtil.F(b * 1000, "0") + " mm"
                           + (nUser > 0 ? " (kiểm tra lại thép chọn: khoảng hở ≥ max(d; 25 mm) thép dưới, max(d; 30 mm) thép trên)" : " – cần tăng tiết diện"));
        d.UserRebarZones = nUser;
        if (nUser > 0) d.Messages.Add("Bố trí thép theo lựa chọn của người dùng tại " + nUser + " vùng (thiết kế lại đã kiểm tra khả năng chịu lực với thép chọn)");

        if (li.Arc != null)
            d.Messages.Add(CurvedBeam.Describe(li.Arc) + ". Mặt đứng dầm thể hiện trải theo chiều dài cung; chiều dài thép dọc tính theo cung. "
                           + "Bắt buộc dùng ĐAI KÍN (móc 135°, đoạn móc ≥ 6d) suốt dầm; thép dọc chịu xoắn neo như thép chịu kéo vào gối"
                           + (h > 0.45 ? "; đặt thêm thép giá cạnh ≥ Ø12 a ≤ 300 mm." : "."));

        // ---- bố trí thép ----
        Detail(d, li, cs, b, h, c, dB, dT, extraBot, extraTop, shear, sdk, nodeX, nodeW, lcl, xEnd, Anchor, pref, conc, main);
        RebarNumbering.Renumber(d.Bars);
        foreach (var z in d.StirrupZones) z.Mark = RebarNumbering.StirrupMark(d.Bars, z.Dia);
        return d;
    }

    /// <summary>Khoảng cách từ mép chịu kéo tới trọng tâm nhóm thép (1 hoặc 2 hàng, khe hở hàng 25 mm).</summary>
    private static double EffA(double cover, List<(int Dia, int Count)> groups, double b, BeamFit fit = null)
    {
        int dmax = groups.Max(g => g.Dia);
        int total = groups.Sum(g => g.Count);
        int per = Math.Max(RcSection.MaxBarsInLayer(b, cover, dmax, fit != null && fit.Top ? RcSection.ClearTop : RcSection.ClearBottom), 1);
        if (fit != null && fit.Cap > 0) per = Math.Max(Math.Min(per, fit.Cap), 1);
        double dm = dmax / 1000.0;
        if (total <= per) return cover + dm / 2;
        // hai hàng: trọng tâm gần đúng theo số thanh
        int n2 = total - per;
        double y1 = cover + dm / 2, y2 = cover + dm + 0.025 + dm / 2;
        return (per * y1 + n2 * y2) / total;
    }

    /// <summary>Chiều dài làm việc của mũ gối thứ j (dùng để so sánh khối lượng khi chọn Ø).</summary>
    private static double SupportBarSpan(int j, int ns, double[] nodeW, double[] lcl)
    {
        if (j == 0) return nodeW[0] + lcl[0] / 4;
        if (j == ns) return nodeW[ns] + lcl[ns - 1] / 4;
        return nodeW[j] + lcl[j - 1] / 3 + lcl[j] / 3;
    }

    /// <summary>Điều kiện bố trí thép một phía dầm: khoảng hở theo vị trí (trên/dưới), số hàng, giới hạn số thanh một hàng.</summary>
    internal sealed class BeamFit
    {
        public bool Top;
        public int MaxLayers = 2;
        public int Cap;
        public bool SameDia;
        public double Clear => Top ? RcSection.ClearTop : RcSection.ClearBottom;
    }

    /// <summary>Khoá cấu kiện vật lý của dải dầm (lựa chọn thép): "D:{id phần tử nhỏ nhất}".</summary>
    internal static string MemberKey(LineInfo li) => "D:" + li.Segs.Min(sg => sg.F.Id).ToString(DesignUtil.Inv);

    /// <summary>Kích thước tiết diện dùng làm cơ sở lựa chọn thép ("220x400").</summary>
    internal static string Basis(double b, double h) => DesignUtil.Mm(b).ToString("0", DesignUtil.Inv) + "x" + DesignUtil.Mm(h).ToString("0", DesignUtil.Inv);

    /// <summary>Giới hạn bước đai theo tuỳ chọn người dùng (nhỏ nhất / lớn nhất).</summary>
    private static void ClampStirrups(ref ShearResult sh, RebarPreferences pref)
    {
        if (pref.MaxStirrupSpacing > 0)
        {
            sh.SSupport = Math.Min(sh.SSupport, pref.MaxStirrupSpacing);
            sh.SMid = Math.Min(sh.SMid, pref.MaxStirrupSpacing);
        }
        if (pref.MinStirrupSpacing > 0)
        {
            sh.SSupport = Math.Max(sh.SSupport, pref.MinStirrupSpacing);
            sh.SMid = Math.Max(sh.SMid, sh.SSupport);
        }
    }

    /// <summary>Phần thép bổ sung của vùng do người dùng chọn tổng số thanh G khi đã có 2 thanh góc chạy suốt Ø dC:
    /// G − 2·Ø dC; nếu G không có đủ 2 thanh Ø dC thì bớt 2 thanh có Ø gần dC nhất (adjusted = true).</summary>
    internal static BarSet Remainder(List<(int Dia, int Count)> g, int dC, double b, double cover, BeamFit fit, out bool adjusted)
    {
        adjusted = false;
        var list = g.GroupBy(x => x.Dia).Select(x => (Dia: x.Key, Count: x.Sum(y => y.Count))).OrderByDescending(x => x.Dia).ToList();
        int i = list.FindIndex(x => x.Dia == dC);
        if (i >= 0 && list[i].Count >= 2) list[i] = (dC, list[i].Count - 2);
        else
        {
            adjusted = true;
            for (int t = 0; t < 2; t++)
            {
                int j = -1;
                for (int q = 0; q < list.Count; q++)
                {
                    if (list[q].Count <= 0) continue;
                    if (j < 0 || Math.Abs(list[q].Dia - dC) < Math.Abs(list[j].Dia - dC)) j = q;
                }
                if (j < 0) break;
                list[j] = (list[j].Dia, list[j].Count - 1);
            }
        }
        var groups = list.Where(x => x.Count > 0).Select(x => (x.Dia, x.Count)).ToList();
        var all = new List<(int Dia, int Count)> { (dC, 2) };
        all.AddRange(groups);
        var fitRes = RebarOptions.CheckFit(all, b, cover, fit.Top, fit.MaxLayers, fit.Cap);
        return new BarSet { Groups = groups, Layers = fitRes.Layers, Ok = fitRes.Fits };
    }

    /// <summary>Chọn Ø cho 2 thanh góc chạy suốt sao cho tổng khối lượng (chạy suốt + bổ sung) nhỏ nhất.
    /// forcedDia &gt; 0: Ø chạy suốt do người dùng chọn. ovr[k] ≠ null: tổng thép tại vị trí k do người dùng chọn → phần bổ sung = ovr − 2 thanh góc.</summary>
    private static int ChooseContinuous(int[] dias, double b, double cover, double minArea, double totalLen,
        Func<int, double> req, Func<int, double> extraLen, int count, out BarSet[] extras,
        BeamFit fit, int forcedDia, List<(int Dia, int Count)>[] ovr, out bool[] adjusted)
    {
        fit ??= new BeamFit();
        double best = double.MaxValue; int bestD = dias[0];
        BarSet[] bestEx = null;
        bool[] bestAdj = null;
        IEnumerable<int> cands = dias;
        if (forcedDia > 0) cands = new[] { forcedDia };
        else if (ovr != null && ovr.Any(o => o != null))
        {
            var ov = ovr.Where(o => o != null).SelectMany(o => o).ToList();
            var c2 = ov.GroupBy(x => x.Dia).Where(gp => gp.Sum(x => x.Count) >= 2).Select(gp => gp.Key).ToList();
            cands = (c2.Count > 0 ? c2 : ov.Select(x => x.Dia)).Distinct().OrderBy(x => x).ToArray();
        }
        foreach (int d in cands)
        {
            int per = RcSection.MaxBarsInLayer(b, cover, d, fit.Clear);
            if (fit.Cap > 0) per = Math.Min(per, fit.Cap);
            if (per < 2 && d != dias[0] && forcedDia <= 0) continue;
            double a2 = 2 * RcSection.BarArea(d);
            var ex = new BarSet[count];
            var adj = new bool[count];
            double score = a2 * totalLen;
            bool ok = true;
            for (int k = 0; k < count; k++)
            {
                if (ovr != null && k < ovr.Length && ovr[k] != null)
                    ex[k] = Remainder(ovr[k], d, b, cover, fit, out adj[k]);
                else
                {
                    double need = req(k) - a2;
                    ex[k] = need > 1e-9 ? RcSection.ChooseExtra(need, 2, d, b, cover, dias, fit.MaxLayers, fit.Clear, fit.Cap, fit.SameDia) : new BarSet();
                }
                if (!ex[k].Ok) ok = false;
                score += ex[k].Area * extraLen(k);
                if (adj[k]) score *= 1.5;
            }
            if (a2 < minArea * (1 - 1e-6)) score *= 3;
            if (!ok) score *= 10;
            if (score < best) { best = score; bestD = d; bestEx = ex; bestAdj = adj; }
        }
        extras = bestEx ?? Enumerable.Range(0, count).Select(_ => new BarSet()).ToArray();
        adjusted = bestAdj ?? new bool[count];
        return bestD;
    }

    /// <summary>Đoạn neo tại gối biên: (đoạn thẳng trong gối, đoạn bẻ xuống/lên). Neo tổng lan = max(30d; lan theo TCVN 5574:2018 10.3.5.5);
    /// đoạn bẻ ≥ 10d và ≤ h − 2c.</summary>
    private static (double straight, double leg) EndAnchor(double w, int d, double h, double c, double lan = 0)
    {
        double dm = d / 1000.0;
        if (w <= 1e-6) return (0, 0);
        double straight = Math.Max(w - c, 0);
        double leg = Math.Max(Math.Max(30 * dm, lan) - straight, 0);
        if (leg > 1e-6) leg = Math.Min(Math.Max(leg, 10 * dm), h - 2 * c);
        return (straight, DesignUtil.R10(leg));
    }

    private static BarMark Longitudinal(string role, bool top, int dia, int count, double x0, double x1, double legL, double legR, int span)
    {
        double horiz = DesignUtil.R10(x1 - x0);
        var segs = new List<double>();
        int legs = (legL > 1e-6 ? 1 : 0) + (legR > 1e-6 ? 1 : 0);
        if (legs == 2) { segs.Add(DesignUtil.Mm(legL)); segs.Add(DesignUtil.Mm(horiz)); segs.Add(DesignUtil.Mm(legR)); }
        else if (legs == 1) { segs.Add(DesignUtil.Mm(horiz)); segs.Add(DesignUtil.Mm(Math.Max(legL, legR))); }
        else segs.Add(DesignUtil.Mm(horiz));
        return new BarMark
        {
            Role = role, IsTop = top, Dia = dia, Count = count, Start = x0, End = x1, Span = span,
            Length = DesignUtil.R10(horiz + legL + legR), Shape = DesignUtil.ShapeOf(legs), Segments = segs
        };
    }

    /// <summary>Bố trí thép dải dầm: thép dưới/trên chạy suốt (2 thanh góc, neo 30d vào gối biên), tăng cường dưới 0,8·Lo giữa nhịp,
    /// mũ gối giữa vươn Lo/3 mỗi bên, mũ gối biên vươn Lo/4, giá cạnh khi h &gt; 700 mm, đai kín 2 nhánh và vùng đai.</summary>
    private static void Detail(BeamLineDesign d, LineInfo li, ConcreteDesignSettings cs, double b, double h, double c,
        int dB, int dT, BarSet[] extraBot, BarSet[] extraTop, ShearResult[] shear, int[] sdk,
        double[] nodeX, double[] nodeW, double[] lcl, double xEnd, Func<int, double> anchor, RebarPreferences pref,
        Material conc, Material main)
    {
        int ns = li.Spans.Count;
        bool supStart = li.Spans[0].SupL, supEnd = li.Spans[ns - 1].SupR;
        double xs = supStart ? -nodeW[0] / 2 + c : c;
        double xe = supEnd ? xEnd + nodeW[ns] / 2 - c : xEnd - c;

        // thép dưới chạy suốt
        var (_, lb0) = EndAnchor(supStart ? nodeW[0] : 0, dB, h, c, anchor(dB));
        var (_, lb1) = EndAnchor(supEnd ? nodeW[ns] : 0, dB, h, c, anchor(dB));
        d.Bars.Add(Longitudinal("Dưới", false, dB, 2, xs, xe, lb0, lb1, -1));
        // thép trên chạy suốt
        var (_, lt0) = EndAnchor(supStart ? nodeW[0] : 0, dT, h, c, anchor(dT));
        var (_, lt1) = EndAnchor(supEnd ? nodeW[ns] : 0, dT, h, c, anchor(dT));
        d.Bars.Add(Longitudinal("Trên", true, dT, 2, xs, xe, lt0, lt1, -1));
        double stock = pref.StockLength > 1 ? pref.StockLength : StockLength;
        if (xe - xs > stock)
        {
            double lapB = RebarDetailing.LapLength(conc, main, dB), lapT = RebarDetailing.LapLength(conc, main, dT);
            d.Messages.Add("Thép chạy suốt dài " + DesignUtil.F(xe - xs, "0.00") + " m > " + DesignUtil.F(stock, "0.0#") + " m – nối chồng thép trên tại giữa nhịp (ll = "
                           + DesignUtil.F(lapT * 1000, "0") + " mm), thép dưới tại gối (ll = " + DesignUtil.F(lapB * 1000, "0")
                           + " mm), so le ≥ 1,3·ll, không quá 50% số thanh tại một tiết diện – xem bản vẽ gia công thép");
        }

        // mũ gối (mỗi nhóm Ø một số hiệu)
        for (int j = 0; j <= ns; j++)
        {
            var ex = extraTop[j];
            if (ex.IsEmpty) continue;
            foreach (var (dia, n) in ex.Groups)
            {
                if (n <= 0) continue;
                if (j == 0)
                {
                    var (_, leg) = EndAnchor(supStart ? nodeW[0] : 0, dia, h, c, anchor(dia));
                    double x1 = li.Spans[0].X0 + li.Spans[0].FaceL + lcl[0] / 4;
                    d.Bars.Add(Longitudinal("Mũ gối", true, dia, n, xs, x1, leg, 0, 0));
                }
                else if (j == ns)
                {
                    var (_, leg) = EndAnchor(supEnd ? nodeW[ns] : 0, dia, h, c, anchor(dia));
                    var sp = li.Spans[ns - 1];
                    double x0 = sp.X0 + sp.FaceR - lcl[ns - 1] / 4;
                    d.Bars.Add(Longitudinal("Mũ gối", true, dia, n, x0, xe, 0, leg, ns - 1));
                }
                else
                {
                    var spl = li.Spans[j - 1]; var spr = li.Spans[j];
                    double x0 = spl.X0 + spl.FaceR - lcl[j - 1] / 3;
                    double x1 = spr.X0 + spr.FaceL + lcl[j] / 3;
                    d.Bars.Add(Longitudinal("Mũ gối", true, dia, n, x0, x1, 0, 0, j));
                }
            }
        }

        // tăng cường dưới
        for (int k = 0; k < ns; k++)
        {
            var ex = extraBot[k];
            if (ex.IsEmpty) continue;
            var sp = li.Spans[k];
            foreach (var (dia, n) in ex.Groups)
            {
                if (n <= 0) continue;
                double x0 = sp.X0 + sp.FaceL + 0.1 * lcl[k];
                double x1 = sp.X0 + sp.FaceR - 0.1 * lcl[k];
                d.Bars.Add(Longitudinal("Tăng cường dưới", false, dia, n, x0, x1, 0, 0, k));
            }
        }

        // cốt giá cạnh dầm cao (TCVN 5574:2018 mục 10.3.6: h > 700 mm)
        if (h > 0.7)
        {
            int pairs = (int)Math.Ceiling((h - 2 * c) / 0.4) - 1;
            d.Bars.Add(Longitudinal("Giá", false, 12, 2 * Math.Max(pairs, 1), xs, xe, 0, 0, -1));
        }

        // đai: kích thước theo mép ngoài đai, đai ôm ngoài thép dọc → lớp bảo vệ tới mép đai = c − Øđai
        var totals = new Dictionary<int, int>();
        var minS = new Dictionary<int, double>();
        var order = new List<int>();
        for (int k = 0; k < ns; k++)
        {
            var sp = li.Spans[k];
            int sd = sdk[k];
            double L0 = lcl[k];
            double ss = shear[k].SSupport, sm = shear[k].SMid;
            int nsup = (int)Math.Ceiling(L0 / 4 / ss - 1e-9);
            double zs = nsup * ss;
            if (2 * zs > L0) { zs = L0 / 2; nsup = (int)Math.Ceiling(zs / ss - 1e-9); }
            double mid = L0 - 2 * zs;
            int cnt;
            if (mid < 1e-6) cnt = 2 * nsup + 1;
            else cnt = 2 * (nsup + 1) + Math.Max((int)Math.Ceiling(mid / sm - 1e-9) - 1, 0);
            if (!totals.ContainsKey(sd)) { totals[sd] = 0; minS[sd] = double.MaxValue; order.Add(sd); }
            totals[sd] += cnt;
            minS[sd] = Math.Min(minS[sd], ss);
            double f0 = sp.X0 + sp.FaceL, f1 = sp.X0 + sp.FaceR;
            d.Spans[k].SupportZone = zs;
            d.StirrupZones.Add(new StirrupZone { Span = k, From = f0, To = f0 + zs, Dia = sd, Spacing = ss });
            if (mid > 1e-6) d.StirrupZones.Add(new StirrupZone { Span = k, From = f0 + zs, To = f1 - zs, Dia = sd, Spacing = sm });
            d.StirrupZones.Add(new StirrupZone { Span = k, From = f1 - zs, To = f1, Dia = sd, Spacing = ss });
        }
        foreach (int sd in order)
        {
            double cst = StirrupCover(c, sd);
            double s1 = b - 2 * cst, s2 = h - 2 * cst;
            d.Bars.Add(new BarMark
            {
                Role = "Đai", Dia = sd, Count = totals[sd], Shape = "Đai", Spacing = minS[sd] < double.MaxValue ? minS[sd] : 0.15,
                Length = DesignUtil.StirrupLength(s1, s2, sd), Segments = new List<double> { DesignUtil.Mm(s1), DesignUtil.Mm(s2) },
                Start = 0, End = xEnd, Span = -1
            });
        }
    }

    /// <summary>Khoảng cách từ mép bê tông tới mép ngoài cốt đai (m): lớp bảo vệ (tới mép cốt dọc) − Øđai, ≥ 15 mm.</summary>
    internal static double StirrupCover(double coverToMain, int stirrupDia) => Math.Max(coverToMain - stirrupDia / 1000.0, 0.015);

    private static string Signature(BeamLineDesign d)
    {
        var sp = string.Join("/", d.Spans.Select(s => Math.Round(s.L * 100) + "," + Math.Round(s.SupportWidthLeft * 100) + "," + Math.Round(s.SupportWidthRight * 100)));
        return d.Section + "|" + Math.Round(d.TopElevation * 100) + "|" + sp + "|" + RebarNumbering.Signature(d.Bars)
               + (d.IsCurved ? "|C" + Math.Round(d.CurveRadius * 100) : "");
    }
}
