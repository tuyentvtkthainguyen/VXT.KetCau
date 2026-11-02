using System.Windows;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Thiết lập bản vẽ (Model/DrawingStyle.cs): cửa sổ "Thiết lập bản vẽ (tỷ lệ, cỡ chữ)…" – tỷ lệ theo loại bản vẽ, cỡ chữ,
/// kiểu kích thước VXT_1-25…, chú thích tuỳ chọn. Lưu trong chuẩn CAD (mặc định người dùng / riêng dự án).</summary>
public partial class MainWindow
{
    private DrawingStyleWindow _drawStyleWin;

    /// <summary>Mở cửa sổ thiết lập bản vẽ. modal = false: không chặn (tự kiểm tra).</summary>
    private DrawingStyleWindow OpenDrawingStyle(bool modal = true, int tab = 0)
    {
        var w = new DrawingStyleWindow(_p) { Owner = this };
        w.SelectTab(tab);
        w.Closed += (s, e) =>
        {
            if (w.Changed)
            {
                MarkDirty(false);
                var st = DrawingStyle.Current;
                SetStatus($"Đã cập nhật thiết lập bản vẽ: mặt đứng dầm 1/{st.ScaleBeamElevation:0.##}, mặt cắt 1/{st.ScaleMemberSection:0.##}, " +
                          $"chữ kích thước {st.DimTextHeight:0.##} mm, ghi chú {st.NoteHeight:0.##} mm, nhãn thép {st.TagHeight:0.##} mm.");
            }
            if (ReferenceEquals(_drawStyleWin, w)) _drawStyleWin = null;
        };
        _drawStyleWin = w;
        if (modal) w.ShowDialog(); else w.Show();
        return w;
    }

    private void MnuDrawingStyle_Click(object sender, RoutedEventArgs e) => OpenDrawingStyle();
}
