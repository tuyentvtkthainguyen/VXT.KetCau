using KetCauVXT.Import;
using KetCauVXT.Model;

namespace KetCauVXT.Loads;

/// <summary>Sinh tải từ đoạn tường khai báo theo vị trí (Project.Walls) và từ cấu tạo sàn (Slab.BuildUp). Gọi mỗi lần phân tích:
/// xoá tải tự sinh cũ (FrameLoad/AreaLoad Source = "WALL"/"BUILDUP") rồi tính lại.
/// <list type="bullet">
/// <item>Tường: q = (t·γ + n·tp·γtrát)·H·(1 − tỷ lệ cửa) kN/m, đặt lên phần dầm thẳng hàng nằm dưới tường (tải phân bố từng đoạn);
///   phần tường không có dầm đỡ → tải phân bố đều lên ô sàn chứa trung điểm đoạn đó (trường hợp HTHT).</item>
/// <item>Chiều cao tường mặc định: chiều cao tầng phía trên − chiều cao dầm dưới tường (≥ 0,35 m); trên tầng mái: 0,9 m (tường chắn mái).</item>
/// <item>Cấu tạo sàn: HTHT = Σ lớp hoàn thiện, HT = hoạt tải của cấu tạo.</item>
/// <item>Dầm cong: tường khai báo theo dây cung của dầm (vd. "Gán tường" lên dầm cong) được coi là tường cong chạy theo dầm –
///   tải q kN/m đặt theo chiều dài cung (vị trí tương đối trên dây cung = vị trí tương đối trên cung), tổng tải = q·L cung.</item>
/// <item>Tường cong (WallLine.Sagitta ≠ 0, vd. nhập từ bản vẽ kiến trúc): chiều dài, diện tích tường theo cung; tải đặt lên các dầm cong
///   cùng đường tròn (tâm lệch ≤ 20 mm, bán kính lệch ≤ nửa chiều dày tường + 50 mm) nằm dưới tường, kể cả khi dầm cong đã bị chia
///   thành nhiều đoạn – tổng tải = q·L cung phần có dầm đỡ; phần còn lại quy về ô sàn chứa điểm giữa cung.</item>
/// </list></summary>
public static class WallLoadGenerator
{
    public const string WallSource = "WALL", BuildUpSource = "BUILDUP";

    public static List<string> Apply(Project p)
    {
        var warn = new List<string>();
        p.FrameLoads.RemoveAll(l => l.Source == WallSource);
        p.AreaLoads.RemoveAll(a => a.Source == WallSource || a.Source == BuildUpSource);
        if (p.Pattern("HTHT") == null && (p.Walls.Count > 0 || p.Slabs.Any(s => !string.IsNullOrEmpty(s.BuildUp)))) ImportGeometry.EnsurePatterns(p);

        // ---- cấu tạo sàn ----
        foreach (var s in p.Slabs.Where(s => !string.IsNullOrEmpty(s.BuildUp)))
        {
            var bu = p.FloorBuildUps.FirstOrDefault(b => b.Name == s.BuildUp);
            if (bu == null) { warn.Add($"Ô sàn {s.Label}: không có cấu tạo sàn \"{s.BuildUp}\"."); continue; }
            // cấu tạo sàn thay cho tải hoàn thiện/hoạt tải nhập tay của ô
            p.AreaLoads.RemoveAll(a => a.Slab == s.Id && a.Source == "" && (a.Pattern == "HTHT" || a.Pattern == "HT"));
            if (bu.Finish > 0) p.AreaLoads.Add(new AreaLoad { Slab = s.Id, Pattern = "HTHT", Q = Math.Round(bu.Finish, 4), Note = "Cấu tạo: " + bu.Name, Source = BuildUpSource });
            if (bu.Live > 0) p.AreaLoads.Add(new AreaLoad { Slab = s.Id, Pattern = "HT", Q = bu.Live, Note = "Hoạt tải: " + bu.Name, Source = BuildUpSource });
        }

        // ---- tường ----
        var stories = p.Stories.OrderBy(s => s.Elevation).ToList();
        foreach (var w in p.Walls)
        {
            var wt = p.WallTypes.FirstOrDefault(t => t.Name == w.Type);
            if (wt == null) { warn.Add($"Tường #{w.Id}: không có loại tường \"{w.Type}\"."); continue; }
            var st = stories.FirstOrDefault(s => s.Name == w.Story);
            if (st == null) { warn.Add($"Tường #{w.Id}: không có tầng {w.Story}."); continue; }
            double L = w.Length;
            if (L < 0.05) continue;
            var beams = BeamsUnder(p, w, st.Elevation, wt.Thickness);
            double hBeam = beams.Select(b => p.Section(b.f.Section).Props(0.5).Depth).DefaultIfEmpty(0.35).Max();
            int idx = stories.IndexOf(st);
            double H = w.Height > 0 ? w.Height : idx + 1 < stories.Count ? stories[idx + 1].Elevation - st.Elevation - Math.Max(hBeam, 0.35) : 0.9;
            if (H <= 0) continue;
            double area = L * H;
            double open = w.OpeningArea >= 0 ? w.OpeningArea : wt.OpeningRatio * area;
            double ratio = Math.Clamp(open / area, 0, 0.9);
            double q = wt.WeightPerM2 * H * (1 - ratio);
            string note = $"{wt.Name}, H = {H:0.00} m" + (ratio > 0 ? $", trừ cửa {ratio * 100:0}%" : "");
            double covered = 0;
            foreach (var (f, r0, r1, len) in beams)
            {
                var fl = new FrameLoad { Element = f.Id, Pattern = "HTHT", Dir = LoadDir.Gravity, Note = note, Source = WallSource };
                fl.Points.Add(new[] { r0, q }); fl.Points.Add(new[] { r1, q });
                p.FrameLoads.Add(fl);
                covered += len;
            }
            double rest = L - covered;
            if (rest > 0.1)
            {
                // phần không có dầm đỡ → tải phân bố lên ô sàn
                double mx = (w.X1 + w.X2) / 2, my = (w.Y1 + w.Y2) / 2;
                if (w.IsCurved)
                {
                    var wa = CurvedBeam.Arc(new Vec3(w.X1, w.Y1, 0), new Vec3(w.X2, w.Y2, 0), w.Sagitta);
                    if (wa != null) { var mp = wa.Mid; mx = mp.X; my = mp.Y; }
                }
                var sl = ModelEdit.SlabAt(p, w.Story, mx, my);
                if (sl != null)
                {
                    var pts = sl.Nodes.Select(id => p.Node(id)).Select(n => (n.X, n.Y)).ToList();
                    double a = Math.Abs(ImportGeometry.SignedArea(pts));
                    p.AreaLoads.Add(new AreaLoad { Slab = sl.Id, Pattern = "HTHT", Q = Math.Round(q * rest / a, 4), Note = $"Tường không có dầm đỡ dài {rest:0.00} m ({note})", Source = WallSource });
                }
                else warn.Add($"Tường #{w.Id} ({wt.Name}) tầng {w.Story}: {rest:0.00} m không có dầm hoặc sàn đỡ – bỏ qua tải phần này.");
            }
        }
        return warn;
    }

    /// <summary>Các dầm ở cao độ z nằm dưới tường: (thanh, r0, r1 trên thanh, chiều dài phủ).</summary>
    public static List<(FrameElement f, double r0, double r1, double len)> BeamsUnder(Project p, WallLine w, double z, double thick)
    {
        if (w.IsCurved) return CurvedBeamsUnder(p, w, z, thick);
        var res = new List<(FrameElement, double, double, double)>();
        double L = w.Chord;
        double ux = (w.X2 - w.X1) / L, uy = (w.Y2 - w.Y1) / L;
        double tol = Math.Max(0.06, thick / 2 + 0.05);
        foreach (var f in p.Frames)
        {
            if (f.IsVerticalKind) continue;
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            if (Math.Abs(a.Z - z) > 0.01 || Math.Abs(b.Z - z) > 0.01) continue;
            double fl = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            if (fl < 1e-6) continue;
            double fx = (b.X - a.X) / fl, fy = (b.Y - a.Y) / fl;
            if (Math.Abs(fx * uy - fy * ux) > 0.02) continue;                       // không song song
            double dist = Math.Abs((a.X - w.X1) * uy - (a.Y - w.Y1) * ux);         // khoảng cách đường thẳng
            if (dist > tol) continue;
            double ta = (a.X - w.X1) * ux + (a.Y - w.Y1) * uy, tb = (b.X - w.X1) * ux + (b.Y - w.Y1) * uy;
            double lo = Math.Max(0, Math.Min(ta, tb)), hi = Math.Min(L, Math.Max(ta, tb));
            if (hi - lo < 0.01) continue;
            double r0 = Math.Abs(lo - ta) / fl, r1 = Math.Abs(hi - ta) / fl;
            res.Add((f, Math.Min(r0, r1), Math.Max(r0, r1), hi - lo));
        }
        return res;
    }

    /// <summary>Tường cong: các dầm cong ở cao độ z cùng đường tròn với tim tường – (thanh, r0, r1 theo cung của thanh, chiều dài cung phủ).</summary>
    private static List<(FrameElement f, double r0, double r1, double len)> CurvedBeamsUnder(Project p, WallLine w, double z, double thick)
    {
        var res = new List<(FrameElement, double, double, double)>();
        var wa = CurvedBeam.Arc(new Vec3(w.X1, w.Y1, z), new Vec3(w.X2, w.Y2, z), w.Sagitta);
        if (wa == null) return res;
        double sw = Math.Abs(wa.Sweep), L = wa.Length;
        if (sw < 1e-9) return res;
        double tolR = Math.Max(0.06, thick / 2 + 0.05);
        // tham số trên cung tường (0 … 1 trong phạm vi tường; phần ngoài chia đều hai phía)
        double Rel(double x, double y)
        {
            double d = (Math.Atan2(y - wa.Cy, x - wa.Cx) - wa.StartAngle) * Math.Sign(wa.Sweep);
            double lo = -(2 * Math.PI - sw) / 2;
            d = (d - lo) % (2 * Math.PI);
            if (d < 0) d += 2 * Math.PI;
            return (d + lo) / sw;
        }
        foreach (var f in p.Frames)
        {
            if (f.IsVerticalKind || !f.IsCurved) continue;
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            if (Math.Abs(a.Z - z) > 0.01 || Math.Abs(b.Z - z) > 0.01) continue;
            var fa = CurvedBeam.Arc(p, f);
            if (fa == null) continue;
            if (Math.Sqrt((fa.Cx - wa.Cx) * (fa.Cx - wa.Cx) + (fa.Cy - wa.Cy) * (fa.Cy - wa.Cy)) > 0.02) continue;
            if (Math.Abs(fa.Radius - wa.Radius) > tolR) continue;
            double tI = Rel(a.X, a.Y), tJ = Rel(b.X, b.Y);
            if (Math.Abs(tJ - tI) < 1e-9) continue;
            // dầm cong nằm trên cung tường: kiểm tra điểm giữa dầm cũng thuộc khoảng [tI, tJ] (không đi vòng phía ngoài)
            var mid = fa.Mid;
            double tm = Rel(mid.X, mid.Y);
            if (tm < Math.Min(tI, tJ) - 1e-6 || tm > Math.Max(tI, tJ) + 1e-6) continue;
            double lo2 = Math.Max(0, Math.Min(tI, tJ)), hi2 = Math.Min(1, Math.Max(tI, tJ));
            if ((hi2 - lo2) * L < 0.01) continue;
            double r0 = (lo2 - tI) / (tJ - tI), r1 = (hi2 - tI) / (tJ - tI);
            res.Add((f, Math.Clamp(Math.Min(r0, r1), 0, 1), Math.Clamp(Math.Max(r0, r1), 0, 1), (hi2 - lo2) * L));
        }
        return res;
    }
}
