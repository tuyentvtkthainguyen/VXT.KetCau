using System.Globalization;
using System.Text;

namespace KetCauVXT.Import;

public enum DxfEntityType { Line, Polyline, Circle, Text }

/// <summary>Thực thể DXF đã đơn giản hoá (toạ độ đã quy đổi về m).</summary>
public class DxfEntity
{
    public DxfEntityType Type { get; set; }
    public string Layer { get; set; } = "0";
    /// <summary>Line: 2 điểm; Polyline: các đỉnh; Circle/Text: 1 điểm (tâm / điểm chèn)</summary>
    public List<(double X, double Y)> Points { get; set; } = new();
    public bool Closed { get; set; }
    public double Radius { get; set; }
    public string Text { get; set; } = "";
    public double TextHeight { get; set; }

    public override string ToString() => $"{Type} [{Layer}] {Points.Count} điểm";
}

/// <summary>Bộ đọc DXF dạng ASCII (R12 … 2018). Chỉ đọc mục ENTITIES với LINE, LWPOLYLINE, POLYLINE/VERTEX/SEQEND,
/// CIRCLE, TEXT, MTEXT. Bỏ qua block INSERT. Đơn vị bản vẽ lấy theo $INSUNITS (4 = mm, 5 = cm, 6 = m), mặc định mm.</summary>
public class DxfReader
{
    public List<DxfEntity> Entities { get; } = new();
    /// <summary>Giá trị $INSUNITS (0 nếu không khai báo)</summary>
    public int InsUnits { get; private set; }
    /// <summary>Hệ số nhân để đổi đơn vị bản vẽ → m</summary>
    public double Scale { get; private set; } = 0.001;
    public List<string> Warnings { get; } = new();

    private readonly List<(int code, string value)> _pairs = new();
    private int _pos;

    /// <summary>Đọc file. scaleOverride &gt; 0: dùng hệ số này (đơn vị bản vẽ → m) thay cho $INSUNITS.</summary>
    public static DxfReader Read(string path, double scaleOverride = 0)
    {
        var r = new DxfReader();
        string[] lines;
        try { lines = File.ReadAllLines(path, Encoding.UTF8); }
        catch (DecoderFallbackException) { lines = File.ReadAllLines(path, Encoding.Latin1); }
        r.Parse(lines, scaleOverride);
        return r;
    }

    public static DxfReader FromLines(string[] lines, double scaleOverride = 0)
    {
        var r = new DxfReader();
        r.Parse(lines, scaleOverride);
        return r;
    }

    private void Parse(string[] lines, double scaleOverride)
    {
        for (int i = 0; i + 1 < lines.Length; i += 2)
        {
            if (!int.TryParse(lines[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
            {
                // dòng lệch cặp – thử dịch 1 dòng
                i -= 1;
                continue;
            }
            _pairs.Add((code, lines[i + 1].Trim()));
        }

        // HEADER: $INSUNITS
        for (int i = 0; i + 1 < _pairs.Count; i++)
            if (_pairs[i].code == 9 && _pairs[i].value == "$INSUNITS" && _pairs[i + 1].code == 70)
            {
                InsUnits = ToInt(_pairs[i + 1].value);
                break;
            }
        Scale = InsUnits switch { 1 => 0.0254, 2 => 0.3048, 4 => 0.001, 5 => 0.01, 6 => 1.0, _ => 0.001 };
        if (scaleOverride > 0) Scale = scaleOverride;

        // tìm SECTION ENTITIES
        _pos = 0;
        bool found = false;
        while (_pos + 1 < _pairs.Count)
        {
            if (_pairs[_pos].code == 0 && _pairs[_pos].value == "SECTION" && _pairs[_pos + 1].code == 2 && _pairs[_pos + 1].value == "ENTITIES")
            {
                _pos += 2; found = true; break;
            }
            _pos++;
        }
        if (!found) { Warnings.Add("File DXF không có mục ENTITIES"); return; }

        while (_pos < _pairs.Count)
        {
            var (code, value) = _pairs[_pos];
            if (code != 0) { _pos++; continue; }
            if (value == "ENDSEC" || value == "EOF") break;
            switch (value)
            {
                case "LINE": ReadLine(); break;
                case "LWPOLYLINE": ReadLwPolyline(); break;
                case "POLYLINE": ReadPolyline(); break;
                case "CIRCLE": ReadCircle(); break;
                case "TEXT": ReadText(false); break;
                case "MTEXT": ReadText(true); break;
                default: _pos++; SkipToNextEntity(); break;
            }
        }
    }

    private void SkipToNextEntity()
    {
        while (_pos < _pairs.Count && _pairs[_pos].code != 0) _pos++;
    }

    /// <summary>Đọc các cặp mã của một thực thể (từ sau mã 0 tới trước mã 0 kế tiếp).</summary>
    private List<(int code, string value)> Body()
    {
        _pos++;
        var b = new List<(int, string)>();
        while (_pos < _pairs.Count && _pairs[_pos].code != 0) { b.Add(_pairs[_pos]); _pos++; }
        return b;
    }

    private static double ToD(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;

    private static int ToInt(string s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private static string Layer(List<(int code, string value)> b) => b.Where(x => x.code == 8).Select(x => x.value).FirstOrDefault() ?? "0";

    private static double Get(List<(int code, string value)> b, int code) =>
        b.Where(x => x.code == code).Select(x => ToD(x.value)).FirstOrDefault();

    private void ReadLine()
    {
        var b = Body();
        var e = new DxfEntity { Type = DxfEntityType.Line, Layer = Layer(b) };
        e.Points.Add((Get(b, 10) * Scale, Get(b, 20) * Scale));
        e.Points.Add((Get(b, 11) * Scale, Get(b, 21) * Scale));
        Entities.Add(e);
    }

    private void ReadLwPolyline()
    {
        var b = Body();
        var e = new DxfEntity { Type = DxfEntityType.Polyline, Layer = Layer(b) };
        e.Closed = (ToInt(b.Where(x => x.code == 70).Select(x => x.value).FirstOrDefault() ?? "0") & 1) == 1;
        double? vx = null;
        foreach (var (code, value) in b)
        {
            if (code == 10) vx = ToD(value);
            else if (code == 20 && vx.HasValue) { e.Points.Add((vx.Value * Scale, ToD(value) * Scale)); vx = null; }
        }
        CloseIfRepeated(e);
        if (e.Points.Count >= 2) Entities.Add(e);
    }

    private void ReadPolyline()
    {
        var b = Body();
        var e = new DxfEntity { Type = DxfEntityType.Polyline, Layer = Layer(b) };
        int flag = ToInt(b.Where(x => x.code == 70).Select(x => x.value).FirstOrDefault() ?? "0");
        e.Closed = (flag & 1) == 1;
        bool mesh = (flag & (16 | 64)) != 0;
        while (_pos < _pairs.Count && _pairs[_pos].code == 0 && _pairs[_pos].value == "VERTEX")
        {
            var v = Body();
            int vf = ToInt(v.Where(x => x.code == 70).Select(x => x.value).FirstOrDefault() ?? "0");
            if ((vf & 128) != 0 && (vf & 64) == 0) continue; // đỉnh mặt lưới – bỏ
            e.Points.Add((Get(v, 10) * Scale, Get(v, 20) * Scale));
        }
        if (_pos < _pairs.Count && _pairs[_pos].code == 0 && _pairs[_pos].value == "SEQEND") Body();
        if (mesh) return;
        CloseIfRepeated(e);
        if (e.Points.Count >= 2) Entities.Add(e);
    }

    private static void CloseIfRepeated(DxfEntity e)
    {
        if (e.Points.Count > 2)
        {
            var a = e.Points[0]; var z = e.Points[^1];
            if (Math.Abs(a.X - z.X) < 1e-6 && Math.Abs(a.Y - z.Y) < 1e-6)
            {
                e.Points.RemoveAt(e.Points.Count - 1);
                e.Closed = true;
            }
        }
    }

    private void ReadCircle()
    {
        var b = Body();
        var e = new DxfEntity { Type = DxfEntityType.Circle, Layer = Layer(b), Radius = Get(b, 40) * Scale };
        e.Points.Add((Get(b, 10) * Scale, Get(b, 20) * Scale));
        Entities.Add(e);
    }

    private void ReadText(bool mtext)
    {
        var b = Body();
        var e = new DxfEntity { Type = DxfEntityType.Text, Layer = Layer(b), TextHeight = Get(b, 40) * Scale };
        // TEXT: nếu có căn lề (72/73 ≠ 0) thì điểm căn là 11/21
        bool aligned = !mtext && b.Any(x => (x.code == 72 || x.code == 73) && ToInt(x.value) != 0) && b.Any(x => x.code == 11);
        double px = aligned ? Get(b, 11) : Get(b, 10);
        double py = aligned ? Get(b, 21) : Get(b, 20);
        e.Points.Add((px * Scale, py * Scale));
        var sb = new StringBuilder();
        foreach (var (code, value) in b) if (code == 3) sb.Append(value);
        foreach (var (code, value) in b) if (code == 1) sb.Append(value);
        e.Text = CleanMText(sb.ToString());
        if (e.Text.Length > 0) Entities.Add(e);
    }

    /// <summary>Bỏ mã định dạng MTEXT (\P, \f...;, {}, %%c...).</summary>
    public static string CleanMText(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '{' || c == '}') continue;
            if (c == '\\' && i + 1 < s.Length)
            {
                char k = s[i + 1];
                if (k == 'P' || k == 'p') { sb.Append(' '); i++; continue; }
                if (k == '\\' || k == '{' || k == '}') { sb.Append(k); i++; continue; }
                if ("fFHhCcTtQqWwAa".IndexOf(k) >= 0)
                {
                    int semi = s.IndexOf(';', i);
                    if (semi > 0) { i = semi; continue; }
                }
                if (k == 'L' || k == 'l' || k == 'O' || k == 'o' || k == 'K' || k == 'k') { i++; continue; }
            }
            sb.Append(c);
        }
        return sb.ToString().Replace("%%c", "Ø").Replace("%%C", "Ø").Replace("%%d", "°").Replace("%%p", "±").Trim();
    }
}
