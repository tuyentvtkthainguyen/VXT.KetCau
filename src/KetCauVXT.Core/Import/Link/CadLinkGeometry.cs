using System.Collections.Concurrent;
using System.Security.Cryptography;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Link;

// =====================================================================
// HÌNH HỌC BẢN VẼ LIÊN KẾT: đọc DWG/DXF một lần (DxfReader / CadFileReaders.Dwg), giữ nét (toạ độ bản vẽ, m) trong bộ nhớ đệm
// theo CadLink.Id; dựng "khung nhìn" đã biến đổi sang toạ độ mô hình (cache theo CadLink.TransformKey) + lưới băm không gian
// để bắt điểm (đầu nét, trung điểm, giao điểm, tâm) mà không phải duyệt toàn bản vẽ.
// =====================================================================

/// <summary>Một nét (đa tuyến) của bản vẽ, toạ độ bản vẽ (m).</summary>
public sealed class CadLinkPrim
{
    public int Layer;
    public bool Closed;
    public double[] Xs = Array.Empty<double>(), Ys = Array.Empty<double>();
    /// <summary>Đoạn cung (chỉ số điểm) – điểm trong cung không phải đầu nét</summary>
    public List<DxfArcSpan> Arcs;
    /// <summary>Vòng tròn: tâm, bán kính (Xs/Ys = điểm chia để vẽ)</summary>
    public bool IsCircle;
    public double Cx, Cy, R;
}

/// <summary>Chữ của bản vẽ (toạ độ bản vẽ, m).</summary>
public sealed class CadLinkText
{
    public int Layer;
    public double X, Y, Height;
    public string Text = "";
}

/// <summary>Bản vẽ đã đọc (toạ độ bản vẽ đã đổi đơn vị → m).</summary>
public sealed class CadLinkGeometry
{
    public string Path { get; init; } = "";
    public DateTime WriteUtc { get; init; }
    public long Length { get; init; }
    public string Hash { get; init; } = "";
    public int InsUnits { get; init; }
    /// <summary>Hệ số đơn vị đã dùng (đơn vị bản vẽ → m).</summary>
    public double UnitScale { get; init; }
    /// <summary>Hệ số người dùng yêu cầu khi đọc (0 = theo $INSUNITS) – đổi → đọc lại.</summary>
    public double RequestedUnitScale { get; init; }
    public List<string> Warnings { get; } = new();
    public List<string> Layers { get; } = new();
    public Dictionary<string, int> LayerIndex { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<int> LayerColors { get; } = new();
    public HashSet<string> OffLayers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> FrozenLayers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<CadLinkPrim> Prims { get; } = new();
    public List<CadLinkText> Texts { get; } = new();
    /// <summary>Thực thể gốc (toạ độ bản vẽ, m) – dùng nhận dạng cấu kiện khi đồng bộ mô hình.</summary>
    public List<DxfEntity> Entities { get; } = new();
    public List<DxfEntity> Fills { get; } = new();
    public double X0 = double.MaxValue, Y0 = double.MaxValue, X1 = double.MinValue, Y1 = double.MinValue;
    /// <summary>Số đoạn thẳng (sau khi chia cung)</summary>
    public int SegmentCount { get; private set; }

    private CadLinkView _view;
    private readonly object _viewLock = new();

    public bool HasExtent => X1 >= X0 && Y1 >= Y0;

    public int Layer(string name)
    {
        name = string.IsNullOrEmpty(name) ? "0" : name;
        if (LayerIndex.TryGetValue(name, out int i)) return i;
        i = Layers.Count;
        Layers.Add(name); LayerIndex[name] = i; LayerColors.Add(7);
        return i;
    }

    // ------------------------------------------------------------------
    // Đọc file
    // ------------------------------------------------------------------
    /// <summary>Đọc nội dung file cho phép AutoCAD vẫn đang mở (FileShare.ReadWrite). Ném IOException khi file đang bị khoá ghi.</summary>
    public static byte[] ReadBytesShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var ms = new MemoryStream(fs.Length > 0 && fs.Length < int.MaxValue ? (int)fs.Length : 0);
        fs.CopyTo(ms);
        return ms.ToArray();
    }

    public static string HashOf(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes ?? Array.Empty<byte>()));
    }

    /// <summary>Đọc bản vẽ DWG/DXF. unitScale &gt; 0: đơn vị bản vẽ → m ghi đè $INSUNITS.
    /// Ném IOException (file đang ghi/khoá – thử lại), FileNotFoundException; nội dung không đọc được → InvalidDataException (thông báo tiếng Việt).</summary>
    public static CadLinkGeometry Load(string path, double unitScale = 0)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("Không tìm thấy bản vẽ link – chọn lại", path);
        var fi = new FileInfo(path);
        byte[] bytes = ReadBytesShared(path);
        string hash = HashOf(bytes);
        DxfReader r;
        if (DwgConverter.IsDwg(path) || (bytes.Length >= 6 && bytes[0] == (byte)'A' && bytes[1] == (byte)'C' && bytes[2] == (byte)'1' && bytes[3] == (byte)'0'))
        {
            r = CadFileReaders.Read(path, unitScale, out string err);
            if (r == null) throw new InvalidDataException(Path0(path) + ": " + (err ?? "không đọc được bản vẽ DWG") + " – lưu bản vẽ dạng DXF (SaveAs DXF) rồi liên kết lại.");
        }
        else r = DxfReader.FromBytes(bytes, unitScale);
        if (r.Entities.Count == 0 && r.Warnings.Any(w => w == DxfReader.BinaryDxfMessage)) throw new InvalidDataException(DxfReader.BinaryDxfMessage);
        var g = new CadLinkGeometry
        {
            Path = path, WriteUtc = fi.LastWriteTimeUtc, Length = fi.Length, Hash = hash, InsUnits = r.InsUnits,
            UnitScale = r.Scale, RequestedUnitScale = unitScale
        };
        g.Warnings.AddRange(r.Warnings);
        g.Fill(r);
        return g;
    }

    private static string Path0(string p) { try { return System.IO.Path.GetFileName(p); } catch (Exception) { return p; } }

    /// <summary>Dựng từ bộ đọc đã nạp (kiểm thử / bộ đọc riêng).</summary>
    public static CadLinkGeometry FromReader(DxfReader r, string path = "", string hash = "")
    {
        var g = new CadLinkGeometry { Path = path ?? "", Hash = hash ?? "", InsUnits = r.InsUnits, UnitScale = r.Scale };
        g.Warnings.AddRange(r.Warnings);
        g.Fill(r);
        return g;
    }

    private void Fill(DxfReader r)
    {
        foreach (var kv in r.LayerColors) { int i = Layer(kv.Key); LayerColors[i] = kv.Value; }
        foreach (var l in r.OffLayers) OffLayers.Add(l);
        foreach (var l in r.FrozenLayers) FrozenLayers.Add(l);
        Entities.AddRange(r.Entities);
        Fills.AddRange(r.FillEntities);
        foreach (var e in r.Entities.Concat(r.FillEntities)) AddEntity(e);
    }

    private void Ext(double x, double y)
    {
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y)) return;
        if (x < X0) X0 = x; if (x > X1) X1 = x; if (y < Y0) Y0 = y; if (y > Y1) Y1 = y;
    }

    private void AddEntity(DxfEntity e)
    {
        if (e?.Points == null || e.Points.Count == 0) return;
        int layer = Layer(e.Layer);
        switch (e.Type)
        {
            case DxfEntityType.Text:
                if (string.IsNullOrWhiteSpace(e.Text)) return;
                Texts.Add(new CadLinkText { Layer = layer, X = e.Points[0].X, Y = e.Points[0].Y, Height = Math.Max(e.TextHeight, 1e-3), Text = e.Text });
                Ext(e.Points[0].X, e.Points[0].Y);
                return;
            case DxfEntityType.Circle:
            {
                if (e.Radius <= 0) return;
                const int n = 24;
                var p = new CadLinkPrim { Layer = layer, Closed = true, IsCircle = true, Cx = e.Points[0].X, Cy = e.Points[0].Y, R = e.Radius, Xs = new double[n], Ys = new double[n] };
                for (int k = 0; k < n; k++)
                {
                    double a = 2 * Math.PI * k / n;
                    p.Xs[k] = p.Cx + p.R * Math.Cos(a); p.Ys[k] = p.Cy + p.R * Math.Sin(a);
                }
                Ext(p.Cx - p.R, p.Cy - p.R); Ext(p.Cx + p.R, p.Cy + p.R);
                Prims.Add(p); SegmentCount += n;
                return;
            }
            default:
            {
                if (e.Points.Count < 2) return;
                var p = new CadLinkPrim
                {
                    Layer = layer, Closed = e.Closed && e.Points.Count > 2, Arcs = e.Arcs,
                    Xs = e.Points.Select(q => q.X).ToArray(), Ys = e.Points.Select(q => q.Y).ToArray()
                };
                for (int i = 0; i < p.Xs.Length; i++) Ext(p.Xs[i], p.Ys[i]);
                Prims.Add(p); SegmentCount += p.Xs.Length - 1 + (p.Closed ? 1 : 0);
                return;
            }
        }
    }

    // ------------------------------------------------------------------
    // Khung nhìn theo toạ độ mô hình
    // ------------------------------------------------------------------
    /// <summary>Hình đã biến đổi theo phép đặt của bản vẽ liên kết (cache: dựng lại khi đổi điểm gốc / góc / tỷ lệ).</summary>
    public CadLinkView View(CadLink link)
    {
        string key = link?.TransformKey ?? "";
        var v = _view;
        if (v != null && v.Key == key) return v;
        lock (_viewLock)
        {
            if (_view != null && _view.Key == key) return _view;
            _view = CadLinkView.Build(this, link ?? new CadLink(), key);
            return _view;
        }
    }

    /// <summary>Bản sao các thực thể đã đổi sang toạ độ mô hình (để nhận dạng tường/cột/dầm/trục bằng ArchPlanReader).</summary>
    public (List<DxfEntity> ents, List<DxfEntity> fills) ModelEntities(CadLink link)
    {
        link ??= new CadLink();
        double s = link.Scale > 0 ? link.Scale : 1;
        DxfEntity T(DxfEntity e)
        {
            var c = new DxfEntity
            {
                Type = e.Type, Layer = e.Layer, Closed = e.Closed, Radius = e.Radius * s, Text = e.Text, TextHeight = e.TextHeight * s,
                Block = e.Block, LineType = e.LineType, AttributeTag = e.AttributeTag, SourceType = e.SourceType, Width = e.Width * s, FontHint = e.FontHint,
                Points = e.Points.Select(q => link.ToModel(q.X, q.Y)).ToList()
            };
            if (e.Arcs != null)
                c.Arcs = e.Arcs.Select(a => { var m = link.ToModel(a.Cx, a.Cy); return new DxfArcSpan { I0 = a.I0, I1 = a.I1, Cx = m.X, Cy = m.Y, R = a.R * s }; }).ToList();
            return c;
        }
        return (Entities.Select(T).ToList(), Fills.Select(T).ToList());
    }
}

/// <summary>Kiểu điểm bắt trên nét bản vẽ.</summary>
public enum CadPointKind : byte { End, Mid, Center }

/// <summary>Hình bản vẽ liên kết ở toạ độ mô hình + chỉ mục không gian (lưới băm) cho bắt điểm.</summary>
public sealed class CadLinkView
{
    /// <summary>Số đoạn tối đa xét trong một lần bắt điểm (giới hạn thời gian khi bản vẽ dày nét).</summary>
    public const int MaxSegsPerQuery = 400;
    /// <summary>Số cặp đoạn tối đa xét giao điểm trong một lần bắt.</summary>
    public const int MaxPairsPerQuery = 30_000;

    public string Key { get; private init; } = "";
    /// <summary>Đoạn thẳng: x1, y1, x2, y2 (toạ độ mô hình).</summary>
    public double[] Seg { get; private set; } = Array.Empty<double>();
    public int[] SegLayer { get; private set; } = Array.Empty<int>();
    /// <summary>Đoạn thuộc cung / vòng tròn (không lấy trung điểm đoạn)</summary>
    public bool[] SegArc { get; private set; } = Array.Empty<bool>();
    public int SegCount { get; private set; }
    /// <summary>Điểm bắt riêng: đầu nét, trung điểm cung, tâm tròn</summary>
    public List<(double X, double Y, int Layer, CadPointKind Kind)> Points { get; } = new();
    public List<(double X, double Y, double Height, int Layer, string Text)> Texts { get; } = new();
    public double X0 = double.MaxValue, Y0 = double.MaxValue, X1 = double.MinValue, Y1 = double.MinValue;
    public double Cell { get; private set; } = 1;
    private readonly Dictionary<long, List<int>> _segCells = new();
    private readonly Dictionary<long, List<int>> _ptCells = new();

    private long Key2(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;
    private int C(double v) => (int)Math.Floor(v / Cell);

    internal static CadLinkView Build(CadLinkGeometry g, CadLink l, string key)
    {
        var v = new CadLinkView { Key = key };
        int n = Math.Max(0, g.SegmentCount);
        var seg = new List<double>(n * 4); var lay = new List<int>(n); var arc = new List<bool>(n);
        void Ext(double x, double y) { if (x < v.X0) v.X0 = x; if (x > v.X1) v.X1 = x; if (y < v.Y0) v.Y0 = y; if (y > v.Y1) v.Y1 = y; }
        foreach (var p in g.Prims)
        {
            int m = p.Xs.Length;
            var pts = new (double X, double Y)[m];
            for (int i = 0; i < m; i++) { pts[i] = l.ToModel(p.Xs[i], p.Ys[i]); Ext(pts[i].X, pts[i].Y); }
            // điểm nằm trong cung (không phải đầu nét)
            bool[] inArc = new bool[m + 1]; bool[] segArc = new bool[m + 1];
            if (p.IsCircle) { for (int i = 0; i <= m; i++) { inArc[i] = true; segArc[i] = true; } }
            else if (p.Arcs != null)
                foreach (var a in p.Arcs)
                {
                    for (int k = a.I0; k < a.I1 && k <= m; k++) segArc[k] = true;
                    for (int k = a.I0 + 1; k < a.I1 && k <= m; k++) inArc[k] = true;
                    // trung điểm cung
                    int i0 = Math.Clamp(a.I0, 0, m - 1), i1 = a.I1 >= m ? 0 : Math.Clamp(a.I1, 0, m - 1);
                    var mc = l.ToModel(a.Cx, a.Cy);
                    double s = l.Scale > 0 ? l.Scale : 1, r = a.R * s;
                    double t0 = Math.Atan2(pts[i0].Y - mc.Y, pts[i0].X - mc.X);
                    int im = Math.Clamp((a.I0 + Math.Min(a.I1, m)) / 2, 0, m - 1);
                    double tm = Math.Atan2(pts[im].Y - mc.Y, pts[im].X - mc.X);
                    double t1 = Math.Atan2(pts[i1].Y - mc.Y, pts[i1].X - mc.X);
                    // góc giữa theo chiều đi qua điểm giữa danh sách
                    double sweep = Norm(t1 - t0), viaMid = Norm(tm - t0);
                    double mid = viaMid <= sweep ? t0 + sweep / 2 : t0 - (2 * Math.PI - sweep) / 2;
                    if (a.I1 - a.I0 >= 2) v.Points.Add((mc.X + r * Math.Cos(mid), mc.Y + r * Math.Sin(mid), p.Layer, CadPointKind.Mid));
                    v.Points.Add((mc.X, mc.Y, p.Layer, CadPointKind.Center));
                }
            if (p.IsCircle)
            {
                var c = l.ToModel(p.Cx, p.Cy);
                v.Points.Add((c.X, c.Y, p.Layer, CadPointKind.Center));
            }
            for (int i = 0; i < m; i++)
                if (!inArc[i]) v.Points.Add((pts[i].X, pts[i].Y, p.Layer, CadPointKind.End));
            int ns = m - 1 + (p.Closed ? 1 : 0);
            for (int k = 0; k < ns; k++)
            {
                var a = pts[k]; var b = pts[(k + 1) % m];
                seg.Add(a.X); seg.Add(a.Y); seg.Add(b.X); seg.Add(b.Y);
                lay.Add(p.Layer); arc.Add(segArc[k]);
            }
        }
        foreach (var t in g.Texts)
        {
            var q = l.ToModel(t.X, t.Y);
            double s = l.Scale > 0 ? l.Scale : 1;
            v.Texts.Add((q.X, q.Y, t.Height * s, t.Layer, t.Text));
            Ext(q.X, q.Y);
        }
        v.Seg = seg.ToArray(); v.SegLayer = lay.ToArray(); v.SegArc = arc.ToArray(); v.SegCount = lay.Count;
        // lưới băm: ô ≈ phạm vi / 200, trong khoảng 0,25 … 20 m
        double span = Math.Max(v.X1 - v.X0, v.Y1 - v.Y0);
        v.Cell = double.IsFinite(span) && span > 0 ? Math.Clamp(span / 200, 0.25, 20) : 1;
        for (int i = 0; i < v.SegCount; i++)
        {
            double x1 = v.Seg[4 * i], y1 = v.Seg[4 * i + 1], x2 = v.Seg[4 * i + 2], y2 = v.Seg[4 * i + 3];
            int cx0 = v.C(Math.Min(x1, x2)), cx1 = v.C(Math.Max(x1, x2)), cy0 = v.C(Math.Min(y1, y2)), cy1 = v.C(Math.Max(y1, y2));
            if ((long)(cx1 - cx0 + 1) * (cy1 - cy0 + 1) > 4096)
            {
                // đoạn rất dài: chỉ ghi các ô dọc theo đoạn
                double L = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                int steps = (int)Math.Min(100_000, Math.Ceiling(L / (v.Cell * 0.5)) + 1);
                long last = long.MinValue;
                for (int k = 0; k <= steps; k++)
                {
                    double t = (double)k / steps;
                    long kk = v.Key2(v.C(x1 + t * (x2 - x1)), v.C(y1 + t * (y2 - y1)));
                    if (kk == last) continue;
                    last = kk; v.AddCell(v._segCells, kk, i);
                }
                continue;
            }
            for (int cx = cx0; cx <= cx1; cx++)
                for (int cy = cy0; cy <= cy1; cy++) v.AddCell(v._segCells, v.Key2(cx, cy), i);
        }
        for (int i = 0; i < v.Points.Count; i++) v.AddCell(v._ptCells, v.Key2(v.C(v.Points[i].X), v.C(v.Points[i].Y)), i);
        return v;
    }

    private static double Norm(double a) { a %= 2 * Math.PI; return a < 0 ? a + 2 * Math.PI : a; }

    private void AddCell(Dictionary<long, List<int>> d, long k, int i)
    {
        if (!d.TryGetValue(k, out var lst)) d[k] = lst = new List<int>(4);
        if (lst.Count == 0 || lst[^1] != i) lst.Add(i);
    }

    /// <summary>Các đoạn có thể nằm trong hình vuông tâm (x, y) cạnh 2·r (tối đa max đoạn).</summary>
    public List<int> SegmentsNear(double x, double y, double r, Func<int, bool> layerVisible = null, int max = MaxSegsPerQuery)
    {
        var res = new List<int>(); var seen = new HashSet<int>();
        int cx0 = C(x - r), cx1 = C(x + r), cy0 = C(y - r), cy1 = C(y + r);
        for (int cx = cx0; cx <= cx1; cx++)
            for (int cy = cy0; cy <= cy1; cy++)
            {
                if (!_segCells.TryGetValue(Key2(cx, cy), out var lst)) continue;
                foreach (int i in lst)
                {
                    if (!seen.Add(i)) continue;
                    if (layerVisible != null && !layerVisible(SegLayer[i])) continue;
                    if (DistToSeg(i, x, y) > r) continue;
                    res.Add(i);
                    if (res.Count >= max) return res;
                }
            }
        return res;
    }

    public List<int> PointsNear(double x, double y, double r, Func<int, bool> layerVisible = null)
    {
        var res = new List<int>();
        int cx0 = C(x - r), cx1 = C(x + r), cy0 = C(y - r), cy1 = C(y + r);
        for (int cx = cx0; cx <= cx1; cx++)
            for (int cy = cy0; cy <= cy1; cy++)
            {
                if (!_ptCells.TryGetValue(Key2(cx, cy), out var lst)) continue;
                foreach (int i in lst)
                {
                    var p = Points[i];
                    if (layerVisible != null && !layerVisible(p.Layer)) continue;
                    if (Math.Abs(p.X - x) <= r && Math.Abs(p.Y - y) <= r) res.Add(i);
                }
            }
        return res;
    }

    public double DistToSeg(int i, double x, double y)
    {
        double ax = Seg[4 * i], ay = Seg[4 * i + 1], bx = Seg[4 * i + 2], by = Seg[4 * i + 3];
        double dx = bx - ax, dy = by - ay, L2 = dx * dx + dy * dy;
        double t = L2 < 1e-18 ? 0 : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / L2, 0, 1);
        double qx = ax + t * dx - x, qy = ay + t * dy - y;
        return Math.Sqrt(qx * qx + qy * qy);
    }

    /// <summary>Giao điểm hai đoạn (kể cả chạm đầu mút trong dung sai nhỏ).</summary>
    public bool Intersect(int i, int j, out double x, out double y)
    {
        x = y = 0;
        double ax = Seg[4 * i], ay = Seg[4 * i + 1], bx = Seg[4 * i + 2], by = Seg[4 * i + 3];
        double cx = Seg[4 * j], cy = Seg[4 * j + 1], dx = Seg[4 * j + 2], dy = Seg[4 * j + 3];
        double d1x = bx - ax, d1y = by - ay, d2x = dx - cx, d2y = dy - cy;
        double den = d1x * d2y - d1y * d2x;
        double l1 = Math.Sqrt(d1x * d1x + d1y * d1y), l2 = Math.Sqrt(d2x * d2x + d2y * d2y);
        if (l1 < 1e-9 || l2 < 1e-9 || Math.Abs(den) < 1e-9 * l1 * l2) return false;
        double t = ((cx - ax) * d2y - (cy - ay) * d2x) / den;
        double u = ((cx - ax) * d1y - (cy - ay) * d1x) / den;
        double e1 = 1e-6 / l1, e2 = 1e-6 / l2;
        if (t < -e1 || t > 1 + e1 || u < -e2 || u > 1 + e2) return false;
        x = ax + t * d1x; y = ay + t * d1y;
        return true;
    }
}

/// <summary>Bộ nhớ đệm hình học bản vẽ liên kết theo CadLink.Id (dùng chung cho mọi bản sao Project – hoàn tác không làm mất).</summary>
public static class CadLinkCache
{
    private static readonly ConcurrentDictionary<string, CadLinkGeometry> _byId = new();

    public static CadLinkGeometry Get(CadLink l) => l != null && _byId.TryGetValue(l.Id ?? "", out var g) ? g : null;
    public static CadLinkGeometry Get(string id) => id != null && _byId.TryGetValue(id, out var g) ? g : null;
    public static void Set(CadLink l, CadLinkGeometry g) { if (l != null && g != null) _byId[l.Id ?? ""] = g; }
    public static void Remove(CadLink l) { if (l != null) _byId.TryRemove(l.Id ?? "", out _); }
    public static void Clear() => _byId.Clear();

    /// <summary>Nhận hình mới đọc: ghi thông tin file vào link, trả true nếu nội dung đổi so với lần đọc trước (mã băm khác).</summary>
    public static bool Accept(CadLink l, CadLinkGeometry g)
    {
        if (l == null || g == null) return false;
        bool first = string.IsNullOrEmpty(l.LastHash);
        bool changed = !first && !string.Equals(l.LastHash, g.Hash, StringComparison.OrdinalIgnoreCase);
        l.LastHash = g.Hash; l.LastWriteUtc = g.WriteUtc == default ? l.LastWriteUtc : g.WriteUtc; l.LastLength = g.Length;
        l.InsUnits = g.InsUnits; l.DetectedUnitScale = g.UnitScale;
        if (!string.IsNullOrEmpty(g.Path)) l.FilePath = g.Path;
        if (string.IsNullOrWhiteSpace(l.Name) && !string.IsNullOrEmpty(g.Path)) l.Name = Path.GetFileName(g.Path);
        // lần đầu: ẩn sẵn các layer đang tắt trong AutoCAD
        l.HiddenLayers ??= new List<string>();
        if (first && l.HiddenLayers.Count == 0 && g.OffLayers.Count > 0) l.HiddenLayers.AddRange(g.OffLayers);
        Set(l, g);
        return changed;
    }

    /// <summary>File trên đĩa khác lần đọc trước (thời điểm ghi / kích thước) – kiểm tra nhanh, chưa tính mã băm.</summary>
    public static bool LooksChanged(CadLink l, string path)
    {
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) return false;
            return l.LastWriteUtc == null || fi.LastWriteTimeUtc != l.LastWriteUtc.Value || fi.Length != l.LastLength;
        }
        catch (Exception) { return false; }
    }

    /// <summary>Kết quả đọc (lại) bản vẽ liên kết.</summary>
    public sealed class ReloadResult
    {
        public bool Ok;
        public bool Missing;
        public bool Changed;
        public string Path = "";
        public string Error = "";
        public CadLinkGeometry Geometry;
        public override string ToString() => Ok ? (Changed ? "Bản vẽ link đã thay đổi – đã cập nhật" : "Đã đọc bản vẽ link") : Error;
    }

    /// <summary>Đọc (lại) đồng bộ: tìm file (đường dẫn tương đối theo dự án trước), đọc, so mã băm, ghi vào bộ đệm.
    /// force = false và file không đổi (thời điểm ghi, kích thước, đơn vị) → dùng hình đã có.</summary>
    public static ReloadResult Reload(CadLink l, string projectPath, bool force = false, int retries = 0, int retryDelayMs = 250)
    {
        var res = new ReloadResult();
        string path = l?.Resolve(projectPath);
        if (path == null) { res.Missing = true; res.Error = $"Không tìm thấy bản vẽ link – chọn lại: {l?.FilePath}"; return res; }
        res.Path = path;
        var old = Get(l);
        if (!force && old != null && string.Equals(old.Path, path, StringComparison.OrdinalIgnoreCase) && Math.Abs(old.RequestedUnitScale - l.UnitScale) < 1e-12 && !LooksChanged(l, path))
        { res.Ok = true; res.Geometry = old; return res; }
        for (int i = 0; ; i++)
        {
            try
            {
                var g = CadLinkGeometry.Load(path, l.UnitScale);
                res.Changed = Accept(l, g);
                res.Geometry = g; res.Ok = true;
                return res;
            }
            catch (IOException ex) when (ex is not FileNotFoundException && i < retries)
            {
                Thread.Sleep(retryDelayMs);   // AutoCAD đang ghi file – chờ rồi đọc lại
            }
            catch (Exception ex)
            {
                res.Error = ex.Message;
                return res;
            }
        }
    }
}

/// <summary>Bắt điểm vào nét bản vẽ liên kết (đầu nét, trung điểm, giao điểm, tâm) – nguồn bổ sung cho BeamTools.SnapCandidates.</summary>
public static class CadLinkSnap
{
    /// <summary>Các bản vẽ liên kết hiện trên tầng, có bắt điểm và đã nạp hình.</summary>
    public static IEnumerable<(CadLink link, CadLinkGeometry g)> Active(Project p, string story) => Active(p?.CadLinks, story, false);

    /// <summary>ignoreFlags = true: bỏ qua công tắc Hiện / Bắt điểm / tầng của link (dùng khi căn bản vẽ).</summary>
    public static IEnumerable<(CadLink link, CadLinkGeometry g)> Active(IEnumerable<CadLink> links, string story, bool ignoreFlags)
    {
        if (links == null) yield break;
        foreach (var l in links)
        {
            if (l == null) continue;
            if (!ignoreFlags && (!l.Snap || !l.ShownOn(story) || l.HalftoneOn(story))) continue;   // [Xref theo tầng] mờ = chỉ tham chiếu, không bắt điểm
            var g = CadLinkCache.Get(l);
            if (g != null) yield return (l, g);
        }
    }

    public static Func<int, bool> LayerFilter(CadLink l, CadLinkGeometry g)
    {
        if (l.HiddenLayers == null || l.HiddenLayers.Count == 0) return null;
        var hidden = new bool[g.Layers.Count];
        var hs = new HashSet<string>(l.HiddenLayers, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < g.Layers.Count; i++) hidden[i] = hs.Contains(g.Layers[i]);
        return i => i < 0 || i >= hidden.Length || !hidden[i];
    }

    /// <summary>Điểm bắt trên nét bản vẽ liên kết trong bán kính tol quanh (x, y). Ưu tiên: đầu nét/tâm 2, giao nét 3, trung điểm 4
    /// (sau đầu dầm/nút của mô hình). mask: End|Node → đầu nét, tâm; Intersection → giao nét; Midpoint → trung điểm.</summary>
    public static List<SnapCandidate> Candidates(Project p, string story, double x, double y, double tol, SnapMask mask = SnapMask.All) =>
        Candidates(Active(p, story), x, y, tol, mask);

    /// <summary>Điểm bắt trên các bản vẽ cho trước (đã lọc).</summary>
    public static List<SnapCandidate> Candidates(IEnumerable<(CadLink link, CadLinkGeometry g)> active, double x, double y, double tol, SnapMask mask = SnapMask.All)
    {
        var list = new List<SnapCandidate>();
        if ((mask & SnapMask.CadLink) == 0) return list;
        bool ends = (mask & (SnapMask.End | SnapMask.Node)) != 0, mids = (mask & SnapMask.Midpoint) != 0, inter = (mask & SnapMask.Intersection) != 0;
        foreach (var (l, g) in active ?? Enumerable.Empty<(CadLink, CadLinkGeometry)>())
        {
            var v = g.View(l);
            var vis = LayerFilter(l, g);
            void Add(SnapKind k, int pri, double px, double py)
            {
                double d = Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d <= tol) list.Add(new SnapCandidate { Kind = k, X = Math.Round(px, 4), Y = Math.Round(py, 4), Dist = d, Priority = pri });
            }
            if (ends || mids)
                foreach (int i in v.PointsNear(x, y, tol, vis))
                {
                    var q = v.Points[i];
                    if (q.Kind == CadPointKind.End && ends) Add(SnapKind.CadEnd, 2, q.X, q.Y);
                    else if (q.Kind == CadPointKind.Center && ends) Add(SnapKind.CadCenter, 2, q.X, q.Y);
                    else if (q.Kind == CadPointKind.Mid && mids) Add(SnapKind.CadMid, 4, q.X, q.Y);
                }
            if (!mids && !inter) continue;
            var segs = v.SegmentsNear(x, y, tol, vis);
            if (mids)
                foreach (int i in segs)
                    if (!v.SegArc[i]) Add(SnapKind.CadMid, 4, (v.Seg[4 * i] + v.Seg[4 * i + 2]) / 2, (v.Seg[4 * i + 1] + v.Seg[4 * i + 3]) / 2);
            if (inter)
            {
                int pairs = 0;
                for (int a = 0; a < segs.Count && pairs < CadLinkView.MaxPairsPerQuery; a++)
                    for (int b = a + 1; b < segs.Count && pairs < CadLinkView.MaxPairsPerQuery; b++, pairs++)
                        if (v.Intersect(segs[a], segs[b], out double ix, out double iy)) Add(SnapKind.CadIntersection, 3, ix, iy);
            }
        }
        return list;
    }

    /// <summary>Điểm bắt tốt nhất trên bản vẽ liên kết; null nếu không có.</summary>
    public static SnapCandidate Best(Project p, string story, double x, double y, double tol, SnapMask mask = SnapMask.All) =>
        Candidates(p, story, x, y, tol, mask).OrderBy(c => c.Priority).ThenBy(c => c.Dist).FirstOrDefault();
}
