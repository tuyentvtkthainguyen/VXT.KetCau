using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Chương "SỐ LIỆU NỘI LỰC NHẬP TỪ &lt;phần mềm&gt;" thay cho "SƠ ĐỒ TÍNH VÀ PHẦN MỀM" khi nội lực được nhập từ ETABS/SAP2000/SAFE
/// (AnalysisResults.External): file nguồn, ngày, đơn vị, bảng đã dùng, phân loại tổ hợp, khớp cấu kiện, ghi chú và trách nhiệm mô hình.</summary>
public static partial class CalculationReport
{
    private static void ExternalResultsSection(Project p, AnalysisResults r, DocxWriter d, int n)
    {
        var x = r.External;
        d.Heading($"{n}. SỐ LIỆU NỘI LỰC NHẬP TỪ {x.Software.ToUpperInvariant()}", 1);
        d.Paragraph($"Nội lực dùng để thiết kế cấu kiện trong thuyết minh này được phân tích bằng phần mềm {x.Software} và nhập vào chương trình Kết cấu VXT "
                    + $"ngày {x.ImportedAt:dd/MM/yyyy HH:mm}. Chương trình không phân tích lại kết cấu; các bước thiết kế, bố trí thép, bản vẽ dùng đúng bộ nội lực nhập về.");
        d.Paragraph("Sơ đồ tính, tải trọng, tổ hợp tải trọng và các giả thiết của mô hình phân tích trong " + x.Software
                    + " thuộc trách nhiệm của người lập mô hình. Người thiết kế cần kiểm tra sự phù hợp giữa mô hình phân tích và hồ sơ thiết kế (kích thước, vật liệu, tải trọng).", false, true);
        d.Heading($"{n}.1. File số liệu", 2);
        d.Table(new[] { "File", "Ngày sửa file" }, x.Files.Select(f => new[] { Path.GetFileName(f.Path), f.Modified == DateTime.MinValue ? "-" : f.Modified.ToString("dd/MM/yyyy HH:mm") }), null, 10);
        if (x.Tables.Count > 0)
            d.Table(new[] { "Bảng", "Số dòng", "Đơn vị trong file", "File" },
                x.Tables.Select(t => new[] { t.Table, t.Rows.ToString(Inv), t.Units, Path.GetFileName(t.File) }), null, 9);
        d.Paragraph("Đơn vị đã quy đổi về kN, m, kN.m. Quy ước dấu theo trục địa phương 1-2-3 (giống ETABS/SAP2000): lực dọc P kéo dương; "
                    + "mô men M3 dương khi căng thớ dưới của dầm; thanh khai báo ngược chiều (đầu I/J đảo) đã được đổi trạm đo và dấu V2, M2.");
        d.Heading($"{n}.2. Tổ hợp dùng thiết kế", 2);
        var uls = r.DesignCombos();
        var sls = r.ServiceCombos();
        d.Bullet("TTGH I (cường độ): " + (uls.Count == 0 ? "-" : string.Join(", ", uls.Take(40)) + (uls.Count > 40 ? $" … ({uls.Count} tổ hợp)" : "")));
        d.Bullet("TTGH II (biến dạng, nứt, nền): " + (sls.Count == 0 ? "-" : string.Join(", ", sls.Take(40)) + (sls.Count > 40 ? $" … ({sls.Count} tổ hợp)" : "")));
        if (!string.IsNullOrEmpty(x.EnvelopeName)) d.Bullet($"Tổ hợp bao {x.EnvelopeName}: bao các tổ hợp TTGH I – dùng vẽ biểu đồ nội lực.");
        d.Heading($"{n}.3. Khớp cấu kiện", 2);
        d.Paragraph($"Số thanh của dự án có nội lực nhập về: {x.MatchedFrames}/{p.Frames.Count}"
                    + (x.FilledFromOwn > 0 ? $"; {x.FilledFromOwn} thanh không có trong file lấy nội lực của chương trình" : "")
                    + (x.UnmatchedFrames - x.FilledFromOwn > 0 ? $"; {x.UnmatchedFrames - x.FilledFromOwn} thanh không có nội lực" : "")
                    + $". Số nút có phản lực/chuyển vị: {x.MatchedNodes}." + (x.SlabPanels > 0 ? $" Số ô sàn thiết kế theo mô men nhập (thay phương pháp hệ số): {x.SlabPanels}." : ""));
        if (x.Punching.Count > 0)
        {
            d.Heading($"{n}.4. Kiểm tra chọc thủng (theo {x.Software})", 2);
            d.Table(new[] { "Điểm", "X (m)", "Y (m)", "Tổ hợp", "Tỷ số", "Đánh giá" },
                x.Punching.OrderByDescending(q => q.Ratio).Take(30).Select(q => new[] { q.Point, N(q.X), N(q.Y), q.Case, N(q.Ratio), q.Ratio <= 1 ? "Đạt" : "KHÔNG ĐẠT" }), null, 10);
        }
        if (x.SoilPressures.Count > 0)
        {
            d.Heading($"{n}.{(x.Punching.Count > 0 ? 5 : 4)}. Áp lực đất nền (theo {x.Software})", 2);
            d.Table(new[] { "Tổ hợp", "p max (kN/m²)", "Điểm", "X (m)", "Y (m)" },
                x.SoilPressures.OrderByDescending(q => q.MaxPressure).Take(20).Select(q => new[] { q.Case, N(q.MaxPressure, "0.0"), q.Point, N(q.X), N(q.Y) }), null, 10);
        }
        if (x.Drifts.Count > 0)
        {
            var top = x.Drifts.GroupBy(q => q.Story).Select(g => g.OrderByDescending(q => q.Drift).First()).ToList();
            d.Paragraph("Chuyển vị lệch tầng lớn nhất theo " + x.Software + ": " + string.Join("; ", top.Select(q => $"{q.Story} {q.Direction} = {q.Drift:0.00000} ({q.Case})")) + ".");
        }
        if (x.Notes.Count > 0)
        {
            d.Paragraph("Ghi chú khi nhập số liệu:", true);
            foreach (var s in x.Notes.Take(30)) d.Bullet(s);
        }
    }
}
