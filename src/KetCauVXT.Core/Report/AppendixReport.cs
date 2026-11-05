using KetCauVXT.Docs;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Xuất "PHỤ LỤC TÍNH TOÁN" (công thức, hệ số, hệ số an toàn, giải thích, tiêu chuẩn) ra Word / Excel.
/// Nội dung lấy từ <see cref="Appendix.Sections"/>.</summary>
public static class AppendixReport
{
    public const string ChapterTitle = "PHỤ LỤC TÍNH TOÁN";

    /// <summary>Tiêu đề 7 cột bảng phụ lục.</summary>
    public static readonly string[] Headers = { "Hạng mục", "Ký hiệu", "Công thức", "Hệ số / giá trị", "Hệ số an toàn", "Giải thích", "Tiêu chuẩn" };

    /// <summary>Bề rộng cột (cm) – tổng 16,5 cm = vùng in A4 đứng của DocxWriter.</summary>
    private static readonly double[] Widths = { 2.3, 1.3, 3.4, 2.6, 1.6, 3.7, 1.6 };

    public static string[] Row(AppendixItem it) =>
        new[] { it.Name, it.Symbol, it.Formula, it.Values, it.SafetyFactor, it.Explanation, it.Standard };

    /// <summary>Ghi chương phụ lục vào một thuyết minh đang lập (gọi ở cuối CalculationReport.Export).</summary>
    public static void Write(Project p, DocxWriter d, bool pageBreak = true)
    {
        if (d == null) return;
        if (pageBreak) d.PageBreak();
        d.Heading(ChapterTitle, 1);
        d.Paragraph("Phụ lục tổng hợp các công thức, chỉ số, hệ số, hệ số an toàn và phương pháp mà chương trình Việt Const áp dụng, kèm giải thích để người mới "
                    + "có thể tìm hiểu và tự kiểm tra kết quả. Các bảng tra được tính lại bằng chính các hàm của chương trình. "
                    + "Những chỗ ghi \"đơn giản hoá\" / \"gần đúng\" cần được kỹ sư đối chiếu với tiêu chuẩn hiện hành trước khi dùng cho hồ sơ chính thức.");
        d.Paragraph("Quy ước số: dấu phẩy là dấu thập phân (0,95 = 0.95). Đơn vị tính toán kN – m.", false, true);
        var applied = RegulationService.Applied(p);
        if (applied.Count > 0)
            d.Paragraph("Văn bản, tiêu chuẩn áp dụng (theo danh mục văn bản của chương trình – Trợ giúp → Văn bản pháp lý & tiêu chuẩn áp dụng): "
                        + string.Join("; ", applied.Select(x => x.Citation)) + ".", false, true);
        foreach (var s in Appendix.Sections(p)) WriteSection(d, s);
    }

    private static void WriteSection(DocxWriter d, AppendixSection s)
    {
        d.Heading(s.FullTitle, 2);
        if (!string.IsNullOrWhiteSpace(s.Intro)) d.Paragraph(s.Intro);
        if (s.Items.Count > 0) d.Table(Headers, s.Items.Select(Row), Widths, 8.5);
        foreach (var t in s.Tables)
        {
            d.Paragraph(t.Title, true, false, ParaAlign.Left);
            d.Table(t.Headers, t.Rows, null, 9);
            if (!string.IsNullOrWhiteSpace(t.Note)) d.Paragraph("Ghi chú: " + t.Note, false, true, ParaAlign.Left);
        }
    }

    /// <summary>Xuất riêng phụ lục ra file Word (.docx). project có thể null (chỉ nội dung chung, không có giá trị công trình).</summary>
    public static void ExportDocx(Project project, string path)
    {
        var d = new DocxWriter();
        d.Title(ChapterTitle, 18);
        d.Paragraph("Công thức – chỉ số – hệ số – hệ số an toàn – phương pháp áp dụng trong Việt Const", false, true, ParaAlign.Center);
        if (project != null && !string.IsNullOrWhiteSpace(project.Name))
            d.Paragraph("Công trình: " + project.Name + (string.IsNullOrWhiteSpace(project.Location) ? "" : " – " + project.Location), false, false, ParaAlign.Center);
        d.Paragraph("Ngày lập: " + DateTime.Now.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture), false, true, ParaAlign.Center);
        d.EmptyLine();
        d.Heading("Mục lục", 2);
        foreach (var s in Appendix.Sections(project)) d.Bullet(s.FullTitle);
        Write(project, d, true);
        d.Save(path);
    }

    /// <summary>Ghi phụ lục vào một trang tính (ExcelReport: sheet "Phu luc").</summary>
    public static void AddSheet(Project p, XlsxSheet sh)
    {
        if (sh == null) return;
        sh.FirstRowIsHeader = false; sh.FreezeRows = 0;
        sh.AddRow(new XlCell(ChapterTitle, XlStyle.Title));
        sh.AddRow("Công thức, chỉ số, hệ số, hệ số an toàn và phương pháp áp dụng trong Việt Const (dấu phẩy là dấu thập phân).");
        sh.AddRow("Văn bản, tiêu chuẩn áp dụng: " + string.Join("; ", RegulationService.Applied(p).Select(x => x.Citation)) + ".");
        foreach (var s in Appendix.Sections(p))
        {
            sh.AddEmptyRow();
            sh.AddBoldRow(s.FullTitle);
            if (!string.IsNullOrWhiteSpace(s.Intro)) sh.AddRow(s.Intro);
            sh.AddHeaderRow(Headers);
            foreach (var it in s.Items) sh.AddRow(Row(it));
            foreach (var t in s.Tables)
            {
                sh.AddBoldRow("Bảng tra: " + t.Title);
                sh.AddHeaderRow(t.Headers);
                foreach (var r in t.Rows) sh.AddRow(r);
                if (!string.IsNullOrWhiteSpace(t.Note)) sh.AddRow("Ghi chú: " + t.Note);
            }
        }
        sh.SetColumnWidths(34, 14, 60, 50, 28, 70, 28);
    }
}
