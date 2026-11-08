using KetCauVXT.Analysis;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

// =====================================================================
// [MóngHìnhDạng] THIẾT KẾ MÓNG ĐƠN CÓ HÌNH DẠNG BẤT KỲ: xoay góc / dóng mép theo mép cột / lệch tâm nhập tay /
// cắt theo đường chuẩn (ranh đất xiên) / đa giác do người dùng vẽ.
// - Áp lực đáy móng: phân bố tuyến tính trên đa giác, lời giải không chịu kéo (lặp vùng tiếp xúc) trên lưới ô 60 × 60,
//   hợp lực tại tim cột + mô men; p = Ntc/A + γtb·Df; ptb ≤ R, pmax ≤ 1,2R, pmin ≥ 0 (móng lệch tâm: vùng tiếp xúc ≥ 75%).
// - Chọc thủng (TCVN 5574:2018 mục 8.1.6): chu vi tháp cách mép cột h0/2 – chỉ kể phần nằm trong đáy móng; phản lực trong
//   đáy tháp (cột + 2h0) ∩ đa giác được trừ.
// - Uốn: mô men công xôn tại mép cột theo phương L / B địa phương (tích phân áp lực ròng phía ngoài mép cột) chia cho bề rộng
//   mặt cắt thật tại mép cột → As trên 1 m; thanh cắt theo đa giác, nhóm chiều dài 50 mm (mỗi nhóm một số hiệu).
// =====================================================================
public static partial class FootingDesigner
{
    private readonly struct Cell
    {
        public readonly double X, Y, W;
        public Cell(double x, double y, double w) { X = x; Y = y; W = w; }
    }

    /// <summary>Kết quả phân bố áp lực: hệ số p = C0 + A·x + B·y trên vùng tiếp xúc.</summary>
    private readonly struct Dist
    {
        public readonly double C0, A, B, Contact, Pmax, Pmin;
        public readonly bool Ok;
        public Dist(double c0, double a, double b, double contact, double pmax, double pmin, bool ok)
        { C0 = c0; A = a; B = b; Contact = contact; Pmax = pmax; Pmin = pmin; Ok = ok; }
        public double P(double x, double y) => Math.Max(0, C0 + A * x + B * y);
    }

    private static List<Cell> Cells(List<(double X, double Y)> poly, double area, int nDiv = 60)
    {
        double x0 = poly.Min(q => q.X), x1 = poly.Max(q => q.X), y0 = poly.Min(q => q.Y), y1 = poly.Max(q => q.Y);
        double dx = (x1 - x0) / nDiv, dy = (y1 - y0) / nDiv;
        var res = new List<Cell>();
        if (dx <= 0 || dy <= 0) return res;
        for (int i = 0; i < nDiv; i++)
            for (int j = 0; j < nDiv; j++)
            {
                double x = x0 + (i + 0.5) * dx, y = y0 + (j + 0.5) * dy;
                if (PolyGeom.Inside(poly, x, y)) res.Add(new Cell(x, y, dx * dy));
            }
        double sum = res.Sum(c => c.W);
        if (sum > 0 && area > 0) res = res.Select(c => new Cell(c.X, c.Y, c.W * area / sum)).ToList();
        return res;
    }

    /// <summary>Phân bố áp lực không chịu kéo: Σp = N, Σp·x = N·ex, Σp·y = N·ey (hệ địa phương).</summary>
    private static Dist Solve(List<Cell> cells, double N, double ex, double ey)
    {
        if (cells.Count < 3 || N <= 0) return new Dist(0, 0, 0, 1, 0, 0, true);
        double total = cells.Sum(c => c.W);
        var active = Enumerable.Repeat(true, cells.Count).ToArray();
        double c0 = 0, a = 0, b = 0, pminFull = 0;
        for (int it = 0; it < 60; it++)
        {
            double s0 = 0, sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
            int nAct = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (!active[i]) continue;
                var c = cells[i];
                s0 += c.W; sx += c.W * c.X; sy += c.W * c.Y; sxx += c.W * c.X * c.X; syy += c.W * c.Y * c.Y; sxy += c.W * c.X * c.Y;
                nAct++;
            }
            if (nAct < 3) return new Dist(0, 0, 0, 0, double.PositiveInfinity, double.NegativeInfinity, false);
            // [s0 sx sy; sx sxx sxy; sy sxy syy]·[c0 a b] = [N, N·ex, N·ey]
            if (!Solve3(s0, sx, sy, sx, sxx, sxy, sy, sxy, syy, N, N * ex, N * ey, out c0, out a, out b))
                return new Dist(0, 0, 0, 0, double.PositiveInfinity, double.NegativeInfinity, false);
            if (it == 0) pminFull = cells.Min(c => c0 + a * c.X + b * c.Y);
            bool changed = false;
            double act = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                bool on = c0 + a * cells[i].X + b * cells[i].Y > 0;
                if (on != active[i]) { active[i] = on; changed = true; }
                if (on) act += cells[i].W;
            }
            if (!changed)
            {
                double pmax = cells.Max(c => c0 + a * c.X + b * c.Y);
                return new Dist(c0, a, b, act / total, pmax, pminFull, true);
            }
            if (act < 0.05 * total) return new Dist(0, 0, 0, 0, double.PositiveInfinity, double.NegativeInfinity, false);
        }
        return new Dist(c0, a, b, 0, double.PositiveInfinity, double.NegativeInfinity, false);
    }

    private static bool Solve3(double a11, double a12, double a13, double a21, double a22, double a23, double a31, double a32, double a33,
        double b1, double b2, double b3, out double x1, out double x2, out double x3)
    {
        double det = a11 * (a22 * a33 - a23 * a32) - a12 * (a21 * a33 - a23 * a31) + a13 * (a21 * a32 - a22 * a31);
        x1 = x2 = x3 = 0;
        if (Math.Abs(det) < 1e-18) return false;
        x1 = (b1 * (a22 * a33 - a23 * a32) - a12 * (b2 * a33 - a23 * b3) + a13 * (b2 * a32 - a22 * b3)) / det;
        x2 = (a11 * (b2 * a33 - a23 * b3) - b1 * (a21 * a33 - a23 * a31) + a13 * (a21 * b3 - b2 * a31)) / det;
        x3 = (a11 * (a22 * b3 - b2 * a32) - a12 * (a21 * b3 - b2 * a31) + b1 * (a21 * a32 - a22 * a31)) / det;
        return true;
    }

    /// <summary>Các trường hợp tải (tổ hợp × dấu mô men): tổng lực Nt và vị trí hợp lực (hệ địa phương, gốc tâm cột).
    /// Lực cột N/div đặt tại tim cột; khi có dầm móng cân bằng (strap &gt; 0) lực cột tăng thành N·strap = N·Ls/(Ls − e) đặt tại trọng tâm đáy (gx, gy);
    /// G (trọng lượng móng + đất) đặt tại trọng tâm; mô men chân cột |M| + |Q|·arm lấy cả hai dấu.</summary>
    private static IEnumerable<(Load L, double N, double Ex, double Ey)> Cases(List<Load> loads, double angle, double div, double arm,
        double gx = 0, double gy = 0, double G = 0, double strap = 0)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        foreach (var l in loads)
        {
            if (l.N <= 0) continue;
            double N = l.N / div * (strap > 0 ? strap : 1);
            double xN = strap > 0 ? gx : 0, yN = strap > 0 ? gy : 0;
            double mx = (Math.Abs(l.My) + Math.Abs(l.Qx) * arm) / div, my = (Math.Abs(l.Mx) + Math.Abs(l.Qy) * arm) / div;
            double Nt = N + G;
            foreach (int sx in new[] { 1, -1 })
                foreach (int sy in new[] { 1, -1 })
                {
                    double wx = sx * mx, wy = sy * my;   // mô men bậc nhất theo X, Y tổng thể
                    double lx = wx * c + wy * s, ly = -wx * s + wy * c;
                    yield return (l, Nt, (N * xN + G * gx + lx) / Nt, (N * yN + G * gy + ly) / Nt);
                    if (my < 1e-12) break;
                }
        }
    }

    /// <summary>Chân cột đối diện để nối dầm móng cân bằng: theo hướng từ tim cột tới trọng tâm đáy móng (lệch ≤ 30°), gần nhất, ≤ nhịp tối đa.</summary>
    private static (int Node, double Span) StrapPartner(Project p, Node n, ShapedFooting.ColumnGeo cg, double gxW, double gyW)
    {
        double dx = gxW - cg.Cx, dy = gyW - cg.Cy, dl = Math.Sqrt(dx * dx + dy * dy);
        if (dl < 1e-6) return (0, 0);
        double maxSpan = p.Foundation?.StrapMaxSpan > 0 ? p.Foundation.StrapMaxSpan : 6.0;
        int best = 0; double bestD = double.MaxValue;
        foreach (var (bn, _) in FoundationUtil.BaseColumns(p))
        {
            if (bn.Id == n.Id) continue;
            double ux = bn.X - n.X, uy = bn.Y - n.Y, ul = Math.Sqrt(ux * ux + uy * uy);
            if (ul < 0.5 || ul > maxSpan + 1e-9) continue;
            double cos = (ux * dx + uy * dy) / (ul * dl);
            if (cos < Math.Cos(30 * Math.PI / 180)) continue;
            if (ul < bestD) { bestD = ul; best = bn.Id; }
        }
        return best == 0 ? (0, 0) : (best, bestD);
    }

    /// <summary>Thiết kế lại móng đơn theo hình dạng (khi có bố trí riêng / bị cắt bởi đường chuẩn). Trả về d (đã cập nhật) – giữ nguyên
    /// nếu không cần (móng chữ nhật đặt tâm) hoặc không áp dụng được (móng kết hợp, cừ tràm).</summary>
    internal static FootingDesign ShapedRedesign(Project p, AnalysisResults r, Node n, FrameElement col, List<string> combos, FootingDesign d)
    {
        var pl = ShapedFooting.PlacementOf(p, n.Id);
        var cg = ShapedFooting.Column(p, col, n);
        if (d == null || d.L <= 0 || d.B <= 0) return d;
        if (!ShapedFooting.Needed(p, cg, pl, d.L, d.B)) return d;
        if (d.Eccentric?.Combined != null) { d.Messages.Add("Cảnh báo: móng kết hợp – bỏ qua bố trí riêng / đường chuẩn (móng hình dạng chỉ áp dụng cho móng đơn)"); return d; }
        if (d.CuTram != null) { d.Messages.Add("Cảnh báo: móng gia cố cừ tràm – chưa hỗ trợ móng hình dạng / xoay góc, giữ móng chữ nhật đặt tâm"); return d; }

        var cs = p.Concrete;
        var soil = GeotechCalc.SoilFor(p, n.X, n.Y, out var geoBh);
        var conc = DesignUtil.ConcreteOf(p, cs.ConcreteFooting, cs.ConcreteFooting);
        var main = p.Material(cs.RebarMain);
        var stlSmall = p.Material(cs.RebarStirrup);
        Material SteelOf(int dia) => dia < 10 ? stlSmall : main;
        int[] dias = (p.RebarPrefs ?? new RebarPreferences()).Filter(DesignUtil.Sorted(cs.FootingBars, new[] { 10, 12, 14, 16 }));
        var loads = new List<Load>();
        foreach (string cn in combos)
        {
            var cr = r.Get(cn);
            if (!cr.Reactions.TryGetValue(n.Id, out var R)) continue;
            loads.Add(new Load { Combo = cn, N = R[2], Mx = R[3], My = R[4], Qx = R[0], Qy = R[1] });
        }
        if (loads.Count == 0) return d;
        double kt = Math.Max(soil.LoadFactorAvg, 1.0), gtb = soil.GammaAvg, Df = soil.Depth;
        var given = p.Footings.FirstOrDefault(x => x.NodeId == n.Id);
        bool fixedSize = pl != null && pl.IsPolygon || given != null && given.L > 0 && given.B > 0;

        // ---- kích thước + áp lực nền ----
        double L = given != null && given.L > 0 ? given.L : d.L, B = given != null && given.B > 0 ? given.B : d.B;
        double L0 = L, B0 = B;
        List<(double X, double Y)> poly = null, loc = null;
        List<Cell> cells = null;
        double angle = 0, area = 0, Rv = 0, ptb = 0, pmax = 0, pmin = 0, contact = 1;
        string gov = "";
        bool clipped = false, sized = false, offsetFooting = false;
        bool wantStrap = pl?.StrapBeam ?? true;
        double strapF = 0, strapSpan = 0;
        int strapNode = 0;
        for (int it = 0; it < 80; it++)
        {
            poly = ShapedFooting.Outline(p, cg, pl, L, B, out angle, out clipped);
            if (poly.Count < 3) { d.Messages.Add("LỖI móng hình dạng: đường bao đáy móng rỗng (bị đường chuẩn cắt hết) – kiểm tra phía đặt móng của đường chuẩn"); return d; }
            loc = ShapedFooting.ToLocal(poly, cg.Cx, cg.Cy, angle);
            area = PolyGeom.Properties(loc).Area;
            cells = Cells(loc, area);
            var pr = PolyGeom.Properties(loc);
            offsetFooting = Math.Sqrt(pr.Cx * pr.Cx + pr.Cy * pr.Cy) > 0.05;
            double lx = loc.Max(q => q.X) - loc.Min(q => q.X), ly = loc.Max(q => q.Y) - loc.Min(q => q.Y);
            double bEq = Math.Min(Math.Min(lx, ly), area / Math.Max(Math.Max(lx, ly), 1e-6));
            Rv = BearingR(soil, Math.Max(bEq, 0.3));
            // [dầm móng cân bằng] trọng tâm đáy lệch tim cột > 50 mm: nối dầm cân bằng tới cột đối diện (nếu cho phép và tìm được)
            strapF = 0; strapNode = 0; strapSpan = 0;
            double eOff = Math.Sqrt(pr.Cx * pr.Cx + pr.Cy * pr.Cy);
            if (wantStrap && eOff > 0.05)
            {
                var gw = ShapedFooting.ToWorld(pr.Cx, pr.Cy, cg.Cx, cg.Cy, angle);
                (strapNode, strapSpan) = StrapPartner(p, n, cg, gw.X, gw.Y);
                if (strapNode != 0 && strapSpan > eOff + 0.3) strapF = strapSpan / (strapSpan - eOff);
                else strapNode = 0;
            }
            double G = gtb * Df * area;
            ptb = double.MinValue; pmax = double.MinValue; pmin = double.MaxValue; contact = 1;
            foreach (var cse in Cases(loads, angle, kt, Df, pr.Cx, pr.Cy, G, strapF))
            {
                var dist = Solve(cells, cse.N, cse.Ex, cse.Ey);
                double p0 = cse.N / area;
                ptb = Math.Max(ptb, p0);
                double pm = dist.Ok ? dist.Pmax : double.PositiveInfinity;
                if (pm > pmax) { pmax = pm; gov = cse.L.Combo; }
                pmin = Math.Min(pmin, dist.Ok ? dist.Pmin : double.NegativeInfinity);
                contact = Math.Min(contact, dist.Ok ? dist.Contact : 0);
            }
            if (ptb == double.MinValue) { ptb = gtb * Df; pmax = ptb; pmin = ptb; }
            bool ok = ptb <= Rv + 1e-6 && pmax <= 1.2 * Rv + 1e-6 && (pmin >= -1e-6 || offsetFooting && contact >= 0.75 - 1e-9);
            if (ok) { sized = true; break; }
            if (fixedSize) break;
            if (Math.Max(L, B) >= ShapedFooting.MaxSize - 1e-9) break;
            L = Math.Min(ShapedFooting.MaxSize, RcSection.CeilTo(L0 + 0.1 * (it + 1), 0.1));
            B = Math.Min(ShapedFooting.MaxSize, RcSection.CeilTo(B0 + 0.1 * (it + 1) * B0 / Math.Max(L0, 1e-6), 0.1));
        }

        // giữ thông báo không phụ thuộc kích thước, bỏ thông báo / dữ liệu lệch tâm của thiết kế chữ nhật
        var keepMsg = d.Messages.Where(m => m.Contains("chịu kéo") || m.Contains("chịu nhổ") || m.Contains("Kích thước đáy móng do người dùng chọn")).ToList();
        d.Messages.Clear(); d.Messages.AddRange(keepMsg);
        d.Checks.Clear(); d.Bars.Clear();
        d.Eccentric = null;
        d.StepL = d.StepB = 0;

        double lx0 = loc.Min(q => q.X), lx1 = loc.Max(q => q.X), ly0 = loc.Min(q => q.Y), ly1 = loc.Max(q => q.Y);
        var props = PolyGeom.Properties(loc);
        var gW = ShapedFooting.ToWorld(props.Cx, props.Cy, cg.Cx, cg.Cy, angle);
        d.Outline = poly.Select(q => new[] { q.X, q.Y }).ToList();
        d.Angle = angle * 180 / Math.PI;
        d.Area = area; d.Gx = gW.X; d.Gy = gW.Y; d.Contact = contact;
        d.L = lx1 - lx0; d.B = ly1 - ly0;
        d.RectL = pl != null && pl.IsPolygon ? 0 : L; d.RectB = pl != null && pl.IsPolygon ? 0 : B;
        d.ColLocalX = -(lx0 + lx1) / 2; d.ColLocalY = -(ly0 + ly1) / 2;
        d.R = Rv; d.Ptb = ptb; d.Pmax = pmax; d.Pmin = pmin;
        var (cx0, cx1, cy0, cy1) = ShapedFooting.ColumnExtents(cg, angle);
        d.ColumnH = cx1 - cx0; d.ColumnB = cy1 - cy0;

        string shape = pl != null && pl.IsPolygon ? "đa giác " + poly.Count + " đỉnh" : clipped ? "chữ nhật cắt theo đường chuẩn (" + poly.Count + " đỉnh)" : "chữ nhật";
        d.Messages.Add($"Móng hình dạng: {shape}, góc {d.Angle:0.#}°, diện tích đáy A = {area:0.00} m², khung bao {d.L:0.00} × {d.B:0.00} m"
                       + (pl != null ? $"; dóng phương L: {FootingPlacement.AlignName(pl.AlignX).ToLowerInvariant()}, phương B: {FootingPlacement.AlignName(pl.AlignY).ToLowerInvariant()}" : ""));
        double eG = Math.Sqrt(props.Cx * props.Cx + props.Cy * props.Cy);
        if (eG > 1e-3)
            d.Messages.Add($"Trọng tâm đáy móng lệch so với tim cột e = {eG * 1000:0} mm (ex' = {props.Cx * 1000:0}, ey' = {props.Cy * 1000:0} mm theo phương L/B)");
        if (!sized)
            d.Messages.Add(fixedSize
                ? "LỖI móng hình dạng với kích thước / đa giác người dùng chọn không thoả áp lực nền – mở rộng đáy móng hoặc dùng dầm móng cân bằng / móng cọc"
                : $"LỖI móng hình dạng không thoả áp lực nền với kích thước ≤ {ShapedFooting.MaxSize:0.0} m – dùng dầm móng cân bằng, móng kết hợp hoặc móng cọc");

        var g = loads.OrderByDescending(l => l.N).First();
        d.N = g.N; d.Mx = g.Mx; d.My = g.My; d.Qx = g.Qx; d.Qy = g.Qy; d.Ntc = g.N / kt; d.Combo = g.Combo;
        d.Checks.Add(new CheckItem { Name = "Áp lực trung bình đáy móng ptb ≤ R", Demand = ptb, Capacity = Rv, Unit = "kN/m²", Note = "A = " + DesignUtil.F(area, "0.00") + " m²" });
        d.Checks.Add(new CheckItem { Name = "Áp lực lớn nhất pmax ≤ 1,2R", Demand = Math.Min(pmax, 99999), Capacity = 1.2 * Rv, Unit = "kN/m²", Note = gov });
        if (offsetFooting && pmin < -1e-6)
            d.Checks.Add(new CheckItem
            {
                Name = "Vùng tiếp xúc nền ≥ 75% (móng lệch tâm, áp lực không chịu kéo)", Demand = 0.75, Capacity = Math.Max(contact, 0), Unit = "",
                Note = "vùng tiếp xúc " + DesignUtil.F(contact * 100, "0") + "%"
            });
        else
            d.Checks.Add(new CheckItem
            {
                Name = "Áp lực nhỏ nhất pmin ≥ 0", Demand = pmin < 0 ? Math.Min(-pmin, 99999) : 0, Capacity = pmin < 0 ? 0 : 1, Unit = "kN/m²",
                Note = "pmin = " + DesignUtil.F(Math.Max(pmin, -99999), "0.0") + " kN/m²"
            });

        // ---- phân bố áp lực ròng tính toán (không kể trọng lượng móng + đất) ----
        var design = Cases(loads, angle, 1.0, Df, props.Cx, props.Cy, 0, strapF).Select(cse => (cse.N, D: Solve(cells, cse.N, cse.Ex, cse.Ey))).Where(x => x.D.Ok).ToList();

        // ---- chọc thủng ----
        double cover = cs.CoverFooting;
        int dGuess = dias[Math.Min(1, dias.Length - 1)];
        double H = Math.Max(MinHeight, given != null && given.Height > 0 ? given.Height : MinHeight);
        double F = 0, Fu = 0, uIn = 0;
        var colLoc = new ShapedFooting.ColumnGeo(0, 0, cg.Angle - angle, cg.D2, cg.D3);
        for (int it = 0; it < 100; it++)
        {
            double h0 = H - cover - dGuess / 1000.0;
            var basePoly = PolyGeom.ClipConvex(loc, colLoc.Expanded(h0));
            var inBase = cells.Where(c => PolyGeom.Inside(basePoly, c.X, c.Y)).ToList();
            uIn = PerimeterInside(loc, colLoc.Expanded(h0 / 2));
            Fu = conc.Rbt * uIn * Math.Max(h0, 0);
            F = 0;
            foreach (var (N, D) in design) F = Math.Max(F, N - inBase.Sum(c => D.P(c.X, c.Y) * c.W));
            if (F <= Fu + 1e-6) break;
            if (given != null && given.Height > 0) break;
            H = Math.Round(H + 0.05, 3);
        }
        d.H = H; d.Hb = H;   // móng hình dạng: đế phẳng
        d.Checks.Add(new CheckItem
        {
            Name = "Chọc thủng F ≤ Rbt·u·h0", Demand = F, Capacity = Fu, Unit = "kN",
            Note = "H = " + DesignUtil.F(H * 1000, "0") + " mm, u = " + DesignUtil.F(uIn, "0.00") + " m (phần chu vi trong đáy móng)"
        });

        // ---- uốn tại mép cột (hệ địa phương) ----
        double mL = 0, mB = 0;   // kN.m trên 1 m bề rộng mặt cắt
        foreach (var (_, D) in design)
        {
            mL = Math.Max(mL, FaceMoment(cells, loc, D, cx1, true, true));
            mL = Math.Max(mL, FaceMoment(cells, loc, D, cx0, true, false));
            mB = Math.Max(mB, FaceMoment(cells, loc, D, cy1, false, true));
            mB = Math.Max(mB, FaceMoment(cells, loc, D, cy0, false, false));
        }
        d.ML = mL * d.B; d.MB = mB * d.L;
        string key = MemberKey(n.Id);
        string basis = Basis(d.L, d.B, H);
        int nUser = 0;
        SpacingChoice Ovr(string zone, SpacingChoice auto, Func<int, double> req)
        {
            var ch = p.ValidRebarChoice(key, zone, -1, basis);
            if (ch == null || ch.Dia <= 0 || ch.Spacing <= 0) return auto;
            nUser++;
            var sc = new SpacingChoice { Dia = ch.Dia, Spacing = ch.Spacing, AsReqPerM = req(ch.Dia), Ok = true };
            if (sc.AsPerM < sc.AsReqPerM * (1 - 1e-6))
                d.Messages.Add("LỖI bố trí thép do người dùng chọn không đạt – " + zone + " " + sc.Text);
            return sc;
        }
        var sL = Ovr(RebarZones.FootingL, RcSection.ChooseSpacing(dia => AsPerM(mL, H - cover - dia / 2000.0, SteelOf(dia)), dias, 0.20, 0.10, 0.15, 10),
            dia => AsPerM(mL, H - cover - dia / 2000.0, SteelOf(dia)));
        double dl = sL.Dia / 1000.0;
        var sB = Ovr(RebarZones.FootingB, RcSection.ChooseSpacing(dia => AsPerM(mB, H - cover - dl - dia / 2000.0, SteelOf(dia)), dias, 0.20, 0.10, 0.15, 10),
            dia => AsPerM(mB, H - cover - dl - dia / 2000.0, SteelOf(dia)));
        d.UserRebarZones = nUser;
        d.AsL = sL.AsReqPerM * d.B; d.AsB = sB.AsReqPerM * d.L;
        d.DiaL = sL.Dia; d.DiaB = sB.Dia; d.SpacingL = sL.Spacing; d.SpacingB = sB.Spacing;
        d.BarsL = sL.Text; d.BarsB = sB.Text;
        if (!sL.Ok || !sB.Ok) d.Messages.Add("LỖI không bố trí đủ thép đáy móng với bước ≥ 100 mm – tăng chiều cao móng");
        double h0L = H - cover - dl, h0B = H - cover - dl - sB.Dia / 2000.0;
        d.Checks.Add(new CheckItem
        {
            Name = "Uốn phương L tại mép cột (trên 1 m)", Demand = mL, Unit = "kN.m/m", Note = sL.Text,
            Capacity = RcSection.MomentCapacity(1.0, h0L, sL.AsPerM, 0, cover, conc, SteelOf(sL.Dia))
        });
        d.Checks.Add(new CheckItem
        {
            Name = "Uốn phương B tại mép cột (trên 1 m)", Demand = mB, Unit = "kN.m/m", Note = sB.Text,
            Capacity = RcSection.MomentCapacity(1.0, h0B, sB.AsPerM, 0, cover, conc, SteelOf(sB.Dia))
        });

        // ---- thanh thép cắt theo đa giác ----
        AddShapedBars(d, loc, cover, sL, true);
        AddShapedBars(d, loc, cover, sB, false);
        if (d.Bars.Count > 2)
            d.Messages.Add("Thép đáy cắt theo hình dạng móng: thanh nhóm theo chiều dài (bước nhóm 50 mm, cắt ngắn về phía an toàn lớp bảo vệ) – mỗi nhóm một số hiệu");

        if (geoBh != null)
        {
            double bq = Math.Sqrt(area * d.B / Math.Max(d.L, 1e-6)), lq = area / Math.Max(bq, 1e-6);
            AddSettlementCheck(d, geoBh, soil, Math.Max(lq, bq), Math.Min(lq, bq), ptb);
        }
        d.StrapNode = strapNode; d.StrapSpan = strapSpan;
        if (strapF > 0)
        {
            double R1 = g.N * strapF, dR = R1 - g.N;
            d.StrapMoment = g.N * eG; d.StrapShear = dR;
            d.Messages.Add($"Dầm móng cân bằng nối tới chân cột nút {strapNode} (nhịp Ls = {strapSpan:0.00} m, e = {eG * 1000:0} mm): R1 = N·Ls/(Ls − e) = {g.N:0.0}×{strapF:0.000} = {R1:0.0} kN tại trọng tâm đáy móng; "
                           + $"dầm chịu M = N·e = {d.StrapMoment:0.0} kN.m, Q = ΔR = {dR:0.0} kN – thiết kế dầm móng (giằng) với nội lực này; móng cột trong được giảm ΔR (chưa kể, thiên về an toàn).");
        }
        else if (eG > 0.05)
        {
            if (wantStrap) d.Messages.Add($"Cảnh báo: không tìm được chân cột đối diện (hướng lệch tâm, ≤ {(p.Foundation?.StrapMaxSpan > 0 ? p.Foundation.StrapMaxSpan : 6):0.0} m) để nối dầm móng cân bằng – móng tính tự cân bằng.");
            d.Messages.Add($"Cảnh báo: móng lệch tâm tự cân bằng – mô men lệch tâm ΔM ≈ N·e = {g.N * eG:0.0} kN.m do đáy móng chịu (áp lực lệch tâm); "
                           + "kiểm tra cột tầng 1 + giằng móng, hoặc bật \"Dầm móng cân bằng\" trong Bố trí móng.");
        }
        try
        {
            double Ls = d.L, Bs = d.B;
            KetCauVXT.Design.Foundation.FootingBehaviour.AnalyzeIsolated(d, soil, conc, cover);
            d.L = Ls; d.B = Bs;
            d.Messages.Add("Sự làm việc móng (móng cứng, trượt, cắt) tính gần đúng theo khung bao L × B của móng hình dạng");
        }
        catch (Exception ex) { d.Messages.Add("Cảnh báo: không phân tích được sự làm việc móng – " + ex.Message); }
        RebarNumbering.Renumber(d.Bars);
        return d;
    }

    /// <summary>Độ dài phần chu vi (đa giác lồi clip) nằm trong đáy móng (lấy mẫu 400 điểm).</summary>
    private static double PerimeterInside(List<(double X, double Y)> poly, List<(double X, double Y)> ring)
    {
        double inside = 0;
        for (int i = 0; i < ring.Count; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Count];
            double len = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            const int ns = 100;
            for (int k = 0; k < ns; k++)
            {
                double t = (k + 0.5) / ns;
                if (PolyGeom.Inside(poly, a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y))) inside += len / ns;
            }
        }
        return inside;
    }

    /// <summary>Mô men công xôn tại mặt cắt x' = face (alongL) hoặc y' = face, phía dương / âm, chia bề rộng mặt cắt thật (kN.m/m).</summary>
    private static double FaceMoment(List<Cell> cells, List<(double X, double Y)> loc, Dist D, double face, bool alongL, bool positive)
    {
        double m = 0;
        foreach (var c in cells)
        {
            double t = alongL ? c.X : c.Y;
            double arm = positive ? t - face : face - t;
            if (arm <= 0) continue;
            m += D.P(c.X, c.Y) * c.W * arm;
        }
        if (m <= 0) return 0;
        // bề rộng mặt cắt: dây cung của đa giác tại mặt cắt (lùi 1 mm ra phía công xôn)
        double f = face + (positive ? 1e-3 : -1e-3);
        var ch = alongL ? PolyGeom.Chords(loc, f, 0, 0, 1) : PolyGeom.Chords(loc, 0, f, 1, 0);
        double w = ch.Sum(q => q.T2 - q.T1);
        if (w < 0.1)
        {
            // cột sát mép (không có công xôn) – bề rộng lấy theo trung bình vùng công xôn
            double span = 0, area = 0;
            foreach (var c in cells)
            {
                double t = alongL ? c.X : c.Y;
                double arm = positive ? t - face : face - t;
                if (arm > 0) { area += c.W; span = Math.Max(span, arm); }
            }
            w = span > 1e-6 ? area / span : 1;
        }
        return m / Math.Max(w, 0.1);
    }

    /// <summary>Thanh lưới đáy cắt theo đa giác (hệ địa phương), nhóm theo chiều dài 50 mm.</summary>
    private static void AddShapedBars(FootingDesign d, List<(double X, double Y)> loc, double c, SpacingChoice sc, bool alongL)
    {
        double y0 = alongL ? loc.Min(q => q.Y) : loc.Min(q => q.X), y1 = alongL ? loc.Max(q => q.Y) : loc.Max(q => q.X);
        double dia = sc.Dia / 1000.0, hook = DesignUtil.R10(Math.Max(10 * dia, 0.10));
        var lens = new List<double>();
        int nLines = (int)Math.Ceiling((y1 - y0 - 2 * c) / sc.Spacing - 1e-9) + 1;
        for (int i = 0; i < nLines; i++)
        {
            double y = Math.Min(y0 + c + i * sc.Spacing, y1 - c);
            var ch = alongL ? PolyGeom.Chords(loc, 0, y, 1, 0) : PolyGeom.Chords(loc, y, 0, 0, 1);
            foreach (var (t1, t2) in ch)
            {
                double len = t2 - t1 - 2 * c;
                if (len < 0.2) continue;
                lens.Add(Math.Floor(len * 20 + 1e-6) / 20);   // nhóm 50 mm (cắt ngắn)
            }
        }
        string role = alongL ? "Lớp dưới phương L" : "Lớp dưới phương B";
        foreach (var grp in lens.GroupBy(x => Math.Round(x, 3)).OrderByDescending(gr => gr.Key))
        {
            double len = DesignUtil.R10(grp.Key);
            d.Bars.Add(new BarMark
            {
                Role = role, Dia = sc.Dia, Spacing = sc.Spacing, Count = grp.Count(), Length = DesignUtil.R10(len + 2 * hook), Shape = "U",
                Start = c, End = c + len, Segments = new List<double> { DesignUtil.Mm(hook), DesignUtil.Mm(len), DesignUtil.Mm(hook) }
            });
        }
    }

    /// <summary>Chữ ký móng hình dạng (gộp móng giống hệt): đường bao so với tim cột + góc.</summary>
    private static string ShapeSig(FootingDesign d)
    {
        if (!d.Shaped) return "";
        var sb = new System.Text.StringBuilder("|S").Append(Math.Round(d.Angle, 1).ToString(DesignUtil.Inv));
        foreach (var v in d.Outline) sb.Append(';').Append(Math.Round((v[0] - d.X) * 100).ToString(DesignUtil.Inv)).Append(',').Append(Math.Round((v[1] - d.Y) * 100).ToString(DesignUtil.Inv));
        return sb.ToString();
    }
}
