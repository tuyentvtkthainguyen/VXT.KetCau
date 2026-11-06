namespace KetCauVXT.Model;

/// <summary>[Trục qua mép cột] Lệch tâm cột so với giao trục (nút). Khi bản vẽ kiến trúc đặt trục qua MÉP cột (thường ở trục biên,
/// cột sát ranh đất), tâm cột lệch khỏi trục ±b/2. Chương trình giữ nút phân tích tại giao trục (sơ đồ tính theo trục – sai khác
/// nhỏ, thiên về đơn giản), còn mặt bằng kết cấu, mặt bằng định vị cột và mặt bằng móng vẽ cột/móng đúng vị trí thật = trục + lệch tâm.
/// Lưu trên FrameElement.ColOffsetX / ColOffsetY (m).</summary>
public static class ColumnOffsets
{
    /// <summary>Lệch tâm (m) của cột thấp nhất nối vào nút nodeId (0, 0 nếu không có cột / không lệch).</summary>
    public static (double X, double Y) AtNode(Project p, int nodeId)
    {
        if (p == null) return (0, 0);
        FrameElement best = null; double zb = double.MaxValue;
        foreach (var f in p.Frames)
        {
            if (!f.IsVerticalKind || (f.NodeI != nodeId && f.NodeJ != nodeId)) continue;
            var a = p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var b = p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
            if (a == null || b == null) continue;
            double z = Math.Min(a.Z, b.Z);
            if (z < zb) { zb = z; best = f; }
        }
        return best == null ? (0, 0) : (best.ColOffsetX, best.ColOffsetY);
    }

    /// <summary>Gán lệch tâm cho mọi cột (mọi tầng) đứng tại (x, y) (m, sai số tol). Trả về số cột đã gán.</summary>
    public static int SetAt(Project p, double x, double y, double offX, double offY, double tol = 0.01)
    {
        int n = 0;
        foreach (var f in p.Frames.Where(f => f.IsVerticalKind))
        {
            var a = p.Nodes.FirstOrDefault(q => q.Id == f.NodeI);
            if (a == null || Math.Abs(a.X - x) > tol || Math.Abs(a.Y - y) > tol) continue;
            f.ColOffsetX = Math.Round(offX, 4); f.ColOffsetY = Math.Round(offY, 4); n++;
        }
        return n;
    }
}
