using KetCauVXT.Export;
using KetCauVXT.Mep;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>ĐHKK đợt 3: bắt / quét chọn và sửa từng đối tượng (dời, xoá, đổi cỡ giữ cao độ đỉnh, đổi lưu lượng, công suất),
/// hạ ống gió qua dầm bằng 2 cút 45° mỗi phía (và cảnh báo khi trần không đủ), kiểu IFC2X3 ĐHKK + IfcRelDefinesByType,
/// tải lạnh phòng → chọn máy, đặt dàn lạnh / dàn nóng theo tường, cỡ ống đồng, aptomat / dây máy lạnh.</summary>
public static class HvacRound3Tests
{
    private static double B(bool b) => b ? 1 : 0;
    private static bool Is((HvacElemKind Kind, int Id)? v, HvacElemKind k, int id) => v.HasValue && v.Value.Kind == k && v.Value.Id == id;

    private static Project House()
    {
        var p = ModelBuilder.NewProject("Kiểm tra ĐHKK đợt 3");
        ModelBuilder.SetGrid(p, new[] { 5.0, 4.0 }, new[] { 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        WallAssembly.Upgrade(p);
        return p;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ================= 1. Bắt đối tượng, quét chọn, sửa =================
        var q = House();
        string s1 = q.Stories.OrderBy(s => s.Elevation).First().Name;
        // mặc định VelocityMain = 6 m/s → Q = 1800 m³/h chọn 600 × 150 (xem RemainingRound2Tests)
        check("ĐHKK3: mặc định v ống chính = 6 m/s", new HvacSettings().VelocityMain, 6, 0);
        check("ĐHKK3: mặc định tự hạ ống qua dầm, khe 50 mm, thông thủy 2,4 m",
            B(new HvacSettings().AutoDropUnderBeams) * 100 + new HvacSettings().BeamClearance * 1000 / 10 + new HvacSettings().MinClearHeight, 100 + 5 + 2.4, 1e-12);
        var duct = HvacRouting.AddDuct(q, s1, 1, 1, 8, 3, HvacDuctSystem.GioCap, 1800);
        check("ĐHKK3: ống thử 600 × 150", duct.W * 1000 + duct.H, 600150, 0);
        var dl = HvacRouting.AddUnit(q, s1, HvacUnitKind.DanLanh, 1, 4, 18000);
        var dn = HvacRouting.AddUnit(q, s1, HvacUnitKind.DanNong, 5, 4, 18000);
        var cu = HvacRouting.ConnectRefrigerant(q, dl, dn);
        var cond = HvacRouting.AddCondensate(q, dl, 1, 5.5);
        var mg = HvacRouting.AddTerminal(q, s1, 3, 2.5, true, 400, 600);

        var pk = HvacEdit.Hit(q, s1, 4, 1.2, 0.05);
        check("ĐHKK3: bấm trong bề rộng ống gió (0,2 m < 0,3 m) → ống gió", B(pk != null && pk.Kind == HvacElemKind.Duct && pk.Id == duct.Id), 1, 0);
        check("ĐHKK3: bấm thân ống → đoạn 1, không trúng đỉnh", B(pk.Segment == 1 && pk.Vertex == -1), 1, 0);
        var pv = HvacEdit.Hit(q, s1, 8.02, 1.0, 0.05);
        check("ĐHKK3: bấm gần góc tuyến → đỉnh 1", B(pv != null && pv.Kind == HvacElemKind.Duct && pv.Vertex == 1), 1, 0);
        var pu = HvacEdit.Hit(q, s1, 1.3, 4.05, 0.05);
        check("ĐHKK3: bấm trong dàn lạnh 0,9 × 0,25 → dàn lạnh", B(pu != null && pu.Kind == HvacElemKind.Unit && pu.Id == dl.Id), 1, 0);
        var pt = HvacEdit.Hit(q, s1, 3.25, 2.7, 0.05);
        check("ĐHKK3: bấm trong miệng gió 600 → miệng gió", B(pt != null && pt.Kind == HvacElemKind.Terminal && pt.Id == mg.Id), 1, 0);
        var pp = HvacEdit.Hit(q, s1, 1.0, 5.0, 0.05);
        check("ĐHKK3: bấm trên ống nước ngưng → ống", B(pp != null && pp.Kind == HvacElemKind.Pipe && pp.Id == cond.Id), 1, 0);
        check("ĐHKK3: bấm chỗ trống → không trúng", B(HvacEdit.Hit(q, s1, 20, 20, 0.05) == null), 1, 0);

        var win = HvacEdit.Window(q, s1, 0, 0, 9, 3.5, false);
        check("ĐHKK3: quét trái → phải: ống gió + miệng gió nằm trọn", B(win.Contains((HvacElemKind.Duct, duct.Id)) && win.Contains((HvacElemKind.Terminal, mg.Id))), 1, 0);
        check("ĐHKK3: quét trái → phải: dàn lạnh y = 4 ngoài khung", B(!win.Contains((HvacElemKind.Unit, dl.Id))), 1, 0);
        var crs = HvacEdit.Window(q, s1, 0.9, 3.9, 1.1, 4.1, true);
        check("ĐHKK3: quét phải → trái chạm dàn lạnh", B(crs.Contains((HvacElemKind.Unit, dl.Id))), 1, 0);
        check("ĐHKK3: khung nhỏ trái → phải không chọn dàn lạnh", B(!HvacEdit.Window(q, s1, 0.9, 3.9, 1.1, 4.1, false).Contains((HvacElemKind.Unit, dl.Id))), 1, 0);

        // dời ống gió 0,5 m theo X
        double x0 = duct.Path[0].X, z0 = duct.Path[0].Z;
        HvacEdit.Move(q, HvacElemKind.Duct, duct.Id, 0.5, 0);
        check("ĐHKK3: dời ống gió Δx = 0,5", duct.Path[0].X, x0 + 0.5, 1e-12);
        check("ĐHKK3: dời không đổi cao độ", duct.Path[0].Z, z0, 1e-12);
        // dời dàn nóng (0; 1) → ống đồng đi lại: 4 ngang + 1 ngang + (2,4 − 0,4) đứng = 7 m
        HvacEdit.Move(q, HvacElemKind.Unit, dn.Id, 0, 1);
        check("ĐHKK3: dời dàn nóng → ống đồng đi lại 4 + 1 + 2 = 7 m", cu.Length, 7, 1e-9);
        check("ĐHKK3: ống đồng kết thúc tại dàn nóng mới", cu.Path[^1].Y, 5, 1e-12);
        // dời dàn lạnh (0,5; 0) → ống ngưng giữ điểm xả (1; 5,5), đầu ống theo dàn
        HvacEdit.Move(q, HvacElemKind.Unit, dl.Id, 0.5, 0);
        check("ĐHKK3: ống ngưng đi lại từ dàn lạnh mới", cond.Path[0].X, 1.5, 1e-12);
        check("ĐHKK3: ống ngưng giữ điểm xả", cond.Path[^1].X * 10 + cond.Path[^1].Y, 10 + 5.5, 1e-9);
        check("ĐHKK3: ống ngưng vẫn dốc 1 %", HvacRouting.ActualSlope(cond), 0.01, 1e-9);

        // đổi cỡ 600×150 → 400×250, giữ cao độ đỉnh
        double topBefore = duct.Path[0].Z + duct.H / 2000;
        HvacEdit.Resize(q, duct, 400, 250);
        check("ĐHKK3: đổi cỡ → 400 × 250", duct.W * 1000 + duct.H, 400250, 0);
        check("ĐHKK3: đổi cỡ giữ cao độ đỉnh ống", duct.Path[0].Z + 0.125, topBefore, 1e-12);
        check("ĐHKK3: tâm ống hạ (250 − 150)/2 = 50 mm", z0 - duct.Path[0].Z, 0.05, 1e-12);
        var fits = q.Mep.Hvac.Fittings;
        check("ĐHKK3: phụ kiện sinh lại theo cỡ mới (cút 90° 400x250)", B(fits.Count(f => f.Kind == HvacFittingKind.Cut && f.Name == "Cút ống gió 400x250") == 1), 1, 0);
        // đổi lưu lượng → chọn lại cỡ (v = 6) 600 × 150, đỉnh giữ nguyên
        HvacEdit.SetFlow(q, duct, 1800);
        check("ĐHKK3: đổi Q → tự chọn lại 600 × 150", duct.W * 1000 + duct.H, 600150, 0);
        check("ĐHKK3: đổi Q giữ đỉnh ống", duct.Path[0].Z + 0.075, topBefore, 1e-12);
        // kéo đỉnh, vẽ lại tuyến
        HvacEdit.MoveVertex(q, duct, 2, 8.5, 3.5);
        check("ĐHKK3: kéo đỉnh cuối tuyến", duct.Path[2].X * 10 + duct.Path[2].Y, 85 + 3.5, 1e-9);
        HvacEdit.Reroute(q, duct, 1, 2, 4, 2);
        check("ĐHKK3: vẽ lại tuyến thẳng 3 m (2 đỉnh)", duct.Length * 10 + duct.Path.Count, 30 + 2, 1e-9);
        check("ĐHKK3: vẽ lại tuyến giữ cao độ", duct.Path[0].Z, z0, 1e-12);
        // công suất máy → cặp ống đồng
        HvacEdit.SetCapacity(q, dl, 24000);
        check("ĐHKK3: đổi 24000 BTU → ống gas Ø15,88", cu.GasDia, 15.88, 0);
        check("ĐHKK3: tên dàn lạnh theo công suất mới", B(dl.Name == "Dàn lạnh treo tường 24000 BTU"), 1, 0);
        check("ĐHKK3: rộng dàn lạnh 24000 BTU = 1,1 m", dl.W, 1.1, 0);
        // Id khối 3D → đối tượng
        check("ĐHKK3: Id 3D DN-GIO-12-3 → ống gió 12", B(Is(HvacEdit.FromSolidId("DN-GIO-12-3"), HvacElemKind.Duct, 12)), 1, 0);
        check("ĐHKK3: Id 3D DN-DHO-5-1 → ống 5", B(Is(HvacEdit.FromSolidId("DN-DHO-5-1"), HvacElemKind.Pipe, 5)), 1, 0);
        check("ĐHKK3: Id 3D DN-DH-7 → máy 7", B(Is(HvacEdit.FromSolidId("DN-DH-7"), HvacElemKind.Unit, 7)), 1, 0);
        check("ĐHKK3: Id 3D DN-MG-3 → miệng gió 3", B(Is(HvacEdit.FromSolidId("DN-MG-3"), HvacElemKind.Terminal, 3)), 1, 0);
        check("ĐHKK3: Id 3D COT-1 → không phải ĐHKK", B(HvacEdit.FromSolidId("COT-1") == null), 1, 0);
        var anc = HvacEdit.Anchor(q, HvacElemKind.Unit, dl.Id);
        check("ĐHKK3: điểm neo kéo 3D dàn lạnh = tâm (cao độ tuyệt đối)", anc.Value.Z, HvacRouting.UnitZ(q, dl), 1e-12);

        // ================= 2. IFC: kiểu ĐHKK + IfcRelDefinesByType =================
        // ống gió có góc để sinh cút (phụ kiện)
        HvacRouting.AddDuct(q, s1, 6, 1, 8.5, 3.5, HvacDuctSystem.GioHoi, 900);
        string ifc = IfcWriter.Write(q);
        var lines = ifc.Replace("\r", "").Split('\n').Where(l => l.StartsWith("#")).ToList();
        var ent = new Dictionary<int, string>();
        foreach (var l in lines)
        {
            int eq = l.IndexOf('=');
            int paren = l.IndexOf('(');
            if (eq > 1 && paren > eq) ent[int.Parse(l.Substring(1, eq - 1))] = l.Substring(eq + 1, paren - eq - 1);
        }
        check("IFC ĐHKK: có IfcDuctSegmentType", B(ent.Values.Contains("IFCDUCTSEGMENTTYPE")), 1, 0);
        check("IFC ĐHKK: có IfcDuctFittingType (cút)", B(ent.Values.Contains("IFCDUCTFITTINGTYPE") && ifc.Contains(".BEND.")), 1, 0);
        check("IFC ĐHKK: có IfcAirTerminalType", B(ent.Values.Contains("IFCAIRTERMINALTYPE")), 1, 0);
        check("IFC ĐHKK: có IfcUnitaryEquipmentType .SPLITSYSTEM.", B(ent.Values.Contains("IFCUNITARYEQUIPMENTTYPE") && ifc.Contains(".SPLITSYSTEM.")), 1, 0);
        check("IFC ĐHKK: có IfcPipeSegmentType (ống đồng / nước ngưng)", B(ent.Values.Contains("IFCPIPESEGMENTTYPE")), 1, 0);
        check("IFC ĐHKK: dàn lạnh / nóng là IfcEnergyConversionDevice", ent.Values.Count(v => v == "IFCENERGYCONVERSIONDEVICE"), q.Mep.Hvac.Units.Count, 0);
        check("IFC ĐHKK: phụ kiện ống gió là IfcFlowFitting", B(ent.Values.Count(v => v == "IFCFLOWFITTING") >= 1), 1, 0);
        // mỗi kiểu có đúng 10 thuộc tính; IfcRelDefinesByType trỏ tới đối tượng có thật và tới một *TYPE
        bool okTypes = true, okRel = true; int nRel = 0, nTyped = 0;
        foreach (var l in lines)
        {
            int eq = l.IndexOf('='), paren = l.IndexOf('(');
            if (eq < 0 || paren < 0) continue;
            string name = l.Substring(eq + 1, paren - eq - 1);
            string args = l.Substring(paren + 1, l.LastIndexOf(')') - paren - 1);
            int depth = 0, commas = 0; bool inStr = false;
            foreach (char ch in args)
            {
                if (ch == '\'') inStr = !inStr;
                else if (!inStr && ch == '(') depth++;
                else if (!inStr && ch == ')') depth--;
                else if (!inStr && depth == 0 && ch == ',') commas++;
            }
            if (name is "IFCDUCTSEGMENTTYPE" or "IFCDUCTFITTINGTYPE" or "IFCAIRTERMINALTYPE" or "IFCUNITARYEQUIPMENTTYPE" or "IFCPIPESEGMENTTYPE")
                okTypes &= commas == 9;
            if (name == "IFCRELDEFINESBYTYPE")
            {
                nRel++;
                okRel &= commas == 5;
                int a = args.IndexOf("(#"), b = args.IndexOf(')', a);
                var rel = args.Substring(a + 1, b - a - 1).Split(',').Select(x => int.Parse(x.Trim().TrimStart('#'))).ToList();
                nTyped += rel.Count;
                okRel &= rel.All(ent.ContainsKey);
                int t = int.Parse(args.Substring(args.LastIndexOf('#') + 1));
                okRel &= ent.TryGetValue(t, out var tn) && tn.EndsWith("TYPE");
            }
        }
        check("IFC ĐHKK: kiểu có đủ 10 thuộc tính IFC2X3", B(okTypes), 1, 0);
        check("IFC ĐHKK: IfcRelDefinesByType 6 thuộc tính, tham chiếu hợp lệ", B(okRel && nRel >= 5), 1, 0);
        int hvSolids = MepRegistration.Solids(q).Count(s => HvacIfc.TypeOf(s) != null);
        check("IFC ĐHKK: mọi khối ĐHKK đều được gán kiểu", nTyped, hvSolids, 0);
        check("IFC ĐHKK: vẫn là IFC2X3 hợp lệ (đầu / cuối tệp)", B(ifc.Contains("FILE_SCHEMA(('IFC2X3'))") && ifc.TrimEnd().EndsWith("END-ISO-10303-21;")), 1, 0);
        try { File.WriteAllText(Path.Combine(outDir, "DHKK_dot3.ifc"), ifc); } catch (Exception) { }

        // ================= 3. Hạ ống gió qua dầm (2 cút 45° mỗi phía) =================
        var p2 = House();
        double floor2 = MepRouting.StoryElevation(p2, s1);
        var d2 = HvacRouting.AddDuct(p2, s1, 1, 1, 8, 1, HvacDuctSystem.GioCap, 1800);
        var cl = HvacClashCheck.Beams(p2).Where(c => c.DuctId == d2.Id).ToList();
        check("Hạ ống: ống thẳng y = 1 va đúng 1 dầm (trục x = 5)", cl.Count, 1, 0);
        double bb = cl[0].BeamBottom, zTop0 = d2.Path[0].Z;
        double zLow = bb - 0.05 - 0.075, dz = zTop0 - zLow;
        var r2 = HvacBeamAvoid.DropUnderBeams(p2, d2);
        check("Hạ ống: 1 chỗ hạ, không cảnh báo", r2.Drops * 10 + r2.Warnings.Count, 10, 0);
        check("Hạ ống: tuyến 6 đỉnh (đầu, xuống, đáy, đáy, lên, cuối)", d2.Path.Count, 6, 0);
        check("Hạ ống: đỉnh ống thấp = đáy dầm − 50 mm", d2.Path.Min(x => x.Z) + 0.075, bb - 0.05, 1e-12);
        // dầm rộng 0,22 → đoạn thấp từ x = 5 − 0,11 − 0,05 = 4,84 tới 5,16; đoạn xiên 45°: chạy ngang = độ hạ
        check("Hạ ống: bắt đầu xiên tại x = 4,84 − Δz", d2.Path[1].X, 4.84 - dz, 1e-9);
        check("Hạ ống: tới đáy tại x = 4,84", d2.Path[2].X, 4.84, 1e-9);
        check("Hạ ống: hết đáy tại x = 5,16", d2.Path[3].X, 5.16, 1e-9);
        check("Hạ ống: lên lại tại x = 5,16 + Δz", d2.Path[4].X, 5.16 + dz, 1e-9);
        check("Hạ ống: đoạn xiên đúng 45°", (d2.Path[1].Z - d2.Path[2].Z) / (d2.Path[2].X - d2.Path[1].X), 1, 1e-9);
        var f2 = HvacRouting.RebuildFittings(p2);
        check("Hạ ống: 4 cút 45° (2 mỗi phía)", f2.Count(f => f.Kind == HvacFittingKind.Cut && f.Name.StartsWith("Cút 45°")), 4, 0);
        check("Hạ ống: chiều dài = 7 + 2·Δz·(√2 − 1)", d2.Length, 7 + 2 * dz * (Math.Sqrt(2) - 1), 1e-9);
        check("Hạ ống: hết va chạm dầm", HvacClashCheck.Beams(p2).Count(c => c.DuctId == d2.Id), 0, 0);
        check("Hạ ống: không còn cảnh báo không qua được dầm", HvacBeamAvoid.Warnings(p2).Count, 0, 0);
        check("Hạ ống: đáy ống ≥ sàn + 2,4 m", B(d2.Path.Min(x => x.Z) - 0.075 >= floor2 + 2.4), 1, 0);

        // tự hạ khi vẽ
        var p3 = House();
        var d3 = HvacRouting.AddDuctAvoidBeams(p3, s1, 1, 1, 8, 1, out var r3, HvacDuctSystem.GioCap, 1800);
        check("Tự hạ khi vẽ (AutoDropUnderBeams mặc định): 1 chỗ hạ, hết va chạm", r3.Drops * 10 + HvacClashCheck.Beams(p3).Count, 10, 0);
        p3.Mep.Hvac.Settings.AutoDropUnderBeams = false;
        HvacRouting.AddDuctAvoidBeams(p3, s1, 1, 2, 8, 2, out var r3b, HvacDuctSystem.GioCap, 1800);
        check("Tắt tự hạ: ống giữ sát trần → còn va chạm", r3b.Drops * 10 + B(HvacClashCheck.Beams(p3).Count == 1), 1, 0);

        // đoạn quá ngắn để xiên 45° → hạ cả tuyến
        var p4 = House();
        var d4 = HvacRouting.AddDuct(p4, s1, 4.8, 1, 5.3, 1, HvacDuctSystem.GioCap, 1800);
        double bb4 = HvacClashCheck.Beams(p4)[0].BeamBottom;
        var r4 = HvacBeamAvoid.DropUnderBeams(p4, d4);
        check("Đoạn ngắn 0,5 m: hạ cả tuyến", B(r4.WholeDuctLowered && d4.Path.Count == 2), 1, 0);
        check("Đoạn ngắn: cao độ đỉnh = đáy dầm − 50 mm", d4.Path.Max(x => x.Z) + 0.075, bb4 - 0.05, 1e-12);
        check("Đoạn ngắn: hết va chạm", HvacClashCheck.Beams(p4).Count, 0, 0);

        // trần không đủ → cảnh báo, giữ nguyên
        var p5 = House();
        var d5 = HvacRouting.AddDuct(p5, s1, 1, 1, 8, 1, HvacDuctSystem.GioCap, 1800);
        double hs = MepRouting.StoryHeight(p5, s1, p5.Mep.Settings);
        p5.Mep.Hvac.Settings.MinClearHeight = hs - 0.3;   // đáy ống sau khi hạ = đỉnh − 0,3 − 0,05 − 0,15 = sàn + h − 0,5 < sàn + h − 0,3
        int n5 = d5.Path.Count;
        double bb5 = HvacClashCheck.Beams(p5)[0].BeamBottom, short5 = (floor2 + hs - 0.3) - (bb5 - 0.05 - 0.15);
        var r5 = HvacBeamAvoid.DropUnderBeams(p5, d5);
        check("Trần không đủ: không hạ, 1 cảnh báo", r5.Drops * 10 + r5.Warnings.Count, 1, 0);
        check("Trần không đủ: mã ONG_GIO_KHONG_QUA_DAM", B(r5.Warnings.All(w => w.Code == HvacBeamAvoid.Code && w.FrameId > 0 && w.Message.Contains("thiếu"))), 1, 0);
        check("Trần không đủ: tuyến giữ nguyên", d5.Path.Count, n5, 0);
        check("Trần không đủ: thông báo đúng phần thiếu (sàn + h − 0,3 − đáy ống sau khi hạ)", B(r5.Warnings[0].Message.Contains($"thiếu {short5 * 1000:0} mm")), 1, 0);
        check("Trần không đủ: cảnh báo trong danh sách điện nước", HvacBeamAvoid.Warnings(p5).Count(w => w.Code == HvacBeamAvoid.Code), 1, 0);
        // cao độ trần phòng cũng là giới hạn
        var p6 = House();
        var d6 = HvacRouting.AddDuct(p6, s1, 1, 1, 8, 1, HvacDuctSystem.GioCap, 1800);
        p6.Arch.Rooms.Add(new Room { Id = 1, Story = s1, Name = "Phòng khách", Outline = new List<double[]> { new[] { 0.0, 0.0 }, new[] { 9.0, 0.0 }, new[] { 9.0, 4.5 }, new[] { 0.0, 4.5 } }, CeilingHeight = hs - 0.35 });
        var r6 = HvacBeamAvoid.DropUnderBeams(p6, d6);
        check("Trần phòng thấp hơn đáy ống sau khi hạ → cảnh báo", r6.Warnings.Count, 1, 0);

        // ================= 4. Tải lạnh, chọn máy, ống đồng, điện =================
        check("Tải lạnh: 20 m², h 3 m = 600·20 = 12000 BTU/h", HvacLoad.Load(20, 3, false, 0), 12000, 1e-9);
        check("Tải lạnh: có tường ngoài +15 % = 13800", HvacLoad.Load(20, 3, true, 0), 13800, 1e-9);
        check("Tải lạnh: 15 m², h 3,3, 4 người = 600·15·1,1 + 500·2 = 10900", HvacLoad.Load(15, 3.3, false, 4), 10900, 1e-9);
        check("Tải lạnh: h ≤ 3 m không giảm", HvacLoad.Load(10, 2.7, false, 0), 6000, 1e-9);
        check("Chọn máy: 12000 → 12000 BTU", HvacLoad.Choose(12000).Btu, 12000, 0);
        check("Chọn máy: 13800 → 18000 BTU", HvacLoad.Choose(13800).Btu, 18000, 0);
        check("Chọn máy: 6000 → 9000 BTU", HvacLoad.Choose(6000).Btu, 9000, 0);
        var c30 = HvacLoad.Choose(30000);
        check("Chọn máy: 30000 → 2 × 18000", c30.Count * 100000 + c30.Btu, 218000, 0);
        check("Tải lạnh: kW = BTU·0,000293", 12000 * HvacLoad.BtuToKw, 3.51685284, 1e-6);
        check("Ống đồng 9000 BTU: Ø6,35/Ø9,52", HvacSizing.CopperPair(9000).Liquid * 100 + HvacSizing.CopperPair(9000).Gas, 635 + 9.52, 1e-9);
        check("Ống đồng 18000 BTU: gas Ø12,7", HvacSizing.CopperPair(18000).Gas, 12.7, 0);
        check("Ống đồng 24000 BTU: gas Ø15,88", HvacSizing.CopperPair(24000).Gas, 15.88, 0);
        var pw9 = HvacLoad.Power(9000);
        check("Điện 9000 BTU: P = 9000·0,29307/3 = 879 W", pw9.PowerW, 9000 * 0.29307107 / 3, 1e-6);
        check("Điện 9000 BTU: I = P/(220·0,85)", pw9.CurrentA, 9000 * 0.29307107 / 3 / 187, 1e-9);
        check("Điện 9000 BTU: 1,25·I = 5,9 A → aptomat 6 A, dây 2,5 mm²", pw9.BreakerA * 10 + pw9.WireMm2, 60 + 2.5, 1e-9);
        var pw24 = HvacLoad.Power(24000);
        check("Điện 24000 BTU: 1,25·12,54 = 15,7 A → aptomat 16 A", pw24.BreakerA, 16, 0);
        check("Điện 24000 BTU: dây 2,5 mm² (Iz 24 A ≥ 16 A)", pw24.WireMm2, 2.5, 0);
        var pw48 = HvacLoad.Power(24000, 2);
        check("Điện 2 × 24000 BTU chung mạch: 31,4 A → 32 A, dây 4 mm²", pw48.BreakerA * 10 + pw48.WireMm2, 320 + 4, 1e-9);

        // đặt máy cho phòng 5 × 4,5: tường trái + dưới là tường ngoài, cửa đi trên tường phải (x = 5)
        var p7 = House();
        p7.Walls.RemoveAll(w => w.Story == s1);
        const string ext = "Gạch đặc 220 trát 2 mặt", inn = "Gạch đặc 110 trát 2 mặt";
        p7.Walls.Add(new WallLine { Id = 101, Story = s1, X1 = 0, Y1 = 0, X2 = 5, Y2 = 0, Type = ext });
        p7.Walls.Add(new WallLine { Id = 102, Story = s1, X1 = 5, Y1 = 0, X2 = 5, Y2 = 4.5, Type = inn, Openings = new List<WallOpening> { new() { Id = 1, Type = "D1 900x2200", Offset = 2.0 } } });
        p7.Walls.Add(new WallLine { Id = 103, Story = s1, X1 = 5, Y1 = 4.5, X2 = 0, Y2 = 4.5, Type = inn });
        p7.Walls.Add(new WallLine { Id = 104, Story = s1, X1 = 0, Y1 = 0, X2 = 0, Y2 = 4.5, Type = ext });
        var room = new Room { Id = 7, Story = s1, Name = "Phòng ngủ", Outline = new List<double[]> { new[] { 0.0, 0.0 }, new[] { 5.0, 0.0 }, new[] { 5.0, 4.5 }, new[] { 0.0, 4.5 } } };
        p7.Arch.Rooms.Add(room);
        double h7 = MepRouting.StoryHeight(p7, s1, null);
        check("Phòng: nhận 4 tường bao", HvacLoad.RoomWalls(p7, room).Count, 4, 0);
        check("Phòng: tường 220 là tường ngoài, 110 là tường trong", B(HvacLoad.IsExterior(p7, p7.Walls.First(w => w.Id == 101)) && !HvacLoad.IsExterior(p7, p7.Walls.First(w => w.Id == 102))), 1, 0);
        var ld = HvacLoad.RoomLoad(p7, room, 2);
        double qExp = 600 * 22.5 * Math.Max(1, h7 / 3) * 1.15;
        check("Phòng: Q = 600·22,5·max(1; h/3)·1,15", ld.LoadBtu, qExp, 1e-9);
        check("Phòng: có tường ngoài", B(ld.Exterior), 1, 0);
        check("Phòng: máy chọn = cỡ chuẩn nhỏ nhất ≥ Q", ld.UnitBtu, HvacLoad.UnitSizes.First(s => s >= qExp), 0);
        var pl = HvacLoad.PlaceAc(p7, room, 2);
        check("Đặt máy: dàn lạnh trên tường trái (đối diện cửa, tường ngoài)", pl.IndoorWallId, 104, 0);
        check("Đặt máy: dàn nóng trên tường ngoài cùng tường", pl.OutdoorWallId, 104, 0);
        double t104 = ModelSolids.WallThickness(p7, p7.Walls.First(w => w.Id == 104));
        var ui = pl.Indoor[0]; var uo = pl.Outdoor[0];
        check("Đặt máy: dàn lạnh giữa tường (y = 2,25)", ui.Y, 2.25, 1e-9);
        check("Đặt máy: dàn lạnh áp mặt trong tường (x = t/2 + D/2)", ui.X, t104 / 2 + ui.D / 2, 1e-9);
        check("Đặt máy: dàn nóng ngoài tường (x = −(t/2 + D/2 + 0,1))", uo.X, -(t104 / 2 + uo.D / 2 + 0.1), 1e-9);
        check("Đặt máy: dàn lạnh xoay dọc tường 90°", ui.Angle, 90, 1e-9);
        var cuP = pl.Pipes.First(x => x.Kind == HvacPipeKind.OngDong);
        check("Đặt máy: ống đồng theo công suất", cuP.GasDia, HvacSizing.CopperPair(ld.UnitBtu).Gas, 0);
        check("Đặt máy: ống đồng dài = (x lạnh − x nóng) + (2,4 − 0,4)", cuP.Length, ui.X - uo.X + 2.0, 1e-9);
        check("Đặt máy: ống nước ngưng xả ra ngoài tường", B(pl.Pipes.Any(x => x.Kind == HvacPipeKind.NuocNgung && x.Path[^1].X < 0)), 1, 0);
        check("Đặt máy: điện máy theo công suất chọn", pl.Power.BreakerA, HvacLoad.Power(ld.UnitBtu).BreakerA, 0);
        // dàn lạnh không đặt trên tường có cửa đi
        p7.Walls.First(w => w.Id == 104).Openings = new List<WallOpening> { new() { Id = 2, Type = "D1 900x2200", Offset = 1.0 } };
        var pl2 = HvacLoad.PlaceAc(p7, room, 2);
        check("Đặt máy: tường trái có cửa → chuyển sang tường ngoài dưới", pl2.IndoorWallId, 101, 0);
    }
}
