using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): mở cửa sổ "Văn bản pháp lý &amp; tiêu chuẩn áp dụng" (không chặn), chụp rồi đóng.</summary>
public partial class MainWindow
{
    private async Task SelfTestRegulation(string tag)
    {
        RegulationWindow w = null;
        try
        {
            w = OpenRegulations(false);
            if (w == null) { StLog("LỖI văn bản pháp lý: không mở được cửa sổ"); return; }
            await Pause(1200);
            ShotWindow(w, $"{tag}_40_VanBanPhapLy");
            var reg = RegulationService.Registry;
            var ws = RegulationService.CheckProject(_p);
            StLog($"văn bản pháp lý: {reg.Docs.Count} văn bản, {reg.Current().Count} đang áp dụng, {reg.Docs.Count(d => d.Status == RegulationStatus.HetHieuLuc)} hết hiệu lực; "
                  + "dự án trích dẫn: " + string.Join("; ", RegulationService.Applied(_p).Select(x => x.Citation))
                  + $"; kiểm tra dự án: {ws.Count(x => x.IsWarning)} cảnh báo, {ws.Count(x => !x.IsWarning)} lưu ý"
                  + (string.IsNullOrEmpty(reg.LoadMessage) ? "" : "; " + reg.LoadMessage));
        }
        catch (Exception ex) { StLog("LỖI văn bản pháp lý: " + ex.Message); throw; }
        finally { w?.Close(); }
    }
}
