using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Mep;

/// <summary>Một điểm của mặt cắt dọc tuyến thoát (chuỗi tuyến nối liên tiếp).</summary>
public sealed class DrainProfilePoint
{
    /// <summary>Chiều dài ngang tích luỹ từ đầu chuỗi (m)</summary>
    public double S { get; set; }
    /// <summary>Cao độ tim và đáy ống (m)</summary>
    public double Z { get; set; }
    public double Invert { get; set; }
    public int RunId { get; set; }
    public double Size { get; set; }
}

/// <summary>Đoạn ống giữa 2 điểm mặt cắt dọc: chiều dài, độ dốc, độ chênh đáy ống.</summary>
public sealed class DrainProfileSegment
{
    public double S0 { get; set; }
    public double S1 { get; set; }
    public double Length => S1 - S0;
    public double Slope { get; set; }
    public double Drop { get; set; }
    public double Size { get; set; }
    public int RunId { get; set; }
    /// <summary>Nhãn "Ø110 – i=2% – L=3,20 m"</summary>
    public string Label { get; set; } = "";
}

/// <summary>MẶT CẮT DỌC TUYẾN THOÁT NƯỚC: chuỗi tuyến thoát nối tiếp (đầu tuyến sau = cuối tuyến trước) từ thiết bị xa nhất tới ống đứng;
/// vẽ đường đáy sàn, ống (2 nét, tỉ lệ đứng phóng đại), cao độ đáy ống tại các điểm đổi hướng, nhãn độ dốc i và chiều dài từng đoạn,
/// phụ kiện (chếch, tê xiên, nắp thăm) và bảng dưới hình (khoảng cách / cao độ đáy ống / độ dốc).</summary>
public static class MepLongSection
{
    public const string TitlePrefix = "MẶT CẮT DỌC TUYẾN THOÁT";
    /// <summary>Phóng đại tỉ lệ đứng so với tỉ lệ ngang</summary>
    public const double VerticalExaggeration = 5;

    /// <summary>Các chuỗi tuyến thoát: bắt đầu từ tuyến không có tuyến thoát nào đổ vào (FromId không là ToId của tuyến thoát khác),
    /// nối tiếp theo ToId → FromId; mỗi tuyến chỉ thuộc một chuỗi.</summary>
    public static List<List<MepRun>> Chains(Project p)
    {
        var res = new List<List<MepRun>>();
        var m = p?.Mep;
        if (m == null) return res;
        var drains = m.Runs.Where(r => MepSystems.IsDrain(r.System) && r.Path != null && r.Path.Count >= 2).ToList();
        var used = new HashSet<int>();
        var heads = drains.Where(r => !drains.Any(o => o.Id != r.Id && o.ToId == r.FromId && o.Story == r.Story)).OrderBy(r => r.Story).ThenBy(r => r.Id).ToList();
        foreach (var h in heads.Concat(drains))
        {
            if (used.Contains(h.Id)) continue;
            var chain = new List<MepRun>();
            var cur = h;
            while (cur != null && used.Add(cur.Id))
            {
                chain.Add(cur);
                var c0 = cur;
                cur = drains.Where(o => !used.Contains(o.Id) && o.FromId == c0.ToId && o.Story == c0.Story).OrderBy(o => o.Id).FirstOrDefault();
            }
            res.Add(chain);
        }
        return res;
    }

    /// <summary>Điểm mặt cắt dọc của chuỗi (phần ngang của từng tuyến – MepDrainage.Profile, nối tiếp theo chiều dài ngang).</summary>
    public static List<DrainProfilePoint> Points(List<MepRun> chain)
    {
        var pts = new List<DrainProfilePoint>();
        double s0 = 0;
        foreach (var r in chain)
        {
            var prof = MepDrainage.Profile(r);
            if (prof.Count == 0) continue;
            foreach (var q in prof)
            {
                double s = s0 + q.S;
                if (pts.Count > 0 && Math.Abs(pts[^1].S - s) < 1e-6 && Math.Abs(pts[^1].Z - q.Z) < 1e-6) continue;
                pts.Add(new DrainProfilePoint { S = s, Z = q.Z, Invert = q.Invert, RunId = r.Id, Size = r.Size });
            }
            s0 += prof[^1].S;
        }
        return pts;
    }

    /// <summary>Đoạn giữa các điểm liên tiếp (bỏ đoạn đứng có chiều dài ngang 0): độ dốc thực = chênh đáy ống / chiều dài.</summary>
    public static List<DrainProfileSegment> Segments(List<DrainProfilePoint> pts)
    {
        var res = new List<DrainProfileSegment>();
        for (int i = 1; i < pts.Count; i++)
        {
            double L = pts[i].S - pts[i - 1].S;
            if (L < 1e-6) continue;
            double drop = pts[i - 1].Invert - pts[i].Invert;
            double slope = drop / L;
            res.Add(new DrainProfileSegment
            {
                S0 = pts[i - 1].S, S1 = pts[i].S, Slope = slope, Drop = drop, Size = pts[i].Size, RunId = pts[i].RunId,
                Label = $"Ø{pts[i].Size:0} – i={Pct(slope)} – L={Dt2(L)} m"
            });
        }
        return res;
    }

    /// <summary>"2%" / "2,5%"</summary>
    public static string Pct(double slope) => (Math.Round(slope * 1000) / 10).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + "%";
    private static string Dt2(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Hình mặt cắt dọc một chuỗi (tỉ lệ ngang 1/50, đứng phóng đại ×5); null khi chuỗi không có đoạn ngang.</summary>
    public static Figure Build(Project p, List<MepRun> chain)
    {
        var pts = Points(chain);
        var segs = Segments(pts);
        if (segs.Count == 0) return null;
        double S = 50, vx = VerticalExaggeration;
        var w = new DxfWriter(S);
        double zRef = pts.Min(q => q.Invert) - 0.2;
        double X(double s) => s * 1000;
        double Y(double z) => (z - zRef) * 1000 * vx;
        double sMax = pts.Max(q => q.S);
        string story = chain[0].Story;
        // đáy sàn tầng (ống treo dưới sàn) – lấy cao độ tầng trừ chiều dày sàn tại điểm đầu
        double el = MepRouting.StoryElevation(p, story);
        var r0 = chain[0].Path;
        double slabT = r0 != null && r0.Count > 0 ? MepDrainage.SlabThicknessAt(p, story, r0[0].X, r0[0].Y) : 0.12;
        double zSoffit = el - Math.Max(slabT, 0);
        double zMaxPipe = pts.Max(q => q.Z + q.Size / 2000);
        if (zSoffit > zMaxPipe - 1e-6 && zSoffit - zMaxPipe < 1.5)
        {
            w.Line(Lay.BeTong, X(-0.3), Y(zSoffit), X(sMax + 0.3), Y(zSoffit));
            w.Line(Lay.BeTong, X(-0.3), Y(el), X(sMax + 0.3), Y(el));
            w.Text(Lay.Chu, X(-0.3), Y(el) + 0.3 * w.Th, 0.8 * w.Th, "SÀN " + story + " " + DxfWriter.ElevText(el), 0, 0, 0);
            w.Text(Lay.Chu, X(-0.3), Y(zSoffit) - 1.2 * w.Th, 0.8 * w.Th, "ĐÁY SÀN " + DxfWriter.ElevText(zSoffit), 0, 0, 0);
        }
        // ống: đường đáy và đỉnh (theo đường kính)
        var bottom = pts.Select(q => (X(q.S), Y(q.Invert))).ToList();
        var topL = pts.Select(q => (X(q.S), Y(q.Invert + q.Size / 1000))).ToList();
        w.Polyline(Lay.Thep, bottom, false, 0.35 * S);
        w.Polyline(Lay.Thep, topL, false, 0.18 * S);
        // nhãn độ dốc + mũi tên chiều chảy
        foreach (var sg in segs)
        {
            double xm = X((sg.S0 + sg.S1) / 2);
            var a = pts.First(q => Math.Abs(q.S - sg.S0) < 1e-6);
            var b = pts.Last(q => Math.Abs(q.S - sg.S1) < 1e-6);
            double ym = (Y(a.Invert + a.Size / 1000) + Y(b.Invert + b.Size / 1000)) / 2;
            w.Text(Lay.Chu, xm, ym + 0.6 * w.Th, 0.8 * w.Th, sg.Label, 0, 1, 0);
            double ax = xm + 0.2 * (X(sg.S1) - X(sg.S0)) * 0.5;
            w.Line(Lay.KyHieu, xm - 0.8 * w.Th, ym + 0.3 * w.Th, ax, ym + 0.3 * w.Th);
            w.Line(Lay.KyHieu, ax, ym + 0.3 * w.Th, ax - 0.4 * w.Th, ym + 0.5 * w.Th);
            w.Line(Lay.KyHieu, ax, ym + 0.3 * w.Th, ax - 0.4 * w.Th, ym + 0.1 * w.Th);
        }
        // phụ kiện trên tuyến (chếch, tê xiên, nắp thăm…) – chiếu theo điểm gần nhất
        var fits = new List<MepFitting>();
        foreach (var r in chain) { try { fits.AddRange(MepDrainage.RunFittings(p, r)); } catch (Exception) { } }
        foreach (var f in fits.Where(ff => ff.Kind != MepFittingKind.MangSong))
        {
            var near = pts.Where(q => q.RunId == f.RunId).OrderBy(q => Math.Abs(q.Z - f.Z)).FirstOrDefault();
            if (near == null) continue;
            double fx = X(near.S), fy = Y(near.Invert + near.Size / 2000);
            w.Circle(Lay.KyHieu, fx, fy, 0.35 * w.Th);
            w.Text(Lay.Chu, fx + 0.5 * w.Th, fy + 0.5 * w.Th, 0.6 * w.Th, MepFittings.KindName(f.Kind, f.System), 0, 0, 0);
        }
        // bảng dưới hình: khoảng cách / cao độ đáy ống / độ dốc
        double rowH = 2.2 * w.Th, yT = Y(zRef) - 2 * w.Th, x0 = X(0) - 14 * w.Th;
        string[] heads = { "KHOẢNG CÁCH (m)", "CAO ĐỘ ĐÁY ỐNG", "ĐỘ DỐC i" };
        for (int i = 0; i <= 3; i++) w.Line(Lay.KyHieu, x0, yT - i * rowH, X(sMax) + 2 * w.Th, yT - i * rowH);
        for (int i = 0; i < 3; i++) w.Text(Lay.Chu, x0 + 0.3 * w.Th, yT - (i + 0.5) * rowH, 0.7 * w.Th, heads[i], 0, 0, 2);
        w.Line(Lay.KyHieu, x0, yT, x0, yT - 3 * rowH);
        w.Line(Lay.KyHieu, X(0) - 0.5 * w.Th, yT, X(0) - 0.5 * w.Th, yT - 3 * rowH);
        var shown = new List<DrainProfilePoint>();
        foreach (var q in pts) if (shown.All(o => Math.Abs(o.S - q.S) > 1e-6)) shown.Add(q);
        foreach (var q in shown)
        {
            double x = X(q.S);
            w.Line(Lay.KyHieu, x, yT - rowH, x, yT - 2 * rowH);
            w.Text(Lay.Chu, x, yT - 1.5 * rowH, 0.65 * w.Th, DxfWriter.ElevText(q.Invert), 90, 1, 2);
            w.Line(Lay.KichThuoc, x, Y(q.Invert), x, yT, "DASHED");
        }
        foreach (var sg in segs)
        {
            w.Text(Lay.Chu, X((sg.S0 + sg.S1) / 2), yT - 0.5 * rowH, 0.7 * w.Th, Dt2(sg.Length), 0, 1, 2);
            w.Text(Lay.Chu, X((sg.S0 + sg.S1) / 2), yT - 2.5 * rowH, 0.7 * w.Th, "i=" + Pct(sg.Slope), 0, 1, 2);
        }
        string title = $"{TitlePrefix} {story} – TUYẾN {string.Join("→", chain.Select(r => r.Id))}";
        w.Title(X(sMax / 2), yT - 3 * rowH - 2.5 * w.Th, title, $"tl ngang 1/{S:0}, tl đứng 1/{S / vx:0}");
        return new Figure(w, title);
    }

    /// <summary>Mặt cắt dọc mọi chuỗi tuyến thoát có đoạn ngang.</summary>
    public static List<Figure> Figures(Project p)
    {
        var res = new List<Figure>();
        foreach (var ch in Chains(p))
        {
            Figure f = null;
            try { f = Build(p, ch); } catch (Exception) { }
            if (f != null) res.Add(f);
        }
        return res;
    }
}
