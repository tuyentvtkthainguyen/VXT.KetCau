namespace KetCauVXT.App;

// =====================================================================
// [Truyền tải] Lệnh "Truyền tải & gợi ý móng…": tải đứng dồn xuống từng chân cột theo diện chịu tải → móng đơn / băng / cọc từng cột
// (TakedownWindow.cs; lõi Design/Foundation/LoadTakedown.cs, TakedownAdvisor.cs).
// =====================================================================
public partial class MainWindow
{
    private async void ShowTakedownUi()
    {
        if (_p == null || _busy) return;
        if (_p.Frames.All(f => !f.IsVerticalKind)) { SetStatus("Mô hình chưa có cột – dựng lưới trục, cột, dầm, sàn và chất tải trước."); return; }
        ApplyUiToProject(false);
        string snap = Snapshot();
        var w = new TakedownWindow(_p, _res) { Owner = this };
        if (w.ShowDialog() != true || string.IsNullOrEmpty(w.Applied)) return;
        PushUndo(snap);
        MarkDirty(false);
        LoadUiFromProject();
        if (_res != null && await DesignFoundation())
        {
            int bad = (_design?.Footings.Count(f => !f.Ok) ?? 0) + (_design?.PileCaps.Count(c => !c.Ok) ?? 0) + (_design?.StripFootings.Count(s => !s.Ok) ?? 0);
            SetStatus($"{w.AppliedText}. Đã thiết kế lại móng theo nội lực khung" + (bad > 0 ? $" – {bad} móng chưa đạt, xem bảng kiểm tra." : " – tất cả đạt.") + " (Hoàn tác để trở lại)");
        }
        else SetStatus($"{w.AppliedText} – chạy phân tích (F5) rồi \"Thiết kế móng\" để kiểm tra chi tiết.");
    }
}
