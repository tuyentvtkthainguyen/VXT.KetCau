using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KetCauVXT.AcadLink
{
    /// <summary>Đọc/ghi JSON tối giản (AutoCAD 2021–2024 chạy .NET Framework 4.8 – không có System.Text.Json).
    /// Đọc: object → Dictionary&lt;string, object&gt; (không phân biệt hoa thường), mảng → List&lt;object&gt;, số → double, true/false → bool, null → null.</summary>
    internal static class MiniJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            var v = Value(s ?? "", ref i);
            return v;
        }

        private static void Ws(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '﻿') { i++; continue; }
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '/') { while (i < s.Length && s[i] != '\n') i++; continue; }
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '*') { int e = s.IndexOf("*/", i + 2, StringComparison.Ordinal); i = e < 0 ? s.Length : e + 2; continue; }
                break;
            }
        }

        private static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                i++;
                while (true)
                {
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == '}') { i++; break; }
                    string key = Str(s, ref i);
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ':') i++;
                    d[key] = Value(s, ref i);
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; break; }
                    throw new FormatException("JSON lỗi tại vị trí " + i);
                }
                return d;
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++;
                while (true)
                {
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ']') { i++; break; }
                    l.Add(Value(s, ref i));
                    Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; break; }
                    throw new FormatException("JSON lỗi tại vị trí " + i);
                }
                return l;
            }
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == st) throw new FormatException("JSON lỗi tại vị trí " + i);
            return double.Parse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string Str(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException("JSON: thiếu dấu \" tại vị trí " + i);
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 <= s.Length) { sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; }
                        break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        // ---- tiện ích đọc ----
        public static object Get(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(key, out v) ? v : null;
        }

        public static string Str(object o, string key, string def = "")
        {
            var v = Get(o, key);
            if (v == null) return def;
            if (v is double) return ((double)v).ToString(CultureInfo.InvariantCulture);
            return v.ToString();
        }

        public static double Num(object o, string key, double def = 0)
        {
            var v = Get(o, key);
            if (v is double) return (double)v;
            double r;
            if (v is string && double.TryParse((string)v, NumberStyles.Float, CultureInfo.InvariantCulture, out r)) return r;
            return def;
        }

        public static bool Bool(object o, string key, bool def = false)
        {
            var v = Get(o, key);
            if (v is bool) return (bool)v;
            if (v is double) return (double)v != 0;
            if (v is string) return string.Equals((string)v, "true", StringComparison.OrdinalIgnoreCase);
            return def;
        }

        public static List<object> Arr(object o, string key) => Get(o, key) as List<object> ?? new List<object>();
    }

    /// <summary>Bộ ghi JSON tối giản, thụt lề, số theo định dạng bất biến (dấu chấm).</summary>
    internal sealed class JsonOut
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly Stack<bool> _first = new Stack<bool>();
        private int _indent;

        public override string ToString() => _sb.ToString();

        private void Comma(bool newline = true)
        {
            if (_first.Count == 0) return;
            if (!_first.Peek()) _sb.Append(newline ? "," : ", ");
            _first.Pop(); _first.Push(false);
            if (newline) _sb.Append('\n').Append(' ', _indent * 2);
        }

        private void Key(string name, bool newline = true)
        {
            Comma(newline);
            if (name != null) { Str(name); _sb.Append(": "); }
        }

        public JsonOut BeginObject(string name = null) { Key(name); _sb.Append('{'); _first.Push(true); _indent++; return this; }
        public JsonOut EndObject() { Close('}'); return this; }
        public JsonOut BeginArray(string name = null) { Key(name); _sb.Append('['); _first.Push(true); _indent++; return this; }
        public JsonOut EndArray() { Close(']'); return this; }

        private void Close(char c)
        {
            bool empty = _first.Pop();
            _indent--;
            if (!empty) _sb.Append('\n').Append(' ', _indent * 2);
            _sb.Append(c);
        }

        public JsonOut Prop(string name, string value) { Key(name); Str(value ?? ""); return this; }
        public JsonOut Prop(string name, double value) { Key(name); _sb.Append(Num(value)); return this; }
        public JsonOut Prop(string name, int value) { Key(name); _sb.Append(value.ToString(CultureInfo.InvariantCulture)); return this; }
        public JsonOut Prop(string name, bool value) { Key(name); _sb.Append(value ? "true" : "false"); return this; }

        /// <summary>Mảng điểm [[x,y],[x,y]…] trên một dòng.</summary>
        public JsonOut Points(string name, IList<double[]> pts)
        {
            Key(name);
            _sb.Append('[');
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0) _sb.Append(", ");
                _sb.Append('[').Append(Num(pts[i][0])).Append(", ").Append(Num(pts[i][1])).Append(']');
            }
            _sb.Append(']');
            return this;
        }

        /// <summary>Mảng số trên một dòng.</summary>
        public JsonOut Numbers(string name, IList<double> v)
        {
            Key(name);
            _sb.Append('[');
            for (int i = 0; i < v.Count; i++) { if (i > 0) _sb.Append(", "); _sb.Append(Num(v[i])); }
            _sb.Append(']');
            return this;
        }

        public static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            return Math.Round(v, 6).ToString("0.######", CultureInfo.InvariantCulture);
        }

        private void Str(string s)
        {
            _sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) _sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }
    }
}
