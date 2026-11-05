using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Tuỳ chọn bố trí thép dầm / cột ở chế độ triển khai hồ sơ (ManualBeam, ManualColumn):
/// thép dưới / trên neo thẳng hoặc bẻ mỏ, thép tăng cường dưới (2)(3)(4) cắt tại 0,1·L0 – L0/7 – L0/8 – kéo vào gối,
/// mũ gối vươn L0/3 – L0/4 – L0/5 – qua hết nhịp ngắn, hàng 2 → h0 giảm, Mu kể đủ các nhóm thép;
/// cột: vị trí nối tự gợi ý ngoài vùng lcr / trên mặt sàn / ống ren, so le 50 %, nhấn cổ chai 1:6;
/// chữ tag cách vòng số hiệu ≥ 0,6·h; đoạn sàn mặt cắt dầm mặc định 50 mm; JSON cũ đọc được.
/// check(tên, giá trị, kỳ vọng, sai số tương đối).</summary>
public static class BeamColumnOptionsTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        var s = new DetailSettings();
        var conc = Material.Concrete("B25"); var main = Material.Rebar("CB400-V");
        double c = s.CoverBeam, w = 0.22;
        double xs = -w / 2 + c, xe = 7.8 + w / 2 - c;
        double l0a = 4.2 - w, l0b = 3.6 - w;
        double lan16 = Math.Max(0.48, RebarDetailing.AnchorageLength(conc, main, 16));
        double leg16 = Dt.R10(Math.Min(Math.Max(Math.Max(lan16 - (w - c), 0), 0.16), 0.40 - 2 * c));

        // ---- 1. thép dưới chạy suốt: bẻ mỏ (mặc định) / neo thẳng ----
        var bh = new ManualBeam().Run(s);
        var bs = new ManualBeam { BotHook = ManualBeam.HookNo, TopHook = ManualBeam.HookNo }.Run(s);
        var botH = bh.Bars.First(b => b.Role == "Dưới"); var botS = bs.Bars.First(b => b.Role == "Dưới");
        check("Dầm: thép dưới neo thẳng – chiều dài = xe − xs", botS.Length, Dt.R10(xe - xs), 1e-9);
        check("Dầm: thép dưới neo thẳng – hình dạng thẳng", B(botS.Shape == "Thẳng"), 1, 0);
        check("Dầm: thép dưới bẻ mỏ – chiều dài = thẳng + 2 mỏ", botH.Length, Dt.R10(Dt.R10(xe - xs) + 2 * leg16), 1e-9);
        check("Dầm: thép dưới bẻ mỏ – hình chữ U", B(botH.Shape == "U"), 1, 0);
        check("Dầm: thép trên neo thẳng – thẳng", B(bs.Bars.First(b => b.Role == "Trên").Shape == "Thẳng"), 1, 0);
        var bNarrow = new ManualBeam { BotHook = ManualBeam.HookNo, SupportWidth = 0.15 }.Run(s);
        check("Dầm: neo thẳng vào gối 125 mm < 10d → cảnh báo", B(bNarrow.Messages.Any(m => m.Contains("neo thẳng vào gối biên"))), 1, 0);
        check("Dầm: gối đủ rộng → không cảnh báo neo thẳng", B(bs.Messages.Any(m => m.Contains("neo thẳng vào gối biên"))), 0, 0);

        // ---- 2. vị trí cắt thép tăng cường dưới ----
        check("Khoảng cắt 0,1·L0", ManualBeam.CutDistance(ManualBeam.CutC01, 3.98), 0.398, 1e-12);
        check("Khoảng cắt L0/7", ManualBeam.CutDistance(ManualBeam.CutL7, 3.5), 0.5, 1e-12);
        check("Khoảng cắt L0/8", ManualBeam.CutDistance(ManualBeam.CutL8, 4.0), 0.5, 1e-12);
        double Add0(DetailResult r, string role) => r.Bars.First(b => b.Role == role && b.Span == 0).Length;
        check("Tăng cường dưới mặc định 0,1·L0: L = 0,8·L0", Add0(bh, "Tăng cường dưới"), Dt.R10(0.8 * l0a), 1e-9);
        var b7 = new ManualBeam { AddBotCut = ManualBeam.CutL7 }.Run(s);
        check("Tăng cường dưới cắt L0/7: L = L0 − 2·L0/7", Add0(b7, "Tăng cường dưới"), Dt.R10(l0a - 2 * l0a / 7), 1e-9);
        var b8 = new ManualBeam { AddBotCut = ManualBeam.CutL8 }.Run(s);
        check("Tăng cường dưới cắt L0/8: L = 0,75·L0", Add0(b8, "Tăng cường dưới"), Dt.R10(0.75 * l0a), 1e-9);
        var bA = new ManualBeam { AddBotCut = ManualBeam.CutAnchor, AddBotHook = ManualBeam.HookYes }.Run(s);
        var tA = bA.Bars.First(b => b.Role == "Tăng cường dưới" && b.Span == 0);
        double into = Math.Min(Math.Max(0.16, 0.15), w - c);
        check("Tăng cường kéo vào gối: nhịp biên từ xs tới mép gối + max(10d;150)", tA.End - tA.Start, (w / 2 + l0a + into) + (w / 2 - c), 1e-6);
        check("Tăng cường kéo vào gối + bẻ mỏ: hình L ở gối biên", B(tA.Shape == "L"), 1, 0);

        // ---- 3. mũ gối: chiều dài vươn ----
        check("Mũ gối tiêu chuẩn: gối biên L0/4", ManualBeam.ExtLength(ManualBeam.ExtStd, 4, true), 1, 1e-12);
        check("Mũ gối tiêu chuẩn: gối giữa L0/3", ManualBeam.ExtLength(ManualBeam.ExtStd, 3, false), 1, 1e-12);
        check("Mũ gối L0/5", ManualBeam.ExtLength(ManualBeam.ExtL5, 5, false), 1, 1e-12);
        BarMark Mid(DetailResult r, string role) => r.Bars.First(b => b.Role == role && b.Start > 0.5 && b.End < 7.3);
        check("Mũ gối giữa (L0/3 hai bên): L = bc + L01/3 + L02/3", Mid(bh, "Mũ gối").Length, Dt.R10(w + l0a / 3 + l0b / 3), 1e-9);
        var b4 = new ManualBeam { SupExt = ManualBeam.ExtL4 }.Run(s);
        check("Mũ gối giữa L0/4: L = bc + (L01 + L02)/4", Mid(b4, "Mũ gối").Length, Dt.R10(w + (l0a + l0b) / 4), 1e-9);
        var bShort = new ManualBeam { SupExt = ManualBeam.ExtShort }.Run(s);
        check("Mũ gối qua hết nhịp ngắn: chạy tới gối biên nhịp ngắn", bShort.Bars.Where(b => b.Role == "Mũ gối").Max(b => b.End - b.Start),
              (w / 2 + l0b + w / 2 - c) + w / 2 + l0a / 3, 1e-6);
        var endSup = bh.Bars.First(b => b.Role == "Mũ gối" && Math.Abs(b.Start - xs) < 1e-9);
        check("Mũ gối biên: vươn L0/4 + bẻ mỏ", endSup.End - endSup.Start, w - c + l0a / 4, 1e-6);
        check("Mũ gối biên bẻ mỏ → hình L", B(endSup.Shape == "L"), 1, 0);
        var bNoHookSup = new ManualBeam { SupHook = ManualBeam.HookNo }.Run(s);
        check("Mũ gối biên không bẻ mỏ → thẳng", B(bNoHookSup.Bars.Where(b => b.Role == "Mũ gối").All(b => b.Shape == "Thẳng")), 1, 0);

        // ---- 4. nhiều nhóm, hàng 2 → h0, Mu ----
        var multi = new ManualBeam { AddBot2N = 2, AddBot2D = 16, AddBot2Row = 2, Sup2N = 1, Sup2D = 14, Sup2Row = 1, MSpan = 60, MSupport = 60, Q = 60 };
        var rm = multi.Run(s);
        double aExp = (3 * 0.033 + 2 * (0.025 + 0.016 + 0.025 + 0.008)) / 5;
        check("Hàng 2 thép dưới: trọng tâm a = Σ(As·y)/ΣAs", ManualBeam.LayerCentroid(c, 16, 2, multi.BottomGroups(), 0.22, 6, false, out bool two), aExp, 1e-9);
        check("Hàng 2 thép dưới → 2 lớp", B(two), 1, 0);
        double As5 = 5 * Dt.Area(16), As2 = 2 * Dt.Area(16);
        double muExp = RcSection.MomentCapacity(0.22, 0.40 - aExp, As5, As2, c + 0.008, conc, main);
        check("Mu nhịp kể thép chạy suốt + tăng cường (2) + (3), h0 theo trọng tâm", rm.Checks.First(k => k.Name.StartsWith("Chịu uốn giữa nhịp")).Capacity, muExp, 1e-6);
        check("Có thanh tăng cường dưới (3) mỗi nhịp", rm.Bars.Count(b => b.Role == "Tăng cường dưới 3"), 2, 0);
        check("Có mũ gối (3) ở 3 gối", rm.Bars.Count(b => b.Role == "Mũ gối 3"), 3, 0);
        check("Tăng cường (3) cắt L0/7 (mặc định nhóm 3)", Add0(rm, "Tăng cường dưới 3"), Dt.R10(l0a - 2 * l0a / 7), 1e-9);
        double asTop = 2 * Dt.Area(16) + Dt.Area(16) + Dt.Area(14);
        check("BeamSpanResult: As trên tại gối kể mũ gối (2)+(3)", ((BeamLineDesign)rm.Source).Spans[0].AsTopLeftProv, asTop, 1e-9);
        check("Số hiệu thép duy nhất khi nhiều nhóm", RebarNumbering.Validate(rm.Bars).Count, 0, 0);
        double aOver = ManualBeam.LayerCentroid(c, 20, 6, new List<ManualBeam.AddGroup>(), 0.22, 6, false, out bool twoOver);
        check("6Ø20 trên b = 220 không lọt 1 hàng → tự chuyển hàng 2", B(twoOver && aOver > c + 0.010 + 1e-6), 1, 0);

        // ---- 5. JSON cũ (chỉ có BotN, AddBotN, SupN…) vẫn đọc được ----
        var old = System.Text.Json.JsonSerializer.Deserialize<ManualBeam>("{\"Name\":\"D9\",\"BotN\":3,\"BotD\":18,\"AddBotN\":1,\"AddBotD\":14,\"TopN\":2,\"TopD\":16,\"SupN\":2,\"SupD\":14}");
        check("JSON cũ: thép dưới neo mặc định bẻ mỏ", B(old.BotHook == ManualBeam.HookYes), 1, 0);
        check("JSON cũ: 1 nhóm tăng cường dưới Ø14", B(old.BottomGroups().Count == 1 && old.BottomGroups()[0].D == 14), 1, 0);
        check("JSON cũ: 1 nhóm mũ gối 2Ø14", B(old.TopGroups().Count == 1 && old.TopGroups()[0].N == 2), 1, 0);
        check("JSON cũ: chạy được", B(old.Run(s).Bars.Count > 3), 1, 0);

        // ---- 6. cột: vị trí nối ----
        var col0 = new ManualColumn();
        var rc = col0.Run(s);
        var cd = (ColumnDesign)rc.Source;
        double lap = cd.Splices[0].Lap;
        check("Cột tự gợi ý: 2 nhóm nối so le", cd.Splices.Count, 2, 0);
        check("Cột tự gợi ý: mối nối nằm ngoài vùng lcr", B(cd.Splices.All(sp => sp.Center - lap / 2 >= sp.ZoneA - 1e-9 && sp.Center + lap / 2 <= sp.ZoneB + 1e-9)), 1, 0);
        check("Cột tự gợi ý: khoảng so le ≥ 1,3·ll", B(cd.Splices[1].Center - cd.Splices[0].Center >= 1.3 * lap - 1e-9), 1, 0);
        check("Cột: thuyết minh có vùng nối gợi ý", B(rc.Calc.Any(t => t.Contains("vùng nối gợi ý"))), 1, 0);
        var rf = new ManualColumn { SpliceMode = ManualColumn.SpliceFloor }.Run(s);
        var cf = (ColumnDesign)rf.Source;
        check("Cột nối trên mặt sàn: tâm nhóm A = cao trình sàn + ll/2", cf.Splices[0].Center, (cf.Stories[1].ZBottom - col0.BaseElevation) + cf.Splices[0].Lap / 2, 1e-9);
        check("Cột nối trên mặt sàn: cảnh báo vùng nguy hiểm", B(rf.Messages.Any(m => m.Contains("Nối thép cột trên mặt sàn"))), 1, 0);
        var rmc = new ManualColumn { SpliceMode = ManualColumn.SpliceMech }.Run(s);
        check("Cột ống nối ren: không chiều dài nối chồng", ((ColumnDesign)rmc.Source).Splices[0].Lap, 0, 0);
        check("Cột ống nối ren: thống kê 6 ống nối", rmc.Extra.Where(e => e.Item.StartsWith("Ống nối ren")).Sum(e => e.Qty), 6, 0);
        var rns = new ManualColumn { Stagger = false }.Run(s);
        check("Cột không so le: không có thanh \"(so le)\"", rns.Bars.Count(b => b.Role == "Thép dọc (so le)"), 0, 0);
        check("Cột không so le: hai nhóm nối cùng cao độ", ((ColumnDesign)rns.Source).Splices[0].Center, ((ColumnDesign)rns.Source).Splices[1].Center, 1e-12);

        // ---- 7. nhấn cổ chai 1:6 ----
        var colK = new ManualColumn { Crank = true };
        var rk = colK.Run(s);
        var kb = rk.Bars.First(b => b.Role == "Thép dọc" && b.Span == 0);
        check("Nhấn cổ chai: hình \"Nhấn\"", B(kb.Shape == "Nhấn"), 1, 0);
        double lapK = ((ColumnDesign)rk.Source).Splices[0].Lap * 1000;
        check("Nhấn cổ chai: đoạn trên = ll", kb.Segments[^1], Math.Round(lapK), 1e-9);
        check("Nhấn cổ chai: đoạn xiên = √(6² + 1²)·d", kb.Segments[^2], Math.Round(Math.Sqrt(37) * 16), 1e-9);
        check("Nhấn cổ chai: chiếu đứng v1 + 6d + ll = chiều dài thanh thẳng", kb.Segments[0] + 6 * 16 + kb.Segments[^1], (kb.End - kb.Start) * 1000, 2e-3);
        check("Nhấn cổ chai: thép chờ chân cột có chân + nhấn", B(rk.Bars.Any(b => b.Role.StartsWith("Thép chờ") && b.Shape == "Nhấn L")), 1, 0);
        check("Nhấn cổ chai: thanh tầng mái khoá đầu không nhấn", B(rk.Bars.Where(b => b.Role == "Thép dọc" && b.Span == 1).All(b => b.Shape != "Nhấn")), 1, 0);
        check("Không chọn nhấn: không có thanh nhấn", rc.Bars.Count(b => (b.Shape ?? "").StartsWith("Nhấn")), 0, 0);
        check("Ống nối ren: không nhấn cổ chai dù chọn", new ManualColumn { Crank = true, SpliceMode = ManualColumn.SpliceMech }.Run(s).Bars.Count(b => (b.Shape ?? "").StartsWith("Nhấn")), 0, 0);

        // ---- 8. chữ tag – vòng số hiệu, cánh sàn 50 ----
        var dw = new DxfWriter(25);
        foreach (var txt in new[] { "2Ø16", "Ø8a150", "1Ø14 L=4250", "WWW" })
        {
            double th = 2.5 * 25;
            check("Tag \"" + txt + "\": khe chữ – vòng = 0,6·h", dw.TagShoulder(txt, th) - 0.3 * th - DxfWriter.TagTextWidth(txt, th) * dw.WidthFactor, 0.6 * th, 1e-9);
            check("Tag \"" + txt + "\": bề rộng chữ ước lượng ≥ bề rộng cũ", B(DxfWriter.TagTextWidth(txt, th) >= DxfWriter.TextWidth(txt, th) - 1e-9), 1, 0);
        }
        check("Mặt cắt dầm: đoạn sàn mặc định 50 mm", new DrawingStyle().SlabFlangeLength, 50, 1e-12);

        // ---- 9. bản vẽ ----
        var figs = new List<Figure>();
        var beamM = new ManualBeam { Name = "D2", AddBot2N = 2, AddBot2Row = 2, AddBotCut = ManualBeam.CutL7, Sup2N = 1, Sup2D = 14, BotHook = ManualBeam.HookNo };
        var fb = beamM.Draw(beamM.Run(s), s);
        check("Bản vẽ dầm nhiều nhóm thép: có hình", B(fb.Count >= 2 && fb.All(fg => fg.W.EntityCount > 10)), 1, 0);
        var fk = colK.Draw(rk, s);
        check("Bản vẽ cột nhấn cổ chai: có hình", B(fk.Count > 0 && fk.All(fg => fg.W.EntityCount > 10)), 1, 0);
        figs.AddRange(fb); figs.AddRange(fk);
        Directory.CreateDirectory(Path.Combine(outDir, "TrienKhai"));
        var (sc, paper) = DetailingExport.SheetFor(figs);
        string f = Path.Combine(outDir, "TrienKhai", "DamCotTuyChon.dxf");
        SheetComposer.Compose(figs, sc, new SheetComposer.SheetInfo { ProjectName = "Kiểm thử tuỳ chọn thép", Title = "DẦM D2 – CỘT C1 (NHẤN CỔ CHAI)", SheetNo = "KC-TC" }, paper).Save(f);
        check("Lưu DXF dầm – cột tuỳ chọn", B(File.Exists(f) && new FileInfo(f).Length > 1000), 1, 0);
    }
}
