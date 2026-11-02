using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Kết quả kiểm tra bố trí một nhóm thép dọc trong bề rộng dầm.</summary>
public readonly record struct BarFit(int Layers, int PerLayer, double ClearMm, double ClearReqMm, bool Fits);

/// <summary>Một phương án bố trí thép cho một vùng (dầm, cột, sàn, móng) để người dùng chọn.</summary>
public class RebarAlternative
{
    /// <summary>"Dầm", "Cột", "Sàn", "Móng"</summary>
    public string Kind { get; set; } = "";
    public string Zone { get; set; } = "";
    public int Index { get; set; } = -1;
    /// <summary>Dầm: các nhóm thanh (tổng tại tiết diện).</summary>
    public List<RebarGroup> Bars { get; set; } = new();
    public int Dia { get; set; }
    public int NB { get; set; }
    public int NH { get; set; }
    /// <summary>Bước thép rải / bước đai vùng gối (m)</summary>
    public double Spacing { get; set; }
    /// <summary>Bước đai giữa nhịp (m)</summary>
    public double Spacing2 { get; set; }
    public string Text { get; set; } = "";
    /// <summary>Diện tích yêu cầu / bố trí: m² (dầm, cột) hoặc m²/m (sàn, móng)</summary>
    public double AsReq { get; set; }
    public double AsProv { get; set; }
    public bool PerMeter => Kind == "Sàn" || Kind == "Móng";
    /// <summary>Phần trăm thừa so với yêu cầu</summary>
    public double OverPct => AsReq > 1e-12 ? (AsProv / AsReq - 1) * 100 : 0;
    public int Layers { get; set; } = 1;
    /// <summary>Khoảng hở thực tế nhỏ nhất giữa các thanh (mm) / yêu cầu (mm)</summary>
    public double ClearMm { get; set; }
    public double ClearReqMm { get; set; }
    public bool Fits { get; set; } = true;
    /// <summary>Cột: hệ số sử dụng (≤ 1); đai dầm: Q/Qu</summary>
    public double Utilization { get; set; }
    public bool IsCurrent { get; set; }
    public string Note { get; set; } = "";

    /// <summary>Lựa chọn lưu vào Project cho cấu kiện member.</summary>
    public RebarChoice ToChoice(string member, string basis, string label) => new()
    {
        Member = member, Zone = Zone, Index = Index, Dia = Dia, NB = NB, NH = NH, Spacing = Spacing, Spacing2 = Spacing2,
        Bars = Bars.Select(g => new RebarGroup(g.Dia, g.Count)).ToList(), Basis = basis ?? "", Label = label ?? ""
    };

    public override string ToString() => Text;
}

/// <summary>Một vùng thép của cấu kiện có thể chọn phương án.</summary>
public class RebarZoneInfo
{
    public string Kind { get; set; } = "";
    public string Zone { get; set; } = "";
    public int Index { get; set; } = -1;
    /// <summary>Tên hiển thị: "Nhịp 1 – thép dưới", "Gối 2 (B) – thép trên"…</summary>
    public string Label { get; set; } = "";
    public double AsReq { get; set; }
    public string Current { get; set; } = "";
    public bool Top { get; set; }
    /// <summary>Đang dùng lựa chọn của người dùng</summary>
    public bool User { get; set; }
    /// <summary>Kích thước để vẽ phác mặt cắt (m): dầm/cột b×h; sàn/móng: dải 1 m × chiều dày</summary>
    public double B { get; set; }
    public double H { get; set; }
    public double Cover { get; set; }
    /// <summary>Cơ sở kích thước lưu kèm lựa chọn (RebarChoice.Basis)</summary>
    public string Basis { get; set; } = "";
    public override string ToString() => Label + (User ? " ★" : "");
}

/// <summary>Sinh và áp dụng các phương án bố trí thép (lựa chọn bố trí thép) cho dầm, cột, sàn, móng đơn.
/// Mọi phương án trả về đều thoả diện tích yêu cầu và điều kiện khoảng hở TCVN 5574:2018 mục 10.3.5
/// (dầm: ≥ max(d; 25 mm) thép dưới, ≥ max(d; 30 mm) thép trên, tối đa 2 hàng; cột: ≥ max(d; 50 mm), tim ≤ 400 mm; sàn/móng: bước 100…smax).
/// Lựa chọn lưu ở Project.RebarChoices theo khoá MemberDesignBase.RebarKeys; bộ thiết kế đọc lại và kiểm tra khả năng chịu lực.</summary>
public static class RebarOptions
{
    // ------------------------------------------------------------------
    // Kiểm tra bố trí
    // ------------------------------------------------------------------

    /// <summary>Bố trí nhóm thép (Ø lớn đặt hàng 1 trước) trong bề rộng width: số hàng, số thanh/hàng, khoảng hở hàng 1.</summary>
    public static BarFit CheckFit(IList<(int Dia, int Count)> groups, double width, double cover, bool top, int maxLayers = 2, int cap = 0)
    {
        var bars = groups.Where(g => g.Count > 0 && g.Dia > 0).SelectMany(g => Enumerable.Repeat(g.Dia, g.Count)).OrderByDescending(x => x).ToList();
        if (bars.Count == 0) return new BarFit(0, 0, 0, 0, true);
        int dmax = bars[0];
        double clr = RcSection.ClearRequired(dmax, top);
        int per = RcSection.MaxBarsInLayer(width, cover, dmax, top ? RcSection.ClearTop : RcSection.ClearBottom);
        if (cap > 0) per = Math.Min(per, cap);
        if (per < Math.Min(2, bars.Count) || per <= 0) return new BarFit(99, per, 0, clr * 1000, false);
        int layers = (int)Math.Ceiling(bars.Count / (double)per);
        var l1 = bars.Take(per).ToList();
        double avail = width - 2 * cover;
        double clear = l1.Count > 1 ? (avail - l1.Sum() / 1000.0) / (l1.Count - 1) : avail - l1[0] / 1000.0;
        bool fits = layers <= Math.Max(maxLayers, 1) && clear >= clr - 1e-9;
        return new BarFit(layers, per, clear * 1000, clr * 1000, fits);
    }

    /// <summary>Các tổ hợp thép dầm cho diện tích yêu cầu asReq tại một tiết diện (một hoặc hai loại Ø chênh ≤ 6 mm, minCount…maxCount thanh),
    /// đều lọt bề rộng; sắp theo diện tích thừa, số hàng, số thanh. exactCount &gt; 0: đúng số thanh đó (thép chạy suốt 2 thanh góc).</summary>
    public static List<RebarAlternative> BeamCombos(double asReq, double b, double cover, bool top, int[] dias, RebarPreferences pref,
        int minCount = 2, int exactCount = 0, int max = 12)
    {
        pref ??= new RebarPreferences();
        int maxLayers = pref.AllowTwoLayers ? 2 : 1, cap = Math.Max(pref.MaxBarsPerLayer, 0);
        var ds = (dias ?? Array.Empty<int>()).Where(x => x > 0).Distinct().OrderBy(x => x).ToArray();
        var res = new List<(RebarAlternative A, double Score)>();
        var seen = new HashSet<string>();
        void Consider(List<(int Dia, int Count)> g)
        {
            int n = g.Sum(x => x.Count);
            if (exactCount > 0 && n != exactCount) return;
            if (n < minCount) return;
            double area = g.Sum(x => x.Count * RcSection.BarArea(x.Dia));
            if (area < asReq * (1 - 1e-6)) return;
            var fit = CheckFit(g, b, cover, top, maxLayers, cap);
            if (!fit.Fits) return;
            string txt = RcSection.BarText(g);
            if (!seen.Add(txt)) return;
            double score = area * (1 + 0.04 * (fit.Layers - 1) + 0.005 * n + 0.02 * (g.Count - 1) + (pref.PreferSameDia && g.Count > 1 ? 0.05 : 0));
            res.Add((new RebarAlternative
            {
                Kind = "Dầm", Bars = g.Select(x => new RebarGroup(x.Dia, x.Count)).ToList(), Dia = g.Max(x => x.Dia), Text = txt,
                AsReq = asReq, AsProv = area, Layers = fit.Layers, ClearMm = fit.ClearMm, ClearReqMm = fit.ClearReqMm, Fits = true,
                Note = (fit.Layers > 1 ? "2 hàng" : "1 hàng") + ", " + fit.PerLayer + " thanh/hàng"
            }, score));
        }
        foreach (int d1 in ds)
        {
            int nmax = Math.Max(RcSection.MaxBarsInLayer(b, cover, d1, top ? RcSection.ClearTop : RcSection.ClearBottom) * maxLayers, minCount);
            if (cap > 0) nmax = Math.Min(nmax, cap * maxLayers);
            if (exactCount > 0) nmax = Math.Min(nmax, exactCount);
            for (int n1 = Math.Max(1, exactCount > 0 ? 1 : minCount); n1 <= nmax; n1++)
            {
                Consider(new List<(int, int)> { (d1, n1) });
                if (n1 < 2) continue;
                foreach (int d2 in ds)
                {
                    if (d2 >= d1 || d1 - d2 > 6) continue;
                    for (int n2 = 1; n1 + n2 <= nmax; n2++)
                        Consider(new List<(int, int)> { (d1, n1), (d2, n2) });
                }
            }
        }
        // tránh phương án quá thừa khi đã có đủ phương án sát yêu cầu
        var sorted = res.OrderBy(x => x.Score).Select(x => x.A).ToList();
        var near = sorted.Where(a => a.AsReq <= 1e-12 || a.OverPct <= 60).ToList();
        if (near.Count >= Math.Min(4, sorted.Count)) sorted = near.Concat(sorted.Where(a => !near.Contains(a))).ToList();
        return sorted.Take(max).ToList();
    }

    /// <summary>Các phương án Ø–bước cho thép rải (sàn, móng): với mỗi Ø, bước lớn nhất thoả As (làm tròn xuống 10 mm, ≤ sMax)
    /// và bước chẵn 50 mm nhỏ hơn kế tiếp; bước ≥ sMin.</summary>
    public static List<RebarAlternative> SpacingAlternatives(string kind, string zone, Func<int, double> asReqPerM, int[] dias, double sMin, double sMax,
        string current, int max = 12)
    {
        var res = new List<RebarAlternative>();
        foreach (int d in dias.Where(x => x > 0).Distinct().OrderBy(x => x))
        {
            double req = Math.Max(asReqPerM(d), 0);
            double s = req > 1e-12 ? RcSection.FloorTo(RcSection.BarArea(d) / req, 0.01) : sMax;
            s = Math.Round(Math.Min(s, sMax), 3);
            if (s < sMin - 1e-9) continue;
            var sps = new List<double> { s };
            double s50 = RcSection.FloorTo(s - 1e-6, 0.05);
            if (s50 < s - 1e-6 && s50 >= sMin - 1e-9) sps.Add(Math.Round(s50, 3));
            foreach (double sp in sps)
            {
                var a = new RebarAlternative
                {
                    Kind = kind, Zone = zone, Index = -1, Dia = d, Spacing = sp,
                    Text = "Ø" + d.ToString(DesignUtil.Inv) + "a" + DesignUtil.Mm(sp).ToString("0", DesignUtil.Inv),
                    AsReq = req, AsProv = RcSection.BarArea(d) / sp, Fits = true, ClearMm = DesignUtil.Mm(sp) - d, ClearReqMm = Math.Max(d, 25),
                    Note = "bước " + DesignUtil.Mm(sMin).ToString("0", DesignUtil.Inv) + "…" + DesignUtil.Mm(sMax).ToString("0", DesignUtil.Inv) + " mm"
                };
                a.IsCurrent = string.Equals(a.Text, (current ?? "").Trim(), StringComparison.Ordinal);
                res.Add(a);
            }
        }
        return res.OrderBy(a => a.AsProv).ThenBy(a => a.Dia).Take(max).ToList();
    }

    // ------------------------------------------------------------------
    // Vùng thép của cấu kiện
    // ------------------------------------------------------------------

    /// <summary>Các vùng thép chọn được của cấu kiện m (dầm, cột, ô sàn, móng đơn).</summary>
    public static List<RebarZoneInfo> Zones(Project p, MemberDesignBase m)
    {
        var z = new List<RebarZoneInfo>();
        if (p == null || m == null) return z;
        string key = m.RebarKeys.FirstOrDefault() ?? "";
        bool U(string zone, int idx, string basis) => p.ValidRebarChoice(key, zone, idx, basis) != null;
        var cs = p.Concrete;
        switch (m)
        {
            case BeamLineDesign b:
            {
                string basis = BeamDesigner.Basis(b.B, b.H);
                int ns = b.Spans.Count;
                var bot = b.Bars.FirstOrDefault(x => x.Role == "Dưới");
                var top = b.Bars.FirstOrDefault(x => x.Role == "Trên");
                void Add(string zone, int idx, string label, double asReq, string cur, bool isTop) => z.Add(new RebarZoneInfo
                {
                    Kind = "Dầm", Zone = zone, Index = idx, Label = label, AsReq = asReq, Current = cur, Top = isTop,
                    User = U(zone, idx, basis), B = b.B, H = b.H, Cover = cs.CoverBeam, Basis = basis
                });
                for (int k = 0; k < ns; k++)
                    Add(RebarZones.BeamBottom, k, "Nhịp " + (k + 1) + " (" + b.Spans[k].LeftSupport + "–" + b.Spans[k].RightSupport + ") – thép dưới",
                        b.Spans[k].AsBotReq, b.Spans[k].BotText, false);
                for (int j = 0; j <= ns; j++)
                {
                    double req = j < b.SupportAsReq.Count ? b.SupportAsReq[j]
                        : Math.Max(j > 0 ? b.Spans[j - 1].AsTopRightReq : 0, j < ns ? b.Spans[j].AsTopLeftReq : 0);
                    string name = j < ns ? b.Spans[j].LeftSupport : b.Spans[ns - 1].RightSupport;
                    string cur = j < ns ? b.Spans[j].TopLeftText : b.Spans[ns - 1].TopRightText;
                    Add(RebarZones.BeamTopSupport, j, "Gối " + (j + 1) + " (" + name + ") – thép trên", req, cur, true);
                }
                Add(RebarZones.BeamTopCont, -1, "Thép trên chạy suốt (giữa nhịp)", b.ContTopAsReq, top != null ? "2Ø" + top.Dia : "", true);
                Add(RebarZones.BeamBotCont, -1, "Thép dưới chạy suốt", b.ContBotAsReq, bot != null ? "2Ø" + bot.Dia : "", false);
                if (!b.IsCurved)
                    for (int k = 0; k < ns; k++)
                    {
                        var s = b.Spans[k];
                        Add(RebarZones.Stirrup, k, "Nhịp " + (k + 1) + " – cốt đai", 0,
                            s.StirrupDia > 0 ? "Ø" + s.StirrupDia + "a" + DesignUtil.Mm(s.StirrupSupport) + "/a" + DesignUtil.Mm(s.StirrupMid) : "", false);
                    }
                break;
            }
            case ColumnDesign c:
                for (int i = 0; i < c.Stories.Count; i++)
                {
                    var s = c.Stories[i];
                    string basis = BeamDesigner.Basis(s.B, s.H);
                    z.Add(new RebarZoneInfo
                    {
                        Kind = "Cột", Zone = RebarZones.ColumnMain, Index = i, Label = "Tầng " + s.Story + " – thép dọc", AsReq = s.AsReq,
                        Current = s.Dia > 0 ? s.TotalBars + "Ø" + s.Dia + " (" + s.BarsAlongB + "×" + s.BarsAlongH + ")" : "",
                        User = U(RebarZones.ColumnMain, i, basis), B = s.B, H = s.H, Cover = cs.CoverColumn, Basis = basis
                    });
                    z.Add(new RebarZoneInfo
                    {
                        Kind = "Cột", Zone = RebarZones.Stirrup, Index = i, Label = "Tầng " + s.Story + " – cốt đai",
                        Current = s.StirrupDia > 0 ? "Ø" + s.StirrupDia + "a" + DesignUtil.Mm(s.StirrupSpacingEnd) + "/a" + DesignUtil.Mm(s.StirrupSpacing) : "",
                        User = U(RebarZones.Stirrup, i, basis), B = s.B, H = s.H, Cover = cs.CoverColumn, Basis = basis
                    });
                }
                break;
            case SlabDesign s:
            {
                string basis = SlabDesigner.Basis(s.Thickness);
                void Add(string zone, string label, string cur, bool isTop) => z.Add(new RebarZoneInfo
                {
                    Kind = "Sàn", Zone = zone, Index = -1, Label = label, Current = cur, Top = isTop, User = U(zone, -1, basis),
                    B = 1.0, H = s.Thickness, Cover = cs.CoverSlab, Basis = basis
                });
                Add(RebarZones.SlabBotX, "Lớp dưới phương ngắn (X)", s.BotX, false);
                Add(RebarZones.SlabBotY, "Lớp dưới phương dài (Y)", s.BotY, false);
                Add(RebarZones.SlabTopX, "Mũ gối phương ngắn (X)", s.TopX, true);
                Add(RebarZones.SlabTopY, "Mũ gối phương dài (Y)", s.TopY, true);
                break;
            }
            case FootingDesign f:
            {
                string basis = FootingDesigner.Basis(f.L, f.B, f.H);
                z.Add(new RebarZoneInfo { Kind = "Móng", Zone = RebarZones.FootingL, Label = "Thép đáy phương L", AsReq = f.AsL, Current = f.BarsL, User = U(RebarZones.FootingL, -1, basis), B = 1.0, H = f.H, Cover = cs.CoverFooting, Basis = basis });
                z.Add(new RebarZoneInfo { Kind = "Móng", Zone = RebarZones.FootingB, Label = "Thép đáy phương B", AsReq = f.AsB, Current = f.BarsB, User = U(RebarZones.FootingB, -1, basis), B = 1.0, H = f.H, Cover = cs.CoverFooting, Basis = basis });
                break;
            }
        }
        return z;
    }

    /// <summary>Các phương án cho vùng z của cấu kiện m. r (kết quả phân tích) cần cho cột (kiểm tra nén lệch tâm) và đai dầm (lực cắt đã lưu ở m).</summary>
    public static List<RebarAlternative> Alternatives(Project p, AnalysisResults r, MemberDesignBase m, RebarZoneInfo z, int max = 12)
    {
        var list = new List<RebarAlternative>();
        if (p == null || m == null || z == null) return list;
        var cs = p.Concrete;
        var pref = p.RebarPrefs ?? new RebarPreferences();
        switch (m)
        {
            case BeamLineDesign b:
            {
                int[] dias = pref.Filter(DesignUtil.Sorted(cs.BeamBars, new[] { 14, 16, 18, 20, 22, 25 }));
                if (z.Zone == RebarZones.Stirrup) { list.AddRange(BeamStirrups(p, b, z.Index)); break; }
                bool cont = z.Zone == RebarZones.BeamTopCont || z.Zone == RebarZones.BeamBotCont;
                list.AddRange(BeamCombos(z.AsReq, b.B, cs.CoverBeam, z.Top, dias, pref, 2, cont ? 2 : 0, max));
                if (!cont)
                {
                    int dc = b.Bars.FirstOrDefault(x => x.Role == (z.Top ? "Trên" : "Dưới"))?.Dia ?? 0;
                    foreach (var a in list)
                        if (dc > 0 && a.Bars.Where(g => g.Dia == dc).Sum(g => g.Count) < 2)
                            a.Note += "; khác Ø chạy suốt 2Ø" + dc + " – thiết kế lại sẽ chọn lại thép chạy suốt";
                }
                break;
            }
            case ColumnDesign c:
                if (z.Zone == RebarZones.Stirrup) list.AddRange(ColumnStirrups(p, c, z.Index));
                else list.AddRange(ColumnDesigner.Alternatives(p, r, c, z.Index, max));
                break;
            case SlabDesign s:
                list.AddRange(SlabDesigner.Alternatives(p, s, z.Zone, max));
                break;
            case FootingDesign f:
                list.AddRange(FootingDesigner.Alternatives(p, f, z.Zone, max));
                break;
        }
        foreach (var a in list)
        {
            a.Zone = z.Zone; a.Index = z.Index; a.Kind = z.Kind;
            if (m is BeamLineDesign && a.Bars.Count > 0) a.IsCurrent = RcSection.BarText(a.Bars.Select(g => (g.Dia, g.Count))) == z.Current;
        }
        return list;
    }

    /// <summary>Phương án đai dầm cho nhịp k: mỗi Ø đai một phương án bước theo tính toán (gối/giữa nhịp) và một phương án dày hơn 50 mm.</summary>
    private static List<RebarAlternative> BeamStirrups(Project p, BeamLineDesign b, int k)
    {
        var res = new List<RebarAlternative>();
        if (k < 0 || k >= b.Spans.Count) return res;
        var cs = p.Concrete;
        var sec = p.FrameSections.FirstOrDefault(x => x.Name == b.Section);
        var conc = DesignUtil.ConcreteOf(p, sec?.Material ?? "", cs.ConcreteBeam);
        var stl = p.Material(cs.RebarStirrup);
        double h0 = b.H - cs.CoverBeam - 0.010;
        var sp = b.Spans[k];
        foreach (int d in DesignUtil.Sorted(cs.StirrupBars, new[] { 6, 8, 10 }).Where(x => x >= 6))
        {
            var sh = RcSection.Shear(sp.QMax, b.B, h0, conc, stl, d);
            if (!sh.Ok || sh.SSupport <= 0) continue;
            foreach (double ss in new[] { sh.SSupport, RcSection.FloorTo(sh.SSupport - 0.05 + 1e-9, 0.05) }.Distinct())
            {
                if (ss < 0.05 - 1e-9) continue;
                double sm = Math.Max(sh.SMid, ss);
                double qu = RcSection.ShearCapacity(b.B, h0, conc, stl, d, 2, ss);
                if (qu < sp.QMax - 1e-6) continue;
                res.Add(new RebarAlternative
                {
                    Kind = "Dầm", Zone = RebarZones.Stirrup, Index = k, Dia = d, Spacing = ss, Spacing2 = sm,
                    Text = "Ø" + d + "a" + DesignUtil.Mm(ss) + "/a" + DesignUtil.Mm(sm), AsReq = sp.QMax, AsProv = qu,
                    Utilization = qu > 0 ? sp.QMax / qu : 0, Fits = true, ClearMm = DesignUtil.Mm(ss) - d, ClearReqMm = 50,
                    IsCurrent = d == sp.StirrupDia && Math.Abs(ss - sp.StirrupSupport) < 1e-4 && Math.Abs(sm - sp.StirrupMid) < 1e-4,
                    Note = "Q = " + DesignUtil.F(sp.QMax, "0.0") + " kN ≤ Qu = " + DesignUtil.F(qu, "0.0") + " kN"
                });
            }
        }
        return res;
    }

    /// <summary>Phương án đai cột tầng i (cấu tạo TCVN 5574:2018 mục 10.3.4.3: Ø ≥ d/4, bước ≤ min(15d; b; 400)).</summary>
    private static List<RebarAlternative> ColumnStirrups(Project p, ColumnDesign c, int i)
    {
        var res = new List<RebarAlternative>();
        if (i < 0 || i >= c.Stories.Count) return res;
        var s = c.Stories[i];
        double smax = RcSection.FloorTo(Math.Min(Math.Min(15 * s.Dia / 1000.0, Math.Min(s.B, s.H)), 0.40), 0.05);
        if (smax < 0.10) smax = Math.Max(RcSection.FloorTo(Math.Min(15 * s.Dia / 1000.0, Math.Min(s.B, s.H)), 0.01), 0.05);
        int dmin = Math.Max(6, (int)Math.Ceiling(s.Dia / 4.0));
        foreach (int d in DesignUtil.Sorted(p.Concrete.StirrupBars, new[] { 6, 8, 10 }).Where(x => x >= dmin))
            foreach (double sm in new[] { smax, RcSection.FloorTo(smax - 0.05 + 1e-9, 0.05) }.Distinct())
            {
                if (sm < 0.10 - 1e-9 && sm < smax - 1e-9) continue;
                double se = Math.Max(Math.Min(RcSection.FloorTo(sm / 2, 0.01), 0.10), 0.05);
                res.Add(new RebarAlternative
                {
                    Kind = "Cột", Zone = RebarZones.Stirrup, Index = i, Dia = d, Spacing = se, Spacing2 = sm,
                    Text = "Ø" + d + "a" + DesignUtil.Mm(se) + "/a" + DesignUtil.Mm(sm), Fits = true, ClearMm = DesignUtil.Mm(se) - d, ClearReqMm = 50,
                    IsCurrent = d == s.StirrupDia && Math.Abs(se - s.StirrupSpacingEnd) < 1e-4 && Math.Abs(sm - s.StirrupSpacing) < 1e-4,
                    Note = "đầu cột a" + DesignUtil.Mm(se) + ", giữa a" + DesignUtil.Mm(sm) + " (≤ " + DesignUtil.Mm(smax) + ")"
                });
            }
        return res;
    }

    // ------------------------------------------------------------------
    // Ghi / xoá lựa chọn
    // ------------------------------------------------------------------

    /// <summary>Ghi phương án a cho mọi cấu kiện vật lý gộp trong m (RebarKeys). Trả về số lựa chọn đã ghi.</summary>
    public static int Apply(Project p, MemberDesignBase m, RebarZoneInfo z, RebarAlternative a)
    {
        if (p == null || m == null || z == null || a == null) return 0;
        int n = 0;
        foreach (string key in m.RebarKeys.Distinct())
        {
            var ch = a.ToChoice(key, z.Basis, m.Name);
            ch.Zone = z.Zone; ch.Index = z.Index;
            p.SetRebarChoice(ch);
            n++;
        }
        return n;
    }

    /// <summary>Xoá lựa chọn của vùng z (z = null: mọi vùng) cho mọi cấu kiện vật lý gộp trong m.</summary>
    public static int Clear(Project p, MemberDesignBase m, RebarZoneInfo z = null)
    {
        if (p == null || m == null) return 0;
        int n = 0;
        foreach (string key in m.RebarKeys.Distinct())
            n += z == null ? p.ClearRebarChoice(key) : p.ClearRebarChoice(key, z.Zone, z.Index);
        return n;
    }
}
