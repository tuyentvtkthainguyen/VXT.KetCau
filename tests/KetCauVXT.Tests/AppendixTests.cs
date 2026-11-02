using System.IO.Compression;
using System.Text;
using KetCauVXT.Docs;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Loads;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng phụ lục tính toán (Docs/Appendix.cs, Report/AppendixReport.cs).
/// check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class AppendixTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var secs = Appendix.Sections();
        check("Phụ lục: ≥ 17 mục", secs.Count >= 17 ? 1 : 0, 1, 0);
        check("Phụ lục: mã mục PL1…PLn liên tiếp", secs.Select((s, i) => s.Code == "PL" + (i + 1) ? 0 : 1).Sum(), 0, 0);
        check("Phụ lục: mỗi mục có tiêu đề và giải thích mở đầu", secs.Count(s => string.IsNullOrWhiteSpace(s.Title) || string.IsNullOrWhiteSpace(s.Intro)), 0, 0);
        check("Phụ lục: mỗi mục có ít nhất 1 hạng mục", secs.Count(s => s.Items.Count == 0), 0, 0);
        check("Phụ lục: mọi hạng mục có công thức hoặc giải thích",
            secs.SelectMany(s => s.Items).Count(i => string.IsNullOrWhiteSpace(i.Formula) && string.IsNullOrWhiteSpace(i.Explanation) && string.IsNullOrWhiteSpace(i.Values)), 0, 0);
        check("Phụ lục: mọi hạng mục có tên", secs.SelectMany(s => s.Items).Count(i => string.IsNullOrWhiteSpace(i.Name)), 0, 0);

        string all = string.Join("\n", secs.Select(s => s.FullTitle + " " + s.Intro + " " + string.Join(" ", s.Items.Select(i => i.AllText)) + " " + string.Join(" ", s.Tables.Select(t => t.AllText))));
        bool Has(params string[] any) => any.Any(a => all.Contains(a));
        check("Phụ lục: có k(z) = 1,844", Has("1,844", "1.844") ? 1 : 0, 1, 0);
        check("Phụ lục: có W0 vùng II = 0,95", Has("0,95", "0.95") ? 1 : 0, 1, 0);
        check("Phụ lục: có W0 vùng V = 1,85", Has("1,85") ? 1 : 0, 1, 0);
        check("Phụ lục: có công thức SPT 300·N", Has("300·N") ? 1 : 0, 1, 0);
        check("Phụ lục: có cu = 6,25·N", Has("6,25·N") ? 1 : 0, 1, 0);
        check("Phụ lục: có FS = 2,5", Has("FS = 2,5") ? 1 : 0, 1, 0);
        check("Phụ lục: có công thức R nền TCVN 9362", Has("R = m1·m2/ktc") ? 1 : 0, 1, 0);
        check("Phụ lục: có pmax ≤ 1,2R", Has("pmax ≤ 1,2R") ? 1 : 0, 1, 0);
        check("Phụ lục: có Sgh = 8 cm", Has("Sgh = 8 cm") ? 1 : 0, 1, 0);
        check("Phụ lục: có ψ = 0,9", Has("ψ = 0,9") ? 1 : 0, 1, 0);
        check("Phụ lục: có [λ] = 180 − 60α", Has("180 − 60α") ? 1 : 0, 1, 0);
        check("Phụ lục: có T1 = Ct·H^(3/4)", Has("T1 = Ct·H^(3/4)") ? 1 : 0, 1, 0);
        check("Phụ lục: có bảng tổng hợp hệ số an toàn", secs.Any(s => s.Title.Contains("hệ số an toàn")) ? 1 : 0, 1, 0);
        check("Phụ lục: có quy trình cho người mới", secs.Any(s => s.Title.Contains("người mới")) ? 1 : 0, 1, 0);

        // bảng tra tính lại bằng hàm của chương trình
        var sp = Appendix.SpectrumParams("C");
        check("Phụ lục: phổ nền C – S", sp.S, 1.15, 1e-6);
        check("Phụ lục: phổ nền C – TB", sp.TB, 0.20, 1e-6);
        check("Phụ lục: phổ nền C – TC", sp.TC, 0.60, 1e-6);
        check("Phụ lục: phổ nền C – TD", sp.TD, 2.0, 1e-6);
        var spD = Appendix.SpectrumParams("D");
        check("Phụ lục: phổ nền D – S", spD.S, 1.35, 1e-6);
        check("Phụ lục: phổ nền D – TC", spD.TC, 0.80, 1e-6);
        check("Phụ lục: bảng k(z) chứa k(10 m, B)", all.Contains(Appendix.F(LateralLoadGenerator.Kz(10, "B"), "0.000")) ? 1 : 0, 1, 0);
        check("Phụ lục: bảng ξR chứa CB400-V", all.Contains(Appendix.F(RcSection.XiR(Material.Rebar("CB400-V").Rs), "0.000")) ? 1 : 0, 1, 0);

        // tìm kiếm không dấu
        check("Phụ lục: chuẩn hoá không dấu", Appendix.Normalize("Hệ số AN TOÀN đất") == "he so an toan dat" ? 1 : 0, 1, 0);
        check("Phụ lục: tìm \"he so an toan\" có kết quả", Appendix.Search(secs, "he so an toan").Count > 0 ? 1 : 0, 1, 0);
        check("Phụ lục: tìm \"chọc thủng\" = \"choc thung\"", Appendix.Search(secs, "chọc thủng").Count, Appendix.Search(secs, "choc thung").Count, 0);
        check("Phụ lục: tìm \"spt\" có kết quả", Appendix.Search(secs, "spt").Count > 0 ? 1 : 0, 1, 0);
        check("Phụ lục: tìm chuỗi vô nghĩa = 0", Appendix.Search(secs, "zzqqxx").Count, 0, 0);

        // có công trình → bổ sung giá trị thực
        var p = ModelBuilder.NewProject("Thử phụ lục");
        ModelBuilder.SetGrid(p, new[] { 4.0 }, new[] { 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions());
        p.Wind.W0 = 1.25; p.Wind.Terrain = "A";
        var ps = Appendix.Sections(p);
        string allP = string.Join("\n", ps.SelectMany(s => s.Items).Select(i => i.AllText));
        check("Phụ lục + công trình: cùng số mục", ps.Count, secs.Count, 0);
        check("Phụ lục + công trình: có dòng giá trị công trình", ps.SelectMany(s => s.Items).Count(i => i.Name.StartsWith("Giá trị đang dùng")) >= 5 ? 1 : 0, 1, 0);
        check("Phụ lục + công trình: ghi W0 = 1,25 và địa hình A", allP.Contains("W0 = 1,25") && allP.Contains("địa hình A") ? 1 : 0, 1, 0);
        check("Phụ lục + công trình: ghi R nền", allP.Contains("R(b = 1 m)") ? 1 : 0, 1, 0);

        // xuất Word / Excel
        Directory.CreateDirectory(outDir);
        string docx = Path.Combine(outDir, "PhuLucTinhToan.docx");
        AppendixReport.ExportDocx(p, docx);
        string xml = "";
        bool zipOk;
        try
        {
            using var z = ZipFile.OpenRead(docx);
            var e = z.GetEntry("word/document.xml");
            zipOk = e != null;
            if (e != null) { using var rd = new StreamReader(e.Open(), Encoding.UTF8); xml = rd.ReadToEnd(); }
        }
        catch (InvalidDataException) { zipOk = false; }
        check("Phụ lục Word: mở được dạng zip, có word/document.xml", zipOk ? 1 : 0, 1, 0);
        check("Phụ lục Word: chứa \"PHỤ LỤC\"", xml.Contains("PHỤ LỤC") ? 1 : 0, 1, 0);
        check("Phụ lục Word: chứa đủ 18 tiêu đề PL", Enumerable.Range(1, secs.Count).Count(i => xml.Contains("PL" + i + ". ")), secs.Count, 0);
        check("Phụ lục Word: XML hợp lệ", IsXml(xml) ? 1 : 0, 1, 0);

        var x = new XlsxWriter();
        var sh = x.AddSheet("Phu luc");
        AppendixReport.AddSheet(p, sh);
        check("Phụ lục Excel: số hàng > số hạng mục", sh.RowCount > ps.Sum(s => s.Items.Count) ? 1 : 0, 1, 0);
        string xlsx = Path.Combine(outDir, "PhuLucTinhToan.xlsx");
        x.Save(xlsx);
        check("Phụ lục Excel: file tạo được", File.Exists(xlsx) && new FileInfo(xlsx).Length > 1000 ? 1 : 0, 1, 0);
        Console.WriteLine($"   · phụ lục: {secs.Count} mục, {secs.Sum(s => s.Items.Count)} hạng mục, {secs.Sum(s => s.Tables.Count)} bảng tra → {docx}");
    }

    private static bool IsXml(string s)
    {
        try { System.Xml.Linq.XDocument.Parse(s); return true; }
        catch (System.Xml.XmlException) { return false; }
    }
}
