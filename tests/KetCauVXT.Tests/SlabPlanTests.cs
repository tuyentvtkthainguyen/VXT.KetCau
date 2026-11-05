using KetCauVXT.Design.Concrete;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;

namespace KetCauVXT.Tests;

/// <summary>Mặt bằng sàn nhiều ô (Core/Detailing/SlabPlanDesigner.cs): hạ cốt, liên kết cạnh, thép mũ nhấn / neo riêng,
/// lỗ khoét + gia cường, lỗ thông tầng, chế độ thủ công. check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class SlabPlanTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var s = new DetailSettings();

        // ---- mẫu: lưới 2×2, ô 2-1 vệ sinh hạ −50, lỗ L1 0,8×0,8 ----
        var sp = ManualSlabPlan.Sample();
        var r = sp.Run(s);
        foreach (var m in r.Messages.Where(x => x.StartsWith("LỖI"))) Console.WriteLine("    [sàn mẫu] " + m);
        check("Sàn MB: lưới X \"4,2; 3,6\" → 3 trục, trục cuối 7,8 m", sp.Xs.Length * 10 + sp.Xs[^1], 37.8, 1e-12);
        check("Sàn MB: lưới Y \"4,5; 3,3\" → trục cuối 7,8 m", sp.Ys[^1], 7.8, 1e-12);
        check("Sàn MB: 4 ô", sp.PanelList.Count, 4, 0);
        check("Sàn MB mẫu: đạt mọi kiểm tra", B(r.Ok), 1, 0);
        check("Sàn MB mẫu: Kind = Mặt bằng sàn", B(r.Kind == "Mặt bằng sàn"), 1, 0);
        var p11 = sp.PanelList.First(p => p.I == 0 && p.J == 0);
        var p21 = sp.PanelList.First(p => p.I == 1 && p.J == 0);
        var p12 = sp.PanelList.First(p => p.I == 0 && p.J == 1);
        check("Ô 2-1 hạ −50 mm", p21.Drop, -0.05, 1e-12);
        check("Ô 1-1 – 2-1 chênh 50 > ngưỡng bậc 30 → cạnh phải ô 1-1 khớp", B(p11.FixR), 0, 0);
        check("Ô 2-1: cạnh trái khớp (bậc sàn)", B(p21.FixL), 0, 0);
        check("Ô 2-1: cạnh trên khớp (ô 2-2 không hạ)", B(p21.FixT), 0, 0);
        check("Ô 1-1 – 1-2 cùng cốt → liên tục", B(p11.FixT && p12.FixB), 1, 0);
        check("Ô 1-1 cạnh trái là biên → khớp", B(p11.FixL), 0, 0);
        // ô 1-1: 1 cạnh ngàm mỗi phương → Marcus kx = 2,08·4,2⁴, ky = 2,08·4,5⁴; q = 1,1·25·0,1 + 1,2·1,5 + 1,2·2 = 6,95
        double q = 1.1 * 25 * 0.1 + 1.2 * 1.5 + 1.2 * 2.0;
        check("Ô 1-1: q tính toán = 6,95 kN/m²", p11.Q, 6.95, 1e-12);
        double kx = 2.08 * Math.Pow(4.2, 4), ky = 2.08 * Math.Pow(4.5, 4), qx = q * ky / (kx + ky);
        check("Ô 1-1: Mx nhịp = 9·qx·lx²/128 (ngàm–khớp)", p11.MxSpan, 9 * qx * 4.2 * 4.2 / 128, 1e-9);
        check("Ô 1-1: Mx gối = qx·lx²/8", p11.MxSup, qx * 4.2 * 4.2 / 8, 1e-9);
        check("Ô 2-1 (khớp X, khớp Y) Mx = qx·lx²/8 với kx = 5·3,6⁴, ky = 5·4,5⁴",
            p21.MxSpan, q * (5 * Math.Pow(4.5, 4)) / (5 * Math.Pow(3.6, 4) + 5 * Math.Pow(4.5, 4)) * 3.6 * 3.6 / 8, 1e-9);

        // ---- thép mũ nhấn ----
        var crank = sp.TopLines.First(t => t.Vertical && t.K == 1 && t.Seg == 0);
        check("Trục 2 (A-B): thép mũ NHẤN (Δ = 50 ≤ ngưỡng nhấn 50)", B(crank.Kind == "Nhấn"), 1, 0);
        check("Thép mũ nhấn có trong danh sách thép", r.Bars.Count(b => b.Role.StartsWith("Thép mũ nhấn trục 2 (A-B)")), 1, 0);
        var (run, inc) = ManualSlabPlan.Crank(0.05);
        check("Nhấn 1:6: đoạn ngang = 6·Δ = 300 mm", run, 0.30, 1e-12);
        check("Nhấn 1:6: đoạn xiên = √(300² + 50²)", inc, Math.Sqrt(0.09 + 0.0025), 1e-12);
        var cb = crank.Marks[0];
        check("Thép nhấn: 5 đoạn (chân – ngang – xiên – ngang – chân)", cb.Segments.Count, 5, 0);
        check("Thép nhấn: đoạn xiên 304 mm", cb.Segments[2], 304, 0);
        // Ext = max(4,2; 3,6)/4 + 0,22/2 = 1,16 m → ngang mỗi bên = 1,16 − 0,15 = 1,01 m
        check("Thép nhấn: đoạn ngang = Ext − 6Δ/2 = 1010 mm", cb.Segments[1], 1010, 0);
        check("Thép nhấn: hình dạng \"Nhấn\", lớp trên", B(cb.Shape == "Nhấn" && cb.IsTop), 1, 0);
        check("Thép nhấn: chiều dài = tổng các đoạn", cb.Length, Math.Round(cb.Segments.Sum() / 10.0) / 100.0, 1e-9);
        check("Trục 2 (B-C) hai ô cùng cốt → mũ thẳng có M > 0", B(sp.TopLines.First(t => t.Vertical && t.K == 1 && t.Seg == 1).Kind == "Thẳng" && sp.TopLines.First(t => t.Vertical && t.K == 1 && t.Seg == 1).M > 0), 1, 0);

        // ---- cắt thép neo riêng ----
        var cut = ManualSlabPlan.Sample(); cut.CrankMode = ManualSlabPlan.CrankCut;
        var rc = cut.Run(s);
        check("Chọn \"cắt thép\": 2 thanh mũ neo riêng tại trục 2 (A-B)", rc.Bars.Count(b => b.Role.StartsWith("Mũ gối (neo riêng) trục 2 (A-B)")), 2, 0);
        check("Chọn \"cắt thép\": không còn thép nhấn", rc.Bars.Count(b => b.Shape == "Nhấn"), 0, 0);
        var lim = ManualSlabPlan.Sample(); lim.CrankLimit = 0.03;
        lim.Run(s);
        check("Δ = 50 > ngưỡng nhấn 30 → neo riêng", B(lim.TopLines.First(t => t.Vertical && t.K == 1 && t.Seg == 0).Kind == "Neo riêng"), 1, 0);
        var small = ManualSlabPlan.Sample(); small.Panels[0].Drop = -0.02;
        var rsm = small.Run(s);
        var tsm = small.TopLines.First(t => t.Vertical && t.K == 1 && t.Seg == 0);
        check("Δ = 20 ≤ ngưỡng bậc 30 → cạnh liên tục, mũ nhấn chịu mô men gối", B(tsm.Continuous && tsm.Kind == "Nhấn" && tsm.M > 0 && small.PanelList[1].FixL), 1, 0);
        check("Δ = 20: vẫn đạt", B(rsm.Ok), 1, 0);

        // ---- lỗ khoét ----
        check("Lỗ L1: thép gia cường phương X, Y", B(r.Bars.Any(b => b.Role == "Gia cường lỗ L1 phương X") && r.Bars.Any(b => b.Role == "Gia cường lỗ L1 phương Y")), 1, 0);
        check("Lỗ L1: gia cường dài = 0,8 + 2·40·12 mm = 1,76 m", r.Bars.First(b => b.Role == "Gia cường lỗ L1 phương X").Length, 1.76, 1e-9);
        int nFull = Dt.CountAcross(4.5 - 0.22, 0.05, p11.BotX.S), nCut = (int)Math.Ceiling(0.8 / p11.BotX.S - 1e-9);
        check("Lỗ L1: số thanh X suốt của ô 1-1 giảm đúng số thanh bị cắt", p11.BarX.Count, nFull - nCut, 0);
        check("Lỗ L1: 2 nhóm đoạn ngắn X, mỗi nhóm = số thanh bị cắt", r.Bars.Where(b => b.Role.StartsWith("Ô 1-1 lớp dưới X (cắt lỗ L1)")).Sum(b => b.Count), 2 * nCut, 0);
        var big = ManualSlabPlan.Sample(); big.Openings[0].W = 2.0;
        var rbig = big.Run(s);
        check("Lỗ 2,0 m > 1/3·4,2 m → cảnh báo, không đạt", B(rbig.Messages.Any(m => m.StartsWith("Cảnh báo lỗ L1")) && !rbig.Ok), 1, 0);
        var outside = ManualSlabPlan.Sample(); outside.Openings[0].X = 20;
        check("Lỗ ngoài lưới → LỖI, không ném", B(outside.Run(s).Messages.Any(m => m.StartsWith("LỖI lỗ L1"))), 1, 0);

        // ---- lỗ thông tầng (cả ô), khối lượng ----
        var vd = new ManualSlabPlan { Panels = { new SlabPanelRow { Col = 2, Row = 2, Function = ManualSlabPlan.VoidFunction } } };
        var rv = vd.Run(s);
        check("Ô 2-2 lỗ thông tầng: bê tông = t·(4,2·4,5 + 3,6·4,5 + 4,2·3,3)", rv.ConcreteVolume, 0.1 * (4.2 * 4.5 + 3.6 * 4.5 + 4.2 * 3.3), 1e-9);
        check("Lỗ thông tầng: ván khuôn = diện tích sàn", rv.FormworkArea, 4.2 * 4.5 + 3.6 * 4.5 + 4.2 * 3.3, 1e-9);
        check("Lỗ thông tầng: không có thép ô 2-2", rv.Bars.Count(b => b.Role.StartsWith("Ô 2-2")), 0, 0);
        check("Lỗ thông tầng: dầm trục 2 (B-C) thành mép → mũ biên", B(vd.TopLines.First(t => t.Vertical && t.K == 1 && t.Seg == 1).Kind == "Biên"), 1, 0);
        check("Mẫu: bê tông = Σ ô·t − lỗ L1", r.ConcreteVolume, 0.1 * (7.8 * 7.8) - 0.1 * 0.64, 1e-9);
        check("Mẫu: chống thấm sàn vệ sinh hạ cốt = 3,6·4,5", r.Extra.First(e => e.Item.StartsWith("Chống thấm")).Qty, 16.2, 1e-9);

        // ---- thủ công ----
        SlabBarRow Row(string layer, string dir, int d, double sp0) => new() { Layer = layer, Direction = dir, Dia = d, Spacing = sp0, X1 = 0, Y1 = 0, X2 = 7.8, Y2 = 7.8 };
        var man = new ManualSlabPlan { Mode = ManualSlabPlan.ModeManual, ManualBars = { Row("Dưới", "X", 6, 0.20), Row("Dưới", "Y", 6, 0.20) } };
        var rm = man.Run(s);
        check("Thủ công Ø6a200 cho ô 4,2 × 4,5 m → thiếu thép, LỖI", B(!rm.Ok && rm.Messages.Any(m => m.StartsWith("LỖI Ô 1-1 lớp dưới X"))), 1, 0);
        check("Thủ công: thép trong kết quả = đúng các vùng khai báo", rm.Bars.Count, 2, 0);
        check("Thủ công: số thanh = ⌊7,8/0,2⌋ + 1 = 40", rm.Bars[0].Count, 40, 0);
        var man2 = new ManualSlabPlan { Mode = ManualSlabPlan.ModeManual, ManualBars = { Row("Dưới", "X", 10, 0.10), Row("Dưới", "Y", 10, 0.10), Row("Trên", "X", 10, 0.10), Row("Trên", "Y", 10, 0.10) } };
        var rm2 = man2.Run(s);
        foreach (var m in rm2.Messages.Where(x => x.StartsWith("LỖI"))) Console.WriteLine("    [sàn thủ công] " + m);
        check("Thủ công Ø10a100 hai lớp → đạt", B(rm2.Ok), 1, 0);
        check("Thủ công: 4 vùng → 4 loại thanh", rm2.Bars.Count, 4, 0);
        check("Thủ công: không tự thêm thép mũ / gia cường", rm2.Bars.Count(b => !b.Role.StartsWith("Thủ công")), 0, 0);
        check("Thủ công chưa khai báo vùng thép → LỖI", B(new ManualSlabPlan { Mode = ManualSlabPlan.ModeManual }.Run(s).Messages.Any(m => m.StartsWith("LỖI chế độ thủ công"))), 1, 0);

        // ---- dữ liệu sai ----
        var bad = new ManualSlabPlan { SpansX = "", Panels = null, Openings = null, ManualBars = null };
        var rb = bad.Run(s);
        check("Lưới rỗng / danh sách null: không ném, có LỖI", B(rb.Messages.Any(m => m.StartsWith("LỖI"))), 1, 0);
        check("Lưới rỗng: không vẽ", bad.Draw(rb, s).Count, 0, 0);
        check("Nhịp 0,2 m → LỖI lưới", B(new ManualSlabPlan { SpansY = "0,2" }.Run(s).Messages.Any(m => m.StartsWith("LỖI lưới"))), 1, 0);

        // ---- đánh số, bản vẽ ----
        check("Sàn MB: số hiệu thép nhất quán (Validate)", RebarNumbering.Validate(r.Bars).Count, 0, 0);
        check("Sàn MB: mọi thanh có số hiệu", r.Bars.Count(b => b.Mark <= 0), 0, 0);
        var figs = sp.Draw(r, s);
        check("Sàn MB: vẽ mặt bằng + mặt cắt 1-1", figs.Count, 2, 0);
        check("Sàn MB: mặt bằng có nội dung", B(figs[0].W.EntityCount > 100), 1, 0);
        check("Sàn MB thủ công: vẽ được", man2.Draw(rm2, s).Count, 2, 0);
        string dir = Path.Combine(outDir, "TrienKhai");
        Directory.CreateDirectory(dir);
        var info = new SheetComposer.SheetInfo { ProjectName = "Kiểm thử mặt bằng sàn", Title = "MẶT BẰNG BỐ TRÍ THÉP SÀN", SheetNo = "KC-SAN" };
        string f = Path.Combine(dir, "MatBangSan.dxf");
        SheetComposer.Compose(figs, 100, info, "A1").Save(f);
        string txt = File.ReadAllText(f);
        check("DXF mặt bằng sàn ghi được, có chữ HẠ COS", B(txt.Contains(DxfWriter.Encode("HẠ COS"))), 1, 0);
        check("DXF có chữ LỖ L1", B(txt.Contains(DxfWriter.Encode("LỖ L1"))), 1, 0);
    }
}
