using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace KetCauVXT.App;

// =====================================================================
// ĐIỀU HƯỚNG 3D KIỂU REVIT / SKETCHUP:
//   • Kéo chuột giữa: di chuyển (pan) – Shift + kéo chuột giữa: xoay quanh tâm.
//   • Kéo chuột phải: di chuyển (pan).
//   • Con lăn: thu phóng VÀO ĐIỂM DƯỚI CON TRỎ (không chỉ vào tâm).
//   • Bấm đúp chuột giữa hoặc phím F / Home: thu phóng vừa toàn mô hình.
//   • Kéo chuột trái: xoay (hoặc quét chọn khi bật "Quét chọn" / phím Q, Ctrl / Shift + kéo luôn quét) – MainWindow.Select3D.cs.
// =====================================================================
public partial class MainWindow
{
    private bool _v3Panning, _v3PanOrbit;
    private Point _v3PanStart;
    private Point3D _v3PanTarget0;
    private double _v3PanYaw0, _v3PanPitch0;
    private FrameworkElement _v3PanHost;
    private DateTime _lastMiddleClick = DateTime.MinValue;

    private (Viewport3D Vp, PerspectiveCamera Cam) Vp3D(object sender) =>
        ReferenceEquals(sender, diaView3DHost) ? (diaViewport, diaCamera) : (viewport3D, camera3D);

    private void View3D_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Right) return;
        var host = sender as FrameworkElement ?? view3DHost;
        if (host.Focusable) host.Focus();
        if (e.ChangedButton == MouseButton.Middle && e.ClickCount >= 2 || e.ChangedButton == MouseButton.Middle && (DateTime.Now - _lastMiddleClick).TotalMilliseconds < 350)
        {
            Zoom3DExtents();
            _lastMiddleClick = DateTime.MinValue;
            e.Handled = true;
            return;
        }
        if (e.ChangedButton == MouseButton.Middle) _lastMiddleClick = DateTime.Now;
        _v3Panning = true;
        _v3PanOrbit = e.ChangedButton == MouseButton.Middle && (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        _v3PanHost = host;
        _v3PanStart = e.GetPosition(host);
        _v3PanTarget0 = _target; _v3PanYaw0 = _yaw; _v3PanPitch0 = _pitch;
        host.CaptureMouse();
        host.Cursor = _v3PanOrbit ? Cursors.SizeAll : Cursors.Hand;
        e.Handled = true;
    }

    private void View3D_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_v3Panning || (e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Right)) return;
        _v3Panning = false;
        _v3PanHost?.ReleaseMouseCapture();
        if (_v3PanHost != null) _v3PanHost.Cursor = null;
        // bấm chuột phải không kéo: để menu ngữ cảnh (nếu có) xử lý
        var p = e.GetPosition(_v3PanHost);
        bool moved = Math.Abs(p.X - _v3PanStart.X) + Math.Abs(p.Y - _v3PanStart.Y) >= 4;
        if (moved || e.ChangedButton == MouseButton.Middle) e.Handled = true;
    }

    /// <summary>Kéo chuột giữa / phải đang di chuyển: pan hoặc xoay (gọi đầu View3D_MouseMove).</summary>
    private bool View3DPanMove(MouseEventArgs e)
    {
        if (!_v3Panning || _v3PanHost == null) return false;
        if (e.MiddleButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed) { _v3Panning = false; _v3PanHost.ReleaseMouseCapture(); _v3PanHost.Cursor = null; return false; }
        var p = e.GetPosition(_v3PanHost);
        double dx = p.X - _v3PanStart.X, dy = p.Y - _v3PanStart.Y;
        if (_v3PanOrbit)
        {
            _yaw = _v3PanYaw0 - dx * 0.4;
            _pitch = Math.Clamp(_v3PanPitch0 + dy * 0.4, -89, 89);
        }
        else
        {
            var (right, up) = CameraAxes();
            double w = Math.Max(1, _v3PanHost.ActualWidth);
            double fov = (camera3D?.FieldOfView ?? 45) * Math.PI / 180;
            double k = 2 * _dist * Math.Tan(fov / 2) / w;   // m / điểm ảnh tại tâm xoay
            _target = _v3PanTarget0 - right * (dx * k) + up * (dy * k);
        }
        UpdateCamera();
        return true;
    }

    private (Vector3D Right, Vector3D Up) CameraAxes()
    {
        double yaw = _yaw * Math.PI / 180, pitch = _pitch * Math.PI / 180;
        var look = -new Vector3D(Math.Cos(pitch) * Math.Cos(yaw), Math.Cos(pitch) * Math.Sin(yaw), Math.Sin(pitch));
        var right = Vector3D.CrossProduct(look, new Vector3D(0, 0, 1));
        if (right.Length < 1e-9) right = new Vector3D(1, 0, 0);
        right.Normalize();
        var up = Vector3D.CrossProduct(right, look);
        up.Normalize();
        return (right, up);
    }

    /// <summary>Thu phóng vào điểm dưới con trỏ: điểm trúng mô hình (nếu có), không thì điểm trên mặt phẳng qua tâm vuông góc hướng nhìn.</summary>
    private void Zoom3DAt(object sender, Point screen, bool zoomIn)
    {
        double f = zoomIn ? 1 / 1.15 : 1.15;
        double nd = Math.Clamp(_dist * f, 0.3, 5000);
        f = nd / _dist;
        var (vp, cam) = Vp3D(sender);
        Point3D? aim = null;
        try
        {
            VisualTreeHelper.HitTest(vp, null, r =>
            {
                if (r is RayMeshGeometry3DHitTestResult rm) { aim = rm.PointHit; return HitTestResultBehavior.Stop; }
                return HitTestResultBehavior.Continue;
            }, new PointHitTestParameters(screen));
        }
        catch (Exception) { aim = null; }
        if (aim == null && vp != null && vp.ActualWidth > 1 && cam != null)
        {
            // tia qua điểm ảnh cắt mặt phẳng qua tâm xoay
            var (right, up) = CameraAxes();
            double w = vp.ActualWidth, h = vp.ActualHeight, fov = cam.FieldOfView * Math.PI / 180;
            double k = 2 * _dist * Math.Tan(fov / 2) / w;
            aim = _target + right * ((screen.X - w / 2) * k) - up * ((screen.Y - h / 2) * k);
        }
        if (aim is Point3D a)
        {
            // giữ điểm dưới con trỏ đứng yên: tâm mới = a + (tâm − a)·f
            _target = a + (_target - a) * f;
        }
        _dist = nd;
        UpdateCamera();
    }

    /// <summary>Thu phóng vừa toàn mô hình (F / Home / bấm đúp chuột giữa).</summary>
    private void Zoom3DExtents()
    {
        FitCameraTarget(true);
        UpdateCamera();
        SetStatus("3D: thu phóng vừa toàn mô hình.");
    }
}
