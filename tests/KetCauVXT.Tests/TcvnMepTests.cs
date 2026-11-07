using KetCauVXT.Mep;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Bảng thông số điện nước theo TCVN / IEC (Mep/MepStandards.cs) và tuyến ống gen đi trong trần / sàn tránh cột – dầm (Mep/SlabConduit.cs).
/// check(tên, giá trị, kỳ vọng, sai số).</summary>
public static class TcvnMepTests
{
    private static double B(bool b) => b ? 1 : 0;

    /// <summary>Khung 6 × 4 m, 3 cao độ (0; 3,3; 6,6): cột C30 ở 4 góc, dầm biên D22x40 + dầm giữa x = 3 (nhịp 4 m) ở 3,3 và 6,6;
    /// sàn T2 (mặt 3,3) và T3 (mặt 6,6) dày slabT; tường 110 trên T2: W1 (0,5;1)→(2,5;1), W2 (3,5;1)→(5,5;1), W3 (0,5;4)→(2,5;4) nằm trên dầm biên.</summary>
    private static Project Frame(double slabT = 0.12)
    {
        var p = new Project();
        p.Stories = new List<Story> { new() { Name = "T1", Elevation = 0, Height = 3.3 }, new() { Name = "T2", Elevation = 3.3, Height = 3.3 }, new() { Name = "T3", Elevation = 6.6, Height = 3.3 } };
        p.FrameSections.Add(new FrameSection { Name = "C30", B = 0.3, H = 0.3 });
        p.FrameSections.Add(new FrameSection { Name = "D22x40", B = 0.22, H = 0.4 });
        p.SlabSections.Add(new SlabSection { Name = "SX", Thickness = slabT });
        var xy = new (double X, double Y)[] { (0, 0), (3, 0), (6, 0), (0, 4), (3, 4), (6, 4) };
        double[] zs = { 0, 3.3, 6.6 };
        string[] st = { "T1", "T2", "T3" };
        int Id(int lvl, int k) => lvl * 10 + k + 1;
        for (int l = 0; l < 3; l++)
            for (int k = 0; k < xy.Length; k++)
                p.Nodes.Add(new Node { Id = Id(l, k), X = xy[k].X, Y = xy[k].Y, Z = zs[l], Story = st[l] });
        int fid = 0;
        int[] corners = { 0, 2, 3, 5 };
        for (int l = 0; l < 2; l++)
            foreach (int k in corners)
                p.Frames.Add(new FrameElement { Id = ++fid, Label = "C" + fid, NodeI = Id(l, k), NodeJ = Id(l + 1, k), Section = "C30", Kind = FrameKind.Column, Story = st[l + 1] });
        var beams = new (int A, int B)[] { (0, 1), (1, 2), (3, 4), (4, 5), (0, 3), (2, 5), (1, 4) };
        for (int l = 1; l < 3; l++)
            foreach (var (a, b) in beams)
                p.Frames.Add(new FrameElement { Id = ++fid, Label = (a, b) == (1, 4) ? $"DG{l}" : "D" + fid, NodeI = Id(l, a), NodeJ = Id(l, b), Section = "D22x40", Kind = FrameKind.Beam, Story = st[l] });
        for (int l = 1; l < 3; l++)
            p.Slabs.Add(new Slab { Id = l, Label = "S" + l, Nodes = new() { Id(l, 0), Id(l, 2), Id(l, 5), Id(l, 3) }, Section = "SX", Story = st[l] });
        p.Walls = new List<WallLine>
        {
            new() { Id = 1, Story = "T2", X1 = 0.5, Y1 = 1, X2 = 2.5, Y2 = 1, Type = "Tường 110 trát 2 mặt" },
            new() { Id = 2, Story = "T2", X1 = 3.5, Y1 = 1, X2 = 5.5, Y2 = 1, Type = "Tường 110 trát 2 mặt" },
            new() { Id = 3, Story = "T2", X1 = 0.5, Y1 = 4, X2 = 2.5, Y2 = 4, Type = "Tường 110 trát 2 mặt" },
        };
        return p;
    }

    /// <summary>Điểm cắt tim dầm giữa x = 3 của các đoạn ngang trong sàn.</summary>
    private static List<double> CrossY(MepRun r)
    {
        var res = new List<double>();
        for (int i = 1; i < r.Path.Count; i++)
        {
            var a = r.Path[i - 1]; var c = r.Path[i];
            if ((a.X - 3) * (c.X - 3) < 0 && Math.Abs(a.Y - c.Y) < 1e-9) res.Add(a.Y);
        }
        return res;
    }

    private static int WallHorizontal(MepRun r)
    {
        int n = 0;
        for (int i = 1; i < r.Path.Count; i++)
            if (r.Path[i].WallId > 0 && MepGeom.Dist2D(r.Path[i - 1].X, r.Path[i - 1].Y, r.Path[i].X, r.Path[i].Y) > 1e-6) n++;
        return n;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ================= 1. Bảng TCVN 4513:1988 – cấp nước =================
        check("TCVN 4513: a(125 l) = 2,15", MepStandards.AlphaFor(125), 2.15, 1e-12);
        check("TCVN 4513: a(150 l) = 2,1", MepStandards.AlphaFor(150), 2.1, 1e-12);
        check("TCVN 4513: a(100 l) = 2,2", MepStandards.AlphaFor(100), 2.2, 1e-12);
        check("TCVN 4513: K(N = 300) = 0,002", MepStandards.KFor(300), 0.002, 1e-12);
        check("TCVN 4513: K(N = 301) = 0,003", MepStandards.KFor(301), 0.003, 1e-12);
        check("TCVN 4513: K(N = 1500) = 0,006", MepStandards.KFor(1500), 0.006, 1e-12);
        check("TCVN 4513: q(N = 1) nhà ở 125 l = 0,2·2,15 + 0,002 (không đổi so với trước)", MepSizing.SupplyFlow(1), 0.432, 1e-12);
        var o150 = new MepStdOptions { WaterNorm = 150 };
        check("TCVN 4513: q(N = 4) nhà ở 150 l = 0,2·2,1·2 + 0,002·4", MepSizing.SupplyFlow(4, o150), 0.2 * 2.1 * 2 + 0.008, 1e-12);
        var oTh = new MepStdOptions { Building = MepBuildingKind.TruongHoc };
        check("TCVN 4513: trường học q = 0,2·1,8·√16 = 1,44", MepSizing.SupplyFlow(16, oTh), 1.44, 1e-12);
        check("TCVN 4513: K theo N chỉ khi N > 300 (N = 400 → K = 0,003)", MepSizing.SupplyFlow(400), 0.2 * 2.15 * 20 + 0.003 * 400, 1e-9);
        check("TCVN 4513: đương lượng lavabo 0,33", MepStandards.Val("cap.n_lavabo"), 0.33, 0);
        check("TCVN 4513: v ≤ 1,5 m/s", MepStandards.Val("cap.v_max"), 1.5, 0);
        check("TCVN 4513: bể – bơm 150 l/người·ngđ = mặc định WaterTankOptions", new WaterTankOptions().LitersPerPerson, MepStandards.Val("cap.q_nguoi"), 0);

        // ================= 2. TCVN 4474:1987 – thoát nước =================
        check("TCVN 4474: Ø50 độ dốc tiêu chuẩn 3,5 %", MepStandards.DrainSlopeStd(50), 0.035, 1e-12);
        check("TCVN 4474: Ø60 (danh nghĩa 50) độ dốc nhỏ nhất 2,5 %", MepStandards.DrainSlopeMin(60), 0.025, 1e-12);
        check("TCVN 4474: Ø110 (danh nghĩa 100) tiêu chuẩn 2 %", MepStandards.DrainSlopeStd(110), 0.02, 1e-12);
        check("TCVN 4474: Ø110 nhỏ nhất 1,2 %", MepStandards.DrainSlopeMin(110), 0.012, 1e-12);
        check("TCVN 4474: Ø160 (danh nghĩa 150) nhỏ nhất 0,7 %", MepStandards.DrainSlopeMin(160), 0.007, 1e-12);
        check("TCVN 4474: ống có xí ≥ Ø110", MepStandards.Val("thoat.d_xi"), 110, 0);
        var ms = new MepSettings();
        check("Độ dốc mặc định chương trình ≥ độ dốc nhỏ nhất TCVN 4474 (Ø50, Ø110)",
            B(ms.DrainSlopeSmall >= MepStandards.DrainSlopeMin(50) && ms.DrainSlopeLarge >= MepStandards.DrainSlopeMin(110)), 1, 0);

        // ================= 3. IEC 60364-5-52 / TCVN 9206 – 9207 – dây dẫn =================
        check("IEC B.52.4 B1: 2,5 mm² → 24 A", MepStandards.Iz(2.5), 24, 1e-12);
        check("IEC B.52.4 B1: 6 mm² → 41 A", MepStandards.Iz(6), 41, 1e-12);
        check("IEC B.52.4 B1: 25 mm² → 101 A (bảng mở rộng)", MepStandards.Iz(25), 101, 1e-12);
        check("IEC B.52.14: 35 °C → 0,94", MepStandards.TempK(35), 0.94, 1e-12);
        check("IEC B.52.14: 40 °C → 0,87", MepStandards.TempK(40), 0.87, 1e-12);
        check("IEC B.52.17: 2 mạch → 0,80", MepStandards.GroupK(2), 0.8, 1e-12);
        check("IEC B.52.17: 3 mạch → 0,70", MepStandards.GroupK(3), 0.7, 1e-12);
        var hot = new MepStdOptions { AmbientC = 35, GroupedCircuits = 2 };
        check("Iz 2,5 mm² ở 35 °C, 2 mạch = 24·0,94·0,8", MepStandards.Iz(2.5, hot), 24 * 0.94 * 0.8, 1e-9);
        check("Dây ổ cắm 3000 W (aptomat 20 A) mặc định → 2,5 mm²", MepSizing.WireFor(3000, MepSystemKind.DienOCam, 220, 0.85, null, 0), 2.5, 0);
        var hot3 = new MepStdOptions { AmbientC = 40, GroupedCircuits = 3 };
        // 20 A / (0,87·0,70 = 0,609): 2,5 → 14,6 A; 4 → 19,5 A; 6 → 25,0 A ≥ 20
        check("Dây ổ cắm 3000 W ở 40 °C, 3 mạch chung ống → 6 mm²", MepSizing.WireFor(3000, MepSystemKind.DienOCam, 220, 0.85, hot3, 0), 6, 0);
        double i1 = MepSizing.Current(1000), sin = Math.Sqrt(1 - 0.85 * 0.85);
        double du15 = 2 * 50 * i1 * (0.0225 / 1.5 * 0.85 + 0.08e-3 * sin) / 220 * 100;
        check("Sụt áp IEC Phụ lục G: 1000 W, 50 m, 1,5 mm²", MepStandards.VoltageDropPct(i1, 50, 1.5), du15, 1e-9);
        check("Sụt áp 1000 W / 50 m / 1,5 mm² > 3 % chiếu sáng", B(du15 > 3), 1, 0);
        check("Chiếu sáng 1000 W, 50 m: sụt áp > 3 % → tăng lên 2,5 mm²", MepSizing.WireFor(1000, MepSystemKind.DienChieuSang, 220, 0.85, null, 50), 2.5, 0);
        check("Chiếu sáng 1000 W, tuyến ngắn → 1,5 mm²", MepSizing.WireFor(1000, MepSystemKind.DienChieuSang, 220, 0.85, null, 5), 1.5, 0);
        check("Sụt áp cho phép: chiếu sáng 3 %, ổ cắm 5 %", MepStandards.VoltageDropLimit(MepSystemKind.DienChieuSang) * 10 + MepStandards.VoltageDropLimit(MepSystemKind.DienOCam), 35, 0);
        check("TCVN 9206: cao độ ổ cắm 0,4 m = họ thư viện", MepStandards.Val("dien.h_ocam"), 0.4, 0);
        check("TCVN 9206: Kc ổ cắm = mặc định tủ điện", new PanelOptions().KcSocket, MepStandards.Val("dien.kc_oc"), 0);
        check("TCVN 9206: Kc chiếu sáng 0,9", new PanelOptions().KcLighting, 0.9, 0);

        // ================= 4. TCVN 5687:2010 – ĐHKK =================
        check("TCVN 5687 Bảng A.1: trong nhà 25 °C", MepStandards.Indoor().T, 25, 0);
        check("TCVN 5687 Bảng A.1: trong nhà 60 %", MepStandards.Indoor().Phi, 60, 0);
        check("TCVN 5687 Phụ lục B: Hà Nội 37,8 °C", MepStandards.Outdoor().T, 37.8, 1e-12);
        check("TCVN 5687 Phụ lục B: TP.HCM 36,8 °C / 56 %", MepStandards.Outdoor(new MepStdOptions { Climate = MepStandards.Hcm }).T * 100 + MepStandards.Outdoor(new MepStdOptions { Climate = MepStandards.Hcm }).Phi, 3736, 1e-9);
        check("TCVN 5687: gió tươi 4 người = 100 m³/h", MepStandards.FreshAir(4), 100, 1e-12);
        check("TCVN 5687: vận tốc ống chính = mặc định HvacSettings", new HvacSettings().VelocityMain, MepStandards.Val("dhkk.v_chinh"), 0);

        // ================= 5. Tuỳ chỉnh / nhãn nguồn =================
        var oc = new MepStdOptions();
        oc.Overrides.Add(new MepStdOverride { Key = "cap.v_max", Value = 1.2 });
        check("Tuỳ chỉnh: v max = 1,2", MepStandards.Get(oc, "cap.v_max"), 1.2, 0);
        check("Tuỳ chỉnh: ghi rõ 'tuỳ chỉnh' + nguồn", B(MepStandards.Cite(oc, "cap.v_max").Contains("tuỳ chỉnh") && MepStandards.Cite(oc, "cap.v_max").Contains("TCVN 4513")), 1, 0);
        check("Nhãn nguồn: 'Theo TCVN 5687:2010 Phụ lục B'", B(MepStandards.Item("dhkk.t_hn").Label == "Theo TCVN 5687:2010 Phụ lục B"), 1, 0);
        check("Mọi thông số có nguồn + đơn vị khai báo", MepStandards.Items.Count(i => string.IsNullOrWhiteSpace(i.Source)), 0, 0);
        check("Khoá thông số không trùng", MepStandards.Items.Count - MepStandards.Items.Select(i => i.Key).Distinct().Count(), 0, 0);
        var tab = MepStandards.Table(oc);
        check("Bảng thông số: mỗi thông số một dòng", tab.Rows.Count, MepStandards.Items.Count, 0);
        check("Thuyết minh tóm tắt có nguồn TCVN 4513 / IEC / TCVN 5687", B(string.Join("\n", MepStandards.Summary(oc)).Contains("TCVN 4513") && string.Join("\n", MepStandards.Summary(oc)).Contains("IEC 60364-5-52")
            && string.Join("\n", MepStandards.Summary(oc)).Contains("TCVN 5687")), 1, 0);
        var oSet = new MepSettings();
        check("Mặc định tuyến ống gen: trần / sàn", B(oSet.ConduitRoute == ConduitRouteMode.TranSan), 1, 0);
        check("Điều kiện qua dầm: sàn 120, Ø25 → được", B(SlabConduit.CrossAllowed(null, 0.12, 25)), 1, 0);
        check("Điều kiện qua dầm: sàn 90 → không", B(SlabConduit.CrossAllowed(null, 0.09, 20)), 0, 0);
        check("Điều kiện qua dầm: Ø32 → không", B(SlabConduit.CrossAllowed(null, 0.12, 32)), 0, 0);
        check("Vị trí qua dầm: t = 0,25 (sát gối) → không; t = 0,5 → được", B(!SlabConduit.InMiddleThird(0.25) && SlabConduit.InMiddleThird(0.5)), 1, 0);

        // ================= 6. Ống gen ổ cắm trong sàn dưới chân, qua dầm giữa nhịp =================
        var p = Frame();
        var s1 = MepRouting.PlaceOnWall(p, "Ổ cắm đơn", p.Walls[0], 1.0, 1);
        var s2 = MepRouting.PlaceOnWall(p, "Ổ cắm đơn", p.Walls[1], 1.0, 1);
        var r1 = MepRouting.Connect(p, s1.Id, s2.Id, MepSystemKind.DienOCam);
        double zf = 3.3 - 0.06;
        check("Sàn: tuyến ổ cắm đi được trong sàn (không ghi chú lỗi)", B(r1.RouteNote == null && r1.Path.Count >= 4), 1, 0);
        check("Sàn: đoạn ngang ở giữa chiều dày sàn (3,3 − 0,12/2)", r1.Path.Where((q, i) => i > 0 && Math.Abs(q.Z - r1.Path[i - 1].Z) < 1e-9 && MepGeom.Dist2D(q.X, q.Y, r1.Path[i - 1].X, r1.Path[i - 1].Y) > 1e-6).Select(q => q.Z).DefaultIfEmpty(-1).Max(), zf, 1e-9);
        check("Sàn: không có đoạn ngang trong tường", WallHorizontal(r1), 0, 0);
        var cy = CrossY(r1);
        check("Sàn: qua dầm giữa đúng 1 lần", cy.Count, 1, 0);
        check("Sàn: điểm qua dầm trong 1/3 giữa nhịp (4/3 ≤ y ≤ 8/3) – không qua thẳng tại y = 1 (sát gối)", B(cy.Count == 1 && cy[0] >= 4.0 / 3 - 1e-9 && cy[0] <= 8.0 / 3 + 1e-9), 1, 0);
        check("Sàn: đoạn đứng trong tường W1 tới mặt sàn 3,3", B(r1.Path.Any(q => q.WallId == 1 && Math.Abs(q.Z - 3.3) < 1e-9)), 1, 0);
        var items = StructuralImpact.Scan(p);
        check("Kiểm tra: qua dầm trong sàn đạt → Gợi ý ONG_GEN_QUA_DAM", (int)(items.FirstOrDefault(i => i.RunId == r1.Id && i.Code == "ONG_GEN_QUA_DAM")?.Severity ?? (ImpactSeverity)(-1)), (int)ImpactSeverity.GoiY, 0);
        check("Kiểm tra: không báo xuyên dầm / xuyên cột cho tuyến trong sàn", items.Count(i => i.RunId == r1.Id && (i.Code == "XUYEN_DAM" || i.Code == "XUYEN_COT")), 0, 0);
        check("Kiểm tra: Ø16 ≤ 1/3 sàn 120 → không SAN_MONG", items.Count(i => i.RunId == r1.Id && i.Code == "SAN_MONG"), 0, 0);
        check("Kiểm tra: không lỗ sàn do ống gen trong sàn", items.Count(i => i.RunId == r1.Id && i.Code == "LO_SAN"), 0, 0);
        double chaseSlab = WallChases.Segments(r1.Path).Sum(s => s.L);

        // ================= 7. So sánh với tuyến trong tường (2 ổ cắm cùng tường W1, cách 0,9 m): rãnh tường ít hơn =================
        var s4 = MepRouting.PlaceOnWall(p, "Ổ cắm đơn", p.Walls[0], 1.9, 1);
        var rs = MepRouting.Connect(p, s1.Id, s4.Id, MepSystemKind.DienOCam);
        double chaseS = WallChases.Segments(rs.Path).Sum(s => s.L);
        p.Mep.Settings.ConduitRoute = ConduitRouteMode.TrongTuong;
        var rw = MepRouting.Connect(p, s1.Id, s4.Id, MepSystemKind.DienOCam);
        double chaseWall = WallChases.Segments(rw.Path).Sum(s => s.L);
        check("Trong tường (cách cũ): còn đoạn ngang trong tường", B(WallHorizontal(rw) > 0), 1, 0);
        check("Trong tường: rãnh 0,1 + 0,9 + 0,1 m", chaseWall, 1.1, 1e-6);
        check("Trần / sàn: rãnh tường chỉ còn 2 đoạn đứng 0,4 m (ít hơn trong tường)", chaseS, 0.8, 1e-6);
        check("Trần / sàn: tuyến ổ cắm – ổ cắm có rãnh tường = 0,8 m", chaseSlab, 0.8, 1e-6);
        p.Mep.Runs.Remove(rw); p.Mep.Runs.Remove(rs);
        p.Mep.Settings.ConduitRoute = ConduitRouteMode.TranSan;

        // ================= 8. Chiếu sáng trong sàn trên (trần) =================
        var sw = MepRouting.PlaceOnWall(p, "Công tắc 1 hạt", p.Walls[0], 0.5, 1);
        var lt = MepRouting.PlaceFree(p, "Đèn ốp trần", "T2", 4.5, 2.0);
        var rl = MepRouting.Connect(p, sw.Id, lt.Id, MepSystemKind.DienChieuSang);
        double zc = 6.6 - 0.06;
        check("Trần: tuyến chiếu sáng đi trong sàn trên (6,6 − 0,06)", B(rl.RouteNote == null && rl.Path.Any(q => Math.Abs(q.Z - zc) < 1e-9)), 1, 0);
        check("Trần: rãnh đứng công tắc lên tới đáy sàn 6,48", B(rl.Path.Any(q => q.WallId == 1 && Math.Abs(q.Z - 6.48) < 1e-9)), 1, 0);
        check("Trần: không đoạn ngang trong tường", WallHorizontal(rl), 0, 0);
        var cyl = CrossY(rl);
        check("Trần: qua dầm giữa nhịp trong 1/3 giữa", B(cyl.Count == 1 && cyl[0] >= 4.0 / 3 - 1e-9 && cyl[0] <= 8.0 / 3 + 1e-9), 1, 0);
        check("Trần: không xuyên dầm / cột", StructuralImpact.Scan(p).Count(i => i.RunId == rl.Id && (i.Code == "XUYEN_DAM" || i.Code == "XUYEN_COT")), 0, 0);

        // ================= 9. Thiết bị trên tường đặt trên dầm: chuyển qua lớp cán nền =================
        var s3 = MepRouting.PlaceOnWall(p, "Ổ cắm đơn", p.Walls[2], 1.0, -1);
        var r3 = MepRouting.Connect(p, s3.Id, s1.Id, MepSystemKind.DienOCam);
        check("Tường trên dầm: tuyến vẫn đi trong sàn", B(r3.RouteNote == null), 1, 0);
        check("Tường trên dầm: đi đứng tới lớp cán nền 3,3 + 0,03 rồi ra khỏi dầm", B(r3.Path.Count(q => Math.Abs(q.Z - 3.33) < 1e-9) >= 2), 1, 0);
        var it3 = StructuralImpact.Scan(p);
        check("Tường trên dầm: không ống đứng xuyên dầm", it3.Count(i => i.RunId == r3.Id && i.Code == "XUYEN_DAM"), 0, 0);
        check("Tường trên dầm: không xuyên cột", it3.Count(i => i.RunId == r3.Id && i.Code == "XUYEN_COT"), 0, 0);

        // ================= 10. Sàn mỏng / ống lớn: không được qua dầm → cảnh báo, đi theo tường =================
        var pt = Frame(0.08);
        var t1 = MepRouting.PlaceOnWall(pt, "Ổ cắm đơn", pt.Walls[0], 1.0, 1);
        var t2 = MepRouting.PlaceOnWall(pt, "Ổ cắm đơn", pt.Walls[1], 1.0, 1);
        var rt = MepRouting.Connect(pt, t1.Id, t2.Id, MepSystemKind.DienOCam);
        check("Sàn 80 mm: không qua dầm được → ghi chú", B(!string.IsNullOrEmpty(rt.RouteNote) && rt.RouteNote.Contains("80 mm")), 1, 0);
        check("Sàn 80 mm: cảnh báo ONG_GEN_TRAN_SAN", StructuralImpact.Scan(pt).Count(i => i.RunId == rt.Id && i.Code == "ONG_GEN_TRAN_SAN"), 1, 0);
        check("Sàn 80 mm: tuyến thay thế không đi trong sàn qua dầm giữa", B(rt.Path.All(q => q.Z > 3.3 - 1e-9)), 1, 0);
        var big = MepRouting.Connect(p, s1.Id, s2.Id, MepSystemKind.DienOCam);
        big.ConduitSize = 32; MepRouting.Route(p, big);
        check("Ống gen Ø32: không được qua dầm trong sàn → ghi chú Ø32", B(!string.IsNullOrEmpty(big.RouteNote) && big.RouteNote.Contains("Ø32")), 1, 0);
        // Ø32 trong sàn 80 mm: kiểm tra SAN_MONG trên đoạn đặt tay trong sàn
        var thin = new MepRun { Id = 99, System = MepSystemKind.DienOCam, Kind = MepRunKind.Day, Story = "T2", ConduitSize = 32, Size = 2.5, Conductors = 3,
            Path = new() { new MepPt(1, 2, 3.26), new MepPt(2, 2, 3.26) } };
        pt.Mep.Runs.Add(thin);
        check("Ø32 trong sàn 80 mm > 1/3 h → SAN_MONG", StructuralImpact.Scan(pt).Count(i => i.RunId == 99 && i.Code == "SAN_MONG"), 1, 0);
        var near = new MepRun { Id = 98, System = MepSystemKind.DienOCam, Kind = MepRunKind.Day, Story = "T2", ConduitSize = 20, Size = 2.5, Conductors = 3,
            Path = new() { new MepPt(2, 0.5, 3.24), new MepPt(4, 0.5, 3.24) } };
        p.Mep.Runs.Add(near);
        check("Ống gen qua dầm sát gối (cách 0,5 m < L/3) → Cảnh báo ONG_GEN_QUA_DAM",
            (int)(StructuralImpact.Scan(p).FirstOrDefault(i => i.RunId == 98 && i.Code == "ONG_GEN_QUA_DAM")?.Severity ?? (ImpactSeverity)(-1)), (int)ImpactSeverity.CanhBao, 0);
        var wallH = new MepRun { Id = 97, System = MepSystemKind.DienOCam, Kind = MepRunKind.Day, Story = "T2", ConduitSize = 16, Size = 2.5, Conductors = 3,
            Path = new() { new MepPt(0.5, 1, 3.6, 1), new MepPt(2.5, 1, 3.6, 1) } };
        p.Mep.Runs.Add(wallH);
        check("Chế độ trần / sàn: đoạn ngang 2 m trong tường → ONG_GEN_NGANG_TUONG", StructuralImpact.Scan(p).Count(i => i.RunId == 97 && i.Code == "ONG_GEN_NGANG_TUONG"), 1, 0);

        p.Mep.Settings.Water.Occupants = 4;
        check("Excel điện nước: có bảng tính → kèm bảng thông số theo tiêu chuẩn", B(MepQuantities.Tables(p).Any(t => t.Title == "Thông số điện nước – ĐHKK theo tiêu chuẩn")), 1, 0);

        // ================= 11. Lưu / mở giữ lựa chọn =================
        var js = System.Text.Json.JsonSerializer.Serialize(p.Mep.Settings);
        var back = System.Text.Json.JsonSerializer.Deserialize<MepSettings>(js);
        check("Lưu / mở: giữ tuyến ống gen + tuỳ chỉnh", B(back.ConduitRoute == ConduitRouteMode.TranSan && back.Standards != null), 1, 0);
    }
}
