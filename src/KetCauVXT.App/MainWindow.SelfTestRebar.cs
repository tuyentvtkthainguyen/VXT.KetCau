using System.IO;
using KetCauVXT.Design.Concrete;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): chọn một dầm, mở hộp thoại "Chọn bố trí thép…" (không chặn), chụp ảnh,
/// áp dụng một phương án thép, thiết kế lại, xuất shop drawing thép vào SelfTestOut, ghi log rồi đóng.</summary>
public partial class MainWindow
{
    private async Task SelfTestRebar(string tag)
    {
        if (_design == null || _design.Beams.Count == 0)
        {
            if (!await EnsureDesign()) { StLog("chọn thép: không thiết kế được – bỏ qua"); return; }
        }
        var beam = _design.Beams.FirstOrDefault(b => !b.IsCurved && b.Spans.Count > 1) ?? _design.Beams.FirstOrDefault();
        if (beam == null) { StLog("chọn thép: không có dầm BTCT – bỏ qua"); return; }
        string key = beam.RebarKeys.FirstOrDefault() ?? "";
        StLog($"chọn thép: dầm {beam.Name} ({key}), {beam.Spans.Count} nhịp, {RebarOptions.Zones(_p, beam).Count} vùng thép");

        var dlg = CreateRebarDialog(beam);
        dlg.Width = 1250; dlg.Height = 650;
        dlg.Show();
        try
        {
            await Pause(1500);
            ShotWindow(dlg, $"{tag}_20_ChonThep");
            // vùng 0 = thép dưới nhịp 1; chọn phương án thứ 2 (khác phương án hiện tại nếu có)
            dlg.SelectZone(0);
            await Pause(500);
            StLog($"chọn thép: vùng 1 có {dlg.AlternativeCount} phương án");
            int idx = dlg.AlternativeCount > 1 ? 1 : 0;
            var applied = await dlg.ApplyAlternative(idx);
            var after = DesignByRebarKey(key);
            StLog($"chọn thép: áp dụng {applied?.Text ?? "(không có)"} → lựa chọn lưu {_p.RebarChoices.Count}, dầm {after?.Name} "
                  + $"{(after == null ? "?" : after.Ok ? "ĐẠT" : "KHÔNG ĐẠT")}, thép dưới nhịp 1 = {(after as KetCauVXT.Design.BeamLineDesign)?.Spans.FirstOrDefault()?.BotText}"
                  + $", vùng dùng thép chọn = {after?.UserRebarZones}");
            await Pause(1200);
            ShotWindow(dlg, $"{tag}_21_ChonThep_SauApDung");

            // shop drawing
            string dir = Path.Combine(SelfTestOut, tag + "_GiaCongThep");
            int n = 0;
            var ds = _design;
            bool ok = await RunBusy("Lỗi xuất shop drawing thép", prog => n = ExportRebarShop(ds, dir));
            StLog($"shop drawing: {(ok ? "ok" : "LỖI")}, {n} file → {dir}; {_lastShopSummary}");
            if (!ok) throw new Exception("Xuất shop drawing thép lỗi");
        }
        finally
        {
            dlg.Close();
        }
    }
}
