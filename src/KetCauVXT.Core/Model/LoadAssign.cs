namespace KetCauVXT.Model;

// =====================================================================
// GÁN TẢI CHO NHIỀU ĐỐI TƯỢNG – bổ sung ModelAssign (LoadAssignMode ở Model/ModelAssign.cs) (tab Móng / Cột / Dầm / Sàn / Tường – Tải trên mặt bằng, MainWindow.MemberRibbon.cs):
//   • theo trường hợp tải (LoadPattern), chế độ Thay thế | Cộng thêm | Xoá – chỉ đụng tới tải NHẬP TAY (Source = "")
//     và chỉ cùng DẠNG tải (thay tải phân bố không xoá lực tập trung và ngược lại – khác ModelAssign);
//   • dầm: tải phân bố đều (kN/m), tải tập trung (kN tại x/L); cột: lực đầu cột Fz, mô men Mx, My (nút đỉnh);
//     móng: tải chân cột (nút chân); sàn: tải phân bố (kN/m²);
//   • đọc giá trị chung của các đối tượng đang chọn ("&lt;khác nhau&gt;" khi khác nhau);
//   • tải tường: q = g_tường(kN/m²) · h · (1 − tỷ lệ cửa); tường trên sàn quy đổi phân bố = q · L / A;
//   • "Tường từ CAD → tải dầm": cặp nét song song cách 100–130 / 190–240 mm trên bản vẽ liên kết nằm trên dầm → đề xuất tường.
// =====================================================================

public sealed class LoadAssignResult
{
    public bool Ok { get; set; } = true;
    public string Message { get; set; } = "";
    /// <summary>Số đối tượng đã đổi tải.</summary>
    public int Changed { get; set; }
}

/// <summary>Đề xuất tường từ bản vẽ CAD nằm trên một dầm.</summary>
public sealed class CadWallProposal
{
    public int BeamId { get; set; }
    /// <summary>Chiều dày tường (m): 0,11 hoặc 0,22</summary>
    public double Thickness { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public double Length => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
    public override string ToString() => $"Dầm #{BeamId}: tường {Thickness * 1000:0} dài {Length:0.00} m";
}

public static class LoadAssign
{
    public const string Different = "<khác nhau>";

    private static bool IsUser(string src) => string.IsNullOrEmpty(src);
    private static bool IsUniform(FrameLoad l) => l.Points.Count >= 2 && l.PointLoads.Count == 0;
    private static bool IsPointOnly(FrameLoad l) => l.Points.Count == 0 && l.PointLoads.Count > 0;

    private static string ModeText(LoadAssignMode m) => m switch { LoadAssignMode.Add => "cộng thêm", LoadAssignMode.Delete => "xoá", _ => "thay thế" };

    private static LoadAssignResult Check(Project p, string pattern)
    {
        if (p == null) return new LoadAssignResult { Ok = false, Message = "Chưa có mô hình." };
        if (string.IsNullOrWhiteSpace(pattern) || p.Patterns.All(q => q.Name != pattern))
            return new LoadAssignResult { Ok = false, Message = $"Không có trường hợp tải \"{pattern}\"." };
        return null;
    }

    // ------------------------------------------------------------------
    // Dầm
    // ------------------------------------------------------------------
    /// <summary>Tải phân bố đều q (kN/m, dương = hướng xuống theo trọng lực) cho các thanh.</summary>
    public static LoadAssignResult FrameUniform(Project p, IEnumerable<int> frames, string pattern, double q, LoadAssignMode mode,
        LoadDir dir = LoadDir.Gravity, string note = "")
    {
        var bad = Check(p, pattern); if (bad != null) return bad;
        var ids = (frames ?? Enumerable.Empty<int>()).Distinct().Where(id => p.Frames.Any(f => f.Id == id)).ToList();
        if (ids.Count == 0) return new LoadAssignResult { Ok = false, Message = "Chưa chọn dầm / thanh nào." };
        foreach (int id in ids)
        {
            if (mode != LoadAssignMode.Add)
                p.FrameLoads.RemoveAll(l => l.Element == id && l.Pattern == pattern && IsUser(l.Source) && IsUniform(l) && l.Dir == dir);
            if (mode != LoadAssignMode.Delete && Math.Abs(q) > 1e-12)
                p.FrameLoads.Add(FrameLoad.Uniform(id, pattern, q, dir, note));
        }
        return new LoadAssignResult
        {
            Changed = ids.Count,
            Message = mode == LoadAssignMode.Delete ? $"Đã xoá tải phân bố {pattern} trên {ids.Count} thanh."
                : $"Đã gán ({ModeText(mode)}) tải phân bố {q:0.###} kN/m – {pattern} cho {ids.Count} thanh."
        };
    }

    /// <summary>Lực tập trung P (kN) tại vị trí tương đối rel (0…1) trên các thanh.</summary>
    public static LoadAssignResult FramePoint(Project p, IEnumerable<int> frames, string pattern, double P, double rel, LoadAssignMode mode,
        LoadDir dir = LoadDir.Gravity, string note = "")
    {
        var bad = Check(p, pattern); if (bad != null) return bad;
        var ids = (frames ?? Enumerable.Empty<int>()).Distinct().Where(id => p.Frames.Any(f => f.Id == id)).ToList();
        if (ids.Count == 0) return new LoadAssignResult { Ok = false, Message = "Chưa chọn dầm / thanh nào." };
        rel = Math.Clamp(rel, 0, 1);
        foreach (int id in ids)
        {
            if (mode != LoadAssignMode.Add)
                p.FrameLoads.RemoveAll(l => l.Element == id && l.Pattern == pattern && IsUser(l.Source) && IsPointOnly(l) && l.Dir == dir);
            if (mode != LoadAssignMode.Delete && Math.Abs(P) > 1e-12)
                p.FrameLoads.Add(new FrameLoad { Element = id, Pattern = pattern, Dir = dir, Note = note, PointLoads = { new[] { rel, P } } });
        }
        return new LoadAssignResult
        {
            Changed = ids.Count,
            Message = mode == LoadAssignMode.Delete ? $"Đã xoá lực tập trung {pattern} trên {ids.Count} thanh."
                : $"Đã gán ({ModeText(mode)}) lực tập trung {P:0.###} kN tại {rel:0.##}L – {pattern} cho {ids.Count} thanh."
        };
    }

    /// <summary>Tổng tải phân bố đều nhập tay (kN/m, trung bình các điểm) của thanh theo trường hợp; null = không có.</summary>
    public static double? FrameUniformOf(Project p, int frame, string pattern)
    {
        var ls = p.FrameLoads.Where(l => l.Element == frame && l.Pattern == pattern && IsUser(l.Source) && IsUniform(l) && l.Dir == LoadDir.Gravity).ToList();
        if (ls.Count == 0) return null;
        return ls.Sum(l => l.Points.Average(q => q[1]));
    }

    // ------------------------------------------------------------------
    // Sàn
    // ------------------------------------------------------------------
    /// <summary>Tải phân bố đều q (kN/m²) cho các ô sàn.</summary>
    public static LoadAssignResult Slabs(Project p, IEnumerable<int> slabs, string pattern, double q, LoadAssignMode mode, string note = "")
    {
        var bad = Check(p, pattern); if (bad != null) return bad;
        var ids = (slabs ?? Enumerable.Empty<int>()).Distinct().Where(id => p.Slabs.Any(s => s.Id == id)).ToList();
        if (ids.Count == 0) return new LoadAssignResult { Ok = false, Message = "Chưa chọn ô sàn nào." };
        foreach (int id in ids)
        {
            if (mode != LoadAssignMode.Add) p.AreaLoads.RemoveAll(a => a.Slab == id && a.Pattern == pattern && IsUser(a.Source));
            if (mode != LoadAssignMode.Delete && Math.Abs(q) > 1e-12) p.AreaLoads.Add(new AreaLoad { Slab = id, Pattern = pattern, Q = q, Note = note });
        }
        return new LoadAssignResult
        {
            Changed = ids.Count,
            Message = mode == LoadAssignMode.Delete ? $"Đã xoá tải sàn {pattern} của {ids.Count} ô."
                : $"Đã gán ({ModeText(mode)}) tải sàn {q:0.###} kN/m² – {pattern} cho {ids.Count} ô sàn."
        };
    }

    /// <summary>Tổng tải sàn nhập tay (kN/m²); null = không có.</summary>
    public static double? SlabQOf(Project p, int slab, string pattern)
    {
        var ls = p.AreaLoads.Where(a => a.Slab == slab && a.Pattern == pattern && IsUser(a.Source)).ToList();
        return ls.Count == 0 ? null : ls.Sum(a => a.Q);
    }

    // ------------------------------------------------------------------
    // Nút: đầu cột / chân cột (móng)
    // ------------------------------------------------------------------
    /// <summary>Lực nút: Fz (kN, dương = hướng xuống theo trọng lực → lưu Fz âm), Mx, My (kN.m).</summary>
    public static LoadAssignResult Nodes(Project p, IEnumerable<int> nodes, string pattern, double fzDown, double mx, double my, LoadAssignMode mode)
    {
        var bad = Check(p, pattern); if (bad != null) return bad;
        var ids = (nodes ?? Enumerable.Empty<int>()).Distinct().Where(id => p.Nodes.Any(n => n.Id == id)).ToList();
        if (ids.Count == 0) return new LoadAssignResult { Ok = false, Message = "Chưa chọn cột / chân cột nào." };
        bool zero = Math.Abs(fzDown) < 1e-12 && Math.Abs(mx) < 1e-12 && Math.Abs(my) < 1e-12;
        foreach (int id in ids)
        {
            if (mode != LoadAssignMode.Add) p.NodeLoads.RemoveAll(l => l.Node == id && l.Pattern == pattern && IsUser(l.Source));
            if (mode != LoadAssignMode.Delete && !zero) p.NodeLoads.Add(new NodeLoad { Node = id, Pattern = pattern, Fz = -fzDown, Mx = mx, My = my });
        }
        return new LoadAssignResult
        {
            Changed = ids.Count,
            Message = mode == LoadAssignMode.Delete ? $"Đã xoá tải nút {pattern} tại {ids.Count} nút."
                : $"Đã gán ({ModeText(mode)}) N = {fzDown:0.##} kN, Mx = {mx:0.##}, My = {my:0.##} kN.m – {pattern} cho {ids.Count} nút."
        };
    }

    /// <summary>Lực đứng nhập tay tại nút (kN, dương = xuống); null = không có.</summary>
    public static double? NodeFzOf(Project p, int node, string pattern)
    {
        var ls = p.NodeLoads.Where(l => l.Node == node && l.Pattern == pattern && IsUser(l.Source)).ToList();
        return ls.Count == 0 ? null : -ls.Sum(l => l.Fz);
    }

    /// <summary>Nút đỉnh (cho tải đầu cột) và nút chân (cho tải móng) của cột.</summary>
    public static (int Top, int Bottom) ColumnNodes(Project p, FrameElement col)
    {
        var a = N(p, col.NodeI); var b = N(p, col.NodeJ);
        if (a == null || b == null) return (col.NodeJ, col.NodeI);
        return a.Z >= b.Z ? (a.Id, b.Id) : (b.Id, a.Id);
    }

    // ------------------------------------------------------------------
    // Giá trị chung
    // ------------------------------------------------------------------
    /// <summary>Giá trị chung của nhiều đối tượng: "—" (không đối tượng), số (giống nhau, null = 0), hoặc "&lt;khác nhau&gt;".</summary>
    public static string Common(IEnumerable<double?> values, string unit = "", string format = "0.###")
    {
        var v = values.Select(x => Math.Round(x ?? 0, 6)).ToList();
        if (v.Count == 0) return "—";
        if (v.Distinct().Count() > 1) return Different;
        return v[0].ToString(format, System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + (unit.Length > 0 ? " " + unit : "");
    }

    // ------------------------------------------------------------------
    // Tải tường
    // ------------------------------------------------------------------
    /// <summary>Tải tường trên dầm (kN/m): g_tường (kN/m² mặt tường, gồm trát) · chiều cao · (1 − tỷ lệ cửa).</summary>
    public static double WallLineLoad(double weightPerM2, double height, double openingRatio = 0) =>
        weightPerM2 * Math.Max(0, height) * (1 - Math.Clamp(openingRatio, 0, 0.95));

    public static double WallLineLoad(WallType t, double height, double openingRatio = -1) =>
        t == null ? 0 : WallLineLoad(t.WeightPerM2, height, openingRatio < 0 ? t.OpeningRatio : openingRatio);

    /// <summary>Tường xây trực tiếp trên sàn (không có dầm đỡ) quy về tải phân bố đều trên ô (kN/m²): q_tường · L / A.</summary>
    public static double WallOnSlabEquivalent(double weightPerM2, double height, double length, double slabArea, double openingRatio = 0) =>
        slabArea <= 1e-9 ? 0 : WallLineLoad(weightPerM2, height, openingRatio) * Math.Max(0, length) / slabArea;

    /// <summary>Diện tích ô sàn (m², đa giác theo nút).</summary>
    public static double SlabArea(Project p, Slab s)
    {
        var pts = s.Nodes.Select(id => N(p, id)).Where(n => n != null).ToList();
        double a = 0;
        for (int i = 0; i < pts.Count; i++) { var u = pts[i]; var w = pts[(i + 1) % pts.Count]; a += u.X * w.Y - w.X * u.Y; }
        return Math.Abs(a) / 2;
    }

    /// <summary>Chọn chiều dày tường chuẩn theo khoảng cách hai nét song song (m); 0 = không phải tường.</summary>
    public static double WallThicknessFromGap(double gap)
    {
        if (gap >= 0.085 && gap <= 0.135) return 0.11;
        if (gap >= 0.185 && gap <= 0.245) return 0.22;
        if (gap >= 0.30 && gap <= 0.345) return 0.33;
        return 0;
    }

    /// <summary>Tường trên bản vẽ CAD (các nét thẳng trên layer tường, toạ độ mô hình, m) nằm trên dầm của tầng:
    /// ghép cặp nét song song cách 100–130 / 190–240 mm thành tim tường; tim tường song song dầm, lệch ≤ t/2 + 50 mm và
    /// chồng lên dầm ≥ minOverlap → đề xuất tường (phần chồng lên dầm). Mỗi dầm / chiều dày gộp thành một đoạn.</summary>
    public static List<CadWallProposal> CadWallsOverBeams(Project p, string story, IReadOnlyList<(double X1, double Y1, double X2, double Y2)> lines,
        double minOverlap = 0.3, double angleTolDeg = 3)
    {
        var res = new List<CadWallProposal>();
        if (p == null || lines == null || lines.Count < 2) return res;
        double cosTol = Math.Cos(angleTolDeg * Math.PI / 180);
        // 1) tim tường từ cặp nét song song
        var centres = new List<(double X1, double Y1, double X2, double Y2, double T)>();
        var segs = lines.Where(l => Hyp(l.X2 - l.X1, l.Y2 - l.Y1) >= minOverlap).ToList();
        for (int i = 0; i < segs.Count; i++)
        {
            var a = segs[i];
            double ax = a.X2 - a.X1, ay = a.Y2 - a.Y1, la = Hyp(ax, ay), ux = ax / la, uy = ay / la;
            for (int j = i + 1; j < segs.Count; j++)
            {
                var b = segs[j];
                double bx = b.X2 - b.X1, by = b.Y2 - b.Y1, lb = Hyp(bx, by);
                if (Math.Abs((ax * bx + ay * by) / (la * lb)) < cosTol) continue;
                // khoảng cách vuông góc từ b tới đường a
                double d1 = (b.X1 - a.X1) * -uy + (b.Y1 - a.Y1) * ux, d2 = (b.X2 - a.X1) * -uy + (b.Y2 - a.Y1) * ux;
                if (Math.Abs(d1 - d2) > 0.02) continue;
                double gap = Math.Abs((d1 + d2) / 2), t = WallThicknessFromGap(gap);
                if (t <= 0) continue;
                // chồng theo phương a
                double s0 = 0, s1 = la;
                double tb1 = (b.X1 - a.X1) * ux + (b.Y1 - a.Y1) * uy, tb2 = (b.X2 - a.X1) * ux + (b.Y2 - a.Y1) * uy;
                double o0 = Math.Max(s0, Math.Min(tb1, tb2)), o1 = Math.Min(s1, Math.Max(tb1, tb2));
                if (o1 - o0 < minOverlap) continue;
                double off = (d1 + d2) / 4;   // nửa khoảng cách có dấu → tim tường
                double cx = -uy * off, cy = ux * off;
                centres.Add((a.X1 + ux * o0 + cx, a.Y1 + uy * o0 + cy, a.X1 + ux * o1 + cx, a.Y1 + uy * o1 + cy, t));
            }
        }
        // 2) ghép với dầm
        var beams = p.Frames.Where(f => !f.IsVerticalKind && f.Story == story && Math.Abs(f.CurveMidOffset) < 1e-9).ToList();
        var acc = new Dictionary<(int, double), (double T0, double T1, double Ux, double Uy, double X0, double Y0)>();
        foreach (var f in beams)
        {
            var ni = N(p, f.NodeI); var nj = N(p, f.NodeJ);
            if (ni == null || nj == null) continue;
            double fx = nj.X - ni.X, fy = nj.Y - ni.Y, lf = Hyp(fx, fy);
            if (lf < 1e-6) continue;
            double ux = fx / lf, uy = fy / lf;
            foreach (var c in centres)
            {
                double cx = c.X2 - c.X1, cy = c.Y2 - c.Y1, lc = Hyp(cx, cy);
                if (lc < 1e-6 || Math.Abs((cx * ux + cy * uy) / lc) < cosTol) continue;
                double e1 = (c.X1 - ni.X) * -uy + (c.Y1 - ni.Y) * ux, e2 = (c.X2 - ni.X) * -uy + (c.Y2 - ni.Y) * ux;
                if (Math.Max(Math.Abs(e1), Math.Abs(e2)) > c.T / 2 + 0.05 + 1e-9) continue;
                double t1 = (c.X1 - ni.X) * ux + (c.Y1 - ni.Y) * uy, t2 = (c.X2 - ni.X) * ux + (c.Y2 - ni.Y) * uy;
                double o0 = Math.Max(0, Math.Min(t1, t2)), o1 = Math.Min(lf, Math.Max(t1, t2));
                if (o1 - o0 < minOverlap) continue;
                var key = (f.Id, c.T);
                acc[key] = acc.TryGetValue(key, out var old) ? (Math.Min(old.T0, o0), Math.Max(old.T1, o1), ux, uy, ni.X, ni.Y) : (o0, o1, ux, uy, ni.X, ni.Y);
            }
        }
        foreach (var kv in acc.OrderBy(k => k.Key.Item1))
        {
            var v = kv.Value;
            res.Add(new CadWallProposal
            {
                BeamId = kv.Key.Item1, Thickness = kv.Key.Item2,
                X1 = R4(v.X0 + v.Ux * v.T0), Y1 = R4(v.Y0 + v.Uy * v.T0), X2 = R4(v.X0 + v.Ux * v.T1), Y2 = R4(v.Y0 + v.Uy * v.T1)
            });
        }
        return res;
    }

    /// <summary>Loại tường mặc định cho chiều dày (đặc, trát 2 mặt nếu có).</summary>
    public static WallType WallTypeFor(Project p, double thickness) =>
        p.WallTypes.Where(w => Math.Abs(w.Thickness - thickness) < 0.015)
            .OrderBy(w => w.OpeningRatio > 0 ? 1 : 0).ThenBy(w => w.Finish == WallFinish.Plaster2Sides ? 0 : 1).FirstOrDefault();

    /// <summary>Tạo tường (WallLine) từ các đề xuất CAD – tải tường dồn vào dầm khi phân tích như tường nhập tay.</summary>
    public static EditResult ApplyCadWalls(Project p, string story, IEnumerable<CadWallProposal> props, double height = 0)
    {
        int n = 0;
        var miss = new List<string>();
        foreach (var w in props ?? Enumerable.Empty<CadWallProposal>())
        {
            var t = WallTypeFor(p, w.Thickness);
            if (t == null) { miss.Add($"{w.Thickness * 1000:0}"); continue; }
            var r = ModelEdit.AddWall(p, story, w.X1, w.Y1, w.X2, w.Y2, t.Name, height);
            if (r.Ok) n++;
        }
        return new EditResult
        {
            Ok = n > 0,
            Message = n > 0 ? $"Đã tạo {n} đoạn tường từ bản vẽ CAD trên dầm tầng {story} (tải tường dồn vào dầm khi phân tích)."
                              + (miss.Count > 0 ? $" Thiếu loại tường dày {string.Join(", ", miss.Distinct())} mm." : "")
                            : "Không tạo được tường nào" + (miss.Count > 0 ? $" – chưa khai báo loại tường dày {string.Join(", ", miss.Distinct())} mm." : ".")
        };
    }

    internal static Node N(Project p, int id) => p.Nodes.FirstOrDefault(n => n.Id == id);
    private static double Hyp(double x, double y) => Math.Sqrt(x * x + y * y);
    private static double R4(double v) => Math.Round(v, 4);
}
