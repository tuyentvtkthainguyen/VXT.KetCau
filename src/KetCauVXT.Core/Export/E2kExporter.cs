using System.Globalization;
using System.Text;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Export;

/// <summary>Xuất mô hình sang file văn bản ETABS (.e2k) – cú pháp ETABS 2016 → v22.
/// Mở trong ETABS: File > Import > ETABS .e2k Text File.
/// – Tầng, lưới trục, vật liệu, tiết diện, cột/dầm/sàn theo tầng.
/// – Sàn xuất là phần tử sàn kiểu Membrane kèm tải sàn (để ETABS tự dồn tải về dầm – kiểm tra chéo cả bước phân tải).
///   Đặt slabsAsAreas = false để xuất tải sàn đã dồn sẵn lên dầm (mô hình giống hệt chương trình).
/// – Tên thanh: C{Id} (cột), B{Id} (dầm), D{Id} (thanh xiên) → so sánh nội lực theo Id.
/// – Thanh tiết diện vát xuất bằng tiết diện trung bình; với khung thép tiền chế nên dùng file .s2k (SAP2000).
/// – Dầm cong xuất thành các đoạn thẳng B{Id}_1…B{Id}_n qua các điểm trung gian trên cung – đúng như mô hình tính
///   (tải phân bố theo cung quy về từng đoạn với cường độ q·ΔS/c, giải phóng liên kết chỉ ở đầu I đoạn đầu và đầu J đoạn cuối).</summary>
public static class E2kExporter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v) => v.ToString("0.######", Inv);

    public static string LineName(FrameElement f, Project p)
    {
        var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
        double horiz = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
        if (horiz < 1e-3) return "C" + f.Id;
        if (Math.Abs(b.Z - a.Z) < 1e-3) return "B" + f.Id;
        return "D" + f.Id;
    }

    public static void Export(Project p, string path) => Export(p, path, true);

    public static void Export(Project p, string path, bool slabsAsAreas)
    {
        WallLoadGenerator.Apply(p);
        SlabLoadDistributor.Apply(p);
        LateralLoadGenerator.Apply(p, null);
        p.InvalidateIndex();

        var sb = new StringBuilder();
        void L(string s) => sb.Append(s).Append("\r\n");

        // ----- tầng: gồm mọi cao độ nút -----
        var levels = p.Stories.Select(s => (s.Name, s.Elevation)).ToList();
        foreach (var z in p.Nodes.Select(n => Math.Round(n.Z, 3)).Distinct())
            if (Math.Abs(z - p.BaseElevation) > 1e-3 && levels.All(l => Math.Abs(l.Elevation - z) > 1e-3))
                levels.Add(($"Z{z * 1000:0}", z));
        levels = levels.OrderBy(l => l.Elevation).ToList();
        string StoryOf(double z)
        {
            if (Math.Abs(z - p.BaseElevation) < 1e-3) return "Base";
            return levels.First(l => Math.Abs(l.Elevation - z) < 1e-3).Name;
        }

        L("$ File " + Path.GetFileName(path) + " – xuất từ Việt Const " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        L("");
        L("$ PROGRAM INFORMATION");
        L("  PROGRAM  \"ETABS\"  VERSION \"20.0.0\"");
        L("");
        L("$ CONTROLS");
        L("  UNITS  \"KN\"  \"M\"  \"C\"  ");
        L($"  TITLE1  \"{Clean(p.Name)}\"  ");
        L("  PREFERENCE  MERGETOL 0.001");
        L("");
        L("$ STORIES - IN SEQUENCE FROM TOP");
        for (int i = levels.Count - 1; i >= 0; i--)
        {
            double below = i == 0 ? p.BaseElevation : levels[i - 1].Elevation;
            L($"  STORY \"{levels[i].Name}\"  HEIGHT {F(levels[i].Elevation - below)}  MASTERSTORY \"Yes\"  ");
        }
        L($"  STORY \"Base\"  ELEV {F(p.BaseElevation)}  ");
        L("");
        if (p.Grids.Count > 0)
        {
            L("$ GRIDS");
            L("  GRIDSYSTEM \"G1\"  TYPE \"CARTESIAN\"  BUBBLESIZE 1.25");
            foreach (var g in p.Grids.OrderBy(g_ => g_.Dir).ThenBy(g_ => g_.Coord))
                L($"  GRID \"G1\"  LABEL \"{Clean(g.Name)}\"  DIR \"{(g.Dir == GridDir.X ? "X" : "Y")}\"  COORD {F(g.Coord)}  VISIBLE \"Yes\"  BUBBLELOC \"Default\"  ");
            L("");
        }
        var diaph = S2kExporter.DiaphragmGroups(p);
        if (diaph.Count > 0)
        {
            L("$ DIAPHRAGM NAMES");
            L("  DIAPHRAGM \"D1\"  TYPE RIGID ");
            L("");
        }

        // ----- vật liệu -----
        L("$ MATERIAL PROPERTIES");
        var mats = p.FrameSections.Select(s => s.Material).Concat(p.SlabSections.Select(s => s.Material))
            .Concat(new[] { p.Concrete.RebarMain, p.Concrete.RebarStirrup }).Distinct();
        foreach (var mn in mats)
        {
            var m = p.Material(mn);
            string type = m.Kind switch { MaterialKind.Concrete => "Concrete", MaterialKind.Rebar => "Rebar", _ => "Steel" };
            L($"  MATERIAL  \"{m.Name}\"    TYPE \"{type}\"    WEIGHTPERVOLUME {F(m.UnitWeight)}");
            L($"  MATERIAL  \"{m.Name}\"    SYMTYPE \"Isotropic\"  E {F(m.E)}  U {F(m.Poisson)}  A {F(m.Alpha)}");
            if (m.Kind == MaterialKind.Concrete) L($"  MATERIAL  \"{m.Name}\"    FC {F(m.Rbn > 0 ? m.Rbn : m.Rb * 1.3)}");
            else L($"  MATERIAL  \"{m.Name}\"    FY {F(m.Fy)}  FU {F(m.Fu > 0 ? m.Fu : m.Fy * 1.4)}");
        }
        L("");

        // ----- tiết diện -----
        L("$ FRAME SECTIONS");
        foreach (var s in p.FrameSections)
        {
            var sp = s.Props();
            switch (s.Shape)
            {
                case SectionShape.RectConcrete:
                    L($"  FRAMESECTION  \"{s.Name}\"  MATERIAL \"{s.Material}\"  SHAPE \"Concrete Rectangular\"  D {F(s.H)}  B {F(s.B)}  ");
                    break;
                case SectionShape.WeldedI:
                    L($"  FRAMESECTION  \"{s.Name}\"  MATERIAL \"{s.Material}\"  SHAPE \"Steel I/Wide Flange\"  D {F((s.HStart + s.HEnd) / 2)}  B {F(s.BfTop)}  TF {F(s.TfTop)}  TW {F(s.Tw)}  BB {F(s.BfBot)}  TFB {F(s.TfBot)}  ");
                    break;
                default:
                    L($"  FRAMESECTION  \"{s.Name}\"  MATERIAL \"{s.Material}\"  SHAPE \"General\"  D {F(sp.Depth)}  B {F(sp.Width)}  AREA {F(sp.A)}  AS2 {F(sp.As2)}  AS3 {F(sp.As3)}  I33 {F(sp.I33)}  I22 {F(sp.I22)}  J {F(sp.J)}  S33POS {F(sp.W33)}  S33NEG {F(sp.W33)}  S22POS {F(sp.W22)}  S22NEG {F(sp.W22)}  ");
                    break;
            }
            if (s.ModI33 != 1 || s.ModI22 != 1 || s.ModJ != 1 || s.ModA != 1)
                L($"  FRAMESECTION  \"{s.Name}\"  AMOD {F(s.ModA)}  JMOD {F(s.ModJ)}  I2MOD {F(s.ModI22)}  I3MOD {F(s.ModI33)}  ");
        }
        L("");
        var concSecs = p.FrameSections.Where(s => s.Shape == SectionShape.RectConcrete).ToList();
        if (concSecs.Count > 0)
        {
            L("$ CONCRETE SECTIONS");
            foreach (var s in concSecs)
            {
                bool col = p.Frames.Any(f => f.Section == s.Name && f.Kind == FrameKind.Column);
                if (col)
                    L($"  CONCRETESECTION  \"{s.Name}\"  LONGBARMATERIAL \"{p.Concrete.RebarMain}\"  CONFINEBARMATERIAL \"{p.Concrete.RebarStirrup}\"  TYPE \"Column\"  PATTERN \"Rectangular\"  CONFINEMENT \"Ties\"  COVER {F(p.Concrete.CoverColumn + 0.01)}  NUMBARS3DIR 3  NUMBARS2DIR 3  BARSIZE \"16\"  TIEBARSIZE \"6\"  SPACING 0.15  NUMCBARS2 2  NUMCBARS3 2  REINFORCEMENT \"Design\"  ");
                else
                    L($"  CONCRETESECTION  \"{s.Name}\"  LONGBARMATERIAL \"{p.Concrete.RebarMain}\"  CONFINEBARMATERIAL \"{p.Concrete.RebarStirrup}\"  TYPE \"Beam\"  COVERTOP {F(p.Concrete.CoverBeam + 0.01)}  COVERBOTTOM {F(p.Concrete.CoverBeam + 0.01)}  ");
            }
            L("");
        }
        if (p.SlabSections.Count > 0 && slabsAsAreas)
        {
            L("$ SLAB PROPERTIES");
            foreach (var s in p.SlabSections)
                L($"  SHELLPROP  \"{s.Name}\"  PROPTYPE  \"Slab\"  MATERIAL \"{s.Material}\"  MODELINGTYPE \"Membrane\"  SLABTYPE \"Slab\"  SLABTHICKNESS {F(s.Thickness)}  ");
            L("");
        }

        // ----- điểm (toạ độ mặt bằng) -----
        var plan = new Dictionary<(long, long), string>();
        string PointName(double x, double y)
        {
            var key = ((long)Math.Round(x * 1000), (long)Math.Round(y * 1000));
            if (!plan.TryGetValue(key, out var n)) { n = (plan.Count + 1).ToString(); plan[key] = n; }
            return n;
        }
        foreach (var n in p.Nodes) PointName(n.X, n.Y);
        // dầm cong: các đoạn thẳng và điểm trung gian
        var curved = new Dictionary<int, List<CurveSegment>>();
        foreach (var f in p.Frames)
        {
            if (!f.IsCurved) continue;
            var segs = CurvedBeam.Segments(p, f);
            if (segs == null || LineName(f, p)[0] != 'B') continue;
            curved[f.Id] = segs;
            foreach (var sg in segs) { PointName(sg.A.X, sg.A.Y); PointName(sg.B.X, sg.B.Y); }
        }
        string SegName(FrameElement f, CurveSegment sg) => LineName(f, p) + "_" + (sg.K + 1).ToString(Inv);
        L("$ POINT COORDINATES");
        foreach (var kv in plan) L($"  POINT \"{kv.Value}\"  {F(kv.Key.Item1 / 1000.0)} {F(kv.Key.Item2 / 1000.0)}");
        L("");

        // ----- thanh -----
        L("$ LINE CONNECTIVITIES");
        foreach (var f in p.Frames)
        {
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            string name = LineName(f, p);
            if (name[0] == 'C')
            {
                var top = a.Z > b.Z ? a : b; var bot = a.Z > b.Z ? b : a;
                int span = levels.Count(l => l.Elevation > bot.Z + 1e-3 && l.Elevation <= top.Z + 1e-3);
                L($"  LINE  \"{name}\"  COLUMN  \"{PointName(top.X, top.Y)}\"  \"{PointName(top.X, top.Y)}\"  {span}");
            }
            else if (name[0] == 'B' && curved.TryGetValue(f.Id, out var csegs))
                foreach (var sg in csegs)
                    L($"  LINE  \"{SegName(f, sg)}\"  BEAM  \"{PointName(sg.A.X, sg.A.Y)}\"  \"{PointName(sg.B.X, sg.B.Y)}\"  0");
            else if (name[0] == 'B')
                L($"  LINE  \"{name}\"  BEAM  \"{PointName(a.X, a.Y)}\"  \"{PointName(b.X, b.Y)}\"  0");
            else
            {
                var top = a.Z > b.Z ? a : b; var bot = a.Z > b.Z ? b : a;
                int span = levels.Count(l => l.Elevation > bot.Z + 1e-3 && l.Elevation <= top.Z + 1e-3);
                L($"  LINE  \"{name}\"  BRACE  \"{PointName(top.X, top.Y)}\"  \"{PointName(bot.X, bot.Y)}\"  {span}");
            }
        }
        L("");
        if (slabsAsAreas && p.Slabs.Count > 0)
        {
            L("$ AREA CONNECTIVITIES");
            foreach (var s in p.Slabs)
            {
                var pts = s.Nodes.Select(id => p.Node(id)).Select(n => $"\"{PointName(n.X, n.Y)}\"");
                L($"  AREA \"F{s.Id}\"  FLOOR  {s.Nodes.Count}  {string.Join("  ", pts)}  {string.Join("  ", s.Nodes.Select(_ => "0"))}");
            }
            L("");
        }

        // ----- gán -----
        L("$ POINT ASSIGNS");
        foreach (var n in p.Nodes.Where(n_ => n_.HasRestraint))
        {
            var flags = new List<string>();
            string[] nm = { "UX", "UY", "UZ", "RX", "RY", "RZ" };
            for (int i = 0; i < 6; i++) if (n.Restraint[i]) flags.Add(nm[i]);
            L($"  POINTASSIGN  \"{PointName(n.X, n.Y)}\"  \"{StoryOf(n.Z)}\"  RESTRAINT \"{string.Join(" ", flags)}\"  ");
        }
        if (diaph.Count > 0 && !slabsAsAreas)
            foreach (var g in diaph) foreach (var id in g)
            {
                var n = p.Node(id);
                L($"  POINTASSIGN  \"{PointName(n.X, n.Y)}\"  \"{StoryOf(n.Z)}\"  DIAPH \"D1\"  ");
            }
        L("");
        L("$ LINE ASSIGNS");
        foreach (var f in p.Frames)
        {
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            string name = LineName(f, p);
            string story = StoryOf(Math.Max(a.Z, b.Z));
            var sb2 = new StringBuilder($"  LINEASSIGN  \"{name}\"  \"{story}\"  SECTION \"{f.Section}\"");
            if (Math.Abs(f.Angle) > 1e-9) sb2.Append($"  ANG {F(f.Angle)}");
            var rel = new List<string>();
            string[] rn = { "P", "V2", "V3", "T", "M2", "M3" };
            // ETABS: I là đầu dưới với cột, đầu "point 1" với dầm – giữ nguyên thứ tự vì cột ở trên đã đặt theo đỉnh
            bool flip = name[0] != 'B' && a.Z > b.Z;
            for (int i = 0; i < 6; i++) if ((flip ? f.ReleaseJ : f.ReleaseI)[i]) rel.Add(rn[i] + "I");
            for (int i = 0; i < 6; i++) if ((flip ? f.ReleaseI : f.ReleaseJ)[i]) rel.Add(rn[i] + "J");
            if (rel.Count > 0) sb2.Append($"  RELEASE \"{string.Join(" ", rel)}\"");
            var (colCp, colExact) = f.Kind == FrameKind.Column ? ColumnInsertion.Cardinal(p, f) : (10, true);   // [Trục qua mép cột] Export/ColumnInsertion.cs
            if (!colExact) L($"$ COLUMN {name} OFFSET X={F(f.ColOffsetX)} Y={F(f.ColOffsetY)} (m) IS NOT HALF THE SECTION SIZE - CARDINALPT 10 KEPT, SET INSERTION POINT MANUALLY IN ETABS");
            if (f.Kind == FrameKind.Column || f.Kind == FrameKind.Beam) sb2.Append($"  CARDINALPT {(f.Kind == FrameKind.Beam ? BeamTools.EtabsCardinal(f.Insertion) : colCp)}");   // [BeamTools] điểm chèn dầm
            sb2.Append("  MAXSTASPC 0.5  AUTOMESH \"YES\"  MESHATINTERSECTIONS \"YES\"");
            if (curved.TryGetValue(f.Id, out var csegs))
            {
                // dầm cong: gán cho từng đoạn, giải phóng đầu I ở đoạn đầu, đầu J ở đoạn cuối
                foreach (var sg in csegs)
                {
                    var sb3 = new StringBuilder($"  LINEASSIGN  \"{SegName(f, sg)}\"  \"{story}\"  SECTION \"{f.Section}\"");
                    if (Math.Abs(f.Angle) > 1e-9) sb3.Append($"  ANG {F(f.Angle)}");
                    var relc = new List<string>();
                    if (sg.K == 0) for (int i = 0; i < 6; i++) if (f.ReleaseI[i]) relc.Add(rn[i] + "I");
                    if (sg.K == sg.Count - 1) for (int i = 0; i < 6; i++) if (f.ReleaseJ[i]) relc.Add(rn[i] + "J");
                    if (relc.Count > 0) sb3.Append($"  RELEASE \"{string.Join(" ", relc)}\"");
                    if (f.Kind == FrameKind.Beam) sb3.Append($"  CARDINALPT {BeamTools.EtabsCardinal(f.Insertion)}");   // [BeamTools]
                    sb3.Append("  MAXSTASPC 0.5  AUTOMESH \"YES\"  MESHATINTERSECTIONS \"YES\"");
                    L(sb3.ToString());
                }
                continue;
            }
            L(sb2.ToString());
        }
        L("");
        if (slabsAsAreas && p.Slabs.Count > 0)
        {
            L("$ AREA ASSIGNS");
            foreach (var s in p.Slabs)
            {
                double z = p.Node(s.Nodes[0]).Z;
                string d = diaph.Count > 0 ? "  DIAPH \"D1\"" : "";
                L($"  AREAASSIGN  \"F{s.Id}\"  \"{StoryOf(z)}\"  SECTION \"{s.Section}\"{d}  OBJMESHTYPE \"DEFAULT\"  ");
            }
            L("");
        }

        // ----- tải -----
        L("$ LOAD PATTERNS");
        foreach (var pat in p.Patterns)
            L($"  LOADPATTERN \"{pat.Name}\"  TYPE  \"{EtabsType(pat.Type)}\"  SELFWEIGHT  {F(pat.SelfWeight)}");
        L("");
        var nl = p.NodeLoads.Where(l => slabsAsAreas ? l.Source != SlabLoadDistributor.Source : true).ToList();
        if (nl.Count > 0)
        {
            L("$ POINT OBJECT LOADS");
            foreach (var l in nl)
            {
                var n = p.Node(l.Node);
                var parts = new List<string>();
                if (l.Fx != 0) parts.Add($"FX {F(l.Fx)}");
                if (l.Fy != 0) parts.Add($"FY {F(l.Fy)}");
                if (l.Fz != 0) parts.Add($"FZ {F(l.Fz)}");
                if (l.Mx != 0) parts.Add($"MX {F(l.Mx)}");
                if (l.My != 0) parts.Add($"MY {F(l.My)}");
                if (l.Mz != 0) parts.Add($"MZ {F(l.Mz)}");
                if (parts.Count == 0) continue;
                L($"  POINTLOAD  \"{PointName(n.X, n.Y)}\"  \"{StoryOf(n.Z)}\"  TYPE \"FORCE\"  LC \"{l.Pattern}\"  {string.Join("  ", parts)}");
            }
            L("");
        }
        var fls = p.FrameLoads.Where(l => !(slabsAsAreas && l.Source == SlabLoadDistributor.Source)).ToList();
        if (fls.Count > 0)
        {
            L("$ FRAME OBJECT LOADS");
            foreach (var fl in fls)
            {
                var f = p.Frame(fl.Element);
                var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
                string name = LineName(f, p);
                string story = StoryOf(Math.Max(a.Z, b.Z));
                string dir = EtabsDir(fl.Dir);
                bool flip = name[0] != 'B' && a.Z > b.Z; // ETABS định nghĩa đầu I của cột ở dưới
                if (curved.TryGetValue(f.Id, out var csegs))
                {
                    // dầm cong: tách tải theo từng đoạn thẳng
                    for (int i = 0; i + 1 < fl.Points.Count; i++)
                        foreach (var (sg, ra, rb, qa, qb) in CurvedBeam.SplitLoad(csegs, fl.Points[i][0], fl.Points[i + 1][0], fl.Points[i][1], fl.Points[i + 1][1]))
                            L($"  LINELOAD  \"{SegName(f, sg)}\"  \"{story}\"  TYPE \"TRAPF\"  DIR \"{dir}\"  LC \"{fl.Pattern}\"  FSTART {F(qa)}  FEND {F(qb)}  RDSTART {F(ra)}  RDEND {F(rb)}");
                    foreach (var pl in fl.PointLoads)
                    {
                        var sg = csegs.FirstOrDefault(x => pl[0] <= x.T1 + 1e-9) ?? csegs[^1];
                        double r = Math.Clamp((pl[0] - sg.T0) / (sg.T1 - sg.T0), 0, 1);
                        L($"  LINELOAD  \"{SegName(f, sg)}\"  \"{story}\"  TYPE \"POINTF\"  DIR \"{dir}\"  LC \"{fl.Pattern}\"  FVAL {F(pl[1])}  RDIST {F(r)}");
                    }
                    continue;
                }
                for (int i = 0; i + 1 < fl.Points.Count; i++)
                {
                    double r0 = fl.Points[i][0], r1 = fl.Points[i + 1][0];
                    if (r1 - r0 < 1e-9) continue;
                    double q0 = fl.Points[i][1], q1 = fl.Points[i + 1][1];
                    if (flip) { (r0, r1) = (1 - r1, 1 - r0); (q0, q1) = (q1, q0); }
                    if (Math.Abs(q0 - q1) < 1e-9 && r0 < 1e-9 && r1 > 1 - 1e-9)
                        L($"  LINELOAD  \"{name}\"  \"{story}\"  TYPE \"UNIFF\"  DIR \"{dir}\"  LC \"{fl.Pattern}\"  FVAL {F(q0)}");
                    else
                        L($"  LINELOAD  \"{name}\"  \"{story}\"  TYPE \"TRAPF\"  DIR \"{dir}\"  LC \"{fl.Pattern}\"  FSTART {F(q0)}  FEND {F(q1)}  RDSTART {F(r0)}  RDEND {F(r1)}");
                }
                foreach (var pl in fl.PointLoads)
                {
                    double r = flip ? 1 - pl[0] : pl[0];
                    L($"  LINELOAD  \"{name}\"  \"{story}\"  TYPE \"POINTF\"  DIR \"{dir}\"  LC \"{fl.Pattern}\"  FVAL {F(pl[1])}  RDIST {F(r)}");
                }
            }
            L("");
        }
        if (slabsAsAreas && p.AreaLoads.Count > 0)
        {
            L("$ SHELL OBJECT LOADS");
            foreach (var al in p.AreaLoads)
            {
                var s = p.Slabs.FirstOrDefault(x => x.Id == al.Slab);
                if (s == null) continue;
                L($"  AREALOAD  \"F{s.Id}\"  \"{StoryOf(p.Node(s.Nodes[0]).Z)}\"  TYPE \"UNIFF\"  DIR \"GRAV\"  LC \"{al.Pattern}\"  FVAL {F(al.Q)}");
            }
            L("");
        }
        foreach (var ml in MassSourceExport.E2kLines(p)) L(ml);   // [Dao động riêng] nguồn khối lượng – Export/MassSourceExport.cs
        L("$ LOAD CASES");
        foreach (var pat in p.Patterns)
        {
            L($"  LOADCASE \"{pat.Name}\"  TYPE  \"Linear Static\"  INITCOND  \"PRESET\"  ");
            L($"  LOADCASE \"{pat.Name}\"  LOADPAT  \"{pat.Name}\"  SF  1 ");
        }
        L("");
        if (p.Combinations.Count > 0)
        {
            L("$ LOAD COMBINATIONS");
            var patNames = p.Patterns.Select(x => x.Name).ToHashSet();
            foreach (var c in p.Combinations)
            {
                L($"  COMBO \"{c.Name}\"  TYPE \"{(c.Type == ComboType.Envelope ? "Envelope" : "Linear Add")}\"  ");
                foreach (var it in c.Items)
                    L(patNames.Contains(it.Case)
                        ? $"  COMBO \"{c.Name}\"  LOADCASE \"{it.Case}\"  SF {F(it.Factor)} "
                        : $"  COMBO \"{c.Name}\"  LOADCOMBO \"{it.Case}\"  SF {F(it.Factor)} ");
            }
            L("");
        }
        L("$ END OF MODEL FILE");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Clean(string s) => (s ?? "").Replace("\"", "'");

    private static string EtabsType(LoadType t) => t switch
    {
        LoadType.Dead => "Dead",
        LoadType.SuperDead => "Super Dead",
        LoadType.Live => "Live",
        LoadType.RoofLive => "Roof Live",
        LoadType.Wind => "Wind",
        LoadType.Seismic => "Seismic",
        _ => "Other"
    };

    private static string EtabsDir(LoadDir d) => d switch
    {
        LoadDir.Gravity => "GRAV",
        LoadDir.GlobalX => "X",
        LoadDir.GlobalY => "Y",
        LoadDir.GlobalZ => "Z",
        LoadDir.Local1 => "1",
        LoadDir.Local2 => "2",
        _ => "3"
    };
}
