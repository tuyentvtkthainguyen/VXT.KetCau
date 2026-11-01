using KetCauVXT.Loads;

namespace KetCauVXT.Model;

/// <summary>Các công cụ tạo mô hình nhanh.</summary>
public static class ModelBuilder
{
    /// <summary>Dự án trống có sẵn vật liệu, tiết diện thông dụng nhà dân dụng và các trường hợp tải mặc định.</summary>
    public static Project NewProject(string name = "Nhà ở dân dụng")
    {
        var p = new Project { Name = name };
        foreach (var c in new[] { "B20", "B25", "B30" }) p.Materials.Add(Material.Concrete(c));
        foreach (var r in new[] { "CB240-T", "CB300-V", "CB400-V" }) p.Materials.Add(Material.Rebar(r));
        foreach (var s in new[] { "SS400", "Q345B" }) p.Materials.Add(Material.Steel(s));

        p.FrameSections.Add(FrameSection.Rect("C22x22", 220, 220));
        p.FrameSections.Add(FrameSection.Rect("C22x30", 220, 300));
        p.FrameSections.Add(FrameSection.Rect("C25x35", 250, 350));
        p.FrameSections.Add(FrameSection.Rect("C30x30", 300, 300));
        p.FrameSections.Add(FrameSection.Rect("D22x30", 220, 300));
        p.FrameSections.Add(FrameSection.Rect("D22x40", 220, 400));
        p.FrameSections.Add(FrameSection.Rect("D22x50", 220, 500));
        p.FrameSections.Add(FrameSection.Rect("D11x30", 110, 300));
        p.SlabSections.Add(new SlabSection { Name = "S100", Thickness = 0.10 });
        p.SlabSections.Add(new SlabSection { Name = "S120", Thickness = 0.12 });

        // Trường hợp tải mặc định – γf theo TCVN 2737:2023 (người dùng điều chỉnh theo loại vật liệu/hoạt tải)
        p.Patterns.Add(new LoadPattern { Name = "TT", Type = LoadType.Dead, SelfWeight = 1, Gamma = 1.1 });
        p.Patterns.Add(new LoadPattern { Name = "HTHT", Type = LoadType.SuperDead, Gamma = 1.2 });
        p.Patterns.Add(new LoadPattern { Name = "HT", Type = LoadType.Live, Gamma = 1.2 });
        p.Patterns.Add(new LoadPattern { Name = "GX", Type = LoadType.Wind, Gamma = 1.2 });
        p.Patterns.Add(new LoadPattern { Name = "GY", Type = LoadType.Wind, Gamma = 1.2 });
        return p;
    }

    /// <summary>Thông số tạo khung BTCT từ lưới trục.</summary>
    public class GridFrameOptions
    {
        public string ColumnSection = "C22x22";
        public string BeamSectionX = "D22x30";
        public string BeamSectionY = "D22x30";
        public string SlabSection = "S100";
        /// <summary>Tải hoàn thiện sàn (lát, trát, trần, M&E), kN/m²</summary>
        public double FinishLoad = 1.2;
        /// <summary>Hoạt tải sàn, kN/m² (TCVN 2737:2023: nhà ở 1.5)</summary>
        public double LiveLoad = 1.5;
        /// <summary>Hoạt tải mái, kN/m²</summary>
        public double RoofLiveLoad = 0.75;
        /// <summary>Tải tường trên dầm biên, kN/m (0 = không gán). Tường 220 gạch đặc cao 3 m ≈ 0.22·18·(3−0.3)·1.0 ≈ 10.7</summary>
        public double PerimeterWallLoad = 0;
        public bool FixedBase = true;
    }

    /// <summary>Sinh khung BTCT: cột tại mọi giao điểm trục, dầm theo mọi đường trục, sàn trong mọi ô lưới, tải sàn.</summary>
    public static void BuildFromGrid(Project p, GridFrameOptions o)
    {
        var xs = p.GridsX.ToList();
        var ys = p.GridsY.ToList();
        var stories = p.Stories.OrderBy(s => s.Elevation).ToList();
        if (xs.Count < 1 || ys.Count < 1 || stories.Count == 0)
            throw new InvalidOperationException("Cần khai báo trục X, trục Y và tầng trước khi sinh khung");

        // nút chân cột
        foreach (var gx in xs)
            foreach (var gy in ys)
            {
                var n = p.GetOrAddNode(gx.Coord, gy.Coord, p.BaseElevation, "Base");
                n.Restraint = o.FixedBase ? Node.Fixed : Node.Pinned;
            }
        double prevZ = p.BaseElevation;
        for (int si = 0; si < stories.Count; si++)
        {
            var st = stories[si];
            bool roof = si == stories.Count - 1;
            // cột
            int ci = 1;
            foreach (var gx in xs)
                foreach (var gy in ys)
                {
                    var a = p.GetOrAddNode(gx.Coord, gy.Coord, prevZ);
                    var b = p.GetOrAddNode(gx.Coord, gy.Coord, st.Elevation, st.Name);
                    p.Frames.Add(new FrameElement
                    {
                        Id = p.NextFrameId(), NodeI = a.Id, NodeJ = b.Id, Kind = FrameKind.Column,
                        Section = o.ColumnSection, Story = st.Name, Label = $"C{ci++}",
                        DesignGroup = $"C-{gx.Name}{gy.Name}"
                    });
                }
            // dầm theo phương X (dọc các trục Y)
            int bi = 1;
            foreach (var gy in ys)
                for (int i = 0; i + 1 < xs.Count; i++)
                    AddBeam(p, xs[i].Coord, gy.Coord, xs[i + 1].Coord, gy.Coord, st, o.BeamSectionX, $"D{st.Name}-{gy.Name}", ref bi);
            foreach (var gx in xs)
                for (int j = 0; j + 1 < ys.Count; j++)
                    AddBeam(p, gx.Coord, ys[j].Coord, gx.Coord, ys[j + 1].Coord, st, o.BeamSectionY, $"D{st.Name}-{gx.Name}", ref bi);

            // sàn
            int sl = 1;
            for (int i = 0; i + 1 < xs.Count; i++)
                for (int j = 0; j + 1 < ys.Count; j++)
                {
                    var n1 = p.GetOrAddNode(xs[i].Coord, ys[j].Coord, st.Elevation);
                    var n2 = p.GetOrAddNode(xs[i + 1].Coord, ys[j].Coord, st.Elevation);
                    var n3 = p.GetOrAddNode(xs[i + 1].Coord, ys[j + 1].Coord, st.Elevation);
                    var n4 = p.GetOrAddNode(xs[i].Coord, ys[j + 1].Coord, st.Elevation);
                    var slab = new Slab
                    {
                        Id = p.NextSlabId(), Label = $"S{st.Name}-{sl++}", Story = st.Name, Section = o.SlabSection,
                        Nodes = new List<int> { n1.Id, n2.Id, n3.Id, n4.Id }
                    };
                    p.Slabs.Add(slab);
                    if (o.FinishLoad > 0) p.AreaLoads.Add(new AreaLoad { Slab = slab.Id, Pattern = "HTHT", Q = o.FinishLoad, Note = "Hoàn thiện" });
                    double live = roof ? o.RoofLiveLoad : o.LiveLoad;
                    if (live > 0) p.AreaLoads.Add(new AreaLoad { Slab = slab.Id, Pattern = "HT", Q = live, Note = roof ? "Hoạt tải mái" : "Hoạt tải sàn" });
                }

            // tường biên (không đặt ở mái)
            if (o.PerimeterWallLoad > 0 && !roof)
            {
                double x0 = xs[0].Coord, x1 = xs[^1].Coord, y0 = ys[0].Coord, y1 = ys[^1].Coord;
                foreach (var f in p.Frames.Where(f_ => f_.Story == st.Name && f_.Kind == FrameKind.Beam))
                {
                    var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
                    bool edge = (Math.Abs(a.X - x0) < 1e-3 && Math.Abs(b.X - x0) < 1e-3) || (Math.Abs(a.X - x1) < 1e-3 && Math.Abs(b.X - x1) < 1e-3)
                             || (Math.Abs(a.Y - y0) < 1e-3 && Math.Abs(b.Y - y0) < 1e-3) || (Math.Abs(a.Y - y1) < 1e-3 && Math.Abs(b.Y - y1) < 1e-3);
                    if (edge) p.FrameLoads.Add(FrameLoad.Uniform(f.Id, "HTHT", o.PerimeterWallLoad, LoadDir.Gravity, "Tường biên"));
                }
            }
            prevZ = st.Elevation;
        }
        ComboGenerator.Generate(p);
        p.InvalidateIndex();
    }

    private static void AddBeam(Project p, double x1, double y1, double x2, double y2, Story st, string sec, string group, ref int idx)
    {
        var a = p.GetOrAddNode(x1, y1, st.Elevation, st.Name);
        var b = p.GetOrAddNode(x2, y2, st.Elevation, st.Name);
        if (p.Frames.Any(f => (f.NodeI == a.Id && f.NodeJ == b.Id) || (f.NodeI == b.Id && f.NodeJ == a.Id))) return;
        p.Frames.Add(new FrameElement
        {
            Id = p.NextFrameId(), NodeI = a.Id, NodeJ = b.Id, Kind = FrameKind.Beam, Section = sec,
            Story = st.Name, Label = $"D{idx++}", DesignGroup = group
        });
    }

    /// <summary>Tạo lưới trục từ danh sách bước cột (m). Trục X đặt tên 1,2,3...; trục Y đặt tên A,B,C...</summary>
    public static void SetGrid(Project p, IEnumerable<double> spansX, IEnumerable<double> spansY)
    {
        p.Grids.Clear();
        double x = 0; int i = 1;
        p.Grids.Add(new GridLine { Name = "1", Dir = GridDir.X, Coord = 0 });
        foreach (var s in spansX) { x += s; p.Grids.Add(new GridLine { Name = (++i).ToString(), Dir = GridDir.X, Coord = Math.Round(x, 4) }); }
        double y = 0; int j = 0;
        p.Grids.Add(new GridLine { Name = "A", Dir = GridDir.Y, Coord = 0 });
        foreach (var s in spansY) { y += s; j++; p.Grids.Add(new GridLine { Name = ((char)('A' + j)).ToString(), Dir = GridDir.Y, Coord = Math.Round(y, 4) }); }
    }

    /// <summary>Tạo tầng từ danh sách chiều cao tầng (m), tầng 1 tính từ cao độ chân cột.</summary>
    public static void SetStories(Project p, IEnumerable<double> heights, double baseElevation = 0)
    {
        p.Stories.Clear();
        p.BaseElevation = baseElevation;
        double z = baseElevation; int k = 1;
        var hs = heights.ToList();
        for (int i = 0; i < hs.Count; i++)
        {
            z += hs[i];
            string name = i == hs.Count - 1 && hs.Count > 1 ? "MAI" : $"T{++k}";
            p.Stories.Add(new Story { Name = name, Elevation = Math.Round(z, 4), Height = hs[i] });
        }
    }

    // ==================================================================
    // KHUNG THÉP TIỀN CHẾ (Zamil)
    // ==================================================================
    public class PortalOptions
    {
        /// <summary>Nhịp khung (m)</summary>
        public double Span = 24;
        /// <summary>Chiều cao đỉnh cột (m)</summary>
        public double EaveHeight = 7;
        /// <summary>Độ dốc mái (%)</summary>
        public double SlopePercent = 15;
        /// <summary>Số bước khung</summary>
        public int Bays = 6;
        /// <summary>Bước khung (m)</summary>
        public double BaySpacing = 6;
        public string ColumnSection = "COT-ZAMIL";
        public string RafterSection = "KEO-ZAMIL";
        /// <summary>Tĩnh tải mái (tôn + xà gồ + giằng), kN/m²</summary>
        public double RoofDead = 0.15;
        /// <summary>Hoạt tải mái, kN/m² (mái tôn không sử dụng 0.3)</summary>
        public double RoofLive = 0.30;
        /// <summary>Hệ số khí động gió ngang nhà: cột đón gió, cột khuất gió, mái đón gió, mái khuất gió</summary>
        public double CeColWind = 0.8, CeColLee = -0.5, CeRoofWind = -0.6, CeRoofLee = -0.4;
        /// <summary>Khoảng cách giằng ngoài mặt phẳng (xà gồ/giằng chéo) cho kèo và cột, m</summary>
        public double BracingSpacing = 1.5;
        public bool PinnedBase = true;
    }

    /// <summary>Sinh các khung zamil phẳng (mặt phẳng XZ) đặt tại y = i·bước khung, tải mái dồn thành tải phân bố trên kèo.
    /// Phân tích phẳng: các nút được khoá UY, RX, RZ.</summary>
    public static void BuildPortalFrames(Project p, PortalOptions o)
    {
        if (p.FrameSections.All(s => s.Name != o.ColumnSection))
            p.FrameSections.Add(FrameSection.WeldedISection(o.ColumnSection, 300, 600, 200, 10, 6));
        if (p.FrameSections.All(s => s.Name != o.RafterSection))
            p.FrameSections.Add(FrameSection.WeldedISection(o.RafterSection, 600, 350, 180, 8, 5));
        foreach (var pn in new[] { ("TT", LoadType.Dead, 1.0, 1.1), ("HT", LoadType.RoofLive, 0.0, 1.3), ("GX", LoadType.Wind, 0.0, 1.2) })
            if (p.Pattern(pn.Item1) == null)
                p.Patterns.Add(new LoadPattern { Name = pn.Item1, Type = pn.Item2, SelfWeight = pn.Item3, Gamma = pn.Item4 });
        p.UseRigidDiaphragm = false;

        double ridge = o.EaveHeight + o.Span / 2 * o.SlopePercent / 100;
        double z0 = p.BaseElevation;
        int nframes = o.Bays + 1;
        double rafterLen = Math.Sqrt(Math.Pow(o.Span / 2, 2) + Math.Pow(ridge - o.EaveHeight, 2));
        double cosA = (o.Span / 2) / rafterLen;

        for (int k = 0; k < nframes; k++)
        {
            double y = k * o.BaySpacing;
            double trib = (k == 0 || k == nframes - 1) ? o.BaySpacing / 2 : o.BaySpacing;
            var b1 = p.GetOrAddNode(0, y, z0); b1.Restraint = o.PinnedBase ? Node.Pinned : Node.Fixed;
            var b2 = p.GetOrAddNode(o.Span, y, z0); b2.Restraint = o.PinnedBase ? Node.Pinned : Node.Fixed;
            var e1 = p.GetOrAddNode(0, y, z0 + o.EaveHeight);
            var e2 = p.GetOrAddNode(o.Span, y, z0 + o.EaveHeight);
            var rg = p.GetOrAddNode(o.Span / 2, y, z0 + ridge);
            foreach (var n in new[] { b1, b2, e1, e2, rg }) { n.Restraint[1] = true; n.Restraint[3] = true; n.Restraint[5] = true; }

            string g = $"KHUNG-{k + 1}";
            int c1 = AddF(p, b1.Id, e1.Id, FrameKind.SteelColumn, o.ColumnSection, $"CT{k + 1}-T", g, o.BracingSpacing);
            int c2 = AddF(p, b2.Id, e2.Id, FrameKind.SteelColumn, o.ColumnSection, $"CT{k + 1}-P", g, o.BracingSpacing);
            int r1 = AddF(p, e1.Id, rg.Id, FrameKind.Rafter, o.RafterSection, $"K{k + 1}-T", g, o.BracingSpacing);
            // kèo phải vẽ từ đỉnh cột phải lên đỉnh để tiết diện vát đối xứng (đầu to tại đỉnh cột)
            int r2 = AddF(p, e2.Id, rg.Id, FrameKind.Rafter, o.RafterSection, $"K{k + 1}-P", g, o.BracingSpacing);

            // tải mái phân bố theo mặt bằng → quy về chiều dài kèo: q·cosα
            double qd = o.RoofDead * trib * cosA, ql = o.RoofLive * trib * cosA;
            foreach (int r in new[] { r1, r2 })
            {
                p.FrameLoads.Add(FrameLoad.Uniform(r, "TT", qd, LoadDir.Gravity, "Tôn + xà gồ"));
                p.FrameLoads.Add(FrameLoad.Uniform(r, "HT", ql, LoadDir.Gravity, "Hoạt tải mái"));
            }
            // gió ngang nhà (theo +X): áp lực tại cao độ đỉnh cột / đỉnh mái
            double wc = p.Wind.W0 * LateralLoadGenerator.Kz(o.EaveHeight, p.Wind.Terrain) * p.Wind.Factor * trib;
            double wr = p.Wind.W0 * LateralLoadGenerator.Kz(ridge, p.Wind.Terrain) * p.Wind.Factor * trib;
            p.FrameLoads.Add(FrameLoad.Uniform(c1, "GX", o.CeColWind * wc, LoadDir.GlobalX, "Gió đón cột trái"));
            p.FrameLoads.Add(FrameLoad.Uniform(c2, "GX", -o.CeColLee * wc, LoadDir.GlobalX, "Gió hút cột phải"));
            // mái: áp lực vuông góc mặt mái (hút lên với hệ số âm) – trục 2 của kèo hướng lên
            p.FrameLoads.Add(FrameLoad.Uniform(r1, "GX", -o.CeRoofWind * wr, LoadDir.Local2, "Gió mái đón"));
            p.FrameLoads.Add(FrameLoad.Uniform(r2, "GX", -o.CeRoofLee * wr, LoadDir.Local2, "Gió mái khuất"));
        }
        if (p.Stories.Count == 0)
        {
            p.Stories.Add(new Story { Name = "DINHCOT", Elevation = z0 + o.EaveHeight, Height = o.EaveHeight });
            p.Stories.Add(new Story { Name = "DINHMAI", Elevation = z0 + ridge, Height = ridge - o.EaveHeight });
        }
        p.Wind.Enabled = false; // gió đã gán trực tiếp lên khung
        ComboGenerator.Generate(p);
        p.InvalidateIndex();
    }

    private static int AddF(Project p, int i, int j, FrameKind k, string sec, string label, string group, double unbraced)
    {
        int id = p.NextFrameId();
        p.Frames.Add(new FrameElement { Id = id, NodeI = i, NodeJ = j, Kind = k, Section = sec, Label = label, DesignGroup = group, UnbracedLength22 = unbraced });
        return id;
    }

    /// <summary>Chia các thanh tại những nút nằm giữa thanh (vd. dầm phụ gác lên dầm chính) – cần cho phân tích đúng.</summary>
    public static int SplitFramesAtNodes(Project p)
    {
        int count = 0;
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var f in p.Frames.ToList())
            {
                var a = p.Node(f.NodeI).P; var b = p.Node(f.NodeJ).P;
                var u = (b - a); double L = u.Length; u = u.Normalize();
                var mid = p.Nodes.Where(n => n.Id != f.NodeI && n.Id != f.NodeJ)
                    .Select(n => (n, t: (n.P - a).Dot(u)))
                    .Where(x => x.t > 0.01 && x.t < L - 0.01 && ((x.n.P - a) - u * x.t).Length < 0.005)
                    .OrderBy(x => x.t).FirstOrDefault();
                if (mid.n == null) continue;
                var f2 = new FrameElement
                {
                    Id = p.NextFrameId(), NodeI = mid.n.Id, NodeJ = f.NodeJ, Kind = f.Kind, Section = f.Section, Story = f.Story,
                    Label = f.Label, DesignGroup = f.DesignGroup, Angle = f.Angle, ReleaseJ = f.ReleaseJ, UnbracedLength22 = f.UnbracedLength22
                };
                // chia lại tải trên thanh
                double r = mid.t / L;
                foreach (var fl in p.FrameLoads.Where(l => l.Element == f.Id).ToList())
                {
                    var right = SliceLoad(fl, r, 1); right.Element = f2.Id;
                    var left = SliceLoad(fl, 0, r);
                    fl.Points = left.Points; fl.PointLoads = left.PointLoads;
                    p.FrameLoads.Add(right);
                }
                f.NodeJ = mid.n.Id; f.ReleaseJ = new bool[6];
                p.Frames.Add(f2);
                count++; changed = true;
                p.InvalidateIndex();
                break;
            }
        }
        return count;
    }

    private static FrameLoad SliceLoad(FrameLoad fl, double r0, double r1)
    {
        double Val(double r)
        {
            for (int i = 0; i + 1 < fl.Points.Count; i++)
                if (r >= fl.Points[i][0] - 1e-12 && r <= fl.Points[i + 1][0] + 1e-12)
                {
                    double d = fl.Points[i + 1][0] - fl.Points[i][0];
                    return d < 1e-12 ? fl.Points[i][1] : fl.Points[i][1] + (fl.Points[i + 1][1] - fl.Points[i][1]) * (r - fl.Points[i][0]) / d;
                }
            return 0;
        }
        var res = new FrameLoad { Pattern = fl.Pattern, Dir = fl.Dir, Note = fl.Note, Source = fl.Source };
        var rs = fl.Points.Select(x => x[0]).Where(x => x > r0 && x < r1).Prepend(r0).Append(r1).Distinct().OrderBy(x => x);
        double lo = fl.Points.Count > 0 ? fl.Points.Min(x => x[0]) : 0, hi = fl.Points.Count > 0 ? fl.Points.Max(x => x[0]) : 0;
        foreach (var r in rs)
            res.Points.Add(new[] { (r - r0) / (r1 - r0), (r < lo - 1e-9 || r > hi + 1e-9) ? 0 : Val(r) });
        foreach (var pl in fl.PointLoads.Where(x => x[0] >= r0 && x[0] < r1))
            res.PointLoads.Add(new[] { (pl[0] - r0) / (r1 - r0), pl[1] });
        return res;
    }
}
