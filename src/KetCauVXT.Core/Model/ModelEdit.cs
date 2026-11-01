using KetCauVXT.Import;

namespace KetCauVXT.Model;

/// <summary>Loại lỗ sàn: cầu thang (có tải bản thang truyền vào dầm viền), giếng trời / thông tầng (chỉ lan can).</summary>
public enum OpeningKind { Stair, Void }

/// <summary>Lỗ sàn trên một tầng (ô không có sàn). Lưu trong Project.Openings để vẽ và để khôi phục sàn.</summary>
public class FloorOpening
{
    public int Id { get; set; }
    public string Story { get; set; } = "";
    public OpeningKind Kind { get; set; }
    public double X0 { get; set; }
    public double Y0 { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public string Note { get; set; } = "";
    public string Name => Kind == OpeningKind.Stair ? "Cầu thang" : "Giếng trời";
    public bool Contains(double x, double y) => x >= X0 - 1e-6 && x <= X1 + 1e-6 && y >= Y0 - 1e-6 && y <= Y1 + 1e-6;
}

/// <summary>Công năng ô sàn – quyết định tải hoàn thiện và hoạt tải (kN/m², TCVN 2737:2023).</summary>
public enum SlabUse { Normal, WC, Balcony, Corridor, Storage, Roof }

public class EditResult
{
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
    public List<int> NewFrames { get; } = new();
    public List<int> NewSlabs { get; } = new();
    public static EditResult Fail(string m) => new() { Ok = false, Message = m };
}

/// <summary>Các thao tác sửa mô hình trên mặt bằng tầng: thêm/xoá ô sàn, đổi công năng (sàn vệ sinh…), thêm ban công công xôn,
/// thêm ô cầu thang / giếng trời. Mọi thao tác giữ mô hình hợp lệ để chạy lại phân tích.</summary>
public static class ModelEdit
{
    public static (double Finish, double Live, string Name) Loads(SlabUse u) => u switch
    {
        SlabUse.WC => (2.0, 1.5, "Sàn vệ sinh (hạ cốt, chống thấm)"),
        SlabUse.Balcony => (1.2, 2.0, "Ban công, lô gia"),
        SlabUse.Corridor => (1.2, 3.0, "Sảnh, hành lang"),
        SlabUse.Storage => (1.2, 4.0, "Kho"),
        SlabUse.Roof => (2.5, 0.75, "Mái BTCT"),
        _ => (1.2, 1.5, "Phòng ở"),
    };

    public static double StoryZ(Project p, string story) =>
        p.Stories.FirstOrDefault(s => s.Name == story)?.Elevation ?? throw new ArgumentException("Không có tầng " + story);

    private static bool AtZ(Project p, FrameElement f, double z) =>
        Math.Abs(p.Node(f.NodeI).Z - z) < 0.01 && Math.Abs(p.Node(f.NodeJ).Z - z) < 0.01;

    /// <summary>Các dầm (thanh ngang) nằm ở cao độ tầng.</summary>
    public static List<FrameElement> StoryBeams(Project p, string story)
    {
        double z = StoryZ(p, story);
        return p.Frames.Where(f => !f.IsVerticalKind && AtZ(p, f, z)).ToList();
    }

    /// <summary>Ô chữ nhật được bao bởi dầm chứa điểm (x, y) trên tầng; null nếu không kín.</summary>
    public static (double X0, double Y0, double X1, double Y1)? PanelAt(Project p, string story, double x, double y)
    {
        var beams = StoryBeams(p, story);
        if (beams.Count == 0) return null;
        var segs = beams.Select(f => { var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ); return new Seg2(a.X, a.Y, b.X, b.Y); }).ToList();
        double x0 = segs.Min(s => Math.Min(s.Ax, s.Bx)), x1 = segs.Max(s => Math.Max(s.Ax, s.Bx));
        double y0 = segs.Min(s => Math.Min(s.Ay, s.By)), y1 = segs.Max(s => Math.Max(s.Ay, s.By));
        if (x < x0 || x > x1 || y < y0 || y > y1) return null;
        var cells = ImportGeometry.RectCells(segs, x0, y0, x1, y1, true, null, "");
        foreach (var c in cells)
            if (x > c.x0 && x < c.x1 && y > c.y0 && y < c.y1) return (c.x0, c.y0, c.x1, c.y1);
        return null;
    }

    public static Slab SlabAt(Project p, string story, double x, double y)
    {
        foreach (var s in p.Slabs.Where(s => s.Story == story))
        {
            var pts = s.Nodes.Select(id => p.Node(id)).ToList();
            if (pts.Count >= 3 && Inside(pts.Select(n => (n.X, n.Y)).ToList(), x, y)) return s;
        }
        return null;
    }

    private static bool Inside(List<(double X, double Y)> poly, double x, double y)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X) c = !c;
        }
        return c;
    }

    private static (double X0, double Y0, double X1, double Y1) Bounds(Project p, Slab s)
    {
        var n = s.Nodes.Select(id => p.Node(id)).ToList();
        return (n.Min(q => q.X), n.Min(q => q.Y), n.Max(q => q.X), n.Max(q => q.Y));
    }

    // ------------------------------------------------------------------
    // Ô sàn
    // ------------------------------------------------------------------
    /// <summary>Thêm ô sàn vào ô kín bởi dầm chứa điểm bấm.</summary>
    public static EditResult AddSlab(Project p, string story, double x, double y, SlabUse use = SlabUse.Normal, string section = null)
    {
        if (SlabAt(p, story, x, y) != null) return EditResult.Fail("Vị trí này đã có ô sàn.");
        var op = p.Openings.FirstOrDefault(o => o.Story == story && o.Contains(x, y));
        if (op != null) return EditResult.Fail($"Vị trí này là {op.Name.ToLowerInvariant()} – xoá lỗ sàn trước.");
        var pn = PanelAt(p, story, x, y);
        if (pn == null) return EditResult.Fail("Không tìm thấy ô kín bởi dầm tại vị trí bấm – thêm dầm bao quanh trước.");
        var r = new EditResult { Ok = true };
        var s = CreateSlab(p, story, pn.Value, use, section);
        r.NewSlabs.Add(s.Id);
        r.Message = $"Đã thêm ô sàn {s.Label} ({pn.Value.X1 - pn.Value.X0:0.00}×{pn.Value.Y1 - pn.Value.Y0:0.00} m, {Loads(use).Name}).";
        return r;
    }

    private static Slab CreateSlab(Project p, string story, (double X0, double Y0, double X1, double Y1) r, SlabUse use, string section)
    {
        double z = StoryZ(p, story);
        var n1 = p.GetOrAddNode(r.X0, r.Y0, z, story); var n2 = p.GetOrAddNode(r.X1, r.Y0, z, story);
        var n3 = p.GetOrAddNode(r.X1, r.Y1, z, story); var n4 = p.GetOrAddNode(r.X0, r.Y1, z, story);
        section ??= p.Slabs.FirstOrDefault(q => q.Story == story)?.Section ?? p.SlabSections.FirstOrDefault()?.Name ?? ImportGeometry.EnsureSlabSection(p, 0.1, p.Concrete.ConcreteSlab);
        int k = 1;
        while (p.Slabs.Any(q => q.Label == $"S{story}-{k}")) k++;
        var s = new Slab { Id = p.NextSlabId(), Label = $"S{story}-{k}", Story = story, Section = section, Nodes = new List<int> { n1.Id, n2.Id, n3.Id, n4.Id } };
        p.Slabs.Add(s);
        SetLoads(p, s, use);
        ModelBuilder.SplitFramesAtNodes(p);
        p.InvalidateIndex();
        return s;
    }

    private static void SetLoads(Project p, Slab s, SlabUse use)
    {
        ImportGeometry.EnsurePatterns(p);
        p.AreaLoads.RemoveAll(a => a.Slab == s.Id && (a.Pattern == "HTHT" || a.Pattern == "HT"));
        var (fin, live, name) = Loads(use);
        p.AreaLoads.Add(new AreaLoad { Slab = s.Id, Pattern = "HTHT", Q = fin, Note = "Hoàn thiện – " + name });
        p.AreaLoads.Add(new AreaLoad { Slab = s.Id, Pattern = "HT", Q = live, Note = "Hoạt tải – " + name });
    }

    /// <summary>Đổi công năng ô sàn (vd. sàn vệ sinh: hoàn thiện 2,0 kN/m² gồm lớp chống thấm và vữa tạo dốc/hạ cốt).</summary>
    public static EditResult SetSlabUse(Project p, Slab s, SlabUse use)
    {
        if (s == null) return EditResult.Fail("Chưa chọn ô sàn.");
        SetLoads(p, s, use);
        var (fin, live, name) = Loads(use);
        return new EditResult { Ok = true, Message = $"{s.Label}: {name} – hoàn thiện {fin:0.##} kN/m², hoạt tải {live:0.##} kN/m²." };
    }

    public static SlabUse UseOf(Project p, Slab s)
    {
        var live = p.AreaLoads.Where(a => a.Slab == s.Id && a.Pattern == "HT").Sum(a => a.Q);
        var fin = p.AreaLoads.Where(a => a.Slab == s.Id && a.Pattern == "HTHT").Sum(a => a.Q);
        foreach (SlabUse u in Enum.GetValues(typeof(SlabUse)))
        {
            var l = Loads(u);
            if (Math.Abs(l.Live - live) < 1e-6 && Math.Abs(l.Finish - fin) < 1e-6) return u;
        }
        return SlabUse.Normal;
    }

    public static EditResult DeleteSlab(Project p, Slab s)
    {
        if (s == null) return EditResult.Fail("Chưa chọn ô sàn.");
        p.Slabs.Remove(s);
        p.AreaLoads.RemoveAll(a => a.Slab == s.Id);
        p.InvalidateIndex();
        return new EditResult { Ok = true, Message = $"Đã xoá ô sàn {s.Label}." };
    }

    // ------------------------------------------------------------------
    // Lỗ sàn: cầu thang, giếng trời
    // ------------------------------------------------------------------
    /// <summary>Biến ô (sàn) tại điểm bấm thành cầu thang hoặc giếng trời: bỏ sàn, ghi lỗ sàn, đặt tải lên dầm viền.
    /// Cầu thang: bản thang + bậc + hoàn thiện ≈ 6,5 kN/m², hoạt tải 3,0 kN/m² (nửa vế thang truyền về cao trình này).
    /// Giếng trời: lan can 0,5 kN/m quanh lỗ.</summary>
    public static EditResult AddOpening(Project p, string story, double x, double y, OpeningKind kind)
    {
        if (p.Openings.Any(o => o.Story == story && o.Contains(x, y))) return EditResult.Fail("Vị trí này đã là lỗ sàn.");
        var s = SlabAt(p, story, x, y);
        (double X0, double Y0, double X1, double Y1) r;
        if (s != null) { r = Bounds(p, s); DeleteSlab(p, s); }
        else
        {
            var pn = PanelAt(p, story, x, y);
            if (pn == null) return EditResult.Fail("Không tìm thấy ô kín bởi dầm tại vị trí bấm.");
            r = pn.Value;
        }
        var o = new FloorOpening { Id = p.Openings.Select(q => q.Id).DefaultIfEmpty(0).Max() + 1, Story = story, Kind = kind, X0 = r.X0, Y0 = r.Y0, X1 = r.X1, Y1 = r.Y1 };
        p.Openings.Add(o);
        ImportGeometry.EnsurePatterns(p);
        var edge = EdgeBeams(p, story, r);
        double len = edge.Sum(f => p.Length(f));
        double area = (r.X1 - r.X0) * (r.Y1 - r.Y0);
        string src = "OPEN" + o.Id;
        if (len > 0.1)
            foreach (var f in edge)
            {
                if (kind == OpeningKind.Stair)
                {
                    p.FrameLoads.Add(Src(FrameLoad.Uniform(f.Id, "HTHT", Math.Round(0.5 * area * 6.5 / len, 3), LoadDir.Gravity, "Cầu thang (bản + bậc + hoàn thiện)"), src));
                    p.FrameLoads.Add(Src(FrameLoad.Uniform(f.Id, "HT", Math.Round(0.5 * area * 3.0 / len, 3), LoadDir.Gravity, "Hoạt tải cầu thang"), src));
                }
                else p.FrameLoads.Add(Src(FrameLoad.Uniform(f.Id, "HTHT", 0.5, LoadDir.Gravity, "Lan can giếng trời"), src));
            }
        o.Note = kind == OpeningKind.Stair ? $"Cầu thang {r.X1 - r.X0:0.00}×{r.Y1 - r.Y0:0.00} m – tải truyền vào {edge.Count} dầm viền" : $"Giếng trời {r.X1 - r.X0:0.00}×{r.Y1 - r.Y0:0.00} m";
        p.InvalidateIndex();
        return new EditResult { Ok = true, Message = o.Note + "." };
    }

    private static FrameLoad Src(FrameLoad l, string s) { l.Source = s; return l; }

    /// <summary>Dầm của tầng nằm trên biên hình chữ nhật.</summary>
    public static List<FrameElement> EdgeBeams(Project p, string story, (double X0, double Y0, double X1, double Y1) r)
    {
        var res = new List<FrameElement>();
        foreach (var f in StoryBeams(p, story))
        {
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            double mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2;
            bool hx = Math.Abs(a.Y - b.Y) < 1e-6 && (Math.Abs(a.Y - r.Y0) < 0.02 || Math.Abs(a.Y - r.Y1) < 0.02) && mx > r.X0 - 0.01 && mx < r.X1 + 0.01;
            bool vy = Math.Abs(a.X - b.X) < 1e-6 && (Math.Abs(a.X - r.X0) < 0.02 || Math.Abs(a.X - r.X1) < 0.02) && my > r.Y0 - 0.01 && my < r.Y1 + 0.01;
            if (hx || vy) res.Add(f);
        }
        return res;
    }

    /// <summary>Xoá lỗ sàn: gỡ tải viền và đặt lại ô sàn.</summary>
    public static EditResult RemoveOpening(Project p, FloorOpening o)
    {
        if (o == null) return EditResult.Fail("Chưa chọn lỗ sàn.");
        p.FrameLoads.RemoveAll(l => l.Source == "OPEN" + o.Id);
        p.Openings.Remove(o);
        var r = AddSlab(p, o.Story, (o.X0 + o.X1) / 2, (o.Y0 + o.Y1) / 2);
        return new EditResult { Ok = true, Message = $"Đã xoá {o.Name.ToLowerInvariant()} – " + (r.Ok ? r.Message : "chưa đặt lại được sàn: " + r.Message) };
    }

    // ------------------------------------------------------------------
    // Ban công công xôn
    // ------------------------------------------------------------------
    /// <summary>Thêm ban công dọc một dầm biên: dầm công xôn tại mọi nút có cột trên đường dầm (và hai đầu), dầm biên đầu công xôn,
    /// ô sàn ban công (tải ban công), lan can trên dầm biên và hai dầm đầu (kN/m, mặc định 1,0 – lan can sắt + bo; tường 110 cao 1 m ≈ 2,5).</summary>
    public static EditResult AddBalcony(Project p, string story, FrameElement edgeBeam, double depth, double railing = 1.0)
    {
        if (edgeBeam == null || edgeBeam.IsVerticalKind) return EditResult.Fail("Chọn một dầm biên trước.");
        if (depth < 0.5 || depth > 3.0) return EditResult.Fail("Độ vươn ban công phải từ 0,5 đến 3,0 m.");
        double z = StoryZ(p, story);
        if (!AtZ(p, edgeBeam, z)) return EditResult.Fail($"Dầm đã chọn không thuộc tầng {story}.");
        var a0 = p.Node(edgeBeam.NodeI); var b0 = p.Node(edgeBeam.NodeJ);
        bool alongX = Math.Abs(a0.Y - b0.Y) < 1e-6, alongY = Math.Abs(a0.X - b0.X) < 1e-6;
        if (!alongX && !alongY) return EditResult.Fail("Chỉ hỗ trợ dầm song song trục X hoặc Y.");
        double c = alongX ? a0.Y : a0.X;
        // chuỗi dầm thẳng hàng liền nhau
        var line = StoryBeams(p, story).Where(f =>
        {
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            return alongX ? Math.Abs(a.Y - c) < 1e-3 && Math.Abs(b.Y - c) < 1e-3 : Math.Abs(a.X - c) < 1e-3 && Math.Abs(b.X - c) < 1e-3;
        }).ToList();
        double T(Node n) => alongX ? n.X : n.Y;
        double lo = Math.Min(T(a0), T(b0)), hi = Math.Max(T(a0), T(b0));
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (var f in line)
            {
                double t1 = Math.Min(T(p.Node(f.NodeI)), T(p.Node(f.NodeJ))), t2 = Math.Max(T(p.Node(f.NodeI)), T(p.Node(f.NodeJ)));
                if (t1 < lo - 1e-6 && t2 >= lo - 1e-3) { lo = t1; grew = true; }
                if (t2 > hi + 1e-6 && t1 <= hi + 1e-3) { hi = t2; grew = true; }
            }
        }
        // phía ngoài: phía không có sàn
        double mid = (lo + hi) / 2;
        bool plusIn = SlabAt(p, story, alongX ? mid : c + 0.3, alongX ? c + 0.3 : mid) != null;
        bool minusIn = SlabAt(p, story, alongX ? mid : c - 0.3, alongX ? c - 0.3 : mid) != null;
        if (plusIn && minusIn) return EditResult.Fail("Dầm đã chọn nằm giữa hai ô sàn – chọn dầm biên ngoài nhà.");
        int dir = plusIn ? -1 : minusIn ? 1 : 0;
        if (dir == 0)
        {
            // không có sàn hai bên: hướng ra xa tâm nhà
            double cen = alongX ? p.Nodes.Average(n => n.Y) : p.Nodes.Average(n => n.X);
            dir = c >= cen ? 1 : -1;
        }
        // điểm đặt công xôn: hai đầu + nút có cột
        var stations = new List<double> { lo, hi };
        foreach (var f in p.Frames.Where(f => f.IsVerticalKind))
        {
            var top = p.Node(f.NodeJ).Z >= p.Node(f.NodeI).Z ? p.Node(f.NodeJ) : p.Node(f.NodeI);
            var bot = top == p.Node(f.NodeJ) ? p.Node(f.NodeI) : p.Node(f.NodeJ);
            foreach (var n in new[] { top, bot })
                if (Math.Abs(n.Z - z) < 0.01 && Math.Abs((alongX ? n.Y : n.X) - c) < 1e-3 && T(n) > lo + 1e-3 && T(n) < hi - 1e-3) stations.Add(T(n));
        }
        stations = ImportGeometry.Cluster(stations, 0.05);
        if (hi - lo < 0.5) return EditResult.Fail("Đoạn dầm biên quá ngắn.");

        var r = new EditResult { Ok = true };
        double hc = Math.Max(0.30, Math.Ceiling(depth / 5 * 20 - 1e-6) / 20);
        string conc = p.Concrete.ConcreteBeam;
        string secC = ImportGeometry.EnsureRect(p, "D", 0.22, hc, conc);
        string secE = ImportGeometry.EnsureRect(p, "D", 0.22, 0.30, conc);
        ImportGeometry.EnsurePatterns(p);
        int g = 1;
        while (p.Frames.Any(f => f.DesignGroup == $"D{story}-BC{g}")) g++;
        string grp = $"D{story}-BC{g}";
        (double X, double Y) Pt(double t, double off) => alongX ? (t, c + dir * off) : (c + dir * off, t);
        var tips = new List<Node>();
        foreach (double t in stations)
        {
            var (bx, by) = Pt(t, 0); var (tx, ty) = Pt(t, depth);
            var nb = p.GetOrAddNode(bx, by, z, story); var nt = p.GetOrAddNode(tx, ty, z, story);
            tips.Add(nt);
            var f = ImportGeometry.AddFrame(p, nb, nt, FrameKind.Beam, secC, story, $"{grp}-CX", grp + "-CX");
            if (f != null)
            {
                r.NewFrames.Add(f.Id);
                if (t == stations[0] || t == stations[^1]) p.FrameLoads.Add(Src(FrameLoad.Uniform(f.Id, "HTHT", railing, LoadDir.Gravity, "Lan can ban công"), "BALCONY"));
            }
        }
        for (int i = 0; i + 1 < tips.Count; i++)
        {
            var f = ImportGeometry.AddFrame(p, tips[i], tips[i + 1], FrameKind.Beam, secE, story, $"{grp}-BIEN", grp + "-BIEN");
            if (f == null) continue;
            r.NewFrames.Add(f.Id);
            p.FrameLoads.Add(Src(FrameLoad.Uniform(f.Id, "HTHT", railing, LoadDir.Gravity, "Lan can ban công"), "BALCONY"));
        }
        ModelBuilder.SplitFramesAtNodes(p);
        p.InvalidateIndex();
        // ô sàn ban công
        for (int i = 0; i + 1 < stations.Count; i++)
        {
            var (x1, y1) = Pt(stations[i], 0); var (x2, y2) = Pt(stations[i + 1], depth);
            var rect = (Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));
            var s = CreateSlab(p, story, rect, SlabUse.Balcony, null);
            r.NewSlabs.Add(s.Id);
        }
        if (depth > 2.0) r.Message = "Lưu ý: công xôn > 2,0 m – kiểm tra kỹ võng và rung. ";
        r.Message += $"Đã thêm ban công vươn {depth:0.00} m dài {hi - lo:0.00} m: {stations.Count} dầm công xôn {secC}, dầm biên {secE}, {r.NewSlabs.Count} ô sàn, lan can {railing:0.##} kN/m.";
        return r;
    }
}
