using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Thiết kế dầm BTCT liên tục theo TCVN 5574:2018: gom phần tử thành dải dầm, tính thép dọc, đai, độ võng,
/// bố trí thép (BarMark) và vùng đai (StirrupZone) theo quy tắc thể hiện kiểu Kết cấu Soft.</summary>
public static class BeamDesigner
{
    private const double Tol = 1e-3;
    /// <summary>Hệ số độ cứng khi nứt dùng ước lượng độ võng: B = 0,3·Eb·I (gần đúng, thiên về an toàn so với 8.2.3 TCVN 5574:2018).</summary>
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
        int[] dias = DesignUtil.Sorted(cs.BeamBars, new[] { 14, 16, 18, 20, 22, 25 });
        int[] sdias = DesignUtil.Sorted(cs.StirrupBars, new[] { 6, 8, 10 }).Where(x => x >= 6).ToArray();
        if (sdias.Length == 0) sdias = new[] { 6 };

        var d = new BeamLineDesign
        {
            Story = li.Story, Section = sec.Name, B = b, H = h, TopElevation = li.Z,
            Frames = li.Segs.Select(s => s.F.Id).ToList()
        };
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
            }
        }

        // ---- thép dọc yêu cầu ----
        var fl = new FlexureResult[ns]; var fs = new FlexureResult[ns]; var fr = new FlexureResult[ns];
        for (int k = 0; k < ns; k++)
        {
            var res = d.Spans[k];
            fl[k] = RcSection.DesignRect(res.MLeft, b, h0, a, conc, main, cs.MinRatioBeam);
            fs[k] = RcSection.DesignRect(res.MSpan, b, h0, a, conc, main, cs.MinRatioBeam);
            fr[k] = RcSection.DesignRect(res.MRight, b, h0, a, conc, main, cs.MinRatioBeam);
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

        // ---- chọn thép chạy suốt + tăng cường / mũ gối ----
        double compSup = supComp.Max(), compSpan = fs.Max(f => f.AsComp);
        int dB = ChooseContinuous(dias, b, c, compSup, xEnd, k => fs[k].As, k => 0.8 * lcl[k], ns, out var extraBot);
        int dT = ChooseContinuous(dias, b, c, compSpan, xEnd, j => supReq[j], j => SupportBarSpan(j, ns, nodeW, lcl), ns + 1, out var extraTop);
        double aCont = 2 * RcSection.BarArea(dB), aTop = 2 * RcSection.BarArea(dT);
        if (aCont < compSup * (1 - 1e-6)) d.Messages.Add("Thép dưới chạy suốt chưa đủ làm cốt chịu nén tại gối – kiểm tra lại");
        if (aTop < compSpan * (1 - 1e-6)) d.Messages.Add("Thép trên chạy suốt chưa đủ làm cốt chịu nén giữa nhịp – kiểm tra lại");

        // ---- cốt đai ----
        int sd = sdias[0];
        if (h > 0.8) sd = Math.Max(sd, sdias.FirstOrDefault(x => x >= 8, sdias[^1]));
        foreach (int dd in sdias)
        {
            if (dd < sd) continue;
            bool ok = d.Spans.All(res => RcSection.Shear(res.QMax, b, h0, conc, stl, dd).Ok);
            sd = dd;
            if (ok) break;
        }
        var shear = d.Spans.Select(res => RcSection.Shear(res.QMax, b, h0, conc, stl, sd)).ToArray();

        // ---- kết quả từng nhịp + kiểm tra ----
        double I = b * h * h * h / 12.0;
        double[] mSls = new double[ns];
        double[] mSlsSup = new double[ns];
        foreach (string cn in sls)
        {
            var cr = r.Get(cn);
            for (int k = 0; k < ns; k++)
            {
                var ser = SpanSeries(cr, li.Spans[k]);
                foreach (var t in ser) mSls[k] = Math.Max(mSls[k], t.M);
                if (ser.Count > 0) mSlsSup[k] = Math.Max(mSlsSup[k], Math.Max(-ser[0].M, -ser[^1].M));
            }
        }

        for (int k = 0; k < ns; k++)
        {
            var res = d.Spans[k]; var sp = li.Spans[k];
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
            res.StirrupDia = sd;
            res.StirrupSupport = shear[k].SSupport;
            res.StirrupMid = shear[k].SMid;

            // h0 thực tế theo số hàng thép
            double h0b = h - EffA(c, botGroups, b);
            double h0tl = h - EffA(c, topLG, b), h0tr = h - EffA(c, topRG, b);
            double muL = RcSection.MomentCapacity(b, h0tl, res.AsTopLeftProv, aCont, a, conc, main);
            double muS = RcSection.MomentCapacity(b, h0b, res.AsBotProv, aTop, a, conc, main);
            double muR = RcSection.MomentCapacity(b, h0tr, res.AsTopRightProv, aCont, a, conc, main);
            string sk = " nhịp " + (k + 1);
            d.Checks.Add(new CheckItem { Name = "Uốn gối trái" + sk, Demand = Math.Abs(res.MLeft), Capacity = muL, Unit = "kN.m", Note = res.TopLeftText + " – " + res.ComboMLeft });
            d.Checks.Add(new CheckItem { Name = "Uốn giữa nhịp" + sk, Demand = res.MSpan, Capacity = muS, Unit = "kN.m", Note = res.BotText + " – " + res.ComboMSpan });
            d.Checks.Add(new CheckItem { Name = "Uốn gối phải" + sk, Demand = Math.Abs(res.MRight), Capacity = muR, Unit = "kN.m", Note = res.TopRightText + " – " + res.ComboMRight });
            double qu = RcSection.ShearCapacity(b, h0, conc, stl, sd, 2, shear[k].SSupport);
            d.Checks.Add(new CheckItem
            {
                Name = "Cắt" + sk, Demand = res.QMax, Capacity = qu, Unit = "kN",
                Note = "Ø" + sd + "a" + DesignUtil.F(shear[k].SSupport * 1000, "0") + " – " + res.ComboQ + (string.IsNullOrEmpty(shear[k].Note) ? "" : "; " + shear[k].Note)
            });
            if (!shear[k].Ok) d.Messages.Add("LỖI" + sk + ": " + shear[k].Note);

            // độ võng ước lượng
            bool cantilever = (k == 0 && !sp.SupL) || (k == ns - 1 && !sp.SupR);
            double B = CrackedStiffnessFactor * conc.E * I;
            double f;
            if (cantilever) f = mSlsSup[k] * sp.L * sp.L / (4 * B);
            else f = 5.0 / 48.0 * mSls[k] * sp.L * sp.L / B;
            double lim = (cantilever ? 2 * sp.L : sp.L) / Math.Max(cs.BeamDeflectionLimit, 1);
            res.Deflection = f; res.DeflectionLimit = lim;
            d.Checks.Add(new CheckItem
            {
                Name = "Võng" + sk, Demand = f * 1000, Capacity = lim * 1000, Unit = "mm",
                Note = cantilever ? "Công xôn: f = M·L²/(4·0,3·Eb·I)" : "f = 5/48·M·L²/(0,3·Eb·I)"
            });
        }
        if (extraBot.Concat(extraTop).Any(e => !e.Ok))
            d.Messages.Add("LỖI không bố trí đủ thép dọc trong bề rộng dầm b = " + DesignUtil.F(b * 1000, "0") + " mm – cần tăng tiết diện");

        // ---- bố trí thép ----
        Detail(d, li, cs, b, h, c, dB, dT, extraBot, extraTop, shear, sd, nodeX, nodeW, lcl, xEnd);
        RebarNumbering.Renumber(d.Bars);
        int sm = RebarNumbering.StirrupMark(d.Bars, sd);
        foreach (var z in d.StirrupZones) z.Mark = sm;
        return d;
    }

    /// <summary>Khoảng cách từ mép chịu kéo tới trọng tâm nhóm thép (1 hoặc 2 hàng, khe hở hàng 25 mm).</summary>
    private static double EffA(double cover, List<(int Dia, int Count)> groups, double b)
    {
        int dmax = groups.Max(g => g.Dia);
        int total = groups.Sum(g => g.Count);
        int per = Math.Max(RcSection.MaxBarsInLayer(b, cover, dmax), 1);
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

    /// <summary>Chọn Ø cho 2 thanh góc chạy suốt sao cho tổng khối lượng (chạy suốt + bổ sung) nhỏ nhất.</summary>
    private static int ChooseContinuous(int[] dias, double b, double cover, double minArea, double totalLen,
        Func<int, double> req, Func<int, double> extraLen, int count, out BarSet[] extras)
    {
        double best = double.MaxValue; int bestD = dias[0];
        BarSet[] bestEx = null;
        foreach (int d in dias)
        {
            if (RcSection.MaxBarsInLayer(b, cover, d) < 2 && d != dias[0]) continue;
            double a2 = 2 * RcSection.BarArea(d);
            var ex = new BarSet[count];
            double score = a2 * totalLen;
            bool ok = true;
            for (int k = 0; k < count; k++)
            {
                double need = req(k) - a2;
                ex[k] = need > 1e-9 ? RcSection.ChooseExtra(need, 2, d, b, cover, dias) : new BarSet();
                if (!ex[k].Ok) ok = false;
                score += ex[k].Area * extraLen(k);
            }
            if (a2 < minArea * (1 - 1e-6)) score *= 3;
            if (!ok) score *= 10;
            if (score < best) { best = score; bestD = d; bestEx = ex; }
        }
        extras = bestEx ?? Enumerable.Range(0, count).Select(_ => new BarSet()).ToArray();
        return bestD;
    }

    /// <summary>Đoạn neo tại gối biên: (đoạn thẳng trong gối, đoạn bẻ xuống/lên). Neo tổng 30d; đoạn bẻ ≥ 10d và ≤ h − 2c.</summary>
    private static (double straight, double leg) EndAnchor(double w, int d, double h, double c)
    {
        double dm = d / 1000.0;
        if (w <= 1e-6) return (0, 0);
        double straight = Math.Max(w - c, 0);
        double leg = Math.Max(30 * dm - straight, 0);
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
        int dB, int dT, BarSet[] extraBot, BarSet[] extraTop, ShearResult[] shear, int sd,
        double[] nodeX, double[] nodeW, double[] lcl, double xEnd)
    {
        int ns = li.Spans.Count;
        bool supStart = li.Spans[0].SupL, supEnd = li.Spans[ns - 1].SupR;
        double xs = supStart ? -nodeW[0] / 2 + c : c;
        double xe = supEnd ? xEnd + nodeW[ns] / 2 - c : xEnd - c;

        // thép dưới chạy suốt
        var (_, lb0) = EndAnchor(supStart ? nodeW[0] : 0, dB, h, c);
        var (_, lb1) = EndAnchor(supEnd ? nodeW[ns] : 0, dB, h, c);
        d.Bars.Add(Longitudinal("Dưới", false, dB, 2, xs, xe, lb0, lb1, -1));
        // thép trên chạy suốt
        var (_, lt0) = EndAnchor(supStart ? nodeW[0] : 0, dT, h, c);
        var (_, lt1) = EndAnchor(supEnd ? nodeW[ns] : 0, dT, h, c);
        d.Bars.Add(Longitudinal("Trên", true, dT, 2, xs, xe, lt0, lt1, -1));
        if (xe - xs > StockLength)
            d.Messages.Add("Thép chạy suốt dài " + DesignUtil.F(xe - xs, "0.00") + " m > 11,7 m – nối chồng thép trên tại giữa nhịp, thép dưới tại gối (lnối ≥ 30d)");

        // mũ gối
        for (int j = 0; j <= ns; j++)
        {
            var ex = extraTop[j];
            if (ex.IsEmpty) continue;
            var (dia, n) = ex.Groups[0];
            if (j == 0)
            {
                var (_, leg) = EndAnchor(supStart ? nodeW[0] : 0, dia, h, c);
                double x1 = li.Spans[0].X0 + li.Spans[0].FaceL + lcl[0] / 4;
                d.Bars.Add(Longitudinal("Mũ gối", true, dia, n, xs, x1, leg, 0, 0));
            }
            else if (j == ns)
            {
                var (_, leg) = EndAnchor(supEnd ? nodeW[ns] : 0, dia, h, c);
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

        // tăng cường dưới
        for (int k = 0; k < ns; k++)
        {
            var ex = extraBot[k];
            if (ex.IsEmpty) continue;
            var sp = li.Spans[k];
            var (dia, n) = ex.Groups[0];
            double x0 = sp.X0 + sp.FaceL + 0.1 * lcl[k];
            double x1 = sp.X0 + sp.FaceR - 0.1 * lcl[k];
            d.Bars.Add(Longitudinal("Tăng cường dưới", false, dia, n, x0, x1, 0, 0, k));
        }

        // cốt giá cạnh dầm cao (TCVN 5574:2018 mục 10.3.6: h > 700 mm)
        if (h > 0.7)
        {
            int pairs = (int)Math.Ceiling((h - 2 * c) / 0.4) - 1;
            d.Bars.Add(Longitudinal("Giá", false, 12, 2 * Math.Max(pairs, 1), xs, xe, 0, 0, -1));
        }

        // đai
        double s1 = b - 2 * c, s2 = h - 2 * c;
        int total = 0;
        double minS = double.MaxValue;
        for (int k = 0; k < ns; k++)
        {
            var sp = li.Spans[k];
            double L0 = lcl[k];
            double ss = shear[k].SSupport, sm = shear[k].SMid;
            minS = Math.Min(minS, ss);
            int nsup = (int)Math.Ceiling(L0 / 4 / ss - 1e-9);
            double zs = nsup * ss;
            if (2 * zs > L0) { zs = L0 / 2; nsup = (int)Math.Ceiling(zs / ss - 1e-9); }
            double mid = L0 - 2 * zs;
            int cnt;
            if (mid < 1e-6) cnt = 2 * nsup + 1;
            else cnt = 2 * (nsup + 1) + Math.Max((int)Math.Ceiling(mid / sm - 1e-9) - 1, 0);
            total += cnt;
            double f0 = sp.X0 + sp.FaceL, f1 = sp.X0 + sp.FaceR;
            d.Spans[k].SupportZone = zs;
            d.StirrupZones.Add(new StirrupZone { Span = k, From = f0, To = f0 + zs, Dia = sd, Spacing = ss });
            if (mid > 1e-6) d.StirrupZones.Add(new StirrupZone { Span = k, From = f0 + zs, To = f1 - zs, Dia = sd, Spacing = sm });
            d.StirrupZones.Add(new StirrupZone { Span = k, From = f1 - zs, To = f1, Dia = sd, Spacing = ss });
        }
        d.Bars.Add(new BarMark
        {
            Role = "Đai", Dia = sd, Count = total, Shape = "Đai", Spacing = minS < double.MaxValue ? minS : 0.15,
            Length = DesignUtil.StirrupLength(s1, s2, sd), Segments = new List<double> { DesignUtil.Mm(s1), DesignUtil.Mm(s2) },
            Start = 0, End = xEnd, Span = -1
        });
    }

    private static string Signature(BeamLineDesign d)
    {
        var sp = string.Join("/", d.Spans.Select(s => Math.Round(s.L * 100) + "," + Math.Round(s.SupportWidthLeft * 100) + "," + Math.Round(s.SupportWidthRight * 100)));
        return d.Section + "|" + Math.Round(d.TopElevation * 100) + "|" + sp + "|" + RebarNumbering.Signature(d.Bars);
    }
}
