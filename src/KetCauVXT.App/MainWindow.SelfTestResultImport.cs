using System.IO;
using KetCauVXT.Design;
using KetCauVXT.Export;
using ConcreteDesignerST = KetCauVXT.Design.Concrete.ConcreteDesigner;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): xuất nội lực hiện tại thành bảng CSV kiểu ETABS, mở hộp thoại nhập kết quả (không modal) với file đó,
/// chụp ảnh, áp dụng + thiết kế, so sánh thép một dầm với trước khi nhập, rồi khôi phục kết quả phân tích của chương trình.</summary>
public partial class MainWindow
{
    private static string BeamSig(BeamLineDesign b) =>
        b == null ? "(không có dầm)" : b.Name + ": " + string.Join("; ", b.Bars.Select(x => $"{x.Role} {x.Count}Ø{x.Dia} L={x.Length:0.00}"));

    private async Task SelfTestResultImport(string tag)
    {
        if (_res == null || _res.External != null)
        {
            _suppressImportedConfirm = true;
            try { if (!await RunAnalysis()) throw new Exception("Phân tích lỗi trước khi thử nhập kết quả"); }
            finally { _suppressImportedConfirm = false; }
        }
        var own = _res;
        if (_design == null) { _design = ConcreteDesignerST.Design(_p, own); RefreshDesign(); }
        var beam0 = _design.Beams.FirstOrDefault();
        string before = BeamSig(beam0);

        string csv = Path.Combine(SelfTestOut, tag + "_NoiLuc_kieuETABS.csv");
        ResultTableWriter.Write(own, csv, new ResultTableWriter.Options { Etabs = true });
        StLog("nhập kết quả: đã ghi bảng kiểu ETABS " + csv + $" ({new FileInfo(csv).Length} byte)");

        var dlg = new ResultImportDialog(_p, own, ApplyImportedResultsAsync) { Owner = this, Width = 1300, Height = 800 };
        dlg.Show();
        dlg.AddFiles(new[] { csv });
        await Pause(800);
        if (beam0 != null && beam0.Frames.Count > 0)
        {
            dlg.SelectTab(5);
            dlg.PreviewFrame(beam0.Frames[0]);
            await Pause(800);
            ShotWindow(dlg, $"{tag}_33_NhapKetQua_XemTruoc");
        }
        dlg.SelectTab(2);
        await Pause(800);
        ShotWindow(dlg, $"{tag}_33_NhapKetQua");
        bool ok = await dlg.ApplyAsync(true);
        dlg.Close();
        if (!ok || _res?.External == null) throw new Exception("Không áp dụng được nội lực nhập");
        var x = _res.External;
        StLog($"nhập kết quả: nguồn {x.Software}, {x.MatchedFrames}/{_p.Frames.Count} thanh, {_res.Cases.Count} trường hợp/tổ hợp, TTGH I {_res.DesignCombos().Count}, ghi chú {x.Notes.Count}");
        await Pause(600);
        Shot($"{tag}_34_NoiLucNhap");
        var beam1 = _design?.Beams.FirstOrDefault(b => beam0 != null && b.Name == beam0.Name);
        string after = BeamSig(beam1);
        StLog((before == after ? "nhập kết quả: thép dầm GIỐNG trước khi nhập – " : "nhập kết quả: thép dầm KHÁC trước khi nhập – ") + before + " | " + after);

        // khôi phục kết quả của chương trình
        _suppressImportedConfirm = true;
        try { if (!await RunAnalysis()) throw new Exception("Không khôi phục được kết quả phân tích"); }
        finally { _suppressImportedConfirm = false; }
        _design = ConcreteDesignerST.Design(_p, _res);
        RefreshDesign();
        StLog("nhập kết quả: đã khôi phục nội lực của chương trình (" + _res.Source + ")");
    }
}
