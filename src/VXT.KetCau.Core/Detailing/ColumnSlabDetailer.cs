using System;
using System.Collections.Generic;
using System.Linq;
using VXT.KetCau.Core.Codes;
using VXT.KetCau.Core.Design;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;

namespace VXT.KetCau.Core.Detailing
{
    /// <summary>Hình học một đoạn cột cho bản vẽ mặt đứng/mặt cắt.</summary>
    public class ColumnGeometry
    {
        public P2 Location { get; set; }
        public double RotationDeg { get; set; }
        public double B { get; set; }
        public double H { get; set; }
        public double BaseZ { get; set; }
        public double TopZ { get; set; }
        /// <summary>Đoạn thép chờ nối phía trên (mm), 0 nếu tầng trên cùng.</summary>
        public double LapAbove { get; set; }
        /// <summary>Đoạn neo xuống móng (mm), 0 nếu không phải tầng dưới cùng.</summary>
        public double AnchorBelow { get; set; }
        public List<StirrupZone> StirrupZones { get; } = new List<StirrupZone>();
        public string BaseLevelText => $"{(BaseZ >= 0 ? "+" : "")}{BaseZ / 1000:0.000}".Replace("+0.000", "±0.000");
        public string TopLevelText => $"+{TopZ / 1000:0.000}";
        public P3 ToGlobal(double x, double y, double z)
        {
            double r = RotationDeg * Math.PI / 180;
            return new P3(Location.X + x * Math.Cos(r) - y * Math.Sin(r), Location.Y + x * Math.Sin(r) + y * Math.Cos(r), z);
        }
    }

    public class SlabGeometry
    {
        public P2 Origin { get; set; }
        public P2 U { get; set; }
        public double L1 { get; set; }
        public double L2 { get; set; }
        public double TopZ { get; set; }
        public double T { get; set; }
        public P2 V => new P2(-U.Y, U.X);
        public P3 ToGlobal(double u, double v, double z) { var p = Origin + U * u + V * v; return new P3(p.X, p.Y, z); }
    }

    /// <summary>Cấu tạo thép cột (theo đoạn tầng), sàn (theo ô bản) và móng đơn.</summary>
    public class ColumnSlabDetailer
    {
        readonly DesignSettings set;
        readonly ConcreteGrade C;
        readonly SteelGrade S;
        public ColumnSlabDetailer(DesignSettings s) { set = s; C = ConcreteGrade.Get(s.Concrete); S = SteelGrade.Get(s.MainSteel); }

        // ===================== CỘT =====================
        /// <param name="above">cột tầng trên cùng vị trí (null nếu là tầng trên cùng)</param>
        /// <param name="footing">móng dưới (null nếu không phải tầng dưới cùng)</param>
        /// <param name="beamDepthAtTop">chiều cao dầm lớn nhất khung vào đỉnh cột (mm)</param>
        public DetailedElement DetailColumn(ColumnElem col, ConcreteSection sec, ColumnElem above, FootingElem footing, double beamDepthAtTop)
        {
            var d = col.Design ?? throw new InvalidOperationException($"Cột {col.Label} chưa được thiết kế");
            double c = set.CoverColumn;
            int dsw = d.Stirrup.D;
            var g = new ColumnGeometry { Location = col.Location, RotationDeg = col.RotationDeg, B = sec.B, H = sec.H, BaseZ = col.BaseZ, TopZ = col.TopZ };
            var bars = new List<DetailBar>();
            string lbl = col.Label;

            // ----- Thép dọc -----
            double lap = Tcvn5574.LapLength(d.D, S, C, compression: true);
            double lan = Tcvn5574.Anchorage(d.D, S, C);
            double z0, z1, hkBot = 0, hkTop = 0;
            if (footing != null)
            {
                z0 = footing.BottomZ + set.CoverFooting + 2 * 12; // đặt trên lưới thép đáy móng
                hkBot = Math.Max(300, 15 * d.D);                  // chân thép bẻ ngang trong móng
                g.AnchorBelow = col.BaseZ - z0;
            }
            else z0 = col.BaseZ;
            if (above != null && above.Design != null)
            {
                z1 = col.TopZ + lap;
                g.LapAbove = lap;
            }
            else
            {
                z1 = col.TopZ - c - 10;                           // tầng trên cùng: dừng dưới mặt sàn, bẻ móc vào dầm
                double straight = z1 - (col.TopZ - beamDepthAtTop);
                hkTop = straight >= lan ? 12 * d.D : Math.Max(12 * d.D, lan - straight);
                hkTop = Math.Ceiling(hkTop / 10) * 10;
            }
            double L = z1 - z0;
            var main = new DetailBar { Element = lbl, Role = BarRole.ColumnMain, D = d.D, OrderX = 0 };
            if (hkBot > 0 && hkTop > 0) { main.Shape = ShapeKind.UHook; main.Segments.AddRange(new[] { hkBot, L, hkTop }); }
            else if (hkBot > 0) { main.Shape = ShapeKind.LHook; main.Segments.AddRange(new[] { hkBot, L }); }
            else if (hkTop > 0) { main.Shape = ShapeKind.LHook; main.Segments.AddRange(new[] { L, hkTop }); }
            else { main.Shape = ShapeKind.Straight; main.Segments.Add(L); }
            for (int i = 0; i < main.Segments.Count; i++) main.Segments[i] = Math.Round(main.Segments[i] / 10) * 10;

            var pos = ColumnBarPositions(sec.B, sec.H, d.Nb, d.Nh, d.D, dsw, c);
            foreach (var (x, y) in pos)
            {
                var pl = new BarPlacement();
                // hướng vào lõi cột (theo cạnh gần nhất); móc đỉnh bẻ vào trong, chân móng bẻ ra ngoài
                double ix = -Math.Sign(x), iy = -Math.Sign(y);
                var inward = Math.Abs(x) / sec.B >= Math.Abs(y) / sec.H ? (ix, 0.0) : (0.0, iy);
                if (hkBot > 0) pl.Path.Add(g.ToGlobal(x - inward.Item1 * hkBot, y - inward.Item2 * hkBot, z0));
                pl.Path.Add(g.ToGlobal(x, y, z0));
                pl.Path.Add(g.ToGlobal(x, y, z1));
                if (hkTop > 0) pl.Path.Add(g.ToGlobal(x + inward.Item1 * hkTop, y + inward.Item2 * hkTop, z1));
                // pháp tuyến mặt phẳng chứa thanh (vuông góc với phương móc)
                pl.Normal = Math.Abs(inward.Item1) > 0 ? Rot(g, 0, 1) : Rot(g, 1, 0);
                pl.X0 = z0; pl.X1 = z1;
                main.Placements.Add(pl);
            }
            main.Count = pos.Count;
            bars.Add(main);

            // ----- Đai -----
            double hc = col.TopZ - col.BaseZ;
            double dense = GeomUtil.RoundUp(Math.Max(Math.Max(sec.B, sec.H), Math.Max(hc / 6, 450)), 50);
            double clearTop = col.TopZ - beamDepthAtTop;
            double s1 = d.StirrupDense.S, s2 = d.Stirrup.S;
            var outer = new DetailBar { Element = lbl, Role = BarRole.Stirrup, D = dsw, Shape = ShapeKind.Stirrup, Segments = { sec.B - 2 * c, sec.H - 2 * c }, Spacing = s1 };
            int nBot = (int)Math.Floor((dense - 50) / s1) + 1;
            int nTop = (int)Math.Floor((dense - 50) / s1) + 1;
            double zA = col.BaseZ + 50, zB = clearTop - 50;
            double lastB = zA + (nBot - 1) * s1, firstT = zB - (nTop - 1) * s1;
            double gap = firstT - lastB;
            int nMid = Math.Max(0, (int)Math.Ceiling(gap / s2 - 1e-9) - 1);
            double sMid = nMid > 0 ? gap / (nMid + 1) : gap;
            g.StirrupZones.Add(new StirrupZone { X0 = col.BaseZ, X1 = lastB + (nMid > 0 ? sMid / 2 : gap / 2), Spec = d.StirrupDense, Count = nBot, Name = "Chân cột" });
            if (nMid > 0) g.StirrupZones.Add(new StirrupZone { X0 = lastB + sMid / 2, X1 = firstT - sMid / 2, Spec = d.Stirrup, Count = nMid, Name = "Giữa cột" });
            g.StirrupZones.Add(new StirrupZone { X0 = firstT - (nMid > 0 ? sMid / 2 : gap / 2), X1 = col.TopZ, Spec = d.StirrupDense, Count = nTop, Name = "Đầu cột" });
            // vùng nút khung (trong chiều cao dầm) đặt đai bước dày
            int nJoint = Math.Max(0, (int)Math.Floor((beamDepthAtTop - 50) / s1));
            // vùng nối chồng (cột tầng dưới đã có thép chờ) — bước ≤ 10d đã bao bởi vùng dày chân cột khi lap ≤ dense
            if (g.LapAbove > 0 && lap > dense) { /* đai vùng chờ ở tầng trên do cột trên chịu */ }

            var outerPath = RectPath(g, sec.B / 2 - c - dsw / 2.0, sec.H / 2 - c - dsw / 2.0);
            AddColStirrup(outer, outerPath, zA, nBot, s1, s1);
            if (nMid > 0) AddColStirrup(outer, outerPath, lastB + sMid, nMid, sMid, s2);
            AddColStirrup(outer, outerPath, firstT, nTop, s1, s1);
            if (nJoint > 0) AddColStirrup(outer, outerPath, clearTop + s1 / 2, nJoint, s1, s1);
            outer.Count = nBot + nMid + nTop + nJoint;
            bars.Add(outer);

            // đai lồng / đai C cho thanh giữa cạnh
            int total = outer.Count;
            AddInnerTies(bars, lbl, g, sec, d, dsw, c, pos, total, s1, zA, nBot, lastB, sMid, nMid, firstT, nTop);

            var e = new DetailedElement { Label = lbl, Category = "Cột", TotalLength = hc, Bars = BarMarker.Assign(bars), Geometry = g };
            e.SourceIds.Add(col.Id);
            return e;
        }

        static P3 Rot(ColumnGeometry g, double x, double y)
        {
            double r = g.RotationDeg * Math.PI / 180;
            return new P3(x * Math.Cos(r) - y * Math.Sin(r), x * Math.Sin(r) + y * Math.Cos(r), 0);
        }

        void AddInnerTies(List<DetailBar> bars, string lbl, ColumnGeometry g, ConcreteSection sec, ColumnDesignResult d, int dsw, double c,
                          List<(double x, double y)> pos, int total, double s1, double zA, int nBot, double lastB, double sMid, int nMid, double firstT, int nTop)
        {
            // Theo phương b (thanh trên cạnh dài theo x)
            void Make(int n, bool alongX)
            {
                if (n <= 2) return;
                double span = alongX ? sec.H - 2 * c : sec.B - 2 * c;
                if (n == 3)
                {
                    // 1 đai C giữa cạnh
                    var ct = new DetailBar { Element = lbl, Role = BarRole.CTie, D = dsw, Shape = ShapeKind.CTie, Segments = { span }, Spacing = s1, Position = alongX ? "X" : "Y" };
                    var a = alongX ? g.ToGlobal(0, -(sec.H / 2 - c), 0) : g.ToGlobal(-(sec.B / 2 - c), 0, 0);
                    var b = alongX ? g.ToGlobal(0, sec.H / 2 - c, 0) : g.ToGlobal(sec.B / 2 - c, 0, 0);
                    foreach (var (z, cnt, s) in Sets(zA, nBot, s1, lastB, sMid, nMid, firstT, nTop))
                        ct.Placements.Add(new BarPlacement { Count = cnt, Spacing = s, Direction = new P3(0, 0, 1), Normal = new P3(0, 0, 1),
                            Path = { new P3(a.X, a.Y, z), new P3(b.X, b.Y, z) } });
                    ct.Count = total;
                    bars.Add(ct);
                }
                else
                {
                    // đai lồng ôm các thanh giữa
                    double inner = alongX ? (sec.B - 2 * c - 2 * dsw - d.D) * (n - 3) / (n - 1) + d.D + 2 * dsw
                                          : (sec.H - 2 * c - 2 * dsw - d.D) * (n - 3) / (n - 1) + d.D + 2 * dsw;
                    var ist = new DetailBar { Element = lbl, Role = BarRole.InnerStirrup, D = dsw, Shape = ShapeKind.Stirrup,
                        Segments = { alongX ? inner : sec.B - 2 * c, alongX ? sec.H - 2 * c : inner }, Spacing = s1, Position = alongX ? "X" : "Y" };
                    var path = alongX ? RectPath(g, inner / 2 - dsw / 2.0, sec.H / 2 - c - dsw / 2.0) : RectPath(g, sec.B / 2 - c - dsw / 2.0, inner / 2 - dsw / 2.0);
                    foreach (var (z, cnt, s) in Sets(zA, nBot, s1, lastB, sMid, nMid, firstT, nTop))
                        AddColStirrup(ist, path, z, cnt, s, s);
                    ist.Count = total;
                    bars.Add(ist);
                }
            }
            Make(d.Nb, true);
            Make(d.Nh, false);
        }

        static IEnumerable<(double z, int n, double s)> Sets(double zA, int nBot, double s1, double lastB, double sMid, int nMid, double firstT, int nTop)
        {
            yield return (zA, nBot, s1);
            if (nMid > 0) yield return (lastB + sMid, nMid, sMid);
            yield return (firstT, nTop, s1);
        }

        static List<P3> RectPath(ColumnGeometry g, double hx, double hy)
            => new List<P3> { g.ToGlobal(-hx, -hy, 0), g.ToGlobal(hx, -hy, 0), g.ToGlobal(hx, hy, 0), g.ToGlobal(-hx, hy, 0), g.ToGlobal(-hx, -hy, 0) };

        static void AddColStirrup(DetailBar bar, List<P3> path, double z, int n, double s, double tagS)
        {
            bar.Placements.Add(new BarPlacement
            {
                Count = n, Spacing = tagS, X0 = z, X1 = z + (n - 1) * s,
                Direction = new P3(0, 0, 1), Normal = new P3(0, 0, 1),
                Path = path.Select(p => new P3(p.X, p.Y, z)).ToList(),
            });
        }

        public static List<(double x, double y)> ColumnBarPositions(double b, double h, int nb, int nh, int d, int dsw, double c)
        {
            var res = new List<(double, double)>();
            double a = c + dsw + d / 2.0;
            double x0 = -b / 2 + a, x1 = b / 2 - a, y0 = -h / 2 + a, y1 = h / 2 - a;
            for (int i = 0; i < nb; i++)
            {
                double x = x0 + (x1 - x0) * i / Math.Max(1, nb - 1);
                res.Add((x, y0)); res.Add((x, y1));
            }
            for (int j = 1; j < nh - 1; j++)
            {
                double y = y0 + (y1 - y0) * j / (nh - 1);
                res.Add((x0, y)); res.Add((x1, y));
            }
            return res;
        }

        // ===================== SÀN =====================
        /// <summary>
        /// Cấu tạo thép một ô sàn chữ nhật: lớp dưới 2 phương neo tới tim dầm, thép mũ các cạnh dài L1/4 từ mép dầm,
        /// cạnh ngoài bẻ móc xuống. Cạnh chung giữa 2 ô do <see cref="DetailSlabs"/> xử lý một lần.
        /// </summary>
        public List<DetailedElement> DetailSlabs(StructModel m, IEnumerable<SlabElem> slabs, Func<P2, P2, double> beamWidthAlong)
        {
            var list = new List<DetailedElement>();
            var geos = new Dictionary<SlabElem, SlabGeometry>();
            foreach (var s in slabs)
            {
                if (s.Boundary.Count != 4 || s.Design == null) continue;
                var p = s.Boundary;
                var e0 = p[1] - p[0]; var e1 = p[2] - p[1];
                bool firstShort = e0.Length <= e1.Length;
                var origin = firstShort ? p[0] : p[1];
                var U = (firstShort ? e0 : e1).Unit;   // phương cạnh ngắn L1
                double l1 = Math.Min(e0.Length, e1.Length), l2 = Math.Max(e0.Length, e1.Length);
                var sec = m.SlabSection(s.Section);
                var g = new SlabGeometry { Origin = origin, U = U, L1 = l1, L2 = l2, TopZ = s.TopZ, T = sec.Thickness };
                // đảm bảo V hướng vào trong ô
                var center = new P2(p.Average(q => q.X), p.Average(q => q.Y));
                if ((center - origin).Dot(g.V) < 0) { g.Origin = origin + U * l1; g.U = U * -1; }
                geos[s] = g;
            }

            foreach (var kv in geos)
            {
                var s = kv.Key; var g = kv.Value; var d = s.Design;
                double c = set.CoverSlab;
                var bars = new List<DetailBar>();
                string lbl = s.Label;
                double zB1 = g.TopZ - g.T + c + d.Bottom1.D / 2.0;
                double zB2 = zB1 + d.Bottom1.D / 2.0 + d.Bottom2.D / 2.0;
                double zT = g.TopZ - c - d.Top1.D / 2.0;

                // Lớp dưới phương 1 (cạnh ngắn): thanh song song U, rải theo V
                bars.Add(SlabSet(lbl, BarRole.Distribution, "L1", d.Bottom1, g, 0, g.L1, 0, g.L2, zB1, true, 0, 0));
                bars.Add(SlabSet(lbl, BarRole.Distribution, "L2", d.Bottom2, g, 0, g.L2, 0, g.L1, zB2, false, 0, 0));

                // Thép mũ 4 cạnh
                double ext1 = GeomUtil.RoundUp(g.L1 / 4, 10);
                double hk = g.T - 2 * c;
                // cạnh v=0 và v=L2 (song song U): thanh mũ phương V, dài ext1 + bw/2, rải dọc U
                foreach (var (v0, sign, pos) in new[] { (0.0, 1.0, "TA"), (g.L2, -1.0, "TB") })
                {
                    var a = g.ToGlobal(0, v0, 0); var b = g.ToGlobal(g.L1, v0, 0);
                    double bw = beamWidthAlong(a.XY, b.XY);
                    bool exterior = IsExteriorEdge(geos, s, a.XY, b.XY);
                    double from = v0 - sign * bw / 2 + (exterior ? sign * c : 0);
                    double to = v0 + sign * (ext1 + 0);
                    var bar = SlabTopBar(lbl, pos, d.Top1, g, from, to, false, zT, hk, exterior, 0, g.L1);
                    bars.Add(bar);
                }
                foreach (var (u0, sign, pos) in new[] { (0.0, 1.0, "TC"), (g.L1, -1.0, "TD") })
                {
                    var a = g.ToGlobal(u0, 0, 0); var b = g.ToGlobal(u0, g.L2, 0);
                    double bw = beamWidthAlong(a.XY, b.XY);
                    bool exterior = IsExteriorEdge(geos, s, a.XY, b.XY);
                    double from = u0 - sign * bw / 2 + (exterior ? sign * c : 0);
                    double to = u0 + sign * ext1;
                    var bar = SlabTopBar(lbl, pos, d.Top2, g, from, to, true, zT, hk, exterior, 0, g.L2);
                    bars.Add(bar);
                }
                var e = new DetailedElement { Label = lbl, Category = "Sàn", Bars = BarMarker.Assign(bars), Geometry = g };
                e.SourceIds.Add(s.Id);
                list.Add(e);
            }
            return list;
        }

        static bool IsExteriorEdge(Dictionary<SlabElem, SlabGeometry> geos, SlabElem self, P2 a, P2 b)
        {
            var mid = (a + b) * 0.5;
            return !geos.Keys.Any(o => o != self && o.Story == self.Story && Edges(o.Boundary).Any(e => e.DistanceTo(mid) < Tol.Snap));
        }

        static IEnumerable<Seg2> Edges(List<P2> poly)
        {
            for (int i = 0; i < poly.Count; i++) yield return new Seg2(poly[i], poly[(i + 1) % poly.Count]);
        }

        /// <summary>Lưới thép lớp dưới: thanh dài [a0,a1] theo phương U (alongU) hoặc V, rải trên [b0,b1].</summary>
        DetailBar SlabSet(string lbl, BarRole role, string pos, SlabBar sb, SlabGeometry g, double a0, double a1, double b0, double b1, double z, bool alongU, double e0, double e1)
        {
            var bar = new DetailBar { Element = lbl, Role = role, Position = pos, D = sb.D, Shape = ShapeKind.Straight, Segments = { Math.Round((a1 - a0) / 10) * 10 }, Spacing = sb.S };
            int n = (int)Math.Floor((b1 - b0 - 100) / sb.S) + 1;
            double start = b0 + (b1 - b0 - (n - 1) * sb.S) / 2;
            var p0 = alongU ? g.ToGlobal(a0, start, z) : g.ToGlobal(start, a0, z);
            var p1 = alongU ? g.ToGlobal(a1, start, z) : g.ToGlobal(start, a1, z);
            var dir = alongU ? g.V : g.U;
            bar.Placements.Add(new BarPlacement { Count = n, Spacing = sb.S, Path = { p0, p1 }, Direction = new P3(dir.X, dir.Y, 0), Normal = new P3(dir.X, dir.Y, 0), X0 = start, X1 = start + (n - 1) * sb.S });
            bar.Count = n;
            return bar;
        }

        DetailBar SlabTopBar(string lbl, string pos, SlabBar sb, SlabGeometry g, double from, double to, bool alongU, double z, double hk, bool exterior, double r0, double r1)
        {
            double L = Math.Abs(to - from);
            var bar = new DetailBar { Element = lbl, Role = BarRole.SlabTop, Position = pos.Substring(0, 1), D = sb.D, Spacing = sb.S };
            if (exterior) { bar.Shape = ShapeKind.Crank; bar.Segments.AddRange(new[] { Math.Round(hk / 10) * 10, Math.Round(L / 10) * 10, Math.Round(hk / 10) * 10 }); }
            else { bar.Shape = ShapeKind.LHook; bar.Segments.AddRange(new[] { Math.Round(L / 10) * 10, Math.Round(hk / 10) * 10 }); }
            int n = (int)Math.Floor((r1 - r0 - 100) / sb.S) + 1;
            double start = r0 + (r1 - r0 - (n - 1) * sb.S) / 2;
            Func<double, double, P3> P = (along, across) => alongU ? g.ToGlobal(along, across, z) : g.ToGlobal(across, along, z);
            var pl = new BarPlacement { Count = n, Spacing = sb.S };
            if (exterior) pl.Path.Add(Down(P(from, start), hk));
            pl.Path.Add(P(from, start));
            pl.Path.Add(P(to, start));
            pl.Path.Add(Down(P(to, start), hk));
            var dir = alongU ? g.V : g.U;
            var nrm = alongU ? g.V : g.U;
            pl.Direction = new P3(dir.X, dir.Y, 0); pl.Normal = new P3(nrm.X, nrm.Y, 0);
            bar.Placements.Add(pl);
            bar.Count = n;
            return bar;
        }

        static P3 Down(P3 p, double h) => new P3(p.X, p.Y, p.Z - h);

        // ===================== MÓNG ĐƠN =====================
        public DetailedElement DetailFooting(FootingElem f, FootingDesignResult d)
        {
            double c = set.CoverFooting;
            var bars = new List<DetailBar>();
            double hk = Math.Min(f.H - 2 * c, Math.Max(150, 10 * d.BarsL.D));
            foreach (var (sb, pos, alongX, span, across, layer) in new[] { (d.BarsL, "X", true, f.L, f.B, 0), (d.BarsB, "Y", false, f.B, f.L, 1) })
            {
                double L = span - 2 * c;
                var bar = new DetailBar { Element = f.Label, Role = BarRole.FootingBottom, Position = pos, D = sb.D, Shape = ShapeKind.UHook, Segments = { hk, L, hk }, Spacing = sb.S };
                int n = (int)Math.Floor((across - 2 * c) / sb.S) + 1;
                double z = f.BottomZ + c + sb.D / 2.0 + layer * sb.D;
                double s0 = -(n - 1) * sb.S / 2;
                Func<double, double, double, P3> P = (a, b, zz) => alongX ? new P3(f.Center.X + a, f.Center.Y + b, zz) : new P3(f.Center.X + b, f.Center.Y + a, zz);
                var pl = new BarPlacement { Count = n, Spacing = sb.S,
                    Path = { P(-L / 2, s0, z + hk), P(-L / 2, s0, z), P(L / 2, s0, z), P(L / 2, s0, z + hk) },
                    Direction = alongX ? new P3(0, 1, 0) : new P3(1, 0, 0), Normal = alongX ? new P3(0, 1, 0) : new P3(1, 0, 0) };
                bar.Placements.Add(pl);
                bar.Count = n;
                bars.Add(bar);
            }
            var e = new DetailedElement { Label = f.Label, Category = "Móng", Bars = BarMarker.Assign(bars) };
            e.SourceIds.Add(f.Id);
            return e;
        }
    }
}
