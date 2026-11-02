using System.IO;
using KetCauVXT.Drawing;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): khai báo cầu thang cho ô cầu thang tầng giữa (không hộp thoại modal),
/// phân tích + thiết kế lại, xuất bản vẽ cầu thang DXF và ghi kết quả.</summary>
public partial class MainWindow
{
    private async Task SelfTestStair(string tag)
    {
        var stories = _p.Stories.OrderBy(s => s.Elevation).ToList();
        if (stories.Count < 2) { StLog("cầu thang: dự án < 2 tầng – bỏ qua"); return; }
        var mid = stories[Math.Max(1, stories.Count / 2)];
        var o = _p.Openings.FirstOrDefault(x => x.Kind == OpeningKind.Stair && stories.FindIndex(s => s.Name == x.Story) >= 1);
        if (o == null)
        {
            var sl = _p.Slabs.Where(x => x.Story == mid.Name && x.Nodes.Count >= 3).OrderByDescending(x => SlabArea(x)).FirstOrDefault();
            if (sl == null) { StLog("cầu thang: tầng " + mid.Name + " không có ô sàn – bỏ qua"); return; }
            var ns = sl.Nodes.Select(id => _p.Node(id)).ToList();
            var r = ModelEdit.AddOpening(_p, mid.Name, ns.Average(n => n.X), ns.Average(n => n.Y), OpeningKind.Stair);
            StLog("cầu thang: thêm lỗ sàn – " + r.Message);
            if (!r.Ok) return;
            o = _p.Openings[^1];
            MarkDirty();
            RefreshAll(false);
        }
        Select(opening: o);
        var dlg = CreateStairDialog(o, false);
        dlg.Show();
        await Pause(1200);
        ShotWindow(dlg, $"{tag}_27_CauThang");
        var cur = dlg.Current;
        var lay = cur.Layout(_p);
        StLog($"cầu thang {cur.Name}: {Stair.TypeName(cur.Type)}, {lay.NRisers} bậc h = {lay.Riser * 1000:0} mm, b = {lay.Tread * 1000:0} mm, 2h+b = {lay.Check2hb * 1000:0} mm, bản {lay.WaistT * 1000:0} mm; {lay.Warnings.Count} lưu ý");
        bool ok = dlg.ApplyNow();
        if (dlg.IsVisible) dlg.Close();
        StLog("cầu thang: áp dụng " + (ok ? "đạt" : "KHÔNG được") + $" – {_p.Stairs.Count} cầu thang khai báo");
        if (!ok) return;

        if (!await RunAnalysis()) { StLog("cầu thang: phân tích lỗi"); return; }
        int nStairLoads = _p.FrameLoads.Count(l => l.Source == KetCauVXT.Loads.StairLoadGenerator.Source) + _p.AreaLoads.Count(a => a.Source == KetCauVXT.Loads.StairLoadGenerator.Source);
        StLog($"cầu thang: {nStairLoads} tải cầu thang truyền vào khung, {_res?.Warnings.Count(x => x.Contains("Cầu thang")) ?? 0} cảnh báo cầu thang");
        if (!await EnsureDesign()) { StLog("cầu thang: thiết kế lỗi"); return; }
        foreach (var sd in _design.Stairs)
            StLog($"thiết kế {sd.Name}: {sd.Parts.Count} bộ phận, {(sd.Ok ? "đạt" : "KHÔNG ĐẠT")} – " +
                  string.Join("; ", sd.Parts.Select(x => $"{x.Name} {x.MainText}/{x.TopText}")));

        // bản vẽ cầu thang
        var figs = StairDrawing.BuildAll(_p, _design, StairDrawing.CurrentScale);
        string dxf = Path.Combine(SelfTestOut, tag + "_KC-08_CauThang.dxf");
        var w = SheetComposer.Compose(figs, StairDrawing.CurrentScale, new SheetComposer.SheetInfo
        {
            ProjectName = _p.Name, Title = "CẦU THANG – MẶT BẰNG, MẶT CẮT, BỐ TRÍ THÉP", SheetNo = "KC-08", Designer = _p.Designer, Owner = _p.Owner, Location = _p.Location
        }, "A1");
        w.Save(dxf);
        StLog("bản vẽ cầu thang: " + dxf + (File.Exists(dxf) ? $" ({new FileInfo(dxf).Length} byte, {figs.Count} hình)" : " – KHÔNG CÓ FILE"));

        // khối lượng cầu thang
        var q = QuantityCalculator.Compute(_p, _design);
        StLog($"khối lượng cầu thang: bê tông {q.Volume(QtyCat.Stair):0.000} m³, ván khuôn {q.Formwork(QtyCat.Stair):0.00} m²; " +
              string.Join("; ", q.StairWorks.Select(x => $"{x.Work} {x.Quantity:0.000} {x.Unit}")));
    }

    private double SlabArea(Slab s)
    {
        try
        {
            var pts = s.Nodes.Select(id => _p.Node(id)).ToList();
            double a = 0;
            for (int i = 0; i < pts.Count; i++) { var p1 = pts[i]; var p2 = pts[(i + 1) % pts.Count]; a += p1.X * p2.Y - p2.X * p1.Y; }
            return Math.Abs(a) / 2;
        }
        catch (KeyNotFoundException) { return 0; }
    }
}
