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
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}
