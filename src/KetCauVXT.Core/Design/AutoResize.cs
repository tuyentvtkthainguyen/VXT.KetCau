using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Import;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Design;

/// <summary>Tự tăng tiết diện BTCT cho tới khi đạt: phân tích → thiết kế → cấu kiện không đạt tăng 50 mm (sàn 10 mm) → lặp.
/// <list type="bullet">
/// <item>Cột: tăng cạnh dài h tới 500 mm, sau đó tăng cạnh ngắn b lên 300, rồi tiếp tục tăng h (≤ 800). Cột tầng dưới không nhỏ hơn tầng trên.</item>
/// <item>Dầm: tăng h (≤ 900); khi h/b &gt; 3 hoặc thiếu chỗ đặt thép thì tăng b. Cả nhóm thiết kế (dải dầm) dùng chung tiết diện.</item>
/// <item>Sàn: tăng chiều dày 10 mm (≤ 200).</item>
/// <item>Móng: không đổi (bộ thiết kế móng tự chọn kích thước).</item>
/// </list></summary>
public static class AutoResize
{
    public class Result
    {
        public AnalysisResults Analysis;
        public DesignSummary Design;
        public int Iterations;
        public bool AllOk;
        public List<string> Log { get; } = new();
        public override string ToString() => string.Join("\n", Log);
    }

    public static Result Run(Project p, int maxIter = 10, IProgress<string> progress = null)
    {
        var r = new Result();
        if (p.Combinations.Count == 0) ComboGenerator.Generate(p);
        for (int it = 1; it <= maxIter; it++)
        {
            progress?.Report($"Lần {it}: phân tích...");
            r.Analysis = FrameAnalysis.Run(p);
            progress?.Report($"Lần {it}: thiết kế BTCT...");
            r.Design = ConcreteDesigner.Design(p, r.Analysis);
            r.Iterations = it;
            var badCols = r.Design.Columns.Where(c => !c.Ok).ToList();
            var badBeams = r.Design.Beams.Where(b => !b.Ok).ToList();
            var badSlabs = r.Design.Slabs.Where(s => !s.Ok).ToList();
            if (badCols.Count + badBeams.Count + badSlabs.Count == 0)
            {
                r.AllOk = true;
                r.Log.Add(it == 1 ? "Tất cả dầm, cột, sàn đạt ngay lần đầu – không cần tăng tiết diện." : $"Lần {it}: tất cả dầm, cột, sàn đạt.");
                break;
            }
            if (it == maxIter)
            {
                r.Log.Add($"Dừng sau {maxIter} lần: còn {badCols.Count} cột, {badBeams.Count} dầm, {badSlabs.Count} sàn không đạt – cần xem lại phương án kết cấu.");
                break;
            }
            int nc = 0, nb = 0, ns = 0;
            var changes = new List<string>();
            foreach (var c in badCols) nc += GrowColumn(p, c, changes);
            foreach (var b in badBeams) nb += GrowBeam(p, b, changes);
            foreach (var s in badSlabs) ns += GrowSlab(p, s, changes);
            EnforceColumnContinuity(p);
            p.InvalidateIndex();
            r.Log.Add($"Lần {it}: {badCols.Count} cột, {badBeams.Count} dầm, {badSlabs.Count} sàn không đạt → tăng tiết diện {nc} đoạn cột, {nb} đoạn dầm, {ns} ô sàn"
                      + (changes.Count > 0 ? ": " + string.Join("; ", changes.Distinct().Take(12)) + (changes.Distinct().Count() > 12 ? "…" : "") : ""));
            if (nc + nb + ns == 0)
            {
                r.Log.Add("Không còn tăng được tiết diện (đã tới giới hạn) – cần xem lại phương án kết cấu.");
                break;
            }
        }
        return r;
    }

    private static string Mat(Project p, FrameSection s) => string.IsNullOrEmpty(s.Material) ? p.Concrete.ConcreteColumn : s.Material;

    private static int GrowColumn(Project p, ColumnDesign c, List<string> log)
    {
        // tầng không đạt: theo tỷ số khả năng chịu lực, hàm lượng hoặc thông báo lỗi có tên tầng
        var fails = c.Stories.Where(st =>
            st.Utilization > 1.0 + 1e-6 || st.Ratio > p.Concrete.MaxRatioColumn + 1e-6
            || c.Checks.Any(k => !k.Ok && k.Name.Contains("tầng " + st.Story))
            || c.Messages.Any(m => m.StartsWith("LỖI") && m.Contains("tầng " + st.Story))).ToList();
        if (fails.Count == 0) fails = c.Stories.ToList();
        double zTop = fails.Max(f => f.ZTop);
        int n = 0;
        foreach (var st in c.Stories.Where(s => s.ZTop <= zTop + 1e-6))
        {
            var f = p.Frames.FirstOrDefault(x => x.Id == st.Frame);
            if (f == null) continue;
            var sec = p.Section(f.Section);
            if (sec.Shape != SectionShape.RectConcrete) continue;
            double b = Math.Min(sec.B, sec.H), h = Math.Max(sec.B, sec.H);
            if (h < 0.5 - 1e-6) h += 0.05;
            else if (b < 0.3 - 1e-6) b = 0.30;
            else if (h < 0.8 - 1e-6) h += 0.05;
            else if (b < h - 1e-6) b += 0.05;
            else continue;
            bool hAlongDepth = sec.H >= sec.B;     // giữ hướng cạnh dài
            string name = hAlongDepth ? ImportGeometry.EnsureRect(p, "C", b, h, Mat(p, sec)) : ImportGeometry.EnsureRect(p, "C", h, b, Mat(p, sec));
            if (name == f.Section) continue;
            log.Add($"{c.Name} {st.Story}: {f.Section}→{name}");
            f.Section = name; n++;
        }
        return n;
    }

    private static int GrowBeam(Project p, BeamLineDesign d, List<string> log)
    {
        var frames = p.Frames.Where(f => d.Frames.Contains(f.Id)).ToList();
        if (frames.Count == 0) return 0;
        var sec = p.Section(frames[0].Section);
        if (sec.Shape != SectionShape.RectConcrete) return 0;
        double b = sec.B, h = sec.H;
        bool narrow = d.Messages.Any(m => m.Contains("bề rộng"));
        if (narrow || h / b > 3.0 - 1e-6) b += 0.05;
        if (!narrow) h += 0.05;
        if (h > 0.9 + 1e-6 || b > 0.6 + 1e-6) return 0;
        string name = ImportGeometry.EnsureRect(p, "D", b, h, Mat(p, sec));
        int n = 0;
        foreach (var f in frames) if (f.Section != name) { f.Section = name; n++; }
        if (n > 0) log.Add($"{d.Name}: {sec.Name}→{name}");
        return n;
    }

    private static int GrowSlab(Project p, SlabDesign d, List<string> log)
    {
        var sl = p.Slabs.FirstOrDefault(s => s.Id == d.SlabId);
        if (sl == null) return 0;
        var ss = p.SlabSections.FirstOrDefault(s => s.Name == sl.Section);
        double t = (ss?.Thickness ?? 0.1) + 0.01;
        if (t > 0.2 + 1e-6) return 0;
        string name = ImportGeometry.EnsureSlabSection(p, Math.Round(t, 3), ss?.Material ?? p.Concrete.ConcreteSlab);
        log.Add($"{d.Name}: {sl.Section}→{name}");
        sl.Section = name;
        return 1;
    }

    /// <summary>Cột tầng dưới không nhỏ hơn tầng trên (cùng vị trí).</summary>
    private static void EnforceColumnContinuity(Project p)
    {
        foreach (var g in p.Frames.Where(f => f.Kind == FrameKind.Column && f.IsConcrete)
                     .GroupBy(f => (Math.Round(p.Node(f.NodeI).X, 3), Math.Round(p.Node(f.NodeI).Y, 3))))
        {
            var stack = g.OrderByDescending(f => Math.Max(p.Node(f.NodeI).Z, p.Node(f.NodeJ).Z)).ToList();
            double bMax = 0, hMax = 0;
            foreach (var f in stack)
            {
                var sec = p.Section(f.Section);
                if (sec.Shape != SectionShape.RectConcrete) continue;
                double b = Math.Max(sec.B, bMax), h = Math.Max(sec.H, hMax);
                if (b > sec.B + 1e-6 || h > sec.H + 1e-6) f.Section = ImportGeometry.EnsureRect(p, "C", b, h, Mat(p, sec));
                bMax = b; hMax = h;
            }
        }
    }
}
