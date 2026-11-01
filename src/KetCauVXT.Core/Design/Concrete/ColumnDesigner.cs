using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Thiết kế cột BTCT tiết diện chữ nhật, cốt thép đối xứng, nén lệch tâm xiên theo TCVN 5574:2018 mục 8.1.2.
/// Cột được gom thành chồng cột theo toạ độ (X, Y) của nút chân; mỗi tầng tính riêng; bố trí thép và đai theo mục 10.3.</summary>
public static class ColumnDesigner
{
    /// <summary>Khoảng hở tối thiểu giữa các thanh thép dọc cột đổ bê tông theo phương đứng, m (TCVN 5574:2018 mục 10.3.5).</summary>
    public const double MinClearColumn = 0.050;
    /// <summary>Khoảng cách tim tối đa giữa các thanh thép dọc cột, m (TCVN 5574:2018 mục 10.3.5).</summary>
    public const double MaxBarSpacing = 0.400;
    /// <summary>Đoạn neo thép cột xuống móng (thẳng), m – kèm móc chân L.</summary>
    public const double FootingAnchor = 0.50;

    private sealed class Candidate
    {
        public int Dia; public int NB; public int NH; public double As;
    }

    /// <summary>Thiết kế toàn bộ cột BTCT (Kind == Column). Lỗi từng cột ghi vào warnings với tiền tố "LỖI".</summary>
    public static List<ColumnDesign> Design(Project p, AnalysisResults r, List<string> warnings)
    {
        warnings ??= new List<string>();
        var cols = p.Frames.Where(f => f.Kind == FrameKind.Column).ToList();
        var stacks = new List<List<FrameElement>>();
        foreach (var g in cols.GroupBy(f =>
                 {
                     var n = DesignUtil.LowerNode(p, f);
                     return (Math.Round(n.X, 2), Math.Round(n.Y, 2));
                 }))
            stacks.Add(g.OrderBy(f => DesignUtil.LowerNode(p, f).Z).ToList());
        stacks = stacks.OrderBy(s => Math.Round(DesignUtil.LowerNode(p, s[0]).Y, 2))
                       .ThenBy(s => Math.Round(DesignUtil.LowerNode(p, s[0]).X, 2)).ToList();

        var combos = r.DesignCombos();
        var designed = new List<(ColumnDesign d, string sig)>();
        foreach (var st in stacks)
        {
            try
            {
                var d = DesignStack(p, r, st, combos);
                string sig = Signature(d);
                int k = designed.FindIndex(x => x.sig == sig);
                if (k >= 0)
                {
                    var keep = designed[k].d;
                    keep.Quantity++;
                    keep.GridName += ", " + d.GridName;
                    for (int i = 0; i < keep.Checks.Count && i < d.Checks.Count; i++)
                        if (d.Checks[i].Ratio > keep.Checks[i].Ratio) keep.Checks[i] = d.Checks[i];
                    for (int i = 0; i < keep.Stories.Count && i < d.Stories.Count; i++)
                        if (d.Stories[i].Utilization > keep.Stories[i].Utilization)
                        {
                            var a = keep.Stories[i]; var b = d.Stories[i];
                            a.N = b.N; a.M2 = b.M2; a.M3 = b.M3; a.Combo = b.Combo; a.Eta2 = b.Eta2; a.Eta3 = b.Eta3;
                            a.Utilization = b.Utilization; a.AsReq = Math.Max(a.AsReq, b.AsReq);
                        }
                    foreach (var msg in d.Messages.Where(x => x.StartsWith("LỖI") && !keep.Messages.Contains(x))) keep.Messages.Add(msg);
                }
                else designed.Add((d, sig));
            }
            catch (Exception ex)
            {
                var n = DesignUtil.LowerNode(p, st[0]);
                warnings.Add("LỖI thiết kế cột tại " + DesignUtil.GridName(p, n.X, n.Y) + ": " + ex.Message);
            }
        }
        int no = 1;
        foreach (var (d, _) in designed) d.Name = "C" + no++;
        return designed.Select(x => x.d).ToList();
    }

    private static ColumnDesign DesignStack(Project p, AnalysisResults r, List<FrameElement> stack, List<string> combos)
    {
        var cs = p.Concrete;
        var main = p.Material(cs.RebarMain);
        var stl = p.Material(cs.RebarStirrup);
        int[] dias = DesignUtil.Sorted(cs.ColumnBars, new[] { 14, 16, 18, 20, 22, 25 });
        int[] sdias = DesignUtil.Sorted(cs.StirrupBars, new[] { 6, 8, 10 });
        var n0 = DesignUtil.LowerNode(p, stack[0]);
        double zBase = n0.Z;
        var d = new ColumnDesign
        {
            Story = stack[0].Story ?? "",
            GridName = DesignUtil.GridName(p, n0.X, n0.Y),
            Frames = stack.Select(f => f.Id).ToList()
        };

        for (int si = 0; si < stack.Count; si++)
        {
            var f = stack[si];
            var sec = p.Section(f.Section);
            if (sec.Shape != SectionShape.RectConcrete || sec.B <= 0 || sec.H <= 0)
                throw new InvalidOperationException("cột " + f.Id + " không phải tiết diện chữ nhật BTCT");
            var conc = DesignUtil.ConcreteOf(p, sec.Material, cs.ConcreteColumn);
            double L = p.Length(f);
            var lo = DesignUtil.LowerNode(p, f); var up = DesignUtil.UpperNode(p, f);
            var res = new ColumnStoryResult
            {
                Frame = f.Id, Story = f.Story ?? "", B = sec.B, H = sec.H, Length = L, ZBottom = lo.Z, ZTop = up.Z
            };
            var forces = CollectForces(r, f, combos);
            var c = SelectBars(res, forces, sec, conc, main, cs, dias, d.Messages);
            res.Dia = c.Dia; res.BarsAlongB = c.NB; res.BarsAlongH = c.NH; res.AsProv = c.As;

            // ---- cốt đai (TCVN 5574:2018 mục 10.3.4.3) ----
            int sd = sdias.FirstOrDefault(x => x >= Math.Max(6, (int)Math.Ceiling(res.Dia / 4.0)), sdias[^1]);
            double bmin = Math.Min(sec.B, sec.H);
            double sMid = RcSection.FloorTo(Math.Min(Math.Min(15 * res.Dia / 1000.0, bmin), 0.40), 0.05);
            if (sMid < 0.10) sMid = Math.Max(RcSection.FloorTo(Math.Min(15 * res.Dia / 1000.0, bmin), 0.01), 0.05);
            double sEnd = Math.Min(RcSection.FloorTo(sMid / 2, 0.01), 0.10);
            sEnd = Math.Max(sEnd, 0.05);
            res.StirrupDia = sd; res.StirrupSpacing = sMid; res.StirrupSpacingEnd = sEnd;

            string tag = "tầng " + res.Story;
            d.Checks.Add(new CheckItem
            {
                Name = "Nén lệch tâm xiên " + tag, Demand = res.Utilization, Capacity = 1.0, Unit = "",
                Note = $"N = {DesignUtil.F(res.N, "0.0")} kN; M2 = {DesignUtil.F(res.M2, "0.00")}; M3 = {DesignUtil.F(res.M3, "0.00")} kN.m; {res.Combo}; " + (2 * res.BarsAlongB + 2 * Math.Max(0, res.BarsAlongH - 2)) + "Ø" + res.Dia
            });
            double mu = res.Ratio;
            d.Checks.Add(new CheckItem
            {
                Name = "Hàm lượng thép tối đa " + tag, Demand = mu, Capacity = cs.MaxRatioColumn, Unit = "%",
                Note = "μ = " + DesignUtil.F(mu, "0.00") + "% (tối thiểu " + DesignUtil.F(cs.MinRatioColumn, "0.00") + "%)"
            });
            d.Stories.Add(res);
        }

        Detail(p, d, stack, cs, zBase);
        RebarNumbering.Renumber(d.Bars);
        foreach (var z in d.StirrupZones) z.Mark = d.Bars.FirstOrDefault(bm => bm.Role == "Đai" && bm.Span == z.Span)?.Mark ?? 0;
        return d;
    }

    /// <summary>Nội lực tại hai đầu cột cho mọi tổ hợp: (tổ hợp, N nén dương, M2, M3).</summary>
    private static List<(string Combo, double N, double M2, double M3)> CollectForces(AnalysisResults r, FrameElement f, List<string> combos)
    {
        var l = new List<(string, double, double, double)>();
        foreach (string cn in combos)
        {
            var cr = r.Get(cn);
            if (!cr.Frames.TryGetValue(f.Id, out var ff) || ff.Count == 0) continue;
            foreach (int i in new[] { 0, ff.Count - 1 })
                l.Add((cn, -ff.P(i), ff.M2(i), ff.M3(i)));
        }
        return l;
    }

    /// <summary>Chọn số thanh và Ø nhỏ nhất (theo diện tích) thoả mãn kiểm tra nén lệch tâm xiên cho mọi tổ hợp và cả hai đầu cột.
    /// Mô men thiết kế: M = η·max(|M|; N·ea) (TCVN 5574:2018 mục 8.1.2.2 – độ lệch tâm ngẫu nhiên; 8.1.2.4 – uốn dọc).</summary>
    private static Candidate SelectBars(ColumnStoryResult res, List<(string Combo, double N, double M2, double M3)> forces,
        FrameSection sec, Material conc, Material steel, ConcreteDesignSettings cs, int[] dias, List<string> msgs)
    {
        double B = sec.B, H = sec.H, a = cs.CoverColumn;
        double A = B * H;
        double asMin = cs.MinRatioColumn / 100.0 * A, asMax = cs.MaxRatioColumn / 100.0 * A;
        double l0 = cs.ColumnLengthFactor * res.Length;
        double i3 = H / Math.Sqrt(12), i2 = B / Math.Sqrt(12);
        double lam3 = l0 / i3, lam2 = l0 / i2;
        double ea3 = RcSection.Ea(res.Length, H), ea2 = RcSection.Ea(res.Length, B);
        if (Math.Max(lam2, lam3) > 120) msgs.Add("LỖI tầng " + res.Story + ": độ mảnh l0/i = " + DesignUtil.F(Math.Max(lam2, lam3), "0") + " > 120");

        // danh sách phương án sắp theo diện tích
        var cands = new List<Candidate>();
        foreach (int dia in dias)
        {
            double dm = dia / 1000.0;
            for (int nb = 2; nb <= 8; nb++)
                for (int nh = 2; nh <= 8; nh++)
                {
                    double sb = (B - 2 * a - dm) / (nb - 1), sh = (H - 2 * a - dm) / (nh - 1);
                    if (sb - dm < Math.Max(MinClearColumn, dm) - 1e-9 || sh - dm < Math.Max(MinClearColumn, dm) - 1e-9) continue;
                    if (sb > MaxBarSpacing + 1e-9 || sh > MaxBarSpacing + 1e-9) continue;
                    int n = 2 * nb + 2 * (nh - 2);
                    double As = n * RcSection.BarArea(dia);
                    if (As < asMin * (1 - 1e-6)) continue;
                    cands.Add(new Candidate { Dia = dia, NB = nb, NH = nh, As = As });
                }
        }
        // ưu tiên: diện tích nhỏ, ít thanh, bố trí cân đối theo tỷ lệ cạnh
        cands = cands.OrderBy(c => Math.Round(c.As * 1e6))
                     .ThenBy(c => 2 * c.NB + 2 * (c.NH - 2))
                     .ThenBy(c => Math.Abs((c.NB - 1) / B - (c.NH - 1) / H)).ToList();
        if (cands.Count == 0) throw new InvalidOperationException("không bố trí được thép dọc trong tiết diện " + sec.Name);

        Candidate chosen = null;
        double bestU = double.MaxValue; Candidate bestC = null;
        (string Combo, double N, double M2, double M3, double E2, double E3, double U) gov = default;
        foreach (var c in cands)
        {
            var lay = ColumnLayout.Create(B, H, a + c.Dia / 2000.0, c.Dia, c.NB, c.NH);
            var g = Evaluate(lay, forces, conc, steel, l0, res.Length, lam2, lam3, ea2, ea3, B, H, out bool unstable);
            if (g.U < bestU) { bestU = g.U; bestC = c; }
            if (!unstable && g.U <= 1.0 && c.As <= asMax * (1 + 1e-6))
            {
                chosen = c; gov = g;
                break;
            }
        }
        if (chosen == null)
        {
            chosen = bestC ?? cands[^1];
            var lay = ColumnLayout.Create(B, H, a + chosen.Dia / 2000.0, chosen.Dia, chosen.NB, chosen.NH);
            gov = Evaluate(lay, forces, conc, steel, l0, res.Length, lam2, lam3, ea2, ea3, B, H, out bool unstable);
            if (unstable) msgs.Add("LỖI cột mất ổn định tầng " + res.Story + ": N ≥ Ncr – cần tăng tiết diện");
            else msgs.Add("LỖI tầng " + res.Story + ": tiết diện " + sec.Name + " không đủ khả năng chịu lực với hàm lượng ≤ "
                          + DesignUtil.F(cs.MaxRatioColumn, "0.0") + "% – cần tăng tiết diện hoặc cấp bê tông");
        }
        res.N = gov.N; res.M2 = gov.M2; res.M3 = gov.M3; res.Combo = gov.Combo ?? "";
        res.Eta2 = gov.E2; res.Eta3 = gov.E3; res.Utilization = gov.U;
        res.AsReq = Math.Max(asMin, EstimateAsReq(B, H, a + chosen.Dia / 2000.0, chosen, forces, conc, steel, l0, res.Length, lam2, lam3, ea2, ea3));
        return chosen;
    }

    /// <summary>Hệ số sử dụng lớn nhất của bố trí thép qua mọi tổ hợp/đầu cột, kèm nội lực tới hạn (đã kể ea và η).</summary>
    private static (string Combo, double N, double M2, double M3, double E2, double E3, double U) Evaluate(
        ColumnLayout lay, List<(string Combo, double N, double M2, double M3)> forces, Material conc, Material steel,
        double l0, double L, double lam2, double lam3, double ea2, double ea3, double B, double H, out bool unstable)
    {
        unstable = false;
        (string, double, double, double, double, double, double) worst = ("", 0, 0, 0, 1, 1, -1);
        // mô men quán tính cốt thép quanh trục trọng tâm
        double is3 = lay.Bars.Sum(x => x.Area * x.Y * x.Y), is2 = lay.Bars.Sum(x => x.Area * x.X * x.X);
        double I3 = B * H * H * H / 12, I2 = H * B * B * B / 12;
        foreach (var fc in forces)
        {
            double N = fc.N;
            double m3 = Math.Abs(fc.M3), m2 = Math.Abs(fc.M2);
            if (N > 0)
            {
                m3 = Math.Max(m3, N * ea3);
                m2 = Math.Max(m2, N * ea2);
            }
            double e3 = RcSection.Eta(N, m3, l0, H, I3, is3, lam3, conc, steel, 2.0, out _);
            double e2 = RcSection.Eta(N, m2, l0, B, I2, is2, lam2, conc, steel, 2.0, out _);
            double u;
            if (double.IsInfinity(e2) || double.IsInfinity(e3)) { unstable = true; u = 99; }
            else
            {
                m3 *= e3; m2 *= e2;
                u = RcSection.BiaxialUtilization(N, m2, m3, lay, conc, steel, out _, out _);
            }
            if (u > worst.Item7)
                worst = (fc.Combo, N, m2, m3, double.IsInfinity(e2) ? 99 : e2, double.IsInfinity(e3) ? 99 : e3, u);
        }
        if (worst.Item7 < 0) worst.Item7 = 0;
        return worst;
    }

    /// <summary>Diện tích thép yêu cầu (lý thuyết) – dò hệ số thu nhỏ diện tích thanh của bố trí đã chọn sao cho hệ số sử dụng = 1.</summary>
    private static double EstimateAsReq(double B, double H, double a, Candidate c, List<(string Combo, double N, double M2, double M3)> forces,
        Material conc, Material steel, double l0, double L, double lam2, double lam3, double ea2, double ea3)
    {
        double lo = 0.0, hi = 1.0;
        var full = ColumnLayout.Create(B, H, a, c.Dia, c.NB, c.NH);
        var u1 = Evaluate(full, forces, conc, steel, l0, L, lam2, lam3, ea2, ea3, B, H, out _);
        if (u1.U > 1.0) return full.As;
        for (int it = 0; it < 25; it++)
        {
            double mid = 0.5 * (lo + hi);
            var lay = ColumnLayout.Create(B, H, a, c.Dia, c.NB, c.NH, mid);
            var g = Evaluate(lay, forces, conc, steel, l0, L, lam2, lam3, ea2, ea3, B, H, out bool uns);
            if (uns || g.U > 1.0) lo = mid; else hi = mid;
        }
        return hi * full.As;
    }

    // ------------------------------------------------------------------
    // Bố trí thép
    // ------------------------------------------------------------------

    /// <summary>Bố trí thép cột: thép dọc mỗi tầng dài = chiều cao tầng + đoạn nối chồng 30d (TCVN 5574:2018 mục 10.3.6);
    /// tầng trên cùng neo vào dầm mái 20d (bẻ L); tầng dưới cùng neo xuống móng 0,5 m + móc chân;
    /// đai kín và đai C, vùng đai dày đầu cột dài max(h; L/6; 450 mm).</summary>
    private static void Detail(Project p, ColumnDesign d, List<FrameElement> stack, ConcreteDesignSettings cs, double zBase)
    {
        int ns = d.Stories.Count;
        for (int si = 0; si < ns; si++)
        {
            var s = d.Stories[si];
            double dm = s.Dia / 1000.0;
            double z0 = s.ZBottom - zBase, z1 = s.ZTop - zBase;
            bool bottom = si == 0, top = si == ns - 1;
            double start = z0, end;
            var segs = new List<double>();
            string shape = "Thẳng";
            double length;
            double lap = DesignUtil.R10(30 * dm);
            if (top)
            {
                end = z1 - cs.CoverBeam;
                double hook = DesignUtil.R10(20 * dm);
                double vert = end - start;
                if (bottom) { vert += FootingAnchor; start -= FootingAnchor; }
                vert = DesignUtil.R10(vert);
                if (bottom)
                {
                    double foot = DesignUtil.R10(Math.Max(0.20, 10 * dm));
                    segs.Add(DesignUtil.Mm(foot)); segs.Add(DesignUtil.Mm(vert)); segs.Add(DesignUtil.Mm(hook));
                    shape = "U"; length = foot + vert + hook;
                }
                else
                {
                    segs.Add(DesignUtil.Mm(vert)); segs.Add(DesignUtil.Mm(hook));
                    shape = "L"; length = vert + hook;
                }
            }
            else
            {
                end = z1 + lap;
                double vert = end - start;
                if (bottom)
                {
                    vert += FootingAnchor; start -= FootingAnchor;
                    vert = DesignUtil.R10(vert);
                    double foot = DesignUtil.R10(Math.Max(0.20, 10 * dm));
                    segs.Add(DesignUtil.Mm(vert)); segs.Add(DesignUtil.Mm(foot));
                    shape = "L"; length = foot + vert;
                }
                else
                {
                    vert = DesignUtil.R10(vert);
                    segs.Add(DesignUtil.Mm(vert));
                    length = vert;
                }
            }
            int total = 2 * s.BarsAlongB + 2 * Math.Max(0, s.BarsAlongH - 2);
            d.Bars.Add(new BarMark
            {
                Role = "Thép dọc", Dia = s.Dia, Count = total, Start = start, End = end, Span = si,
                Length = DesignUtil.R10(length), Shape = shape, Segments = segs
            });
            if (s.Length > 11.7) d.Messages.Add("Thép dọc tầng " + s.Story + " dài hơn 11,7 m – cần nối thêm");

            // đai
            double c = cs.CoverColumn - s.StirrupDia / 1000.0;   // lớp bảo vệ tính tới mép cốt dọc → mép đai
            c = Math.Max(c, 0.015);
            double s1 = s.B - 2 * c, s2 = s.H - 2 * c;
            double zone = Math.Max(Math.Max(s.H, s.B), Math.Max(s.Length / 6, 0.45));
            zone = Math.Min(zone, s.Length / 2);
            double se = s.StirrupSpacingEnd, sm = s.StirrupSpacing;
            int ne = (int)Math.Ceiling(zone / se - 1e-9);
            double ze = Math.Min(ne * se, s.Length / 2);
            double mid = s.Length - 2 * ze;
            int cnt = mid > 1e-6 ? 2 * (ne + 1) + Math.Max((int)Math.Ceiling(mid / sm - 1e-9) - 1, 0) : 2 * ne + 1;
            d.StirrupZones.Add(new StirrupZone { Span = si, From = z0, To = z0 + ze, Dia = s.StirrupDia, Spacing = se });
            if (mid > 1e-6) d.StirrupZones.Add(new StirrupZone { Span = si, From = z0 + ze, To = z1 - ze, Dia = s.StirrupDia, Spacing = sm });
            d.StirrupZones.Add(new StirrupZone { Span = si, From = z1 - ze, To = z1, Dia = s.StirrupDia, Spacing = se });
            d.Bars.Add(new BarMark
            {
                Role = "Đai", Dia = s.StirrupDia, Count = cnt, Shape = "Đai", Spacing = se, Start = z0, End = z1, Span = si,
                Length = DesignUtil.StirrupLength(s1, s2, s.StirrupDia), Segments = new List<double> { DesignUtil.Mm(s1), DesignUtil.Mm(s2) }
            });
            // đai C giằng thanh giữa cạnh: cách một thanh giằng một thanh (TCVN 5574:2018 mục 10.3.4.3)
            int tiesH = s.BarsAlongB >= 4 ? (s.BarsAlongB - 2 + 1) / 2 : 0;   // giằng 2 cạnh b, chạy theo phương h
            int tiesB = s.BarsAlongH >= 4 ? (s.BarsAlongH - 2 + 1) / 2 : 0;
            if (tiesH > 0)
                d.Bars.Add(new BarMark
                {
                    Role = "Đai C", Dia = s.StirrupDia, Count = tiesH * cnt, Shape = "Đai C", Spacing = se, Start = z0, End = z1, Span = si,
                    Length = DesignUtil.TieLength(s2, s.StirrupDia), Segments = new List<double> { DesignUtil.Mm(s2) }
                });
            if (tiesB > 0)
                d.Bars.Add(new BarMark
                {
                    Role = "Đai C", Dia = s.StirrupDia, Count = tiesB * cnt, Shape = "Đai C", Spacing = se, Start = z0, End = z1, Span = si,
                    Length = DesignUtil.TieLength(s1, s.StirrupDia), Segments = new List<double> { DesignUtil.Mm(s1) }
                });
        }
    }

    private static string Signature(ColumnDesign d)
    {
        var st = string.Join("/", d.Stories.Select(s => s.Story + "," + Math.Round(s.B * 1000) + "x" + Math.Round(s.H * 1000) + "," + Math.Round(s.Length * 100)));
        return st + "|" + RebarNumbering.Signature(d.Bars);
    }
}
