using KetCauVXT.Analysis;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Thiết kế ô sàn BTCT chữ nhật toàn khối theo dải bản 1 m (TCVN 5574:2018 mục 8.1.2.2 cho uốn, mục 10.3 cấu tạo).
/// Bản dầm (ly/lx &gt; 2) tính như dải một phương; bản kê bốn cạnh dùng phương pháp Marcus: chia tải theo điều kiện
/// võng bằng nhau tại giữa ô, bỏ qua giảm mô men do xoắn – thiên về an toàn.</summary>
public static partial class SlabDesigner
{
    /// <summary>Hệ số độ cứng khi nứt dùng ước lượng độ võng: tham khảo; độ võng dải sàn tính theo độ cứng tiết diện có vết nứt.</summary>
    public const double CrackedStiffnessFactor = 0.3;
    /// <summary>Hàm lượng thép tối thiểu sàn (%)</summary>
    public const double MinRatioSlab = 0.10;

    internal enum EdgeKind { Simple, Fixed }

    /// <summary>Thiết kế toàn bộ ô sàn. r có thể null (sàn tính từ tải khai báo, không dùng kết quả phân tích khung).</summary>
    public static List<SlabDesign> Design(Project p, AnalysisResults r, List<string> warnings)
    {
        warnings ??= new List<string>();
        var list = new List<(SlabDesign d, string sig)>();
        // lượt 1: nội lực, lớp dưới, yêu cầu thép trên từng ô
        var panels = new List<SlabPanel>();
        foreach (var slab in p.Slabs)
        {
            try
            {
                var pn = DesignSlab(p, slab, r);
                if (pn == null) { warnings.Add("Sàn " + slab.Label + " không phải ô chữ nhật – bỏ qua thiết kế"); continue; }
                panels.Add(pn);
            }
            catch (Exception ex)
            {
                warnings.Add("LỖI thiết kế sàn " + slab.Label + ": " + ex.Message);
            }
        }
        // lượt 2: thép trên theo phương án (mũ cạnh chung tính MỘT lần, lưới trên liên tục) – SlabDesigner.Top.cs
        try { TopReinforcement(p, panels); }
        catch (Exception ex) { warnings.Add("LỖI bố trí thép trên sàn: " + ex.Message); }
        foreach (var pn in panels)
        {
            var d = pn.D;
            RebarNumbering.Renumber(d.Bars);
            string sig = Signature(d);
            int k = list.FindIndex(x => x.sig == sig);
            if (k >= 0)
            {
                var keep = list[k].d;
                keep.Quantity++;
                keep.RebarKeys.AddRange(d.RebarKeys.Where(x => !keep.RebarKeys.Contains(x)));
                keep.EdgeBars.AddRange(d.EdgeBars);
                keep.TopMeshes.AddRange(d.TopMeshes);
            }
            else list.Add((d, sig));
        }
        // đặt tên S{n} theo tầng, giữ thứ tự khai báo
        var result = new List<SlabDesign>();
        foreach (var g in list.Select(x => x.d).GroupBy(d => d.Story).OrderBy(g => DesignUtil.StoryOrder(p, g.Key)))
        {
            int n = 1;
            string sn = DesignUtil.StoryNo(g.Key);
            foreach (var d in g)
            {
                d.Name = "S" + sn + "-" + n++;
                result.Add(d);
            }
        }
        return result;
    }

    private static SlabPanel DesignSlab(Project p, Slab slab, AnalysisResults r = null)
    {
        var pts = slab.Nodes.Select(id => p.Node(id).P).ToList();
        if (!SlabLoadDistributor.IsRectangle(pts, out double la, out double lb)) return null;
        var cs = p.Concrete;
        var sec = p.SlabSection(slab.Section);
        var conc = DesignUtil.ConcreteOf(p, sec.Material, cs.ConcreteSlab);
        // thép sàn: Ø < 10 dùng thép đai, Ø ≥ 10 dùng thép chính
        var stlSmall = p.Material(cs.RebarStirrup);
        var stlMain = p.Material(cs.RebarMain);
        int[] dias = (p.RebarPrefs ?? new RebarPreferences()).Filter(DesignUtil.Sorted(cs.SlabBars, new[] { 6, 8, 10, 12 }));
        double t = sec.Thickness;

        // ---- tải tính toán và tiêu chuẩn ----
        double q = 0, qs = 0;
        foreach (var pat in p.Patterns)
        {
            double qi = pat.SelfWeight * conc.UnitWeight * t;
            qi += p.AreaLoads.Where(a => a.Slab == slab.Id && a.Pattern == pat.Name).Sum(a => a.Q);
            if (pat.Type is LoadType.Wind or LoadType.Seismic) continue;
            q += pat.Gamma * qi;
            qs += qi;
        }

        // ---- điều kiện biên ----
        var edges = new EdgeKind[4];
        for (int i = 0; i < 4; i++)
        {
            if (slab.EdgeFixed != null && slab.EdgeFixed.Count == 4) edges[i] = slab.EdgeFixed[i] ? EdgeKind.Fixed : EdgeKind.Simple;
            else edges[i] = HasNeighbour(p, slab, pts[i], pts[(i + 1) % 4]) ? EdgeKind.Fixed : EdgeKind.Simple;
        }
        // cạnh 0 và 2 có chiều dài la; cạnh 1 và 3 có chiều dài lb
        bool aShort = la <= lb;
        double lx = Math.Min(la, lb), ly = Math.Max(la, lb);
        if (lx < 1e-3) return null;   // ô suy biến (nút trùng) – tránh chia cho 0
        // nhịp phương ngắn (lx) được đỡ bởi 2 cạnh dài
        var longEdges = aShort ? new[] { 1, 3 } : new[] { 0, 2 };
        var shortEdges = aShort ? new[] { 0, 2 } : new[] { 1, 3 };
        int fx = longEdges.Count(i => edges[i] == EdgeKind.Fixed);
        int fy = shortEdges.Count(i => edges[i] == EdgeKind.Fixed);
        // phương ngắn trong hệ toạ độ tổng thể
        var vShort = aShort ? pts[1] - pts[0] : pts[2] - pts[1];
        bool shortIsX = Math.Abs(vShort.X) >= Math.Abs(vShort.Y);

        var d = new SlabDesign
        {
            SlabId = slab.Id, Story = slab.Story ?? "", Lx = lx, Ly = ly, Thickness = t, Q = q,
            TwoWay = ly / lx <= 2.0, Elevation = pts.Max(v => v.Z),
            Outline = pts.Select(v => (v.X, v.Y)).ToList()
        };
        int nf = edges.Count(e => e == EdgeKind.Fixed);
        d.EdgeCondition = nf == 4 ? "4 cạnh ngàm" : nf == 0 ? "4 cạnh khớp" : nf + " cạnh ngàm, " + (4 - nf) + " cạnh khớp";

        // ---- nội lực ----
        double qx, qy;
        if (d.TwoWay)
        {
            // Marcus: qx·cx·lx⁴ = qy·cy·ly⁴, qx + qy = q
            double cx = DeflCoef(fx), cy = DeflCoef(fy);
            double kx = cx * Math.Pow(lx, 4), ky = cy * Math.Pow(ly, 4);
            qx = q * ky / (kx + ky);
            qy = q - qx;
            d.Messages.Add("Bản kê 4 cạnh – phương pháp Marcus, bỏ qua giảm mô men xoắn – thiên về an toàn; qx = "
                           + DesignUtil.F(qx, "0.00") + ", qy = " + DesignUtil.F(qy, "0.00") + " kN/m²");
        }
        else
        {
            qx = q; qy = 0;
            d.Messages.Add("Bản loại dầm (ly/lx = " + DesignUtil.F(ly / lx, "0.00") + " > 2) – tính dải 1 m theo phương ngắn");
        }
        StripMoments(qx, lx, fx, out double m1, out double mI);
        double m2, mII;
        if (d.TwoWay) StripMoments(qy, ly, fy, out m2, out mII);
        else { m2 = 0; mII = 0; }
        // mô men dải sàn nhập từ SAFE/ETABS (thay phương pháp hệ số) – SlabDesigner.External.cs
        ApplyExternalMoments(r, slab, d, shortIsX, ref m1, ref mI, ref m2, ref mII);
        d.M1 = m1; d.M2 = m2; d.MI = mI; d.MII = mII;

        // ---- thép (dải b = 1 m) ----
        double c = cs.CoverSlab;
        double sMax = Math.Min(1.5 * t, 0.20);
        Material SteelOf(int dia) => dia < 10 ? stlSmall : stlMain;
        double Req(double m, double extraDepth, int dia)
        {
            double h0 = t - c - extraDepth - dia / 2000.0;
            var fr = RcSection.DesignRect(m, 1.0, h0, c, conc, SteelOf(dia), MinRatioSlab);
            return fr.As;
        }
        SpacingChoice Choose(double m, double extraDepth) => RcSection.ChooseSpacing(dia => Req(m, extraDepth, dia), dias, sMax, 0.10, 0.15);
        // lựa chọn của người dùng (Project.RebarChoices) thay cho phương án tự chọn
        string key = MemberKey(slab), basis = Basis(t);
        d.RebarKeys.Add(key);
        int nUser = 0;
        SpacingChoice Ovr(string zone, SpacingChoice auto, Func<int, double> req, double sMaxZ)
        {
            var ch = p.ValidRebarChoice(key, zone, -1, basis);
            if (ch == null || ch.Dia <= 0 || ch.Spacing <= 0) return auto;
            nUser++;
            var sc = new SpacingChoice { Dia = ch.Dia, Spacing = ch.Spacing, AsReqPerM = req(ch.Dia), Ok = true };
            if (ch.Spacing < 0.10 - 1e-9 || ch.Spacing > sMaxZ + 1e-9)
                d.Messages.Add("LỖI bố trí thép do người dùng chọn không đạt – " + zone + " " + sc.Text + ": bước ngoài phạm vi 100…" + DesignUtil.F(sMaxZ * 1000, "0") + " mm");
            if (sc.AsPerM < sc.AsReqPerM * (1 - 1e-6))
                d.Messages.Add("LỖI bố trí thép do người dùng chọn không đạt – " + zone + " " + sc.Text + ": As = " + DesignUtil.F(sc.AsPerM * 1e4, "0.00")
                               + " < As yêu cầu " + DesignUtil.F(sc.AsReqPerM * 1e4, "0.00") + " cm²/m");
            return sc;
        }
        // lớp dưới: phương ngắn đặt ngoài (h0 lớn), phương dài đặt trong
        var bx = Ovr(RebarZones.SlabBotX, Choose(m1, 0), dia => Req(m1, 0, dia), sMax);
        var by = Choose(m2, bx.Dia / 1000.0);
        var tx = mI < -1e-9 ? Choose(-mI, 0) : null;
        SpacingChoice ty = null;
        if (mII < -1e-9) ty = Choose(-mII, 0);
        else if (!d.TwoWay && fy > 0) ty = RcSection.ChooseSpacing(_ => 0.0, dias, 0.20, 0.10, 0.20);   // mũ cấu tạo cạnh ngắn bản dầm
        // phương dài của bản dầm: thép phân bố ≥ 10% thép chịu lực (TCVN 5574:2018 mục 10.3.8)
        double needDist = Math.Max(0.1 * bx.AsPerM, 0), sMaxDist = Math.Min(0.25, Math.Max(sMax, 0.20));
        if (!d.TwoWay)
            by = RcSection.ChooseSpacing(_ => needDist, dias, sMaxDist, 0.10, 0.20);
        double bxDepth = bx.Dia / 1000.0;
        by = Ovr(RebarZones.SlabBotY, by, dia => d.TwoWay ? Req(m2, bxDepth, dia) : needDist, d.TwoWay ? sMax : sMaxDist);
        tx = Ovr(RebarZones.SlabTopX, tx, dia => mI < -1e-9 ? Req(-mI, 0, dia) : 0, sMax);
        ty = Ovr(RebarZones.SlabTopY, ty, dia => mII < -1e-9 ? Req(-mII, 0, dia) : 0, Math.Max(sMax, 0.20));
        d.UserRebarZones = nUser;
        if (nUser > 0) d.Messages.Add("Bố trí thép theo lựa chọn của người dùng tại " + nUser + " vùng (đã kiểm tra lại khả năng chịu lực)");
        d.BotX = bx.Text; d.BotY = by.Text; d.TopX = tx?.Text ?? ""; d.TopY = ty?.Text ?? "";
        foreach (var sc in new[] { bx, by, tx, ty })
            if (sc != null && !sc.Ok) d.Messages.Add("LỖI sàn không bố trí đủ thép với bước ≥ 100 mm – cần tăng chiều dày");

        AddCheck(d, "Uốn nhịp phương ngắn", m1, bx, t, c, 0, conc, SteelOf(bx.Dia));
        if (d.TwoWay) AddCheck(d, "Uốn nhịp phương dài", m2, by, t, c, bx.Dia / 1000.0, conc, SteelOf(by.Dia));
        // kiểm tra uốn gối: sau khi bố trí thép trên (mũ cạnh chung / lưới) – SlabDesigner.Top.cs

        // ---- võng (TTGH II) ----
        double qxs = q > 1e-9 ? qx * qs / q : 0;
        // dải 1 m phương ngắn: Ms tiêu chuẩn tỉ lệ với mô men nhịp tính toán; B kể nứt + từ biến theo thép thực bố trí
        double msx = q > 1e-9 ? m1 * qs / q : 0;
        double h0x = t - c - bx.Dia / 2000.0;
        double D = RcSection.FlexuralStiffness(1.0, t, h0x, bx.AsPerM, conc, SteelOf(bx.Dia).E, msx);
        double f = D > 0 ? DeflCoef(fx) / 384.0 * qxs * Math.Pow(lx, 4) / D : 0;
        // bản kê 4 cạnh: hệ số Marcus kể xoắn cho độ võng ν = 1 − 5/6·lx²ly²/(lx⁴+ly⁴) (ô vuông khớp: 0,00406·q·a⁴/D – khớp lời giải tấm)
        if (d.TwoWay) f *= 1 - 5.0 / 6.0 * lx * lx * ly * ly / (Math.Pow(lx, 4) + Math.Pow(ly, 4));
        d.Deflection = f;
        d.Checks.Add(new CheckItem
        {
            Name = "Võng sàn", Demand = f * 1000, Capacity = lx / 200 * 1000, Unit = "mm",
            Note = "f = c/384·qx·lx⁴/B·ν (c = 5 khớp–khớp, 2 ngàm–khớp, 1 ngàm–ngàm; ν hệ số Marcus khi bản kê), B = Eb/(1+φcr)·Ieff kể nứt + từ biến, giới hạn lx/200"
        });
        double tMin = d.TwoWay ? lx / 45 : lx / 35;
        if (t < tMin - 1e-6) d.Messages.Add("Chiều dày sàn " + DesignUtil.F(t * 1000, "0") + " mm nhỏ hơn khuyến nghị " + DesignUtil.F(tMin * 1000, "0") + " mm");

        // ---- bố trí thép lớp dưới (thép trên ở lượt 2) ----
        Detail(d, pts, aShort, shortIsX, bx, by, tx, ty, edges, longEdges, shortEdges, c, t, lx, ly);
        d.FixedEdgeLenShort = longEdges.Count(i => edges[i] == EdgeKind.Fixed) * ly;
        d.FixedEdgeLenLong = shortEdges.Count(i => edges[i] == EdgeKind.Fixed) * lx;
        return new SlabPanel
        {
            Slab = slab, D = d, Pts = pts, Edges = edges, LongEdges = longEdges, ShortEdges = shortEdges, ShortIsX = shortIsX,
            Lx = lx, Ly = ly, T = t, C = c, Conc = conc, SteelOf = SteelOf, MI = mI, MII = mII, Tx = tx, Ty = ty, Dias = dias, SMax = sMax,
            Key = key, Basis = basis, OneWay = !d.TwoWay
        };
    }

    /// <summary>Hệ số độ võng giữa dải: 5 (khớp–khớp), 2,08 (ngàm–khớp), 1 (ngàm–ngàm), từ f = c·q·l⁴/(384·EI).</summary>
    private static double DeflCoef(int fixedEnds) => fixedEnds >= 2 ? 1.0 : fixedEnds == 1 ? 2.08 : 5.0;

    /// <summary>Mô men dải bản: khớp–khớp M = qL²/8; ngàm–khớp Mnh = 9qL²/128, Mg = −qL²/8; ngàm–ngàm Mnh = qL²/24, Mg = −qL²/12.</summary>
    private static void StripMoments(double q, double L, int fixedEnds, out double mSpan, out double mSup)
    {
        double ql2 = q * L * L;
        if (fixedEnds >= 2) { mSpan = ql2 / 24; mSup = -ql2 / 12; }
        else if (fixedEnds == 1) { mSpan = 9 * ql2 / 128; mSup = -ql2 / 8; }
        else { mSpan = ql2 / 8; mSup = 0; }
    }

    private static void AddCheck(SlabDesign d, string name, double m, SpacingChoice sc, double t, double c, double extra, Material conc, Material steel)
    {
        double h0 = t - c - extra - sc.Dia / 2000.0;
        double mu = RcSection.MomentCapacity(1.0, h0, sc.AsPerM, 0, c, conc, steel);
        d.Checks.Add(new CheckItem { Name = name, Demand = Math.Abs(m), Capacity = mu, Unit = "kN.m/m", Note = sc.Text });
    }

    /// <summary>Cạnh [a, b] là ngàm nếu có ô sàn khác (cùng cao độ) có cạnh thẳng hàng và chồng lên đoạn [a, b].</summary>
    private static bool HasNeighbour(Project p, Slab slab, Vec3 a, Vec3 b)
    {
        var u = (b - a); double le = u.Length; u = u.Normalize();
        foreach (var o in p.Slabs)
        {
            if (o.Id == slab.Id || o.Nodes.Count < 3) continue;
            var op = o.Nodes.Select(id => p.Node(id).P).ToList();
            for (int i = 0; i < op.Count; i++)
            {
                Vec3 c = op[i], dd = op[(i + 1) % op.Count];
                if (Math.Abs(c.Z - a.Z) > 0.01 || Math.Abs(dd.Z - a.Z) > 0.01) continue;
                if (!OnLine(a, u, c, out double t1) || !OnLine(a, u, dd, out double t2)) continue;
                double lo = Math.Max(0, Math.Min(t1, t2)), hi = Math.Min(le, Math.Max(t1, t2));
                if (hi - lo > 0.1 * le) return true;
            }
        }
        return false;
    }

    private static bool OnLine(Vec3 a, Vec3 u, Vec3 p, out double t)
    {
        var d = p - a;
        t = d.Dot(u);
        return (d - u * t).Length < 0.01;
    }

    /// <summary>Bố trí thép sàn lớp dưới: hai phương chạy suốt ô (neo vào dầm 10d mỗi đầu).
    /// Thép trên (mũ gối vươn ¼ nhịp phương ngắn từ mép dầm, lưới trên) bố trí ở TopReinforcement.</summary>
    private static void Detail(SlabDesign d, List<Vec3> pts, bool aShort, bool shortIsX, SpacingChoice bx, SpacingChoice by,
        SpacingChoice tx, SpacingChoice ty, EdgeKind[] edges, int[] longEdges, int[] shortEdges, double c, double t, double lx, double ly)
    {
        string dirS = shortIsX ? "X" : "Y", dirL = shortIsX ? "Y" : "X";
        // lớp dưới phương ngắn: thanh dài lx, rải theo ly
        d.Bars.Add(Spread("Lớp dưới phương " + dirS, bx, lx + 2 * Anchor(bx.Dia), ly, false, new List<double>()));
        d.Bars.Add(Spread("Lớp dưới phương " + dirL, by, ly + 2 * Anchor(by.Dia), lx, false, new List<double>()));
        // thép trên (mũ gối, lưới trên) bố trí ở lượt 2 – TopReinforcement (SlabDesigner.Top.cs)
    }

    private static double Anchor(int dia) => Math.Max(10 * dia / 1000.0, 0.10);

    /// <summary>Khoá cấu kiện vật lý của ô sàn (lựa chọn thép): "S:{id ô sàn}".</summary>
    internal static string MemberKey(Slab slab) => "S:" + slab.Id.ToString(DesignUtil.Inv);

    internal static string Basis(double t) => "t=" + DesignUtil.Mm(t).ToString("0", DesignUtil.Inv);

    /// <summary>Các phương án Ø–bước cho một vùng thép của ô sàn đã thiết kế (zone = RebarZones.SlabBotX/BotY/TopX/TopY):
    /// mỗi Ø một bước lớn nhất thoả As (làm tròn xuống 10 mm) và các bước chẵn 50 mm nhỏ hơn, trong khoảng [100 mm; smax].</summary>
    internal static List<RebarAlternative> Alternatives(Project p, SlabDesign d, string zone, int max = 12)
    {
        var res = new List<RebarAlternative>();
        var slab = p.Slabs.FirstOrDefault(x => x.Id == d.SlabId);
        if (slab == null) return res;
        var cs = p.Concrete;
        SlabSection sec;
        try { sec = p.SlabSection(slab.Section); } catch (KeyNotFoundException) { return res; }
        var conc = DesignUtil.ConcreteOf(p, sec.Material, cs.ConcreteSlab);
        var stlSmall = p.Material(cs.RebarStirrup);
        var stlMain = p.Material(cs.RebarMain);
        Material SteelOf(int dia) => dia < 10 ? stlSmall : stlMain;
        int[] dias = (p.RebarPrefs ?? new RebarPreferences()).Filter(DesignUtil.Sorted(cs.SlabBars, new[] { 6, 8, 10, 12 }));
        double t = sec.Thickness, c = cs.CoverSlab;
        double sMax = Math.Min(1.5 * t, 0.20);
        var (bxDia, bxS) = ParseSpacing(d.BotX);
        double bxAs = bxS > 0 ? RcSection.BarArea(bxDia) / bxS : 0;
        bool meshScheme = d.Scheme != SlabRebarScheme.TopBarsBottomMesh;
        string cur = zone == RebarZones.SlabBotX ? d.BotX : zone == RebarZones.SlabBotY ? d.BotY
            : zone == RebarZones.SlabMeshX ? RebarOptions.Token(d.TopX, false) : zone == RebarZones.SlabMeshY ? RebarOptions.Token(d.TopY, false)
            : zone == RebarZones.SlabTopX ? (meshScheme ? RebarOptions.Token(d.TopX, true) : d.TopX) : (meshScheme ? RebarOptions.Token(d.TopY, true) : d.TopY);
        // lưới trên hiện có (phương án lưới 2 lớp): mũ bổ sung chỉ cần phần thiếu
        var (mxD, mxS) = ParseSpacing(RebarOptions.Token(d.TopX, false));
        var (myD, myS) = ParseSpacing(RebarOptions.Token(d.TopY, false));
        double meshX = meshScheme && mxS > 0 ? RcSection.BarArea(mxD) / mxS : 0, meshY = meshScheme && myS > 0 ? RcSection.BarArea(myD) / myS : 0;
        double AsMinTop(int dia) => MinRatioSlab / 100.0 * Math.Max(t - c - dia / 2000.0, 0.01);
        double TopReq(double m, int dia) => m < -1e-9 ? RcSection.DesignRect(-m, 1.0, t - c - dia / 2000.0, c, conc, SteelOf(dia), MinRatioSlab).As : 0;
        Func<int, double> req;
        double sMaxZ = sMax;
        if (zone == RebarZones.SlabBotX) req = dia => RcSection.DesignRect(d.M1, 1.0, t - c - dia / 2000.0, c, conc, SteelOf(dia), MinRatioSlab).As;
        else if (zone == RebarZones.SlabBotY)
        {
            if (d.TwoWay) req = dia => RcSection.DesignRect(d.M2, 1.0, t - c - bxDia / 1000.0 - dia / 2000.0, c, conc, SteelOf(dia), MinRatioSlab).As;
            else { req = _ => 0.1 * bxAs; sMaxZ = Math.Min(0.25, Math.Max(sMax, 0.20)); }
        }
        else if (zone == RebarZones.SlabMeshX || zone == RebarZones.SlabMeshY)
        {
            double mm = zone == RebarZones.SlabMeshX ? d.MI : d.MII;
            req = d.Scheme == SlabRebarScheme.TwoLayerMesh ? dia => Math.Max(TopReq(mm, dia), AsMinTop(dia)) : dia => AsMinTop(dia);
            sMaxZ = Math.Min(sMax, 0.20);
        }
        else if (zone == RebarZones.SlabTopX) req = dia => Math.Max(TopReq(d.MI, dia) - meshX, 0);
        else { req = dia => Math.Max(TopReq(d.MII, dia) - meshY, 0); sMaxZ = Math.Max(sMax, 0.20); }
        res.AddRange(RebarOptions.SpacingAlternatives("Sàn", zone, req, dias, 0.10, sMaxZ, cur, max));
        return res;
    }

    /// <summary>Đọc "Ø8a200" → (8; 0,20 m); chuỗi sai → (0; 0).</summary>
    internal static (int Dia, double Spacing) ParseSpacing(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (0, 0);
        string t = text.Trim().TrimStart('Ø', 'ø', 'D', 'd', 'Φ', 'φ');
        int k = t.IndexOf('a');
        if (k <= 0) return (0, 0);
        if (!int.TryParse(t.Substring(0, k), System.Globalization.NumberStyles.Integer, DesignUtil.Inv, out int dia)) return (0, 0);
        if (!double.TryParse(t.Substring(k + 1), System.Globalization.NumberStyles.Float, DesignUtil.Inv, out double smm)) return (dia, 0);
        return (dia, smm / 1000.0);
    }

    /// <summary>Thép rải: Count = số thanh trên bề rộng rải width với bước s (cộng 1).</summary>
    private static BarMark Spread(string role, SpacingChoice sc, double len, double width, bool top, List<double> segs, double extraLen = 0)
    {
        double L = DesignUtil.R10(len + extraLen);
        if (segs.Count == 0) segs.Add(DesignUtil.Mm(L));
        int n = (int)Math.Ceiling(width / sc.Spacing - 1e-9) + 1;
        return new BarMark
        {
            Role = role, Dia = sc.Dia, Spacing = sc.Spacing, Count = n, Length = L, IsTop = top,
            Shape = segs.Count >= 3 ? "U" : "Thẳng", Segments = segs, Start = 0, End = len
        };
    }

    private static string Signature(SlabDesign d) =>
        d.Story + "|" + Math.Round(d.Lx * 100) + "x" + Math.Round(d.Ly * 100) + "|" + Math.Round(d.Thickness * 1000) + "|"
        + d.EdgeCondition + "|" + Math.Round(d.Q * 100) + "|" + d.BotX + d.BotY + d.TopX + d.TopY + "|" + RebarNumbering.Signature(d.Bars);
}
