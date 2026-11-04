using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using KetCauVXT.Import.Arch;

namespace KetCauVXT.App;

/// <summary>Liên kết AutoCAD: nhận dữ liệu đã tích chọn trong AutoCAD (add-in KetCauVXT.AcadLink, lệnh VXT_GUI).
/// <list type="bullet">
/// <item>Menu Tệp → Nhận dữ liệu từ AutoCAD… (mở file acad_*.json).</item>
/// <item>Theo dõi thư mục %AppData%\KetCauVXT\Link: có file mới → hỏi mở hộp thoại triển khai kết cấu.</item>
/// <item>Dòng lệnh: KetCauVXT.exe --acad "&lt;file.json&gt;" (add-in tự mở chương trình khi chưa chạy).</item>
/// <item>Ghi đường dẫn chương trình vào %AppData%\KetCauVXT\app_path.txt để add-in tìm thấy.</item>
/// </list></summary>
public partial class MainWindow
{
    private FileSystemWatcher _acadWatcher;
    private bool _acadPrompting;
    private readonly HashSet<string> _acadSeen = new(StringComparer.OrdinalIgnoreCase);

    internal static string AcadAppDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KetCauVXT");
    internal static string AcadLinkDir => Path.Combine(AcadAppDataDir, "Link");

    /// <summary>Ghi app_path.txt và bật theo dõi thư mục Link – chạy nền, lỗi bỏ qua (không bao giờ chặn giao diện).</summary>
    private void InitAcadLink(bool watch)
    {
        Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(AcadLinkDir);
                string exe = Environment.ProcessPath ?? "";
                string f = Path.Combine(AcadAppDataDir, "app_path.txt");
                if (exe.Length > 0 && (!File.Exists(f) || !string.Equals(File.ReadAllText(f).Trim(), exe, StringComparison.OrdinalIgnoreCase)))
                    File.WriteAllText(f, exe, new System.Text.UTF8Encoding(false));
                if (!watch) return;
                var w = new FileSystemWatcher(AcadLinkDir, "*.json") { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite, IncludeSubdirectories = false };
                w.Created += (s, e) => OnAcadFileEvent(e.FullPath);
                w.Renamed += (s, e) => OnAcadFileEvent(e.FullPath);
                w.EnableRaisingEvents = true;
                Dispatcher.BeginInvoke(new Action(() => _acadWatcher = w));
            }
            catch (Exception) { /* không có quyền ghi / thư mục mạng … – bỏ qua liên kết tự động, vẫn dùng được menu */ }
        });
    }

    private void OnAcadFileEvent(string path)
    {
        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return;
        Dispatcher.BeginInvoke(new Action(async () => await PromptAcadFile(path)));
    }

    private async Task PromptAcadFile(string path)
    {
        if (SelfTestOut != null) return;
        lock (_acadSeen) { if (!_acadSeen.Add(path)) return; }
        // add-in có thể đang ghi file: chờ đọc được trọn vẹn
        KetCauVXT.Import.Acad.AcadExchange x = null;
        for (int i = 0; i < 20 && x == null; i++)
        {
            await Task.Delay(300);
            try { x = await Task.Run(() => KetCauVXT.Import.Acad.AcadExchange.Load(path)); }
            catch (Exception) when (i < 19) { }
            catch (Exception ex) { SetStatus("Không đọc được dữ liệu AutoCAD: " + ex.Message); return; }
        }
        if (x == null) return;
        while (_acadPrompting || _busy) await Task.Delay(500);
        if (OwnedWindows.OfType<Window>().Any(w => w.IsVisible && w is ArchImportDialog)) { SetStatus("Có dữ liệu mới từ AutoCAD: " + path); return; }
        _acadPrompting = true;
        try
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            await OfferCadLinkFromAcad(x);   // [CadLink] JSON có "Dwg" → tạo / làm mới bản vẽ liên kết – MainWindow.CadLink.cs
            Activate();
            int n = x.Storeys?.Count ?? 0;
            var r = MessageBox.Show(this, $"Có dữ liệu mới từ AutoCAD ({n} tầng).\n{(string.IsNullOrEmpty(x.Dwg) ? "" : x.Dwg + "\n")}\nMở hộp thoại nhập?",
                AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes) await ImportAcadFile(path);
        }
        finally { _acadPrompting = false; }
    }

    /// <summary>Tham số dòng lệnh --acad "&lt;file.json&gt;"; trả true nếu đã xử lý.</summary>
    private bool HandleAcadArgs(string[] args)
    {
        int i = Array.FindIndex(args, a => string.Equals(a, "--acad", StringComparison.OrdinalIgnoreCase));
        if (i < 0 || i + 1 >= args.Length) return false;
        string path = args[i + 1].Trim('"');
        lock (_acadSeen) _acadSeen.Add(path);
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            if (!File.Exists(path)) { MessageBox.Show(this, "Không tìm thấy file dữ liệu AutoCAD:\n" + path, AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            await ImportAcadFile(path);
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        return true;
    }

    private async void MnuAcadLink_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var dlg = new OpenFileDialog
        {
            Filter = "Dữ liệu từ AutoCAD (*.json)|*.json|Tất cả (*.*)|*.*",
            Title = "Chọn file dữ liệu gửi từ AutoCAD (lệnh VXT_GUI)",
        };
        try { if (Directory.Exists(AcadLinkDir)) dlg.InitialDirectory = AcadLinkDir; } catch (Exception) { }
        if (dlg.ShowDialog(this) != true) return;
        await ImportAcadFile(dlg.FileName);
    }

    /// <summary>Mở hộp thoại triển khai kết cấu đã nạp sẵn dữ liệu AutoCAD; người dùng xem, sửa rồi bấm "Tạo mô hình".</summary>
    private async Task ImportAcadFile(string path)
    {
        if (_busy) return;
        ApplyUiToProject(false);
        var dlg = new ArchImportDialog();
        if (IsVisible) dlg.Owner = this;
        dlg.PresetAcadLink(path);
        if (dlg.ShowDialog() != true || dlg.Proposal == null || dlg.Drawing == null) return;
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var rep = ArchToStructure.Apply(_p, dlg.Drawing, dlg.Proposal, dlg.Options);
            Mouse.OverrideCursor = null;
            _res = null; _design = null; _sel = null;
            MarkDirty();
            LoadUiFromProject();
            RefreshAll(true);
            SetStatus($"Đã triển khai kết cấu từ dữ liệu AutoCAD: {rep.Columns} cột, {rep.Beams} dầm, {rep.Slabs} ô sàn – cần chạy lại phân tích.");
            MessageBox.Show(this, rep.ToString(), "Kết quả triển khai kết cấu từ AutoCAD", MessageBoxButton.OK,
                rep.Warnings.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            if (MessageBox.Show(this, "Tiết diện hiện là kích thước sơ bộ.\n\nChạy phân tích + thiết kế và TỰ TĂNG tiết diện các dầm, cột, sàn không đạt (lặp tới khi đạt)?",
                    AppTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                await RunAutoResize();
        }
        catch (Exception ex)
        {
            Mouse.OverrideCursor = null;
            ShowError("Lỗi tạo mô hình từ dữ liệu AutoCAD", ex);
        }
        finally { Mouse.OverrideCursor = null; }
    }
}
