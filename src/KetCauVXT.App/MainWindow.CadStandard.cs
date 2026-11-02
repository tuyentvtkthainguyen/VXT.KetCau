using System.Windows;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Chuẩn layer CAD (Model/CadStandard.cs): cửa sổ "Chuẩn layer CAD…", áp dụng chuẩn khi mở dự án,
/// hỏi "Xem/đổi tên layer trước khi xuất" DXF.</summary>
public partial class MainWindow
{
    /// <summary>Áp dụng chuẩn của dự án (hoặc mặc định của người dùng) cho bộ ghi DXF, Word và tờ in.</summary>
    private void ActivateCadStandard()
    {
        try { CadStandard.Activate(CadStandard.EffectiveFor(_p)); }
        catch (Exception) { CadStandard.Activate(null); }
    }

    private CadStandardWindow _cadStdWin;

    /// <summary>Mở cửa sổ "Chuẩn layer CAD" (tab 0: nhận diện layer kiến trúc, 1: layer bản vẽ kết cấu). modal = false: không chặn (tự kiểm tra).</summary>
    private CadStandardWindow OpenCadStandard(int tab = 0, bool modal = true)
    {
        var w = new CadStandardWindow(_p) { Owner = this };
        w.SelectTab(tab);
        w.Closed += (s, e) =>
        {
            if (w.Changed) { MarkDirty(false); SetStatus("Đã cập nhật chuẩn layer CAD: layer " + string.Join(", ", (CadStandard.Active?.OutputLayers ?? CadStandard.PresetVxt()).Skip(1).Take(4).Select(o => o.Name)) + "…, font " + CadStandard.ActiveFontName + "."); }
            if (ReferenceEquals(_cadStdWin, w)) _cadStdWin = null;
        };
        _cadStdWin = w;
        if (modal) w.ShowDialog(); else w.Show();
        return w;
    }

    private void MnuCadStandard_Click(object sender, RoutedEventArgs e) => OpenCadStandard();

    /// <summary>Gọi trước khi xuất DXF: hiện "Xem/đổi tên layer trước khi xuất" (trừ khi "Không hỏi lại").
    /// Trả về đối tượng phải Dispose sau khi xuất xong (chuẩn chỉ áp dụng cho lần xuất này), null = người dùng huỷ.
    /// <code>using var scope = BeginDxfExport(ExportLayerProbe.ForDrawingSet()); if (scope == null) return; … xuất …</code></summary>
    internal IDisposable BeginDxfExport(IEnumerable<string> usedLayers, string what = "Xuất bản vẽ DXF", Window owner = null)
    {
        var std = LayerExportDialog.Review(owner ?? this, _p, usedLayers, what);
        return std == null ? null : CadStandard.Use(std);
    }
}
