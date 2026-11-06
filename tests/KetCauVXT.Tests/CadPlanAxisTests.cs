using System.Globalization;
using System.Text;
using KetCauVXT.Detailing;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Mở mặt bằng CAD lấy dữ liệu (Import/Arch/PlanAxisPicker.cs): nguồn trục theo layer / theo cột / người dùng chọn,
/// trục qua TIM hay MÉP cột (lệch tâm ±b/2, "mép ngoài nhà"), đơn vị cm, bước trục, tiết diện cột, tường 110/220 trên trục,
/// tên phòng → công năng ô sàn, lỗ cầu thang; áp dụng vào mô hình (lưới, tiết diện + lệch tâm cột, tường, công năng, lỗ sàn)
/// và mặt bằng kết cấu hồ sơ. Bản vẽ DXF tổng hợp ghi ra outDir/NhapMatBang/*_DauVao.dxf.</summary>
public static class CadPlanAxisTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static double B(bool b) => b ? 1 : 0;

    private sealed class Dxf
    {
        private readonly StringBuilder _sb = new();
        private void P(int code, object v) => _sb.Append(code.ToString(Inv)).Append('\n').Append(v is double d ? d.ToString("R", Inv) : Convert.ToString(v, Inv)).Append('\n');
        public Dxf(int insUnits)
        {
            P(0, "SECTION"); P(2, "HEADER"); P(9, "$ACADVER"); P(1, "AC1009"); P(9, "$INSUNITS"); P(70, insUnits); P(0, "ENDSEC");
            P(0, "SECTION"); P(2, "ENTITIES");
        }
        public void Line(string layer, double x1, double y1, double x2, double y2)
        { P(0, "LINE"); P(8, layer); P(10, x1); P(20, y1); P(30, 0.0); P(11, x2); P(21, y2); P(31, 0.0); }
        public void Circle(string layer, double cx, double cy, double r)
        { P(0, "CIRCLE"); P(8, layer); P(10, cx); P(20, cy); P(30, 0.0); P(40, r); }
        public void Text(string layer, double x, double y, double h, string s)
        { P(0, "TEXT"); P(8, layer); P(10, x); P(20, y); P(30, 0.0); P(40, h); P(1, s); }
        public void Rect(string layer, double cx, double cy, double bx, double by)
        {
            P(0, "LWPOLYLINE"); P(8, layer); P(90, 4); P(70, 1);
            foreach (var (x, y) in new[] { (cx - bx / 2, cy - by / 2), (cx + bx / 2, cy - by / 2), (cx + bx / 2, cy + by / 2), (cx - bx / 2, cy + by / 2) }) { P(10, x); P(20, y); }
        }
        public string End() { P(0, "ENDSEC"); P(0, "EOF"); return _sb.ToString(); }
    }

    private static readonly double[] Xs = { 0, 4.2, 7.8 }, Ys = { 0, 4.5, 9.0 };

    /// <summary>Mặt bằng đã đọc: 3×3 cột 220 (X) × 300 (Y). edge = true: trục biên qua MÉP NGOÀI cột, trục giữa qua tim.</summary>
    private static ArchFloorPlan Plan(bool withGrids, bool edge)
    {
        var p = new ArchFloorPlan { Title = "Mặt bằng tầng 1", FloorNo = 1 };
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                double ox = edge ? (i == 0 ? 0.11 : i == 2 ? -0.11 : 0) : 0, oy = edge ? (j == 0 ? 0.15 : j == 2 ? -0.15 : 0) : 0;
                p.Columns.Add(new ArchColumn { X = Xs[i] + ox, Y = Ys[j] + oy, Bx = 0.22, By = 0.30 });
            }
        if (withGrids)
        {
            for (int i = 0; i < 3; i++) p.Grids.Add(new ArchGrid { Name = (i + 1).ToString(Inv), Dir = GridDir.X, Coord = Xs[i] });
            for (int j = 0; j < 3; j++) p.Grids.Add(new ArchGrid { Name = ((char)('A' + j)).ToString(), Dir = GridDir.Y, Coord = Ys[j] });
        }
        // tường: bao 220 trên trục 1 (tim trùng trục); tường 110 tim trên trục B (0 → 4,2); tường 110 mặt trùng trục 2 (tim lệch 55)
        p.Walls.Add(new ArchWall { Ax = 0, Ay = 0, Bx = 0, By = 9.0, Thickness = 0.22, Exterior = true });
        p.Walls.Add(new ArchWall { Ax = 0, Ay = 4.5, Bx = 4.2, By = 4.5, Thickness = 0.11 });
        p.Walls.Add(new ArchWall { Ax = 4.255, Ay = 0, Bx = 4.255, By = 4.5, Thickness = 0.11 });
        p.Walls.Add(new ArchWall { Ax = 5.5, Ay = 6.0, Bx = 5.5, By = 7.5, Thickness = 0.11 });   // tường không nằm trên trục
        // phòng: WC ô (1-2, A-B); ban công ô (2-3, B-C); thang ô (2-3, A-B)
        p.Rooms.Add(new ArchRoom { Text = "WC", Kind = RoomKind.WC, X = 2.1, Y = 2.25 });
        p.Rooms.Add(new ArchRoom { Text = "BAN CÔNG", Kind = RoomKind.Balcony, X = 6.0, Y = 6.75 });
        p.Zones.Add(new ArchZone { Kind = RoomKind.Stair, X0 = 4.4, Y0 = 0.3, X1 = 7.6, Y1 = 4.2, Note = "lớp cầu thang" });
        return p;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ================= 1. đơn vị =================
        check("Đơn vị: $INSUNITS = 5 → cm (×0,01)", PlanAxisPicker.UnitFactor(5, 900), 0.01, 1e-12);
        check("Đơn vị: không khai báo, phạm vi 15000 → mm", PlanAxisPicker.UnitFactor(0, 15000), 0.001, 1e-12);
        check("Đơn vị: không khai báo, phạm vi 900 → cm", PlanAxisPicker.UnitFactor(0, 900), 0.01, 1e-12);
        check("Đơn vị: không khai báo, phạm vi 30 → m", PlanAxisPicker.UnitFactor(0, 30), 1, 1e-12);
        var pcm = Plan(true, false); PlanAxisPicker.Rescale(pcm, 100);   // giả bản vẽ đọc nhầm thành m trong khi vẽ cm
        PlanAxisPicker.Rescale(pcm, 0.01);
        check("Đổi đơn vị: ×100 rồi ×0,01 → cột 220 / trục 7,8 m giữ nguyên", pcm.Columns[0].Bx * 1000 + pcm.Grids.First(g => g.Dir == GridDir.X && g.Name == "3").Coord, 220 + 7.8, 1e-6);

        // ================= 2. trục theo layer, trục qua tim cột =================
        var o = new AxisPickOptions { Source = AxisSource.Layer };
        var r = PlanAxisPicker.Read(Plan(true, false), o);
        check("Theo layer: 3 trục X, 3 trục Y", r.AxesX.Count * 10 + r.AxesY.Count, 33, 0);
        check("Theo layer: bước X = 4,2 3,6; bước Y = 2*4,5", B(r.SpansXText == "4.2 3.6" && r.SpansYText == "4.5 4.5"), 1, 0);
        check("Theo layer: giữ tên trục 1, 2, 3 / A, B, C", B(string.Join("", r.AxesX.Select(a => a.Name)) == "123" && string.Join("", r.AxesY.Select(a => a.Name)) == "ABC"), 1, 0);
        check("Cột: 9 cột trên giao trục, lệch tâm 0", r.Columns.Count(c => c.OnGrid) * 10 + r.OffsetColumns, 90, 0);
        check("Cột: tiết diện C30x22 (b theo Y × h theo X)", B(r.Columns.All(c => c.Section == "C30x22")), 1, 0);

        // ================= 3. trục qua mép ngoài cột (trục biên) – lệch tâm ±b/2 =================
        var re = PlanAxisPicker.Read(Plan(true, true), o);
        var c00 = re.Columns.First(c => c.IX == 0 && c.IY == 0); var c22 = re.Columns.First(c => c.IX == 2 && c.IY == 2); var c11 = re.Columns.First(c => c.IX == 1 && c.IY == 1);
        check("Trục qua mép: cột góc 1-A lệch +110 / +150 mm", c00.OffX * 1000 + c00.OffY, 110 + 0.15, 1e-6);
        check("Trục qua mép: cột góc 3-C lệch −110 / −150 mm", c22.OffX * 1000 + c22.OffY, -110 - 0.15, 1e-6);
        check("Trục qua mép: cột giữa 2-B lệch 0", Math.Abs(c11.OffX) + Math.Abs(c11.OffY), 0, 1e-9);
        check("Trục qua mép: 8 cột lệch tâm (trừ cột giữa)", re.OffsetColumns, 8, 0);

        // ================= 4. trục lập theo cột =================
        var pe = Plan(false, true);
        var rc = PlanAxisPicker.Read(pe, new AxisPickOptions { Source = AxisSource.Columns, Through = AxisThrough.Outside });
        check("Theo cột + mép ngoài nhà: trục 0 / 4,2 / 7,8 và 0 / 4,5 / 9,0", rc.AxesX.Sum(a => a.Coord) + rc.AxesY.Sum(a => a.Coord) * 10, 12.0 + 135.0, 1e-6);
        check("Theo cột + mép ngoài nhà: tên trục tự động 1,2,3 / A,B,C", B(string.Join("", rc.AxesX.Select(a => a.Name)) + string.Join("", rc.AxesY.Select(a => a.Name)) == "123ABC"), 1, 0);
        check("Theo cột + mép ngoài nhà: trục giữa qua tim (Through = Center)", B(rc.AxesX[1].Through == AxisThrough.Center && rc.AxesX[0].Through == null), 1, 0);
        check("Theo cột + mép ngoài nhà: lệch tâm cột góc +110 mm", rc.Columns.First(c => c.IX == 0 && c.IY == 0).OffX, 0.11, 1e-9);
        var rcc = PlanAxisPicker.Read(pe, new AxisPickOptions { Source = AxisSource.Columns, Through = AxisThrough.Center });
        check("Theo cột + tim cột: bước X = 4,09 3,49 (trục qua tim cột biên lệch)", B(rcc.SpansXText == "4.09 3.49"), 1, 0);
        check("Theo cột + tim cột: không cột nào lệch tâm", rcc.OffsetColumns, 0, 0);
        var rmin = PlanAxisPicker.Read(Plan(false, false), new AxisPickOptions { Source = AxisSource.Columns, Through = AxisThrough.EdgeMin });
        check("Theo cột + mép trái/dưới: trục X đầu tại −110 mm, mọi cột lệch +110 mm", rmin.AxesX[0].Coord * 1000 + (rmin.Columns.All(c => Math.Abs(c.OffX - 0.11) < 1e-9 && Math.Abs(c.OffY - 0.15) < 1e-9) ? 1000 : 0), -110 + 1000, 1e-6);
        check("Gom cột thẳng hàng (sai số 50 mm)", PlanAxisPicker.Cluster(new List<double> { 0, 0.02, 4.2, 4.23, 7.8 }, 0.05).Count, 3, 0);

        // ================= 5. người dùng chọn trục =================
        var ua = new List<PickedAxis>();
        check("Chọn: nét gần đứng → trục X tại x = 1,0", B(PlanAxisPicker.AddAxis(ua, PlanAxisPicker.AxisFromSegment(1.0, -1, 1.02, 10)) && ua[0].Dir == GridDir.X && Math.Abs(ua[0].Coord - 1.01) < 1e-9), 1, 0);
        var col = PlanAxisPicker.ColumnAt(pe.Columns, 0.12, 0.10);
        check("Chọn: bấm cột 1-A tìm đúng cột", B(col != null && Math.Abs(col.X - 0.11) < 1e-9), 1, 0);
        PlanAxisPicker.AddAxis(ua, PlanAxisPicker.AxisThroughColumn(col, GridDir.X, AxisThrough.Outside, pe.Columns));
        PlanAxisPicker.AddAxis(ua, PlanAxisPicker.AxisThroughColumn(pe.Columns.First(c => c.X > 7), GridDir.X, AxisThrough.Outside, pe.Columns));
        check("Chọn: trục qua mép ngoài cột 1-A tại x = 0; cột 3 tại x = 7,8", B(ua.Any(a => Math.Abs(a.Coord) < 1e-9) && ua.Any(a => Math.Abs(a.Coord - 7.8) < 1e-9)), 1, 0);
        check("Chọn: trục trùng (sai số 50 mm) bị bỏ", B(!PlanAxisPicker.AddAxis(ua, new PickedAxis { Dir = GridDir.X, Coord = 0.03 })), 1, 0);
        ua.RemoveAll(a => Math.Abs(a.Coord - 1.01) < 1e-9);
        ua.Add(new PickedAxis { Dir = GridDir.Y, Coord = 0 }); ua.Add(new PickedAxis { Dir = GridDir.Y, Coord = 9.0 });
        var ru = PlanAxisPicker.Read(pe, new AxisPickOptions { Source = AxisSource.User }, ua);
        check("Chọn: bước X = 7,8 (2 trục), bước Y = 9", B(ru.SpansXText == "7.8" && ru.SpansYText == "9"), 1, 0);
        check("Tên trục Y thứ 27 = AA", B(PlanAxisPicker.LetterName(26) == "AA" && PlanAxisPicker.LetterName(0) == "A"), 1, 0);
        var ren = new List<PickedAxis> { new() { Dir = GridDir.X, Coord = 5, Name = "X5" }, new() { Dir = GridDir.X, Coord = 1 } };
        PlanAxisPicker.AutoName(ren, true);
        check("Đặt tên: giữ tên người dùng X5, trục chưa tên → 1", B(ren[0].Name == "1" && ren[1].Name == "X5"), 1, 0);

        // ================= 6. tường, công năng, lỗ sàn =================
        double w220 = r.Walls.Where(w => Math.Abs(w.Thickness - 0.22) < 1e-9).Sum(w => w.Length), w110 = r.Walls.Where(w => Math.Abs(w.Thickness - 0.11) < 1e-9).Sum(w => w.Length);
        check("Tường trên trục: 220 dài 9,0 m (trục 1), ngoài nhà", w220 + (r.Walls.Any(w => w.Exterior && w.AxisDir == GridDir.X && w.Axis == 0) ? 100 : 0), 109.0, 1e-6);
        check("Tường trên trục: 110 dài 4,2 + 4,5 m (mặt tường trùng trục 2 vẫn nhận)", w110, 8.7, 1e-6);
        check("Tường ngoài trục không lấy", r.Walls.Count, 3, 0);
        var bWc = r.Bays.First(b => b.IX == 0 && b.IY == 0); var bSt = r.Bays.First(b => b.IX == 1 && b.IY == 0); var bBc = r.Bays.First(b => b.IX == 1 && b.IY == 1);
        check("Phòng WC → ô sàn vệ sinh", B(bWc.Use == SlabUse.WC && bWc.Opening == null), 1, 0);
        check("Ban công → ô sàn ban công", B(bBc.Use == SlabUse.Balcony), 1, 0);
        check("Vùng cầu thang → lỗ sàn thang", B(bSt.Opening == OpeningKind.Stair), 1, 0);
        check("Bảng tóm tắt: 5 hạng mục", r.Summary().Count, 5, 0);

        // ================= 7. áp dụng vào mô hình =================
        var p = ModelBuilder.NewProject("Nhập mặt bằng CAD");
        ModelBuilder.SetGrid(p, re.SpansX, re.SpansY);
        ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x22", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        int nSlab0 = p.Slabs.Count;
        var log = PlanAxisPicker.ApplyToProject(p, re, new AxisApplyOptions());
        var cols = p.Frames.Where(f => f.IsVerticalKind).ToList();
        check("Áp dụng: mọi cột tiết diện C30x22 (tạo mới)", B(cols.Count > 0 && cols.All(f => f.Section == "C30x22") && p.FrameSections.Any(s => s.Name == "C30x22")), 1, 0);
        var f00 = cols.First(f => Math.Abs(p.Node(f.NodeI).X) < 1e-6 && Math.Abs(p.Node(f.NodeI).Y) < 1e-6);
        check("Áp dụng: cột 1-A lệch tâm +110 / +150 mm (mọi tầng)", f00.ColOffsetX * 1000 + f00.ColOffsetY, 110.15, 1e-6);
        check("Áp dụng: số thanh cột lệch tâm = 8 × số tầng", cols.Count(f => Math.Abs(f.ColOffsetX) + Math.Abs(f.ColOffsetY) > 1e-9), 8 * p.Stories.Count, 0);
        check("Áp dụng: tường 3 đoạn ở tầng điển hình (trừ mái)", p.Walls.Count, 3, 0);
        check("Áp dụng: tường 220 dùng loại 'Tường 220 trát 2 mặt'", B(p.Walls.Any(w => w.Type.Contains("220") && Math.Abs(w.Length - 9.0) < 1e-6)), 1, 0);
        var st0 = p.Stories.OrderBy(s => s.Elevation).First().Name;
        var sWc = ModelEdit.SlabAt(p, st0, 2.1, 2.25);
        check("Áp dụng: ô 1-2/A-B công năng vệ sinh", B(sWc != null && ModelEdit.UseOf(p, sWc) == SlabUse.WC), 1, 0);
        check("Áp dụng: lỗ cầu thang ô 2-3/A-B (mất 1 ô sàn)", B(p.Openings.Count == 1 && p.Openings[0].Kind == OpeningKind.Stair && p.Slabs.Count == nSlab0 - 1), 1, 0);
        check("Áp dụng: nhật ký có lưới, cột, lệch tâm, tường, sàn", log.Count, 6, 0);
        var gNo = PlanAxisPicker.ApplyToProject(ModelBuilder.NewProject(), re);
        check("Áp dụng: mô hình chưa dựng lưới → báo, không đổi", B(gNo.Count == 1 && gNo[0].Contains("Số trục")), 1, 0);
        var rect = FramingPlanProbe(p, f00);
        check("Vẽ mặt bằng: tâm hình cột 1-A tại (110; 150) mm", rect.X + rect.Y / 1000, 110 + 0.15, 1e-6);
        var p2 = ModelBuilder.NewProject();
        ModelBuilder.SetGrid(p2, re.SpansX, re.SpansY); ModelBuilder.SetStories(p2, new[] { 3.6 }, -0.6);
        ModelBuilder.BuildFromGrid(p2, new ModelBuilder.GridFrameOptions());
        var pc = Plan(true, true); pc.Columns.RemoveAll(c => c.X > 4 && c.X < 5 && c.Y > 4 && c.Y < 5);   // bỏ cột giữa 2-B
        int nc0 = p2.Frames.Count(f => f.IsVerticalKind);
        PlanAxisPicker.ApplyToProject(p2, PlanAxisPicker.Read(pc, o), new AxisApplyOptions { RemoveColumnsWithoutCad = true, Walls = false });
        check("Áp dụng: xoá cột ở giao trục không có cột trên bản vẽ (2-B)", nc0 - p2.Frames.Count(f => f.IsVerticalKind), 1, 0);
        check("Lệch tâm cột lưu JSON", B(System.Text.Json.JsonSerializer.Serialize(f00).Contains("\"ColOffsetX\":0.11")), 1, 0);

        // ================= 8. mặt bằng kết cấu hồ sơ =================
        var dp = new DetailingProject();
        var lv = dp.Plan.Levels.First();
        var dl = PlanAxisPicker.ApplyToDetailing(dp, lv, re);
        var cItems = lv.Items.Where(i => i.Kind == PlanKinds.Column).ToList();
        check("Hồ sơ: bước trục X / Y theo bản vẽ", B(dp.Plan.SpansX == "4.2 3.6" && dp.Plan.SpansY == "4.5 4.5"), 1, 0);
        check("Hồ sơ: 9 cột đặt trên mặt bằng, cột 1-A tại (0,11; 0,15)", cItems.Count * 100 + (cItems.Any(i => Math.Abs(i.X1 - 0.11) < 1e-9 && Math.Abs(i.Y1 - 0.15) < 1e-9) ? 1 : 0), 901, 0);
        var mc = dp.Columns.FirstOrDefault(q => Math.Abs(q.B - 0.22) < 1e-9 && Math.Abs(q.H - 0.30) < 1e-9);
        check("Hồ sơ: một cấu kiện cột 220×300 dùng chung", B(mc != null && cItems.All(i => i.Member == mc.Name)), 1, 0);
        check("Hồ sơ: nhật ký 2 dòng", dl.Count, 2, 0);

        // ================= 9. DXF tổng hợp (đơn vị cm) qua bộ đọc kiến trúc =================
        string dir = Path.Combine(outDir, "NhapMatBang");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "MatBang_TrucCot_cm_DauVao.dxf");
        var d = new Dxf(5);
        // tường bao 220 (2 nét, cm) trùng tim trục biên
        foreach (var (x0, y0, x1, y1) in new[] { (0.0, 0.0, 780.0, 0.0), (0.0, 900.0, 780.0, 900.0) })
        { d.Line("TUONG", x0 - 11, y0 - 11, x1 + 11, y1 - 11); d.Line("TUONG", x0 - 11, y0 + 11, x1 + 11, y1 + 11); }
        foreach (var (x0, y0, x1, y1) in new[] { (0.0, 0.0, 0.0, 900.0), (780.0, 0.0, 780.0, 900.0) })
        { d.Line("TUONG", x0 - 11, y0 + 11, x1 - 11, y1 - 11); d.Line("TUONG", x0 + 11, y0 + 11, x1 + 11, y1 - 11); }
        for (int i = 0; i < 3; i++)
        {
            double x = Xs[i] * 100;
            d.Line("TRUC", x, -150, x, 1050); d.Circle("TRUC", x, -200, 50); d.Text("TRUC", x - 15, -215, 30, (i + 1).ToString(Inv));
        }
        for (int j = 0; j < 3; j++)
        {
            double y = Ys[j] * 100;
            d.Line("TRUC", -150, y, 930, y); d.Circle("TRUC", -200, y, 50); d.Text("TRUC", -215, y - 15, 30, ((char)('A' + j)).ToString());
        }
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) d.Rect("COT", Xs[i] * 100, Ys[j] * 100, 22, 30);
        d.Text("CHU", 180, 220, 25, "WC");
        File.WriteAllText(path, d.End(), new UTF8Encoding(false));
        var dr = ArchPlanReader.Read(path, new ArchImportOptions());
        var fl = dr.Floors.FirstOrDefault();
        check("DXF cm: đọc được mặt bằng", B(fl != null), 1, 0);
        if (fl == null) return;
        var rd = PlanAxisPicker.Read(fl, new AxisPickOptions());
        check("DXF cm: bước trục X = 4,2 3,6 (đổi đơn vị cm → m)", B(rd.SpansXText == "4.2 3.6"), 1, 0);
        check("DXF cm: bước trục Y = 4,5 4,5", B(rd.SpansYText == "4.5 4.5"), 1, 0);
        check("DXF cm: 9 cột C30x22 trên giao trục", B(rd.Columns.Count(c => c.OnGrid && c.Section == "C30x22") == 9), 1, 0);
        var rdc = PlanAxisPicker.Read(fl, new AxisPickOptions { Source = AxisSource.Columns });
        check("DXF cm: trục theo cột (tim) trùng trục layer", B(rdc.SpansXText == rd.SpansXText && rdc.SpansYText == rd.SpansYText), 1, 0);
    }

    /// <summary>Tâm hình chữ nhật cột trên mặt bằng kết cấu (mm) – qua FramingPlan.ColumnRect.</summary>
    private static (double X, double Y) FramingPlanProbe(Project p, FrameElement f)
    {
        var r = KetCauVXT.Drawing.FramingPlan.ColumnRect(p, f);
        return (r.Average(q => q.X), r.Average(q => q.Y));
    }
}
