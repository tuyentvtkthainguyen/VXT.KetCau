using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

// =====================================================================
// MẶT ĐỨNG CÔNG TRÌNH (kiểu Revit "Elevation"): 4 hướng nhìn – trục 1→n (nhìn từ phía trục đầu Y, hướng +Y), n→1,
// A→n (nhìn từ phía trục đầu X, hướng +X), n→A. Hình chiếu khối (ModelSolids) lên mặt phẳng đứng: khối ở trước che khối
// phía sau (bỏ khối bị che hoàn toàn); cửa đi / cửa sổ vẽ trên tường của nó. Đường cao độ + ký hiệu, trục đứng có bong bóng
// trên & dưới, chuỗi kích thước trục (dưới), chiều cao tầng + tổng (phải). Thêm trục từ mặt đứng: AddGridAt(u).
// =====================================================================

public enum ElevationSide { Front, Back, Left, Right }

public sealed class ElevationItem
{
    public string Id { get; init; } = "";
    public string Category { get; init; } = "";
    public double U0 { get; init; }
    public double U1 { get; init; }
    public double Z0 { get; init; }
    public double Z1 { get; init; }
    /// <summary>Khoảng cách tới người nhìn (nhỏ = ở trước)</summary>
    public double Depth { get; init; }
}

public sealed class BuildingElevationView
{
    public ElevationSide Side { get; init; }
    public string Name { get; init; } = "";
    public SectionLine Line { get; init; }
    public List<ElevationItem> Items { get; init; } = new();
    public List<(double U, string Name)> Axes { get; init; } = new();
    public List<double> Levels { get; init; } = new();
    public Figure Figure { get; init; }
}

public static class BuildingElevation
{
    public static readonly ElevationSide[] Sides = { ElevationSide.Front, ElevationSide.Back, ElevationSide.Left, ElevationSide.Right };

    /// <summary>Phạm vi mô hình (m) theo lưới trục, nút và tường.</summary>
    public static (double X0, double Y0, double X1, double Y1) Extents(Project p)
    {
        var xs = p.Grids.Where(g => g.Dir == GridDir.X).Select(g => g.Coord).Concat(p.Nodes.Select(n => n.X)).Concat(p.Walls.SelectMany(w => new[] { w.X1, w.X2 })).ToList();
        var ys = p.Grids.Where(g => g.Dir == GridDir.Y).Select(g => g.Coord).Concat(p.Nodes.Select(n => n.Y)).Concat(p.Walls.SelectMany(w => new[] { w.Y1, w.Y2 })).ToList();
        if (xs.Count == 0) xs.Add(0);
        if (ys.Count == 0) ys.Add(0);
        return (xs.Min(), ys.Min(), xs.Max(), ys.Max());
    }

    /// <summary>Đường nhìn (như một mặt cắt đặt ngoài nhà 3 m): hướng pháp tuyến trái của đường = hướng nhìn.</summary>
    public static SectionLine ViewLine(Project p, ElevationSide side)
    {
        var (x0, y0, x1, y1) = Extents(p);
        const double off = 3, ext = 1.5;
        return side switch
        {
            ElevationSide.Front => new SectionLine { Name = "trục " + AxisRange(p, GridDir.X, false), X1 = x0 - ext, Y1 = y0 - off, X2 = x1 + ext, Y2 = y0 - off },
            ElevationSide.Back => new SectionLine { Name = "trục " + AxisRange(p, GridDir.X, true), X1 = x1 + ext, Y1 = y1 + off, X2 = x0 - ext, Y2 = y1 + off },
            ElevationSide.Left => new SectionLine { Name = "trục " + AxisRange(p, GridDir.Y, true), X1 = x0 - off, Y1 = y1 + ext, X2 = x0 - off, Y2 = y0 - ext },
            _ => new SectionLine { Name = "trục " + AxisRange(p, GridDir.Y, false), X1 = x1 + off, Y1 = y0 - ext, X2 = x1 + off, Y2 = y1 + ext },
        };
    }

    private static string AxisRange(Project p, GridDir d, bool reverse)
    {
        var g = (d == GridDir.X ? p.GridsX : p.GridsY).Select(q => q.Name).ToList();
        if (g.Count == 0) return d == GridDir.X ? "1-n" : "A-n";
        return reverse ? g[^1] + "-" + g[0] : g[0] + "-" + g[^1];
    }

    public static string Title(Project p, ElevationSide side) => "MẶT ĐỨNG " + ViewLine(p, side).Name.ToUpperInvariant();

    /// <summary>Trục thẳng đứng xuất hiện trên mặt đứng (u dọc đường nhìn, m).</summary>
    public static List<(double U, string Name)> Axes(Project p, SectionLine s)
    {
        double L = s.Length;
        double tx = (s.X2 - s.X1) / Math.Max(L, 1e-9), ty = (s.Y2 - s.Y1) / Math.Max(L, 1e-9);
        var axes = new List<(double U, string Name)>();
        foreach (var g in p.GridsX) if (Math.Abs(tx) > 0.5) axes.Add(((g.Coord - s.X1) / tx, g.Name));
        foreach (var g in p.GridsY) if (Math.Abs(ty) > 0.5) axes.Add(((g.Coord - s.Y1) / ty, g.Name));
        return axes.OrderBy(a => a.U).ToList();
    }

    /// <summary>Hình chiếu các khối (không tính lỗ khoét), khối bị che hoàn toàn bởi khối đặc ở trước bị bỏ; cửa luôn giữ.</summary>
    public static List<ElevationItem> ProjectSolids(Project p, SectionLine s)
    {
        double L = s.Length;
        var res = new List<ElevationItem>();
        if (L < 1e-9) return res;
        double tx = (s.X2 - s.X1) / L, ty = (s.Y2 - s.Y1) / L, nx = -ty, ny = tx;
        List<Solid> solids;
        try { solids = ModelSolids.Build(p, null, false); } catch (Exception) { return res; }
        var raw = new List<ElevationItem>();
        foreach (var so in solids)
        {
            if (so.IsVoid) continue;
            var (bot, top) = so.Rings();
            if (bot.Count < 3) continue;
            var all = bot.Concat(top).ToList();
            double u0 = all.Min(v => (v.X - s.X1) * tx + (v.Y - s.Y1) * ty), u1 = all.Max(v => (v.X - s.X1) * tx + (v.Y - s.Y1) * ty);
            double z0 = all.Min(v => v.Z), z1 = all.Max(v => v.Z);
            if (u1 - u0 < 1e-6 || z1 - z0 < 1e-6) continue;
            double d = all.Min(v => (v.X - s.X1) * nx + (v.Y - s.Y1) * ny);
            raw.Add(new ElevationItem { Id = so.Id, Category = so.Category, U0 = Math.Round(u0, 6), U1 = Math.Round(u1, 6), Z0 = Math.Round(z0, 6), Z1 = Math.Round(z1, 6), Depth = d });
        }
        static bool IsOpening(string c) => c is "Cửa đi" or "Cửa sổ" or "Lỗ cửa";
        var opaque = raw.Where(r => !IsOpening(r.Category) && r.Category is "Tường" or "Cột" or "Dầm" or "Sàn" or "Mái").ToList();
        foreach (var it in raw.OrderBy(r => r.Depth))
        {
            if (!IsOpening(it.Category) && opaque.Any(o => o != it && o.Depth + 0.05 < it.Depth && o.U0 <= it.U0 + 1e-6 && o.U1 >= it.U1 - 1e-6 && o.Z0 <= it.Z0 + 1e-6 && o.Z1 >= it.Z1 - 1e-6))
                continue;   // bị che hoàn toàn
            res.Add(it);
        }
        return res;
    }

    /// <summary>Dựng mặt đứng (DXF, mm).</summary>
    public static BuildingElevationView Draw(Project p, ElevationSide side, double scale = 100)
    {
        var s = ViewLine(p, side);
        var items = ProjectSolids(p, s);
        var w = new DxfWriter(scale);
        double L = s.Length;
        var lv = KetCauVXT.Model.Levels.All(p).Where(l => l.Kind != LevelKind.DayMong && l.Kind != LevelKind.MatMong).ToList();
        // khối: phía sau trước, nét mảnh; phía trước đè lên
        foreach (var it in items.OrderByDescending(i => i.Depth))
        {
            bool open = it.Category is "Cửa đi" or "Cửa sổ" or "Lỗ cửa";
            var pts = new List<(double X, double Y)> { (it.U0 * 1000, it.Z0 * 1000), (it.U1 * 1000, it.Z0 * 1000), (it.U1 * 1000, it.Z1 * 1000), (it.U0 * 1000, it.Z1 * 1000) };
            w.Polyline(open ? Lay.KyHieu : Lay.BeTong, pts, true);
            if (it.Category == "Cửa sổ")
            {
                double xm = (it.U0 + it.U1) / 2 * 1000;
                w.Line(Lay.KyHieu, xm, it.Z0 * 1000, xm, it.Z1 * 1000);   // song cửa
            }
            else if (it.Category == "Cửa đi")
            {
                // nét mở cánh (tam giác chỉ hướng bản lề) kiểu bản vẽ kiến trúc
                w.Line(Lay.KyHieu, it.U0 * 1000, it.Z1 * 1000, it.U1 * 1000, (it.Z0 + it.Z1) / 2 * 1000, "DASHED");
                w.Line(Lay.KyHieu, it.U1 * 1000, (it.Z0 + it.Z1) / 2 * 1000, it.U0 * 1000, it.Z0 * 1000, "DASHED");
            }
        }
        double uMin = 0, uMax = L;
        if (items.Count > 0) { uMin = Math.Min(uMin, items.Min(i => i.U0)); uMax = Math.Max(uMax, items.Max(i => i.U1)); }
        double zTop = Math.Max(lv.Count > 0 ? lv.Max(l => l.Elevation) : 0, items.Count > 0 ? items.Max(i => i.Z1) : 0);
        double zBot = Math.Min(lv.Count > 0 ? lv.Min(l => l.Elevation) : 0, 0) - 0.45;
        // mặt đất
        w.Line(Lay.BeTong, uMin * 1000 - 1000, zBot * 1000, uMax * 1000 + 1000, zBot * 1000);
        // cao độ
        double xl = uMin * 1000 - 1500, xr = uMax * 1000 + 1500;
        var levels = new List<double>();
        foreach (var l in lv)
        {
            double y = l.Elevation * 1000;
            levels.Add(l.Elevation);
            w.Line(Lay.Truc, xl, y, xr, y, "DASHED");
            w.ElevMark(xl, y, SectionView.ElevText(l.Elevation), -1);
            w.Text(Lay.Chu, xr + 200, y + 100, w.Th * 0.9, l.Title.ToUpperInvariant(), 0, 0, 0);
        }
        var ys = levels.Select(z => z * 1000).ToList();
        if (ys.Count >= 2)
        {
            double xd = xr + 2500;
            w.DimChainV(ys, xr, xd);
            w.DimChainV(new List<double> { ys[0], ys[^1] }, xr, xd + 1000);
        }
        // trục đứng – bong bóng trên và dưới
        var axes = Axes(p, s);
        double yBubB = zBot * 1000 - 2800 - w.BubbleR, yBubT = zTop * 1000 + 1500 + w.BubbleR;
        foreach (var (u, name) in axes)
        {
            w.Line(Lay.Truc, u * 1000, yBubB + w.BubbleR, u * 1000, yBubT - w.BubbleR, "CENTER");
            w.GridBubble(u * 1000, yBubB, name);
            w.GridBubble(u * 1000, yBubT, name);
        }
        if (axes.Count >= 2)
        {
            var xs = axes.Select(a => a.U * 1000).ToList();
            double yChain = zBot * 1000 - 1200, yTot = zBot * 1000 - 2000;
            w.DimChainH(xs, yChain, yChain);
            if (axes.Count > 2) w.DimChainH(new List<double> { xs[0], xs[^1] }, yTot, yTot);
        }
        string title = Title(p, side);
        w.Title((uMin + uMax) / 2 * 1000, yBubB - w.BubbleR - 600, title, $"tl: 1/{scale:0}", true);
        return new BuildingElevationView { Side = side, Name = title, Line = s, Items = items, Axes = axes, Levels = levels, Figure = new Figure(w, title) };
    }

    /// <summary>Toạ độ mặt bằng (m) của vị trí u trên mặt đứng và phương trục thẳng đứng tương ứng.</summary>
    public static (GridDir Dir, double Coord) PlanCoord(Project p, ElevationSide side, double u)
    {
        var s = ViewLine(p, side);
        double L = s.Length, tx = (s.X2 - s.X1) / L, ty = (s.Y2 - s.Y1) / L;
        return Math.Abs(tx) > 0.5 ? (GridDir.X, Math.Round(s.X1 + tx * u, 4)) : (GridDir.Y, Math.Round(s.Y1 + ty * u, 4));
    }

    /// <summary>Thêm trục (mặt bằng) từ cú bấm trên mặt đứng tại u (m).</summary>
    public static GridLine AddGridAt(Project p, ElevationSide side, double u, string name = null)
    {
        var (dir, c) = PlanCoord(p, side, u);
        return GridOps.AddGrid(p, dir, c, name);
    }

    /// <summary>Bản vẽ KT các mặt đứng.</summary>
    public static List<DrawingItem> Sheets(Project p, double scale = 100, string prefix = "KT")
    {
        var res = new List<DrawingItem>();
        if (p.Stories.Count == 0) return res;
        int k = 0;
        foreach (var side in Sides)
        {
            k++;
            var d = Draw(p, side, scale);
            if (d.Items.Count == 0) continue;
            res.Add(new DrawingItem { Key = prefix + ":MD:" + side, FileName = $"{prefix}_MatDung_{k}.dxf", Title = d.Name, Scale = scale, W = d.Figure.W });
        }
        return res;
    }
}
