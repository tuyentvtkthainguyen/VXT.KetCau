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
            .P("Bộ cài là **một file duy nhất VietConst-Setup.exe**: tải về rồi chạy.")
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
            .Tip("Nếu trình duyệt cảnh báo khi tải, chọn \"Giữ lại\" (Keep). Có thể bấm chuột phải file → Properties → tích Unblock trước khi chạy.");

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
            .Bullets("Nút **⟳ Cập nhật** (cuối thanh công cụ, màn hình chọn chế độ) hoặc **Trợ giúp → Kiểm tra & tải bản cập nhật…**: so phiên bản đang dùng với bản mới nhất trên GitHub, "
                + "bấm **TẢI VỀ & CÀI ĐẶT** để tải bộ cài (có thanh tiến trình, kiểm tra SHA-256) rồi chạy. Khi có bản mới, nút đổi thành **⬆ Có bản mới …** màu cam. "
                + "Chỉ tải file công khai, không gửi dữ liệu người dùng; bỏ tích **Tự kiểm tra bản mới mỗi khi mở chương trình** để tắt kiểm tra tự động.",
                "Trong AutoCAD: nút **⟳ Cập nhật Việt Const** trên bảng lệnh VXT (lệnh VXT_CAPNHAT) mở cửa sổ cập nhật. Lưu bản vẽ và đóng AutoCAD trước khi chạy bộ cài để thay được add-in.",
                "Tải bộ cài mới và chạy đè lên bản cũ: bộ cài nhớ thư mục và các tuỳ chọn đã chọn lần trước.",
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
                new[] { "BatDauNhanh.json", "Tuỳ chọn \"Không hiện lại\" của bảng Bắt đầu nhanh." },
                new[] { "TuKiemTraCapNhat.txt", "Bật / tắt tự kiểm tra bản mới khi mở chương trình (0 = tắt)." })
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
                new[] { "Công trình", "Thông tin công trình; lưới trục và tầng; tiết diện mặc định; địa điểm xây dựng (vùng gió, động đất); vật liệu; nền đất dưới móng đơn; nút **Tạo mô hình**, **Áp dụng thông số**.", "{s:khoi-tao.luoi-truc}" },
                new[] { "Địa chất", "Hố khoan khảo sát, các lớp đất, hình trụ hố khoan, áp dụng cho thiết kế móng, gợi ý phương án móng.", "{s:mong.dia-chat}" },
                new[] { "Tiết diện", "Bảng tiết diện thanh (chữ nhật BTCT, chữ I tổ hợp) và tiết diện sàn.", "{s:mo-hinh.tiet-dien}" },
                new[] { "Tải trọng", "Mọi lệnh gán tải ở một chỗ: gán tải theo cấu kiện, tải thanh / sàn / nút, tường, cấu tạo sàn, sàn vệ sinh; trường hợp tải, tổ hợp, phổ động đất; tải mặc định, gió, động đất; bảng tải trên thanh, trên sàn.", "{s:tai-trong.truong-hop}" },
                new[] { "Kiểm tra", "Mọi lệnh kiểm tra (kiểm tra mô hình, phân tích, biến dạng, biểu đồ, phản lực, bảng kết quả, kiểm tra BTCT / thép / an toàn móng, so sánh ETABS); nội lực theo trường hợp/tổ hợp và tầng, phản lực chân cột, cảnh báo.", "{s:phan-tich.ket-qua}" },
                new[] { "Thiết kế", "Các tab con Dầm, Cột, Sàn, Móng, Cầu thang, Thép; cấu kiện không đạt tô đỏ.", "{s:thiet-ke.btct}" },
                new[] { "Triển khai", "Mọi lệnh triển khai hồ sơ: dàn trang & in, bản vẽ DXF, bản vẽ từng dầm, hiệu chỉnh bản vẽ, chọn bố trí thép, shop drawing, thuyết minh Word, bảng tính Excel, khối lượng, xuất tất cả, chuẩn layer CAD. Ribbon kiểu Revit có cùng ba tab Tải trọng, Kiểm tra, Triển khai.", "{s:ho-so.xuat}" });

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

        c.Sec("giao-dien.giao-dien-gon", "Giao diện gọn – mở rộng vùng làm việc")
            .Bullets("Menu và thanh công cụ nằm chung **một hàng mảnh** (~28 px) ở cả chế độ Thiết kế, Triển khai hồ sơ và cửa sổ Dàn trang in. Nút lệnh hiện dạng **biểu tượng**; rê chuột để xem tên lệnh, phím tắt và giải thích. Lệnh không đủ chỗ tự vào nút **»** ở cuối thanh công cụ.",
                "Nút **☰** (đầu hàng): ẩn / hiện thanh menu. Khi menu đang ẩn, nhấn **Alt** để hiện tạm thời (như các phần mềm Windows), chọn lệnh xong menu tự ẩn lại. **Ctrl+F1**: ẩn / hiện thanh công cụ (như ribbon Office).",
                "Chế độ Triển khai hồ sơ: các nút thêm cấu kiện (móng đơn, móng băng, dầm, cột, sàn…) gộp vào một nút **＋ ▾** – bấm để chọn loại.",
                "Khung bên (**Nhập liệu – kết quả** của chế độ Thiết kế; **Cấu kiện hồ sơ**, **Thông số cấu kiện** của Triển khai hồ sơ; **Các tờ in**, **Thuộc tính tờ** của Dàn trang in) có nút **«** nhỏ ở góc trên: bấm để thu khung thành dải hẹp có chữ dọc; bấm dải để mở lại. Kéo thanh chia để đổi độ rộng – phần mềm nhớ độ rộng và trạng thái thu gọn cho lần mở sau.",
                "**F11** hoặc nút **⛶** (cuối hàng): **mở rộng vùng làm việc** – ẩn menu, thanh công cụ, thanh trạng thái, các khung bên, cây mô hình và bảng thông tin dưới mặt bằng; chỉ còn khung vẽ / xem trước và một thanh nổi nhỏ góc trên phải (khôi phục, tìm lệnh, vừa màn hình, phân tích / tính toàn bộ). **Esc** hoặc **F11** để khôi phục.",
                "**Ctrl+Q** (hoặc **Ctrl+Shift+P**, nút **🔍**): **tìm lệnh nhanh** – gõ tên lệnh, không cần dấu (vd. \"mat bang\", \"xuat\", \"luu\"), gõ chữ đầu cũng được (\"mbkc\" → Mặt bằng kết cấu); ↑ ↓ chọn, **Enter** chạy, **Esc** đóng. Danh sách gồm mọi mục menu và nút công cụ, kể cả khi menu đang ẩn.",
                "Nút **⋯** (cuối hàng): bật / tắt menu, thanh công cụ, từng khung bên; **Giao diện gọn** (mặc định – nút biểu tượng, chữ khung 12, thanh trạng thái 1 dòng) hoặc **Giao diện tiêu chuẩn** (biểu tượng kèm chữ) – áp dụng khi mở lại cửa sổ; **Khôi phục bố cục mặc định**.",
                "**Chọn / bỏ chọn tất cả layer**: mọi danh sách layer có ô tích (bảng layer của **Link CAD**, layer xuất **In** và quy tắc **Dùng** trong Chuẩn layer CAD, **Layer trục** và **Hiển thị nét** khi mở mặt bằng CAD, thẻ trong **Hiển thị / Đồ hoạ**) có hàng **☐ Tất cả (n/N) · ☑ Chọn hết · ☐ Bỏ hết · ⇄ Đảo**. Ô **Tất cả** ba trạng thái: tích hết / không ô nào / một phần (■); bấm khi đang tích hết → bỏ hết, còn lại → tích hết. **Ctrl+A** trong danh sách ô tích = bấm ô Tất cả. Chỉ áp cho các dòng đang hiện (sau lọc); cả lô được áp và vẽ lại **một lần** nên nhanh cả với hàng trăm layer.",
                "Bố cục chỉ lưu trên máy này, trong file %AppData%\\VietConst\\ui.json (độ rộng khung, khung thu gọn, menu / thanh công cụ ẩn, mật độ giao diện) – không gửi đi đâu, không chứa dữ liệu dự án. Xoá file để trở về mặc định.");

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
                + "Nhập M nhịp, M gối, Q (tuỳ chọn) → kiểm tra M ≤ Mu, Q ≤ Qb + Qsw, dải nén 0,3Rb·b·h0 (TCVN 5574:2018 mục 8.1). Luôn kiểm tra cấu tạo: μ ≥ 0,1 %, bước đai, khe hở thép 1 lớp. "
                + "Mỗi nhóm thép nhập trên **một dòng**: Ø × số thanh × hàng; thép chạy suốt chọn **bẻ mỏ neo vào gối** hoặc **neo thẳng** (cảnh báo khi đoạn neo thẳng < 10d); "
                + "tối đa 3 nhóm tăng cường dưới (2)(3)(4) cắt tại 0,1·L0 / L0/7 / L0/8 từ mép gối hoặc kéo vào gối, có / không bẻ mỏ; tối đa 3 nhóm mũ gối vươn L0/3 (gối giữa) – L0/4 (gối biên) / L0/4 / L0/5 / qua hết nhịp ngắn; "
                + "nhóm đặt hàng 2 (hoặc hàng 1 không đủ khe hở) → h0 tính theo trọng tâm thép, Mu kể đủ các nhóm.",
                "**Cột**: b × h, chiều cao các tầng, cao độ chân, chiều cao dầm tại nút; Ø, số thanh mỗi cạnh, đai đầu / giữa cột. Nhập N, Mx, My tầng dưới → kiểm tra nén lệch tâm xiên (Bresler) có kể độ lệch tâm ngẫu nhiên; "
                + "vị trí nối thép: **tự gợi ý** (giữa chiều cao thông thuỷ, ngoài vùng nguy hiểm lcr = max(hc; H/6; 450 mm), vùng gợi ý ghi trong thuyết minh), **nối trên mặt sàn** (nhà thấp tầng) hoặc **ống nối ren**; "
                + "tuỳ chọn so le 50 % số thanh (≥ 1,3·ll) và **nhấn cổ chai 1:6** đầu thanh tầng dưới (thể hiện trên mặt đứng cột và bảng thống kê).",
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

        c.Sec("che-do-ho-so.lanh-to", "Lanh tô cửa, ô văng cửa")
            .Bullets("＋ Lanh tô: chọn **Lanh tô**, **Lanh tô + ô văng liền khối** hoặc **Ô văng** (console từ dầm / lanh tô có sẵn); nhập bề rộng lỗ cửa, chiều dày tường, chiều cao lanh tô (0 = tự chọn), đoạn gối mỗi bên (≥ 200 mm).",
                "Tải tường phía trên: khi tường cao ≥ Lo/3 và không có sàn tựa trong phạm vi lỗ cửa chỉ lấy tường cao Lo/3 (hiệu ứng vòm); có sàn / dầm tựa thì cộng tải đó.",
                "Ô văng: chiều vươn, chiều dày gốc / mút, gờ chắn nước; tải sửa chữa phân bố hoặc lực tập trung ở mút; thép chịu lực lớp trên neo vào lanh tô. Kiểm tra uốn, cắt lanh tô, **xoắn** lanh tô do ô văng, "
                + "**chống lật** khi lanh tô không liền cột / giằng tường.",
                "Bản vẽ: mặt đứng lanh tô và mặt cắt 1-1 (lanh tô + ô văng, dốc thoát nước, gờ chắn, rãnh nhỏ giọt).",
                "**Tường 110** (cửa đi các phòng, cửa đi lớn): chương trình tự chọn h ≥ max(Lo/10; 150 mm), Lo > 1,5 m lấy ≥ 200 mm; gối (ngàm) vào tường mỗi bên ≥ 200 mm (≥ 250 mm khi Lo > 1,5 m); "
                + "kiểm tra **nén cục bộ khối xây** dưới gối (nhập cường độ khối xây R, mặc định 1,3 MPa – gạch đặc M75, vữa M50); cửa rộng > 1,8 m: gợi ý **bổ trụ BTCT 110×220** hai bên ô cửa hoặc dùng dầm BTCT gác lên cột.");

        c.Sec("che-do-ho-so.mat-bang-ve", "Vẽ mặt bằng kết cấu, nhân bản tầng, ánh xạ kiến trúc → lanh tô các tầng")
            .Bullets("Tab **Mặt bằng kết cấu**: đặt cột, móng, dầm (2 điểm), sàn, lanh tô, ô văng, cầu thang; **bắt điểm** tâm cột, đầu dầm, giao trục, điểm trên tim dầm, đường trục; giữ **Shift** để vẽ vuông góc; "
                + "**Sàn trong ô dầm**: bấm vào bên trong ô được bao bởi dầm (hoặc trục); **Di chuyển**: bấm cấu kiện rồi bấm điểm đích; **↶ Hoàn tác** (Ctrl+Z).",
                "**⧉ Nhân bản tầng…**: sao toàn bộ cấu kiện của mặt bằng đang xem sang các tầng giống nhau về bố trí công năng (số tầng, chiều cao tầng). "
                + "Chế độ **A – dùng chung cấu kiện**: cùng tên D1, S1…, số lượng tự nhân theo số tầng (thống kê thép, bê tông, ván khuôn nhân đúng); "
                + "chế độ **B – tạo cấu kiện mới**: D1 → D1-T3… để sửa riêng từng tầng. Cột luôn dùng chung và được thêm một tầng vào \"Chiều cao các tầng\"; đường cắt mặt cắt sàn được sao theo.",
                "**Nhập MB kết cấu từ CAD…**: mặt bằng kết cấu vẽ trong AutoCAD (DXF/DWG) – dầm vẽ 2 nét song song (hoặc 1 nét tim) trên layer chứa DAM / BEAM / GIANG, cột là đa tuyến kín trên layer COT / COLUMN, "
                + "chữ tên cạnh cấu kiện \"D1 220x400\", \"C1 (220x220)\", chữ S1 trong ô sàn. Tên đã có trong hồ sơ → gán vị trí; chưa có → tạo cấu kiện mới với tiết diện đọc từ chữ. "
                + "Trong AutoCAD có thể gõ **VXT_MB_KC** (nút **Gửi mặt bằng kết cấu → Triển khai hồ sơ**), chọn đối tượng – Việt Const tự hỏi nhận vào mặt bằng đang xem.",
                "**Ánh xạ KT (xref)…**: gắn mặt bằng kiến trúc của tầng (DXF/DWG) làm nền tham chiếu màu xám (không sửa file gốc, tự nạp lại khi file kiến trúc thay đổi; lưu đường dẫn tương đối theo thư mục hồ sơ); "
                + "**Dời xref…** để trục kiến trúc trùng trục kết cấu; **Ẩn / hiện xref**.",
                "**Lanh tô theo cửa KT**: chương trình nhận các ô cửa (layer CUA / DOOR / WINDOW hoặc block cửa) và tường chứa cửa (110 / 220), đặt lanh tô trên mỗi ô cửa; các cửa giống nhau (bề rộng, chiều dày tường, loại) "
                + "gộp thành một cấu kiện LT1, LT2… với số lượng = số ô cửa. **Ô văng trên cửa**: bấm vào ô cửa ở tường ngoài → ô văng OV… liền lanh tô vươn ra phía ngoài.",
                "Cây thư mục: lanh tô – ô văng của mỗi tầng nằm ở mục **Mặt bằng lanh tô – ô văng «tầng»**; khi xuất hồ sơ mỗi tầng có một tờ **LANH TÔ – Ô VĂNG «TẦNG»** gồm mặt bằng lanh tô của tầng và chi tiết các lanh tô.",
                "Bấm một cấu kiện trên mặt bằng → cây thư mục chọn đúng cấu kiện đó dưới đúng nút mặt bằng; bấm một cấu kiện trên cây → mặt bằng chuyển đúng tầng và đánh dấu vị trí; bấm đúp → mở chi tiết cấu kiện.");

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

        c.Sec("che-do-ho-so.cau-thang", "Cầu thang bộ – bản thang, bậc gạch / BTCT, zíc zắc, xương cá, thang thép")
            .Bullets("**Bản thang thường** (mặc định): chỉ thiết kế kết cấu **bản thang + chiếu nghỉ** (dầm chiếu nghỉ, dầm cốn nếu chọn); **bậc xây gạch** (thông dụng ở Việt Nam) chỉ là tải trọng và khối lượng xây bậc, không có thép.",
                "Bản vẽ: mặt bằng thang, **mặt bằng bố trí thép bản thang** (thanh đại diện có móc, chữ Ø..a.. + vòng số hiệu, đường phạm vi rải; dầm chiếu nghỉ / dầm chiếu tới nét đứt; mũi tên LÊN; vạch cắt 1-1, A-A), "
                + "mặt cắt dọc vế 1-1, **mặt cắt ngang bản thang A-A**, chi tiết chiếu nghỉ, dầm – số hiệu khớp bảng thống kê thép.",
                "**Bậc: Đổ BTCT liền khối**: cộng bê tông bậc tam giác ½·b·h·B, trọng lượng bậc; mỗi bậc 1 thanh Ø8 (hoặc Ø6) góc mũi bậc + thép chữ L Ø6a200 ôm bậc; chiều dày tính toán vẫn là bản thang (không kể bậc vào h0); có hình **chi tiết bậc BTCT**.",
                "**Kết cấu thang → Thang zíc zắc**: bản gấp khúc dày t (không có bản nghiêng): tải bản thân 25·t·(b + h)/b, bản tựa đơn trên hình chiếu bằng M = q·L²/8, thép chịu lực đi theo đường gấp khúc, "
                + "**thép góc chữ L tại góc lõm** (không uốn thép vòng qua góc lõm), kiểm tra cắt và độ mảnh L/t ≤ 20.",
                "**Thang xương cá**: dầm cốn giữa b × h đỡ bậc công xôn hai bên / một bên: bậc M = q·l²/2 + P·l (P = 1 kN ở mút), thép lớp trên neo qua dầm cốn; dầm cốn chịu uốn, cắt và **xoắn** (bậc một bên / hoạt tải lệch) – đai kín, thép dọc chịu xoắn theo TCVN 5574:2018.",
                "**Thang thép hình**: 2 cốn U160…U240, I, H hoặc hộp; mác CCT34 / CCT38 / Q235 / SS400; kiểm tra σ = M/W ≤ f·γc, τ, độ võng ≤ L/250; thép góc đỡ bậc L50×5 / L63×6 / L75×6; bản mã + 2 bu lông neo M16; "
                + "khối lượng thép (kg), sơn chống gỉ (m²); bản vẽ mặt đứng cốn, mặt cắt, chi tiết chân cốn.");

        c.Sec("che-do-ho-so.thang-may", "Hố pít – vách thang máy")
            .Bullets("＋ **Hố pít – vách thang máy**: kích thước giếng thông thuỷ, chiều dày vách, chiều sâu hố pít (thường 1,2…1,5 m), đáy hố, số tầng, chiều cao tầng, OH, lỗ cửa tầng; đất (γ, φ, phụ tải), mực nước ngầm, sức chịu tải nền; tải cabin, đối trọng.",
                "Thành hố pít tính như thành bể ngầm (áp lực đất chủ động + nước ngầm, phương pháp dải, vết nứt ≤ 0,2 mm); đáy hố chịu đẩy nổi / phản lực nền, chọc thủng dưới giảm chấn R = 4·(P + Q)·g; chống đẩy nổi khi thi công G ≥ 1,1·U.",
                "Vách giếng BTCT: thép 2 lớp mỗi phương, hàm lượng μ ≥ 0,25 %; lỗ cửa tầng có thép gia cường mép lỗ 2Ø14, thép xiên góc 2Ø12, dầm lanh tô cửa; chọn **vách đặt trên dầm móng** (mặc định) hoặc trên hố pít.",
                "Bản vẽ: mặt bằng giếng, mặt cắt 1-1 qua hố pít, chi tiết thép lỗ cửa; khối lượng bê tông, ván khuôn, thép, băng cản nước, chống thấm, đào đất.");

        c.Sec("che-do-ho-so.trich-chi-tiet", "Trích chi tiết, mặt cắt dầm, shop drawing, điểm cắt thép")
            .Bullets("Tab Mặt bằng kết cấu → công cụ **Trích chi tiết**: bấm 2 góc khoanh vùng cần thể hiện rõ → hình **CHI TIẾT A** (tl 1/25) vẽ lại mặt bằng tại vùng đó (dầm, cột, ô sàn, thép sàn, kích thước) với chữ đúng cỡ in; trên mặt bằng có khung nét đứt và ký hiệu **CT-A**. Chuột phải lên khung: đổi tên, tỷ lệ 1/10…1/50, ghi chú, xoá.",
                "Mỗi nhịp dầm đều có mặt cắt tại **hai gối và giữa nhịp**; mặt cắt giống nhau dùng chung số hiệu. Trên mặt bằng kết cấu tầng có vạch cắt + số hiệu tại các vị trí đó (tắt / bật: chuột phải lên khung chi tiết → \"Thể hiện vị trí mặt cắt dầm trên mặt bằng\").",
                "**Shop drawing thép dầm, sàn**: tích \"Xuất shop drawing\" ở từng dầm / sàn (hoặc Vật liệu… → cho mọi dầm, sàn): từng số hiệu thép vẽ tách riêng đúng hình uốn, kích thước từng đoạn, vị trí nối; bảng gia công – cắt thép: chiều dài cắt (trừ 2d mỗi góc uốn 90°, đai −12d), số cây 11,7 m, hao hụt %. Thép chạy suốt dài hơn cây thép: tự chọn vị trí nối trong vùng cho phép, một nửa số thanh nối so le ≥ 1,3·ll. Excel có thêm trang \"Gia công cắt thép\".",
                "**Điểm cắt / nối thép tuỳ chỉnh** (dầm): vươn mũ gối từng gối, khoảng cắt tăng cường dưới từng nhịp, vị trí nối thép dưới / trên. Chương trình **cảnh báo** khi: mũ gối vươn < L0/4 (gối biên) / L0/3 (gối giữa) hoặc < lan; tăng cường dưới cắt xa mép gối hơn L0/6; nối thép dưới trong 1/3 giữa nhịp; nối thép trên trong L0/4 từ mép gối hoặc trên gối; kháng chấn: nối trong 2h từ mép gối. Điểm sai được đánh dấu X đỏ + \"!\" trên mặt đứng dầm.",
                "**Ghi chú cấu tạo thép sàn** (dưới mặt cắt sàn và trong thuyết minh): nguyên tắc theo miền chịu kéo – thép lớp dưới phương NGẮN đặt trước (sát đáy), phương dài đặt trên; thép mũ phương ngắn đặt sau cùng (trên cùng); mũ vươn ≥ L0/4 cạnh ngắn; thứ tự đặt thép 1)…7) (lớp dưới ngắn → lớp dưới dài → con kê → chân chó → mũ phương dài → mũ phương ngắn → buộc, kiểm tra). Dầm có thứ tự lắp dựng: thép dọc dưới → đai → thép dọc trên → mũ gối.");

        c.Sec("che-do-ho-so.xuat", "Xuất hồ sơ")
            .Bullets("Xuất hồ sơ → **Bản vẽ DXF – gộp 1 file** (các khung A1 đặt cạnh nhau + bảng thống kê cốt thép) hoặc **tách từng cấu kiện** (KC-01_Dầm_D1.dxf … + KC-xx_ThongKeThep.dxf).",
                "**Bảng khối lượng Excel**: tổng hợp bê tông, bê tông lót, ván khuôn, cốt thép theo nhóm Ø ≤ 10 / 10–18 / > 18, cáp DƯL, công tác khác; trang theo cấu kiện (hàm lượng kg/m³); thống kê cốt thép đầy đủ.",
                "**Thuyết minh tính toán Word**: căn cứ pháp lý – tiêu chuẩn, vật liệu, từng cấu kiện (công thức thay số, bảng kiểm tra, thông báo), tổng hợp khối lượng.",
                "**Xuất tất cả vào thư mục…**: cả bản vẽ gộp, bản vẽ tách, Excel và Word.")
            .Note("Ở chế độ triển khai, nội lực do kỹ sư nhập (từ tính tay, chế độ mô hình hoặc phần mềm khác) – chương trình không kiểm tra nội lực đó có đúng với sơ đồ kết cấu hay không.");

        c.Sec("che-do-ho-so.dan-trang", "Dàn trang in, xuất PDF toàn bộ, in trực tiếp")
            .Bullets("Thanh công cụ → **🖨 Dàn trang in** mở cửa sổ dàn trang: danh sách tờ bên trái, tờ đang chọn ở giữa (khung, khung tên, các hình đúng tỷ lệ in), hình chưa đặt và thuộc tính tờ bên phải.",
                "**⟳ Tự dàn trang**: xếp mọi hình theo thứ tự hồ sơ – mặt bằng kết cấu móng → chi tiết móng → mặt bằng định vị cột → chi tiết cột → mặt bằng tầng, chi tiết dầm → sàn → lanh tô, ô văng → cầu thang → bể nước, hố pít, kết cấu khác → thống kê thép, vật liệu. Mỗi mục bắt đầu tờ mới; hình xếp trái → phải, trên → dưới, cách nhau ≥ 10 mm, không đè khung tên; tự chọn khổ nhỏ nhất (A3 → A2 → A1 → A0) vừa các hình của mục (bỏ chọn để dùng khổ mặc định).",
                "Tờ đầu là **danh mục bản vẽ** (số hiệu, tên, khổ, tỷ lệ); tờ đánh số KC-01, KC-02… Mỗi hình in đúng tỷ lệ riêng (mặt bằng 1/100, chi tiết 1/50, mặt cắt 1/25), khung tên ghi các tỷ lệ của tờ.",
                "**Dàn trang tay**: kéo hình để dời; chọn hình rồi **Chuyển** sang tờ khác hoặc **Gỡ hình khỏi tờ**; đặt hình chưa đặt vào tờ đang chọn; thêm / xoá / đổi thứ tự tờ, đổi khổ giấy, khổ đứng, tên tờ, ngày ghi khung tên. Tờ đã chỉnh tay **tự khoá** – lần tự dàn trang sau giữ nguyên tờ khoá. Khung đỏ báo hình chồng hình khác, đè khung tên hoặc ra ngoài khung. Bố cục lưu cùng hồ sơ .kcvxth.",
                "**Xuất PDF toàn bộ…**: một file PDF dạng nét, mỗi tờ một trang đúng khổ giấy, nét theo layer (màu, bề dày in), chữ tiếng Việt bằng phông Arial / Times New Roman của máy (nhúng vào PDF); tuỳ chọn **đen trắng**.",
                "**In…**: chọn máy in đã cài driver trên máy, số bản, khoảng trang; tờ tự thu vừa khổ giấy máy in (xoay nếu hợp hơn) hoặc chọn **In 1:1**. **Xuất DWG/DXF…**: mỗi tờ một file AutoCAD theo định dạng xuất đang chọn.")
            .Note("In và xuất PDF chỉ dùng máy in, phông chữ có sẵn trên máy – không kết nối mạng, không gửi dữ liệu đi đâu.");

        c.Sec("che-do-ho-so.chia-dam", "Chia dầm nhiều đoạn, xuất tất cả theo bộ dàn trang")
            .Bullets("Dầm nhiều nhịp có mặt đứng dài hơn vùng vẽ của khổ đang dùng (ở tỷ lệ chuẩn 1/50) được **chia đoạn tại gối**: mỗi đoạn gồm các nhịp liên tiếp, kéo qua gối đầu / cuối tới đường cắt ngắt (hai đoạn kề nhau cùng thể hiện gối chung), ghi \"XEM TIẾP ĐOẠN 2/3\" / \"TIẾP THEO ĐOẠN 1/3\"; tiêu đề \"D1 (SL=2; L=20300) – ĐOẠN 1/3 (trục 1–4)\".",
                "Số hiệu thép, vùng đai, vị trí mặt cắt giữ như dầm nguyên; thanh chạy qua nhiều đoạn ghi nhãn ở mỗi đoạn, đoạn sau ghi \"(tiếp)\" và không vẽ móc ở đầu bị cắt ngắt. Mặt cắt là hình riêng, vẽ một lần, xếp thành hàng ngay dưới mặt đứng.",
                "Cửa sổ **Dàn trang in** → mục **CHIA DẦM NHIỀU ĐOẠN**: Tự động | Không chia | Theo số nhịp/đoạn (vd. 2) | Tại các trục (vd. 3, 5 – số thứ tự gối hoặc tên trục); tuỳ chọn **cho thu tới 1/75 trước khi chia**; **Áp dụng cho dầm đang chọn** hoặc **cho mọi dầm**. Không chia: mặt đứng dài tự hạ tỷ lệ (khổ chốt) hoặc nâng khổ (khổ tự động).",
                "**Xuất tất cả…** dùng chung bộ dàn trang: nếu đã dàn trang tay thì xuất đúng bộ tờ đó (hình mới được xếp thêm vào cuối), chưa dàn thì tự dàn – khổ, tỷ lệ tự động, cấu kiện cùng nhóm mặt bằng gộp tờ, tờ danh mục ở đầu. File tách rời đặt tên theo số hiệu + nội dung (KC-02_MB_KetCauMong, KC-05_Dầm_D1-D3…).");

        c.Sec("che-do-ho-so.hoan-thien-xuat", "Hoàn thiện bản vẽ: cửa trên mặt bằng, lanh tô từ mô hình, cột lệch tâm, tham chiếu tờ")
            .Bullets("Mô hình có **cửa gắn tường** (giao diện kiểu Revit): mặt bằng kết cấu các tầng vẽ thêm tường (cắt tại ô cửa ở cao 1,2 m), cánh và cung mở cửa đi, 3 nét cửa sổ, ô thoáng nét đứt, thẻ D1 / S1; bộ bản vẽ có tờ **KC-09 BẢNG THỐNG KÊ CỬA** (kiểu, kích thước, số lượng theo tầng) và bảng khối lượng (Excel / Word) có bảng **Thống kê cửa**. Diện tích tường xây đã trừ diện tích cửa thật.",
                "Triển khai hồ sơ → tab **Mặt bằng kết cấu** → nút **Lanh tô từ cửa mô hình**: mọi cửa gắn tường của mô hình đang mở (cửa sổ chế độ Thiết kế) thành lanh tô trên **Mặt bằng lanh tô tầng X** (mặt bằng cùng tên / cùng cao độ, chưa có thì tạo); cửa giống nhau (bề rộng, tường 110 / 220, loại cửa) gộp LT1, LT2…, số lượng = số ô cửa; chạy lại không nhân đôi. Lanh tô tường 110 được kiểm tra / gợi ý tiết diện, gối như lanh tô nhập tay.",
                "**Mở mặt bằng CAD** trong Triển khai hồ sơ: bản vẽ vừa đọc được gắn làm **ánh xạ kiến trúc (xref)** của mặt bằng đang xem (nếu chưa có), dời để trục đầu tiên trùng trục hồ sơ.",
                "Cột **lệch tâm** (trục qua mép cột): mặt bằng kết cấu và mặt bằng móng ghi kích thước trục → mép cột; xuất **ETABS (.e2k)** dùng điểm chèn CARDINALPT (trục qua mép trái → 4, mép phải → 6, dưới → 2, trên → 8, góc → 1/3/7/9); độ lệch không bằng nửa cạnh ghi dòng chú thích \"$ COLUMN … OFFSET\" để chỉnh tay. **SAP2000 (.s2k)**: bảng FRAME INSERTION POINT ASSIGNMENTS với độ dời đầu nút XI, YI, XJ, YJ đúng độ lệch.",
                "Dầm chia đoạn: ghi chú nối tiếp có **số tờ** – \"XEM TIẾP ĐOẠN 2/3 – TỜ KC-05\", \"TIẾP THEO ĐOẠN 1/3 – TỜ KC-04\" (tự cập nhật khi dàn / đánh số lại); điểm cắt / nối thép sai (dấu X đỏ) hiện cả trên các đoạn. Bản vẽ dầm chế độ Thiết kế (KC-04) cũng tự chia đoạn khi mặt đứng dài hơn bề rộng khổ giấy.",
                "**DANH MỤC MẶT CẮT** dưới các mặt cắt mỗi dầm: số mặt cắt, các vị trí dùng chung (gối / giữa nhịp), b×h, thép trên, thép dưới, đai. Tắt trong bộ tờ (SectionCatalog).");

        c.Sec("che-do-ho-so.kho-giay", "Khổ giấy và tỷ lệ tự động")
            .Bullets("Cửa sổ **Dàn trang in** → mục **BỘ TỜ**: **Khổ** (Tự động, A4, A3, A2, A1, A0), **hướng** (Ngang, Đứng, Tự động), **Tỷ lệ mặt bằng** (Tự động hoặc 1/50, 1/75, 1/100…), **Khổ lớn nhất** khi tự chọn khổ → **Áp dụng cho toàn bộ (tự dàn trang)**.",
                "Khổ chốt (vd. A3): mặt bằng lấy **tỷ lệ lớn nhất** (trong dãy chuẩn 1/50 … 1/500) mà hình – kể cả kích thước, đầu trục, tiêu đề – vừa vùng vẽ trừ khung tên và lề; chi tiết giữ tỷ lệ chuẩn (1/20 – 1/50), mặt cắt 1/10 – 1/25, thu nhỏ nếu không vừa.",
                "Khổ **Tự động**: tỷ lệ chốt (vd. mặt bằng 1/100) → chọn **khổ nhỏ nhất** chứa vừa; nếu để tỷ lệ tự động: mặt bằng nhỏ dùng 1/50 (vừa A3), mặt bằng lớn 1/100…; hình vượt khổ lớn nhất cho phép thì hạ tỷ lệ.",
                "Mục **TỜ ĐANG CHỌN**: khổ, hướng, **Tỷ lệ hình chính** → **Áp dụng cho tờ đang chọn** (các hình phụ đổi tỷ lệ cùng hệ số; tờ được khoá).",
                "Hình được **vẽ lại** ở tỷ lệ mới (không phóng / thu hình cũ): chữ ghi chú luôn cao 2,5 mm khi in, tiêu đề 3,5 mm, vòng số hiệu, đầu trục, mũi tên kích thước đúng cỡ; tiêu đề hình ghi đúng \"tl: 1/xx\", khung tên ghi các tỷ lệ của tờ.",
                "Mặt bằng kết cấu tự tránh đè nhãn: nhãn dầm đặt giữa nhịp, đụng nhãn cột / dầm khác thì dời dọc dầm hoặc sang phía bên kia; nhãn lanh tô, ô văng tránh dầm; chữ thép sàn Ø..a.. luôn cách vòng số hiệu và không đè nhau.");

        c.Sec("che-do-ho-so.khung-ten", "Khung tên, tên và số hiệu bản vẽ")
            .Bullets("Cửa sổ **Dàn trang in** → mục **KHUNG TÊN** chọn: **Mặc định** (khung tên dựng sẵn), **Tự tạo** hoặc **Tải lên (AutoCAD)**.",
                "**Thiết kế khung tên…**: bảng các ô – hàng, trường (Đơn vị thiết kế, Công trình, Hạng mục, Địa điểm, Chủ đầu tư, Chủ trì, Thiết kế, Kiểm tra, Duyệt, Tên bản vẽ, Số hiệu, Tỷ lệ, Ngày, Ghi chú, Lần sửa, Tờ số…), nhãn ô, bề rộng (mm), cỡ chữ, bật / tắt ô, hiện nhãn; chiều cao từng hàng; vị trí **góc phải dưới** hoặc **dải dọc bên phải**. Bấm **Xem trước**, **Khôi phục mặc định**, **Dùng khung tên này**.",
                "**Tải khung tên AutoCAD (DWG/DXF)…**: lấy đường, đa tuyến, cung, tròn, chữ, block chèn; trường điền tự động lấy từ thuộc tính ATTDEF/ATTRIB (thẻ TENBANVE, SOHIEU, TYLE, NGAY, CONGTRINH… – có / không dấu, có / không gạch nối) hoặc chữ giữ chỗ **{TEN_BAN_VE}**, **{SO_HIEU}**, **{TY_LE}**, **{NGAY}**, **{CONG_TRINH}**, **{CHU_DAU_TU}**, **{THIET_KE}**, **{TRANG}/{TONG_TRANG}**… Khung ngoài cùng bằng khổ giấy A0…A4 (kể cả vẽ theo tỷ lệ 1/10…1/200) được nhận là **khung bao toàn tờ** và co vừa khổ từng tờ; nếu chỉ là bảng nhỏ thì đặt vào **góc phải dưới** khung viền. Hồ sơ lưu bản sao khung tên – mở lại không cần file gốc (đường dẫn file gốc lưu tương đối so với thư mục hồ sơ).",
                "**Thông tin khung tên…**: nhập Đơn vị thiết kế, Hạng mục, Chủ trì, Kiểm tra, Duyệt, Lần sửa, Ghi chú. Tên công trình, chủ đầu tư, địa điểm, người thiết kế lấy từ Thông tin hồ sơ; tên bản vẽ, số hiệu, tỷ lệ, ngày, tờ số điền theo từng tờ. Khung tên dùng chung cho xem trước, DWG/DXF, PDF và in.",
                "**Tên bản vẽ tự động theo cây thư mục công trình**: tờ mặt bằng mang tên mặt bằng (MẶT BẰNG KẾT CẤU MÓNG, MẶT BẰNG KẾT CẤU TẦNG 2…); tờ chi tiết ngay sau mặt bằng của nó, tên ghi loại và cấu kiện trên tờ – vd. CHI TIẾT MÓNG ĐƠN M1, M2; CHI TIẾT DẦM D1–D3; tờ thống kê ghi tên bảng. Danh mục bản vẽ tự cập nhật.",
                "**Đánh số bản vẽ**: tiền tố (KC, KT, KCM…), mẫu **KC-01** (liên tục), **KC01** (không gạch) hoặc **KC-1.01** (theo nhóm mặt bằng – tờ danh mục là nhóm 0), số bắt đầu, số chữ số → **Đánh số lại**.",
                "**Đổi tên bản vẽ**: sửa ô **Tên bản vẽ** / **Số hiệu** của tờ đang chọn (nháy đúp vào tờ trong danh sách để sửa nhanh). Tên / số đã sửa tay được giữ khi tự dàn trang và đánh số lại; **Đặt lại tên tự động** trả tờ đang chọn về tên – số tự động (giữ Shift khi bấm: mọi tờ).");
        c.Sec("che-do-ho-so.ban-ve-revit", "Bản vẽ kiểu Revit: trình duyệt dự án, khung nhìn, bản vẽ (sheet), thuộc tính, cỡ chữ")
            .Bullets("Cột trái có **thẻ dọc**: **Cấu kiện** (cây cấu kiện như trước) và **Trình duyệt** – trình duyệt dự án kiểu Revit: **Khung nhìn** (mọi hình sinh từ cấu kiện, nhóm theo mặt bằng kết cấu: mặt bằng + chi tiết các cấu kiện đặt trên mặt bằng đó; cấu kiện chưa đặt nhóm theo loại), **Bảng thống kê** (thống kê thép, vật liệu, DANH MỤC BẢN VẼ) và **Bản vẽ (Sheets)**. Khung nhìn đã đặt trên tờ có dấu ■ và số hiệu tờ.",
                "**Tạo bản vẽ**: nút **＋ Bản vẽ** (hoặc chuột phải mục Bản vẽ → Tạo bản vẽ mới…) – số hiệu **tự nhảy tiếp** (KC-01, KC-02… theo mẫu đánh số), bạn đặt tên; **F2** hoặc bảng Thuộc tính để đổi tên / số hiệu; xoá bản vẽ thì khung nhìn trở về danh sách chưa đặt.",
                "**Dàn khung nhìn**: mở bản vẽ (kích đúp) → thẻ **Bản vẽ (sheet)** hiện tờ giấy + khung tên; **kéo khung nhìn từ Trình duyệt thả vào tờ** (vị trí thả là tâm khung). Kéo để dời (tự bắt thẳng hàng cạnh trái / dưới với khung khác), **Delete** gỡ khung. Khung viền đỏ: chồng khung khác, đè khung tên hoặc ra ngoài vùng vẽ. Mỗi khung nhìn chỉ nằm trên **một** tờ (thả vào tờ khác là chuyển tờ); bảng thống kê đặt được nhiều tờ.",
                "**Kích đúp khung nhìn** (trên tờ hoặc trong trình duyệt) để **sửa cấu kiện** (mở bảng nhập + bản vẽ xem trước) hoặc **mặt bằng** (mở không gian vẽ đúng tầng) như *Activate View* của Revit; xong bấm **↩ Quay lại bản vẽ** hoặc **Esc** – khung nhìn vẽ lại theo cấu kiện đã sửa.",
                "**Bảng THUỘC TÍNH** (dưới trình duyệt) theo đối tượng đang chọn: *Bản vẽ* (số hiệu, tên, khổ, khổ đứng, khoá, ngày, thông tin dự án trong khung tên); *Khung nhìn trên tờ* (tên khung nhìn, hiện tiêu đề tên + tỷ lệ dưới hình, **tỷ lệ** – hình được vẽ lại, **cỡ chữ riêng** ×0,7…×2, vị trí X/Y, nút sửa cấu kiện / gỡ khỏi tờ); *Khung nhìn* (mục, tỷ lệ gốc, tờ đang chứa, đặt vào bản vẽ đang mở); *Cấu kiện* (tên, số lượng).",
                "**Cỡ chữ in**: chọn nút gốc trình duyệt (hoặc bỏ chọn) → mục **CỠ CHỮ IN**: ghi chú, nhãn thép, tên hình, tên mặt cắt, kích thước, chữ trục, bảng thống kê, vòng số hiệu Ø, đầu trục Ø (mm khi in; để trống = theo Thiết lập bản vẽ) và **hệ số chung**. Mọi hình triển khai (xem trước, bản vẽ, xuất DWG/DXF/PDF, in) vẽ lại theo cỡ mới; chữ trong mô hình = cỡ in × mẫu số tỷ lệ.",
                "Khi chọn **cấu kiện**, bảng Thuộc tính liệt kê **mọi trường nhập** của cấu kiện theo nhóm (như bảng nhập ở giữa): sửa một ô rồi Enter / chuyển ô – cấu kiện được tính lại, bảng nhập giữa và các khung nhìn trên tờ cập nhật ngay.",
                "**Hoàn tác / làm lại trên bộ bản vẽ**: ở thẻ Bản vẽ (sheet), **Ctrl+Z** hoàn tác, **Ctrl+Y** làm lại các thao tác tạo / xoá / đổi tên / đánh số tờ, đặt / dời / gỡ khung nhìn, đổi tỷ lệ và cỡ chữ khung nhìn (tối đa 50 bước; mở hồ sơ khác thì xoá lịch sử). Cửa sổ xem trước bộ bản vẽ cũ cũng vẽ theo cỡ chữ riêng của từng khung nhìn.",
                "**DANH MỤC BẢN VẼ** là một khung nhìn bảng: kéo vào tờ đầu – bảng tự cập nhật khi thêm, xoá, đổi tên, đánh số bản vẽ. **Tự dàn trang** xếp các khung nhìn chưa đặt vào tờ (tờ đã khoá giữ nguyên). Xuất bản vẽ / PDF / in dùng đúng bộ bản vẽ bạn đã dàn.",
                "Chế độ Thiết kế: **Hiển thị → Cỡ chữ nhãn mặt bằng…** đặt cỡ chữ (px màn hình, 7–30) cho tên cột, dầm, sàn trên mặt bằng; lưu trên máy (ui.json).");
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
                "Tab **Tải trọng**, nhóm **Tải mặc định khi tạo mô hình**: Hoàn thiện sàn 1.2; Hoạt tải sàn 1.5; Hoạt tải mái 0.75; Tường biên trên dầm **7.5** kN/m.",
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
                "Tab **Tải trọng** → nhóm **Gió**: bật **Tính tải gió**, dạng địa hình B (hoặc theo thực tế), Cp. Nhóm **Động đất**: nếu agR đáng kể, bật **Tính động đất**, nhập agR, loại nền, q → **Áp dụng thông số**.",
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

        ex.Sec("quy-trinh.vi-du.coc-ep", "Bước 6b – Truyền tải, cọc ép theo lực ép, số cọc từng hố móng")
            .Steps("Tab **Mô hình móng** → **Truyền tải & gợi ý móng…**: tab **Tải chân cột** cho diện chịu tải, G, P, ψ, Ntc, Ntt từng cột, so với phản lực khung (cột Khung/truyền thường 1,1–1,25 vì khung có gió và mô men).",
                "Dòng **Cọc ép**: lực ép đầu cọc Pép = **50** tấn, k = **2,0** → **Tính lại**: [P] = 500/2 = 250 kN/cọc, cọc 250×250 (Pvl = 1188 kN ≥ Pép – cọc không vỡ khi ép).",
                "Ô **Ranh đất: tự động**: nhà 1 nhịp rộng 5 m → cả 8 cột sát 1 ranh (trái hoặc phải). Bỏ chọn để tự tích thêm ranh trước/sau → cột góc sát 2 ranh (lệch tâm 2 phương).",
                "Tab **Hố móng – số cọc**: mỗi cột một đài, tim cọc cách ranh ≥ 0,5 m, cọc xếp 1 hàng dọc ranh, R1/N = Ls/(Ls − e) ≈ 1,08 (Ls = 5 m); số cọc n = ⌈(1,1·ΣNtt·R1/N + Gđài)/[P]⌉ – nhà mẫu 2–3 cọc mỗi hố.",
                "Giữ ô **thiết kế móng theo tải đứng** → **Áp dụng: Móng cọc** → {m:designFnd}: các đài lệch tâm có dầm móng cân bằng, Pmax ≤ [P].");

        ex.Sec("quy-trinh.vi-du.thep-dam", "Bước 6c – Kiểu bố trí thép dầm và bản vẽ từng dầm")
            .Steps("Chọn dầm dọc tầng 2 D22x40, 3 nhịp (D2-5 trong dự án mẫu) → **Chọn bố trí thép…** → vùng **Kiểu bố trí thép dọc (A / B / C)**: so sánh khối lượng; kiểu B (toàn bộ chạy suốt) tốn hơn kiểu A khoảng 9 %.",
                "Vùng **Nhịp 1 – thép dưới**: chọn số thanh / Ø trong danh sách (mọi phương án đã thoả As và khe hở) → **Áp dụng phương án**.",
                "Thiết kế → **Bản vẽ từng dầm…** → chọn D2-5: mặt đứng, mặt cắt, bảng thống kê thép riêng dầm này → **Xuất bản vẽ dầm này…** (khung tên \"CHI TIẾT DẦM D2-5 (SL=2)\").");

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
                "Không nhớ kích thước? Bấm **Tải mặt bằng để lấy kích thước…** (ngay dưới ô Liên kết chân cột): mở **bản vẽ CAD DWG/DXF** – chương trình tự đọc trục (layer nhóm Trục và tên trục; không có thì theo tim tường), "
                + "chọn mặt bằng tầng, chiều cao tầng lấy theo các mặt bằng tầng nếu có; hoặc mở **ảnh chụp / scan mặt bằng** (JPG, PNG): ① bấm 2 điểm của một kích thước đã biết và nhập chiều dài thật (mm), "
                + "② bấm lần lượt các trục đứng 1, 2, 3…, ③ bấm các trục ngang A, B, C… – chương trình tính bước trục (làm tròn 10 / 50 / 100 mm). Kiểm tra rồi **Đồng ý** để điền vào các ô.",
                "**Chiều cao các tầng (dưới → trên)**, **Cao độ chân cột (m)** (thường âm, vd. −0,6 … −1,5), **Liên kết chân cột** (Ngàm / bỏ chọn = khớp).",
                "Chọn **Tiết diện mặc định** (cột, dầm phương X, dầm phương Y, sàn), **Tải trọng**, **Gió**, **Động đất**, **Vật liệu**, **Nền đất dưới móng đơn**.",
                "Bấm **Tạo mô hình** ({m:buildGrid} cùng tác dụng). Sau này chỉ đổi thông số (không xoá mô hình) thì bấm **Áp dụng thông số**.")
            .Warn("**Tạo mô hình** xoá mô hình hiện có và sinh lại lưới trục, tầng, cột, dầm, sàn và tải. Các sửa đổi trên mặt bằng sẽ mất – lưu dự án trước.");

        c.Sec("khoi-tao.mat-bang-cad", "Mở mặt bằng CAD lấy dữ liệu: chọn trục, trục qua tim / mép cột")
            .P("Trong hộp **Tải mặt bằng để lấy kích thước…**, sau khi mở bản vẽ DWG/DXF, bản vẽ hiện ở giữa (cuộn chuột để thu phóng, giữ nút giữa để kéo; "
               + "bật / tắt từng nhóm nét theo màu: tường, trục, cột, cửa, thang, dầm, sàn). Cột tô **xanh** = đúng tim trục, **cam** = lệch tâm so với trục, **xám** = không nằm trên giao trục.")
            .Bullets("**Nguồn trục – Tự nhận theo layer trục**: đọc các nét trên layer trục (TRUC, AXIS, GRID…). Bản vẽ đặt tên layer khác: mở mục **Layer trục**, tích các layer là trục rồi **Đọc lại**.",
                "**Nguồn trục – Tự nhận theo cột**: gom các cột thẳng hàng (sai số mặc định 50 mm) thành trục – dùng khi bản vẽ không có layer trục.",
                "**Nguồn trục – Người dùng chọn trục**: bấm lên nét trục để thêm trục theo phương nét; bấm lên cột để thêm trục qua cột (chọn trục đứng X hoặc trục ngang Y); "
                + "chuột phải lên trục để xoá. **Lấy trục tự nhận** chép trục layer / cột sang danh sách để sửa tay. Chọn trục trong danh sách để **Đổi tên** / **Xoá**; **Tên tự động** đặt 1, 2, 3… / A, B, C….",
                "**Trục đi qua**: Tim cột; Mép cột – mép trái / dưới; Mép cột – mép phải / trên; Mép cột – **mép ngoài nhà** (trục biên qua mép ngoài cột, trục giữa qua tim – hay gặp ở nhà phố sát ranh đất). "
                + "Cột có tâm lệch khỏi trục được ghi **lệch tâm** (vd. ±110 mm với cột 220): mặt bằng kết cấu, mặt bằng định vị cột và mặt bằng móng vẽ cột / móng đúng vị trí thật; sơ đồ tính giữ nút tại giao trục.",
                "**Đơn vị bản vẽ**: Tự động (theo $INSUNITS và kích thước bản vẽ) hoặc chọn mm / cm / m nếu bản vẽ khai báo sai.",
                "**Thông số đọc được**: bước trục X, Y; cột trên giao trục và tiết diện (vd. C30x22 – cạnh theo Y × cạnh theo X); tường trên trục (tổng chiều dài tường 110, 220); "
                + "công năng ô sàn theo tên phòng (WC → vệ sinh, ban công, hành lang, kho…); lỗ sàn cầu thang / thông tầng.",
                "**Áp dụng**: tích các mục cần cập nhật (tiết diện cột, lệch tâm cột, xoá cột ở giao trục không có cột, tường → tải tường lên dầm, công năng ô sàn, lỗ sàn). "
                + "**Đồng ý** điền bước trục vào ô Lưới trục và tầng, dựng lại mô hình theo lưới mới (nếu chọn hoặc lưới khác) rồi áp dụng; hộp xác nhận liệt kê các thay đổi, hoàn tác bằng Ctrl+Z.",
                "Chế độ **Triển khai hồ sơ** (nút **Tải mặt bằng…** ở không gian vẽ mặt bằng kết cấu): cập nhật bước trục của mặt bằng kết cấu và đặt cột lên mặt bằng đang xem tại vị trí thật (giao trục + lệch tâm); "
                + "cột cùng tiết diện dùng chung một cấu kiện (C1, C2…).")
            .Tip("Tường, công năng, lỗ sàn được áp cho các tầng điển hình (trừ tầng mái). Kiểm tra lại các tầng có mặt bằng khác (tầng 1 kinh doanh, tầng tum…).");

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
                new[] { "VXT_CHUANLAYER (VXT_CL)", "Chuẩn hoá layer bản vẽ bất kỳ về layer chuẩn Việt Const VXT-… để chương trình đọc chắc chắn (xem {s:khoi-tao.chuan-layer})." },
                new[] { "VXT_GUI", "Ghi %AppData%\\KetCauVXT\\Link\\acad_<ngày>_<giờ>.json và báo chương trình (đang mở thì hỏi \"Có dữ liệu mới từ AutoCAD… Mở hộp thoại nhập?\"; chưa mở thì tự chạy)." })
            .P("Trong chương trình: {m:acad} chọn file acad_*.json; hộp thoại **Nhập bản vẽ kiến trúc** mở với các tầng đã khai báo và phương án kết cấu đề xuất – tiếp tục như {s:khoi-tao.kien-truc}.")
            .Tip("Tường vẽ 2 nét song song (tường 1 nét lấy dày 110 mm); cột là đa tuyến kín, miền tô hoặc 4 nét rời 0,12–1,0 m; tên trục ≤ 4 ký tự gần đầu trục; cửa giữ dạng block. Elip, spline chưa hỗ trợ (bị bỏ qua, có báo số lượng).");

        c.Sec("khoi-tao.chuan-layer", "Chuẩn hoá layer bản vẽ về layer chuẩn Việt Const (VXT-…)")
            .P("Bản vẽ kiến trúc / kết cấu của mỗi văn phòng đặt tên layer khác nhau (A-WALL, KT_TUONG 220, Tường, LAYER1…). Chuẩn hoá layer chuyển chúng về bộ layer chuẩn để Việt Const đọc chắc chắn: "
               + "**VXT-TRUC** (đường trục), **VXT-TRUC-KH** (vòng ký hiệu, tên trục), **VXT-COT**, **VXT-TUONG-110**, **VXT-TUONG-220**, **VXT-TUONG**, **VXT-CUA-DI**, **VXT-CUA-SO**, **VXT-THANG**, "
               + "**VXT-LO-SAN**, **VXT-TEN-PHONG**, **VXT-DAM**, **VXT-SAN**, **VXT-KICH-THUOC**, **VXT-BO-QUA**. Mọi bộ đọc của chương trình nhận layer VXT-… trước mọi quy tắc khác.")
            .Bullets("**Tự nhận**: theo TÊN layer (tiếng Việt / Anh, có hoặc không dấu, tiền tố A-…, S-…, xref NHA|…) và theo NỘI DUNG: cặp nét song song cách 110 / 220 → tường 110 / 220; "
                + "nhiều chữ nhật kín 0,1–1,2 m → cột; nét gạch chấm dài + vòng tròn + chữ 1–2 ký tự → trục; block CUA*, DOOR*, D1… → cửa đi, WINDOW*, S1… → cửa sổ; chữ \"Phòng ngủ\", \"WC\", \"Bếp\"… → tên phòng; "
                + "chữ toàn số, đường kích thước → kích thước; nét bậc cách đều 0,24–0,34 m → cầu thang. Mỗi dòng có độ tin cậy và căn cứ; ô vàng = tin cậy thấp, nên xem lại.",
                "**Trong AutoCAD – lệnh VXT_CHUANLAYER (VXT_CL)** hoặc nút **Chuẩn hoá layer bản vẽ → VXT-…** trên bảng lệnh VXT: bảng layer gốc → layer chuẩn (sửa cột **Layer chuẩn**), **Lọc**, "
                + "**Chỉ hiện layer chưa nhận**, **Tô sáng layer đang chọn**, **Chọn đối tượng trên bản vẽ…** (layer của các đối tượng chọn được gán vào layer chuẩn đang chọn ở ô **Gán vào**), **Tự nhận lại**. "
                + "Cột **Chuyển**: tích = chuyển layer đó, bỏ tích = giữ nguyên (**Tích chuyển tất cả** / **Bỏ tích tất cả** cho các dòng đang hiện). "
                + "Bấm **CHUYỂN ĐỔI** → bảng **Xác nhận chuyển đổi layer** liệt kê layer gốc → layer chuẩn, số đối tượng và cột **Layer cũ sau khi chuyển**: "
                + "**Giữ nguyên**, **Xoá layer cũ** (chỉ xoá khi không còn đối tượng, kể cả trong block; layer 0, Defpoints, xref luôn giữ) hoặc **Đổi tên layer cũ** (sửa ô **Tên mới**; "
                + "**Áp dụng cho tất cả** + hậu tố, mặc định _CU). Bấm **XÁC NHẬN CHUYỂN ĐỔI** mới thực hiện, **Quay lại** (Esc) để sửa bảng. Báo cáo cuối ghi số layer đã xoá / đổi tên và layer giữ lại kèm lý do.",
                "**Cách áp dụng** (AutoCAD): ① **Tạo bản sao chuẩn hoá <tên>_VXT.dwg** cạnh file – bản vẽ gốc giữ nguyên (mặc định, khuyên dùng; có thể bind xref đã nạp trong bản sao); "
                + "② **Chuyển đối tượng sang layer chuẩn** ngay trong bản vẽ đang mở; ③ **Đổi tên layer** khi một layer gốc ứng với một layer chuẩn, phần còn lại chuyển đối tượng. "
                + "② và ③ là một lệnh – gõ **U** để hoàn tác. Tuỳ chọn: sửa cả đối tượng trong định nghĩa block, block cửa → layer cửa, lưu thêm DXF (<tên>_VXT.dxf) để mở trong Việt Const, gửi sang Việt Const (VXT_GUI) sau khi chuẩn hoá. "
                + "Xref chưa nạp được cảnh báo (nạp lại rồi chạy lệnh).",
                "**Trong Việt Const (không cần AutoCAD)**: hộp **Tải mặt bằng để lấy kích thước…** → mở bản vẽ → nút **Chuẩn hoá layer…**: cùng bảng đề xuất; **Đồng ý** đọc lại bản vẽ theo bảng (file gốc không bị sửa); "
                + "**Xuất DXF đã chuẩn hoá…** ghi bản sao DXF với layer VXT-….",
                "**Bộ ánh xạ** (layer gốc → layer chuẩn): **Lưu bộ ánh xạ…** / **Mở bộ ánh xạ…** để dùng lại cho các bản vẽ cùng văn phòng kiến trúc; lưu trong %AppData%\\VietConst\\layer-profiles (chỉ trên máy, không gửi đi đâu). "
                + "Gán trong bộ ánh xạ được ưu tiên trước tự nhận; xref NHA|A-WALL dùng lại gán của A-WALL.")
            .Tip("Kiểm tra lại các dòng tin cậy thấp và các layer \"(giữ nguyên)\" trước khi áp dụng; layer 0, DEFPOINTS và layer của xref không bị đổi tên.");

        c.Sec("khoi-tao.cao-do-bo-mon", "Cao độ từ móng đến mái, bộ môn Kiến trúc – Kết cấu – Điện nước, IFC / SketchUp / Revit")
            .P("Mô hình đi từ **đáy móng → mặt móng (chân cột) → nền tầng 1 (±0.000) → sàn tầng 2, sàn tầng 3… → sàn mái (→ sàn tum)**. "
               + "Các sàn do khung đỡ là tầng phân tích; **sàn nền tầng 1** nằm trên đất (on grade) – không dồn tải lên dầm cột, chỉ tính khối lượng, bản vẽ, IFC. "
               + "Độ sâu móng dùng chung với thiết kế móng (Địa chất → chiều sâu chôn móng).")
            .Bullets("**BIM → Tạo cao độ tầng…**: số tầng, chiều cao tầng 1 / tầng trên, cao độ nền, chiều sâu móng, mặt móng, kiểu mái, tầng tum; tuỳ chọn sinh khung + sàn theo lưới trục và sàn nền tầng 1 (chỉ dùng cho mô hình chưa có cấu kiện).",
                "**Chèn tầng…** (phía trên tầng đang xem: sao dầm, sàn, cắt cột, nâng các tầng trên), **Xoá tầng** (nối cột, hạ các tầng trên), tên tầng tự đặt lại T2, T3…, MAI, TUM; **Sàn mọi ô** tạo ô sàn còn thiếu; **Sàn nền** tạo sàn trên đất theo ô lưới. Vẫn vẽ sàn trực tiếp bằng công cụ Sàn.",
                "**Bộ môn**: công tắc **Kiến trúc | Kết cấu | Điện nước** ở đầu ribbon – mỗi bộ môn hiện tab của mình; đối tượng bộ môn khác vẽ **mờ như xref**, không chọn được. **BIM → Bộ môn hiển thị…** bật / tắt, chỉnh độ mờ. "
                + "Tường, cửa, phòng, trần, mái, đồ đạc thuộc Kiến trúc; cột, dầm, sàn, móng thuộc Kết cấu; ống, phụ kiện, thiết bị vệ sinh, điện, đèn thuộc Điện nước.",
                "**Kiến trúc**: vẽ tường, đặt cửa (tường tự cắt ô cửa); **Tìm phòng** tạo phòng từ mạng tường (tên, diện tích), **Đặt tên phòng…** (tên quyết định công năng sàn); **Kiến trúc → kết cấu**: tường → tải dầm / sàn, phòng → công năng ô sàn (WC → sàn vệ sinh…), cửa → lanh tô, giao tường chữ L/T chưa có cột → đề xuất (và tuỳ chọn thêm) cột.",
                "**Thư viện 3D…**: nhập đồ đạc / thiết bị từ SketchUp (File → Export → 3D Model → OBJ hoặc COLLADA .dae; OBJ hỏi hệ số đơn vị) hoặc từ Revit (*.vxtmesh.json); lưu trên máy tại %AppData%\\VietConst\\ThuVien3D (không gửi đi đâu); **Đặt vào mặt bằng (bắt điểm)** – rê chuột, tự bám mặt tường / góc phòng / giao trục (mục Đặt đồ đạc bên dưới).",
                "**Kiểm tra va chạm**: giữa các bộ môn – **Cứng** (giao nhau), **Ống lồng** (ống xuyên dầm / tường – cần ống lồng), **Khe hở** (gần hơn khe hở yêu cầu). Nháy đúp một dòng để xem vị trí trên mặt bằng. Không báo nút khung (dầm–cột), cửa với tường chủ.",
                "**Trao đổi**: **Xuất IFC…** (IFC2X3 – Revit: File → Open → IFC; SketchUp Pro: File → Import → IFC), **Xuất SketchUp…** (COLLADA .dae, nhóm theo bộ môn / loại), **Xuất OBJ…** (+ MTL), chọn bộ môn cần xuất. "
                + "**Nhập IFC…** đọc cột, dầm, sàn (sàn nền), tường, cửa đi / cửa sổ, phòng. Add-in Revit (schema 2.0) xuất thêm tường (lớp cấu tạo), cửa, phòng, ống – thiết bị điện nước; **Cập nhật từ Revit…** so khớp theo UniqueId: thêm / sửa / xoá tường và thiết bị điện nước đã đổi trong Revit.",
                "**Hồ sơ theo bộ môn…**: chọn bộ môn – Kiến trúc (KT-01…: mặt bằng các tầng có tường, cửa, tên + diện tích phòng, đồ đạc; thống kê cửa; thống kê phòng), Kết cấu (KC-…: bộ bản vẽ kết cấu), Điện nước (DN-…); trên mặt bằng, bộ môn khác hiện nét xám làm nền đối chiếu.")
            .Tip("Ở bộ môn Điện nước, phần kiến trúc là nền tham chiếu: sửa tường / cửa ở bộ môn Kiến trúc rồi chạy **Kiểm tra va chạm** để thấy thiết bị, ống bị ảnh hưởng.");

        c.Sec("khoi-tao.xoa-mong", "Xoá móng đã thiết kế, sửa móng trên mặt bằng – thu phóng mọi hình vẽ thiết kế")
            .Bullets("Tab **Mô hình móng**: **Xoá móng chọn** (hoặc chọn móng rồi **Delete**), **Xoá tất cả móng**, **Thiết kế lại** (xoá hết rồi thiết kế lại từ đầu). "
                + "Xoá bỏ kết quả móng đơn / đài cọc / móng băng / dầm móng, kích thước đã chốt, bố trí cọc tự chọn, thép đã chọn của móng; giữ thông số đất nền, loại móng, ranh giới đất, lệch tâm. Tab Móng trên thanh lệnh có \"Xoá móng đã thiết kế…\", \"Thiết kế lại móng\".",
                "**Sửa móng trực tiếp trên mặt bằng móng**: bấm đúp vào móng (hoặc chọn rồi **E** / Enter, nút **Sửa móng…**) → sửa L × B × H, loại móng tại chân cột → thiết kế lại với kích thước đó (0 = để chương trình tự chọn).",
                "Mặt bằng móng: kéo trái / giữa / phải di chuyển, con lăn thu phóng, **F** / bấm đúp chuột giữa vừa màn hình.",
                "Mọi hình vẽ của giai đoạn thiết kế – mặt cắt móng, biểu đồ cấu kiện, dạng dao động, bố trí cọc, cầu thang, phác thép, móng lệch tâm, tờ dàn trang: **con lăn thu phóng vào con trỏ**, **kéo chuột giữa / phải di chuyển**, **F / bấm đúp chuột giữa** về vừa khung. "
                + "Sửa nét, chữ, nhãn thép trên bản vẽ hồ sơ: **Hiệu chỉnh bản vẽ…**.");

        c.Sec("khoi-tao.dat-nen-tcvn", "Chỉ tiêu cơ lý đất theo bảng tra TCVN 9362:2012 – lớp đất cấu tạo tiêu chuẩn, hố khoan và thí nghiệm")
            .P("Khi chọn lớp đất, chương trình mặc định lấy chỉ tiêu cơ lý theo **lớp cấu tạo tiêu chuẩn** (loại đất + trạng thái) và **bảng tra TCVN 9362:2012**: "
               + "c, φ của đất cát theo hệ số rỗng e; c, φ của đất dính theo dải độ sệt IL và e; mô đun biến dạng E của đất kỷ Đệ tứ; áp lực tính toán quy ước R0; γ = γw·(Gs + Sr·e)/(1 + e).")
            .Bullets("**Tab Địa chất**: \"+ Lớp\" tạo lớp theo lớp tiêu chuẩn; cột **Lớp tiêu chuẩn (tra bảng)** chọn nhanh (cát sỏi / thô / vừa / nhỏ / bụi chặt – chặt vừa – rời; cát pha cứng – dẻo; sét pha, sét cứng → dẻo chảy; sỏi sạn; đất đắp, bùn, đá – ngoài bảng tra). "
                + "Đổi **loại đất, e, IL, Sr** → chỉ tiêu tự tra lại (nội suy tuyến tính theo e). Nút **Chọn lớp đất theo bảng tra TCVN…** xem toàn bộ danh mục với γ, φ, c, E, R0.",
                "**Có hố khoan khảo sát và thí nghiệm**: nhập trực tiếp γ, φ, c, E vào bảng lớp đất → cột **Chỉ tiêu theo** tự chuyển sang \"Thí nghiệm\" (giữ nguyên số nhập). Chọn lại \"Bảng tra TCVN\" hoặc nút \"Tra lại bảng cho lớp chọn\" để trở về bảng tra.",
                "**Tab Công trình** (chưa dùng hố khoan): ô **Lớp đất (bảng tra TCVN 9362)** điền φ, c, γ, E của nền dưới móng; sửa tay φ, c, γ → tự chuyển \"theo thí nghiệm\".",
                "**Hệ số ktc** trong công thức R = m1·m2/ktc·(A·b·γ + B·h·γ' + D·c): 1,1 khi chỉ tiêu lớp đặt móng tra bảng, 1,0 khi theo thí nghiệm trực tiếp (TCVN 9362:2012). Thuyết minh địa chất ghi nguồn chỉ tiêu từng lớp.",
                "Giới hạn bảng tra: cát rời không có R0; đất dính IL > 0,75 (dẻo chảy, chảy), bùn, đất đắp, đá: ngoài bảng – chương trình cảnh báo, bắt buộc khảo sát. N (SPT) của lớp tiêu chuẩn chỉ là ước lượng theo trạng thái.");

        c.Sec("khoi-tao.dia-danh-moi", "Địa điểm xây dựng theo địa danh sau sắp xếp (34 tỉnh / thành, 3.321 xã / phường / đặc khu)")
            .P("Tab Công trình → Địa điểm: chọn tỉnh / thành (gõ được tên tỉnh cũ, vd Bắc Kạn → Thái Nguyên), ô **Xã/phường/đặc khu** liệt kê đủ đơn vị hành chính cấp xã sau sắp xếp từ 01/7/2025 "
               + "(danh mục theo QĐ 19/2025/QĐ-TTg và các nghị quyết sau đó). Địa danh chưa có số liệu gió: tra Bảng 5.1 QCVN 02:2022 rồi chọn vùng gió – chương trình lưu lại cho lần sau.");

        c.Sec("khoi-tao.truyen-tai-goi-y-mong", "Truyền tải trọng xuống chân cột và gợi ý phương án móng (sau khi chất tải)")
            .P("Tab **Mô hình móng** (hoặc ribbon Móng) → **Truyền tải & gợi ý móng…**. Không cần chạy phân tích trước (có kết quả phân tích thì so sánh thêm).")
            .Bullets("**Tải lấy đúng như đã chất**: TLBT sàn, dầm, cột; lớp hoàn thiện; tường (tự sinh từ tường kiến trúc); cầu thang; hoạt tải. Mỗi phần tải dồn về **cột gần nhất đỡ sàn** (diện chịu tải), cộng dồn từ mái xuống chân cột; cột đặt trên dầm dồn xuống cột dưới gần nhất.",
                "**Tab Tải từng sàn**: diện tích, G, P, (G + P)/m² và thành phần (kN/m²) – nhà phố BTCT thường 9–13 kN/m²/sàn; > 16 kN/m² chương trình cảnh báo chất tải trùng.",
                "**Tab Tải chân cột**: diện chịu tải, G, P, hệ số giảm hoạt tải ψ (ψA1 = 0,4 + 0,6/√(A/9), ψn1 = 0,4 + (ψA1 − 0,4)/√n), Ntc, Ntt; so với phản lực tổ hợp TTGH I của khung (cột Khung/truyền tải).",
                "**Gợi ý móng từng cột**: móng đơn b × b (R theo TCVN 9362 tại chân cột, H theo chọc thủng), móng băng theo dãy cột dọc phương dài nhà (B, bản móng, sườn), móng cọc ép 200/250/300 ([P] theo hố khoan SPT hoặc nhập tay, n = ⌈1,1·Ntt/[P]⌉ kể trọng lượng đài) – thứ tự ưu tiên móng đơn → băng → cọc; nền yếu → cọc.",
                "**Áp dụng**: chọn phương án; mặc định bật \"thiết kế móng theo tải đứng\" (mô men chân cột do giằng móng chịu, bỏ tổ hợp gió) rồi thiết kế lại móng – kết quả móng sát thực tế nhà phố. Hai tuỳ chọn này cũng có trong hộp thoại Móng.",
                "Vì sao móng theo phân tích khung thường lớn: tổ hợp có gió, mô men chân cột ngàm, phân phối lại nội lực khung và lấy cột lớn nhất cho mọi cột. Truyền tải cho tải từng cột, tải đứng – dùng để chọn phương án và kiểm tra lại mô hình.",
                "**Xuất thuyết minh Word**: tải từng sàn, tải chân cột, móng đơn / băng / cọc và đề xuất.");

        c.Sec("khoi-tao.coc-ep-ho-mong", "Cọc ép theo lực ép đầu cọc – số cọc từng hố móng – cột sát 1 ranh / 2 ranh")
            .P("Trong cửa sổ **Truyền tải & gợi ý móng**: nhập **lực ép đầu cọc Pép (tấn)** của máy ép thực tế (mặc định 50 t) và hệ số **k = Pép/[P]** (mặc định 2,0 theo TCVN 9394), bấm **Tính lại**.")
            .Bullets("**[P] = Pép/k**: 50 t, k = 2 → [P] = 25 t = 250 kN/cọc. Tiết diện cọc tự chọn theo lực ép (≤ 40 t: 200×200; ≤ 70 t: 250×250; ≤ 110 t: 300×300), kiểm tra **độ bền vật liệu cọc Pvl = Rb·Ab + Rsc·As ≥ Pép** (cọc không vỡ khi ép). Có hố khoan: so sánh thêm [P]đn của đất ở chiều dài dự kiến.",
                "**Tab Hố móng – số cọc**: tải truyền về **từng hố móng** (mỗi chân cột một đài; các đài chạm nhau – khe < 0,2 m – gộp thành đài chung), **n = ⌈(1,1·ΣNtt·R1/N + Gđài)/[P]⌉**, cách bố trí cọc (hàng × cột), kích thước đài.",
                "**Cột sát ranh đất**: chương trình phân loại **giữa / sát 1 ranh (lệch tâm 1 phương) / sát 2 ranh – cột góc (lệch tâm 2 phương)**. Ranh lấy theo ranh đã khai báo; nếu chưa có, **Ranh đất: tự động** = hai cạnh dài nhà phố (giáp nhà bên). Bỏ chọn \"tự động\" để tự tích các cạnh trái / phải / trước / sau giáp nhà bên.",
                "Cột sát ranh: đài (móng) **lệch tâm vào trong lô**, tim cọc cách ranh ≥ khoảng lùi ép cọc (mặc định 0,5 m); xếp cọc **một hàng dọc ranh** khi ≤ 3 cọc để giảm lệch tâm. **Dầm móng cân bằng** nối cột trong: **R1 = N·Ls/(Ls − e)** mỗi phương (cột góc nhân cả hai phương); không có cột trong trong phạm vi 6 m → đài tự cân bằng, tăng tải.",
                "Móng đơn cột sát ranh: đáy b × b mép sát ranh, e = b/2 − khoảng cách tim cột tới ranh, b tính với R1.",
                "**Áp dụng: Móng cọc**: ghi [P], loại cọc, lực ép; ghi ranh đất (theo trục ngoài cùng) và bật lùi cọc khỏi ranh để phần thiết kế đài tự lệch tâm. Thuyết minh Word có bảng hố móng.");

        c.Sec("khoi-tao.kieu-bo-tri-thep-dam", "Kiểu bố trí thép dầm (A / B / C) và chọn số thanh thép")
            .Bullets("**Kiểu A** (mặc định): 2 thanh góc chạy suốt; thép tăng cường dưới cắt cách mép gối 0,1·Lo; mũ gối giữa vươn Lo/3, gối biên Lo/4.",
                "**Kiểu B**: toàn bộ thép dưới và thép trên **chạy suốt** dầm (thanh tăng cường lớn nhất các nhịp, mũ gối lớn nhất các gối), neo vào gối biên – thi công đơn giản, tốn thép hơn.",
                "**Kiểu C**: như A nhưng **mũ gối vươn Lo/3 ở mọi gối** (kể cả gối biên); thép tăng cường dưới kéo vào gối ≥ 15d.",
                "Chọn **kiểu cho từng dầm**: Thiết kế → Dầm → **Chọn bố trí thép…** → vùng \"Kiểu bố trí thép dọc (A / B / C)\" – mỗi kiểu có khối lượng thép (kg) để so sánh. **Kiểu mặc định cả công trình**: Thiết kế → Tuỳ chọn chọn thép & gia công.",
                "**Số thanh thép** từng vùng (thép dưới từng nhịp, thép trên từng gối, thép chạy suốt, đai): chọn trong cùng hộp thoại – danh sách phương án đều thoả As yêu cầu và khoảng hở TCVN 5574; chương trình thiết kế lại và kiểm tra.");

        c.Sec("khoi-tao.ban-ve-tung-dam", "Bản vẽ riêng từng dầm")
            .Bullets("Thiết kế → tab **Dầm** → **Bản vẽ từng dầm…** (hoặc menu Thiết kế): danh sách dầm D1, D2… (tầng, tiết diện, số nhịp, SL).",
                "Chọn một dầm → xem trước bản vẽ riêng: mặt đứng (dầm dài tự chia đoạn tại gối), các mặt cắt, **bảng thống kê cốt thép của riêng dầm đó**; phóng to / thu nhỏ.",
                "**Chọn bố trí thép dầm này…**: đổi kiểu A / B / C, số thanh, đai – bản vẽ cập nhật sau khi thiết kế lại.",
                "**Xuất bản vẽ dầm này…** (DXF / DWG, có khung tên \"CHI TIẾT DẦM D1 (SL=…)\"), hoặc **Xuất mỗi dầm một tệp…** vào một thư mục.");

        c.Sec("khoi-tao.autocad-hai-chieu", "AutoCAD hai chiều: mặt bằng + mặt đứng → mô hình, tự gán tải, tính toán → kết quả và bản vẽ về AutoCAD")
            .Steps("**Trong AutoCAD** (add-in Việt Const, gõ VXT mở bảng lệnh): chuẩn hoá layer (VXT_CHUANLAYER) hoặc tích nhóm đối tượng: VXT_TUONG (tường), VXT_COT, VXT_DAM, VXT_SAN, VXT_TRUC, VXT_CUA, VXT_THANG. Có thể vẽ trước MẶT BẰNG KẾT CẤU (cột, dầm, sàn) – chương trình dùng đúng vị trí đó.",
                "**VXT_MATDUNG**: quét chọn mặt đứng / mặt cắt (chữ cao độ +3.600, ±0.000, COS +7.200 và tên tầng) → cao độ, chiều cao các tầng đã khai báo (VXT_TANG). Chưa khai báo tầng: tạo các tầng điển hình dùng chung một mặt bằng (chọn khung + điểm gốc).",
                "**VXT_GUI**: gửi dữ liệu sang Việt Const (file cục bộ %AppData%\\KetCauVXT\\Link). Việt Const tự hỏi mở hộp thoại triển khai kết cấu: xem lưới trục, cột, dầm, ô sàn đề xuất → Tạo mô hình. Tải tường (theo vị trí tường, chiều cao tầng, trừ cửa), tải hoàn thiện + hoạt tải sàn tự gán.",
                "Hộp **Số liệu tính toán – gió, địa chất**: chọn vùng gió (QCVN 02:2022), dạng địa hình, nền đất theo bảng tra TCVN 9362 (hoặc hố khoan ở tab Địa chất) → chương trình chạy phân tích nội lực, thiết kế thép, móng, tự tăng tiết diện cấu kiện không đạt.",
                "Không dùng AutoCAD: Tệp → Nhập bản vẽ kiến trúc → chọn file DXF/DWG mặt bằng, nút **Đọc mặt đứng...** chọn file mặt đứng / mặt cắt → chiều cao tầng gán cho các mặt bằng.",
                "**Kết quả về AutoCAD**: Tệp → *Gửi kết quả về AutoCAD* (hoặc tab Triển khai → *Kết quả về AutoCAD…*). Trong AutoCAD gõ **VXT_NHAN**: vẽ tên cấu kiện, tiết diện, thép dầm / cột / sàn, khung móng / đài cọc lên lớp VXT-KQ-COT, -DAM, -SAN, -MONG đúng vị trí trên bản vẽ gốc (có bản vẽ liên kết) hoặc tại điểm chèn.",
                "**VXT_CHEN**: chèn bản vẽ triển khai dạng **chi tiết rời** – không lồng khung tờ, khung tên; mỗi chi tiết (mặt bằng, mặt đứng dầm, mặt cắt, bảng thép…) nằm trong một khung chữ nhật lớp KHUNG_CHI_TIET để phân biệt. Người dùng tự dàn trang in (Layout / viewport). Xuất bản vẽ DXF trong chương trình cũng có tuỳ chọn \"Chi tiết rời – không lồng khung tên\".");

        c.Sec("khoi-tao.chi-chon-dat-ten", "Nút Chỉ chọn, Esc, quét chọn và đặt tên cấu kiện cùng loại")
            .Bullets("**Chỉ chọn** (nút đầu tiên bên trái ribbon, và trên thanh công cụ mặt bằng cạnh nút Chọn): thoát mọi lệnh vẽ / đặt đang chạy, về chế độ Chọn và **khoá kéo dời** – bấm / quét chỉ để chọn, không di chuyển, không tạo đối tượng. Bấm lần nữa để mở khoá (kéo tay nắm sửa đối tượng).",
                "**Esc**: đang vẽ dầm, cột, sàn, tường, cửa… một lần Esc là thoát hẳn lệnh, về ngay chế độ Chọn.",
                "**Quét chọn** ở chế độ Chọn: kéo khung **trái → phải** chọn đối tượng nằm trọn trong khung; **phải → trái** chọn đối tượng chạm khung; Ctrl + kéo thêm, Shift + kéo bỏ. Di chuyển khung nhìn bằng nút giữa chuột.",
                "**Đặt tên cấu kiện…** (ribbon Kết cấu → Khung, tab Triển khai → Cốt thép, menu Thiết kế): gán **một tên** (C1, D1, M1…) cho các cấu kiện **cùng tiết diện và cùng chiều dài** – cột so cả chồng cột (tiết diện, chiều cao từng tầng); dầm so cả dải dầm (tiết diện, chiều dài từng nhịp, sai số 10 mm); móng đơn so L × B × H.",
                "**Tự đặt tên theo nhóm**: chương trình tự gom và đặt tên cả công trình (tuỳ chọn dầm đặt tên riêng từng tầng, giữ tên đã gán tay). **Gán tên cho đối tượng đang chọn**: chọn (bấm / quét) trên mặt bằng, nhập tên – khác tiết diện hoặc chiều dài thì chương trình báo và hỏi trước khi gán. Móng: chọn ở tab Mô hình móng; gán chung tên → đồng nhất kích thước theo móng lớn nhất.",
                "Khi thiết kế, các cấu kiện cùng tên dùng chung **một phương án thép** – phương án nhẹ nhất bao được As yêu cầu của mọi cấu kiện trong nhóm (có dòng kiểm tra \"Nhóm tên …\"); bản vẽ một hình, SL = số cấu kiện. Sửa mô hình làm một tên lẫn tiết diện / chiều dài → hộp thoại cảnh báo để đặt tên lại.");

        c.Sec("khoi-tao.esc-thoat-lenh", "Phím Esc thoát lệnh vẽ cấu kiện")
            .Bullets("Đang dùng lệnh vẽ (＋ Dầm, dầm cong, tường, ô sàn, ban công, cầu thang, cột, lan can, chi tiết kiến trúc, đặt cửa / đồ đạc, vẽ trục, mặt cắt, lệnh gõ ở dòng lệnh, sửa dầm, "
                + "vẽ đường chuẩn / móng đa giác / nhân bản móng): bấm **Esc một lần** để thoát hẳn lệnh, trở về chế độ **Chọn** – kể cả khi con trỏ nhập đang ở ô chữ của thanh lệnh hoặc dòng lệnh.",
                "Lan can đang vẽ dở (≥ 2 điểm) được giữ lại khi Esc; điểm đầu chưa thành đoạn bị bỏ.",
                "Đang sửa ô bảng hoặc danh sách thả xuống đang mở: Esc chỉ huỷ ô / đóng danh sách đó. Không có lệnh nào: Esc bỏ chọn đối tượng như trước.",
                "Chế độ Triển khai hồ sơ (mặt bằng vị trí cấu kiện): Esc cũng thoát lệnh đặt cấu kiện về \"Chọn\".");

        c.Sec("khoi-tao.chon-nhieu-mong", "Chọn nhiều móng (Ctrl + bấm) – gán nhiều móng / đài cọc giống nhau")
            .Bullets("Mặt bằng móng: **Ctrl + bấm** thêm / bỏ móng, đài cọc hoặc chân cột vào danh sách chọn (khung cam nét đứt); **Ctrl + A** chọn mọi móng cùng loại; **Esc** bỏ chọn.",
                "**Sửa móng…** (hoặc E / Enter): cùng L × B × H, loại móng cho mọi móng chọn. **Xoá móng chọn** (Delete), **Bố trí móng…** áp dụng cho tất cả.",
                "**Bố trí cọc…**: thêm lựa chọn \"Áp dụng cho tất cả … chân cột chọn thêm (Ctrl + bấm)\" – cùng sơ đồ cọc, kích thước đài.",
                "**Gán đài cọc giống nhau**: bấm (Ctrl) các đài / chân cột cần gán, bấm đài nguồn sau cùng → chép toạ độ cọc + kích thước đài của đài nguồn cho tất cả (chân cột chưa là móng cọc được chuyển sang móng cọc), thiết kế lại; đài giống hệt gộp chung tên.",
                "Đài cọc gộp giống hệt (SL > 1) nay được vẽ ở mọi vị trí trên mặt bằng móng.",
                "Thanh lệnh các tab (Móng, Dầm, Sàn… và ribbon Kiến trúc / Kết cấu): mọi nút hiện đủ trong khung nhìn – tự xuống dòng khi khung hẹp, không cần kéo thanh cuộn; tiêu đề tab xuống dòng thay cho nút ».");

        c.Sec("khoi-tao.bo-tri-mong", "Bố trí móng: nhân bản, đồng nhất, lệch tâm / xoay dóng mép cột, đường chuẩn ranh đất xiên, móng đa giác cho đất méo")
            .P("Tab **Mô hình móng**, các nút trên thanh công cụ mặt bằng móng. Mọi thay đổi có hoàn tác Ctrl+Z; bấm \"Thiết kế móng\" (hoặc chọn \"Thiết kế lại ngay\") để kiểm tra áp lực nền, chọc thủng, uốn, thép.")
            .Bullets("**Móng giống nhau**: các móng giống hệt được gộp chung một tên (M2, SL = 6) và **vẽ ở mọi chân cột** trên mặt bằng móng, 3D, bản vẽ bố trí móng.",
                "**Nhân bản móng…**: chọn móng nguồn → chép kích thước L × B × H (chốt) và bố trí (góc, dóng mép cột, lệch tâm, đa giác) sang: các chân cột **bấm chọn trên mặt bằng** (Enter áp dụng), mọi chân cột cùng tiết diện cột, mọi chân cột móng đơn, hoặc danh sách nút (3, 5, 8-11).",
                "**Đồng nhất móng…**: gom các móng có cạnh chênh ≤ dung sai (mặc định 0,3 m), cùng tiết diện cột, thành một loại – lấy L, B, H lớn nhất của nhóm → ít loại móng, thi công đồng loạt.",
                "**Bố trí móng…** (móng / chân cột đang chọn): **góc móng** song song trục X/Y, **theo mép cột** (cột xoay thì móng xoay), góc nhập tay, hoặc theo đường chuẩn gần nhất; "
                + "**dóng phương L / B**: đúng tâm, **mép móng thẳng mép cột** phía âm / dương (cột sát ranh: móng chìa vào trong lô đất), hoặc lệch tâm nhập tay (tâm móng − tim cột, m). Áp dụng được cho mọi chân cột cùng tiết diện.",
                "**Móng lệch tâm**: khi trọng tâm đáy móng lệch tim cột > 50 mm, mặc định **nối dầm móng cân bằng** tới chân cột đối diện theo hướng lệch tâm (≤ nhịp tối đa trong Ranh giới đất): R1 = N·Ls/(Ls − e) đặt tại trọng tâm đáy, dầm chịu M = N·e, Q = ΔR (ghi trong kết quả, vẽ trên mặt bằng). "
                + "Bỏ chọn \"Dầm móng cân bằng\" = móng **tự cân bằng**: đáy móng chịu mô men N·e (áp lực không chịu kéo, vùng tiếp xúc ≥ 75%).",
                "**Vẽ đường chuẩn**: bấm lần lượt các điểm – bắt dính **góc cột** (lấy góc cột ở vị trí góc đất), giao trục, đầu / giao điểm đường chuẩn khác; mỗi đoạn là một đường ranh đất xiên. Esc / Enter kết thúc, Backspace bỏ điểm. "
                + "**Đường chuẩn…**: sửa toạ độ X1 Y1 X2 Y2, khe hở móng tới đường, bật / tắt, tên. Móng không vượt qua đường chuẩn: móng bị cắt theo đường (hình thang / ngũ giác…), tự tăng kích thước tới khi đạt áp lực nền.",
                "**Vẽ móng đa giác**: bấm các đỉnh đáy móng bao quanh chân cột (bắt dính góc cột, góc đất), Enter hoặc bấm lại đỉnh đầu để kết thúc → móng hình dạng bất kỳ cho nhà đất méo. Kích thước đa giác do người dùng định; chương trình tính chiều cao (chọc thủng), thép và kiểm tra áp lực.",
                "**Tính toán móng hình dạng**: áp lực đáy móng tuyến tính trên đa giác, không chịu kéo (lặp vùng tiếp xúc); ptb ≤ R, pmax ≤ 1,2R; chọc thủng theo chu vi cách mép cột h0/2 nằm trong đáy móng; mô men công xôn tại mép cột theo phương L, B của móng chia bề rộng mặt cắt thật → As trên 1 m; "
                + "thép cắt theo đường bao, nhóm chiều dài 50 mm (mỗi nhóm một số hiệu). Bản vẽ: mặt bằng móng vẽ đường bao thật + đường chuẩn + dầm cân bằng; chi tiết \"MÓNG HÌNH DẠNG\" vẽ theo hệ trục của móng với chiều dài từng cạnh; khối lượng V = A·H.",
                "Giới hạn: móng hình dạng là móng đế phẳng (không vát); móng gia cố cừ tràm và móng kết hợp chưa hỗ trợ hình dạng; dầm móng cân bằng chỉ ghi nội lực (M, Q) để thiết kế dầm móng.");

        c.Sec("khoi-tao.chieu-cao-tang", "Sửa chiều cao tầng sau khi đã lập dự án – điều hướng 3D")
            .Bullets("**Chiều cao tầng…** (tab BIM → Cao độ, tab Lưới – Tầng, menu Mô hình, nút cạnh ô \"Chiều cao các tầng\" ở tab Công trình): bảng các tầng (tên, chiều cao, cao độ sàn tự tính) và cao độ chân cột – "
                + "áp dụng thì cột, dầm, sàn, tường, cầu thang, ống điện nước dời theo cao độ mới, giữ nguyên mô hình; đổi tên tầng cập nhật mọi cấu kiện của tầng. Có hoàn tác Ctrl+Z.",
                "Sửa trực tiếp ô **Chiều cao các tầng** / **Cao độ chân cột** ở tab Công trình khi đã có mô hình: rời ô → chương trình hỏi áp dụng ngay cho mô hình (giữ nguyên cấu kiện).",
                "Thêm / bớt tầng: **Chèn tầng…** / **Xoá tầng** (BIM → Cao độ). Sau khi đổi chiều cao cần chạy lại phân tích.",
                "**3D**: kéo chuột trái = xoay (bấm = chọn); kéo chuột **giữa** hoặc **phải** = di chuyển (pan); **Shift + kéo giữa** = xoay; **con lăn** = thu phóng vào điểm dưới con trỏ; "
                + "**F / Home / bấm đúp chuột giữa / nút Vừa màn hình** = thu phóng vừa toàn mô hình; **Q** bật quét chọn, **Ctrl / Shift + kéo trái** quét thêm / bớt.");

        c.Sec("khoi-tao.hoan-thien-kt", "Chi tiết kiến trúc kiểu Revit: mái, trần, lan can, phào chỉ, ốp – sơn, chép tầng, nhập mô hình SketchUp")
            .P("Nhóm **BIM → Hoàn thiện KT** (cũng có ở menu BIM). Mọi phần tử hiện trên mặt bằng, 3D, IFC / SketchUp / OBJ, bảng khối lượng; có hoàn tác Ctrl+Z.")
            .Bullets("**Mái…**: mái bằng, 1 dốc, 2 dốc (tự có 2 tường thu hồi 220), 4 dốc (mặt bằng vuông → mái chóp); độ dốc %, đua mái, chân mái, đường bao (mặc định = hộp bao ô sàn tầng). "
                + "Tuỳ chọn **dồn tải lớp mái** lên các ô sàn trong đường bao khi phân tích (tôn ~0,3, ngói ~0,9 kN/m² mặt bằng).",
                "**Trần theo phòng…**: một trần cho mỗi phòng của tầng (cao độ, loại trần) – cần **Tìm phòng** trước.",
                "**Lan can**: chọn loại (inox tay vịn gỗ, kính cường lực, sắt hoa văn, xây 110) và chiều cao, bấm các điểm trên mặt bằng, **Enter** kết thúc, **Backspace** bỏ điểm cuối, **Esc** dừng. "
                + "Khi phân tích, tải lan can (0,3 / 0,5 / 0,4 kN/m; xây ≈ 2,52·H kN/m) tự dồn lên dầm đỡ bên dưới, phần không có dầm → tải phân bố lên ô sàn.",
                "**Phào chỉ…**: mẫu phào chân tường, gờ chỉ ngang tầng, gờ bậu cửa sổ, gờ chắn nước, phào trần, chỉ đỉnh tường chắn mái; bấm sát **mặt tường** cần đặt (phía bấm = mặt tường) hoặc đặt cho mọi tường biên (mặt ngoài) / mọi tường (mặt trong). Phào tự cắt qua ô cửa.",
                "**Ốp / sơn tường…**: ốp đá granite, gạch thẻ, gạch men WC, gỗ, sơn màu nhấn trong khoảng cao độ chọn; diện tích tự trừ ô cửa.",
                "**Chép KT sang tầng…**: tường (kèm cửa, phào, ốp), phòng, trần, lan can, đồ đạc của tầng đang xem sang các tầng khác (Revit: Copy to Levels); tường trùng vị trí được bỏ qua.",
                "**KL hoàn thiện KT…**: bảng khối lượng mái (m² nghiêng, tường thu hồi), trần (m²), lan can (md), phào chỉ (md), ốp / sơn (m²) theo tầng – xuất CSV.",
                "**Nhập mô hình SketchUp / Revit…**: mô hình 3D OBJ / DAE / *.vxtmesh.json → chương trình tự tìm cao độ sàn, cắt ngang mỗi tầng ở +1,2 m lấy tường, cột (vòng kín ≤ 0,8 m), cửa đi / cửa sổ (khe hở trên tường), "
                + "ghi bản vẽ DXF rồi mở bảng **Nhập bản vẽ kiến trúc** để nhận diện và triển khai kết cấu (cột, dầm, sàn, tải) như với bản vẽ CAD. SketchUp xuất OBJ thường \"Y lên\" – để **Tự nhận** hoặc chọn trục đứng.")
            .Bullets("**Mái nhà chữ L, T, U**: chọn đường bao \"Theo đường bao ô sàn\" – mái chia theo khối, mái cánh chạy vào mái chính tới đường nóc (giao nóc như mái thật); tải mái chỉ dồn lên ô sàn nằm trong đường bao.",
                "**Lan can – tải ngang** tay vịn theo TCVN 2737: nhà ở 0,3; công trình công cộng 0,8; khán đài 1,5 kN/m (hoạt tải HT, hướng ra ngoài) dồn lên dầm đỡ cùng tải đứng.",
                "**Họ đồ đạc tham số** (Thư viện 3D, nguồn \"Họ tham số\"): bàn ăn, bàn trà, bàn làm việc, ghế, giường đôi / đơn, tủ quần áo, sofa, kệ tivi, tủ bếp dưới / trên, bàn thờ, bồn cầu, lavabo, bồn tắm – chọn đồ đạc trên mặt bằng, bấm **E** (hoặc Enter) sửa Rộng × Sâu × Cao, cao độ, góc xoay: chân bàn, đầu giường, tay sofa… tự dựng lại.",
                "**Nhập mô hình**: nhận thêm vùng sàn, ban công, lỗ sàn (layer SAN), cầu thang (cụm mặt bậc, layer THANG) và mái dốc từ các mặt nghiêng (độ dốc, 1 / 2 / 4 dốc) – sau khi triển khai kết cấu, mái được thêm theo đường bao sàn tầng trên cùng.",
                "**Plugin SketchUp…**: cài VietConst_SketchUp.rbz (thư mục SketchUp trong thư mục cài) bằng Window → Extension Manager → Install Extension; Extensions → Việt Const → Xuất… ghi *.vxtmesh.json (m, Z lên, theo tag) để nhập trực tiếp, không phải chọn đơn vị / trục.")
            .Bullets("**Lan can – xoắn dầm biên**: tải ngang tay vịn đặt cao hơn trọng tâm dầm e = chân lan can + H + h_dầm/2 → mômen xoắn phân bố mt = q_ngang·e (hoạt tải HT) trên dầm đỡ; bộ giải tính xoắn phân bố / tập trung quanh trục thanh, thiết kế dầm kiểm tra xoắn (cốt dọc + đai chịu xoắn).",
                "**Thư viện từ phần mềm khác – cách chính xác nhất**: dùng công cụ xuất riêng của từng phần mềm (giữ đúng điểm chèn, kích thước thật, m, trục Z lên, nhóm theo tag / layer / loại họ): "
                + "SketchUp → \"Đưa component đang chọn vào thư viện Việt Const\"; 3ds Max → VietConst_Export.ms; Revit → \"Đưa họ vào thư viện Việt Const\". "
                + "Không có công cụ: xuất OBJ / DAE / DXF 3D (3DFACE, lưới đa diện POLYLINE, MESH; đơn vị theo $INSUNITS) rồi Thư viện 3D → Nhập….",
                "**Ký hiệu mặt bằng** của mục thư viện lấy theo hình chiếu bằng của lưới (giữ đúng hình bàn chữ L, giường, bồn cầu…), co giãn theo Rộng × Sâu; họ tham số vẽ từng chi tiết (mặt bàn, chân, đầu giường…).")
            .Tip("Dựng nhanh mô hình khối trong SketchUp (sàn, tường, cột, ô cửa) → Export OBJ → Nhập mô hình → kiểm tra mặt bằng nhận diện → triển khai kết cấu → chạy phân tích, thiết kế BTCT.");

        c.Sec("khoi-tao.dat-va-xem", "Đặt đồ đạc bằng bắt điểm, kích thước tạm, thao tác 3D, mặt cắt tầng và mặt cắt công trình")
            .P("Như Revit: **không nhập toạ độ X, Y**. Chọn đồ đạc / thiết bị rồi rê chuột trên mặt bằng – chương trình tự nhận diện vị trí và hiện **kích thước tạm** màu xanh; bấm vào con số để nhập khoảng cách chính xác.")
            .Steps("Tab **Đặt & xem → Đặt đồ đạc…** (hoặc BIM → Thư viện 3D… → **Đặt vào mặt bằng (bắt điểm)**): chọn mục thư viện.",
                "Rê chuột: bóng đồ đạc tự **bám mặt tường** (lưng tựa mặt tường hoàn thiện, khe 10 mm, tự xoay theo phương tường), **góc phòng** (đầu tường / mặt tường vuông góc), **giao trục**, **cạnh đồ đạc** khác. **Space** xoay 90°. Bấm để đặt, bấm tiếp để đặt thêm, **Esc** dừng.",
                "Chọn đồ đạc trên mặt bằng: kích thước tạm tới mặt tường / trục / đồ đạc gần nhất ở 4 phía – **bấm số → nhập mm** → đồ đạc dời đúng khoảng cách (Ctrl+Z hoàn tác). **R / Space** xoay, **Delete** xoá.",
                "Môi trường Điện nước: khi đặt thiết bị hiện bóng trên mặt tường gần nhất + kích thước tạm tới đầu tường / ô cửa; chọn thiết bị gắn tường → bấm số kích thước tạm để sửa vị trí.")
            .Bullets("**3D**: bấm chọn đồ đạc / thiết bị điện nước; **kéo** đồ đạc đang chọn để dời trên mặt sàn (vẫn bắt mặt tường); thanh nổi: **Xoay 90°**, **Cao độ…** (đáy đồ đạc / chiều cao lắp thiết bị), **Xoá**, **Ẩn**, **Cô lập**, **Hiện tất cả**, **Hộp cắt** (chỉ hiện tới tầng đang xem). Bộ môn tắt hiển thị cũng ẩn trong 3D.",
                "**Mặt cắt ngang tầng**: Trình duyệt dự án → *Mặt cắt ngang tầng (theo bộ môn)* – mỗi cao độ (nền tầng 1, sàn tầng 2…, mái) có một khung nhìn KT / KC / DN (mặt phẳng cắt cao 1,2 m), tự sinh lại khi chèn / xoá tầng.",
                "**Mặt cắt công trình**: **1-1** (theo phương X, qua giữa nhịp) và **2-2** (theo phương Y) tự sinh – tường cắt trừ ô cửa, dầm, sàn, cột tô đặc; tự ghi **cao độ** các mức (±0.000, +3.600…), **chuỗi kích thước chiều cao tầng + tổng**, **trục và kích thước trục**. **Đặt & xem → Mặt cắt**: bấm 2 điểm trên mặt bằng để thêm mặt cắt (3-3, 4-4…).",
                "Hồ sơ Kiến trúc (Hồ sơ theo bộ môn) có thêm các tờ **MẶT CẮT 1-1, 2-2…**; khung nhìn mặt cắt có nút **Xuất DXF…**.")
            .Tip("Kích thước tạm đo tới **mặt tường hoàn thiện** (kể cả lớp trát / ốp) – đúng cách đo khi thi công lắp đồ.");

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

        c.Sec("mo-hinh.quet-chon", "Quét chọn mọi đối tượng (khung bao / khung cắt)")
            .P("Ở chế độ **Chọn** của tab Mặt bằng, kéo chuột trái trên vùng trống để quét chọn – áp dụng cho cột, dầm, ô sàn, tường, cửa, đồ đạc, thiết bị và tuyến điện nước, trục, đường mặt cắt của tầng đang xem (mọi tầng khi xem tất cả).")
            .Table(new[] { "Thao tác", "Kết quả" },
                new[] { "Kéo trái → phải (khung xanh dương, nét liền)", "Khung bao: chỉ chọn đối tượng nằm trọn trong khung." },
                new[] { "Kéo phải → trái (khung xanh lá, nét đứt)", "Khung cắt: chọn mọi đối tượng chạm khung." },
                new[] { "Ctrl + kéo", "Thêm vào nhóm đang chọn." },
                new[] { "Shift + kéo", "Bỏ bớt khỏi nhóm đang chọn." },
                new[] { "Delete", "Xoá cả nhóm – một bước hoàn tác (Ctrl + Z)." },
                new[] { "Esc", "Bỏ chọn." })
            .Bullets("Thanh trạng thái báo số đối tượng theo loại, vd. \"Đã chọn: 5 dầm, 3 cột, 2 tường\".",
                "Tab **Trục & tầng** → **Bộ lọc…**: đánh dấu các loại cần giữ (kiểu Revit), bỏ các loại còn lại khỏi nhóm.",
                "Cột, dầm, ô sàn trong nhóm dùng chung cho các lệnh sửa nhiều cấu kiện (gán tải, tiết diện, sao chép…).",
                "Dời khung nhìn bằng nút giữa chuột (kéo trái ở chế độ Chọn dành cho quét chọn).")
            .Bullets("**Quét chọn trong khung nhìn 3D**: tích ô **Quét chọn (kéo khung)** trên thanh nổi của tab 3D rồi kéo chuột trái – trái → phải là khung bao (chọn khối nằm trọn khung), phải → trái là khung cắt (chọn khối chạm khung); Ctrl + kéo thêm, Shift + kéo bớt. Nhóm chọn dùng chung với mặt bằng: thanh trạng thái báo số đối tượng, **Delete** xoá cả nhóm (một bước hoàn tác), **Esc** bỏ chọn, bảng thuộc tính sửa được nhóm như trên mặt bằng. Bỏ tích ô để kéo chuột xoay mô hình như cũ.")
            .Tip("Trong cửa sổ Triển khai hồ sơ, tab Mặt bằng kết cấu, công cụ **Chọn** cũng quét chọn được nhiều vị trí cấu kiện; Shift + bấm thêm / bớt từng vị trí, Delete xoá khỏi mặt bằng.");

        c.Sec("mo-hinh.sua-luoi-truc", "Thêm, dời, xoá lưới trục")
            .Steps("Tab **Trục & tầng** → **Vẽ trục**: bấm điểm đầu rồi điểm cuối trên mặt bằng. Đường gần đứng tạo trục số (1, 2, 3…), đường gần ngang tạo trục chữ (A, B, C…); tên tự đặt tiếp theo trục có sẵn. Bấm tiếp để vẽ trục khác, Esc để dừng.",
                "**Sửa lưới trục…**: chọn trục trong danh sách để đổi tên, **Dời tới toạ độ** (đánh dấu *Kéo theo cấu kiện* để cột, đầu dầm, góc sàn, đầu tường nằm trên trục dời cùng), **Sao chép lệch** một khoảng, thêm trục X / Y theo toạ độ, **Xoá trục** (có cảnh báo khi còn cấu kiện nằm trên trục – cấu kiện giữ nguyên), **Đánh số lại**.",
                "Bấm đúp gần đầu trục (bong bóng) trên mặt bằng để đổi tên nhanh.",
                "Mọi thay đổi trong một lần mở hộp thoại là một bước hoàn tác.")
            .Bullets("Có thể quét chọn trục rồi nhấn Delete để xoá nhiều trục ({s:mo-hinh.quet-chon}).",
                "Triển khai hồ sơ: nút **Thêm trục…** trên mặt bằng kết cấu; chuột phải lên đường trục → dời trục (kéo theo / giữ cấu kiện), thêm trục song song, xoá trục. Ô bước trục tự cập nhật.",
                "**Kéo trục bằng chuột**: ở chế độ **Chọn** (bật nắm kéo), bấm giữ bong bóng trục rồi kéo – đường trục mới hiện nét đứt kèm **kích thước tạm** (mm) tới trục kề hai bên, vị trí bắt bước 10 mm. Thả chuột để dời trục; cột, đầu dầm, góc sàn, đầu tường nằm trên trục dời theo. Giữ **Ctrl** khi thả để chỉ dời trục. Mỗi lần kéo là một bước hoàn tác.",
                "**Bong bóng trục**: chuột phải lên bong bóng → *Cả hai đầu*, *Chỉ đầu dưới / trái*, *Chỉ đầu trên / phải* hoặc *Không bong bóng*.",
                "**Ẩn trục theo tầng**: khi đang xem một tầng, chuột phải lên bong bóng → **Ẩn trục … trên tầng …**; trục vẫn hiện ở các tầng khác và khi xem mọi tầng. **Hiện lại mọi trục đã ẩn** (trên tầng này) trả lại. Thiết lập bong bóng và ẩn trục lưu trong dự án, áp dụng cho mặt bằng trên màn hình và bản vẽ mặt bằng kết cấu xuất DXF / DWG.")
            .Note("Sau khi dời trục có kéo theo cấu kiện, chạy lại phân tích để cập nhật nội lực.");

        c.Sec("mo-hinh.them-tang", "Thêm tầng, sửa cao độ tầng")
            .Steps("Tab **Trục & tầng** → **Thêm tầng…**: chọn tầng tham chiếu, vị trí (phía trên / phía dưới), chiều cao tầng mới và cách chép cấu kiện: *Không chép*, *Chỉ chép cột* hoặc *Chép toàn bộ* (cột, dầm, sàn). Các tầng phía trên được nâng lên tương ứng; thêm tầng trên cùng sẽ nối cột từ đỉnh cột tầng dưới.",
                "**Sửa cao độ…**: chọn tầng, nhập cao độ mới rồi **Đặt cao độ** – nút ở cao độ cũ dời theo nên cột, dầm, sàn co giãn, chiều cao các tầng tự tính lại. Cao độ được giới hạn để mỗi tầng cao ít nhất 0,5 m.",
                "Cũng trong hộp đó: **Đổi tên** tầng, **Xoá tầng**.")
            .P("Các nút **Thêm tầng…** và **Sửa cao độ…** cũng có trên khung nhìn mặt đứng ({s:mo-hinh.mat-dung}).");

        c.Sec("mo-hinh.mat-dung", "Mặt đứng công trình và vẽ trục trên mặt đứng")
            .P("Tab **Trục & tầng** → **Mặt đứng** (hoặc cây dự án → Mặt đứng) mở khung nhìn mặt đứng 4 hướng: trước, sau, trái, phải. Mặt đứng thể hiện đường cao độ các tầng kèm ký hiệu cao độ, trục thẳng đứng có bong bóng ở trên và dưới, dãy kích thước trục, hình chiếu tường, cột, dầm, cửa đi, cửa sổ.")
            .Steps("Chọn hướng nhìn trong danh sách.",
                "Bật **Thêm trục (bấm lên hình)** rồi bấm vào vị trí cần thêm trục: chương trình đổi vị trí bấm sang toạ độ mặt bằng, hiện để sửa cho chính xác, rồi thêm trục số (mặt đứng trước / sau) hoặc trục chữ (mặt đứng trái / phải).",
                "**Kéo đường cao độ**: khi ô Thêm trục đang tắt, bấm giữ gần đường cao độ một tầng (trong khoảng 0,3 m) rồi kéo lên / xuống – dòng thông tin hiện cao độ mới (bắt 10 mm) và chiều cao tầng; thả chuột để đặt. Cột, dầm, sàn co giãn theo, mỗi tầng vẫn cao ít nhất 0,5 m; một lần kéo là một bước hoàn tác.",
                "**Xuất DXF…** lưu bản vẽ mặt đứng đang xem.")
            .Bullets("Bộ hồ sơ kiến trúc (KT) có thêm 4 bản vẽ mặt đứng KT_MatDung_1…4.dxf.",
                "Lưới trục và cao độ dùng chung với mặt bằng ({s:mo-hinh.sua-luoi-truc}, {s:mo-hinh.them-tang}).");

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

        c.Sec("mo-hinh.sua-dam", "Kéo, trim, extend dầm; pick đối tượng CAD")
            .P("Sửa dầm như AutoCAD – không cần nhớ kích thước: các nút ở tab **Dầm** của thanh lệnh trên mặt bằng, gõ lệnh ở dòng lệnh (ST, M, TR, EX) hoặc menu **Vẽ**. Mọi thao tác hoàn tác được (Ctrl+Z); nút giao được chia lại để mô hình chạy phân tích ngay.")
            .Steps("**Kéo dầm** ({m:stretchBeam}): bấm gần ĐẦU dầm cần kéo → bấm điểm mới (bắt nút, giao trục, điểm trên dầm, nét bản vẽ link). Đầu dầm đang nối cột / dầm khác được tách sang nút mới, không kéo theo cấu kiện khác. "
                + "Giữ **Shift** khi bấm dầm để dời cả dầm.",
                "**Dời dầm** ({m:moveBeam}): bấm dầm tại điểm gốc (hoặc dùng các dầm đã *Chọn nhiều*) → bấm điểm đích; dầm dời song song.",
                "**Trim** ({m:trimBeam}): bấm vào đoạn dầm cần bỏ – phần giữa hai dầm / cột giao gần nhất, hoặc phần nhô quá dầm giao, bị cắt; đoạn còn lại giữ tiết diện và tải.",
                "**Extend** ({m:extendBeam}): bấm gần đầu dầm cần kéo dài → bấm dầm đích (kể cả khi phải vượt qua dầm gần hơn), hoặc **chuột phải / Enter** để tự kéo tới dầm vuông góc gần nhất phía trước. Dầm đích được chia tại điểm nối.",
                "**Nối dầm hở** ({m:joinBeams}): mọi đầu dầm hở của tầng đang xem tự kéo dài tới dầm vuông góc gần nhất trong **khoảng nối** (mặc định 1500 mm) – dùng sau khi vẽ nhanh theo bản vẽ kiến trúc.",
                "**Pick CAD** ({m:pickCad}): nạp mặt bằng kiến trúc sơ bộ bằng {m:cadLinks}, chọn ô **tạo**: *Dầm* – bấm giữa hai mép dầm / tường song song (100…600 mm) → dầm đúng trục, bề rộng đo được (tự tạo tiết diện b×h, h theo tiết diện đang chọn); "
                + "bấm sát một nét → dầm theo nét; *Sàn / Cầu thang / Giếng trời / Chỉ dầm bao* – bấm trong đa tuyến kín → dầm bao các cạnh còn thiếu + ô sàn / ô thang / lỗ.")
            .Tip("Vẽ nhanh theo kiến trúc: Pick CAD các dầm → **Nối dầm hở** → Pick CAD *Sàn* trong từng phòng → Pick CAD *Cầu thang* ở ô thang.");

        c.Sec("mo-hinh.thanh-lenh", "Thanh lệnh theo cấu kiện, chọn nhiều, gán tải, dòng lệnh kiểu AutoCAD")
            .P("Trên tab **Mặt bằng**, các nút được xếp thành tab theo cấu kiện: **Lưới – Tầng | Móng | Cột | Dầm | Sàn | Tường – Tải | Link CAD**. "
                + "Cụm bên trái (Chọn, Chọn nhiều, Xoá, Hoàn tác, Chọn tương tự, Bỏ chọn) dùng chung. Nháy đúp tên tab để thu gọn / mở lại thanh lệnh; F11 ẩn cả thanh lệnh.")
            .Steps("**Chọn nhiều**: bấm *Chọn nhiều* (hoặc gõ CH) rồi bấm từng đối tượng hoặc kéo khung – trái → phải (khung xanh liền) chọn đối tượng nằm TRỌN trong khung, phải → trái (khung xanh lá nét đứt) chọn đối tượng CHẠM khung; giữ Ctrl để bỏ chọn. "
                + "Tab đang mở quyết định loại được chọn: Dầm → dầm, Cột / Móng → cột, Sàn → ô sàn, các tab khác → mọi loại.",
                "**Chọn tương tự**: cùng tiết diện (tầng đang xem / mọi tầng), cùng loại – công năng, cùng tầng.",
                "**Gán tải trọng** (nhóm *Gán tải* ở tab Dầm, Sàn, Cột, Móng): chọn trường hợp tải, nhập giá trị, chọn *Thay thế / Cộng thêm / Xoá* rồi bấm **Gán** – áp cho MỌI đối tượng đang chọn, một bước hoàn tác (Ctrl+Z). "
                + "Dầm: q (kN/m) và lực tập trung P tại x/L; Sàn: q (kN/m²), *Theo công năng* (hoàn thiện / hoạt tải TCVN 2737 theo từng ô), tường xây trên sàn quy đổi q = g·h·L/A; Cột: N, Mx, My tại đầu cột; Móng: N, Mx, My tại chân cột.",
                "Dòng *Đang chọn* cho biết giá trị chung của các đối tượng; khác nhau thì hiện **«khác nhau»** và khi *Thay thế* chương trình hỏi trước khi ghi đè. Chỉ tải nhập tay bị thay / xoá – tải tự dồn từ sàn, gió, động đất giữ nguyên.",
                "**Tải tường**: tab *Tường – Tải* tính q = g_tường · h · (1 − tỷ lệ cửa) theo loại tường / chiều cao (0 = chiều cao tầng trên − 0,35 m); nút *q tường* điền giá trị vào ô q của tab Dầm.",
                "**Link CAD** (nền như xref theo tầng): *Liên kết DWG/DXF…*, *Quản lý…* (layer, dời / xoay / tỷ lệ), *Ẩn / hiện*, *Độ mờ*, *Đọc lại*, *Căn 2 điểm*, *Khoá*, *Bắt điểm CAD*, *Pick CAD*. "
                + "**Tường CAD → tải dầm**: cặp nét song song cách 110 / 220 mm trên layer tường nằm trên dầm (lệch ≤ t/2 + 50 mm) → tạo tường, tải tường dồn vào dầm khi phân tích.")
            .P("**Dòng lệnh** dưới mặt bằng (gõ thẳng khi đang trỏ mặt bằng): L dầm, C cột, S sàn, W tường, M dời, ST kéo, TR trim, EX extend, CO sao chép (dx,dy n), AR mảng (nx ny dx dy), O offset, MI đối xứng (tên trục), "
                + "E xoá, U hoàn tác, Z / ZE zoom, DI đo khoảng cách, MA sao tiết diện, TAI gán tải, SS chọn tương tự, ? trợ giúp – gõ có dấu / không dấu đều được. "
                + "Toạ độ: x,y tuyệt đối, @dx,dy tương đối, @L<góc (vd @3000<90), hoặc chỉ gõ chiều dài theo hướng chuột. **F3** bắt điểm (□ đầu, △ giữa, × giao, ⊥ vuông góc, ○ tâm nét CAD), **F8** trực giao, **F10** góc cực 45°; "
                + "Enter / Space lặp lệnh, Esc huỷ, ↑↓ lịch sử lệnh; con lăn chuột phóng to tại con trỏ, chuột giữa để kéo.")
            .Tip("Gán tải nhanh cho cả tầng: tab Sàn → *Chọn nhiều* → kéo khung phải → trái qua toàn mặt bằng → trường hợp HT → *Theo công năng* → làm lại với HTHT.");

        c.Sec("mo-hinh.giao-dien-revit", "Giao diện và lệnh vẽ kiểu Revit: kiểu tường nhiều lớp, cửa tự cắt tường")
            .P("Chế độ Thiết kế mặc định dùng **giao diện kiểu Revit**: ribbon một hàng **Kiến trúc | Kết cấu | Tải trọng | Phân tích | Xem | Quản lý** (nút lớn, chữ dưới biểu tượng), "
                + "bên trái **Thuộc tính** (bộ chọn kiểu + xem trước, thông số theo nhóm Ràng buộc / Kích thước / Kết cấu / Tải trọng) và **Trình duyệt dự án** (mặt bằng các tầng, 3D, bảng thống kê, họ & kiểu tường / cửa / tiết diện), "
                + "giữa là vùng vẽ lớn, dưới mặt bằng là **thanh tuỳ chọn** của công cụ đang dùng. Các ô nhập liệu cũ (công trình, lưới trục, tầng, tải, gió, động đất, vật liệu, địa chất, kết quả) nằm ở **Thông tin dự án** "
                + "(nút *Thông tin dự án ►* trên khung Thuộc tính hoặc Quản lý → Thông tin dự án). Muốn trở lại bố cục cũ: Quản lý → **Giao diện cổ điển** (menu Xem → *Giao diện kiểu Revit* để bật lại).")
            .Steps("**Khai báo kiểu tường**: Kiến trúc → **Kiểu tường…** → chọn kiểu ở danh sách trái (Gạch đặc 110/220 trát 2 mặt, Gạch rỗng, Gạch AAC, Tường 220 ốp đá granite mặt ngoài, Tường 110 ốp gạch men WC, Tường BTCT 200…) hoặc *＋ Mới / Nhân bản*. "
                + "Bảng lớp xếp từ **mặt ngoài** (trên) vào **mặt trong** (dưới): *Lõi kết cấu* (gạch / bê tông), *Lớp vữa trát*, *Ốp đá*, *Ốp gạch men*, *Sơn bả*, *Cách nhiệt* – chọn vật liệu (γ lấy theo thư viện *Vật liệu…*), nhập chiều dày; "
                + "*＋ Chèn lớp / Xoá lớp / ▲ / ▼*. Ô xem trước vẽ mặt cắt các lớp; dòng tổng cho biết tổng dày, dày lõi và **tải 1 m² = Σ dày × γ**.",
                "**Vẽ tường**: Kiến trúc → **Tường** → thanh tuỳ chọn: *Kiểu tường*, *Đường định vị* (Tim tường, Tim lõi, Mặt hoàn thiện ngoài / trong, Mặt lõi ngoài / trong), *Cao* (0 = tới tầng trên), *Chuỗi* (vẽ nối tiếp). "
                + "Bấm điểm đầu – điểm cuối; khi vẽ có **kích thước tạm** (chiều dài + góc) cạnh con trỏ, gõ số + Enter để nhập chiều dài; F8 trực giao, F3 bắt điểm. Tường tự nối góc L / T gọn, tô gạch chéo theo mức chi tiết.",
                "**Đặt cửa**: Kiến trúc → **Cửa đi / Cửa sổ / Ô thoáng** → chọn kiểu ở thanh tuỳ chọn (D1 900x2200, S1 1200x1400…; *Kiểu cửa…* để thêm / sửa rộng, cao, bậu, cách mở) → rê chuột lên tường: cửa xem trước bắt vào tường kèm "
                + "kích thước tạm tới đầu tường / cửa bên cạnh; **Space** lật hướng mở, **Shift+Space** đổi bên bản lề; bấm để đặt. Tường **tự cắt ô cửa** (mặt bằng, khối lượng xây trát), tải tường lên dầm trừ đúng diện tích cửa; "
                + "đặt sát đầu tường hoặc chồng cửa khác thì cửa tự dời vào chỗ hợp lệ gần nhất. Esc để dừng.",
                "**Sửa cửa**: bấm vào cửa → tab ngữ cảnh **Sửa | Cửa** và kích thước tạm màu xanh: **bấm vào số** để nhập khoảng cách mới (như Revit); đổi kiểu ở bộ chọn kiểu; Space lật; Delete xoá (tường liền lại). "
                + "Dời / xoá tường thì cửa trên tường đi theo / xoá theo.",
                "**Chọn đối tượng** (tường, dầm, cột, ô sàn): khung Thuộc tính hiện thông số và tải (tường: chiều cao tính tải, diện tích cửa, q lên dầm); ribbon hiện tab **Sửa | …** với các lệnh hợp ngữ cảnh "
                + "(Sửa kiểu, Đặt cửa, Di chuyển, Kéo giãn, Cắt, Kéo dài, Gán tải, Xoá). Đổi kiểu tường / tiết diện dầm – cột / sàn ngay ở bộ chọn kiểu.",
                "**Gán tải**: tab Tải trọng → Tải dầm / sàn / cột / móng / tường mở thanh lệnh cấu kiện (chọn nhiều, Thay thế / Cộng thêm / Xoá); *Gợi ý lanh tô* liệt kê lanh tô đề xuất cho mọi cửa đã đặt.")
            .Bullets("**Hiển thị / Đồ hoạ** (tab Xem): bật / tắt thẻ dầm (hoặc chỉ tên dầm), thẻ cột, thẻ sàn, công năng, thẻ tường, thẻ cửa; mức chi tiết *Thô* (tường tô đặc) / *Trung bình* / *Chi tiết* (thêm đường ranh lõi); "
                + "**tự tránh chồng nhãn** – nhãn đụng nhau được dời, không còn chỗ thì ẩn.",
                "Kiểu tường cũ (dày, γ, trát) tự đổi sang nhiều lớp **giữ nguyên tải 1 m²**; file cũ mở bình thường. Bảng *Loại tường* cũ vẫn dùng được: sửa dày / γ chỉ đổi lớp lõi, giữ lớp ốp.",
                "Gợi ý lanh tô: gối mỗi bên ≥ 200 mm (≥ 250 mm khi L0 > 1,5 m), h ≥ max(L0/10; 150 mm) (≥ 200 mm với tường 110 khi L0 > 1,5 m), cửa > 1,8 m trên tường 110 cảnh báo bổ trụ BTCT – chi tiết thép ở chế độ Triển khai hồ sơ → Lanh tô.")
            .Tip("Làm nhanh một tầng: Kiến trúc → Tường (kiểu Gạch đặc 220, đường định vị *Mặt hoàn thiện ngoài*) vẽ vòng tường bao theo chiều kim đồng hồ → Tường kiểu 110 cho tường ngăn → Cửa đi / Cửa sổ đặt lên tường → Tải trọng → Tải tường kiểm tra q → Phân tích (F5).");

        c.Sec("mo-hinh.revit-hoan-thien", "Kiểu Revit – tay nắm, Tab, làm lại, lệnh sửa bằng chuột, 3D có ô cửa")
            .Steps("**Tay nắm (grip)**: chọn một tường hoặc dầm → hai đầu hiện ô vuông xanh, giữa hiện ô thoi trắng. **Kéo ô đầu** để kéo dài / rút ngắn (bắt điểm trục, nút, nét CAD; F8 trực giao), "
                + "**kéo ô giữa** để dời cả đối tượng. Cửa trên tường giữ nguyên vị trí và tự nằm gọn trong tường; rút tường ngắn hơn cửa thì báo lỗi, tường giữ nguyên. Mỗi lần kéo là một bước hoàn tác.",
                "**Vừa màn hình**: mặt bằng tự vừa đúng cỡ vùng vẽ khi mở, đổi tầng, thu / mở khung bên hoặc đổi cỡ cửa sổ (chừa lề cho bong bóng trục và kích thước). "
                + "Đã lăn chuột phóng / kéo mặt bằng thì giữ nguyên khung nhìn của bạn; bấm **Vừa màn hình** (hoặc gõ Z, ZE) để vừa lại.",
                "**Nhãn mặt bằng**: chữ giữ cỡ ≈ 11 px trên màn hình, có nền trắng mờ cho dễ đọc; nhãn được đặt theo thứ tự ưu tiên trục → cột → dầm → sàn → chữ khác, "
                + "tự dời sang chỗ trống; nhãn dầm theo phương dọc viết xoay dọc theo dầm. Nhãn không còn chỗ bị ẩn – rê chuột vào vị trí đó để xem chú giải.",
                "**Tab**: đưa chuột vào chỗ nhiều đối tượng chồng nhau (cột – dầm – tường – ô sàn) rồi nhấn **Tab** để chọn lần lượt từng đối tượng (thanh trạng thái ghi đối tượng đang chọn, ví dụ 2/4).",
                "**Hoàn tác / Làm lại**: **Ctrl+Z** hoàn tác, **Ctrl+Y** (menu Sửa → Làm lại, hoặc gõ lệnh REDO) làm lại; sửa mô hình mới thì danh sách làm lại được xoá (tối đa 30 bước).",
                "**Lệnh sửa bằng chuột** (gõ ở dòng lệnh dưới mặt bằng, áp cho dầm đang chọn và tường đang chọn): "
                + "**CO** sao chép – bấm điểm gốc rồi điểm đích (bấm tiếp để sao nhiều bản, Esc kết thúc); **M** dời – điểm gốc → điểm đích; "
                + "**MI** đối xứng – bấm 2 điểm của trục đối xứng bất kỳ (hoặc gõ tên trục, vd 2 / B); **AR** mảng – gõ số phần tử (tính cả bản gốc) rồi bấm điểm gốc và điểm thứ hai (khoảng cách giữa 2 phần tử liền nhau); "
                + "**O** offset – gõ khoảng cách rồi bấm phía cần offset. Trong lúc chờ điểm đích có **bóng đối tượng** và dây chun ghi chiều dài + góc; có thể gõ chiều dài theo hướng chuột hoặc @dx,dy. "
                + "Cách gõ số cũ vẫn dùng được (CO: dx,dy số bản; AR: số cột số hàng dx dy).",
                "**3D**: tường xây hiện trong khung 3D với **ô cửa khoét thật** (giữ phần dưới bậu cửa sổ và phần trên đầu cửa); ô tích *Tường (có ô cửa)* ở góc trên để ẩn / hiện tường.")
            .Bullets("Tab **Xem → Hiện nhanh**: bật / tắt ngay thẻ dầm, thẻ cột, thẻ sàn, thẻ cửa, kích thước bước trục, tải đã gán (không cần mở hộp Hiển thị / Đồ hoạ).",
                "Chữ nhãn luôn giữ cùng cỡ trên màn hình khi phóng to / thu nhỏ; thu nhỏ quá mức thì nhãn cấu kiện tự ẩn (tên trục, kích thước vẫn hiện); nhãn đụng nhau được dời hoặc ẩn – cả ở giao diện cổ điển.",
                "Ribbon và thanh lệnh cấu kiện luôn giữ **một hàng tab**: khung hẹp thì lăn chuột trên hàng tab để cuộn, hoặc bấm **»** để chọn tab bị khuất.",
                "Giao diện kiểu Revit ẩn bảng thông tin dưới mặt bằng để vùng vẽ rộng tối đa (thông tin đối tượng xem ở khung Thuộc tính); đổi tầng thì mặt bằng tự **vừa màn hình**.",
                "Link CAD: **khoá** bản vẽ nền (tab Link CAD → 🔒 Khoá) được lưu trong dự án cùng độ mờ, ẩn / hiện và vị trí căn – mở lại dự án vẫn giữ; bản vẽ đang khoá không căn 2 điểm được.");

        c.Sec("mo-hinh.dien-nuoc", "Bộ môn Điện nước: thiết bị chôn ngầm trong tường, tuyến ống / dây, cảnh báo khi kiến trúc thay đổi")
            .P("Menu **Thiết kế → Môi trường Điện nước** mở cửa sổ riêng của bộ môn điện nước. Phần kiến trúc (tường, cửa) hiện **mờ như bản vẽ tham chiếu (xref)** – không sửa được ở đây. "
                + "Mặc định **thiết bị, ống cấp nước, khớp nối, điểm chia chôn ngầm trong tường** (cấp nước đi ngang trong tường ở 0,3 m trên sàn); **ống gen điện ưu tiên đi ngang trong sàn / trần**, "
                + "chỉ đi đứng trong tường tới thiết bị, không đi qua cột và dầm (xem {s:mo-hinh.dien-nuoc-tcvn}); thoát nước đi dưới sàn.")
            .Steps("**Đặt thiết bị**: tab *Cấp thoát nước* (bồn cầu, lavabo, sen tắm, vòi nước, thoát sàn, van, điểm nối ống, bình nóng lạnh) hoặc *Điện* (ổ cắm đơn / đôi, công tắc 1–3 hạt, đèn ốp trần / tuýp / downlight / hắt tường, quạt trần / treo tường, hộp chia, aptomat, tủ điện) "
                + "→ bấm gần tường (≤ 1 m): thiết bị gắn vào **mặt tường phía bấm**, cao độ lắp mặc định theo họ (ổ cắm 0,4 m, công tắc 1,25 m, lavabo 0,8 m…; đèn, quạt trần đặt trên trần). Esc để thôi.",
                "**Nối tuyến**: chọn hệ thống (cấp nước lạnh / nóng, thoát nước thải / xám / mưa, điện chiếu sáng / ổ cắm / điều hoà / điện nhẹ) → *Nối tuyến ống* hoặc *Nối dây (ống gen)* → bấm thiết bị nguồn rồi thiết bị đích. "
                + "Tuyến tự đi trong các tường liền nhau (đường ngắn nhất), tự sinh **cút 90° / chếch 45°** tại chỗ đổi hướng, **măng sông / khớp nối ống gen** theo chiều dài cây ống (ống nước 4 m, ống gen 2,9 m), "
                + "**tê / tê giảm** (điện: **hộp nối**) tại điểm có ≥ 3 tuyến, **côn thu** khi đổi tiết diện tại điểm nối ống.",
                "**Tự chọn cỡ** (bảng tiêu chuẩn – {s:mo-hinh.dien-nuoc-tcvn}): cấp nước theo đương lượng N (nhà ở q = 0,2·a·√N + K·N, a theo tiêu chuẩn dùng nước – 125 l → 2,15, K = 0,002…0,006 theo N; "
                + "công trình công cộng q = 0,2·α·√N – TCVN 4513, ống PPR v ≤ 1,5 m/s); thoát nước theo tổng DFU (có bồn cầu → Ø110); "
                + "dây điện I = P/(U·cosφ) → aptomat → dây Cu/PVC trong ống gen (IEC 60364-5-52 cách lắp B1, hiệu chỉnh nhiệt độ / nhóm mạch, sụt áp ≤ 3 % chiếu sáng, ≤ 5 % mạch khác; "
                + "tối thiểu chiếu sáng 1,5 mm², ổ cắm 2,5 mm²) → ống gen sao cho dây chiếm ≤ 40 % tiết diện.",
                "**Kiến trúc thay đổi**: khi tường dời, đổi dài / dày, bị xoá hoặc thêm cửa, lúc quay lại cửa sổ Điện nước (hoặc bấm *Kiểm tra → Cập nhật theo kiến trúc*) chương trình tự **dời thiết bị theo tường**, đi lại tuyến, tính lại phụ kiện "
                + "và liệt kê **cảnh báo**: thiết bị nằm trong ô cửa, tường bị xoá (thiết bị mồ côi), tường quá mỏng không chôn được ống (Ø ngoài > ½ lõi + 1 lớp trát, vd ống Ø110 trong tường 110), "
                + "thiết bị chồng nhau, tuyến xuyên cột / dầm (cần ống lồng). *Đi tới* phóng tới chỗ lỗi, *Sửa nhanh* dời thiết bị ra cạnh ô cửa / gắn vào tường gần nhất.",
                "**Thống kê và bản vẽ**: *Kiểm tra → Bảng thống kê vật tư* (thiết bị, ống theo Ø – m, ống gen, dây theo tiết diện – m ruột, cáp, cút / chếch / tê / côn / măng sông / hộp nối); "
                + "*Xuất bản vẽ điện nước (DXF)* ra các mặt bằng **cấp nước, thoát nước, điện chiếu sáng, điện ổ cắm** từng tầng (nền kiến trúc, ký hiệu thiết bị, nhãn tuyến, **bảng ký hiệu**) và bảng thống kê, số hiệu DN-01….")
            .Bullets("Thư viện điện nước: *Thư viện MEP → Xem thư viện / Thêm họ thiết bị*. Họ thêm được lưu trong dự án và trong thư viện người dùng **chỉ trên máy này** (%AppData%\\VietConst\\ThuVienMEP.json).",
                "Chọn thiết bị để sửa ký hiệu, cao độ, vị trí dọc tường, mạch điện, đổi mặt tường chôn; chọn tuyến để sửa cỡ hoặc bật / tắt *Tự chọn cỡ*. Delete xoá; mọi thao tác hoàn tác được (Ctrl+Z ở cửa sổ chính).",
                "Thiết bị và tuyến điện nước có khối 3D (dùng cho khung nhìn 3D, IFC / OBJ và kiểm tra va chạm giữa các bộ môn).");

        c.Sec("mo-hinh.dien-nuoc-tcvn", "Thông số theo TCVN và tuyến ống gen đi trong trần / sàn")
            .P("Mọi thông số điện nước – ĐHKK lấy từ **một bảng tiêu chuẩn** (Mep/MepStandards.cs), mỗi dòng ghi nguồn \"Theo TCVN … Bảng …\". "
                + "*Cài đặt điện nước* hiện bảng: giá trị **Tiêu chuẩn** (chỉ xem), ô **Tuỳ chỉnh** (trống = theo tiêu chuẩn) và **Nguồn**; dòng có dấu **(*)** là số trích theo thực hành / tài liệu thứ cấp – "
                + "cần đối chiếu bản gốc tiêu chuẩn trước khi dùng cho hồ sơ.")
            .Bullets("**Cấp nước (TCVN 4513:1988)**: chọn *Loại công trình* – nhà ở q = 0,2·a·√N + K·N (a theo tiêu chuẩn dùng nước: 100 → 2,2; 125 → 2,15; 150 → 2,1; 200 → 2,05…; K = 0,002 khi N ≤ 300, 0,003 ≤ 500, 0,004 ≤ 800, 0,005 ≤ 1200, 0,006 lớn hơn); "
                + "công trình công cộng q = 0,2·α·√N (nhà trẻ 1,2; bệnh viện 1,4; cơ quan 1,5; trường học 1,8; an dưỡng 2,0; khách sạn 2,5); đương lượng lavabo 0,33, chậu bếp 1,0, sen 0,67, xí có két 0,5; v ≤ 1,5 m/s; bể – bơm 150 l/người·ngđ, Kngày 1,3.",
                "**Thoát nước (TCVN 4474:1987)**: ống có xí ≥ Ø110; độ dốc tiêu chuẩn / nhỏ nhất Ø50: 3,5 % / 2,5 %, Ø100: 2 % / 1,2 %, Ø150: 1 % / 0,7 %; ống thông hơi vượt mái ≥ 0,7 m.",
                "**Điện (TCVN 9206 / 9207:2012, IEC 60364-5-52)**: dòng cho phép dây Cu/PVC cách lắp B1 (1,5 → 17,5 A; 2,5 → 24 A; 4 → 32 A; 6 → 41 A; 10 → 57 A; 16 → 76 A; 25 → 101 A) × hệ số nhiệt độ (Bảng B.52.14: 35 °C → 0,94) × hệ số nhóm mạch (Bảng B.52.17: 2 mạch → 0,80); "
                + "sụt áp ≤ 3 % chiếu sáng, ≤ 5 % mạch khác (Phụ lục G, ρ = 0,0225 Ω·mm²/m); Kc chiếu sáng 0,9, ổ cắm 0,5, điều hoà 0,8, bình nóng 0,7; ổ cắm 0,4 m, công tắc 1,25 m, tủ điện 1,5 m.",
                "**ĐHKK (TCVN 5687:2010)**: trong nhà mùa hè 25 °C, 60 % (Bảng A.1, nghỉ ngơi tĩnh tại 25…28 °C, 60…70 %); ngoài nhà (Phụ lục B, m = 35 h/năm) Hà Nội 37,8 °C – 53,4 %, TP.HCM 36,8 °C – 56 % (chọn *Khí hậu ngoài nhà*); "
                + "gió tươi 25 m³/h·người; vận tốc ống gió chính 6 m/s, nhánh 4 m/s.")
            .Steps("**Tuyến ống gen** (*Cài đặt điện nước → Tuyến ống gen điện*): **Trần / sàn (khuyến nghị, mặc định)** – ổ cắm, điều hoà đặt thấp đi trong **sàn dưới chân** (tim ống ở giữa chiều dày sàn; tầng nền không có sàn: trong lớp bê tông lót), "
                + "chiếu sáng và thiết bị đặt cao đi trong **sàn trên (trần)**; trong tường chỉ có **đoạn đứng** từ thiết bị xuống mặt sàn / lên đáy sàn. *Trong tường* – cách cũ, đi ngang theo tim tường.",
                "**Không đi qua cột, dầm**: tuyến trong sàn được dò trực giao tránh tiết diện cột (khe hở 50 mm) và dải bề rộng dầm. Chỉ được **qua dầm trong chiều dày sàn** khi sàn ≥ 100 mm, ống gen ≤ Ø25 và điểm qua nằm trong **1/3 giữa nhịp** (cách gối ≥ L/3); "
                + "không đạt thì vòng đường khác; không có đường → tuyến đi theo tường và có cảnh báo **ONG_GEN_TRAN_SAN**.",
                "Thiết bị trên tường đặt trên dầm (hoặc dưới dầm): ống đi đứng tới **lớp cán nền** (mặc định 30 mm trên mặt sàn) / dưới đáy dầm rồi chuyển ra khỏi bề rộng dầm trước khi vào sàn.",
                "**Kiểm tra kết cấu** cho ống gen trong sàn: **ONG_GEN_QUA_DAM** (qua dầm: đạt → Gợi ý; sàn mỏng, Ø lớn, sát gối → Cảnh báo), **SAN_MONG** (Ø ống > 1/3 chiều dày sàn), "
                + "**ONG_GEN_NGANG_TUONG** (còn đoạn ngang > 0,3 m trong tường khi đã chọn trần / sàn). Rãnh đục tường và khối lượng tường trừ rãnh giảm tương ứng vì đoạn ngang không còn nằm trong tường.")
            .Tip("Cấp nước vẫn đi trong tường như trước; chỉ ống gen điện (dây trong ống gen) áp dụng tuyến trần / sàn. Tuyến vẽ tay (điểm trung gian) giữ nguyên như người dùng vẽ.");

        c.Sec("mo-hinh.dien-nuoc-dot5", "Khoảng hở ống gen, khí hậu theo tỉnh, chiếu sáng LPD, ống đứng thoát nước và bể tự hoại")
            .P("Các phép kiểm tra bổ sung cho điện nước (Mep/MepRound5.cs). Thông số nằm trong bảng tiêu chuẩn của {s:mo-hinh.dien-nuoc-tcvn} "
                + "và tuỳ chỉnh được; dòng có dấu **(*)** là giá trị theo thực hành, cần đối chiếu bản gốc tiêu chuẩn.")
            .Bullets("**Ống gen song song trong sàn**: khoảng hở giữa 2 ống ≥ **max(3·Ø, 50 mm)** khi đi cạnh nhau ≥ 0,5 m. Vi phạm → cảnh báo **ONG_GEN_SAT_NHAU**; "
                + "khi *Đi lại tuyến* / đối chiếu, tuyến vẽ sau được **giãn** sang bên: dời đoạn giữa, 2 đoạn kề kéo dài theo, vẫn tránh cột và chỉ qua dầm ở 1/3 giữa nhịp.",
                "**Số ống trong một dải sàn**: quá 4 ống gen song song trong dải rộng 1 m → **ONG_GEN_BO**. Tại chỗ 2 ống giao nhau, tổng Ø chồng lên phải ≤ 1/3 chiều dày sàn, nếu không → **ONG_GEN_GIAO** "
                + "(cùng quy tắc với **SAN_MONG** cho một ống).",
                "**Khí hậu ĐHKK**: *Khí hậu ngoài nhà* chọn theo tỉnh: Hà Nội, TP.HCM, **Thái Nguyên**, Bắc Ninh, Bắc Giang, Vĩnh Phúc, Hưng Yên, Bình Dương, Đồng Nai, Long An. "
                + "Tỉnh chưa có số liệu riêng lấy theo trạm gần nhất; tên tỉnh ghi rõ trạm. Thái Nguyên tạm lấy giá trị trạm Hà Nội (*) và sửa được qua khoá *dhkk.t_tn* / *dhkk.phi_tn*. "
                + "*Phiên bản TCVN 5687*: **2010** (Phụ lục B) hoặc **2024**. Bản 2024 dẫn chiếu số liệu khí hậu của QCVN 02:2022/BXD; giá trị mặc định vẫn theo bảng 2010 nên cần nhập lại theo QCVN 02:2022.",
                "**Tải lạnh theo khí hậu** (*Tải lạnh chi tiết: Δt, Δw, hiệu chỉnh CLTD theo khí hậu địa phương* = Có): "
                + "Δt = t ngoài − t trong (Hà Nội 37,8 − 25 = 12,8 °C). "
                + "Δw = w ngoài − w trong, với áp suất hơi bão hoà theo công thức Magnus. "
                + "CLTD tường / mái cộng thêm hiệu chỉnh (25,5 − t trong) + (t ngoài − DR/2 − 29,4), trong đó DR là biên độ nhiệt ngày (mặc định 8 °C, Hà Nội → +4,9 °C). "
                + "Mặc định là Không: giữ Δt 9 °C như trước.",
                "**Chiếu sáng (TCVN 7114-1:2008, QCVN 09:2017/BXD)**: loại phòng nhận theo tên phòng. Mỗi loại có độ rọi yêu cầu và mật độ công suất LPD lớn nhất: "
                + "văn phòng / phòng họp 500 lx, 11 W/m²; "
                + "lớp học 300 lx; "
                + "phòng khách 150 lx; "
                + "phòng ngủ 100 lx; "
                + "bếp 200 lx; "
                + "vệ sinh 200 lx; "
                + "hành lang 100 lx; "
                + "cầu thang 150 lx; "
                + "kho 100 lx. "
                + "Kiểm tra Σ W đèn trong phòng / diện tích ≤ LPD. Số đèn cần tính theo phương pháp hệ số sử dụng **N = E·A / (Φ·UF·MF)** (UF 0,6; MF 0,8). "
                + "Quang thông Φ lấy theo thư viện (ốp trần 2200 lm, tuýp LED 1,2 m 1800 lm, downlight 800 lm, hắt tường 960 lm); họ chưa khai báo thì lấy 100 lm/W. "
                + "Sửa LPD / độ rọi bằng khoá *cs.lpd.(mã)* / *cs.e.(mã)* (mã: vanphong, hop, lophoc, khach, ngu, bep, wc, hanhlang, cauthang, kho, khac).",
                "**Ống đứng thoát nước (TCVN 4474:1987; sức chứa DFU tham khảo IPC (*))**: DFU cộng dồn **từ tầng trên xuống**. Ø ống đứng chọn tại chân ống và giữ suốt chiều cao; ống có xí ≥ Ø110. "
                + "Sức chứa theo số tầng có thiết bị (≤ 3 hoặc > 3 khoảng nhánh), ví dụ Ø110 chứa 240 / 500 DFU. "
                + "Ống không có thông hơi lấy 0,5 sức chứa (*Ống đứng thoát nước có ống thông hơi* = Không). "
                + "**Ống thoát ra** hố ga / bể tự hoại chọn Ø theo tổng DFU và độ dốc tiêu chuẩn của Ø đó.",
                "**Bể tự hoại 3 ngăn (TCVN 10334:2014)**: chỉ lập khi đã khai báo số người dùng nước. "
                + "Thể tích nước Wn = t·q·N/1000 (lưu 1 ngày, 150 l/người·ngđ). "
                + "Thể tích cặn Wc = a·T·(100 − W1)·b·c·N / ((100 − W2)·1000), với a = 0,6; T = 180 ngày; W1 = 95 %; W2 = 90 %; b = 0,7; c = 1,2. "
                + "Dung tích W = Wn + Wc. Lớp nước sâu 1,5 m, bề rộng ≥ 0,8 m; chia ngăn chứa L/2, ngăn lắng L/4, ngăn lọc L/4. "
                + "Bộ bản vẽ có hình mặt cắt dọc và mặt bằng bể (tỷ lệ 1:50) với ống vào / ra Ø110 và ống thông hơi.")
            .Tip("Bảng chiếu sáng chỉ xuất khi phòng có đèn. Bảng ống đứng và bể tự hoại chỉ xuất khi đã khai báo số người, nên các dự án cũ không đổi số bảng.");

        c.Sec("mo-hinh.thoat-nuoc-ket-cau", "Thoát nước đi dưới sàn có độ dốc; cảnh báo điểm nguy hiểm cho kết cấu và chi tiết gia cường")
            .P("Ống **thoát nước** (thải, xám, mưa) **không đi trong tường**: từ thiết bị chỉ có đoạn đứng ngắn xuyên sàn ngay trước mặt tường, đoạn ngang **treo dưới đáy sàn** "
                + "(trong không gian trần tầng dưới; ô sàn **hạ cốt** đủ sâu thì đi trong lớp hạ cốt) và **dốc về ống đứng** (hộp gen kỹ thuật). Tầng thấp nhất: ống chôn trong lớp đất đắp dưới sàn nền.")
            .Steps("Tab *Cấp thoát nước* → **Nối thoát về ống đứng**: chương trình đề xuất **ống đứng** tại góc tường gần nhóm thiết bị vệ sinh (đã có thì dùng lại) và nối mọi thiết bị thoát chưa nối.",
                "**Độ dốc**: mặc định theo Ø (TCVN 4474 – thực hành): Ø ≤ 50: **3 %**; Ø60…Ø90: **2 %**; Ø ≥ 110: **2 %** (tối thiểu 1 %). Chọn tuyến → ô *Độ dốc (%)* để nhập riêng (0 = mặc định); "
                + "ngoài khoảng tối thiểu…8 % có cảnh báo. Cao độ tim ống giảm dần từ thiết bị: z = z₀ − i·s; khung thuộc tính ghi cao độ **đáy ống đầu / cuối tuyến**.",
                "**Phụ kiện thoát**: đổi hướng dùng **2 chếch 45°** (không cút 90°), nhánh vào ống đứng / điểm nối dùng **tê xiên 45° (chữ Y)**, **ống kiểm tra (nắp thăm)** ở chân ống đứng và mỗi 12 m tuyến ngang; "
                + "thống kê có **quang treo + ty ren** (≤ 0,8 / 1,0 / 1,2 m theo Ø) và gợi ý **ống thông hơi** vượt mái ≥ 0,7 m.",
                "Cảnh báo thoát nước: **ép đi trong tường** (ô *Ép đi trong tường* – không khuyến khích), ống tụt thấp hơn **trần giả** (đáy sàn − 0,6 m mặc định), vượt khỏi lớp hạ cốt, độ dốc ngoài khoảng.")
            .Bullets("**Ảnh hưởng tới kết cấu** (danh sách cảnh báo, có mức **Cho phép / Gợi ý / Cảnh báo / Nguy hiểm / Cấm** và đề xuất xử lý):",
                "**Xuyên dầm**: cho phép khi Ø lỗ (Ø ống + 20 mm ống lồng) ≤ h/3, mép lỗ trong **1/3 giữa chiều cao**, tâm lỗ trong **1/3 giữa nhịp**, cách mép gối ≥ h (≥ 2h vùng tới hạn kháng chấn), hai lỗ cách ≥ 3Ø; "
                + "Ø > h/4 hoặc ngoài 1/3 giữa nhịp → gia cường; vi phạm → **Nguy hiểm** (dời lỗ / đi dưới đáy dầm / tăng chiều cao dầm). Ống **đứng** xuyên dầm: không cho phép.",
                "**Gia cường lỗ xuyên dầm**: ống lồng thép chờ sẵn; 2 đai (3 đai khi Ø > h/4) Ø8a50 mỗi bên lỗ; 2Ø12 trên + 2Ø12 dưới lỗ dài Ø + 2·max(40d; 500 mm).",
                "**Xuyên cột**: **Cấm** (ống gen ≤ Ø25 trong cột cạnh ≥ 400: cảnh báo, chỉ đặt ống chờ khi đổ).",
                "**Lỗ sàn**: cách mép cột < 2,5·hs → **Nguy hiểm** (vùng chọc thủng); < 6·hs → cảnh báo kiểm tra lại chọc thủng; lỗ > 300 mm → **thép gia cường mép lỗ** bù phần thép bị cắt "
                + "(As cắt chia đều 2 bên, tối thiểu 2Ø12 mỗi bên, dài cạnh lỗ + 2·40d) + 1Ø12 xiên mỗi góc; ≥ 3 lỗ thẳng hàng cách nhau < 1 m → đường yếu.",
                "**Rãnh đục tường**: tường BTCT không đục (đặt ống chờ); rãnh ngang sâu > 1/3 lõi hoặc dài > 1 m trong tường 110 → cảnh báo.",
                "**Va chạm giữa bộ môn**: ống xuyên dầm ở vị trí không cho phép được tính là **va chạm cứng** thay vì *ống lồng*.",
                "Hồ sơ điện nước (DN-) kèm **CHI TIẾT GIA CƯỜNG LỖ XUYÊN DẦM** và **CHI TIẾT GIA CƯỜNG MÉP LỖ SÀN** (tỷ lệ 1/20); mặt bằng thoát nước ghi mũi tên dốc, **i=2%**, cao độ đáy ống.");

        c.Sec("mo-hinh.lo-xuyen-ket-cau", "Lỗ xuyên điện nước trong hồ sơ kết cấu; tường trừ rãnh điện nước; kích thước tạm tường / cột")
            .P("Mọi lỗ xuyên dầm (trừ mức **Cấm**) và lỗ sàn lớn có thép gia cường từ bộ môn điện nước được **đánh ký hiệu** **LX1, LX2…** (lỗ xuyên dầm) và **LS1, LS2…** (lỗ sàn); lỗ giống nhau (cùng cấu kiện, cùng Ø / kích thước) dùng chung ký hiệu.")
            .Bullets("Trong **hồ sơ kết cấu (KC)**:",
                "Tờ **KC-10 \"CHI TIẾT GIA CƯỜNG LỖ XUYÊN KẾT CẤU\"**: mỗi ký hiệu một hình (tỷ lệ 1/20) kèm số lượng và tầng.",
                "**Mặt bằng kết cấu**: vòng tròn tại vị trí lỗ + ký hiệu LX / LS.",
                "**Mặt đứng dầm**: vẽ lỗ + ống lồng, đai gia cường mỗi bên lỗ, thép bổ sung trên / dưới lỗ và ghi chú \"xem chi tiết LX1\".",
                "**Kiểm tra chịu cắt tại lỗ xuyên** (bảng kiểm tra dầm): tiết diện thực h0n = h0 − Ø lỗ, đai quanh lỗ bước 50 mm, Qu = Qb + 0,75·qsw·2h0n ≤ 0,3·Rb·b·h0n so với **Q tại vị trí lỗ** – nội suy bao lực cắt của nhịp (Qmax ở gối, giảm tuyến tính về 0 giữa nhịp).",
                "**Bảng thống kê cốt thép**: thêm cấu kiện \"D1 – gia cường lỗ xuyên\" / \"S1 – gia cường lỗ xuyên\" chứa thép gia cường (số lượng, Ø, chiều dài, trọng lượng).")
            .Bullets("**Triển khai hồ sơ**:",
                "Dầm: bảng **LỖ XUYÊN DẦM – ỐNG LỒNG** (vị trí từ tim gối đầu, cách đáy, Ø); sàn: bảng **LỖ XUYÊN SÀN** (tâm lỗ từ góc ô, cạnh lỗ) – thép gia cường vào thép cấu kiện, lỗ vẽ trên mặt đứng / mặt bằng, cảnh báo lỗ sai vùng.",
                "Mặt bằng kết cấu → nút **Lỗ xuyên từ điện nước**: chép lỗ từ mô hình (chế độ Thiết kế) vào dầm / sàn hồ sơ **cùng tên** (không nhân đôi khi chạy lại).",
                "Lỗ sàn của ô sàn **đã đặt trên mặt bằng kết cấu** được đo khoảng cách tới **mép cột gần nhất** trên cùng mặt bằng: < 2,5·hs → *NGUY HIỂM* (vùng chọc thủng, có dòng kiểm tra không đạt), < 6·hs → *CẢNH BÁO* (giảm chu vi chọc thủng).")
            .Bullets("**Tường trừ phần điện nước chiếm chỗ** (bảng khối lượng):",
                "Ống / ống gen chôn tường: rãnh rộng Ø + 20 mm, sâu Ø + 10 mm (≤ lõi tường) × chiều dài trong tường; hộp âm tường (ổ cắm, công tắc, hộp chia, aptomat, tủ điện): W × H × sâu.",
                "Khối xây (gạch, vữa xây) trừ thể tích này; thêm công tác **Đục rãnh tường (m)**, **Trát vá rãnh (m²)**, **Đục lỗ hộp âm tường (cái)**. Ống thoát nước đi dưới sàn không đục tường. Tải tường giữ nguyên (phần trừ nhỏ, thiên về an toàn).")
            .Bullets("**Mặt cắt** và **kích thước tạm**:",
                "Mặt cắt 1-1 / 2-2 tự chọn nhịp **cắt được nhiều sàn nhất** (tránh ô cầu thang); mức sàn không có sàn trên đường cắt ghi chú \"ô cầu thang / lỗ sàn\"; phần tử **nhìn thấy phía sau** mặt cắt (tới trục kế tiếp, ≤ 6 m) vẽ nét mảnh.",
                "Giao diện kiểu Revit: chọn **tường** → kích thước tạm tới tường / trục song song hai bên (bấm số → dời tường song song); chọn **cột** → khoảng cách tâm cột tới trục gần nhất (bấm số → đổi lệch tâm cột).");

        c.Sec("mo-hinh.dhkk", "Điều hoà – thông gió, cài đặt điện nước, kéo cao độ trên mặt cắt, kiểu trục mặt bằng kết cấu")
            .P("Cửa sổ Môi trường Điện nước có thêm tab **Điều hoà – thông gió** và nút **Cài đặt điện nước…** (tab Kiểm tra). Mọi thay đổi là một bước hoàn tác (Ctrl+Z ở cửa sổ chính).")
            .Bullets("**Điều hoà – thông gió (ĐHKK)**:",
                "Đặt **dàn lạnh / dàn nóng** (nhập công suất BTU/h), **miệng gió cấp / hồi** (sát trần).",
                "**Vẽ ống gió**: chọn hệ (gió cấp / hồi / thải / tươi), nhập lưu lượng Q (m³/h), bấm điểm đầu – điểm cuối (vẽ nối tiếp được). Ống đi trực giao sát đáy sàn trên (khe 0,1 m). Cỡ ống tự chọn: **A = Q / (3600·v)**; ống tròn lấy Ø chuẩn ≥ √(4A/π), ống chữ nhật lấy chiều cao chuẩn nhỏ nhất sao cho w/h ≤ 4, w làm tròn bội 50 mm.",
                "Phụ kiện ống gió tự sinh: **cút** ở điểm đổi hướng, **tê** khi đầu ống chạm thân ống khác, **côn** khi hai ống nối đầu khác tiết diện.",
                "**Ống đồng gas**: bấm dàn lạnh rồi dàn nóng – cặp ống lỏng / gas theo công suất dàn lạnh (≤ 12000 BTU: Ø6,35/Ø9,52; 18000: Ø6,35/Ø12,7; 24000: Ø6,35/Ø15,88; tới 48000: Ø9,52/Ø15,88).",
                "**Ống nước ngưng**: bấm dàn lạnh rồi điểm xả – uPVC, cao độ giảm theo độ dốc cài đặt (tối thiểu **1 %**).",
                "**Va chạm ống gió – dầm**: ống có đỉnh cao hơn đáy dầm tại chỗ cắt qua dầm được cảnh báo (mã VA_CHAM_ONG_GIO) kèm đề xuất hạ ống / dùng ống dẹt – không khoét dầm.",
                "Bảng vật tư điện nước thêm: **tôn mạ kẽm (m²) = chu vi × chiều dài** (độ dày 0,5 / 0,75 / 1,0 / 1,2 mm theo cạnh lớn), ống gió theo cỡ (m), bảo ôn, giá treo, phụ kiện ống gió, **cặp ống đồng (m)** và bảo ôn, ống nước ngưng (m), dàn lạnh / nóng, miệng gió.",
                "Bộ bản vẽ điện nước thêm **MẶT BẰNG ĐIỀU HOÀ – THÔNG GIÓ** từng tầng: ống gió 2 nét + tim và nhãn cỡ – lưu lượng, ký hiệu miệng gió (MGC / MGH), dàn lạnh (DL), dàn nóng (DN), ống gas (G), ống nước ngưng (N, ghi i%), bảng ký hiệu. Khung nhìn 3D / IFC có khối ống gió đúng tiết diện, dàn lạnh / nóng, miệng gió, ống đồng.",
                "Nhập Revit (đối tượng điện nước): **ống gió** (Duct) thành ống gió ĐHKK giữ tiết diện chữ nhật / tròn và hệ cấp / hồi / thải theo tên hệ; **miệng gió** (Air Terminal); thiết bị cơ (Mechanical Equipment) có tên dàn lạnh / indoor / FCU / cassette hoặc dàn nóng / outdoor thành dàn lạnh / nóng (đọc công suất \"… BTU\" trong tên). Phụ kiện ống gió bỏ qua (tự sinh).")
            .Bullets("**Cài đặt điện nước…**:",
                "Thông số chung: cao độ tuyến chôn tường, tuyến chiếu sáng cách trần, chiều dài cây ống / ống gen, chiều cao tầng mặc định, độ dốc mặc định theo Ø, không gian trần, khoảng cách nắp thăm, **khoảng cách quang treo** (0 = theo Ø), thông số ĐHKK (vận tốc ống chính / nhánh, tỷ lệ cạnh, khe trần, giá treo ống gió, độ dốc và Ø ống nước ngưng).",
                "Bảng **độ dốc theo hệ thống + dải Ø**: hệ thống, Ø từ – Ø đến (0 = mọi Ø), độ dốc %, **Min / Max %** (kẹp cả độ dốc nhập tay của tuyến thuộc hệ đó). Áp dụng xong mọi tuyến được đi lại – cao độ đáy ống theo độ dốc mới.",
                "Bảng **cao độ mặc định theo nhóm thiết bị** (ổ cắm, công tắc…): dùng khi đặt thiết bị mới thay cho cao độ của họ; thiết bị trên trần vẫn theo trần.")
            .Bullets("**Thao tác kiểu Revit bổ sung**:",
                "Khung **mặt cắt** (xem mặt cắt): bấm giữ chuột trái gần một đường cao độ tầng rồi kéo để đổi cao độ tầng (bắt 10 mm, không vượt quá tầng kề ±0,5 m) – giống kéo trên mặt đứng; cột, dầm, sàn dời theo.",
                "Mặt bằng (chế độ Thiết kế): nắm trục ở **phần đuôi đường trục** ngoài khung lưới (không chỉ ở bong bóng) để kéo; giữ **Alt** để nắm cả đoạn trục bên trong khung lưới.",
                "Mặt bằng kết cấu (Triển khai hồ sơ): chuột phải lên đường trục → bong bóng **cả hai đầu / chỉ đầu dưới–trái / chỉ đầu trên–phải / không**, **ẩn trục trên mặt bằng này**, **hiện lại mọi trục đã ẩn**. Kiểu trục đi theo tên trục khi thêm / xoá / dời trục và áp dụng cả trên bản vẽ DXF (chuỗi kích thước bỏ trục ẩn).",
                "Khung **3D**: chọn thiết bị điện nước rồi **kéo** – thiết bị gắn tường trượt dọc tường chủ, thiết bị tự do dời trên mặt sàn; bắt bước 5 cm, tuyến nối với thiết bị đi lại ngay, thả chuột là một bước hoàn tác.");

        c.Sec("mo-hinh.dhkk-sua", "Điều hoà – thông gió: sửa từng đối tượng, hạ ống qua dầm, chọn máy theo phòng")
            .P("Tab **Điều hoà – thông gió** của cửa sổ Điện nước có thêm hàng lệnh **Sửa** và **Máy theo phòng**. Mỗi thao tác (kể cả một lần kéo) là một bước hoàn tác.")
            .Bullets("**Chọn và sửa trên mặt bằng** (công cụ Chọn / sửa, hoặc chuột phải để về công cụ chọn):",
                "Bấm vào **ống gió** (trong bề rộng ống), **miệng gió**, **dàn lạnh / dàn nóng**, **ống đồng / ống nước ngưng** để chọn – viền đỏ nét đứt; **Ctrl + bấm** thêm / bớt đối tượng.",
                "**Quét khung**: kéo từ trái sang phải chọn đối tượng **nằm trọn** trong khung (khung xanh dương); kéo từ phải sang trái chọn đối tượng **chạm** khung (khung xanh lá, nét đứt).",
                "**Kéo** đối tượng đã chọn để dời (bắt bước 5 cm). Kéo đúng **đỉnh tuyến** ống gió để nắn tuyến. Dời dàn lạnh / dàn nóng thì ống đồng đi lại tuyến, ống nước ngưng đi lại từ dàn mới và giữ điểm xả, độ dốc.",
                "**Delete** xoá đối tượng đã chọn; xoá dàn lạnh / nóng xoá kèm ống đồng, ống ngưng nối với nó.",
                "Bảng **Thuộc tính** ống gió: hệ, ống tròn / chữ nhật, w × h hoặc Ø (đổi cỡ **giữ cao độ đỉnh ống** – ống vẫn sát trần), lưu lượng Q (tự chọn lại cỡ theo vận tốc), cao độ đỉnh so với sàn, nút **Hạ ống qua dầm**, **Đưa lại sát trần**, **Vẽ lại tuyến** (bấm 2 điểm, giữ cỡ – hệ – cao độ). Hiển thị chiều dài, m² tôn, vận tốc thực, số cút (cút 45°) / tê / côn.",
                "Thuộc tính **dàn lạnh / nóng**: công suất nhập theo **BTU/h hoặc kW** (1 BTU/h = 0,000293 kW), cao độ, hướng, toạ độ; đổi công suất thì cặp ống đồng chọn lại. Hiển thị lưu lượng gió, cỡ ống đồng, công suất điện, aptomat và dây cấp cho máy.",
                "Thuộc tính **miệng gió** (cấp / hồi, kích thước, lưu lượng, cao độ) và **ống** (Ø ống lỏng / gas; ống ngưng: Ø, độ dốc ≥ 1 %).")
            .Bullets("**Ống gió đi qua dầm**:",
                "Khi vẽ ống (ô **Tự hạ ống qua dầm** đang bật – mặc định), ống sát trần mà va dầm được **hạ xuống dưới đáy dầm** (khe 50 mm): mỗi phía dầm một đoạn xiên **45°** (2 cút 45°), đoạn xiên chạy ngang bằng độ hạ; chiều dài vùng thấp = bề rộng dầm (tính theo góc ống – dầm) + bề rộng ống chiếu + 2 × khe.",
                "Chỉ hạ khi trần còn chỗ: đáy ống sau khi hạ ≥ sàn + max(**chiều cao thông thủy tối thiểu** 2,4 m; cao độ trần của phòng chứa điểm cắt). Không đủ → giữ nguyên ống và báo cảnh báo **ONG_GIO_KHONG_QUA_DAM** (ghi rõ thiếu bao nhiêu mm) kèm đề xuất: đổi ống dẹt hơn, đi vòng, nâng trần, hoặc lỗ chờ dầm có gia cường được kết cấu duyệt.",
                "Đoạn ống quá ngắn để xiên 45° → hạ cả tuyến dưới dầm (nếu trần cho phép). Lệnh **Hạ ống qua dầm** áp dụng cho ống đang chọn hoặc mọi ống gió của tầng; hai dầm sát nhau gộp thành một lần hạ.")
            .Bullets("**Chọn máy theo phòng** (phòng vẽ / nhận diện ở môi trường Kiến trúc):",
                "Tải lạnh theo phương pháp diện tích: **Q = 600 · A · max(1; h/3) · (1 + 0,15 nếu phòng có tường ngoài) + 500 · max(0; N − 2)** (BTU/h; A diện tích phòng m², h cao độ trần phòng hoặc chiều cao tầng, N số người). Đây là ước tính sơ bộ cho nhà ở – công trình lớn cần tính tải nhiệt chi tiết.",
                "Chọn máy treo tường **9000 / 12000 / 18000 / 24000 BTU** nhỏ nhất ≥ Q; Q > 24000 → nhiều máy cùng cỡ. Nút **Tải lạnh các phòng** lập bảng cho mọi phòng của tầng.",
                "**Đặt máy cho phòng**: bấm vào trong phòng – dàn lạnh đặt trên tường **không có cửa đi**, **xa cửa đi nhất** (thường là tường đối diện cửa), **ưu tiên tường ngoài**; dàn nóng ở mặt ngoài tường ngoài gần nhất; tự nối ống đồng (cỡ theo công suất) và ống nước ngưng xả ra ngoài tường.",
                "Điện máy lạnh: P = Q · 0,000293 / COP (COP = 3), I = P / (220 · 0,85), dòng thiết kế 1,25 · I (động cơ máy nén) → aptomat chuẩn ≥ dòng thiết kế, dây có dòng cho phép ≥ aptomat và ≥ 2,5 mm².")
            .Bullets("**3D và IFC**:",
                "Khung **3D**: chọn dàn lạnh / dàn nóng, miệng gió, ống gió hoặc ống đồng rồi **kéo** trên mặt phẳng ngang (bắt 5 cm); cả tuyến ống gió được tô sáng; Delete xoá. Phụ kiện ống gió hiện thành khối nhỏ tại điểm nối.",
                "Xuất **IFC2X3**: ống gió → IfcFlowSegment + **IfcDuctSegmentType**; cút / tê / côn → IfcFlowFitting + **IfcDuctFittingType** (BEND / JUNCTION / TRANSITION); miệng gió → IfcFlowTerminal + **IfcAirTerminalType**; dàn lạnh / nóng → **IfcEnergyConversionDevice** + **IfcUnitaryEquipmentType** (SPLITSYSTEM); ống đồng / ống ngưng → **IfcPipeSegmentType**; gắn kiểu bằng **IfcRelDefinesByType** (mỗi kích thước / tên một kiểu).");

        c.Sec("mo-hinh.dien-nuoc-dot4", "Điện nước – ĐHKK: bám tường khi kéo, dò tuyến tránh cột, tải lạnh chi tiết, tủ điện, bể nước – bơm, sơ đồ đứng")
            .Bullets("**Kéo trong 3D có bám mặt tường**:",
                "Kéo **dàn lạnh / dàn nóng**: thiết bị tự bám **mặt tường gần nhất** (trong vùng 0,3 m) – tâm cách mặt tường đúng nửa chiều sâu thiết bị, trục song song tường, vị trí dọc tường bắt bước **5 cm** và không vượt khỏi đầu tường; ống đồng, ống nước ngưng đi lại theo. Ra xa tường thì bắt lưới 5 cm theo X, Y.",
                "Kéo **thiết bị điện nước tự do** (đặt trên sàn, không gắn trần): bám mặt tường như trên; thiết bị **gắn tường** giữ tường chủ, chỉ trượt dọc tường. Đồ đạc nội thất vẫn bám tường / góc phòng như trước.",
                "Thanh trạng thái hiện **✓ Bám mặt tường …** khi đã bám, cùng toạ độ đang kéo.")
            .Bullets("**Dò tuyến ống gió tránh cột** (nút **Dò tuyến tránh cột**, hàng Sửa của tab Điều hoà – thông gió):",
                "Áp dụng cho ống gió đang chọn, hoặc mọi ống gió của tầng nếu chưa chọn. Giữ hai đầu ống và cao độ sát trần, tìm tuyến **trực giao** trên lưới mặt bằng (bước mặc định 0,1 m) bằng thuật toán A*.",
                "**Cột** là vật cản (nở thêm nửa bề rộng ống + khe 50 mm); mỗi lần **cắt dầm** phạt 2 m (phải hạ ống), mỗi lần **xuyên tường** phạt 3 m, mỗi lần **đổi hướng** phạt 1 m – chọn tuyến có tổng chiều dài + phạt nhỏ nhất, nên ít cút. Sau đó ống tự hạ qua dầm (nếu bật) và sinh lại phụ kiện.",
                "Bước lưới và các mức phạt sửa trong **Cài đặt điện nước**.")
            .Bullets("**Dàn nóng và tải lạnh**:",
                "**Cài đặt điện nước → ĐHKK – cách lắp dàn nóng mặc định**: **Đặt sàn / ban công** (cao độ tâm 0,4 m, mặc định) hoặc **Giá treo tường** (mặc định 2,2 m so với sàn, sửa được). Áp dụng cho dàn nóng đặt mới (kể cả **Đặt máy cho phòng**).",
                "**ĐHKK – phương pháp tải lạnh**: **Ước lượng nhanh** (phương pháp diện tích, mặc định) hoặc **Chi tiết (CLTD)**. Khi chọn Chi tiết, **Đặt máy cho phòng** và **Tải lạnh các phòng** dùng tải chi tiết.",
                "Tải chi tiết (CLTD rút gọn, nút **Tải lạnh chi tiết**): tường ngoài Q = U·A·CLTD theo hướng (U = 2,0 W/m²K; CLTD Bắc 6, Đông Bắc 10, Đông 14, Đông Nam 12, Nam 9, Tây Nam 14, Tây 16, Tây Bắc 11 °C); kính Q = A·(U·Δt + SHGF·SC·CLF) (U = 5,8; Δt = 9 °C; SHGF Bắc 120, Nam 200, Đông / Tây 500, các hướng chéo 400 W/m²; SC = 0,6; CLF = 0,7); mái (tầng cao nhất) U = 1,8 × CLTD 22 °C; người 75 W hiện + 55 W ẩn; đèn 10 W/m²; thiết bị 15 W/m²; gió lọt 0,5 lần/h: hiện 1,23·Q(l/s)·Δt, ẩn 3010·Q(l/s)·Δw (Δw = 0,008 kg/kg); tổng × 1,1. Hướng tính theo pháp tuyến ra ngoài của tường, trục +Y mặt bằng là hướng Bắc.",
                "Bảng tải chi tiết từng phòng (kèm cột ước lượng nhanh để so sánh) có trong sheet Excel **Vật tư điện nước** khi chọn phương pháp Chi tiết.")
            .Bullets("**Tủ điện** (tab Điện: **Bảng tủ điện**, **Gán mạch theo tủ**):",
                "Chia mạch theo tầng: **chiếu sáng** (đèn, quạt) ≤ 10 điểm / mạch, **ổ cắm** ≤ 8 điểm / mạch, mỗi **máy lạnh** và mỗi **bình nóng lạnh** một mạch riêng.",
                "Mỗi mạch: P = tổng công suất thiết bị, I = P / (220 · cosφ) (cosφ 0,85) → aptomat chuẩn ≥ I (chiếu sáng tối thiểu 10 A, ổ cắm / bình nóng lạnh tối thiểu 16 A, kèm chống dòng rò RCD 30 mA), dây Cu/PVC có dòng cho phép ≥ aptomat (chiếu sáng ≥ 1,5 mm², ổ cắm ≥ 2,5 mm²); máy lạnh theo dòng thiết kế 1,25·I.",
                "Công suất tính toán Ptt = Σ Kc·P (Kc mặc định: chiếu sáng 0,9; ổ cắm 0,5; điều hoà 0,8; bình nóng lạnh 0,7). Tủ **1 pha** khi Ptt ≤ 12 kW, lớn hơn chuyển **3 pha** (hoặc chọn cố định 1 pha / 3 pha trong Cài đặt).",
                "Tủ 3 pha **cân pha**: mạch lớn xếp trước, lần lượt đưa vào pha đang nhẹ nhất; báo tải từng pha và độ lệch pha = max |Pi − P trung bình| / P trung bình.",
                "**Aptomat tổng** ≥ max(dòng tính toán, aptomat nhánh lớn nhất) (1 pha: MCB 2P; 3 pha: MCCB 4P), **cáp tổng** Cu/XLPE có dòng cho phép ≥ aptomat tổng.",
                "**Sơ đồ nguyên lý tủ điện** (nguồn vào – aptomat tổng – thanh cái – các nhánh ghi aptomat, dây, tên mạch, công suất, tầng, pha) có trong bộ bản vẽ điện nước; **bảng tủ điện** có trong sheet Excel. **Gán mạch theo tủ** ghi tên mạch C1, C2… vào thiết bị.")
            .Bullets("**Bể nước – bơm** (tab Cấp thoát nước: **Bể nước – bơm**; khai báo **số người** trong Cài đặt điện nước):",
                "Dung tích V = N · q · số ngày dự trữ / 1000 (m³), q mặc định 150 l/người·ngày; bể mái 1 ngày, bể ngầm 2 ngày + dự trữ chữa cháy; chọn bồn chuẩn 500 … 6000 l (lớn hơn: bội số 1000 l).",
                "Bơm: Q = Q ngày · Kngày (1,3) / số giờ bơm (4 h); ống đẩy PPR chọn theo v ≤ 1,5 m/s; cột áp H = H tĩnh (miệng bể mái − mực nước thấp bể ngầm) + 1,3 × tổn thất Hazen-Williams (C = 140, chiều dài = chiều cao đẩy + 10 m ngang) + 2 m tự do; công suất N = ρ·g·Q·H / η (η = 0,6).",
                "Bảng bể nước và bơm có trong sheet Excel khi đã khai báo số người.")
            .Bullets("**Sơ đồ đứng** (tự thêm vào bộ bản vẽ điện nước, vẽ tỷ lệ 1 như bảng thống kê):",
                "**Sơ đồ không gian cấp nước**: bể ngầm – bơm – ống đẩy lên **bể nước mái** – trục cấp xuống từng tầng; nhánh tầng vẽ xiên 45° với tên thiết bị; Ø trục và Ø nhánh theo tổng đương lượng; ghi dung tích bể, Q, H bơm khi đã khai báo số người.",
                "**Sơ đồ ống đứng thoát nước**: ống đứng thoát xí (vào bể tự hoại) và ống đứng thoát rửa (vào hố ga), ống thông hơi trên mái; nhánh tầng xiên 45°; Ø ống đứng theo tổng đương lượng thoát luỹ kế (ống thoát xí tối thiểu Ø110).");

        c.Sec("mo-hinh.choc-thung-nho-coc", "Chọc thủng đài cọc, cắt mép móng, cọc chịu nhổ, phản lực dầm móng")
            .P("Các kiểm tra móng theo **TCVN 5574:2018** (chọc thủng mục 8.1.6, cắt tiết diện nghiêng mục 8.1.3.3, neo thép mục 10.3.5) và **TCVN 10304:2014** (cọc), dùng chung cho chế độ Thiết kế và Triển khai hồ sơ.")
            .Bullets("**Đài cọc**:",
                "Phản lực cọc Pi = (N + G)/n ± Mx·yi/Σy² ± My·xi/Σx² (đài cứng).",
                "Chọc thủng do cột: tháp 45° từ mép cột tới đầu cọc; cọc có tim nằm trong đáy tháp không gây chọc thủng → F = N − Σ phản lực cọc trong tháp; Fb,u = Rbt·u·h0 (thiết kế: kể hệ số h0/c, chặn 1…2,5).",
                "Chọc thủng **có kể mô men**: F/Fb,u + ΣM/Mb,u ≤ 1, Mb,u = Rbt·Wb·h0 (Wb của đường bao cách mép cột h0/2); M lấy bằng 0,5 mô men không cân bằng của phản lực các cọc ngoài tháp; phần mô men không lớn hơn F/Fb,u.",
                "Chọc thủng do **cọc góc / cọc biên**: đường bao cách mép cọc h0/2, bỏ các cạnh ra ngoài mép đài.",
                "Cắt trên tiết diện nghiêng giữa mép cột và hàng cọc: Q = Σ phản lực cọc ngoài tiết diện ≤ Qb = 1,5·Rbt·b·h0²/c (0,5 ≤ Qb/Rbt·b·h0 ≤ 2,5).",
                "Bản vẽ mặt cắt đài có **tháp chọc thủng 45°** nét đứt.")
            .Bullets("**Cọc chịu nhổ** (khi Pmin < 0):",
                "[Pk] nhập tay, hoặc nhập **Σ(fi·li)** (ma sát thành theo các lớp đất) → [Pk] = 0,8·u·Σfi·li/1,4 + 0,9·Gcọc (TCVN 10304:2014).",
                "Thép cọc / râu thép chịu kéo: Nt ≤ Rs·As.",
                "Neo râu thép vào đài chịu kéo: l0,an = Rs·d/(4·2,5·Rbt), lan = l0,an·As,cal/As,ef ≥ max(0,3l0,an; 15d; 200 mm) – so với chiều dài neo bố trí.",
                "Ghi chú mối nối cọc (bản mã hàn) phải chịu được lực kéo Nt.")
            .Bullets("**Móng đơn** (Triển khai hồ sơ):",
                "Chọc thủng có kể mô men; móng **sát ranh** (lệch tâm): đường bao 3 cạnh (2 cạnh khi cột ở góc), trọng tâm đường bao lệch e0 → kể thêm F·e0.",
                "**Cắt theo mép móng** hai phương: Q = p·b·(l1 − c) ≤ Qb(c), xét c tới min(l1; 3h0); móng vát dùng h0 trung bình đoạn công xôn.",
                "Chiều cao tự chọn tăng tới khi đạt cả chọc thủng có mô men và cắt mép móng; mặt cắt móng vẽ tháp chọc thủng 45°.")
            .Bullets("**Dầm móng / giằng móng** (dầm Vị trí = Dầm móng → mục *Dầm móng: tải trọng & phản lực*):",
                "**Giằng tựa lên móng**: dầm liên tục gối tại tim móng, q = bản thân + tường xây (1,1·18·t·h) + tải khác; phương trình ba mô men → M, Q dùng kiểm tra dầm khi ô nội lực nhập tay = 0, và **phản lực từng gối** ghi trong thông báo.",
                "**Dầm móng trên nền đàn hồi (Winkler)**: k = 120·R nếu không nhập; lực cột tại gối nhập ở *Lực tập trung tại các gối*; kiểm tra áp lực nền pmax ≤ 1,2R; tổng phản lực nền bằng tổng tải.",
                "Móng đơn / đài cọc có ô **Phản lực dầm móng / giằng cộng thêm vào N**: nhập ΣR các giằng tựa lên móng đó.",
                "Chế độ Thiết kế: dầm móng có trong mô hình phân tích thì phản lực chân cột (tải móng) đã gồm tải dầm móng.",
                "**Tự chuyển phản lực giằng**: ghi tên móng ở ô *Móng tại các gối* (vd. M1; M2; Đ1, gối không có móng để trống) → khi tính toàn bộ hồ sơ, phản lực từng gối tự cộng vào N của móng đơn / đài cọc cùng tên (thuyết minh ghi rõ dầm, gối); ô nhập tay khi đó không cộng thêm (tránh cộng hai lần).")
            .Bullets("**Các trường hợp bổ sung**:",
                "**Móng bậc** (Hình dạng đế móng = Bậc): Hb là chiều cao bậc dưới, bậc trên StepL × StepB (0 = tự chọn) → kiểm tra thêm **chọc thủng tại bậc**: F = N − pnet·(Lb + 2h0b)(Bb + 2h0b) ≤ Rbt·u·h0b, u = 2(Lb + Bb + 2h0b); mặt cắt móng vẽ bậc thẳng đứng, khối lượng bê tông / ván khuôn theo bậc.",
                "**Cột chọc thủng móng băng**: mỗi cột (thêm ô *Cạnh cột ngang băng*) kiểm tra chọc thủng có kể mô men với h0 của sườn; đường bao bị cắt bởi mép bản đáy và đầu băng (cột ở đầu băng → 3 cạnh).",
                "**Nhổ khối nhóm cọc** (≥ 2 cọc, có Σfi·li): [Pk,khối] = 0,9·(W khối đất bao nhóm cọc + Gđài) + 0,8·u_khối·Σfi·li/γk ≥ ΣNt các cọc chịu nhổ.");

        c.Sec("mo-hinh.coc-khoan-nhoi-mini", "Cọc khoan nhồi mini (D300–600) và cọc khoan nhồi")
            .P("Đài cọc (Triển khai hồ sơ) → **Loại cọc** = *Cọc khoan nhồi mini (D300–600)* hoặc *Cọc khoan nhồi*: cọc tròn, thép dọc = ô *Râu thép đầu cọc* (số thanh, Ø), bê tông cọc chọn riêng. Tính theo **TCVN 10304:2014**.")
            .Bullets("**Sức chịu tải theo đất nền** (bật *Tính [P] cọc theo địa chất*, nhập bảng lớp đất từ mặt đất: bề dày, loại đất, IL, SPT N, cu, γ, E, qb nhập tay):",
                "Rc,u = γc·(γcq·qb·Ab + u·Σγcf·fi·li), γc = γcq = 1; thân cọc chia đoạn ≤ 2 m.",
                "*Chỉ tiêu cơ lý*: fi theo **Bảng 3** (độ sâu trung bình z, IL; cát thô/vừa, mịn, bụi), qb mũi trong đất dính theo **Bảng 7** (cọc khoan nhồi, z và IL ≤ 0,6); mũi trong cát dùng qb = 150·N hoặc qb nhập tay.",
                "*SPT (Phụ lục G.3.2)*: cát fs = 10·N/3, sét fc = αp·cu (αp theo cu/σ'v); mũi cát qb = 150·N, sét qb = 9·cu (cu = 6,25·N nếu không nhập).",
                "γcf theo công nghệ khoan: dung dịch bentonite 0,6; ống vách / khoan khô 0,7 (sửa được). [P]đ = γ0·Rc,u/(γn·γk) với γ0 = 1,15, γn = 1,15, γk = 1,4 (1,75 khi ít hố khoan).",
                "Sức chịu tải vật liệu Pvl = φ·(γcb·γ'cb·Rb·Ab + Rsc·As), γcb = 0,85, γ'cb = 0,7 (đổ trong dung dịch / dưới nước), φ = 1 (cọc ngàm hoàn toàn trong đất) → [P] = min([P]đ; Pvl). Chế độ Thiết kế (cọc khoan nhồi theo hố khoan SPT) cũng dùng γcb·γ'cb khi tính Pvl.")
            .Bullets("**Kiểm tra bổ sung**:",
                "Hàm lượng thép dọc μ ≥ 0,4 %, khuyến nghị ≥ 6 thanh.",
                "**Tải ngang** (ô *Tải ngang tại chân cột H*, K của nền): αbd = (K·bp/(γc·Eb·I))^(1/5), bp = 1,5d + 0,5, γc = 3; đầu cọc ngàm vào đài: M = H·B0/(αbd·C0), y0 = H·(A0 − B0²/C0)/(αbd³·Eb·I) (A0 = 2,441; B0 = 1,621; C0 = 1,751) – kiểm tra M ≤ Mu ≈ Rs·As·2rs/π, y0 ≤ 10 mm.",
                "**Lún khối móng quy ước**: Bm = Bg + 2L·tan(φtb/4); cộng lún các lớp dưới mũi s = Σ0,8·σz·h/E tới khi σz ≤ 0,2σbt; S ≤ 80 mm.",
                "**Lồng thép**: dài ≥ max(L/3; 6 m; 4/αbd + 1 m), toàn chiều dài khi cọc chịu nhổ; đai xoắn Ø6 (D ≤ 400) / Ø8 bước 100 vùng đầu cọc (≥ 2 m), 200 thân cọc; đai gia cường Ø16 và con kê 4 cái mỗi 2 m.")
            .Bullets("**Bản vẽ & khối lượng**:",
                "Chi tiết cọc (mặt đứng có lồng thép, hai vùng bước đai, chân lồng thép, cao độ đầu / mũi), mặt cắt cọc, trụ địa chất với chiều dài cọc và [P].",
                "Thống kê thép cọc (thép dọc, đai xoắn, đai gia cường); bê tông cọc kể hao hụt mở rộng thành lỗ (×1,15), khoan tạo lỗ (m, từ mặt đất), dung dịch bentonite / ống vách.",
                "Ghi chú thi công và **thử tải**: tremie, bentonite, vét lắng; thử tĩnh ≥ max(1 %; 1 cọc) (TCVN 9393:2012), PIT / siêu âm (TCVN 9396:2012).")
            .Bullets("**Cọc khoan nhồi đường kính lớn (D800–1500+) và tính năng nâng cao**:",
                "*Khoảng cách tim cọc* tối thiểu: cọc mini 3D; D ≥ 800 lấy max(2,5D; D + 1 m). Tim cọc tới mép đài ≥ D/2 + max(0,25 m; D/3). Đài tự chọn dùng các giá trị này.",
                "*Bê tông B22,5* (nội suy B20 – B25): Rb = 13,0 MPa, Rbt = 0,975 MPa, Eb = 28 750 MPa – chọn được cho cọc và mọi cấu kiện.",
                "*Ma sát âm*: đánh dấu lớp **Đất yếu cố kết / đắp nền** trong bảng lớp đất. Mặt trung hoà lấy ở đáy lớp cố kết sâu nhất; bỏ ma sát dương phía trên và trừ lực kéo xuống Pn = u·Σfi·li: [P]đ,net = γ0·(Rc,u − ΣQ)/(γn·γk) − Pn.",
                "*Tự chọn chiều dài cọc* (ô [P] là sức chịu tải yêu cầu): L nhỏ nhất, bước 0,5 m, để [P] ≥ yêu cầu và mũi nằm trong lớp tốt (cát N ≥ 15, sét IL ≤ 0,5 hoặc N ≥ 15, hoặc có qb nhập tay). Không đạt: lấy L có [P] lớn nhất và cảnh báo. Bản vẽ có thêm **đường cong sức chịu tải Rc,u và [P] theo độ sâu mũi cọc**.",
                "*Hệ số nhóm Converse–Labarre* (tuỳ chọn): η = 1 − θ·[(n1 − 1)·n2 + (n2 − 1)·n1]/(90·n1·n2), θ = arctan(D/s); kiểm tra N + G ≤ η·n·[P].",
                "*Đài 1 cọc*: cạnh ≥ max(D + 2·mép; cạnh cột + 200), H ≥ max(600; neo râu thép + ngàm + 100). *Đài 2/3/4 cọc* với D ≥ 800 hoặc đài sâu ((r − c/2)/h0 ≤ 1): mô hình **thanh chống – giằng Blévot** – a = r − k·c (k = 0,25 / 0,30 / √2/4), θ = arctan(h0/a) ≥ 40° (nên 45°–55°), giằng T = R·a/h0 (2 cọc), chia √3 (3 cọc), √2 (4 cọc) theo cạnh, As ≥ T/Rs trên dải đầu cọc; ứng suất nút cột ≤ ν'·Rb, nút đầu cọc ≤ 0,85·ν'·Rb, ν' = 1 − fck/250.",
                "*Lồng thép dài* hơn cây thép 11,7 m: chia đoạn, nối chồng 40d so le 50 % (lệch ≥ 1,3 lần chiều dài nối), thể hiện trên mặt đứng cọc; móc cẩu lồng; con kê 4 / 6 / 8 cái mỗi tầng (D < 800 / < 1200 / ≥ 1200), a2000.",
                "*Ống siêu âm* thép D60×2 (TCVN 9396:2012): 2 ống khi D800–<1000, 3 ống khi D1000–1500, 4 ống khi > 1500; D ≥ 1000 thêm ống khoan lõi mũi cọc D114. Có trong bảng khối lượng, mặt đứng và mặt cắt cọc.",
                "*Hệ số hao hụt bê tông gợi ý* theo địa chất thân cọc (đất yếu 1,20; cát 1,15; sét cứng 1,10); *lún nhóm cọc theo móng tương đương* đặt ở 2L/3 để so sánh với khối móng quy ước.",
                "*Chế độ Thiết kế*: trong hộp thoại Thiết kế móng chọn cọc khoan nhồi và phương pháp TCVN 10304:2014 (Bảng 3/7 hoặc SPT): hố khoan được chia lớp tại mực nước ngầm (γ' = γsat − 10 dưới nước), đất đắp / bùn được coi là cố kết (ma sát âm); có tuỳ chọn tự chọn chiều dài cọc, bê tông cọc riêng và hệ số nhóm. Để trống phương pháp: tính như phiên bản trước.")
            .Bullets("**Mặt bằng định vị cọc** (Triển khai hồ sơ):",
                "Khi có đài cọc đặt trên mặt bằng móng, cây thư mục thêm nút **Mặt bằng định vị cọc** ngay sau Mặt bằng móng; bản vẽ gồm lưới trục, đường bao đài, cọc đánh số P1…Pn (theo đài từ trên xuống, trong đài theo hàng), kể cả đài xoay.",
                "**Bảng toạ độ cọc** (X, Y theo hệ trục công trình, lực lớn nhất lên cọc) và **bảng thống kê cọc** (đài, D, L, cao độ đầu cọc = −Df + ngàm, cao độ mũi = −Df − L, [P], số lượng) – dùng cho định vị cọc ngoài hiện trường.");

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
                "Tuỳ chọn ở {m:drawStyle} (tab **Tỷ lệ, cỡ chữ, chú thích**): **Cánh sàn trên mặt cắt dầm** (bật/tắt), **Thép sàn trong cánh** (thép lớp dưới, thép mũ vắt qua dầm, thép phân bố, nhãn \"Ø8a200 (sàn)\" theo kết quả thiết kế sàn; chưa thiết kế sàn thì vẽ quy ước nét đứt), **Chiều dài cánh sàn thể hiện (mỗi bên dầm)** (30…3000 mm, mặc định 50 – đoạn sàn ngắn chỉ để nhận biết dầm chữ T / L), **Đường cắt đầu cánh sàn**: Dích dắc / Nét thẳng.")
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

        c.Sec("mong.ban-ve-bo-sung", "Bản vẽ móng, mặt cắt và điện nước bổ sung")
            .Bullets("**Móng và cọc**:",
                "Mặt bằng móng (Triển khai hồ sơ): móng bậc vẽ thêm **đường bao bậc trên** quanh tim cột và hai chuỗi kích thước (mép đế → bậc → mép đế) theo hai phương.",
                "Chế độ Thiết kế, móng cọc: tờ **KC-02.1** gồm *mặt bằng định vị cọc* (cọc đánh số P1…Pn từ trên xuống, trái sang phải; trục, đường bao đài, **bảng toạ độ cọc** X, Y, cao độ đầu / mũi, **bảng thống kê cọc**) và – khi chọn phương pháp TCVN 10304 cho cọc khoan nhồi – **đường cong sức chịu tải theo chiều dài cọc** của hố khoan đang chọn ({s:mong.coc}).",
                "Bê tông **B22,5** có trong mọi danh sách chọn bê tông của chế độ Thiết kế.")
            .Bullets("**Mặt cắt công trình**:",
                "Móng bè (Móng → bề dày bè > 0): mặt cắt vẽ bản bè liên tục (vươn ra ngoài tim cột biên), sườn bè, cổ cột và lót bê tông liền.",
                "Móng băng chạy **dọc theo đường cắt**: vẽ thành một dải liên tục (bản cánh + sườn) thay cho các chữ T ngược rời.",
                "Lớp hoàn thiện sàn vẽ **theo từng phòng** trên đường cắt: WC / vệ sinh / tắm có chống thấm + vữa + gạch men; phòng lát đá, sàn gỗ, phòng thường theo tên phòng / ô *Hoàn thiện sàn*. Ngoài phạm vi phòng không vẽ lớp hoàn thiện.")
            .Bullets("**Điện nước**:",
                "Bộ bản vẽ điện nước có thêm **mặt cắt dọc tuyến thoát** cho mỗi chuỗi ống thoát (tỉ lệ ngang 1/50, đứng 1/10): đáy sàn, đường đáy / đỉnh ống, nhãn Ø – i – L từng đoạn, phụ kiện, bảng khoảng cách – cao độ đáy ống – độ dốc. Chênh cao đáy ống mỗi đoạn = L·i.",
                "**Độ dốc riêng theo hệ thống**: **Cài đặt điện nước…** → bảng luật (hệ thống, Ø từ – đến, độ dốc, min / max); ví dụ thoát nước mưa 1 % mọi Ø. Thứ tự ưu tiên: độ dốc nhập tay của tuyến → luật hệ thống → mặc định theo Ø.",
                "Xuất khối lượng Excel có thêm sheet **Vật tư điện nước** (thiết bị, ống theo Ø, ống gen, dây, cáp, phụ kiện, quang treo) và bảng độ dốc từng tuyến thoát.",
                "Nhập Revit: bật *Nhập ống / thiết bị Revit thành đối tượng điện nước* để thiết bị vệ sinh, ổ cắm, công tắc, đèn, quạt… được đặt theo họ trong thư viện điện nước (so tên họ Revit), ống / ống luồn dây thành tuyến giữ đúng hình học Revit, đầu ống trùng nhau nối chung một điểm. Phụ kiện ống bỏ qua (chương trình tự sinh); ống gió / miệng gió / dàn lạnh – dàn nóng thành đối tượng điều hoà – thông gió; khay cáp và phần tử không nhận ra vẫn nhập dạng khối chung. Cập nhật từ Revit xoá / thêm theo UniqueId.");

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
            .P("Xem {s:cai-dat.smartscreen}. Bộ cài báo file hỏng: tải lại VietConst-Setup.exe (kiểm tra dung lượng đủ khoảng 50 MB).");

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
