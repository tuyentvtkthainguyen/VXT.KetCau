
namespace KetCauVXT.Model;

// =====================================================================
// THAO TÁC GIAO DIỆN (lõi, kiểm chứng được):
//   • Screen3DSelect: chiếu khối 3D lên màn hình theo camera phối cảnh (như WPF PerspectiveCamera, FieldOfView ngang),
//     quét chọn khung bao (nằm trọn) / khung cắt (chạm) trong khung nhìn 3D; đổi khối → PlanSelItem.
//   • GridDragMath: kéo trục bằng chuột – bắt bước 10 mm, kích thước tạm tới trục kề, dời trục (kéo theo cấu kiện tuỳ chọn).
//   • LevelDragMath: kéo đường cao độ trên mặt đứng / mặt cắt – tìm tầng gần con trỏ, bắt 10 mm, kẹp giới hạn, đổi cao độ.
//   • GridDisplay: bong bóng trục từng đầu (cả hai / đầu / cuối / không) và ẩn trục theo tầng.
// =====================================================================

/// <summary>Quét chọn trong khung nhìn 3D.</summary>
public static class Screen3DSelect
{
    /// <summary>Chiếu điểm thế giới p lên màn hình w × h (px). Trả (x, y, độ sâu dọc hướng nhìn); null nếu nằm sau camera.
    /// Ngược với Pick3DMath.CameraRay.</summary>
    public static (double X, double Y, double Depth)? Project(Vec3 pos, Vec3 look, Vec3 up, double fovDeg, double w, double h, Vec3 p)
    {
        var f = look.Normalize();
        var r = f.Cross(up).Normalize();
        var u = r.Cross(f).Normalize();
        var d = p - pos;
        double depth = d.Dot(f);
        if (depth <= 1e-9) return null;
        double t = Math.Tan(fovDeg * Math.PI / 360);
        double ww = Math.Max(w, 1), hh = Math.Max(h, 1);
        double nx = d.Dot(r) / (depth * t);
        double ny = d.Dot(u) / (depth * t * hh / ww);
        return ((nx + 1) * ww / 2, (1 - ny) * hh / 2, depth);
    }

    /// <summary>Các đỉnh hộp bao địa phương của khối (8 đỉnh).</summary>
    public static IEnumerable<Vec3> Corners(Solid s)
    {
        if (s == null || s.Profile.Count == 0) yield break;
        double x0 = s.Profile.Min(q => q[0]), x1 = s.Profile.Max(q => q[0]);
        double y0 = s.Profile.Min(q => q[1]), y1 = s.Profile.Max(q => q[1]);
        double z0 = Math.Min(0, s.Depth), z1 = Math.Max(0, s.Depth);
        foreach (double x in new[] { x0, x1 })
            foreach (double y in new[] { y0, y1 })
                foreach (double z in new[] { z0, z1 })
                    yield return s.ToWorld(x, y, z);
    }

    /// <summary>Hình chữ nhật bao trên màn hình của khối; null nếu có đỉnh nằm sau camera (bỏ qua để an toàn).</summary>
    public static SelectionRules.Box? ScreenRect(Solid s, Vec3 pos, Vec3 look, Vec3 up, double fovDeg, double w, double h)
    {
        double xa = double.MaxValue, ya = double.MaxValue, xb = double.MinValue, yb = double.MinValue;
        int n = 0;
        foreach (var c in Corners(s))
        {
            var q = Project(pos, look, up, fovDeg, w, h, c);
            if (q == null) return null;
            xa = Math.Min(xa, q.Value.X); xb = Math.Max(xb, q.Value.X);
            ya = Math.Min(ya, q.Value.Y); yb = Math.Max(yb, q.Value.Y);
            n++;
        }
        return n == 0 ? null : SelectionRules.Box.Of(xa, ya, xb, yb);
    }

    /// <summary>Khối nằm trong khung (bao: nằm trọn; cắt: chạm). Khung toạ độ màn hình px. Bỏ khối lỗ khoét.</summary>
    public static List<Solid> Select(IEnumerable<Solid> solids, Vec3 pos, Vec3 look, Vec3 up, double fovDeg, double w, double h,
        SelectionRules.Box frame, bool crossing)
    {
        var res = new List<Solid>();
        foreach (var s in solids)
        {
            if (s == null || s.IsVoid) continue;
            var rb = ScreenRect(s, pos, look, up, fovDeg, w, h);
            if (rb == null) continue;
            var b = rb.Value;
            bool inside = b.X0 >= frame.X0 - 1e-9 && b.X1 <= frame.X1 + 1e-9 && b.Y0 >= frame.Y0 - 1e-9 && b.Y1 <= frame.Y1 + 1e-9;
            bool touch = b.X1 >= frame.X0 && b.X0 <= frame.X1 && b.Y1 >= frame.Y0 && b.Y0 <= frame.Y1;
            if (crossing ? touch : inside) res.Add(s);
        }
        return res;
    }

    /// <summary>Đổi mã khối 3D (ModelSolids / MepRegistration) sang mục chọn mặt bằng; null nếu loại không chọn được.</summary>
    public static PlanSelItem? ToSelItem(Solid s)
    {
        if (s == null || string.IsNullOrEmpty(s.Id)) return null;
        var parts = s.Id.Split('-');
        int Num(int i) => i < parts.Length && int.TryParse(parts[i], out int v) ? v : 0;
        switch (parts[0])
        {
            case "COT": return new PlanSelItem(SelCat.Cot, Num(1));
            case "DAM": return new PlanSelItem(SelCat.Dam, Num(1));
            case "SAN": return new PlanSelItem(SelCat.San, Num(1));
            case "TUONG": return new PlanSelItem(SelCat.Tuong, Num(1));
            case "CUA": return new PlanSelItem(SelCat.Cua, Num(2), Num(1));
            case "DD": return new PlanSelItem(SelCat.DoDac, Num(1));
            case "DN":
                if (parts.Length > 2 && parts[1] == "TB") return new PlanSelItem(SelCat.ThietBi, Num(2));
                if (parts.Length > 2 && parts[1] == "ONG") return new PlanSelItem(SelCat.TuyenOng, Num(2));
                return null;
            default: return null;
        }
    }

    /// <summary>Danh sách mục chọn (không trùng) từ các khối.</summary>
    public static List<PlanSelItem> ToSelItems(IEnumerable<Solid> solids) =>
        solids.Select(ToSelItem).Where(x => x != null && x.Value.Id > 0).Select(x => x.Value).Distinct().ToList();
}

/// <summary>Kéo trục bằng chuột trên mặt bằng.</summary>
public static class GridDragMath
{
    /// <summary>Bắt bước (mặc định 10 mm).</summary>
    public static double Snap(double v, double step = 0.01) => step <= 0 ? v : Math.Round(Math.Round(v / step) * step, 6);

    /// <summary>Trục kề trước / sau (cùng phương) – để hiện kích thước tạm; null nếu không có.</summary>
    public static (GridLine Prev, GridLine Next) Neighbours(Project p, GridLine g)
    {
        var same = (g.Dir == GridDir.X ? p.GridsX : p.GridsY).Where(x => x != g).ToList();
        return (same.Where(x => x.Coord < g.Coord - 1e-9).OrderByDescending(x => x.Coord).FirstOrDefault(),
                same.Where(x => x.Coord > g.Coord + 1e-9).OrderBy(x => x.Coord).FirstOrDefault());
    }

    /// <summary>Kích thước tạm khi kéo trục tới toạ độ c: khoảng cách tới trục kề trước / sau (m; NaN nếu không có).</summary>
    public static (double ToPrev, double ToNext) TempDims(Project p, GridLine g, double c)
    {
        var (a, b) = Neighbours(p, g);
        return (a == null ? double.NaN : c - a.Coord, b == null ? double.NaN : b.Coord - c);
    }

    /// <summary>Trục gần điểm (x, y) trong phạm vi tol (m) – để bấm giữ kéo; null nếu không có.</summary>
    public static GridLine HitGrid(Project p, double x, double y, double tol)
    {
        GridLine best = null; double bd = tol;
        foreach (var g in p.Grids)
        {
            double d = Math.Abs((g.Dir == GridDir.X ? x : y) - g.Coord);
            if (d <= bd) { bd = d; best = g; }
        }
        return best;
    }

    /// <summary>Nắm trục ở GIỮA đường trục (không chỉ bong bóng): điểm (x, y) cách đường trục ≤ tol và nằm trong đoạn trục vẽ
    /// [min trục vuông góc − ext; max + ext]. anywhere = false: chỉ nhận phần "đuôi" trục ngoài khung lưới (không lẫn với cấu kiện bên trong);
    /// anywhere = true (giữ Alt): nhận cả đoạn trong khung lưới. Trục ẩn ở tầng story bỏ qua.</summary>
    public static GridLine HitGridLine(Project p, string story, double x, double y, double tol, double ext = 1.2, bool anywhere = false)
    {
        var gxs = p.GridsX.Where(g => GridDisplay.Visible(g, story)).ToList();
        var gys = p.GridsY.Where(g => GridDisplay.Visible(g, story)).ToList();
        double yMin = gys.Count > 0 ? gys.Min(g => g.Coord) : 0, yMax = gys.Count > 0 ? gys.Max(g => g.Coord) : 10;
        double xMin = gxs.Count > 0 ? gxs.Min(g => g.Coord) : 0, xMax = gxs.Count > 0 ? gxs.Max(g => g.Coord) : 10;
        GridLine best = null; double bd = tol;
        foreach (var g in gxs)
        {
            double d = Math.Abs(x - g.Coord);
            if (d > bd || y < yMin - ext - 1e-9 || y > yMax + ext + 1e-9) continue;
            bool tail = y < yMin - 1e-9 || y > yMax + 1e-9;
            if (!anywhere && !tail) continue;
            bd = d; best = g;
        }
        foreach (var g in gys)
        {
            double d = Math.Abs(y - g.Coord);
            if (d > bd || x < xMin - ext - 1e-9 || x > xMax + ext + 1e-9) continue;
            bool tail = x < xMin - 1e-9 || x > xMax + 1e-9;
            if (!anywhere && !tail) continue;
            bd = d; best = g;
        }
        return best;
    }

    /// <summary>Kết thúc kéo: bắt 10 mm rồi dời trục (GridOps.MoveGrid). Trả (toạ độ mới, số đối tượng dời theo).</summary>
    public static (double Coord, int Moved) Drop(Project p, GridLine g, double rawCoord, bool dragMembers, double step = 0.01)
    {
        double c = Snap(rawCoord, step);
        int n = GridOps.MoveGrid(p, g, c, dragMembers);
        return (g.Coord, n);
    }
}

/// <summary>Kéo đường cao độ trên mặt đứng / mặt cắt.</summary>
public static class LevelDragMath
{
    /// <summary>Tầng có cao độ gần z nhất trong phạm vi tol (m); null nếu không có.</summary>
    public static Story NearestStory(Project p, double z, double tol)
    {
        Story best = null; double bd = tol;
        foreach (var s in p.Stories)
        {
            double d = Math.Abs(s.Elevation - z);
            if (d <= bd) { bd = d; best = s; }
        }
        return best;
    }

    /// <summary>Cao độ được phép khi kéo: bắt 10 mm rồi kẹp trong giới hạn LevelOps.Limits.</summary>
    public static double Clamp(Project p, string story, double z, double step = 0.01)
    {
        double v = GridDragMath.Snap(z, step);
        var (lo, hi) = LevelOps.Limits(p, story);
        if (double.IsNaN(lo)) return v;
        if (v < lo) v = GridDragMath.Snap(Math.Ceiling(lo / Math.Max(step, 1e-9) - 1e-6) * step, step);
        if (v > hi) v = GridDragMath.Snap(Math.Floor(hi / Math.Max(step, 1e-9) + 1e-6) * step, step);
        return v;
    }

    /// <summary>Đổi vị trí con trỏ trên ảnh khung nhìn (mặt đứng / mặt cắt vẽ ở toạ độ mm, y = z·1000) ra cao độ mô hình (m).
    /// ptY, imgH: px trên ảnh; boundsY, boundsH: khung bản vẽ đã kết xuất (mm giấy, y hướng xuống);
    /// extY1, extH: phạm vi bản vẽ gốc (mm mô hình); k = 1 / tỷ lệ.</summary>
    public static double ModelZFromView(double ptY, double imgH, double boundsY, double boundsH, double extY1, double extH, double k)
    {
        double drawY = boundsY + ptY / Math.Max(imgH, 1e-9) * boundsH;
        return (extY1 + (extH * k - drawY) / Math.Max(k, 1e-12)) / 1000.0;
    }

    /// <summary>Ngược lại: cao độ z (m) → toạ độ px trên ảnh.</summary>
    public static double ViewYFromModelZ(double z, double imgH, double boundsY, double boundsH, double extY1, double extH, double k)
    {
        double drawY = extH * k - (z * 1000.0 - extY1) * k;
        return (drawY - boundsY) / Math.Max(boundsH, 1e-12) * imgH;
    }

    /// <summary>Thả đường cao độ: đặt cao độ mới (đã bắt / kẹp). Trả cao độ đã đặt.</summary>
    public static double Drop(Project p, string story, double rawZ, double step = 0.01)
    {
        double z = Clamp(p, story, rawZ, step);
        LevelOps.SetElevation(p, story, z);
        return z;
    }
}

/// <summary>Bong bóng trục từng đầu.</summary>
public enum GridBubbleMode { Both, Start, End, None }

/// <summary>Hiển thị trục: bong bóng và ẩn theo tầng.</summary>
public static class GridDisplay
{
    public static readonly string[] BubbleTitles = { "Cả hai đầu", "Chỉ đầu dưới / trái", "Chỉ đầu trên / phải", "Không bong bóng" };

    public static GridBubbleMode Mode(GridLine g) =>
        g.BubbleStart && g.BubbleEnd ? GridBubbleMode.Both : g.BubbleStart ? GridBubbleMode.Start : g.BubbleEnd ? GridBubbleMode.End : GridBubbleMode.None;

    public static void SetMode(GridLine g, GridBubbleMode m)
    {
        g.BubbleStart = m is GridBubbleMode.Both or GridBubbleMode.Start;
        g.BubbleEnd = m is GridBubbleMode.Both or GridBubbleMode.End;
    }

    /// <summary>Trục hiện trên mặt bằng tầng story? (story null = mọi tầng → luôn hiện).</summary>
    public static bool Visible(GridLine g, string story) =>
        string.IsNullOrEmpty(story) || g.HiddenStories == null || !g.HiddenStories.Contains(story);

    /// <summary>Ẩn / hiện trục trên một tầng.</summary>
    public static void SetHidden(GridLine g, string story, bool hidden)
    {
        if (string.IsNullOrEmpty(story)) return;
        g.HiddenStories ??= new();
        if (hidden) { if (!g.HiddenStories.Contains(story)) g.HiddenStories.Add(story); }
        else g.HiddenStories.Remove(story);
    }

    /// <summary>Số bong bóng sẽ vẽ cho các trục hiện trên tầng story.</summary>
    public static int BubbleCount(Project p, string story) =>
        p.Grids.Where(g => Visible(g, story)).Sum(g => (g.BubbleStart ? 1 : 0) + (g.BubbleEnd ? 1 : 0));
}
