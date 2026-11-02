using System.Globalization;
using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Mặt đứng khung theo một trục (tl 1/100) và biểu đồ nội lực trong mặt phẳng khung.
/// Toạ độ: hoành độ = toạ độ dọc trục (mm), tung độ = cao độ Z (mm).
/// Biểu đồ mô men vẽ về phía thớ chịu kéo (giống tab "Biểu đồ nội lực").</summary>
public static class ElevationDrawing
{
    /// <summary>Tỷ lệ mặc định của chương trình (tỷ lệ dùng thật: <see cref="CurrentScale"/> theo Thiết lập bản vẽ).</summary>
    public const double SheetScale = 100;

    /// <summary>Mẫu số tỷ lệ mặt đứng khung theo Thiết lập bản vẽ (mặc định 100).</summary>
    public static double CurrentScale => DrawingStyle.Current.ScaleOf(DrawingKind.FrameElevation);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Tìm trục theo tên (không phân biệt hoa thường); null → trục X ở giữa.</summary>
    public static GridLine FindGrid(Project p, string name)
    {
        var all = p.GridsX.Concat(p.GridsY).ToList();
        if (all.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(name))
        {
            var g = all.FirstOrDefault(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (g != null) return g;
        }
        var gx = p.GridsX.ToList();
        return gx.Count > 0 ? gx[gx.Count / 2] : all[0];
    }

    /// <summary>Tổ hợp mặc định để vẽ biểu đồ: tên cho trước nếu có, ngược lại "BAO", tổ hợp bao đầu tiên, tổ hợp thiết kế đầu tiên.</summary>
    public static string ResolveCase(Project p, AnalysisResults r, string name)
    {
        if (r == null) return name ?? "";
        if (!string.IsNullOrWhiteSpace(name) && (r.Cases.ContainsKey(name) || r.FindCombo(name) != null)) return name;
        // kết quả nhập từ phần mềm khác: tổ hợp bao TTGH I sinh khi nhập (vd. "BAO-ETABS")
        if (r.External != null && !string.IsNullOrEmpty(r.External.EnvelopeName) && r.FindCombo(r.External.EnvelopeName) != null) return r.External.EnvelopeName;
        if (p.Combinations.Any(c => c.Name == "BAO")) return "BAO";
        var env = p.Combinations.FirstOrDefault(c => c.Type == ComboType.Envelope);
        if (env != null) return env.Name;
        return r.DesignCombos().FirstOrDefault() ?? r.Cases.Keys.FirstOrDefault() ?? "";
    }

    /// <summary>Đại lượng: "M3" (mô men trong mặt phẳng), "V2" (lực cắt trong mặt phẳng), "N" (lực dọc).</summary>
    public static readonly string[] Quantities = { "M3", "V2", "N" };

    /// <summary>Vẽ mặt đứng khung trục g. qty != null và r != null → vẽ biểu đồ nội lực đại lượng qty của tổ hợp caseName.
    /// S ≤ 0: tỷ lệ theo Thiết lập bản vẽ.</summary>
    public static DxfWriter Build(Project p, AnalysisResults r, GridLine g, string qty = null, string caseName = null, double S = 0)
    {
        if (S <= 0) S = CurrentScale;
        var w = new DxfWriter(S);
        double th = w.Th;
        var style = w.Style;
        if (g == null)
        {
            w.Text(Lay.Chu, 0, 0, th, "(Chưa có trục định vị để vẽ mặt đứng)");
            return w;
        }
        p.InvalidateIndex();
        var nodes = p.Nodes.ToDictionary(n => n.Id);
        bool dirX = g.Dir == GridDir.X;               // trục X = đường X = Coord → mặt phẳng Y-Z
        bool InPlane(Node n) => Math.Abs((dirX ? n.X : n.Y) - g.Coord) < 0.01;
        (double H, double V) To2(Node n) => ((dirX ? n.Y : n.X) * 1000, n.Z * 1000);
        Vec3 nrm = dirX ? Vec3.UnitX : Vec3.UnitY;
        Vec3 hv = dirX ? Vec3.UnitY : Vec3.UnitX;
        bool diagram = r != null && !string.IsNullOrEmpty(qty);

        var members = new List<(FrameElement F, Node I, Node J)>();
        foreach (var f in p.Frames)
        {
            if (!nodes.TryGetValue(f.NodeI, out var ni) || !nodes.TryGetValue(f.NodeJ, out var nj)) continue;
            if (!InPlane(ni) || !InPlane(nj)) continue;
            if ((nj.P - ni.P).Length < 1e-6) continue;
            members.Add((f, ni, nj));
        }

        var box = Rect2.Empty;
        foreach (var (_, a, b) in members) { var pa = To2(a); var pb = To2(b); box = box.Union(pa.H, pa.V).Union(pb.H, pb.V); }
        var cross = (dirX ? p.GridsY : p.GridsX).ToList();
        foreach (var cg in cross) box = box.Union(cg.Coord * 1000, box.IsEmpty ? p.BaseElevation * 1000 : box.Cy);
        box = box.Union(box.IsEmpty ? 0 : box.Cx, p.BaseElevation * 1000);
        foreach (var st in p.Stories) box = box.Union(box.Cx, st.Elevation * 1000);
        if (box.IsEmpty) box = new Rect2(0, 0, 1000, 1000);

        // ---- cấu kiện ----
        foreach (var (f, a, b) in members)
        {
            var pa = To2(a); var pb = To2(b);
            if (diagram)
            {
                w.Line(Lay.BeTong, pa.H, pa.V, pb.H, pb.V);
                continue;
            }
            var si = DrawUtil.SectionOf(p, f.Section);
            double dh = pb.H - pa.H, dv = pb.V - pa.V, len = Math.Sqrt(dh * dh + dv * dv);
            double ux = dh / len, uy = dv / len;
            double nx = -uy, ny = ux;                       // pháp tuyến trái
            if (ny < 0 || (Math.Abs(ny) < 1e-9 && nx < 0)) { nx = -nx; ny = -ny; }
            if (f.IsVerticalKind)
            {
                double ang = f.Angle * Math.PI / 180, c = Math.Abs(Math.Cos(ang)), s = Math.Abs(Math.Sin(ang));
                double width = dirX ? c * si.B + s * si.H : c * si.H + s * si.B;
                double hw = width * 500;
                w.Polyline(Lay.BeTong, new List<(double X, double Y)>
                {
                    (pa.H + nx * hw, pa.V + ny * hw), (pb.H + nx * hw, pb.V + ny * hw),
                    (pb.H - nx * hw, pb.V - ny * hw), (pa.H - nx * hw, pa.V - ny * hw)
                }, true);
            }
            else
            {
                double d = si.H * 1000;
                bool top = f.Kind == FrameKind.Beam;      // dầm BTCT: nút tại mặt trên dầm
                double o1 = top ? 0 : d / 2, o2 = top ? -d : -d / 2;
                w.Polyline(Lay.BeTong, new List<(double X, double Y)>
                {
                    (pa.H + nx * o1, pa.V + ny * o1), (pb.H + nx * o1, pb.V + ny * o1),
                    (pb.H + nx * o2, pb.V + ny * o2), (pa.H + nx * o2, pa.V + ny * o2)
                }, true);
            }
        }

        // ---- gối tựa ----
        foreach (var n in p.Nodes.Where(q => q.IsSupport && InPlane(q)))
        {
            var pn = To2(n);
            double a = 0.9 * th;
            w.Polyline(Lay.KyHieu, new List<(double X, double Y)> { (pn.H, pn.V), (pn.H - a, pn.V - 1.4 * a), (pn.H + a, pn.V - 1.4 * a) }, true);
            w.Line(Lay.KyHieu, pn.H - 1.5 * a, pn.V - 1.4 * a, pn.H + 1.5 * a, pn.V - 1.4 * a);
            for (int i = -2; i <= 2; i++)
                w.Line(Lay.KyHieu, pn.H + i * 0.6 * a, pn.V - 1.4 * a, pn.H + i * 0.6 * a - 0.5 * a, pn.V - 1.9 * a);
        }

        // ---- trục cắt ngang + đầu trục + kích thước ----
        double yB = box.Y1 - 5 * th, yT = box.Y2 + 3 * th, r0 = w.BubbleR;
        foreach (var cg in cross)
        {
            double x = cg.Coord * 1000;
            w.Line(Lay.Truc, x, yB, x, yT, "CENTER");
            w.GridBubble(x, yB - r0, cg.Name);
        }
        if (cross.Count > 1 && style.ShowGridDims)
        {
            double yd = yB - 2 * r0 - 2.5 * th;
            w.DimChainH(cross.Select(c => c.Coord * 1000).ToList(), yd, yd);
            if (cross.Count > 2) w.Dim(cross[0].Coord * 1000, yd - 2.5 * th, cross[^1].Coord * 1000, yd - 2.5 * th, 0);
        }

        // ---- cao độ tầng ----
        double xL = box.X1 - 4 * th;
        var levels = new List<(double Z, string Name)> { (p.BaseElevation, "Chân cột") };
        foreach (var st in p.Stories.OrderBy(x => x.Elevation))
            if (Math.Abs(st.Elevation - p.BaseElevation) > 1e-6) levels.Add((st.Elevation, st.Name));
        foreach (var (z, name) in levels)
        {
            double y = z * 1000;
            if (!style.ShowElevationMarks) break;
            w.Line(Lay.KyHieu, xL - 0.5 * th, y, box.X1 - 0.8 * th, y);
            w.ElevMark(xL, y, DxfWriter.ElevText(z), -1);
            w.Text(Lay.Chu, xL - 0.7 * th, y - 0.4 * th, 0.8 * th, name, 0, 2, 3);
        }
        if (levels.Count > 1)
            w.DimChainV(levels.Select(l => l.Z * 1000).ToList(), box.X2 + 2 * th, box.X2 + 4 * th);

        // ---- biểu đồ ----
        string qs = (qty ?? "").ToUpperInvariant();
        int comp = qs.StartsWith("V") ? 1 : qs.StartsWith("N") || qs.StartsWith("P") ? 0 : 5;
        string cs = diagram ? ResolveCase(p, r, caseName) : "";
        bool env = r != null ? r.IsEnvelope(cs) : p.Combinations.Any(c => c.Name == cs && c.Type == ComboType.Envelope);
        double maxAbs = 0;
        string unit = comp == 5 ? "kN.m" : "kN";
        if (diagram && !string.IsNullOrEmpty(cs))
        {
            var items = new List<((double X, double Y)[] Base, double[][] S, double Ux, double Uy, double Sigma)>();
            var lengths = new List<double>();
            foreach (var (f, a, b) in members)
            {
                var pa = To2(a); var pb = To2(b);
                lengths.Add(Math.Sqrt((pb.H - pa.H) * (pb.H - pa.H) + (pb.V - pa.V) * (pb.V - pa.V)));
                FrameForces mx = null, mn = null;
                try
                {
                    if (env) (mx, mn) = r.Envelope(f.Id, r.ExpandCombos(cs));
                    else if (r.Get(cs).Frames.TryGetValue(f.Id, out var ff)) mx = ff;
                }
                catch (Exception) { mx = null; }
                if (mx == null || mx.Count < 2 || (mn != null && mn.Count != mx.Count)) continue;
                var R = ElementMatrices.LocalAxes(a.P, b.P, f.Angle);
                var e1 = new Vec3(R[0, 0], R[0, 1], R[0, 2]);
                var e2 = new Vec3(R[1, 0], R[1, 1], R[1, 2]);
                var e3 = new Vec3(R[2, 0], R[2, 1], R[2, 2]);
                Vec3 u; double c2 = 0, c3 = 0;
                if (comp == 5) { c2 = e2.Dot(nrm); c3 = e3.Dot(nrm); u = e1.Cross(nrm); }
                else if (comp == 1) { u = nrm.Cross(e1); c2 = e2.Dot(u); c3 = e3.Dot(u); }
                else { u = nrm.Cross(e1); if (Math.Abs(u.Z) > 0.5 ? u.Z < 0 : u.Dot(hv) < 0) u = u * -1; }
                double ux = u.Dot(hv), uy = u.Z, ul = Math.Sqrt(ux * ux + uy * uy);
                if (ul < 1e-12) continue;
                ux /= ul; uy /= ul;
                double sigma = comp == 0 ? 1 : Math.Abs(c3) >= Math.Abs(c2) ? (c3 >= 0 ? 1 : -1) : (c2 >= 0 ? 1 : -1);
                double s0 = mx.Stations[0], s1 = mx.Stations[^1];
                var bs = new (double X, double Y)[mx.Count];
                for (int i = 0; i < mx.Count; i++)
                {
                    double t = s1 - s0 > 1e-12 ? (mx.Stations[i] - s0) / (s1 - s0) : (double)i / (mx.Count - 1);
                    bs[i] = (pa.H + (pb.H - pa.H) * t, pa.V + (pb.V - pa.V) * t);
                }
                var curves = (mn != null ? new[] { mx, mn } : new[] { mx }).Select(cf => Enumerable.Range(0, cf.Count).Select(i =>
                    comp == 5 ? cf.M2(i) * c2 + cf.M3(i) * c3 :
                    comp == 1 ? cf.V2(i) * c2 + cf.V3(i) * c3 : cf.P(i)).ToArray()).ToArray();
                foreach (var c in curves) foreach (var v in c) maxAbs = Math.Max(maxAbs, Math.Abs(v));
                items.Add((bs, curves, ux, uy, sigma));
            }
            if (maxAbs > 1e-9 && lengths.Count > 0)
            {
                lengths.Sort();
                double span = Math.Max(lengths[lengths.Count / 2], 500);
                double k = 0.25 * span / maxAbs;           // mm / đơn vị nội lực
                double tv = 0.8 * th;
                foreach (var it in items)
                {
                    int n = it.Base.Length;
                    foreach (var s in it.S)
                    {
                        var pts = new List<(double X, double Y)> { it.Base[0] };
                        for (int i = 0; i < n; i++)
                        {
                            var tip = (it.Base[i].X + it.Ux * s[i] * k, it.Base[i].Y + it.Uy * s[i] * k);
                            pts.Add(tip);
                            if (Math.Abs(s[i]) * k > 0.3 * th) w.Line(Lay.KichThuoc, it.Base[i].X, it.Base[i].Y, tip.Item1, tip.Item2);
                        }
                        pts.Add(it.Base[n - 1]);
                        w.Polyline(Lay.Thep, pts, false);
                        int iExt = 0;
                        for (int i = 1; i < n; i++) if (Math.Abs(s[i]) > Math.Abs(s[iExt])) iExt = i;
                        foreach (int i in new[] { 0, n - 1, iExt }.Distinct())
                        {
                            if (!style.ShowLoadValues) break;   // tắt giá trị nội lực (Thiết lập bản vẽ)
                            if (Math.Abs(s[i]) < 1e-3 * Math.Max(1, maxAbs) || Math.Abs(s[i]) < 0.05) continue;
                            double v = s[i] * it.Sigma;
                            double sg = s[i] >= 0 ? 1 : -1;
                            double lx = it.Base[i].X + it.Ux * (s[i] * k + sg * 0.9 * tv), ly = it.Base[i].Y + it.Uy * (s[i] * k + sg * 0.9 * tv);
                            w.Text(Lay.Chu, lx, ly, tv, Fmt(v), 0, 1, 2);
                        }
                    }
                }
            }
        }

        // ---- tên hình ----
        string title;
        if (diagram)
        {
            string what = comp == 5 ? "BIỂU ĐỒ MÔ MEN M" : comp == 1 ? "BIỂU ĐỒ LỰC CẮT Q" : "BIỂU ĐỒ LỰC DỌC N";
            title = $"{what} ({unit}) KHUNG TRỤC {g.Name} – {cs}{(env ? " (BAO)" : "")}";
        }
        else title = $"MẶT ĐỨNG KHUNG TRỤC {g.Name}";
        double cx = w.Extents.IsEmpty ? 0 : (box.X1 + box.X2) / 2;
        double yb = w.Title(cx, w.Extents.Y2 + 4 * th, title, DrawUtil.ScaleText(S));
        if (diagram && (style.ShowLoadValues || maxAbs <= 0))
        {
            string note = maxAbs > 0 ? $"Giá trị lớn nhất |{(comp == 5 ? "M" : comp == 1 ? "Q" : "N")}| = {Fmt(maxAbs)} {unit}" + (comp == 5 ? " – vẽ về phía thớ chịu kéo" : "")
                : "(Chưa có kết quả nội lực – chạy phân tích)";
            w.Text(Lay.Chu, cx, yb - 0.2 * th, 0.8 * th, note, 0, 1, 3);
        }
        if (members.Count == 0)
            w.Text(Lay.Chu, box.Cx, box.Cy, th, $"(Không có cấu kiện nằm trong mặt phẳng trục {g.Name})", 0, 1, 2);
        return w;
    }

    private static string Fmt(double v) => v.ToString(Math.Abs(v) >= 1000 ? "0" : "0.0", Inv);
}
