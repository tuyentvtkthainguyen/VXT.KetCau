using KetCauVXT.Design.Concrete;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Test 63 – Cầu thang bộ: bản thang thường bậc xây gạch (mặc định) / bậc BTCT liền khối, mặt bằng bố trí thép bản thang + mặt cắt A-A,
/// thang zíc zắc, thang xương cá (bậc công xôn, xoắn dầm cốn), thang thép hình; hố pít – vách thang máy (áp lực đất, đẩy nổi, giảm chấn).</summary>
public static class StairLiftTests
{
    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        static int B(bool v) => v ? 1 : 0;
        static bool NoErr(DetailResult r) => !r.Messages.Any(m => m.StartsWith("LỖI"));
        var s = new DetailSettings();
        var all = new List<Figure>();
        var def = DrawingStyle.Default();

        // ---- 1. bản thang thường: bậc xây gạch (mặc định) và bậc BTCT liền khối ----
        var brick = ManualStair.Sample();
        check("Thang mặc định: kết cấu bản thang thường, bậc xây gạch", B(StairForm.IsFlat(brick.Form) && brick.Steps.StartsWith("Xây")), 1, 0);
        var rb = brick.Run(s);
        check("Bậc gạch: tính không lỗi", B(NoErr(rb)), 1, 0);
        check("Bậc gạch: không có thép bậc", rb.Bars.Count(b => b.Role.Contains("bậc BTCT")), 0, 0);
        var mono = ManualStair.Sample(); mono.Steps = "Đổ BTCT liền khối";
        var rm = mono.Run(s);
        check("Bậc BTCT: tính không lỗi", B(NoErr(rm)), 1, 0);
        var (pm, stm, _) = mono.BuildModel(s);
        var Lm = stm.Layout(pm);
        double vSteps = Lm.Flights.Sum(f => StairFormulas.MonolithicStepVolume(f.Treads, Lm.Tread, Lm.Riser, f.Width));
        double vClosed = Lm.Flights.Sum(f => f.Treads * 0.5 * Lm.Tread * Lm.Riser * f.Width);
        check("Bậc BTCT: thể tích bậc = Σ n·½·b·h·B", vSteps, vClosed, 1e-9);
        check("Bậc gạch không tính vào bê tông: V(BTCT) − V(gạch) = thể tích bậc", rm.ConcreteVolume - rb.ConcreteVolume, vSteps, 1e-6);
        int treads = Lm.Flights.Sum(f => f.Treads);
        check("Bậc BTCT: thép góc mũi bậc 1 thanh / bậc", rm.Bars.Where(b => b.Role.Contains("thép góc mũi bậc")).Sum(b => b.Count), treads, 0);
        check("Bậc BTCT: số hiệu thép không trùng", rm.Bars.Select(b => b.Mark).Distinct().Count(), rm.Bars.Count, 0);

        List<Figure> fb, fm;
        using (DrawingStyle.Use(def)) { fb = brick.Draw(rb, s); fm = mono.Draw(rm, s); }
        var plan = fb.FirstOrDefault(f => f.Name.StartsWith("MẶT BẰNG BỐ TRÍ THÉP BẢN THANG"));
        check("Bản vẽ thang: có mặt bằng bố trí thép bản thang", B(plan != null), 1, 0);
        check("Bản vẽ thang: có mặt cắt ngang bản thang A-A", B(fb.Any(f => f.Name.StartsWith("MẶT CẮT A-A BẢN THANG"))), 1, 0);
        var mainBar = rb.Bars.FirstOrDefault(b => b.Role.Contains(" BT") && b.Role.EndsWith(" – Dưới"));
        check("Thang: có thép chịu lực bản thang", B(mainBar != null && mainBar.Spacing > 0), 1, 0);
        if (plan != null && mainBar != null)
        {
            string txt = plan.W.ToDxfStringDec();
            string tag = Dt.Spaced(mainBar.Dia, mainBar.Spacing);
            check("Mặt bằng thép thang: chữ thép chịu lực " + tag + " có trên mặt bằng", B(txt.Contains(tag)), 1, 0);
            check("Mặt bằng thép thang: ghi chú bậc xây gạch", B(txt.Contains("BẬC XÂY GẠCH")), 1, 0);
        }
        check("Bậc BTCT: có hình chi tiết bậc", B(fm.Any(f => f.Name.StartsWith("CHI TIẾT BẬC BTCT"))), 1, 0);
        all.AddRange(fb.Concat(fm));

        // ---- 2. thang zíc zắc ----
        var zz = ManualStair.Sample(); zz.Form = StairForm.Zigzag; zz.Waist = 0.15; zz.Name = "CT2";
        var zr = zz.Run(s) as StairFormResult;
        check("Zíc zắc: kết quả dạng đặc biệt", B(zr != null), 1, 0);
        if (zr != null)
        {
            check("Zíc zắc: tính không lỗi", B(NoErr(zr)), 1, 0);
            double b = zr.Tread, h = zr.Riser, t = zr.T;
            double g = 25 * t * (b + h) / b;
            check("Zíc zắc: tải bản thân 25·t·(b + h)/b", StairFormulas.ZigzagSelfWeight(t, b, h), g, 1e-12);
            double q = 1.1 * (g + (1.2 + 0.27) * (b + h) / b) + 1.3 * 3.0;
            check("Zíc zắc: q = 1,1·(g + hoàn thiện) + 1,3·p", zr.Q, q, 1e-9);
            check("Zíc zắc: M = q·L²/8", zr.M, q * zr.Span * zr.Span / 8, 1e-9);
            double c = s.CoverSlab;
            var fx = RcSection.DesignRect(zr.M, 1.0, t - c - 0.005, c + 0.005, s.Conc(), s.SteelFor(10), 0.1);
            check("Zíc zắc: As yêu cầu = DesignRect (≥ Ø8a200)", zr.AsReq, Math.Max(fx.As, Dt.AreaPerM(8, 0.20)), 1e-9);
            check("Zíc zắc: thép chọn ≥ yêu cầu", B(Dt.AreaPerM(zr.MainD, zr.MainS) >= zr.AsReq - 1e-12), 1, 0);
            check("Zíc zắc: có thép góc lõm chữ L", B(zr.Bars.Any(x => x.Role.Contains("góc lõm"))), 1, 0);
            check("Zíc zắc: không có xây bậc gạch", B(!zr.Extra.Any(e => e.Item.Contains("Xây", StringComparison.OrdinalIgnoreCase))), 1, 0);
            double vz = 0;
            var (pz, sz, _) = zz.BuildModel(s);
            var Lz = sz.Layout(pz);
            foreach (var f in Lz.Flights.Where(x => x.Treads > 0)) vz += t * (f.Treads * b + f.Risers * h) * f.Width;
            foreach (var ld in Lz.Landings) vz += ld.Area * zr.LandingT;
            if (zz.LandingBeam) vz += zz.LandingBeamB * zz.LandingBeamH * Lz.Wr;
            check("Zíc zắc: bê tông = Σ t·(n·b + n'·h)·B + chiếu nghỉ + dầm", zr.ConcreteVolume, vz, 1e-9);
            List<Figure> fz; using (DrawingStyle.Use(def)) fz = zz.Draw(zr, s);
            check("Zíc zắc: ≥ 2 hình", B(fz.Count >= 2), 1, 0);
            all.AddRange(fz);
        }

        // ---- 3. thang xương cá ----
        var fbn = ManualStair.Sample(); fbn.Form = StairForm.Fishbone; fbn.Name = "CT3";
        var fr = fbn.Run(s) as StairFormResult;
        check("Xương cá: kết quả dạng đặc biệt", B(fr != null), 1, 0);
        if (fr != null)
        {
            check("Xương cá: tính không lỗi", B(NoErr(fr)), 1, 0);
            double b = fr.Tread, hs = fr.StepH, lc = fr.Lc;
            check("Xương cá: vươn bậc lc = (B − bs)/2", lc, (fr.Width - fbn.SpineB) / 2, 1e-12);
            double q = 1.1 * (25 * b * hs + (1.2 + 0.27) * b) + 1.3 * 3.0 * b;
            check("Xương cá: tải bậc q = 1,1·g + 1,3·p·b", fr.StepQ, q, 1e-9);
            check("Xương cá: M bậc = q·l²/2 + P·l (P = 1 kN)", fr.StepM, q * lc * lc / 2 + 1.0 * lc, 1e-9);
            double tq = 1.3 * 3.0 * b * lc * (lc + fbn.SpineB) / 2 / b;
            check("Xương cá 2 bên: xoắn phân bố do hoạt tải lệch", fr.TorquePerM, tq, 1e-9);
            check("Xương cá 2 bên: T gối = t·L/2", fr.Torsion, tq * fr.Span / 2, 1e-9);
            List<Figure> ff; using (DrawingStyle.Use(def)) ff = fbn.Draw(fr, s);
            check("Xương cá: ≥ 2 hình", B(ff.Count >= 2), 1, 0);
            all.AddRange(ff);
        }
        var fb1 = ManualStair.Sample(); fb1.Form = StairForm.Fishbone; fb1.CantileverSides = "1 bên"; fb1.Name = "CT4";
        var fr1 = fb1.Run(s) as StairFormResult;
        if (fr1 != null)
        {
            double lc1 = fr1.Width - fb1.SpineB;
            check("Xương cá 1 bên: vươn lc = B − bs", fr1.Lc, lc1, 1e-12);
            double t1 = fr1.StepQ * lc1 * (lc1 + fb1.SpineB) / 2 / fr1.Tread;
            check("Xương cá 1 bên: xoắn t = q·lc·(lc + bs)/2/b", fr1.TorquePerM, t1, 1e-9);
            check("Xương cá 1 bên: xoắn lớn hơn 2 bên", B(fr != null && fr1.Torsion > fr.Torsion), 1, 0);
            check("Xương cá 1 bên: có đai kín chịu xoắn", B(fr1.Bars.Any(x => x.Role.Contains("đai kín"))), 1, 0);
        }

        // ---- 4. thang thép hình ----
        var stl = ManualStair.Sample(); stl.Form = StairForm.Steel; stl.SteelSection = "U200"; stl.Name = "CT5";
        var sr = stl.Run(s) as StairFormResult;
        check("Thang thép: kết quả dạng đặc biệt", B(sr != null && sr.Shape != null), 1, 0);
        if (sr != null && sr.Shape != null)
        {
            check("Thang thép: tính không lỗi", B(NoErr(sr)), 1, 0);
            check("Thang thép: U200 Wx = 152 cm³", sr.Shape.Wx, 152, 1e-12);
            double M = sr.Q * sr.Span * sr.Span / 8;
            check("Thang thép: σ = M/Wx (MPa)", sr.Sigma, M / 152e-6 / 1000, 1e-9);
            double d = 5 * sr.QSer * Math.Pow(sr.Span, 4) / (384 * 2.1e8 * 1520e-8 * sr.CosA * sr.CosA);
            check("Thang thép: δ = 5wL⁴/(384EI·cos²α)", sr.Deflection, d, 1e-9);
            check("Thang thép: f CCT34 = 210 MPa", sr.F, 210, 0);
            check("Thang thép: có khối lượng thép hình (kg)", B(sr.Extra.Any(e => e.Unit == "kg" && e.Item.Contains("U200") && e.Qty > 0)), 1, 0);
            check("Thang thép: có sơn chống gỉ", B(sr.Extra.Any(e => e.Item.StartsWith("Sơn") && e.Qty > 0)), 1, 0);
            var rhs = SteelTables.Rhs(200, 100, 5);
            check("Thép hộp 200×100×5: A = 29 cm²", rhs.A, (200 * 100 - 190 * 90) / 100.0, 1e-12);
            List<Figure> fs; using (DrawingStyle.Use(def)) fs = stl.Draw(sr, s);
            check("Thang thép: ≥ 2 hình", B(fs.Count >= 2), 1, 0);
            all.AddRange(fs);
        }

        // ---- 5. hố pít – vách thang máy ----
        var lp = ManualLiftPit.Sample();
        check("Hố pít mẫu: tên HTM1", B(lp.Name == "HTM1"), 1, 0);
        var lr = lp.Run(s) as LiftPitResult;
        check("Hố pít: kết quả", B(lr != null), 1, 0);
        if (lr != null)
        {
            foreach (var msg in lr.Messages.Where(x => x.StartsWith("LỖI"))) Console.WriteLine("    [HTM1] " + msg);
            check("Hố pít mẫu: đạt mọi kiểm tra", B(lr.Ok), 1, 0);
            double ka = Math.Pow(Math.Tan((45 - lp.Phi / 2) * Math.PI / 180), 2);
            check("Hố pít: Ka = tan²(45° − φ/2)", lr.Ka, ka, 1e-12);
            double pTop = ka * lp.Surcharge;
            double z = lp.PitDepth, hw = lp.Hgw;
            double pBase = ka * (lp.Surcharge + lp.GammaS * hw + (lp.GammaS - 10) * (z - hw)) + 10 * (z - hw);
            check("Hố pít: áp lực chân thành (đất + nước ngầm)", lr.PBase, pBase, 1e-9);
            double L = lp.ShaftW + lp.Wall, H = lp.PitDepth;
            double av = Math.Pow(L, 4) / (Math.Pow(L, 4) + 384.0 / 185.0 * Math.Pow(H, 4));
            double mb = av * (pTop * H * H / 8 + (pBase - pTop) * H * H / 15);
            check("Hố pít: M chân thành = αv·(u·H²/8 + t·H²/15)", lr.MWallBase, mb, 1e-9);
            double aOut = (lp.ShaftW + 2 * lp.Wall) * (lp.ShaftD + 2 * lp.Wall);
            double U = 10 * (lp.PitDepth + lp.PitSlab - lp.Hgw) * aOut;
            check("Hố pít: lực đẩy nổi U = γw·hsub·A", lr.BuoyU, U, 1e-9);
            double Gp = 2 * (lp.ShaftW + lp.Wall + lp.ShaftD + lp.Wall) * lp.Wall * lp.PitDepth * 25 + aOut * lp.PitSlab * 25;
            check("Hố pít: trọng lượng giữ G = thành + đáy", lr.BuoyG, Gp, 1e-9);
            check("Hố pít: giảm chấn cabin 4·(P + Q)·g", lr.BufferCar, 4 * (lp.CarMass + lp.RatedLoad) * 9.81 / 1000, 1e-12);
            check("Vách thang máy: μ = 2·As/t (Ø10a200, t = 200) ≥ 0,25 %", lr.Mu * 100, 2 * Dt.AreaPerM(10, 0.20) / 0.20 * 100, 1e-9);
            check("Hố pít: có băng cản nước, chống thấm", B(lr.Extra.Any(e => e.Item.Contains("waterstop")) && lr.Extra.Any(e => e.Item.StartsWith("Chống thấm"))), 1, 0);
            check("Hố pít: số hiệu thép không trùng", lr.Bars.Select(x => x.Mark).Distinct().Count(), lr.Bars.Count, 0);
            List<Figure> fl; using (DrawingStyle.Use(def)) fl = lp.Draw(lr, s);
            check("Hố pít: ≥ 2 hình (mặt bằng, mặt cắt, lỗ cửa)", B(fl.Count >= 2), 1, 0);
            all.AddRange(fl);
        }
        var bad = new ManualLiftPit { Wall = 0.05 };
        var rbad = DetailingProject.RunSafe(bad, s);
        check("Hố pít sai số liệu: báo LỖI, không ngoại lệ", B(rbad.Messages.Any(m => m.StartsWith("LỖI"))), 1, 0);

        // ---- 6. đăng ký trong hồ sơ ----
        var prj = DetailingProject.Sample();
        check("Hồ sơ mẫu: có nhóm Hố pít – vách thang máy", B(prj.Groups.Any(g => g.Key == "HoPit") && prj.LiftPits.Count == 1), 1, 0);
        var back = DetailingProject.FromJson(prj.ToJson());
        check("Hồ sơ: lưu / mở giữ hố pít và kiểu thang", B(back.LiftPits.Count == 1 && back.Stairs[0].Form == prj.Stairs[0].Form), 1, 0);
        var legacy = DetailingProject.FromJson("{\"ProjectName\":\"cũ\",\"LiftPits\":null}");
        check("Hồ sơ cũ (LiftPits null) → danh sách rỗng", B(legacy.LiftPits != null && legacy.LiftPits.Count == 0), 1, 0);
        var added = prj.AddNew("HoPit");
        check("Thêm hố pít mới: tên HTM2", B(added.Name == "HTM2"), 1, 0);

        // ---- DXF ----
        string dir = Path.Combine(outDir, "TrienKhai");
        Directory.CreateDirectory(dir);
        string fn = Path.Combine(dir, "ThangBo_ThangMay.dxf");
        try
        {
            var (sc, paper) = DetailingExport.SheetFor(all);
            SheetComposer.Compose(all, sc, new SheetComposer.SheetInfo { ProjectName = "Kiểm thử cầu thang – thang máy", Title = "CẦU THANG BỘ – HỐ PÍT THANG MÁY", SheetNo = "KC-TM" }, paper).Save(fn);
            check("Xuất DXF thang bộ + thang máy", B(File.Exists(fn) && new FileInfo(fn).Length > 2000), 1, 0);
        }
        catch (Exception e) { Console.WriteLine("    " + e.Message); check("Xuất DXF thang bộ + thang máy", 0, 1, 0); }
    }
}
