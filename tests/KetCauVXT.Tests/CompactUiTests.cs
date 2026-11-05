using KetCauVXT.Ui;

namespace KetCauVXT.Tests;

/// <summary>Giao diện gọn: tìm lệnh không dấu, xếp hạng, biểu tượng nút, lưu bố cục ui.json.</summary>
public static class CompactUiTests
{
    private static double B(bool b) => b ? 1 : 0;

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        // ---- bỏ dấu ----
        check("Bỏ dấu: \"Mặt bằng kết cấu\" → \"mat bang ket cau\"", B(CommandSearch.Fold("Mặt bằng kết cấu") == "mat bang ket cau"), 1, 0);
        check("Bỏ dấu: đ/Đ → d, bỏ '_' phím tắt menu, gộp ký hiệu", B(CommandSearch.Fold("_Đường  dẫn – ĐỊA điểm…") == "duong dan dia diem"), 1, 0);
        check("Bỏ dấu: chuỗi rỗng / null", B(CommandSearch.Fold(null) == "" && CommandSearch.Fold("  ") == ""), 1, 0);

        // ---- khớp + xếp hạng ----
        var cmds = new List<CommandEntry>
        {
            new() { Title = "Xuất mặt bằng kết cấu sang AutoCAD", Path = "Xuất hồ sơ" },
            new() { Title = "Mặt bằng kết cấu", Path = "Xuất hồ sơ" },
            new() { Title = "Lưu", Gesture = "Ctrl+S", Path = "Tệp" },
            new() { Title = "Lưu thành", Path = "Tệp" },
            new() { Title = "Tính toàn bộ hồ sơ", Path = "Tính toán", Gesture = "F5" },
            new() { Title = "Bảng khối lượng Excel", Path = "Xuất hồ sơ" },
        };
        var r1 = CommandSearch.Filter(cmds, "mat bang");
        check("\"mat bang\" → \"Mặt bằng kết cấu\" đứng đầu (bắt đầu bằng > chứa)", B(r1.Count >= 2 && r1[0].Title == "Mặt bằng kết cấu"), 1, 0);
        check("\"mat bang\" → cũng tìm thấy lệnh chứa cụm từ", B(r1.Any(c => c.Title.StartsWith("Xuất mặt bằng"))), 1, 0);
        check("Điểm: trùng hẳn > bắt đầu bằng > chứa", B(CommandSearch.Score("lưu", "Lưu") > CommandSearch.Score("luu", "Lưu thành")
            && CommandSearch.Score("luu", "Lưu thành") > CommandSearch.Score("luu", "Bản lưu tạm")), 1, 0);
        var r2 = CommandSearch.Filter(cmds, "luu");
        check("\"luu\" → \"Lưu\" trước \"Lưu thành\"", B(r2.Count == 2 && r2[0].Title == "Lưu" && r2[1].Title == "Lưu thành"), 1, 0);
        check("Các từ theo thứ tự bất kỳ (gồm cả đường dẫn menu): \"excel khoi luong\"", B(CommandSearch.Filter(cmds, "excel khoi luong").FirstOrDefault()?.Title == "Bảng khối lượng Excel"), 1, 0);
        check("Viết tắt chữ đầu: \"mbkc\" → \"Mặt bằng kết cấu\"", B(CommandSearch.Filter(cmds, "mbkc").FirstOrDefault()?.Title == "Mặt bằng kết cấu"), 1, 0);
        check("Không khớp → rỗng", CommandSearch.Filter(cmds, "zzzz").Count, 0, 0);
        check("Chuỗi rỗng → mọi lệnh, giữ thứ tự gốc", B(CommandSearch.Filter(cmds, "").Select(c => c.Title).SequenceEqual(cmds.Select(c => c.Title))), 1, 0);
        check("Giới hạn số kết quả", CommandSearch.Filter(cmds, "", 3).Count, 3, 0);
        check("Tên sạch: \"_Mở hồ sơ .kcvxth…\" → \"Mở hồ sơ .kcvxth\"", B(CommandSearch.CleanTitle("_Mở hồ sơ .kcvxth…") == "Mở hồ sơ .kcvxth"), 1, 0);
        check("Tên sạch: bỏ ký hiệu đầu \"＋ Móng đơn\", \"▶ Phân tích\"", B(CommandSearch.CleanTitle("＋ Móng đơn") == "Móng đơn" && CommandSearch.CleanTitle("▶ Phân tích") == "Phân tích"), 1, 0);
        check("Hiển thị kèm phím tắt", B(cmds[2].ToString() == "Lưu   (Ctrl+S)"), 1, 0);

        // ---- biểu tượng nút ----
        check("Biểu tượng: Lưu / Mở / Mới khác nhau và có", B(UiGlyphs.For("Lưu").Length == 1 && UiGlyphs.For("Mở…").Length == 1 && UiGlyphs.For("Mới").Length == 1
            && UiGlyphs.For("Lưu") != UiGlyphs.For("Mở…") && UiGlyphs.For("Mới") != UiGlyphs.For("Mở…")), 1, 0);
        check("Biểu tượng: \"▶ Phân tích\" = chạy, \"🖨 Dàn trang in\" = in", B(UiGlyphs.For("▶ Phân tích") == "\uE768" && UiGlyphs.For("🖨 Dàn trang in") == "\uE749"), 1, 0);
        check("Biểu tượng: \"Mở rộng vùng làm việc\" ≠ \"Mở\"", B(UiGlyphs.For("Mở rộng vùng làm việc") == UiGlyphs.FullScreen), 1, 0);
        check("Biểu tượng: \"⇄ Thiết kế – chạy nội lực\" = chuyển chế độ", B(UiGlyphs.For("⇄ Thiết kế – chạy nội lực") == "\uE8AB"), 1, 0);
        check("Biểu tượng: tên không có từ khoá → rỗng (giữ chữ)", B(UiGlyphs.For("Đánh số lại") == "" && UiGlyphs.For("▲") == ""), 1, 0);

        // ---- thiết lập bố cục ----
        var d = new UiLayoutSettings();
        check("Mặc định: giao diện gọn, chữ khung 12, thanh trạng thái 20 px, thanh công cụ 28 px",
            B(d.Compact && d.PanelFontSize == 12 && d.StatusBarHeight == 20 && d.ToolbarHeight == 28), 1, 0);
        var w = d.For("MainWindow");
        check("Mặc định cửa sổ: menu hiện, thanh công cụ hiện, không khung thu gọn", B(!w.MenuHidden && !w.ToolbarHidden && w.Collapsed.Count == 0), 1, 0);
        w.MenuHidden = true; w.PanelWidths["trai"] = 420; w.Collapsed.Add("cay");
        d.Density = UiLayoutSettings.DensityStandard;
        var back = UiLayoutSettings.FromJson(d.ToJson());
        var wb = back.For("MainWindow");
        check("JSON: lưu / đọc lại bố cục cửa sổ", B(wb.MenuHidden && wb.PanelWidths.TryGetValue("trai", out var pw) && pw == 420 && wb.Collapsed.SequenceEqual(new[] { "cay" })), 1, 0);
        check("JSON: mật độ tiêu chuẩn → chữ 13, thanh trạng thái 24", B(!back.Compact && back.PanelFontSize == 13 && back.StatusBarHeight == 24), 1, 0);
        check("JSON hỏng / rỗng → mặc định (không ném lỗi)", B(UiLayoutSettings.FromJson("{ hỏng").Compact && UiLayoutSettings.FromJson("").Windows.Count == 0
            && UiLayoutSettings.FromJson("{\"Density\":\"\"}").Compact), 1, 0);
        check("For(): cửa sổ mới tạo bố cục trống", B(back.For("DetailSheetWindow").PanelWidths.Count == 0), 1, 0);

        // ---- ghi / đọc file (thư mục kết quả kiểm chứng, không dùng %AppData%) ----
        string dir = Path.Combine(outDir, "GiaoDien");
        string f = Path.Combine(dir, "ui.json");
        bool saved = back.Save(f);
        var loaded = UiLayoutSettings.Load(f);
        check("Ghi / đọc ui.json", B(saved && File.Exists(f) && loaded.For("MainWindow").MenuHidden && !loaded.Compact), 1, 0);
        check("Đọc file không tồn tại → mặc định", B(UiLayoutSettings.Load(Path.Combine(dir, "khong-co.json")).Compact), 1, 0);
        check("Đường dẫn mặc định %AppData%\\VietConst\\ui.json", B((UiLayoutSettings.DefaultPath ?? "").EndsWith(Path.Combine("VietConst", "ui.json"))), 1, 0);
    }
}
