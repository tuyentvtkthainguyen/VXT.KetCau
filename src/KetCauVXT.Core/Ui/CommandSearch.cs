using System.Globalization;
using System.Text;

namespace KetCauVXT.Ui;

// =====================================================================
// GIAO DIỆN GỌN – TÌM LỆNH NHANH (Ctrl+Q / Ctrl+Shift+P)
// So khớp không phân biệt dấu tiếng Việt, hoa/thường: "mat bang" → "Mặt bằng kết cấu".
// Xếp hạng: trùng hẳn > bắt đầu bằng > đầu từ > chứa > các từ (theo thứ tự bất kỳ) đều có mặt.
// Chỉ xử lý chuỗi trong máy – không gửi đi đâu.
// =====================================================================

/// <summary>Một lệnh trong bảng tìm lệnh nhanh.</summary>
public sealed class CommandEntry
{
    /// <summary>Tên lệnh hiển thị, vd. "Mặt bằng kết cấu".</summary>
    public string Title { get; set; } = "";
    /// <summary>Đường dẫn menu, vd. "Xuất hồ sơ › Mặt bằng kết cấu".</summary>
    public string Path { get; set; } = "";
    /// <summary>Phím tắt (nếu có), vd. "Ctrl+S".</summary>
    public string Gesture { get; set; } = "";
    /// <summary>Đối tượng giao diện gốc (MenuItem / Button) – giao diện tự gọi.</summary>
    public object Tag { get; set; }
    public override string ToString() => string.IsNullOrEmpty(Gesture) ? Title : Title + "   (" + Gesture + ")";
}

public static class CommandSearch
{
    /// <summary>Bỏ dấu tiếng Việt (kể cả đ/Đ), chữ thường, gộp khoảng trắng, bỏ ký tự '_' (phím tắt menu) và ký hiệu.</summary>
    public static string Fold(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var norm = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(norm.Length);
        bool space = false;
        foreach (char ch in norm)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch))
            {
                if (space && sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToLowerInvariant(ch));
                space = false;
            }
            else if (ch == '_') continue;   // phím tắt menu "_Tệp"
            else space = true;
        }
        return sb.ToString();
    }

    /// <summary>Điểm khớp (0 = không khớp; càng lớn càng sát).</summary>
    public static int Score(string query, string title, string path = "")
    {
        string q = Fold(query);
        if (q.Length == 0) return 1;
        string t = Fold(title);
        if (t == q) return 1000;
        if (t.StartsWith(q, StringComparison.Ordinal)) return 800 - Math.Min(t.Length, 200);
        if ((" " + t).Contains(" " + q, StringComparison.Ordinal)) return 600 - Math.Min(t.Length, 200);
        if (t.Contains(q, StringComparison.Ordinal)) return 400 - Math.Min(t.Length, 200);
        var words = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string all = t + " " + Fold(path);
        if (words.All(w => all.Contains(w, StringComparison.Ordinal))) return 200 - Math.Min(all.Length, 150);
        // chữ viết tắt: "mbkc" → "mặt bằng kết cấu"
        string initials = string.Concat(t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0]));
        if (q.Length >= 2 && !q.Contains(' ') && initials.StartsWith(q, StringComparison.Ordinal)) return 150;
        return 0;
    }

    /// <summary>Lọc + xếp hạng các lệnh theo chuỗi gõ (giữ thứ tự gốc khi bằng điểm).</summary>
    public static List<CommandEntry> Filter(IEnumerable<CommandEntry> all, string query, int max = 60) =>
        all.Select((c, i) => (c, i, s: Score(query, c.Title, c.Path)))
           .Where(x => x.s > 0)
           .OrderByDescending(x => x.s).ThenBy(x => x.i)
           .Take(max).Select(x => x.c).ToList();

    /// <summary>Tên lệnh sạch từ tiêu đề menu / nút: bỏ '_', dấu "…", ký hiệu đầu dòng (＋ ⇩ ▶ 🖨 ⇄).</summary>
    public static string CleanTitle(string header)
    {
        if (string.IsNullOrWhiteSpace(header)) return "";
        var s = header.Replace("_", "").Replace("…", "").Replace("...", "").Trim();
        int k = 0;
        while (k < s.Length && !char.IsLetterOrDigit(s[k]) && s[k] != '(') k++;
        return s.Substring(k).Trim();
    }
}

/// <summary>Biểu tượng (mã ký tự phông Segoe MDL2 Assets / Segoe Fluent Icons) cho nút lệnh theo từ khoá trong tên.</summary>
public static class UiGlyphs
{
    private static readonly (string Key, string Glyph)[] Map =
    {
        ("luu thanh", "\uE792"), ("luu", "\uE74E"), ("mo ho so mau", "\uE8F1"), ("mo", "\uE8E5"), ("moi", "\uE7C3"),
        ("cay", "\uE8FD"), ("nhap dwg", "\uE8B5"), ("tu autocad", "\uE8B5"), ("tu ban ve", "\uE8B5"), ("nhap revit", "\uE8B5"), ("nhap", "\uE8B5"),
        ("tao khung", "\uE80A"), ("khung tien che", "\uE8B9"), ("kiem tra mh", "\uE9D9"), ("kiem tra thep", "\uE73E"), ("kiem tra", "\uE9D9"),
        ("chay noi luc", "\uE8AB"), ("phan tich", "\uE768"), ("tinh toan bo", "\uE768"), ("tinh", "\uE8EF"), ("thiet ke", "\uE8EF"), ("bang ket qua", "\uE8A5"),
        ("tu dan trang", "\uE8CB"), ("khoa", "\uE72E"), ("dan trang", "\uE749"), ("in", "\uE749"), ("xuat", "\uEDE1"), ("dinh dang", "\uE8D2"), ("chuyen", "\uE8AB"),
        ("nhan ban", "\uE8C8"), ("xoa", "\uE74D"), ("them", "\uE710"), ("vua", "\uE9A6"), ("phong to", "\uE8A3"), ("thu nho", "\uE71F"),
        ("tim", "\uE721"), ("mo rong", "\uE740"), ("khoi phuc", "\uE73F"), ("tro giup", "\uE897"), ("huong dan", "\uE897"), ("tuy chon", "\uE713"),
    };

    public const string Hamburger = "\uE700", Search = "\uE721", FullScreen = "\uE740", Restore = "\uE73F", More = "\uE712", Add = "\uE710",
                        CollapseLeft = "\uE76B", ExpandRight = "\uE76C";

    /// <summary>Mã biểu tượng cho tên lệnh; "" nếu không có từ khoá phù hợp (khi đó giữ chữ).</summary>
    public static string For(string text)
    {
        string f = CommandSearch.Fold(text);
        if (f.Length == 0) return "";
        foreach (var (k, g) in Map)
            if (f == k || f.StartsWith(k + " ", StringComparison.Ordinal) || f.Contains(" " + k + " ", StringComparison.Ordinal) || f.EndsWith(" " + k, StringComparison.Ordinal) || (k.Contains(' ') && f.Contains(k, StringComparison.Ordinal)))
                return g;
        return "";
    }
}
