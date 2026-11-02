using System.IO;
using System.Windows;
using KetCauVXT.Report;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): bảng khối lượng vật tư + thuyết minh tự động (không hộp thoại modal).</summary>
public partial class MainWindow
{
    private async Task SelfTestQuantity(string tag)
    {
        // ---- bảng khối lượng vật tư ----
        var w = new QuantityWindow(_p, _design, () => MarkDirty(false)) { Owner = this, Width = 1400, Height = 850 };
        w.Show();
        await Pause(1500);
        ShotWindow(w, $"{tag}_25_KhoiLuong");
        var q = w.Result;
        if (q == null) throw new Exception("Không tính được bảng khối lượng");
        StLog($"khối lượng: bê tông {q.ConcreteTotal:0.00} m³, lót {q.LeanTotal:0.00} m³, ván khuôn {q.FormworkTotal:0.0} m², thép {q.RebarTotal:0.0} kg, xây {q.MasonryTotal:0.00} m³, {q.Materials.Count} vật tư, {q.Warnings.Count} lưu ý");
        string xlsx = Path.Combine(SelfTestOut, tag + "_" + QuantityReport.DefaultFileName);
        w.ExportExcelTo(xlsx);
        StLog("bảng khối lượng Excel: " + xlsx + (File.Exists(xlsx) ? $" ({new FileInfo(xlsx).Length} byte)" : " – KHÔNG CÓ FILE"));
        w.Close();

        // ---- thuyết minh tự động ----
        var opts = ReportOptions.FromProject(_p);
        opts.ExamplesPerType = 2;
        opts.IncludeForceTables = true;
        if (string.IsNullOrWhiteSpace(opts.Preparer)) opts.Preparer = "Người lập (tự kiểm tra)";
        var dlg = new AutoReportDialog(_p, opts, null) { Owner = this };
        dlg.Show();
        await Pause(1500);
        ShotWindow(dlg, $"{tag}_26_ThuyetMinh");
        dlg.Close();
        string docx = Path.Combine(SelfTestOut, tag + "_ThuyetMinhTuDong.docx");
        GenerateAutoReport(opts, docx);
        StLog("thuyết minh tự động: " + docx + (File.Exists(docx) ? $" ({new FileInfo(docx).Length} byte)" : " – KHÔNG CÓ FILE"));
    }
}
