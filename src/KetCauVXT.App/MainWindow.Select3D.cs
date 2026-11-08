using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using KetCauVXT.Model;
using Shapes = System.Windows.Shapes;

namespace KetCauVXT.App;

// =====================================================================
// QUÉT CHỌN TRONG KHUNG NHÌN 3D: bật "Quét chọn" trên thanh nổi 3D (phím Q) rồi kéo khung; Ctrl/Shift + kéo luôn quét
//   trái → phải: khung bao (xanh dương, nằm trọn) · phải → trái: khung cắt (xanh lá nét đứt, chạm)
//   Ctrl: thêm · Shift: bỏ · Delete: xoá nhóm · Esc: bỏ chọn. Kết quả dùng chung nhóm chọn của mặt bằng (_mixSel).
// Lõi: Model/InteractionOps.cs (Screen3DSelect).
// =====================================================================
public partial class MainWindow
{
    private CheckBox _v3BoxToggle;
    private bool _v3Boxing;
    private Point _v3BoxA;
    private Shapes.Rectangle _v3BoxRect;

    /// <summary>Thêm ô "Quét chọn" vào thanh nổi 3D (gọi sau khi thanh được tạo).</summary>
    private void EnsureBox3DToggle()
    {
        if (_v3BoxToggle != null || _v3Bar?.Child is not StackPanel sp) return;
        _v3BoxToggle = new CheckBox
        {
            Content = "Quét chọn (kéo khung)", Foreground = Brushes.WhiteSmoke, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0),
            ToolTip = "Bật (phím Q): kéo trái → phải chọn đối tượng nằm trọn khung, phải → trái chọn đối tượng chạm khung; Ctrl thêm, Shift bỏ.\n"
                    + "Tắt: kéo để xoay; Ctrl + kéo vẫn quét thêm, Shift + kéo vẫn quét bỏ."
        };
        sp.Children.Add(_v3BoxToggle);
        var bFit = new Button { Content = "Vừa màn hình", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(6, 1, 6, 1), ToolTip = "Thu phóng vừa toàn mô hình (phím F / Home, bấm đúp chuột giữa)" };
        bFit.Click += (s, e) => Zoom3DExtents();
        sp.Children.Add(bFit);
        view3DHost.Focusable = true; view3DHost.FocusVisualStyle = null;   // nhận phím Q, F, Home khi bấm vào khung 3D
        view3DHost.PreviewKeyDown += (s, e) =>
        {
            if (IsTextInputFocused() || e.Handled) return;
            if ((e.Key == Key.F || e.Key == Key.Home) && Keyboard.Modifiers == ModifierKeys.None) { Zoom3DExtents(); e.Handled = true; return; }
            if (e.Key == Key.Q && Keyboard.Modifiers == ModifierKeys.None)
            {
                _v3BoxToggle.IsChecked = _v3BoxToggle.IsChecked != true;
                SetStatus(_v3BoxToggle.IsChecked == true ? "3D: BẬT quét chọn – kéo khung để chọn (Q để tắt)." : "3D: TẮT quét chọn – kéo để xoay (Ctrl/Shift + kéo vẫn quét).");
                e.Handled = true;
            }
            else if (e.Key == Key.Delete && _mixSel.Count > 0) { DeleteMixedSelection(); Build3D(); e.Handled = true; }
            else if (e.Key == Key.Escape && _mixSel.Count > 0) { ClearMixedSelection(); Build3D(); e.Handled = true; }
        };
    }

    private bool View3DBoxStart(object sender, MouseButtonEventArgs e)
    {
        EnsureBox3DToggle();
        bool modDrag = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;   // Ctrl/Shift + kéo: luôn quét
        if ((_v3BoxToggle?.IsChecked != true && !modDrag) || !ReferenceEquals(sender, view3DHost) || view3DHost.Child is not Grid g) return false;
        _v3Boxing = true;
        _v3BoxA = e.GetPosition(viewport3D);
        _v3BoxRect ??= new Shapes.Rectangle { StrokeThickness = 1.2, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        if (!g.Children.Contains(_v3BoxRect)) g.Children.Add(_v3BoxRect);
        _v3BoxRect.Width = 0; _v3BoxRect.Height = 0; _v3BoxRect.Visibility = Visibility.Visible;
        view3DHost.CaptureMouse();
        view3DHost.Focus();
        return true;
    }

    private bool View3DBoxMove(MouseEventArgs e)
    {
        if (!_v3Boxing || _v3BoxRect == null) return false;
        var b = e.GetPosition(viewport3D);
        bool crossing = b.X < _v3BoxA.X;
        _v3BoxRect.Stroke = crossing ? Brushes.LimeGreen : Brushes.DeepSkyBlue;
        _v3BoxRect.StrokeDashArray = crossing ? new DoubleCollection { 4, 3 } : null;
        _v3BoxRect.Fill = new SolidColorBrush(crossing ? Color.FromArgb(30, 50, 205, 50) : Color.FromArgb(30, 0, 191, 255));
        _v3BoxRect.Margin = new Thickness(Math.Min(_v3BoxA.X, b.X), Math.Min(_v3BoxA.Y, b.Y), 0, 0);
        _v3BoxRect.Width = Math.Abs(b.X - _v3BoxA.X);
        _v3BoxRect.Height = Math.Abs(b.Y - _v3BoxA.Y);
        return true;
    }

    private bool View3DBoxEnd(MouseButtonEventArgs e)
    {
        if (!_v3Boxing) return false;
        _v3Boxing = false;
        view3DHost.ReleaseMouseCapture();
        if (_v3BoxRect != null) _v3BoxRect.Visibility = Visibility.Collapsed;
        if (_p == null) return true;
        var b = e.GetPosition(viewport3D);
        if (Math.Abs(b.X - _v3BoxA.X) + Math.Abs(b.Y - _v3BoxA.Y) < 4)   // bấm không kéo: chọn 1 đối tượng như thường
        {
            SelectObject(Pick3D(viewport3D, b, _pick3D));
            return true;
        }
        bool crossing = b.X < _v3BoxA.X;
        List<Solid> solids;
        try { solids = _v3Cache ?? ModelSolids.Build(_p, null, false); } catch (Exception) { solids = new List<Solid>(); }
        var vis = solids.Where(s => Show3D(s.Id, s.Discipline)).ToList();
        var cam = camera3D;
        var pos = new Vec3(cam.Position.X, cam.Position.Y, cam.Position.Z);
        var look = new Vec3(cam.LookDirection.X, cam.LookDirection.Y, cam.LookDirection.Z);
        var up = new Vec3(cam.UpDirection.X, cam.UpDirection.Y, cam.UpDirection.Z);
        var hit = Screen3DSelect.Select(vis, pos, look, up, cam.FieldOfView, viewport3D.ActualWidth, viewport3D.ActualHeight,
            SelectionRules.Box.Of(_v3BoxA.X, _v3BoxA.Y, b.X, b.Y), crossing);
        var picked = Screen3DSelect.ToSelItems(hit);
        var mods = Keyboard.Modifiers;
        var mode = (mods & ModifierKeys.Shift) != 0 ? SelMode.Remove : (mods & ModifierKeys.Control) != 0 ? SelMode.Add : SelMode.Replace;
        var res = MixedSelection.Combine(mode == SelMode.Replace ? null : _mixSel, picked, mode);
        if (mode == SelMode.Replace && HasSelection) ClearSelection();
        _mixSel.Clear(); _mixSel.AddRange(res);
        SyncMultiFromMixed();
        DrawHover();
        SetStatus($"3D – đã chọn: {MixedSelection.Summary(_mixSel)} (khung {(crossing ? "cắt – chạm khung" : "bao – nằm trọn")}). "
                  + "Ctrl + kéo: thêm · Shift + kéo: bỏ · Bộ lọc… · Delete: xoá · Esc: bỏ chọn.");
        return true;
    }
}
