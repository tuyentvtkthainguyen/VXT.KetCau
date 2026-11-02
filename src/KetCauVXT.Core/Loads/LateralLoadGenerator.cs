using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Loads;

/// <summary>Sinh tải ngang theo tầng:
/// – Gió tĩnh theo TCVN 2737:2023: W = W0 · k(z) · c · hệ số hiệu chỉnh (giá trị tiêu chuẩn; γf đưa vào tổ hợp).
/// – Động đất theo TCVN 9386-1:2025 (thay thế TCVN 9386:2012; số điều theo bản 2012), phương pháp phân tích tĩnh lực ngang tương đương (mục 4.3.3.2).
/// Lực tầng chia đều cho các nút đầu cột tại cao trình sàn (khi có sàn cứng, hợp lực đặt tại trọng tâm các nút).</summary>
public static class LateralLoadGenerator
{
    /// <summary>Hệ số k(z) – dạng hàm mũ theo dạng địa hình: k = 1.844·(z/zg)^(2α).
    /// A: α=0.07, zg=250 m; B: α=0.09, zg=300 m; C: α=0.14, zg=400 m. Người dùng đối chiếu bảng của tiêu chuẩn áp dụng.</summary>
    public static double Kz(double z, string terrain)
    {
        (double a, double zg) = terrain switch { "A" => (0.07, 250.0), "C" => (0.14, 400.0), _ => (0.09, 300.0) };
        z = Math.Max(z, 3.0);
        return 1.844 * Math.Pow(z / zg, 2 * a);
    }

    /// <summary>Phổ thiết kế Sd(T)/g theo TCVN 9386-1:2025 (thay thế TCVN 9386:2012; số điều theo bản 2012) (phổ loại 1), công thức (3.13)–(3.16).</summary>
    public static double Sd(double T, SeismicSettings s)
    {
        (double S, double TB, double TC, double TD) = s.Soil switch
        {
            "A" => (1.00, 0.15, 0.40, 2.0),
            "B" => (1.20, 0.15, 0.50, 2.0),
            "D" => (1.35, 0.20, 0.80, 2.0),
            "E" => (1.40, 0.15, 0.50, 2.0),
            _ => (1.15, 0.20, 0.60, 2.0)
        };
        double ag = s.AgR * s.ImportanceFactor;
        double q = s.Q, beta = 0.2;
        double v;
        if (T <= TB) v = ag * S * (2.0 / 3 + T / TB * (2.5 / q - 2.0 / 3));
        else if (T <= TC) v = ag * S * 2.5 / q;
        else if (T <= TD) v = Math.Max(ag * S * 2.5 / q * TC / T, beta * ag);
        else v = Math.Max(ag * S * 2.5 / q * TC * TD / (T * T), beta * ag);
        return v;
    }

    public static List<string> Apply(Project p, List<StoryLateralForce> report)
    {
        var warn = new List<string>();
        p.NodeLoads.RemoveAll(l => l.Source == "WIND" || l.Source == "EQ");
        report?.Clear();
        var stories = p.Stories.OrderBy(s => s.Elevation).Where(s => s.Elevation > p.BaseElevation + 1e-3).ToList();
        if (stories.Count == 0) return warn;

        var colTop = new Dictionary<string, List<int>>();
        foreach (var st in stories)
        {
            var ids = p.Frames.Where(f => f.IsVerticalKind)
                .SelectMany(f => new[] { f.NodeI, f.NodeJ })
                .Distinct().Select(id => p.Node(id))
                .Where(n => Math.Abs(n.Z - st.Elevation) < 1e-3).Select(n => n.Id).ToList();
            colTop[st.Name] = ids;
        }
        double minX = p.Nodes.Min(n => n.X), maxX = p.Nodes.Max(n => n.X);
        double minY = p.Nodes.Min(n => n.Y), maxY = p.Nodes.Max(n => n.Y);

        void Distribute(string pat, string src, Story st, double fx, double fy, string note)
        {
            var ids = colTop[st.Name];
            if (ids.Count == 0) { warn.Add($"Tầng {st.Name} không có nút đầu cột để đặt tải ngang"); return; }
            foreach (var id in ids)
                p.NodeLoads.Add(new NodeLoad { Node = id, Pattern = pat, Fx = fx / ids.Count, Fy = fy / ids.Count, Source = src });
            report?.Add(new StoryLateralForce { Pattern = pat, Story = st.Name, Elevation = st.Elevation, Fx = fx, Fy = fy, Note = note });
        }

        static bool IsY(string name) => name.ToUpperInvariant().Contains('Y');

        // ---------------- GIÓ ----------------
        if (p.Wind.Enabled)
            foreach (var pat in p.Patterns.Where(x => x.Type == LoadType.Wind))
            {
                bool y = IsY(pat.Name);
                double width = y ? (p.Wind.WidthForWindY > 0 ? p.Wind.WidthForWindY : maxX - minX)
                                 : (p.Wind.WidthForWindX > 0 ? p.Wind.WidthForWindX : maxY - minY);
                for (int i = 0; i < stories.Count; i++)
                {
                    var st = stories[i];
                    double below = st.Elevation - (i == 0 ? p.BaseElevation : stories[i - 1].Elevation);
                    double above = i == stories.Count - 1 ? 0 : stories[i + 1].Elevation - st.Elevation;
                    double h = below / 2 + above / 2;
                    double z = st.Elevation - p.BaseElevation;
                    double k = Kz(z, p.Wind.Terrain);
                    double w = p.Wind.W0 * k * p.Wind.Cp * p.Wind.Factor;
                    double F = w * h * width;
                    Distribute(pat.Name, "WIND", st, y ? 0 : F, y ? F : 0,
                        $"z={z:0.00} m; k={k:0.000}; W={w:0.000} kN/m²; h={h:0.00} m; B={width:0.00} m");
                }
            }

        // ---------------- ĐỘNG ĐẤT ----------------
        var eqPats = p.Patterns.Where(x => x.Type == LoadType.Seismic).ToList();
        if (p.Seismic.Enabled && eqPats.Count > 0)
        {
            var W = StoryWeights(p, stories);
            double H = stories[^1].Elevation - p.BaseElevation;
            bool steel = p.Frames.Count(f => !f.IsConcrete) > p.Frames.Count / 2;
            double Ct = steel ? 0.085 : 0.075;
            double T1 = Ct * Math.Pow(H, 0.75);
            double sd = Sd(T1, p.Seismic);
            double TC = p.Seismic.Soil switch { "A" => 0.4, "B" => 0.5, "D" => 0.8, "E" => 0.5, _ => 0.6 };
            double lambda = (T1 <= 2 * TC && stories.Count > 2) ? 0.85 : 1.0;
            double Wtot = W.Values.Sum();
            double Fb = sd * Wtot * lambda;
            double sumZW = stories.Sum(s => (s.Elevation - p.BaseElevation) * W[s.Name]);
            warn.Add($"Động đất: H={H:0.00} m, T1={T1:0.000} s, Sd={sd:0.0000}g, W={Wtot:0.0} kN, λ={lambda}, Fb={Fb:0.0} kN");
            foreach (var pat in eqPats)
            {
                bool y = IsY(pat.Name);
                foreach (var st in stories)
                {
                    double F = sumZW > 0 ? Fb * (st.Elevation - p.BaseElevation) * W[st.Name] / sumZW : 0;
                    Distribute(pat.Name, "EQ", st, y ? 0 : F, y ? F : 0, $"W={W[st.Name]:0.0} kN; T1={T1:0.000} s; Sd={sd:0.0000}g");
                }
            }
        }
        return warn;
    }

    /// <summary>Trọng lượng tham gia dao động mỗi tầng: tĩnh tải + ψE·hoạt tải (TCVN 9386-1:2025 (thay thế TCVN 9386:2012; số điều theo bản 2012) mục 3.2.4).
    /// Cột chia đôi cho tầng trên và dưới.</summary>
    public static Dictionary<string, double> StoryWeights(Project p, List<Story> stories)
    {
        var W = stories.ToDictionary(s => s.Name, _ => 0.0);
        double Factor(LoadPattern pat) => pat.Type switch
        {
            LoadType.Dead or LoadType.SuperDead => 1.0,
            LoadType.Live => p.Seismic.PsiLive,
            _ => 0.0
        };
        void AddAt(double z, double w)
        {
            Story best = null; double d = double.MaxValue;
            foreach (var s in stories) { double dd = Math.Abs(s.Elevation - z); if (dd < d) { d = dd; best = s; } }
            if (best == null) return;
            // phần thuộc chân cột (gần cao độ móng hơn tầng 1) bỏ qua
            if (Math.Abs(z - p.BaseElevation) < d) return;
            W[best.Name] += w;
        }
        foreach (var pat in p.Patterns)
        {
            double k = Factor(pat);
            if (k == 0) continue;
            if (pat.SelfWeight != 0)
                foreach (var f in p.Frames)
                {
                    var sec = p.Section(f.Section);
                    double w = pat.SelfWeight * p.Material(sec.Material).UnitWeight * sec.Props().A * p.Length(f) * k;
                    var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
                    AddAt(a.Z, w / 2); AddAt(b.Z, w / 2);
                }
            foreach (var fl in p.FrameLoads.Where(l => l.Pattern == pat.Name && l.Dir == LoadDir.Gravity))
            {
                var f = p.Frame(fl.Element);
                double L = p.Length(f), tot = 0;
                for (int i = 0; i + 1 < fl.Points.Count; i++)
                    tot += (fl.Points[i][1] + fl.Points[i + 1][1]) / 2 * (fl.Points[i + 1][0] - fl.Points[i][0]) * L;
                tot += fl.PointLoads.Sum(x => x[1]);
                var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
                AddAt(a.Z, tot * k / 2); AddAt(b.Z, tot * k / 2);
            }
            foreach (var nl in p.NodeLoads.Where(l => l.Pattern == pat.Name && l.Fz < 0))
                AddAt(p.Node(nl.Node).Z, -nl.Fz * k);
        }
        return W;
    }
}
