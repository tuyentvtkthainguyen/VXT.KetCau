using System.Windows;
using Microsoft.Win32;
using KetCauVXT.Drawing;
using KetCauVXT.Import;

namespace KetCauVXT.App;

/// <summary>Menu Xuất → "Hiệu chỉnh bản vẽ…" và "Nạp lại bản vẽ đã chỉnh sửa (DXF/DWG)…" (DrawingEditWindow.cs, Drawing/DrawingOverlay.cs);
/// thông báo sau khi xuất kèm cảnh báo bản vẽ đã khoá và chỉnh sửa không còn áp dụng được.</summary>
public partial class MainWindow
{
    private DrawingEditWindow _drawEditWin;

    /// <summary>Mở cửa sổ hiệu chỉnh bản vẽ (không chặn). startKey: khoá bản vẽ cần mở ("DS:KC-04_Dam_Tang_2"…).</summary>
    private DrawingEditWindow OpenDrawingEditor(string startKey = null)
    {
        if (_drawEditWin != null && _drawEditWin.IsLoaded)
        {
            _drawEditWin.Activate();
            if (startKey != null) _drawEditWin.SelectDrawing(startKey);
            return _drawEditWin;
        }
        var w = new DrawingEditWindow(_p, _res, _design, () => MarkDirty(false), startKey) { Owner = this };
        w.Closed += (s, e) => { if (ReferenceEquals(_drawEditWin, w)) _drawEditWin = null; };
        _drawEditWin = w;
        w.Show();
        return w;
    }

    private async void MnuDrawingEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !await EnsureDesign()) return;
        try { OpenDrawingEditor(); }
        catch (Exception ex) { ShowError("Lỗi mở cửa sổ hiệu chỉnh bản vẽ", ex); }
    }

    private async void MnuDrawingAttach_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || !await EnsureDesign()) return;
        var dlg = new OpenFileDialog { Filter = CadFileReaders.OpenFilter, Title = "Nạp lại bản vẽ đã chỉnh sửa trong CAD (DXF/DWG)" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var w = OpenDrawingEditor();
            if (!w.SelectByFileName(dlg.FileName))
                MessageBox.Show(w, "Tên file không trùng bản vẽ nào của bộ bản vẽ (KC-xx, gia công, tờ dàn trang).\nChọn bản vẽ cần khoá ở danh sách bên trái rồi bấm \"Nạp lại bản vẽ đã chỉnh sửa\".",
                    AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            else w.AttachFile(dlg.FileName, true);
        }
        catch (Exception ex) { ShowError("Lỗi nạp bản vẽ đã chỉnh sửa", ex); }
    }

    /// <summary>Thông báo xuất xong + cảnh báo (bản vẽ đã khoá, chỉnh sửa không còn áp dụng được).</summary>
    private void DoneExport(string msg, DrawingExportResult er)
    {
        var notes = er?.Messages ?? new List<string>();
        if (notes.Count == 0) { Done(msg); return; }
        msg += "\n\nLưu ý:\n- " + string.Join("\n- ", notes.Take(15)) + (notes.Count > 15 ? $"\n… (+{notes.Count - 15})" : "");
        SetStatus(msg.Replace("\n", " "));
        MessageBox.Show(msg, AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
