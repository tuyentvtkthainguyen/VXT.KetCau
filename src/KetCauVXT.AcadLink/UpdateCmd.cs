using System;
using System.Diagnostics;
using System.IO;

[assembly: Autodesk.AutoCAD.Runtime.CommandClass(typeof(KetCauVXT.AcadLink.UpdateCmd))]

namespace KetCauVXT.AcadLink
{
    using Autodesk.AutoCAD.Runtime;
    using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

    /// <summary>Lệnh VXT_CAPNHAT (nút "Cập nhật Việt Const" trên bảng lệnh VXT): mở chương trình Việt Const với tham số --update
    /// → cửa sổ kiểm tra / tải bản mới (tải từ GitHub Releases, không gửi dữ liệu đi đâu). Bộ cài cập nhật cả add-in AutoCAD,
    /// nên cần lưu bản vẽ và đóng AutoCAD trước khi cài.</summary>
    public class UpdateCmd
    {
        [CommandMethod("VXT_CAPNHAT", CommandFlags.Modal)]
        public void CapNhat()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            void Say(string m) { ed?.WriteMessage("\n" + m); }
            try
            {
                if (Process.GetProcessesByName("KetCauVXT").Length > 0)
                {
                    Say("Việt Const đang mở – trong chương trình bấm nút \"Cập nhật\" trên thanh công cụ (hoặc Trợ giúp → Kiểm tra & tải bản cập nhật…).");
                    return;
                }
            }
            catch (System.Exception) { }
            string exe = "";
            try
            {
                string f = Path.Combine(LayerStd.AppDataDir, "app_path.txt");
                if (File.Exists(f)) exe = File.ReadAllText(f).Trim();
            }
            catch (System.Exception) { }
            if (exe.Length == 0 || !File.Exists(exe))
            {
                Say("Chưa tìm thấy chương trình Việt Const (hãy mở KetCauVXT.exe một lần để đăng ký). Mở trang phát hành để tải bản mới…");
                try { Process.Start(new ProcessStartInfo("https://github.com/tuyentvtkthainguyen/VXT.KetCau/releases/tag/ketcau-vxt-latest") { UseShellExecute = true }); }
                catch (System.Exception ex) { Say("Không mở được trình duyệt: " + ex.Message); }
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(exe) { Arguments = "--update", UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) });
                Say("Đã mở cửa sổ cập nhật Việt Const. Trước khi chạy bộ cài: LƯU bản vẽ và ĐÓNG AutoCAD để bộ cài thay được add-in.");
            }
            catch (System.Exception ex) { Say("Không mở được " + exe + ": " + ex.Message); }
        }
    }
}
