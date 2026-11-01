using KetCauVXT.Model;

namespace KetCauVXT.Loads;

/// <summary>Tự sinh tổ hợp tải trọng.
/// TTGH I (TCVN 2737:2023): tổ hợp cơ bản 1 (tĩnh tải + 1 tải tạm thời, đủ γf) và tổ hợp cơ bản 2
/// (tĩnh tải + ≥2 tải tạm thời nhân hệ số tổ hợp ψ = 0.9). Tổ hợp đặc biệt có động đất theo TCVN 9386:2012:
/// G + ψE·Q ± E1 ± 0.3·E2. TTGH II: giá trị tiêu chuẩn (hệ số 1.0).</summary>
public static class ComboGenerator
{
    public static void Generate(Project p, double psi = 0.9)
    {
        p.Combinations.Clear();
        var dead = p.Patterns.Where(x => x.Type is LoadType.Dead or LoadType.SuperDead).ToList();
        var live = p.Patterns.Where(x => x.Type is LoadType.Live or LoadType.RoofLive).ToList();
        var wind = p.Patterns.Where(x => x.Type == LoadType.Wind).ToList();
        var eq = p.Patterns.Where(x => x.Type == LoadType.Seismic).ToList();
        var other = p.Patterns.Where(x => x.Type == LoadType.Other).ToList();

        int n = 1;
        LoadCombination Uls(params (LoadPattern pat, double f)[] items) => Add(p, $"TH{n++}", ComboType.ULS, items);

        (LoadPattern, double)[] D(bool factored) => dead.Select(d => (d, factored ? d.Gamma : 1.0)).ToArray();

        // ---- TTGH I ----
        // TH: tĩnh + hoạt
        Uls(D(true).Concat(live.Select(l => (l, l.Gamma))).Concat(other.Select(o => (o, o.Gamma))).ToArray());
        // TH: tĩnh + gió (±)
        foreach (var w in wind)
            foreach (int s in new[] { 1, -1 })
                Uls(D(true).Append((w, s * w.Gamma)).ToArray());
        // TH: tĩnh + 0.9(hoạt + gió)
        if (live.Count > 0)
            foreach (var w in wind)
                foreach (int s in new[] { 1, -1 })
                    Uls(D(true).Concat(live.Select(l => (l, psi * l.Gamma))).Append((w, s * psi * w.Gamma)).ToArray());
        // Tổ hợp đặc biệt động đất
        if (eq.Count > 0 && p.Seismic.Enabled)
        {
            foreach (var e1 in eq)
                foreach (int s1 in new[] { 1, -1 })
                {
                    var others = eq.Where(e => e != e1).ToList();
                    if (others.Count == 0)
                        Uls(D(false).Concat(live.Select(l => (l, p.Seismic.PsiLive))).Append((e1, s1 * 1.0)).ToArray());
                    else
                        foreach (int s2 in new[] { 1, -1 })
                            Uls(D(false).Concat(live.Select(l => (l, p.Seismic.PsiLive))).Append((e1, s1 * 1.0))
                                .Concat(others.Select(e => (e, s2 * 0.3))).ToArray());
                }
        }

        // ---- TTGH II ----
        int m = 1;
        LoadCombination S(params (LoadPattern pat, double f)[] items) => Add(p, $"TC{m++}", ComboType.SLS, items);
        S(D(false).Concat(live.Select(l => (l, 1.0))).ToArray());
        foreach (var w in wind)
            foreach (int s in new[] { 1, -1 })
                S(D(false).Concat(live.Select(l => (l, psi))).Append((w, s * psi)).ToArray());

        // ---- Bao ----
        var bao = new LoadCombination { Name = "BAO", Type = ComboType.Envelope };
        foreach (var c in p.Combinations.Where(c_ => c_.Type == ComboType.ULS).ToList()) bao.Items.Add(new ComboItem(c.Name, 1));
        p.Combinations.Add(bao);
    }

    private static LoadCombination Add(Project p, string name, ComboType t, (LoadPattern pat, double f)[] items)
    {
        var c = new LoadCombination { Name = name, Type = t };
        foreach (var (pat, f) in items) c.Items.Add(new ComboItem(pat.Name, Math.Round(f, 4)));
        p.Combinations.Add(c);
        return c;
    }

    /// <summary>Mô tả tổ hợp dạng chữ: "1.1TT + 1.3HTHT + 1.2HT".</summary>
    public static string Describe(LoadCombination c) =>
        string.Join(" + ", c.Items.Select(i => $"{i.Factor:0.###}{i.Case}")).Replace("+ -", "- ");
}
