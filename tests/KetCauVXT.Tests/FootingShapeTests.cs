using System.Text.Json;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>[MóngHìnhDạng] Bố trí móng: hình học đa giác, móng xoay / dóng mép cột / lệch tâm, đường chuẩn (ranh đất xiên),
/// móng đa giác bất kỳ, áp lực đáy (đối chiếu công thức chữ nhật), nhân bản, đồng nhất, vị trí móng gộp, bản vẽ, khối lượng, JSON.</summary>
public static class FootingShapeTests
{
    private static double B(bool x) => x ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Geometry(check);
        var (p, r) = House();
        Layout(check, p, r);
        Placement(check, p, r);
        LotLines(check, p, r);
        Polygon(check, p, r);
        CopyUnify(check, p, r);
        Output(check, p, r, outDir);
    }

    private static (Project, AnalysisResults) House()
    {
        var p = ModelBuilder.NewProject("Nhà đất méo – móng hình dạng");
        ModelBuilder.SetGrid(p, new[] { 3.6, 4.2 }, new[] { 4.5, 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x22", BeamSectionX = "D22x30", BeamSectionY = "D22x40", PerimeterWallLoad = 7.5 });
        p.Foundation.Type = FoundationType.Isolated;
        return (p, FrameAnalysis.Run(p));
    }

    private static int NodeAt(Project p, int i, int j)
    {
        var gx = p.GridsX.ToList(); var gy = p.GridsY.ToList();
        return FoundationUtil.BaseColumns(p).First(c => Math.Abs(c.Node.X - gx[i].Coord) < 1e-6 && Math.Abs(c.Node.Y - gy[j].Coord) < 1e-6).Node.Id;
    }

    private static ShapedFooting.ColumnGeo Col(Project p, int node)
    {
        var bc = FoundationUtil.BaseColumns(p).First(c => c.Node.Id == node);
        return ShapedFooting.Column(p, bc.Column, bc.Node);
    }

    private static FootingDesign FootOf(DesignSummary s, int node) =>
        s.Footings.FirstOrDefault(f => f.NodeId == node || f.RebarKeys.Contains("M:" + node));

    // ------------------------------------------------------------------
    private static void Geometry(Action<string, double, double, double> check)
    {
        var rect = new List<(double X, double Y)> { (0, 0), (2, 0), (2, 1), (0, 1) };
        var pr = PolyGeom.Properties(rect);
        check("Đa giác: diện tích chữ nhật 2×1", pr.Area, 2, 1e-12);
        check("Đa giác: trọng tâm x", pr.Cx, 1, 1e-12);
        check("Đa giác: trọng tâm y", pr.Cy, 0.5, 1e-12);
        check("Đa giác: Ixx = b·h³/12", pr.Ixx, 2.0 / 12, 1e-12);
        check("Đa giác: Iyy = h·b³/12", pr.Iyy, 8.0 / 12, 1e-12);
        check("Đa giác: Ixy = 0", pr.Ixy, 0, 1e-12);
        var cw = rect.AsEnumerable().Reverse().ToList();
        check("Đa giác: chiều kim đồng hồ cùng diện tích", PolyGeom.Properties(cw).Area, 2, 1e-12);
        var rot = PolyGeom.Rect(5, 3, 2, 1, Math.PI / 6);
        var rp = PolyGeom.Properties(rot);
        check("Chữ nhật xoay 30°: diện tích", rp.Area, 2, 1e-9);
        check("Chữ nhật xoay 30°: trọng tâm", rp.Cx + rp.Cy, 8, 1e-9);
        // tam giác vuông: Ixy quanh trọng tâm = −b²h²/72
        var tri = new List<(double X, double Y)> { (0, 0), (3, 0), (0, 2) };
        check("Tam giác vuông: Ixy = −b²h²/72", PolyGeom.Properties(tri).Ixy, -9.0 * 4 / 72, 1e-9);
        var half = PolyGeom.ClipHalf(rect, 1, 0, 0.5);
        check("Cắt nửa mặt phẳng x ≥ 0,5: diện tích 1,5", PolyGeom.Properties(half).Area, 1.5, 1e-12);
        var diag = new LotLine { X1 = 0, Y1 = 1.5, X2 = 1.5, Y2 = 0 };   // x + y = 1,5
        var cut = PolyGeom.ClipByLine(rect, diag, 2, 1);
        check("Cắt theo đường chuẩn xiên: giữ phía chứa tim cột", PolyGeom.Properties(cut).Area, 2 - 0.5 * 1 * 1 - 0, 1e-9);
        check("Cắt theo đường chuẩn: mọi đỉnh ở phía trong", B(cut.All(q => q.X + q.Y >= 1.5 - 1e-9)), 1, 0);
        var cut2 = PolyGeom.ClipByLine(rect, new LotLine { X1 = 0, Y1 = 1.5, X2 = 1.5, Y2 = 0, Clearance = 0.1 }, 2, 1);
        check("Khe hở đường chuẩn: đỉnh cách đường ≥ khe hở", B(cut2.All(q => (q.X + q.Y - 1.5) / Math.Sqrt(2) >= 0.1 - 1e-9)), 1, 0);
        var ch = PolyGeom.Chords(rect, 0, 0.5, 1, 0);
        check("Dây cung ngang qua chữ nhật: dài 2", ch.Sum(c => c.T2 - c.T1), 2, 1e-12);
        var ell = new List<(double X, double Y)> { (0, 0), (3, 0), (3, 1), (1, 1), (1, 3), (0, 3) };
        check("Đa giác chữ L: không lồi", B(!PolyGeom.IsConvex(ell)), 1, 0);
        check("Đa giác chữ L: diện tích 5", PolyGeom.Properties(ell).Area, 5, 1e-12);
        check("Dây cung qua chữ L (y = 0,5) dài 3", PolyGeom.Chords(ell, 0, 0.5, 1, 0).Sum(c => c.T2 - c.T1), 3, 1e-12);
        var x = PolyGeom.Intersect(0, 0, 2, 2, 0, 2, 2, 0);
        check("Giao hai đường chéo: (1; 1)", x.Value.X + x.Value.Y, 2, 1e-12);
        check("Góc chuẩn hoá về ±45°: 100° → 10°", ShapedFooting.Norm45(100 * Math.PI / 180) * 180 / Math.PI, 10, 1e-9);
        var l = new LotLine { X1 = 0, Y1 = 0, X2 = 4, Y2 = 3 };
        check("Đường chuẩn: góc 36,87°", l.AngleDeg, Math.Atan2(3, 4) * 180 / Math.PI, 1e-12);
        check("Đường chuẩn: khoảng cách điểm tới đoạn", l.DistanceToSegment(4, 0), 2.4, 1e-12);
    }

    // ------------------------------------------------------------------
    private static void Layout(Action<string, double, double, double> check, Project p, AnalysisResults r)
    {
        var s = ConcreteDesigner.Design(p, r);
        int nCols = FoundationUtil.BaseColumns(p).Count;
        check("Móng thường: tổng số móng (kể gộp) = số chân cột", s.Footings.Sum(f => Math.Max(1, f.Quantity)), nCols, 0);
        var all = FootingLayout.AllInstances(p, s);
        check("Vị trí móng gộp: mỗi chân cột một móng trên mặt bằng", all.Count, nCols, 0);
        check("Vị trí móng gộp: không trùng chân cột", all.Select(f => f.NodeId).Distinct().Count(), nCols, 0);
        check("Vị trí móng gộp: toạ độ bản sao = toạ độ nút", B(all.All(f => { var n = p.Node(f.NodeId); return Math.Abs(n.X - f.X) < 1e-9 && Math.Abs(n.Y - f.Y) < 1e-9; })), 1, 0);
        check("Móng thường: không có móng hình dạng", s.Footings.Count(f => f.Shaped), 0, 0);
        // bố trí mặc định (tâm, song song trục) → không đổi kết quả
        int id = NodeAt(p, 1, 1);
        p.Foundation.Placements.Add(new FootingPlacement { NodeId = id });
        var s2 = ConcreteDesigner.Design(p, r);
        var a = FootOf(s, id); var b = FootOf(s2, id);
        check("Bố trí mặc định: không thành móng hình dạng", B(!b.Shaped), 1, 0);
        check("Bố trí mặc định: kích thước như cũ", b.L * 10 + b.B, a.L * 10 + a.B, 1e-12);
        p.Foundation.Placements.Clear();
    }

    // ------------------------------------------------------------------
    private static void Placement(Action<string, double, double, double> check, Project p, AnalysisResults r)
    {
        int id = NodeAt(p, 1, 1);
        var s0 = ConcreteDesigner.Design(p, r);
        var f0 = FootOf(s0, id);
        // đối chiếu: móng chữ nhật đặt tâm (Offset 0) – áp lực từ lời giải tổng quát = công thức p0 ± 6M/(BL²)
        FoundationReset.SetSize(p, id, f0.L, f0.B, f0.H);
        var sRef = ConcreteDesigner.Design(p, r);
        var fr = FootOf(sRef, id);
        p.Foundation.Placements.Add(new FootingPlacement { NodeId = id, AlignX = FootingAlign.Offset, OffsetX = 0 });
        var s1 = ConcreteDesigner.Design(p, r);
        var f1 = FootOf(s1, id);
        Console.WriteLine($"   · Đối chiếu tâm: chữ nhật pmax = {fr.Pmax:0.0}, ptb = {fr.Ptb:0.0}; tổng quát pmax = {f1.Pmax:0.0}, ptb = {f1.Ptb:0.0} kN/m²");
        check("Lời giải tổng quát (đặt tâm): là móng hình dạng", B(f1.Shaped), 1, 0);
        check("Lời giải tổng quát: diện tích = L·B", f1.Area, fr.L * fr.B, 1e-9);
        check("Lời giải tổng quát: ptb = công thức chữ nhật", f1.Ptb, fr.Ptb, 1e-9);
        check("Lời giải tổng quát: pmax ≈ công thức chữ nhật (lưới 60×60, ≤ 2%)", f1.Pmax, fr.Pmax, 0.02);
        check("Lời giải tổng quát: số hiệu thép hợp lệ", RebarNumbering.Validate(f1.Bars).Count, 0, 0);

        // xoay góc 30° nhập tay
        p.Foundation.Placements.Clear();
        FoundationReset.Clear(p, null, new[] { id });
        p.Foundation.Placements.Add(new FootingPlacement { NodeId = id, AngleMode = FootingAngleMode.Custom, Angle = 30 });
        var s2 = ConcreteDesigner.Design(p, r);
        var f2 = FootOf(s2, id);
        Console.WriteLine($"   · Móng xoay 30°: {f2.Name} khung {f2.L:0.00}×{f2.B:0.00}×{f2.H:0.00}, A = {f2.Area:0.00}, pmax = {f2.Pmax:0.0}, {(f2.Ok ? "đạt" : "KHÔNG ĐẠT")}");
        check("Móng xoay 30°: góc", f2.Angle, 30, 1e-9);
        check("Móng xoay 30°: 4 đỉnh", f2.Outline.Count, 4, 0);
        var c2 = Col(p, id);
        check("Móng xoay 30°: trọng tâm trùng tim cột", Math.Abs(f2.Gx - c2.Cx) + Math.Abs(f2.Gy - c2.Cy), 0, 1e-9);
        check("Móng xoay 30°: cạnh đầu theo góc 30°", Math.Atan2(f2.Outline[1][1] - f2.Outline[0][1], f2.Outline[1][0] - f2.Outline[0][0]) * 180 / Math.PI, 30, 1e-6);
        check("Móng xoay 30°: áp lực đạt", B(f2.Checks.Take(3).All(c => c.Ok)), 1, 0);
        check("Móng xoay 30°: đạt", B(f2.Ok), 1, 0);

        // dóng mép móng thẳng mép cột phía âm (cột sát ranh trái) → móng chìa sang phải, lệch tâm tự cân bằng
        int edge = NodeAt(p, 0, 1);
        p.Foundation.Placements.Clear();
        p.Foundation.Placements.Add(new FootingPlacement { NodeId = edge, AlignX = FootingAlign.MinFlush });
        var s3 = ConcreteDesigner.Design(p, r);
        var f3 = FootOf(s3, edge);
        var n3 = Col(p, edge);
        Console.WriteLine($"   · Móng dóng mép cột trái: {f3.L:0.00}×{f3.B:0.00}×{f3.H:0.00}, e = {(f3.Gx - n3.Cx) * 1000:0} mm, dầm cân bằng → nút {f3.StrapNode} (Ls = {f3.StrapSpan:0.00}, M = {f3.StrapMoment:0.0} kN.m), pmax = {f3.Pmax:0.0}, {(f3.Ok ? "đạt" : "KHÔNG ĐẠT")}");
        check("Dóng mép cột: mép trái móng = mép trái cột (x − 0,11)", f3.Outline.Min(v => v[0]), n3.Cx - 0.11, 1e-9);
        check("Dóng mép cột: móng chìa sang phải (trọng tâm > tim cột)", B(f3.Gx > n3.Cx + 0.1), 1, 0);
        check("Dóng mép cột: phương B vẫn đặt tâm", (f3.Outline.Min(v => v[1]) + f3.Outline.Max(v => v[1])) / 2, n3.Cy, 1e-9);
        check("Dóng mép cột: dầm móng cân bằng nối tới cột trục 2 cùng hàng", f3.StrapNode, NodeAt(p, 1, 1), 0);
        check("Dóng mép cột: M dầm cân bằng = N·e", f3.StrapMoment, f3.N * Math.Abs(f3.Gx - n3.Cx), 1e-6);
        check("Dóng mép cột: Q dầm = N·e/(Ls − e)", f3.StrapShear, f3.N * Math.Abs(f3.Gx - n3.Cx) / (f3.StrapSpan - Math.Abs(f3.Gx - n3.Cx)), 1e-6);
        check("Dóng mép cột: áp lực nền đạt (tự tăng kích thước)", B(f3.Checks.Take(3).All(c => c.Ok)), 1, 0);
        check("Dóng mép cột: đạt", B(f3.Ok), 1, 0);
        // tự cân bằng (không dầm cân bằng): đáy móng chịu N·e → cảnh báo, tách nền
        p.Foundation.Placements[0].StrapBeam = false;
        var f3b = FootOf(ConcreteDesigner.Design(p, r), edge);
        check("Tự cân bằng: không có dầm cân bằng", f3b.StrapNode, 0, 0);
        check("Tự cân bằng: cảnh báo mô men N·e do đáy móng chịu", B(f3b.Messages.Any(m => m.Contains("tự cân bằng"))), 1, 0);
        check("Tự cân bằng: có kiểm tra vùng tiếp xúc / pmin", B(f3b.Checks.Any(c => c.Name.Contains("tiếp xúc") || c.Name.Contains("pmin"))), 1, 0);
        p.Foundation.Placements[0].StrapBeam = true;
        // mép phải: MaxFlush
        p.Foundation.Placements[0].AlignX = FootingAlign.MaxFlush;
        var f4 = FootOf(ConcreteDesigner.Design(p, r), edge);
        check("Dóng mép phải: mép phải móng = mép phải cột", f4.Outline.Max(v => v[0]), n3.Cx + 0.11, 1e-9);
        // lệch tâm nhập tay
        p.Foundation.Placements[0].AlignX = FootingAlign.Offset; p.Foundation.Placements[0].OffsetX = 0.25;
        FoundationReset.SetSize(p, edge, 1.6, 1.4, 0.45);
        var f5 = FootOf(ConcreteDesigner.Design(p, r), edge);
        check("Lệch tâm nhập tay: tâm móng = tim cột + 0,25", (f5.Outline.Min(v => v[0]) + f5.Outline.Max(v => v[0])) / 2, n3.Cx + 0.25, 1e-9);
        check("Lệch tâm nhập tay: kích thước chốt 1,6 × 1,4", f5.L * 10 + f5.B, 16 + 1.4, 1e-9);
        check("Lệch tâm nhập tay: chiều cao chốt 0,45", f5.H, 0.45, 1e-12);
        p.Foundation.Placements.Clear();
        FoundationReset.Clear(p, null);
    }

    // ------------------------------------------------------------------
    private static void LotLines(Action<string, double, double, double> check, Project p, AnalysisResults r)
    {
        // ranh đất xiên phía trái: đi qua góc ngoài cột A-1 (x − 0,11; y − 0,11) và lệch 0,3 m ra ngoài ở trục C
        var gx = p.GridsX.ToList(); var gy = p.GridsY.ToList();
        var ca = Col(p, NodeAt(p, 0, 0)); var cc = Col(p, NodeAt(p, 0, gy.Count - 1));
        double x0 = ca.Cx - 0.11, y0 = ca.Cy - 0.11, y1 = cc.Cy + 0.11;
        var line = new LotLine { Name = "Ranh trái", X1 = x0, Y1 = y0, X2 = x0 - 0.3, Y2 = y1 };
        p.Foundation.LotLines.Add(line);
        var snaps = ShapedFooting.SnapPoints(p);
        check("Bắt dính: có góc cột", B(snaps.Any(q => q.Kind == "góc cột" && Math.Abs(q.X - x0) < 1e-9 && Math.Abs(q.Y - y0) < 1e-9)), 1, 0);
        check("Bắt dính: có giao trục", snaps.Count(q => q.Kind == "giao trục"), gx.Count * gy.Count, 0);
        var s = ConcreteDesigner.Design(p, r);
        int shaped = 0;
        foreach (int j in Enumerable.Range(0, gy.Count))
        {
            int id = NodeAt(p, 0, j);
            var f = FootingLayout.AllInstances(p, s).First(q => q.NodeId == id);
            var n = p.Node(id);
            bool inside = f.Outline == null
                ? line.Side(n.X - f.L / 2, n.Y - f.B / 2) * line.Side(n.X, n.Y) >= -1e-12 && line.Side(n.X - f.L / 2, n.Y + f.B / 2) * line.Side(n.X, n.Y) >= -1e-12
                : f.Outline.All(v => line.Side(v[0], v[1]) * Math.Sign(line.Side(n.X, n.Y)) >= -1e-7);
            check($"Ranh đất xiên: móng nút {id} không vượt ranh", B(inside), 1, 0);
            if (f.Shaped) shaped++;
        }
        check("Ranh đất xiên: móng biên trái bị cắt (hình dạng)", B(shaped >= 2), 1, 0);
        var fc = FootingLayout.AllInstances(p, s).First(q => q.NodeId == NodeAt(p, 0, 1));
        Console.WriteLine($"   · Móng cắt ranh xiên nút {fc.NodeId}: {fc.Outline?.Count} đỉnh, A = {fc.Area:0.00} m² (chữ nhật {fc.RectL:0.00}×{fc.RectB:0.00}), pmax = {fc.Pmax:0.0}, {(fc.Ok ? "đạt" : "KHÔNG ĐẠT")}");
        if (fc.Shaped)
        {
            check("Móng cắt ranh: diện tích < chữ nhật trước khi cắt", B(fc.Area < fc.RectL * fc.RectB - 1e-6), 1, 0);
            check("Móng cắt ranh: áp lực nền đạt", B(fc.Checks.Take(3).All(c => c.Ok)), 1, 0);
            check("Móng cắt ranh: số hiệu thép hợp lệ", RebarNumbering.Validate(fc.Bars).Count, 0, 0);
            check("Móng cắt ranh: thép cắt theo nhiều chiều dài", B(fc.Bars.Count >= 2), 1, 0);
        }
        // góc móng theo đường chuẩn gần nhất
        int idm = NodeAt(p, 0, 1);
        p.Foundation.Placements.Add(new FootingPlacement { NodeId = idm, AngleMode = FootingAngleMode.LotLine, AlignX = FootingAlign.MinFlush });
        var fl = FootOf(ConcreteDesigner.Design(p, r), idm);
        check("Góc theo đường chuẩn: α = góc đường chuẩn (chuẩn hoá ±45°)", fl.Angle, ShapedFooting.Norm45(line.AngleDeg * Math.PI / 180) * 180 / Math.PI, 1e-9);
        check("Góc theo đường chuẩn: không vượt ranh", B(fl.Outline.All(v => line.Side(v[0], v[1]) * Math.Sign(line.Side(p.Node(idm).X, p.Node(idm).Y)) >= -1e-7)), 1, 0);
        p.Foundation.Placements.Clear();
        // đường chuẩn tắt → như cũ
        line.Enabled = false;
        check("Đường chuẩn tắt: không còn móng hình dạng", ConcreteDesigner.Design(p, r).Footings.Count(f => f.Shaped), 0, 0);
        line.Enabled = true;
    }

    // ------------------------------------------------------------------
    private static void Polygon(Action<string, double, double, double> check, Project p, AnalysisResults r)
    {
        p.Foundation.LotLines.Clear();
        int id = NodeAt(p, 1, 0);
        // ngũ giác quanh cột (đất méo): đỉnh so với tim cột
        var poly = new List<double[]> { new[] { -0.6, -0.5 }, new[] { 0.9, -0.5 }, new[] { 1.1, 0.4 }, new[] { 0.2, 0.9 }, new[] { -0.6, 0.6 } };
        p.Foundation.Placements.Add(new FootingPlacement { NodeId = id, Polygon = poly });
        var s = ConcreteDesigner.Design(p, r);
        var f = FootOf(s, id);
        var n = Col(p, id);
        double areaExp = PolyGeom.Properties(poly.Select(v => (v[0], v[1])).ToList()).Area;
        Console.WriteLine($"   · Móng đa giác 5 đỉnh nút {id}: A = {f.Area:0.00} m², H = {f.H:0.00}, pmax = {f.Pmax:0.0}, tiếp xúc {f.Contact * 100:0}%, {f.BarsL}/{f.BarsB}, {(f.Ok ? "đạt" : "KHÔNG ĐẠT")}");
        check("Móng đa giác: 5 đỉnh", f.Outline.Count, 5, 0);
        check("Móng đa giác: diện tích = đa giác người dùng", f.Area, areaExp, 1e-9);
        check("Móng đa giác: đỉnh theo toạ độ tuyệt đối", B(f.Outline.Any(v => Math.Abs(v[0] - (n.Cx + 1.1)) < 1e-9 && Math.Abs(v[1] - (n.Cy + 0.4)) < 1e-9)), 1, 0);
        check("Móng đa giác: không tự tăng kích thước", B(f.RectL == 0), 1, 0);
        check("Móng đa giác: chọc thủng có chiều cao", B(f.H >= 0.3 - 1e-9), 1, 0);
        check("Móng đa giác: chọc thủng đạt", B(f.Checks.First(c => c.Name.StartsWith("Chọc thủng")).Ok), 1, 0);
        check("Móng đa giác: thép 2 phương", B(f.Bars.Any(b => b.Role.Contains("phương L")) && f.Bars.Any(b => b.Role.Contains("phương B"))), 1, 0);
        check("Móng đa giác: số hiệu thép hợp lệ", RebarNumbering.Validate(f.Bars).Count, 0, 0);
        check("Móng đa giác: tổng chiều dài thép ≤ đa giác (thanh cắt theo đường bao)",
            B(f.Bars.Where(b => b.Role.Contains("phương L")).Max(b => b.Length) <= f.L + 0.5), 1, 0);
        // đa giác quá nhỏ → không đạt áp lực, báo lỗi
        p.Foundation.Placements[0].Polygon = new List<double[]> { new[] { -0.3, -0.3 }, new[] { 0.3, -0.3 }, new[] { 0.3, 0.3 }, new[] { -0.3, 0.3 } };
        var fs = FootOf(ConcreteDesigner.Design(p, r), id);
        check("Móng đa giác nhỏ: không đạt áp lực nền, báo LỖI", B(!fs.Ok && fs.Messages.Any(m => m.StartsWith("LỖI móng hình dạng"))), 1, 0);
        p.Foundation.Placements.Clear();
        // Kết quả mặc định không còn đa giác
        check("Bỏ bố trí: hết móng hình dạng", ConcreteDesigner.Design(p, r).Footings.Count(x => x.Shaped), 0, 0);
    }

    // ------------------------------------------------------------------
    private static void CopyUnify(Action<string, double, double, double> check, Project p, AnalysisResults r)
    {
        var s = ConcreteDesigner.Design(p, r);
        int types0 = s.Footings.Count;
        int src = NodeAt(p, 1, 1);
        var targets = FootingLayout.SameColumnNodes(p, src);
        check("Nhân bản: chân cột cùng tiết diện", targets.Count, FoundationUtil.BaseColumns(p).Count - 1, 0);
        p.Foundation.Placements.Add(new FootingPlacement { NodeId = src, AngleMode = FootingAngleMode.Custom, Angle = 15 });
        var s1 = ConcreteDesigner.Design(p, r);
        var cr = FootingLayout.CopyTo(p, s1, src, targets);
        check("Nhân bản: số chân cột đích", cr.Targets, targets.Count, 0);
        check("Nhân bản: chép kích thước", cr.Sizes, targets.Count, 0);
        check("Nhân bản: chép bố trí", cr.Placements, targets.Count, 0);
        var (L, Bv, H) = FootingLayout.SizeOf(p, s1, src);
        check("Nhân bản: kích thước chốt ở chân cột đích", B(targets.All(t => { var ft = p.Footings.First(f => f.NodeId == t); return Math.Abs(ft.L - L) < 1e-9 && Math.Abs(ft.B - Bv) < 1e-9 && Math.Abs(ft.Height - H) < 1e-9; })), 1, 0);
        var s2 = ConcreteDesigner.Design(p, r);
        check("Nhân bản: mọi móng xoay 15°", B(FootingLayout.AllInstances(p, s2).All(f => f.Shaped && Math.Abs(f.Angle - 15) < 1e-9)), 1, 0);
        check("Nhân bản: mọi chân cột có móng trên mặt bằng", FootingLayout.AllInstances(p, s2).Count, FoundationUtil.BaseColumns(p).Count, 0);
        Console.WriteLine($"   · Nhân bản móng nút {src} → {targets.Count} chân cột: {s2.Footings.Count} loại móng ({string.Join(", ", s2.Footings.Select(f => f.Name + "×" + Math.Max(1, f.Quantity)))})");
        check("Nhân bản: móng giống hệt gộp tên (ít loại hơn số chân cột)", B(s2.Footings.Count < FoundationUtil.BaseColumns(p).Count), 1, 0);
        p.Foundation.Placements.Clear();
        FoundationReset.Clear(p, null);

        // đồng nhất
        var s3 = ConcreteDesigner.Design(p, r);
        var groups = FootingLayout.Unify(p, s3, 0.5, true, null, false);
        check("Đồng nhất (xem trước): không ghi kích thước", p.Footings.Count(f => f.L > 0), 0, 0);
        var g2 = FootingLayout.Unify(p, s3, 0.5);
        check("Đồng nhất: cùng số nhóm với xem trước", g2.Count, groups.Count, 0);
        if (g2.Count > 0)
        {
            var g = g2[0];
            check("Đồng nhất: mọi móng trong nhóm chốt cùng kích thước", B(g.Nodes.All(n => { var ft = p.Footings.First(f => f.NodeId == n); return Math.Abs(ft.L - g.L) < 1e-9 && Math.Abs(ft.B - g.B) < 1e-9; })), 1, 0);
            var s4 = ConcreteDesigner.Design(p, r);
            Console.WriteLine($"   · Đồng nhất móng (dung sai 0,5 m): {s3.Footings.Count} → {s4.Footings.Count} loại; nhóm 1: {g.L:0.00}×{g.B:0.00}×{g.H:0.00} ({g.Nodes.Count} móng)");
            check("Đồng nhất: số loại móng không tăng", B(s4.Footings.Count <= s3.Footings.Count), 1, 0);
            check("Đồng nhất: móng trong nhóm có cùng kích thước sau thiết kế lại",
                g.Nodes.Select(n => FootingLayout.AllInstances(p, s4).First(f => f.NodeId == n)).Select(f => Math.Round(f.L * 100) * 1000 + Math.Round(f.B * 100)).Distinct().Count(), 1, 0);
        }
        FoundationReset.Clear(p, null);
    }

    // ------------------------------------------------------------------
    private static void Output(Action<string, double, double, double> check, Project p, AnalysisResults r, string outDir)
    {
        var gx = p.GridsX.ToList(); var gy = p.GridsY.ToList();
        p.Foundation.LotLines.Add(new LotLine { Name = "Ranh trái", X1 = gx[0].Coord - 0.11, Y1 = gy[0].Coord - 0.11, X2 = gx[0].Coord - 0.41, Y2 = gy[^1].Coord + 0.11 });
        int id = NodeAt(p, 1, 0);
        p.Foundation.Placements.Add(new FootingPlacement { NodeId = id, Polygon = new List<double[]> { new[] { -0.7, -0.6 }, new[] { 0.8, -0.6 }, new[] { 0.9, 0.7 }, new[] { -0.7, 0.5 } } });
        var s = ConcreteDesigner.Design(p, r);
        int nShaped = s.Footings.Count(f => f.Shaped);
        check("Bản vẽ: có móng hình dạng", B(nShaped >= 2), 1, 0);
        var figs = FootingDrawing.BuildAll(p, s);
        check("Bản vẽ: không lỗi hình", figs.Count(fg => fg.W.ToDxfString().Contains("L\\U+1ED6I V\\U+1EBC")), 0, 0);
        var fd = s.Footings.First(f => f.Shaped);
        var dp = ShapedFootingDrawing.DetailPlan(p, fd, 1, FootingDrawing.SheetScale);
        string dxf = dp.ToDxfString();
        check("Chi tiết móng hình dạng: có đường bao đa giác", B(dxf.Contains("LWPOLYLINE") || dxf.Contains("POLYLINE")), 1, 0);
        check("Chi tiết móng hình dạng: có thép", B(dxf.Contains("THEP")), 1, 0);
        var lay = FootingDrawing.Layout(p, s, FootingDrawing.SheetScale, 0.5);
        check("Mặt bằng móng: có đường chuẩn (KY_HIEU nét đứt)", B(lay.ToDxfString().Contains("KY_HIEU")), 1, 0);
        if (outDir != null)
        {
            try { Directory.CreateDirectory(outDir); dp.Save(Path.Combine(outDir, "MongHinhDang_ChiTiet.dxf")); lay.Save(Path.Combine(outDir, "MongHinhDang_MatBang.dxf")); }
            catch (Exception) { /* thư mục kết quả không ghi được */ }
        }
        // khối lượng: V = A·H
        var q = QuantityCalculator.Compute(p, s);
        double vShaped = q.Concrete.Where(c => c.Category == QtyCat.Footing && c.Dims.StartsWith("đa giác")).Sum(c => c.Volume);
        check("Khối lượng: bê tông móng hình dạng = A·H", vShaped, s.Footings.Where(f => f.Shaped).Sum(f => Math.Max(1, f.Quantity) * f.Area * f.H), 1e-9);
        // JSON
        string js = JsonSerializer.Serialize(p, Project.JsonOptions);
        var p2 = JsonSerializer.Deserialize<Project>(js, Project.JsonOptions);
        check("JSON: giữ đường chuẩn", p2.Foundation.LotLines.Count, 1, 0);
        check("JSON: giữ móng đa giác", p2.Foundation.Placements.Single().Polygon.Count, 4, 0);
        check("JSON: toạ độ đường chuẩn", p2.Foundation.LotLines[0].X2, gx[0].Coord - 0.41, 1e-12);
        var old = JsonSerializer.Deserialize<FoundationSettings>("{\"Type\":0}", Project.JsonOptions);
        check("JSON cũ: không có bố trí / đường chuẩn", old.Placements.Count + old.LotLines.Count, 0, 0);
        p.Foundation.Placements.Clear(); p.Foundation.LotLines.Clear();
    }
}
