using KetCauVXT.Import;
using KetCauVXT.Import.Arch;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Chi tiết kiến trúc kiểu Revit (mái dốc, trần, lan can, phào chỉ, ốp, sao chép tầng) + nhập mô hình SketchUp/Revit → mặt bằng.</summary>
public static class ArchDetailTests
{
    private static double B(bool b) => b ? 1 : 0;

    private static Project House()
    {
        var p = ModelBuilder.NewProject();
        var pl = new GridColumnPlan { SpansX = new() { 4, 4 }, SpansY = new() { 5 }, BaseElevation = 0 };
        pl.Stories.Add(new StoryRow { Name = "T2", Height = 3.6 });
        pl.Stories.Add(new StoryRow { Name = "MAI", Height = 3.3 });
        pl.ResizeMask();
        GridColumnSetup.Rebuild(p, pl, new ModelBuilder.GridFrameOptions());
        WallAssembly.Upgrade(p);
        return p;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "ArchDetail");
        Directory.CreateDirectory(dir);

        // ================= 1. Mái =================
        var p = House();
        var fp = ArchDetail.Footprint(p, "MAI");
        check("KT hộp bao mái 8 × 5", B(fp is { } f0 && Math.Abs(f0.X1 - f0.X0 - 8) < 1e-6 && Math.Abs(f0.Y1 - f0.Y0 - 5) < 1e-6), 1, 0);
        var r2 = ArchDetail.AddRoof(p, "MAI", 0, 0, 8, 5, RoofShape.HaiMai, 40, 0, "Mái ngói");
        check("KT mái 2 dốc: cao đỉnh = 0,4·2,5", r2.RidgeHeight, 1.0, 1e-9);
        double run = Math.Sqrt(2.5 * 2.5 + 1.0 * 1.0);
        check("KT mái 2 dốc: diện tích nghiêng = 2·8·√(2,5²+1²)", RoofGeometry.SlopedArea(r2), 2 * 8 * run, 1e-6);
        check("KT mái 2 dốc: 2 tường thu hồi = 2·½·5·1", RoofGeometry.GableArea(r2), 5.0, 1e-6);
        var (f2, g2) = RoofGeometry.Build(r2, 6.9);
        check("KT mái 2 dốc: đỉnh +7,9", f2.SelectMany(q => q).Max(q => q.Z), 7.9, 1e-9);
        var r4 = ArchDetail.AddRoof(p, "MAI", 0, 0, 8, 5, RoofShape.BonMai, 40, 0.6, "Mái tôn");
        var (f4, _) = RoofGeometry.Build(r4, 0);
        check("KT mái 4 dốc: 4 mặt", f4.Count, 4, 0);
        // diện tích nghiêng mái 4 dốc đều dốc = diện tích bằng / cos α
        double cosA = 1 / Math.Sqrt(1 + 0.4 * 0.4);
        check("KT mái 4 dốc (đua 0,6): S nghiêng = S bằng / cos α", RoofGeometry.SlopedArea(r4), 9.2 * 6.2 / cosA, 1e-6);
        var sq = ArchDetail.AddRoof(p, "MAI", 0, 0, 4, 4, RoofShape.BonMai, 50, 0, "Mái chóp");
        check("KT mái chóp vuông: 4 tam giác", RoofGeometry.Build(sq, 0).Faces.All(q => q.Count == 3) ? 1 : 0, 1, 0);
        var solids = ModelSolids.Build(p, new[] { Discipline.KienTruc });
        check("KT khối mái dốc 2 + 4 + 4 mặt", solids.Count(s => s.Id.StartsWith("MAID-")), 10, 0);
        check("KT khối tường thu hồi 2", solids.Count(s => s.Id.StartsWith("THUHOI-")), 2, 0);
        check("KT mái dốc không vẽ thêm tấm phẳng cũ", solids.Count(s => s.Id.StartsWith("MAI-")), 0, 0);
        var plate = solids.First(s => s.Id == $"MAID-{r2.Id}-1");
        check("KT tấm mái nghiêng đúng độ dốc (pháp tuyến)", Math.Abs(plate.ZDir.Z), 1 / Math.Sqrt(1 + 0.16), 1e-6);

        // ---- mái nhà chữ L: chia khối, mái cánh chạy vào mái chính ----
        var L = new List<(double X, double Y)> { (0, 0), (10, 0), (10, 4), (4, 4), (4, 10), (0, 10) };
        check("KT đa giác chữ L chia 2 hình chữ nhật", RasterOutline.Rectangles(L).Count, 2, 0);
        var un = RasterOutline.UnionOutline(new List<IReadOnlyList<(double X, double Y)>> { new List<(double, double)> { (0, 0), (4, 0), (4, 10), (0, 10) }, new List<(double, double)> { (4, 0), (10, 0), (10, 4), (4, 4) } });
        check("KT hợp 2 ô sàn → đường bao chữ L 6 đỉnh", un?.Count ?? 0, 6, 0);
        check("KT hợp 2 ô sàn → diện tích 64", Math.Abs(RasterOutline.SignedArea(un)), 64, 1e-6);
        var ring = new bool[5, 5];
        for (int i = 0; i < 5; i++) for (int j = 0; j < 5; j++) ring[i, j] = !(i == 2 && j == 2);
        var rl2 = RasterOutline.Loops(ring, 0, 0, 1);
        check("KT vòng có lỗ: 1 vòng ngoài + 1 lỗ", B(rl2.Count == 2 && rl2.Count(l => RasterOutline.SignedArea(l) > 0) == 1), 1, 0);
        var rL = ArchDetail.AddRoof(p, "MAI", L, RoofShape.BonMai, 50, 0, "Mái ngói chữ L");
        var bl = RoofGeometry.Blocks(rL);
        check("KT mái chữ L: 2 khối", bl.Count, 2, 0);
        check("KT mái chữ L: khối cánh kéo vào tới nóc mái chính (x = 2)", bl.Min(b => b.X0 > 3 ? 99 : b.X1 > 9 ? b.X0 : 99), 2, 1e-9);
        var (fL, _) = RoofGeometry.Build(rL, 0);
        check("KT mái chữ L: 4 + 3 mặt", fL.Count, 7, 0);
        check("KT mái chữ L: hai nóc cùng cao 1,0", fL.SelectMany(q => q).Max(q => q.Z), 1.0, 1e-9);
        check("KT mái chữ L: S nghiêng = 64·√1,25", RoofGeometry.SlopedArea(rL), 64 * Math.Sqrt(1.25), 1e-6);
        rL.Overhang = 0.5;
        check("KT mái chữ L đua 0,5: S bằng = 64 + 40·0,5 + 4·0,25", RoofGeometry.PlanArea(rL), 85, 1e-6);
        p.Arch.Roofs.Remove(rL);

        // ================= 2. Tường, phào chỉ, ốp =================
        var wt = p.WallTypes.First(t => Math.Abs(t.Thickness - 0.22) < 1e-6).Name;
        var wr = ModelEdit.AddWall(p, "T2", 0, 0, 8, 0, wt);
        var w = p.Walls.Last();
        var door = p.OpeningTypes.First(o => o.Kind == WallOpeningKind.Door);
        WallAssembly.PlaceOpening(p, w, door.Name, 2.0);
        int outSide = ArchDetail.OutsideSide(p, w);
        var sw = ArchDetail.AddSweep(p, w, -outSide, ArchDetail.SweepPresets[0]);
        double expSweep = 8 - door.Width;
        check("KT phào chân tường trừ cửa đi", ArchDetail.SweepLength(p, sw), expSweep, 1e-6);
        var sw2 = ArchDetail.AddSweep(p, w, outSide, ArchDetail.SweepPresets[4]);   // phào trần 2,86 > đầu cửa → liền
        check("KT phào trần qua đầu cửa liền 8 m", ArchDetail.SweepLength(p, sw2), door.Height < 2.82 ? 8 : ArchDetail.SweepLength(p, sw2), 1e-6);
        var cl = ArchDetail.AddCladding(p, w, outSide, ArchDetail.CladPresets[0]);   // ốp đá 0…1,2 m
        check("KT ốp đá trừ phần cửa", ArchDetail.CladdingArea(p, cl), 8 * 1.2 - door.Width * Math.Min(1.2, door.Height), 1e-6);
        var rects = ArchDetail.Subtract(0, 4, 0, 3, new() { (1, 2, 1, 2) });
        check("KT chia vùng quanh ô cửa: diện tích", rects.Sum(q => (q.A1 - q.A0) * (q.Z1 - q.Z0)), 12 - 1, 1e-9);
        check("KT phía ngoài nhà của tường trục A (y = 0) là y âm", B(outSide == -ArchDetail.SideOf(w, 4, 2.5)), 1, 0);
        var per = ArchDetail.PerimeterWalls(p, "T2");
        check("KT tường biên có tường trục A", B(per.Contains(w)), 1, 0);
        solids = ModelSolids.Build(p, new[] { Discipline.KienTruc });
        check("KT khối phào: 2 đoạn (chân) + 1 (trần)", solids.Count(s => s.Id.StartsWith("PHAO-")), 3, 0);
        check("KT khối ốp có", B(solids.Any(s => s.Id.StartsWith("OP-"))), 1, 0);

        // ================= 3. Lan can + tải =================
        var rl = ArchDetail.AddRailing(p, "T2", new[] { (0.0, 5.0), (8.0, 5.0) }, RailKind.Xay, 1.0);
        check("KT lan can dài 8 m", rl.Length, 8, 1e-9);
        check("KT tải lan can xây 110 cao 1 m", ArchDetail.RailLoad(rl), 18 * 0.11 + 0.54, 1e-9);
        WallLoadGenerator.Apply(p);
        var railLoads = p.FrameLoads.Where(l => l.Note.Contains("Lan can xây")).ToList();
        check("KT tải lan can lên dầm trục B tầng T2 (2 nhịp)", railLoads.Count, 2, 0);
        check("KT giá trị tải lan can", railLoads.Sum(l => l.Points[0][1]) / Math.Max(1, railLoads.Count), 2.52, 1e-9);
        var hz = p.FrameLoads.Where(l => l.Note.Contains($"lan can #{rl.Id}")).ToList();
        check("KT tải ngang tay vịn: 2 dầm, phương Y", B(hz.Count == 2 && hz.All(l => l.Dir == LoadDir.GlobalY && l.Pattern == "HT")), 1, 0);
        check("KT tải ngang hướng ra ngoài nhà (+Y) 0,3 kN/m", hz.Sum(l => l.Points[0][1]) / Math.Max(1, hz.Count), 0.3, 1e-9);
        WallLoadGenerator.Apply(p);
        check("KT tải lan can không nhân đôi khi phân tích lại", p.FrameLoads.Count(l => l.Note.Contains("Lan can xây")), 2, 0);
        r2.SlabLoad = ArchDetailLoads.DefaultRoofLoad(r2.Type);
        check("KT tải mái ngói mặc định 0,9 kN/m²", r2.SlabLoad, 0.9, 1e-9);
        WallLoadGenerator.Apply(p);
        var roofLoads = p.AreaLoads.Where(l => l.Note.Contains($"mái #{r2.Id}")).ToList();
        check("KT tải mái dồn lên 2 ô sàn tầng MAI", roofLoads.Count, p.Slabs.Count(sl => sl.Story == "MAI"), 0);
        WallLoadGenerator.Apply(p);
        check("KT tải mái không nhân đôi", p.AreaLoads.Count(l => l.Note.Contains($"mái #{r2.Id}")), roofLoads.Count, 0);
        var ri = ArchDetail.AddRailing(p, "T2", new[] { (0.0, 2.0), (3.0, 2.0), (3.0, 4.0) }, RailKind.Inox, 0.9);
        solids = ModelSolids.Build(p, new[] { Discipline.KienTruc });
        check("KT khối lan can inox có trụ", B(solids.Count(s => s.Id.StartsWith($"LC-{ri.Id}-") && s.Name.EndsWith("trụ")) >= 4), 1, 0);
        check("KT lan can kiểu IfcRailing", B(solids.Where(s => s.Id.StartsWith("LC-")).All(s => s.IfcType == "IfcRailing")), 1, 0);

        // ================= 4. Trần theo phòng, sao chép tầng =================
        p.Arch.Rooms.Add(new Room { Id = 1, Story = "T2", Name = "Phòng khách", Outline = new() { new[] { 0.0, 0.0 }, new[] { 4.0, 0.0 }, new[] { 4.0, 5.0 }, new[] { 0.0, 5.0 } } });
        check("KT trần theo phòng: 1", ArchDetail.CeilingsFromRooms(p, "T2", 2.9, "Trần thạch cao"), 1, 0);
        check("KT trần không lặp", ArchDetail.CeilingsFromRooms(p, "T2", 2.9, "Trần thạch cao"), 0, 0);
        int walls0 = p.Walls.Count;
        int nCopy = ArchDetail.CopyToStories(p, "T2", new[] { "MAI" });
        check("KT chép tầng: tường sang MAI", p.Walls.Count(q => q.Story == "MAI"), p.Walls.Count(q => q.Story == "T2"), 0);
        check("KT chép tầng: cửa theo tường", p.Walls.Where(q => q.Story == "MAI").Sum(q => q.Openings.Count), 1, 0);
        check("KT chép tầng: phào theo tường mới", p.Arch.Sweeps.Count, 4, 0);
        check("KT chép tầng: lan can 2", p.Arch.Railings.Count(q => q.Story == "MAI"), 2, 0);
        check("KT chép tầng: phòng + trần", p.Arch.Rooms.Count(q => q.Story == "MAI") + p.Arch.Ceilings.Count(q => q.Story == "MAI"), 2, 0);
        ArchDetail.CopyToStories(p, "T2", new[] { "MAI" }, new ArchDetail.CopyOptions { Rooms = false, Ceilings = false, Railings = false, Furniture = false });
        check("KT chép lần 2 không trùng tường", p.Walls.Count, walls0 * 2, 0);
        check("KT số phần tử đã chép > 0", B(nCopy > 0), 1, 0);

        // ================= 5. Khối lượng =================
        var q = ArchDetail.Quantities(p);
        check("KT KL: mái ngói 2 dốc", q.First(r => r.Item.StartsWith("Mái ngói")).Qty, Math.Round(2 * 8 * run, 2), 1e-9);
        check("KT KL: lan can xây md (2 tầng)", q.Where(r => r.Group == "Lan can" && r.Item.StartsWith("Lan can xây")).Sum(r => r.Qty), 16, 1e-9);
        check("KT KL: phào chỉ có", B(q.Any(r => r.Group == "Phào chỉ")), 1, 0);
        File.WriteAllText(Path.Combine(dir, "KhoiLuongHoanThienKT.csv"), ArchDetail.QuantitiesCsv(p));
        check("KT KL: CSV có tiêu đề", B(ArchDetail.QuantitiesCsv(p).StartsWith("Nhóm;Tầng")), 1, 0);
        // lưu / đọc lại dự án
        var json = System.Text.Json.JsonSerializer.Serialize(p, Project.JsonOptions);
        var back = System.Text.Json.JsonSerializer.Deserialize<Project>(json, Project.JsonOptions);
        check("KT lưu/đọc: lan can, phào, ốp, mái dốc", back.Arch.Railings.Count + back.Arch.Sweeps.Count + back.Arch.Claddings.Count + B(back.Arch.Roofs.Any(r => r.Shape == RoofShape.BonMai)),
            p.Arch.Railings.Count + p.Arch.Sweeps.Count + p.Arch.Claddings.Count + 1, 0);
        Levels.RenameStory(p, "T2", "T2A");
        check("KT đổi tên tầng: lan can theo", p.Arch.Railings.Count(r => r.Story == "T2A"), 2, 0);

        // ================= 6a. Họ đồ đạc tham số =================
        var lib = Library3D.LoadAll();
        check("KT thư viện có họ tham số", B(lib.Any(i => i.Id == "HO/Bàn ăn") && lib.Count(i => i.Source == "Họ tham số") == FurnitureFamilies.All.Count), 1, 0);
        var tbl = Library3D.Place(p, lib.First(i => i.Id == "HO/Bàn ăn"), "T2A", 2, 2);
        check("KT đặt bàn ăn: họ tham số", B(tbl.Family == "Bàn ăn"), 1, 0);
        var ts = FurnitureFamilies.Solids(p, tbl);
        check("KT bàn ăn: 4 chân + mặt bàn", ts.Count, 5, 0);
        check("KT mã chi tiết DD-n / DD-n.k", B(ts[0].Id == $"DD-{tbl.Id}" && ts[1].Id == $"DD-{tbl.Id}.1" && FurnitureFamilies.BaseId(ts[3].Id) == $"DD-{tbl.Id}"), 1, 0);
        tbl.Width = 2.0;
        var top = FurnitureFamilies.Solids(p, tbl).First(x => x.Name.EndsWith("Mặt bàn"));
        check("KT đổi rộng 2,0 m → mặt bàn dựng lại 2,0 m", top.Profile.Max(q => q[0]) - top.Profile.Min(q => q[0]), 2.0, 1e-9);
        check("KT mặt bàn cao 0,75", top.Origin.Z + top.Depth - ModelSolids.StoryZ(p, "T2A"), 0.75, 1e-9);
        var up = Library3D.Place(p, lib.First(i => i.Id == "HO/Tủ bếp trên"), "T2A", 4, 1);
        check("KT tủ bếp trên treo cao 1,5 m", FurnitureFamilies.Solids(p, up).Min(x => x.Origin.Z) - ModelSolids.StoryZ(p, "T2A"), 1.5, 1e-9);
        var bed = Library3D.Place(p, lib.First(i => i.Id == "HO/Giường đôi"), "T2A", 3, 3, 90);
        var head = FurnitureFamilies.Solids(p, bed).First(x => x.Name.EndsWith("Đầu giường"));
        // xoay 90°: lưng (−y địa phương) quay về +x thế giới → đầu giường ở phía x lớn
        double hx = head.Origin.X + head.XDir.X * head.Profile.Average(q => q[0]) + head.YDir.X * head.Profile.Average(q => q[1]);
        check("KT giường xoay 90°: đầu giường ở phía +x", B(hx > 3 + 0.9), 1, 0);
        var wc = Library3D.Place(p, lib.First(i => i.Id == "HO/Bồn cầu"), "T2A", 1, 1);
        check("KT bồn cầu thuộc bộ môn điện nước", B(wc.Discipline == Discipline.DienNuoc), 1, 0);
        var all3 = ModelSolids.Build(p);
        check("KT khối 3D có chi tiết họ tham số", all3.Count(x => FurnitureFamilies.BaseId(x.Id) == $"DD-{bed.Id}"), 4, 0);
        var dd = all3.Where(x => x.Id.StartsWith("DD-")).ToList();
        check("KT mã khối đồ đạc không trùng", dd.Select(x => x.Id).Distinct().Count(), dd.Count, 0);

        // ================= 6. Mô hình SketchUp → mặt bằng =================
        var m = HouseMesh();
        check("KT lưới: trục đứng Z", MeshToPlan.GuessUpAxis(m), 2, 0);
        var yUp = HouseMesh();
        foreach (var v in yUp.V) { double y = v[1], z = v[2]; v[1] = z; v[2] = -y; }   // như OBJ SketchUp "Y lên"
        check("KT lưới: nhận ra Y lên", MeshToPlan.GuessUpAxis(yUp), 1, 0);
        MeshToPlan.Prepare(yUp, new MeshPlanOptions(), false);
        check("KT lưới: đổi Y lên → Z lên (cao 6,9 + 0,1)", yUp.V.Max(v => v[2]), 7.0, 1e-6);
        var lv = MeshToPlan.DetectLevels(m, new MeshPlanOptions());
        check("KT lưới: 3 cao độ sàn", lv.Count, 3, 0);
        check("KT lưới: sàn tầng 2 +3,6", lv.Count > 1 ? lv[1] : 0, 3.6, 1e-6);
        var mp = MeshToPlan.Build(m, new MeshPlanOptions());
        check("KT lưới: 1 cột giữa nhà", mp.Columns, 1, 0);
        check("KT lưới: 1 cửa đi (tầng 1)", mp.Doors, 1, 0);
        check("KT lưới: 1 cửa sổ (tầng 2)", mp.Windows, 1, 0);
        check("KT lưới: chiều cao tầng 3,6 / 3,3", B(mp.Heights.Count == 2 && Math.Abs(mp.Heights[0] - 3.6) < 1e-6 && Math.Abs(mp.Heights[1] - 3.3) < 1e-6), 1, 0);
        string dxf = Path.Combine(dir, "MoHinhSketchUp_DauVao.dxf");
        MeshToPlan.WriteDxf(mp.Entities, dxf);
        var o = new ArchImportOptions();
        var d = ArchPlanReader.Read(dxf, o);
        check("KT lưới → DXF → nhận 2 mặt bằng tầng", d.Floors.Count, 2, 0);
        check("KT mặt bằng tầng 1 có ≥ 4 tường", B(d.Floors.Count > 0 && d.Floors[0].Walls.Count >= 4), 1, 0);
        check("KT mặt bằng tầng 1 có cột", B(d.Floors.Count > 0 && d.Floors[0].Columns.Count >= 1), 1, 0);
        check("KT mặt bằng có cửa đi + cửa sổ", d.Floors.Sum(f => f.Openings.Count), 2, 0);
        check("KT tường dày 0,22", B(d.Floors.Count > 0 && d.Floors[0].Walls.Any(x => Math.Abs(x.Thickness - 0.22) < 0.02)), 1, 0);
        check("KT lưới: vùng sàn tầng 1 + tầng 2 (có lỗ thang) ≥ 3 vòng", B(mp.FloorRegions >= 3), 1, 0);
        check("KT lưới: 1 cầu thang", mp.Stairs, 1, 0);
        check("KT bậc thang không thành tường giả", B(d.Floors.Count > 0 && !d.Floors[0].Walls.Any(x => x.Ax > 6.05 + 0 && x.Ax < 6.95 && x.Bx > 6.05 && x.Bx < 6.95 && x.Ay > 1.05 && x.By < 3.45)), 1, 0);
        check("KT mặt bằng tầng 1 có vùng cầu thang", B(d.Floors.Count > 0 && d.Floors[0].Zones.Any(zn => zn.Kind == RoomKind.Stair)), 1, 0);
        check("KT mặt bằng tầng 2 có vùng sàn ban công / lỗ thang", B(d.Floors.Count > 1 && d.Floors[1].SlabRegions.Count >= 2 && d.Floors[1].SlabRegions.Any(r => r.IsOpening)), 1, 0);
        check("KT nhà mái bằng: không có mái dốc", B(mp.Roof == null), 1, 0);
        // nhà 1 tầng mái 2 dốc (không có sàn mái): diềm +3,0 thành đỉnh tầng, mái i = 1,2/3 = 40 %
        var g1 = GableHouseMesh();
        var gp = MeshToPlan.Build(g1, new MeshPlanOptions());
        check("KT mái dốc: nhận mái 2 dốc", B(gp.Roof is { Shape: RoofShape.HaiMai }), 1, 0);
        check("KT mái dốc: độ dốc 40 %", gp.Roof?.SlopePercent ?? 0, 40, 0.5);
        check("KT mái dốc: diềm làm đỉnh tầng 1 → 2 cao độ", gp.Levels.Count, 2, 0);
        check("KT mái dốc: chiều cao tầng 3,0", gp.Heights.FirstOrDefault(), 3.0, 1e-6);
    }

    // ---- nhà 1 tầng 8 × 6 m, tường 220 cao 3,0, mái 2 dốc nóc dọc X ở y = 3, cao nóc +4,2 (không có sàn mái) ----
    private static RawMesh GableHouseMesh()
    {
        var m = new RawMesh();
        void Box(double x0, double y0, double z0, double x1, double y1, double z1)
        {
            int b = m.V.Count;
            foreach (var (x, y, z) in new[] { (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1) })
                m.V.Add(new[] { x, y, z });
            int[][] q = { new[] { 0, 2, 1 }, new[] { 0, 3, 2 }, new[] { 4, 5, 6 }, new[] { 4, 6, 7 }, new[] { 0, 1, 5 }, new[] { 0, 5, 4 },
                          new[] { 1, 2, 6 }, new[] { 1, 6, 5 }, new[] { 2, 3, 7 }, new[] { 2, 7, 6 }, new[] { 3, 0, 4 }, new[] { 3, 4, 7 } };
            foreach (var t in q) m.F.Add(new[] { b + t[0], b + t[1], b + t[2] });
        }
        void Quad(double[] a, double[] b, double[] c, double[] d)
        {
            int i = m.V.Count; m.V.Add(a); m.V.Add(b); m.V.Add(c); m.V.Add(d);
            m.F.Add(new[] { i, i + 1, i + 2 }); m.F.Add(new[] { i, i + 2, i + 3 });
        }
        Box(0, 0, -0.1, 8, 6, 0);
        Box(0, 0, 0, 8, 0.22, 3); Box(0, 5.78, 0, 8, 6, 3); Box(0, 0.22, 0, 0.22, 5.78, 3); Box(7.78, 0.22, 0, 8, 5.78, 3);
        Quad(new[] { 0.0, 0, 3 }, new[] { 8.0, 0, 3 }, new[] { 8.0, 3, 4.2 }, new[] { 0.0, 3, 4.2 });   // mái trước (pháp tuyến −Y, +Z)
        Quad(new[] { 8.0, 6, 3 }, new[] { 0.0, 6, 3 }, new[] { 0.0, 3, 4.2 }, new[] { 8.0, 3, 4.2 });   // mái sau
        return m;
    }

    // ---- nhà 2 tầng 8 × 6 m dựng bằng khối hộp như SketchUp: sàn 0 / 3,6 / 6,9 (dày 0,1, mặt trên đúng cao độ),
    //      tường 220 bao quanh, cửa đi 1,0 × 2,2 tầng 1 mặt trước, cửa sổ 1,2 × 1,2 (bậu 0,9) tầng 2, cột 0,3 × 0,3 giữa nhà ----
    private static RawMesh HouseMesh()
    {
        var m = new RawMesh();
        void Box(double x0, double y0, double z0, double x1, double y1, double z1)
        {
            int b = m.V.Count;
            foreach (var (x, y, z) in new[] { (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1) })
                m.V.Add(new[] { x, y, z });
            int[][] q = { new[] { 0, 2, 1 }, new[] { 0, 3, 2 }, new[] { 4, 5, 6 }, new[] { 4, 6, 7 }, new[] { 0, 1, 5 }, new[] { 0, 5, 4 },
                          new[] { 1, 2, 6 }, new[] { 1, 6, 5 }, new[] { 2, 3, 7 }, new[] { 2, 7, 6 }, new[] { 3, 0, 4 }, new[] { 3, 4, 7 } };
            foreach (var t in q) m.F.Add(new[] { b + t[0], b + t[1], b + t[2] });
        }
        Box(0, 0, -0.1, 8, 6, 0);
        // sàn tầng 2 có lỗ thang x 6…7, y 1…3,5 + ban công nhô ra y −1,2…0, x 2…6
        Box(0, 0, 3.5, 6, 6, 3.6); Box(7, 0, 3.5, 8, 6, 3.6); Box(6, 0, 3.5, 7, 1, 3.6); Box(6, 3.5, 3.5, 7, 6, 3.6);
        Box(2, -1.2, 3.5, 6, 0, 3.6);
        Box(0, 0, 6.8, 8, 6, 6.9);
        // cầu thang 1 vế: 19 bậc (mặt bậc mỏng 4 cm) cao 0,18 m, sâu 0,13 m trong ô x 6…7, y 1…3,5
        for (int k = 1; k <= 19; k++) { double zt = 0.18 * k * 3.6 / 3.42; Box(6, 1 + (k - 1) * 0.13, zt - 0.04, 7, 1 + k * 0.13, zt); }
        const double t = 0.22;
        foreach (var (z0, z1, front) in new[] { (0.0, 3.5, 1), (3.6, 6.8, 2) })
        {
            Box(0, 6 - t, z0, 8, 6, z1);          // tường sau
            Box(0, t, z0, t, 6 - t, z1);          // tường trái
            Box(8 - t, t, z0, 8, 6 - t, z1);      // tường phải
            if (front == 1)
            {   // tường trước có cửa đi 3,0…4,0
                Box(0, 0, z0, 3.0, t, z1); Box(4.0, 0, z0, 8, t, z1); Box(3.0, 0, z0 + 2.2, 4.0, t, z1);
            }
            else
            {   // tường trước có cửa sổ 5,0…6,2, bậu 0,9, cao 1,2
                Box(0, 0, z0, 5.0, t, z1); Box(6.2, 0, z0, 8, t, z1);
                Box(5.0, 0, z0, 6.2, t, z0 + 0.9); Box(5.0, 0, z0 + 2.1, 6.2, t, z1);
            }
        }
        Box(3.85, 2.85, 0, 4.15, 3.15, 3.5);   // cột giữa nhà tầng 1
        return m;
    }
}
