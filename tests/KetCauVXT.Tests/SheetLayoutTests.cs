using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;

namespace KetCauVXT.Tests;

/// <summary>Test 65 – Dàn trang in chế độ Triển khai hồ sơ: tự dàn trang (đủ hình, không chồng, trong khung, không đè khung tên,
/// theo thứ tự mục hồ sơ, đánh số KC-01…, tờ danh mục), tờ khoá giữ nguyên khi dàn lại, đặt hình thủ công, lưu / mở,
/// xuất PDF dạng nét (số trang, khổ giấy, phông nhúng), xuất DXF từng tờ.</summary>
public static class SheetLayoutTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "TrienKhai");
        Directory.CreateDirectory(dir);

        // ---- hình học tờ ----
        var (aw, ah) = SheetGeometry.PaperSize("A1");
        check("Khổ A1 ngang 841 × 594 mm", aw * 1000 + ah, 841 * 1000 + 594, 1e-9);
        var (pw3, ph3) = SheetGeometry.PaperSize("A3", true);
        check("Khổ A3 đứng 297 × 420 mm", pw3 * 1000 + ph3, 297 * 1000 + 420, 1e-9);
        var d1 = SheetGeometry.Drawable("A1");
        check("Vùng vẽ A1: lề trái 25 + 10, lề khác 10 + 10", d1.X1 * 1000 + d1.Y1, 35 * 1000 + 20, 1e-9);

        // ---- tìm chỗ trống ----
        var sh0 = new DetailSheet { Paper = "A3" };
        var rects = new List<Rect2>();
        var s1 = SheetLayoutEngine.FindSpot(sh0, rects, 100, 80);
        check("Chỗ trống đầu tiên: góc trái trên vùng vẽ", B(s1 is { } a && Math.Abs(a.X - 35) < 1e-9 && Math.Abs(a.Y + 80 - (297 - 20)) < 1e-9), 1, 0);
        rects.Add(new Rect2(s1.Value.X, s1.Value.Y, s1.Value.X + 100, s1.Value.Y + 80));
        var s2 = SheetLayoutEngine.FindSpot(sh0, rects, 100, 80);
        check("Hình thứ hai: bên phải hình đầu, cách ≥ 10 mm", B(s2 is { } b2 && Math.Abs(b2.X - (35 + 100 + SheetGeometry.Gap)) < 1e-9), 1, 0);
        check("Hình lớn hơn khổ: không có chỗ", B(SheetLayoutEngine.FindSpot(sh0, new List<Rect2>(), 500, 100) == null), 1, 0);
        // hình cao (cao hơn phần trên khung tên) đặt ở cột bên trái khung tên; hình vừa cao vừa rộng không có chỗ trên A3 (phải nâng khổ)
        var s3 = SheetLayoutEngine.FindSpot(sh0, new List<Rect2>(), 170, 240);
        check("Hình cao ở khổ A3: đặt cột trái, không đè khung tên", B(s3 is { } b3 && !SheetGeometry.Overlap(new Rect2(b3.X, b3.Y, b3.X + 170, b3.Y + 240), SheetGeometry.TitleZone("A3"))), 1, 0);
        check("Hình 360 × 250 trên A3: không có chỗ (tránh khung tên) → nâng khổ", B(SheetLayoutEngine.FindSpot(sh0, new List<Rect2>(), 360, 250) == null), 1, 0);

        // ---- tự dàn trang hồ sơ mẫu ----
        var p = DetailingProject.Sample();
        var res = p.RunAll();
        var log = new List<string>();
        var cat = SheetLayoutEngine.Catalog(p, res, log);
        var map = cat.ToDictionary(f => f.Id);
        check("Danh mục hình: có mặt bằng, chi tiết, thống kê", B(cat.Any(f => f.Id.StartsWith("MB:"))) + B(cat.Any(f => f.Part == 1)) + B(cat.Any(f => f.Part == 2 && f.Section == cat.Max(x => x.Section))), 3, 0);
        check("Danh mục hình: mã hình không trùng", cat.Select(f => f.Id).Distinct().Count(), cat.Count, 0);
        var set = new DetailSheetSet();
        SheetLayoutEngine.AutoLayout(set, cat, log);
        check("Tự dàn trang: mọi hình đều được đặt", SheetLayoutEngine.Unplaced(set, cat).Count, 0, 0);
        check("Tự dàn trang: mỗi hình chỉ trên một tờ", set.Sheets.Sum(s => s.Figures.Count), cat.Count, 0);
        int overlaps = 0, outside = 0, onTitle = 0;
        foreach (var s in set.Sheets)
        {
            var rs = SheetLayoutEngine.PlacedRects(s, map);
            var area = SheetGeometry.Drawable(s.Paper, s.Portrait);
            var tz = SheetGeometry.TitleZone(s.Paper, s.Portrait);
            for (int i = 0; i < rs.Count; i++)
            {
                var a0 = SheetGeometry.Drawable("A0");
                bool fitsA0 = rs[i].W <= a0.W && rs[i].H <= a0.H - SheetGeometry.TitleH;
                if (!fitsA0) continue;   // quá khổ lớn nhất: có cảnh báo riêng
                for (int j = i + 1; j < rs.Count; j++) if (SheetGeometry.Overlap(rs[i], rs[j], SheetGeometry.Gap - 0.01)) overlaps++;
                if (rs[i].X1 < area.X1 - 1e-6 || rs[i].Y1 < area.Y1 - 1e-6 || rs[i].X2 > area.X2 + 1e-6 || rs[i].Y2 > area.Y2 + 1e-6) outside++;
                if (SheetGeometry.Overlap(rs[i], tz)) onTitle++;
            }
        }
        check("Tự dàn trang: các hình không chồng nhau (khe ≥ 10 mm)", overlaps, 0, 0);
        check("Tự dàn trang: hình nằm trong khung vẽ", outside, 0, 0);
        check("Tự dàn trang: hình không đè khung tên", onTitle, 0, 0);
        check("Tờ đầu: danh mục bản vẽ KC-01", B(set.Sheets[0].IsIndex && set.Sheets[0].No == "KC-01"), 1, 0);
        int badNo = set.Sheets.Select((s, i) => s.No == "KC-" + (i + 1).ToString("00") ? 0 : 1).Sum();
        check("Đánh số tờ liên tục KC-01, KC-02…", badNo, 0, 0);
        var secs = set.Sheets.Where(s => !s.IsIndex).Select(s => s.Figures.Min(pf => map[pf.Id].Section)).ToList();
        int desc = 0;
        for (int i = 1; i < secs.Count; i++) if (secs[i] < secs[i - 1]) desc++;
        check("Thứ tự tờ theo mục hồ sơ (móng → cột → dầm → … → thống kê)", desc, 0, 0);
        check("Tờ đầu sau danh mục: mục đầu tiên của hồ sơ (mặt bằng móng nếu có)", B(secs.Count > 0 && secs[0] == cat.Min(f => f.Section)), 1, 0);
        check("Thống kê vật liệu ở cuối bộ", B(secs.Count > 0 && secs[^1] == cat.Max(f => f.Section)), 1, 0);
        int mixed = set.Sheets.Where(s => !s.IsIndex).Count(s => s.Figures.Select(pf => map[pf.Id].Section).Distinct().Count() > 1);
        check("Mỗi tờ chỉ chứa hình của một mục", mixed, 0, 0);
        int mixedPart = set.Sheets.Where(s => !s.IsIndex).Count(s => s.Figures.Select(pf => map[pf.Id].Part).Distinct().Count() > 1);
        check("Mặt bằng và chi tiết thuộc mặt bằng nằm ở tờ riêng", mixedPart, 0, 0);

        // ---- tờ khoá giữ nguyên khi dàn lại ----
        var lockS = set.Sheets[Math.Min(2, set.Sheets.Count - 1)];
        lockS.Locked = true;
        lockS.Figures[0].X += 3.5;
        string lockId = lockS.Figures[0].Id; double lockX = lockS.Figures[0].X;
        int nLock = lockS.Figures.Count;
        SheetLayoutEngine.AutoLayout(set, cat, log);
        var again = set.Sheets.FirstOrDefault(s => ReferenceEquals(s, lockS));
        check("Tờ khoá: vẫn còn sau khi tự dàn lại", B(again != null), 1, 0);
        check("Tờ khoá: giữ nguyên hình và vị trí chỉnh tay", B(again != null && again.Figures.Count == nLock && again.Figures[0].Id == lockId && Math.Abs(again.Figures[0].X - lockX) < 1e-9), 1, 0);
        check("Dàn lại: không mất / không lặp hình", set.Sheets.Sum(s => s.Figures.Count) * 1000 + SheetLayoutEngine.Unplaced(set, cat).Count, cat.Count * 1000, 0);
        check("Dàn lại: chỉ một tờ danh mục", set.Sheets.Count(s => s.IsIndex), 1, 0);

        // ---- đặt / gỡ thủ công ----
        var blank = new DetailSheet { Paper = "A1", Title = "TỜ THÊM" };
        set.Sheets.Add(blank);
        var small = cat.OrderBy(f => f.W * f.H).First();
        bool placed = SheetLayoutEngine.Place(set, blank, small, map);
        check("Đặt hình vào tờ trống: có chỗ, hình rời tờ cũ", B(placed) + B(set.Sheets.Count(s => s.Figures.Any(pf => pf.Id == small.Id)) == 1), 2, 0);
        blank.Figures.Clear();
        check("Gỡ hình: hình về danh sách chưa đặt", SheetLayoutEngine.Unplaced(set, cat).Count(f => f.Id == small.Id), 1, 0);
        SheetLayoutEngine.Place(set, blank, small, map, 100, 100);
        check("Đặt hình tại vị trí chọn (100; 100)", blank.Figures[0].X + blank.Figures[0].Y, 200, 1e-9);

        // ---- lưu / mở ----
        p.Sheets = set;
        var p2 = DetailingProject.FromJson(p.ToJson());
        check("Lưu / mở: số tờ, số hình, tờ khoá, vị trí", B(p2.Sheets != null && p2.Sheets.Sheets.Count == set.Sheets.Count)
            + B(p2.Sheets?.Sheets.Sum(s => s.Figures.Count) == set.Sheets.Sum(s => s.Figures.Count))
            + B(p2.Sheets?.Sheets.Count(s => s.Locked) == set.Sheets.Count(s => s.Locked))
            + B(p2.Sheets != null && Math.Abs(p2.Sheets.Sheets[^1].Figures[0].X - 100) < 1e-9), 4, 0);
        var p3 = DetailingProject.FromJson("{\"ProjectName\":\"Cũ\"}");
        check("Mở hồ sơ cũ (chưa dàn trang): Sheets = null", B(p3.Sheets == null), 1, 0);
        int pruned = SheetLayoutEngine.Prune(set, cat.Where(f => f.Id != small.Id).ToList());
        check("Hình không còn trong hồ sơ bị gỡ khỏi tờ", pruned, 1, 0);
        SheetLayoutEngine.AutoLayout(set, cat, log);

        // ---- ghép tờ ----
        var pages = SheetLayoutEngine.Pages(p, set, cat);
        check("Số trang = số tờ", pages.Count, set.Sheets.Count, 0);
        var det = pages.First(t => !t.Sheet.IsIndex);
        string dxf = DxfWriter.Decode(det.W.ToDxfString());
        check("Tờ chi tiết: khung tên có tên bản vẽ, số hiệu, tỷ lệ", B(dxf.Contains(det.Sheet.Title.Split('(')[0].Trim())) + B(dxf.Contains(det.Sheet.No)) + B(dxf.Contains("Tỷ lệ: 1/")), 3, 0);
        var e = det.W.Extents;
        check("Tờ ghép: phạm vi = khổ giấy × tỷ lệ", Math.Round(e.W / det.W.Scale), det.WMm, 1e-3);
        string idx = DxfWriter.Decode(pages[0].W.ToDxfString());
        check("Tờ danh mục: liệt kê mọi tờ", set.Sheets.Count(s => idx.Contains(s.No)), set.Sheets.Count, 0);

        // ---- PDF dạng nét ----
        string pdfPath = Path.Combine(dir, "HoSo_DanTrang.pdf");
        SheetLayoutEngine.ExportPdf(p, set, cat, pdfPath);
        var bytes = File.ReadAllBytes(pdfPath);
        string head = Encoding.ASCII.GetString(bytes, 0, 5);
        check("PDF: bắt đầu %PDF", B(head == "%PDF-"), 1, 0);
        string txt = Encoding.Latin1.GetString(bytes);
        check("PDF: số trang = số tờ", Regex.Matches(txt, @"/Type /Page /Parent").Count, set.Sheets.Count, 0);
        check("PDF: /Count = số tờ", B(txt.Contains("/Count " + set.Sheets.Count + " ")), 1, 0);
        check("PDF: kết thúc %%EOF", B(txt.TrimEnd().EndsWith("%%EOF")), 1, 0);
        // khổ A1 → 2383,94 × 1683,78 pt
        var one = new DxfWriter(100);
        one.Line(Lay.BeTong, 0, 0, 84100, 59400);
        one.Text(Lay.Chu, 1000, 1000, 250, "MẶT CẮT 1-1 Ø6a150", 0, 0, 0);
        string onePath = Path.Combine(dir, "HoSo_DanTrang_A1.pdf");
        PdfVectorWriter.Write(onePath, new List<(DxfWriter, double, double, double)> { (one, 0.01, 841, 594) }, "A1");
        string t1 = Encoding.Latin1.GetString(File.ReadAllBytes(onePath));
        var mb = Regex.Match(t1, @"/MediaBox \[0 0 ([0-9.]+) ([0-9.]+)\]");
        check("PDF A1: rộng 841 mm = 2383,94 pt", mb.Success ? double.Parse(mb.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0, 841 / 25.4 * 72, 1e-4);
        check("PDF A1: cao 594 mm = 1683,78 pt", mb.Success ? double.Parse(mb.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) : 0, 594 / 25.4 * 72, 1e-4);
        bool hasFont = PdfVectorWriter.FindFont() != null;
        check("PDF: chữ tiếng Việt – phông TrueType nhúng (Identity-H) hoặc Helvetica khi máy không có phông",
            B(hasFont ? t1.Contains("/Identity-H") && t1.Contains("/FontFile2") : t1.Contains("/Helvetica")), 1, 0);
        // nội dung trang: đường thẳng chéo cả tờ (0 0 m 2383.937 1683.78 l)
        string content = Inflate(t1, 10);
        check("PDF: nét vẽ đúng toạ độ pt (đường chéo tờ A1)", B(content.Contains("0 0 m 2383.937 1683.78 l S")), 1, 0);
        check("PDF: chữ đặt bằng Tm / Tj", B(content.Contains(" Tm ") && content.Contains(" Tj ET")), 1, 0);
        if (hasFont)
        {
            string cmap = Inflate(t1, 8);
            check("PDF: bảng ToUnicode có chữ Ặ (U+1EB6) và Ø (U+00D8)", B(cmap.Contains("<1EB6>")) + B(cmap.Contains("<00D8>")), 2, 0);
        }
        check("Bỏ dấu khi không có phông: \"Thép đai Ø6\" → \"Thep dai Ø6\"", B(PdfVectorWriter.Plain("Thép đai Ø6") == "Thep dai Ø6"), 1, 0);

        // ---- DXF từng tờ ----
        var files = SheetLayoutEngine.ExportCad(p, set, cat, Path.Combine(dir, "DanTrang"), KetCauVXT.Model.CadOutputFormat.DxfR12);
        check("Xuất DXF từng tờ: số file = số tờ", files.Count(File.Exists), set.Sheets.Count, 0);
    }

    /// <summary>Giải nén luồng của đối tượng id trong PDF (FlateDecode).</summary>
    private static string Inflate(string pdf, int id)
    {
        int o = pdf.IndexOf("\n" + id + " 0 obj\n", StringComparison.Ordinal);
        if (o < 0) return "";
        int s = pdf.IndexOf("stream\n", o, StringComparison.Ordinal) + 7;
        int e = pdf.IndexOf("\nendstream", s, StringComparison.Ordinal);
        if (s < 7 || e < 0) return "";
        var raw = Encoding.Latin1.GetBytes(pdf.Substring(s, e - s));
        try
        {
            using var z = new ZLibStream(new MemoryStream(raw), CompressionMode.Decompress);
            using var r = new StreamReader(z, Encoding.Latin1);
            return r.ReadToEnd();
        }
        catch (Exception) { return ""; }
    }
}
