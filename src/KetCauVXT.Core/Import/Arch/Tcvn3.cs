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
        return Convert(s);
    }

    /// <summary>Chuyển bắt buộc (khi biết chắc font là TCVN3 – vd. kiểu chữ .VnTime); chuỗi đã có ký tự Unicode &gt; U+00FF giữ nguyên.</summary>
    public static string Convert(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Any(c => c > 0xFF)) return s ?? "";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s) sb.Append(Map.TryGetValue(c, out var u) ? u : c);
        int up = s.Count(char.IsUpper), low = s.Count(c => c >= 'a' && c <= 'z');
        string r = sb.ToString();
        return up > low ? r.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) : r;
    }
}

/// <summary>Chuyển chữ gõ bảng mã VNI Windows (font VNI-Times, VNI-Helve…) sang Unicode: "Taàng" → "Tầng", "Phoøng nguû" → "Phòng ngủ".
/// Dấu thanh/dấu mũ là ký tự Latin-1 đứng SAU nguyên âm; ơ = ô, ư = ö, đ = ñ, i có dấu là ký tự riêng (í ì æ ó ò).</summary>
public static class VniText
{
    private const char Sac = '\u0301', Huyen = '\u0300', Hoi = '\u0309', Nga = '\u0303', Nang = '\u0323', Mu = '\u0302', Trang = '\u0306', Moc = '\u031B';

    /// <summary>Ký tự dấu đứng sau nguyên âm → các dấu tổ hợp (theo bảng VNI Windows, chữ hoa cùng nghĩa).</summary>
    private static readonly Dictionary<char, char[]> Mods = new()
    {
        ['ù'] = new[] { Sac }, ['ø'] = new[] { Huyen }, ['û'] = new[] { Hoi }, ['õ'] = new[] { Nga }, ['ï'] = new[] { Nang },
        ['â'] = new[] { Mu }, ['á'] = new[] { Mu, Sac }, ['à'] = new[] { Mu, Huyen }, ['å'] = new[] { Mu, Hoi }, ['ã'] = new[] { Mu, Nga }, ['ä'] = new[] { Mu, Nang },
        ['ê'] = new[] { Trang }, ['é'] = new[] { Trang, Sac }, ['è'] = new[] { Trang, Huyen }, ['ú'] = new[] { Trang, Hoi }, ['ü'] = new[] { Trang, Nga }, ['ë'] = new[] { Trang, Nang },
    };

    /// <summary>Ký tự đứng riêng thay cho cả chữ.</summary>
    private static readonly Dictionary<char, string> Alone = new()
    {
        ['ô'] = "o\u031B", ['Ô'] = "O\u031B", ['ö'] = "u\u031B", ['Ö'] = "U\u031B", ['ñ'] = "đ", ['Ñ'] = "Đ",
        ['í'] = "í", ['Í'] = "Í", ['ì'] = "ì", ['Ì'] = "Ì", ['æ'] = "i\u0309", ['Æ'] = "I\u0309", ['ó'] = "i\u0303", ['Ó'] = "I\u0303", ['ò'] = "i\u0323", ['Ò'] = "I\u0323",
    };

    private const string Strong = "øûïåäëüöñæØÛÏÅÄËÜÖÑÆ";

    private static bool IsVowel(char c) => "aeiouyAEIOUY".IndexOf(c) >= 0;

    private static char[] ModOf(char c) => Mods.TryGetValue(char.ToLowerInvariant(c), out var m) ? m : null;

    /// <summary>Có cặp "nguyên âm + ký tự dấu VNI" và có ký tự chỉ VNI mới dùng (ø û ï å ä ë ü ö ñ æ…). Không chắc → dùng gợi ý font.</summary>
    public static bool Looks(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Any(c => c > 0xFF)) return false;
        int pairs = 0;
        for (int i = 0; i + 1 < s.Length; i++)
            if ((IsVowel(s[i]) || s[i] == 'ô' || s[i] == 'Ô' || s[i] == 'ö' || s[i] == 'Ö') && ModOf(s[i + 1]) != null) pairs++;
        // chữ Unicode tiếng Việt chỉ gồm ký tự Latin-1 (vd. "Hoà", "khách") cũng có cặp nguyên âm + dấu → bắt buộc có ký tự chỉ VNI mới dùng
        return pairs >= 1 && s.Any(c => Strong.IndexOf(c) >= 0);
    }

    public static string Convert(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Any(c => c > 0xFF)) return s ?? "";
        var sb = new StringBuilder(s.Length + 8);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (Alone.TryGetValue(c, out var a))
            {
                sb.Append(a);
                // ơ, ư (và i có dấu không có) có thể kèm dấu thanh đứng sau
                if ((c == 'ô' || c == 'Ô' || c == 'ö' || c == 'Ö') && i + 1 < s.Length && ModOf(s[i + 1]) is { Length: 1 } t) { sb.Append(t[0]); i++; }
                continue;
            }
            if (IsVowel(c) && i + 1 < s.Length && ModOf(s[i + 1]) is { } m)
            {
                bool okMu = m[0] != Mu || "aeoAEO".IndexOf(c) >= 0, okTrang = m[0] != Trang || c == 'a' || c == 'A';
                if (okMu && okTrang) { sb.Append(c); foreach (char k in m) sb.Append(k); i++; continue; }
            }
            sb.Append(c);
        }
        string r = sb.ToString();
        try { return r.Normalize(NormalizationForm.FormC); } catch (ArgumentException) { return r; }
    }

    public static string Fix(string s) => Looks(s) ? Convert(s) : s;
}

/// <summary>Chọn cách chuyển bảng mã chữ tiếng Việt cũ theo gợi ý font (DxfEntity.FontHint) hoặc tự nhận dạng.</summary>
public static class VnText
{
    public static string Fix(string s, string fontHint = "")
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        if (fontHint == "VNI") return VniText.Convert(s);
        if (fontHint == "TCVN3") return Tcvn3.Looks(s) ? Tcvn3.Fix(s) : Tcvn3.Convert(s);
        if (VniText.Looks(s) && !Tcvn3Stronger(s)) return VniText.Convert(s);
        return Tcvn3.Fix(s);
    }

    /// <summary>TCVN3 thay cả nguyên âm bằng 1 ký tự (thường đứng sau phụ âm); VNI đặt dấu sau nguyên âm.</summary>
    private static bool Tcvn3Stronger(string s)
    {
        int tc = 0, vn = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c < 0xA0) continue;
            bool afterVowel = i > 0 && "aeiouyAEIOUYôÔöÖ".IndexOf(s[i - 1]) >= 0;
            if (afterVowel) vn++; else tc++;
        }
        return tc > vn;
    }
}
