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
        /// <summary>Khung chọn tầng khi nhiều tầng vẽ chung một bản vẽ (đơn vị bản vẽ). Null = cả bản vẽ.</summary>
        public double[] Window { get; set; }
        /// <summary>Điểm gốc của tầng trong bản vẽ (thường là giao trục đầu) — trừ đi để các tầng trùng nhau.</summary>
        public double BaseX { get; set; }
        public double BaseY { get; set; }
        /// <summary>Chỉ khai cao độ, không đọc hình (vd cốt mái không có mặt bằng riêng).</summary>
        public bool LevelOnly { get; set; }
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
        /// <summary>Hệ số đổi đơn vị đã dùng ở lần đọc gần nhất.</summary>
        public double ScaleUsed { get; private set; } = 1;

        static P2 Anchor(DxfEntity e)
        {
            switch (e)
            {
                case DxfLine l: return (l.A + l.B) * 0.5;
                case DxfPolyline p when p.Points.Count > 0: return new P2(p.Points.Average(q => q.X), p.Points.Average(q => q.Y));
                case DxfCircle c: return c.Center;
                case DxfArc a: return a.Center;
                case DxfText t: return t.At;
            }
            return default;
        }

        static bool InWindow(DxfEntity e, double[] w)
        {
            if (w == null || w.Length != 4) return true;
            var p = Anchor(e);
            return p.X >= Math.Min(w[0], w[2]) && p.X <= Math.Max(w[0], w[2]) && p.Y >= Math.Min(w[1], w[3]) && p.Y <= Math.Max(w[1], w[3]);
        }

        public ArchModel Import(IList<FloorPlanInput> plans, string odaExe = null)
        {
            if (plans.Count == 0) throw new ArgumentException("Chưa chọn mặt bằng nào");
            var a = new ArchModel { Source = "DWG/DXF", ProjectName = Path.GetFileNameWithoutExtension(plans.First(p => !p.LevelOnly).Path) };
            var ordered = plans.OrderBy(p => p.Elevation).ToList();
            foreach (var p in ordered) a.Levels.Add(new ArchLevel { Name = p.LevelName, Elevation = p.Elevation, IsRoof = p.IsRoof });

            for (int i = 0; i < ordered.Count; i++)
            {
                var plan = ordered[i];
                if (plan.LevelOnly) continue;
                string file = plan.Path;
                if (file.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)) file = DwgConverter.ToDxf(file, odaExe);
                var dxf = DxfReader.Load(file);
                if (plan.Window != null) dxf.Entities.RemoveAll(e => !InWindow(e, plan.Window));
                double k = UnitScale > 0 ? UnitScale : GuessScale(dxf);
                ScaleUsed = k;
                var off = new P2(plan.OffsetX, plan.OffsetY);
                var bas = new P2(plan.BaseX, plan.BaseY);
                Func<P2, P2> T = q => new P2((q.X - bas.X) * k, (q.Y - bas.Y) * k) + off;
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
                var wallGaps = new List<(P2 center, double width, string wallId)>();
                foreach (var (center, thick, gaps) in PairWalls(wallSegs.Where(s => s.Length > 200).ToList()))
                {
                    string wid = $"{plan.LevelName}-W{++wn}";
                    a.Walls.Add(new ArchWall { Id = wid, Line = center, Thickness = thick, BaseLevel = plan.LevelName, TopLevel = top,
                                               BaseElevation = plan.Elevation, Height = height, Material = thick >= 200 ? "Gạch đặc" : "Gạch rỗng" });
                    foreach (var g in gaps) wallGaps.Add((g.center, g.width, wid));
                }

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

                // ----- Cửa đi: cung mở cửa — tâm cung là bản lề, bán kính là bề rộng cánh
                var hinges = new List<(P2 hinge, double r, ArchWall host, P2 onWall, int side, DxfArc arc)>();
                foreach (var arc in dxf.Entities.OfType<DxfArc>().Where(x => LayerMap.Match(x.Layer, Layers.Door)))
                {
                    var c = T(arc.Center);
                    double r = arc.R * k;
                    if (r < 400 || r > 1800) continue;
                    var host = a.Walls.Where(w => w.BaseLevel == plan.LevelName && w.Line.DistanceTo(c) <= w.Thickness)
                                      .OrderBy(w => w.Line.DistanceTo(c)).FirstOrDefault();
                    if (host == null) continue;
                    double mid = (arc.StartDeg + (((arc.EndDeg - arc.StartDeg) % 360 + 360) % 360) / 2) * Math.PI / 180;
                    var tip = new P2(Math.Cos(mid), Math.Sin(mid));
                    var d = host.Line.Dir; var n = new P2(-d.Y, d.X);
                    var onWall = host.Line.A + d * (c - host.Line.A).Dot(d);
                    hinges.Add((c, r, host, onWall, tip.Dot(n) >= 0 ? 1 : -1, arc));
                }
                var usedArc = new HashSet<int>();
                for (int h1 = 0; h1 < hinges.Count; h1++)
                {
                    if (usedArc.Contains(h1)) continue;
                    var h = hinges[h1];
                    var d = h.host.Line.Dir;
                    double mid = (h.arc.StartDeg + (((h.arc.EndDeg - h.arc.StartDeg) % 360 + 360) % 360) / 2) * Math.PI / 180;
                    double along = new P2(Math.Cos(mid), Math.Sin(mid)).Dot(d);
                    // hai cánh đối đầu trên cùng tường → một cửa hai cánh
                    int mate = -1;
                    for (int h2 = h1 + 1; h2 < hinges.Count; h2++)
                        if (!usedArc.Contains(h2) && hinges[h2].host == h.host && Math.Abs(hinges[h2].onWall.DistanceTo(h.onWall) - (hinges[h2].r + h.r)) < 60) { mate = h2; break; }
                    P2 center; double width; int hingeEnd;
                    if (mate >= 0)
                    {
                        usedArc.Add(mate);
                        center = (h.onWall + hinges[mate].onWall) * 0.5;
                        width = h.r + hinges[mate].r;
                        hingeEnd = 0;
                    }
                    else
                    {
                        center = h.onWall + d * (along >= 0 ? h.r / 2 : -h.r / 2);
                        width = h.r;
                        hingeEnd = along >= 0 ? 0 : 1;
                    }
                    a.Openings.Add(new ArchOpening { HostWallId = h.host.Id, Level = plan.LevelName, Location = center, Width = Math.Round(width / 10) * 10, Height = 2200, SillHeight = 0, Kind = "Cửa đi", HingeEnd = hingeEnd, SwingSide = h.side });
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
                        // nét nằm trong bề rộng một cửa đã có (khung, cánh) thì bỏ qua
                        if (a.Openings.Any(o => o.HostWallId == host.Id && (o.Location - s.Mid).Length < o.Width / 2 + 100)) continue;
                        bool door = g.Key.ToUpperInvariant().Contains("CUA DI") || g.Key.ToUpperInvariant().Contains("DOOR");
                        a.Openings.Add(new ArchOpening { HostWallId = host.Id, Level = plan.LevelName, Location = s.Mid, Width = s.Length, Height = door ? 2200 : 1400, SillHeight = door ? 0 : 900, Kind = door ? "Cửa đi" : "Cửa sổ" });
                    }
                }

                // ----- Khe trên tường mà chưa có cửa nào: ghi nhận là lỗ mở, chờ người dùng xác nhận loại
                foreach (var gp in wallGaps)
                {
                    var host = a.Walls.First(w => w.Id == gp.wallId);
                    if (a.Openings.Any(o => o.HostWallId == host.Id && o.Location.DistanceTo(gp.center) < Math.Max(o.Width, gp.width) / 2)) continue;
                    a.Openings.Add(new ArchOpening { HostWallId = host.Id, Level = plan.LevelName, Location = gp.center, Width = Math.Round(gp.width / 10) * 10, Height = 1400, SillHeight = 900, Kind = "Cửa sổ" });
                    Log.Add($"{plan.LevelName}: khe tường rộng {gp.width:0} tại {gp.center} tạm coi là cửa sổ — kiểm tra lại");
                }

                // ----- Tên phòng (đánh dấu bằng ô vuông nhỏ quanh chữ; FramingGenerator nhận theo điểm)
                foreach (var t in dxf.Entities.OfType<DxfText>())
                {
                    if (!IsRoomName(t.Text)) continue;
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

        static readonly Regex RoomRx = new Regex(@"(^P\.|^PN\d*$|^PK$|\bPHÒNG\b|^WC\b|\bVỆ SINH\b|\bBẾP\b|\bKHÁCH\b|\bNGỦ\b|\bSẢNH\b|\bHÀNH LANG\b|\bBAN CÔNG\b|\bLÔ GIA\b|\bKHO\b|\bGARA\b|\bTHỜ\b|\bSÂN\b|\bLÀM VIỆC\b|\bĂN\b|\bSINH HOẠT\b|\bTẮM\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex NotRoomRx = new Regex(@"(MẶT BẰNG|MẶT ĐỨNG|MẶT CẮT|CHI TIẾT|TỈ LỆ|TỶ LỆ|\bTL\b|GHI CHÚ|BẢN VẼ|CÔNG TRÌNH)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Chữ có phải tên phòng không: phải khớp từ khoá nguyên từ, không phải tên hình hay ghi chú.</summary>
        public static bool IsRoomName(string text)
            => !string.IsNullOrWhiteSpace(text) && text.Length <= 40 && RoomRx.IsMatch(text) && !NotRoomRx.IsMatch(text);

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

        /// <summary>Ghép cặp đường biên tường song song cách nhau trong [MinWall, MaxWall] → tim tường + chiều dày + các khe (lỗ cửa).</summary>
        IEnumerable<(Seg2 center, double thick, List<(P2 center, double width)> gaps)> PairWalls(List<Seg2> segs)
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
            // nối các đoạn tim tường thẳng hàng liền nhau (tường bị cắt bởi cột/cửa); khe ≥ 500 mm giữa hai đoạn là lỗ mở
            var merged = new List<(Seg2, double, List<(P2, double)>)>();
            foreach (var g in result.GroupBy(r => r.Item2))
            {
                var pieces = g.Select(r => new List<Seg2> { r.Item1 }).ToList();
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    for (int i = 0; i < pieces.Count && !changed; i++)
                        for (int j = i + 1; j < pieces.Count && !changed; j++)
                        {
                            var s1 = Span(pieces[i]); var s2 = Span(pieces[j]);
                            if (!Parallel(s1, s2) || Math.Abs((s2.A - s1.A).Cross(s1.Dir)) > Tol.Snap) continue;
                            double gap = Math.Min(Math.Min(s1.A.DistanceTo(s2.A), s1.A.DistanceTo(s2.B)), Math.Min(s1.B.DistanceTo(s2.A), s1.B.DistanceTo(s2.B)));
                            bool overlap = s1.DistanceTo(s2.A) < Tol.Mm || s1.DistanceTo(s2.B) < Tol.Mm;
                            if (gap > 2500 && !overlap) continue; // khe ≤ 2.5 m (lỗ cửa) coi là cùng tường
                            pieces[i].AddRange(pieces[j]); pieces.RemoveAt(j); changed = true;
                        }
                }
                foreach (var list in pieces)
                {
                    var whole = Span(list);
                    var dir = whole.Dir;
                    var iv = list.Select(q => (a: Math.Min((q.A - whole.A).Dot(dir), (q.B - whole.A).Dot(dir)), b: Math.Max((q.A - whole.A).Dot(dir), (q.B - whole.A).Dot(dir)))).OrderBy(q => q.a).ToList();
                    var gaps = new List<(P2, double)>();
                    double reach = iv[0].b;
                    for (int i = 1; i < iv.Count; i++)
                    {
                        if (iv[i].a - reach >= 500) gaps.Add((whole.A + dir * ((reach + iv[i].a) / 2), iv[i].a - reach));
                        reach = Math.Max(reach, iv[i].b);
                    }
                    merged.Add((whole, g.Key, gaps));
                }
            }
            return merged;
        }

        /// <summary>Đoạn bao các đoạn thẳng hàng.</summary>
        static Seg2 Span(List<Seg2> list)
        {
            var d = list[0].Dir; var o = list[0].A;
            var pts = list.SelectMany(q => new[] { q.A, q.B }).OrderBy(p => (p - o).Dot(d)).ToList();
            return new Seg2(pts.First(), pts.Last());
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
