using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Ảnh chụp trạng thái dàn trang (hoàn tác/làm lại).</summary>
public sealed class SheetSnapshot
{
    public List<Sheet> Sheets { get; set; } = new();
    public TitleBlockInfo TitleBlock { get; set; } = new();
}

/// <summary>Bộ nhớ tạm sao chép/dán khung nhìn và đối tượng giữa các tờ.</summary>
public sealed class SheetClipboard
{
    public List<SheetView> Views { get; set; } = new();
    public List<SheetItem> Items { get; set; } = new();
    /// <summary>Số hiệu tờ nguồn (dán lại cùng tờ thì lệch 10 mm)</summary>
    public string Source { get; set; } = "";
}

/// <summary>Dàn trang thủ công: hình DXF của đối tượng chú thích tự do, cắt/viền khung nhìn, tờ trắng,
/// đánh lại số tờ, ảnh chụp trạng thái cho hoàn tác, bộ nhớ tạm sao chép/dán.</summary>
public static partial class SheetBuilder
{
    private static readonly JsonSerializerOptions SnapOpts = new() { Converters = { new JsonStringEnumConverter() } };

    public static readonly string[] ItemLayers = { Lay.Chu, Lay.KyHieu, Lay.KichThuoc, Lay.KhungTen, Lay.BeTong, Lay.Thep, Lay.Truc, "0" };

    public static string ItemKindName(SheetItemKind k) => k switch
    {
        SheetItemKind.Text => "Chữ",
        SheetItemKind.Line => "Đường / mũi tên",
        SheetItemKind.Rect => "Khung chữ nhật",
        SheetItemKind.Circle => "Đường tròn",
        SheetItemKind.Cloud => "Đám mây sửa đổi",
        SheetItemKind.Image => "Ảnh",
        SheetItemKind.Table => "Bảng",
        SheetItemKind.Dim => "Kích thước",
        _ => k.ToString()
    };

    public static string DefaultLayer(SheetItemKind k) => k switch
    {
        SheetItemKind.Text or SheetItemKind.Table => Lay.Chu,
        SheetItemKind.Dim => Lay.KichThuoc,
        _ => Lay.KyHieu
    };

    public static string LayerOf(SheetItem it) => string.IsNullOrWhiteSpace(it?.Layer) ? DefaultLayer(it?.Kind ?? SheetItemKind.Text) : it.Layer.Trim();

    // ==================================================================
    // Màu
    // ==================================================================
    public static bool TryParseColor(string hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        string h = (hex ?? "").Trim().TrimStart('#');
        if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return false;
        r = (byte)((v >> 16) & 255); g = (byte)((v >> 8) & 255); b = (byte)(v & 255);
        return true;
    }

    private static readonly (int Aci, int R, int G, int B)[] AciTable =
    {
        (1, 255, 0, 0), (2, 255, 255, 0), (3, 0, 255, 0), (4, 0, 255, 255), (5, 0, 0, 255), (6, 255, 0, 255),
        (7, 0, 0, 0), (8, 128, 128, 128), (9, 192, 192, 192), (30, 255, 127, 0), (94, 0, 127, 0), (150, 0, 63, 127)
    };

    /// <summary>Màu ACI gần nhất của "#RRGGBB" (0 = theo lớp khi chuỗi trống/sai).</summary>
    public static int Aci(string hex)
    {
        if (!TryParseColor(hex, out byte r, out byte g, out byte b)) return 0;
        int best = 7, bd = int.MaxValue;
        foreach (var (aci, ar, ag, ab) in AciTable)
        {
            int d = (r - ar) * (r - ar) + (g - ag) * (g - ag) + (b - ab) * (b - ab);
            if (d < bd) { bd = d; best = aci; }
        }
        return best;
    }

    // ==================================================================
    // Hình của đối tượng chú thích
    // ==================================================================
    public static string[] TextLines(string t) => (t ?? "").Replace("\r", "").Split('\n');

    /// <summary>Chữ ghi trên kích thước: chữ ghi đè hoặc chiều dài (mm giấy × DimScale).</summary>
    public static string DimText(SheetItem it)
    {
        if (!string.IsNullOrWhiteSpace(it.Text)) return it.Text.Trim();
        if (it.Pts == null || it.Pts.Count < 4) return "";
        double dx = it.Pts[2] - it.Pts[0], dy = it.Pts[3] - it.Pts[1];
        double len = Math.Sqrt(dx * dx + dy * dy) * (it.DimScale > 0 ? it.DimScale : 1);
        return DxfWriter.N0(len);
    }

    private static List<(double X, double Y)> PtsOf(SheetItem it)
    {
        var res = new List<(double X, double Y)>();
        if (it.Pts == null) return res;
        for (int i = 0; i + 1 < it.Pts.Count; i += 2) res.Add((it.Pts[i], it.Pts[i + 1]));
        return res;
    }

    private static void Arrow(DxfWriter w, string L, double tx, double ty, double fx, double fy, double lw)
    {
        double dx = tx - fx, dy = ty - fy, l = Math.Sqrt(dx * dx + dy * dy);
        if (l < 1e-6) return;
        double ux = dx / l, uy = dy / l;
        double al = Math.Min(l * 0.8, Math.Max(2.5, 6 * lw)), aw = 0.3 * al;
        double bx = tx - ux * al, by = ty - uy * al;
        w.Solid(L, tx, ty, bx - uy * aw, by + ux * aw, bx + uy * aw, by - ux * aw, bx + uy * aw, by - ux * aw);
    }

    /// <summary>Hình DXF (mm giấy, tỷ lệ 1/1) của một đối tượng chú thích. Ảnh: khung + tên (DXF R12 không có ảnh).</summary>
    public static DxfWriter ItemFigure(SheetItem it)
    {
        var w = new DxfWriter(1);
        if (it == null) return w;
        string L = LayerOf(it);
        double lw = Math.Max(0, it.LineWeight);
        string lt = (it.LineType ?? "").Trim().ToUpperInvariant();
        if (lt == "CONTINUOUS") lt = "";
        switch (it.Kind)
        {
            case SheetItemKind.Text:
            {
                double h = Math.Max(0.5, it.TextHeight), sp = SheetLayoutMath.LineSpacing * h;
                double a = it.Rotation * Math.PI / 180, ddx = Math.Sin(a), ddy = -Math.Cos(a);
                int ha = Math.Clamp(it.Align, 0, 2);
                var lines = TextLines(it.Text);
                for (int i = 0; i < lines.Length; i++)
                {
                    string t = lines[i].TrimEnd();
                    if (t.Length == 0) continue;
                    w.Text(L, it.X + ddx * i * sp, it.Y + ddy * i * sp, h, t, it.Rotation, ha, 3);
                }
                break;
            }
            case SheetItemKind.Line:
            {
                var pts = PtsOf(it);
                if (pts.Count < 2) break;
                bool closed = it.Closed && pts.Count > 2;
                w.Polyline(L, pts, closed, lw);
                if (!closed)
                {
                    if (it.ArrowStart) Arrow(w, L, pts[0].X, pts[0].Y, pts[1].X, pts[1].Y, lw);
                    if (it.ArrowEnd) Arrow(w, L, pts[^1].X, pts[^1].Y, pts[^2].X, pts[^2].Y, lw);
                }
                break;
            }
            case SheetItemKind.Rect:
                w.Polyline(L, new List<(double X, double Y)> { (it.X, it.Y), (it.X + it.W, it.Y), (it.X + it.W, it.Y + it.H), (it.X, it.Y + it.H) }, true, lw);
                break;
            case SheetItemKind.Circle:
                if (it.R > 0) w.Circle(L, it.X, it.Y, it.R);
                break;
            case SheetItemKind.Cloud:
            {
                double arc = Math.Max(1, it.ArcLength);
                var c = new[] { (it.X, it.Y), (it.X + it.W, it.Y), (it.X + it.W, it.Y + it.H), (it.X, it.Y + it.H) };
                for (int e = 0; e < 4; e++)
                {
                    var (ax, ay) = c[e];
                    var (bx, by) = c[(e + 1) % 4];
                    double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                    if (len < 1e-6) continue;
                    int n = Math.Max(1, (int)Math.Round(len / arc));
                    double seg = len / n, ux = (bx - ax) / len, uy = (by - ay) / len;
                    double ang = Math.Atan2(uy, ux) * 180 / Math.PI;
                    for (int j = 0; j < n; j++)
                    {
                        double mx = ax + ux * (j + 0.5) * seg, my = ay + uy * (j + 0.5) * seg;
                        // nửa đường tròn lồi ra ngoài (đi ngược chiều kim đồng hồ quanh khung)
                        w.Arc(L, mx, my, seg / 2, ang + 180, ang + 360);
                    }
                }
                break;
            }
            case SheetItemKind.Image:
            {
                w.Rect(L, it.X, it.Y, it.X + it.W, it.Y + it.H);
                string name = string.IsNullOrWhiteSpace(it.Text) ? "ẢNH" : "ẢNH: " + Path.GetFileName(it.Text.Trim());
                double th = Math.Clamp(Math.Min(it.H * 0.15, (it.W - 2) / Math.Max(1, name.Length * 0.62)), 0.8, 2.5);
                w.Text(L, it.X + it.W / 2, it.Y + it.H / 2, th, name, 0, 1, 2);
                break;
            }
            case SheetItemKind.Table:
                TableFigure(w, L, it, lw);
                break;
            case SheetItemKind.Dim:
            {
                if (it.Pts == null || it.Pts.Count < 4) break;
                double h = Math.Max(0.5, it.TextHeight);
                var dw = new DxfWriter(h / 2.5);
                double k = it.DimScale > 0 ? 1 / it.DimScale : 1;
                dw.Dim(it.Pts[0], it.Pts[1], it.Pts[2], it.Pts[3], it.Offset, k, string.IsNullOrWhiteSpace(it.Text) ? null : it.Text.Trim());
                w.Append(dw, 0, 0);
                break;
            }
        }
        w.StyleFrom(0, Aci(it.Color), lt.Length > 0 ? lt : null, L);
        return w;
    }

    private static void TableFigure(DxfWriter w, string L, SheetItem it, double lw)
    {
        int rows = Math.Max(1, it.Rows), cols = Math.Max(1, it.Cols);
        double x1 = it.X, y1 = it.Y, x2 = it.X + Math.Max(1, it.W), y2 = it.Y + Math.Max(1, it.H);
        w.Polyline(L, new List<(double X, double Y)> { (x1, y1), (x2, y1), (x2, y2), (x1, y2) }, true, lw > 0 ? lw : 0.35);
        var wts = it.ColWidths != null && it.ColWidths.Count == cols && it.ColWidths.All(x => x > 0) ? it.ColWidths.ToList() : Enumerable.Repeat(1.0, cols).ToList();
        double sum = wts.Sum();
        var xs = new List<double> { x1 };
        for (int c = 0; c < cols; c++) xs.Add(xs[^1] + (x2 - x1) * wts[c] / sum);
        double rh = (y2 - y1) / rows;
        for (int r = 1; r < rows; r++)
        {
            double y = y2 - r * rh;
            if (r == 1 && it.HeaderRow && rows > 1) w.Polyline(L, new List<(double X, double Y)> { (x1, y), (x2, y) }, false, 0.35);
            else w.Line(L, x1, y, x2, y);
        }
        for (int c = 1; c < cols; c++) w.Line(L, xs[c], y1, xs[c], y2);
        double pad = 1.2;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                string t = it.Cell(r, c);
                if (string.IsNullOrWhiteSpace(t)) continue;
                double cw = xs[c + 1] - xs[c];
                double th = Math.Min(Math.Max(0.5, it.TextHeight), 0.6 * rh);
                th = Math.Max(0.8, Math.Min(th, (cw - 2 * pad) / Math.Max(1, t.Length * 0.62)));
                int ha = it.HeaderRow && r == 0 ? 1 : Math.Clamp(it.Align, 0, 2);
                double tx = ha == 0 ? xs[c] + pad : ha == 2 ? xs[c + 1] - pad : (xs[c] + xs[c + 1]) / 2;
                w.Text(L, tx, y2 - (r + 0.5) * rh, th, t, 0, ha, 2);
            }
    }

    /// <summary>Viền khung nhìn (lớp khung tên).</summary>
    public static DxfWriter ViewBorder(SheetView v)
    {
        var w = new DxfWriter(1);
        w.Rect(Lay.KhungTen, v.X, v.Y, v.X + v.W, v.Y + v.H);
        return w;
    }

    /// <summary>Nội dung có vượt ra ngoài khung nhìn không (khi đó DXF cắt theo khung như trên màn hình).</summary>
    public static bool NeedsClip(SheetView v, ViewLayout lay)
    {
        if (lay == null || lay.Content.IsEmpty) return false;
        var f = SheetLayoutMath.Bounds(v).Inflate(0.5);
        var c = lay.Content;
        return c.X1 < f.X1 || c.X2 > f.X2 || c.Y1 < f.Y1 || c.Y2 > f.Y2;
    }

    private static void ComposeView(DxfWriter outW, SheetView v, ViewLayout lay)
    {
        bool clip = NeedsClip(v, lay);
        var frame = SheetLayoutMath.Bounds(v);
        foreach (var it in lay.Items)
        {
            if (clip) outW.AppendScaledClipped(it.F.W, it.K, it.Dx, it.Dy, frame);
            else outW.AppendScaled(it.F.W, it.K, it.Dx, it.Dy);
        }
        if (lay.Caption != null) outW.Append(lay.Caption, 0, 0);
        if (v.Border) outW.Append(ViewBorder(v), 0, 0);
    }

    private static void ComposeItems(DxfWriter outW, Sheet s)
    {
        if (s.Items == null) return;
        foreach (var it in s.Items) outW.Append(ItemFigure(it), 0, 0);
    }

    // ==================================================================
    // Tạo đối tượng
    // ==================================================================
    public static SheetItem NewText(double x, double y, string text, double h = 3.5) =>
        new() { Kind = SheetItemKind.Text, X = x, Y = y, Text = text ?? "", TextHeight = h };

    public static SheetItem NewLine(double x1, double y1, double x2, double y2, bool arrowEnd = false) =>
        new() { Kind = SheetItemKind.Line, X = x1, Y = y1, Pts = new List<double> { x1, y1, x2, y2 }, ArrowEnd = arrowEnd };

    public static SheetItem NewRect(double x, double y, double w, double h) =>
        new() { Kind = SheetItemKind.Rect, X = x, Y = y, W = w, H = h };

    public static SheetItem NewCircle(double cx, double cy, double r) =>
        new() { Kind = SheetItemKind.Circle, X = cx, Y = cy, R = r };

    public static SheetItem NewCloud(double x, double y, double w, double h) =>
        new() { Kind = SheetItemKind.Cloud, X = x, Y = y, W = w, H = h, Layer = Lay.Thep };

    public static SheetItem NewImage(double x, double y, double w, double h, string base64, string name = "") =>
        new() { Kind = SheetItemKind.Image, X = x, Y = y, W = w, H = h, ImageData = base64 ?? "", Text = name ?? "" };

    public static SheetItem NewTable(double x, double y, double w, double h, int rows, int cols)
    {
        var t = new SheetItem { Kind = SheetItemKind.Table, X = x, Y = y, W = w, H = h, Rows = Math.Max(1, rows), Cols = Math.Max(1, cols), TextHeight = 2.5 };
        for (int r = 0; r < t.Rows; r++)
            for (int c = 0; c < t.Cols; c++) t.Cells.Add(r == 0 ? $"Cột {c + 1}" : "");
        return t;
    }

    public static SheetItem NewDim(double x1, double y1, double x2, double y2, double offset = 8, double dimScale = 0) =>
        new() { Kind = SheetItemKind.Dim, X = x1, Y = y1, Pts = new List<double> { x1, y1, x2, y2 }, Offset = offset, DimScale = dimScale, TextHeight = 2.5 };

    /// <summary>Tờ trắng: không khung viền, không khung tên – dàn trang tự do.</summary>
    public static Sheet BlankSheet(Project p, string number, string paper = "A3", bool landscape = true) => new()
    {
        Number = number ?? "KC-01", Name = "TRANG TRẮNG", Paper = paper ?? "A3", Landscape = landscape,
        ShowFrame = false, ShowTitleBlock = false,
        Designer = p?.Designer ?? "", Date = DateTime.Today.ToString("dd/MM/yyyy", Inv)
    };

    /// <summary>Đánh lại số hiệu tờ theo thứ tự: prefix + 01, 02…</summary>
    public static void Renumber(IList<Sheet> sheets, string prefix = "KC-", int start = 1)
    {
        if (sheets == null) return;
        int n = start;
        foreach (var s in sheets) s.Number = (prefix ?? "") + (n++).ToString("00", Inv);
    }

    // ==================================================================
    // Hoàn tác, sao chép
    // ==================================================================
    public static string Snapshot(Project p) =>
        JsonSerializer.Serialize(new SheetSnapshot { Sheets = p.Sheets ?? new List<Sheet>(), TitleBlock = p.TitleBlock ?? new TitleBlockInfo() }, SnapOpts);

    public static void Restore(Project p, string json)
    {
        if (p == null || string.IsNullOrEmpty(json)) return;
        var st = JsonSerializer.Deserialize<SheetSnapshot>(json, SnapOpts);
        if (st == null) return;
        p.Sheets = st.Sheets ?? new List<Sheet>();
        p.TitleBlock = st.TitleBlock ?? new TitleBlockInfo();
    }

    public static string ToClipboard(IEnumerable<SheetView> views, IEnumerable<SheetItem> items, string source) =>
        JsonSerializer.Serialize(new SheetClipboard { Views = views?.ToList() ?? new(), Items = items?.ToList() ?? new(), Source = source ?? "" }, SnapOpts);

    public static SheetClipboard FromClipboard(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<SheetClipboard>(json, SnapOpts); }
        catch (JsonException) { return null; }
    }

    public static SheetItem Clone(SheetItem it) => JsonSerializer.Deserialize<SheetItem>(JsonSerializer.Serialize(it, SnapOpts), SnapOpts);
}
