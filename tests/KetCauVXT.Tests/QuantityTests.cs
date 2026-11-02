using System.IO.Compression;
using System.Text;
using System.Xml;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng bảng khối lượng vật tư (Report/Quantities.cs) và thuyết minh tự động (CalculationReport.Auto.cs).
/// Khung 1 nhịp 5 × 4 m, 2 tầng (3,6 + 3,3 m), cột 220×220, dầm 220×300, sàn 100 – khối lượng tính tay.</summary>
public static class QuantityTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Console.WriteLine("\n=== BẢNG KHỐI LƯỢNG VẬT TƯ + THUYẾT MINH TỰ ĐỘNG ===");
        var p = ModelBuilder.NewProject("Thử khối lượng");
        ModelBuilder.SetGrid(p, new[] { 5.0 }, new[] { 4.0 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x22", BeamSectionX = "D22x30", BeamSectionY = "D22x30", SlabSection = "S100" });
        // tường 220 trát 2 mặt trên trục A (y = 0) tầng T2, cao 3,0 m, cửa 0,9 × 2,2 m
        p.Walls.Add(new WallLine { Id = 1, Story = "T2", X1 = 0, Y1 = 0, X2 = 5, Y2 = 0, Type = "Tường 220 trát 2 mặt", Height = 3.0, OpeningArea = 0.9 * 2.2 });

        // ---------------- chỉ mô hình (chưa thiết kế) ----------------
        var q0 = QuantityCalculator.Compute(p, null);
        // cột: 4 × 0,22² × (3,6 + 3,3)
        check("KL: bê tông cột = 4·0,22²·(3,6+3,3)", q0.Volume(QtyCat.Column), 4 * 0.22 * 0.22 * (3.6 + 3.3), 1e-6);
        // dầm tính đến mép cột: 2 tầng × (2×(5 − 0,22) + 2×(4 − 0,22)) × 0,22 × 0,30
        double Lc = 2 * (5 - 0.22) + 2 * (4 - 0.22);
        check("KL: bê tông dầm (đến mép cột) = 2·0,22·0,30·17,12", q0.Volume(QtyCat.Beam), 2 * 0.22 * 0.30 * Lc, 1e-6);
        // sàn trừ dầm (nửa bề rộng dầm biên) và 4 góc cột: 20 − 0,11·17,12 − 4·0,11² = 18,0684 m² / tầng
        double slabNet = 20 - 0.11 * Lc - 4 * 0.11 * 0.11;
        check("KL: bê tông sàn trừ dầm, cột = 2·18,0684·0,10", q0.Volume(QtyCat.Slab), 2 * slabNet * 0.10, 1e-6);
        check("KL: ván khuôn cột = 4·0,88·(3,3 + 3,0)", q0.Formwork(QtyCat.Column), 4 * 0.88 * (3.3 + 3.0), 1e-6);
        // dầm biên: đáy + thành ngoài h + thành trong (h − hs)
        check("KL: ván khuôn dầm = (0,22 + 0,30 + 0,20)·Ltt", q0.Formwork(QtyCat.Beam), 2 * (0.22 + 0.30 + 0.20) * Lc, 1e-6);
        check("KL: ván khuôn sàn = đáy sàn đã trừ dầm, cột", q0.Formwork(QtyCat.Slab), 2 * slabNet, 1e-6);

        // tường: dài trừ 2 nửa cột 4,78; S = 4,78·3 = 14,34; cửa 1,98; lanh tô (0,9 + 2·0,2)·0,2 = 0,26
        var m = q0.Masonry.FirstOrDefault();
        check("KL: có 1 đoạn tường", q0.Masonry.Count, 1, 0);
        double gross = (5 - 0.22) * 3.0, door = 0.9 * 2.2, lint = (door / 2.2 + 0.4) * 0.2;
        check("KL: tường – chiều dài trừ cột 4,78 m", m?.NetLength ?? 0, 4.78, 1e-6);
        check("KL: tường – V xây = (14,34 − 1,98 − 0,26)·0,22", m?.Volume ?? 0, (gross - door - lint) * 0.22, 1e-6);
        check("KL: tường – trát 2 mặt = 2·(14,34 − 1,98)", m?.PlasterArea ?? 0, 2 * (gross - door), 1e-6);
        check("KL: tường – gạch = V × 550 viên/m³ (tường 220)", m?.Bricks ?? 0, (gross - door - lint) * 0.22 * 550, 1e-6);
        check("KL: lanh tô = 1,30·0,20·0,22", q0.Volume(QtyCat.Lintel), lint * 0.22, 1e-6);
        // tắt lanh tô → V xây chỉ trừ cửa
        var qsNoL = new QuantitySettings { IncludeLintels = false };
        var qn = QuantityCalculator.Compute(p, null, qsNoL);
        check("KL: không lanh tô – V xây = (14,34 − 1,98)·0,22", qn.MasonryTotal, (gross - door) * 0.22, 1e-6);

        // phân tích vật tư = khối lượng × định mức
        var b25 = q0.MaterialLines.FirstOrDefault(l => l.Source == "Bê tông B25" && l.Material == "Xi măng");
        double vB25 = q0.Concrete.Where(c => c.Grade == "B25").Sum(c => c.Volume);
        check("Vật tư: XM bê tông B25 = V × 431 kg/m³", b25?.Quantity ?? 0, vB25 * 431.0, 1e-9);
        check("Vật tư: khối lượng công tác = tổng V bê tông B25", b25?.SourceQty ?? 0, vB25, 1e-9);
        var brick = q0.Materials.FirstOrDefault(x => x.Material == q0.Settings.BrickName);
        check("Vật tư: tổng gạch = tổng gạch từng tường", brick?.Quantity ?? 0, q0.Masonry.Sum(x => x.Bricks), 1e-9);
        var mortar = q0.MaterialLines.Where(l => l.Source.StartsWith("Vữa xây") && l.Material == "Xi măng").Sum(l => l.Quantity);
        check("Vật tư: XM vữa xây M75 = V xây × 0,29 × 320,03", mortar, q0.MasonryTotal * 0.29 * 320.03, 1e-9);
        // định mức sửa được: đổi XM của B25
        var qsEdit = new QuantitySettings();
        qsEdit.ConcreteNorm("B25").Cement = 500;
        var qe = QuantityCalculator.Compute(p, null, qsEdit);
        check("Vật tư: định mức người dùng sửa (B25 → 500 kg/m³)", qe.MaterialLines.First(l => l.Source == "Bê tông B25" && l.Material == "Xi măng").Quantity, vB25 * 500, 1e-9);
        // JSON tương thích ngược: file cũ không có QuantitySettings
        var clone = p.Clone();
        check("Dự án: QuantitySettings có mặc định sau khi lưu/mở", clone.QuantitySettings?.ConcreteNorms?.Count ?? 0, QuantitySettings.DefaultConcreteNorms().Count, 0);
        var old = System.Text.Json.JsonSerializer.Deserialize<Project>("{\"Name\":\"cu\"}", Project.JsonOptions);
        check("Dự án cũ (không có QuantitySettings): dùng mặc định", old?.QuantitySettings?.MasonryNorms?.Count ?? 0, 3, 0);

        // hình học: giao đa giác
        var sq = new List<(double X, double Y)> { (0, 0), (2, 0), (2, 2), (0, 2) };
        var rc = new List<(double X, double Y)> { (1, 1), (3, 1), (3, 3), (1, 3) };
        check("Hình học: diện tích giao 2 hình vuông = 1", QuantityCalculator.ClipArea(sq, rc), 1, 1e-9);

        // ---------------- có phân tích + thiết kế ----------------
        AnalysisResults r = null; DesignSummary ds = null;
        try { r = FrameAnalysis.Run(p); ds = ConcreteDesigner.Design(p, r); }
        catch (Exception ex) { Console.WriteLine("  (phân tích/thiết kế lỗi: " + ex.Message + ")"); }
        check("KL: phân tích + thiết kế chạy được", ds != null ? 1 : 0, 1, 0);
        if (ds == null) return;
        var q = QuantityCalculator.Compute(p, ds);
        double sched = RebarSchedule.BuildRows(ds).Sum(x => x.WeightKg);
        check("KL: tổng cốt thép = tổng bảng thống kê thép", q.RebarTotal, sched, 1e-9);
        check("KL: tổng nhóm Ø = tổng cốt thép (làm tròn 0,01 kg/nhóm)", q.RebarGroups.Total, sched, 1e-3);
        check("KL: thép theo loại cấu kiện cộng lại = tổng", q.RebarByCategory.Sum(x => x.Total), sched, 1e-6);
        check("KL: có cốt thép > 0", sched > 0 ? 1 : 0, 1, 0);
        var ft = ds.Footings.Where(f => f.L > 0 && f.B > 0 && f.H > 0).ToList();
        if (ft.Count > 0)
        {
            double vf = ft.Sum(f => Math.Max(1, f.Quantity) * (f.Hb > 1e-6 && f.Hb < f.H - 1e-6 ? double.NaN : f.L * f.B * f.H));
            if (double.IsFinite(vf)) check("KL: bê tông móng đơn = Σ L·B·H", q.Volume(QtyCat.Footing), vf, 1e-6);
            double vl = ft.Sum(f => Math.Max(1, f.Quantity) * (f.L + 0.2) * (f.B + 0.2) * 0.1);
            check("KL: bê tông lót móng = Σ (L+0,2)(B+0,2)·0,1", q.Lean.Where(c => c.Member.StartsWith("Lót ")).Sum(c => c.Volume), vl, 1e-6);
        }
        var dw = q.MaterialLines.FirstOrDefault(l => l.Material == "Dây thép buộc");
        check("Vật tư: dây thép buộc = tấn thép × 14,28", dw?.Quantity ?? 0, sched / 1000 * 14.28, 1e-3);

        // ---------------- Excel ----------------
        Directory.CreateDirectory(outDir);
        string xlsx = Path.Combine(outDir, QuantityReport.DefaultFileName);
        QuantityReport.ExportExcel(p, q, xlsx);
        string wb = ReadEntry(xlsx, "xl/workbook.xml");
        check("Excel KL: file mở được dạng zip", wb.Length > 0 ? 1 : 0, 1, 0);
        check("Excel KL: đủ 8 trang đúng tên", QuantityReport.SheetNames.Count(n => wb.Contains($"name=\"{n}\"")), 8, 0);
        string s7 = ReadEntry(xlsx, "xl/worksheets/sheet7.xml");
        check("Excel KL: trang Phân tích vật tư có công thức", s7.Contains("<f>") && s7.Contains("SUMIFS") ? 1 : 0, 1, 0);
        check("Excel KL: các trang là XML hợp lệ", Enumerable.Range(1, 8).All(i => IsXml(ReadEntry(xlsx, $"xl/worksheets/sheet{i}.xml"))) ? 1 : 0, 1, 0);
        // đơn giá (tuỳ chọn)
        p.QuantitySettings.Prices.Add(new UnitPrice { Item = "Xi măng", Unit = "kg", Price = 1500 });
        var qp = QuantityCalculator.Compute(p, ds);
        var xm = qp.Materials.First(x => x.Material == "Xi măng");
        check("Đơn giá: thành tiền xi măng = KL × 1500", xm.Amount, xm.Quantity * 1500, 1e-9);
        string xlsxP = Path.Combine(outDir, "BangKhoiLuong_DonGia.xlsx");
        QuantityReport.ExportExcel(p, qp, xlsxP);
        check("Excel KL có đơn giá: có cột Thành tiền", ReadEntry(xlsxP, "xl/worksheets/sheet7.xml").Contains("Thành tiền") ? 1 : 0, 1, 0);
        p.QuantitySettings.Prices.Clear();

        // ---------------- thuyết minh tự động ----------------
        var o = ReportOptions.FromProject(p);
        o.ProjectName = "Nhà thử khối lượng"; o.Owner = "Ông A"; o.ChiefDesigner = "KS. Chủ trì"; o.Preparer = "KS. Người lập"; o.Checker = "KS. Kiểm tra";
        o.Date = "02/11/2026"; o.ExamplesPerType = 2; o.IncludeForceTables = true;
        o.Chapters.Beams = false; o.Chapters.WindSeismic = false; o.Chapters.Appendix = false;
        string docx = Path.Combine(outDir, "ThuyetMinhTuDong.docx");
        CalculationReport.Export(p, r, ds, docx, o);
        string doc = ReadEntry(docx, "word/document.xml");
        check("TM tự động: XML hợp lệ", IsXml(doc) ? 1 : 0, 1, 0);
        check("TM tự động: trang bìa (tiêu đề, chủ trì, ngày)", doc.Contains("THUYẾT MINH TÍNH TOÁN KẾT CẤU") && doc.Contains("CHỦ TRÌ THIẾT KẾ") && doc.Contains("KS. Người lập") && doc.Contains("ngày 02 tháng 11 năm 2026") ? 1 : 0, 1, 0);
        check("TM tự động: mục lục (trường TOC)", doc.Contains("MỤC LỤC") && doc.Contains("TOC \\o") && doc.Contains("fldCharType=\"begin\"") ? 1 : 0, 1, 0);
        check("TM tự động: chương KẾT LUẬN VÀ KIẾN NGHỊ", doc.Contains("KẾT LUẬN VÀ KIẾN NGHỊ") && doc.Contains("Kiến nghị") ? 1 : 0, 1, 0);
        check("TM tự động: bỏ chương dầm theo lựa chọn", doc.Contains("THIẾT KẾ DẦM") ? 0 : 1, 1, 0);
        check("TM tự động: có chương cột, sàn", doc.Contains("THIẾT KẾ CỘT") && doc.Contains("THIẾT KẾ SÀN") ? 1 : 0, 1, 0);
        check("TM tự động: bỏ mục gió/động đất, tổ hợp thành mục 5.4", !doc.Contains("Tải trọng gió") && doc.Contains("5.4. Tổ hợp tải trọng") ? 1 : 0, 1, 0);
        check("TM tự động: chương khối lượng vật tư", doc.Contains("KHỐI LƯỢNG VẬT TƯ CHỦ YẾU") ? 1 : 0, 1, 0);
        check("TM tự động: tên bảng đánh số", doc.Contains("Bảng 1.1.") ? 1 : 0, 1, 0);
        check("TM tự động: ví dụ thứ 2 (cột)", doc.Contains("Ví dụ 2: cột") || ds.Columns.Count < 2 ? 1 : 0, 1, 0);
        check("TM tự động: bảng nội lực bao", doc.Contains("Bảng nội lực bao các thanh") ? 1 : 0, 1, 0);
        check("TM tự động: bỏ phụ lục công thức theo lựa chọn", doc.Contains("PL1. ") ? 0 : 1, 1, 0);
        string ftr = ReadEntry(docx, "word/footer1.xml");
        check("TM tự động: chân trang có số trang (PAGE)", ftr.Contains("PAGE") && ftr.Contains("NUMPAGES") && IsXml(ftr) ? 1 : 0, 1, 0);
        check("TM tự động: settings có updateFields", ReadEntry(docx, "word/settings.xml").Contains("updateFields") ? 1 : 0, 1, 0);
        check("TM tự động: document có footerReference", doc.Contains("footerReference") ? 1 : 0, 1, 0);
        check("TM tự động: rels + content types có footer", ReadEntry(docx, "word/_rels/document.xml.rels").Contains("footer1.xml") && ReadEntry(docx, "[Content_Types].xml").Contains("footer1.xml") ? 1 : 0, 1, 0);
        // thuyết minh mặc định không đổi
        string docx0 = Path.Combine(outDir, "ThuyetMinhMacDinh.docx");
        CalculationReport.Export(p, r, ds, docx0);
        string doc0 = ReadEntry(docx0, "word/document.xml");
        check("TM mặc định: vẫn như cũ (có dầm, gió, không mục lục)", doc0.Contains("THIẾT KẾ DẦM") && doc0.Contains("Tải trọng gió") && !doc0.Contains("MỤC LỤC") && doc0.Contains("KẾT LUẬN") ? 1 : 0, 1, 0);
        check("TM mặc định: không có chân trang", HasEntry(docx0, "word/footer1.xml") ? 0 : 1, 1, 0);
        // Word riêng bảng khối lượng
        string qdoc = Path.Combine(outDir, "BangKhoiLuong.docx");
        QuantityReport.ExportWord(p, q, qdoc);
        check("Word KL: XML hợp lệ, có bảng tổng hợp", IsXml(ReadEntry(qdoc, "word/document.xml")) && ReadEntry(qdoc, "word/document.xml").Contains("Tổng hợp khối lượng chính") ? 1 : 0, 1, 0);
    }

    private static string ReadEntry(string zipPath, string entry)
    {
        try
        {
            using var z = ZipFile.OpenRead(zipPath);
            var e = z.GetEntry(entry);
            if (e == null) return "";
            using var rd = new StreamReader(e.Open(), Encoding.UTF8);
            return rd.ReadToEnd();
        }
        catch (Exception) { return ""; }
    }

    private static bool HasEntry(string zipPath, string entry)
    {
        try { using var z = ZipFile.OpenRead(zipPath); return z.GetEntry(entry) != null; }
        catch (Exception) { return false; }
    }

    private static bool IsXml(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        try { new XmlDocument().LoadXml(s); return true; }
        catch (XmlException) { return false; }
    }
}
