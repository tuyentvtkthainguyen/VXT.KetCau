using System.Globalization;
using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>[LùiCọcRanh] Thể hiện khoảng lùi ép cọc: trên mặt bằng móng – đường giới hạn tim cọc (nét chấm gạch) song song ranh giới cách s,
/// ghi "≥ 500 (lùi ép cọc)"; trên chi tiết đài – đường ranh giới + kích thước ranh giới → tim cọc gần nhất.</summary>
public static class PileSetbackDrawing
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static string Tag(double s) => "≥ " + (s * 1000).ToString("0", Inv) + " (lùi ép cọc)";

    /// <summary>Mặt bằng móng (toạ độ thật × k): đường giới hạn tim cọc cho mỗi cạnh ranh giới có khoảng lùi.</summary>
    public static void AddToLayout(DxfWriter w, Project p, DesignSummary s, double k, ref Rect2 box)
    {
        if (w == null || p == null) return;
        var lines = PileSetback.Lines(p);
        if (lines.Count == 0 || box.IsEmpty) return;
        double th = w.Th;
        var b0 = box;
        bool face = p.Foundation?.PileSetbackToFace == true;
        foreach (var (b, c, sb) in lines)
        {
            double v = (c + b.Inward * sb) * 1000 * k, vb = c * 1000 * k;
            if (b.IsVertical)
            {
                w.Line(Lay.KyHieu, v, b0.Y1, v, b0.Y2, "CENTER");
                double ym = b0.Y1 + 0.25 * (b0.Y2 - b0.Y1);
                w.Dim(Math.Min(vb, v), ym, Math.Max(vb, v), ym, 0, k, Tag(sb));
                w.Text(Lay.Chu, v + b.Inward * 0.4 * th, b0.Y2 - 2 * th, 0.75 * th, face ? "GIỚI HẠN MÉP CỌC" : "GIỚI HẠN TIM CỌC", 90, 1, b.Inward > 0 ? 3 : 1);
            }
            else
            {
                w.Line(Lay.KyHieu, b0.X1, v, b0.X2, v, "CENTER");
                double xm = b0.X1 + 0.25 * (b0.X2 - b0.X1);
                w.Dim(xm, Math.Max(vb, v), xm, Math.Min(vb, v), 0, k, Tag(sb));
                w.Text(Lay.Chu, b0.X2 - 2 * th, v + b.Inward * 0.4 * th, 0.75 * th, face ? "GIỚI HẠN MÉP CỌC" : "GIỚI HẠN TIM CỌC", 0, 2, b.Inward > 0 ? 1 : 3);
            }
        }
    }

    /// <summary>Chi tiết đài (CapPlan): ranh giới + kích thước "≥ 500 (lùi ép cọc)" từ ranh giới tới tim cọc gần nhất. (xc, yc): tim cột trên hình; Bh: bề rộng đài trên hình.</summary>
    public static void OnCapPlan(DxfWriter w, PileCapDesign c, double xc, double yc, double k, double Lh, double Bh)
    {
        var info = c?.Setback;
        if (w == null || info == null || info.Sides.Count == 0 || c.Piles.Count == 0) return;
        double th = w.Th, mm = 1000 * k;
        foreach (var sd in info.Sides)
        {
            if (sd.IsVertical)
            {
                double xb = xc + (sd.Boundary - c.X) * mm;
                double px = xc + (sd.Inward > 0 ? c.Piles.Min(q => q.X) : c.Piles.Max(q => q.X)) * mm;
                w.Line(Lay.KyHieu, xb, -4 * th, xb, Bh + 4 * th, "DASHED");
                for (double y = -4 * th; y <= Bh + 3.2 * th; y += 1.6 * th) w.Line(Lay.KyHieu, xb, y, xb - sd.Inward * 0.8 * th, y + 0.8 * th);
                double yd = Bh + 2.2 * th;
                w.Dim(Math.Min(xb, px), yd, Math.Max(xb, px), yd, 0, k, Tag(sd.Setback));
                w.Text(Lay.Chu, xb - sd.Inward * 1.2 * th, Bh / 2, 0.8 * th, EccentricDrawing.BoundaryLabel, 90, 1, 0);
            }
            else
            {
                double yb = yc + (sd.Boundary - c.Y) * mm;
                double py = yc + (sd.Inward > 0 ? c.Piles.Min(q => q.Y) : c.Piles.Max(q => q.Y)) * mm;
                w.Line(Lay.KyHieu, -4 * th, yb, Lh + 4 * th, yb, "DASHED");
                for (double x = -4 * th; x <= Lh + 3.2 * th; x += 1.6 * th) w.Line(Lay.KyHieu, x, yb, x + 0.8 * th, yb - sd.Inward * 0.8 * th);
                double xd = Lh + 2.2 * th;
                w.Dim(xd, Math.Max(yb, py), xd, Math.Min(yb, py), 0, k, Tag(sd.Setback));
                w.Text(Lay.Chu, Lh / 2, yb - sd.Inward * 1.2 * th, 0.8 * th, EccentricDrawing.BoundaryLabel, 0, 1, 0);
            }
        }
        if (info.Shifted)
            w.Text(Lay.Chu, Lh / 2, -12 * th, 0.75 * th, $"Cọc lùi khỏi ranh giới để ép cọc – nhóm cọc lệch tâm e = {(Math.Max(Math.Abs(info.GroupEx), Math.Abs(info.GroupEy)) * 1000).ToString("0", Inv)} mm: {info.Recommended}", 0, 1, 0);
    }
}
