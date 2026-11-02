using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

/// <summary>Thiết kế móng băng BTCT dưới dãy cột (tiết diện chữ T ngược: bản đáy + sườn):
/// <list type="bullet">
/// <item>Gom chân cột thành dải theo phương X hoặc Y (tự chọn phương có nhiều cột thẳng hàng nhất trên một trục), hoặc giao thoa 2 phương.</item>
/// <item>Bề rộng đáy B theo áp lực nền tiêu chuẩn: ptb ≤ R(B), pmax ≤ 1,2R(B), R theo TCVN 9362:2012 (FootingDesigner.BearingR).</item>
/// <item>Nội lực: dầm Euler trên nền Winkler (phần tử ~0,25 m, lò xo k·B·a tại nút), giải cho từng tổ hợp; mô men dương căng dưới.</item>
/// <item>Sườn b×h: thép dưới/trên theo bao mô men, đai theo lực cắt; bản cánh công xôn (B − b)/2 chịu áp lực nền ròng.</item>
/// </list></summary>
public static class StripFootingDesigner
{
    /// <summary>Kích thước phần tử dầm trên nền đàn hồi (m)</summary>
    public const double MeshSize = 0.25;
    /// <summary>Bề rộng đáy lớn nhất khi tăng dần (m)</summary>
    public const double MaxWidth = 6.0;
    public const double MaxRibHeight = 2.5;
    /// <summary>Chiều dày mép bản cánh tối thiểu (m)</summary>
    public const double MinEdge = 0.20;
    /// <summary>Dung sai gom cột thẳng hàng (m)</summary>
    public const double AlignTol = 0.05;

    private sealed class Col
    {
        public Node Node;
        public FrameElement Frame;
        public double S;        // toạ độ dọc dải
        public double Size;     // kích thước cột theo phương dải
        public double Width;    // kích thước cột vuông góc dải
        public double Share = 1;
    }

    /// <summary>Thiết kế móng băng cho các chân cột có loại móng hiệu lực là Strip.</summary>
    public static List<StripFootingDesign> Design(Project p, AnalysisResults r, List<string> warnings)
    {
        warnings ??= new List<string>();
        var res = new List<StripFootingDesign>();
        if (p == null || r == null) return res;
        var fs = p.Foundation ?? new FoundationSettings();
        var cols = FoundationUtil.BaseColumns(p).Where(x => FoundationUtil.TypeOf(p, x.Node.Id) == FoundationType.Strip).ToList();
        if (cols.Count == 0) return res;

        var linesX = Lines(cols, true);    // dải dọc X: gom theo Y
        var linesY = Lines(cols, false);   // dải dọc Y: gom theo X
        var strips = new List<(bool AlongX, List<(Node Node, FrameElement Column)> Members)>();
        int dir = fs.StripDirection;
        if (dir == 0)
        {
            double avgX = cols.Count / (double)Math.Max(linesX.Count, 1), avgY = cols.Count / (double)Math.Max(linesY.Count, 1);
            dir = avgY > avgX + 1e-9 ? 2 : 1;
        }
        if (dir == 1) strips.AddRange(linesX.Select(l => (true, l)));
        else if (dir == 2) strips.AddRange(linesY.Select(l => (false, l)));
        else
        {
            strips.AddRange(linesX.Where(l => l.Count >= 2).Select(l => (true, l)));
            strips.AddRange(linesY.Where(l => l.Count >= 2).Select(l => (false, l)));
            var covered = new HashSet<int>(strips.SelectMany(s => s.Members.Select(m => m.Node.Id)));
            foreach (var c in cols.Where(c => !covered.Contains(c.Node.Id)))
                strips.Add((true, new List<(Node, FrameElement)> { c }));
        }
        // tỷ lệ chia tải tại nút giao (giao thoa): chia đều cho các dải đi qua nút
        var count = new Dictionary<int, int>();
        foreach (var s in strips)
            foreach (var m in s.Members) count[m.Node.Id] = (count.TryGetValue(m.Node.Id, out int k) ? k : 0) + 1;

        var uls = r.DesignCombos();
        var sls = FoundationUtil.ServiceCombosStrict(r);
        int no = 1;
        foreach (var s in strips)
        {
            string where = DesignUtil.GridName(p, s.Members[0].Node.X, s.Members[0].Node.Y);
            try
            {
                var d = DesignOne(p, r, s.AlongX, s.Members, count, uls, sls, fs);
                d.Name = "MB" + no++;
                if (dir == 3 && s.Members.Any(m => count[m.Node.Id] > 1))
                    d.Messages.Add("Móng băng giao thoa: tải cột tại nút giao chia đều cho các dải; diện tích đáy chồng lấn tại nút giao chưa trừ");
                res.Add(d);
            }
            catch (Exception ex)
            {
                warnings.Add("LỖI thiết kế móng băng qua " + where + ": " + ex.Message);
            }
        }
        return res;
    }

    /// <summary>Gom các chân cột thẳng hàng: alongX = true gom theo toạ độ Y (dải chạy dọc X).</summary>
    private static List<List<(Node Node, FrameElement Column)>> Lines(List<(Node Node, FrameElement Column)> cols, bool alongX)
    {
        var res = new List<List<(Node, FrameElement)>>();
        var sorted = cols.OrderBy(c => alongX ? c.Node.Y : c.Node.X).ToList();
        foreach (var c in sorted)
        {
            double v = alongX ? c.Node.Y : c.Node.X;
            var line = res.FirstOrDefault(l => Math.Abs((alongX ? l[0].Item1.Y : l[0].Item1.X) - v) <= AlignTol);
            if (line == null) { line = new List<(Node, FrameElement)>(); res.Add(line); }
            line.Add(c);
        }
        foreach (var l in res) l.Sort((a, b) => (alongX ? a.Item1.X : a.Item1.Y).CompareTo(alongX ? b.Item1.X : b.Item1.Y));
        return res;
    }

    // ------------------------------------------------------------------
    // Dầm trên nền Winkler
    // ------------------------------------------------------------------
    /// <summary>Lời giải dầm trên nền đàn hồi cho 1 tổ hợp.</summary>
    public sealed class BeamSolution
    {
        public double[] W;          // chuyển vị đứng nút (m, dương lên)
        public double[] Pressure;   // áp lực nền k·(−w) (kN/m²)
        public double[] M;          // mô men tại nút (phía phải nút), dương căng dưới
        public double[] MLeft;      // mô men phía trái nút
        public double[] V;          // lực cắt phần tử nút i → i+1 (dương: tổng lực hướng lên bên trái)
        public double SumLoad;      // tổng tải đứng hướng xuống (kN)
    }

    /// <summary>Mô hình dầm trên nền Winkler: nút xs (m), độ cứng EI (kN.m²), hệ số nền k (kN/m³), bề rộng đáy B (m).
    /// Ma trận độ cứng phân tích Cholesky dải một lần, giải cho nhiều vế phải.</summary>
    public sealed class WinklerBeam
    {
        public readonly double[] Xs;
        public readonly double[] Trib;
        public readonly double[] Ks;
        private readonly double[,] _l;
        private readonly int _n;
        private const int Bw = 3;
        public double K { get; }

        public WinklerBeam(IList<double> xs, double EI, double k, double B)
        {
            Xs = xs.ToArray();
            int nn = Xs.Length;
            if (nn < 2) throw new ArgumentException("dải móng cần ít nhất 2 nút");
            K = k;
            Trib = new double[nn];
            for (int i = 0; i + 1 < nn; i++)
            {
                double le = Xs[i + 1] - Xs[i];
                if (le <= 1e-6) throw new ArgumentException("nút dải móng trùng nhau");
                Trib[i] += le / 2; Trib[i + 1] += le / 2;
            }
            Ks = Trib.Select(t => k * B * t).ToArray();
            _n = 2 * nn;
            var a = new double[_n, Bw + 1];   // a[i, j − i + Bw] = A(i, j), j ≤ i
            void Add(int i, int j, double v)
            {
                if (j > i) (i, j) = (j, i);
                a[i, j - i + Bw] += v;
            }
            for (int e = 0; e + 1 < nn; e++)
            {
                double l = Xs[e + 1] - Xs[e], c = EI / (l * l * l);
                double[,] ke =
                {
                    { 12 * c, 6 * l * c, -12 * c, 6 * l * c },
                    { 6 * l * c, 4 * l * l * c, -6 * l * c, 2 * l * l * c },
                    { -12 * c, -6 * l * c, 12 * c, -6 * l * c },
                    { 6 * l * c, 2 * l * l * c, -6 * l * c, 4 * l * l * c }
                };
                int[] dof = { 2 * e, 2 * e + 1, 2 * e + 2, 2 * e + 3 };
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j <= i; j++) Add(dof[i], dof[j], ke[i, j]);
            }
            for (int i = 0; i < nn; i++) Add(2 * i, 2 * i, Ks[i]);
            // Cholesky dải: A = L·Lᵀ
            _l = new double[_n, Bw + 1];
            for (int i = 0; i < _n; i++)
                for (int j = Math.Max(0, i - Bw); j <= i; j++)
                {
                    double sum = a[i, j - i + Bw];
                    for (int q = Math.Max(0, i - Bw); q < j; q++) sum -= _l[i, q - i + Bw] * _l[j, q - j + Bw];
                    if (i == j)
                    {
                        if (sum <= 0) throw new InvalidOperationException("ma trận dầm trên nền đàn hồi không xác định dương");
                        _l[i, Bw] = Math.Sqrt(sum);
                    }
                    else _l[i, j - i + Bw] = sum / _l[j, Bw];
                }
        }

        /// <summary>Giải với lực nút fz (kN, dương lên) và mô men nút mt (kN.m, ngược chiều kim đồng hồ trong mặt phẳng s–z).</summary>
        public BeamSolution Solve(double[] fz, double[] mt)
        {
            int nn = Xs.Length;
            var b = new double[_n];
            for (int i = 0; i < nn; i++) { b[2 * i] = fz[i]; b[2 * i + 1] = mt[i]; }
            var y = new double[_n];
            for (int i = 0; i < _n; i++)
            {
                double s = b[i];
                for (int q = Math.Max(0, i - Bw); q < i; q++) s -= _l[i, q - i + Bw] * y[q];
                y[i] = s / _l[i, Bw];
            }
            var x = new double[_n];
            for (int i = _n - 1; i >= 0; i--)
            {
                double s = y[i];
                for (int q = i + 1; q <= Math.Min(_n - 1, i + Bw); q++) s -= _l[q, i - q + Bw] * x[q];
                x[i] = s / _l[i, Bw];
            }
            var sol = new BeamSolution
            {
                W = new double[nn], Pressure = new double[nn], M = new double[nn], MLeft = new double[nn], V = new double[nn]
            };
            double vPrev = 0, mPrev = 0;
            for (int i = 0; i < nn; i++)
            {
                double w = x[2 * i];
                sol.W[i] = w;
                sol.Pressure[i] = -K * w;
                double fv = fz[i] - Ks[i] * w;               // lực đứng tổng tại nút (tải + lò xo), dương lên
                sol.SumLoad -= fz[i];
                double ml = i == 0 ? 0 : mPrev + vPrev * (Xs[i] - Xs[i - 1]);
                sol.MLeft[i] = ml;
                sol.M[i] = ml - mt[i];
                sol.V[i] = vPrev + fv;
                vPrev = sol.V[i]; mPrev = sol.M[i];
            }
            return sol;
        }
    }

    /// <summary>Mô men quán tính tiết diện chữ T ngược: bản đáy B × hf ở dưới + sườn b × (h − hf).</summary>
    public static double InertiaT(double B, double hf, double b, double h)
    {
        hf = Math.Clamp(hf, 0, h);
        B = Math.Max(B, b);
        double a1 = B * hf, a2 = b * (h - hf);
        double y1 = hf / 2, y2 = hf + (h - hf) / 2;
        double A = a1 + a2;
        if (A <= 0) return 0;
        double yc = (a1 * y1 + a2 * y2) / A;
        return B * hf * hf * hf / 12 + a1 * (y1 - yc) * (y1 - yc) + b * Math.Pow(h - hf, 3) / 12 + a2 * (y2 - yc) * (y2 - yc);
    }

    // ------------------------------------------------------------------
    private static StripFootingDesign DesignOne(Project p, AnalysisResults r, bool alongX, List<(Node Node, FrameElement Column)> members,
        Dictionary<int, int> count, List<string> uls, List<string> sls, FoundationSettings fs)
    {
        var cs = p.Concrete;
        // nền: hố khoan gần tâm dải khi Soil.UseBorehole, ngược lại p.Soil (GeotechCalc.SoilFor)
        var soil = GeotechCalc.SoilFor(p, members.Average(m => m.Node.X), members.Average(m => m.Node.Y), out var geoBh);
        var conc = DesignUtil.ConcreteOf(p, cs.ConcreteFooting, cs.ConcreteFooting);
        var main = p.Material(cs.RebarMain);
        var stirMat = p.Material(cs.RebarStirrup);
        double kt = Math.Max(soil.LoadFactorAvg, 1.0);
        double Df = Math.Max(soil.Depth, 0);
        double gtb = soil.GammaAvg;
        double cover = Math.Max(cs.CoverFooting, 0.03);

        var cols = members.Select(m => new Col
        {
            Node = m.Node, Frame = m.Column, S = alongX ? m.Node.X : m.Node.Y,
            Size = DesignUtil.ColumnDimAlong(p, m.Column, alongX ? Vec3.UnitX : Vec3.UnitY),
            Width = DesignUtil.ColumnDimAlong(p, m.Column, alongX ? Vec3.UnitY : Vec3.UnitX),
            Share = 1.0 / Math.Max(1, count.TryGetValue(m.Node.Id, out int kc) ? kc : 1)
        }).OrderBy(c => c.S).ToList();

        double overhang = Math.Max(fs.StripOverhang, cols.Max(c => c.Size) / 2 + 0.10);
        double start = cols[0].S - overhang, end = cols[^1].S + overhang;
        double cLine = cols.Average(c => alongX ? c.Node.Y : c.Node.X);
        // [MóngLệchTâm] đầu dải không vượt ranh giới đất; dải nằm trên ranh giới → bản cánh một phía (móng băng lệch tâm)
        var (fSide, fLim, fName) = EccentricFooting.StripSide(p, alongX, cLine, cols.Max(c => c.Width));
        if (EccentricFooting.Active(p))
        {
            var l0 = EccentricFooting.Limits(p, cols[0].Node.X, cols[0].Node.Y, alongX ? cols[0].Size : cols[0].Width, alongX ? cols[0].Width : cols[0].Size);
            var l1 = EccentricFooting.Limits(p, cols[^1].Node.X, cols[^1].Node.Y, alongX ? cols[^1].Size : cols[^1].Width, alongX ? cols[^1].Width : cols[^1].Size);
            double lo = alongX ? l0.XMin : l0.YMin, hi = alongX ? l1.XMax : l1.YMax;
            if (!double.IsInfinity(lo) && start < lo) start = Math.Min(lo, cols[0].S - cols[0].Size / 2);
            if (!double.IsInfinity(hi) && end > hi) end = Math.Max(hi, cols[^1].S + cols[^1].Size / 2);
        }
        double Ls = end - start;
        // ---- hết [MóngLệchTâm] ----
        var d = new StripFootingDesign
        {
            AlongX = alongX, C = cLine, Start = start, End = end, Depth = Df, Story = "Móng",
            NodeIds = cols.Select(c => c.Node.Id).ToList(),
            ColumnPositions = cols.Select(c => c.S).ToList(), ColumnSizes = cols.Select(c => c.Size).ToList(), ColumnWidths = cols.Select(c => c.Width).ToList(),
            LoadShares = cols.Select(c => c.Share).ToList()
        };
        string gname = DesignUtil.NearestGridName(alongX ? p.GridsY : p.GridsX, cLine);
        d.Grid = gname != null ? "Trục " + gname : (alongX ? "y = " : "x = ") + DesignUtil.F(cLine, "0.00");
        if (cols.Count == 1) d.Messages.Add("Dải chỉ có 1 cột – làm việc như móng đơn kéo dài");

        // ---- lưới nút ----
        var keyPts = new List<double> { start, end };
        keyPts.AddRange(cols.Select(c => c.S));
        keyPts = keyPts.OrderBy(v => v).ToList();
        var xs = new List<double> { keyPts[0] };
        for (int i = 0; i + 1 < keyPts.Count; i++)
        {
            double a = keyPts[i], b2 = keyPts[i + 1];
            if (b2 - a < 1e-4) continue;
            int ne = Math.Max(1, (int)Math.Ceiling((b2 - a) / MeshSize - 1e-9));
            for (int j = 1; j <= ne; j++) xs.Add(a + (b2 - a) * j / ne);
        }
        int nn = xs.Count;
        int[] colNode = cols.Select(c => NearestIndex(xs, c.S)).ToArray();

        // ---- tải chân cột ----
        var ulsLoads = cols.Select(c => FoundationUtil.Loads(r, c.Node.Id, uls)).ToList();
        bool hasSls = sls.Count > 0;
        var slsLoads = cols.Select(c => hasSls ? FoundationUtil.Loads(r, c.Node.Id, sls) : FoundationUtil.Loads(r, c.Node.Id, uls, 1.0 / kt)).ToList();
        if (ulsLoads.Any(l => l.Count == 0)) throw new InvalidOperationException("thiếu phản lực chân cột của dải");
        var ulsNames = ulsLoads[0].Select(l => l.Combo).Where(cn => ulsLoads.All(L => L.Any(x => x.Combo == cn))).ToList();
        var slsNames = slsLoads[0].Select(l => l.Combo).Where(cn => slsLoads.All(L => L.Any(x => x.Combo == cn))).ToList();
        if (ulsNames.Count == 0 || slsNames.Count == 0) throw new InvalidOperationException("không có tổ hợp chung cho các chân cột của dải");

        // nút tải: fz (lên), mt (ngược chiều kim đồng hồ) cho một tổ hợp
        (double[] fz, double[] mt, double sumN) NodalLoads(List<List<BaseLoad>> src, string combo, double hArm)
        {
            var fz = new double[nn]; var mt = new double[nn];
            double sumN = 0;
            for (int i = 0; i < cols.Count; i++)
            {
                var l = src[i].First(x => x.Combo == combo);
                double shr = cols[i].Share;
                fz[colNode[i]] -= shr * l.N;
                sumN += shr * l.N;
                mt[colNode[i]] += shr * (alongX ? l.My + hArm * l.Qx : -l.Mx + hArm * l.Qy);
            }
            return (fz, mt, sumN);
        }

        // ---- kích thước sơ bộ ----
        double b = Math.Max(fs.StripRibWidth, 0.20);
        if (cols.Max(c => c.Width) > b + 1e-6) d.Messages.Add("Bề rộng sườn nhỏ hơn cạnh cột – nên mở rộng sườn tại chân cột (vát nách)");
        double maxSpan = overhang * 2;
        for (int i = 0; i + 1 < cols.Count; i++) maxSpan = Math.Max(maxSpan, cols[i + 1].S - cols[i].S);
        double h = Math.Max(0.40, RcSection.CeilTo(maxSpan / 7.0, 0.05));
        double hf = 0.25, hEdge = MinEdge;
        double sumNtcMax = slsNames.Max(cn => slsLoads.Select((L, i) => cols[i].Share * L.First(x => x.Combo == cn).N).Sum());
        // [CừTràm] gia cố nền cừ tràm dưới dải: R(B) ← [p] (CuTramCalc, móng Ls × B); không gia cố → R theo TCVN 9362 như cũ
        bool cuOn = members.Any(mb => CuTramCalc.ActiveAt(p, mb.Node.Id, true));
        double cuX = members.Average(mb => mb.Node.X), cuY = members.Average(mb => mb.Node.Y);
        double CuR(double BB) => cuOn ? CuTramCalc.Evaluate(p, cuX, cuY, soil, Ls, BB).Allowable : FootingDesigner.BearingR(soil, BB);
        double R1 = CuR(1.0);
        double B = Math.Max(RcSection.CeilTo(Math.Max(sumNtcMax, 0) / (Ls * Math.Max(R1 - gtb * Df, 10)), 0.05), Math.Max(b + 0.20, 0.60));

        WinklerBeam beam = null;
        BeamSolution govS = null, govU = null;
        string govSName = "", govUName = "";
        double R = 0, k = 0, pmax = 0, pmin = 0, pavg = 0, sumNgov = 0, q = 0;
        double[] mMax = null, mMin = null, vMax = null;
        bool sized = false, ribOk = false;
        FlexureResult fPos = default, fNeg = default;
        ShearResult sh = default;
        double vDesign = 0, mPos = 0, mNeg = 0, h0 = 0;
        bool negOnFlange = true;

        for (int outer = 0; outer < 60; outer++)
        {
            double depth = Math.Max(Df, h);
            // --- bề rộng theo áp lực nền (TTGH II) ---
            sized = false;
            for (int it = 0; it < 200; it++)
            {
                R = CuR(B);   // [CừTràm]
                k = fs.SubgradeModulus > 0 ? fs.SubgradeModulus : 120 * Math.Max(R, 10);
                double I = InertiaT(B, (hf + hEdge) / 2, b, h);
                beam = new WinklerBeam(xs, conc.E * I, k, B);
                q = gtb * depth * B;
                pmax = double.MinValue; pmin = double.MaxValue; pavg = 0;
                foreach (string cn in slsNames)
                {
                    var (fz, mt, sumN) = NodalLoads(slsLoads, cn, h);
                    for (int i = 0; i < nn; i++) fz[i] -= q * beam.Trib[i];
                    var sol = beam.Solve(fz, mt);
                    double pm = sol.Pressure.Max();
                    if (pm > pmax) { pmax = pm; govS = sol; govSName = cn; sumNgov = sumN; }
                    pmin = Math.Min(pmin, sol.Pressure.Min());
                    pavg = Math.Max(pavg, (sumN + q * Ls) / (B * Ls));
                }
                if (pavg <= R + 1e-6 && pmax <= 1.2 * R + 1e-6) { sized = true; break; }
                if (B >= MaxWidth - 1e-9) break;
                B = Math.Min(MaxWidth, Math.Round(B + 0.05, 3));
            }

            // --- nội lực TTGH I ---
            mMax = Enumerable.Repeat(double.MinValue, nn).ToArray();
            mMin = Enumerable.Repeat(double.MaxValue, nn).ToArray();
            vMax = new double[nn];
            double best = -1;
            foreach (string cn in ulsNames)
            {
                var (fz, mt, _) = NodalLoads(ulsLoads, cn, h);
                for (int i = 0; i < nn; i++) fz[i] -= q * beam.Trib[i];
                var sol = beam.Solve(fz, mt);
                for (int i = 0; i < nn; i++)
                {
                    double a1 = Math.Max(sol.M[i], sol.MLeft[i]), a2 = Math.Min(sol.M[i], sol.MLeft[i]);
                    mMax[i] = Math.Max(mMax[i], a1); mMin[i] = Math.Min(mMin[i], a2);
                    vMax[i] = Math.Max(vMax[i], Math.Abs(sol.V[i]));
                    double am = Math.Max(Math.Abs(a1), Math.Abs(a2));
                    if (am > best) { best = am; govU = sol; govUName = cn; }
                }
            }
            mPos = Math.Max(0, mMax.Max());
            mNeg = Math.Min(0, mMin.Min());
            // lực cắt tại mép cột: bỏ qua đoạn phần tử nằm trong phạm vi cột
            vDesign = 0;
            for (int i = 0; i + 1 < nn; i++)
            {
                double xm = (xs[i] + xs[i + 1]) / 2;
                if (cols.Any(c => Math.Abs(xm - c.S) < c.Size / 2)) continue;
                vDesign = Math.Max(vDesign, vMax[i]);
            }
            if (vDesign <= 0) vDesign = vMax.Max();

            // --- sườn ---
            h0 = h - cover - 0.012;
            fPos = RcSection.DesignRect(mPos, b, h0, cover + 0.01, conc, main, cs.MinRatioBeam);
            fNeg = RcSection.DesignRect(-mNeg, B, h0, cover + 0.01, conc, main, cs.MinRatioBeam);
            negOnFlange = true;
            if (fNeg.Xi * h0 > hEdge)
            {
                fNeg = RcSection.DesignRect(-mNeg, b, h0, cover + 0.01, conc, main, cs.MinRatioBeam);
                negOnFlange = false;
            }
            // đai
            int[] sdias = DesignUtil.Sorted(cs.StirrupBars, new[] { 6, 8, 10 });
            sh = default;
            foreach (int sd in sdias.Where(x => x >= 8).DefaultIfEmpty(sdias[^1]))
            {
                sh = RcSection.Shear(vDesign, b, h0, conc, stirMat, sd, 2);
                if (sh.Ok) break;
            }
            ribOk = fPos.Ok && fNeg.Ok && !fPos.Doubly && !fNeg.Doubly && sh.Ok;

            // --- bản cánh ---
            double cf = fSide != 0 ? Math.Max(B - b, 0) : Math.Max((B - b) / 2, 0);   // [MóngLệchTâm] bản cánh một phía: công xôn B − b
            double pnetMax = Math.Max(0, govU.Pressure.Max() - q / B);
            hf = Math.Max(hEdge, 0.25);
            if (cf > 1e-6)
            {
                for (int it = 0; it < 60; it++)
                {
                    double h0f = hf - cover - 0.01;
                    double vf = pnetMax * Math.Max(cf - h0f, 0);
                    double mf = pnetMax * cf * cf / 2;
                    var ff = RcSection.DesignRect(mf, 1.0, h0f, 0.03, conc, main, 0.05);
                    if (vf <= RcSection.QbConcrete(1.0, h0f, conc) + 1e-6 && ff.Ok && !ff.Doubly) break;
                    hf = Math.Round(hf + 0.05, 3);
                }
            }
            else hf = h;
            if (hf > h - 0.10 && cf > 1e-6) { h = RcSection.CeilTo(hf + 0.15, 0.05); continue; }
            if (ribOk && sized) break;
            if (!ribOk)
            {
                if (h >= MaxRibHeight - 1e-9) break;
                h = Math.Round(h + 0.05, 3);
                continue;
            }
            break;   // không đạt áp lực nền dù B max: dừng
        }
        if (!sized) d.Messages.Add("LỖI không chọn được bề rộng móng băng ≤ " + DesignUtil.F(MaxWidth, "0.0") + " m thoả điều kiện áp lực nền – xem xét móng cọc");
        if (!ribOk) d.Messages.Add("LỖI tiết diện sườn móng băng không đủ chịu lực với h ≤ " + DesignUtil.F(MaxRibHeight * 1000, "0") + " mm");
        if (pmin < 0) d.Messages.Add("Có vùng đáy móng tách khỏi nền (áp lực âm) ở tổ hợp tiêu chuẩn");

        double cfl = fSide != 0 ? Math.Max(B - b, 0) : Math.Max((B - b) / 2, 0);   // [MóngLệchTâm]
        d.Width = B; d.RibB = b; d.RibH = h; d.FlangeRoot = cfl > 1e-6 ? hf : h; d.FlangeEdge = cfl > 1e-6 ? Math.Min(hEdge, hf) : h;
        d.R = R; d.K = k; d.PMax = pmax; d.PAvg = pavg;
        d.SumN = sumNgov; d.SelfWeight = q * Ls;
        d.Combo = govUName; d.ComboService = govSName;
        d.Stations = xs.ToList();
        d.M = govU.M.ToList(); d.V = govU.V.ToList(); d.Pressure = govS.Pressure.ToList();
        d.MMax = mMax.ToList(); d.MMin = mMin.ToList(); d.VMax = vMax.ToList();
        d.MPos = mPos; d.MNeg = mNeg; d.VDesign = vDesign;

        d.Checks.Add(new CheckItem { Name = "Áp lực trung bình đáy móng ptb ≤ R", Demand = pavg, Capacity = R, Unit = "kN/m²", Note = govSName });
        d.Checks.Add(new CheckItem { Name = "Áp lực lớn nhất pmax ≤ 1,2R", Demand = pmax, Capacity = 1.2 * R, Unit = "kN/m²", Note = govSName });
        d.Checks.Add(new CheckItem
        {
            Name = "Áp lực nhỏ nhất pmin ≥ 0", Demand = pmin < 0 ? -pmin : 0, Capacity = pmin < 0 ? 0 : 1, Unit = "kN/m²",
            Note = "pmin = " + DesignUtil.F(pmin, "0.0") + " kN/m²"
        });

        // ---- thép sườn ----
        int[] bdias = DesignUtil.Sorted(cs.BeamBars, new[] { 14, 16, 18, 20, 22, 25 });
        var bot = RcSection.ChooseBars(fPos.As, b, cover, bdias, 2, 2);
        var top = RcSection.ChooseBars(fNeg.As, b, cover, bdias, 2, 2);
        // lựa chọn thép của người dùng (Project.RebarChoices): sườn dưới / trên, đai, bản cánh
        string key = MemberKey(alongX, members), basis = Basis(b, h, B);
        d.RebarKeys.Add(key);
        d.RibH0 = h0;
        int nUser = 0;
        BarSet UserBars(string zone, BarSet auto, bool isTop)
        {
            var ch = p.ValidRebarChoice(key, zone, -1, basis);
            var g = ch?.Bars?.Where(x => x != null && x.Dia > 0 && x.Count > 0).Select(x => (x.Dia, x.Count)).ToList();
            if (g == null || g.Count == 0) return auto;
            nUser++;
            var fit = RebarOptions.CheckFit(g, b, cover, isTop);
            if (!fit.Fits) d.Messages.Add("LỖI bố trí thép do người dùng chọn không đạt – " + zone + " " + RcSection.BarText(g) + ": không lọt bề rộng sườn");
            return new BarSet { Groups = g, Layers = fit.Layers, Ok = true };
        }
        bot = UserBars(RebarZones.StripRibBottom, bot, false);
        top = UserBars(RebarZones.StripRibTop, top, true);
        var sch = p.ValidRebarChoice(key, RebarZones.Stirrup, -1, basis);
        if (sch != null && sch.Dia >= 6 && sch.Spacing > 0)
        {
            nUser++;
            sh.Dia = sch.Dia; sh.Legs = 2; sh.SSupport = sch.Spacing;
            sh.Capacity = RcSection.ShearCapacity(b, h0, conc, stirMat, sch.Dia, 2, sch.Spacing);
            sh.Note = "đai do người dùng chọn";
        }
        if (!bot.Ok || !top.Ok) d.Messages.Add("LỖI không bố trí đủ thép dọc sườn trong bề rộng b – tăng tiết diện sườn");
        double asBot = bot.Groups.Sum(g => g.Count * RcSection.BarArea(g.Dia)), asTop = top.Groups.Sum(g => g.Count * RcSection.BarArea(g.Dia));
        d.AsBottom = fPos.As; d.AsTop = fNeg.As;
        d.BottomBars = RcSection.BarText(bot.Groups); d.TopBars = RcSection.BarText(top.Groups);
        d.Checks.Add(new CheckItem
        {
            Name = "Uốn sườn – mô men dương (căng dưới)", Demand = mPos, Unit = "kN.m", Note = d.BottomBars + ", " + govUName,
            Capacity = mPos > 1e-9 ? RcSection.MomentCapacity(b, h0, asBot, 0, cover, conc, main) : 1
        });
        d.Checks.Add(new CheckItem
        {
            Name = "Uốn sườn – mô men âm (căng trên)", Demand = -mNeg, Unit = "kN.m", Note = d.TopBars + (negOnFlange ? " (vùng nén bản đáy)" : ""),
            Capacity = mNeg < -1e-9 ? RcSection.MomentCapacity(negOnFlange ? B : b, h0, asTop, 0, cover, conc, main) : 1
        });
        d.StirrupDia = sh.Dia; d.StirrupSpacing = sh.SSupport; d.StirrupLegs = sh.Legs;
        d.Stirrups = "Ø" + sh.Dia + "a" + DesignUtil.F(sh.SSupport * 1000, "0");
        d.Checks.Add(new CheckItem
        {
            Name = "Cắt sườn Q ≤ Qu", Demand = vDesign, Unit = "kN", Note = d.Stirrups + (string.IsNullOrEmpty(sh.Note) ? "" : "; " + sh.Note),
            Capacity = sh.Capacity
        });

        // ---- bản cánh ----
        double pnet = Math.Max(0, govU.Pressure.Max() - q / B);
        double h0fl = d.FlangeRoot - cover - 0.01;
        if (cfl > 1e-6)
        {
            d.FlangeM = pnet * cfl * cfl / 2;
            d.FlangeV = pnet * Math.Max(cfl - h0fl, 0);
            var fl = RcSection.DesignRect(d.FlangeM, 1.0, h0fl, 0.03, conc, main, 0.05);
            int[] fd = DesignUtil.Sorted(cs.FootingBars, new[] { 10, 12, 14, 16 });
            var sc = RcSection.ChooseSpacing(dia => Math.Max(fl.As, 0.0005 * h0fl), fd, 0.20, 0.10, 0.15, 10);
            d.FlangeH0 = h0fl;
            var fch = p.ValidRebarChoice(key, RebarZones.StripFlange, -1, basis);
            if (fch != null && fch.Dia > 0 && fch.Spacing > 0)
            {
                nUser++;
                sc = new SpacingChoice { Dia = fch.Dia, Spacing = fch.Spacing, AsReqPerM = Math.Max(fl.As, 0.0005 * h0fl), Ok = true };
            }
            d.AsFlange = sc.AsReqPerM;
            d.FlangeBars = sc.Text;
            if (!sc.Ok) d.Messages.Add("LỖI không bố trí đủ thép bản cánh với bước ≥ 100 mm");
            d.Checks.Add(new CheckItem
            {
                Name = "Uốn bản cánh tại chân sườn", Demand = d.FlangeM, Unit = "kN.m/m", Note = sc.Text,
                Capacity = RcSection.MomentCapacity(1.0, h0fl, RcSection.BarArea(sc.Dia) / sc.Spacing, 0, cover, conc, main)
            });
            d.Checks.Add(new CheckItem
            {
                Name = "Cắt bản cánh Q ≤ Qb (không cốt đai)", Demand = d.FlangeV, Capacity = RcSection.QbConcrete(1.0, h0fl, conc), Unit = "kN/m",
                Note = "hf = " + DesignUtil.F(d.FlangeRoot * 1000, "0") + " mm"
            });
            AddFlangeBars(d, sc.Dia, sc.Spacing, cover);
        }
        AddRibBars(d, bot, top, cover);
        d.UserRebarZones = nUser;
        if (nUser > 0)
        {
            d.Messages.Add("Bố trí thép theo lựa chọn của người dùng tại " + nUser + " vùng (đã kiểm tra lại khả năng chịu lực)");
            foreach (var ci in d.Checks.Where(ck => (ck.Name.StartsWith("Uốn") || ck.Name.StartsWith("Cắt sườn")) && !ck.Ok).ToList())
                d.Messages.Add("LỖI bố trí thép do người dùng chọn không đạt – " + ci.Name + ": " + ci.Note);
        }
        if (cuOn)   // [CừTràm] kết quả cừ tràm dưới dải + kiểm tra độ bền, lún khối quy ước
        {
            d.CuTram = CuTramCalc.Evaluate(p, cuX, cuY, soil, Ls, B, pavg, pmax, slsLoads.Max(ll => ll.Count > 0 ? ll.Max(x2 => x2.N) : 0));
            CuTramCalc.AddChecks(d.Checks, d.Messages, d.CuTram);
        }
        if (geoBh != null && !cuOn)
        {
            // lún móng băng L × B, áp lực trung bình tiêu chuẩn ptb (GeotechCalc.Settlement)
            var st = GeotechCalc.Settlement(geoBh, Ls, B, Df, pavg);
            d.Checks.Add(new CheckItem
            {
                Name = "Độ lún S ≤ Sgh", Demand = st.S * 100, Capacity = st.Limit * 100, Unit = "cm",
                Note = "hố khoan " + geoBh.Name + ", Hc = " + DesignUtil.F(st.Hc, "0.0") + " m"
            });
            d.Messages.Add("Nền theo hố khoan " + geoBh.Name + ": φ = " + DesignUtil.F(soil.Phi, "0.#") + "°, c = " + DesignUtil.F(soil.C, "0.#") + " kPa, γ = " + DesignUtil.F(soil.Gamma, "0.##") + " kN/m³");
            var weak = GeotechCalc.WeakLayerBelow(geoBh, Df, B);
            if (weak != null) d.Messages.Add("Cảnh báo: lớp đất yếu \"" + weak.Name + "\" ở độ sâu " + DesignUtil.F(weak.Top, "0.0") + " m nằm trong phạm vi 2B dưới đáy móng – xem xét móng cọc");
        }
        if (fSide != 0) StripEccentric(d, fSide, fLim, fName, cLine, b, B, cols.Select(c => ulsLoads[cols.IndexOf(c)].Max(l => l.N) * c.Share).ToList(), conc);   // [MóngLệchTâm]
        // [FoundationChecks] phân loại dầm cứng / hữu hạn / mềm trên nền Winkler, lún lệch, giải thích sự làm việc → d.Safety
        try { FootingBehaviour.AnalyzeStrip(d, conc, fs); }
        catch (Exception ex) { d.Messages.Add("Cảnh báo: không phân tích được sự làm việc móng băng – " + ex.Message); }
        RebarNumbering.Renumber(d.Bars);
        return d;
    }

    /// <summary>[MóngLệchTâm] Móng băng trên ranh giới đất: sườn sát ranh giới, bản cánh chỉ vươn vào trong (công xôn B − b);
    /// tim cột lệch so với tâm bản đáy e = |c_cột − C| → mô men xoắn phân bố t = q·e (q = ΣN/L, kN/m), sườn chịu xoắn lớn nhất T = t·l/2 giữa 2 giằng ngang;
    /// kiểm tra sơ bộ T ≤ 0,1·Rb·b²·h (TCVN 5574:2018 mục 8.1.4 – độ bền dải nén) và khuyến nghị giằng ngang tại mỗi cột chịu M = N·e.</summary>
    private static void StripEccentric(StripFootingDesign d, int side, double lim, string name, double cLine, double b, double B, List<double> colN, Material conc)
    {
        d.FlangeSide = side;
        d.Boundary = name;
        d.ColumnLine = cLine;
        d.RibC = lim + side * b / 2;
        d.C = lim + side * B / 2;
        d.Eccentricity = Math.Abs(cLine - d.C);
        double L = Math.Max(d.Length, 1e-6), q = colN.Sum() / L;
        d.TorsionPerM = q * d.Eccentricity;
        double span = 0;
        for (int i = 0; i + 1 < d.ColumnPositions.Count; i++) span = Math.Max(span, d.ColumnPositions[i + 1] - d.ColumnPositions[i]);
        if (span <= 0) span = L;
        d.TorsionMax = d.TorsionPerM * span / 2;
        d.ColumnTorsion = colN.Select(n => n * d.Eccentricity).ToList();
        double tLim = 0.1 * conc.Rb * b * b * d.RibH;
        d.Messages.Add($"Móng băng lệch tâm trên {name.ToLowerInvariant()}: sườn sát ranh giới, bản cánh một phía vươn {Math.Max(B - b, 0) * 1000:0} mm (công xôn B − b); "
                       + $"tim cột lệch tâm bản đáy e = {d.Eccentricity * 1000:0} mm → xoắn phân bố t = q·e = {d.TorsionPerM:0.0} kN.m/m, xoắn sườn lớn nhất T = t·l/2 = {d.TorsionMax:0.0} kN.m");
        d.Messages.Add($"Cảnh báo: bố trí giằng ngang (giằng móng vuông góc dải) tại mỗi chân cột nối sang móng bên trong để tiếp nhận mô men lệch tâm M = N·e "
                       + $"(lớn nhất {(d.ColumnTorsion.Count > 0 ? d.ColumnTorsion.Max() : 0):0.0} kN.m) – nếu không, sườn phải chịu xoắn T = {d.TorsionMax:0.0} kN.m"
                       + (d.TorsionMax > tLim ? $" > 0,1·Rb·b²·h = {tLim:0.0} kN.m (sườn KHÔNG đủ chịu xoắn – bắt buộc giằng ngang)" : $" ≤ 0,1·Rb·b²·h = {tLim:0.0} kN.m"));
    }

    private static int NearestIndex(List<double> xs, double v)
    {
        int best = 0;
        for (int i = 1; i < xs.Count; i++) if (Math.Abs(xs[i] - v) < Math.Abs(xs[best] - v)) best = i;
        return best;
    }

    /// <summary>Khoá cấu kiện vật lý của dải móng băng (lựa chọn thép): "B:{X|Y}:{id nút nhỏ nhất}".</summary>
    internal static string MemberKey(bool alongX, List<(Node Node, FrameElement Column)> members) =>
        "B:" + (alongX ? "X" : "Y") + ":" + members.Min(m => m.Node.Id).ToString(DesignUtil.Inv);

    internal static string Basis(double b, double h, double B) => BeamDesigner.Basis(b, h) + "|" + DesignUtil.Mm(B).ToString("0", DesignUtil.Inv);

    /// <summary>Phương án thép móng băng đã thiết kế: sườn dưới / trên (tổ hợp thanh), đai sườn, bản cánh (Ø–bước).</summary>
    internal static List<RebarAlternative> Alternatives(Project p, StripFootingDesign d, string zone, int max = 12)
    {
        var cs = p.Concrete;
        var pref = p.RebarPrefs ?? new RebarPreferences();
        double cover = Math.Max(cs.CoverFooting, 0.03);
        if (zone == RebarZones.StripRibBottom || zone == RebarZones.StripRibTop)
        {
            int[] bd = pref.Filter(DesignUtil.Sorted(cs.BeamBars, new[] { 14, 16, 18, 20, 22, 25 }));
            bool top = zone == RebarZones.StripRibTop;
            var list = RebarOptions.BeamCombos(top ? d.AsTop : d.AsBottom, d.RibB, cover, top, bd, pref, 2, 0, max);
            foreach (var a in list) a.IsCurrent = a.Text == (top ? d.TopBars : d.BottomBars);
            return list;
        }
        if (zone == RebarZones.Stirrup)
        {
            var res = new List<RebarAlternative>();
            var conc = DesignUtil.ConcreteOf(p, cs.ConcreteFooting, cs.ConcreteFooting);
            var stl = p.Material(cs.RebarStirrup);
            double h0 = d.RibH0 > 0 ? d.RibH0 : d.RibH - cover - 0.012;
            foreach (int dd in DesignUtil.Sorted(cs.StirrupBars, new[] { 6, 8, 10 }).Where(x => x >= 6))
            {
                var sh = RcSection.Shear(d.VDesign, d.RibB, h0, conc, stl, dd, 2);
                if (!sh.Ok || sh.SSupport <= 0) continue;
                foreach (double ss in new[] { sh.SSupport, RcSection.FloorTo(sh.SSupport - 0.05 + 1e-9, 0.05) }.Distinct())
                {
                    if (ss < 0.05 - 1e-9) continue;
                    double qu = RcSection.ShearCapacity(d.RibB, h0, conc, stl, dd, 2, ss);
                    if (qu < d.VDesign - 1e-6) continue;
                    res.Add(new RebarAlternative
                    {
                        Kind = "Dầm", Zone = RebarZones.Stirrup, Index = -1, Dia = dd, Spacing = ss, Spacing2 = ss,
                        Text = "Ø" + dd + "a" + DesignUtil.Mm(ss), AsReq = d.VDesign, AsProv = qu, Fits = true,
                        IsCurrent = dd == d.StirrupDia && Math.Abs(ss - d.StirrupSpacing) < 1e-4,
                        Note = "Q = " + DesignUtil.F(d.VDesign, "0.0") + " kN ≤ Qu = " + DesignUtil.F(qu, "0.0") + " kN"
                    });
                }
            }
            return res;
        }
        int[] fd = DesignUtil.Sorted(cs.FootingBars, new[] { 10, 12, 14, 16 });
        double req = d.AsFlange;
        return RebarOptions.SpacingAlternatives("Móng", zone, _ => req, fd.Where(x => x >= 10).DefaultIfEmpty(fd.Max()).ToArray(), 0.10, 0.20, d.FlangeBars, max);
    }

    private static void AddRibBars(StripFootingDesign d, BarSet bot, BarSet top, double c)
    {
        double L = d.Length, b = d.RibB, h = d.RibH;
        double run = DesignUtil.R10(L - 2 * c);
        void Long(BarSet set, string role, bool isTop)
        {
            foreach (var g in set.Groups.Where(g => g.Count > 0))
            {
                double leg = DesignUtil.R10(Math.Max(Math.Min(h - 2 * c - 0.05, Math.Max(15 * g.Dia / 1000.0, 0.20)), 0.10));
                d.Bars.Add(new BarMark
                {
                    Role = role, Dia = g.Dia, Count = g.Count, IsTop = isTop, Shape = "U", Start = c, End = L - c,
                    Length = DesignUtil.R10(run + 2 * leg),
                    Segments = new List<double> { DesignUtil.Mm(leg), DesignUtil.Mm(run), DesignUtil.Mm(leg) }
                });
            }
        }
        Long(bot, "Dưới", false);
        Long(top, "Trên", true);
        if (h >= 0.70 - 1e-9)
        {
            int perSide = Math.Max(1, (int)Math.Ceiling((h - 2 * c) / 0.40 - 1e-9) - 1);
            d.Bars.Add(new BarMark { Role = "Giá", Dia = 12, Count = 2 * perSide, Shape = "Thẳng", Start = c, End = L - c, Length = run });
        }
        if (d.Bars.Any(x => x.Length > 11.7)) d.Messages.Add("Thép dọc dài hơn 11,7 m – nối chồng theo TCVN 5574:2018 mục 10.3.6");
        if (d.StirrupDia > 0 && d.StirrupSpacing > 0)
        {
            double s1 = Math.Max(b - 2 * c, 0.05), s2 = Math.Max(h - 2 * c, 0.05);
            d.Bars.Add(new BarMark
            {
                Role = "Đai", Dia = d.StirrupDia, Spacing = d.StirrupSpacing, Shape = "Đai", Start = 0, End = L,
                Count = (int)Math.Ceiling((L - 2 * c) / d.StirrupSpacing - 1e-9) + 1,
                Length = DesignUtil.StirrupLength(s1, s2, d.StirrupDia), Segments = new List<double> { DesignUtil.Mm(s1), DesignUtil.Mm(s2) }
            });
        }
    }

    private static void AddFlangeBars(StripFootingDesign d, int dia, double sp, double c)
    {
        double L = d.Length, B = d.Width;
        double run = DesignUtil.R10(B - 2 * c);
        double leg = DesignUtil.R10(Math.Max(Math.Min(d.FlangeEdge - 2 * c, 0.15), 0.10));
        d.Bars.Add(new BarMark
        {
            Role = "Lớp dưới bản móng", Dia = dia, Spacing = sp, Shape = "U", Start = c, End = L - c,
            Count = (int)Math.Ceiling((L - 2 * c) / sp - 1e-9) + 1, Length = DesignUtil.R10(run + 2 * leg),
            Segments = new List<double> { DesignUtil.Mm(leg), DesignUtil.Mm(run), DesignUtil.Mm(leg) }
        });
        // thép phân bố dọc dải trong bản cánh Ø10a250 (cấu tạo), mỗi bên sườn
        double cf = (B - d.RibB) / 2 - c;
        int nSide = Math.Max(1, (int)Math.Ceiling(cf / 0.25 - 1e-9));
        d.Bars.Add(new BarMark
        {
            Role = "Thép rải dọc bản móng", Dia = 10, Spacing = 0.25, Shape = "Thẳng", Start = c, End = L - c,
            Count = 2 * nSide, Length = DesignUtil.R10(L - 2 * c)
        });
    }
}
