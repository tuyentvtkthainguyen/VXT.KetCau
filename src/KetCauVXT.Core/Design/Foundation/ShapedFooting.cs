using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

// =====================================================================
// [MóngHìnhDạng] HÌNH HỌC MÓNG ĐƠN: CỘT (tâm thật, góc xoay, 4 góc), GÓC MÓNG, DÓNG MÉP MÓNG THEO MÉP CỘT,
// CẮT THEO ĐƯỜNG CHUẨN (RANH ĐẤT XIÊN), MÓNG ĐA GIÁC. Thiết kế: Design/Concrete/FootingDesigner.Shaped.cs.
// =====================================================================

/// <summary>Hình học móng đơn có bố trí riêng (FootingPlacement) hoặc bị cắt bởi đường chuẩn (LotLine).</summary>
public static class ShapedFooting
{
    /// <summary>Kích thước lớn nhất khi tự tăng kích thước móng hình dạng (m)</summary>
    public const double MaxSize = 6.0;

    /// <summary>Cột tại chân: tâm thật (nút + lệch tâm cột), góc trục 2 (rad), kích thước theo trục 2 (H) và trục 3 (B).</summary>
    public readonly record struct ColumnGeo(double Cx, double Cy, double Angle, double D2, double D3)
    {
        public (double X, double Y)[] Corners()
        {
            double c = Math.Cos(Angle), s = Math.Sin(Angle);
            var res = new (double, double)[4];
            int k = 0;
            foreach (var (u, v) in new[] { (-D2 / 2, -D3 / 2), (D2 / 2, -D3 / 2), (D2 / 2, D3 / 2), (-D2 / 2, D3 / 2) })
                res[k++] = (Cx + u * c - v * s, Cy + u * s + v * c);
            return res;
        }

        /// <summary>Hình chữ nhật cột nở thêm d mỗi phía (theo hệ trục cột).</summary>
        public List<(double X, double Y)> Expanded(double d) => PolyGeom.Rect(Cx, Cy, D2 + 2 * d, D3 + 2 * d, Angle);
    }

    public static FootingPlacement PlacementOf(Project p, int node) => p?.Foundation?.Placements?.FirstOrDefault(x => x.NodeId == node);

    public static List<LotLine> Lines(Project p) =>
        p?.Foundation?.LotLines?.Where(l => l != null && l.Enabled && l.Length > 1e-6).ToList() ?? new List<LotLine>();

    public static ColumnGeo Column(Project p, FrameElement col, Node n)
    {
        var sec = col == null ? null : p.Section(col.Section);
        double a = 0;
        if (col != null)
        {
            var e2 = DesignUtil.Axis2(p, col);
            if (Math.Abs(e2.X) + Math.Abs(e2.Y) > 1e-9) a = Math.Atan2(e2.Y, e2.X);
        }
        var (ox, oy) = ColumnOffsets.AtNode(p, n.Id);
        double d2 = sec != null && sec.H > 0 ? sec.H : 0.22, d3 = sec != null && sec.B > 0 ? sec.B : 0.22;
        return new ColumnGeo(n.X + ox, n.Y + oy, a, d2, d3);
    }

    /// <summary>Góc về khoảng (−45°, 45°] (rad) – phương L của móng gần trục X nhất.</summary>
    public static double Norm45(double a)
    {
        double q = Math.PI / 2;
        while (a > q / 2 + 1e-12) a -= q;
        while (a <= -q / 2 + 1e-12) a += q;
        return a;
    }

    /// <summary>Góc phương L của móng (rad).</summary>
    public static double FootingAngle(Project p, FootingPlacement pl, ColumnGeo cg)
    {
        if (pl == null) return 0;
        switch (pl.AngleMode)
        {
            case FootingAngleMode.Column: return Norm45(cg.Angle);
            case FootingAngleMode.Custom: return pl.Angle * Math.PI / 180;
            case FootingAngleMode.LotLine:
                var near = Lines(p).OrderBy(l => l.DistanceToSegment(cg.Cx, cg.Cy)).FirstOrDefault();
                return near == null ? 0 : Norm45(near.AngleDeg * Math.PI / 180);
            default: return 0;
        }
    }

    /// <summary>Phạm vi cột trong hệ địa phương của móng (góc a), gốc tại tâm cột.</summary>
    public static (double X0, double X1, double Y0, double Y1) ColumnExtents(ColumnGeo cg, double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
        foreach (var (x, y) in cg.Corners())
        {
            double u = (x - cg.Cx) * c + (y - cg.Cy) * s, v = -(x - cg.Cx) * s + (y - cg.Cy) * c;
            x0 = Math.Min(x0, u); x1 = Math.Max(x1, u); y0 = Math.Min(y0, v); y1 = Math.Max(y1, v);
        }
        return (x0, x1, y0, y1);
    }

    /// <summary>Tâm móng chữ nhật L × B (toạ độ tuyệt đối) theo cách dóng.</summary>
    public static (double X, double Y) RectCenter(ColumnGeo cg, FootingPlacement pl, double L, double B, double a)
    {
        var (x0, x1, y0, y1) = ColumnExtents(cg, a);
        double u = Shift(pl?.AlignX ?? FootingAlign.Center, pl?.OffsetX ?? 0, x0, x1, L);
        double v = Shift(pl?.AlignY ?? FootingAlign.Center, pl?.OffsetY ?? 0, y0, y1, B);
        double c = Math.Cos(a), s = Math.Sin(a);
        return (cg.Cx + u * c - v * s, cg.Cy + u * s + v * c);
    }

    private static double Shift(FootingAlign al, double off, double c0, double c1, double size) => al switch
    {
        FootingAlign.MinFlush => c0 + size / 2,
        FootingAlign.MaxFlush => c1 - size / 2,
        FootingAlign.Offset => off,
        _ => (c0 + c1) / 2
    };

    /// <summary>Đường bao đáy móng (tuyệt đối, ngược chiều kim đồng hồ) cho kích thước L × B (bỏ qua khi móng đa giác).
    /// clipped = bị cắt bởi đường chuẩn.</summary>
    public static List<(double X, double Y)> Outline(Project p, ColumnGeo cg, FootingPlacement pl, double L, double B, out double angle, out bool clipped)
    {
        angle = FootingAngle(p, pl, cg);
        List<(double X, double Y)> poly;
        if (pl != null && pl.IsPolygon)
        {
            poly = PolyGeom.Ccw(pl.Polygon.Select(v => (cg.Cx + v[0], cg.Cy + v[1])));
            if (pl.AngleMode == FootingAngleMode.Global) angle = PolygonAngle(poly);
        }
        else
        {
            var (cx, cy) = RectCenter(cg, pl, L, B, angle);
            poly = PolyGeom.Rect(cx, cy, L, B, angle);
        }
        clipped = false;
        if (pl == null || pl.ClipToLot)
        {
            double a0 = PolyGeom.Properties(poly).Area;
            foreach (var l in Lines(p))
            {
                // chỉ đường chuẩn đi qua phạm vi móng mới cắt (đường ở xa: bỏ qua)
                poly = PolyGeom.ClipByLine(poly, l, cg.Cx, cg.Cy);
                if (poly.Count < 3) break;
            }
            clipped = poly.Count < 3 || PolyGeom.Properties(poly).Area < a0 - 1e-6;
        }
        return poly.Count >= 3 ? PolyGeom.Ccw(poly) : poly;
    }

    /// <summary>Góc cạnh dài nhất của đa giác (rad, chuẩn hoá ±45°) – phương L của móng đa giác.</summary>
    public static double PolygonAngle(IList<(double X, double Y)> poly)
    {
        double best = -1, a = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var u = poly[i]; var v = poly[(i + 1) % poly.Count];
            double len = Math.Sqrt((v.X - u.X) * (v.X - u.X) + (v.Y - u.Y) * (v.Y - u.Y));
            if (len > best + 1e-9) { best = len; a = Math.Atan2(v.Y - u.Y, v.X - u.X); }
        }
        return Norm45(a);
    }

    /// <summary>Có cần thiết kế theo hình dạng không: có bố trí riêng khác mặc định, hoặc đường chuẩn cắt móng chữ nhật đặt tâm.</summary>
    public static bool Needed(Project p, ColumnGeo cg, FootingPlacement pl, double L, double B)
    {
        if (pl != null && (pl.IsPolygon || pl.AlignX != FootingAlign.Center || pl.AlignY != FootingAlign.Center
                           || pl.AngleMode == FootingAngleMode.Column && Math.Abs(Norm45(cg.Angle)) > 1e-6
                           || pl.AngleMode == FootingAngleMode.Custom && Math.Abs(pl.Angle) > 1e-6
                           || pl.AngleMode == FootingAngleMode.LotLine)) return true;
        if (pl != null && !pl.ClipToLot) return false;
        if (Lines(p).Count == 0) return false;
        Outline(p, cg, pl, L, B, out _, out bool clipped);
        return clipped;
    }

    /// <summary>Đa giác chuyển sang hệ địa phương của móng (gốc tại tâm cột, x' theo góc a).</summary>
    public static List<(double X, double Y)> ToLocal(IEnumerable<(double X, double Y)> poly, double ox, double oy, double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        return poly.Select(q => ((q.X - ox) * c + (q.Y - oy) * s, -(q.X - ox) * s + (q.Y - oy) * c)).ToList();
    }

    public static (double X, double Y) ToWorld(double u, double v, double ox, double oy, double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        return (ox + u * c - v * s, oy + u * s + v * c);
    }

    /// <summary>Các điểm bắt dính để vẽ đường chuẩn / móng đa giác: góc cột chân, giao trục, đầu mút và giao điểm đường chuẩn, đỉnh móng.</summary>
    public static List<(double X, double Y, string Kind)> SnapPoints(Project p, DesignSummary d = null)
    {
        var res = new List<(double, double, string)>();
        foreach (var (n, f) in FoundationUtil.BaseColumns(p))
        {
            var cg = Column(p, f, n);
            foreach (var (x, y) in cg.Corners()) res.Add((x, y, "góc cột"));
            res.Add((cg.Cx, cg.Cy, "tim cột"));
        }
        var gx = p.Grids.Where(g => g.Dir == GridDir.X).ToList(); var gy = p.Grids.Where(g => g.Dir == GridDir.Y).ToList();
        foreach (var a in gx) foreach (var b in gy) res.Add((a.Coord, b.Coord, "giao trục"));
        var lines = Lines(p);
        foreach (var l in lines) { res.Add((l.X1, l.Y1, "đầu đường chuẩn")); res.Add((l.X2, l.Y2, "đầu đường chuẩn")); }
        for (int i = 0; i < lines.Count; i++)
            for (int j = i + 1; j < lines.Count; j++)
            {
                var a = lines[i]; var b = lines[j];
                var q = PolyGeom.Intersect(a.X1, a.Y1, a.X2, a.Y2, b.X1, b.Y1, b.X2, b.Y2);
                if (q != null) res.Add((q.Value.X, q.Value.Y, "góc đất"));
            }
        if (d != null)
            foreach (var f in d.Footings.Where(f => f.Shaped))
                foreach (var v in f.Outline) res.Add((v[0], v[1], "đỉnh móng"));
        return res;
    }
}
