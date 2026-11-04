using KetCauVXT.Analysis;
using KetCauVXT.Export;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng công cụ khai báo / vẽ dầm kiểu ETABS (Model/BeamTools.cs): nhập toạ độ, bắt điểm, vẽ nhanh theo đoạn trục,
/// dầm phụ theo số lượng / khoảng cách / danh sách, dầm theo trục + khoảng cách, công xôn, sao chép, đối xứng, phạm vi tầng,
/// thư viện tiết diện, điểm chèn, lưu/mở và cân bằng lực sau khi thêm dầm phụ. check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class BeamToolsTests
{
    /// <summary>Nhà khung 1 nhịp X (5 m) × 1 nhịp Y (4,5 m) hoặc nhiều nhịp, các tầng theo chiều cao; dầm D22x30, cột C22x30.</summary>
    private static Project House(double[] spansX, double[] spansY, double[] heights, bool build = true)
    {
        var p = ModelBuilder.NewProject("Kiểm tra công cụ dầm");
        ModelBuilder.SetGrid(p, spansX, spansY);
        ModelBuilder.SetStories(p, heights, -0.6);
        if (build) ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        return p;
    }

    private static List<FrameElement> NewBeams(Project p, EditResult r) => p.Frames.Where(f => r.NewFrames.Contains(f.Id)).ToList();

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Directory.CreateDirectory(outDir);

        // ---------------- 1. nhập toạ độ ----------------
        {
            var a = BeamTools.ParseCoordinateInput("3000,4500", null, CoordUnit.Mm);
            check("Toạ độ tuyệt đối mm \"3000,4500\": x = 3 m", a.Ok ? a.X : -1, 3.0, 1e-9);
            check("Toạ độ tuyệt đối mm \"3000,4500\": y = 4,5 m", a.Y, 4.5, 1e-9);
            check("Toạ độ tuyệt đối: loại Absolute", a.Kind == CoordInputKind.Absolute ? 1 : 0, 1, 0);
            var b = BeamTools.ParseCoordinateInput("3.5;4,2", null, CoordUnit.M);
            check("Toạ độ tuyệt đối m \"3.5;4,2\" (phẩy thập phân khi ngăn cách bằng ;): y = 4,2", b.Ok ? b.Y : -1, 4.2, 1e-9);
            var c = BeamTools.ParseCoordinateInput("@3000,0", (1, 2), CoordUnit.Mm);
            check("Tương đối \"@3000,0\" từ (1; 2): x = 4", c.Ok ? c.X : -1, 4.0, 1e-9);
            check("Tương đối: y giữ nguyên 2", c.Y, 2.0, 1e-9);
            check("Tương đối: loại Relative", c.Kind == CoordInputKind.Relative ? 1 : 0, 1, 0);
            var d = BeamTools.ParseCoordinateInput("3000<90", (1, 2), CoordUnit.Mm);
            check("Cực \"3000<90\" từ (1; 2): y = 5", d.Ok ? d.Y : -1, 5.0, 1e-9);
            check("Cực \"3000<90\": x = 1", d.X, 1.0, 1e-9);
            var e = BeamTools.ParseCoordinateInput("@2m<180", (1, 2), CoordUnit.Mm);
            check("Cực có đơn vị \"@2m<180\": x = −1", e.Ok ? e.X : 99, -1.0, 1e-9);
            var f = BeamTools.ParseCoordinateInput("1500", (0, 0), CoordUnit.Mm, (10, 0));
            check("Khoảng cách theo hướng chuột \"1500\": x = 1,5", f.Ok ? f.X : -1, 1.5, 1e-9);
            check("Khoảng cách theo hướng chuột: loại Distance", f.Kind == CoordInputKind.Distance ? 1 : 0, 1, 0);
            var g = BeamTools.ParseCoordinateInput("1200mm,2,4m", null, CoordUnit.Mm);
            check("Ba số ngăn cách bằng dấu phẩy → báo lỗi", g.Ok ? 0 : 1, 1, 0);
            check("Chuỗi sai \"abc\" → báo lỗi", BeamTools.ParseCoordinateInput("abc", null, CoordUnit.Mm).Ok ? 0 : 1, 1, 0);
            check("Khoảng cách không có điểm trước → báo lỗi", BeamTools.ParseCoordinateInput("1500", null, CoordUnit.Mm).Ok ? 0 : 1, 1, 0);
            check("Đơn vị cm: \"120cm\" = 1,2 m", BeamTools.TryLength("120cm", CoordUnit.Mm, out double l1) ? l1 : -1, 1.2, 1e-12);
            check("Đơn vị m khi cài đặt m: \"1,25\" = 1,25 m", BeamTools.TryLength("1,25", CoordUnit.M, out double l2) ? l2 : -1, 1.25, 1e-12);
        }

        // ---------------- 2. bắt điểm: thứ tự ưu tiên ----------------
        {
            var p = House(new[] { 5.0 }, new[] { 4.5 }, new[] { 3.6, 3.3 });
            string st = "T2";
            var s1 = BeamTools.BestSnap(p, st, 5.03, 0.02, 0.15);
            check("Bắt điểm: gần nút góc → đầu dầm/nút", s1.Kind is SnapKind.End or SnapKind.Node ? 1 : 0, 1, 0);
            check("Bắt điểm: toạ độ nút góc x = 5", s1.X, 5.0, 1e-9);
            var s2 = BeamTools.BestSnap(p, st, 2.52, 0.03, 0.15);
            check("Bắt điểm: gần giữa dầm → trung điểm", s2.Kind == SnapKind.Midpoint ? 1 : 0, 1, 0);
            check("Bắt điểm: trung điểm x = 2,5", s2.X, 2.5, 1e-9);
            var s3 = BeamTools.BestSnap(p, st, 1.2, 0.04, 0.15);
            check("Bắt điểm: trên dầm xa nút/giữa → điểm gần nhất trên dầm", s3.Kind == SnapKind.Nearest ? 1 : 0, 1, 0);
            check("Bắt điểm: điểm gần nhất nằm trên dầm (y = 0)", s3.Y, 0.0, 1e-9);
            var s4 = BeamTools.BestSnap(p, st, 1.32, 4.45, 0.15, SnapMask.All, (1.3, 2.0));
            check("Bắt điểm: chân đường vuông góc ưu tiên hơn điểm gần nhất", s4.Kind == SnapKind.Perpendicular ? 1 : 0, 1, 0);
            check("Bắt điểm: chân vuông góc x = 1,3", s4.X, 1.3, 1e-9);
            var s5 = BeamTools.BestSnap(p, st, 2.52, 0.03, 0.15, SnapMask.All & ~SnapMask.Midpoint);
            check("Bắt điểm: tắt trung điểm → điểm gần nhất", s5.Kind == SnapKind.Nearest ? 1 : 0, 1, 0);
            var s6 = BeamTools.BestSnap(p, st, 2.37, 2.21, 0.15);
            check("Bắt điểm: không có đối tượng → lưới 50 mm", s6.Kind == SnapKind.Round ? 1 : 0, 1, 0);
            check("Bắt điểm: làm tròn 50 mm x = 2,35", s6.X, 2.35, 1e-9);
            var s7 = BeamTools.BestSnap(p, st, 0.05, 4.4, 0.2, SnapMask.GridIntersection);
            check("Bắt điểm: chỉ bật giao trục → giao trục 1/B", s7.Kind == SnapKind.GridIntersection && Math.Abs(s7.Y - 4.5) < 1e-9 ? 1 : 0, 1, 0);
            var cands = BeamTools.SnapCandidates(p, st, 2.52, 0.03, 0.15);
            check("Bắt điểm: danh sách ứng viên sắp theo ưu tiên", cands.Zip(cands.Skip(1)).All(q => q.First.Priority <= q.Second.Priority) ? 1 : 0, 1, 0);
            // giao điểm dầm × trục (không có nút)
            p.Grids.Add(new GridLine { Name = "1'", Dir = GridDir.X, Coord = 3.0 });
            var s8 = BeamTools.BestSnap(p, st, 3.02, 0.06, 0.15, SnapMask.Intersection | SnapMask.Nearest);
            check("Bắt điểm: giao điểm dầm × trục phụ ưu tiên hơn điểm gần nhất", s8.Kind == SnapKind.Intersection && Math.Abs(s8.X - 3.0) < 1e-9 ? 1 : 0, 1, 0);
        }

        // ---------------- 3. vẽ nhanh dầm theo đoạn trục ----------------
        {
            var p = House(new[] { 5.0, 4.0 }, new[] { 4.5 }, new[] { 3.6, 3.3 }, build: false);
            string st = "T2";
            var segs = BeamTools.GridSegments(p);
            check("Đoạn trục: 3 trục số × 1 nhịp + 2 trục chữ × 2 nhịp = 7 đoạn", segs.Count, 7, 0);
            var r = BeamTools.QuickBeamsOnGridSegments(p, st, segs, "D22x30");
            check("Vẽ nhanh theo lưới: thành công", r.Ok ? 1 : 0, 1, 0);
            check("Vẽ nhanh theo lưới: 7 dầm", ModelEdit.StoryBeams(p, st).Count, 7, 0);
            var again = BeamTools.QuickBeamsOnGridSegments(p, st, segs, "D22x30");
            check("Vẽ nhanh lần 2: không tạo dầm trùng", again.Ok ? 0 : 1, 1, 0);
            check("Vẽ nhanh lần 2: số dầm giữ nguyên 7", ModelEdit.StoryBeams(p, st).Count, 7, 0);
            var at = BeamTools.GridSegmentAt(p, 2.5, 0.05, 0.2);
            check("Bấm gần trục A giữa 1–2 → đoạn A: 1–2", at != null && at.Line.Name == "A" && at.FromGrid == "1" && at.ToGrid == "2" ? 1 : 0, 1, 0);
            check("Cửa sổ chọn (window) bao nhịp 1–2: 4 đoạn", BeamTools.GridSegmentsInWindow(p, -0.1, -0.1, 5.1, 4.6, false).Count, 4, 0);
            check("Cửa sổ cắt (crossing) giữa nhịp 1–2: 2 đoạn trục A, B", BeamTools.GridSegmentsInWindow(p, 2, -1, 3, 5.5, true).Count, 2, 0);
            // dầm có sẵn một phần: chỉ thêm phần còn thiếu
            var p2 = House(new[] { 5.0 }, new[] { 4.5 }, new[] { 3.6, 3.3 }, build: false);
            BeamTools.AddBeamExact(p2, st, 0, 0, 2, 0, "D22x30");
            var r2 = BeamTools.QuickBeamsOnGridSegments(p2, st, new[] { BeamTools.GridSegmentAt(p2, 2.5, 0, 0.1) }, "D22x30");
            double tot = ModelEdit.StoryBeams(p2, st).Sum(q => p2.Length(q));
            check("Đoạn trục có dầm một phần: tổng chiều dài dầm trên trục A = 5 m (không chồng)", tot, 5.0, 1e-9);
            check("Đoạn trục có dầm một phần: thêm đúng 1 dầm 3 m", r2.Ok && r2.NewFrames.Count == 1 ? p2.Length(p2.Frame(r2.NewFrames[0])) : 0, 3.0, 1e-9);
        }

        // ---------------- 4. dầm phụ ----------------
        {
            var o = new SecondaryBeamOptions { Count = 2 };
            var pos = BeamTools.SecondaryPositions(0, 5, o);
            check("Vị trí dầm phụ n = 2 trong 5 m: 2 dầm", pos.Count, 2, 0);
            check("Vị trí dầm phụ n = 2: dầm 1 tại 1,667 m", pos[0], 5.0 / 3, 1e-4);
            var ps = BeamTools.SecondaryPositions(0, 4.5, new SecondaryBeamOptions { Spacing = 1.2 });
            check("Khoảng cách đều 1,2 m trong 4,5 m: 3 dầm (1,2; 2,4; 3,6)", ps.Count, 3, 0);
            check("Khoảng cách đều: dầm cuối 3,6", ps.LastOrDefault(), 3.6, 1e-9);
            var pm = BeamTools.SecondaryPositions(0, 4.5, new SecondaryBeamOptions { Spacing = 1.2, FromEnd = true });
            check("Khoảng cách đều đo từ mép cuối: dầm đầu 0,9 m", pm.FirstOrDefault(), 0.9, 1e-9);
            var po = BeamTools.SecondaryPositions(0, 4.5, new SecondaryBeamOptions { Spacing = 1.5, StartOffset = 0.75 });
            check("Khoảng cách đều + khoảng đầu 0,75: 0,75; 2,25; 3,75", po.Count == 3 && Math.Abs(po[1] - 2.25) < 1e-9 ? 1 : 0, 1, 0);

            var p = House(new[] { 5.0 }, new[] { 4.5 }, new[] { 3.6, 3.3 });
            string st = "T2";
            int slabs0 = p.Slabs.Count(s => s.Story == st);
            var r = BeamTools.SecondaryBeamsAt(p, st, 2.5, 2.2, new SecondaryBeamOptions { AlongX = false, Count = 2, Section = "D22x30" });
            Console.WriteLine("   · " + r.Message);
            var nb = NewBeams(p, r);
            check("Dầm phụ n = 2 trong ô 5×4,5 m: thành công", r.Ok ? 1 : 0, 1, 0);
            check("Dầm phụ n = 2: 2 dầm mới", nb.Count, 2, 0);
            var xs = nb.Select(f => p.Node(f.NodeI).X).OrderBy(v => v).ToList();
            check("Dầm phụ n = 2: khoảng cách 1,667 m", xs.Count == 2 ? xs[1] - xs[0] : 0, 5.0 / 3, 1e-3);
            check("Dầm phụ n = 2: dầm 1 cách trục 1 là 1,667 m", xs.FirstOrDefault(), 5.0 / 3, 1e-3);
            check("Dầm phụ nhịp giữa hai dầm đỡ (L = 4,5 m)", nb.Count > 0 ? p.Length(nb[0]) : 0, 4.5, 1e-9);
            int onA = ModelEdit.StoryBeams(p, st).Count(f => Math.Abs(p.Node(f.NodeI).Y) < 1e-6 && Math.Abs(p.Node(f.NodeJ).Y) < 1e-6);
            check("Dầm đỡ trục A được chia tại đầu dầm phụ: 3 đoạn", onA, 3, 0);
            check("Ô sàn bị dầm phụ cắt được chia: 1 → 3 ô", p.Slabs.Count(s => s.Story == st), slabs0 + 2, 0);
            check("Ô sàn chia giữ tải hoạt tải 1,5 kN/m²", p.Slabs.Where(s => s.Story == st).All(s => Math.Abs(p.AreaLoads.Where(a => a.Slab == s.Id && a.Pattern == "HT").Sum(a => a.Q) - 1.5) < 1e-9) ? 1 : 0, 1, 0);
            check("Mô hình hợp lệ sau khi thêm dầm phụ", p.Validate().Count, 0, 0);
            var res = FrameAnalysis.Run(p);
            double expect = 0;
            foreach (var al in p.AreaLoads.Where(a => a.Pattern == "HT"))
            {
                var sl = p.Slabs.First(x => x.Id == al.Slab);
                expect += al.Q * KetCauVXT.Loads.SlabLoadDistributor.PolygonArea(sl.Nodes.Select(id => p.Node(id).P).ToList());
            }
            check("Dầm phụ: ΣFZ phản lực (HT) = Σ hoạt tải sàn", res.Get("HT").Reactions.Values.Sum(q => q[2]), expect, 1e-6);
            check("Dầm phụ nhận tải sàn (tải SLAB trên dầm phụ)", p.FrameLoads.Any(l => l.Source == "SLAB" && nb.Any(f => f.Id == l.Element)) ? 1 : 0, 1, 0);
            check("Dầm phụ có mô men dương giữa nhịp (HT)", res.Get("HT").Frames.TryGetValue(nb[0].Id, out var ff) && Enumerable.Range(0, ff.Count).Max(i => ff.M3(i)) > 0.1 ? 1 : 0, 1, 0);
            check("Phân tích sau khi thêm dầm phụ: chuyển vị hữu hạn", res.Get("TT").Disp.Values.All(v => v.All(double.IsFinite)) ? 1 : 0, 1, 0);

            // khoảng cách 1,2 m từ trục A (dầm song song X)
            var q = House(new[] { 5.0 }, new[] { 4.5 }, new[] { 3.6, 3.3 });
            var r2 = BeamTools.SecondaryBeamsAt(q, st, 2.5, 2.2, new SecondaryBeamOptions { AlongX = true, Spacing = 1.2, RefGrid = "A", Section = "D22x30" });
            var ys = NewBeams(q, r2).Select(f => q.Node(f.NodeI).Y).Distinct().OrderBy(v => v).ToList();
            check("Dầm phụ cách đều 1,2 m từ trục A: 3 dầm", ys.Count, 3, 0);
            check("Dầm phụ cách đều 1,2 m từ trục A: y = 1,2", ys.ElementAtOrDefault(0), 1.2, 1e-9);
            check("Dầm phụ cách đều 1,2 m từ trục A: y = 2,4", ys.ElementAtOrDefault(1), 2.4, 1e-9);
            check("Dầm phụ cách đều 1,2 m từ trục A: y = 3,6", ys.ElementAtOrDefault(2), 3.6, 1e-9);
            var bad = BeamTools.SecondaryBeamsAt(q, st, 2.5, 2.2, new SecondaryBeamOptions { AlongX = true, Spacing = 1.2, RefGrid = "2", Section = "D22x30" });
            check("Trục mốc không song song dầm phụ → báo lỗi", bad.Ok ? 0 : 1, 1, 0);

            // danh sách khoảng cách từ trục 2 (đo ngược về trục 1)
            var w = House(new[] { 5.0 }, new[] { 4.5 }, new[] { 3.6, 3.3 });
            var r3 = BeamTools.SecondaryBeamsAt(w, st, 2.5, 2.2, new SecondaryBeamOptions { AlongX = false, Distances = new List<double> { 1.0, 2.5 }, RefGrid = "2", Section = "D22x30" });
            var x3 = NewBeams(w, r3).Select(f => w.Node(f.NodeI).X).Distinct().OrderBy(v => v).ToList();
            check("Danh sách khoảng cách 1000; 2500 từ trục 2: 2 dầm", x3.Count, 2, 0);
            check("Danh sách khoảng cách: x = 5 − 2,5 = 2,5", x3.ElementAtOrDefault(0), 2.5, 1e-9);
            check("Danh sách khoảng cách: x = 5 − 1 = 4", x3.ElementAtOrDefault(1), 4.0, 1e-9);
        }

        // ---------------- 5. dầm theo trục + khoảng cách, toạ độ, công xôn ----------------
        {
            var p = House(new[] { 5.0, 4.0 }, new[] { 4.5 }, new[] { 3.6, 3.3 });
            string st = "T2";
            var go = BeamTools.ParseGridOffset(p, "2 + 1200", CoordUnit.Mm);
            check("Đọc \"2 + 1200\": trục 2, cách 1,2 m", go != null && go.Value.Grid.Name == "2" ? go.Value.Offset : -1, 1.2, 1e-12);
            var gn = BeamTools.ParseGridOffset(p, "B-600", CoordUnit.Mm);
            check("Đọc \"B-600\": cách −0,6 m", gn != null ? gn.Value.Offset : 0, -0.6, 1e-12);
            var r = BeamTools.BeamByGridOffset(p, st, "2", 1.2, "A", "B", "D22x30");
            var nb = NewBeams(p, r);
            check("Dầm trục 2 + 1200 từ A đến B: thành công", r.Ok ? 1 : 0, 1, 0);
            check("Dầm trục 2 + 1200: x = 6,2", nb.Count > 0 ? p.Node(nb[0].NodeI).X : 0, 6.2, 1e-9);
            check("Dầm trục 2 + 1200: dài 4,5 m", nb.Sum(f => p.Length(f)), 4.5, 1e-9);
            var bd = BeamTools.BeamByGridOffset(p, st, "2", 0, "1", "3", "D22x30");
            check("Trục giới hạn cùng phương → báo lỗi", bd.Ok ? 0 : 1, 1, 0);
            var rc = BeamTools.BeamByCoordinates(p, st, 1.0, 0, 1.0, 4.5, "D22x30");
            check("Dầm theo toạ độ tuyệt đối (1; 0) – (1; 4,5)", rc.Ok ? p.Length(p.Frame(rc.NewFrames[0])) : 0, 4.5, 1e-9);

            var cx = BeamTools.Cantilever(p, st, (9.0, 4.5), 1.2, 90, "D22x30");
            Console.WriteLine("   · " + cx.Message);
            check("Công xôn dài 1,2 m: thành công", cx.Ok ? 1 : 0, 1, 0);
            var cf = cx.Ok ? p.Frame(cx.NewFrames[0]) : null;
            check("Công xôn: chiều dài 1,2 m", cf != null ? p.Length(cf) : 0, 1.2, 1e-9);
            check("Công xôn: đầu tự do y = 5,7", cf != null ? Math.Max(p.Node(cf.NodeI).Y, p.Node(cf.NodeJ).Y) : 0, 5.7, 1e-9);
            check("Công xôn tại nút có cột: không cảnh báo thiếu gối", cx.Message.Contains("LƯU Ý") ? 0 : 1, 1, 0);
            var cw = BeamTools.Cantilever(p, st, (2.0, 2.0), 1.0, 0, "D22x30");
            check("Công xôn giữa ô (không gối) → có cảnh báo", cw.Ok && cw.Message.Contains("LƯU Ý") ? 1 : 0, 1, 0);
            check("Công xôn quá dài (> 6 m) → báo lỗi", BeamTools.Cantilever(p, st, (9.0, 0), 1200, 0, "D22x30").Ok ? 0 : 1, 1, 0);
        }

        // ---------------- 6. sao chép, đối xứng ----------------
        {
            var p = House(new[] { 5.0 }, new[] { 4.5 }, new[] { 3.6, 3.3 });
            string st = "T2";
            var src = ModelEdit.StoryBeams(p, st).First(f => Math.Abs(p.Node(f.NodeI).X) < 1e-6 && Math.Abs(p.Node(f.NodeJ).X) < 1e-6);
            var r = BeamTools.Replicate(p, new[] { src.Id }, 3.0, 0, 3);
            Console.WriteLine("   · " + r.Message);
            var xs = NewBeams(p, r).Select(f => p.Node(f.NodeI).X).Distinct().OrderBy(v => v).ToList();
            check("Sao chép 3 bản cách 3 m: 3 dầm mới", xs.Count, 3, 0);
            check("Sao chép: x = 3; 6; 9", xs.Count == 3 && Math.Abs(xs[0] - 3) < 1e-9 && Math.Abs(xs[1] - 6) < 1e-9 && Math.Abs(xs[2] - 9) < 1e-9 ? 1 : 0, 1, 0);
            check("Sao chép giữ tiết diện", NewBeams(p, r).All(f => f.Section == src.Section) ? 1 : 0, 1, 0);
            var dup = BeamTools.Replicate(p, new[] { src.Id }, 3.0, 0, 1);
            check("Sao chép trùng vị trí đã có → không tạo dầm", dup.Ok ? 0 : 1, 1, 0);
            var toMai = BeamTools.Replicate(p, r.NewFrames.Take(1), 0, 0, 1, new[] { "MAI" });
            check("Sao chép sang tầng MAI", toMai.Ok && NewBeams(p, toMai).All(f => f.Story == "MAI") ? 1 : 0, 1, 0);

            var m = House(new[] { 5.0, 4.0 }, new[] { 4.5 }, new[] { 3.6, 3.3 });
            var sb = BeamTools.BeamByGridOffset(m, st, "1", 1.5, "A", "B", "D22x40");
            var mr = BeamTools.Mirror(m, sb.NewFrames, BeamTools.FindGrid(m, "2"));
            var mb = NewBeams(m, mr);
            check("Đối xứng qua trục 2 (x = 5): dầm x = 1,5 → 8,5", mb.Count > 0 ? m.Node(mb[0].NodeI).X : 0, 8.5, 1e-9);
            check("Đối xứng: giữ dầm gốc", m.Frames.Any(f => sb.NewFrames.Contains(f.Id)) ? 1 : 0, 1, 0);
            check("Đối xứng: giữ tiết diện D22x40", mb.All(f => f.Section == "D22x40") ? 1 : 0, 1, 0);
            var onAxis = ModelEdit.StoryBeams(m, st).First(f => Math.Abs(m.Node(f.NodeI).X - 5) < 1e-6 && Math.Abs(m.Node(f.NodeJ).X - 5) < 1e-6);
            check("Đối xứng dầm nằm trên trục đối xứng → bỏ qua", BeamTools.Mirror(m, new[] { onAxis.Id }, BeamTools.FindGrid(m, "2")).Ok ? 0 : 1, 1, 0);
            var mf = BeamTools.Mirror(m, sb.NewFrames.Where(id => m.Frames.Any(f => f.Id == id)).ToList(), GridDir.X, 4.0, keepOriginal: false);
            check("Lật (không giữ gốc) qua x = 4: dầm gốc bị xoá", m.Frames.Any(f => sb.NewFrames.Contains(f.Id)) ? 0 : 1, 1, 0);
            check("Lật qua x = 4: dầm mới tại x = 6,5", mf.Ok && NewBeams(m, mf).Any(f => Math.Abs(m.Node(f.NodeI).X - 6.5) < 1e-9) ? 1 : 0, 1, 0);
        }

        // ---------------- 7. phạm vi tầng ----------------
        {
            var p = House(new[] { 5.0 }, new[] { 4.5 }, new[] { 3.6, 3.3, 3.3 });
            var all = p.Stories.Select(s => s.Name).ToList();
            check("Ba tầng T2, T3, MAI", all.Count, 3, 0);
            check("Tầng tương tự (mặt bằng giống nhau): cả 3 tầng", BeamTools.StoriesSimilar(p, "T2").Count, 3, 0);
            check("Phạm vi Tất cả tầng: 3", BeamTools.ResolveStories(p, "T2", StoryScope.All).Count, 3, 0);
            check("Phạm vi Tầng hiện tại: 1", BeamTools.ResolveStories(p, "T2", StoryScope.Current).Count, 1, 0);
            var sel = BeamTools.ResolveStories(p, "T2", StoryScope.Selected, new[] { "T2", "MAI" });
            var opt = new SecondaryBeamOptions { AlongX = false, Count = 1, Section = "D22x30" };
            var r = BeamTools.ForStories(p, sel, st => BeamTools.SecondaryBeamsAt(p, st, 2.5, 2.2, opt));
            Console.WriteLine("   · " + r.Message);
            int Cnt(string st) => ModelEdit.StoryBeams(p, st).Count(f => Math.Abs(p.Node(f.NodeI).X - 2.5) < 1e-6 && Math.Abs(p.Node(f.NodeJ).X - 2.5) < 1e-6);
            check("Đánh dấu T2 + MAI: dầm phụ ở T2", Cnt("T2"), 1, 0);
            check("Đánh dấu T2 + MAI: không có ở T3", Cnt("T3"), 0, 0);
            check("Đánh dấu T2 + MAI: dầm phụ ở MAI", Cnt("MAI"), 1, 0);
            check("Sau khi sửa: tầng tương tự của T3 chỉ còn T3", BeamTools.StoriesSimilar(p, "T3").Count, 1, 0);
            check("Sau khi sửa: T2 tương tự MAI", BeamTools.StoriesSimilar(p, "T2").Contains("MAI") ? 1 : 0, 1, 0);
            check("Phạm vi Tầng tương tự có danh sách đánh dấu → dùng danh sách", BeamTools.ResolveStories(p, "T3", StoryScope.Similar, new[] { "T2" }).Count, 2, 0);
        }

        // ---------------- 8. thư viện tiết diện ----------------
        {
            var p = House(new[] { 5.0 }, new[] { 4.5 }, new[] { 3.6, 3.3 });
            var a = SectionLibrary.AddRect(p, "", 250, 500, "B25");
            check("Thêm tiết diện tên tự động D25x50", a.Ok && p.FrameSections.Any(s => s.Name == "D25x50") ? 1 : 0, 1, 0);
            check("Thêm tiết diện trùng tên → báo lỗi", SectionLibrary.AddRect(p, "D25x50", 250, 500, "B25").Ok ? 0 : 1, 1, 0);
            check("Kích thước sai (b = 20 mm) → báo lỗi", SectionLibrary.AddRect(p, "X", 20, 500, "B25").Ok ? 0 : 1, 1, 0);
            var beams = ModelEdit.StoryBeams(p, "T2").Select(f => f.Id).ToList();
            BeamTools.AssignSection(p, beams, "D25x50");
            check("Gán tiết diện cho 4 dầm tầng T2", SectionLibrary.UsageCount(p, "D25x50"), 4, 0);
            var rn = SectionLibrary.Rename(p, "D25x50", "DCHINH");
            check("Đổi tên tiết diện: cập nhật 4 thanh", rn.Ok ? SectionLibrary.UsageCount(p, "DCHINH") : 0, 4, 0);
            check("Xoá tiết diện đang dùng (không gán lại) → chặn", SectionLibrary.Delete(p, "DCHINH").Ok ? 0 : 1, 1, 0);
            check("Xoá tiết diện đang dùng → tiết diện vẫn còn", p.FrameSections.Any(s => s.Name == "DCHINH") ? 1 : 0, 1, 0);
            var dl = SectionLibrary.Delete(p, "DCHINH", "D22x40");
            check("Xoá có gán lại sang D22x40: thành công", dl.Ok && p.FrameSections.All(s => s.Name != "DCHINH") ? 1 : 0, 1, 0);
            check("Xoá có gán lại: 4 thanh dùng D22x40", SectionLibrary.UsageCount(p, "D22x40"), 4, 0);
            var cp = SectionLibrary.Copy(p, "D22x40");
            check("Sao chép tiết diện → D22x40_2", cp.Ok && p.FrameSections.Any(s => s.Name == "D22x40_2") ? 1 : 0, 1, 0);
            SectionLibrary.AddRect(p, "", 220, 350, "B25");
            var md = SectionLibrary.Modify(p, "D22x35", 220, 450, "B30", 0.35, 1, 0.1, 1, "#FF8800");
            var s45 = p.FrameSections.FirstOrDefault(s => s.Name == "D22x45");
            check("Sửa kích thước tên tự động: D22x35 → D22x45", md.Ok && s45 != null ? 1 : 0, 1, 0);
            check("Sửa: hệ số I33 = 0,35", s45?.ModI33 ?? 0, 0.35, 1e-12);
            check("Sửa: màu hiển thị", s45 != null && s45.DisplayColor == "#FF8800" ? 1 : 0, 1, 0);
            var other = ModelBuilder.NewProject("Nguồn");
            other.FrameSections.Add(FrameSection.Rect("DP20x30", 200, 300, "B20"));
            var im = SectionLibrary.Import(p, other);
            check("Nhập tiết diện từ dự án khác: thêm DP20x30", im.Ok && p.FrameSections.Any(s => s.Name == "DP20x30") ? 1 : 0, 1, 0);
            check("Nhập: không ghi đè tiết diện trùng tên", p.FrameSections.Count(s => s.Name == "D22x30"), 1, 0);
        }

        // ---------------- 9. điểm chèn, lưu/mở, tương thích file cũ ----------------
        {
            var p = House(new[] { 5.0 }, new[] { 4.5 }, new[] { 3.6, 3.3 });
            string json0 = System.Text.Json.JsonSerializer.Serialize(p, Project.JsonOptions);
            check("File mặc định không ghi Insertion/PlanOffset/DisplayColor", json0.Contains("\"Insertion\"") || json0.Contains("\"PlanOffset\"") || json0.Contains("\"DisplayColor\"") ? 0 : 1, 1, 0);
            var b = ModelEdit.StoryBeams(p, "T2").First(f => Math.Abs(p.Node(f.NodeI).Y) < 1e-6 && Math.Abs(p.Node(f.NodeJ).Y) < 1e-6);
            var si = BeamTools.SetInsertion(p, new[] { b.Id }, BeamInsertion.MiddleLeft, 0.05);
            check("Gán điểm chèn mép trái + dời 50 mm", si.Ok ? 1 : 0, 1, 0);
            check("Độ lệch tim = 0,05 − b/2 = −0,06 m", BeamTools.LateralOffset(p, b), 0.05 - 0.11, 1e-9);
            check("Điểm chèn mép trái = cardinal 4 (ETABS)", BeamTools.EtabsCardinal(b.Insertion), 4, 0);
            check("Điểm chèn mặt trên: trọng tâm thấp hơn h/2", BeamTools.VerticalOffset(p, new FrameElement { Section = "D22x30", Insertion = BeamInsertion.TopCenter }), -0.15, 1e-9);
            p.FrameSections.First(s => s.Name == "D22x30").DisplayColor = "#3366CC";
            string path = Path.Combine(outDir, "BeamTools_LuuMo.kcvxt");
            p.Save(path);
            var q = Project.Load(path);
            var qb = q.Frames.First(f => f.Id == b.Id);
            check("Lưu/mở: giữ điểm chèn", qb.Insertion == BeamInsertion.MiddleLeft ? 1 : 0, 1, 0);
            check("Lưu/mở: giữ dời ngang 0,05", qb.PlanOffset, 0.05, 1e-12);
            check("Lưu/mở: giữ màu tiết diện", q.FrameSections.First(s => s.Name == "D22x30").DisplayColor == "#3366CC" ? 1 : 0, 1, 0);
            // chia dầm giữ điểm chèn
            var r = BeamTools.SecondaryBeamsAt(q, "T2", 2.5, 2.2, new SecondaryBeamOptions { AlongX = false, Count = 1, Section = "D22x30" });
            var pieces = ModelEdit.StoryBeams(q, "T2").Where(f => Math.Abs(q.Node(f.NodeI).Y) < 1e-6 && Math.Abs(q.Node(f.NodeJ).Y) < 1e-6).ToList();
            check("Dầm bị chia giữ điểm chèn ở mọi đoạn", r.Ok && pieces.Count == 2 && pieces.All(f => f.Insertion == BeamInsertion.MiddleLeft && Math.Abs(f.PlanOffset - 0.05) < 1e-12) ? 1 : 0, 1, 0);
            string e2k = Path.Combine(outDir, "BeamTools_DiemChen.e2k");
            E2kExporter.Export(q, e2k);
            check("Xuất e2k: dầm mép trái ghi CARDINALPT 4", File.ReadAllText(e2k).Contains("CARDINALPT 4") ? 1 : 0, 1, 0);
            var old = System.Text.Json.JsonSerializer.Deserialize<Project>(json0, Project.JsonOptions);
            check("File cũ: điểm chèn mặc định trọng tâm, dời 0", old.Frames.All(f => f.Insertion == BeamInsertion.Centroid && f.PlanOffset == 0) ? 1 : 0, 1, 0);
            var dx = BeamTools.DeleteFrames(q, r.NewFrames);
            check("Xoá nhiều dầm đã chọn", dx.Ok && q.Frames.All(f => !r.NewFrames.Contains(f.Id)) ? 1 : 0, 1, 0);
        }
    }
}
