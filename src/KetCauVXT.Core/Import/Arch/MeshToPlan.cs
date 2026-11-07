using System.Globalization;
using System.Text;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Arch;

// =====================================================================
// MÔ HÌNH 3D SKETCHUP / REVIT (OBJ, DAE, *.vxtmesh.json) → MẶT BẰNG CÁC TẦNG → TRIỂN KHAI KẾT CẤU
//   1. Đọc lưới tam giác, tự nhận trục đứng (SketchUp xuất OBJ thường "Y lên"), đơn vị.
//   2. Tìm cao độ sàn: các mặt nằm ngang hướng lên có tổng diện tích lớn (≥ 25 % mặt lớn nhất).
//   3. Cắt ngang mỗi tầng ở +1,2 m → nét mặt tường (layer TUONG); vòng kín nhỏ (≤ 0,8 m) → cột (layer COT).
//      Khe hở trên tường ở +1,2 m mà ở +2,6 m vẫn có tường → ô cửa: ở +0,3 m cũng hở → cửa đi, còn tường → cửa sổ (layer CUA).
//   4. Ghi DXF (mm) mỗi tầng một hình "MẶT BẰNG TẦNG n" đặt cạnh nhau → hộp thoại "Nhập bản vẽ kiến trúc" đọc như bản vẽ CAD
//      (tường, cột, cửa, lưới trục tự lập, phòng) rồi đề xuất kết cấu – cùng quy trình với file DWG/DXF.
// Mọi xử lý trên máy, không gửi dữ liệu đi đâu.
// =====================================================================
public sealed class RawMesh
{
    public List<double[]> V { get; } = new();
    public List<int[]> F { get; } = new();
}

public sealed class MeshPlanOptions
{
    /// <summary>Hệ số đổi đơn vị → m (0 = tự nhận theo kích thước: &gt; 2000 → mm, &gt; 200 → cm, còn lại m). DAE dùng &lt;unit&gt; của file.</summary>
    public double Scale { get; set; }
    /// <summary>Trục đứng: 0 = tự nhận, 1 = Y lên (SketchUp OBJ mặc định), 2 = Z lên</summary>
    public int UpAxis { get; set; }
    /// <summary>Cao độ cắt mặt bằng trên mặt sàn (m)</summary>
    public double CutHeight { get; set; } = 1.2;
    /// <summary>Diện tích mặt ngang nhỏ nhất để coi là sàn (m²)</summary>
    public double MinFloorArea { get; set; } = 6;
}

public sealed class MeshPlanResult
{
    /// <summary>Cao độ mặt sàn các tầng (m, gốc = điểm thấp nhất mô hình), phần tử cuối = mái</summary>
    public List<double> Levels { get; } = new();
    public List<DxfEntity> Entities { get; } = new();
    public List<string> Info { get; } = new();
    public int Walls, Columns, Doors, Windows;
    /// <summary>Chiều cao các tầng (m)</summary>
    public List<double> Heights => Levels.Zip(Levels.Skip(1), (a, b) => Math.Round(b - a, 3)).ToList();
}

public static class MeshToPlan
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Đọc file mô hình (không dời gốc). OBJ: scale theo tuỳ chọn; DAE: đơn vị trong file; *.json: lưới add-in Revit (m).</summary>
    public static RawMesh Read(string path, MeshPlanOptions o)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        string text = File.ReadAllText(path);
        LibraryItem it = ext switch
        {
            ".obj" => Library3D.ReadObj(text, "m", 1.0),
            ".dae" => Library3D.ReadDae(text, "m"),
            _ => Library3D.ReadRevitMesh(text, "m")
        };
        var m = new RawMesh();
        m.V.AddRange(it.Vertices.Select(v => new[] { v[0], v[1], v[2] }));
        m.F.AddRange(it.Faces);
        return Prepare(m, o, ext == ".obj");
    }

    /// <summary>Đổi trục đứng về Z, đơn vị về m, gốc z = điểm thấp nhất.</summary>
    public static RawMesh Prepare(RawMesh m, MeshPlanOptions o, bool autoScale = true)
    {
        o ??= new MeshPlanOptions();
        int up = o.UpAxis == 0 ? GuessUpAxis(m) : o.UpAxis;
        if (up == 1) foreach (var v in m.V) { double y = v[1], z = v[2]; v[1] = -z; v[2] = y; }
        double k = o.Scale;
        if (k <= 0 && autoScale && m.V.Count > 0)
        {
            double ext = new[] { 0, 1, 2 }.Max(i => m.V.Max(v => v[i]) - m.V.Min(v => v[i]));
            k = ext > 2000 ? 0.001 : ext > 200 ? 0.01 : 1;
        }
        if (k > 0 && Math.Abs(k - 1) > 1e-12) foreach (var v in m.V) { v[0] *= k; v[1] *= k; v[2] *= k; }
        if (m.V.Count > 0) { double z0 = m.V.Min(v => v[2]); foreach (var v in m.V) v[2] -= z0; }
        return m;
    }

    private static Vec3 P(RawMesh m, int i) => new(m.V[i][0], m.V[i][1], m.V[i][2]);

    /// <summary>Trục đứng (1 = Y, 2 = Z): nhà có ≥ 3 mặt sàn lớn hướng lên (sàn các tầng + mái) phủ gần kín mặt bằng, còn theo phương ngang
    /// chỉ có 2 mặt tường lớn (mặt ngoài tường này, mặt trong tường đối diện). Điểm = Σ diện tích các mức lớn / diện tích hộp bao vuông góc.
    /// Hoà (±0,1) → Z.</summary>
    public static int GuessUpAxis(RawMesh m)
    {
        double Score(int ax)
        {
            var lv = UpLevels(m, ax, 1.0);
            int o1 = ax == 2 ? 0 : 0, o2 = ax == 2 ? 1 : 2;
            double e1 = m.V.Max(v => v[o1]) - m.V.Min(v => v[o1]), e2 = m.V.Max(v => v[o2]) - m.V.Min(v => v[o2]);
            double box = Math.Max(1e-9, e1 * e2);
            return lv.Sum(l => Math.Min(1.0, l.area / box));
        }
        if (m.V.Count == 0) return 2;
        double sz = Score(2), sy = Score(1);
        return sy > sz + 0.1 ? 1 : 2;
    }

    /// <summary>Các mức mặt vuông góc trục ax hướng dương (gộp 0,05 m; giữ ≥ max(minArea, 25 % mức lớn nhất); cách nhau &lt; 1,8 m gộp).</summary>
    private static List<(double z, double area)> UpLevels(RawMesh m, int ax, double minArea)
    {
        var groups = new Dictionary<long, (double area, double zsum)>();
        foreach (var f in m.F)
        {
            var a = P(m, f[0]); var b = P(m, f[1]); var c = P(m, f[2]);
            var n = (b - a).Cross(c - a);
            double area = n.Length / 2;
            double comp = ax == 2 ? n.Z : n.Y;
            if (area < 1e-9 || comp / n.Length < 0.95) continue;
            double z = ax == 2 ? (a.Z + b.Z + c.Z) / 3 : (a.Y + b.Y + c.Y) / 3;
            long key = (long)Math.Round(z / 0.05);
            var g = groups.GetValueOrDefault(key);
            groups[key] = (g.area + area, g.zsum + z * area);
        }
        if (groups.Count == 0) return new List<(double, double)>();
        double max = groups.Values.Max(g => g.area);
        var cand = groups.Values.Where(g => g.area >= Math.Max(minArea, 0.25 * max)).Select(g => (z: g.zsum / g.area, g.area)).OrderBy(g => g.z).ToList();
        var res = new List<(double z, double area)>();
        foreach (var c in cand)
        {
            if (res.Count > 0 && c.z - res[^1].z < 1.8) { if (c.area > res[^1].area) res[^1] = c; continue; }
            res.Add(c);
        }
        return res;
    }

    /// <summary>Cao độ mặt sàn: mặt ngang hướng lên, gộp theo cao độ 0,05 m, giữ nhóm có diện tích ≥ max(MinFloorArea, 25 % nhóm lớn nhất);
    /// các nhóm cách nhau &lt; 1,8 m gộp (lấy nhóm diện tích lớn hơn).</summary>
    public static List<double> DetectLevels(RawMesh m, MeshPlanOptions o) =>
        UpLevels(m, 2, (o ?? new MeshPlanOptions()).MinFloorArea).Select(r => Math.Round(r.z, 3)).ToList();

    /// <summary>Cắt lưới bằng mặt phẳng ngang z → các đoạn thẳng (x1, y1, x2, y2).</summary>
    public static List<(double X1, double Y1, double X2, double Y2)> Slice(RawMesh m, double z)
    {
        z += 1e-5;
        var res = new List<(double, double, double, double)>();
        foreach (var f in m.F)
        {
            var a = P(m, f[0]); var b = P(m, f[1]); var c = P(m, f[2]);
            var pts = new List<(double, double)>();
            void Edge(Vec3 p, Vec3 q)
            {
                if ((p.Z - z) * (q.Z - z) >= 0) return;
                double t = (z - p.Z) / (q.Z - p.Z);
                pts.Add((p.X + t * (q.X - p.X), p.Y + t * (q.Y - p.Y)));
            }
            Edge(a, b); Edge(b, c); Edge(c, a);
            if (pts.Count == 2 && Math.Abs(pts[0].Item1 - pts[1].Item1) + Math.Abs(pts[0].Item2 - pts[1].Item2) > 1e-6)
                res.Add((pts[0].Item1, pts[0].Item2, pts[1].Item1, pts[1].Item2));
        }
        return res;
    }

    /// <summary>Đường thẳng chứa đoạn (góc 0…π, khoảng cách có dấu) và khoảng (t0, t1) trên đường.</summary>
    private sealed class LineGroup
    {
        public double Ang, Off;
        public List<(double T0, double T1)> Spans = new();
        public (double X, double Y) At(double t) => (Math.Cos(Ang) * t - Math.Sin(Ang) * Off, Math.Sin(Ang) * t + Math.Cos(Ang) * Off);
    }

    private static List<LineGroup> Merge(IEnumerable<(double X1, double Y1, double X2, double Y2)> segs)
    {
        var groups = new List<LineGroup>();
        foreach (var s in segs)
        {
            double ang = Math.Atan2(s.Y2 - s.Y1, s.X2 - s.X1);
            if (ang < 0) ang += Math.PI;
            if (ang >= Math.PI - 1e-9) ang -= Math.PI;
            double cs = Math.Cos(ang), sn = Math.Sin(ang);
            double off = -sn * s.X1 + cs * s.Y1;
            double t1 = cs * s.X1 + sn * s.Y1, t2 = cs * s.X2 + sn * s.Y2;
            var g = groups.FirstOrDefault(q => (Math.Abs(q.Ang - ang) < 0.005 || Math.Abs(Math.Abs(q.Ang - ang) - Math.PI) < 0.005) && Math.Abs(q.Off - off) < 0.004);
            if (g == null) { g = new LineGroup { Ang = ang, Off = off }; groups.Add(g); }
            g.Spans.Add((Math.Min(t1, t2), Math.Max(t1, t2)));
        }
        foreach (var g in groups)
        {
            var sp = g.Spans.OrderBy(q => q.T0).ToList();
            var u = new List<(double, double)>();
            foreach (var q in sp)
            {
                if (u.Count > 0 && q.T0 <= u[^1].Item2 + 0.004) u[^1] = (u[^1].Item1, Math.Max(u[^1].Item2, q.T1));
                else u.Add(q);
            }
            g.Spans = u.Where(q => q.Item2 - q.Item1 > 0.01).ToList();
        }
        return groups.Where(g => g.Spans.Count > 0).ToList();
    }

    private static bool Covered(List<LineGroup> gs, LineGroup g, double t) =>
        gs.Any(q => Math.Abs(q.Ang - g.Ang) < 0.005 && Math.Abs(q.Off - g.Off) < 0.01 && q.Spans.Any(s => t >= s.T0 - 0.005 && t <= s.T1 + 0.005));

    /// <summary>Mặt bằng từ mô hình: mỗi tầng (trừ mái trên cùng) một hình, dời theo X để không chồng nhau.</summary>
    public static MeshPlanResult Build(RawMesh m, MeshPlanOptions o)
    {
        o ??= new MeshPlanOptions();
        var res = new MeshPlanResult();
        var levels = DetectLevels(m, o);
        res.Levels.AddRange(levels);
        if (m.V.Count == 0) { res.Info.Add("Mô hình rỗng."); return res; }
        if (levels.Count < 2)
        {
            res.Info.Add($"Chỉ tìm được {levels.Count} mặt sàn – cần ít nhất sàn tầng 1 và sàn mái (mặt ngang hướng lên ≥ {o.MinFloorArea:0.#} m²).");
            return res;
        }
        double xmin = m.V.Min(v => v[0]), xmax = m.V.Max(v => v[0]), ymin = m.V.Min(v => v[1]), ymax = m.V.Max(v => v[1]);
        double W = xmax - xmin, gap = Math.Max(10, W * 0.5);
        for (int k = 0; k + 1 < levels.Count; k++)
        {
            double z = levels[k], h = levels[k + 1] - z, dx = k * (W + gap);
            double zc = z + Math.Min(o.CutHeight, h * 0.45);
            var cut = Merge(Slice(m, zc));
            var low = Merge(Slice(m, z + Math.Min(0.3, h * 0.1)));
            var high = h >= 2.9 ? Merge(Slice(m, z + Math.Min(2.6, h - 0.25))) : new List<LineGroup>();
            // ---- cột: vòng kín 4 đoạn ngắn ----
            var colSeg = new HashSet<(LineGroup, int)>();
            var shortSegs = new List<(LineGroup g, int i, (double X, double Y) a, (double X, double Y) b)>();
            foreach (var g in cut)
                for (int i = 0; i < g.Spans.Count; i++)
                    if (g.Spans[i].T1 - g.Spans[i].T0 <= 0.8) shortSegs.Add((g, i, g.At(g.Spans[i].T0), g.At(g.Spans[i].T1)));
            bool Same((double X, double Y) p, (double X, double Y) q) => Math.Abs(p.X - q.X) < 0.005 && Math.Abs(p.Y - q.Y) < 0.005;
            foreach (var s0 in shortSegs)
            {
                if (colSeg.Contains((s0.g, s0.i))) continue;
                var loop = new List<(double X, double Y)> { s0.a };
                var used = new List<(LineGroup, int)> { (s0.g, s0.i) };
                var cur = s0.b;
                bool closed = false;
                for (int step = 0; step < 6; step++)
                {
                    if (Same(cur, s0.a)) { closed = true; break; }
                    loop.Add(cur);
                    var nx = shortSegs.FirstOrDefault(s => !used.Contains((s.g, s.i)) && (Same(s.a, cur) || Same(s.b, cur)));
                    if (nx.g == null) break;
                    used.Add((nx.g, nx.i));
                    cur = Same(nx.a, cur) ? nx.b : nx.a;
                }
                if (!closed || loop.Count < 3) continue;
                double bx = loop.Max(q => q.X) - loop.Min(q => q.X), by = loop.Max(q => q.Y) - loop.Min(q => q.Y);
                if (Math.Min(bx, by) < 0.15 || Math.Max(bx, by) > 0.8) continue;
                foreach (var u in used) colSeg.Add(u);
                res.Entities.Add(new DxfEntity { Type = DxfEntityType.Polyline, Layer = "COT", Closed = true, Points = loop.Select(q => (q.X + dx, q.Y)).ToList(), SourceType = "LWPOLYLINE" });
                res.Columns++;
            }
            // ---- tường + ô cửa ----
            var openMid = new List<(double X, double Y)>();
            foreach (var g in cut)
            {
                for (int i = 0; i < g.Spans.Count; i++)
                {
                    if (colSeg.Contains((g, i))) continue;
                    var a = g.At(g.Spans[i].T0); var b = g.At(g.Spans[i].T1);
                    res.Entities.Add(new DxfEntity { Type = DxfEntityType.Line, Layer = "TUONG", Points = new() { (a.X + dx, a.Y), (b.X + dx, b.Y) }, SourceType = "LINE" });
                    res.Walls++;
                }
                for (int i = 0; i + 1 < g.Spans.Count; i++)
                {
                    double t0 = g.Spans[i].T1, t1 = g.Spans[i + 1].T0, w = t1 - t0, tm = (t0 + t1) / 2;
                    if (w < 0.5 || w > 3.2) continue;
                    if (high.Count > 0 && !Covered(high, g, tm)) continue;    // hở cả phía trên → hai tường rời, không phải ô cửa
                    var mid = g.At(tm);
                    if (openMid.Any(q => Math.Abs(q.X - mid.X) + Math.Abs(q.Y - mid.Y) < 0.5)) continue;   // mặt tường bên kia của cùng ô cửa
                    openMid.Add(mid);
                    bool door = !Covered(low, g, tm);
                    var a = g.At(t0); var b = g.At(t1);
                    double nxv = -Math.Sin(g.Ang), nyv = Math.Cos(g.Ang);
                    if (door)
                    {
                        // cánh cửa: đa tuyến hở ≥ 4 điểm (nhận là cửa đi)
                        var pts = new List<(double, double)> { (a.X + dx, a.Y), (a.X + nxv * w + dx, a.Y + nyv * w), (b.X + nxv * w * 0.3 + dx, b.Y + nyv * w * 0.3), (b.X + dx, b.Y) };
                        res.Entities.Add(new DxfEntity { Type = DxfEntityType.Polyline, Layer = "CUA", Points = pts, SourceType = "LWPOLYLINE" });
                        res.Doors++;
                    }
                    else
                    {
                        res.Entities.Add(new DxfEntity { Type = DxfEntityType.Line, Layer = "CUA", Points = new() { (a.X + dx, a.Y), (b.X + dx, b.Y) }, SourceType = "LINE" });
                        res.Windows++;
                    }
                }
            }
            res.Entities.Add(new DxfEntity
            {
                Type = DxfEntityType.Text, Layer = "CHU", Text = $"MẶT BẰNG TẦNG {k + 1}", TextHeight = 0.5,
                Points = new() { (xmin + dx, ymin - 2.0) }, SourceType = "TEXT"
            });
            res.Info.Add($"Tầng {k + 1}: sàn +{z:0.000}, cao {h:0.00} m, cắt ở +{zc - z:0.00} m.");
        }
        res.Info.Add($"Mái +{levels[^1]:0.000}. Tổng: {res.Walls} nét tường, {res.Columns} cột, {res.Doors} cửa đi, {res.Windows} cửa sổ.");
        return res;
    }

    /// <summary>Ghi DXF tối giản (R2000, đơn vị mm, $INSUNITS = 4): LINE, LWPOLYLINE, TEXT. Chữ tiếng Việt mã hoá \U+XXXX.</summary>
    public static void WriteDxf(IEnumerable<DxfEntity> ents, string path)
    {
        var sb = new StringBuilder();
        void C(int code, string v) { sb.Append(code).Append('\n').Append(v).Append('\n'); }
        string N(double v) => (v * 1000).ToString("0.###", Inv);
        C(0, "SECTION"); C(2, "HEADER"); C(9, "$ACADVER"); C(1, "AC1015"); C(9, "$INSUNITS"); C(70, "4"); C(0, "ENDSEC");
        C(0, "SECTION"); C(2, "ENTITIES");
        foreach (var e in ents)
        {
            switch (e.Type)
            {
                case DxfEntityType.Line when e.Points.Count >= 2:
                    C(0, "LINE"); C(8, e.Layer); C(10, N(e.Points[0].X)); C(20, N(e.Points[0].Y)); C(30, "0"); C(11, N(e.Points[1].X)); C(21, N(e.Points[1].Y)); C(31, "0");
                    break;
                case DxfEntityType.Polyline when e.Points.Count >= 2:
                    C(0, "LWPOLYLINE"); C(8, e.Layer); C(90, e.Points.Count.ToString(Inv)); C(70, e.Closed ? "1" : "0");
                    foreach (var q in e.Points) { C(10, N(q.X)); C(20, N(q.Y)); }
                    break;
                case DxfEntityType.Text when e.Points.Count >= 1:
                    C(0, "TEXT"); C(8, e.Layer); C(10, N(e.Points[0].X)); C(20, N(e.Points[0].Y)); C(30, "0"); C(40, N(e.TextHeight)); C(1, DxfWriter.Encode(e.Text));
                    break;
            }
        }
        C(0, "ENDSEC"); C(0, "EOF");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
}
