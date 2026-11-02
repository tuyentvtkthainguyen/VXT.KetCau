using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Thiết kế móng đơn BTCT trên nền thiên nhiên dưới chân cột:
/// sức chịu tải nền theo TCVN 9362:2012, kích thước đáy theo điều kiện áp lực, chiều cao theo chọc thủng
/// (TCVN 5574:2018 mục 8.1.6) và cốt thép đáy theo mô men tại mép cột.</summary>
public static class FootingDesigner
{
    /// <summary>Chiều cao móng tối thiểu, m</summary>
    public const double MinHeight = 0.30;
    /// <summary>Kích thước đáy tối thiểu, m</summary>
    public const double MinSize = 0.80;

    private struct Load
    {
        public string Combo;
        public double N, Mx, My, Qx, Qy;
    }

    /// <summary>Thiết kế móng đơn dưới mọi chồng cột BTCT có nút chân là gối tựa và loại móng hiệu lực là móng đơn.</summary>
    public static List<FootingDesign> Design(Project p, AnalysisResults r, List<string> warnings)
    {
        warnings ??= new List<string>();
        var list = new List<(FootingDesign d, string sig)>();
        var combos = r.DesignCombos();

        // cột chân: cột BTCT có nút dưới là gối
        var baseCols = new List<(Node n, FrameElement f)>();
        foreach (var f in p.Frames.Where(f => f.Kind == FrameKind.Column))
        {
            var lo = DesignUtil.LowerNode(p, f);
            // chỉ chân cột có loại móng hiệu lực là móng đơn (Footing.Type ?? Project.Foundation.Type)
            if (lo.IsSupport && KetCauVXT.Design.Foundation.FoundationUtil.TypeOf(p, lo.Id) == FoundationType.Isolated
                && baseCols.All(x => x.n.Id != lo.Id)) baseCols.Add((lo, f));
        }
        baseCols = baseCols.OrderBy(x => Math.Round(x.n.Y, 2)).ThenBy(x => Math.Round(x.n.X, 2)).ToList();

        foreach (var (n, f) in baseCols)
        {
            try
            {
                var d = DesignOne(p, r, n, f, combos);
                string sig = Signature(d);
                int k = list.FindIndex(x => x.sig == sig);
                if (k >= 0)
                {
                    var keep = list[k].d;
                    keep.Quantity++;
                    for (int i = 0; i < keep.Checks.Count && i < d.Checks.Count; i++)
                        if (d.Checks[i].Ratio > keep.Checks[i].Ratio) keep.Checks[i] = d.Checks[i];
                    keep.Messages.Add("Gộp móng giống hệt tại " + DesignUtil.GridName(p, d.X, d.Y));
                }
                else list.Add((d, sig));
            }
            catch (Exception ex)
            {
                warnings.Add("LỖI thiết kế móng tại " + DesignUtil.GridName(p, n.X, n.Y) + ": " + ex.Message);
            }
        }
        int no = 1;
        foreach (var (d, _) in list) d.Name = "M" + no++;
        return list.Select(x => x.d).ToList();
    }

    /// <summary>Sức chịu tải tiêu chuẩn của nền R = m1·m2/ktc·(A·b·γII + B·Df·γ'II + D·c) (TCVN 9362:2012 mục 4.6.9, công thức 3),
    /// A = 0,25π/(cotφ + φ − π/2), B = 1 + π/(cotφ + φ − π/2), D = π·cotφ/(cotφ + φ − π/2), φ (rad).
    /// Nếu SoilSettings.ROverride &gt; 0 thì dùng trực tiếp giá trị này.</summary>
    public static double BearingR(SoilSettings s, double b)
    {
        if (s.ROverride > 0) return s.ROverride;
        double phi = Math.Max(s.Phi, 0.5) * Math.PI / 180.0;
        double cot = 1.0 / Math.Tan(phi);
        double den = cot + phi - Math.PI / 2;
        double A = 0.25 * Math.PI / den, B = 1 + Math.PI / den, D = Math.PI * cot / den;
        return s.M1 * s.M2 / Math.Max(s.Ktc, 1e-6) * (A * b * s.Gamma + B * s.Depth * s.GammaAbove + D * s.C);
    }

    private static FootingDesign DesignOne(Project p, AnalysisResults r, Node n, FrameElement col, List<string> combos)
    {
        var cs = p.Concrete; var soil = p.Soil;
        var sec = p.Section(col.Section);
        var conc = DesignUtil.ConcreteOf(p, cs.ConcreteFooting, cs.ConcreteFooting);
        var main = p.Material(cs.RebarMain);
        var stlSmall = p.Material(cs.RebarStirrup);
        int[] dias = DesignUtil.Sorted(cs.FootingBars, new[] { 10, 12, 14, 16 });

        // kích thước cột theo phương X (hc) và Y (bc)
        double hc = DesignUtil.ColumnDimAlong(p, col, Vec3.UnitX);
        double bc = DesignUtil.ColumnDimAlong(p, col, Vec3.UnitY);

        // ---- nội lực tại chân cột từ phản lực ----
        var loads = new List<Load>();
        foreach (string cn in combos)
        {
            var cr = r.Get(cn);
            if (!cr.Reactions.TryGetValue(n.Id, out var R)) continue;
            // phản lực tác dụng lên kết cấu; lực tác dụng xuống móng = ngược dấu → N = Fz (phản lực hướng lên = nén)
            loads.Add(new Load { Combo = cn, N = R[2], Mx = R[3], My = R[4], Qx = R[0], Qy = R[1] });
        }
        if (loads.Count == 0) throw new InvalidOperationException("không có phản lực tại nút " + n.Id);

        var d = new FootingDesign
        {
            NodeId = n.Id, ColumnFrame = col.Id, X = n.X, Y = n.Y, ColumnB = bc, ColumnH = hc,
            Depth = soil.Depth, Story = "Móng"
        };
        if (loads.All(l => l.N <= 0)) d.Messages.Add("LỖI chân cột chịu kéo ở mọi tổ hợp – móng đơn trên nền thiên nhiên không phù hợp");
        if (loads.Any(l => l.N <= 0)) d.Messages.Add("Có tổ hợp chân cột chịu nhổ – cần kiểm tra chống nhổ móng");

        double kt = Math.Max(soil.LoadFactorAvg, 1.0);
        double gtb = soil.GammaAvg, Df = soil.Depth;
        var given = p.Footings.FirstOrDefault(x => x.NodeId == n.Id);
        double asp = Math.Max(soil.AspectRatio, 1.0);
        // cạnh dài theo phương có mô men lớn hơn: L theo X chịu My
        double myMax = loads.Max(l => Math.Abs(l.My) + Math.Abs(l.Qx) * Df), mxMax = loads.Max(l => Math.Abs(l.Mx) + Math.Abs(l.Qy) * Df);
        bool longX = myMax >= mxMax;

        // ---- kích thước đáy (TCVN 9362:2012 mục 4.6) ----
        double Nmax = loads.Max(l => l.N) / kt;
        double R0 = BearingR(soil, 1.0);
        double Aest = Nmax / Math.Max(R0 - gtb * Df, 10);
        double bs = Math.Max(RcSection.CeilTo(Math.Sqrt(Math.Max(Aest, 0) / asp), 0.1), MinSize);
        double L = 0, B = 0, Rv = 0;
        double pmax = 0, pmin = 0, ptb = 0;
        string gov = "";
        bool sized = false;
        if (given != null && given.L > 0 && given.B > 0)
        {
            L = given.L; B = given.B;
            Rv = BearingR(soil, Math.Min(L, B));
            EvalPressure(loads, L, B, kt, gtb, Df, out ptb, out pmax, out pmin, out gov);
            sized = true;
            d.Messages.Add("Kích thước đáy móng do người dùng chọn");
        }
        else
        {
            for (int it = 0; it < 200; it++)
            {
                double sl = RcSection.CeilTo(bs * asp, 0.1);
                L = longX ? sl : bs; B = longX ? bs : sl;
                Rv = BearingR(soil, Math.Min(L, B));
                EvalPressure(loads, L, B, kt, gtb, Df, out ptb, out pmax, out pmin, out gov);
                if (ptb <= Rv + 1e-6 && pmax <= 1.2 * Rv + 1e-6 && pmin >= -1e-6) { sized = true; break; }
                bs = Math.Round(bs + 0.1, 3);
            }
        }
        if (!sized) d.Messages.Add("LỖI không chọn được kích thước đáy móng thoả điều kiện áp lực nền – xem xét móng băng/móng cọc");
        d.L = L; d.B = B; d.R = Rv; d.Ptb = ptb; d.Pmax = pmax; d.Pmin = pmin;

        // tổ hợp tính toán bất lợi (theo N lớn nhất và độ lệch tâm)
        var g = loads.OrderByDescending(l => l.N * (1 + 6 * (Math.Abs(l.My) / Math.Max(l.N, 1e-6)) / L + 6 * (Math.Abs(l.Mx) / Math.Max(l.N, 1e-6)) / B)).First();
        d.N = g.N; d.Mx = g.Mx; d.My = g.My; d.Qx = g.Qx; d.Qy = g.Qy; d.Ntc = g.N / kt; d.Combo = g.Combo;

        d.Checks.Add(new CheckItem { Name = "Áp lực trung bình đáy móng ptb ≤ R", Demand = ptb, Capacity = Rv, Unit = "kN/m²", Note = gov });
        d.Checks.Add(new CheckItem { Name = "Áp lực lớn nhất pmax ≤ 1,2R", Demand = pmax, Capacity = 1.2 * Rv, Unit = "kN/m²", Note = gov });
        d.Checks.Add(new CheckItem
        {
            Name = "Áp lực nhỏ nhất pmin ≥ 0", Demand = pmin < 0 ? -pmin : 0, Capacity = pmin < 0 ? 0 : 1, Unit = "kN/m²",
            Note = "pmin = " + DesignUtil.F(pmin, "0.0") + " kN/m²"
        });

        // ---- chiều cao móng theo chọc thủng (TCVN 5574:2018 mục 8.1.6.2) ----
        double cover = cs.CoverFooting;
        int dGuess = dias[Math.Min(1, dias.Length - 1)];
        double H = Math.Max(MinHeight, given != null && given.Height > 0 ? given.Height : MinHeight);
        double F = 0, Fu = 0;
        for (int it = 0; it < 100; it++)
        {
            double h0 = H - cover - dGuess / 1000.0;
            Punching(loads, L, B, hc, bc, h0, conc, out F, out Fu);
            if (F <= Fu + 1e-6) break;
            if (given != null && given.Height > 0) break;
            H = Math.Round(H + 0.05, 3);
        }
        d.H = H; d.Hb = Math.Max(0.2, Math.Min(H, RcSection.CeilTo(H / 2, 0.05)));
        if (d.Hb > H) d.Hb = H;
        d.Checks.Add(new CheckItem { Name = "Chọc thủng F ≤ Rbt·u·h0", Demand = F, Capacity = Fu, Unit = "kN", Note = "H = " + DesignUtil.F(H * 1000, "0") + " mm" });

        // ---- cốt thép đáy (uốn tại mép cột, áp lực tính toán phía pmax) ----
        double mL = 0, mB = 0;
        foreach (var l in loads)
        {
            if (l.N <= 0) continue;
            double area = L * B;
            // áp lực ròng (không kể trọng lượng móng và đất)
            double exL = (Math.Abs(l.My) + Math.Abs(l.Qx) * H) * 6 / (B * L * L);
            double exB = (Math.Abs(l.Mx) + Math.Abs(l.Qy) * H) * 6 / (L * B * B);
            double p0 = l.N / area;
            // phương L: công xôn dài (L − hc)/2, áp lực biên pmax, tại mép cột pI (tuyến tính)
            double cl = (L - hc) / 2, cb = (B - bc) / 2;
            double pmL = p0 + exL, pIL = p0 + exL * (hc / L);
            mL = Math.Max(mL, B * cl * cl * (2 * pmL + pIL) / 6);
            double pmB = p0 + exB, pIB = p0 + exB * (bc / B);
            mB = Math.Max(mB, L * cb * cb * (2 * pmB + pIB) / 6);
        }
        Material SteelOf(int dia) => dia < 10 ? stlSmall : main;
        // lớp dưới cùng theo phương L (h0 lớn), lớp trên theo phương B
        var sL = RcSection.ChooseSpacing(dia => AsPerM(mL / B, H - cover - dia / 2000.0, SteelOf(dia)), dias, 0.20, 0.10, 0.15, 10);
        double dl = sL.Dia / 1000.0;
        var sB = RcSection.ChooseSpacing(dia => AsPerM(mB / L, H - cover - dl - dia / 2000.0, SteelOf(dia)), dias, 0.20, 0.10, 0.15, 10);
        d.AsL = sL.AsReqPerM * B; d.AsB = sB.AsReqPerM * L;
        d.DiaL = sL.Dia; d.DiaB = sB.Dia; d.SpacingL = sL.Spacing; d.SpacingB = sB.Spacing;
        d.BarsL = sL.Text; d.BarsB = sB.Text;
        if (!sL.Ok || !sB.Ok) d.Messages.Add("LỖI không bố trí đủ thép đáy móng với bước ≥ 100 mm – tăng chiều cao móng");
        double h0L = H - cover - dl, h0B = H - cover - dl - sB.Dia / 2000.0;
        d.Checks.Add(new CheckItem
        {
            Name = "Uốn phương L tại mép cột", Demand = mL, Unit = "kN.m", Note = sL.Text,
            Capacity = RcSection.MomentCapacity(B, h0L, sL.AsPerM * B, 0, cover, conc, SteelOf(sL.Dia))
        });
        d.Checks.Add(new CheckItem
        {
            Name = "Uốn phương B tại mép cột", Demand = mB, Unit = "kN.m", Note = sB.Text,
            Capacity = RcSection.MomentCapacity(L, h0B, sB.AsPerM * L, 0, cover, conc, SteelOf(sB.Dia))
        });

        // ---- bố trí thép (lưới đáy, bẻ móc lên 2 đầu) ----
        double c = cover;
        double hookL = DesignUtil.R10(Math.Max(10 * dl, 0.10));
        double hookB = DesignUtil.R10(Math.Max(10 * sB.Dia / 1000.0, 0.10));
        double lenL = DesignUtil.R10(L - 2 * c), lenB = DesignUtil.R10(B - 2 * c);
        d.Bars.Add(new BarMark
        {
            Role = "Lớp dưới phương L", Dia = sL.Dia, Spacing = sL.Spacing, Count = (int)Math.Ceiling((B - 2 * c) / sL.Spacing - 1e-9) + 1,
            Length = DesignUtil.R10(lenL + 2 * hookL), Shape = "U", Start = c, End = L - c,
            Segments = new List<double> { DesignUtil.Mm(hookL), DesignUtil.Mm(lenL), DesignUtil.Mm(hookL) }
        });
        d.Bars.Add(new BarMark
        {
            Role = "Lớp dưới phương B", Dia = sB.Dia, Spacing = sB.Spacing, Count = (int)Math.Ceiling((L - 2 * c) / sB.Spacing - 1e-9) + 1,
            Length = DesignUtil.R10(lenB + 2 * hookB), Shape = "U", Start = c, End = B - c,
            Segments = new List<double> { DesignUtil.Mm(hookB), DesignUtil.Mm(lenB), DesignUtil.Mm(hookB) }
        });
        RebarNumbering.Renumber(d.Bars);
        return d;
    }

    /// <summary>Diện tích thép trên 1 m: As = M/(0,9·Rs·h0) (gần đúng cánh tay đòn z = 0,9h0).</summary>
    private static double AsPerM(double mPerM, double h0, Material steel)
    {
        if (h0 <= 0) return 1;
        double asMin = 0.0005 * h0;   // μmin = 0,05% (TCVN 5574:2018 mục 10.3.3) theo h0 trên 1 m
        return Math.Max(mPerM / (0.9 * steel.Rs * h0), asMin);
    }

    /// <summary>Áp lực đáy móng tiêu chuẩn: p = Ntc/A + γtb·Df ± 6Mx/(L·B²) ± 6My/(B·L²), M kể cả lực cắt × Df;
    /// trả về giá trị bất lợi nhất qua các tổ hợp (nội lực tiêu chuẩn = tính toán / hệ số vượt tải trung bình).</summary>
    private static void EvalPressure(List<Load> loads, double L, double B, double kt, double gtb, double Df,
        out double ptb, out double pmax, out double pmin, out string combo)
    {
        ptb = double.MinValue; pmax = double.MinValue; pmin = double.MaxValue; combo = "";
        double A = L * B;
        foreach (var l in loads)
        {
            double N = l.N / kt;
            double my = (Math.Abs(l.My) + Math.Abs(l.Qx) * Df) / kt;
            double mx = (Math.Abs(l.Mx) + Math.Abs(l.Qy) * Df) / kt;
            double p0 = N / A + gtb * Df;
            double dp = 6 * my / (B * L * L) + 6 * mx / (L * B * B);
            if (p0 > ptb) ptb = p0;
            if (p0 + dp > pmax) { pmax = p0 + dp; combo = l.Combo; }
            pmin = Math.Min(pmin, p0 - dp);
        }
    }

    /// <summary>Kiểm tra chọc thủng (TCVN 5574:2018 mục 8.1.6.2, công thức 8.88): F ≤ Fb,u = Rbt·u·h0,
    /// u = chu vi đường bao cách mép cột h0/2: 2·(hc + h0 + bc + h0);
    /// F = N − p_net·(hc + 2h0)(bc + 2h0) với p_net = N/(L·B) (áp lực ròng tính toán trung bình, không kể trọng lượng móng;
    /// mô men không làm thay đổi tổng phản lực trong tháp chọc thủng đối xứng nên bỏ qua).</summary>
    private static void Punching(List<Load> loads, double L, double B, double hc, double bc, double h0, Material conc, out double F, out double Fu)
    {
        F = 0;
        double u = 2 * (hc + h0 + bc + h0);
        Fu = conc.Rbt * u * Math.Max(h0, 0);
        foreach (var l in loads)
        {
            if (l.N <= 0) continue;
            double pnet = l.N / (L * B);
            double ap = Math.Min((hc + 2 * h0) * (bc + 2 * h0), L * B);
            double f = l.N - pnet * ap;
            if (f > F) F = f;
        }
    }

    private static string Signature(FootingDesign d) =>
        Math.Round(d.L * 100) + "x" + Math.Round(d.B * 100) + "x" + Math.Round(d.H * 100) + "|" + Math.Round(d.ColumnB * 1000) + "x" + Math.Round(d.ColumnH * 1000)
        + "|" + d.BarsL + "|" + d.BarsB + "|" + RebarNumbering.Signature(d.Bars);
}
