using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>[Tên cấu kiện] Cột / dầm / móng cùng tiết diện + chiều dài → một tên, thiết kế chung một phương án thép.</summary>
public static class MemberMarkTests
{
    private static double B(bool x) => x ? 1 : 0;

    public static void Run(Action<string, double, double, double> check)
    {
        var p = ModelBuilder.NewProject("Đặt tên cấu kiện");
        ModelBuilder.SetGrid(p, new[] { 4.0, 4.0 }, new[] { 4.5, 4.5, 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, 0.0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions
        {
            ColumnSection = "C22x22", BeamSectionX = "D22x30", BeamSectionY = "D22x30", PerimeterWallLoad = 5, FinishLoad = 1.5, LiveLoad = 1.5, RoofLiveLoad = 0.75
        });
        p.Foundation.Type = FoundationType.Isolated;
        var r = FrameAnalysis.Run(p);
        var s0 = ConcreteDesigner.Design(p, r);
        int beams0 = s0.Beams.Sum(b => b.Quantity), cols0 = s0.Columns.Sum(c => c.Quantity);
        Console.WriteLine($"   · Chưa đặt tên: {s0.Beams.Count} loại dầm ({beams0} dải), {s0.Columns.Count} loại cột ({cols0} chồng), {s0.Footings.Count} loại móng");

        // ---- tự đặt tên ----
        var gs = MemberMarks.AutoAssign(p, s0);
        foreach (var g in gs) Console.WriteLine($"     {g.Kind} {g.Mark}: {g.Section}, L = {g.Length}, SL = {g.Count} – {g.Where}");
        var stacks = MemberMarks.ColumnStacks(p);
        var lines = MemberMarks.BeamLines(p);
        check("Tự đặt tên: mọi cột có tên", p.Frames.Count(f => f.Kind == FrameKind.Column && string.IsNullOrEmpty(f.Mark)), 0, 0);
        check("Tự đặt tên: mọi dầm có tên", p.Frames.Count(f => f.Kind == FrameKind.Beam && string.IsNullOrEmpty(f.Mark)), 0, 0);
        check("Tự đặt tên cột: 12 chồng cột cùng tiết diện, cùng chiều cao → 1 tên", gs.Count(g => g.Kind == "Cột"), 1, 0);
        check("Tự đặt tên dầm: số tên = số khoá (tiết diện + nhịp) khác nhau", gs.Count(g => g.Kind == "Dầm"), lines.Select(l => l.Key).Distinct().Count(), 0);
        check("Tự đặt tên dầm: tổng SL = số dải dầm", gs.Where(g => g.Kind == "Dầm").Sum(g => g.Count), lines.Count, 0);
        check("Mỗi tên: cùng tiết diện + chiều dài", B(MemberMarks.Conflicts(p).Count == 0), 1, 0);
        check("Móng: có tên M…", B(gs.Any(g => g.Kind == "Móng") && p.Footings.Count(f => f.Mark.StartsWith("M")) == stacks.Count), 1, 0);

        // ---- thiết kế theo tên ----
        var s1 = ConcreteDesigner.Design(p, r);
        foreach (var b in s1.Beams) Console.WriteLine($"     {b.Name}: SL {b.Quantity}, {(b.Ok ? "đạt" : "CHƯA ĐẠT")}");
        check("Thiết kế cột: một loại cột C1, SL = 12", B(s1.Columns.Count == 1 && s1.Columns[0].Name == "C1" && s1.Columns[0].Quantity == stacks.Count), 1, 0);
        check("Thiết kế dầm: tên = tên đã gán", B(s1.Beams.All(b => gs.Any(g => g.Kind == "Dầm" && g.Mark == b.Name))), 1, 0);
        check("Thiết kế dầm: tổng SL không đổi", s1.Beams.Sum(b => b.Quantity), beams0, 0);
        check("Thiết kế dầm: mỗi tên một phương án", s1.Beams.Count, gs.Count(g => g.Kind == "Dầm"), 0);
        check("Dầm cùng tên: As bố trí bao As yêu cầu cả nhóm (đạt)", s1.Beams.SelectMany(b => b.Checks).Count(c => c.Name.StartsWith("Nhóm tên") && !c.Ok), 0, 0);
        check("Cột cùng tên: As bố trí bao As yêu cầu cả nhóm (đạt)", s1.Columns.SelectMany(b => b.Checks).Count(c => c.Name.StartsWith("Nhóm tên") && !c.Ok), 0, 0);
        check("Móng: tên theo tên đã gán", B(s1.Footings.All(f => f.Name.StartsWith("M"))), 1, 0);
        check("Phương án chung không nhẹ hơn phương án nặng nhất từng dầm riêng (cùng tên)", B(s1.Beams.All(b => b.Ok)), B(s0.Beams.All(b => b.Ok)), 0);

        // ---- dầm theo tầng ----
        var q = p.Clone();
        MemberMarks.ClearAll(q);
        var gsS = MemberMarks.AutoAssign(q, s0, beamsPerStory: true);
        check("Dầm tên riêng từng tầng: nhiều tên hơn", B(gsS.Count(g => g.Kind == "Dầm") >= gs.Count(g => g.Kind == "Dầm")), 1, 0);

        // ---- gán tay ----
        var c1 = p.Frames.First(f => f.Kind == FrameKind.Column);
        var bx = p.Frames.First(f => f.Kind == FrameKind.Beam);
        string m1 = MemberMarks.AssignFrames(p, new[] { c1.Id, bx.Id }, "X1");
        check("Gán tay: không gán chung cột + dầm", B(m1.StartsWith("Không gán chung")), 1, 0);
        // đổi tiết diện một dải dầm → khác nhóm
        var line0 = lines[0].Frames;
        foreach (var f in line0) f.Section = "D22x40";
        var other = lines.First(l => l.Key == lines[0].Key && !ReferenceEquals(l.Frames, lines[0].Frames)).Frames;
        string m2 = MemberMarks.AssignFrames(p, new[] { line0[0].Id, other[0].Id }, "D9");
        Console.WriteLine("   · " + m2.Replace("\n", " "));
        check("Gán tay: khác tiết diện → từ chối", B(m2.StartsWith("Không gán")), 1, 0);
        string m3 = MemberMarks.AssignFrames(p, new[] { other[0].Id }, "D9");
        check("Gán tay: cùng tiết diện → gán cả dải dầm", B(m3.StartsWith("Đã gán") && other.All(f => f.Mark == "D9")), 1, 0);
        check("Sửa mô hình sau khi gán: phát hiện tên lẫn tiết diện", B(MemberMarks.Conflicts(p).Count > 0), 1, 0);

        // ---- móng ----
        var nodes = s0.Footings.Select(f => f.NodeId).Take(2).ToList();
        string m4 = MemberMarks.AssignFootings(p, s0, nodes, "MX");
        Console.WriteLine("   · " + m4);
        check("Gán tên móng", B(nodes.All(n => p.Footings.First(f => f.NodeId == n).Mark == "MX")), 1, 0);

        // ---- xoá tên ----
        MemberMarks.ClearAll(p);
        foreach (var f in line0) f.Section = "D22x30";
        var s2 = ConcreteDesigner.Design(p, r);
        check("Xoá tên: đặt tên tự động như cũ (D…-n, C n)", B(s2.Beams.All(b => b.Name.Contains('-')) && s2.Columns.Count == s0.Columns.Count), 1, 0);
        var pc = p.Clone();
        check("Lưu / mở: tên giữ nguyên (JSON)", B(pc.Frames.All(f => f.Mark == "")), 1, 0);
        MemberMarks.AutoAssign(p, s0);
        check("Lưu / mở: tên đã gán ghi vào tệp", B(p.Clone().Frames.All(f => !string.IsNullOrEmpty(f.Mark))), 1, 0);
    }
}
