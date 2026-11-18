using System;
using Autodesk.Revit.UI;

namespace KetCauVXT.RevitExport
{
    /// <summary>Tab "Việt Const" trên ribbon Revit: xuất mô hình, chuẩn hoá, dựng kết cấu, nhận tiết diện, thư viện họ.</summary>
    public class RibbonApp : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            const string tab = "Việt Const";
            try { app.CreateRibbonTab(tab); } catch { /* tab đã có (add-in khác của Việt Const tạo) */ }
            string dll = typeof(RibbonApp).Assembly.Location;
            RibbonPanel panel;
            try { panel = app.CreateRibbonPanel(tab, "Liên kết Việt Const"); }
            catch (Exception) { return Result.Succeeded; }
            void Add(string name, string text, string cls, string tip)
            {
                var b = new PushButtonData(name, text, dll, cls) { ToolTip = tip };
                panel.AddItem(b);
            }
            Add("VxtStandardize", "Chuẩn hoá\ntheo Việt Const", typeof(StandardizeCommand).FullName,
                "Thống nhất mô hình Revit với Việt Const: đơn vị mm, tên cao độ, trục, vật liệu, loại tường / cột / dầm / sàn, thông tin dự án, khung tên, nét in. Xem trước và chọn mục trước khi áp dụng.");
            Add("VxtExport", "Xuất sang\nViệt Const", typeof(ExportCommand).FullName,
                "Xuất cao độ, trục, cột, dầm, sàn, tường, cửa, phòng sang file *.vxtrevit.json để tính trong Việt Const.");
            Add("VxtBuild", "Dựng kết cấu\ntừ Việt Const", typeof(BuildStructureCommand).FullName,
                "Kiến trúc → kết cấu: tạo cột, dầm, sàn kết cấu theo phương án Việt Const (file *.vxt2revit.json). Lần sau chỉ cập nhật cấu kiện đã dựng.");
            Add("VxtSync", "Nhận tiết diện\ntừ Việt Const", typeof(SyncSectionsCommand).FullName,
                "Sau khi Việt Const tối ưu: đổi loại cột, dầm, sàn trong Revit theo tiết diện mới (khớp theo mã đã gắn hoặc theo vị trí).");
            Add("VxtLibrary", "Đưa họ vào\nthư viện", typeof(ExportLibraryCommand).FullName,
                "Ghi hình học các họ đang chọn vào thư viện 3D của Việt Const.");

            // Kiến trúc từ phương án + cầu nối agent
            RibbonPanel arch;
            try { arch = app.CreateRibbonPanel(tab, "Phương án → hồ sơ"); }
            catch (Exception) { return Result.Succeeded; }
            void AddA(string name, string text, string cls, string tip) => arch.AddItem(new PushButtonData(name, text, dll, cls) { ToolTip = tip });
            AddA("VxtArch", "Dựng kiến trúc\ntừ phương án", typeof(BuildArchitectureCommand).FullName,
                "Phương án mặt bằng (*.vxtphuongan.json) → cao độ, trục, tường, sàn, cửa, phòng có tên, cầu thang. Xem trước rồi áp dụng; chạy lại chỉ thêm phần thiếu.");
            AddA("VxtSheets", "Bộ bản vẽ\nkiến trúc", typeof(DrawingSetCommand).FullName,
                "Tạo mặt bằng các tầng (tag phòng, cửa), 4 mặt đứng, mặt cắt A-A, phối cảnh 3D, thống kê cửa / phòng và các tờ KT-01…");
            AddA("VxtImage", "Ảnh phối cảnh\n3D", typeof(Image3DCommand).FullName,
                "Lưu ảnh 3D (PNG) của mô hình – làm nền cho render AI hoặc so với SketchUp.");
            try
            {
                AgentBridge.Init();
                AgentBridge.Button = arch.AddItem(new PushButtonData("VxtAgent", "Cho phép\nagent", dll, typeof(AgentToggleCommand).FullName)
                {
                    ToolTip = "Bật / tắt cầu nối để agent (Claude, Việt Const) chạy lệnh Revit trên máy này qua thư mục lệnh. Mặc định tắt; lệnh kết cấu luôn hỏi bạn duyệt.",
                }) as PushButton;
            }
            catch (Exception) { /* không tạo được ExternalEvent – bỏ qua cầu nối */ }
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) { AgentBridge.Shutdown(); return Result.Succeeded; }
    }
}
