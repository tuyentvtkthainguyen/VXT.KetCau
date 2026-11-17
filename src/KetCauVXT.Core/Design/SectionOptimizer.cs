using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Steel;
using KetCauVXT.Import;
using KetCauVXT.Loads;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Design;

/// <summary>Đơn giá tính chi phí khi tối ưu (đồng). Giá mặc định chỉ để so sánh phương án – sửa theo đơn giá địa phương.</summary>
public class CostRates
{
    /// <summary>Bê tông thương phẩm + đổ, đ/m³</summary>
    public double Concrete { get; set; } = 1_500_000;
    /// <summary>Cốt thép gồm gia công, lắp dựng, hao hụt, đ/kg</summary>
    public double Rebar { get; set; } = 22_000;
    /// <summary>Ván khuôn (lắp + tháo), đ/m²</summary>
    public double Formwork { get; set; } = 180_000;
}

/// <summary>Tuỳ chọn tối ưu tiết diện.</summary>
public class OptimizeOptions
{
    /// <summary>Giữ bề rộng dầm, cạnh ngắn cột theo tường kiến trúc; chỉ đổi chiều cao dầm, cạnh dài cột, chiều dày sàn.
    /// Chỉ tăng bề rộng khi đã hết cách khác (ghi rõ trong kết quả để người dùng quyết định).</summary>
    public bool KeepWidth { get; set; } = true;
    /// <summary>Cấu kiện có tỷ số khả năng chịu lực nhỏ hơn giá trị này được thử giảm tiết diện</summary>
    public double ShrinkBelow { get; set; } = 0.75;
    public int MaxSafetyIterations { get; set; } = 12;
    public int MaxRounds { get; set; } = 8;
    public double MinBeamH { get; set; } = 0.25;
    public double MaxBeamH { get; set; } = 0.90;
    /// <summary>Chiều cao dầm tối thiểu theo nhịp: h ≥ L / giá trị này (dầm chính kinh nghiệm L/12–L/16; lấy L/18 làm cận dưới cấu tạo)</summary>
    public double SpanDepthLimit { get; set; } = 18;
    public double MinColumn { get; set; } = 0.22;
    public double MaxColumnLong { get; set; } = 0.80;
    public double MinSlab { get; set; } = 0.08;
    public double MaxSlab { get; set; } = 0.20;
    /// <summary>Chiều dày sàn tối thiểu theo cạnh ngắn: t ≥ Lngắn / giá trị này</summary>
    public double SlabSpanLimit { get; set; } = 45;
}

/// <summary>Tối ưu tiết diện BTCT theo tổng chi phí bê tông + cốt thép + ván khuôn, bảo đảm mọi dầm, cột, sàn đạt:
/// <list type="number">
/// <item>An toàn: cấu kiện không đạt tăng 50 mm chiều cao dầm / cạnh dài cột, 10 mm sàn (giữ bề rộng theo tường) – phân tích lại sau mỗi lượt.</item>
/// <item>Kinh tế: theo từng nhóm (sàn, dầm, cột) thử giảm một bậc các cấu kiện dư khả năng (tỷ số &lt; 0,75), phân tích + thiết kế lại;
/// cấu kiện không còn đạt được trả lại; giữ thay đổi khi tổng chi phí giảm. Sau đó thử tăng chiều cao dầm nhiều thép (bớt thép) nếu rẻ hơn.</item>
/// </list>
/// Mọi phương án đều được phân tích lại toàn khung (nội lực thay đổi theo độ cứng), không ước lượng.</summary>
public static class SectionOptimizer
{
    public sealed class Eval
    {
        public AnalysisResults Analysis;
        public DesignSummary Design;
        public double ConcreteM3, RebarKg, FormworkM2, Cost;
        public int Bad;
        public string Summary(CostRates r) => $"bê tông {ConcreteM3:0.00} m³, thép {RebarKg:0} kg, ván khuôn {FormworkM2:0.0} m² → {Cost / 1e6:0.00} triệu đồng";
    }

    public sealed class Result
    {
        public Eval Initial;
        public Eval Final;
        public bool AllOk;
        public int Evaluations;
        public List<string> Log { get; } = new();
        /// <summary>Nơi buộc phải tăng bề rộng (cột lồi khỏi tường, dầm rộng hơn tường) – người dùng quyết định</summary>
        public List<string> WidthChanges { get; } = new();
        /// <summary>Tiết diện trước → sau, theo nhóm</summary>
        public List<string> Changes { get; } = new();
        public double Saving => Initial != null && Final != null ? Initial.Cost - Final.Cost : 0;
        public override string ToString() => string.Join("\n", Log);
    }

    public static Result Run(Project p, CostRates rates = null, OptimizeOptions o = null, IProgress<string> progress = null)
    {
        rates ??= p.CostRates ?? new CostRates();
        o ??= new OptimizeOptions();
        var r = new Result();
        if (p.Combinations.Count == 0) ComboGenerator.Generate(p);
        var start = Snapshot(p);

        Eval Run1(string what)
        {
            progress?.Report(what);
            r.Evaluations++;
            return Evaluate(p, rates);
        }

        // ---------------- 1. an toàn ----------------
        var cur = Run1("Phân tích, thiết kế phương án ban đầu...");
        r.Initial = cur;
        r.Log.Add($"Phương án ban đầu: {cur.Bad} cấu kiện không đạt; {cur.Summary(rates)}.");
        for (int it = 1; it <= o.MaxSafetyIterations && cur.Bad > 0; it++)
        {
            var ch = new List<string>();
            int n = 0;
            foreach (var b in cur.Design.Beams.Where(x => !x.Ok)) n += GrowBeam(p, b, o, ch, r.WidthChanges);
            foreach (var c in cur.Design.Columns.Where(x => !x.Ok)) n += GrowColumn(p, c, o, ch, r.WidthChanges);
            foreach (var s in cur.Design.Slabs.Where(x => !x.Ok)) n += GrowSlab(p, s, o, ch);
            AutoResize.EnforceColumnContinuity(p);
            p.InvalidateIndex();
            if (n == 0) { r.Log.Add("Không tăng thêm được tiết diện (đã tới giới hạn) – cần xem lại phương án kết cấu (thêm cột, dầm phụ)."); break; }
            r.Log.Add($"An toàn – lượt {it}: tăng {n} cấu kiện: " + string.Join("; ", ch.Distinct().Take(10)) + (ch.Distinct().Count() > 10 ? "…" : ""));
            cur = Run1($"An toàn – lượt {it}: phân tích lại...");
        }
        if (cur.Bad > 0)
        {
            r.Final = cur; r.AllOk = false;
            r.Log.Add($"Còn {cur.Bad} cấu kiện không đạt – dừng, chưa tối ưu chi phí.");
            return r;
        }
        r.Log.Add($"Đã đạt an toàn: {cur.Summary(rates)}.");

        // ---------------- 2. kinh tế ----------------
        for (int round = 1; round <= o.MaxRounds; round++)
        {
            bool any = false;
            foreach (var cat in new[] { "sàn", "dầm", "cột", "dầm sâu hơn" })
            {
                var before = Snapshot(p);
                var changed = new List<(string Key, string Text)>();
                int n = cat switch
                {
                    "sàn" => ShrinkSlabs(p, cur.Design, o, changed),
                    "dầm" => ShrinkBeams(p, cur.Design, o, changed),
                    "cột" => ShrinkColumns(p, cur.Design, o, changed),
                    _ => DeepenBeams(p, cur.Design, o, changed),
                };
                if (n == 0) continue;
                if (cat == "cột") AutoResize.EnforceColumnContinuity(p);
                p.InvalidateIndex();
                var trial = Run1($"Kinh tế – vòng {round}: thử {(cat == "dầm sâu hơn" ? "tăng chiều cao" : "giảm")} {n} {cat}...");
                if (trial.Bad > 0)
                {
                    // trả lại các nhóm vừa đổi mà nay không đạt, thử lại một lần
                    var failKeys = FailingKeys(trial.Design);
                    var keep = changed.Where(c => !failKeys.Contains(c.Key)).ToList();
                    Restore(p, before);
                    if (keep.Count == 0) continue;
                    changed = new List<(string Key, string Text)>();
                    n = cat switch
                    {
                        "sàn" => ShrinkSlabs(p, cur.Design, o, changed, keep.Select(k => k.Key).ToHashSet()),
                        "dầm" => ShrinkBeams(p, cur.Design, o, changed, keep.Select(k => k.Key).ToHashSet()),
                        "cột" => ShrinkColumns(p, cur.Design, o, changed, keep.Select(k => k.Key).ToHashSet()),
                        _ => DeepenBeams(p, cur.Design, o, changed, keep.Select(k => k.Key).ToHashSet()),
                    };
                    if (n == 0) continue;
                    if (cat == "cột") AutoResize.EnforceColumnContinuity(p);
                    p.InvalidateIndex();
                    trial = Run1($"Kinh tế – vòng {round}: thử lại {n} {cat}...");
                    if (trial.Bad > 0) { Restore(p, before); continue; }
                }
                if (trial.Cost < cur.Cost - 1)
                {
                    r.Log.Add($"Vòng {round} – {cat}: {n} thay đổi, chi phí {cur.Cost / 1e6:0.00} → {trial.Cost / 1e6:0.00} triệu đồng: "
                              + string.Join("; ", changed.Select(c => c.Text).Distinct().Take(8)) + (changed.Count > 8 ? "…" : ""));
                    r.Changes.AddRange(changed.Select(c => c.Text));
                    cur = trial; any = true;
                }
                else Restore(p, before);
            }
            if (!any) { r.Log.Add($"Vòng {round}: không còn thay đổi nào làm giảm chi phí – dừng."); break; }
        }
        // bảo đảm kết quả cuối đúng với tiết diện đang có trong mô hình
        p.InvalidateIndex();
        r.Final = Run1("Phân tích, thiết kế phương án tối ưu...");
        r.AllOk = r.Final.Bad == 0;
        if (!r.AllOk)
        {
            // không xảy ra trong thực tế (mọi thay đổi đã kiểm tra); an toàn: quay về phương án đạt trước đó không còn – báo rõ
            r.Log.Add($"CẢNH BÁO: phương án cuối còn {r.Final.Bad} cấu kiện không đạt – chạy lại tối ưu hoặc tăng tay.");
        }
        r.Log.Add($"Kết quả: {r.Final.Summary(rates)}; so với ban đầu {(r.Saving >= 0 ? "giảm" : "tăng")} {Math.Abs(r.Saving) / 1e6:0.00} triệu đồng"
                  + (r.Initial.Bad > 0 ? " (phương án ban đầu chưa đạt an toàn)" : "") + $"; {r.Evaluations} lần phân tích.");
        if (r.WidthChanges.Count > 0) r.Log.Add("Phải tăng bề rộng (khác bề rộng tường): " + string.Join("; ", r.WidthChanges.Distinct()));
        _ = start;
        return r;
    }

    /// <summary>Phân tích + thiết kế + khối lượng + chi phí của mô hình hiện tại.</summary>
    public static Eval Evaluate(Project p, CostRates rates)
    {
        var e = new Eval();
        e.Analysis = FrameAnalysis.Run(p);
        e.Design = ConcreteDesigner.Design(p, e.Analysis);
        if (p.Frames.Any(f => !f.IsConcrete)) SteelDesigner.Check(p, e.Analysis, e.Design);
        e.Bad = e.Design.Beams.Count(b => !b.Ok) + e.Design.Columns.Count(c => !c.Ok) + e.Design.Slabs.Count(s => !s.Ok);
        try
        {
            var q = QuantityCalculator.Compute(p, e.Design);
            e.ConcreteM3 = q.ConcreteTotal; e.RebarKg = q.RebarTotal; e.FormworkM2 = q.FormworkTotal;
        }
        catch
        {
            // dự phòng: thể tích thanh + thép theo bố trí
            e.ConcreteM3 = p.Frames.Where(f => f.IsConcrete).Sum(f => { var s = p.Section(f.Section); return s.B * s.H * p.Length(f); });
            e.RebarKg = e.Design.All.Sum(m => m.Bars.Sum(b => b.WeightPerBar * b.Count) * Math.Max(1, m.Quantity));
            e.FormworkM2 = p.Frames.Where(f => f.IsConcrete).Sum(f => { var s = p.Section(f.Section); return (s.B + 2 * s.H) * p.Length(f); });
        }
        e.Cost = e.ConcreteM3 * rates.Concrete + e.RebarKg * rates.Rebar + e.FormworkM2 * rates.Formwork;
        return e;
    }

    // ------------------------------------------------------------------ ảnh chụp tiết diện
    private sealed class Snap
    {
        public Dictionary<int, string> Frames = new();
        public Dictionary<int, string> Slabs = new();
    }

    private static Snap Snapshot(Project p) => new()
    {
        Frames = p.Frames.ToDictionary(f => f.Id, f => f.Section),
        Slabs = p.Slabs.ToDictionary(s => s.Id, s => s.Section),
    };

    private static void Restore(Project p, Snap s)
    {
        foreach (var f in p.Frames) if (s.Frames.TryGetValue(f.Id, out var n)) f.Section = n;
        foreach (var sl in p.Slabs) if (s.Slabs.TryGetValue(sl.Id, out var n)) sl.Section = n;
        p.InvalidateIndex();
    }

    private static HashSet<string> FailingKeys(DesignSummary d)
    {
        var k = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in d.Beams.Where(x => !x.Ok)) k.Add(BeamKey(b));
        foreach (var c in d.Columns.Where(x => !x.Ok)) foreach (var st in c.Stories) k.Add("C:" + st.Frame);
        foreach (var s in d.Slabs.Where(x => !x.Ok)) k.Add("S:" + s.SlabId);
        return k;
    }

    private static string BeamKey(BeamLineDesign b) => "D:" + string.Join(",", b.Frames.OrderBy(x => x));
    private static string Mat(Project p, FrameSection s) => string.IsNullOrEmpty(s.Material) ? p.Concrete.ConcreteBeam : s.Material;
    private static double Step(double v, double step) => Math.Round(v / step) * step;
    private static double CeilStep(double v, double step) => Math.Ceiling(v / step - 1e-9) * step;

    // ------------------------------------------------------------------ dầm
    private static double MinBeamH(BeamLineDesign d, Project p, List<FrameElement> frames, OptimizeOptions o)
    {
        double span = d.Spans.Count > 0 ? d.Spans.Max(s => s.L) : frames.Max(f => p.Length(f));
        return Math.Max(o.MinBeamH, CeilStep(span / o.SpanDepthLimit, 0.05));
    }

    private static int SetBeam(Project p, List<FrameElement> frames, double b, double h, string mat)
    {
        string name = ImportGeometry.EnsureRect(p, "D", Step(b, 0.005), Step(h, 0.005), mat);
        int n = 0;
        foreach (var f in frames) if (f.Section != name) { f.Section = name; n++; }
        return n;
    }

    private static int GrowBeam(Project p, BeamLineDesign d, OptimizeOptions o, List<string> log, List<string> width)
    {
        var frames = p.Frames.Where(f => d.Frames.Contains(f.Id)).ToList();
        if (frames.Count == 0) return 0;
        var sec = p.Section(frames[0].Section);
        if (sec.Shape != SectionShape.RectConcrete) return 0;
        double b = sec.B, h = sec.H;
        bool narrow = d.Messages.Any(m => m.Contains("bề rộng"));
        if (!narrow && h + 0.05 <= o.MaxBeamH + 1e-6) h += 0.05;
        else if (!o.KeepWidth || narrow || h + 0.05 > o.MaxBeamH + 1e-6)
        {
            if (b + 0.05 > 0.6 + 1e-6) return 0;
            b += 0.05;
            width.Add($"{d.Name}: bề rộng {sec.B * 1000:0}→{b * 1000:0} mm");
        }
        int n = SetBeam(p, frames, b, h, Mat(p, sec));
        if (n > 0) log.Add($"{d.Name}: {sec.Name}→{frames[0].Section}");
        return n;
    }

    private static int ShrinkBeams(Project p, DesignSummary ds, OptimizeOptions o, List<(string, string)> changed, HashSet<string> only = null)
    {
        int n = 0;
        foreach (var d in ds.Beams)
        {
            string key = BeamKey(d);
            if (only != null ? !only.Contains(key) : (!d.Ok || d.MaxRatio >= o.ShrinkBelow)) continue;
            var frames = p.Frames.Where(f => d.Frames.Contains(f.Id)).ToList();
            if (frames.Count == 0) continue;
            var sec = p.Section(frames[0].Section);
            if (sec.Shape != SectionShape.RectConcrete) continue;
            double h = sec.H - 0.05;
            if (h < MinBeamH(d, p, frames, o) - 1e-6 || h < sec.B - 1e-6) continue;
            if (SetBeam(p, frames, sec.B, h, Mat(p, sec)) > 0) { n++; changed.Add((key, $"{d.Name} {sec.Name}→{frames[0].Section}")); }
        }
        return n;
    }

    /// <summary>Dầm dùng nhiều thép (tỷ số ≥ 0,9 hoặc hàm lượng thép chịu kéo lớn): thử tăng 50 mm – có thể bớt thép, rẻ hơn.</summary>
    private static int DeepenBeams(Project p, DesignSummary ds, OptimizeOptions o, List<(string, string)> changed, HashSet<string> only = null)
    {
        int n = 0;
        foreach (var d in ds.Beams)
        {
            string key = BeamKey(d);
            var frames = p.Frames.Where(f => d.Frames.Contains(f.Id)).ToList();
            if (frames.Count == 0) continue;
            var sec = p.Section(frames[0].Section);
            if (sec.Shape != SectionShape.RectConcrete) continue;
            double steel = d.Bars.Sum(b => b.WeightPerBar * b.Count);
            double conc = sec.B * sec.H * frames.Sum(f => p.Length(f));
            bool heavy = conc > 1e-6 && steel / conc > 160;   // > 160 kg thép / m³ bê tông dầm
            if (only != null ? !only.Contains(key) : (!d.Ok || !(heavy || d.MaxRatio >= 0.9))) continue;
            if (sec.H + 0.05 > o.MaxBeamH + 1e-6) continue;
            if (SetBeam(p, frames, sec.B, sec.H + 0.05, Mat(p, sec)) > 0) { n++; changed.Add((key, $"{d.Name} {sec.Name}→{frames[0].Section} (bớt thép)")); }
        }
        return n;
    }

    // ------------------------------------------------------------------ cột
    private static string SetColumn(Project p, FrameElement f, FrameSection sec, double b, double h)
    {
        bool hAlongDepth = sec.H >= sec.B;   // giữ hướng cạnh dài
        string mat = string.IsNullOrEmpty(sec.Material) ? p.Concrete.ConcreteColumn : sec.Material;
        string name = hAlongDepth ? ImportGeometry.EnsureRect(p, "C", Step(b, 0.005), Step(h, 0.005), mat) : ImportGeometry.EnsureRect(p, "C", Step(h, 0.005), Step(b, 0.005), mat);
        f.Section = name;
        return name;
    }

    private static int GrowColumn(Project p, ColumnDesign c, OptimizeOptions o, List<string> log, List<string> width)
    {
        var fails = c.Stories.Where(st => st.Utilization > 1.0 + 1e-6 || st.Ratio > p.Concrete.MaxRatioColumn + 1e-6
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
            if (h + 0.05 <= o.MaxColumnLong + 1e-6) h += 0.05;
            else if (b + 0.05 <= h + 1e-6)
            {
                b += 0.05;
                width.Add($"{c.Name} {st.Story}: cạnh ngắn {Math.Min(sec.B, sec.H) * 1000:0}→{b * 1000:0} mm (cột lồi khỏi tường)");
            }
            else continue;
            string old = f.Section;
            if (SetColumn(p, f, sec, b, h) != old) { log.Add($"{c.Name} {st.Story}: {old}→{f.Section}"); n++; }
        }
        return n;
    }

    private static int ShrinkColumns(Project p, DesignSummary ds, OptimizeOptions o, List<(string, string)> changed, HashSet<string> only = null)
    {
        int n = 0;
        foreach (var c in ds.Columns)
            foreach (var st in c.Stories)
            {
                string key = "C:" + st.Frame;
                if (only != null ? !only.Contains(key) : (!c.Ok || st.Utilization >= o.ShrinkBelow || st.Utilization <= 0)) continue;
                var f = p.Frames.FirstOrDefault(x => x.Id == st.Frame);
                if (f == null) continue;
                var sec = p.Section(f.Section);
                if (sec.Shape != SectionShape.RectConcrete) continue;
                double b = Math.Min(sec.B, sec.H), h = Math.Max(sec.B, sec.H) - 0.05;
                if (h < Math.Max(b, o.MinColumn) - 1e-6) continue;
                string old = f.Section;
                if (SetColumn(p, f, sec, b, h) != old) { n++; changed.Add((key, $"{c.Name} {st.Story} {old}→{f.Section}")); }
            }
        return n;
    }

    // ------------------------------------------------------------------ sàn
    private static int GrowSlab(Project p, SlabDesign d, OptimizeOptions o, List<string> log)
    {
        var sl = p.Slabs.FirstOrDefault(s => s.Id == d.SlabId);
        if (sl == null) return 0;
        var ss = p.SlabSections.FirstOrDefault(s => s.Name == sl.Section);
        double t = Math.Round((ss?.Thickness ?? 0.1) + 0.01, 3);
        if (t > o.MaxSlab + 1e-6) return 0;
        string name = ImportGeometry.EnsureSlabSection(p, t, ss?.Material ?? p.Concrete.ConcreteSlab);
        log.Add($"{d.Name}: {sl.Section}→{name}");
        sl.Section = name;
        return 1;
    }

    private static int ShrinkSlabs(Project p, DesignSummary ds, OptimizeOptions o, List<(string, string)> changed, HashSet<string> only = null)
    {
        int n = 0;
        foreach (var d in ds.Slabs)
        {
            string key = "S:" + d.SlabId;
            if (only != null ? !only.Contains(key) : (!d.Ok || d.MaxRatio >= o.ShrinkBelow)) continue;
            var sl = p.Slabs.FirstOrDefault(s => s.Id == d.SlabId);
            if (sl == null) continue;
            var ss = p.SlabSections.FirstOrDefault(s => s.Name == sl.Section);
            if (ss == null) continue;
            double t = Math.Round(ss.Thickness - 0.01, 3);
            double tMin = Math.Max(o.MinSlab, CeilStep(d.Lx / o.SlabSpanLimit, 0.01));
            if (t < tMin - 1e-6) continue;
            string name = ImportGeometry.EnsureSlabSection(p, t, ss.Material);
            if (name == sl.Section) continue;
            changed.Add((key, $"{d.Name} {sl.Section}→{name}"));
            sl.Section = name; n++;
        }
        return n;
    }
}
