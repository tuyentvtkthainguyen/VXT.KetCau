using KetCauVXT.Drawing;
using KetCauVXT.Mep;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Thoát nước đi dưới sàn có độ dốc; cảnh báo điểm nguy hiểm cho kết cấu (xuyên dầm / cột, lỗ sàn, rãnh tường) và chi tiết gia cường.</summary>
public static class DrainSlopeImpactTests
{
    private static double B(bool v) => v ? 1 : 0;

    private static void EnsureSlabSection(Project p)
    {
        if (!p.SlabSections.Any(s => s.Name == "S120")) p.SlabSections.Add(new SlabSection { Name = "S120", Thickness = 0.12 });
    }

    /// <summary>Phòng vệ sinh tầng T2 (cốt +3,300) 4 × 3 m, tường 220, sàn S120 phủ cả phòng; tầng T1, T3 để có tầng dưới / trên.</summary>
    private static Project Wc()
    {
        var p = new Project();
        p.Stories = new List<Story>
        {
            new() { Name = "T1", Elevation = 0, Height = 3.3 }, new() { Name = "T2", Elevation = 3.3, Height = 3.3 }, new() { Name = "T3", Elevation = 6.6, Height = 3.3 }
        };
        EnsureSlabSection(p);
        p.Nodes.Add(new Node { Id = 1, X = 0, Y = 0, Z = 3.3, Story = "T2" });
        p.Nodes.Add(new Node { Id = 2, X = 4, Y = 0, Z = 3.3, Story = "T2" });
        p.Nodes.Add(new Node { Id = 3, X = 4, Y = 3, Z = 3.3, Story = "T2" });
        p.Nodes.Add(new Node { Id = 4, X = 0, Y = 3, Z = 3.3, Story = "T2" });
        p.Slabs.Add(new Slab { Id = 1, Label = "S1", Nodes = new() { 1, 2, 3, 4 }, Section = "S120", Story = "T2" });
        p.Walls = new List<WallLine>
        {
            new() { Id = 1, Story = "T2", X1 = 0, Y1 = 0, X2 = 4, Y2 = 0, Type = "Tường 220 trát 2 mặt" },
            new() { Id = 2, Story = "T2", X1 = 4, Y1 = 0, X2 = 4, Y2 = 3, Type = "Tường 220 trát 2 mặt" },
            new() { Id = 3, Story = "T2", X1 = 4, Y1 = 3, X2 = 0, Y2 = 3, Type = "Tường 220 trát 2 mặt" },
            new() { Id = 4, Story = "T2", X1 = 0, Y1 = 3, X2 = 0, Y2 = 0, Type = "Tường 220 trát 2 mặt" },
        };
        return p;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "TrienKhai", "DienNuoc");
        Directory.CreateDirectory(dir);

        // ================= 1. Độ dốc mặc định =================
        var set = new MepSettings();
        check("Độ dốc mặc định Ø42 = 3 %", MepDrainage.DefaultSlope(set, 42), 0.03, 1e-12);
        check("Độ dốc mặc định Ø50 = 3 %", MepDrainage.DefaultSlope(set, 50), 0.03, 1e-12);
        check("Độ dốc mặc định Ø90 = 2 %", MepDrainage.DefaultSlope(set, 90), 0.02, 1e-12);
        check("Độ dốc mặc định Ø110 = 2 %", MepDrainage.DefaultSlope(set, 110), 0.02, 1e-12);
        check("Độ dốc tối thiểu Ø110 = 1 %", MepDrainage.MinSlope(110), 0.01, 1e-12);
        check("Độ dốc tối thiểu Ø60 = 1,5 %", MepDrainage.MinSlope(60), 0.015, 1e-12);
        check("Quang treo Ø110 cách 1,2 m", MepDrainage.HangerSpacing(110), 1.2, 1e-12);
        check("Thư viện có ống đứng thoát nước", B(MepLibrary.Defaults().Find(MepDrainage.StackFamily) != null), 1, 0);

        // ================= 2. Đi dưới sàn tới ống đứng =================
        var p = Wc();
        var wc = MepRouting.PlaceOnWall(p, "Bồn cầu", p.Walls[0], 1.0, 1);
        var lv = MepRouting.PlaceOnWall(p, "Lavabo", p.Walls[0], 2.5, 1);
        var runs = MepDrainage.ConnectToStack(p, "T2");
        MepRouting.RebuildAll(p);
        var stack = p.Mep.Devices.FirstOrDefault(d => MepDrainage.IsStack(p.Mep, d));
        check("Tự đề xuất 1 ống đứng", p.Mep.Devices.Count(d => MepDrainage.IsStack(p.Mep, d)), 1, 0);
        check("Ống đứng tại góc tường (0,15; 0,15)", B(stack != null && Math.Abs(stack.X - 0.15) < 1e-9 && Math.Abs(stack.Y - 0.15) < 1e-9), 1, 0);
        check("Nối 2 thiết bị thoát về ống đứng", runs.Count, 2, 0);
        var rw = runs.First(r => r.FromId == wc.Id);
        var rl = runs.First(r => r.FromId == lv.Id);
        check("Tuyến bồn cầu Ø110 thoát thải", B(rw.Size == 110 && rw.System == MepSystemKind.ThoatNuocThai), 1, 0);
        double soffit = 3.3 - 0.12;
        check("Đoạn ngang dưới đáy sàn (z < 3,18)", B(rw.Path.Skip(1).All(q => q.Z < soffit - 1e-9)), 1, 0);
        check("Không đoạn nào trong tường", rw.Path.Count(q => q.WallId > 0) + rl.Path.Count(q => q.WallId > 0), 0, 0);
        check("Cao độ tim đầu tuyến = 3,3 − 0,12 − 0,05 − 0,055", rw.Path[1].Z, 3.3 - 0.12 - 0.05 - 0.055, 1e-9);
        check("Điểm xuống bồn cầu trước mặt tường: y = 0,125 + 0,075", rw.Path[0].Y, 0.2, 1e-9);
        double Lh = MepDrainage.HorizontalLength(rw);
        check("Chiều dài ngang tuyến bồn cầu = 0,85 + 0,05", Lh, 0.9, 1e-9);
        check("Độ dốc 2 %: tụt cao độ = i·L", rw.Path[1].Z - rw.Path[^1].Z, 0.02 * Lh, 1e-9);
        check("Cuối tuyến vào ống đứng (không đoạn đứng lên)", B(Math.Abs(rw.Path[^1].X - 0.15) < 1e-9 && rw.Path[^1].Z < soffit), 1, 0);
        double Ll = MepDrainage.HorizontalLength(rl);
        check("Lavabo Ø42 dốc 3 %: tụt = 0,03·L", rl.Path[1].Z - rl.Path[^1].Z, 0.03 * Ll, 1e-9);
        var prof = MepDrainage.Profile(rw);
        check("Mặt cắt dọc: đáy ống đầu = tim − Ø/2", prof[0].Invert, rw.Path[1].Z - 0.055, 1e-9);
        check("Mặt cắt dọc: đáy ống cuối thấp hơn đầu", B(prof[^1].Invert < prof[0].Invert), 1, 0);

        // phụ kiện
        var fit = p.Mep.Fittings;
        check("Thoát nước không dùng cút 90°", fit.Count(f => MepSystems.IsDrain(f.System) && f.Kind == MepFittingKind.Cut90), 0, 0);
        check("Tuyến bồn cầu: 2 lần đổi hướng 90° → 4 chếch 45°", fit.Count(f => f.RunId == rw.Id && f.Kind == MepFittingKind.Chech45), 4, 0);
        check("Mỗi nhánh vào ống đứng 1 tê xiên (Y)", fit.Count(f => f.Kind == MepFittingKind.TeXien && f.DeviceId == stack.Id), 2, 0);
        check("Chân ống đứng: 1 ống kiểm tra", fit.Count(f => f.Kind == MepFittingKind.KiemTra && f.DeviceId == stack.Id), 1, 0);
        check("Tên phụ kiện tê xiên", B(fit.Any(f => f.Name.StartsWith("Tê xiên 45° (chữ Y)"))), 1, 0);
        p.Mep.Settings.CleanoutSpacing = 1.0;
        MepRouting.RebuildAll(p);
        check("Tuyến ngang dài: ống kiểm tra mỗi 1 m (⌊L/1⌋)", p.Mep.Fittings.Count(f => f.RunId == rl.Id && f.Kind == MepFittingKind.KiemTra), Math.Floor(Ll / 1.0 + 1e-9), 0);
        p.Mep.Settings.CleanoutSpacing = 12;
        MepRouting.RebuildAll(p);

        // quang treo
        check("Quang treo tuyến bồn cầu = ⌈0,9/1,2⌉ + 1", MepDrainage.Hangers(p, rw), Math.Ceiling(0.9 / 1.2) + 1, 0);
        var rows = MepQuantities.Rows(p);
        check("Thống kê có quang treo ống Ø110", B(rows.Any(r => r.Item.StartsWith("Quang treo") && r.Item.EndsWith("Ø110"))), 1, 0);
        check("Thống kê có tê xiên", B(rows.Any(r => r.Item.StartsWith("Tê xiên"))), 1, 0);

        // độ dốc nhập tay
        rw.Slope = 0.03; MepRouting.Route(p, rw);
        check("Độ dốc nhập 3 %: tụt = 0,03·L", rw.Path[1].Z - rw.Path[^1].Z, 0.03 * MepDrainage.HorizontalLength(rw), 1e-9);
        rw.Slope = 0.005; MepRouting.RebuildAll(p);
        var w1 = MepUpdater.Reconcile(p).Warnings;
        check("Độ dốc 0,5 % < tối thiểu → cảnh báo DO_DOC", w1.Count(x => x.Code == "DO_DOC" && x.RunId == rw.Id), 1, 0);
        rw.Slope = 0;
        // trần giả
        check("Không gian trần 0,6 m đủ → không cảnh báo", MepUpdater.Reconcile(p).Warnings.Count(x => x.Code == "KHONG_GIAN_TRAN"), 0, 0);
        p.Mep.Settings.CeilingVoid = 0.1;
        check("Trần giả 0,1 m → cảnh báo KHONG_GIAN_TRAN (ống bồn cầu)", MepUpdater.Reconcile(p).Warnings.Count(x => x.Code == "KHONG_GIAN_TRAN" && x.RunId == rw.Id), 1, 0);
        p.Mep.Settings.CeilingVoid = 0.6;
        // gợi ý thông hơi
        check("Gợi ý ống thông hơi cho ống đứng", MepUpdater.Reconcile(p).Warnings.Count(x => x.Code == "THONG_HOI"), 1, 0);
        // hạ cốt
        p.Slabs[0].LevelOffset = -0.2;
        MepRouting.Route(p, rw);
        check("Sàn hạ cốt 200: đi trong lớp hạ cốt", B(MepDrainage.ModeOf(p, rw, (rw.Path[0].X, rw.Path[0].Y)) == MepDrainage.DrainMode.HaCot), 1, 0);
        check("Hạ cốt: tim đầu tuyến = 3,3 − 0,02 − 0,055", rw.Path[1].Z, 3.3 - 0.02 - 0.055, 1e-9);
        p.Slabs[0].LevelOffset = 0;
        // ép trong tường
        rl.DrainInWall = true;
        MepRouting.RebuildAll(p);
        var w2 = MepUpdater.Reconcile(p).Warnings;
        check("Ép đi trong tường → cảnh báo THOAT_TRONG_TUONG", w2.Count(x => x.Code == "THOAT_TRONG_TUONG" && x.RunId == rl.Id), 1, 0);
        check("Ép trong tường: có đoạn nằm trong tường", B(rl.Path.Any(q => q.WallId > 0)), 1, 0);
        rl.DrainInWall = false;
        MepRouting.RebuildAll(p);

        // tầng thấp nhất: ống chôn dưới nền, không quang treo
        var fd = MepRouting.PlaceFree(p, "Thoát sàn (phễu thu)", "T1", 1, 1);
        var wc1 = MepRouting.PlaceFree(p, "Bồn cầu", "T1", 3, 1);
        var r1 = MepDrainage.ConnectToStack(p, "T1");
        check("Tầng T1 là tầng thấp nhất", B(MepDrainage.IsLowestStory(p, "T1")), 1, 0);
        check("Tầng thấp nhất: không quang treo", r1.Sum(r => MepDrainage.Hangers(p, r)), 0, 0);
        _ = fd; _ = wc1;

        // bản vẽ thoát nước
        MepRouting.RebuildAll(p);
        var plan = MepDrawings.Plan(p, "T2", MepDrawings.PlanKind.ThoatNuoc);
        check("Có mặt bằng thoát nước T2", B(plan != null), 1, 0);
        if (plan != null)
        {
            string txt = plan.W.ToDxfStringDec();
            check("Mặt bằng thoát: ghi độ dốc i=2%", B(txt.Contains("i=2%")), 1, 0);
            check("Mặt bằng thoát: ghi độ dốc i=3% (lavabo)", B(txt.Contains("i=3%")), 1, 0);
            check("Mặt bằng thoát: cao độ đáy ống", B(txt.Contains("Đáy ống")), 1, 0);
            check("Mặt bằng thoát: ký hiệu ống đứng OĐ", B(txt.Contains("OĐ")), 1, 0);
            plan.W.Save(Path.Combine(dir, "MB_ThoatNuoc_T2_DoDoc.dxf"));
        }
        check("Ký hiệu ống đứng vẽ được (2 vòng + chấm)", B(MepSymbols.Draw(new DxfWriter(100), "ongdung", 0, 0, 200) >= 3), 1, 0);

        // ================= 3. Quy tắc lỗ xuyên dầm =================
        var ok = StructuralImpact.EvaluateBeamHole(0.22, 0.45, 4.0, 2.0, 0.225, 100);
        check("Lỗ Ø100 giữa nhịp, giữa chiều cao dầm 220×450 → cho phép", (int)ok.Severity, (int)ImpactSeverity.ChoPhep, 0);
        check("Lỗ cách mép gối 0,3 < h → nguy hiểm", (int)StructuralImpact.EvaluateBeamHole(0.22, 0.45, 4.0, 0.3, 0.225, 100).Severity, (int)ImpactSeverity.NguyHiem, 0);
        check("Lỗ Ø170 > h/3 = 150 → nguy hiểm", (int)StructuralImpact.EvaluateBeamHole(0.22, 0.45, 4.0, 2.0, 0.225, 170).Severity, (int)ImpactSeverity.NguyHiem, 0);
        check("Lỗ Ø130 (h/4 < Ø ≤ h/3) → cảnh báo", (int)StructuralImpact.EvaluateBeamHole(0.22, 0.45, 4.0, 2.0, 0.225, 130).Severity, (int)ImpactSeverity.CanhBao, 0);
        check("Cách gối 0,6 (≥ h, < L0/3) → cảnh báo", (int)StructuralImpact.EvaluateBeamHole(0.22, 0.45, 4.0, 0.6, 0.225, 100).Severity, (int)ImpactSeverity.CanhBao, 0);
        check("Kháng chấn: cách gối 0,6 < 2h → nguy hiểm", (int)StructuralImpact.EvaluateBeamHole(0.22, 0.45, 4.0, 0.6, 0.225, 100, seismic: true).Severity, (int)ImpactSeverity.NguyHiem, 0);
        check("Lỗ sát đáy dầm (ngoài 1/3 giữa chiều cao) → nguy hiểm", (int)StructuralImpact.EvaluateBeamHole(0.22, 0.45, 4.0, 2.0, 0.10, 100).Severity, (int)ImpactSeverity.NguyHiem, 0);
        check("Hai lỗ cách 0,2 < 3Ø = 0,3 → nguy hiểm", (int)StructuralImpact.EvaluateBeamHole(0.22, 0.45, 4.0, 2.0, 0.225, 100, false, 0.2, 100).Severity, (int)ImpactSeverity.NguyHiem, 0);
        check("Gia cường: thép dọc bổ sung L = 0,1 + 2·max(40·12; 500) = 1,10 m", ok.Strengthening.Bars[1].Length, 1.10, 1e-9);
        check("Gia cường: 2 đai mỗi bên (Ø ≤ h/4) → 4 đai", ok.Strengthening.Bars[0].Count, 4, 0);
        check("Gia cường: 3 đai mỗi bên khi Ø > h/4 → 6 đai", StructuralImpact.EvaluateBeamHole(0.22, 0.45, 4.0, 2.0, 0.225, 130).Strengthening.Bars[0].Count, 6, 0);

        // ================= 4. Lỗ sàn, thép gia cường mép lỗ =================
        var tb = StructuralImpact.TrimmingBars(0.5, 0.5, 8, 0.2, 12);
        check("Lỗ 500: số thanh Ø8a200 bị cắt = ⌊0,5/0,2⌋ = 2", tb.Cut, 2, 0);
        check("Lỗ 500: As cắt = 2·π·8²/4 mm²", tb.AsCut * 1e6, 2 * Math.PI * 64 / 4, 1e-6);
        check("Lỗ 500: mỗi bên tối thiểu 2Ø12", tb.PerSide, 2, 0);
        check("Lỗ 500: dài = 0,5 + 2·40·12 mm = 1,46 m", tb.Length, 1.46, 1e-9);
        var tb2 = StructuralImpact.TrimmingBars(1.2, 1.2, 10, 0.15, 12);
        check("Lỗ 1200, Ø10a150: cắt 8 thanh → As/2 = 314 mm² → 3Ø12 mỗi bên", tb2.PerSide, Math.Ceiling(8 * Math.PI * 100 / 4 / 2 / (Math.PI * 144 / 4)), 0);
        check("Lỗ sàn cách cột 0,2 < 2,5·hs → nguy hiểm (chọc thủng)", (int)StructuralImpact.EvaluateSlabOpening(0.13, 0.13, 0.12, 0.2).Severity, (int)ImpactSeverity.NguyHiem, 0);
        check("Lỗ sàn cách cột 0,5 < 6·hs → cảnh báo", (int)StructuralImpact.EvaluateSlabOpening(0.13, 0.13, 0.12, 0.5).Severity, (int)ImpactSeverity.CanhBao, 0);
        check("Lỗ nhỏ xa cột → chỉ gợi ý", (int)StructuralImpact.EvaluateSlabOpening(0.13, 0.13, 0.12, 2.0).Severity, (int)ImpactSeverity.GoiY, 0);
        var big = StructuralImpact.EvaluateSlabOpening(0.6, 0.4, 0.12, null);
        check("Lỗ 600×400 > 300 → cảnh báo + thép gia cường", B(big.Severity == ImpactSeverity.CanhBao && big.Strengthening != null && big.Strengthening.Bars.Count == 3), 1, 0);

        // ================= 5. Quét mô hình: dầm, cột, sàn, tường =================
        var q = new Project();
        q.Stories = new List<Story> { new() { Name = "T1", Elevation = 0, Height = 3.3 }, new() { Name = "T2", Elevation = 3.3, Height = 3.3 } };
        EnsureSlabSection(q);
        q.FrameSections.Add(new FrameSection { Name = "C22", B = 0.22, H = 0.22 });
        q.FrameSections.Add(new FrameSection { Name = "D22x45", B = 0.22, H = 0.45 });
        q.Nodes.Add(new Node { Id = 1, X = 0, Y = 1.5, Z = 0, Story = "T1" });
        q.Nodes.Add(new Node { Id = 2, X = 0, Y = 1.5, Z = 3.3, Story = "T2" });
        q.Nodes.Add(new Node { Id = 3, X = 6, Y = 1.5, Z = 0, Story = "T1" });
        q.Nodes.Add(new Node { Id = 4, X = 6, Y = 1.5, Z = 3.3, Story = "T2" });
        q.Frames.Add(new FrameElement { Id = 1, Label = "C1", NodeI = 1, NodeJ = 2, Section = "C22", Kind = FrameKind.Column, Story = "T2" });
        q.Frames.Add(new FrameElement { Id = 2, Label = "C2", NodeI = 3, NodeJ = 4, Section = "C22", Kind = FrameKind.Column, Story = "T2" });
        q.Frames.Add(new FrameElement { Id = 3, Label = "D1", NodeI = 2, NodeJ = 4, Section = "D22x45", Kind = FrameKind.Beam, Story = "T2" });
        q.Nodes.Add(new Node { Id = 5, X = 0, Y = 0, Z = 3.3, Story = "T2" });
        q.Nodes.Add(new Node { Id = 6, X = 6, Y = 0, Z = 3.3, Story = "T2" });
        q.Nodes.Add(new Node { Id = 7, X = 6, Y = 3, Z = 3.3, Story = "T2" });
        q.Nodes.Add(new Node { Id = 8, X = 0, Y = 3, Z = 3.3, Story = "T2" });
        q.Slabs.Add(new Slab { Id = 1, Label = "S1", Nodes = new() { 5, 6, 7, 8 }, Section = "S120", Story = "T2" });
        q.Walls = new List<WallLine>
        {
            new() { Id = 1, Story = "T2", X1 = 0, Y1 = 4, X2 = 3, Y2 = 4, Type = "Tường 110 trát 2 mặt" },
            new() { Id = 2, Story = "T2", X1 = 0, Y1 = 5, X2 = 3, Y2 = 5, Type = "Tường BTCT 200" },
        };
        MepRun R(int id, double size, MepRunKind kind, params MepPt[] pts)
        {
            var r = new MepRun { Id = id, System = MepSystemKind.ThoatNuocThai, Kind = kind, Story = "T2", Size = size, Path = pts.ToList() };
            q.Mep.Runs.Add(r);
            return r;
        }
        double zc = 2.85 + 0.225;
        R(1, 60, MepRunKind.Ong, new MepPt(3, 0.5, zc), new MepPt(3, 2.5, zc));              // giữa nhịp – cho phép
        R(2, 60, MepRunKind.Ong, new MepPt(0.3, 0.5, zc), new MepPt(0.3, 2.5, zc));          // sát gối – nguy hiểm
        R(3, 160, MepRunKind.Ong, new MepPt(4, 0.5, zc), new MepPt(4, 2.5, zc));             // Ø lớn – nguy hiểm
        R(4, 50, MepRunKind.Ong, new MepPt(2, 1.5, 3.5), new MepPt(2, 1.5, 2.5));            // ống đứng xuyên dầm
        R(5, 25, MepRunKind.Ong, new MepPt(5.5, 1.5, 1.0), new MepPt(6.5, 1.5, 1.0));        // qua cột
        R(6, 110, MepRunKind.Ong, new MepPt(0.35, 1.75, 3.5), new MepPt(0.35, 1.75, 2.9));   // lỗ sàn sát cột
        R(7, 20, MepRunKind.Ong, new MepPt(2, 2.5, 3.5), new MepPt(2, 2.5, 2.9));
        R(8, 20, MepRunKind.Ong, new MepPt(2.5, 2.5, 3.5), new MepPt(2.5, 2.5, 2.9));
        R(9, 20, MepRunKind.Ong, new MepPt(3, 2.5, 3.5), new MepPt(3, 2.5, 2.9));
        R(10, 25, MepRunKind.Ong, new MepPt(0.5, 4, 3.6, 1), new MepPt(2.0, 4, 3.6, 1));     // rãnh ngang 1,5 m trong tường 110
        R(11, 25, MepRunKind.Ong, new MepPt(0.5, 5, 3.6, 2), new MepPt(1.0, 5, 3.6, 2));     // rãnh trong tường BTCT
        var items = StructuralImpact.Scan(q);
        ImpactSeverity? Sev(int run, string code) => items.FirstOrDefault(i => i.RunId == run && i.Code == code)?.Severity;
        check("Quét: xuyên dầm giữa nhịp → cho phép (ống lồng)", (int)(Sev(1, "XUYEN_DAM") ?? (ImpactSeverity)(-1)), (int)ImpactSeverity.ChoPhep, 0);
        check("Quét: xuyên dầm sát gối → nguy hiểm", (int)(Sev(2, "XUYEN_DAM") ?? (ImpactSeverity)(-1)), (int)ImpactSeverity.NguyHiem, 0);
        check("Quét: Ø160 (lỗ 180 > h/3) → nguy hiểm", (int)(Sev(3, "XUYEN_DAM") ?? (ImpactSeverity)(-1)), (int)ImpactSeverity.NguyHiem, 0);
        check("Quét: ống đứng xuyên dầm → nguy hiểm", (int)(Sev(4, "XUYEN_DAM") ?? (ImpactSeverity)(-1)), (int)ImpactSeverity.NguyHiem, 0);
        check("Quét: ống qua cột → CẤM", (int)(Sev(5, "XUYEN_COT") ?? (ImpactSeverity)(-1)), (int)ImpactSeverity.Cam, 0);
        check("Quét: lỗ sàn sát cột → nguy hiểm (chọc thủng)", (int)(Sev(6, "LO_SAN") ?? (ImpactSeverity)(-1)), (int)ImpactSeverity.NguyHiem, 0);
        check("Quét: 3 lỗ sàn thẳng hàng < 1 m → cảnh báo đường yếu", items.Count(i => i.Code == "LO_SAN_THANG_HANG"), 1, 0);
        check("Quét: rãnh ngang dài 1,5 m trong tường 110 → cảnh báo", (int)(Sev(10, "RANH_TUONG") ?? (ImpactSeverity)(-1)), (int)ImpactSeverity.CanhBao, 0);
        check("Quét: rãnh trong tường BTCT → nguy hiểm", (int)(Sev(11, "RANH_TUONG") ?? (ImpactSeverity)(-1)), (int)ImpactSeverity.NguyHiem, 0);
        check("Quét: mỗi tuyến – mỗi dầm một mục", items.Count(i => i.Code == "XUYEN_DAM" && i.RunId == 1), 1, 0);
        check("Quét: đề xuất có gia cường cho lỗ xuyên dầm", B(items.First(i => i.RunId == 1 && i.Code == "XUYEN_DAM").Strengthening != null), 1, 0);
        check("Quét: nguy hiểm sát gối – đề xuất dời lỗ", B(items.First(i => i.RunId == 2 && i.Code == "XUYEN_DAM").Proposal.Contains("dời lỗ")), 1, 0);
        var wq = StructuralImpact.ToWarnings(items);
        check("Cảnh báo điện nước mang mức độ", B(wq.Any(w => w.Code == "XUYEN_COT" && w.Severity == "Cấm")), 1, 0);

        // chi tiết gia cường
        var figs = StructuralImpact.Figures(items);
        check("Có chi tiết gia cường lỗ xuyên dầm", B(figs.Any(f => f.Name.StartsWith(StructuralImpact.BeamDetailTitle))), 1, 0);
        foreach (var f in figs.Take(3))
        {
            string t = f.W.ToDxfStringDec();
            if (f.Name.StartsWith(StructuralImpact.BeamDetailTitle)) check("Chi tiết dầm: ghi ống lồng", B(t.Contains("Ống lồng thép")), 1, 0);
        }
        var sf = StructuralImpact.SlabFigure(big.Strengthening);
        check("Chi tiết gia cường mép lỗ sàn: tiêu đề", B(sf.W.ToDxfStringDec().Contains(StructuralImpact.SlabDetailTitle)), 1, 0);
        int k = 0;
        foreach (var f in figs) f.W.Save(Path.Combine(dir, $"GiaCuong_{++k}.dxf"));
        sf.W.Save(Path.Combine(dir, "GiaCuong_LoSan.dxf"));

        // ================= 6. Va chạm: ống lồng hợp lệ / không hợp lệ =================
        try
        {
            var cl = ClashDetector.Run(q);
            ClashType? T(int run) => cl.FirstOrDefault(c => (c.A.Id.StartsWith($"DN-ONG-{run}-") && c.B.Id == "DAM-3") || (c.B.Id.StartsWith($"DN-ONG-{run}-") && c.A.Id == "DAM-3"))?.Type;
            check("Va chạm: ống qua dầm giữa nhịp → ống lồng", B(T(1) == ClashType.OngLong), 1, 0);
            check("Va chạm: ống qua dầm sát gối → va chạm cứng", B(T(2) == ClashType.Cung), 1, 0);
            check("Va chạm: ống đứng xuyên dầm → va chạm cứng", B(T(4) == ClashType.Cung), 1, 0);
        }
        catch (Exception ex)
        {
            check("Va chạm chạy không lỗi: " + ex.Message, 0, 1, 0);
        }

        // ================= 7. Tích hợp cảnh báo điện nước =================
        var cs = MepUpdater.CheckStructure(q);
        check("Cảnh báo kết cấu: XUYEN_COT mang mức Cấm", cs.Count(x => x.Code == "XUYEN_COT" && x.Severity == "Cấm"), 1, 0);
        check("Cảnh báo kết cấu: XUYEN_DAM có đề xuất xử lý", B(cs.Where(x => x.Code == "XUYEN_DAM").All(x => !string.IsNullOrEmpty(x.Fix))), 1, 0);
    }
}
