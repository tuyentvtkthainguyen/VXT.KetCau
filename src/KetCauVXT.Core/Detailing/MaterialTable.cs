using System.Globalization;
using KetCauVXT.Drawing;

namespace KetCauVXT.Detailing;

/// <summary>BẢNG THỐNG KÊ VẬT LIỆU vẽ trên bản vẽ (chế độ Triển khai hồ sơ): bê tông, ván khuôn, cốt thép theo nhóm đường kính cho từng cấu kiện,
/// gộp theo loại cấu kiện có dòng cộng nhóm, dòng TỔNG CỘNG; kèm bảng TỔNG HỢP (bê tông theo cấp, bê tông lót, ván khuôn, thép theo nhóm Ø, cáp DƯL, công tác khác).
/// Vẽ ở tỷ lệ 1 (mm giấy) như bảng thống kê cốt thép (Drawing/RebarSchedule.cs); tối đa <see cref="MaxRows"/> dòng mỗi hình.</summary>
public static class MaterialTable
{
    public const int MaxRows = 35;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Độ rộng cột (mm giấy).</summary>
    public static readonly double[] ColW = { 10, 34, 10, 18, 20, 20, 18, 18, 18, 20, 18 };
    public static readonly string[] Heads =
    {
        "STT", "CẤU KIỆN", "SL", "BÊ TÔNG 1 CK (m³)", "BÊ TÔNG TOÀN BỘ (m³)", "VÁN KHUÔN (m²)",
        "THÉP Ø≤10 (kg)", "THÉP 10<Ø≤18 (kg)", "THÉP Ø>18 (kg)", "TỔNG THÉP (kg)", "HÀM LƯỢNG (kg/m³)"
    };

    /// <summary>Một dòng bảng: Kind = "row" | "group" (tiêu đề nhóm) | "sub" (cộng nhóm) | "total".</summary>
    public sealed class Row
    {
        public string Kind = "row";
        public string No = "", Name = "";
        public int Qty;
        public double Conc1, Conc, Form, S10, S18, S99;
        public double Steel => S10 + S18 + S99;
        public double Ratio => Conc > 1e-9 ? Steel / Conc : 0;
    }

    /// <summary>Dòng bảng theo cấu kiện (toàn bộ = 1 CK × SL), gộp theo loại, có cộng nhóm và tổng cộng.</summary>
    public static List<Row> Rows(List<(IDetailMember Member, DetailResult Result)> results)
    {
        var rows = new List<Row>();
        var total = new Row { Kind = "total", Name = "TỔNG CỘNG" };
        int no = 1;
        foreach (var g in results.Where(x => x.Result != null).GroupBy(x => string.IsNullOrEmpty(x.Result.Kind) ? "Khác" : x.Result.Kind))
        {
            rows.Add(new Row { Kind = "group", Name = g.Key.ToUpperInvariant() });
            var sub = new Row { Kind = "sub", Name = "Cộng " + g.Key.ToLowerInvariant() };
            foreach (var (m, r) in g)
            {
                int q = Math.Max(1, r.Quantity);
                double Kg(int lo, int hi) => r.Bars.Where(b => b.Dia >= lo && b.Dia <= hi).Sum(b => b.WeightPerBar * b.Count) * q;
                var row = new Row
                {
                    No = (no++).ToString(Inv), Name = m.Name, Qty = q, Conc1 = r.ConcreteVolume, Conc = r.ConcreteVolume * q,
                    Form = r.FormworkArea * q, S10 = Kg(0, 10), S18 = Kg(11, 18), S99 = Kg(19, 99)
                };
                rows.Add(row);
                foreach (var t in new[] { sub, total })
                {
                    t.Qty += q; t.Conc += row.Conc; t.Form += row.Form; t.S10 += row.S10; t.S18 += row.S18; t.S99 += row.S99;
                }
            }
            rows.Add(sub);
        }
        rows.Add(total);
        return rows;
    }

    /// <summary>Hình vẽ: bảng thống kê vật liệu (chia nhiều hình nếu nhiều dòng) + bảng tổng hợp.</summary>
    public static List<Figure> Build(DetailingProject p, List<(IDetailMember Member, DetailResult Result)> results)
    {
        var figs = new List<Figure>();
        if (results == null) return figs;
        var rows = Rows(results);
        int parts = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)MaxRows));
        for (int i = 0; i < parts; i++)
        {
            var part = rows.Skip(i * MaxRows).Take(MaxRows).ToList();
            figs.Add(new Figure(Table(part, i == 0 ? "BẢNG THỐNG KÊ VẬT LIỆU" : "BẢNG THỐNG KÊ VẬT LIỆU (tiếp " + (i + 1) + ")"), "TKVL" + (i + 1), i == 0));
        }
        figs.Add(new Figure(Summary(p, results), "THVL", false));
        return figs;
    }

    private static string F(double v, string fmt) => Math.Abs(v) < 1e-9 ? "" : v.ToString(fmt, Inv).Replace('.', ',');

    private static DxfWriter Table(List<Row> rows, string title)
    {
        var w = new DxfWriter(1);
        double th = w.ScheduleTh, rowH = Math.Max(7, 2.6 * th), headH = Math.Max(14, 5.2 * th);
        var cx = new double[ColW.Length + 1];
        for (int i = 0; i < ColW.Length; i++) cx[i + 1] = cx[i] + ColW[i];
        double W = cx[^1];
        w.Text(Lay.Chu, W / 2, 3, 1.5 * th, title, 0, 1, 0);
        double y = 0, yh = -headH;
        w.Polyline(Lay.KhungTen, new List<(double X, double Y)> { (0, y), (W, y) }, false, 0.5);
        w.Line(Lay.KhungTen, 0, yh, W, yh);
        for (int i = 0; i < Heads.Length; i++) Wrap(w, (cx[i] + cx[i + 1]) / 2, -headH / 2, ColW[i] - 1.5, Heads[i], 0.8 * th);
        double yr = yh;
        foreach (var r in rows)
        {
            double y2 = yr - rowH, ym = (yr + y2) / 2;
            bool bold = r.Kind != "row";
            if (r.Kind == "group")
            {
                w.Text(Lay.Chu, cx[1] + 1.5, ym, 0.9 * th, r.Name, 0, 0, 2);
            }
            else
            {
                string[] cells =
                {
                    r.No, r.Name, r.Kind == "row" ? r.Qty.ToString(Inv) : "", r.Kind == "row" ? F(r.Conc1, "0.000") : "", F(r.Conc, "0.000"), F(r.Form, "0.00"),
                    F(r.S10, "0.0"), F(r.S18, "0.0"), F(r.S99, "0.0"), F(r.Steel, "0.0"), r.Conc > 1e-9 ? F(r.Ratio, "0") : ""
                };
                for (int i = 0; i < cells.Length; i++)
                {
                    if (string.IsNullOrEmpty(cells[i])) continue;
                    if (i == 1) w.Text(Lay.Chu, cx[1] + 1.5, ym, (bold ? 0.9 : 0.85) * th, cells[i], 0, 0, 2);
                    else w.Text(Lay.Chu, (cx[i] + cx[i + 1]) / 2, ym, (bold ? 0.9 : 0.85) * th, cells[i], 0, 1, 2);
                }
            }
            if (bold) w.Polyline(Lay.KhungTen, new List<(double X, double Y)> { (0, y2), (W, y2) }, false, r.Kind == "total" ? 0.5 : 0.3);
            else w.Line(Lay.KhungTen, 0, y2, W, y2);
            yr = y2;
        }
        for (int i = 0; i <= ColW.Length; i++)
            w.Polyline(Lay.KhungTen, new List<(double X, double Y)> { (cx[i], 0), (cx[i], yr) }, false, i == 0 || i == ColW.Length ? 0.5 : 0);
        w.Polyline(Lay.KhungTen, new List<(double X, double Y)> { (0, yr), (W, yr) }, false, 0.5);
        return w;
    }

    private static DxfWriter Summary(DetailingProject p, List<(IDetailMember Member, DetailResult Result)> results)
    {
        var w = new DxfWriter(1);
        double th = w.ScheduleTh, rowH = Math.Max(7, 2.6 * th);
        double[] cw = { 10, 92, 16, 24 };
        var cx = new double[cw.Length + 1];
        for (int i = 0; i < cw.Length; i++) cx[i + 1] = cx[i] + cw[i];
        double W = cx[^1];
        w.Text(Lay.Chu, W / 2, 3, 1.5 * th, "TỔNG HỢP VẬT LIỆU", 0, 1, 0);
        var heads = new[] { "STT", "VẬT LIỆU / CÔNG TÁC", "ĐƠN VỊ", "KHỐI LƯỢNG" };
        double yh = -rowH * 1.4;
        w.Polyline(Lay.KhungTen, new List<(double X, double Y)> { (0, 0), (W, 0) }, false, 0.5);
        w.Line(Lay.KhungTen, 0, yh, W, yh);
        for (int i = 0; i < heads.Length; i++) w.Text(Lay.Chu, (cx[i] + cx[i + 1]) / 2, yh / 2, 0.85 * th, heads[i], 0, 1, 2);
        // gộp theo vật liệu (không theo nhóm cấu kiện)
        var q = DetailingExport.Quantities(p, results);
        var items = new List<(string Item, string Unit, double Qty)>();
        void Add(string item, string unit, double v) { if (v > 1e-9) items.Add((item, unit, v)); }
        foreach (var g in results.Where(x => x.Result != null).GroupBy(x => string.IsNullOrEmpty(x.Result.ConcreteClass) ? p.Settings.Concrete : x.Result.ConcreteClass))
            Add("Bê tông " + g.Key, "m³", g.Sum(x => x.Result.ConcreteVolume * Math.Max(1, x.Result.Quantity)));
        Add("Bê tông lót " + p.Settings.LeanConcrete + " dày " + Dt.Mm(p.Settings.LeanThickness) + " mm", "m³", q.Where(x => x.Item.StartsWith("Bê tông lót")).Sum(x => x.Qty));
        Add("Ván khuôn", "m²", q.Where(x => x.Item.StartsWith("Ván khuôn")).Sum(x => x.Qty));
        Add("Cốt thép Ø ≤ 10", "kg", q.Where(x => x.Unit == "kg" && x.Item.StartsWith("Cốt thép") && x.Item.EndsWith("Ø ≤ 10")).Sum(x => x.Qty));
        Add("Cốt thép 10 < Ø ≤ 18", "kg", q.Where(x => x.Unit == "kg" && x.Item.StartsWith("Cốt thép") && x.Item.EndsWith("10 < Ø ≤ 18")).Sum(x => x.Qty));
        Add("Cốt thép Ø > 18", "kg", q.Where(x => x.Unit == "kg" && x.Item.StartsWith("Cốt thép") && x.Item.EndsWith("Ø > 18")).Sum(x => x.Qty));
        Add("Cáp dự ứng lực", "kg", q.Where(x => x.Item == "Cáp dự ứng lực").Sum(x => x.Qty));
        foreach (var ex in q.Where(x => !x.Item.StartsWith("Bê tông") && !x.Item.StartsWith("Ván khuôn") && !x.Item.StartsWith("Cốt thép") && x.Item != "Cáp dự ứng lực")
                     .GroupBy(x => (x.Item, x.Unit)))
            Add(ex.Key.Item, ex.Key.Unit, ex.Sum(x => x.Qty));
        double yr = yh;
        int no = 1;
        foreach (var (item, unit, v) in items)
        {
            double y2 = yr - rowH, ym = (yr + y2) / 2;
            w.Text(Lay.Chu, (cx[0] + cx[1]) / 2, ym, 0.85 * th, (no++).ToString(Inv), 0, 1, 2);
            w.Text(Lay.Chu, cx[1] + 1.5, ym, 0.85 * th, item, 0, 0, 2);
            w.Text(Lay.Chu, (cx[2] + cx[3]) / 2, ym, 0.85 * th, unit, 0, 1, 2);
            w.Text(Lay.Chu, (cx[3] + cx[4]) / 2, ym, 0.85 * th, F(v, unit == "kg" ? "0.0" : unit == "m³" ? "0.000" : "0.##"), 0, 1, 2);
            w.Line(Lay.KhungTen, 0, y2, W, y2);
            yr = y2;
        }
        for (int i = 0; i <= cw.Length; i++)
            w.Polyline(Lay.KhungTen, new List<(double X, double Y)> { (cx[i], 0), (cx[i], yr) }, false, i == 0 || i == cw.Length ? 0.5 : 0);
        w.Polyline(Lay.KhungTen, new List<(double X, double Y)> { (0, yr), (W, yr) }, false, 0.5);
        return w;
    }

    private static void Wrap(DxfWriter w, double cx, double cy, double width, string text, double h)
    {
        var words = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        string cur = "";
        foreach (var word in words)
        {
            string t = cur.Length == 0 ? word : cur + " " + word;
            if (DxfWriter.TextWidth(t, h) * w.WidthFactor > width && cur.Length > 0) { lines.Add(cur); cur = word; }
            else cur = t;
        }
        if (cur.Length > 0) lines.Add(cur);
        double lh = 1.45 * h, y0 = cy + (lines.Count - 1) * lh / 2;
        for (int i = 0; i < lines.Count; i++) w.Text(Lay.Chu, cx, y0 - i * lh, h, lines[i], 0, 1, 2);
    }
}
