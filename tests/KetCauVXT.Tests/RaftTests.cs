using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm tra MÓNG BÈ (Detailing/RaftDesigner.cs) – phương pháp móng cứng: áp lực đáy móng (đối xứng, lệch tâm) so với công thức đóng,
/// mô men dải tĩnh định so với tính tay, chọc thủng cột giữa / cột biên, số thanh lưới thép, bản vẽ DXF.
/// check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class RaftTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var s = new DetailSettings();

        // ---- 1. bè đối xứng: p = ΣNtc/A + γ·Df ----
        var smp = RaftInput.Sample();
        var r = smp.Design(s);
        double sumNtc = 9200 / 1.15;
        check("Bè mẫu: ΣNtc = ΣN/1,15", r.SumNtc, sumNtc, 1e-9);
        check("Bè mẫu: ptb = ΣNtc/A + γtb·Df", r.Ptb, sumNtc / 81 + 20 * 1.5, 1e-9);
        check("Bè mẫu: đối xứng → pmax = pmin = ptb", r.Pmax - r.Pmin, 0, 1e-9);
        check("Bè mẫu: 3 dải X + 3 dải Y", r.Strips.Count, 6, 0);
        check("Bè mẫu: 9 kiểm tra chọc thủng", r.Punch.Count, 9, 0);
        check("Bè mẫu: dải biên rộng 0,5 + 2 = 2,5 m", r.Strips.First(x => x.AlongX).Width, 2.5, 1e-9);
        check("Bè mẫu: tổng tải cột F·ΣN = áp lực q'·B·L (cân bằng dải)",
            B(r.Strips.All(x => Math.Abs(x.F * x.SumN - x.QAdj * x.Width * x.Length) < 1e-6)), 1, 0);
        check("Bè mẫu: thể tích bê tông 9 × 9 × 0,5", r.ConcreteVolume, 40.5, 1e-9);
        check("Bè mẫu: ván khuôn chu vi × dày", r.FormworkArea, 36 * 0.5, 1e-9);
        check("Bè mẫu: bê tông lót (Lx+0,2)(Ly+0,2)·0,1", r.LeanVolume, 9.2 * 9.2 * 0.1, 1e-9);
        check("Bè mẫu: số hiệu đánh từ 1, lớp dưới trước lớp trên", B(r.Bars.Min(b => b.Mark) == 1
            && r.Bars.Where(b => b.Role.StartsWith("Lớp dưới")).Max(b => b.Mark) < r.Bars.Where(b => b.Role.StartsWith("Lớp trên")).Min(b => b.Mark)), 1, 0);
        check("Bè mẫu: có 4 lớp lưới + con kê", B(new[] { "Lớp dưới X", "Lớp dưới Y", "Lớp trên X", "Lớp trên Y", "Con kê" }.All(x => r.Bars.Any(b => b.Role == x))), 1, 0);
        check("Bè mẫu: có thuyết minh và kiểm tra", B(r.Calc.Count > 10 && r.Checks.Count > 10), 1, 0);

        // ---- 2. lệch tâm: cột giữa có My; cột lệch tâm 1 m ----
        var e1 = new RaftInput { Lx = 6, Ly = 6, T = 0.6, Df = 1.5, R = 200 };
        e1.Columns.Add(new RaftColumn { X = 3, Y = 3, N = 1150, Ntc = 1000, My = 230 });
        var re1 = e1.Design(s);
        check("Lệch tâm: My,tc = My·Ntc/N = 200", re1.MyTot, 200, 1e-9);
        check("Lệch tâm: pmax = 1000/36 + 30 + 200·3/108", re1.Pmax, 1000.0 / 36 + 30 + 200.0 * 3 / 108, 1e-9);
        check("Lệch tâm: pmin = 1000/36 + 30 − 200·3/108", re1.Pmin, 1000.0 / 36 + 30 - 200.0 * 3 / 108, 1e-9);
        var e2 = new RaftInput { Lx = 6, Ly = 6, T = 0.6, Df = 1.5, R = 200 };
        e2.Columns.Add(new RaftColumn { X = 4, Y = 3, N = 1150, Ntc = 1000 });
        var re2 = e2.Design(s);
        check("Cột lệch 1 m: ex = 1 m", re2.Ex, 1, 1e-9);
        check("Cột lệch 1 m: pmax = 1000/36 + 30 + 1000·3/108", re2.Pmax, 1000.0 / 36 + 30 + 3000.0 / 108, 1e-9);
        check("Cột lệch 1 m: pmin = 30 (e = L/6)", re2.Pmin, 30, 1e-9);

        // ---- 3. dải 2 cột: tính tay ----
        var st2 = new RaftInput { Lx = 6, Ly = 1, T = 0.5, Df = 1.0, R = 300, Chairs = false };
        st2.Columns.Add(new RaftColumn { X = 1, Y = 0.5, N = 300 });
        st2.Columns.Add(new RaftColumn { X = 5, Y = 0.5, N = 300 });
        var rs = st2.Design(s);
        var sx = rs.Strips.Single(x => x.AlongX);
        check("Dải 2 cột: q = ΣN/A = 100 kN/m², F = 1", sx.QAdj, 100, 1e-9);
        check("Dải 2 cột: M căng dưới tại chân cột = q·1²/2 = 50", sx.MBot, 50, 1e-9);
        check("Dải 2 cột: vị trí M căng dưới x = 1 m", sx.XBot, 1, 1e-9);
        check("Dải 2 cột: M căng trên giữa nhịp = 100·3²/2 − 300·2 = −150", sx.MTop, -150, 1e-9);
        check("Dải 2 cột: M(3) = −150", sx.MomentAt(3), -150, 1e-9);
        check("Dải 2 cột: M(6) = 0 (cân bằng)", sx.MomentAt(6), 0, 1e-6);
        check("Dải 2 cột: 2 dải Y rộng 3 m", rs.Strips.Count(x => !x.AlongX && Math.Abs(x.Width - 3) < 1e-9), 2, 0);
        check("Dải Y: M = 300·0,5²/2 = 37,5 → 12,5 kN.m/m", rs.MBotY, 12.5, 1e-9);
        check("Dải Y: không có mô men căng trên", rs.MTopY, 0, 1e-9);
        check("Dải 2 cột: Mthiết kế dưới X = 50 kN.m/m", rs.MBotX, 50, 1e-9);
        check("Dải 2 cột: Mthiết kế trên X = 150 kN.m/m", rs.MTopX, 150, 1e-9);

        // ---- 4. chọc thủng ----
        var pc = new RaftInput { Lx = 6, Ly = 6, T = 0.5, Df = 1.5, R = 200, BotDiaX = 16, BotSX = 0.15, BotDiaY = 16, BotSY = 0.15 };
        pc.Columns.Add(new RaftColumn { X = 3, Y = 3, B = 0.4, H = 0.4, N = 1150 });
        var rp = pc.Design(s);
        double h0 = 0.5 - 0.05 - 0.016, rbt = 1.05e3;
        double ab = Math.Pow(0.4 + 2 * h0, 2), F = 1150 - 1150.0 / 36 * ab, u = 4 * (0.4 + h0);
        check("Chọc thủng cột giữa: h0 = T − c − Ø = 434 mm", rp.Punch[0].H0, h0, 1e-9);
        check("Chọc thủng cột giữa: u = 4(bc + h0)", rp.Punch[0].U, u, 1e-9);
        check("Chọc thủng cột giữa: F = N − p·(bc + 2h0)²", rp.Punch[0].F, F, 1e-9);
        check("Chọc thủng cột giữa: Fb,ult = Rbt·u·h0", rp.Punch[0].Fu, rbt * u * h0, 1e-9);
        check("Chọc thủng cột giữa: vị trí 'giữa'", B(rp.Punch[0].Position == "giữa"), 1, 0);
        var pe = new RaftInput { Lx = 6, Ly = 6, T = 0.5, Df = 1.5, R = 400, BotDiaX = 16, BotSX = 0.15, BotDiaY = 16, BotSY = 0.15 };
        pe.Columns.Add(new RaftColumn { X = 0.2, Y = 3, B = 0.4, H = 0.4, N = 1150 });
        var rpe = pe.Design(s);
        double ue = 2 * (0.2 + 0.2 + h0 / 2) + (0.4 + h0);
        double abe = (0.2 + 0.2 + h0) * (0.4 + 2 * h0);
        double pE = 1150.0 / 36 + 1150 * (0.2 - 3) * (0.2 - 3) / 108;
        check("Chọc thủng cột biên: chu vi cắt cụt 3 cạnh", rpe.Punch[0].U, ue, 1e-9);
        check("Chọc thủng cột biên: đáy tháp cắt cụt", rpe.Punch[0].Ab, abe, 1e-9);
        check("Chọc thủng cột biên: p tại cột = ΣN/A + My·x/Iy", rpe.Punch[0].P, pE, 1e-9);
        check("Chọc thủng cột biên: F = N − p·Ab", rpe.Punch[0].F, 1150 - pE * abe, 1e-9);
        check("Chọc thủng cột biên: vị trí 'biên'", B(rpe.Punch[0].Position == "biên"), 1, 0);
        var thin = new RaftInput { Lx = 6, Ly = 6, T = 0.25, Df = 1.5, R = 400 };
        thin.Columns.Add(new RaftColumn { X = 3, Y = 3, B = 0.3, H = 0.3, N = 2500 });
        var rt = thin.Design(s);
        check("Bè mỏng chịu tải lớn: báo LỖI chọc thủng, không đạt", B(!rt.Ok && rt.Messages.Any(m => m.StartsWith("LỖI chọc thủng"))), 1, 0);

        // ---- 5. thép người dùng chọn ----
        var ub = RaftInput.Sample(); ub.BotDiaX = 16; ub.BotSX = 0.15;
        var ru = ub.Design(s);
        var bx = ru.Bars.First(b => b.Role == "Lớp dưới X");
        check("Thép dưới X Ø16a150: số thanh ⌊(9 − 0,1)/0,15⌋ + 1 = 60", bx.Count, 60, 0);
        check("Thép dưới X: dài 9 − 2c + 2(T − 2c) = 9,70 m", bx.Length, 9.7, 1e-9);
        check("Thép dưới X: hình U, Ø16, bước 150", B(bx.Shape == "U" && bx.Dia == 16 && Math.Abs(bx.Spacing - 0.15) < 1e-9), 1, 0);
        check("Thép dài 9,7 m < 11,7 m: không báo nối", B(!ru.Messages.Any(m => m.Contains("nối chồng"))), 1, 0);
        var big = RaftInput.Sample(); big.Lx = 14; foreach (var col in big.Columns) col.X *= 1.5;
        var rb = big.Design(s);
        check("Bè dài 14 m: báo nối chồng thép", B(rb.Messages.Any(m => m.Contains("nối chồng"))), 1, 0);
        var weak = RaftInput.Sample(); weak.TopDiaX = 12; weak.TopSX = 0.30;
        var rw = weak.Design(s);
        check("Thép trên quá thưa: kiểm tra không đạt / LỖI", B(rw.Messages.Any(m => m.StartsWith("LỖI Thép trên X")) || rw.Checks.Any(c => c.Name.StartsWith("Thép trên X") && !c.Ok)), 1, 0);

        // ---- 6. không có cột ----
        var none = new RaftInput();
        DetailResult rn = null;
        bool threw = false;
        try { rn = none.Run(s); } catch { threw = true; }
        check("Không có cột: không ném ngoại lệ, báo LỖI", B(!threw && rn != null && rn.Messages.Any(m => m.StartsWith("LỖI")) && !rn.Ok), 1, 0);
        check("Không có cột: Draw không lỗi", B(none.Draw(rn, s).Count >= 0), 1, 0);

        // ---- 7. bản vẽ ----
        var figs = smp.Draw(r, s);
        check("Bản vẽ móng bè: mặt bằng + mặt cắt", figs.Count, 2, 0);
        check("Bản vẽ móng bè: có đối tượng", B(figs.All(f => f.W.EntityCount > 20)), 1, 0);
        string dir = Path.Combine(outDir, "TrienKhai");
        Directory.CreateDirectory(dir);
        var wsh = SheetComposer.Compose(figs, 100, new SheetComposer.SheetInfo { ProjectName = "Mẫu móng bè", Title = "MÓNG BÈ MB1", SheetNo = "KC-M1" }, "A1");
        string dxf = Path.Combine(dir, "MongBe.dxf");
        wsh.Save(dxf);
        string txt = File.ReadAllText(dxf);
        check("Bản vẽ móng bè DXF: tồn tại, không rỗng", B(File.Exists(dxf) && new FileInfo(dxf).Length > 2000), 1, 0);
        check("Bản vẽ móng bè DXF: có layer THEP, BE_TONG, KICH_THUOC", B(new[] { "THEP", "BE_TONG", "KICH_THUOC" }.All(txt.Contains)), 1, 0);
    }
}
