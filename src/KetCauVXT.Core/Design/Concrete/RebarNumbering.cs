using System.Globalization;
using System.Text;

namespace KetCauVXT.Design.Concrete;

/// <summary>Đánh số hiệu thép trong một cấu kiện theo quy tắc kiểu Kết cấu Soft (skill "danh-so-tag-thep-ket-cau", mục 1 và 8):
/// <list type="bullet">
/// <item>Mỗi cấu kiện đánh số riêng từ 1.</item>
/// <item>Thứ tự: thép dọc (dưới, trên, mũ gối, tăng cường, giá), sau đó đai, đai C, đai lồng, thép rải.</item>
/// <item>Khác vai trò/vị trí, Ø, chiều dài hoặc hình dạng → khác số. Thép trên và dưới giống hệt nhau vẫn khác số.</item>
/// <item>Thanh đối xứng / giống hệt nhau cùng vai trò dùng chung một số (vd. mũ gối trái – phải).</item>
/// <item>Mỗi số hiệu chỉ ứng với một bộ (Ø, chiều dài, hình dạng).</item>
/// </list>
/// Các BarMark cùng số hiệu vẫn giữ là các bản ghi riêng (vị trí Start/End/Span khác nhau để vẽ);
/// bảng thống kê gộp theo Mark.</summary>
public static class RebarNumbering
{
    /// <summary>Thứ tự vai trò (so khớp theo tiền tố dài nhất của Role).</summary>
    private static readonly (string Prefix, int Rank)[] RoleOrder =
    {
        ("Dưới", 0),
        ("Trên", 1),
        ("Mũ gối", 2),
        ("Tăng cường", 3),
        ("Giá", 4),
        ("Thép dọc", 5),
        ("Lớp dưới", 6),
        ("Lớp trên", 7),
        ("Đai lồng", 12),
        ("Đai C", 11),
        ("Đai", 10),
        ("Thép rải", 13),
    };

    /// <summary>Hạng thứ tự của vai trò; vai trò lạ xếp sau thép dọc, trước đai.</summary>
    public static int RoleRank(string role)
    {
        if (string.IsNullOrEmpty(role)) return 8;
        int best = 8, bestLen = -1;
        foreach (var (prefix, rank) in RoleOrder)
            if (role.StartsWith(prefix, StringComparison.Ordinal) && prefix.Length > bestLen)
            {
                best = rank; bestLen = prefix.Length;
            }
        return best;
    }

    /// <summary>Khoá nhận dạng: vai trò + vị trí trên/dưới + Ø + chiều dài (10 mm) + hình dạng + kích thước đoạn.</summary>
    public static string IdentityKey(BarMark b)
    {
        var sb = new StringBuilder();
        sb.Append(b.Role).Append('|').Append(b.IsTop ? 'T' : 'B').Append('|').Append(b.Dia).Append('|');
        sb.Append(Math.Round(b.Length * 100).ToString("0", CultureInfo.InvariantCulture)).Append('|').Append(b.Shape).Append('|');
        foreach (var s in b.Segments) sb.Append(Math.Round(s / 10.0).ToString("0", CultureInfo.InvariantCulture)).Append(',');
        return sb.ToString();
    }

    /// <summary>Đánh lại số hiệu cho danh sách thép của MỘT cấu kiện (sửa trực tiếp Mark) và sắp xếp lại danh sách theo số hiệu.
    /// Trả về từ điển khoá nhận dạng → số hiệu.</summary>
    public static Dictionary<string, int> Renumber(List<BarMark> bars)
    {
        var map = new Dictionary<string, int>();
        if (bars == null || bars.Count == 0) return map;

        // làm tròn chiều dài 10 mm trước khi so sánh
        foreach (var b in bars) b.Length = DesignUtil.R10(b.Length);

        var groups = bars
            .Select((b, i) => (b, i))
            .GroupBy(x => IdentityKey(x.b))
            .Select(g => new
            {
                Key = g.Key,
                Rank = RoleRank(g.First().b.Role),
                Start = g.Min(x => Math.Min(x.b.Start, x.b.End)),
                Dia = g.First().b.Dia,
                First = g.Min(x => x.i),
                Items = g.Select(x => x.b).ToList()
            })
            .OrderBy(g => g.Rank).ThenBy(g => Math.Round(g.Start, 2)).ThenByDescending(g => g.Dia).ThenBy(g => g.First)
            .ToList();

        int n = 1;
        foreach (var g in groups)
        {
            map[g.Key] = n;
            foreach (var b in g.Items) b.Mark = n;
            n++;
        }
        var sorted = bars.OrderBy(b => b.Mark).ThenBy(b => b.Start).ToList();
        bars.Clear();
        bars.AddRange(sorted);
        return map;
    }

    /// <summary>Kiểm tra mục 8 của quy tắc: mỗi số hiệu chỉ ứng với một bộ (Ø, chiều dài, hình dạng). Trả về danh sách lỗi.</summary>
    public static List<string> Validate(IEnumerable<BarMark> bars)
    {
        var err = new List<string>();
        foreach (var g in bars.GroupBy(b => b.Mark))
        {
            int k = g.Select(b => (b.Dia, Math.Round(b.Length * 100), b.Shape)).Distinct().Count();
            if (k > 1) err.Add("Số hiệu " + g.Key + " ứng với " + k + " loại thanh khác nhau");
        }
        return err;
    }

    /// <summary>Số hiệu của thép đai theo Ø (vai trò bắt đầu bằng "Đai" nhưng không phải "Đai C"/"Đai lồng"); 0 nếu không có.</summary>
    public static int StirrupMark(IEnumerable<BarMark> bars, int dia, string role = "Đai")
    {
        var b = bars.FirstOrDefault(x => x.Role == role && x.Dia == dia);
        return b?.Mark ?? 0;
    }

    /// <summary>Chữ ký toàn bộ thép của cấu kiện (dùng để gộp các cấu kiện giống hệt nhau).</summary>
    public static string Signature(IEnumerable<BarMark> bars)
    {
        var parts = bars.OrderBy(b => b.Mark).ThenBy(b => b.Start)
            .Select(b => b.Mark + ":" + IdentityKey(b) + ":" + b.Count + ":" + Math.Round(b.Spacing * 1000)
                         + ":" + Math.Round(b.Start * 100) + ":" + Math.Round(b.End * 100));
        return string.Join(";", parts);
    }
}
