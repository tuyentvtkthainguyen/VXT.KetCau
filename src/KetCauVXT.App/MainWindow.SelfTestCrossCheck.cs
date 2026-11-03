using System.IO;
using KetCauVXT.Export;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest): ghi kết quả hiện tại thành bảng CSV kiểu ETABS, mở cửa sổ "So sánh với ETABS/SAP/SAFE" (không modal)
/// với file đó, chạy so sánh (sai lệch phải ≈ 0 %), chụp ảnh, xuất Excel + Word vào thư mục tự kiểm tra rồi đóng.</summary>
public partial class MainWindow
{
    private async Task SelfTestCrossCheck(string tag)
    {
        if (_res == null || _res.External != null)
        {
            _suppressImportedConfirm = true;
            try { if (!await RunAnalysis()) throw new Exception("Phân tích lỗi trước khi thử so sánh ETABS"); }
            finally { _suppressImportedConfirm = false; }
        }
        string csv = Path.Combine(SelfTestOut, tag + "_SoSanh_kieuETABS.csv");
        ResultTableWriter.Write(_res, csv, new ResultTableWriter.Options { Etabs = true });
        StLog("so sánh ETABS: đã ghi bảng kiểu ETABS " + csv + $" ({new FileInfo(csv).Length} byte)");

        var w = new CrossCheckWindow(_p, _res) { Owner = this, Width = 1300, Height = 820 };
        w.Show();
        try
        {
            w.AddFiles(new[] { csv });
            await Pause(500);
            var rep = w.RunCompare();
            if (rep == null) throw new Exception("So sánh ETABS không chạy được");
            w.SelectTab(2);
            var worst = rep.Worst.FirstOrDefault();
            if (worst != null) w.ShowMember(worst.FrameId);
            await Pause(900);
            ShotWindow(w, $"{tag}_42_SoSanhETABS");
            string dir = Path.Combine(SelfTestOut, tag + "_SoSanhETABS");
            string xl = w.ExportReports(dir);
            var st = rep.Stat(CrossCheckSession.GroupMembers);
            var sr = rep.Stat(CrossCheckSession.GroupReactions);
            StLog($"so sánh ETABS: {rep.Pairs.Count(x => x.Active)}/{rep.Pairs.Count} trường hợp, {rep.Members.Count(m => m.CaseCount > 0)}/{_p.Frames.Count} cấu kiện; "
                  + $"thanh TB {st?.Mean:0.0000} %, max {st?.Max:0.0000} % ({st?.MaxAt}); phản lực max {sr?.Max:0.0000} %; "
                  + $"cảnh báo {st?.Warn}, sai lệch lớn {st?.Fail}; không khớp {rep.UnmatchedExternal.Count}; đánh giá {CrossCheckReport.LevelText(rep.Overall)}"
                  + ((st?.Max ?? 0) < 0.01 ? " – ĐẠT (≈ 0 %)" : " – KHÁC 0 %, kiểm tra!"));
            StLog("so sánh ETABS: báo cáo " + (xl ?? "(lỗi xuất)") + (xl != null ? " + " + Path.Combine(dir, CrossCheckWriter.WordName) : ""));
        }
        finally { w.Close(); }
    }
}
