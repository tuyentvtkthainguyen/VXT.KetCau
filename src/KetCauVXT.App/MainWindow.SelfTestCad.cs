namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): mở cửa sổ "Chuẩn layer CAD" (không chặn), chụp 2 tab rồi đóng.</summary>
public partial class MainWindow
{
    private async Task SelfTestCadStandard(string tag)
    {
        CadStandardWindow w = null;
        try
        {
            w = OpenCadStandard(0, false);
            await Pause(1200);
            ShotWindow(w, $"{tag}_21_ChuanLayer");
            w.SelectTab(1);
            await Pause(1200);
            ShotWindow(w, $"{tag}_22_LayerKetCau");
            StLog("chuẩn layer CAD: font " + Model.CadStandard.ActiveFontName + ", layer xuất " +
                  string.Join(", ", (Model.CadStandard.Active?.OutputLayers ?? Model.CadStandard.PresetVxt()).Select(o => o.Key + "→" + o.Name)));
        }
        catch (Exception ex) { StLog("LỖI chuẩn layer CAD: " + ex.Message); throw; }
        finally { w?.Close(); }
    }
}
