using System.Text.Json;
using System.Text.Json.Nodes;
using KetCauVXT.Analysis;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Loads;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng cầu thang bộ (Model/Stair.cs, Loads/StairLoadGenerator.cs, StairDesigner, StairDrawing, StairQuantity):
/// ô thang 2,4 × 4,5 m tầng T2 (+3,600) → T3 (+6,900), H = 3,3 m; thang 2 vế chữ U 20 bậc h = 165, b = 270, vế rộng 1,15 m, bản 120.
/// Giá trị kỳ vọng tính tay. check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class StairTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        Console.WriteLine("\n=== CẦU THANG BỘ: HÌNH HỌC, TẢI, THIẾT KẾ, BẢN VẼ, KHỐI LƯỢNG ===");
        double B(bool v) => v ? 1 : 0;

        Project NewP()
        {
            var p = ModelBuilder.NewProject("Thử cầu thang");
            ModelBuilder.SetGrid(p, new[] { 2.4, 4.0 }, new[] { 4.5 });
            ModelBuilder.SetStories(p, new[] { 3.6, 3.3, 3.3 }, 0.0);
            ModelBuilder.BuildFromGrid(p, new ModelBuilder.GridFrameOptions { ColumnSection = "C22x22", BeamSectionX = "D22x30", BeamSectionY = "D22x30", SlabSection = "S100" });
            ModelEdit.AddOpening(p, "T3", 1.2, 2.25, OpeningKind.Stair);
            return p;
        }

        // ================= cầu thang mặc định (chỉ có lỗ sàn) =================
        var p0 = NewP();
        check("CT: dự án chỉ có lỗ sàn cầu thang, chưa khai báo", p0.Stairs.Count, 0, 0);
        var q0 = QuantityCalculator.Compute(p0, null);
        var L0 = Stair.Effective(p0)[0].Layout(p0);
        check("CT mặc định: hình học hợp lệ", B(L0.Valid), 1, 0);
        check("CT mặc định: 2h + b trong 600…650 mm", B(L0.StepOk), 1, 0);
        double v0 = L0.Flights.Sum(f => (f.Risers - 1) * Math.Sqrt(L0.Tread * L0.Tread + L0.Riser * L0.Riser) * L0.FlightWidth * 0.12)
                    + (L0.Lr - L0.LandingBeamU - 0.11) * L0.Wr * 0.12 + 0.22 * 0.30 * L0.Wr;
        check("CT mặc định: bê tông = bản thang + chiếu nghỉ + dầm CN (không kể bậc)", q0.Volume(QtyCat.Stair), v0, 1e-6);
        check("CT mặc định: có khối xây bậc gạch", B(q0.StairWorks.Any(x => x.Work == "Xây bậc thang gạch" && x.Quantity > 0)), 1, 0);

        // ================= khai báo cầu thang =================
        var p = NewP();
        var op = p.Openings.First(o => o.Kind == OpeningKind.Stair);
        check("CT: lỗ sàn có tải ước tính cũ OPEN", B(p.FrameLoads.Any(l => l.Source == "OPEN" + op.Id)), 1, 0);
        var s = StairEdit.Draft(p, op);
        s.Risers = 20; s.Tread = 0.27; s.WaistThickness = 0.12; s.Gap = 0.10;
        s.Steps = StepConstruction.Brick; s.Finish = StepFinish.Granite; s.Railing = RailingKind.Steel;
        var er = StairEdit.Apply(p, s);
        check("CT: khai báo được", B(er.Ok), 1, 0);
        check("CT: bỏ tải ước tính cũ của lỗ sàn", p.FrameLoads.Count(l => l.Source == "OPEN" + op.Id), 0, 0);
        var L = p.Stairs[0].Layout(p);
        double h = 3.3 / 20, b = 0.27, w = (2.4 - 0.1) / 2, diag = Math.Sqrt(b * b + h * h), cos = b / diag, incl = 9 * diag;
        check("Hình học: chiều cao tầng 3,3 m", L.H, 3.3, 1e-9);
        check("Hình học: 20 cổ bậc", L.NRisers, 20, 0);
        check("Hình học: h = 3300/20 = 165 mm", L.Riser, h, 1e-9);
        check("Hình học: b = 270 mm", L.Tread, b, 1e-9);
        check("Hình học: 2h + b = 600 mm", L.Check2hb, 0.60, 1e-9);
        check("Hình học: 2 vế, mỗi vế 10 bậc", L.Flights.Count * 100 + L.Flights[0].Risers, 210, 0);
        check("Hình học: bề rộng vế = (2,4 − 0,1)/2", L.FlightWidth, w, 1e-9);
        check("Hình học: L nghiêng = 9·√(b² + h²)", L.Flights[0].Inclined, incl, 1e-9);
        check("Hình học: cosα = b/√(b²+h²)", L.Flights[0].CosA, cos, 1e-9);
        check("Hình học: dầm chiếu nghỉ tại u = 9·b", L.LandingBeamU, 9 * b, 1e-9);
        double ld = 4.5 - 9 * b;
        check("Hình học: chiếu nghỉ sâu 4,5 − 2,43", L.Landings[0].Span, ld, 1e-9);
        check("Hình học: cao độ chiếu nghỉ = 3,6 + 10·h", L.Landings[0].Z, 3.6 + 10 * h, 1e-9);

        // ---- tải trọng (TCVN 2737:2023) ----
        var set = StairLoadCalc.Compute(p, L);
        double kf = (b + h) / b;
        double gStd = 25 * 0.12 / cos + 18 * h / 2 + 28 * 0.02 * kf + 18 * 0.02 * kf + 18 * 0.015 / cos + 0.3 / w + 3.0;
        check("Tải vế tiêu chuẩn = BT/cosα + gạch h/2 + granite + vữa lót + trát + lan can + 3,0", set.FlightStd, gStd, 1e-3);
        double gTT = 25 * 0.12 / cos * 1.1 + (18 * h / 2 + 28 * 0.02 * kf + 18 * 0.02 * kf + 18 * 0.015 / cos + 0.3 / w) * 1.3 + 3.0 * (p.Pattern("HT")?.Gamma ?? 1.2);
        check("Tải vế tính toán (γf TT 1,1; HTHT 1,3; HT theo dự án)", set.FlightDesign, gTT, 1e-3);
        check("Tải chiếu nghỉ tiêu chuẩn = 25·0,12 + 28·0,02 + 18·0,02 + 18·0,015 + 3,0", set.LandingStd, 3.0 + 0.56 + 0.36 + 0.27 + 3.0, 1e-3);
        check("Hoạt tải cầu thang 3,0 kN/m² (TCVN 2737:2023)", set.FlightOf("HT"), 3.0, 1e-9);

        // ---- truyền tải vào khung ----
        var warn = StairLoadGenerator.Apply(p);
        double Sum(string pat) =>
            p.FrameLoads.Where(l => l.Source == StairLoadGenerator.Source && l.Pattern == pat).Sum(l =>
            {
                double Lf = p.Length(p.Frame(l.Element)), t = 0;
                for (int i = 0; i + 1 < l.Points.Count; i++) t += (l.Points[i + 1][0] - l.Points[i][0]) * Lf * (l.Points[i][1] + l.Points[i + 1][1]) / 2;
                return t + l.PointLoads.Sum(x => x[1]);
            })
            + p.AreaLoads.Where(a => a.Source == StairLoadGenerator.Source && a.Pattern == pat).Sum(a =>
            {
                var sl = p.Slabs.First(x => x.Id == a.Slab);
                var pts = sl.Nodes.Select(id => p.Node(id)).Select(n => (n.X, n.Y)).ToList();
                return a.Q * Math.Abs(QuantityCalculator.Area(pts));
            });
        double live = 3.0 * (2 * 9 * b * w + ld * 2.4);
        check("Truyền tải HT: Σ tải lên khung = 3,0 × (2 vế × L0 × w + chiếu nghỉ)", Sum("HT"), live, 1e-6);
        check("Truyền tải TT: Σ = tải bản + chiếu nghỉ + dầm CN", Sum("TT"), StairLoadGenerator.TotalLoad(p, L, "TT"), 1e-6);
        check("Truyền tải HTHT: Σ = bậc + ốp + trát + lan can", Sum("HTHT"), StairLoadGenerator.TotalLoad(p, L, "HTHT"), 1e-6);
        check("Truyền tải: không có cảnh báo bỏ tải", warn.Count(x => x.Contains("bỏ qua")), 0, 0);
        var r = FrameAnalysis.Run(p);
        double react = r.Get("HT").Reactions.Values.Sum(x => x[2]);
        double applied = p.FrameLoads.Where(l => l.Pattern == "HT").Sum(l => { double Lf = p.Length(p.Frame(l.Element)), t = 0; for (int i = 0; i + 1 < l.Points.Count; i++) t += (l.Points[i + 1][0] - l.Points[i][0]) * Lf * (l.Points[i][1] + l.Points[i + 1][1]) / 2; return t + l.PointLoads.Sum(x => x[1]); });
        check("Phân tích: HT có cầu thang – ΣFZ phản lực ≥ tải dầm (cân bằng)", B(react >= applied - 1e-3), 1, 0);

        // ---- thiết kế ----
        var ds = ConcreteDesigner.Design(p, r);
        check("Thiết kế: 1 cầu thang", ds.Stairs.Count, 1, 0);
        var sd = ds.Stairs[0];
        var bt = sd.Parts.First(x => x.Kind == "Bản thang");
        check("Thiết kế: 2 vế giống nhau gộp SL = 2", bt.Quantity, 2, 0);
        check("Thiết kế: bản thang M = q·L0²/8, L0 = 9·b", bt.M, set.FlightDesign * (9 * b) * (9 * b) / 8, 1e-6);
        var cs = p.Concrete;
        var conc = p.Material(cs.ConcreteSlab);
        int dMain = sd.Parts.First(x => x.Kind == "Bản thang").Bars.First(x => x.Role == "Dưới").Dia;
        var stl = p.Material(dMain >= 10 ? cs.RebarMain : cs.RebarStirrup);
        double asHand = RcSection.DesignRect(bt.M, 1.0, 0.12 - cs.CoverSlab - dMain / 2000.0, 0.02, conc, stl, 0.10).As;
        check("Thiết kế: As bản thang bố trí ≥ As yêu cầu (tính tay)", B(bt.AsProv >= asHand - 1e-9), 1, 0);
        check("Thiết kế: mọi bộ phận As bố trí ≥ yêu cầu", B(sd.Parts.All(x => x.AsProv >= x.AsReq - 1e-9)), 1, 0);
        check("Thiết kế: có chiếu nghỉ, dầm chiếu nghỉ", B(sd.Parts.Any(x => x.Kind == "Chiếu nghỉ") && sd.Parts.Any(x => x.Kind == "Dầm chiếu nghỉ")), 1, 0);
        check("Thiết kế: số hiệu thép mỗi bộ phận đánh từ 1, thép dọc trước thép rải",
            B(sd.Parts.All(x => x.Bars.Min(m => m.Mark) == 1 && x.Bars.Where(m => m.Role.StartsWith("Thép rải")).All(m => x.Bars.Where(n => n.Role == "Dưới").All(n => n.Mark < m.Mark)))), 1, 0);
        check("Thiết kế: cầu thang đạt", B(sd.Ok), 1, 0);

        // ---- bản vẽ ----
        Directory.CreateDirectory(outDir);
        var figs = StairDrawing.Build(p, ds, sd, 50);
        check("Bản vẽ: mặt bằng + 2 mặt cắt + chiếu nghỉ + dầm + bảng + ghi chú", B(figs.Count >= 6), 1, 0);
        var wsh = SheetComposer.Compose(figs, 50, new SheetComposer.SheetInfo { ProjectName = p.Name, Title = "CẦU THANG", SheetNo = "KC-08" }, "A1");
        string dxf = Path.Combine(outDir, "KC-08_CauThang.dxf");
        wsh.Save(dxf);
        string txt = File.ReadAllText(dxf);
        check("Bản vẽ DXF: tồn tại, không rỗng", B(File.Exists(dxf) && new FileInfo(dxf).Length > 2000), 1, 0);
        check("Bản vẽ DXF: đủ layer BE_TONG, THEP, DAI, KICH_THUOC, CHU, KY_HIEU",
            B(new[] { "BE_TONG", "THEP", "DAI", "KICH_THUOC", "CHU", "KY_HIEU" }.All(x => txt.Contains("\n" + x + "\r") || txt.Contains(x))), 1, 0);
        check("Bản vẽ DXF: chữ mã hoá \\U+XXXX, nhãn Ø (\\U+00D8)", B(!txt.Any(ch => ch > 127) && txt.Contains("\\U+00D8")), 1, 0);
        var texts = figs.SelectMany(f => f.W.Texts()).ToList();
        check("Bản vẽ: có MẶT CẮT 1-1, BẢNG THỐNG KÊ, GHI CHÚ một khối", B(texts.Any(x => x.Contains("MẶT CẮT 1-1")) && texts.Any(x => x.StartsWith("BẢNG THỐNG KÊ CỐT THÉP")) && texts.Count(x => x == "GHI CHÚ:") == 1), 1, 0);
        var rows = StairSchedule.Rows(ds);
        check("Bảng thống kê: trọng lượng = 0,00617·d²·L", rows.Sum(x => x.WeightKg), rows.Sum(x => Math.Round(0.00617 * x.Dia * x.Dia * x.TotalLengthM, 2)), 1e-6);
        var all = new List<ScheduleRow>(); StairSchedule.AppendTo(all, ds); StairSchedule.AppendTo(all, ds);
        check("Bảng thống kê: AppendTo không trùng khi gọi 2 lần", all.Count, rows.Count, 0);
        var shv = SheetBuilder.Figures(p, r, ds, new SheetView { Kind = SheetViewKind.StairDetail, Param = sd.Name });
        check("Dàn trang: khung nhìn Chi tiết cầu thang có hình", B(shv.Count >= 6), 1, 0);
        check("Dàn trang: bộ tờ mặc định có tờ cầu thang", B(SheetBuilder.DefaultSet(p, ds, r).Any(x => x.Views.Any(v => v.Kind == SheetViewKind.StairDetail))), 1, 0);

        // ---- khối lượng ----
        var q = QuantityCalculator.Compute(p, ds);
        double vSlab = 2 * incl * w * 0.12, vLand = (ld - 0.11) * 2.4 * 0.12, vBeam = 0.22 * 0.30 * 2.4;
        check("KL: bê tông cầu thang = 2 bản thang + chiếu nghỉ + dầm CN (không kể bậc)", q.Volume(QtyCat.Stair), vSlab + vLand + vBeam, 1e-6);
        double vSteps = 2 * 9 * 0.5 * b * h * w;
        check("KL: xây bậc gạch = Σ ½·b·h·w = 18 × ½×0,27×0,165×1,15", q.StairWorks.Where(x => x.Work == "Xây bậc thang gạch").Sum(x => x.Quantity), vSteps, 1e-6);
        double aGr = 2 * (9 * b + 10 * h) * w + ld * 2.4;
        check("KL: ốp granite = Σ((n−1)·b + n·h)·w + mặt chiếu nghỉ", q.StairWorks.Where(x => x.Work == "Ốp lát bậc thang đá granite").Sum(x => x.Quantity), aGr, 1e-6);
        check("KL: gạch xây bậc = V × 550 viên", q.MaterialLines.Where(x => x.Source == "Xây bậc thang gạch").Sum(x => x.Quantity), vSteps * 550, 1e-6);
        check("KL: thép cầu thang vào tổng cốt thép", B(q.RebarRows.Any(x => x.Category == StairSchedule.Category)), 1, 0);
        // bậc đổ BTCT → vào bê tông, không còn khối xây
        var p2 = NewP();
        var s2 = s.Clone(); s2.Id = 0; s2.Steps = StepConstruction.Concrete; s2.OpeningId = p2.Openings.First(o => o.Kind == OpeningKind.Stair).Id;
        StairEdit.Apply(p2, s2);
        var q2 = QuantityCalculator.Compute(p2, null);
        check("KL bậc BTCT: bê tông = bản + chiếu nghỉ + dầm + bậc", q2.Volume(QtyCat.Stair), vSlab + vLand + vBeam + vSteps, 1e-6);
        check("KL bậc BTCT: không có khối xây bậc", q2.StairWorks.Count(x => x.Kind == "Xây"), 0, 0);
        // dầm cốn 2 bên
        var p3 = NewP();
        var s3 = s.Clone(); s3.Id = 0; s3.Support = StairSupport.Stringer; s3.StringerSides = 2; s3.StringerB = 0.15; s3.StringerH = 0.30;
        s3.OpeningId = p3.Openings.First(o => o.Kind == OpeningKind.Stair).Id;
        StairEdit.Apply(p3, s3);
        var q3 = QuantityCalculator.Compute(p3, null);
        double vStr = 2 * incl * (w - 0.30) * 0.12 + vLand + vBeam + 2 * 2 * 0.15 * 0.30 * incl;
        check("KL dầm cốn: bê tông = bản giữa dầm cốn + chiếu nghỉ + dầm CN + 4 dầm cốn", q3.Volume(QtyCat.Stair), vStr, 1e-6);
        var d3 = StairDesigner.DesignOne(p3, p3.Stairs[0]);
        check("Thiết kế dầm cốn: có dầm cốn, thép bản ngang vế", B(d3.Parts.Any(x => x.Kind == "Dầm cốn") && d3.Parts.First(x => x.Kind == "Bản thang").MainAlongFlight == false), 1, 0);

        // ---- JSON ----
        string path = Path.Combine(outDir, "CauThang.kcvxt");
        p.Save(path);
        var pl = Project.Load(path);
        var sl2 = pl.Stairs.FirstOrDefault();
        check("JSON: lưu / mở giữ cầu thang", B(sl2 != null && sl2.Risers == 20 && sl2.Finish == StepFinish.Granite && sl2.Steps == StepConstruction.Brick && sl2.OpeningId == op.Id && Math.Abs(sl2.Tread - 0.27) < 1e-12), 1, 0);
        check("JSON: enum ghi dạng chữ", B(File.ReadAllText(path).Contains("\"TwoFlightU\"")), 1, 0);
        var node = JsonNode.Parse(JsonSerializer.Serialize(p0, Project.JsonOptions)).AsObject();
        node.Remove("Stairs");
        var old = JsonSerializer.Deserialize<Project>(node.ToJsonString(), Project.JsonOptions);
        check("JSON: dự án cũ không có Stairs → danh sách rỗng", B(old.Stairs != null && old.Stairs.Count == 0), 1, 0);
        var qs = new QuantitySettings();
        var qsNode = JsonNode.Parse(JsonSerializer.Serialize(qs, Project.JsonOptions)).AsObject();
        qsNode.Remove("StairSteps"); qsNode.Remove("StairFinish");
        var qsOld = JsonSerializer.Deserialize<QuantitySettings>(qsNode.ToJsonString(), Project.JsonOptions);
        check("JSON: QuantitySettings cũ → bậc xây gạch, ốp granite", B(qsOld.StairSteps == StepConstruction.Brick && qsOld.StairFinish == StepFinish.Granite), 1, 0);

        // ---- xoá khai báo → trả tải ước tính cũ ----
        StairEdit.Remove(p, p.Stairs[0]);
        check("Xoá cầu thang: lỗ sàn trở lại tải ước tính OPEN", B(p.FrameLoads.Any(l => l.Source == "OPEN" + op.Id)), 1, 0);
        StairLoadGenerator.Apply(p);
        check("Xoá cầu thang: không còn tải STAIR", p.FrameLoads.Count(l => l.Source == StairLoadGenerator.Source), 0, 0);
        Console.WriteLine($"   · DXF cầu thang: {dxf} ({wsh.EntityCount} thực thể)");
    }
}
