using KetCauVXT.Import;

namespace KetCauVXT.Model;

/// <summary>Thao tác khai báo / xoá cầu thang trên mô hình (giữ mô hình hợp lệ để chạy lại phân tích).</summary>
public static class StairEdit
{
    /// <summary>Cầu thang đã khai báo cho lỗ sàn (null nếu chưa).</summary>
    public static Stair ForOpening(Project p, FloorOpening o) =>
        o == null ? null : p.Stairs?.FirstOrDefault(s => s.OpeningId == o.Id);

    /// <summary>Cầu thang để sửa cho lỗ sàn: bản sao của cầu thang đã khai báo hoặc cầu thang mặc định mới.</summary>
    public static Stair Draft(Project p, FloorOpening o)
    {
        var ex = ForOpening(p, o);
        if (ex != null) return ex.Clone();
        var s = Stair.FromOpening(o, null);
        s.Note = "";
        s.Name = NextName(p);
        return s;
    }

    public static string NextName(Project p)
    {
        int k = 1;
        while (p.Stairs.Any(s => s.Name == "CT" + k)) k++;
        return "CT" + k;
    }

    /// <summary>Thêm mới hoặc thay cầu thang cùng Id / cùng lỗ sàn. Bỏ tải ước tính cũ của lỗ sàn (tải mới sinh khi phân tích).</summary>
    public static EditResult Apply(Project p, Stair s)
    {
        if (s == null) return EditResult.Fail("Chưa có cầu thang.");
        p.Stairs ??= new List<Stair>();
        if (s.OpeningId > 0 && p.Openings.All(o => o.Id != s.OpeningId)) return EditResult.Fail("Lỗ sàn cầu thang không còn tồn tại.");
        var L = s.Layout(p);
        if (!L.Valid) return EditResult.Fail(string.Join(" ", L.Warnings));
        int idx = p.Stairs.FindIndex(x => (s.Id > 0 && x.Id == s.Id) || (s.OpeningId > 0 && x.OpeningId == s.OpeningId));
        if (s.Id <= 0) s.Id = p.Stairs.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1;
        if (string.IsNullOrWhiteSpace(s.Name)) s.Name = NextName(p);
        if (string.IsNullOrEmpty(s.Story)) s.Story = L.TopStory;
        if (idx >= 0) p.Stairs[idx] = s; else p.Stairs.Add(s);
        if (s.OpeningId > 0) RemoveLegacyLoads(p, s.OpeningId);
        p.InvalidateIndex();
        string msg = $"Cầu thang {s.Name}: {Stair.TypeName(L.S.Type)}, {L.NRisers} bậc h = {L.Riser * 1000:0} mm, b = {L.Tread * 1000:0} mm (2h + b = {L.Check2hb * 1000:0} mm), bản dày {L.WaistT * 1000:0} mm";
        return new EditResult { Ok = true, Message = msg + (L.Warnings.Count > 0 ? " – " + string.Join(" ", L.Warnings) : "") };
    }

    /// <summary>Xoá cầu thang khai báo; lỗ sàn (nếu còn) trở lại tải ước tính cũ trên dầm viền.</summary>
    public static EditResult Remove(Project p, Stair s)
    {
        if (s == null || p.Stairs == null || !p.Stairs.Remove(s)) return EditResult.Fail("Chưa chọn cầu thang.");
        var o = p.Openings.FirstOrDefault(x => x.Id == s.OpeningId);
        if (o != null && p.FrameLoads.All(l => l.Source != "OPEN" + o.Id))
        {
            ImportGeometry.EnsurePatterns(p);
            var edge = ModelEdit.EdgeBeams(p, o.Story, (o.X0, o.Y0, o.X1, o.Y1));
            double len = edge.Sum(f => p.Length(f));
            double area = (o.X1 - o.X0) * (o.Y1 - o.Y0);
            if (len > 0.1)
                foreach (var f in edge)
                {
                    p.FrameLoads.Add(new FrameLoad { Element = f.Id, Pattern = "HTHT", Note = "Cầu thang (bản + bậc + hoàn thiện)", Source = "OPEN" + o.Id, Points = { new[] { 0.0, Math.Round(0.5 * area * 6.5 / len, 3) }, new[] { 1.0, Math.Round(0.5 * area * 6.5 / len, 3) } } });
                    p.FrameLoads.Add(new FrameLoad { Element = f.Id, Pattern = "HT", Note = "Hoạt tải cầu thang", Source = "OPEN" + o.Id, Points = { new[] { 0.0, Math.Round(0.5 * area * 3.0 / len, 3) }, new[] { 1.0, Math.Round(0.5 * area * 3.0 / len, 3) } } });
                }
        }
        p.InvalidateIndex();
        return new EditResult { Ok = true, Message = $"Đã xoá khai báo cầu thang {s.Name}." };
    }

    /// <summary>Bỏ tải ước tính cũ của lỗ sàn cầu thang: tải viền "OPEN{id}" và tải "Cầu thang" nhập từ bản vẽ kiến trúc trên dầm viền lỗ.</summary>
    public static int RemoveLegacyLoads(Project p, int openingId)
    {
        int n = p.FrameLoads.RemoveAll(l => l.Source == "OPEN" + openingId);
        var o = p.Openings.FirstOrDefault(x => x.Id == openingId);
        if (o == null) return n;
        try
        {
            var edge = new HashSet<int>(ModelEdit.EdgeBeams(p, o.Story, (o.X0, o.Y0, o.X1, o.Y1)).Select(f => f.Id));
            n += p.FrameLoads.RemoveAll(l => l.Source == "ARCH" && edge.Contains(l.Element) && (l.Note == "Cầu thang" || l.Note == "Hoạt tải cầu thang"));
        }
        catch (ArgumentException) { }
        return n;
    }
}
