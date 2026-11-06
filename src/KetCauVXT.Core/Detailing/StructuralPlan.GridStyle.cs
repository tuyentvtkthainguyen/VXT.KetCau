namespace KetCauVXT.Detailing;

// =====================================================================
// HIỂN THỊ TRỤC TRÊN MẶT BẰNG KẾT CẤU (Triển khai hồ sơ): bong bóng từng đầu (đầu dưới / trái = Start, đầu trên / phải = End)
// và ẩn trục trên từng mặt bằng. Khoá theo phương + TÊN trục (1, 2… / A, B…) – không đổi khi thêm bớt trục khác thì tên có thể
// dời: khi Insert / Remove / Move lưới, PlanGridEdit gọi RemapStyles để giữ kiểu theo trục.
// Hồ sơ cũ không có GridStyles → mọi trục hai bong bóng, hiện ở mọi mặt bằng.
// =====================================================================

/// <summary>Kiểu hiển thị một trục của mặt bằng kết cấu.</summary>
public class PlanGridStyle
{
    /// <summary>true: trục X (trục đứng 1, 2, 3…); false: trục Y (A, B, C…).</summary>
    public bool XDir { get; set; }
    /// <summary>Tên trục: "1", "2"… hoặc "A", "B"…</summary>
    public string Name { get; set; } = "";
    public bool BubbleStart { get; set; } = true;
    public bool BubbleEnd { get; set; } = true;
    /// <summary>Tên các mặt bằng (PlanLevel.Name) ẩn trục này.</summary>
    public List<string> HiddenLevels { get; set; } = new();
}

public partial class StructuralPlan
{
    /// <summary>Kiểu hiển thị trục (chỉ lưu trục khác mặc định).</summary>
    public List<PlanGridStyle> GridStyles { get; set; } = new();

    public static string GridName(bool xDir, int index) => xDir ? NameX(index) : NameY(index);

    /// <summary>Kiểu của trục (không có → mặc định, không thêm vào danh sách).</summary>
    public PlanGridStyle StyleOf(bool xDir, int index)
    {
        string n = GridName(xDir, index);
        return (GridStyles ?? new()).FirstOrDefault(s => s.XDir == xDir && s.Name == n) ?? new PlanGridStyle { XDir = xDir, Name = n };
    }

    private PlanGridStyle EnsureStyle(bool xDir, int index)
    {
        GridStyles ??= new();
        string n = GridName(xDir, index);
        var s = GridStyles.FirstOrDefault(q => q.XDir == xDir && q.Name == n);
        if (s == null) { s = new PlanGridStyle { XDir = xDir, Name = n }; GridStyles.Add(s); }
        return s;
    }

    /// <summary>Đặt bong bóng hai đầu của trục.</summary>
    public void SetBubbles(bool xDir, int index, bool start, bool end)
    {
        var s = EnsureStyle(xDir, index);
        s.BubbleStart = start; s.BubbleEnd = end;
        Prune();
    }

    /// <summary>Ẩn / hiện trục trên mặt bằng level.</summary>
    public void SetGridHidden(bool xDir, int index, string level, bool hidden)
    {
        if (string.IsNullOrEmpty(level)) return;
        var s = EnsureStyle(xDir, index);
        s.HiddenLevels ??= new();
        if (hidden) { if (!s.HiddenLevels.Contains(level)) s.HiddenLevels.Add(level); }
        else s.HiddenLevels.Remove(level);
        Prune();
    }

    /// <summary>Trục hiện trên mặt bằng level? (level null = luôn hiện).</summary>
    public bool GridVisible(bool xDir, int index, string level)
    {
        if (string.IsNullOrEmpty(level)) return true;
        var s = StyleOf(xDir, index);
        return s.HiddenLevels == null || !s.HiddenLevels.Contains(level);
    }

    /// <summary>Hiện lại mọi trục đã ẩn trên mặt bằng level (level null = mọi mặt bằng).</summary>
    public int ShowAllGrids(string level = null)
    {
        int n = 0;
        foreach (var s in GridStyles ?? new())
        {
            s.HiddenLevels ??= new();
            if (level == null) { n += s.HiddenLevels.Count; s.HiddenLevels.Clear(); }
            else if (s.HiddenLevels.Remove(level)) n++;
        }
        Prune();
        return n;
    }

    /// <summary>Số bong bóng sẽ vẽ trên mặt bằng level (các trục hiện).</summary>
    public int BubbleCount(string level)
    {
        int n = 0;
        for (int i = 0; i < Xs.Length; i++) if (GridVisible(true, i, level)) { var s = StyleOf(true, i); n += (s.BubbleStart ? 1 : 0) + (s.BubbleEnd ? 1 : 0); }
        for (int j = 0; j < Ys.Length; j++) if (GridVisible(false, j, level)) { var s = StyleOf(false, j); n += (s.BubbleStart ? 1 : 0) + (s.BubbleEnd ? 1 : 0); }
        return n;
    }

    /// <summary>Bỏ các kiểu bằng mặc định.</summary>
    private void Prune() => GridStyles?.RemoveAll(s => s.BubbleStart && s.BubbleEnd && (s.HiddenLevels == null || s.HiddenLevels.Count == 0));

    /// <summary>Sau khi thứ tự trục đổi (thêm / xoá / dời): oldNames[i] = tên cũ của trục nay ở vị trí i (null = trục mới).
    /// Kiểu được gán lại theo tên mới; kiểu của trục đã xoá bị bỏ.</summary>
    public void RemapStyles(bool xDir, IReadOnlyList<string> oldNames)
    {
        if (GridStyles == null || GridStyles.Count == 0) return;
        var old = GridStyles.Where(s => s.XDir == xDir).ToList();
        GridStyles.RemoveAll(s => s.XDir == xDir);
        for (int i = 0; i < oldNames.Count; i++)
        {
            if (oldNames[i] == null) continue;
            var s = old.FirstOrDefault(q => q.Name == oldNames[i]);
            if (s == null) continue;
            GridStyles.Add(new PlanGridStyle { XDir = xDir, Name = GridName(xDir, i), BubbleStart = s.BubbleStart, BubbleEnd = s.BubbleEnd, HiddenLevels = s.HiddenLevels?.ToList() ?? new() });
        }
    }
}
