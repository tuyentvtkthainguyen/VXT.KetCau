using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Đặt đồ đạc bằng bắt điểm (mặt tường, góc phòng, giao trục, cạnh đồ đạc), kích thước tạm (tính + sửa → dời),
/// chọn trong 3D bằng tia, kéo trên mặt phẳng ngang, ẩn / cô lập, khung nhìn mặt cắt ngang tầng, mặt cắt đứng 1-1 / 2-2 tự sinh
/// (phần tử bị cắt, cao độ, chuỗi chiều cao tầng, trục), mặt cắt do người dùng vẽ, hồ sơ KT có mặt cắt.</summary>
public static class PlaceSnap3DSectionTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        static double B(bool v) => v ? 1 : 0;
        string dir = Path.Combine(outDir, "TrienKhai", "MatCat");
        Directory.CreateDirectory(dir);

        // ---------------- mô hình 3 tầng: lưới 2 nhịp 4 m × 1 nhịp 5 m ----------------
        var p = ModelBuilder.NewProject("Nhà 3 tầng – đặt đồ đạc, mặt cắt");
        ModelBuilder.SetGrid(p, new[] { 4.0, 4.0 }, new[] { 5.0 });
        Levels.ApplyWizard(p, new LevelWizardOptions { Floors = 3, FirstFloorHeight = 3.6, FloorHeight = 3.3, FootingTopDepth = 0.9, FoundationDepth = 1.5 });
        WallAssembly.Upgrade(p);
        const string wt = "Gạch đặc 220 trát 2 mặt";
        int nid = p.Walls.Count == 0 ? 1 : p.Walls.Max(w => w.Id) + 1;
        var w1 = new WallLine { Id = nid, Story = "T2", X1 = 0, Y1 = 5, X2 = 8, Y2 = 5, Type = wt };
        p.Walls.Add(w1);
        double half = ModelSolids.WallThickness(p, w1) / 2;
        check("Tường gạch 220 trát 2 mặt dày 250 mm", half * 2, 0.25, 1e-9);

        // ---------------- 1. bắt mặt tường ----------------
        var sn = FurnitureSnap.Snap(p, "T2", 4.0, 3.9, 1.8, 2.0, 0);
        check("Bắt mặt tường: kiểu 'Mặt tường'", B(sn.Kind == "Mặt tường"), 1, 0);
        check("Bắt mặt tường: lưng cách mặt tường 10 mm → Y = 5 − (0,125 + 0,01 + 1,0)", sn.Y, 5 - (half + FurnitureSnap.Gap + 1.0), 1e-9);
        check("Bắt mặt tường: X giữ theo con trỏ", sn.X, 4.0, 1e-9);
        check("Bắt mặt tường: tự xoay quay lưng vào tường (180°)", sn.Rotation, 180, 1e-9);
        check("Bắt mặt tường: tường tựa = w1", sn.WallId, w1.Id, 0);
        var snUp = FurnitureSnap.Snap(p, "T2", 4.0, 6.0, 1.8, 2.0, 0);
        check("Phía trên tường: Y = 5 + 1,135, góc 0°", snUp.Y + snUp.Rotation, 5 + half + FurnitureSnap.Gap + 1.0, 1e-9);
        var snCorner = FurnitureSnap.Snap(p, "T2", 1.0, 3.9, 1.8, 2.0, 0);
        check("Góc phòng (đầu tường): X = 0,9", snCorner.X, 0.9, 1e-9);
        check("Góc phòng: kiểu 'Góc phòng'", B(snCorner.Kind == "Góc phòng"), 1, 0);
        var w2 = new WallLine { Id = nid + 1, Story = "T2", X1 = 0, Y1 = 0, X2 = 0, Y2 = 5, Type = wt };
        p.Walls.Add(w2);
        var snCorner2 = FurnitureSnap.Snap(p, "T2", 1.2, 3.9, 1.8, 2.0, 0);
        check("Góc phòng có tường vuông góc: X = 0,9 + 0,125 + 0,01", snCorner2.X, 0.9 + half + FurnitureSnap.Gap, 1e-9);
        check("Góc phòng có tường vuông góc: vẫn tựa w1", snCorner2.WallId, w1.Id, 0);
        var snGrid = FurnitureSnap.Snap(p, "T3", 4.1, -0.1, 0.6, 0.6, 30);
        check("Giao trục (tầng không tường): (4; 0)", snGrid.X * 10 + snGrid.Y, 40, 1e-9);
        check("Giao trục: giữ góc xoay 30°", snGrid.Rotation, 30, 1e-9);
        var snFree = FurnitureSnap.Snap(p, "T3", 2.0, 2.5, 0.6, 0.6, 0);
        check("Tự do: giữ vị trí con trỏ", B(snFree.Kind == "Tự do" && Math.Abs(snFree.X - 2) < 1e-9 && Math.Abs(snFree.Y - 2.5) < 1e-9), 1, 0);

        // ---------------- 2. đặt + kích thước tạm ----------------
        var bed = new LibraryItem { Id = "giuong", Name = "Giường đôi", Category = "Nội thất", Width = 1.8, Depth = 2.0, Height = 0.5 };
        var f = FurnitureSnap.Place(p, bed, "T2", sn);
        check("Đặt: 1 đồ đạc trong mô hình", p.Arch.Furniture.Count, 1, 0);
        check("Đặt: toạ độ theo bắt điểm", f.Y, sn.Y, 1e-12);
        var dims = TempDimensions.ForRect(p, "T2", f.X, f.Y, f.Width, f.Depth, f.Rotation, true, f.Id);
        RectDim D(string d) => dims.FirstOrDefault(x => x.Dir == d);
        check("Kích thước tạm: đủ 4 hướng", dims.Count, 4, 0);
        check("Kích thước tạm +Y = khe 10 mm tới mặt tường", D("+Y")?.Value ?? -1, FurnitureSnap.Gap, 1e-9);
        check("Kích thước tạm +Y tham chiếu mặt tường", B(D("+Y")?.RefKind == "Mặt tường"), 1, 0);
        check("Kích thước tạm −X = 4 − 0,125 − 0,9 = 2,975 (mặt tường w2)", D("-X")?.Value ?? -1, 4 - half - 0.9, 1e-9);
        check("Kích thước tạm +X tới trục 3 = 8 − 4 − 0,9", D("+X")?.Value ?? -1, 3.1, 1e-9);
        check("Kích thước tạm +X tham chiếu trục 3", B(D("+X")?.RefKind == "Trục" && D("+X")?.RefName == "3"), 1, 0);
        check("Kích thước tạm −Y tới trục A = Y − 1,0", D("-Y")?.Value ?? -1, f.Y - 1.0, 1e-9);
        var dimsC = TempDimensions.ForRect(p, "T2", f.X, f.Y, f.Width, f.Depth, f.Rotation, false, f.Id);
        check("Kích thước tạm tới tim tường: +Y = 0,01 + 0,125", dimsC.First(x => x.Dir == "+Y").Value, FurnitureSnap.Gap + half, 1e-9);
        var dx = D("-X");
        check("Sửa kích thước tạm −X = 1000 mm: hợp lệ", B(TempDimensions.Apply(f, dx, 1.0)), 1, 0);
        check("Sửa kích thước tạm: X = 4 − 1,975", f.X, 4 - 1.975, 1e-9);
        var dims2 = TempDimensions.ForRect(p, "T2", f.X, f.Y, f.Width, f.Depth, f.Rotation, true, f.Id);
        check("Sau khi sửa: −X đúng 1000 mm", dims2.First(x => x.Dir == "-X").Value, 1.0, 1e-9);
        check("Sửa kích thước tạm âm: từ chối", B(TempDimensions.Apply(f, dims2[0], -0.1)), 0, 0);
        var mv = TempDimensions.MoveFor(new RectDim { Dir = "+Y", Value = 0.5 }, 0.2);
        check("Dời theo kích thước tạm +Y 500 → 200: dy = +0,3", mv.Dy + mv.Dx, 0.3, 1e-12);
        // cạnh đồ đạc
        var snNext = FurnitureSnap.Snap(p, "T3", 0, 0, 1, 1, 0);
        _ = snNext;
        var tb = FurnitureSnap.Place(p, new LibraryItem { Id = "ban", Name = "Bàn", Width = 1.2, Depth = 0.6, Height = 0.75 }, "T3", new SnapResult(2, 2.5, 0, "Tự do", 0));
        var snEdge = FurnitureSnap.Snap(p, "T3", 2.0 + 0.6 + 0.25 + 0.1, 2.5, 0.5, 0.5, 0);
        check("Cạnh đồ đạc: đặt sát cạnh phải bàn (X = 2 + 0,6 + 0,25)", snEdge.X, 2.85, 1e-9);
        check("Cạnh đồ đạc: kiểu 'Cạnh đồ đạc'", B(snEdge.Kind == "Cạnh đồ đạc"), 1, 0);
        _ = tb;

        // ---------------- 3. 3D: tia, chọn, kéo, ẩn / cô lập ----------------
        var r0 = Pick3DMath.CameraRay(new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 0, 1), 90, 200, 100, 100, 50);
        check("Tia camera giữa khung = hướng nhìn", r0.D.X, 1, 1e-12);
        var r1 = Pick3DMath.CameraRay(new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 0, 1), 90, 200, 100, 200, 50);
        check("Tia mép phải (FOV ngang 90°): lệch 45° sang −Y", r1.D.Y, -Math.Sqrt(0.5), 1e-12);
        var r2 = Pick3DMath.CameraRay(new Vec3(0, 0, 0), new Vec3(1, 0, 0), new Vec3(0, 0, 1), 90, 200, 100, 100, 0);
        check("Tia mép trên: Z = tan45·h/w / chuẩn", r2.D.Z, 0.5 / Math.Sqrt(1.25), 1e-12);
        var solids = ModelSolids.Build(p);
        double zf = ModelSolids.StoryZ(p, "T2");
        var down = new Ray3(new Vec3(f.X, f.Y, zf + 2.0), new Vec3(0, 0, -1));   // dưới sàn tầng 3
        var hit = Pick3DMath.Pick(down, solids);
        check("Chọn 3D từ trên xuống: trúng giường (DD-)", B(hit?.Id == "DD-" + f.Id), 1, 0);
        check("Khoảng cách tới mặt giường = 2,0 − 0,5", Pick3DMath.Hit(down, hit) ?? -1, 1.5, 1e-9);
        var miss = new Ray3(new Vec3(-50, -50, 30), new Vec3(0, 0, -1));
        check("Tia ngoài công trình: không trúng", B(Pick3DMath.Pick(miss, solids) == null), 1, 0);
        var beamY = p.Frames.First(fr => fr.Kind == FrameKind.Beam && fr.Story == "T2" && Math.Abs(p.Node(fr.NodeI).X - p.Node(fr.NodeJ).X) < 1e-9 && Math.Abs(p.Node(fr.NodeI).X - 8) < 1e-9);
        var side = new Ray3(new Vec3(20, 2.5, 3.5), new Vec3(-1, 0, 0));
        var hb = Pick3DMath.Pick(side, solids, s => s.Category == "Dầm");
        check("Tia ngang trúng dầm trục 3 (khối xoay theo dầm)", B(hb?.Id == "DAM-" + beamY.Id), 1, 0);
        var pz = Pick3DMath.OnPlaneZ(new Ray3(new Vec3(1, 1, 10), new Vec3(0, 0.6, -0.8)), 2);
        check("Giao mặt phẳng z = 2: y = 1 + 0,6·10", pz?.Y ?? -1, 7, 1e-9);
        var drag = Pick3DMath.Drag(new Ray3(new Vec3(0, 0, 10), new Vec3(0, 0, -1)), new Ray3(new Vec3(1, 2, 10), new Vec3(0, 0, -1)), 0, 5, 5);
        check("Kéo trên sàn: (5;5) + (1;2)", (drag?.X ?? 0) * 10 + (drag?.Y ?? 0), 67, 1e-9);
        check("Tia song song mặt phẳng: không kéo", B(Pick3DMath.OnPlaneZ(new Ray3(new Vec3(0, 0, 1), new Vec3(1, 0, 0)), 0) == null), 1, 0);
        var flt = new View3DFilter();
        flt.Isolate(new[] { "DD-1" });
        check("Cô lập: chỉ DD-1 hiện", B(flt.Visible("DD-1") && !flt.Visible("DAM-3")), 1, 0);
        flt.Hide(new[] { "DD-1" });
        check("Ẩn đối tượng đang cô lập: hết cô lập", B(!flt.Visible("DD-1") && !flt.IsIsolating && flt.Visible("DAM-3")), 1, 0);
        flt.Reset();
        check("Hiện tất cả", B(flt.Visible("DD-1")), 1, 0);

        // ---------------- 4. khung nhìn mặt cắt ngang tầng ----------------
        var views = LevelViews.Build(p);
        check("Mặt cắt ngang tầng: (nền + 3 sàn) × 3 bộ môn = 12", views.Count, 12, 0);
        check("Mặt cắt ngang tầng: có KT tầng 1 ±0.000", B(views.Any(v => v.Key == "PV:KT:NEN" && v.Name.Contains("±0.000"))), 1, 0);
        check("Mặt cắt ngang tầng: có DN sàn mái", B(views.Any(v => v.Key == "PV:DN:MAI")), 1, 0);
        check("Mặt phẳng cắt cao 1,2 m", views[0].CutHeight, 1.2, 0);
        var q = p.Clone();
        Levels.InsertStory(q, "T2", 3.0);
        check("Chèn tầng → khung nhìn tự sinh lại = 15", LevelViews.Build(q).Count, 15, 0);

        // ---------------- 5. mặt cắt đứng ----------------
        var defs = SectionView.Defaults(p);
        check("Mặt cắt tự sinh: 1-1 và 2-2", B(defs.Count == 2 && defs[0].Name == "1-1" && defs[1].Name == "2-2"), 1, 0);
        check("1-1 qua giữa nhịp A–B (y = 2,5)", defs[0].Y1, 2.5, 1e-9);
        check("2-2 qua giữa nhịp 1–2 (x = 2)", defs[1].X1, 2.0, 1e-9);
        var s11 = SectionView.Draw(p, defs[0]);
        int nBeams = s11.Items.Count(i => i.Category == "Dầm");
        check("1-1: cắt 3 dầm trục 1, 2, 3 × 3 tầng = 9", nBeams, 9, 0);
        check("1-1: cắt 6 ô sàn khung", s11.Items.Count(i => i.Category == "Sàn"), 6, 0);
        check("1-1: cắt 2 ô sàn nền", s11.Items.Count(i => i.Category == "Sàn nền"), 2, 0);
        var fsB = p.FrameSections.First(s => s.Name == beamY.Section);
        var cutB = s11.Items.First(i => i.Id == "DAM-" + beamY.Id);
        check("1-1: dầm bị cắt rộng b", cutB.U1 - cutB.U0, fsB.B, 1e-9);
        check("1-1: dầm bị cắt cao h", cutB.Z1 - cutB.Z0, fsB.H, 1e-9);
        check("1-1: đỉnh dầm tại cao độ sàn T2", cutB.Z1, zf, 1e-9);
        check("1-1: dầm trục 3 tại u = 1,5 + 8 (tâm)", (cutB.U0 + cutB.U1) / 2, 9.5, 1e-9);
        var slab = s11.Items.First(i => i.Category == "Sàn");
        var sl0 = p.Slabs.First(s => "SAN-" + s.Id == slab.Id);
        double t = p.SlabSections.First(s => s.Name == sl0.Section).Thickness;
        check("1-1: sàn bị cắt dày t", slab.Z1 - slab.Z0, t, 1e-9);
        check("1-1: 4 cao độ (nền, T2, T3, mái)", s11.Levels.Count, 4, 0);
        check("1-1: tổng chuỗi chiều cao tầng = chiều cao công trình 10,2", s11.Levels.Zip(s11.Levels.Skip(1), (a, b) => b - a).Sum(), 10.2, 1e-9);
        check("1-1: trục 1, 2, 3 cắt qua", s11.Axes.Count, 3, 0);
        check("1-1: kích thước trục 1–2 = 4 m", s11.Axes[1].U - s11.Axes[0].U, 4, 1e-9);
        string txt = s11.Figure.W.ToDxfStringDec();
        check("1-1: tiêu đề MẶT CẮT 1-1", B(txt.Contains("MẶT CẮT 1-1")), 1, 0);
        check("1-1: ký hiệu cao độ ±0.000 và +3.600, +10.200", B(txt.Contains("±0.000") && txt.Contains("+3.600") && txt.Contains("+10.200")), 1, 0);
        check("1-1: ghi đáy móng −1.500", B(txt.Contains("-1.500")), 1, 0);
        check("1-1: kích thước tầng 3600 và 3300", B(txt.Contains("3600") && txt.Contains("3300")), 1, 0);
        s11.Figure.W.Save(Path.Combine(dir, "MatCat_1_1.dxf"));
        // 2-2 qua ô cửa đi trên tường w1
        var rd = WallAssembly.PlaceOpening(p, w1, "D1 900x2200", 2.0);
        check("Đặt cửa đi D1 tại giữa nhịp 1–2", B(rd.Ok), 1, 0);
        var s22 = SectionView.Draw(p, defs[1]);
        var wallCuts = s22.Items.Where(i => i.Id == "TUONG-" + w1.Id).ToList();
        double hw = WallSolid3D.DefaultHeight(p, w1);
        check("2-2: tường qua ô cửa còn 1 khối (trên đầu cửa)", wallCuts.Count, 1, 0);
        check("2-2: khối tường bắt đầu từ đầu cửa (+3,6 + 2,2)", wallCuts.FirstOrDefault().Z0, zf + 2.2, 1e-9);
        check("2-2: khối tường tới đỉnh tường", wallCuts.FirstOrDefault().Z1, zf + hw, 1e-9);
        check("2-2: có cửa đi bị cắt", s22.Items.Count(i => i.Category == "Cửa đi"), 1, 0);
        check("2-2: trục A, B", B(s22.Axes.Count == 2 && s22.Axes[0].Name == "A"), 1, 0);
        check("2-2: kích thước trục A–B = 5 m", s22.Axes[1].U - s22.Axes[0].U, 5, 1e-9);
        check("2-2: không có tường w2 (song song, ngoài mặt cắt)", s22.Items.Count(i => i.Id == "TUONG-" + w2.Id), 0, 0);
        s22.Figure.W.Save(Path.Combine(dir, "MatCat_2_2.dxf"));

        // ---------------- 6. mặt cắt do người dùng vẽ ----------------
        check("Tên mặt cắt kế tiếp 3-3", B(SectionView.NextName(p) == "3-3"), 1, 0);
        check("2 điểm trùng: không tạo", B(SectionView.Add(p, 1, 1, 1.01, 1) == null), 1, 0);
        var us = SectionView.Add(p, -1, 0, 9, 0);
        check("Vẽ mặt cắt 2 điểm: tạo 3-3", B(us?.Name == "3-3" && p.Arch.Sections.Count == 1), 1, 0);
        var s33 = SectionView.Draw(p, us);
        check("3-3 (dọc trục A): cắt cột trục A (3 cột × 3 tầng)", s33.Items.Count(i => i.Category == "Cột"), 9, 0);
        check("Mọi mặt cắt = 3", SectionView.All(p).Count, 3, 0);
        string json = System.Text.Json.JsonSerializer.Serialize(p, Project.JsonOptions);
        var p2 = System.Text.Json.JsonSerializer.Deserialize<Project>(json, Project.JsonOptions);
        check("Lưu / mở: giữ mặt cắt người dùng vẽ", p2.Arch.Sections.Count, 1, 0);
        s33.Figure.W.Save(Path.Combine(dir, "MatCat_3_3.dxf"));

        // ---------------- 7. hồ sơ kiến trúc có mặt cắt ----------------
        var kt = DisciplineDocs.Build(p, null, new DisciplineDocOptions { Disciplines = new() { Discipline.KienTruc } });
        check("Hồ sơ KT: có tờ MẶT CẮT 1-1, 2-2, 3-3", kt.Count(i => i.Key.StartsWith("KT:MC:")), 3, 0);
        check("Hồ sơ KT: tờ mặt cắt đánh số KT-", B(kt.Where(i => i.Key.StartsWith("KT:MC:")).All(i => i.Number.StartsWith("KT-"))), 1, 0);
    }
}
