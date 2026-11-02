using System.Globalization;
using System.Text;

namespace KetCauVXT.Drawing;

/// <summary>Loại nét vẽ cơ bản đọc ra từ một mảnh hình DXF (để vẽ lại trên màn hình / máy in).</summary>
public enum PrimKind { Line, Polyline, Circle, Arc, Solid, Text }

/// <summary>Một nét vẽ cơ bản, toạ độ theo đơn vị của mảnh hình (mm mô hình).
/// Line: Pts = x1,y1,x2,y2. Polyline: Pts = x0,y0,x1,y1..., Closed, Width.
/// Circle/Arc: Pts = cx,cy; R; A1→A2 (độ, ngược chiều kim đồng hồ). Solid: Pts = 4 đỉnh theo thứ tự chu vi.
/// Text: Pts = x,y (điểm chèn); H (chiều cao chữ), Rot (độ), HA (0 trái,1 giữa,2 phải), VA (0 chân,1 đáy,2 giữa,3 đỉnh).</summary>
public sealed class DrawPrim
{
    public PrimKind Kind;
    public string Layer = "0";
    public string LType;
    public double[] Pts = Array.Empty<double>();
    public bool Closed;
    public double Width;
    public double R, A1, A2;
    public string Text;
    public double H, Rot;
    public int HA, VA;
    /// <summary>Hệ số rộng chữ (TEXT mã 41; 1 = chuẩn).</summary>
    public double WF = 1;
}

public sealed partial class DxfWriter
{
    /// <summary>Giải mã \U+XXXX (do <see cref="Encode"/> tạo) về chuỗi Unicode.</summary>
    public static string Decode(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf("\\U+", StringComparison.Ordinal) < 0) return s ?? "";
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 7 <= s.Length && s[i + 1] == 'U' && s[i + 2] == '+'
                && int.TryParse(s.AsSpan(i + 3, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
            {
                sb.Append((char)code);
                i += 6;
            }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }

    /// <summary>Danh sách nét vẽ cơ bản của mảnh hình (đọc lại từ các mã nhóm DXF đã ghi).</summary>
    public List<DrawPrim> GetPrimitives()
    {
        var res = new List<DrawPrim>(_ents.Count);
        foreach (var e in _ents)
        {
            var pr = new DrawPrim();
            double x0 = 0, y0 = 0, x1 = 0, y1 = 0, x2 = 0, y2 = 0, x3 = 0, y3 = 0;
            bool layerSet = false, inVertex = false;
            var vx = new List<double>();
            double vxPending = double.NaN;
            foreach (var (code, val) in e.Codes)
            {
                if (code == 0) { if (val as string == "VERTEX") inVertex = true; continue; }
                if (code == 8) { if (!layerSet) { pr.Layer = val as string ?? "0"; layerSet = true; } continue; }
                if (code == 6) { pr.LType = val as string; continue; }
                if (code == 1) { pr.Text = Decode(val as string); continue; }
                double d = val is double dd ? dd : val is int ii ? ii : 0;
                if (e.Type == "POLYLINE")
                {
                    if (!inVertex)
                    {
                        if (code == 70) pr.Closed = (((int)d) & 1) != 0;
                        else if (code == 40) pr.Width = d;
                    }
                    else if (code == 10) vxPending = d;
                    else if (code == 20 && !double.IsNaN(vxPending)) { vx.Add(vxPending); vx.Add(d); vxPending = double.NaN; }
                    continue;
                }
                switch (code)
                {
                    case 10: x0 = d; break;
                    case 20: y0 = d; break;
                    case 11: x1 = d; break;
                    case 21: y1 = d; break;
                    case 12: x2 = d; break;
                    case 22: y2 = d; break;
                    case 13: x3 = d; break;
                    case 23: y3 = d; break;
                    case 40: pr.R = d; pr.H = d; break;
                    case 50: pr.A1 = d; pr.Rot = d; break;
                    case 51: pr.A2 = d; break;
                    case 41: if (e.Type == "TEXT" && d > 0) pr.WF = d; break;
                    case 72: pr.HA = (int)d; break;
                    case 73: pr.VA = (int)d; break;
                }
            }
            switch (e.Type)
            {
                case "LINE": pr.Kind = PrimKind.Line; pr.Pts = new[] { x0, y0, x1, y1 }; break;
                case "POLYLINE":
                    if (vx.Count < 4) continue;
                    pr.Kind = PrimKind.Polyline; pr.Pts = vx.ToArray(); break;
                case "CIRCLE": pr.Kind = PrimKind.Circle; pr.Pts = new[] { x0, y0 }; pr.H = 0; pr.Rot = 0; break;
                case "ARC": pr.Kind = PrimKind.Arc; pr.Pts = new[] { x0, y0 }; pr.H = 0; pr.Rot = 0; break;
                case "SOLID": pr.Kind = PrimKind.Solid; pr.Pts = new[] { x0, y0, x1, y1, x3, y3, x2, y2 }; break;
                case "TEXT":
                    pr.Kind = PrimKind.Text; pr.Pts = new[] { x0, y0 }; pr.R = 0; pr.A1 = 0;
                    if (string.IsNullOrEmpty(pr.Text)) continue;
                    break;
                default: continue;
            }
            res.Add(pr);
        }
        return res;
    }

    /// <summary>Ghép mảnh hình khác vào sau khi phóng k lần rồi tịnh tiến (dx, dy): P' = k·P + (dx, dy).
    /// Chiều cao chữ, bán kính, bề rộng nét đa tuyến cũng được nhân k.</summary>
    public void AppendScaled(DxfWriter other, double k, double dx, double dy)
    {
        if (other == null) return;
        if (Math.Abs(k - 1) < 1e-12) { Append(other, dx, dy); return; }
        MergeDimStyles(other);
        foreach (var e in other._ents)
        {
            var b = e.Box;
            var n = new DxfEnt { Type = e.Type, Box = b.IsEmpty ? b : new Rect2(b.X1 * k + dx, b.Y1 * k + dy, b.X2 * k + dx, b.Y2 * k + dy) };
            foreach (var (code, val) in e.Codes)
            {
                object v = val;
                if (val is double d)
                {
                    if (code >= 10 && code <= 18) v = d * k + dx;
                    else if (code >= 20 && code <= 28) v = d * k + dy;
                    else if (code == 40) v = d * k;
                    else if (code == 41 && e.Type == "POLYLINE") v = d * k;
                }
                n.Codes.Add((code, v));
            }
            _ents.Add(n);
            _ext = _ext.Union(n.Box);
        }
    }
}
