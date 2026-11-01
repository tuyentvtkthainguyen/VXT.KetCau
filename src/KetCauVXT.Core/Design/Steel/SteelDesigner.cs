using System.Globalization;
using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Steel;

/// <summary>
/// Kiểm tra cấu kiện thép (cột, kèo khung tiền chế, xà gồ, giằng) theo tinh thần TCVN 5575 (công thức kiểu SNiP II-23-81*).
/// <para>LƯU Ý: Các công thức dưới đây là dạng ĐƠN GIẢN HOÁ phục vụ thiết kế sơ bộ / kiểm tra nhanh.
/// Người dùng phải đối chiếu và kiểm tra lại theo đúng bản TCVN 5575:2024 đang áp dụng
/// (hệ số c, mx, ψ, φe, giới hạn ổn định cục bộ, liên kết, chân cột...).</para>
/// Đơn vị nội bộ: kN, m, kN/m². Nội lực theo quy ước ETABS: P kéo dương, M3 uốn quanh trục 3 (trong mặt phẳng khung).
/// <list type="bullet">
/// <item>Bền: σ = |N|/A + |M3|/W33 + |M2|/W22 ≤ f·γc.</item>
/// <item>Cắt: τ = V2·S/(I·tw) ≈ V2/(hw·tw) ≤ fv·γc, fv = 0.58·f.</item>
/// <item>Hệ số uốn dọc φ(λ̄), λ̄ = λ·√(f/E):
///   λ̄ ≤ 2.5: φ = 1 − (0.073 − 5.53·f/E)·λ̄^1.5;
///   2.5 &lt; λ̄ ≤ 4.5: φ = 1.47 − 13·f/E − (0.371 − 27.3·f/E)·λ̄ + (0.0275 − 5.53·f/E)·λ̄²;
///   λ̄ &gt; 4.5: φ = 332/(λ̄²·(51 − λ̄)).</item>
/// <item>Ổn định trong mặt phẳng (gần đúng): N/(φx·A·f·γc) + |M3|/(W33·f·γc·(1 − N/Ne)) + |M2|/(W22·f·γc) ≤ 1,
///   Ne = π²·E·A/λx² (lực Euler – cách tiếp cận gần đúng thay cho φe của tiêu chuẩn).</item>
/// <item>Ổn định ngoài mặt phẳng: N/(φy·A·f·γc) + |M3|/(φb·W33·f·γc) + |M2|/(W22·f·γc) ≤ 1.
///   φb = 1 khi l0y ≤ 13·bf; ngược lại φ1 = ψ·(Iy/Ix)·(h/l0y)²·E/f với α = 1.54·(It/Iy)·(l0y/h)², ψ = 1.6 + 0.08·α
///   (ψ lấy thấp hơn bảng tiêu chuẩn → thiên về an toàn), φb = φ1 nếu φ1 ≤ 0.85, ngược lại φb = min(1; 0.68 + 0.21·φ1).</item>
/// <item>Độ mảnh: λ ≤ 120 (thanh chịu nén), λ ≤ 300 (thanh chỉ chịu kéo, thanh giằng).</item>
/// <item>Ổn định cục bộ (giới hạn đơn giản hoá): cánh b0/tf ≤ 0.5·√(E/f); bụng theo TCVN 5575:2012 7.6.2.2 (α = (σ−σ1)/σ;
///   α ≤ 0,5 như cột nén đúng tâm, α ≥ 1 theo 4,35√((2α−1)E/(σ(2−α+√(α²+4β²)))) ≤ 3,8√(E/f)); chủ yếu uốn ≤ 5,5√(E/f).</item>
/// <item>TTGH II: độ võng đỉnh kèo (UZ nút đỉnh so với nút đỉnh cột) ≤ L/[RafterDeflection], L = nhịp ngang của dãy kèo;
///   chuyển vị ngang đỉnh cột ≤ H/[ColumnDrift].</item>
/// </list>
/// Chiều dài tính toán: cột khung chân khớp μ = 2.0 (khung có chuyển vị ngang), chân ngàm μ = 1.5 (gần đúng);
/// kèo μ = 1.0 với chiều dài tổng của dãy kèo; xà gồ/giằng μ = 1.0. Ngoài mặt phẳng l0y = UnbracedLength22 (hoặc L).
/// </summary>
public static class SteelDesigner
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v, string fmt = "0.##") => double.IsFinite(v) ? v.ToString(fmt, Inv) : "∞";

    public const string ChkStrength = "Bền (ứng suất pháp)";
    public const string ChkShear = "Bền chịu cắt";
    public const string ChkInPlane = "Ổn định tổng thể trong mặt phẳng";
    public const string ChkOutPlane = "Ổn định ngoài mặt phẳng (kể cả oằn xoắn)";
    public const string ChkSlender = "Độ mảnh giới hạn";
    public const string ChkFlange = "Ổn định cục bộ bản cánh";
    public const string ChkWeb = "Ổn định cục bộ bản bụng";
    public const string ChkDeflection = "Độ võng kèo (TTGH II)";
    public const string ChkDrift = "Chuyển vị ngang đỉnh cột (TTGH II)";

    /// <summary>Kiểm tra mọi thanh thép (SteelColumn/Rafter/Purlin/Brace, tiết diện WeldedI hoặc General).
    /// Kết quả ghi vào s.Steel (xoá kết quả cũ).</summary>
    public static void Check(Project p, AnalysisResults r, DesignSummary s)
    {
        if (p == null || r == null || s == null) return;
        s.Steel.Clear();
        var frames = p.Frames.Where(f => f.Kind is FrameKind.SteelColumn or FrameKind.Rafter or FrameKind.Purlin or FrameKind.Brace).ToList();
        if (frames.Count == 0) return;

        var uls = LoadCases(r, r.DesignCombos(), s.Warnings);
        var sls = LoadCases(r, r.ServiceCombos(), s.Warnings);
        var chains = RafterChains(p);

        foreach (var f in frames)
        {
            FrameSection sec = p.FrameSections.FirstOrDefault(x => x.Name == f.Section);
            if (sec == null) { s.Warnings.Add($"Thép: thanh {f.Label} ({f.Id}) không có tiết diện '{f.Section}' – bỏ qua."); continue; }
            if (sec.Shape == SectionShape.RectConcrete) continue;
            Material mat;
            try { mat = p.Material(sec.Material); }
            catch (Exception) { s.Warnings.Add($"Thép: thanh {f.Label} – không tìm thấy vật liệu '{sec.Material}'."); continue; }
            if (mat.F <= 0 || mat.E <= 0)
            {
                s.Warnings.Add($"Thép: thanh {f.Label} – vật liệu '{mat.Name}' chưa có cường độ tính toán f hoặc E – bỏ qua.");
                continue;
            }
            try
            {
                s.Steel.Add(CheckMember(p, f, sec, mat, uls, sls, chains));
            }
            catch (Exception ex)
            {
                s.Warnings.Add($"Thép: lỗi khi kiểm tra thanh {f.Label} ({f.Id}): {ex.Message}");
            }
        }
    }

    private static List<(string name, CaseResult res)> LoadCases(AnalysisResults r, IEnumerable<string> names, List<string> warn)
    {
        var l = new List<(string, CaseResult)>();
        foreach (var n in names)
        {
            try { l.Add((n, r.Get(n))); }
            catch (Exception ex) { warn?.Add($"Thép: không lấy được kết quả tổ hợp {n}: {ex.Message}"); }
        }
        return l;
    }

    // ------------------------------------------------------------------
    // Dãy kèo liên tục (các kèo nối nhau qua nút chung): tổng chiều dài và nhịp ngang
    // ------------------------------------------------------------------
    private static Dictionary<int, (double length, double span)> RafterChains(Project p)
    {
        var res = new Dictionary<int, (double, double)>();
        var rafters = p.Frames.Where(f => f.Kind == FrameKind.Rafter).ToList();
        var byNode = new Dictionary<int, List<FrameElement>>();
        foreach (var f in rafters)
            foreach (int n in new[] { f.NodeI, f.NodeJ })
            {
                if (!byNode.TryGetValue(n, out var lst)) byNode[n] = lst = new List<FrameElement>();
                lst.Add(f);
            }
        var visited = new HashSet<int>();
        foreach (var f0 in rafters)
        {
            if (visited.Contains(f0.Id)) continue;
            var chain = new List<FrameElement>();
            var q = new Queue<FrameElement>();
            q.Enqueue(f0); visited.Add(f0.Id);
            while (q.Count > 0)
            {
                var f = q.Dequeue(); chain.Add(f);
                foreach (int n in new[] { f.NodeI, f.NodeJ })
                    foreach (var g in byNode[n])
                        if (visited.Add(g.Id)) q.Enqueue(g);
            }
            double len = 0;
            var nodes = new List<Node>();
            foreach (var f in chain)
            {
                len += p.Length(f);
                nodes.Add(p.Node(f.NodeI)); nodes.Add(p.Node(f.NodeJ));
            }
            double span = 0;
            for (int i = 0; i < nodes.Count; i++)
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    double dx = nodes[i].X - nodes[j].X, dy = nodes[i].Y - nodes[j].Y;
                    span = Math.Max(span, Math.Sqrt(dx * dx + dy * dy));
                }
            foreach (var f in chain) res[f.Id] = (len, span);
        }
        return res;
    }

    // ------------------------------------------------------------------
    // Bộ theo dõi giá trị bất lợi nhất của từng hạng mục kiểm tra
    // ------------------------------------------------------------------
    private sealed class Tracker
    {
        public readonly List<CheckItem> Items = new();
        private readonly Dictionary<string, CheckItem> _idx = new();

        public void Update(string name, double demand, double capacity, string unit, string note)
        {
            var c = new CheckItem { Name = name, Demand = demand, Capacity = capacity, Unit = unit, Note = note };
            if (!_idx.TryGetValue(name, out var old))
            {
                _idx[name] = c; Items.Add(c); return;
            }
            if (c.Ratio > old.Ratio + 1e-12)
            {
                int i = Items.IndexOf(old);
                Items[i] = c; _idx[name] = c;
            }
        }
    }

    private static double SafeDiv(double a, double b) =>
        Math.Abs(b) > 1e-15 ? a / b : (Math.Abs(a) > 1e-12 ? 1e6 : 0);

    /// <summary>Hệ số uốn dọc φ theo độ mảnh quy ước λ̄ (TCVN 5575 / SNiP II-23-81*, công thức (8)–(9)).</summary>
    public static double Phi(double lambdaBar, double f, double E)
    {
        double lb = lambdaBar;
        if (lb <= 1e-9) return 1.0;
        if (lb >= 50) return 0.01;
        double fe = f / E;
        double phi;
        if (lb <= 2.5) phi = 1 - (0.073 - 5.53 * fe) * Math.Pow(lb, 1.5);
        else if (lb <= 4.5) phi = 1.47 - 13 * fe - (0.371 - 27.3 * fe) * lb + (0.0275 - 5.53 * fe) * lb * lb;
        else phi = 332.0 / (lb * lb * (51 - lb));
        return Math.Clamp(phi, 0.01, 1.0);
    }

    /// <summary>Hệ số ổn định khi uốn φb (gần đúng, xem chú thích lớp). bf = bề rộng cánh nén (lấy cánh nhỏ hơn).</summary>
    public static double PhiB(SectionProps sp, double bf, double l0y, double f, double E)
    {
        if (l0y <= 13 * bf || l0y <= 1e-9) return 1.0;
        if (sp.I22 <= 0 || sp.I33 <= 0 || sp.Depth <= 0) return 1.0;
        double h = sp.Depth;
        double alpha = 1.54 * (Math.Max(sp.J, 0) / sp.I22) * Math.Pow(l0y / h, 2);
        alpha = Math.Clamp(alpha, 0.1, 40);
        double psi = 1.6 + 0.08 * alpha;
        double phi1 = psi * (sp.I22 / sp.I33) * Math.Pow(h / l0y, 2) * E / f;
        double phib = phi1 <= 0.85 ? phi1 : Math.Min(1.0, 0.68 + 0.21 * phi1);
        return Math.Clamp(phib, 0.05, 1.0);
    }

    private static SteelMemberCheck CheckMember(Project p, FrameElement f, FrameSection sec, Material mat,
        List<(string name, CaseResult res)> uls, List<(string name, CaseResult res)> sls,
        Dictionary<int, (double length, double span)> chains)
    {
        double L = p.Length(f);
        var set = p.Steel ?? new SteelDesignSettings();
        double gc = set.GammaC > 0 ? set.GammaC : 1.0;
        double fd = mat.F * gc;              // kN/m²
        double fvd = 0.58 * mat.F * gc;      // kN/m²
        double E = mat.E;
        double sqrtEf = Math.Sqrt(E / mat.F);

        var nI = p.Node(f.NodeI); var nJ = p.Node(f.NodeJ);
        Node nLo = nI.Z <= nJ.Z ? nI : nJ, nHi = nI.Z <= nJ.Z ? nJ : nI;

        // --- chiều dài tính toán ---
        double mu, l0x;
        string l0Note;
        switch (f.Kind)
        {
            case FrameKind.SteelColumn:
                bool pinned = !nLo.IsSupport || (!nLo.Restraint[3] && !nLo.Restraint[4]);
                mu = pinned ? 2.0 : 1.5;
                l0x = mu * L;
                l0Note = $"Cột: μ = {F(mu)} ({(pinned ? "chân khớp" : "chân ngàm")}, khung có chuyển vị ngang), l0x = {F(l0x, "0.00")} m";
                break;
            case FrameKind.Rafter:
                mu = 1.0;
                l0x = chains.TryGetValue(f.Id, out var ch) ? ch.length : L;
                l0Note = $"Kèo: μ = 1.0, l0x = tổng chiều dài dãy kèo = {F(l0x, "0.00")} m";
                break;
            default:
                mu = 1.0; l0x = L;
                l0Note = $"μ = 1.0, l0x = L = {F(L, "0.00")} m";
                break;
        }
        double l0y = f.UnbracedLength22 > 1e-6 ? f.UnbracedLength22 : L;

        bool isI = sec.Shape == SectionShape.WeldedI;
        double bfMin = isI ? Math.Min(sec.BfTop, sec.BfBot) : sec.GWidth;
        if (bfMin <= 0) bfMin = sec.Props(0.5).Width;

        var res = new SteelMemberCheck
        {
            Name = string.IsNullOrWhiteSpace(f.Label) ? $"F{f.Id}" : f.Label,
            Story = f.Story, Frame = f.Id, Section = sec.Name, Quantity = 1
        };
        res.Messages.Add(l0Note + $"; l0y = {F(l0y, "0.00")} m (khoảng cách giằng ngoài mặt phẳng).");
        if (sec.IsTapered) res.Messages.Add("Tiết diện vát: đặc trưng hình học lấy tại từng mặt cắt kiểm tra.");

        var tr = new Tracker();
        double govRatio = -1, maxLambda = 0, maxNc = 0;

        foreach (var (cname, cr) in uls)
        {
            if (!cr.Frames.TryGetValue(f.Id, out var ff) || ff.Count == 0) continue;
            double Lf = ff.Stations[^1] > 1e-9 ? ff.Stations[^1] : L;
            for (int i = 0; i < ff.Count; i++)
            {
                double st = ff.Stations[i];
                var sp = sec.Props(st / Lf);
                if (sp.A <= 0) continue;
                double P = ff.P(i), V2 = ff.V2(i), V3 = ff.V3(i), M2 = ff.M2(i), M3 = ff.M3(i);
                double Nc = Math.Max(0, -P);
                maxNc = Math.Max(maxNc, Nc);
                string at = $"{cname}, x = {F(st, "0.00")} m";

                // 1. Bền
                double sN = Math.Abs(P) / sp.A;
                double sM3 = SafeDiv(Math.Abs(M3), sp.W33);
                double sM2 = SafeDiv(Math.Abs(M2), sp.W22);
                double sigma = sN + sM3 + sM2;
                tr.Update(ChkStrength, sigma / 1000, fd / 1000, "MPa", at);
                double rStrength = sigma / fd;

                // 2. Cắt
                double Aw, As3;
                if (isI)
                {
                    double hw = sp.Depth - sec.TfTop - sec.TfBot;
                    Aw = Math.Max(hw, 0) * sec.Tw;
                    As3 = sp.As3;
                }
                else { Aw = sp.As2; As3 = sp.As3; }
                double tau = Math.Max(SafeDiv(Math.Abs(V2), Aw), SafeDiv(Math.Abs(V3), As3));
                tr.Update(ChkShear, tau / 1000, fvd / 1000, "MPa", at);

                // 3. Ổn định
                double i33 = sp.I33 > 0 ? sp.i33 : 0, i22 = sp.I22 > 0 ? sp.i22 : 0;
                double lx = i33 > 0 ? l0x / i33 : 999, ly = i22 > 0 ? l0y / i22 : 999;
                maxLambda = Math.Max(maxLambda, Math.Max(lx, ly));
                double phib = PhiB(sp, bfMin, l0y, mat.F, E);
                double rIn = 0;
                if (Nc > 1e-6)
                {
                    double phix = Phi(lx * Math.Sqrt(mat.F / E), mat.F, E);
                    double Ne = Math.PI * Math.PI * E * sp.A / (lx * lx);
                    double amp = Math.Max(1 - Nc / Ne, 0.01);
                    rIn = Nc / (phix * sp.A * fd) + SafeDiv(Math.Abs(M3), sp.W33 * fd * amp) + SafeDiv(Math.Abs(M2), sp.W22 * fd);
                    tr.Update(ChkInPlane, rIn, 1.0, "",
                        $"{at}; λx = {F(lx, "0.0")}, φx = {F(phix, "0.000")}, Ne = {F(Ne, "0")} kN");
                }
                double phiy = Nc > 1e-6 ? Phi(ly * Math.Sqrt(mat.F / E), mat.F, E) : 1.0;
                double rOut = (Nc > 1e-6 ? Nc / (phiy * sp.A * fd) : 0)
                              + SafeDiv(Math.Abs(M3), phib * sp.W33 * fd) + SafeDiv(Math.Abs(M2), sp.W22 * fd);
                tr.Update(ChkOutPlane, rOut, 1.0, "",
                    $"{at}; λy = {F(ly, "0.0")}, φy = {F(phiy, "0.000")}, φb = {F(phib, "0.000")}");

                // 4. Ổn định cục bộ bụng (phụ thuộc mặt cắt)
                if (isI && sec.Tw > 0)
                {
                    double hw = sp.Depth - sec.TfTop - sec.TfBot;
                    double lam = Math.Max(lx, 0) * Math.Sqrt(mat.F / E);
                    double lim; string how;
                    if (Nc > 1e-6)
                    {
                        // TCVN 5575:2012 mục 7.6.2.2: α = (σ − σ1)/σ tại hai mép bụng
                        double yw = hw / 2;
                        double sm = sp.I33 > 0 ? Math.Abs(M3) * yw / sp.I33 : 0;
                        double sig = Nc / sp.A + sm, sig1 = Nc / sp.A - sm;
                        double alpha = sig > 1e-9 ? (sig - sig1) / sig : 0;
                        double tauW = hw * sec.Tw > 0 ? Math.Abs(V2) / (hw * sec.Tw) : 0;
                        double lAxial = (lam <= 2 ? 1.30 + 0.15 * lam * lam : Math.Min(1.20 + 0.35 * lam, 2.3)) * sqrtEf;
                        double LimAt(double a)
                        {
                            double beta = 1.4 * (2 * a - 1) * tauW / sig;
                            double v = 4.35 * Math.Sqrt((2 * a - 1) * E / (sig * (2 - a + Math.Sqrt(a * a + 4 * beta * beta))));
                            return Math.Min(v, 3.8 * sqrtEf);
                        }
                        if (alpha <= 0.5) lim = lAxial;
                        else if (alpha >= 1) lim = LimAt(Math.Min(alpha, 2));
                        else lim = lAxial + (LimAt(1) - lAxial) * (alpha - 0.5) / 0.5;
                        how = $"nén + uốn, α = {F(alpha, "0.00")}, λ̄ = {F(lam, "0.00")}: [hw/tw] = {F(lim / sqrtEf, "0.00")}√(E/f) (TCVN 5575:2012 7.6.2.2)";
                    }
                    else
                    {
                        lim = 5.5 * sqrtEf;
                        how = "chủ yếu uốn: [hw/tw] = 5,5√(E/f); khi hw/tw > 3,2√(E/f) cần sườn ngang";
                    }
                    double rw = hw / sec.Tw;
                    if (Nc > 1e-6 && rw > lim && lim < 3.8 * sqrtEf - 1e-9)
                    {
                        // TCVN 5575:2012 7.6.2.6: bụng mảnh chịu nén – chỉ kể 2 dải bụng rộng 0,85·tw·√(E/f) sát cánh
                        double hEff = Math.Min(hw, 2 * 0.85 * sec.Tw * sqrtEf);
                        double aRed = Math.Max(sp.A - (hw - hEff) * sec.Tw, 1e-9);
                        double phixW = Phi(lx * Math.Sqrt(mat.F / E), mat.F, E);
                        double neW = Math.PI * Math.PI * E * aRed / Math.Max(lx * lx, 1e-9);
                        double ampW = Math.Max(1 - Nc / neW, 0.01);
                        double rRed = Nc / (phixW * aRed * fd) + SafeDiv(Math.Abs(M3), sp.W33 * fd * ampW) + SafeDiv(Math.Abs(M2), sp.W22 * fd);
                        tr.Update(ChkWeb, rRed, 1.0, "",
                            $"{at}; hw/tw = {F(rw, "0.0")} > [{F(lim, "0.0")}] ({how}) → bụng mảnh, kể tiết diện hữu hiệu A = {F(aRed * 1e4, "0.0")} cm² (7.6.2.6)");
                    }
                    else tr.Update(ChkWeb, rw, lim, "", $"{at}; {how}");
                }

                double gov = Math.Max(rStrength, Math.Max(rIn, rOut));
                if (gov > govRatio)
                {
                    govRatio = gov;
                    res.Combo = cname; res.Station = st;
                    res.N = -P; res.M = M3; res.V = V2;
                }
            }
        }

        // 5. Độ mảnh
        bool tensionOnly = f.Kind == FrameKind.Brace || maxNc < 1e-3;
        double lamLim = tensionOnly ? 300 : 120;
        res.Lambda = maxLambda;
        tr.Update(ChkSlender, maxLambda, lamLim, "",
            tensionOnly ? "Thanh chịu kéo / thanh giằng: [λ] = 300" : "Thanh chịu nén: [λ] = 120");

        // 6. Ổn định cục bộ bản cánh
        if (isI)
        {
            double r1 = sec.TfTop > 0 ? (sec.BfTop - sec.Tw) / 2 / sec.TfTop : 0;
            double r2 = sec.TfBot > 0 ? (sec.BfBot - sec.Tw) / 2 / sec.TfBot : 0;
            tr.Update(ChkFlange, Math.Max(r1, r2), 0.5 * sqrtEf, "", "b0/tf ≤ 0.5√(E/f) (giới hạn đơn giản hoá)");
        }

        // 7. TTGH II
        if (f.Kind == FrameKind.Rafter && chains.TryGetValue(f.Id, out var chn) && chn.span > 1e-6)
        {
            double lim = chn.span / Math.Max(set.RafterDeflection, 1);
            double worst = 0; string wc = "";
            foreach (var (cname, cr) in sls)
            {
                double dz = Uz(cr, nHi.Id) - Uz(cr, nLo.Id);
                if (Math.Abs(dz) > Math.Abs(worst)) { worst = dz; wc = cname; }
            }
            res.Deflection = Math.Abs(worst); res.DeflectionLimit = lim;
            tr.Update(ChkDeflection, Math.Abs(worst) * 1000, lim * 1000, "mm",
                $"{(wc == "" ? "-" : wc)}; Δ = UZ(đỉnh) − UZ(đầu kèo); [Δ] = L/{F(set.RafterDeflection)} với L = {F(chn.span, "0.00")} m");
        }
        else if (f.Kind == FrameKind.SteelColumn)
        {
            double H = Math.Abs(nHi.Z - nLo.Z);
            if (H > 1e-6)
            {
                double lim = H / Math.Max(set.ColumnDrift, 1);
                double worst = 0; string wc = "";
                foreach (var (cname, cr) in sls)
                {
                    double dx = Disp(cr, nHi.Id, 0) - Disp(cr, nLo.Id, 0);
                    double dy = Disp(cr, nHi.Id, 1) - Disp(cr, nLo.Id, 1);
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d > worst) { worst = d; wc = cname; }
                }
                res.Deflection = worst; res.DeflectionLimit = lim;
                tr.Update(ChkDrift, worst * 1000, lim * 1000, "mm",
                    $"{(wc == "" ? "-" : wc)}; [Δ] = H/{F(set.ColumnDrift)} với H = {F(H, "0.00")} m");
            }
        }

        // sắp xếp hạng mục theo thứ tự cố định
        string[] order = { ChkStrength, ChkShear, ChkInPlane, ChkOutPlane, ChkSlender, ChkFlange, ChkWeb, ChkDeflection, ChkDrift };
        res.Checks = tr.Items.OrderBy(c => { int k = Array.IndexOf(order, c.Name); return k < 0 ? 99 : k; }).ToList();
        if (res.Checks.Count == 0) res.Messages.Add("LỖI: không có nội lực của thanh trong các tổ hợp TTGH I.");
        res.Messages.Add("Công thức kiểm tra là dạng đơn giản hoá – cần kiểm tra lại theo TCVN 5575:2024.");
        if (res.Checks.Any(c => c.Name == ChkWeb && !c.Ok))
            res.Messages.Add("Bản bụng vượt giới hạn đơn giản hoá: cần kiểm tra ổn định cục bộ chi tiết hoặc bố trí sườn gia cường.");
        return res;
    }

    private static double Disp(CaseResult cr, int node, int dof) =>
        cr.Disp.TryGetValue(node, out var d) && d != null && d.Length > dof ? d[dof] : 0;
    private static double Uz(CaseResult cr, int node) => Disp(cr, node, 2);

    // ==================================================================
    // XÀ GỒ
    // ==================================================================
    /// <summary>
    /// Kiểm tra xà gồ đơn giản (dầm đơn giản một nhịp) chịu uốn xiên.
    /// Tải (kN/m² mặt mái): qDead, qLive theo phương trọng lực; wind vuông góc mặt mái (dương = ép vào mái, âm = hút/bốc).
    /// Phân tải: qn = q·cosα·a (vuông góc mái, uốn quanh trục 3), qt = q·sinα·a (song song mái, uốn quanh trục 2); a = spacing.
    /// Tổ hợp TTGH I: 1.1D + 1.3L; 1.1D + 0.9(1.3L + 1.2W); 0.9D + 1.2W.
    /// Mx = qn·L²/8; My = qt·L²/8 (chưa kể thanh chống xà gồ); σ = Mx/W33 + My/W22 ≤ f·γc; τ = V/As2 ≤ 0.58·f·γc.
    /// Độ võng TTGH II (D + L; D + W): f = 5·qn·L⁴/(384·E·I33) ≤ L/200.
    /// Đặc trưng tiết diện General: GW33, GW22, GI33 (xà gồ C/Z). Không kể oằn xoắn/ổn định cục bộ thép thành mỏng.
    /// </summary>
    public static SteelMemberCheck CheckPurlin(double span, double spacing, double slopeDeg, double qDead, double qLive, double wind,
        FrameSection sec, Material mat, SteelDesignSettings set)
    {
        set ??= new SteelDesignSettings();
        var res = new SteelMemberCheck { Name = "Xà gồ " + (sec?.Name ?? ""), Section = sec?.Name ?? "", Quantity = 1 };
        if (sec == null || mat == null || span <= 0 || spacing <= 0)
        {
            res.Messages.Add("LỖI: thiếu tiết diện, vật liệu hoặc nhịp/bước xà gồ.");
            return res;
        }
        var sp = sec.Props(0.5);
        double W33 = sec.Shape == SectionShape.General ? sec.GW33 : sp.W33;
        double W22 = sec.Shape == SectionShape.General ? sec.GW22 : sp.W22;
        double I33 = sec.Shape == SectionShape.General ? sec.GI33 : sp.I33;
        double As2 = sp.As2 > 0 ? sp.As2 : sp.A * 0.5;
        double gc = set.GammaC > 0 ? set.GammaC : 1.0;
        double fd = mat.F * gc, fvd = 0.58 * mat.F * gc;
        double a = slopeDeg * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
        double L = span;

        var combos = new List<(string name, double grav, double wn)>
        {
            ("1.1TT + 1.3HT", 1.1 * qDead + 1.3 * qLive, 0),
            ("1.1TT + 0.9(1.3HT + 1.2GIÓ)", 1.1 * qDead + 0.9 * 1.3 * qLive, 0.9 * 1.2 * wind),
            ("0.9TT + 1.2GIÓ", 0.9 * qDead, 1.2 * wind)
        };
        var tr = new Tracker();
        double gov = -1;
        foreach (var (name, grav, wn) in combos)
        {
            double qn = (grav * c + wn) * spacing;
            double qt = grav * s * spacing;
            double Mx = qn * L * L / 8, My = qt * L * L / 8, V = Math.Abs(qn) * L / 2;
            double sigma = SafeDiv(Math.Abs(Mx), W33) + SafeDiv(Math.Abs(My), W22);
            double tau = SafeDiv(V, As2);
            string note = $"{name}: qn = {F(qn, "0.000")} kN/m, qt = {F(qt, "0.000")} kN/m, Mx = {F(Mx, "0.00")} kN.m, My = {F(My, "0.00")} kN.m";
            tr.Update("Bền uốn hai phương", sigma / 1000, fd / 1000, "MPa", note);
            tr.Update(ChkShear, tau / 1000, fvd / 1000, "MPa", $"{name}: V = {F(V, "0.00")} kN");
            if (sigma / fd > gov) { gov = sigma / fd; res.Combo = name; res.M = Mx; res.V = V; res.Station = L / 2; }
        }
        // TTGH II
        double lim = L / 200;
        double worst = 0; string wcn = "";
        foreach (var (name, qn) in new[] { ("TT + HT", (qDead + qLive) * c * spacing), ("TT + GIÓ", (qDead * c + wind) * spacing) })
        {
            double d = I33 > 0 ? 5 * Math.Abs(qn) * Math.Pow(L, 4) / (384 * mat.E * I33) : 1e3;
            if (d > worst) { worst = d; wcn = name; }
        }
        res.Deflection = worst; res.DeflectionLimit = lim;
        tr.Update("Độ võng xà gồ (TTGH II)", worst * 1000, lim * 1000, "mm", $"{wcn}; [f] = L/200, L = {F(L, "0.00")} m");
        res.Checks = tr.Items;
        res.Lambda = 0;
        res.Messages.Add($"Nhịp {F(L, "0.00")} m, bước {F(spacing, "0.00")} m, độ dốc {F(slopeDeg, "0.0")}°; tải mái tính trên m² mặt mái.");
        res.Messages.Add("Chưa kể thanh chống xà gồ (sag rod), oằn xoắn khi gió bốc (cánh dưới chịu nén) và ổn định cục bộ thép thành mỏng – cần kiểm tra theo TCVN 5575:2024 / tiêu chuẩn thép thành mỏng.");
        return res;
    }
}
