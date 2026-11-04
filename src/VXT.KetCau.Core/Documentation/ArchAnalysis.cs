using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VXT.KetCau.Core.Drawing;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;

namespace VXT.KetCau.Core.Documentation
{
    /// <summary>Suy các thông tin hồ sơ cần từ dữ liệu kiến trúc: cửa thuộc tường nào, đường bao phòng, ký hiệu cửa, tường ngoài.</summary>
    public static class ArchAnalysis
    {
        public static List<ArchLevel> OrderedLevels(ArchModel a) => a.Levels.OrderBy(l => l.Elevation).ToList();

        /// <summary>Chạy trước khi lập hồ sơ và trước khi sinh kết cấu. Gọi lại nhiều lần vẫn cho cùng kết quả.</summary>
        public static List<string> Prepare(ArchModel a)
        {
            var log = new List<string>();
            int wn = 0;
            foreach (var wl in a.Walls.Where(x => string.IsNullOrEmpty(x.Id))) wl.Id = "W" + (++wn) + "-" + Guid.NewGuid().ToString("N").Substring(0, 4);
            RehostOpenings(a, log);
            ResolveRooms(a, log);
            AssignOpeningMarks(a);
            var levels = OrderedLevels(a);
            foreach (var wl in a.Walls)
            {
                wl.IsExterior = IsExterior(a, wl);
                int i = levels.FindIndex(l => l.Name == wl.BaseLevel);
                if (i < 0) continue;
                wl.BaseElevation = levels[i].Elevation;
                if (i + 1 < levels.Count) { wl.TopLevel = levels[i + 1].Name; wl.Height = levels[i + 1].Elevation - levels[i].Elevation; }
            }
            return log;
        }

        static void RehostOpenings(ArchModel a, List<string> log)
        {
            foreach (var o in a.Openings)
            {
                var host = a.Walls.FirstOrDefault(x => x.Id == o.HostWallId);
                if (host != null)
                {
                    if (string.IsNullOrEmpty(o.Level)) o.Level = host.BaseLevel;
                    if (host.BaseLevel == o.Level && host.Line.DistanceTo(o.Location) <= host.Thickness) continue;
                }
                var near = a.Walls.Where(x => string.IsNullOrEmpty(o.Level) || x.BaseLevel == o.Level)
                                  .OrderBy(x => x.Line.DistanceTo(o.Location)).FirstOrDefault();
                if (near == null || near.Line.DistanceTo(o.Location) > Math.Max(near.Thickness, 300))
                {
                    log.Add($"Cửa {o.Kind} rộng {o.Width:0} tại {o.Location} không nằm trên tường nào — sẽ không vẽ");
                    o.HostWallId = null;
                    continue;
                }
                o.HostWallId = near.Id;
                o.Level = near.BaseLevel;
            }
        }

        /// <summary>Tên phòng đọc từ DWG chỉ là một điểm: bắn tia tới mặt tường bốn phía để lấy hình chữ nhật của phòng.</summary>
        static void ResolveRooms(ArchModel a, List<string> log)
        {
            foreach (var r in a.Rooms)
            {
                if (r.Boundary.Count < 3) continue;
                if (Math.Abs(GeomUtil.Area(r.Boundary)) > 1e6) continue; // đã có đường bao thật
                var c = new P2(r.Boundary.Average(p => p.X), r.Boundary.Average(p => p.Y));
                var rects = a.Walls.Where(x => x.BaseLevel == r.Level).Select(WallRect).ToList();
                double? x0 = Ray(rects, c, -1, 0), x1 = Ray(rects, c, 1, 0), y0 = Ray(rects, c, 0, -1), y1 = Ray(rects, c, 0, 1);
                if (x0 == null || x1 == null || y0 == null || y1 == null)
                {
                    log.Add($"Phòng \"{r.Name}\" ({r.Level}) không khép kín bằng tường — chưa tính được diện tích");
                    continue;
                }
                r.Boundary = new List<P2> { new P2(c.X - x0.Value, c.Y - y0.Value), new P2(c.X + x1.Value, c.Y - y0.Value), new P2(c.X + x1.Value, c.Y + y1.Value), new P2(c.X - x0.Value, c.Y + y1.Value) };
            }
        }

        static double? Ray(List<Rect> rects, P2 c, int dx, int dy)
        {
            double? best = null;
            foreach (var q in rects)
            {
                double d;
                if (dx != 0)
                {
                    if (c.Y < q.Y0 || c.Y > q.Y1) continue;
                    d = dx > 0 ? q.X0 - c.X : c.X - q.X1;
                }
                else
                {
                    if (c.X < q.X0 || c.X > q.X1) continue;
                    d = dy > 0 ? q.Y0 - c.Y : c.Y - q.Y1;
                }
                if (d > 1 && (best == null || d < best)) best = d;
            }
            return best;
        }

        /// <summary>Cùng loại, cùng kích thước thì cùng ký hiệu: D1, D2… cho cửa đi; S1, S2… cho cửa sổ.</summary>
        static void AssignOpeningMarks(ArchModel a)
        {
            foreach (var kind in new[] { true, false })
            {
                int n = 0;
                var list = a.Openings.Where(o => IsDoor(o) == kind && o.HostWallId != null)
                    .GroupBy(o => (R(o.Width), R(o.Height), R(o.SillHeight)))
                    .OrderByDescending(g => g.Key.Item1).ThenByDescending(g => g.Key.Item2);
                foreach (var g in list)
                {
                    string mark = (kind ? "D" : "S") + (++n);
                    foreach (var o in g) o.Mark = mark;
                }
            }
        }

        static double R(double v) => Math.Round(v / 10) * 10;
        public static bool IsDoor(ArchOpening o) => (o.Kind ?? "").IndexOf("đi", StringComparison.OrdinalIgnoreCase) >= 0 || (o.Kind ?? "").IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0;
        public static bool IsWetRoom(ArchRoom r) => Regex.IsMatch(r.Name ?? "", "(WC|VỆ SINH|VS\\b|TẮM)", RegexOptions.IgnoreCase);

        public struct Rect
        {
            public double X0, Y0, X1, Y1;
            public Rect(double x0, double y0, double x1, double y1) { X0 = Math.Min(x0, x1); Y0 = Math.Min(y0, y1); X1 = Math.Max(x0, x1); Y1 = Math.Max(y0, y1); }
            public double W => X1 - X0;
            public double H => Y1 - Y0;
        }

        public static bool IsOrtho(Seg2 s) => s.IsHorizontal(0.5) || s.IsVertical(0.5);

        /// <summary>Khung chữ nhật của tường trực giao (với tường xiên chỉ là khung bao gần đúng).</summary>
        public static Rect WallRect(ArchWall w)
        {
            double t = w.Thickness / 2;
            if (w.Line.IsHorizontal(0.5)) return new Rect(Math.Min(w.Line.A.X, w.Line.B.X), w.Line.Mid.Y - t, Math.Max(w.Line.A.X, w.Line.B.X), w.Line.Mid.Y + t);
            if (w.Line.IsVertical(0.5)) return new Rect(w.Line.Mid.X - t, Math.Min(w.Line.A.Y, w.Line.B.Y), w.Line.Mid.X + t, Math.Max(w.Line.A.Y, w.Line.B.Y));
            return new Rect(Math.Min(w.Line.A.X, w.Line.B.X) - t, Math.Min(w.Line.A.Y, w.Line.B.Y) - t, Math.Max(w.Line.A.X, w.Line.B.X) + t, Math.Max(w.Line.A.Y, w.Line.B.Y) + t);
        }

        /// <summary>Từ giữa tường đi ra ngoài theo hướng (dx, dy) mà không gặp tường cùng tầng nào thì mặt đó nhìn thấy được.</summary>
        public static bool FaceVisible(ArchModel a, ArchWall w, int dx, int dy)
        {
            var q = WallRect(w);
            foreach (var t in new[] { 0.5, 0.15, 0.85 })
            {
                var p = w.Line.A + (w.Line.B - w.Line.A) * t;
                bool blocked = false;
                foreach (var o in a.Walls.Where(x => x != w && x.BaseLevel == w.BaseLevel))
                {
                    var r = WallRect(o);
                    if (dx != 0 && p.Y >= r.Y0 && p.Y <= r.Y1 && (dx > 0 ? r.X0 >= q.X1 - 1 : r.X1 <= q.X0 + 1)) { blocked = true; break; }
                    if (dy != 0 && p.X >= r.X0 && p.X <= r.X1 && (dy > 0 ? r.Y0 >= q.Y1 - 1 : r.Y1 <= q.Y0 + 1)) { blocked = true; break; }
                }
                if (!blocked) return true;
            }
            return false;
        }

        static bool IsExterior(ArchModel a, ArchWall w)
        {
            if (w.Line.IsHorizontal(0.5)) return FaceVisible(a, w, 0, 1) || FaceVisible(a, w, 0, -1);
            if (w.Line.IsVertical(0.5)) return FaceVisible(a, w, 1, 0) || FaceVisible(a, w, -1, 0);
            return true;
        }

        /// <summary>Vẽ kết quả nhận diện của một tầng để so với bản vẽ gốc (lớp VXT_NHANDIEN).</summary>
        public static void DrawOverlay(IDraw2D w, ArchModel a, string level, double textH = 200)
        {
            const string L = "VXT_NHANDIEN";
            foreach (var g in a.Grids)
            {
                w.Line(L, g.Line.A.X, g.Line.A.Y, g.Line.B.X, g.Line.B.Y);
                w.Text(L, g.Line.A.X, g.Line.A.Y, textH, "Trục " + g.Name, 0, 0, 0);
            }
            foreach (var wl in a.Walls.Where(x => x.BaseLevel == level))
            {
                w.Line(L, wl.Line.A.X, wl.Line.A.Y, wl.Line.B.X, wl.Line.B.Y);
                w.Text(L, wl.Line.Mid.X, wl.Line.Mid.Y, textH * 0.7, "t=" + wl.Thickness.ToString("0"), 1, 2, 0);
            }
            foreach (var c in a.Columns.Where(x => x.BaseLevel == level))
                w.Rect(L, c.Center.X - c.B / 2, c.Center.Y - c.H / 2, c.Center.X + c.B / 2, c.Center.Y + c.H / 2);
            foreach (var o in a.Openings.Where(x => x.Level == level))
            {
                double r = o.Width / 2;
                w.Line(L, o.Location.X - r, o.Location.Y - r, o.Location.X + r, o.Location.Y + r);
                w.Line(L, o.Location.X - r, o.Location.Y + r, o.Location.X + r, o.Location.Y - r);
                w.Text(L, o.Location.X, o.Location.Y + r, textH * 0.7, (IsDoor(o) ? "Đi " : "Sổ ") + o.Width.ToString("0"), 1, 0, 0);
            }
            foreach (var rm in a.Rooms.Where(x => x.Level == level && x.Boundary.Count >= 3))
            {
                w.Text(L, rm.Boundary.Average(p => p.X), rm.Boundary.Average(p => p.Y), textH, rm.Name, 1, 2, 0);
                for (int i = 0; i < rm.Boundary.Count; i++)
                {
                    var p = rm.Boundary[i]; var q = rm.Boundary[(i + 1) % rm.Boundary.Count];
                    w.Line(L, p.X, p.Y, q.X, q.Y);
                }
            }
            foreach (var s in a.Stairs.Where(x => x.BaseLevel == level))
                for (int i = 0; i < s.Boundary.Count; i++)
                {
                    var p = s.Boundary[i]; var q = s.Boundary[(i + 1) % s.Boundary.Count];
                    w.Line(L, p.X, p.Y, q.X, q.Y);
                }
        }
    }

    /// <summary>Hợp các hình chữ nhật (tường, cột) rồi trừ lỗ cửa: nét chỉ còn ở đường biên, chỗ giao tường sạch.</summary>
    public class WallMask
    {
        readonly List<double> xs, ys;
        readonly bool[,] cell;

        public WallMask(IList<ArchAnalysis.Rect> solids, IList<ArchAnalysis.Rect> holes)
        {
            xs = Axis(solids.SelectMany(r => new[] { r.X0, r.X1 }).Concat(holes.SelectMany(r => new[] { r.X0, r.X1 })));
            ys = Axis(solids.SelectMany(r => new[] { r.Y0, r.Y1 }).Concat(holes.SelectMany(r => new[] { r.Y0, r.Y1 })));
            cell = new bool[Math.Max(xs.Count - 1, 0), Math.Max(ys.Count - 1, 0)];
            foreach (var r in solids) Mark(r, true);
            foreach (var r in holes) Mark(r, false);
        }

        static List<double> Axis(IEnumerable<double> v)
        {
            var res = new List<double>();
            foreach (var x in v.Select(q => Math.Round(q)).OrderBy(q => q))
                if (res.Count == 0 || x - res[res.Count - 1] >= 1) res.Add(x);
            return res;
        }

        static int Idx(List<double> axis, double v)
        {
            int i = axis.BinarySearch(Math.Round(v));
            if (i >= 0) return i;
            i = ~i;
            if (i >= axis.Count) return axis.Count - 1;
            if (i > 0 && v - axis[i - 1] < axis[i] - v) return i - 1;
            return i;
        }

        void Mark(ArchAnalysis.Rect r, bool value)
        {
            int i0 = Idx(xs, r.X0), i1 = Idx(xs, r.X1), j0 = Idx(ys, r.Y0), j1 = Idx(ys, r.Y1);
            for (int i = i0; i < i1; i++) for (int j = j0; j < j1; j++) cell[i, j] = value;
        }

        bool At(int i, int j) => i >= 0 && j >= 0 && i < xs.Count - 1 && j < ys.Count - 1 && cell[i, j];

        /// <summary>Các hình chữ nhật đặc (gộp theo hàng) để tô.</summary>
        public IEnumerable<ArchAnalysis.Rect> FillRects()
        {
            for (int j = 0; j < ys.Count - 1; j++)
            {
                int i = 0;
                while (i < xs.Count - 1)
                {
                    if (!cell[i, j]) { i++; continue; }
                    int s = i;
                    while (i < xs.Count - 1 && cell[i, j]) i++;
                    yield return new ArchAnalysis.Rect(xs[s], ys[j], xs[i], ys[j + 1]);
                }
            }
        }

        /// <summary>Các đoạn biên đã nối liền.</summary>
        public IEnumerable<Seg2> Edges()
        {
            for (int i = 0; i < xs.Count; i++)
            {
                int j = 0;
                while (j < ys.Count - 1)
                {
                    if (At(i - 1, j) == At(i, j)) { j++; continue; }
                    int s = j;
                    bool state = At(i, j);
                    while (j < ys.Count - 1 && At(i - 1, j) != At(i, j) && At(i, j) == state) j++;
                    yield return new Seg2(new P2(xs[i], ys[s]), new P2(xs[i], ys[j]));
                }
            }
            for (int j = 0; j < ys.Count; j++)
            {
                int i = 0;
                while (i < xs.Count - 1)
                {
                    if (At(i, j - 1) == At(i, j)) { i++; continue; }
                    int s = i;
                    bool state = At(i, j);
                    while (i < xs.Count - 1 && At(i, j - 1) != At(i, j) && At(i, j) == state) i++;
                    yield return new Seg2(new P2(xs[s], ys[j]), new P2(xs[i], ys[j]));
                }
            }
        }
    }
}
