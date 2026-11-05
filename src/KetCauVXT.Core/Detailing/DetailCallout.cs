using System.Text.Json.Serialization;
using KetCauVXT.Design;
using KetCauVXT.Drawing;

namespace KetCauVXT.Detailing;

// =====================================================================
// TRÍCH CHI TIẾT (khoanh vùng / "scan") TRÊN MẶT BẰNG KẾT CẤU + VỊ TRÍ MẶT CẮT DẦM TRÊN MẶT BẰNG
//  • Người dùng khoanh một cửa sổ trên mặt bằng (tab Mặt bằng kết cấu → công cụ "Trích chi tiết") → DetailCallout.
//  • Trên mặt bằng: khung nét đứt quanh vùng + vòng ký hiệu "A" (tên chi tiết) và chữ "CT-A".
//  • Hình mới "CHI TIẾT A (tl 1/25)": vẽ lại mặt bằng tầng ở tỷ lệ chi tiết (chữ, đầu trục, vòng số hiệu đúng cỡ in nhờ
//    DxfWriter.OverrideScales) rồi cắt theo cửa sổ (DxfWriter.AppendScaledClipped) – dầm, cột, ô sàn, thép sàn, kích thước trong vùng.
//  • Vị trí mặt cắt dầm trên mặt bằng (tuỳ chọn ShowBeamCuts): vạch cắt ngắn vuông góc tim dầm tại các mặt cắt của chi tiết dầm
//    (gối – giữa nhịp – gối, số hiệu như mặt đứng dầm – BeamDrawing.SectionCuts).
// =====================================================================

/// <summary>Một chi tiết trích từ mặt bằng (cửa sổ toạ độ m trên mặt bằng tầng Level).</summary>
public class DetailCallout
{
    /// <summary>Tên chi tiết: "A", "B"… (hình "CHI TIẾT A").</summary>
    public string Name { get; set; } = "A";
    /// <summary>Tên mặt bằng (PlanLevel.Name).</summary>
    public string Level { get; set; } = "";
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    /// <summary>Mẫu số tỷ lệ chi tiết (10…50, mặc định 25).</summary>
    public double Scale { get; set; } = 25;
    /// <summary>Ghi chú thêm dưới tên chi tiết (tuỳ chọn).</summary>
    public string Note { get; set; } = "";

    [JsonIgnore] public double MinX => Math.Min(X1, X2);
    [JsonIgnore] public double MaxX => Math.Max(X1, X2);
    [JsonIgnore] public double MinY => Math.Min(Y1, Y2);
    [JsonIgnore] public double MaxY => Math.Max(Y1, Y2);
    [JsonIgnore] public bool IsValid => MaxX - MinX > 0.05 && MaxY - MinY > 0.05;
    /// <summary>Ký hiệu ghi trên mặt bằng: "CT-A".</summary>
    [JsonIgnore] public string Tag => "CT-" + Name;
    /// <summary>Tiêu đề hình: "CHI TIẾT A".</summary>
    [JsonIgnore] public string Title => "CHI TIẾT " + Name;
}

public partial class StructuralPlan
{
    /// <summary>Các chi tiết trích (khoanh vùng) trên mặt bằng.</summary>
    public List<DetailCallout> Callouts { get; set; } = new();
    /// <summary>Thể hiện vị trí mặt cắt dầm (vạch cắt + số hiệu) trên mặt bằng kết cấu tầng.</summary>
    public bool ShowBeamCuts { get; set; } = true;

    /// <summary>Tên chi tiết kế tiếp chưa dùng: A, B, … Z, A1…</summary>
    public string NextCalloutName()
    {
        var used = new HashSet<string>((Callouts ?? new()).Select(c => c.Name ?? ""));
        for (int i = 0; ; i++)
        {
            string n = i < 26 ? ((char)('A' + i)).ToString() : ((char)('A' + i % 26)).ToString() + (i / 26).ToString(Dt.Inv);
            if (!used.Contains(n)) return n;
        }
    }

    /// <summary>Thêm chi tiết trích (cửa sổ 2 góc, m). Trả về chi tiết (null nếu cửa sổ quá nhỏ).</summary>
    public DetailCallout AddCallout(PlanLevel lv, double x1, double y1, double x2, double y2, double scale = 25, string name = null)
    {
        var c = new DetailCallout
        {
            Name = string.IsNullOrWhiteSpace(name) ? NextCalloutName() : name.Trim(), Level = lv?.Name ?? "",
            X1 = Math.Round(Math.Min(x1, x2), 3), Y1 = Math.Round(Math.Min(y1, y2), 3), X2 = Math.Round(Math.Max(x1, x2), 3), Y2 = Math.Round(Math.Max(y1, y2), 3),
            Scale = CalloutDrawing.SnapScale(scale)
        };
        if (!c.IsValid) return null;
        (Callouts ??= new()).Add(c);
        return c;
    }

    /// <summary>Chi tiết trích chứa điểm (x, y) trên mặt bằng lv (gần cạnh khung ±tol hoặc gần vòng ký hiệu).</summary>
    public DetailCallout HitCallout(PlanLevel lv, double x, double y, double tol)
    {
        foreach (var c in (Callouts ?? new()).Where(q => q.Level == lv?.Name))
        {
            bool inX = x >= c.MinX - tol && x <= c.MaxX + tol, inY = y >= c.MinY - tol && y <= c.MaxY + tol;
            bool nearEdge = inX && inY && (Math.Abs(x - c.MinX) < tol || Math.Abs(x - c.MaxX) < tol || Math.Abs(y - c.MinY) < tol || Math.Abs(y - c.MaxY) < tol);
            if (nearEdge) return c;
        }
        return null;
    }
}

/// <summary>Vẽ chi tiết trích và ký hiệu trên mặt bằng.</summary>
public static class CalloutDrawing
{
    public static readonly double[] Scales = { 10, 15, 20, 25, 30, 50 };

    /// <summary>Tỷ lệ chi tiết chuẩn gần nhất (10 / 15 / 20 / 25 / 30 / 50).</summary>
    public static double SnapScale(double s)
    {
        if (double.IsNaN(s) || s <= 0) return 25;
        return Scales.OrderBy(v => Math.Abs(v - s)).First();
    }

    /// <summary>Khung nét đứt + vòng ký hiệu chi tiết trên mặt bằng (toạ độ mm); vạch cắt dầm nếu bật ShowBeamCuts.</summary>
    public static void DrawOnPlan(DxfWriter w, StructuralPlan plan, PlanLevel lv, Dictionary<string, (IDetailMember M, DetailResult R)> map)
    {
        if (w == null || plan == null || lv == null) return;
        double th = w.Th;
        foreach (var c in (plan.Callouts ?? new()).Where(q => q.Level == lv.Name && q.IsValid))
        {
            double x0 = c.MinX * 1000, y0 = c.MinY * 1000, x1 = c.MaxX * 1000, y1 = c.MaxY * 1000;
            w.Rect(Lay.KyHieu, x0, y0, x1, y1, "DASHED");
            // vòng ký hiệu ở góc trên phải, nối với khung bằng đường dẫn ngắn
            double r = 1.1 * th, bx = x1 + 1.6 * th, by = y1 + 1.6 * th;
            w.Line(Lay.KyHieu, x1, y1, bx - r * 0.7071, by - r * 0.7071);
            w.Circle(Lay.KyHieu, bx, by, r);
            w.Text(Lay.Chu, bx, by, 1.0 * th, c.Name, 0, 1, 2);
            w.Text(Lay.Chu, bx + r + 0.4 * th, by - 0.5 * th, 0.8 * th, c.Tag, 0, 0, 0);
        }
        if (plan.ShowBeamCuts && map != null) DrawBeamCuts(w, lv, map);
    }

    /// <summary>Vạch cắt mặt cắt dầm trên mặt bằng: tại mỗi mặt cắt của chi tiết dầm (BeamDrawing.SectionCuts) hai vạch đậm ngắn
    /// hai bên dầm, số hiệu mặt cắt cạnh vạch (nhỏ). Toạ độ dọc dầm co giãn theo chiều dài vị trí / tổng nhịp chi tiết.</summary>
    public static int DrawBeamCuts(DxfWriter w, PlanLevel lv, Dictionary<string, (IDetailMember M, DetailResult R)> map)
    {
        int n = 0;
        double th = w.Th;
        var done = new HashSet<string>();
        foreach (var it in lv.Items.Where(i => i.Kind == PlanKinds.Beam && !string.IsNullOrEmpty(i.Member)))
        {
            if (!map.TryGetValue(it.Member, out var v) || v.R?.Source is not BeamLineDesign d || d.Spans.Count == 0) continue;
            double len = it.Length * 1000, total = d.Spans.Sum(sp => sp.L) * 1000;
            if (len < 1 || total < 1) continue;
            double ux = (it.X2 - it.X1) * 1000 / len, uy = (it.Y2 - it.Y1) * 1000 / len, nx = -uy, ny = ux;
            double half = (v.M is ManualBeam mb ? mb.B : d.B) * 500;
            double k = len / total;
            // mỗi số hiệu mặt cắt chỉ ghi một lần trên cùng một vị trí dầm (giảm rối mặt bằng)
            foreach (var (x, num) in BeamDrawing.SectionCuts(d))
            {
                string key = it.Member + "|" + Math.Round(it.X1, 2) + "|" + Math.Round(it.Y1, 2) + "|" + num;
                if (!done.Add(key)) continue;
                double px = it.X1 * 1000 + ux * x * k, py = it.Y1 * 1000 + uy * x * k;
                foreach (int sgn in new[] { 1, -1 })
                {
                    double a = half + 0.15 * th, b = half + 0.9 * th;
                    w.Polyline(Lay.KyHieu, new List<(double X, double Y)> { (px + sgn * nx * a, py + sgn * ny * a), (px + sgn * nx * b, py + sgn * ny * b) }, false, 0.15 * th);
                }
                double tx = px + nx * (half + 1.5 * th), ty = py + ny * (half + 1.5 * th);
                w.Text(Lay.Chu, tx, ty, 0.7 * th, num.ToString(Dt.Inv), 0, 1, 2);
                n++;
            }
        }
        return n;
    }

    /// <summary>Hình các chi tiết trích của một mặt bằng: "CHI TIẾT A" (tl 1/25…) – mặt bằng vẽ lại ở tỷ lệ chi tiết, cắt theo cửa sổ.</summary>
    public static List<Figure> Figures(StructuralPlan plan, PlanLevel lv, Dictionary<string, (IDetailMember M, DetailResult R)> map, DetailSettings st)
    {
        var figs = new List<Figure>();
        if (plan == null || lv == null) return figs;
        foreach (var c in (plan.Callouts ?? new()).Where(q => q.Level == lv.Name && q.IsValid))
        {
            try { figs.Add(new Figure(Draw(plan, lv, c, map), c.Title, false)); }
            catch (Exception ex) { figs.Add(new Figure(DrawUtil.ErrorFigure(c.Title, ex, c.Scale), c.Title, false)); }
        }
        return figs;
    }

    /// <summary>Một chi tiết trích: vẽ lại mặt bằng tầng với tỷ lệ chi tiết rồi cắt theo cửa sổ (mm), viền cắt nét lượn sóng thẳng (nét mảnh),
    /// tên "CHI TIẾT A" + "tl: 1/25" bên dưới.</summary>
    public static DxfWriter Draw(StructuralPlan plan, PlanLevel lv, DetailCallout c, Dictionary<string, (IDetailMember M, DetailResult R)> map)
    {
        double sc = SnapScale(c.Scale);
        DxfWriter full;
        bool cuts = plan.ShowBeamCuts;
        var callouts = plan.Callouts;
        try
        {
            // tắt ký hiệu chi tiết trích & vạch cắt dầm khi vẽ lại (tránh lồng ký hiệu vào chi tiết)
            plan.ShowBeamCuts = false; plan.Callouts = new List<DetailCallout>();
            double baseScale = StructuralPlanDrawing.Scale;
            using (DxfWriter.OverrideScales(x => Math.Abs(x - baseScale) < 1e-6 ? sc : x))
                full = StructuralPlanDrawing.Level(plan, lv, map, "", false);
        }
        finally { plan.ShowBeamCuts = cuts; plan.Callouts = callouts; }
        var w = new DxfWriter(sc);
        double th = w.Th;
        var clip = new Rect2(c.MinX * 1000, c.MinY * 1000, c.MaxX * 1000, c.MaxY * 1000);
        w.AppendScaledClipped(full, 1, 0, 0, clip);
        w.Rect(Lay.KyHieu, clip.X1, clip.Y1, clip.X2, clip.Y2);
        // kích thước cửa sổ chi tiết
        w.Dim(clip.X1, clip.Y1, clip.X2, clip.Y1, -2.5 * th);
        w.Dim(clip.X1, clip.Y1, clip.X1, clip.Y2, 2.5 * th);
        double y = w.Title((clip.X1 + clip.X2) / 2, clip.Y1 - 5.5 * th, c.Title, DrawUtil.ScaleText(sc));
        if (!string.IsNullOrWhiteSpace(c.Note)) w.Text(Lay.Chu, (clip.X1 + clip.X2) / 2, y - 0.5 * th, 0.85 * th, c.Note.Trim(), 0, 1, 3);
        w.Text(Lay.Chu, clip.X1, clip.Y2 + 0.6 * th, 0.8 * th, "(trích từ " + StructuralPlanDrawing.LevelTitle(lv).ToLowerInvariant() + ")", 0, 0, 0);
        return w;
    }
}
