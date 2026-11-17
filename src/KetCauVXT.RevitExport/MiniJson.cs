using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KetCauVXT.RevitExport
{
    /// <summary>Bộ đọc JSON tối giản (Revit 2023/2024 chạy .NET Framework 4.8 – không có System.Text.Json).
    /// Kết quả: Dictionary&lt;string, object&gt; (đối tượng), List&lt;object&gt; (mảng), double, string, bool, null.</summary>
    internal static class MiniJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            var v = Value(s, ref i);
            Ws(s, ref i);
            if (i < s.Length) throw new FormatException($"JSON: ký tự thừa tại vị trí {i}.");
            return v;
        }

        private static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON: hết dữ liệu.");
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (c == 't' && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (c == 'f' && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (c == 'n' && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            return Num(s, ref i);
        }

        private static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            i++;
            Ws(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(s, ref i);
                string k = Str(s, ref i);
                Ws(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException($"JSON: thiếu ':' tại {i}.");
                i++;
                d[k] = Value(s, ref i);
                Ws(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return d; }
                throw new FormatException($"JSON: thiếu '}}' tại {i}.");
            }
        }

        private static List<object> Arr(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            Ws(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Ws(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return l; }
                throw new FormatException($"JSON: thiếu ']' tại {i}.");
            }
        }

        private static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException($"JSON: cần chuỗi tại {i}.");
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            throw new FormatException("JSON: chuỗi chưa đóng.");
        }

        private static double Num(string s, ref int i)
        {
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == st) throw new FormatException($"JSON: giá trị không hợp lệ tại {i}.");
            return double.Parse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- truy cập an toàn
        public static Dictionary<string, object> O(object o, string key) =>
            o is Dictionary<string, object> d && d.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        public static List<object> A(object o, string key) =>
            o is Dictionary<string, object> d && d.TryGetValue(key, out var v) && v is List<object> l ? l : new List<object>();

        public static string S(object o, string key, string def = "") =>
            o is Dictionary<string, object> d && d.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : def;

        public static double D(object o, string key, double def = 0) =>
            o is Dictionary<string, object> d && d.TryGetValue(key, out var v) && v is double x ? x : def;
    }
}
