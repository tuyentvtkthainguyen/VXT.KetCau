using System.IO.Compression;
using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Export;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng dầm cong trên mặt bằng (Model/CurvedBeam.cs, FrameAnalysis, BeamDesigner + RcTorsion, xuất e2k/s2k, DXF, báo cáo).
/// check(tên, giá trị, kỳ vọng, sai số tương đối).
/// Lời giải giải tích: dầm công xôn cung tròn bán kính R chịu tải đều q (theo chiều dài cung), θ đo từ đầu tự do:
/// M(θ) = q·R²·(1 − cos θ), T(θ) = q·R²·(θ − sin θ), V(θ) = q·R·θ.</summary>
public static class CurvedBeamTests
{
    private const double R = 4.0;

    /// <summary>Công xôn 1/4 đường tròn bán kính R: ngàm tại I = (R, 0), tự do tại J = (0, R), tâm tại gốc toạ độ, cao độ +3,0.</summary>
    private static Project QuarterCantilever(int segs, double q, bool selfWeight)
    {
        var p = new Project();
        p.Materials.Add(Material.Concrete("B25"));
        p.FrameSections.Add(FrameSection.Rect("D30x50", 300, 500));
        p.Stories.Add(new Story { Name = "T1", Elevation = 3, Height = 3 });
        p.Patterns.Add(new LoadPattern { Name = "P", Type = LoadType.Dead });
        if (selfWeight) p.Patterns.Add(new LoadPattern { Name = "G", Type = LoadType.Dead, SelfWeight = 1 });
        p.Nodes.Add(new Node { Id = 1, X = R, Y = 0, Z = 3, Restraint = Node.Fixed, Story = "T1" });
        p.Nodes.Add(new Node { Id = 2, X = 0, Y = R, Z = 3, Story = "T1" });
        double s = CurvedBeam.SagittaThrough(R, 0, 0, R, R / Math.Sqrt(2), R / Math.Sqrt(2));
        p.Frames.Add(new FrameElement
        {
            Id = 1, NodeI = 1, NodeJ = 2, Section = "D30x50", Kind = FrameKind.Beam, Story = "T1", Label = "DC1", DesignGroup = "DC1",
            CurveMidOffset = s, CurveSegments = segs
        });
        if (q != 0) p.FrameLoads.Add(FrameLoad.Uniform(1, "P", q));
        return p;
    }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Directory.CreateDirectory(outDir);
        double half = Math.PI / 2;

        // ---------------- 1. hình học ----------------
        {
            var a = CurvedBeam.Arc(new Vec3(-1, 0, 3), new Vec3(1, 0, 3), 1.0);   // nửa đường tròn R = 1 vồng về +Y
            check("Dầm cong: nửa đường tròn – R từ độ vồng", a.Radius, 1.0, 1e-9);
            check("Dầm cong: nửa đường tròn – chiều dài cung π·R", a.Length, Math.PI, 1e-9);
            check("Dầm cong: nửa đường tròn – góc ở tâm 180°", a.AngleDeg, 180, 1e-9);
            check("Dầm cong: điểm giữa cung y = +1 (vồng trái hướng I→J)", a.Mid.Y, 1.0, 1e-9);
            check("Dầm cong: tiếp tuyến tại giữa cung theo +X", a.TangentAt(0.5).X, 1.0, 1e-9);
            check("Dầm cong: R = (c²/4 + f²)/(2f), c = 6, f = 1", CurvedBeam.RadiusFromSagitta(6, 1), 5.0, 1e-12);
            check("Dầm cong: độ vồng từ R (cung nhỏ) R = 5, c = 6", CurvedBeam.SagittaFromRadius(6, 5), 1.0, 1e-12);
            check("Dầm cong: L cung = R·4·atan(2f/c)", CurvedBeam.ArcLength(6, 1), 5 * 4 * Math.Atan(2.0 / 6), 1e-12);
            check("Dầm cong: độ vồng qua điểm thứ ba (0; 0,5) của dây cung (±1; 0)", CurvedBeam.SagittaThrough(-1, 0, 1, 0, 0, 0.5), 0.5, 1e-9);
            check("Dầm cong: điểm thứ ba phía dưới → độ vồng âm", CurvedBeam.SagittaThrough(-1, 0, 1, 0, 0.6, -0.8), -1.0, 1e-9);
            var p = QuarterCantilever(0, 10, false);
            var arc = CurvedBeam.Arc(p, p.Frames[0]);
            check("Dầm cong 1/4 đường tròn: R = 4 m", arc.Radius, R, 1e-9);
            check("Dầm cong 1/4 đường tròn: Project.Length = chiều dài cung π·R/2", p.Length(p.Frames[0]), R * half, 1e-9);
            check("Dầm cong: chia tự động ≤ 10°/đoạn → 9 đoạn cho 90°", CurvedBeam.SegmentCount(p.Frames[0], arc), 9, 0);
            var pts = CurvedBeam.Discretize(p, p.Frames[0]);
            check("Dầm cong: điểm chia nằm trên cung", pts.Max(q => Math.Abs(Math.Sqrt(q.X * q.X + q.Y * q.Y) - R)), 0, 1e-9);
            double tm = arc.ParamOf(R * Math.Cos(0.3), R * Math.Sin(0.3), out double dd);
            check("Dầm cong: tham số của điểm trên cung", tm, 0.3 / half, 1e-9);
            check("Dầm cong: Validate hợp lệ", p.Validate().Count, 0, 0);
            var bad = QuarterCantilever(0, 0, false);
            bad.Nodes[1].Z = 3.5;
            check("Dầm cong: hai đầu lệch cao độ → báo lỗi", bad.Validate().Any(e => e.Contains("nằm ngang")) ? 1 : 0, 1, 0);
        }

        // ---------------- 2. công xôn 1/4 đường tròn – so với lời giải giải tích ----------------
        {
            double q = 10;
            var p = QuarterCantilever(0, q, true);
            var res = FrameAnalysis.Run(p);
            var ff = res.Get("P").Frames[1];
            double L = p.Length(p.Frames[0]);
            check("Công xôn cong (9 đoạn): M3 tại ngàm = −q·R²", ff.At(0)[5], -q * R * R, 0.02);
            check("Công xôn cong (9 đoạn): |T| tại ngàm = q·R²·(π/2 − 1)", Math.Abs(ff.At(0)[3]), q * R * R * (half - 1), 0.02);
            check("Công xôn cong (9 đoạn): |V2| tại ngàm = q·R·π/2", Math.Abs(ff.At(0)[1]), q * R * half, 0.01);
            check("Công xôn cong: trạm cuối = chiều dài cung", ff.Stations[^1], L, 1e-6);
            check("Công xôn cong: M3 tại đầu tự do = 0", ff.At(L)[5], 0, 1e-6);
            check("Công xôn cong: T tại đầu tự do = 0", ff.At(L)[3], 0, 1e-6);
            var rc = res.Get("P").Reactions[1];
            check("Công xôn cong: phản lực FZ = q·L cung", rc[2], q * L, 1e-6);
            check("Công xôn cong: mô men phản lực |M| = q·R²·√(1² + (π/2 − 1)²)", Math.Sqrt(rc[3] * rc[3] + rc[4] * rc[4]), q * R * R * Math.Sqrt(1 + (half - 1) * (half - 1)), 0.01);
            var g = res.Get("G").Reactions[1];
            double w = p.Material("B25").UnitWeight * 0.3 * 0.5;
            check("Công xôn cong: trọng lượng bản thân theo cung ΣFZ = γ·A·L cung", g[2], w * L, 1e-6);

            var p36 = QuarterCantilever(36, q, false);
            var f36 = FrameAnalysis.Run(p36).Get("P").Frames[1];
            double th = Math.PI / 4;   // giữa cung: θ = π/4 tính từ đầu tự do
            check("Công xôn cong (36 đoạn): M3 giữa cung = −q·R²·(1 − cos π/4)", f36.At(L / 2)[5], -q * R * R * (1 - Math.Cos(th)), 0.02);
            check("Công xôn cong (36 đoạn): |T| giữa cung = q·R²·(π/4 − sin π/4)", Math.Abs(f36.At(L / 2)[3]), q * R * R * (th - Math.Sin(th)), 0.02);
            check("Công xôn cong (36 đoạn): M3 tại ngàm", f36.At(0)[5], -q * R * R, 0.005);
            check("Công xôn cong (36 đoạn): |T| tại ngàm", Math.Abs(f36.At(0)[3]), q * R * R * (half - 1), 0.005);
            // sai lệch lớn nhất dọc cung (mọi mặt cắt) so với lời giải giải tích – nội lực đã quy về hệ trục tiếp tuyến
            double devM = 0, devT = 0;
            for (int i = 0; i < ff.Count; i++)
            {
                double tt = (L - ff.Stations[i]) / R;
                devM = Math.Max(devM, Math.Abs(ff.M3(i) + q * R * R * (1 - Math.Cos(tt))));
                devT = Math.Max(devT, Math.Abs(Math.Abs(ff.T(i)) - q * R * R * (tt - Math.Sin(tt))));
            }
            check("Công xôn cong (9 đoạn): sai lệch M3 dọc cung ≤ 2% qR²", devM / (q * R * R), 0, 0.02);
            check("Công xôn cong (9 đoạn): sai lệch T dọc cung ≤ 2% Tmax", devT / (q * R * R * (half - 1)), 0, 0.02);
        }

        // ---------------- 3. dầm thẳng không đổi khi độ vồng = 0 ----------------
        {
            Project Straight(double s)
            {
                var p = new Project();
                p.Materials.Add(Material.Concrete("B25"));
                p.FrameSections.Add(FrameSection.Rect("D22x40", 220, 400));
                p.Patterns.Add(new LoadPattern { Name = "P", Type = LoadType.Dead });
                p.Nodes.Add(new Node { Id = 1, X = 0, Y = 0, Z = 3, Restraint = Node.Fixed });
                p.Nodes.Add(new Node { Id = 2, X = 6, Y = 0, Z = 3, Restraint = Node.Fixed });
                p.Frames.Add(new FrameElement { Id = 1, NodeI = 1, NodeJ = 2, Section = "D22x40", Kind = FrameKind.Beam, CurveMidOffset = s, CurveSegments = 12 });
                p.FrameLoads.Add(FrameLoad.Uniform(1, "P", 10));
                return p;
            }
            var r0 = FrameAnalysis.Run(Straight(0)).Get("P").Frames[1];
            check("Độ vồng = 0: dầm thẳng ngàm 2 đầu M3 đầu I = −qL²/12", r0.At(0)[5], -30, 1e-9);
            check("Độ vồng = 0: M3 giữa nhịp = qL²/24", r0.At(3)[5], 15, 1e-9);
            check("Độ vồng = 0: số mặt cắt như dầm thẳng (13)", r0.Count, 13, 0);
            var r1 = FrameAnalysis.Run(Straight(0.002)).Get("P").Frames[1];
            check("Độ vồng 2 mm: gần như dầm thẳng (M3 đầu I)", r1.At(0)[5], -30, 0.01);
            check("Độ vồng 2 mm: gần như dầm thẳng (M3 giữa)", r1.At(r1.Stations[^1] / 2)[5], 15, 0.01);
        }

        // ---------------- 4. thiết kế: kiểm tra xoắn ----------------
        {
            double q = 2;
            var p = QuarterCantilever(0, q, false);
            var res = FrameAnalysis.Run(p);
            var ds = ConcreteDesigner.Design(p, res);
            var d = ds.Beams.FirstOrDefault(b => b.Frames.Contains(1));
            check("Thiết kế dầm cong: có kết quả", d != null ? 1 : 0, 1, 0);
            if (d != null)
            {
                check("Thiết kế dầm cong: đánh dấu IsCurved", d.IsCurved ? 1 : 0, 1, 0);
                check("Thiết kế dầm cong: R", d.CurveRadius, R, 1e-9);
                check("Thiết kế dầm cong: chiều dài nhịp = chiều dài cung", d.Spans[0].L, R * half, 1e-9);
                check("Thiết kế dầm cong: T max = q·R²·(π/2 − 1)", d.TorsionMax, q * R * R * (half - 1), 0.02);
                check("Thiết kế dầm cong: có 3 kiểm tra xoắn", d.Checks.Count(c => c.Name.StartsWith("Xoắn")), 3, 0);
                check("Thiết kế dầm cong: cốt dọc chịu xoắn Asl > 0", d.Spans[0].AsTorsion > 0 ? 1 : 0, 1, 0);
                check("Thiết kế dầm cong: Asl = T·uk/(2·Ak·Rs)", d.Spans[0].AsTorsion,
                    RcTorsion.LongitudinalArea(d.TorsionMax, 0.3, 0.5, p.Concrete.CoverBeam, p.Material(p.Concrete.RebarMain)), 1e-9);
                check("Thiết kế dầm cong: kiểm tra xoắn đạt (tải nhỏ)", d.Checks.Where(c => c.Name.StartsWith("Xoắn")).All(c => c.Ok) ? 1 : 0, 1, 0);
                var bot = d.Bars.FirstOrDefault(b => b.Role == "Dưới");
                check("Thiết kế dầm cong: thép dưới chạy suốt dài ≥ chiều dài cung − lớp bảo vệ", bot != null && bot.Length >= R * half - 2 * p.Concrete.CoverBeam - 0.011 ? 1 : 0, 1, 0);
                check("Thiết kế dầm cong: ghi chú đai kín", d.Messages.Any(m => m.Contains("ĐAI KÍN")) ? 1 : 0, 1, 0);
                // bản vẽ chi tiết dầm: ghi chú R, chiều dài cung
                var figs = BeamDrawing.Build(p, d);
                string dxf = figs[0].W.ToDxfString();
                check("Bản vẽ dầm cong: có ghi chú \"Dầm cong R=…, chiều dài cung L=…\"", dxf.Contains("cong R=4000") ? 1 : 0, 1, 0);
                // báo cáo Word
                string doc = Path.Combine(outDir, "DamCong_ThuyetMinh.docx");
                string xml = "";
                try
                {
                    CalculationReport.Export(p, res, ds, doc);
                    using var z = ZipFile.OpenRead(doc);
                    var e = z.GetEntry("word/document.xml");
                    if (e != null) using (var sr = new StreamReader(e.Open())) xml = sr.ReadToEnd();
                }
                catch (Exception ex) { Console.WriteLine("   · Lỗi xuất thuyết minh dầm cong: " + ex.Message); }
                check("Thuyết minh: có mục dầm cong (R, L cung, T max)", xml.Contains("Dầm cong trên mặt bằng") && xml.Contains("T max") ? 1 : 0, 1, 0);
            }
        }

        // ---------------- 5. xuất ETABS / SAP2000: các đoạn thẳng ----------------
        {
            var p = QuarterCantilever(0, 10, false);
            string e2k = Path.Combine(outDir, "DamCong.e2k");
            E2kExporter.Export(p, e2k, false);
            string t = File.ReadAllText(e2k);
            int nLines = t.Split('\n').Count(l => l.TrimStart().StartsWith("LINE  \"B1_"));
            check("e2k: dầm cong xuất thành 9 đoạn thẳng B1_1…B1_9", nLines, 9, 0);
            check("e2k: không còn thanh B1 nguyên", t.Contains("LINE  \"B1\"") ? 1 : 0, 0, 0);
            int nPts = t.Split('\n').Count(l => l.TrimStart().StartsWith("POINT \""));
            check("e2k: có 10 điểm (2 đầu + 8 điểm trung gian)", nPts, 10, 0);
            // tổng tải xuất = q·L cung
            double tot = 0;
            foreach (var l in t.Split('\n').Where(ln => ln.Contains("LINELOAD") && ln.Contains("TRAPF")))
            {
                var tok = l.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                double V(string key) => double.Parse(tok[tok.IndexOf(key) + 1], System.Globalization.CultureInfo.InvariantCulture);
                string seg = tok[1].Trim('"');
                int ks = int.Parse(seg.Substring(seg.IndexOf('_') + 1)) - 1;
                var cs = CurvedBeam.Segments(p, p.Frames[0])[ks];
                tot += (V("FSTART") + V("FEND")) / 2 * (V("RDEND") - V("RDSTART")) * cs.Chord;
            }
            check("e2k: tổng tải trên các đoạn = q·L cung", tot, 10 * R * half, 1e-5);
            string s2k = Path.Combine(outDir, "DamCong.s2k");
            S2kExporter.Export(p, s2k);
            string ts = File.ReadAllText(s2k);
            check("s2k: dầm cong xuất thành 9 đoạn 1_1…1_9", ts.Split('\n').Count(l => l.Contains("Frame=1_") && l.Contains("JointI=")), 9, 0);
            check("s2k: có nút trung gian 1_8", ts.Contains("Joint=1_8 ") ? 1 : 0, 1, 0);
        }

        // ---------------- 6. bản vẽ mặt bằng DXF: cung ARC ----------------
        {
            var p = QuarterCantilever(0, 10, false);
            var w = FramingPlan.Build(p, null, p.Stories[0], FramingPlan.SheetScale);
            string dxf = w.ToDxfString();
            int arcs = dxf.Split('\n').Count(l => l.Trim() == "ARC");
            check("DXF mặt bằng: dầm cong vẽ bằng 2 cung ARC (R ± b/2)", arcs, 2, 0);
            check("DXF mặt bằng: ghi bán kính R=4000", dxf.Contains("R=4000") ? 1 : 0, 1, 0);
            check("DXF mặt bằng: phạm vi bám theo cung (không lấy cả đường tròn)", w.Extents.X1 > -1000 ? 1 : 0, 1, 0);
            File.WriteAllText(Path.Combine(outDir, "DamCong_MatBang.dxf"), dxf);
        }

        // ---------------- 7. lưu / mở lại, tương thích file cũ ----------------
        {
            var p = QuarterCantilever(12, 10, false);
            string path = Path.Combine(outDir, "DamCong.kcvxt");
            p.Save(path);
            var p2 = Project.Load(path);
            check("Lưu/mở: giữ độ vồng", p2.Frames[0].CurveMidOffset, p.Frames[0].CurveMidOffset, 1e-12);
            check("Lưu/mở: giữ số đoạn chia", p2.Frames[0].CurveSegments, 12, 0);
            check("Lưu/mở: chiều dài cung", p2.Length(p2.Frames[0]), R * half, 1e-9);
            string json = File.ReadAllText(path);
            check("Lưu: không ghi thuộc tính suy diễn IsCurved", json.Contains("\"IsCurved\"") ? 1 : 0, 0, 0);
            var lines = json.Split('\n').Where(l => !l.Contains("CurveMidOffset") && !l.Contains("CurveSegments"));
            var old = System.Text.Json.JsonSerializer.Deserialize<Project>(string.Join("\n", lines), Project.JsonOptions);
            check("File cũ (không có CurveMidOffset): dầm thẳng", old.Frames[0].IsCurved ? 1 : 0, 0, 0);
        }

        // ---------------- 8. nhà khung: uốn cong dầm biên, tải sàn, cân bằng ----------------
        {
            var p = ModelBuilder.NewProject("Dầm cong ban công");
            ModelBuilder.SetGrid(p, new[] { 5.0 }, new[] { 4.5 });
            ModelBuilder.SetStories(p, new[] { 3.6, 3.3 }, -0.6);
            ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x30", BeamSectionX = "D22x30", BeamSectionY = "D22x40" });
            string st = "T2";
            var edge = ModelEdit.StoryBeams(p, st).First(f => Math.Abs(p.Node(f.NodeI).Y) < 1e-6 && Math.Abs(p.Node(f.NodeJ).Y) < 1e-6);
            // vồng ra ngoài nhà (phía −Y)
            double sg = p.Node(edge.NodeJ).X > p.Node(edge.NodeI).X ? -1.0 : 1.0;
            var er = ModelEdit.SetBeamCurvature(p, edge, sg);
            check("SetBeamCurvature: thành công", er.Ok ? 1 : 0, 1, 0);
            var arc = CurvedBeam.Arc(p, edge);
            check("SetBeamCurvature: cung vồng ra ngoài (y giữa cung = −1)", arc.Mid.Y, -1.0, 1e-9);
            check("SetBeamCurvature: R = (2,5² + 1)/2", arc.Radius, (6.25 + 1) / 2, 1e-9);
            var dup = ModelEdit.AddCurvedBeam(p, st, (0, 4.5), (5, 4.5), (2.5, 5.5), "D22x30");
            check("AddCurvedBeam: không cho trùng dầm có sẵn", dup.Ok ? 0 : 1, 1, 0);
            var res = FrameAnalysis.Run(p);
            // tổng hoạt tải: sàn đa giác dây cung + viên phân ngoài dây cung
            double expect = 0;
            foreach (var al in p.AreaLoads.Where(a => a.Pattern == "HT"))
            {
                var sl = p.Slabs.First(x => x.Id == al.Slab);
                expect += al.Q * KetCauVXT.Loads.SlabLoadDistributor.PolygonArea(sl.Nodes.Select(id => p.Node(id).P).ToList());
            }
            double qT2 = p.AreaLoads.Where(a => a.Pattern == "HT" && p.Slabs.First(x => x.Id == a.Slab).Story == st).Sum(a => a.Q);
            double seg = arc.Radius * arc.Radius / 2 * (arc.Angle - Math.Sin(arc.Angle));
            expect += qT2 * seg;
            double applied = p.FrameLoads.Where(l => l.Pattern == "HT").Sum(fl =>
            {
                double L = p.Length(p.Frame(fl.Element)), sm = 0;
                for (int i = 0; i + 1 < fl.Points.Count; i++) sm += (fl.Points[i][1] + fl.Points[i + 1][1]) / 2 * (fl.Points[i + 1][0] - fl.Points[i][0]) * L;
                return sm;
            }) - p.NodeLoads.Where(l => l.Pattern == "HT").Sum(l => l.Fz);
            check("Sàn + dầm cong: Σ tải dồn = q·(diện tích đa giác + viên phân)", applied, expect, 1e-6);
            check("Sàn + dầm cong: ΣFZ phản lực = Σ tải (HT)", res.Get("HT").Reactions.Values.Sum(r => r[2]), expect, 1e-6);
            check("Sàn + dầm cong: có tải viên phân trên dầm cong", p.FrameLoads.Any(l => l.Element == edge.Id && l.Note.Contains("viên phân")) ? 1 : 0, 1, 0);
            double tmax = 0;
            var fe = res.Get("HT").Frames[edge.Id];
            for (int i = 0; i < fe.Count; i++) tmax = Math.Max(tmax, Math.Abs(fe.T(i)));
            check("Dầm biên cong chịu xoắn (T ≠ 0)", tmax > 0.1 ? 1 : 0, 1, 0);
            var ds = ConcreteDesigner.Design(p, res);
            check("Thiết kế nhà có dầm cong: dầm cong có kiểm tra xoắn", ds.Beams.Any(b => b.IsCurved && b.Checks.Any(c => c.Name.StartsWith("Xoắn"))) ? 1 : 0, 1, 0);

            // thêm dầm cong mới qua ô sàn (giữa hai dầm biên trục 1, 2), phân tích lại được
            var p2 = p.Clone();
            int nf = p2.Frames.Count;
            var add = ModelEdit.AddCurvedBeam(p2, st, (0, 2.25), (5, 2.25), (2.5, 3.0), "D22x30");
            check("AddCurvedBeam: thêm dầm cong qua 3 điểm", add.Ok ? 1 : 0, 1, 0);
            var nb = add.Ok ? p2.Frames.FirstOrDefault(f => f.Id == add.NewFrames[0]) : null;
            check("AddCurvedBeam: dầm mới là dầm cong", nb != null && nb.IsCurved ? 1 : 0, 1, 0);
            check("AddCurvedBeam: độ vồng qua điểm thứ ba = 0,75 m", nb != null ? Math.Abs(nb.CurveMidOffset) : 0, 0.75, 1e-6);
            check("AddCurvedBeam: dầm trục 1, 2 được chia tại đầu dầm cong (+1 dầm cong, +2 đoạn chia)", p2.Frames.Count, nf + 3, 0);
            check("AddCurvedBeam: mô hình phân tích được", FrameAnalysis.Run(p2).Get("HT").Disp.Values.All(v => v.All(double.IsFinite)) ? 1 : 0, 1, 0);
            var st2 = ModelEdit.SetBeamCurvature(p2, nb, 0);
            check("SetBeamCurvature(0): trả về dầm thẳng", st2.Ok && !nb.IsCurved ? 1 : 0, 1, 0);
        }
    }
}
