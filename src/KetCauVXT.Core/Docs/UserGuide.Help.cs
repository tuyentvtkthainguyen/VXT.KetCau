using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Docs;

// =====================================================================
// TRỢ GIÚP THEO NGỮ CẢNH (F1) và BẮT ĐẦU NHANH – dữ liệu dùng chung cho giao diện và kiểm thử.
// Mọi giá trị trong các bảng dưới là HelpId của mục hướng dẫn (UserGuide.Content.cs); kiểm thử UserGuideTests bảo đảm tồn tại.
// =====================================================================

/// <summary>Ánh xạ ngữ cảnh giao diện (tab, khung nhìn, chế độ sửa mặt bằng, cửa sổ) → HelpId.</summary>
public static class HelpContext
{
    /// <summary>Mục mở khi không xác định được ngữ cảnh.</summary>
    public const string DefaultId = "quy-trinh";

    /// <summary>Tab bên trái của cửa sổ chính (Header của TabItem).</summary>
    public static readonly IReadOnlyDictionary<string, string> LeftTabs = new Dictionary<string, string>
    {
        ["Công trình"] = "khoi-tao.luoi-truc",
        ["Địa chất"] = "mong.dia-chat",
        ["Tiết diện"] = "mo-hinh.tiet-dien",
        ["Tải trọng"] = "tai-trong.truong-hop",
        ["Kết quả"] = "phan-tich.ket-qua",
        ["Thiết kế"] = "thiet-ke.btct",
    };

    /// <summary>Tab con của tab "Thiết kế".</summary>
    public static readonly IReadOnlyDictionary<string, string> DesignTabs = new Dictionary<string, string>
    {
        ["Dầm"] = "thiet-ke.dam",
        ["Cột"] = "thiet-ke.cot",
        ["Sàn"] = "thiet-ke.san",
        ["Móng"] = "mong.thiet-ke",
        ["Cầu thang"] = "thiet-ke.cau-thang",
        ["Thép"] = "thiet-ke.thep",
    };

    /// <summary>Khung nhìn bên phải (tabView).</summary>
    public static readonly IReadOnlyDictionary<string, string> ViewTabs = new Dictionary<string, string>
    {
        ["Mặt bằng"] = "giao-dien.mat-bang",
        ["3D"] = "giao-dien.khung-nhin",
        ["Biểu đồ nội lực"] = "phan-tich.bieu-do",
        ["Mô hình móng"] = "mong.loai",
        ["Dao động"] = "phan-tich.dao-dong",
    };

    /// <summary>Chế độ sửa mặt bằng (tên enum PlanEditMode của MainWindow).</summary>
    public static readonly IReadOnlyDictionary<string, string> PlanModes = new Dictionary<string, string>
    {
        ["Select"] = "giao-dien.mat-bang",
        ["AddSlab"] = "mo-hinh.san",
        ["Balcony"] = "mo-hinh.ban-cong",
        ["Stair"] = "mo-hinh.cau-thang",
        ["Void"] = "mo-hinh.gieng-troi",
        ["WC"] = "mo-hinh.gieng-troi",
        ["Delete"] = "mo-hinh.chinh-sua",
        ["AddBeam"] = "mo-hinh.dam",
        ["AddWall"] = "mo-hinh.tuong",
        ["AssignWall"] = "mo-hinh.tuong",
        ["AssignBuildUp"] = "mo-hinh.san",
        ["AddCurvedBeam"] = "mo-hinh.dam-cong",
        ["QuickGridBeam"] = "mo-hinh.dam-etabs",
        ["SecondaryBeam"] = "mo-hinh.dam-etabs",
        ["CantileverBeam"] = "mo-hinh.dam-etabs",
        ["MultiSelect"] = "mo-hinh.dam-etabs",
        ["SlabDropMode"] = "mo-hinh.san-ha-cot",
    };

    /// <summary>Cửa sổ / hộp thoại (tên lớp) → mục hướng dẫn.</summary>
    public static readonly IReadOnlyDictionary<string, string> Windows = new Dictionary<string, string>
    {
        ["MainWindow"] = DefaultId,
        ["AppendixWindow"] = "ho-so.phu-luc",
        ["ArchImportDialog"] = "khoi-tao.kien-truc",
        ["DxfImportDialog"] = "khoi-tao.dxf",
        ["PortalFrameDialog"] = "khoi-tao.tien-che",
        ["TemplateWindow"] = "khoi-tao.mau",
        ["SectionManagerWindow"] = "mo-hinh.tiet-dien",
        ["WallTypesWindow"] = "mo-hinh.tuong",
        ["WallListWindow"] = "mo-hinh.tuong",
        ["FloorBuildUpWindow"] = "mo-hinh.san",
        ["StairDialog"] = "mo-hinh.cau-thang",
        ["LocationDbWindow"] = "tai-trong.gio",
        ["ModalDialog"] = "phan-tich.dao-dong",
        ["ResultImportDialog"] = "phan-tich.nhap-ket-qua",
        ["CrossCheckWindow"] = "phan-tich.so-sanh",
        ["FoundationDialog"] = "mong.loai",
        ["PileLayoutDialog"] = "mong.coc",
        ["BoundaryDialog"] = "mong.lech-tam",
        ["EccentricFootingDialog"] = "mong.lech-tam",
        ["CuTramDialog"] = "mong.cu-tram",
        ["SoilPresetWindow"] = "mong.dia-chat",
        ["GeoAdvisorWindow"] = "mong.dia-chat",
        ["RebarChoiceDialog"] = "thiet-ke.chon-thep",
        ["RebarPrefsDialog"] = "thiet-ke.tuy-chon-thep",
        ["DrawingStyleWindow"] = "ho-so.thiet-lap-ban-ve",
        ["CadStandardWindow"] = "ho-so.chuan-layer",
        ["LayerExportDialog"] = "ho-so.chuan-layer",
        ["ExportOptionsDialog"] = "ho-so.xuat",
        ["SheetWindow"] = "ho-so.dan-trang",
        ["TitleBlockDialog"] = "ho-so.khung-ten",
        ["DrawingEditWindow"] = "ho-so.hieu-chinh",
        ["QuantityWindow"] = "ho-so.khoi-luong",
        ["AutoReportDialog"] = "ho-so.thuyet-minh",
        ["VerificationWindow"] = "kiem-chung.bao-cao",
        ["RegulationWindow"] = "phap-ly.danh-muc",
        ["UserGuideWindow"] = "giao-dien.tro-giup",
        ["QuickStartWindow"] = "quy-trinh.bat-dau-nhanh",
        ["ModelCheckWindow"] = "phan-tich.kiem-tra-mo-hinh",
        ["DeformedShapeWindow"] = "phan-tich.bien-dang",
        ["ResultTablesWindow"] = "phan-tich.bang-ket-qua",
        ["ComboEditorWindow"] = "tai-trong.sua-to-hop",
        ["LoadPatternsWindow"] = "tai-trong.truong-hop",
        ["LoadCasesWindow"] = "mo-hinh.khai-bao",
        ["MaterialsWindow"] = "mo-hinh.vat-lieu",
        ["SpectrumDialog"] = "mo-hinh.khai-bao",
        ["CadLinkWindow"] = "khoi-tao.lien-ket-autocad",
        ["CadLinkDiffDialog"] = "khoi-tao.lien-ket-autocad",
    };

    private static string Get(IReadOnlyDictionary<string, string> map, string key) =>
        key != null && map.TryGetValue(key.Trim(), out var v) ? v : null;

    /// <summary>Tab trái (và tab con của "Thiết kế"); null nếu không biết.</summary>
    public static string ForLeftTab(string header, string subTab = null) =>
        (header?.Trim() == "Thiết kế" ? Get(DesignTabs, subTab) : null) ?? Get(LeftTabs, header);

    public static string ForViewTab(string header) => Get(ViewTabs, header);
    public static string ForPlanMode(string mode) => Get(PlanModes, mode);
    public static string ForWindow(string typeName) => Get(Windows, typeName);

    /// <summary>Mọi HelpId xuất hiện trong các bảng ánh xạ và bước Bắt đầu nhanh (kiểm thử).</summary>
    public static IEnumerable<string> AllMappedIds() =>
        new[] { DefaultId }.Concat(LeftTabs.Values).Concat(DesignTabs.Values).Concat(ViewTabs.Values).Concat(PlanModes.Values)
            .Concat(Windows.Values).Concat(QuickStart.Steps.Select(s => s.HelpId)).Distinct();
}

/// <summary>Một bước của bảng "Bắt đầu nhanh". Description có thể chứa đánh dấu {m:…} (hiển thị qua UserGuide.Plain).</summary>
public sealed class QuickStartStep
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>Mục hướng dẫn của bước</summary>
    public string HelpId { get; init; } = "";
    /// <summary>Trạng thái: true = đã làm, false = chưa, null = không tự xác định được (người dùng tự kiểm tra).</summary>
    public Func<Project, bool, DesignSummary, bool?> Status { get; init; } = (p, r, d) => null;
}

/// <summary>Danh sách bước "Bắt đầu nhanh" theo quy trình chuẩn của chương trình; trạng thái tự nhận biết từ dự án.</summary>
public static class QuickStart
{
    public static readonly IReadOnlyList<QuickStartStep> Steps = new List<QuickStartStep>
    {
        new() { Id = "luoi", Title = "Khởi tạo lưới trục, tầng", HelpId = "khoi-tao.luoi-truc",
            Description = "Tab Công trình: bước cột X, Y, chiều cao tầng, cao độ chân cột → Tạo mô hình (hoặc nhập bản vẽ kiến trúc / DWG / Revit / ETABS).",
            Status = (p, r, d) => p.Grids.Count >= 2 && p.Stories.Count >= 1 },
        new() { Id = "tiet-dien", Title = "Tiết diện, vật liệu", HelpId = "mo-hinh.tiet-dien",
            Description = "Khai báo tiết diện dầm/cột, tiết diện sàn, cấp bê tông, nhóm thép.",
            Status = (p, r, d) => p.Frames.Count > 0 && p.Frames.All(f => p.FrameSections.Any(s => s.Name == f.Section)) },
        new() { Id = "cau-kien", Title = "Cột, dầm, sàn", HelpId = "mo-hinh.dam",
            Description = "Sửa mô hình trên tab Mặt bằng: thêm/xoá dầm, dầm phụ, công xôn, ô sàn, ban công, cầu thang, giếng trời.",
            Status = (p, r, d) => p.Frames.Any(f => f.Kind == FrameKind.Column) && p.Frames.Any(f => f.Kind == FrameKind.Beam) && p.Slabs.Count > 0 },
        new() { Id = "tuong", Title = "Tường", HelpId = "mo-hinh.tuong",
            Description = "Vẽ tường / gán tường lên dầm theo loại tường (tải tường dồn vào dầm).",
            Status = (p, r, d) => p.Walls.Count > 0 || p.FrameLoads.Any(l => l.Source == "WALL") },
        new() { Id = "tai-trong", Title = "Tải trọng, tổ hợp", HelpId = "tai-trong.to-hop",
            Description = "Kiểm tra trường hợp tải, γf, gió, động đất; tạo tổ hợp tải.",
            Status = (p, r, d) => p.Patterns.Count > 0 && p.Combinations.Count > 0 },
        new() { Id = "mong", Title = "Địa chất, móng", HelpId = "mong.dia-chat",
            Description = "Nhập hố khoan (tab Địa chất) hoặc chỉ tiêu nền ở tab Công trình; chọn loại móng (tab Mô hình móng).",
            Status = (p, r, d) => p.Boreholes.Count > 0 ? true : d != null && (d.Footings.Count + d.PileCaps.Count + d.StripFootings.Count) > 0 ? (bool?)null : false },
        new() { Id = "phan-tich", Title = "Phân tích", HelpId = "phan-tich.chay",
            Description = "{m:modelCheck} rồi {m:run} (F5): tĩnh, gió, động đất; dao động riêng nếu bật.",
            Status = (p, r, d) => r },
        new() { Id = "kiem-tra", Title = "Kiểm tra kết quả", HelpId = "phan-tich.bieu-do",
            Description = "Xem biểu đồ nội lực, phản lực, cảnh báo; so sánh với ETABS/SAP khi cần.",
            Status = (p, r, d) => r ? (bool?)null : false },
        new() { Id = "thiet-ke", Title = "Thiết kế", HelpId = "thiet-ke.btct",
            Description = "Thiết kế BTCT, móng, kiểm tra thép; chọn bố trí thép; cấu kiện không đạt tô đỏ.",
            Status = (p, r, d) => d != null },
        new() { Id = "ho-so", Title = "Bản vẽ, thuyết minh", HelpId = "ho-so.ban-ve",
            Description = "Bộ bản vẽ DXF, dàn trang, shop drawing, khối lượng, thuyết minh Word ({m:expAll}).",
            Status = (p, r, d) => p.Sheets.Count > 0 ? true : d != null ? (bool?)null : false },
    };

    public static bool? Evaluate(QuickStartStep s, Project p, bool hasResults, DesignSummary design)
    {
        if (p == null) return false;
        try { return s.Status(p, hasResults, design); }
        catch { return null; }
    }
}
