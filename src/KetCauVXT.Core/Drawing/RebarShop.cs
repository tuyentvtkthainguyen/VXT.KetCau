using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

// =====================================================================
// TRIỂN KHAI THÉP GIA CÔNG (SHOP DRAWING)
// Từ BarMark của các cấu kiện đã thiết kế: tách thanh dài hơn cây thép thương phẩm thành các đoạn nối chồng
// (TCVN 5574:2018 10.3.6), gán mã hình dạng uốn, lập danh sách cắt và tối ưu cắt cây 11,7 m theo Ø.
// Số hiệu gia công = số hiệu bản vẽ chi tiết; thanh bị tách nối mang hậu tố a, b, c… (vd "3a", "3b").
// Đơn vị: chiều dài thép mm (trừ khi ghi m), đường kính mm.
// =====================================================================

/// <summary>Tuỳ chọn triển khai gia công.</summary>
public sealed class ShopOptions
{
    /// <summary>Chiều dài cây thép thương phẩm, m</summary>
    public double StockLength { get; set; } = 11.7;
    /// <summary>Đầu thừa ≥ giá trị này (m) coi là tái sử dụng được</summary>
    public double MinReuseOffcut { get; set; } = 1.0;
    /// <summary>Nối so le ≤ 50% số thanh tại một tiết diện</summary>
    public bool Stagger { get; set; } = true;
    /// <summary>Chỉ lấy một loại cấu kiện ("Dầm", "Cột", "Sàn", "Móng"); null/"" = tất cả</summary>
    public string Category { get; set; }
    /// <summary>Trừ độ dãn dài khi uốn vào chiều dài cắt (lượng trừ mỗi chỗ uốn = hệ số × d)</summary>
    public bool BendDeduction { get; set; } = true;
    public double Deduct90 { get; set; } = 2.0;
    public double Deduct135 { get; set; } = 3.0;
    public double Deduct180 { get; set; } = 4.0;
    /// <summary>Nguyên tắc nối (RebarPreferences): hệ số α nối vùng kéo, hệ số so le, cho phép ống nối ren, không nối móng</summary>
    public double LapAlphaTension { get; set; } = 1.2;
    public double StaggerFactor { get; set; } = 1.3;
    public bool AllowCoupler { get; set; } = true;
    public bool NoLapInFootings { get; set; } = true;

    public static ShopOptions From(RebarPreferences p) => p == null ? new ShopOptions() : new ShopOptions
    {
        LapAlphaTension = p.LapAlphaTension > 0 ? p.LapAlphaTension : 1.2, StaggerFactor = Math.Max(p.StaggerFactor, 1.0),
        AllowCoupler = p.AllowCoupler, NoLapInFootings = p.NoLapInFootings,
        StockLength = p.StockLength > 1 ? p.StockLength : 11.7,
        MinReuseOffcut = p.MinReuseOffcut >= 0 ? p.MinReuseOffcut : 1.0,
        Stagger = p.StaggerSplices,
        BendDeduction = p.BendDeduction, Deduct90 = p.Deduct90, Deduct135 = p.Deduct135, Deduct180 = p.Deduct180
    };
}

/// <summary>Một dòng của bảng gia công: một loại thanh (cùng số hiệu gia công) trong một cấu kiện.</summary>
public class ShopBar
{
    /// <summary>"Dầm", "Cột", "Sàn", "Móng", "Đài cọc", "Móng băng"</summary>
    public string Category { get; set; } = "";
    public string Member { get; set; } = "";
    public string Story { get; set; } = "";
    /// <summary>Số hiệu trên bản vẽ chi tiết / bảng thống kê</summary>
    public int ParentMark { get; set; }
    /// <summary>Số hiệu gia công: "3" hoặc "3a", "3b"… khi thanh được tách nối</summary>
    public string Label { get; set; } = "";
    public string Role { get; set; } = "";
    /// <summary>Hình dạng kiểu BarMark: "Thẳng", "L", "U", "Đai", "Đai C", "Móc"</summary>
    public string Shape { get; set; } = "Thẳng";
    /// <summary>Mã hình dạng uốn: 00 thẳng, 11 chữ L, 21 chữ U, 31 thẳng 2 móc 180°, 51 đai kín móc 135°, 52 đai C móc 135°</summary>
    public string ShapeCode { get; set; } = "00";
    public string ShapeName { get; set; } = "Thẳng";
    /// <summary>Kích thước các đoạn (mm) theo thứ tự vẽ; đai: [A, B]</summary>
    public List<double> Segments { get; set; } = new();
    /// <summary>Đoạn móc (mm) – đai/đai C: max(75; 10d) mỗi móc 135°</summary>
    public double HookMm { get; set; }
    public int Dia { get; set; }
    /// <summary>Tổng kích thước ngoài các đoạn (mm) – như bản vẽ chi tiết / bảng thống kê</summary>
    public double OutsideLengthMm { get; set; }
    /// <summary>Lượng trừ độ dãn dài khi uốn (mm)</summary>
    public double BendDeductionMm { get; set; }
    /// <summary>Chiều dài cắt 1 thanh (mm) = kích thước ngoài − độ dãn khi uốn, làm tròn 10 mm</summary>
    public double CutLengthMm { get; set; }
    public int MemberCount { get; set; }
    public int BarsPerMember { get; set; }
    public int BarsTotal => MemberCount * BarsPerMember;
    public double TotalLengthM => Math.Round(BarsTotal * CutLengthMm / 1000.0, 2);
    public double WeightKg => Math.Round(RebarSchedule.UnitWeight(Dia) * BarsTotal * CutLengthMm / 1000.0, 2);
    /// <summary>Thanh thuộc một thanh dài được tách nối chồng</summary>
    public bool Spliced { get; set; }
    /// <summary>Chiều dài nối chồng dùng (mm)</summary>
    public double LapMm { get; set; }
    public string Note { get; set; } = "";
    public string ShapeText => ShapeCode + " " + ShapeName + ": " + string.Join("×", Segments.Select(DxfWriter.N0)) + (HookMm > 0 ? " + móc " + DxfWriter.N0(HookMm) : "");
}

/// <summary>Một cây thép thương phẩm và các đoạn cắt từ nó.</summary>
public class StockBar
{
    public int Dia { get; set; }
    public double StockMm { get; set; }
    public List<(string Label, double Mm)> Pieces { get; } = new();
    public double UsedMm => Pieces.Sum(p => p.Mm);
    public double LeftoverMm => Math.Max(StockMm - UsedMm, 0);
    public string Pattern => string.Join(" + ", Pieces.OrderByDescending(p => p.Mm).ThenBy(p => p.Label, StringComparer.Ordinal)
        .Select(p => p.Label + "(" + DxfWriter.N0(p.Mm) + ")"));
}

/// <summary>Một mẫu cắt lặp lại (các cây cắt giống hệt nhau).</summary>
public class CutPattern
{
    public int Dia { get; set; }
    public int Count { get; set; }
    public List<(string Label, double Mm)> Pieces { get; set; } = new();
    public double LeftoverMm { get; set; }
    public bool Reusable { get; set; }
    public string Text => string.Join(" + ", Pieces.Select(p => p.Label + "(" + DxfWriter.N0(p.Mm) + ")"));
}

/// <summary>Phương án cắt thép cho một đường kính.</summary>
public class DiaCutPlan
{
    public int Dia { get; set; }
    public double StockMm { get; set; }
    public double MinReuseMm { get; set; }
    public List<StockBar> Bars { get; } = new();
    /// <summary>Đoạn dài hơn cây thương phẩm (không cắt được – đặt cây dài riêng)</summary>
    public List<(string Label, double Mm)> Oversize { get; } = new();
    public int StockCount => Bars.Count;
    public int PieceCount => Bars.Sum(b => b.Pieces.Count);
    public double PieceLengthM => Math.Round(Bars.Sum(b => b.UsedMm) / 1000.0, 2);
    public double StockLengthM => Math.Round(Bars.Count * StockMm / 1000.0, 2);
    /// <summary>Tổng đầu thừa ≥ MinReuse (tái sử dụng được), m</summary>
    public double ReusableM => Math.Round(Bars.Where(b => b.LeftoverMm >= MinReuseMm - 1e-6).Sum(b => b.LeftoverMm) / 1000.0, 2);
    /// <summary>Hao hụt thật (đầu thừa &lt; MinReuse), m</summary>
    public double WasteM => Math.Round(Bars.Where(b => b.LeftoverMm < MinReuseMm - 1e-6).Sum(b => b.LeftoverMm) / 1000.0, 2);
    public double WastePct => StockLengthM > 1e-9 ? Math.Round(WasteM / StockLengthM * 100, 2) : 0;
    public double StockWeightKg => Math.Round(RebarSchedule.UnitWeight(Dia) * StockLengthM, 2);
    public double PieceWeightKg => Math.Round(RebarSchedule.UnitWeight(Dia) * PieceLengthM, 2);

    /// <summary>Gộp các cây cắt giống hệt thành mẫu cắt (nhiều cây nhất trước).</summary>
    public List<CutPattern> Patterns()
    {
        return Bars.GroupBy(b => b.Pattern).Select(g => new CutPattern
        {
            Dia = Dia, Count = g.Count(),
            Pieces = g.First().Pieces.OrderByDescending(p => p.Mm).ThenBy(p => p.Label, StringComparer.Ordinal).ToList(),
            LeftoverMm = g.First().LeftoverMm, Reusable = g.First().LeftoverMm >= MinReuseMm - 1e-6
        }).OrderByDescending(c => c.Count).ThenByDescending(c => c.Pieces.Sum(p => p.Mm)).ToList();
    }
}

/// <summary>Kết quả triển khai gia công.</summary>
public class ShopResult
{
    public ShopOptions Options { get; set; } = new();
    public List<ShopBar> Bars { get; } = new();
    public List<DiaCutPlan> Plans { get; } = new();
    public List<string> Messages { get; } = new();
    /// <summary>Mọi mối nối đã đặt (tâm, chiều dài nối, nhóm so le, vùng cho phép, vi phạm) – toạ độ theo cấu kiện (dầm: dọc dải; cột: cao độ từ chân;
    /// sàn / thanh có SpliceAllowed: dọc thanh)</summary>
    public List<SpliceInfo> Splices { get; } = new();
    /// <summary>Số mối nối nằm trong vùng cấm (không tránh được – phải xử lý)</summary>
    public int Violations => Splices.Count(x => x.Violation);
    public IEnumerable<string> Categories => Bars.Select(b => b.Category).Distinct();
    public double TotalWeightKg => Math.Round(Bars.Sum(b => b.WeightKg), 2);
}

public static class RebarShop
{
    public const double Eps = 1e-6;

    // ------------------------------------------------------------------
    // Mã hình dạng
    // ------------------------------------------------------------------

    /// <summary>Mã + tên hình dạng uốn theo hình dạng BarMark.</summary>
    public static (string Code, string Name) ShapeCodeOf(string shape)
    {
        string sh = shape ?? "";
        if (sh.StartsWith("Đai C", StringComparison.Ordinal)) return ("52", "Đai C móc 135°");
        if (sh.StartsWith("Đai", StringComparison.Ordinal)) return ("51", "Đai kín móc 135°");
        if (sh.StartsWith("U", StringComparison.Ordinal)) return ("21", "Chữ U");
        if (sh.StartsWith("L", StringComparison.Ordinal)) return ("11", "Chữ L");
        if (sh.StartsWith("Móc", StringComparison.Ordinal)) return ("31", "Thẳng 2 móc 180°");
        return ("00", "Thẳng");
    }

    /// <summary>Đoạn móc 135° của đai (mm) = max(75; 10d).</summary>
    public static double StirrupHookMm(int d) => Math.Max(75, 10 * d);

    /// <summary>Chiều dài cắt đai kín A×B (mm, kích thước ngoài) gồm 2 móc 135°: 2(A + B) + 2·max(75; 10d), làm tròn 10 mm.</summary>
    public static double ClosedStirrupLengthMm(double a, double b, int d) => R10mm(2 * (a + b) + 2 * StirrupHookMm(d));

    /// <summary>Chiều dài cắt đai C (mm): A + 2·max(75; 10d).</summary>
    public static double CTieLengthMm(double a, int d) => R10mm(a + 2 * StirrupHookMm(d));

    public static double R10mm(double mm) => Math.Round(mm / 10.0, MidpointRounding.AwayFromZero) * 10.0;

    /// <summary>Số chỗ uốn 90° / 135° / 180° theo mã hình dạng: 11 chữ L (1×90°), 21 chữ U (2×90°), 31 hai móc 180°,
    /// 51 đai kín (3×90° + 2 móc 135°), 52 đai C (2 móc 135°), 00 thẳng.</summary>
    public static (int B90, int B135, int B180) Bends(string shapeCode) => shapeCode switch
    {
        "11" => (1, 0, 0),
        "21" => (2, 0, 0),
        "31" => (0, 0, 2),
        "51" => (3, 2, 0),
        "52" => (0, 2, 0),
        _ => (0, 0, 0)
    };

    /// <summary>Độ dãn dài khi uốn (mm) cần trừ khỏi tổng kích thước ngoài: d·(n90·k90 + n135·k135 + n180·k180); 0 khi tắt.</summary>
    public static double BendDeduction(string shapeCode, int d, ShopOptions o)
    {
        if (o == null || !o.BendDeduction) return 0;
        var (a, b, c) = Bends(shapeCode);
        return d * (a * o.Deduct90 + b * o.Deduct135 + c * o.Deduct180);
    }

    // ------------------------------------------------------------------
    // Nối chồng – tách thanh dài
    // ------------------------------------------------------------------

    /// <summary>Tâm các mối nối (m, cùng hệ toạ độ với S/E) cho đoạn thẳng [S; E] có móc đầu legS và móc cuối legE (m),
    /// mỗi đoạn ≤ stock; ưu tiên tâm nối nằm trong các vùng cho phép zones (rỗng = mọi vị trí). forced = có mối nối phải đặt ngoài vùng cho phép.</summary>
    public static List<double> SpliceCenters(double S, double E, double legS, double legE, double lap, double stock,
        IList<(double A, double B)> zones, out bool forced)
    {
        forced = false;
        var cs = new List<double>();
        double a = S;
        bool first = true;
        for (int guard = 0; guard < 100; guard++)
        {
            double lastLen = (E - a) + (first ? legS : 0) + legE;
            if (lastLen <= stock + Eps) break;
            double maxEnd = a + stock - (first ? legS : 0);
            double cMax = Math.Min(maxEnd - lap / 2, E - lap / 2 - 1e-3);
            double cMin = a + 1.5 * lap;
            double best = double.NaN;
            if (zones != null && zones.Count > 0)
                foreach (var (za, zb) in zones)
                {
                    double c = Math.Min(zb, cMax);
                    if (c >= Math.Max(za, cMin) - Eps && (double.IsNaN(best) || c > best)) best = c;
                }
            else best = cMax;
            if (double.IsNaN(best)) { best = cMax; forced = true; }
            if (best < cMin - Eps) best = Math.Max(cMax, a + lap);   // cây quá ngắn so với ll – vẫn tiến
            best = Math.Round(best, 3);
            cs.Add(best);
            a = best - lap / 2;
            first = false;
        }
        return cs;
    }

    /// <summary>Chiều dài thẳng các đoạn (m) theo tâm nối: đoạn i phủ [a_i; b_i], b_i = c_i + ll/2, a_{i+1} = c_i − ll/2.</summary>
    public static List<(double A, double B)> PiecesFromCenters(double S, double E, double lap, IList<double> centers)
    {
        var r = new List<(double, double)>();
        double a = S;
        foreach (double c in centers)
        {
            r.Add((a, c + lap / 2));
            a = c - lap / 2;
        }
        r.Add((a, E));
        return r;
    }

    private static bool Valid(double S, double E, double legS, double legE, double lap, double stock, IList<double> centers,
        IList<(double A, double B)> zones, bool needZone)
    {
        var ps = PiecesFromCenters(S, E, lap, centers);
        for (int i = 0; i < ps.Count; i++)
        {
            double len = ps[i].B - ps[i].A + (i == 0 ? legS : 0) + (i == ps.Count - 1 ? legE : 0);
            if (len > stock + Eps || ps[i].B - ps[i].A < lap - Eps) return false;
        }
        if (needZone && zones != null && zones.Count > 0)
            foreach (double c in centers)
                if (!zones.Any(z => c >= z.A - Eps && c <= z.B + Eps)) return false;
        return true;
    }

    /// <summary>Tâm nối của nhóm thanh so le: dời toàn bộ tâm nối của nhóm A đi ±1,3·ll (ưu tiên vẫn trong vùng cho phép).
    /// Không so le được → trả về null.</summary>
    public static List<double> StaggerCenters(List<double> centersA, double S, double E, double legS, double legE, double lap, double stock,
        IList<(double A, double B)> zones, out bool outsideZone)
    {
        outsideZone = false;
        double sd = RebarDetailing.StaggerDistance(lap);
        foreach (bool needZone in new[] { true, false })
            foreach (double sg in new[] { -1.0, 1.0 })
            {
                var cb = centersA.Select(c => Math.Round(c + sg * sd, 3)).ToList();
                if (Valid(S, E, legS, legE, lap, stock, cb, zones, needZone))
                {
                    outsideZone = !needZone;
                    return cb;
                }
            }
        return null;
    }

    /// <summary>Vùng cho phép nối thép dầm (m, toạ độ dọc dải từ tim gối đầu) theo nguyên tắc nối (SpliceRules.BeamAllowed, dùng tham số của p).</summary>
    public static List<(double A, double B)> BeamSpliceZones(Project p, BeamLineDesign d, bool top, string role) => SpliceRules.BeamAllowed(p, d, top, role);

    // ------------------------------------------------------------------
    // Lập bảng gia công
    // ------------------------------------------------------------------

    private static IEnumerable<(MemberDesignBase M, string Cat)> Members(DesignSummary s)
    {
        foreach (var m in s.Beams) yield return (m, "Dầm");
        foreach (var m in s.Columns) yield return (m, "Cột");
        foreach (var m in s.Slabs) yield return (m, "Sàn");
        foreach (var m in s.Footings) yield return (m, "Móng");
        foreach (var m in s.PileCaps) yield return (m, "Đài cọc");
        foreach (var m in s.StripFootings) yield return (m, "Móng băng");
    }

    /// <summary>Nhóm loại cấu kiện để xuất bản vẽ riêng: Dầm / Cột / Sàn / Móng (móng đơn, đài cọc, móng băng).</summary>
    public static string CategoryGroup(string cat) => cat is "Đài cọc" or "Móng băng" ? "Móng" : cat ?? "";

    private static Material ConcreteFor(Project p, MemberDesignBase m)
    {
        var cs = p.Concrete;
        string mat = null, fallback = cs.ConcreteFooting;
        try
        {
            switch (m)
            {
                case BeamLineDesign b: fallback = cs.ConcreteBeam; mat = p.FrameSections.FirstOrDefault(x => x.Name == b.Section)?.Material; break;
                case ColumnDesign c:
                    fallback = cs.ConcreteColumn;
                    var f = c.Frames.Count > 0 ? p.Frames.FirstOrDefault(x => x.Id == c.Frames[0]) : null;
                    mat = f != null ? p.FrameSections.FirstOrDefault(x => x.Name == f.Section)?.Material : null;
                    break;
                case SlabDesign sd:
                    fallback = cs.ConcreteSlab;
                    var sl = p.Slabs.FirstOrDefault(x => x.Id == sd.SlabId);
                    mat = sl != null ? p.SlabSections.FirstOrDefault(x => x.Name == sl.Section)?.Material : null;
                    break;
            }
        }
        catch (Exception) { }
        return DesignUtil.ConcreteOf(p, mat ?? "", fallback);
    }

    /// <summary>Lập bảng gia công + phương án cắt từ kết quả thiết kế.</summary>
    public static ShopResult Build(Project p, DesignSummary s, ShopOptions o = null)
    {
        o ??= ShopOptions.From(p?.RebarPrefs);
        var res = new ShopResult { Options = o };
        if (p == null || s == null) return res;
        p.InvalidateIndex();
        double stock = o.StockLength > 1 ? o.StockLength : 11.7;
        var main = p.Material(p.Concrete.RebarMain);
        var small = p.Material(p.Concrete.RebarStirrup);
        foreach (var (m, cat) in Members(s))
        {
            if (!string.IsNullOrEmpty(o.Category) && CategoryGroup(cat) != o.Category && cat != o.Category) continue;
            var conc = ConcreteFor(p, m);
            int q = Math.Max(1, m.Quantity);
            // cùng số hiệu → một nhóm (thanh chưa đánh số – Mark = 0 – giữ riêng)
            foreach (var g in m.Bars.Where(x => x.Count > 0 && x.Length > 0 && x.Dia > 0).Select((x, i) => (Bar: x, Key: x.Mark > 0 ? x.Mark : -1 - i))
                         .GroupBy(t => t.Key, t => t.Bar).OrderBy(t => t.Key))
            {
                int mark = Math.Max(g.Key, 0);
                var rows = new List<ShopBar>();
                bool splitAny = false;
                string groupNote = "";
                foreach (var bm in g)
                {
                    var steel = bm.Dia < 10 ? small : main;
                    var pieces = SplitBar(p, o, m, bm, conc, steel, stock, res.Messages, res.Splices, out double lapM, out string spNote);
                    if (!string.IsNullOrEmpty(spNote)) groupNote = spNote;
                    if (pieces.Count > 1 || pieces.Any(x => x.Count != bm.Count)) splitAny = true;
                    foreach (var pc in pieces)
                    {
                        var (code, name) = ShapeCodeOf(pc.Shape);
                        double outside = R10mm(pc.LengthMm), ded = BendDeduction(code, bm.Dia, o);
                        rows.Add(new ShopBar
                        {
                            Category = cat, Member = m.Name, Story = m.Story, ParentMark = bm.Mark, Role = bm.Role, Shape = pc.Shape,
                            ShapeCode = code, ShapeName = name, Segments = pc.Segments, Dia = bm.Dia,
                            OutsideLengthMm = outside, BendDeductionMm = ded, CutLengthMm = R10mm(outside - ded),
                            MemberCount = q, BarsPerMember = pc.Count, HookMm = code is "51" or "52" ? StirrupHookMm(bm.Dia) : 0,
                            Spliced = pieces.Count > 1, LapMm = pieces.Count > 1 ? Math.Round(lapM * 1000) : 0
                        });
                    }
                }
                // gộp các đoạn giống hệt trong cùng số hiệu, gán hậu tố a, b, c…
                var merged = rows.GroupBy(r => r.ShapeCode + "|" + r.CutLengthMm + "|" + string.Join(",", r.Segments.Select(DxfWriter.N0)))
                    .Select(gr => { var f = gr.First(); f.BarsPerMember = gr.Sum(x => x.BarsPerMember); return f; }).ToList();
                if (!splitAny && merged.Count == 1)
                {
                    merged[0].Label = mark.ToString(DrawUtil.Inv);
                    if (!string.IsNullOrEmpty(groupNote)) merged[0].Note = groupNote;
                    res.Bars.Add(merged[0]);
                    continue;
                }
                int k = 0;
                foreach (var r in merged.OrderBy(x => x.Segments.Count > 1 && x.Shape != "Thẳng" ? 0 : 1).ThenByDescending(x => x.CutLengthMm))
                {
                    r.Label = merged.Count == 1 ? mark.ToString(DrawUtil.Inv) : mark.ToString(DrawUtil.Inv) + (char)('a' + Math.Min(k, 25));
                    if (r.Spliced) r.Note = groupNote;
                    k++;
                    res.Bars.Add(r);
                }
            }
        }
        // phương án cắt theo Ø
        foreach (var gd in res.Bars.GroupBy(b => b.Dia).OrderBy(x => x.Key))
        {
            var pcs = gd.Select(b => (Label: b.Member + "/" + b.Label, Mm: b.CutLengthMm, Qty: b.BarsTotal));
            res.Plans.Add(Optimize(gd.Key, pcs, stock * 1000, o.MinReuseOffcut * 1000));
        }
        foreach (var pl in res.Plans.Where(x => x.Oversize.Count > 0))
            res.Messages.Add("Ø" + pl.Dia + ": " + pl.Oversize.Count + " đoạn dài hơn cây " + DxfWriter.N0(stock * 1000) + " mm – đặt cây dài riêng");
        return res;
    }

    /// <summary>Một loại đoạn thanh sau khi tách.</summary>
    internal sealed class Piece
    {
        public string Shape = "Thẳng";
        public List<double> Segments = new();
        public double LengthMm;
        public int Count;
    }

    /// <summary>Kế hoạch mối nối của một thanh dài.</summary>
    public sealed class SplicePlan
    {
        /// <summary>Tâm các mối nối (m) theo thứ tự từ đầu thanh</summary>
        public List<double> Centers { get; } = new();
        /// <summary>Chiều dài nối tại từng mối (m); 0 = ống nối ren</summary>
        public List<double> Laps { get; } = new();
        /// <summary>Mối nối thứ i nằm ngoài vùng cho phép (không tránh được)</summary>
        public List<bool> Bad { get; } = new();
        public List<string> Issues { get; } = new();
        public bool Violation => Bad.Any(x => x);
        public int Couplers => Laps.Count(l => l <= 1e-9);
    }

    /// <summary>Lập mối nối cho đoạn thẳng [S; E] (móc đầu legS, cuối legE, m), mỗi đoạn cắt ≤ stock: toàn bộ chiều dài nối ll nằm TRONG một vùng cho phép
    /// (allowed = null: mọi vị trí; rỗng: không được nối). Không đặt được → ống nối ren tại điểm trong vùng cho phép (allowCoupler),
    /// cuối cùng mới đặt ngoài vùng và ghi vi phạm kèm đề xuất (dời sang vùng khác / cây dài hơn / ống nối ren).</summary>
    public static SplicePlan PlanSplices(double S, double E, double legS, double legE, double lap, double stock,
        IList<(double A, double B)> allowed, bool allowCoupler)
    {
        var plan = new SplicePlan();
        var zones = allowed ?? new List<(double A, double B)> { (S, E) };
        double a = S;
        bool first = true;
        for (int guard = 0; guard < 100; guard++)
        {
            double lead = first ? legS : 0;
            if ((E - a) + lead + legE <= stock + Eps) break;
            double maxEnd = a + stock - lead;
            // 1) nối chồng: [c − ll/2; c + ll/2] ⊂ vùng cho phép, đoạn trước ≥ ll
            double best = double.NaN;
            foreach (var (za, zb) in zones)
            {
                double lo = Math.Max(za + lap / 2, a + 1.5 * lap), hi = Math.Min(Math.Min(zb - lap / 2, maxEnd - lap / 2), E - lap / 2 - 1e-3);
                if (hi >= lo - Eps && (double.IsNaN(best) || hi > best)) best = hi;
            }
            if (!double.IsNaN(best))
            {
                best = Math.Round(best, 3);
                plan.Centers.Add(best); plan.Laps.Add(lap); plan.Bad.Add(false);
                a = best - lap / 2; first = false;
                continue;
            }
            string sugg = Suggest(zones, a, lead, lap, stock);
            // 2) ống nối ren tại một điểm trong vùng cho phép
            if (allowCoupler)
            {
                double cb = double.NaN;
                foreach (var (za, zb) in zones)
                {
                    double lo = Math.Max(za, a + lap), hi = Math.Min(Math.Min(zb, maxEnd), E - 1e-3);
                    if (hi >= lo - Eps && (double.IsNaN(cb) || hi > cb)) cb = hi;
                }
                if (!double.IsNaN(cb))
                {
                    cb = Math.Round(cb, 3);
                    plan.Centers.Add(cb); plan.Laps.Add(0); plan.Bad.Add(false);
                    plan.Issues.Add("vùng cho phép ngắn hơn ll = " + DesignUtil.F(lap * 1000, "0") + " mm – dùng ống nối ren tại x = " + DesignUtil.F(cb, "0.00") + " m" + sugg);
                    a = cb; first = false;
                    continue;
                }
            }
            // 3) không có vị trí hợp lệ trong tầm cây thép → vi phạm (ghi rõ, không đặt lặng lẽ)
            double c = Math.Round(Math.Min(maxEnd - lap / 2, E - lap / 2 - 1e-3), 3);
            plan.Centers.Add(c); plan.Laps.Add(lap); plan.Bad.Add(true);
            plan.Issues.Add("VI PHẠM nguyên tắc nối: mối nối tại x = " + DesignUtil.F(c, "0.00") + " m nằm trong vùng cấm" + sugg);
            a = c - lap / 2; first = false;
        }
        return plan;
    }

    /// <summary>Đề xuất khi không nối chồng được trong tầm cây thép: vùng cho phép kế tiếp đủ dài → cây dài hơn; không có vùng đủ dài → ống nối ren.</summary>
    private static string Suggest(IList<(double A, double B)> zones, double a, double lead, double lap, double stock)
    {
        var ok = zones.Where(z => z.B - z.A >= lap - Eps && z.B - lap / 2 > a + 1.5 * lap).OrderBy(z => z.A).ToList();
        if (ok.Count == 0) return "; đề xuất: ống nối ren (coupler) – không có vùng cho phép đủ dài cho ll";
        var z0 = ok[0];
        double c = Math.Max(z0.A + lap / 2, a + 1.5 * lap);
        double need = c + lap / 2 - a + lead;
        return "; đề xuất: dời mối nối sang vùng " + DesignUtil.F(z0.A, "0.00") + "–" + DesignUtil.F(z0.B, "0.00") + " m dùng cây dài ≥ "
               + DesignUtil.F(RcSection.CeilTo(need, 0.1), "0.0") + " m (cây hiện tại " + DesignUtil.F(stock, "0.0#") + " m), hoặc ống nối ren";
    }

    /// <summary>Đoạn thẳng (m) của các đoạn cắt theo tâm nối và chiều dài nối từng mối (0 = ống nối ren).</summary>
    public static List<(double A, double B)> PiecesFromCenters(double S, double E, IList<double> centers, IList<double> laps)
    {
        var r = new List<(double, double)>();
        double a = S;
        for (int i = 0; i < centers.Count; i++)
        {
            double l = i < laps.Count ? laps[i] : 0;
            r.Add((a, centers[i] + l / 2));
            a = centers[i] - l / 2;
        }
        r.Add((a, E));
        return r;
    }

    private static bool InZone(IList<(double A, double B)> zones, double from, double to) =>
        zones == null || zones.Any(z => from >= z.A - Eps && to <= z.B + Eps);

    /// <summary>Nhóm so le: dời mọi tâm nối của nhóm A đi ±k·ll (vẫn trong vùng cho phép, mỗi đoạn ≤ cây). null nếu không được.</summary>
    private static List<double> Stagger(SplicePlan pa, double S, double E, double legS, double legE, double lap, double stock, double k,
        IList<(double A, double B)> zones)
    {
        double sd = RcSection.CeilTo(k * lap, 0.01);
        foreach (double sg in new[] { -1.0, 1.0 })
        {
            var cb = pa.Centers.Select(c => Math.Round(c + sg * sd, 3)).ToList();
            var ps = PiecesFromCenters(S, E, cb, pa.Laps);
            bool ok = true;
            for (int i = 0; i < ps.Count && ok; i++)
            {
                double len = ps[i].B - ps[i].A + (i == 0 ? legS : 0) + (i == ps.Count - 1 ? legE : 0);
                if (len > stock + Eps || ps[i].B - ps[i].A < lap - Eps) ok = false;
            }
            for (int i = 0; i < cb.Count && ok; i++)
                if (!InZone(zones, cb[i] - pa.Laps[i] / 2, cb[i] + pa.Laps[i] / 2)) ok = false;
            if (ok) return cb;
        }
        return null;
    }

    /// <summary>Vùng cho phép nối của thanh theo nguyên tắc (SpliceRules): null = mọi vị trí, rỗng = không được nối.</summary>
    internal static IList<(double A, double B)> AllowedZones(Project p, MemberDesignBase m, BarMark bm, double S, double E, ShopOptions o, out bool noLap, out string rule)
    {
        noLap = false; rule = "";
        if (bm.SpliceAllowed != null)
        {
            var z = new List<(double, double)>();
            for (int i = 0; i + 1 < bm.SpliceAllowed.Count; i += 2) z.Add((bm.SpliceAllowed[i], bm.SpliceAllowed[i + 1]));
            rule = "theo bố trí cấu kiện";
            return z;
        }
        switch (m)
        {
            case BeamLineDesign bd:
                rule = bm.IsTop ? "thép trên dầm: ⅓ giữa nhịp" : "thép dưới dầm: từ lcr tới Lo/4 cạnh gối";
                return SpliceRules.BeamAllowed(p, bd, bm.IsTop, bm.Role);
            case ColumnDesign cd when cd.Stories.Count > 0:
            {
                double zb = cd.Stories[0].ZBottom;
                rule = "cột: giữa chiều cao thông thuỷ, ngoài nút và lcr";
                return cd.Stories.Select(st => SpliceRules.ColumnAllowed(p, st, zb)).Where(x => x.B - x.A > 1e-3).Select(x => (x.A, x.B)).ToList();
            }
            case KetCauVXT.Design.Foundation.StripFootingDesign sf:
                rule = bm.IsTop ? "sườn móng băng – thép trên: lcr…L/4 cạnh cột" : "sườn móng băng – thép dưới: ⅓ giữa khoảng cột";
                return SpliceRules.StripAllowed(sf, bm.IsTop);
            case FootingDesign:
            case KetCauVXT.Design.Foundation.PileCapDesign:
                noLap = o.NoLapInFootings;
                rule = "móng / đài cọc: không nối";
                return noLap ? new List<(double, double)>() : null;
            case SlabDesign:
            {
                string r = bm.Role ?? "";
                if (r.Contains("mũ")) { rule = "thép mũ sàn: không nối trên gối"; return new List<(double, double)>(); }
                double L = E - S;
                rule = bm.IsTop ? "lưới trên sàn: ⅓ giữa nhịp" : "lưới dưới sàn: trong L/4 cạnh gối";
                return bm.IsTop ? new List<(double, double)> { (S + L / 3, E - L / 3) } : new List<(double, double)> { (S, S + L / 4), (E - L / 4, E) };
            }
        }
        return null;
    }

    /// <summary>Tách một BarMark (Count thanh trong 1 cấu kiện) thành các đoạn ≤ cây thương phẩm theo nguyên tắc nối:
    /// nối chồng ll trong vùng cho phép, so le ≤ 50% số thanh tại một tiết diện, ống nối ren khi vùng ngắn, ghi vi phạm nếu không tránh được.</summary>
    internal static List<Piece> SplitBar(Project p, ShopOptions o, MemberDesignBase m, BarMark bm, Material conc, Material steel, double stock,
        List<string> msgs, List<SpliceInfo> rec, out double lap, out string note)
    {
        lap = 0; note = "";
        var segs = bm.Segments?.ToList() ?? new List<double>();
        string shape = bm.Shape ?? "Thẳng";
        var one = new Piece { Shape = shape, Segments = segs.Count > 0 ? segs : new List<double> { DesignUtil.Mm(bm.Length) }, LengthMm = bm.Length * 1000, Count = bm.Count };
        // chỉ đai / đai C không tách (DrawUtil.IsStirrup coi mọi thanh có bước rải – thép sàn, lưới, móng – là "đai" nên không dùng ở đây)
        bool stirrup = shape.StartsWith("Đai", StringComparison.Ordinal) || (bm.Role ?? "").StartsWith("Đai", StringComparison.Ordinal);
        if (bm.Length <= stock + Eps || stirrup) return new List<Piece> { one };

        lap = RebarDetailing.LapLength(conc, steel, bm.Dia, true, 1.0, o.LapAlphaTension);
        var (hs, he) = DrawUtil.Hooks(bm);
        double legS = hs / 1000.0, legE = he / 1000.0;
        double straight = Math.Max(bm.Length - legS - legE, 0);
        bool memberCoords = bm.SpliceAllowed == null && (m is BeamLineDesign || m is ColumnDesign || m is KetCauVXT.Design.Foundation.StripFootingDesign);
        double S = memberCoords ? Math.Min(bm.Start, bm.End) : 0;
        double E = S + straight;
        string who = m.Name + " – số hiệu " + bm.Mark + " (" + bm.Count + "Ø" + bm.Dia + ", dài " + DesignUtil.F(bm.Length, "0.00") + " m)";
        var zones = AllowedZones(p, m, bm, S, E, o, out bool noLap, out string rule);
        if (noLap)
        {
            msgs.Add(who + ": " + rule + " – dài hơn cây " + DesignUtil.F(stock, "0.0#") + " m, đặt cây dài ≥ " + DesignUtil.F(RcSection.CeilTo(bm.Length, 0.1), "0.0") + " m (không nối)");
            note = "không nối (" + rule + ")";
            return new List<Piece> { one };
        }
        double lapL = lap;
        var pa = PlanSplices(S, E, legS, legE, lapL, stock, zones, o.AllowCoupler);
        foreach (var iss in pa.Issues) msgs.Add(who + ": " + iss);
        int nA = bm.Count, nB = 0;
        List<double> cb = null;
        if (o.Stagger && bm.Count >= 2 && pa.Centers.Count > 0)
        {
            cb = Stagger(pa, S, E, legS, legE, lapL, stock, o.StaggerFactor, zones);
            if (cb != null) { nA = (bm.Count + 1) / 2; nB = bm.Count - nA; }
            else msgs.Add(who + ": không so le được ≥ " + DesignUtil.F(o.StaggerFactor, "0.0#") + "·ll trong vùng cho phép – nối 100% thanh tại một tiết diện (thoả hiệp); đề xuất ống nối ren so le hoặc cây dài hơn");
        }
        void Record(List<double> cs, int count, string grp)
        {
            for (int i = 0; i < cs.Count; i++)
            {
                double l = pa.Laps[i], c = cs[i];
                (double, double) zz = (double.NaN, double.NaN);
                if (zones != null)
                    foreach (var z in zones)
                        if (c - l / 2 >= z.A - Eps && c + l / 2 <= z.B + Eps) { zz = (z.A, z.B); break; }
                rec.Add(new SpliceInfo
                {
                    Member = m.Name, Mark = bm.Mark, Dia = bm.Dia, Span = bm.Span, Center = c, Lap = l, Group = grp, Count = count,
                    ZoneA = zones == null ? S : zz.Item1, ZoneB = zones == null ? E : zz.Item2,
                    Violation = pa.Bad[i] || (zones != null && double.IsNaN(zz.Item1)),
                    Note = l <= 1e-9 ? "ống nối ren" : "nối chồng ll = " + DesignUtil.F(l * 1000, "0") + " mm"
                });
            }
        }
        Record(pa.Centers, nA, "A");
        if (cb != null) Record(cb, nB, "B");
        note = (pa.Couplers > 0 ? pa.Couplers + " ống nối ren; " : "") + "nối chồng ll = " + DesignUtil.F(lapL * 1000, "0") + " mm"
               + (cb != null ? ", so le ≥ " + DesignUtil.F(o.StaggerFactor, "0.0#") + "·ll" : ", không so le")
               + " tại x = " + string.Join("/", pa.Centers.Select(c => DesignUtil.F(c, "0.00"))) + " m"
               + (cb != null ? " & " + string.Join("/", cb.Select(c => DesignUtil.F(c, "0.00"))) + " m" : "")
               + "; vùng cho phép (" + rule + "): " + (zones == null ? "mọi vị trí" : SpliceRules.Describe(zones))
               + (pa.Violation ? "; VI PHẠM – xem ghi chú" : "");
        var list = new List<Piece>();
        void AddPieces(List<double> centers, int count)
        {
            if (count <= 0) return;
            var ps = PiecesFromCenters(S, E, centers, pa.Laps);
            for (int i = 0; i < ps.Count; i++)
            {
                double str = ps[i].B - ps[i].A;
                bool f0 = i == 0, f1 = i == ps.Count - 1;
                double l0 = f0 ? legS : 0, l1 = f1 ? legE : 0;
                var pc = new Piece { Count = count };
                double strMm = DesignUtil.R10(str) * 1000;
                if (l0 > 0 && l1 > 0) { pc.Shape = "U"; pc.Segments = new List<double> { hs, strMm, he }; }
                else if (l0 > 0) { pc.Shape = "L"; pc.Segments = new List<double> { hs, strMm }; }
                else if (l1 > 0) { pc.Shape = "L"; pc.Segments = new List<double> { strMm, he }; }
                else { pc.Shape = "Thẳng"; pc.Segments = new List<double> { strMm }; }
                pc.LengthMm = pc.Segments.Sum();
                list.Add(pc);
            }
        }
        AddPieces(pa.Centers, nA);
        if (cb != null) AddPieces(cb, nB);
        return list;
    }

    // ------------------------------------------------------------------
    // Tối ưu cắt cây
    // ------------------------------------------------------------------

    /// <summary>Tối ưu cắt 1D bằng "first-fit decreasing": xếp các đoạn từ dài đến ngắn, mỗi đoạn vào cây đầu tiên còn đủ chỗ
    /// (tận dụng đầu thừa của cây đã cắt), không có thì mở cây mới. Đầu thừa ≥ minReuseMm báo là tái sử dụng, nhỏ hơn là hao hụt.</summary>
    public static DiaCutPlan Optimize(int dia, IEnumerable<(string Label, double Mm, int Qty)> pieces, double stockMm, double minReuseMm)
    {
        var plan = new DiaCutPlan { Dia = dia, StockMm = stockMm, MinReuseMm = minReuseMm };
        var all = new List<(string Label, double Mm)>();
        foreach (var (lb, mm, qty) in pieces)
            for (int i = 0; i < qty; i++) all.Add((lb, mm));
        all = all.OrderByDescending(x => x.Mm).ThenBy(x => x.Label, StringComparer.Ordinal).ToList();
        var rem = new List<double>();
        foreach (var pc in all)
        {
            if (pc.Mm <= 0) continue;
            if (pc.Mm > stockMm + Eps) { plan.Oversize.Add(pc); continue; }
            int k = -1;
            for (int i = 0; i < rem.Count; i++)
                if (rem[i] >= pc.Mm - Eps) { k = i; break; }
            if (k < 0)
            {
                plan.Bars.Add(new StockBar { Dia = dia, StockMm = stockMm });
                rem.Add(stockMm);
                k = rem.Count - 1;
            }
            plan.Bars[k].Pieces.Add(pc);
            rem[k] -= pc.Mm;
        }
        return plan;
    }
}
