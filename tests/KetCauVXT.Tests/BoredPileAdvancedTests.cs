using KetCauVXT.Design.Foundation;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng cọc khoan nhồi nâng cao (mini D300–600 và đường kính lớn D800–1500+): khoảng cách / mép tối thiểu, ống siêu âm, con kê,
/// bê tông B22,5, hệ số nhóm Converse–Labarre, thanh chống – giằng Blévot đài 2/3/4 cọc, nối lồng thép, ma sát âm, đường cong sức chịu tải,
/// tự chọn chiều dài cọc, hố khoan → bảng lớp đất, sức chịu tải chế độ thiết kế, lún móng tương đương, đài cọc khoan nhồi lớn chế độ hồ sơ,
/// mặt bằng định vị cọc (toạ độ P1…Pn). Kỳ vọng tính tay dạng đóng.</summary>
public static class BoredPileAdvancedTests
{
    private static double B(bool x) => x ? 1 : 0;

    private static string Safe(string s)
    {
        var bad = Path.GetInvalidFileNameChars();
        return new string((s ?? "").Select(ch => bad.Contains(ch) || ch == ' ' ? '_' : ch).ToArray());
    }

    private static List<PileSoilLayer> Soil(bool consolidating = true) => new()
    {
        new() { Name = "Đất đắp", Thickness = 2, Kind = BoredPile.KindClay, IL = 0.8, Gamma = 17, E = 4000, Consolidating = consolidating },
        new() { Name = "Sét dẻo chảy", Thickness = 6, Kind = BoredPile.KindClay, IL = 0.9, Gamma = 8, E = 3000, Consolidating = consolidating },
        new() { Name = "Sét pha dẻo cứng", Thickness = 10, Kind = BoredPile.KindClay, IL = 0.3, NSpt = 14, Gamma = 9.5, E = 15000 },
        new() { Name = "Cát thô chặt", Thickness = 30, Kind = BoredPile.KindSandCoarse, NSpt = 35, Gamma = 10, E = 40000 }
    };

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "TrienKhai", "CocKhoanNhoiLon");
        Directory.CreateDirectory(dir);
        var s = new DetailSettings();

        // ---- 1. Cấu tạo: khoảng cách, mép, ống siêu âm, con kê ----
        check("Khoảng cách tối thiểu cọc mini D600 = 3D", BoredPileDesign.MinSpacing(0.6), 1.8, 1e-12);
        check("Khoảng cách tối thiểu D800 = max(2,5D; D + 1) = 2,0 m", BoredPileDesign.MinSpacing(0.8), 2.0, 1e-12);
        check("Khoảng cách tối thiểu D1000 = 2,5 m", BoredPileDesign.MinSpacing(1.0), 2.5, 1e-12);
        check("Khoảng cách tối thiểu D2000 = 5,0 m", BoredPileDesign.MinSpacing(2.0), 5.0, 1e-12);
        check("Mép tối thiểu D500 = max(0,15; D/3)", BoredPileDesign.MinEdgeClear(0.5), 0.5 / 3, 1e-12);
        check("Mép tối thiểu D1000 = max(0,25; D/3)", BoredPileDesign.MinEdgeClear(1.0), 1.0 / 3, 1e-12);
        check("Mép tối thiểu D300 = 0,15 m", BoredPileDesign.MinEdgeClear(0.3), 0.15, 1e-12);
        check("Ống siêu âm D600: 0", BoredPileDesign.SonicTubes(0.6), 0, 0);
        check("Ống siêu âm D900: 2", BoredPileDesign.SonicTubes(0.9), 2, 0);
        check("Ống siêu âm D1000: 3", BoredPileDesign.SonicTubes(1.0), 3, 0);
        check("Ống siêu âm D1500: 3", BoredPileDesign.SonicTubes(1.5), 3, 0);
        check("Ống siêu âm D1600: 4", BoredPileDesign.SonicTubes(1.6), 4, 0);
        check("Con kê mỗi tầng D600 / D1000 / D1500 = 4 / 6 / 8", BoredPileDesign.SpacersPerLevel(0.6) * 100 + BoredPileDesign.SpacersPerLevel(1.0) * 10 + BoredPileDesign.SpacersPerLevel(1.5), 468, 0);
        check("Số tầng con kê lồng 20 m (a2000) = 11", BoredPileDesign.SpacerLevels(20), 11, 0);
        check("Số tầng con kê lồng 6 m = 4", BoredPileDesign.SpacerLevels(6), 4, 0);

        // ---- 2. Bê tông B22,5 (nội suy B20 – B25) ----
        var b225 = Material.Concrete("B22.5");
        check("B22,5: Rb = 13,0 MPa", b225.Rb, 13.0 * U.MPa, 1e-9);
        check("B22,5: Rbt = 0,975 MPa", b225.Rbt, 0.975 * U.MPa, 1e-9);
        check("B22,5: Eb = 28 750 MPa", b225.E, 28750 * U.MPa, 1e-6);
        check("B22,5: Rbn = 16,75 MPa", b225.Rbn, 16.75 * U.MPa, 1e-9);
        check("B22,5: viết dấu phẩy B22,5 cùng giá trị", Material.Concrete("B22,5").Rb, b225.Rb, 1e-9);
        int i225 = Array.IndexOf(Material.ConcreteClasses, "B22.5");
        check("Danh sách cấp bê tông có B22.5 nằm giữa B20 và B25", B(i225 > 0 && Material.ConcreteClasses[i225 - 1] == "B20" && Material.ConcreteClasses[i225 + 1] == "B25"), 1, 0);

        // ---- 3. Hệ số nhóm Converse–Labarre ----
        double th = Math.Atan(1.0 / 2.5) * 180 / Math.PI;
        check("Converse–Labarre 2×2, D1000, s2500: η = 1 − θ·4/360", BoredPileDesign.GroupEfficiency(2, 2, 1.0, 2.5), 1 - th * 4 / 360, 1e-12);
        double th3 = Math.Atan(0.6 / 1.8) * 180 / Math.PI;
        check("Converse–Labarre 3×2: η = 1 − θ·[(2)·2 + (1)·3]/(90·6)", BoredPileDesign.GroupEfficiency(3, 2, 0.6, 1.8), 1 - th3 * 7 / 540, 1e-12);
        check("Converse–Labarre 1 cọc: η = 1", BoredPileDesign.GroupEfficiency(1, 1, 1.0, 2.5), 1, 0);
        var rw = BoredPileDesign.Rows(new List<(double X, double Y)> { (-1, -1), (1, -1), (-1, 1), (1, 1) });
        check("Số hàng nhóm 4 cọc vuông = 2×2", rw.Nx * 10 + rw.Ny, 22, 0);
        var rw2 = BoredPileDesign.Rows(new List<(double X, double Y)> { (-1.25, 0), (1.25, 0) });
        check("Số hàng nhóm 2 cọc = 2×1", rw2.Nx * 10 + rw2.Ny, 21, 0);

        // ---- 4. Thanh chống – giằng (Blévot) ----
        check("Blévot k: 2/3/4 cọc = 0,25 / 0,30 / √2/4", BoredPileDesign.BlevotK(2) + BoredPileDesign.BlevotK(3) + BoredPileDesign.BlevotK(4), 0.55 + Math.Sqrt(2) / 4, 1e-12);
        const double rb = 14500, rs = 350000, fck = 22;
        var t2 = BoredPileDesign.StrutTie(4000, 2, 1.5, 0.6, 1.4, 0.36, Math.PI / 4, rb, rs, fck);
        check("Đài 2 cọc: T = N·(2l − c)/(8z) (l = 3,0 m)", t2.Tie, 4000 * (6 - 0.6) / (8 * 1.4), 1e-9);
        check("Đài 2 cọc: θ = arctan(z/a), a = r − c/4", t2.ThetaDeg, Math.Atan(1.4 / 1.35) * 180 / Math.PI, 1e-9);
        check("Đài 2 cọc: As = T/Rs", t2.AsReq, t2.Tie / rs, 1e-12);
        double s2 = Math.Pow(Math.Sin(t2.ThetaDeg * Math.PI / 180), 2);
        check("Đài 2 cọc: nút cột σ = N/(Ac·sin²θ)", t2.SigmaColNode, 4000 / (0.36 * s2), 1e-6);
        check("Đài 2 cọc: nút đầu cọc σ = R/(Ap·sin²θ)", t2.SigmaPileNode, 2000 / (Math.PI / 4 * s2), 1e-6);
        check("Giới hạn nút cột ν'·Rb, ν' = 1 − fck/250", t2.LimitColNode, (1 - fck / 250.0) * rb, 1e-9);
        check("Giới hạn nút cọc 0,85·ν'·Rb", t2.LimitPileNode, 0.85 * (1 - fck / 250.0) * rb, 1e-9);
        check("Đài 2 cọc: (r − c/2)/h0 = 0,857 ≤ 1 → đài sâu", B(t2.Deep && Math.Abs(t2.ShearSpanRatio - 1.2 / 1.4) < 1e-12), 1, 0);
        double r4 = 3.0 / Math.Sqrt(2);
        var t4 = BoredPileDesign.StrutTie(8000, 4, r4, 0.6, 1.4, 0.36, Math.PI / 4, rb, rs, fck);
        check("Đài 4 cọc: T theo cạnh = N·(2s − c)/(16z)", t4.Tie, 8000 * (6 - 0.6) / (16 * 1.4), 1e-9);
        var t3 = BoredPileDesign.StrutTie(6000, 3, 1.7, 0.6, 1.4, 0.36, Math.PI / 4, rb, rs, fck);
        check("Đài 3 cọc: T = Hr/√3, Hr = R·(r − 0,3c)/z", t3.Tie, 2000 * (1.7 - 0.18) / 1.4 / Math.Sqrt(3), 1e-9);
        check("Thanh chống – giằng N ≤ 0: không tính", BoredPileDesign.StrutTie(0, 2, 1.5, 0.6, 1.4, 0.36, 0.785, rb, rs, fck).Tie, 0, 0);

        // ---- 5. Đài 1 cọc, nối lồng thép ----
        var sc = BoredPileDesign.SinglePileCapMin(1.0, 0.6, 0.8, 0.1);
        check("Đài 1 cọc D1000: cạnh ≥ D + 2·max(0,25; D/3)", sc.Side, 1.0 + 2.0 / 3, 1e-12);
        check("Đài 1 cọc: H ≥ max(0,6; La + ngàm + 0,1)", sc.H, 1.0, 1e-12);
        var cs = BoredPileDesign.CageSplices(20, 20);
        check("Lồng 20 m Ø20: lap = 40d = 0,80 m", cs.Lap, 0.80, 1e-12);
        check("Lồng 20 m Ø20: 2 đoạn (11,7 + 9,1)", cs.Segments.Count * 100 + cs.Segments[^1], 209.1, 1e-9);
        check("Lồng 20 m Ø20: mối nối ở độ sâu 10,9 m", cs.SpliceDepths.FirstOrDefault(), 10.9, 1e-9);
        check("Lồng 20 m Ø20: chiều dài thanh = lồng + lap", cs.BarLength, 20.8, 1e-9);
        check("Lồng 20 m Ø20: lệch so le 1,3·lap", cs.Stagger, 1.04, 1e-12);
        var cs3 = BoredPileDesign.CageSplices(30, 25);
        check("Lồng 30 m Ø25: 3 đoạn, 2 mối nối, tổng = lồng + 2·lap", cs3.Segments.Count * 100 + cs3.Splices * 10 + (Math.Abs(cs3.BarLength - 32.0) < 1e-9 ? 1 : 0), 321, 0);
        check("Lồng 10 m: 1 đoạn, không nối", BoredPileDesign.CageSplices(10, 20).Splices, 0, 0);

        // ---- 6. Ma sát âm ----
        var soil = Soil();
        var bp = BoredPile.Capacity(soil, 1.0, 2.0, 25, BoredPile.MethodTable, BoredPile.DrillSlurry);
        var nf = BoredPileDesign.NegativeFriction(bp, soil);
        check("Ma sát âm: có kết quả khi có lớp cố kết", B(nf != null), 1, 0);
        check("Ma sát âm: mặt trung hoà tại đáy lớp cố kết sâu nhất z = 8 m", nf?.NeutralDepth ?? 0, 8, 1e-12);
        double lost = bp.Rows.Where(x => x.To <= 8 + 1e-9).Sum(x => x.Q), pn = bp.Rows.Where(x => x.To <= 8 + 1e-9).Sum(x => bp.U * x.Fi * x.Len);
        check("Ma sát âm: ΣQ bỏ đi = ma sát dương các lớp trên mặt trung hoà", nf?.ShaftLost ?? 0, lost, 1e-9);
        check("Ma sát âm: Pn = u·Σfi·li", nf?.Pn ?? 0, pn, 1e-9);
        check("Ma sát âm: Pn > 0", B(pn > 0), 1, 0);
        check("Ma sát âm: [P]đ,net = γ0(Rc,u − ΣQ)/(γnγk) − Pn", nf?.PsoilNet ?? 0, 1.15 * (bp.Rcu - lost) / (1.15 * 1.4) - pn, 1e-9);
        check("Ma sát âm: không có lớp cố kết → null", B(BoredPileDesign.NegativeFriction(bp, Soil(false)) == null), 1, 0);

        // ---- 7. Đường cong sức chịu tải, tự chọn chiều dài, hao hụt ----
        var curve = BoredPileDesign.Curve(soil, 1.0, 2.0, 10, 30, 1.0, BoredPile.MethodTable, BoredPile.DrillSlurry, 0);
        check("Đường cong L = 10…30 m bước 1 m: 21 điểm", curve.Count, 21, 0);
        check("Đường cong: Rc,u tại L = 30 m > tại L = 10 m", B(curve.Count > 0 && curve[^1].Rcu > curve[0].Rcu), 1, 0);
        check("Đường cong: mũi trong cát chặt N35 là lớp tốt, sét dẻo chảy không tốt",
              B(BoredPileDesign.GoodTip(soil[3]) && !BoredPileDesign.GoodTip(soil[1]) && BoredPileDesign.GoodTip(soil[2])), 1, 0);
        check("Đường cong: [P] = Psoil khi không có Pvl", B(curve.All(c => Math.Abs(c.Allowable - c.Psoil) < 1e-9)), 1, 0);
        double preq = curve[10].Psoil * 0.98;
        var al = BoredPileDesign.AutoLength(soil, 1.0, 2.0, preq, 0, BoredPile.MethodTable, BoredPile.DrillSlurry);
        check("Tự chọn L: đạt", B(al.Ok), 1, 0);
        check("Tự chọn L: bội số 0,5 m", B(Math.Abs(al.Length * 2 - Math.Round(al.Length * 2)) < 1e-9), 1, 0);
        check("Tự chọn L: [P] tại L chọn ≥ yêu cầu", B(al.Result != null && al.Result.Psoil >= preq - 1e-6), 1, 0);
        var prev = al.Curve.FirstOrDefault(c => Math.Abs(c.Length - (al.Length - 0.5)) < 1e-9);
        check("Tự chọn L: L − 0,5 m không đạt (nhỏ nhất)", B(prev == null || prev.Allowable < preq - 1e-6 || !prev.GoodTip), 1, 0);
        var alNo = BoredPileDesign.AutoLength(soil, 1.0, 2.0, 1e7, 0, BoredPile.MethodTable, BoredPile.DrillSlurry);
        check("Tự chọn L: tải không thể đạt → Ok = false, lấy L có [P] lớn nhất",
              B(!alNo.Ok && Math.Abs(alNo.Curve.First(c => Math.Abs(c.Length - alNo.Length) < 1e-9).Allowable - alNo.Curve.Max(c => c.Allowable)) < 1e-9), 1, 0);
        check("Hệ số hao hụt: toàn đất yếu cố kết = 1,20", BoredPileDesign.OvercutFactor(soil, 0, 8), 1.20, 1e-9);
        check("Hệ số hao hụt: toàn cát = 1,15", BoredPileDesign.OvercutFactor(soil, 18, 20), 1.15, 1e-9);
        check("Hệ số hao hụt: 10 m sét cứng (1,10) + 5 m cát (1,15) = 1,117 → 1,10 (làm tròn 0,05)", BoredPileDesign.OvercutFactor(soil, 8, 15), 1.10, 1e-9);

        // ---- 8. Hố khoan → bảng lớp đất; sức chịu tải chế độ thiết kế ----
        var bh = new Borehole
        {
            Name = "HK1", WaterDepth = 5,
            Layers =
            {
                new SoilLayer { Name = "Đất đắp", Kind = SoilKind.Fill, Top = 0, Bottom = 2, Gamma = 17, N = 4, E = 3 },
                new SoilLayer { Name = "Bùn sét", Kind = SoilKind.Organic, Top = 2, Bottom = 8, Gamma = 16, GammaSat = 17, N = 2, E = 1.5 },
                new SoilLayer { Name = "Sét pha", Kind = SoilKind.SiltyClay, Top = 8, Bottom = 18, Gamma = 19, GammaSat = 19.5, N = 12, E = 12 },
                new SoilLayer { Name = "Cát thô", Kind = SoilKind.Sand, Grain = SandGrain.Coarse, Top = 18, Bottom = 45, Gamma = 19, GammaSat = 20, N = 35, E = 40 }
            }
        };
        var fl = BoredPileDesign.FromBorehole(bh);
        check("Hố khoan → lớp đất: 5 lớp (bùn chia tại mực nước 5 m)", fl.Count, 5, 0);
        check("Hố khoan → lớp đất: tổng bề dày = 45 m", BoredPileDesign.TotalDepth(fl), 45, 1e-9);
        check("Hố khoan → lớp đất: bùn dưới mực nước γ' = γsat − 10 = 7", fl[2].Gamma, 7, 1e-9);
        check("Hố khoan → lớp đất: bùn trên mực nước γ = 16", fl[1].Gamma, 16, 1e-9);
        check("Hố khoan → lớp đất: bùn hữu cơ IL ≥ 1", B(fl[1].IL >= 1), 1, 0);
        check("Hố khoan → lớp đất: sét pha chưa có IL, N = 12 → IL = 0,5", fl[3].IL, 0.5, 1e-12);
        check("Hố khoan → lớp đất: E cát 40 MPa → 40 000 kPa", fl[4].E, 40000, 1e-9);
        check("Hố khoan → lớp đất: đất đắp + bùn đánh dấu cố kết, sét / cát không",
              B(fl[0].Consolidating && fl[1].Consolidating && fl[2].Consolidating && !fl[3].Consolidating && !fl[4].Consolidating), 1, 0);
        check("Hố khoan → lớp đất: cát thô → KindSandCoarse", B(fl[4].Kind == BoredPile.KindSandCoarse), 1, 0);

        var prj = new Project();
        prj.Soil.Depth = 2.0;
        var fs = new FoundationSettings { PileBored = true, BoredMethod = BoredPile.MethodTable, PileSize = 1.0, PileLength = 25, PileCapacity = 3000 };
        var dc = BoredPileDesign.DesignCapacity(prj, bh, fs);
        var bpD = BoredPile.Capacity(fl, 1.0, 2.0, 25, BoredPile.MethodTable, BoredPile.DrillSlurry);
        var nfD = BoredPileDesign.NegativeFriction(bpD, fl);
        check("Thiết kế: [P]đ = γ0(Rc,u − ΣQ)/(γnγk) − Pn (cùng bảng lớp đất)", dc.Pdn, nfD.PsoilNet, 1e-6);
        check("Thiết kế: FS = γn·γk/γ0", dc.SafetyFactor, 1.15 * 1.4 / 1.15, 1e-12);
        check("Thiết kế: mũi cọc tại 27 m", dc.TipDepth, 27, 1e-9);
        check("Thiết kế: dòng cuối bảng là mũi cọc", B(dc.Rows.Count > 1 && dc.Rows[^1].IsTip), 1, 0);
        check("Thiết kế: có ghi đường cong sức chịu tải và ma sát âm",
              B(dc.Notes.Any(n => n.StartsWith("Đường cong sức chịu tải")) && dc.Notes.Any(n => n.StartsWith("Ma sát âm"))), 1, 0);
        var gc = GeotechCalc.PileCapacity(prj, bh, fs);
        check("Thiết kế: GeotechCalc.PileCapacity chuyển sang cọc khoan nhồi TCVN 10304 khi chọn phương pháp", gc.Pdn, dc.Pdn, 1e-6);
        var fsA = new FoundationSettings { PileBored = true, BoredMethod = BoredPile.MethodTable, PileSize = 1.0, PileLength = 25, PileCapacity = 2500, PileAutoLength = true };
        var dcA = BoredPileDesign.DesignCapacity(prj, bh, fsA);
        check("Thiết kế tự chọn L: có thuyết minh tự chọn chiều dài", B(dcA.Notes.Any(n => n.Contains("chiều dài cọc"))), 1, 0);

        // ---- 9. Lún móng tương đương 2L/3 ----
        var se = BoredPileDesign.SettlementEquivalentRaft(soil, 3.5, 3.5, 25, 2, 8000);
        check("Lún móng tương đương: p0 = Ntc/(B·L)", se.P0, 8000 / (3.5 * 3.5), 1e-9);
        check("Lún móng tương đương: S > 0, vùng nén > 0", B(se.S > 0 && se.Depth > 0), 1, 0);

        // ---- 10. Đài cọc khoan nhồi D1000 (chế độ hồ sơ) ----
        var cap = new ManualPileCap
        {
            Name = "ĐK1", N = 9000, Mx = 0, My = 150, PileD = 1.0, PileType = ManualPileCap.TypeBored, CapacityFromSoil = true, SoilLayers = Soil(),
            PileLength = 30, Df = 2.0, NumPiles = 2, PileBarN = 18, PileBarD = 25, ColX = 0.6, ColY = 0.6, GroupEfficiency = true, PileConcrete = "B30"
        };
        var rc = cap.Run(s);
        check("Đài D1000: khoảng cách mặc định ≥ max(2,5D; D + 1)", B(rc.Checks.Any(k => k.Name.Contains("max(2,5D; D + 1 m)") && k.Capacity >= 2.5 - 1e-9)), 1, 0);
        check("Đài D1000 2 cọc: có 4 kiểm tra thanh chống – giằng", rc.Checks.Count(k => k.Name.StartsWith("Thanh chống – giằng")), 4, 0);
        check("Đài D1000: lưu kết quả thanh chống – giằng 2 cọc", cap.StrutTieResult?.Piles ?? 0, 2, 0);
        check("Đài D1000: kiểm tra nhóm cọc ma sát Converse–Labarre", B(rc.Checks.Any(k => k.Name.StartsWith("Nhóm cọc ma sát"))), 1, 0);
        check("Đài D1000: ma sát âm tính từ lớp cố kết", B(cap.NegFriction != null && cap.NegFriction.Pn > 0), 1, 0);
        check("Đài D1000: 3 ống siêu âm mỗi cọc", cap.SonicTubeCount, 3, 0);
        var eT = rc.Extra.FirstOrDefault(e => e.Item1.StartsWith("Ống siêu âm"));
        check("Đài D1000: khối lượng ống siêu âm = n·3·(L + ngàm + 0,5)", eT.Item3, 2 * 3 * (30 + cap.Embed + 0.5), 1e-9);
        check("Đài D1000: có ống khoan lõi mũi cọc D114", B(rc.Extra.Any(e => e.Item1.StartsWith("Ống khoan lõi"))), 1, 0);
        var eK = rc.Extra.FirstOrDefault(e => e.Item1.StartsWith("Con kê"));
        check("Đài D1000: con kê = n·tầng·6", eK.Item3, 2 * BoredPileDesign.SpacerLevels(cap.CageLenUsed) * 6, 1e-9);
        check("Đài D1000: chia lồng thép khi lồng > 11,7 m", B(cap.CageSplice != null && (cap.CageLenUsed <= 11.7 + 1e-9 || cap.CageSplice.Splices > 0)), 1, 0);
        check("Đài D1000: đường cong sức chịu tải ≥ 2 điểm", B(cap.CapacityCurve.Count >= 2), 1, 0);
        check("Đài D1000: [P] ≤ Pvl và > 0", B(cap.PaUsed > 0 && cap.PaUsed <= BoredPile.MaterialCapacity(1.0, Material.Concrete("B30").Rb, Material.Rebar("CB400-V").Rsc, 18 * Math.PI * 0.025 * 0.025 / 4) + 1e-6), 1, 0);
        var figs = cap.Draw(rc, s);
        string all = string.Join("\n", figs.Select(f => f.W.ToDxfStringDec()));
        check("Bản vẽ D1000: có đường cong sức chịu tải", B(figs.Any(f => f.Name.StartsWith("ĐƯỜNG CONG SỨC CHỊU TẢI"))), 1, 0);
        check("Bản vẽ D1000: ghi ống siêu âm và con kê 6 cái/tầng", B(all.Contains("ỐNG SIÊU ÂM") && all.Contains("CON KÊ 6 CÁI")), 1, 0);
        check("Bản vẽ D1000: ghi nối chồng lồng thép khi chia đoạn", B(cap.CageSplice.Splices == 0 || all.Contains("NỐI CHỒNG 40d")), 1, 0);
        foreach (var f in figs) f.W.Save(Path.Combine(dir, "DaiCocKhoanNhoi_D1000_" + Safe(f.Name) + ".dxf"));

        var capA = new ManualPileCap
        {
            Name = "ĐK2", N = 5000, Mx = 0, My = 0, PileD = 1.0, PileType = ManualPileCap.TypeBored, CapacityFromSoil = true, SoilLayers = Soil(false),
            AutoLength = true, Pa = 2500, Df = 2.0, NumPiles = 2, PileBarN = 18, PileBarD = 25, ColX = 0.6, ColY = 0.6
        };
        capA.Run(s);
        check("Đài tự chọn L: chiều dài là bội số 0,5 m", B(Math.Abs(capA.PileLength * 2 - Math.Round(capA.PileLength * 2)) < 1e-9 && capA.PileLength >= 10), 1, 0);
        check("Đài tự chọn L: [P] dùng ≥ [P] yêu cầu 2500 kN (Pvl đủ)", B(capA.PaUsed >= 2500 - 1), 1, 0);

        var cap1 = new ManualPileCap
        {
            Name = "ĐK3", N = 3000, Mx = 0, My = 0, PileD = 1.2, PileType = ManualPileCap.TypeBored, Pa = 4000, NumPiles = 1, PileLength = 25, Df = 2.0,
            PileBarN = 20, PileBarD = 25, ColX = 0.6, ColY = 0.6
        };
        var r1 = cap1.Run(s);
        check("Đài 1 cọc D1200: 2 kiểm tra kích thước đài tối thiểu", r1.Checks.Count(k => k.Name.StartsWith("Đài đơn cọc")), 2, 0);
        check("Đài 1 cọc D1200: không có thanh chống – giằng", B(cap1.StrutTieResult == null), 1, 0);

        // ---- 11. Mặt bằng định vị cọc ----
        var dp = new DetailingProject();
        dp.Plan.SpansX = "6 6"; dp.Plan.SpansY = "6";
        var pc = new ManualPileCap { Name = "Đ4", N = 3600, Mx = 0, My = 0, PileD = 0.5, PileType = ManualPileCap.TypeMiniBored, Pa = 1200, NumPiles = 4, PileBarN = 6, PileBarD = 16, Df = 1.8 };
        dp.PileCaps.Add(pc);
        check("Mặt bằng cọc: chưa đặt đài → không có hình / nhóm", B(PileLocationPlan.PlanFigure(dp) == null && StructuralPlan.GroupForTree(dp).All(g => g.Kind != PileLocationPlan.Kind)), 1, 0);
        var fnd = dp.Plan.Foundation;
        fnd.Items.Add(new PlanItem { Kind = PlanKinds.Footing, Member = "Đ4", X1 = 6, Y1 = 6, X2 = 6, Y2 = 6 });
        fnd.Items.Add(new PlanItem { Kind = PlanKinds.Footing, Member = "Đ4", X1 = 0, Y1 = 0, X2 = 0, Y2 = 0, Rotation = 90 });
        var rows = PileLocationPlan.Rows(dp);
        check("Mặt bằng cọc: 2 đài × 4 cọc = 8 cọc", rows.Count, 8, 0);
        check("Mặt bằng cọc: số hiệu P1…P8 không trùng", B(rows.Select(r => r.No).Distinct().Count() == 8 && rows[0].No == "P1" && rows[^1].No == "P8"), 1, 0);
        var r6 = rows.Take(4).ToList();
        check("Mặt bằng cọc: đài tại giao trục (6; 6) đánh số trước, tâm nhóm cọc trùng tim cột",
              B(Math.Abs(r6.Average(r => r.X) - 6) < 1e-6 && Math.Abs(r6.Average(r => r.Y) - 6) < 1e-6), 1, 0);
        var r0 = rows.Skip(4).ToList();
        check("Mặt bằng cọc: đài xoay 90° tại (0; 0) giữ tâm nhóm cọc", B(Math.Abs(r0.Average(r => r.X)) < 1e-6 && Math.Abs(r0.Average(r => r.Y)) < 1e-6), 1, 0);
        double half = pc.Piles.Max(q => Math.Abs(q.X));
        check("Mặt bằng cọc: toạ độ cọc = tim cột + toạ độ trong đài", r6.Max(r => r.X), 6 + half, 1e-6);
        check("Mặt bằng cọc: cao độ đầu cọc = −Df + ngàm", rows[0].HeadElev, -1.8 + pc.Embed, 1e-12);
        check("Mặt bằng cọc: cao độ mũi = −Df − L", rows[0].TipElev, -1.8 - pc.PileLength, 1e-12);
        var sch = PileLocationPlan.Schedule(rows);
        check("Mặt bằng cọc: bảng thống kê gộp 1 loại, 8 cọc", sch.Count * 100 + sch.Sum(x => x.Count), 108, 0);
        var groups = StructuralPlan.GroupForTree(dp);
        var gP = groups.FirstOrDefault(g => g.Kind == PileLocationPlan.Kind);
        check("Cây: có nhóm Mặt bằng định vị cọc chứa đài Đ4, ngay sau Mặt bằng móng",
              B(gP != null && gP.Members.Any(m => m.Name == "Đ4") && groups.IndexOf(gP) == groups.FindIndex(g => g.Kind == "Mong") + 1), 1, 0);
        var res = dp.All.Select(m => (m, DetailingProject.RunSafe(m, dp.Settings))).ToList();
        var pfs = StructuralPlanDrawing.ForGroup(dp, res, gP);
        check("Xem trước nhóm: 1 hình MẶT BẰNG ĐỊNH VỊ CỌC", B(pfs.Count == 1 && pfs[0].Name == PileLocationPlan.Title), 1, 0);
        string dPlan = pfs.Count > 0 ? pfs[0].W.ToDxfStringDec() : "";
        check("Hình mặt bằng cọc: có bảng toạ độ, bảng thống kê, P1, P8, trục",
              B(dPlan.Contains("BẢNG TOẠ ĐỘ CỌC")) + B(dPlan.Contains("BẢNG THỐNG KÊ CỌC")) + B(dPlan.Contains("P1")) + B(dPlan.Contains("P8")) + B(dPlan.Contains(Lay.Truc)), 5, 0);
        check("Hình mặt bằng cọc: ghi toạ độ X cọc đầu tiên", B(dPlan.Contains(rows[0].X.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture))), 1, 0);
        var build = StructuralPlanDrawing.Build(dp, res);
        int iF = build.FindIndex(f => f.Name == StructuralPlanDrawing.TitleFoundation), iC = build.FindIndex(f => f.Name == PileLocationPlan.Title);
        check("Bản vẽ mặt bằng: mặt bằng định vị cọc xuất sau mặt bằng móng", B(iF >= 0 && iC > iF), 1, 0);
        if (pfs.Count > 0) pfs[0].W.Save(Path.Combine(dir, "MatBangDinhViCoc.dxf"));
    }
}
