using KetCauVXT.Mep;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Điện nước đợt 5 (Mep/MepRound5.cs): khoảng hở ống gen song song trong sàn, Δt theo bảng tỉnh (TCVN 5687 2010 / 2024),
/// LPD + phương pháp hệ số sử dụng, sức chứa ống đứng thoát nước, bể tự hoại 3 ngăn. check(tên, giá trị, kỳ vọng, sai số).</summary>
public static class MepRound5Tests
{
    private static double B(bool b) => b ? 1 : 0;

    /// <summary>3 tầng T1 (0), T2 (3,3), T3 (6,6); sàn T2 6 × 4 m dày slabT (không cột, không dầm).</summary>
    private static Project SlabOnly(double slabT = 0.12)
    {
        var p = new Project();
        p.Stories = new List<Story> { new() { Name = "T1", Elevation = 0, Height = 3.3 }, new() { Name = "T2", Elevation = 3.3, Height = 3.3 }, new() { Name = "T3", Elevation = 6.6, Height = 3.3 } };
        p.SlabSections.Add(new SlabSection { Name = "SX", Thickness = slabT });
        var xy = new (double X, double Y)[] { (0, 0), (6, 0), (6, 4), (0, 4) };
        for (int k = 0; k < 4; k++) p.Nodes.Add(new Node { Id = 11 + k, X = xy[k].X, Y = xy[k].Y, Z = 3.3, Story = "T2" });
        p.Slabs.Add(new Slab { Id = 1, Label = "S1", Nodes = new() { 11, 12, 13, 14 }, Section = "SX", Story = "T2" });
        return p;
    }

    private static MepRun Run(int id, double dia, params (double X, double Y)[] pts) => new()
    {
        Id = id, System = MepSystemKind.DienOCam, Kind = MepRunKind.Day, Story = "T2", ConduitSize = dia, Size = 2.5, Conductors = 3,
        Path = pts.Select(q => new MepPt(q.X, q.Y, 3.24)).ToList()
    };

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "MepRound5");
        Directory.CreateDirectory(dir);

        // ================= 0. Mặc định =================
        var o0 = new MepStdOptions();
        check("Mặc định: TCVN 5687 bản 2010, tải lạnh không theo khí hậu, ống đứng có thông hơi",
            B(o0.Edition == MepStandards.Ed2010 && !o0.CoolingFromClimate && o0.StackVented), 1, 0);
        check("Mặc định: khoảng hở ống gen k = 3Ø, tối thiểu 50 mm, tối đa 4 ống / dải 1 m",
            MepStandards.Val("gen.kc_d") * 1000 + MepStandards.Val("gen.kc_min") * 1000 + MepStandards.Val("gen.max_bo") * 10 + MepStandards.Val("gen.dai_rong"), 3000 + 50 + 40 + 1, 1e-9);
        var ci0 = new CoolingInput();
        check("Mặc định CoolingInput giữ Δt 9, Δw 0,008, hiệu chỉnh CLTD 0", ci0.DeltaT + ci0.DeltaW + ci0.CltdCorrection, 9 + 0.008, 1e-12);
        check("Họ đèn mặc định có quang thông (tuýp LED 1,2 m = 1800 lm)", MepLibrary.Defaults().Find("Đèn tuýp LED 1,2 m")?.Lumen ?? -1, 1800, 0);

        // ================= 1. Khoảng hở ống gen song song =================
        check("Khoảng hở yêu cầu Ø20: max(3·20, 50) = 60 mm", ConduitSpacing.Required(null, 20, 20), 0.06, 1e-12);
        check("Khoảng hở yêu cầu Ø16: max(48, 50) = 50 mm", ConduitSpacing.Required(null, 16, 16), 0.05, 1e-12);
        var p = SlabOnly();
        var ra = Run(1, 20, (1, 1), (1, 2), (5, 2), (5, 1));
        var rb = Run(2, 20, (1.5, 1), (1.5, 2.04), (4.5, 2.04), (4.5, 1));
        p.Mep.Runs.Add(ra); p.Mep.Runs.Add(rb);
        check("Đoạn ống gen trong sàn: 3 + 3 đoạn", ConduitSpacing.Segments(p).Count, 6, 0);
        var it = ConduitSpacing.Check(p);
        check("Hai ống Ø20 cách tim 40 mm (hở 20 < 60) → 1 cảnh báo ONG_GEN_SAT_NHAU", it.Count(i => i.Code == "ONG_GEN_SAT_NHAU"), 1, 0);
        check("Cảnh báo gắn tuyến vẽ sau (tuyến 2), mức Cảnh báo",
            it.Where(i => i.Code == "ONG_GEN_SAT_NHAU").Select(i => i.RunId * 10 + (int)i.Severity).FirstOrDefault(), 2 * 10 + (int)ImpactSeverity.CanhBao, 0);
        check("Giao 2 ống Ø20 trong sàn 120: 40 ≤ 1/3 h = 40 → không ONG_GEN_GIAO", it.Count(i => i.Code == "ONG_GEN_GIAO"), 0, 0);
        check("Quét kết cấu có kèm kiểm tra khoảng hở", StructuralImpact.Scan(p).Count(i => i.Code == "ONG_GEN_SAT_NHAU"), 1, 0);
        int moved = ConduitSpacing.Spread(p);
        check("Giãn tuyến: dời 1 tuyến", moved, 1, 0);
        check("Giãn tuyến: đoạn giữa tuyến 2 dời ra y = 2 + 0,06 + 0,02 + 0,005", rb.Path[2].Y, 2.085, 1e-9);
        check("Giãn tuyến: tuyến 1 giữ nguyên", ra.Path[1].Y + ra.Path[2].Y, 4, 1e-12);
        check("Giãn tuyến: đoạn kề kéo dài, vẫn trực giao", B(Math.Abs(rb.Path[1].X - 1.5) < 1e-12 && Math.Abs(rb.Path[2].X - 4.5) < 1e-12 && Math.Abs(rb.Path[1].Y - rb.Path[2].Y) < 1e-12), 1, 0);
        check("Sau giãn: hết ONG_GEN_SAT_NHAU", ConduitSpacing.Check(p).Count(i => i.Code == "ONG_GEN_SAT_NHAU"), 0, 0);
        check("Sau giãn: lần 2 không dời thêm", ConduitSpacing.Spread(p), 0, 0);

        var pb = SlabOnly();
        for (int k = 0; k < 5; k++) pb.Mep.Runs.Add(Run(11 + k, 16, (1, 0.5 + 0.2 * k), (5, 0.5 + 0.2 * k)));
        var ib = ConduitSpacing.Check(pb);
        check("5 ống Ø16 song song cách 0,2 m: không sát nhau (hở 184 ≥ 50)", ib.Count(i => i.Code == "ONG_GEN_SAT_NHAU"), 0, 0);
        check("5 ống trong dải 1 m > 4 → đúng 1 cảnh báo ONG_GEN_BO", ib.Count(i => i.Code == "ONG_GEN_BO"), 1, 0);
        pb.Mep.Runs.RemoveAt(4);
        check("4 ống trong dải 1 m → không cảnh báo bó", ConduitSpacing.Check(pb).Count(i => i.Code == "ONG_GEN_BO"), 0, 0);

        var pc = SlabOnly(0.09);
        pc.Mep.Runs.Add(Run(21, 25, (1, 2), (5, 2)));
        pc.Mep.Runs.Add(Run(22, 25, (3, 0.5), (3, 3.5)));
        var ic = ConduitSpacing.Check(pc);
        check("Giao Ø25 + Ø25 = 50 > 1/3·90 = 30 → ONG_GEN_GIAO", ic.Count(i => i.Code == "ONG_GEN_GIAO"), 1, 0);
        check("Điểm giao (3; 2)", ic.Where(i => i.Code == "ONG_GEN_GIAO").Select(i => i.X * 10 + i.Y).FirstOrDefault(), 32, 1e-9);

        // ================= 2. Khí hậu theo tỉnh, phiên bản TCVN 5687 =================
        double Ps(double t) => 610.94 * Math.Exp(17.625 * t / (t + 243.04));
        double Wk(double t, double phi) { double pv = phi / 100 * Ps(t); return 0.622 * pv / (101325 - pv); }
        var dHn = ClimateCooling.Of(null);
        check("Hà Nội: Δt = 37,8 − 25 = 12,8", dHn.DT, 12.8, 1e-9);
        check("Hà Nội: Δw = w(37,8; 53,4 %) − w(25; 60 %)", dHn.DW, Wk(37.8, 53.4) - Wk(25, 60), 1e-12);
        check("Hà Nội: Δw ≈ 0,0103 kg/kg", dHn.DW, 0.0103, 2e-4);
        check("Hiệu chỉnh CLTD = (25,5 − 25) + (37,8 − 8/2 − 29,4) = 4,9", dHn.CltdCorr, 4.9, 1e-9);
        var oTn = new MepStdOptions { Climate = MepStandards.ThaiNguyen };
        check("Thái Nguyên có trong bảng tỉnh", B(MepStandards.Province(oTn).Name == MepStandards.ThaiNguyen && MepStandards.Provinces.Length >= 8), 1, 0);
        check("Thái Nguyên: lấy theo trạm Hà Nội 37,8 °C / 53,4 % (chưa đối chiếu)", MepStandards.Outdoor(oTn).T * 100 + MepStandards.Outdoor(oTn).Phi, 3780 + 53.4, 1e-9);
        check("Thái Nguyên: nguồn ghi 'trạm Hà Nội', đánh dấu (*)", B(!MepStandards.Item("dhkk.t_tn").Verified && MepStandards.Item("dhkk.t_tn").Source.Contains("trạm Hà Nội")), 1, 0);
        check("TP.HCM: Δt = 36,8 − 25 = 11,8", ClimateCooling.Of(new MepStdOptions { Climate = MepStandards.Hcm }).DT, 11.8, 1e-9);
        check("Bắc Ninh → trạm Hà Nội", MepStandards.Outdoor(new MepStdOptions { Climate = "Bắc Ninh (trạm Hà Nội)" }).T, 37.8, 1e-12);
        check("Bình Dương → trạm TP.HCM", MepStandards.Outdoor(new MepStdOptions { Climate = "Bình Dương (trạm TP.HCM)" }).T, 36.8, 1e-12);
        check("Tên lạ → Hà Nội", MepStandards.Outdoor(new MepStdOptions { Climate = "Không rõ" }).T, 37.8, 1e-12);
        oTn.Overrides.Add(new MepStdOverride { Key = "dhkk.t_tn", Value = 36.5 });
        check("Thái Nguyên tuỳ chỉnh 36,5 °C → Δt 11,5", ClimateCooling.Of(oTn).DT, 11.5, 1e-9);
        check("Bản 2010: nguồn TCVN 5687:2010 Phụ lục B", B(MepStandards.ClimateSource(null).Contains("TCVN 5687:2010 Phụ lục B")), 1, 0);
        var o24 = new MepStdOptions { Edition = MepStandards.Ed2024 };
        check("Bản 2024: nguồn TCVN 5687:2024 + QCVN 02:2022/BXD", B(MepStandards.ClimateSource(o24).Contains("TCVN 5687:2024") && MepStandards.ClimateSource(o24).Contains("QCVN 02:2022")), 1, 0);
        check("Bản 2024: thuyết minh ghi nguồn 2024", B(MepStandards.Summary(o24).Any(s => s.Contains("TCVN 5687:2024"))), 1, 0);
        var ci = ClimateCooling.Input(null);
        check("CoolingInput theo khí hậu: Δt 12,8, hiệu chỉnh 4,9", ci.DeltaT + ci.CltdCorrection, 12.8 + 4.9, 1e-9);
        var pr = new Project();
        pr.Stories = new List<Story> { new() { Name = "T1", Elevation = 0, Height = 3.3 } };
        var room = new Room { Id = 1, Story = "T1", Name = "Văn phòng", CeilingHeight = 3, Outline = new() { new[] { 0.0, 0 }, new[] { 4.0, 0 }, new[] { 4.0, 5 }, new[] { 0.0, 5 } } };
        pr.Arch.Rooms.Add(room);
        ci.Roof = true;
        var cr = HvacCoolingDetailed.Room(pr, room, ci);
        check("Tải mái có hiệu chỉnh CLTD: 1,8·20·(22 + 4,9)", cr.QRoof, 1.8 * 20 * 26.9, 1e-6);
        check("Gió lọt hiện: 1,23·(0,5·20·3/3,6)·12,8", cr.QInfS, 1.23 * (0.5 * 20 * 3 * 1000 / 3600) * 12.8, 1e-6);
        var cr0 = HvacCoolingDetailed.Room(pr, room, new CoolingInput { Roof = true });
        check("Mặc định (không theo khí hậu): mái 1,8·20·22", cr0.QRoof, 1.8 * 20 * 22, 1e-9);

        // ================= 3. Chiếu sáng: LPD, phương pháp hệ số sử dụng =================
        check("Loại phòng: 'Phòng ngủ 1' → ngủ 100 lx", LightingDesign.SpaceOf("Phòng ngủ 1").Lux, 100, 0);
        check("Loại phòng: Văn phòng 500 lx, LPD 11 W/m²", LightingDesign.SpaceOf("Văn phòng").Lux * 100 + LightingDesign.SpaceOf("Văn phòng").Lpd, 50011, 0);
        check("Loại phòng: tên lạ → khác", B(LightingDesign.SpaceOf("abc").Code == "khac"), 1, 0);
        check("N = ⌈500·20 / (1800·0,6·0,8)⌉ = ⌈11,57⌉ = 12", LightingDesign.LampCount(500, 20, 1800, 0.6, 0.8), 12, 0);
        check("N đúng số nguyên: 432·20 / 864 = 10 (không làm tròn lên thêm)", LightingDesign.LampCount(432, 20, 1800, 0.6, 0.8), 10, 0);
        check("Quang thông họ chưa khai báo: 10 W × 100 lm/W", LightingDesign.LampLumen(new MepFamily { Power = 10 }), 1000, 1e-9);
        check("Mặc định UF 0,6; MF 0,8", MepStandards.Val("cs.uf") * 10 + MepStandards.Val("cs.mf"), 6.8, 1e-12);
        for (int k = 0; k < 6; k++) MepRouting.PlaceFree(pr, "Đèn tuýp LED 1,2 m", "T1", 0.5 + 0.6 * k, 2.5);
        MepRouting.PlaceFree(pr, "Đèn tuýp LED 1,2 m", "T1", 9, 9);   // ngoài phòng
        var lc = LightingDesign.Check(pr, room);
        check("Phòng: 6 đèn tuýp trong phòng (bỏ đèn ngoài)", lc.Lamps, 6, 0);
        check("LPD = 6·18 / 20 = 5,4 W/m² ≤ 11 → đạt", lc.Lpd * 10 + B(lc.LpdOk), 54 + 1, 1e-9);
        check("E đạt = 6·1800·0,6·0,8 / 20 = 259,2 lx < 500 → chưa đạt", lc.LuxAchieved * 10 + B(lc.LuxOk), 2592, 1e-6);
        check("Số đèn cần theo phương pháp hệ số sử dụng = 12", lc.LampsRequired, 12, 0);
        for (int k = 0; k < 8; k++) MepRouting.PlaceFree(pr, "Đèn tuýp LED 1,2 m", "T1", 0.5 + 0.4 * k, 4);
        var lc2 = LightingDesign.Check(pr, room);
        check("14 đèn: LPD = 252 / 20 = 12,6 > 11 → vượt", lc2.Lpd * 10 + B(lc2.LpdOk), 126, 1e-9);
        pr.Mep.Settings.Standards.Overrides.Add(new MepStdOverride { Key = "cs.lpd.vanphong", Value = 13 });
        check("Tuỳ chỉnh LPD văn phòng 13 → đạt", B(LightingDesign.Check(pr, room).LpdOk), 1, 0);
        var lt = LightingDesign.Table(pr);
        check("Bảng chiếu sáng: 1 phòng", lt.Rows.Count, 1, 0);
        check("Excel điện nước có bảng chiếu sáng", B(MepQuantities.Tables(pr).Any(t => t.Title == LightingDesign.TableTitle)), 1, 0);

        // ================= 4. Ống đứng – ống thoát ra =================
        check("Sức chứa Ø110 ≤ 3 tầng = 240 DFU", DrainStacks.Capacity(110, 2, true), 240, 0);
        check("Sức chứa Ø110 > 3 tầng = 500 DFU", DrainStacks.Capacity(110, 4, true), 500, 0);
        check("Không thông hơi: Ø90 48 × 0,5 = 24", DrainStacks.Capacity(90, 2, false), 24, 1e-12);
        check("Ống đứng rửa 30 DFU, có thông hơi → Ø90", DrainStacks.StackSize(30, 2, false, true), 90, 0);
        check("Ống đứng rửa 30 DFU, không thông hơi → Ø110", DrainStacks.StackSize(30, 2, false, false), 110, 0);
        check("Ống đứng xí 10 DFU → tối thiểu Ø110", DrainStacks.StackSize(10, 2, true, true), 110, 0);
        var bd = DrainStacks.BuildingDrain(30, false);
        check("Ống thoát ra 30 DFU (không xí) → Ø90, i 2 %, sức chứa 42", bd.Od * 1000 + bd.Slope * 100 + bd.Cap / 100, 90000 + 2 + 0.42, 1e-9);
        check("Ống thoát ra có xí → Ø110 (216 DFU ở 2 %)", DrainStacks.BuildingDrain(30, true).Cap, 216, 0);
        var pd = new Project();
        pd.Stories = new List<Story> { new() { Name = "T1", Elevation = 0, Height = 3.3 }, new() { Name = "T2", Elevation = 3.3, Height = 3.3 }, new() { Name = "T3", Elevation = 6.6, Height = 3.3 } };
        foreach (var st in new[] { "T1", "T2", "T3" })
        {
            MepRouting.PlaceFree(pd, "Bồn cầu", st, 1, 1);
            MepRouting.PlaceFree(pd, "Lavabo", st, 2, 1);
            MepRouting.PlaceFree(pd, "Sen tắm", st, 3, 1);
        }
        var lib = pd.Mep.Library;
        double dWc = lib.Find("Bồn cầu").DrainUnits, dWash = lib.Find("Lavabo").DrainUnits + lib.Find("Sen tắm").DrainUnits;
        var dr = DrainStacks.Build(pd);
        check("2 ống đứng (xí, rửa)", dr.Stacks.Count, 2, 0);
        var sWc = dr.Stacks.First(s => s.Wc); var sWa = dr.Stacks.First(s => !s.Wc);
        check("Ống đứng xí: cộng dồn từ trên – tầng T3 trước", B(sWc.Levels[0].Story == "T3" && Math.Abs(sWc.Levels[0].Acc - dWc) < 1e-9), 1, 0);
        check("Ống đứng xí: chân ống 3·DFU xí", sWc.Total, 3 * dWc, 1e-9);
        check("Ống đứng xí: 3 khoảng nhánh, Ø110, đạt", sWc.Intervals * 1000 + sWc.Od + B(sWc.Ok) * 0.1, 3000 + 110 + 0.1, 1e-9);
        check("Ống đứng rửa: chân 3·(lavabo + sen)", sWa.Total, 3 * dWash, 1e-9);
        check("Ống đứng rửa: Ø theo bảng", sWa.Od, DrainStacks.StackSize(3 * dWash, 3, false, true), 0);
        check("Ống thoát ra: tổng DFU, Ø110 (có xí)", dr.TotalDfu * 1000 + dr.DrainOd, 3 * (dWc + dWash) * 1000 + 110, 1e-6);
        pd.Mep.Settings.Standards.StackVented = false;
        check("Không thông hơi: sức chứa ống xí Ø110 còn 120", DrainStacks.Build(pd).Stacks.First(s => s.Wc).Cap, 120, 1e-9);
        pd.Mep.Settings.Standards.StackVented = true;

        // ================= 5. Bể tự hoại 3 ngăn =================
        var sp = SepticTank.Compute(6);
        double wn = 1 * 150 * 6 / 1000.0, wc = 0.6 * 180 * (100 - 95) * 0.7 * 1.2 * 6 / ((100 - 90) * 1000.0);
        check("Wn = 1·150·6/1000 = 0,9 m³", sp.Wn, wn, 1e-12);
        check("Wc = 0,6·180·5·0,7·1,2·6 / (10·1000) = 0,27216 m³", sp.Wc, wc, 1e-12);
        check("W = Wn + Wc = 1,17216 m³", sp.W, 1.17216, 1e-9);
        check("B = max(0,8; ⌈√(0,78144/2)⌉0,05 = 0,65) = 0,8 m", sp.B, 0.8, 1e-9);
        check("L = ⌈0,9768⌉0,05 = 1,0 m; ngăn 0,5 + 0,25 + 0,25", sp.L * 1000 + sp.L1 * 100 + sp.L2 * 10 + sp.L3, 1000 + 50 + 2.5 + 0.25, 1e-9);
        check("Dung tích thực B·L·H = 0,8·1,0·1,5 = 1,2 m³ ≥ W", sp.WReal * 10 + B(sp.WReal >= sp.W), 12 + 1, 1e-9);
        var sp20 = SepticTank.Compute(20);
        check("N = 20: W = 20·(0,15 + 0,04536)", sp20.W, 20 * (0.15 + 0.04536), 1e-9);
        check("N = 20: dung tích thực ≥ W", B(sp20.WReal >= sp20.W - 1e-9 && sp20.B * sp20.L * sp20.H >= sp20.W - 1e-9), 1, 0);
        var fig = SepticTank.Sketch(sp);
        string tx = fig.W.ToDxfStringDec();
        check("Hình bể tự hoại: tiêu đề + 3 ngăn + ống vào / ra / thông hơi",
            B(tx.Contains(SepticTank.Title) && tx.Contains("NGĂN CHỨA") && tx.Contains("NGĂN LẮNG") && tx.Contains("NGĂN LỌC") && tx.Contains("ỐNG VÀO") && tx.Contains("ỐNG RA") && tx.Contains("ỐNG THÔNG HƠI")), 1, 0);
        check("Hình bể tự hoại: ghi kích thước ngăn 500 / 250", B(tx.Contains("500") && tx.Contains("250")), 1, 0);
        fig.W.Save(Path.Combine(dir, "BeTuHoai_3Ngan.dxf"));
        check("Hình bể tự hoại là sơ đồ tỷ lệ 1", B(MepRound5.IsSchematic(SepticTank.Title) && MepRound4.IsSchematic(SepticTank.Title)), 1, 0);
        check("Chưa khai báo số người → không bảng / hình bể tự hoại", B(!MepQuantities.Tables(pd).Any(t => t.Title == "Bể tự hoại 3 ngăn") && !MepDrawings.Figures(pd).Any(f => f.Name == SepticTank.Title)), 1, 0);
        pd.Mep.Settings.Water.Occupants = 6;
        var tabs = MepQuantities.Tables(pd);
        check("Có số người: Excel có bảng ống đứng + bể tự hoại", B(tabs.Any(t => t.Title == DrainStacks.TableTitle) && tabs.Any(t => t.Title == "Bể tự hoại 3 ngăn")), 1, 0);
        check("Có số người: bộ bản vẽ điện nước có hình bể tự hoại", B(MepDrawings.Figures(pd).Any(f => f.Name == SepticTank.Title)), 1, 0);
    }
}
