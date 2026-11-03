using KetCauVXT.Analysis;
using KetCauVXT.Loads;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng phân tích dao động riêng và phổ phản ứng (Analysis/FrameAnalysis.Modal.cs, Analysis/ModalSolver.cs).
/// check(tên, giá trị, kỳ vọng, sai số tương đối). Lời giải giải tích:
/// (a) cột công xôn + khối lượng đỉnh: k = 1/(L³/3EI + L/GAs), T = 2π√(m/k);
/// (b) thanh công xôn khối lượng phân bố (Euler–Bernoulli): ωn = βn²·√(EI/(m̄L⁴)), β1..3 = 1,8751; 4,6941; 7,8548;
/// (c) nhà khung cứng 2 tầng (dầm tuyệt đối cứng): det(K − ω²M) = 0 dạng 2×2;
/// (d) 1 tầng, sàn cứng, khối lượng lệch tâm: dao động xoắn ghép, Σ tỷ lệ khối lượng = 100 % khi tính đủ mọi dạng;
/// (e) tổng khối lượng = Σ(G + ψE·Q)/g; (f) phổ phản ứng một bậc tự do: Vđáy = m·Sd(T)·g; CQC/SRSS;
/// (g) nhà phố 4 tầng: T1 so với Ct·H^0,75; (h) mặc định vẫn là tĩnh lực ngang tương đương, kết quả tĩnh không đổi.</summary>
public static class ModalTests
{
    private const double g = ModalResult.G;
    private static double B(bool b) => b ? 1 : 0;

    private static Project Base()
    {
        var p = new Project();
        p.Materials.Add(Material.Concrete("B25"));
        p.FrameSections.Add(FrameSection.Rect("C30x30", 300, 300));
        p.FrameSections.Add(FrameSection.Rect("D30x60", 300, 600));
        p.SlabSections.Add(new SlabSection { Name = "S100", Thickness = 0.10 });
        return p;
    }

    /// <summary>Khối lượng chỉ từ trường hợp "M" (tải nút Fz).</summary>
    private static void MassFromM(Project p)
    {
        p.Patterns.Add(new LoadPattern { Name = "M", Type = LoadType.Dead });
        p.Modal.UseDefaultMassSource = false;
        p.Modal.MassSource = new List<MassSourceItem> { new("M", 1.0) };
    }

    /// <summary>Độ cứng ngang đỉnh cột công xôn kể biến dạng cắt (Timoshenko – đúng như phần tử của chương trình).</summary>
    private static double CantileverK(Project p, double L)
    {
        var sp = p.Section("C30x30").Props(); var m = p.Material("B25");
        return 1.0 / (L * L * L / (3 * m.E * sp.I33) + L / (m.G * sp.As2));
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Directory.CreateDirectory(outDir);

        // ---------------- 0. đại số ----------------
        {
            var a = new double[,] { { 4, 1, 0 }, { 1, 3, 1 }, { 0, 1, 2 } };
            ModalSolver.SymmetricEigen(a, out var d, out var v);
            double res = 0;
            for (int k = 0; k < 3; k++)
                for (int i = 0; i < 3; i++)
                {
                    double s = 0; for (int j = 0; j < 3; j++) s += a[i, j] * v[j, k];
                    res = Math.Max(res, Math.Abs(s - d[k] * v[i, k]));
                }
            check("Dao động: Householder/QL – A·v = λ·v", res, 0, 1e-10);
            check("Dao động: Householder/QL – vết = Σλ", d.Sum(), 9, 1e-12);
            check("Dao động: Householder/QL – trị riêng tăng dần", B(d[0] <= d[1] && d[1] <= d[2]), 1, 0);
        }

        // ---------------- (a) cột công xôn + khối lượng đỉnh ----------------
        double Ttip = 0, mTip = 0;
        {
            var p = Base(); MassFromM(p);
            double L = 3.5, W = 100;
            p.Nodes.Add(new Node { Id = 1, Z = 0, Restraint = Node.Fixed });
            p.Nodes.Add(new Node { Id = 2, Z = L });
            p.Frames.Add(new FrameElement { Id = 1, NodeI = 1, NodeJ = 2, Section = "C30x30", Kind = FrameKind.Column });
            p.NodeLoads.Add(new NodeLoad { Node = 2, Pattern = "M", Fz = -W });
            var r = FrameAnalysis.Run(p);
            var mr = r.Modal;
            check("(a) Có kết quả dao động riêng", B(mr != null && mr.Modes.Count == 2), 1, 0);
            double k = CantileverK(p, L), m = W / g;
            double T = 2 * Math.PI * Math.Sqrt(m / k);
            Ttip = T; mTip = m;
            check("(a) Cột công xôn + khối lượng đỉnh: T = 2π√(m/k)", mr.Modes[0].Period, T, 1e-6);
            check("(a) Hai dạng X, Y cùng chu kỳ (tiết diện vuông)", mr.Modes[1].Period, T, 1e-6);
            check("(a) Tổng khối lượng tham gia = W/g", mr.TotalMass[0], m, 1e-12);
            check("(a) Σ tỷ lệ khối lượng UX (2 dạng) = 100 %", mr.SumRatio(0), 1, 1e-9);
            check("(a) Σ tỷ lệ khối lượng UY (2 dạng) = 100 %", mr.SumRatio(1), 1, 1e-9);
            check("(a) Dạng chuẩn hoá khối lượng: m·u² = 1", m * (Math.Pow(mr.Modes[0].Disp[2][0], 2) + Math.Pow(mr.Modes[0].Disp[2][1], 2)), 1, 1e-9);
            check("(a) Mặc định: phương pháp động đất = tĩnh lực ngang tương đương", B(p.Seismic.Method == SeismicMethods.Static && !p.Seismic.IsResponseSpectrum), 1, 0);
        }

        // ---------------- (b) công xôn khối lượng phân bố ----------------
        foreach (int nEl in new[] { 10, 20 })
        {
            var p = Base();
            p.Patterns.Add(new LoadPattern { Name = "TT", Type = LoadType.Dead, SelfWeight = 1 });   // nguồn khối lượng mặc định: TT × 1
            double L = 20;
            for (int i = 0; i <= nEl; i++)
            {
                var rs = new bool[6]; rs[1] = true;   // chỉ uốn trong mặt phẳng XZ
                if (i == 0) rs = Node.Fixed;
                p.Nodes.Add(new Node { Id = i + 1, Z = L * i / nEl, Restraint = rs });
            }
            for (int i = 0; i < nEl; i++) p.Frames.Add(new FrameElement { Id = i + 1, NodeI = i + 1, NodeJ = i + 2, Section = "C30x30", Kind = FrameKind.Column });
            p.Modal.NumModes = nEl;   // lấy đủ mọi dạng để kiểm Σ tỷ lệ khối lượng = 100 %
            var mr = FrameAnalysis.Run(p).Modal;
            var sp = p.Section("C30x30").Props(); var mat = p.Material("B25");
            double mbar = mat.UnitWeight * sp.A / g;
            double w0 = Math.Sqrt(mat.E * sp.I33 / (mbar * Math.Pow(L, 4)));
            double[] beta = { 1.875104, 4.694091, 7.854757 };
            check($"(b) Công xôn {nEl} phần tử: số dạng = số bậc tự do ux ({nEl})", mr.Modes.Count, nEl, 0);
            check($"(b) Công xôn {nEl} phần tử: tổng khối lượng tham gia = m̄L·(1 − 1/(2n))", mr.TotalMass[0], mbar * L * (1 - 0.5 / nEl), 1e-9);
            for (int k = 0; k < (nEl >= 20 ? 3 : 2); k++)
                check($"(b) Công xôn {nEl} phần tử: ω{k + 1} = β{k + 1}²·√(EI/(m̄L⁴)) (≤ {(nEl >= 20 ? 1.5 : 2)} %)", mr.Modes[k].Omega, beta[k] * beta[k] * w0, nEl >= 20 ? 0.015 : 0.02);
            check($"(b) Công xôn {nEl} phần tử: Σ tỷ lệ khối lượng mọi dạng = 100 %", mr.SumRatio(0), 1, 1e-8);
            check($"(b) Công xôn {nEl} phần tử: Meff dạng 1 / m̄L ≈ 0,613 (lời giải liên tục)", mr.Modes[0].Ratio[0] * mr.TotalMass[0] / (mbar * L), 0.6131, 0.01);
            if (nEl == 20)
            {
                // lặp không gian con cho cùng kết quả với giải đầy đủ
                p.Modal.Solver = ModalSettings.SolverSubspace; p.Modal.NumModes = 4;
                var ms = FrameAnalysis.Run(p).Modal;
                for (int k = 0; k < 3; k++)
                    check($"(b) Lặp không gian con = giải đầy đủ: T{k + 1}", ms.Modes[k].Period, mr.Modes[k].Period, 1e-6);
            }
        }

        // ---------------- (c) nhà khung cứng 2 tầng ----------------
        {
            var p = Base(); MassFromM(p);
            double h = 3.3, span = 5;
            var free = new bool[6]; free[1] = free[2] = free[3] = free[4] = free[5] = true;   // chỉ còn UX: dầm tuyệt đối cứng, không xoay
            int id = 1;
            p.Nodes.Add(new Node { Id = id++, X = 0, Z = 0, Restraint = Node.Fixed });
            p.Nodes.Add(new Node { Id = id++, X = span, Z = 0, Restraint = Node.Fixed });
            for (int s = 1; s <= 2; s++)
            {
                p.Nodes.Add(new Node { Id = id++, X = 0, Z = s * h, Restraint = (bool[])free.Clone() });
                p.Nodes.Add(new Node { Id = id++, X = span, Z = s * h, Restraint = (bool[])free.Clone() });
            }
            int fid = 1;
            for (int s = 0; s < 2; s++)
            {
                p.Frames.Add(new FrameElement { Id = fid++, NodeI = 1 + 2 * s, NodeJ = 3 + 2 * s, Section = "C30x30", Kind = FrameKind.Column });
                p.Frames.Add(new FrameElement { Id = fid++, NodeI = 2 + 2 * s, NodeJ = 4 + 2 * s, Section = "C30x30", Kind = FrameKind.Column });
                p.Frames.Add(new FrameElement { Id = fid++, NodeI = 3 + 2 * s, NodeJ = 4 + 2 * s, Section = "D30x60", Kind = FrameKind.Beam });
            }
            double W1 = 300, W2 = 200;   // kN mỗi tầng (chia đôi 2 nút)
            foreach (var (n, w) in new[] { (3, W1 / 2), (4, W1 / 2), (5, W2 / 2), (6, W2 / 2) })
                p.NodeLoads.Add(new NodeLoad { Node = n, Pattern = "M", Fz = -w });
            var mr = FrameAnalysis.Run(p).Modal;
            var sp = p.Section("C30x30").Props(); var mat = p.Material("B25");
            double phi = 12 * mat.E * sp.I33 / (mat.G * sp.As2 * h * h);
            double k = 2 * 12 * mat.E * sp.I33 / (h * h * h * (1 + phi));
            double m1 = W1 / g, m2 = W2 / g;
            // m1·m2·λ² − (m1·k + m2·2k)·λ + k² = 0
            double a = m1 * m2, b = -(m1 * k + m2 * 2 * k), c = k * k;
            double l1 = (-b - Math.Sqrt(b * b - 4 * a * c)) / (2 * a), l2 = (-b + Math.Sqrt(b * b - 4 * a * c)) / (2 * a);
            var sym = mr.Modes.Where(m => m.Ratio[0] > 1e-6).ToList();   // dạng đối xứng (hai nút cùng tầng chuyển vị như nhau)
            check("(c) Nhà 2 tầng: ω1² = nghiệm nhỏ của det(K − ω²M)", sym[0].Omega * sym[0].Omega, l1, 1e-6);
            check("(c) Nhà 2 tầng: ω2² = nghiệm lớn", sym[1].Omega * sym[1].Omega, l2, 1e-6);
            check("(c) Nhà 2 tầng: tỷ số dạng 1 u2/u1 = (2k − λ1·m1)/k", sym[0].Disp[5][0] / sym[0].Disp[3][0], (2 * k - l1 * m1) / k, 1e-6);
            check("(c) Nhà 2 tầng: Σ tỷ lệ khối lượng = 100 %", mr.SumRatio(0), 1, 1e-8);
        }

        // ---------------- (d) 1 tầng, sàn cứng, khối lượng lệch tâm ----------------
        {
            var p = Base(); MassFromM(p);
            p.Stories.Add(new Story { Name = "T1", Elevation = 3.5, Height = 3.5 });
            double Lx = 6, Ly = 4, H = 3.5;
            var pts = new[] { (0.0, 0.0), (Lx, 0.0), (Lx, Ly), (0.0, Ly) };
            for (int i = 0; i < 4; i++)
            {
                p.Nodes.Add(new Node { Id = i + 1, X = pts[i].Item1, Y = pts[i].Item2, Z = 0, Restraint = Node.Fixed });
                p.Nodes.Add(new Node { Id = i + 11, X = pts[i].Item1, Y = pts[i].Item2, Z = H, Story = "T1" });
                p.Frames.Add(new FrameElement { Id = i + 1, NodeI = i + 1, NodeJ = i + 11, Section = "C30x30", Kind = FrameKind.Column, Story = "T1" });
                p.Frames.Add(new FrameElement { Id = i + 11, NodeI = i + 11, NodeJ = (i + 1) % 4 + 11, Section = "D30x60", Kind = FrameKind.Beam, Story = "T1" });
            }
            p.Slabs.Add(new Slab { Id = 1, Nodes = new List<int> { 11, 12, 13, 14 }, Section = "S100", Story = "T1" });
            // khối lượng lệch: nút (6;4) nặng hơn
            double[] Wn = { 100, 100, 300, 100 };
            for (int i = 0; i < 4; i++) p.NodeLoads.Add(new NodeLoad { Node = i + 11, Pattern = "M", Fz = -Wn[i] });
            var r = FrameAnalysis.Run(p);
            var mr = r.Modal;
            check("(d) Sàn cứng: 3 bậc tự do động (UX, UY, RZ nút chủ)", mr.DynamicDofs, 3, 0);
            check("(d) Sàn cứng: 3 dạng", mr.Modes.Count, 3, 0);
            check("(d) Σ UX = 100 %", mr.SumRatio(0), 1, 1e-9);
            check("(d) Σ UY = 100 %", mr.SumRatio(1), 1, 1e-9);
            check("(d) Σ RZ = 100 %", mr.SumRatio(3), 1, 1e-9);
            double M = Wn.Sum() / g;
            double xc = (Wn[1] * Lx + Wn[2] * Lx) / Wn.Sum(), yc = (Wn[2] * Ly + Wn[3] * Ly) / Wn.Sum();
            check("(d) Tâm khối lượng Xm", mr.Xc, xc, 1e-9);
            check("(d) Tâm khối lượng Ym", mr.Yc, yc, 1e-9);
            double Ip = 0;
            for (int i = 0; i < 4; i++) Ip += Wn[i] / g * (Math.Pow(pts[i].Item1 - xc, 2) + Math.Pow(pts[i].Item2 - yc, 2));
            check("(d) Mô men quán tính khối lượng Σm·r²", mr.TotalMass[3], Ip, 1e-9);
            check("(d) Khối lượng tầng", mr.Stories.Count == 1 ? mr.Stories[0].Mass : 0, M, 1e-9);
            var m1 = mr.Modes[0];
            check("(d) Dạng 1 ghép xoắn – tịnh tiến (UX+UY) và xoắn RZ đều > 1 %", B(m1.Ratio[0] + m1.Ratio[1] > 0.01 && m1.Ratio[3] > 0.01), 1, 0);
            check("(d) Tâm cứng (4 cột như nhau) Xr = Lx/2", mr.Stories[0].Xr, Lx / 2, 1e-6);
            check("(d) Tâm cứng Yr = Ly/2", mr.Stories[0].Yr, Ly / 2, 1e-6);
            // khối lượng đều → dạng thuần tịnh tiến/xoắn (không ghép)
            foreach (var nl in p.NodeLoads.Where(l => l.Pattern == "M")) nl.Fz = -100;
            var mu = FrameAnalysis.Run(p).Modal;
            check("(d) Khối lượng đều: dạng 1 không ghép xoắn (RZ < 1e-6)", mu.Modes[0].Ratio[3], 0, 1e-6);
            Console.WriteLine($"   · (d) lệch tâm: T = {string.Join("; ", mr.Modes.Select(x => $"{x.Period:0.0000} s [{x.Dominant}, UX {x.Ratio[0] * 100:0.0}%, UY {x.Ratio[1] * 100:0.0}%, RZ {x.Ratio[3] * 100:0.0}%]"))}");
        }

        // ---------------- (f) phổ phản ứng ----------------
        {
            // hệ số tương quan CQC
            double zeta = 0.05, r = 1 / 0.95;
            double rho = 8 * zeta * zeta * (1 + r) * Math.Pow(r, 1.5) / (Math.Pow(1 - r * r, 2) + 4 * zeta * zeta * r * Math.Pow(1 + r, 2));
            check("(f) CQC: ρ (T2/T1 = 0,95; ζ = 5 %) theo công thức Der Kiureghian", ModalSolver.CqcRho(1.0, r, zeta), rho, 1e-12);
            check("(f) CQC: ρ ≈ 0,79 với hai dạng gần nhau", ModalSolver.CqcRho(1.0, r, zeta), 0.79, 0.01);
            var close = ModalSolver.Correlation(new[] { 1.0, r }, true, zeta);
            var far = ModalSolver.Correlation(new[] { 1.0, 3.0 }, true, zeta);
            var srss = ModalSolver.Correlation(new[] { 1.0, r }, false, zeta);
            double[] rr = { 1, 1 };
            check("(f) Hai dạng xa nhau (ω2/ω1 = 3): CQC ≈ SRSS (≤ 1 %)", ModalSolver.Combine(rr, far), ModalSolver.Combine(rr, srss), 0.01);
            check("(f) Hai dạng gần nhau: CQC = √(2 + 2ρ) ≠ SRSS = √2", ModalSolver.Combine(rr, close), Math.Sqrt(2 + 2 * rho), 1e-12);
            check("(f) Hai dạng gần nhau: CQC/SRSS > 1,3", B(ModalSolver.Combine(rr, close) / ModalSolver.Combine(rr, srss) > 1.3), 1, 0);

            // một bậc tự do: cột công xôn + khối lượng đỉnh, chỉ dao động theo X
            var p = Base(); MassFromM(p);
            double L = 3.5, W = 100;
            p.Nodes.Add(new Node { Id = 1, Z = 0, Restraint = Node.Fixed });
            var rs = new bool[6]; rs[1] = true;
            p.Nodes.Add(new Node { Id = 2, Z = L, Restraint = rs });
            p.Frames.Add(new FrameElement { Id = 1, NodeI = 1, NodeJ = 2, Section = "C30x30", Kind = FrameKind.Column });
            p.NodeLoads.Add(new NodeLoad { Node = 2, Pattern = "M", Fz = -W });
            p.Patterns.Add(new LoadPattern { Name = "DDX", Type = LoadType.Seismic });
            p.Seismic.Enabled = true; p.Seismic.AgR = 0.1; p.Seismic.Soil = "C"; p.Seismic.Method = SeismicMethods.ResponseSpectrum;
            var res = FrameAnalysis.Run(p);
            var mr = res.Modal;
            check("(f) Một bậc tự do: có kết quả phổ phản ứng", B(mr != null && mr.Spectrum.Count == 1), 1, 0);
            double T = mr.Modes[0].Period;
            check("(f) Một bậc tự do: T như (a)", T, Ttip, 1e-6);
            double V = mTip * LateralLoadGenerator.Sd(T, p.Seismic) * g;
            check("(f) Một bậc tự do: Vđáy = m·Sd(T)·g", mr.Spectrum[0].BaseShear, V, 1e-9);
            check("(f) Một bậc tự do: phản lực FX của DDX (không dấu) = m·Sd·g", Math.Abs(res.Get("DDX").Reactions[1][0]), V, 1e-6);
            check("(f) Một bậc tự do: mô men chân cột = V·L", Math.Abs(res.Get("DDX").Reactions[1][4]), V * L, 1e-6);
            check("(f) Một bậc tự do: chuyển vị đỉnh = Sd·g/ω²", res.Get("DDX").Disp[2][0], LateralLoadGenerator.Sd(T, p.Seismic) * g / Math.Pow(mr.Modes[0].Omega, 2), 1e-6);
        }

        // ---------------- (e)(g)(h) nhà phố 4 tầng ----------------
        {
            Project House()
            {
                var h = ModelBuilder.NewProject("Nhà phố mẫu 4 tầng – dao động");
                ModelBuilder.SetGrid(h, new[] { 5.0 }, new[] { 4.5, 4.5, 4.5 });
                ModelBuilder.SetStories(h, new[] { 3.9, 3.3, 3.3, 3.3 }, -0.6);
                ModelBuilder.BuildFromGrid(h, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5 });
                h.Seismic.Enabled = true; h.Seismic.AgR = 0.0611; h.Seismic.Soil = "C";
                foreach (var n in new[] { "DDX", "DDY" })
                    if (h.Pattern(n) == null) h.Patterns.Add(new LoadPattern { Name = n, Type = LoadType.Seismic, Gamma = 1.0 });
                ComboGenerator.Generate(h);
                return h;
            }
            var house = House();
            var hr = FrameAnalysis.Run(house);
            var mr = hr.Modal;
            check("(g) Nhà phố: có kết quả dao động riêng", B(mr != null && mr.Modes.Count > 0), 1, 0);

            // (e) tổng khối lượng = Σ(G + ψE·Q)/g
            double expect = 0;
            foreach (var (pat, f) in house.Modal.Effective(house))
            {
                double w = 0;
                if (pat.SelfWeight != 0)
                    foreach (var fr in house.Frames)
                    {
                        var s = house.Section(fr.Section);
                        w += pat.SelfWeight * house.Material(s.Material).UnitWeight * s.Props().A * house.Length(fr);
                    }
                foreach (var fl in house.FrameLoads.Where(l => l.Pattern == pat.Name && l.Dir == LoadDir.Gravity))
                {
                    double Lf = house.Length(house.Frame(fl.Element));
                    for (int i = 0; i + 1 < fl.Points.Count; i++) w += (fl.Points[i][1] + fl.Points[i + 1][1]) / 2 * (fl.Points[i + 1][0] - fl.Points[i][0]) * Lf;
                    w += fl.PointLoads.Sum(x => x[1]);
                }
                foreach (var nl in house.NodeLoads.Where(l => l.Pattern == pat.Name && l.Source != "WIND" && l.Source != "EQ")) w -= nl.Fz;
                expect += f * w;
            }
            check("(e) Nguồn khối lượng mặc định: TT 1 + HTHT 1 + HT 0,24", B(house.Modal.Describe(house).Contains("0.24·HT") && house.Modal.Describe(house).Contains("1·TT")), 1, 0);
            check("(e) Tổng khối lượng (tham gia + tại gối) = Σ(G + ψE·Q)/g", mr.TotalMass[0] + mr.BaseMass, expect / g, 1e-9);
            check("(e) Σ khối lượng tầng = tổng khối lượng tham gia", mr.Stories.Sum(s => s.Mass), mr.TotalMass[0], 1e-6);

            // (g) T1 hợp lý so với Ct·H^0,75
            double T1 = mr.First.Period;
            Console.WriteLine($"   · (g) Nhà phố 4 tầng: T1 = {T1:0.000} s (phương {mr.First.Dominant}), Ct·H^0,75 = {mr.T1Formula:0.000} s, {mr.Modes.Count} dạng, {mr.DynamicDofs} bậc tự do động; {mr.Solver}");
            foreach (var m in mr.Modes.Take(6))
                Console.WriteLine($"     dạng {m.Index}: T = {m.Period:0.000} s, UX {m.Ratio[0] * 100:0.0}% (Σ {m.Cumulative[0] * 100:0.0}%), UY {m.Ratio[1] * 100:0.0}% (Σ {m.Cumulative[1] * 100:0.0}%), RZ {m.Ratio[3] * 100:0.0}% – {m.Dominant}");
            check("(g) T1 trong khoảng hợp lý 0,5…5 lần Ct·H^0,75 (khung thuần, độ cứng tiết diện nguyên)", B(T1 > 0.5 * mr.T1Formula && T1 < 5 * mr.T1Formula), 1, 0);
            check("(g) Phương X đạt ≥ 90 % khối lượng trong số dạng đã tính", B(mr.ModesToReach(0) > 0), 1, 0);
            check("(g) Phương Y đạt ≥ 90 % khối lượng trong số dạng đã tính", B(mr.ModesToReach(1) > 0), 1, 0);
            check("(g) Sàn cứng: số bậc tự do động ≈ 3 × số tầng", B(mr.Diaphragm && mr.DynamicDofs >= 12 && mr.DynamicDofs <= 3 * 4 + 40), 1, 0);
            check("(g) Khối lượng tầng: 4 tầng", mr.Stories.Count, 4, 0);
            check("(g) Ghi chú gió: f1 so với fL", B(mr.Notes.Any(n => n.Contains("fL"))), 1, 0);

            // full 3D (không sàn cứng) – lặp không gian con
            var h3 = House(); h3.UseRigidDiaphragm = false; h3.Modal.Solver = ModalSettings.SolverSubspace; h3.Modal.NumModes = 6;
            var m3 = FrameAnalysis.Run(h3).Modal;
            var h3d = House(); h3d.UseRigidDiaphragm = false; h3d.Modal.NumModes = 6;
            var m3d = FrameAnalysis.Run(h3d).Modal;
            Console.WriteLine($"   · (g) Không sàn cứng: T1 = {m3.First.Period:0.000} s ({m3.DynamicDofs} bậc tự do động, {m3.Iterations} vòng lặp); giải đầy đủ T1 = {m3d.First.Period:0.000} s");
            check("(g) Không sàn cứng: lặp không gian con = giải đầy đủ (T1)", m3.First.Period, m3d.First.Period, 1e-6);
            check("(g) Không sàn cứng: lặp không gian con = giải đầy đủ (T3)", m3.Modes[2].Period, m3d.Modes[2].Period, 1e-6);
            check("(g) Không sàn cứng: T1 ≥ T1 sàn cứng", B(m3.First.Period >= T1 - 1e-9), 1, 0);

            // (h) mặc định: tĩnh lực ngang tương đương, kết quả tĩnh không đổi khi bật/tắt dao động riêng
            var off = House(); off.Modal.Enabled = false;
            var ro = FrameAnalysis.Run(off);
            check("(h) Tắt dao động riêng: không có kết quả modal", B(ro.Modal == null), 1, 0);
            double diff = 0;
            foreach (var cname in new[] { "TT", "HT", "DDX", "DDY", "GX" })
                foreach (var (fid, ff) in ro.Get(cname).Frames)
                {
                    var f2 = hr.Get(cname).Frames[fid];
                    for (int i = 0; i < ff.Count; i++) for (int c = 0; c < 6; c++) diff = Math.Max(diff, Math.Abs(ff.F[i][c] - f2.F[i][c]));
                }
            check("(h) Mặc định (tĩnh lực ngang tương đương): nội lực TT/HT/DDX/DDY/GX không đổi khi chạy dao động riêng", diff, 0, 1e-12);
            check("(h) Lực ngang tầng DDX không đổi", hr.LateralForces.Where(x => x.Pattern == "DDX").Sum(x => x.Fx), ro.LateralForces.Where(x => x.Pattern == "DDX").Sum(x => x.Fx), 1e-12);

            // T1 theo dao động riêng cho phương pháp tĩnh
            var ht = House(); ht.Modal.UseModalT1 = true;
            var rt = FrameAnalysis.Run(ht);
            check("(h) Tuỳ chọn T1 theo dao động: lực tầng ghi T1 dao động", B(rt.LateralForces.Any(x => x.Pattern == "DDX" && x.Note.Contains($"T1={rt.Modal.Dominant(0).Period:0.000}"))), 1, 0);

            // phổ phản ứng trên nhà phố
            var hs = House(); hs.Seismic.Method = SeismicMethods.ResponseSpectrum;
            var rsr = FrameAnalysis.Run(hs);
            var sp = rsr.Modal.Spectrum;
            check("(f) Nhà phố phổ phản ứng: có 2 trường hợp DDX, DDY", sp.Count, 2, 0);
            double minV = double.MaxValue;
            foreach (var cr in new[] { "DDX", "DDY" })
                foreach (var ff in rsr.Get(cr).Frames.Values) foreach (var row in ff.F) foreach (var v in row) minV = Math.Min(minV, v);
            check("(f) Kết quả phổ phản ứng không dấu (≥ 0)", B(minV >= 0), 1, 0);
            foreach (var rc in sp)
            {
                Console.WriteLine($"   · (f) {rc.Pattern}: V phổ = {rc.BaseShear:0.0} kN, Fb tĩnh (T = {rc.StaticPeriod:0.000} s) = {rc.StaticBaseShear:0.0} kN, Σ khối lượng {rc.MassRatio * 100:0.0}%");
                check($"(f) {rc.Pattern}: V phổ / Fb tĩnh trong khoảng 0,3…1,3", B(rc.BaseShear > 0.3 * rc.StaticBaseShear && rc.BaseShear < 1.3 * rc.StaticBaseShear), 1, 0);
                check($"(f) {rc.Pattern}: có mô men xoắn ngẫu nhiên tầng", B(rc.Torsion.Count == 4 && rc.Torsion.All(t => t.moment > 0)), 1, 0);
            }
            var uls = rsr.DesignCombos().First(c => hs.Combinations.First(x => x.Name == c).Items.Any(i => i.Case == "DDX"));
            check("(f) Tổ hợp có DDX tính được (±)", B(rsr.Get(uls).Frames.Count == hs.Frames.Count), 1, 0);
            // hiệu chỉnh lực cắt đáy
            var hk = House(); hk.Seismic.Method = SeismicMethods.ResponseSpectrum; hk.Modal.ScaleToStatic = 5.0;
            var rk = FrameAnalysis.Run(hk).Modal.Spectrum[0];
            check("(f) Hiệu chỉnh V phổ ≥ 5·Fb: hệ số = 5·Fb/V", rk.ScaleFactor, 5 * rk.StaticBaseShear / rk.BaseShear, 1e-9);
            // SRSS ≤ CQC khi các dạng cùng dấu đóng góp (lực cắt đáy)
            var hq = House(); hq.Seismic.Method = SeismicMethods.ResponseSpectrum; hq.Modal.Combination = "SRSS";
            var rq = FrameAnalysis.Run(hq).Modal.Spectrum[0];
            check("(f) Lực cắt đáy: SRSS ≤ CQC (đóng góp các dạng cùng dấu)", B(rq.BaseShear <= sp[0].BaseShear + 1e-9), 1, 0);

            // báo cáo + Excel
            try
            {
                string docx = Path.Combine(outDir, "DaoDongRieng.docx"), xlsx = Path.Combine(outDir, "DaoDongRieng.xlsx");
                CalculationReport.Export(hs, rsr, null, docx);
                ExcelReport.Export(hs, rsr, null, xlsx);
                string doc = ZipText(docx, "word/document.xml");
                check("Thuyết minh: có chương PHÂN TÍCH DAO ĐỘNG RIÊNG", B(doc.Contains("PHÂN TÍCH DAO ĐỘNG RIÊNG")), 1, 0);
                check("Thuyết minh: có mục phổ phản ứng", B(doc.Contains("Phân tích phổ phản ứng dạng dao động")), 1, 0);
                string wb = ZipText(xlsx, "xl/workbook.xml");
                check("Excel: có trang Dao động riêng", B(wb.Contains("Dao động riêng")), 1, 0);
            }
            catch (Exception ex) { check("Thuyết minh/Excel dao động riêng: " + ex.Message, 0, 1, 0); }
        }
    }

    private static string ZipText(string path, string entry)
    {
        using var z = System.IO.Compression.ZipFile.OpenRead(path);
        var e = z.GetEntry(entry);
        if (e == null) return "";
        using var sr = new StreamReader(e.Open());
        return sr.ReadToEnd();
    }
}
