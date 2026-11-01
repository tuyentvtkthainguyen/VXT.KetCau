using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

namespace KetCauVXT.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Số thập phân dùng dấu chấm trong toàn bộ giao diện (giống ETABS/SAP2000), tránh nhầm với dấu phẩy.
        var ci = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        CultureInfo.DefaultThreadCurrentCulture = ci;
        CultureInfo.DefaultThreadCurrentUICulture = ci;
        Thread.CurrentThread.CurrentCulture = ci;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("en-US")));
        base.OnStartup(e);
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
            "Kết cấu VXT", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
