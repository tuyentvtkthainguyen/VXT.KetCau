using System.Globalization;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Trích chi tiết trên mặt bằng, mặt cắt mọi nhịp dầm, shop drawing (chiều dài cắt, cắt từ cây 11,7 m, vị trí nối),
/// điểm cắt / nối người dùng điều chỉnh + cảnh báo vùng nguy hiểm, nguyên tắc bố trí thép sàn + thứ tự đặt thép.</summary>
public static class ShopCutTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var s = new DetailSettings();
        var conc = Material.Concrete("B25"); var main = Material.Rebar("CB400-V");

        // ---- 1. chiều dài cắt (trừ uốn 2d / góc 90°, đai −12d) ----
        var u = Dt.Hooked("U", 16, 2, 7.0, 0.3, 0.3);
        check("Shop: U Ø16 7000 + 2×300 – L cắt = R10(7,6 − 4·0,016)", ShopDrawing.CutLength(u), Dt.R10(7.6 - 4 * 0.016), 1e-9);
        var st = Dt.Stirrup("Đai", 6, 10, 0.17, 0.35, 0.1);
        check("Shop: đai Ø6 – L cắt = R10(L − 12d)", ShopDrawing.CutLength(st), Dt.R10(st.Length - 12 * 0.006), 1e-9);
        check("Shop: thanh thẳng không trừ uốn", ShopDrawing.CutLength(Dt.Straight("T", 12, 1, 5.0)), 5.0, 1e-9);

        // ---- 2. cắt từ cây 11,7 m ----
        var p10 = ShopDrawing.PlanStock(3.0, 10, 11.7, 0);
        check("Cắt cây: 10 thanh 3,0 m → 3 đoạn/cây → 4 cây", p10.StockBars, 4, 0);
        check("Cắt cây: hao hụt = 1 − 30/46,8", p10.WastePct, Math.Round((1 - 30 / 46.8) * 100, 1), 1e-9);
        double lap16 = RebarDetailing.LapLength(conc, main, 16);
        var p14 = ShopDrawing.PlanStock(14.2, 3, 11.7, lap16);
        int np = (int)Math.Ceiling((14.2 - 11.7) / (11.7 - lap16) - 1e-9) + 1;
        double last = Dt.R10(14.2 + (np - 1) * lap16 - (np - 1) * 11.7);
        int perLast = (int)Math.Floor(11.7 / last + 1e-9);
        check("Cắt cây: 3Ø16 L = 14,2 m > 11,7 → số đoạn", p14.Pieces, np, 0);
        check("Cắt cây: đoạn cuối = L + ll − 11,7", p14.PieceLengths[^1], last, 1e-9);
        check("Cắt cây: số cây = 3·(np − 1) + ⌈3/⌊11,7/đoạn cuối⌋⌉", p14.StockBars, 3 * (np - 1) + (int)Math.Ceiling(3.0 / perLast - 1e-9), 0);
        check("Cắt cây: nối chồng ll = LapLength(B25, CB400-V, 16)", p14.Lap, lap16, 1e-9);

        // ---- 3. dầm dài: vị trí nối tự động trong vùng cho phép, đoạn cắt ≤ cây ----
        var bl = new ManualBeam { Name = "DL", Spans = "4,2; 4,2; 4,2; 4,2", ShopOut = true };
        var rl = bl.Run(s);
        var g = BarCutCheck.GeoOf(bl);
        var spl = ShopDrawing.BeamSplices(bl, rl, s);
        check("Dầm 16,8 m: có thanh chạy suốt trên + dưới cần nối", spl.Count(x => x.A.Count > 0), 2, 0);
        check("Nối tự động thép dưới: ngoài 1/3 giữa nhịp", B(spl.Where(x => !x.Bar.IsTop).SelectMany(x => x.A.Concat(x.B)).All(x => BarCutCheck.SpliceDanger(g, x, false, false).Length == 0)), 1, 0);
        check("Nối tự động thép trên: ngoài L0/4 từ mép gối", B(spl.Where(x => x.Bar.IsTop).SelectMany(x => x.A.Concat(x.B)).All(x => BarCutCheck.SpliceDanger(g, x, true, false).Length == 0)), 1, 0);
        var rows = ShopDrawing.Rows(bl, rl, s);
        check("Shop dầm dài: mọi đoạn cắt ≤ 11,7 m", B(rows.All(r => r.CutLength <= s.StockLength + 1e-9)), 1, 0);
        check("Shop dầm dài: thanh dưới tách đoạn nhóm A và B (so le)", B(rows.Any(r => r.Role.StartsWith("Dưới") && r.Sub.StartsWith("A")) && rows.Any(r => r.Role.StartsWith("Dưới") && r.Sub.StartsWith("B"))), 1, 0);
        var lf = bl.Draw(rl, s);
        var plain = new ManualBeam { Name = "DL", Spans = "4,2; 4,2; 4,2; 4,2" };
        int nPlain = plain.Draw(plain.Run(s), s).Count;
        check("Shop drawing dầm: thêm 2 hình (gia công + bảng cắt)", lf.Count - nPlain, 2, 0);
        check("Shop drawing dầm: tiêu đề SHOP DRAWING THÉP DẦM DL", B(lf.Any(f => f.W.Texts().Any(t => t.StartsWith("SHOP DRAWING THÉP DẦM DL")))), 1, 0);
        check("Bảng cắt thép có cột CÂY 11,7 m", B(lf.Any(f => f.W.Texts().Any(t => t.StartsWith("CÂY 11,7")))), 1, 0);

        // ---- 4. mặt cắt tại mọi nhịp: hai gối + giữa nhịp ----
        var b0 = new ManualBeam();
        var r0 = b0.Run(s);
        var d0 = (BeamLineDesign)r0.Source;
        var cuts = BeamDrawing.SectionCuts(d0);
        check("Mặt cắt dầm 2 nhịp: 3 vị trí mỗi nhịp = 6", cuts.Count, 6, 0);
        var g0 = BarCutCheck.GeoOf(b0);
        for (int k = 0; k < g0.Spans; k++)
        {
            int kk = k;
            check("Nhịp " + (k + 1) + ": 3 mặt cắt (gối – giữa – gối)", cuts.Count(c => c.X / 1000 > g0.Face0(kk) && c.X / 1000 < g0.Face1(kk)), 3, 0);
        }
        int nSec = cuts.Select(c => c.Num).Distinct().Count();
        var f0 = b0.Draw(r0, s);
        check("Chi tiết dầm: 1 mặt đứng + mỗi mặt cắt khác nhau một hình", f0.Count, 1 + nSec, 0);

        // ---- 5. điểm cắt / nối người dùng – cảnh báo ----
        double f0a = g0.Face0(0), l0a = g0.L0[0];
        string X(double v) => v.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
        var bMid = new ManualBeam { BotSplices = X(f0a + l0a / 2) }.Run(s);
        check("Nối thép dưới giữa nhịp → CẢNH BÁO", B(bMid.Messages.Any(m => m.StartsWith("CẢNH BÁO: nối thép dưới"))), 1, 0);
        check("Nối thép dưới giữa nhịp → kiểm tra không đạt", B(bMid.Checks.Any(c => c.Name.StartsWith("Nối thép dưới") && !c.Ok)), 1, 0);
        var bL5 = new ManualBeam { BotSplices = X(f0a + l0a / 5) }.Run(s);
        check("Nối thép dưới cách mép gối L0/5 → không cảnh báo", B(bL5.Messages.Any(m => m.StartsWith("CẢNH BÁO"))), 0, 0);
        check("Nối thép dưới cách mép gối L0/5 → kiểm tra đạt", B(bL5.Checks.Where(c => c.Name.StartsWith("Nối thép dưới")).All(c => c.Ok)), 1, 0);
        var bTopS = new ManualBeam { TopSplices = X(f0a + 0.2) }.Run(s);
        check("Nối thép trên sát gối → CẢNH BÁO", B(bTopS.Messages.Any(m => m.StartsWith("CẢNH BÁO: nối thép trên"))), 1, 0);
        var bTopM = new ManualBeam { TopSplices = X(f0a + l0a / 2) }.Run(s);
        check("Nối thép trên giữa nhịp → không cảnh báo", B(bTopM.Messages.Any(m => m.StartsWith("CẢNH BÁO"))), 0, 0);
        check("Kháng chấn: nối thép dưới trong 2h từ mép gối → nguy hiểm", B(BarCutCheck.SpliceDanger(g0, f0a + 0.3, false, true).Length > 0), 1, 0);
        var bExt = new ManualBeam { SupExtUser = "0,5" };
        var rExt = bExt.Run(s);
        check("Mũ gối biên vươn 500 < L0/4 → CẢNH BÁO", B(rExt.Messages.Any(m => m.StartsWith("CẢNH BÁO: mũ gối tại gối 1"))), 1, 0);
        var supBar = rExt.Bars.First(b => b.Role == "Mũ gối" && b.Start < 0);
        check("Mũ gối biên vươn tuỳ chỉnh: đầu trong tại mép gối + 500", supBar.End, bExt.SupportWidth / 2 + 0.5, 1e-9);
        check("Mũ gối biên: điểm cắt sai được đánh dấu trên mặt đứng (dấu X đỏ)", B(bExt.Draw(rExt, s)[0].W.Texts().Contains("cắt mũ gối sớm")), 1, 0);
        var bCut = new ManualBeam { AddBotCutUser = "0,9" }.Run(s);
        check("Tăng cường dưới cắt 900 > L0/6 → CẢNH BÁO", B(bCut.Messages.Any(m => m.StartsWith("CẢNH BÁO: tăng cường dưới nhịp 1"))), 1, 0);
        var cOk = new ManualBeam { AddBotCutUser = "0,3" };
        var rOk = cOk.Run(s);
        check("Tăng cường dưới cắt 300 ≤ L0/6 → không cảnh báo", B(rOk.Messages.Any(m => m.StartsWith("CẢNH BÁO"))), 0, 0);
        check("Tăng cường dưới cắt tuỳ chỉnh 300: thanh bắt đầu tại mép gối + 300", rOk.Bars.First(b => b.Role == "Tăng cường dưới" && b.Span == 0).Start, f0a + 0.3, 1e-9);
        check("Mặc định (không tuỳ chỉnh): không cảnh báo cắt / nối", B(r0.Messages.Any(m => m.StartsWith("CẢNH BÁO"))), 0, 0);
        check("ParseKeep giữ số 0: \"0,9; 0; 1,2\"", BarCutCheck.ParseKeep("0,9; 0; 1,2")[1], 0, 0);
        check("Thuyết minh dầm: thứ tự lắp dựng thép dầm", B(r0.Calc.Any(c => c.Contains("Thứ tự lắp dựng thép dầm"))), 1, 0);

        // ---- 6. sàn: nguyên tắc miền chịu kéo, thứ tự đặt thép ----
        var sl = new ManualSlab();
        var rs = sl.Run(s);
        int i1 = rs.Calc.FindIndex(c => c.StartsWith("1) Thép lớp dưới PHƯƠNG NGẮN"));
        int i2 = rs.Calc.FindIndex(c => c.StartsWith("2) Thép lớp dưới PHƯƠNG DÀI"));
        int i6 = rs.Calc.FindIndex(c => c.StartsWith("6) Thép mũ PHƯƠNG NGẮN"));
        check("Ghi chú sàn: có thứ tự đặt thép 1) dưới ngắn → 2) dưới dài → … → 6) mũ ngắn", B(i1 >= 0 && i2 > i1 && i6 > i2), 1, 0);
        check("Ghi chú sàn: nguyên tắc miền chịu kéo", B(rs.Calc.Any(c => c.Contains("Nguyên tắc bố trí thép sàn theo miền chịu kéo"))), 1, 0);
        var sf = sl.Draw(rs, s);
        check("Sàn: vẫn 2 hình (ghi chú đặt dưới mặt cắt)", sf.Count, 2, 0);
        check("Sàn: mặt cắt có khối GHI CHÚ CẤU TẠO THÉP SÀN", B(sf[1].W.Texts().Contains("GHI CHÚ CẤU TẠO THÉP SÀN")), 1, 0);
        var sShop = new ManualSlab { ShopOut = true };
        check("Shop drawing sàn: thêm bảng gia công", sShop.Draw(sShop.Run(s), s).Count, 3, 0);
        var spn = ManualSlabPlan.Sample(); var rsp = spn.Run(s);
        check("Mặt bằng sàn: thuyết minh thứ tự đặt thép", B(rsp.Calc.Any(c => c.StartsWith("1) Thép lớp dưới PHƯƠNG NGẮN"))), 1, 0);

        // ---- 7. trích chi tiết + vạch cắt dầm trên mặt bằng; mặt cắt sàn: phương ngắn nằm dưới ----
        var p = DetailingProject.Sample();
        var res = p.All.Select(m => (Member: m, Result: DetailingProject.RunSafe(m, p.Settings))).ToList();
        var map = StructuralPlanDrawing.Map(p, res);
        var t2 = p.Plan.Levels.First(l => l.Name == "Tầng 2");
        var co = p.Plan.AddCallout(t2, 3.6, -0.6, 8.4, 2.0);
        check("Trích chi tiết: tạo chi tiết A", B(co != null && co.Name == "A"), 1, 0);
        check("Trích chi tiết: cửa sổ quá nhỏ → null", B(p.Plan.AddCallout(t2, 1, 1, 1.02, 1.02) == null), 1, 0);
        var cf = CalloutDrawing.Figures(p.Plan, t2, map, p.Settings);
        check("Trích chi tiết: 1 hình CHI TIẾT A", B(cf.Count == 1 && cf[0].Name == "CHI TIẾT A"), 1, 0);
        var cw = cf[0].W;
        check("Trích chi tiết: tỷ lệ 1/25", cw.Scale, 25, 1e-9);
        check("Trích chi tiết: ghi tl: 1/25", B(cw.Texts().Contains("tl: 1/25")), 1, 0);
        double th = cw.Th, wX = (co.MaxX - co.MinX) * 1000;
        check("Trích chi tiết: hình bị cắt theo cửa sổ (bề rộng ≤ cửa sổ + kích thước)", B(cw.Extents.X2 - cw.Extents.X1 <= wX + 12 * th), 1, 0);
        check("Trích chi tiết: chữ trong chi tiết đúng cỡ in 2,5 mm × 25", B(cw.TextHeights(Lay.KichThuoc).Any(h => Math.Abs(h - 2.5 * 25) < 1e-6)), 1, 0);
        check("Mặt cắt / chi tiết của mặt bằng gồm CHI TIẾT A", B(StructuralPlanDrawing.SectionFigures(p.Plan, t2, map, p.Settings).Any(f => f.Name == "CHI TIẾT A")), 1, 0);
        var lvW = StructuralPlanDrawing.Level(p.Plan, t2, map, StructuralPlanDrawing.LevelTitle(t2), false);
        check("Mặt bằng: ký hiệu CT-A", B(lvW.Texts().Contains("CT-A")), 1, 0);
        var tmp = new DxfWriter(100);
        check("Mặt bằng: vạch cắt dầm D1 tại các mặt cắt", CalloutDrawing.DrawBeamCuts(tmp, t2, map), cuts.Count, 0);
        var js = DetailingProject.FromJson(p.ToJson());
        check("Lưu / mở: giữ chi tiết trích (tên, mặt bằng, toạ độ)", B(js.Plan.Callouts.Count == 1 && js.Plan.Callouts[0].Name == "A" && js.Plan.Callouts[0].Level == "Tầng 2" && Math.Abs(js.Plan.Callouts[0].X2 - 8.4) < 1e-9), 1, 0);

        p.Plan.Sections.Add(new SectionMark { Name = "9-9", Level = "Tầng 2", X1 = 6.0, Y1 = -0.5, X2 = 6.0, Y2 = 5.0 });
        var sec = StructuralPlanDrawing.SlabSection(p.Plan, t2, p.Plan.Sections[^1], map, p.Settings);
        var (lineY, dotY) = BottomBars(sec.ToDxfString());
        check("Mặt cắt sàn theo phương dài: có thanh dọc + chấm thép", B(!double.IsNaN(lineY) && !double.IsNaN(dotY)), 1, 0);
        check("Mặt cắt sàn: thép lớp dưới phương NGẮN (chấm) nằm dưới phương dài (đường)", B(dotY < lineY), 1, 0);

        // ---- 8. ghi file ----
        string dir = Path.Combine(outDir, "TrienKhai");
        Directory.CreateDirectory(dir);
        var info = new SheetComposer.SheetInfo { ProjectName = "Kiểm thử shop drawing", Title = "SHOP DRAWING – TRÍCH CHI TIẾT", SheetNo = "KC-SD" };
        var all = new List<Figure>(lf.Skip(nPlain)) { cf[0] };
        SheetComposer.Compose(all, 50, info, "A1").Save(Path.Combine(dir, "ShopDrawing_TrichChiTiet.dxf"));
        check("Ghi được DXF shop drawing + chi tiết trích", B(File.Exists(Path.Combine(dir, "ShopDrawing_TrichChiTiet.dxf"))), 1, 0);
    }

    /// <summary>Cao độ (y) đường thép LINE lớp THEP thấp nhất nằm ngang và tâm chấm SOLID lớp THEP thấp nhất (mặt cắt sàn).</summary>
    private static (double LineY, double DotY) BottomBars(string dxf)
    {
        var lines = dxf.Replace("\r\n", "\n").Split('\n');
        double lineY = double.NaN, dotY = double.NaN;
        string type = null, layer = null; double? x1 = null, y1 = null, y2 = null; double? sx = null, sy = null;
        void Flush()
        {
            if (type == "LINE" && layer == "THEP" && y1.HasValue && y2.HasValue && Math.Abs(y1.Value - y2.Value) < 1e-6 && y1.Value < -1)
                lineY = double.IsNaN(lineY) ? y1.Value : Math.Min(lineY, y1.Value);
            if (type == "SOLID" && layer == "THEP" && sy.HasValue)
                dotY = double.IsNaN(dotY) ? sy.Value : Math.Min(dotY, sy.Value);
        }
        for (int i = 0; i + 1 < lines.Length; i += 2)
        {
            if (!int.TryParse(lines[i].Trim(), out int code)) continue;
            string v = lines[i + 1].Trim();
            if (code == 0) { Flush(); type = v; layer = null; x1 = y1 = y2 = sx = sy = null; continue; }
            double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d);
            if (code == 8) layer = v;
            else if (code == 10) { x1 = d; if (sx == null) sx = d; }
            else if (code == 20) { y1 = d; if (sy == null) sy = d; }
            else if (code == 21) y2 = d;
        }
        Flush();
        return (lineY, dotY);
    }
}
