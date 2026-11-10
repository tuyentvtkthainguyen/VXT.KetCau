using System.Globalization;
using System.Text;
using KetCauVXT.Design;

namespace KetCauVXT.Drawing;

/// <summary>Tên layer chuẩn bản vẽ kết cấu (theo skill đánh số/tag thép).</summary>
public static class Lay
{
    public const string BeTong = "BE_TONG";
    public const string Thep = "THEP";
    public const string Dai = "DAI";
    public const string KichThuoc = "KICH_THUOC";
    public const string Chu = "CHU";
    public const string KyHieu = "KY_HIEU";
    public const string Truc = "TRUC";
    public const string KhungTen = "KHUNG_TEN";
    /// <summary>[Chi tiết rời] khung bao từng chi tiết khi xuất không lồng khung tên (người dùng tự dàn trang trong AutoCAD)</summary>
    public const string KhungChiTiet = "KHUNG_CHI_TIET";
    /// <summary>Tô vật liệu mặt cắt (HATCH) – không nằm trong bảng layer mặc định; ghi khi có dùng (màu 8, đổi tên/màu bằng layer thêm khoá "HATCH").</summary>
    public const string Hatch = "HATCH";
}

/// <summary>Hình chữ nhật bao (trục song song), dùng cho phạm vi bản vẽ và tránh chồng nhãn.</summary>
public struct Rect2
{
    public double X1, Y1, X2, Y2;

    public Rect2(double x1, double y1, double x2, double y2)
    {
        X1 = Math.Min(x1, x2); X2 = Math.Max(x1, x2);
        Y1 = Math.Min(y1, y2); Y2 = Math.Max(y1, y2);
    }

    public static Rect2 Empty => new Rect2 { X1 = double.MaxValue, Y1 = double.MaxValue, X2 = double.MinValue, Y2 = double.MinValue };
    public readonly bool IsEmpty => X1 > X2 || Y1 > Y2;
    public readonly double W => IsEmpty ? 0 : X2 - X1;
    public readonly double H => IsEmpty ? 0 : Y2 - Y1;
    public readonly double Cx => (X1 + X2) / 2;
    public readonly double Cy => (Y1 + Y2) / 2;

    public readonly bool Intersects(Rect2 o) =>
        !IsEmpty && !o.IsEmpty && X1 < o.X2 && o.X1 < X2 && Y1 < o.Y2 && o.Y1 < Y2;

    public readonly Rect2 Offset(double dx, double dy) => IsEmpty ? this : new Rect2(X1 + dx, Y1 + dy, X2 + dx, Y2 + dy);
    public readonly Rect2 Inflate(double d) => IsEmpty ? this : new Rect2(X1 - d, Y1 - d, X2 + d, Y2 + d);

    public readonly Rect2 Union(Rect2 o)
    {
        if (IsEmpty) return o;
        if (o.IsEmpty) return this;
        return new Rect2(Math.Min(X1, o.X1), Math.Min(Y1, o.Y1), Math.Max(X2, o.X2), Math.Max(Y2, o.Y2));
    }

    public readonly Rect2 Union(double x, double y) => Union(new Rect2(x, y, x, y));
}

internal sealed class DxfEnt
{
    public string Type = "";
    public List<(int Code, object Val)> Codes = new();
    public Rect2 Box = Rect2.Empty;
    // ---- hiệu chỉnh bản vẽ sau khi xuất (DxfWriter.Keys.cs, DrawingEdits.cs) ----
    /// <summary>Khoá ổn định của thực thể trong hình chứa nó (gán khi EnsureKeys; không đổi khi dựng lại cùng hình học).</summary>
    public string Key;
    /// <summary>Nhóm thực thể (nhãn thép, tên hình, kích thước…) – chọn/dời cả nhóm.</summary>
    public string Group;
    /// <summary>Vai trò trong nhóm: "leader" (đường dẫn: dời nhóm chỉ dời điểm cuối), "anchor" (chấm tại thanh: giữ nguyên).</summary>
    public string Role;
    /// <summary>Khoá hình (Figure) gốc khi thực thể được ghép từ hình có OwnerKey; null = thực thể của chính bộ ghi.</summary>
    public string Owner;
    /// <summary>Hệ số phóng từ toạ độ hình gốc sang bộ ghi này.</summary>
    public double OwnerK = 1;

    public void CopyMetaFrom(DxfEnt src, string ownerKey = null, double k = 1)
    {
        if (src == null) return;
        Key = src.Key; Group = src.Group; Role = src.Role;
        if (src.Owner != null) { Owner = src.Owner; OwnerK = src.OwnerK * k; }
        else if (ownerKey != null) { Owner = ownerKey; OwnerK = k; }
        else { Owner = null; OwnerK = 1; }
    }
    // ---- hết ----
}

/// <summary>
/// Bộ ghi DXF ASCII tối giản (AC1009 / R12). Đơn vị mm. Bản vẽ ở tỷ lệ 1/Scale:
/// chữ in cao 2,5 mm ⇒ chiều cao chữ trong mô hình Th = 2,5 × Scale.
/// Có thể dùng làm "mảnh hình" rồi ghép vào bản vẽ khác bằng <see cref="Append"/>.
/// </summary>
public sealed partial class DxfWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly List<DxfEnt> _ents = new();
    private Rect2 _ext = Rect2.Empty;

    /// <summary>Mẫu số tỷ lệ của tờ bản vẽ (50 ⇒ 1/50).</summary>
    public double Scale { get; set; }
    /// <summary>Chiều cao chữ ghi chú trong mô hình (mặc định 2,5 mm khi in – Style.NoteHeight); cũng là đơn vị khoảng cách bố trí.</summary>
    public double Th => TextH(KetCauVXT.Model.TextRole.Note);
    /// <summary>Bán kính vòng tròn số hiệu (mặc định Ø5,5 mm khi in – Style.MarkDiameter).</summary>
    public double MarkR => Style.MarkDiameter / 2 * Scale;
    public Rect2 Extents => _ext;
    public int EntityCount => _ents.Count;

    public DxfWriter(double scale = 50) { Scale = MapScale(scale <= 0 ? 1 : scale); }

    // ---- vẽ lại hình ở tỷ lệ khác (dàn trang in – Detailing/SheetLayout.cs) ----
    [ThreadStatic] private static Func<double, double> _scaleMap;

    /// <summary>Mẫu số tỷ lệ sau khi áp phép đổi tỷ lệ đang có hiệu lực (không có → giữ nguyên).</summary>
    public static double MapScale(double scale)
    {
        if (_scaleMap == null || scale <= 1.0001) return scale;   // bảng (tỷ lệ 1/1) không đổi
        double s = _scaleMap(scale);
        return s > 0 && !double.IsNaN(s) ? s : scale;
    }

    /// <summary>Trong phạm vi using: mọi DxfWriter mới tạo (và chữ "tl: 1/…") đổi mẫu số tỷ lệ theo map ⇒ hàm vẽ có sẵn
    /// vẽ lại hình ở tỷ lệ mới với chữ, vòng số hiệu, đầu trục… đúng cỡ in (toạ độ hình học giữ mm thật).</summary>
    public static IDisposable OverrideScales(Func<double, double> map)
    {
        var prev = _scaleMap;
        _scaleMap = map;
        return new ScaleScope(prev);
    }

    private sealed class ScaleScope : IDisposable
    {
        private readonly Func<double, double> _prev;
        private bool _done;
        public ScaleScope(Func<double, double> prev) { _prev = prev; }
        public void Dispose() { if (_done) return; _done = true; _scaleMap = _prev; }
    }

    public static readonly (string Name, int Color, string LType)[] LayerDefs =
    {
        ("0", 7, "CONTINUOUS"),
        (Lay.BeTong, 7, "CONTINUOUS"),
        (Lay.Thep, 1, "CONTINUOUS"),
        (Lay.Dai, 3, "CONTINUOUS"),
        (Lay.KichThuoc, 4, "CONTINUOUS"),
        (Lay.Chu, 2, "CONTINUOUS"),
        (Lay.KyHieu, 6, "CONTINUOUS"),
        (Lay.Truc, 8, "CENTER"),
        (Lay.KhungTen, 7, "CONTINUOUS"),
        (Lay.KhungChiTiet, 5, "CONTINUOUS"),
    };

    // ------------------------------------------------------------------
    // Tiện ích chữ
    // ------------------------------------------------------------------
    /// <summary>Mã hoá ký tự &gt; 127 thành \U+XXXX (chữ tiếng Việt, Ø, ±, ≤...).</summary>
    public static string Encode(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length + 16);
        foreach (char c in s)
        {
            if (c == '\r' || c == '\n') { sb.Append(' '); continue; }
            if (c < 128) sb.Append(c);
            else sb.Append("\\U+").Append(((int)c).ToString("X4", Inv));
        }
        return sb.ToString();
    }

    /// <summary>Ước lượng bề rộng chuỗi (font Arial).</summary>
    public static double TextWidth(string text, double h) => string.IsNullOrEmpty(text) ? 0 : text.Length * 0.62 * h;

    /// <summary>Bề rộng chữ ước lượng THIÊN LỚN cho nhãn thép (Arial / Times): chữ số, chữ thường ≈ 0,56h; chữ hoa ≈ 0,67h;
    /// Ø, W, M, m, w ≈ 0,80h; dấu cách, dấu chấm ≈ 0,28h; nhân 1,08 dự phòng – để vòng số hiệu luôn cách chữ một khe hở.</summary>
    public static double TagTextWidth(string text, double h)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        double w = 0;
        foreach (char ch in text)
        {
            if (ch == ' ' || ch == '.' || ch == ',' || ch == ':' || ch == ';' || ch == '\'' || ch == '|' || ch == 'i' || ch == 'l' || ch == 'I') w += 0.30;
            else if (ch == 'Ø' || ch == 'ø' || ch == 'Φ' || ch == 'φ' || ch == 'W' || ch == 'M' || ch == 'm' || ch == 'w' || ch == '@' || ch == '%') w += 0.80;
            else if (char.IsUpper(ch)) w += 0.67;
            else w += 0.56;
        }
        return Math.Max(w * h * 1.08, TextWidth(text, h));
    }

    /// <summary>Khe hở tối thiểu giữa cuối chữ nhãn và mép vòng số hiệu (× chiều cao chữ).</summary>
    public const double TagGapFactor = 0.6;

    public static string N0(double v) => Math.Round(v).ToString("0", Inv);

    /// <summary>Cao độ dạng "+4.800", "-1.500", "±0.000" (z tính bằng m).</summary>
    public static string ElevText(double zM)
    {
        double r = Math.Round(zM, 3);
        if (Math.Abs(r) < 0.0005) return "±0.000";
        return (r > 0 ? "+" : "-") + Math.Abs(r).ToString("0.000", Inv);
    }

    // ------------------------------------------------------------------
    // Thực thể cơ bản
    // ------------------------------------------------------------------
    private DxfEnt Add(string type, string layer, Rect2 box, string ltype = null)
    {
        var e = new DxfEnt { Type = type, Box = box, Group = _group, Role = _role, Key = _nextKey };   // nhóm/khoá: DxfWriter.Keys.cs
        _nextKey = null;
        e.Codes.Add((8, layer));
        if (!string.IsNullOrEmpty(ltype)) e.Codes.Add((6, ltype));
        _ents.Add(e);
        _ext = _ext.Union(box);
        return e;
    }

    public void Line(string layer, double x1, double y1, double x2, double y2, string ltype = null)
    {
        var e = Add("LINE", layer, new Rect2(x1, y1, x2, y2), ltype);
        e.Codes.Add((10, x1)); e.Codes.Add((20, y1)); e.Codes.Add((30, 0.0));
        e.Codes.Add((11, x2)); e.Codes.Add((21, y2)); e.Codes.Add((31, 0.0));
    }

    /// <summary>POLYLINE 2D (R12) – VERTEX – SEQEND. width &gt; 0: nét đậm bề rộng không đổi.</summary>
    public void Polyline(string layer, IList<(double X, double Y)> pts, bool closed, double width = 0, string ltype = null)
    {
        if (pts == null || pts.Count < 2) return;
        var box = Rect2.Empty;
        foreach (var p in pts) box = box.Union(p.X, p.Y);
        var e = Add("POLYLINE", layer, box, ltype);
        e.Codes.Add((66, 1));
        e.Codes.Add((10, 0.0)); e.Codes.Add((20, 0.0)); e.Codes.Add((30, 0.0));
        e.Codes.Add((70, closed ? 1 : 0));
        if (width > 0) { e.Codes.Add((40, width)); e.Codes.Add((41, width)); }
        foreach (var p in pts)
        {
            e.Codes.Add((0, "VERTEX"));
            e.Codes.Add((8, layer));
            e.Codes.Add((10, p.X)); e.Codes.Add((20, p.Y)); e.Codes.Add((30, 0.0));
        }
        e.Codes.Add((0, "SEQEND"));
        e.Codes.Add((8, layer));
    }

    public void Rect(string layer, double x1, double y1, double x2, double y2, string ltype = null)
    {
        Polyline(layer, new List<(double X, double Y)> { (x1, y1), (x2, y1), (x2, y2), (x1, y2) }, true, 0, ltype);
    }

    public void Circle(string layer, double cx, double cy, double r)
    {
        var e = Add("CIRCLE", layer, new Rect2(cx - r, cy - r, cx + r, cy + r));
        e.Codes.Add((10, cx)); e.Codes.Add((20, cy)); e.Codes.Add((30, 0.0)); e.Codes.Add((40, r));
    }

    /// <summary>Cung tròn, góc độ ngược chiều kim đồng hồ từ a1 tới a2.</summary>
    public void Arc(string layer, double cx, double cy, double r, double a1, double a2)
    {
        var e = Add("ARC", layer, new Rect2(cx - r, cy - r, cx + r, cy + r));
        e.Codes.Add((10, cx)); e.Codes.Add((20, cy)); e.Codes.Add((30, 0.0)); e.Codes.Add((40, r));
        e.Codes.Add((50, a1)); e.Codes.Add((51, a2));
    }

    /// <summary>SOLID: thứ tự điểm DXF 1-2-3-4 tô theo đường 1→2→4→3.</summary>
    public void Solid(string layer, double x1, double y1, double x2, double y2, double x3, double y3, double x4, double y4)
    {
        var box = new Rect2(x1, y1, x2, y2).Union(x3, y3).Union(x4, y4);
        var e = Add("SOLID", layer, box);
        e.Codes.Add((10, x1)); e.Codes.Add((20, y1)); e.Codes.Add((30, 0.0));
        e.Codes.Add((11, x2)); e.Codes.Add((21, y2)); e.Codes.Add((31, 0.0));
        e.Codes.Add((12, x3)); e.Codes.Add((22, y3)); e.Codes.Add((32, 0.0));
        e.Codes.Add((13, x4)); e.Codes.Add((23, y4)); e.Codes.Add((33, 0.0));
    }

    public void FilledRect(string layer, double x1, double y1, double x2, double y2)
    {
        Solid(layer, x1, y1, x2, y1, x1, y2, x2, y2);
    }

    /// <summary>Chấm tròn đặc (mặt cắt thanh thép) – xấp xỉ bằng 12 tam giác SOLID.</summary>
    public void Dot(string layer, double cx, double cy, double r)
    {
        const int n = 12;
        for (int i = 0; i < n; i++)
        {
            double a1 = 2 * Math.PI * i / n, a2 = 2 * Math.PI * (i + 1) / n;
            double x1 = cx + r * Math.Cos(a1), y1 = cy + r * Math.Sin(a1);
            double x2 = cx + r * Math.Cos(a2), y2 = cy + r * Math.Sin(a2);
            Solid(layer, cx, cy, x1, y1, x2, y2, x2, y2);
        }
    }

    public Rect2 TextBox(double x, double y, double h, string text, double rot = 0, int ha = 0, int va = 0)
    {
        double w = TextWidth(text, h) * WidthFactor;
        double lx = ha == 1 ? -w / 2 : ha == 2 ? -w : 0;
        var (ly0, ly1) = va switch
        {
            2 => (-h / 2, h / 2),
            3 => (-h, 0.0),
            1 => (0.0, h),
            _ => (-0.25 * h, h)
        };
        double a = rot * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
        var r = Rect2.Empty;
        foreach (var (px, py) in new[] { (lx, ly0), (lx + w, ly0), (lx, ly1), (lx + w, ly1) })
            r = r.Union(x + px * c - py * s, y + px * s + py * c);
        return r;
    }

    /// <summary>TEXT. ha (72): 0 trái, 1 giữa, 2 phải. va (73): 0 chân chữ, 1 đáy, 2 giữa, 3 đỉnh.</summary>
    public Rect2 Text(string layer, double x, double y, double h, string text, double rot = 0, int ha = 0, int va = 0)
    {
        if (string.IsNullOrEmpty(text)) return Rect2.Empty;
        var box = TextBox(x, y, h, text, rot, ha, va);
        var e = Add("TEXT", layer, box);
        e.Codes.Add((10, x)); e.Codes.Add((20, y)); e.Codes.Add((30, 0.0));
        e.Codes.Add((40, h));
        double wf = WidthFactor;
        if (Math.Abs(wf - 1) > 1e-9) e.Codes.Add((41, wf));   // hệ số rộng chữ (chỉ ghi khi khác 1)
        e.Codes.Add((1, Encode(text)));
        if (Math.Abs(rot) > 1e-9) e.Codes.Add((50, rot));
        e.Codes.Add((7, "STANDARD"));
        if (ha != 0 || va != 0)
        {
            e.Codes.Add((72, ha));
            e.Codes.Add((11, x)); e.Codes.Add((21, y)); e.Codes.Add((31, 0.0));
            e.Codes.Add((73, va));
        }
        return box;
    }

    /// <summary>Ghép toàn bộ thực thể của mảnh hình khác vào, tịnh tiến (dx, dy).</summary>
    public void Append(DxfWriter other, double dx, double dy)
    {
        if (other == null) return;
        MergeDimStyles(other);
        other.EnsureKeys();   // khoá thực thể theo toạ độ của hình gốc (DxfWriter.Keys.cs)
        foreach (var e in other._ents)
        {
            var n = new DxfEnt { Type = e.Type, Box = e.Box.Offset(dx, dy) };
            n.CopyMetaFrom(e, other.OwnerKey);
            foreach (var (code, val) in e.Codes)
            {
                object v = val;
                if (val is double d)
                {
                    if (code >= 10 && code <= 18) v = d + dx;
                    else if (code >= 20 && code <= 28) v = d + dy;
                }
                n.Codes.Add((code, v));
            }
            _ents.Add(n);
            _ext = _ext.Union(n.Box);
        }
    }

    // ------------------------------------------------------------------
    // Ký hiệu tổ hợp
    // ------------------------------------------------------------------
    /// <summary>Vòng tròn số hiệu thép.</summary>
    public Rect2 MarkCircle(double cx, double cy, int mark, double rot = 0)
    {
        double r = MarkR;
        var g0 = BeginGroup("MARK|" + mark.ToString(Inv) + "|" + KeyNum(cx) + "|" + KeyNum(cy));
        Circle(Lay.KyHieu, cx, cy, r);
        Text(Lay.Chu, cx, cy, 0.9 * TagTh, mark.ToString(Inv), rot, 1, 2);
        EndGroup(g0);
        return new Rect2(cx - r, cy - r, cx + r, cy + r);
    }

    /// <summary>Chiều dài vai nhãn: lề đầu 0,3h + chữ (ước lượng thiên lớn) + khe hở ≥ 0,6h trước vòng số hiệu.</summary>
    public double TagShoulder(string text, double th) => 0.3 * th + TagTextWidth(text, th) * WidthFactor + TagGapFactor * th;

    /// <summary>Phạm vi chiếm chỗ của nhãn (vai + chữ + vòng số) với gốc vai tại (kx, ky).
    /// dir = +1: vai kéo sang phải (hoặc lên trên nếu vertical), −1: ngược lại.</summary>
    public Rect2 TagBox(double kx, double ky, string text, int mark, int dir = 1, bool vertical = false)
    {
        double th = TagTh, r = MarkR;
        double sh = TagShoulder(text, th);
        double total = sh + (mark > 0 ? 2 * r : 0);
        double lo = mark > 0 ? r : 0.2 * th;
        double hi = Math.Max(1.4 * th, mark > 0 ? r : 0);
        if (!vertical) return new Rect2(kx, ky - lo, kx + dir * total, ky + hi);
        return new Rect2(kx - hi, ky, kx + lo, ky + dir * total);
    }

    /// <summary>Nhãn thép kiểu Kết cấu Soft: chấm tại thanh (px,py) → đường dẫn → vai tại (kx,ky)
    /// có chữ đặt trên vai → vòng tròn số hiệu ở cuối vai. vertical: vai và chữ xoay dọc 90°.</summary>
    public Rect2 LeaderTag(double px, double py, double kx, double ky, string text, int mark, int dir = 1,
        bool vertical = false, bool dot = true)
    {
        double th = TagTh, r = MarkR;
        double sh = TagShoulder(text, th);
        // nhóm "nhãn thép": chọn/dời cả nhãn; chấm giữ tại thanh, đường dẫn chỉ dời điểm cuối (DxfWriter.Keys.cs)
        var g0 = BeginGroup("TAG|" + text + "|" + mark.ToString(Inv) + "|" + KeyNum(px) + "|" + KeyNum(py));
        _role = "anchor";
        if (dot) Dot(Lay.KyHieu, px, py, 0.22 * th);
        _role = "leader";
        if (Math.Abs(px - kx) > 1e-6 || Math.Abs(py - ky) > 1e-6) Line(Lay.KyHieu, px, py, kx, ky);
        _role = null;
        if (!vertical)
        {
            Line(Lay.KyHieu, kx, ky, kx + dir * sh, ky);
            if (dir > 0) Text(Lay.Chu, kx + 0.3 * th, ky + 0.3 * th, th, text, 0, 0, 0);
            else Text(Lay.Chu, kx - 0.3 * th, ky + 0.3 * th, th, text, 0, 2, 0);
            if (mark > 0) MarkCircle(kx + dir * (sh + r), ky, mark);
        }
        else
        {
            Line(Lay.KyHieu, kx, ky, kx, ky + dir * sh);
            if (dir > 0) Text(Lay.Chu, kx - 0.3 * th, ky + 0.3 * th, th, text, 90, 0, 0);
            else Text(Lay.Chu, kx - 0.3 * th, ky - 0.3 * th, th, text, 90, 2, 0);
            if (mark > 0) MarkCircle(kx, ky + dir * (sh + r), mark, 90);
        }
        EndGroup(g0);
        return TagBox(kx, ky, text, mark, dir, vertical);
    }

    /// <summary>Kích thước thẳng vẽ bằng LINE + gạch xiên + TEXT (không dùng DIMENSION).
    /// offset: khoảng cách từ điểm đo tới đường kích thước, dương về phía pháp tuyến trái của p1→p2.
    /// k: hệ số phóng hình (giá trị ghi = chiều dài / k). Kiểu kích thước (Thiết lập bản vẽ) theo tỷ lệ thể hiện Scale / k
    /// (vd. mặt cắt 1/25 vẽ phóng trên tờ 1/50 → kiểu VXT_1-25); dimStyle != null: dùng thông số cho trước.</summary>
    public void Dim(double x1, double y1, double x2, double y2, double offset, double k = 1, string text = null,
        KetCauVXT.Model.DimParams dimStyle = null)
    {
        double dx = x2 - x1, dy = y2 - y1, len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) return;
        var g0 = BeginGroup("DIM|" + KeyNum(x1) + "|" + KeyNum(y1) + "|" + KeyNum(x2) + "|" + KeyNum(y2) + "|" + KeyNum(offset));
        try { DimCore(x1, y1, x2, y2, offset, k, text, dimStyle, dx, dy, len); }
        finally { EndGroup(g0); }
    }

    private void DimCore(double x1, double y1, double x2, double y2, double offset, double k, string text,
        KetCauVXT.Model.DimParams dimStyle, double dx, double dy, double len)
    {
        var st = dimStyle ?? Style.DimFor(Scale / (k <= 0 ? 1 : k));
        UseDimStyle(st.Scale);
        double th = st.TextHeight * Scale;                       // chữ số kích thước
        double ux = dx / len, uy = dy / len, nx = -uy, ny = ux;
        double sg = offset >= 0 ? 1 : -1;
        double gap = st.ExtOffset * Scale, ext = st.ExtBeyond * Scale;   // hở điểm đo, gióng vượt
        double dle = st.LineExtend * Scale, tick = st.TickSize * Scale, tgap = st.TextGap * Scale;
        bool arrow = st.Arrow == KetCauVXT.Model.DimArrow.ClosedArrow;
        double ax = x1 + nx * offset, ay = y1 + ny * offset, bx = x2 + nx * offset, by = y2 + ny * offset;
        // chân đường gióng: cách điểm đo "gap"; nếu đặt độ dài gióng cố định thì chỉ vẽ đoạn FixedExt sát đường kích thước (gọn, kiểu DIMFXL)
        double start = gap;
        if (st.FixedExt > 0) start = Math.Max(gap, Math.Abs(offset) - st.FixedExt * Scale);
        if (Math.Abs(offset) > start)
        {
            Line(Lay.KichThuoc, x1 + nx * start * sg, y1 + ny * start * sg, ax + nx * ext * sg, ay + ny * ext * sg);
            Line(Lay.KichThuoc, x2 + nx * start * sg, y2 + ny * start * sg, bx + nx * ext * sg, by + ny * ext * sg);
        }
        if (!arrow)
        {
            Line(Lay.KichThuoc, ax - ux * dle, ay - uy * dle, bx + ux * dle, by + uy * dle);
            double tx = (ux + nx) * 0.3536 * tick, ty = (uy + ny) * 0.3536 * tick;
            Line(Lay.KichThuoc, ax - tx, ay - ty, ax + tx, ay + ty);
            Line(Lay.KichThuoc, bx - tx, by - ty, bx + tx, by + ty);
        }
        else
        {
            // mũi tên đặc (SOLID) dài "tick", rộng tick/3, đỉnh tại đường gióng
            Line(Lay.KichThuoc, ax, ay, bx, by);
            double hw = tick / 6;
            Solid(Lay.KichThuoc, ax, ay, ax + ux * tick + nx * hw, ay + uy * tick + ny * hw, ax + ux * tick - nx * hw, ay + uy * tick - ny * hw, ax + ux * tick - nx * hw, ay + uy * tick - ny * hw);
            Solid(Lay.KichThuoc, bx, by, bx - ux * tick + nx * hw, by - uy * tick + ny * hw, bx - ux * tick - nx * hw, by - uy * tick - ny * hw, bx - ux * tick - nx * hw, by - uy * tick - ny * hw);
        }
        string t = text ?? (st.Decimals == 0 && Math.Abs(st.LinearFactor - 1) < 1e-12 ? N0(len / (k <= 0 ? 1 : k)) : st.Format(len / (k <= 0 ? 1 : k)));
        double ang = Math.Atan2(uy, ux) * 180 / Math.PI;
        if (ang > 90.001) ang -= 180;
        else if (ang <= -89.999) ang += 180;
        double ar = ang * Math.PI / 180, upx = -Math.Sin(ar), upy = Math.Cos(ar);
        double mx = (ax + bx) / 2 + upx * tgap, my = (ay + by) / 2 + upy * tgap;
        Text(Lay.KichThuoc, mx, my, th, t, ang, 1, 0);
    }

    /// <summary>Chuỗi kích thước ngang: điểm đo tại cao độ yPts, đường kích thước tại yLine.</summary>
    public void DimChainH(IList<double> xs, double yPts, double yLine, double k = 1)
    {
        var v = xs.OrderBy(x => x).ToList();
        for (int i = 0; i + 1 < v.Count; i++)
            if (v[i + 1] - v[i] > 1e-3) Dim(v[i], yPts, v[i + 1], yPts, yLine - yPts, k);
    }

    /// <summary>Chuỗi kích thước đứng: điểm đo tại hoành độ xPts, đường kích thước tại xLine.</summary>
    public void DimChainV(IList<double> ys, double xPts, double xLine, double k = 1)
    {
        var v = ys.OrderBy(y => y).ToList();
        for (int i = 0; i + 1 < v.Count; i++)
            if (v[i + 1] - v[i] > 1e-3) Dim(xPts, v[i], xPts, v[i + 1], xPts - xLine, k);
    }

    /// <summary>Đầu trục định vị (vòng tròn + tên).</summary>
    public void GridBubble(double x, double y, string name)
    {
        var g0 = BeginGroup("AXIS|" + name + "|" + KeyNum(x) + "|" + KeyNum(y));
        Circle(Lay.KyHieu, x, y, BubbleR);
        Text(Lay.Chu, x, y, TextH(KetCauVXT.Model.TextRole.Axis), name, 0, 1, 2);
        EndGroup(g0);
    }

    /// <summary>Ký hiệu cao độ ▽ với đỉnh tam giác tại (x,y); dir = −1 chữ về bên trái, +1 bên phải.</summary>
    public Rect2 ElevMark(double x, double y, string text, int dir = -1)
    {
        if (!Style.ShowElevationMarks) return Rect2.Empty;   // tắt ký hiệu cao độ (Thiết lập bản vẽ)
        var g0 = BeginGroup("ELEV|" + text + "|" + KeyNum(x) + "|" + KeyNum(y));
        try { return ElevMarkCore(x, y, text, dir); }
        finally { EndGroup(g0); }
    }

    private Rect2 ElevMarkCore(double x, double y, string text, int dir)
    {
        double th = Th;
        Polyline(Lay.KyHieu, new List<(double X, double Y)> { (x, y), (x - 0.5 * th, y + 0.8 * th), (x + 0.5 * th, y + 0.8 * th) }, true);
        double tw = TextWidth(text, th) * WidthFactor;
        double xe = x + dir * (tw + 1.0 * th);
        Line(Lay.KyHieu, x, y + 0.8 * th, xe, y + 0.8 * th);
        if (dir < 0) Text(Lay.Chu, x - 0.7 * th, y + 1.1 * th, th, text, 0, 2, 0);
        else Text(Lay.Chu, x + 0.7 * th, y + 1.1 * th, th, text, 0, 0, 0);
        return new Rect2(x, y, xe, y + 2.2 * th);
    }

    /// <summary>Đường cắt lửng (zigzag) nằm ngang từ x1 tới x2 tại y.</summary>
    public void BreakH(string layer, double x1, double x2, double y)
    {
        double th = Th, xm = (x1 + x2) / 2;
        Polyline(layer, new List<(double X, double Y)>
        {
            (x1 - 0.3 * th, y), (xm - 0.3 * th, y), (xm - 0.15 * th, y + 0.5 * th),
            (xm + 0.15 * th, y - 0.5 * th), (xm + 0.3 * th, y), (x2 + 0.3 * th, y)
        }, false);
    }

    /// <summary>Đường cắt lửng đứng tại x từ y1 tới y2.</summary>
    public void BreakV(string layer, double x, double y1, double y2)
    {
        double th = Th, ym = (y1 + y2) / 2;
        Polyline(layer, new List<(double X, double Y)>
        {
            (x, y1 - 0.3 * th), (x, ym - 0.3 * th), (x + 0.5 * th, ym - 0.15 * th),
            (x - 0.5 * th, ym + 0.15 * th), (x, ym + 0.3 * th), (x, y2 + 0.3 * th)
        }, false);
    }

    /// <summary>Tên hình gạch chân + dòng tỷ lệ bên dưới, căn giữa tại cx, đỉnh tại yTop. Trả về cao độ đáy.
    /// section = true: tên mặt cắt (cỡ chữ Style.SectionTitleHeight), ngược lại tên cấu kiện/mặt bằng (Style.MemberTitleHeight).</summary>
    public double Title(double cx, double yTop, string title, string scaleText, bool section = false)
    {
        double th = Th, h = section ? SectionTitleTh : TitleTh;
        double tw = TextWidth(title, h) * WidthFactor;
        double yb = yTop - h;
        var g0 = BeginGroup("TITLE|" + title);   // tên hình + gạch chân + tỷ lệ: một nhóm
        Text(Lay.Chu, cx, yb, h, title, 0, 1, 0);
        Line(Lay.Chu, cx - tw / 2, yb - 0.35 * th, cx + tw / 2, yb - 0.35 * th);
        double y2 = yb - 0.35 * th - 0.5 * th - th;
        if (!string.IsNullOrEmpty(scaleText)) Text(Lay.Chu, cx, y2, th, scaleText, 0, 1, 0);
        EndGroup(g0);
        return y2 - 0.4 * th;
    }

    /// <summary>Vạch cắt "n|" – nét đậm ngắn từ (x,y1) tới (x,y2), số đặt cạnh.</summary>
    public void CutMark(double x1, double y1, double x2, double y2, string num, double tx, double ty)
    {
        var g0 = BeginGroup("CUT|" + num + "|" + KeyNum(tx) + "|" + KeyNum(ty));
        Polyline(Lay.KyHieu, new List<(double X, double Y)> { (x1, y1), (x2, y2) }, false, 0.2 * Th);
        Text(Lay.Chu, tx, ty, 1.2 * Th, num, 0, 1, 2);
        EndGroup(g0);
    }

    // ------------------------------------------------------------------
    // Ghi file
    // ------------------------------------------------------------------
    private static string F(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d)) d = 0;
        return Math.Round(d, 6).ToString("0.0#####", Inv);
    }

    private static void G(StringBuilder sb, int code, object val)
    {
        sb.Append(code.ToString(Inv).PadLeft(3)).Append("\r\n");
        string s = val switch
        {
            double d => F(d),
            int i => i.ToString(Inv),
            string str => str,
            null => "",
            _ => Convert.ToString(val, Inv) ?? ""
        };
        sb.Append(s).Append("\r\n");
    }

    public string ToDxfString()
    {
        var sb = new StringBuilder(1 << 16);
        var ext = _ext.IsEmpty ? new Rect2(0, 0, 1000, 1000) : _ext;

        // HEADER
        G(sb, 0, "SECTION"); G(sb, 2, "HEADER");
        G(sb, 9, "$ACADVER"); G(sb, 1, "AC1009");
        G(sb, 9, "$INSBASE"); G(sb, 10, 0.0); G(sb, 20, 0.0); G(sb, 30, 0.0);
        G(sb, 9, "$EXTMIN"); G(sb, 10, ext.X1); G(sb, 20, ext.Y1); G(sb, 30, 0.0);
        G(sb, 9, "$EXTMAX"); G(sb, 10, ext.X2); G(sb, 20, ext.Y2); G(sb, 30, 0.0);
        G(sb, 9, "$LIMMIN"); G(sb, 10, ext.X1); G(sb, 20, ext.Y1);
        G(sb, 9, "$LIMMAX"); G(sb, 10, ext.X2); G(sb, 20, ext.Y2);
        G(sb, 9, "$LTSCALE"); G(sb, 40, 0.3 * Scale);
        G(sb, 9, "$TEXTSIZE"); G(sb, 40, Th);
        G(sb, 9, "$TEXTSTYLE"); G(sb, 7, "STANDARD");
        G(sb, 9, "$CLAYER"); G(sb, 8, "0");
        G(sb, 0, "ENDSEC");

        // TABLES
        var std = Standard ?? KetCauVXT.Model.CadStandard.Active;
        if (std != null) WriteTablesStd(sb, std);   // chuẩn layer CAD (DxfWriter.Standard.cs)
        else
        {
            G(sb, 0, "SECTION"); G(sb, 2, "TABLES");
            G(sb, 0, "TABLE"); G(sb, 2, "LTYPE"); G(sb, 70, 3);
            G(sb, 0, "LTYPE"); G(sb, 2, "CONTINUOUS"); G(sb, 70, 0); G(sb, 3, "Solid line");
            G(sb, 72, 65); G(sb, 73, 0); G(sb, 40, 0.0);
            G(sb, 0, "LTYPE"); G(sb, 2, "DASHED"); G(sb, 70, 0); G(sb, 3, "__ __ __ __");
            G(sb, 72, 65); G(sb, 73, 2); G(sb, 40, 19.05); G(sb, 49, 12.7); G(sb, 49, -6.35);
            G(sb, 0, "LTYPE"); G(sb, 2, "CENTER"); G(sb, 70, 0); G(sb, 3, "____ _ ____ _");
            G(sb, 72, 65); G(sb, 73, 4); G(sb, 40, 50.8); G(sb, 49, 31.75); G(sb, 49, -6.35); G(sb, 49, 6.35); G(sb, 49, -6.35);
            G(sb, 0, "ENDTAB");

            bool hatchLayer = _ents.Any(e => e.Type == "HATCH" && LayerOf(e) == Lay.Hatch) && !LayerDefs.Any(d => d.Name == Lay.Hatch);
            G(sb, 0, "TABLE"); G(sb, 2, "LAYER"); G(sb, 70, LayerDefs.Length + (hatchLayer ? 1 : 0));
            foreach (var (name, color, lt) in LayerDefs)
            {
                G(sb, 0, "LAYER"); G(sb, 2, name); G(sb, 70, 0); G(sb, 62, color); G(sb, 6, lt);
            }
            if (hatchLayer) { G(sb, 0, "LAYER"); G(sb, 2, Lay.Hatch); G(sb, 70, 0); G(sb, 62, 8); G(sb, 6, "CONTINUOUS"); }   // lớp tô vật liệu
            G(sb, 0, "ENDTAB");

            G(sb, 0, "TABLE"); G(sb, 2, "STYLE"); G(sb, 70, 1);
            G(sb, 0, "STYLE"); G(sb, 2, "STANDARD"); G(sb, 70, 0); G(sb, 40, 0.0); G(sb, 41, WidthFactor); G(sb, 50, 0.0);
            G(sb, 71, 0); G(sb, 42, Th); G(sb, 3, "arial.ttf"); G(sb, 4, "");
            G(sb, 0, "ENDTAB");
            WriteDimStyles(sb);   // bảng DIMSTYLE VXT_1-25… (DxfWriter.Style.cs)
            G(sb, 0, "ENDSEC");
        }

        // ENTITIES
        G(sb, 0, "SECTION"); G(sb, 2, "ENTITIES");
        var map = std != null ? OutputNameMap(std) : null;
        foreach (var e0 in _ents)
        {
            if (e0.Type == "HATCH") { foreach (var x in ExpandHatchR12(e0)) WriteEnt(x); continue; }   // R12: mô phỏng (DxfWriter.Hatch.cs)
            WriteEnt(e0);
        }
        void WriteEnt(DxfEnt e)
        {
            G(sb, 0, e.Type);
            foreach (var (code, val) in e.Codes)
            {
                object v = val;
                if (map != null && code == 8 && val is string ln) v = map.TryGetValue(ln, out var on) ? on : KetCauVXT.Model.CadStandard.SafeLayerName(ln);
                else if (map != null && code == 6 && val is string lt6 && _definedLtypes != null && !_definedLtypes.Contains(lt6)) v = "CONTINUOUS";
                G(sb, code, v);
            }
        }
        G(sb, 0, "ENDSEC");
        G(sb, 0, "EOF");
        return sb.ToString();
    }

    public void Save(string path)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, ToDxfString(), new UTF8Encoding(false));
    }
}

/// <summary>Các tiện ích dùng chung của module bản vẽ.</summary>
internal static class DrawUtil
{
    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static bool IsStirrup(BarMark b) =>
        (b.Shape ?? "").StartsWith("Đai") || (b.Role ?? "").StartsWith("Đai") || b.Spacing > 0;

    public static string StirrupTag(int dia, double spacingM) => $"Ø{dia}a{Math.Round(spacingM * 1000).ToString("0", Inv)}";

    public static string BarTag(int count, int dia) => $"{count}Ø{dia}";

    public static string ScaleText(double denom) => "tl: 1/" + Math.Round(DxfWriter.MapScale(denom)).ToString("0", Inv);

    public static int RolePriority(string role)
    {
        role ??= "";
        if (role == "Trên" || role == "Dưới" || role.StartsWith("Thép dọc")) return 0;
        if (role.Contains("Mũ")) return 1;
        if (role.Contains("Tăng cường")) return 2;
        if (role.Contains("Giá")) return 3;
        return 4;
    }

    /// <summary>Chiều dài móc đầu/cuối (mm) suy ra từ Shape + Segments.</summary>
    public static (double Hs, double He) Hooks(BarMark bm)
    {
        var seg = bm.Segments ?? new List<double>();
        string sh = bm.Shape ?? "";
        if (sh.StartsWith("Nhấn")) return (sh == "Nhấn L" && seg.Count >= 4 ? seg[0] : 0.0, 0.0);   // nhấn cổ chai: chỉ chân bẻ (nếu có) ở đầu dưới
        if (seg.Count >= 3 && sh.StartsWith("U")) return (seg[0], seg[seg.Count - 1]);
        if (seg.Count >= 2 && sh.StartsWith("L")) return seg[0] <= seg[seg.Count - 1] ? (seg[0], 0.0) : (0.0, seg[seg.Count - 1]);
        return (0.0, 0.0);
    }

    /// <summary>Thanh nhấn cổ chai (Shape "Nhấn" / "Nhấn L"): (đoạn nối chồng đầu trên, mm; đoạn xiên, mm); (0, 0) nếu không phải.</summary>
    public static (double Lap, double Incline) Crank(BarMark bm)
    {
        var seg = bm.Segments ?? new List<double>();
        return (bm.Shape ?? "").StartsWith("Nhấn") && seg.Count >= 3 ? (seg[^1], seg[^2]) : (0.0, 0.0);
    }

    /// <summary>Gán làn (hàng) cho các thanh để đoạn [Start,End] trong cùng làn không chồng nhau.</summary>
    public static int[] AssignLanes(IList<BarMark> bars)
    {
        var res = new int[bars.Count];
        var order = Enumerable.Range(0, bars.Count)
            .OrderBy(i => RolePriority(bars[i].Role))
            .ThenByDescending(i => bars[i].End - bars[i].Start).ToList();
        var lanes = new List<List<(double A, double B)>>();
        foreach (int i in order)
        {
            double a = Math.Min(bars[i].Start, bars[i].End), e = Math.Max(bars[i].Start, bars[i].End);
            int r = 0;
            for (; r < lanes.Count; r++)
                if (lanes[r].All(t => e <= t.A + 1e-3 || a >= t.B - 1e-3)) break;
            if (r == lanes.Count) lanes.Add(new List<(double A, double B)>());
            lanes[r].Add((a, e));
            res[i] = r;
        }
        return res;
    }

    /// <summary>Chọn vị trí đặt nhãn dọc thanh [a,b] sao cho cách các vị trí đã dùng ít nhất minSep.</summary>
    public static double PickAnchor(double a, double b, List<double> used, double minSep)
    {
        double[] fr = { 0.5, 0.35, 0.65, 0.25, 0.75, 0.15, 0.85, 0.42, 0.58 };
        foreach (double f in fr)
        {
            double x = a + (b - a) * f;
            if (used.All(u => Math.Abs(u - x) >= minSep)) { used.Add(x); return x; }
        }
        double m = (a + b) / 2; used.Add(m); return m;
    }

    /// <summary>Tìm số hiệu trong danh sách thép theo nhãn "Ø8a200"/"3Ø16" (ưu tiên đúng IsTop).</summary>
    public static int FindMark(List<BarMark> bars, string tag, bool? isTop, HashSet<int> used = null)
    {
        if (bars == null || string.IsNullOrEmpty(tag)) return 0;
        string t = tag.Replace(" ", "");
        var c = bars.Where(b => b.Tag.Replace(" ", "") == t).ToList();
        if (c.Count == 0)
        {
            // nhãn sàn có thể chỉ chứa phần "Ø8" hoặc khác định dạng: so khớp theo đường kính + bước
            c = bars.Where(b => t.StartsWith("Ø" + b.Dia + "a") && t == StirrupTag(b.Dia, b.Spacing)).ToList();
        }
        if (c.Count == 0) return 0;
        var pick = c.FirstOrDefault(b => (isTop == null || b.IsTop == isTop) && (used == null || !used.Contains(b.Mark)))
                   ?? c.FirstOrDefault(b => isTop == null || b.IsTop == isTop) ?? c[0];
        used?.Add(pick.Mark);
        return pick.Mark;
    }

    public static FrameSectionInfo SectionOf(KetCauVXT.Model.Project p, string name)
    {
        var s = p.FrameSections.FirstOrDefault(x => x.Name == name);
        if (s == null) return new FrameSectionInfo(0.22, 0.3);
        if (s.Shape == KetCauVXT.Model.SectionShape.RectConcrete) return new FrameSectionInfo(s.B, s.H);
        var pr = s.Props();
        return new FrameSectionInfo(pr.Width, pr.Depth);
    }

    /// <summary>Mảnh hình báo lỗi (để một cấu kiện lỗi không làm hỏng cả bản vẽ).</summary>
    public static DxfWriter ErrorFigure(string what, Exception ex, double S)
    {
        var w = new DxfWriter(S);
        w.Text(Lay.Chu, 0, 0, w.Th, "LỖI VẼ " + what + ": " + (ex?.Message ?? ""));
        return w;
    }

    public static string SafeFile(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "Khac";
        var bad = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (char ch in s.Trim()) sb.Append(bad.Contains(ch) || ch == ' ' ? '_' : ch);
        return sb.ToString();
    }
}

/// <summary>Kích thước tiết diện chữ nhật (m): B theo trục 3, H theo trục 2.</summary>
internal readonly record struct FrameSectionInfo(double B, double H);
