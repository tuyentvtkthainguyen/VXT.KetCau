using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VXT.KetCau.Core.Drawing;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;
using Rect = VXT.KetCau.Core.Documentation.ArchAnalysis.Rect;

namespace VXT.KetCau.Core.Documentation
{
    /// <summary>
    /// Dựng hồ sơ kiến trúc từ <see cref="ArchModel"/>: mặt bằng các tầng, mặt đứng, mặt cắt, thống kê cửa,
    /// chi tiết thang, chi tiết vệ sinh. Toạ độ mm thật; chữ in cao 2,5 mm × tỉ lệ.
    /// </summary>
    public class ArchDocComposer
    {
        readonly IDraw2D w;
        readonly ArchModel a;
        readonly StructModel m;
        readonly DocOptions opt;
        readonly List<ArchLevel> levels;
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
        public List<string> Warnings { get; } = new List<string>();

        double s = 100;
        double TH => 2.5 * s;

        public ArchDocComposer(IDraw2D surface, ArchModel arch, StructModel model, DocOptions o)
        {
            w = surface; a = arch; m = model; opt = o;
            levels = ArchAnalysis.OrderedLevels(a);
        }

        // ------------------------------------------------------------------ Tiện ích chung
        IEnumerable<(string Name, Seg2 Line)> GridList => a.Grids.Select(g => (g.Name, g.Line));
        IEnumerable<ArchWall> WallsAt(string lvl) => a.Walls.Where(x => x.BaseLevel == lvl);
        List<double> XGrids => a.Grids.Where(g => g.Line.IsVertical(2)).Select(g => g.Line.Mid.X).OrderBy(v => v).ToList();
        List<double> YGrids => a.Grids.Where(g => g.Line.IsHorizontal(2)).Select(g => g.Line.Mid.Y).OrderBy(v => v).ToList();

        Rect Extents(string lvl)
        {
            var rects = WallsAt(lvl).Select(ArchAnalysis.WallRect).ToList();
            rects.AddRange(a.Columns.Where(c => c.BaseLevel == lvl).Select(c => new Rect(c.Center.X - c.B / 2, c.Center.Y - c.H / 2, c.Center.X + c.B / 2, c.Center.Y + c.H / 2)));
            if (rects.Count == 0) rects = a.Walls.Select(ArchAnalysis.WallRect).ToList();
            if (rects.Count == 0)
            {
                var xs = XGrids; var ys = YGrids;
                return xs.Count > 0 && ys.Count > 0 ? new Rect(xs.First(), ys.First(), xs.Last(), ys.Last()) : new Rect(0, 0, 1000, 1000);
            }
            return new Rect(rects.Min(r => r.X0), rects.Min(r => r.Y0), rects.Max(r => r.X1), rects.Max(r => r.Y1));
        }

        /// <summary>Vị trí đường cắt: giữa nhịp ở khoảng giữa, tránh ô thang.</summary>
        public (double X, double Y) CutPositions()
        {
            var e = Extents(levels.First().Name);
            var xs = XGrids; var ys = YGrids;
            double cx = xs.Count >= 2 ? (xs[xs.Count / 2 - 1 + (xs.Count % 2)] + xs[Math.Min(xs.Count / 2 + (xs.Count % 2), xs.Count - 1)]) / 2 : (e.X0 + e.X1) / 2;
            double cy = ys.Count >= 2 ? (ys[ys.Count / 2 - 1 + (ys.Count % 2)] + ys[Math.Min(ys.Count / 2 + (ys.Count % 2), ys.Count - 1)]) / 2 : (e.Y0 + e.Y1) / 2;
            if (xs.Count >= 2 && Math.Abs(cx - xs[0]) < 1) cx = (xs[0] + xs[1]) / 2;
            if (ys.Count >= 2 && Math.Abs(cy - ys[0]) < 1) cy = (ys[0] + ys[1]) / 2;
            return (cx, cy);
        }

        // ================================================================== MẶT BẰNG
        public void DrawPlan(string lvl, double scale, bool withCutMarks)
        {
            s = scale; w.SetScaleX(s);
            var level = levels.First(l => l.Name == lvl);
            int idx = levels.FindIndex(l => l.Name == lvl);
            bool roof = level.IsRoof || idx == levels.Count - 1 && !WallsAt(lvl).Any();
            var ann = new PlanAnnotation(w, TH);
            var e = roof && idx > 0 ? Extents(levels[idx - 1].Name) : Extents(lvl);

            if (roof) DrawRoofOutline(idx);
            else DrawWallsAndOpenings(lvl);

            foreach (var st in a.Stairs.Where(x => x.BaseLevel == lvl)) DrawStairPlan(st, false);
            if (!roof) foreach (var r in a.Rooms.Where(x => x.Level == lvl)) DrawRoomLabel(r, level);

            ann.Grids(GridList, e.X0, e.Y0, e.X1, e.Y1, roof ? 0 : 1);
            if (!roof) ExteriorChains(lvl, e, ann);

            if (withCutMarks && !roof)
            {
                var (cx, cy) = CutPositions();
                double o = ann.ChainOffset(2) + TH * 5.5;
                ann.CutMark(cx, e.Y1 + o, cx, e.Y0 - o, "A");
                ann.CutMark(e.X0 - o, cy, e.X1 + o, cy, "B");
            }
            double ty = e.Y0 - ann.ChainOffset(2) - TH * (withCutMarks ? 11 : 7.5);
            string title = roof ? "MẶT BẰNG MÁI" : "MẶT BẰNG " + lvl.ToUpperInvariant();
            ann.Title((e.X0 + e.X1) / 2, ty, $"{title} ({ShopDrawingComposer.LevelText(level.Elevation)})", s);
        }

        void DrawRoofOutline(int idx)
        {
            if (idx <= 0) return;
            string below = levels[idx - 1].Name;
            var solids = WallsAt(below).Where(x => x.IsExterior && ArchAnalysis.IsOrtho(x.Line)).Select(ArchAnalysis.WallRect).ToList();
            var mask = new WallMask(solids, new Rect[0]);
            foreach (var q in mask.Edges()) w.Line("TUONG", q.A.X, q.A.Y, q.B.X, q.B.Y);
            var e = Extents(below);
            w.Text("CHU", (e.X0 + e.X1) / 2, (e.Y0 + e.Y1) / 2, TH, "SÀN MÁI – độ dốc và thoát nước theo thiết kế", 1, 2, 0);
        }

        void DrawWallsAndOpenings(string lvl)
        {
            var walls = WallsAt(lvl).ToList();
            var ortho = walls.Where(x => ArchAnalysis.IsOrtho(x.Line)).ToList();
            var opens = a.Openings.Where(o => o.Level == lvl && o.HostWallId != null).ToList();
            var solids = ortho.Select(ArchAnalysis.WallRect).ToList();
            var cols = a.Columns.Where(c => c.BaseLevel == lvl).ToList();
            solids.AddRange(cols.Where(c => Math.Abs(c.RotationDeg) < 0.5).Select(ColRect));
            var holes = new List<Rect>();
            foreach (var o in opens)
            {
                var host = ortho.FirstOrDefault(x => x.Id == o.HostWallId);
                if (host == null) continue;
                var d = host.Line.Dir; var n = new P2(-d.Y, d.X);
                var c = Project(host, o.Location);
                var p0 = c - d * (o.Width / 2) - n * (host.Thickness / 2 + 2);
                var p1 = c + d * (o.Width / 2) + n * (host.Thickness / 2 + 2);
                holes.Add(new Rect(p0.X, p0.Y, p1.X, p1.Y));
            }
            var mask = new WallMask(solids, holes);
            foreach (var q in mask.FillRects())
                w.HatchX("HATCH", new[] { (q.X0, q.Y0), (q.X1, q.Y0), (q.X1, q.Y1), (q.X0, q.Y1) }, "ANSI31", 1.5 * s);
            foreach (var q in mask.Edges()) w.Line("TUONG", q.A.X, q.A.Y, q.B.X, q.B.Y);

            // Tường xiên: vẽ từng đoạn giữa các lỗ cửa
            foreach (var x in walls.Except(ortho)) DrawSkewWall(x, opens.Where(o => o.HostWallId == x.Id).ToList());

            foreach (var c in cols)
            {
                var pts = ColCorners(c);
                w.HatchX("HATCH", pts, "SOLID", 1.5 * s);
                w.Poly("COT", pts, true);
            }
            var e = Extents(lvl);
            var center = new P2((e.X0 + e.X1) / 2, (e.Y0 + e.Y1) / 2);
            foreach (var o in opens)
            {
                var host = walls.FirstOrDefault(x => x.Id == o.HostWallId);
                if (host == null) continue;
                DrawOpening(o, host, center);
            }
        }

        static Rect ColRect(ArchColumn c) => new Rect(c.Center.X - c.B / 2, c.Center.Y - c.H / 2, c.Center.X + c.B / 2, c.Center.Y + c.H / 2);

        static List<(double x, double y)> ColCorners(ArchColumn c)
        {
            double r = c.RotationDeg * Math.PI / 180, cs = Math.Cos(r), sn = Math.Sin(r), hb = c.B / 2, hh = c.H / 2;
            return new[] { (-hb, -hh), (hb, -hh), (hb, hh), (-hb, hh) }
                .Select(p => (c.Center.X + p.Item1 * cs - p.Item2 * sn, c.Center.Y + p.Item1 * sn + p.Item2 * cs)).ToList();
        }

        void DrawSkewWall(ArchWall x, List<ArchOpening> opens)
        {
            var d = x.Line.Dir; var n = new P2(-d.Y, d.X); double t = x.Thickness / 2;
            var cuts = new List<(double a, double b)>();
            foreach (var o in opens) { double u = (o.Location - x.Line.A).Dot(d); cuts.Add((u - o.Width / 2, u + o.Width / 2)); }
            double cur = 0, L = x.Line.Length;
            foreach (var c in cuts.OrderBy(q => q.a).Concat(new[] { (a: L, b: L) }))
            {
                if (c.a > cur + 1)
                {
                    var p0 = x.Line.A + d * cur; var p1 = x.Line.A + d * c.a;
                    var poly = new[] { p0 - n * t, p1 - n * t, p1 + n * t, p0 + n * t }.Select(p => (p.X, p.Y)).ToList();
                    w.HatchX("HATCH", poly, "ANSI31", 1.5 * s);
                    w.Poly("TUONG", poly, true);
                }
                cur = Math.Max(cur, c.b);
            }
        }

        static P2 Project(ArchWall host, P2 p)
        {
            var d = host.Line.Dir;
            return host.Line.A + d * (p - host.Line.A).Dot(d);
        }

        void DrawOpening(ArchOpening o, ArchWall host, P2 center)
        {
            var d = host.Line.Dir; var n = new P2(-d.Y, d.X);
            var c = Project(host, o.Location);
            double t = host.Thickness / 2, half = o.Width / 2;
            int side = o.SwingSide != 0 ? Math.Sign(o.SwingSide) : ((center - c).Dot(n) >= 0 ? 1 : -1);
            var tagAt = c - n * (side * (t + TH * 1.5));
            if (ArchAnalysis.IsDoor(o))
            {
                Action<P2, double, double> leaf = (hinge, sign, width) =>
                {
                    var end = hinge + n * (side * width);
                    w.Line("CUA", hinge.X, hinge.Y, end.X, end.Y);
                    // cung quét từ cánh mở về khung đối diện
                    double a1 = Math.Atan2(n.Y * side, n.X * side) * 180 / Math.PI;
                    double a2 = Math.Atan2(d.Y * sign, d.X * sign) * 180 / Math.PI;
                    double delta = ((a2 - a1) % 360 + 360) % 360;
                    if (delta <= 180) w.ArcX("CUA", hinge.X, hinge.Y, width, a1, a2);
                    else w.ArcX("CUA", hinge.X, hinge.Y, width, a2, a1);
                };
                var face = c + n * (side * t);
                if (o.Width >= 1500)
                {
                    leaf(face - d * half, 1, half);
                    leaf(face + d * half, -1, half);
                }
                else
                {
                    double sign = o.HingeEnd == 0 ? 1 : -1;
                    leaf(face - d * (half * sign), sign, o.Width);
                }
            }
            else
            {
                foreach (var f in new[] { -t, -t / 3, t / 3, t })
                {
                    var p0 = c - d * half + n * f; var p1 = c + d * half + n * f;
                    w.Line("CUA", p0.X, p0.Y, p1.X, p1.Y);
                }
                foreach (var sg in new[] { -1.0, 1.0 })
                {
                    var p0 = c + d * (sg * half) - n * t; var p1 = c + d * (sg * half) + n * t;
                    w.Line("CUA", p0.X, p0.Y, p1.X, p1.Y);
                }
            }
            if (!string.IsNullOrEmpty(o.Mark)) w.Text("CHU", tagAt.X, tagAt.Y, TH, o.Mark, 1, 2, 0);
        }

        void DrawRoomLabel(ArchRoom r, ArchLevel level)
        {
            if (r.Boundary.Count < 3) return;
            double area = Math.Abs(GeomUtil.Area(r.Boundary)) / 1e6;
            double cx = r.Boundary.Average(p => p.X), cy = r.Boundary.Average(p => p.Y);
            w.Text("PHONG", cx, cy + TH * 1.0, TH * 1.1, r.Name, 1, 2, 0);
            w.Text("CHU", cx, cy - TH * 0.6, TH * 0.9, ShopDrawingComposer.LevelText(level.Elevation), 1, 2, 0);
            if (area > 1) w.Text("CHU", cx, cy - TH * 2.0, TH * 0.9, $"S={area.ToString("0.0", Ci)} m²", 1, 2, 0);
            // kích thước thông thuỷ của phòng khi đủ chỗ
            double x0 = r.Boundary.Min(p => p.X), x1 = r.Boundary.Max(p => p.X), y0 = r.Boundary.Min(p => p.Y), y1 = r.Boundary.Max(p => p.Y);
            if (r.Boundary.Count == 4 && x1 - x0 > TH * 8 && y1 - y0 > TH * 8 && area > 1)
            {
                w.DimH(x0, x1, y0 + TH * 2.0, TH);
                w.DimV(x0 + TH * 2.0, y0, y1, TH);
            }
        }

        /// <summary>Chuỗi trong cùng: mảng tường và cửa trên mặt ngoài, ghi ở cả 4 phía.</summary>
        void ExteriorChains(string lvl, Rect e, PlanAnnotation ann)
        {
            var walls = WallsAt(lvl).Where(x => ArchAnalysis.IsOrtho(x.Line)).ToList();
            foreach (var (dx, dy) in new[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
            {
                bool horizontal = dx == 0;
                var face = walls.Where(x => (horizontal ? x.Line.IsHorizontal(0.5) : x.Line.IsVertical(0.5)) && ArchAnalysis.FaceVisible(a, x, dx, dy)).ToList();
                var pts = new List<double> { horizontal ? e.X0 : e.Y0, horizontal ? e.X1 : e.Y1 };
                foreach (var x in face)
                {
                    var q = ArchAnalysis.WallRect(x);
                    pts.Add(horizontal ? q.X0 : q.Y0); pts.Add(horizontal ? q.X1 : q.Y1);
                    foreach (var o in a.Openings.Where(o => o.HostWallId == x.Id))
                    {
                        double u = horizontal ? o.Location.X : o.Location.Y;
                        pts.Add(u - o.Width / 2); pts.Add(u + o.Width / 2);
                    }
                }
                var uniq = GeomUtil.Cluster(pts, 5);
                if (horizontal) ann.ChainH(uniq, dy < 0 ? e.Y0 - ann.ChainOffset(0) : e.Y1 + ann.ChainOffset(0), 60);
                else ann.ChainV(uniq, dx < 0 ? e.X0 - ann.ChainOffset(0) : e.X1 + ann.ChainOffset(0), 60);
            }
        }

        // ================================================================== THANG
        public class StairCalc
        {
            public double H, Riser, Tread, Landing, Length, Width;
            public int Risers, PerFlight;
            public bool Dogleg;
            public double X0, Y0, X1, Y1;
            public bool LongX;
        }

        public StairCalc CalcStair(ArchStair st, List<string> warn = null)
        {
            double x0 = st.Boundary.Min(p => p.X), x1 = st.Boundary.Max(p => p.X), y0 = st.Boundary.Min(p => p.Y), y1 = st.Boundary.Max(p => p.Y);
            var c = new StairCalc { X0 = x0, X1 = x1, Y0 = y0, Y1 = y1, LongX = x1 - x0 >= y1 - y0 };
            c.Length = Math.Max(x1 - x0, y1 - y0); c.Width = Math.Min(x1 - x0, y1 - y0);
            int i = levels.FindIndex(l => l.Name == st.BaseLevel);
            c.H = i >= 0 && i + 1 < levels.Count ? levels[i + 1].Elevation - levels[i].Elevation : 3300;
            c.Dogleg = c.Width >= 1800;
            int n = (int)Math.Round(c.H / 170);
            if (c.Dogleg && n % 2 == 1) n++;
            c.Risers = Math.Max(n, 2);
            c.Riser = c.H / c.Risers;
            if (c.Dogleg)
            {
                c.PerFlight = c.Risers / 2;
                c.Landing = Math.Max(1000, c.Width / 2);
                int treads = c.PerFlight - 1;
                c.Tread = treads > 0 ? (c.Length - c.Landing) / treads : 260;
            }
            else
            {
                c.PerFlight = c.Risers;
                c.Landing = 0;
                c.Tread = (c.Length) / Math.Max(1, c.Risers - 1);
            }
            if (c.Tread > 300)
            {
                // ô thang dài hơn cần: giữ bậc 280, phần dư vào chiếu nghỉ
                double t = 280;
                if (c.Dogleg) c.Landing = c.Length - t * (c.PerFlight - 1);
                c.Tread = t;
            }
            double blondel = 2 * c.Riser + c.Tread;
            if (warn != null && (c.Tread < 240 || blondel < 550 || blondel > 650))
                warn.Add($"Thang tầng {st.BaseLevel}: {c.Risers} bậc cao {c.Riser:0}, rộng {c.Tread:0} (2h+b = {blondel:0} mm, nên trong 550–650) — ô thang {c.Length:0}×{c.Width:0} chưa phù hợp, cần xem lại.");
            return c;
        }

        void DrawStairPlan(ArchStair st, bool numbers)
        {
            var c = CalcStair(st);
            Func<double, double, P2> P = (along, across) => c.LongX ? new P2(c.X0 + along, c.Y0 + across) : new P2(c.X0 + across, c.Y0 + along);
            Action<double, double, double, double, string> L = (a0, b0, a1, b1, lay) => { var p = P(a0, b0); var q = P(a1, b1); w.Line(lay, p.X, p.Y, q.X, q.Y); };
            L(0, 0, c.Length, 0, "THANG"); L(c.Length, 0, c.Length, c.Width, "THANG"); L(c.Length, c.Width, 0, c.Width, "THANG"); L(0, c.Width, 0, 0, "THANG");
            int treads = c.Dogleg ? c.PerFlight - 1 : c.Risers - 1;
            double runLen = treads * c.Tread;
            if (c.Dogleg)
            {
                double mid = c.Width / 2, g = 50;
                L(0, mid - g, runLen, mid - g, "THANG"); L(0, mid + g, runLen, mid + g, "THANG"); L(runLen, mid - g, runLen, mid + g, "THANG");
                for (int i = 0; i <= treads; i++)
                {
                    double u = i * c.Tread;
                    L(u, 0, u, mid - g, "THANG");
                    L(u, mid + g, u, c.Width, "THANG");
                }
                // mũi tên "LÊN" ở vế đầu
                var a0 = P(0, mid / 2); var a1 = P(runLen * 0.85, mid / 2);
                w.Line("KY_HIEU", a0.X, a0.Y, a1.X, a1.Y);
                w.Dot("KY_HIEU", a0.X, a0.Y, TH * 0.2);
                w.Text("CHU", a0.X, a0.Y, TH * 0.9, "LÊN", 2, 1, 0);
                if (numbers)
                {
                    for (int i = 0; i < treads; i++)
                    {
                        var p1 = P((i + 0.5) * c.Tread, mid * 0.28);
                        w.Text("CHU", p1.X, p1.Y, TH * 0.8, (i + 1).ToString(), 1, 2, 0);
                        var p2 = P((treads - 1 - i + 0.5) * c.Tread, mid + g + mid * 0.72);
                        w.Text("CHU", p2.X, p2.Y, TH * 0.8, (c.PerFlight + i + 1).ToString(), 1, 2, 0);
                    }
                }
            }
            else
            {
                for (int i = 0; i <= treads; i++) L(i * c.Tread, 0, i * c.Tread, c.Width, "THANG");
                var a0 = P(0, c.Width / 2); var a1 = P(runLen * 0.85, c.Width / 2);
                w.Line("KY_HIEU", a0.X, a0.Y, a1.X, a1.Y);
                w.Text("CHU", a0.X, a0.Y, TH * 0.9, "LÊN", 2, 1, 0);
                if (numbers)
                    for (int i = 0; i < treads; i++)
                    {
                        var p = P((i + 0.5) * c.Tread, c.Width * 0.25);
                        w.Text("CHU", p.X, p.Y, TH * 0.8, (i + 1).ToString(), 1, 2, 0);
                    }
            }
        }

        /// <summary>Chi tiết thang: mặt bằng đánh số bậc + mặt cắt zích-zắc.</summary>
        public void DrawStairDetail(ArchStair st, double scale)
        {
            s = scale; w.SetScaleX(s);
            var c = CalcStair(st, Warnings);
            var ann = new PlanAnnotation(w, TH);
            DrawStairPlan(st, true);
            ann.ChainH(new[] { c.X0, c.X1 }, c.Y0 - ann.ChainOffset(0));
            ann.ChainV(new[] { c.Y0, c.Y1 }, c.X0 - ann.ChainOffset(0));
            ann.Title((c.X0 + c.X1) / 2, c.Y0 - ann.ChainOffset(1) - TH * 2, $"MẶT BẰNG THANG {st.BaseLevel.ToUpperInvariant()}", s);

            // mặt cắt đặt bên phải mặt bằng
            double ox = c.X1 + TH * 18, oy = c.Y0;
            double waist = 120;
            int treads = c.Dogleg ? c.PerFlight - 1 : c.Risers - 1;
            var pts = new List<(double x, double y)>();
            double u = 0, z = 0;
            pts.Add((ox, oy));
            for (int i = 0; i < c.PerFlight; i++)
            {
                z += c.Riser; pts.Add((ox + u, oy + z));
                if (i < c.PerFlight - 1) { u += c.Tread; pts.Add((ox + u, oy + z)); }
            }
            double uEnd = u;
            if (c.Dogleg) { pts.Add((ox + uEnd + c.Landing, oy + z)); }
            w.Poly("BE_TONG", pts);
            // bản thang song song đường gờ bậc
            double slope = Math.Atan2(c.Riser, c.Tread), off = waist / Math.Cos(slope);
            w.Line("BE_TONG", ox, oy - off, ox + uEnd, oy + z - off);
            if (c.Dogleg)
            {
                w.Line("BE_TONG", ox + uEnd, oy + z - off, ox + uEnd + c.Landing, oy + z - off);
                w.Line("BE_TONG", ox + uEnd + c.Landing, oy + z, ox + uEnd + c.Landing, oy + z - off);
                // vế hai đi ngược lại, nét mảnh vì nằm phía sau đường cắt
                double u2 = uEnd, z2 = z;
                var back = new List<(double x, double y)> { (ox + uEnd, oy + z) };
                for (int i = 0; i < c.PerFlight; i++)
                {
                    z2 += c.Riser; back.Add((ox + u2, oy + z2));
                    if (i < c.PerFlight - 1) { u2 -= c.Tread; back.Add((ox + u2, oy + z2)); }
                }
                w.Poly("THANG", back);
            }
            w.Line("BE_TONG", ox - TH * 2, oy, ox + uEnd + c.Landing + TH * 2, oy);
            ann.Level(ox + uEnd + c.Landing + TH * 2.5, oy, levels.First(l => l.Name == st.BaseLevel).Elevation);
            ann.Level(ox + uEnd + c.Landing + TH * 2.5, oy + c.H, levels.First(l => l.Name == st.BaseLevel).Elevation + c.H);
            if (c.Dogleg) ann.Level(ox + uEnd + c.Landing + TH * 2.5, oy + z, levels.First(l => l.Name == st.BaseLevel).Elevation + z, "chiếu nghỉ");
            w.DimV(ox - TH * 3, oy, oy + z, TH, $"{c.PerFlight}x{c.Riser.ToString("0", Ci)}={z.ToString("0", Ci)}");
            if (c.Dogleg) w.DimV(ox - TH * 3, oy + z, oy + c.H, TH, $"{c.PerFlight}x{c.Riser.ToString("0", Ci)}={(c.H - z).ToString("0", Ci)}");
            w.DimH(ox, ox + uEnd, oy - TH * 4, TH, $"{treads}x{c.Tread.ToString("0", Ci)}={uEnd.ToString("0", Ci)}");
            if (c.Dogleg) w.DimH(ox + uEnd, ox + uEnd + c.Landing, oy - TH * 4, TH);
            ann.Title(ox + (uEnd + c.Landing) / 2, oy - TH * 8, "MẶT CẮT THANG", s);
            w.Text("CHU", ox + (uEnd + c.Landing) / 2, oy - TH * 12.5, TH, $"{c.Risers} bậc, cao {c.Riser.ToString("0", Ci)} mm, rộng {c.Tread.ToString("0", Ci)} mm", 1, 0, 0);
        }

        // ================================================================== MẶT ĐỨNG
        public static readonly string[] SideNames = { "TRƯỚC", "SAU", "TRÁI", "PHẢI" };

        /// <param name="side">0: nhìn từ phía Y nhỏ; 1: Y lớn; 2: X nhỏ; 3: X lớn.</param>
        public void DrawElevation(int side, double scale)
        {
            s = scale; w.SetScaleX(s);
            var ann = new PlanAnnotation(w, TH);
            int dx = side == 2 ? -1 : side == 3 ? 1 : 0, dy = side == 0 ? -1 : side == 1 ? 1 : 0;
            Func<P2, double> U = p => side == 0 ? p.X : side == 1 ? -p.X : side == 2 ? -p.Y : p.Y;
            double uMin = double.MaxValue, uMax = double.MinValue;
            var withWalls = levels.Where(l => WallsAt(l.Name).Any()).ToList();
            if (withWalls.Count == 0) return;
            double zGround = levels.First().Elevation;
            double zTop = levels.Last().Elevation;

            foreach (var lv in withWalls)
            {
                int li = levels.IndexOf(lv);
                double z0 = lv.Elevation, z1 = li + 1 < levels.Count ? levels[li + 1].Elevation : z0 + 3000;
                bool horizontal = dx == 0;
                foreach (var x in WallsAt(lv.Name).Where(x => (horizontal ? x.Line.IsHorizontal(0.5) : x.Line.IsVertical(0.5)) && ArchAnalysis.FaceVisible(a, x, dx, dy)))
                {
                    var q = ArchAnalysis.WallRect(x);
                    double a0 = horizontal ? q.X0 : q.Y0, a1 = horizontal ? q.X1 : q.Y1;
                    double u0 = Math.Min(UOf(side, a0), UOf(side, a1)), u1 = Math.Max(UOf(side, a0), UOf(side, a1));
                    uMin = Math.Min(uMin, u0); uMax = Math.Max(uMax, u1);
                    w.Rect("TUONG", u0, z0, u1, z1);
                    foreach (var o in a.Openings.Where(o => o.HostWallId == x.Id))
                    {
                        double uc = U(o.Location);
                        DrawOpeningFront(o, uc, z0);
                    }
                }
            }
            if (uMin > uMax) return;
            // sàn các tầng, tường chắn mái, mặt đất
            foreach (var lv in levels) w.Line("TUONG", uMin, lv.Elevation, uMax, lv.Elevation);
            bool hasRoof = levels.Last().IsRoof || !WallsAt(levels.Last().Name).Any();
            if (hasRoof && opt.ParapetHeight > 0)
            {
                w.Rect("TUONG", uMin, zTop, uMax, zTop + opt.ParapetHeight);
                ann.Level(uMax + TH * 2, zTop + opt.ParapetHeight, zTop + opt.ParapetHeight, "đỉnh mái");
            }
            w.Line("BE_TONG", uMin - TH * 6, zGround, uMax + TH * 6, zGround);
            foreach (var lv in levels) ann.Level(uMax + TH * 2, lv.Elevation, lv.Elevation, lv.Name);
            ann.ChainV(levels.Select(l => l.Elevation), uMin - ann.ChainOffset(0));
            if (levels.Count > 2) ann.ChainV(new[] { levels.First().Elevation, levels.Last().Elevation }, uMin - ann.ChainOffset(1));

            // trục vuông góc phía dưới
            var grids = (dx == 0 ? a.Grids.Where(g => g.Line.IsVertical(2)) : a.Grids.Where(g => g.Line.IsHorizontal(2)))
                .Select(g => (g.Name, u: U(g.Line.Mid))).OrderBy(g => g.u).ToList();
            double yb = zGround - TH * 3;
            ann.ChainH(grids.Select(g => g.u), yb);
            foreach (var g in grids)
            {
                w.Line("TRUC", g.u, zGround, g.u, yb - TH * 1.5);
                ann.Bubble(g.u, yb - TH * 3, g.Name, TH * 1.5);
            }
            string nm = grids.Count >= 2 ? $"TRỤC {grids.First().Name}-{grids.Last().Name}" : SideNames[side];
            ann.Title((uMin + uMax) / 2, yb - TH * 7, $"MẶT ĐỨNG {SideNames[side]} ({nm})", s);
        }

        static double UOf(int side, double v) => side == 1 || side == 2 ? -v : v;

        void DrawOpeningFront(ArchOpening o, double uc, double zFloor)
        {
            double u0 = uc - o.Width / 2, u1 = uc + o.Width / 2, z0 = zFloor + o.SillHeight, z1 = z0 + o.Height;
            w.Rect("CUA", u0, z0, u1, z1);
            if (ArchAnalysis.IsDoor(o))
            {
                w.Rect("CUA", u0 + 50, z0, u1 - 50, z1 - 50);
                if (o.Width >= 1500) w.Line("CUA", uc, z0, uc, z1 - 50);
            }
            else
            {
                w.Rect("CUA", u0 + 40, z0 + 40, u1 - 40, z1 - 40);
                w.Line("CUA", uc, z0 + 40, uc, z1 - 40);
            }
            if (!string.IsNullOrEmpty(o.Mark)) w.Text("CHU", uc, z1 + TH * 0.4, TH * 0.9, o.Mark, 1, 0, 0);
        }

        // ================================================================== MẶT CẮT
        /// <param name="alongY">true: đường cắt song song trục Y tại x = pos (mặt cắt A-A); false: song song trục X tại y = pos.</param>
        public void DrawSection(bool alongY, double pos, string name, double scale)
        {
            s = scale; w.SetScaleX(s);
            var ann = new PlanAnnotation(w, TH);
            Func<P2, double> U = p => alongY ? p.Y : p.X;
            Func<P2, double> V = p => alongY ? p.X : p.Y;
            double uMin = double.MaxValue, uMax = double.MinValue;
            double zGround = levels.First().Elevation;
            double zTop = levels.Last().Elevation;
            bool hasRoof = levels.Last().IsRoof || !WallsAt(levels.Last().Name).Any();

            for (int li = 0; li < levels.Count; li++)
            {
                var lv = levels[li];
                double z0 = lv.Elevation;
                double z1 = li + 1 < levels.Count ? levels[li + 1].Elevation : z0;
                var walls = WallsAt(lv.Name).Where(x => ArchAnalysis.IsOrtho(x.Line)).ToList();
                if (walls.Count == 0 || z1 <= z0) continue;
                double tSlab = SlabThickness(li + 1);

                var cut = walls.Where(x => (alongY ? x.Line.IsHorizontal(0.5) : x.Line.IsVertical(0.5)))
                               .Where(x => { var q = ArchAnalysis.WallRect(x); double a0 = alongY ? q.X0 : q.Y0, a1 = alongY ? q.X1 : q.Y1; return pos >= a0 && pos <= a1; }).ToList();
                var beyond = walls.Where(x => (alongY ? x.Line.IsVertical(0.5) : x.Line.IsHorizontal(0.5)) && V(x.Line.Mid) > pos).OrderBy(x => V(x.Line.Mid)).ToList();

                // tường phía sau đường cắt: chỉ đoạn nhìn thấy đầu tiên
                var seen = new List<(double a, double b)>();
                foreach (var x in beyond)
                {
                    var q = ArchAnalysis.WallRect(x);
                    double a0 = alongY ? q.Y0 : q.X0, a1 = alongY ? q.Y1 : q.X1;
                    if (seen.Any(r => a0 >= r.a - 1 && a1 <= r.b + 1)) continue;
                    seen.Add((a0, a1));
                    foreach (var o in a.Openings.Where(o => o.HostWallId == x.Id)) DrawOpeningFront(o, U(o.Location), z0);
                }

                double levelMin = double.MaxValue, levelMax = double.MinValue;
                foreach (var x in cut)
                {
                    double t = x.Thickness / 2, uc = U(x.Line.Mid);
                    levelMin = Math.Min(levelMin, uc - t); levelMax = Math.Max(levelMax, uc + t);
                    double along = pos;
                    var o = a.Openings.FirstOrDefault(q => q.HostWallId == x.Id && Math.Abs((alongY ? q.Location.X : q.Location.Y) - along) <= q.Width / 2);
                    double zTopWall = z1 - tSlab;
                    if (o == null) HatchRect(uc - t, z0, uc + t, zTopWall);
                    else
                    {
                        double zs = z0 + o.SillHeight, zh = zs + o.Height;
                        if (zs > z0 + 1) HatchRect(uc - t, z0, uc + t, zs);
                        if (zh < zTopWall - 1) HatchRect(uc - t, zh, uc + t, zTopWall);
                        w.Line("CUA", uc - t / 3, zs, uc - t / 3, zh); w.Line("CUA", uc + t / 3, zs, uc + t / 3, zh);
                        w.Line("CUA", uc - t, zs, uc + t, zs); w.Line("CUA", uc - t, zh, uc + t, zh);
                    }
                    // dầm dưới sàn tại tường bị cắt
                    double hb = BeamDepth(li + 1, uc, alongY);
                    if (hb > 0) ConcreteRect(uc - t, z1 - hb, uc + t, z1);
                }
                if (levelMin > levelMax)
                {
                    levelMin = walls.Min(x => { var q = ArchAnalysis.WallRect(x); return alongY ? q.Y0 : q.X0; });
                    levelMax = walls.Max(x => { var q = ArchAnalysis.WallRect(x); return alongY ? q.Y1 : q.X1; });
                }
                uMin = Math.Min(uMin, levelMin); uMax = Math.Max(uMax, levelMax);
                ConcreteRect(levelMin, z1 - tSlab, levelMax, z1);
            }
            if (uMin > uMax) return;

            if (hasRoof && opt.ParapetHeight > 0)
            {
                HatchRect(uMin, zTop, uMin + 110, zTop + opt.ParapetHeight);
                HatchRect(uMax - 110, zTop, uMax, zTop + opt.ParapetHeight);
                ann.Level(uMax + TH * 2, zTop + opt.ParapetHeight, zTop + opt.ParapetHeight, "đỉnh mái");
            }
            w.Line("BE_TONG", uMin - TH * 6, zGround, uMax + TH * 6, zGround);
            DrawFootingsInSection(alongY, pos, zGround);
            foreach (var lv in levels) ann.Level(uMax + TH * 2, lv.Elevation, lv.Elevation, lv.Name);
            ann.ChainV(levels.Select(l => l.Elevation), uMin - ann.ChainOffset(0));
            if (levels.Count > 2) ann.ChainV(new[] { levels.First().Elevation, levels.Last().Elevation }, uMin - ann.ChainOffset(1));

            var grids = (alongY ? a.Grids.Where(g => g.Line.IsHorizontal(2)) : a.Grids.Where(g => g.Line.IsVertical(2)))
                .Select(g => (g.Name, u: U(g.Line.Mid))).OrderBy(g => g.u).ToList();
            double depth = m?.Footings.Count > 0 ? Math.Max(m.Footings.Min(f => f.BottomZ), zGround - 2500) : zGround;
            double yb = depth - TH * 3;
            ann.ChainH(grids.Select(g => g.u), yb);
            foreach (var g in grids)
            {
                w.Line("TRUC", g.u, zTop, g.u, yb - TH * 1.5);
                ann.Bubble(g.u, yb - TH * 3, g.Name, TH * 1.5);
            }
            ann.Title((uMin + uMax) / 2, yb - TH * 7, $"MẶT CẮT {name}", s);
        }

        void DrawFootingsInSection(bool alongY, double pos, double zGround)
        {
            if (m == null || m.Footings.Count == 0) return;
            // móng ở hàng trục gần đường cắt nhất, nhìn thấy phía sau
            double Vf(FootingElem f) => alongY ? f.Center.X : f.Center.Y;
            double best = m.Footings.Min(f => Math.Abs(Vf(f) - pos));
            foreach (var f in m.Footings.Where(f => Math.Abs(Math.Abs(Vf(f) - pos) - best) < 50 && Vf(f) >= pos - 1))
            {
                double uc = alongY ? f.Center.Y : f.Center.X;
                double half = (alongY ? f.B : f.L) / 2;
                w.Rect("BE_TONG", uc - half, f.BottomZ, uc + half, f.BottomZ + f.H);
                var col = m.Columns.FirstOrDefault(c => c.Id == f.ColumnId);
                var sec = col != null ? m.Section(col.Section) : null;
                double hc = sec != null ? (alongY ? sec.H : sec.B) / 2 : 110;
                w.Line("BE_TONG", uc - hc, f.BottomZ + f.H, uc - hc, zGround);
                w.Line("BE_TONG", uc + hc, f.BottomZ + f.H, uc + hc, zGround);
                w.Text("CHU", uc, f.BottomZ - TH * 1.2, TH * 0.9, f.Label ?? "", 1, 2, 0);
            }
        }

        double SlabThickness(int levelIdx)
        {
            if (m != null && levelIdx < levels.Count)
            {
                var slab = m.Slabs.FirstOrDefault(q => q.Story == levels[levelIdx].Name);
                var sec = slab != null ? m.SlabSection(slab.Section) : null;
                if (sec != null) return sec.Thickness;
            }
            return 100;
        }

        double BeamDepth(int levelIdx, double u, bool alongY)
        {
            if (m == null || levelIdx >= levels.Count) return 0;
            var beam = m.Beams.Where(b => b.Story == levels[levelIdx].Name)
                .FirstOrDefault(b => alongY ? Math.Abs(b.Start.Y - u) < 200 && Math.Abs(b.End.Y - u) < 200 : Math.Abs(b.Start.X - u) < 200 && Math.Abs(b.End.X - u) < 200);
            return beam != null ? m.Section(beam.Section).H : 0;
        }

        void HatchRect(double u0, double z0, double u1, double z1)
        {
            if (z1 <= z0 || u1 <= u0) return;
            var p = new[] { (u0, z0), (u1, z0), (u1, z1), (u0, z1) };
            w.HatchX("HATCH", p, "ANSI31", 1.5 * s);
            w.Poly("TUONG", p, true);
        }

        void ConcreteRect(double u0, double z0, double u1, double z1)
        {
            if (z1 <= z0 || u1 <= u0) return;
            var p = new[] { (u0, z0), (u1, z0), (u1, z1), (u0, z1) };
            w.HatchX("HATCH", p, "SOLID", 1.5 * s);
            w.Poly("BE_TONG", p, true);
        }

        // ================================================================== THỐNG KÊ CỬA
        public void DrawOpeningSchedule(double scale)
        {
            s = scale; w.SetScaleX(s);
            var opens = a.Openings.Where(o => !string.IsNullOrEmpty(o.Mark)).ToList();
            if (opens.Count == 0) return;
            var marks = opens.GroupBy(o => o.Mark).OrderBy(g => g.Key[0]).ThenBy(g => int.TryParse(g.Key.Substring(1), out var v) ? v : 0).ToList();
            var lvlNames = levels.Where(l => opens.Any(o => o.Level == l.Name)).Select(l => l.Name).ToList();

            // Hình mặt đứng từng loại cửa phía trên bảng
            double x = 0, topY = 0, maxH = 0;
            foreach (var g in marks)
            {
                var o = g.First();
                double z0 = 0, z1 = o.Height;
                DrawOpeningFront(new ArchOpening { Kind = o.Kind, Width = o.Width, Height = o.Height, SillHeight = 0 }, x + o.Width / 2, 0);
                w.Line("BE_TONG", x - 100, 0, x + o.Width + 100, 0);
                w.DimH(x, x + o.Width, -TH * 2, TH);
                w.DimV(x - TH * 2, z0, z1, TH);
                w.TextUnderlined("CHU", x + o.Width / 2, -TH * 5, TH * 1.2, g.Key);
                maxH = Math.Max(maxH, z1);
                x += o.Width + TH * 10;
            }
            topY = -TH * 8;
            double u = TH / 125.0 * 2; // chiều rộng cột theo chiều cao chữ
            var cw = new List<double> { 600 * u, 1100 * u, 500 * u, 500 * u, 500 * u };
            cw.AddRange(lvlNames.Select(_ => 650.0 * u));
            cw.Add(600 * u);
            var rows = new List<string[]>();
            var head = new List<string> { "KÝ HIỆU", "LOẠI", "RỘNG", "CAO", "BỆ" };
            head.AddRange(lvlNames.Select(n => n.ToUpperInvariant()));
            head.Add("TỔNG");
            rows.Add(head.ToArray());
            foreach (var g in marks)
            {
                var o = g.First();
                var r = new List<string> { g.Key, o.Kind ?? "", o.Width.ToString("0", Ci), o.Height.ToString("0", Ci), o.SillHeight.ToString("0", Ci) };
                r.AddRange(lvlNames.Select(n => { int c = g.Count(q => q.Level == n); return c > 0 ? c.ToString() : ""; }));
                r.Add(g.Count().ToString());
                rows.Add(r.ToArray());
            }
            w.Text("CHU", cw.Sum() / 2, topY - TH * 0.5, TH * 1.4, "BẢNG THỐNG KÊ CỬA", 1, 0, 0);
            SimpleTable.Draw(w, 0, topY - TH * 1.5, TH * 0.9, cw.ToArray(), TH * 2.4, rows);
        }

        // ================================================================== CHI TIẾT VỆ SINH
        /// <summary>Phòng vệ sinh phóng to: lưới gạch, thiết bị gợi ý, phễu thu, kích thước.</summary>
        public void DrawWetRoom(ArchRoom r, double scale)
        {
            s = scale; w.SetScaleX(s);
            var ann = new PlanAnnotation(w, TH);
            double x0 = r.Boundary.Min(p => p.X), x1 = r.Boundary.Max(p => p.X), y0 = r.Boundary.Min(p => p.Y), y1 = r.Boundary.Max(p => p.Y);
            double W = x1 - x0, H = y1 - y0;

            // Tường bao quanh (cắt đúng phần tường trong khung nhìn)
            double margin = 300;
            var clip = new Rect(x0 - margin, y0 - margin, x1 + margin, y1 + margin);
            var solids = new List<Rect>();
            var holes = new List<Rect>();
            var walls = WallsAt(r.Level).Where(x => ArchAnalysis.IsOrtho(x.Line)).ToList();
            foreach (var x in walls)
            {
                var q = ArchAnalysis.WallRect(x);
                var c = Clip(q, clip);
                if (c.HasValue) solids.Add(c.Value);
            }
            var doors = new List<(ArchOpening o, ArchWall host)>();
            foreach (var o in a.Openings.Where(o => o.Level == r.Level && o.HostWallId != null))
            {
                var host = walls.FirstOrDefault(x => x.Id == o.HostWallId);
                if (host == null) continue;
                var q = ArchAnalysis.WallRect(host);
                var d = host.Line.Dir; var n = new P2(-d.Y, d.X);
                var c = Project(host, o.Location);
                var p0 = c - d * (o.Width / 2) - n * (host.Thickness / 2 + 2);
                var p1 = c + d * (o.Width / 2) + n * (host.Thickness / 2 + 2);
                var hole = Clip(new Rect(p0.X, p0.Y, p1.X, p1.Y), clip);
                if (hole == null) continue;
                holes.Add(hole.Value);
                if (c.X >= x0 - host.Thickness && c.X <= x1 + host.Thickness && c.Y >= y0 - host.Thickness && c.Y <= y1 + host.Thickness)
                    doors.Add((o, host));
            }
            var mask = new WallMask(solids, holes);
            foreach (var q in mask.FillRects()) w.HatchX("HATCH", new[] { (q.X0, q.Y0), (q.X1, q.Y0), (q.X1, q.Y1), (q.X0, q.Y1) }, "ANSI31", 1.5 * s);
            foreach (var q in mask.Edges()) w.Line("TUONG", q.A.X, q.A.Y, q.B.X, q.B.Y);
            var center = new P2((x0 + x1) / 2, (y0 + y1) / 2);
            foreach (var (o, host) in doors) DrawOpening(o, host, center);

            // Lưới gạch 300×300 từ góc trong
            const double tile = 300;
            for (double x = x0 + tile; x < x1 - 1; x += tile) w.Line("LATNEN", x, y0, x, y1);
            for (double y = y0 + tile; y < y1 - 1; y += tile) w.Line("LATNEN", x0, y, x1, y);

            // Thiết bị đặt ở tường xa cửa nhất
            var door = doors.FirstOrDefault(q => ArchAnalysis.IsDoor(q.o));
            bool doorOnX = door.o != null && door.host.Line.IsVertical(0.5);
            bool farAtHighY = door.o == null || door.o.Location.Y < center.Y;
            if (door.o != null && doorOnX) farAtHighY = true;
            double wy = farAtHighY ? y1 : y0;
            double dir = farAtHighY ? -1 : 1;
            double toiletX = x0 + Math.Min(450, W / 2);
            DrawToilet(toiletX, wy, dir);
            if (W >= 1500) DrawBasin(x1 - 350, wy, dir);
            double drainX = (x0 + x1) / 2, drainY = (y0 + y1) / 2 - dir * Math.Min(300, H / 4);
            w.Circle("THIETBI", drainX, drainY, 60);
            w.Line("THIETBI", drainX - 60, drainY, drainX + 60, drainY);
            w.Line("THIETBI", drainX, drainY - 60, drainX, drainY + 60);
            w.Text("CHU", drainX + TH * 0.8, drainY, TH * 0.9, "phễu thu", 0, 2, 0);

            ann.ChainH(new[] { x0, x1 }, y0 - ann.ChainOffset(0) - margin);
            ann.ChainV(new[] { y0, y1 }, x0 - ann.ChainOffset(0) - margin);
            ann.Title((x0 + x1) / 2, y0 - ann.ChainOffset(1) - margin - TH * 2, $"CHI TIẾT {r.Name.ToUpperInvariant()} – {r.Level.ToUpperInvariant()}", s);
            double ny = y0 - ann.ChainOffset(1) - margin - TH * 7;
            w.Text("CHU", (x0 + x1) / 2, ny, TH * 0.9, "Lát gạch chống trơn 300x300, dốc 1% về phễu thu", 1, 0, 0);
            w.Text("CHU", (x0 + x1) / 2, ny - TH * 1.6, TH * 0.9, "Vị trí thiết bị là gợi ý — KTS kiểm tra lại", 1, 0, 0);
        }

        static Rect? Clip(Rect q, Rect c)
        {
            double x0 = Math.Max(q.X0, c.X0), y0 = Math.Max(q.Y0, c.Y0), x1 = Math.Min(q.X1, c.X1), y1 = Math.Min(q.Y1, c.Y1);
            if (x1 - x0 < 1 || y1 - y0 < 1) return null;
            return new Rect(x0, y0, x1, y1);
        }

        void DrawToilet(double cx, double wy, double dir)
        {
            double tankW = 420, tankD = 180, bowlR = 190;
            w.Rect("THIETBI", cx - tankW / 2, wy, cx + tankW / 2, wy + dir * tankD);
            w.Circle("THIETBI", cx, wy + dir * (tankD + bowlR + 60), bowlR);
            w.Line("THIETBI", cx - 120, wy + dir * tankD, cx - bowlR, wy + dir * (tankD + bowlR + 60));
            w.Line("THIETBI", cx + 120, wy + dir * tankD, cx + bowlR, wy + dir * (tankD + bowlR + 60));
        }

        void DrawBasin(double cx, double wy, double dir)
        {
            double bw = 500, bd = 420;
            w.Rect("THIETBI", cx - bw / 2, wy, cx + bw / 2, wy + dir * bd);
            w.Circle("THIETBI", cx, wy + dir * bd * 0.55, 150);
            w.Circle("THIETBI", cx, wy + dir * bd * 0.55, 20);
        }
    }
}
