using System.Text.Json;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>[MóngLệchTâm] Kiểm chứng móng lệch tâm do ranh giới đất (nhà phố 2 nhịp, ranh giới trái + phải):
/// nhận cột sát ranh (biên, góc), móng sát ranh (mép móng = ranh giới), áp lực lệch tâm (lõi / tách nền một phần),
/// đòn bẩy dầm cân bằng R1 = N1·L/(L − e), giảm tải móng cột trong, M = N1·e, móng kết hợp (hợp lực – trọng tâm),
/// đài cọc lệch tâm (cân bằng lực / mô men), móng băng bản cánh một phía, JSON, bản vẽ, khối lượng, thuyết minh.
/// check(tên, giá trị, kỳ vọng, sai số tương đối): đạt khi |got − exp| ≤ tol·max(1, |exp|).</summary>
public static class EccentricFootingTests
{
    private static double B(bool x) => x ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Formulas(check);
        var (p, r) = TownHouse();
        Detection(check, p);
        Isolated(check, p, r, outDir);
        PileCaps(check, p, r);
        Strips(check, p, r);
        Json(check, p);
    }

    /// <summary>Nhà phố 2 nhịp 3,6 + 3,6 m (X) × 4,5 + 4,5 m (Y), 3 tầng, cột 220×220; ranh giới trái / phải tại mép ngoài cột trục 1 và 3.</summary>
    private static (Project, AnalysisResults) TownHouse()
    {
        var p = ModelBuilder.NewProject("Nhà phố – móng lệch tâm");
        ModelBuilder.SetGrid(p, new[] { 3.6, 3.6 }, new[] { 4.5, 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x22", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5 });
        var r = FrameAnalysis.Run(p);
        var gx = p.GridsX.ToList();
        p.Foundation.Type = FoundationType.Isolated;
        p.Foundation.Boundaries = new List<PropertyBoundary>
        {
            new() { Side = BoundarySide.Left, Grid = gx[0].Name, Offset = 0.11 },
            new() { Side = BoundarySide.Right, Grid = gx[^1].Name, Offset = 0.11 }
        };
        return (p, r);
    }

    // ------------------------------------------------------------------
    private static void Formulas(Action<string, double, double, double> check)
    {
        // lõi tiết diện: N = 600, M = 60, L = 2, B = 1,5 → e = 0,1 ≤ L/6
        var pr = EccentricFooting.Pressure(600, 60, 0, 2.0, 1.5);
        check("Lệch tâm trong lõi: pmax = N/A·(1 + 6e/L) = 260", pr.Pmax, 600 / 3.0 * (1 + 6 * 0.1 / 2), 1e-9);
        check("Lệch tâm trong lõi: pmin = N/A·(1 − 6e/L) = 140", pr.Pmin, 600 / 3.0 * (1 - 6 * 0.1 / 2), 1e-9);
        check("Lệch tâm trong lõi: tiếp xúc 100%", pr.Contact, 1, 1e-12);
        // tách nền một phần: M = 300 → e = 0,5 > L/6, c0 = 0,5
        var pp = EccentricFooting.Pressure(600, 300, 0, 2.0, 1.5);
        check("Tách nền: pmax = 2N/(3B·c0) = 533,33", pp.Pmax, 2 * 600 / (3 * 1.5 * 0.5), 1e-9);
        check("Tách nền: trùng FootingBehaviour.PartialContactPmax", pp.Pmax, FootingBehaviour.PartialContactPmax(600, 0.5, 2.0, 1.5), 1e-9);
        check("Tách nền: vùng tiếp xúc 3c0/L = 75%", pp.Contact, 0.75, 1e-9);
        check("Tách nền: đánh dấu Partial", B(pp.Partial), 1, 0);
        check("Điều kiện: tiếp xúc 75% chấp nhận (pmax ≤ 1,2R)", B(EccentricFooting.PressureOk(pp, 500)), 1, 0);

        // đòn bẩy dầm cân bằng
        double N1 = 500, L = 4, e = 0.6;
        double R1 = EccentricFooting.LeverReaction(N1, 0, L, e);
        check("Dầm cân bằng: R1 = N1·L/(L − e) = 588,24", R1, N1 * L / (L - e), 1e-12);
        check("Dầm cân bằng: ΔR = N1·e/(L − e)", R1 - N1, N1 * e / (L - e), 1e-9);
        check("Dầm cân bằng: N2' = N2 − 0,5·ΔR", EccentricFooting.InteriorReduced(800, R1 - N1, 0.5), 800 - 0.5 * (R1 - N1), 1e-12);
        check("Dầm cân bằng: β > 1 bị giới hạn 1", EccentricFooting.InteriorReduced(800, R1 - N1, 3), 800 - (R1 - N1), 1e-12);
        check("Dầm cân bằng: M = N1·e = 300 kN.m", EccentricFooting.StrapMoment(N1, e), 300, 1e-12);
        // hai móng biên đối diện: cân bằng lực + mô men
        double Na = 500, Nb = 650, e1 = 0.5, e2 = 0.4, Lab = 4.2;
        double Ra = EccentricFooting.LeverReaction(Na, Nb, Lab, e1, e2), Rb = EccentricFooting.LeverReaction(Nb, Na, Lab, e2, e1);
        check("Hai móng biên: R1 + R2 = N1 + N2", Ra + Rb, Na + Nb, 1e-9);
        check("Hai móng biên: ΣM tại cột 1 = 0 (R1·e1 + R2·(L − e2) = N2·L)", Ra * e1 + Rb * (Lab - e2), Nb * Lab, 1e-9);

        // móng kết hợp
        double xb = EccentricFooting.CombinedResultant(400, 0.11, 800, 3.71);
        check("Móng kết hợp: x̄ = (N1x1 + N2x2)/(N1 + N2)", xb, (400 * 0.11 + 800 * 3.71) / 1200, 1e-12);
        var (b1, b2) = EccentricFooting.TrapezoidWidths(6, 4, 1.6);
        check("Móng hình thang: B1 + B2 = 2A/L", b1 + b2, 3, 1e-12);
        check("Móng hình thang: B2 = (2A/L)·(3x̄/L − 1) = 0,6", b2, 0.6, 1e-12);
        check("Móng hình thang: trọng tâm L(B1 + 2B2)/(3(B1 + B2)) = x̄", EccentricFooting.TrapezoidCentroid(4, b1, b2), 1.6, 1e-12);
        // chọc thủng cột sát mép: chu vi 3 phía
        EccentricFooting.Punching(500, 1.6, 2.0, 0.22, 0.22, 0.69, 0, 0.35, 1050, out double F, out double Fu, out double u);
        check("Chọc thủng cột sát mép: u = 3 phía = 2(hc + h0/2) + (bc + h0)", u, 2 * (0.22 + 0.175) + (0.22 + 0.35), 1e-9);
        check("Chọc thủng cột sát mép: Fb,u = Rbt·u·h0", Fu, 1050 * u * 0.35, 1e-9);
        double ap = (0.22 + 0.35) * (0.22 + 0.7);   // tháp bị cắt phía mép móng
        check("Chọc thủng cột sát mép: F = N − N/A·A_tháp (tháp bị cắt)", F, 500 - 500 / 3.2 * ap, 1e-9);
    }

    // ------------------------------------------------------------------
    private static void Detection(Action<string, double, double, double> check, Project p)
    {
        var cols = FoundationUtil.BaseColumns(p);
        var gx = p.GridsX.ToList(); var gy = p.GridsY.ToList();
        check("Nhà phố: 9 chân cột", cols.Count, 9, 0);
        check("Ranh giới: 2 cạnh, trái x = −0,11", EccentricFooting.Lines(p).First(l => l.B.Side == BoundarySide.Left).Coord, -0.11, 1e-12);
        check("Ranh giới: phải x = 7,31", EccentricFooting.Lines(p).First(l => l.B.Side == BoundarySide.Right).Coord, 7.31, 1e-12);
        int left = 0, right = 0, inner = 0;
        foreach (var (n, _) in cols)
        {
            var lim = EccentricFooting.Limits(p, n.X, n.Y, 0.22, 0.22);
            if (lim.EdgeLeft) left++;
            if (lim.EdgeRight) right++;
            if (!lim.EdgeX && !lim.EdgeY) inner++;
        }
        check("Nhận cột sát ranh trái: 3 cột trục 1", left, 3, 0);
        check("Nhận cột sát ranh phải: 3 cột trục 3", right, 3, 0);
        check("Cột không sát ranh: 3 cột trục 2", inner, 3, 0);
        // móng sát ranh: mép trái móng = ranh giới
        var n0 = cols.First(c => Math.Abs(c.Node.X - gx[0].Coord) < 1e-6 && Math.Abs(c.Node.Y - gy[1].Coord) < 1e-6).Node;
        var l0 = EccentricFooting.Limits(p, n0.X, n0.Y, 0.22, 0.22);
        var (ox, oy) = EccentricFooting.Offset(l0, n0.X, n0.Y, 1.6, 2.0, 0.22, 0.22);
        check("Móng sát ranh: ex = L/2 − (x − x_ranh) = 0,69 m", ox, 0.69, 1e-9);
        check("Móng sát ranh: mép móng x = ranh giới", n0.X + ox - 1.6 / 2, -0.11, 1e-9);
        check("Móng sát ranh: không lệch theo Y", oy, 0, 1e-12);
        // cột góc: thêm ranh giới trước → lệch tâm 2 phương
        var fsSave = p.Foundation.Boundaries.ToList();
        p.Foundation.Boundaries.Add(new PropertyBoundary { Side = BoundarySide.Front, Grid = gy[0].Name, Offset = 0.11 });
        int corner = cols.Count(c => { var lim = EccentricFooting.Limits(p, c.Node.X, c.Node.Y, 0.22, 0.22); return lim.EdgeX && lim.EdgeY; });
        check("Cột góc (trái/phải + trước): 2 cột lệch tâm 2 phương", corner, 2, 0);
        var nc = cols.First(c => Math.Abs(c.Node.X - gx[0].Coord) < 1e-6 && Math.Abs(c.Node.Y - gy[0].Coord) < 1e-6).Node;
        var (cx, cy) = EccentricFooting.Offset(EccentricFooting.Limits(p, nc.X, nc.Y, 0.22, 0.22), nc.X, nc.Y, 1.4, 1.4, 0.22, 0.22);
        check("Cột góc: ex = ey = 0,59 m (móng 1,4 × 1,4 sát 2 ranh)", cx + cy, 2 * (0.7 - 0.11), 1e-9);
        p.Foundation.Boundaries = fsSave;
        // cột trục 2 với móng lớn hơn khoảng cách tới ranh: không dịch
        var nm = cols.First(c => Math.Abs(c.Node.X - gx[1].Coord) < 1e-6).Node;
        check("Cột trong: móng 2 × 2 không phải dịch", EccentricFooting.Offset(EccentricFooting.Limits(p, nm.X, nm.Y, 0.22, 0.22), nm.X, nm.Y, 2, 2, 0.22, 0.22).Ox, 0, 1e-12);
    }

    private static EccentricLocation Loc(DesignSummary s, int node) =>
        s.Footings.Where(f => f.Eccentric != null).SelectMany(f => f.Eccentric.Locations).FirstOrDefault(l => l.NodeId == node);

    private static FootingDesign FootOf(DesignSummary s, int node) =>
        s.Footings.FirstOrDefault(f => f.NodeId == node || f.RebarKeys.Contains("M:" + node) || (f.Eccentric?.Locations.Any(l => l.NodeId == node) ?? false));

    // ------------------------------------------------------------------
    private static void Isolated(Action<string, double, double, double> check, Project p, AnalysisResults r, string outDir)
    {
        var gx = p.GridsX.ToList(); var gy = p.GridsY.ToList();
        var cols = FoundationUtil.BaseColumns(p);
        var combos = r.DesignCombos();
        int Node(int i, int j) => cols.First(c => Math.Abs(c.Node.X - gx[i].Coord) < 1e-6 && Math.Abs(c.Node.Y - gy[j].Coord) < 1e-6).Node.Id;
        p.Foundation.EccentricDefault = EccentricScheme.Auto;
        var s = ConcreteDesigner.Design(p, r);
        check("Móng lệch tâm: không lỗi thiết kế móng", s.Warnings.Count(w => w.StartsWith("LỖI thiết kế")), 0, 0);
        check("Móng lệch tâm: tổng số móng (kể gộp) = 9", s.Footings.Sum(f => Math.Max(1, f.Quantity)), 9, 0);
        int nEdge = 0;
        foreach (int i in new[] { 0, 2 })
            for (int j = 0; j < 3; j++)
            {
                int id = Node(i, j);
                var loc = Loc(s, id);
                var f = FootOf(s, id);
                if (loc == null || f == null) { check($"Móng biên nút {id}: có thông tin lệch tâm", 0, 1, 0); continue; }
                nEdge++;
                double edgeX = i == 0 ? loc.X + loc.OffsetX - f.L / 2 : loc.X + loc.OffsetX + f.L / 2;
                check($"Móng biên nút {id}: mép móng sát ranh giới x = {(i == 0 ? -0.11 : 7.31):0.00}", edgeX, i == 0 ? -0.11 : 7.31, 1e-3);
                check($"Móng biên nút {id}: lệch tâm vào trong lô đất", B(i == 0 ? loc.OffsetX > 0 : loc.OffsetX < 0), 1, 0);
            }
        check("Móng lệch tâm: 6 vị trí móng biên", nEdge, 6, 0);
        // (a) dầm cân bằng: đòn bẩy
        var fe = FootOf(s, Node(0, 1));
        var ei = fe.Eccentric;
        Console.WriteLine($"   · {fe.Name}: {EccentricOverride.SchemeName(ei.Scheme)}, {fe.L:0.00}×{fe.B:0.00}×{fe.H:0.00} m, ex = {ei.OffsetX * 1000:0} mm, N1 = {ei.N1:0.0}, R1 = {ei.R1:0.0} kN, pmax = {fe.Pmax:0.0} kN/m², {(fe.Ok ? "đạt" : "KHÔNG ĐẠT")}");
        check("Tự chọn: cột biên có cột trong cách 3,6 m → dầm cân bằng (a)", B(ei.Scheme == EccentricScheme.Strap), 1, 0);
        check("Dầm cân bằng: nối với cột trục 2", ei.PartnerX, Node(1, 1), 0);
        check("Dầm cân bằng: R1 = N1·L/(L − e) (đòn bẩy)", ei.R1, EccentricFooting.LeverReaction(ei.N1, 0, 3.6, Math.Abs(ei.OffsetX)), 1e-9);
        check("Móng biên có dầm cân bằng: đạt", B(fe.Ok), 1, 0);
        check("Dầm móng cân bằng được thiết kế", B(s.StrapBeams.Count > 0), 1, 0);
        check("Dầm cân bằng: tổng số dầm (kể gộp) = 6", s.StrapBeams.Sum(x => x.Quantity), 6, 0);
        foreach (var sb in s.StrapBeams)
        {
            var l = sb.Links[0];
            double mExp = 0;
            foreach (var a in l.LoadsA) mExp = Math.Max(mExp, a.N * l.E1 + Math.Abs(a.My) + Math.Abs(a.Qx) * 0.5);
            Console.WriteLine($"   · {sb.Name} ×{sb.Quantity}: {sb.B * 1000:0}×{sb.H * 1000:0}, L = {sb.Span:0.00}, e1 = {sb.E1 * 1000:0} mm, M = {sb.M:0.0}, Q = {sb.V:0.0}, {sb.TopBars}/{sb.BottomBars}, {sb.Stirrups}, {(sb.Ok ? "đạt" : "KHÔNG ĐẠT")}");
            check($"{sb.Name}: M = max(N1·e1 + |M0|)", sb.M, mExp, 1e-9);
            check($"{sb.Name}: M ≥ N1·e1 (tổ hợp N lớn nhất)", B(sb.M >= l.LoadsA.Max(a => a.N) * l.E1 - 1e-9), 1, 0);
            check($"{sb.Name}: Q = R1 − N1", sb.V, l.LoadsA.Max(a => Math.Abs(EccentricFooting.LeverReaction(a.N, 0, l.Span, l.E1) - a.N)), 1e-9);
            check($"{sb.Name}: dầm cứng hơn cột I/L ≥ 2Ic/Hc", B(sb.StiffBeam >= sb.StiffColumn - 1e-12), 1, 0);
            check($"{sb.Name}: số hiệu thép hợp lệ", RebarNumbering.Validate(sb.Bars).Count, 0, 0);
        }
        // móng cột trong được giảm tải N2' = N2 − β·ΣΔR
        int mid = Node(1, 1);
        var fm = FootOf(s, mid);
        var mi = fm?.Eccentric;
        check("Móng cột trong: có thông tin giảm tải", B(mi != null && mi.Relief > 0), 1, 0);
        if (mi != null)
        {
            check("Móng cột trong: giảm tải từ 2 móng biên", mi.ReliefFrom.Count, 2, 0);
            var lm = FoundationUtil.Loads(r, mid, combos).OrderByDescending(x => x.N).First();
            double sum = 0;
            foreach (int eid in mi.ReliefFrom)
            {
                var la = FoundationUtil.Loads(r, eid, combos).First(x => x.Combo == lm.Combo);
                var loc = Loc(s, eid);
                double dR = EccentricFooting.LeverReaction(la.N, 0, 3.6, Math.Abs(loc.OffsetX)) - la.N;
                if (dR > 0) sum += dR;
            }
            check("Móng cột trong: giảm tải = β·Σ(R1 − N1), β = 0,5", mi.Relief, 0.5 * sum, 1e-9);
        }

        // (b) tự cân bằng tại một móng biên: kiểm tra áp lực theo công thức lệch tâm
        int nb = Node(0, 0);
        EccentricFooting.Set(p.Foundation, new[] { nb }, new EccentricOverride { Scheme = EccentricScheme.SelfBalanced });
        var sbal = ConcreteDesigner.Design(p, r);
        var fb = FootOf(sbal, nb);
        var lb = Loc(sbal, nb);
        Console.WriteLine($"   · tự cân bằng {fb.Name}: {fb.L:0.00}×{fb.B:0.00}, ex = {lb.OffsetX * 1000:0} mm, ptb = {fb.Ptb:0.0}, pmax = {Math.Min(fb.Pmax, 99999):0.0}, tiếp xúc {fb.Eccentric.Contact * 100:0}% → {(fb.Ok ? "đạt" : "KHÔNG ĐẠT")}");
        check("Tự cân bằng: phương án (b)", B(fb.Eccentric.Scheme == EccentricScheme.SelfBalanced), 1, 0);
        check("Tự cân bằng: đề xuất của chương trình vẫn là (a)", B(fb.Eccentric.Recommended == EccentricScheme.Strap), 1, 0);
        var so = p.Soil;
        double kt = Math.Max(so.LoadFactorAvg, 1), A = fb.L * fb.B, pmax = double.MinValue;
        foreach (var l in FoundationUtil.Loads(r, nb, combos))
        {
            double ml = Math.Abs(l.My) + Math.Abs(l.Qx) * so.Depth + Math.Abs(l.N * lb.OffsetX), mb = Math.Abs(l.Mx) + Math.Abs(l.Qy) * so.Depth;
            pmax = Math.Max(pmax, EccentricFooting.Pressure(l.N / kt + so.GammaAvg * so.Depth * A, ml / kt, mb / kt, fb.L, fb.B).Pmax);
        }
        check("Tự cân bằng: pmax theo công thức lệch tâm (M = N·e0 + M0 + Q·Df)", Math.Min(fb.Pmax, 1e9), Math.Min(pmax, 1e9), 1e-9);
        var g = FoundationUtil.Loads(r, nb, combos).OrderByDescending(x => x.N).First();
        check("Tự cân bằng: ΔM = N·e0 truyền vào chân cột", fb.Eccentric.ColumnMomentX, Math.Abs(g.N * lb.OffsetX), 1e-9);
        check("Tự cân bằng: cảnh báo mô men chân cột / giằng", B(fb.Messages.Any(m => m.Contains("ΔM = N·e0"))), 1, 0);
        check("Tự cân bằng: móng sát ranh không thoả tiếp xúc → báo LỖI + đề xuất (a)/(c)", B(fb.Ok || fb.Messages.Any(m => m.StartsWith("LỖI") && m.Contains("dầm móng cân bằng"))), 1, 0);

        // (c) móng kết hợp
        int nk = Node(0, 2);
        EccentricFooting.Set(p.Foundation, new[] { nb }, null);
        EccentricFooting.Set(p.Foundation, new[] { nk }, new EccentricOverride { Scheme = EccentricScheme.Combined });
        var sk = ConcreteDesigner.Design(p, r);
        var fk = sk.Footings.FirstOrDefault(f => f.Eccentric?.Combined != null);
        check("Móng kết hợp: được lập", B(fk != null), 1, 0);
        if (fk != null)
        {
            var c = fk.Eccentric.Combined;
            Console.WriteLine($"   · móng kết hợp {fk.Name}: {(c.Trapezoid ? "hình thang" : "chữ nhật")} L = {c.Length:0.00}, B1 = {c.B1:0.00}, B2 = {c.B2:0.00}, x̄ = {c.Resultant:0.000}, trọng tâm {c.Centroid:0.000}, ptb = {fk.Ptb:0.0}, pmax = {fk.Pmax:0.0} → {(fk.Ok ? "đạt" : "KHÔNG ĐẠT")}");
            check("Móng kết hợp: gộp cột biên + cột trục 2", c.NodeB, Node(1, 2), 0);
            check("Móng kết hợp: x̄ = (N1·x1 + N2·x2)/(N1 + N2) từ mép ranh giới", c.Resultant, EccentricFooting.CombinedResultant(c.N1, 0.11, c.N2, 3.71), 1e-9);
            check("Móng kết hợp: trọng tâm đáy móng ≈ hợp lực (sai số làm tròn ≤ 50 mm)", B(Math.Abs(c.Centroid - c.Resultant) <= 0.05 || c.Length >= 3.71 + 0.11 + 0.3 - 1e-9), 1, 0);
            check("Móng kết hợp: mép móng tại ranh giới", c.Start, -0.11, 1e-9);
            check("Móng kết hợp: cột trong không còn móng riêng", s.Footings.Count == 0 ? 0 : sk.Footings.Count(f => f.NodeId == c.NodeB), 0, 0);
            check("Móng kết hợp: tổng số chân cột được đỡ = 9", sk.Footings.Sum(f => Math.Max(1, f.Quantity) + (f.Eccentric?.Combined != null ? 1 : 0)), 9, 0);
        }
        EccentricFooting.Set(p.Foundation, new[] { nk }, null);

        // ---- bản vẽ, khối lượng, thuyết minh ----
        string dir = Path.Combine(string.IsNullOrEmpty(outDir) ? Path.GetTempPath() : outDir, "MongLechTam");
        try
        {
            Directory.CreateDirectory(dir);
            var figs = FootingDrawing.BuildAll(p, s);
            check("Bản vẽ móng lệch tâm: xuất DXF mọi hình", figs.Count(f => f.W.ToDxfString().Length > 0), figs.Count, 0);
            string plan = figs[0].W.ToDxfString();
            check("Bản vẽ: mặt bằng móng có chữ 'RANH GIỚI ĐẤT'", B(plan.Contains(DxfWriter.Encode(EccentricDrawing.BoundaryLabel))), 1, 0);
            check("Bản vẽ: mặt bằng móng có nhãn dầm cân bằng DM1 b×h", B(plan.Contains("DM1 (") && plan.Contains(DxfWriter.Encode("×"))), 1, 0);
            check("Bản vẽ: có chi tiết dầm cân bằng (mặt đứng + mặt cắt)", figs.Count(f => f.Name.StartsWith("DMCB")), 2 * s.StrapBeams.Count, 0);
            string el = figs.First(f => f.Name.StartsWith("DMCB")).W.ToDxfString();
            var tb0 = s.StrapBeams[0].Bars.First(x => x.Role == "Trên");
            check("Bản vẽ: nhãn thép dầm cân bằng kiểu Kết cấu Soft (nØd + Øa)", B(el.Contains(DxfWriter.Encode($"{tb0.Count}Ø{tb0.Dia}")) && el.Contains(DxfWriter.Encode(s.StrapBeams[0].Stirrups))), 1, 0);
            if (fk != null)
            {
                var kf = FootingDrawing.BuildAll(p, sk);
                check("Bản vẽ móng kết hợp: xuất DXF", kf.Count(f => f.W.ToDxfString().Length > 0), kf.Count, 0);
                check("Bản vẽ móng kết hợp: có tiêu đề MÓNG KẾT HỢP", B(kf.Any(f => f.W.ToDxfString().Contains(DxfWriter.Encode("MÓNG KẾT HỢP")))), 1, 0);
            }
            File.WriteAllText(Path.Combine(dir, "mb_mong_lech_tam.dxf"), plan);
            var q = QuantityCalculator.Compute(p, s);
            var dm = q.Concrete.Where(c => c.Member.StartsWith("DM")).ToList();
            check("Khối lượng: có bê tông dầm cân bằng", B(dm.Count == s.StrapBeams.Count), 1, 0);
            check("Khối lượng: dầm cân bằng thuộc nhóm Giằng móng", B(dm.All(c => c.Category == QtyCat.TieBeam)), 1, 0);
            check("Khối lượng: V = Σ n·b·h·Ltt", dm.Sum(c => c.Volume), s.StrapBeams.Sum(x => x.Quantity * x.B * x.H * x.ClearLength), 1e-9);
            check("Khối lượng: thép dầm cân bằng trong bảng thống kê", B(q.RebarRows.Any(rw => rw.Member.StartsWith("DM") && rw.Category == "Giằng móng" && rw.WeightKg > 0)), 1, 0);
            CalculationReport.Export(p, r, s, Path.Combine(dir, "tm_mong_lech_tam.docx"));
            check("Thuyết minh có mục móng lệch tâm (xuất được)", B(File.Exists(Path.Combine(dir, "tm_mong_lech_tam.docx"))), 1, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("   · lỗi bản vẽ / khối lượng / thuyết minh móng lệch tâm: " + ex);
            check("Bản vẽ / khối lượng / thuyết minh móng lệch tâm", 0, 1, 0);
        }
    }

    // ------------------------------------------------------------------
    private static void PileCaps(Action<string, double, double, double> check, Project p, AnalysisResults r)
    {
        var save = JsonSerializer.Serialize(p.Foundation, Project.JsonOptions);
        p.Foundation.Type = FoundationType.Pile;
        p.Foundation.PileCapacity = 300;
        p.Foundation.PileSize = 0.25;
        p.Foundation.EccentricDefault = EccentricScheme.SelfBalanced;
        var s = ConcreteDesigner.Design(p, r);
        check("Đài lệch tâm: không lỗi thiết kế đài", s.Warnings.Count(w => w.StartsWith("LỖI thiết kế đài")), 0, 0);
        var ecc = s.PileCaps.Where(c => Math.Abs(c.OffsetX) > 1e-6).ToList();
        check("Đài lệch tâm: có đài lệch tâm tại ranh giới", B(ecc.Count > 0), 1, 0);
        foreach (var c in ecc)
        {
            bool leftSide = c.OffsetX > 0;
            double edge = leftSide ? c.X + c.OffsetX - c.L / 2 : c.X + c.OffsetX + c.L / 2;
            Console.WriteLine($"   · {c.Name}: {c.Piles.Count} cọc, đài {c.L:0.00}×{c.B:0.00}, ex = {c.OffsetX * 1000:0} mm, Pmax = {c.PMax:0.0}, Pmin = {c.PMin:0.0} kN, {(c.Ok ? "đạt" : "KHÔNG ĐẠT")}");
            check($"{c.Name}: mép đài sát ranh giới", edge, leftSide ? -0.11 : 7.31, 1e-3);
            check($"{c.Name}: mọi cọc nằm trong ranh giới", B(c.Piles.All(q => leftSide ? c.X + q.X - c.PileSize / 2 >= -0.11 - 1e-9 : c.X + q.X + c.PileSize / 2 <= 7.31 + 1e-9)), 1, 0);
            check($"{c.Name}: ΣPi = N + G", c.Reactions.Sum(), c.N + c.Weight, 1e-6);
            double sx = 0, sy = 0;
            for (int i = 0; i < c.Piles.Count; i++) { sx += c.Piles[i].X * c.Reactions[i]; sy += c.Piles[i].Y * c.Reactions[i]; }
            if (c.Piles.Select(q => q.X).Distinct().Count() > 1) check($"{c.Name}: Σxi·Pi = −My − H·Qx (mô men đối với tim cột)", sx, -c.My - c.H * c.Qx, 1e-6);
            if (c.Piles.Select(q => q.Y).Distinct().Count() > 1) check($"{c.Name}: Σyi·Pi = Mx − H·Qy", sy, c.Mx - c.H * c.Qy, 1e-6);
            check($"{c.Name}: trọng tâm nhóm cọc = tâm đài (lệch so với tim cột)", c.Piles.Average(q => q.X), c.OffsetX, 1e-6);
            var it = c.Safety?.Items.FirstOrDefault(i => i.Name.StartsWith("Lệch tâm nhóm cọc"));
            check($"{c.Name}: 'Lệch tâm do ranh giới' là trạng thái chủ ý (Thông tin)", B(it != null && it.Name.Contains("Lệch tâm do ranh giới") && it.Status == CheckStatus.Info), 1, 0);
            var edgeItem = c.Safety?.Items.FirstOrDefault(i => i.Name.StartsWith("Mép cọc tới mép đài"));
            check($"{c.Name}: mép cọc – mép đài tính theo tâm đài lệch", B(edgeItem != null && edgeItem.Status == CheckStatus.Ok), 1, 0);
        }
        // đài có dầm cân bằng: phản lực đều theo phương dầm
        p.Foundation.EccentricDefault = EccentricScheme.Auto;
        var s2 = ConcreteDesigner.Design(p, r);
        var st = s2.PileCaps.Where(c => c.Eccentric?.Scheme == EccentricScheme.Strap).ToList();
        check("Đài có dầm cân bằng: có đài + dầm DM", B(st.Count > 0 && s2.StrapBeams.Count > 0), 1, 0);
        foreach (var c in st.Take(2))
        {
            double gxm = c.Piles.Average(q => q.X), m = 0;
            for (int i = 0; i < c.Piles.Count; i++) m += (c.Piles[i].X - gxm) * c.Reactions[i];
            check($"{c.Name} (dầm cân bằng): Σ(xi − x̄)·Pi = 0 – phản lực đều theo phương dầm", m, 0, 1e-6);
            check($"{c.Name} (dầm cân bằng): ΣPi = R1 + G (R1 = N·L/(L − e))", c.Reactions.Sum(), c.N + c.Weight, 1e-6);
        }
        p.Foundation = JsonSerializer.Deserialize<FoundationSettings>(save, Project.JsonOptions);
    }

    // ------------------------------------------------------------------
    private static void Strips(Action<string, double, double, double> check, Project p, AnalysisResults r)
    {
        var save = JsonSerializer.Serialize(p.Foundation, Project.JsonOptions);
        p.Foundation.Type = FoundationType.Strip;
        p.Foundation.StripDirection = 2;   // dải dọc Y: trên trục 1, 2, 3
        p.Soil.ROverride = 150;
        try
        {
            var s = ConcreteDesigner.Design(p, r);
            check("Móng băng lệch tâm: không lỗi", s.Warnings.Count(w => w.StartsWith("LỖI thiết kế móng băng")), 0, 0);
            var left = s.StripFootings.FirstOrDefault(x => Math.Abs((x.FlangeSide != 0 ? x.ColumnLine : x.C) - 0) < 1e-6);
            var right = s.StripFootings.FirstOrDefault(x => Math.Abs((x.FlangeSide != 0 ? x.ColumnLine : x.C) - 7.2) < 1e-6);
            var midS = s.StripFootings.FirstOrDefault(x => x.FlangeSide == 0);
            check("Móng băng trục 1: bản cánh một phía (+X)", left?.FlangeSide ?? 0, 1, 0);
            check("Móng băng trục 3: bản cánh một phía (−X)", right?.FlangeSide ?? 0, -1, 0);
            check("Móng băng trục 2: bản cánh hai phía", B(midS != null), 1, 0);
            if (left != null)
            {
                Console.WriteLine($"   · {left.Name}: B = {left.Width:0.00}, sườn {left.RibB * 1000:0}×{left.RibH * 1000:0}, e = {left.Eccentricity * 1000:0} mm, t = {left.TorsionPerM:0.0} kN.m/m, T = {left.TorsionMax:0.0} kN.m");
                check("Móng băng lệch tâm: mép bản đáy sát ranh giới", left.C - left.Width / 2, -0.11, 1e-9);
                check("Móng băng lệch tâm: mép sườn sát ranh giới", left.RibC - left.RibB / 2, -0.11, 1e-9);
                check("Móng băng lệch tâm: e = |tim cột − tâm bản đáy|", left.Eccentricity, Math.Abs(left.ColumnLine - left.C), 1e-12);
                double q = left.ColumnTorsion.Sum() / Math.Max(left.Eccentricity, 1e-12) / left.Length;
                check("Móng băng lệch tâm: xoắn t = q·e (q = ΣN/L)", left.TorsionPerM, q * left.Eccentricity, 1e-9);
                check("Móng băng lệch tâm: khuyến nghị giằng ngang", B(left.Messages.Any(m => m.Contains("giằng ngang"))), 1, 0);
                check("Móng băng lệch tâm: kiểm tra bản cánh công xôn", B(left.Checks.Any(c => c.Name.Contains("bản cánh"))), 1, 0);
                if (midS != null && left.Width > left.RibB + 1e-6 && midS.Width > midS.RibB + 1e-6 && left.FlangeV > 1e-6 && midS.FlangeV > 1e-6)
                {
                    // cùng áp lực ròng: M/Q tỷ lệ với độ vươn c (c = B − b một phía, (B − b)/2 hai phía): M = p·c²/2, Q = p·(c − h0) → p = 2M/c²
                    double cL = left.Width - left.RibB, pL = 2 * left.FlangeM / (cL * cL);
                    check("Móng băng lệch tâm: bản cánh công xôn một phía c = B − b (Q = p·(c − h0))", left.FlangeV, pL * Math.Max(cL - (left.FlangeRoot - Math.Max(p.Concrete.CoverFooting, 0.03) - 0.01), 0), 1e-9);
                }
                var figs = FootingDrawing.BuildAll(p, s);
                check("Bản vẽ móng băng lệch tâm: xuất DXF", figs.Count(f => f.W.ToDxfString().Length > 0), figs.Count, 0);
            }
        }
        finally
        {
            p.Soil.ROverride = 0;
            p.Foundation = JsonSerializer.Deserialize<FoundationSettings>(save, Project.JsonOptions);
        }
    }

    // ------------------------------------------------------------------
    private static void Json(Action<string, double, double, double> check, Project p)
    {
        p.Foundation.Eccentric.Add(new EccentricOverride { NodeId = -5, AutoOffset = false, OffsetX = 0.45, OffsetY = -0.2, Scheme = EccentricScheme.Combined, PartnerX = 7, StrapB = 0.3, StrapH = 0.6 });
        p.Foundation.BoundaryClearance = 0.02;
        string json = JsonSerializer.Serialize(p, Project.JsonOptions);
        var back = JsonSerializer.Deserialize<Project>(json, Project.JsonOptions);
        check("JSON: số cạnh ranh giới", back.Foundation.Boundaries.Count, 2, 0);
        check("JSON: cạnh trái theo trục + khoảng cách", back.Foundation.Boundaries.First(b => b.Side == BoundarySide.Left).Offset, 0.11, 1e-12);
        check("JSON: toạ độ ranh giới tính lại", EccentricFooting.Lines(back).First(l => l.B.Side == BoundarySide.Right).Coord, 7.31, 1e-12);
        var o = back.Foundation.Eccentric.First(x => x.NodeId == -5);
        check("JSON: lệch tâm nhập tay ex", o.OffsetX, 0.45, 1e-12);
        check("JSON: phương án (c) (enum chữ)", B(o.Scheme == EccentricScheme.Combined && json.Contains("\"Combined\"")), 1, 0);
        check("JSON: tiết diện dầm cân bằng", o.StrapH, 0.6, 1e-12);
        check("JSON: khoảng hở ranh giới", back.Foundation.BoundaryClearance, 0.02, 1e-12);
        var old = JsonSerializer.Deserialize<FoundationSettings>("{\"Type\":\"Isolated\"}", Project.JsonOptions);
        check("JSON cũ (không có ranh giới): danh sách rỗng, không lệch tâm", (old.Boundaries?.Count ?? -1) + (old.Eccentric?.Count ?? -1), 0, 0);
        check("JSON cũ: dung sai mặc định 150 mm, β = 0,5", old.BoundaryTolerance + old.StrapReliefFactor, 0.65, 1e-12);
        p.Foundation.Eccentric.RemoveAll(x => x.NodeId == -5);
        p.Foundation.BoundaryClearance = 0;
        var noB = JsonSerializer.Deserialize<Project>(json, Project.JsonOptions);
        noB.Foundation.Boundaries.Clear(); noB.Foundation.Eccentric.Clear();
        check("Không có ranh giới: không bật móng lệch tâm", B(!EccentricFooting.Active(noB)), 1, 0);
    }
}
