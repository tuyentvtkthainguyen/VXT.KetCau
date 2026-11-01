using System;
using System.Collections.Generic;
using System.Linq;
using VXT.KetCau.Core.Codes;
using VXT.KetCau.Core.Design;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;

namespace VXT.KetCau.Core.Detailing
{
    /// <summary>Gối tựa của dầm liên tục (cột hoặc dầm đỡ).</summary>
    public class BeamSupport
    {
        /// <summary>Vị trí tim gối theo trục dầm (mm, tính từ gốc chuỗi).</summary>
        public double X { get; set; }
        /// <summary>Bề rộng gối theo phương dầm (mm); 0 = đầu tự do (công-xôn).</summary>
        public double Width { get; set; }
        public string GridName { get; set; }
        /// <summary>"Cột" / "Dầm" / "Tự do".</summary>
        public string Kind { get; set; }
        public double FaceLeft => X - Width / 2;
        public double FaceRight => X + Width / 2;
    }

    public class StirrupZone
    {
        public int Span { get; set; }
        public double X0 { get; set; }
        public double X1 { get; set; }
        public StirrupSpec Spec { get; set; }
        public int Count { get; set; }
        /// <summary>"Gối trái" / "Giữa nhịp" / "Gối phải".</summary>
        public string Name { get; set; }
    }

    /// <summary>Hình học dầm liên tục — dùng cho dựng thép Revit và vẽ mặt đứng/mặt cắt.</summary>
    public class BeamChainGeometry
    {
        public P2 Origin { get; set; }
        public P2 Dir { get; set; }
        public double TopZ { get; set; }
        public double B { get; set; }
        public double H { get; set; }
        public List<BeamSupport> Supports { get; } = new List<BeamSupport>();
        public List<StirrupZone> StirrupZones { get; } = new List<StirrupZone>();
        /// <summary>Vị trí các mặt cắt (x, tên "1-1", "2-2"...).</summary>
        public List<(double X, string Name, int Span)> Sections { get; } = new List<(double, string, int)>();
        public double Length => Supports.Count > 0 ? Supports.Last().FaceRight - Supports.First().FaceLeft : 0;
        public P2 Perp => new P2(-Dir.Y, Dir.X);

        /// <summary>Đổi toạ độ cục bộ (x dọc dầm, y ngang, z) → toàn cục.</summary>
        public P3 ToGlobal(double x, double y, double z)
        {
            var p = Origin + Dir * x + Perp * y;
            return new P3(p.X, p.Y, z);
        }
    }

    /// <summary>Chuỗi dầm liên tục (nhiều nhịp thẳng hàng cùng tầng).</summary>
    public class BeamChain
    {
        public string Label { get; set; }
        public string Story { get; set; }
        public List<BeamElem> Spans { get; } = new List<BeamElem>();
        public BeamChainGeometry Geo { get; set; }
    }

    /// <summary>Gom dầm thẳng hàng thành dầm liên tục và xác định gối tựa.</summary>
    public static class BeamChainBuilder
    {
        public static List<BeamChain> Build(StructModel m)
        {
            var chains = new List<BeamChain>();
            int storyIdx = 0;
            foreach (var st in m.Stories.OrderBy(s => s.Elevation))
            {
                storyIdx++;
                var beams = m.Beams.Where(b => b.Story == st.Name).ToList();
                var used = new HashSet<BeamElem>();
                int n = 0;
                // ưu tiên dầm chính trước, theo phương X rồi Y
                foreach (var seed in beams.OrderBy(b => b.IsSecondary).ThenBy(b => Math.Abs((b.End - b.Start).Unit.Y) > 0.7)
                                          .ThenBy(b => Math.Min(b.Start.Y, b.End.Y)).ThenBy(b => Math.Min(b.Start.X, b.End.X)))
                {
                    if (used.Contains(seed)) continue;
                    var dir = (seed.End - seed.Start).Unit;
                    var sec = m.Section(seed.Section);
                    var line = new Seg2(seed.Start, seed.End);
                    var group = beams.Where(b => !used.Contains(b) && b.Section == seed.Section && b.IsSecondary == seed.IsSecondary
                                                 && Math.Abs(Math.Abs((b.End - b.Start).Unit.Dot(dir)) - 1) < 1e-3
                                                 && DistToLine(b.Start, seed.Start, dir) < Tol.Mm && DistToLine(b.End, seed.Start, dir) < Tol.Mm)
                                     .ToList();
                    // chuẩn hoá chiều và sắp xếp theo trục
                    foreach (var b in group)
                        if ((b.End - b.Start).Dot(dir) < 0) Reverse(b);
                    var sorted = group.OrderBy(b => (b.Start - seed.Start).Dot(dir)).ToList();
                    // tách thành các chuỗi liền mạch
                    var cur = new List<BeamElem>();
                    foreach (var b in sorted)
                    {
                        if (cur.Count > 0 && !cur.Last().End.Equals(b.Start))
                        { chains.Add(Make(m, st, storyIdx, ++n, cur)); cur = new List<BeamElem>(); }
                        cur.Add(b);
                    }
                    if (cur.Count > 0) chains.Add(Make(m, st, storyIdx, ++n, cur));
                    foreach (var b in group) used.Add(b);
                }
            }
            return chains;
        }

        static double DistToLine(P2 p, P2 o, P2 dir) => Math.Abs((p - o).Cross(dir));

        /// <summary>Đảo chiều dầm, đồng thời đảo nội lực và kết quả thiết kế trái ↔ phải cho khớp.</summary>
        public static void Reverse(BeamElem b)
        {
            var t = b.Start; b.Start = b.End; b.End = t;
            if (b.Forces != null)
            {
                var f = b.Forces;
                double L = b.Length;
                foreach (var s in f.Stations) { s.X = L - s.X; s.V = -s.V; }
                var m = f.MnegLeft; f.MnegLeft = f.MnegRight; f.MnegRight = m;
                var v = f.VmaxLeft; f.VmaxLeft = f.VmaxRight; f.VmaxRight = v;
            }
            if (b.Design != null)
            {
                var d = b.Design;
                var fl = d.TopLeft; d.TopLeft = d.TopRight; d.TopRight = fl;
                var bl = d.TopLeftBars; d.TopLeftBars = d.TopRightBars; d.TopRightBars = bl;
                var sl = d.ShearLeft; d.ShearLeft = d.ShearRight; d.ShearRight = sl;
            }
        }

        static BeamChain Make(StructModel m, Story st, int storyIdx, int n, List<BeamElem> spans)
        {
            var c = new BeamChain { Story = st.Name, Label = $"D{storyIdx}-{n}" };
            c.Spans.AddRange(spans);
            var sec = m.Section(spans[0].Section);
            var dir = (spans[0].End - spans[0].Start).Unit;
            var g = new BeamChainGeometry { Origin = spans[0].Start, Dir = dir, TopZ = spans[0].TopZ, B = sec.B, H = sec.H };
            var nodes = new List<P2> { spans[0].Start };
            nodes.AddRange(spans.Select(s => s.End));
            foreach (var p in nodes)
            {
                double x = (p - g.Origin).Dot(dir);
                var sup = new BeamSupport { X = x, Kind = "Tự do", Width = 0 };
                var col = m.Columns.FirstOrDefault(cl => cl.Location.Equals(p) && cl.TopZ >= g.TopZ - 10 && cl.BaseZ < g.TopZ - 10);
                if (col != null)
                {
                    var cs = m.Section(col.Section);
                    double rot = col.RotationDeg * Math.PI / 180;
                    var ax = new P2(Math.Cos(rot), Math.Sin(rot));
                    // bề rộng cột theo phương dầm
                    sup.Width = Math.Abs(ax.Dot(dir)) > 0.7 ? cs.B : cs.H;
                    sup.Kind = "Cột";
                }
                else
                {
                    // dầm đỡ đi qua điểm này
                    var carrier = m.Beams.FirstOrDefault(b => b.Story == st.Name && !spans.Contains(b)
                                                         && Math.Abs((b.End - b.Start).Unit.Dot(dir)) < 0.3
                                                         && new Seg2(b.Start, b.End).DistanceTo(p) < Tol.Mm);
                    if (carrier != null) { sup.Width = m.Section(carrier.Section).B; sup.Kind = "Dầm"; }
                }
                sup.GridName = m.Grids.Where(gl => Math.Abs((gl.Line.B - gl.Line.A).Unit.Dot(dir)) < 0.3 && gl.Line.DistanceTo(p) < Tol.Snap)
                                      .Select(gl => gl.Name).FirstOrDefault();
                g.Supports.Add(sup);
            }
            c.Geo = g;
            return c;
        }
    }

    /// <summary>
    /// Cấu tạo thép dầm liên tục theo kết quả thiết kế từng nhịp:
    /// thép trên chạy suốt (nối chồng giữa nhịp khi vượt chiều dài cây thép), thép dưới từng nhịp neo vào gối,
    /// mũ gối L/4 từ mép gối (đối xứng dùng chung số), tăng cường dưới 0.8Ln, đai 3 vùng (gối L/4 – giữa – gối L/4),
    /// thép giá khi h &gt; 700 kèm đai C.
    /// </summary>
    public class BeamDetailer
    {
        readonly DesignSettings set;
        readonly ConcreteGrade C;
        readonly SteelGrade S;
        public BeamDetailer(DesignSettings s) { set = s; C = ConcreteGrade.Get(s.Concrete); S = SteelGrade.Get(s.MainSteel); }

        public DetailedElement Detail(BeamChain chain)
        {
            var g = chain.Geo;
            var bars = new List<DetailBar>();
            double c = set.CoverBeam;
            var designs = chain.Spans.Select(s => s.Design).ToList();
            if (designs.Any(d => d == null)) throw new InvalidOperationException($"Dầm {chain.Label} chưa được thiết kế");
            int dsw = designs.Max(d => d.StirrupSupport.D);
            double b = g.B, h = g.H;
            double zTop(int d, int layer = 0) => g.TopZ - c - dsw - d / 2.0 - layer * (d + 25);
            double zBot(int d, int layer = 0) => g.TopZ - h + c + dsw + d / 2.0 + layer * (d + 25);
            double hookMax = h - 2 * c - 2 * dsw;
            var sup = g.Supports;
            int nSpan = chain.Spans.Count;
            double clear(int j) => sup[j + 1].FaceLeft - sup[j].FaceRight;
            double xStartAnch = sup[0].Width > 0 ? sup[0].FaceLeft + c : sup[0].X + c;
            double xEndAnch = sup[nSpan].Width > 0 ? sup[nSpan].FaceRight - c : sup[nSpan].X - c;

            // ---------- Thép trên chạy suốt ----------
            int dTop = designs.Min(d => Math.Min(d.TopLeftBars.D, d.TopRightBars.D));
            int nTopCont = 2;
            {
                double lan = Tcvn5574.Anchorage(dTop, S, C);
                double lap = Tcvn5574.LapLength(dTop, S, C);
                var mids = Enumerable.Range(0, nSpan).Select(j => (sup[j].FaceRight + sup[j + 1].FaceLeft) / 2).ToList();
                double hkEnds = EndHook(dTop, lan, sup[0], c, hookMax) + EndHook(dTop, lan, sup[nSpan], c, hookMax);
                var pieces = SplitForStock(xStartAnch, xEndAnch, mids, lap, set.StockBarLength - hkEnds);
                for (int k = 0; k < pieces.Count; k++)
                {
                    var (x0, x1) = pieces[k];
                    double hk0 = k == 0 ? EndHook(dTop, lan, sup[0], c, hookMax) : 0;
                    double hk1 = k == pieces.Count - 1 ? EndHook(dTop, lan, sup[nSpan], c, hookMax) : 0;
                    var bar = MakeLong(chain.Label, BarRole.Top, dTop, x0, x1, hk0, hk1, true);
                    AddLongPlacements(bar, g, x0, x1, hk0, hk1, zTop(dTop), BarYs(b, c, dsw, dTop, nTopCont), true);
                    bars.Add(bar);
                }
            }

            // ---------- Theo từng nhịp ----------
            for (int j = 0; j < nSpan; j++)
            {
                var d = designs[j];
                double Ln = clear(j);
                double fL = sup[j].FaceRight, fR = sup[j + 1].FaceLeft;

                // Thép dưới chủ: 2 thanh góc (3 nếu tổng ≤ 3 thì toàn bộ)
                var bot = d.BottomBars;
                int nCont = bot.N <= 3 ? bot.N : 2;
                double lanB = Tcvn5574.Anchorage(bot.D, S, C);
                // gối giữa: thép dưới neo vào gối max(15d, min(lan, bề rộng gối − c)); gối hẹp thì kéo sang nhịp bên cạnh (vùng nén)
                double anchL = j == 0 ? 0 : Math.Max(15 * bot.D, Math.Min(lanB, sup[j].Width - c));
                double anchR = j == nSpan - 1 ? 0 : Math.Max(15 * bot.D, Math.Min(lanB, sup[j + 1].Width - c));
                double x0 = j == 0 ? xStartAnch : fL - anchL;
                double x1 = j == nSpan - 1 ? xEndAnch : fR + anchR;
                double h0b = j == 0 ? EndHook(bot.D, lanB, sup[0], c, hookMax) : 0;
                double h1b = j == nSpan - 1 ? EndHook(bot.D, lanB, sup[nSpan], c, hookMax) : 0;
                var allY = BarYs(b, c, dsw, bot.D, bot.N);
                var contY = bot.N <= 3 ? allY : new List<double> { allY.First(), allY.Last() };
                var bb = MakeLong(chain.Label, BarRole.Bottom, bot.D, x0, x1, h0b, h1b, false);
                bb.Span = j;
                AddLongPlacements(bb, g, x0, x1, h0b, h1b, zBot(bot.D), contY, false);
                bars.Add(bb);

                // Tăng cường dưới
                if (bot.N > nCont)
                {
                    var addY = allY.Skip(1).Take(allY.Count - 2).ToList();
                    double ax0 = fL + 0.1 * Ln, ax1 = fR - 0.1 * Ln;
                    var ab = MakeLong(chain.Label, BarRole.Additional, bot.D, ax0, ax1, 0, 0, false);
                    ab.Span = j;
                    AddLongPlacements(ab, g, ax0, ax1, 0, 0, zBot(bot.D), addY, false);
                    bars.Add(ab);
                }

                // Thép giá h > 700
                if (h > 700)
                {
                    int rows = Math.Max(1, (int)Math.Floor((h - 2 * c - 2 * dsw) / 400.0));
                    double sx0 = fL - 15 * 12, sx1 = fR + 15 * 12;
                    var sb = MakeLong(chain.Label, BarRole.Side, 12, sx0, sx1, 0, 0, false);
                    sb.Span = j;
                    for (int r = 1; r <= rows; r++)
                    {
                        double z = zBot(12) + (zTop(12) - zBot(12)) * r / (rows + 1);
                        AddLongPlacements(sb, g, sx0, sx1, 0, 0, z, BarYs(b, c, dsw, 12, 2), z > g.TopZ - h / 2);
                    }
                    sb.Count = 2 * rows;
                    bars.Add(sb);
                }
            }

            // ---------- Mũ gối ----------
            for (int i = 0; i <= nSpan; i++)
            {
                var left = i > 0 ? designs[i - 1].TopRightBars : null;
                var right = i < nSpan ? designs[i].TopLeftBars : null;
                double asReq = Math.Max(left?.Area ?? 0, right?.Area ?? 0);
                var gov = (left?.Area ?? 0) >= (right?.Area ?? 0) ? left : right;
                if (gov == null) continue;
                double asCont = nTopCont * Tcvn5574.BarArea(dTop);
                if (asReq <= asCont + 1) continue;
                int dS = gov.D;
                int nS = Math.Max(1, (int)Math.Ceiling((asReq - asCont) / Tcvn5574.BarArea(dS) - 1e-9));
                // mũ gối đặt xen giữa 2 thanh chạy suốt; nếu không đủ chỗ thì lớp 2
                int nMax1 = Math.Max(2, (int)Math.Floor((b - 2 * c - 2 * dsw + Math.Max(25, dS)) / (dS + Math.Max(25, dS)))) - nTopCont;
                double Lmax = Math.Max(i > 0 ? clear(i - 1) : 0, i < nSpan ? clear(i) : 0);
                double ext = GeomUtil.RoundUp(Lmax / 4, 10);
                double lanS = Tcvn5574.Anchorage(dS, S, C);
                double sx0 = i == 0 ? xStartAnch : sup[i].FaceLeft - ext;
                double sx1 = i == nSpan ? xEndAnch : sup[i].FaceRight + ext;
                double hk0 = i == 0 ? EndHook(dS, lanS, sup[0], c, hookMax) : 0;
                double hk1 = i == nSpan ? EndHook(dS, lanS, sup[nSpan], c, hookMax) : 0;
                var mb = MakeLong(chain.Label, BarRole.SupportTop, dS, sx0, sx1, hk0, hk1, true);
                mb.OrderX = sup[i].X;
                var ys = new List<double>();
                var allY = BarYs(b, c, dsw, dS, nTopCont + Math.Min(nS, Math.Max(0, nMax1)));
                ys.AddRange(allY.Skip(1).Take(allY.Count - 2));
                int n2 = nS - ys.Count;
                AddLongPlacements(mb, g, sx0, sx1, hk0, hk1, zTop(dS), ys, true);
                if (n2 > 0) AddLongPlacements(mb, g, sx0, sx1, hk0, hk1, zTop(dS, 1), BarYs(b, c, dsw, dS, Math.Max(2, n2)).Take(n2).ToList(), true);
                mb.Count = nS;
                bars.Add(mb);
            }

            // ---------- Đai ----------
            double sa = b - 2 * c, sbH = h - 2 * c;
            var stirrup = new DetailBar { Element = chain.Label, Role = BarRole.Stirrup, D = dsw, Shape = ShapeKind.Stirrup, Segments = { sa, sbH }, OrderX = 0 };
            int nSecName = 0;
            for (int j = 0; j < nSpan; j++)
            {
                var d = designs[j];
                double fL = sup[j].FaceRight, fR = sup[j + 1].FaceLeft, Ln = fR - fL;
                double s1 = d.StirrupSupport.S, s2 = d.StirrupSpan.S;
                double zone = Ln / 4;
                int n1 = Math.Max(1, (int)Math.Floor((zone - 50) / s1) + 1);
                double lastL = fL + 50 + (n1 - 1) * s1;
                double firstR = fR - 50 - (n1 - 1) * s1;
                double gap = firstR - lastL;
                int nMid = Math.Max(0, (int)Math.Ceiling(gap / s2 - 1e-9) - 1);
                double sMid = nMid > 0 ? gap / (nMid + 1) : gap;
                // ranh giới vùng đặt ở giữa 2 đai
                double bL = nMid > 0 ? lastL + sMid / 2 : (lastL + firstR) / 2;
                double bR = nMid > 0 ? firstR - sMid / 2 : (lastL + firstR) / 2;
                g.StirrupZones.Add(new StirrupZone { Span = j, X0 = fL, X1 = bL, Spec = d.StirrupSupport, Count = n1, Name = "Gối trái" });
                if (nMid > 0) g.StirrupZones.Add(new StirrupZone { Span = j, X0 = bL, X1 = bR, Spec = new StirrupSpec { D = dsw, Legs = d.StirrupSpan.Legs, S = s2 }, Count = nMid, Name = "Giữa nhịp" });
                g.StirrupZones.Add(new StirrupZone { Span = j, X0 = bR, X1 = fR, Spec = d.StirrupSupport, Count = n1, Name = "Gối phải" });

                AddStirrupSet(stirrup, g, fL + 50, n1, s1, b, h, c, dsw);
                if (nMid > 0) AddStirrupSet(stirrup, g, lastL + sMid, nMid, sMid, b, h, c, dsw, s2);
                AddStirrupSet(stirrup, g, firstR, n1, s1, b, h, c, dsw);
                stirrup.Count += 2 * n1 + nMid;

                // mặt cắt: 1-1 tại gối (vị trí đai dày), 2-2 giữa nhịp; nhịp sau giống nhịp trước thì dùng lại tên
                g.Sections.Add((fL + zone / 2, $"{++nSecName}-{nSecName}", j));
                g.Sections.Add(((fL + fR) / 2, $"{++nSecName}-{nSecName}", j));
            }
            stirrup.Spacing = designs.Min(d => d.StirrupSupport.S);
            bars.Add(stirrup);

            // Đai C cho thép giá
            if (h > 700)
            {
                var ct = new DetailBar { Element = chain.Label, Role = BarRole.CTie, D = 6, Shape = ShapeKind.CTie, Segments = { b - 2 * c }, OrderX = 0 };
                for (int j = 0; j < nSpan; j++)
                {
                    double fL = sup[j].FaceRight, fR = sup[j + 1].FaceLeft;
                    double s = 2 * designs[j].StirrupSpan.S;
                    int n = Math.Max(1, (int)Math.Floor((fR - fL - 100) / s) + 1);
                    ct.Count += n;
                    ct.Spacing = s;
                    ct.Placements.Add(new BarPlacement { Count = n, Spacing = s, X0 = fL + 50, X1 = fR - 50, Direction = Dir3(g), Normal = Dir3(g),
                        Path = { g.ToGlobal(fL + 50, -(b / 2 - c), g.TopZ - h / 2), g.ToGlobal(fL + 50, b / 2 - c, g.TopZ - h / 2) } });
                }
                bars.Add(ct);
            }

            var marked = BarMarker.Assign(bars);
            return new DetailedElement
            {
                Label = chain.Label,
                Category = "Dầm",
                TotalLength = Math.Round(g.Length / 10) * 10,
                Bars = marked,
                Geometry = g,
            }.WithSources(chain.Spans.Select(s => s.Id));
        }

        static P3 Dir3(BeamChainGeometry g) => new P3(g.Dir.X, g.Dir.Y, 0);

        /// <summary>Móc neo đầu dầm: nếu đoạn thẳng trong gối &lt; lan thì bẻ 90° xuống/lên, ≥ 12d, ≤ chiều cao khả dụng.</summary>
        static double EndHook(int d, double lan, BeamSupport s, double c, double hookMax)
        {
            double straight = Math.Max(0, s.Width - c);
            if (s.Width <= 0) return Math.Min(Math.Floor(hookMax / 10) * 10, 12 * d); // đầu công-xôn: bẻ móc cấu tạo
            if (straight >= lan) return 0;
            double hk = Math.Max(12 * d, lan - straight);
            // làm tròn lên 10 mm nhưng không vượt chiều cao khả dụng (làm tròn xuống theo hookMax)
            return Math.Min(Math.Ceiling(hk / 10) * 10, Math.Floor(hookMax / 10) * 10);
        }

        DetailBar MakeLong(string elem, BarRole role, int d, double x0, double x1, double hk0, double hk1, bool upper)
        {
            var bar = new DetailBar { Element = elem, Role = role, D = d, OrderX = x0 };
            double L = x1 - x0;
            if (hk0 > 0 && hk1 > 0) { bar.Shape = ShapeKind.UHook; bar.Segments.AddRange(new[] { hk0, L, hk1 }); }
            else if (hk0 > 0) { bar.Shape = ShapeKind.LHook; bar.Segments.AddRange(new[] { hk0, L }); }
            else if (hk1 > 0) { bar.Shape = ShapeKind.LHook; bar.Segments.AddRange(new[] { L, hk1 }); }
            else { bar.Shape = ShapeKind.Straight; bar.Segments.Add(L); }
            // làm tròn đoạn thẳng 10 mm để số hiệu ổn định
            for (int i = 0; i < bar.Segments.Count; i++) bar.Segments[i] = Math.Round(bar.Segments[i] / 10) * 10;
            return bar;
        }

        static void AddLongPlacements(DetailBar bar, BeamChainGeometry g, double x0, double x1, double hk0, double hk1, double z, List<double> ys, bool upper)
        {
            double sgn = upper ? -1 : 1; // thép trên móc xuống, thép dưới móc lên
            foreach (var y in ys)
            {
                var pl = new BarPlacement { X0 = x0, X1 = x1, UpperHalf = upper, Normal = new P3(g.Perp.X, g.Perp.Y, 0) };
                if (hk0 > 0) pl.Path.Add(g.ToGlobal(x0, y, z + sgn * hk0));
                pl.Path.Add(g.ToGlobal(x0, y, z));
                pl.Path.Add(g.ToGlobal(x1, y, z));
                if (hk1 > 0) pl.Path.Add(g.ToGlobal(x1, y, z + sgn * hk1));
                bar.Placements.Add(pl);
            }
            if (bar.Count == 0) bar.Count = ys.Count; else if (bar.Role != BarRole.Side && bar.Role != BarRole.SupportTop) bar.Count += ys.Count;
        }

        static void AddStirrupSet(DetailBar st, BeamChainGeometry g, double x, int n, double s, double b, double h, double c, int d, double tagSpacing = 0)
        {
            double y = b / 2 - c - d / 2.0;
            double zt = g.TopZ - c - d / 2.0, zb = g.TopZ - h + c + d / 2.0;
            var pl = new BarPlacement
            {
                Count = n, Spacing = tagSpacing > 0 ? tagSpacing : s, X0 = x, X1 = x + (n - 1) * s,
                Direction = new P3(g.Dir.X, g.Dir.Y, 0), Normal = new P3(g.Dir.X, g.Dir.Y, 0),
            };
            pl.Path.Add(g.ToGlobal(x, -y, zt));
            pl.Path.Add(g.ToGlobal(x, y, zt));
            pl.Path.Add(g.ToGlobal(x, y, zb));
            pl.Path.Add(g.ToGlobal(x, -y, zb));
            pl.Path.Add(g.ToGlobal(x, -y, zt));
            st.Placements.Add(pl);
        }

        /// <summary>Toạ độ ngang các thanh rải đều trên bề rộng dầm.</summary>
        public static List<double> BarYs(double b, double c, int dsw, int d, int n)
        {
            double y0 = -b / 2 + c + dsw + d / 2.0, y1 = -y0;
            if (n <= 1) return new List<double> { 0 };
            return Enumerable.Range(0, n).Select(i => y0 + (y1 - y0) * i / (n - 1)).ToList();
        }

        /// <summary>Chia thanh dài thành các đoạn ≤ chiều dài cây thép, mối nối đặt giữa nhịp (vùng thép trên chịu nén).</summary>
        public static List<(double, double)> SplitForStock(double x0, double x1, List<double> mids, double lap, double stock)
        {
            var res = new List<(double, double)>();
            double start = x0;
            while (x1 - start > stock)
            {
                double limit = start + stock - lap / 2;
                var cut = mids.Where(mx => mx > start + lap && mx <= limit).DefaultIfEmpty(double.NaN).Max();
                if (double.IsNaN(cut)) cut = limit; // nhịp quá dài: nối tại giới hạn cây thép
                res.Add((start, cut + lap / 2));
                start = cut - lap / 2;
            }
            res.Add((start, x1));
            return res;
        }
    }

    static class DetailedElementExt
    {
        public static DetailedElement WithSources(this DetailedElement e, IEnumerable<string> ids) { e.SourceIds.AddRange(ids); return e; }
    }
}
