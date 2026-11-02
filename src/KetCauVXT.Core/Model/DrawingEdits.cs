using System.Globalization;

namespace KetCauVXT.Model;

// =====================================================================
// HIỆU CHỈNH BẢN VẼ SAU KHI XUẤT (lưu trong dự án .kcvxt)
//  • DrawingEdits: lớp chỉnh sửa phủ lên bản vẽ tự sinh – dời chữ/nhãn, sửa nội dung chữ, xoá nét, đổi lớp/màu.
//    Mỗi chỉnh sửa gắn với khoá HÌNH (Owner: "BEAM:D2-1", "PLAN:Tầng 2"… – dùng chung cho bộ bản vẽ KC-xx và tờ dàn trang)
//    hoặc khoá BẢN VẼ ("DS:KC-01_MatBangKetCau", "SHEET:KC-03" – khung tên, viền…) và khoá thực thể ổn định (Target).
//    Sau mỗi lần dựng lại/xuất bản vẽ, lớp chỉnh sửa được áp lại; chỉnh sửa không tìm thấy thực thể được báo
//    "chỉnh sửa không còn áp dụng được" (không mất âm thầm).
//  • DrawingNotes: ghi chú/đối tượng thêm (chữ, đường, mũi tên, khung, đám mây…) theo kiểu SheetItem, toạ độ mm giấy của bản vẽ.
//  • FrozenDrawings: bản vẽ đã sửa trong AutoCAD rồi nạp lại – thay cho bản vẽ tự sinh tới khi "Bỏ khoá".
// Bộ máy áp dụng: Drawing/DrawingOverlay.cs.
// =====================================================================

/// <summary>Loại chỉnh sửa.</summary>
public enum DrawingEditOp
{
    /// <summary>Dời (Dx, Dy theo đơn vị của hình chủ – mm mô hình ở tỷ lệ của hình).</summary>
    Move,
    /// <summary>Thay nội dung chữ (Text).</summary>
    EditText,
    /// <summary>Xoá thực thể / nhóm.</summary>
    Delete,
    /// <summary>Đổi lớp (Layer) và/hoặc màu ACI (Color, 0 = theo lớp, −1 = giữ).</summary>
    Restyle
}

/// <summary>Một chỉnh sửa trên bản vẽ tự sinh.</summary>
public class DrawingEdit
{
    /// <summary>Khoá hình/bản vẽ chủ của thực thể: "BEAM:D2-1", "SHEET:KC-03", "DS:KC-06_ThongKeThep"…</summary>
    public string Owner { get; set; } = "";
    /// <summary>Khoá thực thể ("TEXT|CHU|1200|350|…") hoặc nhóm ("G:TAG|3Ø16|2|…").</summary>
    public string Target { get; set; } = "";
    public DrawingEditOp Op { get; set; }
    public double Dx { get; set; }
    public double Dy { get; set; }
    /// <summary>Nội dung chữ mới (EditText).</summary>
    public string Text { get; set; }
    /// <summary>Nội dung chữ lúc sửa (để mô tả).</summary>
    public string OldText { get; set; }
    /// <summary>Lớp mới (Restyle; rỗng = giữ).</summary>
    public string Layer { get; set; }
    /// <summary>Màu ACI mới (Restyle; −1 = giữ, 0 = theo lớp).</summary>
    public int Color { get; set; } = -1;
    /// <summary>Khoá bản vẽ nơi tạo chỉnh sửa (để liệt kê / khôi phục theo bản vẽ).</summary>
    public string Drawing { get; set; } = "";
    public DateTime Created { get; set; }

    public bool IsGroup => (Target ?? "").StartsWith("G:", StringComparison.Ordinal);

    public string Describe()
    {
        string tgt = Target ?? "";
        if (tgt.StartsWith("G:", StringComparison.Ordinal)) tgt = "nhóm " + tgt.Substring(2);
        if (tgt.Length > 70) tgt = tgt.Substring(0, 67) + "…";
        string inv(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        string what = Op switch
        {
            DrawingEditOp.Move => $"dời ({inv(Dx)}; {inv(Dy)})",
            DrawingEditOp.EditText => $"sửa chữ \"{OldText}\" → \"{Text}\"",
            DrawingEditOp.Delete => "xoá",
            DrawingEditOp.Restyle => $"đổi lớp/màu ({Layer}{(Color >= 0 ? "; màu " + Color : "")})",
            _ => Op.ToString()
        };
        return $"{Owner}: {what} – {tgt}";
    }
}

/// <summary>Đối tượng thêm vào bản vẽ tự sinh (chữ, đường, mũi tên, khung, đám mây…). Toạ độ Item theo mm giấy của bản vẽ chủ
/// (= toạ độ mô hình / tỷ lệ), nên chữ cao 3,5 mm in ra đúng 3,5 mm.</summary>
public class DrawingAnnotation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 12);
    public string Owner { get; set; } = "";
    public SheetItem Item { get; set; } = new();
    public DateTime Created { get; set; }
}

/// <summary>Bản vẽ đã khoá: file DXF/DWG người dùng sửa trong CAD rồi nạp lại.</summary>
public class FrozenDrawing
{
    /// <summary>Khoá bản vẽ ("DS:KC-04_Dam_Tang_2", "SHEET:KC-03", "SHOP:KC-07.1_GiaCongThep_Dam").</summary>
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>Tên file gốc khi nạp.</summary>
    public string FileName { get; set; } = "";
    /// <summary>Đường dẫn file lúc nạp (tham khảo).</summary>
    public string SourcePath { get; set; } = "";
    /// <summary>"dxf" hoặc "dwg".</summary>
    public string Format { get; set; } = "dxf";
    /// <summary>Nội dung file gốc (base64) – xuất "tách rời" ghi lại đúng file này.</summary>
    public string Data { get; set; } = "";
    /// <summary>Nội dung DXF ASCII để hiển thị/ghép (DWG đã chuyển sang DXF); rỗng = giải mã từ Data.</summary>
    public string DxfText { get; set; } = "";
    /// <summary>SHA-256 nội dung file gốc.</summary>
    public string Hash { get; set; } = "";
    /// <summary>Mã băm bản vẽ tự sinh lúc khoá (để nhận biết mô hình đã thay đổi sau khi khoá).</summary>
    public string BaseHash { get; set; } = "";
    /// <summary>Mẫu số tỷ lệ của bản vẽ (1 = mm giấy).</summary>
    public double Scale { get; set; } = 1;
    public DateTime FrozenAt { get; set; }
}

public partial class Project
{
    /// <summary>Chỉnh sửa phủ lên bản vẽ tự sinh (Drawing/DrawingOverlay.cs).</summary>
    public List<DrawingEdit> DrawingEdits { get; set; } = new();
    /// <summary>Đối tượng thêm vào bản vẽ tự sinh.</summary>
    public List<DrawingAnnotation> DrawingNotes { get; set; } = new();
    /// <summary>Bản vẽ đã khoá (sửa trong CAD rồi nạp lại).</summary>
    public List<FrozenDrawing> FrozenDrawings { get; set; } = new();
}
