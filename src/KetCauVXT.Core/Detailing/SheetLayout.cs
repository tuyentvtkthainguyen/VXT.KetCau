using System.Text.Json.Serialization;
using KetCauVXT.Drawing;

namespace KetCauVXT.Detailing;

// =====================================================================
// DÀN TRANG IN cho chế độ Triển khai hồ sơ.
// Mỗi hình (mặt bằng, mặt cắt, chi tiết cấu kiện, bảng thống kê) có một mã ổn định (Id) để lưu vị trí trên tờ.
// Toạ độ trên tờ: mm GIẤY, gốc góc trái dưới tờ, trục y hướng lên; (X, Y) = góc trái dưới khung bao hình.
// Mỗi hình in theo tỷ lệ riêng của nó (mặt bằng 1/100, chi tiết 1/50, mặt cắt 1/25…).
// =====================================================================

/// <summary>Một hình đã đặt lên tờ.</summary>
public class PlacedFigure
{
    public string Id { get; set; } = "";
    /// <summary>Góc trái dưới khung bao hình trên tờ (mm giấy).</summary>
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Mẫu số tỷ lệ in của hình trên tờ (0 = tỷ lệ gốc của hàm vẽ). Hình được VẼ LẠI ở tỷ lệ này ⇒ chữ, vòng số hiệu, đầu trục đúng cỡ in.</summary>
    public double Scale { get; set; }
}

/// <summary>Một tờ in.</summary>
public class DetailSheet
{
    /// <summary>Số hiệu tờ (KC-01…), đánh lại khi tự dàn trang / bấm "Đánh số lại".</summary>
    public string No { get; set; } = "KC-01";
    public string Title { get; set; } = "";
    /// <summary>A0 | A1 | A2 | A3 | A4</summary>
    public string Paper { get; set; } = "A1";
    /// <summary>Tỷ lệ chốt cho các hình chính của tờ (0 = tự động theo khổ).</summary>
    public double Scale { get; set; }
    /// <summary>Khổ đứng (mặc định khổ ngang).</summary>
    public bool Portrait { get; set; }
    /// <summary>Tờ người dùng đã chỉnh tay: "Tự dàn trang" giữ nguyên tờ này (hình, vị trí, khổ giấy).</summary>
    public bool Locked { get; set; }
    /// <summary>Tờ danh mục bản vẽ (nội dung sinh tự động theo danh sách tờ).</summary>
    public bool IsIndex { get; set; }
    /// <summary>Tên bản vẽ do người dùng sửa: giữ nguyên khi tự dàn trang / đặt tên tự động.</summary>
    public bool NameLocked { get; set; }
    /// <summary>Số hiệu do người dùng sửa: giữ nguyên khi đánh số lại.</summary>
    public bool NumberLocked { get; set; }
    public List<PlacedFigure> Figures { get; set; } = new();

    [JsonIgnore] public (double W, double H) Size => SheetGeometry.PaperSize(Paper, Portrait);
}

/// <summary>Bộ tờ in của hồ sơ (lưu trong tệp .kcvxth).</summary>
public class DetailSheetSet
{
    /// <summary>Khổ giấy mặc định cho tờ chi tiết.</summary>
    public string Paper { get; set; } = "A1";
    /// <summary>Tự chọn khổ nhỏ nhất (A3 → A2 → A1 → A0) chứa được các hình của mục.</summary>
    public bool AutoPaper { get; set; } = true;
    /// <summary>Khổ lớn nhất khi tự chọn khổ (vượt quá thì hạ tỷ lệ).</summary>
    public string MaxPaper { get; set; } = "A1";
    /// <summary>Hướng giấy: "Ngang" | "Đứng" | "Tự động" (chọn hướng chứa hình tốt hơn).</summary>
    public string Orientation { get; set; } = OrientLandscape;
    /// <summary>Tỷ lệ chốt cho các mặt bằng (0 = tự động: khổ chốt → tỷ lệ lớn nhất vừa khổ; khổ tự động → 1/100 hoặc 1/50).</summary>
    public double PlanScale { get; set; }
    public const string OrientLandscape = "Ngang", OrientPortrait = "Đứng", OrientAuto = "Tự động", PaperAuto = "Tự động";
    public static readonly string[] Orientations = { OrientLandscape, OrientPortrait, OrientAuto };
    /// <summary>Khổ cho tờ mới (tự động → khổ lớn nhất cho phép).</summary>
    [JsonIgnore] public string DefaultPaper => !AutoPaper && SheetGeometry.Papers.Contains(Paper) ? Paper : SheetGeometry.Papers.Contains(MaxPaper) ? MaxPaper : "A1";
    /// <summary>Có tờ danh mục bản vẽ ở đầu bộ.</summary>
    public bool IndexSheet { get; set; } = true;
    /// <summary>Ngày ghi trong khung tên (người dùng nhập; để trống = không ghi).</summary>
    public string Date { get; set; } = "";
    public List<DetailSheet> Sheets { get; set; } = new();

    /// <summary>Khung tên (mặc định / tự tạo / tải lên DWG-DXF) – Detailing/SheetTitleBlock.cs.</summary>
    public DetailTitleBlock TitleBlock { get; set; } = new();

    // ---- đánh số bản vẽ ----
    public const string StyleDash = "KC-01", StyleNoDash = "KC01", StyleGroup = "KC-1.01";
    public static readonly string[] NumberStyles = { StyleDash, StyleNoDash, StyleGroup };
    /// <summary>Tiền tố số hiệu (KC, KT, KCM…).</summary>
    public string NumberPrefix { get; set; } = "KC";
    /// <summary>Mẫu số hiệu: "KC-01" (liên tục), "KC01" (không gạch), "KC-1.01" (theo nhóm mặt bằng – số thứ tự trong nhóm).</summary>
    public string NumberStyle { get; set; } = StyleDash;
    /// <summary>Số bắt đầu (số tờ đầu, hoặc số nhóm đầu với mẫu theo nhóm).</summary>
    public int StartNo { get; set; } = 1;
    /// <summary>Số chữ số của số thứ tự (2 → 01, 3 → 001).</summary>
    public int Digits { get; set; } = 2;

    /// <summary>Số hiệu theo mẫu: n = số thứ tự tờ (từ StartNo), g = số nhóm, i = thứ tự trong nhóm (từ 1).</summary>
    public string FormatNo(int n, int g, int i)
    {
        string pre = (NumberPrefix ?? "").Trim();
        string fmt = new string('0', Math.Clamp(Digits, 1, 4));
        string Num(int v) => v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
        return NumberStyle switch
        {
            StyleNoDash => pre + Num(n),
            StyleGroup => (pre.Length > 0 ? pre + "-" : "") + g.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + Num(i),
            _ => (pre.Length > 0 ? pre + "-" : "") + Num(n)
        };
    }
}

/// <summary>Một hình có thể dàn trang (sinh từ hồ sơ).</summary>
public sealed class SheetFigure
{
    public string Id = "";
    /// <summary>Thứ tự mục hồ sơ (0 = mặt bằng móng … 9 = bảng thống kê).</summary>
    public int Section;
    /// <summary>0 = mặt bằng (và mặt cắt của mặt bằng), 1 = chi tiết cấu kiện thuộc mặt bằng, 2 = bảng thống kê.</summary>
    public int Part;
    /// <summary>Loại cấu kiện (DetailResult.Kind: "Móng đơn", "Dầm"…); "" với mặt bằng / bảng.</summary>
    public string Kind = "";
    /// <summary>Tên mục / tên tờ gợi ý (vd. "CHI TIẾT MÓNG").</summary>
    public string SectionTitle = "";
    /// <summary>Tên hiển thị (vd. "Móng đơn M1 – MẶT CẮT 1-1").</summary>
    public string Caption = "";
    /// <summary>Tên cấu kiện ("" với mặt bằng / bảng).</summary>
    public string Member = "";
    public Figure F;
    /// <summary>Kích thước hình trên giấy (mm) ở tỷ lệ gốc.</summary>
    public double W => F?.W == null ? 0 : F.W.Extents.W / Math.Max(F.W.Scale, 1e-9);
    public double H => F?.W == null ? 0 : F.W.Extents.H / Math.Max(F.W.Scale, 1e-9);

    /// <summary>Vẽ lại hình ở mẫu số tỷ lệ s (null = không vẽ lại được – giữ tỷ lệ gốc).</summary>
    public Func<double, Figure> Redraw;
    private readonly Dictionary<double, Figure> _at = new();
    /// <summary>Mẫu số tỷ lệ gốc của hàm vẽ.</summary>
    public double BaseScale => F?.W == null ? 1 : F.W.Scale;
    public bool CanRescale => Redraw != null && BaseScale > 1.0001;
    /// <summary>Lớp tỷ lệ: 0 mặt bằng (1/50…1/500), 1 mặt đứng – chi tiết (1/20…1/100), 2 mặt cắt – chi tiết nhỏ (1/10…1/50).</summary>
    public int ScaleClass => BaseScale >= 99 ? 0 : BaseScale >= 40 ? 1 : 2;

    /// <summary>Hình ở mẫu số tỷ lệ s (s ≤ 0 hoặc không vẽ lại được → hình gốc). Có bộ đệm.</summary>
    public Figure At(double s)
    {
        if (s <= 0 || !CanRescale || Math.Abs(s - BaseScale) < 1e-9) return F;
        if (!_at.TryGetValue(s, out var f))
        {
            try { f = Redraw(s); } catch (Exception) { f = null; }
            if (f?.W == null || f.W.EntityCount == 0 || f.W.Extents.IsEmpty) f = null;
            _at[s] = f;
        }
        return f ?? F;
    }
    /// <summary>Mẫu số tỷ lệ thực của hình ở yêu cầu s.</summary>
    public double ScaleAt(double s) => At(s)?.W?.Scale ?? BaseScale;
    public double WAt(double s) { var f = At(s); return f?.W == null ? 0 : f.W.Extents.W / Math.Max(f.W.Scale, 1e-9); }
    public double HAt(double s) { var f = At(s); return f?.W == null ? 0 : f.W.Extents.H / Math.Max(f.W.Scale, 1e-9); }
}

/// <summary>Hình học tờ: khổ giấy, vùng vẽ được (trong khung, trừ khung tên).</summary>
public static class SheetGeometry
{
    public const double MarginLeft = 25, MarginOther = 10, TitleW = 180, TitleH = 45;
    /// <summary>Khe hở tối thiểu giữa các hình và tới khung (mm giấy).</summary>
    public const double Gap = 10;
    public static readonly string[] Papers = { "A4", "A3", "A2", "A1", "A0" };
    /// <summary>Dãy tỷ lệ chuẩn (mẫu số).</summary>
    public static readonly double[] StdScales = { 10, 15, 20, 25, 30, 50, 75, 100, 125, 150, 200, 250, 300, 500 };
    /// <summary>Khoảng tỷ lệ hợp lý theo lớp hình (mẫu số nhỏ nhất, lớn nhất).</summary>
    public static (double Lo, double Hi) ClassRange(int cls) => cls switch { 0 => (50, 500), 1 => (20, 200), _ => (10, 100) };
    /// <summary>Tỷ lệ chuẩn gần nhất (≥ s).</summary>
    public static double SnapUp(double s) => StdScales.FirstOrDefault(x => x >= s - 1e-6) is var v && v > 0 ? v : StdScales[^1];

    public static (double W, double H) PaperSize(string paper, bool portrait = false)
    {
        var (w, h) = SheetComposer.Paper(paper);
        return portrait ? (h, w) : (w, h);
    }

    /// <summary>Vùng vẽ được trong khung (đã trừ khe hở).</summary>
    public static Rect2 Drawable(string paper, bool portrait = false)
    {
        var (w, h) = PaperSize(paper, portrait);
        return new Rect2(MarginLeft + Gap, MarginOther + Gap, w - MarginOther - Gap, h - MarginOther - Gap);
    }

    /// <summary>Vùng khung tên (góc phải dưới), đã nới thêm khe hở.</summary>
    public static Rect2 TitleZone(string paper, bool portrait = false)
    {
        var (w, _) = PaperSize(paper, portrait);
        return new Rect2(w - MarginOther - TitleW - Gap, 0, w, MarginOther + TitleH + Gap);
    }

    public static bool Overlap(Rect2 a, Rect2 b, double gap = 0) =>
        a.X1 < b.X2 + gap - 1e-6 && b.X1 < a.X2 + gap - 1e-6 && a.Y1 < b.Y2 + gap - 1e-6 && b.Y1 < a.Y2 + gap - 1e-6;
}

/// <summary>Sinh danh mục hình, tự dàn trang, ghép tờ ra DxfWriter (khung tên + hình đúng tỷ lệ).</summary>
public static class SheetLayoutEngine
{
    public static readonly string[] SectionTitles =
    {
        "MẶT BẰNG KẾT CẤU MÓNG", "CHI TIẾT MÓNG", "MẶT BẰNG ĐỊNH VỊ CỘT", "CHI TIẾT CỘT", "MẶT BẰNG KẾT CẤU TẦNG – CHI TIẾT DẦM",
        "CHI TIẾT SÀN", "CHI TIẾT LANH TÔ – Ô VĂNG", "CHI TIẾT CẦU THANG", "CHI TIẾT BỂ NƯỚC – HỐ PÍT – KẾT CẤU KHÁC", "THỐNG KÊ THÉP – VẬT LIỆU"
    };
    public const string IndexTitle = "DANH MỤC BẢN VẼ";

    /// <summary>Mục hồ sơ của một cấu kiện (theo loại).</summary>
    public static int SectionOf(IDetailMember m, DetailResult r)
    {
        string t = m?.GetType().Name ?? "";
        string k = r?.Kind ?? "";
        if (t is "ManualFooting" or "ManualStrip" or "ManualPileCap" or "RaftInput" || k.StartsWith("Móng")) return 1;
        if (t == "ManualColumn" || k == "Cột") return 3;
        if (t is "ManualBeam" or "PrestressInput" || k.StartsWith("Dầm") || k == "Dự ứng lực") return 4;
        if (t is "ManualSlab" or "ManualSlabPlan" || k.StartsWith("Sàn") || k.StartsWith("Mặt bằng sàn")) return 5;
        if (t == "ManualLintel" || k.StartsWith("Lanh tô")) return 6;
        if (t.Contains("Stair") || k.Contains("thang bộ") || k.Contains("Cầu thang")) return 7;
        return 8;   // bể nước, hố pít thang máy, loại khác
    }

    /// <summary>Mục của một hình mặt bằng (theo tiêu đề).</summary>
    public static int PlanSection(string name)
    {
        if (name == StructuralPlanDrawing.TitleFoundation || name.Contains("MÓNG")) return 0;
        if (name == StructuralPlanDrawing.TitleColumns || name.Contains("CỘT")) return 2;
        if (name.Contains("LANH TÔ") || name.Contains("Ô VĂNG")) return 6;
        if (name.Contains("THANG")) return 7;
        return 4;
    }

    /// <summary>Danh mục mọi hình của hồ sơ theo CÂY THƯ MỤC CÔNG TRÌNH (StructuralPlan.GroupForTree – như cây của cửa sổ hồ sơ):
    /// mỗi mặt bằng (móng, dầm móng, định vị cột, các tầng, lanh tô – ô văng từng tầng) → chi tiết các cấu kiện thuộc mặt bằng đó;
    /// cấu kiện chưa đặt trên mặt bằng xếp theo loại (móng → cột → dầm → sàn → lanh tô → thang → khác); cuối cùng là bảng thống kê.
    /// Section = số thứ tự nhóm; Part 0 = mặt bằng (+ mặt cắt), 1 = chi tiết, 2 = bảng. Mã hình ổn định giữa các lần sinh.</summary>
    public static List<SheetFigure> Catalog(DetailingProject p, List<(IDetailMember Member, DetailResult Result)> results, List<string> log = null)
    {
        log ??= new List<string>();
        var list = new List<SheetFigure>();
        var seen = new HashSet<string>();
        var resOf = new Dictionary<IDetailMember, DetailResult>();
        foreach (var (m, r) in results) if (m != null) resOf[m] = r;
        void Add(SheetFigure f, string groupTitle)
        {
            if (f.F?.W == null || f.F.W.EntityCount == 0 || f.F.W.Extents.IsEmpty) return;
            string id = f.Id; int n = 2;
            while (!seen.Add(f.Id)) f.Id = id + "#" + n++;
            f.SectionTitle = groupTitle;
            list.Add(f);
        }
        var done = new HashSet<IDetailMember>();
        var drawn = new HashSet<string>();
        int sec = 0;
        void Members(IEnumerable<IDetailMember> ms, int s, string title)
        {
            foreach (var m in ms)
            {
                if (m == null || !done.Add(m) || !resOf.TryGetValue(m, out var r)) continue;
                List<Figure> figs;
                try { figs = m.Draw(r, p.Settings) ?? new List<Figure>(); }
                catch (Exception e) { log.Add("Không vẽ được " + m.Name + ": " + e.Message); continue; }
                for (int i = 0; i < figs.Count; i++)
                {
                    int idx = i; var mm = m; var rr0 = r; double b0 = figs[i].W.Scale; string fn = figs[i].Name;
                    Add(new SheetFigure
                    {
                        Id = m.GetType().Name + ":" + m.Name + ":" + (string.IsNullOrEmpty(figs[i].Name) ? i.ToString() : figs[i].Name),
                        Section = s, Part = 1, Member = m.Name, Kind = r?.Kind ?? "",
                        Caption = (r?.Kind ?? "") + " " + m.Name + (string.IsNullOrEmpty(figs[i].Name) ? "" : " – " + figs[i].Name), F = figs[i],
                        // vẽ lại cấu kiện với mọi tỷ lệ nhân cùng hệ số (hình chính b0 → sc)
                        Redraw = sc => Pick(RedrawAt(b0, sc, () => mm.Draw(rr0, p.Settings)), idx, fn)
                    }, title);
                }
            }
        }
        List<PlanTreeGroup> groups;
        try { groups = StructuralPlan.GroupForTree(p); }
        catch (Exception e) { log.Add("Không lập được cây mặt bằng: " + e.Message); groups = new List<PlanTreeGroup>(); }
        foreach (var g in groups.Where(x => x.Kind != "ChuaDat"))
        {
            int s = sec++;
            string title = (g.Title ?? "").ToUpperInvariant();
            if (g.Kind == "Tang" && g.Level != null && g.Level.Items.Count == 0 && g.Members.Count == 0) continue;
            List<Figure> plans;
            try { plans = StructuralPlanDrawing.ForGroup(p, results, g) ?? new List<Figure>(); }
            catch (Exception e) { log.Add("Không vẽ được " + g.Title + ": " + e.Message); plans = new List<Figure>(); }
            foreach (var f in plans)
            {
                string nm = f.Name ?? "";
                if (!drawn.Add(nm)) continue;   // mặt bằng móng dùng chung cho nhóm dầm móng
                var gg = g; double b1 = f.W.Scale;
                Add(new SheetFigure
                {
                    Id = (g.Kind == StructuralPlan.LintelGroupKind ? "MBLT:" : "MB:") + nm, Section = s, Part = 0, Caption = nm, F = f,
                    Redraw = sc => Pick(RedrawAt(b1, sc, () => StructuralPlanDrawing.ForGroup(p, results, gg)), -1, nm)
                }, title);
            }
            Members(g.Members, s, title);
        }
        // cấu kiện chưa đặt trên mặt bằng: theo loại
        var un = results.Select(t => t.Member).Where(m => m != null && !done.Contains(m)).ToList();
        foreach (var grp in un.GroupBy(m => SectionOf(m, resOf.TryGetValue(m, out var rr) ? rr : null)).OrderBy(x => x.Key))
        {
            int s = sec++;
            Members(grp, s, SectionTitles[Math.Clamp(grp.Key, 0, SectionTitles.Length - 1)]);
        }
        int sT = sec;
        try
        {
            var sch = RebarSchedule.BuildFigures(DetailingExport.Summary(results), 1, 30);
            for (int i = 0; i < sch.Count; i++) Add(new SheetFigure { Id = "TKTHEP:" + i, Section = sT, Part = 2, Caption = "Thống kê cốt thép (" + (i + 1) + ")", F = sch[i] }, "THỐNG KÊ CỐT THÉP");
        }
        catch (Exception e) { log.Add("Không lập được thống kê thép: " + e.Message); }
        try
        {
            var mat = MaterialTable.Build(p, results) ?? new List<Figure>();
            for (int i = 0; i < mat.Count; i++) Add(new SheetFigure { Id = "TKVL:" + i, Section = sT, Part = 2, Caption = "Thống kê vật liệu (" + (i + 1) + ")", F = mat[i] }, "THỐNG KÊ VẬT LIỆU");
        }
        catch (Exception e) { log.Add("Không lập được thống kê vật liệu: " + e.Message); }
        return list;
    }

    /// <summary>Gọi hàm vẽ trong phạm vi đổi tỷ lệ: mọi tỷ lệ x → x · sc / b0 (hình chính b0 → sc; mặt cắt kèm theo đổi cùng hệ số).</summary>
    public static List<Figure> RedrawAt(double b0, double sc, Func<List<Figure>> draw)
    {
        double f = sc / Math.Max(b0, 1e-9);
        using (DxfWriter.OverrideScales(x => x * f)) return draw() ?? new List<Figure>();
    }

    /// <summary>Chọn hình theo tên (ưu tiên) hoặc thứ tự.</summary>
    private static Figure Pick(List<Figure> figs, int idx, string name)
    {
        if (figs == null || figs.Count == 0) return null;
        if (!string.IsNullOrEmpty(name))
        {
            var byName = figs.Where(x => x.Name == name).ToList();
            if (byName.Count == 1) return byName[0];
        }
        return idx >= 0 && idx < figs.Count ? figs[idx] : figs.FirstOrDefault(x => x.Name == name);
    }

    // ------------------------------------------------------------------
    // Tên bản vẽ tự động
    // ------------------------------------------------------------------
    /// <summary>Gộp tên cấu kiện: D1, D2, D3, D5 → "D1–D3, D5" (≥ 3 số liên tiếp cùng tiền tố gộp bằng gạch nối).</summary>
    public static string CompressNames(IEnumerable<string> names)
    {
        var l = names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        var parsed = l.Select(n => { var m = System.Text.RegularExpressions.Regex.Match(n, @"^(.*?)(\d+)$"); return (Name: n, Pre: m.Success ? m.Groups[1].Value : n, Num: m.Success ? int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) : -1); }).ToList();
        var outp = new List<string>();
        int i = 0;
        while (i < parsed.Count)
        {
            int j = i;
            if (parsed[i].Num >= 0)
                while (j + 1 < parsed.Count && parsed[j + 1].Pre == parsed[i].Pre && parsed[j + 1].Num == parsed[j].Num + 1) j++;
            if (j - i >= 2) outp.Add(parsed[i].Name + "–" + parsed[j].Name);
            else for (int k = i; k <= j; k++) outp.Add(parsed[k].Name);
            i = j + 1;
        }
        return string.Join(", ", outp);
    }

    /// <summary>Tên tự động của tờ: tờ mặt bằng → tên mặt bằng; tờ chi tiết → "CHI TIẾT MÓNG ĐƠN M1, M2; DẦM D1–D3"; tờ thống kê → tên bảng.</summary>
    public static string AutoTitle(DetailSheet sh, IReadOnlyDictionary<string, SheetFigure> map)
    {
        if (sh.IsIndex) return IndexTitle;
        var figs = sh.Figures.Where(pf => map.ContainsKey(pf.Id)).Select(pf => map[pf.Id]).ToList();
        if (figs.Count == 0) return string.IsNullOrWhiteSpace(sh.Title) ? "BẢN VẼ" : sh.Title;
        var plan = figs.FirstOrDefault(f => f.Part == 0 && !(f.Caption ?? "").StartsWith("MẶT CẮT "));
        if (plan != null) return plan.Caption.ToUpperInvariant();
        if (figs.All(f => f.Part == 2))
        {
            bool steel = figs.Any(f => f.Id.StartsWith("TKTHEP:")), mat = figs.Any(f => f.Id.StartsWith("TKVL:"));
            return steel && mat ? "THỐNG KÊ CỐT THÉP – VẬT LIỆU" : steel ? "THỐNG KÊ CỐT THÉP" : "THỐNG KÊ VẬT LIỆU";
        }
        var det = figs.Where(f => f.Part == 1).ToList();
        if (det.Count == 0) return figs[0].Caption.ToUpperInvariant();
        var parts = det.GroupBy(f => (f.Kind ?? "").Trim()).Select(g => ((g.Key.Length > 0 ? g.Key : "CẤU KIỆN").ToUpperInvariant() + " " + CompressNames(g.Select(f => f.Member))).Trim()).ToList();
        string t = "CHI TIẾT " + string.Join("; ", parts);
        return t.Length > 90 ? t.Substring(0, 88).TrimEnd(',', ' ', ';') + "…" : t;
    }

    /// <summary>Đặt tên tự động mọi tờ chưa khoá tên (tên trùng thêm "(1/2)…").</summary>
    public static void ApplyNames(DetailSheetSet set, IList<SheetFigure> catalog)
    {
        var map = catalog.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
        var auto = set.Sheets.Where(s => !s.NameLocked).ToList();
        foreach (var s in auto) s.Title = AutoTitle(s, map);
        foreach (var g in auto.GroupBy(s => s.Title).Where(g => g.Count() > 1))
        {
            int i = 1, n = g.Count();
            foreach (var s in g) s.Title = s.Title + " (" + i++ + "/" + n + ")";
        }
    }

    // ------------------------------------------------------------------
    // Xếp hình
    // ------------------------------------------------------------------
    /// <summary>Tìm chỗ trống cho hình w × h (mm) trên tờ: ưu tiên cao nhất rồi trái nhất (xếp trái→phải, trên→dưới);
    /// không chồng hình đã đặt (khe ≥ Gap), không đè khung tên. null = không còn chỗ.</summary>
    public static (double X, double Y)? FindSpot(DetailSheet sh, IList<Rect2> placed, double w, double h, DetailSheetSet set = null)
    {
        var area = SheetGeometry.Drawable(sh.Paper, sh.Portrait);
        var tz = TitleZone(set, sh);
        if (w > area.W + 1e-6 || h > area.H + 1e-6) return null;
        var xs = new List<double> { area.X1 };
        var ys = new List<double> { area.Y2 };   // đỉnh hình
        foreach (var r in placed) { xs.Add(r.X2 + SheetGeometry.Gap); xs.Add(r.X1); ys.Add(r.Y1 - SheetGeometry.Gap); ys.Add(r.Y2); }
        xs.Add(tz.X1 - w);
        ys.Add(tz.Y2 + h);
        foreach (double top in ys.Where(y => y <= area.Y2 + 1e-6 && y - h >= area.Y1 - 1e-6).Distinct().OrderByDescending(y => y))
            foreach (double x in xs.Where(x => x >= area.X1 - 1e-6 && x + w <= area.X2 + 1e-6).Distinct().OrderBy(x => x))
            {
                var c = new Rect2(x, top - h, x + w, top);
                if (SheetGeometry.Overlap(c, tz)) continue;
                if (placed.Any(r => SheetGeometry.Overlap(c, r, SheetGeometry.Gap))) continue;
                return (x, top - h);
            }
        return null;
    }

    /// <summary>Vùng khung tên của tờ theo khung tên đang dùng của bộ tờ (mặc định / tự tạo / tải lên).</summary>
    public static Rect2 TitleZone(DetailSheetSet set, DetailSheet sh)
    {
        var (pw, ph) = sh.Size;
        return set?.TitleBlock != null ? set.TitleBlock.Zone(pw, ph) : SheetGeometry.TitleZone(sh.Paper, sh.Portrait);
    }

    /// <summary>Khung bao các hình đã đặt trên tờ (mm giấy).</summary>
    public static List<Rect2> PlacedRects(DetailSheet sh, IReadOnlyDictionary<string, SheetFigure> map)
    {
        var res = new List<Rect2>();
        foreach (var pf in sh.Figures)
            if (map.TryGetValue(pf.Id, out var f)) res.Add(new Rect2(pf.X, pf.Y, pf.X + f.WAt(pf.Scale), pf.Y + f.HAt(pf.Scale)));
        return res;
    }

    /// <summary>Khổ nhỏ nhất chứa được hình lớn nhất của nhóm và tổng diện tích (hệ số lấp đầy 0,55).</summary>
    public static string PaperFor(IEnumerable<SheetFigure> figs, string maxPaper = "A1") => PaperFor(figs.Select(f => (f.W, f.H)), maxPaper);

    /// <summary>Khổ nhỏ nhất (A4 → … → maxPaper) chứa hình lớn nhất (kích thước mm giấy) và tổng diện tích (lấp đầy ≤ 0,55).</summary>
    public static string PaperFor(IEnumerable<(double W, double H)> sizes, string maxPaper = "A1", DetailSheetSet set = null, bool portrait = false)
    {
        var l = sizes.ToList();
        if (l.Count == 0) return "A3";
        double area = l.Sum(f => (f.W + SheetGeometry.Gap) * (f.H + SheetGeometry.Gap));
        int iMax = Array.IndexOf(SheetGeometry.Papers, maxPaper);
        if (iMax < 0) iMax = Array.IndexOf(SheetGeometry.Papers, "A1");
        var big = l.OrderByDescending(f => f.W * f.H).First();
        foreach (var p in SheetGeometry.Papers)
        {
            var d = SheetGeometry.Drawable(p, portrait);
            var tz = set != null ? TitleZone(set, new DetailSheet { Paper = p, Portrait = portrait }) : SheetGeometry.TitleZone(p, portrait);
            double free = d.W * d.H - Math.Max(0, Math.Min(d.X2, tz.X2) - Math.Max(d.X1, tz.X1)) * Math.Max(0, Math.Min(d.Y2, tz.Y2) - Math.Max(d.Y1, tz.Y1));
            bool fitsBig = FindSpot(new DetailSheet { Paper = p, Portrait = portrait }, new List<Rect2>(), big.W, big.H, set) != null;
            bool fitsAll = l.All(f => f.W <= d.W && f.H <= d.H);
            if (fitsBig && fitsAll && area <= 0.55 * free) return p;
            if (p == SheetGeometry.Papers[Math.Max(0, iMax)] && fitsBig) return p;
        }
        return "A0";
    }

    /// <summary>Hình có vừa tờ trống khổ paper (hướng ngang / đứng theo lựa chọn) ở mẫu số s?</summary>
    public static bool Fits(DetailSheetSet set, SheetFigure f, double s, string paper, string orient = null)
    {
        orient ??= set?.Orientation ?? DetailSheetSet.OrientLandscape;
        double w = f.WAt(s), h = f.HAt(s);
        foreach (bool por in orient == DetailSheetSet.OrientAuto ? new[] { false, true } : new[] { orient == DetailSheetSet.OrientPortrait })
            if (FindSpot(new DetailSheet { Paper = paper, Portrait = por }, new List<Rect2>(), w, h, set) != null) return true;
        return false;
    }

    /// <summary>CHỌN TỶ LỆ cho một hình (mẫu số):
    /// • tỷ lệ chốt (tờ / bộ tờ với mặt bằng) → dùng;
    /// • khổ chốt → mặt bằng: tỷ lệ LỚN NHẤT (mẫu số nhỏ nhất trong 1/50…1/500) vừa vùng vẽ trừ khung tên; hình khác: tỷ lệ chuẩn theo loại, hạ nếu không vừa;
    /// • khổ tự động → mặt bằng 1/50 nếu vừa A3, không thì 1/100…; hình khác tỷ lệ chuẩn; vượt khổ lớn nhất cho phép thì hạ tỷ lệ.
    /// 0 = hình không vẽ lại được (giữ tỷ lệ gốc).</summary>
    public static double ChooseScale(DetailSheetSet set, SheetFigure f, double fixedScale = 0, string paper = null, string orient = null)
    {
        if (!f.CanRescale) return 0;
        var (lo, hi) = SheetGeometry.ClassRange(f.ScaleClass);
        var cands = SheetGeometry.StdScales.Where(x => x >= lo - 1e-9 && x <= hi + 1e-9).ToList();
        double pref = Math.Clamp(SheetGeometry.SnapUp(f.BaseScale), lo, hi);
        double fx = fixedScale > 0 ? fixedScale : f.ScaleClass == 0 ? set.PlanScale : 0;
        if (fx > 0) return fx;
        bool autoPaper = paper == null ? set.AutoPaper : paper == DetailSheetSet.PaperAuto;
        string P = paper == null || paper == DetailSheetSet.PaperAuto ? (set.AutoPaper ? set.MaxPaper ?? "A1" : set.Paper) : paper;
        if (!autoPaper)
        {
            var list = f.ScaleClass == 0 ? cands : cands.Where(x => x >= pref - 1e-9).ToList();
            foreach (var sc in list) if (Fits(set, f, sc, P, orient)) return sc;
            return list.Count > 0 ? list[^1] : pref;
        }
        if (f.ScaleClass == 0 && pref >= 100 && Fits(set, f, 50, "A3", orient)) return 50;
        foreach (var sc in cands.Where(x => x >= pref - 1e-9)) if (Fits(set, f, sc, P, orient)) return sc;
        return cands[^1];
    }

    /// <summary>TỰ DÀN TRANG: giữ các tờ đã khoá (và hình trên đó), xếp các hình còn lại theo thứ tự mục hồ sơ;
    /// mỗi mục bắt đầu tờ mới; đánh số lại KC-01… (tờ danh mục là KC-01).</summary>
    public static void AutoLayout(DetailSheetSet set, IList<SheetFigure> catalog, List<string> log = null)
    {
        log ??= new List<string>();
        var map = catalog.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
        var locked = set.Sheets.Where(s => s.Locked && !s.IsIndex).ToList();
        foreach (var s in locked) s.Figures.RemoveAll(pf => !map.ContainsKey(pf.Id));
        var used = new HashSet<string>(locked.SelectMany(s => s.Figures.Select(pf => pf.Id)));
        var fresh = new List<(int Section, DetailSheet S)>();
        // mỗi nhóm (mặt bằng / chi tiết thuộc mặt bằng / bảng) bắt đầu tờ mới
        foreach (var grp in catalog.Where(f => !used.Contains(f.Id)).GroupBy(f => f.Section * 10 + f.Part).OrderBy(g => g.Key))
        {
            var items = grp.ToList();
            // tỷ lệ từng hình theo khổ / tỷ lệ chốt, rồi khổ – hướng giấy
            var scl = items.ToDictionary(f => f.Id, f => ChooseScale(set, f));
            double Wd(SheetFigure f) => f.WAt(scl[f.Id]);
            double Hd(SheetFigure f) => f.HAt(scl[f.Id]);
            var big = items.OrderByDescending(f => Wd(f) * Hd(f)).First();
            bool portrait = set.Orientation == DetailSheetSet.OrientPortrait;
            string paper = set.AutoPaper ? PaperFor(items.Select(f => (Wd(f), Hd(f))), set.MaxPaper ?? "A1", set, portrait) : set.Paper;
            if (set.Orientation == DetailSheetSet.OrientAuto)
            {
                // hướng chứa hình lớn nhất với độ lấp đầy cao hơn
                double Fill(bool por)
                {
                    var d = SheetGeometry.Drawable(paper, por);
                    if (FindSpot(new DetailSheet { Paper = paper, Portrait = por }, new List<Rect2>(), Wd(big), Hd(big), set) == null) return -1;
                    return Math.Max(Wd(big) / d.W, Hd(big) / d.H);
                }
                double fl = Fill(false), fp = Fill(true);
                portrait = fp > fl + 0.05;
                if (set.AutoPaper)
                {
                    string pPor = PaperFor(items.Select(f => (Wd(f), Hd(f))), set.MaxPaper ?? "A1", set, true);
                    if (Array.IndexOf(SheetGeometry.Papers, pPor) < Array.IndexOf(SheetGeometry.Papers, paper)) { paper = pPor; portrait = true; }
                }
            }
            DetailSheet cur = null; List<Rect2> rects = null;
            int part = 0;
            foreach (var f in items)
            {
                double fw = Wd(f), fh = Hd(f);
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (cur == null)
                    {
                        string pp = paper; bool pr = portrait;
                        // hình lớn hơn khổ đã chọn → nâng khổ cho tờ này (thử cả khổ đứng khi hướng tự động)
                        foreach (var cand in SheetGeometry.Papers.SkipWhile(x => x != paper))
                        {
                            pp = cand;
                            if (FindSpot(new DetailSheet { Paper = cand, Portrait = portrait }, new List<Rect2>(), fw, fh, set) != null) { pr = portrait; break; }
                            if (set.Orientation == DetailSheetSet.OrientAuto && FindSpot(new DetailSheet { Paper = cand, Portrait = !portrait }, new List<Rect2>(), fw, fh, set) != null) { pr = !portrait; break; }
                        }
                        cur = new DetailSheet { Paper = pp, Portrait = pr, Title = grp.First().SectionTitle };
                        rects = new List<Rect2>();
                        fresh.Add((grp.Key / 10, cur)); part++;
                    }
                    var spot = FindSpot(cur, rects, fw, fh, set);
                    if (spot is { } sp)
                    {
                        cur.Figures.Add(new PlacedFigure { Id = f.Id, X = sp.X, Y = sp.Y, Scale = scl[f.Id] });
                        rects.Add(new Rect2(sp.X, sp.Y, sp.X + fw, sp.Y + fh));
                        break;
                    }
                    if (cur.Figures.Count == 0)
                    {
                        // quá lớn kể cả trên tờ trống khổ lớn nhất: đặt ở góc trái trên, ghi cảnh báo
                        var a = SheetGeometry.Drawable(cur.Paper, cur.Portrait);
                        cur.Figures.Add(new PlacedFigure { Id = f.Id, X = a.X1, Y = a.Y2 - fh, Scale = scl[f.Id] });
                        rects.Add(new Rect2(a.X1, a.Y2 - fh, a.X1 + fw, a.Y2));
                        log.Add("Hình \"" + f.Caption + "\" lớn hơn khổ " + cur.Paper + " – cần thu tỷ lệ hoặc chọn khổ lớn hơn.");
                        break;
                    }
                    cur = null;   // sang tờ mới cùng mục
                }
            }
        }
        // ghép: tờ khoá giữ chỗ theo mục của hình đầu tiên
        int SecOf(DetailSheet s) => s.Figures.Select(pf => map.TryGetValue(pf.Id, out var f) ? f.Section : 9999).DefaultIfEmpty(9999).Min();
        var all = locked.Select(s => (Section: SecOf(s), S: s, Order: set.Sheets.IndexOf(s))).Concat(fresh.Select((t, i) => (t.Section, t.S, Order: 10000 + i)))
            .OrderBy(t => t.Section).ThenBy(t => t.Order).Select(t => t.S).ToList();
        var oldIndex = set.Sheets.FirstOrDefault(s => s.IsIndex);
        // tờ chưa khoá bố cục nhưng đã sửa tên / số hiệu tay: chuyển tên – số cho tờ mới chứa cùng hình đầu tiên
        foreach (var old in set.Sheets.Where(s => !s.Locked && !s.IsIndex && (s.NameLocked || s.NumberLocked) && s.Figures.Count > 0))
        {
            string first = old.Figures[0].Id;
            var nw = fresh.Select(t => t.S).FirstOrDefault(s => s.Figures.Any(pf => pf.Id == first));
            if (nw == null) continue;
            if (old.NameLocked) { nw.Title = old.Title; nw.NameLocked = true; }
            if (old.NumberLocked) { nw.No = old.No; nw.NumberLocked = true; }
        }
        set.Sheets = all;
        if (set.IndexSheet)
        {
            // khổ tờ danh mục: A3 nếu đủ dòng (7 mm / dòng), không thì khổ lớn hơn
            double need = 12 + 7.0 * (set.Sheets.Count + 2) + SheetGeometry.TitleH;
            string ip = SheetGeometry.Papers.Skip(1).FirstOrDefault(x => SheetGeometry.Drawable(x).H >= need) ?? "A0";
            var ix = new DetailSheet { IsIndex = true, Title = IndexTitle, Paper = ip };
            if (oldIndex != null) { ix.NameLocked = oldIndex.NameLocked; ix.NumberLocked = oldIndex.NumberLocked; if (oldIndex.NameLocked) ix.Title = oldIndex.Title; if (oldIndex.NumberLocked) ix.No = oldIndex.No; }
            set.Sheets.Insert(0, ix);
        }
        ApplyNames(set, catalog);
        Renumber(set, catalog);
    }

    /// <summary>Đánh số lại các tờ chưa khoá số hiệu theo mẫu của bộ tờ (KC-01 / KC01 / KC-1.01 theo nhóm mặt bằng).
    /// Nhóm: tờ danh mục là nhóm 0; các tờ khác theo nhóm cây thư mục (mặt bằng + chi tiết của nó) của hình đầu tiên.</summary>
    public static void Renumber(DetailSheetSet set, IList<SheetFigure> catalog = null)
    {
        var map = (catalog ?? new List<SheetFigure>()).GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
        int start = set.StartNo;
        int group = start - 1, inGroup = 0, lastSec = int.MinValue;
        for (int i = 0; i < set.Sheets.Count; i++)
        {
            var sh = set.Sheets[i];
            int sec = sh.IsIndex ? -1 : sh.Figures.Select(pf => map.TryGetValue(pf.Id, out var f) ? f.Section : int.MaxValue).DefaultIfEmpty(int.MaxValue).Min();
            if (sec == int.MaxValue) sec = lastSec == int.MinValue ? 0 : lastSec;   // tờ trống: theo nhóm trước
            int gNo;
            if (sh.IsIndex) { gNo = 0; inGroup = 0; }
            else
            {
                if (sec != lastSec || lastSec == -1) { group++; inGroup = 0; }
                gNo = group;
            }
            lastSec = sh.IsIndex ? -1 : sec;
            inGroup++;
            if (!sh.NumberLocked) sh.No = set.FormatNo(start + i, gNo, inGroup);
        }
    }

    /// <summary>Lớp tỷ lệ chính của tờ (lớp thô nhất có trên tờ: 0 mặt bằng, 1 chi tiết, 2 mặt cắt).</summary>
    public static int MainClass(DetailSheet sh, IReadOnlyDictionary<string, SheetFigure> map) =>
        sh.Figures.Where(pf => map.ContainsKey(pf.Id)).Select(pf => map[pf.Id].ScaleClass).DefaultIfEmpty(9).Min();

    /// <summary>ĐỔI KHỔ / HƯỚNG / TỶ LỆ của một tờ rồi xếp lại các hình trên tờ:
    /// paper: "Tự động" | A4…A0; orient: Ngang | Đứng | Tự động; scale: 0 = tự động (khổ chốt → tỷ lệ lớn nhất vừa khổ; khổ tự động → tỷ lệ chuẩn, khổ nhỏ nhất).
    /// Tỷ lệ chốt áp cho hình chính của tờ (lớp thô nhất), hình phụ đổi cùng hệ số (làm tròn tỷ lệ chuẩn). Hình không còn chỗ → chưa đặt (ghi log). Tờ được khoá.</summary>
    public static void RefitSheet(DetailSheetSet set, DetailSheet sh, IReadOnlyDictionary<string, SheetFigure> map, string paper, string orient, double scale, List<string> log = null)
    {
        log ??= new List<string>();
        var figs = sh.Figures.Where(pf => map.ContainsKey(pf.Id)).Select(pf => map[pf.Id]).ToList();
        if (figs.Count == 0) { if (paper != DetailSheetSet.PaperAuto) sh.Paper = paper; sh.Portrait = orient == DetailSheetSet.OrientPortrait; return; }
        int main = figs.Min(f => f.ScaleClass);
        var mainF = figs.Where(f => f.ScaleClass == main).OrderByDescending(f => f.W * f.H).First();
        bool autoPaper = paper == DetailSheetSet.PaperAuto;
        string P = autoPaper ? (set.MaxPaper ?? "A1") : paper;
        double sMain = scale > 0 ? scale : ChooseScale(set, mainF, 0, autoPaper ? DetailSheetSet.PaperAuto : P, orient);
        double ratio = sMain > 0 && mainF.CanRescale ? sMain / Math.Max(SheetGeometry.SnapUp(mainF.BaseScale), 1) : 1;
        var scl = new Dictionary<string, double>();
        foreach (var f in figs)
        {
            if (!f.CanRescale) { scl[f.Id] = 0; continue; }
            if (f.ScaleClass == main) scl[f.Id] = scale > 0 ? scale : ChooseScale(set, f, 0, autoPaper ? DetailSheetSet.PaperAuto : P, orient);
            else { var (lo, hi) = SheetGeometry.ClassRange(f.ScaleClass); scl[f.Id] = Math.Clamp(SheetGeometry.SnapUp(SheetGeometry.SnapUp(f.BaseScale) * ratio), lo, hi); }
        }
        var sizes = figs.Select(f => (f.WAt(scl[f.Id]), f.HAt(scl[f.Id]))).ToList();
        bool por = orient == DetailSheetSet.OrientPortrait;
        if (autoPaper) P = PaperFor(sizes, set.MaxPaper ?? "A1", set, por);
        if (orient == DetailSheetSet.OrientAuto)
        {
            var big = sizes.OrderByDescending(t => t.Item1 * t.Item2).First();
            bool okL = FindSpot(new DetailSheet { Paper = P }, new List<Rect2>(), big.Item1, big.Item2, set) != null;
            bool okP = FindSpot(new DetailSheet { Paper = P, Portrait = true }, new List<Rect2>(), big.Item1, big.Item2, set) != null;
            por = !okL && okP || okP && big.Item2 > big.Item1 * 1.15;
        }
        sh.Paper = P; sh.Portrait = por; sh.Scale = scale; sh.Locked = true;
        var order = sh.Figures.ToList();
        sh.Figures.Clear();
        var rects = new List<Rect2>();
        foreach (var pf in order)
        {
            if (!map.TryGetValue(pf.Id, out var f)) continue;
            double sc = scl[f.Id], w = f.WAt(sc), h = f.HAt(sc);
            var spot = FindSpot(sh, rects, w, h, set);
            if (spot == null) { log.Add("Hình \"" + f.Caption + "\" không còn chỗ trên tờ " + sh.No + " – đã chuyển về danh sách chưa đặt."); continue; }
            sh.Figures.Add(new PlacedFigure { Id = f.Id, X = spot.Value.X, Y = spot.Value.Y, Scale = sc });
            rects.Add(new Rect2(spot.Value.X, spot.Value.Y, spot.Value.X + w, spot.Value.Y + h));
        }
    }

    /// <summary>Đặt một hình vào tờ ở chỗ trống đầu tiên (hoặc tại (x, y) nếu cho). Gỡ hình khỏi tờ cũ. false = không còn chỗ.</summary>
    public static bool Place(DetailSheetSet set, DetailSheet target, SheetFigure f, IReadOnlyDictionary<string, SheetFigure> map, double? x = null, double? y = null)
    {
        foreach (var s in set.Sheets) s.Figures.RemoveAll(pf => pf.Id == f.Id);
        double sc = target.Scale > 0 && f.ScaleClass <= MainClass(target, map) ? target.Scale : ChooseScale(set, f, 0, target.Paper);
        if (x.HasValue && y.HasValue) { target.Figures.Add(new PlacedFigure { Id = f.Id, X = x.Value, Y = y.Value, Scale = sc }); return true; }
        var spot = FindSpot(target, PlacedRects(target, map), f.WAt(sc), f.HAt(sc), set);
        if (spot == null) return false;
        target.Figures.Add(new PlacedFigure { Id = f.Id, X = spot.Value.X, Y = spot.Value.Y, Scale = sc });
        return true;
    }

    /// <summary>Các hình chưa nằm trên tờ nào.</summary>
    public static List<SheetFigure> Unplaced(DetailSheetSet set, IList<SheetFigure> catalog)
    {
        var on = new HashSet<string>(set.Sheets.SelectMany(s => s.Figures.Select(pf => pf.Id)));
        return catalog.Where(f => !on.Contains(f.Id)).ToList();
    }

    /// <summary>Bỏ các hình không còn trong hồ sơ (cấu kiện đã xoá / đổi tên).</summary>
    public static int Prune(DetailSheetSet set, IList<SheetFigure> catalog)
    {
        var ids = new HashSet<string>(catalog.Select(f => f.Id));
        return set.Sheets.Sum(s => s.Figures.RemoveAll(pf => !ids.Contains(pf.Id)));
    }

    // ------------------------------------------------------------------
    // Ghép tờ
    // ------------------------------------------------------------------
    /// <summary>Tỷ lệ in ghi trong khung tên (danh sách tỷ lệ các hình, vd. "1/100; 1/50; 1/25").</summary>
    public static string ScaleText(DetailSheet sh, IReadOnlyDictionary<string, SheetFigure> map)
    {
        var sc = sh.Figures.Where(pf => map.ContainsKey(pf.Id)).Select(pf => map[pf.Id].ScaleAt(pf.Scale)).Where(s => s > 1.0001)
            .Distinct().OrderByDescending(s => s).Select(s => "1/" + Math.Round(s)).ToList();
        return string.Join("; ", sc);
    }

    /// <summary>Tờ ra DxfWriter ở tỷ lệ S (= tỷ lệ thô nhất của các hình): khung + khung tên + hình (phóng k = S / tỷ lệ hình).
    /// Toạ độ mô hình = mm giấy × S, gốc góc trái dưới tờ.</summary>
    public static DxfWriter Compose(DetailingProject p, DetailSheetSet set, DetailSheet sh, IReadOnlyDictionary<string, SheetFigure> map)
    {
        double S = sh.Figures.Where(pf => map.ContainsKey(pf.Id)).Select(pf => map[pf.Id].ScaleAt(pf.Scale)).DefaultIfEmpty(100).Max();
        if (S <= 1.0001) S = 100;
        var w = new DxfWriter(S);
        var info = new SheetComposer.SheetInfo
        {
            ProjectName = p?.ProjectName ?? "", Owner = p?.Owner ?? "", Location = p?.Location ?? "", Designer = p?.Designer ?? "",
            Title = sh.Title ?? "", SheetNo = sh.No ?? ""
        };
        var (pw, ph) = sh.Size;
        var tb = set?.TitleBlock;
        if (tb?.ActiveDef != null) tb.Draw(w, pw, ph, tb.ValuesFor(p, set, sh, ScaleText(sh, map)));
        else SheetComposer.Frame(w, pw, ph, info, ScaleText(sh, map), set?.Date ?? "");
        if (sh.IsIndex) w.Append(IndexFigure(set, map, S), 0, 0);
        foreach (var pf in sh.Figures)
        {
            if (!map.TryGetValue(pf.Id, out var f)) continue;
            var fw = f.At(pf.Scale).W;
            double k = S / Math.Max(fw.Scale, 1e-9);   // hình đã vẽ đúng tỷ lệ riêng; k chỉ đổi đơn vị mô hình sang tỷ lệ tờ (cỡ in giữ nguyên)
            var e = fw.Extents;
            double dx = pf.X * S - e.X1 * k, dy = pf.Y * S - e.Y1 * k;
            if (Math.Abs(k - 1) < 1e-9) w.Append(fw, dx, dy); else w.AppendScaled(fw, k, dx, dy);
        }
        return w;
    }

    /// <summary>Bảng danh mục bản vẽ (toạ độ mô hình tỷ lệ S, đặt góc trái trên vùng vẽ tờ A3).</summary>
    public static DxfWriter IndexFigure(DetailSheetSet set, IReadOnlyDictionary<string, SheetFigure> map, double S)
    {
        var w = new DxfWriter(S);
        var idx = set?.Sheets.FirstOrDefault(s => s.IsIndex);
        var (_, ph) = idx?.Size ?? SheetGeometry.PaperSize("A3");
        double x0 = (SheetGeometry.MarginLeft + SheetGeometry.Gap) * S, top = (ph - SheetGeometry.MarginOther - SheetGeometry.Gap) * S;
        double[] cw = { 14, 28, 150, 22, 40 };
        string[] hd = { "STT", "SỐ HIỆU", "TÊN BẢN VẼ", "KHỔ", "TỶ LỆ" };
        double rh = 7 * S, th = 2.5 * S;
        w.Text(Lay.Chu, x0 + cw.Sum() * S / 2, top - 4 * S, 1.6 * th, IndexTitle, 0, 1, 2);
        double y = top - 12 * S;
        var rows = new List<string[]> { hd };
        int i = 1;
        foreach (var s in set?.Sheets ?? new List<DetailSheet>())
            rows.Add(new[] { (i++).ToString(), s.No ?? "", s.Title ?? "", s.Paper + (s.Portrait ? " đứng" : ""), s.IsIndex ? "" : ScaleText(s, map) });
        double tw = cw.Sum() * S;
        w.Line(Lay.KhungTen, x0, y, x0 + tw, y);
        foreach (var row in rows)
        {
            double cx = x0;
            for (int c = 0; c < cw.Length; c++)
            {
                w.Text(Lay.Chu, c == 2 ? cx + 2 * S : cx + cw[c] * S / 2, y - rh / 2, th, row[c], 0, c == 2 ? 0 : 1, 2);
                cx += cw[c] * S;
            }
            y -= rh;
            w.Line(Lay.KhungTen, x0, y, x0 + tw, y);
        }
        double xx = x0;
        for (int c = 0; c <= cw.Length; c++) { w.Line(Lay.KhungTen, xx, top - 12 * S, xx, y); if (c < cw.Length) xx += cw[c] * S; }
        return w;
    }

    /// <summary>Ghép mọi tờ thành các trang (DxfWriter tỷ lệ S, khổ giấy mm) – dùng cho PDF / in / DXF từng tờ.</summary>
    public static List<(DetailSheet Sheet, DxfWriter W, double WMm, double HMm)> Pages(DetailingProject p, DetailSheetSet set, IList<SheetFigure> catalog)
    {
        var map = catalog.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
        var res = new List<(DetailSheet, DxfWriter, double, double)>();
        foreach (var sh in set.Sheets)
        {
            var (pw, ph) = sh.Size;
            res.Add((sh, Compose(p, set, sh, map), pw, ph));
        }
        return res;
    }

    /// <summary>Xuất PDF toàn bộ hồ sơ (nét vector, đúng khổ giấy từng tờ).</summary>
    public static void ExportPdf(DetailingProject p, DetailSheetSet set, IList<SheetFigure> catalog, string path, bool monochrome = false)
    {
        var pages = Pages(p, set, catalog).Select(t => (t.W, 1.0 / t.W.Scale, t.WMm, t.HMm)).ToList();
        PdfVectorWriter.Write(path, pages, (p?.ProjectName ?? "") + " – hồ sơ kết cấu", monochrome);
    }

    /// <summary>Xuất DWG/DXF từng tờ: KC-01_TenTo.dxf… Trả về các file đã ghi.</summary>
    public static List<string> ExportCad(DetailingProject p, DetailSheetSet set, IList<SheetFigure> catalog, string folder, KetCauVXT.Model.CadOutputFormat? fmt = null)
    {
        Directory.CreateDirectory(folder);
        var files = new List<string>();
        foreach (var (sh, w, _, _) in Pages(p, set, catalog))
        {
            string nm = string.Concat(((sh.No ?? "") + "_" + (sh.Title ?? "")).Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch == ' ' ? '_' : ch));
            files.Add(CadFileWriters.Write(w, Path.Combine(folder, nm + ".dxf"), fmt ?? DetailingExport.DefaultFormat));
        }
        return files;
    }
}
