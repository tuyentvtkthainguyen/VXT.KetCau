using KetCauVXT.Design;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Mep;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Mặt cắt kiến trúc không thể hiện móng (cắt tại cốt sân) / mặt cắt kết cấu vẽ móng (móng đơn vát, bê tông lót, cổ cột) theo kết quả thiết kế,
/// tô vật liệu đúng loại (BTCT, gạch, vữa trát, đá ốp, AAC, đất…) + chú thích vật liệu, lớp hoàn thiện sàn; rãnh điện nước trên mặt bằng / 3D;
/// lỗ sàn hồ sơ triển khai × cột trên mặt bằng (chọc thủng); lực cắt tại lỗ xuyên dầm nội suy theo vị trí.</summary>
public static class SectionMaterialTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        static double B(bool v) => v ? 1 : 0;
        string dir = Path.Combine(outDir, "TrienKhai", "MatCat");
        Directory.CreateDirectory(dir);

        // ---------------- mô hình 3 tầng: lưới 2 nhịp 4 m × 1 nhịp 5 m ----------------
        var p = ModelBuilder.NewProject("Nhà 3 tầng – mặt cắt vật liệu");
        ModelBuilder.SetGrid(p, new[] { 4.0, 4.0 }, new[] { 5.0 });
        Levels.ApplyWizard(p, new LevelWizardOptions { Floors = 3, FirstFloorHeight = 3.6, FloorHeight = 3.3, FootingTopDepth = 0.9, FoundationDepth = 1.5 });
        WallAssembly.Upgrade(p);
        int nid = p.Walls.Count == 0 ? 1 : p.Walls.Max(w => w.Id) + 1;
        var wStone = new WallLine { Id = nid, Story = "T2", X1 = 0, Y1 = 5, X2 = 8, Y2 = 5, Type = "Tường 220 ốp đá granite mặt ngoài" };
        var wAac = new WallLine { Id = nid + 1, Story = "T2", X1 = 0, Y1 = 0, X2 = 8, Y2 = 0, Type = "Gạch AAC 200 trát 2 mặt" };
        p.Walls.Add(wStone); p.Walls.Add(wAac);
        p.Arch ??= new ArchModel();
        p.Arch.Rooms.Add(new Room { Id = 1, Story = "T2", Name = "WC", Outline = new() { new[] { 0.0, 0.0 }, new[] { 4.0, 0.0 }, new[] { 4.0, 5.0 }, new[] { 0.0, 5.0 } } });

        // ---------------- 1. Mặt cắt KIẾN TRÚC: không móng ----------------
        var sY = new SectionLine { Name = "9-9", X1 = 2.0, Y1 = -1.5, X2 = 2.0, Y2 = 6.5 };   // cắt ngang qua 2 tường (vuông góc)
        var kt = SectionView.Draw(p, sY, 100, null, new SectionOptions { ShowFoundation = false });
        double zSite = -0.45;
        check("KT: cốt sân = nền − 0,45", kt.SiteLevel, zSite, 1e-9);
        check("KT: không có khối móng", kt.Foundations.Count, 0, 0);
        check("KT: không phần tử nào dưới cốt sân", kt.Items.Count(i => i.Z0 < zSite - 1e-6), 0, 0);
        string tKt = kt.Figure.W.ToDxfStringDec();
        check("KT: không ghi cao độ đáy móng −1.500", B(!tKt.Contains("-1.500")), 1, 0);
        check("KT: có ghi CỐT SÂN -0.450", B(tKt.Contains("CỐT SÂN -0.450")), 1, 0);
        check("KT: dải đất tự nhiên (EARTH) dưới cốt sân", B(kt.Figure.W.Hatches().Any(h => h.Pattern == HatchPatterns.Earth)), 1, 0);
        var hk = kt.Figure.W.Hatches();
        check("KT: hatch dưới cốt sân chỉ là đất / đất đắp / cát", B(hk.Where(h => h.Loops[0].Min(q => q.Y) < zSite * 1000 - 1)
            .All(h => h.Pattern is HatchPatterns.Earth or HatchPatterns.ArSand)), 1, 0);

        // ---------------- 2. Vật liệu đúng loại ----------------
        check("Vật liệu: BTCT = ANSI31 + AR-CONC", B(SectionMaterials.Patterns(SectionMaterial.BTCT).Select(x => x.Pattern).SequenceEqual(new[] { HatchPatterns.Ansi31, HatchPatterns.ArConc })), 1, 0);
        check("Vật liệu: tên Gạch đặc → GachDac", B(SectionMaterials.FromName(WallMaterials.SolidBrick) == SectionMaterial.GachDac), 1, 0);
        check("Vật liệu: AAC → GachAAC", B(SectionMaterials.FromName(WallMaterials.Aac) == SectionMaterial.GachAAC), 1, 0);
        check("Vật liệu: Gạch rỗng → GachRong", B(SectionMaterials.FromName(WallMaterials.HollowBrick2) == SectionMaterial.GachRong), 1, 0);
        check("Vật liệu: Vữa → VuaTrat", B(SectionMaterials.FromName(WallMaterials.Mortar) == SectionMaterial.VuaTrat), 1, 0);
        check("Vật liệu: Đá granite → Da", B(SectionMaterials.FromName(WallMaterials.Granite) == SectionMaterial.Da), 1, 0);
        check("Vật liệu: Ceramic → GachMen", B(SectionMaterials.FromName(WallMaterials.Ceramic) == SectionMaterial.GachMen), 1, 0);
        check("Vật liệu: BTCT tên → BTCT", B(SectionMaterials.FromName(WallMaterials.Concrete) == SectionMaterial.BTCT), 1, 0);
        var pats = hk.Select(h => h.Pattern).ToHashSet();
        check("Mặt cắt: có tô BTCT (ANSI31 + AR-CONC)", B(pats.Contains(HatchPatterns.Ansi31) && pats.Contains(HatchPatterns.ArConc)), 1, 0);
        check("Mặt cắt: tường ốp đá – lớp đá (VXT_DA)", B(pats.Contains(SectionMaterials.Stone)), 1, 0);
        check("Mặt cắt: tường AAC – lõi AAC (VXT_AAC)", B(pats.Contains(SectionMaterials.Aac)), 1, 0);
        check("Mặt cắt: vữa trát (VXT_VUA)", B(pats.Contains(SectionMaterials.Mortar)), 1, 0);
        // lớp tường: tường AAC 200 trát 10 + 200 + 10 → 3 dải; dải vữa rộng 10 mm (mặt cắt vuông góc)
        var wallCut = kt.Items.First(i => i.Id == "TUONG-" + wAac.Id);
        double wU0 = wallCut.U0 * 1000, wU1 = wallCut.U1 * 1000;
        var inWall = hk.Where(h => h.Loops[0].Min(q => q.X) >= wU0 - 1e-6 && h.Loops[0].Max(q => q.X) <= wU1 + 1e-6 && h.Loops[0].Min(q => q.Y) >= wallCut.Z0 * 1000 - 1e-6).ToList();
        var vua = inWall.Where(h => h.Pattern == SectionMaterials.Mortar).ToList();
        check("Tường AAC: 2 dải vữa trát", vua.Count, 2, 0);
        check("Tường AAC: dải vữa rộng 10 mm", vua.Count > 0 ? vua[0].Loops[0].Max(q => q.X) - vua[0].Loops[0].Min(q => q.X) : 0, 10, 1e-6);
        var aac = inWall.Where(h => h.Pattern == SectionMaterials.Aac).ToList();
        check("Tường AAC: lõi rộng 200 mm", aac.Count > 0 ? aac[0].Loops[0].Max(q => q.X) - aac[0].Loops[0].Min(q => q.X) : 0, 200, 1e-6);
        check("Tường ốp đá: tổng dải = bề dày tường cắt", (wallCut.U1 - wallCut.U0), ModelSolids.WallThickness(p, wAac), 1e-9);
        // lớp hoàn thiện sàn: phòng WC tầng T2 → chống thấm + vữa lót + gạch lát
        check("Sàn có phòng: lớp gạch lát (VXT_MEN)", B(kt.Materials.Contains(SectionMaterial.GachMen)), 1, 0);
        check("Sàn WC / mái: lớp chống thấm", B(kt.Materials.Contains(SectionMaterial.ChongTham)), 1, 0);
        check("Chú thích vật liệu: có tiêu đề + BTCT", B(tKt.Contains("CHÚ THÍCH VẬT LIỆU") && tKt.Contains("Bê tông cốt thép")), 1, 0);
        check("Chú thích vật liệu: liệt kê Đá ốp", B(tKt.Contains("Đá ốp / đá lát")), 1, 0);
        var noMat = SectionView.Draw(p, sY, 100, null, new SectionOptions { ShowFoundation = false, Materials = false });
        check("Tắt tô vật liệu: không có mẫu VXT_*", B(!noMat.Figure.W.Hatches().Any(h => h.Pattern.StartsWith("VXT_"))), 1, 0);
        kt.Figure.W.Save(Path.Combine(dir, "MatCat_KT_KhongMong.dxf"));

        // ---------------- 3. Mặt cắt KẾT CẤU: có móng ----------------
        var sX = new SectionLine { Name = "8-8", X1 = -1.5, Y1 = 0, X2 = 9.5, Y2 = 0 };   // dọc trục A qua 3 móng
        var baseNodes = p.Frames.Where(f => f.Kind == FrameKind.Column).Select(f => { var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ); return a.Z <= b.Z ? a : b; })
            .GroupBy(n => n.Id).Select(g => g.First()).ToList();
        double zBase = baseNodes.Min(n => n.Z);
        var onA = baseNodes.Where(n => n.Z < zBase + 0.05 && Math.Abs(n.Y) < 1e-6).ToList();
        var designs = onA.Select(n => new FootingDesign { NodeId = n.Id, Name = "M1", L = 1.6, B = 1.4, H = 0.5, Hb = 0.3 }).ToList();
        var kc = SectionView.Draw(p, sX, 100, null, new SectionOptions { ShowFoundation = true, Footings = designs });
        var bodies = kc.Foundations.Where(f => !f.Beyond).Where(f => f.Kind == "Móng đơn").ToList();
        check("KC: số móng bị cắt = số cột trục A", bodies.Count, onA.Count, 0);
        check("KC: 3 móng trục A", onA.Count, 3, 0);
        var fb = bodies.OrderBy(f => f.Points.Min(q => q.U)).FirstOrDefault();
        check("KC: móng rộng theo phương cắt = L 1600", fb == null ? 0 : (fb.Points.Max(q => q.U) - fb.Points.Min(q => q.U)) * 1000, 1600, 1e-6);
        check("KC: đáy móng tại −1,5", fb == null ? 0 : fb.Points.Min(q => q.Z), -1.5, 1e-9);
        check("KC: đỉnh móng = −1,5 + H 0,5", fb == null ? 0 : fb.Points.Max(q => q.Z), -1.0, 1e-9);
        check("KC: móng vát (6 đỉnh)", fb?.Points.Count ?? 0, 6, 0);
        var lean = kc.Foundations.Where(f => !f.Beyond).Where(f => f.Kind == "Bê tông lót").ToList();
        check("KC: bê tông lót mỗi móng", lean.Count, onA.Count, 0);
        check("KC: móng phía sau mặt cắt vẽ hình chiếu (nét mảnh)", B(kc.Foundations.All(f => !f.Beyond || string.IsNullOrEmpty(f.Label))), 1, 0);
        var lb = lean.OrderBy(f => f.Points.Min(q => q.U)).FirstOrDefault();
        check("KC: lót rộng L + 2×100 = 1800", lb == null ? 0 : (lb.Points.Max(q => q.U) - lb.Points.Min(q => q.U)) * 1000, 1800, 1e-6);
        check("KC: lót dày 100", lb == null ? 0 : (lb.Points.Max(q => q.Z) - lb.Points.Min(q => q.Z)) * 1000, 100, 1e-6);
        check("KC: lót tô AR-CONC", B(lb?.Material == SectionMaterial.BeTongLot && SectionMaterials.Patterns(SectionMaterial.BeTongLot)[0].Pattern == HatchPatterns.ArConc), 1, 0);
        bool needNeck = zBase > -1.0 + 1e-6;
        check("KC: cổ cột khi chân cột cao hơn đỉnh móng", kc.Foundations.Count(f => !f.Beyond && f.Kind == "Cổ cột"), needNeck ? onA.Count : 0, 0);
        string tKc = kc.Figure.W.ToDxfStringDec();
        check("KC: ghi đáy móng −1.500", B(tKc.Contains("-1.500")), 1, 0);
        check("KC: nhãn móng M1 1600×1400×500", B(tKc.Contains("M1 1600×1400×500")), 1, 0);
        // móng cọc: đài + 2 cọc cắt ngắn
        var pPile = p.Clone();
        pPile.Foundation.Type = FoundationType.Pile;
        var kcp = SectionView.Draw(pPile, sX, 100, null, new SectionOptions { ShowFoundation = true });
        check("Móng cọc: đài cọc mỗi cột", kcp.Foundations.Where(f => !f.Beyond).Count(f => f.Kind == "Đài cọc"), onA.Count, 0);
        check("Móng cọc: 2 cọc mỗi đài, có nét cắt ngắt", kcp.Foundations.Where(f => !f.Beyond).Count(f => f.Kind == "Cọc" && f.BreakAtBottom), 2 * onA.Count, 0);
        kc.Figure.W.Save(Path.Combine(dir, "MatCat_KC_CoMong.dxf"));
        // mặc định: SectionLine.ShowFoundation = null → có móng; hồ sơ KT mặc định không móng, KC có móng
        var ktSheets = SectionView.Sheets(p, 100);
        var kcSheets = SectionView.Sheets(p, 100, true, designs, "KC");
        check("Tờ KT mặt cắt: không ghi đáy móng", B(ktSheets.Count > 0 && ktSheets.All(i => !i.W.ToDxfStringDec().Contains("ĐÁY MÓNG"))), 1, 0);
        check("Tờ KC mặt cắt: có đáy móng, Key KC:MC:", B(kcSheets.Count > 0 && kcSheets.All(i => i.Key.StartsWith("KC:MC:") && i.W.ToDxfStringDec().Contains("ĐÁY MÓNG"))), 1, 0);
        sY.ShowFoundation = true;
        var forced = SectionView.Sheets(new Project(), 100);
        check("Hồ sơ trống: không tờ mặt cắt", forced.Count, 0, 0);
        string js = System.Text.Json.JsonSerializer.Serialize(sY, Project.JsonOptions);
        var sY2 = System.Text.Json.JsonSerializer.Deserialize<SectionLine>(js, Project.JsonOptions);
        check("Lựa chọn thể hiện móng lưu trong hồ sơ", B(sY2.ShowFoundation == true), 1, 0);

        // ---------------- 4. Rãnh điện nước: mặt bằng + 3D ----------------
        var pm = new Project();
        pm.Stories = new List<Story> { new() { Name = "T1", Elevation = 0, Height = 3.3 }, new() { Name = "T2", Elevation = 3.3, Height = 3.3 } };
        pm.Walls = new List<WallLine>
        {
            new() { Id = 1, Story = "T1", X1 = 0, Y1 = 0, X2 = 4, Y2 = 0, Type = "Tường 220 trát 2 mặt" },
            new() { Id = 2, Story = "T1", X1 = 4, Y1 = 0, X2 = 4, Y2 = 3, Type = "Tường 110 trát 2 mặt" },
            new() { Id = 3, Story = "T1", X1 = 4, Y1 = 3, X2 = 0, Y2 = 3, Type = "Tường 220 trát 2 mặt" },
            new() { Id = 4, Story = "T1", X1 = 0, Y1 = 3, X2 = 0, Y2 = 0, Type = "Tường 220 trát 2 mặt" },
        };
        var s1 = MepRouting.PlaceOnWall(pm, "Ổ cắm đơn", pm.Walls[0], 1.0, 1);
        var s2 = MepRouting.PlaceOnWall(pm, "Ổ cắm đơn", pm.Walls[1], 2.0, 1);
        MepRouting.Connect(pm, s1.Id, s2.Id, MepSystemKind.DienOCam);
        var segs = WallChases.ChaseSegments(pm);
        check("Rãnh: có đoạn chôn tường", B(segs.Count > 0), 1, 0);
        var chs = WallChases.Solids(pm);
        check("Rãnh 3D: mỗi đoạn có chiều dài một khối", B(chs.Count > 0 && chs.Count <= segs.Count), 1, 0);
        check("Rãnh 3D: hạng mục Rãnh tường, tường chủ", B(chs.All(c => c.Category == WallChases.SolidCategory && c.HostId.StartsWith("TUONG-"))), 1, 0);
        check("Rãnh 3D: không va chạm (bỏ qua)", B(ClashDetector.Detect(chs.Concat(ModelSolids.Build(pm)).ToList(), new ClashOptions()).All(c => c.A.Category != WallChases.SolidCategory && c.B.Category != WallChases.SolidCategory)), 1, 0);
        var wp = new DxfWriter(50);
        int nPlan = WallChases.DrawPlan(wp, pm, "T1");
        check("Rãnh mặt bằng: vẽ nét đứt các đoạn", B(nPlan > 0 && nPlan == segs.Count(sg => sg.Run.Story == "T1")), 1, 0);
        check("Rãnh mặt bằng: có nét DASHED", B(wp.ToDxfString().Contains("DASHED")), 1, 0);

        // ---------------- 5. Lỗ sàn hồ sơ triển khai × cột trên mặt bằng ----------------
        var dp = new DetailingProject();
        var ms = new ManualSlab { Name = "S1", T = 0.12 };
        ms.Penetrations.Add(new SlabPenetration { Tag = "LS1", X = 0.3, Y = 0.3, W = 0.3, L = 0.3 });
        ms.Penetrations.Add(new SlabPenetration { Tag = "LS2", X = 1.5, Y = 1.5, W = 0.3, L = 0.3 });
        dp.Slabs.Add(ms);
        dp.Columns.Add(new ManualColumn { Name = "C1", B = 0.22, H = 0.22 });
        dp.Plan.Levels = new List<PlanLevel>
        {
            new() { Name = "Tầng 2", Items = new()
            {
                new PlanItem { Member = "C1", Kind = PlanKinds.Column, X1 = 0, Y1 = 0, X2 = 0, Y2 = 0 },
                new PlanItem { Member = "S1", Kind = PlanKinds.Slab, X1 = 0, Y1 = 0, X2 = 4, Y2 = 4 }
            } }
        };
        check("Khe hở 2 hình chữ nhật: (0,3 − 0,15 − 0,11) theo 2 phương", SlabPunching.Gap(0.3, 0.3, 0.3, 0.3, 0, 0, 0.22, 0.22), Math.Sqrt(2) * 0.04, 1e-9);
        var ev = SlabPunching.Evaluate(dp, ms);
        check("Lỗ sàn: đánh giá 2 lỗ", ev.Count, 2, 0);
        check("LS1 sát cột < 2,5·hs → nguy hiểm", B(ev.First(e => e.Tag == "LS1").Severity == ImpactSeverity.NguyHiem), 1, 0);
        check("LS2 cách xa (≥ 6·hs) → cho phép", B(ev.First(e => e.Tag == "LS2").Severity == ImpactSeverity.ChoPhep), 1, 0);
        var res = dp.RunAll().First(x => x.Member == ms).Result;
        check("Chạy hồ sơ: cảnh báo LS1 chọc thủng", B(res.Messages.Any(m => m.StartsWith("CẢNH BÁO lỗ sàn LS1"))), 1, 0);
        check("Chạy hồ sơ: kiểm tra khoảng cách LS1 không đạt", B(res.Checks.Any(c => c.Name.Contains("LS1") && !c.Ok)), 1, 0);
        check("Chạy hồ sơ: LS2 không cảnh báo", B(!res.Messages.Any(m => m.Contains("LS2") && m.StartsWith("CẢNH BÁO"))), 1, 0);
        var msFree = new ManualSlab { Name = "S9", T = 0.12 };
        msFree.Penetrations.Add(new SlabPenetration { X = 0.3, Y = 0.3, W = 0.3, L = 0.3 });
        check("Ô sàn chưa đặt trên mặt bằng: không đánh giá", SlabPunching.Evaluate(dp, msFree).Count, 0, 0);

        // ---------------- 6. Lực cắt tại lỗ xuyên dầm: nội suy theo vị trí ----------------
        check("V tại x = L/4: Qmax/2", PenetrationRegister.ShearAt(80, 6, 1.5), 40, 1e-9);
        check("V giữa nhịp = 0", PenetrationRegister.ShearAt(80, 6, 3), 0, 1e-9);
        check("V tại gối phải = Qmax", PenetrationRegister.ShearAt(80, 6, 6), 80, 1e-9);
        check("V ngoài nhịp kẹp về gối", PenetrationRegister.ShearAt(80, 6, -1), 80, 1e-9);
        check("V lấy |Qmax|", PenetrationRegister.ShearAt(-60, 4, 1), 30, 1e-9);
        var bd = new BeamLineDesign();
        bd.Spans.Add(new BeamSpanResult { Frame = 1, L = 4 });
        bd.Spans.Add(new BeamSpanResult { Frame = 2, L = 5 });
        check("Đầu nhịp 2 = L1 = 4", PenetrationRegister.SpanStart(bd, 2), 4, 1e-9);
        check("Thanh không thuộc dầm → NaN", B(double.IsNaN(PenetrationRegister.SpanStart(bd, 9))), 1, 0);
    }
}
