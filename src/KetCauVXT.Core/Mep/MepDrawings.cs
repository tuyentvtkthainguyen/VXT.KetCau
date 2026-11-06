using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Mep;

/// <summary>Ký hiệu 2D thiết bị điện nước (quy ước bản vẽ VN). Toạ độ mm; u = phương dọc tường, n = pháp tuyến hướng vào phòng.</summary>
public static class MepSymbols
{
    public static readonly string[] All =
    {
        "ocam1", "ocam2", "congtac1", "congtac2", "congtac3", "den", "dentuyp", "downlight", "denhat", "quattran", "quattuong",
        "hopchia", "aptomat", "tudien", "boncau", "lavabo", "sen", "voi", "thoatsan", "binhnong", "van", "diemnoi", "ongdung"
    };

    /// <summary>Vẽ ký hiệu tại (x, y) mm, cỡ r (mm), hướng dọc tường (ux, uy), pháp tuyến vào phòng (nx, ny). Trả số thực thể vẽ.</summary>
    public static int Draw(DxfWriter w, string sym, double x, double y, double r, double ux = 1, double uy = 0, double nx = 0, double ny = 1, double lenMm = 0)
    {
        int n0 = w.EntityCount;
        (double, double) P(double a, double b) => (x + a * ux + b * nx, y + a * uy + b * ny);
        void L(double a1, double b1, double a2, double b2) { var p1 = P(a1, b1); var p2 = P(a2, b2); w.Line(Lay.KyHieu, p1.Item1, p1.Item2, p2.Item1, p2.Item2); }
        void Poly(IEnumerable<(double A, double B)> pts, bool closed) => w.Polyline(Lay.KyHieu, pts.Select(q => P(q.A, q.B)).ToList(), closed);
        IEnumerable<(double, double)> Semi(double rr, double b0 = 0)
        {
            for (int i = 0; i <= 12; i++) { double t = Math.PI * i / 12; yield return (rr * Math.Cos(t), b0 + rr * Math.Sin(t)); }
        }
        IEnumerable<(double, double)> Oval(double a, double b, double cb)
        {
            for (int i = 0; i < 24; i++) { double t = 2 * Math.PI * i / 24; yield return (a * Math.Cos(t), cb + b * Math.Sin(t)); }
        }
        switch (sym)
        {
            case "ocam1":
            case "ocam2":
                Poly(Semi(r), false); L(-r, 0, r, 0);
                if (sym == "ocam1") L(0, r, 0, 1.6 * r);
                else { L(-0.35 * r, 0.93 * r, -0.35 * r, 1.6 * r); L(0.35 * r, 0.93 * r, 0.35 * r, 1.6 * r); }
                break;
            case "congtac1":
            case "congtac2":
            case "congtac3":
            {
                int k = sym[^1] - '0';
                var c = P(0, 0.6 * r); w.Circle(Lay.KyHieu, c.Item1, c.Item2, 0.4 * r);
                L(0.28 * r, 0.88 * r, 1.2 * r, 1.8 * r);
                for (int i = 0; i < k; i++) { double a = 0.75 * r + i * 0.22 * r, b = 1.35 * r + i * 0.22 * r; L(a, b, a + 0.3 * r, b - 0.3 * r); }
                break;
            }
            case "den":
            {
                w.Circle(Lay.KyHieu, x, y, r); double d = r * 0.7071;
                w.Line(Lay.KyHieu, x - d, y - d, x + d, y + d); w.Line(Lay.KyHieu, x - d, y + d, x + d, y - d);
                break;
            }
            case "dentuyp":
            {
                double hl = Math.Max(lenMm, 4 * r) / 2;
                Poly(new[] { (-hl, -0.25 * r), (hl, -0.25 * r), (hl, 0.25 * r), (-hl, 0.25 * r) }, true); L(-hl, 0, hl, 0);
                break;
            }
            case "downlight":
                w.Circle(Lay.KyHieu, x, y, 0.6 * r); w.Circle(Lay.KyHieu, x, y, 0.3 * r);
                break;
            case "denhat":
                Poly(Semi(r), false); L(-r, 0, r, 0); L(-0.6 * r, 0.2 * r, 0.6 * r, 0.8 * r); L(-0.6 * r, 0.8 * r, 0.6 * r, 0.2 * r);
                break;
            case "quattran":
                w.Circle(Lay.KyHieu, x, y, r);
                for (int i = 0; i < 3; i++) { double t = 2 * Math.PI * i / 3 + Math.PI / 2; w.Line(Lay.KyHieu, x, y, x + 1.6 * r * Math.Cos(t), y + 1.6 * r * Math.Sin(t)); }
                break;
            case "quattuong":
                Poly(Semi(r), false); L(-r, 0, r, 0); L(0, 0.2 * r, -0.8 * r, 1.4 * r); L(0, 0.2 * r, 0.8 * r, 1.4 * r);
                break;
            case "hopchia":
                Poly(new[] { (-0.5 * r, 0.0), (0.5 * r, 0.0), (0.5 * r, r), (-0.5 * r, r) }, true); L(-0.5 * r, 0, 0.5 * r, r); L(-0.5 * r, r, 0.5 * r, 0);
                break;
            case "aptomat":
                Poly(new[] { (-0.5 * r, 0.0), (0.5 * r, 0.0), (0.5 * r, 1.4 * r), (-0.5 * r, 1.4 * r) }, true); L(-0.5 * r, 0.3 * r, 0.5 * r, 1.1 * r);
                break;
            case "tudien":
            {
                Poly(new[] { (-r, 0.0), (r, 0.0), (r, 0.8 * r), (-r, 0.8 * r) }, true);
                var a1 = P(-r, 0); var a2 = P(r, 0); var a3 = P(r, 0.8 * r);
                w.Solid(Lay.KyHieu, a1.Item1, a1.Item2, a2.Item1, a2.Item2, a3.Item1, a3.Item2, a3.Item1, a3.Item2);
                break;
            }
            case "boncau":
                Poly(new[] { (-1.2 * r, 0.0), (1.2 * r, 0.0), (1.2 * r, 0.7 * r), (-1.2 * r, 0.7 * r) }, true);
                Poly(Oval(r, 1.3 * r, 2.0 * r), true);
                break;
            case "lavabo":
                Poly(new[] { (-1.5 * r, 0.0), (1.5 * r, 0.0), (1.5 * r, 2.2 * r), (-1.5 * r, 2.2 * r) }, true);
                Poly(Oval(1.1 * r, 0.75 * r, 1.15 * r), true);
                { var c = P(0, 1.15 * r); w.Circle(Lay.KyHieu, c.Item1, c.Item2, 0.15 * r); }
                break;
            case "sen":
            {
                var c = P(0, 0.9 * r); w.Circle(Lay.KyHieu, c.Item1, c.Item2, 0.5 * r);
                for (int i = 0; i < 6; i++) { double t = 2 * Math.PI * i / 6; L(0.6 * r * Math.Cos(t), 0.9 * r + 0.6 * r * Math.Sin(t), 0.95 * r * Math.Cos(t), 0.9 * r + 0.95 * r * Math.Sin(t)); }
                L(0, 0, 0, 0.4 * r);
                break;
            }
            case "voi":
            {
                var c = P(0, 0.35 * r); w.Circle(Lay.KyHieu, c.Item1, c.Item2, 0.3 * r); L(0, 0.65 * r, 0, 1.3 * r);
                break;
            }
            case "thoatsan":
            {
                w.Circle(Lay.KyHieu, x, y, 0.8 * r);
                w.Line(Lay.KyHieu, x - 0.8 * r, y, x + 0.8 * r, y); w.Line(Lay.KyHieu, x, y - 0.8 * r, x, y + 0.8 * r);
                break;
            }
            case "binhnong":
                Poly(new[] { (-1.2 * r, 0.0), (1.2 * r, 0.0), (1.2 * r, 1.4 * r), (-1.2 * r, 1.4 * r) }, true);
                { var c = P(0, 0.7 * r); w.Text(Lay.Chu, c.Item1, c.Item2, 0.5 * r, "BNL", 0, 1, 2); }
                break;
            case "ongdung":
                w.Circle(Lay.KyHieu, x, y, 0.7 * r); w.Circle(Lay.KyHieu, x, y, 0.45 * r); w.Dot(Lay.KyHieu, x, y, 0.2 * r);
                break;
            case "van":
                Poly(new[] { (-0.6 * r, 0.2 * r), (0.6 * r, 1.0 * r), (0.6 * r, 0.2 * r), (-0.6 * r, 1.0 * r) }, true);
                break;
            default:
                w.Circle(Lay.KyHieu, x, y, 0.35 * r);
                break;
        }
        return w.EntityCount - n0;
    }
}

/// <summary>Bản vẽ bộ môn điện nước: mặt bằng cấp nước, thoát nước, điện chiếu sáng, điện ổ cắm theo tầng (kiến trúc vẽ nền),
/// kèm bảng ký hiệu; và bảng thống kê vật tư. Tiền tố số hiệu bản vẽ bộ môn: DN-.</summary>
public static class MepDrawings
{
    public const double Scale = 100;
    public enum PlanKind { CapNuoc, ThoatNuoc, ChieuSang, OCam }

    public static string PlanTitle(PlanKind k) => k switch
    {
        PlanKind.CapNuoc => "MẶT BẰNG CẤP NƯỚC",
        PlanKind.ThoatNuoc => "MẶT BẰNG THOÁT NƯỚC",
        PlanKind.ChieuSang => "MẶT BẰNG ĐIỆN CHIẾU SÁNG",
        _ => "MẶT BẰNG ĐIỆN Ổ CẮM"
    };

    public static bool InPlan(PlanKind k, MepSystemKind s) => k switch
    {
        PlanKind.CapNuoc => MepSystems.IsSupply(s),
        PlanKind.ThoatNuoc => MepSystems.IsDrain(s),
        PlanKind.ChieuSang => s == MepSystemKind.DienChieuSang,
        _ => s is MepSystemKind.DienOCam or MepSystemKind.DienDieuHoa or MepSystemKind.DienNhe
    };

    /// <summary>Thiết bị thể hiện trên mặt bằng loại k.</summary>
    public static bool DeviceInPlan(MepModel m, MepDevice d, PlanKind k)
    {
        var fam = m.FamilyOf(d);
        if (fam != null && fam.Connectors.Any(c => InPlan(k, c.System))) return true;
        if (m.Runs.Any(r => (r.FromId == d.Id || r.ToId == d.Id) && InPlan(k, r.System))) return true;
        var cat = fam?.Category;
        return k switch
        {
            PlanKind.ChieuSang => cat is MepCategory.Den or MepCategory.CongTac or MepCategory.TuDien or MepCategory.Aptomat || fam?.Symbol == "quattran",
            PlanKind.OCam => cat is MepCategory.OCam or MepCategory.TuDien or MepCategory.Aptomat or MepCategory.BinhNong || fam?.Symbol == "quattuong",
            PlanKind.CapNuoc => cat is MepCategory.Van or MepCategory.DiemNoiOng && m.Runs.Any(r => (r.FromId == d.Id || r.ToId == d.Id) && MepSystems.IsSupply(r.System)),
            _ => false
        };
    }

    /// <summary>Tiền tố ký hiệu thiết bị.</summary>
    public static string Prefix(MepFamily f) => f?.Category switch
    {
        MepCategory.OCam => "OC", MepCategory.CongTac => "CT", MepCategory.Den => "Đ", MepCategory.Quat => "Q", MepCategory.HopChia => "HC",
        MepCategory.Aptomat => "AT", MepCategory.TuDien => "TĐ", MepCategory.BinhNong => "BNL", MepCategory.Van => "V",
        MepCategory.DiemNoiOng => f.Symbol == "ongdung" ? "OĐ" : "N",
        MepCategory.VeSinh => f.Symbol switch { "boncau" => "BC", "lavabo" => "LV", "sen" => "S", "thoatsan" => "TS", _ => "VN" },
        _ => "TB"
    };

    /// <summary>Đặt ký hiệu tự động cho thiết bị chưa có (theo tầng, tiền tố, thứ tự Id).</summary>
    public static int AutoMark(Project p)
    {
        var m = p.Mep; int n = 0;
        foreach (var g in m.Devices.GroupBy(d => (d.Story, Prefix(m.FamilyOf(d)))))
        {
            int i = 1;
            var taken = g.Select(d => d.Mark).Where(s => !string.IsNullOrWhiteSpace(s)).ToHashSet();
            foreach (var d in g.OrderBy(d => d.Id))
            {
                if (!string.IsNullOrWhiteSpace(d.Mark)) continue;
                string mk;
                do mk = g.Key.Item2 + (i++); while (taken.Contains(mk));
                d.Mark = mk; taken.Add(mk); n++;
            }
        }
        return n;
    }

    /// <summary>Tất cả hình bộ môn điện nước: mặt bằng theo tầng × loại (có nội dung) + bảng thống kê vật tư.</summary>
    public static List<Figure> Figures(Project p)
    {
        var figs = new List<Figure>();
        var m = p.Mep;
        if (m == null || m.IsEmpty) return figs;
        if (m.Fittings.Count == 0) MepRouting.RebuildAll(p);
        var stories = p.Stories.OrderBy(s => s.Elevation).Select(s => s.Name).Where(n => m.Devices.Any(d => d.Story == n)).ToList();
        foreach (var extra in m.Devices.Select(d => d.Story).Distinct().Where(s => !stories.Contains(s))) stories.Add(extra);
        foreach (var st in stories)
            foreach (var k in Enum.GetValues<PlanKind>())
            {
                var f = Plan(p, st, k);
                if (f != null) figs.Add(f);
            }
        // mặt cắt dọc tuyến thoát (cao độ đáy ống, độ dốc i, phụ kiện) – Mep/MepLongSection.cs
        try { figs.AddRange(MepLongSection.Figures(p)); } catch (Exception) { }
        // mặt bằng điều hoà – thông gió (Mep/MepHvac.cs)
        try { figs.AddRange(HvacDrawings.Figures(p)); } catch (Exception) { }
        // sơ đồ nguyên lý tủ điện, sơ đồ không gian cấp nước, sơ đồ ống đứng thoát nước – Mep/MepRound4.cs
        try { figs.AddRange(MepRound4.Figures(p)); } catch (Exception) { }
        var sch = MepQuantities.ScheduleFigure(p);
        if (sch != null) figs.Add(sch);
        return figs;
    }

    /// <summary>Mặt bằng một loại của một tầng; null nếu không có thiết bị / tuyến.</summary>
    public static Figure Plan(Project p, string story, PlanKind k)
    {
        var m = p.Mep;
        var devs = m.Devices.Where(d => d.Story == story && DeviceInPlan(m, d, k)).ToList();
        var runs = m.Runs.Where(r => r.Story == story && InPlan(k, r.System)).ToList();
        if (devs.Count == 0 && runs.Count == 0) return null;
        var w = new DxfWriter(Scale);
        double th = w.Th, r0 = 0.9 * th;

        // ---- nền kiến trúc (xref): tường 2 nét, ô cửa để trống ----
        foreach (var wl in p.Walls.Where(x => x.Story == story))
        {
            double t = MepRouting.WallInfo(p, wl).Total;
            double L = wl.Chord; if (L < 1e-6) continue;
            double ux = (wl.X2 - wl.X1) / L, uy = (wl.Y2 - wl.Y1) / L, nx = -uy, ny = ux;
            var cuts = new List<(double A, double B)>();
            foreach (var o in wl.Openings ?? new())
            {
                var ot = WallAssembly.TypeOf(p, o); if (ot == null) continue;
                cuts.Add((Math.Max(0, o.Offset - ot.Width / 2), Math.Min(L, o.Offset + ot.Width / 2)));
            }
            cuts = cuts.OrderBy(c => c.A).ToList();
            double s0 = 0;
            var pieces = new List<(double A, double B)>();
            foreach (var c in cuts) { if (c.A > s0) pieces.Add((s0, c.A)); s0 = Math.Max(s0, c.B); }
            if (s0 < L) pieces.Add((s0, L));
            foreach (var (a, b) in pieces)
                foreach (int sg in new[] { 1, -1 })
                {
                    double ox = nx * t / 2 * sg, oy = ny * t / 2 * sg;
                    w.Line(Lay.BeTong, (wl.X1 + ux * a + ox) * 1000, (wl.Y1 + uy * a + oy) * 1000, (wl.X1 + ux * b + ox) * 1000, (wl.Y1 + uy * b + oy) * 1000);
                }
            foreach (var (a, b) in cuts)
                foreach (double s in new[] { a, b })
                    w.Line(Lay.BeTong, (wl.X1 + ux * s + nx * t / 2) * 1000, (wl.Y1 + uy * s + ny * t / 2) * 1000, (wl.X1 + ux * s - nx * t / 2) * 1000, (wl.Y1 + uy * s - ny * t / 2) * 1000);
        }

        // ---- tuyến: hình chiếu mặt bằng; đoạn đứng (lên / xuống trong tường) = chấm tròn ----
        foreach (var r in runs)
        {
            var pts = new List<(double, double)>();
            foreach (var q in r.Path)
            {
                var xy = (q.X * 1000, q.Y * 1000);
                if (pts.Count == 0 || MepGeom.Dist2D(pts[^1].Item1, pts[^1].Item2, xy.Item1, xy.Item2) > 1e-3) pts.Add(xy);
            }
            string lt = MepSystems.IsDrain(r.System) ? "DASHED" : null;
            if (pts.Count >= 2) w.Polyline(Lay.Thep, pts, false, 0, lt);
            for (int i = 1; i < r.Path.Count; i++)
            {
                var a = r.Path[i - 1]; var b = r.Path[i];
                if (MepGeom.Dist2D(a.X, a.Y, b.X, b.Y) < 1e-6 && Math.Abs(a.Z - b.Z) > 1e-6) w.Dot(Lay.Thep, a.X * 1000, a.Y * 1000, 0.15 * th);
            }
            if (MepSystems.IsDrain(r.System)) SlopeMarks(p, w, r, th);
            // nhãn cỡ ở giữa đoạn dài nhất
            int best = -1; double bl = 0;
            for (int i = 1; i < r.Path.Count; i++)
            {
                double l2 = MepGeom.Dist2D(r.Path[i - 1].X, r.Path[i - 1].Y, r.Path[i].X, r.Path[i].Y);
                if (l2 > bl) { bl = l2; best = i; }
            }
            if (best > 0 && bl > 0.3)
            {
                var a = r.Path[best - 1]; var b = r.Path[best];
                double ang = Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI;
                if (ang > 90) ang -= 180; if (ang < -90) ang += 180;
                w.Text(Lay.Chu, (a.X + b.X) / 2 * 1000, (a.Y + b.Y) / 2 * 1000 + 0.3 * th, 0.8 * th, RunLabel(r), ang, 1, 0);
            }
        }

        // ---- thiết bị ----
        foreach (var d in devs)
        {
            var fam = m.FamilyOf(d);
            var pos = MepRouting.DevicePos(p, d);
            double ux = 1, uy = 0, nx = 0, ny = 1;
            if (pos.Wall != null)
            {
                double L = Math.Max(1e-9, pos.Wall.Chord);
                ux = (pos.Wall.X2 - pos.Wall.X1) / L; uy = (pos.Wall.Y2 - pos.Wall.Y1) / L;
                int s = d.Side >= 0 ? 1 : -1; nx = -uy * s; ny = ux * s;
            }
            MepSymbols.Draw(w, fam?.Symbol ?? "", pos.X * 1000, pos.Y * 1000, r0, ux, uy, nx, ny, (fam?.W ?? 0) * 1000);
            string mk = string.IsNullOrWhiteSpace(d.Mark) ? Prefix(fam) + d.Id : d.Mark;
            w.Text(Lay.Chu, pos.X * 1000 + nx * 2.4 * r0 + 0.6 * r0, pos.Y * 1000 + ny * 2.4 * r0, 0.8 * th, mk, 0, 0, 2);
        }

        // ---- tiêu đề + bảng ký hiệu ----
        var ext = w.Extents;
        double x0 = ext.IsEmpty ? 0 : ext.X1, y0 = ext.IsEmpty ? 0 : ext.Y1;
        string title = PlanTitle(k) + " " + story.ToUpperInvariant();
        w.Text(Lay.Chu, (ext.IsEmpty ? 0 : ext.Cx), y0 - 3 * th, 1.4 * th, title, 0, 1, 0);
        w.Text(Lay.Chu, (ext.IsEmpty ? 0 : ext.Cx), y0 - 4.8 * th, 0.9 * th, "TL: 1/" + Scale.ToString("0"), 0, 1, 0);
        Legend(w, m, devs, runs, (ext.IsEmpty ? 0 : ext.X2) + 4 * th, ext.IsEmpty ? 0 : ext.Y2, th, r0);
        return new Figure(w, title);
    }

    /// <summary>Chuỗi độ dốc "i=2%" (dấu phẩy thập phân).</summary>
    public static string SlopeText(double i) => "i=" + (i * 100).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + "%";

    /// <summary>Cao độ dạng ±0,000.</summary>
    public static string LevelText(double z) => (z >= 0 ? "+" : "-") + Math.Abs(z).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Tuyến thoát: mũi tên chiều dốc + "i=…%" giữa mỗi đoạn ngang ≥ 0,5 m; cao độ đáy ống đầu / cuối tuyến.</summary>
    public static void SlopeMarks(Project p, DxfWriter w, MepRun r, double th)
    {
        double i = MepDrainage.SlopeOf(p, r);
        var prof = MepDrainage.Profile(r);
        for (int k = 1; k < prof.Count; k++)
        {
            var a = prof[k - 1]; var b = prof[k];
            double L = MepGeom.Dist2D(a.X, a.Y, b.X, b.Y);
            if (L < 0.5) continue;
            double ux = (b.X - a.X) / L, uy = (b.Y - a.Y) / L, mx = (a.X + b.X) / 2 * 1000, my = (a.Y + b.Y) / 2 * 1000;
            double al = 2.0 * th, ah = 0.6 * th;
            double tx = mx + ux * al / 2, ty = my + uy * al / 2;
            w.Line(Lay.KyHieu, mx - ux * al / 2 - uy * 0.9 * th, my - uy * al / 2 + ux * 0.9 * th, tx - uy * 0.9 * th, ty + ux * 0.9 * th);
            w.Line(Lay.KyHieu, tx - uy * 0.9 * th, ty + ux * 0.9 * th, tx - ux * ah - uy * (0.9 * th + 0.4 * ah), ty - uy * ah + ux * (0.9 * th + 0.4 * ah));
            w.Line(Lay.KyHieu, tx - uy * 0.9 * th, ty + ux * 0.9 * th, tx - ux * ah - uy * (0.9 * th - 0.4 * ah), ty - uy * ah + ux * (0.9 * th - 0.4 * ah));
            double ang = Math.Atan2(uy, ux) * 180 / Math.PI;
            if (ang > 90) ang -= 180; if (ang < -90) ang += 180;
            w.Text(Lay.Chu, mx - uy * 1.6 * th, my + ux * 1.6 * th, 0.8 * th, SlopeText(i), ang, 1, 0);
        }
        if (prof.Count >= 2)
        {
            w.Text(Lay.Chu, prof[0].X * 1000 + 0.6 * th, prof[0].Y * 1000 - 1.6 * th, 0.7 * th, "Đáy ống " + LevelText(prof[0].Invert), 0, 0, 0);
            w.Text(Lay.Chu, prof[^1].X * 1000 + 0.6 * th, prof[^1].Y * 1000 - 1.6 * th, 0.7 * th, "Đáy ống " + LevelText(prof[^1].Invert), 0, 0, 0);
        }
    }

    public static string RunLabel(MepRun r) => r.Kind switch
    {
        MepRunKind.Ong => $"{(MepSystems.IsDrain(r.System) ? "uPVC" : "PPR")} Ø{r.Size:0}",
        _ => $"{r.Conductors}x{r.Size.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')} – Ø{(r.ConduitSize > 0 ? r.ConduitSize : MepSizing.ConduitFor(r.Size, Math.Max(1, r.Conductors))):0}"
    };

    public const string LegendTitle = "BẢNG KÝ HIỆU";

    /// <summary>Bảng ký hiệu: ký hiệu thiết bị dùng trên mặt bằng + kiểu nét hệ thống.</summary>
    public static void Legend(DxfWriter w, MepModel m, List<MepDevice> devs, List<MepRun> runs, double x, double yTop, double th, double r0)
    {
        double rh = 3.2 * r0, c1 = 6 * r0, c2 = 26 * th;
        var fams = devs.Select(d => m.FamilyOf(d)).Where(f => f != null).GroupBy(f => f.Name).Select(g => g.First()).ToList();
        var systems = runs.Select(r => r.System).Distinct().ToList();
        int rows = fams.Count + systems.Count;
        double y = yTop;
        w.Text(Lay.Chu, x + (c1 + c2) / 2, y + 0.8 * th, 1.1 * th, LegendTitle, 0, 1, 0);
        w.Line(Lay.KhungTen, x, y, x + c1 + c2, y);
        foreach (var f in fams)
        {
            double cy = y - rh / 2;
            MepSymbols.Draw(w, f.Symbol, x + c1 / 2, cy - 0.6 * r0, r0, 1, 0, 0, 1, Math.Min(f.W * 1000, c1 * 0.8));
            w.Text(Lay.Chu, x + c1 + 0.8 * th, cy, 0.9 * th, f.Name, 0, 0, 2);
            y -= rh; w.Line(Lay.KhungTen, x, y, x + c1 + c2, y);
        }
        foreach (var s in systems)
        {
            double cy = y - rh / 2;
            w.Line(Lay.Thep, x + 0.15 * c1, cy, x + 0.85 * c1, cy, MepSystems.IsDrain(s) ? "DASHED" : null);
            w.Text(Lay.Chu, x + c1 + 0.8 * th, cy, 0.9 * th, "Tuyến " + MepSystems.Name(s).ToLowerInvariant() + (MepSystems.IsDrain(s) ? " (đi dưới sàn, có độ dốc i)" : " (chôn ngầm)"), 0, 0, 2);
            y -= rh; w.Line(Lay.KhungTen, x, y, x + c1 + c2, y);
        }
        if (rows > 0)
        {
            w.Line(Lay.KhungTen, x, yTop, x, y); w.Line(Lay.KhungTen, x + c1, yTop, x + c1, y); w.Line(Lay.KhungTen, x + c1 + c2, yTop, x + c1 + c2, y);
        }
    }
}
