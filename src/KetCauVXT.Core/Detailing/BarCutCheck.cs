using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// ĐIỂM CẮT / NỐI THÉP DẦM DO NGƯỜI DÙNG ĐIỀU CHỈNH – KIỂM TRA & CẢNH BÁO VÙNG NGUY HIỂM
// Quy tắc (dầm liên tục chịu tải phân bố, TCVN 5574:2018 mục 10.3.5 + thực hành cấu tạo; khi không có biểu đồ bao mô men):
//  • Mũ gối (thép chịu mô men âm): vươn từ mép gối ≥ L0/4 ở gối biên, ≥ L0/3 ở gối giữa (L0 nhịp thông thuỷ lớn hơn hai bên),
//    và ≥ lan (chiều dài neo) – ngắn hơn: thép bị cắt trong vùng còn mô men âm → NGUY HIỂM.
//  • Tăng cường dưới (mô men dương): điểm cắt lý thuyết ≈ 0,2·L0 từ mép gối, cộng đoạn kéo dài ≥ max(20d; lan/…) ⇒ cắt cách mép
//    gối không quá L0/6 (các lựa chọn 0,1·L0, L0/7, L0/8 đều đạt). Cắt xa hơn → thanh ngắn, hụt thép tại vùng M+ → NGUY HIỂM.
//  • Nối thép dưới chạy suốt: KHÔNG nối trong 1/3 giữa nhịp (M+ lớn nhất); nên nối gần gối (trong L0/3 từ mép gối hoặc trên gối).
//  • Nối thép trên chạy suốt: KHÔNG nối trong đoạn L0/4 từ mép gối và trên gối (M− lớn nhất); nên nối ở 1/3 giữa nhịp.
//  • Kháng chấn (tuỳ chọn): không nối trong vùng tới hạn 2h từ mép gối (TCVN 9386:2012 mục 5.6.3).
//  • Một tiết diện nối ≤ 50 % số thanh, các mối nối so le ≥ 1,3·ll (TCVN 5574:2018 mục 10.3.6).
// =====================================================================

/// <summary>Một điểm cắt / nối không đạt (để đánh dấu trên mặt đứng dầm).</summary>
public sealed class CutIssue
{
    /// <summary>Vị trí dọc dầm (m, từ tim gối đầu).</summary>
    public double X { get; set; }
    public bool Top { get; set; }
    public string Text { get; set; } = "";
}

public static class BarCutCheck
{
    public const string SeismicYes = "Có – không nối trong vùng tới hạn 2h từ mép gối";
    public const string SeismicNo = "Không";

    /// <summary>Danh sách số, GIỮ số 0 (0 = theo lựa chọn tự động): "0,9; 0; 1,2" → [0.9, 0, 1.2].</summary>
    public static List<double> ParseKeep(string text)
    {
        var res = new List<double>();
        if (string.IsNullOrWhiteSpace(text)) return res;
        string[] parts = text.Contains(';') ? text.Split(';') : text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in parts)
        {
            var t = raw.Trim().Replace(',', '.');
            if (t.Length == 0) { res.Add(0); continue; }
            res.Add(double.TryParse(t, System.Globalization.NumberStyles.Float, Dt.Inv, out double v) && v > 0 ? v : 0);
        }
        return res;
    }

    /// <summary>Giá trị thứ i của danh sách (0 nếu thiếu).</summary>
    public static double At(List<double> l, int i) => l != null && i >= 0 && i < l.Count ? l[i] : 0;

    /// <summary>Hình học dầm: toạ độ tim gối (m), bề rộng gối, nhịp thông thuỷ.</summary>
    public sealed record Geo(double[] Node, double W, double[] L0, double H)
    {
        public int Spans => L0.Length;
        public double Face0(int k) => Node[k] + W / 2;
        public double Face1(int k) => Node[k + 1] - W / 2;
        /// <summary>Nhịp chứa x (−1: trên gối / ngoài dầm).</summary>
        public int SpanAt(double x)
        {
            for (int k = 0; k < Spans; k++) if (x > Face0(k) - 1e-9 && x < Face1(k) + 1e-9) return k;
            return -1;
        }
        public int SupportAt(double x)
        {
            for (int j = 0; j < Node.Length; j++) if (Math.Abs(x - Node[j]) <= W / 2 + 1e-9) return j;
            return -1;
        }
    }

    public static Geo GeoOf(ManualBeam m)
    {
        var spans = DetailShim.ParseList(m.Spans);
        var node = new double[spans.Count + 1];
        for (int k = 0; k < spans.Count; k++) node[k + 1] = node[k] + spans[k];
        double w = Math.Max(m.SupportWidth, 0);
        return new Geo(node, w, spans.Select(L => Math.Max(L - w, 0.05)).ToArray(), m.H);
    }

    /// <summary>Chiều dài vươn tối thiểu của mũ gối tại gối j (m): gối biên max(L0/4; lan), gối giữa max(L0/3; lan) với L0 lớn hơn hai bên.</summary>
    public static double MinSupportExtension(Geo g, int j, double lan)
    {
        int ns = g.Spans;
        bool end = j == 0 || j == ns;
        double l0 = j == 0 ? g.L0[0] : j == ns ? g.L0[ns - 1] : Math.Max(g.L0[j - 1], g.L0[j]);
        return Math.Max(end ? l0 / 4 : l0 / 3, lan);
    }

    /// <summary>Khoảng cắt lớn nhất của thép tăng cường dưới tính từ mép gối (m) = L0/6.</summary>
    public static double MaxBottomCut(double l0) => l0 / 6;

    /// <summary>Vùng nguy hiểm khi NỐI thép: trả về mô tả (rỗng = cho phép). top = thép trên.</summary>
    public static string SpliceDanger(Geo g, double x, bool top, bool seismic)
    {
        int k = g.SpanAt(x), j = g.SupportAt(x);
        if (top)
        {
            if (j >= 0) return "trên gối " + (j + 1) + " (vùng mô men âm lớn nhất)";
            if (k < 0) return "ngoài dầm";
            double d0 = x - g.Face0(k), d1 = g.Face1(k) - x, l0 = g.L0[k];
            if (d0 < l0 / 4 || d1 < l0 / 4) return "trong đoạn L0/4 = " + Dt.Mm(l0 / 4) + " mm từ mép gối (vùng mô men âm)";
            if (seismic && (d0 < 2 * g.H || d1 < 2 * g.H)) return "trong vùng tới hạn 2h = " + Dt.Mm(2 * g.H) + " mm từ mép gối (kháng chấn)";
            return "";
        }
        if (j >= 0) return seismic ? "trên gối – vùng tới hạn nút khung (kháng chấn)" : "";
        if (k < 0) return "ngoài dầm";
        double e0 = x - g.Face0(k), e1 = g.Face1(k) - x, L = g.L0[k];
        if (e0 > L / 3 - 1e-9 && e1 > L / 3 - 1e-9) return "trong 1/3 giữa nhịp (vùng mô men dương lớn nhất)";
        if (seismic && (e0 < 2 * g.H || e1 < 2 * g.H)) return "trong vùng tới hạn 2h = " + Dt.Mm(2 * g.H) + " mm từ mép gối (kháng chấn)";
        return "";
    }

    /// <summary>Kiểm tra mọi điểm cắt / nối người dùng nhập của dầm: thêm CheckItem + "CẢNH BÁO: …" vào kết quả; trả về các điểm không đạt.</summary>
    public static List<CutIssue> Evaluate(ManualBeam m, DetailSettings s, List<CheckItem> checks, List<string> msgs, List<string> calc = null)
    {
        var issues = new List<CutIssue>();
        if (m == null) return issues;
        s ??= new DetailSettings();
        var g = GeoOf(m);
        if (g.Spans == 0) return issues;
        Material conc, main;
        try { conc = Material.Concrete(s.Concrete); } catch (ArgumentException) { conc = Material.Concrete("B25"); }
        try { main = Material.Rebar(s.RebarMain); } catch (ArgumentException) { main = Material.Rebar("CB400-V"); }
        double Lan(int d) => Math.Max(30 * d / 1000.0, RebarDetailing.AnchorageLength(conc, main, d));
        bool seismic = (m.Seismic ?? "").StartsWith("Có");
        int ns = g.Spans;
        bool any = false;

        // ---- mũ gối: vươn tuỳ chỉnh ----
        var ext = ParseKeep(m.SupExtUser);
        int dTop = m.SupN > 0 ? m.SupD : m.TopD;
        for (int j = 0; j <= ns; j++)
        {
            double e = At(ext, j);
            if (e <= 0) continue;
            any = true;
            double req = MinSupportExtension(g, j, Lan(dTop));
            bool end = j == 0 || j == ns;
            checks?.Add(Dt.Check("Mũ gối tại gối " + (j + 1) + ": vươn từ mép gối ≥ " + (end ? "max(L0/4; lan)" : "max(L0/3; lan)"), req * 1000, e * 1000, "mm", "người dùng nhập " + Dt.Mm(e) + " mm"));
            if (e < req - 1e-6)
            {
                msgs?.Add("CẢNH BÁO: mũ gối tại gối " + (j + 1) + " vươn " + Dt.Mm(e) + " mm < " + Dt.Mm(req) + " mm – cắt thép trong vùng còn mô men âm (NGUY HIỂM)");
                if (j > 0) issues.Add(new CutIssue { X = g.Node[j] - g.W / 2 - e, Top = true, Text = "cắt mũ gối sớm" });
                if (j < ns) issues.Add(new CutIssue { X = g.Node[j] + g.W / 2 + e, Top = true, Text = "cắt mũ gối sớm" });
            }
        }
        // ---- tăng cường dưới: khoảng cắt tuỳ chỉnh ----
        var cut = ParseKeep(m.AddBotCutUser);
        for (int k = 0; k < ns; k++)
        {
            double cd = At(cut, k);
            if (cd <= 0) continue;
            any = true;
            double lim = MaxBottomCut(g.L0[k]);
            checks?.Add(Dt.Check("Tăng cường dưới nhịp " + (k + 1) + ": cắt cách mép gối ≤ L0/6", cd * 1000, lim * 1000, "mm"));
            if (cd > lim + 1e-6)
            {
                msgs?.Add("CẢNH BÁO: tăng cường dưới nhịp " + (k + 1) + " cắt cách mép gối " + Dt.Mm(cd) + " mm > L0/6 = " + Dt.Mm(lim) + " mm – thiếu thép vùng mô men dương (NGUY HIỂM)");
                issues.Add(new CutIssue { X = g.Face0(k) + cd, Top = false, Text = "cắt tăng cường xa gối" });
                issues.Add(new CutIssue { X = g.Face1(k) - cd, Top = false, Text = "cắt tăng cường xa gối" });
            }
            if (2 * cd >= g.L0[k]) msgs?.Add("LỖI tăng cường dưới nhịp " + (k + 1) + ": khoảng cắt " + Dt.Mm(cd) + " mm ≥ L0/2 – thanh không còn chiều dài");
        }
        // ---- vị trí nối thép chạy suốt ----
        double lap = RebarDetailing.LapLength(conc, main, Math.Max(m.BotD, m.TopD));
        foreach (var (list, top) in new[] { (ParseKeep(m.BotSplices), false), (ParseKeep(m.TopSplices), true) })
        {
            var xsS = list.Where(v => v > 0).OrderBy(v => v).ToList();
            if (xsS.Count == 0) continue;
            any = true;
            string which = top ? "trên" : "dưới";
            foreach (double x in xsS)
            {
                string dz = SpliceDanger(g, x, top, seismic);
                checks?.Add(Dt.Check("Nối thép " + which + " tại x = " + Dt.F(x) + " m: ngoài vùng nguy hiểm", 1, dz.Length == 0 ? 1 : 0, "", dz));
                if (dz.Length > 0)
                {
                    msgs?.Add("CẢNH BÁO: nối thép " + which + " tại x = " + Dt.F(x) + " m nằm " + dz + " – dời mối nối (NGUY HIỂM)");
                    issues.Add(new CutIssue { X = x, Top = top, Text = "nối " + which + " sai vùng" });
                }
            }
            // độ dài đoạn thép giữa hai mối nối (kể cả đoạn nối chồng) ≤ cây thép
            double xs0 = -g.W / 2, xe0 = g.Node[^1] + g.W / 2;
            var pts = new List<double> { xs0 }; pts.AddRange(xsS); pts.Add(xe0);
            for (int i = 1; i < pts.Count; i++)
            {
                double piece = pts[i] - pts[i - 1] + (i > 1 ? lap / 2 : 0) + (i < pts.Count - 1 ? lap / 2 : 0);
                if (piece > s.StockLength + 1e-6)
                    msgs?.Add("CẢNH BÁO: đoạn thép " + which + " giữa x = " + Dt.F(pts[i - 1]) + " và " + Dt.F(pts[i]) + " m dài " + Dt.F(piece) + " m > cây " + Dt.F(s.StockLength) + " m – cần thêm mối nối");
            }
            for (int i = 1; i < xsS.Count; i++)
                if (xsS[i] - xsS[i - 1] < RebarDetailing.StaggerDistance(lap) - 1e-6)
                    msgs?.Add("Mối nối thép " + which + " tại " + Dt.F(xsS[i - 1]) + " và " + Dt.F(xsS[i]) + " m gần nhau < 1,3·ll = " + Dt.Mm(RebarDetailing.StaggerDistance(lap)) + " mm – coi là cùng một tiết diện nối (≤ 50 % số thanh)");
        }
        if (any)
            calc?.Add("Điểm cắt / nối thép người dùng điều chỉnh: mũ gối vươn ≥ L0/4 (gối biên), ≥ L0/3 (gối giữa) và ≥ lan; tăng cường dưới cắt ≤ L0/6 từ mép gối; "
                      + "thép dưới không nối trong 1/3 giữa nhịp, thép trên không nối trong L0/4 từ mép gối và trên gối" + (seismic ? "; kháng chấn: không nối trong 2h từ mép gối" : "")
                      + "; nối chồng ll = " + Dt.Mm(lap) + " mm, so le ≥ 1,3·ll, ≤ 50 % số thanh tại một tiết diện (TCVN 5574:2018 mục 10.3.5–10.3.6)");
        return issues;
    }

    /// <summary>Đánh dấu điểm cắt / nối sai trên mặt đứng dầm (toạ độ mm, y = 0 đỉnh dầm): dấu X đỏ tại thanh + vòng "!" và chữ nhỏ.</summary>
    public static int DrawMarks(DxfWriter w, IEnumerable<CutIssue> issues, double hMm)
    {
        if (w == null || issues == null) return 0;
        int n = 0;
        double th = w.Th;
        foreach (var it in issues)
        {
            int start = w.EntityCount;
            double x = it.X * 1000, y = it.Top ? -0.12 * hMm : -0.88 * hMm, a = 0.6 * th;
            w.Line(Lay.KyHieu, x - a, y - a, x + a, y + a);
            w.Line(Lay.KyHieu, x - a, y + a, x + a, y - a);
            double cy = it.Top ? 0.9 * th + 1.2 * th : -hMm - 2.1 * th;
            w.Circle(Lay.KyHieu, x, cy, 0.75 * th);
            w.Text(Lay.Chu, x, cy, 0.9 * th, "!", 0, 1, 2);
            w.Text(Lay.Chu, x + 1.0 * th, cy - 0.35 * th, 0.7 * th, it.Text, 0, 0, 0);
            w.StyleFrom(start, 1, null);   // màu đỏ
            n++;
        }
        return n;
    }
}
