using System.Windows.Controls;

namespace KetCauVXT.App;

/// <summary>[Tự kiểm tra] Ba tab gom lệnh: Tải trọng, Kiểm tra, Triển khai – khung trái (cổ điển) và ribbon kiểu Revit; chụp ảnh, đếm nút lệnh.</summary>
public partial class MainWindow
{
    private async Task SelfTestCommandTabs(string tag)
    {
        try
        {
            int Count(ContentControl c) => c?.Content is Border b && b.Child is WrapPanel wp
                ? wp.Children.OfType<Border>().Sum(g => (g.Child as DockPanel)?.Children.OfType<WrapPanel>().Sum(x => x.Children.Count) ?? 0) : 0;
            StLog($"tab gom lệnh (khung trái): Tải trọng {Count(ccLoadCommands)} lệnh, Kiểm tra {Count(ccCheckCommands)} lệnh, Triển khai {Count(ccDeliverCommands)} lệnh");
            SelectLeft("Tải trọng"); await Pause(900); Shot($"{tag}_65_TabTaiTrong");
            SelectLeft("Kiểm tra"); await Pause(900); Shot($"{tag}_66_TabKiemTra");
            SelectLeft("Triển khai"); await Pause(900); Shot($"{tag}_67_TabTrienKhai");
            ApplyRevitLayout(true, false);
            try
            {
                await Pause(1200);
                int k = 68;
                foreach (var h in new[] { "Tải trọng", "Kiểm tra", "Triển khai" })
                {
                    var ti = _rvTabs?.Items.OfType<TabItem>().FirstOrDefault(t => (t.Header as string) == h);
                    if (ti == null) { StLog("ribbon: thiếu tab " + h); continue; }
                    _rvTabs.SelectedItem = ti;
                    await Pause(900);
                    var sv = ti.Content as ScrollViewer;
                    StLog($"ribbon {h}: rộng nội dung {sv?.ExtentWidth:0} / khung {sv?.ViewportWidth:0} px, cao {sv?.ExtentHeight:0} / {sv?.ViewportHeight:0} px");
                    Shot($"{tag}_{k++}_Ribbon_{h.Replace(" ", "")}");
                }
                StLog("ribbon tabs: " + string.Join(" | ", _rvTabs?.Items.OfType<TabItem>().Select(t => t.Header as string) ?? Array.Empty<string>()));
            }
            finally { ApplyRevitLayout(false, false); await Pause(500); }
        }
        catch (Exception ex) { StLog("tab gom lệnh: LỖI " + ex.Message); }
    }
}
