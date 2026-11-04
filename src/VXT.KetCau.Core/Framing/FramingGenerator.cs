using System;
using System.Collections.Generic;
using System.Linq;
using VXT.KetCau.Core.Codes;
using VXT.KetCau.Core.Design;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;

namespace VXT.KetCau.Core.Framing
{
    public class FramingOptions
    {
        /// <summary>Chiều dày lớp hoàn thiện sàn: cao độ kết cấu = cao độ kiến trúc − giá trị này (mm).</summary>
        public double FinishThickness { get; set; } = 50;
        /// <summary>Cao độ đỉnh móng so với cốt ±0.000 (mm, âm).</summary>
        public double FoundationTop { get; set; } = -1000;
        /// <summary>Chiều dày tường tối thiểu để tạo cột tại giao trục (mm).</summary>
        public double MinWallForColumn { get; set; } = 200;
        /// <summary>Tường dày ≥ giá trị này mà không nằm trên dầm khung sẽ được đỡ bằng dầm phụ (mm).</summary>
        public double MinWallForSecondaryBeam { get; set; } = 100;
        /// <summary>Tạo giằng móng tại cốt nền tầng 1.</summary>
        public bool TieBeamsAtGround { get; set; } = true;
        /// <summary>Bề rộng cột lấy bằng chiều dày tường (giấu cột trong tường).</summary>
        public bool ColumnFitsWall { get; set; } = true;
        /// <summary>Tải sơ bộ trên 1 m² sàn để chọn tiết diện cột (kN/m²).</summary>
        public double PreliminaryFloorLoad { get; set; } = 12;
        /// <summary>Sức chịu tải nền R (kPa) cho móng đơn sơ bộ.</summary>
        public double SoilBearing { get; set; } = 150;
    }

    public class FramingReport
    {
        public List<string> Info { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Sinh mô hình kết cấu (cột, dầm khung, dầm phụ, sàn, móng, tải trọng, tổ hợp) từ hồ sơ kiến trúc.
    /// Phù hợp nhà khung BTCT có lưới trục trực giao; với lưới không trực giao vẫn tạo cột/dầm theo trục,
    /// sàn lấy theo đường bao sàn kiến trúc.
    /// </summary>
    public class FramingGenerator
    {
        readonly FramingOptions opt;
        readonly DesignSettings set;
        public FramingReport Report { get; } = new FramingReport();

        public FramingGenerator(DesignSettings s, FramingOptions o = null) { set = s; opt = o ?? new FramingOptions(); }

        public StructModel Generate(ArchModel a)
        {
            var m = new StructModel { ProjectName = a.ProjectName, Settings = set };
            BuildStories(a, m);
            BuildGrids(a, m);
            var colLocs = ColumnLocations(a, m);
            BuildColumns(a, m, colLocs);
            BuildBeams(a, m, colLocs);
            BuildSlabs(a, m);
            SizeColumns(m);
            BuildFootings(m);
            BuildLoads(a, m);
            Report.Info.Add($"Đã tạo {m.Stories.Count} tầng, {m.Columns.Count} cột, {m.Beams.Count} dầm, {m.Slabs.Count} ô sàn, {m.Footings.Count} móng");
            return m;
        }

        // ---------------------------------------------------------------- Tầng
        void BuildStories(ArchModel a, StructModel m)
        {
            var levels = a.Levels.OrderBy(l => l.Elevation).ToList();
            if (levels.Count < 2) throw new InvalidOperationException("Hồ sơ kiến trúc cần ít nhất 2 cao độ tầng");
            double baseZ = levels[0].Elevation + opt.FoundationTop;
            m.Stories.Add(new Story { Name = "Móng", Elevation = baseZ, Height = 0 });
            double prev = baseZ;
            for (int i = 0; i < levels.Count; i++)
            {
                var l = levels[i];
                double z = i == 0 ? l.Elevation - opt.FinishThickness : l.Elevation - opt.FinishThickness;
                m.Stories.Add(new Story { Name = l.Name, Elevation = z, Height = z - prev, IsRoof = l.IsRoof || i == levels.Count - 1 });
                prev = z;
            }
        }

        // ---------------------------------------------------------------- Trục
        void BuildGrids(ArchModel a, StructModel m)
        {
            if (a.Grids.Count >= 2)
            {
                foreach (var g in a.Grids)
                    m.Grids.Add(new GridLine { Name = g.Name, Line = g.Line, IsXDir = g.Line.IsHorizontal(5) });
                return;
            }
            // Không có trục: suy từ tim tường dày
            var thick = a.Walls.Where(w => w.Thickness >= opt.MinWallForColumn).ToList();
            var xs = GeomUtil.Cluster(thick.Where(w => w.Line.IsVertical(2)).Select(w => w.Line.Mid.X), Tol.Snap);
            var ys = GeomUtil.Cluster(thick.Where(w => w.Line.IsHorizontal(2)).Select(w => w.Line.Mid.Y), Tol.Snap);
            if (xs.Count < 2 || ys.Count < 2) throw new InvalidOperationException("Không có lưới trục và không suy ra được trục từ tường");
            double x0 = xs.First() - 1000, x1 = xs.Last() + 1000, y0 = ys.First() - 1000, y1 = ys.Last() + 1000;
            for (int i = 0; i < xs.Count; i++)
                m.Grids.Add(new GridLine { Name = (i + 1).ToString(), Line = new Seg2(new P2(xs[i], y0), new P2(xs[i], y1)), IsXDir = false });
            for (int j = 0; j < ys.Count; j++)
                m.Grids.Add(new GridLine { Name = ((char)('A' + j)).ToString(), Line = new Seg2(new P2(x0, ys[j]), new P2(x1, ys[j])), IsXDir = true });
            Report.Warnings.Add("Hồ sơ không có lưới trục — đã tự suy trục từ tim tường, cần kiểm tra lại");
        }

        // ---------------------------------------------------------------- Vị trí cột
        /// <summary>Vị trí cột và tầng kiến trúc cao nhất mà cột còn chạy lên (chỉ số trong danh sách tầng KT).</summary>
        class ColLoc { public P2 P; public int TopLevel = -1; public double B, H, Rot; public bool FromArch; }

        List<ColLoc> ColumnLocations(ArchModel a, StructModel m)
        {
            var levels = a.Levels.OrderBy(l => l.Elevation).ToList();
            var locs = new List<ColLoc>();
            Func<P2, ColLoc> find = p => locs.FirstOrDefault(c => c.P.DistanceTo(p) < Tol.Snap * 2);

            // 1) Cột kiến trúc (bắt dính giao trục gần nhất)
            var inters = GridIntersections(m);
            foreach (var ac in a.Columns)
            {
                var p = ac.Center;
                var snap = inters.OrderBy(q => q.DistanceTo(p)).FirstOrDefault();
                if (inters.Count > 0 && snap.DistanceTo(p) < Math.Max(ac.B, ac.H)) p = snap;
                int top = levels.FindIndex(l => l.Name == ac.TopLevel);
                int bot = levels.FindIndex(l => l.Name == ac.BaseLevel);
                var c = find(p) ?? new ColLoc { P = p, B = ac.B, H = ac.H, Rot = ac.RotationDeg, FromArch = true };
                if (!locs.Contains(c)) locs.Add(c);
                c.TopLevel = Math.Max(c.TopLevel, top > 0 ? top - 1 : Math.Max(bot, 0));
            }

            // 2) Giao trục có tường dày đi qua (tường đứng trên tầng i → cột chạy tới tầng i+1)
            foreach (var p in inters)
            {
                for (int li = 0; li < levels.Count - 1; li++)
                {
                    var walls = a.Walls.Where(w => w.BaseLevel == levels[li].Name && w.Thickness >= opt.MinWallForColumn
                                                   && w.Line.DistanceTo(p) <= w.Thickness / 2 + Tol.Snap).ToList();
                    bool corner = walls.Count >= 2 || walls.Any(w => w.Line.A.DistanceTo(p) < Tol.Snap * 4 || w.Line.B.DistanceTo(p) < Tol.Snap * 4);
                    bool floorCorner = a.Floors.Any(f => f.Level == levels[li + 1].Name && f.Boundary.Any(q => q.DistanceTo(p) < Tol.Snap * 4));
                    if (corner || floorCorner || walls.Count > 0 && a.Columns.Count == 0)
                    {
                        var c = find(p) ?? new ColLoc { P = p, B = 220, H = 220 };
                        if (!locs.Contains(c)) locs.Add(c);
                        c.TopLevel = Math.Max(c.TopLevel, li);
                    }
                }
            }
            if (locs.Count == 0) throw new InvalidOperationException("Không xác định được vị trí cột nào từ hồ sơ kiến trúc");
            Report.Info.Add($"Xác định {locs.Count} vị trí cột ({locs.Count(c => c.FromArch)} từ cột kiến trúc)");
            return locs;
        }

        List<P2> GridIntersections(StructModel m)
        {
            var res = new List<P2>();
            for (int i = 0; i < m.Grids.Count; i++)
                for (int j = i + 1; j < m.Grids.Count; j++)
                    if (Seg2.SegIntersect(m.Grids[i].Line, m.Grids[j].Line, out var ip, 500) && !res.Any(q => q.DistanceTo(ip) < Tol.Mm))
                        res.Add(ip);
            return res;
        }

        // ---------------------------------------------------------------- Cột
        void BuildColumns(ArchModel a, StructModel m, List<ColLoc> locs)
        {
            // Stories: [0]=Móng, [1]=tầng KT 0 (nền), [2]=tầng KT 1, ...  Cột từ story k đến k+1.
            foreach (var c in locs)
            {
                int topStory = c.TopLevel + 2; // tầng KT li có tường → cột tới story li+2
                topStory = Math.Min(topStory, m.Stories.Count - 1);
                for (int k = 0; k < topStory; k++)
                {
                    var s0 = m.Stories[k]; var s1 = m.Stories[k + 1];
                    m.Columns.Add(new ColumnElem
                    {
                        Location = c.P, BaseStory = s0.Name, TopStory = s1.Name, BaseZ = s0.Elevation, TopZ = s1.Elevation,
                        RotationDeg = c.Rot, Section = m.GetOrAddSection("C", c.B, c.H).Name,
                    });
                }
            }
        }

        // ---------------------------------------------------------------- Dầm
        void BuildBeams(ArchModel a, StructModel m, List<ColLoc> locs)
        {
            var levels = a.Levels.OrderBy(l => l.Elevation).ToList();
            for (int k = 1; k < m.Stories.Count; k++)
            {
                var st = m.Stories[k];
                bool ground = k == 1;
                if (ground && !opt.TieBeamsAtGround) continue;
                // cột có mặt tại tầng này (đỉnh cột = cao độ tầng)
                var here = m.Columns.Where(c => Math.Abs(c.TopZ - st.Elevation) < 1).Select(c => c.Location).ToList();
                if (here.Count == 0) continue;

                // Dầm khung theo trục giữa 2 cột liên tiếp
                foreach (var g in m.Grids)
                {
                    var on = here.Where(p => g.Line.DistanceTo(p) < Tol.Snap).OrderBy(p => g.Line.Param(p)).ToList();
                    for (int i = 0; i + 1 < on.Count; i++)
                    {
                        double L = on[i].DistanceTo(on[i + 1]);
                        if (L < 600) continue;
                        double h = ground ? Math.Max(300, GeomUtil.RoundUp(L / 15, 50)) : Math.Max(300, GeomUtil.RoundUp(L / 12, 50));
                        double b = h > 600 ? 300 : 220;
                        m.Beams.Add(new BeamElem { Start = on[i], End = on[i + 1], Story = st.Name, TopZ = st.Elevation, Section = m.GetOrAddSection(ground ? "G" : "D", b, h).Name });
                    }
                }

                // Dầm phụ đỡ tường không nằm trên dầm khung (tường đứng trên tầng KT tương ứng)
                if (k - 1 < levels.Count)
                {
                    string lvl = levels[k - 1].Name;
                    var frame = m.Beams.Where(bm => bm.Story == st.Name).ToList();
                    foreach (var w in a.Walls.Where(w => w.BaseLevel == lvl && w.Thickness >= opt.MinWallForSecondaryBeam && w.Line.Length > 1000))
                    {
                        // tường có thể dài qua nhiều nhịp: xét từng đoạn, không chỉ hai đầu tường
                        bool onFrame = WallOnBeams(w.Line, frame);
                        if (onFrame) continue;
                        if (ground) continue; // tường tầng 1 đặt trực tiếp lên giằng/nền
                        // kéo dài tới dầm vuông góc gần nhất ở hai đầu
                        var p0 = ExtendTo(w.Line.A, w.Line.A - w.Line.B, frame);
                        var p1 = ExtendTo(w.Line.B, w.Line.B - w.Line.A, frame);
                        if (p0 == null || p1 == null) { Report.Warnings.Add($"Tường {w.Id} tầng {lvl} không tìm được dầm đỡ hai đầu — tải tường quy về sàn"); continue; }
                        double L = p0.Value.DistanceTo(p1.Value);
                        double h = Math.Max(250, GeomUtil.RoundUp(L / 15, 50));
                        m.Beams.Add(new BeamElem { Start = p0.Value, End = p1.Value, Story = st.Name, TopZ = st.Elevation, IsSecondary = true, Section = m.GetOrAddSection("DP", 220, h).Name });
                    }
                }
            }
        }

        static bool WallOnBeams(Seg2 wall, List<BeamElem> beams)
        {
            int n = Math.Max(2, (int)Math.Ceiling(wall.Length / 250));
            for (int i = 0; i <= n; i++)
            {
                var p = wall.A + (wall.B - wall.A) * ((double)i / n);
                if (!beams.Any(bm => Math.Abs((bm.End - bm.Start).Unit.Dot(wall.Dir)) > 0.95 && new Seg2(bm.Start, bm.End).DistanceTo(p) < Tol.Snap * 2)) return false;
            }
            return true;
        }

        static P2? ExtendTo(P2 from, P2 dirVec, List<BeamElem> beams)
        {
            var dir = dirVec.Unit;
            P2? best = null; double bd = double.MaxValue;
            foreach (var b in beams)
            {
                var s = new Seg2(b.Start, b.End);
                if (Math.Abs(s.Dir.Dot(dir)) > 0.3) continue;
                if (!Seg2.LineIntersect(new Seg2(from, from + dir), s, out var ip)) continue;
                if (s.DistanceTo(ip) > Tol.Mm) continue;
                double t = (ip - from).Dot(dir);
                if (t < -Tol.Snap || t > 3000) continue;
                if (Math.Abs(t) < bd) { bd = Math.Abs(t); best = ip; }
            }
            return best;
        }

        // ---------------------------------------------------------------- Sàn
        void BuildSlabs(ArchModel a, StructModel m)
        {
            var levels = a.Levels.OrderBy(l => l.Elevation).ToList();
            var xs = m.Grids.Where(g => g.Line.IsVertical(2)).Select(g => g.Line.Mid.X).OrderBy(v => v).ToList();
            var ys = m.Grids.Where(g => g.Line.IsHorizontal(2)).Select(g => g.Line.Mid.Y).OrderBy(v => v).ToList();
            bool orthogonal = xs.Count >= 2 && ys.Count >= 2 && xs.Count + ys.Count == m.Grids.Count;
            int sNo = 0;
            for (int k = 2; k < m.Stories.Count; k++)
            {
                var st = m.Stories[k];
                var lvl = levels[k - 1];
                var floors = a.Floors.Where(f => f.Level == lvl.Name).ToList();
                var beams = m.Beams.Where(b => b.Story == st.Name).ToList();
                if (beams.Count == 0) continue;
                var panels = new List<List<P2>>();
                if (orthogonal)
                {
                    for (int i = 0; i + 1 < xs.Count; i++)
                        for (int j = 0; j + 1 < ys.Count; j++)
                        {
                            var ctr = new P2((xs[i] + xs[i + 1]) / 2, (ys[j] + ys[j + 1]) / 2);
                            bool has = floors.Count == 0 ? CellClosedByBeams(beams, xs[i], xs[i + 1], ys[j], ys[j + 1]) : floors.Any(f => GeomUtil.PointInPolygon(ctr, f.Boundary));
                            if (!has) continue;
                            // bị ô cầu thang chiếm → bỏ
                            if (a.Stairs.Any(s => GeomUtil.PointInPolygon(ctr, s.Boundary))) { Report.Info.Add($"Bỏ ô sàn tại ô cầu thang {st.Name}"); continue; }
                            foreach (var cell in SplitBySecondary(beams, xs[i], xs[i + 1], ys[j], ys[j + 1])) panels.Add(cell);
                        }
                }
                else
                {
                    foreach (var f in floors) panels.Add(f.Boundary.ToList());
                    if (floors.Count > 0) Report.Warnings.Add($"Lưới trục không trực giao — sàn {st.Name} lấy theo đường bao kiến trúc, cấu tạo thép sàn chỉ áp dụng ô chữ nhật");
                }
                foreach (var p in panels)
                {
                    var ctr = new P2(p.Average(q => q.X), p.Average(q => q.Y));
                    // phòng chứa tâm ô sàn, hoặc tên phòng (điểm đánh dấu từ DWG) nằm trong ô sàn
                    var room = a.Rooms.FirstOrDefault(r => r.Level == lvl.Name && r.Boundary.Count >= 3 && GeomUtil.PointInPolygon(ctr, r.Boundary))
                               ?? a.Rooms.FirstOrDefault(r => r.Level == lvl.Name && r.Boundary.Count >= 3
                                                             && GeomUtil.PointInPolygon(new P2(r.Boundary.Average(q => q.X), r.Boundary.Average(q => q.Y)), p));
                    string usage = room?.Usage ?? room?.Name ?? (st.IsRoof ? "mái" : "phòng ở");
                    var live = Tcvn2737.LiveLoad(usage, st.IsRoof);
                    bool wet = usage.ToLowerInvariant().Contains("wc") || usage.ToLowerInvariant().Contains("vệ sinh") || usage.ToLowerInvariant().Contains("tắm");
                    double l1 = Math.Min(p[0].DistanceTo(p[1]), p[1].DistanceTo(p[2])), l2 = Math.Max(p[0].DistanceTo(p[1]), p[1].DistanceTo(p[2]));
                    double t = SlabDesigner.PreliminaryThickness(l1, l2, live.q);
                    m.Slabs.Add(new SlabElem
                    {
                        Boundary = p, Story = st.Name, TopZ = st.Elevation, Section = m.GetOrAddSlab(t).Name,
                        LiveLoad = live.q, SuperDead = Tcvn2737.FloorFinish(st.IsRoof, wet), Usage = live.name, Label = $"S{k - 1}-{++sNo}",
                    });
                }
                // đồng nhất chiều dày sàn trong tầng (lấy lớn nhất) — thuận tiện thi công
                var mine = m.Slabs.Where(s => s.Story == st.Name).ToList();
                if (mine.Count > 0)
                {
                    double tMax = mine.Max(s => m.SlabSection(s.Section).Thickness);
                    foreach (var s in mine) s.Section = m.GetOrAddSlab(tMax).Name;
                }
            }
        }

        static bool CellClosedByBeams(List<BeamElem> beams, double x0, double x1, double y0, double y1)
        {
            bool Has(P2 a, P2 b) => beams.Any(bm => new Seg2(bm.Start, bm.End).DistanceTo((a + b) * 0.5) < Tol.Snap);
            return Has(new P2(x0, y0), new P2(x1, y0)) && Has(new P2(x0, y1), new P2(x1, y1)) && Has(new P2(x0, y0), new P2(x0, y1)) && Has(new P2(x1, y0), new P2(x1, y1));
        }

        /// <summary>Chia ô lưới theo dầm phụ cắt ngang hết ô (song song trục).</summary>
        static IEnumerable<List<P2>> SplitBySecondary(List<BeamElem> beams, double x0, double x1, double y0, double y1)
        {
            var cutsX = beams.Where(b => b.IsSecondary && Math.Abs(b.Start.X - b.End.X) < Tol.Mm && b.Start.X > x0 + 300 && b.Start.X < x1 - 300
                                         && Math.Min(b.Start.Y, b.End.Y) <= y0 + Tol.Snap && Math.Max(b.Start.Y, b.End.Y) >= y1 - Tol.Snap)
                             .Select(b => b.Start.X).Distinct().OrderBy(v => v).ToList();
            var cutsY = beams.Where(b => b.IsSecondary && Math.Abs(b.Start.Y - b.End.Y) < Tol.Mm && b.Start.Y > y0 + 300 && b.Start.Y < y1 - 300
                                         && Math.Min(b.Start.X, b.End.X) <= x0 + Tol.Snap && Math.Max(b.Start.X, b.End.X) >= x1 - Tol.Snap)
                             .Select(b => b.Start.Y).Distinct().OrderBy(v => v).ToList();
            var X = new List<double> { x0 }; X.AddRange(cutsX); X.Add(x1);
            var Y = new List<double> { y0 }; Y.AddRange(cutsY); Y.Add(y1);
            for (int i = 0; i + 1 < X.Count; i++)
                for (int j = 0; j + 1 < Y.Count; j++)
                    yield return new List<P2> { new P2(X[i], Y[j]), new P2(X[i + 1], Y[j]), new P2(X[i + 1], Y[j + 1]), new P2(X[i], Y[j + 1]) };
        }

        // ---------------------------------------------------------------- Chọn tiết diện cột sơ bộ
        void SizeColumns(StructModel m)
        {
            var rb = ConcreteGrade.Get(set.Concrete).Rb;
            foreach (var grp in m.Columns.GroupBy(c => (Math.Round(c.Location.X), Math.Round(c.Location.Y))))
            {
                var stack = grp.OrderBy(c => c.BaseZ).ToList();
                var loc = stack[0].Location;
                // diện tích chịu tải: 1/4 các ô sàn kề cột
                double At = 0;
                foreach (var st in m.Stories)
                {
                    double a = m.Slabs.Where(s => s.Story == st.Name && s.Boundary.Any(p => p.DistanceTo(loc) < Tol.Snap))
                                      .Sum(s => Math.Abs(GeomUtil.Area(s.Boundary)) / 4) / 1e6;
                    At = Math.Max(At, a);
                }
                if (At <= 0) At = 9;
                int nAdj = m.Slabs.Count(s => s.Boundary.Any(p => p.DistanceTo(loc) < Tol.Snap));
                double k = nAdj >= 3 ? 1.2 : (nAdj == 2 ? 1.3 : 1.5); // cột giữa / biên / góc
                for (int i = 0; i < stack.Count; i++)
                {
                    int floorsAbove = stack.Count - i;
                    double N = opt.PreliminaryFloorLoad * At * floorsAbove; // kN
                    double A = k * N * 1e3 / rb;                             // mm²
                    var cur = m.Section(stack[i].Section);
                    double b = opt.ColumnFitsWall ? Math.Max(220, cur.B) : Math.Max(220, GeomUtil.RoundUp(Math.Sqrt(A), 50));
                    double h = Math.Max(Math.Max(220, cur.H), GeomUtil.RoundUp(A / b, 50));
                    if (h > 3 * b) { b = GeomUtil.RoundUp(Math.Sqrt(A), 50); h = b; }
                    stack[i].Section = m.GetOrAddSection("C", b, h).Name;
                }
            }
            // sau khi đổi tiết diện, cập nhật thứ tự để cột dưới không nhỏ hơn cột trên
            foreach (var grp in m.Columns.GroupBy(c => (Math.Round(c.Location.X), Math.Round(c.Location.Y))))
            {
                var stack = grp.OrderByDescending(c => c.BaseZ).ToList();
                double b = 0, h = 0;
                foreach (var c in stack)
                {
                    var s = m.Section(c.Section);
                    b = Math.Max(b, s.B); h = Math.Max(h, s.H);
                    c.Section = m.GetOrAddSection("C", b, h).Name;
                }
            }
            m.Sections.RemoveAll(s => s.Kind == SectionKind.Column && !m.Columns.Any(c => c.Section == s.Name));
        }

        // ---------------------------------------------------------------- Móng
        void BuildFootings(StructModel m)
        {
            var baseCols = m.Columns.Where(c => c.BaseStory == m.Stories[0].Name).ToList();
            int i = 0;
            foreach (var c in baseCols)
            {
                m.Footings.Add(new FootingElem { Center = c.Location, ColumnId = c.Id, B = 1200, L = 1200, H = 400, BottomZ = c.BaseZ - 400, Label = $"M{++i}" });
            }
        }

        // ---------------------------------------------------------------- Tải trọng & tổ hợp
        /// <summary>Gán tải trọng (sàn, tường) và tổ hợp. Dùng được cho cả mô hình đọc từ Revit kết cấu (khớp tầng theo tên).</summary>
        public void BuildLoads(ArchModel a, StructModel m)
        {
            m.LoadPatterns.Clear(); m.AreaLoads.Clear(); m.LineLoads.Clear(); m.Combos.Clear();
            m.LoadPatterns.Add(new LoadPattern { Name = "TT", Type = LoadType.Dead, SelfWeightMultiplier = 1 });
            m.LoadPatterns.Add(new LoadPattern { Name = "HTHT", Type = LoadType.SuperDead });
            m.LoadPatterns.Add(new LoadPattern { Name = "HT", Type = LoadType.Live });
            m.LoadPatterns.Add(new LoadPattern { Name = "HTM", Type = LoadType.LiveRoof });
            bool wind = set.WindW0 > 0;
            if (wind)
                foreach (var w in new[] { "GX", "GXX", "GY", "GYY" }) m.LoadPatterns.Add(new LoadPattern { Name = w, Type = LoadType.Wind });

            foreach (var s in m.Slabs)
            {
                m.AreaLoads.Add(new AreaLoad { SlabId = s.Id, Pattern = "HTHT", Value = s.SuperDead });
                var st = m.StoryByName(s.Story);
                m.AreaLoads.Add(new AreaLoad { SlabId = s.Id, Pattern = st.IsRoof ? "HTM" : "HT", Value = s.LiveLoad });
            }

            // Tường xây → tải phân bố trên dầm
            var levels = a.Levels.OrderBy(l => l.Elevation).ToList();
            foreach (var w in a.Walls)
            {
                var st = m.StoryByName(w.BaseLevel);
                if (st == null)
                {
                    int li = levels.FindIndex(l => l.Name == w.BaseLevel);
                    if (li < 0 || li + 1 >= m.Stories.Count) continue;
                    st = m.Stories[li + 1];
                }
                var beams = m.Beams.Where(b => b.Story == st.Name).ToList();
                // mọi dầm nằm dưới tường đều nhận tải, kể cả khi tường dài qua nhiều nhịp
                var on = beams.Where(b => w.Line.DistanceTo((b.Start + b.End) * 0.5) < Math.Max(Tol.Snap * 2, w.Thickness)
                                     && Math.Abs((b.End - b.Start).Unit.Dot(w.Line.Dir)) > 0.95).ToList();
                double opening = a.Openings.Where(o => o.HostWallId == w.Id).Sum(o => o.Width * o.Height) / Math.Max(1, w.Line.Length * w.Height);
                double gamma = Tcvn2737.WallGamma(w.Material);
                if (on.Count > 0)
                {
                    foreach (var b in on)
                    {
                        double hb = m.Section(b.Section).H;
                        double q = Tcvn2737.WallLineLoad(w.Thickness, Math.Max(0, w.Height - hb), gamma, opening);
                        m.LineLoads.Add(new LineLoad { BeamId = b.Id, Pattern = "HTHT", Value = Math.Round(q, 2), Note = $"Tường {w.Thickness:0} cao {w.Height:0}" });
                    }
                }
                else
                {
                    var slab = m.Slabs.FirstOrDefault(s => s.Story == st.Name && GeomUtil.PointInPolygon(w.Line.Mid, s.Boundary));
                    if (slab != null)
                    {
                        double W = Tcvn2737.WallLineLoad(w.Thickness, w.Height, gamma, opening) * w.Line.Length / 1000; // kN
                        double A = Math.Abs(GeomUtil.Area(slab.Boundary)) / 1e6;
                        m.AreaLoads.Add(new AreaLoad { SlabId = slab.Id, Pattern = "HTHT", Value = Math.Round(W / A, 2) });
                    }
                }
            }

            // Tổ hợp theo TCVN 2737:2023 (hệ số γf đưa vào tổ hợp)
            double gl = 1.2; // γf hoạt tải đại diện (q ≥ 2: 1.2; q < 2: 1.3) — lấy theo ô sàn chiếm đa số
            if (m.Slabs.Count > 0 && m.Slabs.Average(s => s.LiveLoad) < 2) gl = 1.3;
            void Combo(string name, params (string, double)[] items) { var c = new LoadCombo { Name = name }; c.Items.AddRange(items); m.Combos.Add(c); }
            Combo("TH1", ("TT", Tcvn2737.DeadFactor), ("HTHT", Tcvn2737.SuperDeadFactor), ("HT", gl), ("HTM", 1.3));
            if (wind)
            {
                int n = 2;
                foreach (var w in new[] { "GX", "GXX", "GY", "GYY" })
                {
                    Combo($"TH{n++}", ("TT", Tcvn2737.DeadFactor), ("HTHT", Tcvn2737.SuperDeadFactor), (w, Tcvn2737.WindFactor));
                    Combo($"TH{n++}", ("TT", Tcvn2737.DeadFactor), ("HTHT", Tcvn2737.SuperDeadFactor), ("HT", gl * Tcvn2737.Psi2), ("HTM", 1.3 * Tcvn2737.Psi2), (w, Tcvn2737.WindFactor * Tcvn2737.Psi2));
                }
            }
            if (set.SeismicAgR > 0)
            {
                Combo("THDD1", ("TT", 1.0), ("HTHT", 1.0), ("HT", 0.3), ("EX", 1.0), ("EY", 0.3));
                Combo("THDD2", ("TT", 1.0), ("HTHT", 1.0), ("HT", 0.3), ("EX", 0.3), ("EY", 1.0));
            }
            var env = new LoadCombo { Name = "BAO", IsEnvelope = true };
            foreach (var c in m.Combos.ToList()) env.Items.Add((c.Name, 1.0));
            m.Combos.Add(env);
            var sls = new LoadCombo { Name = "TC", IsServiceability = true };
            sls.Items.AddRange(new[] { ("TT", 1.0), ("HTHT", 1.0), ("HT", 1.0), ("HTM", 1.0) });
            m.Combos.Add(sls);
        }
    }
}
