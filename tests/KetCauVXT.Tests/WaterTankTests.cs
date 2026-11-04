using KetCauVXT.Design.Concrete;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;

namespace KetCauVXT.Tests;

/// <summary>Kiểm tra BỂ NƯỚC BTCT (Detailing/WaterTankDesigner.cs): áp lực thuỷ tĩnh, Ka, mô men dải đứng (công xôn, ngàm–khớp),
/// dải ngang ngàm 2 đầu, chia tải Grashof, bề rộng vết nứt TCVN 5574:2018 8.2.2.2 (ví dụ tính tay), tự tăng thép khi nứt vượt,
/// thể tích bê tông, chống đẩy nổi, dữ liệu sai không ném lỗi, bản vẽ DXF. check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class WaterTankTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var s = new DetailSettings();

        // ---- công thức ----
        double ka = WaterTankFormulas.Ka(20);
        double t35 = Math.Tan(35 * Math.PI / 180);
        check("Bể nước: Ka(φ = 20°) = tan²35° = 0,4903", ka, t35 * t35, 1e-9);
        check("Bể nước: Ka(φ = 30°) = 1/3", WaterTankFormulas.Ka(30), 1.0 / 3, 1e-9);
        var (mbP, msP) = WaterTankFormulas.Vertical(0, 16, 1.8, true);
        check("Bể nước: ngàm–khớp tải tam giác Mchân = p·H²/15 = 16·1,8²/15 = 3,456", mbP, 3.456, 1e-9);
        check("Bể nước: ngàm–khớp tải tam giác Mnhịp = 0,0298·p·H² = 1,5448", msP, 0.0298 * 16 * 3.24, 1e-9);
        var (mbC, msC) = WaterTankFormulas.Vertical(5, 10, 2, false);
        check("Bể nước: công xôn Mchân = u·H²/2 + t·H²/6 = 10 + 6,667", mbC, 10 + 40.0 / 6, 1e-9);
        check("Bể nước: công xôn Mnhịp = 0", msC, 0, 1e-12);
        var (mbU, _) = WaterTankFormulas.Vertical(8, 0, 2, true);
        check("Bể nước: ngàm–khớp tải đều Mchân = u·H²/8 = 4", mbU, 4, 1e-9);
        var (mhs, mhp) = WaterTankFormulas.Horizontal(12, 3);
        check("Bể nước: dải ngang ngàm 2 đầu Mgối = pL²/12 = 9", mhs, 9, 1e-9);
        check("Bể nước: dải ngang ngàm 2 đầu Mnhịp = pL²/24 = 4,5", mhp, 4.5, 1e-9);
        check("Bể nước: L/H ≥ 2 → một phương đứng αv = 1", WaterTankFormulas.ShareVertical(4, 2, true), 1, 1e-12);
        check("Bể nước: H/L ≥ 2 → một phương ngang αv = 0", WaterTankFormulas.ShareVertical(1, 2, false), 0, 1e-12);
        check("Bể nước: L = H có nắp αv = 1/(1 + 384/185)", WaterTankFormulas.ShareVertical(2, 2, true), 1 / (1 + 384.0 / 185), 1e-9);
        check("Bể nước: L = H hở αv = 1/(1 + 48)", WaterTankFormulas.ShareVertical(2, 2, false), 1 / 49.0, 1e-9);
        check("Bể nước: áp lực đất không nước ngầm p = Ka·(q + γz)", WaterTankFormulas.SoilPressure(1.92, ka, 18, 10, 10, 99), ka * (10 + 18 * 1.92), 1e-9);
        check("Bể nước: áp lực đất có nước ngầm p = Ka·(q + γ·hn + γ'(z − hn)) + γw(z − hn)",
            WaterTankFormulas.SoilPressure(3, ka, 18, 10, 10, 1), ka * (10 + 18 + 8 * 2) + 20, 1e-9);
        check("Bể nước: chia tải bản kê Grashof α = L2⁴/(L1⁴ + L2⁴)", WaterTankFormulas.ShareShort(2, 3), 81.0 / (16 + 81), 1e-9);
        check("Bể nước: bản kê L2/L1 > 2 → α = 1", WaterTankFormulas.ShareShort(2, 4.5), 1, 1e-12);

        // ---- nứt: ví dụ tính tay M = 20 kN.m/m, h = 200, a = 30, Ø12a150 ----
        // As = 113,1/0,15 = 754 mm²/m; h0 = 164; z = 0,9·164 = 147,6; σs = 20e6/(147,6·754) = 179,7 MPa;
        // ls = 0,5·(1000·100)/754·12 = 795,8 → giới hạn min(40·12; 400) = 400 mm; acrc = 1,4·0,5·179,7/200000·400 = 0,2516 mm
        double As = Math.PI * 0.012 * 0.012 / 4 / 0.15, h0 = 0.2 - 0.03 - 0.006;
        double sig = 20 / (0.9 * h0 * As);
        double expA = 1.4 * 0.5 * sig / 2e8 * 0.40;
        var cw = WaterTankFormulas.CrackWidth(20, 0, 0.2, 0.03, 12, 0.15, 2e8);
        check("Bể nước: nứt – σs = M/(0,9h0·As) ≈ 179,7 MPa", cw.SigmaS / 1000, sig / 1000, 1e-9);
        check("Bể nước: nứt – ls bị giới hạn 400 mm", cw.Ls, 0.40, 1e-12);
        check("Bể nước: nứt – acrc = 0,2516 mm (tính tay)", cw.Acrc * 1000, expA * 1000, 1e-9);
        check("Bể nước: nứt – giá trị tính tay ≈ 0,2516 mm", cw.Acrc * 1000, 0.2516, 2e-3);
        var cw2 = WaterTankFormulas.CrackWidth(5, 0, 0.2, 0.03, 10, 0.10, 2e8);
        check("Bể nước: nứt – ls ≥ max(10ds; 100 mm)", B(cw2.Ls >= 0.10 - 1e-12), 1, 0);

        // ---- bể ngầm có nắp ----
        var ug = new WaterTankInput { Name = "BN1", Position = WaterTankInput.PosUnderground, Lx = 3, Ly = 2, H = 1.8, Hw = 1.6, Tw = 0.2, Tb = 0.25, Tc = 0.12, Hgw = 99 };
        var r = (WaterTankResult)ug.Run(s);
        check("Bể ngầm: chạy không lỗi", B(!r.Messages.Any(m => m.StartsWith("LỖI"))), 1, 0);
        check("Bể ngầm: áp lực nước đáy γw·Hw = 16 kN/m²", r.PWater, 16, 1e-9);
        check("Bể ngầm: Ka = 0,4903", r.Ka, ka, 1e-9);
        check("Bể ngầm: áp lực đất chân thành p = Ka·(10 + 18·1,92)", r.PSoilBase, ka * (10 + 18 * 1.92), 1e-9);
        check("Bể ngầm: mọi mặt thép acrc ≤ 0,2 mm (tự tăng thép)", B(r.Faces.Where(f => !f.Name.StartsWith("Bản nắp")).All(f => f.Crack <= 0.0002 + 1e-12)), 1, 0);
        check("Bể ngầm: mọi mặt thép As ≥ yêu cầu và ≥ Ø10a200", B(r.Faces.All(f => f.AsProv >= f.AsReq - 1e-12 && f.AsProv >= Math.PI * 0.01 * 0.01 / 4 / 0.2 * (f.Name.StartsWith("Bản nắp") ? 0.64 : 1) - 1e-12)), 1, 0);
        double vol = 3.4 * 2.4 * 0.25 + 3.4 * 2.4 * 0.12 + (3.4 * 2.4 - 6) * 1.8;
        check("Bể ngầm: thể tích bê tông = 3,4·2,4·(0,25 + 0,12) + (8,16 − 6)·1,8 = 6,9072 m³", r.ConcreteVolume, vol, 1e-9);
        check("Bể ngầm: thể tích = công thức đóng", WaterTankFormulas.ConcreteVolume(3, 2, 1.8, 0.2, 0.25, 0.12, 0), 6.9072, 1e-9);
        check("Bể ngầm: không có nước ngầm → không kiểm tra đẩy nổi (U = 0)", r.BuoyU, 0, 1e-12);
        check("Bể ngầm: có khối lượng thép, ván khuôn, bê tông lót", B(r.SteelWeight > 0 && r.FormworkArea > 0 && r.LeanVolume > 0), 1, 0);
        check("Bể ngầm: có băng cản nước, chống thấm", B(r.Extra.Any(e => e.Item.Contains("waterstop")) && r.Extra.Any(e => e.Item.StartsWith("Chống thấm mặt trong"))), 1, 0);
        double fw = 2 * ((3.0 + 2.0) * 1.8) + 2 * (3.4 + 2.4) * (1.8 + 0.12) + 2 * (3.4 + 2.4) * 0.25 + 3 * 2;
        check("Bể ngầm: ván khuôn = thành trong + thành ngoài + mép đáy + đáy nắp", r.FormworkArea, fw, 1e-9);
        check("Bể ngầm: số hiệu thép từ 1, liên tục", B(r.Bars.Min(b => b.Mark) == 1 && r.Bars.Select(b => b.Mark).Distinct().Count() == r.Bars.Max(b => b.Mark)), 1, 0);
        check("Bể ngầm: số hiệu hợp lệ (một số hiệu – một bộ Ø, dài, dạng)", RebarNumbering.Validate(r.Bars).Count, 0, 0);
        check("Bể ngầm: có thép đứng 2 mặt, ngang, góc, đáy 2 lớp, nắp",
            B(new[] { "Thép đứng thành – mặt trong", "Thép đứng thành – mặt ngoài", "Thép ngang thành", "Thép góc", "Lớp dưới bản đáy", "Lớp trên bản đáy", "Lớp dưới bản nắp", "Mũ bản nắp" }
                .All(p => r.Bars.Any(b => b.Role.StartsWith(p)))), 1, 0);
        check("Bể ngầm: thuyết minh có công thức Ka, vết nứt", B(r.Calc.Any(c => c.Contains("Ka = tan²")) && r.Calc.Any(c => c.Contains("acrc ="))), 1, 0);

        // ---- đẩy nổi: nước ngầm ở mặt đất ----
        var wet = new WaterTankInput { Name = "BN2", Position = WaterTankInput.PosUnderground, Lx = 3, Ly = 2, H = 1.8, Hw = 1.6, Tw = 0.2, Tb = 0.25, Tc = 0.12, Hgw = 0.5 };
        var rw = (WaterTankResult)wet.Run(s);
        check("Đẩy nổi: U = γw·(Hout − hn)·A = 10·1,67·8,16 = 136,27 kN", rw.BuoyU, 10 * (2.17 - 0.5) * 8.16, 1e-9);
        check("Đẩy nổi: G = 25·V = 172,68 kN", rw.BuoyG, 25 * 6.9072, 1e-9);
        check("Đẩy nổi: G ≥ 1,1·U → đạt", B(rw.Checks.First(c => c.Name.StartsWith("Chống đẩy nổi")).Ok), 1, 0);
        wet.Hgw = 0;
        var rf = (WaterTankResult)wet.Run(s);
        check("Đẩy nổi: nước ngầm tại mặt đất → G = 172,7 < 1,1·U = 194,8 → LỖI", B(rf.Messages.Any(m => m.StartsWith("LỖI") && m.Contains("đẩy nổi")) && !rf.Ok), 1, 0);
        check("Đẩy nổi: hệ số yêu cầu 1,1·U", rf.Checks.First(c => c.Name.StartsWith("Chống đẩy nổi")).Demand, 1.1 * 10 * 2.17 * 8.16, 1e-9);

        // ---- bể trên mái hở ----
        var roof = new WaterTankInput { Name = "BM1", Position = WaterTankInput.PosRoof, Lx = 3, Ly = 2, H = 1.8, Hw = 1.6, Tw = 0.2, Tb = 0.2, Tc = 0, Hgw = 99 };
        var rr = (WaterTankResult)roof.Run(s);
        double avL = Math.Pow(3.2, 4) / (Math.Pow(3.2, 4) + 48 * Math.Pow(1.8, 4));
        check("Bể mái hở: Mchân thành = αv·1,1·γw·Hw³/6 (thành dài)", rr.WallVertIn.M, avL * 1.1 * 10 * Math.Pow(1.6, 3) / 6, 1e-9);
        check("Bể mái hở: không áp lực đất, không đẩy nổi", B(rr.PSoilBase == 0 && rr.BuoyU == 0 && rr.CoverBot == null), 1, 0);
        check("Bể mái hở: không có thép nắp", B(!rr.Bars.Any(b => b.Role.Contains("nắp"))), 1, 0);
        check("Bể mái hở: acrc ≤ 0,2 mm", B(rr.Faces.All(f => f.Crack <= 0.0002 + 1e-12)), 1, 0);
        double qB = 1.1 * 10 * 1.6 + 1.1 * 25 * 0.2, aB = WaterTankFormulas.ShareShort(2.2, 3.2);
        check("Bể mái: bản đáy Mgối (lớp trên) = α·q·L1²/12", rr.BottomTop.M, aB * qB * 2.2 * 2.2 / 12, 1e-9);
        check("Bể mái: bản đáy Mnhịp (lớp dưới) = α·q·L1²/24", rr.BottomBot.M, aB * qB * 2.2 * 2.2 / 24, 1e-9);

        // ---- người dùng chọn thép thiếu → LỖI có đề xuất ----
        var weak = new WaterTankInput { Name = "BN3", Position = WaterTankInput.PosRoof, Lx = 4, Ly = 4, H = 3.0, Hw = 2.8, Tw = 0.2, Tb = 0.25, Tc = 0.12, WallVInD = 10, WallVInS = 0.2, WallHD = 10, WallHS = 0.2 };
        var rk = (WaterTankResult)weak.Run(s);
        check("Thép người dùng chọn thiếu/nứt vượt → LỖI có đề xuất", B(rk.Messages.Any(m => m.StartsWith("LỖI") && (m.Contains("đề xuất") || m.Contains("yêu cầu")))), 1, 0);
        check("Thép người dùng chọn được giữ nguyên Ø10a200", B(rk.WallVertIn.D == 10 && Math.Abs(rk.WallVertIn.S - 0.2) < 1e-12 && rk.WallVertIn.UserChoice), 1, 0);

        // ---- dữ liệu sai không ném lỗi ----
        bool threw = false; DetailResult bad = null;
        try { bad = new WaterTankInput { Lx = 0, Ly = -1, H = 0, Tw = 0 }.Run(s); }
        catch { threw = true; }
        check("Dữ liệu sai: không ném ngoại lệ, có LỖI", B(!threw && bad != null && bad.Messages.Any(m => m.StartsWith("LỖI")) && !bad.Ok), 1, 0);
        bool threw2 = false;
        try { var fb = new WaterTankInput { Lx = double.NaN }; var rb = fb.Run(null); fb.Draw(rb, null); }
        catch { threw2 = true; }
        check("Dữ liệu NaN, thiết lập null: không ném ngoại lệ", B(!threw2), 1, 0);

        // ---- bản vẽ ----
        var figs = ug.Draw(r, s);
        check("Bản vẽ bể: mặt bằng + mặt cắt 1-1", figs.Count, 2, 0);
        string dir = Path.Combine(outDir, "TrienKhai");
        Directory.CreateDirectory(dir);
        figs.AddRange(roof.Draw(rr, s));
        var sh = SheetComposer.Compose(figs, WaterTankDrawing.SheetScale, new SheetComposer.SheetInfo { ProjectName = "Kiểm chứng bể nước", Title = "BỂ NƯỚC BTCT", SheetNo = "KC-BN" }, "A1");
        string path = Path.Combine(dir, "BeNuoc.dxf");
        sh.Save(path);
        string txt = File.ReadAllText(path);
        check("Bản vẽ bể DXF: tồn tại, không rỗng", B(File.Exists(path) && new FileInfo(path).Length > 2000), 1, 0);
        check("Bản vẽ bể DXF: layer BE_TONG, THEP, KICH_THUOC, CHU, KY_HIEU", B(new[] { "BE_TONG", "THEP", "KICH_THUOC", "CHU", "KY_HIEU" }.All(txt.Contains)), 1, 0);
        check("Bản vẽ bể DXF: có tên mặt cắt và nhãn thép", B(txt.Contains(DxfWriter.Encode("MẶT CẮT 1-1")) && txt.Contains(DxfWriter.Encode(r.WallVertIn.Text))), 1, 0);
    }
}
