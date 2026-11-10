using System.Windows;
using System.Windows.Controls;

namespace KetCauVXT.App;

/// <summary>[Gom lệnh theo tab] Mọi lệnh GÁN TẢI TRỌNG ở một tab "Tải trọng", mọi lệnh KIỂM TRA ở tab "Kiểm tra", mọi lệnh TRIỂN KHAI
/// hồ sơ ở tab "Triển khai" – cả ribbon kiểu Revit lẫn khung bên trái (giao diện cổ điển). Một định nghĩa nhóm lệnh dùng chung cho hai nơi.
/// Menu cũ giữ nguyên.</summary>
public partial class MainWindow
{
    private static readonly RoutedEventArgs NoArgs = new();

    private void Do(RoutedEventHandler h) => h(this, NoArgs);

    // ------------------------------------------------------------------
    // TẢI TRỌNG
    // ------------------------------------------------------------------
    private FrameworkElement[] LoadCommandGroups() => new[]
    {
        RvStack("Gán tải theo cấu kiện",
            RvBtn("Tải dầm", "\uE896", "Gán tải phân bố / tập trung cho các dầm đang chọn", () => RvShowMemberRibbon(RtDam)),
            RvBtn("Tải sàn", "\uE896", "Gán tải ô sàn / theo công năng", () => RvShowMemberRibbon(RtSan)),
            RvBtn("Tải cột", "\uE896", "Gán tải đầu cột", () => RvShowMemberRibbon(RtCot)),
            RvBtn("Tải móng", "\uE896", "Tải chân cột / móng", () => RvShowMemberRibbon(RtMong)),
            RvBtn("Tải tường", "\uE896", "Tải tường lên dầm (tự trừ diện tích cửa đã đặt)", () => RvShowMemberRibbon(RtTuong))),
        RvStack("Gán tải",
            RvBtn("Tải trên thanh…", "\uE896", "Tải đều / hình thang / tập trung trên các thanh đang chọn", () => Do(MnuAssignFrameLoad_Click)),
            RvBtn("Tải trên sàn…", "\uE896", "Tải phân bố trên các ô sàn đang chọn", () => Do(MnuAssignAreaLoad_Click)),
            RvBtn("Tải tại nút…", "\uE896", "Lực / mô men tại nút đang chọn", () => Do(MnuAssignJointLoad_Click)),
            RvBtn("Tường lên dầm", "\uE896", "Bấm dầm để gán tường (tải tường theo loại tường đang chọn)", () => { RvMode("AssignWall"); ShowOptionsFor("Wall"); }),
            RvBtn("Cấu tạo sàn", "\uE8F1", "Bấm ô sàn để gán cấu tạo sàn (lớp hoàn thiện + hoạt tải)", () => RvMode("AssignBuildUp")),
            RvBtn("Sàn vệ sinh", "\uE8F1", "Bấm ô sàn để đổi thành sàn vệ sinh (hoàn thiện nặng hơn)", () => RvMode("WC"))),
        RvStack("Trường hợp & tổ hợp",
            RvBtn("Trường hợp tải…", "\uE8FD", "Load Patterns: TT, HTHT, HT, gió, động đất…", () => Do(MnuLoadPatterns_Click)),
            RvBtn("Trường hợp phân tích…", "\uE8FD", "Load Cases: tĩnh, dao động, phổ phản ứng", () => Do(MnuLoadCases_Click)),
            RvBtn("Tổ hợp tải…", "\uE8FD", "Sửa tổ hợp tải trọng", () => Do(MnuComboEditor_Click)),
            RvBtn("Tự tạo tổ hợp TCVN", "\uE895", "Tự sinh tổ hợp theo TCVN 2737:2023 (TTGH I, II, BAO)", () => Do(MnuCombos_Click)),
            RvBtn("Khối lượng & dao động…", "\uE8B1", "Nguồn khối lượng, số dạng dao động riêng", () => Do(MnuModal_Click)),
            RvBtn("Phổ động đất…", "\uE7BA", "Phổ phản ứng / động đất TCVN 9386-1:2025", () => Do(MnuSpectrum_Click)),
            RvBtn("Sàn cứng…", "\uE71B", "Sàn cứng (diaphragm) theo tầng", () => Do(MnuDiaphragms_Click))),
        RvStack("Thư viện tải",
            RvBtn("Loại tường…", "\uE8F1", "Loại tường (chiều dày, γ, trát) → tải 1 m²", () => Do(MnuWallTypes_Click)),
            RvBtn("Cấu tạo sàn…", "\uE8F1", "Thư viện cấu tạo sàn (lớp hoàn thiện + hoạt tải)", () => Do(MnuBuildUps_Click)),
            RvBtn("Tường đã khai báo…", "\uE8FD", "Bảng các đoạn tường đã gán / vẽ", () => Do(MnuWallList_Click)),
            RvBtn("Cầu thang…", "\uE8A1", "Khai báo cầu thang: tải bản thang, chiếu nghỉ dồn vào dầm", () => Do(BtnStair_Click))),
        RvStack("Tự động & xem",
            RvBtn("Tường CAD → tải dầm", "\uE8B5", "Tường trên bản vẽ CAD liên kết nằm trên dầm → tạo tường, tải dồn vào dầm", CadWallsToBeams),
            RvBtn("Gợi ý lanh tô", "\uEA80", "Lanh tô đề xuất cho mọi cửa đã đặt", ShowLintelProposals),
            RvBtn("Hiện tải gán", "\uE890", "Bật / tắt hiển thị tải đã gán trên mặt bằng", ToggleAssignedLoads),
            RvBtn("Truyền tải chân cột…", "\uE8EF", "Tải đứng dồn xuống từng chân cột theo diện chịu tải → gợi ý móng, số cọc từng hố móng", ShowTakedownUi))
    };

    /// <summary>Nhóm lệnh gọn kiểu Revit: nút nhỏ (biểu tượng + chữ) xếp 3 nút một cột – ribbon thấp, đủ lệnh trong khung nhìn.</summary>
    private static FrameworkElement RvStack(string title, params Button[] big)
    {
        var col = new WrapPanel { Orientation = Orientation.Vertical, Height = 3 * 25 };
        foreach (var b in big)
        {
            // chuyển nút lớn → nút nhỏ: lấy biểu tượng + chữ từ nội dung nút lớn
            var src = b.Content as StackPanel;
            string glyph = (src?.Children.OfType<TextBlock>().FirstOrDefault())?.Text ?? "";
            string label = (src?.Children.OfType<TextBlock>().Skip(1).FirstOrDefault())?.Text ?? "";
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text = glyph, FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets, Segoe Fluent Icons, Segoe UI Symbol"), FontSize = 14, Width = 20,
                VerticalAlignment = VerticalAlignment.Center, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 70, 120))
            });
            row.Children.Add(new TextBlock { Text = label, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
            b.Content = row;
            b.MinHeight = 0; b.Height = 24; b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Padding = new Thickness(3, 0, 6, 0); b.Margin = new Thickness(1, 0, 1, 1);
            col.Children.Add(b);
        }
        return RvGroup(title, col);
    }

    private void ToggleAssignedLoads()
    {
        mnuShowLoads.IsChecked = !mnuShowLoads.IsChecked;
        Do(MnuShowAssignedLoads_Click);
    }

    // ------------------------------------------------------------------
    // KIỂM TRA
    // ------------------------------------------------------------------
    private FrameworkElement[] CheckCommandGroups() => new[]
    {
        RvStack("Mô hình",
            RvBtn("Kiểm tra mô hình…", "\uE9D9", "Nút trùng, thanh trùng, dầm hở, cạnh sàn không dầm, trường hợp tải rỗng… – sửa tự động", () => Do(MnuCheckModel_Click)),
            RvBtn("Phân tích (F5)", "\uE768", "Chạy phân tích kết cấu", () => Do(MnuRun_Click))),
        RvStack("Kết quả phân tích",
            RvBtn("Hình biến dạng…", "\uE890", "Chuyển vị, hình biến dạng theo trường hợp / tổ hợp", () => Do(MnuDeformed_Click)),
            RvBtn("Biểu đồ nội lực", "\uE8A1", "M, V, N trên mặt bằng / mặt đứng trục", () => RvViewTab("Biểu đồ nội lực")),
            RvBtn("Dạng dao động", "\uE8B1", "Chu kỳ, dạng dao động riêng", () => RvViewTab("Dao động")),
            RvBtn("Phản lực gối…", "\uE896", "Phản lực chân cột theo trường hợp / tổ hợp", () => Do(MnuReactions_Click)),
            RvBtn("Bảng kết quả…", "\uE8A5", "Chuyển vị, phản lực, nội lực, lệch tầng… (Ctrl+T)", () => Do(MnuTables_Click))),
        RvStack("Kiểm tra cấu kiện",
            RvBtn("Kiểm tra BTCT", "\uE73E", "Thiết kế / kiểm tra dầm, cột, sàn BTCT (TCVN 5574:2018)", () => Do(MnuDesignConcrete_Click)),
            RvBtn("Kiểm tra thép", "\uE73E", "Kiểm tra cấu kiện thép (TCVN 5575:2024)", () => Do(MnuCheckSteel_Click)),
            RvBtn("An toàn móng…", "\uE73E", "Áp lực đáy, lún, chọc thủng, sức chịu tải cọc của móng / đài đang chọn", () => Do(BtnFndSafety_Click)),
            RvBtn("Tự tăng tiết diện", "\uE895", "Tăng tiết diện cấu kiện không đạt đến khi đạt", () => Do(MnuAutoResize_Click))),
        RvStack("Đối chiếu",
            RvBtn("So sánh ETABS / SAP…", "\uE8AB", "So sánh nội lực, phản lực với kết quả ETABS / SAP / SAFE", () => Do(MnuCrossCheck_Click)),
            RvBtn("Nhập kết quả ngoài…", "\uE8B5", "Nhập nội lực từ ETABS / SAP / SAFE / phần mềm khác", () => Do(MnuImportResults_Click)))
    };

    // ------------------------------------------------------------------
    // TRIỂN KHAI HỒ SƠ
    // ------------------------------------------------------------------
    private FrameworkElement[] DeliverCommandGroups() => new[]
    {
        RvStack("Bản vẽ",
            RvBtn("Dàn trang & in…", "\uE749", "Bộ bản vẽ khổ A3/A1, khung tên, in / xuất PDF", () => Do(MnuSheets_Click)),
            RvBtn("Bản vẽ DXF…", "\uEDE1", "Xuất bản vẽ kết cấu DXF / DWG", () => Do(MnuExpDxf_Click)),
            RvBtn("Bản vẽ từng dầm…", "\uE8A5", "Bản vẽ riêng từng dầm D1, D2…: mặt đứng, mặt cắt, thống kê thép", () => Do(BtnBeamSheet_Click)),
            RvBtn("Hiệu chỉnh bản vẽ…", "\uE70F", "Dời nhãn, sửa chữ – giữ khi thiết kế lại", () => Do(MnuDrawingEdit_Click)),
            RvBtn("Nạp bản vẽ đã sửa…", "\uE8B5", "Nạp lại bản vẽ DXF / DWG đã chỉnh trong AutoCAD", () => Do(MnuDrawingAttach_Click))),
        RvStack("Cốt thép",
            RvBtn("Chọn bố trí thép…", "\uE8EF", "Chọn số thanh, Ø, kiểu bố trí A/B/C cho cấu kiện đang chọn", () => Do(BtnRebarChoice_Click)),
            RvBtn("Shop drawing thép…", "\uE719", "Bảng gia công, cắt cây thép theo chiều dài thương phẩm", () => Do(MnuExpShop_Click)),
            RvBtn("Tuỳ chọn thép…", "\uE713", "Tuỳ chọn chọn thép & gia công (kiểu bố trí dầm mặc định, chiều dài cây thép…)", () => Do(MnuRebarPrefs_Click))),
        RvStack("Thuyết minh & khối lượng",
            RvBtn("Thuyết minh Word…", "\uE8A5", "Thuyết minh tính toán (.docx)", () => Do(MnuExpWord_Click)),
            RvBtn("Thuyết minh tự động…", "\uE8A5", "Chọn chương, trang bìa, mục lục", () => Do(MnuAutoReport_Click)),
            RvBtn("Bảng tính Excel…", "\uE8EF", "Bảng tính kết quả (.xlsx)", () => Do(MnuExpExcel_Click)),
            RvBtn("Khối lượng vật tư…", "\uE8FD", "Bê tông, ván khuôn, thép theo nhóm Ø, xây, trát", () => Do(MnuQuantities_Click))),
        RvStack("Xuất & chuẩn",
            RvBtn("Xuất tất cả…", "\uEDE1", "Mô hình, bản vẽ, thuyết minh, bảng tính vào một thư mục", () => Do(MnuExpAll_Click)),
            RvBtn("Chuẩn layer CAD…", "\uE713", "Tên layer, màu, nét theo chuẩn đơn vị", () => Do(MnuCadStandard_Click)),
            RvBtn("Thiết lập bản vẽ…", "\uE713", "Tỷ lệ, cỡ chữ, kiểu kích thước", () => Do(MnuDrawingStyle_Click)),
            RvBtn("Mẫu định dạng…", "\uE8D2", "Template khung tên, thuyết minh", () => Do(MnuTemplates_Click)))
    };

    /// <summary>Khung bên trái (giao diện cổ điển / Thông tin dự án): đặt các nhóm lệnh vào đầu tab Tải trọng, Kiểm tra, Triển khai.</summary>
    private void BuildClassicCommandTabs()
    {
        static FrameworkElement Panel(FrameworkElement[] groups)
        {
            var wp = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            foreach (var g in groups) wp.Children.Add(g);
            return new Border
            {
                Child = wp, Background = RvRibbonBack, BorderBrush = System.Windows.Media.Brushes.Gainsboro, BorderThickness = new Thickness(1), Padding = new Thickness(2),
                Margin = new Thickness(0, 0, 0, 6)
            };
        }
        try
        {
            ccLoadCommands.Content = Panel(LoadCommandGroups());
            ccCheckCommands.Content = Panel(CheckCommandGroups());
            ccDeliverCommands.Content = Panel(DeliverCommandGroups());
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Gom lệnh theo tab: " + ex.Message); }
    }
}
