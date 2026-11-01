using System.Globalization;
using System.Text;

namespace KetCauVXT.Tests;

/// <summary>Sinh bản vẽ kiến trúc sơ bộ mẫu (DXF, đơn vị mm): nhà phố 5×15 m, 3 tầng + tum, các mặt bằng đặt cạnh nhau.
/// Có block ký hiệu trục (ATTRIB), block cửa (cung), cửa sổ, cầu thang, ban công, WC, tên tầng 3 gõ TCVN3, cửa cuốn 3,8 m mặt tiền tầng 1.</summary>
public static class ArchSample
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Build()
    {
        var sb = new StringBuilder();
        void P(int code, object v) { sb.Append(code).Append('\n').Append(Convert.ToString(v, Inv)).Append('\n'); }
        string F(double v) => v.ToString("0.###", Inv);

        // HEADER
        P(0, "SECTION"); P(2, "HEADER");
        P(9, "$ACADVER"); P(1, "AC1009");
        P(9, "$INSUNITS"); P(70, 4);
        P(0, "ENDSEC");
        // TABLES: lớp + kiểu nét
        P(0, "SECTION"); P(2, "TABLES");
        P(0, "TABLE"); P(2, "LTYPE"); P(70, 2);
        P(0, "LTYPE"); P(2, "CONTINUOUS"); P(70, 0); P(3, "Solid line"); P(72, 65); P(73, 0); P(40, 0.0);
        P(0, "LTYPE"); P(2, "CENTER"); P(70, 0); P(3, "Center ____ _ ____"); P(72, 65); P(73, 4); P(40, 2.0); P(49, 1.25); P(49, -0.25); P(49, 0.25); P(49, -0.25);
        P(0, "ENDTAB");
        var layers = new[] { ("KT-TUONG", 7, "CONTINUOUS"), ("KT-TRUC", 1, "CENTER"), ("KT-CUA", 30, "CONTINUOUS"), ("KT-CAUTHANG", 4, "CONTINUOUS"),
                             ("KT-LANCAN", 8, "CONTINUOUS"), ("KT-TEXT", 2, "CONTINUOUS"), ("KT-TENHINH", 2, "CONTINUOUS") };
        P(0, "TABLE"); P(2, "LAYER"); P(70, layers.Length);
        foreach (var (n, c, lt) in layers) { P(0, "LAYER"); P(2, n); P(70, 0); P(62, c); P(6, lt); }
        P(0, "ENDTAB");
        P(0, "ENDSEC");

        // BLOCKS
        P(0, "SECTION"); P(2, "BLOCKS");
        // đầu trục: vòng tròn R400 + thuộc tính TEN
        P(0, "BLOCK"); P(8, "0"); P(2, "TRUC_BUBBLE"); P(70, 2); P(10, 0.0); P(20, 0.0); P(30, 0.0); P(3, "TRUC_BUBBLE");
        P(0, "CIRCLE"); P(8, "0"); P(10, 0.0); P(20, 0.0); P(30, 0.0); P(40, 400.0);
        P(0, "ATTDEF"); P(8, "0"); P(10, 0.0); P(20, 0.0); P(30, 0.0); P(40, 350.0); P(1, "X"); P(3, "Tên trục"); P(2, "TEN"); P(70, 0); P(72, 1); P(11, 0.0); P(21, 0.0); P(31, 0.0); P(74, 2);
        P(0, "ENDBLK"); P(8, "0");
        // cửa đi 900: cánh + cung
        P(0, "BLOCK"); P(8, "0"); P(2, "CUA_D1"); P(70, 0); P(10, 0.0); P(20, 0.0); P(30, 0.0); P(3, "CUA_D1");
        P(0, "LINE"); P(8, "0"); P(10, 0.0); P(20, 0.0); P(30, 0.0); P(11, 0.0); P(21, 900.0); P(31, 0.0);
        P(0, "ARC"); P(8, "0"); P(10, 0.0); P(20, 0.0); P(30, 0.0); P(40, 900.0); P(50, 0.0); P(51, 90.0);
        P(0, "ENDBLK"); P(8, "0");
        // cửa sổ 1200 trong tường 220
        P(0, "BLOCK"); P(8, "0"); P(2, "CUA_S1"); P(70, 0); P(10, 0.0); P(20, 0.0); P(30, 0.0); P(3, "CUA_S1");
        foreach (double y in new[] { -110.0, -40.0, 40.0, 110.0 })
        { P(0, "LINE"); P(8, "0"); P(10, 0.0); P(20, y); P(30, 0.0); P(11, 1200.0); P(21, y); P(31, 0.0); }
        P(0, "ENDBLK"); P(8, "0");
        P(0, "ENDSEC");

        // ENTITIES
        P(0, "SECTION"); P(2, "ENTITIES");
        void Line(string layer, double x1, double y1, double x2, double y2)
        { P(0, "LINE"); P(8, layer); P(10, F(x1)); P(20, F(y1)); P(30, 0); P(11, F(x2)); P(21, F(y2)); P(31, 0); }
        void Text(string layer, double x, double y, double h, string s)
        { P(0, "TEXT"); P(8, layer); P(10, F(x)); P(20, F(y)); P(30, 0); P(40, F(h)); P(1, s); }
        void Insert(string layer, string block, double x, double y, double rot, string attr = null)
        {
            P(0, "INSERT"); P(8, layer);
            if (attr != null) P(66, 1);
            P(2, block); P(10, F(x)); P(20, F(y)); P(30, 0); P(50, F(rot));
            if (attr != null)
            {
                P(0, "ATTRIB"); P(8, "KT-TRUC"); P(10, F(x)); P(20, F(y)); P(30, 0); P(40, 350); P(1, attr); P(2, "TEN"); P(70, 0); P(72, 1); P(11, F(x)); P(21, F(y)); P(31, 0); P(74, 2);
                P(0, "SEQEND"); P(8, layer);
            }
        }
        // tường 2 nét: tim (x1,y1)-(x2,y2) song song trục, chiều dày t, các khe [a,b] dọc theo tường
        void Wall(double ox, double x1, double y1, double x2, double y2, double t, params (double a, double b)[] gaps)
        {
            bool hz = Math.Abs(y1 - y2) < 1e-6;
            double a0 = hz ? Math.Min(x1, x2) : Math.Min(y1, y2), a1 = hz ? Math.Max(x1, x2) : Math.Max(y1, y2), c = hz ? y1 : x1;
            var pieces = new List<(double, double)>();
            double cur = a0;
            foreach (var (ga, gb) in gaps.OrderBy(g => g.a)) { if (ga > cur) pieces.Add((cur, ga)); cur = gb; }
            if (a1 > cur) pieces.Add((cur, a1));
            foreach (var (pa, pb) in pieces)
                foreach (double side in new[] { -t / 2, t / 2 })
                {
                    if (hz) Line("KT-TUONG", ox + pa, c + side, ox + pb, c + side);
                    else Line("KT-TUONG", ox + c + side, pa, ox + c + side, pb);
                }
        }

        var gx = new[] { (0.0, "1"), (5000.0, "2") };
        var gy = new[] { (0.0, "A"), (5000.0, "B"), (9500.0, "C"), (15000.0, "D") };
        string[] titles =
        {
            "M\\U+1EB6T B\\U+1EB0NG T\\U+1EA6NG 1",      // mã \U+ của AutoCAD
            "MẶT BẰNG TẦNG 2 (+3.600)",                   // Unicode
            "MÆT B»NG TÇNG 3",                            // TCVN3 (.VnTimeH)
            "MẶT BẰNG TẦNG TUM",
        };
        for (int k = 0; k < 4; k++)
        {
            double ox = k * 14000;
            bool tum = k == 3;
            // trục
            foreach (var (x, n) in gx)
            {
                Line("KT-TRUC", ox + x, -2500, ox + x, 17000);
                Insert("KT-TRUC", "TRUC_BUBBLE", ox + x, 17400, 0, n);
            }
            foreach (var (y, n) in gy)
            {
                Line("KT-TRUC", ox - 2500, y, ox + 7500, y);
                Insert("KT-TRUC", "TRUC_BUBBLE", ox - 2900, y, 0, n);
            }
            Text("KT-TENHINH", ox + 500, tum ? 3300 : -3800, 500, titles[k]);
            if (tum)
            {
                // tum: phòng thang trên trục B–C
                Wall(ox, 0, 5000, 5000, 5000, 220, (3800, 4700));
                Wall(ox, 0, 9500, 5000, 9500, 220);
                Wall(ox, 0, 5000, 0, 9500, 220);
                Wall(ox, 5000, 5000, 5000, 9500, 220);
                Wall(ox, 1310, 5055, 1310, 9445, 110);
                StairDraw(ox);
                Text("KT-TEXT", ox + 3000, 7200, 250, "TUM THANG");
                Text("KT-TEXT", ox + 2500, 2500, 250, "SÂN THƯỢNG");
                continue;
            }
            // tường bao 220 (mặt tiền tầng 1: cửa cuốn 3,8 m; tầng 2–3: cửa ra ban công 1,4 m)
            if (k == 0) Wall(ox, 0, 0, 5000, 0, 220, (600, 4400));
            else Wall(ox, 0, 0, 5000, 0, 220, (1800, 3200));
            Wall(ox, 0, 15000, 5000, 15000, 220);
            Wall(ox, 0, 0, 0, 15000, 220);
            Wall(ox, 5000, 0, 5000, 15000, 220);
            // tường ngăn 110
            Wall(ox, 1310, 5000, 4890, 5000, 110, (3800, 4700));
            Wall(ox, 1310, 9500, 4890, 9500, 110, (2000, 2900));
            Wall(ox, 1310, 5055, 1310, 9445, 110);
            // WC
            Wall(ox, 3500, 9555, 3500, 12000, 110, (10200, 10900));
            Wall(ox, 3555, 12000, 4890, 12000, 110);
            // cửa đi, cửa sổ
            Insert("KT-CUA", "CUA_D1", ox + 3800, 5000, 0);
            Insert("KT-CUA", "CUA_D1", ox + 2000, 9500, 0);
            Insert("KT-CUA", "CUA_D1", ox + 3500, 10200, 90);
            Insert("KT-CUA", "CUA_S1", ox + 1900, 15000, 0);
            StairDraw(ox);
            Text("KT-CAUTHANG", ox + 500, 6000, 200, "LÊN");
            string[][] rooms =
            {
                new[] { "GARA", "P.KHÁCH", "BẾP + ĂN", "WC" },
                new[] { "P.NGỦ 1", "SINH HOẠT", "P.NGỦ 2", "WC" },
                new[] { "P.THỜ", "KHO", "P.NGỦ 3", "WC" },
            };
            Text("KT-TEXT", ox + 2500, 2500, 250, rooms[k][0]);
            Text("KT-TEXT", ox + 3000, 7200, 250, rooms[k][1]);
            Text("KT-TEXT", ox + 1500, 13500, 250, rooms[k][2]);
            Text("KT-TEXT", ox + 4100, 10700, 250, rooms[k][3]);
            if (k > 0)
            {
                // ban công 1,2 m phía trước
                Line("KT-LANCAN", ox + 0, -1300, ox + 5000, -1300);
                Line("KT-LANCAN", ox + 0, -110, ox + 0, -1300);
                Line("KT-LANCAN", ox + 5000, -110, ox + 5000, -1300);
                Text("KT-TEXT", ox + 2000, -800, 250, "BAN CÔNG");
            }
        }

        void StairDraw(double ox)
        {
            Line("KT-CAUTHANG", ox + 110, 5055, ox + 1255, 5055);
            Line("KT-CAUTHANG", ox + 1255, 5055, ox + 1255, 9445);
            Line("KT-CAUTHANG", ox + 1255, 9445, ox + 110, 9445);
            Line("KT-CAUTHANG", ox + 110, 9445, ox + 110, 5055);
            for (double y = 5400; y < 9200; y += 260) Line("KT-CAUTHANG", ox + 110, y, ox + 1255, y);
        }

        P(0, "ENDSEC");
        P(0, "EOF");
        return sb.ToString();
    }
}
