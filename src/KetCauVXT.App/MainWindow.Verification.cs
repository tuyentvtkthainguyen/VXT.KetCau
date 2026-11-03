using System.Windows;
using KetCauVXT.Verification;

namespace KetCauVXT.App;

// TRỢ GIÚP → "Kiểm chứng phần mềm…" – VerificationWindow.cs (lõi: Core/Verification/*)
public partial class MainWindow
{
    private void MnuVerification_Click(object sender, RoutedEventArgs e)
    {
        try { new VerificationWindow { Owner = this }.Show(); }
        catch (Exception ex) { MessageBox.Show(this, "Không mở được kiểm chứng phần mềm:\n" + ex.Message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    /// <summary>Tự kiểm tra: mở cửa sổ, chạy, chụp ảnh, xuất báo cáo (không ghi đè dữ liệu người dùng).</summary>
    private async Task SelfTestVerification(string tag)
    {
        VerificationUserData.UserFolderOverride = System.IO.Path.Combine(SelfTestOut, "KiemChung_User");
        var w = new VerificationWindow { Owner = this };
        w.Show();
        await w.RunAsync();
        await Pause(1200);
        ShotWindow(w, $"{tag}_41_KiemChung");
        var (d, x) = w.Export(System.IO.Path.Combine(SelfTestOut, tag + "_KiemChung"));
        StLog($"kiểm chứng: {w.Result?.Summary} → {d}");
        w.Close();
        VerificationUserData.UserFolderOverride = null;
    }
}
