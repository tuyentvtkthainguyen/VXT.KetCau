using System.Text.Json;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): bố trí cọc do người dùng chọn – mở "Bố trí cọc…" cho đài đầu tiên, chọn sơ đồ 5 cọc,
/// chụp hộp thoại, áp dụng, thiết kế lại, chụp mặt bằng móng; trả lại thông số móng ban đầu.</summary>
public partial class MainWindow
{
    private async Task SelfTestPileLayout(string tag)
    {
        string fsSnap = JsonSerializer.Serialize(_p.Foundation, Project.JsonOptions);
        PileLayoutDialog dlg = null;
        try
        {
            tabView.SelectedItem = ViewTab("Mô hình móng");
            _p.Foundation.Type = FoundationType.Pile;
            if (!await DesignFoundation()) { StLog("bố trí cọc: thiết kế móng cọc lỗi"); return; }
            var cap = DesignCaps().FirstOrDefault();
            if (cap == null) { StLog("bố trí cọc: không có đài cọc"); return; }
            int node = cap.NodeIds.FirstOrDefault();
            StLog($"bố trí cọc: đài {cap.Name} (nút {node}) tự chọn {cap.Piles.Count} cọc, {cap.L:0.00}×{cap.B:0.00}×{cap.H:0.00} m, Pmax = {cap.PMax:0.0} kN");
            _fndSelItem = cap;
            Select(footingNode: node);
            await Pause(800);

            dlg = OpenPileLayout(cap);
            await Pause(1200);
            dlg.SelectPattern(PilePattern.P5);
            await Pause(1500);
            ShotWindow(dlg, $"{tag}_31_BoTriCoc");
            StLog("bố trí cọc (thử 5 cọc): " + dlg.Summary);
            var tr = dlg.Trial;
            if (tr?.Safety != null)
                StLog($"bố trí cọc: {tr.Safety.Items.Count} hạng mục kiểm tra, {tr.Safety.Items.Count(i => i.Status == KetCauVXT.Design.Foundation.CheckStatus.Fail)} không đạt, "
                      + $"{tr.Safety.Warnings.Count} cảnh báo; η nhóm = {tr.Safety.GroupEta:0.00}");

            await dlg.ApplyAsync();
            await Pause(1200);
            var c5 = DesignCaps().FirstOrDefault(c => c.NodeIds.Contains(node));
            StLog(c5 == null ? "bố trí cọc: không tìm thấy đài sau khi áp dụng"
                : $"bố trí cọc: sau áp dụng {c5.Name} {c5.Piles.Count} cọc, {c5.L:0.00}×{c5.B:0.00}×{c5.H:0.00} m, Pmax = {c5.PMax:0.0} kN – {(c5.Ok ? "đạt" : "KHÔNG ĐẠT")}"
                  + $"; bố trí riêng: {_p.Foundation.CapOverrides.Count} nút");
            try { dlg.Close(); } catch (InvalidOperationException) { }
            dlg = null;
            _fndSelItem = c5;
            Select(footingNode: node);
            RedrawFoundationPlan();
            await Pause(1500);
            Shot($"{tag}_32_MongCocSauBoTri");
        }
        catch (Exception ex) { StLog("bố trí cọc: LỖI " + ex.Message); }
        finally
        {
            try { dlg?.Close(); } catch (InvalidOperationException) { }
            var fs = JsonSerializer.Deserialize<FoundationSettings>(fsSnap, Project.JsonOptions);
            if (fs != null) _p.Foundation = fs;
            MarkDirty(false);
            SyncFoundationUi();
            StLog("bố trí cọc: đã trả lại thông số móng ban đầu (" + FoundationTypeName(_p.Foundation.Type) + ")");
        }
    }
}
