using KetCauVXT.Analysis;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Thiết kế ô sàn BTCT chữ nhật toàn khối theo dải bản 1 m (TCVN 5574:2018 mục 8.1.2.2 cho uốn, mục 10.3 cấu tạo).
/// Bản dầm (ly/lx &gt; 2) tính như dải một phương; bản kê bốn cạnh dùng phương pháp Marcus: chia tải theo điều kiện
/// võng bằng nhau tại giữa ô, bỏ qua giảm mô men do xoắn – thiên về an toàn.</summary>
public static class SlabDesigner
{
    /// <summary>Hệ số độ cứng khi nứt dùng ước lượng độ võng: B = 0,3·Eb·t³/12 (gần đúng).</summary>
    public const double CrackedStiffnessFactor = 0.3;
    /// <summary>Hàm lượng thép tối thiểu sàn (%)</summary>
    public const double MinRatioSlab = 0.10;

    private enum EdgeKind { Simple, Fixed }

    /// <summary>Thiết kế toàn bộ ô sàn. r có thể null (sàn tính từ tải khai báo, không dùng kết quả phân tích khung).</summary>
    public static List<SlabDesign> Design(Project p, AnalysisResults r, List<string> warnings)
    {
        warnings ??= new List<string>();
        var list = new List<(SlabDesign d, string sig)>();
        foreach (var slab in p.Slabs)
        {
            try
            {
                var d = DesignSlab(p, slab);
                if (d == null) { warnings.Add("Sàn " + slab.Label + " không phải ô chữ nhật – bỏ qua thiết kế"); continue; }
                string sig = Signature(d);
                int k = list.FindIndex(x => x.sig == sig);
                if (k >= 0) list[k].d.Quantity++;
                else list.Add((d, sig));
            }
            catch (Exception ex)
            {
                warnings.Add("LỖI thiết kế sàn " + slab.Label + ": " + ex.Message);
            }
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

    private static SlabDesign DesignSlab(Project p, Slab slab)
    {
        var pts = slab.Nodes.Select(id => p.Node(id).P).ToList();
        if (!SlabLoadDistributor.IsRectangle(pts, out double la, out double lb)) return null;
        var cs = p.Concrete;
        var sec = p.SlabSection(slab.Section);
        var conc = DesignUtil.ConcreteOf(p, sec.Material, cs.ConcreteSlab);
        // thép sàn: Ø < 10 dùng thép đai, Ø ≥ 10 dùng thép chính
        var stlSmall = p.Material(cs.RebarStirrup);
        var stlMain = p.Material(cs.RebarMain);
        int[] dias = DesignUtil.Sorted(cs.SlabBars, new[] { 6, 8, 10, 12 });
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
        d.M1 = m1; d.M2 = m2; d.MI = mI; d.MII = mII;

        // ---- thép (dải b = 1 m) ----
        double c = cs.CoverSlab;
        double sMax = Math.Min(1.5 * t, 0.20);
        Material SteelOf(int dia) => dia < 10 ? stlSmall : stlMain;
        SpacingChoice Choose(double m, double extraDepth)
        {
            return RcSection.ChooseSpacing(dia =>
            {
                double h0 = t - c - extraDepth - dia / 2000.0;
                var fr = RcSection.DesignRect(m, 1.0, h0, c, conc, SteelOf(dia), MinRatioSlab);
                return fr.As;
            }, dias, sMax, 0.10, 0.15);
        }
        // lớp dưới: phương ngắn đặt ngoài (h0 lớn), phương dài đặt trong
        var bx = Choose(m1, 0);
        var by = Choose(m2, bx.Dia / 1000.0);
        var tx = mI < -1e-9 ? Choose(-mI, 0) : null;
        SpacingChoice ty = null;
        if (mII < -1e-9) ty = Choose(-mII, 0);
        else if (!d.TwoWay && fy > 0) ty = RcSection.ChooseSpacing(_ => 0.0, dias, 0.20, 0.10, 0.20);   // mũ cấu tạo cạnh ngắn bản dầm
        // phương dài của bản dầm: thép phân bố ≥ 10% thép chịu lực (TCVN 5574:2018 mục 10.3.8)
        if (!d.TwoWay)
        {
            double need = Math.Max(0.1 * bx.AsPerM, 0);
            by = RcSection.ChooseSpacing(_ => need, dias, Math.Min(0.25, Math.Max(sMax, 0.20)), 0.10, 0.20);
        }
        d.BotX = bx.Text; d.BotY = by.Text; d.TopX = tx?.Text ?? ""; d.TopY = ty?.Text ?? "";
        foreach (var sc in new[] { bx, by, tx, ty })
            if (sc != null && !sc.Ok) d.Messages.Add("LỖI sàn không bố trí đủ thép với bước ≥ 100 mm – cần tăng chiều dày");

        AddCheck(d, "Uốn nhịp phương ngắn", m1, bx, t, c, 0, conc, SteelOf(bx.Dia));
        if (d.TwoWay) AddCheck(d, "Uốn nhịp phương dài", m2, by, t, c, bx.Dia / 1000.0, conc, SteelOf(by.Dia));
        if (tx != null) AddCheck(d, "Uốn gối phương ngắn", -mI, tx, t, c, 0, conc, SteelOf(tx.Dia));
        if (ty != null) AddCheck(d, "Uốn gối phương dài", -mII, ty, t, c, 0, conc, SteelOf(ty.Dia));

        // ---- võng (TTGH II) ----
        double qxs = q > 1e-9 ? qx * qs / q : 0;
        // dải 1 m phương ngắn: Ms tiêu chuẩn tỉ lệ với mô men nhịp tính toán; B kể nứt + từ biến theo thép thực bố trí
        double msx = q > 1e-9 ? m1 * qs / q : 0;
        double h0x = t - c - bx.Dia / 2000.0;
        double D = RcSection.FlexuralStiffness(1.0, t, h0x, bx.AsPerM, conc, SteelOf(bx.Dia).E, msx);
        double f = D > 0 ? DeflCoef(fx) / 384.0 * qxs * Math.Pow(lx, 4) / D : 0;
        d.Deflection = f;
        d.Checks.Add(new CheckItem
        {
            Name = "Võng sàn", Demand = f * 1000, Capacity = lx / 200 * 1000, Unit = "mm",
            Note = "f = c/384·qx·lx⁴/B (c = 5 khớp–khớp, 2 ngàm–khớp, 1 ngàm–ngàm), B = Eb/(1+φcr)·Ieff kể nứt + từ biến, giới hạn lx/200"
        });
        double tMin = d.TwoWay ? lx / 45 : lx / 35;
        if (t < tMin - 1e-6) d.Messages.Add("Chiều dày sàn " + DesignUtil.F(t * 1000, "0") + " mm nhỏ hơn khuyến nghị " + DesignUtil.F(tMin * 1000, "0") + " mm");

        // ---- bố trí thép ----
        Detail(d, pts, aShort, shortIsX, bx, by, tx, ty, edges, longEdges, shortEdges, c, t, lx, ly);
        RebarNumbering.Renumber(d.Bars);
        return d;
    }

    /// <summary>Hệ số độ võng giữa dải: 5 (khớp–khớp), 2 (ngàm–khớp), 1 (ngàm–ngàm), từ f = c·q·l⁴/(384·EI).</summary>
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

    /// <summary>Bố trí thép sàn: lớp dưới hai phương chạy suốt ô (neo vào dầm 10d mỗi đầu);
    /// mũ gối vươn ra ¼ nhịp phương ngắn tính từ mép gối (TCVN 5574:2018 mục 10.3), bẻ móc xuống chạm lớp dưới.
    /// Cạnh khớp đặt mũ cấu tạo nếu chưa có mũ tính toán (bỏ qua – ghi chú).</summary>
    private static void Detail(SlabDesign d, List<Vec3> pts, bool aShort, bool shortIsX, SpacingChoice bx, SpacingChoice by,
        SpacingChoice tx, SpacingChoice ty, EdgeKind[] edges, int[] longEdges, int[] shortEdges, double c, double t, double lx, double ly)
    {
        string dirS = shortIsX ? "X" : "Y", dirL = shortIsX ? "Y" : "X";
        // lớp dưới phương ngắn: thanh dài lx, rải theo ly
        d.Bars.Add(Spread("Lớp dưới phương " + dirS, bx, lx + 2 * Anchor(bx.Dia), ly, false, new List<double>()));
        d.Bars.Add(Spread("Lớp dưới phương " + dirL, by, ly + 2 * Anchor(by.Dia), lx, false, new List<double>()));
        double hook = DesignUtil.R10(Math.Max(t - 2 * c, 0.05));
        // mũ gối trên các cạnh dài (thép theo phương ngắn)
        if (tx != null)
            foreach (int _e in longEdges.Where(i => edges[i] == EdgeKind.Fixed))
            {
                // mũ gối giữa 2 ô: vươn lx/4 vào ô này; phần sang ô bên được tính khi thiết kế ô kề (đối xứng)
                double len = DesignUtil.R10(2 * lx / 4);
                d.Bars.Add(Spread("Lớp trên mũ gối phương " + dirS, tx, len, ly, true,
                    new List<double> { DesignUtil.Mm(hook), DesignUtil.Mm(len), DesignUtil.Mm(hook) }, hook * 2));
            }
        if (ty != null)
            foreach (int _e in shortEdges.Where(i => edges[i] == EdgeKind.Fixed))
            {
                double len = DesignUtil.R10(2 * lx / 4);
                d.Bars.Add(Spread("Lớp trên mũ gối phương " + dirL, ty, len, lx, true,
                    new List<double> { DesignUtil.Mm(hook), DesignUtil.Mm(len), DesignUtil.Mm(hook) }, hook * 2));
            }
    }

    private static double Anchor(int dia) => Math.Max(10 * dia / 1000.0, 0.10);

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
