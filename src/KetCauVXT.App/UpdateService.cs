using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KetCauVXT.App;

// =====================================================================
// CẬP NHẬT PHẦN MỀM: kiểm tra + tải bộ cài mới nhất từ GitHub Releases (tag ketcau-vxt-latest)
//  - Chỉ TẢI XUỐNG file công khai (version.txt + VietConst-Setup.exe/.bin); không gửi dữ liệu người dùng đi đâu.
//  - Bộ cài tải về thư mục %TEMP%\VietConstUpdate\<phiên bản>, kiểm tra kích thước + SHA-256 (nếu GitHub cung cấp), rồi chạy.
//  - Người dùng bấm nút mới tải; tự kiểm tra khi mở chương trình chỉ để hiện nút "Có bản mới" (tắt được).
// =====================================================================
public static class UpdateService
{
    public const string Repo = "tuyentvtkthainguyen/VXT.KetCau";
    public const string Tag = "ketcau-vxt-latest";
    public static string ReleasePage => $"https://github.com/{Repo}/releases/tag/{Tag}";

    public sealed class Asset { public string Name, Url, Sha256; public long Size; }

    public sealed class Info
    {
        public Version Latest;            // null: bản phát hành chưa ghi số phiên bản
        public DateTime? Published;
        public List<Asset> Files = new();
        public Asset Setup => Files.FirstOrDefault(f => f.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        public bool IsNewer => Latest != null && Latest > Current && Setup != null;
    }

    /// <summary>Kết quả kiểm tra gần nhất (null: chưa kiểm tra / lỗi).</summary>
    public static Info Last { get; private set; }

    /// <summary>Báo khi kết quả kiểm tra thay đổi (chạy trên luồng giao diện).</summary>
    public static event Action Changed;

    public static Version Current
    {
        get
        {
            var v = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    private static string SettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KetCauVXT", "TuKiemTraCapNhat.txt");

    /// <summary>Tự kiểm tra bản mới khi mở chương trình (mặc định bật).</summary>
    public static bool AutoCheck
    {
        get { try { return !File.Exists(SettingsFile) || File.ReadAllText(SettingsFile).Trim() != "0"; } catch { return true; } }
        set
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)); File.WriteAllText(SettingsFile, value ? "1" : "0"); }
            catch { /* bỏ qua */ }
        }
    }

    private static HttpClient NewClient(TimeSpan timeout)
    {
        var h = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = timeout };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("VietConst-Updater/" + Current);
        return h;
    }

    public static async Task<Info> CheckAsync(CancellationToken ct = default)
    {
        using var h = NewClient(TimeSpan.FromSeconds(25));
        string json = await h.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/tags/{Tag}", ct);
        var info = new Info();
        string verUrl = null;
        using (var doc = JsonDocument.Parse(json))
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("published_at", out var pub) && pub.ValueKind == JsonValueKind.String && DateTime.TryParse(pub.GetString(), out var d))
                info.Published = d.ToLocalTime();
            foreach (var a in root.GetProperty("assets").EnumerateArray())
            {
                var asset = new Asset
                {
                    Name = a.GetProperty("name").GetString() ?? "",
                    Url = a.GetProperty("browser_download_url").GetString() ?? "",
                    Size = a.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0
                };
                if (a.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String)
                {
                    string s = dg.GetString() ?? "";
                    if (s.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) asset.Sha256 = s.Substring(7).ToLowerInvariant();
                }
                if (asset.Name.Equals("version.txt", StringComparison.OrdinalIgnoreCase)) verUrl = asset.Url;
                else if (asset.Name.StartsWith("VietConst-Setup", StringComparison.OrdinalIgnoreCase)) info.Files.Add(asset);
            }
        }
        if (verUrl != null)
        {
            string s = (await h.GetStringAsync(verUrl, ct)).Trim();
            if (Version.TryParse(s, out var v)) info.Latest = new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
        Last = info;
        RaiseChanged();
        return info;
    }

    private static void RaiseChanged()
    {
        var app = Application.Current;
        if (app == null) return;
        app.Dispatcher.BeginInvoke(new Action(() => { try { Changed?.Invoke(); } catch { /* bỏ qua */ } }));
    }

    /// <summary>Kiểm tra ngầm khi mở chương trình (không hiện lỗi). Bỏ qua trong chế độ tự kiểm (CI).</summary>
    public static void StartAutoCheck(string[] args)
    {
        if (args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)) || !AutoCheck) return;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(4000); await CheckAsync(); }
            catch { /* không có mạng: im lặng */ }
        });
    }

    /// <summary>Tải toàn bộ file bộ cài (exe + các phần .bin) vào %TEMP%\VietConstUpdate\&lt;phiên bản&gt;. Trả về đường dẫn file .exe.</summary>
    public static async Task<string> DownloadAsync(Info info, IProgress<(long done, long total)> progress, CancellationToken ct)
    {
        if (info?.Setup == null) throw new InvalidOperationException("Bản phát hành không có file VietConst-Setup.exe.");
        string dir = Path.Combine(Path.GetTempPath(), "VietConstUpdate", info.Latest?.ToString() ?? "moi-nhat");
        Directory.CreateDirectory(dir);
        long total = info.Files.Sum(f => f.Size), done = 0;
        using var h = NewClient(TimeSpan.FromHours(2));
        foreach (var f in info.Files)
        {
            string path = Path.Combine(dir, f.Name);
            if (File.Exists(path) && (f.Size <= 0 || new FileInfo(path).Length == f.Size) && HashOk(path, f.Sha256))
            {
                done += f.Size; progress?.Report((done, total));
                continue;
            }
            string tmp = path + ".part";
            using (var resp = await h.GetAsync(f.Url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                using var src = await resp.Content.ReadAsStreamAsync(ct);
                using var dst = File.Create(tmp);
                var buf = new byte[1 << 16];
                int n;
                while ((n = await src.ReadAsync(buf.AsMemory(0, buf.Length), ct)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                    done += n;
                    progress?.Report((done, total));
                }
            }
            if (f.Size > 0 && new FileInfo(tmp).Length != f.Size) throw new IOException("Tải thiếu dữ liệu: " + f.Name);
            if (!HashOk(tmp, f.Sha256)) { File.Delete(tmp); throw new IOException("File tải về bị sai (mã kiểm tra SHA-256 không khớp): " + f.Name); }
            File.Move(tmp, path, true);
        }
        return Path.Combine(dir, info.Setup.Name);
    }

    private static bool HashOk(string path, string sha256)
    {
        if (string.IsNullOrEmpty(sha256)) return true;
        using var s = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(s)).Equals(sha256, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Giao diện
    // ------------------------------------------------------------------

    /// <summary>Mở cửa sổ cập nhật (một cửa sổ duy nhất).</summary>
    public static void ShowWindow(Window owner)
    {
        foreach (Window w in Application.Current.Windows)
            if (w is UpdateWindow uw) { uw.Activate(); return; }
        var win = new UpdateWindow();
        if (owner != null && owner.IsLoaded) win.Owner = owner;
        win.Show();
    }

    /// <summary>Nút "Cập nhật" cho thanh công cụ: tự đổi thành "Có bản mới …" (màu cam) khi kiểm tra thấy bản mới.</summary>
    public static Button MakeToolbarButton(Func<Window> owner)
    {
        var b = new Button { Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(4, 0, 0, 0) };
        void Refresh()
        {
            var i = Last;
            if (i != null && i.IsNewer)
            {
                b.Content = "⬆ Có bản mới " + i.Latest;
                b.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                b.Foreground = Brushes.White;
                b.FontWeight = FontWeights.SemiBold;
                b.ToolTip = $"Đang dùng {Current}. Bấm để tải và cài bản {i.Latest}.";
            }
            else
            {
                b.Content = "⟳ Cập nhật";
                b.ClearValue(Control.BackgroundProperty);
                b.ClearValue(Control.ForegroundProperty);
                b.FontWeight = FontWeights.Normal;
                b.ToolTip = $"Kiểm tra và tải bản cập nhật mới (đang dùng {Current})";
            }
        }
        Refresh();
        Action h = Refresh;
        Changed += h;
        b.Unloaded += (s, e) => Changed -= h;
        b.Loaded += (s, e) => { Changed -= h; Changed += h; Refresh(); };
        b.Click += (s, e) => ShowWindow(owner?.Invoke());
        return b;
    }
}

/// <summary>Cửa sổ cập nhật: phiên bản đang dùng / bản mới nhất, tải về (thanh tiến trình) và chạy bộ cài.</summary>
public sealed class UpdateWindow : Window
{
    private readonly TextBlock _cur = new() { FontSize = 13 };
    private readonly TextBlock _latest = new() { FontSize = 13, Margin = new Thickness(0, 2, 0, 0) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 6), Foreground = Brushes.DimGray };
    private readonly ProgressBar _bar = new() { Height = 16, Minimum = 0, Maximum = 1, Visibility = Visibility.Collapsed };
    private readonly CheckBox _auto = new() { Content = "Tự kiểm tra bản mới mỗi khi mở chương trình", Margin = new Thickness(0, 10, 0, 0) };
    private readonly Button _check = new() { Content = "Kiểm tra lại", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button _download = new() { Content = "TẢI VỀ & CÀI ĐẶT", Padding = new Thickness(14, 4, 14, 4), Margin = new Thickness(0, 0, 8, 0), FontWeight = FontWeights.Bold, IsEnabled = false };
    private readonly Button _close = new() { Content = "Đóng", Padding = new Thickness(14, 4, 14, 4), IsCancel = true };
    private CancellationTokenSource _cts;
    private bool _busy;

    public UpdateWindow()
    {
        Title = "Việt Const – cập nhật phần mềm";
        Width = 560; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var sp = new StackPanel { Margin = new Thickness(18) };
        sp.Children.Add(new TextBlock { Text = "CẬP NHẬT VIỆT CONST", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x3A, 0x5F)), Margin = new Thickness(0, 0, 0, 10) });
        sp.Children.Add(_cur);
        sp.Children.Add(_latest);
        sp.Children.Add(_status);
        sp.Children.Add(_bar);
        _auto.IsChecked = UpdateService.AutoCheck;
        _auto.Checked += (s, e) => UpdateService.AutoCheck = true;
        _auto.Unchecked += (s, e) => UpdateService.AutoCheck = false;
        sp.Children.Add(_auto);
        sp.Children.Add(new TextBlock
        {
            Text = "Chỉ tải file cài đặt công khai từ GitHub (" + UpdateService.Repo + "); chương trình không gửi dữ liệu của bạn đi đâu. "
                 + "Bộ cài sẽ hỏi đóng Việt Const và AutoCAD (nếu đang dùng add-in) – hãy lưu dự án / bản vẽ trước.",
            TextWrapping = TextWrapping.Wrap, FontSize = 11.5, Foreground = Brushes.Gray, Margin = new Thickness(0, 8, 0, 12)
        });
        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var page = new Button { Content = "Trang phát hành…", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        page.Click += (s, e) => { try { Process.Start(new ProcessStartInfo(UpdateService.ReleasePage) { UseShellExecute = true }); } catch { } };
        btns.Children.Add(page);
        btns.Children.Add(_check);
        btns.Children.Add(_download);
        btns.Children.Add(_close);
        sp.Children.Add(btns);
        Content = sp;

        _cur.Text = "Phiên bản đang dùng: " + UpdateService.Current;
        _check.Click += async (s, e) => await Check();
        _download.Click += async (s, e) => await DownloadAndInstall();
        _close.Click += (s, e) => Close();
        Closing += (s, e) =>
        {
            if (!_busy) return;
            if (MessageBox.Show(this, "Đang tải bản cập nhật. Huỷ tải và đóng?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) _cts?.Cancel();
            else e.Cancel = true;
        };
        Loaded += async (s, e) => { if (UpdateService.Last != null) Display(UpdateService.Last); await Check(); };
    }

    private void Display(UpdateService.Info i)
    {
        string when = i.Published.HasValue ? $" (phát hành {i.Published:dd/MM/yyyy HH:mm})" : "";
        long size = i.Files.Sum(f => f.Size);
        string mb = size > 0 ? $" – {size / 1048576.0:0.#} MB" : "";
        if (i.Setup == null)
        {
            _latest.Text = "Bản mới nhất: chưa có bộ cài trên trang phát hành.";
            _status.Text = "Không có gì để tải.";
            _download.IsEnabled = false;
        }
        else if (i.Latest == null)
        {
            _latest.Text = "Bản mới nhất: (chưa ghi số phiên bản)" + when + mb;
            _status.Text = "Không so được phiên bản – vẫn có thể tải và cài bản mới nhất trên trang phát hành.";
            _download.IsEnabled = true;
            _download.Content = "TẢI BẢN MỚI NHẤT";
        }
        else if (i.IsNewer)
        {
            _latest.Text = $"Bản mới nhất: {i.Latest}{when}{mb}";
            _latest.Foreground = new SolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C));
            _latest.FontWeight = FontWeights.SemiBold;
            _status.Text = "Có bản cập nhật mới. Bấm TẢI VỀ & CÀI ĐẶT.";
            _download.IsEnabled = true;
            _download.Content = "TẢI VỀ & CÀI ĐẶT";
        }
        else
        {
            _latest.Text = $"Bản mới nhất: {i.Latest}{when}";
            _status.Text = "Bạn đang dùng bản mới nhất.";
            _download.IsEnabled = true;               // vẫn cho cài lại nếu cần
            _download.Content = "Cài lại bản này";
        }
    }

    private async Task Check()
    {
        if (_busy) return;
        _check.IsEnabled = false;
        _status.Text = "Đang kiểm tra bản mới trên GitHub…";
        try { Display(await UpdateService.CheckAsync()); }
        catch (Exception ex)
        {
            _latest.Text = "Bản mới nhất: không kiểm tra được.";
            _status.Text = "Không kết nối được tới GitHub (" + ex.Message + "). Kiểm tra mạng rồi bấm Kiểm tra lại, hoặc mở Trang phát hành để tải tay.";
            _download.IsEnabled = false;
        }
        finally { _check.IsEnabled = true; }
    }

    private async Task DownloadAndInstall()
    {
        var info = UpdateService.Last;
        if (info?.Setup == null || _busy) return;
        _busy = true;
        _cts = new CancellationTokenSource();
        _download.IsEnabled = false; _check.IsEnabled = false;
        _bar.Visibility = Visibility.Visible;
        _bar.Value = 0;
        var prog = new Progress<(long done, long total)>(p =>
        {
            _bar.Value = p.total > 0 ? (double)p.done / p.total : 0;
            _status.Text = p.total > 0 ? $"Đang tải… {p.done / 1048576.0:0.0} / {p.total / 1048576.0:0.0} MB" : $"Đang tải… {p.done / 1048576.0:0.0} MB";
        });
        string exe;
        try { exe = await UpdateService.DownloadAsync(info, prog, _cts.Token); }
        catch (OperationCanceledException) { _status.Text = "Đã huỷ tải."; Reset(); return; }
        catch (Exception ex) { _status.Text = "Tải không thành công: " + ex.Message; Reset(); return; }
        _busy = false;
        _status.Text = "Đã tải xong: " + exe;

        var ok = MessageBox.Show(this,
            $"Đã tải xong bản cập nhật{(info.Latest != null ? " " + info.Latest : "")}.\n\n" +
            "Bấm OK: chạy bộ cài và đóng Việt Const (chương trình sẽ hỏi lưu dự án nếu chưa lưu).\n" +
            "Nếu đang mở AutoCAD có add-in Việt Const, hãy lưu bản vẽ và đóng AutoCAD trước khi cài.",
            Title, MessageBoxButton.OKCancel, MessageBoxImage.Information);
        if (ok != MessageBoxResult.OK) { Reset(); _status.Text = "Bộ cài đã tải sẵn tại: " + exe; return; }
        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Không chạy được bộ cài: " + ex.Message + "\nMở tay file: " + exe, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            Reset();
            return;
        }
        Close();
        // đóng các cửa sổ chính (mỗi cửa sổ tự hỏi lưu dự án); bộ cài chờ chương trình đóng hẳn (AppMutex) rồi mới cài
        foreach (var w in Application.Current.Windows.OfType<Window>().ToList())
        {
            try { w.Close(); } catch { /* bỏ qua */ }
        }
    }

    private void Reset()
    {
        _busy = false;
        _bar.Visibility = Visibility.Collapsed;
        _check.IsEnabled = true;
        _download.IsEnabled = UpdateService.Last?.Setup != null;
    }
}
