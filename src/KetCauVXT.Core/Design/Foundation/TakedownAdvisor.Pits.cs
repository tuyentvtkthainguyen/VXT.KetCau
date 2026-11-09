using KetCauVXT.Design.Concrete;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

// =====================================================================
// [CọcÉp + SátRanh + Hố móng]
//   • [P] sơ bộ của cọc ép theo lực ép đầu cọc thực tế: [P] = Pép/k (TCVN 9394:2012 – Pép,min = (1,5–2)·[P]); kiểm tra độ bền
//     vật liệu cọc Pvl = Rb·Ab + Rsc·As ≥ Pép (cọc không vỡ khi ép).
//   • Chân cột sát ranh đất: giữa / sát 1 ranh (lệch tâm 1 phương) / sát 2 ranh (cột góc, lệch tâm 2 phương). Móng (đài) lệch tâm
//     vào trong lô đất, dầm móng cân bằng nối cột trong: R1 = N·Ls/(Ls − e) mỗi phương.
//   • Hố móng: mỗi chân cột một đài; các đài chạm nhau (khe &lt; 0,2 m) gộp thành đài chung. Số cọc n = ⌈(1,1·ΣNtt·R1/N + Gđài)/[P]⌉.
// =====================================================================

public static partial class TakedownAdvisor
{
    /// <summary>Khe hở tối thiểu giữa hai đài cọc (m); nhỏ hơn → gộp hố móng</summary>
    public const double PitGap = 0.2;

    public static string EdgeText(ColumnFoundationSuggestion c)
    {
        if (c == null || c.Edge == EdgeKind.Interior) return "giữa";
        var s = new List<string>();
        if (c.EdgeLeft) s.Add("trái");
        if (c.EdgeRight) s.Add("phải");
        if (c.EdgeFront) s.Add("trước");
        if (c.EdgeBack) s.Add("sau");
        return (c.Edge == EdgeKind.TwoSides ? "sát 2 ranh – góc (2 phương): " : "sát 1 ranh (1 phương): ") + string.Join(" + ", s);
    }

    /// <summary>Kích thước cọc theo lực ép: ≤ 40 t → 200, ≤ 70 t → 250, ≤ 110 t → 300, lớn hơn → 350.</summary>
    public static double PressPileSize(double forceT) => forceT <= 40 + 1e-9 ? 0.20 : forceT <= 70 + 1e-9 ? 0.25 : forceT <= 110 + 1e-9 ? 0.30 : 0.35;

    /// <summary>Thép dọc cọc thường dùng: 200 → 4Ø14, 250 → 4Ø16, 300 → 4Ø18, 350 → 8Ø18.</summary>
    public static (int Bars, int Dia) PressPileRebar(double size) => size <= 0.2 + 1e-9 ? (4, 14) : size <= 0.25 + 1e-9 ? (4, 16) : size <= 0.3 + 1e-9 ? (4, 18) : (8, 18);

    /// <summary>[P] sơ bộ cọc ép theo lực ép đầu cọc (tấn) và hệ số k.</summary>
    public static PressPileInfo PressCapacity(double forceT, double k, double size = 0, string concrete = "B25", string rebar = "CB400-V")
    {
        var pi = new PressPileInfo { ForceT = forceT, K = k > 1 ? k : 2.0, Concrete = string.IsNullOrWhiteSpace(concrete) ? "B25" : concrete };
        pi.Size = size > 0.05 ? size : PressPileSize(forceT);
        (pi.Bars, pi.BarDia) = PressPileRebar(pi.Size);
        Material mc, ms;
        try { mc = Material.Concrete(pi.Concrete); } catch (Exception) { mc = Material.Concrete("B25"); pi.Concrete = "B25"; }
        try { ms = Material.Rebar(string.IsNullOrWhiteSpace(rebar) ? "CB400-V" : rebar); } catch (Exception) { ms = Material.Rebar("CB400-V"); }
        double As = pi.Bars * Math.PI * Math.Pow(pi.BarDia / 1000.0, 2) / 4;
        pi.Pvl = mc.Rb * pi.Size * pi.Size + ms.Rsc * As;
        double pep = pi.Force;
        if (pep > pi.Pvl)
        {
            pi.Notes.Add($"Lực ép {F(forceT, "0")} t > độ bền vật liệu cọc Pvl = {F(pi.Pvl / 10, "0")} t – tăng tiết diện / mác bê tông cọc; [P] lấy theo Pvl.");
            pep = pi.Pvl;
        }
        pi.P = Math.Floor(pep / pi.K / 5) * 5;
        pi.Notes.Add($"[P] = Pép/k = {F(forceT * 10, "0")}/{F(pi.K, "0.0#")} = {F(pi.P, "0")} kN ({F(pi.P / 10, "0.#")} t/cọc); cọc {F(pi.Size * 1000, "0")}×{F(pi.Size * 1000, "0")} "
                      + $"{pi.Concrete}, {pi.Bars}Ø{pi.BarDia}: Pvl = Rb·Ab + Rsc·As = {F(pi.Pvl, "0")} kN ≥ Pép = {F(forceT * 10, "0")} kN"
                      + (forceT * 10 <= pi.Pvl ? " – đạt" : " – KHÔNG ĐẠT"));
        pi.Notes.Add($"Thi công (TCVN 9394): ép đến Pép,min = {F(1.5 * pi.P / 10, "0")}–{F(2 * pi.P / 10, "0")} t và chiều dài thiết kế; dừng ép khi đạt Pép,max ≈ {F(Math.Min(3 * pi.P, pi.Pvl) / 10, "0")} t. "
                      + "Nén tĩnh kiểm tra ≥ 1% số cọc (≥ 2 cọc).");
        return pi;
    }

    // ------------------------------------------------------------------
    // CỘT SÁT RANH
    // ------------------------------------------------------------------
    /// <summary>Ranh đất dùng nhận cột sát ranh: ranh đã khai báo trong dự án; nếu chưa có → ranh ảo tại mép ngoài cột ngoài cùng
    /// theo các cạnh chọn (mặc định: hai cạnh dài của nhà phố – giáp nhà bên).</summary>
    public static List<PropertyBoundary> EdgeBoundaries(Project p, IList<ColumnTakedown> cols, IList<BoundarySide> sides, out bool isVirtual)
    {
        isVirtual = false;
        var res = new List<PropertyBoundary>();
        if (cols == null || cols.Count == 0) return res;
        var lines = EccentricFooting.Lines(p);
        if (lines.Count > 0 && sides == null)
        {
            foreach (var (b, c) in lines) { var q = b.Clone(); q.Grid = ""; q.Coord = c; res.Add(q); }
            return res;
        }
        isVirtual = true;
        double xmin = cols.Min(c => c.X), xmax = cols.Max(c => c.X), ymin = cols.Min(c => c.Y), ymax = cols.Max(c => c.Y);
        if (sides == null)
        {
            // nhà phố: hai cạnh dài giáp nhà bên
            sides = xmax - xmin <= ymax - ymin ? new[] { BoundarySide.Left, BoundarySide.Right } : new[] { BoundarySide.Front, BoundarySide.Back };
        }
        foreach (var sd in sides.Distinct())
        {
            var b = new PropertyBoundary { Side = sd };
            switch (sd)
            {
                case BoundarySide.Left:
                    b.Coord = cols.Where(c => c.X - xmin < 0.05).Min(c => c.X - Math.Max(c.Bx, 0.1) / 2);
                    break;
                case BoundarySide.Right:
                    b.Coord = cols.Where(c => xmax - c.X < 0.05).Max(c => c.X + Math.Max(c.Bx, 0.1) / 2);
                    break;
                case BoundarySide.Front:
                    b.Coord = cols.Where(c => c.Y - ymin < 0.05).Min(c => c.Y - Math.Max(c.By, 0.1) / 2);
                    break;
                default:
                    b.Coord = cols.Where(c => ymax - c.Y < 0.05).Max(c => c.Y + Math.Max(c.By, 0.1) / 2);
                    break;
            }
            // gắn trục ngoài cùng nếu có (để ranh đi theo trục khi sửa lưới)
            var gx = p?.Grids?.Where(g => g.Dir == (b.IsVertical ? GridDir.X : GridDir.Y)).ToList() ?? new List<GridLine>();
            double edgeAxis = sd switch { BoundarySide.Left => xmin, BoundarySide.Right => xmax, BoundarySide.Front => ymin, _ => ymax };
            var ga = gx.FirstOrDefault(g => Math.Abs(g.Coord - edgeAxis) < 0.01);
            if (ga != null) { b.Grid = ga.Name; b.Offset = Math.Round(b.Inward * (ga.Coord - b.Coord), 4); }
            res.Add(b);
        }
        return res;
    }

    /// <summary>Phân loại chân cột: giữa / sát 1 ranh / sát 2 ranh; khoảng cách tim cột tới ranh; nhịp dầm cân bằng tới cột trong.</summary>
    public static void ClassifyEdges(Project p, TakedownAdvice adv, IList<BoundarySide> sides)
    {
        var cols = adv.T.Columns;
        adv.Boundaries.Clear();
        adv.Boundaries.AddRange(EdgeBoundaries(p, cols, sides, out bool v));
        adv.VirtualBoundaries = v;
        double tol = Math.Max(p?.Foundation?.BoundaryTolerance ?? 0.15, 0.02);
        double smax = Math.Max(p?.Foundation?.StrapMaxSpan ?? 6, 1);
        foreach (var s in adv.Columns)
        {
            var c = s.T;
            double hx = Math.Max(c.Bx, 0.1) / 2, hy = Math.Max(c.By, 0.1) / 2;
            foreach (var b in adv.Boundaries)
            {
                switch (b.Side)
                {
                    case BoundarySide.Left:
                        if (c.X - hx - b.Coord <= tol + 1e-9 && c.X >= b.Coord - tol) { s.EdgeLeft = true; s.DistX = c.X - b.Coord; }
                        break;
                    case BoundarySide.Right:
                        if (b.Coord - (c.X + hx) <= tol + 1e-9 && c.X <= b.Coord + tol) { s.EdgeRight = true; s.DistX = b.Coord - c.X; }
                        break;
                    case BoundarySide.Front:
                        if (c.Y - hy - b.Coord <= tol + 1e-9 && c.Y >= b.Coord - tol) { s.EdgeFront = true; s.DistY = c.Y - b.Coord; }
                        break;
                    default:
                        if (b.Coord - (c.Y + hy) <= tol + 1e-9 && c.Y <= b.Coord + tol) { s.EdgeBack = true; s.DistY = b.Coord - c.Y; }
                        break;
                }
            }
            // sát cả hai ranh đối diện (nhà quá hẹp) → không lệch tâm theo phương đó
            if (s.EdgeLeft && s.EdgeRight) { s.EdgeLeft = s.EdgeRight = false; s.DistX = double.NaN; }
            if (s.EdgeFront && s.EdgeBack) { s.EdgeFront = s.EdgeBack = false; s.DistY = double.NaN; }
            bool ex = s.EdgeLeft || s.EdgeRight, ey = s.EdgeFront || s.EdgeBack;
            s.Edge = ex && ey ? EdgeKind.TwoSides : ex || ey ? EdgeKind.OneSide : EdgeKind.Interior;
            // cột trong cùng hàng, phía trong lô đất
            if (ex)
            {
                int dir = s.EdgeLeft ? 1 : -1;
                var inner = cols.Where(o => !ReferenceEquals(o, c) && Math.Abs(o.Y - c.Y) < 0.3 && (o.X - c.X) * dir > 0.5).OrderBy(o => Math.Abs(o.X - c.X)).FirstOrDefault();
                if (inner != null && Math.Abs(inner.X - c.X) <= smax + 1e-9) s.LsX = Math.Abs(inner.X - c.X);
            }
            if (ey)
            {
                int dir = s.EdgeFront ? 1 : -1;
                var inner = cols.Where(o => !ReferenceEquals(o, c) && Math.Abs(o.X - c.X) < 0.3 && (o.Y - c.Y) * dir > 0.5).OrderBy(o => Math.Abs(o.Y - c.Y)).FirstOrDefault();
                if (inner != null && Math.Abs(inner.Y - c.Y) <= smax + 1e-9) s.LsY = Math.Abs(inner.Y - c.Y);
            }
        }
    }

    /// <summary>Hệ số dầm móng cân bằng một phương: Ls/(Ls − e); không có cột trong (Ls = NaN) → móng tự cân bằng, hệ số ≈ 1 + 3e/a (a: cạnh đáy).</summary>
    public static double StrapFactor(double e, double Ls, double a)
    {
        if (e <= 1e-9) return 1;
        if (double.IsFinite(Ls) && Ls > e + 0.3) return Ls / (Ls - e);
        return 1 + 3 * e / Math.Max(a, 0.5);
    }

    /// <summary>Móng đơn lệch tâm cho cột sát ranh (đáy vuông b, mép sát ranh): e = b/2 − d, R1 = N·Π Ls/(Ls − e).</summary>
    internal static void EccentricIsolated(ColumnFoundationSuggestion sg, double net)
    {
        if (sg.Edge == EdgeKind.Interior || !double.IsFinite(sg.B) || net <= 5) return;
        double b = sg.B, f = 1;
        for (int it = 0; it < 30; it++)
        {
            double ex = double.IsFinite(sg.DistX) ? Math.Max(b / 2 - sg.DistX, 0) : 0;
            double ey = double.IsFinite(sg.DistY) ? Math.Max(b / 2 - sg.DistY, 0) : 0;
            f = StrapFactor(ex, sg.LsX, b) * StrapFactor(ey, sg.LsY, b);
            double bn = Math.Max(RcSection.CeilTo(Math.Sqrt(sg.T.Ntc * f / net), 0.1), FootingDesigner.MinSize);
            sg.Ex = ex; sg.Ey = ey;
            if (Math.Abs(bn - b) < 1e-9) break;
            b = bn;
        }
        sg.B = b; sg.StrapFactor = f;
        bool strap = (sg.Ex <= 1e-9 || double.IsFinite(sg.LsX)) && (sg.Ey <= 1e-9 || double.IsFinite(sg.LsY));
        sg.Note = (sg.Note.Length > 0 ? sg.Note + "; " : "") + EdgeText(sg) + $" – móng lệch tâm"
                  + (sg.Ex > 0 ? $" ex = {F(sg.Ex)} m" : "") + (sg.Ey > 0 ? $" ey = {F(sg.Ey)} m" : "")
                  + (strap ? $", dầm móng cân bằng (R1 = {F(f, "0.00")}·N)" : $", không có cột trong để nối dầm cân bằng – tăng đáy (×{F(f, "0.00")})");
    }

    // ------------------------------------------------------------------
    // HỐ MÓNG – SỐ CỌC
    // ------------------------------------------------------------------
    private static (int nx, int ny) PilePattern(int n, bool longX, bool row = false)
    {
        if (row && n <= 3) return longX ? (n, 1) : (1, n);
        (int a, int b) = n switch
        {
            1 => (1, 1), 2 => (2, 1), 3 => (3, 1), 4 => (2, 2), 5 => (3, 2), 6 => (3, 2), 7 or 8 or 9 => (3, 3),
            _ => ((int)Math.Ceiling(Math.Sqrt(n)), (int)Math.Ceiling(n / Math.Ceiling(Math.Sqrt(n))))
        };
        if (n == 3) (a, b) = (2, 2); // tam giác (cột giữa)
        return longX ? (a, b) : (b, a);
    }

    /// <summary>Tính số cọc từng hố móng (gộp đài chạm nhau), kể lệch tâm cột sát ranh.</summary>
    public static void PlanPits(Project p, TakedownAdvice adv, PileSuggestion ps)
    {
        adv.Pits.Clear();
        if (ps == null || ps.Capacity <= 0) return;
        var fs = p?.Foundation ?? new FoundationSettings();
        double d = ps.Size, s = Math.Max(fs.PileSpacingFactor, 3) * d, edge = Math.Max(0.7 * d, d / 2 + 0.15);
        double setback = Math.Max(fs.PileSetback, d / 2 + 0.1);
        double Df = adv.Df, P = ps.Capacity;

        FoundationPit Solve(List<ColumnFoundationSuggestion> mem)
        {
            var pit = new FoundationPit();
            pit.Cols.AddRange(mem);
            double N = mem.Sum(c => c.T.Ntt);
            bool single = mem.Count == 1;
            var c0 = mem[0];
            bool edX = single && double.IsFinite(c0.DistX), edY = single && double.IsFinite(c0.DistY);
            pit.Edge = single ? c0.Edge : mem.Max(c => c.Edge);
            // nhóm cột: tâm theo hợp lực
            double cx = N > 0 ? mem.Sum(c => c.T.X * c.T.Ntt) / N : mem.Average(c => c.T.X);
            double cy = N > 0 ? mem.Sum(c => c.T.Y * c.T.Ntt) / N : mem.Average(c => c.T.Y);
            double spanX = mem.Max(c => c.T.X) - mem.Min(c => c.T.X), spanY = mem.Max(c => c.T.Y) - mem.Min(c => c.T.Y);
            int n = Math.Max(1, (int)Math.Ceiling(PileK * N / P - 1e-9));
            double f = 1, ex = 0, ey = 0, lx = 0, ly = 0;
            int nx = 1, ny = 1;
            for (int it = 0; it < 12; it++)
            {
                // cột sát 1 ranh dọc (trái/phải): xếp cọc thành 1 hàng dọc ranh khi n ≤ 3 để giảm lệch tâm
                bool longX = edY && !edX ? true : edX && !edY ? false : spanX >= spanY;
                bool row = edX ^ edY;
                (nx, ny) = PilePattern(n, longX, row);
                if (n == 5) { lx = ly = s * Math.Sqrt(2) + 2 * edge; }
                else { lx = (nx - 1) * s + 2 * edge + spanX * (single ? 0 : 1); ly = (ny - 1) * s + 2 * edge + spanY * (single ? 0 : 1); }
                if (n == 3 && !row) { lx = s + 2 * edge; ly = s * 0.866 + 2 * edge; }
                // lệch tâm: tâm nhóm cọc cách ranh ≥ khoảng lùi + nửa nhóm
                ex = edX ? Math.Max(setback + (nx - 1) * s / 2 - c0.DistX, 0) : 0;
                ey = edY ? Math.Max(setback + (ny - 1) * s / 2 - c0.DistY, 0) : 0;
                // mép đài không vượt ranh
                if (edX) ex = Math.Max(ex, lx / 2 + Math.Max(fs.BoundaryClearance, 0) - c0.DistX);
                if (edY) ey = Math.Max(ey, ly / 2 + Math.Max(fs.BoundaryClearance, 0) - c0.DistY);
                f = StrapFactor(ex, single ? c0.LsX : double.NaN, lx) * StrapFactor(ey, single ? c0.LsY : double.NaN, ly);
                double gcap = 1.1 * 25 * Math.Max(Df, 0.8) * lx * ly; // đài + đất đắp (γ ≈ 20–25)
                int nn = Math.Max(1, (int)Math.Ceiling((PileK * N * f + gcap) / P - 1e-9));
                pit.Ndesign = PileK * N * f + gcap;
                if (nn == n) break;
                n = nn;
            }
            pit.Piles = n; pit.Nx = nx; pit.Ny = ny; pit.CapLx = lx; pit.CapLy = ly; pit.Factor = f; pit.Ex = ex; pit.Ey = ey;
            int dx = c0.EdgeLeft ? 1 : c0.EdgeRight ? -1 : 0, dy = c0.EdgeFront ? 1 : c0.EdgeBack ? -1 : 0;
            pit.Cx = cx + (single ? dx * ex : 0); pit.Cy = cy + (single ? dy * ey : 0);
            pit.EdgeText = single ? EdgeText(c0) : "đài chung " + mem.Count + " cột";
            if (single && pit.Edge != EdgeKind.Interior)
            {
                bool strap = (ex <= 1e-9 || double.IsFinite(c0.LsX)) && (ey <= 1e-9 || double.IsFinite(c0.LsY));
                pit.Note = $"đài lệch tâm vào trong lô{(ex > 0 ? $", ex = {F(ex)} m" : "")}{(ey > 0 ? $", ey = {F(ey)} m" : "")} (tim cọc cách ranh ≥ {F(setback)} m để ép); "
                           + (strap ? $"dầm móng cân bằng nối cột trong (Ls = {(double.IsFinite(c0.LsX) && ex > 0 ? F(c0.LsX) : F(c0.LsY))} m): R1 = N·Ls/(Ls − e) = {F(f, "0.00")}·N"
                                    : $"không có cột trong ≤ {F(fs.StrapMaxSpan)} m – đài tự cân bằng, tăng tải ×{F(f, "0.00")}");
            }
            else if (!single) pit.Note = "các đài chạm nhau (khe < " + F(PitGap) + " m) – gộp đài chung, hợp lực gần tâm đài";
            return pit;
        }

        // 1) đài riêng từng chân cột
        var pits = adv.Columns.Select(c => Solve(new List<ColumnFoundationSuggestion> { c })).ToList();
        // 2) gộp các đài chạm nhau
        for (int guard = 0; guard < 200; guard++)
        {
            bool merged = false;
            for (int i = 0; i < pits.Count && !merged; i++)
                for (int j = i + 1; j < pits.Count && !merged; j++)
                {
                    var a = pits[i]; var b = pits[j];
                    double gx = Math.Abs(a.Cx - b.Cx) - (a.CapLx + b.CapLx) / 2, gy = Math.Abs(a.Cy - b.Cy) - (a.CapLy + b.CapLy) / 2;
                    if (gx < PitGap && gy < PitGap)
                    {
                        var mem = a.Cols.Concat(b.Cols).ToList();
                        pits.RemoveAt(j); pits.RemoveAt(i);
                        pits.Add(Solve(mem));
                        merged = true;
                    }
                }
            if (!merged) break;
        }
        int k = 1;
        foreach (var pit in pits.OrderBy(q => Math.Round(q.Cy, 1)).ThenBy(q => q.Cx))
        {
            pit.Name = "HM" + k++;
            adv.Pits.Add(pit);
        }
    }
}
