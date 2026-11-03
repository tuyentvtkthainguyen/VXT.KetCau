using System.Globalization;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Analysis;

/// <summary>PHÂN TÍCH DAO ĐỘNG RIÊNG VÀ PHỔ PHẢN ỨNG (dùng chung lưới phần tử, đánh số phương trình, ma trận độ cứng K đã phân tích LDLᵀ).
///
/// 1. Khối lượng: tập trung tại nút từ "Nguồn khối lượng" (ModalSettings.Effective): trọng lượng bản thân thanh chia đôi về hai đầu
///    đoạn; tải phân bố/tập trung trên thanh theo phương trọng lực quy về hai đầu đoạn theo quy tắc đòn bẩy (bảo toàn tổng và trọng tâm);
///    tải nút Fz &lt; 0. m = W/g (kN → t, g = 9,81 m/s²). Khối lượng tịnh tiến UX, UY (UZ nếu chọn); bỏ quán tính xoay của nút.
///    Với sàn cứng (Project.UseRigidDiaphragm), nút phụ quy về nút chủ qua ma trận ràng buộc nên M nút chủ = Σm, Σm và Σm·r² (đúng
///    bằng "khối lượng tầng + mô men quán tính khối lượng") cùng các số hạng ghép khi nút chủ không trùng tâm khối lượng.
/// 2. Trị riêng K·φ = ω²·M·φ: khử tĩnh (Guyan – chính xác vì các bậc tự do bị khử không có khối lượng) về các bậc tự do có khối lượng,
///    giải đầy đủ bằng Householder + QL khi số bậc tự do động ≤ 800; ngược lại lặp không gian con (Bathe) với K⁻¹ skyline.
/// 3. Hệ số tham gia Γ = φᵀMι, khối lượng hữu hiệu Γ² (φ chuẩn hoá φᵀMφ = 1), tỷ lệ so với tổng khối lượng các bậc tự do tự do.
/// 4. Phổ phản ứng (SeismicSettings.Method = SeismicMethods.ResponseSpectrum): đáp ứng dạng k theo phương d: u = Γkd·Sd(Tk)·g/ωk²·φk;
///    tổ hợp CQC/SRSS cho từng đại lượng (chuyển vị, phản lực, nội lực mặt cắt) → kết quả KHÔNG DẤU thay kết quả tĩnh của trường hợp
///    động đất (DDX/DDY); tổ hợp ±E1 ± 0,3E2 sẵn có cho cặp bao +/− như ETABS. Xoắn ngẫu nhiên: Mi = 0,05·Li·Fi cộng giá trị tuyệt đối.</summary>
public partial class FrameAnalysis
{
    /// <summary>Tên "trường hợp" nội bộ không có tải trên thanh – dùng khôi phục nội lực từ véc tơ chuyển vị (dạng dao động, xoắn).</summary>
    private const string ModalKey = "\u0001MODAL";

    private List<(double z, double xc, double yc, List<int> nodes)> _dGroups;
    private int[,] _dMaster;

    // dữ liệu dao động riêng dùng lại cho phổ phản ứng
    private double[][] _phi;                     // véc tơ riêng theo phương trình (φᵀMφ = 1)
    private Dictionary<int, double> _nodeMass;   // khối lượng tịnh tiến tại nút lưới (t)
    private Dictionary<int, double>[] _mRows;    // M theo phương trình (hàng thưa)

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static bool IsYDir(string name) => name.ToUpperInvariant().Contains('Y');

    private bool UsesResponseSpectrum =>
        _p.Seismic != null && _p.Seismic.Enabled && _p.Seismic.IsResponseSpectrum && _p.Patterns.Any(t => t.Type == LoadType.Seismic);

    // ------------------------------------------------------------------
    private void ModalBeforeCases(IProgress<string> progress)
    {
        var ms = _p.Modal ??= new ModalSettings();
        bool rs = UsesResponseSpectrum;
        bool seisStatic = _p.Seismic != null && _p.Seismic.Enabled && !_p.Seismic.IsResponseSpectrum && _p.Patterns.Any(t => t.Type == LoadType.Seismic);
        bool t1 = seisStatic && ms.UseModalT1;
        if (!ms.Enabled && !rs && !t1) return;
        if (!ms.Enabled) _res.Warnings.Add("Dao động riêng: tự chạy vì phương pháp động đất cần chu kỳ dao động (phổ phản ứng / T1 theo dao động).");
        try
        {
            progress?.Report("Phân tích dao động riêng...");
            RunModal();
        }
        catch (Exception ex)
        {
            _res.Modal = null; _phi = null;
            _res.Warnings.Add("Phân tích dao động riêng không thành công: " + ex.Message);
            if (rs) _res.Warnings.Add("Phổ phản ứng không thực hiện được – trường hợp động đất giữ kết quả tĩnh lực ngang tương đương.");
            return;
        }
        if (t1 && _res.Modal != null && _res.Modal.Modes.Count > 0)
        {
            double tx = _res.Modal.Dominant(0)?.Period ?? 0, ty = _res.Modal.Dominant(1)?.Period ?? 0;
            if (ms.ModalT1Cap > 0 && _res.Modal.T1Formula > 0)
            {
                double cap = ms.ModalT1Cap * _res.Modal.T1Formula;
                tx = Math.Min(tx, cap); ty = Math.Min(ty, cap);
            }
            _res.Warnings.RemoveAll(w => w.StartsWith("Động đất"));
            _res.Warnings.AddRange(LateralLoadGenerator.Apply(_p, _res.LateralForces, tx, ty).Where(w => w.StartsWith("Động đất")));
            _p.InvalidateIndex();
            _res.Modal.Notes.Add($"Phương pháp tĩnh lực ngang tương đương dùng T1 theo dao động riêng: TX = {tx.ToString("0.000", Inv)} s, TY = {ty.ToString("0.000", Inv)} s"
                + (ms.ModalT1Cap > 0 ? $" (giới hạn ≤ {ms.ModalT1Cap.ToString("0.##", Inv)}·Ct·H^0,75)" : "") + $"; Ct·H^0,75 = {_res.Modal.T1Formula.ToString("0.000", Inv)} s.");
        }
    }

    private void ModalAfterCases(IProgress<string> progress)
    {
        if (!UsesResponseSpectrum || _res.Modal == null || _phi == null || _phi.Length == 0) return;
        try
        {
            progress?.Report("Phân tích phổ phản ứng...");
            ResponseSpectrum();
        }
        catch (Exception ex)
        {
            _res.Warnings.Add("Phân tích phổ phản ứng không thành công: " + ex.Message + " – trường hợp động đất giữ kết quả tĩnh lực ngang tương đương.");
        }
    }

    // ------------------------------------------------------------------
    // KHỐI LƯỢNG
    // ------------------------------------------------------------------
    /// <summary>Trọng lượng tham gia dao động (kN) tại nút lưới (kể cả nút trung gian id âm) theo nguồn khối lượng.</summary>
    private Dictionary<int, double> NodeWeights(List<(LoadPattern pat, double factor)> src)
    {
        var W = new Dictionary<int, double>();
        void Add(int id, double w) { if (w == 0) return; W.TryGetValue(id, out var v); W[id] = v + w; }
        void Lever(Sub s, double x, double w)   // x: vị trí trên thanh gốc
        {
            double len = s.S1 - s.S0;
            double t = len > 1e-12 ? Math.Clamp((x - s.S0) / len, 0, 1) : 0.5;
            Add(s.NI, w * (1 - t)); Add(s.NJ, w * t);
        }
        foreach (var (pat, k) in src)
        {
            if (pat.SelfWeight != 0)
                foreach (var s in _subs)
                {
                    double w = pat.SelfWeight * s.Mat.UnitWeight * s.Sp.A * (s.S1 - s.S0) * k;
                    Add(s.NI, w / 2); Add(s.NJ, w / 2);
                }
            foreach (var fl in _p.FrameLoads.Where(l => l.Pattern == pat.Name))
            {
                double sign = fl.Dir == LoadDir.Gravity ? 1 : fl.Dir == LoadDir.GlobalZ ? -1 : 0;
                if (sign == 0 || !_framesSubs.TryGetValue(fl.Element, out var subs)) continue;
                double Lp = subs[^1].S1;
                for (int i = 0; i + 1 < fl.Points.Count; i++)
                {
                    double xa = fl.Points[i][0] * Lp, xb = fl.Points[i + 1][0] * Lp;
                    double qa = fl.Points[i][1], qb = fl.Points[i + 1][1];
                    if (xb - xa <= 1e-9) continue;
                    foreach (var s in subs)
                    {
                        double a = Math.Max(xa, s.S0), b = Math.Min(xb, s.S1);
                        if (b - a <= 1e-9) continue;
                        double va = qa + (qb - qa) * (a - xa) / (xb - xa);
                        double vb = qa + (qb - qa) * (b - xa) / (xb - xa);
                        // trọng tâm hình thang; tách thành hai phần (chữ nhật + tam giác) để ổn định khi va + vb ≈ 0
                        double r1 = va * (b - a), r2 = (vb - va) * (b - a) / 2;
                        Lever(s, (a + b) / 2, r1 * sign * k);
                        Lever(s, a + 2 * (b - a) / 3, r2 * sign * k);
                    }
                }
                foreach (var pl in fl.PointLoads)
                {
                    double x = pl[0] * Lp;
                    var s = subs.FirstOrDefault(q => x >= q.S0 - 1e-9 && x <= q.S1 + 1e-9) ?? subs[^1];
                    Lever(s, x, pl[1] * sign * k);
                }
            }
            foreach (var nl in _p.NodeLoads.Where(l => l.Pattern == pat.Name && l.Source != "WIND" && l.Source != "EQ"))
                if (_map.ContainsKey(nl.Node)) Add(nl.Node, -nl.Fz * k);
        }
        return W;
    }

    private Vec3 PosOf(int id) => _xyz[id];

    // ------------------------------------------------------------------
    // DAO ĐỘNG RIÊNG
    // ------------------------------------------------------------------
    private void RunModal()
    {
        var ms = _p.Modal;
        var mr = new ModalResult
        {
            MassSource = ms.Describe(_p), Diaphragm = _dGroups != null && _dGroups.Count > 0, VerticalMass = ms.IncludeVerticalMass
        };
        _res.Modal = mr;
        var src = ms.Effective(_p);
        if (src.Count == 0) throw new InvalidOperationException("nguồn khối lượng trống (Phân tích → Dao động riêng… → Nguồn khối lượng).");

        // --- khối lượng nút ---
        var W = NodeWeights(src);
        _nodeMass = new Dictionary<int, double>();
        foreach (var kv in W)
            if (kv.Value > 0 && _map.ContainsKey(kv.Key)) _nodeMass[kv.Key] = kv.Value / ModalResult.G;
        if (_nodeMass.Count == 0) throw new InvalidOperationException("không có khối lượng tại các nút (kiểm tra nguồn khối lượng và tải trọng).");
        int nd = ms.IncludeVerticalMass ? 3 : 2;

        // tâm khối lượng, tổng khối lượng (chỉ bậc tự do tự do)
        double sm = 0, sx = 0, sy = 0;
        foreach (var (id, m) in _nodeMass)
        {
            var mp = _map[id];
            if (mp[0].Count == 0 && mp[1].Count == 0) { mr.BaseMass += m; continue; }
            var q = PosOf(id); sm += m; sx += m * q.X; sy += m * q.Y;
        }
        mr.Xc = sm > 0 ? sx / sm : 0; mr.Yc = sm > 0 ? sy / sm : 0;
        foreach (var (id, m) in _nodeMass)
        {
            var mp = _map[id]; var q = PosOf(id);
            if (mp[0].Count > 0) { mr.TotalMass[0] += m; mr.TotalMass[3] += m * (q.Y - mr.Yc) * (q.Y - mr.Yc); }
            if (mp[1].Count > 0) { mr.TotalMass[1] += m; mr.TotalMass[3] += m * (q.X - mr.Xc) * (q.X - mr.Xc); }
            if (nd == 3 && mp[2].Count > 0) mr.TotalMass[2] += m;
        }

        // --- M theo phương trình ---
        _mRows = new Dictionary<int, double>[_neq];
        foreach (var (id, m) in _nodeMass)
        {
            var mp = _map[id];
            for (int d = 0; d < nd; d++)
                foreach (var (ea, ca) in mp[d])
                    foreach (var (eb, cb) in mp[d])
                    {
                        var row = _mRows[ea] ??= new Dictionary<int, double>();
                        row.TryGetValue(eb, out var v);
                        row[eb] = v + ca * cb * m;
                    }
        }
        double maxM = 0;
        for (int i = 0; i < _neq; i++) if (_mRows[i] != null && _mRows[i].TryGetValue(i, out var v)) maxM = Math.Max(maxM, v);
        var massEqs = new List<int>();
        for (int i = 0; i < _neq; i++)
            if (_mRows[i] != null && _mRows[i].TryGetValue(i, out var v) && v > maxM * 1e-12) massEqs.Add(i);
        int nm = massEqs.Count;
        mr.DynamicDofs = nm;
        if (nm == 0) throw new InvalidOperationException("không có bậc tự do có khối lượng.");
        var stories = _p.Stories.Where(s => s.Elevation > _p.BaseElevation + 1e-3).OrderBy(s => s.Elevation).ToList();
        int req = ms.RequestedModes(stories.Count);
        mr.RequestedModes = req;
        int p = Math.Min(req, nm);

        double[] MulM(double[] x)
        {
            var y = new double[_neq];
            foreach (int i in massEqs)
            {
                double s = 0;
                foreach (var (j, v) in _mRows[i]) s += v * x[j];
                y[i] = s;
            }
            return y;
        }

        bool dense = ms.Solver == ModalSettings.SolverDense || (ms.Solver != ModalSettings.SolverSubspace && nm <= 800);
        double[] w2;
        double[][] phi;
        if (dense)
        {
            mr.Solver = "Khử tĩnh các bậc tự do không khối lượng + Householder/QL (giải đầy đủ)";
            var pos = new Dictionary<int, int>();
            for (int a = 0; a < nm; a++) pos[massEqs[a]] = a;
            var F = new double[nm, nm];
            var x = new double[_neq];
            for (int c = 0; c < nm; c++)
            {
                Array.Clear(x);
                x[massEqs[c]] = 1;
                _K.Solve(x);
                for (int a = 0; a < nm; a++) F[a, c] = x[massEqs[a]];
            }
            var Mm = new double[nm, nm];
            for (int a = 0; a < nm; a++)
                foreach (var (j, v) in _mRows[massEqs[a]])
                    if (pos.TryGetValue(j, out var b)) Mm[a, b] = v;
            var (om2, ph) = ModalSolver.SolveDense(F, Mm);
            int got = Math.Min(p, om2.Length);
            w2 = new double[got]; phi = new double[got][];
            for (int k = 0; k < got; k++)
            {
                w2[k] = om2[k];
                var pm = new double[_neq];
                for (int a = 0; a < nm; a++) pm[massEqs[a]] = ph[a, k];
                var full = MulM(pm);           // φ = ω²·K⁻¹·M·φm (khôi phục bậc tự do không khối lượng)
                _K.Solve(full);
                for (int i = 0; i < _neq; i++) full[i] *= om2[k];
                phi[k] = full;
            }
            mr.Iterations = 1;
        }
        else
        {
            mr.Solver = "Lặp không gian con (Bathe), K⁻¹ từ phân tích LDLᵀ";
            var dK = new double[_neq]; var dM = new double[_neq];
            for (int i = 0; i < _neq; i++) dK[i] = _K.Get(i, i);   // sau Factor: phần tử đường chéo là D (dương) – chỉ dùng để chọn véc tơ xuất phát
            foreach (int i in massEqs) dM[i] = _mRows[i][i];
            var (om2, ph, it, conv) = ModalSolver.Subspace(_neq, v => _K.Solve(v), MulM, dK, dM, massEqs, p, ms.Tolerance);
            w2 = om2; phi = ph;
            mr.Iterations = it;
            if (!conv) _res.Warnings.Add($"Dao động riêng: lặp không gian con chưa hội tụ sau {it} vòng (sai số {ms.Tolerance:0e0}) – kết quả gần đúng.");
        }
        _phi = phi;

        // --- kết quả từng dạng ---
        var innerKeys = new Dictionary<int, int[]>();
        foreach (var kv in _framesSubs)
            if (kv.Value.Count > 1) innerKeys[kv.Key] = kv.Value.Take(kv.Value.Count - 1).Select(s => s.NJ).ToArray();
        var cum = new double[4];
        for (int k = 0; k < phi.Length; k++)
        {
            var f = phi[k];
            double[] NodeU(int id)
            {
                var u = new double[6];
                var mp = _map[id];
                for (int d = 0; d < 6; d++) foreach (var (e, c) in mp[d]) u[d] += c * f[e];
                return u;
            }
            // dấu: thành phần tịnh tiến lớn nhất dương
            double big = 0;
            foreach (var id in _map.Keys)
            {
                var u = NodeU(id);
                for (int d = 0; d < 3; d++) if (Math.Abs(u[d]) > Math.Abs(big)) big = u[d];
            }
            if (big < 0) for (int i = 0; i < f.Length; i++) f[i] = -f[i];
            var mode = new ModeResult { Index = k + 1, Omega = Math.Sqrt(Math.Max(w2[k], 0)) };
            mode.Period = mode.Omega > 0 ? 2 * Math.PI / mode.Omega : double.PositiveInfinity;
            mode.Frequency = mode.Omega / (2 * Math.PI);
            double umax = 0;
            foreach (var id in _map.Keys.Where(i => i > 0))
            {
                var u = NodeU(id);
                mode.Disp[id] = u;
                umax = Math.Max(umax, Math.Sqrt(u[0] * u[0] + u[1] * u[1] + u[2] * u[2]));
            }
            foreach (var (fid, keys) in innerKeys)
                mode.FrameInner[fid] = keys.Select(key => { var u = NodeU(key); return new[] { u[0], u[1], u[2] }; }).ToArray();
            mode.DisplayScale = umax > 0 ? 1 / umax : 1;
            var L = new double[4];
            foreach (var (id, m) in _nodeMass)
            {
                var u = NodeU(id); var q = PosOf(id);
                L[0] += m * u[0]; L[1] += m * u[1];
                if (nd == 3) L[2] += m * u[2];
                L[3] += m * (-(q.Y - mr.Yc) * u[0] + (q.X - mr.Xc) * u[1]);
            }
            for (int d = 0; d < 4; d++)
            {
                mode.Gamma[d] = L[d];
                mode.Ratio[d] = mr.TotalMass[d] > 0 ? L[d] * L[d] / mr.TotalMass[d] : 0;
                cum[d] += mode.Ratio[d];
                mode.Cumulative[d] = cum[d];
            }
            double rx = mode.Ratio[0], ry = mode.Ratio[1], rz = mode.Ratio[2], rt = mode.Ratio[3];
            double best = Math.Max(Math.Max(rx, ry), Math.Max(rz, rt));
            mode.Dominant = best < 0.01 ? "Cục bộ" : best == rt ? "Xoắn" : best == rx ? "X" : best == ry ? "Y" : "Z";
            mr.Modes.Add(mode);
        }

        // --- khối lượng tầng, tâm khối lượng, tâm cứng ---
        StoryTable(mr, stories);
        if (stories.Count > 0)
        {
            mr.Height = stories[^1].Elevation - _p.BaseElevation;
            bool steel = _p.Frames.Count(fr => !fr.IsConcrete) > _p.Frames.Count / 2;
            mr.T1Formula = (steel ? 0.085 : 0.075) * Math.Pow(Math.Max(mr.Height, 0), 0.75);
        }

        // --- ghi chú, cảnh báo ---
        var m1 = mr.First;
        if (m1 != null)
        {
            _res.Warnings.Add($"Dao động riêng: {mr.Modes.Count} dạng ({nm} bậc tự do động), T1 = {m1.Period.ToString("0.000", Inv)} s (phương {m1.Dominant}), "
                + $"Σ khối lượng tham gia UX = {mr.SumRatio(0) * 100:0.0} %, UY = {mr.SumRatio(1) * 100:0.0} %; Ct·H^0,75 = {mr.T1Formula.ToString("0.000", Inv)} s.");
            if (m1.Period > 5) _res.Warnings.Add("Dao động riêng: chu kỳ T1 > 5 s – kiểm tra mô hình (nút treo, giải phóng liên kết, thiếu sàn cứng/giằng).");
            if (mr.Modes.Count < req && mr.Modes.Count == nm) mr.Notes.Add($"Số dạng yêu cầu {req} > số bậc tự do động {nm} – đã tính toàn bộ {nm} dạng.");
        }
        foreach (int d in new[] { 0, 1 })
            if (mr.TotalMass[d] > 0 && mr.SumRatio(d) < 0.9 - 1e-9)
                _res.Warnings.Add($"Dao động riêng: tổng tỷ lệ khối lượng tham gia phương {(d == 0 ? "X" : "Y")} = {mr.SumRatio(d) * 100:0.0} % < 90 % (TCVN 9386 mục 4.3.3.3.1) – tăng số dạng dao động.");
        WindNote(mr);
    }

    private void StoryTable(ModalResult mr, List<Story> stories)
    {
        if (stories.Count == 0) return;
        var groups = new Dictionary<string, List<(int id, double m)>>();
        foreach (var s in stories) groups.TryAdd(s.Name, new List<(int id, double m)>());
        foreach (var (id, m) in _nodeMass)
        {
            var mp = _map[id];
            if (mp[0].Count == 0 && mp[1].Count == 0) continue;
            double z = PosOf(id).Z;
            Story best = null; double dz = double.MaxValue;
            foreach (var s in stories) { double dd = Math.Abs(s.Elevation - z); if (dd < dz) { dz = dd; best = s; } }
            if (best == null || Math.Abs(z - _p.BaseElevation) < dz) continue;
            groups[best.Name].Add((id, m));
        }
        foreach (var s in stories.GroupBy(x => x.Name).Select(x => x.First()))
        {
            var g = groups[s.Name];
            if (g.Count == 0) continue;
            double M = g.Sum(t => t.m);
            var sm = new StoryMass
            {
                Story = s.Name, Elevation = s.Elevation, Mass = M,
                Xm = g.Sum(t => t.m * PosOf(t.id).X) / M, Ym = g.Sum(t => t.m * PosOf(t.id).Y) / M
            };
            sm.Inertia = g.Sum(t => { var q = PosOf(t.id); return t.m * ((q.X - sm.Xm) * (q.X - sm.Xm) + (q.Y - sm.Ym) * (q.Y - sm.Ym)); });
            var atLevel = _map.Keys.Where(i => i > 0 && Math.Abs(PosOf(i).Z - s.Elevation) < 1e-3).Select(PosOf).ToList();
            if (atLevel.Count == 0) atLevel = g.Select(t => PosOf(t.id)).ToList();
            sm.Lx = atLevel.Max(q => q.X) - atLevel.Min(q => q.X);
            sm.Ly = atLevel.Max(q => q.Y) - atLevel.Min(q => q.Y);
            // tâm cứng: lực đơn vị tại nút chủ sàn cứng (Fx, Fy, Mz) – vị trí lực ngang không gây xoay sàn
            if (_dGroups != null)
                for (int gi = 0; gi < _dGroups.Count; gi++)
                {
                    if (Math.Abs(_dGroups[gi].z - s.Elevation) > 1e-3) continue;
                    double Rot(int dof)
                    {
                        var x = new double[_neq];
                        x[_dMaster[gi, dof]] = 1;
                        _K.Solve(x);
                        return x[_dMaster[gi, 2]];
                    }
                    double tFx = Rot(0), tFy = Rot(1), tMz = Rot(2);
                    if (Math.Abs(tMz) > 1e-30)
                    {
                        sm.Xr = _dGroups[gi].xc - tFy / tMz;
                        sm.Yr = _dGroups[gi].yc + tFx / tMz;
                    }
                    break;
                }
            mr.Stories.Add(sm);
        }
    }

    /// <summary>So sánh tần số dao động riêng f1 với tần số giới hạn fL (gió) – tham khảo TCVN 2737:1995 bảng 9.</summary>
    private void WindNote(ModalResult mr)
    {
        var m1 = mr.First;
        if (m1 == null || _p.Wind == null) return;
        string zone = (_p.Wind.Zone ?? "").Trim().ToUpperInvariant();
        string[] romans = { "V", "IV", "III", "II", "I" };
        string z = romans.FirstOrDefault(r => zone.StartsWith(r));
        if (z == null)
        {
            double[] w0 = { 0.65, 0.95, 1.25, 1.55, 1.85 };
            int best = 0;
            for (int i = 1; i < 5; i++) if (Math.Abs(w0[i] - _p.Wind.W0) < Math.Abs(w0[best] - _p.Wind.W0)) best = i;
            z = new[] { "I", "II", "III", "IV", "V" }[best];
        }
        bool steel = _p.Frames.Count(fr => !fr.IsConcrete) > _p.Frames.Count / 2;
        int zi = Array.IndexOf(new[] { "I", "II", "III", "IV", "V" }, z);
        double fL = steel ? new[] { 3.4, 4.1, 5.0, 5.6, 5.9 }[zi] : new[] { 1.1, 1.3, 1.6, 1.7, 1.9 }[zi];
        string dyn = mr.Height < 40
            ? "H < 40 m: theo TCVN 2737 không bắt buộc xét thành phần động của tải gió (nhà nhiều tầng)"
            : m1.Frequency >= fL
                ? "H ≥ 40 m, f1 ≥ fL: cần xét thành phần động của gió – chỉ kể xung vận tốc gió (cách tính đơn giản)"
                : "H ≥ 40 m, f1 < fL: cần xét thành phần động của gió với các dạng dao động có f < fL (chương trình chưa tính – người dùng tính bổ sung)";
        mr.Notes.Add($"Gió: f1 = {m1.Frequency.ToString("0.000", Inv)} Hz; tần số giới hạn fL = {fL.ToString("0.0", Inv)} Hz (vùng áp lực gió {z}, δ = {(steel ? "0,15" : "0,3")}; "
            + $"bảng 9 TCVN 2737:1995 – đối chiếu {Std.Loads}). {dyn}.");
    }

    // ------------------------------------------------------------------
    // PHỔ PHẢN ỨNG
    // ------------------------------------------------------------------
    private void EnsureModalKey()
    {
        foreach (var s in _subs)
        {
            if (!s.Segs.ContainsKey(ModalKey)) s.Segs[ModalKey] = new List<double[]>();
            if (!s.Pts.ContainsKey(ModalKey)) s.Pts[ModalKey] = new List<double[]>();
            if (!s.Pstar.ContainsKey(ModalKey)) s.Pstar[ModalKey] = new double[12];
        }
    }

    private void ResponseSpectrum()
    {
        var mr = _res.Modal; var ms = _p.Modal; var ss = _p.Seismic;
        EnsureModalKey();
        int n = Math.Min(_phi.Length, mr.Modes.Count);
        if (n == 0) return;
        bool cqc = !string.Equals(ms.Combination, "SRSS", StringComparison.OrdinalIgnoreCase);
        var omega = mr.Modes.Take(n).Select(m => m.Omega).ToList();
        var rho = ModalSolver.Correlation(omega, cqc, ms.Damping);
        // đáp ứng đơn vị từng dạng (u = φk)
        var unit = new CaseResult[n];
        for (int k = 0; k < n; k++) unit[k] = Recover((double[])_phi[k].Clone(), ModalKey, new Dictionary<int, double[]>());
        var stories = _p.Stories.Where(s => s.Elevation > _p.BaseElevation + 1e-3).OrderBy(s => s.Elevation).ToList();
        double TC = ss.Soil switch { "A" => 0.4, "B" => 0.5, "D" => 0.8, "E" => 0.5, _ => 0.6 };

        foreach (var pat in _p.Patterns.Where(t => t.Type == LoadType.Seismic))
        {
            int d = IsYDir(pat.Name) ? 1 : 0;
            var rc = new RsCase { Pattern = pat.Name, Dir = d == 1 ? 'Y' : 'X', Combination = cqc ? $"CQC (ζ = {ms.Damping.ToString("0.###", Inv)})" : "SRSS" };
            var a = new double[n];
            var vb = new double[n];
            for (int k = 0; k < n; k++)
            {
                var mo = mr.Modes[k];
                double sd = LateralLoadGenerator.Sd(mo.Period, ss);
                double G = mo.Gamma[d];
                a[k] = mo.Omega > 0 ? G * sd * ModalResult.G / (mo.Omega * mo.Omega) : 0;
                vb[k] = G * G * sd * ModalResult.G;
                rc.Rows.Add(new RsModeRow { Mode = mo.Index, Period = mo.Period, Sd = sd, Meff = G * G, BaseShear = vb[k] });
                rc.MassRatio += mo.Ratio[d];
            }
            rc.BaseShear = ModalSolver.Combine(vb, rho);
            var res = CombineModes(pat.Name, unit, a, rho);

            // so sánh lực cắt đáy tĩnh (T theo dao động riêng)
            var dom = mr.Dominant(d);
            rc.StaticPeriod = dom?.Period ?? 0;
            double sdS = LateralLoadGenerator.Sd(rc.StaticPeriod, ss);
            double lam = (rc.StaticPeriod <= 2 * TC && stories.Count > 2) ? 0.85 : 1.0;
            rc.StaticBaseShear = sdS * mr.TotalMass[d] * ModalResult.G * lam;
            if (ms.ScaleToStatic > 0 && rc.BaseShear > 0 && rc.BaseShear < ms.ScaleToStatic * rc.StaticBaseShear)
            {
                rc.ScaleFactor = ms.ScaleToStatic * rc.StaticBaseShear / rc.BaseShear;
                res = res.Scaled(rc.ScaleFactor, pat.Name);
            }

            // xoắn ngẫu nhiên: Mi = e·Li·Fi, Fi phân phối lực cắt đáy tĩnh theo zi·mi (TCVN 9386 4.3.3.2.3, 4.3.3.3.3)
            if (ms.AccidentalTorsion && ms.Eccentricity > 0 && mr.Stories.Count > 0)
            {
                double sumZM = mr.Stories.Sum(s => (s.Elevation - _p.BaseElevation) * s.Mass);
                var F = new double[_neq];
                var applied = new Dictionary<int, double[]>();
                bool any = false;
                foreach (var st in mr.Stories)
                {
                    double Fi = sumZM > 0 ? rc.StaticBaseShear * (st.Elevation - _p.BaseElevation) * st.Mass / sumZM : 0;
                    double Li = d == 0 ? st.Ly : st.Lx;
                    double Mt = ms.Eccentricity * Li * Fi;
                    rc.Torsion.Add((st.Story, Fi, Li, Mt));
                    if (Mt == 0) continue;
                    var ids = _p.Frames.Where(f => f.IsVerticalKind).SelectMany(f => new[] { f.NodeI, f.NodeJ }).Distinct()
                        .Where(id => _map.ContainsKey(id) && Math.Abs(PosOf(id).Z - st.Elevation) < 1e-3).ToList();
                    if (ids.Count == 0) continue;
                    double r2 = ids.Sum(id => { var q = PosOf(id); return (q.X - st.Xm) * (q.X - st.Xm) + (q.Y - st.Ym) * (q.Y - st.Ym); });
                    if (r2 <= 1e-9) continue;
                    foreach (var id in ids)
                    {
                        var q = PosOf(id);
                        double fx = -Mt * (q.Y - st.Ym) / r2, fy = Mt * (q.X - st.Xm) / r2;
                        if (!applied.TryGetValue(id, out var ap)) applied[id] = ap = new double[6];
                        ap[0] += fx; ap[1] += fy;
                        foreach (var (e, c) in _map[id][0]) F[e] += c * fx;
                        foreach (var (e, c) in _map[id][1]) F[e] += c * fy;
                        any = true;
                    }
                }
                if (any)
                {
                    _K.Solve(F);
                    AddAbs(res, Recover(F, ModalKey, applied));
                }
            }
            res.Name = pat.Name;
            _res.Cases[pat.Name] = res;
            mr.Spectrum.Add(rc);
            string sc = rc.ScaleFactor > 1 + 1e-9 ? $", đã nhân hệ số {rc.ScaleFactor.ToString("0.000", Inv)}" : "";
            _res.Warnings.Add($"Phổ phản ứng {pat.Name} (phương {rc.Dir}, {rc.Combination}): Vđáy = {rc.BaseShear.ToString("0.0", Inv)} kN{sc}; "
                + $"tĩnh lực ngang tương đương với T = {rc.StaticPeriod.ToString("0.000", Inv)} s: {rc.StaticBaseShear.ToString("0.0", Inv)} kN; Σ khối lượng {rc.MassRatio * 100:0.0} %.");
            if (rc.MassRatio < 0.9 - 1e-9)
                _res.Warnings.Add($"Phổ phản ứng {pat.Name}: tổng khối lượng hữu hiệu {rc.MassRatio * 100:0.0} % < 90 % – tăng số dạng dao động (TCVN 9386 mục 4.3.3.3.1).");
        }
        mr.Notes.Add("Kết quả phổ phản ứng là giá trị không dấu (tổ hợp " + (cqc ? "CQC" : "SRSS") + "); tổ hợp ±E1 ± 0,3·E2 cho cặp bao +/−. "
            + "Xoắn ngẫu nhiên Mi = " + ms.Eccentricity.ToString("0.###", Inv) + "·Li·Fi cộng trị tuyệt đối vào đáp ứng.");
    }

    /// <summary>Tổ hợp đáp ứng các dạng (CQC/SRSS) cho mọi đại lượng; a[k] = hệ số nhân dạng k.</summary>
    private static CaseResult CombineModes(string name, CaseResult[] unit, double[] a, double[,] rho)
    {
        int n = unit.Length;
        var r = new CaseResult { Name = name };
        var buf = new double[n];
        foreach (var id in unit[0].Disp.Keys)
        {
            var v = new double[6];
            for (int c = 0; c < 6; c++)
            {
                for (int k = 0; k < n; k++) buf[k] = a[k] * (unit[k].Disp.TryGetValue(id, out var u) ? u[c] : 0);
                v[c] = ModalSolver.Combine(buf, rho);
            }
            r.Disp[id] = v;
        }
        foreach (var id in unit[0].Reactions.Keys)
        {
            var v = new double[6];
            for (int c = 0; c < 6; c++)
            {
                for (int k = 0; k < n; k++) buf[k] = a[k] * (unit[k].Reactions.TryGetValue(id, out var u) ? u[c] : 0);
                v[c] = ModalSolver.Combine(buf, rho);
            }
            r.Reactions[id] = v;
        }
        foreach (var (fid, f0) in unit[0].Frames)
        {
            var ff = new FrameForces { Stations = (double[])f0.Stations.Clone(), F = new double[f0.Count][] };
            for (int i = 0; i < f0.Count; i++)
            {
                ff.F[i] = new double[6];
                for (int c = 0; c < 6; c++)
                {
                    for (int k = 0; k < n; k++) buf[k] = a[k] * unit[k].Frames[fid].F[i][c];
                    ff.F[i][c] = ModalSolver.Combine(buf, rho);
                }
            }
            r.Frames[fid] = ff;
        }
        return r;
    }

    /// <summary>r += |t| cho mọi đại lượng (cùng mặt cắt).</summary>
    private static void AddAbs(CaseResult r, CaseResult t)
    {
        foreach (var (id, u) in t.Disp)
            if (r.Disp.TryGetValue(id, out var v)) for (int c = 0; c < 6; c++) v[c] += Math.Abs(u[c]);
        foreach (var (id, u) in t.Reactions)
            if (r.Reactions.TryGetValue(id, out var v)) for (int c = 0; c < 6; c++) v[c] += Math.Abs(u[c]);
        foreach (var (fid, f) in t.Frames)
            if (r.Frames.TryGetValue(fid, out var g) && g.Count == f.Count)
                for (int i = 0; i < f.Count; i++) for (int c = 0; c < 6; c++) g.F[i][c] += Math.Abs(f.F[i][c]);
    }
}
