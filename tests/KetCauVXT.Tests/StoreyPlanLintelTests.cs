using System.Globalization;
using KetCauVXT.Detailing;
using KetCauVXT.Import;

namespace KetCauVXT.Tests;

/// <summary>Nhân bản tầng (dùng chung / tạo mới cấu kiện, SL nhân theo tầng), bắt điểm khi vẽ mặt bằng, sàn trong ô dầm, hoàn tác,
/// nhập mặt bằng kết cấu vẽ trong AutoCAD (DXF), ánh xạ mặt bằng kiến trúc → nhận cửa → đề xuất lanh tô (gộp LT theo ô cửa), ô văng trên cửa,
/// gợi ý an toàn lanh tô tường 110, ánh xạ vị trí ↔ cấu kiện (cây thư mục), mục lanh tô – ô văng các tầng.</summary>
public static class StoreyPlanLintelTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static int B(bool b) => b ? 1 : 0;

    /// <summary>Bộ dựng DXF ASCII đơn giản (mm).</summary>
    private sealed class Dxf
    {
        private readonly List<string> _l = new();
        public Dxf()
        {
            P(0, "SECTION"); P(2, "HEADER"); P(9, "$ACADVER"); P(1, "AC1027"); P(9, "$INSUNITS"); P(70, 4); P(0, "ENDSEC");
            P(0, "SECTION"); P(2, "ENTITIES");
        }
        public void P(int code, object v)
        {
            _l.Add(code.ToString(Inv));
            _l.Add(v is double d ? d.ToString("R", Inv) : v is int i ? i.ToString(Inv) : Convert.ToString(v, Inv));
        }
        public string[] End() { P(0, "ENDSEC"); P(0, "EOF"); return _l.ToArray(); }
        public void Line(string layer, double x1, double y1, double x2, double y2)
        { P(0, "LINE"); P(8, layer); P(10, x1); P(20, y1); P(30, 0.0); P(11, x2); P(21, y2); P(31, 0.0); }
        public void Arc(string layer, double cx, double cy, double r, double a1, double a2)
        { P(0, "ARC"); P(8, layer); P(10, cx); P(20, cy); P(30, 0.0); P(40, r); P(50, a1); P(51, a2); }
        public void Text(string layer, double x, double y, double h, string s)
        { P(0, "TEXT"); P(8, layer); P(10, x); P(20, y); P(30, 0.0); P(40, h); P(1, s); }
        public void Rect(string layer, double cx, double cy, double b, double h)
        {
            P(0, "LWPOLYLINE"); P(8, layer); P(90, 4); P(70, 1);
            foreach (var (x, y) in new[] { (cx - b / 2, cy - h / 2), (cx + b / 2, cy - h / 2), (cx + b / 2, cy + h / 2), (cx - b / 2, cy + h / 2) }) { P(10, x); P(20, y); }
        }
    }

    /// <summary>Hồ sơ thử: lưới 4,2 + 3,6 × 4,5; Tầng 2: 4 cột C, 2 dầm D (trục A, B), 1 sàn, 1 lanh tô, 1 đường cắt.</summary>
    private static (DetailingProject P, PlanLevel T2, string Col, string Beam, string Slab, string Lt) Build()
    {
        var p = new DetailingProject();
        p.Plan.SpansX = "4.2 3.6"; p.Plan.SpansY = "4.5";
        var t2 = p.Plan.Levels.First(l => l.Name == "Tầng 2");
        var c = p.Plan.Place(p, t2, PlanKinds.Column, 0, 0, 0, 0);
        foreach (var (x, y) in new[] { (7.8, 0.0), (0.0, 4.5), (7.8, 4.5) }) p.Plan.Place(p, t2, PlanKinds.Column, x, y, x, y, c.Member);
        var b = p.Plan.Place(p, t2, PlanKinds.Beam, 0, 0, 7.8, 0);
        p.Plan.Place(p, t2, PlanKinds.Beam, 0, 4.5, 7.8, 4.5, b.Member);
        var s = p.Plan.Place(p, t2, PlanKinds.Slab, 0, 0, 4.2, 4.5);
        var l = p.Plan.Place(p, t2, PlanKinds.Lintel, 1.5, 4.5, 2.7, 4.5);
        p.Plan.Sections.Add(new SectionMark { Name = "1-1", Level = t2.Name, X1 = 2, Y1 = -0.5, X2 = 2, Y2 = 5 });
        p.Plan.SyncQuantities(p);
        return (p, t2, c.Member, b.Member, s.Member, l.Member);
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ================= 1. nhân bản tầng – A: dùng chung cấu kiện =================
        var (pa, t2a, col, beam, slab, lt) = Build();
        var colM = pa.Columns.First(x => x.Name == col);
        int stories0 = DetailShim.ParseList(colM.Stories).Count;
        int items0 = t2a.Items.Count;
        check("Mẫu nhân bản: Tầng 2 có 8 vị trí, SL dầm = 2", items0 * 10 + pa.Beams.First(x => x.Name == beam).Quantity, 82, 0);
        var made = PlanStorey.CopyLevels(pa, pa.Plan, t2a, 2, 3.6, false, true);
        check("Nhân bản A: tạo 2 tầng Tầng 3, Tầng 4", B(made.Count == 2 && made[0].Name == "Tầng 3" && made[1].Name == "Tầng 4"), 1, 0);
        check("Nhân bản A: cao độ Tầng 3 = 7,2; Tầng 4 = 10,8", made[0].Elevation + made[1].Elevation * 10, 7.2 + 108, 1e-9);
        check("Nhân bản A: Mái đẩy lên 7,2 + 2·3,6 = 14,4", pa.Plan.Levels.First(l => l.Name == "Mái").Elevation, 14.4, 1e-9);
        check("Nhân bản A: thứ tự mặt bằng Móng, Tầng 2, Tầng 3, Tầng 4, Mái",
              B(string.Join("|", pa.Plan.Levels.Select(l => l.Name)) == "Móng|Tầng 2|Tầng 3|Tầng 4|Mái"), 1, 0);
        check("Nhân bản A: mỗi tầng mới có đủ 8 vị trí", made.Sum(l => l.Items.Count), 2 * items0, 0);
        check("Nhân bản A: không tạo cấu kiện mới (dùng chung tên)", pa.All.Count(), 4, 0);
        check("Nhân bản A: SL dầm = 2 vị trí × 3 tầng = 6", pa.Beams.First(x => x.Name == beam).Quantity, 6, 0);
        check("Nhân bản A: SL sàn = 3, SL lanh tô = 3", pa.Slabs.First(x => x.Name == slab).Quantity * 10 + pa.Lintels.First(x => x.Name == lt).Quantity, 33, 0);
        check("Nhân bản A: cột đếm theo vị trí – SL = 4 (cột nhiều tầng)", colM.Quantity, 4, 0);
        check("Nhân bản A: cột thêm 2 tầng (3,6 m)", DetailShim.ParseList(colM.Stories).Count - stories0, 2, 0);
        check("Nhân bản A: tầng cột thêm = 3,6 m", DetailShim.ParseList(colM.Stories)[^1], 3.6, 1e-9);
        check("Nhân bản A: đường cắt sàn sao theo tầng (3 đường cắt)", pa.Plan.Sections.Count, 3, 0);
        check("Nhân bản A: đường cắt mới thuộc Tầng 3 / Tầng 4", B(pa.Plan.Sections.Any(q => q.Level == "Tầng 3") && pa.Plan.Sections.Any(q => q.Level == "Tầng 4")), 1, 0);
        check("Nhân bản A: SL khớp mặt bằng (không cảnh báo)", pa.Plan.QuantityWarnings(pa).Count, 0, 0);
        var qa = DetailingExport.Quantities(pa, pa.RunAll());
        check("Nhân bản A: bảng khối lượng có số liệu", B(qa.Count > 0), 1, 0);

        // ================= 2. nhân bản tầng – B: tạo cấu kiện mới =================
        var (pb, t2b, colB, beamB, slabB, ltB) = Build();
        var mb = PlanStorey.CopyLevels(pb, pb.Plan, t2b, 1, 3.6, true, true);
        string bNew = beamB + "-T3";
        check("Nhân bản B: dầm mới tên " + bNew, B(pb.Beams.Any(x => x.Name == bNew)), 1, 0);
        check("Nhân bản B: 2 dầm, 2 sàn, 2 lanh tô, 1 cột", pb.Beams.Count * 1000 + pb.Slabs.Count * 100 + pb.Lintels.Count * 10 + pb.Columns.Count, 2221, 0);
        check("Nhân bản B: lanh tô mới " + ltB + "-T3", B(pb.Lintels.Any(x => x.Name == ltB + "-T3")), 1, 0);
        check("Nhân bản B: Tầng 3 tham chiếu cấu kiện mới, cột dùng chung",
              B(mb[0].Items.Where(i => i.Kind == PlanKinds.Beam).All(i => i.Member == bNew) && mb[0].Items.Where(i => i.Kind == PlanKinds.Column).All(i => i.Member == colB)), 1, 0);
        check("Nhân bản B: SL dầm gốc 2, dầm mới 2", pb.Beams.First(x => x.Name == beamB).Quantity * 10 + pb.Beams.First(x => x.Name == bNew).Quantity, 22, 0);
        var cb = pb.Beams.First(x => x.Name == bNew);
        check("Nhân bản B: dầm mới cao độ đỉnh = 7,2", cb.TopElevation, 7.2, 1e-9);
        cb.B = 0.33;
        check("Nhân bản B: sửa dầm mới không đổi dầm gốc", pb.Beams.First(x => x.Name == beamB).B, new ManualBeam().B, 1e-12);
        check("Mã tầng: Tầng 3 → T3, Mái → M, Tum → TUM", B(PlanStorey.LevelCode("Tầng 3") == "T3" && PlanStorey.LevelCode("Mái") == "M" && PlanStorey.LevelCode("Tum") == "TUM"), 1, 0);
        check("Tên tầng kế tiếp: Tầng 2 → Tầng 5 (đã có Tầng 3)", B(PlanStorey.NextLevelName(pb.Plan, "Tầng 2") == "Tầng 4"), 1, 0);
        var lv1 = pb.Plan.CopyLevel(t2b, "Tầng X", 20, false);
        check("StructuralPlan.CopyLevel: chỉ sao vị trí", lv1.Items.Count, items0, 0);

        // ================= 3. bắt điểm =================
        var plan = pa.Plan;
        var ls = new PlanLevel { Name = "thu", Elevation = 0 };
        ls.Items.Add(new PlanItem { Kind = PlanKinds.Column, X1 = 0, Y1 = 0, X2 = 0, Y2 = 0, Member = col });
        ls.Items.Add(new PlanItem { Kind = PlanKinds.Beam, X1 = 1, Y1 = 1, X2 = 3, Y2 = 1, Member = beam });
        var s1 = PlanSnap.Snap(plan, ls, 0.1, 0.08, 0.25);
        check("Bắt điểm: tâm cột (0;0)", B(s1.X == 0 && s1.Y == 0 && s1.Kind == PlanSnap.KindColumn), 1, 0);
        var s2 = PlanSnap.Snap(plan, ls, 3.1, 1.05, 0.25);
        check("Bắt điểm: đầu dầm (3;1)", B(Math.Abs(s2.X - 3) < 1e-9 && Math.Abs(s2.Y - 1) < 1e-9 && s2.Kind == PlanSnap.KindEnd), 1, 0);
        var s3 = PlanSnap.Snap(plan, ls, 4.15, 4.4, 0.25);
        check("Bắt điểm: giao trục 2-B (4,2;4,5)", B(Math.Abs(s3.X - 4.2) < 1e-9 && Math.Abs(s3.Y - 4.5) < 1e-9 && s3.Kind == PlanSnap.KindGridX), 1, 0);
        var s4 = PlanSnap.Snap(plan, ls, 2.0, 1.08, 0.25);
        check("Bắt điểm: trên tim dầm (2;1)", B(Math.Abs(s4.X - 2) < 1e-9 && Math.Abs(s4.Y - 1) < 1e-9 && s4.Kind == PlanSnap.KindOnBeam), 1, 0);
        var s5 = PlanSnap.Snap(plan, ls, 4.1, 2.33, 0.25);
        check("Bắt điểm: đường trục X = 4,2, Y làm tròn 50 mm = 2,35", s5.X * 10 + s5.Y, 42 + 2.35, 1e-9);
        var s6 = PlanSnap.Snap(plan, ls, 2.6, 1.3, 0.25, (1, 1), true);
        check("Bắt điểm vuông góc (Shift): giữ Y = 1", s6.Y, 1, 1e-12);
        var s7 = PlanSnap.Snap(plan, ls, 1.2, 3.0, 0.25, (1, 1), true);
        check("Bắt điểm vuông góc (Shift): giữ X = 1, Y = 3", s7.X * 10 + s7.Y, 13, 1e-9);
        var bay = PlanSnap.BayAt(pa.Plan, t2a, 2, 2);
        check("Sàn trong ô dầm: ô (0;0)–(4,2;4,5)", B(bay != null && bay.Value.X0 == 0 && bay.Value.Y0 == 0 && Math.Abs(bay.Value.X1 - 4.2) < 1e-9 && Math.Abs(bay.Value.Y1 - 4.5) < 1e-9), 1, 0);
        var it0 = new PlanItem { Kind = PlanKinds.Beam, X1 = 0, Y1 = 0, X2 = 2, Y2 = 0 };
        PlanSnap.Move(it0, 0.5, 1.2);
        check("Di chuyển vị trí: (0,5;1,2)–(2,5;1,2)", it0.X1 + it0.Y1 + it0.X2 + it0.Y2, 0.5 + 1.2 + 2.5 + 1.2, 1e-9);

        // ================= 4. hoàn tác =================
        var undo = new PlanUndo();
        int nT2 = t2a.Items.Count;
        undo.Push(pa.Plan);
        t2a.Items.Add(new PlanItem { Kind = PlanKinds.Beam, X1 = 4.2, Y1 = 0, X2 = 4.2, Y2 = 4.5, Member = beam });
        bool ok = undo.Undo(pa.Plan);
        check("Hoàn tác: khôi phục số vị trí Tầng 2", B(ok) * 100 + pa.Plan.Levels.First(l => l.Name == "Tầng 2").Items.Count, 100 + nT2, 0);
        check("Hoàn tác: hết ảnh chụp → false", B(undo.Undo(pa.Plan)), 0, 0);
        t2a = pa.Plan.Levels.First(l => l.Name == "Tầng 2");

        // ================= 5. ánh xạ vị trí ↔ cấu kiện (cây thư mục) =================
        var t3a = pa.Plan.Levels.First(l => l.Name == "Tầng 3");
        var beamM = pa.Beams.First(x => x.Name == beam);
        check("Vị trí → cấu kiện: dầm trên Tầng 3", B(ReferenceEquals(PlanMemberMap.Find(pa, t3a.Items.First(i => i.Kind == PlanKinds.Beam)), beamM)), 1, 0);
        check("Cấu kiện → vị trí: dầm đặt 6 lần trên 3 mặt bằng", PlanMemberMap.Placements(pa.Plan, beamM).Count, 6, 0);
        var groups = StructuralPlan.GroupForTree(pa);
        var gb = PlanMemberMap.GroupFor(groups, beamM, t3a);
        check("Cây: dầm chọn ở Tầng 3 → nút Mặt bằng kết cấu Tầng 3", B(gb != null && gb.Kind == "Tang" && gb.Level == t3a), 1, 0);
        var ltM = pa.Lintels.First(x => x.Name == lt);
        var gl = PlanMemberMap.GroupFor(groups, ltM, t3a);
        check("Cây: lanh tô Tầng 3 → nút Mặt bằng lanh tô – ô văng Tầng 3", B(gl != null && gl.Kind == StructuralPlan.LintelGroupKind && gl.Level == t3a && gl.Title == "Mặt bằng lanh tô – ô văng Tầng 3"), 1, 0);
        var gc = PlanMemberMap.GroupFor(groups, colM, t3a, t3a.Items.First(i => i.Kind == PlanKinds.Column));
        check("Cây: cột → nút Mặt bằng định vị cột", B(gc != null && gc.Kind == "Cot"), 1, 0);
        check("Cây: 3 nút mặt bằng lanh tô (Tầng 2, 3, 4)", groups.Count(g => g.Kind == StructuralPlan.LintelGroupKind), 3, 0);
        var dup = new DetailingProject();
        dup.Beams.Add(new ManualBeam { Name = "K9" }); dup.Slabs.Add(new ManualSlab { Name = "K9" });
        check("Vị trí → cấu kiện: trùng tên khác loại → đúng loại (sàn)", B(PlanMemberMap.Find(dup, new PlanItem { Kind = PlanKinds.Slab, Member = "K9" }) is ManualSlab), 1, 0);
        check("Tên mới chưa dùng: LT2 khi đã có LT1", B(PlanMemberMap.NextName(pa, "LT") == "LT2"), 1, 0);

        // mục lanh tô – ô văng các tầng
        var resA = pa.RunAll();
        var sheets = LintelPlanDrawing.Sheets(pa, resA);
        check("Lanh tô các tầng: 3 tờ (Tầng 2, 3, 4), chi tiết LT ở tầng thấp nhất", sheets.Count * 100 + sheets[0].Members.Count * 10 + sheets[1].Members.Count, 310, 0);
        string lpd = sheets[0].Plan.W.ToDxfStringDec();
        check("Mặt bằng lanh tô – ô văng: tiêu đề + tên lanh tô", B(sheets[0].Plan.Name == LintelPlanDrawing.Title(t2a) && lpd.Contains(lt)), 1, 0);
        var pvl = StructuralPlanDrawing.ForGroup(pa, resA, gl);
        check("Xem trước nút mặt bằng lanh tô: 1 hình mặt bằng lanh tô", B(pvl.Count == 1 && pvl[0].Name.StartsWith(LintelPlanDrawing.TitlePrefix)), 1, 0);
        var dlog = new List<string>();
        string dDir = Path.Combine(outDir, "TrienKhai", "NhanBanTang");
        var files = DetailingExport.Drawings(pa, resA, dDir, true, dlog, KetCauVXT.Model.CadOutputFormat.DxfR12);
        var fn = files.Select(Path.GetFileName).ToList();
        check("Xuất tách: 3 tờ lanh tô các tầng (LanhTo_Tầng…), không còn tờ lanh tô rời", fn.Count(n => n.Contains("LanhTo_")) * 10 + fn.Count(n => n.Contains("_" + lt + ".")), 30, 0);

        // ================= 6. nhập mặt bằng kết cấu vẽ trong AutoCAD (DXF) =================
        var d = new Dxf();
        d.Rect("COT", 10000, 20000, 220, 220);
        d.Rect("COT", 14200, 20000, 220, 220);
        d.Line("DAM", 10000, 19890, 14200, 19890); d.Line("DAM", 10000, 20110, 14200, 20110);
        d.Line("DAM", 10000, 24390, 14200, 24390); d.Line("DAM", 10000, 24610, 14200, 24610);
        d.Text("CHU", 12000, 20300, 250, "D1 220x400");
        d.Text("CHU", 12000, 24800, 250, "D2 220x300");
        d.Text("CHU", 10300, 20300, 250, "C1 (220x220)");
        d.Text("CHU", 12000, 22250, 250, "S1");
        var rd = DxfReader.FromLines(d.End());
        var pi = new DetailingProject();
        pi.Plan.SpansX = "4.2"; pi.Plan.SpansY = "4.5";
        var lvi = pi.Plan.Levels.First(l => l.Name == "Tầng 2");
        var ir = StructuralPlanImport.Import(pi, lvi, rd.Entities);
        check("Nhập DXF: 2 cột, 2 dầm, 1 sàn", ir.Columns * 100 + ir.Beams * 10 + ir.Slabs, 221, 0);
        check("Nhập DXF: dời gốc (10; 20) m về giao trục 1-A", ir.OffsetX * 100 + ir.OffsetY, 1000 + 20, 1e-6);
        var ci = pi.Columns.FirstOrDefault();
        check("Nhập DXF: 1 cấu kiện cột C1 220×220 (2 vị trí)", B(pi.Columns.Count == 1 && ci.Name == "C1" && Math.Abs(ci.B - 0.22) < 1e-9 && Math.Abs(ci.H - 0.22) < 1e-9 && ci.Quantity == 2), 1, 0);
        var d1 = pi.Beams.FirstOrDefault(x => x.Name == "D1"); var d2 = pi.Beams.FirstOrDefault(x => x.Name == "D2");
        check("Nhập DXF: dầm D1 220×400, D2 220×300 (từ chữ)", B(d1 != null && d2 != null && Math.Abs(d1.B - 0.22) < 1e-9 && Math.Abs(d1.H - 0.40) < 1e-9 && Math.Abs(d2.H - 0.30) < 1e-9), 1, 0);
        var bi = lvi.Items.First(i => i.Kind == PlanKinds.Beam && i.Member == "D1");
        check("Nhập DXF: tim dầm D1 (0;0)–(4,2;0) – từ cặp nét song song", Math.Abs(bi.X1) + Math.Abs(bi.Y1) + Math.Abs(bi.X2 - 4.2) + Math.Abs(bi.Y2), 0, 1e-6);
        check("Nhập DXF: nhịp dầm D1 = 4,2 m", DetailShim.ParseList(d1.Spans).Sum(), 4.2, 1e-6);
        var si = lvi.Items.FirstOrDefault(i => i.Kind == PlanKinds.Slab);
        check("Nhập DXF: sàn S1 trong ô (0;0)–(4,2;4,5)", B(si != null && si.Member == "S1" && Math.Abs(si.MaxX - 4.2) < 1e-6 && Math.Abs(si.MaxY - 4.5) < 1e-6 && Math.Abs(si.MinX) < 1e-6), 1, 0);
        check("Nhập DXF: ô sàn S1 Lx = 4,2 m", pi.Slabs.First(x => x.Name == "S1").Lx, 4.2, 1e-6);
        check("Đọc kích thước trong chữ: 220x400", PlanCadGeom.Section("D1 220x400")?.H ?? 0, 0.4, 1e-12);
        check("Đọc tên cấu kiện: \"C12 (300x300)\" → C12", B(PlanCadGeom.MemberName("C12 (300x300)") == "C12"), 1, 0);
        // dữ liệu lệnh VXT_MB_KC (JSON)
        string js = "{\"kind\":\"matbang\",\"unit\":\"mm\",\"entities\":[{\"t\":\"line\",\"layer\":\"DAM\",\"pts\":[[0,0],[4200,0]],\"closed\":false},"
                  + "{\"t\":\"line\",\"layer\":\"COT\",\"pts\":[[-110,-110],[110,-110],[110,110],[-110,110]],\"closed\":true},{\"t\":\"text\",\"layer\":\"CHU\",\"x\":2000,\"y\":300,\"s\":\"D5 200x350\"}]}";
        var je = StructuralPlanImport.ParseExchange(js);
        check("VXT_MB_KC: 3 thực thể, đổi mm → m", B(je.Count == 3 && Math.Abs(je[0].Points[1].X - 4.2) < 1e-12 && je[1].Closed && je[2].Type == DxfEntityType.Text), 1, 0);
        var pj = new DetailingProject();
        var jr = StructuralPlanImport.Import(pj, pj.Plan.Levels[1], je);
        check("VXT_MB_KC: nhập 1 dầm D5 200×350 (nét đơn) + 1 cột", B(jr.Beams == 1 && jr.Columns == 1 && pj.Beams.Any(x => x.Name == "D5" && Math.Abs(x.H - 0.35) < 1e-9)), 1, 0);

        // ================= 7. ánh xạ kiến trúc → cửa → lanh tô =================
        var a = new Dxf();
        // tường ngoài dưới 220 (y 0 – 220), cửa đi 900 tại x 2000 – 2900
        a.Line("TUONG", 0, 0, 2000, 0); a.Line("TUONG", 2900, 0, 6000, 0);
        a.Line("TUONG", 0, 220, 2000, 220); a.Line("TUONG", 2900, 220, 6000, 220);
        // tường ngoài trên 220
        a.Line("TUONG", 0, 4000, 6000, 4000); a.Line("TUONG", 0, 4220, 6000, 4220);
        // tường 110 trong nhà x = 4000 / 4110 và 5000 / 5110, cửa đi 800 tại y 1000 – 1800
        foreach (double x0 in new[] { 4000.0, 5000.0 })
        {
            a.Line("TUONG", x0, 220, x0, 1000); a.Line("TUONG", x0, 1800, x0, 4000);
            a.Line("TUONG", x0 + 110, 220, x0 + 110, 1000); a.Line("TUONG", x0 + 110, 1800, x0 + 110, 4000);
        }
        a.Line("CUA", 2000, 220, 2000, 1120); a.Arc("CUA", 2000, 220, 900, 0, 90);
        a.Line("CUA", 4110, 1000, 4910, 1000); a.Arc("CUA", 4110, 1000, 800, 0, 90);
        a.Line("CUA", 5110, 1000, 5910, 1000); a.Arc("CUA", 5110, 1000, 800, 0, 90);
        var ra = DxfReader.FromLines(a.End());
        var u = ArchUnderlay.FromEntities(ra.Entities);
        var ops = u.Openings.OrderBy(o => o.X).ToList();
        check("Ánh xạ KT: nhận 3 ô cửa", ops.Count, 3, 0);
        var o1 = ops.FirstOrDefault(o => o.Width > 0.85);
        check("Cửa tường ngoài: rộng 900, tường 220, tâm (2,45; 0,11)", B(o1 != null && Math.Abs(o1.Width - 0.9) < 1e-6 && Math.Abs(o1.WallT - 0.22) < 1e-6 && Math.Abs(o1.X - 2.45) < 1e-6 && Math.Abs(o1.Y - 0.11) < 1e-6), 1, 0);
        check("Cửa tường ngoài: nhận là tường ngoài, phía ngoài −Y", B(o1 != null && o1.Exterior && o1.OutSide == -1), 1, 0);
        var o2 = ops.Where(o => o.Width < 0.85).ToList();
        check("Cửa tường 110: 2 cửa rộng 800, tường 110, phương Y", B(o2.Count == 2 && o2.All(o => Math.Abs(o.Width - 0.8) < 1e-6 && Math.Abs(o.WallT - 0.11) < 1e-6 && Math.Abs(o.Uy - 1) < 1e-9 && !o.Exterior)), 1, 0);
        check("Cửa tường 110: tâm (4,055; 1,4)", B(o2.Count > 0 && Math.Abs(o2[0].X - 4.055) < 1e-6 && Math.Abs(o2[0].Y - 1.4) < 1e-6), 1, 0);
        var pl = new DetailingProject();
        var lvl = pl.Plan.Levels.First(l => l.Name == "Tầng 2");
        var added = DoorLintels.Place(pl, lvl, ops);
        check("Lanh tô theo cửa: 3 vị trí, 2 cấu kiện (gộp cửa giống nhau)", added.Count * 10 + pl.Lintels.Count, 32, 0);
        var lt110 = pl.Lintels.FirstOrDefault(x => Math.Abs(x.Bw - 0.11) < 1e-9);
        var lt220 = pl.Lintels.FirstOrDefault(x => Math.Abs(x.Bw - 0.22) < 1e-9);
        check("Lanh tô tường 110: Lo = 0,8 m, SL = 2", B(lt110 != null && Math.Abs(lt110.Lo - 0.8) < 1e-9 && lt110.Quantity == 2), 1, 0);
        check("Lanh tô tường 220: Lo = 0,9 m, SL = 1", B(lt220 != null && Math.Abs(lt220.Lo - 0.9) < 1e-9 && lt220.Quantity == 1), 1, 0);
        check("Lanh tô theo cửa: tên LT1, LT2", B(pl.Lintels.Select(x => x.Name).OrderBy(x => x).SequenceEqual(new[] { "LT1", "LT2" })), 1, 0);
        check("Lanh tô theo cửa: bấm lại không đặt trùng", DoorLintels.Place(pl, lvl, ops).Count, 0, 0);
        var cpy = DoorLintels.PlaceCanopy(pl, lvl, o1, 0.6);
        check("Ô văng trên cửa ngoài: vươn ra phía ngoài (y 0 → −0,6), dài 0,9 + 2·0,2",
              Math.Abs(cpy.MinY + 0.6) + Math.Abs(cpy.MaxY) + Math.Abs(cpy.MaxX - cpy.MinX - 1.3), 0, 1e-6);
        var ov = pl.Lintels.FirstOrDefault(x => x.Name == "OV1");
        check("Ô văng trên cửa: cấu kiện OV1 lanh tô + ô văng liền khối, SL 1", B(ov != null && ov.Type == ManualLintel.TypeCombined && ov.Quantity == 1 && Math.Abs(ov.Lc - 0.6) < 1e-9), 1, 0);
        check("Ô văng trên cửa: lanh tô của cửa đổi sang OV1", B(lvl.Items.Any(i => i.Kind == PlanKinds.Lintel && i.Member == "OV1") && !lvl.Items.Any(i => i.Member == lt220.Name)), 1, 0);
        var plg = StructuralPlan.GroupForTree(pl).FirstOrDefault(g => g.Kind == StructuralPlan.LintelGroupKind);
        check("Cây: mục Mặt bằng lanh tô – ô văng Tầng 2 chứa LT (110) và OV1", B(plg != null && plg.Members.Contains(lt110) && plg.Members.Contains(ov)), 1, 0);
        var lvR = new PlanLevel { Name = "R" };
        ArchUnderlay.Attach(lvR, Path.Combine(outDir, "kt", "tang2.dxf"), outDir);
        check("Ánh xạ: lưu đường dẫn tương đối theo thư mục hồ sơ", B(lvR.UnderlayRel.Replace('\\', '/') == "kt/tang2.dxf" && lvR.UnderlayVisible), 1, 0);
        check("Ánh xạ: file không có → không nạp (null)", B(ArchUnderlay.Load(lvR, outDir) == null), 1, 0);

        // ================= 8. gợi ý an toàn lanh tô tường 110 =================
        var s = new DetailSettings();
        var w1 = new ManualLintel { Name = "LTa", Type = ManualLintel.TypeLintel, Lo = 1.0, Bw = 0.11 };
        var r1 = w1.Run(s);
        check("Tường 110, Lo 1,0 m: h tự chọn 150, gối 200 mm", w1.HlUsed * 1000 + w1.LaUsed, 150 + 0.2, 1e-9);
        check("Tường 110: có gợi ý an toàn + kiểm tra nén cục bộ khối xây", B(r1.Messages.Any(m => m.StartsWith("Gợi ý (tường 110)")) && r1.Checks.Any(c => c.Name.StartsWith("Tường 110: nén cục bộ"))), 1, 0);
        check("Tường 110, Lo 1,0 m: đạt mọi kiểm tra", B(r1.Ok), 1, 0);
        var w2 = new ManualLintel { Name = "LTb", Type = ManualLintel.TypeLintel, Lo = 1.6, Bw = 0.11 };
        w2.Run(s);
        check("Tường 110, Lo 1,6 m > 1,5: h ≥ 200, gối ≥ 250 mm", w2.HlUsed * 1000 + w2.LaUsed, 200 + 0.25, 1e-9);
        var w3 = new ManualLintel { Name = "LTc", Type = ManualLintel.TypeLintel, Lo = 2.0, Bw = 0.11 };
        var r3 = w3.Run(s);
        check("Tường 110, cửa lớn 2,0 m: khuyến nghị bổ trụ BTCT 110×220", B(r3.Messages.Any(m => m.Contains("bổ trụ BTCT 110×220"))), 1, 0);
        check("Nén cục bộ khối xây: 0,5·1,25·1,3 MPa·0,11·0,2 = 17,875 kN", ManualLintel.BearingCapacity(1.3, 0.11, 0.2), 17.875, 1e-9);
        var w4 = new ManualLintel { Name = "LTd", Type = ManualLintel.TypeLintel, Lo = 1.0, Bw = 0.22 };
        var r4 = w4.Run(s);
        check("Tường 220: không áp gợi ý tường 110", B(!r4.Messages.Any(m => m.Contains("tường 110"))), 1, 0);
        var w5 = new ManualLintel { Name = "LTe", Type = ManualLintel.TypeLintel, Lo = 1.0, Bw = 0.11, Hl = 0.10, La = 0.12 };
        var r5 = w5.Run(s);
        check("Tường 110: h 100 < 150, gối 120 < 200 → không đạt", B(!r5.Checks.First(c => c.Name.StartsWith("Tường 110: chiều cao")).Ok && !r5.Checks.First(c => c.Name.StartsWith("Tường 110: gối")).Ok), 1, 0);
    }
}
