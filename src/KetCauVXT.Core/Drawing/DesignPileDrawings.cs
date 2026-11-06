using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Detailing;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Chế độ THIẾT KẾ – bản vẽ cọc: mặt bằng định vị cọc (P1…Pn, bảng toạ độ, bảng thống kê cọc) và đường cong
/// sức chịu tải cọc theo chiều dài (khi dùng phương pháp TCVN 10304 cho cọc khoan nhồi: FoundationSettings.BoredMethod).
/// Dùng chung bộ vẽ với chế độ hồ sơ (Detailing/PileLocationPlan.DrawPlan, BoredPileDesign.CurveFigure).</summary>
public static class DesignPileDrawings
{
    /// <summary>Toạ độ cọc tuyệt đối của mọi đài cọc đã thiết kế (mỗi vị trí đài giống nhau một lần), đánh số P1… theo Y giảm rồi X tăng.</summary>
    public static List<PileCoordRow> Rows(Project p, DesignSummary s)
    {
        var rows = new List<PileCoordRow>();
        if (p == null || s?.PileCaps == null) return rows;
        var fs = p.Foundation ?? new FoundationSettings();
        var places = new List<(PileCapDesign D, double X, double Y)>();
        foreach (var d in s.PileCaps)
        {
            var locs = d.Locations != null && d.Locations.Count > 0 ? d.Locations : new List<(double X, double Y)> { (d.X, d.Y) };
            foreach (var (x, y) in locs) places.Add((d, x, y));
        }
        int k = 0;
        foreach (var (d, x0, y0) in places.OrderBy(q => -Math.Round(q.Y, 3)).ThenBy(q => Math.Round(q.X, 3)))
        {
            double depth = d.Depth > 0 ? d.Depth : Math.Max(p.Soil?.Depth ?? 1.5, 0);
            double len = fs.PileTipDepth > depth ? fs.PileTipDepth - depth : Math.Max(fs.PileLength, 0);
            var idx = Enumerable.Range(0, d.Piles.Count).OrderBy(i => -Math.Round(d.Piles[i].Y, 3)).ThenBy(i => Math.Round(d.Piles[i].X, 3)).ToList();
            foreach (int i in idx)
            {
                k++;
                rows.Add(new PileCoordRow
                {
                    No = "P" + k, Cap = string.IsNullOrEmpty(d.Name) ? "Đ?" : d.Name,
                    X = Math.Round(x0 + d.Piles[i].X, 3), Y = Math.Round(y0 + d.Piles[i].Y, 3),
                    D = d.PileSize, Length = len, HeadElev = -depth + 0.1, TipElev = -depth - len,
                    P = d.PMax, Pa = d.PileCapacity
                });
            }
        }
        return rows;
    }

    /// <summary>Mặt bằng định vị cọc chế độ thiết kế; null khi không có đài cọc.</summary>
    public static Figure LocationPlan(Project p, DesignSummary s)
    {
        var rows = Rows(p, s);
        if (rows.Count == 0) return null;
        var outlines = new List<(string Name, List<(double X, double Y)> Pts)>();
        foreach (var d in s.PileCaps)
        {
            if (d.L <= 0 || d.B <= 0) continue;
            var locs = d.Locations != null && d.Locations.Count > 0 ? d.Locations : new List<(double X, double Y)> { (d.X, d.Y) };
            foreach (var (x, y) in locs)
                outlines.Add((d.Name, new List<(double X, double Y)> { (x - d.L / 2, y - d.B / 2), (x + d.L / 2, y - d.B / 2), (x + d.L / 2, y + d.B / 2), (x - d.L / 2, y + d.B / 2) }));
        }
        var gx = p.GridsX.Select(g => (g.Name, g.Coord)).ToList();
        var gy = p.GridsY.Select(g => (g.Name, g.Coord)).ToList();
        return PileLocationPlan.DrawPlan(rows, outlines, gx, gy);
    }

    /// <summary>Đường cong sức chịu tải cọc khoan nhồi theo chiều dài (hố khoan đang chọn / hố đầu tiên); null khi không dùng TCVN 10304 hoặc không có hố khoan.</summary>
    public static Figure CapacityCurve(Project p)
    {
        var fs = p?.Foundation;
        if (fs == null || string.IsNullOrWhiteSpace(fs.BoredMethod) || p.Boreholes == null || p.Boreholes.Count == 0) return null;
        var bh = p.Boreholes.FirstOrDefault(b => b.Name == p.Soil?.Borehole) ?? p.Boreholes[0];
        var curve = Curve(p, bh, fs);
        if (curve.Count == 0) return null;
        return BoredPileDesign.CurveFigure(curve, $"ĐƯỜNG CONG SỨC CHỊU TẢI CỌC D{fs.PileSize * 1000:0} – HỐ KHOAN {bh.Name}");
    }

    /// <summary>Các điểm đường cong (L từ 6 m (D &lt; 0,8) / 10 m tới hết hố khoan, bước 1 m).</summary>
    public static List<CapacityCurvePoint> Curve(Project p, Borehole bh, FoundationSettings fs)
    {
        var layers = BoredPileDesign.FromBorehole(bh);
        double d = Math.Max(fs.PileSize, 0.1);
        double head = Math.Max(p?.Soil?.Depth ?? 1.5, 0);
        string method = fs.BoredMethod == BoredPile.MethodSpt ? BoredPile.MethodSpt : BoredPile.MethodTable;
        string drill = string.IsNullOrWhiteSpace(fs.BoredDrill) ? BoredPile.DrillSlurry : fs.BoredDrill;
        double pvl = 0;
        try
        {
            var r = BoredPileDesign.DesignCapacity(p, bh, fs);
            pvl = r.Pvl;
        }
        catch (Exception) { }
        double maxL = Math.Max(BoredPileDesign.TotalDepth(layers) - head - 0.5, 6);
        return BoredPileDesign.Curve(layers, d, head, d < 0.8 ? 6 : 10, maxL, 1.0, method, drill, pvl);
    }

    /// <summary>Các hình cho tờ KC-02.1 (mặt bằng định vị cọc + đường cong sức chịu tải).</summary>
    public static List<Figure> Figures(Project p, DesignSummary s)
    {
        var res = new List<Figure>();
        var lp = LocationPlan(p, s);
        if (lp != null) res.Add(lp);
        Figure cv = null;
        try { cv = CapacityCurve(p); } catch (Exception) { }
        if (cv != null && res.Count > 0) res.Add(cv);
        return res;
    }
}
