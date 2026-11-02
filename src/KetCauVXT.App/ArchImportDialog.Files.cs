using System.Windows;

namespace KetCauVXT.App;

// =====================================================================
// NẠP SẴN FILE BẢN VẼ (DWG/DXF) – tách file để không đụng tới ArchImportDialog.xaml.cs:
// dùng cho tự kiểm tra giao diện (đọc DWG) hoặc mở hộp thoại từ chỗ khác với file đã chọn.
// =====================================================================
public partial class ArchImportDialog
{
    private bool _presetAutoRead;

    /// <summary>Thay danh sách file bằng các file cho trước (.dwg/.dxf). autoRead: khi cửa sổ hiện lên tự bấm "Nhận diện"
    /// (đọc bản vẽ + đề xuất kết cấu); dùng được với ShowDialog() và Show().</summary>
    public void PresetFiles(IEnumerable<string> paths, bool autoRead = true)
    {
        _files.Clear();
        foreach (var f in paths ?? Enumerable.Empty<string>())
            if (!string.IsNullOrWhiteSpace(f) && !_files.Contains(f, StringComparer.OrdinalIgnoreCase)) _files.Add(f);
        RefreshFiles();
        MarkLayersStale();
        _presetAutoRead = autoRead && _files.Count > 0;
        Loaded -= PresetFiles_Loaded;
        if (_presetAutoRead) Loaded += PresetFiles_Loaded;
    }

    private void PresetFiles_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= PresetFiles_Loaded;
        if (!_presetAutoRead) return;
        _presetAutoRead = false;
        BtnRead_Click(this, new RoutedEventArgs());
    }
}
