using KetCauVXT.Analysis;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng quy trình mô hình kiểu ETABS: kiểm tra mô hình (Model/ModelCheck.cs) + sửa tự động, giải phóng đầu thanh
/// so với lời giải giải tích, gán tải hình thang / tập trung / tại nút (Model/ModelAssign.cs), hệ số độ cứng thanh, sàn cứng theo tầng,
/// khai báo tổ hợp (Loads/ComboEditor.cs) lưu/mở JSON, bảng kết quả (Analysis/ResultTables.cs).
/// check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class ModelWorkflowTests
{
    private static Project House(double[] spansX, double[] spansY, double[] heights)
    {
        var p = ModelBuilder.NewProject("Kiểm tra quy trình mô hình");
        ModelBuilder.SetGrid(p, spansX, spansY);
        ModelBuilder.SetStories(p, heights, 0);
        ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x30" });
        p.Wind.Enabled = false;
        return p;
    }

    /// <summary>Dầm một nhịp L (m) theo X, hai nút ngàm (gối cứng), tiết diện D22x40.</summary>
    private static Project Beam(double L)
    {
        var p = new Project();
        p.Materials.Add(Material.Concrete("B25"));
        p.FrameSections.Add(FrameSection.Rect("D22x40", 220, 400));
        p.FrameSections.Add(FrameSection.Rect("C30x30", 300, 300));
        p.Patterns.Add(new LoadPattern { Name = "A", Type = LoadType.Dead });
        p.Patterns.Add(new LoadPattern { Name = "B", Type = LoadType.Live });
        p.Nodes.Add(new Node { Id = 1, X = 0, Y = 0, Z = 3, Restraint = Node.Fixed });
        p.Nodes.Add(new Node { Id = 2, X = L, Y = 0, Z = 3, Restraint = Node.Fixed });
        p.Frames.Add(new FrameElement { Id = 1, NodeI = 1, NodeJ = 2, Section = "D22x40", Kind = FrameKind.Beam, Label = "D1" });
        return p;
    }

    private static Node NodeAt(Project p, double x, double y, double z) =>
        p.Nodes.FirstOrDefault(n => Math.Abs(n.X - x) < 1e-3 && Math.Abs(n.Y - y) < 1e-3 && Math.Abs(n.Z - z) < 1e-3);

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Directory.CreateDirectory(outDir);

        // ---------------- 1. kiểm tra mô hình: mô hình sạch ----------------
        {
            var p = House(new[] { 5.0 }, new[] { 4.5 }, new[] { 3.6 });
            var rep = ModelCheck.Run(p);
            check("Mô hình sạch: không có lỗi", rep.Errors, 0, 0);
            check("Mô hình sạch: không có nút trùng", rep.Count(ModelCheck.DuplicateNodes), 0, 0);
            check("Mô hình sạch: không có cạnh sàn thiếu dầm", rep.Count(ModelCheck.SlabEdgeNoBeam), 0, 0);
            check("Mô hình sạch: không có dầm cắt nhau không nút", rep.Count(ModelCheck.CrossNoNode), 0, 0);
        }

        // ---------------- 2. kiểm tra mô hình: gieo lỗi + sửa tự động ----------------
        {
            var p = House(new[] { 5.0 }, new[] { 4.5 }, new[] { 3.6 });
            double z = p.Stories[0].Elevation;
            string st = p.Stories[0].Name;
            int nFrames0 = p.Frames.Count;
            var corner = NodeAt(p, 5, 4.5, z);
            // (a) nút trùng: nút cách nút góc 2 mm + dầm công xôn từ nút đó
            var dup = new Node { Id = p.NextNodeId(), X = corner.X + 0.002, Y = corner.Y, Z = z, Story = st };
            p.Nodes.Add(dup);
            var tip = new Node { Id = p.NextNodeId(), X = 6.5, Y = 4.5, Z = z, Story = st };
            p.Nodes.Add(tip);
            p.Frames.Add(new FrameElement { Id = p.NextFrameId(), NodeI = dup.Id, NodeJ = tip.Id, Section = "D22x30", Kind = FrameKind.Beam, Story = st, Label = "CX" });
            // (b) thanh dài 0
            p.Frames.Add(new FrameElement { Id = p.NextFrameId(), NodeI = corner.Id, NodeJ = corner.Id, Section = "D22x30", Kind = FrameKind.Beam, Story = st, Label = "Z0" });
            // (c) thanh trùng (cột cùng hai nút, ngược chiều) có tải
            var b0 = p.Frames.First(f => f.IsVerticalKind && f.Story == st);
            var bd = new FrameElement { Id = p.NextFrameId(), NodeI = b0.NodeJ, NodeJ = b0.NodeI, Section = b0.Section, Kind = FrameKind.Column, Story = st, Label = "TRUNG" };
            p.Frames.Add(bd);
            p.FrameLoads.Add(FrameLoad.Uniform(bd.Id, "HTHT", 3.0));
            // (d) hai dầm cắt nhau giữa ô, đầu dầm nằm giữa dầm biên (chưa chia)
            var m1 = p.GetOrAddNode(2.5, 0, z, st); var m2 = p.GetOrAddNode(2.5, 4.5, z, st);
            var m3 = p.GetOrAddNode(0, 2.25, z, st); var m4 = p.GetOrAddNode(5, 2.25, z, st);
            p.Frames.Add(new FrameElement { Id = p.NextFrameId(), NodeI = m1.Id, NodeJ = m2.Id, Section = "D22x30", Kind = FrameKind.Beam, Story = st, Label = "DP1" });
            p.Frames.Add(new FrameElement { Id = p.NextFrameId(), NodeI = m3.Id, NodeJ = m4.Id, Section = "D22x30", Kind = FrameKind.Beam, Story = st, Label = "DP2" });
            // (e) nút tự do
            p.Nodes.Add(new Node { Id = p.NextNodeId(), X = 20, Y = 20, Z = 20 });
            // (f) trường hợp tải không có tải
            p.Patterns.Add(new LoadPattern { Name = "RONG", Type = LoadType.Other });
            // (g) tổ hợp tham chiếu trường hợp không có
            p.Combinations.Add(new LoadCombination { Name = "THX", Items = { new ComboItem("KHONG_CO", 1.0), new ComboItem("TT", 1.1) } });
            // (h) tải gán cho thanh không tồn tại
            p.FrameLoads.Add(FrameLoad.Uniform(9999, "TT", 1));

            var rep = ModelCheck.Run(p);
            check("Phát hiện nút trùng (2 mm)", rep.Count(ModelCheck.DuplicateNodes), 1, 0);
            check("Phát hiện thanh dài 0", rep.Count(ModelCheck.ZeroLength), 1, 0);
            check("Phát hiện thanh trùng", rep.Count(ModelCheck.DuplicateFrames), 1, 0);
            check("Phát hiện dầm cắt nhau không có nút", rep.Count(ModelCheck.CrossNoNode), 1, 0);
            check("Phát hiện 4 nút nằm giữa dầm biên chưa chia", rep.Count(ModelCheck.NodeOnFrame), 4, 0);
            check("Phát hiện nút tự do", rep.Has(ModelCheck.FreeNodes) ? 1 : 0, 1, 0);
            check("Phát hiện nhóm thanh không nối gối (công xôn từ nút trùng)", rep.Has(ModelCheck.Unsupported) ? 1 : 0, 1, 0);
            check("Phát hiện trường hợp tải rỗng RONG", rep.Issues.Count(i => i.Code == ModelCheck.EmptyPattern && i.Message.Contains("RONG")), 1, 0);
            check("Trường hợp tải TT (có TLBT) không bị báo rỗng", rep.Issues.Count(i => i.Code == ModelCheck.EmptyPattern && i.Message.Contains(" TT ")), 0, 0);
            check("Trường hợp HT (tải sàn dồn tự sinh) không bị báo rỗng", rep.Issues.Count(i => i.Code == ModelCheck.EmptyPattern && i.Message.Contains(" HT ")), 0, 0);
            check("Phát hiện tổ hợp tham chiếu thiếu", rep.Count(ModelCheck.ComboMissingRef), 1, 0);
            check("Phát hiện tải mồ côi", rep.Count(ModelCheck.OrphanLoads), 1, 0);
            check("Số mục sửa tự động được ≥ 10", rep.Fixable >= 10 ? 1 : 0, 1, 0);

            var fix = ModelCheck.Fix(p);
            var rep2 = ModelCheck.Run(p);
            check("Sau sửa: hết nút trùng", rep2.Count(ModelCheck.DuplicateNodes), 0, 0);
            check("Sau sửa: hết thanh dài 0", rep2.Count(ModelCheck.ZeroLength), 0, 0);
            check("Sau sửa: hết thanh trùng", rep2.Count(ModelCheck.DuplicateFrames), 0, 0);
            check("Sau sửa: hết dầm cắt nhau không nút", rep2.Count(ModelCheck.CrossNoNode), 0, 0);
            check("Sau sửa: hết nút giữa thanh chưa chia", rep2.Count(ModelCheck.NodeOnFrame), 0, 0);
            check("Sau sửa: hết nút tự do", rep2.Count(ModelCheck.FreeNodes), 0, 0);
            check("Sau sửa: hết nhóm không nối gối (công xôn đã nối vào nút góc)", rep2.Count(ModelCheck.Unsupported), 0, 0);
            check("Sau sửa: hết tổ hợp thiếu", rep2.Count(ModelCheck.ComboMissingRef), 0, 0);
            check("Sau sửa: hết tải mồ côi", rep2.Count(ModelCheck.OrphanLoads), 0, 0);
            check("Sau sửa: không còn lỗi", rep2.Errors, 0, 0);
            check("Gộp 1 nút trùng", fix.MergedNodes, 1, 0);
            check("Có nút tại giao điểm (2,5; 2,25)", NodeAt(p, 2.5, 2.25, z) != null ? 1 : 0, 1, 0);
            var cx = p.Frames.First(f => f.Label == "CX");
            check("Dầm công xôn nối vào nút góc giữ lại", cx.NodeI, corner.Id, 0);
            check("Tải của thanh trùng chuyển sang thanh giữ lại", p.FrameLoads.Count(l => l.Pattern == "HTHT" && l.Source == "" && Math.Abs(l.Points[0][1] - 3.0) < 1e-9), 1, 0);
            // 4 dầm biên mỗi dầm chia đôi tại m1…m4 → 8 đoạn, 2 dầm phụ chia đôi tại giao điểm → 4 đoạn, công xôn 1, cột 4 (cột trùng đã xoá)
            check("Số thanh sau sửa = 4 cột + 8 đoạn dầm biên + 4 đoạn dầm phụ + 1 công xôn", p.Frames.Count, 4 + 8 + 4 + 1, 0);
            check("Ban đầu có 8 thanh (4 cột + 4 dầm)", nFrames0, 8, 0);
            // phân tích được sau khi sửa (không còn lỗi Validate)
            bool ok = true;
            try { FrameAnalysis.Run(p); } catch { ok = false; }
            check("Phân tích chạy được sau khi sửa tự động", ok ? 1 : 0, 1, 0);
        }

        // ---------------- 3. các lỗi khác: cột không gối, cạnh sàn thiếu dầm, thiếu tiết diện ----------------
        {
            var p = House(new[] { 5.0 }, new[] { 4.5 }, new[] { 3.6 });
            double z = p.Stories[0].Elevation;
            // bỏ một dầm biên → cạnh sàn không có dầm đỡ
            var edge = p.Frames.First(f => !f.IsVerticalKind);
            p.Frames.Remove(edge);
            // cột treo không gối
            var top = NodeAt(p, 0, 0, z);
            var nb = new Node { Id = p.NextNodeId(), X = 0, Y = 0, Z = z + 3 };
            var nt = new Node { Id = p.NextNodeId(), X = 0, Y = 0, Z = z + 6 };
            p.Nodes.Add(nb); p.Nodes.Add(nt);
            p.Frames.Add(new FrameElement { Id = p.NextFrameId(), NodeI = nb.Id, NodeJ = nt.Id, Section = "XYZ", Kind = FrameKind.Column, Label = "CT" });
            var rep = ModelCheck.Run(p);
            check("Phát hiện cạnh sàn không có dầm đỡ", rep.Count(ModelCheck.SlabEdgeNoBeam), 1, 0);
            check("Phát hiện chân cột không có gối", rep.Count(ModelCheck.ColumnNoSupport), 1, 0);
            check("Phát hiện tiết diện chưa khai báo", rep.Count(ModelCheck.MissingSection), 1, 0);
            check("Phát hiện tiết diện không dùng (Info)", rep.Has(ModelCheck.UnusedSection) ? 1 : 0, 1, 0);
            check("Cột treo: nhóm không nối gối", rep.Has(ModelCheck.Unsupported) ? 1 : 0, 1, 0);
            check("Nút top còn được dùng", top != null ? 1 : 0, 1, 0);
        }

        // ---------------- 4. giải phóng đầu thanh: dầm ngàm – khớp (propped cantilever) ----------------
        {
            var p = Beam(6);
            var rr = ModelAssign.Releases(p, new[] { 1 }, new bool[6], new[] { false, false, false, false, false, true });
            check("Gán giải phóng M3 đầu J thành công", rr.Ok ? 1 : 0, 1, 0);
            ModelAssign.FrameUniform(p, new[] { 1 }, "A", 10);
            var res = FrameAnalysis.Run(p);
            var ff = res.Get("A").Frames[1];
            check("Ngàm–khớp: M3 tại đầu J (giải phóng) = 0", ff.At(6)[5], 0, 1e-6);
            check("Ngàm–khớp: M3 tại ngàm = −qL²/8 = −45", ff.At(0)[5], -45, 1e-3);
            check("Ngàm–khớp: M3 tại x = 3,5 m = −45 + 37,5x − 5x² = 25", ff.At(3.5)[5], 25, 1e-3);
            check("Ngàm–khớp: phản lực ngàm = 5qL/8 = 37,5", res.Get("A").Reactions[1][2], 37.5, 1e-3);
            check("Ngàm–khớp: phản lực khớp = 3qL/8 = 22,5", res.Get("A").Reactions[2][2], 22.5, 1e-3);
            check("Ngàm–khớp: mô men phản lực tại khớp ≈ 0", Math.Abs(res.Get("A").Reactions[2][4]) + Math.Abs(res.Get("A").Reactions[2][5]), 0, 1e-6);
            var bad = ModelAssign.Releases(p, new[] { 1 }, new[] { true, false, false, false, false, false }, new[] { true, false, false, false, false, false });
            check("Giải phóng P hai đầu bị từ chối (biến hình)", bad.Ok ? 0 : 1, 1, 0);
            check("ReleaseName mô tả", ModelAssign.ReleaseName(p.Frames[0]) == "I: – / J: M3" ? 1 : 0, 1, 0);
        }

        // ---------------- 5. gán tải hình thang / tập trung → phản lực ----------------
        {
            var p = Beam(6);
            p.Frames[0].PinBothEnds();
            int n1 = ModelAssign.FrameTrapezoid(p, new[] { 1 }, "A", 1.0, 4.0, 4.0, 10.0, LoadDir.Gravity, relative: false);
            int n2 = ModelAssign.FramePoint(p, new[] { 1 }, "B", 2.0, 12.0, LoadDir.Gravity, relative: false);
            check("Gán tải hình thang cho 1 thanh", n1, 1, 0);
            check("Gán lực tập trung cho 1 thanh", n2, 1, 0);
            var res = FrameAnalysis.Run(p);
            // W = (4+10)/2·3 = 21 kN, trọng tâm cách đầu I: 1 + 3/3·(4+2·10)/(4+10) = 2,7142857 m
            check("Hình thang: phản lực J = W·x̄/L = 9,5", res.Get("A").Reactions[2][2], 9.5, 1e-3);
            check("Hình thang: phản lực I = 11,5", res.Get("A").Reactions[1][2], 11.5, 1e-3);
            check("Tập trung P = 12 tại 2 m: phản lực J = 4", res.Get("B").Reactions[2][2], 4, 1e-3);
            check("Tập trung: phản lực I = 8", res.Get("B").Reactions[1][2], 8, 1e-3);
            check("Tập trung: M3 dưới lực = Pab/L = 16", res.Get("B").Frames[1].At(2)[5], 16, 1e-3);
            // tỷ lệ tương đối + thay thế
            ModelAssign.FramePoint(p, new[] { 1 }, "B", 0.5, 6.0, LoadDir.Gravity, relative: true, mode: LoadAssignMode.Replace);
            check("Thay thế: chỉ còn 1 tải B trên thanh", p.FrameLoads.Count(l => l.Pattern == "B"), 1, 0);
            var res2 = FrameAnalysis.Run(p);
            check("Thay thế: P = 6 giữa nhịp → phản lực J = 3", res2.Get("B").Reactions[2][2], 3, 1e-3);
            ModelAssign.FrameUniform(p, new[] { 1 }, "B", 0, LoadDir.Gravity, LoadAssignMode.Delete);
            check("Xoá tải B nhập tay", p.FrameLoads.Count(l => l.Pattern == "B"), 0, 0);
            bool threw = false;
            try { ModelAssign.FrameUniform(p, new[] { 1 }, "KHONG_CO", 1); } catch (ArgumentException) { threw = true; }
            check("Gán tải cho trường hợp chưa khai báo → báo lỗi", threw ? 1 : 0, 1, 0);
        }

        // ---------------- 6. tải nút, liên kết nút, hệ số độ cứng ----------------
        {
            var p = new Project();
            p.Materials.Add(Material.Concrete("B25"));
            p.FrameSections.Add(FrameSection.Rect("C30x30", 300, 300));
            p.Patterns.Add(new LoadPattern { Name = "P", Type = LoadType.Wind });
            p.Nodes.Add(new Node { Id = 1, Z = 0 });
            p.Nodes.Add(new Node { Id = 2, Z = 4 });
            p.Frames.Add(new FrameElement { Id = 1, NodeI = 1, NodeJ = 2, Section = "C30x30", Kind = FrameKind.Column, Label = "C1" });
            ModelAssign.Restraint(p, new[] { 1 }, Node.Fixed);
            check("Gán ngàm: tên liên kết", ModelAssign.RestraintName(p.Nodes[0].Restraint) == "Ngàm" ? 1 : 0, 1, 0);
            check("Khớp: tên liên kết", ModelAssign.RestraintName(Node.Pinned) == "Khớp" ? 1 : 0, 1, 0);
            ModelAssign.JointLoad(p, new[] { 2 }, "P", new double[] { 20, 0, 0, 0, 0, 0 });
            var r1 = FrameAnalysis.Run(p).Get("P");
            check("Tải nút Fx = 20 → phản lực FX = −20", r1.Reactions[1][0], -20, 1e-6);
            double E = 30000e3, I = 0.3 * 0.3 * 0.3 * 0.3 / 12, G = E / 2.4, As = 5.0 / 6 * 0.09;
            double bend = 20 * 64 / (3 * E * I), shear = 20 * 4 / (G * As);
            check("Chuyển vị đỉnh = PL³/3EI + PL/GAs", r1.Disp[2][0], bend + shear, 1e-3);
            ModelAssign.Modifiers(p, new[] { 1 }, new[] { 1, 1, 1, 1, 0.5, 0.5 });
            var r2 = FrameAnalysis.Run(p).Get("P");
            check("Hệ số độ cứng I × 0,5 → phần uốn tăng gấp đôi", r2.Disp[2][0], 2 * bend + shear, 1e-3);
            ModelAssign.Modifiers(p, new[] { 1 }, new double[] { 1, 1, 1, 1, 1, 1 });
            check("Hệ số toàn 1 → bỏ gán (null)", p.Frames[0].StiffnessModifiers == null ? 1 : 0, 1, 0);
            check("NodesOf cột (chân) = nút 1", ModelAssign.NodesOf(p, new[] { 1 }, true).Single(), 1, 0);
        }

        // ---------------- 7. sàn cứng theo tầng, tải sàn ----------------
        {
            var p = House(new[] { 4.0 }, new[] { 4.0 }, new[] { 3.3, 3.3 });
            var s1 = p.Stories[0];
            ModelAssign.Diaphragm(p, s1.Name, false);
            check("Tắt sàn cứng tầng 1", p.IsDiaphragmOff(s1.Elevation) ? 1 : 0, 1, 0);
            check("Tầng mái vẫn sàn cứng", p.IsDiaphragmOff(p.Stories[1].Elevation) ? 0 : 1, 1, 0);
            var res = FrameAnalysis.Run(p);
            check("Phân tích: sàn cứng chỉ gán cho 1 cao trình", res.Warnings.Any(w => w.Contains("diaphragm") && w.Contains("cho 1 cao trình")) ? 1 : 0, 1, 0);
            string path = Path.Combine(outDir, "QuyTrinhMoHinh_SanCung.kcvxt");
            p.Save(path);
            var q = Project.Load(path);
            check("Lưu/mở JSON giữ danh sách tầng tắt sàn cứng", q.IsDiaphragmOff(s1.Elevation) ? 1 : 0, 1, 0);
            ModelAssign.Diaphragm(q, s1.Name, true);
            check("Bật lại sàn cứng → danh sách rỗng (null)", q.DiaphragmOff == null ? 1 : 0, 1, 0);
            int na = ModelAssign.AreaUniform(p, p.Slabs.Select(s => s.Id), "HT", 2.0, LoadAssignMode.Replace);
            check("Gán tải sàn thay thế cho 2 ô sàn", na, 2, 0);
            check("Tải HT nhập tay sau thay thế = 2 dòng", p.AreaLoads.Count(l => l.Pattern == "HT" && l.Source == ""), 2, 0);
        }

        // ---------------- 8. khai báo tổ hợp (Core API) + lưu/mở JSON ----------------
        {
            var p = Beam(5);
            var a = ComboEditor.Add(p, "C1", ComboType.ULS, new[] { new ComboItem("A", 1.1), new ComboItem("B", 1.3) });
            check("Thêm tổ hợp C1", a.Ok ? 1 : 0, 1, 0);
            check("Trùng tên → từ chối", ComboEditor.Add(p, "C1", ComboType.ULS, new[] { new ComboItem("A", 1) }).Ok ? 0 : 1, 1, 0);
            check("Tham chiếu thiếu → từ chối", ComboEditor.Add(p, "C2", ComboType.ULS, new[] { new ComboItem("KHONG", 1) }).Ok ? 0 : 1, 1, 0);
            check("Tổ hợp rỗng → từ chối", ComboEditor.Add(p, "C3", ComboType.ULS, Array.Empty<ComboItem>()).Ok ? 0 : 1, 1, 0);
            ComboEditor.Add(p, "C2", ComboType.SLS, new[] { new ComboItem("A", 1), new ComboItem("B", 1) });
            var env = ComboEditor.Add(p, "BAO1", ComboType.Envelope, new[] { new ComboItem("C1", 1), new ComboItem("C2", 1) });
            check("Thêm tổ hợp bao BAO1", env.Ok ? 1 : 0, 1, 0);
            check("Tổ hợp tuyến tính chứa tổ hợp bao → từ chối", ComboEditor.Add(p, "C4", ComboType.ULS, new[] { new ComboItem("BAO1", 1) }).Ok ? 0 : 1, 1, 0);
            var u = ComboEditor.Update(p, "C1", "TTGH1", ComboType.ULS, new[] { new ComboItem("A", 1.15), new ComboItem("B", 1.2) });
            check("Sửa + đổi tên C1 → TTGH1", u.Ok ? 1 : 0, 1, 0);
            check("Đổi tên cập nhật tham chiếu trong tổ hợp bao", p.Combinations.First(c => c.Name == "BAO1").Items.Any(i => i.Case == "TTGH1") ? 1 : 0, 1, 0);
            check("Vòng tham chiếu → từ chối", ComboEditor.Update(p, "TTGH1", "TTGH1", ComboType.Envelope, new[] { new ComboItem("BAO1", 1) }).Ok ? 0 : 1, 1, 0);
            var dpl = ComboEditor.Duplicate(p, "C2");
            check("Sao chép tổ hợp C2", dpl.Ok ? 1 : 0, 1, 0);
            var del = ComboEditor.Delete(p, "C2");
            check("Xoá C2", del.Ok ? 1 : 0, 1, 0);
            check("Xoá C2 bỏ tham chiếu trong BAO1", p.Combinations.First(c => c.Name == "BAO1").Items.Count, 1, 0);
            check("Không còn vấn đề tổ hợp", ComboEditor.Problems(p).Count, 0, 0);
            string path = Path.Combine(outDir, "QuyTrinhMoHinh_ToHop.kcvxt");
            p.Save(path);
            var q = Project.Load(path);
            var t1 = q.Combinations.FirstOrDefault(c => c.Name == "TTGH1");
            check("Lưu/mở: số tổ hợp = 3 (TTGH1, BAO1, bản sao)", q.Combinations.Count, 3, 0);
            check("Lưu/mở: hệ số A trong TTGH1 = 1,15", t1?.Items.First(i => i.Case == "A").Factor ?? 0, 1.15, 1e-12);
            check("Lưu/mở: loại BAO1 = Envelope", q.Combinations.First(c => c.Name == "BAO1").Type == ComboType.Envelope ? 1 : 0, 1, 0);
            // phân tích + tổ hợp tự đặt
            p.FrameLoads.Add(FrameLoad.Uniform(1, "A", 10)); p.FrameLoads.Add(FrameLoad.Uniform(1, "B", 4));
            var res = FrameAnalysis.Run(p);
            check("Tổ hợp TTGH1: M3 ngàm = −(1,15·10 + 1,2·4)·25/12", res.Get("TTGH1").Frames[1].At(0)[5], -(1.15 * 10 + 1.2 * 4) * 25 / 12, 1e-6);
            // tự sinh TCVN có giữ tổ hợp người dùng
            ComboEditor.Generate(p, keepUser: true);
            check("Tự sinh TCVN giữ tổ hợp người dùng TTGH1", p.Combinations.Any(c => c.Name == "TTGH1") ? 1 : 0, 1, 0);
            check("Tự sinh TCVN có tổ hợp BAO", p.Combinations.Any(c => c.Name == "BAO") ? 1 : 0, 1, 0);
            ComboEditor.Generate(p, keepUser: false);
            check("Tự sinh không giữ → bỏ TTGH1", p.Combinations.Any(c => c.Name == "TTGH1") ? 0 : 1, 1, 0);
            // trường hợp tải: đổi tên cập nhật tổ hợp
            var ren = LoadPatternEditor.Rename(p, "A", "TT2");
            check("Đổi tên trường hợp tải A → TT2", ren.Ok ? 1 : 0, 1, 0);
            check("Đổi tên cập nhật tải trên thanh", p.FrameLoads.Count(l => l.Pattern == "TT2"), 1, 0);
            check("Đổi tên cập nhật tổ hợp", p.Combinations.Any(c => c.Items.Any(i => i.Case == "A")) ? 0 : 1, 1, 0);
            var dp = LoadPatternEditor.Delete(p, "B");
            check("Xoá trường hợp tải B", dp.Ok ? 1 : 0, 1, 0);
            check("Xoá B: không còn tổ hợp tham chiếu B", p.Combinations.Any(c => c.Items.Any(i => i.Case == "B")) ? 0 : 1, 1, 0);
        }

        // ---------------- 9. bảng kết quả ----------------
        {
            var p = House(new[] { 4.0, 3.0 }, new[] { 4.0 }, new[] { 3.3, 3.0 });
            p.Modal ??= new ModalSettings();
            p.Modal.Enabled = true;
            ComboGenerator.Generate(p);
            var res = FrameAnalysis.Run(p);
            var fTT = new ResultTableFilter { Cases = new List<string> { "TT" } };
            var jd = ResultTables.Build(res, ResultTableKind.JointDisplacements, fTT);
            check("Bảng chuyển vị nút: số dòng = số nút có chuyển vị (TT)", jd.Rows.Count, res.Get("TT").Disp.Count, 0);
            check("Bảng chuyển vị nút: 10 cột", jd.Columns.Count, 10, 0);
            var jr = ResultTables.Build(res, ResultTableKind.JointReactions, fTT);
            check("Bảng phản lực: số dòng = số gối (6)", jr.Rows.Count, 6, 0);
            var bf = ResultTables.Build(res, ResultTableKind.BeamForces, fTT);
            int expBeam = p.Frames.Where(f => !f.IsVerticalKind).Sum(f => res.Get("TT").Frames[f.Id].Count);
            check("Bảng nội lực dầm: số dòng = Σ số mặt cắt dầm", bf.Rows.Count, expBeam, 0);
            var cf = ResultTables.Build(res, ResultTableKind.ColumnForces, new ResultTableFilter { Cases = new List<string> { "TT" }, Story = p.Stories[0].Name });
            int expCol = p.Frames.Where(f => f.IsVerticalKind && f.Story == p.Stories[0].Name).Sum(f => res.Get("TT").Frames[f.Id].Count);
            check("Bảng nội lực cột lọc tầng 1: số dòng", cf.Rows.Count, expCol, 0);
            var fGX = new ResultTableFilter { Cases = new List<string> { "TT", "HT" } };
            var sd = ResultTables.Build(res, ResultTableKind.StoryDrifts, fGX);
            check("Bảng lệch tầng: 2 tầng × 2 trường hợp × 2 phương = 8 dòng", sd.Rows.Count, 8, 0);
            var md = ResultTables.Build(res, ResultTableKind.ModalPeriods);
            check("Bảng dao động: số dòng = số dạng", md.Rows.Count, res.Modal?.Modes.Count ?? -1, 0);
            var br = ResultTables.Build(res, ResultTableKind.BaseReactions, fTT);
            check("Bảng phản lực đáy (TT): 1 dòng", br.Rows.Count, 1, 0);
            double fz = res.Get("TT").Reactions.Values.Sum(v => v[2]);
            check("Phản lực đáy FZ = Σ FZ gối", (double)br.Rows[0][4], fz, 1e-9);
            var env = ResultTables.Build(res, ResultTableKind.BaseReactions, new ResultTableFilter { Cases = new List<string> { "BAO" } });
            check("Phản lực đáy theo tổ hợp bao: Max + Min = 2 dòng", env.Rows.Count, 2, 0);
            var all = ResultTables.Build(res, ResultTableKind.JointReactions);
            check("Bảng phản lực mọi trường hợp: 6 gối × (số trường hợp + tổ hợp tuyến tính)", all.Rows.Count, 6 * ResultTables.DefaultCases(res).Count, 0);
            string csv = jr.ToCsv();
            check("CSV phản lực: số dòng = dòng bảng + tiêu đề", csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length, jr.Rows.Count + 1, 0);
            File.WriteAllText(Path.Combine(outDir, "QuyTrinhMoHinh_PhanLuc.csv"), csv);
        }
    }
}
