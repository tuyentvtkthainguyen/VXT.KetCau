using System.Windows;
using KetCauVXT.Import.Acad;

namespace KetCauVXT.App;

// =====================================================================
// ĐIỂM VÀO RIÊNG CHO LIÊN KẾT AutoCAD (tách file để không đụng tới ArchImportDialog.xaml.cs):
// nạp thẳng dữ liệu đã tích chọn trong AutoCAD (acad_*.json) thay cho việc chọn file DXF và bấm "Nhận diện".
// =====================================================================
public partial class ArchImportDialog
{
    private string _acadJson;

    /// <summary>Kết quả đọc dữ liệu AutoCAD (null nếu hộp thoại dùng file DXF/DWG như thường).</summary>
    public AcadLinkResult AcadResult { get; private set; }

    /// <summary>Đặt file dữ liệu AutoCAD cần nạp; việc đọc + đề xuất kết cấu chạy khi cửa sổ hiện lên (dùng được với ShowDialog() và Show()).</summary>
    public void PresetAcadLink(string jsonPath)
    {
        _acadJson = jsonPath;
        Title = Title + " – dữ liệu từ AutoCAD";
        Loaded -= AcadLink_Loaded;
        Loaded += AcadLink_Loaded;
    }

    private async void AcadLink_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= AcadLink_Loaded;
        if (string.IsNullOrEmpty(_acadJson)) return;
        await LoadAcadLinkAsync(_acadJson);
    }

    /// <summary>Đọc file trao đổi AutoCAD → ArchDrawing → hiện danh sách tầng → đề xuất kết cấu.</summary>
    public async Task LoadAcadLinkAsync(string jsonPath)
    {
        if (_busy) return;
        if (!TryUiToOptions(out string err)) { Warn(err); return; }
        var o = Options;
        AcadLinkResult r = null;
        bool ok = await RunBusy("Đang đọc dữ liệu từ AutoCAD...", () => r = AcadLinkReader.Read(jsonPath, o), "Lỗi đọc dữ liệu AutoCAD");
        if (!ok || r == null) return;
        AcadResult = r;
        Drawing = r.Drawing;
        Proposal = null;
        dgFloors.ItemsSource = null;
        dgFloors.ItemsSource = Drawing.Floors;
        btnPropose.IsEnabled = Drawing.Floors.Count > 0;
        UpdateAfterProposal();
        SetStatus($"Dữ liệu AutoCAD: {Drawing.Floors.Count} tầng ({r})" + (Drawing.Warnings.Count > 0 ? $", {Drawing.Warnings.Count} cảnh báo." : "."));
        if (!Drawing.Floors.Any(f => f.Walls.Count > 0))
        {
            Warn("Dữ liệu AutoCAD không có tầng nào nhận diện được tường.\n\nTrong AutoCAD: gán nhóm \"Tường\" (VXT_TUONG), khai báo tầng (VXT_TANG) rồi gửi lại (VXT_GUI).");
            return;
        }
        await DoPropose();
    }
}
