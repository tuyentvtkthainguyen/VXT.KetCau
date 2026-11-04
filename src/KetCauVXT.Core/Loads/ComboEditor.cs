using KetCauVXT.Model;

namespace KetCauVXT.Loads;

/// <summary>Khai báo tổ hợp tải trọng bằng tay (Define → Load Combinations của ETABS): thêm, sửa, đổi tên, sao chép, xoá,
/// kiểm tra tham chiếu, tự sinh theo TCVN 2737:2023 (ComboGenerator) có giữ tổ hợp người dùng.
/// Tổ hợp lưu trong Project.Combinations (JSON như cũ).</summary>
public static class ComboEditor
{
    /// <summary>Tên đã dùng bởi trường hợp tải hoặc tổ hợp.</summary>
    public static bool NameUsed(Project p, string name, LoadCombination except = null) =>
        p.Patterns.Any(x => x.Name == name) || p.Combinations.Any(c => c != except && c.Name == name);

    public static string UniqueName(Project p, string baseName)
    {
        baseName = string.IsNullOrWhiteSpace(baseName) ? "TH" : baseName.Trim();
        if (!NameUsed(p, baseName)) return baseName;
        int k = 1;
        while (NameUsed(p, baseName + k)) k++;
        return baseName + k;
    }

    private static List<ComboItem> Clean(IEnumerable<ComboItem> items) =>
        (items ?? Enumerable.Empty<ComboItem>()).Where(i => i != null && !string.IsNullOrWhiteSpace(i.Case))
            .Select(i => new ComboItem(i.Case.Trim(), i.Factor)).ToList();

    /// <summary>Thêm tổ hợp mới.</summary>
    public static EditResult Add(Project p, string name, ComboType type, IEnumerable<ComboItem> items)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return EditResult.Fail("Tên tổ hợp trống.");
        if (NameUsed(p, name)) return EditResult.Fail($"Tên {name} đã dùng cho trường hợp tải / tổ hợp khác.");
        var c = new LoadCombination { Name = name, Type = type, Items = Clean(items) };
        string err = CheckItems(p, c);
        if (err != null) return EditResult.Fail(err);
        p.Combinations.Add(c);
        return new EditResult { Ok = true, Message = $"Đã thêm tổ hợp {name}: {ComboGenerator.Describe(c)}" };
    }

    /// <summary>Sửa tổ hợp: đổi tên (cập nhật tham chiếu trong tổ hợp bao), loại, thành phần.</summary>
    public static EditResult Update(Project p, string oldName, string newName, ComboType type, IEnumerable<ComboItem> items)
    {
        var c = p.Combinations.FirstOrDefault(x => x.Name == oldName);
        if (c == null) return EditResult.Fail("Không có tổ hợp " + oldName);
        newName = string.IsNullOrWhiteSpace(newName) ? oldName : newName.Trim();
        if (newName != oldName && NameUsed(p, newName, c)) return EditResult.Fail($"Tên {newName} đã dùng.");
        var test = new LoadCombination { Name = newName, Type = type, Items = Clean(items) };
        // kiểm tra trên bản sao để không phá dữ liệu khi lỗi
        string err = CheckItems(p, test, c);
        if (err != null) return EditResult.Fail(err);
        c.Type = type; c.Items = test.Items;
        if (newName != oldName)
        {
            foreach (var o in p.Combinations) foreach (var it in o.Items.Where(i => i.Case == oldName)) it.Case = newName;
            c.Name = newName;
        }
        return new EditResult { Ok = true, Message = $"Đã sửa tổ hợp {newName}: {ComboGenerator.Describe(c)}" };
    }

    /// <summary>Xoá tổ hợp; thành phần tham chiếu tới nó trong tổ hợp khác (tổ hợp bao) cũng bị bỏ.</summary>
    public static EditResult Delete(Project p, string name)
    {
        var c = p.Combinations.FirstOrDefault(x => x.Name == name);
        if (c == null) return EditResult.Fail("Không có tổ hợp " + name);
        p.Combinations.Remove(c);
        int refs = 0;
        foreach (var o in p.Combinations) refs += o.Items.RemoveAll(i => i.Case == name);
        return new EditResult { Ok = true, Message = $"Đã xoá tổ hợp {name}" + (refs > 0 ? $" và {refs} tham chiếu trong tổ hợp khác." : ".") };
    }

    /// <summary>Sao chép tổ hợp thành tên mới.</summary>
    public static EditResult Duplicate(Project p, string name)
    {
        var c = p.Combinations.FirstOrDefault(x => x.Name == name);
        if (c == null) return EditResult.Fail("Không có tổ hợp " + name);
        return Add(p, UniqueName(p, name + "_"), c.Type, c.Items.Select(i => new ComboItem(i.Case, i.Factor)));
    }

    /// <summary>Kiểm tra thành phần: phải là trường hợp tải hoặc tổ hợp đã có, không tự tham chiếu / vòng lặp.
    /// self = tổ hợp gốc khi sửa (được thay bằng test).</summary>
    private static string CheckItems(Project p, LoadCombination test, LoadCombination self = null)
    {
        if (test.Items.Count == 0) return "Tổ hợp chưa có thành phần nào.";
        foreach (var it in test.Items)
        {
            if (it.Case == test.Name || (self != null && it.Case == self.Name)) return $"Tổ hợp {test.Name} không được chứa chính nó.";
            bool isPat = p.Pattern(it.Case) != null;
            var sub = p.Combinations.FirstOrDefault(x => x.Name == it.Case && x != self);
            if (!isPat && sub == null) return $"Thành phần {it.Case} không phải trường hợp tải hay tổ hợp đã khai báo.";
            if (sub != null && test.Type != ComboType.Envelope && sub.Type == ComboType.Envelope)
                return $"Tổ hợp tuyến tính không chứa được tổ hợp bao {sub.Name} – đổi loại thành Bao.";
        }
        // vòng lặp tham chiếu
        var map = p.Combinations.Where(x => x != self).ToDictionary(x => x.Name, x => x.Items.Select(i => i.Case).ToList());
        map[test.Name] = test.Items.Select(i => i.Case).ToList();
        if (self != null && self.Name != test.Name)
            foreach (var k in map.Keys.ToList()) map[k] = map[k].Select(n => n == self.Name ? test.Name : n).ToList();
        if (HasCycle(map, test.Name)) return "Tổ hợp tham chiếu vòng (A chứa B, B chứa A).";
        return null;
    }

    private static bool HasCycle(Dictionary<string, List<string>> map, string start)
    {
        var stack = new HashSet<string>();
        bool Dfs(string n)
        {
            if (!map.TryGetValue(n, out var ch)) return false;
            if (!stack.Add(n)) return true;
            foreach (var c in ch) if (Dfs(c)) return true;
            stack.Remove(n);
            return false;
        }
        return Dfs(start);
    }

    /// <summary>Các vấn đề của danh sách tổ hợp: tham chiếu thiếu, tổ hợp rỗng, vòng lặp.</summary>
    public static List<string> Problems(Project p)
    {
        var r = new List<string>();
        var map = p.Combinations.GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First().Items.Select(i => i.Case).ToList());
        foreach (var g in p.Combinations.GroupBy(x => x.Name).Where(g => g.Count() > 1)) r.Add($"Tên tổ hợp {g.Key} bị trùng ({g.Count()} lần).");
        foreach (var c in p.Combinations)
        {
            if (c.Items.Count == 0) r.Add($"Tổ hợp {c.Name} rỗng.");
            foreach (var it in c.Items)
                if (p.Pattern(it.Case) == null && !map.ContainsKey(it.Case))
                    r.Add($"Tổ hợp {c.Name} tham chiếu {it.Case} chưa khai báo.");
            if (HasCycle(map, c.Name)) r.Add($"Tổ hợp {c.Name} tham chiếu vòng.");
        }
        return r;
    }

    /// <summary>Bỏ các thành phần tham chiếu trường hợp/tổ hợp không tồn tại. Trả về số thành phần đã bỏ.</summary>
    public static int RemoveMissingRefs(Project p)
    {
        int n = 0;
        bool changed = true;
        while (changed)
        {
            changed = false;
            var names = new HashSet<string>(p.Patterns.Select(x => x.Name).Concat(p.Combinations.Select(c => c.Name)));
            foreach (var c in p.Combinations)
            {
                int k = c.Items.RemoveAll(i => !names.Contains(i.Case));
                if (k > 0) { n += k; changed = true; }
            }
        }
        return n;
    }

    /// <summary>Tự sinh tổ hợp theo TCVN 2737:2023 (ComboGenerator). keepUser = true: giữ các tổ hợp người dùng tự đặt
    /// (tên khác TH#, TC#, BAO), đổi tên nếu trùng.</summary>
    public static int Generate(Project p, bool keepUser, double psi = 0.9)
    {
        var user = keepUser ? p.Combinations.Where(c => !IsGeneratedName(c.Name)).ToList() : new List<LoadCombination>();
        ComboGenerator.Generate(p, psi);
        int before = p.Combinations.Count;
        foreach (var u in user)
        {
            if (NameUsed(p, u.Name)) u.Name = UniqueName(p, u.Name + "_");
            p.Combinations.Add(u);
        }
        RemoveMissingRefs(p);
        return before;
    }

    /// <summary>Tên do ComboGenerator đặt: TH1…, TC1…, BAO.</summary>
    public static bool IsGeneratedName(string n)
    {
        if (n == "BAO") return true;
        if (n.Length > 2 && (n.StartsWith("TH") || n.StartsWith("TC")) && n.Substring(2).All(char.IsDigit)) return true;
        return false;
    }
}

/// <summary>Khai báo trường hợp tải (Define → Load Patterns của ETABS): thêm, đổi tên (cập nhật mọi tham chiếu), xoá.</summary>
public static class LoadPatternEditor
{
    public static EditResult Add(Project p, string name, LoadType type, double selfWeight = 0, double gamma = 1.2)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return EditResult.Fail("Tên trường hợp tải trống.");
        if (ComboEditor.NameUsed(p, name)) return EditResult.Fail($"Tên {name} đã dùng.");
        p.Patterns.Add(new LoadPattern { Name = name, Type = type, SelfWeight = selfWeight, Gamma = gamma });
        return new EditResult { Ok = true, Message = $"Đã thêm trường hợp tải {name}." };
    }

    /// <summary>Đổi tên: cập nhật tải trên thanh / nút / sàn, tổ hợp, nguồn khối lượng.</summary>
    public static EditResult Rename(Project p, string oldName, string newName)
    {
        var pat = p.Pattern(oldName);
        if (pat == null) return EditResult.Fail("Không có trường hợp tải " + oldName);
        newName = (newName ?? "").Trim();
        if (newName.Length == 0 || newName == oldName) return EditResult.Fail("Tên mới không hợp lệ.");
        if (ComboEditor.NameUsed(p, newName)) return EditResult.Fail($"Tên {newName} đã dùng.");
        foreach (var l in p.FrameLoads.Where(x => x.Pattern == oldName)) l.Pattern = newName;
        foreach (var l in p.NodeLoads.Where(x => x.Pattern == oldName)) l.Pattern = newName;
        foreach (var l in p.AreaLoads.Where(x => x.Pattern == oldName)) l.Pattern = newName;
        foreach (var c in p.Combinations) foreach (var it in c.Items.Where(i => i.Case == oldName)) it.Case = newName;
        if (p.Modal?.MassSource != null) foreach (var m in p.Modal.MassSource.Where(x => x.Pattern == oldName)) m.Pattern = newName;
        pat.Name = newName;
        return new EditResult { Ok = true, Message = $"Đã đổi tên {oldName} → {newName}." };
    }

    /// <summary>Xoá trường hợp tải cùng mọi tải gán và thành phần tổ hợp tham chiếu.</summary>
    public static EditResult Delete(Project p, string name)
    {
        var pat = p.Pattern(name);
        if (pat == null) return EditResult.Fail("Không có trường hợp tải " + name);
        p.Patterns.Remove(pat);
        int n = p.FrameLoads.RemoveAll(l => l.Pattern == name) + p.AreaLoads.RemoveAll(l => l.Pattern == name) + p.NodeLoads.RemoveAll(l => l.Pattern == name);
        p.Modal?.MassSource?.RemoveAll(m => m.Pattern == name);
        int refs = ComboEditor.RemoveMissingRefs(p);
        return new EditResult { Ok = true, Message = $"Đã xoá trường hợp tải {name} ({n} tải gán, {refs} thành phần tổ hợp)." };
    }

    /// <summary>Số tải nhập tay / tự sinh đang gán theo trường hợp tải.</summary>
    public static (int frame, int area, int node) LoadCounts(Project p, string name) =>
        (p.FrameLoads.Count(l => l.Pattern == name), p.AreaLoads.Count(l => l.Pattern == name), p.NodeLoads.Count(l => l.Pattern == name));
}
