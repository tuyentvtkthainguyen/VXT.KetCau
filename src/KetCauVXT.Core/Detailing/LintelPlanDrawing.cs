using KetCauVXT.Drawing;

namespace KetCauVXT.Detailing;

/// <summary>Mục "LANH TÔ – Ô VĂNG CÁC TẦNG": mỗi tầng có lanh tô / ô văng đặt trên mặt bằng → một tờ gồm MẶT BẰNG LANH TÔ – Ô VĂNG của tầng
/// (cột, lanh tô, ô văng + nền tường / cửa kiến trúc từ ánh xạ nếu có) và chi tiết các cấu kiện lanh tô – ô văng của tầng đó.
/// Cấu kiện đặt ở nhiều tầng nằm ở tầng thấp nhất.</summary>
public static class LintelPlanDrawing
{
    public const string TitlePrefix = "MẶT BẰNG LANH TÔ – Ô VĂNG ";

    public static string Title(PlanLevel lv) => TitlePrefix + (lv?.Name ?? "").ToUpperInvariant() + " (" + DxfWriter.ElevText(lv?.Elevation ?? 0) + ")";

    public static bool HasLintels(PlanLevel lv) => lv != null && !lv.IsFoundation && lv.Items.Any(i => i.Kind == PlanKinds.Lintel || i.Kind == PlanKinds.Canopy);

    /// <summary>Hình mặt bằng lanh tô – ô văng của tầng (null nếu tầng không có lanh tô / ô văng).</summary>
    public static Figure PlanFigure(StructuralPlan plan, PlanLevel lv, Dictionary<string, (IDetailMember M, DetailResult R)> map, string projectDir = null)
    {
        if (plan == null || !HasLintels(lv)) return null;
        var tmp = new PlanLevel { Name = lv.Name + " · lanh tô", Elevation = lv.Elevation };
        tmp.Items.AddRange(lv.Items.Where(i => i.Kind == PlanKinds.Column || i.Kind == PlanKinds.Lintel || i.Kind == PlanKinds.Canopy));
        string title = Title(lv);
        var w = StructuralPlanDrawing.Level(plan, tmp, map, title, false);
        // nền kiến trúc (ánh xạ): tường mảnh trên layer ký hiệu
        try
        {
            var u = lv.UnderlayVisible ? ArchUnderlay.Load(lv, projectDir) : null;
            if (u != null)
                foreach (var s in u.Walls)
                    w.Line(Lay.KyHieu, (s.X1 + lv.UnderlayDx) * 1000, (s.Y1 + lv.UnderlayDy) * 1000, (s.X2 + lv.UnderlayDx) * 1000, (s.Y2 + lv.UnderlayDy) * 1000);   // bản vẽ mặt bằng: mm
        }
        catch { }
        return new Figure(w, title, true);
    }

    /// <summary>Các tờ lanh tô theo tầng: (tầng, hình mặt bằng, cấu kiện lanh tô – ô văng của tầng).</summary>
    public static List<(PlanLevel Level, Figure Plan, List<IDetailMember> Members)> Sheets(DetailingProject p, List<(IDetailMember Member, DetailResult Result)> res, string projectDir = null)
    {
        var list = new List<(PlanLevel, Figure, List<IDetailMember>)>();
        var plan = p?.Plan;
        if (plan == null) return list;
        var map = StructuralPlanDrawing.Map(p, res);
        var taken = new HashSet<IDetailMember>();
        foreach (var lv in plan.Levels.Where(HasLintels).OrderBy(l => l.Elevation))
        {
            var mem = new List<IDetailMember>();
            foreach (var it in lv.Items.Where(i => i.Kind == PlanKinds.Lintel || i.Kind == PlanKinds.Canopy))
            {
                var m = PlanMemberMap.Find(p, it);
                if (m is ManualLintel && taken.Add(m)) mem.Add(m);
            }
            Figure f = null;
            try { f = PlanFigure(plan, lv, map, projectDir); } catch { }
            if (f != null || mem.Count > 0) list.Add((lv, f, mem));
        }
        return list;
    }
}
