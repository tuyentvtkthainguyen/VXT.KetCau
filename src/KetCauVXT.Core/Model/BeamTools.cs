using System.Globalization;
using System.Text.Json;
using KetCauVXT.Import;

namespace KetCauVXT.Model;

// =====================================================================
// CÔNG CỤ KHAI BÁO / VẼ DẦM KIỂU ETABS (lõi, kiểm thử được – giao diện ở App/MainWindow.BeamTools.cs):
//   • nhập toạ độ kiểu AutoCAD/ETABS: "x,y" tuyệt đối, "@dx,dy" tương đối, "L<góc" cực, "L" theo hướng chuột (đơn vị mm/m);
//   • bắt điểm: đầu dầm, nút, giao trục, giao điểm, trung điểm, chân đường vuông góc, điểm gần nhất trên dầm, trên trục;
//   • vẽ nhanh dầm theo đoạn trục (Quick Draw Beams), dầm phụ theo số lượng / khoảng cách / danh sách khoảng cách,
//     dầm theo trục + khoảng cách, dầm công xôn, sao chép tịnh tiến / sang tầng khác, đối xứng qua trục;
//   • phạm vi tầng: tầng hiện tại / các tầng tương tự (cùng mặt bằng dầm – cột) / tất cả / tầng đánh dấu;
//   • điểm chèn (cardinal point) và dời ngang tiết diện trên mặt bằng (chỉ bản vẽ + xuất ETABS, phân tích bỏ qua);
//   • thư viện tiết diện (SectionLibrary): thêm, sao chép, sửa, đổi tên, xoá có bảo vệ / gán lại, nhập từ dự án khác.
// Mọi thao tác trả về EditResult như ModelEdit; nút được tạo/chia đúng (GetOrAddNode + SplitFramesAtNodes),
// dầm phụ cắt ô sàn thì ô sàn được chia lại theo dầm (giữ tiết diện, cấu tạo, tải trên sàn).
// =====================================================================

/// <summary>Điểm chèn tiết diện dầm (ETABS "Cardinal point"): điểm của tiết diện nằm trên đường nối hai nút.
/// Trái/phải theo hướng I→J nhìn từ trên xuống; trên/dưới theo phương đứng. Giá trị 0 = trọng tâm (mặc định, file cũ).</summary>
public enum BeamInsertion { Centroid = 0, TopCenter, TopLeft, TopRight, MiddleLeft, MiddleRight, BottomCenter, BottomLeft, BottomRight }

/// <summary>Đơn vị số nhập khi gõ toạ độ (số có hậu tố "mm", "cm", "m" thì theo hậu tố).</summary>
public enum CoordUnit { Mm, M }

public enum CoordInputKind { Absolute, Relative, Polar, Distance }

/// <summary>Kết quả phân tích chuỗi toạ độ nhập tay (m).</summary>
public class CoordInput
{
    public bool Ok { get; set; }
    public string Error { get; set; } = "";
    public CoordInputKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public static CoordInput Fail(string e) => new() { Ok = false, Error = e };
}

/// <summary>Loại điểm bắt (thứ tự ưu tiên: đầu dầm/nút → giao trục → giao điểm → trung điểm → vuông góc → gần nhất → trên trục → lưới 50 mm).</summary>
public enum SnapKind { None, End, Node, GridIntersection, Intersection, Midpoint, Perpendicular, Nearest, GridLine, Round,
    /* [CadLink] nét bản vẽ liên kết – Import/Link/CadLinkGeometry.cs */ CadEnd, CadMid, CadIntersection, CadCenter }

[Flags]
public enum SnapMask
{
    None = 0, End = 1, Node = 2, GridIntersection = 4, Intersection = 8, Midpoint = 16, Perpendicular = 32, Nearest = 64, GridLine = 128,
    /// <summary>[CadLink] bắt điểm vào nét bản vẽ liên kết (đầu nét / trung điểm / giao nét theo các cờ End, Midpoint, Intersection)</summary>
    CadLink = 256,
    All = End | Node | GridIntersection | Intersection | Midpoint | Perpendicular | Nearest | GridLine | CadLink
}

public class SnapCandidate
{
    public SnapKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Khoảng cách tới con trỏ, m</summary>
    public double Dist { get; set; }
    public int Priority { get; set; }
    public override string ToString() => $"{BeamTools.SnapName(Kind)} ({X:0.###}; {Y:0.###})";
}

/// <summary>Đoạn trục giữa hai giao trục liền kề (đơn vị "ô lưới" của Quick Draw Beams).</summary>
public class GridSegment
{
    public GridLine Line { get; set; }
    public string FromGrid { get; set; } = "";
    public string ToGrid { get; set; } = "";
    /// <summary>Toạ độ dọc trục của hai đầu đoạn (Y với trục theo X, X với trục theo Y), m; From &lt; To.</summary>
    public double From { get; set; }
    public double To { get; set; }
    public double X1 => Line.Dir == GridDir.X ? Line.Coord : From;
    public double Y1 => Line.Dir == GridDir.X ? From : Line.Coord;
    public double X2 => Line.Dir == GridDir.X ? Line.Coord : To;
    public double Y2 => Line.Dir == GridDir.X ? To : Line.Coord;
    public double Length => To - From;
    public override string ToString() => $"trục {Line.Name}: {FromGrid}–{ToGrid}";
}

/// <summary>Phạm vi tầng áp dụng cho công cụ vẽ (như "Plan / Similar Stories / All Stories" của ETABS).</summary>
public enum StoryScope { Current, Similar, All, Selected }

/// <summary>Tuỳ chọn dầm phụ trong một ô (Quick Draw Secondary Beams).</summary>
public class SecondaryBeamOptions
{
    /// <summary>true = dầm phụ song song trục X (đường y = const, khoảng cách đo theo Y); false = song song trục Y.</summary>
    public bool AlongX { get; set; } = true;
    /// <summary>Số dầm chia đều (≥ 1). Dùng khi Spacing = 0 và Distances rỗng.</summary>
    public int Count { get; set; } = 1;
    /// <summary>Khoảng cách đều giữa các dầm, m (0 = dùng Count).</summary>
    public double Spacing { get; set; }
    /// <summary>Khoảng cách dầm đầu tiên tới mốc, m (0 = bằng Spacing). Chỉ dùng với Spacing.</summary>
    public double StartOffset { get; set; }
    /// <summary>Đo từ mép cuối ô (đảo chiều / đối xứng) thay vì mép đầu – khi không có trục mốc.</summary>
    public bool FromEnd { get; set; }
    /// <summary>Danh sách khoảng cách từ mốc (trục RefGrid hoặc mép ô), m. Ưu tiên hơn Count/Spacing.</summary>
    public List<double> Distances { get; set; } = new();
    /// <summary>Tên trục mốc song song dầm phụ ("" = mép ô).</summary>
    public string RefGrid { get; set; } = "";
    public string Section { get; set; } = "";
    /// <summary>Chia lại ô sàn bị dầm phụ cắt qua (để tải sàn truyền vào dầm phụ như ETABS).</summary>
    public bool SplitSlabs { get; set; } = true;
}

public static class BeamTools
{
    /// <summary>Chiều dài dầm nhỏ nhất tạo bằng các công cụ này, m.</summary>
    public const double MinLength = 0.1;

    private static double R4(double v) => Math.Round(v, 4);
    private static double Hyp(double dx, double dy) => Math.Sqrt(dx * dx + dy * dy);

    // ==================================================================
    // Điểm chèn
    // ==================================================================
    /// <summary>Số "cardinal point" của ETABS (1…11; 10 = trọng tâm).</summary>
    public static int EtabsCardinal(BeamInsertion k) => k switch
    {
        BeamInsertion.BottomLeft => 1, BeamInsertion.BottomCenter => 2, BeamInsertion.BottomRight => 3,
        BeamInsertion.MiddleLeft => 4, BeamInsertion.MiddleRight => 6,
        BeamInsertion.TopLeft => 7, BeamInsertion.TopCenter => 8, BeamInsertion.TopRight => 9,
        _ => 10
    };

    public static string InsertionName(BeamInsertion k) => k switch
    {
        BeamInsertion.TopCenter => "Giữa mặt trên (8)", BeamInsertion.TopLeft => "Mép trái – mặt trên (7)", BeamInsertion.TopRight => "Mép phải – mặt trên (9)",
        BeamInsertion.MiddleLeft => "Mép trái (4)", BeamInsertion.MiddleRight => "Mép phải (6)",
        BeamInsertion.BottomCenter => "Giữa mặt dưới (2)", BeamInsertion.BottomLeft => "Mép trái – mặt dưới (1)", BeamInsertion.BottomRight => "Mép phải – mặt dưới (3)",
        _ => "Trọng tâm (10)"
    };

    private static bool IsLeft(BeamInsertion k) => k is BeamInsertion.TopLeft or BeamInsertion.MiddleLeft or BeamInsertion.BottomLeft;
    private static bool IsRight(BeamInsertion k) => k is BeamInsertion.TopRight or BeamInsertion.MiddleRight or BeamInsertion.BottomRight;

    /// <summary>Độ lệch của tim tiết diện so với đường nối nút trên mặt bằng, m (dương = sang trái hướng I→J):
    /// PlanOffset + (−b/2 nếu điểm chèn ở mép trái, +b/2 nếu ở mép phải).</summary>
    public static double LateralOffset(Project p, FrameElement f)
    {
        if (f == null) return 0;
        double off = f.PlanOffset;
        if (f.Insertion != BeamInsertion.Centroid && (IsLeft(f.Insertion) || IsRight(f.Insertion)))
        {
            var sec = p.FrameSections.FirstOrDefault(s => s.Name == f.Section);
            double b = sec == null ? 0 : sec.Shape == SectionShape.RectConcrete ? sec.B : sec.Props(0.5).Width;
            off += IsLeft(f.Insertion) ? -b / 2 : b / 2;
        }
        return off;
    }

    /// <summary>Độ lệch đứng của trọng tâm so với đường nối nút, m (âm = trọng tâm thấp hơn – điểm chèn ở mặt trên). Chỉ thông tin.</summary>
    public static double VerticalOffset(Project p, FrameElement f)
    {
        if (f == null || f.Insertion == BeamInsertion.Centroid) return 0;
        var sec = p.FrameSections.FirstOrDefault(s => s.Name == f.Section);
        double h = sec == null ? 0 : sec.Props(0.5).Depth;
        return f.Insertion switch
        {
            BeamInsertion.TopCenter or BeamInsertion.TopLeft or BeamInsertion.TopRight => -h / 2,
            BeamInsertion.BottomCenter or BeamInsertion.BottomLeft or BeamInsertion.BottomRight => h / 2,
            _ => 0
        };
    }

    /// <summary>Gán điểm chèn + dời ngang (m) cho các dầm.</summary>
    public static EditResult SetInsertion(Project p, IEnumerable<int> frameIds, BeamInsertion ins, double planOffset)
    {
        var fs = Beams(p, frameIds);
        if (fs.Count == 0) return EditResult.Fail("Chưa chọn dầm để gán điểm chèn.");
        if (double.IsNaN(planOffset) || Math.Abs(planOffset) > 2) return EditResult.Fail("Độ dời ngang phải trong khoảng ±2000 mm.");
        foreach (var f in fs) { f.Insertion = ins; f.PlanOffset = R4(planOffset); }
        var r = new EditResult { Ok = true, Message = $"Đã gán điểm chèn \"{InsertionName(ins)}\", dời ngang {planOffset * 1000:0} mm cho {fs.Count} dầm (chỉ ảnh hưởng bản vẽ và xuất ETABS)." };
        r.NewFrames.AddRange(fs.Select(f => f.Id));
        return r;
    }

    private static List<FrameElement> Beams(Project p, IEnumerable<int> ids)
    {
        var set = new HashSet<int>(ids ?? Enumerable.Empty<int>());
        return p.Frames.Where(f => set.Contains(f.Id) && !f.IsVerticalKind).ToList();
    }

    // ==================================================================
    // Gán tiết diện / xoá nhiều dầm
    // ==================================================================
    public static EditResult AssignSection(Project p, IEnumerable<int> frameIds, string section)
    {
        if (string.IsNullOrEmpty(section) || !p.FrameSections.Any(s => s.Name == section)) return EditResult.Fail("Chưa chọn tiết diện hợp lệ.");
        var fs = Beams(p, frameIds);
        if (fs.Count == 0) return EditResult.Fail("Chưa chọn dầm để gán tiết diện.");
        int changed = 0;
        foreach (var f in fs) if (f.Section != section) { f.Section = section; changed++; }
        p.InvalidateIndex();
        var r = new EditResult { Ok = true, Message = $"Đã gán tiết diện {section} cho {fs.Count} dầm ({changed} dầm đổi tiết diện)." };
        r.NewFrames.AddRange(fs.Select(f => f.Id));
        return r;
    }

    public static EditResult DeleteFrames(Project p, IEnumerable<int> frameIds)
    {
        var set = new HashSet<int>(frameIds ?? Enumerable.Empty<int>());
        var fs = p.Frames.Where(f => set.Contains(f.Id)).ToList();
        if (fs.Count == 0) return EditResult.Fail("Chưa chọn thanh để xoá.");
        foreach (var f in fs) ModelEdit.DeleteFrame(p, f);
        p.InvalidateIndex();
        return new EditResult { Ok = true, Message = $"Đã xoá {fs.Count} thanh cùng tải trên thanh." };
    }

    // ==================================================================
    // Nhập toạ độ
    // ==================================================================
    /// <summary>Đổi một số có thể kèm đơn vị ("1200", "1,2m", "120cm", "1200 mm") ra m.</summary>
    public static bool TryLength(string tok, CoordUnit unit, out double m)
    {
        m = 0;
        var s = (tok ?? "").Trim().ToLowerInvariant().Replace(" ", "");
        if (s.Length == 0) return false;
        double k = unit == CoordUnit.Mm ? 0.001 : 1.0;
        if (s.EndsWith("mm")) { k = 0.001; s = s[..^2]; }
        else if (s.EndsWith("cm")) { k = 0.01; s = s[..^2]; }
        else if (s.EndsWith("m")) { k = 1.0; s = s[..^1]; }
        s = s.Replace(',', '.');
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || double.IsNaN(v) || double.IsInfinity(v)) return false;
        m = v * k;
        return true;
    }

    private static bool TryAngle(string tok, out double deg)
    {
        var s = (tok ?? "").Trim().ToLowerInvariant().Replace("°", "").Replace("độ", "").Replace(" ", "");
        if (s.EndsWith("d")) s = s[..^1];
        s = s.Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out deg) && !double.IsNaN(deg) && !double.IsInfinity(deg);
    }

    /// <summary>Tách "a,b" / "a;b" / "a b". Có dấu ";" thì dấu phẩy là dấu thập phân.</summary>
    private static string[] SplitPair(string s)
    {
        if (s.Contains(';')) return s.Split(';');
        if (s.Contains(',')) return s.Split(',');
        return s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Phân tích chuỗi toạ độ kiểu AutoCAD/ETABS (kết quả m):
    /// "x,y" hoặc "#x,y" tuyệt đối; "@dx,dy" tương đối điểm trước; "L&lt;góc" hoặc "@L&lt;góc" toạ độ cực tính từ điểm trước
    /// (góc độ, ngược chiều kim đồng hồ từ +X); "L" = khoảng cách theo hướng từ điểm trước tới con trỏ (dirTo).
    /// Số theo đơn vị unit, hoặc theo hậu tố mm/cm/m. Dấu thập phân phẩy: ngăn cách hai số bằng ";" (vd "1,5;2,4").</summary>
    public static CoordInput ParseCoordinateInput(string text, (double X, double Y)? last, CoordUnit unit, (double X, double Y)? dirTo = null)
    {
        var s = (text ?? "").Trim();
        if (s.Length == 0) return CoordInput.Fail("Chưa nhập toạ độ.");
        bool rel = false, abs = false;
        if (s[0] == '@') { rel = true; s = s[1..].Trim(); }
        else if (s[0] == '#') { abs = true; s = s[1..].Trim(); }
        if (s.Length == 0) return CoordInput.Fail("Thiếu giá trị sau ký hiệu @ / #.");
        int lt = s.IndexOf('<');
        if (lt >= 0)
        {
            if (!TryLength(s[..lt], unit, out double L)) return CoordInput.Fail($"Không đọc được chiều dài \"{s[..lt]}\".");
            if (!TryAngle(s[(lt + 1)..], out double ang)) return CoordInput.Fail($"Không đọc được góc \"{s[(lt + 1)..]}\".");
            var o = abs ? (0.0, 0.0) : last ?? (0.0, 0.0);
            double a = ang * Math.PI / 180;
            return new CoordInput { Ok = true, Kind = CoordInputKind.Polar, X = R4(o.Item1 + L * Math.Cos(a)), Y = R4(o.Item2 + L * Math.Sin(a)) };
        }
        var parts = SplitPair(s);
        if (parts.Length == 1)
        {
            if (last == null || abs || rel) return CoordInput.Fail("Nhập \"x,y\" (tuyệt đối), \"@dx,dy\" (tương đối) hoặc \"L<góc\" (cực).");
            if (!TryLength(parts[0], unit, out double L)) return CoordInput.Fail($"Không đọc được khoảng cách \"{parts[0]}\".");
            if (dirTo == null) return CoordInput.Fail("Di chuột để chỉ hướng rồi nhập khoảng cách.");
            double dx = dirTo.Value.X - last.Value.X, dy = dirTo.Value.Y - last.Value.Y, d = Hyp(dx, dy);
            if (d < 1e-9) return CoordInput.Fail("Di chuột ra khỏi điểm trước để chỉ hướng rồi nhập khoảng cách.");
            return new CoordInput { Ok = true, Kind = CoordInputKind.Distance, X = R4(last.Value.X + L * dx / d), Y = R4(last.Value.Y + L * dy / d) };
        }
        if (parts.Length != 2) return CoordInput.Fail("Nhập hai số x,y – nếu dùng dấu phẩy thập phân hãy ngăn cách bằng dấu chấm phẩy (vd 1,5;2,4).");
        if (!TryLength(parts[0], unit, out double x)) return CoordInput.Fail($"Không đọc được \"{parts[0]}\".");
        if (!TryLength(parts[1], unit, out double y)) return CoordInput.Fail($"Không đọc được \"{parts[1]}\".");
        if (rel)
        {
            var o = last ?? (0.0, 0.0);
            return new CoordInput { Ok = true, Kind = CoordInputKind.Relative, X = R4(o.Item1 + x), Y = R4(o.Item2 + y) };
        }
        return new CoordInput { Ok = true, Kind = CoordInputKind.Absolute, X = R4(x), Y = R4(y) };
    }

    // ==================================================================
    // Bắt điểm
    // ==================================================================
    public static string SnapName(SnapKind k) => k switch
    {
        SnapKind.End => "Đầu dầm", SnapKind.Node => "Nút", SnapKind.GridIntersection => "Giao trục", SnapKind.Intersection => "Giao điểm",
        SnapKind.Midpoint => "Trung điểm", SnapKind.Perpendicular => "Vuông góc", SnapKind.Nearest => "Trên dầm", SnapKind.GridLine => "Trên trục",
        SnapKind.Round => "Lưới 50 mm",
        SnapKind.CadEnd => "Đầu nét (bản vẽ link)", SnapKind.CadMid => "Trung điểm (bản vẽ link)", SnapKind.CadIntersection => "Giao nét (bản vẽ link)",
        SnapKind.CadCenter => "Tâm (bản vẽ link)", _ => ""
    };

    private static int SnapPriority(SnapKind k) => k switch
    {
        SnapKind.End => 1, SnapKind.Node => 1, SnapKind.GridIntersection => 2, SnapKind.Intersection => 3, SnapKind.Midpoint => 4,
        SnapKind.Perpendicular => 5, SnapKind.Nearest => 6, SnapKind.GridLine => 7, _ => 9
    };

    /// <summary>Các điểm bắt trong bán kính tol (m) quanh (x, y) trên cao trình tầng, sắp xếp theo ưu tiên rồi khoảng cách.
    /// from = điểm trước (cho bắt vuông góc).</summary>
    public static List<SnapCandidate> SnapCandidates(Project p, string story, double x, double y, double tol, SnapMask mask = SnapMask.All, (double X, double Y)? from = null)
    {
        double z = ModelEdit.StoryZ(p, story);
        var list = new List<SnapCandidate>();
        void Add(SnapKind k, double px, double py)
        {
            if (double.IsNaN(px) || double.IsNaN(py)) return;
            double d = Hyp(px - x, py - y);
            if (d <= tol) list.Add(new SnapCandidate { Kind = k, X = R4(px), Y = R4(py), Dist = d, Priority = SnapPriority(k) });
        }
        bool Has(SnapMask m) => (mask & m) != 0;
        var beams = ModelEdit.StoryBeams(p, story);
        var straight = new List<(FrameElement f, Seg2 s)>();
        foreach (var f in beams)
        {
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            if (Has(SnapMask.End)) { Add(SnapKind.End, a.X, a.Y); Add(SnapKind.End, b.X, b.Y); }
            if (f.IsCurved)
            {
                var arc = CurvedBeam.Arc(p, f);
                if (arc == null) continue;
                if (Has(SnapMask.Midpoint)) Add(SnapKind.Midpoint, arc.Mid.X, arc.Mid.Y);
                if (Has(SnapMask.Nearest))
                {
                    double t = arc.ParamOf(x, y, out _);
                    if (!double.IsNaN(t)) { var q = arc.PointAt(t); Add(SnapKind.Nearest, q.X, q.Y); }
                }
                continue;
            }
            straight.Add((f, new Seg2(a.X, a.Y, b.X, b.Y)));
        }
        if (Has(SnapMask.Node))
            foreach (var n in p.Nodes)
                if (Math.Abs(n.Z - z) < 0.01) Add(SnapKind.Node, n.X, n.Y);
        var gxs = p.GridsX.ToList(); var gys = p.GridsY.ToList();
        if (Has(SnapMask.GridIntersection))
            foreach (var gx in gxs) foreach (var gy in gys) Add(SnapKind.GridIntersection, gx.Coord, gy.Coord);
        foreach (var (f, s) in straight)
        {
            if (Has(SnapMask.Midpoint)) Add(SnapKind.Midpoint, (s.Ax + s.Bx) / 2, (s.Ay + s.By) / 2);
            if (Has(SnapMask.Nearest))
            {
                var (t, _) = ImportGeometry.ProjectPoint(s, x, y);
                if (t >= 0 && t <= 1) Add(SnapKind.Nearest, s.Ax + t * (s.Bx - s.Ax), s.Ay + t * (s.By - s.Ay));
            }
            if (Has(SnapMask.Perpendicular) && from != null)
            {
                var (t, _) = ImportGeometry.ProjectPoint(s, from.Value.X, from.Value.Y);
                if (t > 1e-6 && t < 1 - 1e-6) Add(SnapKind.Perpendicular, s.Ax + t * (s.Bx - s.Ax), s.Ay + t * (s.By - s.Ay));
            }
            if (Has(SnapMask.Intersection))
            {
                // dầm × trục
                double dx = s.Bx - s.Ax, dy = s.By - s.Ay;
                foreach (var gx in gxs)
                    if (Math.Abs(dx) > 1e-9)
                    {
                        double t = (gx.Coord - s.Ax) / dx;
                        if (t > 1e-6 && t < 1 - 1e-6) Add(SnapKind.Intersection, gx.Coord, s.Ay + t * dy);
                    }
                foreach (var gy in gys)
                    if (Math.Abs(dy) > 1e-9)
                    {
                        double t = (gy.Coord - s.Ay) / dy;
                        if (t > 1e-6 && t < 1 - 1e-6) Add(SnapKind.Intersection, s.Ax + t * dx, gy.Coord);
                    }
            }
        }
        if (Has(SnapMask.Intersection))
            for (int i = 0; i < straight.Count; i++)
                for (int j = i + 1; j < straight.Count; j++)
                    if (ImportGeometry.Intersect(straight[i].s, straight[j].s, out double ix, out double iy, 0.005)) Add(SnapKind.Intersection, ix, iy);
        if (Has(SnapMask.Perpendicular) && from != null)
        {
            foreach (var gx in gxs) Add(SnapKind.Perpendicular, gx.Coord, from.Value.Y);
            foreach (var gy in gys) Add(SnapKind.Perpendicular, from.Value.X, gy.Coord);
        }
        if (Has(SnapMask.GridLine))
        {
            foreach (var gx in gxs) Add(SnapKind.GridLine, gx.Coord, Math.Round(y / 0.05) * 0.05);
            foreach (var gy in gys) Add(SnapKind.GridLine, Math.Round(x / 0.05) * 0.05, gy.Coord);
        }
        if (Has(SnapMask.CadLink)) list.AddRange(KetCauVXT.Import.Link.CadLinkSnap.Candidates(p, story, x, y, tol, mask));   // [CadLink] nét bản vẽ liên kết
        return list.OrderBy(c => c.Priority).ThenBy(c => c.Dist).ToList();
    }

    /// <summary>Điểm bắt tốt nhất; không có điểm nào trong tol → làm tròn lưới 50 mm (Kind = Round).</summary>
    public static SnapCandidate BestSnap(Project p, string story, double x, double y, double tol, SnapMask mask = SnapMask.All, (double X, double Y)? from = null)
    {
        var c = SnapCandidates(p, story, x, y, tol, mask, from).FirstOrDefault();
        if (c != null) return c;
        double rx = R4(Math.Round(x / 0.05) * 0.05), ry = R4(Math.Round(y / 0.05) * 0.05);
        return new SnapCandidate { Kind = SnapKind.Round, X = rx, Y = ry, Dist = Hyp(rx - x, ry - y), Priority = 9 };
    }

    // ==================================================================
    // Dầm chính xác (không làm tròn), chống trùng, chia nút
    // ==================================================================
    private static string NextGroup(Project p, string story, string tag)
    {
        int k = 1;
        while (p.Frames.Any(f => f.DesignGroup == $"D{story}-{tag}{k}")) k++;
        return $"D{story}-{tag}{k}";
    }

    /// <summary>Các khoảng (theo chiều dài, m, tính từ điểm 1) của đoạn 1→2 CHƯA có dầm thẳng cùng tầng nằm trùng.</summary>
    public static List<(double T0, double T1)> Uncovered(Project p, string story, double x1, double y1, double x2, double y2, double tol = 0.002)
    {
        double L = Hyp(x2 - x1, y2 - y1);
        var res = new List<(double T0, double T1)>();
        if (L < 1e-9) return res;
        double ux = (x2 - x1) / L, uy = (y2 - y1) / L;
        var iv = new List<(double Lo, double Hi)>();
        foreach (var f in ModelEdit.StoryBeams(p, story))
        {
            if (f.IsCurved) continue;
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            double ta = (a.X - x1) * ux + (a.Y - y1) * uy, tb = (b.X - x1) * ux + (b.Y - y1) * uy;
            double da = Math.Abs(-(a.X - x1) * uy + (a.Y - y1) * ux), db = Math.Abs(-(b.X - x1) * uy + (b.Y - y1) * ux);
            if (da > tol || db > tol) continue;
            double lo = Math.Max(0, Math.Min(ta, tb)), hi = Math.Min(L, Math.Max(ta, tb));
            if (hi - lo > 1e-4) iv.Add((lo, hi));
        }
        double cur = 0;
        foreach (var (lo, hi) in iv.OrderBy(q => q.Lo))
        {
            if (lo > cur + 0.01) res.Add((cur, lo));
            cur = Math.Max(cur, hi);
        }
        if (L > cur + 0.01) res.Add((cur, L));
        return res.Where(q => q.T1 - q.T0 >= 0.05).ToList();
    }

    /// <summary>Tạo nút giao giữa đoạn mới và các dầm có sẵn cùng cao trình (như ModelEdit.AddBeam).</summary>
    private static void IntersectionNodes(Project p, string story, double z, double x1, double y1, double x2, double y2)
    {
        var seg = new Seg2(x1, y1, x2, y2);
        foreach (var f in ModelEdit.StoryBeams(p, story))
        {
            var ni = p.Node(f.NodeI); var nj = p.Node(f.NodeJ);
            if (f.IsCurved)
            {
                var arc = CurvedBeam.Arc(p, f);
                if (arc != null)
                    foreach (var (qx, qy) in CurvedBeam.Intersections(arc, x1, y1, x2, y2))
                        p.GetOrAddNode(R4(qx), R4(qy), z, story);
                continue;
            }
            if (ImportGeometry.Intersect(seg, new Seg2(ni.X, ni.Y, nj.X, nj.Y), out double ix, out double iy, 0.02))
                p.GetOrAddNode(R4(ix), R4(iy), z, story);
        }
    }

    /// <summary>Thêm dầm thẳng đúng toạ độ (không làm tròn 50 mm; nút trùng trong 1 mm được dùng lại). Phần đã có dầm trùng
    /// được bỏ qua (chỉ thêm phần còn thiếu). Dầm mới chia tại nút giao; dầm cũ bị cắt cũng được chia.
    /// NewFrames = mọi đoạn của dầm mới sau khi chia. splitSlabs = chia lại ô sàn bị dầm cắt qua.</summary>
    public static EditResult AddBeamExact(Project p, string story, double x1, double y1, double x2, double y2, string section, string tag = "T", bool splitSlabs = false)
    {
        double z = ModelEdit.StoryZ(p, story);
        x1 = R4(x1); y1 = R4(y1); x2 = R4(x2); y2 = R4(y2);
        double L = Hyp(x2 - x1, y2 - y1);
        if (L < MinLength) return EditResult.Fail($"Dầm quá ngắn (< {MinLength * 1000:0} mm).");
        if (string.IsNullOrEmpty(section) || !p.FrameSections.Any(s => s.Name == section)) return EditResult.Fail("Chưa chọn tiết diện dầm.");
        var free = Uncovered(p, story, x1, y1, x2, y2);
        if (free.Count == 0) return EditResult.Fail($"Đã có dầm trên suốt đoạn ({x1:0.###}; {y1:0.###}) – ({x2:0.###}; {y2:0.###}) tầng {story} – bỏ qua.");
        string grp = NextGroup(p, story, tag);
        double ux = (x2 - x1) / L, uy = (y2 - y1) / L;
        int added = 0;
        foreach (var (t0, t1) in free)
        {
            double ax = R4(x1 + ux * t0), ay = R4(y1 + uy * t0), bx = R4(x1 + ux * t1), by = R4(y1 + uy * t1);
            var a = p.GetOrAddNode(ax, ay, z, story); var b = p.GetOrAddNode(bx, by, z, story);
            IntersectionNodes(p, story, z, ax, ay, bx, by);
            if (ImportGeometry.AddFrame(p, a, b, FrameKind.Beam, section, story, grp, grp) != null) added++;
        }
        if (added == 0) return EditResult.Fail("Đã có dầm nối đúng hai điểm này.");
        int split = ModelBuilder.SplitFramesAtNodes(p);
        p.InvalidateIndex();
        var r = new EditResult { Ok = true };
        r.NewFrames.AddRange(p.Frames.Where(f => f.DesignGroup == grp).Select(f => f.Id));
        int ns = splitSlabs ? SplitSlabsByBeams(p, story) : 0;
        double cov = free.Sum(q => q.T1 - q.T0);
        r.Message = $"Đã thêm dầm {section} dài {cov:0.00} m tại tầng {story}"
                    + (cov < L - 0.01 ? $" (bỏ qua {L - cov:0.00} m đã có dầm)" : "")
                    + (split > 0 ? ", đã chia tại các nút giao" : "") + (ns > 0 ? $", chia lại {ns} ô sàn" : "") + ".";
        return r;
    }

    /// <summary>Dầm cong đúng toạ độ (không bắt điểm) – dùng khi sao chép / đối xứng dầm cong.</summary>
    private static EditResult AddCurvedExact(Project p, string story, double x1, double y1, double x2, double y2, double sagitta, string section, string tag)
    {
        double z = ModelEdit.StoryZ(p, story);
        var a = p.GetOrAddNode(R4(x1), R4(y1), z, story); var b = p.GetOrAddNode(R4(x2), R4(y2), z, story);
        if (a.Id == b.Id) return EditResult.Fail("Dầm cong quá ngắn.");
        if (p.Frames.Any(f => (f.NodeI == a.Id && f.NodeJ == b.Id) || (f.NodeI == b.Id && f.NodeJ == a.Id))) return EditResult.Fail("Đã có dầm nối đúng hai điểm này.");
        var arc = CurvedBeam.Arc(a.P, b.P, sagitta);
        if (arc != null)
            foreach (var f in ModelEdit.StoryBeams(p, story).Where(q => !q.IsCurved))
            {
                var ni = p.Node(f.NodeI); var nj = p.Node(f.NodeJ);
                foreach (var (ix, iy) in CurvedBeam.Intersections(arc, ni.X, ni.Y, nj.X, nj.Y)) p.GetOrAddNode(R4(ix), R4(iy), z, story);
            }
        string grp = NextGroup(p, story, tag);
        var fr = ImportGeometry.AddFrame(p, a, b, FrameKind.Beam, section, story, grp, grp);
        if (fr == null) return EditResult.Fail("Không thêm được dầm cong.");
        fr.CurveMidOffset = R4(sagitta);
        ModelBuilder.SplitFramesAtNodes(p);
        p.InvalidateIndex();
        var r = new EditResult { Ok = true, Message = $"Đã thêm dầm cong {section} tại tầng {story}." };
        r.NewFrames.AddRange(p.Frames.Where(f => f.DesignGroup == grp).Select(f => f.Id));
        return r;
    }

    private static EditResult Merge(List<EditResult> parts, string okMsg, string failMsg)
    {
        var r = new EditResult { Ok = parts.Any(q => q.Ok) };
        foreach (var q in parts.Where(q => q.Ok)) { r.NewFrames.AddRange(q.NewFrames); r.NewSlabs.AddRange(q.NewSlabs); }
        r.Message = r.Ok ? okMsg : failMsg + (parts.Count > 0 ? " " + parts[0].Message : "");
        return r;
    }

    // ==================================================================
    // Chia lại ô sàn theo dầm
    // ==================================================================
    /// <summary>Ô sàn chữ nhật của tầng có dầm thẳng (song song X/Y) cắt ngang suốt ô → chia thành các ô nhỏ theo dầm
    /// (giữ tiết diện, cấu tạo sàn và các tải trên sàn nhập tay / từ cấu tạo; tải tường tự sinh lại khi phân tích).
    /// Trả về số ô sàn đã chia.</summary>
    public static int SplitSlabsByBeams(Project p, string story)
    {
        var segs = ModelEdit.StoryBeams(p, story).Where(f => !f.IsCurved)
            .Select(f => { var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ); return new Seg2(a.X, a.Y, b.X, b.Y); }).ToList();
        int count = 0;
        foreach (var s in p.Slabs.Where(q => q.Story == story).ToList())
        {
            var nodes = s.Nodes.Select(id => p.Nodes.FirstOrDefault(n => n.Id == id)).ToList();
            if (nodes.Count < 4 || nodes.Any(n => n == null)) continue;
            var pts = nodes.Select(n => (n.X, n.Y)).ToList();
            if (!ImportGeometry.IsAxisRect(pts, out double x0, out double y0, out double x1, out double y1, 0.005)) continue;
            double z = nodes[0].Z;
            var xs = new List<double> { x0, x1 }; var ys = new List<double> { y0, y1 };
            // đường chia x = c: dầm song song Y phủ kín [y0, y1]
            foreach (double c in ImportGeometry.Cluster(segs.Where(g => g.IsAlongY(0.002) && g.Ax > x0 + 0.05 && g.Ax < x1 - 0.05).Select(g => g.Ax), 0.001))
                if (Covers(segs.Where(g => g.IsAlongY(0.002) && Math.Abs(g.Ax - c) < 0.002).Select(g => (Math.Min(g.Ay, g.By), Math.Max(g.Ay, g.By))), y0, y1)) xs.Add(c);
            foreach (double c in ImportGeometry.Cluster(segs.Where(g => g.IsAlongX(0.002) && g.Ay > y0 + 0.05 && g.Ay < y1 - 0.05).Select(g => g.Ay), 0.001))
                if (Covers(segs.Where(g => g.IsAlongX(0.002) && Math.Abs(g.Ay - c) < 0.002).Select(g => (Math.Min(g.Ax, g.Bx), Math.Max(g.Ax, g.Bx))), x0, x1)) ys.Add(c);
            if (xs.Count == 2 && ys.Count == 2) continue;
            xs.Sort(); ys.Sort();
            var loads = p.AreaLoads.Where(a => a.Slab == s.Id && a.Source != "WALL").ToList();
            p.Slabs.Remove(s);
            p.AreaLoads.RemoveAll(a => a.Slab == s.Id);
            int part = 0;
            for (int i = 0; i + 1 < xs.Count; i++)
                for (int j = 0; j + 1 < ys.Count; j++)
                {
                    var n1 = p.GetOrAddNode(R4(xs[i]), R4(ys[j]), z, story); var n2 = p.GetOrAddNode(R4(xs[i + 1]), R4(ys[j]), z, story);
                    var n3 = p.GetOrAddNode(R4(xs[i + 1]), R4(ys[j + 1]), z, story); var n4 = p.GetOrAddNode(R4(xs[i]), R4(ys[j + 1]), z, story);
                    string label = s.Label;
                    if (part > 0)
                    {
                        int k = part;
                        while (p.Slabs.Any(q => q.Label == $"{s.Label}.{k}")) k++;
                        label = $"{s.Label}.{k}";
                    }
                    part++;
                    var ns = new Slab { Id = p.NextSlabId(), Label = label, Story = s.Story, Section = s.Section, BuildUp = s.BuildUp, Nodes = new List<int> { n1.Id, n2.Id, n3.Id, n4.Id },
                        LevelOffset = s.LevelOffset, DropReason = s.DropReason };   // [Sàn hạ cốt] ô chia lại giữ hạ cốt
                    p.Slabs.Add(ns);
                    foreach (var l in loads) p.AreaLoads.Add(new AreaLoad { Slab = ns.Id, Pattern = l.Pattern, Q = l.Q, Note = l.Note, Source = l.Source });
                }
            count++;
        }
        if (count > 0) { ModelBuilder.SplitFramesAtNodes(p); p.InvalidateIndex(); }
        return count;
    }

    private static bool Covers(IEnumerable<(double Lo, double Hi)> iv, double a, double b)
    {
        double cur = a;
        foreach (var (lo, hi) in iv.OrderBy(q => q.Lo))
        {
            if (lo > cur + 0.01) return false;
            cur = Math.Max(cur, hi);
            if (cur >= b - 0.01) return true;
        }
        return cur >= b - 0.01;
    }

    // ==================================================================
    // Vẽ nhanh dầm theo đoạn trục (Quick Draw Beams)
    // ==================================================================
    /// <summary>Mọi đoạn trục giữa hai giao trục liền kề.</summary>
    public static List<GridSegment> GridSegments(Project p)
    {
        var xs = p.GridsX.ToList(); var ys = p.GridsY.ToList();
        var res = new List<GridSegment>();
        foreach (var gx in xs)
            for (int j = 0; j + 1 < ys.Count; j++)
                if (ys[j + 1].Coord - ys[j].Coord > 1e-6)
                    res.Add(new GridSegment { Line = gx, From = ys[j].Coord, To = ys[j + 1].Coord, FromGrid = ys[j].Name, ToGrid = ys[j + 1].Name });
        foreach (var gy in ys)
            for (int i = 0; i + 1 < xs.Count; i++)
                if (xs[i + 1].Coord - xs[i].Coord > 1e-6)
                    res.Add(new GridSegment { Line = gy, From = xs[i].Coord, To = xs[i + 1].Coord, FromGrid = xs[i].Name, ToGrid = xs[i + 1].Name });
        return res;
    }

    /// <summary>Đoạn trục gần điểm (x, y) nhất trong phạm vi tol (m); null nếu không có.</summary>
    public static GridSegment GridSegmentAt(Project p, double x, double y, double tol)
    {
        GridSegment best = null; double bd = tol;
        foreach (var s in GridSegments(p))
        {
            double along = s.Line.Dir == GridDir.X ? y : x, perp = Math.Abs((s.Line.Dir == GridDir.X ? x : y) - s.Line.Coord);
            if (along <= s.From + 1e-9 || along >= s.To - 1e-9 || perp > bd) continue;
            bd = perp; best = s;
        }
        return best;
    }

    /// <summary>Đoạn trục trong cửa sổ chọn: window (crossing = false) – nằm trọn trong khung; crossing – chạm khung.</summary>
    public static List<GridSegment> GridSegmentsInWindow(Project p, double xa, double ya, double xb, double yb, bool crossing)
    {
        double x0 = Math.Min(xa, xb), x1 = Math.Max(xa, xb), y0 = Math.Min(ya, yb), y1 = Math.Max(ya, yb);
        bool In(double x, double y) => x >= x0 - 1e-9 && x <= x1 + 1e-9 && y >= y0 - 1e-9 && y <= y1 + 1e-9;
        return GridSegments(p).Where(s =>
        {
            if (!crossing) return In(s.X1, s.Y1) && In(s.X2, s.Y2);
            if (s.Line.Dir == GridDir.X) return s.Line.Coord >= x0 && s.Line.Coord <= x1 && s.To >= y0 && s.From <= y1;
            return s.Line.Coord >= y0 && s.Line.Coord <= y1 && s.To >= x0 && s.From <= x1;
        }).ToList();
    }

    /// <summary>Thêm dầm trên các đoạn trục (bỏ qua đoạn đã có dầm; đoạn có dầm một phần → thêm phần còn thiếu).</summary>
    public static EditResult QuickBeamsOnGridSegments(Project p, string story, IEnumerable<GridSegment> segments, string section, bool splitSlabs = true)
    {
        ModelEdit.StoryZ(p, story);
        if (string.IsNullOrEmpty(section) || !p.FrameSections.Any(s => s.Name == section)) return EditResult.Fail("Chưa chọn tiết diện dầm.");
        var segs = (segments ?? Enumerable.Empty<GridSegment>()).ToList();
        if (segs.Count == 0) return EditResult.Fail("Chưa chọn đoạn trục nào.");
        var r = new EditResult();
        int added = 0, skipped = 0;
        foreach (var s in segs)
        {
            var e = AddBeamExact(p, story, s.X1, s.Y1, s.X2, s.Y2, section, "L", false);
            if (e.Ok) { added++; r.NewFrames.AddRange(e.NewFrames); } else skipped++;
        }
        int ns = splitSlabs && added > 0 ? SplitSlabsByBeams(p, story) : 0;
        r.Ok = added > 0;
        r.Message = r.Ok
            ? $"Đã thêm dầm {section} trên {added} đoạn trục tại tầng {story}" + (skipped > 0 ? $" (bỏ qua {skipped} đoạn đã có dầm)" : "") + (ns > 0 ? $", chia lại {ns} ô sàn" : "") + "."
            : $"Các đoạn trục đã chọn ({segs.Count}) đều đã có dầm tại tầng {story}.";
        return r;
    }

    // ==================================================================
    // Dầm phụ theo số lượng / khoảng cách
    // ==================================================================
    /// <summary>Ô (hình chữ nhật) chứa điểm: ô kín bởi dầm; không có thì ô lưới trục.</summary>
    public static (double X0, double Y0, double X1, double Y1)? BayAt(Project p, string story, double x, double y)
    {
        var pn = ModelEdit.PanelAt(p, story, x, y);
        if (pn != null) return pn;
        var xs = p.GridsX.Select(g => g.Coord).ToList(); var ys = p.GridsY.Select(g => g.Coord).ToList();
        for (int i = 0; i + 1 < xs.Count; i++)
            for (int j = 0; j + 1 < ys.Count; j++)
                if (x > xs[i] && x < xs[i + 1] && y > ys[j] && y < ys[j + 1]) return (xs[i], ys[j], xs[i + 1], ys[j + 1]);
        return null;
    }

    /// <summary>Toạ độ các dầm phụ trong khoảng (a, b) theo tuỳ chọn; refCoord = toạ độ trục mốc (null = mép ô a, hoặc b khi FromEnd).
    /// Thứ tự ưu tiên: danh sách khoảng cách → khoảng cách đều → số lượng chia đều. Bỏ vị trí cách mép ô &lt; 50 mm.</summary>
    public static List<double> SecondaryPositions(double a, double b, SecondaryBeamOptions o, double? refCoord = null)
    {
        double lo = Math.Min(a, b), hi = Math.Max(a, b), L = hi - lo;
        var res = new List<double>();
        if (o == null || L < 0.2) return res;
        const double edge = 0.05;
        double r0 = refCoord ?? (o.FromEnd ? hi : lo);
        int sgn = r0 > (lo + hi) / 2 ? -1 : 1;
        if (refCoord != null && o.FromEnd) sgn = -sgn;
        if (o.Distances != null && o.Distances.Count > 0)
            res.AddRange(o.Distances.Where(d => !double.IsNaN(d)).Select(d => r0 + sgn * d));
        else if (o.Spacing > 1e-6)
        {
            double s = o.Spacing, start = o.StartOffset > 1e-6 ? o.StartOffset : s;
            for (int k = 0; k < 1000; k++)
            {
                double v = r0 + sgn * (start + k * s);
                if (sgn > 0 ? v >= hi - edge : v <= lo + edge) break;
                res.Add(v);
            }
        }
        else if (o.Count >= 1)
            for (int k = 1; k <= Math.Min(o.Count, 200); k++) res.Add(lo + k * L / (o.Count + 1));
        return ImportGeometry.Cluster(res.Where(v => v > lo + edge && v < hi - edge).Select(R4), 0.001);
    }

    /// <summary>Dầm phụ trong ô chứa điểm (x, y) – ô kín bởi dầm, không có thì ô lưới trục (xem SecondaryBeams).</summary>
    public static EditResult SecondaryBeamsAt(Project p, string story, double x, double y, SecondaryBeamOptions o)
    {
        var bay = BayAt(p, story, x, y);
        if (bay == null) return EditResult.Fail("Không tìm thấy ô kín bởi dầm / ô lưới trục tại vị trí bấm.");
        return SecondaryBeams(p, story, bay.Value, o);
    }

    /// <summary>Thêm dầm phụ song song X hoặc Y trong ô chữ nhật bay, nhịp giữa hai cạnh ô (hai dầm đỡ được chia nút tại đầu dầm phụ).</summary>
    public static EditResult SecondaryBeams(Project p, string story, (double X0, double Y0, double X1, double Y1) bay, SecondaryBeamOptions o)
    {
        ModelEdit.StoryZ(p, story);
        if (o == null) return EditResult.Fail("Thiếu tuỳ chọn dầm phụ.");
        if (string.IsNullOrEmpty(o.Section) || !p.FrameSections.Any(s => s.Name == o.Section)) return EditResult.Fail("Chưa chọn tiết diện dầm phụ.");
        double x0 = Math.Min(bay.X0, bay.X1), x1 = Math.Max(bay.X0, bay.X1), y0 = Math.Min(bay.Y0, bay.Y1), y1 = Math.Max(bay.Y0, bay.Y1);
        double? rc = null;
        if (!string.IsNullOrWhiteSpace(o.RefGrid))
        {
            var g = FindGrid(p, o.RefGrid);
            if (g == null) return EditResult.Fail($"Không có trục \"{o.RefGrid}\".");
            if (o.AlongX ? g.Dir != GridDir.Y : g.Dir != GridDir.X)
                return EditResult.Fail($"Trục mốc {g.Name} phải song song dầm phụ (dầm song song {(o.AlongX ? "X → chọn trục chữ A, B…" : "Y → chọn trục số 1, 2…")}).");
            rc = g.Coord;
        }
        var pos = SecondaryPositions(o.AlongX ? y0 : x0, o.AlongX ? y1 : x1, o, rc);
        if (pos.Count == 0) return EditResult.Fail($"Không có vị trí dầm phụ nào nằm trong ô {x1 - x0:0.00}×{y1 - y0:0.00} m – kiểm tra số lượng / khoảng cách / trục mốc.");
        var r = new EditResult();
        int skipped = 0;
        foreach (double c in pos)
        {
            var e = o.AlongX ? AddBeamExact(p, story, x0, c, x1, c, o.Section, "P") : AddBeamExact(p, story, c, y0, c, y1, o.Section, "P");
            if (e.Ok) r.NewFrames.AddRange(e.NewFrames); else skipped++;
        }
        r.Ok = r.NewFrames.Count > 0;
        if (!r.Ok) return EditResult.Fail("Các vị trí dầm phụ đều đã có dầm.");
        int ns = o.SplitSlabs ? SplitSlabsByBeams(p, story) : 0;
        double baseC = o.AlongX ? y0 : x0;
        var sp = new List<double>();
        for (int i = 0; i < pos.Count; i++) sp.Add(pos[i] - (i == 0 ? baseC : pos[i - 1]));
        r.Message = $"Đã thêm {pos.Count - skipped} dầm phụ {o.Section} song song trục {(o.AlongX ? "X" : "Y")} trong ô {x1 - x0:0.00}×{y1 - y0:0.00} m tầng {story}: "
                    + $"cách mép ô {string.Join("; ", pos.Select(v => ((v - baseC) * 1000).ToString("0", CultureInfo.InvariantCulture)))} mm"
                    + (skipped > 0 ? $" (bỏ qua {skipped} vị trí đã có dầm)" : "") + (ns > 0 ? $", chia lại {ns} ô sàn" : "") + ".";
        return r;
    }

    // ==================================================================
    // Dầm theo trục + khoảng cách, toạ độ, công xôn
    // ==================================================================
    public static GridLine FindGrid(Project p, string name)
    {
        var n = (name ?? "").Trim();
        return p.Grids.FirstOrDefault(g => g.Name == n) ?? p.Grids.FirstOrDefault(g => string.Equals(g.Name, n, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Đọc "trục ± khoảng cách": "2", "2+1200", "A - 600", "B+1,2m" (khoảng cách theo unit). null nếu không đọc được.</summary>
    public static (GridLine Grid, double Offset)? ParseGridOffset(Project p, string text, CoordUnit unit)
    {
        var s = (text ?? "").Trim();
        if (s.Length == 0) return null;
        var g0 = FindGrid(p, s);
        if (g0 != null) return (g0, 0);
        for (int i = s.Length - 1; i > 0; i--)
        {
            if (s[i] != '+' && s[i] != '-') continue;
            var g = FindGrid(p, s[..i].Trim());
            if (g == null) continue;
            if (!TryLength(s[(i + 1)..], unit, out double d)) return null;
            return (g, s[i] == '-' ? -d : d);
        }
        return null;
    }

    /// <summary>Dầm nằm trên đường song song trục gridRef, cách trục offset (m), từ trục fromGrid (+fromOffset) tới trục toGrid (+toOffset)
    /// – hai trục giới hạn vuông góc với gridRef. Vd. trục 2 + 1,2 m, từ trục A tới C.</summary>
    public static EditResult BeamByGridOffset(Project p, string story, string gridRef, double offset, string fromGrid, string toGrid, string section,
                                              double fromOffset = 0, double toOffset = 0)
    {
        var g = FindGrid(p, gridRef);
        if (g == null) return EditResult.Fail($"Không có trục \"{gridRef}\".");
        var gf = FindGrid(p, fromGrid); var gt = FindGrid(p, toGrid);
        if (gf == null || gt == null) return EditResult.Fail($"Không có trục giới hạn \"{(gf == null ? fromGrid : toGrid)}\".");
        if (gf.Dir == g.Dir || gt.Dir == g.Dir) return EditResult.Fail($"Trục giới hạn {gf.Name}, {gt.Name} phải vuông góc với trục {g.Name}.");
        double c = g.Coord + offset, t1 = gf.Coord + fromOffset, t2 = gt.Coord + toOffset;
        var r = g.Dir == GridDir.X ? AddBeamExact(p, story, c, t1, c, t2, section, "T", true) : AddBeamExact(p, story, t1, c, t2, c, section, "T", true);
        if (r.Ok) r.Message = $"Trục {g.Name} {(offset >= 0 ? "+" : "−")} {Math.Abs(offset) * 1000:0} mm, từ trục {gf.Name} tới trục {gt.Name}: " + r.Message;
        return r;
    }

    /// <summary>Dầm theo toạ độ tuyệt đối hai đầu (m).</summary>
    public static EditResult BeamByCoordinates(Project p, string story, double x1, double y1, double x2, double y2, string section) =>
        AddBeamExact(p, story, x1, y1, x2, y2, section, "T", true);

    /// <summary>Dầm công xôn từ điểm đầu (nên nằm trên dầm/cột/nút có sẵn) dài length (m) theo góc angleDeg (độ, ngược chiều kim
    /// đồng hồ từ +X). Đầu tự do không có gối.</summary>
    public static EditResult Cantilever(Project p, string story, (double X, double Y) start, double length, double angleDeg, string section)
    {
        double z = ModelEdit.StoryZ(p, story);
        if (double.IsNaN(length) || length < MinLength) return EditResult.Fail("Chiều dài công xôn phải ≥ 100 mm.");
        if (length > 6) return EditResult.Fail("Chiều dài công xôn > 6 m – kiểm tra lại số liệu (đơn vị mm/m?).");
        double a = angleDeg * Math.PI / 180;
        double ex = R4(start.X + length * Math.Cos(a)), ey = R4(start.Y + length * Math.Sin(a));
        bool supported = p.Nodes.Any(n => Math.Abs(n.Z - z) < 0.01 && Math.Abs(n.X - start.X) < 0.002 && Math.Abs(n.Y - start.Y) < 0.002
                                          && p.Frames.Any(f => f.NodeI == n.Id || f.NodeJ == n.Id))
                         || ModelEdit.StoryBeams(p, story).Any(f => !f.IsCurved && OnBeam(p, f, start.X, start.Y));
        var r = AddBeamExact(p, story, start.X, start.Y, ex, ey, section, "CX", false);
        if (!r.Ok) return r;
        r.Message = $"Đã thêm dầm công xôn {section} dài {length:0.00} m, hướng {angleDeg:0.#}° tại tầng {story}"
                    + (supported ? "." : " – LƯU Ý: điểm đầu chưa nằm trên dầm/cột nào, công xôn không có gối đỡ!")
                    + (length > 2.0 ? " Công xôn > 2,0 m – kiểm tra kỹ võng và rung." : "");
        return r;
    }

    private static bool OnBeam(Project p, FrameElement f, double x, double y)
    {
        var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
        var (t, d) = ImportGeometry.ProjectPoint(new Seg2(a.X, a.Y, b.X, b.Y), x, y);
        return t >= -1e-6 && t <= 1 + 1e-6 && d < 0.002;
    }

    // ==================================================================
    // Sao chép, đối xứng
    // ==================================================================
    private static string StoryOfFrame(Project p, FrameElement f)
    {
        if (p.Stories.Any(s => s.Name == f.Story)) return f.Story;
        return p.StoryAt(p.Node(f.NodeI).Z)?.Name;
    }

    private static EditResult CopyBeam(Project p, FrameElement src, string story, double ax, double ay, double bx, double by, bool mirrored, string tag)
    {
        var e = src.IsCurved ? AddCurvedExact(p, story, ax, ay, bx, by, mirrored ? -src.CurveMidOffset : src.CurveMidOffset, src.Section, tag)
                             : AddBeamExact(p, story, ax, ay, bx, by, src.Section, tag);
        if (!e.Ok) return e;
        foreach (var nf in p.Frames.Where(f => e.NewFrames.Contains(f.Id)))
        {
            nf.Angle = src.Angle; nf.Insertion = src.Insertion; nf.PlanOffset = mirrored ? -src.PlanOffset : src.PlanOffset;
            nf.UnbracedLength22 = src.UnbracedLength22;
            if (mirrored) nf.Insertion = MirrorInsertion(src.Insertion);
        }
        if (e.NewFrames.Count == 1)
        {
            var nf = p.Frames.First(f => f.Id == e.NewFrames[0]);
            nf.ReleaseI = (bool[])src.ReleaseI.Clone(); nf.ReleaseJ = (bool[])src.ReleaseJ.Clone();
        }
        return e;
    }

    private static BeamInsertion MirrorInsertion(BeamInsertion k) => k switch
    {
        BeamInsertion.TopLeft => BeamInsertion.TopRight, BeamInsertion.TopRight => BeamInsertion.TopLeft,
        BeamInsertion.MiddleLeft => BeamInsertion.MiddleRight, BeamInsertion.MiddleRight => BeamInsertion.MiddleLeft,
        BeamInsertion.BottomLeft => BeamInsertion.BottomRight, BeamInsertion.BottomRight => BeamInsertion.BottomLeft,
        _ => k
    };

    /// <summary>Sao chép dầm: tịnh tiến k·(dx, dy), k = 1…copies trên cùng tầng của dầm gốc (hoặc trên các tầng targetStories);
    /// dx = dy = 0 và có targetStories → chép nguyên vị trí sang các tầng đó (bỏ tầng gốc). Giữ tiết diện, góc xoay, độ cong,
    /// điểm chèn; đoạn đã có dầm được bỏ qua.</summary>
    public static EditResult Replicate(Project p, IEnumerable<int> frameIds, double dx, double dy, int copies, IEnumerable<string> targetStories = null)
    {
        var src = Beams(p, frameIds);
        if (src.Count == 0) return EditResult.Fail("Chưa chọn dầm để sao chép.");
        bool move = Math.Abs(dx) + Math.Abs(dy) > 1e-6;
        var ts = targetStories?.Where(s => p.Stories.Any(q => q.Name == s)).Distinct().ToList();
        if (!move && (ts == null || ts.Count == 0)) return EditResult.Fail("Nhập Δx, Δy khác 0 hoặc chọn tầng đích để sao chép.");
        if (move && (copies < 1 || copies > 100)) return EditResult.Fail("Số bản sao phải từ 1 đến 100.");
        var geo = src.Select(f => (f, a: p.Node(f.NodeI), b: p.Node(f.NodeJ), st: StoryOfFrame(p, f)))
                     .Select(q => (q.f, ax: q.a.X, ay: q.a.Y, bx: q.b.X, by: q.b.Y, q.st)).Where(q => q.st != null).ToList();
        var r = new EditResult();
        int ok = 0, skip = 0;
        var ks = move ? Enumerable.Range(1, copies).ToList() : new List<int> { 0 };
        foreach (int k in ks)
            foreach (var g in geo)
                foreach (var st in ts != null && ts.Count > 0 ? ts : new List<string> { g.st })
                {
                    if (!move && st == g.st) continue;
                    var e = CopyBeam(p, g.f, st, g.ax + k * dx, g.ay + k * dy, g.bx + k * dx, g.by + k * dy, false, "S");
                    if (e.Ok) { ok++; r.NewFrames.AddRange(e.NewFrames); } else skip++;
                }
        r.Ok = ok > 0;
        if (r.Ok) foreach (var st in r.NewFrames.Select(id => p.Frames.First(f => f.Id == id).Story).Distinct().ToList()) SplitSlabsByBeams(p, st);
        r.Message = r.Ok
            ? (move ? $"Đã sao chép {src.Count} dầm × {copies} lần (Δx = {dx * 1000:0}, Δy = {dy * 1000:0} mm): thêm {ok} dầm" : $"Đã chép {src.Count} dầm sang {ts.Count} tầng: thêm {ok} dầm")
              + (skip > 0 ? $", bỏ qua {skip} (đã có dầm / ngoài phạm vi)." : ".")
            : "Không sao chép được dầm nào (vị trí đích đã có dầm).";
        return r;
    }

    /// <summary>Đối xứng dầm qua đường trục (trục theo X: x = Coord; theo Y: y = Coord). keepOriginal = false → xoá dầm gốc (lật).</summary>
    public static EditResult Mirror(Project p, IEnumerable<int> frameIds, GridLine axis, bool keepOriginal = true)
    {
        if (axis == null) return EditResult.Fail("Chưa chọn trục đối xứng.");
        return Mirror(p, frameIds, axis.Dir, axis.Coord, keepOriginal, axis.Name);
    }

    public static EditResult Mirror(Project p, IEnumerable<int> frameIds, GridDir dir, double coord, bool keepOriginal = true, string axisName = null)
    {
        var src = Beams(p, frameIds);
        if (src.Count == 0) return EditResult.Fail("Chưa chọn dầm để đối xứng.");
        var geo = src.Select(f => (f, a: p.Node(f.NodeI), b: p.Node(f.NodeJ), st: StoryOfFrame(p, f))).Where(q => q.st != null).ToList();
        (double, double) M(double x, double y) => dir == GridDir.X ? (2 * coord - x, y) : (x, 2 * coord - y);
        bool Same(double x1, double y1, double x2, double y2) => Math.Abs(x1 - x2) + Math.Abs(y1 - y2) < 1e-4;
        var r = new EditResult();
        int ok = 0, skip = 0;
        var work = new List<(FrameElement f, double ax, double ay, double bx, double by, string st)>();
        foreach (var g in geo)
        {
            var (ax, ay) = M(g.a.X, g.a.Y); var (bx, by) = M(g.b.X, g.b.Y);
            bool self = (Same(ax, ay, g.a.X, g.a.Y) && Same(bx, by, g.b.X, g.b.Y)) || (Same(ax, ay, g.b.X, g.b.Y) && Same(bx, by, g.a.X, g.a.Y));
            if (self && !g.f.IsCurved) { skip++; continue; }   // dầm nằm trên trục đối xứng
            work.Add((g.f, ax, ay, bx, by, g.st));
        }
        var del = keepOriginal ? new List<FrameElement>() : work.Select(w => w.f).ToList();
        foreach (var f in del) ModelEdit.DeleteFrame(p, f);
        p.InvalidateIndex();
        foreach (var w in work)
        {
            var e = CopyBeam(p, w.f, w.st, w.ax, w.ay, w.bx, w.by, true, "M");
            if (e.Ok) { ok++; r.NewFrames.AddRange(e.NewFrames); } else skip++;
        }
        r.Ok = ok > 0 || del.Count > 0;
        if (ok > 0) foreach (var st in r.NewFrames.Select(id => p.Frames.First(f => f.Id == id).Story).Distinct().ToList()) SplitSlabsByBeams(p, st);
        string ax0 = axisName != null ? $"trục {axisName}" : (dir == GridDir.X ? $"x = {coord:0.###} m" : $"y = {coord:0.###} m");
        r.Message = r.Ok ? $"Đã đối xứng {src.Count} dầm qua {ax0}: thêm {ok} dầm" + (keepOriginal ? "" : $", xoá {del.Count} dầm gốc") + (skip > 0 ? $", bỏ qua {skip}." : ".")
                         : "Không đối xứng được dầm nào (vị trí đối xứng đã có dầm hoặc dầm nằm trên trục đối xứng).";
        return r;
    }

    // ==================================================================
    // Phạm vi tầng
    // ==================================================================
    /// <summary>Chữ ký mặt bằng tầng: các đường dầm thẳng (gộp đoạn thẳng hàng liền nhau), dầm cong, vị trí cột đỡ sàn tầng.
    /// Hai tầng cùng chữ ký = "tầng tương tự".</summary>
    public static string PlanSignature(Project p, string story)
    {
        double z = ModelEdit.StoryZ(p, story);
        var lines = new Dictionary<string, List<(double Lo, double Hi)>>();
        var items = new List<string>();
        foreach (var f in ModelEdit.StoryBeams(p, story))
        {
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            if (f.IsCurved)
            {
                items.Add(string.Create(CultureInfo.InvariantCulture, $"C|{Math.Min(a.X, b.X):0.000},{Math.Min(a.Y, b.Y):0.000}|{Math.Max(a.X, b.X):0.000},{Math.Max(a.Y, b.Y):0.000}|{Math.Abs(f.CurveMidOffset):0.000}"));
                continue;
            }
            double L = Hyp(b.X - a.X, b.Y - a.Y);
            if (L < 1e-6) continue;
            double ux = (b.X - a.X) / L, uy = (b.Y - a.Y) / L;
            if (ux < -1e-9 || (Math.Abs(ux) <= 1e-9 && uy < 0)) { ux = -ux; uy = -uy; }
            double ang = Math.Round(Math.Atan2(uy, ux) * 180 / Math.PI, 2), c = Math.Round(-a.X * uy + a.Y * ux, 3);
            double ta = a.X * ux + a.Y * uy, tb = b.X * ux + b.Y * uy;
            string key = string.Create(CultureInfo.InvariantCulture, $"L|{ang:0.00}|{c:0.000}");
            if (!lines.TryGetValue(key, out var lst)) lines[key] = lst = new List<(double, double)>();
            lst.Add((Math.Min(ta, tb), Math.Max(ta, tb)));
        }
        foreach (var kv in lines)
        {
            double lo = double.NaN, hi = double.NaN;
            foreach (var (l, h) in kv.Value.OrderBy(q => q.Lo))
            {
                if (double.IsNaN(lo)) { lo = l; hi = h; continue; }
                if (l <= hi + 0.002) { hi = Math.Max(hi, h); continue; }
                items.Add(string.Create(CultureInfo.InvariantCulture, $"{kv.Key}|{lo:0.000}-{hi:0.000}"));
                lo = l; hi = h;
            }
            if (!double.IsNaN(lo)) items.Add(string.Create(CultureInfo.InvariantCulture, $"{kv.Key}|{lo:0.000}-{hi:0.000}"));
        }
        foreach (var f in p.Frames.Where(f => f.IsVerticalKind))
        {
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            var top = a.Z >= b.Z ? a : b;
            if (Math.Abs(top.Z - z) < 0.01) items.Add(string.Create(CultureInfo.InvariantCulture, $"K|{top.X:0.000},{top.Y:0.000}"));
        }
        items.Sort(StringComparer.Ordinal);
        return string.Join(";", items);
    }

    /// <summary>Các tầng có cùng mặt bằng dầm – cột với tầng đã cho (gồm cả tầng đó), theo cao độ tăng dần.</summary>
    public static List<string> StoriesSimilar(Project p, string story)
    {
        string sig = PlanSignature(p, story);
        return p.Stories.OrderBy(s => s.Elevation).Where(s => s.Name == story || PlanSignature(p, s.Name) == sig).Select(s => s.Name).ToList();
    }

    /// <summary>Danh sách tầng áp dụng theo phạm vi; Selected = các tầng đánh dấu (rỗng → tầng hiện tại).
    /// Similar: nếu có danh sách đánh dấu thì dùng danh sách đó (người dùng tự chọn tầng giống nhau), không thì tự nhận dạng.</summary>
    public static List<string> ResolveStories(Project p, string current, StoryScope scope, IEnumerable<string> ticked = null)
    {
        var all = p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name).ToList();
        var tk = ticked?.Where(all.Contains).Distinct().ToList() ?? new List<string>();
        List<string> res = scope switch
        {
            StoryScope.All => all,
            StoryScope.Similar => tk.Count > 0 ? tk.Union(new[] { current }).ToList() : StoriesSimilar(p, current),
            StoryScope.Selected => tk.Count > 0 ? tk : new List<string> { current },
            _ => new List<string> { current }
        };
        return all.Where(res.Contains).ToList();
    }

    /// <summary>Chạy một thao tác cho từng tầng trong danh sách; gộp kết quả (Ok nếu ít nhất một tầng thành công).</summary>
    public static EditResult ForStories(Project p, IEnumerable<string> stories, Func<string, EditResult> op)
    {
        var list = stories.ToList();
        if (list.Count == 0) return EditResult.Fail("Chưa chọn tầng áp dụng.");
        var parts = new List<(string st, EditResult r)>();
        foreach (var st in list) parts.Add((st, op(st)));
        var ok = parts.Where(q => q.r.Ok).ToList();
        if (list.Count == 1) return parts[0].r;
        var r = new EditResult { Ok = ok.Count > 0 };
        foreach (var q in ok) { r.NewFrames.AddRange(q.r.NewFrames); r.NewSlabs.AddRange(q.r.NewSlabs); }
        r.Message = r.Ok
            ? $"[{ok.Count}/{list.Count} tầng: {string.Join(", ", ok.Select(q => q.st))}] " + ok[0].r.Message
              + (ok.Count < list.Count ? $" – không thực hiện được ở tầng {string.Join(", ", parts.Where(q => !q.r.Ok).Select(q => q.st))}: {parts.First(q => !q.r.Ok).r.Message}" : "")
            : parts[0].r.Message;
        return r;
    }
}

// =====================================================================
// THƯ VIỆN TIẾT DIỆN (Define → Frame Sections)
// =====================================================================
public static class SectionLibrary
{
    /// <summary>Tên tự động theo kích thước (cm): D22x40, C30x30.</summary>
    public static string AutoName(string prefix, double bM, double hM) => ImportGeometry.RectName(string.IsNullOrEmpty(prefix) ? "D" : prefix, bM, hM);

    public static int UsageCount(Project p, string name) => p.Frames.Count(f => f.Section == name);

    public static string Unique(Project p, string baseName)
    {
        string n = baseName; int k = 2;
        while (p.FrameSections.Any(s => s.Name == n)) n = baseName + "_" + k++;
        return n;
    }

    /// <summary>Tên đang theo quy ước tự động (tiền tố + kích thước).</summary>
    public static bool IsAutoName(FrameSection s) =>
        s != null && s.Shape == SectionShape.RectConcrete && s.Name.Length > 0 && s.Name == AutoName(s.Name[..1], s.B, s.H);

    private static string CheckDims(double bMm, double hMm) =>
        double.IsNaN(bMm) || double.IsNaN(hMm) || bMm < 50 || hMm < 50 || bMm > 3000 || hMm > 3000 ? "Kích thước b, h phải từ 50 đến 3000 mm." : null;

    /// <summary>Thêm tiết diện chữ nhật BTCT (mm). Tên rỗng → tự đặt theo kích thước (prefix D dầm / C cột).</summary>
    public static EditResult AddRect(Project p, string name, double bMm, double hMm, string material, string prefix = "D")
    {
        var e = CheckDims(bMm, hMm);
        if (e != null) return EditResult.Fail(e);
        name = (name ?? "").Trim();
        if (name.Length == 0) name = Unique(p, AutoName(prefix, bMm / 1000, hMm / 1000));
        if (p.FrameSections.Any(s => s.Name == name)) return EditResult.Fail($"Đã có tiết diện tên {name}.");
        p.FrameSections.Add(FrameSection.Rect(name, bMm, hMm, string.IsNullOrEmpty(material) ? p.Concrete.ConcreteBeam : material));
        p.InvalidateIndex();
        return new EditResult { Ok = true, Message = $"Đã thêm tiết diện {name} ({bMm:0}×{hMm:0} mm, {material})." };
    }

    /// <summary>Sao chép tiết diện (mọi thuộc tính) sang tên mới (rỗng → tên cũ + "_2").</summary>
    public static EditResult Copy(Project p, string name, string newName = null)
    {
        var s = p.FrameSections.FirstOrDefault(q => q.Name == name);
        if (s == null) return EditResult.Fail($"Không có tiết diện {name}.");
        newName = string.IsNullOrWhiteSpace(newName) ? Unique(p, name) : newName.Trim();
        if (p.FrameSections.Any(q => q.Name == newName)) return EditResult.Fail($"Đã có tiết diện tên {newName}.");
        var c = Clone(s); c.Name = newName;
        p.FrameSections.Insert(p.FrameSections.IndexOf(s) + 1, c);
        p.InvalidateIndex();
        return new EditResult { Ok = true, Message = $"Đã sao chép {name} → {newName}." };
    }

    public static FrameSection Clone(FrameSection s) =>
        JsonSerializer.Deserialize<FrameSection>(JsonSerializer.Serialize(s, Project.JsonOptions), Project.JsonOptions);

    /// <summary>Sửa tiết diện chữ nhật: kích thước (mm), vật liệu, hệ số độ cứng, màu. autoRename = đổi tên theo kích thước mới nếu
    /// tên đang tự động.</summary>
    public static EditResult Modify(Project p, string name, double bMm, double hMm, string material, double modI33 = 1, double modI22 = 1, double modJ = 1, double modA = 1,
                                    string color = null, bool autoRename = true)
    {
        var s = p.FrameSections.FirstOrDefault(q => q.Name == name);
        if (s == null) return EditResult.Fail($"Không có tiết diện {name}.");
        if (s.Shape == SectionShape.RectConcrete)
        {
            var e = CheckDims(bMm, hMm);
            if (e != null) return EditResult.Fail(e);
        }
        foreach (var m in new[] { modI33, modI22, modJ, modA })
            if (double.IsNaN(m) || m <= 0 || m > 10) return EditResult.Fail("Hệ số điều chỉnh độ cứng phải trong khoảng (0; 10].");
        bool auto = autoRename && IsAutoName(s);
        if (s.Shape == SectionShape.RectConcrete) { s.B = bMm / 1000; s.H = hMm / 1000; }
        if (!string.IsNullOrEmpty(material)) s.Material = material;
        s.ModI33 = modI33; s.ModI22 = modI22; s.ModJ = modJ; s.ModA = modA;
        s.DisplayColor = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
        p.InvalidateIndex();
        string msg = $"Đã sửa tiết diện {name}.";
        if (auto)
        {
            string nn = AutoName(name[..1], s.B, s.H);
            if (nn != name && !p.FrameSections.Any(q => q.Name == nn)) { var rr = Rename(p, name, nn); msg = $"Đã sửa tiết diện {name} → {nn}" + (rr.Ok ? "" : $" ({rr.Message})") + "."; }
        }
        return new EditResult { Ok = true, Message = msg };
    }

    /// <summary>Đổi tên tiết diện, cập nhật mọi thanh đang dùng.</summary>
    public static EditResult Rename(Project p, string oldName, string newName)
    {
        newName = (newName ?? "").Trim();
        var s = p.FrameSections.FirstOrDefault(q => q.Name == oldName);
        if (s == null) return EditResult.Fail($"Không có tiết diện {oldName}.");
        if (newName.Length == 0) return EditResult.Fail("Tên tiết diện không được để trống.");
        if (newName == oldName) return new EditResult { Ok = true, Message = "Tên không đổi." };
        if (p.FrameSections.Any(q => q.Name == newName)) return EditResult.Fail($"Đã có tiết diện tên {newName}.");
        int n = 0;
        foreach (var f in p.Frames.Where(f => f.Section == oldName)) { f.Section = newName; n++; }
        s.Name = newName;
        p.InvalidateIndex();
        return new EditResult { Ok = true, Message = $"Đã đổi tên {oldName} → {newName} ({n} thanh cập nhật)." };
    }

    /// <summary>Xoá tiết diện. Đang được dùng: cần reassignTo (tiết diện khác) – các thanh được gán lại trước khi xoá.</summary>
    public static EditResult Delete(Project p, string name, string reassignTo = null)
    {
        var s = p.FrameSections.FirstOrDefault(q => q.Name == name);
        if (s == null) return EditResult.Fail($"Không có tiết diện {name}.");
        int used = UsageCount(p, name);
        if (used > 0)
        {
            if (string.IsNullOrEmpty(reassignTo)) return EditResult.Fail($"Tiết diện {name} đang được {used} thanh sử dụng – chọn tiết diện thay thế để gán lại trước khi xoá.");
            if (reassignTo == name || !p.FrameSections.Any(q => q.Name == reassignTo)) return EditResult.Fail($"Tiết diện thay thế \"{reassignTo}\" không hợp lệ.");
            foreach (var f in p.Frames.Where(f => f.Section == name)) f.Section = reassignTo;
        }
        p.FrameSections.Remove(s);
        p.InvalidateIndex();
        return new EditResult { Ok = true, Message = used > 0 ? $"Đã gán {used} thanh sang {reassignTo} và xoá tiết diện {name}." : $"Đã xoá tiết diện {name}." };
    }

    /// <summary>Nhập tiết diện từ dự án khác (names null = tất cả); trùng tên → bỏ qua (overwrite = false) hoặc ghi đè thuộc tính.</summary>
    public static EditResult Import(Project p, Project src, IEnumerable<string> names = null, bool overwrite = false)
    {
        if (src == null) return EditResult.Fail("Không có dự án nguồn.");
        var set = names == null ? null : new HashSet<string>(names);
        int add = 0, upd = 0, skip = 0;
        foreach (var s in src.FrameSections.Where(s => set == null || set.Contains(s.Name)))
        {
            var ex = p.FrameSections.FirstOrDefault(q => q.Name == s.Name);
            if (ex != null)
            {
                if (!overwrite) { skip++; continue; }
                int i = p.FrameSections.IndexOf(ex);
                p.FrameSections[i] = Clone(s); upd++;
                continue;
            }
            p.FrameSections.Add(Clone(s)); add++;
            if (!string.IsNullOrEmpty(s.Material) && p.Materials.All(m => m.Name != s.Material))
            {
                var m = src.Materials.FirstOrDefault(q => q.Name == s.Material);
                if (m != null) p.Materials.Add(JsonSerializer.Deserialize<Material>(JsonSerializer.Serialize(m, Project.JsonOptions), Project.JsonOptions));
            }
        }
        p.InvalidateIndex();
        bool ok = add + upd > 0;
        return new EditResult { Ok = ok, Message = ok ? $"Đã nhập {add} tiết diện" + (upd > 0 ? $", ghi đè {upd}" : "") + (skip > 0 ? $", bỏ qua {skip} trùng tên" : "") + "." : $"Không nhập tiết diện nào ({skip} trùng tên)." };
    }
}
