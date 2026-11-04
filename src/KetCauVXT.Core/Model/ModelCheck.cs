using KetCauVXT.Analysis;
using KetCauVXT.Import;
using KetCauVXT.Loads;

namespace KetCauVXT.Model;

public enum IssueSeverity { Error, Warning, Info }

/// <summary>Một vấn đề tìm thấy khi kiểm tra mô hình (Analyze → Check Model của ETABS).</summary>
public class ModelIssue
{
    /// <summary>Mã loại vấn đề – xem hằng số trong <see cref="ModelCheck"/>.</summary>
    public string Code { get; set; } = "";
    public IssueSeverity Severity { get; set; }
    public string Message { get; set; } = "";
    public string Story { get; set; } = "";
    public List<int> Nodes { get; } = new();
    public List<int> Frames { get; } = new();
    public List<int> Slabs { get; } = new();
    /// <summary>Sửa tự động được bằng <see cref="ModelCheck.Fix"/>.</summary>
    public bool Fixable { get; set; }
    /// <summary>Vị trí để phóng tới (m).</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public string SeverityName => Severity switch { IssueSeverity.Error => "Lỗi", IssueSeverity.Warning => "Cảnh báo", _ => "Thông tin" };
    public override string ToString() => $"[{SeverityName}] {Message}";
}

public class ModelCheckOptions
{
    /// <summary>Dung sai trùng nút (m) – mặc định 5 mm (ETABS: "Joint/Joint tolerance").</summary>
    public double NodeTolerance { get; set; } = 0.005;
    /// <summary>Chiều dài thanh tối thiểu (m).</summary>
    public double MinLength { get; set; } = 0.001;
    /// <summary>Kiểm tra cạnh sàn không có dầm đỡ.</summary>
    public bool CheckSlabEdges { get; set; } = true;
    /// <summary>Kiểm tra trường hợp tải không có tải (sinh tải tự động trên bản sao để đếm).</summary>
    public bool CheckEmptyPatterns { get; set; } = true;
}

public class ModelCheckReport
{
    public List<ModelIssue> Issues { get; } = new();
    public int Errors => Issues.Count(i => i.Severity == IssueSeverity.Error);
    public int Warnings => Issues.Count(i => i.Severity == IssueSeverity.Warning);
    public int Infos => Issues.Count(i => i.Severity == IssueSeverity.Info);
    public int Fixable => Issues.Count(i => i.Fixable);
    public bool Has(string code) => Issues.Any(i => i.Code == code);
    public int Count(string code) => Issues.Count(i => i.Code == code);
    public string Summary => Issues.Count == 0 ? "Mô hình không có lỗi." : $"{Errors} lỗi, {Warnings} cảnh báo, {Infos} thông tin ({Fixable} mục sửa tự động được).";
}

/// <summary>Kết quả sửa tự động.</summary>
public class ModelFixResult
{
    public int MergedNodes { get; set; }
    public int DeletedFrames { get; set; }
    public int SplitFrames { get; set; }
    public int AddedNodes { get; set; }
    public int DeletedNodes { get; set; }
    public int RemovedItems { get; set; }
    public List<string> Messages { get; } = new();
    public int Total => MergedNodes + DeletedFrames + SplitFrames + AddedNodes + DeletedNodes + RemovedItems;
    public override string ToString() => Messages.Count == 0 ? "Không có gì cần sửa." : string.Join("; ", Messages);
}

/// <summary>Kiểm tra mô hình trước khi phân tích (như Analyze → Check Model của ETABS): nút trùng, thanh trùng / chồng,
/// thanh dài 0, nút tự do, nhóm thanh không nối gối (biến hình), cột không có gối/móng ở chân, dầm cắt nhau không có nút,
/// nút nằm giữa thanh chưa chia, cạnh sàn không có dầm đỡ, tiết diện thiếu / không dùng, trường hợp tải không có tải,
/// tổ hợp tham chiếu thiếu, tải gán cho đối tượng / trường hợp không tồn tại. <see cref="Fix"/> sửa tự động các mục Fixable.</summary>
public static class ModelCheck
{
    public const string DuplicateNodes = "NUT_TRUNG";
    public const string ZeroLength = "THANH_DAI_0";
    public const string DuplicateFrames = "THANH_TRUNG";
    public const string OverlapFrames = "THANH_CHONG";
    public const string FreeNodes = "NUT_TU_DO";
    public const string Unsupported = "KHONG_GOI";
    public const string ColumnNoSupport = "COT_KHONG_GOI";
    public const string ColumnNoFooting = "COT_CHUA_MONG";
    public const string CrossNoNode = "DAM_CAT_KHONG_NUT";
    public const string NodeOnFrame = "NUT_GIUA_THANH";
    public const string SlabEdgeNoBeam = "SAN_CANH_KHONG_DAM";
    public const string MissingSection = "THIEU_TIET_DIEN";
    public const string UnusedSection = "TIET_DIEN_KHONG_DUNG";
    public const string EmptyPattern = "TH_TAI_RONG";
    public const string ComboMissingRef = "TO_HOP_THIEU";
    public const string ComboEmpty = "TO_HOP_RONG";
    public const string OrphanLoads = "TAI_MO_COI";
    public const string BadNodeRef = "THAM_CHIEU_NUT";

    /// <summary>Các mã sửa được, theo thứ tự áp dụng khi "Sửa tự động tất cả".</summary>
    public static readonly string[] FixOrder = { BadNodeRef, ZeroLength, DuplicateNodes, DuplicateFrames, FreeNodes, CrossNoNode, NodeOnFrame, OrphanLoads, ComboMissingRef };

    public static ModelCheckReport Run(Project p, ModelCheckOptions o = null)
    {
        o ??= new ModelCheckOptions();
        p.InvalidateIndex();
        var rep = new ModelCheckReport();
        var nodeById = new Dictionary<int, Node>();
        foreach (var n in p.Nodes) nodeById[n.Id] = n;

        // ---- tham chiếu nút không tồn tại ----
        foreach (var f in p.Frames.Where(f => !nodeById.ContainsKey(f.NodeI) || !nodeById.ContainsKey(f.NodeJ)))
        {
            var i = Issue(rep, BadNodeRef, IssueSeverity.Error, $"Thanh {Name(f)} tham chiếu nút không tồn tại – xoá thanh.", true);
            i.Frames.Add(f.Id); i.Story = f.Story;
        }
        foreach (var s in p.Slabs.Where(s => s.Nodes.Any(id => !nodeById.ContainsKey(id))))
        {
            var i = Issue(rep, BadNodeRef, IssueSeverity.Error, $"Ô sàn {s.Label} tham chiếu nút không tồn tại – xoá ô sàn.", true);
            i.Slabs.Add(s.Id); i.Story = s.Story;
        }
        var frames = p.Frames.Where(f => nodeById.ContainsKey(f.NodeI) && nodeById.ContainsKey(f.NodeJ)).ToList();

        // ---- nút trùng ----
        foreach (var grp in DuplicateGroups(p, o.NodeTolerance))
        {
            var n0 = nodeById[grp[0]];
            var i = Issue(rep, DuplicateNodes, IssueSeverity.Warning,
                $"{grp.Count} nút trùng nhau (cách < {o.NodeTolerance * 1000:0.#} mm) tại ({n0.X:0.###}; {n0.Y:0.###}; {n0.Z:0.###}): {string.Join(", ", grp)} – gộp nút.", true);
            i.Nodes.AddRange(grp); At(i, n0);
        }

        // ---- thanh dài 0 ----
        foreach (var f in frames)
        {
            double L = (nodeById[f.NodeJ].P - nodeById[f.NodeI].P).Length;
            if (L >= o.MinLength && f.NodeI != f.NodeJ) continue;
            var i = Issue(rep, ZeroLength, IssueSeverity.Error, $"Thanh {Name(f)} có chiều dài {L * 1000:0.##} mm – xoá thanh.", true);
            i.Frames.Add(f.Id); i.Story = f.Story; At(i, nodeById[f.NodeI]);
        }

        // ---- thanh trùng / chồng lên nhau ----
        var seenPair = new Dictionary<(int, int), FrameElement>();
        foreach (var f in frames)
        {
            var key = (Math.Min(f.NodeI, f.NodeJ), Math.Max(f.NodeI, f.NodeJ));
            if (key.Item1 == key.Item2) continue;
            if (seenPair.TryGetValue(key, out var f0))
            {
                if (f0.IsCurved != f.IsCurved || Math.Abs(f0.CurveMidOffset - f.CurveMidOffset) > 1e-3) continue;
                var i = Issue(rep, DuplicateFrames, IssueSeverity.Error, $"Thanh {Name(f)} trùng thanh {Name(f0)} (cùng hai nút {key.Item1}–{key.Item2}) – xoá thanh trùng, chuyển tải sang thanh giữ lại.", true);
                i.Frames.Add(f0.Id); i.Frames.Add(f.Id); i.Story = f.Story; At(i, nodeById[f.NodeI]);
            }
            else seenPair[key] = f;
        }
        var straight = frames.Where(f => !f.IsCurved && f.NodeI != f.NodeJ).ToList();
        for (int a = 0; a < straight.Count; a++)
            for (int b = a + 1; b < straight.Count; b++)
            {
                var fa = straight[a]; var fb = straight[b];
                if (SameNodes(fa, fb)) continue;
                double ov = Overlap(nodeById[fa.NodeI].P, nodeById[fa.NodeJ].P, nodeById[fb.NodeI].P, nodeById[fb.NodeJ].P, o.NodeTolerance);
                if (ov <= 0.01) continue;
                var i = Issue(rep, OverlapFrames, IssueSeverity.Error, $"Thanh {Name(fa)} và {Name(fb)} chồng lên nhau trên đoạn dài {ov:0.###} m – xoá hoặc chia lại một thanh.", false);
                i.Frames.Add(fa.Id); i.Frames.Add(fb.Id); i.Story = fa.Story; At(i, nodeById[fb.NodeI]);
            }

        // ---- nút nằm giữa thanh chưa chia (nút trên thanh) ----
        foreach (var f in straight)
        {
            var A = nodeById[f.NodeI].P; var B = nodeById[f.NodeJ].P;
            var u = B - A; double L = u.Length;
            if (L < o.MinLength) continue;
            u = u.Normalize();
            foreach (var n in p.Nodes)
            {
                if (n.Id == f.NodeI || n.Id == f.NodeJ) continue;
                double t = (n.P - A).Dot(u);
                if (t <= 0.01 || t >= L - 0.01) continue;
                if (((n.P - A) - u * t).Length >= 0.005) continue;
                if (!IsUsedNode(p, n.Id)) continue;
                var i = Issue(rep, NodeOnFrame, IssueSeverity.Warning, $"Nút {n.Id} nằm giữa thanh {Name(f)} nhưng thanh chưa chia tại nút – chia thanh.", true);
                i.Frames.Add(f.Id); i.Nodes.Add(n.Id); i.Story = f.Story; At(i, n);
            }
        }

        // ---- dầm cắt nhau không có nút giao ----
        var beams = straight.Where(f => !f.IsVerticalKind && Math.Abs(nodeById[f.NodeI].Z - nodeById[f.NodeJ].Z) < 1e-3).ToList();
        foreach (var g in beams.GroupBy(f => Math.Round(nodeById[f.NodeI].Z, 3)))
        {
            var list = g.ToList();
            for (int a = 0; a < list.Count; a++)
                for (int b = a + 1; b < list.Count; b++)
                {
                    var fa = list[a]; var fb = list[b];
                    if (fa.NodeI == fb.NodeI || fa.NodeI == fb.NodeJ || fa.NodeJ == fb.NodeI || fa.NodeJ == fb.NodeJ) continue;
                    var sa = new Seg2(nodeById[fa.NodeI].X, nodeById[fa.NodeI].Y, nodeById[fa.NodeJ].X, nodeById[fa.NodeJ].Y);
                    var sb = new Seg2(nodeById[fb.NodeI].X, nodeById[fb.NodeI].Y, nodeById[fb.NodeJ].X, nodeById[fb.NodeJ].Y);
                    if (!ImportGeometry.Intersect(sa, sb, out double x, out double y, 0.01)) continue;
                    var i = Issue(rep, CrossNoNode, IssueSeverity.Warning, $"Dầm {Name(fa)} cắt dầm {Name(fb)} tại ({x:0.###}; {y:0.###}) nhưng không có nút giao – thêm nút và chia hai dầm.", true);
                    i.Frames.Add(fa.Id); i.Frames.Add(fb.Id); i.Story = fa.Story; i.X = x; i.Y = y; i.Z = g.Key;
                }
        }

        // ---- nút tự do ----
        var free = p.Nodes.Where(n => !IsUsedNode(p, n.Id)).ToList();
        if (free.Count > 0)
        {
            var i = Issue(rep, FreeNodes, IssueSeverity.Info, $"{free.Count} nút không nối với thanh / sàn / móng / tải nào – xoá nút thừa.", true);
            i.Nodes.AddRange(free.Select(n => n.Id)); At(i, free[0]);
        }

        // ---- nhóm thanh không nối gối (biến hình) ----
        var comps = Components(frames);
        foreach (var c in comps)
        {
            if (c.nodes.Any(id => nodeById[id].HasRestraint)) continue;
            var i = Issue(rep, Unsupported, IssueSeverity.Error,
                $"Nhóm {c.frames.Count} thanh ({string.Join(", ", c.frames.Take(6).Select(id => Name(p.Frame(id))))}{(c.frames.Count > 6 ? "…" : "")}) không nối tới gối nào – mô hình biến hình.", false);
            i.Frames.AddRange(c.frames); i.Nodes.AddRange(c.nodes); At(i, nodeById[c.nodes[0]]);
        }

        // ---- cột không có gối / móng ở chân ----
        var attached = new Dictionary<int, int>();
        foreach (var f in frames) { attached[f.NodeI] = attached.GetValueOrDefault(f.NodeI) + 1; attached[f.NodeJ] = attached.GetValueOrDefault(f.NodeJ) + 1; }
        double zMin = p.Nodes.Count > 0 ? p.Nodes.Min(n => n.Z) : 0;
        var footingNodes = new HashSet<int>(p.Footings.Select(x => x.NodeId));
        foreach (var f in frames.Where(f => f.IsVerticalKind))
        {
            var a = nodeById[f.NodeI]; var b = nodeById[f.NodeJ];
            var bot = a.Z <= b.Z ? a : b;
            if (!bot.IsSupport && attached.GetValueOrDefault(bot.Id) <= 1)
            {
                var i = Issue(rep, ColumnNoSupport, IssueSeverity.Error, $"Chân cột {Name(f)} (nút {bot.Id}, z = {bot.Z:0.###}) không có gối và không nối thanh nào – gán liên kết ngàm hoặc nối dầm đỡ.", false);
                i.Frames.Add(f.Id); i.Nodes.Add(bot.Id); i.Story = f.Story; At(i, bot);
            }
            else if (bot.IsSupport && p.Footings.Count > 0 && !footingNodes.Contains(bot.Id) && Math.Abs(bot.Z - zMin) < 1e-3)
            {
                var i = Issue(rep, ColumnNoFooting, IssueSeverity.Info, $"Chân cột {Name(f)} (nút {bot.Id}) có gối nhưng chưa gán móng.", false);
                i.Frames.Add(f.Id); i.Nodes.Add(bot.Id); i.Story = f.Story; At(i, bot);
            }
        }

        // ---- cạnh sàn không có dầm đỡ ----
        if (o.CheckSlabEdges)
            foreach (var s in p.Slabs.Where(s => s.Nodes.All(nodeById.ContainsKey) && s.Nodes.Count >= 3))
            {
                var bad = new List<int>();
                for (int k = 0; k < s.Nodes.Count; k++)
                {
                    var a = nodeById[s.Nodes[k]]; var b = nodeById[s.Nodes[(k + 1) % s.Nodes.Count]];
                    if (!EdgeSupported(p, nodeById, beams, a, b)) bad.Add(k + 1);
                }
                if (bad.Count == 0) continue;
                var i = Issue(rep, SlabEdgeNoBeam, IssueSeverity.Warning,
                    $"Ô sàn {s.Label} ({s.Story}): cạnh {string.Join(", ", bad)} không có dầm / tường đỡ – kiểm tra (cạnh tự do công xôn thì bỏ qua).", false);
                i.Slabs.Add(s.Id); i.Story = s.Story;
                i.X = s.Nodes.Average(id => nodeById[id].X); i.Y = s.Nodes.Average(id => nodeById[id].Y); i.Z = nodeById[s.Nodes[0]].Z;
            }

        // ---- tiết diện ----
        var secNames = new HashSet<string>(p.FrameSections.Select(x => x.Name));
        foreach (var g in p.Frames.Where(f => !secNames.Contains(f.Section)).GroupBy(f => f.Section))
        {
            var i = Issue(rep, MissingSection, IssueSeverity.Error, $"{g.Count()} thanh dùng tiết diện chưa khai báo '{g.Key}'.", false);
            i.Frames.AddRange(g.Select(f => f.Id));
        }
        var slabSecs = new HashSet<string>(p.SlabSections.Select(x => x.Name));
        foreach (var g in p.Slabs.Where(s => !slabSecs.Contains(s.Section)).GroupBy(s => s.Section))
        {
            var i = Issue(rep, MissingSection, IssueSeverity.Error, $"{g.Count()} ô sàn dùng tiết diện sàn chưa khai báo '{g.Key}'.", false);
            i.Slabs.AddRange(g.Select(s => s.Id));
        }
        var unused = p.FrameSections.Where(sec => p.Frames.All(f => f.Section != sec.Name)).Select(sec => sec.Name).ToList();
        if (unused.Count > 0)
            Issue(rep, UnusedSection, IssueSeverity.Info, $"{unused.Count} tiết diện thanh chưa dùng: {string.Join(", ", unused.Take(12))}{(unused.Count > 12 ? "…" : "")}.", false);

        // ---- tải gán cho đối tượng / trường hợp không tồn tại ----
        var fids = new HashSet<int>(p.Frames.Select(f => f.Id));
        var sids = new HashSet<int>(p.Slabs.Select(s => s.Id));
        var pats = new HashSet<string>(p.Patterns.Select(x => x.Name));
        int orphan = p.FrameLoads.Count(l => !fids.Contains(l.Element) || !pats.Contains(l.Pattern))
                   + p.AreaLoads.Count(l => !sids.Contains(l.Slab) || !pats.Contains(l.Pattern))
                   + p.NodeLoads.Count(l => !nodeById.ContainsKey(l.Node) || !pats.Contains(l.Pattern));
        if (orphan > 0)
            Issue(rep, OrphanLoads, IssueSeverity.Warning, $"{orphan} tải gán cho thanh / ô sàn / nút / trường hợp tải không tồn tại – xoá.", true);

        // ---- trường hợp tải không có tải ----
        if (o.CheckEmptyPatterns && p.Patterns.Count > 0)
        {
            var counted = CountLoadsWithGenerated(p);
            foreach (var pat in p.Patterns)
            {
                if (pat.SelfWeight != 0) continue;
                if (counted.GetValueOrDefault(pat.Name) > 0) continue;
                Issue(rep, EmptyPattern, IssueSeverity.Info, $"Trường hợp tải {pat.Name} ({pat.Type}) không có tải nào (kể cả tải tự sinh) – kết quả bằng 0.", false);
            }
        }

        // ---- tổ hợp ----
        var comboNames = new HashSet<string>(p.Combinations.Select(c => c.Name));
        foreach (var c in p.Combinations)
        {
            if (c.Items.Count == 0) { Issue(rep, ComboEmpty, IssueSeverity.Warning, $"Tổ hợp {c.Name} không có thành phần.", false); continue; }
            var miss = c.Items.Where(it => !pats.Contains(it.Case) && !comboNames.Contains(it.Case)).Select(it => it.Case).Distinct().ToList();
            if (miss.Count > 0)
                Issue(rep, ComboMissingRef, IssueSeverity.Error, $"Tổ hợp {c.Name} tham chiếu {string.Join(", ", miss)} chưa khai báo – bỏ thành phần thiếu.", true);
        }
        return rep;
    }

    // ------------------------------------------------------------------
    // Sửa tự động
    // ------------------------------------------------------------------
    /// <summary>Sửa tự động các mã cho trước (null = mọi mã sửa được, theo <see cref="FixOrder"/>).</summary>
    public static ModelFixResult Fix(Project p, IEnumerable<string> codes = null, ModelCheckOptions o = null)
    {
        o ??= new ModelCheckOptions();
        var want = codes == null ? new HashSet<string>(FixOrder) : new HashSet<string>(codes);
        var r = new ModelFixResult();
        foreach (var code in FixOrder.Where(want.Contains))
        {
            switch (code)
            {
                case BadNodeRef:
                {
                    var ids = new HashSet<int>(p.Nodes.Select(n => n.Id));
                    var bad = p.Frames.Where(f => !ids.Contains(f.NodeI) || !ids.Contains(f.NodeJ)).Select(f => f.Id).ToList();
                    int ns = p.Slabs.RemoveAll(s => s.Nodes.Any(id => !ids.Contains(id)));
                    DeleteFrames(p, bad);
                    if (bad.Count + ns > 0) { r.DeletedFrames += bad.Count; r.RemovedItems += ns; r.Messages.Add($"xoá {bad.Count} thanh, {ns} ô sàn tham chiếu nút thiếu"); }
                    break;
                }
                case ZeroLength:
                {
                    int n = DeleteZeroLength(p, o.MinLength);
                    if (n > 0) { r.DeletedFrames += n; r.Messages.Add($"xoá {n} thanh dài 0"); }
                    break;
                }
                case DuplicateNodes:
                {
                    int n = MergeDuplicateNodes(p, o.NodeTolerance);
                    if (n > 0)
                    {
                        r.MergedNodes += n; r.Messages.Add($"gộp {n} nút trùng");
                        int z = DeleteZeroLength(p, o.MinLength);
                        if (z > 0) { r.DeletedFrames += z; r.Messages.Add($"xoá {z} thanh dài 0 sau khi gộp"); }
                    }
                    break;
                }
                case DuplicateFrames:
                {
                    int n = DeleteDuplicateFrames(p);
                    if (n > 0) { r.DeletedFrames += n; r.Messages.Add($"xoá {n} thanh trùng"); }
                    break;
                }
                case CrossNoNode:
                {
                    int added = AddCrossingNodes(p);
                    if (added > 0)
                    {
                        int s = ModelBuilder.SplitFramesAtNodes(p);
                        r.AddedNodes += added; r.SplitFrames += s;
                        r.Messages.Add($"thêm {added} nút giao, chia {s} thanh");
                    }
                    break;
                }
                case NodeOnFrame:
                {
                    int s = ModelBuilder.SplitFramesAtNodes(p);
                    if (s > 0) { r.SplitFrames += s; r.Messages.Add($"chia {s} thanh tại nút trung gian"); }
                    break;
                }
                case OrphanLoads:
                {
                    var fids = new HashSet<int>(p.Frames.Select(f => f.Id));
                    var sids = new HashSet<int>(p.Slabs.Select(s => s.Id));
                    var nids = new HashSet<int>(p.Nodes.Select(n => n.Id));
                    var pats = new HashSet<string>(p.Patterns.Select(x => x.Name));
                    int n = p.FrameLoads.RemoveAll(l => !fids.Contains(l.Element) || !pats.Contains(l.Pattern))
                          + p.AreaLoads.RemoveAll(l => !sids.Contains(l.Slab) || !pats.Contains(l.Pattern))
                          + p.NodeLoads.RemoveAll(l => !nids.Contains(l.Node) || !pats.Contains(l.Pattern));
                    if (n > 0) { r.RemovedItems += n; r.Messages.Add($"xoá {n} tải mồ côi"); }
                    break;
                }
                case ComboMissingRef:
                {
                    int n = ComboEditor.RemoveMissingRefs(p);
                    if (n > 0) { r.RemovedItems += n; r.Messages.Add($"bỏ {n} thành phần tổ hợp thiếu"); }
                    break;
                }
                case FreeNodes:
                {
                    int n = p.Nodes.RemoveAll(nd => !IsUsedNode(p, nd.Id));
                    if (n > 0) { r.DeletedNodes += n; r.Messages.Add($"xoá {n} nút tự do"); }
                    break;
                }
            }
            p.InvalidateIndex();
        }
        return r;
    }

    /// <summary>Gộp các nút trùng (cách nhau &lt; tol) vào nút có Id nhỏ nhất: cập nhật thanh, sàn, móng, tải nút, thiết lập móng;
    /// liên kết gộp theo phép HOẶC. Trả về số nút đã bỏ.</summary>
    public static int MergeDuplicateNodes(Project p, double tol = 0.005)
    {
        var groups = DuplicateGroups(p, tol);
        if (groups.Count == 0) return 0;
        var map = new Dictionary<int, int>();
        var byId = p.Nodes.ToDictionary(n => n.Id);
        foreach (var g in groups)
        {
            var keep = byId[g[0]];
            foreach (var id in g.Skip(1))
            {
                map[id] = keep.Id;
                var o = byId[id];
                for (int d = 0; d < 6; d++) keep.Restraint[d] |= o.Restraint[d];
                if (string.IsNullOrEmpty(keep.Story)) keep.Story = o.Story;
            }
        }
        int M(int id) => map.TryGetValue(id, out var k) ? k : id;
        foreach (var f in p.Frames) { f.NodeI = M(f.NodeI); f.NodeJ = M(f.NodeJ); }
        foreach (var s in p.Slabs)
        {
            var nn = new List<int>();
            foreach (var id in s.Nodes.Select(M)) if (nn.Count == 0 || nn[^1] != id) nn.Add(id);
            if (nn.Count > 1 && nn[0] == nn[^1]) nn.RemoveAt(nn.Count - 1);
            if (nn.Count != s.Nodes.Count) s.EdgeFixed = new List<bool>();
            s.Nodes = nn;
        }
        foreach (var x in p.Footings) x.NodeId = M(x.NodeId);
        // móng trùng nút sau khi gộp: giữ bản đầu
        p.Footings = p.Footings.GroupBy(x => x.NodeId).Select(g => g.First()).ToList();
        foreach (var l in p.NodeLoads) l.Node = M(l.Node);
        var fs = p.Foundation;
        if (fs != null)
        {
            foreach (var x in fs.CapOverrides ?? new List<PileCapOverride>()) x.NodeId = M(x.NodeId);
            foreach (var x in fs.Eccentric ?? new List<EccentricOverride>()) x.NodeId = M(x.NodeId);
            foreach (var x in fs.CuTram?.Overrides ?? new List<CuTramOverride>()) x.NodeId = M(x.NodeId);
        }
        p.Slabs.RemoveAll(s => s.Nodes.Count < 3);
        int removed = p.Nodes.RemoveAll(n => map.ContainsKey(n.Id));
        p.InvalidateIndex();
        return removed;
    }

    /// <summary>Xoá thanh dài &lt; minLength (hoặc hai đầu cùng một nút) cùng tải trên thanh.</summary>
    public static int DeleteZeroLength(Project p, double minLength = 0.001)
    {
        var byId = p.Nodes.ToDictionary(n => n.Id);
        var ids = p.Frames.Where(f => f.NodeI == f.NodeJ || (byId.TryGetValue(f.NodeI, out var a) && byId.TryGetValue(f.NodeJ, out var b) && (b.P - a.P).Length < minLength))
                    .Select(f => f.Id).ToList();
        DeleteFrames(p, ids);
        return ids.Count;
    }

    /// <summary>Xoá thanh trùng (cùng hai nút, cùng độ cong); tải trên thanh bị xoá chuyển sang thanh giữ lại (đổi chiều nếu ngược hướng).</summary>
    public static int DeleteDuplicateFrames(Project p)
    {
        var seen = new Dictionary<(int, int), FrameElement>();
        var del = new List<int>();
        foreach (var f in p.Frames)
        {
            var key = (Math.Min(f.NodeI, f.NodeJ), Math.Max(f.NodeI, f.NodeJ));
            if (key.Item1 == key.Item2) continue;
            if (!seen.TryGetValue(key, out var k)) { seen[key] = f; continue; }
            if (k.IsCurved != f.IsCurved || Math.Abs(k.CurveMidOffset - f.CurveMidOffset) > 1e-3) continue;
            bool reversed = k.NodeI != f.NodeI;
            foreach (var l in p.FrameLoads.Where(x => x.Element == f.Id))
            {
                l.Element = k.Id;
                if (reversed)
                {
                    l.Points = l.Points.Select(q => new[] { 1 - q[0], q[1] }).OrderBy(q => q[0]).ToList();
                    l.PointLoads = l.PointLoads.Select(q => new[] { 1 - q[0], q[1] }).ToList();
                }
            }
            del.Add(f.Id);
        }
        DeleteFrames(p, del);
        return del.Count;
    }

    /// <summary>Thêm nút tại giao điểm của các dầm thẳng nằm ngang cùng cao độ cắt nhau mà chưa có nút. Trả về số nút thêm.</summary>
    public static int AddCrossingNodes(Project p)
    {
        var byId = p.Nodes.ToDictionary(n => n.Id);
        var beams = p.Frames.Where(f => !f.IsVerticalKind && !f.IsCurved && byId.ContainsKey(f.NodeI) && byId.ContainsKey(f.NodeJ)
                                        && Math.Abs(byId[f.NodeI].Z - byId[f.NodeJ].Z) < 1e-3).ToList();
        int added = 0;
        foreach (var g in beams.GroupBy(f => Math.Round(byId[f.NodeI].Z, 3)))
        {
            var list = g.ToList();
            for (int a = 0; a < list.Count; a++)
                for (int b = a + 1; b < list.Count; b++)
                {
                    var fa = list[a]; var fb = list[b];
                    if (fa.NodeI == fb.NodeI || fa.NodeI == fb.NodeJ || fa.NodeJ == fb.NodeI || fa.NodeJ == fb.NodeJ) continue;
                    var sa = new Seg2(byId[fa.NodeI].X, byId[fa.NodeI].Y, byId[fa.NodeJ].X, byId[fa.NodeJ].Y);
                    var sb = new Seg2(byId[fb.NodeI].X, byId[fb.NodeI].Y, byId[fb.NodeJ].X, byId[fb.NodeJ].Y);
                    if (!ImportGeometry.Intersect(sa, sb, out double x, out double y, 0.01)) continue;
                    int before = p.Nodes.Count;
                    p.GetOrAddNode(Math.Round(x, 4), Math.Round(y, 4), g.Key, fa.Story);
                    if (p.Nodes.Count > before) added++;
                }
        }
        p.InvalidateIndex();
        return added;
    }

    private static void DeleteFrames(Project p, ICollection<int> ids)
    {
        if (ids.Count == 0) return;
        var set = new HashSet<int>(ids);
        p.Frames.RemoveAll(f => set.Contains(f.Id));
        p.FrameLoads.RemoveAll(l => set.Contains(l.Element));
        p.InvalidateIndex();
    }

    // ------------------------------------------------------------------
    // tiện ích
    // ------------------------------------------------------------------
    /// <summary>Nhóm các nút trùng (Id tăng dần, phần tử đầu là nút giữ lại).</summary>
    public static List<List<int>> DuplicateGroups(Project p, double tol)
    {
        var cell = Math.Max(tol, 1e-6) * 2;
        var bucket = new Dictionary<(long, long, long), List<Node>>();
        (long, long, long) Key(double x, double y, double z) => ((long)Math.Floor(x / cell), (long)Math.Floor(y / cell), (long)Math.Floor(z / cell));
        foreach (var n in p.Nodes)
        {
            var k = Key(n.X, n.Y, n.Z);
            if (!bucket.TryGetValue(k, out var l)) bucket[k] = l = new List<Node>();
            l.Add(n);
        }
        var parent = new Dictionary<int, int>();
        int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        foreach (var n in p.Nodes) parent[n.Id] = n.Id;
        foreach (var n in p.Nodes)
        {
            var k = Key(n.X, n.Y, n.Z);
            for (long dx = -1; dx <= 1; dx++) for (long dy = -1; dy <= 1; dy++) for (long dz = -1; dz <= 1; dz++)
                    {
                        if (!bucket.TryGetValue((k.Item1 + dx, k.Item2 + dy, k.Item3 + dz), out var l)) continue;
                        foreach (var m in l)
                        {
                            if (m.Id <= n.Id) continue;
                            if ((m.P - n.P).Length >= tol) continue;
                            int ra = Find(n.Id), rb = Find(m.Id);
                            if (ra != rb) parent[Math.Max(ra, rb)] = Math.Min(ra, rb);
                        }
                    }
        }
        return p.Nodes.GroupBy(n => Find(n.Id)).Where(g => g.Count() > 1).Select(g => g.Select(n => n.Id).OrderBy(x => x).ToList()).ToList();
    }

    private static bool IsUsedNode(Project p, int id) =>
        p.Frames.Any(f => f.NodeI == id || f.NodeJ == id) || p.Slabs.Any(s => s.Nodes.Contains(id))
        || p.Footings.Any(x => x.NodeId == id) || p.NodeLoads.Any(l => l.Node == id);

    private static bool SameNodes(FrameElement a, FrameElement b) =>
        (a.NodeI == b.NodeI && a.NodeJ == b.NodeJ) || (a.NodeI == b.NodeJ && a.NodeJ == b.NodeI);

    /// <summary>Chiều dài đoạn chồng nhau của hai đoạn thẳng cùng phương (0 nếu không cùng đường thẳng).</summary>
    private static double Overlap(Vec3 a1, Vec3 a2, Vec3 b1, Vec3 b2, double tol)
    {
        var u = a2 - a1; double L = u.Length;
        if (L < 1e-9) return 0;
        u = u.Normalize();
        double Dist(Vec3 q) { double t = (q - a1).Dot(u); return ((q - a1) - u * t).Length; }
        if (Dist(b1) > tol || Dist(b2) > tol) return 0;
        double t1 = (b1 - a1).Dot(u), t2 = (b2 - a1).Dot(u);
        double lo = Math.Max(0, Math.Min(t1, t2)), hi = Math.Min(L, Math.Max(t1, t2));
        return hi - lo;
    }

    private static List<(List<int> frames, List<int> nodes)> Components(List<FrameElement> frames)
    {
        var parent = new Dictionary<int, int>();
        int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        void Union(int a, int b) { int ra = Find(a), rb = Find(b); if (ra != rb) parent[ra] = rb; }
        foreach (var f in frames) { parent.TryAdd(f.NodeI, f.NodeI); parent.TryAdd(f.NodeJ, f.NodeJ); }
        foreach (var f in frames) Union(f.NodeI, f.NodeJ);
        return frames.GroupBy(f => Find(f.NodeI))
            .Select(g => (g.Select(f => f.Id).ToList(), g.SelectMany(f => new[] { f.NodeI, f.NodeJ }).Distinct().ToList())).ToList();
    }

    /// <summary>Cạnh sàn a–b nằm trên (hợp các) dầm cùng cao độ, hoặc trên tường khai báo.</summary>
    private static bool EdgeSupported(Project p, Dictionary<int, Node> byId, List<FrameElement> beams, Node a, Node b)
    {
        var u = b.P - a.P; double L = u.Length;
        if (L < 1e-6) return true;
        u = u.Normalize();
        var cover = new List<(double, double)>();
        foreach (var f in beams)
        {
            var p1 = byId[f.NodeI].P; var p2 = byId[f.NodeJ].P;
            if (Math.Abs(p1.Z - a.Z) > 0.01) continue;
            double d1 = ((p1 - a.P) - u * (p1 - a.P).Dot(u)).Length, d2 = ((p2 - a.P) - u * (p2 - a.P).Dot(u)).Length;
            if (d1 > 0.02 || d2 > 0.02) continue;
            double t1 = (p1 - a.P).Dot(u), t2 = (p2 - a.P).Dot(u);
            cover.Add((Math.Min(t1, t2), Math.Max(t1, t2)));
        }
        // dầm cong: chấp nhận cạnh có hai đầu là hai nút của dầm cong
        if (p.Frames.Any(f => f.IsCurved && SameNodes(f, new FrameElement { NodeI = a.Id, NodeJ = b.Id }))) return true;
        // tường khai báo dọc cạnh
        foreach (var w in p.Walls ?? new List<WallLine>())
        {
            var w1 = new Vec3(w.X1, w.Y1, a.Z); var w2 = new Vec3(w.X2, w.Y2, a.Z);
            double d1 = ((w1 - a.P) - u * (w1 - a.P).Dot(u)).Length, d2 = ((w2 - a.P) - u * (w2 - a.P).Dot(u)).Length;
            if (d1 > 0.05 || d2 > 0.05) continue;
            var st = p.Stories.FirstOrDefault(s => s.Name == w.Story);
            if (st != null && Math.Abs(st.Elevation - a.Z) > 0.01) continue;
            double t1 = (w1 - a.P).Dot(u), t2 = (w2 - a.P).Dot(u);
            cover.Add((Math.Min(t1, t2), Math.Max(t1, t2)));
        }
        double reach = 0.01;
        foreach (var (lo, hi) in cover.OrderBy(c => c.Item1))
        {
            if (lo > reach + 0.02) break;
            reach = Math.Max(reach, hi);
        }
        return reach >= L - 0.02;
    }

    /// <summary>Đếm tải theo trường hợp tải sau khi sinh tải tự động (tường, cầu thang, sàn, gió, động đất) trên BẢN SAO dự án.</summary>
    private static Dictionary<string, int> CountLoadsWithGenerated(Project p)
    {
        Project c = p;
        try
        {
            c = p.Clone();
            c.InvalidateIndex();
            WallLoadGenerator.Apply(c);
            StairLoadGenerator.Apply(c);
            SlabLoadDistributor.Apply(c);
            LateralLoadGenerator.Apply(c, new List<StoryLateralForce>());
        }
        catch { c = p; }
        var d = new Dictionary<string, int>();
        foreach (var l in c.FrameLoads) d[l.Pattern] = d.GetValueOrDefault(l.Pattern) + 1;
        foreach (var l in c.AreaLoads) d[l.Pattern] = d.GetValueOrDefault(l.Pattern) + 1;
        foreach (var l in c.NodeLoads) d[l.Pattern] = d.GetValueOrDefault(l.Pattern) + 1;
        return d;
    }

    private static ModelIssue Issue(ModelCheckReport rep, string code, IssueSeverity sev, string msg, bool fixable)
    {
        var i = new ModelIssue { Code = code, Severity = sev, Message = msg, Fixable = fixable };
        rep.Issues.Add(i);
        return i;
    }

    private static void At(ModelIssue i, Node n) { i.X = n.X; i.Y = n.Y; i.Z = n.Z; if (string.IsNullOrEmpty(i.Story)) i.Story = n.Story; }

    private static string Name(FrameElement f) => string.IsNullOrEmpty(f.Label) ? $"#{f.Id}" : $"{f.Label} (#{f.Id})";
}
