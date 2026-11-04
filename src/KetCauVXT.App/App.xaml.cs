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
