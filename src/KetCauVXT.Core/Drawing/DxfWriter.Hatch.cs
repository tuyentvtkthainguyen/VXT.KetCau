using System.Globalization;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

// =====================================================================
// TÔ VẬT LIỆU MẶT CẮT (HATCH)
//   DxfWriter.Hatch(...) lưu một thực thể "HATCH" trong bộ ghi (đi cùng Append/AppendScaled/cắt khung/khoá/hiệu chỉnh như mọi thực thể):
//     8 lớp, [62 màu], 2 tên mẫu, 41 tỷ lệ mẫu (đơn vị mô hình / đơn vị mẫu), 52 góc mẫu (độ), 91 số vòng biên,
//     mỗi vòng: 93 số đỉnh, rồi các cặp 10/20 (vòng đầu = biên ngoài, các vòng sau = lỗ; tô theo luật chẵn–lẻ).
//   Ghi DXF R12 (ToDxfString): mô phỏng – SOLID → các tam giác/hình thang SOLID, mẫu nét → các LINE đã cắt theo biên (có giới hạn số nét).
//   Ghi DXF 2018 / DWG (KetCauVXT.Dwg – CadDocWriter): HATCH thật, định nghĩa nét mẫu lấy từ bảng HatchPatterns.
//   Màn hình / in / PDF (App – SheetRender): cùng bộ sinh nét PatternSegments ⇒ trùng với bản R12.
// Mẫu định nghĩa theo đơn vị "mm giấy" ở tỷ lệ mẫu 1 (như acadiso.pat): tỷ lệ mẫu trong mô hình = Scale × hệ số.
// =====================================================================

/// <summary>Một họ nét của mẫu tô: góc (độ), điểm gốc (X, Y), véc-tơ dời giữa hai nét liên tiếp (Dx, Dy – toạ độ thế giới, chưa xoay/phóng),
/// các đoạn nét (dương = nét, âm = khe, 0 = chấm); rỗng = nét liền.</summary>
public sealed class HatchLineDef
{
    public double Angle, X, Y, Dx, Dy;
    public double[] Dash = Array.Empty<double>();

    /// <summary>Theo cú pháp file .pat: góc, x, y, dời dọc nét (shift), khoảng cách nét (offset), đoạn nét…</summary>
    public static HatchLineDef Pat(double angle, double x, double y, double shift, double offset, params double[] dash)
    {
        double a = angle * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
        return new HatchLineDef { Angle = angle, X = x, Y = y, Dx = shift * c - offset * s, Dy = shift * s + offset * c, Dash = dash ?? Array.Empty<double>() };
    }

    /// <summary>Véc-tơ dời cho trực tiếp theo toạ độ thế giới.</summary>
    public static HatchLineDef World(double angle, double x, double y, double dx, double dy, params double[] dash) =>
        new() { Angle = angle, X = x, Y = y, Dx = dx, Dy = dy, Dash = dash ?? Array.Empty<double>() };
}

/// <summary>Mẫu tô: tên (ANSI31, AR-CONC…), mô tả, các họ nét; SOLID = tô đặc (không có nét).</summary>
public sealed class HatchPatternDef
{
    public string Name = "SOLID";
    public string Description = "";
    public HatchLineDef[] Lines = Array.Empty<HatchLineDef>();
    /// <summary>Tên mẫu có trong acad.pat/acadiso.pat (DXF mã 76 = 1); false = mẫu tự định nghĩa (mã 76 = 2).</summary>
    public bool Predefined = true;
    public bool IsSolid => Lines == null || Lines.Length == 0;
}

/// <summary>Bảng mẫu tô dùng được (tên không phân biệt hoa thường). Mẫu kiến trúc (AR-CONC, AR-SAND, EARTH, BRICK) là bản rút gọn
/// cùng dáng mẫu của AutoCAD – định nghĩa nét được ghi kèm trong HATCH nên mọi phần mềm CAD hiển thị giống nhau.
/// Thêm mẫu riêng bằng <see cref="Register"/> (vd. từ bảng góc/khoảng cách/nét).</summary>
public static class HatchPatterns
{
    public const string Solid = "SOLID";
    public const string Ansi31 = "ANSI31";
    public const string Ansi37 = "ANSI37";
    public const string ArConc = "AR-CONC";
    public const string ArSand = "AR-SAND";
    public const string Earth = "EARTH";
    public const string Brick = "BRICK";

    private static readonly Dictionary<string, HatchPatternDef> _defs = new(StringComparer.OrdinalIgnoreCase);

    static HatchPatterns()
    {
        var P = (Func<double, double, double, double, double, double[], HatchLineDef>)((a, x, y, sh, of, d) => HatchLineDef.Pat(a, x, y, sh, of, d));
        var none = Array.Empty<double>();
        Register(new HatchPatternDef { Name = Solid, Description = "Tô đặc" });
        Register(new HatchPatternDef { Name = Ansi31, Description = "Gạch chéo 45° (ANSI31) – bê tông lót, gạch xây, vật liệu chung",
            Lines = new[] { P(45, 0, 0, 0, 3.175, none) } });
        Register(new HatchPatternDef { Name = Ansi37, Description = "Gạch chéo lưới 45° (ANSI37)",
            Lines = new[] { P(45, 0, 0, 0, 3.175, none), P(135, 0, 0, 0, 3.175, none) } });
        Register(new HatchPatternDef { Name = Earth, Description = "Đất (EARTH) – bó 3 nét ngang/đứng xen kẽ",
            Lines = new[]
            {
                P(0, 0, 0, 6.35, 6.35, new[] { 6.35, -6.35 }),
                P(0, 0, 2.38125, 6.35, 6.35, new[] { 6.35, -6.35 }),
                P(0, 0, 4.7625, 6.35, 6.35, new[] { 6.35, -6.35 }),
                P(90, 0.79375, 5.55625, 6.35, 6.35, new[] { 6.35, -6.35 }),
                P(90, 3.175, 5.55625, 6.35, 6.35, new[] { 6.35, -6.35 }),
                P(90, 5.55625, 5.55625, 6.35, 6.35, new[] { 6.35, -6.35 }),
            } });
        Register(new HatchPatternDef { Name = Brick, Description = "Gạch xây (BRICK) – hàng gạch so le 12,7 × 6,35",
            Lines = new[]
            {
                P(0, 0, 0, 0, 6.35, none),
                P(90, 0, 0, 0, 12.7, new[] { 6.35, -6.35 }),
                P(90, 6.35, 0, 0, 12.7, new[] { -6.35, 6.35 }),
            } });
        // AR-CONC rút gọn: lưới u = (6; 0), v = (3; 5) (30 mm² giấy / ô) – mỗi ô 2 tam giác đá + 4 chấm.
        // Mỗi cạnh tam giác là một họ nét song song với một véc-tơ lưới nguyên thuỷ w (chu kỳ nét = |w|), véc-tơ dời z với |det(w, z)| = 30.
        double Ang(double x, double y) => Math.Atan2(y, x) * 180 / Math.PI;
        double Len(double x, double y) => Math.Sqrt(x * x + y * y);
        HatchLineDef Edge(double x0, double y0, double ex, double ey, double wx, double wy, double zx, double zy)
        {
            double le = Len(ex, ey), lw = Len(wx, wy);
            return HatchLineDef.World(Ang(ex, ey), x0, y0, zx, zy, le, -(lw - le));
        }
        HatchLineDef DotAt(double x, double y) => HatchLineDef.World(0, x, y, 3, 5, 0, -6);
        Register(new HatchPatternDef { Name = ArConc, Description = "Bê tông (AR-CONC) – đá dăm và chấm",
            Lines = new[]
            {
                // tam giác 1: (0;0) – (1,2;0) – (0,6;1)
                Edge(0, 0, 1.2, 0, 6, 0, 3, 5),
                Edge(1.2, 0, -0.6, 1.0, -3, 5, 6, 0),
                Edge(0.6, 1.0, -0.6, -1.0, -3, -5, 6, 0),
                // tam giác 2: (3,3;1,8) – (4,38;2,4) – (3,3;2,4)
                Edge(3.3, 1.8, 1.08, 0.6, 9, 5, 3, 5),
                Edge(4.38, 2.4, -1.08, 0, -6, 0, 3, 5),
                Edge(3.3, 2.4, 0, -0.6, 0, -10, 3, 5),
                DotAt(1.9, 3.1), DotAt(4.9, 0.5), DotAt(0.3, 2.6), DotAt(5.2, 3.9),
            } });
        // AR-SAND rút gọn: chấm rải theo 3 lưới lệch nhau (≈ 0,66 chấm / mm² giấy)
        Register(new HatchPatternDef { Name = ArSand, Description = "Cát (AR-SAND) – chấm rải",
            Lines = new[]
            {
                HatchLineDef.World(0, 0, 0, 1.1, 1.9, 0, -2.4),
                HatchLineDef.World(0, 0.9, 0.55, 1.1, 1.9, 0, -2.4),
                HatchLineDef.World(0, 1.7, 1.25, 1.1, 1.9, 0, -2.4),
            } });
    }

    /// <summary>Thêm / thay mẫu (tên trống → bỏ qua).</summary>
    public static void Register(HatchPatternDef d)
    {
        if (d == null || string.IsNullOrWhiteSpace(d.Name)) return;
        d.Name = d.Name.Trim().ToUpperInvariant();
        d.Lines ??= Array.Empty<HatchLineDef>();
        _defs[d.Name] = d;
    }

    /// <summary>Định nghĩa mẫu tự chọn từ bảng (góc°, khoảng cách nét, đoạn nét/khe…) – nét song song đơn giản, gốc (0;0).
    /// Vd. <c>Custom("VXT_GACH", (45, 2.0, null), (135, 4.0, new[] { 1.0, -1.0 }))</c>.</summary>
    public static HatchPatternDef Custom(string name, params (double Angle, double Spacing, double[] Dash)[] lines)
    {
        var d = new HatchPatternDef
        {
            Name = name, Description = "Mẫu tự định nghĩa", Predefined = false,
            Lines = (lines ?? Array.Empty<(double, double, double[])>()).Where(l => l.Spacing > 1e-9)
                .Select(l => HatchLineDef.Pat(l.Angle, 0, 0, 0, l.Spacing, l.Dash ?? Array.Empty<double>())).ToArray()
        };
        Register(d);
        return d;
    }

    /// <summary>Có mẫu này trong bảng.</summary>
    public static bool Has(string name) => !string.IsNullOrWhiteSpace(name) && _defs.ContainsKey(name.Trim());

    /// <summary>Định nghĩa mẫu; tên lạ → ANSI31.</summary>
    public static HatchPatternDef Get(string name) =>
        !string.IsNullOrWhiteSpace(name) && _defs.TryGetValue(name.Trim(), out var d) ? d : _defs[Ansi31];

    public static bool IsSolid(string name) => Get(name).IsSolid;

    public static IReadOnlyCollection<string> Names => _defs.Keys.ToList();
}

/// <summary>Vật liệu mặt cắt → mẫu tô quy ước.</summary>
public enum HatchMaterial
{
    /// <summary>Bê tông cốt thép – AR-CONC</summary>
    Concrete,
    /// <summary>Bê tông lót – ANSI31 dày</summary>
    LeanConcrete,
    /// <summary>Đất nền – EARTH 45°</summary>
    Earth,
    /// <summary>Cát đệm – AR-SAND</summary>
    Sand,
    /// <summary>Gạch xây trên mặt cắt – ANSI31 (quy ước bản vẽ kết cấu VN)</summary>
    Brick,
    /// <summary>Khối xây thể hiện hàng gạch – BRICK</summary>
    Masonry,
    /// <summary>Tô đặc</summary>
    Solid
}

/// <summary>Dữ liệu một thực thể HATCH của bộ ghi (đọc lại từ mã nhóm) – dùng cho bộ ghi DXF 2018/DWG, màn hình, kiểm thử.</summary>
public sealed class HatchData
{
    public string Layer = Lay.Hatch;
    /// <summary>Màu ACI (0 = theo lớp).</summary>
    public int Aci;
    public string Pattern = HatchPatterns.Solid;
    /// <summary>Tỷ lệ mẫu (đơn vị mô hình / đơn vị mẫu).</summary>
    public double PatScale = 1;
    /// <summary>Góc mẫu (độ).</summary>
    public double Angle;
    /// <summary>Vòng biên: [0] biên ngoài, còn lại lỗ (luật chẵn – lẻ).</summary>
    public List<List<(double X, double Y)>> Loops = new();
    public bool IsSolid => HatchPatterns.IsSolid(Pattern);
}

public sealed partial class DxfWriter
{
    /// <summary>Số nét tối đa của một miền tô khi mô phỏng (R12, màn hình); vượt → mẫu tự giãn thưa (tăng tỷ lệ).</summary>
    public static int MaxHatchSegments { get; set; } = 2500;

    /// <summary>Thiết lập bản vẽ đang cho tô vật liệu (DrawingStyle.HatchSections + định dạng xuất).</summary>
    public bool HatchOn => Style.HatchEnabled;

    /// <summary>Tỷ lệ mẫu trong mô hình cho mẫu định nghĩa theo mm giấy: Scale × hệ số × hệ số chung (Thiết lập bản vẽ).</summary>
    public double HatchScale(double factor = 1) => Scale * (factor > 0 ? factor : 1) * Style.HatchScaleFactor;

    /// <summary>Mẫu, hệ số tỷ lệ (mm giấy) và góc quy ước của vật liệu.</summary>
    public static (string Pattern, double Factor, double Angle) MaterialPattern(HatchMaterial m) => m switch
    {
        HatchMaterial.LeanConcrete => (HatchPatterns.Ansi31, 0.6, 0.0),
        HatchMaterial.Earth => (HatchPatterns.Earth, 0.5, 45.0),
        HatchMaterial.Sand => (HatchPatterns.ArSand, 1.0, 0.0),
        HatchMaterial.Brick => (HatchPatterns.Ansi31, 0.8, 0.0),
        HatchMaterial.Masonry => (HatchPatterns.Brick, 0.5, 0.0),
        HatchMaterial.Solid => (HatchPatterns.Solid, 1.0, 0.0),
        _ => (HatchPatterns.ArConc, 1.0, 0.0)
    };

    /// <summary>Tô vật liệu quy ước trên lớp HATCH nếu thiết lập bản vẽ cho phép (<see cref="HatchOn"/>). Trả về true nếu đã ghi.</summary>
    public bool HatchMat(HatchMaterial m, params IList<(double X, double Y)>[] loops)
    {
        if (!HatchOn) return false;
        var (pat, f, ang) = MaterialPattern(m);
        return Hatch(pat, HatchScale(f), ang, Lay.Hatch, loops);
    }

    /// <summary>Tô vật liệu hình chữ nhật (nếu thiết lập cho phép).</summary>
    public bool HatchMatRect(HatchMaterial m, double x1, double y1, double x2, double y2)
    {
        double a = Math.Min(x1, x2), b = Math.Max(x1, x2), c = Math.Min(y1, y2), d = Math.Max(y1, y2);
        if (b - a < 1e-9 || d - c < 1e-9) return false;
        return HatchMat(m, new List<(double X, double Y)> { (a, c), (b, c), (b, d), (a, d) });
    }

    /// <summary>Miền tô (luôn ghi, không phụ thuộc thiết lập): loops[0] biên ngoài, các vòng sau là lỗ.
    /// pattern: SOLID, ANSI31, ANSI37, AR-CONC, EARTH, AR-SAND, BRICK hoặc mẫu đã đăng ký (tên lạ → ANSI31);
    /// patternScale: tỷ lệ mẫu (đơn vị mô hình / đơn vị mẫu, xem <see cref="HatchScale"/>); angleDeg: góc xoay mẫu;
    /// color: màu ACI (null = theo lớp). Trả về false nếu biên không hợp lệ (ít hơn 3 đỉnh / diện tích 0).</summary>
    public bool Hatch(IList<IList<(double X, double Y)>> loops, string pattern, double patternScale, double angleDeg, string layer, int? color = null)
    {
        var clean = new List<List<(double X, double Y)>>();
        foreach (var l in loops ?? Array.Empty<IList<(double X, double Y)>>())
        {
            var c = CleanLoop(l);
            if (c != null) clean.Add(c);
        }
        if (clean.Count == 0) return false;
        string pat = string.IsNullOrWhiteSpace(pattern) ? HatchPatterns.Ansi31 : pattern.Trim().ToUpperInvariant();
        if (!HatchPatterns.Has(pat)) pat = HatchPatterns.Ansi31;
        double sc = double.IsNaN(patternScale) || double.IsInfinity(patternScale) || patternScale <= 0 ? 1 : patternScale;
        var box = Rect2.Empty;
        foreach (var l in clean) foreach (var p in l) box = box.Union(p.X, p.Y);
        var e = Add("HATCH", string.IsNullOrEmpty(layer) ? Lay.Hatch : layer, box);
        if (color is > 0 and < 256) e.Codes.Add((62, color.Value));
        e.Codes.Add((2, pat));
        e.Codes.Add((41, sc));
        e.Codes.Add((52, double.IsNaN(angleDeg) ? 0.0 : angleDeg));
        e.Codes.Add((91, clean.Count));
        foreach (var l in clean)
        {
            e.Codes.Add((93, l.Count));
            foreach (var p in l) { e.Codes.Add((10, p.X)); e.Codes.Add((20, p.Y)); }
        }
        return true;
    }

    /// <summary>Miền tô – dạng gọn: <c>w.Hatch("ANSI31", w.HatchScale(), 0, Lay.Hatch, outer, hole1…)</c>.</summary>
    public bool Hatch(string pattern, double patternScale, double angleDeg, string layer, params IList<(double X, double Y)>[] loops) =>
        Hatch(loops == null ? null : loops.Cast<IList<(double X, double Y)>>().ToList(), pattern, patternScale, angleDeg, layer);

    /// <summary>Bỏ đỉnh trùng liên tiếp và đỉnh đóng vòng; null nếu &lt; 3 đỉnh hoặc diện tích ≈ 0.</summary>
    private static List<(double X, double Y)> CleanLoop(IList<(double X, double Y)> l)
    {
        if (l == null || l.Count < 3) return null;
        var r = new List<(double X, double Y)>(l.Count);
        foreach (var p in l)
        {
            if (double.IsNaN(p.X) || double.IsNaN(p.Y) || double.IsInfinity(p.X) || double.IsInfinity(p.Y)) return null;
            if (r.Count > 0 && Math.Abs(r[^1].X - p.X) < 1e-9 && Math.Abs(r[^1].Y - p.Y) < 1e-9) continue;
            r.Add(p);
        }
        while (r.Count > 1 && Math.Abs(r[^1].X - r[0].X) < 1e-9 && Math.Abs(r[^1].Y - r[0].Y) < 1e-9) r.RemoveAt(r.Count - 1);
        if (r.Count < 3 || Math.Abs(Area(r)) < 1e-9) return null;
        return r;
    }

    private static double Area(IList<(double X, double Y)> p)
    {
        double a = 0;
        for (int i = 0, j = p.Count - 1; i < p.Count; j = i++) a += (p[j].X * p[i].Y - p[i].X * p[j].Y);
        return a / 2;
    }

    // ------------------------------------------------------------------
    // Đọc lại
    // ------------------------------------------------------------------
    internal static HatchData ParseHatch(DxfEnt e)
    {
        if (e == null || e.Type != "HATCH") return null;
        var h = new HatchData();
        bool layerSet = false;
        List<(double X, double Y)> cur = null;
        double px = double.NaN;
        foreach (var (code, val) in e.Codes)
        {
            switch (code)
            {
                case 8: if (!layerSet) { h.Layer = val as string ?? "0"; layerSet = true; } break;
                case 62: h.Aci = val is int ci ? ci : 0; break;
                case 2: h.Pattern = val as string ?? HatchPatterns.Solid; break;
                case 41: h.PatScale = Num(val); break;
                case 52: h.Angle = Num(val); break;
                case 93: cur = new List<(double X, double Y)>(); h.Loops.Add(cur); break;
                case 10: px = Num(val); break;
                case 20:
                    if (cur != null && !double.IsNaN(px)) cur.Add((px, Num(val)));
                    px = double.NaN;
                    break;
            }
        }
        h.Loops.RemoveAll(l => l.Count < 3);
        return h;
    }

    /// <summary>Các miền tô của bộ ghi (kiểm thử, bộ ghi DXF 2018/DWG).</summary>
    public List<HatchData> Hatches()
    {
        var res = new List<HatchData>();
        foreach (var e in _ents)
            if (e.Type == "HATCH" && ParseHatch(e) is { } h && h.Loops.Count > 0) res.Add(h);
        return res;
    }

    /// <summary>Miền tô của nét cơ bản PrimKind.Hatch (toạ độ của nét).</summary>
    public static HatchData HatchOf(DrawPrim pr)
    {
        if (pr == null || pr.Kind != PrimKind.Hatch) return null;
        var h = new HatchData { Layer = pr.Layer, Aci = pr.Aci, Pattern = pr.Pattern ?? HatchPatterns.Solid, PatScale = pr.PatScale > 0 ? pr.PatScale : 1, Angle = pr.Rot };
        int at = 0;
        foreach (int n in pr.Loops ?? Array.Empty<int>())
        {
            var l = new List<(double X, double Y)>(n);
            for (int i = 0; i < n && 2 * at + 1 < pr.Pts.Length; i++, at++) l.Add((pr.Pts[2 * at], pr.Pts[2 * at + 1]));
            if (l.Count >= 3) h.Loops.Add(l);
        }
        return h;
    }

    private static DrawPrim HatchPrim(HatchData h, int entIndex)
    {
        var pts = new List<double>();
        var counts = new List<int>();
        foreach (var l in h.Loops) { counts.Add(l.Count); foreach (var p in l) { pts.Add(p.X); pts.Add(p.Y); } }
        return new DrawPrim
        {
            Kind = PrimKind.Hatch, Layer = h.Layer, Ent = entIndex, Pts = pts.ToArray(), Loops = counts.ToArray(),
            Pattern = h.Pattern, PatScale = h.PatScale, Rot = h.Angle, Aci = h.Aci
        };
    }

    // ------------------------------------------------------------------
    // Hình học: nét mẫu, tô đặc
    // ------------------------------------------------------------------
    /// <summary>Các đoạn nét của mẫu tô trong miền (luật chẵn – lẻ: lỗ không có nét). Số đoạn vượt cap → mẫu giãn thưa dần
    /// (tỷ lệ × 1,6 mỗi lần); usedScale = tỷ lệ thực dùng. Chấm (đoạn 0) → đoạn ngắn 0,12 đơn vị mẫu.</summary>
    public static List<(double X1, double Y1, double X2, double Y2)> PatternSegments(IList<IList<(double X, double Y)>> loops, string pattern,
        double scale, double angleDeg, int cap, out double usedScale)
    {
        usedScale = scale;
        var res = new List<(double X1, double Y1, double X2, double Y2)>();
        var def = HatchPatterns.Get(pattern);
        if (def.IsSolid || loops == null || loops.Count == 0) return res;
        if (cap <= 0) cap = MaxHatchSegments;
        if (double.IsNaN(scale) || scale <= 0) scale = 1;
        var edges = new List<(double X1, double Y1, double X2, double Y2)>();
        foreach (var l in loops)
        {
            if (l == null || l.Count < 3) continue;
            for (int i = 0; i < l.Count; i++) { var a = l[i]; var b = l[(i + 1) % l.Count]; edges.Add((a.X, a.Y, b.X, b.Y)); }
        }
        if (edges.Count == 0) return res;
        double ra = angleDeg * Math.PI / 180, ca = Math.Cos(ra), sa = Math.Sin(ra);
        var ts = new List<double>();
        for (int attempt = 0; attempt < 10; attempt++)
        {
            res.Clear();
            bool over = false;
            double dotLen = 0.12 * scale;
            foreach (var ln in def.Lines)
            {
                double a = (ln.Angle + angleDeg) * Math.PI / 180, dx = Math.Cos(a), dy = Math.Sin(a), nx = -dy, ny = dx;
                double bx = scale * (ln.X * ca - ln.Y * sa), by = scale * (ln.X * sa + ln.Y * ca);
                double ox = scale * (ln.Dx * ca - ln.Dy * sa), oy = scale * (ln.Dx * sa + ln.Dy * ca);
                double sp = ox * nx + oy * ny;
                if (Math.Abs(sp) < 1e-9 * scale) continue;
                double pmin = double.MaxValue, pmax = double.MinValue;
                foreach (var ed in edges) { double p = ed.X1 * nx + ed.Y1 * ny; if (p < pmin) pmin = p; if (p > pmax) pmax = p; }
                double c0 = bx * nx + by * ny, k1 = (pmin - c0) / sp, k2 = (pmax - c0) / sp;
                double kmin = Math.Floor(Math.Min(k1, k2)), kmax = Math.Ceiling(Math.Max(k1, k2));
                if (kmax - kmin > cap) { over = true; break; }
                double[] dash = ln.Dash ?? Array.Empty<double>();
                double cycle = 0;
                foreach (double d in dash) cycle += Math.Abs(d * scale);
                for (double k = kmin; k <= kmax && !over; k++)
                {
                    double Ox = bx + k * ox, Oy = by + k * oy;
                    ts.Clear();
                    foreach (var ed in edges)
                    {
                        double sP = (ed.X1 - Ox) * nx + (ed.Y1 - Oy) * ny, sQ = (ed.X2 - Ox) * nx + (ed.Y2 - Oy) * ny;
                        if ((sP > 0) == (sQ > 0)) continue;
                        double tP = (ed.X1 - Ox) * dx + (ed.Y1 - Oy) * dy, tQ = (ed.X2 - Ox) * dx + (ed.Y2 - Oy) * dy;
                        ts.Add(tP + (tQ - tP) * sP / (sP - sQ));
                    }
                    if (ts.Count < 2) continue;
                    ts.Sort();
                    for (int i = 0; i + 1 < ts.Count; i += 2)
                    {
                        double t0 = ts[i], t1 = ts[i + 1];
                        if (t1 - t0 < 1e-9) continue;
                        if (dash.Length == 0 || cycle < 1e-9)
                        {
                            res.Add((Ox + t0 * dx, Oy + t0 * dy, Ox + t1 * dx, Oy + t1 * dy));
                        }
                        else
                        {
                            if ((t1 - t0) / cycle > cap) { over = true; break; }
                            for (double c = Math.Floor(t0 / cycle) * cycle; c < t1; c += cycle)
                            {
                                double acc = c;
                                foreach (double d0 in dash)
                                {
                                    double d = d0 * scale, len = Math.Abs(d);
                                    if (d > 0)
                                    {
                                        double s = Math.Max(acc, t0), en = Math.Min(acc + len, t1);
                                        if (en - s > 1e-9) res.Add((Ox + s * dx, Oy + s * dy, Ox + en * dx, Oy + en * dy));
                                    }
                                    else if (d0 == 0 && acc >= t0 && acc <= t1)
                                    {
                                        double en = Math.Min(acc + dotLen, t1);
                                        if (en - acc < 1e-9) en = acc + 1e-6 * scale;
                                        res.Add((Ox + acc * dx, Oy + acc * dy, Ox + en * dx, Oy + en * dy));
                                    }
                                    acc += len;
                                }
                                if (res.Count > cap) { over = true; break; }
                            }
                            if (over) break;
                        }
                    }
                    if (res.Count > cap) over = true;
                }
                if (over) break;
            }
            if (!over) { usedScale = scale; return res; }
            scale *= 1.6;
        }
        usedScale = scale;
        if (res.Count > cap) res.RemoveRange(cap, res.Count - cap);
        return res;
    }

    /// <summary>Nét mẫu của nét cơ bản PrimKind.Hatch (màn hình / in: trùng với bản mô phỏng R12).</summary>
    public static List<(double X1, double Y1, double X2, double Y2)> HatchSegments(DrawPrim pr)
    {
        var h = HatchOf(pr);
        if (h == null || h.IsSolid) return new List<(double X1, double Y1, double X2, double Y2)>();
        return PatternSegments(h.Loops.Cast<IList<(double X, double Y)>>().ToList(), h.Pattern, h.PatScale, h.Angle, MaxHatchSegments, out _);
    }

    /// <summary>Chia miền tô đặc thành các mảnh SOLID: một vòng lồi → quạt tam giác (n − 2), một vòng lõm → cắt tai,
    /// có lỗ (hoặc cắt tai lỗi) → các dải hình thang theo phương Y (luật chẵn – lẻ). Mảnh 3 hoặc 4 đỉnh theo thứ tự chu vi.</summary>
    public static List<(double X, double Y)[]> SolidPieces(IList<IList<(double X, double Y)>> loops)
    {
        var res = new List<(double X, double Y)[]>();
        var ls = (loops ?? Array.Empty<IList<(double X, double Y)>>()).Select(CleanLoop).Where(l => l != null).ToList();
        if (ls.Count == 0) return res;
        if (ls.Count == 1)
        {
            var p = ls[0];
            if (Area(p) < 0) p.Reverse();
            if (IsConvex(p))
            {
                for (int i = 1; i + 1 < p.Count; i++) res.Add(new[] { p[0], p[i], p[i + 1] });
                return res;
            }
            var ear = EarClip(p);
            if (ear != null) return ear;
        }
        return Slabs(ls);
    }

    private static double Cross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool IsConvex(List<(double X, double Y)> p)
    {
        for (int i = 0; i < p.Count; i++)
            if (Cross(p[i], p[(i + 1) % p.Count], p[(i + 2) % p.Count]) < -1e-9) return false;
        return true;
    }

    /// <summary>Cắt tai (đa giác đơn, ngược chiều kim đồng hồ); null nếu không cắt được.</summary>
    private static List<(double X, double Y)[]> EarClip(List<(double X, double Y)> poly)
    {
        var res = new List<(double X, double Y)[]>();
        var idx = Enumerable.Range(0, poly.Count).ToList();
        int guard = 0, maxGuard = poly.Count * poly.Count + 10;
        while (idx.Count > 3 && guard++ < maxGuard)
        {
            bool cut = false;
            for (int i = 0; i < idx.Count; i++)
            {
                var a = poly[idx[(i + idx.Count - 1) % idx.Count]]; var b = poly[idx[i]]; var c = poly[idx[(i + 1) % idx.Count]];
                double cr = Cross(a, b, c);
                if (cr <= 1e-12) { if (Math.Abs(cr) <= 1e-12) { idx.RemoveAt(i); cut = true; break; } continue; }
                bool inside = false;
                foreach (int j in idx)
                {
                    var q = poly[j];
                    if (q == a || q == b || q == c) continue;
                    if (Cross(a, b, q) >= 0 && Cross(b, c, q) >= 0 && Cross(c, a, q) >= 0) { inside = true; break; }
                }
                if (inside) continue;
                res.Add(new[] { a, b, c });
                idx.RemoveAt(i);
                cut = true;
                break;
            }
            if (!cut) return null;
        }
        if (idx.Count == 3)
        {
            var a = poly[idx[0]]; var b = poly[idx[1]]; var c = poly[idx[2]];
            if (Math.Abs(Cross(a, b, c)) > 1e-12) res.Add(new[] { a, b, c });
        }
        return idx.Count <= 3 ? res : null;
    }

    /// <summary>Dải hình thang theo các cao độ đỉnh (miền chẵn – lẻ, có lỗ).</summary>
    private static List<(double X, double Y)[]> Slabs(List<List<(double X, double Y)>> loops)
    {
        var res = new List<(double X, double Y)[]>();
        var edges = new List<(double X1, double Y1, double X2, double Y2)>();
        var ys = new List<double>();
        foreach (var l in loops)
            for (int i = 0; i < l.Count; i++)
            {
                var a = l[i]; var b = l[(i + 1) % l.Count];
                ys.Add(a.Y);
                if (Math.Abs(a.Y - b.Y) > 1e-12) edges.Add((a.X, a.Y, b.X, b.Y));
            }
        ys.Sort();
        var lv = new List<double>();
        foreach (double y in ys) if (lv.Count == 0 || y - lv[^1] > 1e-9) lv.Add(y);
        var xs = new List<(double Xm, double Xa, double Xb)>();
        for (int s = 0; s + 1 < lv.Count; s++)
        {
            double ya = lv[s], yb = lv[s + 1], ym = (ya + yb) / 2;
            xs.Clear();
            foreach (var e in edges)
            {
                double lo = Math.Min(e.Y1, e.Y2), hi = Math.Max(e.Y1, e.Y2);
                if (ym <= lo || ym >= hi) continue;
                double X(double y) => e.X1 + (e.X2 - e.X1) * (y - e.Y1) / (e.Y2 - e.Y1);
                xs.Add((X(ym), X(ya), X(yb)));
            }
            xs.Sort((p, q) => p.Xm.CompareTo(q.Xm));
            for (int i = 0; i + 1 < xs.Count; i += 2)
            {
                var L = xs[i]; var R = xs[i + 1];
                if (R.Xm - L.Xm < 1e-12) continue;
                res.Add(new[] { (L.Xa, ya), (R.Xa, ya), (R.Xb, yb), (L.Xb, yb) });
            }
        }
        return res;
    }

    /// <summary>Cắt đa giác theo hình chữ nhật (Sutherland – Hodgman). Kết quả có thể rỗng.</summary>
    public static List<(double X, double Y)> ClipPolygon(IList<(double X, double Y)> poly, Rect2 r)
    {
        var cur = (poly ?? Array.Empty<(double X, double Y)>()).ToList();
        for (int side = 0; side < 4 && cur.Count > 0; side++)
        {
            bool In((double X, double Y) p) => side switch { 0 => p.X >= r.X1, 1 => p.X <= r.X2, 2 => p.Y >= r.Y1, _ => p.Y <= r.Y2 };
            (double X, double Y) Cut((double X, double Y) a, (double X, double Y) b)
            {
                double t = side switch
                {
                    0 => (r.X1 - a.X) / (b.X - a.X),
                    1 => (r.X2 - a.X) / (b.X - a.X),
                    2 => (r.Y1 - a.Y) / (b.Y - a.Y),
                    _ => (r.Y2 - a.Y) / (b.Y - a.Y)
                };
                return (a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y));
            }
            var outp = new List<(double X, double Y)>(cur.Count + 4);
            for (int i = 0; i < cur.Count; i++)
            {
                var a = cur[(i + cur.Count - 1) % cur.Count]; var b = cur[i];
                bool ia = In(a), ib = In(b);
                if (ib) { if (!ia) outp.Add(Cut(a, b)); outp.Add(b); }
                else if (ia) outp.Add(Cut(a, b));
            }
            cur = outp;
        }
        return cur;
    }

    // ------------------------------------------------------------------
    // Mô phỏng R12
    // ------------------------------------------------------------------
    /// <summary>Ghi miền tô dạng R12 vào bộ ghi này: SOLID hoặc LINE trên lớp/màu của miền tô.</summary>
    private void EmitHatchR12(HatchData h)
    {
        if (h == null || h.Loops.Count == 0) return;
        int start = _ents.Count;
        var loops = h.Loops.Cast<IList<(double X, double Y)>>().ToList();
        if (h.IsSolid)
        {
            foreach (var pc in SolidPieces(loops))
            {
                if (pc.Length == 3) Solid(h.Layer, pc[0].X, pc[0].Y, pc[1].X, pc[1].Y, pc[2].X, pc[2].Y, pc[2].X, pc[2].Y);
                else Solid(h.Layer, pc[0].X, pc[0].Y, pc[1].X, pc[1].Y, pc[3].X, pc[3].Y, pc[2].X, pc[2].Y);
            }
        }
        else
            foreach (var (x1, y1, x2, y2) in PatternSegments(loops, h.Pattern, h.PatScale, h.Angle, MaxHatchSegments, out _))
                Line(h.Layer, x1, y1, x2, y2);
        if (h.Aci is > 0 and < 256) StyleFrom(start, h.Aci, null);
    }

    /// <summary>Các thực thể R12 (SOLID/LINE) thay cho một miền tô.</summary>
    private List<DxfEnt> ExpandHatchR12(DxfEnt e)
    {
        var tmp = new DxfWriter(Scale);
        tmp.EmitHatchR12(ParseHatch(e));
        return tmp._ents;
    }

    /// <summary>Số thực thể R12 (SOLID + LINE) mà các miền tô của bộ ghi sẽ thành khi ghi DXF R12 (kiểm thử).</summary>
    public int HatchR12EntityCount()
    {
        int n = 0;
        foreach (var e in _ents) if (e.Type == "HATCH") n += ExpandHatchR12(e).Count;
        return n;
    }

    /// <summary>Cắt miền tô theo khung (ghép có cắt – DxfWriter.Sheet.cs).</summary>
    private void ClipHatchInto(DxfEnt e, Rect2 clip)
    {
        var h = ParseHatch(e);
        if (h == null) return;
        var loops = new List<IList<(double X, double Y)>>();
        foreach (var l in h.Loops)
        {
            var c = ClipPolygon(l, clip);
            if (c.Count >= 3) loops.Add(c);
        }
        if (loops.Count == 0) return;
        Hatch(loops, h.Pattern, h.PatScale, h.Angle, h.Layer, h.Aci > 0 ? h.Aci : (int?)null);
    }
}
