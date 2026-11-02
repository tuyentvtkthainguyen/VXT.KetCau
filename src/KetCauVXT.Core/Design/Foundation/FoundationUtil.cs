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
    public static List<BaseLoad> Loads(AnalysisResults r, int nodeId, IEnumerable<string> combos, double scale = 1.0)
    {
        var loads = new List<BaseLoad>();
        foreach (string cn in combos)
        {
            CaseResult cr;
            try { cr = r.Get(cn); }
            catch (Exception) { continue; }
            if (cr == null || !cr.Reactions.TryGetValue(nodeId, out var R) || R == null || R.Length < 5) continue;
            loads.Add(new BaseLoad
            {
                Combo = cn, N = R[2] * scale, Mx = R[3] * scale, My = R[4] * scale, Qx = R[0] * scale, Qy = R[1] * scale
            });
        }
        return loads;
    }

    /// <summary>Tổ hợp tiêu chuẩn (TTGH II) thật sự có trong dự án; rỗng nếu chưa khai báo.</summary>
    public static List<string> ServiceCombosStrict(AnalysisResults r) => r.CombosOfType(ComboType.SLS).ToList();
}
