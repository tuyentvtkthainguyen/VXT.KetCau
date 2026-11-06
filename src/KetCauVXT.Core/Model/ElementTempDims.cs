namespace KetCauVXT.Model;

/// <summary>Một kích thước tạm (kiểu Revit) giữa hai điểm mặt bằng (m). Target: "Trục" | "Tường"; Axis: "X" / "Y" (phương đo).</summary>
public sealed record ElemTempDim(double X1, double Y1, double X2, double Y2, double Value, string Target, string RefName, string Axis, int Side);

/// <summary>Kích thước tạm khi CHỌN tường / cột trên mặt bằng thiết kế:
/// <list type="bullet">
/// <item>Tường thẳng: khoảng cách vuông góc từ tim tường tới tường song song / trục song song gần nhất mỗi bên (đo tại giữa tường).</item>
/// <item>Cột: khoảng cách từ tâm cột tới trục X, Y gần nhất (độ lệch tâm) và từ mép cột tới trục đó.</item>
/// </list>
/// Sửa số → <see cref="MoveWall"/> (dời tường song song) / <see cref="SetColumnOffset"/> (đổi lệch tâm cột).</summary>
public static class ElementTempDims
{
    public static List<ElemTempDim> ForWall(Project p, WallLine w)
    {
        var res = new List<ElemTempDim>();
        if (p == null || w == null || w.IsCurved) return res;
        double L = w.Chord;
        if (L < 1e-6) return res;
        double ux = (w.X2 - w.X1) / L, uy = (w.Y2 - w.Y1) / L, nx = -uy, ny = ux;
        double mx = (w.X1 + w.X2) / 2, my = (w.Y1 + w.Y2) / 2;
        var cand = new List<(double S, string Target, string Name)>();
        foreach (var o in p.Walls.Where(o => o != w && o.Story == w.Story && !o.IsCurved && o.Chord > 1e-6))
        {
            double ox = (o.X2 - o.X1) / o.Chord, oy = (o.Y2 - o.Y1) / o.Chord;
            if (Math.Abs(ox * uy - oy * ux) > 0.02) continue;   // không song song
            // hình chiếu dọc phải chồng lấn giữa tường
            double t1 = (o.X1 - w.X1) * ux + (o.Y1 - w.Y1) * uy, t2 = (o.X2 - w.X1) * ux + (o.Y2 - w.Y1) * uy;
            if (Math.Max(t1, t2) < L / 2 - 1e-6 || Math.Min(t1, t2) > L / 2 + 1e-6) continue;
            double s = (o.X1 - mx) * nx + (o.Y1 - my) * ny;
            if (Math.Abs(s) > 1e-4) cand.Add((s, "Tường", "#" + o.Id));
        }
        if (Math.Abs(nx) > 0.999) foreach (var g in p.GridsX) { double s = (g.Coord - mx) * nx; if (Math.Abs(s) > 1e-4) cand.Add((s, "Trục", g.Name)); }
        if (Math.Abs(ny) > 0.999) foreach (var g in p.GridsY) { double s = (g.Coord - my) * ny; if (Math.Abs(s) > 1e-4) cand.Add((s, "Trục", g.Name)); }
        string axis = Math.Abs(nx) > Math.Abs(ny) ? "X" : "Y";
        foreach (int side in new[] { -1, 1 })
        {
            var c = cand.Where(q => Math.Sign(q.S) == side).OrderBy(q => Math.Abs(q.S)).ThenBy(q => q.Target == "Trục" ? 0 : 1).FirstOrDefault();
            if (c.Target == null) continue;
            res.Add(new ElemTempDim(mx, my, mx + nx * c.S, my + ny * c.S, Math.Abs(c.S), c.Target, c.Name, axis, side));
        }
        return res;
    }

    /// <summary>Dời tường song song để khoảng cách tới đối tượng tham chiếu của d bằng value (m). true nếu đổi.</summary>
    public static bool MoveWall(WallLine w, ElemTempDim d, double value)
    {
        if (w == null || d == null || value < 0) return false;
        double L = w.Chord;
        if (L < 1e-6) return false;
        double nx = -(w.Y2 - w.Y1) / L, ny = (w.X2 - w.X1) / L;
        // tham chiếu ở phía side: s_ref = side·|s|; muốn khoảng cách mới = value → dời tường một đoạn δ theo n: δ = side·(|s| − value)
        double delta = d.Side * (d.Value - value);
        if (Math.Abs(delta) < 1e-9) return false;
        w.X1 += nx * delta; w.Y1 += ny * delta; w.X2 += nx * delta; w.Y2 += ny * delta;
        return true;
    }

    /// <summary>Cột: (tâm → trục X gần nhất), (tâm → trục Y gần nhất); mỗi mục kèm khoảng mép gần trục.</summary>
    public static List<ElemTempDim> ForColumn(Project p, FrameElement col)
    {
        var res = new List<ElemTempDim>();
        if (p == null || col == null || !col.IsVerticalKind) return res;
        Node n;
        try { n = p.Node(col.NodeI); } catch { return res; }
        double cx = n.X + col.ColOffsetX, cy = n.Y + col.ColOffsetY;
        var gx = p.GridsX.OrderBy(g => Math.Abs(g.Coord - cx)).FirstOrDefault();
        var gy = p.GridsY.OrderBy(g => Math.Abs(g.Coord - cy)).FirstOrDefault();
        if (gx != null)
        {
            double s = gx.Coord - cx;
            res.Add(new ElemTempDim(cx, cy, gx.Coord, cy, Math.Abs(s), "Trục", gx.Name, "X", Math.Sign(s) == 0 ? 1 : Math.Sign(s)));
        }
        if (gy != null)
        {
            double s = gy.Coord - cy;
            res.Add(new ElemTempDim(cx, cy, cx, gy.Coord, Math.Abs(s), "Trục", gy.Name, "Y", Math.Sign(s) == 0 ? 1 : Math.Sign(s)));
        }
        return res;
    }

    /// <summary>Đặt khoảng cách tâm cột → trục của d bằng value (m) bằng cách đổi lệch tâm ColOffsetX/Y (giữ phía).</summary>
    public static bool SetColumnOffset(Project p, FrameElement col, ElemTempDim d, double value)
    {
        if (p == null || col == null || d == null || value < 0) return false;
        Node n;
        try { n = p.Node(col.NodeI); } catch { return false; }
        if (d.Axis == "X")
        {
            var g = p.GridsX.FirstOrDefault(q => q.Name == d.RefName); if (g == null) return false;
            col.ColOffsetX = g.Coord - d.Side * value - n.X;
        }
        else
        {
            var g = p.GridsY.FirstOrDefault(q => q.Name == d.RefName); if (g == null) return false;
            col.ColOffsetY = g.Coord - d.Side * value - n.Y;
        }
        if (Math.Abs(col.ColOffsetX) < 1e-9) col.ColOffsetX = 0;
        if (Math.Abs(col.ColOffsetY) < 1e-9) col.ColOffsetY = 0;
        return true;
    }
}
