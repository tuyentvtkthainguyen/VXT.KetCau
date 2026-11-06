using System.Windows;
using System.Windows.Controls;

namespace KetCauVXT.App;

/// <summary>[Bộ môn điện nước] Mở môi trường Điện nước (MepWindow) – kiến trúc là tham chiếu, điện nước tự cập nhật khi kiến trúc đổi.</summary>
public partial class MainWindow
{
    private MepWindow _mepWin;

    /// <summary>Thêm mục "Môi trường Điện nước…" vào menu Thiết kế (gọi trong constructor trước InitCompactUi để Ctrl+Q tìm được).</summary>
    private void InitMepUi()
    {
        try
        {
            var mi = new MenuItem { Header = "Môi trường _Điện nước (cấp thoát nước, điện)…", ToolTip = "Đặt thiết bị điện nước chôn ngầm trong tường, nối tuyến, tự chọn cỡ, cảnh báo khi kiến trúc thay đổi, xuất bản vẽ DN-" };
            mi.Click += (_, _) => OpenMepEnvironment();
            MenuItem host = null;
            foreach (var o in mainMenu.Items)
                if (o is MenuItem m && (m.Header as string ?? "").Replace("_", "") == "Thiết kế") { host = m; break; }
            if (host != null) { host.Items.Add(new Separator()); host.Items.Add(mi); }
            else mainMenu.Items.Add(mi);
        }
        catch { }
    }

    /// <summary>Mở (hoặc đưa lên trước) môi trường Điện nước cho dự án đang mở.</summary>
    public void OpenMepEnvironment()
    {
        if (_mepWin != null && _mepWin.IsLoaded && ReferenceEquals(_mepWin.Tag, _p)) { _mepWin.Activate(); return; }
        if (_mepWin != null && _mepWin.IsLoaded) _mepWin.Close();
        _mepWin = new MepWindow(_p, Snapshot, PushUndo, () => { MarkDirty(false); }) { Owner = this, Tag = _p };
        _mepWin.Closed += (_, _) => { _mepWin = null; };
        _mepWin.Show();
    }
}
