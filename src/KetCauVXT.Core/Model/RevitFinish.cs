namespace KetCauVXT.Model;

// =====================================================================
// HOÀN THIỆN GIAO DIỆN KIỂU REVIT (phần lõi, kiểm chứng được):
//   • WallSolid3D – khối tường 3D quanh ô cửa (dưới bậu, trên đầu cửa, hai bên);
//   • SnapshotHistory – hoàn tác / làm lại (Ctrl+Z / Ctrl+Y) bằng ảnh chụp JSON;
//   • WallGrips – kéo đầu tường / dời tường bằng tay nắm, cửa gắn tường giữ vị trí tuyệt đối và chặn trong tường;
//   • PlanEditOps – sao chép / dời / đối xứng / mảng / offset tường và dầm theo điểm bấm chuột (lệnh CO, M, MI, AR, O);
//   • PlanLabelRules – chữ nhãn mặt bằng giữ cỡ trên màn hình, ẩn khi thu nhỏ quá mức;
//   • HitCycle – phím Tab chuyển vòng giữa các đối tượng chồng nhau dưới con trỏ.
// =====================================================================

/// <summary>Một khối tường 3D: đoạn [A, B] (m dọc tường từ đầu tường), cao độ tương đối Z0…Z1 (m, tính từ chân tường).</summary>
public readonly record struct WallPiece(double A, double B, double Z0, double Z1)
{
    public double Length => B - A;
    public double Height => Z1 - Z0;
}

public static class WallSolid3D
{
    /// <summary>Chiều cao tường mặc định như khi tính tải: H khai báo, hoặc chiều cao tầng − max(h dầm, 0,35 m), tầng trên cùng 0,9 m (tường chắn mái).</summary>
    public static double DefaultHeight(Project p, WallLine w, double beamDepth = 0.35)
    {
        if (w.Height > 0) return w.Height;
        var stories = p.Stories.OrderBy(s => s.Elevation).ToList();
        int idx = stories.FindIndex(s => s.Name == w.Story);
        if (idx < 0) return 3.0;
        return idx + 1 < stories.Count ? Math.Max(0.1, stories[idx + 1].Elevation - stories[idx].Elevation - Math.Max(beamDepth, 0.35)) : 0.9;
    }

    /// <summary>Các khối tường đặc quanh ô cửa: phần tường liền (đủ cao H) ngoài các ô cửa, phần dưới bậu (0…bậu)
    /// và phần trên đầu cửa (bậu + cao cửa … H) trong phạm vi mỗi ô cửa. Cửa cao tới đỉnh tường → không có khối trên.</summary>
    public static List<WallPiece> Pieces(Project p, WallLine w, double H)
    {
        var res = new List<WallPiece>();
        double L = w.IsCurved ? w.Length : w.Chord;
        if (L < 1e-6 || H <= 1e-6) return res;
        var ops = (w.Openings ?? new List<WallOpening>())
            .Select(o => (o, t: WallAssembly.TypeOf(p, o)))
            .Where(q => q.t != null)
            .Select(q =>
            {
                var (a, b) = WallAssembly.Span(p, q.o);
                return (A: Math.Max(0, a), B: Math.Min(L, b), Sill: Math.Clamp(q.t.Sill, 0, H), Head: Math.Clamp(q.t.Sill + q.t.Height, 0, H));
            })
            .Where(q => q.B > q.A + 1e-6 && q.Head > q.Sill + 1e-6)
            .OrderBy(q => q.A).ToList();
        double cur = 0;
        foreach (var o in ops)
        {
            if (o.A > cur + 1e-6) res.Add(new WallPiece(cur, o.A, 0, H));
            double a = Math.Max(o.A, cur);
            if (o.B > a + 1e-6)
            {
                if (o.Sill > 1e-6) res.Add(new WallPiece(a, o.B, 0, o.Sill));
                if (o.Head < H - 1e-6) res.Add(new WallPiece(a, o.B, o.Head, H));
            }
            cur = Math.Max(cur, o.B);
        }
        if (L > cur + 1e-6) res.Add(new WallPiece(cur, L, 0, H));
        return res;
    }

    /// <summary>Thể tích các khối (m³) với chiều dày t.</summary>
    public static double Volume(IEnumerable<WallPiece> pieces, double t) => pieces.Sum(q => q.Length * q.Height) * t;
}

/// <summary>Hoàn tác / làm lại bằng ảnh chụp JSON của dự án. undo, redo = danh sách (phần tử cuối = đỉnh ngăn xếp).</summary>
public static class SnapshotHistory
{
    /// <summary>Thao tác sửa mới: lưu ảnh TRƯỚC khi sửa, xoá ngăn làm lại.</summary>
    public static void Push(List<string> undo, List<string> redo, string before, int max = 30)
    {
        undo.Add(before);
        while (undo.Count > max) undo.RemoveAt(0);
        redo.Clear();
    }

    /// <summary>Hoàn tác: trả về ảnh cần nạp (null nếu hết); ảnh hiện tại chuyển sang ngăn làm lại.</summary>
    public static string Undo(List<string> undo, List<string> redo, string current, int max = 30)
    {
        if (undo.Count == 0) return null;
        string s = undo[^1];
        undo.RemoveAt(undo.Count - 1);
        redo.Add(current);
        while (redo.Count > max) redo.RemoveAt(0);
        return s;
    }

    /// <summary>Làm lại: trả về ảnh cần nạp (null nếu hết); ảnh hiện tại quay lại ngăn hoàn tác.</summary>
    public static string Redo(List<string> undo, List<string> redo, string current, int max = 30)
    {
        if (redo.Count == 0) return null;
        string s = redo[^1];
        redo.RemoveAt(redo.Count - 1);
        undo.Add(current);
        while (undo.Count > max) undo.RemoveAt(0);
        return s;
    }
}

/// <summary>Tay nắm (grip) tường: kéo một đầu, dời cả tường. Cửa gắn tường giữ vị trí tuyệt đối (chiếu lên tim tường mới) rồi chặn trong tường.</summary>
public static class WallGrips
{
    public const double MinLength = 0.10;

    /// <summary>Kéo đầu tường (end2 = false: đầu 1, true: đầu 2) tới (x, y). Không cho tường ngắn hơn 0,1 m hoặc không còn đủ chỗ cho cửa đã gắn.</summary>
    public static EditResult MoveEnd(Project p, WallLine w, bool end2, double x, double y)
    {
        if (w == null) return EditResult.Fail("Chưa chọn tường.");
        if (w.IsCurved) return EditResult.Fail("Tường cong: dùng lệnh sửa tường cong.");
        // vị trí tuyệt đối của các cửa trước khi kéo
        var abs = (w.Openings ?? new List<WallOpening>()).Select(o => (o, pt: WallAssembly.PointAt(w, o.Offset))).ToList();
        double ox1 = w.X1, oy1 = w.Y1, ox2 = w.X2, oy2 = w.Y2;
        if (end2) { w.X2 = Math.Round(x, 4); w.Y2 = Math.Round(y, 4); } else { w.X1 = Math.Round(x, 4); w.Y1 = Math.Round(y, 4); }
        if (w.Chord < MinLength)
        {
            w.X1 = ox1; w.Y1 = oy1; w.X2 = ox2; w.Y2 = oy2;
            return EditResult.Fail($"Tường phải dài tối thiểu {MinLength * 1000:0} mm.");
        }
        var old = abs.Select(q => (q.o, q.o.Offset)).ToList();
        foreach (var (o, pt) in abs)
        {
            double t = WallAssembly.ProjectPoint(w, pt.X, pt.Y, out _);
            double wd = WallAssembly.TypeOf(p, o)?.Width ?? 0.9;
            double c = WallAssembly.ClampOffset(w, wd, t);
            if (double.IsNaN(c))
            {
                w.X1 = ox1; w.Y1 = oy1; w.X2 = ox2; w.Y2 = oy2;
                foreach (var (oo, off) in old) oo.Offset = off;
                return EditResult.Fail($"Tường quá ngắn cho cửa {o.Type} – xoá hoặc dời cửa trước.");
            }
            o.Offset = Math.Round(c, 4);
        }
        // tách các cửa chồng nhau sau khi chặn
        foreach (var o in (w.Openings ?? new List<WallOpening>()).OrderBy(q => q.Offset).ToList())
        {
            double wd = WallAssembly.TypeOf(p, o)?.Width ?? 0.9;
            if (!WallAssembly.Overlaps(p, w, wd, o.Offset, o.Id)) continue;
            double f = WallAssembly.FreeOffset(p, w, wd, o.Offset, o.Id);
            if (double.IsNaN(f))
            {
                w.X1 = ox1; w.Y1 = oy1; w.X2 = ox2; w.Y2 = oy2;
                foreach (var (oo, off) in old) oo.Offset = off;
                return EditResult.Fail("Tường quá ngắn cho các cửa đã gắn – xoá bớt cửa trước.");
            }
            o.Offset = Math.Round(f, 4);
        }
        return new EditResult { Ok = true, Message = $"Đã kéo đầu tường #{w.Id}: dài {w.Chord * 1000:0} mm" + (abs.Count > 0 ? $", {abs.Count} cửa giữ vị trí." : ".") };
    }

    /// <summary>Dời cả tường (cửa đi theo tường).</summary>
    public static EditResult Move(WallLine w, double dx, double dy)
    {
        if (w == null) return EditResult.Fail("Chưa chọn tường.");
        w.X1 = Math.Round(w.X1 + dx, 4); w.Y1 = Math.Round(w.Y1 + dy, 4);
        w.X2 = Math.Round(w.X2 + dx, 4); w.Y2 = Math.Round(w.Y2 + dy, 4);
        return new EditResult { Ok = true, Message = $"Đã dời tường #{w.Id} ({dx * 1000:0}; {dy * 1000:0}) mm." };
    }
}

/// <summary>Thao tác sửa theo điểm bấm (lệnh CO / M / MI / AR / O kiểu AutoCAD – Revit) cho tường và dầm.</summary>
public static class PlanEditOps
{
    /// <summary>Đối xứng điểm qua đường thẳng đi qua A, B.</summary>
    public static (double X, double Y) MirrorPoint(double x, double y, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay, L2 = dx * dx + dy * dy;
        if (L2 < 1e-18) return (x, y);
        double t = ((x - ax) * dx + (y - ay) * dy) / L2;
        double px = ax + t * dx, py = ay + t * dy;
        return (Math.Round(2 * px - x, 6), Math.Round(2 * py - y, 6));
    }

    /// <summary>Phía của điểm so với đường thẳng A→B: +1 bên trái, −1 bên phải, 0 trên đường.</summary>
    public static int Side(double ax, double ay, double bx, double by, double px, double py)
    {
        double c = (bx - ax) * (py - ay) - (by - ay) * (px - ax);
        return c > 1e-12 ? 1 : c < -1e-12 ? -1 : 0;
    }

    private static int NextWallId(Project p) => p.Walls.Select(w => w.Id).DefaultIfEmpty(0).Max() + 1;
    private static int NextOpeningId(Project p) => p.Walls.SelectMany(w => w.Openings ?? new List<WallOpening>()).Select(o => o.Id).DefaultIfEmpty(0).Max() + 1;

    private static WallLine CloneWall(Project p, WallLine w, double x1, double y1, double x2, double y2, bool flipOpenings)
    {
        var n = new WallLine
        {
            Id = NextWallId(p), Story = w.Story, Type = w.Type, Height = w.Height, OpeningArea = w.OpeningArea, Note = w.Note, Sagitta = flipOpenings ? -w.Sagitta : w.Sagitta,
            X1 = Math.Round(x1, 4), Y1 = Math.Round(y1, 4), X2 = Math.Round(x2, 4), Y2 = Math.Round(y2, 4)
        };
        p.Walls.Add(n);
        foreach (var o in w.Openings ?? new List<WallOpening>())
            n.Openings.Add(new WallOpening { Id = NextOpeningId(p), Type = o.Type, Offset = o.Offset, Flip = flipOpenings ? !o.Flip : o.Flip, FlipHand = flipOpenings ? !o.FlipHand : o.FlipHand });
        return n;
    }

    private static bool SameWallExists(Project p, WallLine src, double x1, double y1, double x2, double y2)
    {
        bool Eq(double a, double b) => Math.Abs(a - b) < 1e-4;
        return p.Walls.Any(w => w.Story == src.Story && ((Eq(w.X1, x1) && Eq(w.Y1, y1) && Eq(w.X2, x2) && Eq(w.Y2, y2)) || (Eq(w.X1, x2) && Eq(w.Y1, y2) && Eq(w.X2, x1) && Eq(w.Y2, y1))));
    }

    /// <summary>Sao chép tường k·(dx, dy), k = 1…copies (cửa gắn tường chép theo). Bỏ qua vị trí đã có tường trùng.</summary>
    public static EditResult CopyWalls(Project p, IEnumerable<int> ids, double dx, double dy, int copies = 1)
    {
        if (Math.Abs(dx) + Math.Abs(dy) < 1e-6) return EditResult.Fail("Khoảng sao chép phải khác 0.");
        if (copies < 1 || copies > 100) return EditResult.Fail("Số bản sao từ 1 đến 100.");
        var src = p.Walls.Where(w => ids.Contains(w.Id)).ToList();
        if (src.Count == 0) return EditResult.Fail("Chưa chọn tường.");
        int ok = 0, skip = 0;
        for (int k = 1; k <= copies; k++)
            foreach (var w in src)
            {
                double x1 = w.X1 + k * dx, y1 = w.Y1 + k * dy, x2 = w.X2 + k * dx, y2 = w.Y2 + k * dy;
                if (SameWallExists(p, w, x1, y1, x2, y2)) { skip++; continue; }
                CloneWall(p, w, x1, y1, x2, y2, false); ok++;
            }
        return new EditResult { Ok = ok > 0, Message = ok > 0 ? $"Đã sao chép {src.Count} tường × {copies}: thêm {ok} tường" + (skip > 0 ? $", bỏ qua {skip} (trùng)." : ".") : "Vị trí đích đã có tường." };
    }

    /// <summary>Dời các tường (dx, dy).</summary>
    public static EditResult MoveWalls(Project p, IEnumerable<int> ids, double dx, double dy)
    {
        var src = p.Walls.Where(w => ids.Contains(w.Id)).ToList();
        if (src.Count == 0) return EditResult.Fail("Chưa chọn tường.");
        foreach (var w in src) WallGrips.Move(w, dx, dy);
        return new EditResult { Ok = true, Message = $"Đã dời {src.Count} tường ({dx * 1000:0}; {dy * 1000:0}) mm." };
    }

    /// <summary>Đối xứng tường qua đường A–B (keepOriginal = false → lật, xoá tường gốc). Cửa đổi hướng mở theo.</summary>
    public static EditResult MirrorWalls(Project p, IEnumerable<int> ids, double ax, double ay, double bx, double by, bool keepOriginal = true)
    {
        if (Math.Abs(bx - ax) + Math.Abs(by - ay) < 1e-6) return EditResult.Fail("Trục đối xứng cần 2 điểm khác nhau.");
        var src = p.Walls.Where(w => ids.Contains(w.Id)).ToList();
        if (src.Count == 0) return EditResult.Fail("Chưa chọn tường.");
        int ok = 0, skip = 0;
        foreach (var w in src)
        {
            var (x1, y1) = MirrorPoint(w.X1, w.Y1, ax, ay, bx, by);
            var (x2, y2) = MirrorPoint(w.X2, w.Y2, ax, ay, bx, by);
            if (keepOriginal && SameWallExists(p, w, x1, y1, x2, y2)) { skip++; continue; }
            CloneWall(p, w, x1, y1, x2, y2, true); ok++;
        }
        if (!keepOriginal) foreach (var w in src) p.Walls.Remove(w);
        return new EditResult { Ok = ok > 0, Message = ok > 0 ? $"Đã đối xứng {ok} tường" + (keepOriginal ? "" : " (xoá tường gốc)") + (skip > 0 ? $", bỏ qua {skip} (nằm trên trục)." : ".") : "Không đối xứng được (tường nằm trên trục đối xứng)." };
    }

    /// <summary>Offset tường song song cách distance (m) về phía điểm (px, py).</summary>
    public static EditResult OffsetWall(Project p, WallLine w, double distance, double px, double py)
    {
        if (w == null) return EditResult.Fail("Chưa chọn tường.");
        if (distance <= 1e-4) return EditResult.Fail("Khoảng offset phải > 0.");
        double dx = w.X2 - w.X1, dy = w.Y2 - w.Y1, L = Math.Sqrt(dx * dx + dy * dy);
        if (L < 1e-6) return EditResult.Fail("Tường quá ngắn.");
        int side = Side(w.X1, w.Y1, w.X2, w.Y2, px, py);
        if (side == 0) return EditResult.Fail("Bấm điểm lệch khỏi tim tường để chọn phía offset.");
        double nx = -dy / L * distance * side, ny = dx / L * distance * side;
        if (SameWallExists(p, w, w.X1 + nx, w.Y1 + ny, w.X2 + nx, w.Y2 + ny)) return EditResult.Fail("Vị trí offset đã có tường.");
        var n = CloneWall(p, w, w.X1 + nx, w.Y1 + ny, w.X2 + nx, w.Y2 + ny, false);
        n.Openings.Clear();   // offset không chép cửa (như Revit)
        return new EditResult { Ok = true, Message = $"Đã offset tường #{w.Id} cách {distance * 1000:0} mm → tường #{n.Id}." };
    }

    /// <summary>Khoảng cách có dấu từ điểm (px, py) tới tim dầm A→B (dương: bên trái hướng A→B) – dùng cho offset dầm theo điểm bấm.</summary>
    public static double SignedDistance(double ax, double ay, double bx, double by, double px, double py)
    {
        double dx = bx - ax, dy = by - ay, L = Math.Sqrt(dx * dx + dy * dy);
        if (L < 1e-12) return 0;
        return ((px - ax) * -dy + (py - ay) * dx) / L;
    }

    /// <summary>Đối xứng dầm qua đường A–B bất kỳ (trục song song X / Y → dùng BeamTools.Mirror giữ góc xoay, điểm chèn).</summary>
    public static EditResult MirrorBeams(Project p, IEnumerable<int> ids, double ax, double ay, double bx, double by, bool keepOriginal = true)
    {
        if (Math.Abs(bx - ax) + Math.Abs(by - ay) < 1e-6) return EditResult.Fail("Trục đối xứng cần 2 điểm khác nhau.");
        var list = ids.Distinct().ToList();
        if (Math.Abs(bx - ax) < 1e-6) return BeamTools.Mirror(p, list, GridDir.X, ax, keepOriginal);
        if (Math.Abs(by - ay) < 1e-6) return BeamTools.Mirror(p, list, GridDir.Y, ay, keepOriginal);
        var src = p.Frames.Where(f => list.Contains(f.Id) && !f.IsVerticalKind).ToList();
        if (src.Count == 0) return EditResult.Fail("Chưa chọn dầm để đối xứng.");
        var work = new List<(FrameElement f, string st, double x1, double y1, double x2, double y2)>();
        foreach (var f in src)
        {
            var a = p.Nodes.FirstOrDefault(n => n.Id == f.NodeI); var b = p.Nodes.FirstOrDefault(n => n.Id == f.NodeJ);
            string st = BeamEdit.StoryOf(p, f);
            if (a == null || b == null || st == null) continue;
            var (x1, y1) = MirrorPoint(a.X, a.Y, ax, ay, bx, by);
            var (x2, y2) = MirrorPoint(b.X, b.Y, ax, ay, bx, by);
            work.Add((f, st, x1, y1, x2, y2));
        }
        var res = new EditResult();
        if (!keepOriginal) { foreach (var w in work) ModelEdit.DeleteFrame(p, w.f); p.InvalidateIndex(); }
        int ok = 0, skip = 0;
        foreach (var w in work)
        {
            var e = BeamTools.AddBeamExact(p, w.st, w.x1, w.y1, w.x2, w.y2, w.f.Section, "M");
            if (e.Ok) { ok++; res.NewFrames.AddRange(e.NewFrames); } else skip++;
        }
        res.Ok = ok > 0 || !keepOriginal;
        res.Message = ok > 0 ? $"Đã đối xứng {work.Count} dầm qua trục nghiêng: thêm {ok} dầm" + (skip > 0 ? $", bỏ qua {skip}." : ".") : "Không đối xứng được dầm nào (đã có dầm tại vị trí đối xứng).";
        return res;
    }

    /// <summary>Mảng thẳng: n phần tử (tính cả bản gốc) cách đều vector (dx, dy) giữa 2 phần tử liền nhau.</summary>
    public static (double Dx, double Dy, int Copies) LinearArray(double dx, double dy, int count) => (dx, dy, Math.Max(0, count - 1));

    /// <summary>Mảng "điểm cuối": n phần tử trải đều từ gốc tới vector (dx, dy) (như Revit "Move to: Last").</summary>
    public static (double Dx, double Dy, int Copies) LastArray(double dx, double dy, int count) =>
        count < 2 ? (0, 0, 0) : (dx / (count - 1), dy / (count - 1), count - 1);
}

/// <summary>Quy tắc nhãn mặt bằng: cỡ chữ cố định trên màn hình, ẩn nhãn cấu kiện khi thu nhỏ dưới ngưỡng.</summary>
public static class PlanLabelRules
{
    /// <summary>Ngưỡng tỷ lệ (px/m) dưới đó ẩn nhãn cấu kiện (trục, tiêu đề vẫn hiện).</summary>
    public const double DefaultMinScale = 6;

    /// <summary>Chiều cao chữ quy ra mét thế giới để giữ cỡ fontPx trên màn hình ở tỷ lệ scale (px/m).</summary>
    public static double WorldHeight(double fontPx, double scale) => scale <= 1e-9 ? 0 : fontPx / scale;

    /// <summary>Cỡ chữ trên màn hình: luôn = cỡ đặt (không phóng theo zoom).</summary>
    public static double ScreenFont(double fontPx, double scale) => fontPx;

    /// <summary>Nhãn có được vẽ không: nhãn cấu kiện (member = true) ẩn khi scale &lt; minScale.</summary>
    public static bool Visible(double scale, bool member, double minScale = DefaultMinScale) => !member || scale >= minScale;
}

/// <summary>Phím Tab chuyển vòng giữa các đối tượng chồng nhau dưới con trỏ (như Revit).</summary>
public static class HitCycle
{
    /// <summary>Chỉ số ứng viên kế tiếp (vòng lại đầu); -1 nếu không có ứng viên.</summary>
    public static int Next(int count, int current) => count <= 0 ? -1 : current < 0 ? 0 : (current + 1) % count;

    /// <summary>Hai danh sách ứng viên có giống nhau (cùng đối tượng, cùng thứ tự) – để giữ chỉ số Tab khi chuột nhích ít.</summary>
    public static bool Same(IReadOnlyList<string> a, IReadOnlyList<string> b) => a != null && b != null && a.Count == b.Count && a.SequenceEqual(b);
}
