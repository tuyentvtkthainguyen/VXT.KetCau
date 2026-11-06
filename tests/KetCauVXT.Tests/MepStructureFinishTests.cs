using KetCauVXT.Design;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Mep;
using KetCauVXT.Model;
using KetCauVXT.Report;
using KetCauVXT.Ui;

namespace KetCauVXT.Tests;

/// <summary>Chi tiết gia cường lỗ xuyên từ cảnh báo điện nước đưa vào hồ sơ KẾT CẤU (tờ KC-10, mặt bằng, mặt đứng dầm, thống kê thép,
/// kiểm tra cắt tại lỗ, hồ sơ triển khai); tường trừ phần rãnh / hộp điện nước chiếm chỗ; mặt cắt chọn nhịp có đủ sàn + phần nhìn thấy;
/// biểu tượng nút "Đặt & xem"; kích thước tạm tường / cột.</summary>
public static class MepStructureFinishTests
{
    private static double B(bool v) => v ? 1 : 0;

    /// <summary>Dầm D1 22×45 nhịp 6 m (trục 1–2, y = 1,5) giữa 2 cột C22, sàn S1 120; tuyến ống xuyên dầm giữa nhịp (Ø60 ×2) và Ø160,
    /// ống đứng Ø400 xuyên sàn (lỗ &gt; 300 mm).</summary>
    private static Project Model()
    {
        var pq = new Project();
        pq.Stories = new List<Story> { new() { Name = "T1", Elevation = 0, Height = 3.3 }, new() { Name = "T2", Elevation = 3.3, Height = 3.3 } };
        if (!pq.SlabSections.Any(s => s.Name == "S120")) pq.SlabSections.Add(new SlabSection { Name = "S120", Thickness = 0.12 });
        pq.FrameSections.Add(new FrameSection { Name = "C22", B = 0.22, H = 0.22 });
        pq.FrameSections.Add(new FrameSection { Name = "D22x45", B = 0.22, H = 0.45 });
        pq.Nodes.Add(new Node { Id = 1, X = 0, Y = 1.5, Z = 0, Story = "T1" });
        pq.Nodes.Add(new Node { Id = 2, X = 0, Y = 1.5, Z = 3.3, Story = "T2" });
        pq.Nodes.Add(new Node { Id = 3, X = 6, Y = 1.5, Z = 0, Story = "T1" });
        pq.Nodes.Add(new Node { Id = 4, X = 6, Y = 1.5, Z = 3.3, Story = "T2" });
        pq.Frames.Add(new FrameElement { Id = 1, Label = "C1", NodeI = 1, NodeJ = 2, Section = "C22", Kind = FrameKind.Column, Story = "T2" });
        pq.Frames.Add(new FrameElement { Id = 2, Label = "C2", NodeI = 3, NodeJ = 4, Section = "C22", Kind = FrameKind.Column, Story = "T2" });
        pq.Frames.Add(new FrameElement { Id = 3, Label = "D1", NodeI = 2, NodeJ = 4, Section = "D22x45", Kind = FrameKind.Beam, Story = "T2" });
        pq.Nodes.Add(new Node { Id = 5, X = 0, Y = 0, Z = 3.3, Story = "T2" });
        pq.Nodes.Add(new Node { Id = 6, X = 6, Y = 0, Z = 3.3, Story = "T2" });
        pq.Nodes.Add(new Node { Id = 7, X = 6, Y = 3, Z = 3.3, Story = "T2" });
        pq.Nodes.Add(new Node { Id = 8, X = 0, Y = 3, Z = 3.3, Story = "T2" });
        pq.Slabs.Add(new Slab { Id = 1, Label = "S1", Nodes = new() { 5, 6, 7, 8 }, Section = "S120", Story = "T2" });
        void R(int id, double size, params MepPt[] pts) =>
            pq.Mep.Runs.Add(new MepRun { Id = id, System = MepSystemKind.ThoatNuocThai, Kind = MepRunKind.Ong, Story = "T2", Size = size, Path = pts.ToList() });
        double zc = 2.85 + 0.225;
        R(1, 60, new MepPt(2.9, 0.5, zc), new MepPt(2.9, 2.5, zc));     // giữa nhịp
        R(2, 60, new MepPt(3.6, 0.5, zc), new MepPt(3.6, 2.5, zc));     // giữa nhịp (cùng Ø → cùng ký hiệu)
        R(3, 160, new MepPt(4.5, 0.5, zc), new MepPt(4.5, 2.5, zc));    // Ø lớn – nguy hiểm, vẫn có chi tiết
        R(4, 400, new MepPt(3, 2.6, 3.6), new MepPt(3, 2.6, 2.9));       // ống đứng Ø400 xuyên sàn → lỗ 420 > 300 → thép gia cường mép lỗ
        return pq;
    }

    private static DesignSummary Design(Project p)
    {
        var s = new DesignSummary();
        var d = new BeamLineDesign { Name = "D1", Story = "T2", B = 0.22, H = 0.45, Section = "D22x45", TopElevation = 3.3, Frames = new() { 3 } };
        d.Spans.Add(new BeamSpanResult { Frame = 3, L = 6, Lclear = 5.78, SupportWidthLeft = 0.22, SupportWidthRight = 0.22, QMax = 80, StirrupDia = 6, StirrupSupport = 0.1, StirrupMid = 0.2, SupportZone = 1.2, LeftSupport = "1", RightSupport = "2" });
        d.Bars.Add(new BarMark { Mark = 1, Role = "Dưới", Dia = 16, Count = 2, Length = 6.4, Start = -0.1, End = 6.1 });
        d.Bars.Add(new BarMark { Mark = 2, Role = "Trên", Dia = 16, Count = 2, Length = 6.4, Start = -0.1, End = 6.1, IsTop = true });
        d.Bars.Add(new BarMark { Mark = 3, Role = "Đai", Dia = 6, Count = 40, Length = 1.3, Shape = "Đai", Spacing = 0.15 });
        s.Beams.Add(d);
        return s;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "TrienKhai", "DienNuocKetCau");
        Directory.CreateDirectory(dir);

        // ================= 1. Sổ lỗ xuyên =================
        var p = Model();
        var marks = PenetrationRegister.Build(p);
        var lx = marks.Where(m => m.Kind == "LoXuyenDam").ToList();
        var ls = marks.Where(m => m.Kind == "LoSan").ToList();
        check("Sổ lỗ: 3 lỗ xuyên dầm (Ø60 ×2, Ø160)", lx.Count, 3, 0);
        check("Sổ lỗ: 2 lỗ Ø60 giống nhau cùng ký hiệu LX1", lx.Count(m => m.Tag == "LX1"), 2, 0);
        check("Sổ lỗ: lỗ Ø160 ký hiệu LX2", B(lx.Any(m => m.Tag == "LX2" && Math.Abs(m.S.HoleDia - 180) < 1e-6)), 1, 0);
        check("Sổ lỗ: lỗ sàn lớn có thép gia cường LS1", B(ls.Count == 1 && ls[0].Tag == "LS1"), 1, 0);
        check("Sổ lỗ: cao độ mặt bằng dầm = 3,3", lx[0].LevelZ, 3.3, 1e-9);

        // ================= 2. Tờ KC chi tiết gia cường =================
        var figs = PenetrationRegister.Figures(p, marks);
        check("Tờ KC-10: 3 hình (LX1, LX2, LS1)", figs.Count, 3, 0);
        string f0 = figs[0].W.ToDxfStringDec();
        check("Hình LX1: có tiêu đề chi tiết gia cường lỗ xuyên dầm + ký hiệu", B(f0.Contains(StructuralImpact.BeamDetailTitle) && f0.Contains("LX1")), 1, 0);
        check("Hình LX1: SL = 2", B(f0.Contains("SL=2")), 1, 0);
        check("Hình LS1: chi tiết gia cường mép lỗ sàn", B(figs[2].W.ToDxfStringDec().Contains(StructuralImpact.SlabDetailTitle)), 1, 0);
        var s = Design(p);
        List<DrawingItem> items;
        try { items = DrawingSet.BuildDrawings(p, s); }
        catch (Exception ex) { check("Dựng hồ sơ KC không lỗi: " + ex.Message, 0, 1, 0); items = new List<DrawingItem>(); PenetrationRegister.AttachToDesign(p, s); }
        var kc10 = items.FirstOrDefault(i => i.Number == "KC-10");
        check("Hồ sơ KC: có tờ KC-10 CHI TIẾT GIA CƯỜNG LỖ XUYÊN KẾT CẤU", B(kc10 != null && kc10.Title == PenetrationRegister.SheetTitle), 1, 0);
        if (kc10?.W != null) File.WriteAllText(Path.Combine(dir, "KC-10_GiaCuongLoXuyen.dxf"), kc10.W.ToDxfString());

        // ================= 3. Gắn vào thiết kế: lỗ trên mặt đứng, kiểm tra cắt, thống kê =================
        var d = s.Beams[0];
        check("Thiết kế: dầm D1 có 3 lỗ trên mặt đứng", d.Holes.Count, 3, 0);
        check("Thiết kế: vị trí lỗ trên mặt đứng x = 2,9 m (từ tim gối đầu)", d.Holes.OrderBy(h => h.X).First().X, 2900, 1e-6);
        check("Thiết kế: tâm lỗ cách đáy dầm = h/2 = 225", d.Holes[0].Zb, 225, 1e-6);
        var shear = d.Checks.Where(c => c.Name.StartsWith("Chịu cắt tại lỗ xuyên")).ToList();
        check("Thiết kế: kiểm tra cắt tại lỗ cho 3 lỗ", shear.Count, 3, 0);
        var conc = Material.Concrete(p.Concrete.ConcreteBeam); var stl = Material.Rebar(p.Concrete.RebarStirrup);
        var sh80 = PenetrationRegister.ShearAtHole(0.22, 0.45, p.Concrete.CoverBeam, 80, conc, stl, 8, 0.2);
        check("Cắt tại lỗ: h0n = h0 − Ø (0,45 − 0,025 − 0,01 − 0,08)", sh80.H0n, 0.45 - p.Concrete.CoverBeam - 0.01 - 0.08, 1e-9);
        var sh180 = PenetrationRegister.ShearAtHole(0.22, 0.45, p.Concrete.CoverBeam, 180, conc, stl, 8, 0.2);
        check("Cắt tại lỗ: lỗ lớn → khả năng chịu cắt nhỏ hơn", B(sh180.Capacity < sh80.Capacity && sh180.Capacity > 0), 1, 0);
        // (đổi từ fork X: Q tại lỗ nội suy theo vị trí lỗ thay vì Qmax của nhịp) – lỗ x = 2,9 m trong nhịp L = 6 m: V = 80·|1 − 2·2,9/6|
        check("Thiết kế: dòng kiểm tra cắt dùng Q tại vị trí lỗ (nội suy, ≤ Qmax 80)", B(shear.All(c => c.Demand <= 80 + 1e-9)
            && shear.Any(c => Math.Abs(c.Demand - PenetrationRegister.ShearAt(80, 6, 2.9)) < 1e-6)), 1, 0);
        check("Thống kê: 2 cấu kiện phụ gia cường (dầm D1, sàn S1)", s.Others.Count(o => o.Member is PenetrationMember), 2, 0);
        var rows = RebarSchedule.BuildRows(s);
        var pr = rows.Where(r => r.Member.EndsWith(PenetrationRegister.MemberSuffix)).ToList();
        check("Thống kê: có hàng thép gia cường lỗ xuyên", B(pr.Count >= 5), 1, 0);
        double wExp = pr.Sum(r => Math.Round(RebarSchedule.UnitWeight(r.Dia) * r.TotalLengthM, 2));
        check("Thống kê: trọng lượng thép gia cường = Σ 0,00617·d²·L", pr.Sum(r => r.WeightKg), wExp, 1e-6);
        var lx1Bars = pr.Where(r => r.Role.Contains("LX1") && r.Role.Contains("trên lỗ")).ToList();
        check("Thống kê: LX1 – thép trên lỗ 2 lỗ × 2 thanh = 4 thanh", lx1Bars.Sum(r => r.BarsTotal), 4, 0);
        // gọi lại không nhân đôi
        PenetrationRegister.AttachToDesign(p, s);
        check("Gắn lại không nhân đôi (lỗ, cấu kiện phụ)", B(d.Holes.Count == 3 && s.Others.Count(o => o.Member is PenetrationMember) == 2), 1, 0);

        // mặt đứng dầm: lỗ + ghi chú; thép gia cường không vẽ như thép dọc chạy suốt
        var bf = BeamDrawing.Build(p, d, s);
        string bt = bf[0].W.ToDxfStringDec();
        check("Mặt đứng dầm: ghi chú 'xem chi tiết LX1'", B(bt.Contains("xem chi tiết LX1")), 1, 0);
        check("Mặt đứng dầm: ghi chú lỗ LX2", B(bt.Contains("LX2 – lỗ Ø180")), 1, 0);
        File.WriteAllText(Path.Combine(dir, "MatDungDam_LoXuyen.dxf"), bf[0].W.ToDxfString());
        // mặt bằng kết cấu: dấu LX / LS
        var plan = FramingPlan.Build(p, s, p.Stories[1], 100);
        string pt = plan.ToDxfStringDec();
        check("Mặt bằng kết cấu: dấu LX1, LX2, LS1", B(pt.Contains("LX1") && pt.Contains("LX2") && pt.Contains("LS1")), 1, 0);
        check("Dấu mặt bằng: 4 lỗ tại cao độ +3,3", PenetrationRegister.DrawPlanMarks(new DxfWriter(100), p, 3.3, marks), 4, 0);
        check("Dấu mặt bằng: cao độ khác → 0", PenetrationRegister.DrawPlanMarks(new DxfWriter(100), p, 0, marks), 0, 0);

        // ================= 4. Hồ sơ triển khai: dầm / sàn có lỗ =================
        var ds = new DetailSettings();
        var mb = new ManualBeam { Name = "D1", B = 0.22, H = 0.45, Spans = "6", Q = 70 };
        mb.Penetrations.Add(new BeamPenetration { X = 3.0, Dia = 80 });
        mb.Penetrations.Add(new BeamPenetration { X = 0.3, Dia = 80 });   // sát gối – cảnh báo
        var rb = mb.Run(ds);
        var bd = (BeamLineDesign)rb.Source;
        check("Triển khai dầm: 2 lỗ trên mặt đứng", bd.Holes.Count, 2, 0);
        check("Triển khai dầm: thép gia cường vào thép cấu kiện", B(rb.Bars.Count(b => BeamHoleDraw.IsHoleBar(b)) >= 6), 1, 0);
        check("Triển khai dầm: kiểm tra cắt tại 2 lỗ (có Q)", rb.Checks.Count(c => c.Name.StartsWith("Chịu cắt tại lỗ xuyên")), 2, 0);
        check("Triển khai dầm: lỗ sát gối (x = 0,3 → LX1) có CẢNH BÁO", B(rb.Messages.Any(m => m.StartsWith("CẢNH BÁO lỗ xuyên LX1"))), 1, 0);
        check("Triển khai dầm: lỗ giữa nhịp (LX2) không cảnh báo", B(!rb.Messages.Any(m => m.StartsWith("CẢNH BÁO lỗ xuyên LX2"))), 1, 0);
        check("Triển khai dầm: thép gia cường có số hiệu", B(rb.Bars.Where(b => BeamHoleDraw.IsHoleBar(b)).All(b => b.Mark > 0)), 1, 0);
        var mf = mb.Draw(rb, ds);
        check("Triển khai dầm: mặt đứng ghi 'xem chi tiết LX1'", B(mf.Count > 0 && mf[0].W.ToDxfStringDec().Contains("xem chi tiết LX1")), 1, 0);
        var msl = new ManualSlab { Name = "S1", Lx = 3.6, Ly = 4.2, T = 0.12 };
        var rs0 = msl.Run(ds);
        msl.Penetrations.Add(new SlabPenetration { X = 1.0, Y = 1.0, W = 0.5, L = 0.4 });
        var rs = msl.Run(ds);
        check("Triển khai sàn: bê tông trừ lỗ 0,5 × 0,4 × 0,12", rs0.ConcreteVolume - rs.ConcreteVolume, 0.5 * 0.4 * 0.12, 1e-9);
        check("Triển khai sàn: thép gia cường mép lỗ", B(rs.Bars.Count(b => (b.Role ?? "").StartsWith("Gia cường lỗ sàn")) == 3), 1, 0);
        check("Triển khai sàn: mặt bằng ghi LS1", B(msl.Draw(rs, ds)[0].W.ToDxfStringDec().Contains("LS1")), 1, 0);
        // từ mô hình
        var dp = new DetailingProject();
        dp.Beams.Add(new ManualBeam { Name = "D1", B = 0.22, H = 0.45, Spans = "6" });
        dp.Slabs.Add(new ManualSlab { Name = "S1", Lx = 3, Ly = 6, T = 0.12 });
        var log = ModelPenetrations.Apply(p, dp, marks);
        check("Từ mô hình: dầm D1 nhận 3 lỗ", dp.Beams[0].Penetrations.Count, 3, 0);
        check("Từ mô hình: x lỗ đầu = 2,9 m từ nút đầu", dp.Beams[0].Penetrations.Min(q => q.X), 2.9, 1e-9);
        check("Từ mô hình: sàn S1 nhận lỗ LS1 tại (3; 2,6)", B(dp.Slabs[0].Penetrations.Count == 1 && Math.Abs(dp.Slabs[0].Penetrations[0].X - 3) < 1e-9 && Math.Abs(dp.Slabs[0].Penetrations[0].Y - 2.6) < 1e-9), 1, 0);
        ModelPenetrations.Apply(p, dp, marks);
        check("Từ mô hình: chạy lại không nhân đôi", dp.Beams[0].Penetrations.Count + dp.Slabs[0].Penetrations.Count, 4, 0);
        check("Từ mô hình: nhật ký", B(log.Count >= 1), 1, 0);

        // ================= 5. Tường trừ phần rãnh / hộp điện nước =================
        var pw = new Project();
        pw.Stories = new List<Story> { new() { Name = "T1", Elevation = 0, Height = 3.3 }, new() { Name = "T2", Elevation = 3.3, Height = 3.3 } };
        pw.Walls = new List<WallLine> { new() { Id = 1, Story = "T1", X1 = 0, Y1 = 0, X2 = 4, Y2 = 0, Type = pw.WallTypes.FirstOrDefault()?.Name ?? "" } };
        var wl = pw.Walls[0];
        pw.Mep.Runs.Add(new MepRun { Id = 1, System = MepSystemKind.CapNuocLanh, Kind = MepRunKind.Ong, Story = "T1", Size = 25, Path = new() { new MepPt(0.5, 0, 0.3, 1), new MepPt(2.0, 0, 0.3, 1), new MepPt(2.0, 0, 1.0, 1) } });
        pw.Mep.Runs.Add(new MepRun { Id = 2, System = MepSystemKind.ThoatNuocThai, Kind = MepRunKind.Ong, Story = "T1", Size = 110, Path = new() { new MepPt(0.5, 0, 0.3, 1), new MepPt(3.0, 0, 0.3, 1) } });   // thoát nước: không đục tường
        var sock = new MepDevice { Id = 1, Family = "Ổ cắm đơn", Story = "T1", WallId = 1, Offset = 1, X = 1, Y = 0, Height = 0.4 };
        pw.Mep.Devices.Add(sock);
        var chases = WallChases.Compute(pw);
        double core = MepRouting.WallInfo(pw, wl).Core;
        double vExp = 2.2 * 0.045 * Math.Min(0.035, core);
        var fam = pw.Mep.FamilyOf(sock);
        double vBox = fam.W * fam.H * Math.Min(fam.D, core);
        check("Rãnh tường: chỉ ống cấp (thoát nước đi dưới sàn không đục)", chases.Count(c => c.Kind == "Rãnh"), 1, 0);
        check("Rãnh tường: thể tích = 2,2 × (Ø+20) × (Ø+10)", chases.First(c => c.Kind == "Rãnh").Volume, vExp, 1e-12);
        check("Hộp ổ cắm âm tường: thể tích = W × H × min(D; lõi)", chases.First(c => c.Kind == "Hộp").Volume, vBox, 1e-12);
        check("Trát vá rãnh = L × (rộng + 2 × 50 mm)", chases.First(c => c.Kind == "Rãnh").PatchArea, 2.2 * (0.045 + 0.1), 1e-12);
        var q = QuantityCalculator.Compute(pw, null);
        var mi = q.Masonry.FirstOrDefault(m => m.WallId == 1);
        check("Khối lượng xây: trừ thể tích chiếm chỗ (MepVoid)", mi?.MepVoid ?? -1, vExp + vBox, 1e-9);
        check("Khối lượng: có dòng 'Đục rãnh tường' (m) = 2,2", q.MepWallWorks.Where(x => x.Unit == "m").Sum(x => x.Quantity), 2.2, 1e-9);
        check("Khối lượng: có dòng 'Trát vá rãnh' (m²)", B(q.MepWallWorks.Any(x => x.Kind == "Trát vá" && x.Unit == "m²")), 1, 0);
        check("Khối lượng: đục lỗ hộp âm tường 1 cái", q.MepWallWorks.Where(x => x.Unit == "cái").Sum(x => x.Quantity), 1, 0);
        check("Bảng đục rãnh / trát vá có dữ liệu", B(WallChases.Table(q).Rows.Count >= 4), 1, 0);

        // ================= 6. Mặt cắt: chọn nhịp có đủ sàn; phần nhìn thấy phía sau =================
        var pc = new Project();
        pc.Stories = new List<Story> { new() { Name = "T1", Elevation = 0, Height = 3.3 }, new() { Name = "T2", Elevation = 3.3, Height = 3.3 }, new() { Name = "T3", Elevation = 6.6, Height = 3.3 } };
        if (!pc.SlabSections.Any(x => x.Name == "S120")) pc.SlabSections.Add(new SlabSection { Name = "S120", Thickness = 0.12 });
        pc.FrameSections.Add(new FrameSection { Name = "C22", B = 0.22, H = 0.22 });
        foreach (var (nm, c) in new[] { ("1", 0.0), ("2", 5.0) }) pc.Grids.Add(new GridLine { Name = nm, Dir = GridDir.X, Coord = c });
        foreach (var (nm, c) in new[] { ("A", 0.0), ("B", 4.0), ("C", 8.0), ("D", 12.0) }) pc.Grids.Add(new GridLine { Name = nm, Dir = GridDir.Y, Coord = c });
        int nid = 0, sid = 0;
        foreach (var (st, z) in new[] { ("T2", 3.3), ("T3", 6.6) })
            foreach (var (y0, y1) in new[] { (0.0, 4.0), (4.0, 8.0), (8.0, 12.0) })
            {
                if (st == "T3" && y0 == 4.0) continue;   // ô cầu thang tầng T3
                var ids = new List<int>();
                foreach (var (x, y) in new[] { (0.0, y0), (5.0, y0), (5.0, y1), (0.0, y1) }) { pc.Nodes.Add(new Node { Id = ++nid, X = x, Y = y, Z = z, Story = st }); ids.Add(nid); }
                pc.Slabs.Add(new Slab { Id = ++sid, Label = "S" + sid, Nodes = ids, Section = "S120", Story = st });
            }
        pc.Nodes.Add(new Node { Id = 901, X = 2.5, Y = 3.0, Z = 0, Story = "T1" });
        pc.Nodes.Add(new Node { Id = 902, X = 2.5, Y = 3.0, Z = 3.3, Story = "T2" });
        pc.Frames.Add(new FrameElement { Id = 901, Label = "C9", NodeI = 901, NodeJ = 902, Section = "C22", Kind = FrameKind.Column, Story = "T2" });
        var defs = SectionView.Defaults(pc);
        check("Mặt cắt 1-1 tránh nhịp cầu thang: qua giữa nhịp A–B (y = 2)", defs[0].Y1, 2.0, 1e-9);
        var sv = SectionView.Draw(pc, defs[0]);
        check("Mặt cắt 1-1: thấy sàn cả 2 tầng (T2, T3)", sv.Items.Count(i => i.Category == "Sàn"), 2, 0);
        check("Mặt cắt 1-1: chiều sâu nhìn tới trục B (2 + 0,3)", SectionView.ViewDepth(pc, defs[0]), 2.3, 1e-9);
        check("Mặt cắt 1-1: cột phía sau mặt cắt vẽ hình chiếu", B(sv.Beyond.Any(i => i.Category == "Cột")), 1, 0);
        var mid = new SectionLine { Name = "9-9", X1 = -1.5, Y1 = 6, X2 = 6.5, Y2 = 6 };
        var sv9 = SectionView.Draw(pc, mid);
        check("Mặt cắt qua ô cầu thang: ghi chú không có sàn", B(sv9.Figure.W.ToDxfStringDec().Contains("ô cầu thang")), 1, 0);
        File.WriteAllText(Path.Combine(dir, "MatCat_1_1_NhinThay.dxf"), sv.Figure.W.ToDxfString());

        // ================= 7. Biểu tượng nút "Đặt & xem" =================
        check("Biểu tượng: Đặt đồ đạc / Mặt cắt / Xem mặt cắt có và khác nhau",
            B(UiGlyphs.For("Đặt đồ đạc").Length == 1 && UiGlyphs.For("Mặt cắt").Length == 1 && UiGlyphs.For("Xem mặt cắt").Length == 1
              && UiGlyphs.For("Mặt cắt") != UiGlyphs.For("Xem mặt cắt")), 1, 0);
        check("Biểu tượng cũ không đổi: Lưu / Phân tích", B(UiGlyphs.For("Lưu") == "" && UiGlyphs.For("▶ Phân tích") == ""), 1, 0);

        // ================= 8. Kích thước tạm tường / cột =================
        var pt2 = new Project();
        pt2.Stories = new List<Story> { new() { Name = "T1", Elevation = 0, Height = 3.3 } };
        pt2.Walls = new List<WallLine>
        {
            new() { Id = 1, Story = "T1", X1 = 0, Y1 = 0, X2 = 5, Y2 = 0 },
            new() { Id = 2, Story = "T1", X1 = 0, Y1 = 3, X2 = 5, Y2 = 3 }
        };
        var wd = ElementTempDims.ForWall(pt2, pt2.Walls[0]);
        check("Kích thước tạm tường: 1 kích thước tới tường song song = 3 m", B(wd.Count == 1 && Math.Abs(wd[0].Value - 3) < 1e-9 && wd[0].Target == "Tường"), 1, 0);
        check("Sửa kích thước tạm tường 2500 → tường dời song song", B(ElementTempDims.MoveWall(pt2.Walls[0], wd[0], 2.5) && Math.Abs(pt2.Walls[0].Y1 - 0.5) < 1e-9 && Math.Abs(pt2.Walls[0].Y2 - 0.5) < 1e-9), 1, 0);
        check("Sau khi dời: khoảng cách = 2,5", ElementTempDims.ForWall(pt2, pt2.Walls[0])[0].Value, 2.5, 1e-9);
        pt2.Grids.Add(new GridLine { Name = "2", Dir = GridDir.X, Coord = 5 });
        pt2.Grids.Add(new GridLine { Name = "A", Dir = GridDir.Y, Coord = 0 });
        pt2.FrameSections.Add(new FrameSection { Name = "C22", B = 0.22, H = 0.22 });
        pt2.Nodes.Add(new Node { Id = 1, X = 5.11, Y = 0, Z = 0, Story = "T1" });
        pt2.Nodes.Add(new Node { Id = 2, X = 5.11, Y = 0, Z = 3.3, Story = "T1" });
        var col = new FrameElement { Id = 1, Label = "C1", NodeI = 1, NodeJ = 2, Section = "C22", Kind = FrameKind.Column, Story = "T1" };
        pt2.Frames.Add(col);
        var cd = ElementTempDims.ForColumn(pt2, col);
        var cdx = cd.First(x => x.Axis == "X");
        check("Kích thước tạm cột: tâm cột → trục 2 = 110 mm", cdx.Value, 0.11, 1e-9);
        check("Sửa lệch tâm cột về 0 → tâm cột trên trục", B(ElementTempDims.SetColumnOffset(pt2, col, cdx, 0) && Math.Abs(5.11 + col.ColOffsetX - 5) < 1e-9), 1, 0);
        check("Kích thước tạm cột theo Y (trục A) = 0", cd.First(x => x.Axis == "Y").Value, 0, 1e-9);
    }
}
