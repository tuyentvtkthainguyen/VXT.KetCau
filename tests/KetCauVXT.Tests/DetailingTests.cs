using KetCauVXT.Design.Concrete;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Chế độ TRIỂN KHAI HỒ SƠ (Core/Detailing): dầm / cột / sàn nhập tay, móng đơn, móng băng (Winkler), đài cọc,
/// hồ sơ *.kcvxth (JSON), xuất bản vẽ DXF gộp / tách, Excel khối lượng, Word thuyết minh.
/// check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class DetailingTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var s = new DetailSettings();
        var conc = Material.Concrete("B25"); var main = Material.Rebar("CB400-V");

        // ---- dầm: Mu theo công thức TCVN 5574:2018 8.1.2.2.4, số đai ----
        var beam = new ManualBeam { B = 0.22, H = 0.40, Spans = "4,2; 3,6", BotN = 2, BotD = 16, AddBotN = 1, AddBotD = 16, TopN = 2, TopD = 16, SupN = 1, SupD = 16, MSpan = 50, MSupport = 60, Q = 70 };
        var rb = beam.Run(s);
        double As = 3 * Math.PI * 0.016 * 0.016 / 4, Asc = 2 * Math.PI * 0.016 * 0.016 / 4;
        double h0 = 0.40 - 0.025 - 0.008, a = 0.025 + 0.008;
        double x = (main.Rs * As - main.Rsc * Asc) / (conc.Rb * 0.22);
        double muExp = x >= 2 * a ? conc.Rb * 0.22 * x * (h0 - x / 2) + main.Rsc * Asc * (h0 - a)
            : Math.Max(main.Rs * As * (h0 - a), conc.Rb * 0.22 * Math.Min(main.Rs * As / (conc.Rb * 0.22), RcSection.XiR(main.Rs, main.E) * h0) * (h0 - Math.Min(main.Rs * As / (conc.Rb * 0.22), RcSection.XiR(main.Rs, main.E) * h0) / 2));
        var chkSpan = rb.Checks.First(c => c.Name.StartsWith("Chịu uốn giữa nhịp"));
        check("Dầm nhập tay: Mu nhịp = công thức TCVN 5574 8.1.2.2.4", chkSpan.Capacity, muExp, 1e-6);
        check("Dầm nhập tay: 2 nhịp → 2 BeamSpanResult", ((KetCauVXT.Design.BeamLineDesign)rb.Source).Spans.Count, 2, 0);
        // đai: nhịp thông thuỷ 3,98 m: vùng gối ⌈3,98/4/0,1⌉ = 10 đai → 1,0 m; giữa 1,98 m → ⌈1,98/0,2⌉ − 1 = 9; 2·(10+1) + 9 = 31; nhịp 3,38 m: ⌈8,45⌉ = 9 → 0,9 m, giữa 1,58 → 7; 2·10 + 7 = 27
        check("Dầm nhập tay: tổng số đai 2 nhịp = 31 + 27", rb.Bars.First(b => b.Role == "Đai").Count, 58, 0);
        check("Dầm nhập tay: mũ gối 3 vị trí, thép tăng cường 2 nhịp", rb.Bars.Count(b => b.Role == "Mũ gối") * 10 + rb.Bars.Count(b => b.Role == "Tăng cường dưới"), 32, 0);
        check("Dầm nhập tay: bê tông = b·h·ΣLo", rb.ConcreteVolume, 0.22 * 0.40 * (3.98 + 3.38), 1e-9);
        check("Dầm nhập tay: số hiệu thép duy nhất (RebarNumbering.Validate)", RebarNumbering.Validate(rb.Bars).Count, 0, 0);
        check("Dầm nhập tay: đạt với nội lực vừa phải", B(rb.Ok), 1, 0);
        var rbBad = new ManualBeam { MSpan = 500 }.Run(s);
        check("Dầm nhập tay: M quá lớn → LỖI", B(rbBad.Messages.Any(m => m.StartsWith("LỖI"))), 1, 0);
        check("Dầm nhập tay: danh sách nhịp rỗng không ném lỗi", B(new ManualBeam { Spans = "" }.Run(s).Messages.Any(m => m.StartsWith("LỖI"))), 1, 0);
        check("Danh sách nhịp \"3*4,2; 3,6\" = 4 nhịp", DetailShim.ParseList("3*4,2; 3,6").Count, 4, 0);
        var bf = beam.Draw(rb, s);
        check("Dầm nhập tay: vẽ mặt đứng + mặt cắt (BeamDrawing)", B(bf.Count >= 2 && bf.All(f => f.W.EntityCount > 10)), 1, 0);

        // ---- cột ----
        var col = new ManualColumn { B = 0.22, H = 0.30, Stories = "3,9; 3,6", Dia = 16, NB = 2, NH = 3, N = 650, Mx = 25, My = 12 };
        var rc = col.Run(s);
        check("Cột nhập tay: 2·2 + 2·(3 − 2) = 6 thanh", ((KetCauVXT.Design.ColumnDesign)rc.Source).Stories[0].TotalBars, 6, 0);
        check("Cột nhập tay: μ = 6Ø16/(b·h)", ((KetCauVXT.Design.ColumnDesign)rc.Source).Stories[0].Ratio, 6 * Math.PI * 0.016 * 0.016 / 4 / (0.22 * 0.30) * 100, 1e-9);
        check("Cột nhập tay: có kiểm tra Bresler", B(rc.Checks.Any(c => c.Name.Contains("Bresler"))), 1, 0);
        check("Cột nhập tay: vẽ được (ColumnDrawing)", B(col.Draw(rc, s).Count > 0), 1, 0);
        check("Cột N rất lớn → không đạt", B(new ManualColumn { N = 5000 }.Run(s).Ok), 0, 0);

        // ---- sàn ----
        var (ms, msup) = ManualSlab.Strip(10, 3.6, 2);
        check("Sàn: dải ngàm 2 đầu Mnh = qL²/24", ms, 10 * 3.6 * 3.6 / 24, 1e-12);
        check("Sàn: dải ngàm 2 đầu Mg = qL²/12", msup, 10 * 3.6 * 3.6 / 12, 1e-12);
        check("Sàn: khớp 2 đầu M = qL²/8", ManualSlab.Strip(10, 3, 0).Span, 11.25, 1e-12);
        var slab = new ManualSlab();
        var rs = slab.Run(s);
        check("Sàn nhập tay: tự chọn thép đạt", B(rs.Ok), 1, 0);
        check("Sàn nhập tay: có lớp dưới 2 phương + mũ", B(rs.Bars.Any(b => b.Role == "Lớp dưới phương ngắn") && rs.Bars.Any(b => b.Role == "Lớp dưới phương dài") && rs.Bars.Any(b => b.Role.StartsWith("Mũ"))), 1, 0);
        check("Sàn nhập tay: bê tông = lx·ly·t", rs.ConcreteVolume, 3.6 * 4.2 * 0.10, 1e-9);
        var rsUser = new ManualSlab { BotXD = 6, BotXS = 0.20, Live = 6 }.Run(s);
        check("Sàn: người dùng chọn Ø6a200 với hoạt tải 6 kN/m² → không đạt", B(rsUser.Ok), 0, 0);
        check("Sàn: vẽ mặt bằng + mặt cắt", slab.Draw(rs, s).Count, 2, 0);

        // ---- móng đơn ----
        var (ptb, pmax, pmin) = ManualFooting.Pressure(500, 30, 0, 2.0, 1.8, 20, 1.5);
        check("Móng đơn: ptb = N/A + γ·Df", ptb, 500 / 3.6 + 30, 1e-12);
        check("Móng đơn: pmax = ptb + 6M/(B·L²)", pmax, 500 / 3.6 + 30 + 6 * 30 / (1.8 * 4.0), 1e-12);
        var fo = new ManualFooting { N = 600, ML = 20, MB = 10, R = 150, Df = 1.5 };
        var rf = fo.Run(s);
        var fd = (KetCauVXT.Design.FootingDesign)rf.Source;
        check("Móng đơn tự chọn kích thước: ptb ≤ R", B(fd.Ptb <= 150 + 1e-9), 1, 0);
        check("Móng đơn tự chọn kích thước: pmax ≤ 1,2R", B(fd.Pmax <= 180 + 1e-9), 1, 0);
        check("Móng đơn: đạt mọi kiểm tra", B(rf.Ok), 1, 0);
        double h0f = fd.H - Math.Max(s.CoverFooting, 0.035) - 0.012;
        double fu = conc.Rbt * 2 * (0.22 + 0.30 + 2 * h0f) * h0f;
        check("Móng đơn: Fu chọc thủng = Rbt·u·h0", rf.Checks.First(c => c.Name.StartsWith("Chọc thủng")).Capacity, fu, 1e-9);
        check("Móng đơn: vẽ mặt bằng + mặt cắt (FootingDrawing)", fo.Draw(rf, s).Count, 2, 0);
        check("Móng đơn R quá nhỏ → LỖI, không ném", B(new ManualFooting { R = 20 }.Run(s).Messages.Any(m => m.StartsWith("LỖI"))), 1, 0);

        // ---- móng băng (Winkler) ----
        var st = new ManualStrip();
        var rst = st.Run(s);
        double sumNtc = st.Columns.Sum(c => c.N / 1.15), resist = 0;
        for (int i = 0; i < st.Xs.Length; i++)
        {
            double trib = (i > 0 ? (st.Xs[i] - st.Xs[i - 1]) / 2 : 0) + (i + 1 < st.Xs.Length ? (st.Xs[i + 1] - st.Xs[i]) / 2 : 0);
            resist += st.PressureTc[i] * trib * st.BUsed;
        }
        check("Móng băng: cân bằng ΣN tc = ∫p·B dx (Winkler)", resist, sumNtc, 1e-6);
        int nX = st.Xs.Length;
        check("Móng băng đối xứng: M(x) đối xứng", st.Moment[nX / 4], st.Moment[nX - 1 - nX / 4], 1e-3);
        check("Móng băng: mô men 2 đầu tự do ≈ 0", Math.Abs(st.Moment[0]) + 1, 1, 1e-6);
        check("Móng băng: có thép sườn + bản cánh + đai", B(new[] { "Dưới", "Trên", "Đai", "Lớp dưới phương ngang" }.All(rl => rst.Bars.Any(b => b.Role == rl))), 1, 0);
        check("Móng băng: vẽ mặt đứng + mặt cắt", st.Draw(rst, s).Count, 2, 0);

        // ---- đài cọc ----
        var P = ManualPileCap.Forces(1000, 0, 100, 0, new List<(double X, double Y)> { (-0.375, -0.375), (0.375, -0.375), (-0.375, 0.375), (0.375, 0.375) });
        check("Đài 4 cọc: Pmax = N/4 + My·x/Σx²", P.Max(), 250 + 100 * 0.375 / (4 * 0.375 * 0.375), 1e-12);
        check("Đài 4 cọc: Pmin", P.Min(), 250 - 100 * 0.375 / (4 * 0.375 * 0.375), 1e-12);
        var pc = new ManualPileCap { N = 1200, Mx = 30, My = 40, Pa = 400 };
        var rpc = pc.Run(s);
        check("Đài cọc tự chọn số cọc: Pmax ≤ [P]", B(pc.PileForces.Max() <= 400 + 1e-9), 1, 0);
        check("Đài cọc: ΣPi = N + G", pc.PileForces.Sum(), 1200 + 1.1 * 20 * pc.CapL * pc.CapB * 1.5, 1e-9);
        check("Đài cọc: khoảng cách cọc 3D đạt", B(rpc.Checks.First(c => c.Name.StartsWith("Khoảng cách")).Ok), 1, 0);
        check("Đài cọc: khối lượng cọc trong Extra", rpc.Extra.First(e => e.Unit == "cọc").Qty, pc.Piles.Count, 0);
        check("Đài cọc: vẽ mặt bằng + mặt cắt", pc.Draw(rpc, s).Count, 2, 0);

        // ---- hồ sơ JSON + xuất ----
        var prj = DetailingProject.Sample();
        var prj2 = DetailingProject.FromJson(prj.ToJson());
        check("Hồ sơ *.kcvxth: đọc lại đủ 9 nhóm cấu kiện", prj2.All.Count(), prj.All.Count(), 0);
        check("Hồ sơ *.kcvxth: giữ thông số (nhịp dầm, cột móng băng)", B(prj2.Beams[0].Spans == prj.Beams[0].Spans && prj2.Strips[0].Columns.Count == 4), 1, 0);
        var added = prj2.AddNew("Dam");
        check("Thêm dầm mới: tên D2 (không trùng)", B(added.Name == "D2"), 1, 0);
        var res = prj.RunAll();
        check("Tính toàn bộ hồ sơ mẫu: 9 kết quả, không ngoại lệ", B(res.Count == 9 && res.All(r => !r.Result.Messages.Any(m => m.StartsWith("LỖI tính toán")))), 1, 0);
        var sum = DetailingExport.Summary(res);
        var rows = RebarSchedule.BuildRows(sum);
        check("Bảng thống kê: có thép móng bè / bể nước / DƯL (DesignSummary.Others)", B(new[] { "Móng bè", "Bể nước", "Dự ứng lực", "Móng cọc", "Sàn" }.All(c => rows.Any(r => r.Category == c))), 1, 0);
        var q = DetailingExport.Quantities(prj, res);
        check("Khối lượng: bê tông dầm = tổng bê tông 1 CK × SL", q.First(r => r.Group == "Dầm" && r.Unit == "m³").Qty, res.Where(r => r.Result.Kind == "Dầm").Sum(r => r.Result.ConcreteVolume * r.Result.Quantity), 1e-9);
        check("Khối lượng: thép theo 3 nhóm đường kính = tổng thép", q.Where(r => r.Unit == "kg" && r.Item.StartsWith("Cốt thép")).Sum(r => r.Qty), res.Sum(r => r.Result.SteelWeight * r.Result.Quantity), 1e-6);
        string dir = Path.Combine(outDir, "TrienKhai");
        Directory.CreateDirectory(dir);
        var log = new List<string>();
        var one = DetailingExport.Drawings(prj, res, Path.Combine(dir, "Gop"), false, log);
        var many = DetailingExport.Drawings(prj, res, Path.Combine(dir, "TachRoi"), true, log);
        foreach (var l in log) Console.WriteLine("    " + l);
        check("Xuất bản vẽ gộp: 1 file DXF", one.Count, 1, 0);
        check("Xuất bản vẽ tách rời: 9 cấu kiện + 1 thống kê thép", many.Count, 10, 0);
        check("Xuất bản vẽ: không lỗi vẽ", log.Count, 0, 0);
        check("Bản vẽ gộp có chữ tiếng Việt mã hoá", B(File.ReadAllText(one[0]).Contains(DxfWriter.Encode("THỐNG KÊ"))), 1, 0);
        string xl = Path.Combine(dir, "KhoiLuong_TrienKhai.xlsx"), wd = Path.Combine(dir, "ThuyetMinh_TrienKhai.docx");
        DetailingExport.Excel(prj, res, xl);
        DetailingExport.Word(prj, res, wd);
        check("Excel khối lượng ghi được", B(new FileInfo(xl).Length > 2000), 1, 0);
        check("Word thuyết minh ghi được", B(new FileInfo(wd).Length > 4000), 1, 0);
        prj.Save(Path.Combine(dir, "HoSoMau" + DetailingProject.Extension));
        check("Lưu / mở file *.kcvxth", DetailingProject.Load(Path.Combine(dir, "HoSoMau" + DetailingProject.Extension)).All.Count(), 9, 0);
    }
}
