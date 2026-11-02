using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

/// <summary>[MóngLệchTâm] Móng kết hợp (móng phối hợp) cột biên sát ranh giới + cột trong cùng trục:
/// <list type="bullet">
/// <item>Hợp lực x̄ = (N1·x1 + N2·x2)/(N1 + N2) (đo từ mép móng tại ranh giới) trùng trọng tâm đáy móng → áp lực đều:
/// móng chữ nhật dài L = 2x̄ nếu L ≥ x2 + a2/2 + 0,3 m; ngược lại móng hình thang L = x2 + a2/2 + 0,3 m, B1 + B2 = 2A/L, x̄ = L(B1 + 2B2)/(3(B1 + B2)).</item>
/// <item>Bề rộng theo áp lực tiêu chuẩn ptb ≤ R, pmax ≤ 1,2R (TCVN 9362:2012); các tổ hợp khác có hợp lực lệch khỏi trọng tâm → kể mô men ΣN·Δx.</item>
/// <item>Phương dọc: dầm tĩnh định chịu áp lực ròng q(x) = ΣN·b(x)/A hướng lên và 2 lực tập trung N1, N2 → M(x), Q(x);
/// mô men âm giữa 2 cột (căng trên) → thép lớp trên, mô men dương ở đầu vươn (căng dưới) → thép lớp dưới.</item>
/// <item>Phương ngang: bản công xôn (B − bc)/2 dưới mỗi cột; chọc thủng từng cột (cột biên: chu vi 3 phía); cắt bê tông không cốt đai Q ≤ Qb (TCVN 5574:2018 8.1.3.3, 8.1.6.2).</item>
/// </list></summary>
public static class CombinedFootingDesigner
{
    public const double Overhang = 0.30;

    private static double AsPerM(double mPerM, double h0, Material steel) =>
        h0 <= 0 ? 1 : Math.Max(mPerM / (0.9 * steel.Rs * h0), 0.0005 * h0);

    /// <summary>Biểu đồ dọc móng (toạ độ t từ mép ranh giới): áp lực ròng tuyến tính p(t) = ΣN/A + M·(t − tc)/I·…, hai lực tập trung N1 (t1), N2 (t2).</summary>
    public static (double MPos, double MNeg, double VMax) Diagram(double N1, double t1, double N2, double t2, double Lc, double B1, double B2, double ecc, double a1, double a2, double h0)
    {
        int n = 240;
        double A = Lc * (B1 + B2) / 2, Nt = N1 + N2;
        if (A <= 0 || Lc <= 0) return (0, 0, 0);
        double tc = EccentricFooting.TrapezoidCentroid(Lc, B1, B2);
        double Bavg = (B1 + B2) / 2, I = Bavg * Lc * Lc * Lc / 12;
        double M = Nt * ecc;
        double dt = Lc / n, V = 0, Mo = 0, mp = 0, mn = 0, vm = 0;
        bool p1 = false, p2 = false;
        for (int i = 0; i < n; i++)
        {
            double ta = i * dt, tb = ta + dt, tm = ta + dt / 2;
            double b = B1 + (B2 - B1) * tm / Lc;
            double dq = Math.Max(Nt / A + M * (tm - tc) / I, 0) * b * dt;   // phản lực nền hướng lên trên đoạn dt
            Mo += V * dt + dq * dt / 2;
            V += dq;
            if (!p1 && t1 <= tb + 1e-12) { V -= N1; p1 = true; }
            if (!p2 && t2 <= tb + 1e-12) { V -= N2; p2 = true; }
            mp = Math.Max(mp, Mo); mn = Math.Min(mn, Mo);
            // lực cắt tại tiết diện cách mép cột ≥ h0 (bỏ vùng trong tháp chọc thủng)
            if (Math.Abs(tb - t1) > a1 / 2 + h0 && Math.Abs(tb - t2) > a2 / 2 + h0) vm = Math.Max(vm, Math.Abs(V));
        }
        return (mp, mn, vm);
    }

    public static FootingDesign Design(Project p, AnalysisResults r, EccPlan plan, EccNode e, List<string> combos)
    {
        var q = plan.Get(e.CombinedWith) ?? throw new InvalidOperationException("không tìm thấy cột trong của móng kết hợp");
        bool ax = e.PartnerX == q.NodeId;
        int dir = ax ? Math.Sign(q.X - e.X) : Math.Sign(q.Y - e.Y);
        if (dir == 0) dir = 1;
        double p1 = ax ? e.X : e.Y, p2 = ax ? q.X : q.Y, axis = ax ? e.Y : e.X;
        double a1 = ax ? e.Hc : e.Bc, a2 = ax ? q.Hc : q.Bc, w1 = ax ? e.Bc : e.Hc, w2 = ax ? q.Bc : q.Hc;
        double lim = ax ? (dir > 0 ? e.Limits.XMin : e.Limits.XMax) : (dir > 0 ? e.Limits.YMin : e.Limits.YMax);
        if (double.IsInfinity(lim) || double.IsNaN(lim) || e.Manual) lim = p1 - dir * a1 / 2;
        double start = lim, t1 = (p1 - start) * dir, t2 = (p2 - start) * dir;
        if (t1 < a1 / 2 - 1e-6) t1 = a1 / 2;

        var cs = p.Concrete;
        var soil = GeotechCalc.SoilFor(p, (e.X + q.X) / 2, (e.Y + q.Y) / 2, out var geoBh);
        var conc = DesignUtil.ConcreteOf(p, cs.ConcreteFooting, cs.ConcreteFooting);
        var main = p.Material(cs.RebarMain);
        var stlSmall = p.Material(cs.RebarStirrup);
        Material SteelOf(int dia) => dia < 10 ? stlSmall : main;
        int[] dias = (p.RebarPrefs ?? new RebarPreferences()).Filter(DesignUtil.Sorted(cs.FootingBars, new[] { 10, 12, 14, 16 }));
        double kt = Math.Max(soil.LoadFactorAvg, 1.0), gtb = soil.GammaAvg, Df = soil.Depth, cover = cs.CoverFooting;

        var pairs = new List<(BaseLoad A, BaseLoad B)>();
        foreach (var la in e.Loads) foreach (var lb in q.Loads) if (lb.Combo == la.Combo) { pairs.Add((la, lb)); break; }
        if (pairs.Count == 0) throw new InvalidOperationException("không có tổ hợp chung của 2 chân cột móng kết hợp");
        var gp = pairs.OrderByDescending(x => x.A.N + x.B.N).First();
        double tbar = EccentricFooting.CombinedResultant(Math.Max(gp.A.N, 1e-6), t1, Math.Max(gp.B.N, 1e-6), t2);
        double Lmin = t2 + a2 / 2 + Overhang;
        bool rect = 2 * tbar >= Lmin - 1e-9;
        double Lc = RcSection.CeilTo(rect ? 2 * tbar : Lmin, 0.05);
        if (rect) Lc = Math.Max(Lc, Lmin);

        var d = new FootingDesign
        {
            NodeId = e.NodeId, ColumnFrame = e.Column?.Id ?? 0, X = e.X, Y = e.Y, ColumnB = e.Bc, ColumnH = e.Hc, Depth = soil.Depth, Story = "Móng"
        };
        d.RebarKeys.Add(FootingDesigner.MemberKey(e.NodeId));
        d.RebarKeys.Add(FootingDesigner.MemberKey(q.NodeId));

        double wMin = Math.Max(Math.Max(w1, w2) + 0.20, FootingDesigner.MinSize);
        double NtcMax = pairs.Max(x => x.A.N + x.B.N) / kt;
        double R = FootingDesigner.BearingR(soil, 1.0);
        double A0 = NtcMax / Math.Max(R - gtb * Df, 10);
        double S = Math.Max(2 * A0 / Lc, 2 * wMin);   // B1 + B2
        double B1 = 0, B2 = 0, tc = Lc / 2;
        PressureResult w = default;
        bool sized = false;
        string gov = "";
        void Shape()
        {
            if (rect) { B1 = B2 = Math.Max(RcSection.CeilTo(S / 2, 0.05), wMin); }
            else
            {
                var (b1, b2) = EccentricFooting.TrapezoidWidths(S * Lc / 2, Lc, tbar);
                b2 = Math.Max(RcSection.CeilTo(b2, 0.05), w2 + 0.20);
                b1 = Math.Max(RcSection.CeilTo(Math.Max(S - b2, b1), 0.05), wMin);
                B1 = b1; B2 = b2;
            }
            tc = EccentricFooting.TrapezoidCentroid(Lc, B1, B2);
        }
        PressureResult Eval()
        {
            var worst = new PressureResult { Ptb = double.MinValue, Pmax = double.MinValue, Pmin = double.MaxValue, Contact = 1 };
            double A = Lc * (B1 + B2) / 2, Bavg = (B1 + B2) / 2;
            foreach (var (la, lb) in pairs)
            {
                double Nt = la.N + lb.N;
                double ml = ax ? Math.Abs(la.My) + Math.Abs(lb.My) + (Math.Abs(la.Qx) + Math.Abs(lb.Qx)) * Df : Math.Abs(la.Mx) + Math.Abs(lb.Mx) + (Math.Abs(la.Qy) + Math.Abs(lb.Qy)) * Df;
                double mb = ax ? Math.Abs(la.Mx) + Math.Abs(lb.Mx) + (Math.Abs(la.Qy) + Math.Abs(lb.Qy)) * Df : Math.Abs(la.My) + Math.Abs(lb.My) + (Math.Abs(la.Qx) + Math.Abs(lb.Qx)) * Df;
                ml += Math.Abs(la.N * (t1 - tc) + lb.N * (t2 - tc));
                var pr = EccentricFooting.Pressure(Nt / kt + gtb * Df * A, ml / kt, mb / kt, Lc, Bavg);
                if (pr.Ptb > worst.Ptb) worst.Ptb = pr.Ptb;
                if (pr.Pmax > worst.Pmax) { worst.Pmax = pr.Pmax; worst.Combo = la.Combo; gov = la.Combo; worst.EL = pr.EL; worst.Partial = pr.Partial; }
                worst.Pmin = Math.Min(worst.Pmin, pr.Pmin);
                worst.Contact = Math.Min(worst.Contact, pr.Contact);
            }
            return worst;
        }
        for (int it = 0; it < 200; it++)
        {
            Shape();
            R = FootingDesigner.BearingR(soil, Math.Min(Lc, Math.Min(B1, B2)));
            w = Eval();
            if (EccentricFooting.PressureOk(w, R)) { sized = true; break; }
            S = Math.Round(S + 0.10, 3);
            if (S / 2 > 6) break;
        }
        if (!sized) d.Messages.Add("LỖI móng kết hợp không thoả áp lực nền với bề rộng ≤ 6 m – xem xét móng băng / móng cọc");
        double Bmax = Math.Max(B1, B2), Bavg2 = (B1 + B2) / 2, Aarea = Lc * Bavg2;

        // ---- chiều cao: chọc thủng 2 cột + cắt dọc ----
        double H = Math.Max(0.40, RcSection.CeilTo(Math.Max(t2 - t1, 1.0) / 8, 0.05)), F1 = 0, F2 = 0, Fu1 = 0, Fu2 = 0, Vd = 0, Qb = 0;
        (double MPos, double MNeg, double VMax) dg = default;
        int dg0 = dias[Math.Min(1, dias.Length - 1)];
        for (int it = 0; it < 60; it++)
        {
            double h0 = H - cover - dg0 / 1000.0;
            F1 = F2 = 0; Vd = 0; dg = (0, 0, 0);
            double mpos = 0, mneg = 0;
            foreach (var (la, lb) in pairs)
            {
                if (la.N + lb.N <= 0) continue;
                EccentricFooting.Punching(la.N, Lc, Bavg2, a1, w1, -(t1 - Lc / 2), 0, h0, conc.Rbt, out double f1, out double fu1, out _);
                EccentricFooting.Punching(lb.N, Lc, Bavg2, a2, w2, -(t2 - Lc / 2), 0, h0, conc.Rbt, out double f2, out double fu2, out _);
                F1 = Math.Max(F1, f1); Fu1 = fu1; F2 = Math.Max(F2, f2); Fu2 = fu2;
                double ecc = (la.N * t1 + lb.N * t2) / (la.N + lb.N) - tc;
                var g = Diagram(la.N, t1, lb.N, t2, Lc, B1, B2, ecc, a1, a2, h0);
                mpos = Math.Max(mpos, g.MPos); mneg = Math.Min(mneg, g.MNeg); Vd = Math.Max(Vd, g.VMax);
            }
            dg = (mpos, mneg, Vd);
            Qb = RcSection.QbConcrete(Bavg2, h0, conc);
            if (F1 <= Fu1 + 1e-6 && F2 <= Fu2 + 1e-6 && Vd <= Qb + 1e-6) break;
            if (H >= 2.0 - 1e-9) break;
            H = Math.Round(H + 0.05, 3);
        }

        // ---- thép ----
        double h0L = H - cover - dg0 / 1000.0;
        double Mpos = dg.MPos, Mneg = -dg.MNeg;
        double pnet = pairs.Max(x => (x.A.N + x.B.N)) / Aarea;
        double cT = Math.Max((Bmax - Math.Min(w1, w2)) / 2, 0);
        double mT = pnet * cT * cT / 2;   // kN.m/m
        var sBot = RcSection.ChooseSpacing(dia => AsPerM(Mpos / Bavg2, H - cover - dia / 2000.0, SteelOf(dia)), dias, 0.20, 0.10, 0.15, 10);
        var sTop = RcSection.ChooseSpacing(dia => AsPerM(Mneg / Bavg2, H - cover - dia / 2000.0, SteelOf(dia)), dias, 0.20, 0.10, 0.15, 12);
        var sTr = RcSection.ChooseSpacing(dia => AsPerM(mT, H - cover - sBot.Dia / 1000.0 - dia / 2000.0, SteelOf(dia)), dias, 0.20, 0.10, 0.15, 10);
        if (!sBot.Ok || !sTop.Ok || !sTr.Ok) d.Messages.Add("LỖI không bố trí đủ thép móng kết hợp với bước ≥ 100 mm – tăng chiều cao móng");

        double Lx = ax ? Lc : Bmax, Ly = ax ? Bmax : Lc;
        d.L = Lx; d.B = Ly; d.H = H; d.Hb = H; d.R = R; d.Ptb = w.Ptb; d.Pmax = w.Pmax; d.Pmin = w.Pmin;
        var gl = pairs.FirstOrDefault(x => x.A.Combo == gov);
        if (gl.A.Combo == null) gl = gp;
        d.N = gl.A.N + gl.B.N; d.Mx = gl.A.Mx + gl.B.Mx; d.My = gl.A.My + gl.B.My; d.Qx = gl.A.Qx + gl.B.Qx; d.Qy = gl.A.Qy + gl.B.Qy;
        d.Ntc = d.N / kt; d.Combo = gl.A.Combo;
        // phương L = X: phương dọc nếu ax
        var sL = ax ? sBot : sTr; var sB = ax ? sTr : sBot;
        d.ML = ax ? Mpos : mT * Lc; d.MB = ax ? mT * Lc : Mpos;
        d.AsL = sL.AsReqPerM * Ly; d.AsB = sB.AsReqPerM * Lx;
        d.DiaL = sL.Dia; d.DiaB = sB.Dia; d.SpacingL = sL.Spacing; d.SpacingB = sB.Spacing; d.BarsL = sL.Text; d.BarsB = sB.Text;

        d.Checks.Add(new CheckItem { Name = "Áp lực trung bình đáy móng ptb ≤ R", Demand = w.Ptb, Capacity = R, Unit = "kN/m²", Note = gov });
        d.Checks.Add(new CheckItem { Name = "Áp lực lớn nhất pmax ≤ 1,2R", Demand = w.Pmax, Capacity = 1.2 * R, Unit = "kN/m²", Note = gov });
        if (w.Pmin < -1e-6)
            d.Checks.Add(new CheckItem { Name = "Áp lực nhỏ nhất pmin ≥ 0 hoặc tiếp xúc ≥ 75% (lệch tâm)", Demand = 0.75, Capacity = Math.Max(w.Contact, 0), Unit = "", Note = $"pmin = {w.Pmin:0.0} kN/m²" });
        else
            d.Checks.Add(new CheckItem { Name = "Áp lực nhỏ nhất pmin ≥ 0", Demand = 0, Capacity = 1, Unit = "kN/m²", Note = $"pmin = {w.Pmin:0.0} kN/m²" });
        d.Checks.Add(new CheckItem { Name = "Chọc thủng F ≤ Rbt·u·h0", Demand = F1, Capacity = Fu1, Unit = "kN", Note = $"cột biên (nút {e.NodeId}, chu vi 3 phía), H = {H * 1000:0} mm" });
        d.Checks.Add(new CheckItem { Name = "Uốn phương L tại mép cột", Demand = d.ML, Unit = "kN.m", Note = sL.Text, Capacity = RcSection.MomentCapacity(ax ? Bavg2 : Lc, h0L, sL.AsPerM * (ax ? Bavg2 : Lc), 0, cover, conc, SteelOf(sL.Dia)) });
        d.Checks.Add(new CheckItem { Name = "Uốn phương B tại mép cột", Demand = d.MB, Unit = "kN.m", Note = sB.Text, Capacity = RcSection.MomentCapacity(ax ? Lc : Bavg2, h0L - sL.Dia / 1000.0, sB.AsPerM * (ax ? Lc : Bavg2), 0, cover, conc, SteelOf(sB.Dia)) });
        d.Checks.Add(new CheckItem { Name = "Chọc thủng cột trong F ≤ Rbt·u·h0", Demand = F2, Capacity = Fu2, Unit = "kN", Note = $"nút {q.NodeId}" });
        d.Checks.Add(new CheckItem { Name = "Uốn dọc móng – mô men âm giữa 2 cột (thép lớp trên)", Demand = Mneg, Unit = "kN.m", Note = sTop.Text, Capacity = Mneg > 1e-9 ? RcSection.MomentCapacity(Bavg2, H - cover - sTop.Dia / 2000.0, sTop.AsPerM * Bavg2, 0, cover, conc, SteelOf(sTop.Dia)) : 1 });
        d.Checks.Add(new CheckItem { Name = "Cắt dọc móng Q ≤ Qb (không cốt đai)", Demand = Vd, Capacity = Qb, Unit = "kN", Note = "tiết diện cách mép cột h0" });

        double c = cover;
        int Cnt(double width, double sp) => (int)Math.Ceiling((width - 2 * c) / Math.Max(sp, 0.05) - 1e-9) + 1;
        double hk(int dia) => DesignUtil.R10(Math.Max(10 * dia / 1000.0, 0.10));
        void AddBar(string role, SpacingChoice sc, double len, double across, bool top)
        {
            double h1 = top ? DesignUtil.R10(Math.Max(Math.Min(H - 2 * c - 0.05, 0.25), 0.10)) : hk(sc.Dia);
            double ln = DesignUtil.R10(len - 2 * c);
            d.Bars.Add(new BarMark
            {
                Role = role, Dia = sc.Dia, Spacing = sc.Spacing, Count = Cnt(across, sc.Spacing), Shape = "U", IsTop = top, Start = c, End = len - c,
                Length = DesignUtil.R10(ln + 2 * h1), Segments = new List<double> { DesignUtil.Mm(h1), DesignUtil.Mm(ln), DesignUtil.Mm(h1) }
            });
        }
        AddBar("Lớp dưới phương L", sL, Lx, Ly, false);
        AddBar("Lớp dưới phương B", sB, Ly, Lx, false);
        AddBar(ax ? "Lớp trên phương L" : "Lớp trên phương B", sTop, Lc, Bmax, true);
        RebarNumbering.Renumber(d.Bars);

        // ---- thông tin móng kết hợp ----
        double mid = start + dir * Lc / 2;
        var info = new CombinedFootingInfo
        {
            NodeA = e.NodeId, NodeB = q.NodeId, AlongX = ax, Length = Lc, B1 = B1, B2 = B2, Start = start, Dir = dir, Axis = axis,
            Resultant = tbar, Centroid = tc, XB = q.X, YB = q.Y, N1 = gp.A.N, N2 = gp.B.N, MPos = Mpos, MNeg = -Mneg, V = Vd
        };
        PilePoint Pt(double t, double s) => ax ? new PilePoint(start + dir * t, axis + s) : new PilePoint(axis + s, start + dir * t);
        info.Outline.AddRange(new[] { Pt(0, -B1 / 2), Pt(Lc, -B2 / 2), Pt(Lc, B2 / 2), Pt(0, B1 / 2) });
        double oxA = ax ? mid - p1 : 0, oyA = ax ? 0 : mid - p1;
        var ei = new EccentricInfo
        {
            Scheme = EccentricScheme.Combined, Recommended = e.Recommended, Recommendation = e.Reason, Sides = e.Manual ? "nhập tay" : e.Limits.Sides(), Boundary = !e.Manual,
            OffsetX = oxA, OffsetY = oyA, PartnerX = ax ? q.NodeId : 0, PartnerY = ax ? 0 : q.NodeId, SpanX = ax ? Math.Abs(p2 - p1) : 0, SpanY = ax ? 0 : Math.Abs(p2 - p1),
            N1 = gp.A.N, R1 = gp.A.N + gp.B.N, Ptb = w.Ptb, Pmax = w.Pmax, Pmin = w.Pmin, Contact = w.Contact, ResultantE = Math.Abs(tbar - tc), Combined = info
        };
        ei.Locations.Add(new EccentricLocation { NodeId = e.NodeId, X = e.X, Y = e.Y, OffsetX = oxA, OffsetY = oyA });
        ei.Options.Add($"(c) Móng kết hợp {(rect ? "chữ nhật" : "hình thang")} L = {Lc:0.00} m, B1 = {B1:0.00} m, B2 = {B2:0.00} m; hợp lực cách mép ranh giới x̄ = {tbar:0.000} m, trọng tâm đáy móng {tc:0.000} m");
        d.Eccentric = ei;
        EccentricFooting.Record(plan, e, oxA, oyA, Lx, Ly, "", false);
        q.Designed = true;
        d.Messages.Add($"Móng kết hợp (móng phối hợp) cột biên nút {e.NodeId} + cột trong nút {q.NodeId}, cách nhau {Math.Abs(p2 - p1):0.00} m: "
                       + (rect ? $"chữ nhật L = 2·x̄ = {Lc:0.00} m × B = {B1:0.00} m" : $"hình thang L = {Lc:0.00} m, B1 = {B1:0.00} m (phía ranh giới), B2 = {B2:0.00} m")
                       + $"; x̄ = (N1·x1 + N2·x2)/(N1 + N2) = {tbar:0.000} m từ mép ranh giới");
        d.Messages.Add($"Dọc móng: M− = {Mneg:0.0} kN.m (thép lớp trên {sTop.Text}), M+ = {Mpos:0.0} kN.m (thép lớp dưới {sBot.Text}); ngang: công xôn {cT * 1000:0} mm, m = {mT:0.0} kN.m/m ({sTr.Text})");
        if (geoBh != null) d.Messages.Add("Nền theo hố khoan " + geoBh.Name);
        var rep = new FoundationCheckReport
        {
            Kind = "Móng kết hợp", Classification = rect ? "Móng kết hợp chữ nhật" : "Móng kết hợp hình thang",
            Behaviour = "Móng kết hợp đỡ cột biên sát ranh giới và cột trong: trọng tâm đáy móng trùng hợp lực 2 lực dọc nên áp lực nền gần đều; "
                        + "theo phương dọc móng làm việc như dầm ngược chịu áp lực nền hướng lên và 2 gối là chân cột (mô men âm giữa 2 cột – thép lớp trên), "
                        + "theo phương ngang là bản công xôn dưới mỗi cột."
        };
        foreach (var ck in d.Checks)
            rep.Items.Add(new SafetyItem
            {
                Group = ck.Name.StartsWith("Uốn") ? "Uốn" : ck.Name.StartsWith("Chọc") ? "Chọc thủng" : ck.Name.StartsWith("Cắt") ? "Cắt" : "Nền",
                Name = ck.Name, Value = ck.Demand, Limit = ck.Capacity, Unit = ck.Unit, Status = ck.Ok ? CheckStatus.Ok : CheckStatus.Fail, Formula = ck.Note
            });
        d.Safety = rep;
        return d;
    }
}
