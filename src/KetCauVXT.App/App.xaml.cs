using System.Linq;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

namespace KetCauVXT.App;

public partial class App : Application
{
    /// <summary>Mutex báo "chương trình đang chạy" – bộ cài (Inno Setup, AppMutex) dùng để yêu cầu người dùng tự lưu và đóng
    /// chương trình trước khi cài đè/gỡ, không đóng cưỡng bức (tránh mất dữ liệu chưa lưu).</summary>
    private static Mutex _runningMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        try { _runningMutex = new Mutex(false, "KetCauVXT_DangChay"); } catch { }
        // Số thập phân dùng dấu chấm trong toàn bộ giao diện (giống ETABS/SAP2000), tránh nhầm với dấu phẩy.
        var ci = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        CultureInfo.DefaultThreadCurrentCulture = ci;
        CultureInfo.DefaultThreadCurrentUICulture = ci;
        Thread.CurrentThread.CurrentCulture = ci;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("en-US")));
        // Bộ đọc DWG tích hợp (KetCauVXT.Dwg – ACadSharp): nhập kiến trúc, quét layer, nhập mặt bằng kết cấu đọc thẳng file .dwg
        global::KetCauVXT.Dwg.DwgReader.Register();
        // [mẫu định dạng + khung tên người dùng] DWG → DXF để đọc ATTDEF/căn lề chữ khi tải khung tên; áp dụng mẫu mặc định cho dự án mới
        global::KetCauVXT.Import.CadFileReaders.DwgToDxf = global::KetCauVXT.Dwg.DwgReader.ConvertToDxf;
        // Bộ ghi DXF 2018 / DWG 2018 có HATCH (KetCauVXT.Dwg – ACadSharp) cho Chuẩn layer CAD → Định dạng xuất
        global::KetCauVXT.Dwg.CadDocWriter.Register();
        global::KetCauVXT.Model.TemplateService.AutoApplyDefault = true;
        DialogUi.RegisterFitToScreen();   // hộp thoại dài hơn màn hình → cuộn bằng chuột giữa, không tràn màn
        base.OnStartup(e);
        StartWindows(e.Args ?? Array.Empty<string>());
        UpdateService.StartAutoCheck(e.Args ?? Array.Empty<string>());   // kiểm tra ngầm bản mới → hiện nút "Có bản mới" (tắt được)
    }

    /// <summary>Chọn cửa sổ mở đầu: tham số "--…" (tự kiểm tra, AutoCAD) hoặc file .kcvxt → chế độ Thiết kế – chạy nội lực;
    /// file .kcvxth → chế độ Triển khai hồ sơ; còn lại → màn hình chọn chế độ (hoặc chế độ đã ghi nhớ).</summary>
    private void StartWindows(string[] args)
    {
        bool Has(string ext) => args.Any(a => a.EndsWith(ext, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(a));
        if (args.Any(a => a.StartsWith("--", StringComparison.Ordinal)) || Has(".kcvxt")) { LauncherWindow.Open(LauncherWindow.Mode.Model); return; }
        if (Has(".kcvxth"))
        {
            LauncherWindow.Open(LauncherWindow.Mode.Detailing, args.First(a => a.EndsWith(".kcvxth", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(a)));
            return;
        }
        var pref = LauncherWindow.LoadPref();
        if (pref.Remember && Enum.TryParse(pref.Mode, out LauncherWindow.Mode m) && m != LauncherWindow.Mode.None) { LauncherWindow.Open(m); return; }
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var l = new LauncherWindow();
        bool ok = l.ShowDialog() == true && l.Chosen != LauncherWindow.Mode.None;
        ShutdownMode = ShutdownMode.OnLastWindowClose;
        if (ok) LauncherWindow.Open(l.Chosen);
        else Shutdown(0);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (global::KetCauVXT.App.MainWindow.SelfTestOut != null)
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(global::KetCauVXT.App.MainWindow.SelfTestOut, "selftest.log"), "LỖI KHÔNG BẮT: " + e.Exception + "\n"); } catch { }
            e.Handled = true;
            Shutdown(2);
            return;
        }
        MessageBox.Show("Lỗi không mong muốn:\n" + e.Exception.Message + "\n\n" + e.Exception.GetType().Name,
            "Việt Const", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
