using KetCauVXT.Drawing;
using KetCauVXT.Mep;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Điện nước – ĐHKK đợt 4: bám mặt tường khi kéo, dò tuyến ống gió A* tránh cột, cao độ dàn nóng, tải lạnh CLTD rút gọn,
/// chia mạch / cân pha / aptomat tổng + sơ đồ nguyên lý tủ điện, bể nước / bơm, sơ đồ không gian cấp nước, sơ đồ ống đứng thoát nước.</summary>
public static class MepRound4Tests
{
    private static double B(bool b) => b ? 1 : 0;

    private static Project House()
    {
        var p = ModelBuilder.NewProject("Kiểm tra điện nước đợt 4");
        ModelBuilder.SetGrid(p, new[] { 5.0, 4.0 }, new[] { 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        WallAssembly.Upgrade(p);
        p.Walls.Clear();
        p.Mep ??= new MepModel();
        return p;
    }

    private const string Ext = "Gạch đặc 220 trát 2 mặt", Inn = "Gạch đặc 110 trát 2 mặt";

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "MepRound4");
        Directory.CreateDirectory(dir);

        // ================= 1. Bám mặt tường khi kéo =================
        var p = House();
        string s1 = p.Stories.OrderBy(s => s.Elevation).First().Name;
        string s2 = p.Stories.OrderBy(s => s.Elevation).Skip(1).First().Name;
        p.Walls.Add(new WallLine { Id = 201, Story = s1, X1 = 0, Y1 = 0, X2 = 5, Y2 = 0, Type = Ext });
        double t = ModelSolids.WallThickness(p, p.Walls[0]);
        check("Bám tường: tường thử có chiều dày > 0", B(t > 0.05), 1, 0);
        check("Bám tường: mặc định bước 5 cm, vùng bắt 0,3 m", MepWallFaceSnap.Step * 10 + MepWallFaceSnap.Tolerance, 0.5 + 0.3, 1e-12);
        double c = t / 2 + 0.25 / 2;
        var sn = MepWallFaceSnap.Snap(p, s1, 2.03, 0.4, 0.25, 0.9);
        check("Bám tường: đã bám tường 201", B(sn.Snapped && sn.WallId == 201 && sn.Side == 1), 1, 0);
        check("Bám tường: dọc tường 2,03 → bước 5 cm = 2,05", sn.X, 2.05, 1e-9);
        check("Bám tường: tâm cách tim = dày/2 + sâu/2", sn.Y, c, 1e-9);
        check("Bám tường: trục song song tường (0°)", sn.Angle, 0, 1e-9);
        var sm = MepWallFaceSnap.Snap(p, s1, 2.03, -0.4, 0.25, 0.9);
        check("Bám tường mặt kia: y = −(dày/2 + sâu/2), góc 180°", sm.Y * 1000 + sm.Angle, -c * 1000 + 180, 1e-6);
        var se = MepWallFaceSnap.Snap(p, s1, 0.1, 0.3, 0.25, 0.9);
        check("Bám tường: kẹp trong [W/2, L − W/2] → 0,45", se.X, 0.45, 1e-9);
        var sf = MepWallFaceSnap.Snap(p, s1, 2.03, 2.02, 0.25, 0.9);
        check("Ngoài vùng bắt: lưới 5 cm (2,05; 2,00), không bám", B(!sf.Snapped) * 100 + sf.X + sf.Y, 2.05 + 2.0, 1e-9);
        var dl = HvacRouting.AddUnit(p, s1, HvacUnitKind.DanLanh, 3, 3, 12000);
        check("Dàn lạnh mặc định: sâu 0,25, rộng 0,9", dl.D * 10 + dl.W, 2.5 + 0.9, 1e-12);
        MepWallFaceSnap.MoveUnit(p, dl, 2.03, 0.4);
        check("Kéo dàn lạnh: bám mặt tường (x = 2,05; y = dày/2 + 0,125)", dl.X * 1000 + dl.Y, 2050 + c, 1e-6);
        var lamp = MepRouting.PlaceFree(p, "Bình nóng lạnh 30 L", s1, 1, 2);
        var famH = p.Mep.FamilyOf(lamp);
        check("Bình nóng lạnh: họ gắn tường (giữ tường chủ khi kéo)", B(famH.WallHosted), 1, 0);
        var fr = MepRouting.PlaceFree(p, "Thoát sàn (phễu thu)", s1, 1, 2);
        var famF = p.Mep.FamilyOf(fr);
        check("Phễu thu: thiết bị tự do, không trên trần", B(!famF.WallHosted && !famF.Ceiling), 1, 0);
        var rf = MepWallFaceSnap.MoveDevice(p, fr, 3.01, 0.2);
        check("Kéo thiết bị tự do sát tường: bám mặt tường", fr.Y, t / 2 + famF.D / 2, 1e-9);
        check("Kéo thiết bị tự do: x bước 5 cm = 3,00", fr.X, 3.0, 1e-9);
        check("Kéo thiết bị tự do: báo đã bám", B(rf.Snapped), 1, 0);
        p.Mep.Devices.Remove(lamp); p.Mep.Devices.Remove(fr);

        // ================= 2. Dò tuyến A* tránh cột =================
        var q = House();
        var hs = new HvacSettings();
        check("Dò tuyến: mặc định bước 0,1 m, phạt đổi hướng 1, cắt dầm 2, xuyên tường 3",
            hs.RouteStep * 1000 + hs.BendPenalty * 100 + hs.BeamPenalty * 10 + hs.WallPenalty, 100 + 100 + 20 + 3, 1e-9);
        double z = HvacRouting.DuctZ(q, s1, 200);
        var colsAt = q.Frames.Where(f => f.IsVerticalKind).Where(f =>
        {
            var a = q.Node(f.NodeI); var b = q.Node(f.NodeJ);
            return Math.Abs(a.X - 5) < 1e-6 && Math.Abs(a.Y) < 1e-6 && z > Math.Min(a.Z, b.Z) && z < Math.Max(a.Z, b.Z);
        }).ToList();
        check("Dò tuyến: có cột tại (5; 0) cắt cao độ ống", B(colsAt.Count > 0), 1, 0);
        var r1 = HvacGridRouter.Route(q, s1, 2.5, 1, 2.5, 3.5, z, 300, 200);
        check("Dò tuyến trong một ô: thẳng, 0 đổi hướng, dài 2,5 m", r1.Bends * 100 + r1.Length, 2.5, 1e-9);
        var r2 = HvacGridRouter.Route(q, s1, 2.5, 1, 7, 3, z, 300, 200);
        check("Dò tuyến chữ L: 1 lần đổi hướng, dài 4,5 + 2", r2.Bends * 100 + r2.Length, 100 + 6.5, 1e-9);
        check("Dò tuyến chữ L: cắt 1 dầm (trục x = 5)", r2.BeamCrossings, 1, 0);
        var r3 = HvacGridRouter.Route(q, s1, 3, 0.1, 7, 0.1, z, 300, 200);
        check("Dò tuyến qua cột: tìm được", B(r3.Found), 1, 0);
        check("Dò tuyến qua cột: vòng ít đổi hướng nhất (2)", r3.Bends, 2, 0);
        double bc = 0.22, hc = 0.3;
        var secC = q.FrameSections.FirstOrDefault(s => s.Name == colsAt.FirstOrDefault()?.Section);
        if (secC != null) { bc = Math.Min(secC.B, secC.H); hc = Math.Max(secC.B, secC.H); }
        double halfMin = bc / 2 + 0.15;   // tối thiểu: nửa cạnh nhỏ của cột + nửa bề rộng ống
        bool clear = true;
        for (int i = 1; i < r3.Points.Count; i++)
            for (int k = 0; k <= 50; k++)
            {
                double x = r3.Points[i - 1].X + (r3.Points[i].X - r3.Points[i - 1].X) * k / 50.0, y = r3.Points[i - 1].Y + (r3.Points[i].Y - r3.Points[i - 1].Y) * k / 50.0;
                if (Math.Abs(x - 5) < halfMin && Math.Abs(y) < halfMin) clear = false;
            }
        check("Dò tuyến qua cột: không đi qua cột (kể cả nửa bề rộng ống)", B(clear), 1, 0);
        check("Dò tuyến qua cột: dài ≤ 4 + 2·1 m", B(r3.Length <= 6 + 1e-9 && r3.Length > 4), 1, 0);
        check("Dò tuyến qua cột: đầu / cuối đúng điểm", r3.Points[0].X + r3.Points[0].Y + r3.Points[^1].X + r3.Points[^1].Y, 3 + 0.1 + 7 + 0.1, 1e-9);
        check("Dò tuyến qua cột: đỉnh trực giao", B(Enumerable.Range(1, r3.Points.Count - 1).All(i =>
            Math.Abs(r3.Points[i].X - r3.Points[i - 1].X) < 1e-9 || Math.Abs(r3.Points[i].Y - r3.Points[i - 1].Y) < 1e-9)), 1, 0);
        var od = HvacRouting.Orthogonal(3, 0.1, 7, 0.1, z);
        check("So sánh: tuyến thẳng cũ đi xuyên cột", B(od.Count == 2), 1, 0);
        var duct = HvacGridRouter.AddRouted(q, s1, 3, 0.1, 7, 0.1, out var rr);
        check("Thêm ống gió theo A*: tuyến tìm được, đầu cuối giữ nguyên", B(rr.Found && Math.Abs(duct.Path[0].X - 3) < 1e-9 && Math.Abs(duct.Path[^1].X - 7) < 1e-9), 1, 0);
        check("Thêm ống gió theo A*: ≥ 4 đỉnh (vòng tránh cột)", B(duct.Path.Count >= 4), 1, 0);

        // ================= 3. Cao độ dàn nóng =================
        var u = House();
        check("Dàn nóng: mặc định đặt sàn / ban công, giá treo 2,2 m", B(new HvacSettings().OutdoorMount == HvacMount.Floor) * 10 + new HvacSettings().OutdoorBracketHeight, 10 + 2.2, 1e-12);
        check("Dàn nóng đặt sàn: cao độ 0,4 m", HvacRouting.AddUnit(u, s1, HvacUnitKind.DanNong, 1, 1).Height, 0.4, 1e-12);
        u.Mep.Hvac.Settings.OutdoorMount = HvacMount.Bracket;
        check("Dàn nóng giá treo: cao độ 2,2 m", HvacRouting.AddUnit(u, s1, HvacUnitKind.DanNong, 1, 2).Height, 2.2, 1e-12);
        u.Mep.Hvac.Settings.OutdoorBracketHeight = 2.5;
        check("Dàn nóng giá treo 2,5 m", HvacRouting.AddUnit(u, s1, HvacUnitKind.DanNong, 1, 3).Height, 2.5, 1e-12);
        check("Dàn lạnh không đổi: 2,4 m", HvacRouting.AddUnit(u, s1, HvacUnitKind.DanLanh, 1, 4).Height, 2.4, 1e-12);

        // ================= 4. Tải lạnh CLTD rút gọn =================
        var pc = House();
        pc.Walls.Add(new WallLine { Id = 101, Story = s1, X1 = 0, Y1 = 0, X2 = 5, Y2 = 0, Type = Ext, Openings = new List<WallOpening> { new() { Id = 1, Type = "S1 1200x1400", Offset = 2.5 } } });
        pc.Walls.Add(new WallLine { Id = 102, Story = s1, X1 = 5, Y1 = 0, X2 = 5, Y2 = 4.5, Type = Inn });
        pc.Walls.Add(new WallLine { Id = 103, Story = s1, X1 = 5, Y1 = 4.5, X2 = 0, Y2 = 4.5, Type = Inn });
        pc.Walls.Add(new WallLine { Id = 104, Story = s1, X1 = 0, Y1 = 0, X2 = 0, Y2 = 4.5, Type = Ext });
        var room = new Room { Id = 9, Story = s1, Name = "Phòng ngủ", CeilingHeight = 3.0, Outline = new List<double[]> { new[] { 0.0, 0.0 }, new[] { 5.0, 0.0 }, new[] { 5.0, 4.5 }, new[] { 0.0, 4.5 } } };
        pc.Arch.Rooms.Add(room);
        var ci = new CoolingInput { People = 3, Roof = false };
        check("CLTD: mặc định U tường 2,0; U kính 5,8; Δt 9; SC 0,6; CLF 0,7", ci.UWall + ci.UGlass + ci.DeltaT + ci.ShadingCoef + ci.GlassClf, 2 + 5.8 + 9 + 0.6 + 0.7, 1e-12);
        check("CLTD: mặc định người 75 + 55 W, đèn 10, thiết bị 15 W/m², 0,5 lần/h, Δw 0,008, an toàn 1,1",
            ci.PeopleSensible + ci.PeopleLatent + ci.LightingWm2 + ci.EquipmentWm2 + ci.Ach + ci.DeltaW + ci.Safety, 75 + 55 + 10 + 15 + 0.5 + 0.008 + 1.1, 1e-12);
        check("CLTD: hướng Nam = CLTD 9, SHGF 200; hướng Tây = CLTD 16", HvacCoolingDetailed.CltdWall[4] + HvacCoolingDetailed.Shgf[4] + HvacCoolingDetailed.CltdWall[6], 9 + 200 + 16, 0);
        check("Hướng: pháp tuyến (0; −1) = Nam, (−1; 0) = Tây, (0; 1) = Bắc, (1; 0) = Đông",
            HvacCoolingDetailed.OrientIndex(0, -1) * 1000 + HvacCoolingDetailed.OrientIndex(-1, 0) * 100 + HvacCoolingDetailed.OrientIndex(0, 1) * 10 + HvacCoolingDetailed.OrientIndex(1, 0), 4000 + 600 + 0 + 2, 0);
        check("Hướng: lệch Bắc 90° → pháp tuyến (0; −1) thành Đông (180 − 90 = 90°)", HvacCoolingDetailed.OrientIndex(0, -1, 90), 2, 0);
        var cr = HvacCoolingDetailed.Room(pc, room, ci);
        check("CLTD: 2 mặt tường ngoài (Nam, Tây)", B(cr.Walls.Count == 2 && cr.Walls.Any(w => w.Orient == "N") && cr.Walls.Any(w => w.Orient == "T")), 1, 0);
        double ag = 1.2 * 1.4;
        double eW = 2.0 * (5 * 3 - ag) * 9 + 2.0 * (4.5 * 3) * 16;
        double eG = ag * (5.8 * 9 + 200 * 0.6 * 0.7);
        double A = 22.5, ls = 0.5 * A * 3 * 1000 / 3600;
        double eTot = (eW + eG + 0 + 3 * 75 + 3 * 55 + 10 * A + 15 * A + 1.23 * ls * 9 + 3010 * ls * 0.008) * 1.1;
        check("CLTD: tường = Σ U·A đặc·CLTD", cr.QWall, eW, 1e-9);
        check("CLTD: kính = A·(U·Δt + SHGF·SC·CLF)", cr.QGlass, eG, 1e-9);
        check("CLTD: không mái", cr.QRoof, 0, 0);
        check("CLTD: gió lọt hiện = 1,23·Q(l/s)·Δt", cr.QInfS, 1.23 * ls * 9, 1e-9);
        check("CLTD: tổng (× 1,1)", cr.TotalW, eTot, 1e-9);
        check("CLTD: BTU/h = W / 0,293", cr.Btu, eTot / 1000 / HvacLoad.BtuToKw, 1e-6);
        var crR = HvacCoolingDetailed.Room(pc, room, new CoolingInput { People = 3, Roof = true });
        check("CLTD: có mái → + U·A·CLTDmái (× 1,1)", crR.TotalW - cr.TotalW, 1.8 * A * 22 * 1.1, 1e-9);
        check("CLTD: so ước lượng nhanh = HvacLoad.Load", cr.QuickBtu, HvacLoad.Load(A, 3.0, true, 3), 1e-9);
        double quickLoad = HvacLoad.RoomLoad(pc, room, 3).LoadBtu;
        check("Phương pháp mặc định: ước lượng nhanh", quickLoad, HvacLoad.Load(A, 3.0, true, 3), 1e-9);
        pc.Mep.Hvac.Settings.CoolingMethod = HvacMount.DetailMethod;
        var rlD = HvacLoad.RoomLoad(pc, room, 3);
        var crAuto = HvacCoolingDetailed.Room(pc, room, new CoolingInput { People = 3 });
        check("Phương pháp chi tiết: RoomLoad dùng CLTD", rlD.LoadBtu, crAuto.Btu, 1e-9);
        check("Phương pháp chi tiết: ghi tên phương pháp", B(rlD.Method == HvacCoolingDetailed.MethodText), 1, 0);
        var ct = HvacCoolingDetailed.Table(pc);
        check("Bảng tải lạnh chi tiết: 1 phòng, 14 cột", ct.Rows.Count * 100 + ct.Headers.Length, 114, 0);

        // ================= 5. Tủ điện =================
        var pe = House();
        for (int i = 0; i < 12; i++) MepRouting.PlaceFree(pe, "Đèn ốp trần", s1, 0.5 + 0.3 * i, 1);
        for (int i = 0; i < 10; i++) MepRouting.PlaceFree(pe, "Ổ cắm đôi", s1, 0.5 + 0.3 * i, 2);
        MepRouting.PlaceFree(pe, "Bình nóng lạnh 30 L", s1, 4, 4);
        HvacRouting.AddUnit(pe, s1, HvacUnitKind.DanLanh, 1, 4, 12000);
        HvacRouting.AddUnit(pe, s1, HvacUnitKind.DanLanh, 2, 4, 12000);
        var po = new PanelOptions();
        check("Tủ điện: mặc định ≤ 10 đèn, ≤ 8 ổ cắm / mạch", po.MaxLightingPoints * 10 + po.MaxSocketPoints, 108, 0);
        check("Tủ điện: mặc định Kc 0,9 / 0,5 / 0,8 / 0,7; cosφ 0,85; 3 pha khi > 12 kW",
            po.KcLighting + po.KcSocket + po.KcAc + po.KcHeater + po.CosPhi + po.ThreePhaseAboveKw, 0.9 + 0.5 + 0.8 + 0.7 + 0.85 + 12, 1e-12);
        var lib = pe.Mep.Library;
        double pLamp = lib.Find("Đèn ốp trần").Power, pSock = lib.Find("Ổ cắm đôi").Power, pHeat = lib.Find("Bình nóng lạnh 30 L").Power;
        check("Họ thiết bị: đèn 24 W, ổ đôi 600 W, bình 2500 W", pLamp + pSock + pHeat, 24 + 600 + 2500, 0);
        var pr = ElectricalPanels.Build(pe);
        check("Chia mạch: 12 đèn → 2 mạch, 10 ổ → 2 mạch, 1 bình, 2 máy lạnh = 7 mạch", pr.Circuits.Count, 7, 0);
        check("Chia mạch: số điểm 10, 2, 8, 2, 1, 1, 1", pr.Circuits.Aggregate(0.0, (a, x) => a * 100 + x.Points), ((((((10.0 * 100 + 2) * 100 + 8) * 100 + 2) * 100 + 1) * 100 + 1) * 100 + 1), 0);
        check("Chia mạch: nhóm C1 chiếu sáng, C3 ổ cắm, C5 bình, C6 điều hoà",
            B(pr.Circuits[0].Group == ElectricalPanels.Lighting && pr.Circuits[2].Group == ElectricalPanels.Socket && pr.Circuits[4].Group == ElectricalPanels.Heater && pr.Circuits[5].Group == ElectricalPanels.Ac), 1, 0);
        check("Mạch C1: 10 × 24 = 240 W → aptomat tối thiểu 10 A, dây 1,5", pr.Circuits[0].PowerW * 1000 + pr.Circuits[0].BreakerA * 10 + pr.Circuits[0].WireMm2, 240000 + 100 + 1.5, 1e-9);
        double i3 = 8 * 600 / (220 * 0.85);
        check("Mạch C3: 8 ổ đôi 4800 W, I = 25,7 A → 32 A + RCD, dây 4 mm²", pr.Circuits[2].CurrentA * 0 + pr.Circuits[2].BreakerA * 10 + pr.Circuits[2].WireMm2, 320 + 4, 1e-9);
        check("Mạch C3: I = P/(220·0,85)", pr.Circuits[2].CurrentA, i3, 1e-9);
        check("Mạch C3: có RCD 30 mA", B(pr.Circuits[2].Rcd && pr.Circuits[2].BreakerText == "MCB 1P 32A + RCD 30mA"), 1, 0);
        check("Mạch C4: 1200 W → tối thiểu 16 A, dây 2,5", pr.Circuits[3].BreakerA * 10 + pr.Circuits[3].WireMm2, 160 + 2.5, 1e-9);
        check("Mạch bình nóng lạnh: 2500 W → 16 A, dây 2,5", pr.Circuits[4].BreakerA * 10 + pr.Circuits[4].WireMm2, 160 + 2.5, 1e-9);
        var acp = HvacLoad.Power(12000);
        check("Mạch máy lạnh: theo HvacLoad.Power (aptomat, dây)", pr.Circuits[5].BreakerA * 10 + pr.Circuits[5].WireMm2, acp.BreakerA * 10 + acp.WireMm2, 1e-9);
        double inst = 12 * 24 + 10 * 600 + 2500 + 2 * acp.PowerW;
        double dem = 0.9 * 12 * 24 + 0.5 * 10 * 600 + 0.7 * 2500 + 0.8 * 2 * acp.PowerW;
        check("Tủ: P đặt", pr.InstalledW, inst, 1e-9);
        check("Tủ: P tính toán Σ Kc·P", pr.DemandW, dem, 1e-9);
        check("Tủ: Ptt < 12 kW → 1 pha", B(!pr.ThreePhase), 1, 0);
        check("Tủ 1 pha: Itt = Ptt/(220·0,85)", pr.DesignA, dem / 187, 1e-9);
        check("Tủ 1 pha: Itt 36,8 A → aptomat tổng 40 A, cáp 4 mm² (Iz 42 A)", pr.MainBreakerA * 10 + pr.MainCableMm2, 400 + 4, 1e-9);
        check("Tủ 1 pha: chữ aptomat tổng", B(pr.MainBreakerText == "MCB 2P 40A"), 1, 0);
        var p3 = ElectricalPanels.Build(pe, new PanelOptions { Phases = PanelOptions.Three });
        check("Tủ 3 pha: tổng tải 3 pha = Ptt", p3.PhaseW.Sum(), dem, 1e-9);
        check("Tủ 3 pha: pha nặng nhất = mạch ổ cắm 0,5·4800", p3.PhaseW.Max(), 2400, 1e-9);
        check("Tủ 3 pha: lệch pha < 10 %", B(p3.UnbalancePct < 10), 1, 0);
        double avg = dem / 3;
        check("Tủ 3 pha: lệch pha = max|Pi − TB| / TB", p3.UnbalancePct, (2400 - avg) / avg * 100, 1e-9);
        check("Tủ 3 pha: mọi mạch có pha L1–L3", B(p3.Circuits.All(x => x.Phase is "L1" or "L2" or "L3")), 1, 0);
        check("Tủ 3 pha: aptomat tổng ≥ mạch lớn nhất 32 A", p3.MainBreakerA, 32, 0);
        check("Tủ 3 pha: chữ MCCB 4P", B(p3.MainBreakerText.StartsWith("MCCB 4P")), 1, 0);
        check("Tủ không đổi mô hình: thiết bị chưa gán mạch", B(pe.Mep.Devices.All(d => string.IsNullOrEmpty(d.Circuit))), 1, 0);
        int nAp = ElectricalPanels.Apply(pe, pr);
        check("Gán mạch: 23 thiết bị điện", nAp, 23, 0);
        var dg = ElectricalPanels.Diagram(pr);
        string dTxt = dg.W.ToDxfStringDec();
        check("Sơ đồ nguyên lý: tiêu đề", B(dTxt.Contains(ElectricalPanels.DiagramTitle)), 1, 0);
        check("Sơ đồ nguyên lý: aptomat tổng MCB 2P 40A", B(dTxt.Contains("MCB 2P 40A")), 1, 0);
        check("Sơ đồ nguyên lý: aptomat nhánh 32A + RCD", B(dTxt.Contains("MCB 1P 32A + RCD 30mA")), 1, 0);
        check("Sơ đồ nguyên lý: nhãn mạch C1 … C7", B(Enumerable.Range(1, 7).All(i => dTxt.Contains("C" + i))), 1, 0);
        check("Sơ đồ nguyên lý: dây Cu/PVC 2x4+E4", B(dTxt.Contains("Cu/PVC 2x4+E4")), 1, 0);
        dg.W.Save(Path.Combine(dir, "SoDoNguyenLy_TuDien.dxf"));
        var tb = ElectricalPanels.Table(pr);
        check("Bảng tủ điện: 7 mạch + 2 dòng tổng", tb.Rows.Count, 9, 0);
        check("Bảng tủ điện: 2 dòng in đậm", tb.TotalRows.Count, 2, 0);

        // ================= 6. Bể nước, bơm =================
        var wo = new WaterTankOptions();
        check("Bể nước: mặc định 150 l/người, bể mái 1 ngày, bể ngầm 2 ngày, bơm 4 h, Kngày 1,3",
            wo.LitersPerPerson + wo.RoofDays + wo.GroundDays + wo.PumpHours + wo.KDay, 150 + 1 + 2 + 4 + 1.3, 1e-12);
        check("Bể nước: mặc định đặt bể mái 0,5 + cao 1,5 m; bể ngầm −1,5 m; 10 m ống ngang; C 140; ×1,3; 2 m tự do; η 0,6",
            wo.RoofTankStand + wo.RoofTankHeight + wo.GroundTankLevel + wo.HorizontalLength + wo.HazenC + wo.LocalFactor + wo.FreeHead + wo.PumpEfficiency,
            0.5 + 1.5 - 1.5 + 10 + 140 + 1.3 + 2 + 0.6, 1e-12);
        check("V = N·q·ngày/1000: 10 người · 150 l · 2 ngày + 5 m³", WaterSupplyCalc.Volume(10, 150, 2, 5), 8, 1e-12);
        check("Bồn chuẩn: 1,2 m³ → 1500 l; 7,2 m³ → 8000 l", WaterSupplyCalc.StandardTank(1200) * 10000 + WaterSupplyCalc.StandardTank(7200), 1500 * 10000 + 8000, 0);
        check("Hazen-Williams: Q 1 l/s, D 50, L 100 m, C 140 = 0,682 m", WaterSupplyCalc.HazenWilliams(0.001, 0.05, 100, 140), 0.6816342060200625, 1e-9);
        var pw = House();
        pw.Mep.Settings.Water.Occupants = 10;
        var tp = WaterSupplyCalc.Compute(pw);
        check("Bể: Q ngày = 1,5 m³; bể mái 1,5; bể ngầm 3,0", tp.DailyM3 * 100 + tp.RoofM3 * 10 + tp.GroundM3, 150 + 15 + 3, 1e-9);
        check("Bể: bồn 1500 l / 3000 l", tp.RoofTankL * 10000 + tp.GroundTankL, 1500 * 10000 + 3000, 0);
        check("Bơm: Q = 1,5·1,3/4 = 0,4875 m³/h", tp.PumpM3h, 0.4875, 1e-12);
        check("Bơm: ống đẩy PPR Ø20 (v ≤ 1,5 m/s)", tp.PipeDn, 20, 0);
        double top = pw.Stories.Max(s => s.Elevation);
        double hst = top + 0.5 + 1.5 + 1.5;
        check("Bơm: H tĩnh = z mái + 0,5 + 1,5 − (−1,5)", tp.StaticHead, hst, 1e-9);
        double hf = 1.3 * 10.67 * (hst + 10) * Math.Pow(0.4875 / 3600, 1.852) / (Math.Pow(140, 1.852) * Math.Pow(0.0132, 4.87));
        check("Bơm: tổn thất = 1,3·Hazen-Williams(Di 13,2 mm)", tp.FrictionHead, hf, 1e-9);
        check("Bơm: H = Htĩnh + tổn thất + 2", tp.PumpHead, hst + hf + 2, 1e-9);
        check("Bơm: N = ρgQH/η", tp.PowerKw, 9.81 * (0.4875 / 3600) * (hst + hf + 2) / 0.6, 1e-9);

        // ================= 7. Sơ đồ đứng =================
        var pr7 = House();
        foreach (var st in new[] { s1, s2 })
        {
            MepRouting.PlaceFree(pr7, "Bồn cầu", st, 1, 1);
            MepRouting.PlaceFree(pr7, "Lavabo", st, 2, 1);
            MepRouting.PlaceFree(pr7, "Sen tắm", st, 3, 1);
        }
        pr7.Mep.Settings.Water.Occupants = 6;
        var sup = RiserDiagrams.Supply(pr7);
        check("Sơ đồ cấp nước: có hình", B(sup != null), 1, 0);
        string sTxt = sup?.W.ToDxfStringDec() ?? "";
        check("Sơ đồ cấp nước: tiêu đề, bể mái, bể ngầm, bơm", B(sTxt.Contains(RiserDiagrams.SupplyTitle) && sTxt.Contains("BỂ NƯỚC MÁI") && sTxt.Contains("BỂ NGẦM") && sTxt.Contains("BƠM")), 1, 0);
        check("Sơ đồ cấp nước: tên tầng + thiết bị", B(sTxt.Contains(s1) && sTxt.Contains(s2) && sTxt.Contains("Lavabo")), 1, 0);
        double nAll = 2 * (lib.Find("Bồn cầu").SupplyUnits + lib.Find("Lavabo").SupplyUnits + lib.Find("Sen tắm").SupplyUnits);
        check("Sơ đồ cấp nước: Ø trục theo tổng đương lượng", B(sTxt.Contains("Ø" + MepSizing.SupplyPipe(MepSizing.SupplyFlow(nAll)).ToString("0"))), 1, 0);
        sup?.W.Save(Path.Combine(dir, "SoDo_CapNuoc.dxf"));
        var drn = RiserDiagrams.Drain(pr7);
        string dr = drn?.W.ToDxfStringDec() ?? "";
        check("Sơ đồ ống đứng thoát: tiêu đề, ống thoát xí + rửa, bể tự hoại, hố ga",
            B(dr.Contains(RiserDiagrams.DrainTitle) && dr.Contains("ỐNG ĐỨNG THOÁT XÍ") && dr.Contains("ỐNG ĐỨNG THOÁT RỬA") && dr.Contains("BỂ TỰ HOẠI") && dr.Contains("HỐ GA")), 1, 0);
        check("Sơ đồ ống đứng thoát xí: Ø110", B(dr.Contains("Ø110")), 1, 0);
        drn?.W.Save(Path.Combine(dir, "SoDo_OngDungThoat.dxf"));
        var figs = MepDrawings.Figures(pr7);
        check("Bộ bản vẽ điện nước: có 2 sơ đồ đứng", B(figs.Any(f => f.Name == RiserDiagrams.SupplyTitle) && figs.Any(f => f.Name == RiserDiagrams.DrainTitle)), 1, 0);
        check("Bộ bản vẽ: không có tủ điện khi chưa có thiết bị điện", B(!figs.Any(f => f.Name == ElectricalPanels.DiagramTitle)), 1, 0);
        var tabs = MepQuantities.Tables(pr7);
        check("Excel: có bảng bể nước – bơm (khai báo số người)", B(tabs.Any(x => x.Title == "Bể nước và bơm cấp nước")), 1, 0);
        var figE = MepDrawings.Figures(pe);
        check("Bộ bản vẽ (có điện): có sơ đồ nguyên lý tủ điện", B(figE.Any(f => f.Name == ElectricalPanels.DiagramTitle)), 1, 0);
        check("Excel (có điện): có bảng tủ điện", B(MepQuantities.Tables(pe).Any(x => x.Title.StartsWith("Bảng tủ điện"))), 1, 0);
        var sheets = MepRegistration.Sheets(pe, new DisciplineDocOptions());
        check("Hồ sơ DN: sơ đồ tủ điện tỷ lệ 1", sheets.FirstOrDefault(s => s.Title == ElectricalPanels.DiagramTitle)?.Scale ?? -1, 1, 0);
        check("Sơ đồ là hình tỷ lệ 1", B(MepRound4.IsSchematic(RiserDiagrams.SupplyTitle) && !MepRound4.IsSchematic("MẶT BẰNG")), 1, 0);
    }
}
