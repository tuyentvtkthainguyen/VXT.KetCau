using System.Globalization;
using System.Text;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Export;

/// <summary>Xuất mô hình sang file văn bản SAP2000 (.s2k / .$2k) để kiểm tra chéo nội lực.
/// Mô hình xuất ra GIỐNG HỆT mô hình tính của chương trình:
/// – Thanh tiết diện thay đổi được chia thành các đoạn lăng trụ như khi phân tích.
/// – Tải sàn xuất dưới dạng tải phân bố trên dầm (đã dồn theo phân giác 45°), không xuất phần tử sàn.
/// – Sàn cứng thể hiện bằng ràng buộc Diaphragm cho các nút sàn mỗi cao trình.
/// – Tải gió/động đất xuất dưới dạng tải nút đã sinh.
/// Mở trong SAP2000: File > Import > SAP2000 .s2k Text File.
/// Tên thanh trong SAP2000 = Id thanh của chương trình (đoạn vát: Id_1, Id_2...).
/// – Dầm cong xuất thành các đoạn thẳng Id_1…Id_n với nút trung gian Id_k trên cung (giống mô hình tính);
///   tải phân bố theo cung quy về đoạn với cường độ q·ΔS/c.</summary>
public static class S2kExporter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v) => v.ToString("0.######", Inv);
    private static string Q(string s) => s.Contains(' ') || s.Contains('-') ? $"\"{s}\"" : s;

    public static void Export(Project p, string path)
    {
        // đảm bảo tải sàn / tải ngang đã được sinh như khi phân tích
        WallLoadGenerator.Apply(p);
        SlabLoadDistributor.Apply(p);
        LateralLoadGenerator.Apply(p, null);
        p.InvalidateIndex();

        var sb = new StringBuilder();
        void L(string s) => sb.Append(s).Append("\r\n");
        void T(string name) { L(""); L($"TABLE:  \"{name}\""); }

        L($"File {Path.GetFileName(path)} – xuất từ Việt Const ngày {DateTime.Now:dd/MM/yyyy HH:mm}");
        T("PROGRAM CONTROL");
        L("   ProgramName=SAP2000   Version=22.0.0   CurrUnits=\"KN, m, C\"   SteelCode=AISC360-16   ConcCode=\"ACI 318-14\"   RegenHinge=Yes");

        // ---------------- vật liệu ----------------
        var usedMats = p.FrameSections.Select(s => s.Material).Concat(p.SlabSections.Select(s => s.Material)).Distinct().ToList();
        T("MATERIAL PROPERTIES 01 - GENERAL");
        foreach (var mn in usedMats)
        {
            var m = p.Material(mn);
            string type = m.Kind == MaterialKind.Concrete ? "Concrete" : "Steel";
            L($"   Material={Q(m.Name)}   Type={type}   SymType=Isotropic   TempDepend=No   Color=Gray8Dark");
        }
        T("MATERIAL PROPERTIES 02 - BASIC MECHANICAL PROPERTIES");
        foreach (var mn in usedMats)
        {
            var m = p.Material(mn);
            L($"   Material={Q(m.Name)}   UnitWeight={F(m.UnitWeight)}   UnitMass={F(m.UnitWeight / U.g)}   E1={F(m.E)}   G12={F(m.G)}   U12={F(m.Poisson)}   A1={F(m.Alpha)}");
        }
        T("MATERIAL PROPERTIES 03A - STEEL DATA");
        foreach (var mn in usedMats.Where(n => p.Material(n).Kind != MaterialKind.Concrete))
        {
            var m = p.Material(mn);
            L($"   Material={Q(m.Name)}   Fy={F(m.Fy)}   Fu={F(m.Fu > 0 ? m.Fu : m.Fy * 1.4)}   EffFy={F(m.Fy)}   EffFu={F(m.Fu > 0 ? m.Fu : m.Fy * 1.4)}   SSCurveOpt=Simple   SSHysType=Kinematic");
        }
        T("MATERIAL PROPERTIES 03B - CONCRETE DATA");
        foreach (var mn in usedMats.Where(n => p.Material(n).Kind == MaterialKind.Concrete))
        {
            var m = p.Material(mn);
            L($"   Material={Q(m.Name)}   Fc={F(m.Rbn > 0 ? m.Rbn : m.Rb * 1.3)}   LtWtConc=No   SSCurveOpt=Mander   SSHysType=Takeda");
        }

        // ---------------- tiết diện ----------------
        // tiết diện lăng trụ cho từng đoạn của thanh vát
        var segSections = new Dictionary<(int frame, int seg), string>();
        var secLines = new List<string>();
        foreach (var s in p.FrameSections.Where(s_ => !s_.IsTapered))
            secLines.Add(SectionLine(s.Name, s, s.Props()));
        foreach (var f in p.Frames)
        {
            var s = p.Section(f.Section);
            if (!s.IsTapered) continue;
            int n = Math.Max(2, p.Steel.TaperSegments);
            for (int k = 0; k < n; k++)
            {
                double t = (k + 0.5) / n;
                double h = s.HStart + (s.HEnd - s.HStart) * t;
                string name = $"{s.Name}_h{h * 1000:0}";
                segSections[(f.Id, k)] = name;
                if (secLines.Any(l => l.Contains($"SectionName={Q(name)} "))) continue;
                secLines.Add(SectionLine(name, s, s.Props(t), h));
            }
        }
        T("FRAME SECTION PROPERTIES 01 - GENERAL");
        foreach (var l in secLines) L(l);

        // ---------------- nút ----------------
        T("JOINT COORDINATES");
        foreach (var n in p.Nodes)
            L($"   Joint={n.Id}   CoordSys=GLOBAL   CoordType=Cartesian   XorR={F(n.X)}   Y={F(n.Y)}   Z={F(n.Z)}   SpecialJt=No   GlobalX={F(n.X)}   GlobalY={F(n.Y)}   GlobalZ={F(n.Z)}");
        // nút trung gian của thanh vát
        var extraJoints = new Dictionary<(int, int), string>();
        // dầm cong: đoạn thẳng + nút trung gian trên cung
        var curved = new Dictionary<int, List<CurveSegment>>();
        foreach (var f in p.Frames)
        {
            if (!f.IsCurved) continue;
            var cs = CurvedBeam.Segments(p, f);
            if (cs == null) continue;
            curved[f.Id] = cs;
            for (int k = 1; k < cs.Count; k++)
            {
                var q = cs[k].A;
                string name = $"{f.Id}_{k}";
                extraJoints[(f.Id, k)] = name;
                L($"   Joint={name}   CoordSys=GLOBAL   CoordType=Cartesian   XorR={F(q.X)}   Y={F(q.Y)}   Z={F(q.Z)}   SpecialJt=No   GlobalX={F(q.X)}   GlobalY={F(q.Y)}   GlobalZ={F(q.Z)}");
            }
        }
        foreach (var f in p.Frames)
        {
            var s = p.Section(f.Section);
            if (!s.IsTapered || curved.ContainsKey(f.Id)) continue;
            int n = Math.Max(2, p.Steel.TaperSegments);
            var a = p.Node(f.NodeI).P; var b = p.Node(f.NodeJ).P;
            for (int k = 1; k < n; k++)
            {
                var q = a + (b - a) * ((double)k / n);
                string name = $"{f.Id}_{k}";
                extraJoints[(f.Id, k)] = name;
                L($"   Joint={name}   CoordSys=GLOBAL   CoordType=Cartesian   XorR={F(q.X)}   Y={F(q.Y)}   Z={F(q.Z)}   SpecialJt=No   GlobalX={F(q.X)}   GlobalY={F(q.Y)}   GlobalZ={F(q.Z)}");
            }
        }

        // ---------------- thanh ----------------
        var segs = new List<(FrameElement f, string name, string ji, string jj, string sec, int k, int n)>();
        foreach (var f in p.Frames)
        {
            var s = p.Section(f.Section);
            if (curved.TryGetValue(f.Id, out var cseg))
            {
                int nc = cseg.Count;
                for (int k = 0; k < nc; k++)
                {
                    string ji = k == 0 ? f.NodeI.ToString() : extraJoints[(f.Id, k)];
                    string jj = k == nc - 1 ? f.NodeJ.ToString() : extraJoints[(f.Id, k + 1)];
                    string sname = s.IsTapered && segSections.TryGetValue((f.Id, Math.Min(k * Math.Max(2, p.Steel.TaperSegments) / nc, Math.Max(2, p.Steel.TaperSegments) - 1)), out var tsn) ? tsn : s.Name;
                    segs.Add((f, $"{f.Id}_{k + 1}", ji, jj, sname, k, nc));
                }
                continue;
            }
            if (!s.IsTapered) { segs.Add((f, f.Id.ToString(), f.NodeI.ToString(), f.NodeJ.ToString(), s.Name, 0, 1)); continue; }
            int n = Math.Max(2, p.Steel.TaperSegments);
            for (int k = 0; k < n; k++)
            {
                string ji = k == 0 ? f.NodeI.ToString() : extraJoints[(f.Id, k)];
                string jj = k == n - 1 ? f.NodeJ.ToString() : extraJoints[(f.Id, k + 1)];
                segs.Add((f, $"{f.Id}_{k + 1}", ji, jj, segSections[(f.Id, k)], k, n));
            }
        }
        T("CONNECTIVITY - FRAME");
        foreach (var sg in segs) L($"   Frame={sg.name}   JointI={sg.ji}   JointJ={sg.jj}   IsCurved=No");
        T("FRAME SECTION ASSIGNMENTS");
        foreach (var sg in segs)
        {
            var s = p.Section(sg.f.Section);
            L($"   Frame={sg.name}   SectionType={ShapeName(s)}   AutoSelect=N.A.   AnalSect={Q(sg.sec)}   MatProp=Default");
        }
        if (p.Frames.Any(f => Math.Abs(f.Angle) > 1e-9))
        {
            T("FRAME LOCAL AXES ASSIGNMENTS 1 - TYPICAL");
            foreach (var sg in segs.Where(x => Math.Abs(x.f.Angle) > 1e-9)) L($"   Frame={sg.name}   Angle={F(sg.f.Angle)}");
        }
        var rel = segs.Where(x => (x.k == 0 && x.f.ReleaseI.Any(b => b)) || (x.k == x.n - 1 && x.f.ReleaseJ.Any(b => b))).ToList();
        if (rel.Count > 0)
        {
            T("FRAME RELEASE ASSIGNMENTS 1 - GENERAL");
            string YN(bool b) => b ? "Yes" : "No";
            foreach (var sg in rel)
            {
                var ri = sg.k == 0 ? sg.f.ReleaseI : new bool[6];
                var rj = sg.k == sg.n - 1 ? sg.f.ReleaseJ : new bool[6];
                L($"   Frame={sg.name}   PI={YN(ri[0])}   V2I={YN(ri[1])}   V3I={YN(ri[2])}   TI={YN(ri[3])}   M2I={YN(ri[4])}   M3I={YN(ri[5])}   PJ={YN(rj[0])}   V2J={YN(rj[1])}   V3J={YN(rj[2])}   TJ={YN(rj[3])}   M2J={YN(rj[4])}   M3J={YN(rj[5])}   PartialFix=No");
            }
        }
        var mods = p.FrameSections.Where(s => s.ModI33 != 1 || s.ModI22 != 1 || s.ModJ != 1 || s.ModA != 1).Select(s => s.Name).ToHashSet();
        if (mods.Count > 0)
        {
            T("FRAME PROPERTY MODIFIERS");
            foreach (var sg in segs.Where(x => mods.Contains(x.f.Section)))
            {
                var s = p.Section(sg.f.Section);
                L($"   Frame={sg.name}   AMod={F(s.ModA)}   A2Mod=1   A3Mod=1   JMod={F(s.ModJ)}   I2Mod={F(s.ModI22)}   I3Mod={F(s.ModI33)}   MMod=1   WMod=1");
            }
        }

        // ---------------- gối ----------------
        T("JOINT RESTRAINT ASSIGNMENTS");
        foreach (var n in p.Nodes.Where(n_ => n_.HasRestraint))
        {
            string Y(int i) => n.Restraint[i] ? "Yes" : "No";
            L($"   Joint={n.Id}   U1={Y(0)}   U2={Y(1)}   U3={Y(2)}   R1={Y(3)}   R2={Y(4)}   R3={Y(5)}");
        }

        // ---------------- sàn cứng ----------------
        var diaph = DiaphragmGroups(p);
        if (diaph.Count > 0)
        {
            T("CONSTRAINT DEFINITIONS - DIAPHRAGM");
            for (int i = 0; i < diaph.Count; i++) L($"   Name=DIAPH{i + 1}   CoordSys=GLOBAL   Axis=Z   MultiLevel=No");
            T("JOINT CONSTRAINT ASSIGNMENTS");
            for (int i = 0; i < diaph.Count; i++)
                foreach (var id in diaph[i]) L($"   Joint={id}   Constraint=DIAPH{i + 1}   Type=Diaphragm");
        }

        // ---------------- tải ----------------
        T("LOAD PATTERN DEFINITIONS");
        foreach (var pat in p.Patterns)
            L($"   LoadPat={Q(pat.Name)}   DesignType={SapType(pat.Type)}   SelfWtMult={F(pat.SelfWeight)}");
        foreach (var ml in MassSourceExport.S2kLines(p, Q)) L(ml);   // [Dao động riêng] nguồn khối lượng – Export/MassSourceExport.cs
        T("LOAD CASE DEFINITIONS");
        foreach (var pat in p.Patterns)
            L($"   Case={Q(pat.Name)}   Type=LinStatic   InitialCond=Zero   DesignType={SapType(pat.Type)}   DesignAct=Non-Composite   AutoType=None   RunCase=Yes");
        T("CASE - STATIC 1 - LOAD ASSIGNMENTS");
        foreach (var pat in p.Patterns)
            L($"   Case={Q(pat.Name)}   LoadType=\"Load pattern\"   LoadName={Q(pat.Name)}   LoadSF=1");

        T("FRAME LOADS - DISTRIBUTED");
        foreach (var fl in p.FrameLoads)
        {
            var f = p.Frame(fl.Element);
            var mySegs = segs.Where(s => s.f.Id == f.Id).ToList();
            double Lp = p.Length(f);
            for (int i = 0; i + 1 < fl.Points.Count; i++)
            {
                double ra = fl.Points[i][0], rb = fl.Points[i + 1][0];
                if (rb - ra < 1e-9) continue;
                double qa = fl.Points[i][1], qb = fl.Points[i + 1][1];
                foreach (var sg in mySegs)
                {
                    double s0 = (double)sg.k / sg.n, s1 = (double)(sg.k + 1) / sg.n;
                    double a = Math.Max(ra, s0), b = Math.Min(rb, s1);
                    if (b - a < 1e-9) continue;
                    double va = qa + (qb - qa) * (a - ra) / (rb - ra), vb = qa + (qb - qa) * (b - ra) / (rb - ra);
                    var (cs, dir, sign) = SapDir(fl.Dir);
                    double r0 = (a - s0) / (s1 - s0), r1 = (b - s0) / (s1 - s0);
                    double lseg = Lp / sg.n;
                    if (curved.TryGetValue(f.Id, out var cl))
                    {
                        // dầm cong: chiều dài đoạn = dây cung, cường độ quy theo ΔS/c
                        lseg = cl[sg.k].Chord;
                        va *= cl[sg.k].LoadScale; vb *= cl[sg.k].LoadScale;
                    }
                    L($"   Frame={sg.name}   LoadPat={Q(fl.Pattern)}   CoordSys={cs}   Type=Force   Dir={dir}   DistType=RelDist   RelDistA={F(r0)}   RelDistB={F(r1)}   AbsDistA={F(r0 * lseg)}   AbsDistB={F(r1 * lseg)}   FOverLA={F(sign * va)}   FOverLB={F(sign * vb)}");
                }
            }
        }
        if (p.FrameLoads.Any(l => l.PointLoads.Count > 0))
        {
            T("FRAME LOADS - POINT");
            foreach (var fl in p.FrameLoads)
                foreach (var pl in fl.PointLoads)
                {
                    var f = p.Frame(fl.Element);
                    var mySegs = segs.Where(s => s.f.Id == f.Id).ToList();
                    var sg = mySegs.First(s => pl[0] <= (double)(s.k + 1) / s.n + 1e-9);
                    double s0 = (double)sg.k / sg.n;
                    double r = (pl[0] - s0) * sg.n;
                    var (cs, dir, sign) = SapDir(fl.Dir);
                    double lsg = curved.TryGetValue(f.Id, out var cl) ? cl[sg.k].Chord : p.Length(f) / sg.n;
                    L($"   Frame={sg.name}   LoadPat={Q(fl.Pattern)}   CoordSys={cs}   Type=Force   Dir={dir}   DistType=RelDist   RelDist={F(r)}   AbsDist={F(r * lsg)}   Force={F(sign * pl[1])}");
                }
        }
        if (p.NodeLoads.Count > 0)
        {
            T("JOINT LOADS - FORCE");
            foreach (var nl in p.NodeLoads)
                L($"   Joint={nl.Node}   LoadPat={Q(nl.Pattern)}   CoordSys=GLOBAL   F1={F(nl.Fx)}   F2={F(nl.Fy)}   F3={F(nl.Fz)}   M1={F(nl.Mx)}   M2={F(nl.My)}   M3={F(nl.Mz)}");
        }

        // ---------------- tổ hợp ----------------
        if (p.Combinations.Count > 0)
        {
            T("COMBINATION DEFINITIONS");
            var patNames = p.Patterns.Select(x => x.Name).ToHashSet();
            foreach (var c in p.Combinations)
            {
                string ctype = c.Type == ComboType.Envelope ? "Envelope" : "\"Linear Add\"";
                bool first = true;
                foreach (var it in c.Items)
                {
                    string caseType = patNames.Contains(it.Case) ? "\"Linear Static\"" : "\"Response Combo\"";
                    if (first)
                        L($"   ComboName={Q(c.Name)}   ComboType={ctype}   AutoDesign=No   CaseType={caseType}   CaseName={Q(it.Case)}   ScaleFactor={F(it.Factor)}   SteelDesign=None   ConcDesign=None   AlumDesign=None   ColdDesign=None");
                    else
                        L($"   ComboName={Q(c.Name)}   CaseType={caseType}   CaseName={Q(it.Case)}   ScaleFactor={F(it.Factor)}");
                    first = false;
                }
            }
        }
        L("");
        L("END TABLE DATA");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string ShapeName(FrameSection s) => s.Shape switch
    {
        SectionShape.RectConcrete => "Rectangular",
        SectionShape.WeldedI => "\"I/Wide Flange\"",
        _ => "General"
    };

    private static string SectionLine(string name, FrameSection s, SectionProps sp, double h = 0)
    {
        if (s.Shape == SectionShape.RectConcrete)
            return $"   SectionName={Q(name)}   Material={Q(s.Material)}   Shape=Rectangular   t3={F(s.H)}   t2={F(s.B)}   Area={F(sp.A)}   TorsConst={F(sp.J)}   I33={F(sp.I33)}   I22={F(sp.I22)}   AS2={F(sp.As2)}   AS3={F(sp.As3)}   Color=Yellow";
        if (s.Shape == SectionShape.WeldedI)
        {
            double hh = h > 0 ? h : s.HStart;
            return $"   SectionName={Q(name)}   Material={Q(s.Material)}   Shape=\"I/Wide Flange\"   t3={F(hh)}   t2={F(s.BfTop)}   tf={F(s.TfTop)}   tw={F(s.Tw)}   t2b={F(s.BfBot)}   tfb={F(s.TfBot)}   Area={F(sp.A)}   TorsConst={F(sp.J)}   I33={F(sp.I33)}   I22={F(sp.I22)}   AS2={F(sp.As2)}   AS3={F(sp.As3)}   Color=Cyan";
        }
        return $"   SectionName={Q(name)}   Material={Q(s.Material)}   Shape=General   t3={F(sp.Depth)}   t2={F(sp.Width)}   Area={F(sp.A)}   TorsConst={F(sp.J)}   I33={F(sp.I33)}   I22={F(sp.I22)}   AS2={F(sp.As2)}   AS3={F(sp.As3)}   S33={F(sp.W33)}   S22={F(sp.W22)}   Color=Green";
    }

    private static string SapType(LoadType t) => t switch
    {
        LoadType.Dead => "Dead",
        LoadType.SuperDead => "\"Super Dead\"",
        LoadType.Live => "Live",
        LoadType.RoofLive => "\"Roof Live\"",
        LoadType.Wind => "Wind",
        LoadType.Seismic => "Quake",
        _ => "Other"
    };

    /// <summary>(Hệ toạ độ, phương, dấu). Trọng lực dương hướng xuống → Dir=Gravity giữ nguyên dấu.</summary>
    private static (string cs, string dir, double sign) SapDir(LoadDir d) => d switch
    {
        LoadDir.Gravity => ("GLOBAL", "Gravity", 1.0),
        LoadDir.GlobalX => ("GLOBAL", "X", 1.0),
        LoadDir.GlobalY => ("GLOBAL", "Y", 1.0),
        LoadDir.GlobalZ => ("GLOBAL", "Z", 1.0),
        LoadDir.Local1 => ("Local", "1", 1.0),
        LoadDir.Local2 => ("Local", "2", 1.0),
        _ => ("Local", "3", 1.0)
    };

    /// <summary>Nhóm nút sàn cứng theo cao trình – cùng quy tắc với bộ phân tích.</summary>
    public static List<List<int>> DiaphragmGroups(Project p)
    {
        var res = new List<List<int>>();
        if (!p.UseRigidDiaphragm || p.Slabs.Count == 0) return res;
        var used = new HashSet<int>(p.Frames.SelectMany(f => new[] { f.NodeI, f.NodeJ }));
        var slabNodes = p.Slabs.SelectMany(s => s.Nodes).Distinct().Where(used.Contains).Select(id => p.Node(id))
            .Where(n => !n.Restraint[0] && !n.Restraint[1] && !n.Restraint[5]);
        foreach (var g in slabNodes.GroupBy(n => Math.Round(n.Z, 3)).OrderBy(g_ => g_.Key))
        {
            var l = g.Select(n => n.Id).ToList();
            if (l.Count >= 2) res.Add(l);
        }
        return res;
    }
}
