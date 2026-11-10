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
        RvGroup("Gán tải theo cấu kiện",
            RvBtn("Tải dầm", "", "Gán tải phân bố / tập trung cho các dầm đang chọn", () => RvShowMemberRibbon(RtDam)),
            RvBtn("Tải sàn", "", "Gán tải ô sàn / theo công năng", () => RvShowMemberRibbon(RtSan)),
            RvBtn("Tải cột", "", "Gán tải đầu cột", () => RvShowMemberRibbon(RtCot)),
            RvBtn("Tải móng", "", "Tải chân cột / móng", () => RvShowMemberRibbon(RtMong)),
            RvBtn("Tải tường", "", "Tải tường lên dầm (tự trừ diện tích cửa đã đặt)", () => RvShowMemberRibbon(RtTuong))),
        RvGroup("Gán tải",
            RvBtn("Tải trên thanh…", "", "Tải đều / hình thang / tập trung trên các thanh đang chọn", () => Do(MnuAssignFrameLoad_Click)),
            RvBtn("Tải trên sàn…", "", "Tải phân bố trên các ô sàn đang chọn", () => Do(MnuAssignAreaLoad_Click)),
            RvBtn("Tải tại nút…", "", "Lực / mô men tại nút đang chọn", () => Do(MnuAssignJointLoad_Click)),
            RvBtn("Tường lên dầm", "", "Bấm dầm để gán tường (tải tường theo loại tường đang chọn)", () => { RvMode("AssignWall"); ShowOptionsFor("Wall"); }),
            RvBtn("Cấu tạo sàn", "", "Bấm ô sàn để gán cấu tạo sàn (lớp hoàn thiện + hoạt tải)", () => RvMode("AssignBuildUp")),
            RvBtn("Sàn vệ sinh", "", "Bấm ô sàn để đổi thành sàn vệ sinh (hoàn thiện nặng hơn)", () => RvMode("WC"))),
        RvGroup("Trường hợp & tổ hợp",
            RvBtn("Trường hợp tải…", "", "Load Patterns: TT, HTHT, HT, gió, động đất…", () => Do(MnuLoadPatterns_Click)),
            RvBtn("Trường hợp phân tích…", "", "Load Cases: tĩnh, dao động, phổ phản ứng", () => Do(MnuLoadCases_Click)),
            RvBtn("Tổ hợp tải…", "", "Sửa tổ hợp tải trọng", () => Do(MnuComboEditor_Click)),
            RvBtn("Tự tạo tổ hợp TCVN", "", "Tự sinh tổ hợp theo TCVN 2737:2023 (TTGH I, II, BAO)", () => Do(MnuCombos_Click)),
            RvBtn("Khối lượng & dao động…", "", "Nguồn khối lượng, số dạng dao động riêng", () => Do(MnuModal_Click)),
            RvBtn("Phổ động đất…", "", "Phổ phản ứng / động đất TCVN 9386-1:2025", () => Do(MnuSpectrum_Click)),
            RvBtn("Sàn cứng…", "", "Sàn cứng (diaphragm) theo tầng", () => Do(MnuDiaphragms_Click))),
        RvGroup("Thư viện tải",
            RvBtn("Loại tường…", "", "Loại tường (chiều dày, γ, trát) → tải 1 m²", () => Do(MnuWallTypes_Click)),
            RvBtn("Cấu tạo sàn…", "", "Thư viện cấu tạo sàn (lớp hoàn thiện + hoạt tải)", () => Do(MnuBuildUps_Click)),
            RvBtn("Tường đã khai báo…", "", "Bảng các đoạn tường đã gán / vẽ", () => Do(MnuWallList_Click)),
            RvBtn("Cầu thang…", "", "Khai báo cầu thang: tải bản thang, chiếu nghỉ dồn vào dầm", () => Do(BtnStair_Click))),
        RvGroup("Tự động & xem",
            RvBtn("Tường CAD → tải dầm", "", "Tường trên bản vẽ CAD liên kết nằm trên dầm → tạo tường, tải dồn vào dầm", CadWallsToBeams),
            RvBtn("Gợi ý lanh tô", "", "Lanh tô đề xuất cho mọi cửa đã đặt", ShowLintelProposals),
            RvBtn("Hiện tải gán", "", "Bật / tắt hiển thị tải đã gán trên mặt bằng", ToggleAssignedLoads),
            RvBtn("Truyền tải chân cột…", "", "Tải đứng dồn xuống từng chân cột theo diện chịu tải → gợi ý móng, số cọc từng hố móng", ShowTakedownUi))
    };

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
        RvGroup("Mô hình",
            RvBtn("Kiểm tra mô hình…", "", "Nút trùng, thanh trùng, dầm hở, cạnh sàn không dầm, trường hợp tải rỗng… – sửa tự động", () => Do(MnuCheckModel_Click)),
            RvBtn("Phân tích (F5)", "", "Chạy phân tích kết cấu", () => Do(MnuRun_Click))),
        RvGroup("Kết quả phân tích",
            RvBtn("Hình biến dạng…", "", "Chuyển vị, hình biến dạng theo trường hợp / tổ hợp", () => Do(MnuDeformed_Click)),
            RvBtn("Biểu đồ nội lực", "", "M, V, N trên mặt bằng / mặt đứng trục", () => RvViewTab("Biểu đồ nội lực")),
            RvBtn("Dạng dao động", "", "Chu kỳ, dạng dao động riêng", () => RvViewTab("Dao động")),
            RvBtn("Phản lực gối…", "", "Phản lực chân cột theo trường hợp / tổ hợp", () => Do(MnuReactions_Click)),
            RvBtn("Bảng kết quả…", "", "Chuyển vị, phản lực, nội lực, lệch tầng… (Ctrl+T)", () => Do(MnuTables_Click))),
        RvGroup("Kiểm tra cấu kiện",
            RvBtn("Kiểm tra BTCT", "", "Thiết kế / kiểm tra dầm, cột, sàn BTCT (TCVN 5574:2018)", () => Do(MnuDesignConcrete_Click)),
            RvBtn("Kiểm tra thép", "", "Kiểm tra cấu kiện thép (TCVN 5575:2024)", () => Do(MnuCheckSteel_Click)),
            RvBtn("An toàn móng…", "", "Áp lực đáy, lún, chọc thủng, sức chịu tải cọc của móng / đài đang chọn", () => Do(BtnFndSafety_Click)),
            RvBtn("Tự tăng tiết diện", "", "Tăng tiết diện cấu kiện không đạt đến khi đạt", () => Do(MnuAutoResize_Click))),
        RvGroup("Đối chiếu",
            RvBtn("So sánh ETABS / SAP…", "", "So sánh nội lực, phản lực với kết quả ETABS / SAP / SAFE", () => Do(MnuCrossCheck_Click)),
            RvBtn("Nhập kết quả ngoài…", "", "Nhập nội lực từ ETABS / SAP / SAFE / phần mềm khác", () => Do(MnuImportResults_Click)))
    };

    // ------------------------------------------------------------------
    // TRIỂN KHAI HỒ SƠ
    // ------------------------------------------------------------------
    private FrameworkElement[] DeliverCommandGroups() => new[]
    {
        RvGroup("Bản vẽ",
            RvBtn("Dàn trang & in…", "", "Bộ bản vẽ khổ A3/A1, khung tên, in / xuất PDF", () => Do(MnuSheets_Click)),
            RvBtn("Bản vẽ DXF…", "", "Xuất bản vẽ kết cấu DXF / DWG", () => Do(MnuExpDxf_Click)),
            RvBtn("Bản vẽ từng dầm…", "", "Bản vẽ riêng từng dầm D1, D2…: mặt đứng, mặt cắt, thống kê thép", () => Do(BtnBeamSheet_Click)),
            RvBtn("Hiệu chỉnh bản vẽ…", "", "Dời nhãn, sửa chữ – giữ khi thiết kế lại", () => Do(MnuDrawingEdit_Click)),
            RvBtn("Nạp bản vẽ đã sửa…", "", "Nạp lại bản vẽ DXF / DWG đã chỉnh trong AutoCAD", () => Do(MnuDrawingAttach_Click))),
        RvGroup("Cốt thép",
            RvBtn("Chọn bố trí thép…", "", "Chọn số thanh, Ø, kiểu bố trí A/B/C cho cấu kiện đang chọn", () => Do(BtnRebarChoice_Click)),
            RvBtn("Shop drawing thép…", "", "Bảng gia công, cắt cây thép theo chiều dài thương phẩm", () => Do(MnuExpShop_Click)),
            RvBtn("Tuỳ chọn thép…", "", "Tuỳ chọn chọn thép & gia công (kiểu bố trí dầm mặc định, chiều dài cây thép…)", () => Do(MnuRebarPrefs_Click))),
        RvGroup("Thuyết minh & khối lượng",
            RvBtn("Thuyết minh Word…", "", "Thuyết minh tính toán (.docx)", () => Do(MnuExpWord_Click)),
            RvBtn("Thuyết minh tự động…", "", "Chọn chương, trang bìa, mục lục", () => Do(MnuAutoReport_Click)),
            RvBtn("Bảng tính Excel…", "", "Bảng tính kết quả (.xlsx)", () => Do(MnuExpExcel_Click)),
            RvBtn("Khối lượng vật tư…", "", "Bê tông, ván khuôn, thép theo nhóm Ø, xây, trát", () => Do(MnuQuantities_Click))),
        RvGroup("Xuất & chuẩn",
            RvBtn("Xuất tất cả…", "", "Mô hình, bản vẽ, thuyết minh, bảng tính vào một thư mục", () => Do(MnuExpAll_Click)),
            RvBtn("Chuẩn layer CAD…", "", "Tên layer, màu, nét theo chuẩn đơn vị", () => Do(MnuCadStandard_Click)),
            RvBtn("Thiết lập bản vẽ…", "", "Tỷ lệ, cỡ chữ, kiểu kích thước", () => Do(MnuDrawingStyle_Click)),
            RvBtn("Mẫu định dạng…", "", "Template khung tên, thuyết minh", () => Do(MnuTemplates_Click)))
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
