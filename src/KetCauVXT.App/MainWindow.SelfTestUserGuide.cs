using KetCauVXT.Docs;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): mở cửa sổ "Hướng dẫn sử dụng" (không chặn), chọn chương 4, chụp; thử trợ giúp theo ngữ cảnh;
/// mở bảng "Bắt đầu nhanh", chụp rồi đóng.</summary>
public partial class MainWindow
{
    private async Task SelfTestUserGuide(string tag)
    {
        UserGuideWindow w = null;
        QuickStartWindow q = null;
        try
        {
            w = OpenUserGuide(null, false);
            if (w == null) { StLog("LỖI hướng dẫn sử dụng: không mở được cửa sổ"); return; }
            await Pause(800);
            w.SelectChapter(3);
            await Pause(1200);
            ShotWindow(w, $"{tag}_51_HuongDan");
            var chs = UserGuide.Chapters();
            string ctx = CurrentHelpId();
            var opened = w.ShowHelp(ctx);
            StLog($"hướng dẫn sử dụng: {chs.Count} chương, {UserGuide.AllSections().Count()} mục; chương 4 = \"{chs[Math.Min(3, chs.Count - 1)].Title}\"; "
                  + $"ngữ cảnh hiện tại = {ctx} → {(opened != null ? opened.FullTitle : "KHÔNG CÓ MỤC")}"
                  + (UserGuide.Exists(ctx) ? "" : "  LỖI: HelpId không có trong hướng dẫn"));

            q = ShowQuickStart(false);
            if (q == null) { StLog("LỖI bắt đầu nhanh: không mở được bảng"); return; }
            q.Refresh();
            await Pause(1000);
            ShotWindow(q, $"{tag}_52_BatDauNhanh");
            StLog($"bắt đầu nhanh: {QuickStart.Steps.Count} bước, đã làm {q.DoneCount}");
        }
        catch (Exception ex) { StLog("LỖI hướng dẫn sử dụng: " + ex.Message); throw; }
        finally { w?.Close(); q?.Close(); }
    }
}
