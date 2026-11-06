using System.Text.Json;
using System.Text.RegularExpressions;
using KetCauVXT.Drawing;
using KetCauVXT.Export;
using KetCauVXT.Import;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Cao độ móng → mái, bộ môn (hiện / mờ / sở hữu), kiến trúc (phòng, chuyển sang kết cấu), va chạm, IFC (xuất + nhập lại),
/// OBJ / DAE, thư viện 3D, nhập / cập nhật Revit (kiến trúc + điện nước), hồ sơ theo bộ môn.</summary>
public static class LevelsDisciplineIfcTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        static double B(bool v) => v ? 1 : 0;
        string dir = Path.Combine(outDir, "TrienKhai", "BoMon");
        Directory.CreateDirectory(dir);

        // ---------------- 1. Trình tạo cao độ ----------------
        var p = ModelBuilder.NewProject("Nhà 3 tầng");
        ModelBuilder.SetGrid(p, new[] { 4.0, 4.0 }, new[] { 5.0 });
        string msg = Levels.ApplyWizard(p, new LevelWizardOptions { Floors = 3, FirstFloorHeight = 3.6, FloorHeight = 3.3, FootingTopDepth = 0.9, FoundationDepth = 1.5 });
        check("Cao độ: 3 sàn do khung đỡ (T2, T3, MAI)", p.Stories.Count, 3, 0);
        var st = p.Stories.OrderBy(s => s.Elevation).ToList();
        check("Cao độ: sàn tầng 2 = +3,600", st[0].Elevation, 3.6, 1e-9);
        check("Cao độ: sàn tầng 3 = +6,900", st[1].Elevation, 6.9, 1e-9);
        check("Cao độ: sàn mái = +10,200", st[2].Elevation, 10.2, 1e-9);
        check("Cao độ: mặt móng −0,900", p.BaseElevation, -0.9, 1e-9);
        check("Cao độ: cột tầng 1 cao 3,6 + 0,9 = 4,5", st[0].Height, 4.5, 1e-9);
        var all = Levels.All(p);
        check("Danh sách cao độ: đáy móng, mặt móng, nền, 3 sàn = 6", all.Count, 6, 0);
        check("Đáy móng = −1,500", all[0].Elevation, -1.5, 1e-9);
        check("Đáy móng là loại DayMong", B(all[0].Kind == LevelKind.DayMong), 1, 0);
        check("Nền tầng 1 ±0,000", B(all.Any(l => l.Kind == LevelKind.Nen && Math.Abs(l.Elevation) < 1e-9 && l.Title == "Nền tầng 1")), 1, 0);
        check("Tên hiển thị: Sàn tầng 2", B(Levels.TitleOf(p, st[0]) == "Sàn tầng 2"), 1, 0);
        check("Tên hiển thị: Sàn mái", B(Levels.TitleOf(p, st[2]) == "Sàn mái"), 1, 0);
        check("Khung theo lưới: 18 cột", p.Frames.Count(f => f.Kind == FrameKind.Column), 18, 0);
        check("Khung theo lưới: 21 dầm", p.Frames.Count(f => f.Kind == FrameKind.Beam), 21, 0);
        check("Sàn khung: 6 ô", p.Slabs.Count, 6, 0);
        check("Sàn nền tầng 1: 2 ô trên đất", p.Arch.GroundSlabs.Count, 2, 0);
        check("Sàn nền: tổng 40 m²", p.Arch.GroundSlabs.Sum(g => g.Area), 40, 1e-9);
        check("Sàn nền không vào phân tích (OnGrade, không là Slab)", B(p.Arch.GroundSlabs.All(g => g.OnGrade) && p.Slabs.Count == 6), 1, 0);
        check("Trình tạo không ghi đè mô hình đã có cấu kiện", B(Levels.ApplyWizard(p, new LevelWizardOptions()).StartsWith("Mô hình đã có")), 1, 0);
        check("Thông báo trình tạo có cao độ nền", B(msg.Contains("nền")), 1, 0);

        // ---------------- 2. Chèn / xoá tầng ----------------
        var q = p.Clone();
        string nm = Levels.InsertStory(q, "T2", 3.0);
        var qs = q.Stories.OrderBy(s => s.Elevation).ToList();
        check("Chèn tầng: 4 sàn", qs.Count, 4, 0);
        check("Chèn tầng: tên tự động T2, T3, T4, MAI", B(string.Join(",", qs.Select(s => s.Name)) == "T2,T3,T4,MAI"), 1, 0);
        check("Chèn tầng: tầng mới tên T3", B(nm == "T3"), 1, 0);
        check("Chèn tầng: tầng mới +6,600", qs[1].Elevation, 6.6, 1e-9);
        check("Chèn tầng: mái nâng lên +13,200", qs[3].Elevation, 13.2, 1e-9);
        check("Chèn tầng: dầm tầng mới = 7", q.Frames.Count(f => f.Kind == FrameKind.Beam && f.Story == "T3"), 7, 0);
        check("Chèn tầng: sàn tầng mới = 2", q.Slabs.Count(s => s.Story == "T3"), 2, 0);
        check("Chèn tầng: cột cắt thêm 6 → 24", q.Frames.Count(f => f.Kind == FrameKind.Column), 24, 0);
        check("Chèn tầng: nút cao nhất +13,200", q.Nodes.Max(n => n.Z), 13.2, 1e-9);
        Levels.DeleteStory(q, "T3");
        var qd = q.Stories.OrderBy(s => s.Elevation).ToList();
        check("Xoá tầng: còn 3 sàn", qd.Count, 3, 0);
        check("Xoá tầng: tên T2, T3, MAI", B(string.Join(",", qd.Select(s => s.Name)) == "T2,T3,MAI"), 1, 0);
        check("Xoá tầng: mái về +10,200", qd[2].Elevation, 10.2, 1e-9);
        check("Xoá tầng: 18 cột", q.Frames.Count(f => f.Kind == FrameKind.Column), 18, 0);
        check("Xoá tầng: 21 dầm", q.Frames.Count(f => f.Kind == FrameKind.Beam), 21, 0);
        check("Xoá tầng: 6 ô sàn", q.Slabs.Count, 6, 0);
        ModelEdit.DeleteSlab(q, q.Slabs.First(s => s.Story == "T3"));
        check("Sàn mọi ô lưới: thêm lại 1 ô", Levels.SlabsForAllBays(q), 1, 0);

        // ---------------- 3. Bộ môn ----------------
        check("Bộ môn mặc định: Kết cấu", B(p.ActiveDiscipline == Discipline.KetCau), 1, 0);
        check("Bộ môn tham chiếu hiện", B(DisciplineView.Visible(p, Discipline.KienTruc)), 1, 0);
        check("Bộ môn tham chiếu vẽ mờ", B(DisciplineView.Halftone(p, Discipline.KienTruc)), 1, 0);
        check("Độ mờ tham chiếu 0,35", DisciplineView.Opacity(p, Discipline.KienTruc), 0.35, 1e-9);
        check("Bộ môn đang mở rõ (1)", DisciplineView.Opacity(p, Discipline.KetCau), 1, 0);
        check("Chỉ sửa bộ môn đang mở", B(!DisciplineView.Editable(p, Discipline.DienNuoc) && DisciplineView.Editable(p, Discipline.KetCau)), 1, 0);
        p.DisciplineDisplay.ShowDienNuoc = false;
        check("Tắt Điện nước → độ mờ 0", DisciplineView.Opacity(p, Discipline.DienNuoc), 0, 0);
        p.DisciplineDisplay.ShowDienNuoc = true;
        check("Sở hữu: tường → Kiến trúc", B(DisciplineView.OwnerOf("Tường") == Discipline.KienTruc), 1, 0);
        check("Sở hữu: ống nước → Điện nước", B(DisciplineView.OwnerOf("Ống nước") == Discipline.DienNuoc), 1, 0);
        check("Sở hữu: dầm → Kết cấu", B(DisciplineView.OwnerOf("Dầm") == Discipline.KetCau), 1, 0);
        check("Sở hữu: sàn nền → Kiến trúc", B(DisciplineView.OwnerOf("Sàn nền") == Discipline.KienTruc), 1, 0);
        check("Tiền tố hồ sơ KT / KC / DN", B(DisciplineView.Prefix(Discipline.KienTruc) == "KT" && DisciplineView.Prefix(Discipline.KetCau) == "KC" && DisciplineView.Prefix(Discipline.DienNuoc) == "DN"), 1, 0);
        var rt = p.Clone(); rt.ActiveDiscipline = Discipline.DienNuoc;
        var rt2 = JsonSerializer.Deserialize<Project>(JsonSerializer.Serialize(rt, Project.JsonOptions), Project.JsonOptions);
        check("Lưu / mở: bộ môn đang mở", B(rt2.ActiveDiscipline == Discipline.DienNuoc), 1, 0);
        check("Lưu / mở: sàn nền, loại cao độ", B(rt2.Arch.GroundSlabs.Count == 2 && rt2.Stories.Any(s => s.Kind == "Mai")), 1, 0);
        var old = JsonSerializer.Deserialize<Project>("{\"Name\":\"cũ\"}", Project.JsonOptions);
        check("File cũ: bộ môn Kết cấu, có phần kiến trúc rỗng", B(old.ActiveDiscipline == Discipline.KetCau && old.Arch != null && old.Arch.Rooms.Count == 0), 1, 0);

        // ---------------- 4. Kiến trúc: phòng, chuyển sang kết cấu ----------------
        string wt = p.WallTypes[1].Name;
        foreach (var (x1, y1, x2, y2) in new[] { (0.0, 0.0, 8.0, 0.0), (8.0, 0.0, 8.0, 5.0), (8.0, 5.0, 0.0, 5.0), (0.0, 5.0, 0.0, 0.0), (4.0, 0.0, 4.0, 5.0) })
            ModelEdit.AddWall(p, "T2", x1, y1, x2, y2, wt);
        check("Tường: 5 đoạn", p.Walls.Count, 5, 0);
        var rooms = RoomFinder.Detect(p, "T2");
        check("Tìm phòng: 2 phòng", rooms.Count, 2, 0);
        check("Tìm phòng: mỗi phòng 20 m²", rooms.Sum(r => r.Area), 40, 1e-9);
        rooms[0].Name = "WC"; rooms[1].Name = "Phòng ngủ";
        var tr = ArchStructureSync.Transfer(p);
        check("Kiến trúc → kết cấu: 5 tường trên dầm", tr.WallsOnBeams, 5, 0);
        check("Kiến trúc → kết cấu: phòng WC → sàn vệ sinh (1 ô)", tr.SlabUsesSet, 1, 0);
        check("Sàn dưới phòng WC thành sàn vệ sinh", B(ModelEdit.UseOf(p, ModelEdit.SlabAt(p, "T2", 2, 2.5)) == SlabUse.WC), 1, 0);
        check("Giao tường tại cột: không đề xuất cột", tr.ProposedColumns.Count, 0, 0);
        ModelEdit.AddWall(p, "T2", 2, 0, 2, 5, wt);
        var tr2 = ArchStructureSync.Transfer(p);
        check("Tường chữ T giữa nhịp: đề xuất 2 cột", tr2.ProposedColumns.Count, 2, 0);
        check("Tìm lại phòng: 3 phòng", RoomFinder.Detect(p, "T2").Count, 3, 0);
        var bottom = p.Walls.First(w => Math.Abs(w.Y1) < 1e-9 && Math.Abs(w.Y2) < 1e-9);
        string doorType = p.OpeningTypes.First(t => t.Kind == WallOpeningKind.Door).Name;
        check("Đặt cửa đi lên tường", B(WallAssembly.PlaceOpening(p, bottom, doorType, 6.0).Ok), 1, 0);
        check("Cửa → đề xuất lanh tô", B(ArchStructureSync.Transfer(p).LintelProposals >= 1), 1, 0);

        // ---------------- 5. Khối 3D, va chạm ----------------
        var sol = ModelSolids.Build(p);
        var col = sol.First(s => s.Category == "Cột" && s.Story == "T2");
        check("Khối cột tầng 1: 0,22 × 0,22 × 4,5", col.Volume, 0.22 * 0.22 * 4.5, 1e-9);
        check("Khối tường: 6 (bộ môn Kiến trúc)", sol.Count(s => s.Category == "Tường" && s.Discipline == Discipline.KienTruc), 6, 0);
        check("Khối cửa đi + lỗ cửa", B(sol.Count(s => s.IfcType == "IfcDoor") == 1 && sol.Count(s => s.IsVoid) == 1), 1, 0);
        check("Mô hình mẫu: không va chạm giữa bộ môn", ClashDetector.Run(p).Count, 0, 0);
        var beam = new Solid { Id = "DAM-1", Name = "D1", Category = "Dầm", Discipline = Discipline.KetCau, Origin = new Vec3(0, 0, 3.0), XDir = Vec3.UnitX, ZDir = Vec3.UnitZ, Profile = Solid.Rect(0, -0.11, 4, 0.11), Depth = 0.4 };
        var pipe = new Solid { Id = "ONG-1", Name = "Ống D60", Category = "Ống", Discipline = Discipline.DienNuoc, AllowSleeve = true, Origin = new Vec3(2, -1, 3.2), XDir = Vec3.UnitX, ZDir = Vec3.UnitZ, Profile = Solid.Rect(-0.03, 0, 0.03, 2), Depth = 0.06 };
        var colS = new Solid { Id = "COT-9", Name = "C9", Category = "Cột", Discipline = Discipline.KetCau, Origin = new Vec3(6, 0, 0), Profile = Solid.Rect(-0.11, -0.11, 0.11, 0.11), Depth = 3.3 };
        var lamp = new Solid { Id = "DEN-1", Name = "Đèn", Category = "Thiết bị", Discipline = Discipline.DienNuoc, Origin = new Vec3(6.05, 0, 2.5), Profile = Solid.Rect(-0.1, -0.1, 0.1, 0.1), Depth = 0.1 };
        var door = new Solid { Id = "CUA-1", Name = "D1", Category = "Cửa đi", Discipline = Discipline.KienTruc, Origin = new Vec3(5.9, -0.02, 0), Profile = Solid.Rect(0, 0, 0.9, 0.04), Depth = 2.2 };
        var near = new Solid { Id = "ONG-2", Name = "Ống", Category = "Ống", Discipline = Discipline.DienNuoc, Origin = new Vec3(0, 0.14, 3.2), Profile = Solid.Rect(0, 0, 4, 0.05), Depth = 0.05 };
        var cl = ClashDetector.Detect(new List<Solid> { beam, pipe, colS, lamp, door, near }, new ClashOptions { Clearance = 0.05 });
        check("Va chạm: ống xuyên dầm → ống lồng", cl.Count(c => c.Type == ClashType.OngLong && (c.A.Id == "ONG-1" || c.B.Id == "ONG-1")), 1, 0);
        check("Va chạm: đèn trong cột → cứng", cl.Count(c => c.Type == ClashType.Cung && new[] { c.A.Id, c.B.Id }.Contains("DEN-1")), 1, 0);
        check("Va chạm: cửa đi chạm cột → cứng", cl.Count(c => c.Type == ClashType.Cung && new[] { c.A.Id, c.B.Id }.Contains("CUA-1") && new[] { c.A.Id, c.B.Id }.Contains("COT-9")), 1, 0);
        check("Khe hở: ống cách dầm 30 mm < 50 mm", cl.Count(c => c.Type == ClashType.KheHo && new[] { c.A.Id, c.B.Id }.Contains("ONG-2")), 1, 0);
        check("Không báo trong cùng kết cấu (dầm–cột)", cl.Count(c => c.A.Discipline == Discipline.KetCau && c.B.Discipline == Discipline.KetCau), 0, 0);

        // ---------------- 6. IFC ----------------
        string ifc = IfcWriter.Write(p);
        File.WriteAllText(Path.Combine(dir, "MoHinh_BoMon.ifc"), ifc);
        check("IFC: đầu ISO-10303-21", B(ifc.StartsWith("ISO-10303-21;")), 1, 0);
        check("IFC: lược đồ IFC2X3", B(ifc.Contains("FILE_SCHEMA(('IFC2X3'));")), 1, 0);
        check("IFC: kết thúc END-ISO-10303-21", B(ifc.TrimEnd().EndsWith("END-ISO-10303-21;")), 1, 0);
        check("IFC: 2 ENDSEC", Regex.Matches(ifc, "^ENDSEC;", RegexOptions.Multiline).Count, 2, 0);
        int Cnt(string e) => Regex.Matches(ifc, @"=" + e + @"\(").Count;
        check("IFC: số IfcColumn = số cột", Cnt("IFCCOLUMN"), p.Frames.Count(f => f.Kind == FrameKind.Column), 0);
        check("IFC: số IfcBeam = số dầm", Cnt("IFCBEAM"), p.Frames.Count(f => f.Kind == FrameKind.Beam), 0);
        check("IFC: IfcSlab = sàn khung + sàn nền", Cnt("IFCSLAB"), p.Slabs.Count + p.Arch.GroundSlabs.Count, 0);
        check("IFC: 6 IfcWallStandardCase", Cnt("IFCWALLSTANDARDCASE"), 6, 0);
        check("IFC: 1 IfcDoor, 1 IfcOpeningElement", B(Cnt("IFCDOOR") == 1 && Cnt("IFCOPENINGELEMENT") == 1), 1, 0);
        check("IFC: quan hệ khoét + lấp lỗ", B(Cnt("IFCRELVOIDSELEMENT") == 1 && Cnt("IFCRELFILLSELEMENT") == 1), 1, 0);
        check("IFC: cao độ = đáy móng, nền, 3 sàn", Cnt("IFCBUILDINGSTOREY"), 5, 0);
        check("IFC: IfcSpace = số phòng", Cnt("IFCSPACE"), p.Arch.Rooms.Count, 0);
        var data = ifc[(ifc.IndexOf("DATA;", StringComparison.Ordinal) + 5)..ifc.LastIndexOf("ENDSEC;", StringComparison.Ordinal)]
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        bool wellFormed = data.All(l => Regex.IsMatch(l, @"^#\d+=IFC[A-Z0-9]+\(.*\);$"));
        check("IFC: mọi dòng DATA dạng #n=IFC...(...);", B(wellFormed), 1, 0);
        bool balanced = data.All(l =>
        {
            int d = 0; bool str = false;
            foreach (char c in l) { if (c == '\'') str = !str; else if (!str) { if (c == '(') d++; else if (c == ')') d--; if (d < 0) return false; } }
            return d == 0 && !str;
        });
        check("IFC: ngoặc cân bằng", B(balanced), 1, 0);
        var ids = data.Select(l => int.Parse(l[1..l.IndexOf('=')])).ToList();
        check("IFC: mã thực thể tăng dần, không trùng", B(ids.SequenceEqual(Enumerable.Range(1, ids.Count))), 1, 0);
        check("IFC: chữ tiếng Việt mã hoá \\X2\\", B(IfcWriter.Str("Sàn").Contains("\\X2\\")), 1, 0);
        check("IFC: giải mã chữ tiếng Việt", B(IfcWriter.Unstr(IfcWriter.Str("Phòng ngủ 'A'")) == "Phòng ngủ 'A'"), 1, 0);
        check("IFC: GlobalId 22 ký tự, tất định", B(IfcWriter.GlobalId("x").Length == 22 && IfcWriter.GlobalId("x") == IfcWriter.GlobalId("x") && IfcWriter.GlobalId("x") != IfcWriter.GlobalId("y")), 1, 0);

        // nhập lại IFC
        var p2 = ModelBuilder.NewProject("Nhập IFC");
        var ir = IfcImporter.Import(p2, ifc);
        check("Nhập IFC: 3 cao độ sàn", ir.Storeys, 3, 0);
        check("Nhập IFC: số cột", ir.Columns, p.Frames.Count(f => f.Kind == FrameKind.Column), 0);
        check("Nhập IFC: số dầm", ir.Beams, p.Frames.Count(f => f.Kind == FrameKind.Beam), 0);
        check("Nhập IFC: 6 tường", ir.Walls, 6, 0);
        check("Nhập IFC: 1 cửa đi gắn tường", ir.Doors, 1, 0);
        check("Nhập IFC: phòng", ir.Spaces, p.Arch.Rooms.Count, 0);
        check("Nhập IFC: sàn nền 2 ô (trên đất)", p2.Arch.GroundSlabs.Count, 2, 0);
        check("Nhập IFC: sàn khung 6 ô", p2.Slabs.Count, 6, 0);
        var fs = p2.FrameSections.FirstOrDefault(s => s.Name == p2.Frames.First(f => f.Kind == FrameKind.Column).Section);
        check("Nhập IFC: cột 220 mm", fs?.B ?? 0, 0.22, 1e-6);
        var wd = p2.Walls.First(w => w.Openings.Count > 0);
        check("Nhập IFC: cửa cách đầu tường 6,0 m", wd.Openings[0].Offset, 6.0, 1e-3);
        check("Nhập IFC: tường dài 8 m", p2.Walls.Max(w => w.Chord), 8.0, 1e-6);

        // ---------------- 7. OBJ, DAE ----------------
        var L = new List<double[]> { new[] { 0.0, 0 }, new[] { 4.0, 0 }, new[] { 4.0, 2 }, new[] { 2.0, 2 }, new[] { 2.0, 4 }, new[] { 0.0, 4 } };
        var tris = MeshExport.Triangulate(L);
        check("Tam giác hoá chữ L: 4 tam giác", tris.Count, 4, 0);
        double triArea = tris.Sum(t => Math.Abs(Geo2.SignedArea(new List<double[]> { L[t[0]], L[t[1]], L[t[2]] })));
        check("Tam giác hoá chữ L: tổng diện tích 12", triArea, 12, 1e-9);
        int objTris = MeshExport.SaveObj(p, Path.Combine(dir, "MoHinh_BoMon.obj"));
        string obj = File.ReadAllText(Path.Combine(dir, "MoHinh_BoMon.obj"));
        int nv = Regex.Matches(obj, "^v ", RegexOptions.Multiline).Count;
        var faceIdx = Regex.Matches(obj, @"^f (\d+) (\d+) (\d+)", RegexOptions.Multiline).SelectMany(m => new[] { 1, 2, 3 }.Select(k => int.Parse(m.Groups[k].Value))).ToList();
        check("OBJ: số mặt = số tam giác", faceIdx.Count / 3, objTris, 0);
        check("OBJ: chỉ số đỉnh hợp lệ", B(faceIdx.All(i => i >= 1 && i <= nv)), 1, 0);
        check("OBJ: có file vật liệu .mtl", B(File.Exists(Path.Combine(dir, "MoHinh_BoMon.mtl")) && obj.Contains("mtllib MoHinh_BoMon.mtl")), 1, 0);
        int daeTris = MeshExport.SaveDae(p, Path.Combine(dir, "MoHinh_BoMon.dae"));
        var daeXml = System.Xml.Linq.XDocument.Load(Path.Combine(dir, "MoHinh_BoMon.dae"));
        var ns = System.Xml.Linq.XNamespace.Get("http://www.collada.org/2005/11/COLLADASchema");
        check("DAE: XML hợp lệ, COLLADA 1.4.1", B(daeXml.Root?.Name == ns + "COLLADA" && (string)daeXml.Root.Attribute("version") == "1.4.1"), 1, 0);
        check("DAE: số tam giác = OBJ", daeTris, objTris, 0);
        check("DAE: mỗi khối một geometry", daeXml.Descendants(ns + "geometry").Count(), ModelSolids.Build(p, null, false).Count, 0);

        // ---------------- 8. Thư viện 3D ----------------
        string cube = "v 1 1 0\nv 3 1 0\nv 3 2 0\nv 1 2 0\nv 1 1 0.5\nv 3 1 0.5\nv 3 2 0.5\nv 1 2 0.5\n" +
                      "f 1 2 3 4\nf 5 6 7 8\nf 1 2 6 5\nf 2 3 7 6\nf 3 4 8 7\nf 4 1 5 8\n";
        var li = Library3D.ReadObj(cube, "Bồn cầu");
        check("Thư viện OBJ: rộng 2, sâu 1, cao 0,5", li.Width + li.Depth * 10 + li.Height * 100, 2 + 10 + 50, 1e-9);
        check("Thư viện OBJ: 12 tam giác", li.Faces.Count, 12, 0);
        check("Thư viện OBJ: gốc tại tâm đáy", B(Math.Abs(li.Vertices.Min(v => v[0]) + 1) < 1e-9 && Math.Abs(li.Vertices.Min(v => v[2])) < 1e-9), 1, 0);
        string libDir = Path.Combine(dir, "ThuVien3D");
        if (Directory.Exists(libDir)) Directory.Delete(libDir, true);
        string objPath = Path.Combine(dir, "Bồn cầu.obj");
        File.WriteAllText(objPath, cube);
        var imp = Library3D.Import(objPath, null, 1.0, libDir);
        check("Thư viện: nhận nhóm Thiết bị vệ sinh, bộ môn Điện nước", B(imp.Category == "Thiết bị vệ sinh" && imp.Discipline == Discipline.DienNuoc), 1, 0);
        check("Thư viện: lưu cục bộ và đọc lại", Library3D.LoadAll(libDir).Count, 1, 0);
        var daeLib = Library3D.ReadDae(MeshExport.Dae(new List<Mesh> { MeshExport.ToMesh(colS) }, "cot"), "Cột mẫu");
        check("Thư viện DAE: 8 đỉnh, 12 tam giác", daeLib.Vertices.Count * 100 + daeLib.Faces.Count, 812, 0);
        check("Thư viện DAE: cao 3,3", daeLib.Height, 3.3, 1e-6);
        int nf0 = p.Arch.Furniture.Count;
        var placed = Library3D.Place(p, li, "T2", 1.0, 1.0, 90);
        check("Đặt mục thư viện vào tầng", p.Arch.Furniture.Count - nf0, 1, 0);
        check("Mục đặt có khối IfcFurnishingElement", B(ModelSolids.Build(p).Any(s => s.Id == "DD-" + placed.Id && s.IfcType == "IfcFurnishingElement")), 1, 0);

        // ---------------- 9. Revit: kiến trúc + điện nước, cập nhật theo UniqueId ----------------
        var ex = new RevitExchange
        {
            ProjectName = "Revit mẫu",
            Levels = new() { new RvLevel { Name = "L1", Elevation = 0 }, new RvLevel { Name = "L2", Elevation = 3.3 } },
            Grids = new() { new RvGrid { Name = "1", X1 = 10, Y1 = 20, X2 = 10, Y2 = 30 }, new RvGrid { Name = "2", X1 = 14, Y1 = 20, X2 = 14, Y2 = 30 },
                            new RvGrid { Name = "A", X1 = 5, Y1 = 20, X2 = 20, Y2 = 20 }, new RvGrid { Name = "B", X1 = 5, Y1 = 25, X2 = 20, Y2 = 25 } },
            Columns = new()
        };
        foreach (var (cx, cy) in new[] { (10.0, 20.0), (14.0, 20.0), (14.0, 25.0), (10.0, 25.0) })
            ex.Columns.Add(new RvColumn { Id = $"c{cx}{cy}", X = cx, Y = cy, BaseLevel = "L1", TopLevel = "L2", B = 0.22, H = 0.22 });
        ex.Walls = new()
        {
            new RvWall { Id = "w1", Level = "L1", X1 = 10, Y1 = 20, X2 = 14, Y2 = 20, Height = 3.0, TypeName = "Tường 220 Revit", Layers = new() { new RvLayer { Function = "Finish1", Material = "Vữa xi măng", Thickness = 0.015 }, new RvLayer { Function = "Structure", Material = "Gạch đặc", Thickness = 0.22 }, new RvLayer { Function = "Finish2", Material = "Vữa xi măng", Thickness = 0.015 } } },
            new RvWall { Id = "w2", Level = "L1", X1 = 14, Y1 = 20, X2 = 14, Y2 = 25, Height = 3.0, TypeName = "Tường 220 Revit" },
            new RvWall { Id = "w3", Level = "L1", X1 = 14, Y1 = 25, X2 = 10, Y2 = 25, Height = 3.0, TypeName = "Tường 220 Revit" },
            new RvWall { Id = "w4", Level = "L1", X1 = 10, Y1 = 25, X2 = 10, Y2 = 20, Height = 3.0, TypeName = "Tường 220 Revit" }
        };
        ex.Openings = new() { new RvOpening { Id = "d1", HostId = "w1", Category = "Door", X = 12, Y = 20, Width = 0.9, Height = 2.2 } };
        ex.Rooms = new() { new RvRoom { Id = "r1", Level = "L1", Name = "Phòng khách", Number = "101", Outline = new() { new[] { 10.0, 20 }, new[] { 14.0, 20 }, new[] { 14.0, 25 }, new[] { 10.0, 25 } } } };
        ex.Mep = new()
        {
            new RvMep { Id = "m1", Category = "Pipe", Name = "Ống PPR D25", X1 = 10.5, Y1 = 21, Z1 = 0.3, X2 = 13.5, Y2 = 21, Z2 = 0.3, Diameter = 0.025 },
            new RvMep { Id = "m2", Category = "PlumbingFixture", Name = "Lavabo", X1 = 12, Y1 = 24, Z1 = 0.8, W = 0.5, D = 0.4, H = 0.2 }
        };
        var p3 = ModelBuilder.NewProject("Revit");
        var r3 = RevitJsonImporter.Build(p3, ex);
        check("Revit: 4 tường có UniqueId", p3.Walls.Count(w => w.ExternalId != null), 4, 0);
        check("Revit: dời gốc theo trục (tường đầu 0,0 → 4,0)", p3.Walls.First(w => w.ExternalId == "w1").X2, 4.0, 1e-9);
        check("Revit: kiểu tường nhiều lớp 250 mm", p3.WallTypes.First(t => t.Name == "Tường 220 Revit").TotalThickness, 0.25, 1e-9);
        check("Revit: cửa đi gắn tường w1 cách đầu 2 m", p3.Walls.First(w => w.ExternalId == "w1").Openings.FirstOrDefault()?.Offset ?? -1, 2.0, 1e-6);
        check("Revit: 1 phòng 20 m²", p3.Arch.Rooms.Sum(r => r.Area), 20, 1e-9);
        check("Revit: 2 đối tượng điện nước", p3.Arch.Furniture.Count(f => f.Discipline == Discipline.DienNuoc), 2, 0);
        check("Revit: báo cáo ghi phần kiến trúc", B(r3.Info.Any(i => i.Contains("Kiến trúc từ Revit"))), 1, 0);
        check("Revit: ống thành khối Ống nước (IfcFlowSegment, cho phép ống lồng)", B(ModelSolids.Build(p3).Any(s => s.IfcType == "IfcFlowSegment" && s.AllowSleeve)), 1, 0);
        ex.Walls[0].X2 = 15;                                  // sửa w1
        ex.Walls.RemoveAt(3);                                 // xoá w4
        ex.Walls.Add(new RvWall { Id = "w5", Level = "L1", X1 = 12, Y1 = 20, X2 = 12, Y2 = 25, Height = 3.0, TypeName = "Tường 220 Revit" });
        ex.Mep.RemoveAt(1);                                   // xoá lavabo
        var up = RevitArchImport.Update(p3, ex);
        check("Cập nhật Revit: thêm 1 (w5)", up.Added, 1, 0);
        check("Cập nhật Revit: sửa 1 (w1)", up.Updated, 1, 0);
        check("Cập nhật Revit: xoá 2 (w4 + lavabo)", up.Removed, 2, 0);
        check("Cập nhật Revit: w1 dài 5 m", p3.Walls.First(w => w.ExternalId == "w1").Chord, 5.0, 1e-9);
        check("Cập nhật Revit: còn 4 tường", p3.Walls.Count, 4, 0);

        // ---------------- 10. Hồ sơ theo bộ môn ----------------
        var kt = DisciplineDocs.Build(p, null, new DisciplineDocOptions { Disciplines = new() { Discipline.KienTruc } });
        check("Hồ sơ KT: có mặt bằng + thống kê cửa + thống kê phòng", B(kt.Count >= 3), 1, 0);
        check("Hồ sơ KT: số hiệu KT-01…", B(kt.All(i => i.Number.StartsWith("KT-")) && kt[0].Number == "KT-01"), 1, 0);
        check("Hồ sơ KT: tên file theo số hiệu", B(kt.All(i => i.FileName.StartsWith(i.Number + "_"))), 1, 0);
        var ktPlan = kt.First(i => i.Key == "KT:T2");
        string ktTxt = ktPlan.W.ToDxfStringDec();
        check("Mặt bằng KT: có tên phòng WC", B(ktTxt.Contains("WC")), 1, 0);
        check("Mặt bằng KT: có diện tích phòng", B(ktTxt.Contains("m²")), 1, 0);
        foreach (var it in kt) it.W.Save(Path.Combine(dir, it.FileName));
        var dn = DisciplineDocs.Build(p3, null, new DisciplineDocOptions { Disciplines = new() { Discipline.DienNuoc } });
        check("Hồ sơ DN (tạm): 1 mặt bằng điện nước", dn.Count, 1, 0);
        check("Hồ sơ DN: số hiệu DN-01", B(dn.Count == 1 && dn[0].Number == "DN-01"), 1, 0);
        foreach (var it in dn) it.W.Save(Path.Combine(dir, it.FileName));
    }
}
