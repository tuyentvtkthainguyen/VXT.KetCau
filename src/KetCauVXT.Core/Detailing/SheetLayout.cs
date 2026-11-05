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
}

/// <summary>Một tờ in.</summary>
public class DetailSheet
{
    /// <summary>Số hiệu tờ (KC-01…), đánh lại khi tự dàn trang / bấm "Đánh số lại".</summary>
    public string No { get; set; } = "KC-01";
    public string Title { get; set; } = "";
    /// <summary>A0 | A1 | A2 | A3</summary>
    public string Paper { get; set; } = "A1";
    /// <summary>Khổ đứng (mặc định khổ ngang).</summary>
    public bool Portrait { get; set; }
    /// <summary>Tờ người dùng đã chỉnh tay: "Tự dàn trang" giữ nguyên tờ này (hình, vị trí, khổ giấy).</summary>
    public bool Locked { get; set; }
    /// <summary>Tờ danh mục bản vẽ (nội dung sinh tự động theo danh sách tờ).</summary>
    public bool IsIndex { get; set; }
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
    /// <summary>Có tờ danh mục bản vẽ ở đầu bộ.</summary>
    public bool IndexSheet { get; set; } = true;
    /// <summary>Ngày ghi trong khung tên (người dùng nhập; để trống = không ghi).</summary>
    public string Date { get; set; } = "";
    public List<DetailSheet> Sheets { get; set; } = new();
}

/// <summary>Một hình có thể dàn trang (sinh từ hồ sơ).</summary>
public sealed class SheetFigure
{
    public string Id = "";
    /// <summary>Thứ tự mục hồ sơ (0 = mặt bằng móng … 9 = bảng thống kê).</summary>
    public int Section;
    /// <summary>Tên mục / tên tờ gợi ý (vd. "CHI TIẾT MÓNG").</summary>
    public string SectionTitle = "";
    /// <summary>Tên hiển thị (vd. "Móng đơn M1 – MẶT CẮT 1-1").</summary>
    public string Caption = "";
    /// <summary>Tên cấu kiện ("" với mặt bằng / bảng).</summary>
    public string Member = "";
    public Figure F;
    /// <summary>Kích thước hình trên giấy (mm).</summary>
    public double W => F?.W == null ? 0 : F.W.Extents.W / Math.Max(F.W.Scale, 1e-9);
    public double H => F?.W == null ? 0 : F.W.Extents.H / Math.Max(F.W.Scale, 1e-9);
}

/// <summary>Hình học tờ: khổ giấy, vùng vẽ được (trong khung, trừ khung tên).</summary>
public static class SheetGeometry
{
    public const double MarginLeft = 25, MarginOther = 10, TitleW = 180, TitleH = 45;
    /// <summary>Khe hở tối thiểu giữa các hình và tới khung (mm giấy).</summary>
    public const double Gap = 10;
    public static readonly string[] Papers = { "A3", "A2", "A1", "A0" };

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

    /// <summary>Danh mục mọi hình của hồ sơ, theo thứ tự mục (ổn định giữa các lần sinh).</summary>
    public static List<SheetFigure> Catalog(DetailingProject p, List<(IDetailMember Member, DetailResult Result)> results, List<string> log = null)
    {
        log ??= new List<string>();
        var list = new List<SheetFigure>();
        var seen = new HashSet<string>();
        void Add(SheetFigure f)
        {
            if (f.F?.W == null || f.F.W.EntityCount == 0 || f.F.W.Extents.IsEmpty) return;
            string id = f.Id; int n = 2;
            while (!seen.Add(f.Id)) f.Id = id + "#" + n++;
            f.SectionTitle = SectionTitles[Math.Clamp(f.Section, 0, SectionTitles.Length - 1)];
            list.Add(f);
        }
        List<Figure> plans;
        try { plans = StructuralPlanDrawing.Build(p, results) ?? new List<Figure>(); }
        catch (Exception e) { log.Add("Không vẽ được mặt bằng kết cấu: " + e.Message); plans = new List<Figure>(); }
        int lastPlanSection = 4;
        foreach (var f in plans)
        {
            string nm = f.Name ?? "";
            // mặt cắt sàn đi theo mặt bằng ngay trước nó
            int sec = nm.StartsWith("MẶT CẮT ") ? lastPlanSection : PlanSection(nm);
            if (!nm.StartsWith("MẶT CẮT ")) lastPlanSection = sec;
            Add(new SheetFigure { Id = "MB:" + nm, Section = sec, Caption = nm, F = f });
        }
        foreach (var (m, r) in results)
        {
            List<Figure> figs;
            try { figs = m.Draw(r, p.Settings) ?? new List<Figure>(); }
            catch (Exception e) { log.Add("Không vẽ được " + m.Name + ": " + e.Message); continue; }
            int sec = SectionOf(m, r);
            for (int i = 0; i < figs.Count; i++)
                Add(new SheetFigure
                {
                    Id = m.GetType().Name + ":" + m.Name + ":" + (string.IsNullOrEmpty(figs[i].Name) ? i.ToString() : figs[i].Name),
                    Section = sec, Member = m.Name, Caption = (r?.Kind ?? "") + " " + m.Name + (string.IsNullOrEmpty(figs[i].Name) ? "" : " – " + figs[i].Name), F = figs[i]
                });
        }
        try
        {
            var sch = RebarSchedule.BuildFigures(DetailingExport.Summary(results), 1, 30);
            for (int i = 0; i < sch.Count; i++) Add(new SheetFigure { Id = "TKTHEP:" + i, Section = 9, Caption = "Thống kê cốt thép (" + (i + 1) + ")", F = sch[i] });
        }
        catch (Exception e) { log.Add("Không lập được thống kê thép: " + e.Message); }
        try
        {
            var mat = MaterialTable.Build(p, results) ?? new List<Figure>();
            for (int i = 0; i < mat.Count; i++) Add(new SheetFigure { Id = "TKVL:" + i, Section = 9, Caption = "Thống kê vật liệu (" + (i + 1) + ")", F = mat[i] });
        }
        catch (Exception e) { log.Add("Không lập được thống kê vật liệu: " + e.Message); }
        return list;
    }

    // ------------------------------------------------------------------
    // Xếp hình
    // ------------------------------------------------------------------
    /// <summary>Tìm chỗ trống cho hình w × h (mm) trên tờ: ưu tiên cao nhất rồi trái nhất (xếp trái→phải, trên→dưới);
    /// không chồng hình đã đặt (khe ≥ Gap), không đè khung tên. null = không còn chỗ.</summary>
    public static (double X, double Y)? FindSpot(DetailSheet sh, IList<Rect2> placed, double w, double h)
    {
        var area = SheetGeometry.Drawable(sh.Paper, sh.Portrait);
        var tz = SheetGeometry.TitleZone(sh.Paper, sh.Portrait);
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

    /// <summary>Khung bao các hình đã đặt trên tờ (mm giấy).</summary>
    public static List<Rect2> PlacedRects(DetailSheet sh, IReadOnlyDictionary<string, SheetFigure> map)
    {
        var res = new List<Rect2>();
        foreach (var pf in sh.Figures)
            if (map.TryGetValue(pf.Id, out var f)) res.Add(new Rect2(pf.X, pf.Y, pf.X + f.W, pf.Y + f.H));
        return res;
    }

    /// <summary>Khổ nhỏ nhất chứa được hình lớn nhất của nhóm và tổng diện tích (hệ số lấp đầy 0,55).</summary>
    public static string PaperFor(IEnumerable<SheetFigure> figs, string maxPaper = "A1")
    {
        var l = figs.ToList();
        if (l.Count == 0) return "A3";
        double area = l.Sum(f => (f.W + SheetGeometry.Gap) * (f.H + SheetGeometry.Gap));
        int iMax = Math.Max(0, Array.IndexOf(SheetGeometry.Papers, maxPaper));
        foreach (var p in SheetGeometry.Papers)
        {
            var d = SheetGeometry.Drawable(p);
            bool fitsAll = l.All(f => f.W <= d.W && f.H <= d.H);
            if (fitsAll && area <= 0.55 * d.W * d.H) return p;
            if (p == SheetGeometry.Papers[iMax] && l.All(f => f.W <= d.W && f.H <= d.H)) return p;
        }
        return "A0";
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
        foreach (var grp in catalog.Where(f => !used.Contains(f.Id)).GroupBy(f => f.Section).OrderBy(g => g.Key))
        {
            var items = grp.ToList();
            string paper = set.AutoPaper ? PaperFor(items, set.Paper) : set.Paper;
            DetailSheet cur = null; List<Rect2> rects = null;
            int part = 0;
            foreach (var f in items)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (cur == null)
                    {
                        string pp = paper;
                        // hình lớn hơn khổ đã chọn → nâng khổ cho tờ này
                        foreach (var cand in SheetGeometry.Papers.SkipWhile(x => x != paper))
                        {
                            var d = SheetGeometry.Drawable(cand);
                            pp = cand;
                            if (f.W <= d.W && f.H <= d.H - SheetGeometry.TitleH) break;
                        }
                        cur = new DetailSheet { Paper = pp, Title = grp.First().SectionTitle };
                        rects = new List<Rect2>();
                        fresh.Add((grp.Key, cur)); part++;
                    }
                    var spot = FindSpot(cur, rects, f.W, f.H);
                    if (spot is { } sp)
                    {
                        cur.Figures.Add(new PlacedFigure { Id = f.Id, X = sp.X, Y = sp.Y });
                        rects.Add(new Rect2(sp.X, sp.Y, sp.X + f.W, sp.Y + f.H));
                        break;
                    }
                    if (cur.Figures.Count == 0)
                    {
                        // quá lớn kể cả trên tờ trống khổ lớn nhất: đặt ở góc trái trên, ghi cảnh báo
                        var a = SheetGeometry.Drawable(cur.Paper, cur.Portrait);
                        cur.Figures.Add(new PlacedFigure { Id = f.Id, X = a.X1, Y = a.Y2 - f.H });
                        rects.Add(new Rect2(a.X1, a.Y2 - f.H, a.X1 + f.W, a.Y2));
                        log.Add("Hình \"" + f.Caption + "\" lớn hơn khổ " + cur.Paper + " – cần thu tỷ lệ hoặc chọn khổ lớn hơn.");
                        break;
                    }
                    cur = null;   // sang tờ mới cùng mục
                }
            }
        }
        // ghép: tờ khoá giữ chỗ theo mục của hình đầu tiên
        int SecOf(DetailSheet s) => s.Figures.Select(pf => map.TryGetValue(pf.Id, out var f) ? f.Section : 99).DefaultIfEmpty(99).Min();
        var all = locked.Select(s => (Section: SecOf(s), S: s, Order: set.Sheets.IndexOf(s))).Concat(fresh.Select((t, i) => (t.Section, t.S, Order: 10000 + i)))
            .OrderBy(t => t.Section).ThenBy(t => t.Order).Select(t => t.S).ToList();
        // tên tờ nhiều phần trong cùng mục: "(1/2)"
        foreach (var g in all.Where(s => !s.Locked).GroupBy(s => s.Title).Where(g => g.Count() > 1))
        {
            int i = 1, n = g.Count();
            foreach (var s in g) s.Title = s.Title + " (" + i++ + "/" + n + ")";
        }
        set.Sheets = all;
        if (set.IndexSheet) set.Sheets.Insert(0, new DetailSheet { IsIndex = true, Title = IndexTitle, Paper = "A3" });
        Renumber(set);
    }

    public static void Renumber(DetailSheetSet set)
    {
        for (int i = 0; i < set.Sheets.Count; i++) set.Sheets[i].No = "KC-" + (i + 1).ToString("00");
    }

    /// <summary>Đặt một hình vào tờ ở chỗ trống đầu tiên (hoặc tại (x, y) nếu cho). Gỡ hình khỏi tờ cũ. false = không còn chỗ.</summary>
    public static bool Place(DetailSheetSet set, DetailSheet target, SheetFigure f, IReadOnlyDictionary<string, SheetFigure> map, double? x = null, double? y = null)
    {
        foreach (var s in set.Sheets) s.Figures.RemoveAll(pf => pf.Id == f.Id);
        if (x.HasValue && y.HasValue) { target.Figures.Add(new PlacedFigure { Id = f.Id, X = x.Value, Y = y.Value }); return true; }
        var spot = FindSpot(target, PlacedRects(target, map), f.W, f.H);
        if (spot == null) return false;
        target.Figures.Add(new PlacedFigure { Id = f.Id, X = spot.Value.X, Y = spot.Value.Y });
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
        var sc = sh.Figures.Where(pf => map.ContainsKey(pf.Id)).Select(pf => map[pf.Id].F.W.Scale).Where(s => s > 1.0001)
            .Distinct().OrderByDescending(s => s).Select(s => "1/" + Math.Round(s)).ToList();
        return string.Join("; ", sc);
    }

    /// <summary>Tờ ra DxfWriter ở tỷ lệ S (= tỷ lệ thô nhất của các hình): khung + khung tên + hình (phóng k = S / tỷ lệ hình).
    /// Toạ độ mô hình = mm giấy × S, gốc góc trái dưới tờ.</summary>
    public static DxfWriter Compose(DetailingProject p, DetailSheetSet set, DetailSheet sh, IReadOnlyDictionary<string, SheetFigure> map)
    {
        double S = sh.Figures.Where(pf => map.ContainsKey(pf.Id)).Select(pf => map[pf.Id].F.W.Scale).DefaultIfEmpty(100).Max();
        if (S <= 1.0001) S = 100;
        var w = new DxfWriter(S);
        var info = new SheetComposer.SheetInfo
        {
            ProjectName = p?.ProjectName ?? "", Owner = p?.Owner ?? "", Location = p?.Location ?? "", Designer = p?.Designer ?? "",
            Title = sh.Title ?? "", SheetNo = sh.No ?? ""
        };
        var (pw, ph) = sh.Size;
        SheetComposer.Frame(w, pw, ph, info, ScaleText(sh, map), set?.Date ?? "");
        if (sh.IsIndex) w.Append(IndexFigure(set, map, S), 0, 0);
        foreach (var pf in sh.Figures)
        {
            if (!map.TryGetValue(pf.Id, out var f)) continue;
            var fw = f.F.W;
            double k = S / Math.Max(fw.Scale, 1e-9);
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
