using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

public sealed partial class DxfWriter
{
    /// <summary>Cung tròn (ARC, góc độ ngược chiều kim đồng hồ a1 → a2) với khung bao sát cung – không lấy cả đường tròn
    /// làm phạm vi (giữ đúng vị trí tên hình, khung nhìn khi dàn trang).</summary>
    public void ArcBounded(string layer, double cx, double cy, double r, double a1, double a2, string ltype = null)
    {
        double sweep = a2 - a1;
        while (sweep <= 0) sweep += 360;
        var box = Rect2.Empty;
        const int n = 24;
        for (int k = 0; k <= n; k++)
        {
            double a = (a1 + sweep * k / n) * Math.PI / 180;
            box = box.Union(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
        }
        var e = Add("ARC", layer, box, ltype);
        e.Codes.Add((10, cx)); e.Codes.Add((20, cy)); e.Codes.Add((30, 0.0)); e.Codes.Add((40, r));
        e.Codes.Add((50, a1)); e.Codes.Add((51, a2));
    }
}

/// <summary>Tiện ích vẽ dầm cong dùng chung cho các bản vẽ DXF (mặt bằng kết cấu, mặt bằng sàn, khung nhìn dàn trang).
/// Toạ độ bản vẽ = toạ độ mô hình × 1000 (mm).</summary>
public static class CurvedDraw
{
    /// <summary>Vẽ dầm cong trên mặt bằng: hai cung đồng tâm bán kính R ± b/2 (thực thể ARC) và hai nét đầu dầm.
    /// Trả về khung bao (mm).</summary>
    public static Rect2 PlanBeam(DxfWriter w, ArcGeometry a, double halfWidthMm, string layer, string ltype = null)
    {
        double cx = a.Cx * 1000, cy = a.Cy * 1000, r = a.Radius * 1000;
        var (a1, a2) = a.CcwDegrees();
        double ro = r + halfWidthMm, ri = Math.Max(r - halfWidthMm, 1);
        w.ArcBounded(layer, cx, cy, ro, a1, a2, ltype);
        w.ArcBounded(layer, cx, cy, ri, a1, a2, ltype);
        var box = Rect2.Empty;
        foreach (var q in a.Points(24))
        {
            double dx = q.X * 1000 - cx, dy = q.Y * 1000 - cy, d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-9) continue;
            box = box.Union(cx + dx / d * ro, cy + dy / d * ro).Union(cx + dx / d * ri, cy + dy / d * ri);
        }
        return box;
    }

    /// <summary>Điểm đặt và góc chữ (độ, trong (−90°, 90°]) tên dầm cong: giữa cung, lệch theo pháp tuyến một khoảng offsetMm.</summary>
    public static (double X, double Y, double AngDeg) LabelAnchor(ArcGeometry a, double offsetMm)
    {
        var m = a.Mid; var t = a.TangentAt(0.5);
        double ang = Math.Atan2(t.Y, t.X) * 180 / Math.PI;
        if (ang > 90.001) ang -= 180; else if (ang <= -89.999) ang += 180;
        double ar = ang * Math.PI / 180, nx = -Math.Sin(ar), ny = Math.Cos(ar);
        return (m.X * 1000 + nx * offsetMm, m.Y * 1000 + ny * offsetMm, ang);
    }

    /// <summary>Ghi chú dầm cong cho bản vẽ chi tiết: "Dầm cong R=…, chiều dài cung L=…" (mm).</summary>
    public static string Note(double radiusM, double arcLengthM) =>
        $"Dầm cong R={DxfWriter.N0(radiusM * 1000)}, chiều dài cung L={DxfWriter.N0(arcLengthM * 1000)} (mặt đứng thể hiện trải theo cung)";
}
