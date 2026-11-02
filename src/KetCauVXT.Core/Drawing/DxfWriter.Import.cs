using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KetCauVXT.Drawing;

/// <summary>Nạp lại bản vẽ DXF (ASCII, mọi phiên bản R12…2018) thành bộ ghi – dùng cho "bản vẽ đã khoá" (người dùng sửa trong AutoCAD rồi nạp lại):
/// LINE, POLYLINE/VERTEX, LWPOLYLINE (cung lồi bulge chia đoạn), CIRCLE, ARC, TEXT/ATTRIB (góc xoay, căn lề), MTEXT (bỏ mã định dạng),
/// SOLID/TRACE, INSERT và DIMENSION (phân rã block, lồng ≤ 8 cấp). Chỉ model space. Thực thể khác (HATCH, SPLINE, ELLIPSE…) được bỏ qua và đếm trong cảnh báo.
/// Lớp mang tên xuất theo chuẩn layer CAD đang áp dụng được đổi lại về tên nội bộ (để hiển thị màu nét đúng, xuất lại đúng chuẩn).</summary>
public sealed partial class DxfWriter
{
    private sealed class RawDxf
    {
        public string Type = "";
        public List<(int Code, string Val)> Codes = new();
        public List<RawDxf> Sub;   // VERTEX của POLYLINE, ATTRIB của INSERT
        public string S(int code) { foreach (var (c, v) in Codes) if (c == code) return v; return null; }
        public double D(int code, double def = 0)
        {
            string s = S(code);
            return s != null && double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v) ? v : def;
        }
        public int I(int code, int def = 0)
        {
            string s = S(code);
            if (s == null) return def;
            return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v
                : double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double dv) ? (int)dv : def;
        }
    }

    private readonly struct Xf
    {
        public readonly double A, B, C, D, E, F;
        public Xf(double a, double b, double c, double d, double e, double f) { A = a; B = b; C = c; D = d; E = e; F = f; }
        public static Xf Identity => new(1, 0, 0, 1, 0, 0);
        public (double X, double Y) Map(double x, double y) => (A * x + B * y + E, C * x + D * y + F);
        public double Scale => Math.Sqrt(Math.Abs(A * D - B * C));
        public double Rot => Math.Atan2(C, A) * 180 / Math.PI;
        public bool Mirror => A * D - B * C < 0;
        /// <summary>this ∘ o (áp o trước rồi this).</summary>
        public Xf Then(Xf o) => new(A * o.A + B * o.C, A * o.B + B * o.D, C * o.A + D * o.C, C * o.B + D * o.D, A * o.E + B * o.F + E, C * o.E + D * o.F + F);
    }

    /// <summary>Đọc nội dung file DXF (đã giải mã thành chuỗi) thành bộ ghi tỷ lệ 1/scale. warnings: các thực thể không hỗ trợ.</summary>
    public static DxfWriter FromDxfText(string text, double scale, out List<string> warnings)
    {
        warnings = new List<string>();
        var w = new DxfWriter(scale);
        if (string.IsNullOrEmpty(text)) { warnings.Add("File DXF rỗng."); return w; }
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var pairs = new List<(int Code, string Val)>(lines.Length / 2 + 1);
        for (int i = 0; i + 1 < lines.Length; i += 2)
        {
            if (!int.TryParse(lines[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code)) { warnings.Add("File DXF lỗi định dạng (mã nhóm không phải số)."); break; }
            pairs.Add((code, code == 0 ? lines[i + 1].Trim() : lines[i + 1].TrimEnd()));
        }

        var blocks = new Dictionary<string, (double Bx, double By, List<RawDxf> Ents)>(StringComparer.OrdinalIgnoreCase);
        var model = new List<RawDxf>();
        int p = 0;
        while (p < pairs.Count)
        {
            if (pairs[p].Code == 0 && pairs[p].Val == "SECTION" && p + 1 < pairs.Count && pairs[p + 1].Code == 2)
            {
                string sec = pairs[p + 1].Val.Trim();
                p += 2;
                if (sec == "ENTITIES") model = ReadEntityList(pairs, ref p, "ENDSEC");
                else if (sec == "BLOCKS")
                {
                    while (p < pairs.Count && !(pairs[p].Code == 0 && pairs[p].Val == "ENDSEC"))
                    {
                        if (pairs[p].Code == 0 && pairs[p].Val == "BLOCK")
                        {
                            var head = ReadOne(pairs, ref p);
                            var ents = ReadEntityList(pairs, ref p, "ENDBLK");
                            string name = head.S(2) ?? head.S(3) ?? "";
                            if (name.Length > 0) blocks[name] = (head.D(10), head.D(20), ents);
                            if (p < pairs.Count) ReadOne(pairs, ref p);   // ENDBLK
                        }
                        else p++;
                    }
                }
                else while (p < pairs.Count && !(pairs[p].Code == 0 && pairs[p].Val == "ENDSEC")) p++;
            }
            p++;
        }

        // lớp mang tên xuất theo chuẩn → tên nội bộ
        var rev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var std = w.Standard ?? KetCauVXT.Model.CadStandard.Active;
        if (std != null) foreach (var kv in OutputNameMap(std)) if (!rev.ContainsKey(kv.Value)) rev[kv.Value] = kv.Key;
        foreach (var (name, _, _) in LayerDefs) rev[name] = name;
        string MapLayer(string ln)
        {
            ln = string.IsNullOrWhiteSpace(ln) ? "0" : ln.Trim();
            return rev.TryGetValue(ln, out var k) ? k : KetCauVXT.Model.CadStandard.SafeLayerName(ln);
        }

        var skipped = new Dictionary<string, int>(StringComparer.Ordinal);
        int count = 0;
        void Emit(List<RawDxf> list, Xf xf, string parentLayer, int depth)
        {
            foreach (var e in list)
            {
                if (count > 2_000_000) return;
                if (e.I(67) == 1) continue;   // paper space
                string layer = e.S(8) ?? "0";
                if (layer == "0" && parentLayer != null) layer = parentLayer;
                string L = MapLayer(layer);
                int aci = e.I(62);
                string lt = (e.S(6) ?? "").Trim().ToUpperInvariant();
                if (!LtypeDefs.Any(d => d.Name == lt) || lt == "CONTINUOUS") lt = null;
                else if (std == null && lt != "DASHED" && lt != "CENTER") lt = null;   // bảng LTYPE kiểu cũ chỉ có 3 kiểu nét
                int start = w._ents.Count;
                switch (e.Type)
                {
                    case "LINE":
                    {
                        var (x1, y1) = xf.Map(e.D(10), e.D(20)); var (x2, y2) = xf.Map(e.D(11), e.D(21));
                        w.Line(L, x1, y1, x2, y2);
                        break;
                    }
                    case "POLYLINE":
                    {
                        int fl = e.I(70);
                        if ((fl & (16 | 64)) != 0) { Skip("POLYLINE lưới 3D"); break; }
                        var pts = new List<(double X, double Y, double B)>();
                        foreach (var v in e.Sub ?? new List<RawDxf>()) if (v.Type == "VERTEX") pts.Add((v.D(10), v.D(20), v.D(42)));
                        EmitPoly(L, pts, (fl & 1) != 0, e.D(40), xf);
                        break;
                    }
                    case "LWPOLYLINE":
                    {
                        var pts = new List<(double X, double Y, double B)>();
                        double px = double.NaN;
                        foreach (var (c, v) in e.Codes)
                        {
                            double d = double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double dd) ? dd : 0;
                            if (c == 10) px = d;
                            else if (c == 20 && !double.IsNaN(px)) { pts.Add((px, d, 0)); px = double.NaN; }
                            else if (c == 42 && pts.Count > 0) pts[^1] = (pts[^1].X, pts[^1].Y, d);
                        }
                        EmitPoly(L, pts, (e.I(70) & 1) != 0, e.D(43), xf);
                        break;
                    }
                    case "CIRCLE":
                    {
                        var (cx, cy) = xf.Map(e.D(10), e.D(20));
                        w.Circle(L, cx, cy, e.D(40) * xf.Scale);
                        break;
                    }
                    case "ARC":
                    {
                        var (cx, cy) = xf.Map(e.D(10), e.D(20));
                        double a1 = e.D(50), a2 = e.D(51), rot = xf.Rot;
                        if (xf.Mirror) { double t = 180 - a2; a2 = 180 - a1; a1 = t; }
                        w.Arc(L, cx, cy, e.D(40) * xf.Scale, Norm(a1 + rot), Norm(a2 + rot));
                        break;
                    }
                    case "TEXT":
                    case "ATTRIB":
                    {
                        if ((e.I(70) & 1) != 0 && e.Type == "ATTRIB") break;   // thuộc tính ẩn
                        int ha = e.I(72), va = e.I(e.Type == "ATTRIB" ? 74 : 73);
                        double x = e.D(10), y = e.D(20);
                        bool al = (ha != 0 || va != 0) && e.S(11) != null;
                        if (al) { x = e.D(11); y = e.D(21); }
                        if (ha == 3 || ha == 5) { x = (e.D(10) + e.D(11)) / 2; y = (e.D(20) + e.D(21)) / 2; ha = 1; }
                        else if (ha == 4) { ha = 1; va = 2; }
                        ha = Math.Clamp(ha, 0, 2); va = Math.Clamp(va, 0, 3);
                        var (tx, ty) = xf.Map(x, y);
                        string s = CleanText(e.S(1));
                        if (s.Length > 0) w.Text(L, tx, ty, Math.Max(1e-6, e.D(40) * xf.Scale), s, Norm(e.D(50) + xf.Rot), ha, va);
                        break;
                    }
                    case "MTEXT":
                    {
                        var sb = new StringBuilder();
                        foreach (var (c, v) in e.Codes) if (c == 3) sb.Append(v);
                        sb.Append(e.S(1) ?? "");
                        string s = CleanMText(sb.ToString());
                        double rot = e.D(50);
                        if (e.S(11) != null) rot = Math.Atan2(e.D(21), e.D(11)) * 180 / Math.PI;
                        int att = Math.Clamp(e.I(71, 1), 1, 9);
                        int ha = (att - 1) % 3, va = ((att - 1) / 3) switch { 0 => 3, 1 => 2, _ => 1 };
                        var (tx, ty) = xf.Map(e.D(10), e.D(20));
                        if (s.Length > 0) w.Text(L, tx, ty, Math.Max(1e-6, e.D(40) * xf.Scale), s, Norm(rot + xf.Rot), ha, va);
                        break;
                    }
                    case "SOLID":
                    case "TRACE":
                    {
                        var (x1, y1) = xf.Map(e.D(10), e.D(20)); var (x2, y2) = xf.Map(e.D(11), e.D(21));
                        var (x3, y3) = xf.Map(e.D(12), e.D(22));
                        var (x4, y4) = e.S(13) != null ? xf.Map(e.D(13), e.D(23)) : (x3, y3);
                        w.Solid(L, x1, y1, x2, y2, x3, y3, x4, y4);
                        break;
                    }
                    case "INSERT":
                    case "DIMENSION":
                    {
                        string bn = e.S(2) ?? "";
                        if (depth < 8 && blocks.TryGetValue(bn, out var blk))
                        {
                            Xf local;
                            if (e.Type == "DIMENSION") local = new Xf(1, 0, 0, 1, -blk.Bx, -blk.By);   // block kích thước vẽ theo WCS
                            else
                            {
                                double sx = e.D(41, 1), sy = e.D(42, 1), r = e.D(50) * Math.PI / 180;
                                if (Math.Abs(sx) < 1e-12) sx = 1;
                                if (Math.Abs(sy) < 1e-12) sy = 1;
                                double c = Math.Cos(r), sn = Math.Sin(r);
                                var (ix, iy) = (e.D(10), e.D(20));
                                local = new Xf(c * sx, -sn * sy, sn * sx, c * sy, ix, iy).Then(new Xf(1, 0, 0, 1, -blk.Bx, -blk.By));
                            }
                            Emit(blk.Ents, xf.Then(local), layer, depth + 1);
                        }
                        else if (bn.Length > 0) Skip(e.Type + " " + bn);
                        if (e.Sub != null) Emit(e.Sub.Where(a => a.Type == "ATTRIB").ToList(), xf, parentLayer, depth);
                        break;
                    }
                    case "VIEWPORT":
                    case "POINT":
                    case "ATTDEF":
                    case "SEQEND":
                        break;
                    default:
                        Skip(e.Type);
                        break;
                }
                count += w._ents.Count - start;
                if (w._ents.Count > start && ((aci > 0 && aci < 256) || lt != null)) w.StyleFrom(start, aci > 0 && aci < 256 ? aci : 0, lt);
            }
        }

        void EmitPoly(string L, List<(double X, double Y, double B)> pts, bool closed, double width, Xf xf)
        {
            if (pts.Count < 2) return;
            var outPts = new List<(double X, double Y)>();
            int n = pts.Count, segs = closed ? n : n - 1;
            outPts.Add(xf.Map(pts[0].X, pts[0].Y));
            for (int i = 0; i < segs; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % n];
                if (Math.Abs(a.B) > 1e-9)
                {
                    foreach (var q in BulgePoints(a.X, a.Y, b.X, b.Y, a.B)) outPts.Add(xf.Map(q.X, q.Y));
                }
                if (!(closed && i == segs - 1)) outPts.Add(xf.Map(b.X, b.Y));
            }
            w.Polyline(L, outPts, closed, width > 0 ? width * xf.Scale : 0);
        }

        void Skip(string what) => skipped[what] = skipped.TryGetValue(what, out int k) ? k + 1 : 1;

        Emit(model, Xf.Identity, null, 0);
        foreach (var kv in skipped.OrderByDescending(x => x.Value))
            warnings.Add($"Bỏ qua {kv.Value} thực thể {kv.Key} (chưa hỗ trợ khi nạp lại bản vẽ).");
        return w;
    }

    private static double Norm(double a)
    {
        a %= 360;
        if (a < 0) a += 360;
        return Math.Abs(a) < 1e-9 ? 0 : a;
    }

    /// <summary>Các điểm trung gian trên cung lồi (bulge) từ (x1,y1) tới (x2,y2), không gồm hai đầu.</summary>
    private static IEnumerable<(double X, double Y)> BulgePoints(double x1, double y1, double x2, double y2, double bulge)
    {
        double dx = x2 - x1, dy = y2 - y1, c = Math.Sqrt(dx * dx + dy * dy);
        if (c < 1e-12) yield break;
        double ang = 4 * Math.Atan(bulge);
        double r = c / (2 * Math.Sin(ang / 2));
        double h = r * Math.Cos(ang / 2);
        double cx = (x1 + x2) / 2 - dy / c * h, cy = (y1 + y2) / 2 + dx / c * h;
        double a1 = Math.Atan2(y1 - cy, x1 - cx), rr = Math.Abs(r);
        int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(ang) / (Math.PI / 18)));
        for (int i = 1; i < n; i++)
        {
            double a = a1 + ang * i / n;
            yield return (cx + rr * Math.Cos(a), cy + rr * Math.Sin(a));
        }
    }

    private static string CleanText(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("%%c", "Ø").Replace("%%C", "Ø").Replace("%%d", "°").Replace("%%D", "°").Replace("%%p", "±").Replace("%%P", "±")
             .Replace("%%u", "").Replace("%%U", "").Replace("%%o", "").Replace("%%O", "");
        return Decode(s).Trim();
    }

    private static readonly Regex MtFmt = new(@"\\[ACFHQTWfcpq][^;]*;|\\[LlOoKkNn]|[{}]", RegexOptions.Compiled);

    private static string CleanMText(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\\P", " ").Replace("\\~", " ").Replace("\\\\", "\u0001");
        s = MtFmt.Replace(s, "");
        s = Regex.Replace(s, @"\\S([^;^/#]*)[\^/#]([^;]*);", "$1/$2");
        return CleanText(s.Replace("\u0001", "\\"));
    }

    /// <summary>Một thực thể (mã 0 tại p) và các cặp mã đến trước mã 0 kế tiếp.</summary>
    private static RawDxf ReadOne(List<(int Code, string Val)> pairs, ref int p)
    {
        var e = new RawDxf { Type = pairs[p].Val.Trim() };
        p++;
        while (p < pairs.Count && pairs[p].Code != 0) { e.Codes.Add(pairs[p]); p++; }
        return e;
    }

    /// <summary>Đọc danh sách thực thể tới mã 0 = end (ENDSEC / ENDBLK); POLYLINE gom VERTEX, INSERT gom ATTRIB tới SEQEND.</summary>
    private static List<RawDxf> ReadEntityList(List<(int Code, string Val)> pairs, ref int p, string end)
    {
        var list = new List<RawDxf>();
        while (p < pairs.Count)
        {
            if (pairs[p].Code != 0) { p++; continue; }
            string t = pairs[p].Val.Trim();
            if (t == end || t == "ENDSEC") break;
            var e = ReadOne(pairs, ref p);
            if (e.Type == "POLYLINE" || (e.Type == "INSERT" && e.I(66) == 1))
            {
                e.Sub = new List<RawDxf>();
                while (p < pairs.Count && pairs[p].Code == 0)
                {
                    string st = pairs[p].Val.Trim();
                    if (st == "SEQEND") { ReadOne(pairs, ref p); break; }
                    if (st != "VERTEX" && st != "ATTRIB") break;
                    e.Sub.Add(ReadOne(pairs, ref p));
                }
            }
            list.Add(e);
        }
        return list;
    }

    /// <summary>Giải mã byte file DXF: UTF-8 (có/không BOM) nếu hợp lệ, ngược lại Windows-1252/Latin-1.</summary>
    public static string DecodeDxfBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return "";
        try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(bytes); }
    }
}
