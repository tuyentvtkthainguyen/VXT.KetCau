using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Export;

/// <summary>Điểm chèn cột lệch tâm (trục qua mép cột – FrameElement.ColOffsetX/Y) khi xuất sang ETABS / SAP2000.
/// ETABS (.e2k): dùng CARDINALPT – từ khoá đã có sẵn trong LINEASSIGN, ETABS đọc chắc chắn. Quy ước cột đứng góc 0:
/// trục 2 ∥ X (cạnh H), trục 3 ∥ Y (cạnh B); điểm chèn 1 dưới-trái … 9 trên-phải, 10 trọng tâm.
/// Tâm cột lệch +H/2 theo X so với nút (trục qua mép TRÁI) → điểm chèn "trái"; lệch −H/2 → "phải"; lệch +B/2 theo Y → "dưới"; −B/2 → "trên".
/// Lệch không bằng nửa cạnh (hoặc cột xoay khác 0°/90°/180°/270°) → không biểu diễn được bằng điểm chèn: giữ 10, ghi chú "$ …" trong file.
/// SAP2000 (.s2k): bảng FRAME INSERTION POINT ASSIGNMENTS với độ dời đầu nút toàn cục (XI…ZJ) – biểu diễn được mọi độ lệch.</summary>
public static class ColumnInsertion
{
    /// <summary>(điểm chèn ETABS, biểu diễn đúng?). Cột không lệch → (10, true).</summary>
    public static (int Cardinal, bool Exact) Cardinal(Project p, FrameElement f)
    {
        if (f == null || !f.IsVerticalKind) return (10, true);
        double ox = f.ColOffsetX, oy = f.ColOffsetY;
        if (Math.Abs(ox) < 1e-4 && Math.Abs(oy) < 1e-4) return (10, true);
        var si = DrawUtil.SectionOf(p, f.Section);
        double a = ((f.Angle % 360) + 360) % 360;
        // lệch theo trục địa phương 2 / 3 của tiết diện
        double c = Math.Cos(a * Math.PI / 180), s = Math.Sin(a * Math.PI / 180);
        bool axisAligned = Math.Abs(c * s) < 1e-6;
        if (!axisAligned) return (10, false);
        double o2 = ox * c + oy * s, o3 = -ox * s + oy * c;   // toạ độ lệch theo trục 2, 3
        double h2 = si.H / 2, h3 = si.B / 2;
        int col, row;   // col: 0 trái, 1 giữa, 2 phải; row: 0 dưới, 1 giữa, 2 trên
        const double tol = 0.002;
        if (Math.Abs(o2) < tol) col = 1; else if (Math.Abs(o2 - h2) < tol) col = 0; else if (Math.Abs(o2 + h2) < tol) col = 2; else return (10, false);
        if (Math.Abs(o3) < tol) row = 1; else if (Math.Abs(o3 - h3) < tol) row = 0; else if (Math.Abs(o3 + h3) < tol) row = 2; else return (10, false);
        if (row == 1 && col == 1) return (10, true);
        return (row * 3 + col + 1, true);
    }

    /// <summary>Tên điểm chèn kiểu SAP2000 ("4 (Middle Left)").</summary>
    public static string CardinalName(int cp) => cp switch
    {
        1 => "1 (Bottom Left)", 2 => "2 (Bottom Center)", 3 => "3 (Bottom Right)", 4 => "4 (Middle Left)", 5 => "5 (Middle Center)",
        6 => "6 (Middle Right)", 7 => "7 (Top Left)", 8 => "8 (Top Center)", 9 => "9 (Top Right)", 11 => "11 (Shear Center)", _ => "10 (Centroid)"
    };
}
