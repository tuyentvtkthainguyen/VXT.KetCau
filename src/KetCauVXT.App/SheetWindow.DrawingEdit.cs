using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Dàn trang: "Hiệu chỉnh tờ…" (DrawingEditWindow), tờ đã khoá (bản sửa trong CAD – vẽ từ file đã nạp, kèm nhãn cảnh báo),
/// xuất PDF tách rời từng tờ.</summary>
public partial class SheetWindow
{
    /// <summary>Hình cả tờ khi tờ đã khoá (mm giấy, y xuống); null = tờ không khoá.</summary>
    private DrawingGroup FrozenSheetDrawing(Sheet s)
    {
        var fz = DrawingOverlay.FrozenOf(_p, DrawingOverlay.SheetKey(s));
        if (fz == null) return null;
        var w = DrawingOverlay.FrozenWriter(fz);
        if (w == null || w.EntityCount == 0) return null;
        var (pw, ph) = s.PaperSize();
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pw, ph));
            dc.DrawDrawing(SheetRender.Render(new[] { (w, 1.0, 0.0, 0.0) }, 0, ph, 0, PrimsOf));
        }
        dg.Freeze();
        return dg;
    }

    /// <summary>Tờ đã khoá: phủ bản đã nạp lên trang (che các khung nhìn tự sinh) và hiện nhãn cảnh báo.</summary>
    private void ShowFrozenOverlay(double pw, double ph)
    {
        if (_sheet == null) return;
        var fz = DrawingOverlay.FrozenOf(_p, DrawingOverlay.SheetKey(_sheet));
        if (fz == null) return;
        var w = DrawingOverlay.FrozenWriter(fz);
        if (w == null || w.EntityCount == 0) return;
        var host = new DrawingHost { Width = pw, Height = ph, IsHitTestVisible = false, Background = Brushes.White };
        host.Drawing = SheetRender.Render(new[] { (w, 1.0, 0.0, 0.0) }, 0, ph, MinStroke, PrimsOf);
        pageCanvas.Children.Add(host);
        string msg = SheetBuilder.FrozenSheetWarning(_p, _r, _d, _sheet, fz);
        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 200, 40, 40)), Padding = new Thickness(2, 1, 2, 1), IsHitTestVisible = false,
            Child = new TextBlock { Text = "ĐÃ KHOÁ – " + msg, Foreground = Brushes.White, FontSize = Math.Max(2.5, pw / 120), FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, MaxWidth = pw * 0.8 }
        };
        Canvas.SetLeft(badge, 4); Canvas.SetTop(badge, 4);
        pageCanvas.Children.Add(badge);
        SetStatus(msg + " (Hiệu chỉnh tờ… → Bỏ khoá để dựng lại.)");
    }

    private void BtnEditDrawing_Click(object sender, RoutedEventArgs e)
    {
        if (_sheet == null) { SetStatus("Chọn một tờ trước."); return; }
        try
        {
            var w = new DrawingEditWindow(_p, _r, _d, Dirty, DrawingOverlay.SheetKey(_sheet)) { Owner = this };
            w.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show(this, "Lỗi hiệu chỉnh tờ: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        // chỉnh sửa của từng hình áp trong SheetBuilder.Figures → dựng lại bộ đệm hình
        _figCache.Clear();
        _primCache.Clear();
        RebuildPage();
    }

    /// <summary>PDF tách rời: mỗi tờ một file trong thư mục (tên theo mẫu, mặc định "số_tên.pdf").</summary>
    private void ExportPdfSplit(IList<Sheet> sheets, DrawingExportOptions o)
    {
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục lưu PDF (mỗi tờ một file)" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            foreach (var s in sheets)
            {
                i++;
                string nm = string.IsNullOrWhiteSpace(o.FilePattern) ? Path.GetFileNameWithoutExtension(SheetBuilder.FileName(s))
                    : DrawingExportOptions.ApplyPattern(o.FilePattern, s.Number, s.Name, _p.Name, i);
                string fn = nm;
                for (int k = 2; !used.Add(fn); k++) fn = nm + "_" + k;
                ExportPdf(Path.Combine(dlg.FolderName, fn + ".pdf"), new List<Sheet> { s });
            }
            SetStatus($"Đã xuất {sheets.Count} file PDF (mỗi tờ một file) vào {dlg.FolderName}.");
        }
        catch (Exception ex) { MessageBox.Show(this, "Lỗi xuất PDF: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { Mouse.OverrideCursor = null; }
    }
}
