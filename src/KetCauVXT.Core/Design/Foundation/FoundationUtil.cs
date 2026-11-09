using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

/// <summary>Nội lực chân cột (lực tác dụng xuống móng) của một tổ hợp, theo quy ước của FootingDesigner:
/// N = Fz phản lực (dương = nén), Mx, My, Qx, Qy là phản lực gối tác dụng lên kết cấu.</summary>
public struct BaseLoad
{
    public string Combo;
    public double N, Mx, My, Qx, Qy;
}

/// <summary>Tiện ích dùng chung cho các module móng (móng đơn, đài cọc, móng băng).</summary>
public static class FoundationUtil
{
    /// <summary>Loại móng hiệu lực tại nút chân cột: Footing.Type (nếu có) hoặc Project.Foundation.Type.</summary>
    public static FoundationType TypeOf(Project p, int nodeId)
    {
        if (p == null) return FoundationType.Isolated;
        var f = p.Footings?.FirstOrDefault(x => x != null && x.NodeId == nodeId && x.Type.HasValue);
        if (f?.Type != null) return f.Type.Value;
        return p.Foundation?.Type ?? FoundationType.Isolated;
    }

    /// <summary>Các chân cột BTCT có nút dưới là gối (mỗi nút một lần), sắp theo Y rồi X.</summary>
    public static List<(Node Node, FrameElement Column)> BaseColumns(Project p)
    {
        var res = new List<(Node, FrameElement)>();
        var seen = new HashSet<int>();
        foreach (var f in p.Frames.Where(f => f.Kind == FrameKind.Column))
        {
            Node lo;
            try { lo = DesignUtil.LowerNode(p, f); }
            catch (KeyNotFoundException) { continue; }
            if (lo.IsSupport && seen.Add(lo.Id)) res.Add((lo, f));
        }
        return res.OrderBy(x => Math.Round(x.Item1.Y, 2)).ThenBy(x => Math.Round(x.Item1.X, 2)).ToList();
    }

    /// <summary>Nội lực chân cột tại nút nodeId cho các tổ hợp combos (bỏ qua tổ hợp không có phản lực).
    /// scale: hệ số nhân (vd. 1/kt để quy về tiêu chuẩn).</summary>
    public static List<BaseLoad> Loads(AnalysisResults r, int nodeId, IEnumerable<string> combos, double scale = 1.0, bool momentFactor = true)
    {
        var loads = new List<BaseLoad>();
        double km = momentFactor ? MomentFactor(r?.Project) : 1.0;   // [Truyền tải] mô men / lực cắt chân cột truyền vào móng
        foreach (string cn in combos)
        {
            CaseResult cr;
            try { cr = r.Get(cn); }
            catch (Exception) { continue; }
            if (cr == null || !cr.Reactions.TryGetValue(nodeId, out var R) || R == null || R.Length < 5) continue;
            loads.Add(new BaseLoad
            {
                Combo = cn, N = R[2] * scale, Mx = R[3] * scale * km, My = R[4] * scale * km, Qx = R[0] * scale * km, Qy = R[1] * scale * km
            });
        }
        return loads;
    }

    /// <summary>[Truyền tải] Hệ số mô men + lực cắt chân cột truyền vào móng (FoundationSettings.BaseMomentFactor, 0…1).</summary>
    public static double MomentFactor(Project p) => Math.Clamp(p?.Foundation?.BaseMomentFactor ?? 1.0, 0, 1);

    /// <summary>[Truyền tải] Tổ hợp dùng thiết kế móng: tổ hợp TTGH I; khi FoundationSettings.GravityCombosOnly – bỏ các tổ hợp có gió / động đất
    /// (còn lại ≥ 1 tổ hợp, ngược lại giữ nguyên).</summary>
    public static List<string> Combos(Project p, AnalysisResults r, List<string> combos = null)
    {
        combos ??= r.DesignCombos();
        if (p?.Foundation?.GravityCombosOnly != true) return combos;
        var g = combos.Where(c => IsGravityCombo(p, r, c)).ToList();
        return g.Count > 0 ? g : combos;
    }

    /// <summary>Tổ hợp chỉ gồm tải đứng (không chứa trường hợp gió / động đất, xét cả tổ hợp bao lồng nhau).</summary>
    public static bool IsGravityCombo(Project p, AnalysisResults r, string name, int depth = 0)
    {
        if (depth > 6) return true;
        var pat = p.Patterns.FirstOrDefault(x => x.Name == name);
        if (pat != null) return pat.Type is not (LoadType.Wind or LoadType.Seismic);
        var c = r?.FindCombo(name) ?? p.Combinations.FirstOrDefault(x => x.Name == name);
        if (c == null) return !(name.Contains("GIO", StringComparison.OrdinalIgnoreCase) || name.Contains("WIND", StringComparison.OrdinalIgnoreCase) || name.Contains("DD", StringComparison.Ordinal));
        return c.Items.Where(i => Math.Abs(i.Factor) > 1e-12).All(i => IsGravityCombo(p, r, i.Case, depth + 1));
    }

    /// <summary>Tổ hợp tiêu chuẩn (TTGH II) thật sự có trong dự án; rỗng nếu chưa khai báo.</summary>
    public static List<string> ServiceCombosStrict(AnalysisResults r) => r.CombosOfType(ComboType.SLS).ToList();
}
