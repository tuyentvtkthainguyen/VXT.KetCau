using System.Windows;
using System.Windows.Input;
using KetCauVXT.Mep;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Kéo đối tượng điều hoà – thông gió trong khung 3D chính: dàn lạnh / dàn nóng / miệng gió / ống gió / ống đồng
/// (chọn khối rồi kéo trên mặt phẳng ngang qua điểm neo, bắt bước 5 cm; dời dàn → ống đồng, ống ngưng đi lại tuyến; ống gió → phụ kiện sinh lại).
/// Lõi: Mep/MepHvacEdit.cs (HvacEdit.FromSolidId / Anchor / Move).</summary>
public partial class MainWindow
{
    private HvacElemKind _v3HvKind;
    private int _v3HvId;
    private double _v3HvDoneX, _v3HvDoneY;

    /// <summary>Khối 3D thuộc cùng đối tượng ĐHKK đang chọn (tô sáng cả tuyến ống gió, không chỉ một đoạn).</summary>
    private bool HvacSame3D(string id)
    {
        if (_v3SelId == null || id == null || !id.StartsWith("DN-", StringComparison.Ordinal) || !_v3SelId.StartsWith("DN-", StringComparison.Ordinal)) return false;
        var a = HvacEdit.FromSolidId(_v3SelId);
        if (a == null) return false;
        var b = HvacEdit.FromSolidId(id);
        return b != null && b.Value.Kind == a.Value.Kind && b.Value.Id == a.Value.Id;
    }

    /// <summary>Gọi từ View3DDragStart khi khối chọn không phải đồ đạc / thiết bị điện nước. true = bắt đầu kéo ĐHKK.</summary>
    private bool HvacDrag3DStart(MouseButtonEventArgs e)
    {
        var it = HvacEdit.FromSolidId(_v3SelId);
        if (it == null || _p?.Mep?.Hvac == null) return false;
        var pt = e.GetPosition(viewport3D);
        var hit = Pick3D(viewport3D, pt, _pick3D) as Solid;
        var hi = HvacEdit.FromSolidId(hit?.Id);
        if (hi == null || hi.Value.Kind != it.Value.Kind || hi.Value.Id != it.Value.Id) return false;
        var a = HvacEdit.Anchor(_p, it.Value.Kind, it.Value.Id);
        if (a == null) return false;
        _v3Drag = true; _v3Moved = false; _v3DragDev = 0;
        _v3HvKind = it.Value.Kind; _v3HvId = it.Value.Id; _v3HvDoneX = 0; _v3HvDoneY = 0;
        _v3DragRay = CameraRayAt(pt);
        _v3X0 = a.Value.X; _v3Y0 = a.Value.Y; _v3Z = a.Value.Z;
        _v3DragSnap = Snapshot();
        view3DHost.CaptureMouse();
        e.Handled = true;
        return true;
    }

    /// <summary>Gọi từ View3DDragMove khi đang kéo ĐHKK.</summary>
    private bool HvacDrag3DMove(MouseEventArgs e)
    {
        var q = Pick3DMath.Drag(_v3DragRay, CameraRayAt(e.GetPosition(viewport3D)), _v3Z, _v3X0, _v3Y0);
        if (q == null) return true;
        const double step = 0.05;
        double tx = Math.Round((q.Value.X - _v3X0) / step) * step, ty = Math.Round((q.Value.Y - _v3Y0) / step) * step;
        double dx = tx - _v3HvDoneX, dy = ty - _v3HvDoneY;
        if (Math.Abs(dx) + Math.Abs(dy) < 1e-9) return true;
        if (!HvacEdit.Move(_p, _v3HvKind, _v3HvId, dx, dy)) { _v3Drag = false; _v3HvId = 0; return false; }
        _v3HvDoneX = tx; _v3HvDoneY = ty;
        _v3Moved = true;
        Build3D();
        SetStatus($"Kéo đối tượng điều hoà – thông gió: Δx = {tx:0.00}, Δy = {ty:0.00} m (bước 5 cm)");
        return true;
    }

    /// <summary>Gọi từ View3DDragEnd: cập nhật điện nước khi đã kéo ĐHKK.</summary>
    private void HvacDrag3DEnd()
    {
        if (_v3HvId > 0 && _v3Moved)
        {
            try { HvacRouting.RebuildFittings(_p); MepUpdater.Reconcile(_p); } catch (Exception) { /* bỏ qua */ }
        }
        _v3HvId = 0;
    }

    /// <summary>Xoá đối tượng ĐHKK đang chọn trong 3D. true = đã xử lý.</summary>
    private bool HvacDelete3D()
    {
        var it = HvacEdit.FromSolidId(_v3SelId);
        if (it == null || _p?.Mep?.Hvac == null) return false;
        string snap = Snapshot();
        if (!HvacEdit.Delete(_p, it.Value.Kind, it.Value.Id)) return false;
        try { MepUpdater.Reconcile(_p); } catch (Exception) { /* bỏ qua */ }
        PushUndo(snap);
        _v3SelId = null;
        FurnishChanged();
        return true;
    }
}
