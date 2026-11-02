using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Thép trên của sàn (lượt 2, sau khi mọi ô đã tính nội lực):
/// <list type="bullet">
/// <item>Mũ gối trên cạnh chung hai ô: tính MỘT lần theo yêu cầu lớn hơn của hai ô, dài lx1/4 + bdầm + lx2/4 (đo từ mép dầm), bẻ móc 2 đầu;
/// chỉ ghi thống kê ở một ô (ô có yêu cầu lớn hơn). Cạnh ngàm ngoài biên: một phía, vươn lx/4 + neo qua dầm biên (tới mép ngoài − lớp bảo vệ).</item>
/// <item>Phương án "Lưới 2 lớp": lưới trên liên tục cả dải ô thẳng hàng (cùng bề rộng, cùng chiều dày) theo mỗi phương,
/// As lưới = max(yêu cầu gối các ô, hàm lượng tối thiểu); mũ bổ sung chỉ nơi lưới thiếu.</item>
/// <item>Phương án "Lưới 2 lớp + mũ tăng cường": lưới trên theo hàm lượng tối thiểu, mũ tăng cường phần thiếu tại gối.</item>
/// </list>
/// Thanh dài hơn cây thương phẩm được tách nối chồng ở bản vẽ gia công (RebarShop).</summary>
public static partial class SlabDesigner
{
    /// <summary>Dữ liệu một ô sàn giữa hai lượt thiết kế.</summary>
    internal sealed class SlabPanel
    {
        public Slab Slab;
        public SlabDesign D;
        public List<Vec3> Pts;
        public EdgeKind[] Edges;
        public int[] LongEdges, ShortEdges;
        public bool ShortIsX, OneWay;
        public double Lx, Ly, T, C, SMax;
        public Material Conc;
        public Func<int, Material> SteelOf;
        public double MI, MII;
        public SpacingChoice Tx, Ty;
        public int[] Dias;
        public string Key, Basis;
        public SlabRebarScheme Scheme;
        /// <summary>Lưới trên theo phương ngắn [0] / dài [1] (null = không có lưới trên)</summary>
        public SpacingChoice[] Mesh = new SpacingChoice[2];
        /// <summary>Thép mũ đã bố trí tại các cạnh ngàm theo phương [0]/[1]: As (m²/m) và nhãn</summary>
        public List<(int Dir, double As, string Text, int Dia)> EdgeProv = new();
        public double Z => Pts.Max(v => v.Z);
        public double X1 => Pts.Min(v => v.X);
        public double X2 => Pts.Max(v => v.X);
        public double Y1 => Pts.Min(v => v.Y);
        public double Y2 => Pts.Max(v => v.Y);
        public bool AxisAligned => Enumerable.Range(0, Pts.Count).All(i =>
        {
            var a = Pts[i]; var b = Pts[(i + 1) % Pts.Count];
            return Math.Abs(a.X - b.X) < 1e-3 || Math.Abs(a.Y - b.Y) < 1e-3;
        });
        /// <summary>Phương (0 ngắn, 1 dài) của thép chạy song song trục tổng thể X (alongX) hoặc Y.</summary>
        public int DirOfGlobal(bool alongX) => ShortIsX == alongX ? 0 : 1;
        public double M(int dir) => dir == 0 ? MI : MII;
        public string Label => string.IsNullOrEmpty(Slab.Label) ? "S" + Slab.Id : Slab.Label;
        public double H0(int dia) => T - C - dia / 2000.0;
        /// <summary>As yêu cầu tại gối theo phương dir (m²/m) cho thép Ø dia.</summary>
        public double ReqTop(int dir, int dia)
        {
            double m = M(dir);
            if (m >= -1e-9) return 0;
            return RcSection.DesignRect(-m, 1.0, H0(dia), C, Conc, SteelOf(dia), MinRatioSlab).As;
        }
        public double AsMin(int dia) => MinRatioSlab / 100.0 * Math.Max(H0(dia), 0.01);
    }

    private static string DirName(SlabPanel pn, int dir) => (dir == 0) == pn.ShortIsX ? "X" : "Y";

    /// <summary>Phương án bố trí thép của ô: lựa chọn riêng (RebarZones.SlabScheme) hoặc mặc định công trình.</summary>
    internal static SlabRebarScheme SchemeOf(Project p, string key)
    {
        var ch = p.FindRebarChoice(key, RebarZones.SlabScheme, -1);
        return ch?.Scheme ?? (p.RebarPrefs ?? new RebarPreferences()).SlabScheme;
    }

    /// <summary>Bề rộng dầm đặt trên đoạn [a; b] (m), 0 nếu không có dầm BTCT.</summary>
    private static double EdgeBeamWidth(Project p, Vec3 a, Vec3 b)
    {
        var u = b - a; double le = u.Length;
        if (le < 1e-6) return 0;
        u = u.Normalize();
        double w = 0;
        foreach (var f in p.Frames)
        {
            if (f.Kind != FrameKind.Beam) continue;
            Vec3 c, d;
            try { c = p.Node(f.NodeI).P; d = p.Node(f.NodeJ).P; } catch (KeyNotFoundException) { continue; }
            if (Math.Abs(c.Z - a.Z) > 0.05 || Math.Abs(d.Z - a.Z) > 0.05) continue;
            var a2 = new Vec3(a.X, a.Y, c.Z);
            if (!OnLine(a2, u, c, out double t1) || !OnLine(a2, u, d, out double t2)) continue;
            double lo = Math.Max(0, Math.Min(t1, t2)), hi = Math.Min(le, Math.Max(t1, t2));
            if (hi - lo < 0.1 * le) continue;
            var sec = p.FrameSections.FirstOrDefault(x => x.Name == f.Section);
            if (sec != null && sec.Shape == SectionShape.RectConcrete) w = Math.Max(w, sec.B);
        }
        return w;
    }

    private sealed class EdgeItem
    {
        public SlabPanel P;
        public int Edge, Dir;
        public Vec3 A, B;
        public SpacingChoice Choice;
        public bool User;
        public bool Done;
    }

    /// <summary>Lượt 2: lưới trên, mũ gối (cạnh chung một lần), kiểm tra uốn gối, chuỗi thép trên.</summary>
    private static void TopReinforcement(Project p, List<SlabPanel> panels)
    {
        foreach (var pn in panels) { pn.Scheme = SchemeOf(p, pn.Key); pn.D.Scheme = pn.Scheme; }

        // ---- 1. lưới trên (lưới 2 lớp) ----
        foreach (var pn in panels.Where(x => x.Scheme != SlabRebarScheme.TopBarsBottomMesh))
            for (int dir = 0; dir < 2; dir++)
            {
                int dd = dir;
                Func<int, double> req = pn.Scheme == SlabRebarScheme.TwoLayerMesh
                    ? dia => Math.Max(pn.ReqTop(dd, dia), pn.AsMin(dia))
                    : dia => pn.AsMin(dia);
                var auto = RcSection.ChooseSpacing(req, pn.Dias, Math.Min(pn.SMax, 0.20), 0.10, 0.20);
                var ch = p.ValidRebarChoice(pn.Key, dir == 0 ? RebarZones.SlabMeshX : RebarZones.SlabMeshY, -1, pn.Basis);
                if (ch != null && ch.Dia > 0 && ch.Spacing > 0)
                {
                    auto = new SpacingChoice { Dia = ch.Dia, Spacing = ch.Spacing, AsReqPerM = req(ch.Dia), Ok = true };
                    pn.D.UserRebarZones++;
                    if (auto.AsPerM < auto.AsReqPerM * (1 - 1e-6) && pn.Scheme == SlabRebarScheme.TwoLayerMesh)
                        pn.D.Messages.Add("Lưới trên " + auto.Text + " chọn nhỏ hơn yêu cầu gối – đặt thêm mũ bổ sung");
                }
                pn.Mesh[dir] = auto;
            }
        // dải ô thẳng hàng → lưới liên tục, thống kê ở ô đầu dải
        foreach (bool alongX in new[] { true, false })
        {
            var cand = panels.Where(x => x.Scheme != SlabRebarScheme.TopBarsBottomMesh).ToList();
            var used = new HashSet<SlabPanel>();
            foreach (var pn in cand.OrderBy(x => Math.Round(x.Z, 2)).ThenBy(x => alongX ? x.Y1 : x.X1).ThenBy(x => alongX ? x.X1 : x.Y1))
            {
                if (used.Contains(pn)) continue;
                var run = new List<SlabPanel> { pn };
                used.Add(pn);
                if (pn.AxisAligned)
                {
                    bool grown = true;
                    while (grown)
                    {
                        grown = false;
                        var last = run[^1];
                        var next = cand.FirstOrDefault(o => !used.Contains(o) && o.AxisAligned && Math.Abs(o.Z - last.Z) < 0.01 && Math.Abs(o.T - last.T) < 1e-4
                            && (alongX ? Math.Abs(o.X1 - last.X2) < 0.01 && Math.Abs(o.Y1 - last.Y1) < 0.01 && Math.Abs(o.Y2 - last.Y2) < 0.01
                                       : Math.Abs(o.Y1 - last.Y2) < 0.01 && Math.Abs(o.X1 - last.X1) < 0.01 && Math.Abs(o.X2 - last.X2) < 0.01));
                        if (next != null) { run.Add(next); used.Add(next); grown = true; }
                    }
                }
                // lưới chung của dải = lưới lớn nhất trong dải
                var gov = run.Select(x => x.Mesh[x.DirOfGlobal(alongX)]).Where(x => x != null).OrderByDescending(x => x.AsPerM).FirstOrDefault();
                if (gov == null) continue;
                foreach (var x in run) x.Mesh[x.DirOfGlobal(alongX)] = gov;
                var owner = run[0];
                double c = owner.C, hook = DesignUtil.R10(Math.Max(owner.T - 2 * c, 0.05));
                double len, width;
                Vec3 e0a, e0b, e1a, e1b;
                if (owner.AxisAligned)
                {
                    double a0 = alongX ? run[0].X1 : run[0].Y1, a1 = alongX ? run[^1].X2 : run[^1].Y2;
                    double w0 = alongX ? owner.Y1 : owner.X1, w1 = alongX ? owner.Y2 : owner.X2;
                    double z = owner.Z;
                    e0a = alongX ? new Vec3(a0, w0, z) : new Vec3(w0, a0, z); e0b = alongX ? new Vec3(a0, w1, z) : new Vec3(w1, a0, z);
                    e1a = alongX ? new Vec3(a1, w0, z) : new Vec3(w0, a1, z); e1b = alongX ? new Vec3(a1, w1, z) : new Vec3(w1, a1, z);
                    double b0 = EdgeBeamWidth(p, e0a, e0b), b1 = EdgeBeamWidth(p, e1a, e1b);
                    len = (a1 - a0) + Math.Max(b0 / 2 - c, 0) + Math.Max(b1 / 2 - c, 0);
                    width = w1 - w0;
                    owner.D.TopMeshes.Add(new SlabTopMesh { AlongX = alongX, X1 = alongX ? a0 : w0, X2 = alongX ? a1 : w1, Y1 = alongX ? w0 : a0, Y2 = alongX ? w1 : a1 });
                }
                else
                {
                    int dir = owner.DirOfGlobal(alongX);
                    len = dir == 0 ? owner.Lx : owner.Ly;
                    width = dir == 0 ? owner.Ly : owner.Lx;
                }
                len = DesignUtil.R10(len);
                string names = string.Join(", ", run.Select(x => x.Label));
                var bm = Spread("Lớp trên lưới phương " + (alongX ? "X" : "Y"), gov, len, width, true,
                    new List<double> { DesignUtil.Mm(hook), DesignUtil.Mm(len), DesignUtil.Mm(hook) }, hook * 2);
                owner.D.Bars.Add(bm);
                if (owner.D.TopMeshes.Count > 0 && owner.D.TopMeshes[^1].Bar == null) owner.D.TopMeshes[^1].Bar = bm;
                if (run.Count > 1)
                {
                    owner.D.Messages.Add("Lưới trên phương " + (alongX ? "X" : "Y") + " " + gov.Text + " liên tục qua các ô " + names
                                         + " (dài " + DesignUtil.F(len, "0.00") + " m – thanh dài hơn cây thương phẩm nối chồng, xem bản vẽ gia công)");
                    foreach (var x in run.Skip(1))
                        x.D.Messages.Add("Lưới trên phương " + (alongX ? "X" : "Y") + " " + gov.Text + " liên tục từ ô " + owner.Label + " (thống kê ở ô đó)");
                }
            }
        }

        // ---- 2. mũ gối trên các cạnh ngàm ----
        var items = new List<EdgeItem>();
        foreach (var pn in panels)
        {
            for (int i = 0; i < 4; i++)
            {
                if (pn.Edges[i] != EdgeKind.Fixed) continue;
                int dir = pn.LongEdges.Contains(i) ? 0 : 1;
                var it = new EdgeItem { P = pn, Edge = i, Dir = dir, A = pn.Pts[i], B = pn.Pts[(i + 1) % 4] };
                string zone = dir == 0 ? RebarZones.SlabTopX : RebarZones.SlabTopY;
                bool user = p.ValidRebarChoice(pn.Key, zone, -1, pn.Basis) != null;
                if (pn.Scheme == SlabRebarScheme.TopBarsBottomMesh)
                    it.Choice = dir == 0 ? pn.Tx : pn.Ty;
                else
                {
                    double mesh = pn.Mesh[dir]?.AsPerM ?? 0;
                    var ud = user ? (dir == 0 ? pn.Tx : pn.Ty) : null;
                    if (ud != null) it.Choice = ud;
                    else if (pn.ReqTop(dir, pn.Dias[0]) > mesh + 1e-9 || pn.Dias.Any(dg => pn.ReqTop(dir, dg) > mesh + 1e-9))
                        it.Choice = RcSection.ChooseSpacing(dg => Math.Max(pn.ReqTop(dir, dg) - mesh, 0), pn.Dias, pn.SMax, 0.10, 0.20);
                }
                it.User = user && it.Choice != null;
                items.Add(it);
            }
        }
        double Need(EdgeItem it, int dia) => Math.Max(it.P.ReqTop(it.Dir, dia) - (it.P.Mesh[it.Dir]?.AsPerM ?? 0), 0);
        foreach (var it in items)
        {
            if (it.Done) continue;
            it.Done = true;
            var u = (it.B - it.A); double le = u.Length;
            if (le < 1e-6) continue;
            u = u.Normalize();
            // ô kề: cạnh thẳng hàng chồng lên cạnh này
            EdgeItem mate = null; double lo = 0, hi = le;
            foreach (var o in items)
            {
                if (o == it || o.Done || o.P == it.P || Math.Abs(o.P.Z - it.P.Z) > 0.01) continue;
                if (!OnLine(it.A, u, o.A, out double t1) || !OnLine(it.A, u, o.B, out double t2)) continue;
                double l2 = Math.Max(0, Math.Min(t1, t2)), h2 = Math.Min(le, Math.Max(t1, t2));
                if (h2 - l2 > 0.1 * le) { mate = o; lo = l2; hi = h2; break; }
            }
            var A = it.A + u * lo; var B = it.A + u * hi;
            double ovl = hi - lo;
            double bw = EdgeBeamWidth(p, A, B);
            var cen = new Vec3(it.P.Pts.Average(v => v.X), it.P.Pts.Average(v => v.Y), it.P.Z);
            var nrm = new Vec3(-u.Y, u.X, 0);
            if ((cen - A).Dot(nrm) < 0) nrm = nrm * -1;   // pháp tuyến hướng vào ô it
            if (mate == null)
            {
                if (it.Choice == null) continue;
                // cạnh ngàm ngoài biên: một phía, neo qua dầm biên
                var pn = it.P;
                double hook = DesignUtil.R10(Math.Max(pn.T - 2 * pn.C, 0.05));
                double ext = pn.Lx / 4;
                double over = Math.Max(bw - pn.C, 0);
                double len = DesignUtil.R10(ext + over);
                var bm = Spread("Lớp trên mũ gối biên phương " + DirName(pn, it.Dir), it.Choice, len, ovl, true,
                    new List<double> { DesignUtil.Mm(hook), DesignUtil.Mm(len), DesignUtil.Mm(hook) }, hook * 2);
                pn.D.Bars.Add(bm);
                pn.D.EdgeBars.Add(new SlabEdgeBar { X1 = A.X, Y1 = A.Y, X2 = B.X, Y2 = B.Y, NX = nrm.X, NY = nrm.Y, Ext1 = ext + bw / 2, Ext2 = Math.Max(bw / 2 - pn.C, 0), Bar = bm });
                pn.EdgeProv.Add((it.Dir, it.Choice.AsPerM, it.Choice.Text, it.Choice.Dia));
                continue;
            }
            mate.Done = true;
            // cạnh chung: thép theo yêu cầu lớn hơn (lựa chọn của người dùng được giữ nếu đủ cho ô bên kia)
            var cands = new[] { it, mate }.Where(x => x.Choice != null).ToList();
            if (cands.Count == 0) continue;
            EdgeItem gov = null;
            foreach (var x in cands.Where(q => q.User))
            {
                var other = x == it ? mate : it;
                if (x.Choice.AsPerM >= Need(other, x.Choice.Dia) * (1 - 1e-6)) { gov = x; break; }
            }
            gov ??= cands.OrderByDescending(x => x.Choice.AsPerM).ThenBy(x => x.P.Slab.Id).First();
            foreach (var x in cands.Where(q => q.User && q != gov))
                x.P.D.Messages.Add("Mũ gối cạnh chung với ô " + (x == it ? mate : it).P.Label + ": dùng " + gov.Choice.Text
                                   + " theo yêu cầu ô " + gov.P.Label + " (lớn hơn thép chọn " + x.Choice.Text + ")");
            var own = gov.P;
            var oth = gov == it ? mate : it;
            double hk = DesignUtil.R10(Math.Max(Math.Min(own.T, oth.P.T) - 2 * own.C, 0.05));
            double extOwn = own.Lx / 4, extOth = oth.P.Lx / 4;
            double L = DesignUtil.R10(extOwn + bw + extOth);
            var sb = Spread("Lớp trên mũ gối chung phương " + DirName(own, gov.Dir), gov.Choice, L, ovl, true,
                new List<double> { DesignUtil.Mm(hk), DesignUtil.Mm(L), DesignUtil.Mm(hk) }, hk * 2);
            own.D.Bars.Add(sb);
            // pháp tuyến hướng vào ô chủ
            var cOwn = new Vec3(own.Pts.Average(v => v.X), own.Pts.Average(v => v.Y), own.Z);
            var n2 = new Vec3(-u.Y, u.X, 0);
            if ((cOwn - A).Dot(n2) < 0) n2 = n2 * -1;
            own.D.EdgeBars.Add(new SlabEdgeBar
            {
                X1 = A.X, Y1 = A.Y, X2 = B.X, Y2 = B.Y, NX = n2.X, NY = n2.Y, Ext1 = extOwn + bw / 2, Ext2 = extOth + bw / 2, Shared = true, Bar = sb
            });
            own.EdgeProv.Add((gov.Dir, gov.Choice.AsPerM, gov.Choice.Text, gov.Choice.Dia));
            oth.P.EdgeProv.Add((oth.Dir, gov.Choice.AsPerM, gov.Choice.Text, gov.Choice.Dia));
            oth.P.D.Messages.Add("Mũ gối cạnh chung với ô " + own.Label + " (" + gov.Choice.Text + ", dài " + DesignUtil.F(L, "0.00")
                                 + " m + móc) thống kê ở ô " + own.Label);
        }

        // ---- 3. kiểm tra uốn gối + chuỗi thép trên ----
        foreach (var pn in panels)
        {
            var d = pn.D;
            for (int dir = 0; dir < 2; dir++)
            {
                double m = pn.M(dir);
                var mesh = pn.Mesh[dir];
                var edge = pn.EdgeProv.Where(x => x.Dir == dir).ToList();
                bool hasFixed = (dir == 0 ? pn.LongEdges : pn.ShortEdges).Any(i => pn.Edges[i] == EdgeKind.Fixed);
                double eAs = hasFixed && edge.Count > 0 ? edge.Min(x => x.As) : 0;
                if (hasFixed && edge.Count == 0) eAs = 0;
                double As = (mesh?.AsPerM ?? 0) + eAs;
                string txt;
                if (pn.Scheme == SlabRebarScheme.TopBarsBottomMesh)
                    txt = edge.Count == 0 ? "" : edge.OrderByDescending(x => x.As).First().Text;
                else
                    txt = (mesh != null ? "lưới " + mesh.Text : "") + (edge.Count > 0 ? " + mũ " + edge.OrderByDescending(x => x.As).First().Text : "");
                if (dir == 0) d.TopX = txt; else d.TopY = txt;
                if (m < -1e-9 || (hasFixed && As > 0))
                {
                    int dia = Math.Max(mesh?.Dia ?? 0, edge.Count > 0 ? edge.Max(x => x.Dia) : 0);
                    if (dia <= 0) dia = pn.Dias[0];
                    double mu = RcSection.MomentCapacity(1.0, pn.H0(dia), As, 0, pn.C, pn.Conc, pn.SteelOf(dia));
                    d.Checks.Add(new CheckItem
                    {
                        Name = dir == 0 ? "Uốn gối phương ngắn" : "Uốn gối phương dài", Demand = Math.Abs(m), Capacity = mu, Unit = "kN.m/m",
                        Note = (txt.Length > 0 ? txt : "không có thép trên") + " – As = " + DesignUtil.F(As * 1e4, "0.00") + " cm²/m"
                    });
                    string zoneTop = dir == 0 ? RebarZones.SlabTopX : RebarZones.SlabTopY;
                    if (mu >= Math.Abs(m) - 1e-6)   // thép thực đặt (mũ chung theo ô kề / lưới) đã đủ → bỏ báo thiếu của riêng lựa chọn
                        d.Messages.RemoveAll(x => x.StartsWith("LỖI bố trí thép do người dùng chọn không đạt – " + zoneTop + " ", StringComparison.Ordinal) && x.Contains(": As ="));
                    if (m < -1e-9 && mu < Math.Abs(m) - 1e-6 && d.UserRebarZones > 0)
                        d.Messages.Add("LỖI bố trí thép do người dùng chọn không đạt – uốn gối phương " + (dir == 0 ? "ngắn" : "dài") + ": " + txt);
                }
            }
            if (pn.Scheme != SlabRebarScheme.TopBarsBottomMesh)
                d.Messages.Add("Phương án thép sàn: " + RebarPreferences.SchemeName(pn.Scheme));
        }
    }
}
