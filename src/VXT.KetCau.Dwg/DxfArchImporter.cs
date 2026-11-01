using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;

namespace VXT.KetCau.Dwg
{
    /// <summary>Quy ước tên lớp (layer) trong bản vẽ kiến trúc — so khớp theo từ khoá, không phân biệt hoa thường.</summary>
    public class LayerMap
    {
        public string[] Grid { get; set; } = { "TRUC", "AXIS", "GRID", "A-GRID", "S-GRID" };
        public string[] GridBubble { get; set; } = { "TRUC", "AXIS", "GRID", "BUBBLE", "KH_TRUC" };
        public string[] Wall { get; set; } = { "TUONG", "WALL", "A-WALL", "XAY" };
        public string[] Column { get; set; } = { "COT", "COLUMN", "A-COLS", "S-COLS" };
        public string[] Door { get; set; } = { "CUA", "DOOR", "WINDOW", "A-DOOR", "A-GLAZ" };
        public string[] Room { get; set; } = { "PHONG", "ROOM", "TEXT", "CHU", "A-AREA" };
        public string[] Stair { get; set; } = { "THANG", "STAIR", "A-FLOR-STRS" };
        public string[] Slab { get; set; } = { "SAN", "SLAB", "FLOOR", "A-FLOR" };

        public static bool Match(string layer, string[] keys)
        {
            var l = (layer ?? "").ToUpperInvariant();
            return keys.Any(k => l == k || l.Contains(k));
        }
    }

    /// <summary>Một mặt bằng tầng (một file DWG/DXF) ứng với một cao độ.</summary>
    public class FloorPlanInput
    {
        public string Path { get; set; }
        public string LevelName { get; set; }
        /// <summary>Cao độ hoàn thiện sàn tầng (mm).</summary>
        public double Elevation { get; set; }
        public bool IsRoof { get; set; }
        /// <summary>Dịch gốc toạ độ để các tầng trùng nhau (mm).</summary>
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
    }

    /// <summary>
    /// Đọc hồ sơ kiến trúc từ DWG/DXF mặt bằng các tầng → ArchModel.
    /// Nhận diện: trục (đường + ký hiệu trục), tường (cặp đường song song → tim tường, chiều dày),
    /// cột (hình chữ nhật kín / khối), cửa (giảm tải tường), tên phòng (tra hoạt tải), cầu thang.
    /// </summary>
    public class DxfArchImporter
    {
        public LayerMap Layers { get; set; } = new LayerMap();
        /// <summary>Hệ số đổi đơn vị bản vẽ → mm (0 = tự nhận biết).</summary>
        public double UnitScale { get; set; } = 0;
        public double MinWallThickness { get; set; } = 90;
        public double MaxWallThickness { get; set; } = 450;
        public List<string> Log { get; } = new List<string>();

        public ArchModel Import(IList<FloorPlanInput> plans, string odaExe = null)
        {
            if (plans.Count == 0) throw new ArgumentException("Chưa chọn mặt bằng nào");
            var a = new ArchModel { Source = "DWG/DXF", ProjectName = Path.GetFileNameWithoutExtension(plans[0].Path) };
            var ordered = plans.OrderBy(p => p.Elevation).ToList();
            foreach (var p in ordered) a.Levels.Add(new ArchLevel { Name = p.LevelName, Elevation = p.Elevation, IsRoof = p.IsRoof });

            for (int i = 0; i < ordered.Count; i++)
            {
                var plan = ordered[i];
                string file = plan.Path;
                if (file.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)) file = DwgConverter.ToDxf(file, odaExe);
                var dxf = DxfReader.Load(file);
                double k = UnitScale > 0 ? UnitScale : GuessScale(dxf);
                var off = new P2(plan.OffsetX, plan.OffsetY);
                Func<P2, P2> T = q => new P2(q.X * k, q.Y * k) + off;
                double height = i + 1 < ordered.Count ? ordered[i + 1].Elevation - plan.Elevation : 0;
                string top = i + 1 < ordered.Count ? ordered[i + 1].LevelName : null;

                // ----- Trục (lấy từ tầng có nhiều trục nhất; trục tầng sau trùng thì bỏ)
                var gridLines = dxf.Entities.OfType<DxfLine>().Where(l => LayerMap.Match(l.Layer, Layers.Grid))
                                   .Select(l => new Seg2(T(l.A), T(l.B))).Where(s => s.Length > 1000).ToList();
                gridLines.AddRange(dxf.Entities.OfType<DxfPolyline>().Where(l => LayerMap.Match(l.Layer, Layers.Grid) && l.Points.Count == 2)
                                   .Select(l => new Seg2(T(l.Points[0]), T(l.Points[1]))));
                var bubbles = dxf.Entities.OfType<DxfText>().Where(t => LayerMap.Match(t.Layer, Layers.GridBubble) && t.Text.Length <= 4).ToList();
                foreach (var gl in MergeCollinear(gridLines))
                {
                    if (a.Grids.Any(g => Parallel(g.Line, gl) && g.Line.DistanceTo(gl.Mid) < Tol.Snap)) continue;
                    var name = bubbles.Select(b => (b, d: Math.Min(T(b.At).DistanceTo(gl.A), T(b.At).DistanceTo(gl.B))))
                                      .Where(x => x.d < 2000).OrderBy(x => x.d).Select(x => x.b.Text).FirstOrDefault();
                    a.Grids.Add(new ArchGrid { Name = name ?? "?", Line = gl });
                }

                // ----- Tường
                var wallSegs = new List<Seg2>();
                foreach (var e in dxf.Entities.Where(x => LayerMap.Match(x.Layer, Layers.Wall)))
                {
                    if (e is DxfLine l) wallSegs.Add(new Seg2(T(l.A), T(l.B)));
                    else if (e is DxfPolyline pl)
                    {
                        for (int j = 0; j + 1 < pl.Points.Count; j++) wallSegs.Add(new Seg2(T(pl.Points[j]), T(pl.Points[j + 1])));
                        if (pl.Closed && pl.Points.Count > 2) wallSegs.Add(new Seg2(T(pl.Points.Last()), T(pl.Points[0])));
                    }
                }
                int wn = 0;
                foreach (var (center, thick) in PairWalls(wallSegs.Where(s => s.Length > 200).ToList()))
                    a.Walls.Add(new ArchWall { Id = $"{plan.LevelName}-W{++wn}", Line = center, Thickness = thick, BaseLevel = plan.LevelName, TopLevel = top,
                                               BaseElevation = plan.Elevation, Height = height, Material = thick >= 200 ? "Gạch đặc" : "Gạch rỗng" });

                // ----- Cột
                int cn = 0;
                foreach (var pl in dxf.Entities.OfType<DxfPolyline>().Where(x => LayerMap.Match(x.Layer, Layers.Column) && x.Points.Count >= 4))
                {
                    var pts = pl.Points.Select(T).ToList();
                    var (ctr, b, h, rot) = MinRect(pts);
                    if (b < 150 || h < 150 || b > 2000 || h > 2000) continue;
                    if (a.Columns.Any(c => c.BaseLevel == plan.LevelName && c.Center.DistanceTo(ctr) < Tol.Snap)) continue;
                    a.Columns.Add(new ArchColumn { Id = $"{plan.LevelName}-C{++cn}", Center = ctr, B = b, H = h, RotationDeg = rot, BaseLevel = plan.LevelName, TopLevel = top });
                }

                // ----- Cửa → lỗ trên tường gần nhất
                foreach (var g in dxf.Entities.Where(x => LayerMap.Match(x.Layer, Layers.Door)).GroupBy(x => x.Layer))
                {
                    // ước lượng mỗi cửa bằng một khối nhỏ: lấy các đường song song tường dài 600–3000
                    foreach (var l in g.OfType<DxfLine>())
                    {
                        var s = new Seg2(T(l.A), T(l.B));
                        if (s.Length < 600 || s.Length > 3000) continue;
                        var host = a.Walls.Where(w => w.BaseLevel == plan.LevelName && Parallel(w.Line, s) && w.Line.DistanceTo(s.Mid) < w.Thickness).FirstOrDefault();
                        if (host == null) continue;
                        if (a.Openings.Any(o => o.HostWallId == host.Id && o.Location.DistanceTo(s.Mid) < 300)) continue;
                        bool door = g.Key.ToUpperInvariant().Contains("CUA DI") || g.Key.ToUpperInvariant().Contains("DOOR");
                        a.Openings.Add(new ArchOpening { HostWallId = host.Id, Location = s.Mid, Width = s.Length, Height = door ? 2200 : 1400, SillHeight = door ? 0 : 900, Kind = door ? "Cửa đi" : "Cửa sổ" });
                    }
                }

                // ----- Tên phòng (đánh dấu bằng ô vuông nhỏ quanh chữ; FramingGenerator nhận theo điểm)
                var roomRx = new Regex("(P\\.?|PHÒNG|WC|VỆ SINH|BẾP|KHÁCH|NGỦ|SẢNH|HÀNH LANG|BAN CÔNG|LÔ GIA|KHO|GARA|THỜ|SÂN|MÁI|LÀM VIỆC|ĂN|SINH HOẠT)", RegexOptions.IgnoreCase);
                foreach (var t in dxf.Entities.OfType<DxfText>().Where(x => LayerMap.Match(x.Layer, Layers.Room) || roomRx.IsMatch(x.Text)))
                {
                    if (!roomRx.IsMatch(t.Text) || t.Text.Length > 40) continue;
                    var c = T(t.At);
                    a.Rooms.Add(new ArchRoom { Name = t.Text, Usage = t.Text, Level = plan.LevelName,
                        Boundary = { new P2(c.X - 50, c.Y - 50), new P2(c.X + 50, c.Y - 50), new P2(c.X + 50, c.Y + 50), new P2(c.X - 50, c.Y + 50) } });
                }

                // ----- Cầu thang (đường bao kín lớn trên lớp thang)
                foreach (var pl in dxf.Entities.OfType<DxfPolyline>().Where(x => LayerMap.Match(x.Layer, Layers.Stair) && x.Closed && x.Points.Count >= 4))
                {
                    var pts = pl.Points.Select(T).ToList();
                    if (Math.Abs(GeomUtil.Area(pts)) < 2e6) continue;
                    a.Stairs.Add(new ArchStair { Boundary = pts, BaseLevel = plan.LevelName, TopLevel = top });
                }

                // ----- Sàn (đường bao kín trên lớp sàn)
                foreach (var pl in dxf.Entities.OfType<DxfPolyline>().Where(x => LayerMap.Match(x.Layer, Layers.Slab) && x.Closed && x.Points.Count >= 3))
                    a.Floors.Add(new ArchFloor { Boundary = pl.Points.Select(T).ToList(), Level = plan.LevelName, Elevation = plan.Elevation });

                Log.Add($"{plan.LevelName}: {a.Walls.Count(w => w.BaseLevel == plan.LevelName)} tường, {a.Columns.Count(c => c.BaseLevel == plan.LevelName)} cột, {a.Rooms.Count(r => r.Level == plan.LevelName)} phòng (tỉ lệ ×{k})");
            }
            // đặt tên trục còn thiếu
            int nx = 0, ny = 0;
            foreach (var g in a.Grids.OrderBy(g => g.Line.Mid.X).ThenBy(g => g.Line.Mid.Y))
                if (g.Name == "?") g.Name = g.Line.IsHorizontal(5) ? ((char)('A' + ny++)).ToString() : (++nx).ToString();
            Log.Add($"Tổng: {a.Grids.Count} trục, {a.Walls.Count} tường, {a.Columns.Count} cột");
            return a;
        }

        /// <summary>Tự nhận biết đơn vị: kích thước bao &lt; 500 → mét (×1000), &lt; 5000 → cm (×10), còn lại mm.</summary>
        static double GuessScale(DxfReader dxf)
        {
            if (dxf.InsUnits == 6) return 1000; // m
            if (dxf.InsUnits == 5) return 10;   // cm
            var pts = dxf.Entities.OfType<DxfLine>().SelectMany(l => new[] { l.A, l.B }).ToList();
            if (pts.Count == 0) return 1;
            double ext = Math.Max(pts.Max(p => p.X) - pts.Min(p => p.X), pts.Max(p => p.Y) - pts.Min(p => p.Y));
            return ext < 500 ? 1000 : (ext < 5000 ? 10 : 1);
        }

        static bool Parallel(Seg2 a, Seg2 b) => Math.Abs(Math.Abs(a.Dir.Dot(b.Dir)) - 1) < 1e-3;

        /// <summary>Nối các đoạn thẳng hàng chồng lên nhau (trục bị cắt đoạn).</summary>
        static List<Seg2> MergeCollinear(List<Seg2> segs)
        {
            var res = new List<Seg2>();
            var used = new bool[segs.Count];
            for (int i = 0; i < segs.Count; i++)
            {
                if (used[i]) continue;
                var s = segs[i]; var dir = s.Dir;
                var pts = new List<P2> { s.A, s.B };
                for (int j = i + 1; j < segs.Count; j++)
                {
                    if (used[j] || !Parallel(s, segs[j])) continue;
                    if (Math.Abs((segs[j].A - s.A).Cross(dir)) > Tol.Mm) continue;
                    used[j] = true; pts.Add(segs[j].A); pts.Add(segs[j].B);
                }
                var ord = pts.OrderBy(p => (p - s.A).Dot(dir)).ToList();
                res.Add(new Seg2(ord.First(), ord.Last()));
            }
            return res;
        }

        /// <summary>Ghép cặp đường biên tường song song cách nhau trong [MinWall, MaxWall] → tim tường + chiều dày.</summary>
        IEnumerable<(Seg2 center, double thick)> PairWalls(List<Seg2> segs)
        {
            var used = new HashSet<int>();
            var result = new List<(Seg2, double)>();
            for (int i = 0; i < segs.Count; i++)
            {
                if (used.Contains(i)) continue;
                var a = segs[i]; var dir = a.Dir;
                int best = -1; double bestD = double.MaxValue; double ov0 = 0, ov1 = 0;
                for (int j = 0; j < segs.Count; j++)
                {
                    if (j == i || used.Contains(j) || !Parallel(a, segs[j])) continue;
                    var b = segs[j];
                    double d = Math.Abs((b.A - a.A).Cross(dir));
                    if (d < MinWallThickness || d > MaxWallThickness) continue;
                    double ta0 = 0, ta1 = a.Length;
                    double tb0 = (b.A - a.A).Dot(dir), tb1 = (b.B - a.A).Dot(dir);
                    double o0 = Math.Max(ta0, Math.Min(tb0, tb1)), o1 = Math.Min(ta1, Math.Max(tb0, tb1));
                    if (o1 - o0 < Math.Min(500, 0.5 * Math.Min(a.Length, b.Length))) continue;
                    if (d < bestD) { bestD = d; best = j; ov0 = o0; ov1 = o1; }
                }
                if (best < 0) continue;
                used.Add(i); used.Add(best);
                var nrm = new P2(-dir.Y, dir.X);
                if ((segs[best].A - a.A).Dot(nrm) < 0) nrm = nrm * -1;
                var c0 = a.A + dir * ov0 + nrm * (bestD / 2);
                var c1 = a.A + dir * ov1 + nrm * (bestD / 2);
                double t = Math.Round(bestD / 10) * 10;
                if (!result.Any(r => r.Item1.DistanceTo(c0) < Tol.Snap && r.Item1.DistanceTo(c1) < Tol.Snap)) result.Add((new Seg2(c0, c1), t));
            }
            // nối các đoạn tim tường thẳng hàng liền nhau (tường bị cắt bởi cột/cửa)
            var merged = new List<(Seg2, double)>();
            foreach (var g in result.GroupBy(r => r.Item2))
            {
                var segsT = g.Select(r => r.Item1).ToList();
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    for (int i = 0; i < segsT.Count && !changed; i++)
                        for (int j = i + 1; j < segsT.Count && !changed; j++)
                        {
                            var s1 = segsT[i]; var s2 = segsT[j];
                            if (!Parallel(s1, s2) || Math.Abs((s2.A - s1.A).Cross(s1.Dir)) > Tol.Snap) continue;
                            double gap = Math.Min(Math.Min(s1.A.DistanceTo(s2.A), s1.A.DistanceTo(s2.B)), Math.Min(s1.B.DistanceTo(s2.A), s1.B.DistanceTo(s2.B)));
                            bool overlap = s1.DistanceTo(s2.A) < Tol.Mm || s1.DistanceTo(s2.B) < Tol.Mm;
                            if (gap > 2500 && !overlap) continue; // khe ≤ 2.5 m (lỗ cửa) coi là cùng tường
                            var pts = new[] { s1.A, s1.B, s2.A, s2.B }.OrderBy(p => (p - s1.A).Dot(s1.Dir)).ToList();
                            segsT[i] = new Seg2(pts.First(), pts.Last()); segsT.RemoveAt(j); changed = true;
                        }
                }
                merged.AddRange(segsT.Select(s => (s, g.Key)));
            }
            return merged;
        }

        /// <summary>Hình chữ nhật bao nhỏ nhất của đa giác (gần đúng theo cạnh dài nhất).</summary>
        static (P2 ctr, double b, double h, double rotDeg) MinRect(List<P2> pts)
        {
            P2 dir = new P2(1, 0); double maxL = 0;
            for (int i = 0; i < pts.Count; i++) { var e = pts[(i + 1) % pts.Count] - pts[i]; if (e.Length > maxL) { maxL = e.Length; dir = e.Unit; } }
            var nrm = new P2(-dir.Y, dir.X);
            double u0 = pts.Min(p => p.Dot(dir)), u1 = pts.Max(p => p.Dot(dir)), v0 = pts.Min(p => p.Dot(nrm)), v1 = pts.Max(p => p.Dot(nrm));
            var ctr = dir * ((u0 + u1) / 2) + nrm * ((v0 + v1) / 2);
            double rot = Math.Atan2(dir.Y, dir.X) * 180 / Math.PI;
            rot = Math.Round(rot, 1) % 90; if (Math.Abs(rot) < 0.5) rot = 0;
            double b = Math.Round((u1 - u0) / 10) * 10, h = Math.Round((v1 - v0) / 10) * 10;
            if (rot == 0 && Math.Abs(dir.Y) > 0.7) { var t = b; b = h; h = t; }
            return (ctr, b, h, rot);
        }
    }
}
