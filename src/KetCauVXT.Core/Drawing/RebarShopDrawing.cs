using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Bản vẽ gia công cốt thép (shop drawing) DXF: bảng gia công theo loại cấu kiện (số hiệu trùng bản vẽ chi tiết,
/// mã hình dạng, phác hình uốn có kích thước từng đoạn, chiều dài cắt, số lượng, khối lượng), sơ đồ cắt cây thép thương phẩm
/// theo Ø và bảng tổng hợp hao hụt. Vẽ theo mm giấy, tỷ lệ 1/1 (giống bảng thống kê KC-06).</summary>
public static class RebarShopDrawing
{
    public const double SheetScale = 1;

    private static readonly string[] Heads =
    {
        "TÊN CẤU KIỆN", "SỐ HIỆU", "MÃ HÌNH", "HÌNH DẠNG & KÍCH THƯỚC (mm)", "Ø (mm)", "CHIỀU DÀI CẮT 1 THANH (mm)",
        "SỐ C.KIỆN", "SỐ THANH", "TỔNG CHIỀU DÀI (m)", "TỔNG TRỌNG LƯỢNG (kg)", "GHI CHÚ"
    };
    private static readonly double[] ColW = { 26, 14, 12, 66, 11, 22, 14, 30, 20, 22, 44 };

    /// <summary>Các loại cấu kiện có thép gia công, theo thứ tự xuất.</summary>
    public static readonly string[] Categories = { "Dầm", "Cột", "Sàn", "Móng" };

    /// <summary>Mảnh hình bản vẽ gia công. category: "Dầm"/"Cột"/"Sàn"/"Móng" hoặc rỗng = tất cả (bảng từng loại + phương án cắt chung).</summary>
    public static List<Figure> BuildFigures(Project p, DesignSummary s, string category = null, ShopResult shop = null, int maxRows = 26)
    {
        var figs = new List<Figure>();
        if (p == null || s == null) return figs;
        string cat = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        if (shop == null)
        {
            var o = ShopOptions.From(p.RebarPrefs);
            o.Category = cat;
            shop = RebarShop.Build(p, s, o);
        }
        if (shop.Bars.Count == 0) return figs;
        foreach (var c in Categories)
        {
            if (cat != null && c != cat) continue;
            var rows = shop.Bars.Where(b => RebarShop.CategoryGroup(b.Category) == c).ToList();
            if (rows.Count == 0) continue;
            int parts = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)maxRows));
            for (int i = 0; i < parts; i++)
            {
                var part = rows.Skip(i * maxRows).Take(maxRows).ToList();
                string title = "BẢNG GIA CÔNG CỐT THÉP – " + c.ToUpperInvariant() + (i > 0 ? $" (tiếp {i + 1})" : "");
                figs.Add(new Figure(Table(part, title), "GC_" + c + "_" + (i + 1), i == 0));
            }
        }
        figs.Add(new Figure(CutPlan(shop), "PAC", true));
        figs.Add(new Figure(Summary(shop), "THGC", false));
        return figs;
    }

    // ------------------------------------------------------------------
    // Bảng gia công
    // ------------------------------------------------------------------
    private static DxfWriter Table(List<ShopBar> rows, string title)
    {
        var w = new DxfWriter(SheetScale);
        double th = w.Th, u = SheetScale;
        double rowH = 17 * u, headH = 16 * u;
        double[] cx = new double[ColW.Length + 1];
        for (int i = 0; i < ColW.Length; i++) cx[i + 1] = cx[i] + ColW[i] * u;
        double W = cx[^1];
        double y = 0;
        w.Text(Lay.Chu, W / 2, y + 3 * u, 1.6 * th, title, 0, 1, 0);
        double yh = y - headH;
        w.Line(Lay.KhungTen, 0, y, W, y);
        w.Line(Lay.KhungTen, 0, yh, W, yh);
        for (int i = 0; i < Heads.Length; i++)
        {
            double cxm = (cx[i] + cx[i + 1]) / 2;
            if (i == 7)
            {
                double ym = y - headH / 2;
                w.Line(Lay.KhungTen, cx[i], ym, cx[i + 1], ym);
                w.Text(Lay.Chu, cxm, ym + headH / 4, 0.85 * th, "SỐ THANH", 0, 1, 2);
                double cm = (cx[i] + cx[i + 1]) / 2;
                w.Line(Lay.KhungTen, cm, ym, cm, yh);
                w.Text(Lay.Chu, (cx[i] + cm) / 2, yh + headH / 4, 0.8 * th, "MỘT CK", 0, 1, 2);
                w.Text(Lay.Chu, (cm + cx[i + 1]) / 2, yh + headH / 4, 0.8 * th, "TOÀN BỘ", 0, 1, 2);
            }
            else Wrap(w, cxm, y - headH / 2, ColW[i] * u, Heads[i], 0.8 * th);
        }
        double yr = yh;
        string cur = null; double top = yh; int start = 0;
        var spans = new List<(string Name, double Y1, double Y2)>();
        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            string key = row.Category + "|" + row.Member;
            if (key != cur)
            {
                if (cur != null) spans.Add((rows[start].Member, yr, top));
                cur = key; start = r; top = yr;
            }
            double y2 = yr - rowH, ym = (yr + y2) / 2;
            w.Line(Lay.KhungTen, cx[1], y2, W, y2);
            LabelCircle(w, (cx[1] + cx[2]) / 2, ym, row.Label);
            w.Text(Lay.Chu, (cx[2] + cx[3]) / 2, ym, th, row.ShapeCode, 0, 1, 2);
            DrawShape(w, row, cx[3] + 3 * u, cx[4] - 3 * u, y2 + 2 * u, yr - 2 * u);
            w.Text(Lay.Chu, (cx[4] + cx[5]) / 2, ym, th, row.Dia.ToString(DrawUtil.Inv), 0, 1, 2);
            w.Text(Lay.Chu, (cx[5] + cx[6]) / 2, ym, th, DxfWriter.N0(row.CutLengthMm), 0, 1, 2);
            w.Text(Lay.Chu, (cx[6] + cx[7]) / 2, ym, th, row.MemberCount.ToString(DrawUtil.Inv), 0, 1, 2);
            double cm = (cx[7] + cx[8]) / 2;
            w.Text(Lay.Chu, (cx[7] + cm) / 2, ym, th, row.BarsPerMember.ToString(DrawUtil.Inv), 0, 1, 2);
            w.Text(Lay.Chu, (cm + cx[8]) / 2, ym, th, row.BarsTotal.ToString(DrawUtil.Inv), 0, 1, 2);
            w.Text(Lay.Chu, (cx[8] + cx[9]) / 2, ym, th, row.TotalLengthM.ToString("0.00", DrawUtil.Inv), 0, 1, 2);
            w.Text(Lay.Chu, (cx[9] + cx[10]) / 2, ym, th, row.WeightKg.ToString("0.00", DrawUtil.Inv), 0, 1, 2);
            string note = string.IsNullOrEmpty(row.Note) ? row.Role : row.Role + "; " + row.Note;
            Wrap(w, (cx[10] + cx[11]) / 2, ym, ColW[10] * u, note, 0.75 * th);
            yr = y2;
        }
        if (cur != null) spans.Add((rows[start].Member, yr, top));
        foreach (var (name, y1, y2m) in spans)
        {
            w.Line(Lay.KhungTen, 0, y1, cx[1], y1);
            double ym = (y1 + y2m) / 2;
            if (y2m - y1 >= DxfWriter.TextWidth(name, th) + 2 * u) w.Text(Lay.Chu, (cx[0] + cx[1]) / 2, ym, th, name, 90, 1, 2);
            else Wrap(w, (cx[0] + cx[1]) / 2, ym, ColW[0] * u, name, th);
        }
        double yt = yr - rowH;
        w.Line(Lay.KhungTen, 0, yt, W, yt);
        w.Text(Lay.Chu, cx[1] + 2 * u, (yr + yt) / 2, th, "TỔNG CỘNG", 0, 0, 2);
        w.Text(Lay.Chu, (cx[9] + cx[10]) / 2, (yr + yt) / 2, th, rows.Sum(r => r.WeightKg).ToString("0.00", DrawUtil.Inv), 0, 1, 2);
        for (int i = 0; i < cx.Length; i++)
        {
            bool full = i == 0 || i == cx.Length - 1 || i >= 8;
            w.Line(Lay.KhungTen, cx[i], y, cx[i], full ? yt : yr);
        }
        w.Line(Lay.KhungTen, (cx[7] + cx[8]) / 2, yh, (cx[7] + cx[8]) / 2, yr);
        return w;
    }

    /// <summary>Vòng số hiệu gia công (cho phép hậu tố chữ: "3a").</summary>
    private static void LabelCircle(DxfWriter w, double cx, double cy, string label)
    {
        double r = w.MarkR * (label.Length > 2 ? 1.25 : 1.0);
        w.Circle(Lay.KyHieu, cx, cy, r);
        w.Text(Lay.Chu, cx, cy, (label.Length > 2 ? 0.75 : 0.9) * w.Th, label, 0, 1, 2);
    }

    private static void Wrap(DxfWriter w, double cx, double cy, double width, string text, double h)
    {
        var words = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        string cur = "";
        foreach (var wd in words)
        {
            string t = cur.Length == 0 ? wd : cur + " " + wd;
            if (DxfWriter.TextWidth(t, h) > width * 0.92 && cur.Length > 0) { lines.Add(cur); cur = wd; }
            else cur = t;
        }
        if (cur.Length > 0) lines.Add(cur);
        if (lines.Count > 4) { lines = lines.Take(4).ToList(); lines[3] += "…"; }
        double lh = 1.35 * h;
        double y0 = cy + (lines.Count - 1) * lh / 2;
        for (int i = 0; i < lines.Count; i++) w.Text(Lay.Chu, cx, y0 - i * lh, h, lines[i], 0, 1, 2);
    }

    /// <summary>Phác hình uốn trong ô (x1..x2, y1..y2), ghi kích thước từng đoạn (mm).</summary>
    internal static void DrawShape(DxfWriter w, ShopBar r, double x1, double x2, double y1, double y2)
    {
        double th = 0.8 * w.Th;
        var seg = r.Segments ?? new List<double>();
        double xm = (x1 + x2) / 2, ym = (y1 + y2) / 2, hh = y2 - y1, ww = x2 - x1;
        string N(double v) => DxfWriter.N0(v);
        switch (r.ShapeCode)
        {
            case "51":
            {
                double a = seg.Count > 0 ? seg[0] : 200, b = seg.Count > 1 ? seg[1] : 300;
                double bh = hh * 0.62, bw = Math.Min(ww * 0.42, Math.Max(bh * a / Math.Max(b, 1), bh * 0.45));
                double bx1 = xm - bw / 2 - 0.12 * ww, by1 = ym - bh / 2 - 0.05 * hh;
                w.Rect(Lay.Dai, bx1, by1, bx1 + bw, by1 + bh);
                double hk = Math.Min(0.35 * bh, 0.35 * bw);
                // hai móc 135° tại góc trên trái
                w.Line(Lay.Dai, bx1, by1 + bh, bx1 + 0.7 * hk, by1 + bh - 0.7 * hk);
                w.Line(Lay.Dai, bx1 + 0.12 * hk, by1 + bh, bx1 + 0.82 * hk, by1 + bh - 0.7 * hk);
                w.Text(Lay.Chu, bx1 + bw / 2, by1 - 0.3 * th, th, N(a), 0, 1, 3);
                w.Text(Lay.Chu, bx1 + bw + 0.4 * th, by1 + bh / 2, th, N(b), 0, 0, 2);
                w.Text(Lay.Chu, bx1 + bw + 0.4 * th + DxfWriter.TextWidth(N(b), th) + 1.2 * th, by1 + bh / 2, 0.85 * th, "móc 135° " + N(r.HookMm), 0, 0, 2);
                return;
            }
            case "52":
            {
                double a = seg.Count > 0 ? seg[0] : 200;
                double xa = x1 + 0.15 * ww, xb = x1 + 0.6 * ww, yb = ym;
                w.Line(Lay.Dai, xa, yb, xb, yb);
                w.Line(Lay.Dai, xa, yb, xa + 0.2 * hh, yb - 0.3 * hh);
                w.Line(Lay.Dai, xb, yb, xb - 0.2 * hh, yb - 0.3 * hh);
                w.Text(Lay.Chu, (xa + xb) / 2, yb + 0.3 * th, th, N(a), 0, 1, 1);
                w.Text(Lay.Chu, xb + 1.0 * th, yb, 0.85 * th, "móc 135° " + N(r.HookMm), 0, 0, 2);
                return;
            }
        }
        double yb2 = ym - 0.2 * hh, hk2 = hh * 0.45;
        if (r.ShapeCode == "21" && seg.Count >= 3)
        {
            double xa = x1 + 0.12 * ww, xb = x2 - 0.12 * ww;
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xa, yb2 + hk2), (xa, yb2), (xb, yb2), (xb, yb2 + hk2) }, false);
            w.Text(Lay.Chu, xm, yb2 + 0.3 * th, th, N(seg[1]), 0, 1, 1);
            w.Text(Lay.Chu, xa - 0.3 * th, yb2 + hk2 / 2, th, N(seg[0]), 0, 2, 2);
            w.Text(Lay.Chu, xb + 0.3 * th, yb2 + hk2 / 2, th, N(seg[^1]), 0, 0, 2);
            return;
        }
        if (r.ShapeCode == "11" && seg.Count >= 2)
        {
            bool hookLeft = seg[0] <= seg[^1];
            double hook = hookLeft ? seg[0] : seg[^1], main = hookLeft ? seg[^1] : seg[0];
            double xa = x1 + (hookLeft ? 0.12 : 0.04) * ww, xb = x2 - (hookLeft ? 0.04 : 0.12) * ww;
            if (hookLeft)
            {
                w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xa, yb2 + hk2), (xa, yb2), (xb, yb2) }, false);
                w.Text(Lay.Chu, xa - 0.3 * th, yb2 + hk2 / 2, th, N(hook), 0, 2, 2);
            }
            else
            {
                w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xa, yb2), (xb, yb2), (xb, yb2 + hk2) }, false);
                w.Text(Lay.Chu, xb + 0.3 * th, yb2 + hk2 / 2, th, N(hook), 0, 0, 2);
            }
            w.Text(Lay.Chu, (xa + xb) / 2, yb2 + 0.3 * th, th, N(main), 0, 1, 1);
            return;
        }
        if (r.ShapeCode == "31")
        {
            double xa = x1 + 0.1 * ww, xb = x2 - 0.1 * ww, rr = 0.18 * hh;
            w.Line(Lay.Thep, xa, yb2, xb, yb2);
            w.Arc(Lay.Thep, xa, yb2 + rr, rr, 90, 270);
            w.Arc(Lay.Thep, xb, yb2 + rr, rr, -90, 90);
            w.Text(Lay.Chu, xm, yb2 + 0.3 * th, th, N(seg.Count > 0 ? seg[0] : r.CutLengthMm), 0, 1, 1);
            return;
        }
        double len = seg.Count == 1 ? seg[0] : (seg.Count > 1 ? seg.Sum() : r.CutLengthMm);
        w.Line(Lay.Thep, x1 + 0.05 * ww, yb2, x2 - 0.05 * ww, yb2);
        w.Text(Lay.Chu, xm, yb2 + 0.3 * th, th, N(len), 0, 1, 1);
    }

    // ------------------------------------------------------------------
    // Phương án cắt
    // ------------------------------------------------------------------
    private static DxfWriter CutPlan(ShopResult shop, int maxPatterns = 14)
    {
        var w = new DxfWriter(SheetScale);
        double th = w.Th, u = SheetScale;
        double stock = shop.Options.StockLength * 1000;
        double barW = 200 * u, x0 = 44 * u, rowH = 9 * u;
        double k = barW / Math.Max(stock, 1);
        double y = 0;
        w.Text(Lay.Chu, (x0 + barW) / 2, y + 3 * u, 1.6 * th,
            "SƠ ĐỒ CẮT THÉP TỪ CÂY " + DxfWriter.N0(stock) + " mm (tối ưu first-fit decreasing)", 0, 1, 0);
        y -= 6 * u;
        foreach (var pl in shop.Plans)
        {
            var pats = pl.Patterns();
            w.Text(Lay.Chu, 0, y - 0.5 * rowH, 1.1 * th, string.Format(DrawUtil.Inv,
                "Ø{0}: {1} cây, cần {2:0.00} m, đầu thừa tái sử dụng {3:0.00} m, hao hụt {4:0.00} m ({5:0.0}%)",
                pl.Dia, pl.StockCount, pl.PieceLengthM, pl.ReusableM, pl.WasteM, pl.WastePct), 0, 0, 2);
            y -= rowH;
            foreach (var pt in pats.Take(maxPatterns))
            {
                double yb = y - rowH * 0.75, yt = y - rowH * 0.2;
                w.Text(Lay.Chu, 2 * u, (yb + yt) / 2, 0.85 * th, pt.Count.ToString(DrawUtil.Inv) + " cây ×", 0, 0, 2);
                double xc = x0;
                foreach (var (label, mm) in pt.Pieces)
                {
                    double xw = mm * k;
                    w.Rect(Lay.Thep, xc, yb, xc + xw, yt);
                    string t = label.Contains('/') ? label.Substring(label.LastIndexOf('/') + 1) : label;
                    string txt = t + " " + DxfWriter.N0(mm);
                    double h = DxfWriter.TextWidth(txt, 0.7 * th) < xw * 0.95 ? 0.7 * th : (DxfWriter.TextWidth(t, 0.7 * th) < xw * 0.95 ? 0.7 * th : 0.45 * th);
                    if (DxfWriter.TextWidth(txt, h) > xw * 0.95) txt = t;
                    w.Text(Lay.Chu, xc + xw / 2, (yb + yt) / 2, h, txt, 0, 1, 2);
                    xc += xw;
                }
                if (pt.LeftoverMm > 1)
                {
                    w.Rect(pt.Reusable ? Lay.KichThuoc : Lay.KyHieu, xc, yb, x0 + barW, yt);
                    w.Text(Lay.Chu, x0 + barW + 1.5 * u, (yb + yt) / 2, 0.75 * th,
                        (pt.Reusable ? "thừa dùng lại " : "hao hụt ") + DxfWriter.N0(pt.LeftoverMm), 0, 0, 2);
                }
                y -= rowH;
            }
            if (pats.Count > maxPatterns)
            {
                w.Text(Lay.Chu, x0, y - 0.5 * rowH, 0.8 * th, $"… và {pats.Count - maxPatterns} mẫu cắt khác (xem bảng Excel \"Gia công thép\")", 0, 0, 2);
                y -= rowH;
            }
            foreach (var (label, mm) in pl.Oversize.Take(3))
            {
                w.Text(Lay.Chu, x0, y - 0.5 * rowH, 0.8 * th, $"Đoạn {label} dài {DxfWriter.N0(mm)} mm > cây {DxfWriter.N0(stock)} mm – đặt cây dài riêng", 0, 0, 2);
                y -= rowH;
            }
            y -= 0.5 * rowH;
        }
        return w;
    }

    // ------------------------------------------------------------------
    // Tổng hợp
    // ------------------------------------------------------------------
    private static DxfWriter Summary(ShopResult shop)
    {
        var w = new DxfWriter(SheetScale);
        double th = w.Th, u = SheetScale, rh = 9 * u;
        string[] hd = { "Ø (mm)", "Số cây thép", "Chiều dài cần (m)", "Thừa dùng lại (m)", "Hao hụt (m)", "Hao hụt (%)", "KL gia công (kg)", "KL thép cây (kg)" };
        double[] cw = { 16, 20, 24, 24, 20, 18, 24, 24 };
        double[] cx = new double[cw.Length + 1];
        for (int i = 0; i < cw.Length; i++) cx[i + 1] = cx[i] + cw[i] * u;
        double W = cx[^1];
        w.Text(Lay.Chu, W / 2, 3 * u, 1.6 * th, "TỔNG HỢP GIA CÔNG THÉP", 0, 1, 0);
        int nr = shop.Plans.Count + 2;
        for (int r = 0; r <= nr; r++) w.Line(Lay.KhungTen, 0, -r * rh, W, -r * rh);
        for (int i = 0; i < cx.Length; i++) w.Line(Lay.KhungTen, cx[i], 0, cx[i], -nr * rh);
        for (int i = 0; i < hd.Length; i++) w.Text(Lay.Chu, (cx[i] + cx[i + 1]) / 2, -0.5 * rh, 0.75 * th, hd[i], 0, 1, 2);
        int row = 1;
        foreach (var pl in shop.Plans)
        {
            double ym = -(row + 0.5) * rh;
            string[] v =
            {
                "Ø" + pl.Dia, pl.StockCount.ToString(DrawUtil.Inv), pl.PieceLengthM.ToString("0.00", DrawUtil.Inv), pl.ReusableM.ToString("0.00", DrawUtil.Inv),
                pl.WasteM.ToString("0.00", DrawUtil.Inv), pl.WastePct.ToString("0.0", DrawUtil.Inv), pl.PieceWeightKg.ToString("0.00", DrawUtil.Inv), pl.StockWeightKg.ToString("0.00", DrawUtil.Inv)
            };
            for (int i = 0; i < v.Length; i++) w.Text(Lay.Chu, (cx[i] + cx[i + 1]) / 2, ym, th, v[i], 0, 1, 2);
            row++;
        }
        {
            double ym = -(row + 0.5) * rh;
            double st = shop.Plans.Sum(x => x.StockLengthM), ws = shop.Plans.Sum(x => x.WasteM);
            string[] v =
            {
                "CỘNG", shop.Plans.Sum(x => x.StockCount).ToString(DrawUtil.Inv), shop.Plans.Sum(x => x.PieceLengthM).ToString("0.00", DrawUtil.Inv),
                shop.Plans.Sum(x => x.ReusableM).ToString("0.00", DrawUtil.Inv), ws.ToString("0.00", DrawUtil.Inv),
                (st > 0 ? ws / st * 100 : 0).ToString("0.0", DrawUtil.Inv), shop.Plans.Sum(x => x.PieceWeightKg).ToString("0.00", DrawUtil.Inv),
                shop.Plans.Sum(x => x.StockWeightKg).ToString("0.00", DrawUtil.Inv)
            };
            for (int i = 0; i < v.Length; i++) w.Text(Lay.Chu, (cx[i] + cx[i + 1]) / 2, ym, th, v[i], 0, 1, 2);
        }
        // nhóm đường kính (khối lượng gia công)
        double y0 = -nr * rh - 6 * u;
        double g1 = shop.Bars.Where(b => b.Dia <= 10).Sum(b => b.WeightKg), g2 = shop.Bars.Where(b => b.Dia > 10 && b.Dia <= 18).Sum(b => b.WeightKg),
               g3 = shop.Bars.Where(b => b.Dia > 18).Sum(b => b.WeightKg);
        string[] gl = { "NHÓM Ø≤10 (kg)", "NHÓM 10<Ø≤18 (kg)", "NHÓM Ø>18 (kg)", "TỔNG CỘNG (kg)" };
        double[] gv = { g1, g2, g3, g1 + g2 + g3 };
        double gw = 45 * u, vw = 30 * u;
        for (int r = 0; r <= gl.Length; r++) w.Line(Lay.KhungTen, 0, y0 - r * rh, gw + vw, y0 - r * rh);
        w.Line(Lay.KhungTen, 0, y0, 0, y0 - gl.Length * rh);
        w.Line(Lay.KhungTen, gw, y0, gw, y0 - gl.Length * rh);
        w.Line(Lay.KhungTen, gw + vw, y0, gw + vw, y0 - gl.Length * rh);
        for (int r = 0; r < gl.Length; r++)
        {
            w.Text(Lay.Chu, 2 * u, y0 - (r + 0.5) * rh, th, gl[r], 0, 0, 2);
            w.Text(Lay.Chu, gw + vw / 2, y0 - (r + 0.5) * rh, th, gv[r].ToString("0.00", DrawUtil.Inv), 0, 1, 2);
        }
        // ghi chú
        double yn = y0 - gl.Length * rh - 6 * u;
        var notes = new List<string>
        {
            "GHI CHÚ GIA CÔNG:",
            "1. Số hiệu gia công trùng số hiệu trên bản vẽ chi tiết; thanh dài hơn cây " + DxfWriter.N0(shop.Options.StockLength * 1000) + " mm được tách nối chồng, ghi hậu tố a, b, c…",
            "2. Nối chồng theo TCVN 5574:2018 mục 10.3.6: ll = 1,2·l0,an (vùng kéo), l0,an = Rs·d/(4·η1·η2·Rbt); ≥ 20d và ≥ 250 mm.",
            "3. Thép dưới dầm nối trong ¼ nhịp gần gối, thép trên nối ở ⅓ giữa nhịp; nối so le, tim mối nối cách nhau ≥ 1,3·ll (≤ 50% số thanh tại một tiết diện).",
            "4. Đai kín móc 135°, đoạn móc max(75 mm; 10d); chiều dài cắt = 2(A + B) + 2·móc. Chiều dài cắt chưa trừ độ giãn khi uốn.",
            "5. Đầu thừa ≥ " + DxfWriter.N0(shop.Options.MinReuseOffcut * 1000) + " mm thu hồi dùng lại; đầu thừa ngắn hơn tính hao hụt."
        };
        foreach (var n in notes) { w.Text(Lay.Chu, 0, yn, 0.85 * th, n, 0, 0, 0); yn -= 1.6 * th; }
        foreach (var m in shop.Messages.Distinct().Take(8)) { w.Text(Lay.Chu, 0, yn, 0.75 * th, "· " + m, 0, 0, 0); yn -= 1.4 * th; }
        return w;
    }

    // ------------------------------------------------------------------
    // Xuất file
    // ------------------------------------------------------------------

    /// <summary>Ghi các bản vẽ gia công KC-07.x theo loại cấu kiện (bảng gia công + sơ đồ cắt riêng loại đó) và KC-07 tổng hợp
    /// (sơ đồ cắt chung). Trả về danh sách file đã ghi.</summary>
    public static List<string> ExportAll(Project p, DesignSummary s, string folder)
    {
        var paths = new List<string>();
        if (p == null || s == null) return paths;
        Directory.CreateDirectory(folder);
        SheetComposer.SheetInfo Info(string title, string no) => new SheetComposer.SheetInfo
        {
            ProjectName = p.Name, Title = title, SheetNo = no, Designer = p.Designer, Owner = p.Owner, Location = p.Location
        };
        int k = 0;
        foreach (var c in Categories)
        {
            var o = ShopOptions.From(p.RebarPrefs);
            o.Category = c;
            var shop = RebarShop.Build(p, s, o);
            if (shop.Bars.Count == 0) continue;
            k++;
            var figs = BuildFigures(p, s, c, shop);
            var w = SheetComposer.Compose(figs, SheetScale, Info("GIA CÔNG CỐT THÉP " + c.ToUpperInvariant(), "KC-07." + k), DrawingSet.Paper);
            string path = Path.Combine(folder, "KC-07." + k + "_GiaCongThep_" + DrawUtil.SafeFile(Ascii(c)) + ".dxf");
            w.Save(path);
            paths.Add(path);
        }
        var all = RebarShop.Build(p, s, ShopOptions.From(p.RebarPrefs));
        if (all.Bars.Count > 0)
        {
            var figs = new List<Figure> { new Figure(CutPlan(all), "PAC", true), new Figure(Summary(all), "THGC", false) };
            var w = SheetComposer.Compose(figs, SheetScale, Info("PHƯƠNG ÁN CẮT VÀ TỔNG HỢP GIA CÔNG THÉP", "KC-07"), DrawingSet.Paper);
            string path = Path.Combine(folder, "KC-07_PhuongAnCatThep.dxf");
            w.Save(path);
            paths.Add(path);
        }
        return paths;
    }

    private static string Ascii(string c) => c switch { "Dầm" => "Dam", "Cột" => "Cot", "Sàn" => "San", "Móng" => "Mong", _ => c };
}
