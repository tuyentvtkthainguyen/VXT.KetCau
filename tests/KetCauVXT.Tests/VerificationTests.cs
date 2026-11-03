using KetCauVXT.Verification;

/// <summary>Bộ kiểm chứng phần mềm: chạy toàn bộ bài toán chuẩn, kiểm mỗi đại lượng trong ngưỡng, xuất Báo cáo kiểm chứng (Word + Excel).</summary>
public static class VerificationTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Console.WriteLine("\n=== BỘ KIỂM CHỨNG PHẦN MỀM (lời giải giải tích, tính tay độc lập theo TCVN) ===");
        VerificationUserData.UserFolderOverride = Path.Combine(Path.GetTempPath(), "kcvxt_kiemchung_user");
        var r = VerificationRunner.RunAll();
        Console.WriteLine($"   · {r.ProblemCount} bài toán, {r.Count} đại lượng, {r.Passed} đạt, {r.Failed} không đạt, sai số lớn nhất {r.MaxErrorPercent:0.####} %, {r.Elapsed.TotalSeconds:0.0} s");
        foreach (var s in r.Skipped) Console.WriteLine("   · bỏ qua: " + s);
        check("Kiểm chứng: có ít nhất 20 bài toán", r.ProblemCount >= 20 ? 1 : 0, 1, 0);
        check("Kiểm chứng: không bài toán nào lỗi khi chạy", r.Errors, 0, 0);
        foreach (var c in r.Cases)
        {
            if (!c.Pass)
                Console.WriteLine($"   ! {c.Id} {c.Title} – {c.Quantity}: tham chiếu {c.Reference}, VXT {c.Computed}, sai số {c.ErrorPercent:0.####} % > {c.Tolerance} % {c.Error}");
            check($"Kiểm chứng {c.Id} {c.Title} – {c.Quantity} trong ngưỡng {c.Tolerance} %", c.Pass ? 1 : 0, 1, 0);
        }
        string dir = Path.Combine(outDir, "KiemChung");
        Directory.CreateDirectory(dir);
        var (docx, xlsx) = VerificationReport.SaveAll(r, dir);
        Console.WriteLine($"   · báo cáo: {docx}, {xlsx}");
        static bool ZipOk(string f) { try { using var z = System.IO.Compression.ZipFile.OpenRead(f); return z.Entries.Count > 0; } catch { return false; } }
        check("Báo cáo kiểm chứng Word: mở được", File.Exists(docx) && ZipOk(docx) ? 1 : 0, 1, 0);
        check("Báo cáo kiểm chứng Excel: mở được", File.Exists(xlsx) && ZipOk(xlsx) ? 1 : 0, 1, 0);
        string xml = "";
        try { using var z = System.IO.Compression.ZipFile.OpenRead(docx); using var sr = new StreamReader(z.GetEntry("word/document.xml")!.Open()); xml = sr.ReadToEnd(); } catch { }
        check("Báo cáo: có mục 5 kiểm chứng độc lập + phiếu xác nhận", xml.Contains("5.3. Xác nhận của bên kiểm chứng độc lập") && xml.Contains("5.2. Quy trình kiểm chứng độc lập") ? 1 : 0, 1, 0);

        // kiểm chứng độc lập: bên thứ ba điền Excel → nhập lại → mục 5 có bảng so sánh
        var c0 = r.Cases[0]; var c1 = r.Cases[1];
        c0.UserBookValue = c0.Reference * 1.01; c0.UserBookSource = "Giáo trình thử, ví dụ 1"; c0.UserKind = VerificationUserData.Kinds[0]; c0.UserVerifier = "KS. Kiểm chứng A"; c0.UserDate = "03/11/2026";
        c1.UserBookValue = c1.Reference * 1.10; c1.UserBookSource = "Tính tay thử"; c1.UserKind = VerificationUserData.Kinds[4]; c1.UserVerifier = "KS. Kiểm chứng B";
        check("Kiểm chứng độc lập: lệch 1 % → đạt (ngưỡng max(ngưỡng;3 %))", c0.BookPass == true ? 1 : 0, 1, 0);
        check("Kiểm chứng độc lập: lệch 10 % → không đạt", c1.BookPass == false ? 1 : 0, 1, 0);
        string filled = Path.Combine(dir, "BaoCaoKiemChung_DaDien.xlsx");
        VerificationReport.SaveXlsx(r, filled);
        var ud = new VerificationUserData();
        int n = ud.ImportXlsx(filled, out string imsg);
        Console.WriteLine("   · nhập Excel đã điền: " + imsg);
        check("Nhập Excel bên thứ ba: nhận 2 giá trị", n, 2, 0);
        check("Nhập Excel bên thứ ba: giữ giá trị, nguồn, người xác nhận",
            ud.Entries.TryGetValue(c0.Id, out var e0) && e0.Value is double v0 && Math.Abs(v0 - c0.UserBookValue.Value) <= 1e-6 * Math.Max(1, Math.Abs(v0)) && e0.Source == c0.UserBookSource && e0.Verifier == "KS. Kiểm chứng A" ? 1 : 0, 1, 0);
        string d2 = Path.Combine(dir, "BaoCaoKiemChung_CoDocLap.docx");
        VerificationReport.SaveDocx(r, d2);
        string xml2 = "";
        try { using var z = System.IO.Compression.ZipFile.OpenRead(d2); using var sr = new StreamReader(z.GetEntry("word/document.xml")!.Open()); xml2 = sr.ReadToEnd(); } catch { }
        check("Mục 5 có bảng so sánh với người xác nhận", xml2.Contains("KS. Kiểm chứng A") && xml2.Contains("Không đạt") ? 1 : 0, 1, 0);
        foreach (var c in new[] { c0, c1 }) { c.UserBookValue = null; c.UserBookSource = ""; c.UserKind = ""; c.UserVerifier = ""; c.UserDate = ""; }

        check("Báo cáo: ghi rõ tự kiểm chứng, chưa có bên thứ ba xác nhận", xml.Contains("TỰ KIỂM CHỨNG") && xml.Contains("bên thứ ba") ? 1 : 0, 1, 0);
    }
}
