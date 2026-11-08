using KetCauVXT.Analysis;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Thiết kế móng đơn BTCT trên nền thiên nhiên dưới chân cột:
/// sức chịu tải nền theo TCVN 9362:2012, kích thước đáy theo điều kiện áp lực, chiều cao theo chọc thủng
/// (TCVN 5574:2018 mục 8.1.6) và cốt thép đáy theo mô men tại mép cột.</summary>
public static partial class FootingDesigner
{
    /// <summary>Chiều cao móng tối thiểu, m</summary>
    public const double MinHeight = 0.30;
    /// <summary>Kích thước đáy tối thiểu, m</summary>
    public const double MinSize = 0.80;

    private struct Load
    {
        public string Combo;
        public double N, Mx, My, Qx, Qy;
    }

    /// <summary>Thiết kế móng đơn dưới mọi chồng cột BTCT có nút chân là gối tựa và loại móng hiệu lực là móng đơn.</summary>
    public static List<FootingDesign> Design(Project p, AnalysisResults r, List<string> warnings)
    {
        warnings ??= new List<string>();
        var list = new List<(FootingDesign d, string sig)>();
        var combos = r.DesignCombos();

        // cột chân: cột BTCT có nút dưới là gối
        var baseCols = new List<(Node n, FrameElement f)>();
        foreach (var f in p.Frames.Where(f => f.Kind == FrameKind.Column))
        {
            var lo = DesignUtil.LowerNode(p, f);
            // chỉ chân cột có loại móng hiệu lực là móng đơn (Footing.Type ?? Project.Foundation.Type)
            if (lo.IsSupport && KetCauVXT.Design.Foundation.FoundationUtil.TypeOf(p, lo.Id) == FoundationType.Isolated
                && baseCols.All(x => x.n.Id != lo.Id)) baseCols.Add((lo, f));
        }
        baseCols = baseCols.OrderBy(x => Math.Round(x.n.Y, 2)).ThenBy(x => Math.Round(x.n.X, 2)).ToList();
        // [MóngLệchTâm] ranh giới đất: kế hoạch lệch tâm (null = không có ranh giới → thiết kế như cũ); móng biên có dầm cân bằng thiết kế trước
        var plan = EccentricFooting.Plan(p, r, combos, baseCols.Select(x => (x.n, x.f)));
        var rank = baseCols.Select((x, i) => (x.n.Id, i)).ToDictionary(t => t.Id, t => t.i);
        var order = plan == null ? baseCols : baseCols.OrderBy(x => EccentricFooting.Order(plan, x.n.Id)).ThenBy(x => rank[x.n.Id]).ToList();
        if (plan != null) foreach (var w in plan.Notes) warnings.Add("Cảnh báo: " + w);
        // ---- hết [MóngLệchTâm] ----

        // [CặpĐàiBiên] hai móng biên đối diện nối chung một dầm cân bằng: e2 của móng đối diện lấy theo kết quả thiết kế thật, lặp tới khi hội tụ (EccentricPairs.Sync)
        for (int pass = 0; pass < 4; pass++)
        {
            list.Clear();
            var passWarn = new List<string>();
            foreach (var (n, f) in order)
            {
                try
                {
                    // [MóngLệchTâm] móng kết hợp: nút phụ thiết kế chung với nút chính
                    var epn = plan?.Get(n.Id);
                    if (epn != null && epn.Secondary) continue;
                    var d = epn != null && epn.CombinedWith != 0 && epn.Scheme == EccentricScheme.Combined
                        ? CombinedFootingDesigner.Design(p, r, plan, epn, combos)
                        : DesignOne(p, r, n, f, combos, plan);
                    if (epn == null || epn.Scheme != EccentricScheme.Combined || epn.CombinedWith == 0)
                        d = ShapedRedesign(p, r, n, f, combos, d);   // [MóngHìnhDạng] xoay / dóng mép cột / ranh đất xiên / đa giác
                    string sig = Signature(d);
                    int k = list.FindIndex(x => x.sig == sig);
                    if (k >= 0)
                    {
                        var keep = list[k].d;
                        keep.Quantity++;
                        keep.RebarKeys.AddRange(d.RebarKeys.Where(x => !keep.RebarKeys.Contains(x)));
                        for (int i = 0; i < keep.Checks.Count && i < d.Checks.Count; i++)
                            if (d.Checks[i].Ratio > keep.Checks[i].Ratio) keep.Checks[i] = d.Checks[i];
                        keep.Messages.Add("Gộp móng giống hệt tại " + DesignUtil.GridName(p, d.X, d.Y));
                        EccentricFooting.Merge(keep.Eccentric, d.Eccentric);   // [MóngLệchTâm]
                    }
                    else list.Add((d, sig));
                }
                catch (Exception ex)
                {
                    passWarn.Add("LỖI thiết kế móng tại " + DesignUtil.GridName(p, n.X, n.Y) + ": " + ex.Message);
                }
            }
            if (pass == 3 || plan == null || !EccentricPairs.Sync(plan)) { warnings.AddRange(passWarn); break; }
        }
        if (plan != null) list = list.OrderBy(x => rank.TryGetValue(x.d.NodeId, out int k) ? k : 0).ToList();   // [MóngLệchTâm] giữ thứ tự đặt tên như cũ
        int no = 1;
        foreach (var (d, _) in list) d.Name = "M" + no++;
        var outList = list.Select(x => x.d).ToList();
        CuTramCalc.DifferentialSettlement(outList);   // [CừTràm] lún lệch giữa các móng cừ tràm kề nhau
        return outList;
    }

    /// <summary>Sức chịu tải tiêu chuẩn của nền R = m1·m2/ktc·(A·b·γII + B·Df·γ'II + D·c) (TCVN 9362:2012 mục 4.6.9, công thức 3),
    /// A = 0,25π/(cotφ + φ − π/2), B = 1 + π/(cotφ + φ − π/2), D = π·cotφ/(cotφ + φ − π/2), φ (rad).
    /// Nếu SoilSettings.ROverride &gt; 0 thì dùng trực tiếp giá trị này.</summary>
    public static double BearingR(SoilSettings s, double b)
    {
        if (s.ROverride > 0) return s.ROverride;
        double phi = Math.Max(s.Phi, 0.5) * Math.PI / 180.0;
        double cot = 1.0 / Math.Tan(phi);
        double den = cot + phi - Math.PI / 2;
        double A = 0.25 * Math.PI / den, B = 1 + Math.PI / den, D = Math.PI * cot / den;
        return s.M1 * s.M2 / Math.Max(s.Ktc, 1e-6) * (A * b * s.Gamma + B * s.Depth * s.GammaAbove + D * s.C);
    }

    private static FootingDesign DesignOne(Project p, AnalysisResults r, Node n, FrameElement col, List<string> combos, EccPlan plan = null)
    {
        var cs = p.Concrete;
        // nền: hố khoan gần nhất khi Soil.UseBorehole, ngược lại p.Soil (GeotechCalc.SoilFor)
        var soil = KetCauVXT.Design.Foundation.GeotechCalc.SoilFor(p, n.X, n.Y, out var geoBh);
        var sec = p.Section(col.Section);
        var conc = DesignUtil.ConcreteOf(p, cs.ConcreteFooting, cs.ConcreteFooting);
        var main = p.Material(cs.RebarMain);
        var stlSmall = p.Material(cs.RebarStirrup);
        int[] dias = (p.RebarPrefs ?? new RebarPreferences()).Filter(DesignUtil.Sorted(cs.FootingBars, new[] { 10, 12, 14, 16 }));

        // kích thước cột theo phương X (hc) và Y (bc)
        double hc = DesignUtil.ColumnDimAlong(p, col, Vec3.UnitX);
        double bc = DesignUtil.ColumnDimAlong(p, col, Vec3.UnitY);

        // ---- nội lực tại chân cột từ phản lực ----
        var loads = new List<Load>();
        foreach (string cn in combos)
        {
            var cr = r.Get(cn);
            if (!cr.Reactions.TryGetValue(n.Id, out var R)) continue;
            // phản lực tác dụng lên kết cấu; lực tác dụng xuống móng = ngược dấu → N = Fz (phản lực hướng lên = nén)
            loads.Add(new Load { Combo = cn, N = R[2], Mx = R[3], My = R[4], Qx = R[0], Qy = R[1] });
        }
        if (loads.Count == 0) throw new InvalidOperationException("không có phản lực tại nút " + n.Id);

        var d = new FootingDesign
        {
            NodeId = n.Id, ColumnFrame = col.Id, X = n.X, Y = n.Y, ColumnB = bc, ColumnH = hc,
            Depth = soil.Depth, Story = "Móng"
        };
        string key = MemberKey(n.Id);
        d.RebarKeys.Add(key);
        if (loads.All(l => l.N <= 0)) d.Messages.Add("LỖI chân cột chịu kéo ở mọi tổ hợp – móng đơn trên nền thiên nhiên không phù hợp");
        if (loads.Any(l => l.N <= 0)) d.Messages.Add("Có tổ hợp chân cột chịu nhổ – cần kiểm tra chống nhổ móng");

        double kt = Math.Max(soil.LoadFactorAvg, 1.0);
        double gtb = soil.GammaAvg, Df = soil.Depth;
        var given = p.Footings.FirstOrDefault(x => x.NodeId == n.Id);
        double asp = Math.Max(soil.AspectRatio, 1.0);
        // cạnh dài theo phương có mô men lớn hơn: L theo X chịu My
        double myMax = loads.Max(l => Math.Abs(l.My) + Math.Abs(l.Qx) * Df), mxMax = loads.Max(l => Math.Abs(l.Mx) + Math.Abs(l.Qy) * Df);
        bool longX = myMax >= mxMax;
        // [MóngLệchTâm] móng lệch tâm: tải dạng BaseLoad, móng biên kéo dài dọc ranh giới (giảm độ lệch tâm)
        var ep = plan?.Get(n.Id);
        if (ep != null && ep.Scheme == EccentricScheme.None) ep = null;
        List<BaseLoad> bl = ep == null ? null : loads.Select(l => new BaseLoad { Combo = l.Combo, N = l.N, Mx = l.Mx, My = l.My, Qx = l.Qx, Qy = l.Qy }).ToList();
        double eox = 0, eoy = 0;
        PressureResult epr = default;
        if (ep != null)
        {
            if (ep.Limits.EdgeX && !ep.Limits.EdgeY) { longX = false; asp = Math.Max(asp, EccentricFooting.EdgeAspect); }
            else if (ep.Limits.EdgeY && !ep.Limits.EdgeX) { longX = true; asp = Math.Max(asp, EccentricFooting.EdgeAspect); }
        }
        // ---- hết [MóngLệchTâm] ----

        // [CừTràm] gia cố nền bằng cừ tràm: sức chịu tải nền R ← [p] theo kích thước móng (CuTramCalc.Evaluate); không gia cố → R theo TCVN 9362 như cũ
        bool cuOn = CuTramCalc.ActiveAt(p, n.Id);
        double RofSize(double LL, double BB) => cuOn ? CuTramCalc.Evaluate(p, n.X, n.Y, soil, LL, BB).Allowable : BearingR(soil, Math.Min(LL, BB));
        // [CừTràm] giới hạn kích thước móng trên nền cừ tràm: L ≤ 0,5·bước cột theo X, B ≤ 0,5·bước cột theo Y – vượt quá → không khả thi, dùng cọc
        double cuLimX = double.PositiveInfinity, cuLimY = double.PositiveInfinity, cuPL = 0, cuPB = 0;
        bool cuCapped = false;
        if (cuOn)
        {
            foreach (var (bn, _) in FoundationUtil.BaseColumns(p))
            {
                if (bn.Id == n.Id) continue;
                double ddx = Math.Abs(bn.X - n.X), ddy = Math.Abs(bn.Y - n.Y);
                if (ddy < 0.3 && ddx > 0.3) cuLimX = Math.Min(cuLimX, 0.5 * ddx);
                if (ddx < 0.3 && ddy > 0.3) cuLimY = Math.Min(cuLimY, 0.5 * ddy);
            }
            cuLimX = Math.Max(cuLimX, MinSize); cuLimY = Math.Max(cuLimY, MinSize);
        }

        // ---- kích thước đáy (TCVN 9362:2012 mục 4.6) ----
        double Nmax = loads.Max(l => l.N) / kt;
        double R0 = RofSize(1.0, 1.0);
        double Aest = Nmax / Math.Max(R0 - gtb * Df, 10);
        double bs = Math.Max(RcSection.CeilTo(Math.Sqrt(Math.Max(Aest, 0) / asp), 0.1), MinSize);
        double L = 0, B = 0, Rv = 0;
        double pmax = 0, pmin = 0, ptb = 0;
        string gov = "";
        bool sized = false;
        double bs0 = bs;
        // [MóngLệchTâm] áp lực đáy móng: đối xứng (cũ) hoặc lệch tâm (EccentricFooting.Evaluate)
        void Eval(double LL, double BB)
        {
            if (ep == null) { EvalPressure(loads, LL, BB, kt, gtb, Df, out ptb, out pmax, out pmin, out gov); return; }
            epr = EccentricFooting.Evaluate(plan, ep, bl, LL, BB, kt, gtb, Df, out eox, out eoy);
            ptb = epr.Ptb; pmax = epr.Pmax; pmin = epr.Pmin; gov = epr.Combo;
        }

        if (given != null && given.L > 0 && given.B > 0)
        {
            L = given.L; B = given.B;
            Rv = RofSize(L, B);   // [CừTràm]
            Eval(L, B);   // [MóngLệchTâm]
            sized = true;
            d.Messages.Add("Kích thước đáy móng do người dùng chọn");
        }
        else
        {
            for (int it = 0; it < 200; it++)
            {
                double sl = RcSection.CeilTo(bs * asp, 0.1);
                L = longX ? sl : bs; B = longX ? bs : sl;
                if (cuOn && it > 0 && (L > cuLimX + 1e-9 || B > cuLimY + 1e-9)) { L = cuPL; B = cuPB; Rv = RofSize(L, B); Eval(L, B); cuCapped = true; break; }   // [CừTràm]
                cuPL = L; cuPB = B;
                Rv = RofSize(L, B);   // [CừTràm]
                Eval(L, B);   // [MóngLệchTâm]
                if (ep == null ? ptb <= Rv + 1e-6 && pmax <= 1.2 * Rv + 1e-6 && pmin >= -1e-6 : EccentricFooting.PressureOk(epr, Rv)) { sized = true; break; }
                if (ep != null && (Math.Abs(eox) > 1e-6 || Math.Abs(eoy) > 1e-6) && Math.Min(L, B) >= EccentricFooting.MaxSelfSize - 1e-9) break;   // [MóngLệchTâm] không tăng quá 4 m
                bs = Math.Round(bs + 0.1, 3);
            }
            // [MóngLệchTâm] lệch tâm tự cân bằng không thoả: lấy kích thước theo ptb ≤ R, báo lỗi + đề xuất dầm cân bằng / móng kết hợp
            if (!sized && ep != null && Math.Abs(eox) < 1e-6 && Math.Abs(eoy) < 1e-6)
                d.Messages.Add("LỖI không chọn được kích thước đáy móng thoả điều kiện áp lực nền – xem xét móng băng/móng cọc");
            else if (!sized && ep != null)
            {
                bs = bs0;
                for (int it = 0; it < 200; it++)
                {
                    double sl = RcSection.CeilTo(bs * asp, 0.1);
                    L = longX ? sl : bs; B = longX ? bs : sl;
                    if (cuOn && it > 0 && (L > cuLimX + 1e-9 || B > cuLimY + 1e-9)) { L = cuPL; B = cuPB; Rv = RofSize(L, B); Eval(L, B); cuCapped = true; break; }   // [CừTràm]
                    cuPL = L; cuPB = B;
                    Rv = RofSize(L, B);   // [CừTràm]
                    Eval(L, B);
                    if (ptb <= Rv + 1e-6) break;
                    bs = Math.Round(bs + 0.1, 3);
                }
                d.Messages.Add($"LỖI móng lệch tâm {(ep.Scheme == EccentricScheme.Strap ? "có dầm cân bằng" : "tự cân bằng")} không thoả áp lực nền với kích thước ≤ {EccentricFooting.MaxSelfSize:0.0} m "
                               + $"(pmax = {DesignUtil.F(pmax >= 1e8 ? 99999 : pmax, "0")} kN/m², tiếp xúc {epr.Contact * 100:0}%) – "
                               + (ep.Scheme == EccentricScheme.Strap ? "tăng kích thước / dùng móng cọc" : "dùng phương án (a) dầm móng cân bằng hoặc (c) móng kết hợp"));
            }
        }
        if (!sized && ep == null) d.Messages.Add("LỖI không chọn được kích thước đáy móng thoả điều kiện áp lực nền – xem xét móng băng/móng cọc");
        d.L = L; d.B = B; d.R = Rv; d.Ptb = ptb; d.Pmax = pmax; d.Pmin = pmin;

        // tổ hợp tính toán bất lợi (theo N lớn nhất và độ lệch tâm)
        var g = loads.OrderByDescending(l => l.N * (1 + 6 * (Math.Abs(l.My) / Math.Max(l.N, 1e-6)) / L + 6 * (Math.Abs(l.Mx) / Math.Max(l.N, 1e-6)) / B)).First();
        d.N = g.N; d.Mx = g.Mx; d.My = g.My; d.Qx = g.Qx; d.Qy = g.Qy; d.Ntc = g.N / kt; d.Combo = g.Combo;

        d.Checks.Add(new CheckItem { Name = "Áp lực trung bình đáy móng ptb ≤ R", Demand = ptb, Capacity = Rv, Unit = "kN/m²", Note = gov });
        d.Checks.Add(new CheckItem { Name = "Áp lực lớn nhất pmax ≤ 1,2R", Demand = pmax, Capacity = 1.2 * Rv, Unit = "kN/m²", Note = gov });
        if (ep != null && pmin < -1e-6)   // [MóngLệchTâm] tách nền một phần: điều kiện vùng tiếp xúc ≥ 75%
            d.Checks.Add(new CheckItem
            {
                Name = "Áp lực nhỏ nhất pmin ≥ 0 hoặc tiếp xúc ≥ 75% (lệch tâm)", Demand = 0.75, Capacity = Math.Max(epr.Contact, 0), Unit = "",
                Note = "pmin = " + DesignUtil.F(pmin, "0.0") + " kN/m², vùng tiếp xúc 3c0/L = " + DesignUtil.F(epr.Contact * 100, "0") + "%"
            });
        else
        d.Checks.Add(new CheckItem
        {
            Name = "Áp lực nhỏ nhất pmin ≥ 0", Demand = pmin < 0 ? -pmin : 0, Capacity = pmin < 0 ? 0 : 1, Unit = "kN/m²",
            Note = "pmin = " + DesignUtil.F(pmin, "0.0") + " kN/m²"
        });

        // ---- chiều cao móng theo chọc thủng (TCVN 5574:2018 mục 8.1.6.2) ----
        double cover = cs.CoverFooting;
        int dGuess = dias[Math.Min(1, dias.Length - 1)];
        double H = Math.Max(MinHeight, given != null && given.Height > 0 ? given.Height : MinHeight);
        double F = 0, Fu = 0;
        for (int it = 0; it < 100; it++)
        {
            double h0 = H - cover - dGuess / 1000.0;
            Punching(loads, L, B, hc, bc, h0, conc, out F, out Fu);
            if (ep != null)   // [MóngLệchTâm] chọc thủng cột sát mép móng: tháp và chu vi bị cắt bởi mép móng
            {
                F = 0;
                foreach (var l in bl)
                {
                    EccentricFooting.Punching(l.N, L, B, hc, bc, eox, eoy, h0, conc.Rbt, out double f1, out double fu1, out _);
                    F = Math.Max(F, f1); Fu = fu1;
                }
            }
            if (F <= Fu + 1e-6) break;
            if (given != null && given.Height > 0) break;
            H = Math.Round(H + 0.05, 3);
        }
        d.H = H; d.Hb = Math.Max(0.2, Math.Min(H, RcSection.CeilTo(H / 2, 0.05)));
        if (d.Hb > H) d.Hb = H;
        if (ep != null && (Math.Abs(eox) > 1e-6 || Math.Abs(eoy) > 1e-6)) d.Hb = H;   // [MóngLệchTâm] móng lệch tâm: đế phẳng (không vát)
        d.Checks.Add(new CheckItem { Name = "Chọc thủng F ≤ Rbt·u·h0", Demand = F, Capacity = Fu, Unit = "kN", Note = "H = " + DesignUtil.F(H * 1000, "0") + " mm" });

        // ---- cốt thép đáy (uốn tại mép cột, áp lực tính toán phía pmax) ----
        double mL = 0, mB = 0;
        foreach (var l in loads)
        {
            if (l.N <= 0) continue;
            double area = L * B;
            // áp lực ròng (không kể trọng lượng móng và đất)
            double exL = (Math.Abs(l.My) + Math.Abs(l.Qx) * H) * 6 / (B * L * L);
            double exB = (Math.Abs(l.Mx) + Math.Abs(l.Qy) * H) * 6 / (L * B * B);
            double p0 = l.N / area;
            // phương L: công xôn dài (L − hc)/2, áp lực biên pmax, tại mép cột pI (tuyến tính)
            double cl = (L - hc) / 2, cb = (B - bc) / 2;
            double pmL = p0 + exL, pIL = p0 + exL * (hc / L);
            mL = Math.Max(mL, B * cl * cl * (2 * pmL + pIL) / 6);
            double pmB = p0 + exB, pIB = p0 + exB * (bc / B);
            mB = Math.Max(mB, L * cb * cb * (2 * pmB + pIB) / 6);
        }
        if (ep != null) (mL, mB) = EccentricFooting.Bending(plan, ep, bl, L, B, hc, bc, eox, eoy, H);   // [MóngLệchTâm] công xôn lệch tâm, áp lực tuyến tính / tam giác
        Material SteelOf(int dia) => dia < 10 ? stlSmall : main;
        d.ML = mL; d.MB = mB;
        string basis = Basis(L, B, H);
        int nUser = 0;
        SpacingChoice Ovr(string zone, SpacingChoice auto, Func<int, double> req)
        {
            var ch = p.ValidRebarChoice(key, zone, -1, basis);
            if (ch == null || ch.Dia <= 0 || ch.Spacing <= 0) return auto;
            nUser++;
            var sc = new SpacingChoice { Dia = ch.Dia, Spacing = ch.Spacing, AsReqPerM = req(ch.Dia), Ok = true };
            if (ch.Spacing < 0.10 - 1e-9 || ch.Spacing > 0.20 + 1e-9)
                d.Messages.Add("LỖI bố trí thép do người dùng chọn không đạt – " + zone + " " + sc.Text + ": bước ngoài phạm vi 100…200 mm");
            if (sc.AsPerM < sc.AsReqPerM * (1 - 1e-6))
                d.Messages.Add("LỖI bố trí thép do người dùng chọn không đạt – " + zone + " " + sc.Text + ": As = " + DesignUtil.F(sc.AsPerM * 1e4, "0.00")
                               + " < As yêu cầu " + DesignUtil.F(sc.AsReqPerM * 1e4, "0.00") + " cm²/m");
            return sc;
        }
        // lớp dưới cùng theo phương L (h0 lớn), lớp trên theo phương B
        var sL = Ovr(RebarZones.FootingL, RcSection.ChooseSpacing(dia => AsPerM(mL / B, H - cover - dia / 2000.0, SteelOf(dia)), dias, 0.20, 0.10, 0.15, 10),
            dia => AsPerM(mL / B, H - cover - dia / 2000.0, SteelOf(dia)));
        double dl = sL.Dia / 1000.0;
        var sB = Ovr(RebarZones.FootingB, RcSection.ChooseSpacing(dia => AsPerM(mB / L, H - cover - dl - dia / 2000.0, SteelOf(dia)), dias, 0.20, 0.10, 0.15, 10),
            dia => AsPerM(mB / L, H - cover - dl - dia / 2000.0, SteelOf(dia)));
        d.UserRebarZones = nUser;
        if (nUser > 0) d.Messages.Add("Bố trí thép theo lựa chọn của người dùng tại " + nUser + " vùng (đã kiểm tra lại khả năng chịu lực)");
        d.AsL = sL.AsReqPerM * B; d.AsB = sB.AsReqPerM * L;
        d.DiaL = sL.Dia; d.DiaB = sB.Dia; d.SpacingL = sL.Spacing; d.SpacingB = sB.Spacing;
        d.BarsL = sL.Text; d.BarsB = sB.Text;
        if (!sL.Ok || !sB.Ok) d.Messages.Add("LỖI không bố trí đủ thép đáy móng với bước ≥ 100 mm – tăng chiều cao móng");
        double h0L = H - cover - dl, h0B = H - cover - dl - sB.Dia / 2000.0;
        d.Checks.Add(new CheckItem
        {
            Name = "Uốn phương L tại mép cột", Demand = mL, Unit = "kN.m", Note = sL.Text,
            Capacity = RcSection.MomentCapacity(B, h0L, sL.AsPerM * B, 0, cover, conc, SteelOf(sL.Dia))
        });
        d.Checks.Add(new CheckItem
        {
            Name = "Uốn phương B tại mép cột", Demand = mB, Unit = "kN.m", Note = sB.Text,
            Capacity = RcSection.MomentCapacity(L, h0B, sB.AsPerM * L, 0, cover, conc, SteelOf(sB.Dia))
        });

        // ---- bố trí thép (lưới đáy, bẻ móc lên 2 đầu) ----
        double c = cover;
        double hookL = DesignUtil.R10(Math.Max(10 * dl, 0.10));
        double hookB = DesignUtil.R10(Math.Max(10 * sB.Dia / 1000.0, 0.10));
        double lenL = DesignUtil.R10(L - 2 * c), lenB = DesignUtil.R10(B - 2 * c);
        d.Bars.Add(new BarMark
        {
            Role = "Lớp dưới phương L", Dia = sL.Dia, Spacing = sL.Spacing, Count = (int)Math.Ceiling((B - 2 * c) / sL.Spacing - 1e-9) + 1,
            Length = DesignUtil.R10(lenL + 2 * hookL), Shape = "U", Start = c, End = L - c,
            Segments = new List<double> { DesignUtil.Mm(hookL), DesignUtil.Mm(lenL), DesignUtil.Mm(hookL) }
        });
        d.Bars.Add(new BarMark
        {
            Role = "Lớp dưới phương B", Dia = sB.Dia, Spacing = sB.Spacing, Count = (int)Math.Ceiling((L - 2 * c) / sB.Spacing - 1e-9) + 1,
            Length = DesignUtil.R10(lenB + 2 * hookB), Shape = "U", Start = c, End = B - c,
            Segments = new List<double> { DesignUtil.Mm(hookB), DesignUtil.Mm(lenB), DesignUtil.Mm(hookB) }
        });
        if (geoBh != null && !cuOn) AddSettlementCheck(d, geoBh, soil, L, B, ptb);
        if (cuOn)   // [CừTràm] kết quả 2 phương pháp + kiểm tra độ bền (MNN), lún khối quy ước, áp lực mũi cừ
        {
            d.CuTram = CuTramCalc.Evaluate(p, n.X, n.Y, soil, L, B, ptb, pmax, loads.Max(l => l.N) / kt);
            if (cuCapped || !sized)
                CuTramCalc.MarkInfeasible(d.CuTram, d.Messages, cuCapped
                    ? $"móng cần lớn hơn 0,5·bước cột (giới hạn {DesignUtil.F(Math.Min(cuLimX, cuLimY), "0.00")} m) mà vẫn chưa thoả [p] – dừng ở {DesignUtil.F(L, "0.00")}×{DesignUtil.F(B, "0.00")} m"
                    : "không chọn được kích thước móng thoả [p] của nền gia cố");
            CuTramCalc.AddChecks(d.Checks, d.Messages, d.CuTram);
        }
        if (ep != null) AttachEccentric(p, plan, ep, d, eox, eoy, epr, L, B, kt, gtb, Df, Rv);   // [MóngLệchTâm]
        // [FoundationChecks] sự làm việc móng đơn: móng cứng, lệch tâm / tách nền, trượt, chọc thủng có mô men, cắt (không thêm CheckItem)
        try { KetCauVXT.Design.Foundation.FootingBehaviour.AnalyzeIsolated(d, soil, conc, cover); }
        catch (Exception ex) { d.Messages.Add("Cảnh báo: không phân tích được sự làm việc móng – " + ex.Message); }
        RebarNumbering.Renumber(d.Bars);
        return d;
    }

    /// <summary>[MóngLệchTâm] Ghi kết quả lệch tâm vào móng: EccentricInfo, liên kết dầm cân bằng, ΔR cho móng cột trong, thông báo.</summary>
    private static void AttachEccentric(Project p, EccPlan plan, EccNode ep, FootingDesign d, double ox, double oy, PressureResult pr, double L, double B,
        double kt, double gtb, double Df, double R)
    {
        EccentricFooting.Record(plan, ep, ox, oy, L, B, "", false);
        bool off = Math.Abs(ox) > 1e-6 || Math.Abs(oy) > 1e-6;
        if (!off && !ep.StrapX && !ep.StrapY && ep.RelievedBy.Count == 0) return;
        var info = EccentricFooting.Info(plan, ep, ox, oy, pr, L, B);
        info.Straps.AddRange(EccentricFooting.Links(plan, ep, L, B, "", false));
        if (ep.Limits.EdgeX || ep.Limits.EdgeY) info.Options = EccentricFooting.CompareOptions(plan, ep, L, B, kt, gtb, Df, R);
        d.Eccentric = info;
        string where = DesignUtil.GridName(p, d.X, d.Y);
        if (off)
            d.Messages.Add($"Móng lệch tâm ({(info.Boundary ? "ranh giới " + info.Sides : "nhập tay")}): tâm đáy móng lệch so với tim cột ex = {ox * 1000:0} mm, ey = {oy * 1000:0} mm – "
                           + EccentricOverride.SchemeName(ep.Scheme) + (ep.Scheme != ep.Recommended ? " (đề xuất: " + EccentricOverride.SchemeName(ep.Recommended) + ")" : ""));
        if (ep.StrapX || ep.StrapY)
            d.Messages.Add($"Dầm móng cân bằng: R1 = N1 + ΔR = {info.N1:0.0} + {info.DeltaR:0.0} = {info.R1:0.0} kN tại trọng tâm đáy móng (đòn bẩy R1 = N1·L/(L − e)) – áp lực đáy móng gần đều");
        if (ep.RelievedBy.Count > 0 && info.Relief > 1e-6)
            d.Messages.Add($"Móng cột trong được giảm tải do dầm cân bằng từ nút {string.Join(", ", info.ReliefFrom)}: N2' = N2 − β·ΔR, β = {plan.Fs.StrapReliefFactor:0.##} → giảm {info.Relief:0.0} kN (tổ hợp N lớn nhất)");
        if (ep.Scheme == EccentricScheme.SelfBalanced && off && (info.ColumnMomentX > 1e-3 || info.ColumnMomentY > 1e-3))
            d.Messages.Add($"Cảnh báo: móng lệch tâm tự cân bằng tại {where} – mô men lệch tâm ΔM = N·e0 = {info.ColumnMomentX:0.0} / {info.ColumnMomentY:0.0} kN.m (theo X / Y) "
                           + "truyền vào chân cột và hệ giằng móng; cần kiểm tra cột tầng 1 + giằng móng với mô men này (chưa kể vào thiết kế cột).");
        if (pr.Partial && off)
            d.Messages.Add($"Cảnh báo: hợp lực ra ngoài lõi tiết diện (tách nền một phần) – pmax = 2N/(3B·c0) = {(pr.Pmax >= 1e8 ? 99999 : pr.Pmax):0} kN/m², vùng tiếp xúc {pr.Contact * 100:0}%");
    }

    /// <summary>Kiểm tra lún khi nền lấy theo hố khoan: S = GeotechCalc.Settlement(hố khoan, L, B, Df, ptb) ≤ Sgh = 8 cm
    /// (thêm vào cuối danh sách Checks; không thêm khi không dùng hố khoan).</summary>
    private static void AddSettlementCheck(FootingDesign d, Borehole bh, SoilSettings soil, double L, double B, double ptb)
    {
        var st = KetCauVXT.Design.Foundation.GeotechCalc.Settlement(bh, L, B, soil.Depth, ptb);
        d.Checks.Add(new CheckItem
        {
            Name = "Độ lún S ≤ Sgh", Demand = st.S * 100, Capacity = st.Limit * 100, Unit = "cm",
            Note = "hố khoan " + bh.Name + ", Hc = " + DesignUtil.F(st.Hc, "0.0") + " m"
        });
        d.Messages.Add("Nền theo hố khoan " + bh.Name + ": φ = " + DesignUtil.F(soil.Phi, "0.#") + "°, c = " + DesignUtil.F(soil.C, "0.#") + " kPa, γ = " + DesignUtil.F(soil.Gamma, "0.##") + " kN/m³");
        var weak = KetCauVXT.Design.Foundation.GeotechCalc.WeakLayerBelow(bh, soil.Depth, Math.Min(L, B));
        if (weak != null) d.Messages.Add("Cảnh báo: lớp đất yếu \"" + weak.Name + "\" ở độ sâu " + DesignUtil.F(weak.Top, "0.0") + " m nằm trong phạm vi 2B dưới đáy móng – xem xét móng cọc");
    }

    /// <summary>Khoá cấu kiện vật lý của móng đơn (lựa chọn thép): "M:{id nút chân cột}".</summary>
    internal static string MemberKey(int nodeId) => "M:" + nodeId.ToString(DesignUtil.Inv);

    internal static string Basis(double L, double B, double H) =>
        DesignUtil.Mm(L).ToString("0", DesignUtil.Inv) + "x" + DesignUtil.Mm(B).ToString("0", DesignUtil.Inv) + "x" + DesignUtil.Mm(H).ToString("0", DesignUtil.Inv);

    /// <summary>Các phương án Ø–bước cho thép đáy móng đơn đã thiết kế (zone = RebarZones.FootingL / FootingB).</summary>
    internal static List<RebarAlternative> Alternatives(Project p, FootingDesign d, string zone, int max = 12)
    {
        var cs = p.Concrete;
        var main = p.Material(cs.RebarMain);
        var stlSmall = p.Material(cs.RebarStirrup);
        Material SteelOf(int dia) => dia < 10 ? stlSmall : main;
        int[] dias = (p.RebarPrefs ?? new RebarPreferences()).Filter(DesignUtil.Sorted(cs.FootingBars, new[] { 10, 12, 14, 16 }));
        double cover = cs.CoverFooting, H = d.H, dl = d.DiaL / 1000.0;
        bool alongL = zone == RebarZones.FootingL;
        Func<int, double> req = alongL
            ? dia => AsPerM(d.ML / Math.Max(d.B, 1e-6), H - cover - dia / 2000.0, SteelOf(dia))
            : dia => AsPerM(d.MB / Math.Max(d.L, 1e-6), H - cover - dl - dia / 2000.0, SteelOf(dia));
        return RebarOptions.SpacingAlternatives("Móng", zone, req, dias.Where(x => x >= 10).DefaultIfEmpty(dias.Max()).ToArray(), 0.10, 0.20, alongL ? d.BarsL : d.BarsB, max);
    }

    /// <summary>Diện tích thép trên 1 m: As = M/(0,9·Rs·h0) (gần đúng cánh tay đòn z = 0,9h0).</summary>
    private static double AsPerM(double mPerM, double h0, Material steel)
    {
        if (h0 <= 0) return 1;
        double asMin = 0.0005 * h0;   // μmin = 0,05% (TCVN 5574:2018 mục 10.3.3) theo h0 trên 1 m
        return Math.Max(mPerM / (0.9 * steel.Rs * h0), asMin);
    }

    /// <summary>Áp lực đáy móng tiêu chuẩn: p = Ntc/A + γtb·Df ± 6Mx/(L·B²) ± 6My/(B·L²), M kể cả lực cắt × Df;
    /// trả về giá trị bất lợi nhất qua các tổ hợp (nội lực tiêu chuẩn = tính toán / hệ số vượt tải trung bình).</summary>
    private static void EvalPressure(List<Load> loads, double L, double B, double kt, double gtb, double Df,
        out double ptb, out double pmax, out double pmin, out string combo)
    {
        ptb = double.MinValue; pmax = double.MinValue; pmin = double.MaxValue; combo = "";
        double A = L * B;
        foreach (var l in loads)
        {
            double N = l.N / kt;
            double my = (Math.Abs(l.My) + Math.Abs(l.Qx) * Df) / kt;
            double mx = (Math.Abs(l.Mx) + Math.Abs(l.Qy) * Df) / kt;
            double p0 = N / A + gtb * Df;
            double dp = 6 * my / (B * L * L) + 6 * mx / (L * B * B);
            if (p0 > ptb) ptb = p0;
            if (p0 + dp > pmax) { pmax = p0 + dp; combo = l.Combo; }
            pmin = Math.Min(pmin, p0 - dp);
        }
    }

    /// <summary>Kiểm tra chọc thủng (TCVN 5574:2018 mục 8.1.6.2, công thức 8.88): F ≤ Fb,u = Rbt·u·h0,
    /// u = chu vi đường bao cách mép cột h0/2: 2·(hc + h0 + bc + h0);
    /// F = N − p_net·(hc + 2h0)(bc + 2h0) với p_net = N/(L·B) (áp lực ròng tính toán trung bình, không kể trọng lượng móng;
    /// mô men không làm thay đổi tổng phản lực trong tháp chọc thủng đối xứng nên bỏ qua).</summary>
    private static void Punching(List<Load> loads, double L, double B, double hc, double bc, double h0, Material conc, out double F, out double Fu)
    {
        F = 0;
        double u = 2 * (hc + h0 + bc + h0);
        Fu = conc.Rbt * u * Math.Max(h0, 0);
        foreach (var l in loads)
        {
            if (l.N <= 0) continue;
            double pnet = l.N / (L * B);
            double ap = Math.Min((hc + 2 * h0) * (bc + 2 * h0), L * B);
            double f = l.N - pnet * ap;
            if (f > F) F = f;
        }
    }

    private static string Signature(FootingDesign d) =>
        Math.Round(d.L * 100) + "x" + Math.Round(d.B * 100) + "x" + Math.Round(d.H * 100) + "|" + Math.Round(d.ColumnB * 1000) + "x" + Math.Round(d.ColumnH * 1000)
        + "|" + d.BarsL + "|" + d.BarsB + "|" + RebarNumbering.Signature(d.Bars) + EccentricFooting.Sig(d.Eccentric)   // [MóngLệchTâm]
        + (d.CuTram != null ? "|CT" + d.CuTram.Count : "")   // [CừTràm] móng có / không gia cố cừ tràm không gộp
        + ShapeSig(d);   // [MóngHìnhDạng]
}
