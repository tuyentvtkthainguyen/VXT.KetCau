using KetCauVXT.Model;

namespace KetCauVXT.App;

// =====================================================================
// LỰA CHỌN DÙNG CHUNG cho mọi khung nhìn (mặt bằng, 3D, biểu đồ, móng).
//   _sel        : thanh (dầm/cột) đang chọn        – khai báo trong MainWindow.xaml.cs
//   _selSlab    : ô sàn đang chọn
//   _selOpening : lỗ sàn (cầu thang / giếng trời) đang chọn
//   _selFooting : nút chân cột của móng đang chọn (0 = không)
// Mọi nơi đổi lựa chọn gọi Select(...) → OnSelectionChanged() → vẽ lại tất cả khung nhìn có tô sáng.
// =====================================================================
public partial class MainWindow
{
    private Slab _selSlab;
    private FloorOpening _selOpening;
    private int _selFooting;
    /// <summary>Đoạn tường đang chọn (MainWindow.Walls.cs)</summary>
    private WallLine _selWall;

    /// <summary>Đặt lựa chọn (chỉ một đối tượng tại một thời điểm) rồi cập nhật mọi khung nhìn.</summary>
    private void Select(FrameElement frame = null, Slab slab = null, FloorOpening opening = null, int footingNode = 0, WallLine wall = null)
    {
        _sel = frame; _selSlab = slab; _selOpening = opening; _selFooting = footingNode; _selWall = wall;
        OnSelectionChanged();
    }

    private void ClearSelection() => Select();

    private bool HasSelection => _sel != null || _selSlab != null || _selOpening != null || _selFooting != 0 || _selWall != null;

    private void OnSelectionChanged()
    {
        RedrawPlan();
        UpdateHighlight3D();
        RedrawSecondaryViews();
        ShowSelection();
        DrawDiagram();
    }

    /// <summary>Tô sáng đối tượng đang chọn trong khung 3D (cài đặt ở MainWindow.View3D.cs).</summary>
    partial void UpdateHighlight3D();

    /// <summary>Vẽ lại các khung nhìn phụ: biểu đồ nội lực, mô hình móng (cài đặt ở MainWindow.Diagrams.cs / MainWindow.Foundation.cs).</summary>
    partial void RedrawSecondaryViews();
}
