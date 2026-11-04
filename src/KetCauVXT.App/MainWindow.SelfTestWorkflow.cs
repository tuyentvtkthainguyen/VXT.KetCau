using KetCauVXT.Analysis;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Bước tự kiểm tra (--selftest) quy trình mô hình kiểu ETABS: hiện cây mô hình + tải gán, chụp; mở "Kiểm tra mô hình" (không modal),
/// chụp; mở "Bảng kết quả" (phản lực), chụp; mở "Tổ hợp tải trọng", chụp; đóng tất cả, ghi tóm tắt vào selftest.log.</summary>
public partial class MainWindow
{
    private async Task SelfTestWorkflow(string tag)
    {
        bool explorerWas = _explorerVisible, loadsWas = _showAssignedLoads;
        try
        {
            if (_res == null || _res.External != null)
            {
                _suppressImportedConfirm = true;
                try { if (!await RunAnalysis()) throw new Exception("Phân tích lỗi trước khi thử quy trình mô hình"); }
                finally { _suppressImportedConfirm = false; }
            }
            // ---- cây mô hình ----
            ShowPlanTab();
            ShowModelExplorer(true);
            _showAssignedLoads = true;
            if (mnuShowLoads != null) mnuShowLoads.IsChecked = true;
            BuildModelExplorer();
            foreach (var it in _explorer.Items.OfType<System.Windows.Controls.TreeViewItem>()) it.IsExpanded = true;
            RedrawPlan();
            await Pause(1200);
            Shot($"{tag}_47_CayMoHinh");
            StLog($"cây mô hình: {_explorer.Items.Count} nhánh; {_p.Frames.Count} thanh, {_p.Slabs.Count} sàn, {_p.Patterns.Count} trường hợp tải, {_p.Combinations.Count} tổ hợp");

            // ---- kiểm tra mô hình ----
            var w = OpenModelCheck();
            if (w != null)
            {
                w.Width = 1100; w.Height = 600;
                await Pause(900);
                ShotWindow(w, $"{tag}_48_KiemTraMoHinh");
                var rep = w.Report;
                StLog("kiểm tra mô hình: " + (rep?.Summary ?? "không chạy được"));
                if (rep != null)
                    foreach (var g in rep.Issues.GroupBy(i => i.Code)) StLog($"   {g.Key}: {g.Count()} ({g.First().SeverityName})");
            }

            // ---- bảng kết quả ----
            var w2 = await OpenTables(ResultTableKind.JointReactions);
            if (w2 != null)
            {
                w2.Width = 1300; w2.Height = 720;
                string c = _p.Pattern("TT") != null ? "TT" : _res.Cases.Keys.FirstOrDefault();
                w2.SetCase(c);
                await Pause(900);
                ShotWindow(w2, $"{tag}_49_BangKetQua");
                foreach (var (k, title) in ResultTables.All)
                {
                    try
                    {
                        var t = ResultTables.Build(_res, k, new ResultTableFilter { Cases = c != null ? new List<string> { c } : null });
                        StLog($"bảng kết quả {title}: {t.Rows.Count} dòng");
                    }
                    catch (Exception ex) { StLog($"bảng kết quả {title}: lỗi {ex.Message}"); }
                }
                try { w2.Export(System.IO.Path.Combine(SelfTestOut, tag + "_BangKetQua_PhanLuc.xlsx")); } catch (Exception ex) { StLog("xuất bảng kết quả lỗi: " + ex.Message); }
            }

            // ---- tổ hợp tải trọng ----
            var w3 = OpenComboEditor();
            if (w3 != null)
            {
                w3.Width = 1100; w3.Height = 620;
                var first = _p.Combinations.FirstOrDefault(c => c.Type == ComboType.ULS) ?? _p.Combinations.FirstOrDefault();
                if (first != null) w3.SelectCombo(first.Name);
                await Pause(900);
                ShotWindow(w3, $"{tag}_50_ToHop");
                StLog($"tổ hợp tải trọng: {_p.Combinations.Count} tổ hợp ({_p.Combinations.Count(c => c.Type == ComboType.ULS)} TTGH I, "
                      + $"{_p.Combinations.Count(c => c.Type == ComboType.SLS)} TTGH II, {_p.Combinations.Count(c => c.Type == ComboType.Envelope)} bao); "
                      + $"vấn đề: {KetCauVXT.Loads.ComboEditor.Problems(_p).Count}");
            }
        }
        catch (Exception ex) { StLog("quy trình mô hình ETABS: lỗi " + ex.Message); }
        finally
        {
            try { _checkWin?.Close(); } catch { }
            try { _tablesWin?.Close(); } catch { }
            try { _comboWin?.Close(); } catch { }
            try { _deformWin?.Close(); } catch { }
            _showAssignedLoads = loadsWas;
            if (mnuShowLoads != null) mnuShowLoads.IsChecked = loadsWas;
            ShowModelExplorer(explorerWas);
            RedrawPlan();
            StLog("quy trình mô hình ETABS: xong");
        }
    }
}
