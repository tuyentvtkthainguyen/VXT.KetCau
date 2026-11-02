using System.Globalization;
using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>[CừTràm] Bản vẽ gia cố nền cừ tràm: mặt bằng lưới cừ dưới móng (vòng tròn Ø gốc theo bước s = 1/√mật độ) và mặt cắt
/// (móng, bê tông lót, cát đệm, cây cừ thân côn dài Lc, đường mực nước ngầm thấp nhất MNN, ghi chú yêu cầu đầu cừ dưới MNN).</summary>
public static class CuTramDrawing
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v, string fmt = "0.##") => v.ToString(fmt, Inv);
    /// <summary>Tiêu đề / nhãn chung</summary>
    public const string Label = "CỪ TRÀM";
    public const string WaterLabel = "MNN THẤP NHẤT";
    /// <summary>Tỷ lệ mặt cắt cừ tràm (cây cừ dài 3–5 m vẽ ở 1/50)</summary>
    public const double SectionScale = 50;

    /// <summary>Ghi chú chuẩn: "Cừ tràm Ø8–10cm dài 4m, mật độ 25 cây/m², đầu cừ nằm dưới MNN thấp nhất".</summary>
    public static string Note(CuTramResult r) =>
        $"Cừ tràm Ø{F(r.D1 * 100, "0.#")}–{F(Math.Max(r.D1 * 100, 10), "0.#")}cm (ngọn ≥ Ø{F(r.D2 * 100, "0.#")}) dài {F(r.Length, "0.#")}m, mật độ {F(r.Density, "0")} cây/m², đầu cừ nằm dưới MNN thấp nhất";

    public static List<Figure> BuildDetails(Project p, DesignSummary s, double S)
    {
        var res = new List<Figure>();
        if (s == null) return res;
        bool first = true;
        var items = new List<(string Name, double L, double B, double H, int Qty, CuTramResult R)>();
        foreach (var g in FootingDrawing.Distinct(s.Footings.Where(f => f.CuTram != null)))
        {
            var f = g.First();
            items.Add((f.Name, f.L, f.B, f.H, Math.Max(1, f.Quantity), f.CuTram));
        }
        foreach (var st in s.StripFootings.Where(x => x.CuTram != null))
            items.Add((st.Name, Math.Min(st.Length, 3.0), st.Width, Math.Max(st.FlangeRoot, 0.3), Math.Max(1, st.Quantity), st.CuTram));
        foreach (var it in items)
        {
            try
            {
                res.Add(new Figure(Plan(it.Name, it.L, it.B, it.Qty, it.R, S), "CT " + it.Name, first));
                res.Add(new Figure(Section(p, it.Name, it.L, it.H, it.R, S), "CT " + it.Name + " mc"));
            }
            catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure("Cừ tràm " + it.Name, ex, S), "CT " + it.Name, true)); }
            first = false;
        }
        return res;
    }

    /// <summary>Mặt bằng lưới cừ tràm dưới móng L × B (tỷ lệ chi tiết móng).</summary>
    public static DxfWriter Plan(string name, double Lm, double Bm, int qty, CuTramResult r, double S)
    {
        var w = new DxfWriter(S);
        double det = DrawingStyle.Current.ScaleOf(DrawingKind.FootingDetail);
        double th = w.Th, k = S / det;
        double L = Lm * 1000 * k, B = Bm * 1000 * k;
        w.Rect(Lay.BeTong, 0, 0, L, B);
        int nx = Math.Max(1, r.Nx), ny = Math.Max(1, r.Ny);
        double sx = L / nx, sy = B / ny, rad = Math.Max(r.D1 * 500 * k, 0.15 * th);
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
                w.Circle(Lay.KyHieu, sx * (i + 0.5), sy * (j + 0.5), rad);
        w.DimChainH(new List<double> { 0, sx / 2, sx * 1.5 }, 0, -3.5 * th, k);
        w.Dim(0, 0, L, 0, -6.5 * th, k);
        w.Dim(0, 0, 0, B, 3.5 * th, k);
        w.Text(Lay.Chu, L / 2, B + 1.0 * th, 0.9 * th, $"{Label} {r.Count} CÂY ({F(r.Density, "0")} cây/m², lưới ≈ {nx}×{ny}, a ≈ {F(r.Spacing * 1000, "0")})", 0, 1, 0);
        w.Text(Lay.Chu, L / 2, -9.5 * th, 0.8 * th, Note(r), 0, 1, 0);
        w.Title(L / 2, w.Extents.Y2 + 4 * th, $"MẶT BẰNG {Label} DƯỚI {name} (SL={qty})", DrawUtil.ScaleText(det));
        return w;
    }

    /// <summary>Mặt cắt móng + cừ tràm (tỷ lệ 1/50): móng, bê tông lót, cát đệm, cây cừ thân côn, mặt đất, đường MNN thấp nhất.</summary>
    public static DxfWriter Section(Project p, string name, double Lm, double Hm, CuTramResult r, double S)
    {
        var w = new DxfWriter(S);
        double th = w.Th, k = S / SectionScale;
        double mm = 1000 * k;
        double L = Lm * mm, H = Math.Max(Hm, 0.2) * mm;
        // gốc toạ độ: đáy móng y = 0; độ sâu z (m từ mặt đất) → y = (Df − z)·mm
        double Yz(double z) => (r.Df - z) * mm;
        double lean = r.LeanThickness * mm, ext = 100 * k;
        w.Rect(Lay.BeTong, 0, 0, L, H);   // móng
        w.Rect(Lay.BeTong, -ext, -lean, L + ext, 0);   // bê tông lót
        w.Text(Lay.Chu, L + ext + 0.6 * th, -lean / 2, 0.75 * th, $"BT lót dày {F(r.LeanThickness * 1000, "0")}", 0, 0, 2);
        if (r.SandCushion > 1e-6)
        {
            double ys = Yz(r.SoilTop);
            w.Rect(Lay.KyHieu, -ext, ys, L + ext, -lean, "DASHED");
            w.Text(Lay.Chu, L + ext + 0.6 * th, (ys - lean) / 2, 0.75 * th, $"Cát đệm dày {F(r.SandCushion * 1000, "0")}", 0, 0, 2);
        }
        // cây cừ (thân côn)
        int n = Math.Max(1, Math.Min(r.Nx, 30));
        double sx = L / n, yt = Yz(r.TopDepth), yb = Yz(r.TipDepth), d1 = Math.Max(r.D1 * mm, 0.08 * th), d2 = Math.Max(r.D2 * mm, 0.05 * th);
        for (int i = 0; i < n; i++)
        {
            double x = sx * (i + 0.5);
            w.Polyline(Lay.KyHieu, new List<(double X, double Y)> { (x - d1 / 2, yt), (x - d2 / 2, yb), (x + d2 / 2, yb), (x + d1 / 2, yt) }, true);
        }
        // mặt đất + cột
        double yg = Yz(0);
        w.Line(Lay.BeTong, -ext - 4 * th, yg, L + ext + 4 * th, yg);
        w.Text(Lay.Chu, -ext - 4 * th, yg + 0.3 * th, 0.75 * th, "MẶT ĐẤT TỰ NHIÊN", 0, 0, 0);
        double cb = Math.Min(0.3 * mm, L / 3);
        w.Rect(Lay.BeTong, L / 2 - cb / 2, H, L / 2 + cb / 2, Math.Max(yg, H + 300 * k));
        // mực nước ngầm thấp nhất
        if (double.IsFinite(r.WaterDepth))
        {
            double yw = Yz(r.WaterDepth);
            w.Line(Lay.KyHieu, -ext - 4 * th, yw, L + ext + 4 * th, yw, "DASHED");
            double xv = -ext - 2.5 * th, tri = 0.6 * th;
            w.Polyline(Lay.KyHieu, new List<(double X, double Y)> { (xv - tri / 2, yw + tri), (xv + tri / 2, yw + tri), (xv, yw) }, true);
            w.Text(Lay.Chu, xv + tri, yw + 0.3 * th, 0.75 * th, $"{WaterLabel} −{F(r.WaterDepth, "0.00")} m{(r.WaterFromBorehole ? " (theo hố khoan)" : "")}", 0, 0, 0);
        }
        else w.Text(Lay.Chu, -ext - 4 * th, yb - 1.5 * th, 0.75 * th, "KHÔNG GẶP NƯỚC NGẦM – CỪ TRÀM KHÔNG ĐẢM BẢO ĐỘ BỀN", 0, 0, 0);
        // kích thước
        w.Dim(L + ext, yt, L + ext, yb, -2.5 * th - 0.0, k, $"Lc = {F(r.Length * 1000, "0")}");
        w.Dim(0, yb, L, yb, -2.5 * th, k);
        w.Dim(-ext, yg, -ext, 0, 2.5 * th, k, $"Df = {F(r.Df * 1000, "0")}");
        w.Text(Lay.Chu, L / 2, yb - 5.2 * th, 0.8 * th, Note(r), 0, 1, 0);
        w.Text(Lay.Chu, L / 2, yb - 6.6 * th, 0.75 * th, $"[p]1 = {F(r.P1, "0")} kN/m², [p]2 = {F(r.P2, "0")} kN/m² → [p] = {F(r.Allowable, "0")} kN/m²; "
                                                          + (r.DurabilityOk ? "đầu cừ dưới MNN – đạt" : "KHÔNG ĐẠT: đầu cừ trên MNN"), 0, 1, 0);
        w.Title(L / 2, w.Extents.Y1 - 1.2 * th, $"MẶT CẮT {Label} {name}", DrawUtil.ScaleText(SectionScale), true);
        return w;
    }

    /// <summary>Mặt bằng móng: ghi chú "cừ tràm" dưới tên móng gia cố.</summary>
    public static void AddToLayout(DxfWriter w, DesignSummary s, double k, ref Rect2 box)
    {
        if (w == null || s == null) return;
        double th = w.Th;
        foreach (var f in s.Footings.Where(x => x.CuTram != null))
        {
            var locs = f.Eccentric?.Locations?.Count > 0 ? f.Eccentric.Locations.Select(l => (l.X + l.OffsetX, l.Y + l.OffsetY)).ToList() : new List<(double, double)> { (f.X, f.Y) };
            foreach (var (x, y) in locs)
            {
                double cx = x * 1000 * k, cy = y * 1000 * k, hb = f.B * 500 * k;
                w.Text(Lay.Chu, cx, cy - hb + 0.4 * th, 0.7 * th, $"{Label} {F(f.CuTram.Density, "0")}c/m²", 0, 1, 0);
            }
        }
    }
}
