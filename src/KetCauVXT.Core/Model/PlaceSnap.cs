namespace KetCauVXT.Model;

// =====================================================================
// ĐẶT ĐỒ ĐẠC / THIẾT BỊ BẰNG BẮT ĐIỂM (không nhập toạ độ), KÍCH THƯỚC TẠM KIỂU REVIT, CHỌN – KÉO TRONG 3D,
// MẶT BẰNG CẮT NGANG TỪNG TẦNG (khung nhìn tự sinh).
// Phần giao diện: App/MainWindow.Furnish.cs (mặt bằng + 3D), App/MepWindow.Snap.cs (điện nước).
// =====================================================================

/// <summary>Kết quả bắt điểm khi đặt đồ đạc: tâm, góc xoay (độ), kiểu bắt, tường tựa (0 = không).</summary>
public readonly record struct SnapResult(double X, double Y, double Rotation, string Kind, int WallId);

/// <summary>Bắt điểm đặt đồ đạc: mặt tường (lưng tựa tường, cách mặt hoàn thiện một khe), góc phòng (đầu tường / tường vuông góc),
/// giao trục, cạnh đồ đạc khác. Tự xoay theo phương tường gần nhất.</summary>
public static class FurnitureSnap
{
    /// <summary>Khe hở giữa lưng đồ đạc và mặt tường hoàn thiện (m)</summary>
    public const double Gap = 0.01;
    /// <summary>Khoảng bắt vào góc phòng (m)</summary>
    public const double CornerTol = 0.30;
    /// <summary>Khoảng bắt giao trục / cạnh đồ đạc (m)</summary>
    public const double PointTol = 0.25;

    /// <summary>Tường thẳng của tầng (tầng "NEN" gồm cả tường khai báo ở "Base" / "").</summary>
    public static List<WallLine> StoryWalls(Project p, string story)
    {
        var keys = story == "NEN" || string.IsNullOrEmpty(story) ? new[] { "NEN", "Base", "" } : new[] { story };
        return p.Walls.Where(w => keys.Contains(w.Story ?? "") && !w.IsCurved && w.Chord > 1e-6).ToList();
    }

    public static double Half(Project p, WallLine w) => ModelSolids.WallThickness(p, w) / 2;

    /// <summary>Hộp bao trục toạ độ (nửa rộng hx, hy) của hình chữ nhật w × d xoay rotDeg.</summary>
    public static (double Hx, double Hy) HalfExtents(double width, double depth, double rotDeg)
    {
        double a = rotDeg * Math.PI / 180, c = Math.Abs(Math.Cos(a)), s = Math.Abs(Math.Sin(a));
        return ((width * c + depth * s) / 2, (width * s + depth * c) / 2);
    }

    /// <summary>Bắt điểm tại con trỏ (x, y). tol = khoảng hút vào mặt tường tính từ mép đồ đạc (m).</summary>
    public static SnapResult Snap(Project p, string story, double x, double y, double width, double depth, double rotDeg,
        bool autoAlign = true, int ignoreFurnId = -1, double tol = 0.6)
    {
        // 1) mặt tường gần nhất
        WallLine best = null; double bestGap = double.MaxValue, bestT = 0, bestD = 0;
        foreach (var w in StoryWalls(p, story))
        {
            double t = WallAssembly.ProjectPoint(w, x, y, out _);
            double L = w.Chord, ux = (w.X2 - w.X1) / L, uy = (w.Y2 - w.Y1) / L;
            double raw = (x - w.X1) * ux + (y - w.Y1) * uy;
            if (raw < -0.2 || raw > L + 0.2) continue;   // ngoài phạm vi đoạn tường
            double sd = -(x - w.X1) * uy + (y - w.Y1) * ux;   // >0: bên trái hướng tường
            double g = Math.Abs(sd) - Half(p, w) - depth / 2;   // khe giữa lưng đồ đạc và mặt tường
            if (g > tol) continue;
            if (g < bestGap) { bestGap = g; best = w; bestT = t; bestD = sd; }
        }
        if (best != null && autoAlign)
        {
            var w = best;
            double L = w.Chord, ux = (w.X2 - w.X1) / L, uy = (w.Y2 - w.Y1) / L, nx = -uy, ny = ux;
            int side = bestD >= 0 ? 1 : -1;
            double h = Half(p, w);
            double ang = Math.Atan2(uy, ux) * 180 / Math.PI;
            if (side < 0) ang += 180;
            ang = Norm(ang);
            // dọc tường: chặn trong đoạn, bắt vào góc (đầu tường hoặc mặt tường vuông góc gần đầu)
            double lo = width / 2, hi = L - width / 2;
            double t = bestT;
            string kind = "Mặt tường";
            double cornerLo = lo + CornerInset(p, story, w, true, side), cornerHi = hi - CornerInset(p, story, w, false, side);
            if (L >= width)
            {
                if (Math.Abs(t - cornerLo) <= CornerTol || t < cornerLo) { t = cornerLo; kind = "Góc phòng"; }
                else if (Math.Abs(t - cornerHi) <= CornerTol || t > cornerHi) { t = cornerHi; kind = "Góc phòng"; }
            }
            else t = L / 2;
            double off = side * (h + Gap + depth / 2);
            return new SnapResult(w.X1 + ux * t + nx * off, w.Y1 + uy * t + ny * off, ang, kind, w.Id);
        }
        // 2) giao trục
        foreach (var gx in p.GridsX)
            foreach (var gy in p.GridsY)
                if (Math.Abs(gx.Coord - x) <= PointTol && Math.Abs(gy.Coord - y) <= PointTol)
                    return new SnapResult(gx.Coord, gy.Coord, Norm(rotDeg), "Giao trục", 0);
        // 3) cạnh đồ đạc khác (đặt sát cạnh)
        var (hx, hy) = HalfExtents(width, depth, rotDeg);
        foreach (var f in StoryFurniture(p, story).Where(q => q.Id != ignoreFurnId))
        {
            var (fx, fy) = HalfExtents(f.Width, f.Depth, f.Rotation);
            bool yOver = Math.Abs(y - f.Y) < hy + fy, xOver = Math.Abs(x - f.X) < hx + fx;
            if (yOver)
            {
                if (Math.Abs((x - hx) - (f.X + fx)) <= PointTol) return new SnapResult(f.X + fx + hx, y, Norm(rotDeg), "Cạnh đồ đạc", 0);
                if (Math.Abs((x + hx) - (f.X - fx)) <= PointTol) return new SnapResult(f.X - fx - hx, y, Norm(rotDeg), "Cạnh đồ đạc", 0);
            }
            if (xOver)
            {
                if (Math.Abs((y - hy) - (f.Y + fy)) <= PointTol) return new SnapResult(x, f.Y + fy + hy, Norm(rotDeg), "Cạnh đồ đạc", 0);
                if (Math.Abs((y + hy) - (f.Y - fy)) <= PointTol) return new SnapResult(x, f.Y - fy - hy, Norm(rotDeg), "Cạnh đồ đạc", 0);
            }
        }
        return new SnapResult(x, y, Norm(rotDeg), "Tự do", 0);
    }

    /// <summary>Lùi khỏi đầu tường: nửa chiều dày tường vuông góc nối ở đầu đó (mặt trong phòng) + khe.</summary>
    private static double CornerInset(Project p, string story, WallLine w, bool start, int side)
    {
        double ex = start ? w.X1 : w.X2, ey = start ? w.Y1 : w.Y2;
        double best = 0;
        foreach (var o in StoryWalls(p, story))
        {
            if (ReferenceEquals(o, w)) continue;
            double t = WallAssembly.ProjectPoint(o, ex, ey, out double d);
            if (d > Half(p, o) + 0.02) continue;
            // không thẳng hàng
            double c = Math.Abs(((w.X2 - w.X1) * (o.X2 - o.X1) + (w.Y2 - w.Y1) * (o.Y2 - o.Y1)) / (w.Chord * o.Chord));
            if (c > 0.9) continue;
            best = Math.Max(best, Half(p, o) + Gap);
            _ = t;
        }
        return best;
    }

    public static IEnumerable<FurnitureItem> StoryFurniture(Project p, string story) =>
        (p.Arch?.Furniture ?? new List<FurnitureItem>()).Where(f => f.Story == story);

    public static double Norm(double deg)
    {
        deg %= 360; if (deg < 0) deg += 360;
        if (Math.Abs(deg - 360) < 1e-9) deg = 0;
        return Math.Round(deg, 6);
    }

    /// <summary>Đặt đồ đạc (đã bắt điểm) vào mô hình: Id mới, trả về đối tượng.</summary>
    public static FurnitureItem Place(Project p, LibraryItem it, string story, SnapResult s)
    {
        p.Arch ??= new ArchModel();
        var f = Library3D.Place(p, it, story, s.X, s.Y, s.Rotation);
        f.Discipline = it.Discipline;
        return f;
    }
}

/// <summary>Một kích thước tạm: hướng, giá trị (m), đối tượng tham chiếu, đoạn vẽ (m).</summary>
public sealed class RectDim
{
    /// <summary>"+X", "-X", "+Y", "-Y"</summary>
    public string Dir { get; init; } = "";
    public double Value { get; init; }
    /// <summary>"Mặt tường", "Tim tường", "Trục", "Đồ đạc"</summary>
    public string RefKind { get; init; } = "";
    public string RefName { get; init; } = "";
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
    public (double Ux, double Uy) Unit => Dir switch { "+X" => (1, 0), "-X" => (-1, 0), "+Y" => (0, 1), _ => (0, -1) };
    public override string ToString() => $"{Dir} {Value * 1000:0} mm → {RefKind} {RefName}";
}

/// <summary>Kích thước tạm (Revit) cho một hình chữ nhật trên mặt bằng: khoảng hở từ mép tới mặt tường / tim tường / trục / đồ đạc gần nhất theo 4 hướng.</summary>
public static class TempDimensions
{
    public static List<RectDim> ForRect(Project p, string story, double cx, double cy, double width, double depth, double rotDeg,
        bool wallFaces = true, int ignoreFurnId = -1, double maxDist = 30)
    {
        var (hx, hy) = FurnitureSnap.HalfExtents(width, depth, rotDeg);
        var res = new List<RectDim>();
        foreach (var dir in new[] { "+X", "-X", "+Y", "-Y" })
        {
            double ux = dir == "+X" ? 1 : dir == "-X" ? -1 : 0, uy = dir == "+Y" ? 1 : dir == "-Y" ? -1 : 0;
            double h = ux != 0 ? hx : hy;
            double best = double.MaxValue; string kind = "", name = "";
            void Try(double dist, string k, string n)
            {
                double v = dist - h;
                if (v < -1e-6 || dist > maxDist) return;
                if (v < best - 1e-9) { best = v; kind = k; name = n; }
            }
            foreach (var w in FurnitureSnap.StoryWalls(p, story))
            {
                double L = w.Chord, wx = (w.X2 - w.X1) / L, wy = (w.Y2 - w.Y1) / L;
                double den = ux * wy - uy * wx;   // tia × tường
                if (Math.Abs(den) < 1e-9) continue;
                // tâm + s·u = W1 + t·w
                double qx = w.X1 - cx, qy = w.Y1 - cy;
                double s = (qx * wy - qy * wx) / den;
                double t = (qx * uy - qy * ux) / den;
                double half = FurnitureSnap.Half(p, w);
                if (t < -half - 1e-6 || t > L + half + 1e-6 || s <= 1e-9) continue;
                double cosA = Math.Abs(den);   // |sin góc giữa tia và tường| = |cos góc tia – pháp tuyến|
                Try(wallFaces ? s - half / cosA : s, wallFaces ? "Mặt tường" : "Tim tường", $"#{w.Id}");
            }
            foreach (var g in ux != 0 ? p.GridsX : p.GridsY)
            {
                double s = ux != 0 ? (g.Coord - cx) * ux : (g.Coord - cy) * uy;
                if (s > h - 1e-9) Try(s, "Trục", g.Name);
            }
            foreach (var f in FurnitureSnap.StoryFurniture(p, story).Where(q => q.Id != ignoreFurnId))
            {
                var (fx, fy) = FurnitureSnap.HalfExtents(f.Width, f.Depth, f.Rotation);
                if (ux != 0 && Math.Abs(cy - f.Y) < fy + hy - 1e-9) { double s = (f.X - cx) * ux - fx; if (s > 0) Try(s, "Đồ đạc", f.Name); }
                if (uy != 0 && Math.Abs(cx - f.X) < fx + hx - 1e-9) { double s = (f.Y - cy) * uy - fy; if (s > 0) Try(s, "Đồ đạc", f.Name); }
            }
            if (best == double.MaxValue) continue;
            double x1 = cx + ux * h, y1 = cy + uy * h;
            res.Add(new RectDim { Dir = dir, Value = Math.Round(best, 9), RefKind = kind, RefName = name, X1 = x1, Y1 = y1, X2 = x1 + ux * best, Y2 = y1 + uy * best });
        }
        return res;
    }

    /// <summary>Độ dời (dx, dy) để kích thước tạm thành newValue (m): mép tiến lại / lùi xa đối tượng tham chiếu.</summary>
    public static (double Dx, double Dy) MoveFor(RectDim d, double newValue)
    {
        var (ux, uy) = d.Unit;
        double k = d.Value - newValue;
        return (ux * k, uy * k);
    }

    /// <summary>Sửa kích thước tạm của đồ đạc: dời đồ đạc. Trả về false nếu giá trị âm.</summary>
    public static bool Apply(FurnitureItem f, RectDim d, double newValue)
    {
        if (newValue < 0 || double.IsNaN(newValue)) return false;
        var (dx, dy) = MoveFor(d, newValue);
        f.X = Math.Round(f.X + dx, 9); f.Y = Math.Round(f.Y + dy, 9);
        return true;
    }
}

/// <summary>Tia 3D (gốc, hướng đơn vị).</summary>
public readonly record struct Ray3(Vec3 O, Vec3 D);

/// <summary>Chọn đối tượng trong 3D bằng tia từ camera, chiếu chuột lên mặt phẳng ngang (kéo đồ đạc).</summary>
public static class Pick3DMath
{
    /// <summary>Tia từ camera phối cảnh (FieldOfView ngang – như WPF PerspectiveCamera) qua điểm màn hình (px, py) trong khung w × h.</summary>
    public static Ray3 CameraRay(Vec3 pos, Vec3 look, Vec3 up, double fovDeg, double w, double h, double px, double py)
    {
        var f = look.Normalize();
        var r = f.Cross(up).Normalize();
        var u = r.Cross(f).Normalize();
        double t = Math.Tan(fovDeg * Math.PI / 360);
        double nx = 2 * px / Math.Max(w, 1) - 1, ny = 1 - 2 * py / Math.Max(h, 1);
        var d = f + r * (nx * t) + u * (ny * t * h / Math.Max(w, 1));
        return new Ray3(pos, d.Normalize());
    }

    /// <summary>Khoảng cách tới khối (tia cắt hộp bao địa phương của khối lăng trụ); null = không cắt.</summary>
    public static double? Hit(Ray3 ray, Solid s)
    {
        if (s == null || s.Profile.Count == 0) return null;
        var X = s.XDir.Normalize(); var Z = s.ZDir.Normalize(); var Y = Z.Cross(X).Normalize();
        var o = ray.O - s.Origin;
        double[] ro = { o.Dot(X), o.Dot(Y), o.Dot(Z) };
        double[] rd = { ray.D.Dot(X), ray.D.Dot(Y), ray.D.Dot(Z) };
        double[] lo = { s.Profile.Min(q => q[0]), s.Profile.Min(q => q[1]), Math.Min(0, s.Depth) };
        double[] hi = { s.Profile.Max(q => q[0]), s.Profile.Max(q => q[1]), Math.Max(0, s.Depth) };
        double t0 = double.NegativeInfinity, t1 = double.PositiveInfinity;
        for (int i = 0; i < 3; i++)
        {
            if (Math.Abs(rd[i]) < 1e-12)
            {
                if (ro[i] < lo[i] || ro[i] > hi[i]) return null;
                continue;
            }
            double a = (lo[i] - ro[i]) / rd[i], b = (hi[i] - ro[i]) / rd[i];
            if (a > b) (a, b) = (b, a);
            t0 = Math.Max(t0, a); t1 = Math.Min(t1, b);
            if (t0 > t1) return null;
        }
        if (t1 < 0) return null;
        return t0 >= 0 ? t0 : t1;
    }

    /// <summary>Khối gần nhất bị tia cắt (bỏ khối lỗ khoét).</summary>
    public static Solid Pick(Ray3 ray, IEnumerable<Solid> solids, Func<Solid, bool> filter = null)
    {
        Solid best = null; double bd = double.MaxValue;
        foreach (var s in solids)
        {
            if (s.IsVoid || (filter != null && !filter(s))) continue;
            var d = Hit(ray, s);
            if (d is double v && v < bd) { bd = v; best = s; }
        }
        return best;
    }

    /// <summary>Giao tia với mặt phẳng ngang z = const; null nếu song song / phía sau.</summary>
    public static (double X, double Y)? OnPlaneZ(Ray3 ray, double z)
    {
        if (Math.Abs(ray.D.Z) < 1e-9) return null;
        double t = (z - ray.O.Z) / ray.D.Z;
        if (t < 0) return null;
        return (ray.O.X + ray.D.X * t, ray.O.Y + ray.D.Y * t);
    }

    /// <summary>Kéo trên mặt phẳng ngang: điểm mới = điểm gốc + (giao tia hiện tại − giao tia lúc bấm).</summary>
    public static (double X, double Y)? Drag(Ray3 start, Ray3 now, double z, double x0, double y0)
    {
        var a = OnPlaneZ(start, z); var b = OnPlaneZ(now, z);
        if (a == null || b == null) return null;
        return (x0 + b.Value.X - a.Value.X, y0 + b.Value.Y - a.Value.Y);
    }
}

/// <summary>Ẩn / cô lập đối tượng trong khung 3D (theo Id khối: "DD-3", "DAM-5"…).</summary>
public sealed class View3DFilter
{
    public HashSet<string> Hidden { get; } = new();
    public HashSet<string> Isolated { get; } = new();
    public bool IsIsolating => Isolated.Count > 0;
    public bool Visible(string id) => !Hidden.Contains(id) && (Isolated.Count == 0 || Isolated.Contains(id));
    public void Isolate(IEnumerable<string> ids) { Isolated.Clear(); foreach (var i in ids) Isolated.Add(i); }
    public void Hide(IEnumerable<string> ids) { foreach (var i in ids) Hidden.Add(i); Isolated.ExceptWith(Hidden); }
    public void Reset() { Hidden.Clear(); Isolated.Clear(); }
}

/// <summary>Khung nhìn mặt bằng cắt ngang tầng (tự sinh theo cao độ, mỗi bộ môn một khung).</summary>
public sealed class PlanViewDef
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>Tên tầng ("NEN" = nền tầng 1)</summary>
    public string Story { get; init; } = "";
    public Discipline Discipline { get; init; }
    public double Elevation { get; init; }
    /// <summary>Mặt phẳng cắt cao hơn sàn (m)</summary>
    public double CutHeight { get; init; } = 1.2;
    public override string ToString() => Name;
}

public static class LevelViews
{
    /// <summary>Mặt cắt ngang các tầng: nền tầng 1 + mọi sàn tầng / mái / tum, × 3 bộ môn (KT, KC, DN). Sinh lại theo cao độ hiện tại.</summary>
    public static List<PlanViewDef> Build(Project p, IEnumerable<Discipline> disciplines = null)
    {
        var ds = (disciplines ?? DisciplineView.All).ToList();
        var res = new List<PlanViewDef>();
        foreach (var lv in Levels.All(p).Where(l => l.Kind is LevelKind.Nen or LevelKind.San or LevelKind.Mai or LevelKind.Tum))
        {
            string story = lv.Kind == LevelKind.Nen ? "NEN" : lv.Name;
            string elev = (lv.Elevation >= 0 ? "+" : "") + lv.Elevation.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
            if (Math.Abs(lv.Elevation) < 1e-9) elev = "±0.000";
            string title = lv.Kind == LevelKind.Nen ? "Tầng 1" : lv.Title.Replace("Sàn ", "");
            foreach (var d in ds)
                res.Add(new PlanViewDef
                {
                    Key = $"PV:{DisciplineView.Prefix(d)}:{story}", Discipline = d, Story = story, Elevation = lv.Elevation,
                    Name = $"{DisciplineView.Prefix(d)} – Mặt cắt ngang {title.ToLowerInvariant()} ({elev})"
                });
        }
        return res;
    }
}

/// <summary>Đường cắt đứng (mặt cắt công trình, kiểu Revit): tên, hai điểm trên mặt bằng (m). Auto = mặt cắt tự sinh 1-1 / 2-2.</summary>
public sealed class SectionLine
{
    public string Name { get; set; } = "1-1";
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool Auto { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public double Length => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
}
