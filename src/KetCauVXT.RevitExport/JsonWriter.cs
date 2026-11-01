using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KetCauVXT.RevitExport
{
    /// <summary>Bộ ghi JSON tối giản (Revit 2024 chạy .NET Framework 4.8 – không có System.Text.Json sẵn).
    /// Kết quả khớp lược đồ KetCauVXT.Import.RevitExchange.</summary>
    internal sealed class JsonWriter
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly Stack<bool> _first = new Stack<bool>();
        private int _indent;

        public override string ToString() => _sb.ToString();

        private void Comma()
        {
            if (_first.Count == 0) return;
            if (!_first.Peek()) _sb.Append(',');
            _first.Pop(); _first.Push(false);
            _sb.Append('\n').Append(' ', _indent * 2);
        }

        public JsonWriter BeginObject(string name = null) { Key(name); _sb.Append('{'); _first.Push(true); _indent++; return this; }
        public JsonWriter EndObject() { Close('}'); return this; }
        public JsonWriter BeginArray(string name = null) { Key(name); _sb.Append('['); _first.Push(true); _indent++; return this; }
        public JsonWriter EndArray() { Close(']'); return this; }

        private void Close(char c)
        {
            bool empty = _first.Pop();
            _indent--;
            if (!empty) _sb.Append('\n').Append(' ', _indent * 2);
            _sb.Append(c);
        }

        private void Key(string name)
        {
            Comma();
            if (name != null) { Str(name); _sb.Append(": "); }
        }

        public JsonWriter Prop(string name, string value) { Key(name); Str(value ?? ""); return this; }
        public JsonWriter Prop(string name, double value) { Key(name); _sb.Append(Num(value)); return this; }

        /// <summary>Mảng số [x, y] viết trên một dòng.</summary>
        public JsonWriter Pair(double a, double b) { Key(null); _sb.Append('[').Append(Num(a)).Append(", ").Append(Num(b)).Append(']'); return this; }

        private static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            return Math.Round(v, 5).ToString("0.#####", CultureInfo.InvariantCulture);
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
