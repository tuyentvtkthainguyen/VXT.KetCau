using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace KetCauVXT.App;

// =====================================================================
// [Esc thoát lệnh] Đang thực hiện lệnh vẽ cấu kiện (＋ Dầm, dầm cong, tường, ô sàn, ban công, cầu thang, cột, lan can, chi tiết KT,
// đặt cửa / đồ đạc, vẽ trục, mặt cắt, lệnh dòng lệnh, căn bản vẽ CAD, sửa dầm, vẽ đường chuẩn / móng đa giác / nhân bản móng…):
// một lần Esc là thoát hẳn lệnh về chế độ Chọn – kể cả khi con trỏ nhập đang ở ô chữ của thanh lệnh / dòng lệnh
// (trừ khi đang sửa ô bảng hoặc danh sách thả xuống đang mở – Esc thuộc về ô đó). Không có lệnh: Esc bỏ chọn như cũ.
// =====================================================================
public partial class MainWindow
{
    /// <summary>Có lệnh vẽ / thao tác bấm điểm đang chạy không.</summary>
    private bool DrawCommandActive()
    {
        if (_p == null) return false;
        if (_fndTool != FndTool.None) return true;
        if (!PlanTabActive) return false;
        return _mode != PlanEditMode.Select || _lineStart != null || _cmdStage != CmdStage.None || _adTool != AdTool.None
               || _fnPlace != null || _secTool || _rvPlaceType != null || _gridTool || _clAlignStep > 0 || _xmvStep > 0 || _rfTool != null;
    }

    /// <summary>Esc thuộc về ô đang nhập (ô bảng đang sửa, danh sách thả xuống đang mở).</summary>
    private static bool EscOwnedByControl()
    {
        var d = Keyboard.FocusedElement as DependencyObject;
        while (d != null)
        {
            if (d is DataGridCell cell && cell.IsEditing) return true;
            if (d is ComboBox cb && cb.IsDropDownOpen) return true;
            if (d is ContextMenu || d is MenuItem) return true;
            d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return false;
    }

    /// <summary>Esc: thoát hẳn lệnh vẽ đang chạy. true = đã xử lý.</summary>
    private bool EscapeDrawCommand()
    {
        if (!DrawCommandActive() || EscOwnedByControl()) return false;
        if (_fndTool != FndTool.None) return FndToolKey(Key.Escape);
        // đưa con trỏ nhập về mặt bằng để các bước huỷ không bị chặn bởi ô chữ đang có con trỏ
        _cmdBox?.Clear();
        planHost?.Focus();
        for (int i = 0; i < 8 && DrawCommandActive(); i++)
        {
            string before = StateKey();
            if (_cmdStage != CmdStage.None) CancelStage(false);
            else if (!HandlePlanEditKey(Key.Escape, false)) break;
            if (StateKey() == before)
            {
                // bước huỷ không đổi trạng thái → ép về chế độ Chọn
                if (rbModeSelect.IsChecked != true) rbModeSelect.IsChecked = true;
                break;
            }
        }
        if (planHost != null && _mode == PlanEditMode.Select) planHost.Cursor = null;
        DrawHover();
        SetStatus("Đã thoát lệnh (Esc) – chế độ Chọn. " + ModeHint(PlanEditMode.Select));
        UpdateSelectOnlyButtons();
        return true;
    }

    private string StateKey() =>
        $"{_mode}|{_lineStart}|{_cmdStage}|{_adTool}|{_fnPlace != null}|{_secTool}|{_rvPlaceType != null}|{_gridTool}|{_clAlignStep}|{_xmvStep}|{_rfTool != null}|{_multiSel.Count}|{_multiSlab.Count}";
}
