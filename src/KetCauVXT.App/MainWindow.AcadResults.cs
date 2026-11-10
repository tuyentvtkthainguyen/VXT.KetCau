using System.IO;
using System.Windows;
using KetCauVXT.Drawing;
using KetCauVXT.Import.Acad;

namespace KetCauVXT.App;

/// <summary>[Kết quả về AutoCAD] Gửi kết quả thiết kế ngược về AutoCAD: tên cấu kiện, tiết diện, thép (cột, dầm, sàn), khung móng / đài cọc
/// theo đúng vị trí trên bản vẽ gốc (bản vẽ liên kết) + bộ bản vẽ chi tiết rời (không khung tên, mỗi chi tiết một khung bao).
/// Ghi vào %AppData%\KetCauVXT\Link\Back – trong AutoCAD gõ VXT_NHAN (vẽ kết quả) / VXT_CHEN (chèn chi tiết). Chỉ ghi file cục bộ.</summary>
public partial class MainWindow
{
    private async void MnuAcadResults_Click(object sender, RoutedEventArgs e) => await SendResultsToAcad();

    private async Task SendResultsToAcad()
    {
        if (_busy || _p == null || !await EnsureDesign()) return;
        var p = _p; var ds = _design;
        AcadResultPackage pk = null;
        string path = null;
        DrawingExportResult er = null;
        string folder = Path.Combine(AcadResults.BackDir, "ChiTiet_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        bool ok = await RunBusy("Lỗi gửi kết quả về AutoCAD", prog =>
        {
            prog.Report("Đang dựng kết quả theo toạ độ bản vẽ...");
            pk = AcadResults.Build(p, ds);
            prog.Report("Đang xuất bản vẽ chi tiết rời (không khung tên)...");
            Directory.CreateDirectory(folder);
            er = DrawingSet.Export(p, ds, folder, new DrawingExportOptions { Mode = DrawingExportMode.Split, DetailsOnly = true });
            pk.Details.AddRange(er?.Files ?? new List<string>());
            path = AcadResults.Write(pk);
        });
        if (!ok || pk == null) return;
        int nT = pk.Items.Count(i => i.Type == "text"), nP = pk.Items.Count(i => i.Type == "poly");
        string msg = $"Đã ghi kết quả cho AutoCAD:\n{path}\n\n• {nT} ghi chú (tên, tiết diện, thép), {nP} khung cột / móng / đài cọc"
                     + (pk.Aligned ? " – đúng vị trí trên bản vẽ gốc (theo bản vẽ liên kết)." : " – theo toạ độ mô hình (VXT_NHAN sẽ hỏi điểm chèn).")
                     + $"\n• {pk.Details.Count} bản vẽ chi tiết rời (không khung tên, mỗi chi tiết một khung bao):\n{folder}"
                     + "\n\nTrong AutoCAD (đã nạp add-in Việt Const): gõ VXT_NHAN để vẽ kết quả lên bản vẽ, VXT_CHEN để chèn các chi tiết rồi tự dàn trang in."
                     + (pk.Notes.Count > 0 ? "\n\n" + string.Join("\n", pk.Notes) : "");
        SetStatus($"Đã gửi kết quả về AutoCAD: {nT + nP} đối tượng, {pk.Details.Count} bản vẽ chi tiết.");
        MessageBox.Show(this, msg, AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
