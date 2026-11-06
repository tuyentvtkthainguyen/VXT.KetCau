namespace KetCauVXT.Detailing;

/// <summary>Phản lực giằng / dầm móng (ManualBeam, Vị trí = Dầm móng, "Giằng tựa lên móng") tự chuyển sang móng đơn / đài cọc theo tên ghi ở gối.
/// Nếu móng nhận phản lực tự động thì KHÔNG cộng thêm ô nhập tay "Phản lực dầm móng / giằng" (tránh cộng hai lần).</summary>
public static class TieReaction
{
    /// <summary>Phản lực dùng tính: tự động nếu &gt; 0, ngược lại ô nhập tay.</summary>
    public static double Use(double auto, double manual) => auto > 1e-9 ? auto : Math.Max(manual, 0);

    /// <summary>Ghi thuyết minh lực dọc kể phản lực giằng.</summary>
    public static void Report(DetailResult r, double n0, double n, double auto, string autoText, double manual)
    {
        if (auto > 1e-9)
        {
            r.Calc.Add("Lực dọc chân cột kể phản lực giằng / dầm móng (tự chuyển từ dầm móng: " + autoText + "): N = " + Dt.F(n0, "0.0") + " + " + Dt.F(auto, "0.0") + " = " + Dt.F(n, "0.0") + " kN");
            if (manual > 1e-9) r.Messages.Add("Ô \"Phản lực dầm móng / giằng\" nhập tay (" + Dt.F(manual, "0.0") + " kN) KHÔNG cộng thêm vì đã nhận phản lực tự động " + Dt.F(auto, "0.0") + " kN từ dầm móng (tránh cộng hai lần)");
        }
        else if (manual > 1e-9) r.Calc.Add("Lực dọc chân cột kể phản lực dầm móng / giằng: N = " + Dt.F(n0, "0.0") + " + " + Dt.F(manual, "0.0") + " = " + Dt.F(n, "0.0") + " kN");
    }

    /// <summary>Tên móng ở từng gối (phân cách ';' hoặc ','; ô trống = gối không tựa lên móng).</summary>
    public static List<string> SupportNames(string s) =>
        (s ?? "").Split(new[] { ';', ',' }).Select(x => x.Trim()).ToList();

    /// <summary>Tính các dầm móng "Giằng tựa lên móng" có ghi tên móng ở gối, cộng phản lực vào AutoBeamReaction của móng đơn / đài cọc cùng tên.
    /// Trả về số móng nhận phản lực.</summary>
    public static int Transfer(DetailingProject p)
    {
        foreach (var f in p.Footings) { f.AutoBeamReaction = 0; f.AutoBeamReactionText = ""; }
        foreach (var c in p.PileCaps) { c.AutoBeamReaction = 0; c.AutoBeamReactionText = ""; }
        var parts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in p.Beams)
        {
            if (!(b.Position ?? "").StartsWith("Dầm móng") || b.GroundCalc != ManualBeam.GroundTie || string.IsNullOrWhiteSpace(b.SupportFootings)) continue;
            DetailingProject.RunSafe(b, p.Settings);
            var gr = b.GroundResult;
            if (gr == null) continue;
            var names = SupportNames(b.SupportFootings);
            for (int i = 0; i < gr.Reactions.Count && i < names.Count; i++)
            {
                string nm = names[i];
                if (string.IsNullOrEmpty(nm)) continue;
                double rr = gr.Reactions[i];
                bool hit = false;
                foreach (var f in p.Footings.Where(x => string.Equals(x.Name, nm, StringComparison.OrdinalIgnoreCase))) { f.AutoBeamReaction += rr; hit = true; }
                foreach (var c in p.PileCaps.Where(x => string.Equals(x.Name, nm, StringComparison.OrdinalIgnoreCase))) { c.AutoBeamReaction += rr; hit = true; }
                if (!hit) continue;
                if (!parts.TryGetValue(nm, out var lst)) parts[nm] = lst = new List<string>();
                lst.Add(b.Name + " gối " + (i + 1) + ": " + Dt.F(rr, "0.0") + " kN");
            }
        }
        int n = 0;
        foreach (var f in p.Footings) if (f.AutoBeamReaction > 1e-9) { f.AutoBeamReactionText = string.Join("; ", parts.TryGetValue(f.Name, out var l) ? l : new()); n++; }
        foreach (var c in p.PileCaps) if (c.AutoBeamReaction > 1e-9) { c.AutoBeamReactionText = string.Join("; ", parts.TryGetValue(c.Name, out var l) ? l : new()); n++; }
        return n;
    }
}
