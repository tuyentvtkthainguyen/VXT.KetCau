namespace KetCauVXT.Ui;

/// <summary>Logic ô "Tất cả" ba trạng thái cho danh sách có ô tích (layer, nhóm nét, thẻ…):
/// trạng thái = true (tích hết) / false (không tích ô nào hoặc danh sách rỗng) / null (một phần);
/// bấm ô "Tất cả": đang tích hết → bỏ hết, còn lại (không ô nào / một phần) → tích hết.
/// Chỉ áp dụng cho các dòng đang hiện (đã lọc) – dòng bị lọc ẩn giữ nguyên.</summary>
public static class CheckAll
{
    /// <summary>true = tất cả tích, false = không ô nào (hoặc rỗng), null = một phần.</summary>
    public static bool? State(IEnumerable<bool> values)
    {
        bool any = false, anyOn = false, anyOff = false;
        foreach (bool v in values ?? Array.Empty<bool>())
        {
            any = true;
            if (v) anyOn = true; else anyOff = true;
            if (anyOn && anyOff) return null;
        }
        return any && anyOn;
    }

    /// <summary>Giá trị mới khi bấm ô "Tất cả" đang ở trạng thái <paramref name="state"/>.</summary>
    public static bool ToggleTarget(bool? state) => state != true;

    /// <summary>Đếm (số ô đang tích, tổng số ô).</summary>
    public static (int On, int Total) Count(IEnumerable<bool> values)
    {
        int on = 0, n = 0;
        foreach (bool v in values ?? Array.Empty<bool>()) { n++; if (v) on++; }
        return (on, n);
    }

    /// <summary>Áp lệnh cho danh sách giá trị: chỉ các chỉ số <paramref name="visible"/> (null = tất cả) bị đổi.
    /// mode: "all" tích hết, "none" bỏ hết, "invert" đảo, "toggle" theo ô "Tất cả".</summary>
    public static bool[] Apply(IReadOnlyList<bool> values, string mode, Func<int, bool> visible = null)
    {
        var res = values.ToArray();
        var idx = Enumerable.Range(0, res.Length).Where(i => visible == null || visible(i)).ToList();
        bool target = mode switch
        {
            "none" => false,
            "toggle" => ToggleTarget(State(idx.Select(i => res[i]))),
            _ => true
        };
        foreach (int i in idx) res[i] = mode == "invert" ? !res[i] : target;
        return res;
    }

    /// <summary>Chữ đếm dạng "3/10".</summary>
    public static string CountText(IEnumerable<bool> values)
    {
        var (on, n) = Count(values);
        return on + "/" + n;
    }
}
