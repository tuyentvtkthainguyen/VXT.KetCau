using System.Windows;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Menu Tệp → "Mẫu định dạng (template)…": thư viện mẫu định dạng (Model/Template.cs, TemplateWindow.cs).
/// Mẫu mặc định được áp dụng cho dự án mới qua điểm cắm trong ModelBuilder.NewProject (TemplateService.AutoApplyDefault bật ở App.xaml.cs).</summary>
public partial class MainWindow
{
    private TemplateWindow _templateWin;

    /// <summary>Mở cửa sổ mẫu định dạng. modal = false: không chặn (tự kiểm tra).</summary>
    private TemplateWindow OpenTemplates(bool modal = true)
    {
        var w = new TemplateWindow(_p, _design, parts =>
        {
            if (parts.HasFlag(TemplateParts.CadStandard)) ActivateCadStandard();
            MarkDirty(false);
        }) { Owner = this };
        w.Closed += (s, e) =>
        {
            if (w.Changed) SetStatus("Đã áp dụng mẫu định dạng vào dự án.");
            if (ReferenceEquals(_templateWin, w)) _templateWin = null;
        };
        _templateWin = w;
        if (modal) w.ShowDialog(); else w.Show();
        return w;
    }

    private void MnuTemplates_Click(object sender, RoutedEventArgs e)
    {
        if (_p == null) return;
        try { OpenTemplates(); }
        catch (Exception ex) { ShowError("Lỗi mẫu định dạng", ex); }
    }
}
