using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng danh mục văn bản pháp lý, tiêu chuẩn, định mức (Model/Regulations.cs), trích dẫn trong thuyết minh / phụ lục /
/// ghi chú bản vẽ / bảng khối lượng, kiểm tra dự án và nhập định mức từ Excel (Model/NormImport.cs).
/// check(tên, giá trị, kỳ vọng, sai số).</summary>
public static class RegulationTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Console.WriteLine("\n=== VĂN BẢN PHÁP LÝ, TIÊU CHUẨN, ĐỊNH MỨC ÁP DỤNG ===");
        string prevOverride = RegulationService.UserFolderOverride;
        string tmp = Path.Combine(Path.GetTempPath(), "kcvxt_reg_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tmp);
        Directory.CreateDirectory(outDir);
        RegulationService.UserFolderOverride = tmp;
        RegulationService.Reload();
        try
        {
            Registry(check);
            UserFile(check, tmp);
            Reports(check, outDir);
            Quantities(check);
            CheckProject(check);
            NormExcel(check, tmp);
        }
        finally
        {
            RegulationService.UserFolderOverride = prevOverride;
            RegulationService.Reload();
            try { Directory.Delete(tmp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    // ------------------------------------------------------------------
    private static void Registry(Action<string, double, double, double> check)
    {
        var reg = RegulationService.Registry;
        check("Danh mục: đọc không lỗi (không có file người dùng)", B(reg.LoadMessage == null), 1, 0);
        string[] codes =
        {
            "135/2025/QH15", "217/2026/NĐ-CP", "212/2026/NĐ-CP", "206/2026/NĐ-CP", "34/2026/TT-BXD", "36/2026/TT-BXD", "37/2026/TT-BXD", "38/2026/TT-BXD",
            "39/2026/TT-BXD", "40/2026/TT-BXD", "9947/BXD-VPB", "QCVN 02:2022/BXD", "TCVN 9386-1:2025", "TCVN 9386-5:2025", "TCVN 2737:2023", "TCVN 5574:2018",
            "TCVN 5575:2024", "TCVN 10304:2014", "TCVN 9362:2012", "TCVN 9379:2012", "TCVN 1651:2018", "QCVN 06:2022/BXD",
            "50/2014/QH13", "62/2020/QH14", "15/2021/NĐ-CP", "175/2024/NĐ-CP", "10/2021/NĐ-CP", "06/2021/TT-BXD", "12/2021/TT-BXD", "13/2021/TT-BXD",
            "TCVN 9386:2012", "TCVN 2737:1995"
        };
        check("Danh mục: có đủ các văn bản đã kiểm tra", codes.Count(c => reg.Find(c) != null), codes.Length, 0);
        check("Danh mục: số hiệu không trùng", reg.Docs.Select(d => Regulations.Key(d.Code)).Distinct().Count(), reg.Docs.Count, 0);
        check("Danh mục: tìm không phân biệt Đ/D, khoảng trắng", B(reg.Find("217/2026/ND-CP")?.Code == "217/2026/NĐ-CP" && reg.Find("tcvn 9386-1 : 2025") != null), 1, 0);

        RegulationDoc F(string c) => reg.Find(c) ?? new RegulationDoc();
        var luat = F("135/2025/QH15");
        check("Luật 135/2025/QH15: còn hiệu lực, ngày 10/12/2025, hiệu lực 01/7/2026",
            B(luat.Status == RegulationStatus.HieuLuc && luat.IssuedDate == "10/12/2025" && luat.EffectiveDate == "01/7/2026" && luat.Kind == RegulationKind.Luat), 1, 0);
        check("Luật 135/2025: trích dẫn \"Luật Xây dựng số 135/2025/QH15\"", B(luat.Citation == "Luật Xây dựng số 135/2025/QH15"), 1, 0);
        check("Luật 135/2025 thay thế 50/2014/QH13 và 62/2020/QH14", B(luat.Replaces.Contains("50/2014/QH13") && luat.Replaces.Contains("62/2020/QH14")), 1, 0);
        check("Luật 50/2014/QH13: hết hiệu lực → 135/2025/QH15", B(F("50/2014/QH13").Status == RegulationStatus.HetHieuLuc && F("50/2014/QH13").ReplacedBy == "135/2025/QH15"), 1, 0);
        check("Luật 62/2020/QH14: hết hiệu lực → 135/2025/QH15", B(F("62/2020/QH14").Status == RegulationStatus.HetHieuLuc && F("62/2020/QH14").ReplacedBy == "135/2025/QH15"), 1, 0);
        var nd217 = F("217/2026/NĐ-CP");
        check("NĐ 217/2026: ngày 19/6/2026, hiệu lực 01/7/2026", B(nd217.IssuedDate == "19/6/2026" && nd217.EffectiveDate == "01/7/2026"), 1, 0);
        check("NĐ 217/2026: ghi chú Điều 16, 19–23, 24, 25", B(nd217.Note.Contains("Điều 16") && nd217.Note.Contains("Điều 19–23") && nd217.Note.Contains("Điều 24") && nd217.Note.Contains("Điều 25")), 1, 0);
        check("NĐ 212/2026: 17/6/2026, hiệu lực 01/7/2026", B(F("212/2026/NĐ-CP").IssuedDate == "17/6/2026" && F("212/2026/NĐ-CP").EffectiveDate == "01/7/2026"), 1, 0);
        check("NĐ 175/2024: hết hiệu lực từ 01/7/2026, thay bởi 217/2026", B(F("175/2024/NĐ-CP").Status == RegulationStatus.HetHieuLuc && F("175/2024/NĐ-CP").ReplacedBy == "217/2026/NĐ-CP"), 1, 0);
        check("NĐ 15/2021: hết hiệu lực", B(F("15/2021/NĐ-CP").Status == RegulationStatus.HetHieuLuc), 1, 0);
        check("NĐ 10/2021 → NĐ 206/2026 (ghi \"kiểm tra\")", B(F("10/2021/NĐ-CP").ReplacedBy == "206/2026/NĐ-CP" && F("206/2026/NĐ-CP").Note.Contains("kiểm tra")), 1, 0);
        check("TT 06/2021/TT-BXD → TT 34/2026/TT-BXD", B(F("06/2021/TT-BXD").Status == RegulationStatus.HetHieuLuc && F("06/2021/TT-BXD").ReplacedBy == "34/2026/TT-BXD"), 1, 0);
        check("TT 02/2025/TT-BXD → TT 34/2026/TT-BXD", B(F("02/2025/TT-BXD").ReplacedBy == "34/2026/TT-BXD"), 1, 0);
        check("TT 34/2026: hiệu lực 01/7/2026, thay thế 06/2021", B(F("34/2026/TT-BXD").EffectiveDate == "01/7/2026" && F("34/2026/TT-BXD").Replaces.Contains("06/2021/TT-BXD")), 1, 0);
        check("TT 12/2021/TT-BXD → TT 38/2026/TT-BXD", B(F("12/2021/TT-BXD").Status == RegulationStatus.HetHieuLuc && F("12/2021/TT-BXD").ReplacedBy == "38/2026/TT-BXD"), 1, 0);
        check("TT 09/2024, 08/2025 → TT 38/2026", B(F("09/2024/TT-BXD").ReplacedBy == "38/2026/TT-BXD" && F("08/2025/TT-BXD").ReplacedBy == "38/2026/TT-BXD"), 1, 0);
        check("TT 13/2021/TT-BXD → TT 37/2026/TT-BXD", B(F("13/2021/TT-BXD").Status == RegulationStatus.HetHieuLuc && F("13/2021/TT-BXD").ReplacedBy == "37/2026/TT-BXD"), 1, 0);
        check("TT 37/2026: 26/6/2026, hiệu lực 01/7/2026, thay TT 13/2021 + TT 01/2025",
            B(F("37/2026/TT-BXD").IssuedDate == "26/6/2026" && F("37/2026/TT-BXD").Replaces.Contains("13/2021/TT-BXD") && F("37/2026/TT-BXD").Replaces.Contains("01/2025/TT-BXD")), 1, 0);
        check("TT 38/2026: ghi chú CV 9947 thay Phụ lục I–VIII (VII: vật liệu)", B(F("38/2026/TT-BXD").Note.Contains("9947/BXD-VPB") && F("38/2026/TT-BXD").Note.Contains("vật liệu xây dựng")), 1, 0);
        check("CV 9947/BXD-VPB: ngày 29/6/2026", B(F("9947/BXD-VPB").IssuedDate == "29/6/2026" && F("9947/BXD-VPB").Kind == RegulationKind.CongVan), 1, 0);
        check("TCVN 9386:2012: hết hiệu lực, ReplacedBy chứa TCVN 9386-1:2025 và 9386-5:2025",
            B(F("TCVN 9386:2012").Status == RegulationStatus.HetHieuLuc && F("TCVN 9386:2012").ReplacedBy.Contains("TCVN 9386-1:2025") && F("TCVN 9386:2012").ReplacedBy.Contains("TCVN 9386-5:2025")), 1, 0);
        check("TCVN 9386-1:2025: trạng thái Kiểm tra, hiệu lực 27/10/2026", B(F("TCVN 9386-1:2025").Status == RegulationStatus.KiemTra && F("TCVN 9386-1:2025").EffectiveDate == "27/10/2026"), 1, 0);
        check("TCVN 2737:1995 → TCVN 2737:2023", B(F("TCVN 2737:1995").ReplacedBy == "TCVN 2737:2023"), 1, 0);
        check("QCVN 02:2022/BXD còn hiệu lực; bản sửa đổi là Dự thảo",
            B(F("QCVN 02:2022/BXD").Status == RegulationStatus.HieuLuc && reg.Docs.Any(d => d.Topic == Topics.NaturalDataDraft && d.Status == RegulationStatus.DuThao)), 1, 0);
        check("TCVN 5574:2018, 5575:2024, 10304:2014, 9362:2012 còn hiệu lực",
            B(new[] { "TCVN 5574:2018", "TCVN 5575:2024", "TCVN 10304:2014", "TCVN 9362:2012" }.All(c => F(c).Status == RegulationStatus.HieuLuc)), 1, 0);
        string[] urls =
        {
            "https://qlda.gxd.vn/van-ban/luat/luat-xay-dung-135-2025.html", "nghi-dinh-217_2026_nd-cp", "nghi-dinh-212-2026.html", "nghi-dinh-206-2026.html",
            "thong-tu-34-2026-tt-bxd", "thong-tu-37-2026-TT-BXD.html", "38-bxd.signed.pdf", "cong-van-9947-bxd-vpb", "tcvn-9386-5-2025", "Thuyet-minh-sua-doi-QCVN02-v2.pdf"
        };
        check("Danh mục: có đủ 10 đường dẫn nguồn đã kiểm tra", urls.Count(u => reg.Docs.Any(d => d.SourceUrl.Contains(u))), urls.Length, 0);
        check("Danh mục: mọi văn bản hết hiệu lực/kiểm tra-thay thế đều có văn bản đang áp dụng thay", reg.Docs.Where(d => d.IsSuperseded).Count(d => reg.Successors(d).Count == 0), 0, 0);

        // tra theo chủ đề
        check("Std.Law = Luật Xây dựng số 135/2025/QH15", B(Std.Law == "Luật Xây dựng số 135/2025/QH15"), 1, 0);
        check("Std.Decree = Nghị định 217/2026/NĐ-CP (không phải 175/2024)", B(Std.Decree == "Nghị định 217/2026/NĐ-CP"), 1, 0);
        check("Std.Grade = Thông tư 34/2026/TT-BXD", B(Std.Grade == "Thông tư 34/2026/TT-BXD"), 1, 0);
        check("Std.Norms = Thông tư 38/2026/TT-BXD", B(Std.Norms == "Thông tư 38/2026/TT-BXD"), 1, 0);
        check("Std.Seismic = TCVN 9386-1:2025", B(Std.Seismic == "TCVN 9386-1:2025"), 1, 0);
        check("Std.Loads = TCVN 2737:2023", B(Std.Loads == "TCVN 2737:2023"), 1, 0);
        check("Std.SeismicClause ghi \"đối chiếu\", không có \"TCVN 9386:2012\"", B(Std.SeismicClause("mục 4.3.3.2.2").Contains("đối chiếu") && !Std.SeismicClause("mục 4.3.3.2.2").Contains("TCVN 9386:2012")), 1, 0);
        check("Ghi chú phương pháp động đất: nhắc bản 2012 trong câu \"thay thế\"", B(Std.SeismicMethodNote.Contains("thay thế TCVN 9386:2012") && Std.SeismicMethodNote.Contains("bản 2025")), 1, 0);
        var succ = reg.Successors(reg.Find("TCVN 9386:2012"));
        check("Thay thế TCVN 9386:2012 → 2 văn bản đang áp dụng", succ.Count, 2, 0);
        check("Thay thế 15/2021/NĐ-CP → 217/2026/NĐ-CP", B(reg.Successors(reg.Find("15/2021/NĐ-CP")).Any(x => x.Code == "217/2026/NĐ-CP")), 1, 0);
        check("Hiện hành theo phạm vi Định mức gồm TT 37, TT 38, CV 9947",
            B(new[] { "37/2026/TT-BXD", "38/2026/TT-BXD", "9947/BXD-VPB" }.All(c => RegulationService.Current(RegulationScope.DinhMuc).Any(d => d.Code == c))), 1, 0);
        check("Hiện hành không gồm văn bản hết hiệu lực / dự thảo", RegulationService.Current().Count(d => d.Status is RegulationStatus.HetHieuLuc or RegulationStatus.DuThao), 0, 0);

        // văn bản áp dụng theo nội dung dự án
        var p = ModelBuilder.NewProject("Thử văn bản");
        var ap = RegulationService.Applied(p).Select(d => d.Code).ToList();
        check("Applied: Luật 135, NĐ 217, TT 34, TCVN 2737, 5574, QCVN 02, TCVN 9362",
            B(new[] { "135/2025/QH15", "217/2026/NĐ-CP", "34/2026/TT-BXD", "TCVN 2737:2023", "TCVN 5574:2018", "QCVN 02:2022/BXD", "TCVN 9362:2012" }.All(ap.Contains)), 1, 0);
        check("Applied: luôn nêu TCVN 9386-1:2025 và NĐ 212/2026 (năng lực); móng đơn → không có TCVN 10304", B(ap.Contains("TCVN 9386-1:2025") && ap.Contains("212/2026/NĐ-CP") && !ap.Contains("TCVN 10304:2014")), 1, 0);
        p.Seismic.Enabled = true; p.Foundation.Type = FoundationType.Pile;
        ap = RegulationService.Applied(p, true).Select(d => d.Code).ToList();
        check("Applied: bật động đất → TCVN 9386-1:2025 + 9386-5:2025; móng cọc → TCVN 10304:2014",
            B(ap.Contains("TCVN 9386-1:2025") && ap.Contains("TCVN 9386-5:2025") && ap.Contains("TCVN 10304:2014") && !ap.Contains("TCVN 9362:2012")), 1, 0);
        check("Applied (có khối lượng): NĐ 206/2026, TT 36, 37, 38/2026 + CV 9947", B(new[] { "206/2026/NĐ-CP", "36/2026/TT-BXD", "37/2026/TT-BXD", "38/2026/TT-BXD", "9947/BXD-VPB" }.All(ap.Contains)), 1, 0);
        check("Applied: không có văn bản hết hiệu lực", RegulationService.Applied(p, true).Count(d => !d.IsCurrent), 0, 0);
    }

    // ------------------------------------------------------------------
    private static void UserFile(Action<string, double, double, double> check, string tmp)
    {
        int nBuilt = Regulations.BuiltIn().Docs.Count;
        // file người dùng: sửa 1, thêm 1, xoá 1
        string json = """
        {
          "Version": 1,
          "Docs": [
            { "Code": "TCVN 5574:2018", "Title": "Thiết kế kết cấu bê tông và bê tông cốt thép", "Kind": "TCVN", "Status": "HieuLuc", "Scope": "BTCT", "Topic": "btct", "Note": "Ghi chú người dùng" },
            { "Code": "TCVN 99999:2099", "Title": "Văn bản thử của người dùng", "Kind": "TCVN", "Status": "HieuLuc", "Scope": "Thep" }
          ],
          "Removed": [ "40/2026/TT-BXD" ]
        }
        """;
        File.WriteAllText(RegulationService.UserFilePath, json, new UTF8Encoding(false));
        RegulationService.Reload();
        var reg = RegulationService.Registry;
        check("File người dùng: đọc không lỗi", B(reg.LoadMessage == null), 1, 0);
        check("File người dùng: sửa văn bản có sẵn (ghi chú)", B(reg.Find("TCVN 5574:2018")?.Note == "Ghi chú người dùng"), 1, 0);
        check("File người dùng: thêm văn bản mới", B(reg.Find("TCVN 99999:2099") != null), 1, 0);
        check("File người dùng: xoá văn bản có sẵn", B(reg.Find("40/2026/TT-BXD") == null), 1, 0);
        check("File người dùng: số văn bản = có sẵn + 1 − 1", reg.Docs.Count, nBuilt, 0);
        check("File người dùng: văn bản khác giữ nguyên (Luật 135)", B(reg.Find("135/2025/QH15")?.EffectiveDate == "01/7/2026"), 1, 0);

        // file hỏng → danh mục có sẵn + thông báo
        File.WriteAllText(RegulationService.UserFilePath, "{ hỏng", new UTF8Encoding(false));
        RegulationService.Reload();
        check("File hỏng: dùng danh mục có sẵn + thông báo", B(RegulationService.Registry.LoadMessage != null && RegulationService.Registry.Docs.Count == nBuilt), 1, 0);
        RegulationService.ResetUserDefault();
        check("Xoá file người dùng → danh mục có sẵn", B(!File.Exists(RegulationService.UserFilePath) && RegulationService.Registry.Find("40/2026/TT-BXD") != null), 1, 0);

        // thêm văn bản động đất mới cùng chủ đề → trích dẫn tự đổi theo
        var r2 = RegulationService.Registry.Clone();
        r2.Upsert(new RegulationDoc { Code = "TCVN 99999-1:2099", Title = "Văn bản thử thay thế", Kind = RegulationKind.TCVN, Status = RegulationStatus.HieuLuc, Scope = RegulationScope.DongDat, Topic = Topics.Seismic, Replaces = new List<string> { "TCVN 9386-1:2025" } });
        r2.MarkSuperseded("TCVN 9386-1:2025", "TCVN 99999-1:2099");
        check("Đánh dấu hết hiệu lực: trạng thái + ReplacedBy", B(r2.Find("TCVN 9386-1:2025").Status == RegulationStatus.HetHieuLuc && r2.Find("TCVN 9386-1:2025").ReplacedBy == "TCVN 99999-1:2099"), 1, 0);
        RegulationService.SaveUserDefault(r2);
        var saved = Regulations.ReadFile(RegulationService.UserFilePath);
        check("Lưu làm mặc định: chỉ lưu phần khác (2 văn bản)", saved.Docs.Count, 2, 0);
        RegulationService.Reload();
        check("Lưu làm mặc định → đọc lại: Std.Seismic theo văn bản mới", B(Std.Seismic == "TCVN 99999-1:2099"), 1, 0);
        check("Văn bản mới thay → TCVN 9386:2012 thay bằng văn bản mới (chuỗi thay thế)",
            B(RegulationService.Registry.Successors(RegulationService.Registry.Find("TCVN 9386:2012")).Any(x => x.Code == "TCVN 99999-1:2099")), 1, 0);
        RegulationService.ResetUserDefault();
        check("Khôi phục: Std.Seismic = TCVN 9386-1:2025", B(Std.Seismic == "TCVN 9386-1:2025"), 1, 0);

        // xuất / nhập JSON (đối tượng và mảng)
        string exp = Path.Combine(tmp, "XuatDanhMuc.json");
        RegulationService.Registry.ExportJson(exp);
        var empty = new Regulations();
        empty.ImportJson(exp);
        check("Xuất → nhập JSON: đủ văn bản", empty.Docs.Count, nBuilt, 0);
        check("Xuất → nhập JSON: trạng thái + ReplacedBy giữ nguyên", B(empty.Find("TCVN 9386:2012")?.ReplacedBy == "TCVN 9386-1:2025; TCVN 9386-5:2025" && empty.Find("38/2026/TT-BXD")?.Status == RegulationStatus.HieuLuc), 1, 0);
        check("Xuất JSON: enum ghi dạng chữ", B(File.ReadAllText(exp).Contains("\"HetHieuLuc\"")), 1, 0);
        string arr = Path.Combine(tmp, "Mang.json");
        File.WriteAllText(arr, JsonSerializer.Serialize(new[] { new RegulationDoc { Code = "TCVN 5574:2018", Title = "x", Note = "Sửa từ mảng", Kind = RegulationKind.TCVN, Topic = Topics.Concrete } }, Regulations.JsonOptions));
        var rr = RegulationService.Registry.Clone();
        var (a2, u2, _) = rr.ImportJson(arr);
        check("Nhập JSON dạng mảng: cập nhật 1, thêm 0", a2 * 10 + u2, 1, 0);
        check("Nhập JSON dạng mảng: nội dung mới", B(rr.Find("TCVN 5574:2018")?.Note == "Sửa từ mảng"), 1, 0);
        RegulationService.Reload();
    }

    // ------------------------------------------------------------------
    private static List<string> Paragraphs(string docx)
    {
        using var z = ZipFile.OpenRead(docx);
        var e = z.GetEntry("word/document.xml");
        if (e == null) return new List<string>();
        using var s = e.Open();
        var x = XDocument.Load(s);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return x.Descendants(w + "p").Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))).Where(t => t.Length > 0).ToList();
    }

    /// <summary>Số đoạn trích dẫn văn bản đã hết hiệu lực mà không nằm trong câu "thay thế".</summary>
    private static int BadCitations(List<string> paras, out string sample)
    {
        sample = "";
        int n = 0;
        foreach (var t in paras)
        {
            if (t.Contains("thay thế")) continue;
            var bad = RegulationService.SupersededCitedIn(t);
            if (bad.Count == 0) continue;
            n++;
            if (sample.Length == 0) sample = bad[0].Code + " trong \"" + (t.Length > 120 ? t.Substring(0, 120) + "…" : t) + "\"";
        }
        return n;
    }

    private static Project House()
    {
        var p = ModelBuilder.NewProject("Thử thuyết minh văn bản");
        p.Location = "Thái Nguyên";
        ModelBuilder.SetGrid(p, new[] { 4.0, 4.0 }, new[] { 5.0 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5 });
        return p;
    }

    private static void Reports(Action<string, double, double, double> check, string outDir)
    {
        var p = House();
        p.Seismic.Enabled = true; p.Seismic.AgR = 0.1;
        var r = FrameAnalysis.Run(p);
        var ds = ConcreteDesigner.Design(p, r);

        string doc1 = Path.Combine(outDir, "VanBan_ThuyetMinh.docx");
        CalculationReport.Export(p, r, ds, doc1);
        var ps = Paragraphs(doc1);
        string all = string.Join("\n", ps);
        check("Thuyết minh: chương \"CĂN CỨ PHÁP LÝ VÀ TIÊU CHUẨN ÁP DỤNG\"", B(ps.Any(t => t.Contains("CĂN CỨ PHÁP LÝ VÀ TIÊU CHUẨN ÁP DỤNG"))), 1, 0);
        check("Thuyết minh: có hai bảng (Văn bản pháp lý; Tiêu chuẩn, quy chuẩn kỹ thuật)", B(ps.Any(t => t.EndsWith("Văn bản pháp lý")) && ps.Any(t => t.EndsWith("Tiêu chuẩn, quy chuẩn kỹ thuật"))), 1, 0);
        check("Thuyết minh: có \"Luật Xây dựng số 135/2025/QH15\"", B(all.Contains("Luật Xây dựng số 135/2025/QH15")), 1, 0);
        check("Thuyết minh: có \"217/2026/NĐ-CP\"", B(all.Contains("217/2026/NĐ-CP")), 1, 0);
        check("Thuyết minh: có \"34/2026/TT-BXD\" (cấp công trình)", B(all.Contains("34/2026/TT-BXD")), 1, 0);
        check("Thuyết minh: có \"TCVN 9386-1:2025\"", B(all.Contains("TCVN 9386-1:2025")), 1, 0);
        check("Thuyết minh: ghi chú đối chiếu bản 2025 (phổ, γI, gia tốc nền)", B(all.Contains("đối chiếu phổ phản ứng, hệ số tầm quan trọng, gia tốc nền theo bản 2025")), 1, 0);
        int bad1 = BadCitations(ps, out string s1);
        if (bad1 > 0) Console.WriteLine("   · trích dẫn cũ: " + s1);
        check("Thuyết minh: \"TCVN 9386:2012\" chỉ xuất hiện trong câu \"thay thế\"", ps.Count(t => t.Contains("TCVN 9386:2012") && !t.Contains("thay thế")), 0, 0);
        check("Thuyết minh: không trích dẫn văn bản hết hiệu lực ngoài câu \"thay thế\"", bad1, 0, 0);
        check("Thuyết minh: có câu \"TCVN 9386-1:2025 thay thế TCVN 9386:2012\"", B(ps.Any(t => t.Contains("TCVN 9386-1:2025 thay thế TCVN 9386:2012"))), 1, 0);
        check("Thuyết minh: không còn \"Phụ lục H TCVN 9386\"", B(!all.Contains("Phụ lục H TCVN 9386")), 1, 0);

        // thuyết minh tự động (có chương khối lượng)
        string doc2 = Path.Combine(outDir, "VanBan_ThuyetMinhTuDong.docx");
        var o = ReportOptions.FromProject(p);
        CalculationReport.Export(p, r, ds, doc2, o);
        var ps2 = Paragraphs(doc2);
        string all2 = string.Join("\n", ps2);
        check("Thuyết minh tự động: Luật 135/2025, NĐ 217/2026, TCVN 9386-1:2025",
            B(all2.Contains("Luật Xây dựng số 135/2025/QH15") && all2.Contains("217/2026/NĐ-CP") && all2.Contains("TCVN 9386-1:2025")), 1, 0);
        check("Thuyết minh tự động: chương khối lượng trích \"38/2026/TT-BXD\"", B(all2.Contains("38/2026/TT-BXD") && all2.Contains("9947/BXD-VPB")), 1, 0);
        int bad2 = BadCitations(ps2, out string s2);
        if (bad2 > 0) Console.WriteLine("   · trích dẫn cũ (tự động): " + s2);
        check("Thuyết minh tự động: không trích dẫn văn bản hết hiệu lực ngoài câu \"thay thế\"", bad2, 0, 0);
        check("Thuyết minh tự động: không còn \"1776/BXD-VP\"", B(!all2.Contains("1776/BXD-VP")), 1, 0);

        // không bật động đất: kiến nghị vẫn nhắc tiêu chuẩn động đất hiện hành
        var p0 = House();
        var r0 = FrameAnalysis.Run(p0);
        var ds0 = ConcreteDesigner.Design(p0, r0);
        string doc3 = Path.Combine(outDir, "VanBan_KhongDongDat.docx");
        CalculationReport.Export(p0, r0, ds0, doc3, ReportOptions.FromProject(p0));
        var ps3 = Paragraphs(doc3);
        check("Không động đất: kiến nghị \"… theo TCVN 9386-1:2025\"", B(ps3.Any(t => t.Contains("Chưa xét tải trọng động đất") && t.Contains("TCVN 9386-1:2025"))), 1, 0);
        check("Không động đất: không trích dẫn văn bản cũ", BadCitations(ps3, out _), 0, 0);

        // phụ lục riêng
        string pl = Path.Combine(outDir, "VanBan_PhuLuc.docx");
        AppendixReport.ExportDocx(p, pl);
        var pp = Paragraphs(pl);
        check("Phụ lục: trích \"TCVN 9386-1:2025\"", B(pp.Any(t => t.Contains("TCVN 9386-1:2025"))), 1, 0);
        check("Phụ lục: \"TCVN 9386:2012\" chỉ trong câu \"thay thế\"", pp.Count(t => t.Contains("TCVN 9386:2012") && !t.Contains("thay thế")), 0, 0);
        check("Phụ lục: liệt kê văn bản áp dụng (Luật 135/2025)", B(pp.Any(t => t.Contains("Văn bản, tiêu chuẩn áp dụng") && t.Contains("135/2025/QH15"))), 1, 0);
        var secs = KetCauVXT.Docs.Appendix.Sections(p);
        string apx = string.Join("\n", secs.Select(s => s.Intro + " " + string.Join(" ", s.Items.Select(i => i.AllText)) + " " + string.Join(" ", s.Tables.Select(t => t.AllText))));
        check("Phụ lục (dữ liệu): agR tra theo QCVN 02:2022/BXD Bảng 6.1, 6.2", B(apx.Contains("QCVN 02:2022/BXD (Bảng 6.1, 6.2)")), 1, 0);

        // ghi chú chung bản vẽ
        var notes = GeneralNotes.Lines(p, NotesScope.All);
        var std = notes.FirstOrDefault(l => l.Contains("Tiêu chuẩn thiết kế"));
        check("Ghi chú chung: dòng tiêu chuẩn thiết kế có TCVN 9386-1:2025, Luật 135/2025, NĐ 217/2026",
            B(std != null && std.Contains("TCVN 9386-1:2025") && std.Contains("135/2025/QH15") && std.Contains("217/2026/NĐ-CP") && std.Contains("TCVN 5574:2018")), 1, 0);
        check("Ghi chú chung (mặc định tờ in): có dòng tiêu chuẩn, không có văn bản cũ",
            B(SheetBuilder.DefaultNotes(p).Contains("Tiêu chuẩn thiết kế") && RegulationService.SupersededCitedIn(SheetBuilder.DefaultNotes(p)).Count == 0), 1, 0);
        Console.WriteLine($"   · thuyết minh: {doc1}; tự động: {doc2}");
    }

    // ------------------------------------------------------------------
    private static void Quantities(Action<string, double, double, double> check)
    {
        check("Khối lượng: nguồn định mức mặc định trích \"38/2026/TT-BXD\"", B(QuantitySettings.DefaultSource.Contains("38/2026/TT-BXD")), 1, 0);
        check("Khối lượng: nguồn mặc định có Phụ lục VII, CV 9947/BXD-VPB, THAM KHẢO",
            B(QuantitySettings.DefaultSource.Contains("Phụ lục VII") && QuantitySettings.DefaultSource.Contains("9947/BXD-VPB") && QuantitySettings.DefaultSource.Contains("THAM KHẢO")), 1, 0);
        check("Khối lượng: nguồn mặc định không trích văn bản cũ", RegulationService.SupersededCitedIn(QuantitySettings.DefaultSource).Count, 0, 0);
        var qs = new QuantitySettings();
        check("Khối lượng: dự án mới → EffectiveSource = nguồn mặc định", B(qs.EffectiveSource() == QuantitySettings.DefaultSource), 1, 0);
        qs.NormSource = QuantitySettings.LegacyDefaultSources[0];
        check("Khối lượng: ghi chú nguồn mặc định cũ (12/2021, 1776) tự thay bằng nguồn hiện hành", B(qs.EffectiveSource().Contains("38/2026/TT-BXD") && !qs.EffectiveSource().Contains("12/2021")), 1, 0);
        qs.NormSource = "Định mức công ty";
        check("Khối lượng: ghi chú nguồn của người dùng giữ nguyên", B(qs.EffectiveSource() == "Định mức công ty"), 1, 0);
        check("Khối lượng: ghi chú định mức xây mặc định không còn \"1776\"", B(QuantitySettings.DefaultMasonryNorms().All(n => !n.Note.Contains("1776"))), 1, 0);
        var p = House();
        var q = QuantityCalculator.Compute(p, null);
        check("Khối lượng: ghi chú bảng trích \"38/2026/TT-BXD\"", B(q.Notes.Any(n => n.Contains("38/2026/TT-BXD"))), 1, 0);
        var nt = QuantityReport.NormTables(q.Settings ?? new QuantitySettings());
        check("Khối lượng: bảng \"Nguồn định mức\" trích \"38/2026/TT-BXD\"", B(nt[0].Rows.Any(row => row.Any(c => (c as string ?? "").Contains("38/2026/TT-BXD")))), 1, 0);
    }

    // ------------------------------------------------------------------
    private static void CheckProject(Action<string, double, double, double> check)
    {
        var p0 = ModelBuilder.NewProject("Mới");
        check("Kiểm tra dự án: dự án mới không có cảnh báo", RegulationService.CheckProject(p0).Count(w => w.IsWarning), 0, 0);

        var p1 = ModelBuilder.NewProject("Định mức cũ");
        p1.QuantitySettings.NormSource = "Theo định mức 12/2021/TT-BXD của Bộ Xây dựng";
        var w1 = RegulationService.CheckProject(p1);
        var hit = w1.FirstOrDefault(w => w.Code == "12/2021/TT-BXD");
        check("Kiểm tra dự án: ghi chú trích \"12/2021/TT-BXD\" → cảnh báo", B(hit != null && hit.IsWarning), 1, 0);
        check("Kiểm tra dự án: gợi ý thay bằng TT 38/2026/TT-BXD", B(hit != null && hit.Message.Contains("38/2026/TT-BXD")), 1, 0);

        var p2 = ModelBuilder.NewProject("Ghi chú tờ in");
        p2.Sheets.Add(new Sheet { Number = "KC-01", Views = new List<SheetView> { new SheetView { Param = "1. Thiết kế theo TCVN 9386:2012 và Luật Xây dựng 2014." } } });
        var w2 = RegulationService.CheckProject(p2);
        check("Kiểm tra dự án: ghi chú tờ in trích TCVN 9386:2012 → cảnh báo", B(w2.Any(w => w.Code == "TCVN 9386:2012" && w.IsWarning && w.Message.Contains("TCVN 9386-1:2025"))), 1, 0);
        check("Kiểm tra dự án: \"Luật Xây dựng 2014\" → cảnh báo 50/2014/QH13", B(w2.Any(w => w.Code == "50/2014/QH13")), 1, 0);

        var p3 = ModelBuilder.NewProject("Viết tắt");
        p3.QuantitySettings.NormSource = "Phương pháp theo Thông tư 13/2021; chi phí theo NĐ 10/2021";
        var w3 = RegulationService.CheckProject(p3);
        check("Kiểm tra dự án: \"Thông tư 13/2021\" (không hậu tố) → cảnh báo", B(w3.Any(w => w.Code == "13/2021/TT-BXD")), 1, 0);
        check("Kiểm tra dự án: \"NĐ 10/2021\" → cảnh báo", B(w3.Any(w => w.Code == "10/2021/NĐ-CP")), 1, 0);

        var p4 = ModelBuilder.NewProject("Đúng");
        p4.QuantitySettings.NormSource = "Theo TCVN 9386-1:2025, TCVN 2737:2023 và Thông tư 38/2026/TT-BXD";
        check("Kiểm tra dự án: trích văn bản hiện hành → không cảnh báo", RegulationService.CheckProject(p4).Count(w => w.IsWarning), 0, 0);

        var p5 = ModelBuilder.NewProject("Vùng gió cũ");
        p5.Wind.Zone = "IIB";
        check("Kiểm tra dự án: vùng gió \"IIB\" (TCVN 2737:1995) → cảnh báo", B(RegulationService.CheckProject(p5).Any(w => w.IsWarning && w.Code == "TCVN 2737:1995")), 1, 0);

        var p6 = ModelBuilder.NewProject("Mặc định cũ");
        p6.QuantitySettings.NormSource = QuantitySettings.LegacyDefaultSources[0];
        check("Kiểm tra dự án: ghi chú nguồn mặc định cũ (tự thay) → không cảnh báo", RegulationService.CheckProject(p6).Count(w => w.IsWarning), 0, 0);
        p6.QuantitySettings.MasonryNorms[0].Note = "1776 AE.221xx";
        check("Kiểm tra dự án: ghi chú định mức xây \"1776 AE.221xx\" (file cũ) → cảnh báo", B(RegulationService.CheckProject(p6).Any(w => w.IsWarning && w.Code == "1776/BXD-VP")), 1, 0);

        var p7 = ModelBuilder.NewProject("Nghị định 175");
        p7.QuantitySettings.NormSource = "Căn cứ NĐ 175/2024/NĐ-CP";
        var w7 = RegulationService.CheckProject(p7).FirstOrDefault(w => w.Code == "175/2024/NĐ-CP");
        check("Kiểm tra dự án: NĐ 175/2024 (đã hết hiệu lực) → cảnh báo", B(w7 != null && w7.IsWarning), 1, 0);

        var p8 = ModelBuilder.NewProject("Động đất");
        p8.Seismic.Enabled = true;
        check("Kiểm tra dự án: bật động đất → lưu ý đối chiếu bản 2025", B(RegulationService.CheckProject(p8).Any(w => !w.IsWarning && w.Message.Contains("bản 2025"))), 1, 0);
    }

    // ------------------------------------------------------------------
    private static void NormExcel(Action<string, double, double, double> check, string tmp)
    {
        string path = Path.Combine(tmp, "DinhMuc_Thu.xlsx");
        var x = new XlsxWriter();
        var sh = x.AddSheet("Dinh muc");
        sh.AddRow("BẢNG ĐỊNH MỨC VẬT LIỆU (dữ liệu kiểm thử)");
        sh.AddHeaderRow("Mã hiệu", "Tên công tác", "Đơn vị", "Vật liệu", "Đơn vị vật liệu", "Định mức");
        sh.AddRow("T.01", "Cấp phối bê tông B25, đá 1x2, xi măng PC40", "m³", "Xi măng PC40", "kg", 421.5);
        sh.AddRow("", "", "", "Cát vàng", "m³", 0.448);
        sh.AddRow("", "", "", "Đá dăm 1x2", "m³", 0.871);
        sh.AddRow("", "", "", "Nước", "lít", 190);
        sh.AddRow("T.02", "Cấp phối bê tông B35, đá 1x2", "m³", "Xi măng PC40", "tấn", 0.5);
        sh.AddRow("", "", "", "Cát vàng", "m³", 0.40);
        sh.AddRow("T.03", "Cấp phối vữa xây xi măng cát vàng mác 75", "m³", "Xi măng PC30", "kg", 300.5);
        sh.AddRow("", "", "", "Cát vàng", "m³", 1.1);
        sh.AddRow("", "", "", "Nước", "m³", 0.26);
        sh.AddRow("T.04", "Xây tường gạch đặc dày ≤ 11 cm", "m³", "Gạch đặc", "viên", 640);
        sh.AddRow("VÍ DỤ", "VÍ DỤ – dòng mẫu", "m³", "Xi măng", "kg", 999);
        x.Save(path);

        var qs = new QuantitySettings();
        int nConc = qs.ConcreteNorms.Count;
        var r = NormImport.ImportFile(qs, path);
        check("Nhập định mức Excel: 10 dòng (bỏ dòng ví dụ)", r.Rows, 10, 0);
        check("Nhập định mức Excel: 2 cấp phối bê tông, 1 cấp phối vữa, 1 dòng tham khảo", r.Concrete * 100 + r.Mortar * 10 + r.Other, 211, 0);
        var b25 = qs.ConcreteNorm("B25");
        check("Nhập định mức Excel: B25 xi măng 421,5 kg", b25?.Cement ?? 0, 421.5, 1e-12);
        check("Nhập định mức Excel: B25 cát 0,448 m³", b25?.Sand ?? 0, 0.448, 1e-12);
        check("Nhập định mức Excel: B25 đá 0,871 m³", b25?.Stone ?? 0, 0.871, 1e-12);
        check("Nhập định mức Excel: B25 nước 190 lít", b25?.Water ?? 0, 190, 1e-12);
        check("Nhập định mức Excel: B35 (mới) xi măng 0,5 tấn → 500 kg", qs.ConcreteNorm("B35")?.Cement ?? 0, 500, 1e-12);
        check("Nhập định mức Excel: thêm cấp B35 vào bảng", qs.ConcreteNorms.Count, nConc + 1, 0);
        var m75 = qs.MortarNorm("M75");
        check("Nhập định mức Excel: vữa M75 xi măng 300,5 kg", m75?.Cement ?? 0, 300.5, 1e-12);
        check("Nhập định mức Excel: vữa M75 nước 0,26 m³ → 260 lít", m75?.Water ?? 0, 260, 1e-12);
        check("Nhập định mức Excel: lưu đủ bảng nhập (10 dòng, mã hiệu kéo xuống)", B(qs.ImportedNorms.Count == 10 && qs.ImportedNorms[1].Code == "T.01" && qs.ImportedNorms[1].Material == "Cát vàng"), 1, 0);
        check("Nhập định mức Excel: ghi chú nguồn trích \"38/2026/TT-BXD\" + tên file",
            B(qs.NormSource.Contains("38/2026/TT-BXD") && qs.NormSource.Contains("DinhMuc_Thu.xlsx") && qs.ImportedNormFile.StartsWith("DinhMuc_Thu.xlsx")), 1, 0);
        check("Nhập định mức Excel: ghi chú cấp phối \"Nhập từ Excel – T.01\"", B(b25?.Note == "Nhập từ Excel – T.01"), 1, 0);

        // lưu → mở dự án giữ bảng nhập; tính vật tư theo định mức nhập
        var p = House();
        p.QuantitySettings = qs;
        var back = JsonSerializer.Deserialize<Project>(JsonSerializer.Serialize(p, Project.JsonOptions), Project.JsonOptions);
        check("Nhập định mức Excel: lưu/mở dự án giữ bảng định mức nhập", back?.QuantitySettings?.ImportedNorms?.Count ?? 0, 10, 0);
        var q = QuantityCalculator.Compute(p, null);
        var line = q.MaterialLines.FirstOrDefault(l => l.Source == "Bê tông B25" && l.Material == "Xi măng");
        check("Nhập định mức Excel: phân tích vật tư dùng định mức nhập (B25: 421,5 kg/m³)", line?.Norm ?? 0, 421.5, 1e-12);
        var nt = QuantityReport.NormTables(qs);
        check("Nhập định mức Excel: bảng Định mức có mục \"Định mức nhập từ Excel\" (10 dòng)", nt.FirstOrDefault(t => t.Title.StartsWith("Định mức nhập từ Excel"))?.Rows.Count ?? 0, 10, 0);

        // file mẫu: chỉ có dòng ví dụ → không nhập gì
        string tpl = Path.Combine(tmp, "MauDinhMuc.xlsx");
        NormImport.WriteTemplate(tpl);
        var qs2 = new QuantitySettings();
        var r2 = NormImport.ImportFile(qs2, tpl);
        check("File mẫu định mức: dòng ví dụ bị bỏ qua (0 dòng, bảng cấp phối giữ nguyên)", B(r2.Rows == 0 && qs2.ConcreteNorm("B25")?.Cement == 431.0 && qs2.ImportedNorms.Count == 0), 1, 0);
        // CSV
        string csv = Path.Combine(tmp, "DinhMuc.csv");
        File.WriteAllText(csv, "Mã hiệu;Tên công tác;Đơn vị;Vật liệu;Đơn vị vật liệu;Định mức\nC.9;Bê tông B20;m³;Xi măng;kg;355,5\n;;;Cát vàng;m³;0,46\n", new UTF8Encoding(true));
        var qs3 = new QuantitySettings();
        var r3 = NormImport.ImportFile(qs3, csv);
        check("Nhập định mức CSV ';' (dấu phẩy thập phân): B20 xi măng 355,5", qs3.ConcreteNorm("B20")?.Cement ?? 0, 355.5, 1e-12);
        check("Nhập định mức CSV: 2 dòng", r3.Rows, 2, 0);
        Console.WriteLine($"   · định mức Excel: {r}");
    }
}
