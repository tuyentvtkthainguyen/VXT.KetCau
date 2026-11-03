using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Analysis;

/// <summary>Phân tích tĩnh tuyến tính khung không gian bằng phương pháp phần tử hữu hạn (phương pháp độ cứng trực tiếp).
/// – Phần tử thanh Timoshenko 12 bậc tự do, giải phóng liên kết đầu thanh bằng khử tĩnh.
/// – Thanh tiết diện thay đổi (kèo/cột khung tiền chế) được chia thành nhiều đoạn lăng trụ.
/// – Dầm cong trên mặt bằng (FrameElement.CurveMidOffset ≠ 0) được chia thành các đoạn thẳng nối các điểm trên cung
///   (nút trung gian nội bộ id âm, không lưu vào dự án). Vị trí S0, S1 của đoạn đo theo chiều dài cung; tải phân bố theo
///   chiều dài cung được quy về dây cung của đoạn với cường độ q·ΔS/c (bảo toàn tổng tải). Nội lực tại mặt cắt được
///   chuyển về hệ trục tiếp tuyến của cung (e1 theo tiếp tuyến) → M3 uốn đứng, T xoắn, liên tục qua các nút trung gian.
/// – Sàn tuyệt đối cứng trong mặt phẳng (diaphragm) bằng ràng buộc nút chủ – nút phụ.
/// – Tải sàn dồn về dầm theo đường phân giác 45°; tải gió/động đất sinh tự động theo tầng.</summary>
public partial class FrameAnalysis   // dao động riêng + phổ phản ứng: FrameAnalysis.Modal.cs
{
    private readonly Project _p;
    private readonly AnalysisResults _res;

    // --- lưới phần tử ---
    private class Sub
    {
        public FrameElement Frame;
        public int NI, NJ;            // khoá nút (id nút dự án, nút trung gian có id âm)
        public double S0, S1, L;      // vị trí đầu/cuối trên thanh gốc (m), L = chiều dài đoạn (dây cung với dầm cong)
        public double Scale = 1.0;    // chiều dài địa phương / chiều dài trên thanh gốc: L/(S1 − S0) (= 1 với thanh thẳng)
        public double[,] R, T, K0, K; // K0 chưa khử, K đã khử giải phóng
        public bool[] RelI, RelJ;
        public SectionProps Sp;
        public Material Mat;
        public List<(int eq, double c)>[] Map = new List<(int, double)>[12];
        // tải theo trường hợp: các đoạn tuyến tính và lực tập trung (địa phương)
        public Dictionary<string, List<double[]>> Segs = new();
        public Dictionary<string, List<double[]>> Pts = new();
        public Dictionary<string, double[]> Pstar = new();
    }

    private readonly List<Sub> _subs = new();
    private readonly Dictionary<int, Vec3> _xyz = new();
    private readonly Dictionary<int, List<(int eq, double c)>[]> _map = new();
    private readonly Dictionary<int, List<Sub>> _framesSubs = new();
    private readonly Dictionary<int, ArcGeometry> _arcs = new();   // dầm cong: hình học cung theo Id thanh
    private int _neq;
    private SkylineMatrix _K;

    public FrameAnalysis(Project p)
    {
        _p = p;
        _res = new AnalysisResults(p);
    }

    /// <summary>Chạy toàn bộ: sinh tải tự động → lập lưới → giải từng trường hợp tải.</summary>
    public static AnalysisResults Run(Project p, IProgress<string> progress = null)
    {
        var a = new FrameAnalysis(p);
        return a.Execute(progress);
    }

    public AnalysisResults Execute(IProgress<string> progress = null)
    {
        _p.InvalidateIndex();
        var err = _p.Validate();
        if (err.Count > 0) throw new InvalidOperationException("Mô hình chưa hợp lệ:\n- " + string.Join("\n- ", err));

        progress?.Report("Dồn tải sàn về dầm...");
        _res.Warnings.AddRange(WallLoadGenerator.Apply(_p));
        _res.Warnings.AddRange(StairLoadGenerator.Apply(_p));   // cầu thang khai báo (Loads/StairLoadGenerator.cs)
        _res.Warnings.AddRange(SlabLoadDistributor.Apply(_p));
        progress?.Report("Sinh tải gió / động đất...");
        _res.Warnings.AddRange(LateralLoadGenerator.Apply(_p, _res.LateralForces));
        _p.InvalidateIndex();

        progress?.Report("Lập lưới phần tử...");
        BuildMesh();
        NumberEquations();
        progress?.Report($"Lắp ghép ma trận độ cứng ({_neq} phương trình)...");
        Assemble();
        _res.Equations = _neq;
        progress?.Report("Phân tích ma trận...");
        _K.Factor();
        ModalBeforeCases(progress);   // [Dao động riêng – FrameAnalysis.Modal.cs]

        foreach (var pat in _p.Patterns)
        {
            progress?.Report("Giải trường hợp " + pat.Name);
            _res.Cases[pat.Name] = SolveCase(pat);
        }
        ModalAfterCases(progress);    // [Phổ phản ứng – FrameAnalysis.Modal.cs]
        progress?.Report("Hoàn tất");
        return _res;
    }

    // ------------------------------------------------------------------
    private void BuildMesh()
    {
        foreach (var n in _p.Nodes) _xyz[n.Id] = n.P;
        int nextInternal = -1;
        foreach (var f in _p.Frames)
        {
            var sec = _p.Section(f.Section);
            var mat = _p.Material(sec.Material);
            var pi = _p.Node(f.NodeI).P;
            var pj = _p.Node(f.NodeJ).P;
            double Lp = (pj - pi).Length;
            int nseg = sec.IsTapered ? Math.Max(2, _p.Steel.TaperSegments) : 1;
            var arc = CurvedBeam.Arc(_p, f);
            if (arc != null)
            {
                _arcs[f.Id] = arc;
                Lp = arc.Length;
                nseg = Math.Max(nseg, CurvedBeam.SegmentCount(f, arc));
            }
            var R = ElementMatrices.LocalAxes(pi, pj, f.Angle);
            var T = ElementMatrices.Transformation(R);
            var list = new List<Sub>();
            int prev = f.NodeI;

            var relI = (bool[])f.ReleaseI.Clone();
            var relJ = (bool[])f.ReleaseJ.Clone();
            for (int k = 0; k < 4; k++) if (relI[k] && relJ[k]) relJ[k] = false; // tránh biến hình P, V2, V3, T

            for (int s = 0; s < nseg; s++)
            {
                double t0 = (double)s / nseg, t1 = (double)(s + 1) / nseg;
                int nj;
                if (s == nseg - 1) nj = f.NodeJ;
                else
                {
                    nj = nextInternal--;
                    _xyz[nj] = arc != null ? arc.PointAt(t1) : pi + (pj - pi) * t1;
                }
                var sp = sec.Props((t0 + t1) / 2);
                var sub = new Sub
                {
                    Frame = f, NI = prev, NJ = nj, S0 = t0 * Lp, S1 = t1 * Lp, L = Lp / nseg,
                    R = R, T = T, Sp = sp, Mat = mat,
                    RelI = s == 0 ? relI : new bool[6],
                    RelJ = s == nseg - 1 ? relJ : new bool[6]
                };
                if (arc != null)
                {
                    // đoạn thẳng nối hai điểm trên cung: hệ trục riêng theo dây cung của đoạn
                    var qa = arc.PointAt(t0); var qb = arc.PointAt(t1);
                    sub.L = (qb - qa).Length;
                    sub.Scale = sub.L / Math.Max(sub.S1 - sub.S0, 1e-12);
                    sub.R = ElementMatrices.LocalAxes(qa, qb, f.Angle);
                    sub.T = ElementMatrices.Transformation(sub.R);
                }
                sub.K0 = ElementMatrices.LocalStiffness(sub.L, mat.E, mat.G,
                    sp.A * sec.ModA, sp.As2, sp.As3, sp.I33 * sec.ModI33, sp.I22 * sec.ModI22, sp.J * sec.ModJ);
                sub.K = (double[,])sub.K0.Clone();
                ElementMatrices.Condense(sub.K, null, sub.RelI, sub.RelJ);
                list.Add(sub);
                _subs.Add(sub);
                prev = nj;
            }
            _framesSubs[f.Id] = list;
        }

        // tải trên thanh → tải địa phương trên từng đoạn
        foreach (var pat in _p.Patterns)
        {
            foreach (var sub in _subs) { sub.Segs[pat.Name] = new List<double[]>(); sub.Pts[pat.Name] = new List<double[]>(); }
            if (pat.SelfWeight != 0)
                foreach (var sub in _subs)
                {
                    double q = pat.SelfWeight * sub.Mat.UnitWeight * sub.Sp.A / sub.Scale;   // dầm cong: theo chiều dài cung
                    var d = DirLocal(sub, LoadDir.Gravity);
                    sub.Segs[pat.Name].Add(new[] { 0, sub.L, q * d[0], q * d[1], q * d[2], q * d[0], q * d[1], q * d[2] });
                }
        }
        foreach (var fl in _p.FrameLoads)
        {
            if (!_framesSubs.TryGetValue(fl.Element, out var subs)) continue;
            if (_p.Pattern(fl.Pattern) == null) continue;
            double Lp = subs[^1].S1;
            // các đoạn tuyến tính của tải (vị trí theo chiều dài thanh gốc – theo cung với dầm cong)
            for (int i = 0; i + 1 < fl.Points.Count; i++)
            {
                double xa = fl.Points[i][0] * Lp, xb = fl.Points[i + 1][0] * Lp;
                double qa = fl.Points[i][1], qb = fl.Points[i + 1][1];
                if (xb - xa <= 1e-9) continue;
                foreach (var sub in subs)
                {
                    double a = Math.Max(xa, sub.S0), b = Math.Min(xb, sub.S1);
                    if (b - a <= 1e-9) continue;
                    double va = (qa + (qb - qa) * (a - xa) / (xb - xa)) / sub.Scale;
                    double vb = (qa + (qb - qa) * (b - xa) / (xb - xa)) / sub.Scale;
                    var dir = DirLocal(sub, fl.Dir);
                    sub.Segs[fl.Pattern].Add(new[]
                    {
                        (a - sub.S0) * sub.Scale, (b - sub.S0) * sub.Scale,
                        va * dir[0], va * dir[1], va * dir[2],
                        vb * dir[0], vb * dir[1], vb * dir[2]
                    });
                }
            }
            foreach (var pl in fl.PointLoads)
            {
                double x = pl[0] * Lp;
                var sub = subs.FirstOrDefault(s => x >= s.S0 - 1e-9 && x <= s.S1 + 1e-9) ?? subs[^1];
                var dir = DirLocal(sub, fl.Dir);
                sub.Pts[fl.Pattern].Add(new[] { (x - sub.S0) * sub.Scale, pl[1] * dir[0], pl[1] * dir[1], pl[1] * dir[2] });
            }
        }
        // tải nút tương đương đã khử giải phóng
        foreach (var sub in _subs)
            foreach (var pat in _p.Patterns)
            {
                var pe = ElementMatrices.EquivalentLoads(sub.L, sub.Segs[pat.Name], sub.Pts[pat.Name]);
                var k = (double[,])sub.K0.Clone();
                ElementMatrices.Condense(k, pe, sub.RelI, sub.RelJ);
                sub.Pstar[pat.Name] = pe;
            }
    }

    /// <summary>Véc tơ đơn vị phương tải biểu diễn trong hệ địa phương của đoạn.</summary>
    private static double[] DirLocal(Sub s, LoadDir dir)
    {
        double[] g = dir switch
        {
            LoadDir.Gravity => new double[] { 0, 0, -1 },
            LoadDir.GlobalX => new double[] { 1, 0, 0 },
            LoadDir.GlobalY => new double[] { 0, 1, 0 },
            LoadDir.GlobalZ => new double[] { 0, 0, 1 },
            _ => null
        };
        if (g == null)
            return dir switch
            {
                LoadDir.Local1 => new double[] { 1, 0, 0 },
                LoadDir.Local2 => new double[] { 0, 1, 0 },
                _ => new double[] { 0, 0, 1 }
            };
        return new[]
        {
            s.R[0, 0] * g[0] + s.R[0, 1] * g[1] + s.R[0, 2] * g[2],
            s.R[1, 0] * g[0] + s.R[1, 1] * g[1] + s.R[1, 2] * g[2],
            s.R[2, 0] * g[0] + s.R[2, 1] * g[1] + s.R[2, 2] * g[2]
        };
    }

    // ------------------------------------------------------------------
    private void NumberEquations()
    {
        var used = new HashSet<int>();
        foreach (var s in _subs) { used.Add(s.NI); used.Add(s.NJ); }

        // nhóm diaphragm: nút góc sàn tại cao độ tầng, không bị giữ ngang
        var slave = new Dictionary<int, int>(); // node → id nhóm
        var groups = new List<(double z, double xc, double yc, List<int> nodes)>();
        if (_p.UseRigidDiaphragm && _p.Slabs.Count > 0)
        {
            var slabNodes = new HashSet<int>(_p.Slabs.SelectMany(s => s.Nodes));
            foreach (var g in slabNodes.Where(used.Contains).Select(id => _p.Node(id))
                         .Where(n => !n.Restraint[0] && !n.Restraint[1] && !n.Restraint[5])
                         .GroupBy(n => Math.Round(n.Z, 3)))
            {
                var nodes = g.Select(n => n.Id).ToList();
                if (nodes.Count < 2) continue;
                double xc = g.Average(n => n.X), yc = g.Average(n => n.Y);
                foreach (var id in nodes) slave[id] = groups.Count;
                groups.Add((g.Key, xc, yc, nodes));
            }
        }

        // sắp xếp nút theo cao độ để giảm profile; chèn phương trình nút chủ sau nút cuối của mỗi tầng
        var order = used.OrderBy(id => _xyz[id].Z).ThenBy(id => _xyz[id].X).ThenBy(id => _xyz[id].Y).ToList();
        var lastOfGroup = new Dictionary<int, int>();
        foreach (var id in order) if (slave.TryGetValue(id, out var gi)) lastOfGroup[gi] = id;
        var master = new int[groups.Count, 3];

        int eq = 0;
        var own = new Dictionary<int, int[]>();
        foreach (var id in order)
        {
            var node = id > 0 ? _p.Node(id) : null;
            var e = new int[6];
            bool isSlave = slave.ContainsKey(id);
            for (int d = 0; d < 6; d++)
            {
                bool restrained = node != null && node.Restraint[d];
                bool viaMaster = isSlave && (d == 0 || d == 1 || d == 5);
                e[d] = restrained || viaMaster ? -1 : eq++;
            }
            own[id] = e;
            if (isSlave && lastOfGroup[slave[id]] == id)
            {
                int g = slave[id];
                master[g, 0] = eq++; master[g, 1] = eq++; master[g, 2] = eq++;
            }
        }
        _neq = eq;

        foreach (var id in order)
        {
            var m = new List<(int, double)>[6];
            for (int d = 0; d < 6; d++) m[d] = new List<(int, double)>();
            var e = own[id];
            for (int d = 0; d < 6; d++) if (e[d] >= 0) m[d].Add((e[d], 1.0));
            if (slave.TryGetValue(id, out var g))
            {
                var grp = groups[g];
                double dx = _xyz[id].X - grp.xc, dy = _xyz[id].Y - grp.yc;
                m[0].Add((master[g, 0], 1.0)); m[0].Add((master[g, 2], -dy));
                m[1].Add((master[g, 1], 1.0)); m[1].Add((master[g, 2], dx));
                m[5].Add((master[g, 2], 1.0));
            }
            _map[id] = m;
        }
        foreach (var s in _subs)
            for (int d = 0; d < 6; d++)
            {
                s.Map[d] = _map[s.NI][d];
                s.Map[6 + d] = _map[s.NJ][d];
            }
        _dGroups = groups; _dMaster = master;   // [Dao động riêng – FrameAnalysis.Modal.cs]
        if (groups.Count > 0)
            _res.Warnings.Add($"Đã gán sàn cứng (diaphragm) cho {groups.Count} cao trình sàn.");
    }

    private void Assemble()
    {
        var first = Enumerable.Range(0, _neq).ToArray();
        foreach (var s in _subs)
        {
            int mn = int.MaxValue;
            var eqs = new List<int>();
            foreach (var l in s.Map) foreach (var (e, _) in l) { eqs.Add(e); mn = Math.Min(mn, e); }
            foreach (var e in eqs) if (mn < first[e]) first[e] = mn;
        }
        _K = new SkylineMatrix(_neq, first);
        foreach (var s in _subs)
        {
            var kg = Dense.Mul(Dense.Transpose(s.T), Dense.Mul(s.K, s.T));
            for (int a = 0; a < 12; a++)
                foreach (var (ea, ca) in s.Map[a])
                    for (int b = 0; b < 12; b++)
                    {
                        double v = kg[a, b];
                        if (v == 0) continue;
                        foreach (var (eb, cb) in s.Map[b])
                            if (ea <= eb) _K.Add(ea, eb, ca * cb * v);
                    }
        }
        // bậc tự do không có độ cứng (nút treo, xoay tự do ở nút khớp) → lò xo rất nhỏ, cảnh báo
        double maxd = 0;
        for (int i = 0; i < _neq; i++) maxd = Math.Max(maxd, _K.Get(i, i));
        int weak = 0;
        for (int i = 0; i < _neq; i++)
            if (_K.Get(i, i) <= maxd * 1e-12) { _K.Add(i, i, Math.Max(maxd, 1) * 1e-9); weak++; }
        if (weak > 0)
            _res.Warnings.Add($"{weak} bậc tự do không có độ cứng (thường là xoay tại nút khớp) – đã gán lò xo rất nhỏ.");
    }

    // ------------------------------------------------------------------
    private CaseResult SolveCase(LoadPattern pat)
    {
        var F = new double[_neq];
        var applied = new Dictionary<int, double[]>();
        foreach (var nl in _p.NodeLoads.Where(l => l.Pattern == pat.Name))
        {
            if (!_map.TryGetValue(nl.Node, out var m)) continue;
            double[] v = { nl.Fx, nl.Fy, nl.Fz, nl.Mx, nl.My, nl.Mz };
            if (!applied.TryGetValue(nl.Node, out var acc)) applied[nl.Node] = acc = new double[6];
            for (int d = 0; d < 6; d++)
            {
                acc[d] += v[d];
                foreach (var (e, c) in m[d]) F[e] += c * v[d];
            }
        }
        foreach (var s in _subs)
        {
            var pg = Dense.MulT(s.T, s.Pstar[pat.Name]);
            for (int a = 0; a < 12; a++)
                if (pg[a] != 0)
                    foreach (var (e, c) in s.Map[a]) F[e] += c * pg[a];
        }
        _K.Solve(F);
        return Recover(F, pat.Name, applied);
    }

    /// <summary>Từ véc tơ chuyển vị theo phương trình U → chuyển vị nút, phản lực, nội lực mặt cắt.
    /// pat: tên trường hợp có tải trên thanh (Segs/Pts/Pstar của các đoạn) – dạng dao động dùng tên rỗng ModalKey.</summary>
    private CaseResult Recover(double[] F, string patName, Dictionary<int, double[]> applied)
    {
        var res = new CaseResult { Name = patName };
        foreach (var kv in _map)
        {
            var u = new double[6];
            for (int d = 0; d < 6; d++) foreach (var (e, c) in kv.Value[d]) u[d] += c * F[e];
            if (kv.Key > 0) res.Disp[kv.Key] = u;
        }
        var internalDisp = new Dictionary<int, double[]>();
        foreach (var kv in _map)
            if (kv.Key < 0)
            {
                var u = new double[6];
                for (int d = 0; d < 6; d++) foreach (var (e, c) in kv.Value[d]) u[d] += c * F[e];
                internalDisp[kv.Key] = u;
            }
        double[] DispOf(int id) => id > 0 ? res.Disp[id] : internalDisp[id];

        // nội lực đầu đoạn và phản lực
        var endForces = new Dictionary<Sub, double[]>();
        var reac = new Dictionary<int, double[]>();
        foreach (var s in _subs)
        {
            var dg = new double[12];
            Array.Copy(DispOf(s.NI), 0, dg, 0, 6);
            Array.Copy(DispOf(s.NJ), 0, dg, 6, 6);
            var dl = Dense.Mul(s.T, dg);
            var f = Dense.Mul(s.K, dl);
            var ps = s.Pstar[patName];
            for (int i = 0; i < 12; i++) f[i] -= ps[i];
            endForces[s] = f;
            var fg = Dense.MulT(s.T, f);
            foreach (var (nid, off) in new[] { (s.NI, 0), (s.NJ, 6) })
            {
                if (nid <= 0) continue;
                var node = _p.Node(nid);
                if (!node.HasRestraint) continue;
                if (!reac.TryGetValue(nid, out var r)) reac[nid] = r = new double[6];
                for (int d = 0; d < 6; d++) r[d] += fg[off + d];
            }
        }
        foreach (var kv in reac)
        {
            var node = _p.Node(kv.Key);
            applied.TryGetValue(kv.Key, out var ap);
            var r = new double[6];
            for (int d = 0; d < 6; d++)
                r[d] = node.Restraint[d] ? kv.Value[d] - (ap?[d] ?? 0) : 0;
            res.Reactions[kv.Key] = r;
        }

        // nội lực tại các mặt cắt
        foreach (var f in _p.Frames)
        {
            var subs = _framesSubs[f.Id];
            double Lp = subs[^1].S1;
            var st = new SortedSet<double>();
            int nseg = Math.Max(4, (int)Math.Ceiling(Lp / 0.5 - 1e-9));
            for (int i = 0; i <= nseg; i++) st.Add(Math.Round(Lp * i / nseg, 6));
            foreach (var s in subs) { st.Add(Math.Round(s.S0, 6)); st.Add(Math.Round(s.S1, 6)); }
            foreach (var s in subs) foreach (var pt in s.Pts[patName]) st.Add(Math.Round(s.S0 + pt[0] / s.Scale, 6));
            var stations = st.ToArray();
            var ff = new FrameForces { Stations = stations, F = new double[stations.Length][] };
            for (int i = 0; i < stations.Length; i++)
            {
                double x = stations[i];
                var s = subs.FirstOrDefault(q => x <= q.S1 + 1e-9) ?? subs[^1];
                ff.F[i] = SectionForce(s, endForces[s], (x - s.S0) * s.Scale, patName);
                if (_arcs.TryGetValue(f.Id, out var arc)) ff.F[i] = ToArcAxes(s, ff.F[i], arc, x / Math.Max(Lp, 1e-12));
            }
            res.Frames[f.Id] = ff;
        }
        return res;
    }

    /// <summary>Dầm cong: chuyển nội lực (P, V2, V3, T, M2, M3) từ hệ trục dây cung của đoạn sang hệ trục tiếp tuyến của cung
    /// tại tham số t (cùng góc xoay tiết diện). Ở nút trung gian hai đoạn kề nhau cho cùng giá trị (cân bằng nút).</summary>
    private static double[] ToArcAxes(Sub s, double[] f, ArcGeometry arc, double t)
    {
        var rt = CurvedBeam.AxesAt(arc, t, s.Frame.Angle);
        var r = new double[6];
        for (int blk = 0; blk < 2; blk++)
        {
            int o = 3 * blk;
            // véc tơ tổng thể g = Rsubᵀ·f ; địa phương mới = Rt·g
            var g = new double[3];
            for (int j = 0; j < 3; j++) g[j] = s.R[0, j] * f[o] + s.R[1, j] * f[o + 1] + s.R[2, j] * f[o + 2];
            for (int i = 0; i < 3; i++) r[o + i] = rt[i, 0] * g[0] + rt[i, 1] * g[1] + rt[i, 2] * g[2];
        }
        return r;
    }

    /// <summary>Nội lực trên mặt dương tại vị trí x (tính từ đầu đoạn): cân bằng phần thanh [0, x].</summary>
    private static double[] SectionForce(Sub s, double[] fe, double x, string pat)
    {
        double F1 = -fe[0], F2 = -fe[1], F3 = -fe[2];
        double M1 = -fe[3];
        double M2 = -(fe[4] + x * fe[2]);
        double M3 = -(fe[5] - x * fe[1]);
        foreach (var sg in s.Segs[pat])
        {
            double xa = sg[0], xb = Math.Min(sg[1], x);
            if (xb <= xa + 1e-12) continue;
            double t = (xb - sg[0]) / (sg[1] - sg[0]);
            double q1e = sg[2] + t * (sg[5] - sg[2]);
            double q2e = sg[3] + t * (sg[6] - sg[3]);
            double q3e = sg[4] + t * (sg[7] - sg[4]);
            double len = xb - xa;
            F1 -= (sg[2] + q1e) / 2 * len;
            F2 -= (sg[3] + q2e) / 2 * len;
            F3 -= (sg[4] + q3e) / 2 * len;
            // ∫ (s − x) q ds bằng Simpson (chính xác với hàm bậc 2)
            double xm = (xa + xb) / 2;
            double q2m = (sg[3] + q2e) / 2, q3m = (sg[4] + q3e) / 2;
            double i2 = len / 6 * ((xa - x) * sg[3] + 4 * (xm - x) * q2m + (xb - x) * q2e);
            double i3 = len / 6 * ((xa - x) * sg[4] + 4 * (xm - x) * q3m + (xb - x) * q3e);
            M2 -= -i3;
            M3 -= i2;
        }
        foreach (var pt in s.Pts[pat])
        {
            if (pt[0] >= x - 1e-9) continue;
            F1 -= pt[1]; F2 -= pt[2]; F3 -= pt[3];
            M2 -= -(pt[0] - x) * pt[3];
            M3 -= (pt[0] - x) * pt[2];
        }
        return new[] { F1, F2, F3, M1, M2, M3 };
    }
}
