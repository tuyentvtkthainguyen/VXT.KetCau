using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): bật dao động riêng, phân tích, mở khung nhìn "Dao động", chụp dạng 1 và dạng 3
/// (dừng hoạt hoạ, biên độ cố định), ghi bảng 5 chu kỳ đầu + tỷ lệ khối lượng vào selftest.log, trả lại thiết lập.</summary>
public partial class MainWindow
{
    private async Task SelfTestModal(string tag)
    {
        InitModalUi();
        var saveModal = (_p.Modal ?? new ModalSettings()).Clone();
        bool saveDia = _p.UseRigidDiaphragm;
        try
        {
            _p.Modal ??= new ModalSettings();
            _p.Modal.Enabled = true;
            if (!await RunAnalysis()) throw new Exception("Phân tích (dao động riêng) lỗi");
            var mr = _res?.Modal;
            if (mr == null || mr.Modes.Count == 0)
            {
                StLog("dao động riêng: không có kết quả – " + string.Join(" | ", _res?.Warnings.Where(w => w.Contains("dao động")) ?? Enumerable.Empty<string>()));
                return;
            }
            StLog($"dao động riêng: {mr.Modes.Count} dạng, {mr.DynamicDofs} bậc tự do động, {mr.Solver}; nguồn khối lượng {mr.MassSource}; "
                + $"T1 = {mr.First.Period:0.000} s, Ct·H^0,75 = {mr.T1Formula:0.000} s; Σ UX = {mr.SumRatio(0) * 100:0.0} %, Σ UY = {mr.SumRatio(1) * 100:0.0} %");
            StLog("   Dạng |   T (s) |  f (Hz) |   UX % |   UY % |   RZ % |  ΣUX % |  ΣUY % | phương");
            foreach (var m in mr.Modes.Take(5))
                StLog($"   {m.Index,4} | {m.Period,7:0.000} | {m.Frequency,7:0.000} | {m.Ratio[0] * 100,6:0.0} | {m.Ratio[1] * 100,6:0.0} | {m.Ratio[3] * 100,6:0.0} | {m.Cumulative[0] * 100,6:0.0} | {m.Cumulative[1] * 100,6:0.0} | {m.Dominant}");
            foreach (var s in mr.Stories)
                StLog($"   tầng {s.Story}: M = {s.Mass:0.00} t, CM ({s.Xm:0.00}; {s.Ym:0.00}), CR ({(double.IsNaN(s.Xr) ? "-" : s.Xr.ToString("0.00"))}; {(double.IsNaN(s.Yr) ? "-" : s.Yr.ToString("0.00"))})");

            tabView.SelectedItem = _modalTab;
            SetModalPlaying(false);
            _sldModalAmp.Value = 1;
            _cbModalView.SelectedIndex = 0;
            _cbMode.SelectedIndex = 0;
            await Pause(1500); Shot($"{tag}_43_DaoDong_Mode1");
            _cbMode.SelectedIndex = Math.Min(2, mr.Modes.Count - 1);
            await Pause(1500); Shot($"{tag}_44_DaoDong_Mode3");
            _cbMode.SelectedIndex = 0;
        }
        finally
        {
            _p.Modal = saveModal;
            _p.UseRigidDiaphragm = saveDia;
            if (_modalTab != null) SetModalPlaying(true);
        }
    }
}
