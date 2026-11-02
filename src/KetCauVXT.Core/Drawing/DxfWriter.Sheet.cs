namespace KetCauVXT.Drawing;

/// <summary>Bổ sung cho dàn trang thủ công: gán màu/kiểu nét/lớp cho nhóm thực thể vừa vẽ,
/// ghép mảnh hình có cắt theo khung (vùng crop của khung nhìn).</summary>
public sealed partial class DxfWriter
{
    /// <summary>Gán kiểu nét (mã 6), màu ACI (mã 62, 0 = theo lớp) và đổi lớp (mã 8, null = giữ) cho các thực thể từ chỉ số start.</summary>
    public void StyleFrom(int start, int aci, string ltype, string layer = null)
    {
        for (int i = Math.Max(0, start); i < _ents.Count; i++)
        {
            var c = _ents[i].Codes;
            if (!string.IsNullOrEmpty(layer))
                for (int j = 0; j < c.Count; j++)
                    if (c[j].Code == 8) c[j] = (8, layer);
            int at = c.Count > 0 && c[0].Code == 8 ? 1 : 0;
            if (!string.IsNullOrEmpty(ltype) && !c.Any(x => x.Code == 6)) { c.Insert(at, (6, ltype)); at++; }
            if (aci > 0 && aci < 256 && !c.Any(x => x.Code == 62)) c.Insert(at, (62, aci));
        }
    }

    /// <summary>Đếm thực thể theo loại và lớp (dùng cho kiểm tra).</summary>
    public int CountEntities(string type, string layer = null) =>
        _ents.Count(e => e.Type == type && (layer == null || e.Codes.Any(c => c.Code == 8 && (c.Val as string) == layer)));

    private static bool InsideR(Rect2 r, double x, double y, double eps = 1e-6) =>
        x >= r.X1 - eps && x <= r.X2 + eps && y >= r.Y1 - eps && y <= r.Y2 + eps;

    private static bool ContainsR(Rect2 outer, Rect2 b, double eps = 1e-6) =>
        !b.IsEmpty && b.X1 >= outer.X1 - eps && b.X2 <= outer.X2 + eps && b.Y1 >= outer.Y1 - eps && b.Y2 <= outer.Y2 + eps;

    private static bool OverlapR(Rect2 a, Rect2 b) =>
        !a.IsEmpty && !b.IsEmpty && a.X1 <= b.X2 && b.X1 <= a.X2 && a.Y1 <= b.Y2 && b.Y1 <= a.Y2;

    /// <summary>Cắt đoạn thẳng theo hình chữ nhật (Liang–Barsky). Trả về false nếu nằm ngoài.</summary>
    public static bool ClipSegment(Rect2 r, ref double x1, ref double y1, ref double x2, ref double y2)
    {
        double dx = x2 - x1, dy = y2 - y1, t0 = 0, t1 = 1;
        double[] p = { -dx, dx, -dy, dy };
        double[] q = { x1 - r.X1, r.X2 - x1, y1 - r.Y1, r.Y2 - y1 };
        for (int i = 0; i < 4; i++)
        {
            if (Math.Abs(p[i]) < 1e-12) { if (q[i] < -1e-9) return false; continue; }
            double t = q[i] / p[i];
            if (p[i] < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
            else { if (t < t0) return false; if (t < t1) t1 = t; }
        }
        double nx1 = x1 + t0 * dx, ny1 = y1 + t0 * dy, nx2 = x1 + t1 * dx, ny2 = y1 + t1 * dy;
        x1 = nx1; y1 = ny1; x2 = nx2; y2 = ny2;
        return true;
    }

    /// <summary>Như <see cref="AppendScaled"/> nhưng chỉ giữ phần nằm trong clip (mm sau biến đổi):
    /// đoạn thẳng/đa tuyến/đường tròn/cung bị cắt, chữ và miền tô giữ lại nếu điểm chèn/trọng tâm nằm trong.</summary>
    public void AppendScaledClipped(DxfWriter other, double k, double dx, double dy, Rect2 clip)
    {
        if (other == null) return;
        var tmp = new DxfWriter(Scale);
        tmp.AppendScaled(other, k, dx, dy);
        MergeDimStyles(tmp);
        foreach (var e in tmp._ents)
        {
            if (ContainsR(clip, e.Box)) { _ents.Add(e); _ext = _ext.Union(e.Box); continue; }
            if (!OverlapR(clip, e.Box) && !e.Box.IsEmpty) continue;
            string layer = "0", lt = null;
            int aci = 0;
            foreach (var (code, val) in e.Codes)
            {
                if (code == 0) break;
                if (code == 8 && layer == "0") layer = val as string ?? "0";
                else if (code == 6) lt = val as string;
                else if (code == 62 && val is int ci) aci = ci;
            }
            double D(int code)
            {
                foreach (var (c, v) in e.Codes)
                {
                    if (c == 0) break;
                    if (c == code) return v is double d ? d : v is int i ? i : 0;
                }
                return 0;
            }
            int start = _ents.Count;
            switch (e.Type)
            {
                case "LINE":
                {
                    double x1 = D(10), y1 = D(20), x2 = D(11), y2 = D(21);
                    if (ClipSegment(clip, ref x1, ref y1, ref x2, ref y2)) Line(layer, x1, y1, x2, y2);
                    break;
                }
                case "POLYLINE":
                {
                    var pts = new List<(double X, double Y)>();
                    bool inV = false, closed = false;
                    double width = 0, px = double.NaN;
                    foreach (var (c, v) in e.Codes)
                    {
                        double d = v is double dd ? dd : v is int ii ? ii : 0;
                        if (c == 0) { inV = (v as string) == "VERTEX"; continue; }
                        if (!inV) { if (c == 70) closed = (((int)d) & 1) != 0; else if (c == 40) width = d; continue; }
                        if (c == 10) px = d;
                        else if (c == 20 && !double.IsNaN(px)) { pts.Add((px, d)); px = double.NaN; }
                    }
                    if (closed && pts.Count > 2) pts.Add(pts[0]);
                    ClipPath(layer, pts, width, clip);
                    break;
                }
                case "CIRCLE":
                case "ARC":
                {
                    double cx = D(10), cy = D(20), r = D(40);
                    double a1 = e.Type == "ARC" ? D(50) : 0, a2 = e.Type == "ARC" ? D(51) : 360;
                    double sweep = e.Type == "ARC" ? ((a2 - a1) % 360 + 360) % 360 : 360;
                    if (sweep < 1e-9) sweep = 360;
                    int n = Math.Max(8, (int)Math.Ceiling(sweep / 5));
                    var pts = new List<(double X, double Y)>();
                    for (int i = 0; i <= n; i++)
                    {
                        double a = (a1 + sweep * i / n) * Math.PI / 180;
                        pts.Add((cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
                    }
                    ClipPath(layer, pts, 0, clip);
                    break;
                }
                case "TEXT":
                    if (InsideR(clip, D(10), D(20))) { _ents.Add(e); _ext = _ext.Union(e.Box); start = _ents.Count; }
                    break;
                case "SOLID":
                {
                    double gx = (D(10) + D(11) + D(12) + D(13)) / 4, gy = (D(20) + D(21) + D(22) + D(23)) / 4;
                    if (InsideR(clip, gx, gy)) { _ents.Add(e); _ext = _ext.Union(e.Box); start = _ents.Count; }
                    break;
                }
                default:
                    if (!e.Box.IsEmpty && InsideR(clip, e.Box.Cx, e.Box.Cy)) { _ents.Add(e); _ext = _ext.Union(e.Box); start = _ents.Count; }
                    break;
            }
            for (int ci = Math.Max(0, start); ci < _ents.Count; ci++) _ents[ci].CopyMetaFrom(e);   // đoạn bị cắt giữ khoá/nhóm của thực thể gốc
            StyleFrom(start, aci, lt);
        }
    }

    /// <summary>Cắt đường gấp khúc theo clip, ghi các đoạn còn lại thành đa tuyến hở.</summary>
    private void ClipPath(string layer, List<(double X, double Y)> pts, double width, Rect2 clip)
    {
        var cur = new List<(double X, double Y)>();
        void Flush()
        {
            if (cur.Count >= 2) Polyline(layer, cur, false, width);
            cur = new List<(double X, double Y)>();
        }
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            double x1 = pts[i].X, y1 = pts[i].Y, x2 = pts[i + 1].X, y2 = pts[i + 1].Y;
            if (!ClipSegment(clip, ref x1, ref y1, ref x2, ref y2)) { Flush(); continue; }
            if (cur.Count > 0)
            {
                var last = cur[^1];
                if (Math.Abs(last.X - x1) > 1e-6 || Math.Abs(last.Y - y1) > 1e-6) { Flush(); cur.Add((x1, y1)); }
            }
            else cur.Add((x1, y1));
            cur.Add((x2, y2));
            bool cutEnd = Math.Abs(x2 - pts[i + 1].X) > 1e-6 || Math.Abs(y2 - pts[i + 1].Y) > 1e-6;
            if (cutEnd) Flush();
        }
        Flush();
    }
}
