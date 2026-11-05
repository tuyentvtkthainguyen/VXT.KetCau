using KetCauVXT.Model;

namespace KetCauVXT.Docs;

// =====================================================================
// NỘI DUNG HƯỚNG DẪN SỬ DỤNG – mô tả đúng menu, nút, hộp thoại của phiên bản hiện tại (MainWindow.xaml, MainWindow.*.cs, các hộp thoại).
// Đường dẫn menu viết {m:mã} (bảng UserGuide.Menus); tham chiếu mục {s:help-id}. HelpId là khoá ổn định (F1, nút "?").
// Mục Pending = true: tính năng đang được phát triển song song – cập nhật nội dung khi tính năng hoàn thành (tìm "pending: true").
// =====================================================================
public static partial class UserGuide
{
    private static GuideSection Ch(string helpId, string title) => new() { HelpId = helpId, Title = title, Level = 1 };

    private static List<GuideSection> BuildChapters() => new()
    {
        ChIntro(), ChInstall(), ChUi(), ChModes(), ChWorkflow(), ChStart(), ChModel(), ChLoads(),
        ChAnalysis(), ChDesign(), ChDocs(), ChVerify(), ChLegal(), ChFaq(), ChAppendix()
    };

    // ==================================================================
    // 1. GIỚI THIỆU
    // ==================================================================
    private static GuideSection ChIntro()
    {
        var c = Ch("gioi-thieu", "Giới thiệu");
        c.P("**Việt Const** là phần mềm phân tích – thiết kế kết cấu cho **nhà dân dụng dưới 7 tầng** (khung bê tông cốt thép) và **nhà thép tiền chế** (khung zamil). "
            + "Chương trình lập mô hình khung không gian, tính nội lực bằng phần tử hữu hạn, thiết kế cốt thép dầm, cột, sàn, móng, cầu thang theo tiêu chuẩn Việt Nam, "
            + "rồi triển khai hồ sơ: bản vẽ DXF, dàn trang in, bảng gia công thép, bảng khối lượng, thuyết minh Word, bảng tính Excel và mô hình ETABS/SAP2000 để kiểm tra chéo.",
            "Hướng dẫn này đi từ cài đặt, khởi tạo dự án đến khi sử dụng kết quả, dành cho cả kỹ sư đang hành nghề và người mới bắt đầu. "
            + "Trong chương trình, nhấn **F1** để mở đúng mục hướng dẫn của tab, công cụ hoặc hộp thoại đang dùng.");

        c.Sec("gioi-thieu.pham-vi", "Phạm vi áp dụng")
            .Bullets(
                "Nhà ở riêng lẻ, nhà phố, nhà liền kề, biệt thự, nhà văn phòng nhỏ **dưới 7 tầng**, khung BTCT toàn khối (cột – dầm – sàn), móng đơn, móng băng, móng cọc, gia cố nền bằng cừ tràm. "
                + "Khi tạo mô hình quá 7 tầng chương trình cảnh báo: kết quả với nhà cao hơn cần kiểm tra thêm.",
                "Nhà xưởng, nhà kho **khung thép tiền chế** một nhịp (cột, kèo tiết diện chữ I tổ hợp có thể vát), kiểm tra bền, ổn định, độ mảnh, ổn định cục bộ bụng, chuyển vị.",
                "Dầm cong trên mặt bằng (ban công cong, mặt tiền cong), cầu thang (bản chịu lực, có dầm cốn), ban công công xôn, giếng trời, sàn vệ sinh hạ cốt.",
                "Nhập mô hình từ bản vẽ kiến trúc DWG/DXF, add-in AutoCAD, add-in Revit, file ETABS .e2k / SAP2000 .s2k; nhập kết quả nội lực từ ETABS/SAP2000/SAFE.")
            .Note("Chương trình là công cụ hỗ trợ. Kỹ sư có chứng chỉ hành nghề chịu trách nhiệm kiểm tra số liệu đầu vào (đặc biệt hệ số tải trọng, điều kiện địa chất) và kết quả trước khi dùng cho hồ sơ chính thức.");

        c.Sec("gioi-thieu.tieu-chuan", "Tiêu chuẩn và căn cứ áp dụng")
            .P("Danh mục dưới đây lấy trực tiếp từ danh mục văn bản của chương trình ({m:regs}); thuyết minh, phụ lục, ghi chú chung bản vẽ và bảng khối lượng trích dẫn theo đúng danh mục này.")
            .Table(new[] { "Nội dung", "Văn bản / tiêu chuẩn" },
                new[] { "Tải trọng và tác động", Std.Loads },
                new[] { "Số liệu điều kiện tự nhiên (vùng gió, gia tốc nền)", Std.NaturalData },
                new[] { "Kết cấu bê tông cốt thép", Std.Concrete },
                new[] { "Thép cốt bê tông", Std.Rebar },
                new[] { "Kết cấu thép", Std.Steel },
                new[] { "Động đất", Std.Seismic + "; " + Std.SeismicGeo },
                new[] { "Nền nhà và công trình", Std.Soil },
                new[] { "Móng cọc", Std.Piles },
                new[] { "Luật Xây dựng", Std.Law },
                new[] { "Quản lý hoạt động xây dựng", Std.Decree },
                new[] { "Cấp công trình", Std.Grade },
                new[] { "Định mức (bảng khối lượng, tham khảo)", Std.Norms + "; " + Std.NormLetter })
            .Note(Std.SeismicMethodNote);

        c.Sec("gioi-thieu.cau-truc", "Cấu trúc phần mềm")
            .Table(new[] { "Thành phần", "Chức năng" },
                new[] { "KetCauVXT.exe (giao diện WPF)", "Cửa sổ chính, các hộp thoại, dàn trang, tự kiểm tra giao diện (--selftest)." },
                new[] { "KetCauVXT.Core (thư viện lõi)", "Mô hình, phân tích phần tử hữu hạn (khung Timoshenko 12 bậc tự do, sàn cứng, giải phóng liên kết, thanh vát), tải trọng, thiết kế BTCT / thép / móng, bản vẽ DXF, Word, Excel, ETABS/SAP, kiểm chứng. Không dùng thư viện ngoài." },
                new[] { "KetCauVXT.Dwg", "Đọc trực tiếp bản vẽ DWG (AutoCAD R14 … 2018+) bằng thư viện ACadSharp (MIT); nếu đọc lỗi thì thử ODA File Converter." },
                new[] { "Add-in AutoCAD 2021–2026 (tuỳ chọn)", "Lệnh VXT, VXT_TUONG, VXT_COT, VXT_GUI… tích chọn đối tượng kiến trúc rồi gửi sang chương trình." },
                new[] { "Add-in Revit 2023–2026 (tuỳ chọn)", "Lệnh \"Xuất sang Việt Const\": xuất lưới, tầng, cột, dầm, sàn ra file JSON." })
            .P("Đơn vị nội bộ là **kN, m**. Kích thước tiết diện nhập bằng **mm**; bản vẽ, thuyết minh hiển thị mm, MPa, kN.m. Số thập phân trong các ô nhập dùng dấu chấm (vd. 3.6).");

        c.Sec("gioi-thieu.gioi-han", "Giới hạn đã biết của phiên bản hiện tại")
            .Bullets(
                "Phân tích tĩnh tuyến tính bậc nhất: chưa kể P-Delta, phi tuyến vật liệu, phân phối lại nội lực; chưa tính bề rộng vết nứt acrc.",
                "Gió: chỉ thành phần tĩnh W = W0·k(z)·c (nhà thấp tầng); k(z) dạng hàm mũ – đối chiếu bảng của TCVN 2737:2023.",
                "Động đất: tĩnh lực ngang tương đương (mặc định) hoặc phổ phản ứng sau phân tích dao động riêng; phổ loại 1 lập theo cơ sở EN 1998-1 như TCVN 9386:2012 – người dùng đối chiếu bản 2025 (phổ, hệ số tầm quan trọng, gia tốc nền).",
                "Thiết kế dầm (uốn, cắt, võng) theo tiết diện **chữ nhật b×h** – không kể cánh sàn chịu nén (thiên về an toàn); bản vẽ mặt cắt dầm vẫn thể hiện cánh sàn chữ T / L ({s:mo-hinh.canh-san}). Chịu cắt kiểm tra với c = 2h0 (đơn giản hoá) – kiểm tra lại dầm chịu lực tập trung lớn.",
                "Ô sàn bị tường hoặc dầm xiên cắt vẫn tính như ô chữ nhật (Marcus theo hình bao); tự kiểm tra các ô sàn hình dạng đặc biệt.",
                "Độ võng dùng độ cứng gần đúng (φb,cr = 2,5); lún dùng β = 0,8 cho mọi loại đất, ứng suất theo Boussinesq.",
                "Kết quả nội lực nhập từ ETABS/SAP2000/SAFE chỉ dùng trong phiên làm việc, **không lưu vào file dự án** – mở lại dự án phải nhập lại.",
                "Khung thép tiền chế: chưa thiết kế liên kết bu lông, bản mã; bộ bản vẽ KC-01…KC-08 dành cho kết cấu BTCT (chưa có bản vẽ chi tiết khung thép). Đối chiếu công thức với TCVN 5575:2024 trước khi dùng cho hồ sơ chính thức.",
                "Nhập bản vẽ kiến trúc: chưa hỗ trợ tường xiên/cong, tầng hầm, tầng lửng, bản vẽ PDF/ảnh; tường tầng 1 coi như đặt trên giằng móng.",
                "Khối lượng, định mức là giá trị tham khảo – không thay thế dự toán được lập và thẩm tra theo quy định.",
                "Trên mặt bằng chưa có công cụ vẽ cột riêng: cột được tạo khi tạo khung từ lưới trục, nhập bản vẽ / Revit / ETABS (xem {s:mo-hinh.cot}).")
            .P("Danh sách giới hạn đầy đủ, kèm bài toán kiểm chứng tương ứng, nằm ở mục 7 \"Giới hạn đã biết\" của Báo cáo kiểm chứng ({m:verify}).");

        c.Sec("gioi-thieu.quy-uoc", "Quy ước trong tài liệu")
            .Bullets(
                "**Tệp → Mới**: đường dẫn menu (menu Tệp, mục Mới). Tên nút, tab, ô nhập viết **đậm** đúng như trên màn hình.",
                "Trục 1, 2, 3… song song phương Y, đặt theo phương X; trục A, B, C… song song phương X, đặt theo phương Y.",
                "Quy ước dấu nội lực giống ETABS: P kéo dương, M3 dương căng thớ dưới dầm; biểu đồ mô men vẽ về phía thớ chịu kéo.",
                "Ô ghi \"Lưu ý\", \"Mẹo\", \"Cảnh báo\" là thông tin quan trọng; mục ghi [SẮP CÓ] mô tả tính năng đang được hoàn thiện.");
        return c;
    }

    // ==================================================================
    // 2. CÀI ĐẶT
    // ==================================================================
    private static GuideSection ChInstall()
    {
        var c = Ch("cai-dat", "Cài đặt và cập nhật");
        c.Sec("cai-dat.yeu-cau", "Yêu cầu hệ thống")
            .Bullets("Windows 10 hoặc 11, 64-bit. **Không cần cài .NET** (chương trình đóng gói tự chứa).",
                "Màn hình từ 1366×768 (khuyến nghị 1920×1080) – cửa sổ chính mặc định 1440×860.",
                "Tuỳ chọn: AutoCAD 2021–2026 (add-in liên kết), Revit 2023–2026 (add-in xuất mô hình), Microsoft Word/Excel hoặc LibreOffice để mở thuyết minh và bảng tính, ETABS/SAP2000 để kiểm tra chéo.");

        c.Sec("cai-dat.bo-cai", "Cài bằng bộ cài VietConst-Setup.exe")
            .P("Bộ cài được chia nhiều phần: **VietConst-Setup.exe** và các file **VietConst-Setup-*.bin**. Tải **tất cả** về **cùng một thư mục** rồi chạy file .exe (thiếu file .bin bộ cài sẽ báo hỏi đĩa tiếp theo).")
            .Steps(
                "Lưu và đóng Việt Const nếu đang mở – bộ cài không tự đóng chương trình để tránh mất dữ liệu chưa lưu.",
                "Chạy **VietConst-Setup.exe**. Chọn kiểu cài: **chỉ cho tôi** (không cần quyền quản trị, cài vào %LocalAppData%\\Programs\\KetCauVXT) hoặc **cho mọi người dùng** (cần quyền quản trị, cài vào Program Files).",
                "Đọc trang giới thiệu, chọn thư mục cài (mặc định ...\\KetCauVXT).",
                "Trang **Tuỳ chọn thêm**: Tạo biểu tượng trên Desktop; Mở file dự án .kcvxt bằng Việt Const (liên kết file); **Cài add-in AutoCAD 2021–2026** (mặc định không chọn – chọn nếu dùng liên kết AutoCAD).",
                "Bấm Cài đặt, xong chọn **Mở Việt Const**.")
            .P("Sau khi cài, menu Start có nhóm **Việt Const** gồm: chương trình, **Dự án mẫu** (thư mục DuAnMau: NhaPho.kcvxt, TuBanVeKienTruc.kcvxt và bản vẽ kiến trúc mẫu *_DauVao.dxf), **Hướng dẫn sử dụng**, **Hướng dẫn sử dụng (Word)** và **Gỡ cài đặt**.");

        c.Sec("cai-dat.smartscreen", "Cảnh báo Windows SmartScreen")
            .P("Bộ cài và chương trình chưa có chữ ký số thương mại nên lần đầu chạy Windows có thể hiện \"Windows đã bảo vệ máy tính của bạn\". "
               + "Bấm **Thông tin thêm** (More info) → **Vẫn chạy** (Run anyway). Chỉ làm như vậy với file tải từ trang phát hành chính thức của dự án (GitHub Releases của tác giả).")
            .Tip("Nếu trình duyệt chặn tải file .bin, chọn \"Giữ lại\" (Keep). Có thể bấm chuột phải file → Properties → tích Unblock trước khi chạy.");

        c.Sec("cai-dat.autocad", "Add-in AutoCAD (liên kết AutoCAD → Việt Const)")
            .Steps(
                "Cách 1: khi cài chọn **Cài add-in AutoCAD 2021–2026** – gói KetCauVXT.bundle được chép vào thư mục tự nạp ApplicationPlugins (cho mọi người dùng: ProgramData; cho một người: AppData).",
                "Cách 2 (bản zip): đóng AutoCAD, bấm đúp **AutoCAD\\CaiDatAutoCAD.cmd**; gỡ bằng **CaiDatAutoCAD.cmd -Go**. Có thể nạp thủ công bằng lệnh NETLOAD (chọn DLL đúng năm AutoCAD).",
                "Mở AutoCAD, nếu được hỏi chọn **Always Load / Luôn nạp**, gõ lệnh **VXT** để hiện bảng lệnh.",
                "Mở KetCauVXT.exe ít nhất một lần để chương trình ghi đường dẫn vào %AppData%\\KetCauVXT\\app_path.txt – add-in dùng đường dẫn này để tự mở chương trình khi gửi dữ liệu.")
            .P("Cách dùng các lệnh: xem {s:khoi-tao.autocad}.");

        c.Sec("cai-dat.revit", "Add-in Revit")
            .P("Add-in **KetCauVXT.RevitExport** được build riêng cho Revit 2023–2026. Chép KetCauVXT.RevitExport.dll và KetCauVXT.RevitExport.addin vào %AppData%\\Autodesk\\Revit\\Addins\\<năm>\\, "
               + "mở Revit → tab Add-Ins → External Tools → **Xuất sang Việt Const**. Xem {s:khoi-tao.revit}.");

        c.Sec("cai-dat.zip", "Bản chạy ngay không cần cài")
            .P("Giải nén **KetCauVXT-win-x64.zip** vào một thư mục bất kỳ (không cần quyền quản trị) và chạy **KetCauVXT.exe**. Bản zip có kèm thư mục AutoCAD (add-in + CaiDatAutoCAD.cmd) và HuongDan.md. Bản này không tạo liên kết file .kcvxt và không có mục gỡ cài đặt – xoá thư mục là gỡ.");

        c.Sec("cai-dat.cap-nhat", "Cập nhật phiên bản")
            .Bullets("Tải bộ cài mới (cùng tất cả file .bin) và chạy đè lên bản cũ: bộ cài nhớ thư mục và các tuỳ chọn đã chọn lần trước.",
                "Dữ liệu dự án (.kcvxt) và thiết lập cá nhân (%AppData%\\KetCauVXT) **không bị xoá** khi cài đè hoặc gỡ cài đặt.",
                "Sau khi cập nhật nên mở {m:regs} để xem danh mục văn bản mới và bấm **Kiểm tra dự án** với các dự án cũ.");

        c.Sec("cai-dat.go", "Gỡ cài đặt")
            .P("Settings → Apps → **Việt Const** → Uninstall, hoặc menu Start → Việt Const → **Gỡ cài đặt Việt Const**. Gỡ cài đặt xoá cả add-in AutoCAD trong ApplicationPlugins (nếu đã cài), giữ nguyên dự án và %AppData%\\KetCauVXT.");

        c.Sec("cai-dat.du-lieu", "Dữ liệu người dùng (%AppData%\\KetCauVXT)")
            .Table(new[] { "File / thư mục", "Nội dung" },
                new[] { "CadStandard.json", "Chuẩn layer CAD, font, thiết lập bản vẽ mặc định của người dùng ({m:cadStd}, {m:drawStyle})." },
                new[] { "Templates\\", "Thư viện mẫu định dạng *.kcvxt-mau ({m:templates})." },
                new[] { "DiaDanh.json", "Danh mục địa danh – vùng gió, agR, loại nền do người dùng nhập / lưu ({m:locations})." },
                new[] { "VanBanPhapLy.json", "Danh mục văn bản pháp lý người dùng lưu làm mặc định ({m:regs})." },
                new[] { "KiemChung.json", "Giá trị kiểm chứng độc lập do người dùng nhập ({m:verify})." },
                new[] { "XuatBanVe.json", "Lựa chọn xuất bản vẽ gần nhất (gộp / tách, mẫu tên file)." },
                new[] { "Link\\acad_*.json", "Dữ liệu gửi từ AutoCAD (lệnh VXT_GUI)." },
                new[] { "app_path.txt", "Đường dẫn KetCauVXT.exe cho add-in AutoCAD." },
                new[] { "BatDauNhanh.json", "Tuỳ chọn \"Không hiện lại\" của bảng Bắt đầu nhanh." })
            .Tip("Sao lưu thư mục này khi chuyển máy để giữ chuẩn layer, mẫu định dạng, danh mục địa danh và văn bản pháp lý của bạn.");
        return c;
    }

    // ==================================================================
    // 3. GIAO DIỆN
    // ==================================================================
    private static GuideSection ChUi()
    {
        var c = Ch("giao-dien", "Giao diện tổng quan");
        c.Sec("giao-dien.tong-quan", "Bố cục cửa sổ chính")
            .Table(new[] { "Vùng", "Nội dung" },
                new[] { "Menu (trên cùng)", "Bố cục kiểu ETABS: Tệp, Sửa, Xem, Khai báo, Vẽ, Chọn, Gán, Phân tích, Hiển thị, Thiết kế, Triển khai hồ sơ, Tuỳ chọn, Trợ giúp – xem {s:giao-dien.menu}." },
                new[] { "Thanh công cụ", "Các lệnh hay dùng: Mới, Mở, Lưu, nhập bản vẽ, tạo khung, ▶ Phân tích, thiết kế, Xuất tất cả." },
                new[] { "Cột trái – các tab nhập liệu", "Công trình, Địa chất, Tiết diện, Tải trọng, Kết quả, Thiết kế." },
                new[] { "Bên phải – khung nhìn", "Ô **Tầng đang xem**, nút **Vừa màn hình**, tuỳ chọn **Tên cấu kiện**, **Biểu đồ M3 theo**; các tab Mặt bằng, 3D, Biểu đồ nội lực, Mô hình móng, Dao động." },
                new[] { "Dưới khung nhìn", "Ô thông tin cấu kiện đang chọn (trái) và biểu đồ nội lực của thanh đang chọn (phải)." },
                new[] { "Thanh trạng thái (dưới cùng)", "Thông báo, gợi ý của công cụ đang dùng, thanh tiến trình khi tính, đường dẫn file dự án." })
            .P("Tiêu đề cửa sổ ghi tên công trình, tên file và dấu * khi có thay đổi chưa lưu. Có thể kéo vách ngăn giữa cột trái và khung nhìn để đổi bề rộng.");

        var menu = c.Sec("giao-dien.menu", "Menu chính");
        menu.P("Menu sắp xếp theo trình tự làm việc như ETABS: **Tệp** (dự án, nhập / xuất mô hình) → **Sửa** → **Xem** → **Khai báo** (vật liệu, tiết diện, tải, tổ hợp, khối lượng, phổ) → **Vẽ** (khung, dầm, sàn, cầu thang, tường) → **Chọn** → **Gán** (tải, liên kết, giải phóng, tiết diện, hệ số độ cứng) → **Phân tích** → **Hiển thị** (biến dạng, biểu đồ, dao động, phản lực, bảng kết quả) → **Thiết kế** → **Triển khai hồ sơ** → **Tuỳ chọn** → **Trợ giúp**.",
            "Bảng dưới liệt kê các mục menu được hướng dẫn nhắc tới (tự lập từ bảng đường dẫn menu của chương trình).");
        menu.Table(new[] { "Menu", "Mục", "Xem mục" }, MenuRows());

        c.Sec("giao-dien.thanh-cong-cu", "Thanh công cụ")
            .Table(new[] { "Nút", "Tương đương" },
                new[] { "Mới / Mở / Lưu", "{m:new} (Ctrl+N), {m:open} (Ctrl+O), {m:save} (Ctrl+S)" },
                new[] { "Cây mô hình", "{m:explorer} (Ctrl+E) – hiện / ẩn cây mô hình bên trái" },
                new[] { "Nhập DWG/DXF", "{m:importDxf}" },
                new[] { "Từ bản vẽ KT", "{m:importArch}" },
                new[] { "Nhập Revit", "{m:importRevit}" },
                new[] { "Tạo khung BTCT", "{m:buildGrid}" },
                new[] { "Khung tiền chế", "{m:portal}" },
                new[] { "Kiểm tra MH", "{m:modelCheck}" },
                new[] { "▶ Phân tích", "{m:run} (F5)" },
                new[] { "Thiết kế BTCT / Kiểm tra thép", "{m:designRc}, {m:checkSteel}" },
                new[] { "Bảng kết quả", "{m:resultTables} (Ctrl+T)" },
                new[] { "Xuất tất cả", "{m:expAll}" });

        c.Sec("giao-dien.tab-trai", "Các tab nhập liệu (cột trái)")
            .Table(new[] { "Tab", "Nội dung", "Xem mục" },
                new[] { "Công trình", "Thông tin công trình; lưới trục và tầng; tiết diện mặc định; tải trọng tiêu chuẩn; địa điểm xây dựng (vùng gió, động đất); gió; động đất; vật liệu; nền đất dưới móng đơn; nút **Tạo mô hình**, **Áp dụng thông số**.", "{s:khoi-tao.luoi-truc}" },
                new[] { "Địa chất", "Hố khoan khảo sát, các lớp đất, hình trụ hố khoan, áp dụng cho thiết kế móng, gợi ý phương án móng.", "{s:mong.dia-chat}" },
                new[] { "Tiết diện", "Bảng tiết diện thanh (chữ nhật BTCT, chữ I tổ hợp) và tiết diện sàn.", "{s:mo-hinh.tiet-dien}" },
                new[] { "Tải trọng", "Trường hợp tải, tổ hợp tải trọng, tải trên thanh, tải trên sàn.", "{s:tai-trong.truong-hop}" },
                new[] { "Kết quả", "Nội lực theo trường hợp/tổ hợp và tầng, phản lực chân cột, cảnh báo / thông tin.", "{s:phan-tich.ket-qua}" },
                new[] { "Thiết kế", "Các tab con Dầm, Cột, Sàn, Móng, Cầu thang, Thép; cấu kiện không đạt tô đỏ.", "{s:thiet-ke.btct}" });

        c.Sec("giao-dien.khung-nhin", "Khung nhìn: Mặt bằng, 3D, Biểu đồ nội lực, Mô hình móng, Dao động")
            .Bullets(
                "**Mặt bằng**: mặt bằng tầng đang xem, thanh công cụ sửa mô hình và công cụ dầm kiểu ETABS ({s:giao-dien.mat-bang}). Con lăn chuột thu phóng; bấm chọn cấu kiện; nút **Vừa màn hình** đưa toàn bộ mặt bằng vào khung.",
                "**3D**: kéo chuột trái để xoay quanh tâm mô hình, con lăn để thu phóng, bấm (không kéo) để chọn cấu kiện. Tuỳ chọn **Làm mờ phần còn lại** khi đang chọn một cấu kiện.",
                "**Biểu đồ nội lực**: M3, M2, V2, V3, N, T trên mặt bằng tầng, mặt đứng theo trục hoặc 3D ({s:phan-tich.bieu-do}).",
                "**Mô hình móng**: mặt bằng móng, mặt cắt móng đang chọn, các nút thiết kế móng ({s:mong.loai}).",
                "**Dao động**: hoạt hình dạng dao động riêng, chu kỳ, tỷ lệ khối lượng tham gia ({s:phan-tich.dao-dong}).")
            .P("Lựa chọn dùng chung cho mọi khung nhìn: cấu kiện chọn ở mặt bằng sáng màu vàng cả trên 3D, biểu đồ và mô hình móng.");

        c.Sec("giao-dien.mat-bang", "Thanh công cụ của tab Mặt bằng")
            .P("Chọn tầng ở ô **Tầng đang xem** trước khi sửa. Mỗi nút là một chế độ; thanh trạng thái luôn ghi cách dùng chế độ đang bật (kèm \"F1: hướng dẫn\").")
            .Table(new[] { "Nút", "Thao tác", "Xem mục" },
                new[] { "Chọn", "Bấm chọn cột, dầm, lỗ sàn, ô sàn (Esc quay về chế độ này).", "{s:mo-hinh.chinh-sua}" },
                new[] { "＋ Ô sàn", "Bấm vào ô kín bởi dầm để thêm ô sàn theo ô **Công năng**.", "{s:mo-hinh.san}" },
                new[] { "＋ Ban công", "Bấm vào dầm biên để thêm ban công công xôn (ô **Vươn (m)**, **Lan can (kN/m)**).", "{s:mo-hinh.ban-cong}" },
                new[] { "＋ Cầu thang", "Bấm vào ô để biến thành ô cầu thang; **Khai báo cầu thang…** để khai báo chi tiết.", "{s:mo-hinh.cau-thang}" },
                new[] { "＋ Giếng trời / Sàn vệ sinh", "Bấm vào ô để thành giếng trời, thông tầng / sàn vệ sinh.", "{s:mo-hinh.gieng-troi}" },
                new[] { "＋ Dầm / ＋ Dầm cong", "Vẽ dầm bằng 2 điểm / dầm cong bằng 3 điểm.", "{s:mo-hinh.dam}" },
                new[] { "＋ Tường / Gán tường", "Vẽ tường 2 điểm / bấm dầm để đặt tường suốt chiều dài dầm.", "{s:mo-hinh.tuong}" },
                new[] { "Gán cấu tạo", "Bấm ô sàn để gán cấu tạo sàn đang chọn ở ô **Cấu tạo sàn**.", "{s:mo-hinh.san}" },
                new[] { "Xoá", "Bấm vào tường, dầm, cột (hỏi lại), ô sàn hoặc lỗ sàn để xoá.", "{s:mo-hinh.chinh-sua}" },
                new[] { "↶ Hoàn tác", "Hoàn tác thao tác sửa mặt bằng (Ctrl+Z).", "{s:giao-dien.hoan-tac}" },
                new[] { "Hàng \"Dầm (kiểu ETABS)\"", "Khai báo tiết diện…, Vẽ nhanh theo lưới, Dầm phụ, Công xôn, Dầm theo toạ độ / trục…, Chọn nhiều, Gán tiết diện, Điểm chèn…, Sao chép / Đối xứng…, Phạm vi tầng, Bắt điểm, Gõ toạ độ.", "{s:mo-hinh.dam-etabs}" });

        c.Sec("giao-dien.trang-thai", "Thanh trạng thái và ô thông tin")
            .Bullets("Thanh trạng thái báo kết quả lệnh vừa chạy (số nút, thanh, ô sàn, tổ hợp…), gợi ý của chế độ đang bật và cảnh báo không chặn (vd. dự án trích dẫn văn bản đã hết hiệu lực).",
                "Khi chương trình đang tính, thanh tiến trình chạy và các lệnh khác bị khoá; đóng cửa sổ lúc này sẽ được nhắc chờ.",
                "Ô thông tin dưới khung nhìn hiện thuộc tính và kết quả thiết kế của cấu kiện đang chọn; ô bên phải vẽ biểu đồ nội lực của thanh theo ô **Biểu đồ M3 theo**.");

        c.Sec("giao-dien.hoan-tac", "Hoàn tác, lưu và phím tắt")
            .Bullets("**Ctrl+Z** hoàn tác các thao tác sửa mô hình (mặt bằng, công cụ dầm, tiết diện, cầu thang, móng, chọn thép…), tối đa 30 bước.",
                "**Delete** xoá đối tượng đang chọn trên mặt bằng; **Esc** dừng công cụ đang vẽ và quay về **Chọn**.",
                "**Ctrl+N / Ctrl+O / Ctrl+S**: mới / mở / lưu; **F5**: chạy phân tích; **F1**: hướng dẫn theo ngữ cảnh; **Shift+F1**: phụ lục tính toán.",
                "Sửa mô hình sau khi đã phân tích sẽ xoá kết quả cũ (thanh trạng thái báo \"Mô hình đã thay đổi – cần chạy lại phân tích\").")
            .P("Bảng đầy đủ: {s:phu-luc.phim-tat}.");

        c.Sec("giao-dien.tro-giup", "Trợ giúp ngay trên phần mềm")
            .Bullets(
                "**F1** ở bất kỳ đâu mở cửa sổ hướng dẫn tại đúng mục của ngữ cảnh: tab trái đang mở, khung nhìn đang dùng, công cụ sửa mặt bằng đang bật (＋ Dầm, Dầm phụ, Công xôn…) hoặc hộp thoại đang mở.",
                "Nút **?** (Hướng dẫn) ở đầu các hộp thoại chính mở mục hướng dẫn của hộp thoại đó.",
                "{m:guide}: cây chương bên trái, nội dung bên phải, ô tìm kiếm không dấu (vd. \"dam phu\", \"coc\", \"smartscreen\"), nút **Xuất Word…**.",
                "{m:quickStart}: bảng các bước làm việc, trạng thái từng bước tự nhận biết từ dự án, nút **Làm bước này** và **Xem hướng dẫn** ({s:quy-trinh.bat-dau-nhanh}).",
                "{m:appendix} (Shift+F1): công thức, hệ số, hệ số an toàn và giải thích của mọi phép tính mà chương trình dùng ({s:ho-so.phu-luc}).");

        c.Sec("giao-dien.cay-mo-hinh", "Cây mô hình, menu Xem / Sửa, thanh trạng thái")
            .Bullets("{m:explorer} (**Ctrl+E**, nút **Cây mô hình**): khung neo bên trái (như Model Explorer của ETABS) gồm **Thông tin dự án**, **Lưới trục**, **Tầng**, **Vật liệu**, **Tiết diện**, **Tải trọng** (trường hợp tải, trường hợp phân tích, tổ hợp), **Đối tượng** (cột, dầm, sàn, tường, móng theo tầng – bấm để chọn và phóng tới), **Kết quả** (chu kỳ dao động, phản lực đáy) khi đã phân tích.",
                "Bấm đúp một nút nhánh của cây để mở hộp thoại khai báo tương ứng. Chọn một trường hợp tải trên cây để lọc {m:showLoads}.",
                "Menu **Xem**: {m:viewPlan}, {m:view3d}, {m:viewFnd}, {m:fit}, {m:labels}. Menu **Sửa**: {m:undo}, {m:deleteSel}, {m:replicate}, {m:split}, {m:mergeNodes} (gộp nút cách nhau < 5 mm).",
                "Thanh trạng thái bên phải: hệ đơn vị (kN, m, °C – tiết diện hiển thị mm, ứng suất MPa), tầng đang xem, toạ độ con trỏ trên mặt bằng (m), hệ trục tổng thể (X sang phải, Y lên trên, Z hướng lên).")
            .P("Kiểm tra mô hình: {s:phan-tich.kiem-tra-mo-hinh}; hình biến dạng: {s:phan-tich.bien-dang}.");
        return c;
    }

    private static string[][] MenuRows()
    {
        var refs = new Dictionary<string, string>
        {
            ["new"] = "khoi-tao.luoi-truc", ["open"] = "khoi-tao.luu", ["save"] = "khoi-tao.luu", ["saveAs"] = "khoi-tao.luu",
            ["importDxf"] = "khoi-tao.dxf", ["importArch"] = "khoi-tao.kien-truc", ["importRevit"] = "khoi-tao.revit", ["importCsi"] = "khoi-tao.etabs",
            ["acad"] = "khoi-tao.autocad", ["cadStd"] = "ho-so.chuan-layer", ["drawStyle"] = "ho-so.thiet-lap-ban-ve", ["templates"] = "khoi-tao.mau",
            ["buildGrid"] = "khoi-tao.luoi-truc", ["portal"] = "khoi-tao.tien-che", ["split"] = "mo-hinh.chinh-sua", ["sections"] = "mo-hinh.tiet-dien",
            ["wallTypes"] = "mo-hinh.tuong", ["walls"] = "mo-hinh.tuong", ["buildUps"] = "mo-hinh.san", ["locations"] = "tai-trong.gio",
            ["combos"] = "tai-trong.to-hop", ["run"] = "phan-tich.chay", ["modal"] = "phan-tich.dao-dong", ["importResults"] = "phan-tich.nhap-ket-qua",
            ["crossCheck"] = "phan-tich.so-sanh", ["designRc"] = "thiet-ke.btct", ["checkSteel"] = "thiet-ke.thep", ["autoResize"] = "thiet-ke.tu-tang",
            ["designFnd"] = "mong.loai", ["rebarChoice"] = "thiet-ke.chon-thep", ["rebarPrefs"] = "thiet-ke.tuy-chon-thep",
            ["expDxf"] = "ho-so.ban-ve", ["drawEdit"] = "ho-so.hieu-chinh", ["drawAttach"] = "ho-so.hieu-chinh", ["expWord"] = "ho-so.thuyet-minh",
            ["expExcel"] = "ho-so.bang-tinh", ["quantities"] = "ho-so.khoi-luong", ["autoReport"] = "ho-so.thuyet-minh", ["shop"] = "ho-so.shop",
            ["csv"] = "ho-so.bang-tinh", ["e2k"] = "ho-so.bang-tinh", ["s2k"] = "ho-so.bang-tinh", ["expAll"] = "ho-so.xuat-tat-ca", ["sheets"] = "ho-so.dan-trang",
            ["guide"] = "giao-dien.tro-giup", ["quickStart"] = "quy-trinh.bat-dau-nhanh", ["appendix"] = "ho-so.phu-luc", ["regs"] = "phap-ly.danh-muc",
            ["verify"] = "kiem-chung.bao-cao", ["about"] = "gioi-thieu",
            ["undo"] = "giao-dien.hoan-tac", ["deleteSel"] = "mo-hinh.chinh-sua", ["replicate"] = "mo-hinh.dam-etabs", ["mergeNodes"] = "giao-dien.cay-mo-hinh",
            ["explorer"] = "giao-dien.cay-mo-hinh", ["cadLinks"] = "khoi-tao.lien-ket-autocad", ["viewPlan"] = "giao-dien.khung-nhin", ["view3d"] = "giao-dien.khung-nhin", ["viewFnd"] = "mong.loai",
            ["fit"] = "giao-dien.khung-nhin", ["labels"] = "giao-dien.khung-nhin", ["materials"] = "mo-hinh.vat-lieu", ["loadPatterns"] = "tai-trong.truong-hop",
            ["loadCases"] = "mo-hinh.khai-bao", ["comboEditor"] = "tai-trong.sua-to-hop", ["spectrum"] = "mo-hinh.khai-bao", ["diaphragms"] = "mo-hinh.khai-bao",
            ["drawBeam"] = "mo-hinh.dam", ["drawCurved"] = "mo-hinh.dam-cong", ["drawQuick"] = "mo-hinh.dam-etabs", ["drawSecondary"] = "mo-hinh.dam-etabs",
            ["drawCantilever"] = "mo-hinh.dam-etabs", ["beamByPos"] = "mo-hinh.dam-etabs", ["drawSlab"] = "mo-hinh.san", ["drawBalcony"] = "mo-hinh.ban-cong",
            ["drawStair"] = "mo-hinh.cau-thang", ["drawVoid"] = "mo-hinh.gieng-troi", ["stairDecl"] = "mo-hinh.cau-thang", ["drawWall"] = "mo-hinh.tuong",
            ["selOne"] = "mo-hinh.chinh-sua", ["selMulti"] = "mo-hinh.chon", ["selAll"] = "mo-hinh.chon", ["selBySection"] = "mo-hinh.chon", ["selClear"] = "mo-hinh.chinh-sua",
            ["assignFrameLoad"] = "mo-hinh.gan", ["assignAreaLoad"] = "mo-hinh.gan", ["assignJointLoad"] = "mo-hinh.gan", ["assignRestraint"] = "mo-hinh.gan",
            ["assignRelease"] = "mo-hinh.gan", ["assignSection"] = "mo-hinh.gan", ["assignModifiers"] = "mo-hinh.gan", ["insertion"] = "mo-hinh.dam-etabs",
            ["assignDiaphragm"] = "mo-hinh.gan", ["assignWall"] = "mo-hinh.tuong", ["assignBuildUp"] = "mo-hinh.san", ["assignWc"] = "mo-hinh.gieng-troi",
            ["modelCheck"] = "phan-tich.kiem-tra-mo-hinh", ["deformed"] = "phan-tich.bien-dang", ["diagrams"] = "phan-tich.bieu-do", ["modes"] = "phan-tich.dao-dong",
            ["reactions"] = "phan-tich.bang-ket-qua", ["resultTables"] = "phan-tich.bang-ket-qua", ["showLoads"] = "mo-hinh.gan",
        };
        return Menus.Where(m => !m.Planned && m.Id != "exit").Select(m => new[]
        {
            m.Path[0], string.Join(" → ", m.Path.Skip(1)), refs.TryGetValue(m.Id, out var h) ? "{s:" + h + "}" : ""
        }).ToArray();
    }

    // ==================================================================
    // 4. QUY TRÌNH TỔNG QUÁT + VÍ DỤ
    // ==================================================================
    // ==================================================================
    // HAI CHẾ ĐỘ LÀM VIỆC – TRIỂN KHAI HỒ SƠ (DetailingWindow, Core/Detailing)
    // ==================================================================
    private static GuideSection ChModes()
    {
        var c = Ch("che-do-ho-so", "Hai chế độ làm việc – Triển khai hồ sơ");
        c.P("Khi khởi động, màn hình **chọn chế độ làm việc** hiện 2 lựa chọn (phím 1 / 2):",
            "**1 – Thiết kế – chạy nội lực**: giao diện mô hình tính như ETABS (lưới trục, tầng, tiết diện, tải trọng, phân tích khung 3D, thiết kế theo nội lực) – các chương sau của hướng dẫn.",
            "**2 – Triển khai hồ sơ**: không cần mô hình – chỉ nhập kích thước dầm, sàn, cột, chọn số thanh, Ø, khoảng cách thép; thiết kế móng đơn, móng băng, móng cọc, móng bè theo tải chân cột; "
            + "tính bê tông dự ứng lực, bể nước; xuất bản vẽ, thống kê thép, khối lượng, thuyết minh.")
            .Bullets("Đánh dấu **Luôn mở chế độ đã chọn khi khởi động** để bỏ qua màn hình này; đổi lại: {m:startMode}.",
                "Đang ở chế độ mô hình: {m:detailingMode} (cửa sổ mô hình vẫn mở để lấy nội lực). Ở chế độ triển khai: Tệp → **Chuyển sang chế độ Thiết kế – chạy nội lực**.",
                "Mở file **.kcvxt** (dự án mô hình) → chế độ thiết kế; file **.kcvxth** (hồ sơ triển khai) → chế độ triển khai.");

        c.Sec("che-do-ho-so.giao-dien", "Cửa sổ Triển khai hồ sơ")
            .Bullets("Bên trái: **danh sách cấu kiện** theo nhóm (Dầm, Cột, Sàn, Móng đơn, Móng băng, Móng cọc, Móng bè, Dự ứng lực, Bể nước); ✓ đạt, ✗ đỏ = không đạt. Nút **Nhân bản**, **Xoá**, ▲▼.",
                "Thanh công cụ: **＋ Dầm**, **＋ Cột**, **＋ Sàn**, **＋ Móng đơn**, **＋ Móng băng**, **＋ Móng cọc**, **＋ Móng bè**, **＋ DƯL**, **＋ Bể nước**; **Tính toàn bộ (F5)**, **Xuất tất cả…**.",
                "Giữa: **bảng nhập** theo nhóm (Tiết diện, Thép dọc, Thép đai, Nội lực, Nền…). Ô kích thước ghi đơn vị (mm, m, kN, kN.m); nhập **0** ở ô thép / kích thước = chương trình **tự chọn**. Kết quả tính lại ngay khi sửa.",
                "Phải: tab **Kết quả kiểm tra** (bảng yêu cầu – khả năng – tỷ số, dòng đỏ = không đạt, thông báo LỖI kèm đề xuất), **Thuyết minh** (công thức, thay số, thống kê thép 1 cấu kiện), "
                + "**Bản vẽ (xem trước)** (khung A1, phóng to / thu nhỏ, **Xuất DXF cấu kiện này…**), **Khối lượng hồ sơ**.",
                "Tuỳ chọn → **Vật liệu, lớp bảo vệ…** (cấp bê tông, thép dọc / thép đai, lớp bảo vệ dầm, cột, sàn, móng, bể; bê tông lót; chiều dài cây thép) và **Thông tin công trình…** (in lên khung tên).",
                "Tệp → **Mở hồ sơ mẫu**: đủ 9 loại cấu kiện để tham khảo. Lưu hồ sơ: Ctrl+S (file .kcvxth).");

        c.Sec("che-do-ho-so.dam-cot-san", "Dầm, cột, sàn – nhập kích thước, chọn thép")
            .Bullets("**Dầm**: b × h, **các nhịp tim – tim** (vd. \"4,2; 3,6\" hoặc \"3*4,2\"), bề rộng gối, cao độ đỉnh; thép dưới / trên chạy suốt (≥ 2 thanh), tăng cường dưới mỗi nhịp, mũ gối; đai Ø, bước vùng gối / giữa nhịp, số nhánh. "
                + "Nhập M nhịp, M gối, Q (tuỳ chọn) → kiểm tra M ≤ Mu, Q ≤ Qb + Qsw, dải nén 0,3Rb·b·h0 (TCVN 5574:2018 mục 8.1). Luôn kiểm tra cấu tạo: μ ≥ 0,1 %, bước đai, khe hở thép 1 lớp.",
                "**Cột**: b × h, chiều cao các tầng, cao độ chân, chiều cao dầm tại nút; Ø, số thanh mỗi cạnh, đai đầu / giữa cột. Nhập N, Mx, My tầng dưới → kiểm tra nén lệch tâm xiên (Bresler) có kể độ lệch tâm ngẫu nhiên; "
                + "vị trí nối thép tự đặt ngoài vùng nguy hiểm, so le 2 nhóm ≥ 1,3·ll.",
                "**Sàn**: Lx, Ly, t, số cạnh ngàm; tĩnh tải hoàn thiện, hoạt tải (tiêu chuẩn) → q tính toán, phân tải Marcus, mô men dải 1 m; thép lớp dưới 2 phương, mũ gối (0 = tự chọn Ø6…Ø12, bước 100…200).",
                "**Vị trí dầm**: Dầm tầng / **Dầm móng (giằng móng)** / Dầm mái. Dầm móng: lớp bảo vệ ≥ 35 mm, **bê tông lót** (vươn mỗi bên, mặc định 100 mm) và chọn **Dầm móng sát ranh đất** (trái / phải) – "
                + "phía sát ranh bê tông lót và bê tông dầm không vươn sang đất nhà bên, thành dầm xây gạch làm khuôn (có trong khối lượng).",
                "**Cánh sàn (mặt cắt dầm)**: Chữ T – sàn 2 bên / Chữ L – sàn bên trái hoặc phải / Không có sàn; chiều dày sàn, **hạ cốt sàn từng bên** (vd. −50 sàn vệ sinh), thép sàn thể hiện. "
                + "Mặt cắt dầm vẽ cánh sàn, bậc sàn hạ cốt và thép sàn qua dầm như Kết cấu Soft / XSCad.",
                "**Cột – vị trí**: Chân cột từ móng (thép chờ chôn trong móng, bẻ chân tựa lưới thép đáy) hoặc trên dầm / sàn; **thép chờ nhô khỏi mặt móng (cổ cột) ≥ max(30d; ll)** – để 0 chương trình tự lấy; "
                + "**Đỉnh cột**: Tầng mái – khoá đầu cột **uốn mỏ** vào dầm mái (đoạn uốn ≥ max(12d; lan − (hd − c))) hoặc chờ nối lên tầng trên (nhô ll).",
                "Bản vẽ dùng chung bộ vẽ của chế độ mô hình: mặt đứng dầm – chuỗi vùng đai, nhãn thép, mặt cắt 1-1 / 2-2; mặt đứng cột, mặt cắt; mặt bằng thép sàn + mặt cắt. Số hiệu thép theo quy tắc Kết cấu Soft.");

        c.Sec("che-do-ho-so.mat-bang-san", "Mặt bằng sàn – hạ cốt, nhấn thép, khoét lỗ, thép tự động / thủ công")
            .Bullets("＋ Mặt bằng sàn: khai báo **nhịp trục X, Y** (tim dầm), chiều dày, tải. Bảng **ô sàn khác mặc định**: cột / hàng của ô, **hạ cốt Δ** (vd. −50 vệ sinh, ban công), chiều dày riêng, công năng (Lỗ thông tầng = bỏ ô).",
                "**Cách bố trí**: Tự động theo diện tích – chương trình tính từng ô (cạnh liên tục khi chênh cốt ≤ ngưỡng bậc), chọn thép lớp dưới, thép mũ qua dầm; hoặc **Thủ công** – kỹ sư tự khai báo vùng thép (lớp, phương, Ø, bước, toạ độ), chương trình kiểm tra từng ô còn thiếu thép.",
                "**Nhấn thép**: tại dầm có hai ô chênh cốt |Δ| ≤ ngưỡng nhấn (mặc định 50 mm) thép mũ được **bẻ xiên 1:6** vắt qua dầm; lớn hơn thì cắt thép, neo riêng từng ô vào dầm.",
                "**Lỗ khoét sàn**: toạ độ góc dưới trái, kích thước; chương trình cắt thép qua lỗ, đặt **thép gia cường mép lỗ**, cảnh báo lỗ lớn cần dầm bo.");

        c.Sec("che-do-ho-so.autocad", "Lấy kích thước từ AutoCAD (lệnh VXT_KT)")
            .Steps("Trong AutoCAD đã nạp add-in Việt Const: gõ **VXT_KT** (hoặc nút **Lấy kích thước → Triển khai hồ sơ** trên bảng VXT).",
                "Chọn loại: **Dam** (chọn trục dầm, các hình chữ nhật cột → nhịp; 2 cạnh dầm → b; nhập h), **Cot** (các hình chữ nhật → b × h, tên lấy theo chữ bên trong), **San** (các đường trục → nhịp X, Y; đa tuyến kín bên trong → lỗ khoét), "
                + "**Mong** (hình chữ nhật móng + cột bên trong), **MongBang** (trục băng + cột).",
                "Việt Const (cửa sổ Triển khai hồ sơ) tự nhận trong vài giây và hỏi: **Có** – áp vào cấu kiện đang chọn (giữ thép, nội lực), **Không** – tạo cấu kiện mới; hoặc bấm **⇩ Từ AutoCAD** (Ctrl+K).")
            .Bullets("Đơn vị bản vẽ lấy theo INSUNITS (mm / m); dữ liệu trao đổi qua thư mục %AppData%\\KetCauVXT\\AcadPick.");

        c.Sec("che-do-ho-so.mong", "Móng đơn, móng băng, móng cọc")
            .Bullets("**Móng đơn**: kích thước cột, N, ML, MB tính toán (N tiêu chuẩn = N/1,15 nếu để 0), R, Df. L, B, H = 0 → tự chọn: ptb ≤ R, pmax ≤ 1,2R, không tách đáy; H theo chọc thủng F ≤ Rbt·u·h0 và móng cứng; thép mép cột với áp lực hình thang.",
                "**Móng băng** (dầm móng chữ T ngược dưới dãy cột): chiều dài băng, bề rộng bản (0 = tự chọn), bản mép, sườn; bảng **cột trên móng băng** (vị trí X, N, M). Tính dầm trên nền đàn hồi Winkler (k = 120·R nếu để 0): "
                + "áp lực nền, M+ tại cột (thép dưới), M− giữa nhịp (thép trên), Q (đai); bản cánh tính console.",
                "**Móng cọc**: cột, N, Mx, My; cọc D, [P] nén, [Pk] nhổ, số cọc (0 = tự chọn), khoảng cách (0 = 3D), tim cọc tới mép đài. Kiểm tra Pmax ≤ [P], nhổ, khoảng cách 3D, chọc thủng cột và cọc góc, cắt tiết diện nghiêng, thép mép cột.",
                "**Sát ranh đất** (móng đơn: phương L / B; đài cọc: phương X / Y; móng băng: cánh 1 bên; móng bè: cạnh sát ranh): bê tông móng và bê tông lót **không vươn sang đất nhà bên** (lót chỉ vươn phía trong), "
                + "cột lệch về mép móng → móng lệch tâm: có **dầm móng cân bằng** (chương trình ghi mô men N·e cho dầm) hoặc kể N·e vào áp lực đáy móng / lực cọc; chu vi chọc thủng bị cắt ở cạnh ranh; "
                + "đài cọc giữ tim cọc cách ranh ≥ 500 mm (máy ép). Bản vẽ ghi **RANH ĐẤT**, thành sát ranh xây gạch / tấm khuôn để lại (có trong khối lượng).",
                "Khối lượng kèm theo: bê tông lót, đào đất hố móng, số cọc, đập đầu cọc.");

        c.Sec("che-do-ho-so.mong-be", "Móng bè")
            .P("Khai báo Lx, Ly, chiều dày, Df, R và bảng **cột trên móng bè** (toạ độ X, Y từ góc móng, kích thước, N, Ntc, Mx, My). Phương pháp móng cứng (ACI 336.2R): "
               + "áp lực đáy p = ΣNtc/A + γ·Df ± M·y/I, chia dải theo hàng cột, mô men dải theo cân bằng tĩnh, thép lớp trên / dưới 2 phương (0 = tự chọn), chọc thủng từng cột (cột giữa, biên, góc).");

        c.Sec("che-do-ho-so.du-ung-luc", "Bê tông dự ứng lực (căng sau)")
            .P("Dầm hoặc dải sàn 1 m nhịp đơn giản: tiết diện (chữ nhật / chữ T), tải G2, Q; cáp T12.7 / T15.2, số sợi, độ lệch tâm giữa nhịp và đầu, σcon, ma sát μ, k, tụt neo. "
               + "Chương trình tính tổn hao (chùng ứng suất, ma sát, tụt neo, co ngót, từ biến; tối thiểu 100 MPa), P0, P∞, tải cân bằng, ứng suất lúc buông neo và khai thác, Mu, độ vồng – độ võng (TCVN 5574:2018 mục 9), "
               + "vẽ đường cáp parabol + mặt cắt, khối lượng cáp, neo, ống gen.");

        c.Sec("che-do-ho-so.be-nuoc", "Bể nước BTCT")
            .P("Bể ngầm hoặc bể trên mái: kích thước trong, chiều cao nước, chiều dày thành / đáy / nắp; đất (γ, φ, phụ tải, mực nước ngầm). Tính thành bể theo phương pháp dải (Grashof) với áp lực nước / đất, "
               + "kiểm tra bề rộng vết nứt ≤ 0,2 mm (tự tăng thép nếu cần), đáy, nắp, chống đẩy nổi khi bể rỗng; khối lượng kèm băng cản nước, chống thấm.");

        c.Sec("che-do-ho-so.xuat", "Xuất hồ sơ")
            .Bullets("Xuất hồ sơ → **Bản vẽ DXF – gộp 1 file** (các khung A1 đặt cạnh nhau + bảng thống kê cốt thép) hoặc **tách từng cấu kiện** (KC-01_Dầm_D1.dxf … + KC-xx_ThongKeThep.dxf).",
                "**Bảng khối lượng Excel**: tổng hợp bê tông, bê tông lót, ván khuôn, cốt thép theo nhóm Ø ≤ 10 / 10–18 / > 18, cáp DƯL, công tác khác; trang theo cấu kiện (hàm lượng kg/m³); thống kê cốt thép đầy đủ.",
                "**Thuyết minh tính toán Word**: căn cứ pháp lý – tiêu chuẩn, vật liệu, từng cấu kiện (công thức thay số, bảng kiểm tra, thông báo), tổng hợp khối lượng.",
                "**Xuất tất cả vào thư mục…**: cả bản vẽ gộp, bản vẽ tách, Excel và Word.")
            .Note("Ở chế độ triển khai, nội lực do kỹ sư nhập (từ tính tay, chế độ mô hình hoặc phần mềm khác) – chương trình không kiểm tra nội lực đó có đúng với sơ đồ kết cấu hay không.");
        return c;
    }

    private static GuideSection ChWorkflow()
    {
        var c = Ch("quy-trinh", "Quy trình làm việc tổng quát");
        c.P("Một công trình đi qua các bước dưới đây. Có thể quay lại bước trước bất kỳ lúc nào; sửa mô hình sẽ yêu cầu chạy lại phân tích và thiết kế.");

        c.Sec("quy-trinh.so-do", "Sơ đồ các bước")
            .Table(new[] { "Bước", "Việc làm", "Ở đâu trong chương trình", "Kết quả" },
                new[] { "1. Khởi tạo", "Dự án mới, mẫu định dạng, hoặc nhập từ bản vẽ kiến trúc / AutoCAD / Revit / ETABS", "{m:new}; {m:importArch}; {m:acad}; {m:importRevit}; {m:importCsi}", "Dự án .kcvxt" },
                new[] { "2. Lưới, tầng", "Bước cột X, Y; chiều cao tầng; cao độ chân cột; liên kết chân cột", "Tab Công trình → Tạo mô hình", "Lưới trục, tầng, khung sơ bộ" },
                new[] { "3. Tiết diện, vật liệu", "Tiết diện dầm/cột/sàn, cấp bê tông, nhóm thép", "Tab Tiết diện; {m:sections}; tab Công trình → Vật liệu", "Thư viện tiết diện" },
                new[] { "4. Mô hình", "Cột, dầm, dầm phụ, công xôn, dầm cong, sàn, ban công, cầu thang, giếng trời, tường", "Tab Mặt bằng (thanh công cụ sửa + công cụ dầm kiểu ETABS)", "Mô hình hình học" },
                new[] { "5. Tải trọng, tổ hợp", "Tải sàn theo công năng, cấu tạo sàn, tải tường, tải nhập tay; tổ hợp", "Tab Tải trọng; {m:combos}", "Trường hợp tải, tổ hợp" },
                new[] { "6. Gió, động đất", "Địa danh → vùng gió, agR; dạng địa hình; loại nền; hệ số q; dao động riêng", "Tab Công trình; {m:modal}", "GX, GY, DDX, DDY" },
                new[] { "7. Địa chất, móng", "Hố khoan, chỉ tiêu nền, loại móng, cọc, ranh giới đất, cừ tràm", "Tab Địa chất; tab Mô hình móng; {m:designFnd}", "Phương án móng" },
                new[] { "8. Phân tích", "Tĩnh, dao động riêng, phổ phản ứng", "{m:run} (F5)", "Nội lực, phản lực, chuyển vị" },
                new[] { "9. Kiểm tra kết quả", "Biểu đồ, phản lực, cảnh báo, so sánh ETABS/SAP", "Tab Kết quả; Biểu đồ nội lực; {m:crossCheck}", "Kết quả đã kiểm tra" },
                new[] { "10. Thiết kế", "Thép dầm, cột, sàn, móng, cầu thang; chọn bố trí thép; kiểm tra thép", "{m:designRc}; {m:designFnd}; {m:rebarChoice}; {m:checkSteel}", "Cốt thép, kết luận đạt/không đạt" },
                new[] { "11. Triển khai hồ sơ", "Bản vẽ DXF, dàn trang, shop drawing, khối lượng, thuyết minh", "Menu Triển khai hồ sơ; {m:expAll}", "Bộ hồ sơ" },
                new[] { "12. Kiểm chứng", "Báo cáo kiểm chứng, đối chiếu ETABS/SAP, kỹ sư kiểm tra", "{m:verify}; {m:crossCheck}", "Hồ sơ đủ cơ sở" })
            .Tip("Bảng {m:quickStart} hiện các bước này kèm trạng thái của dự án đang mở.");

        c.Sec("quy-trinh.bat-dau-nhanh", "Bảng Bắt đầu nhanh")
            .P("Bảng **Bắt đầu nhanh** tự hiện khi mở chương trình lần đầu và khi tạo dự án mới (tích **Không hiện lại** để tắt; mở lại bằng {m:quickStart}). Mỗi dòng là một bước của quy trình:")
            .Bullets("**Trạng thái** tự nhận biết từ dự án: ✓ đã làm (vd. đã có lưới trục, đã có cột – dầm – sàn, đã có tường, đã có kết quả phân tích, đã thiết kế), ○ chưa làm, ? cần bạn tự kiểm tra (vd. kiểm tra kết quả).",
                "**Làm bước này**: chuyển đến tab, khung nhìn hoặc công cụ tương ứng (vd. tab Công trình, công cụ ＋ Tường, chạy phân tích).",
                "**Xem hướng dẫn**: mở mục hướng dẫn của bước đó.",
                "Bảng không chặn cửa sổ chính; trạng thái được cập nhật mỗi khi bạn quay lại bảng (hoặc bấm **Làm mới**).");

        c.Sec("quy-trinh.luu-y", "Nguyên tắc chung")
            .Bullets("Lưu dự án thường xuyên (Ctrl+S). File .kcvxt là JSON, có thể sao lưu, so sánh phiên bản.",
                "\"Tạo mô hình\" ở tab Công trình **xoá mô hình hiện có** và sinh lại; \"Áp dụng thông số\" chỉ cập nhật thông tin, vật liệu, gió, động đất, nền đất.",
                "Trước khi thiết kế, xem tab Kết quả → **Cảnh báo / thông tin** và tab **Biểu đồ nội lực** để chắc mô hình làm việc đúng.",
                "Các lệnh xuất tự chạy phân tích và thiết kế nếu kết quả chưa có hoặc đã cũ.");

        Example(c.Sec("quy-trinh.vi-du", "Ví dụ: nhà phố 4 tầng 5×13,5 m"));
        return c;
    }

    private static void Example(GuideSection ex)
    {
        ex.P("Ví dụ dùng đúng số liệu của dự án mẫu **NhaPho.kcvxt** (thư mục Dự án mẫu khi cài đặt; bộ kiểm thử tự lập lại và xuất toàn bộ hồ sơ của dự án này): "
             + "nhà phố tại Thái Nguyên, mặt tiền 5 m, sâu 13,5 m (3 nhịp 4,5 m), 4 tầng cao 3,9 + 3,3 + 3,3 + 3,3 m, chân cột tại cao độ −0,6 m.");

        ex.Sec("quy-trinh.vi-du.khoi-tao", "Bước 1 – Khởi tạo lưới trục và tầng")
            .Steps("{m:new} (Ctrl+N). Ở tab **Công trình**, nhóm **Thông tin công trình**: Tên công trình \"Nhà phố mẫu 4 tầng\", Địa điểm xây dựng \"Thái Nguyên\", Chủ đầu tư, Người thiết kế.",
                "Nhóm **Lưới trục và tầng (m)**: Bước cột theo X = **5**; Bước cột theo Y = **4.5 4.5 4.5** (hoặc 3*4.5); Chiều cao các tầng = **3.9 3.3 3.3 3.3**; Cao độ chân cột = **-0.6**; giữ **Ngàm** cho liên kết chân cột.",
                "Nhóm **Tiết diện mặc định**: Cột **C22x30**, Dầm phương X **D22x30**, Dầm phương Y **D22x40**, Sàn **S100**.",
                "Nhóm **Tải trọng (giá trị tiêu chuẩn)**: Hoàn thiện sàn 1.2; Hoạt tải sàn 1.5; Hoạt tải mái 0.75; Tường biên trên dầm **7.5** kN/m.",
                "Nhóm **Địa điểm xây dựng**: Tỉnh/Thành phố gõ \"Thái Nguyên\" → chọn xã/phường → **Vùng áp lực gió** tự điền, W0 điền vào nhóm Gió. Kiểm tra lại với QCVN 02:2022 cho địa danh cụ thể.",
                "Bấm **Tạo mô hình** và xác nhận. Thanh trạng thái báo số nút, thanh, ô sàn và tổ hợp đã tạo. Lưu dự án ({m:saveAs}).")
            .Note("Mô hình gồm 2 trục X (1, 2) × 4 trục Y (A…D), 4 tầng: cột tại mọi giao trục, dầm trên mọi đường trục, sàn trong mọi ô; tải hoàn thiện, hoạt tải sàn dồn 45° về dầm; tải tường biên 7,5 kN/m trên dầm biên.");

        ex.Sec("quy-trinh.vi-du.mo-hinh", "Bước 2 – Hoàn thiện mô hình trên mặt bằng")
            .Steps("Chọn tab **Mặt bằng**, ô **Tầng đang xem** = tầng 2. Kiểm tra lưới, tên cột, dầm (bật **Tên cấu kiện**).",
                "Nếu ô 4,5 × 5 m cần dầm phụ đỡ tường: bật **Dầm phụ**, bấm vào ô, chọn \"Song song trục X\", **Chia đều – số dầm n** = 1, tiết diện D22x30, giữ **Chia lại ô sàn theo dầm phụ** → Đồng ý. Đặt **Phạm vi tầng** = \"Tất cả tầng\" nếu muốn áp cho mọi tầng.",
                "Ô cầu thang: bật **＋ Cầu thang**, bấm vào ô giữa nhà; chọn ô vừa tạo rồi bấm **Khai báo cầu thang…** để khai báo dạng thang, số bậc, bản thang, dầm chiếu nghỉ.",
                "Phòng vệ sinh: bật **Sàn vệ sinh**, bấm vào ô sàn WC (hoàn thiện 2,0 kN/m²).",
                "Ban công tầng 2–4: bật **＋ Ban công**, nhập **Vươn (m)** = 1.2, **Lan can (kN/m)** = 1.0, bấm vào dầm biên mặt tiền.",
                "Tường trong nhà: chọn **Loại tường** (vd. tường 110), bật **＋ Tường** hoặc **Gán tường** rồi bấm dầm. Ctrl+Z để hoàn tác khi bấm nhầm.");

        ex.Sec("quy-trinh.vi-du.tai-trong", "Bước 3 – Tải trọng và tổ hợp")
            .Steps("Tab **Tải trọng**: kiểm tra trường hợp tải TT (trọng lượng bản thân, γf 1,1), HTHT (hoàn thiện, 1,3), HT (hoạt tải, 1,3), GX, GY (gió, 1,2).",
                "Bật **Hiện tải tự sinh (sàn)** để xem tải sàn đã dồn lên dầm; thêm tải tập trung / phân bố riêng nếu cần (**+ Cho thanh đang chọn**).",
                "Tab Công trình → nhóm **Gió**: bật **Tính tải gió**, dạng địa hình B (hoặc theo thực tế), Cp. Nhóm **Động đất**: nếu agR đáng kể, bật **Tính động đất**, nhập agR, loại nền, q → **Áp dụng thông số**.",
                "{m:combos}: chương trình sinh tổ hợp TTGH I (TH1, TH2…), TTGH II (TC1…) và tổ hợp bao **BAO**.");

        ex.Sec("quy-trinh.vi-du.mong", "Bước 4 – Địa chất và móng")
            .Steps("Có hồ sơ khảo sát: tab **Địa chất** → **+ Hố khoan**, nhập toạ độ, mực nước ngầm, các lớp đất (hoặc **Chọn theo đất tham khảo…**), tích **Dùng hố khoan cho thiết kế móng**. Chưa có: dùng nhóm **Nền đất dưới móng đơn** ở tab Công trình (φ, c, γ, Df).",
                "Tab **Mô hình móng**: **Loại móng** = Móng đơn (hoặc Móng cọc / Móng băng); **Thông số…** để khai báo cọc, móng băng.",
                "Nhà phố thường sát ranh: **Ranh giới đất…** → khai báo cạnh trái/phải; cột sát ranh sẽ dùng móng lệch tâm có dầm móng cân bằng.");

        ex.Sec("quy-trinh.vi-du.phan-tich", "Bước 5 – Phân tích và kiểm tra kết quả")
            .Steps("Nhấn **F5** ({m:run}). Thanh trạng thái báo số phương trình, trường hợp tải, cảnh báo.",
                "Tab **Kết quả**: chọn trường hợp TT, xem **Phản lực chân cột**; tổng phản lực đứng phải bằng tổng tải đứng (bộ kiểm thử của chương trình kiểm tra đúng điều này cho dự án mẫu).",
                "Tab **Biểu đồ nội lực**: Đại lượng M3, khung nhìn \"Mặt đứng theo trục\", trục A → xem biểu đồ mô men khung; đổi sang BAO để xem đường max/min.",
                "Tuỳ chọn: {m:e2k}, chạy trong ETABS, xuất bảng kết quả rồi {m:crossCheck} để đối chiếu.");

        ex.Sec("quy-trinh.vi-du.thiet-ke", "Bước 6 – Thiết kế")
            .Steps("{m:designRc}: tab **Thiết kế** hiện bảng Dầm, Cột, Sàn, Móng, Cầu thang; dòng tô đỏ là cấu kiện không đạt.",
                "Cấu kiện không đạt: tăng tiết diện ({m:sections}) hoặc {m:autoResize} rồi thiết kế lại.",
                "Chọn một dầm trên mặt bằng → **Chọn bố trí thép…** để chọn phương án thép dưới, mũ gối, chạy suốt, đai theo ý muốn.",
                "{m:designFnd}: xem kích thước móng, thép đáy, kiểm tra áp lực đáy, chọc thủng, lún ở tab Mô hình móng → **Kiểm tra an toàn…**.");

        ex.Sec("quy-trinh.vi-du.ho-so", "Bước 7 – Triển khai hồ sơ")
            .Steps("{m:drawStyle} và {m:cadStd}: chọn tỷ lệ, cỡ chữ, kiểu kích thước, tên layer theo chuẩn của đơn vị (một lần, dùng cho mọi dự án).",
                "{m:expAll}: chọn thư mục → chọn Gộp 1 file / Tách rời → xem/đổi tên layer → chương trình tạo thư mục <tên công trình>_<ngày giờ> gồm MoHinh.kcvxt, BanVe (KC-01…KC-08), GiaCongThep, ThuyetMinh.docx, BangTinh.xlsx, bảng khối lượng, NoiLuc (CSV), MoHinh.e2k, MoHinh.s2k.",
                "{m:sheets}: **Tạo bộ bản vẽ mặc định**, chỉnh khung tên, bố cục, in hoặc **Xuất PDF…**.",
                "{m:autoReport}: chọn chương, nhập trang bìa → thuyết minh có mục lục, số trang; mở bằng Word và chọn Yes để cập nhật mục lục.",
                "Kỹ sư kiểm tra lại toàn bộ số liệu, ký hồ sơ.")
            .Tip("Các file kết quả mẫu của ví dụ này (DXF, Word, Excel, .e2k, .s2k) được bộ kiểm thử tạo trong thư mục KetQuaMau/NhaPho mỗi lần build – dùng để so sánh khi học chương trình.");
    }

    // ==================================================================
    // 5. KHỞI TẠO DỰ ÁN
    // ==================================================================
    private static GuideSection ChStart()
    {
        var c = Ch("khoi-tao", "Khởi tạo dự án");
        c.P("Có sáu cách bắt đầu một dự án; chọn cách phù hợp với dữ liệu đang có. Ngoài ra có thể gắn bản vẽ AutoCAD làm nền để vẽ theo ({s:khoi-tao.lien-ket-autocad}).");

        c.Sec("khoi-tao.luoi-truc", "Dự án mới từ lưới trục (tab Công trình)")
            .Steps("{m:new} (Ctrl+N) – mẫu định dạng mặc định (nếu đã đặt) được áp dụng tự động.",
                "Nhập **Thông tin công trình** (tên, địa điểm, chủ đầu tư, người thiết kế – dùng cho khung tên, trang bìa thuyết minh).",
                "Nhập **Bước cột theo X (trục 1, 2, 3...)** và **Bước cột theo Y (trục A, B, C...)**: các khoảng cách cách nhau dấu cách, viết gọn được \"3*3.3\" = 3.3 3.3 3.3.",
                "**Chiều cao các tầng (dưới → trên)**, **Cao độ chân cột (m)** (thường âm, vd. −0,6 … −1,5), **Liên kết chân cột** (Ngàm / bỏ chọn = khớp).",
                "Chọn **Tiết diện mặc định** (cột, dầm phương X, dầm phương Y, sàn), **Tải trọng**, **Gió**, **Động đất**, **Vật liệu**, **Nền đất dưới móng đơn**.",
                "Bấm **Tạo mô hình** ({m:buildGrid} cùng tác dụng). Sau này chỉ đổi thông số (không xoá mô hình) thì bấm **Áp dụng thông số**.")
            .Warn("**Tạo mô hình** xoá mô hình hiện có và sinh lại lưới trục, tầng, cột, dầm, sàn và tải. Các sửa đổi trên mặt bằng sẽ mất – lưu dự án trước.");

        c.Sec("khoi-tao.mau", "Mẫu định dạng (template)")
            .P("{m:templates} mở thư viện mẫu của người dùng (%AppData%\\KetCauVXT\\Templates). Mẫu **không chứa mô hình** mà chứa các thiết lập dùng lại giữa các dự án: "
               + "chuẩn CAD (layer, font, kiểu bản vẽ), khung tên người dùng, tờ mẫu, thông tin khung tên (đơn vị thiết kế, người ký, logo, tiền tố số hiệu), tuỳ chọn thuyết minh, định mức khối lượng, tuỳ chọn thép.")
            .Bullets("**Tạo từ dự án hiện tại…**: chọn các phần đưa vào mẫu, đặt tên → lưu thành file *.kcvxt-mau.",
                "**Áp dụng vào dự án…**: Gộp (giá trị của mẫu ghi đè, giữ phần mẫu không có) hoặc Thay thế (phần có trong mẫu thay toàn bộ phần tương ứng).",
                "**Đặt làm mặc định**: mẫu được áp dụng tự động cho mọi dự án mới; **Bỏ mặc định** để thôi.",
                "**Nhập file… / Xuất file…** để gửi mẫu cho đồng nghiệp; **Đổi tên…**, **Mở thư mục**.");

        c.Sec("khoi-tao.tien-che", "Khung thép tiền chế")
            .Steps("{m:portal} (hoặc nút **Khung tiền chế**).",
                "Nhập Nhịp khung L, Chiều cao đỉnh cột H, Độ dốc mái i (%), Số bước khung, Bước khung B, Khoảng cách giằng / xà gồ, liên kết chân cột (Khớp / ngàm).",
                "Nhập Tĩnh tải mái (tôn + xà gồ), Hoạt tải mái, hệ số khí động Ce cột và mái (đón gió / khuất gió).",
                "Tiết diện cột, kèo chữ I: h1 (đầu I), h2 (đầu J – vát), bf, tf, tw và mác thép → **Tạo khung**. Chương trình hỏi có xoá mô hình hiện có không (No = thêm vào mô hình).",
                "Chạy phân tích (F5) rồi {m:checkSteel}.")
            .Note("Khung phẳng trong mặt phẳng XZ, đặt tại các trục Y = 0, B, 2B… Gió ngang nhà gán trực tiếp lên cột và kèo theo W0, địa hình ở tab Công trình.");

        c.Sec("khoi-tao.kien-truc", "Từ bản vẽ kiến trúc DWG/DXF (triển khai kết cấu tự động)")
            .P("{m:importArch} hoặc nút **Từ bản vẽ KT**: đọc bản vẽ kiến trúc sơ bộ, nhận diện các mặt bằng tầng, đề xuất hệ kết cấu và tải trọng, cho xem trước, sửa rồi tạo mô hình.")
            .Steps("**Chọn file DXF/DWG...** (chọn được nhiều file; một file có thể chứa nhiều mặt bằng tầng đặt cạnh nhau). **Đơn vị bản vẽ**: Theo file / mm / cm / m.",
                "Bấm **Nhận diện**: chương trình tách từng mặt bằng, đọc tên hình (\"MẶT BẰNG TẦNG 1/2/…/TUM/MÁI\" – đọc được Unicode, mã \\U+, font TCVN3), nhận trục và tên trục (kể cả block có thuộc tính), tường (2 nét song song dày 80–450 mm, khe ≤ 1,6 m là cửa), cột kiến trúc, cửa (block), phòng theo chữ ghi, cầu thang, ban công.",
                "Bảng **Các mặt bằng tầng**: sửa cột Dùng, Tầng, Tum, Mái, H (m) rồi bấm **Đề xuất kết cấu**.",
                "**Thông số triển khai**: nhịp dầm lớn nhất, cạnh ngắn ô sàn lớn nhất, công xôn lớn nhất, bề rộng dầm, cạnh cột nhỏ nhất, trọng lượng riêng khối xây, cao trình chân cột, chiều cao tầng, cấp bê tông, **Đặt dầm phụ đỡ tường không nằm trên trục**, **Chân cột ngàm**.",
                "**Từ khoá tên lớp (layer)** và bảng **Tải theo công năng phòng** (TT hoàn thiện, hoạt tải, không sàn, từ khoá) sửa được; tab **Layer trong file** cho phép gán từng layer vào nhóm.",
                "Xem trước trên **Mặt bằng** từng cao trình: dầm chính xanh dương, dầm phụ xanh ngọc, công xôn cam, dầm biên tím; bấm vào dầm/cột/ô sàn để bỏ hoặc chọn lại (nét đứt xám = đã bỏ). Bảng **Cột** sửa được vị trí, kích thước, tầng lên đến.",
                "Bấm **Tạo mô hình**. Chương trình hỏi có chạy **Tự tăng tiết diện đến khi đạt** không: đồng ý thì lặp phân tích → thiết kế → tăng 50 mm cho dầm/cột không đạt, 10 mm cho sàn.")
            .Tip("Để nhận diện tốt: tường trên lớp chứa TUONG/WALL, trục trên lớp TRUC/GRID (hoặc kiểu nét CENTER), cầu thang trên lớp chứa THANG, cửa trên lớp CUA/DOOR; ghi tên phòng bằng chữ trong phòng; ghi \"MẶT BẰNG TẦNG …\" ngay dưới hoặc trên mỗi mặt bằng. Quy tắc nhận diện layer chỉnh ở {m:cadStd} → tab **Nhận diện layer kiến trúc**.")
            .Note("Giới hạn: chưa hỗ trợ tường xiên/cong, tầng hầm, tầng lửng (nhập số tầng thủ công), bản vẽ PDF/ảnh. Bản vẽ kiến trúc mẫu để thử: DuAnMau\\KienTrucMau_DauVao.dxf.");

        c.Sec("khoi-tao.dxf", "Nhập mặt bằng kết cấu DWG/DXF")
            .P("{m:importDxf} (nút **Nhập DWG/DXF**) đọc một mặt bằng kết cấu đã vẽ sẵn: trục (layer TRUC), cột (COT), dầm (DAM), sàn (SAN) và dựng mô hình theo các tầng đã khai báo. Kết thúc, hộp thông báo liệt kê số cột, dầm, ô sàn đã nhập và cảnh báo (nếu có).")
            .P("File DWG được đọc trực tiếp (AutoCAD R14 … 2018+). Nếu đọc lỗi, chương trình thử chuyển đổi bằng ODA File Converter (miễn phí) nếu máy có cài; nếu vẫn không được, trong AutoCAD dùng SAVEAS → DXF.");

        c.Sec("khoi-tao.autocad", "Từ AutoCAD (add-in, lệnh VXT_*)")
            .P("Add-in cho phép tích chọn trực tiếp trên bản vẽ kiến trúc các nhóm đối tượng rồi gửi sang chương trình (cài đặt: {s:cai-dat.autocad}).")
            .Table(new[] { "Lệnh", "Việc làm" },
                new[] { "VXT (VXT_MENU)", "Bảng lệnh: gán nhóm, khai báo tầng, xem, xoá, chuẩn layer, gửi dữ liệu." },
                new[] { "VXT_TUONG, VXT_COT, VXT_DAM, VXT_SAN, VXT_TRUC, VXT_CUA, VXT_THANG, VXT_TENPHONG, VXT_BOQUA", "Gán nhóm: [DoiTuong] quét chọn (lưu XData trong DWG) hoặc [Layer] – mọi đối tượng trên layer đó thuộc nhóm." },
                new[] { "VXT_TANG", "Khai báo tầng [Them/Danhsach/Xoa]: tên, cao độ mặt sàn, chiều cao tầng, điểm gốc (cùng một giao trục ở mọi tầng), vùng mặt bằng (2 góc)." },
                new[] { "VXT_XEM / VXT_XOA", "Xem số đối tượng theo nhóm, tô sáng / bỏ gán nhóm." },
                new[] { "VXT_LAYER", "Tạo layer kết cấu KC-…, chuyển đối tượng đã gán sang layer kết cấu, tạo kiểu chữ VXT_ARIAL / VXT_TIMES." },
                new[] { "VXT_GUI", "Ghi %AppData%\\KetCauVXT\\Link\\acad_<ngày>_<giờ>.json và báo chương trình (đang mở thì hỏi \"Có dữ liệu mới từ AutoCAD… Mở hộp thoại nhập?\"; chưa mở thì tự chạy)." })
            .P("Trong chương trình: {m:acad} chọn file acad_*.json; hộp thoại **Nhập bản vẽ kiến trúc** mở với các tầng đã khai báo và phương án kết cấu đề xuất – tiếp tục như {s:khoi-tao.kien-truc}.")
            .Tip("Tường vẽ 2 nét song song (tường 1 nét lấy dày 110 mm); cột là đa tuyến kín, miền tô hoặc 4 nét rời 0,12–1,0 m; tên trục ≤ 4 ký tự gần đầu trục; cửa giữ dạng block. Elip, spline chưa hỗ trợ (bị bỏ qua, có báo số lượng).");

        c.Sec("khoi-tao.lien-ket-autocad", "Liên kết bản vẽ AutoCAD (DWG/DXF) làm nền mặt bằng")
            .P("Như Xref / \"Import DXF as background\" của ETABS: bản vẽ kiến trúc được gắn vào dự án làm **nền mờ** cho mặt bằng từng tầng, bắt điểm vào nét bản vẽ, tự cập nhật khi bản vẽ được lưu trong AutoCAD.")
            .Steps("{m:cadLinks}: cửa sổ **Bản vẽ liên kết (AutoCAD DWG/DXF)** (không chặn) → **Thêm bản vẽ…**.",
                "Thuộc tính từng bản vẽ: tầng, điểm gốc, điểm chèn, góc xoay, **Tỷ lệ ×**, đơn vị, độ đậm, màu, chữ, bắt điểm, tự cập nhật. **Căn theo 2 điểm** để khớp bản vẽ với lưới trục; **Đặt lại vị trí**; **Phóng tới bản vẽ**, **Vừa mô hình + bản vẽ**.",
                "Bảng layer của bản vẽ: ẩn / hiện (**Ẩn layer đã chọn**, **Chỉ hiện layer đã chọn**, **Hiện tất cả**) và gán nhóm cấu kiện.",
                "Bật **Bắt điểm bản vẽ link (đầu nét, trung điểm, giao nét, tâm)** – dùng cho ＋ Dầm, công cụ dầm kiểu ETABS, tường.",
                "Sửa bản vẽ trong AutoCAD (**Mở bằng AutoCAD**) và lưu: chương trình tự đọc lại nền, báo \"Bản vẽ link đã thay đổi – đã cập nhật\" kèm nút **Cập nhật mô hình từ link…**.",
                "**Cập nhật mô hình từ link…**: danh sách khác biệt bản vẽ ↔ mô hình của tầng (thêm / dời / không còn trên bản vẽ) – xanh = thêm, cam = dời, đỏ = xoá; mục xoá và dời cột mặc định không đánh dấu; **Chọn mọi mục thêm**, **Chọn tất cả**, **Bỏ chọn tất cả** → **Áp dụng** (hoàn tác được).")
            .Bullets("Đường dẫn bản vẽ lưu tương đối theo file dự án; không thấy file khi mở dự án → thông báo chọn lại (**Chọn lại file…**). **Đọc lại**, **Mở thư mục**, **Bỏ liên kết**.",
                "Add-in AutoCAD gửi dữ liệu có đường dẫn DWG → chương trình đề nghị tạo / làm mới liên kết cho bản vẽ đó.");

        c.Sec("khoi-tao.revit", "Từ Revit (JSON)")
            .Steps("Trong Revit: Add-Ins → External Tools → **Xuất sang Việt Const**, chọn nơi lưu file *.vxtrevit.json. Dữ liệu xuất: Levels, Grids (đường thẳng), Structural Columns, Structural Framing, Floors.",
                "Trong Việt Const: {m:importRevit} (nút **Nhập Revit**), chọn file JSON.",
                "Xác nhận thay thế mô hình hiện tại (giữ lại vật liệu, tiết diện, trường hợp tải, thông số gió/động đất). Gốc toạ độ tự dời về giao trục đầu tiên.");

        c.Sec("khoi-tao.etabs", "Từ ETABS / SAP2000 (.e2k / .s2k)")
            .P("{m:importCsi} đọc file mô hình văn bản ETABS (.e2k, .$et) hoặc SAP2000 (.s2k, .$2k): nút, thanh, tiết diện, vật liệu, tầng, tải trọng. Sau khi nhập nên kiểm tra tiết diện, tầng và tải trên mặt bằng, chạy phân tích và so sánh với kết quả ETABS ({s:phan-tich.so-sanh}).");

        c.Sec("khoi-tao.luu", "Mở, lưu dự án, dự án mẫu")
            .Bullets("{m:open} (Ctrl+O), {m:save} (Ctrl+S), {m:saveAs}. File dự án **.kcvxt** là JSON (UTF-8) – có thể sinh bằng công cụ khác (Python, Dynamo, Grasshopper) rồi mở bằng chương trình.",
                "Khi đã liên kết file, bấm đúp file .kcvxt trong Explorer để mở; dòng lệnh: KetCauVXT.exe \"đường dẫn\\file.kcvxt\".",
                "Mở dự án trích dẫn văn bản đã hết hiệu lực: thanh trạng thái cảnh báo (không chặn) – xem {s:phap-ly.cap-nhat}.",
                "Dự án mẫu: menu Start → Việt Const → Dự án mẫu (NhaPho.kcvxt, TuBanVeKienTruc.kcvxt).");
        return c;
    }

    // ==================================================================
    // 6. MÔ HÌNH (+ địa chất, móng)
    // ==================================================================
    private static GuideSection ChModel()
    {
        var c = Ch("mo-hinh", "Lập mô hình kết cấu");

        c.Sec("mo-hinh.luoi-tang", "Lưới trục và tầng")
            .P("Lưới trục và tầng được tạo ở tab Công trình ({s:khoi-tao.luoi-truc}) hoặc từ bản vẽ/Revit/ETABS. Ô **Tầng đang xem** (trên khung nhìn) chọn tầng hiển thị và tầng áp dụng của các công cụ sửa.")
            .Bullets("Muốn đổi bước cột / số tầng của mô hình đơn giản: sửa ở tab Công trình rồi **Tạo mô hình** lại (mô hình bị sinh lại).",
                "Với mô hình đã sửa nhiều, thêm dầm theo trục + khoảng cách bằng **Dầm theo toạ độ / trục…** thay vì sinh lại ({s:mo-hinh.dam-etabs}).",
                "Bắt điểm **Giao trục**, **Trên trục** dùng lưới trục hiện có.");

        c.Sec("mo-hinh.vat-lieu", "Vật liệu")
            .Bullets("Tab Công trình → nhóm **Vật liệu**: cấp bê tông cột, dầm, sàn, móng; thép dọc (Ø ≥ 10) và thép đai, thép sàn (Ø < 10). Bấm **Áp dụng thông số** sau khi đổi.",
                "{m:materials}: hộp thoại **Khai báo vật liệu (Material Properties)** – các bảng **Bê tông**, **Cốt thép**, **Thép hình** (E, γ, Rb, Rbt, Rs…); **＋ Thêm**, **Xoá (không dùng)**, **Đặt lại theo thư viện**. Giá trị tính toán theo TCVN 5574:2018 (bê tông Bảng 7, 10; cốt thép Bảng 13) và TCVN 5575:2024 (thép hình f = fy/γM).",
                "Bảng tra cường độ đầy đủ: {m:appendix}.");

        c.Sec("mo-hinh.tiet-dien", "Tiết diện dầm, cột, sàn")
            .P("Có hai nơi khai báo tiết diện:")
            .Bullets("Tab **Tiết diện**: **+ Chữ nhật BTCT**, **+ Chữ I tổ hợp**, **Xoá**; bảng sửa trực tiếp kích thước (mm). Chữ nhật: b (song song trục 3) × h (song song trục 2). Chữ I: h1 đầu I, h2 đầu J (vát), bf × tf cánh, tw bụng. Phần dưới: **Tiết diện sàn** (+ Thêm, Xoá, chiều dày).",
                "{m:sections} (hoặc nút **Khai báo tiết diện…** trên hàng công cụ dầm) – cửa sổ **Khai báo tiết diện dầm / cột (Frame Sections)** như Define → Frame Sections của ETABS, không chặn cửa sổ chính.")
            .Table(new[] { "Nút / cột", "Ý nghĩa" },
                new[] { "＋ Dầm chữ nhật… / ＋ Cột chữ nhật…", "Thêm tiết diện mới (tên tự động dạng D22x40, C22x30)." },
                new[] { "Sao chép, Đổi tên…, Tên tự động, Màu…", "Nhân bản, đổi tên (tên mới áp cho mọi thanh đang dùng), đặt tên theo kích thước, màu hiển thị." },
                new[] { "Xoá…", "Tiết diện đang dùng chỉ xoá được khi gán lại các thanh sang tiết diện khác." },
                new[] { "Nhập từ dự án…", "Lấy tiết diện từ một file .kcvxt khác." },
                new[] { "Hệ số I33, I22, J, A", "Hệ số điều chỉnh độ cứng như Property Modifiers của ETABS (vd. dầm I33 = 0,35, cột 0,7 khi phân tích kể nứt)." },
                new[] { "Dùng làm tiết diện hiện hành (bấm đúp)", "Tiết diện dùng cho ＋ Dầm, Dầm phụ, Công xôn, Gán tiết diện (ô **Tiết diện dầm**)." },
                new[] { "Tự đổi tên theo kích thước khi sửa b, h", "Bật thì sửa b, h sẽ đổi tên tương ứng." })
            .Note("Tiết diện dầm khai báo là chữ nhật b×h. Cánh sàn chữ T / L chỉ được vẽ trên mặt cắt dầm (lấy theo ô sàn thực tế hai bên dầm); tính toán vẫn theo chữ nhật b×h – xem {s:mo-hinh.canh-san}.");

        c.Sec("mo-hinh.cot", "Cột")
            .Bullets("Cột được tạo tại mọi giao trục khi **Tạo mô hình**, hoặc theo bản vẽ kiến trúc (bảng **Cột** của hộp thoại nhập: vị trí, Bx, By, tầng lên đến), bản vẽ kết cấu, Revit, ETABS.",
                "Đổi tiết diện cột: chọn cột trên mặt bằng, xem thông tin ở ô dưới; đổi tiết diện trong bảng tiết diện hoặc {m:sections}.",
                "Xoá cột: chế độ **Xoá**, bấm vào cột (chương trình hỏi lại).",
                "Liên kết chân cột: tab Công trình → **Liên kết chân cột** (Ngàm / Khớp).")
            .Note("Phiên bản hiện tại chưa có công cụ vẽ thêm cột bằng chuột (menu Vẽ chưa có mục cột): thêm cột bằng cách tạo lại khung, nhập bản vẽ / Revit / ETABS.");

        c.Sec("mo-hinh.dam", "Vẽ dầm bằng 2 điểm (＋ Dầm)")
            .Steps("Chọn tiết diện ở ô **Tiết diện dầm** (hoặc bấm đúp tiết diện trong cửa sổ khai báo).",
                "Bật **＋ Dầm**, bấm điểm đầu rồi điểm cuối. Chương trình bắt đầu dầm, nút, giao trục, giao điểm, trung điểm, vuông góc, điểm gần nhất, điểm trên trục (bật/tắt ở nhóm **Bắt điểm**; tắt ô **ETABS** = bắt nút/giao trục/lưới 50 mm như cũ).",
                "Bấm tiếp để vẽ nối; **chuột phải** kết thúc chuỗi, chuột phải lần nữa hoặc **Esc** thoát công cụ.",
                "Gõ số trên mặt bằng để mở ô nhập toạ độ: \"x,y\" tuyệt đối, \"@dx,dy\" tương đối điểm trước, \"L<góc\" toạ độ cực, \"L\" chiều dài theo hướng chuột; đơn vị theo ô **Gõ toạ độ** (mm / m) hoặc hậu tố mm, cm, m.")
            .P("Dầm mới được chia tại giao điểm với dầm có sẵn; ô sàn bị chia lại tương ứng. Phạm vi tầng áp dụng theo ô **Phạm vi tầng** ({s:mo-hinh.dam-etabs}).");

        c.Sec("mo-hinh.dam-etabs", "Công cụ dầm kiểu ETABS")
            .P("Hàng công cụ **Dầm (kiểu ETABS):** dưới thanh sửa mặt bằng:")
            .Table(new[] { "Công cụ", "Cách dùng" },
                new[] { "Vẽ nhanh theo lưới", "Bấm vào đoạn trục giữa hai giao trục để đặt dầm; kéo khung (trái → phải: đoạn nằm trọn trong khung; phải → trái: đoạn chạm khung) để đặt nhiều dầm. Đoạn đã có dầm được bỏ qua. Shift + kéo hoặc chuột giữa để di chuyển." },
                new[] { "Dầm phụ", "Bấm vào trong một ô (kín bởi dầm hoặc ô lưới) → chọn phương (song song trục X / Y), **Chia đều – số dầm n**, **Cách đều – khoảng cách s (mm)** (kèm khoảng đầu) hoặc **Danh sách khoảng cách tới mốc (mm)**; **Mốc đo (từ trục)**, **Đo từ mép cuối ô**, tiết diện, **Chia lại ô sàn theo dầm phụ** (tải sàn truyền vào dầm phụ như ETABS)." },
                new[] { "Công xôn", "Bấm điểm gốc (nút, đầu dầm, giao trục, điểm trên dầm) rồi nhập độ vươn và hướng (ban công, ô văng)." },
                new[] { "Dầm theo toạ độ / trục…", "Tab **Theo trục + khoảng cách** (vd. trục 2 + 1200 từ A đến C), **Toạ độ tuyệt đối / tương đối**, **Dầm công xôn**." },
                new[] { "Chọn nhiều", "Bấm để thêm/bớt dầm; kéo khung trái → phải chọn dầm nằm trọn, phải → trái chọn dầm chạm khung (giữ Ctrl để bỏ chọn); Delete xoá." },
                new[] { "Gán tiết diện", "Gán tiết diện hiện hành cho các dầm đã chọn." },
                new[] { "Điểm chèn…", "Điểm chèn (cardinal point) và dời ngang tiết diện – vd. mép dầm trùng trục / mép tường." },
                new[] { "Sao chép / Đối xứng…", "Tab **Tịnh tiến** (Δx, Δy, n bản), **Đối xứng qua trục**, **Sang tầng khác** (Replicate)." },
                new[] { "Phạm vi tầng", "Tầng hiện tại / Các tầng tương tự / Tất cả tầng / Tầng đánh dấu (nút **Chọn tầng…**) – áp dụng cho mọi công cụ vẽ dầm (như One Story / Similar Stories / All Stories của ETABS)." },
                new[] { "Bắt điểm", "ETABS (bật/tắt chung), Nút, Giao trục, Giao, Giữa, Vuông góc, Gần, Trên trục." },
                new[] { "Gõ toạ độ", "Đơn vị số khi gõ toạ độ (mm / m)." })
            .Tip("Mọi thao tác hoàn tác được bằng Ctrl+Z. Thanh trạng thái luôn ghi bước tiếp theo của công cụ đang bật.");

        c.Sec("mo-hinh.dam-cong", "Dầm cong")
            .Bullets("**＋ Dầm cong**: bấm điểm đầu, điểm cuối rồi một điểm bất kỳ trên cung (dây chun hình cung ghi R và chiều dài cung). Dầm thẳng bị cung cắt qua được chia tại giao điểm.",
                "**Uốn cong** dầm có sẵn: chọn dầm, chọn **Độ vồng f** hoặc **Bán kính R**, nhập giá trị (m; 0 = nắn thẳng; số âm = vồng về phía phải hướng I→J) rồi bấm **Uốn cong**.",
                "Phân tích chia dầm cong thành các đoạn thẳng ≤ 10°, tải theo chiều dài cung; thiết kế kể **xoắn** ({s:thiet-ke.dam-cong}).",
                "Sàn kề dầm cong lấy theo dây cung, tải dồn lên dây cung được rải lại lên cung; phần cung vồng ra ngoài ô sàn được cộng tải viên phân.");

        c.Sec("mo-hinh.san", "Sàn và cấu tạo sàn")
            .Bullets("**＋ Ô sàn**: bấm vào ô kín bởi dầm; công năng theo ô **Công năng** (Phòng ở, Vệ sinh, Ban công, Hành lang, Kho, Mái) quyết định hoạt tải và hoàn thiện.",
                "Chiều dày sàn: tiết diện sàn (tab Tiết diện → Tiết diện sàn; mặc định S100, S120).",
                "{m:buildUps}: thư viện **Cấu tạo sàn** – các lớp (dày, trọng lượng riêng, tải cố định), hoạt tải tiêu chuẩn; tải hoàn thiện = Σ(dày × γ + tải cố định); không khai báo bản BTCT (trọng lượng bản thân tính theo tiết diện sàn). Nút **Gán cho ô sàn đang chọn**.",
                "Chế độ **Gán cấu tạo**: chọn cấu tạo ở ô **Cấu tạo sàn** rồi bấm các ô sàn.",
                "Phương án thép sàn (thép mũ + lưới dưới, lưới 2 lớp, lưới 2 lớp + mũ tăng cường): {s:thiet-ke.san}.");

        c.Sec("mo-hinh.ban-cong", "Ban công công xôn")
            .Steps("Nhập **Vươn (m)** (0,5 – 3,0 m) và **Lan can (kN/m)** trên thanh công cụ.",
                "Bật **＋ Ban công**, bấm vào dầm biên ngoài nhà.",
                "Chương trình thêm dầm công xôn tại các cột, dầm biên đầu công xôn, ô sàn ban công (hoạt tải 2,0 kN/m²) và tải lan can trên dầm biên.")
            .Tip("Ban công không có cột đỡ ở giữa: dùng công cụ **Công xôn** để đặt từng dầm công xôn tại vị trí mong muốn.");

        c.Sec("mo-hinh.cau-thang", "Cầu thang")
            .Steps("Bật **＋ Cầu thang**, bấm vào ô sàn / ô kín bởi dầm: sàn được bỏ, ký hiệu cầu thang vẽ trong ô; tải bản thang (6,5 kN/m²) và hoạt tải (3,0 kN/m², nửa vế) truyền vào dầm viền.",
                "Chọn ô cầu thang rồi bấm **Khai báo cầu thang…**: phác thảo trực tiếp mặt bằng và mặt cắt vế 1, kiểm tra 2h + b.",
                "**Hình học**: tên, dạng thang, phương vế (tự động theo cạnh dài ô / theo trục X / Y), chân thang ở phía toạ độ lớn, tổng số bậc (0 = chiều cao tầng / ~170 mm), số bậc vế 1, bề rộng bậc b (0,63 − 2h trong 260…300 mm), bề rộng vế, khe giữa 2 vế, chiều dày bản (0 = L/30 ≥ 100), chiều sâu và chiều dày chiếu nghỉ.",
                "**Sơ đồ chịu lực**: bản thang tựa dầm chiếu nghỉ / dầm sàn (bản chịu lực) hoặc có dầm cốn (limon, 1 hoặc 2 bên); dầm chiếu nghỉ; tuỳ chọn gối ngàm một phần (M = qL²/10).",
                "**Bậc thang, hoàn thiện, tải**: bậc xây gạch / BTCT, ốp mặt bậc (đá granite, gạch ốp, gỗ…), tải trọng → Áp dụng (hoàn tác được).")
            .P("Kết quả thiết kế ở tab Thiết kế → Cầu thang, bản vẽ KC-08 ({s:thiet-ke.cau-thang}).");

        c.Sec("mo-hinh.gieng-troi", "Giếng trời, thông tầng, sàn vệ sinh")
            .Bullets("**＋ Giếng trời**: bấm vào ô sàn → bỏ sàn (ô thông tầng), lan can 0,5 kN/m quanh lỗ.",
                "**Sàn vệ sinh**: bấm vào ô sàn → hoàn thiện 2,0 kN/m² (chống thấm, hạ cốt); bấm lại để trả về phòng ở.",
                "Xoá lỗ sàn (chế độ **Xoá**) sẽ đặt lại sàn.");

        c.Sec("mo-hinh.san-ha-cot", "Sàn hạ cốt (hạ cos)")
            .P("Ô sàn có mặt trên thấp hơn cao độ sàn tầng một khoảng Δ (âm = hạ; trong khoảng −500 … 0 mm): sàn vệ sinh (chống thấm + vữa tạo dốc; WC hạ sâu đặt xí bệt âm −100 … −300 mm), ban công, lô gia, sân thượng, bếp…")
            .Steps("Thanh sửa mặt bằng: nhập độ hạ ở ô **Hạ cốt (mm)** (vd. −50; gõ 50 cũng hiểu là hạ 50 mm).",
                "Chọn chế độ **Hạ cốt sàn** rồi bấm vào từng ô sàn → ô được tô gạch chéo, viền nét đứt và ghi \"▽ −0.050 (−50)\"; bấm lại ô đang có đúng Δ đó để **bỏ** hạ cốt. Góc dưới trái mặt bằng hiện chú thích các mức hạ của tầng.",
                "Gán hàng loạt: nút **Hạ cốt sàn…** → hộp **Hạ cốt sàn (sàn vệ sinh, ban công, lô gia…)**: **Áp dụng cho** (Ô đang chọn / Mọi ô cùng công năng – tầng đang xem / mọi tầng / Mọi ô đang hạ cốt), **Hạ cốt Δ (mm)** (0 = bỏ), **Ghi chú** (lý do, in lên bản vẽ và thuyết minh).",
                "Cùng hộp: bảng **Hạ cốt mặc định theo công năng** (Phòng ở 0; **Vệ sinh −50**; **Ban công, lô gia −50**; Hành lang, Kho, Mái 0 – sửa được), ô **Tự đặt hạ cốt mặc định khi đổi công năng** và **Ngưỡng bậc sàn (mm)** (mặc định 30). → **Đồng ý** (hoàn tác Ctrl+Z).")
            .Bullets("Mặc định chỉ áp dụng khi **đổi** công năng ô sàn (nút Sàn vệ sinh, thêm ô sàn, ban công) – ô đã chỉnh tay giữ nguyên, mở dự án cũ không đổi gì.",
                "Phân tích: tải sàn giữ nguyên, nút ô sàn vẫn ở cao độ tầng (Δ là thông tin hình học / cấu tạo).",
                "Thiết kế sàn: cạnh chung giữa hai ô chênh cốt > ngưỡng bậc tính **khớp** (không liên tục mô men qua bậc); mỗi ô có thép mũ cấu tạo riêng neo vào dầm, không có thép mũ / lưới trên vắt liên tục qua bậc.",
                "Dầm đỡ ô hạ cốt: kiểm tra **h ≥ hs1 + Δ + hs2** – không đạt thì cảnh báo và đề nghị chiều cao dầm (tab Kiểm tra mô hình, thuyết minh mục sàn).",
                "Bản vẽ: mặt cắt dầm vẽ bậc sàn hai bên theo đúng cao độ; mặt bằng kết cấu đánh dấu \"SÀN HẠ COS −50\" + gạch chéo; bảng khối lượng tính đúng cao độ.");

        c.Sec("mo-hinh.tuong", "Tường – loại tường, vẽ và gán tường")
            .Bullets("{m:wallTypes}: thư viện **Loại tường** – tên, chiều dày, γ xây, hoàn thiện, trát mỗi mặt, γ trát, tỷ lệ cửa (%), tải tường đặc (kN/m²). Tải 1 m² tường đặc = dày × γ xây + số mặt trát × dày trát × γ trát; tải lên dầm = tải 1 m² × chiều cao × (1 − tỷ lệ cửa). **Khôi phục thư viện mặc định** nếu lỡ xoá.",
                "**＋ Tường**: chọn **Loại tường**, **Cao (m)** (0 = tự động: chiều cao tầng trên − chiều cao dầm), **Diện tích cửa (m²)** (trống = theo tỷ lệ cửa của loại tường; 0 = không có cửa) rồi bấm 2 điểm.",
                "**Gán tường**: bấm vào dầm để đặt tường suốt chiều dài dầm (thay tường cũ trùng vị trí). Tường gán lên dầm cong được coi là tường cong theo dầm.",
                "{m:walls}: bảng mọi đoạn tường – sửa loại, chiều cao, diện tích cửa, xoá.",
                "Tường 110 vẽ nhạt, 220 trung bình, 330 đậm; tường không trát vẽ viền nét đứt. Tải tường dồn vào dầm bên dưới (nguồn tải WALL).");

        c.Sec("mo-hinh.chinh-sua", "Chọn, xoá, chia thanh")
            .Bullets("Chế độ **Chọn**: bấm chọn cột, dầm, ô sàn, lỗ sàn; rê chuột sẽ sáng nhẹ đối tượng dưới con trỏ. Thông tin hiện ở ô dưới khung nhìn.",
                "Chế độ **Xoá** hoặc phím **Delete**: xoá tường, dầm, cột (hỏi lại), ô sàn, lỗ sàn.",
                "{m:split}: chia các thanh tại nút trung gian nằm trên thanh (sau khi nhập mô hình từ nguồn khác).",
                "Hoàn tác: Ctrl+Z (30 bước).");

        c.Sec("mo-hinh.chon", "Chọn đối tượng (menu Chọn)")
            .Bullets("{m:selOne} (Esc) và {m:selMulti} – bấm để thêm/bớt, kéo khung trái → phải chọn đối tượng nằm trọn, phải → trái chọn đối tượng chạm khung; giữ Ctrl để bỏ chọn.",
                "Mọi dầm / mọi cột / {m:selAll} của tầng đang xem (**Ctrl+A** khi đang ở khung nhìn Mặt bằng và không gõ chữ trong ô nhập).",
                "{m:selBySection}: chọn mọi thanh dùng một tiết diện; {m:selClear}.",
                "Các lệnh menu **Gán** áp dụng cho đối tượng đang chọn; cột đang chọn nhiều được tô viền trên mặt bằng.");

        c.Sec("mo-hinh.gan", "Gán tải trọng, liên kết, giải phóng, tiết diện (menu Gán)")
            .P("Chọn đối tượng trước ({s:mo-hinh.chon}) rồi dùng menu **Gán** (như Assign của ETABS). Mọi thao tác hoàn tác được bằng Ctrl+Z; tải gán là **tải nhập tay** (khác tải dồn từ sàn / tường được sinh lại mỗi lần phân tích).")
            .Table(new[] { "Lệnh", "Nội dung" },
                new[] { "{m:assignFrameLoad}", "Hộp **Gán tải trên thanh**: trường hợp tải; phương (Trọng lực −Z, dương hướng xuống; phương tổng thể hoặc địa phương: trục 1 dọc thanh I→J, trục 2 hướng lên, trục 3 = 1×2); dạng tải **Phân bố đều** (q1), **Hình thang / tam giác** (Điểm 1, Điểm 2: khoảng cách + q), **Lực tập trung** (P, khoảng cách); khoảng cách tương đối (0…1) hoặc tuyệt đối (m từ đầu I); cách gán **Cộng thêm vào tải đã có / Thay thế tải nhập tay cùng trường hợp / Xoá tải nhập tay cùng trường hợp**." },
                new[] { "{m:assignAreaLoad}", "Tải phân bố đều lên **Ô sàn đang chọn / Mọi ô sàn tầng đang xem / Mọi ô sàn của công trình**; tải sàn được dồn về dầm theo đường phân giác 45° khi phân tích." },
                new[] { "{m:assignJointLoad}", "Lực, mô men tại nút trong hệ toạ độ tổng thể (Fz dương hướng lên – tải trọng lực nhập số âm)." },
                new[] { "{m:assignRestraint}", "Nút **Ngàm** (giữ 6 bậc tự do), **Khớp** (UX, UY, UZ), **Con lăn (UZ)**, **Tự do**; nút nhanh **Mọi nút chân cột thấp nhất**. Gối thật (chân cột / móng) cần giữ UZ." },
                new[] { "{m:assignRelease}", "Giải phóng mô men đầu thanh: **Khớp đầu I**, **Khớp đầu J**, **Khớp hai đầu (M2, M3)**, **Không giải phóng** (vd. dầm phụ gác khớp lên dầm chính)." },
                new[] { "{m:assignSection}", "Gán tiết diện cho mọi thanh đang chọn (dầm và cột)." },
                new[] { "{m:assignModifiers}", "Hệ số độ cứng thanh (nhân thêm với hệ số của tiết diện): nút **Dầm nứt (I33 = 0,35)**, **Cột nứt (I = 0,7)**, **Đặt lại 1** – chỉ ảnh hưởng độ cứng khi phân tích." },
                new[] { "{m:insertion}", "Điểm chèn / dời ngang tiết diện ({s:mo-hinh.dam-etabs})." },
                new[] { "{m:assignDiaphragm}", "Đánh dấu sàn tuyệt đối cứng cho tầng ({s:mo-hinh.khai-bao})." },
                new[] { "{m:assignWall}, {m:assignBuildUp}, {m:assignWc}", "Chế độ bấm trên mặt bằng như nút **Gán tường**, **Gán cấu tạo**, **Sàn vệ sinh**." })
            .P("{m:showLoads}: vẽ tải nhập tay trên mặt bằng (đa giác tải + mũi tên + giá trị cho tải thanh, giá trị tại tâm ô cho tải sàn, mũi tên cho tải nút), lọc theo trường hợp tải chọn trên cây mô hình hoặc ở ô **Biểu đồ M3 theo**.");

        c.Sec("mo-hinh.khai-bao", "Khai báo trường hợp tải, trường hợp phân tích, phổ phản ứng, sàn cứng (menu Khai báo)")
            .Bullets("{m:loadPatterns}: hộp **Trường hợp tải (Load Patterns)** – tên, loại (Dead = tĩnh tải có trọng lượng bản thân hệ số 1, SuperDead = hoàn thiện / tường, Live / RoofLive = hoạt tải, Wind = gió, Seismic = động đất), hệ số trọng lượng bản thân, γf; thêm, đổi tên (cập nhật mọi tham chiếu), xoá.",
                "{m:loadCases}: hộp **Trường hợp phân tích (Load Cases)** – mỗi trường hợp tải được giải bằng một trường hợp phân tích tĩnh tuyến tính cùng tên (như ETABS); tổ hợp cộng tuyến tính kết quả; nút **Nguồn khối lượng & dao động…** và **Phổ phản ứng / động đất…**.",
                "{m:modal}: nguồn khối lượng và dao động riêng ({s:phan-tich.dao-dong}).",
                "{m:spectrum}: hộp **Phổ phản ứng / động đất (TCVN 9386-1:2025)** – **Tính động đất (trường hợp DDX, DDY)**, gia tốc nền agR (g), loại nền, hệ số ứng xử q, hệ số tầm quan trọng γI, hệ số tổ hợp hoạt tải ψE, phương pháp; đồ thị phổ thiết kế Sd(T).",
                "{m:diaphragms} / {m:assignDiaphragm}: **Dùng sàn tuyệt đối cứng trong mặt phẳng** (chung cho phân tích tĩnh và dao động); tầng được đánh dấu: các nút sàn cùng cao trình chuyển vị ngang và xoay quanh Z như một khối (nút chủ tại trọng tâm).",
                "Tổ hợp tải trọng: {s:tai-trong.sua-to-hop}.");

        c.Sec("mo-hinh.canh-san", "Dầm có cánh sàn (chữ T / L) trên bản vẽ")
            .Bullets("Bản vẽ chi tiết dầm (KC-04) thể hiện **cánh sàn liền khối theo mô hình** (kiểu Kết cấu Soft / XSCad): chữ **T** khi có sàn hai bên, chữ **L** ở dầm biên, chữ nhật khi không có sàn (lỗ cầu thang, giếng trời); ghi kích thước hs, đường cắt ở đầu cánh; mặt đứng dầm có nét mép dưới sàn. Cánh lấy theo ô sàn thực tế hai bên từng mặt cắt (lỗ sàn, độ lệch tim dầm so với trục).",
                "Tuỳ chọn ở {m:drawStyle} (tab **Tỷ lệ, cỡ chữ, chú thích**): **Cánh sàn trên mặt cắt dầm** (bật/tắt), **Thép sàn trong cánh** (thép lớp dưới, thép mũ vắt qua dầm, thép phân bố, nhãn \"Ø8a200 (thép sàn)\" theo kết quả thiết kế sàn; chưa thiết kế sàn thì vẽ quy ước nét đứt), **Chiều dài cánh sàn thể hiện (mỗi bên dầm)** (100…3000 mm, mặc định 400), **Đường cắt đầu cánh sàn**: Dích dắc / Nét thẳng.")
            .Warn("**Thiết kế** dầm vẫn theo tiết diện chữ nhật b×h (tính uốn bằng tiết diện chữ nhật – không kể cánh chịu nén), thiên về an toàn. Cánh sàn chỉ là thể hiện bản vẽ.");

        c.Sec("mong.dia-chat", "Địa chất – hố khoan (tab Địa chất)")
            .Bullets("**Hố khoan khảo sát địa chất**: **+ Hố khoan**, **Nhân bản**, **Xoá**; tên, toạ độ X, Y, cao độ miệng, mực nước ngầm sâu (−1 = không gặp).",
                "**Các lớp đất** (độ sâu tính từ miệng hố khoan): loại đất, chiều dày/độ sâu, γ, γsat, φ, c, IL (−1 = đất rời), SPT N…; **+ Lớp**, **Xoá lớp**, **Sắp xếp / nối liền**, **Chọn theo đất tham khảo…** (khoảng giá trị điển hình, giá trị đại diện, tương quan SPT).",
                "**Hình trụ hố khoan** vẽ tô theo loại đất.",
                "**Áp dụng cho thiết kế móng**: tích **Dùng hố khoan cho thiết kế móng** (móng đơn, móng băng lấy φ, c, γ lớp đặt móng và kiểm tra lún; móng cọc dùng hố khoan gần chân cột nhất); **Gán chỉ tiêu lớp đặt móng → tab Công trình**; **Mặc định nền đất tốt khi chưa có khảo sát**; **Đặt lại nền đất tốt giả định**.",
                "**Gợi ý phương án móng…**: chương trình đề xuất móng đơn / băng / cọc theo hố khoan và tải chân cột.")
            .Note("Không có hố khoan: thiết kế móng dùng chỉ tiêu nhập tay ở tab Công trình → **Nền đất dưới móng đơn (TCVN 9362:2012)** (φ, c, γ, Df, R tiêu chuẩn nhập tay – 0 = tính).");

        c.Sec("mong.loai", "Móng – loại móng và tab Mô hình móng")
            .Bullets("Tab **Mô hình móng**: mặt bằng móng (lưới trục, cột, móng đơn L×B, đài cọc, móng băng, lực dọc N, màu đạt/không đạt) và mặt cắt móng đang chọn (bản đế vát, cổ móng, cốt ±0.000, thép lớp dưới). Bật/tắt **Ghi kích thước móng**.",
                "**Loại móng** (cả công trình): Móng đơn, Móng cọc, Móng băng. **Tại chân cột này**: chọn một móng trên mặt bằng rồi đặt loại riêng (Theo công trình / Móng đơn / Móng cọc / Móng băng).",
                "**Thông số…** ({m:designFnd}): hộp thoại **Thiết kế móng** – móng cọc (cọc vuông/tròn, cạnh/đường kính, chiều dài, [P] nén, [P] kéo, khoảng cách tim cọc, mép đài, đoạn ngàm, chiều cao đài tối thiểu, tính [P] và chiều dài cọc từ hố khoan theo SPT – TCVN 10304:2014, cọc khoan nhồi hay ép/đóng, FS, [H]); móng băng (phương dải: tự chọn, dọc trục X, dọc trục Y, giao thoa 2 phương; bề rộng sườn; hệ số nền); tuỳ chọn bản vẽ (tháp chọc thủng 45°, biểu đồ mô men sườn).",
                "**Thiết kế móng**: thiết kế lại toàn bộ móng; **Kiểm tra an toàn…**: bảng kiểm tra an toàn và sự làm việc của móng đang chọn.")
            .P("Thiết kế và ý nghĩa kết quả móng: {s:mong.thiet-ke}.");

        c.Sec("mong.coc", "Móng cọc – bố trí cọc")
            .Steps("Tab **Mô hình móng**: chọn **Loại móng** = Móng cọc (hoặc đặt riêng chân cột), khai báo cọc ở **Thông số…** → Thiết kế móng.",
                "Chọn một đài trên mặt bằng móng → **Bố trí cọc…**: chọn số cọc, **Sơ đồ bố trí cọc**, khoảng cách cọc, kích thước / chiều cao đài (**Thông số bố trí (0 = tự chọn)**). Bảng kiểm tra an toàn cập nhật trực tiếp.",
                "**Áp dụng** (hoàn tác được) hoặc **Tự chọn (bỏ bố trí riêng)** để trả về bố trí tự động.")
            .Note("[P] = min(Rc,u/FS; Pvl), Pvl = Rb·Ab + Rsc·As. Khi tính từ hố khoan: mũi tự chọn tại mặt lớp đầu tiên có N ≥ 30 (đất rời) / N ≥ 20 (đất dính) + 3d, ≤ 40 m. [P] nên lấy theo thí nghiệm nén tĩnh khi có.");

        c.Sec("mong.lech-tam", "Móng lệch tâm, ranh giới đất, lùi cọc")
            .Bullets("**Ranh giới đất…**: chọn các cạnh ranh (trái / phải / trước / sau) theo trục + khoảng cách ra phía ngoài lô (vd. cột 220 tim trục sát ranh: 110 mm), khoảng hở mép móng – ranh, dung sai nhận cột sát ranh, phương án lệch tâm mặc định; xem trước (nét đứt đỏ là ranh giới, cột sát ranh tô cam). Móng sát ranh tự lệch tâm, không vượt ranh.",
                "**Lệch tâm…** cho chân cột đang chọn: (a) Có dầm móng cân bằng, (b) Lệch tâm tự cân bằng, (c) Móng kết hợp, hoặc Tự chọn phương án; có thể nhập trực tiếp độ lệch tâm tâm móng – tim cột; kích thước dầm cân bằng / móng kết hợp.",
                "**Lùi cọc khỏi ranh giới** (móng cọc, trong hộp Ranh giới đất): khoảng lùi chung đo tới tim / mép cọc theo phương pháp ép, khoảng lùi riêng từng cạnh (trống = theo chung, 0 = phía không có nhà bên cạnh), tuỳ chọn mép đài. Mặt bằng móng vẽ đường giới hạn tim cọc.",
                "Bản vẽ móng thể hiện ranh giới đất, móng lệch tâm, dầm móng cân bằng (DM b×h).");

        c.Sec("mong.cu-tram", "Gia cố nền bằng cừ tràm")
            .Bullets("Nút **Cừ tràm…** trên tab Mô hình móng (hộp thoại không chặn): chiều dài cừ, Ø gốc / ngọn, mật độ, bê tông lót, cát đệm, MNN thấp nhất, FS, η, αs, chỉ tiêu sau gia cố, phương pháp tính.",
                "**Phạm vi áp dụng**: cả công trình (móng đơn + móng băng) hoặc riêng chân cột đang chọn.",
                "Kết quả tức thời của hai phương pháp cho móng đang chọn (hoặc móng thử 1,2 × 1,2 m), bảng kiểm tra tô màu và cảnh báo; **Áp dụng + thiết kế lại** (hoàn tác được).")
            .Warn("Giá trị mặc định là kinh nghiệm thực hành – người thiết kế kiểm tra. Đầu cừ phải nằm dưới mực nước ngầm thấp nhất.");
        return c;
    }

    // ==================================================================
    // 7. TẢI TRỌNG
    // ==================================================================
    private static GuideSection ChLoads()
    {
        var c = Ch("tai-trong", "Tải trọng và tổ hợp");

        c.Sec("tai-trong.truong-hop", "Trường hợp tải (tab Tải trọng)")
            .P("Bảng **Trường hợp tải** (+ Thêm, Xoá) gồm tên, loại tải, hệ số trọng lượng bản thân, hệ số độ tin cậy γf. Dự án mới có sẵn:")
            .Table(new[] { "Tên", "Loại", "γf mặc định", "Ghi chú" },
                new[] { "TT", "Tĩnh tải", "1,1", "Có trọng lượng bản thân dầm, cột, sàn (hệ số 1)." },
                new[] { "HTHT", "Hoàn thiện", "1,3", "Lớp hoàn thiện, tường, cấu tạo sàn." },
                new[] { "HT", "Hoạt tải", "1,3", "Hoạt tải sàn, mái theo công năng." },
                new[] { "GX, GY", "Gió", "1,2", "Sinh tự động khi bật Tính tải gió." },
                new[] { "DDX, DDY", "Động đất", "1,0", "Thêm khi bật Tính động đất." })
            .Note("γf là lựa chọn mặc định của chương trình – người dùng sửa theo TCVN 2737:2023 và loại tải của công trình.");

        c.Sec("tai-trong.san", "Tải sàn theo công năng và cấu tạo sàn")
            .Bullets("Tab Công trình → **Hoàn thiện sàn**, **Hoạt tải sàn**, **Hoạt tải mái** (giá trị tiêu chuẩn, kN/m²) dùng khi tạo mô hình.",
                "Công năng ô sàn (Phòng ở, Vệ sinh, Ban công, Hành lang, Kho, Mái) và cấu tạo sàn gán cho ô ({s:mo-hinh.san}) thay đổi hoàn thiện, hoạt tải của ô.",
                "Tải sàn được dồn về dầm theo đường phân giác 45° (tam giác/hình thang); bật **Hiện tải tự sinh (sàn)** ở bảng **Tải trên thanh** để xem.");

        c.Sec("tai-trong.tuong", "Tải tường")
            .Bullets("Tường biên: ô **Tường biên trên dầm (kN/m)** ở tab Công trình (dùng khi tạo mô hình). Ví dụ tường 220 gạch đặc cao 3 m ≈ 0,22·18·(3 − 0,3) ≈ 10,7 kN/m.",
                "Tường khai báo theo loại tường ({s:mo-hinh.tuong}): tải tính theo chiều dày, trát, chiều cao, diện tích cửa và dồn lên dầm bên dưới.");

        c.Sec("tai-trong.nhap-tay", "Tải trên thanh và tải trên sàn nhập tay")
            .Bullets("**Tải trên thanh (kN/m)**: chọn thanh trên mặt bằng → **+ Cho thanh đang chọn**; sửa trường hợp tải, phương, các điểm tải trong bảng; **Xoá**.",
                "**Tải trên sàn (kN/m²)**: **+ Cho các ô sàn tầng đang xem** rồi sửa giá trị, trường hợp tải.",
                "Tải tự sinh (sàn, tường, cầu thang, lỗ sàn) được chương trình lập lại mỗi khi mô hình thay đổi – sửa ở nguồn (công năng, cấu tạo sàn, tường), không sửa trực tiếp.");

        c.Sec("tai-trong.gio", "Gió – địa danh, vùng gió")
            .Steps("Tab Công trình → nhóm **Địa điểm xây dựng – vùng gió, động đất (QCVN 02:2022/BXD)**: **Tỉnh/Thành phố (từ 01/7/2025)** – 34 tỉnh/thành sau sắp xếp, gõ được tên tỉnh cũ (vd. Bắc Kạn → Thái Nguyên), không dấu cũng được.",
                "**Xã/phường (huyện cũ)**: chọn từ danh mục hoặc gõ tên mới; **Vùng áp lực gió** I…V → W0 tự điền.",
                "**Lưu địa danh** để lưu tỉnh, địa danh, vùng gió, agR, loại nền vào danh mục của bạn; **Danh mục địa danh…** ({m:locations}) để xem/sửa, **Nhập từ Excel/CSV…** Bảng 5.1, 6.1, 6.2 QCVN 02:2022, **Xuất CSV mẫu…**, **Dùng cho công trình**.",
                "Nhóm **Gió (TCVN 2737:2023)**: **Tính tải gió** (Bật), **Áp lực gió W0 (kN/m²)**, **Dạng địa hình** A/B/C, **Hệ số khí động tổng Cp** → **Áp dụng thông số**.")
            .Warn("Chương trình chỉ có sẵn bảng W0 theo vùng gió I–V và danh sách 34 tỉnh/thành; phân vùng chi tiết đến xã/phường do người dùng nhập – **đối chiếu QCVN 02:2022 cho địa danh cụ thể**. Chưa tính thành phần động của gió.");

        c.Sec("tai-trong.dong-dat", "Động đất (TCVN 9386-1:2025)")
            .Bullets("Nhóm **Động đất**: **Tính động đất** (thêm trường hợp DDX, DDY), **Gia tốc nền agR (g)** (tra QCVN 02:2022 Bảng 6.1, 6.2 theo địa danh), **Loại nền đất** A…E, **Hệ số ứng xử q**.",
                "Mặc định: phương pháp tĩnh lực ngang tương đương, T1 = Ct·H^¾, phổ thiết kế loại 1. Có thể dùng phổ phản ứng sau phân tích dao động riêng ({s:phan-tich.pho}).",
                "Tổ hợp đặc biệt: G + ψE·Q ± E1 ± 0,3·E2.")
            .Note(Std.SeismicMethodNote);

        c.Sec("tai-trong.to-hop", "Tổ hợp tải trọng")
            .P("{m:combos} (hoặc nút **Tạo tổ hợp tải** ở tab Tải trọng) tự sinh tổ hợp theo TCVN 2737:2023:")
            .Bullets("**TH1, TH2, …** – TTGH I: tổ hợp cơ bản 1 (tĩnh tải + 1 tải tạm thời, đủ γf) và tổ hợp cơ bản 2 (tĩnh tải + ≥ 2 tải tạm thời nhân ψ = 0,9); tổ hợp đặc biệt có động đất.",
                "**TC1, TC2, …** – TTGH II: giá trị tiêu chuẩn (hệ số 1,0) dùng cho võng, nứt, nền móng.",
                "**BAO** – tổ hợp bao các tổ hợp TTGH I (biểu đồ vẽ cả max và min).",
                "Bảng **Tổ hợp tải trọng** ở tab Tải trọng hiện công thức từng tổ hợp (vd. 1.1TT + 1.3HTHT + 1.2HT).")
            .Tip("Tạo lại tổ hợp sau khi thêm/xoá trường hợp tải hoặc bật/tắt gió, động đất.");

        c.Sec("tai-trong.sua-to-hop", "Sửa tổ hợp tải trọng (Load Combinations)")
            .Steps("{m:comboEditor}: cửa sổ **Tổ hợp tải trọng (Load Combinations)** (không chặn) – danh sách tổ hợp bên trái.",
                "**＋ Mới** / **Sao chép** / **Xoá** tổ hợp; sửa **Tên tổ hợp**, **Loại**: TTGH I (tổ hợp tính toán – thiết kế cường độ), TTGH II (tổ hợp tiêu chuẩn – võng, nứt, lún), Bao (max/min của các thành phần).",
                "Thành phần: **＋ Dòng**, **Xoá dòng**, **Thêm mọi trường hợp tải** – mỗi dòng là một trường hợp tải hoặc một tổ hợp khác × hệ số.",
                "**Lưu tổ hợp** / **Bỏ sửa**. **Tự sinh TCVN 2737:2023** sinh lại bộ tổ hợp chuẩn – tích **giữ tổ hợp tự đặt** để không mất tổ hợp do bạn thêm.")
            .P("Chương trình kiểm tra tham chiếu (trường hợp / tổ hợp không còn tồn tại); mọi thao tác hoàn tác được. Lệnh nhanh {m:combos} chỉ tự sinh theo tiêu chuẩn ({s:tai-trong.to-hop}).");

        c.Sec("tai-trong.khoi-luong", "Nguồn khối lượng (cho dao động, động đất)")
            .P("{m:modal} → **Nguồn khối lượng (Mass Source)**: khối lượng = Σ hệ số × tải theo phương trọng lực / g. Mặc định: tĩnh tải × 1,0 + hoạt tải × ψE (ψE = φ·ψ2 = 0,8 × 0,3 = 0,24; mái 0). "
               + "Trường hợp có trọng lượng bản thân (TT) gồm dầm, cột, sàn; tường và hoàn thiện dồn lên dầm được quy về nút. Xem {s:phan-tich.dao-dong}.");
        return c;
    }

    // ==================================================================
    // 8. PHÂN TÍCH
    // ==================================================================
    private static GuideSection ChAnalysis()
    {
        var c = Ch("phan-tich", "Phân tích và đọc kết quả");

        c.Sec("phan-tich.chay", "Chạy phân tích")
            .Bullets("{m:run} – phím **F5** hoặc nút **▶ Phân tích**. Chương trình lập ma trận độ cứng khung không gian (sàn cứng trong mặt phẳng), giải mọi trường hợp tải và tổ hợp; thanh trạng thái báo số phương trình, trường hợp tải, số cảnh báo.",
                "Nếu đã bật dao động riêng trong {m:modal}, phân tích dao động chạy cùng lúc.",
                "Nếu kết quả hiện hành là kết quả nhập từ ETABS/SAP, chương trình hỏi trước khi thay bằng kết quả tự tính.",
                "Mô hình rỗng hoặc kết cấu biến hình: chương trình báo lỗi rõ ràng ({s:phan-tich.on-dinh}).");

        c.Sec("phan-tich.dao-dong", "Dao động riêng (tab Dao động)")
            .Steps("{m:modal}: tích **Chạy phân tích dao động riêng cùng phân tích tĩnh (F5)**.",
                "**Nguồn khối lượng**: dùng mặc định hoặc bảng trường hợp tải × hệ số (Thêm dòng / Xoá dòng); **Sàn tuyệt đối cứng (diaphragm)**; **Kể khối lượng theo phương đứng (UZ)**.",
                "**Số dạng dao động** (0 = tự động max(12; 3 × số tầng)), **Phương pháp giải trị riêng** (tự động: giải đầy đủ khi ≤ 800 bậc tự do động, ngược lại lặp không gian con).",
                "**Động đất**: phương pháp (tĩnh lực ngang tương đương / phổ phản ứng), tổ hợp dạng CQC / SRSS, tỷ số cản ζ, xoắn ngẫu nhiên e·L, hiệu chỉnh lực cắt đáy phổ (tuỳ chọn), dùng T1 từ dao động cho phương pháp tĩnh (giới hạn ≤ k × Ct·H^0,75) → Đồng ý, F5.",
                "Khung nhìn **Dao động**: chọn **Dạng**, **Hiển thị**, **Tầng**, **Biên độ**; nút **⏸ Dừng** / chạy hoạt hình; **Thiết lập…** mở lại hộp thoại. Các tab dưới: **Tỷ lệ khối lượng tham gia** (như Modal Participating Mass Ratios của ETABS), **Khối lượng tầng**, **Phổ phản ứng**, **Ghi chú**.");

        c.Sec("phan-tich.pho", "Phổ phản ứng")
            .P("Chọn phương pháp **phổ phản ứng** trong {m:modal}: DDX/DDY là tổ hợp CQC/SRSS các dạng dao động – giá trị **không dấu**; các tổ hợp G + ψE·Q ± E1 ± 0,3·E2 lấy cả hai dấu như cặp bao +/− của ETABS. "
               + "Có thể hiệu chỉnh lực cắt đáy phổ ≥ k × lực cắt đáy tĩnh (tuỳ chọn, TCVN 9386 không bắt buộc). Mặc định vẫn là tĩnh lực ngang tương đương.");

        c.Sec("phan-tich.ket-qua", "Tab Kết quả")
            .Bullets("**Trường hợp / tổ hợp** và **Tầng**: lọc bảng nội lực các thanh (P, V2, V3, T, M2, M3 tại các mặt cắt – cột giống bảng ETABS).",
                "**Phản lực chân cột (kN, kN.m)**: FX, FY, FZ, MX, MY, MZ từng nút chân cột. Kiểm tra nhanh: tổng FZ theo tĩnh tải bằng tổng tải đứng.",
                "**Cảnh báo / thông tin**: thông báo của phân tích (mô hình, tải, sàn, động đất…) – đọc kỹ trước khi thiết kế.");

        c.Sec("phan-tich.bieu-do", "Biểu đồ nội lực")
            .Bullets("Tab **Biểu đồ nội lực**: **Đại lượng** M3, M2, V2, V3, N, T; trường hợp/tổ hợp theo ô **Biểu đồ M3 theo** (BAO vẽ cả max và min).",
                "**Khung nhìn**: Mặt bằng tầng; Mặt đứng theo trục (chọn **Trục** – mô men vẽ phía thớ chịu kéo); 3D.",
                "**Tỉ lệ** tung độ, **Ghi giá trị**, **Vừa màn hình**; đỏ = dương, xanh = âm.",
                "Chọn một thanh ở bất kỳ khung nhìn nào: biểu đồ của thanh hiện ở ô dưới khung nhìn.");

        c.Sec("phan-tich.chuyen-vi", "Chuyển vị, phản lực, lực ngang tầng")
            .P("Chuyển vị nút, phản lực, lực ngang tầng (gió, động đất) và kết quả dao động có trong **Bảng tính Excel** ({m:expExcel}: các sheet Chuyen vi, Phan luc, Luc ngang tang, Dao động riêng, Noi luc) và **Nội lực CSV** ({m:csv}: Element Forces – Frames, Joint Reactions, Joint Displacements, Story Forces theo dạng bảng ETABS). Chuyển vị đỉnh, võng được kiểm tra trong thiết kế ({s:thiet-ke.btct}).");

        c.Sec("phan-tich.nhap-ket-qua", "Nhập kết quả ETABS / SAP2000 / SAFE / phần mềm khác")
            .Steps("{m:importResults}: chọn một hoặc nhiều file bảng kết quả (.xlsx, .csv, .txt, .s2k, .$2k, .$et, .$sf).",
                "Tab **Bảng dữ liệu**: chương trình nhận diện bảng (nội lực thanh, phản lực nút, chuyển vị nút, phản lực tổng, chuyển vị lệch tầng, toạ độ nút, nối thanh, dải sàn SAFE, mô men phần tử sàn, áp lực đất nền, chọc thủng), số dòng, đơn vị.",
                "Tab **Trường hợp / tổ hợp**: phân loại từng trường hợp – TTGH I (thiết kế), TTGH II (tiêu chuẩn), TTGH I + II, Trường hợp tải, Bỏ qua (tự nhận theo tên; tổ hợp bao Max/Min được tách thành \"Tên (Max)\", \"Tên (Min)\").",
                "Tab **Khớp cấu kiện**: khớp thanh theo tên gốc, tên xuất VXT hoặc toạ độ; dòng KHÔNG KHỚP ghi lý do. Tuỳ chọn lấy nội lực của chương trình cho thanh không có trong file.",
                "Tab **Ánh xạ cột (phần mềm khác)** cho CSV tự do (trạm đo tương đối, đổi dấu M3, đổi dấu P); tab **SAFE / sàn, móng**; tab **Xem trước M3**.",
                "**Áp dụng** → bộ nội lực nhập trở thành kết quả hiện hành: thiết kế, biểu đồ, bản vẽ, thuyết minh dùng như kết quả của chương trình.")
            .Warn("Kết quả nhập **không được lưu vào file dự án** – mở lại dự án phải nhập lại. Đặt tên trường hợp tải/tổ hợp trong ETABS giống trong chương trình để ghép tự động.");

        c.Sec("phan-tich.so-sanh", "So sánh với ETABS / SAP2000 / SAFE")
            .Steps("{m:crossCheck}: nếu chưa có kết quả tự tính, chương trình chạy phân tích trước (kết quả hiện hành **không bị thay thế**).",
                "Chưa có mô hình ETABS: bấm **Xuất mô hình sang ETABS (.e2k)…** hoặc **SAP2000 (.s2k)…**, chạy trong ETABS/SAP; xuất bảng: Display → Show Tables → Analysis Results: Element Forces – Beams/Columns (SAP: Frames), Joint Reactions, Base Reactions, Joint Displacements, Story Drifts, Modal Periods → File → Export to Excel (.xlsx) hoặc CSV.",
                "**Thêm file kết quả…** → **Nhận diện**; chọn **Đơn vị khi bảng không ghi đơn vị**, **Ngưỡng cảnh báo (%)**, **không đạt (%)**.",
                "Tab **Ghép trường hợp**: ghép trường hợp/tổ hợp ngoài ↔ của chương trình (tự theo tên, sửa được).",
                "Tab **Cấu kiện**: sai lệch lớn nhất từng thanh – xanh ≤ ngưỡng cảnh báo, cam cảnh báo, đỏ vượt ngưỡng, xám thiếu số liệu; lọc theo tầng/loại, **Chỉ hiện cấu kiện vượt ngưỡng cảnh báo**; nhấp đúp → biểu đồ M3 hai phía.",
                "Tab **Phản lực, chuyển vị, dao động** và **Tổng hợp & nguyên nhân**; xuất báo cáo Excel + Word.")
            .P("Nguyên nhân lệch thường gặp: xem {s:hoi-dap.lech-etabs}.");

        c.Sec("phan-tich.bang-ket-qua", "Bảng kết quả (Show Tables)")
            .Bullets("{m:resultTables} (**Ctrl+T**, nút **Bảng kết quả**): cửa sổ không chặn; tự chạy phân tích nếu chưa có kết quả. Tiêu đề ghi nguồn kết quả (tự tính hay nhập từ ETABS/SAP).",
                "**Bảng**: Chuyển vị nút (Joint Displacements, mm / rad), Phản lực gối (Joint Reactions), Nội lực dầm (Element Forces – Beams), Nội lực cột (Element Forces – Columns), Chuyển vị lệch tầng (Story Drifts), Chu kỳ & tỷ lệ khối lượng (Modal Periods & Mass Ratios), Phản lực đáy (Base Reactions).",
                "Lọc theo **Trường hợp / tổ hợp** và **Tầng**; **Xuất Excel…**, **Xuất CSV…**, **Sao chép** (dán vào Excel).",
                "{m:reactions} mở thẳng bảng Phản lực gối.");

        c.Sec("phan-tich.kiem-tra-mo-hinh", "Kiểm tra mô hình (Check Model)")
            .Steps("{m:modelCheck} (nút **Kiểm tra MH**) trước khi phân tích: cửa sổ **Kiểm tra mô hình (Check Model)** (không chặn) liệt kê vấn đề theo mức độ, mã, tầng, nội dung.",
                "Mục kiểm tra: nút trùng (theo **Dung sai nút (mm)**), thanh trùng / chồng, thanh dài 0, nút tự do, nhóm thanh không nối gối (biến hình), cột không có gối / móng ở chân, dầm cắt nhau không có nút, nút nằm giữa thanh chưa chia, **Cạnh sàn không có dầm**, tiết diện thiếu / không dùng, **Trường hợp tải rỗng**, tổ hợp tham chiếu thiếu, tải gán cho đối tượng / trường hợp không tồn tại. Tích **Hiện mục thông tin** để xem cả mục không phải lỗi.",
                "Bấm đúp một dòng (hoặc **Phóng tới**) để chọn và phóng tới đối tượng trên mặt bằng.",
                "**Sửa mục đang chọn** / **Sửa tự động tất cả**: gộp nút trùng, xoá thanh dài 0 / thanh trùng (chuyển tải sang thanh giữ lại), thêm nút tại giao điểm dầm và chia thanh, chia thanh tại nút nằm giữa, xoá nút tự do, xoá tải mồ côi, bỏ thành phần tổ hợp thiếu. Hoàn tác bằng Ctrl+Z; **Kiểm tra lại**.");

        c.Sec("phan-tich.bien-dang", "Hình biến dạng (Deformed Shape)")
            .Bullets("{m:deformed}: khung dây biến dạng theo **Trường hợp / tổ hợp** tuyến tính, **Khung nhìn** 3D (kéo chuột để xoay) hoặc mặt bằng **Tầng**; **Tỉ lệ** phóng tự động × hệ số; tuỳ chọn **Hình chưa biến dạng** mờ phía sau.",
                "Đường đàn hồi mỗi thanh nội suy Hermite từ chuyển vị và góc xoay hai nút (đầu có giải phóng mô men bỏ góc xoay đầu đó) – dùng để phát hiện nhanh mô hình sai (thanh rời, gối thiếu, chuyển vị bất thường).",
                "{m:diagrams}, {m:modes} chuyển sang khung nhìn Biểu đồ nội lực / Dao động.");

        c.Sec("phan-tich.on-dinh", "Mô hình không ổn định, lỗi phân tích")
            .Bullets("Thông báo kết cấu biến hình / ma trận suy biến: thường do cột không có liên kết chân, dầm treo không nối vào nút, thanh trùng, tất cả liên kết chân là khớp với khung một tầng không có giằng, hoặc giải phóng liên kết quá mức.",
                "Cách xử lý: xem cảnh báo ở tab Kết quả; trên mặt bằng kiểm tra dầm có nối vào cột/dầm khác; chạy {m:split} sau khi nhập mô hình; kiểm tra **Liên kết chân cột**.",
                "Kiểm tra mô hình tự động trước khi phân tích: {m:modelCheck} ({s:phan-tich.kiem-tra-mo-hinh}); gộp nút trùng: {m:mergeNodes}; xem hình biến dạng ({s:phan-tich.bien-dang}).");
        return c;
    }

    // ==================================================================
    // 9. THIẾT KẾ
    // ==================================================================
    private static GuideSection ChDesign()
    {
        var c = Ch("thiet-ke", "Thiết kế cấu kiện");

        c.Sec("thiet-ke.btct", "Thiết kế BTCT – quy trình chung")
            .Bullets("{m:designRc} (nút **Thiết kế BTCT**): tự chạy phân tích nếu cần, thiết kế dầm, cột, sàn, móng, cầu thang theo TCVN 5574:2018 và TCVN 9362:2012 / TCVN 10304:2014; thanh trạng thái báo số cấu kiện không đạt.",
                "Tab **Thiết kế** → Dầm, Cột, Sàn, Móng, Cầu thang, Thép: mỗi dòng một cấu kiện / vùng thép; dòng **tô đỏ** = không đạt. Chọn dòng hoặc chọn cấu kiện trên mặt bằng để xem chi tiết ở ô thông tin.",
                "Mỗi cấu kiện có danh sách hạng mục kiểm tra (yêu cầu / khả năng, hệ số sử dụng k); hạng mục bất lợi nhất quyết định kết luận.",
                "Đổi tuỳ chọn thép, lựa chọn thép riêng, tiết diện → thiết kế lại tự động hoặc khi xuất.");

        c.Sec("thiet-ke.dam", "Dầm")
            .Bullets("Thép dọc theo mô men bao tại gối và nhịp (thép dưới, mũ gối, thép chạy suốt), chịu cắt với c = 2h0, đai; võng dài hạn kể nứt theo thép thực bố trí (B = Eb/(1+φb,cr)·Ieff, φb,cr = 2,5).",
                "Khoảng hở thép theo TCVN 5574:2018 mục 10.3.5; cho phép 2 hàng thép khi bật trong tuỳ chọn.",
                "Bản vẽ: KC-04.n chi tiết dầm theo tầng (mặt đứng, mặt cắt, tag thép kiểu Kết cấu Soft).");

        c.Sec("thiet-ke.cot", "Cột")
            .P("Cốt thép đối xứng, nén lệch tâm xiên bằng đường bao tải trọng (load contour), uốn dọc theo hệ số η; đai theo cấu tạo và vùng tới hạn. Bản vẽ KC-03.");

        c.Sec("thiet-ke.san", "Sàn – phương án thép sàn")
            .Bullets("Ô chữ nhật, phương pháp Marcus theo điều kiện biên; võng dài hạn kể nứt + từ biến.",
                "Phương án thép: **Thép mũ + lưới dưới** (mặc định), **Lưới 2 lớp**, **Lưới 2 lớp + mũ tăng cường** – đặt mặc định ở {m:rebarPrefs} (Phương án thép sàn mặc định), từng ô chọn riêng được bằng **Chọn bố trí thép…** ở tab Thiết kế → Sàn.",
                "Bản vẽ KC-05 mặt bằng thép sàn.");

        c.Sec("mong.thiet-ke", "Móng – thiết kế và kiểm tra")
            .Bullets("Móng đơn, móng băng trên nền thiên nhiên: R nền (TCVN 9362:2012), áp lực đáy (pmax ≤ 1,2R), chọc thủng, uốn, lún cộng lớp phân tố (Sgh = 8 cm).",
                "Móng cọc: [P] theo SPT / chỉ tiêu cơ lý (TCVN 10304:2014), phản lực đầu cọc, chọc thủng đài, uốn đài; bố trí cọc ({s:mong.coc}).",
                "Móng băng: dầm trên nền đàn hồi (hệ số nền), sườn và bản cánh.",
                "Móng lệch tâm / dầm móng cân bằng / móng kết hợp ({s:mong.lech-tam}); cừ tràm ({s:mong.cu-tram}).",
                "Tab Mô hình móng → **Kiểm tra an toàn…**: bảng kiểm tra an toàn và sự làm việc của móng đang chọn; tab Thiết kế → Móng: **Chọn bố trí thép…** (Ø – bước thép đáy).",
                "Bản vẽ KC-02 mặt bằng và chi tiết móng.");

        c.Sec("thiet-ke.cau-thang", "Cầu thang")
            .P("Tab Thiết kế → **Cầu thang**: mỗi bộ phận một dòng (bản thang, chiếu nghỉ, dầm cốn, dầm chiếu nghỉ). Bản vẽ KC-08.n: mặt bằng, mặt cắt, bố trí thép, thống kê thép. Khai báo: {s:mo-hinh.cau-thang}.");

        c.Sec("thiet-ke.dam-cong", "Dầm cong – xoắn")
            .P("Thiết kế trên chiều dài cung: uốn + cắt + **xoắn** (T ≤ 0,1·Rb·b²·h tương tác tuyến tính với Q; đai kín Asw1/s = T/(2·Ak·Rsw) + qsw/(2·Rsw), bước ≤ min(uk/8; 300 mm); cốt dọc Asl = T·uk/(2·Ak·Rs) chia ½ trên + ½ dưới) theo TCVN 5574:2018 mục 8.1.4 (giản lược). Mặt đứng khung chỉ thể hiện hình chiếu của dầm cong.");

        c.Sec("thiet-ke.thep", "Kết cấu thép tiền chế và tab Thép")
            .Bullets("{m:checkSteel}: bền, ổn định trong/ngoài mặt phẳng, độ mảnh [λ] = 180 − 60α, ổn định cục bộ bụng, tiết diện hữu hiệu bụng mảnh, võng kèo, chuyển vị đỉnh cột (TCVN 5575).",
                "Tab Thiết kế → **Thép**: bảng kiểm tra cấu kiện thép; nút **Xuất shop drawing thép…** và **Tuỳ chọn…** (thép cốt BTCT) cũng đặt ở đây.",
                "Chưa thiết kế liên kết bu lông, bản mã.");

        c.Sec("thiet-ke.chon-thep", "Chọn bố trí thép")
            .Steps("Chọn dầm/cột/ô sàn/móng trên mặt bằng (hoặc dòng ở tab Thiết kế), bấm **Chọn bố trí thép…** ({m:rebarChoice}).",
                "Trái: **Vùng thép** của cấu kiện (★ = đang dùng lựa chọn của người dùng). Giữa: danh sách phương án thoả As yêu cầu và khoảng hở (As yêu cầu, As bố trí…). Phải: **Mặt cắt phương án**.",
                "**Áp dụng phương án**; **Bỏ chọn vùng này (tự động)**; **Bỏ chọn cả cấu kiện**. Lựa chọn lưu vào dự án, hoàn tác được, thiết kế chạy lại.")
            .Note("Mọi phương án hiển thị đều thoả As yêu cầu và khoảng hở theo TCVN 5574:2018 mục 10.3.5.");

        c.Sec("thiet-ke.tuy-chon-thep", "Tuỳ chọn chọn thép và gia công")
            .P("{m:rebarPrefs} (hoặc nút **Tuỳ chọn…** ở tab Thiết kế → Thép):")
            .Bullets("**Chọn thép**: Ø ưu tiên thép dọc, số thanh tối đa một hàng dầm, Ø đai cố định, bước đai nhỏ nhất / lớn nhất, phương án thép sàn mặc định, ưu tiên thép tăng cường cùng Ø thép chạy suốt, cho phép thép dầm 2 hàng.",
                "**Gia công thép (shop drawing)**: chiều dài cây thép thương phẩm (vd. 11,7 m), đầu thừa tái sử dụng khi ≥ …, trừ dãn khi uốn 90° / 135° / 180°.",
                "**Nguyên tắc nối thép (không nối trong vùng nguy hiểm)**: vùng tới hạn dầm lcr = k·h (thường / kháng chấn), vùng tới hạn cột, hệ số α chiều dài nối vùng kéo / nén, khoảng so le ≥ k·ll, nối so le (≤ 50 % số thanh tại một tiết diện), ống nối ren, không nối thép móng đơn / đài cọc.")
            .Note("Đổi tuỳ chọn sẽ thiết kế lại BTCT (nếu đã có kết quả phân tích); lựa chọn thép riêng từng cấu kiện vẫn được giữ.");

        c.Sec("thiet-ke.tu-tang", "Tự tăng tiết diện đến khi đạt")
            .P("{m:autoResize}: lặp phân tích → thiết kế → tăng 50 mm cho dầm/cột không đạt, 10 mm cho sàn, đến khi mọi cấu kiện đạt hoặc hết số vòng lặp. Kiểm tra lại kiến trúc (chiều cao thông thuỷ, kích thước cột) sau khi tăng.");

        c.Sec("thiet-ke.canh-bao", "Ý nghĩa các cảnh báo và kết luận")
            .Table(new[] { "Thông báo", "Ý nghĩa", "Xử lý" },
                new[] { "Dòng tô đỏ / KHÔNG ĐẠT", "Một hạng mục kiểm tra có hệ số k > 1 (yêu cầu > khả năng).", "Xem hạng mục bất lợi nhất; tăng tiết diện, cấp bê tông, chọn thép khác." },
                new[] { "LỖI … (trong thông báo cấu kiện)", "Không bố trí được thép thoả mãn (vượt hàm lượng, không đủ chỗ, tiết diện quá bé).", "Tăng tiết diện; cho phép 2 hàng thép; đổi Ø ưu tiên." },
                new[] { "Võng vượt giới hạn", "Độ võng dài hạn lớn hơn [f].", "Tăng h dầm / chiều dày sàn, thêm dầm phụ." },
                new[] { "Áp lực đáy / lún vượt", "Móng không đủ kích thước hoặc nền yếu.", "Tăng kích thước móng, đổi loại móng, cừ tràm, móng cọc." },
                new[] { "Mô hình đã thay đổi – cần chạy lại phân tích", "Kết quả cũ đã bị xoá do sửa mô hình.", "F5 rồi thiết kế lại." },
                new[] { "Dự án trích dẫn văn bản đã hết hiệu lực", "Ghi chú / thuyết minh còn trích văn bản cũ.", "{m:regs} → Kiểm tra dự án." });
        return c;
    }

    // ==================================================================
    // 10. TRIỂN KHAI HỒ SƠ
    // ==================================================================
    private static GuideSection ChDocs()
    {
        var c = Ch("ho-so", "Triển khai hồ sơ");

        c.Sec("ho-so.ban-ve", "Bộ bản vẽ DXF KC-01 … KC-08")
            .P("{m:expDxf}: chọn kiểu xuất, thư mục, xem/đổi tên layer → chương trình vẽ bộ bản vẽ (tag thép, bảng thống kê kiểu Kết cấu Soft, ghi chú chung theo danh mục văn bản):")
            .Table(new[] { "Số hiệu", "File", "Nội dung" },
                new[] { "KC-01", "KC-01_MatBangKetCau.dxf", "Mặt bằng kết cấu các tầng" },
                new[] { "KC-02", "KC-02_Mong.dxf", "Mặt bằng và chi tiết móng (móng đơn, đài cọc, móng băng, lệch tâm, cừ tràm)" },
                new[] { "KC-03", "KC-03_Cot.dxf", "Chi tiết cột" },
                new[] { "KC-04.n", "KC-04_Dam_<tầng>.dxf", "Chi tiết dầm từng tầng" },
                new[] { "KC-05", "KC-05_San.dxf", "Mặt bằng thép sàn" },
                new[] { "KC-06", "KC-06_ThongKeThep.dxf", "Thống kê cốt thép" },
                new[] { "KC-07…", "KC-07_PhuongAnCatThep.dxf, KC-07.n_GiaCongThep_….dxf", "Gia công thép (shop drawing – {s:ho-so.shop})" },
                new[] { "KC-08.n", "KC-08.n_CauThang_<tên>.dxf", "Cầu thang: mặt bằng, mặt cắt, bố trí thép" })
            .Note("Bộ bản vẽ hiện dành cho kết cấu BTCT; khung thép tiền chế chưa có bản vẽ chi tiết.");

        c.Sec("ho-so.thiet-lap-ban-ve", "Thiết lập bản vẽ (tỷ lệ, cỡ chữ, kiểu kích thước)")
            .Bullets("{m:drawStyle}: tab **Tỷ lệ, cỡ chữ, chú thích** – tỷ lệ theo loại bản vẽ (mặt bằng: tự chọn 1/50 nếu mặt bằng ≤ 25 m, ngược lại 1/100; mặt cắt dầm và cột; móng; cầu thang…), cỡ chữ từng thành phần, chú thích tuỳ chọn (số hiệu phần tử nội bộ, giá trị nội lực, giá trị thiết kế, ký hiệu cao độ ▽, kích thước lưới trục).",
                "Tab **Kiểu kích thước (Dimension)**: kiểu VXT_1-25…, chữ, gạch xiên / mũi tên đặc, DIMSCALE.",
                "Lưu trong chuẩn CAD (mặc định người dùng hoặc riêng dự án) – dùng chung cho bản vẽ DXF, tờ in.");

        c.Sec("ho-so.chuan-layer", "Chuẩn layer CAD")
            .Bullets("{m:cadStd}: tab **Nhận diện layer kiến trúc** – mỗi layer bản vẽ kiến trúc được xếp vào nhóm Tường, Cột, Dầm, Sàn, Trục, Cửa, Cầu thang, Tên phòng hoặc Bỏ qua; gán trực tiếp (ưu tiên) và quy tắc theo mẫu tên (có ưu tiên, biểu thức chính quy), **Bỏ qua layer đang tắt (off)**.",
                "Tab **Layer bản vẽ kết cấu**: layer nội bộ → tên layer xuất, màu, nét in (mm), In; **Đổi tên layer hàng loạt** (tiền tố, hậu tố, tìm/thay).",
                "Nhập / xuất chuẩn (CadStandard.json) để dùng chung trong đơn vị; trước mỗi lần xuất DXF hộp **Xem/đổi tên layer trước khi xuất** cho phép đổi lần cuối.");

        c.Sec("ho-so.xuat", "Xuất gộp một file / tách rời, mẫu tên file")
            .Bullets("Hộp **Tuỳ chọn xuất**: **Kiểu xuất** Gộp 1 file (các bản vẽ đặt cạnh nhau, mỗi bản giữ tỷ lệ trong khung bao riêng có tên ở trên; PDF: nhiều trang) hoặc Tách rời từng bản vẽ (mỗi bản một file).",
                "**Tên file gộp** (mặc định {duan}_BoBanVe), **Mẫu tên từng file**: {so} số hiệu, {ten} tên bản vẽ, {duan} tên công trình, {stt} thứ tự 01, 02…, {ngay} ngày xuất. Để trống = tên mặc định (KC-01_MatBangKetCau…).",
                "**Ghi nhớ lựa chọn cho lần sau** (XuatBanVe.json).");

        c.Sec("trien-khai.hatch-dinh-dang", "Định dạng xuất DXF R12 / DXF 2018 / DWG 2018 và mẫu tô HATCH")
            .Table(new[] { "Định dạng", "Đặc điểm" },
                new[] { "DXF R12 (tương thích cũ) – mặc định", "Bộ ghi gốc của chương trình, mở được bằng mọi phần mềm CAD; mẫu tô HATCH được mô phỏng bằng LINE / SOLID; không lưu được nét in theo layer." },
                new[] { "DXF 2018 (có HATCH)", "AC1032, UTF-8: HATCH thật, LWPOLYLINE, nét in theo layer (ghi qua thư viện ACadSharp)." },
                new[] { "DWG 2018 (có HATCH)", "File .dwg AC1032 có HATCH thật (ghi qua ACadSharp)." })
            .Bullets("Chọn định dạng trong chuẩn CAD ({m:cadStd} – mặc định người dùng và theo dự án) hoặc ô **Định dạng file** ở hộp **Tuỳ chọn xuất** trước mỗi lần xuất. File chuẩn cũ không có mục này → DXF R12.",
                "Tô vật liệu mặt cắt: cửa sổ Kiểu bản vẽ → ô **Tô vật liệu (hatch) mặt cắt**: **Tự động** (bật khi xuất DXF 2018 / DWG, tắt với DXF R12), **Bật** (cả R12 – mô phỏng bằng nét), **Tắt**. Bê tông AR-CONC (dầm, cột, móng, bản thang), bê tông lót ANSI31, đất EARTH dưới móng, bậc gạch – trên layer HATCH (màu 8).",
                "Mẫu tô: SOLID, ANSI31…, mẫu kiến trúc AR-CONC, AR-SAND, EARTH, BRICK (bản rút gọn cùng dáng mẫu AutoCAD; định nghĩa nét ghi kèm trong HATCH nên mọi phần mềm CAD hiển thị giống nhau).",
                "Phần mở rộng file theo định dạng: .dwg cho DWG 2018, .dxf cho hai loại còn lại.");

        c.Sec("ho-so.dan-trang", "Dàn trang và in bản vẽ")
            .Steps("{m:sheets}: cửa sổ **Dàn trang & in bản vẽ** – danh sách tờ bên trái (Thêm, Trang trắng, Tạo tờ từ mẫu, Lưu tờ làm mẫu, Nhân bản, Xoá, ▲▼, Đánh số lại, **Tạo bộ bản vẽ mặc định**).",
                "**Thuộc tính tờ**: khổ A0…A4 / tuỳ chỉnh, ngang/dọc, khung viền, khung tên, **Khung tên công trình…** (thông tin dùng chung cho mọi tờ, logo, người thiết kế / kiểm tra / duyệt).",
                "**Thêm khung nhìn** (mặt bằng, mặt đứng, mặt cắt, bảng…) → **Thêm vào tờ**; kéo / đổi cỡ khung nhìn trên trang; **Thuộc tính khung nhìn**: tỷ lệ, vùng cắt, ẩn tên hình, viền khung, khoá, **Co vừa khung**.",
                "Đối tượng tự do: Chữ, Đường, Mũi tên, Khung, Tròn, Mây, Ảnh, Bảng, Kích thước; Chép / Dán / Nhân đôi / Xoá, ↶ ↷, Bắt lưới, Bắt đối tượng, căn hàng, phân bố đều, lên trên / xuống dưới, khoá.",
                "**In…** (chọn \"Microsoft Print to PDF\" nếu cần), **Xuất PDF…**, **Xuất DXF…** (tuỳ chọn **Chỉ các tờ đang chọn**), **Hiệu chỉnh tờ…**.")
            .P("Các tờ lưu trong dự án (.kcvxt); tờ mẫu lưu trong thư viện mẫu định dạng ({s:khoi-tao.mau}).");

        c.Sec("ho-so.khung-ten", "Khung tên người dùng, tờ mẫu")
            .Bullets("**Tạo tờ từ mẫu** → nhóm **Khung tên người dùng (tải từ DWG / DXF / ảnh)** → **Tải khung tên từ file (DWG/DXF/ảnh)…** mở hộp thoại **Khung tên người dùng**.",
                "Tab **Chữ trong file → trường**: chữ có chỗ trống {TEN_CONG_TRINH}, {SO_HIEU}… hoặc thẻ ATTRIB/ATTDEF trùng tên trường được nhận tự động; nhấp vào chữ trên hình để gán trường. Tab **Chữ trường đặt thêm**: thêm trường tại toạ độ mm.",
                "Tuỳ chọn: điểm gốc, đơn vị, **Lấy miền tô (viền)**, **Vẽ khung viền trong của chương trình**, ô logo, **Xem với giá trị của dự án / tờ hiện tại** → **Đồng ý – dùng khung tên này**; **Dùng khung tên mặc định của chương trình** để quay lại.");

        c.Sec("ho-so.hieu-chinh", "Hiệu chỉnh bản vẽ sau khi xuất")
            .Bullets("{m:drawEdit}: mở một bản vẽ tự sinh (bộ KC-xx, gia công thép, tờ dàn trang), chọn thực thể (chữ, nhãn thép, kích thước, nét; **Chọn cả nhóm**), kéo dời, **Sửa chữ… (F2)**, **Xoá (Del)**, **Lớp / màu…**, thêm chữ / mũi tên / đường / mây / khung; ↶ ↷.",
                "Chỉnh sửa lưu vào dự án và **được áp lại sau mỗi lần dựng lại / xuất**; chỉnh sửa không còn khớp (do mô hình đổi) liệt kê ở mục **Chỉnh sửa không còn áp dụng được** – **Xoá các chỉnh sửa này** hoặc **Khôi phục bản gốc**.",
                "{m:drawAttach}: khoá bản vẽ bằng file đã sửa trong AutoCAD – các lần xuất / in sau dùng file này tới khi **Bỏ khoá**. **Xuất DXF bản vẽ này…** để xuất riêng.")
            .Warn("Bản vẽ đã khoá không cập nhật theo mô hình. Sau khi sửa mô hình, bỏ khoá rồi sửa lại trong CAD nếu cần.");

        c.Sec("ho-so.shop", "Shop drawing thép (gia công, nối thép, vùng cấm nối)")
            .Bullets("{m:shop} (hoặc nút ở tab Thiết kế → Thép): bảng gia công DXF theo loại cấu kiện (KC-07.n), phương án cắt và tổng hợp (sơ đồ cắt cây 11,7 m – KC-07) và bảng Excel \"Gia công thép\".",
                "Nối thép theo nguyên tắc ở {m:rebarPrefs}: không nối trong vùng tới hạn dầm (lcr = k·h) và cột, chiều dài nối theo α vùng kéo / nén, nối so le, ống nối ren khi vùng nối cho phép ngắn hơn ll; móng đơn / đài cọc dùng thanh nguyên.",
                "Chiều dài cắt có thể trừ độ dãn dài khi uốn; đầu thừa đủ dài được tái sử dụng.");

        c.Sec("ho-so.khoi-luong", "Bảng khối lượng vật tư")
            .Bullets("{m:quantities}: khối lượng bê tông, cốt thép, ván khuôn, khối xây… theo cấu kiện và tầng; **Tính lại**, **Xuất Excel…**, **Xuất Word…**.",
                "Tab **Định mức**: cấp phối, định mức vật liệu; **Lấy danh mục vật tư từ bảng**, **Khôi phục định mức mặc định**, **Nhập định mức từ Excel…** (cột: mã hiệu, tên, đơn vị, vật liệu, định mức), **Xuất file mẫu định mức…**.")
            .Warn("Cấp phối mặc định là **THAM KHẢO** (số liệu hệ định mức cũ). Nguồn ghi trên bảng: " + Std.Norms + " – đối chiếu và nhập lại theo định mức hiện hành; bảng khối lượng không thay thế dự toán.");

        c.Sec("ho-so.thuyet-minh", "Thuyết minh tính toán (Word)")
            .Bullets("{m:expWord}: thuyết minh đầy đủ theo mặc định.",
                "{m:autoReport}: hộp **Lập thuyết minh tính toán tự động** – chọn chương (giới thiệu công trình; căn cứ pháp lý và tiêu chuẩn; vật liệu, tiết diện; sơ đồ tính, tải trọng, tổ hợp; gió, động đất; kết quả nội lực; thiết kế dầm, cột, sàn, cầu thang, móng; địa chất; thống kê cốt thép, kết cấu thép; khối lượng vật tư; phụ lục công thức). Luôn có: trang bìa, mục lục, chương KẾT LUẬN VÀ KIẾN NGHỊ, phụ lục cảnh báo.",
                "**Thông tin trang bìa**: tên công trình, chủ đầu tư, địa điểm, đơn vị thiết kế, chủ trì thiết kế, người lập, kiểm tra, ngày lập.",
                "**Tuỳ chọn**: số ví dụ tính toán chi tiết cho mỗi loại cấu kiện, bảng nội lực bao từng thanh, mục lục tự động, đánh số trang, đánh số tên bảng (Bảng n.k). Font theo chuẩn CAD/thuyết minh.")
            .Tip("Mở file bằng Word, chọn **Yes** khi được hỏi cập nhật trường để có mục lục và số trang đúng (hoặc Ctrl+A → F9).");

        c.Sec("ho-so.bang-tinh", "Bảng tính Excel, nội lực CSV, mô hình ETABS/SAP")
            .Bullets("{m:expExcel}: các sheet Thông tin, Tải trọng, Nội lực, Phản lực, Chuyển vị, Lực ngang tầng, Dao động riêng, Dầm, Cột, Sàn, Móng, Đài cọc, Móng băng, Thép, Thống kê thép, Gia công thép, Cảnh báo, Phụ lục.",
                "{m:csv}: bảng nội lực, phản lực, chuyển vị dạng ETABS (dùng cho kiểm tra chéo hoặc phần mềm khác).",
                "{m:e2k}: sàn xuất là phần tử Membrane kèm tải sàn (ETABS tự dồn tải – kiểm tra cả bước dồn tải). Mở trong ETABS: File > Import > ETABS .e2k Text File. Tên thanh C{Id} (cột), B{Id} (dầm), D{Id} (xiên).",
                "{m:s2k}: mô hình giống hệt (tải sàn đã dồn lên dầm, Diaphragm, thanh vát chia đoạn) – nội lực phải trùng gần như tuyệt đối. Mở: File > Import > SAP2000 .s2k Text File.");

        c.Sec("ho-so.xuat-tat-ca", "Xuất tất cả vào thư mục")
            .P("{m:expAll} (nút **Xuất tất cả**): chọn thư mục gốc → tự phân tích, thiết kế nếu cần → chọn kiểu xuất bản vẽ → xem/đổi layer → tạo thư mục <tên công trình>_<yyyyMMdd_HHmm> gồm: MoHinh.kcvxt, BanVe\\ (bộ DXF), GiaCongThep\\, ThuyetMinh.docx, BangTinh.xlsx, bảng khối lượng, NoiLuc\\ (CSV), MoHinh.e2k, MoHinh.s2k. Hạng mục lỗi được liệt kê, các hạng mục khác vẫn xuất.");

        c.Sec("ho-so.phu-luc", "Phụ lục tính toán")
            .P("{m:appendix} (Shift+F1): mọi công thức, chỉ số, hệ số, hệ số an toàn và giải thích dễ hiểu của các phép tính chương trình thực sự dùng (PL1…PLn), bảng tra tính lại bằng chính hàm của chương trình (cường độ vật liệu, k(z), phổ động đất, …), có giá trị của công trình đang mở. Ô tìm kiếm không dấu, **Xuất Word…**, **In…**.");
        return c;
    }

    // ==================================================================
    // 11. KIỂM CHỨNG
    // ==================================================================
    private static GuideSection ChVerify()
    {
        var c = Ch("kiem-chung", "Kiểm chứng và kiểm tra chéo");
        c.Sec("kiem-chung.bao-cao", "Báo cáo kiểm chứng phần mềm")
            .Steps("{m:verify}: bấm **Chạy kiểm chứng** – chương trình chạy bộ bài toán chuẩn (phân tích, thiết kế, móng) và so với lời giải tham chiếu.",
                "Bảng: mã, bài toán, đại lượng, tham chiếu, Việt Const, sai số %, ngưỡng %, kết luận. Dòng tóm tắt: số bài toán, số đạt / không đạt, sai số lớn nhất.",
                "**Xuất báo cáo Word + Excel…**: báo cáo gồm 1. Mục đích và phương pháp, 2. Phạm vi, 3. Môi trường và phiên bản, **4. Kết quả kiểm chứng** (tự kiểm chứng), **5. Kiểm chứng độc lập**, 6. Kết luận, 7. Giới hạn đã biết, Phụ lục A lời giải tham chiếu tự lập.");

        c.Sec("kiem-chung.doc-lap", "Kiểm chứng độc lập (mục 5 của báo cáo)")
            .Bullets("**Mục 4 – Tự kiểm chứng**: tham chiếu là lời giải giải tích / tính tay độc lập theo công thức tiêu chuẩn.",
                "**Mục 5 – Kiểm chứng độc lập**: nhập ở các cột màu vàng – giá trị theo tài liệu / bên thứ ba, nguồn tài liệu (tên, trang/ví dụ), loại nguồn, người/đơn vị xác nhận, ngày; ngưỡng so với tài liệu = max(ngưỡng; 3 %).",
                "**Lưu giá trị kiểm chứng độc lập** (KiemChung.json); hoặc gửi file Excel báo cáo cho bên kiểm chứng điền rồi **Nhập kết quả kiểm chứng độc lập từ Excel…**.");

        c.Sec("kiem-chung.etabs", "Kiểm tra chéo với ETABS / SAP2000")
            .Bullets("SAP2000 (.s2k): mô hình giống hệt – nội lực phải trùng gần như tuyệt đối; dùng để kiểm tra lời giải khung.",
                "ETABS (.e2k): kiểm tra cả bước dồn tải sàn (ETABS tự dồn từ phần tử Membrane).",
                "So sánh tự động: {s:phan-tich.so-sanh}; nguyên nhân lệch: {s:hoi-dap.lech-etabs}.");

        c.Sec("kiem-chung.trach-nhiem", "Trách nhiệm của người dùng")
            .P("Báo cáo kiểm chứng chỉ áp dụng cho phạm vi bài toán đã kiểm chứng và giả thiết của từng bài toán; không thay thế việc kỹ sư chịu trách nhiệm kiểm tra kết quả thiết kế, đặc biệt hệ số tải trọng, điều kiện địa chất và các giới hạn nêu ở {s:gioi-thieu.gioi-han}.");
        return c;
    }

    // ==================================================================
    // 12. VĂN BẢN PHÁP LÝ
    // ==================================================================
    private static GuideSection ChLegal()
    {
        var c = Ch("phap-ly", "Văn bản pháp lý và tiêu chuẩn");
        c.Sec("phap-ly.danh-muc", "Danh mục văn bản")
            .Bullets("{m:regs}: danh mục văn bản (luật, nghị định, thông tư, quy chuẩn, tiêu chuẩn) lọc theo **Phạm vi**, **Chỉ văn bản đang áp dụng**, màu theo trạng thái; phần **Chi tiết**: thay thế / được thay thế, ghi chú, cần kiểm tra, nguồn.",
                "Thuyết minh (chương CĂN CỨ PHÁP LÝ VÀ TIÊU CHUẨN ÁP DỤNG), phụ lục, ghi chú chung bản vẽ và bảng khối lượng trích dẫn theo danh mục này qua \"chủ đề\" (vd. tai-trong, btct, dong-dat, mong-coc).",
                "Danh mục có sẵn chỉ gồm văn bản đã kiểm tra nguồn; mục ghi \"kiểm tra\" cần người dùng đối chiếu văn bản gốc.");

        c.Sec("phap-ly.cap-nhat", "Cập nhật danh mục")
            .Bullets("**＋ Thêm…**, **Sửa…**, **Xoá**, **Đánh dấu hết hiệu lực…** (ghi văn bản thay thế).",
                "**Nhập JSON… / Xuất JSON…** để chia sẻ; **Lưu làm mặc định** (%AppData%\\KetCauVXT\\VanBanPhapLy.json); **Khôi phục danh mục có sẵn**.",
                "**Kiểm tra dự án**: dò các ghi chú, thuyết minh của dự án còn trích dẫn văn bản cũ. Khi mở dự án, thanh trạng thái cảnh báo nếu dự án trích văn bản đã hết hiệu lực.");
        return c;
    }

    // ==================================================================
    // 13. HỎI ĐÁP
    // ==================================================================
    private static GuideSection ChFaq()
    {
        var c = Ch("hoi-dap", "Câu hỏi thường gặp và xử lý lỗi");
        c.Sec("hoi-dap.dwg", "Không đọc được file DWG")
            .Bullets("Chương trình đọc DWG AutoCAD R14 … 2018+. Bản vẽ lưu bằng phiên bản mới hơn hoặc lỗi: trong AutoCAD dùng SAVEAS → DXF (hoặc DWG 2018) rồi nhập lại.",
                "Cài ODA File Converter (miễn phí, opendesign.com) – chương trình tự dùng khi đọc trực tiếp lỗi.",
                "Bản vẽ dùng đơn vị lạ: chọn **Đơn vị bản vẽ** (mm / cm / m) thay vì \"Theo file\".",
                "Không nhận được tường/trục: kiểm tra tên layer và quy tắc ở {m:cadStd}; dùng tab **Layer trong file** để gán tay.");

        c.Sec("hoi-dap.smartscreen", "Windows chặn bộ cài / chương trình")
            .P("Xem {s:cai-dat.smartscreen}. Bộ cài báo thiếu đĩa: tải đủ mọi file VietConst-Setup-*.bin vào cùng thư mục với VietConst-Setup.exe.");

        c.Sec("hoi-dap.lech-etabs", "Kết quả lệch so với ETABS – nguyên nhân")
            .Bullets("ETABS mặc định có **vùng cứng đầu thanh** (End length offsets) và có thể đã gán **hệ số giảm độ cứng** (Property modifiers): tắt hoặc khai báo giống nhau ở hai bên.",
                "Dồn tải sàn: .e2k xuất sàn Membrane để ETABS tự dồn – khác cách dồn 45° của chương trình; dùng .s2k (tải đã dồn) để so riêng lời giải khung.",
                "Sàn cứng (diaphragm), liên kết chân cột, trọng lượng bản thân (hệ số TT), γf và tên tổ hợp phải giống nhau.",
                "Dầm cong: chương trình báo nội lực theo hệ trục tiếp tuyến cung, ETABS/SAP theo trục từng đoạn → lệch nhỏ (cỡ sin 5°) giữa M3 và T ở đầu đoạn.",
                "Phổ phản ứng: DDX/DDY không dấu; so sánh theo cặp bao +/−.",
                "Đơn vị bảng xuất từ ETABS: chọn đúng **Đơn vị khi bảng không ghi đơn vị** trong cửa sổ so sánh.");

        c.Sec("hoi-dap.khong-on-dinh", "Mô hình không ổn định / không phân tích được")
            .P("Xem {s:phan-tich.on-dinh}.");

        c.Sec("hoi-dap.khong-dat", "Nhiều cấu kiện không đạt")
            .Bullets("Kiểm tra tải: tải tường có bị gán trùng (tường biên + tường khai báo)? Hoạt tải công năng đúng chưa?",
                "Kiểm tra tiết diện mặc định quá nhỏ so với nhịp; dùng {m:autoResize}.",
                "Kiểm tra cấp bê tông, nhóm thép ở tab Công trình.");

        c.Sec("hoi-dap.mat-sua", "Mất các sửa đổi trên mặt bằng")
            .P("Bấm **Tạo mô hình** ở tab Công trình sinh lại toàn bộ mô hình. Dùng Ctrl+Z ngay sau đó, hoặc mở lại file đã lưu. Chỉ đổi thông số thì bấm **Áp dụng thông số**.");

        c.Sec("hoi-dap.ket-qua-nhap", "Mở lại dự án mất kết quả nhập từ ETABS")
            .P("Kết quả nhập không lưu trong file .kcvxt (giới hạn đã biết). Nhập lại bằng {m:importResults} sau khi mở dự án.");

        c.Sec("hoi-dap.muc-luc", "Thuyết minh Word không có số trang / mục lục trống")
            .P("Mục lục và số trang là trường (field) – Word cập nhật khi mở nếu chọn Yes; hoặc Ctrl+A → F9. LibreOffice: Tools → Update → Update All.");

        c.Sec("hoi-dap.autocad", "AutoCAD không thấy lệnh VXT")
            .Bullets("Kiểm tra đã cài add-in (tuỳ chọn khi cài, hoặc CaiDatAutoCAD.cmd) và chọn Always Load khi AutoCAD hỏi.",
                "AutoCAD 2027 chưa được hỗ trợ (dùng .NET 10). Thử NETLOAD DLL đúng năm trong thư mục AutoCAD\\KetCauVXT.bundle\\Contents\\<năm>.",
                "VXT_GUI không tự mở chương trình: mở KetCauVXT.exe một lần để ghi app_path.txt.");
        return c;
    }

    // ==================================================================
    // 14. PHỤ LỤC
    // ==================================================================
    private static GuideSection ChAppendix()
    {
        var c = Ch("phu-luc", "Phụ lục");
        c.Sec("phu-luc.phim-tat", "Phím tắt và thao tác chuột")
            .Table(new[] { "Phím / chuột", "Tác dụng" },
                new[] { "Ctrl+N / Ctrl+O / Ctrl+S", "Dự án mới / mở / lưu" },
                new[] { "F5", "Chạy phân tích" },
                new[] { "F1", "Hướng dẫn sử dụng tại mục của ngữ cảnh hiện tại (tab, khung nhìn, công cụ, hộp thoại)" },
                new[] { "Shift+F1", "Phụ lục tính toán" },
                new[] { "Ctrl+Z", "Hoàn tác thao tác sửa mô hình (30 bước)" },
                new[] { "Ctrl+E", "Hiện / ẩn cây mô hình" },
                new[] { "Ctrl+T", "Bảng kết quả (tự phân tích nếu cần)" },
                new[] { "Ctrl+A", "Chọn mọi thanh tầng đang xem (khi đang ở khung nhìn Mặt bằng, không gõ trong ô nhập)" },
                new[] { "Delete", "Xoá đối tượng đang chọn trên mặt bằng" },
                new[] { "Esc", "Dừng công cụ vẽ, về chế độ Chọn; đóng hộp thoại" },
                new[] { "Chuột phải (khi vẽ dầm)", "Kết thúc chuỗi vẽ; lần nữa: thoát công cụ" },
                new[] { "Gõ số khi vẽ dầm", "Ô nhập toạ độ: x,y | @dx,dy | L<góc | L" },
                new[] { "Con lăn chuột", "Thu phóng mặt bằng, 3D, biểu đồ, mặt bằng móng" },
                new[] { "Kéo chuột trái (3D)", "Xoay mô hình" },
                new[] { "Shift + kéo / chuột giữa (công cụ dầm)", "Di chuyển mặt bằng" },
                new[] { "Ctrl+F (phụ lục, hướng dẫn)", "Tìm kiếm" },
                new[] { "F2 (hiệu chỉnh bản vẽ)", "Sửa chữ" });

        c.Sec("phu-luc.dinh-dang", "Định dạng file")
            .Table(new[] { "Phần mở rộng", "Nội dung" },
                new[] { ".kcvxt", "Dự án (JSON UTF-8): mô hình, tải, tổ hợp, địa chất, móng, lựa chọn thép, tờ dàn trang, chỉnh sửa bản vẽ, chuẩn CAD riêng dự án." },
                new[] { ".kcvxt-mau", "Mẫu định dạng (chuẩn CAD, khung tên, tờ mẫu, thuyết minh, định mức, tuỳ chọn thép)." },
                new[] { ".dwg / .dxf", "Nhập: bản vẽ kiến trúc, mặt bằng kết cấu, khung tên. Xuất: bộ bản vẽ DXF." },
                new[] { "acad_*.json", "Dữ liệu trao đổi từ add-in AutoCAD (lược đồ Version 1, docs/acad-link.md)." },
                new[] { "*.vxtrevit.json", "Mô hình xuất từ add-in Revit." },
                new[] { ".e2k / .$et, .s2k / .$2k", "Mô hình ETABS / SAP2000 dạng văn bản (nhập và xuất)." },
                new[] { ".xlsx / .csv / .txt", "Bảng kết quả ETABS/SAP/SAFE (nhập, so sánh); danh mục địa danh; định mức; báo cáo kiểm chứng độc lập." },
                new[] { ".docx / .xlsx", "Thuyết minh, phụ lục, báo cáo kiểm chứng, bảng tính, bảng khối lượng, hướng dẫn sử dụng." },
                new[] { ".json", "Chuẩn layer CAD, danh mục văn bản pháp lý (nhập/xuất)." });

        c.Sec("phu-luc.thu-muc", "Thư mục dữ liệu")
            .Bullets("Chương trình: %LocalAppData%\\Programs\\KetCauVXT (cài cho một người) hoặc Program Files\\KetCauVXT (mọi người dùng); dự án mẫu ở DuAnMau; HuongDanSuDung.docx ở thư mục cài.",
                "Thiết lập người dùng: %AppData%\\KetCauVXT ({s:cai-dat.du-lieu}).",
                "Add-in AutoCAD: %AppData%\\Autodesk\\ApplicationPlugins\\KetCauVXT.bundle (hoặc ProgramData khi cài cho mọi người).");

        c.Sec("phu-luc.dong-lenh", "Dòng lệnh")
            .Table(new[] { "Lệnh", "Tác dụng" },
                new[] { "KetCauVXT.exe <file.kcvxt>", "Mở dự án." },
                new[] { "KetCauVXT.exe --acad \"<file.json>\"", "Mở hộp thoại nhập với dữ liệu gửi từ AutoCAD." },
                new[] { "KetCauVXT.exe --selftest <file.kcvxt> <thư mục>", "Tự kiểm tra giao diện: mở dự án, phân tích, thiết kế, mở các cửa sổ và chụp ảnh vào thư mục (CI dùng mỗi lần build)." });

        var m = c.Sec("phu-luc.menu", "Bảng đường dẫn menu được nhắc trong tài liệu");
        m.P("Mục [SẮP CÓ] là menu dự kiến của tính năng đang phát triển.");
        m.Table(new[] { "Mã", "Đường dẫn" }, Menus.Select(x => new[] { x.Id, x.Display + (x.Planned ? " [SẮP CÓ]" : "") }).ToArray());
        return c;
    }
}
