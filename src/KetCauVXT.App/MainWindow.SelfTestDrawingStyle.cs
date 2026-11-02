namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): mở cửa sổ "Thiết lập bản vẽ (tỷ lệ, cỡ chữ)" (không chặn), chụp 2 tab rồi đóng.</summary>
public partial class MainWindow
{
    private async Task SelfTestDrawingStyle(string tag)
    {
        DrawingStyleWindow w = null;
        try
        {
            w = OpenDrawingStyle(false, 0);
            await Pause(1200);
            ShotWindow(w, $"{tag}_28_ThietLapBanVe");
            w.SelectTab(1);
            await Pause(1200);
            ShotWindow(w, $"{tag}_28b_KieuKichThuoc");
            var st = Model.DrawingStyle.Current;
            StLog($"thiết lập bản vẽ: dầm 1/{st.ScaleBeamElevation}, mặt cắt 1/{st.ScaleMemberSection}, mặt bằng 1/{st.ScaleFramingPlan}, " +
                  $"chữ dim {st.DimTextHeight} mm, ghi chú {st.NoteHeight} mm, nhãn {st.TagHeight} mm; kiểu kích thước: " +
                  string.Join(", ", st.DimTable().Select(d => d.Name)));
        }
        catch (Exception ex) { StLog("LỖI thiết lập bản vẽ: " + ex.Message); throw; }
        finally { w?.Close(); }
    }
}
