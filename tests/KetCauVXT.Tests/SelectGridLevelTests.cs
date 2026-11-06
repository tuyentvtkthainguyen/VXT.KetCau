using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Mep;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Quét chọn mọi đối tượng (khung bao / khung cắt, Ctrl thêm, Shift bớt, bộ lọc, xoá nhóm), sửa lưới trục (vẽ 2 điểm,
/// tự đặt tên, dời kéo theo cấu kiện, đổi tên, xoá, đánh số lại), thêm / kéo cao độ tầng, mặt đứng 4 hướng có trục bong bóng,
/// thêm trục từ mặt đứng; lưới + quét chọn trên mặt bằng Triển khai hồ sơ.</summary>
public static class SelectGridLevelTests
{
    private static double B(bool b) => b ? 1 : 0;

    private static Project House()
    {
        var p = ModelBuilder.NewProject("Kiểm tra quét chọn – lưới trục – tầng");
        ModelBuilder.SetGrid(p, new[] { 5.0, 4.0 }, new[] { 4.5 });
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        WallAssembly.Upgrade(p);
        return p;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---------------- 1. quét chọn hỗn hợp ----------------
        var p = House();
        string st = p.Stories.OrderBy(s => s.Elevation).First().Name;
        string st2 = p.Stories.OrderBy(s => s.Elevation).Skip(1).First().Name;
        var w = new WallLine { Id = 901, Story = st, X1 = 0, Y1 = 0, X2 = 5, Y2 = 0, Type = "Gạch đặc 220 trát 2 mặt" };
        p.Walls.Add(w);
        WallAssembly.PlaceOpening(p, w, "D1 900x2200", 2.5);
        p.Arch.Furniture.Add(new FurnitureItem { Id = 1, Story = st, X = 2.5, Y = 2.0, Width = 1.0, Depth = 0.6 });
        p.Mep.Devices.Add(new MepDevice { Id = 1, Story = st, X = 7, Y = 2 });

        var small = SelectionRules.Box.Of(-0.2, -0.2, 0.2, 0.2);
        var win = MixedSelection.Select(p, st, small, false);
        var cr = MixedSelection.Select(p, st, small, true);
        var cw = MixedSelection.Counts(win); var cc = MixedSelection.Counts(cr);
        int Cnt(Dictionary<SelCat, int> d, SelCat c) => d.TryGetValue(c, out int n) ? n : 0;
        check("Quét chọn: khung bao quanh chân cột A-1 → 1 cột", Cnt(cw, SelCat.Cot), 1, 0);
        check("Quét chọn: khung bao không lấy dầm chỉ chạm khung", Cnt(cw, SelCat.Dam), 0, 0);
        check("Quét chọn: khung bao không lấy trục (chỉ chạm)", Cnt(cw, SelCat.Truc), 0, 0);
        check("Quét chọn: khung cắt lấy 2 dầm qua nút A-1", Cnt(cc, SelCat.Dam), 2, 0);
        check("Quét chọn: khung cắt lấy trục 1 và A", Cnt(cc, SelCat.Truc), 2, 0);
        check("Quét chọn: khung cắt lấy tường trục A", Cnt(cc, SelCat.Tuong), 1, 0);
        check("Quét chọn: khung bao ⊂ khung cắt", B(win.All(cr.Contains)), 1, 0);

        var big = SelectionRules.Box.Of(-0.5, -0.5, 9.5, 5.0);
        var all = MixedSelection.Select(p, st, big, false);
        var ca = MixedSelection.Counts(all);
        check("Quét chọn: khung bao cả nhà → 6 cột tầng 1", Cnt(ca, SelCat.Cot), 6, 0);
        check("Quét chọn: khung bao cả nhà → 7 dầm tầng 1", Cnt(ca, SelCat.Dam), 7, 0);
        check("Quét chọn: có cửa, đồ đạc, thiết bị", Cnt(ca, SelCat.Cua) * Cnt(ca, SelCat.DoDac) * Cnt(ca, SelCat.ThietBi), 1, 0);
        var other = MixedSelection.Select(p, st2, big, false);
        check("Quét chọn: tầng 2 không lấy tường / đồ đạc tầng 1", Cnt(MixedSelection.Counts(other), SelCat.Tuong) + Cnt(MixedSelection.Counts(other), SelCat.DoDac), 0, 0);
        var onlyCols = MixedSelection.Select(p, st, big, false, new HashSet<SelCat> { SelCat.Cot });
        check("Quét chọn: giới hạn loại = chỉ cột", onlyCols.Count, 6, 0);

        // Ctrl thêm / Shift bớt / bộ lọc / tóm tắt
        var add = MixedSelection.Combine(win, MixedSelection.Select(p, st, SelectionRules.Box.Of(4.8, -0.2, 5.2, 0.2), false), SelMode.Add);
        check("Ctrl + kéo: thêm cột B-2 → 2 cột", Cnt(MixedSelection.Counts(add), SelCat.Cot), 2, 0);
        var rem = MixedSelection.Combine(add, win, SelMode.Remove);
        check("Shift + kéo: bớt cột A-1 → 1 cột", rem.Count(i => i.Cat == SelCat.Cot), 1, 0);
        check("Ctrl + kéo không trùng lặp", MixedSelection.Combine(add, add, SelMode.Add).Count, add.Count, 0);
        var flt = MixedSelection.Filter(all, new HashSet<SelCat> { SelCat.Dam, SelCat.Tuong });
        check("Bộ lọc: chỉ còn dầm + tường", B(flt.All(i => i.Cat is SelCat.Dam or SelCat.Tuong) && flt.Count == 8), 1, 0);
        string sum = MixedSelection.Summary(flt);
        check("Tóm tắt: \"7 dầm, 1 tường\"", B(sum == "7 dầm, 1 tường"), 1, 0);
        check("Tóm tắt rỗng", B(MixedSelection.Summary(new List<PlanSelItem>()) == "không có đối tượng"), 1, 0);

        // xoá nhóm
        int frames0 = p.Frames.Count, walls0 = p.Walls.Count;
        int nDel = MixedSelection.Delete(p, MixedSelection.Filter(all, new HashSet<SelCat> { SelCat.Dam, SelCat.DoDac, SelCat.ThietBi, SelCat.Cua }));
        check("Xoá nhóm: 7 dầm + cửa + đồ đạc + thiết bị = 10", nDel, 10, 0);
        check("Xoá nhóm: còn cột, tường", B(p.Frames.Count == frames0 - 7 && p.Walls.Count == walls0 && w.Openings.Count == 0), 1, 0);
        check("Xoá nhóm: đồ đạc / thiết bị đã xoá", p.Arch.Furniture.Count + p.Mep.Devices.Count, 0, 0);

        // ---------------- 2. lưới trục ----------------
        p = House();
        check("Tên trục X kế tiếp = 4", B(GridOps.NextName(p, GridDir.X) == "4"), 1, 0);
        check("Tên trục Y kế tiếp = C", B(GridOps.NextName(p, GridDir.Y) == "C"), 1, 0);
        check("Tên chữ: Z → AA", B(GridOps.LetterName(26) == "AA" && GridOps.LetterIndex("AB") == 27), 1, 0);
        var d1 = GridOps.FromTwoPoints(12, -1, 12.1, 6);
        check("Vẽ trục 2 điểm gần đứng → trục X", B(d1 != null && d1.Value.Dir == GridDir.X), 1, 0);
        check("Vẽ trục 2 điểm: toạ độ trung bình", d1?.Coord ?? 0, 12.05, 1e-9);
        var d2 = GridOps.FromTwoPoints(-1, 7, 10, 7.2);
        check("Vẽ trục 2 điểm gần ngang → trục Y", B(d2 != null && d2.Value.Dir == GridDir.Y), 1, 0);
        check("Vẽ trục: 2 điểm trùng → không tạo", B(GridOps.FromTwoPoints(1, 1, 1.01, 1.01) == null), 1, 0);
        var g4 = GridOps.AddGrid(p, GridDir.X, 12);
        check("Thêm trục X: tên 4", B(g4.Name == "4"), 1, 0);
        int nG = p.Grids.Count;
        var again = GridOps.AddGrid(p, GridDir.X, 12.001);
        check("Thêm trục trùng toạ độ → không thêm", B(again == g4 && p.Grids.Count == nG), 1, 0);
        var gC = GridOps.OffsetCopy(p, p.GridsY.First(), 7.0);
        check("Sao chép lệch trục A 7 m → trục C tại 7", B(gC.Name == "C" && Math.Abs(gC.Coord - 7) < 1e-9), 1, 0);

        var g2 = GridOps.Find(p, "2");
        var (an, aw) = GridOps.Attached(p, g2);
        check("Trục 2: nút gắn trên trục (2 nút × 3 cao độ)", an, 6, 0);
        int moved = GridOps.MoveGrid(p, g2, 5.5, true);
        check("Dời trục 2 → 5,5 m kéo theo 6 nút", moved, 6, 0);
        check("Dời trục: nút cột dời theo", B(p.Nodes.Count(n => Math.Abs(n.X - 5.5) < 1e-9) == 6 && !p.Nodes.Any(n => Math.Abs(n.X - 5) < 1e-9)), 1, 0);
        check("Bước trục X sau khi dời = 5.5 3.5 3", B(GridOps.Spans(p, GridDir.X) == "5.5 3.5 3"), 1, 0);
        var g3 = GridOps.Find(p, "3");
        GridOps.MoveGrid(p, g3, 9.5, false);
        check("Dời trục không kéo: nút giữ nguyên", p.Nodes.Count(n => Math.Abs(n.X - 9) < 1e-9), 6, 0);
        check("Đổi tên trục 4 → 4a", B(GridOps.Rename(p, g4, "4a") && g4.Name == "4a"), 1, 0);
        check("Đổi tên trùng → từ chối", B(!GridOps.Rename(p, g4, "1")), 1, 0);
        int att = GridOps.DeleteGrid(p, g3);
        check("Xoá trục 3: báo 0 nút gắn (đã dời trục)", att, 0, 0);
        check("Xoá trục: cấu kiện giữ nguyên", p.Nodes.Count(n => Math.Abs(n.X - 9) < 1e-9), 6, 0);
        GridOps.Renumber(p);
        check("Đánh số lại: 1, 2, 3 / A, B, C", B(string.Join(",", p.GridsX.Select(g => g.Name)) == "1,2,3" && string.Join(",", p.GridsY.Select(g => g.Name)) == "A,B,C"), 1, 0);
        var del = MixedSelection.Select(p, null, SelectionRules.Box.Of(-3, 6.5, 20, 7.5), false, new HashSet<SelCat> { SelCat.Truc });
        check("Quét chọn trục C (khung bao, trục đứng chỉ chạm)", del.Count, 1, 0);
        MixedSelection.Delete(p, del);
        check("Xoá trục bằng quét chọn", p.GridsY.Count(), 2, 0);

        // ---------------- 3. cao độ tầng ----------------
        p = House();
        var ord = p.Stories.OrderBy(s => s.Elevation).ToList();
        int nSt = p.Stories.Count, cols0 = p.Frames.Count(f => f.Kind == FrameKind.Column);
        string nTop = LevelOps.AddLevel(p, ord[^1].Name, true, 3.0, LevelCopyMode.ColumnsOnly);
        var top = p.Stories.First(s => s.Name == nTop);
        check("Thêm tầng trên cùng: thêm 1 tầng", p.Stories.Count, nSt + 1, 0);
        check("Thêm tầng trên cùng: cao độ = mái + 3,0", top.Elevation, ord[^1].Elevation + 3.0, 1e-6);
        check("Thêm tầng trên cùng (chỉ cột): 6 cột mới", p.Frames.Count(f => f.Kind == FrameKind.Column) - cols0, 6, 0);
        check("Thêm tầng chỉ cột: không có dầm tầng mới", p.Frames.Count(f => f.Kind == FrameKind.Beam && f.Story == nTop), 0, 0);
        var p2 = House();
        var o2 = p2.Stories.OrderBy(s => s.Elevation).ToList();
        int beams0 = p2.Frames.Count(f => f.Kind == FrameKind.Beam);
        double topElev0 = o2[^1].Elevation;   // ghi lại trước khi chèn (Story là tham chiếu, sẽ bị nâng)
        string nMid = LevelOps.AddLevel(p2, o2[0].Name, true, 3.0, LevelCopyMode.Full);
        check("Chèn tầng giữa (chép toàn bộ): thêm dầm", B(p2.Frames.Count(f => f.Kind == FrameKind.Beam) > beams0), 1, 0);
        check("Chèn tầng giữa: tầng trên nâng 3,0 m", p2.Stories.OrderBy(s => s.Elevation).Last().Elevation, topElev0 + 3.0, 1e-6);
        var p3 = House();
        string nNone = LevelOps.AddLevel(p3, p3.Stories.OrderBy(s => s.Elevation).Last().Name, true, 3.2, LevelCopyMode.None);
        check("Thêm tầng không chép: không có cấu kiện tầng mới", p3.Frames.Count(f => f.Story == nNone), 0, 0);

        p = House();
        ord = p.Stories.OrderBy(s => s.Elevation).ToList();
        string s1 = ord[0].Name;
        double z0 = ord[0].Elevation;
        int nAt = p.Nodes.Count(n => Math.Abs(n.Z - z0) < 1e-6);
        LevelOps.SetElevation(p, s1, z0 + 0.3);
        check("Kéo cao độ tầng 1 lên 0,3 m: nút dời theo", p.Nodes.Count(n => Math.Abs(n.Z - (z0 + 0.3)) < 1e-6), nAt, 0);
        check("Kéo cao độ: chiều cao tầng 1 tăng 0,3", ord[0].Height, 3.9, 1e-6);
        check("Kéo cao độ: chiều cao tầng 2 giảm 0,3", ord[1].Height, 3.0, 1e-6);
        check("Chuỗi chiều cao tầng", B(LevelOps.Heights(p) == "3.9 3"), 1, 0);
        var (lo, hi) = LevelOps.Limits(p, s1);
        check("Giới hạn kéo: thấp nhất = chân cột + 0,5", lo, p.BaseElevation + 0.5, 1e-9);
        check("Giới hạn kéo: cao nhất = tầng trên − 0,5", hi, ord[1].Elevation - 0.5, 1e-9);
        bool threw = false;
        try { LevelOps.SetElevation(p, s1, ord[1].Elevation - 0.2); } catch (ArgumentException) { threw = true; }
        check("Kéo cao độ quá tầng trên → báo lỗi", B(threw), 1, 0);

        // ---------------- 4. mặt đứng ----------------
        p = House();
        st = p.Stories.OrderBy(s => s.Elevation).First().Name;
        var wf = new WallLine { Id = 1, Story = st, X1 = 0, Y1 = 0, X2 = 5, Y2 = 0, Type = "Gạch đặc 220 trát 2 mặt" };
        p.Walls.Add(wf);
        WallAssembly.PlaceOpening(p, wf, "D1 900x2200", 2.5);
        check("Mặt đứng trước: tên MẶT ĐỨNG TRỤC 1-3", B(BuildingElevation.Title(p, ElevationSide.Front) == "MẶT ĐỨNG TRỤC 1-3"), 1, 0);
        check("Mặt đứng trái: tên MẶT ĐỨNG TRỤC B-A", B(BuildingElevation.Title(p, ElevationSide.Left) == "MẶT ĐỨNG TRỤC B-A"), 1, 0);
        var ed = BuildingElevation.Draw(p, ElevationSide.Front, 100);
        check("Mặt đứng trước: 3 trục 1, 2, 3", ed.Axes.Count, 3, 0);
        check("Mặt đứng trước: các cao độ (chân + 2 tầng)", B(ed.Levels.Count >= 2), 1, 0);
        check("Mặt đứng trước: có cửa đi", B(ed.Items.Any(i => i.Category == "Cửa đi")), 1, 0);
        string dxf = ed.Figure.W.ToDxfStringDec();
        check("Mặt đứng DXF: có bong bóng trục (CIRCLE)", B(dxf.Contains("CIRCLE")), 1, 0);
        check("Mặt đứng DXF: có tiêu đề", B(dxf.Contains("MẶT ĐỨNG")), 1, 0);
        var edL = BuildingElevation.Draw(p, ElevationSide.Left, 100);
        check("Mặt đứng trái: 2 trục A, B", edL.Axes.Count, 2, 0);
        var sheets = BuildingElevation.Sheets(p, 100);
        check("Hồ sơ KT: 4 mặt đứng", sheets.Count, 4, 0);
        check("Hồ sơ KT: khoá KT:MD:", B(sheets.All(s => s.Key.StartsWith("KT:MD:"))), 1, 0);
        // thêm trục từ mặt đứng: u dọc đường nhìn trước (bắt đầu ở x = −1,5)
        var pc = BuildingElevation.PlanCoord(p, ElevationSide.Front, 1.5 + 7.0);
        check("Mặt đứng → mặt bằng: u ↦ x = 7", pc.Coord, 7.0, 1e-9);
        var gE = BuildingElevation.AddGridAt(p, ElevationSide.Front, 1.5 + 7.0);
        check("Thêm trục từ mặt đứng: trục X tên 4 tại 7 m", B(gE.Dir == GridDir.X && gE.Name == "4" && Math.Abs(gE.Coord - 7) < 1e-9), 1, 0);
        check("Mặt đứng sau khi thêm trục: 4 trục", BuildingElevation.Draw(p, ElevationSide.Front).Axes.Count, 4, 0);
        try
        {
            string dir = Path.Combine(outDir, "TrienKhai", "MatDung");
            Directory.CreateDirectory(dir);
            foreach (var s in sheets) s.W.Save(Path.Combine(dir, s.FileName));
        }
        catch (Exception) { }

        // ---------------- 5. mặt bằng Triển khai hồ sơ ----------------
        var pl = new StructuralPlan { SpansX = "3.6 4.2", SpansY = "4" };
        var lv = new PlanLevel { Name = "Tầng 2", Elevation = 3.6 };
        pl.Levels.Add(lv);
        lv.Items.Add(new PlanItem { Kind = PlanKinds.Column, X1 = 0, Y1 = 0, X2 = 0, Y2 = 0 });
        lv.Items.Add(new PlanItem { Kind = PlanKinds.Column, X1 = 3.6, Y1 = 0, X2 = 3.6, Y2 = 0 });
        lv.Items.Add(new PlanItem { Kind = PlanKinds.Beam, X1 = 0, Y1 = 0, X2 = 3.6, Y2 = 0 });
        lv.Items.Add(new PlanItem { Kind = PlanKinds.Beam, X1 = 3.6, Y1 = 0, X2 = 7.8, Y2 = 0 });
        check("Lưới hồ sơ: bước trục", B(PlanGridEdit.SpansOf(new[] { 0.0, 3.6, 7.8 }) == "3.6 4.2"), 1, 0);
        var sw = PlanGridEdit.Select(lv, -0.5, -0.5, 4.0, 0.5, false);
        check("Hồ sơ – khung bao: 2 cột + 1 dầm", sw.Count, 3, 0);
        var sc = PlanGridEdit.Select(lv, 4.0, 0.5, -0.5, -0.5, true);
        check("Hồ sơ – khung cắt: thêm dầm chạm khung", sc.Count, 4, 0);
        check("Hồ sơ – thêm trục 6 m", B(PlanGridEdit.Insert(pl, true, 6.0) && pl.SpansX == "3.6 2.4 1.8"), 1, 0);
        check("Hồ sơ – thêm trục trùng → từ chối", B(!PlanGridEdit.Insert(pl, true, 6.001)), 1, 0);
        check("Hồ sơ – xoá trục vừa thêm", B(PlanGridEdit.Remove(pl, true, 2) && pl.SpansX == "3.6 4.2"), 1, 0);
        int mv = PlanGridEdit.Move(pl, true, 1, 4.0, true);
        check("Hồ sơ – dời trục 2 → 4 m kéo theo cột + 2 đầu dầm", mv, 3, 0);
        check("Hồ sơ – bước trục sau dời", B(pl.SpansX == "4 3.8"), 1, 0);
        PlanGridEdit.Insert(pl, true, -1.0);
        check("Hồ sơ – thêm trục trước trục đầu: cấu kiện dời để trục đầu = 0", lv.Items[0].X1, 1.0, 1e-9);
        check("Hồ sơ – bước trục có trục mới đầu", B(pl.SpansX == "1 4 3.8"), 1, 0);
        var ga = PlanGridEdit.GridAt(pl, 1.05, 2.0, 0.2);
        check("Hồ sơ – bắt trục gần điểm bấm", B(ga != null && ga.Value.XDir && ga.Value.Index == 1), 1, 0);
        check("Hồ sơ – không xoá khi còn 2 trục", B(!PlanGridEdit.Remove(pl, false, 0)), 1, 0);
    }
}
