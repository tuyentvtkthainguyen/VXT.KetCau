using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

// =====================================================================
// THỂ HIỆN SÀN HẠ CỐT trên bản vẽ (Slab.LevelOffset – Model/SlabDrop.cs):
//  • Mặt bằng kết cấu (FramingPlan): đường bao nét đứt lùi vào trong mép dầm + gạch chéo 45° (nét đơn, cắt theo đường bao, chừa
//    chỗ chữ) + chữ "SÀN HẠ COS -50" + ký hiệu cao độ ▽ -0.050 (so với sàn tầng); chú thích ở góc bản vẽ.
//    Gạch chéo vẽ bằng LINE – một chỗ duy nhất (Mark) để sau đổi sang HATCH thật của DxfWriter.
//  • Mặt bằng thép sàn (SlabDrawing): chữ "SÀN HẠ COS -50" dưới tên ô.
//  • Mặt cắt dầm (BeamDrawing): kích thước bậc "Δ=50" sát mặt dầm phía ô hạ + ghi chú "Sàn hạ cốt Δ = 50"; mặt đứng dầm: ghi chú.
// =====================================================================
public static class SlabDropDrawing
{
    /// <summary>Chữ đánh dấu ô hạ cốt.</summary>
    public const string Tag = "SÀN HẠ COS";

    public static string TagText(Slab s) => Tag + " " + SlabDrop.MmText(s.LevelOffset);

    /// <summary>Đỉnh ô sàn (mm).</summary>
    private static List<(double X, double Y)> Poly(Project p, Slab s)
    {
        var res = new List<(double X, double Y)>();
        foreach (int id in s.Nodes)
        {
            var n = p.Nodes.FirstOrDefault(q => q.Id == id);
            if (n != null) res.Add((n.X * 1000, n.Y * 1000));
        }
        return res;
    }

    private static bool IsAxisRect(List<(double X, double Y)> pts) =>
        pts.Count == 4 && Enumerable.Range(0, 4).All(i =>
        {
            var a = pts[i]; var b = pts[(i + 1) % 4];
            return Math.Abs(a.X - b.X) < 0.5 || Math.Abs(a.Y - b.Y) < 0.5;
        });

    /// <summary>Đánh dấu một ô sàn hạ cốt trên mặt bằng kết cấu: đường bao nét đứt, gạch chéo, chữ "SÀN HẠ COS -50", ▽ -0.050.
    /// Chữ đặt dưới tên ô + chiều dày (FramingPlan: tên ô tại tâm, "h=100" ngay dưới). Không làm gì nếu ô không hạ cốt.</summary>
    public static void Mark(DxfWriter w, Project p, Slab s)
    {
        if (w == null || p == null || !SlabDrop.IsLowered(s)) return;
        var pts = Poly(p, s);
        if (pts.Count < 3) return;
        double th = w.Th;
        double cx = pts.Average(q => q.X), cy = pts.Average(q => q.Y);
        // đường bao lùi vào trong mép dầm (ô chữ nhật); đa giác khác: theo đỉnh ô
        var outline = pts;
        if (IsAxisRect(pts))
        {
            double x1 = pts.Min(q => q.X), x2 = pts.Max(q => q.X), y1 = pts.Min(q => q.Y), y2 = pts.Max(q => q.Y);
            double ins = Math.Min(Inset(p, s), 0.2 * Math.Min(x2 - x1, y2 - y1));
            outline = new List<(double X, double Y)> { (x1 + ins, y1 + ins), (x2 - ins, y1 + ins), (x2 - ins, y2 - ins), (x1 + ins, y2 - ins) };
        }
        w.Polyline(Lay.KyHieu, outline, true, 0, "DASHED");
        // chữ + ký hiệu cao độ
        string t1 = TagText(s), t2 = SlabDrop.LevelText(s.LevelOffset);
        double h1 = 0.8 * th;
        double yT = cy - 2.8 * th;                 // dưới "h=100" (FramingPlan: cy − 1,6·th, cao 0,8·th)
        w.Text(Lay.Chu, cx, yT, h1, t1, 0, 1, 3);
        double yM = yT - 1.5 * th, a = 0.45 * th;
        double tw2 = DxfWriter.TextWidth(t2, h1) * w.WidthFactor;
        double xm = cx - (tw2 + 2.2 * a) / 2 + a;  // tam giác + chữ canh giữa ô
        LevelMark(w, xm, yM, a, t2, h1);
        double tw1 = DxfWriter.TextWidth(t1, h1) * w.WidthFactor;
        double halfW = Math.Max(Math.Max(tw1, tw2 + 2.2 * a) / 2, DxfWriter.TextWidth(s.Label ?? "", 1.2 * th) / 2) + 0.8 * th;
        var keep = new Rect2(cx - halfW, yM - 0.6 * th, cx + halfW, cy + 2.2 * th);
        Hatch(w, outline, Math.Max(1.2 * th, 150), keep);
    }

    /// <summary>Lùi đường bao vào trong: nửa bề rộng dầm lớn nhất quanh ô (mặc định 110 mm).</summary>
    private static double Inset(Project p, Slab s)
    {
        double b = 0;
        var ids = new HashSet<int>(s.Nodes);
        foreach (var f in p.Frames.Where(f => f.Kind == FrameKind.Beam && f.Story == s.Story && (ids.Contains(f.NodeI) || ids.Contains(f.NodeJ))))
        {
            var sec = p.FrameSections.FirstOrDefault(x => x.Name == f.Section);
            if (sec != null && sec.B > 0) b = Math.Max(b, sec.B);
        }
        return (b > 0 ? b / 2 * 1000 : 110) + 40;
    }

    /// <summary>Ký hiệu cao độ: tam giác ngược đỉnh tại (x, y) + đường gạch ngang + chữ cao độ bên phải.</summary>
    public static void LevelMark(DxfWriter w, double x, double y, double a, string text, double h)
    {
        w.Polyline(Lay.KyHieu, new List<(double X, double Y)> { (x - a, y + a), (x + a, y + a), (x, y) }, true);
        w.Line(Lay.KyHieu, x - 1.2 * a, y, x + 1.2 * a + DxfWriter.TextWidth(text, h) * w.WidthFactor, y);
        w.Text(Lay.Chu, x + 1.2 * a, y + 0.15 * h, h, text, 0, 0, 0);
    }

    /// <summary>Gạch chéo 45° (đường x − y = c) trong đa giác, bỏ phần nằm trong keepOut. Thay bằng HATCH thật khi DxfWriter hỗ trợ.</summary>
    public static void Hatch(DxfWriter w, List<(double X, double Y)> poly, double spacing, Rect2? keepOut = null)
    {
        if (poly == null || poly.Count < 3 || spacing <= 0) return;
        var f = poly.Select(q => q.X - q.Y).ToList();
        double cMin = f.Min(), cMax = f.Max();
        double step = spacing * Math.Sqrt(2);      // khoảng cách vuông góc giữa hai đường = spacing
        int n = (int)Math.Floor((cMax - cMin) / step);
        if (n > 400) { step = (cMax - cMin) / 400; n = 400; }
        double c0 = cMin + ((cMax - cMin) - n * step) / 2;
        for (int k = 0; k <= n; k++)
        {
            double c = c0 + k * step;
            var xs = new List<(double X, double Y)>();
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                double fa = f[i], fb = f[(i + 1) % poly.Count];
                if ((fa <= c && fb > c) || (fb <= c && fa > c))
                {
                    double t = (c - fa) / (fb - fa);
                    xs.Add((a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
                }
            }
            xs = xs.OrderBy(q => q.X + q.Y).ToList();
            for (int i = 0; i + 1 < xs.Count; i += 2)
                foreach (var (P, Q) in Cut(xs[i], xs[i + 1], keepOut))
                    if (Math.Abs(Q.X - P.X) + Math.Abs(Q.Y - P.Y) > 1) w.Line(Lay.KyHieu, P.X, P.Y, Q.X, Q.Y);
        }
    }

    /// <summary>Đoạn P→Q trừ phần nằm trong hình chữ nhật r.</summary>
    private static List<((double X, double Y) P, (double X, double Y) Q)> Cut((double X, double Y) p, (double X, double Y) q, Rect2? r)
    {
        var res = new List<((double X, double Y) P, (double X, double Y) Q)>();
        if (r == null || r.Value.IsEmpty) { res.Add((p, q)); return res; }
        var R = r.Value;
        double dx = q.X - p.X, dy = q.Y - p.Y, t0 = 0, t1 = 1;
        bool ok = true;
        foreach (var (den, num) in new[] { (-dx, p.X - R.X1), (dx, R.X2 - p.X), (-dy, p.Y - R.Y1), (dy, R.Y2 - p.Y) })
        {
            if (Math.Abs(den) < 1e-12) { if (num < 0) { ok = false; break; } continue; }
            double t = num / den;
            if (den < 0) { if (t > t1) { ok = false; break; } if (t > t0) t0 = t; }
            else { if (t < t0) { ok = false; break; } if (t < t1) t1 = t; }
        }
        if (!ok || t1 <= t0) { res.Add((p, q)); return res; }
        if (t0 > 1e-9) res.Add((p, (p.X + dx * t0, p.Y + dy * t0)));
        if (t1 < 1 - 1e-9) res.Add(((p.X + dx * t1, p.Y + dy * t1), q));
        return res;
    }

    /// <summary>Chú thích ký hiệu sàn hạ cốt ở dưới bản vẽ (gọi trước khi ghi tên hình). Không có ô hạ cốt → không vẽ.</summary>
    public static void Legend(DxfWriter w, Project p, Story st)
    {
        if (w == null || p == null || st == null) return;
        var low = SlabDrop.Lowered(p, st.Name);
        if (low.Count == 0) return;
        double th = w.Th;
        var ext = w.Extents;
        if (ext.IsEmpty) return;
        double x0 = ext.X1, y0 = ext.Y1 - 2.5 * th, a = 2.4 * th;
        var sq = new List<(double X, double Y)> { (x0, y0 - a), (x0 + a, y0 - a), (x0 + a, y0), (x0, y0) };
        w.Polyline(Lay.KyHieu, sq, true, 0, "DASHED");
        Hatch(w, sq, a / 4);
        var vals = low.Select(s => SlabDrop.MmText(s.LevelOffset)).Distinct().OrderBy(x => x).ToList();
        w.Text(Lay.Chu, x0 + a + 0.8 * th, y0 - 0.9 * th, th,
            $"{Tag}: mặt sàn thấp hơn cao độ sàn tầng {DxfWriter.ElevText(st.Elevation)} một khoảng Δ (mm) = {string.Join(", ", vals)} – {low.Count} ô", 0, 0, 0);
        w.Text(Lay.Chu, x0 + a + 0.8 * th, y0 - 2.3 * th, 0.8 * th,
            "Cạnh giáp ô chênh cốt: thép mũ tách riêng từng ô, neo vào dầm; dầm biên ô hạ cốt h ≥ hs1 + Δ + hs2", 0, 0, 0);
    }

    /// <summary>Mặt bằng thép sàn: chữ "SÀN HẠ COS -50" dưới tên ô (trả vùng chữ để giữ chỗ nhãn thép). Ô không hạ → rỗng.</summary>
    public static Rect2 Note(DxfWriter w, Project p, Slab s)
    {
        if (w == null || p == null || !SlabDrop.IsLowered(s)) return Rect2.Empty;
        var pts = Poly(p, s);
        if (pts.Count < 3) return Rect2.Empty;
        double th = w.Th, cx = (pts.Min(q => q.X) + pts.Max(q => q.X)) / 2, cy = (pts.Min(q => q.Y) + pts.Max(q => q.Y)) / 2;
        return w.Text(Lay.Chu, cx, cy - 0.9 * th, 0.8 * th, TagText(s) + " (▽ " + SlabDrop.LevelText(s.LevelOffset) + ")", 0, 1, 3);
    }

    /// <summary>Mặt cắt dầm: kích thước bậc "Δ=50" sát mặt dầm phía cánh hạ (B, H, k: bề rộng, cao (đã phóng), hệ số phóng; dz mm âm)
    /// và ghi chú "Sàn hạ cốt Δ = 50" (đặt tránh nhãn qua layout).</summary>
    public static void SectionNote(DxfWriter w, TagLayout layout, double B, double H, double k, double? dzL, double? dzR)
    {
        if (w == null) return;
        double th = w.Th;
        var notes = new List<string>();
        void Side(double? dz, int sg)
        {
            if (dz == null || dz.Value > -0.5) return;
            double x = sg < 0 ? 0 : B;
            string t = "Δ=" + DxfWriter.N0(-dz.Value);
            w.Dim(x, H + dz.Value * k, x, H, sg < 0 ? 0.7 * th : -0.7 * th, k, t);
            string n = "Sàn hạ cốt Δ = " + DxfWriter.N0(-dz.Value);
            if (!notes.Contains(n)) notes.Add(n);
        }
        Side(dzL, -1);
        Side(dzR, 1);
        if (notes.Count == 0) return;
        string txt = string.Join("; ", notes);
        double y = H + 4.2 * th;
        var box = w.TextBox(B / 2, y, 0.8 * th, txt, 0, 1, 0);
        var (_, dy) = layout != null ? layout.Place(box, 0, 1.2 * th) : (0.0, 0.0);
        w.Text(Lay.Chu, B / 2, y + dy, 0.8 * th, txt, 0, 1, 0);
    }

    /// <summary>Mặt đứng dầm: ghi chú hạ cốt cho một nhịp (null nếu không có cánh hạ).</summary>
    public static string ElevationNote(BeamSlabSection sc)
    {
        if (sc == null) return null;
        var parts = new List<string>();
        if (sc.Left != null && sc.Left.Dz < -0.5) parts.Add("bên trái Δ = " + DxfWriter.N0(-sc.Left.Dz));
        if (sc.Right != null && sc.Right.Dz < -0.5) parts.Add("bên phải Δ = " + DxfWriter.N0(-sc.Right.Dz));
        return parts.Count == 0 ? null : "Sàn hạ cốt " + string.Join(", ", parts);
    }
}
