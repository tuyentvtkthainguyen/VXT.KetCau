using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Detailing;

// =====================================================================
// SHOP DRAWING THÉP DẦM, SÀN (bản vẽ gia công – cắt thép)
//  • Mỗi số hiệu thép vẽ tách riêng đúng hình dạng uốn, ghi kích thước từng đoạn (kích thước NGOÀI, mm).
//  • Chiều dài cắt L_cắt = Σ đoạn (kích thước ngoài) − độ giãn khi uốn: 2d mỗi góc 90°, 3d mỗi móc 135° (xấp xỉ BS 8666 / thực hành gia công),
//    làm tròn 10 mm. Thanh thẳng: L_cắt = L.
//  • Cắt từ cây thép thương phẩm (DetailSettings.StockLength, mặc định 11,7 m):
//      L_cắt ≤ cây: số đoạn / cây = ⌊cây / L_cắt⌋, số cây = ⌈n / (đoạn/cây)⌉;
//      L_cắt > cây: nối chồng ll (TCVN 5574:2018 – RebarDetailing.LapLength, vùng kéo α = 1,2):
//        số đoạn np = ⌈(L − cây)/(cây − ll)⌉ + 1, tổng dài L + (np − 1)·ll, (np − 1) đoạn cả cây + 1 đoạn cuối; hao hụt = 1 − Σ dùng / Σ cây.
//  • Thép chạy suốt của dầm: vị trí nối chọn tự động trong VÙNG CHO PHÉP (BarCutCheck.SpliceDanger): thép dưới gần gối (ngoài 1/3 giữa nhịp),
//    thép trên trong 1/3 giữa nhịp; một nửa số thanh nối so le ≥ 1,3·ll (≤ 50 % số thanh tại một tiết diện). Người dùng nhập vị trí nối → dùng vị trí đó.
// =====================================================================

/// <summary>Một dòng bảng gia công – cắt thép.</summary>
public sealed class ShopRow
{
    public string Member { get; set; } = "";
    public int Mark { get; set; }
    public string Sub { get; set; } = "";
    public string Role { get; set; } = "";
    public int Dia { get; set; }
    public string Shape { get; set; } = "Thẳng";
    /// <summary>Kích thước ngoài từng đoạn (mm).</summary>
    public List<double> Segments { get; set; } = new();
    /// <summary>Chiều dài theo bản vẽ (m) = Σ đoạn.</summary>
    public double Length { get; set; }
    /// <summary>Trừ độ giãn khi uốn (m).</summary>
    public double Deduct { get; set; }
    /// <summary>Chiều dài cắt 1 đoạn (m).</summary>
    public double CutLength { get; set; }
    public int PerMember { get; set; }
    public int Members { get; set; } = 1;
    public int Total => PerMember * Math.Max(Members, 1);
    public int StockBars { get; set; }
    public double WastePct { get; set; }
    public string Note { get; set; } = "";
    public double WeightKg => 0.00617 * Dia * Dia * CutLength * Total;
    public string ShapeText => Segments.Count == 0 ? Shape : Shape + " " + string.Join("-", Segments.Select(v => Math.Round(v).ToString("0", Dt.Inv)));
}

public static class ShopDrawing
{
    /// <summary>Độ giãn khi uốn (m): 2d mỗi góc 90°, đai kín 3 góc 90° + 2 móc 135° (3d) = 12d.</summary>
    public static double BendDeduction(BarMark b)
    {
        if (b == null) return 0;
        double d = b.Dia / 1000.0;
        if ((b.Shape ?? "").StartsWith("Đai")) return 12 * d;
        int bends = b.Shape switch { "Thẳng" => 0, "L" => 1, "U" => 2, _ => Math.Max((b.Segments?.Count ?? 1) - 1, 0) };
        return bends * 2 * d;
    }

    /// <summary>Chiều dài cắt (m) = L − trừ uốn, làm tròn 10 mm.</summary>
    public static double CutLength(BarMark b) => b == null ? 0 : Dt.R10(Math.Max(b.Length - BendDeduction(b), 0));

    /// <summary>Kế hoạch cắt từ cây thép.</summary>
    public sealed record StockPlan(int Pieces, List<double> PieceLengths, int StockBars, double WastePct, double Lap);

    /// <summary>Cắt n thanh dài cutLen (m) từ cây stock (m), nối chồng lap (m) khi cutLen &gt; stock.</summary>
    public static StockPlan PlanStock(double cutLen, int count, double stock, double lap)
    {
        stock = stock > 0.5 ? stock : 11.7;
        count = Math.Max(count, 0);
        if (cutLen <= 1e-6 || count == 0) return new StockPlan(1, new List<double> { cutLen }, 0, 0, 0);
        if (cutLen <= stock + 1e-9)
        {
            int per = Math.Max((int)Math.Floor(stock / cutLen + 1e-9), 1);
            int bars = (int)Math.Ceiling(count / (double)per - 1e-9);
            double waste = bars > 0 ? 1 - count * cutLen / (bars * stock) : 0;
            return new StockPlan(1, new List<double> { cutLen }, bars, Math.Round(waste * 100, 1), 0);
        }
        lap = Math.Min(Math.Max(lap, 0), stock / 2);
        int np = (int)Math.Ceiling((cutLen - stock) / (stock - lap) - 1e-9) + 1;
        double total = cutLen + (np - 1) * lap;
        double last = Dt.R10(total - (np - 1) * stock);
        var pieces = Enumerable.Repeat(stock, np - 1).ToList(); pieces.Add(last);
        int perLast = Math.Max((int)Math.Floor(stock / Math.Max(last, 0.01) + 1e-9), 1);
        int barsT = count * (np - 1) + (int)Math.Ceiling(count / (double)perLast - 1e-9);
        double wst = barsT > 0 ? 1 - count * total / (barsT * stock) : 0;
        return new StockPlan(np, pieces, barsT, Math.Round(wst * 100, 1), lap);
    }

    /// <summary>Vị trí nối (m dọc dầm) cho thanh chạy suốt từ x0 tới x1: chọn điểm XA NHẤT trong vùng cho phép mà đoạn thép (kể ll/2 + chân neo) ≤ cây.</summary>
    public static List<double> AutoSplices(BarCutCheck.Geo g, double x0, double x1, bool top, double stock, double lap, bool seismic, double legStart = 0, double legEnd = 0)
    {
        var res = new List<double>();
        if (g == null || x1 - x0 + legStart + legEnd <= stock + 1e-9) return res;
        double start = x0, used = legStart;
        for (int guard = 0; guard < 50 && x1 - start + used + legEnd > stock + 1e-9; guard++)
        {
            double xmax = start + stock - used - lap / 2, xmin = start + lap / 2 + 0.05;
            double pick = double.NaN;
            for (double x = Math.Floor(xmax * 20) / 20; x >= xmin; x -= 0.05)
                if (BarCutCheck.SpliceDanger(g, x, top, seismic).Length == 0) { pick = Math.Round(x, 2); break; }
            if (double.IsNaN(pick)) pick = Math.Round(xmax, 2);   // không có vùng cho phép đủ gần – nối tại giới hạn cây (cảnh báo ở BarCutCheck khi người dùng nhập)
            res.Add(pick);
            start = pick - lap / 2; used = 0;
        }
        return res;
    }

    /// <summary>Đoạn thép (m) của thanh chạy suốt [x0, x1] có chân neo legA / legB, nối tại splices (ll).</summary>
    public static List<double> Pieces(double x0, double x1, double legA, double legB, List<double> splices, double lap)
    {
        var pts = new List<double> { x0 };
        pts.AddRange(splices.OrderBy(v => v));
        pts.Add(x1);
        var res = new List<double>();
        for (int i = 1; i < pts.Count; i++)
        {
            double a = i == 1 ? pts[0] : pts[i - 1] - lap / 2, b = i == pts.Count - 1 ? pts[^1] : pts[i] + lap / 2;
            res.Add(Dt.R10(b - a + (i == 1 ? legA : 0) + (i == pts.Count - 1 ? legB : 0)));
        }
        return res;
    }

    private static (Material Conc, Material Main, Material Small) Mats(DetailSettings s)
    {
        s ??= new DetailSettings();
        Material c, m, sm;
        try { c = Material.Concrete(s.Concrete); } catch (ArgumentException) { c = Material.Concrete("B25"); }
        try { m = Material.Rebar(s.RebarMain); } catch (ArgumentException) { m = Material.Rebar("CB400-V"); }
        try { sm = Material.Rebar(s.RebarSmall); } catch (ArgumentException) { sm = Material.Rebar("CB240-T"); }
        return (c, m, sm);
    }

    public static double Lap(DetailSettings s, int dia)
    {
        var (c, m, sm) = Mats(s);
        return RebarDetailing.LapLength(c, dia <= 10 ? sm : m, dia);
    }

    /// <summary>Thông tin nối của thanh chạy suốt dầm (để vẽ): (số hiệu, vị trí nối nhóm A, nhóm B so le).</summary>
    public sealed record SpliceInfo(BarMark Bar, List<double> A, List<double> B, double Lap);

    /// <summary>Vị trí nối các thanh chạy suốt của dầm nhập tay (vị trí người dùng nhập, hoặc tự chọn trong vùng cho phép).</summary>
    public static List<SpliceInfo> BeamSplices(ManualBeam m, DetailResult r, DetailSettings s)
    {
        var res = new List<SpliceInfo>();
        if (m == null || r == null) return res;
        s ??= new DetailSettings();
        var g = BarCutCheck.GeoOf(m);
        bool seismic = (m.Seismic ?? "").StartsWith("Có");
        foreach (var b in r.Bars.Where(x => x.Role == "Dưới" || x.Role == "Trên"))
        {
            bool top = b.IsTop;
            double lap = Lap(s, b.Dia);
            var (legA, legB) = Legs(b);
            var user = BarCutCheck.ParseKeep(top ? m.TopSplices : m.BotSplices).Where(v => v > 0).OrderBy(v => v).ToList();
            var a = user.Count > 0 ? user : AutoSplices(g, b.Start, b.End, top, s.StockLength, lap, seismic, legA, legB);
            // nhóm B: so le ≥ 1,3·ll (thử dời về phía đầu trước – đoạn đầu ngắn lại), chỉ nhận khi vẫn ngoài vùng nguy hiểm và mọi đoạn ≤ cây
            var bb = new List<double>();
            double stag = RebarDetailing.StaggerDistance(lap);
            for (int i = 0; i < a.Count; i++)
            {
                double x = a[i], pick = x;
                foreach (double cand in new[] { x - stag, x + stag })
                {
                    if (cand <= b.Start + lap || cand >= b.End - lap || BarCutCheck.SpliceDanger(g, cand, top, seismic).Length > 0) continue;
                    var trial = new List<double>(bb) { cand };
                    trial.AddRange(a.Skip(i + 1));
                    if (Pieces(b.Start, b.End, legA, legB, trial, lap).All(v => v <= s.StockLength + 1e-6)) { pick = Math.Round(cand, 2); break; }
                }
                bb.Add(pick);
            }
            res.Add(new SpliceInfo(b, a, bb, lap));
        }
        return res;
    }

    private static (double A, double B) Legs(BarMark b)
    {
        var sg = b.Segments ?? new List<double>();
        if (b.Shape == "U" && sg.Count >= 3) return (sg[0] / 1000, sg[2] / 1000);
        if (b.Shape == "L" && sg.Count >= 2) return (0, sg[1] / 1000);
        return (0, 0);
    }

    /// <summary>Bảng gia công – cắt thép của một cấu kiện (dầm: thanh chạy suốt quá dài tách đoạn theo vị trí nối; sàn / cấu kiện khác: theo cây).</summary>
    public static List<ShopRow> Rows(IDetailMember m, DetailResult r, DetailSettings s)
    {
        var rows = new List<ShopRow>();
        if (r == null) return rows;
        s ??= new DetailSettings();
        int members = Math.Max(1, r.Quantity);
        var spl = m is ManualBeam mb ? BeamSplices(mb, r, s) : new List<SpliceInfo>();
        foreach (var b in r.Bars.OrderBy(x => x.Mark))
        {
            var si = spl.FirstOrDefault(x => ReferenceEquals(x.Bar, b));
            double lap = Lap(s, b.Dia);
            if (si != null && si.A.Count > 0)
            {
                // nhóm A (½ số thanh) và nhóm B (so le) – các đoạn cắt theo vị trí nối
                int nA = (b.Count + 1) / 2, nB = b.Count - nA;
                var (legA, legB) = Legs(b);
                foreach (var (list, n, tag) in new[] { (si.A, nA, "A"), (si.B, nB, "B") })
                {
                    if (n <= 0) continue;
                    var pcs = Pieces(b.Start, b.End, legA, legB, list, si.Lap);
                    for (int i = 0; i < pcs.Count; i++)
                    {
                        bool first = i == 0, last = i == pcs.Count - 1;
                        int bends = (first && legA > 0 ? 1 : 0) + (last && legB > 0 ? 1 : 0);
                        double ded = bends * 2 * b.Dia / 1000.0;
                        var segs = new List<double>();
                        if (first && legA > 0) segs.Add(Math.Round(legA * 1000));
                        segs.Add(Math.Round((pcs[i] - (first ? legA : 0) - (last ? legB : 0)) * 1000));
                        if (last && legB > 0) segs.Add(Math.Round(legB * 1000));
                        double cl = Dt.R10(pcs[i] - ded);
                        var sp = PlanStock(cl, n * members, s.StockLength, 0);
                        rows.Add(new ShopRow
                        {
                            Member = m?.Name ?? r.Name, Mark = b.Mark, Sub = tag + (i + 1), Role = b.Role + " – đoạn " + (i + 1) + "/" + pcs.Count + " (nhóm " + tag + ")",
                            Dia = b.Dia, Shape = bends == 0 ? "Thẳng" : bends == 1 ? "L" : "U", Segments = segs, Length = pcs[i], Deduct = ded, CutLength = cl,
                            PerMember = n, Members = members, StockBars = sp.StockBars, WastePct = sp.WastePct,
                            Note = "nối chồng ll = " + Dt.Mm(si.Lap) + " tại x = " + string.Join("; ", list.Select(v => Dt.F(v))) + " m"
                        });
                    }
                }
                continue;
            }
            double cut = CutLength(b);
            var plan = PlanStock(cut, b.Count * members, s.StockLength, lap);
            rows.Add(new ShopRow
            {
                Member = m?.Name ?? r.Name, Mark = b.Mark, Role = b.Role, Dia = b.Dia, Shape = b.Shape, Segments = (b.Segments ?? new()).ToList(),
                Length = b.Length, Deduct = BendDeduction(b), CutLength = cut, PerMember = b.Count, Members = members,
                StockBars = plan.StockBars, WastePct = plan.WastePct,
                Note = plan.Pieces > 1 ? "dài > cây " + Dt.F(s.StockLength) + " m: " + plan.Pieces + " đoạn (" + string.Join(" + ", plan.PieceLengths.Select(v => Dt.Mm(v))) + "), nối chồng " + Dt.Mm(plan.Lap)
                    : (cut > 1e-6 ? Math.Max((int)Math.Floor(s.StockLength / cut + 1e-9), 1) + " đoạn / cây" : "")
            });
        }
        return rows;
    }

    // ------------------------------------------------------------------
    // Bản vẽ
    // ------------------------------------------------------------------
    /// <summary>Shop drawing dầm: hình gia công từng thanh (tách riêng, kích thước từng đoạn, điểm nối) + bảng cắt thép.</summary>
    public static List<Figure> BeamFigures(ManualBeam m, DetailResult r, DetailSettings s)
    {
        var figs = new List<Figure>();
        if (m == null || r == null || r.Bars.Count == 0) return figs;
        s ??= new DetailSettings();
        double S = 50;
        var w = new DxfWriter(S);
        double th = w.Th, y = 0;
        var spl = BeamSplices(m, r, s);
        double maxL = r.Bars.Where(b => !(b.Shape ?? "").StartsWith("Đai")).Select(b => Math.Abs(b.End - b.Start) * 1000).DefaultIfEmpty(3000).Max();
        foreach (var b in r.Bars.OrderBy(x => x.Mark))
        {
            bool st = (b.Shape ?? "").StartsWith("Đai");
            string head = (st ? Dt.Spaced(b.Dia, b.Spacing) + " – " + b.Count + " đai" : Dt.Bars(b.Count, b.Dia)) + "  " + b.Role + "  L = " + Dt.Mm(b.Length) + ", L cắt = " + Dt.Mm(CutLength(b));
            w.MarkCircle(-2.0 * th, y + 0.4 * th, b.Mark);
            w.Text(Lay.Chu, -0.8 * th, y, th, head, 0, 0, 0);
            double yb = y - 2.6 * th;
            if (st)
            {
                double s1 = b.Segments.Count > 0 ? b.Segments[0] : 200, s2 = b.Segments.Count > 1 ? b.Segments[1] : 400;
                w.Rect(Lay.Thep, 0, yb - s2, s1, yb);
                double hk = Math.Max(75, 10 * b.Dia);
                w.Line(Lay.Thep, s1 * 0.15, yb, s1 * 0.15 + hk * 0.7071, yb - hk * 0.7071);
                w.Line(Lay.Thep, s1 * 0.15 + 0.3 * th, yb, s1 * 0.15 + 0.3 * th + hk * 0.7071, yb - hk * 0.7071);
                w.Dim(0, yb - s2, s1, yb - s2, -1.5 * th);
                w.Dim(s1, yb - s2, s1, yb, -1.5 * th);
                y = yb - s2 - 4.5 * th;
                continue;
            }
            var (legA, legB) = Legs(b);
            double x0 = 0, len = Math.Abs(b.End - b.Start) * 1000;
            var pts = new List<(double X, double Y)>();
            double dir = b.IsTop ? -1 : 1;   // mỏ thép trên bẻ xuống, thép dưới bẻ lên
            if (legA > 0) pts.Add((x0, yb + dir * legA * 1000));
            pts.Add((x0, yb)); pts.Add((x0 + len, yb));
            if (legB > 0) pts.Add((x0 + len, yb + dir * legB * 1000));
            w.Polyline(Lay.Thep, pts, false, 0.3 * S);
            // kích thước từng đoạn
            double dy = b.IsTop ? 1.6 * th : -1.6 * th;
            w.Dim(x0, yb, x0 + len, yb, b.IsTop ? -2.2 * th : 2.2 * th);
            if (legA > 0) w.Dim(x0, yb, x0, yb + dir * legA * 1000, 1.4 * th);
            if (legB > 0) w.Dim(x0 + len, yb, x0 + len, yb + dir * legB * 1000, -1.4 * th);
            // điểm nối (nhóm A, nhóm B so le) + ghi chú
            double maxLeg = Math.Max(legA, legB) * 1000;
            double yLow = Math.Min(b.IsTop ? yb - maxLeg : yb, yb - 3.2 * th);
            var si = spl.FirstOrDefault(q => ReferenceEquals(q.Bar, b));
            if (si != null && si.A.Count > 0)
            {
                foreach (var (list, grp) in new[] { (si.A, "A"), (si.B, "B") })
                    foreach (double xs in list)
                    {
                        double px = (xs - b.Start) * 1000, half = si.Lap * 500, yy = yb + dy * (grp == "A" ? 0.6 : 1.1);
                        w.Line(Lay.KyHieu, px - half, yy, px + half, yy);
                        w.Line(Lay.KyHieu, px - half, yb + dy * 0.3, px - half, yb + dy * 1.3);
                        w.Line(Lay.KyHieu, px + half, yb + dy * 0.3, px + half, yb + dy * 1.3);
                        w.Text(Lay.Chu, px, yy + (b.IsTop ? 0.2 * th : -1.0 * th), 0.7 * th, "nối " + grp + " ll=" + Dt.Mm(si.Lap), 0, 1, 0);
                    }
                yLow = Math.Min(yLow, yb - 3.0 * th);
                w.Text(Lay.Chu, x0, yLow - 1.2 * th, 0.8 * th,
                    "Nhóm A (" + ((b.Count + 1) / 2) + " thanh) nối tại x = " + string.Join("; ", si.A.Select(v => Dt.F(v))) + " m; nhóm B (" + (b.Count / 2) + " thanh) so le tại "
                    + string.Join("; ", si.B.Select(v => Dt.F(v))) + " m – các đoạn cắt xem bảng", 0, 0, 0);
                yLow -= 1.5 * th;
            }
            y = yLow - 3.0 * th;
        }
        w.Title(maxL / 2, 3.5 * th + 2.5 * th, "SHOP DRAWING THÉP DẦM " + m.Name + " (SL=" + Math.Max(1, r.Quantity) + ")", DrawUtil.ScaleText(S));
        w.Text(Lay.Chu, 0, y - 0.5 * th, 0.8 * th, "Kích thước ghi là kích thước NGOÀI thanh (mm). Chiều dài cắt = Σ đoạn − 2d mỗi góc uốn 90° (đai: −12d). Thứ tự lắp dựng: xem ghi chú cấu tạo.", 0, 0, 0);
        figs.Add(new Figure(w, "Shop drawing " + m.Name, true));
        figs.Add(new Figure(CutTable("BẢNG GIA CÔNG – CẮT THÉP DẦM " + m.Name, Rows(m, r, s), s), "Bảng cắt thép " + m.Name, false));
        return figs;
    }

    /// <summary>Shop drawing sàn: bảng gia công – cắt thép (hình dạng, L cắt, số thanh, cây thép, hao hụt).</summary>
    public static List<Figure> SlabFigures(IDetailMember m, DetailResult r, DetailSettings s)
    {
        var figs = new List<Figure>();
        if (r == null || r.Bars.Count == 0) return figs;
        figs.Add(new Figure(CutTable("SHOP DRAWING – BẢNG GIA CÔNG, CẮT THÉP SÀN " + (m?.Name ?? r.Name), Rows(m, r, s), s), "Shop drawing " + (m?.Name ?? r.Name), true));
        return figs;
    }

    /// <summary>Bảng cắt thép (DXF, tỷ lệ 1/1 – kích thước giấy mm).</summary>
    public static DxfWriter CutTable(string title, List<ShopRow> rows, DetailSettings s)
    {
        var w = new DxfWriter(1);
        double th = 2.5, rh = 7;
        string[] head = { "SỐ HIỆU", "Ø", "HÌNH DẠNG – KÍCH THƯỚC (mm)", "L (mm)", "TRỪ UỐN", "L CẮT (mm)", "SL/1 CK", "SL CK", "TỔNG", "CÂY " + Dt.F(s?.StockLength ?? 11.7) + " m", "HAO HỤT %", "KL (kg)", "GHI CHÚ" };
        double[] cw = { 16, 10, 60, 18, 15, 20, 15, 12, 14, 18, 16, 16, 95 };
        double x = 0, y = 0, W = cw.Sum();
        w.Text(Lay.Chu, W / 2, y + 4, 3.5, title, 0, 1, 0);
        void Row(string[] cells, bool bold)
        {
            double xx = 0;
            w.Line(Lay.KhungTen, 0, y, W, y);
            for (int i = 0; i < cells.Length; i++)
            {
                string t = cells[i] ?? "";
                double h = bold ? 2.0 : th;
                double maxChars = (cw[i] - 2) / (0.62 * h);
                if (t.Length > maxChars && maxChars > 4) t = t[..Math.Max((int)maxChars - 1, 1)] + "…";
                w.Text(Lay.Chu, i == 2 || i == 12 ? xx + 1.2 : xx + cw[i] / 2, y - rh / 2, h, t, 0, i == 2 || i == 12 ? 0 : 1, 2);
                xx += cw[i];
            }
            y -= rh;
        }
        Row(head, true);
        foreach (var r in rows)
            Row(new[] { r.Mark + r.Sub, r.Dia.ToString(Dt.Inv), r.ShapeText, Dt.Mm(r.Length), Dt.Mm(r.Deduct), Dt.Mm(r.CutLength), r.PerMember.ToString(Dt.Inv), r.Members.ToString(Dt.Inv),
                r.Total.ToString(Dt.Inv), r.StockBars.ToString(Dt.Inv), Dt.F(r.WastePct, "0.0"), Dt.F(r.WeightKg, "0.0"), r.Note }, false);
        Row(new[] { "", "", "TỔNG", "", "", "", "", "", rows.Sum(q => q.Total).ToString(Dt.Inv), rows.Sum(q => q.StockBars).ToString(Dt.Inv), "", Dt.F(rows.Sum(q => q.WeightKg), "0.0"), "" }, true);
        w.Line(Lay.KhungTen, 0, y, W, y);
        // đường dọc
        double top = 0;
        foreach (double c in new[] { 0.0 }.Concat(cw.Select((v, i) => cw.Take(i + 1).Sum())))
            w.Line(Lay.KhungTen, c, top, c, y);
        return w;
    }

    /// <summary>Trang Excel "Gia công cắt thép" cho mọi dầm, sàn của hồ sơ.</summary>
    public static void AddExcelSheet(XlsxWriter x, DetailingProject p, List<(IDetailMember Member, DetailResult Result)> results)
    {
        if (x == null || results == null) return;
        var sh = x.AddSheet("Gia công cắt thép");
        sh.AddBoldRow("BẢNG GIA CÔNG – CẮT THÉP DẦM, SÀN (cây " + Dt.F(p?.Settings?.StockLength ?? 11.7) + " m)");
        sh.AddHeaderRow("Cấu kiện", "Số hiệu", "Vai trò", "Ø (mm)", "Hình dạng (mm)", "L (mm)", "Trừ uốn (mm)", "L cắt (mm)", "SL/1 CK", "SL CK", "Tổng thanh", "Số cây", "Hao hụt %", "KL (kg)", "Ghi chú");
        foreach (var (m, r) in results)
        {
            if (r == null || m is not (ManualBeam or ManualSlab or ManualSlabPlan)) continue;
            foreach (var row in Rows(m, r, p?.Settings))
                sh.AddRow(row.Member, row.Mark + row.Sub, row.Role, row.Dia, row.ShapeText, Math.Round(row.Length * 1000), Math.Round(row.Deduct * 1000), Math.Round(row.CutLength * 1000),
                    row.PerMember, row.Members, row.Total, row.StockBars, row.WastePct, Math.Round(row.WeightKg, 1), row.Note);
        }
        sh.SetColumnWidths(10, 8, 30, 7, 26, 9, 9, 9, 8, 7, 9, 8, 9, 10, 50);
    }
}
