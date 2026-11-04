using System.Globalization;
using System.Text;
using System.Text.Json;
using KetCauVXT.Dwg;
using KetCauVXT.Import;
using KetCauVXT.Import.Link;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Bản vẽ AutoCAD liên kết (Model/CadLink.cs, Import/Link/*): đọc DXF mm, phép đặt (điểm gốc, dời, xoay, tỷ lệ), đơn vị ghi đè,
/// bắt điểm vào nét bản vẽ (đầu nét, giao nét, layer ẩn), phát hiện bản vẽ thay đổi (mã băm), so sánh bản vẽ ↔ mô hình
/// (thêm 1 tường), áp dụng, đường dẫn tương đối khi dời thư mục dự án, JSON, cảnh báo thiếu file, biến thể DWG (ACadSharp).
/// File tạm ghi vào thư mục tạm của hệ thống (không ghi *.dxf vào outDir).</summary>
public static class CadLinkTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static int B(bool b) => b ? 1 : 0;
    private static bool Near(double a, double b, double tol = 1e-6) => Math.Abs(a - b) <= tol;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string root = Path.Combine(Path.GetTempPath(), "kcvxt_cadlink_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(root);
        try { Main(check, root); }
        catch (Exception ex)
        {
            Console.WriteLine("   LOI CadLinkTests: " + ex);
            check("Bản vẽ link: chạy hết bộ kiểm thử không ngoại lệ", 0, 1, 0);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (Exception) { /* bỏ qua */ }
        }
    }

    /// <summary>Bộ dựng DXF ASCII đơn giản (đơn vị mm).</summary>
    private sealed class Dxf
    {
        private readonly StringBuilder _sb = new();
        public void P(int code, object v) => _sb.Append(code.ToString(Inv)).Append('\n').Append(v is double d ? d.ToString("R", Inv) : Convert.ToString(v, Inv)).Append('\n');
        public Dxf(int insUnits = 4)
        {
            P(0, "SECTION"); P(2, "HEADER"); P(9, "$ACADVER"); P(1, "AC1009"); P(9, "$INSUNITS"); P(70, insUnits); P(0, "ENDSEC");
            P(0, "SECTION"); P(2, "ENTITIES");
        }
        public void Line(string layer, double x1, double y1, double x2, double y2)
        { P(0, "LINE"); P(8, layer); P(10, x1); P(20, y1); P(30, 0.0); P(11, x2); P(21, y2); P(31, 0.0); }
        public void Text(string layer, double x, double y, double h, string s)
        { P(0, "TEXT"); P(8, layer); P(10, x); P(20, y); P(30, 0.0); P(40, h); P(1, s); }
        public string End() { P(0, "ENDSEC"); P(0, "EOF"); return _sb.ToString(); }
        public void Save(string path) => File.WriteAllText(path, End(), new UTF8Encoding(false));
    }

    /// <summary>Mặt bằng 8 × 6 m: tường bao 220 (2 nét, tim trên trục), trục 1-2-3 (x = 0/4/8 m), A-B (y = 0/6 m) có tên.
    /// interior = thêm tường ngăn 110 tại trục 2 (hai nét x = 3945 / 4055 mm).</summary>
    private static void Plan(Dxf d, bool interior)
    {
        const string W = "KT-TUONG", G = "KT-TRUC";
        d.Line(W, -110, -110, 8110, -110); d.Line(W, 110, 110, 7890, 110);
        d.Line(W, 110, 5890, 7890, 5890); d.Line(W, -110, 6110, 8110, 6110);
        d.Line(W, -110, -110, -110, 6110); d.Line(W, 110, 110, 110, 5890);
        d.Line(W, 7890, 110, 7890, 5890); d.Line(W, 8110, -110, 8110, 6110);
        if (interior) { d.Line(W, 3945, 110, 3945, 5890); d.Line(W, 4055, 110, 4055, 5890); }
        foreach (var (x, n) in new[] { (0.0, "1"), (4000.0, "2"), (8000.0, "3") }) { d.Line(G, x, -1500, x, 9000); d.Text(G, x, 9400, 350, n); }
        foreach (var (y, n) in new[] { (0.0, "A"), (6000.0, "B") }) { d.Line(G, -1500, y, 9500, y); d.Text(G, -1900, y, 350, n); }
        d.Text("KT-TENHINH", 4000, -2600, 300, "MAT BANG TANG 2");
    }

    private static Project NewProject()
    {
        var p = new Project { Name = "Kiểm thử bản vẽ link" };
        p.Stories.Add(new Story { Name = "T1", Elevation = 3.6, Height = 3.6 });
        p.Stories.Add(new Story { Name = "T2", Elevation = 6.9, Height = 3.3 });
        return p;
    }

    private static void Main(Action<string, double, double, double> check, string root)
    {
        // ------------------------------------------------------------ 1. phép đặt: dời + xoay 90° + tỷ lệ 2, INSUNITS mm → m
        string fT = Path.Combine(root, "PhepDat.dxf");
        {
            var d = new Dxf(4);
            d.Line("NEN", 1000, 0, 3000, 0);        // (1;0) → (3;0) m
            d.Line("NEN", 2000, -1000, 2000, 1000); // cắt nét trên tại (2;0) m
            d.Line("PHU", 0, 5000, 1000, 5000);
            d.Save(fT);
        }
        var l = new CadLink { FilePath = fT, Story = "T1", OffsetX = 10, OffsetY = 5, Rotation = 90, Scale = 2 };
        var rr = CadLinkCache.Reload(l, null, true);
        var g = rr.Geometry;
        check("Bản vẽ link: đọc DXF", B(rr.Ok && g != null), 1, 0);
        check("Bản vẽ link: $INSUNITS = 4 (mm)", g?.InsUnits ?? -1, 4, 0);
        check("Bản vẽ link: hệ số đơn vị mm → m", g?.UnitScale ?? -1, 0.001, 1e-12);
        check("Bản vẽ link: 3 đoạn, 2 layer", (g?.SegmentCount ?? 0) * 10 + (g?.Layers.Count ?? 0), 32, 0);
        check("Bản vẽ link: ghi mã băm SHA-256 + kích thước file vào link", B(l.LastHash.Length == 64 && l.LastLength == new FileInfo(fT).Length && l.LastWriteUtc != null), 1, 0);
        var v = g.View(l);
        int iSeg = Enumerable.Range(0, v.SegCount).FirstOrDefault(i => Near(v.Seg[4 * i], 10, 1e-9) && Near(v.Seg[4 * i + 1], 7, 1e-9));
        check("Bản vẽ link: (1;0) m → xoay 90°, ×2, dời (10;5) → (10;7)", B(Near(v.Seg[4 * iSeg], 10, 1e-9) && Near(v.Seg[4 * iSeg + 1], 7, 1e-9)), 1, 0);
        check("Bản vẽ link: (3;0) m → (10;11)", B(Near(v.Seg[4 * iSeg + 2], 10, 1e-9) && Near(v.Seg[4 * iSeg + 3], 11, 1e-9)), 1, 0);
        var back = l.ToDrawing(10, 11);
        check("Bản vẽ link: ToDrawing ngược lại (10;11) → (3;0)", B(Near(back.X, 3, 1e-9) && Near(back.Y, 0, 1e-9)), 1, 0);
        // điểm gốc ≠ 0: điểm bản vẽ (2;0) đặt vào (0;0) mô hình
        var l2 = new CadLink { BaseX = 2, BaseY = 0, OffsetX = 0, OffsetY = 0, Rotation = 0, Scale = 1 };
        var m2 = l2.ToModel(3, 1);
        check("Bản vẽ link: điểm gốc (2;0) → (3;1) thành (1;1)", B(Near(m2.X, 1) && Near(m2.Y, 1)), 1, 0);
        // căn theo cặp điểm
        var l3 = new CadLink { OffsetX = 1, OffsetY = 1 };
        l3.AlignByPoints(l3.ToModel(5, 5), (20, 30));
        var m3 = l3.ToModel(5, 5);
        check("Bản vẽ link: căn theo 2 điểm – điểm bản vẽ trùng giao trục đích", B(Near(m3.X, 20) && Near(m3.Y, 30)), 1, 0);
        var l4 = new CadLink();
        l4.AlignByTwoPairs((0, 0), (10, 10), (1, 0), (10, 11));
        var m4 = l4.ToModel(1, 0);
        check("Bản vẽ link: căn hai cặp điểm → xoay 90°", B(Near(l4.Rotation, 90, 1e-6) && Near(m4.X, 10, 1e-6) && Near(m4.Y, 11, 1e-6)), 1, 0);
        // đơn vị ghi đè (cm): toạ độ ×10
        var lcm = new CadLink { FilePath = fT, UnitScale = 0.01 };
        var gcm = CadLinkCache.Reload(lcm, null, true).Geometry;
        var vcm = gcm.View(lcm);
        check("Bản vẽ link: đơn vị ghi đè cm → (1000;0) = 10 m", B(Enumerable.Range(0, vcm.SegCount).Any(i => Near(vcm.Seg[4 * i], 10, 1e-9) && Near(vcm.Seg[4 * i + 1], 0, 1e-9))), 1, 0);
        CadLinkCache.Remove(lcm);

        // ------------------------------------------------------------ 2. bắt điểm vào nét bản vẽ
        var p = NewProject();
        p.CadLinks.Add(l);
        var c1 = BeamTools.SnapCandidates(p, "T1", 10.03, 7.02, 0.15);
        check("Bắt điểm link: có đầu nét (10;7)", B(c1.Any(c => c.Kind == SnapKind.CadEnd && Near(c.X, 10, 1e-4) && Near(c.Y, 7, 1e-4))), 1, 0);
        var b2 = BeamTools.BestSnap(p, "T1", 10.03, 9.02, 0.15);
        check("Bắt điểm link: giao nét (10;9) ưu tiên hơn trung điểm", B(b2.Kind == SnapKind.CadIntersection && Near(b2.X, 10, 1e-4) && Near(b2.Y, 9, 1e-4)), 1, 0);
        var c3 = BeamTools.SnapCandidates(p, "T1", 10.03, 9.02, 0.15, SnapMask.All & ~SnapMask.CadLink);
        check("Bắt điểm link: tắt cờ CadLink → không có điểm bản vẽ", c3.Count(c => c.Kind is SnapKind.CadEnd or SnapKind.CadMid or SnapKind.CadIntersection or SnapKind.CadCenter), 0, 0);
        var c4 = BeamTools.SnapCandidates(p, "T2", 10.03, 7.02, 0.15);
        check("Bắt điểm link: bản vẽ gắn tầng T1 không bắt ở tầng T2", c4.Count(c => c.Kind == SnapKind.CadEnd), 0, 0);
        l.HiddenLayers.Add("NEN");
        var c5 = BeamTools.SnapCandidates(p, "T1", 10.03, 7.02, 0.15);
        check("Bắt điểm link: layer ẩn không bắt điểm", c5.Count(c => c.Kind == SnapKind.CadEnd), 0, 0);
        l.HiddenLayers.Clear();
        check("Bắt điểm link: chỉ mục lưới băm – đoạn gần (10;9)", CadLinkSnap.Active(p, "T1").First().g.View(l).SegmentsNear(10, 9, 0.1).Count, 2, 0);

        // ------------------------------------------------------------ 3. liên kết sống: so sánh, thay đổi file, áp dụng
        string dir1 = Path.Combine(root, "DuAn1");
        Directory.CreateDirectory(Path.Combine(dir1, "KT"));
        string proj1 = Path.Combine(dir1, "NhaPho.kcvxt");
        string plan1 = Path.Combine(dir1, "KT", "MatBang.dxf");
        { var d = new Dxf(4); Plan(d, false); d.Save(plan1); }
        var pp = NewProject();
        var lk = new CadLink { FilePath = plan1, Story = "T1", OffsetX = 10, OffsetY = 5 };
        pp.CadLinks.Add(lk);
        var r0 = CadLinkCache.Reload(lk, proj1, true);
        check("Liên kết sống: đọc mặt bằng", B(r0.Ok && !r0.Changed), 1, 0);
        var d0 = CadLinkSync.Compare(pp, lk, r0.Geometry, "T1");
        foreach (var w in d0.Warnings.Take(3)) Console.WriteLine("   · " + w);
        Console.WriteLine("   · " + d0.Summary);
        int addW0 = d0.Count(CadDiffAction.Add, CadDiffElement.Wall);
        check("So sánh lần đầu: nhận dạng tường bao (≥ 4 đoạn thêm mới)", B(addW0 >= 4), 1, 0);
        check("So sánh lần đầu: có trục 2 tại x = 14 m (dời 10 m)", B(d0.Items.Any(i => i.Action == CadDiffAction.Add && i.Element == CadDiffElement.Grid && i.Dir == GridDir.X && Near(i.Coord, 14, 1e-3))), 1, 0);
        check("So sánh lần đầu: tường bao toạ độ mô hình (tim x = 10 … 18)",
            B(d0.Items.Where(i => i.Element == CadDiffElement.Wall).All(i => i.X1 >= 9.9 && i.X2 <= 18.1 && i.Y1 >= 4.9 && i.Y2 <= 11.1)), 1, 0);
        check("So sánh lần đầu: không có mục xoá / dời", d0.Items.Count(i => i.Action != CadDiffAction.Add), 0, 0);
        var a0 = CadLinkSync.Apply(pp, d0);
        check("Áp dụng lần đầu: thành công", B(a0.Ok), 1, 0);
        check("Áp dụng lần đầu: số tường mô hình = số mục thêm", pp.Walls.Count, addW0, 0);
        check("Áp dụng lần đầu: tường 220 → loại tường dày 0,22", B(pp.Walls.All(w => pp.WallTypes.First(t => t.Name == w.Type).Thickness == 0.22)), 1, 0);
        check("Áp dụng: ghi mã băm đã đồng bộ", B(lk.SyncedHash == r0.Geometry.Hash), 1, 0);
        var d1 = CadLinkSync.Compare(pp, lk, r0.Geometry, "T1");
        check("So sánh lại ngay: bản vẽ khớp mô hình (không thay đổi)", d1.Items.Count, 0, 0);

        // AutoCAD lưu bản vẽ: thêm một tường ngăn 2 nét
        string oldHash = lk.LastHash;
        { var d = new Dxf(4); Plan(d, true); d.Save(plan1); }
        File.SetLastWriteTimeUtc(plan1, DateTime.UtcNow.AddSeconds(2));
        check("Liên kết sống: phát hiện file đổi (thời điểm ghi / kích thước)", B(CadLinkCache.LooksChanged(lk, plan1)), 1, 0);
        var r1 = CadLinkCache.Reload(lk, proj1);
        check("Liên kết sống: đọc lại → mã băm khác (Changed)", B(r1.Ok && r1.Changed && lk.LastHash != oldHash), 1, 0);
        var d2 = CadLinkSync.Compare(pp, lk, r1.Geometry, "T1");
        Console.WriteLine("   · " + d2.Summary);
        check("Khác biệt: đúng 1 tường mới", d2.Count(CadDiffAction.Add, CadDiffElement.Wall), 1, 0);
        check("Khác biệt: không có mục khác", d2.Items.Count, 1, 0);
        var nw = d2.Items.FirstOrDefault(i => i.Element == CadDiffElement.Wall);
        check("Khác biệt: tường mới trên trục 2 (x = 14 m), dày 110", B(nw != null && Near(nw.X1, 14, 0.01) && Near(nw.X2, 14, 0.01) && Near(nw.Thickness, 0.11, 0.01)), 1, 0);
        int nW = pp.Walls.Count;
        var a2 = CadLinkSync.Apply(pp, d2);
        check("Áp dụng: thêm 1 tường", pp.Walls.Count - nW, 1, 0);
        check("Áp dụng: tường mới loại 110", B(a2.Ok && pp.WallTypes.First(t => t.Name == pp.Walls[^1].Type).Thickness == 0.11 && pp.Walls[^1].Story == "T1"), 1, 0);
        check("So sánh sau áp dụng: khớp", CadLinkSync.Compare(pp, lk, r1.Geometry, "T1").Items.Count, 0, 0);
        // tường mô hình không còn trên bản vẽ → mục xoá, mặc định không đánh dấu
        pp.Walls.Add(new WallLine { Id = 999, Story = "T1", X1 = 11, Y1 = 8, X2 = 13, Y2 = 8, Type = pp.WallTypes[0].Name });
        var d3 = CadLinkSync.Compare(pp, lk, r1.Geometry, "T1");
        var rm = d3.Items.FirstOrDefault(i => i.Action == CadDiffAction.Remove && i.Element == CadDiffElement.Wall);
        check("Khác biệt: tường mô hình không có trên bản vẽ → xoá (không đánh dấu sẵn)", B(rm != null && rm.WallId == 999 && !rm.Apply), 1, 0);
        // tường lệch 0,2 m → dời
        var ww = pp.Walls.First(w => Near(w.X1, 14, 0.01) && Near(w.X2, 14, 0.01));
        ww.X1 += 0.2; ww.X2 += 0.2;
        var d4 = CadLinkSync.Compare(pp, lk, r1.Geometry, "T1");
        var mv = d4.Items.FirstOrDefault(i => i.Action == CadDiffAction.Move && i.Element == CadDiffElement.Wall);
        check("Khác biệt: tường lệch 0,2 m → dời về vị trí bản vẽ", B(mv != null && mv.WallId == ww.Id && Near(mv.Distance, 0.2, 0.01)), 1, 0);
        foreach (var it in d4.Items) it.Apply = it.Action == CadDiffAction.Move;
        CadLinkSync.Apply(pp, d4);
        check("Áp dụng dời: tường về x = 14 m", B(Near(ww.X1, 14, 0.01) && Near(ww.X2, 14, 0.01)), 1, 0);
        pp.Walls.RemoveAll(w => w.Id == 999);

        // ------------------------------------------------------------ 4. đường dẫn tương đối, JSON
        pp.UpdateCadLinkPaths(proj1);
        check("Đường dẫn tương đối theo file dự án", B(lk.RelativePath.Replace('\\', '/') == "KT/MatBang.dxf"), 1, 0);
        pp.Save(proj1);
        string dir2 = Path.Combine(root, "DuAn_DaDoi");
        Directory.Move(dir1, dir2);
        string proj2 = Path.Combine(dir2, "NhaPho.kcvxt");
        var p2 = Project.Load(proj2);
        var lk2 = p2.CadLinks.FirstOrDefault();
        check("JSON: giữ bản vẽ liên kết", p2.CadLinks.Count, 1, 0);
        string res2 = lk2?.Resolve(proj2);
        check("Dời cả thư mục dự án + bản vẽ: tìm lại theo đường dẫn tương đối", B(res2 != null && string.Equals(Path.GetFullPath(res2), Path.GetFullPath(Path.Combine(dir2, "KT", "MatBang.dxf")), StringComparison.OrdinalIgnoreCase)), 1, 0);
        check("JSON: Id, tầng, điểm chèn, mã băm giữ nguyên",
            B(lk2 != null && lk2.Id == lk.Id && lk2.Story == "T1" && Near(lk2.OffsetX, 10) && Near(lk2.OffsetY, 5) && lk2.LastHash == lk.LastHash && lk2.SyncedHash == lk.SyncedHash), 1, 0);
        var lj = new CadLink { Story = CadLink.AllStories, Rotation = 12.5, Scale = 0.5, UnitScale = 0.01, Opacity = 0.3, ByLayerColor = true, ShowText = true, Snap = false, AutoReload = false };
        lj.HiddenLayers.Add("KT-TEXT"); lj.LayerMap.Add(new LayerMapEntry("XAY", CadCategory.Tuong));
        var pj = NewProject(); pj.CadLinks.Add(lj);
        var pj2 = JsonSerializer.Deserialize<Project>(JsonSerializer.Serialize(pj, Project.JsonOptions), Project.JsonOptions);
        var lj2 = pj2.CadLinks[0];
        check("JSON: vòng lặp đủ thuộc tính", B(lj2.IsAllStories && Near(lj2.Rotation, 12.5) && Near(lj2.Scale, 0.5) && Near(lj2.UnitScale, 0.01) && Near(lj2.Opacity, 0.3)
                                             && lj2.ByLayerColor && lj2.ShowText && !lj2.Snap && !lj2.AutoReload && lj2.HiddenLayers.SequenceEqual(new[] { "KT-TEXT" })
                                             && lj2.LayerMap.Count == 1 && lj2.LayerMap[0].Category == CadCategory.Tuong), 1, 0);
        var old = JsonSerializer.Deserialize<Project>("{\"Name\":\"Dự án cũ\"}", Project.JsonOptions);
        check("JSON: file dự án cũ (không có CadLinks) → danh sách rỗng", B(old.CadLinks != null && old.CadLinks.Count == 0), 1, 0);
        check("JSON: không ghi thuộc tính tính toán (DisplayName, TransformKey)", B(!JsonSerializer.Serialize(lj, Project.JsonOptions).Contains("TransformKey") && !JsonSerializer.Serialize(lj, Project.JsonOptions).Contains("DisplayName")), 1, 0);

        // ------------------------------------------------------------ 5. thiếu file
        var lm = new CadLink { FilePath = Path.Combine(root, "khong_co", "MatBang.dxf"), RelativePath = "khong_co.dxf" };
        check("Thiếu file: Resolve = null", B(lm.Resolve(proj2) == null), 1, 0);
        var rm2 = CadLinkCache.Reload(lm, proj2);
        check("Thiếu file: cảnh báo \"Không tìm thấy bản vẽ link – chọn lại\"", B(!rm2.Ok && rm2.Missing && rm2.Error.Contains("Không tìm thấy bản vẽ link – chọn lại")), 1, 0);

        // ------------------------------------------------------------ 6. DWG (ACadSharp)
        var prev = CadFileReaders.Dwg;
        try
        {
            DwgReader.Register();
            string dwg = Path.Combine(root, "MauLink.dwg");
            DwgReader.WriteDwg(DwgSample.BuildHouse("KT-"), dwg);
            var ld = new CadLink { FilePath = dwg, Story = "T1" };
            var rd = CadLinkCache.Reload(ld, null, true);
            check("DWG link: đọc qua CadFileReaders.Dwg", B(rd.Ok && rd.Geometry != null && rd.Geometry.SegmentCount > 10), 1, 0);
            check("DWG link: đơn vị mm → m", rd.Geometry?.UnitScale ?? -1, 0.001, 1e-12);
            var pd = NewProject(); pd.CadLinks.Add(ld);
            var dd = CadLinkSync.Compare(pd, ld, rd.Geometry, "T1");
            check("DWG link: nhận dạng tường để cập nhật mô hình", B(dd.Count(CadDiffAction.Add, CadDiffElement.Wall) > 0), 1, 0);
            var cd = BeamTools.SnapCandidates(pd, "T1", 0.0, -0.11, 0.05);
            check("DWG link: bắt điểm đầu nét tường (0;-0,11)", B(cd.Any(c => c.Kind is SnapKind.CadEnd or SnapKind.CadIntersection)), 1, 0);
            CadLinkCache.Remove(ld);
        }
        finally { CadFileReaders.Dwg = prev; }
        CadLinkCache.Remove(l); CadLinkCache.Remove(lk);
    }
}
