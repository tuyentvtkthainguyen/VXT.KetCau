using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Design;

/// <summary>Một nhóm cấu kiện cùng tên (ký hiệu).</summary>
public sealed class MarkGroup
{
    public string Kind { get; set; } = "";          // "Cột" / "Dầm" / "Móng"
    public string Mark { get; set; } = "";
    public string Section { get; set; } = "";       // tiết diện / kích thước móng
    public string Length { get; set; } = "";        // chiều dài (dầm: các nhịp; cột: chiều cao từng tầng)
    public int Count { get; set; }
    /// <summary>Khoá nhận dạng: frame id nhỏ nhất của mỗi cấu kiện (cột: chồng cột, dầm: dải dầm) hoặc nút móng.</summary>
    public List<int> Members { get; } = new();
    public string Where { get; set; } = "";
}

/// <summary>[Tên cấu kiện] Gán một tên (ký hiệu) cho các cột / dầm / móng có tiết diện giống nhau và chiều dài giống nhau.
/// • Cột: đơn vị là CHỒNG CỘT tại một vị trí (chân → đỉnh); giống nhau khi cùng dãy (tầng, tiết diện, chiều cao) từng đoạn.
/// • Dầm: đơn vị là DẢI DẦM liên tục; giống nhau khi cùng tiết diện, cùng số nhịp và chiều dài từng nhịp (sai số 10 mm).
/// • Móng (móng đơn): giống nhau khi cùng L × B × H; gán tên chung → đồng nhất kích thước theo móng lớn nhất nhóm.
/// Thiết kế: các cấu kiện cùng tên thiết kế chung MỘT phương án thép (phương án bất lợi nhất, kiểm tra As yêu cầu của mọi cấu kiện
/// trong nhóm), tên thiết kế = tên đã gán, SL = số cấu kiện.</summary>
public static class MemberMarks
{
    public const double LenTol = 0.01;

    private static string L(double m) => DesignUtil.F(Math.Round(m / LenTol) * LenTol, "0.00");

    // ------------------------------------------------------------------
    // đơn vị cấu kiện
    // ------------------------------------------------------------------
    /// <summary>Các chồng cột (chân → đỉnh) và khoá giống nhau.</summary>
    public static List<(List<FrameElement> Frames, string Key, string Section, string Length, string Where)> ColumnStacks(Project p)
    {
        var res = new List<(List<FrameElement>, string, string, string, string)>();
        var cols = p.Frames.Where(f => f.Kind == FrameKind.Column).ToList();
        foreach (var g in cols.GroupBy(f => { var n = DesignUtil.LowerNode(p, f); return (Math.Round(n.X, 2), Math.Round(n.Y, 2)); })
                     .OrderBy(g => g.Key.Item2).ThenBy(g => g.Key.Item1))
        {
            var st = g.OrderBy(f => DesignUtil.LowerNode(p, f).Z).ToList();
            var parts = st.Select(f => (Sec: f.Section, Len: L(Length(p, f)), f.Story)).ToList();
            string key = string.Join("|", parts.Select(x => x.Story + ":" + x.Sec + ":" + x.Len));
            string secs = string.Join(" / ", parts.Select(x => x.Sec).Distinct());
            string lens = string.Join(" + ", parts.Select(x => x.Len));
            var n0 = DesignUtil.LowerNode(p, st[0]);
            res.Add((st, key, secs, lens, DesignUtil.GridName(p, n0.X, n0.Y)));
        }
        return res;
    }

    /// <summary>Các dải dầm liên tục và khoá giống nhau (tiết diện + chiều dài từng phần tử theo thứ tự).</summary>
    public static List<(List<FrameElement> Frames, string Key, string Section, string Length, string Where)> BeamLines(Project p)
    {
        var res = new List<(List<FrameElement>, string, string, string, string)>();
        var warn = new List<string>();
        var lines = BeamDesigner.BuildLines(p, BeamDesigner.FramesByNode(p), warn);
        foreach (var li in lines)
        {
            var segs = li.Segs.OrderBy(s => s.X0).ToList();
            var lens = segs.Select(s => L(s.Len)).ToList();
            // dầm đối xứng: nhịp đọc xuôi / ngược coi như nhau
            string fw = string.Join("+", lens), bw = string.Join("+", Enumerable.Reverse(lens));
            string key = (li.Sec?.Name ?? segs[0].F.Section) + ":" + (string.CompareOrdinal(fw, bw) <= 0 ? fw : bw) + (li.Arc != null ? ":cong" : "");
            res.Add((segs.Select(s => s.F).ToList(), key, li.Sec?.Name ?? segs[0].F.Section, string.Join(" + ", lens), li.Story));
        }
        return res;
    }

    private static double Length(Project p, FrameElement f)
    {
        var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
        return a == null || b == null ? 0 : Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
    }

    /// <summary>Tên chung của một nhóm phần tử (mọi phần tử cùng tên, khác rỗng) – rỗng nếu không.</summary>
    public static string CommonMark(IEnumerable<FrameElement> fs)
    {
        var m = fs.Select(f => f.Mark ?? "").Distinct().ToList();
        return m.Count == 1 ? m[0] : "";
    }

    // ------------------------------------------------------------------
    // tự đặt tên theo nhóm
    // ------------------------------------------------------------------
    /// <summary>Tự đặt tên: cột C1, C2…; dầm D1, D2…; móng M1, M2… – mỗi nhóm cùng tiết diện + chiều dài một tên.
    /// keepExisting = giữ tên đã gán tay. design = kết quả thiết kế gần nhất (để lấy kích thước móng); null = theo kích thước đã chốt.</summary>
    public static List<MarkGroup> AutoAssign(Project p, DesignSummary design = null, bool columns = true, bool beams = true, bool footings = true, bool keepExisting = false, bool beamsPerStory = false)
    {
        var groups = new List<MarkGroup>();
        if (columns) groups.AddRange(AssignUnits(p, "Cột", "C", ColumnStacks(p), keepExisting));
        if (beams) groups.AddRange(AssignUnits(p, "Dầm", "D", beamsPerStory ? BeamLines(p).Select(u => (u.Frames, u.Where + "#" + u.Key, u.Section, u.Length, u.Where)).ToList() : BeamLines(p), keepExisting));
        if (footings) groups.AddRange(AssignFootings(p, design, keepExisting));
        return groups;
    }

    private static List<MarkGroup> AssignUnits(Project p, string kind, string prefix,
        List<(List<FrameElement> Frames, string Key, string Section, string Length, string Where)> units, bool keep)
    {
        var res = new List<MarkGroup>();
        var used = new HashSet<string>(keep ? units.Select(u => CommonMark(u.Frames)).Where(m => m.Length > 0) : Enumerable.Empty<string>());
        int no = 1;
        string Next() { while (used.Contains(prefix + no)) no++; used.Add(prefix + no); return prefix + no++; }
        foreach (var g in units.Where(u => !keep || CommonMark(u.Frames).Length == 0).GroupBy(u => u.Key))
        {
            string m = Next();
            var mg = new MarkGroup { Kind = kind, Mark = m, Section = g.First().Section, Length = g.First().Length, Count = g.Count() };
            foreach (var u in g)
            {
                foreach (var f in u.Frames) f.Mark = m;
                mg.Members.Add(u.Frames.Min(f => f.Id));
            }
            mg.Where = string.Join(", ", g.Select(u => u.Where).Distinct().Take(12)) + (g.Select(u => u.Where).Distinct().Count() > 12 ? "…" : "");
            res.Add(mg);
        }
        return res;
    }

    /// <summary>Kích thước móng đơn tại từng nút: kích thước đã chốt (Footing.L &gt; 0) hoặc theo thiết kế gần nhất.</summary>
    public static Dictionary<int, (double L, double B, double H)> FootingSizes(Project p, DesignSummary d)
    {
        var res = new Dictionary<int, (double, double, double)>();
        if (d != null)
            foreach (var f in FootingLayout.AllInstances(p, d)) res[f.NodeId] = (f.L, f.B, f.H);
        foreach (var f in p.Footings.Where(f => f.L > 0 && f.B > 0)) res[f.NodeId] = (f.L, f.B, f.Height > 0 ? f.Height : res.TryGetValue(f.NodeId, out var o) ? o.Item3 : 0);
        return res;
    }

    private static List<MarkGroup> AssignFootings(Project p, DesignSummary d, bool keep)
    {
        var res = new List<MarkGroup>();
        var sizes = FootingSizes(p, d);
        if (sizes.Count == 0) return res;
        var used = new HashSet<string>(keep ? p.Footings.Select(f => f.Mark ?? "").Where(m => m.Length > 0) : Enumerable.Empty<string>());
        int no = 1;
        string Next() { while (used.Contains("M" + no)) no++; used.Add("M" + no); return "M" + no++; }
        foreach (var g in sizes.Where(kv => !keep || string.IsNullOrEmpty(p.Footings.FirstOrDefault(f => f.NodeId == kv.Key)?.Mark))
                     .GroupBy(kv => $"{L(kv.Value.L)}x{L(kv.Value.B)}x{L(kv.Value.H)}")
                     .OrderByDescending(g => g.Count()))
        {
            string m = Next();
            var mg = new MarkGroup { Kind = "Móng", Mark = m, Section = g.Key.Replace("x", " × ") + " m", Length = "", Count = g.Count() };
            foreach (var kv in g)
            {
                FootingOf(p, kv.Key).Mark = m;
                mg.Members.Add(kv.Key);
            }
            mg.Where = string.Join(", ", g.Select(kv => { var n = p.Node(kv.Key); return n == null ? "" : DesignUtil.GridName(p, n.X, n.Y); }).Take(12));
            res.Add(mg);
        }
        return res;
    }

    private static Footing FootingOf(Project p, int node)
    {
        var f = p.Footings.FirstOrDefault(x => x.NodeId == node);
        if (f == null) { f = new Footing { NodeId = node, Label = "" }; p.Footings.Add(f); }
        return f;
    }

    // ------------------------------------------------------------------
    // gán tay
    // ------------------------------------------------------------------
    /// <summary>Gán tên cho các cấu kiện chứa các thanh đã chọn (cột → cả chồng cột, dầm → cả dải dầm).
    /// Chỉ gán khi mọi cấu kiện cùng tiết diện + chiều dài; nếu khác → không gán, trả lý do.</summary>
    public static string AssignFrames(Project p, IEnumerable<int> frameIds, string mark, bool force = false)
    {
        mark = (mark ?? "").Trim();
        var ids = new HashSet<int>(frameIds ?? Enumerable.Empty<int>());
        if (ids.Count == 0) return "Chưa chọn cột / dầm nào.";
        var units = ColumnStacks(p).Select(u => (u, Kind: "Cột")).Concat(BeamLines(p).Select(u => (u, Kind: "Dầm")))
                                   .Where(x => x.u.Frames.Any(f => ids.Contains(f.Id))).ToList();
        if (units.Count == 0) return "Các đối tượng đã chọn không phải cột / dầm BTCT.";
        if (units.Select(x => x.Kind).Distinct().Count() > 1) return "Không gán chung một tên cho cả cột và dầm – chọn riêng từng loại.";
        // tên đã dùng cho cấu kiện khác loại / khác kích thước
        var other = (units[0].Kind == "Cột" ? ColumnStacks(p) : BeamLines(p))
                    .Where(u => CommonMark(u.Frames) == mark && mark.Length > 0 && !units.Any(x => ReferenceEquals(x.u.Frames, u.Frames) || x.u.Frames.SequenceEqual(u.Frames)))
                    .ToList();
        var keys = units.Select(x => x.u.Key).Concat(other.Select(u => u.Key)).Distinct().ToList();
        if (mark.Length > 0 && keys.Count > 1 && !force)
            return $"Không gán: {units.Count} {units[0].Kind.ToLowerInvariant()} đã chọn" + (other.Count > 0 ? $" (và {other.Count} cấu kiện đã mang tên {mark})" : "")
                   + " khác tiết diện hoặc chiều dài:\n" + string.Join("\n", units.Select(x => x.u).Concat(other).GroupBy(u => u.Key).Select(g => $"  • {g.First().Section}, L = {g.First().Length} m: {g.Count()} cấu kiện ({string.Join(", ", g.Select(u => u.Where).Distinct().Take(6))})"));
        foreach (var x in units) foreach (var f in x.u.Frames) f.Mark = mark;
        return mark.Length == 0 ? $"Đã xoá tên {units.Count} {units[0].Kind.ToLowerInvariant()}." : $"Đã gán tên {mark} cho {units.Count} {units[0].Kind.ToLowerInvariant()} (tiết diện {units[0].u.Section}, L = {units[0].u.Length} m).";
    }

    /// <summary>Gán tên cho các móng đơn tại các nút; khác kích thước → đồng nhất theo móng lớn nhất (chốt kích thước).</summary>
    public static string AssignFootings(Project p, DesignSummary d, IEnumerable<int> nodes, string mark)
    {
        mark = (mark ?? "").Trim();
        var ns = nodes?.Distinct().ToList() ?? new List<int>();
        if (ns.Count == 0) return "Chưa chọn móng nào.";
        var sizes = FootingSizes(p, d);
        foreach (int n in ns) FootingOf(p, n).Mark = mark;
        if (mark.Length == 0) return $"Đã xoá tên {ns.Count} móng.";
        var all = p.Footings.Where(f => f.Mark == mark).Select(f => f.NodeId).ToList();
        var known = all.Where(sizes.ContainsKey).Select(n => sizes[n]).ToList();
        if (known.Count == 0) return $"Đã gán tên {mark} cho {ns.Count} móng (chưa có kích thước – thiết kế móng để đồng nhất).";
        double Lm = known.Max(x => x.L), Bm = known.Max(x => x.B), Hm = known.Max(x => x.H);
        bool differ = known.Any(x => Math.Abs(x.L - Lm) > 1e-6 || Math.Abs(x.B - Bm) > 1e-6 || Math.Abs(x.H - Hm) > 1e-6) || known.Count < all.Count;
        if (differ) foreach (int n in all) FoundationReset.SetSize(p, n, Lm, Bm, Hm);
        return $"Đã gán tên {mark} cho {ns.Count} móng ({all.Count} móng mang tên này)" + (differ ? $"; đồng nhất kích thước {DesignUtil.F(Lm, "0.##")} × {DesignUtil.F(Bm, "0.##")} × {DesignUtil.F(Hm, "0.##")} m (móng lớn nhất nhóm)." : ".");
    }

    /// <summary>Xoá mọi tên đã gán.</summary>
    public static int ClearAll(Project p, bool columns = true, bool beams = true, bool footings = true)
    {
        int n = 0;
        foreach (var f in p.Frames)
            if ((columns && f.Kind == FrameKind.Column || beams && f.Kind == FrameKind.Beam) && !string.IsNullOrEmpty(f.Mark)) { f.Mark = ""; n++; }
        if (footings) foreach (var f in p.Footings.Where(f => !string.IsNullOrEmpty(f.Mark))) { f.Mark = ""; n++; }
        return n;
    }

    /// <summary>Bảng các nhóm tên hiện có.</summary>
    public static List<MarkGroup> Groups(Project p, DesignSummary d = null)
    {
        var res = new List<MarkGroup>();
        void Units(string kind, List<(List<FrameElement> Frames, string Key, string Section, string Length, string Where)> units)
        {
            foreach (var g in units.Select(u => (u, m: CommonMark(u.Frames))).Where(x => x.m.Length > 0).GroupBy(x => x.m).OrderBy(g => g.Key, NaturalComparer.Instance))
            {
                var mg = new MarkGroup { Kind = kind, Mark = g.Key, Section = string.Join(" | ", g.Select(x => x.u.Section).Distinct()), Length = string.Join(" | ", g.Select(x => x.u.Length).Distinct()), Count = g.Count() };
                foreach (var x in g) mg.Members.Add(x.u.Frames.Min(f => f.Id));
                mg.Where = string.Join(", ", g.Select(x => x.u.Where).Distinct().Take(12));
                res.Add(mg);
            }
        }
        Units("Cột", ColumnStacks(p));
        Units("Dầm", BeamLines(p));
        var sizes = FootingSizes(p, d);
        foreach (var g in p.Footings.Where(f => !string.IsNullOrEmpty(f.Mark)).GroupBy(f => f.Mark).OrderBy(g => g.Key, NaturalComparer.Instance))
        {
            var mg = new MarkGroup
            {
                Kind = "Móng", Mark = g.Key, Count = g.Count(),
                Section = string.Join(" | ", g.Select(f => sizes.TryGetValue(f.NodeId, out var z) ? $"{DesignUtil.F(z.L, "0.##")} × {DesignUtil.F(z.B, "0.##")} × {DesignUtil.F(z.H, "0.##")} m" : "chưa có").Distinct())
            };
            mg.Members.AddRange(g.Select(f => f.NodeId));
            mg.Where = string.Join(", ", g.Select(f => { var n = p.Node(f.NodeId); return n == null ? "" : DesignUtil.GridName(p, n.X, n.Y); }).Take(12));
            res.Add(mg);
        }
        return res;
    }

    /// <summary>Nhóm tên có cấu kiện khác tiết diện / chiều dài (do sửa mô hình sau khi gán) – cảnh báo.</summary>
    public static List<string> Conflicts(Project p) =>
        Groups(p).Where(g => g.Kind != "Móng" && (g.Section.Contains('|') || g.Length.Contains('|')))
                 .Select(g => $"Tên {g.Mark} ({g.Kind.ToLowerInvariant()}): các cấu kiện khác tiết diện / chiều dài ({g.Section}; {g.Length}) – sửa tên hoặc đặt tên lại.").ToList();

    // ------------------------------------------------------------------
    // dùng trong thiết kế
    // ------------------------------------------------------------------
    public static double SteelKg(MemberDesignBase d) => d?.Bars.Sum(b => b.Count * b.WeightPerBar) ?? 0;

    /// <summary>Tên số tự động (prefix + n) không trùng các tên đã gán.</summary>
    public static Func<string> Namer(string prefix, IEnumerable<string> taken)
    {
        var used = new HashSet<string>(taken.Where(x => !string.IsNullOrEmpty(x)));
        int no = 1;
        return () => { while (used.Contains(prefix + no)) no++; used.Add(prefix + no); return prefix + no++; };
    }
}

/// <summary>So sánh tên tự nhiên: C2 &lt; C10.</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();
    public int Compare(string a, string b)
    {
        a ??= ""; b ??= "";
        string pa = new string(a.TakeWhile(c => !char.IsDigit(c)).ToArray()), pb = new string(b.TakeWhile(c => !char.IsDigit(c)).ToArray());
        int c0 = string.CompareOrdinal(pa, pb);
        if (c0 != 0) return c0;
        string da = new string(a.Skip(pa.Length).TakeWhile(char.IsDigit).ToArray()), db = new string(b.Skip(pb.Length).TakeWhile(char.IsDigit).ToArray());
        if (int.TryParse(da, out int ia) && int.TryParse(db, out int ib) && ia != ib) return ia.CompareTo(ib);
        return string.CompareOrdinal(a, b);
    }
}
