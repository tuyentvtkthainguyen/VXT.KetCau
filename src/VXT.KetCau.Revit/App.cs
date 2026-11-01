using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace VXT.KetCau.Revit
{
    /// <summary>Ribbon "VXT Kết cấu" — quy trình từ kiến trúc tới shop drawing thép.</summary>
    public class App : IExternalApplication
    {
        const string Tab = "VXT Kết cấu";

        public Result OnStartup(UIControlledApplication app)
        {
            try { app.CreateRibbonTab(Tab); } catch { /* đã có */ }
            string asm = Assembly.GetExecutingAssembly().Location;

            var p1 = app.CreateRibbonPanel(Tab, "1. Mô hình");
            Add(p1, "ArchToStruct", "Kiến trúc\n→ Kết cấu", "Commands.CmdArchToStruct", asm, "KT", Color.FromRgb(0x2E, 0x7D, 0x32),
                "Đọc hồ sơ kiến trúc Revit (file hiện hành hoặc file liên kết) và tự sinh cột, dầm, sàn, móng, tải trọng.");
            Add(p1, "DwgToStruct", "DWG kiến trúc\n→ Kết cấu", "Commands.CmdDwgToStruct", asm, "DWG", Color.FromRgb(0x00, 0x69, 0x7C),
                "Đọc mặt bằng DWG/DXF các tầng (trục, tường, cột, cửa, phòng) và tự sinh mô hình kết cấu.");
            Add(p1, "ReadStruct", "Đọc kết cấu\nRevit", "Commands.CmdReadStruct", asm, "RV", Color.FromRgb(0x45, 0x5A, 0x64),
                "Dùng mô hình kết cấu đã dựng sẵn trong Revit làm mô hình tính toán.");

            var p2 = app.CreateRibbonPanel(Tab, "2. Tính toán");
            Add(p2, "Analyze", "ETABS / SAP\nTạo & chạy", "Commands.CmdAnalyze", asm, "E", Color.FromRgb(0xC6, 0x28, 0x28),
                "Tạo mô hình tính toán trong ETABS hoặc SAP2000 (vật liệu, tiết diện, tải, tổ hợp TCVN), chạy phân tích và đọc nội lực.");
            Add(p2, "ReadResults", "Đọc lại\nnội lực", "Commands.CmdReadResults", asm, "N", Color.FromRgb(0xAD, 0x14, 0x57),
                "Đọc nội lực từ ETABS/SAP2000 đang mở (sau khi bạn tự chỉnh và chạy lại mô hình).");
            Add(p2, "Design", "Thiết kế\n& cấu tạo", "Commands.CmdDesign", asm, "TK", Color.FromRgb(0x6A, 0x1B, 0x9A),
                "Kiểm tra/thiết kế dầm, cột, sàn, móng theo TCVN 5574:2018; bố trí thép, đánh số Kết cấu Soft; xuất thuyết minh và bảng thống kê.");

            var p3 = app.CreateRibbonPanel(Tab, "3. Thép & bản vẽ");
            Add(p3, "Rebar", "Tạo thép\n3D", "Commands.CmdRebar", asm, "Ø", Color.FromRgb(0xD8, 0x43, 0x15),
                "Dựng cốt thép Revit (dọc, mũ gối, tăng cường, đai, đai C, thép sàn, móng) theo kết quả cấu tạo.");
            Add(p3, "Shop", "Shop\ndrawing", "Commands.CmdShopDrawing", asm, "SD", Color.FromRgb(0x15, 0x65, 0xC0),
                "Tạo mặt đứng, mặt cắt dầm, chi tiết cột, bảng thống kê; tự dim, ghi chú thép; xếp lên tờ KC-xx.");
            Add(p3, "Dxf", "Xuất\nDXF", "Commands.CmdExportDxf", asm, "DXF", Color.FromRgb(0x37, 0x47, 0x4F),
                "Xuất toàn bộ shop drawing thép ra file DXF (mở bằng AutoCAD).");

            var p4 = app.CreateRibbonPanel(Tab, "Cài đặt");
            Add(p4, "Settings", "Thông số", "Commands.CmdSettings", asm, "⚙", Color.FromRgb(0x61, 0x61, 0x61),
                "Vật liệu, lớp bảo vệ, tải gió/động đất, quy tắc sinh khung, phần mềm phân tích.");
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;

        static void Add(RibbonPanel p, string name, string text, string cls, string asm, string icon, Color color, string tip)
        {
            var d = new PushButtonData("VXT_" + name, text, asm, "VXT.KetCau.Revit." + cls) { ToolTip = tip, LargeImage = Icon(icon, color, 32), Image = Icon(icon, color, 16) };
            p.AddItem(d);
        }

        /// <summary>Biểu tượng vẽ bằng mã: ô màu bo góc + chữ trắng (không cần file ảnh).</summary>
        static ImageSource Icon(string text, Color bg, int size)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, size, size), size * 0.18, size * 0.18);
                var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"),
                                           size * (text.Length >= 3 ? 0.36 : 0.5), Brushes.White, 1.0);
                dc.DrawText(ft, new Point((size - ft.Width) / 2, (size - ft.Height) / 2));
            }
            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            return bmp;
        }
    }
}
