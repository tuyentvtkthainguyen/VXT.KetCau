using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

// =====================================================================
// DANH MỤC MẶT CẮT DẦM (bảng nhỏ dưới các mặt cắt) và CHIA ĐOẠN MẶT ĐỨNG DẦM CHO BẢN VẼ CHẾ ĐỘ THIẾT KẾ
// =====================================================================
public static partial class BeamDrawing
{
    public const string CatalogTitle = "DANH MỤC MẶT CẮT";

    /// <summary>Bật danh mục mặt cắt trên bản vẽ dầm chế độ Thiết kế (KC-04).</summary>
    public static bool DesignSectionCatalog { get; set; }

    /// <summary>Một dòng danh mục mặt cắt.</summary>
    public sealed class CatalogRow
    {
        public int Num;
        public List<string> Places = new();
        public string Size = "", Top = "", Bottom = "", Stirrup = "";
    }

    /// <summary>Các dòng danh mục: số mặt cắt, vị trí (gối / giữa nhịp – mọi vị trí dùng chung số), b×h, thép trên, thép dưới, đai.</summary>
    public static List<CatalogRow> Catalog(Project p, BeamLineDesign d)
    {
        var res = new List<CatalogRow>();
        var spans = d?.Spans ?? new List<BeamSpanResult>();
        if (spans.Count == 0) return res;
        var xs = new List<double> { 0 };
        foreach (var sp in spans) xs.Add(xs[^1] + sp.L * 1000);
        int ns = xs.Count;
        var sw = new double[ns];
        var gn = new string[ns];
        for (int i = 0; i < ns; i++)
        {
            double v = i == 0 ? spans[0].SupportWidthLeft : i == ns - 1 ? spans[^1].SupportWidthRight : Math.Max(spans[i - 1].SupportWidthRight, spans[i].SupportWidthLeft);
            sw[i] = v > 1e-6 ? v * 1000 : 220;
            string g = (i == 0 ? spans[0].LeftSupport : spans[i - 1].RightSupport) ?? "";
            gn[i] = g.Length > 0 ? g : (i + 1).ToString();
        }
        double b = d.B * 1000, h = d.H * 1000;
        if (b <= 0 || h <= 0)
        {
            var si = DrawUtil.SectionOf(p, d.Section);
            if (b <= 0) b = si.B * 1000;
            if (h <= 0) h = si.H * 1000;
        }
        var cuts = ComputeCuts(d, xs, sw, h > 0 ? h : 400);
        string Bars(List<BarMark> l) => l.Count == 0 ? "–" : string.Join(" + ", l.OrderBy(t => t.Mark).Select(t => $"{t.Count}Ø{t.Dia} ({t.Mark})"));
        for (int k = 0; k < cuts.Count; k++)
        {
            var c = cuts[k];
            int sp = k / 3, role = k % 3;
            string place = role == 1 ? $"giữa nhịp {gn[sp]}–{gn[sp + 1]}" : $"gối {(role == 0 ? gn[sp] : gn[sp + 1])} (nhịp {gn[sp]}–{gn[sp + 1]})";
            var row = res.FirstOrDefault(r => r.Num == c.Num);
            if (row == null)
            {
                row = new CatalogRow
                {
                    Num = c.Num, Size = $"{DxfWriter.N0(b)}×{DxfWriter.N0(h)}", Top = Bars(c.Top), Bottom = Bars(c.Bot),
                    Stirrup = c.Zone == null ? "–" : $"Ø{c.Zone.Dia}a{DxfWriter.N0(c.Zone.Spacing * 1000)}" + (c.Zone.Legs > 2 ? $" ({c.Zone.Legs} nhánh)" : "")
                };
                res.Add(row);
            }
            if (!row.Places.Contains(place)) row.Places.Add(place);
        }
        return res.OrderBy(r => r.Num).ToList();
    }

    /// <summary>Hình danh mục mặt cắt của một dầm (vẽ theo mm giấy, tỷ lệ 1/1); null nếu dầm không có nhịp.</summary>
    public static Figure CatalogFigure(Project p, BeamLineDesign d)
    {
        var rows = Catalog(p, d);
        if (rows.Count == 0) return null;
        var w = new DxfWriter(1);
        double th = 2.5, rh = 6.5;
        double[] cw = { 14, 70, 22, 46, 46, 30 };
        string[] hd = { "MẶT CẮT", "VỊ TRÍ", "b×h (mm)", "THÉP TRÊN", "THÉP DƯỚI", "ĐAI" };
        double tw = cw.Sum(), y = 0;
        w.Text(Lay.Chu, tw / 2, 3, 1.4 * th, CatalogTitle + " " + d.Name, 0, 1, 0);
        w.Line(Lay.KhungTen, 0, y, tw, y);
        var table = new List<string[]> { hd };
        foreach (var r in rows)
        {
            // vị trí dài: tách nhiều dòng (tối đa 3 vị trí mỗi dòng)
            var chunks = r.Places.Select((s, i) => (s, i)).GroupBy(t => t.i / 2).Select(g => string.Join("; ", g.Select(t => t.s))).ToList();
            for (int i = 0; i < chunks.Count; i++)
                table.Add(i == 0 ? new[] { r.Num + "-" + r.Num, chunks[i], r.Size, r.Top, r.Bottom, r.Stirrup } : new[] { "", chunks[i], "", "", "", "" });
        }
        foreach (var row in table)
        {
            double cx = 0;
            for (int c = 0; c < cw.Length; c++)
            {
                bool left = c == 1 || c == 3 || c == 4;
                w.Text(Lay.Chu, left ? cx + 1.2 : cx + cw[c] / 2, y - rh / 2, row == hd ? 0.9 * th : 0.8 * th, row[c], 0, left ? 0 : 1, 2);
                cx += cw[c];
            }
            y -= rh;
            w.Line(Lay.KhungTen, 0, y, tw, y);
        }
        double xx = 0;
        for (int c = 0; c <= cw.Length; c++) { w.Line(Lay.KhungTen, xx, 0, xx, y); if (c < cw.Length) xx += cw[c]; }
        return new Figure(w, CatalogTitle + " " + d.Name);
    }

    /// <summary>Bản vẽ dầm chế độ Thiết kế có chia đoạn: dầm nhiều nhịp có mặt đứng rộng hơn maxWidthMm (mm giấy) → chia đều tại gối
    /// (ít đoạn nhất mà đoạn nào cũng vừa); mặt cắt giữ hình riêng; maxWidthMm ≤ 0 → như <see cref="BuildAll"/>.
    /// Có danh mục mặt cắt khi <see cref="DesignSectionCatalog"/>.</summary>
    public static List<Figure> BuildAllSplit(Project p, IEnumerable<BeamLineDesign> beams, DesignSummary ds, double maxWidthMm)
    {
        var res = new List<Figure>();
        foreach (var d in beams)
        {
            List<Figure> figs;
            try { figs = Build(p, d, ds); }
            catch (Exception ex) { res.Add(new Figure(DrawUtil.ErrorFigure(d.Name, ex, CurrentScale), d.Name, true)); continue; }
            try
            {
                int nsp = d.Spans?.Count ?? 0;
                if (maxWidthMm > 0 && nsp >= 2 && figs.Count > 0 && figs[0].Name == d.Name)
                {
                    double wMm = figs[0].W.Extents.W / Math.Max(figs[0].W.Scale, 1e-9);
                    if (wMm > maxWidthMm)
                        for (int n = Math.Max(2, (int)Math.Ceiling(wMm / maxWidthMm)); n <= nsp; n++)
                        {
                            var seg = BuildSegments(p, d, EvenBreaks(nsp, n), ds);
                            if (seg.Count < 2) break;
                            if (n < nsp && seg.Any(f => f.W.Extents.W / Math.Max(f.W.Scale, 1e-9) > maxWidthMm)) continue;
                            figs = seg.Concat(figs.Skip(1)).ToList();
                            break;
                        }
                }
                if (DesignSectionCatalog) { var cf = CatalogFigure(p, d); if (cf != null) figs.Add(cf); }
            }
            catch (Exception) { /* giữ hình nguyên */ }
            res.AddRange(figs);
        }
        return res;
    }
}
