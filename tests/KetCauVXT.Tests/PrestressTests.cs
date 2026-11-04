using KetCauVXT.Detailing;
using KetCauVXT.Drawing;

namespace KetCauVXT.Tests;

/// <summary>Kiểm tra mô-đun BÊ TÔNG DỰ ỨNG LỰC căng sau (Detailing/PrestressDesigner.cs): đặc trưng tiết diện chữ nhật / chữ T,
/// hao tổn chùng ứng suất, ma sát, tải cân bằng, ứng suất thớ, khả năng chịu uốn cáp đơn thuần, độ vồng, hao tổn tối thiểu 100 MPa,
/// không lỗi khi 0 tao cáp, bản vẽ DXF. check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class PrestressTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---- tiết diện ----
        var rc = PrestressSection.Of(0.3, 0.7);
        check("DƯL: tiết diện CN 300×700 – A = 0,21 m²", rc.A, 0.21, 1e-9);
        check("DƯL: tiết diện CN – I = bh³/12 = 0,008575 m⁴", rc.I, 0.008575, 1e-9);
        check("DƯL: tiết diện CN – yt = h/2", rc.Yt, 0.35, 1e-9);
        var tee = PrestressSection.Of(0.3, 0.7, 1.2, 0.12);
        check("DƯL: tiết diện T 300×700, cánh 1200×120 – A = 0,318 m²", tee.A, 0.318, 1e-9);
        check("DƯL: tiết diện T – yt = 0,07998/0,318 = 0,251509 m", tee.Yt, 0.0799800 / 0.318, 1e-6);
        check("DƯL: tiết diện T – I = 0,0147027 m⁴ (Steiner)", tee.I, 0.01470269, 1e-5);

        // ---- hao tổn, công thức ----
        check("DƯL: chùng ứng suất (0,22·0,75 − 0,1)·1395 = 90,675 MPa", PrestressDesigner.RelaxationLoss(1395, 1860), 90.675, 1e-6);
        check("DƯL: chùng ứng suất không âm khi σsp nhỏ", PrestressDesigner.RelaxationLoss(500, 1860), 0, 1e-9);
        check("DƯL: góc chuyển hướng θ = 8f·x/L² = 8·0,22·6/144", PrestressDesigner.ThetaParabola(0.22, 12, 6), 0.0733333, 1e-5);
        check("DƯL: ma sát 1395·(1 − e^−(0,2·0,1 + 0,001·6)) = 35,80 MPa", PrestressDesigner.FrictionLoss(1395, 0.2, 0.1, 0.001, 6), 35.80255, 1e-6);
        check("DƯL: tải cân bằng 8·1000·0,22/12² = 12,222 kN/m", PrestressDesigner.BalancedLoad(1000, 0.22, 12), 12.2222222, 1e-6);
        check("DƯL: độ vồng 5·P·f·L²/(48EI) = 0,0033 m", PrestressDesigner.CamberOf(1000, 0.22, 0, 12, 1e6), 0.0033, 1e-6);
        check("DƯL: độ vồng thêm P·e0·L²/(8EI) khi e0 = 0,05", PrestressDesigner.CamberOf(1000, 0, 0.05, 12, 1e6), 1000 * 0.05 * 144 / 8e6, 1e-6);
        var (sT, sB) = PrestressDesigner.Stresses(rc, 1000, 0.2, 300);
        check("DƯL: ứng suất thớ trên P/A − Pe/Wt + M/Wt = 8843,5 kN/m²", sT, 8843.5374, 1e-6);
        check("DƯL: ứng suất thớ dưới P/A + Pe/Wb − M/Wb = 680,27 kN/m²", sB, 680.2721, 1e-6);

        // ---- thiết kế mẫu (mặc định) ----
        var st = new DetailSettings();
        var inp = new PrestressInput();
        DetailResult dr = null;
        bool threw = false;
        try { dr = inp.Run(st); } catch (Exception) { threw = true; }
        var r = dr as PrestressResult;
        check("DƯL: chạy mẫu dầm 12 m không lỗi ngoại lệ", B(!threw && r != null), 1, 0);
        check("DƯL: loại cấu kiện 'Dự ứng lực'", B(r?.Kind == "Dự ứng lực"), 1, 0);
        check("DƯL: P∞ < P0 < σcon·Ap", B(r != null && r.PInf < r.P0 && r.P0 < r.SigmaCon * 1000 * r.Ap), 1, 0);
        check("DƯL: tổng hao tổn = hao tổn 1 + co ngót + từ biến (≥ 100)", r?.LossTotal ?? 0,
            Math.Max(100, (r?.LossFirst ?? 0) + (r?.LossShrink ?? 0) + (r?.LossCreep ?? 0)), 1e-9);
        check("DƯL: co ngót B35 = 0,0002·195000 = 39 MPa", r?.LossShrink ?? 0, 39, 1e-9);
        check("DƯL: khối lượng cáp 8·140 mm²·(12 + 1,2) m·7850 = 116,05 kg", r?.StrandWeight ?? 0, 116.0544, 1e-6);
        check("DƯL: có thép thường dưới, trên, đai (đánh số từ 1)", B(r != null && r.Bars.Count == 3 && r.Bars.Min(b => b.Mark) == 1), 1, 0);
        check("DƯL: có kiểm tra ứng suất, uốn, cắt, võng (≥ 7 mục)", B(r != null && r.Checks.Count >= 7), 1, 0);
        check("DƯL: có thuyết minh (≥ 20 dòng)", B(r != null && r.Calc.Count >= 20), 1, 0);
        check("DƯL: thống kê neo, ống gen", B(r != null && r.Extra.Any(x => x.Item.StartsWith("Neo")) && r.Extra.Any(x => x.Item.StartsWith("Ống gen"))), 1, 0);
        check("DƯL: bê tông = A·L = 0,21·12 = 2,52 m³", r?.ConcreteVolume ?? 0, 2.52, 1e-9);

        // ---- khả năng chịu uốn: chỉ cáp, chữ nhật ----
        var only = new PrestressInput { NumStrands = 4, BotCount = 0, TopCount = 0 };
        var ro = (PrestressResult)only.Run(st);
        // T = 4·140e-6·1550e3 = 868 kN; x = T/(Rb·b) = 0,148376 m; Mu = T·(dp − x/2), dp = 0,35 + 0,22 = 0,57 m
        check("DƯL: Mu chỉ cáp = Rsp·Ap·(dp − x/2) = 430,36 kN.m", ro.MuCap, 430.3648, 1e-5);
        check("DƯL: ξ = x/h0 = 0,148376/0,57", ro.Xi, 0.14837607 / 0.57, 1e-5);

        // ---- hao tổn tối thiểu 100 MPa ----
        var low = new PrestressInput { B = 0.4, H = 0.8, NumStrands = 1, EcMid = 0, EEnd = 0, JackRatio = 0.5, Mu = 0, Wobble = 0, AnchorSet = 0, G2 = 0, Qk = 0 };
        var rl = (PrestressResult)low.Run(st);
        check("DƯL: hao tổn tính được < 100 MPa", B(rl.LossFirst + rl.LossShrink + rl.LossCreep < 100), 1, 0);
        check("DƯL: tổng hao tổn lấy tối thiểu 100 MPa", rl.LossTotal, 100, 1e-9);

        // ---- 0 tao cáp ----
        bool t0 = false; DetailResult r0 = null;
        try { r0 = new PrestressInput { NumStrands = 0 }.Run(st); } catch (Exception) { t0 = true; }
        check("DƯL: 0 tao cáp không ném ngoại lệ", B(!t0 && r0 != null), 1, 0);
        check("DƯL: 0 tao cáp → thông báo LỖI, không đạt", B(r0 != null && r0.Messages.Any(m => m.StartsWith("LỖI")) && !r0.Ok), 1, 0);

        // ---- dải sàn ----
        var slab = new PrestressInput { Name = "SDUL1", MemberType = "Sàn (dải 1 m)", H = 0.22, L = 8, G2 = 2.5, Qk = 3, NumStrands = 4, StrandType = "T12.7", StrandsPerTendon = 4,
            EcMid = 0.055, BotCount = 5, BotDia = 10, TopCount = 5, TopDia = 10, StirrupDia = 0 };
        var rs = (PrestressResult)slab.Run(st);
        check("DƯL sàn: tiết diện 1 m × 220 mm", rs.Section.A, 0.22, 1e-9);
        check("DƯL sàn: không có đai", B(rs.Bars.All(b => b.Role != "Đai")), 1, 0);

        // ---- bản vẽ ----
        var figs = inp.Draw(r, st);
        figs.AddRange(slab.Draw(rs, st));
        check("DƯL: bản vẽ mặt đứng cáp + mặt cắt (dầm + sàn = 4 hình)", figs.Count(f => f.W.EntityCount > 0), 4, 0);
        string dir = Path.Combine(outDir, "TrienKhai");
        Directory.CreateDirectory(dir);
        var wsh = SheetComposer.Compose(figs, 50, new SheetComposer.SheetInfo { ProjectName = "Mẫu dự ứng lực", Title = "DẦM / SÀN DỰ ỨNG LỰC", SheetNo = "KC-DUL" }, "A1");
        string dxf = Path.Combine(dir, "DuUngLuc.dxf");
        wsh.Save(dxf);
        string txt = File.ReadAllText(dxf);
        check("DƯL: DXF tồn tại, không rỗng", B(new FileInfo(dxf).Length > 2000), 1, 0);
        check("DƯL: DXF có layer THEP (cáp) và BE_TONG", B(txt.Contains("THEP") && txt.Contains("BE_TONG")), 1, 0);
    }
}
