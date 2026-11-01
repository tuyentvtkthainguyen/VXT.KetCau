using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using VXT.KetCau.Core.Drawing;

namespace VXT.KetCau.Dwg
{
    /// <summary>
    /// Ghi DXF R12 ASCII tối giản (mở được bằng mọi phiên bản AutoCAD/BricsCAD/ZWCAD).
    /// Chữ tiếng Việt mã hoá \U+XXXX, font Arial; đơn vị mm.
    /// </summary>
    public class DxfWriter : IDraw2D
    {
        readonly StringBuilder ent = new StringBuilder();
        readonly Dictionary<string, int> layers = new Dictionary<string, int>();
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        public DxfWriter()
        {
            // Lớp theo quy ước Kết cấu Soft: mỗi loại nét một lớp
            AddLayer("BE_TONG", 7); AddLayer("THEP", 1); AddLayer("DAI", 3); AddLayer("KICH_THUOC", 4);
            AddLayer("CHU", 2); AddLayer("KY_HIEU", 6); AddLayer("TRUC", 8); AddLayer("BANG", 7);
        }

        public void AddLayer(string name, int color) { layers[name] = color; }

        static string N(double v) => v.ToString("0.###", Ci);
        void G(int code, string v) { ent.Append(code).Append('\n').Append(v).Append('\n'); }
        void G(int code, double v) => G(code, N(v));

        public void Line(string layer, double x1, double y1, double x2, double y2)
        {
            G(0, "LINE"); G(8, layer); G(10, x1); G(20, y1); G(30, 0); G(11, x2); G(21, y2); G(31, 0);
        }

        public void Circle(string layer, double x, double y, double r)
        {
            G(0, "CIRCLE"); G(8, layer); G(10, x); G(20, y); G(30, 0); G(40, r);
        }

        /// <summary>Chấm đặc (dùng SOLID 4 đỉnh).</summary>
        public void Dot(string layer, double x, double y, double r)
        {
            G(0, "SOLID"); G(8, layer);
            G(10, x - r); G(20, y - r); G(30, 0); G(11, x + r); G(21, y - r); G(31, 0);
            G(12, x - r); G(22, y + r); G(32, 0); G(13, x + r); G(23, y + r); G(33, 0);
        }

        public void Text(string layer, double x, double y, double height, string text, int h, int v, double rotDeg)
        {
            G(0, "TEXT"); G(8, layer); G(7, "VXT");
            G(10, x); G(20, y); G(30, 0); G(40, height); G(1, Encode(text));
            if (Math.Abs(rotDeg) > 1e-6) G(50, rotDeg);
            if (h != 0 || v != 0) { G(72, h.ToString()); G(11, x); G(21, y); G(31, 0); G(73, v.ToString()); }
        }

        /// <summary>DXF R12: không tạo DIMENSION thật — để bộ dựng hình vẽ bằng nét + chữ.</summary>
        public bool Dimension(double x0, double y0, double x1, double y1, double lineY, bool vertical, string overrideText) => false;

        /// <summary>Mã hoá Unicode \U+XXXX cho ký tự ngoài ASCII (yêu cầu của DXF R12).</summary>
        public static string Encode(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s ?? "")
            {
                if (ch < 128) sb.Append(ch);
                else sb.Append("\\U+").Append(((int)ch).ToString("X4"));
            }
            return sb.ToString();
        }

        public void Save(string path)
        {
            var sb = new StringBuilder();
            void H(int c, string v) => sb.Append(c).Append('\n').Append(v).Append('\n');
            H(0, "SECTION"); H(2, "HEADER");
            H(9, "$ACADVER"); H(1, "AC1009");
            H(9, "$INSUNITS"); H(70, "4");
            H(9, "$DWGCODEPAGE"); H(3, "ANSI_1258");
            H(0, "ENDSEC");
            H(0, "SECTION"); H(2, "TABLES");
            H(0, "TABLE"); H(2, "LTYPE"); H(70, "1");
            H(0, "LTYPE"); H(2, "CONTINUOUS"); H(70, "0"); H(3, "Solid line"); H(72, "65"); H(73, "0"); H(40, "0.0");
            H(0, "ENDTAB");
            H(0, "TABLE"); H(2, "LAYER"); H(70, layers.Count.ToString());
            foreach (var kv in layers) { H(0, "LAYER"); H(2, kv.Key); H(70, "0"); H(62, kv.Value.ToString()); H(6, "CONTINUOUS"); }
            H(0, "ENDTAB");
            H(0, "TABLE"); H(2, "STYLE"); H(70, "1");
            H(0, "STYLE"); H(2, "VXT"); H(70, "0"); H(40, "0.0"); H(41, "0.8"); H(50, "0.0"); H(71, "0"); H(42, "2.5"); H(3, "arial.ttf"); H(4, "");
            H(0, "ENDTAB");
            H(0, "ENDSEC");
            H(0, "SECTION"); H(2, "ENTITIES");
            sb.Append(ent);
            H(0, "ENDSEC");
            H(0, "EOF");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
