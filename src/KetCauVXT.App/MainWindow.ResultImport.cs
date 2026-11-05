using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using KetCauVXT.Analysis;
using KetCauVXT.Import;
using KetCauVXT.Import.Csi;
using KetCauVXT.Model;

namespace KetCauVXT.App;

/// <summary>Nhập mô hình ETABS/SAP2000 (.e2k/.s2k) và nhập kết quả nội lực từ ETABS / SAP2000 / SAFE / phần mềm khác
/// (Tệp → Nhập mô hình ETABS/SAP…, Phân tích → Nhập kết quả nội lực…). Kết quả nhập thay _res – thiết kế, biểu đồ, bản vẽ,
/// thuyết minh dùng như kết quả của chương trình; chạy lại phân tích sẽ hỏi trước khi thay thế.</summary>
public partial class MainWindow
{
    private TextBlock _txtResSource;
    /// <summary>Bỏ hỏi xác nhận khi chạy lại phân tích (tự kiểm tra).</summary>
    private bool _suppressImportedConfirm;

    // ------------------------------------------------------------------ mô hình
    private void MnuImportCsiModel_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var dlg = new OpenFileDialog
        {
            Filter = "Mô hình ETABS / SAP2000 (*.e2k;*.$et;*.s2k;*.$2k)|*.e2k;*.$et;*.s2k;*.$2k|ETABS (*.e2k;*.$et)|*.e2k;*.$et|SAP2000 (*.s2k;*.$2k)|*.s2k;*.$2k|Tất cả (*.*)|*.*",
            Title = "Chọn file mô hình văn bản ETABS (.e2k) hoặc SAP2000 (.s2k)"
        };
        if (dlg.ShowDialog(this) != true) return;
        var r = MessageBox.Show(this, "Thay thế toàn bộ mô hình hiện tại (lưới trục, tầng, nút, thanh, sàn, tải trọng, tổ hợp) bằng mô hình trong file?\n" +
                                      "(Giữ lại thông số thiết kế, địa chất, khung tên, mẫu bản vẽ.)",
            AppTitle, MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (r != MessageBoxResult.OK) return;
        try
        {
            ApplyUiToProject(false);
            string path = dlg.FileName;
            ImportReport rep = IsS2k(path) ? S2kImporter.Import(_p, path) : E2kImporter.Import(_p, path);
            _res = null; _design = null; _sel = null;
            MarkDirty();
            LoadUiFromProject();
            RefreshAll(true);
            SetStatus($"Đã nhập mô hình {rep.Source}: {rep.Columns} cột, {rep.Beams} dầm, {rep.Slabs} ô sàn, {rep.Stories} tầng.");
            MessageBox.Show(this, rep.ToString() + "\n\nCó thể chạy phân tích của chương trình (F5) hoặc nhập kết quả nội lực từ file của phần mềm đó (Phân tích → Nhập kết quả nội lực…).",
                "Kết quả nhập mô hình", MessageBoxButton.OK, rep.Warnings.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError("Lỗi nhập mô hình ETABS/SAP2000", ex); }
    }

    /// <summary>File SAP2000 dạng bảng (TABLE: …) hay file ETABS .e2k ($ …).</summary>
    private static bool IsS2k(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".s2k" or ".$2k") return true;
        if (ext is ".e2k" or ".$et") return false;
        foreach (var l in File.ReadLines(path).Take(200))
        {
            var t = l.TrimStart();
            if (t.StartsWith("TABLE:", StringComparison.OrdinalIgnoreCase)) return true;
            if (t.StartsWith("$ ")) return false;
        }
        return false;
    }

    // ------------------------------------------------------------------ kết quả
    private void MnuImportResults_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_p.Frames.Count == 0 && _p.Slabs.Count == 0)
        {
            MessageBox.Show(this, "Dự án chưa có mô hình. Hãy nhập mô hình (Tệp → Nhập mô hình ETABS/SAP…) hoặc tạo mô hình trước khi nhập kết quả nội lực.",
                AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ApplyUiToProject(false);
        var own = _res != null && _res.External == null ? _res : null;
        var dlg = new ResultImportDialog(_p, own, ApplyImportedResultsAsync) { Owner = this };
        dlg.ShowDialog();
    }

    /// <summary>Đặt bộ nội lực nhập làm kết quả hiện hành; design = thiết kế lại ngay (BTCT + thép).</summary>
    internal async Task<bool> ApplyImportedResultsAsync(AnalysisResults res, bool design)
    {
        if (res == null || _busy) return false;
        _res = res; _design = null;
        _dirty = true; UpdateTitle();
        RefreshLoads();
        RefreshCaseCombos();
        RefreshResults();
        RefreshDesign();
        ShowSelection();
        var x = res.External;
        SetStatus($"Đã nhập nội lực từ {x?.Software} ({x?.FileList}): {x?.MatchedFrames} thanh, {res.Cases.Count} trường hợp/tổ hợp, {res.Warnings.Count} ghi chú.");
        if (!design) return true;
        bool ok = await EnsureDesign();
        if (ok && _design != null)
        {
            int bad = _design.All.Count(m => !m.Ok);
            SetStatus($"Thiết kế theo nội lực {x?.Software} xong: {_design.Beams.Count} dầm, {_design.Columns.Count} cột, {_design.Slabs.Count} ô sàn – {bad} cấu kiện không đạt. Xuất bản vẽ/thuyết minh từ menu Xuất.");
        }
        return ok;
    }

    /// <summary>Hỏi trước khi chạy phân tích của chương trình đè lên nội lực nhập từ phần mềm khác.</summary>
    private bool ConfirmReplaceImportedResults()
    {
        if (_res?.External == null || _suppressImportedConfirm) return true;
        var r = MessageBox.Show(this,
            $"Nội lực hiện tại được nhập từ {_res.External.Software} ({_res.External.FileList}).\n\n" +
            "Chạy phân tích của chương trình sẽ THAY THẾ bộ nội lực này (thiết kế, bản vẽ sẽ theo kết quả của chương trình). Tiếp tục?",
            AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return r == MessageBoxResult.Yes;
    }

    /// <summary>Danh sách trường hợp/tổ hợp cho ô chọn: kết quả nhập → tổ hợp bao nhập, các trường hợp có số liệu, tổ hợp dự án tổ hợp được.</summary>
    private List<string> ImportedCaseNames(List<string> names)
    {
        UpdateResultSourceStatus();
        if (_res?.External == null) return names;
        var res = new List<string>();
        void Add(string n) { if (!string.IsNullOrEmpty(n) && !res.Contains(n)) res.Add(n); }
        Add(_res.External.EnvelopeName);
        foreach (var c in _res.ExternalCombos) Add(c.Name);
        foreach (var k in _res.Cases.Keys) Add(k);
        foreach (var n in names) if (Resolvable(n, 0)) Add(n);
        return res;
    }

    private bool Resolvable(string name, int depth)
    {
        if (_res == null) return false;
        if (_res.Cases.ContainsKey(name)) return true;
        if (depth > 8) return false;
        var c = _res.FindCombo(name);
        return c != null && c.Items.Count > 0 && c.Items.All(i => Resolvable(i.Case, depth + 1));
    }

    /// <summary>Ô trạng thái "Nội lực: …" trên thanh trạng thái (tạo bằng mã, không sửa XAML).</summary>
    private void UpdateResultSourceStatus()
    {
        if (_txtResSource == null)
        {
            if (txtStatus?.Parent is not StatusBarItem it || it.Parent is not StatusBar sb) return;
            _txtResSource = new TextBlock { Margin = new Thickness(8, 0, 8, 0) };
            sb.Items.Insert(Math.Min(2, sb.Items.Count), new StatusBarItem { Content = _txtResSource });
        }
        if (_res == null) { _txtResSource.Text = "Nội lực: chưa có"; _txtResSource.Foreground = Brushes.Gray; _txtResSource.ToolTip = null; return; }
        if (_res.External == null) { _txtResSource.Text = "Nội lực: Việt Const"; _txtResSource.Foreground = Brushes.DimGray; _txtResSource.ToolTip = null; return; }
        var x = _res.External;
        _txtResSource.Text = $"Nội lực: nhập từ {x.Software} ({x.FileList})";
        _txtResSource.Foreground = Brushes.DarkOrange;
        _txtResSource.ToolTip = $"Nhập lúc {x.ImportedAt:dd/MM/yyyy HH:mm}; {x.MatchedFrames} thanh có nội lực; TTGH I: {_res.DesignCombos().Count} tổ hợp.\n"
                                + string.Join("\n", x.Notes.Take(12));
    }
}
