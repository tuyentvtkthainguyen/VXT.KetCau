using KetCauVXT.Design.Concrete;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Lanh tô cửa – ô văng cửa (Core/Detailing/LintelDesigner.cs): tải tường (hiệu ứng vòm), sơ đồ dầm đơn giản,
/// mô men ngàm ô văng, xoắn lanh tô, chống lật, khối lượng, thép, bản vẽ. check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class LintelTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var s = new DetailSettings();

        // ---- mẫu: lanh tô + ô văng cửa 1,2 m, tường 220 ----
        var lt = ManualLintel.Sample();
        var r = lt.Run(s);
        foreach (var m in r.Messages.Where(x => x.StartsWith("LỖI"))) Console.WriteLine("    [lanh tô mẫu] " + m);
        check("Lanh tô mẫu: đạt mọi kiểm tra", B(r.Ok), 1, 0);
        check("Lanh tô mẫu: Kind", B(r.Kind == "Lanh tô – ô văng"), 1, 0);

        // ---- chiều cao, gối tự chọn ----
        check("h tự chọn Lo = 1,2 m: max(0,12; 0,15) = 150 mm", ManualLintel.AutoHeight(1.2), 0.15, 1e-12);
        check("h tự chọn Lo = 2,4 m: 0,24 → làm tròn lên 0,25", ManualLintel.AutoHeight(2.4), 0.25, 1e-12);
        check("Mẫu: h = 150, gối = max(200; h) = 200 mm", lt.HlUsed * 10 + lt.LaUsed, 1.5 + 0.2, 1e-12);
        var lt24 = new ManualLintel { Type = ManualLintel.TypeLintel, Lo = 2.4 }; lt24.Run(s);
        check("Lo = 2,4 m: gối = max(200; h = 250) = 250 mm", lt24.LaUsed, 0.25, 1e-12);

        // ---- hiệu ứng vòm ----
        check("Tường cao 1,0 ≥ Lo/3 = 0,4, không có sàn → hw = Lo/3", ManualLintel.EffectiveWallHeight(1.2, 1.0, false), 0.4, 1e-12);
        check("Có sàn tựa trong phạm vi → lấy toàn bộ hw = 1,0", ManualLintel.EffectiveWallHeight(1.2, 1.0, true), 1.0, 1e-12);
        check("Tường thấp 0,3 < Lo/3 → hw = 0,3", ManualLintel.EffectiveWallHeight(1.2, 0.3, false), 0.3, 1e-12);
        double gw = 1.1 * 18 * 0.22 * 0.4 + 1.3 * 2 * 0.015 * 18 * 0.4;
        check("Tải tường gw = 1,1·γ·b·hw + 1,3·2·δ·18·hw", lt.WallLoad, gw, 1e-12);

        // ---- ô văng: mô men ngàm ----
        double lc = 0.6, tr = 0.08, tt = 0.06;
        double mSelf = 1.1 * 25 * lc * lc * (tr / 6 + tt / 3);
        double mFin = 1.3 * 0.5 * lc * lc / 2;
        double gUp = 1.1 * 25 * 0.06 * 0.10;
        double mLive = Math.Max(1.3 * 0.75 * lc * lc / 2, 1.3 * 1.0 * lc);
        double mRoot = mSelf + mFin + gUp * (lc - 0.03) + mLive;
        check("Ô văng: M ngàm = bản thân + hoàn thiện + gờ + max(p; P)", lt.MRoot, mRoot, 1e-12);
        check("Ô văng: bản thân dày thay đổi = 1,1·25·Lc²·(hr/6 + ht/3)", mSelf, 1.1 * 25 * (0.06 * lc * lc / 2 + (0.08 - 0.06) * lc * lc / 6), 1e-12);
        double vRoot = 1.1 * 25 * (tr + tt) / 2 * lc + 1.3 * 0.5 * lc + gUp + Math.Max(1.3 * 0.75 * lc, 1.3 * 1.0);
        check("Ô văng: Q ngàm", lt.VRoot, vRoot, 1e-12);
        var (mShort, _) = ManualLintel.CanopyRoot(0.3, 0.08, 0.06, 0.5, 0.75, 1.0, false, 0, 0);
        check("Ô văng 0,3 m: tải tập trung P chi phối (1,3·P·Lc > 1,3·p·Lc²/2)", mShort, 1.1 * 25 * 0.09 * (0.08 / 6 + 0.06 / 3) + 1.3 * 0.5 * 0.045 + 1.3 * 1.0 * 0.3, 1e-12);
        var conc = Material.Concrete(s.Concrete);
        var cb240 = Material.Rebar(s.RebarSmall);
        double asC = RcSection.DesignRect(mRoot, 1.0, 0.08 - 0.02 - 0.004, 0.02, conc, cb240, 0.1).As;
        check("Ô văng: As yêu cầu = DesignRect(M, 1 m, h0)", lt.AsCanopy, asC, 1e-9);

        // ---- lanh tô: tải, M, Q ----
        double self = 1.1 * 25 * 0.22 * 0.15;
        double canopyLoad = 1.1 * 25 * (tr + tt) / 2 * lc + 1.3 * 0.5 * lc + gUp + 1.3 * 0.75 * lc;
        double q = self + gw + canopyLoad;
        check("Lanh tô: tải ô văng truyền xuống", lt.CanopyLoad, canopyLoad, 1e-12);
        check("Lanh tô: q = bản thân + tường + ô văng", lt.Q, q, 1e-12);
        check("Lanh tô: l0 = min(1,05·Lo; Lo + a) = 1,26 m", lt.L0, 1.26, 1e-12);
        check("Lanh tô: M = q·l0²/8", lt.M, q * 1.26 * 1.26 / 8, 1e-12);
        check("Lanh tô: Q = q·Lo/2", lt.V, q * 1.2 / 2, 1e-12);
        check("Lanh tô: xoắn T = mt·Lo/2", lt.Torsion, mRoot * 1.2 / 2, 1e-12);
        check("Lanh tô: thép dưới ≥ 2Ø10", B(r.Bars.First(b => b.Role == "Lanh tô dưới").Count >= 2 && r.Bars.First(b => b.Role == "Lanh tô dưới").Dia >= 10), 1, 0);

        // ---- chống lật khi lanh tô chỉ gác lên tường ----
        var lf = new ManualLintel { Type = ManualLintel.TypeCombined, AnchoredEnds = false, Lc = 0.6, Hm = 0.6 };
        var rf = lf.Run(s);
        double gChar = 25 * 0.22 * 0.15 + (18 * 0.22 + 2 * 0.015 * 18) * 0.6;
        double k = 0.9 * gChar * 0.22 / 2 / mRoot;
        check("Chống lật: K = 0,9·(25·b·h + gtường·Hm)·b/2 / M ngàm", lf.OverturnK, k, 1e-12);
        check("Chống lật: K < 1,3 → LỖI cần neo ô văng", B(!rf.Ok && rf.Messages.Any(m => m.Contains("cần neo ô văng"))), 1, 0);

        // ---- khối lượng ----
        double ltot = 1.2 + 2 * 0.2;
        double vol = 0.22 * 0.15 * ltot + (tr + tt) / 2 * lc * ltot + 0.06 * 0.10 * ltot;
        check("Bê tông = lanh tô + ô văng + gờ", r.ConcreteVolume, vol, 1e-12);
        var rl = new ManualLintel { Type = ManualLintel.TypeLintel }.Run(s);
        check("Chỉ lanh tô: bê tông = b·h·(Lo + 2a)", rl.ConcreteVolume, 0.22 * 0.15 * ltot, 1e-12);
        check("Chỉ lanh tô: không có thép ô văng", B(!rl.Bars.Any(b => b.Role.StartsWith("Ô văng")) && rl.Ok), 1, 0);
        check("Mẫu: có chống thấm mặt ô văng trong khối lượng", B(r.Extra.Any(e => e.Item.StartsWith("Trát / chống thấm"))), 1, 0);

        // ---- thép ----
        foreach (var role in new[] { "Lanh tô dưới", "Lanh tô trên", "Đai lanh tô", "Ô văng chịu lực", "Ô văng phân bố", "Gờ ô văng" })
            check("Có thép: " + role, B(r.Bars.Any(b => b.Role == role)), 1, 0);
        check("Số hiệu thép duy nhất (Validate)", RebarNumbering.Validate(r.Bars).Count, 0, 0);
        var mainBar = r.Bars.First(b => b.Role == "Ô văng chịu lực");
        check("Thép ô văng: bẻ móc mút + neo bẻ xuống lanh tô (hình U)", B(mainBar.Shape == "U" && mainBar.IsTop), 1, 0);
        var rUser = new ManualLintel { Type = ManualLintel.TypeCombined, Lc = 1.2, CanD = 6, CanS = 0.25, Tr = 0.10, Tt = 0.07 }.Run(s);
        check("Ô văng 1,2 m với Ø6a250 người dùng chọn → không đạt", B(rUser.Checks.First(c => c.Name.StartsWith("Ô văng chịu uốn")).Ok), 0, 0);
        var rCon = new ManualLintel { Type = ManualLintel.TypeCanopy }.Run(s);
        check("Ô văng console: không có lanh tô, có ghi chú xoắn dầm đỡ", B(!rCon.Bars.Any(b => b.Role.StartsWith("Lanh tô")) && rCon.Messages.Any(m => m.Contains("dầm đỡ"))), 1, 0);
        check("Gối 150 mm < 200 → không đạt", B(new ManualLintel { La = 0.15 }.Run(s).Ok), 0, 0);

        // ---- dữ liệu sai không ném lỗi ----
        check("Lo = 0 → LỖI, không ném", B(new ManualLintel { Lo = 0 }.Run(s).Messages.Any(m => m.StartsWith("LỖI"))), 1, 0);
        check("Lc = NaN → LỖI, không ném", B(new ManualLintel { Lc = double.NaN }.Run(s).Messages.Any(m => m.StartsWith("LỖI"))), 1, 0);
        check("Settings null không ném", B(new ManualLintel().Run(null).Ok), 1, 0);

        // ---- bản vẽ ----
        var figs = lt.Draw(r, s);
        check("Vẽ: mặt đứng lanh tô + mặt cắt 1-1", figs.Count, 2, 0);
        check("Vẽ: có nội dung", B(figs.All(f => f.W.EntityCount > 20)), 1, 0);
        check("Ô văng console: vẽ mặt cắt", new ManualLintel { Type = ManualLintel.TypeCanopy }.Draw(rCon, s).Count, 1, 0);
        string dir = Path.Combine(outDir, "TrienKhai");
        Directory.CreateDirectory(dir);
        string f = Path.Combine(dir, "LanhTo.dxf");
        SheetComposer.Compose(figs, 100, new SheetComposer.SheetInfo { ProjectName = "Kiểm thử lanh tô", Title = "LANH TÔ – Ô VĂNG CỬA", SheetNo = "KC-LT" }, "A1").Save(f);
        check("Ghi DXF lanh tô", B(File.Exists(f) && new FileInfo(f).Length > 3000), 1, 0);
        check("DXF có chữ tiếng Việt mã hoá", B(File.ReadAllText(f).Contains(DxfWriter.Encode("LANH TÔ"))), 1, 0);
    }
}
