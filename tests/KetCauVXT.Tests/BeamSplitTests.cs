using KetCauVXT.Design;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Test 69 – Chia mặt đứng dầm nhiều đoạn để dàn trang vừa khổ giấy (tự động / không chia / n nhịp mỗi đoạn / tại các trục),
/// giữ số hiệu thép, mặt cắt vẽ một lần; xuất hồ sơ dùng chung bộ dàn trang (không đè khung tên, độ lấp đầy tờ chi tiết).</summary>
public static class BeamSplitTests
{
    private static double B(bool b) => b ? 1 : 0;
    private static bool IsStirrup(BarMark b) => (b.Shape ?? "").StartsWith("Đai") || (b.Role ?? "").StartsWith("Đai") || b.Spacing > 0;
    private static double TitleL(string t)
    {
        var m = System.Text.RegularExpressions.Regex.Match(t ?? "", @"L=(\d+)");
        return m.Success ? double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "TrienKhai", "ChiaDam");
        Directory.CreateDirectory(dir);

        var beam = new ManualBeam { Name = "D5", Spans = "5*4,5" };
        var p = new DetailingProject { ProjectName = "Thử chia dầm" };
        p.Beams.Add(beam);
        var res = p.RunAll();
        var r = res.First(t => t.Member == beam).Result;
        var d = (BeamLineDesign)r.Source;
        check("Dầm 5 nhịp × 4,5 m", d.Spans.Count, 5, 0);

        // ---- tự động trên A3 1/50 ----
        var set = new DetailSheetSet { AutoPaper = false, Paper = "A3" };
        var cat = SheetLayoutEngine.Catalog(p, res, null, set);
        var segs = cat.Where(f => f.Member == "D5" && f.Segment > 0).OrderBy(f => f.Segment).ToList();
        check("A3 1/50: mặt đứng chia ≥ 2 đoạn", B(segs.Count >= 2), 1, 0);
        check("A3 1/50: không còn hình mặt đứng nguyên", cat.Count(f => f.Member == "D5" && f.F.Name == "D5"), 0, 0);
        var dA3 = SheetGeometry.Drawable("A3");
        check("A3 1/50: mỗi đoạn vừa bề rộng vùng vẽ", segs.Count(f => f.W <= dA3.W + 1e-6), segs.Count, 0);
        check("Đoạn ở tỷ lệ 1/50", segs.Count(f => Math.Abs(f.BaseScale - 50) < 1e-9), segs.Count, 0);
        var brk = SheetLayoutEngine.BeamBreaks(set, "D5", d, BeamDrawing.Build(DetailShim.Project(p.Settings), d)[0], p.Settings);
        var ranges = BeamDrawing.SegmentRanges(5, brk);
        bool cover = ranges[0].I0 == 0 && ranges[^1].I1 == 5;
        for (int i = 1; i < ranges.Count; i++) cover &= ranges[i].I0 == ranges[i - 1].I1;
        check("Các đoạn phủ đủ 5 nhịp, nối tiếp tại gối", B(cover && ranges.Count == segs.Count), 1, 0);
        var texts = segs.Select(f => f.F.W.GetPrimitives().Where(q => q.Kind == PrimKind.Text).Select(q => q.Text ?? "").ToList()).ToList();
        check("Tiêu đề đoạn \"D5 (SL=…) – ĐOẠN 1/n (trục …)\"", B(texts[0].Any(t => t.StartsWith("D5 (SL=") && t.Contains("ĐOẠN 1/" + segs.Count) && t.Contains("trục"))), 1, 0);
        check("Tiêu đề giữ chiều dài cả dầm L ≥ 22500 (không phải chiều dài đoạn)", B(texts.All(l => l.Any(t => t.StartsWith("D5 (SL=") && TitleL(t) >= 22500))), 1, 0);
        check("Đoạn sau: thép chạy tiếp ghi \"(tiếp)\"", B(texts.Skip(1).Any(l => l.Any(t => t.Contains("(tiếp)")))), 1, 0);
        check("Đường cắt ngắt: \"XEM TIẾP ĐOẠN 2/…\" ở đoạn đầu", B(texts[0].Any(t => t.StartsWith("XEM TIẾP ĐOẠN 2/"))), 1, 0);
        // số hiệu thép: mọi số hiệu thép dọc của dầm xuất hiện trên các đoạn (vòng số hiệu)
        var longMarks = d.Bars.Where(b => !IsStirrup(b)).Select(b => b.Mark.ToString()).Distinct().ToList();
        var shown = new HashSet<string>(texts.SelectMany(l => l));
        check("Số hiệu thép dọc trùng bảng thống kê (đều có trên các đoạn)", longMarks.Count(shown.Contains), longMarks.Count, 0);
        var tags = d.Bars.Where(b => !IsStirrup(b)).Select(b => b.Count + "Ø" + b.Dia).Distinct().ToList();
        check("Nhãn thép dọc nØd có trên các đoạn", tags.Count(t => shown.Any(s => s.StartsWith(t))), tags.Count, 0);
        // mặt cắt: mỗi số hiệu đúng một hình
        var secFigs = cat.Where(f => f.Member == "D5" && f.Segment == 0 && f.F.Name.StartsWith("D5 ")).ToList();
        var nums = BeamDrawing.SectionCuts(d).Select(c => c.Num).Distinct().Count();
        check("Mỗi mặt cắt xuất hiện đúng 1 lần (hình riêng)", secFigs.Count * 100 + secFigs.Select(f => f.F.Name).Distinct().Count(), nums * 100 + nums, 0);
        // dàn trang: đoạn + mặt cắt xếp được trên A3, không đè khung tên
        SheetLayoutEngine.AutoLayout(set, cat);
        var map = cat.ToDictionary(f => f.Id);
        int over = set.Sheets.Sum(s => SheetLayoutEngine.PlacedRects(s, map).Count(x => SheetGeometry.Overlap(x, SheetLayoutEngine.TitleZone(set, s))));
        check("Dàn trang A3: không hình nào đè khung tên", over, 0, 0);
        check("Dàn trang A3: đủ mọi hình", SheetLayoutEngine.Unplaced(set, cat).Count, 0, 0);
        var segSheet = set.Sheets.First(s => s.Figures.Any(pf => map[pf.Id].Segment == 1));
        check("Dàn trang A3: các đoạn ở tỷ lệ 1/50", set.Sheets.SelectMany(s => s.Figures).Where(pf => map[pf.Id].Segment > 0).Count(pf => map[pf.Id].ScaleAt(pf.Scale) == 50), segs.Count, 0);
        // mặt cắt nằm dưới mặt đứng cùng dầm (cùng tờ: đỉnh mặt cắt thấp hơn đáy đoạn mặt đứng)
        var rs = segSheet.Figures.Where(pf => map[pf.Id].Segment == 0 && map[pf.Id].Member == "D5").ToList();
        var e1 = segSheet.Figures.First(pf => map[pf.Id].Segment == 1);
        check("Mặt cắt xếp thành hàng dưới mặt đứng", B(rs.Count == 0 || rs.All(pf => pf.Y + map[pf.Id].HAt(pf.Scale) <= e1.Y + 1e-6)), 1, 0);
        var files = SheetLayoutEngine.ExportCad(p, set, cat, dir, CadOutputFormat.DxfR12);
        check("Xuất DXF các tờ dầm chia đoạn", files.Count(File.Exists), set.Sheets.Count, 0);

        // ---- theo số nhịp / tại các trục / không chia ----
        set.BeamSplitBy["D5"] = DetailSheetSet.SplitPerSpans + " 2";
        var b2 = SheetLayoutEngine.BeamBreaks(set, "D5", d, null, p.Settings);
        check("Theo 2 nhịp/đoạn: chia tại gối 2, 4 → 3 đoạn", B(b2.SequenceEqual(new[] { 2, 4 })), 1, 0);
        set.BeamSplitBy["D5"] = DetailSheetSet.SplitAtAxes + " 3";
        var b3 = SheetLayoutEngine.BeamBreaks(set, "D5", d, null, p.Settings);
        check("Tại trục thứ 3: 2 đoạn (nhịp 1–2, 3–5)", B(b3.SequenceEqual(new[] { 2 })), 1, 0);
        var cat3 = SheetLayoutEngine.Catalog(p, res, null, set);
        check("Tại trục thứ 3: 2 hình đoạn", cat3.Count(f => f.Member == "D5" && f.Segment > 0), 2, 0);
        set.BeamSplitBy["D5"] = DetailSheetSet.SplitNone;
        var catN = SheetLayoutEngine.Catalog(p, res, null, set);
        var whole = catN.First(f => f.Member == "D5" && f.F.Name == "D5");
        check("Không chia: giữ mặt đứng nguyên", B(catN.All(f => f.Segment == 0)), 1, 0);
        double sN = SheetLayoutEngine.ChooseScale(set, whole);
        check("Không chia trên A3: hạ tỷ lệ (> 1/50) cho vừa khổ", B(sN > 50 && SheetLayoutEngine.Fits(set, whole, sN, "A3")), 1, 0);
        var setAuto = new DetailSheetSet { AutoPaper = true, IndexSheet = false, BeamSplit = DetailSheetSet.SplitNone };
        var catA = SheetLayoutEngine.Catalog(p, res, null, setAuto);
        SheetLayoutEngine.AutoLayout(setAuto, catA);
        var mapA = catA.ToDictionary(f => f.Id);
        var shW = setAuto.Sheets.First(s => s.Figures.Any(pf => mapA[pf.Id].F.Name == "D5" && mapA[pf.Id].Member == "D5"));
        check("Không chia, khổ tự động: nâng khổ (> A3) giữ 1/50", B(Array.IndexOf(SheetGeometry.Papers, shW.Paper) > Array.IndexOf(SheetGeometry.Papers, "A3")), 1, 0);

        // ---- xuất hồ sơ mẫu dùng chung bộ dàn trang ----
        var ps = DetailingProject.Sample();
        var rsS = ps.RunAll();
        var log = new List<string>();
        var (es, ec) = SheetLayoutEngine.ExportSet(ps, rsS, log);
        var em = ec.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
        int ov = 0, low = 0, detSheets = 0;
        foreach (var s in es.Sheets.Where(x => !x.IsIndex))
        {
            var rr = SheetLayoutEngine.PlacedRects(s, em);
            var tz = SheetLayoutEngine.TitleZone(es, s);
            ov += rr.Count(x => SheetGeometry.Overlap(x, tz));
            if (s.Figures.All(pf => em[pf.Id].Part == 1) && s.Paper != "A4")
            {
                detSheets++;
                var dr = SheetGeometry.Drawable(s.Paper, s.Portrait);
                var u = rr.Aggregate(Rect2.Empty, (a, b) => a.Union(b));
                if (Math.Max(u.W / dr.W, u.H / dr.H) < 0.35) low++;
            }
        }
        check("Xuất hồ sơ mẫu: không hình nào đè khung tên", ov, 0, 0);
        check("Xuất hồ sơ mẫu: tờ chi tiết (khổ > A4) lấp đầy ≥ 35 % chiều hạn chế", low, 0, 0);
        check("Xuất hồ sơ mẫu: có tờ chi tiết", B(es.Sheets.Count(x => !x.IsIndex && x.Figures.Any(pf => em[pf.Id].Part == 1)) > 0), 1, 0);
        var dLog = new List<string>();
        var ef = DetailingExport.Drawings(ps, rsS, Path.Combine(dir, "HoSoMau"), true, dLog, CadOutputFormat.DxfR12);
        check("Xuất tất cả (tách): số file = số tờ dàn trang", ef.Count, es.Sheets.Count, 0);
        check("Xuất tất cả: tờ đầu danh mục, tờ cuối thống kê", B(Path.GetFileName(ef[0]).Contains("DanhMuc")) + B(Path.GetFileName(ef[^1]).Contains("ThongKe")), 2, 0);
    }
}
