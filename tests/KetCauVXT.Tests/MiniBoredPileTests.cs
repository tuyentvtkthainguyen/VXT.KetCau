using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Kiểm chứng cọc khoan nhồi mini (TCVN 10304:2014: Bảng 3, Bảng 7, Phụ lục G.3.2, mục 7.1.9, Phụ lục A, lún khối quy ước)
/// và các trường hợp móng còn lại: chọc thủng tại bậc móng, cột chọc thủng móng băng, nhổ khối nhóm cọc, tự chuyển phản lực giằng sang móng.
/// Kỳ vọng tính tay (dạng đóng).</summary>
public static class MiniBoredPileTests
{
    private static double B(bool x) => x ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        const double rbt = 1050;

        // ---- 1. Chọc thủng tại bậc móng ----
        var sp1 = Punching.StepPunching(800, 200, 1.0, 1.0, 0.3, 2.0, 2.0, rbt);
        check("Bậc móng: F = N − pnet·(Lb + 2h0)(Bb + 2h0)", sp1.F, 800 - 200 * 1.6 * 1.6, 1e-9);
        check("Bậc móng: u = 2(Lb + Bb + 2h0)", sp1.U, 2 * (1 + 1 + 0.6), 1e-9);
        check("Bậc móng: Fb,u = Rbt·u·h0", sp1.Fu, rbt * 5.2 * 0.3, 1e-9);
        check("Bậc móng: đáy tháp vượt đế móng → F = 0", Punching.StepPunching(800, 200, 1.8, 1.8, 0.3, 2.0, 2.0, rbt).F, 0, 0);
        var s = new DetailSettings();
        var fs = new ManualFooting { N = 600, ML = 0, MB = 0, L = 2.0, B = 2.0, H = 0.6, Hb = 0.3, FootShape = "Bậc", StepL = 1.0, StepB = 1.0, R = 200, Df = 1.5 };
        var rfs = fs.Run(s);
        var kStep = rfs.Checks.FirstOrDefault(k => k.Name.StartsWith("Chọc thủng tại bậc móng"));
        double h0b = 0.3 - 0.05 - 0.012;
        check("Móng bậc: có kiểm tra chọc thủng tại bậc", B(kStep != null), 1, 0);
        check("Móng bậc: F tại bậc = N − N/A·(1 + 2h0b)²", kStep?.Demand ?? -1, 600 - 600 / 4.0 * Math.Pow(1 + 2 * h0b, 2), 1e-9);
        check("Móng bậc: bê tông = L·B·Hb + Lb·Bb·(H − Hb)", rfs.ConcreteVolume, 2 * 2 * 0.3 + 1 * 1 * 0.3, 1e-9);
        check("Móng bậc: ván khuôn = 2(L + B)·Hb + 2(Lb + Bb)·(H − Hb)", rfs.FormworkArea, 2 * 4 * 0.3 + 2 * 2 * 0.3, 1e-9);
        var fdS = rfs.Source as FootingDesign;
        check("Móng bậc: kích thước bậc trên lưu cho bản vẽ", fdS?.StepL ?? 0, 1.0, 1e-9);
        check("Móng bậc: mặt cắt vẽ được", B(fs.Draw(rfs, s).Count >= 1), 1, 0);

        // ---- 2. Cột chọc thủng móng băng ----
        var pc = Punching.StripColumn(600, 0, 0.22, 0.22, 0.5, 2.0, 5, 5, 100, rbt);
        check("Móng băng: u đường bao kín = 2(0,72 + 0,72)", pc.U, 2 * (0.72 + 0.72), 1e-9);
        check("Móng băng: F = N − pnet·(bc + 2h0)²", pc.F, 600 - 100 * 1.22 * 1.22, 1e-9);
        check("Móng băng: tỉ số = F/(Rbt·u·h0)", pc.Ratio, (600 - 100 * 1.22 * 1.22) / (rbt * 2.88 * 0.5), 1e-9);
        check("Móng băng hẹp (b = 0,6 m): đường bao bị cắt 2 cạnh dọc → u = 2·0,6", Punching.StripColumn(600, 0, 0.22, 0.22, 0.5, 0.6, 5, 5, 100, rbt).U, 1.2, 1e-9);
        check("Cột ở đầu băng (cách đầu 0,15 m): u = 2·(0,15 + 0,36) + 0,72", Punching.StripColumn(600, 0, 0.22, 0.22, 0.5, 2.0, 0.15, 5, 100, rbt).U, 2 * 0.51 + 0.72, 1e-9);
        var ms = new ManualStrip();
        var rms = ms.Run(s);
        check("Móng băng mẫu: có kiểm tra chọc thủng cho từng cột", rms.Checks.Count(k => k.Name.Contains("chọc thủng móng băng")), ms.Columns.Count, 0);

        // ---- 3. Nhổ khối nhóm cọc ----
        var bu = BoredPile.BlockUplift(2, 1, 10, 18, 300, 50);
        check("Nhổ khối: W đất = Lb·Bb·L·γ", bu.W, 2 * 1 * 10 * 18, 1e-9);
        check("Nhổ khối: ma sát khối = 0,8·2(Lb + Bb)·Σfi·li/1,4", bu.Fs, 0.8 * 2 * 3 * 300 / 1.4, 1e-9);
        check("Nhổ khối: [Pk,khối] = 0,9(W + Gđài) + ma sát", bu.Cap, 0.9 * (360 + 50) + 0.8 * 6 * 300 / 1.4, 1e-9);
        var capU = new ManualPileCap { N = 600, Mx = 0, My = 700, Pa = 600, Pt = 300, NumPiles = 2, PileBarN = 4, PileBarD = 16, SkinFriction = 300 };
        var rcu = capU.Run(s);
        check("Đài nhổ có Σfi·li: có kiểm tra nhổ khối nhóm cọc", B(rcu.Checks.Any(k => k.Name.StartsWith("Nhổ khối nhóm cọc"))), 1, 0);

        // ---- 4. Tự chuyển phản lực giằng sang móng ----
        var dp = new DetailingProject();
        var m1 = new ManualFooting { Name = "M1", N = 600, ML = 0, MB = 0, BeamReaction = 50 };
        var m2 = new ManualFooting { Name = "M2", N = 600, ML = 0, MB = 0 };
        var m3 = new ManualFooting { Name = "M3", N = 600, ML = 0, MB = 0 };
        dp.Footings.AddRange(new[] { m1, m2, m3 });
        dp.Beams.Add(new ManualBeam { Name = "GM1", Position = "Dầm móng (giằng móng)", GroundCalc = ManualBeam.GroundTie, Spans = "4", B = 0.22, H = 0.40, WallH = 0, SupportFootings = "M1; M2" });
        dp.Beams.Add(new ManualBeam { Name = "GM2", Position = "Dầm móng (giằng móng)", GroundCalc = ManualBeam.GroundTie, Spans = "3", B = 0.22, H = 0.40, WallH = 0, SupportFootings = "M2; M3" });
        var res = dp.RunAll();
        double q = 1.1 * 25 * 0.22 * 0.40;
        check("Giằng → M1: R = qL/2 (dầm 1 nhịp 4 m)", m1.AutoBeamReaction, q * 4 / 2, 1e-9);
        check("Giằng → M2: cộng hai giằng qL1/2 + qL2/2", m2.AutoBeamReaction, q * 4 / 2 + q * 3 / 2, 1e-9);
        check("Giằng → M3: R = q·3/2", m3.AutoBeamReaction, q * 3 / 2, 1e-9);
        var r1 = res.First(x => ReferenceEquals(x.Member, m1)).Result;
        check("M1: N tính = N + R tự động (không cộng ô nhập tay 50 kN)", (r1.Source as FootingDesign)?.N ?? -1, 600 + q * 2, 1e-9);
        check("M1: thông báo bỏ qua ô nhập tay", B(r1.Messages.Any(mm => mm.Contains("KHÔNG cộng thêm"))), 1, 0);
        check("M1: thuyết minh ghi dầm và gối", B(r1.Calc.Any(c => c.Contains("GM1 gối 1"))), 1, 0);
        check("Tên móng ở gối: tách theo ';'", TieReaction.SupportNames("M1; ; M3").Count, 3, 0);

        // ---- 5. Bảng tra TCVN 10304:2014 ----
        check("Bảng 3: fi(z = 3; IL 0,4) = 25 kPa", BoredPile.FiTable(3, BoredPile.KindClay, 0.4), 25, 1e-9);
        check("Bảng 3: fi(z = 1; cát thô/vừa) = 35 kPa", BoredPile.FiTable(1, BoredPile.KindSandCoarse, 0), 35, 1e-9);
        check("Bảng 3: nội suy z = 2,5; IL 0,3 → 32,5", BoredPile.FiTable(2.5, BoredPile.KindClay, 0.3), 32.5, 1e-9);
        check("Bảng 3: nội suy IL 0,45 tại z = 5 → 26,5", BoredPile.FiTable(5, BoredPile.KindClay, 0.45), 26.5, 1e-9);
        check("Bảng 3: đất chảy IL > 1 → fi = 0", BoredPile.FiTable(5, BoredPile.KindClay, 1.2), 0, 0);
        check("Bảng 3: z > 35 m lấy z = 35 (cát mịn) = 70", BoredPile.FiTable(40, BoredPile.KindSandFine, 0), 70, 1e-9);
        check("Bảng 7: qb(z = 4; IL 0,4) = 450 kPa", BoredPile.QbClayTable(4, 0.4), 450, 1e-9);
        check("Bảng 7: qb(z = 10; IL 0) = 1350 kPa", BoredPile.QbClayTable(10, 0), 1350, 1e-9);
        check("Bảng 7: nội suy IL 0,25 tại z = 10 → 1000", BoredPile.QbClayTable(10, 0.25), 1000, 1e-9);
        check("Bảng 7: IL > 0,6 lấy cột 0,6", BoredPile.QbClayTable(10, 0.8), 600, 1e-9);
        check("γcf: khoan dung dịch bentonite = 0,6", BoredPile.GammaCf(BoredPile.DrillSlurry), 0.6, 1e-9);
        check("γcf: khoan khô = 0,7", BoredPile.GammaCf(BoredPile.DrillDry), 0.7, 1e-9);

        // ---- 6. Sức chịu tải theo chỉ tiêu cơ lý ----
        var clay = new List<PileSoilLayer> { new() { Name = "Sét", Thickness = 10, Kind = BoredPile.KindClay, IL = 0.4, Gamma = 18 } };
        var c1 = BoredPile.Capacity(clay, 0.4, 0, 4, BoredPile.MethodTable, BoredPile.DrillSlurry);
        double u4 = Math.PI * 0.4, ab4 = Math.PI * 0.16 / 4;
        check("Cơ lý: chia thân 4 m thành 2 đoạn ≤ 2 m", c1.Rows.Count, 2, 0);
        check("Cơ lý: ma sát u·γcf·Σfi·li = π·0,4·0,6·(15·2 + 25·2)", c1.ShaftResistance, u4 * 0.6 * (15 * 2 + 25 * 2), 1e-9);
        check("Cơ lý: mũi qb·Ab = 450·Ab", c1.TipResistance, 450 * ab4, 1e-9);
        check("Cơ lý: Rc,u", c1.Rcu, u4 * 0.6 * 80 + 450 * ab4, 1e-9);
        check("Cơ lý: [P]đ = γ0·Rc,u/(γn·γk) = Rc,u/1,4", c1.Psoil, (u4 * 0.6 * 80 + 450 * ab4) / 1.4, 1e-9);
        check("Cơ lý: Σfi·li = 80 kN/m (cọc chịu nhổ)", c1.SumFiLi, 80, 1e-9);
        var c2 = BoredPile.Capacity(clay, 0.4, 1.5, 3, BoredPile.MethodTable, BoredPile.DrillSlurry);
        check("Cơ lý: đầu cọc −1,5 m: fi(2,25) = 22; fi(3,75) = 26,5", c2.ShaftResistance, u4 * 0.6 * (22 * 1.5 + 26.5 * 1.5), 1e-9);
        var lay2 = new List<PileSoilLayer>
        {
            new() { Name = "Sét dẻo", Thickness = 2, Kind = BoredPile.KindClay, IL = 0.6 },
            new() { Name = "Cát vừa", Thickness = 10, Kind = BoredPile.KindSandCoarse, NSpt = 20 },
        };
        var c3 = BoredPile.Capacity(lay2, 0.3, 0, 6, BoredPile.MethodTable, BoredPile.DrillCasing);
        double u3 = Math.PI * 0.3, ab3 = Math.PI * 0.09 / 4;
        check("Cơ lý 2 lớp (ống vách γcf 0,7): ma sát π·0,3·0,7·(8·2 + 48·2 + 56·2)", c3.ShaftResistance, u3 * 0.7 * (8 * 2 + 48 * 2 + 56 * 2), 1e-9);
        check("Cơ lý: mũi trong cát dùng qb = 150·N = 3000 kPa", c3.Qb, 3000, 1e-9);
        check("Cơ lý: mũi cát qb·Ab", c3.TipResistance, 3000 * ab3, 1e-9);
        var lay3 = new List<PileSoilLayer> { new() { Name = "Sét", Thickness = 10, Kind = BoredPile.KindClay, IL = 0.4, QbUser = 999 } };
        check("Cơ lý: qb nhập tay được ưu tiên", BoredPile.Capacity(lay3, 0.4, 0, 4, BoredPile.MethodTable, BoredPile.DrillSlurry).Qb, 999, 0);
        check("γcf nhập tay thay công nghệ khoan", BoredPile.Capacity(clay, 0.4, 0, 4, BoredPile.MethodTable, BoredPile.DrillSlurry, 0.8).ShaftResistance, u4 * 0.8 * 80, 1e-9);

        // ---- 7. Sức chịu tải theo SPT (Phụ lục G.3.2) ----
        var sand = new List<PileSoilLayer> { new() { Name = "Cát", Thickness = 20, Kind = BoredPile.KindSandCoarse, NSpt = 15, Gamma = 19 } };
        var c4 = BoredPile.Capacity(sand, 0.5, 0, 8, BoredPile.MethodSpt, BoredPile.DrillSlurry);
        double u5 = Math.PI * 0.5, ab5 = Math.PI * 0.25 / 4;
        check("SPT cát: fs = 10N/3 = 50 kPa → ma sát u·0,6·50·8", c4.ShaftResistance, u5 * 0.6 * 50 * 8, 1e-9);
        check("SPT cát: qb = 150·N = 2250 kPa", c4.TipResistance, 2250 * ab5, 1e-9);
        var soft = new List<PileSoilLayer> { new() { Name = "Sét", Thickness = 10, Kind = BoredPile.KindClay, Cu = 40, Gamma = 18 } };
        var c5 = BoredPile.Capacity(soft, 0.4, 0, 2, BoredPile.MethodSpt, BoredPile.DrillDry);
        double psi = 40 / 18.0, ap = 0.5 * Math.Pow(psi, -0.25);
        check("SPT sét: αp = 0,5·ψ^−0,25 (ψ = cu/σ'v = 40/18)", BoredPile.AlphaP(40, 18), ap, 1e-12);
        check("SPT sét: fc = αp·cu → ma sát u·0,7·fc·2", c5.ShaftResistance, u4 * 0.7 * ap * 40 * 2, 1e-9);
        check("SPT sét: qb = 9·cu = 360 kPa", c5.Qb, 360, 1e-9);
        check("SPT sét: cu = 6,25·N khi không nhập cu", BoredPile.Capacity(new List<PileSoilLayer> { new() { Kind = BoredPile.KindClay, Thickness = 10, NSpt = 8 } }, 0.4, 0, 2, BoredPile.MethodSpt, BoredPile.DrillDry).Qb, 9 * 6.25 * 8, 1e-9);

        // ---- 8. Vật liệu, thép, tải ngang ----
        double as616 = 6 * Math.PI * 0.016 * 0.016 / 4, rb = 14500, rsc = 350000;
        check("Pvl = 0,85·0,7·Rb·Ab + Rsc·As (D400, 6Ø16)", BoredPile.MaterialCapacity(0.4, rb, rsc, as616), 0.85 * 0.7 * rb * ab4 + rsc * as616, 1e-6);
        check("Pvl khoan khô: γ'cb = 1", BoredPile.MaterialCapacity(0.4, rb, rsc, as616, false), 0.85 * rb * ab4 + rsc * as616, 1e-6);
        check("μ = As/Ab (%)", BoredPile.SteelRatio(0.4, as616), as616 / ab4 * 100, 1e-9);
        check("Mu ≈ Rs·As·2rs/π", BoredPile.MomentCapacity(350000, as616, 0.142), 350000 * as616 * 0.284 / Math.PI, 1e-9);
        double I4 = Math.PI * Math.Pow(0.4, 4) / 64, eb = 30e6;
        double alpha = Math.Pow(5000 * 1.1 / (3 * eb * I4), 0.2);
        var lat = BoredPile.Lateral(20, 0.4, 15, 5000, eb);
        check("Tải ngang: bp = 1,5d + 0,5", lat.Bp, 1.1, 1e-12);
        check("Tải ngang: αbd = (K·bp/(γc·Eb·I))^(1/5)", lat.AlphaBd, alpha, 1e-12);
        check("Tải ngang: M ngàm = H·B0/(αbd·C0)", lat.M0, 20 * 1.621 / (alpha * 1.751), 1e-9);
        check("Tải ngang: y0 = H·(A0 − B0²/C0)/(αbd³·Eb·I)", lat.Y0, 20 * (2.441 - 1.621 * 1.621 / 1.751) / (Math.Pow(alpha, 3) * eb * I4), 1e-12);
        check("Tải ngang: cọc dài αbd·L ≥ 4", B(lat.Long), B(alpha * 15 >= 4), 0);
        check("Tải ngang: d > 0,8 m → bp = d + 1", BoredPile.Lateral(20, 1.0, 20, 5000, eb).Bp, 2.0, 1e-12);

        // ---- 9. Lồng thép, lún khối quy ước ----
        check("Lồng thép L = 15 m: max(L/3; 6) = 6 m", BoredPile.CageLength(15, 0, false), 6, 1e-12);
        check("Lồng thép L = 30 m: L/3 = 10 m", BoredPile.CageLength(30, 0, false), 10, 1e-12);
        check("Lồng thép theo tải ngang: 4/αbd + 1 = 9 m (αbd = 0,5)", BoredPile.CageLength(15, 0.5, false), 9, 1e-12);
        check("Lồng thép cọc chịu nhổ: toàn chiều dài", BoredPile.CageLength(15, 0, true), 15, 1e-12);
        check("Lồng thép không dài hơn cọc", BoredPile.CageLength(5, 0, false), 5, 1e-12);
        var homo = new List<PileSoilLayer> { new() { Name = "Đồng nhất", Thickness = 100, Kind = BoredPile.KindClay, Gamma = 20, E = 20000 } };
        var st = BoredPile.Settlement(homo, 1, 2, 10, 11, 1000, 20);
        double bm = 1 + 20 * Math.Tan(5 * Math.PI / 180), lm = 2 + 20 * Math.Tan(5 * Math.PI / 180), p0 = 1000 / (bm * lm);
        check("Lún: Bm = Bg + 2L·tan(φ/4)", st.Bm, bm, 1e-12);
        check("Lún: Lm", st.Lm, lm, 1e-12);
        check("Lún: p0 = p − σbt = Ntc/(Bm·Lm) (γtb = γđất = 20)", st.P0, p0, 1e-9);
        double hh = 0.2 * bm, z = 0, sExp = 0;
        for (int i = 0; i < 400; i++)
        {
            double zm = z + hh / 2, sz = p0 * bm * lm / ((bm + zm) * (lm + zm));
            if (sz <= 0.2 * 20 * (11 + zm)) break;
            sExp += 0.8 * sz * hh / 20000; z += hh;
        }
        check("Lún: S = Σ0,8·σz·h/E (σz phân bố 2:1, dừng σz ≤ 0,2σbt)", st.S, sExp, 1e-12);
        check("Lún: S > 0", B(st.S > 0), 1, 0);

        // ---- 10. Đài cọc khoan nhồi mini (Triển khai hồ sơ) ----
        var cap = new ManualPileCap
        {
            Name = "ĐKN1", N = 900, Mx = 20, My = 30, PileD = 0.4, PileLength = 15, Df = 1.5, PileType = ManualPileCap.TypeMiniBored,
            CapacityFromSoil = true, PileBarN = 6, PileBarD = 16, HLat = 30
        };
        var rc = cap.Run(s);
        var br = cap.BoredResult;
        check("Đài KN: có kết quả sức chịu tải theo đất", B(br != null), 1, 0);
        check("Đài KN: [P] = ⌊min([P]đ; Pvl)⌋", cap.PaUsed, br == null ? -1 : Math.Floor(Math.Min(br.Psoil, br.Pvl)), 0);
        check("Đài KN: Pvl = 0,85·0,7·Rb·Ab + Rsc·As (B25, 6Ø16)", br?.Pvl ?? -1, 0.85 * 0.7 * 14500 * ab4 + 350000 * as616, 1e-6);
        int nPile = cap.Piles.Count;
        check("Đài KN: thép dọc cọc = 6 thanh × số cọc", rc.Bars.Where(b => b.Role == "Cọc – thép dọc").Sum(b => b.Count), 6 * nPile, 0);
        check("Đài KN: có đai xoắn và đai gia cường", B(rc.Bars.Any(b => b.Role == "Cọc – đai xoắn") && rc.Bars.Any(b => b.Role == "Cọc – đai gia cường")), 1, 0);
        var vConc = rc.Extra.FirstOrDefault(e => e.Item.StartsWith("Bê tông cọc khoan nhồi"));
        check("Đài KN: bê tông cọc = n·Ab·L·1,15", vConc.Qty, nPile * ab4 * 15 * 1.15, 1e-9);
        check("Đài KN: khoan tạo lỗ = n·(L + Df)", rc.Extra.FirstOrDefault(e => e.Item.StartsWith("Khoan tạo lỗ")).Qty, nPile * 16.5, 1e-9);
        check("Đài KN: kiểm tra hàm lượng thép μ ≥ 0,4 %", B(rc.Checks.Any(k => k.Name.StartsWith("Hàm lượng thép dọc"))), 1, 0);
        check("Đài KN: kiểm tra tải ngang M và y0", rc.Checks.Count(k => k.Name.StartsWith("Cọc chịu tải ngang")), 2, 0);
        check("Đài KN: tải ngang chia đều H/n", cap.LateralResult?.M0 ?? -1, (30.0 / nPile) * 1.621 / (cap.LateralResult?.AlphaBd ?? 1) / 1.751, 1e-9);
        check("Đài KN: có kiểm tra lún khối quy ước", B(rc.Checks.Any(k => k.Name.StartsWith("Lún khối móng quy ước"))), 1, 0);
        check("Đài KN: lồng thép ≥ 6 m và ≤ L", B(cap.CageLenUsed >= 6 - 1e-9 && cap.CageLenUsed <= 15 + 1e-9), 1, 0);
        check("Đài KN: ghi chú thử tải tĩnh", B(rc.Messages.Any(m => m.Contains("Thử tải tĩnh"))), 1, 0);
        var figs = cap.Draw(rc, s);
        check("Đài KN: bản vẽ ≥ 5 hình (mặt bằng, mặt cắt đài, cọc, mặt cắt cọc, trụ địa chất)", B(figs.Count >= 5), 1, 0);
        check("Đài KN: có chi tiết cọc khoan nhồi", B(figs.Any(f => f.W.ToDxfStringDec().Contains("CHI TIẾT CỌC KHOAN NHỒI"))), 1, 0);
        check("Đài KN: trụ địa chất ghi mũi cọc", B(figs.Any(f => f.W.ToDxfStringDec().Contains("MŨI CỌC"))), 1, 0);
        var capN = new ManualPileCap { N = 900, PileD = 0.4, PileType = ManualPileCap.TypeMiniBored, Pa = 5000, PileBarN = 6, PileBarD = 16 };
        var rcn = capN.Run(s);
        check("Đài KN [P] nhập tay > Pvl → không đạt kiểm tra vật liệu", B(rcn.Checks.Any(k => k.Name.StartsWith("[P] nhập tay ≤ Pvl") && !k.Ok)), 1, 0);
        check("Cọc ép đúc sẵn: không thêm thép lồng cọc khoan nhồi", B(new ManualPileCap().Run(s).Bars.All(b => !b.Role.StartsWith("Cọc –"))), 1, 0);
        check("Thử tải: 1 % của 150 cọc → 2 cọc", B(BoredPile.ConstructionNotes(150, 0.4).Any(n => n.Contains("= 2 cọc"))), 1, 0);

        // ---- 11. Chế độ Thiết kế: Pvl cọc khoan nhồi kể γcb·γ'cb ----
        var bh = new Borehole { Name = "HK", WaterDepth = -1, Layers = { new SoilLayer { Name = "Cát", Kind = SoilKind.SiltyClay, Top = 0, Bottom = 30, Gamma = 19, N = 20 } } };
        var pr = GeotechCalc.PileCapacity(bh, 0.4, true, 12, 1.5, true, 2.5, as616);
        check("Thiết kế (hố khoan): Pvl khoan nhồi = 0,85·0,7·Rb·Ab + Rsc·As", pr.Pvl, 0.85 * 0.7 * 14500 * ab4 + 350000 * as616, 1e-6);

        // ---- xuất DXF chi tiết cọc ----
        var dir = Path.Combine(outDir, "TrienKhai", "CocKhoanNhoi");
        Directory.CreateDirectory(dir);
        for (int i = 0; i < figs.Count; i++) figs[i].W.Save(Path.Combine(dir, "CocKhoanNhoi_" + (i + 1) + ".dxf"));
        check("Xuất DXF chi tiết cọc khoan nhồi", B(File.Exists(Path.Combine(dir, "CocKhoanNhoi_3.dxf"))), 1, 0);
    }
}
