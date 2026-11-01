using System.Text;

namespace KetCauVXT.Import.Arch;

/// <summary>Chuyển chữ gõ bằng bảng mã TCVN3 (ABC – font .VnTime, .VnArial…) sang Unicode.
/// Bản vẽ AutoCAD cũ ở Việt Nam thường dùng font TCVN3: "MÆT B»NG TÇNG 2" → "MẶT BẰNG TẦNG 2" (font chữ hoa .VnTimeH dùng chung mã chữ thường).</summary>
public static class Tcvn3
{
    private static readonly Dictionary<char, char> Map = Build();

    private static Dictionary<char, char> Build()
    {
        var m = new Dictionary<char, char>();
        void Add(int code, char c) => m[(char)code] = c;
        Add(0xA1, 'Ă'); Add(0xA2, 'Â'); Add(0xA3, 'Ê'); Add(0xA4, 'Ô'); Add(0xA5, 'Ơ'); Add(0xA6, 'Ư'); Add(0xA7, 'Đ');
        Add(0xA8, 'ă'); Add(0xA9, 'â'); Add(0xAA, 'ê'); Add(0xAB, 'ô'); Add(0xAC, 'ơ'); Add(0xAD, 'ư'); Add(0xAE, 'đ');
        void Row(string chars, params int[] codes) { for (int i = 0; i < codes.Length; i++) Add(codes[i], chars[i]); }
        Row("àảãáạ", 0xB5, 0xB6, 0xB7, 0xB8, 0xB9);
        Row("ằẳẵắặ", 0xBB, 0xBC, 0xBD, 0xBE, 0xC6);
        Row("ầẩẫấậ", 0xC7, 0xC8, 0xC9, 0xCA, 0xCB);
        Row("èẻẽéẹ", 0xCC, 0xCE, 0xCF, 0xD0, 0xD1);
        Row("ềểễếệ", 0xD2, 0xD3, 0xD4, 0xD5, 0xD6);
        Row("ìỉĩíị", 0xD7, 0xD8, 0xDC, 0xDD, 0xDE);
        Row("òỏõóọ", 0xDF, 0xE1, 0xE2, 0xE3, 0xE4);
        Row("ồổỗốộ", 0xE5, 0xE6, 0xE7, 0xE8, 0xE9);
        Row("ờởỡớợ", 0xEA, 0xEB, 0xEC, 0xED, 0xEE);
        Row("ùủũúụ", 0xEF, 0xF1, 0xF2, 0xF3, 0xF4);
        Row("ừửữứự", 0xF5, 0xF6, 0xF7, 0xF8, 0xF9);
        Row("ỳỷỹýỵ", 0xFA, 0xFB, 0xFC, 0xFD, 0xFE);
        return m;
    }

    /// <summary>Ký tự chỉ xuất hiện khi chữ là TCVN3 (không phải chữ cái tiếng Việt Unicode thuộc Latin-1).</summary>
    private const string Trigger = "¡¢£¤¥¦§¨©ª«¬­®µ¶·¸¹»¼½¾ÆÇËÎÏÐÑÓÖ×ØÞßåæçëîïñöø÷þ";

    public static bool Looks(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        bool trig = false;
        foreach (char c in s)
        {
            if (c > 0xFF) return false;           // đã là Unicode tiếng Việt
            if (Trigger.IndexOf(c) >= 0) trig = true;
        }
        return trig;
    }

    /// <summary>Chuyển nếu chuỗi có dấu hiệu TCVN3; chuỗi chữ in (đa số chữ cái ASCII viết hoa) được đổi sang chữ hoa.</summary>
    public static string Fix(string s)
    {
        if (!Looks(s)) return s;
        var sb = new StringBuilder(s.Length);
        foreach (char c in s) sb.Append(Map.TryGetValue(c, out var u) ? u : c);
        int up = s.Count(char.IsUpper), low = s.Count(c => c >= 'a' && c <= 'z');
        string r = sb.ToString();
        return up > low ? r.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) : r;
    }
}
