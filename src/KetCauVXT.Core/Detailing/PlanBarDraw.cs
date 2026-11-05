using KetCauVXT.Drawing;

namespace KetCauVXT.Detailing;

/// <summary>Thể hiện thép rải trên MẶT BẰNG kiểu Kết cấu Soft / hồ sơ chuyên nghiệp:
/// một thanh đại diện vẽ đúng hình dạng (móc / bẻ đầu), chữ "Ø8a150" đặt ngay trên thanh ở giữa đoạn, vòng số hiệu sát sau chữ
/// (không kéo đường dẫn dài), và ĐƯỜNG PHẠM VI RẢI vuông góc với thanh: nét mảnh có gạch xiên 45° ở hai đầu, chấm tại giao với thanh.</summary>
public static class PlanBarDraw
{
    /// <summary>Thanh thẳng theo phương nằm ngang (horizontal = true: dọc trục X) hoặc dọc (trục Y).
    /// a, b: toạ độ đầu – cuối thanh dọc theo phương thanh; pos: toạ độ vuông góc (vị trí thanh đại diện).
    /// hookA / hookB: chiều dài móc / đoạn bẻ ở đầu (vẽ vuông góc, về phía hookSide: +1 / −1), 0 = không.
    /// textAt: vị trí đặt chữ dọc thanh (0…1). range: phạm vi rải (r0 → r1) theo phương vuông góc, đặt tại rangeAt (0…1 dọc thanh); null = không vẽ.
    /// labelSide: +1 chữ phía trên / bên trái thanh, −1 phía dưới / bên phải.</summary>
    public static Rect2 Bar(DxfWriter w, bool horizontal, double a, double b, double pos, string text, int mark,
        double hookA = 0, double hookB = 0, int hookSide = 1, double textAt = 0.5, (double R0, double R1)? range = null, double rangeAt = 0.25,
        int labelSide = 1, string layer = null)
    {
        layer ??= Lay.Thep;
        double th = w.TagTh > 0 ? w.TagTh : w.Th;
        (double, double) P(double along, double across) => horizontal ? (along, across) : (across, along);
        var pts = new List<(double X, double Y)>();
        if (hookA > 1e-6) pts.Add(P(a, pos + hookSide * hookA));
        pts.Add(P(a, pos)); pts.Add(P(b, pos));
        if (hookB > 1e-6) pts.Add(P(b, pos + hookSide * hookB));
        w.Polyline(layer, pts, false, 0.35 * w.Scale);

        // phạm vi rải
        if (range is { } rg && Math.Abs(rg.R1 - rg.R0) > 1e-6)
        {
            double at = a + (b - a) * rangeAt;
            var (x0, y0) = P(at, rg.R0); var (x1, y1) = P(at, rg.R1);
            w.Line(Lay.KyHieu, x0, y0, x1, y1);
            double t = 0.45 * th;
            foreach (var (x, y) in new[] { (x0, y0), (x1, y1) })
                w.Line(Lay.KyHieu, x - t, y - t, x + t, y + t);
            var (xd, yd) = P(at, pos);
            w.Dot(Lay.KyHieu, xd, yd, 0.18 * th);
        }

        // chữ trên thanh + vòng số hiệu ngay sau chữ
        double tw = DxfWriter.TextWidth(text, th) * w.WidthFactor;
        double r = w.MarkR;
        double total = tw + (mark > 0 ? 0.9 * th + 2 * r : 0);
        double mid = a + (b - a) * textAt;
        double s0 = mid - total / 2;
        double off = labelSide * 0.35 * th;
        Rect2 box;
        if (horizontal)
        {
            double ty = pos + off + (labelSide > 0 ? 0 : -th);
            w.Text(Lay.Chu, s0, ty, th, text, 0, 0, 0);
            if (mark > 0) w.MarkCircle(s0 + tw + 0.9 * th + r, pos + labelSide * (0.35 * th + 0.5 * th), mark);
            box = new Rect2(s0, Math.Min(pos, ty), s0 + total, Math.Max(pos, ty + th));
        }
        else
        {
            // chữ xoay 90°, đọc từ dưới lên, đặt bên trái thanh (labelSide = +1) hoặc bên phải (−1)
            double tx = pos - off - (labelSide > 0 ? 0 : -th);
            w.Text(Lay.Chu, tx, s0, th, text, 90, 0, 0);
            if (mark > 0) w.MarkCircle(pos - labelSide * (0.35 * th + 0.5 * th), s0 + tw + 0.9 * th + r, mark, 90);
            box = new Rect2(Math.Min(pos, tx - th), s0, Math.Max(pos, tx), s0 + total);
        }
        return box;
    }
}
