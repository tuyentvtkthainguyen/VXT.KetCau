using KetCauVXT.Model;

namespace KetCauVXT.Mep;

// =====================================================================
// ĐHKK – ĐỢT 3: sửa từng đối tượng, hạ ống gió qua dầm, chọn máy điều hoà theo phòng, kiểu IFC.
// • HvacEdit: bắt đối tượng trên mặt bằng (ống gió / miệng gió / dàn lạnh – nóng / ống đồng – nước ngưng), quét chọn (cửa sổ / cắt),
//   di chuyển, xoá (dàn lạnh xoá kèm ống nối), đổi cỡ (giữ cao độ đỉnh ống – ống treo sát trần), đổi lưu lượng → tự chọn lại cỡ,
//   vẽ lại tuyến, kéo đỉnh tuyến, đổi công suất máy → cập nhật cặp ống đồng; dời dàn lạnh / nóng → ống đồng, ống ngưng đi lại.
// • HvacBeamAvoid: ống gió sát trần va dầm → hạ ống dưới đáy dầm bằng 2 cút 45° mỗi phía (đoạn xiên 45°: chạy ngang = độ hạ),
//   chỉ khi trần còn đủ chỗ (đáy ống sau khi hạ ≥ sàn + max(chiều cao thông thủy tối thiểu, cao độ trần phòng)); không đủ → cảnh báo
//   ONG_GIO_KHONG_QUA_DAM. Đoạn ống quá ngắn để xiên 45° → hạ cả tuyến (nếu trần cho phép).
// • HvacLoad: tải lạnh phòng theo phương pháp diện tích (thực hành dân dụng VN):
//       Q (BTU/h) = 600 · A · max(1; h / 3) · (1 + 0,15 nếu phòng có tường ngoài) + 500 · max(0; N − 2)
//   (600 BTU/h·m² ≈ 176 W/m² cho phòng cao ≤ 3 m; tường ngoài nhận bức xạ +15 %; mỗi người thêm ngoài 2 người ≈ 500 BTU/h);
//   chọn máy 9000 / 12000 / 18000 / 24000 BTU nhỏ nhất ≥ Q (Q > 24000 → nhiều máy); dàn lạnh đặt tường tốt nhất
//   (không có cửa đi, xa cửa đi nhất ≈ đối diện cửa, ưu tiên tường ngoài), dàn nóng trên tường ngoài; ống đồng theo công suất;
//   điện máy lạnh: P = Q·0,000293 / COP (COP = 3), dòng thiết kế 1,25·I (động cơ máy nén) → aptomat, dây ≥ 2,5 mm² (MepSizing).
// • HvacIfc: kiểu IFC2X3 riêng cho ĐHKK (IfcDuctSegmentType, IfcDuctFittingType, IfcAirTerminalType, IfcUnitaryEquipmentType,
//   IfcPipeSegmentType) gắn bằng IfcRelDefinesByType.
// =====================================================================

/// <summary>Loại đối tượng ĐHKK chọn được trên mặt bằng.</summary>
public enum HvacElemKind { Duct, Terminal, Unit, Pipe }

/// <summary>Kết quả bắt đối tượng: loại, Id, đỉnh tuyến gần nhất (-1 = không), đoạn tuyến (chỉ số điểm cuối, 0 = không), khoảng cách (m).</summary>
public sealed record HvacPick(HvacElemKind Kind, int Id, int Vertex, int Segment, double Dist);

public static class HvacEdit
{
    private static HvacModel H(Project p) { p.Mep ??= new MepModel(); p.Mep.Hvac ??= new HvacModel(); return p.Mep.Hvac; }

    private static int Rank(HvacElemKind k) => k switch { HvacElemKind.Unit => 0, HvacElemKind.Terminal => 1, HvacElemKind.Pipe => 2, _ => 3 };

    /// <summary>Mọi đối tượng ĐHKK của tầng nằm trong dung sai tol (m) quanh (x, y), gần nhất trước (cùng khoảng cách: thiết bị → miệng gió → ống → ống gió).</summary>
    public static List<HvacPick> HitAll(Project p, string story, double x, double y, double tol = 0.15)
    {
        var res = new List<HvacPick>();
        var h = p?.Mep?.Hvac;
        if (h == null) return res;
        foreach (var u in h.Units.Where(u => u.Story == story))
        {
            double a = -u.Angle * Math.PI / 180, dx = x - u.X, dy = y - u.Y;
            double lx = dx * Math.Cos(a) - dy * Math.Sin(a), ly = dx * Math.Sin(a) + dy * Math.Cos(a);
            double ex = Math.Max(0, Math.Abs(lx) - u.W / 2), ey = Math.Max(0, Math.Abs(ly) - u.D / 2);
            double d = Math.Sqrt(ex * ex + ey * ey);
            if (d <= tol) res.Add(new HvacPick(HvacElemKind.Unit, u.Id, -1, 0, d));
        }
        foreach (var t in h.Terminals.Where(t => t.Story == story))
        {
            double ex = Math.Max(0, Math.Abs(x - t.X) - t.W / 2000), ey = Math.Max(0, Math.Abs(y - t.Y) - t.H / 2000);
            double d = Math.Sqrt(ex * ex + ey * ey);
            if (d <= tol) res.Add(new HvacPick(HvacElemKind.Terminal, t.Id, -1, 0, d));
        }
        foreach (var q in h.Pipes.Where(q => q.Story == story))
        {
            var pk = OnPath(q.Path, x, y, 0, tol);
            if (pk.Seg > 0) res.Add(new HvacPick(HvacElemKind.Pipe, q.Id, pk.Vertex, pk.Seg, pk.Dist));
        }
        foreach (var d in h.Ducts.Where(d => d.Story == story))
        {
            var pk = OnPath(d.Path, x, y, d.Width / 2000, tol);
            if (pk.Seg > 0) res.Add(new HvacPick(HvacElemKind.Duct, d.Id, pk.Vertex, pk.Seg, pk.Dist));
        }
        return res.OrderBy(r => Math.Round(r.Dist, 6)).ThenBy(r => Rank(r.Kind)).ToList();
    }

    /// <summary>Đối tượng gần nhất (null = không trúng).</summary>
    public static HvacPick Hit(Project p, string story, double x, double y, double tol = 0.15) => HitAll(p, story, x, y, tol).FirstOrDefault();

    private static (int Seg, int Vertex, double Dist) OnPath(List<MepPt> path, double x, double y, double halfWidth, double tol)
    {
        int seg = 0; double best = double.MaxValue;
        for (int i = 1; i < path.Count; i++)
        {
            var pr = MepGeom.Project(x, y, path[i - 1].X, path[i - 1].Y, path[i].X, path[i].Y);
            double d = Math.Max(0, pr.Dist - halfWidth);
            if (d <= tol && d < best) { best = d; seg = i; }
        }
        if (seg == 0) return (0, -1, 0);
        int vtx = -1; double vb = Math.Max(tol, halfWidth);
        for (int i = 0; i < path.Count; i++)
        {
            double dv = MepGeom.Dist2D(x, y, path[i].X, path[i].Y);
            if (dv <= vb) { vb = dv; vtx = i; }
        }
        return (seg, vtx, best);
    }

    /// <summary>Các điểm đặc trưng trên mặt bằng của đối tượng (tâm thiết bị / các đỉnh tuyến).</summary>
    public static List<(double X, double Y)> Points(Project p, HvacElemKind k, int id)
    {
        var h = p?.Mep?.Hvac;
        if (h == null) return new();
        return k switch
        {
            HvacElemKind.Unit => h.Units.Where(u => u.Id == id).Select(u => (u.X, u.Y)).ToList(),
            HvacElemKind.Terminal => h.Terminals.Where(t => t.Id == id).Select(t => (t.X, t.Y)).ToList(),
            HvacElemKind.Duct => h.Duct(id)?.Path.Select(q => (q.X, q.Y)).ToList() ?? new(),
            _ => h.Pipes.FirstOrDefault(q => q.Id == id)?.Path.Select(q => (q.X, q.Y)).ToList() ?? new()
        };
    }

    /// <summary>Quét chọn trên mặt bằng: crossing = false → đối tượng nằm trọn trong khung (quét trái → phải);
    /// crossing = true → chạm khung là chọn (quét phải → trái).</summary>
    public static List<(HvacElemKind Kind, int Id)> Window(Project p, string story, double x1, double y1, double x2, double y2, bool crossing)
    {
        var res = new List<(HvacElemKind, int)>();
        var h = p?.Mep?.Hvac;
        if (h == null) return res;
        double rx1 = Math.Min(x1, x2), rx2 = Math.Max(x1, x2), ry1 = Math.Min(y1, y2), ry2 = Math.Max(y1, y2);
        bool In(double x, double y) => x >= rx1 - 1e-9 && x <= rx2 + 1e-9 && y >= ry1 - 1e-9 && y <= ry2 + 1e-9;
        bool PathSel(List<MepPt> pts)
        {
            if (pts.Count == 0) return false;
            if (!crossing) return pts.All(q => In(q.X, q.Y));
            if (pts.Any(q => In(q.X, q.Y))) return true;
            for (int i = 1; i < pts.Count; i++)
                if (MepGeom.SegmentHitsRect(pts[i - 1].X, pts[i - 1].Y, pts[i].X, pts[i].Y, rx1, ry1, rx2, ry2)) return true;
            return false;
        }
        foreach (var u in h.Units.Where(u => u.Story == story))
        {
            double r = Math.Max(u.W, u.D) / 2;
            bool sel = crossing ? u.X + r >= rx1 && u.X - r <= rx2 && u.Y + r >= ry1 && u.Y - r <= ry2 : In(u.X - u.W / 2, u.Y - u.D / 2) && In(u.X + u.W / 2, u.Y + u.D / 2);
            if (sel) res.Add((HvacElemKind.Unit, u.Id));
        }
        foreach (var t in h.Terminals.Where(t => t.Story == story))
        {
            double hw = t.W / 2000, hh = t.H / 2000;
            bool sel = crossing ? t.X + hw >= rx1 && t.X - hw <= rx2 && t.Y + hh >= ry1 && t.Y - hh <= ry2 : In(t.X - hw, t.Y - hh) && In(t.X + hw, t.Y + hh);
            if (sel) res.Add((HvacElemKind.Terminal, t.Id));
        }
        foreach (var d in h.Ducts.Where(d => d.Story == story)) if (PathSel(d.Path)) res.Add((HvacElemKind.Duct, d.Id));
        foreach (var q in h.Pipes.Where(q => q.Story == story)) if (PathSel(q.Path)) res.Add((HvacElemKind.Pipe, q.Id));
        return res;
    }

    private static List<MepPt> Shift(List<MepPt> pts, double dx, double dy) =>
        pts.Select(q => new MepPt(q.X + dx, q.Y + dy, q.Z, q.WallId)).ToList();

    /// <summary>Di chuyển đối tượng (dx, dy) trên mặt bằng; dời dàn lạnh / nóng thì ống đồng, ống ngưng nối với nó đi lại tuyến.</summary>
    public static bool Move(Project p, HvacElemKind k, int id, double dx, double dy)
    {
        var h = H(p);
        switch (k)
        {
            case HvacElemKind.Unit:
                var u = h.Unit(id); if (u == null) return false;
                u.X += dx; u.Y += dy; Reconnect(p, u); return true;
            case HvacElemKind.Terminal:
                var t = h.Terminals.FirstOrDefault(q => q.Id == id); if (t == null) return false;
                t.X += dx; t.Y += dy; return true;
            case HvacElemKind.Duct:
                var d = h.Duct(id); if (d == null) return false;
                d.Path = Shift(d.Path, dx, dy); HvacRouting.RebuildFittings(p); return true;
            default:
                var pp = h.Pipes.FirstOrDefault(q => q.Id == id); if (pp == null) return false;
                pp.Path = Shift(pp.Path, dx, dy); return true;
        }
    }

    /// <summary>Di chuyển nhiều đối tượng cùng lúc.</summary>
    public static int MoveMany(Project p, IEnumerable<(HvacElemKind Kind, int Id)> items, double dx, double dy)
    {
        var list = items.Distinct().ToList();
        var units = list.Where(i => i.Kind == HvacElemKind.Unit).Select(i => i.Id).ToHashSet();
        int n = 0;
        foreach (var it in list)
        {
            // ống nối với dàn đang dời sẽ được đi lại tuyến – không dời thêm lần nữa
            if (it.Kind == HvacElemKind.Pipe)
            {
                var q = H(p).Pipes.FirstOrDefault(x => x.Id == it.Id);
                if (q != null && (units.Contains(q.FromUnit) || units.Contains(q.ToUnit))) continue;
            }
            if (Move(p, it.Kind, it.Id, dx, dy)) n++;
        }
        return n;
    }

    /// <summary>Đi lại tuyến ống đồng / ống nước ngưng nối với dàn u (giữ điểm xả nước ngưng).</summary>
    public static void Reconnect(Project p, HvacUnit u)
    {
        var h = H(p);
        foreach (var q in h.Pipes.Where(q => q.FromUnit == u.Id || q.ToUnit == u.Id).ToList())
        {
            if (q.Kind == HvacPipeKind.OngDong)
            {
                var a = h.Unit(q.FromUnit); var b = h.Unit(q.ToUnit);
                if (a == null || b == null) continue;
                double z1 = HvacRouting.UnitZ(p, a), z2 = HvacRouting.UnitZ(p, b);
                var path = HvacRouting.Orthogonal(a.X, a.Y, b.X, b.Y, z1);
                if (Math.Abs(z2 - z1) > 1e-6) path.Add(new MepPt { X = b.X, Y = b.Y, Z = z2 });
                q.Path = path;
                (q.LiquidDia, q.GasDia) = HvacSizing.CopperPair(a.CapacityBtu);
            }
            else if (q.FromUnit == u.Id && q.Path.Count >= 2)
            {
                var end = q.Path[^1];
                q.Path = HvacRouting.SlopedPath(u.X, u.Y, HvacRouting.UnitZ(p, u) - u.Hh / 2, end.X, end.Y, Math.Max(0.01, q.Slope));
            }
        }
    }

    /// <summary>Xoá một đối tượng (dàn lạnh / nóng xoá kèm ống đồng, ống ngưng nối với nó).</summary>
    public static bool Delete(Project p, HvacElemKind k, int id)
    {
        var h = H(p);
        int n = k switch
        {
            HvacElemKind.Unit => h.Units.RemoveAll(u => u.Id == id),
            HvacElemKind.Terminal => h.Terminals.RemoveAll(t => t.Id == id),
            HvacElemKind.Duct => h.Ducts.RemoveAll(d => d.Id == id),
            _ => h.Pipes.RemoveAll(q => q.Id == id)
        };
        if (k == HvacElemKind.Unit && n > 0) h.Pipes.RemoveAll(q => q.FromUnit == id || q.ToUnit == id);
        if (k == HvacElemKind.Duct && n > 0) HvacRouting.RebuildFittings(p);
        return n > 0;
    }

    public static int DeleteMany(Project p, IEnumerable<(HvacElemKind Kind, int Id)> items) => items.Distinct().ToList().Count(it => Delete(p, it.Kind, it.Id));

    /// <summary>Đổi tiết diện ống gió (mm); giữ cao độ ĐỈNH ống (ống treo sát trần) rồi sinh lại phụ kiện.</summary>
    public static void Resize(Project p, HvacDuct d, double w, double hgt, bool round = false, double dia = 0)
    {
        double old = d.Depth;
        d.Round = round;
        if (round) d.Dia = Math.Max(50, dia > 0 ? dia : w);
        else { d.W = Math.Max(50, w); d.H = Math.Max(50, hgt); }
        KeepTop(d, old);
        HvacRouting.RebuildFittings(p);
    }

    /// <summary>Đổi lưu lượng → tự chọn lại cỡ theo vận tốc (ống chính / nhánh), giữ cao độ đỉnh ống.</summary>
    public static void SetFlow(Project p, HvacDuct d, double flow, bool branch = false)
    {
        double old = d.Depth;
        d.Flow = Math.Max(0, flow);
        HvacRouting.Size(p, d, branch);
        KeepTop(d, old);
        HvacRouting.RebuildFittings(p);
    }

    private static void KeepTop(HvacDuct d, double oldDepthMm)
    {
        double dz = (oldDepthMm - d.Depth) / 2000.0;
        if (Math.Abs(dz) < 1e-12) return;
        d.Path = d.Path.Select(q => new MepPt(q.X, q.Y, q.Z + dz, q.WallId)).ToList();
    }

    /// <summary>Đặt cao độ đỉnh ống (tuyệt đối, m) cho cả tuyến (đoạn hạ qua dầm bị san phẳng).</summary>
    public static void SetTop(Project p, HvacDuct d, double topAbs)
    {
        double z = topAbs - d.Depth / 2000.0;
        d.Path = d.Path.Select(q => new MepPt(q.X, q.Y, z, q.WallId)).ToList();
        HvacRouting.RebuildFittings(p);
    }

    /// <summary>Vẽ lại tuyến trực giao (x1,y1) → (x2,y2) ở cao độ hiện tại của ống.</summary>
    public static void Reroute(Project p, HvacDuct d, double x1, double y1, double x2, double y2)
    {
        double z = d.Path.Count > 0 ? d.Path.Max(q => q.Z) : HvacRouting.DuctZ(p, d.Story, d.Depth);
        d.Path = HvacRouting.Orthogonal(x1, y1, x2, y2, z);
        HvacRouting.RebuildFittings(p);
    }

    /// <summary>Kéo một đỉnh tuyến ống gió tới (x, y) (giữ cao độ đỉnh đó).</summary>
    public static bool MoveVertex(Project p, HvacDuct d, int i, double x, double y)
    {
        if (d == null || i < 0 || i >= d.Path.Count) return false;
        var q = d.Path[i];
        d.Path[i] = new MepPt(x, y, q.Z, q.WallId);
        HvacRouting.RebuildFittings(p);
        return true;
    }

    /// <summary>Đối tượng ĐHKK từ Id khối 3D: "DN-GIO-{ống}-{đoạn}", "DN-DH-{máy}", "DN-MG-{miệng gió}", "DN-DHO-{ống}-{đoạn}"; null = không phải.</summary>
    public static (HvacElemKind Kind, int Id)? FromSolidId(string sid)
    {
        if (string.IsNullOrEmpty(sid)) return null;
        static int Num(string s) { int k = s.IndexOf('-'); return int.TryParse(k < 0 ? s : s.Substring(0, k), out int v) ? v : 0; }
        (HvacElemKind, int)? R(HvacElemKind k, string rest) { int id = Num(rest); return id > 0 ? (k, id) : null; }
        if (sid.StartsWith("DN-GIO-", StringComparison.Ordinal)) return R(HvacElemKind.Duct, sid.Substring(7));
        if (sid.StartsWith("DN-DHO-", StringComparison.Ordinal)) return R(HvacElemKind.Pipe, sid.Substring(7));
        if (sid.StartsWith("DN-DH-", StringComparison.Ordinal)) return R(HvacElemKind.Unit, sid.Substring(6));
        if (sid.StartsWith("DN-MG-", StringComparison.Ordinal)) return R(HvacElemKind.Terminal, sid.Substring(6));
        return null;
    }

    /// <summary>Điểm neo khi kéo trong 3D (tâm thiết bị / miệng gió, đỉnh đầu tuyến ống), cao độ tuyệt đối.</summary>
    public static (double X, double Y, double Z)? Anchor(Project p, HvacElemKind k, int id)
    {
        var h = p?.Mep?.Hvac;
        if (h == null) return null;
        switch (k)
        {
            case HvacElemKind.Unit: { var u = h.Unit(id); return u == null ? null : (u.X, u.Y, HvacRouting.UnitZ(p, u)); }
            case HvacElemKind.Terminal:
            {
                var t = h.Terminals.FirstOrDefault(q => q.Id == id);
                return t == null ? null : (t.X, t.Y, MepRouting.StoryElevation(p, t.Story) + t.Height);
            }
            case HvacElemKind.Duct: { var d = h.Duct(id); return d == null || d.Path.Count == 0 ? null : (d.Path[0].X, d.Path[0].Y, d.Path[0].Z); }
            default: { var q = h.Pipes.FirstOrDefault(x => x.Id == id); return q == null || q.Path.Count == 0 ? null : (q.Path[0].X, q.Path[0].Y, q.Path[0].Z); }
        }
    }

    /// <summary>Đổi công suất máy (BTU/h): đổi tên mặc định, kích thước dàn, cặp ống đồng nối với máy.</summary>
    public static void SetCapacity(Project p, HvacUnit u, double btu)
    {
        u.CapacityBtu = Math.Max(1000, btu);
        if (u.Kind == HvacUnitKind.DanLanh)
        {
            if (u.Name.StartsWith("Dàn lạnh treo tường")) u.Name = $"Dàn lạnh treo tường {u.CapacityBtu:0} BTU";
            u.W = HvacLoad.IndoorWidth(u.CapacityBtu);
        }
        else
        {
            if (u.Name.StartsWith("Dàn nóng")) u.Name = $"Dàn nóng {u.CapacityBtu:0} BTU";
            u.W = u.CapacityBtu <= 12000 + 1e-6 ? 0.7 : u.CapacityBtu <= 18000 + 1e-6 ? 0.8 : 0.9;
        }
        Reconnect(p, u);
    }
}

/// <summary>Kết quả hạ ống gió qua dầm.</summary>
public sealed record HvacDropResult(int DuctId, int Drops, bool WholeDuctLowered, List<MepWarning> Warnings);

/// <summary>Ống gió đi qua dầm: hạ ống dưới đáy dầm bằng cặp cút 45° (tự động khi vẽ, hoặc lệnh "Hạ ống qua dầm").</summary>
public static class HvacBeamAvoid
{
    public const string Code = "ONG_GIO_KHONG_QUA_DAM";

    private sealed record Cross(double S, double HalfAlong, double BeamBottom, int FrameId, double X, double Y);

    private static (double B, double H) Dims(Project p, FrameElement f)
    {
        var sec = p.FrameSections.FirstOrDefault(s => s.Name == f.Section);
        return (sec?.B > 0 ? sec.B : 0.22, sec?.H > 0 ? sec.H : 0.3);
    }

    /// <summary>Cao độ thấp nhất cho phép của ĐÁY ống gió tại (x, y): sàn tầng + max(chiều cao thông thủy tối thiểu, cao độ trần phòng).</summary>
    public static double MinBottom(Project p, string story, double x, double y)
    {
        var set = p.Mep?.Hvac?.Settings ?? new HvacSettings();
        double floor = MepRouting.StoryElevation(p, story), lim = set.MinClearHeight;
        foreach (var r in p.Arch?.Rooms ?? new List<Room>())
            if (r.Story == story && r.CeilingHeight > 0 && r.Outline.Count >= 3 && Geo2.Contains(r.Outline, x, y)) lim = Math.Max(lim, r.CeilingHeight);
        return floor + lim;
    }

    private static List<Cross> Crossings(Project p, HvacDuct d, MepPt a, MepPt c, List<FrameElement> beams)
    {
        var res = new List<Cross>();
        double L = MepGeom.Dist2D(a.X, a.Y, c.X, c.Y);
        if (L < 1e-6) return res;
        double half = d.Depth / 2000.0, z = (a.Z + c.Z) / 2, top = z + half, bot = z - half;
        double ux = (c.X - a.X) / L, uy = (c.Y - a.Y) / L;
        double clr = Math.Max(0, p.Mep.Hvac.Settings.BeamClearance);
        foreach (var f in beams)
        {
            Node ni, nj;
            try { ni = p.Node(f.NodeI); nj = p.Node(f.NodeJ); } catch { continue; }
            if (ni == null || nj == null) continue;
            if (!MepGeom.SegmentsIntersect(a.X, a.Y, c.X, c.Y, ni.X, ni.Y, nj.X, nj.Y)) continue;
            var (bb, bh) = Dims(p, f);
            double zt = Math.Max(ni.Z, nj.Z), zb = zt - bh;
            if (top <= zb + 1e-6 || bot >= zt - 1e-6) continue;
            double ex = nj.X - ni.X, ey = nj.Y - ni.Y, el = Math.Sqrt(ex * ex + ey * ey);
            if (el < 1e-9) continue;
            double den = ux * ey - uy * ex;
            double s = Math.Abs(den) < 1e-12 ? L / 2 : ((ni.X - a.X) * ey - (ni.Y - a.Y) * ex) / den;
            double sin = Math.Max(0.2, Math.Abs(den) / el);
            // nửa bề rộng dầm đo dọc ống + nửa bề rộng ống chiếu (ống xiên góc với dầm) + khe hở
            double cos = Math.Sqrt(Math.Max(0, 1 - sin * sin));
            double along = bb / 2 / sin + d.Width / 2000.0 * cos / sin + clr;
            res.Add(new Cross(s, along, zb, f.Id, a.X + ux * s, a.Y + uy * s));
        }
        return res.OrderBy(x => x.S).ToList();
    }

    private static MepWarning Warn(HvacDuct d, Cross x, double bottom, double lim) => new()
    {
        Code = Code, Story = d.Story, X = x.X, Y = x.Y, FrameId = x.FrameId,
        Message = $"Ống gió {d.SizeText} ({HvacSizing.SystemName(d.System)}) không hạ được dưới dầm {x.FrameId}: đáy ống sau khi hạ {bottom:0.###} m thấp hơn giới hạn trần {lim:0.###} m (thiếu {(lim - bottom) * 1000:0} mm)",
        Fix = "Đổi ống dẹt hơn (giữ tiết diện) / đi vòng tránh dầm / nâng trần; nếu buộc xuyên dầm phải có lỗ chờ + gia cường được kết cấu duyệt"
    };

    /// <summary>Hạ ống gió d dưới các dầm nó va (đoạn ngang): mỗi dầm thêm 4 đỉnh – xuống 45°, chạy dưới đáy dầm (khe BeamClearance), lên 45°.
    /// Không đủ chỗ trần → cảnh báo, giữ nguyên đoạn đó; đoạn quá ngắn để xiên 45° → hạ cả tuyến nếu trần cho phép.</summary>
    public static HvacDropResult DropUnderBeams(Project p, HvacDuct d)
    {
        var warns = new List<MepWarning>();
        if (d == null || d.Path.Count < 2) return new HvacDropResult(d?.Id ?? 0, 0, false, warns);
        var set = p.Mep.Hvac.Settings;
        double clr = Math.Max(0, set.BeamClearance), half = d.Depth / 2000.0;
        var beams = p.Frames.Where(f => !f.IsVerticalKind).ToList();
        var outPts = new List<MepPt> { d.Path[0] };
        int drops = 0; bool needWhole = false; double wholeZ = double.MaxValue;
        for (int i = 1; i < d.Path.Count; i++)
        {
            var a = d.Path[i - 1]; var c = d.Path[i];
            double L = MepGeom.Dist2D(a.X, a.Y, c.X, c.Y);
            if (L < 1e-6 || Math.Abs(a.Z - c.Z) > 1e-6) { outPts.Add(c); continue; }
            double z0 = a.Z;
            // cửa sổ hạ ống: [w0 bắt đầu xiên xuống, w1 tới đáy, w2 hết đáy, w3 lên lại z0]
            var win = new List<(double W1, double W2, double Z, Cross X)>();
            foreach (var x in Crossings(p, d, a, c, beams))
            {
                double zLow = x.BeamBottom - clr - half;
                double lim = MinBottom(p, d.Story, x.X, x.Y);
                if (zLow - half < lim - 1e-9) { warns.Add(Warn(d, x, zLow - half, lim)); continue; }
                win.Add((x.S - x.HalfAlong, x.S + x.HalfAlong, zLow, x));
            }
            // gộp các cửa sổ chồng nhau (2 dầm sát nhau → 1 lần hạ)
            var merged = new List<(double W1, double W2, double Z)>();
            foreach (var w in win.OrderBy(q => q.W1))
            {
                if (merged.Count > 0)
                {
                    var m = merged[^1];
                    double dzPrev = z0 - Math.Min(m.Z, w.Z);
                    if (w.W1 - dzPrev <= m.W2 + dzPrev + 1e-6) { merged[^1] = (m.W1, Math.Max(m.W2, w.W2), Math.Min(m.Z, w.Z)); continue; }
                }
                merged.Add((w.W1, w.W2, w.Z));
            }
            double ux = (c.X - a.X) / L, uy = (c.Y - a.Y) / L;
            MepPt At(double s, double z) => new(a.X + ux * s, a.Y + uy * s, z, 0);
            foreach (var m in merged)
            {
                double dz = z0 - m.Z, w0 = m.W1 - dz, w3 = m.W2 + dz;
                if (w0 < -1e-6 || w3 > L + 1e-6) { needWhole = true; wholeZ = Math.Min(wholeZ, m.Z); continue; }
                if (w0 > 1e-6) outPts.Add(At(w0, z0));
                outPts.Add(At(m.W1, m.Z)); outPts.Add(At(m.W2, m.Z));
                if (w3 < L - 1e-6) outPts.Add(At(w3, z0));
                drops++;
            }
            outPts.Add(c);
        }
        if (needWhole)
        {
            // hạ cả tuyến (đoạn ngắn không đủ xiên 45°) – kiểm tra trần tại mọi đỉnh
            double worst = d.Path.Max(q => MinBottom(p, d.Story, q.X, q.Y));
            if (wholeZ - half >= worst - 1e-9)
            {
                d.Path = d.Path.Select(q => new MepPt(q.X, q.Y, Math.Min(q.Z, wholeZ), q.WallId)).ToList();
                HvacRouting.RebuildFittings(p);
                // ống đã hạ cả tuyến: các dầm còn lại cũng đã nằm trên → chạy lại để xử lý dầm sâu hơn (nếu có)
                var again = DropUnderBeams(p, d);
                warns.AddRange(again.Warnings);
                return new HvacDropResult(d.Id, drops + again.Drops, true, warns);
            }
            var any = Crossings(p, d, d.Path[0], d.Path[^1], beams).FirstOrDefault();
            warns.Add(new MepWarning
            {
                Code = Code, Story = d.Story, X = d.Path[0].X, Y = d.Path[0].Y, FrameId = any?.FrameId ?? 0,
                Message = $"Ống gió {d.SizeText}: đoạn ống quá ngắn để hạ bằng 2 cút 45° và hạ cả tuyến thì đáy ống {wholeZ - half:0.###} m thấp hơn giới hạn trần {worst:0.###} m",
                Fix = "Kéo dài đoạn ống / đổi ống dẹt hơn / đi vòng tránh dầm"
            });
        }
        d.Path = Dedup(outPts);
        HvacRouting.RebuildFittings(p);
        return new HvacDropResult(d.Id, drops, false, warns);
    }

    private static List<MepPt> Dedup(List<MepPt> pts)
    {
        var res = new List<MepPt>();
        foreach (var q in pts)
            if (res.Count == 0 || MepGeom.Dist(res[^1], q) > 1e-6) res.Add(q);
        return res;
    }

    /// <summary>Hạ mọi ống gió của tầng (story = null → mọi tầng).</summary>
    public static List<HvacDropResult> DropAll(Project p, string story = null) =>
        (p?.Mep?.Hvac?.Ducts ?? new List<HvacDuct>()).Where(d => story == null || d.Story == story).ToList().Select(d => DropUnderBeams(p, d)).ToList();

    /// <summary>Cảnh báo cho các va chạm ống gió – dầm KHÔNG thể hạ ống (trần không đủ chỗ).</summary>
    public static List<MepWarning> Warnings(Project p)
    {
        var res = new List<MepWarning>();
        var h = p?.Mep?.Hvac;
        if (h == null || h.Ducts.Count == 0) return res;
        var beams = p.Frames.Where(f => !f.IsVerticalKind).ToList();
        double clr = Math.Max(0, h.Settings.BeamClearance);
        foreach (var d in h.Ducts)
        {
            double half = d.Depth / 2000.0;
            for (int i = 1; i < d.Path.Count; i++)
                foreach (var x in Crossings(p, d, d.Path[i - 1], d.Path[i], beams))
                {
                    double bottom = x.BeamBottom - clr - 2 * half, lim = MinBottom(p, d.Story, x.X, x.Y);
                    if (bottom < lim - 1e-9) res.Add(Warn(d, x, bottom, lim));
                }
        }
        return res;
    }
}

/// <summary>Tải lạnh một phòng + máy chọn.</summary>
public sealed record HvacRoomLoad(int RoomId, string Room, double Area, double Height, bool Exterior, int People,
    double LoadBtu, double LoadKw, double UnitBtu, int UnitCount, string Method);

/// <summary>Điện cấp cho máy điều hoà.</summary>
public sealed record HvacAcPower(double PowerW, double CurrentA, double DesignA, double BreakerA, double WireMm2);

/// <summary>Kết quả đặt máy điều hoà cho phòng.</summary>
public sealed record HvacAcPlacement(HvacRoomLoad Load, List<HvacUnit> Indoor, List<HvacUnit> Outdoor, List<HvacPipe> Pipes,
    int IndoorWallId, int OutdoorWallId, HvacAcPower Power, List<string> Notes);

public static class HvacLoad
{
    /// <summary>Công suất máy treo tường thông dụng (BTU/h).</summary>
    public static readonly double[] UnitSizes = { 9000, 12000, 18000, 24000 };
    /// <summary>Tải lạnh riêng (BTU/h·m²) cho phòng cao ≤ 3 m.</summary>
    public const double BtuPerM2 = 600;
    public const double ExteriorFactor = 0.15;
    public const double BtuPerExtraPerson = 500;
    public const double BtuToKw = 0.00029307107;
    /// <summary>Hệ số hiệu quả máy (COP) dùng tính công suất điện.</summary>
    public const double Cop = 3.0;
    /// <summary>Hệ số dòng thiết kế động cơ máy nén.</summary>
    public const double MotorFactor = 1.25;

    public const string MethodText = "Q = 600·A·max(1; h/3)·(1 + 0,15 nếu có tường ngoài) + 500·max(0; N − 2) (BTU/h)";

    /// <summary>Tải lạnh (BTU/h) theo phương pháp diện tích.</summary>
    public static double Load(double area, double height = 3.0, bool exterior = false, int people = 0) =>
        BtuPerM2 * Math.Max(0, area) * Math.Max(1, height / 3.0) * (1 + (exterior ? ExteriorFactor : 0)) + BtuPerExtraPerson * Math.Max(0, people - 2);

    /// <summary>Chọn máy: công suất chuẩn nhỏ nhất ≥ Q; Q &gt; 24000 → n máy cùng cỡ nhỏ nhất sao cho n·cỡ ≥ Q.</summary>
    public static (double Btu, int Count) Choose(double q)
    {
        foreach (double s in UnitSizes) if (s >= q - 1e-6) return (s, 1);
        int n = (int)Math.Ceiling(q / UnitSizes[^1] - 1e-9);
        foreach (double s in UnitSizes) if (n * s >= q - 1e-6) return (s, n);
        return (UnitSizes[^1], n);
    }

    /// <summary>Bề rộng dàn lạnh treo tường (m) theo công suất.</summary>
    public static double IndoorWidth(double btu) => btu <= 9000 + 1e-6 ? 0.8 : btu <= 12000 + 1e-6 ? 0.9 : btu <= 18000 + 1e-6 ? 1.0 : 1.1;

    /// <summary>Điện máy lạnh: P = Q·0,000293·1000 / COP (W); I = P / (U·cosφ); dòng thiết kế 1,25·I → aptomat; dây theo MepSizing (≥ 2,5 mm²).</summary>
    public static HvacAcPower Power(double btu, int count = 1)
    {
        double pw = btu * BtuToKw * 1000 / Cop * Math.Max(1, count);
        double i = MepSizing.Current(pw), di = MotorFactor * i;
        return new HvacAcPower(pw, i, di, MepSizing.Breaker(di), MepSizing.WireFor(MotorFactor * pw, MepSystemKind.DienDieuHoa));
    }

    private static double Thick(Project p, WallLine w) => ModelSolids.WallThickness(p, w);

    /// <summary>Tường ngoài: kiểu tường có chức năng "Tường ngoài".</summary>
    public static bool IsExterior(Project p, WallLine w) =>
        p.WallTypes?.FirstOrDefault(t => t.Name == w.Type)?.Function == WallFunctionName.Exterior;

    /// <summary>Tường bao quanh phòng: tường cùng tầng có tim song song và nằm sát một cạnh đường bao phòng (≤ nửa dày + 0,15 m).</summary>
    public static List<WallLine> RoomWalls(Project p, Room r)
    {
        var res = new List<WallLine>();
        var o = r.Outline;
        if (o == null || o.Count < 3) return res;
        foreach (var w in p.Walls.Where(w => w.Story == r.Story && w.Chord > 1e-6))
        {
            double tol = Thick(p, w) / 2 + 0.15;
            double mx = (w.X1 + w.X2) / 2, my = (w.Y1 + w.Y2) / 2, wx = (w.X2 - w.X1) / w.Chord, wy = (w.Y2 - w.Y1) / w.Chord;
            for (int i = 0; i < o.Count; i++)
            {
                var a = o[i]; var b = o[(i + 1) % o.Count];
                double ex = b[0] - a[0], ey = b[1] - a[1], el = Math.Sqrt(ex * ex + ey * ey);
                if (el < 1e-6 || Math.Abs(wx * ey - wy * ex) / el > 0.05) continue;
                var pr = MepGeom.Project(mx, my, a[0], a[1], b[0], b[1]);
                if (pr.Dist <= tol && pr.T > 1e-6 && pr.T < 1 - 1e-6) { res.Add(w); break; }
            }
        }
        return res;
    }

    /// <summary>Tải lạnh của phòng (chiều cao = cao độ trần phòng, nếu không có thì chiều cao tầng).</summary>
    public static HvacRoomLoad RoomLoad(Project p, Room r, int people = 0)
    {
        double hgt = r.CeilingHeight > 0 ? r.CeilingHeight : MepRouting.StoryHeight(p, r.Story, (p.Mep ?? new MepModel()).Settings);
        bool ext = RoomWalls(p, r).Any(w => IsExterior(p, w));
        double a = r.Area, q = Load(a, hgt, ext, people);
        var (btu, n) = Choose(q);
        return new HvacRoomLoad(r.Id, string.IsNullOrEmpty(r.Name) ? $"Phòng {r.Id}" : r.Name, a, hgt, ext, people, q, q * BtuToKw, btu, n, MethodText);
    }

    private static (double X, double Y) DoorPoint(WallLine w, WallOpening o) =>
        (w.X1 + (w.X2 - w.X1) / w.Chord * o.Offset, w.Y1 + (w.Y2 - w.Y1) / w.Chord * o.Offset);

    /// <summary>Đặt máy điều hoà cho phòng: tính tải, chọn máy, dàn lạnh lên tường tốt nhất (không có cửa đi, xa cửa đi nhất, ưu tiên tường ngoài),
    /// dàn nóng ở mặt ngoài tường ngoài gần nhất, nối ống đồng + ống nước ngưng xả ra ngoài.</summary>
    public static HvacAcPlacement PlaceAc(Project p, Room r, int people = 0)
    {
        p.Mep ??= new MepModel(); p.Mep.Hvac ??= new HvacModel();
        var notes = new List<string>();
        var load = RoomLoad(p, r, people);
        var walls = RoomWalls(p, r);
        var (cx, cy) = r.Center;
        var doors = new List<(double X, double Y, int WallId)>();
        foreach (var w in walls)
            foreach (var o in w.Openings ?? new List<WallOpening>())
                if (WallAssembly.TypeOf(p, o)?.Kind == WallOpeningKind.Door) { var (dx, dy) = DoorPoint(w, o); doors.Add((dx, dy, w.Id)); }
        if (doors.Count == 0) notes.Add("Phòng chưa có cửa đi – chọn tường theo ưu tiên tường ngoài.");
        WallLine best = null; double bestScore = double.MinValue;
        foreach (var w in walls)
        {
            double mx = (w.X1 + w.X2) / 2, my = (w.Y1 + w.Y2) / 2;
            double score = (doors.Count > 0 ? doors.Min(d => MepGeom.Dist2D(mx, my, d.X, d.Y)) : 0)
                + (IsExterior(p, w) ? 2.0 : 0) + Math.Min(w.Chord, 3) * 0.1 - (doors.Any(d => d.WallId == w.Id) ? 100 : 0);
            if (score > bestScore + 1e-9) { bestScore = score; best = w; }
        }
        var indoor = new List<HvacUnit>(); var outdoor = new List<HvacUnit>(); var pipes = new List<HvacPipe>();
        if (best == null)
        {
            notes.Add("Không tìm thấy tường bao phòng – đặt dàn lạnh tại tâm phòng, cần chỉnh tay.");
            var u0 = HvacRouting.AddUnit(p, r.Story, HvacUnitKind.DanLanh, cx, cy, load.UnitBtu);
            indoor.Add(u0);
            return new HvacAcPlacement(load, indoor, outdoor, pipes, 0, 0, Power(load.UnitBtu, load.UnitCount), notes);
        }
        if (doors.Any(d => d.WallId == best.Id)) notes.Add("Mọi tường đều có cửa đi – dàn lạnh đặt cạnh cửa, kiểm tra lại.");
        // pháp tuyến vào trong phòng
        double ux = (best.X2 - best.X1) / best.Chord, uy = (best.Y2 - best.Y1) / best.Chord, nx = -uy, ny = ux;
        double mx0 = (best.X1 + best.X2) / 2, my0 = (best.Y1 + best.Y2) / 2;
        if ((cx - mx0) * nx + (cy - my0) * ny < 0) { nx = -nx; ny = -ny; }
        double t = Thick(p, best), ang = Math.Atan2(uy, ux) * 180 / Math.PI;
        // phần tường nằm trong phòng: chiếu các đỉnh phòng lên tường
        double s0 = double.MaxValue, s1 = double.MinValue;
        foreach (var q in r.Outline)
        {
            double s = (q[0] - best.X1) * ux + (q[1] - best.Y1) * uy, dn = Math.Abs((q[0] - best.X1) * nx + (q[1] - best.Y1) * ny);
            if (dn <= t / 2 + 0.2) { s0 = Math.Min(s0, s); s1 = Math.Max(s1, s); }
        }
        if (s0 > s1) { s0 = 0; s1 = best.Chord; }
        s0 = Math.Max(0, s0); s1 = Math.Min(best.Chord, s1);
        WallLine outW = IsExterior(p, best) ? best : null;
        if (outW == null)
            outW = walls.Where(w => IsExterior(p, w)).OrderBy(w => MepGeom.Dist2D((w.X1 + w.X2) / 2, (w.Y1 + w.Y2) / 2, mx0, my0)).FirstOrDefault();
        if (outW == null)
        {
            outW = p.Walls.Where(w => w.Story == r.Story && w.Chord > 1e-6 && IsExterior(p, w))
                .OrderBy(w => MepGeom.Project(cx, cy, w.X1, w.Y1, w.X2, w.Y2).Dist).FirstOrDefault();
            notes.Add(outW == null ? "Tầng không có tường ngoài – dàn nóng đặt sau tường dàn lạnh, cần chỉnh tay." : $"Phòng không có tường ngoài – dàn nóng đặt trên tường ngoài {outW.Id} gần nhất.");
        }
        for (int k = 0; k < load.UnitCount; k++)
        {
            double s = s0 + (s1 - s0) * (k + 0.5) / load.UnitCount;
            double bx = best.X1 + ux * s, by = best.Y1 + uy * s;
            var u = HvacRouting.AddUnit(p, r.Story, HvacUnitKind.DanLanh, 0, 0, load.UnitBtu);
            u.W = IndoorWidth(load.UnitBtu);
            u.X = bx + nx * (t / 2 + u.D / 2); u.Y = by + ny * (t / 2 + u.D / 2); u.Angle = ang;
            indoor.Add(u);
            // dàn nóng: mặt ngoài tường ngoài, điểm gần dàn lạnh nhất
            var ow = outW ?? best;
            double ox = (ow.X2 - ow.X1) / ow.Chord, oy = (ow.Y2 - ow.Y1) / ow.Chord, onx = -oy, ony = ox;
            var pr = MepGeom.Project(bx, by, ow.X1, ow.Y1, ow.X2, ow.Y2);
            double omx = (ow.X1 + ow.X2) / 2, omy = (ow.Y1 + ow.Y2) / 2;
            if ((cx - omx) * onx + (cy - omy) * ony > 0) { onx = -onx; ony = -ony; }   // hướng ra ngoài phòng
            double ot = Thick(p, ow);
            var v = HvacRouting.AddUnit(p, r.Story, HvacUnitKind.DanNong, 0, 0, load.UnitBtu);
            v.W = load.UnitBtu <= 12000 + 1e-6 ? 0.7 : load.UnitBtu <= 18000 + 1e-6 ? 0.8 : 0.9;
            v.X = pr.Px + onx * (ot / 2 + v.D / 2 + 0.1); v.Y = pr.Py + ony * (ot / 2 + v.D / 2 + 0.1); v.Angle = Math.Atan2(oy, ox) * 180 / Math.PI;
            outdoor.Add(v);
            pipes.Add(HvacRouting.ConnectRefrigerant(p, u, v));
            pipes.Add(HvacRouting.AddCondensate(p, u, pr.Px + onx * (ot / 2 + 0.05), pr.Py + ony * (ot / 2 + 0.05)));
        }
        var pw = Power(load.UnitBtu, 1);
        notes.Add($"Tải lạnh {load.LoadBtu:0} BTU/h ({load.LoadKw:0.00} kW) → {load.UnitCount} × {load.UnitBtu:0} BTU; ống đồng {HvacSizing.Mm(HvacSizing.CopperPair(load.UnitBtu).Liquid)}/{HvacSizing.Mm(HvacSizing.CopperPair(load.UnitBtu).Gas)} mm; "
            + $"điện mỗi máy {pw.PowerW:0} W, aptomat {pw.BreakerA:0} A, dây {HvacSizing.Mm(pw.WireMm2)} mm².");
        return new HvacAcPlacement(load, indoor, outdoor, pipes, best.Id, outW?.Id ?? 0, pw, notes);
    }
}

/// <summary>Kiểu IFC2X3 cho khối ĐHKK (theo Category / Id của khối 3D).</summary>
public static class HvacIfc
{
    /// <summary>(thực thể kiểu, PredefinedType) cho khối; null = không phải ĐHKK.</summary>
    public static (string Entity, string Predefined)? TypeOf(Solid s)
    {
        if (s == null) return null;
        string n = s.Name ?? "";
        return s.Category switch
        {
            "Ống gió" => ("IFCDUCTSEGMENTTYPE", "RIGIDSEGMENT"),
            "Phụ kiện ống gió" => ("IFCDUCTFITTINGTYPE", n.StartsWith("Tê") ? "JUNCTION" : n.StartsWith("Côn") ? "TRANSITION" : "BEND"),
            "Miệng gió" => ("IFCAIRTERMINALTYPE", "DIFFUSER"),
            "Thiết bị điều hoà" => ("IFCUNITARYEQUIPMENTTYPE", "SPLITSYSTEM"),
            "Ống đồng" or "Ống nước ngưng" => ("IFCPIPESEGMENTTYPE", "RIGIDSEGMENT"),
            _ => null
        };
    }
}
