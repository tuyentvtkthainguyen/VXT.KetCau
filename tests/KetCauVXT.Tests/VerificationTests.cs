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
        check("Báo cáo: ghi rõ tự kiểm chứng, chưa có bên thứ ba xác nhận", xml.Contains("TỰ KIỂM CHỨNG") && xml.Contains("bên thứ ba") ? 1 : 0, 1, 0);
    }
}
