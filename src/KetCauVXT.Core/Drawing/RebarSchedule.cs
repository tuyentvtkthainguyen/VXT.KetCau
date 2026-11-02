using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

/// <summary>Một hàng của BẢNG THỐNG KÊ CỐT THÉP (dùng chung cho DXF, Excel, Word).</summary>
public class ScheduleRow
{
    public string Member { get; set; } = "";
    /// <summary>Loại cấu kiện: "Dầm", "Cột", "Sàn", "Móng", "Đài cọc", "Móng băng"</summary>
    public string Category { get; set; } = "";
    public string Story { get; set; } = "";
    public int Mark { get; set; }
    public string Role { get; set; } = "";
    /// <summary>Hình dạng: "Thẳng", "L", "U", "Đai", "Đai C"</summary>
    public string Shape { get; set; } = "";
    /// <summary>Mô tả hình dạng & kích thước, ví dụ "L: 300+5600" hoặc "Đai 170x420".</summary>
    public string ShapeText { get; set; } = "";
    public List<double> Segments { get; set; } = new();
    public int Dia { get; set; }
    public double LengthMm { get; set; }
    public int MemberCount { get; set; }
    public int BarsPerMember { get; set; }
    public int BarsTotal { get; set; }
    public double TotalLengthM { get; set; }
    public double WeightKg { get; set; }
}

/// <summary>Tổng hợp theo đường kính.</summary>
public class DiameterSummary
{
    public int Dia { get; set; }
    public double LengthM { get; set; }
    public double WeightKg { get; set; }
}

/// <summary>Tổng hợp theo nhóm Ø≤10, 10&lt;Ø≤18, Ø&gt;18.</summary>
public class GroupSummary
{
    public double WeightLe10 { get; set; }
    public double Weight10To18 { get; set; }
    public double WeightGt18 { get; set; }
    public double Total => WeightLe10 + Weight10To18 + WeightGt18;
}

public static class RebarSchedule
{
    public const double SheetScale = 1; // bảng vẽ ở tỷ lệ 1/1 trên tờ (mm giấy) → chữ cao 2,5

    public static double UnitWeight(int dia) => 0.00617 * dia * dia;

    // ------------------------------------------------------------------
    // Dữ liệu
    // ------------------------------------------------------------------
    /// <summary>Lập danh sách hàng thống kê từ toàn bộ cấu kiện (dầm, cột, sàn, móng đơn, đài cọc, móng băng; theo thứ tự đó).</summary>
    public static List<ScheduleRow> BuildRows(DesignSummary s)
    {
        var rows = new List<ScheduleRow>();
        if (s == null) return rows;
        void AddMember(MemberDesignBase m, string cat)
        {
            int q = Math.Max(1, m.Quantity);
            // các BarMark cùng số hiệu (thanh đối xứng: mũ gối trái – phải, mũ sàn các cạnh…) gộp thành MỘT hàng, số thanh cộng dồn
            // (thanh chưa đánh số – Mark = 0 – giữ riêng từng hàng)
            foreach (var gm in m.Bars.Where(x => x.Count > 0 && x.Length > 0 && x.Dia > 0).Select((x, i) => (Bar: x, Key: x.Mark > 0 ? x.Mark : -1 - i))
                         .GroupBy(t => t.Key).OrderBy(t => t.Key))
            {
                var b = gm.First().Bar;
                int cnt = gm.Sum(t => t.Bar.Count);
                double lmm = Math.Round(b.Length * 100) * 10;
                int tot = cnt * q;
                double tl = tot * lmm / 1000.0;
                rows.Add(new ScheduleRow
                {
                    Member = m.Name, Category = cat, Story = m.Story, Mark = b.Mark, Role = b.Role, Shape = b.Shape,
                    ShapeText = ShapeText(b), Segments = b.Segments?.ToList() ?? new List<double>(),
                    Dia = b.Dia, LengthMm = lmm, MemberCount = q, BarsPerMember = cnt, BarsTotal = tot,
                    TotalLengthM = Math.Round(tl, 2), WeightKg = Math.Round(UnitWeight(b.Dia) * tl, 2)
                });
            }
        }
        foreach (var m in s.Beams) AddMember(m, "Dầm");
        foreach (var m in s.Columns) AddMember(m, "Cột");
        foreach (var m in s.Slabs) AddMember(m, "Sàn");
        foreach (var m in s.Footings) AddMember(m, "Móng");
        foreach (var m in s.PileCaps) AddMember(m, "Đài cọc");
        foreach (var m in s.StripFootings) AddMember(m, "Móng băng");
        StairSchedule.AppendTo(rows, s);   // cầu thang (Drawing/StairDrawing.cs)
        return rows;
    }

    public static string ShapeText(BarMark b)
    {
        var seg = b.Segments ?? new List<double>();
        string sh = b.Shape ?? "Thẳng";
        if (sh.StartsWith("Đai"))
        {
            if (seg.Count >= 2) return $"{sh} {DxfWriter.N0(seg[0])}x{DxfWriter.N0(seg[1])}";
            return sh;
        }
        if (seg.Count == 0) return $"{sh}: {DxfWriter.N0(b.Length * 1000)}";
        return $"{sh}: " + string.Join("+", seg.Select(DxfWriter.N0));
    }

    public static List<DiameterSummary> ByDiameter(IEnumerable<ScheduleRow> rows) =>
        rows.GroupBy(r => r.Dia).OrderBy(g => g.Key).Select(g => new DiameterSummary
        {
            Dia = g.Key, LengthM = Math.Round(g.Sum(r => r.TotalLengthM), 2), WeightKg = Math.Round(g.Sum(r => r.WeightKg), 2)
        }).ToList();

    public static GroupSummary Groups(IEnumerable<ScheduleRow> rows)
    {
        var g = new GroupSummary();
        foreach (var r in rows)
        {
            if (r.Dia <= 10) g.WeightLe10 += r.WeightKg;
            else if (r.Dia <= 18) g.Weight10To18 += r.WeightKg;
            else g.WeightGt18 += r.WeightKg;
        }
        g.WeightLe10 = Math.Round(g.WeightLe10, 2);
        g.Weight10To18 = Math.Round(g.Weight10To18, 2);
        g.WeightGt18 = Math.Round(g.WeightGt18, 2);
        return g;
    }

    // ------------------------------------------------------------------
    // Vẽ
    // ------------------------------------------------------------------
    private static readonly string[] Heads =
    {
        "TÊN CẤU KIỆN", "SỐ HIỆU", "HÌNH DẠNG & KÍCH THƯỚC (mm)", "Ø (mm)", "CHIỀU DÀI 1 THANH (mm)",
        "SỐ C.KIỆN", "SỐ THANH", "TỔNG CHIỀU DÀI (m)", "TỔNG TRỌNG LƯỢNG (kg)"
    };
    // bề rộng cột (mm giấy)
    private static readonly double[] ColW = { 28, 14, 62, 13, 22, 15, 30, 22, 24 };

    /// <summary>Vẽ bảng thống kê thành nhiều mảnh (mỗi mảnh ≤ maxRows hàng) + bảng tổng hợp.
    /// S: mẫu số tỷ lệ của tờ (bảng vẽ theo mm giấy × S).</summary>
    public static List<Figure> BuildFigures(DesignSummary s, double S = 1, int maxRows = 30)
    {
        var rows = BuildRows(s);
        var figs = new List<Figure>();
        int n = rows.Count;
        int parts = Math.Max(1, (int)Math.Ceiling(n / (double)maxRows));
        for (int i = 0; i < parts; i++)
        {
            var part = rows.Skip(i * maxRows).Take(maxRows).ToList();
            figs.Add(new Figure(Table(part, S, i == 0 ? "BẢNG THỐNG KÊ CỐT THÉP" :
                $"BẢNG THỐNG KÊ CỐT THÉP (tiếp {i + 1})"), "BTK" + (i + 1), i == 0));
        }
        figs.Add(new Figure(Summary(rows, S), "TH", false));
        return figs;
    }

    private static DxfWriter Table(List<ScheduleRow> rows, double S, string title)
    {
        var w = new DxfWriter(S);
        double th = w.ScheduleTh, u = S;   // cỡ chữ bảng thống kê (Thiết lập bản vẽ, mặc định 2,5 mm)
        double rowH = 14 * u, headH = 16 * u;
        double[] cx = new double[ColW.Length + 1];
        for (int i = 0; i < ColW.Length; i++) cx[i + 1] = cx[i] + ColW[i] * u;
        double W = cx[^1];
        double y = 0;
        w.Text(Lay.Chu, W / 2, y + 3 * u, 1.6 * th, title, 0, 1, 0);
        // tiêu đề cột
        double yh = y - headH;
        w.Line(Lay.KhungTen, 0, y, W, y);
        w.Line(Lay.KhungTen, 0, yh, W, yh);
        for (int i = 0; i < Heads.Length; i++)
        {
            double cxm = (cx[i] + cx[i + 1]) / 2;
            if (i == 6)
            {
                double ym = y - headH / 2;
                w.Line(Lay.KhungTen, cx[i], ym, cx[i + 1], ym);
                w.Text(Lay.Chu, cxm, ym + headH / 4, 0.85 * th, "SỐ THANH", 0, 1, 2);
                double cm = (cx[i] + cx[i + 1]) / 2;
                w.Line(Lay.KhungTen, cm, ym, cm, yh);
                w.Text(Lay.Chu, (cx[i] + cm) / 2, yh + headH / 4, 0.8 * th, "MỘT CK", 0, 1, 2);
                w.Text(Lay.Chu, (cm + cx[i + 1]) / 2, yh + headH / 4, 0.8 * th, "TOÀN BỘ", 0, 1, 2);
            }
            else WrapText(w, cxm, y - headH / 2, ColW[i] * u, Heads[i], 0.8 * th);
        }
        // hàng
        double yr = yh;
        int start = 0;
        string curMember = null;
        double memberTop = yh;
        var memberSpans = new List<(string Name, double Y1, double Y2)>();
        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            string key = row.Category + "|" + row.Member + "|" + row.Story;
            if (key != curMember)
            {
                if (curMember != null) memberSpans.Add((rows[start].Member, yr, memberTop));
                curMember = key; start = r; memberTop = yr;
            }
            double y2 = yr - rowH;
            w.Line(Lay.KhungTen, cx[1], y2, W, y2);
            double ym = (yr + y2) / 2;
            w.MarkCircle((cx[1] + cx[2]) / 2, ym, row.Mark);
            DrawShape(w, row, cx[2] + 3 * u, cx[3] - 3 * u, y2 + 2 * u, yr - 2 * u);
            w.Text(Lay.Chu, (cx[3] + cx[4]) / 2, ym, th, row.Dia.ToString(DrawUtil.Inv), 0, 1, 2);
            w.Text(Lay.Chu, (cx[4] + cx[5]) / 2, ym, th, DxfWriter.N0(row.LengthMm), 0, 1, 2);
            w.Text(Lay.Chu, (cx[5] + cx[6]) / 2, ym, th, row.MemberCount.ToString(DrawUtil.Inv), 0, 1, 2);
            double cm = (cx[6] + cx[7]) / 2;
            w.Text(Lay.Chu, (cx[6] + cm) / 2, ym, th, row.BarsPerMember.ToString(DrawUtil.Inv), 0, 1, 2);
            w.Text(Lay.Chu, (cm + cx[7]) / 2, ym, th, row.BarsTotal.ToString(DrawUtil.Inv), 0, 1, 2);
            w.Text(Lay.Chu, (cx[7] + cx[8]) / 2, ym, th, row.TotalLengthM.ToString("0.00", DrawUtil.Inv), 0, 1, 2);
            w.Text(Lay.Chu, (cx[8] + cx[9]) / 2, ym, th, row.WeightKg.ToString("0.00", DrawUtil.Inv), 0, 1, 2);
            yr = y2;
        }
        if (curMember != null) memberSpans.Add((rows[start].Member, yr, memberTop));
        foreach (var (name, y1, y2m) in memberSpans)
        {
            w.Line(Lay.KhungTen, 0, y1, cx[1], y1);
            double ym = (y1 + y2m) / 2;
            if (y2m - y1 >= DxfWriter.TextWidth(name, th) + 2 * u)
                w.Text(Lay.Chu, (cx[0] + cx[1]) / 2, ym, th, name, 90, 1, 2);
            else WrapText(w, (cx[0] + cx[1]) / 2, ym, ColW[0] * u, name, th);
        }
        // tổng trọng lượng bảng
        double yt = yr - rowH;
        w.Line(Lay.KhungTen, 0, yt, W, yt);
        w.Text(Lay.Chu, cx[1] + 2 * u, (yr + yt) / 2, th, "TỔNG CỘNG", 0, 0, 2);
        w.Text(Lay.Chu, (cx[8] + cx[9]) / 2, (yr + yt) / 2, th, rows.Sum(r => r.WeightKg).ToString("0.00", DrawUtil.Inv), 0, 1, 2);
        // đường dọc
        for (int i = 0; i < cx.Length; i++)
        {
            bool inner = i > 0 && i < cx.Length - 1;
            if (i == 7 || i == 8 || i == 9 || i == 0 || !inner) w.Line(Lay.KhungTen, cx[i], y, cx[i], yt);
            else w.Line(Lay.KhungTen, cx[i], y, cx[i], yr);
        }
        double cmid = (cx[6] + cx[7]) / 2;
        w.Line(Lay.KhungTen, cmid, yh, cmid, yr);
        return w;
    }

    private static void WrapText(DxfWriter w, double cx, double cy, double width, string text, double h)
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
        double lh = 1.35 * h;
        double y0 = cy + (lines.Count - 1) * lh / 2;
        for (int i = 0; i < lines.Count; i++) w.Text(Lay.Chu, cx, y0 - i * lh, h, lines[i], 0, 1, 2);
    }

    /// <summary>Vẽ phác hình dạng thanh trong ô (x1..x2, y1..y2) và ghi chiều dài từng đoạn.</summary>
    private static void DrawShape(DxfWriter w, ScheduleRow r, double x1, double x2, double y1, double y2)
    {
        double th = 0.85 * w.ScheduleTh;
        var seg = r.Segments ?? new List<double>();
        string sh = r.Shape ?? "";
        double xm = (x1 + x2) / 2, ym = (y1 + y2) / 2, hh = y2 - y1;
        if (sh.StartsWith("Đai"))
        {
            double a = seg.Count > 0 ? seg[0] : 200, b = seg.Count > 1 ? seg[1] : 300;
            double bw = Math.Min((x2 - x1) * 0.35, hh * 1.2), bh = hh * 0.7;
            if (a > b) bw = Math.Min((x2 - x1) * 0.45, bw * 1.3);
            double bx1 = xm - bw / 2, by1 = ym - bh / 2;
            if (sh == "Đai C")
            {
                // đai C: thanh thẳng 2 móc
                w.Line(Lay.Thep, bx1, ym, bx1 + bw, ym);
                w.Line(Lay.Thep, bx1, ym, bx1 + 0.25 * bh, ym - 0.35 * bh);
                w.Line(Lay.Thep, bx1 + bw, ym, bx1 + bw - 0.25 * bh, ym - 0.35 * bh);
                w.Text(Lay.Chu, xm, ym + 0.3 * th, th, DxfWriter.N0(a), 0, 1, 1);
                return;
            }
            w.Rect(Lay.Thep, bx1, by1, bx1 + bw, by1 + bh);
            double hk = 0.3 * bh;
            w.Line(Lay.Thep, bx1, by1 + bh - 0.15 * hk, bx1 + 0.7 * hk, by1 + bh - 0.85 * hk);
            w.Line(Lay.Thep, bx1 + 0.15 * hk, by1 + bh, bx1 + 0.85 * hk, by1 + bh - 0.7 * hk);
            w.Text(Lay.Chu, xm, by1 - 0.3 * th, th, DxfWriter.N0(a), 0, 1, 3);
            w.Text(Lay.Chu, bx1 + bw + 0.4 * th, ym, th, DxfWriter.N0(b), 0, 0, 2);
            return;
        }
        double yb = ym - 0.15 * hh;
        double hk2 = hh * 0.45;
        if (sh.StartsWith("U") && seg.Count >= 3)
        {
            double xa = x1 + 0.12 * (x2 - x1), xb = x2 - 0.12 * (x2 - x1);
            w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xa, yb + hk2), (xa, yb), (xb, yb), (xb, yb + hk2) }, false);
            w.Text(Lay.Chu, xm, yb + 0.3 * th, th, DxfWriter.N0(seg[1]), 0, 1, 1);
            w.Text(Lay.Chu, xa - 0.3 * th, yb + hk2 / 2, th, DxfWriter.N0(seg[0]), 0, 2, 2);
            w.Text(Lay.Chu, xb + 0.3 * th, yb + hk2 / 2, th, DxfWriter.N0(seg[seg.Count - 1]), 0, 0, 2);
            return;
        }
        if (sh.StartsWith("L") && seg.Count >= 2)
        {
            bool hookLeft = seg[0] <= seg[seg.Count - 1];
            double hook = hookLeft ? seg[0] : seg[seg.Count - 1], main = hookLeft ? seg[seg.Count - 1] : seg[0];
            double xa = x1 + 0.12 * (x2 - x1), xb = x2 - 0.04 * (x2 - x1);
            if (hookLeft)
            {
                w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xa, yb + hk2), (xa, yb), (xb, yb) }, false);
                w.Text(Lay.Chu, xa - 0.3 * th, yb + hk2 / 2, th, DxfWriter.N0(hook), 0, 2, 2);
            }
            else
            {
                xa = x1 + 0.04 * (x2 - x1); xb = x2 - 0.12 * (x2 - x1);
                w.Polyline(Lay.Thep, new List<(double X, double Y)> { (xa, yb), (xb, yb), (xb, yb + hk2) }, false);
                w.Text(Lay.Chu, xb + 0.3 * th, yb + hk2 / 2, th, DxfWriter.N0(hook), 0, 0, 2);
            }
            w.Text(Lay.Chu, (xa + xb) / 2, yb + 0.3 * th, th, DxfWriter.N0(main), 0, 1, 1);
            return;
        }
        double len = seg.Count == 1 ? seg[0] : (seg.Count > 1 ? seg.Sum() : r.LengthMm);
        w.Line(Lay.Thep, x1 + 0.05 * (x2 - x1), yb, x2 - 0.05 * (x2 - x1), yb);
        w.Text(Lay.Chu, xm, yb + 0.3 * th, th, DxfWriter.N0(len), 0, 1, 1);
    }

    private static DxfWriter Summary(List<ScheduleRow> rows, double S)
    {
        var w = new DxfWriter(S);
        double th = w.ScheduleTh, u = S;   // cỡ chữ bảng thống kê (Thiết lập bản vẽ, mặc định 2,5 mm)
        var byDia = ByDiameter(rows);
        var g = Groups(rows);
        double c0 = 40 * u, cw = 22 * u, rh = 10 * u;
        int nc = Math.Max(1, byDia.Count);
        double W = c0 + nc * cw;
        w.Text(Lay.Chu, W / 2, 3 * u, 1.6 * th, "TỔNG HỢP CỐT THÉP", 0, 1, 0);
        string[] labels = { "Đường kính (mm)", "Chiều dài (m)", "Trọng lượng (kg)" };
        for (int r = 0; r <= 3; r++) w.Line(Lay.KhungTen, 0, -r * rh, W, -r * rh);
        w.Line(Lay.KhungTen, 0, 0, 0, -3 * rh);
        for (int i = 0; i <= nc; i++) w.Line(Lay.KhungTen, c0 + i * cw, 0, c0 + i * cw, -3 * rh);
        for (int r = 0; r < 3; r++) w.Text(Lay.Chu, 2 * u, -(r + 0.5) * rh, th, labels[r], 0, 0, 2);
        for (int i = 0; i < byDia.Count; i++)
        {
            double xm = c0 + (i + 0.5) * cw;
            w.Text(Lay.Chu, xm, -0.5 * rh, th, "Ø" + byDia[i].Dia, 0, 1, 2);
            w.Text(Lay.Chu, xm, -1.5 * rh, th, byDia[i].LengthM.ToString("0.00", DrawUtil.Inv), 0, 1, 2);
            w.Text(Lay.Chu, xm, -2.5 * rh, th, byDia[i].WeightKg.ToString("0.00", DrawUtil.Inv), 0, 1, 2);
        }
        // nhóm
        double y0 = -3 * rh - 6 * u;
        string[] gl = { "NHÓM Ø≤10 (kg)", "NHÓM 10<Ø≤18 (kg)", "NHÓM Ø>18 (kg)", "TỔNG CỘNG (kg)" };
        double[] gv = { g.WeightLe10, g.Weight10To18, g.WeightGt18, g.Total };
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
        return w;
    }
}
