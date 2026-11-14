using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace KetCauVXT.App;

/// <summary>[Mượt khi vẽ] Giảm giật lag mặt bằng:
/// 1) Kéo / lăn chuột thu phóng: không dựng lại toàn bộ mặt bằng mỗi sự kiện chuột – dời / phóng tức thời ảnh đã vẽ (RenderTransform), dựng lại đầy đủ một lần khi ngừng thao tác 0.15 s hoặc khi nhả chuột.
/// 2) Di chuột (dây chun, bắt điểm, tô sáng, kéo tay nắm): gộp các sự kiện chuột dồn dập – chỉ xử lý vị trí mới nhất mỗi lượt
///    (chuột tần số cao 500–1000 Hz trước đây làm hàng đợi xử lý dồn lại → trễ, giật).</summary>
public partial class MainWindow
{
    private double _rOx = double.NaN, _rOy, _rScale;   // phép chiếu lúc dựng mặt bằng gần nhất
    private DispatcherTimer _quickTimer;
    private bool _quickActive;
    private Point? _pendingMove;
    private bool _moveQueued;
    internal int PlanRedrawCount { get; private set; }

    /// <summary>Gọi đầu RedrawPlan: bỏ phép dời / phóng tạm, ghi phép chiếu hiện tại.</summary>
    private void PlanRedrawBegin()
    {
        PlanRedrawCount++;
        _quickTimer?.Stop();
        if (_quickActive || planCanvas.RenderTransform != Transform.Identity)
        {
            _quickActive = false;
            planCanvas.RenderTransform = Transform.Identity;
        }
        _rOx = _ox; _rOy = _oy; _rScale = _scale;
    }

    /// <summary>Dời / phóng tức thời ảnh mặt bằng đã vẽ theo phép chiếu mới (_ox, _oy, _scale); dựng lại đầy đủ khi ngừng thao tác.</summary>
    private void PlanQuickView(int delayMs = 150)
    {
        if (double.IsNaN(_rOx) || _rScale <= 0 || planCanvas.Children.Count == 0) { RedrawPlan(); return; }
        double k = _scale / _rScale;
        _quickActive = true;
        planCanvas.RenderTransform = new MatrixTransform(k, 0, 0, k, _ox - k * _rOx, _oy - k * _rOy);
        if (planOverlay.Children.Count > 0) planOverlay.Children.Clear();
        _quickTimer ??= new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
        _quickTimer.Stop();
        _quickTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
        _quickTimer.Tick -= QuickTimer_Tick;
        _quickTimer.Tick += QuickTimer_Tick;
        _quickTimer.Start();
    }

    private void QuickTimer_Tick(object sender, EventArgs e)
    {
        _quickTimer.Stop();
        if (_panning && _dragged) { _quickTimer.Start(); return; }   // đang kéo: chờ nhả chuột
        if (_quickActive) { RedrawPlan(); DrawHover(); }
    }

    /// <summary>Nhả chuột sau khi kéo: dựng lại ngay.</summary>
    private void PlanQuickFlush()
    {
        if (!_quickActive) return;
        RedrawPlan();
    }

    /// <summary>Gộp sự kiện di chuột: xử lý vị trí mới nhất sau khi hàng đợi sự kiện vào đã trống.</summary>
    private void QueuePlanMove(Point p)
    {
        _pendingMove = p;
        if (_moveQueued) return;
        _moveQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            _moveQueued = false;
            if (_pendingMove is not Point q) return;
            _pendingMove = null;
            ProcessPlanMove(q);
        }));
    }
}
