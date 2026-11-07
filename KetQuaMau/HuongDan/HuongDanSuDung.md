# Hướng dẫn sử dụng phần mềm Việt Const

*Phân tích – thiết kế kết cấu nhà dân dụng dưới 7 tầng (khung BTCT) và khung thép tiền chế. Tác giả: Vũ Xuân Tuyến.*

Trong chương trình: **Trợ giúp → Hướng dẫn sử dụng…** (hoặc phím **F1** – mở đúng mục đang làm).

## Mục lục

- 1. Giới thiệu
  - 1.1. Phạm vi áp dụng
  - 1.2. Tiêu chuẩn và căn cứ áp dụng
  - 1.3. Cấu trúc phần mềm
  - 1.4. Giới hạn đã biết của phiên bản hiện tại
  - 1.5. Quy ước trong tài liệu
- 2. Cài đặt và cập nhật
  - 2.1. Yêu cầu hệ thống
  - 2.2. Cài bằng bộ cài VietConst-Setup.exe
  - 2.3. Cảnh báo Windows SmartScreen
  - 2.4. Add-in AutoCAD (liên kết AutoCAD → Việt Const)
  - 2.5. Add-in Revit
  - 2.6. Bản chạy ngay không cần cài
  - 2.7. Cập nhật phiên bản
  - 2.8. Gỡ cài đặt
  - 2.9. Dữ liệu người dùng (%AppData%\KetCauVXT)
- 3. Giao diện tổng quan
  - 3.1. Bố cục cửa sổ chính
  - 3.2. Menu chính
  - 3.3. Thanh công cụ
  - 3.4. Các tab nhập liệu (cột trái)
  - 3.5. Khung nhìn: Mặt bằng, 3D, Biểu đồ nội lực, Mô hình móng, Dao động
  - 3.6. Thanh công cụ của tab Mặt bằng
  - 3.7. Thanh trạng thái và ô thông tin
  - 3.8. Hoàn tác, lưu và phím tắt
  - 3.9. Trợ giúp ngay trên phần mềm
  - 3.10. Giao diện gọn – mở rộng vùng làm việc
  - 3.11. Cây mô hình, menu Xem / Sửa, thanh trạng thái
- 4. Hai chế độ làm việc – Triển khai hồ sơ
  - 4.1. Cửa sổ Triển khai hồ sơ
  - 4.2. Dầm, cột, sàn – nhập kích thước, chọn thép
  - 4.3. Mặt bằng sàn – hạ cốt, nhấn thép, khoét lỗ, thép tự động / thủ công
  - 4.4. Lanh tô cửa, ô văng cửa
  - 4.5. Vẽ mặt bằng kết cấu, nhân bản tầng, ánh xạ kiến trúc → lanh tô các tầng
  - 4.6. Lấy kích thước từ AutoCAD (lệnh VXT_KT)
  - 4.7. Móng đơn, móng băng, móng cọc
  - 4.8. Móng bè
  - 4.9. Bê tông dự ứng lực (căng sau)
  - 4.10. Bể nước BTCT
  - 4.11. Cầu thang bộ – bản thang, bậc gạch / BTCT, zíc zắc, xương cá, thang thép
  - 4.12. Hố pít – vách thang máy
  - 4.13. Trích chi tiết, mặt cắt dầm, shop drawing, điểm cắt thép
  - 4.14. Xuất hồ sơ
  - 4.15. Dàn trang in, xuất PDF toàn bộ, in trực tiếp
  - 4.16. Chia dầm nhiều đoạn, xuất tất cả theo bộ dàn trang
  - 4.17. Hoàn thiện bản vẽ: cửa trên mặt bằng, lanh tô từ mô hình, cột lệch tâm, tham chiếu tờ
  - 4.18. Khổ giấy và tỷ lệ tự động
  - 4.19. Khung tên, tên và số hiệu bản vẽ
  - 4.20. Bản vẽ kiểu Revit: trình duyệt dự án, khung nhìn, bản vẽ (sheet), thuộc tính, cỡ chữ
- 5. Quy trình làm việc tổng quát
  - 5.1. Sơ đồ các bước
  - 5.2. Bảng Bắt đầu nhanh
  - 5.3. Nguyên tắc chung
  - 5.4. Ví dụ: nhà phố 4 tầng 5×13,5 m
- 6. Khởi tạo dự án
  - 6.1. Dự án mới từ lưới trục (tab Công trình)
  - 6.2. Mở mặt bằng CAD lấy dữ liệu: chọn trục, trục qua tim / mép cột
  - 6.3. Mẫu định dạng (template)
  - 6.4. Khung thép tiền chế
  - 6.5. Từ bản vẽ kiến trúc DWG/DXF (triển khai kết cấu tự động)
  - 6.6. Nhập mặt bằng kết cấu DWG/DXF
  - 6.7. Từ AutoCAD (add-in, lệnh VXT_*)
  - 6.8. Chuẩn hoá layer bản vẽ về layer chuẩn Việt Const (VXT-…)
  - 6.9. Cao độ từ móng đến mái, bộ môn Kiến trúc – Kết cấu – Điện nước, IFC / SketchUp / Revit
  - 6.10. Chi tiết kiến trúc kiểu Revit: mái, trần, lan can, phào chỉ, ốp – sơn, chép tầng, nhập mô hình SketchUp
  - 6.11. Đặt đồ đạc bằng bắt điểm, kích thước tạm, thao tác 3D, mặt cắt tầng và mặt cắt công trình
  - 6.12. Liên kết bản vẽ AutoCAD (DWG/DXF) làm nền mặt bằng
  - 6.13. Từ Revit (JSON)
  - 6.14. Từ ETABS / SAP2000 (.e2k / .s2k)
  - 6.15. Mở, lưu dự án, dự án mẫu
- 7. Lập mô hình kết cấu
  - 7.1. Lưới trục và tầng
  - 7.2. Quét chọn mọi đối tượng (khung bao / khung cắt)
  - 7.3. Thêm, dời, xoá lưới trục
  - 7.4. Thêm tầng, sửa cao độ tầng
  - 7.5. Mặt đứng công trình và vẽ trục trên mặt đứng
  - 7.6. Vật liệu
  - 7.7. Tiết diện dầm, cột, sàn
  - 7.8. Cột
  - 7.9. Vẽ dầm bằng 2 điểm (＋ Dầm)
  - 7.10. Công cụ dầm kiểu ETABS
  - 7.11. Dầm cong
  - 7.12. Sàn và cấu tạo sàn
  - 7.13. Ban công công xôn
  - 7.14. Cầu thang
  - 7.15. Giếng trời, thông tầng, sàn vệ sinh
  - 7.16. Kéo, trim, extend dầm; pick đối tượng CAD
  - 7.17. Thanh lệnh theo cấu kiện, chọn nhiều, gán tải, dòng lệnh kiểu AutoCAD
  - 7.18. Giao diện và lệnh vẽ kiểu Revit: kiểu tường nhiều lớp, cửa tự cắt tường
  - 7.19. Kiểu Revit – tay nắm, Tab, làm lại, lệnh sửa bằng chuột, 3D có ô cửa
  - 7.20. Bộ môn Điện nước: thiết bị chôn ngầm trong tường, tuyến ống / dây, cảnh báo khi kiến trúc thay đổi
  - 7.21. Thông số theo TCVN và tuyến ống gen đi trong trần / sàn
  - 7.22. Khoảng hở ống gen, khí hậu theo tỉnh, chiếu sáng LPD, ống đứng thoát nước và bể tự hoại
  - 7.23. Thoát nước đi dưới sàn có độ dốc; cảnh báo điểm nguy hiểm cho kết cấu và chi tiết gia cường
  - 7.24. Lỗ xuyên điện nước trong hồ sơ kết cấu; tường trừ rãnh điện nước; kích thước tạm tường / cột
  - 7.25. Điều hoà – thông gió, cài đặt điện nước, kéo cao độ trên mặt cắt, kiểu trục mặt bằng kết cấu
  - 7.26. Điều hoà – thông gió: sửa từng đối tượng, hạ ống qua dầm, chọn máy theo phòng
  - 7.27. Điện nước – ĐHKK: bám tường khi kéo, dò tuyến tránh cột, tải lạnh chi tiết, tủ điện, bể nước – bơm, sơ đồ đứng
  - 7.28. Chọc thủng đài cọc, cắt mép móng, cọc chịu nhổ, phản lực dầm móng
  - 7.29. Cọc khoan nhồi mini (D300–600) và cọc khoan nhồi
  - 7.30. Sàn hạ cốt (hạ cos)
  - 7.31. Tường – loại tường, vẽ và gán tường
  - 7.32. Chọn, xoá, chia thanh
  - 7.33. Chọn đối tượng (menu Chọn)
  - 7.34. Gán tải trọng, liên kết, giải phóng, tiết diện (menu Gán)
  - 7.35. Khai báo trường hợp tải, trường hợp phân tích, phổ phản ứng, sàn cứng (menu Khai báo)
  - 7.36. Dầm có cánh sàn (chữ T / L) trên bản vẽ
  - 7.37. Địa chất – hố khoan (tab Địa chất)
  - 7.38. Móng – loại móng và tab Mô hình móng
  - 7.39. Móng cọc – bố trí cọc
  - 7.40. Móng lệch tâm, ranh giới đất, lùi cọc
  - 7.41. Gia cố nền bằng cừ tràm
- 8. Tải trọng và tổ hợp
  - 8.1. Trường hợp tải (tab Tải trọng)
  - 8.2. Tải sàn theo công năng và cấu tạo sàn
  - 8.3. Tải tường
  - 8.4. Tải trên thanh và tải trên sàn nhập tay
  - 8.5. Gió – địa danh, vùng gió
  - 8.6. Động đất (TCVN 9386-1:2025)
  - 8.7. Tổ hợp tải trọng
  - 8.8. Sửa tổ hợp tải trọng (Load Combinations)
  - 8.9. Nguồn khối lượng (cho dao động, động đất)
- 9. Phân tích và đọc kết quả
  - 9.1. Chạy phân tích
  - 9.2. Dao động riêng (tab Dao động)
  - 9.3. Phổ phản ứng
  - 9.4. Tab Kết quả
  - 9.5. Biểu đồ nội lực
  - 9.6. Chuyển vị, phản lực, lực ngang tầng
  - 9.7. Nhập kết quả ETABS / SAP2000 / SAFE / phần mềm khác
  - 9.8. So sánh với ETABS / SAP2000 / SAFE
  - 9.9. Bảng kết quả (Show Tables)
  - 9.10. Kiểm tra mô hình (Check Model)
  - 9.11. Hình biến dạng (Deformed Shape)
  - 9.12. Mô hình không ổn định, lỗi phân tích
- 10. Thiết kế cấu kiện
  - 10.1. Thiết kế BTCT – quy trình chung
  - 10.2. Dầm
  - 10.3. Cột
  - 10.4. Sàn – phương án thép sàn
  - 10.5. Móng – thiết kế và kiểm tra
  - 10.6. Bản vẽ móng, mặt cắt và điện nước bổ sung
  - 10.7. Cầu thang
  - 10.8. Dầm cong – xoắn
  - 10.9. Kết cấu thép tiền chế và tab Thép
  - 10.10. Chọn bố trí thép
  - 10.11. Tuỳ chọn chọn thép và gia công
  - 10.12. Tự tăng tiết diện đến khi đạt
  - 10.13. Ý nghĩa các cảnh báo và kết luận
- 11. Triển khai hồ sơ
  - 11.1. Bộ bản vẽ DXF KC-01 … KC-08
  - 11.2. Thiết lập bản vẽ (tỷ lệ, cỡ chữ, kiểu kích thước)
  - 11.3. Chuẩn layer CAD
  - 11.4. Xuất gộp một file / tách rời, mẫu tên file
  - 11.5. Định dạng xuất DXF R12 / DXF 2018 / DWG 2018 và mẫu tô HATCH
  - 11.6. Dàn trang và in bản vẽ
  - 11.7. Khung tên người dùng, tờ mẫu
  - 11.8. Hiệu chỉnh bản vẽ sau khi xuất
  - 11.9. Shop drawing thép (gia công, nối thép, vùng cấm nối)
  - 11.10. Bảng khối lượng vật tư
  - 11.11. Thuyết minh tính toán (Word)
  - 11.12. Bảng tính Excel, nội lực CSV, mô hình ETABS/SAP
  - 11.13. Xuất tất cả vào thư mục
  - 11.14. Phụ lục tính toán
- 12. Kiểm chứng và kiểm tra chéo
  - 12.1. Báo cáo kiểm chứng phần mềm
  - 12.2. Kiểm chứng độc lập (mục 5 của báo cáo)
  - 12.3. Kiểm tra chéo với ETABS / SAP2000
  - 12.4. Trách nhiệm của người dùng
- 13. Văn bản pháp lý và tiêu chuẩn
  - 13.1. Danh mục văn bản
  - 13.2. Cập nhật danh mục
- 14. Câu hỏi thường gặp và xử lý lỗi
  - 14.1. Không đọc được file DWG
  - 14.2. Windows chặn bộ cài / chương trình
  - 14.3. Kết quả lệch so với ETABS – nguyên nhân
  - 14.4. Mô hình không ổn định / không phân tích được
  - 14.5. Nhiều cấu kiện không đạt
  - 14.6. Mất các sửa đổi trên mặt bằng
  - 14.7. Mở lại dự án mất kết quả nhập từ ETABS
  - 14.8. Thuyết minh Word không có số trang / mục lục trống
  - 14.9. AutoCAD không thấy lệnh VXT
- 15. Phụ lục
  - 15.1. Phím tắt và thao tác chuột
  - 15.2. Định dạng file
  - 15.3. Thư mục dữ liệu
  - 15.4. Dòng lệnh
  - 15.5. Bảng đường dẫn menu được nhắc trong tài liệu

## 1. Giới thiệu

**Việt Const** là phần mềm phân tích – thiết kế kết cấu cho **nhà dân dụng dưới 7 tầng** (khung bê tông cốt thép) và **nhà thép tiền chế** (khung zamil). Chương trình lập mô hình khung không gian, tính nội lực bằng phần tử hữu hạn, thiết kế cốt thép dầm, cột, sàn, móng, cầu thang theo tiêu chuẩn Việt Nam, rồi triển khai hồ sơ: bản vẽ DXF, dàn trang in, bảng gia công thép, bảng khối lượng, thuyết minh Word, bảng tính Excel và mô hình ETABS/SAP2000 để kiểm tra chéo.

Hướng dẫn này đi từ cài đặt, khởi tạo dự án đến khi sử dụng kết quả, dành cho cả kỹ sư đang hành nghề và người mới bắt đầu. Trong chương trình, nhấn **F1** để mở đúng mục hướng dẫn của tab, công cụ hoặc hộp thoại đang dùng.

### 1.1. Phạm vi áp dụng

- Nhà ở riêng lẻ, nhà phố, nhà liền kề, biệt thự, nhà văn phòng nhỏ **dưới 7 tầng**, khung BTCT toàn khối (cột – dầm – sàn), móng đơn, móng băng, móng cọc, gia cố nền bằng cừ tràm. Khi tạo mô hình quá 7 tầng chương trình cảnh báo: kết quả với nhà cao hơn cần kiểm tra thêm.
- Nhà xưởng, nhà kho **khung thép tiền chế** một nhịp (cột, kèo tiết diện chữ I tổ hợp có thể vát), kiểm tra bền, ổn định, độ mảnh, ổn định cục bộ bụng, chuyển vị.
- Dầm cong trên mặt bằng (ban công cong, mặt tiền cong), cầu thang (bản chịu lực, có dầm cốn), ban công công xôn, giếng trời, sàn vệ sinh hạ cốt.
- Nhập mô hình từ bản vẽ kiến trúc DWG/DXF, add-in AutoCAD, add-in Revit, file ETABS .e2k / SAP2000 .s2k; nhập kết quả nội lực từ ETABS/SAP2000/SAFE.

> **Lưu ý:** Chương trình là công cụ hỗ trợ. Kỹ sư có chứng chỉ hành nghề chịu trách nhiệm kiểm tra số liệu đầu vào (đặc biệt hệ số tải trọng, điều kiện địa chất) và kết quả trước khi dùng cho hồ sơ chính thức.

### 1.2. Tiêu chuẩn và căn cứ áp dụng

Danh mục dưới đây lấy trực tiếp từ danh mục văn bản của chương trình (**Trợ giúp → Văn bản pháp lý & tiêu chuẩn áp dụng…**); thuyết minh, phụ lục, ghi chú chung bản vẽ và bảng khối lượng trích dẫn theo đúng danh mục này.

| Nội dung | Văn bản / tiêu chuẩn |
|---|---|
| Tải trọng và tác động | TCVN 2737:2023 |
| Số liệu điều kiện tự nhiên (vùng gió, gia tốc nền) | QCVN 02:2022/BXD |
| Kết cấu bê tông cốt thép | TCVN 5574:2018 |
| Thép cốt bê tông | TCVN 1651:2018 |
| Kết cấu thép | TCVN 5575:2024 |
| Động đất | TCVN 9386-1:2025; TCVN 9386-5:2025 |
| Nền nhà và công trình | TCVN 9362:2012 |
| Móng cọc | TCVN 10304:2014 |
| Luật Xây dựng | Luật Xây dựng số 135/2025/QH15 |
| Quản lý hoạt động xây dựng | Nghị định 217/2026/NĐ-CP |
| Cấp công trình | Thông tư 34/2026/TT-BXD |
| Định mức (bảng khối lượng, tham khảo) | Thông tư 38/2026/TT-BXD; Công văn 9947/BXD-VPB |

> **Lưu ý:** Tải trọng động đất tính theo TCVN 9386-1:2025 (thay thế TCVN 9386:2012; cùng cơ sở EN 1998-1 với TCVN 9386:2012) bằng phương pháp tĩnh lực ngang tương đương, phổ thiết kế loại 1 – người dùng đối chiếu phổ phản ứng, hệ số tầm quan trọng, gia tốc nền theo bản 2025.

### 1.3. Cấu trúc phần mềm

| Thành phần | Chức năng |
|---|---|
| KetCauVXT.exe (giao diện WPF) | Cửa sổ chính, các hộp thoại, dàn trang, tự kiểm tra giao diện (--selftest). |
| KetCauVXT.Core (thư viện lõi) | Mô hình, phân tích phần tử hữu hạn (khung Timoshenko 12 bậc tự do, sàn cứng, giải phóng liên kết, thanh vát), tải trọng, thiết kế BTCT / thép / móng, bản vẽ DXF, Word, Excel, ETABS/SAP, kiểm chứng. Không dùng thư viện ngoài. |
| KetCauVXT.Dwg | Đọc trực tiếp bản vẽ DWG (AutoCAD R14 … 2018+) bằng thư viện ACadSharp (MIT); nếu đọc lỗi thì thử ODA File Converter. |
| Add-in AutoCAD 2021–2026 (tuỳ chọn) | Lệnh VXT, VXT_TUONG, VXT_COT, VXT_GUI… tích chọn đối tượng kiến trúc rồi gửi sang chương trình. |
| Add-in Revit 2023–2026 (tuỳ chọn) | Lệnh "Xuất sang Việt Const": xuất lưới, tầng, cột, dầm, sàn ra file JSON. |

Đơn vị nội bộ là **kN, m**. Kích thước tiết diện nhập bằng **mm**; bản vẽ, thuyết minh hiển thị mm, MPa, kN.m. Số thập phân trong các ô nhập dùng dấu chấm (vd. 3.6).

### 1.4. Giới hạn đã biết của phiên bản hiện tại

- Phân tích tĩnh tuyến tính bậc nhất: chưa kể P-Delta, phi tuyến vật liệu, phân phối lại nội lực; chưa tính bề rộng vết nứt acrc.
- Gió: chỉ thành phần tĩnh W = W0·k(z)·c (nhà thấp tầng); k(z) dạng hàm mũ – đối chiếu bảng của TCVN 2737:2023.
- Động đất: tĩnh lực ngang tương đương (mặc định) hoặc phổ phản ứng sau phân tích dao động riêng; phổ loại 1 lập theo cơ sở EN 1998-1 như TCVN 9386:2012 – người dùng đối chiếu bản 2025 (phổ, hệ số tầm quan trọng, gia tốc nền).
- Thiết kế dầm (uốn, cắt, võng) theo tiết diện **chữ nhật b×h** – không kể cánh sàn chịu nén (thiên về an toàn); bản vẽ mặt cắt dầm vẫn thể hiện cánh sàn chữ T / L (*mục 7.36 (Dầm có cánh sàn (chữ T / L) trên bản vẽ)*). Chịu cắt kiểm tra với c = 2h0 (đơn giản hoá) – kiểm tra lại dầm chịu lực tập trung lớn.
- Ô sàn bị tường hoặc dầm xiên cắt vẫn tính như ô chữ nhật (Marcus theo hình bao); tự kiểm tra các ô sàn hình dạng đặc biệt.
- Độ võng dùng độ cứng gần đúng (φb,cr = 2,5); lún dùng β = 0,8 cho mọi loại đất, ứng suất theo Boussinesq.
- Kết quả nội lực nhập từ ETABS/SAP2000/SAFE chỉ dùng trong phiên làm việc, **không lưu vào file dự án** – mở lại dự án phải nhập lại.
- Khung thép tiền chế: chưa thiết kế liên kết bu lông, bản mã; bộ bản vẽ KC-01…KC-08 dành cho kết cấu BTCT (chưa có bản vẽ chi tiết khung thép). Đối chiếu công thức với TCVN 5575:2024 trước khi dùng cho hồ sơ chính thức.
- Nhập bản vẽ kiến trúc: chưa hỗ trợ tường xiên/cong, tầng hầm, tầng lửng, bản vẽ PDF/ảnh; tường tầng 1 coi như đặt trên giằng móng.
- Khối lượng, định mức là giá trị tham khảo – không thay thế dự toán được lập và thẩm tra theo quy định.
- Trên mặt bằng chưa có công cụ vẽ cột riêng: cột được tạo khi tạo khung từ lưới trục, nhập bản vẽ / Revit / ETABS (xem *mục 7.8 (Cột)*).

Danh sách giới hạn đầy đủ, kèm bài toán kiểm chứng tương ứng, nằm ở mục 7 "Giới hạn đã biết" của Báo cáo kiểm chứng (**Trợ giúp → Kiểm chứng phần mềm…**).

### 1.5. Quy ước trong tài liệu

- **Tệp → Mới**: đường dẫn menu (menu Tệp, mục Mới). Tên nút, tab, ô nhập viết **đậm** đúng như trên màn hình.
- Trục 1, 2, 3… song song phương Y, đặt theo phương X; trục A, B, C… song song phương X, đặt theo phương Y.
- Quy ước dấu nội lực giống ETABS: P kéo dương, M3 dương căng thớ dưới dầm; biểu đồ mô men vẽ về phía thớ chịu kéo.
- Ô ghi "Lưu ý", "Mẹo", "Cảnh báo" là thông tin quan trọng; mục ghi [SẮP CÓ] mô tả tính năng đang được hoàn thiện.

## 2. Cài đặt và cập nhật

### 2.1. Yêu cầu hệ thống

- Windows 10 hoặc 11, 64-bit. **Không cần cài .NET** (chương trình đóng gói tự chứa).
- Màn hình từ 1366×768 (khuyến nghị 1920×1080) – cửa sổ chính mặc định 1440×860.
- Tuỳ chọn: AutoCAD 2021–2026 (add-in liên kết), Revit 2023–2026 (add-in xuất mô hình), Microsoft Word/Excel hoặc LibreOffice để mở thuyết minh và bảng tính, ETABS/SAP2000 để kiểm tra chéo.

### 2.2. Cài bằng bộ cài VietConst-Setup.exe

Bộ cài là **một file duy nhất VietConst-Setup.exe**: tải về rồi chạy.

1. Lưu và đóng Việt Const nếu đang mở – bộ cài không tự đóng chương trình để tránh mất dữ liệu chưa lưu.
2. Chạy **VietConst-Setup.exe**. Chọn kiểu cài: **chỉ cho tôi** (không cần quyền quản trị, cài vào %LocalAppData%\Programs\KetCauVXT) hoặc **cho mọi người dùng** (cần quyền quản trị, cài vào Program Files).
3. Đọc trang giới thiệu, chọn thư mục cài (mặc định ...\KetCauVXT).
4. Trang **Tuỳ chọn thêm**: Tạo biểu tượng trên Desktop; Mở file dự án .kcvxt bằng Việt Const (liên kết file); **Cài add-in AutoCAD 2021–2026** (mặc định không chọn – chọn nếu dùng liên kết AutoCAD).
5. Bấm Cài đặt, xong chọn **Mở Việt Const**.

Sau khi cài, menu Start có nhóm **Việt Const** gồm: chương trình, **Dự án mẫu** (thư mục DuAnMau: NhaPho.kcvxt, TuBanVeKienTruc.kcvxt và bản vẽ kiến trúc mẫu *_DauVao.dxf), **Hướng dẫn sử dụng**, **Hướng dẫn sử dụng (Word)** và **Gỡ cài đặt**.

### 2.3. Cảnh báo Windows SmartScreen

Bộ cài và chương trình chưa có chữ ký số thương mại nên lần đầu chạy Windows có thể hiện "Windows đã bảo vệ máy tính của bạn". Bấm **Thông tin thêm** (More info) → **Vẫn chạy** (Run anyway). Chỉ làm như vậy với file tải từ trang phát hành chính thức của dự án (GitHub Releases của tác giả).

> **Mẹo:** Nếu trình duyệt cảnh báo khi tải, chọn "Giữ lại" (Keep). Có thể bấm chuột phải file → Properties → tích Unblock trước khi chạy.

### 2.4. Add-in AutoCAD (liên kết AutoCAD → Việt Const)

1. Cách 1: khi cài chọn **Cài add-in AutoCAD 2021–2026** – gói KetCauVXT.bundle được chép vào thư mục tự nạp ApplicationPlugins (cho mọi người dùng: ProgramData; cho một người: AppData).
2. Cách 2 (bản zip): đóng AutoCAD, bấm đúp **AutoCAD\CaiDatAutoCAD.cmd**; gỡ bằng **CaiDatAutoCAD.cmd -Go**. Có thể nạp thủ công bằng lệnh NETLOAD (chọn DLL đúng năm AutoCAD).
3. Mở AutoCAD, nếu được hỏi chọn **Always Load / Luôn nạp**, gõ lệnh **VXT** để hiện bảng lệnh.
4. Mở KetCauVXT.exe ít nhất một lần để chương trình ghi đường dẫn vào %AppData%\KetCauVXT\app_path.txt – add-in dùng đường dẫn này để tự mở chương trình khi gửi dữ liệu.

Cách dùng các lệnh: xem *mục 6.7 (Từ AutoCAD (add-in, lệnh VXT_*))*.

### 2.5. Add-in Revit

Add-in **KetCauVXT.RevitExport** được build riêng cho Revit 2023–2026. Chép KetCauVXT.RevitExport.dll và KetCauVXT.RevitExport.addin vào %AppData%\Autodesk\Revit\Addins\<năm>\, mở Revit → tab Add-Ins → External Tools → **Xuất sang Việt Const**. Xem *mục 6.13 (Từ Revit (JSON))*.

### 2.6. Bản chạy ngay không cần cài

Giải nén **KetCauVXT-win-x64.zip** vào một thư mục bất kỳ (không cần quyền quản trị) và chạy **KetCauVXT.exe**. Bản zip có kèm thư mục AutoCAD (add-in + CaiDatAutoCAD.cmd) và HuongDan.md. Bản này không tạo liên kết file .kcvxt và không có mục gỡ cài đặt – xoá thư mục là gỡ.

### 2.7. Cập nhật phiên bản

- Tải bộ cài mới và chạy đè lên bản cũ: bộ cài nhớ thư mục và các tuỳ chọn đã chọn lần trước.
- Dữ liệu dự án (.kcvxt) và thiết lập cá nhân (%AppData%\KetCauVXT) **không bị xoá** khi cài đè hoặc gỡ cài đặt.
- Sau khi cập nhật nên mở **Trợ giúp → Văn bản pháp lý & tiêu chuẩn áp dụng…** để xem danh mục văn bản mới và bấm **Kiểm tra dự án** với các dự án cũ.

### 2.8. Gỡ cài đặt

Settings → Apps → **Việt Const** → Uninstall, hoặc menu Start → Việt Const → **Gỡ cài đặt Việt Const**. Gỡ cài đặt xoá cả add-in AutoCAD trong ApplicationPlugins (nếu đã cài), giữ nguyên dự án và %AppData%\KetCauVXT.

### 2.9. Dữ liệu người dùng (%AppData%\KetCauVXT)

| File / thư mục | Nội dung |
|---|---|
| CadStandard.json | Chuẩn layer CAD, font, thiết lập bản vẽ mặc định của người dùng (**Tuỳ chọn → Chuẩn layer CAD…**, **Tuỳ chọn → Thiết lập bản vẽ (tỷ lệ, cỡ chữ)…**). |
| Templates\ | Thư viện mẫu định dạng *.kcvxt-mau (**Tuỳ chọn → Mẫu định dạng (template)…**). |
| DiaDanh.json | Danh mục địa danh – vùng gió, agR, loại nền do người dùng nhập / lưu (**Tuỳ chọn → Danh mục địa danh (QCVN 02:2022)…**). |
| VanBanPhapLy.json | Danh mục văn bản pháp lý người dùng lưu làm mặc định (**Trợ giúp → Văn bản pháp lý & tiêu chuẩn áp dụng…**). |
| KiemChung.json | Giá trị kiểm chứng độc lập do người dùng nhập (**Trợ giúp → Kiểm chứng phần mềm…**). |
| XuatBanVe.json | Lựa chọn xuất bản vẽ gần nhất (gộp / tách, mẫu tên file). |
| Link\acad_*.json | Dữ liệu gửi từ AutoCAD (lệnh VXT_GUI). |
| app_path.txt | Đường dẫn KetCauVXT.exe cho add-in AutoCAD. |
| BatDauNhanh.json | Tuỳ chọn "Không hiện lại" của bảng Bắt đầu nhanh. |

> **Mẹo:** Sao lưu thư mục này khi chuyển máy để giữ chuẩn layer, mẫu định dạng, danh mục địa danh và văn bản pháp lý của bạn.

## 3. Giao diện tổng quan

### 3.1. Bố cục cửa sổ chính

| Vùng | Nội dung |
|---|---|
| Menu (trên cùng) | Bố cục kiểu ETABS: Tệp, Sửa, Xem, Khai báo, Vẽ, Chọn, Gán, Phân tích, Hiển thị, Thiết kế, Triển khai hồ sơ, Tuỳ chọn, Trợ giúp – xem *mục 3.2 (Menu chính)*. |
| Thanh công cụ | Các lệnh hay dùng: Mới, Mở, Lưu, nhập bản vẽ, tạo khung, ▶ Phân tích, thiết kế, Xuất tất cả. |
| Cột trái – các tab nhập liệu | Công trình, Địa chất, Tiết diện, Tải trọng, Kết quả, Thiết kế. |
| Bên phải – khung nhìn | Ô **Tầng đang xem**, nút **Vừa màn hình**, tuỳ chọn **Tên cấu kiện**, **Biểu đồ M3 theo**; các tab Mặt bằng, 3D, Biểu đồ nội lực, Mô hình móng, Dao động. |
| Dưới khung nhìn | Ô thông tin cấu kiện đang chọn (trái) và biểu đồ nội lực của thanh đang chọn (phải). |
| Thanh trạng thái (dưới cùng) | Thông báo, gợi ý của công cụ đang dùng, thanh tiến trình khi tính, đường dẫn file dự án. |

Tiêu đề cửa sổ ghi tên công trình, tên file và dấu * khi có thay đổi chưa lưu. Có thể kéo vách ngăn giữa cột trái và khung nhìn để đổi bề rộng.

### 3.2. Menu chính

Menu sắp xếp theo trình tự làm việc như ETABS: **Tệp** (dự án, nhập / xuất mô hình) → **Sửa** → **Xem** → **Khai báo** (vật liệu, tiết diện, tải, tổ hợp, khối lượng, phổ) → **Vẽ** (khung, dầm, sàn, cầu thang, tường) → **Chọn** → **Gán** (tải, liên kết, giải phóng, tiết diện, hệ số độ cứng) → **Phân tích** → **Hiển thị** (biến dạng, biểu đồ, dao động, phản lực, bảng kết quả) → **Thiết kế** → **Triển khai hồ sơ** → **Tuỳ chọn** → **Trợ giúp**.

Bảng dưới liệt kê các mục menu được hướng dẫn nhắc tới (tự lập từ bảng đường dẫn menu của chương trình).

| Menu | Mục | Xem mục |
|---|---|---|
| Tệp | Mới | *mục 6.1 (Dự án mới từ lưới trục (tab Công trình))* |
| Tệp | Mở .kcvxt... | *mục 6.15 (Mở, lưu dự án, dự án mẫu)* |
| Tệp | Lưu | *mục 6.15 (Mở, lưu dự án, dự án mẫu)* |
| Tệp | Lưu thành... | *mục 6.15 (Mở, lưu dự án, dự án mẫu)* |
| Tệp | Nhập mặt bằng kết cấu DWG/DXF... | *mục 6.6 (Nhập mặt bằng kết cấu DWG/DXF)* |
| Tệp | Nhập bản vẽ kiến trúc (triển khai kết cấu)... | *mục 6.5 (Từ bản vẽ kiến trúc DWG/DXF (triển khai kết cấu tự động))* |
| Tệp | Nhập từ Revit (JSON)... | *mục 6.13 (Từ Revit (JSON))* |
| Tệp | Nhập mô hình ETABS/SAP (.e2k/.s2k)… | *mục 6.14 (Từ ETABS / SAP2000 (.e2k / .s2k))* |
| Tệp | Nhận dữ liệu từ AutoCAD… | *mục 6.7 (Từ AutoCAD (add-in, lệnh VXT_*))* |
| Tệp | Liên kết bản vẽ AutoCAD (DWG/DXF)… | *mục 6.12 (Liên kết bản vẽ AutoCAD (DWG/DXF) làm nền mặt bằng)* |
| Tệp | Chuyển sang chế độ Triển khai hồ sơ… |  |
| Tệp | Chọn chế độ làm việc khi khởi động… |  |
| Tệp | Xuất mô hình → Mô hình ETABS .e2k... | *mục 11.12 (Bảng tính Excel, nội lực CSV, mô hình ETABS/SAP)* |
| Tệp | Xuất mô hình → Mô hình SAP2000 .s2k... | *mục 11.12 (Bảng tính Excel, nội lực CSV, mô hình ETABS/SAP)* |
| Tệp | Xuất mô hình → Nội lực CSV... | *mục 11.12 (Bảng tính Excel, nội lực CSV, mô hình ETABS/SAP)* |
| Sửa | Hoàn tác | *mục 3.8 (Hoàn tác, lưu và phím tắt)* |
| Sửa | Xoá đối tượng đang chọn | *mục 7.32 (Chọn, xoá, chia thanh)* |
| Sửa | Sao chép / Đối xứng dầm đã chọn… | *mục 7.10 (Công cụ dầm kiểu ETABS)* |
| Sửa | Chia thanh tại nút | *mục 7.32 (Chọn, xoá, chia thanh)* |
| Sửa | Gộp nút trùng | *mục 3.11 (Cây mô hình, menu Xem / Sửa, thanh trạng thái)* |
| Xem | Cây mô hình | *mục 3.11 (Cây mô hình, menu Xem / Sửa, thanh trạng thái)* |
| Xem | Mặt bằng | *mục 3.5 (Khung nhìn: Mặt bằng, 3D, Biểu đồ nội lực, Mô hình móng, Dao động)* |
| Xem | Khung nhìn 3D | *mục 3.5 (Khung nhìn: Mặt bằng, 3D, Biểu đồ nội lực, Mô hình móng, Dao động)* |
| Xem | Mô hình móng | *mục 7.38 (Móng – loại móng và tab Mô hình móng)* |
| Xem | Vừa màn hình | *mục 3.5 (Khung nhìn: Mặt bằng, 3D, Biểu đồ nội lực, Mô hình móng, Dao động)* |
| Xem | Tên cấu kiện | *mục 3.5 (Khung nhìn: Mặt bằng, 3D, Biểu đồ nội lực, Mô hình móng, Dao động)* |
| Khai báo | Vật liệu… | *mục 7.6 (Vật liệu)* |
| Khai báo | Tiết diện dầm/cột… | *mục 7.7 (Tiết diện dầm, cột, sàn)* |
| Khai báo | Loại tường… | *mục 7.31 (Tường – loại tường, vẽ và gán tường)* |
| Khai báo | Cấu tạo sàn… | *mục 7.12 (Sàn và cấu tạo sàn)* |
| Khai báo | Trường hợp tải (Load Patterns)… | *mục 8.1 (Trường hợp tải (tab Tải trọng))* |
| Khai báo | Trường hợp phân tích (Load Cases)… | *mục 7.35 (Khai báo trường hợp tải, trường hợp phân tích, phổ phản ứng, sàn cứng (menu Khai báo))* |
| Khai báo | Tổ hợp tải trọng… | *mục 8.8 (Sửa tổ hợp tải trọng (Load Combinations))* |
| Khai báo | Tự tạo tổ hợp theo TCVN 2737:2023 | *mục 8.7 (Tổ hợp tải trọng)* |
| Khai báo | Nguồn khối lượng & dao động riêng… | *mục 9.2 (Dao động riêng (tab Dao động))* |
| Khai báo | Phổ phản ứng / động đất… | *mục 7.35 (Khai báo trường hợp tải, trường hợp phân tích, phổ phản ứng, sàn cứng (menu Khai báo))* |
| Khai báo | Sàn cứng (diaphragm) theo tầng… | *mục 7.35 (Khai báo trường hợp tải, trường hợp phân tích, phổ phản ứng, sàn cứng (menu Khai báo))* |
| Vẽ | Tạo khung BTCT từ lưới trục | *mục 6.1 (Dự án mới từ lưới trục (tab Công trình))* |
| Vẽ | Tạo khung thép tiền chế... | *mục 6.4 (Khung thép tiền chế)* |
| Vẽ | Dầm (2 điểm) | *mục 7.9 (Vẽ dầm bằng 2 điểm (＋ Dầm))* |
| Vẽ | Dầm cong | *mục 7.11 (Dầm cong)* |
| Vẽ | Dầm nhanh theo lưới | *mục 7.10 (Công cụ dầm kiểu ETABS)* |
| Vẽ | Dầm phụ | *mục 7.10 (Công cụ dầm kiểu ETABS)* |
| Vẽ | Dầm công xôn | *mục 7.10 (Công cụ dầm kiểu ETABS)* |
| Vẽ | Dầm theo toạ độ / trục… | *mục 7.10 (Công cụ dầm kiểu ETABS)* |
| Vẽ | Kéo đầu dầm / kéo dầm |  |
| Vẽ | Dời dầm song song |  |
| Vẽ | Trim dầm (cắt tại dầm giao) |  |
| Vẽ | Extend dầm (kéo dài tới dầm chọn / vuông góc) |  |
| Vẽ | Nối dầm hở vào dầm vuông góc gần nhất |  |
| Vẽ | Pick đối tượng CAD: dầm / sàn / thang / lỗ |  |
| Vẽ | Ô sàn | *mục 7.12 (Sàn và cấu tạo sàn)* |
| Vẽ | Ban công | *mục 7.13 (Ban công công xôn)* |
| Vẽ | Ô cầu thang | *mục 7.14 (Cầu thang)* |
| Vẽ | Giếng trời | *mục 7.15 (Giếng trời, thông tầng, sàn vệ sinh)* |
| Vẽ | Khai báo cầu thang… | *mục 7.14 (Cầu thang)* |
| Vẽ | Tường | *mục 7.31 (Tường – loại tường, vẽ và gán tường)* |
| Vẽ | Tường đã khai báo… | *mục 7.31 (Tường – loại tường, vẽ và gán tường)* |
| Chọn | Chọn từng đối tượng | *mục 7.32 (Chọn, xoá, chia thanh)* |
| Chọn | Chọn nhiều (bấm / kéo khung) | *mục 7.33 (Chọn đối tượng (menu Chọn))* |
| Chọn | Mọi thanh tầng đang xem | *mục 7.33 (Chọn đối tượng (menu Chọn))* |
| Chọn | Theo tiết diện… | *mục 7.33 (Chọn đối tượng (menu Chọn))* |
| Chọn | Bỏ chọn | *mục 7.32 (Chọn, xoá, chia thanh)* |
| Gán | Tải trên thanh (đều / hình thang / tập trung)… | *mục 7.34 (Gán tải trọng, liên kết, giải phóng, tiết diện (menu Gán))* |
| Gán | Tải trên sàn… | *mục 7.34 (Gán tải trọng, liên kết, giải phóng, tiết diện (menu Gán))* |
| Gán | Tải tại nút… | *mục 7.34 (Gán tải trọng, liên kết, giải phóng, tiết diện (menu Gán))* |
| Gán | Liên kết nút (ngàm / khớp / tự do)… | *mục 7.34 (Gán tải trọng, liên kết, giải phóng, tiết diện (menu Gán))* |
| Gán | Giải phóng đầu thanh… | *mục 7.34 (Gán tải trọng, liên kết, giải phóng, tiết diện (menu Gán))* |
| Gán | Tiết diện thanh… | *mục 7.34 (Gán tải trọng, liên kết, giải phóng, tiết diện (menu Gán))* |
| Gán | Hệ số độ cứng thanh… | *mục 7.34 (Gán tải trọng, liên kết, giải phóng, tiết diện (menu Gán))* |
| Gán | Điểm chèn / dời ngang… | *mục 7.10 (Công cụ dầm kiểu ETABS)* |
| Gán | Sàn cứng tầng đang xem… | *mục 7.34 (Gán tải trọng, liên kết, giải phóng, tiết diện (menu Gán))* |
| Gán | Gán tường lên dầm | *mục 7.31 (Tường – loại tường, vẽ và gán tường)* |
| Gán | Gán cấu tạo sàn | *mục 7.12 (Sàn và cấu tạo sàn)* |
| Gán | Sàn vệ sinh | *mục 7.15 (Giếng trời, thông tầng, sàn vệ sinh)* |
| Phân tích | Kiểm tra mô hình… | *mục 9.10 (Kiểm tra mô hình (Check Model))* |
| Phân tích | Chạy phân tích | *mục 9.1 (Chạy phân tích)* |
| Phân tích | Nhập kết quả nội lực (ETABS/SAP/SAFE/khác)… | *mục 9.7 (Nhập kết quả ETABS / SAP2000 / SAFE / phần mềm khác)* |
| Phân tích | So sánh với ETABS/SAP/SAFE… | *mục 9.8 (So sánh với ETABS / SAP2000 / SAFE)* |
| Hiển thị | Hình biến dạng… | *mục 9.11 (Hình biến dạng (Deformed Shape))* |
| Hiển thị | Biểu đồ nội lực | *mục 9.5 (Biểu đồ nội lực)* |
| Hiển thị | Dạng dao động | *mục 9.2 (Dao động riêng (tab Dao động))* |
| Hiển thị | Phản lực gối… | *mục 9.9 (Bảng kết quả (Show Tables))* |
| Hiển thị | Bảng kết quả… | *mục 9.9 (Bảng kết quả (Show Tables))* |
| Hiển thị | Hiển thị tải gán | *mục 7.34 (Gán tải trọng, liên kết, giải phóng, tiết diện (menu Gán))* |
| Thiết kế | Thiết kế BTCT | *mục 10.1 (Thiết kế BTCT – quy trình chung)* |
| Thiết kế | Kiểm tra thép | *mục 10.9 (Kết cấu thép tiền chế và tab Thép)* |
| Thiết kế | Tự tăng tiết diện đến khi đạt | *mục 10.12 (Tự tăng tiết diện đến khi đạt)* |
| Thiết kế | Thiết kế móng… | *mục 7.38 (Móng – loại móng và tab Mô hình móng)* |
| Thiết kế | Chọn bố trí thép… | *mục 10.10 (Chọn bố trí thép)* |
| Thiết kế | Tuỳ chọn chọn thép & gia công… | *mục 10.11 (Tuỳ chọn chọn thép và gia công)* |
| Triển khai hồ sơ | Bản vẽ DXF... | *mục 11.1 (Bộ bản vẽ DXF KC-01 … KC-08)* |
| Triển khai hồ sơ | Hiệu chỉnh bản vẽ… | *mục 11.8 (Hiệu chỉnh bản vẽ sau khi xuất)* |
| Triển khai hồ sơ | Nạp lại bản vẽ đã chỉnh sửa (DXF/DWG)… | *mục 11.8 (Hiệu chỉnh bản vẽ sau khi xuất)* |
| Triển khai hồ sơ | Xuất shop drawing thép… | *mục 11.9 (Shop drawing thép (gia công, nối thép, vùng cấm nối))* |
| Triển khai hồ sơ | Dàn trang & in bản vẽ… | *mục 11.6 (Dàn trang và in bản vẽ)* |
| Triển khai hồ sơ | Thuyết minh Word... | *mục 11.11 (Thuyết minh tính toán (Word))* |
| Triển khai hồ sơ | Lập thuyết minh tự động… | *mục 11.11 (Thuyết minh tính toán (Word))* |
| Triển khai hồ sơ | Bảng tính Excel... | *mục 11.12 (Bảng tính Excel, nội lực CSV, mô hình ETABS/SAP)* |
| Triển khai hồ sơ | Bảng khối lượng vật tư… | *mục 11.10 (Bảng khối lượng vật tư)* |
| Triển khai hồ sơ | Xuất tất cả vào thư mục... | *mục 11.13 (Xuất tất cả vào thư mục)* |
| Tuỳ chọn | Chuẩn layer CAD… | *mục 11.3 (Chuẩn layer CAD)* |
| Tuỳ chọn | Thiết lập bản vẽ (tỷ lệ, cỡ chữ)… | *mục 11.2 (Thiết lập bản vẽ (tỷ lệ, cỡ chữ, kiểu kích thước))* |
| Tuỳ chọn | Mẫu định dạng (template)… | *mục 6.3 (Mẫu định dạng (template))* |
| Tuỳ chọn | Danh mục địa danh (QCVN 02:2022)… | *mục 8.5 (Gió – địa danh, vùng gió)* |
| Trợ giúp | Hướng dẫn sử dụng… | *mục 3.9 (Trợ giúp ngay trên phần mềm)* |
| Trợ giúp | Bắt đầu nhanh… | *mục 5.2 (Bảng Bắt đầu nhanh)* |
| Trợ giúp | Phụ lục tính toán & hướng dẫn… | *mục 11.14 (Phụ lục tính toán)* |
| Trợ giúp | Văn bản pháp lý & tiêu chuẩn áp dụng… | *mục 13.1 (Danh mục văn bản)* |
| Trợ giúp | Kiểm chứng phần mềm… | *mục 12.1 (Báo cáo kiểm chứng phần mềm)* |
| Trợ giúp | Giới thiệu | *chương 1 (Giới thiệu)* |

### 3.3. Thanh công cụ

| Nút | Tương đương |
|---|---|
| Mới / Mở / Lưu | **Tệp → Mới** (Ctrl+N), **Tệp → Mở .kcvxt...** (Ctrl+O), **Tệp → Lưu** (Ctrl+S) |
| Cây mô hình | **Xem → Cây mô hình** (Ctrl+E) – hiện / ẩn cây mô hình bên trái |
| Nhập DWG/DXF | **Tệp → Nhập mặt bằng kết cấu DWG/DXF...** |
| Từ bản vẽ KT | **Tệp → Nhập bản vẽ kiến trúc (triển khai kết cấu)...** |
| Nhập Revit | **Tệp → Nhập từ Revit (JSON)...** |
| Tạo khung BTCT | **Vẽ → Tạo khung BTCT từ lưới trục** |
| Khung tiền chế | **Vẽ → Tạo khung thép tiền chế...** |
| Kiểm tra MH | **Phân tích → Kiểm tra mô hình…** |
| ▶ Phân tích | **Phân tích → Chạy phân tích** (F5) |
| Thiết kế BTCT / Kiểm tra thép | **Thiết kế → Thiết kế BTCT**, **Thiết kế → Kiểm tra thép** |
| Bảng kết quả | **Hiển thị → Bảng kết quả…** (Ctrl+T) |
| Xuất tất cả | **Triển khai hồ sơ → Xuất tất cả vào thư mục...** |

### 3.4. Các tab nhập liệu (cột trái)

| Tab | Nội dung | Xem mục |
|---|---|---|
| Công trình | Thông tin công trình; lưới trục và tầng; tiết diện mặc định; tải trọng tiêu chuẩn; địa điểm xây dựng (vùng gió, động đất); gió; động đất; vật liệu; nền đất dưới móng đơn; nút **Tạo mô hình**, **Áp dụng thông số**. | *mục 6.1 (Dự án mới từ lưới trục (tab Công trình))* |
| Địa chất | Hố khoan khảo sát, các lớp đất, hình trụ hố khoan, áp dụng cho thiết kế móng, gợi ý phương án móng. | *mục 7.37 (Địa chất – hố khoan (tab Địa chất))* |
| Tiết diện | Bảng tiết diện thanh (chữ nhật BTCT, chữ I tổ hợp) và tiết diện sàn. | *mục 7.7 (Tiết diện dầm, cột, sàn)* |
| Tải trọng | Trường hợp tải, tổ hợp tải trọng, tải trên thanh, tải trên sàn. | *mục 8.1 (Trường hợp tải (tab Tải trọng))* |
| Kết quả | Nội lực theo trường hợp/tổ hợp và tầng, phản lực chân cột, cảnh báo / thông tin. | *mục 9.4 (Tab Kết quả)* |
| Thiết kế | Các tab con Dầm, Cột, Sàn, Móng, Cầu thang, Thép; cấu kiện không đạt tô đỏ. | *mục 10.1 (Thiết kế BTCT – quy trình chung)* |

### 3.5. Khung nhìn: Mặt bằng, 3D, Biểu đồ nội lực, Mô hình móng, Dao động

- **Mặt bằng**: mặt bằng tầng đang xem, thanh công cụ sửa mô hình và công cụ dầm kiểu ETABS (*mục 3.6 (Thanh công cụ của tab Mặt bằng)*). Con lăn chuột thu phóng; bấm chọn cấu kiện; nút **Vừa màn hình** đưa toàn bộ mặt bằng vào khung.
- **3D**: kéo chuột trái để xoay quanh tâm mô hình, con lăn để thu phóng, bấm (không kéo) để chọn cấu kiện. Tuỳ chọn **Làm mờ phần còn lại** khi đang chọn một cấu kiện.
- **Biểu đồ nội lực**: M3, M2, V2, V3, N, T trên mặt bằng tầng, mặt đứng theo trục hoặc 3D (*mục 9.5 (Biểu đồ nội lực)*).
- **Mô hình móng**: mặt bằng móng, mặt cắt móng đang chọn, các nút thiết kế móng (*mục 7.38 (Móng – loại móng và tab Mô hình móng)*).
- **Dao động**: hoạt hình dạng dao động riêng, chu kỳ, tỷ lệ khối lượng tham gia (*mục 9.2 (Dao động riêng (tab Dao động))*).

Lựa chọn dùng chung cho mọi khung nhìn: cấu kiện chọn ở mặt bằng sáng màu vàng cả trên 3D, biểu đồ và mô hình móng.

### 3.6. Thanh công cụ của tab Mặt bằng

Chọn tầng ở ô **Tầng đang xem** trước khi sửa. Mỗi nút là một chế độ; thanh trạng thái luôn ghi cách dùng chế độ đang bật (kèm "F1: hướng dẫn").

| Nút | Thao tác | Xem mục |
|---|---|---|
| Chọn | Bấm chọn cột, dầm, lỗ sàn, ô sàn (Esc quay về chế độ này). | *mục 7.32 (Chọn, xoá, chia thanh)* |
| ＋ Ô sàn | Bấm vào ô kín bởi dầm để thêm ô sàn theo ô **Công năng**. | *mục 7.12 (Sàn và cấu tạo sàn)* |
| ＋ Ban công | Bấm vào dầm biên để thêm ban công công xôn (ô **Vươn (m)**, **Lan can (kN/m)**). | *mục 7.13 (Ban công công xôn)* |
| ＋ Cầu thang | Bấm vào ô để biến thành ô cầu thang; **Khai báo cầu thang…** để khai báo chi tiết. | *mục 7.14 (Cầu thang)* |
| ＋ Giếng trời / Sàn vệ sinh | Bấm vào ô để thành giếng trời, thông tầng / sàn vệ sinh. | *mục 7.15 (Giếng trời, thông tầng, sàn vệ sinh)* |
| ＋ Dầm / ＋ Dầm cong | Vẽ dầm bằng 2 điểm / dầm cong bằng 3 điểm. | *mục 7.9 (Vẽ dầm bằng 2 điểm (＋ Dầm))* |
| ＋ Tường / Gán tường | Vẽ tường 2 điểm / bấm dầm để đặt tường suốt chiều dài dầm. | *mục 7.31 (Tường – loại tường, vẽ và gán tường)* |
| Gán cấu tạo | Bấm ô sàn để gán cấu tạo sàn đang chọn ở ô **Cấu tạo sàn**. | *mục 7.12 (Sàn và cấu tạo sàn)* |
| Xoá | Bấm vào tường, dầm, cột (hỏi lại), ô sàn hoặc lỗ sàn để xoá. | *mục 7.32 (Chọn, xoá, chia thanh)* |
| ↶ Hoàn tác | Hoàn tác thao tác sửa mặt bằng (Ctrl+Z). | *mục 3.8 (Hoàn tác, lưu và phím tắt)* |
| Hàng "Dầm (kiểu ETABS)" | Khai báo tiết diện…, Vẽ nhanh theo lưới, Dầm phụ, Công xôn, Dầm theo toạ độ / trục…, Chọn nhiều, Gán tiết diện, Điểm chèn…, Sao chép / Đối xứng…, Phạm vi tầng, Bắt điểm, Gõ toạ độ. | *mục 7.10 (Công cụ dầm kiểu ETABS)* |

### 3.7. Thanh trạng thái và ô thông tin

- Thanh trạng thái báo kết quả lệnh vừa chạy (số nút, thanh, ô sàn, tổ hợp…), gợi ý của chế độ đang bật và cảnh báo không chặn (vd. dự án trích dẫn văn bản đã hết hiệu lực).
- Khi chương trình đang tính, thanh tiến trình chạy và các lệnh khác bị khoá; đóng cửa sổ lúc này sẽ được nhắc chờ.
- Ô thông tin dưới khung nhìn hiện thuộc tính và kết quả thiết kế của cấu kiện đang chọn; ô bên phải vẽ biểu đồ nội lực của thanh theo ô **Biểu đồ M3 theo**.

### 3.8. Hoàn tác, lưu và phím tắt

- **Ctrl+Z** hoàn tác các thao tác sửa mô hình (mặt bằng, công cụ dầm, tiết diện, cầu thang, móng, chọn thép…), tối đa 30 bước.
- **Delete** xoá đối tượng đang chọn trên mặt bằng; **Esc** dừng công cụ đang vẽ và quay về **Chọn**.
- **Ctrl+N / Ctrl+O / Ctrl+S**: mới / mở / lưu; **F5**: chạy phân tích; **F1**: hướng dẫn theo ngữ cảnh; **Shift+F1**: phụ lục tính toán.
- Sửa mô hình sau khi đã phân tích sẽ xoá kết quả cũ (thanh trạng thái báo "Mô hình đã thay đổi – cần chạy lại phân tích").

Bảng đầy đủ: *mục 15.1 (Phím tắt và thao tác chuột)*.

### 3.9. Trợ giúp ngay trên phần mềm

- **F1** ở bất kỳ đâu mở cửa sổ hướng dẫn tại đúng mục của ngữ cảnh: tab trái đang mở, khung nhìn đang dùng, công cụ sửa mặt bằng đang bật (＋ Dầm, Dầm phụ, Công xôn…) hoặc hộp thoại đang mở.
- Nút **?** (Hướng dẫn) ở đầu các hộp thoại chính mở mục hướng dẫn của hộp thoại đó.
- **Trợ giúp → Hướng dẫn sử dụng…**: cây chương bên trái, nội dung bên phải, ô tìm kiếm không dấu (vd. "dam phu", "coc", "smartscreen"), nút **Xuất Word…**.
- **Trợ giúp → Bắt đầu nhanh…**: bảng các bước làm việc, trạng thái từng bước tự nhận biết từ dự án, nút **Làm bước này** và **Xem hướng dẫn** (*mục 5.2 (Bảng Bắt đầu nhanh)*).
- **Trợ giúp → Phụ lục tính toán & hướng dẫn…** (Shift+F1): công thức, hệ số, hệ số an toàn và giải thích của mọi phép tính mà chương trình dùng (*mục 11.14 (Phụ lục tính toán)*).

### 3.10. Giao diện gọn – mở rộng vùng làm việc

- Menu và thanh công cụ nằm chung **một hàng mảnh** (~28 px) ở cả chế độ Thiết kế, Triển khai hồ sơ và cửa sổ Dàn trang in. Nút lệnh hiện dạng **biểu tượng**; rê chuột để xem tên lệnh, phím tắt và giải thích. Lệnh không đủ chỗ tự vào nút **»** ở cuối thanh công cụ.
- Nút **☰** (đầu hàng): ẩn / hiện thanh menu. Khi menu đang ẩn, nhấn **Alt** để hiện tạm thời (như các phần mềm Windows), chọn lệnh xong menu tự ẩn lại. **Ctrl+F1**: ẩn / hiện thanh công cụ (như ribbon Office).
- Chế độ Triển khai hồ sơ: các nút thêm cấu kiện (móng đơn, móng băng, dầm, cột, sàn…) gộp vào một nút **＋ ▾** – bấm để chọn loại.
- Khung bên (**Nhập liệu – kết quả** của chế độ Thiết kế; **Cấu kiện hồ sơ**, **Thông số cấu kiện** của Triển khai hồ sơ; **Các tờ in**, **Thuộc tính tờ** của Dàn trang in) có nút **«** nhỏ ở góc trên: bấm để thu khung thành dải hẹp có chữ dọc; bấm dải để mở lại. Kéo thanh chia để đổi độ rộng – phần mềm nhớ độ rộng và trạng thái thu gọn cho lần mở sau.
- **F11** hoặc nút **⛶** (cuối hàng): **mở rộng vùng làm việc** – ẩn menu, thanh công cụ, thanh trạng thái, các khung bên, cây mô hình và bảng thông tin dưới mặt bằng; chỉ còn khung vẽ / xem trước và một thanh nổi nhỏ góc trên phải (khôi phục, tìm lệnh, vừa màn hình, phân tích / tính toàn bộ). **Esc** hoặc **F11** để khôi phục.
- **Ctrl+Q** (hoặc **Ctrl+Shift+P**, nút **🔍**): **tìm lệnh nhanh** – gõ tên lệnh, không cần dấu (vd. "mat bang", "xuat", "luu"), gõ chữ đầu cũng được ("mbkc" → Mặt bằng kết cấu); ↑ ↓ chọn, **Enter** chạy, **Esc** đóng. Danh sách gồm mọi mục menu và nút công cụ, kể cả khi menu đang ẩn.
- Nút **⋯** (cuối hàng): bật / tắt menu, thanh công cụ, từng khung bên; **Giao diện gọn** (mặc định – nút biểu tượng, chữ khung 12, thanh trạng thái 1 dòng) hoặc **Giao diện tiêu chuẩn** (biểu tượng kèm chữ) – áp dụng khi mở lại cửa sổ; **Khôi phục bố cục mặc định**.
- **Chọn / bỏ chọn tất cả layer**: mọi danh sách layer có ô tích (bảng layer của **Link CAD**, layer xuất **In** và quy tắc **Dùng** trong Chuẩn layer CAD, **Layer trục** và **Hiển thị nét** khi mở mặt bằng CAD, thẻ trong **Hiển thị / Đồ hoạ**) có hàng **☐ Tất cả (n/N) · ☑ Chọn hết · ☐ Bỏ hết · ⇄ Đảo**. Ô **Tất cả** ba trạng thái: tích hết / không ô nào / một phần (■); bấm khi đang tích hết → bỏ hết, còn lại → tích hết. **Ctrl+A** trong danh sách ô tích = bấm ô Tất cả. Chỉ áp cho các dòng đang hiện (sau lọc); cả lô được áp và vẽ lại **một lần** nên nhanh cả với hàng trăm layer.
- Bố cục chỉ lưu trên máy này, trong file %AppData%\VietConst\ui.json (độ rộng khung, khung thu gọn, menu / thanh công cụ ẩn, mật độ giao diện) – không gửi đi đâu, không chứa dữ liệu dự án. Xoá file để trở về mặc định.

### 3.11. Cây mô hình, menu Xem / Sửa, thanh trạng thái

- **Xem → Cây mô hình** (**Ctrl+E**, nút **Cây mô hình**): khung neo bên trái (như Model Explorer của ETABS) gồm **Thông tin dự án**, **Lưới trục**, **Tầng**, **Vật liệu**, **Tiết diện**, **Tải trọng** (trường hợp tải, trường hợp phân tích, tổ hợp), **Đối tượng** (cột, dầm, sàn, tường, móng theo tầng – bấm để chọn và phóng tới), **Kết quả** (chu kỳ dao động, phản lực đáy) khi đã phân tích.
- Bấm đúp một nút nhánh của cây để mở hộp thoại khai báo tương ứng. Chọn một trường hợp tải trên cây để lọc **Hiển thị → Hiển thị tải gán**.
- Menu **Xem**: **Xem → Mặt bằng**, **Xem → Khung nhìn 3D**, **Xem → Mô hình móng**, **Xem → Vừa màn hình**, **Xem → Tên cấu kiện**. Menu **Sửa**: **Sửa → Hoàn tác**, **Sửa → Xoá đối tượng đang chọn**, **Sửa → Sao chép / Đối xứng dầm đã chọn…**, **Sửa → Chia thanh tại nút**, **Sửa → Gộp nút trùng** (gộp nút cách nhau < 5 mm).
- Thanh trạng thái bên phải: hệ đơn vị (kN, m, °C – tiết diện hiển thị mm, ứng suất MPa), tầng đang xem, toạ độ con trỏ trên mặt bằng (m), hệ trục tổng thể (X sang phải, Y lên trên, Z hướng lên).

Kiểm tra mô hình: *mục 9.10 (Kiểm tra mô hình (Check Model))*; hình biến dạng: *mục 9.11 (Hình biến dạng (Deformed Shape))*.

## 4. Hai chế độ làm việc – Triển khai hồ sơ

Khi khởi động, màn hình **chọn chế độ làm việc** hiện 2 lựa chọn (phím 1 / 2):

**1 – Thiết kế – chạy nội lực**: giao diện mô hình tính như ETABS (lưới trục, tầng, tiết diện, tải trọng, phân tích khung 3D, thiết kế theo nội lực) – các chương sau của hướng dẫn.

**2 – Triển khai hồ sơ**: không cần mô hình – chỉ nhập kích thước dầm, sàn, cột, chọn số thanh, Ø, khoảng cách thép; thiết kế móng đơn, móng băng, móng cọc, móng bè theo tải chân cột; tính bê tông dự ứng lực, bể nước; xuất bản vẽ, thống kê thép, khối lượng, thuyết minh.

- Đánh dấu **Luôn mở chế độ đã chọn khi khởi động** để bỏ qua màn hình này; đổi lại: **Tệp → Chọn chế độ làm việc khi khởi động…**.
- Đang ở chế độ mô hình: **Tệp → Chuyển sang chế độ Triển khai hồ sơ…** (cửa sổ mô hình vẫn mở để lấy nội lực). Ở chế độ triển khai: Tệp → **Chuyển sang chế độ Thiết kế – chạy nội lực**.
- Mở file **.kcvxt** (dự án mô hình) → chế độ thiết kế; file **.kcvxth** (hồ sơ triển khai) → chế độ triển khai.

### 4.1. Cửa sổ Triển khai hồ sơ

- Bên trái: **danh sách cấu kiện** theo nhóm (Dầm, Cột, Sàn, Móng đơn, Móng băng, Móng cọc, Móng bè, Dự ứng lực, Bể nước); ✓ đạt, ✗ đỏ = không đạt. Nút **Nhân bản**, **Xoá**, ▲▼.
- Thanh công cụ: **＋ Dầm**, **＋ Cột**, **＋ Sàn**, **＋ Móng đơn**, **＋ Móng băng**, **＋ Móng cọc**, **＋ Móng bè**, **＋ DƯL**, **＋ Bể nước**; **Tính toàn bộ (F5)**, **Xuất tất cả…**.
- Giữa: **bảng nhập** theo nhóm (Tiết diện, Thép dọc, Thép đai, Nội lực, Nền…). Ô kích thước ghi đơn vị (mm, m, kN, kN.m); nhập **0** ở ô thép / kích thước = chương trình **tự chọn**. Kết quả tính lại ngay khi sửa.
- Phải: tab **Kết quả kiểm tra** (bảng yêu cầu – khả năng – tỷ số, dòng đỏ = không đạt, thông báo LỖI kèm đề xuất), **Thuyết minh** (công thức, thay số, thống kê thép 1 cấu kiện), **Bản vẽ (xem trước)** (khung A1, phóng to / thu nhỏ, **Xuất DXF cấu kiện này…**), **Khối lượng hồ sơ**.
- Tuỳ chọn → **Vật liệu, lớp bảo vệ…** (cấp bê tông, thép dọc / thép đai, lớp bảo vệ dầm, cột, sàn, móng, bể; bê tông lót; chiều dài cây thép) và **Thông tin công trình…** (in lên khung tên).
- Tệp → **Mở hồ sơ mẫu**: đủ 9 loại cấu kiện để tham khảo. Lưu hồ sơ: Ctrl+S (file .kcvxth).

### 4.2. Dầm, cột, sàn – nhập kích thước, chọn thép

- **Dầm**: b × h, **các nhịp tim – tim** (vd. "4,2; 3,6" hoặc "3*4,2"), bề rộng gối, cao độ đỉnh; thép dưới / trên chạy suốt (≥ 2 thanh), tăng cường dưới mỗi nhịp, mũ gối; đai Ø, bước vùng gối / giữa nhịp, số nhánh. Nhập M nhịp, M gối, Q (tuỳ chọn) → kiểm tra M ≤ Mu, Q ≤ Qb + Qsw, dải nén 0,3Rb·b·h0 (TCVN 5574:2018 mục 8.1). Luôn kiểm tra cấu tạo: μ ≥ 0,1 %, bước đai, khe hở thép 1 lớp. Mỗi nhóm thép nhập trên **một dòng**: Ø × số thanh × hàng; thép chạy suốt chọn **bẻ mỏ neo vào gối** hoặc **neo thẳng** (cảnh báo khi đoạn neo thẳng < 10d); tối đa 3 nhóm tăng cường dưới (2)(3)(4) cắt tại 0,1·L0 / L0/7 / L0/8 từ mép gối hoặc kéo vào gối, có / không bẻ mỏ; tối đa 3 nhóm mũ gối vươn L0/3 (gối giữa) – L0/4 (gối biên) / L0/4 / L0/5 / qua hết nhịp ngắn; nhóm đặt hàng 2 (hoặc hàng 1 không đủ khe hở) → h0 tính theo trọng tâm thép, Mu kể đủ các nhóm.
- **Cột**: b × h, chiều cao các tầng, cao độ chân, chiều cao dầm tại nút; Ø, số thanh mỗi cạnh, đai đầu / giữa cột. Nhập N, Mx, My tầng dưới → kiểm tra nén lệch tâm xiên (Bresler) có kể độ lệch tâm ngẫu nhiên; vị trí nối thép: **tự gợi ý** (giữa chiều cao thông thuỷ, ngoài vùng nguy hiểm lcr = max(hc; H/6; 450 mm), vùng gợi ý ghi trong thuyết minh), **nối trên mặt sàn** (nhà thấp tầng) hoặc **ống nối ren**; tuỳ chọn so le 50 % số thanh (≥ 1,3·ll) và **nhấn cổ chai 1:6** đầu thanh tầng dưới (thể hiện trên mặt đứng cột và bảng thống kê).
- **Sàn**: Lx, Ly, t, số cạnh ngàm; tĩnh tải hoàn thiện, hoạt tải (tiêu chuẩn) → q tính toán, phân tải Marcus, mô men dải 1 m; thép lớp dưới 2 phương, mũ gối (0 = tự chọn Ø6…Ø12, bước 100…200).
- **Vị trí dầm**: Dầm tầng / **Dầm móng (giằng móng)** / Dầm mái. Dầm móng: lớp bảo vệ ≥ 35 mm, **bê tông lót** (vươn mỗi bên, mặc định 100 mm) và chọn **Dầm móng sát ranh đất** (trái / phải) – phía sát ranh bê tông lót và bê tông dầm không vươn sang đất nhà bên, thành dầm xây gạch làm khuôn (có trong khối lượng).
- **Cánh sàn (mặt cắt dầm)**: Chữ T – sàn 2 bên / Chữ L – sàn bên trái hoặc phải / Không có sàn; chiều dày sàn, **hạ cốt sàn từng bên** (vd. −50 sàn vệ sinh), thép sàn thể hiện. Mặt cắt dầm vẽ cánh sàn, bậc sàn hạ cốt và thép sàn qua dầm như Kết cấu Soft / XSCad.
- **Cột – vị trí**: Chân cột từ móng (thép chờ chôn trong móng, bẻ chân tựa lưới thép đáy) hoặc trên dầm / sàn; **thép chờ nhô khỏi mặt móng (cổ cột) ≥ max(30d; ll)** – để 0 chương trình tự lấy; **Đỉnh cột**: Tầng mái – khoá đầu cột **uốn mỏ** vào dầm mái (đoạn uốn ≥ max(12d; lan − (hd − c))) hoặc chờ nối lên tầng trên (nhô ll).
- Bản vẽ dùng chung bộ vẽ của chế độ mô hình: mặt đứng dầm – chuỗi vùng đai, nhãn thép, mặt cắt 1-1 / 2-2; mặt đứng cột, mặt cắt; mặt bằng thép sàn + mặt cắt. Số hiệu thép theo quy tắc Kết cấu Soft.

### 4.3. Mặt bằng sàn – hạ cốt, nhấn thép, khoét lỗ, thép tự động / thủ công

- ＋ Mặt bằng sàn: khai báo **nhịp trục X, Y** (tim dầm), chiều dày, tải. Bảng **ô sàn khác mặc định**: cột / hàng của ô, **hạ cốt Δ** (vd. −50 vệ sinh, ban công), chiều dày riêng, công năng (Lỗ thông tầng = bỏ ô).
- **Cách bố trí**: Tự động theo diện tích – chương trình tính từng ô (cạnh liên tục khi chênh cốt ≤ ngưỡng bậc), chọn thép lớp dưới, thép mũ qua dầm; hoặc **Thủ công** – kỹ sư tự khai báo vùng thép (lớp, phương, Ø, bước, toạ độ), chương trình kiểm tra từng ô còn thiếu thép.
- **Nhấn thép**: tại dầm có hai ô chênh cốt |Δ| ≤ ngưỡng nhấn (mặc định 50 mm) thép mũ được **bẻ xiên 1:6** vắt qua dầm; lớn hơn thì cắt thép, neo riêng từng ô vào dầm.
- **Lỗ khoét sàn**: toạ độ góc dưới trái, kích thước; chương trình cắt thép qua lỗ, đặt **thép gia cường mép lỗ**, cảnh báo lỗ lớn cần dầm bo.

### 4.4. Lanh tô cửa, ô văng cửa

- ＋ Lanh tô: chọn **Lanh tô**, **Lanh tô + ô văng liền khối** hoặc **Ô văng** (console từ dầm / lanh tô có sẵn); nhập bề rộng lỗ cửa, chiều dày tường, chiều cao lanh tô (0 = tự chọn), đoạn gối mỗi bên (≥ 200 mm).
- Tải tường phía trên: khi tường cao ≥ Lo/3 và không có sàn tựa trong phạm vi lỗ cửa chỉ lấy tường cao Lo/3 (hiệu ứng vòm); có sàn / dầm tựa thì cộng tải đó.
- Ô văng: chiều vươn, chiều dày gốc / mút, gờ chắn nước; tải sửa chữa phân bố hoặc lực tập trung ở mút; thép chịu lực lớp trên neo vào lanh tô. Kiểm tra uốn, cắt lanh tô, **xoắn** lanh tô do ô văng, **chống lật** khi lanh tô không liền cột / giằng tường.
- Bản vẽ: mặt đứng lanh tô và mặt cắt 1-1 (lanh tô + ô văng, dốc thoát nước, gờ chắn, rãnh nhỏ giọt).
- **Tường 110** (cửa đi các phòng, cửa đi lớn): chương trình tự chọn h ≥ max(Lo/10; 150 mm), Lo > 1,5 m lấy ≥ 200 mm; gối (ngàm) vào tường mỗi bên ≥ 200 mm (≥ 250 mm khi Lo > 1,5 m); kiểm tra **nén cục bộ khối xây** dưới gối (nhập cường độ khối xây R, mặc định 1,3 MPa – gạch đặc M75, vữa M50); cửa rộng > 1,8 m: gợi ý **bổ trụ BTCT 110×220** hai bên ô cửa hoặc dùng dầm BTCT gác lên cột.

### 4.5. Vẽ mặt bằng kết cấu, nhân bản tầng, ánh xạ kiến trúc → lanh tô các tầng

- Tab **Mặt bằng kết cấu**: đặt cột, móng, dầm (2 điểm), sàn, lanh tô, ô văng, cầu thang; **bắt điểm** tâm cột, đầu dầm, giao trục, điểm trên tim dầm, đường trục; giữ **Shift** để vẽ vuông góc; **Sàn trong ô dầm**: bấm vào bên trong ô được bao bởi dầm (hoặc trục); **Di chuyển**: bấm cấu kiện rồi bấm điểm đích; **↶ Hoàn tác** (Ctrl+Z).
- **⧉ Nhân bản tầng…**: sao toàn bộ cấu kiện của mặt bằng đang xem sang các tầng giống nhau về bố trí công năng (số tầng, chiều cao tầng). Chế độ **A – dùng chung cấu kiện**: cùng tên D1, S1…, số lượng tự nhân theo số tầng (thống kê thép, bê tông, ván khuôn nhân đúng); chế độ **B – tạo cấu kiện mới**: D1 → D1-T3… để sửa riêng từng tầng. Cột luôn dùng chung và được thêm một tầng vào "Chiều cao các tầng"; đường cắt mặt cắt sàn được sao theo.
- **Nhập MB kết cấu từ CAD…**: mặt bằng kết cấu vẽ trong AutoCAD (DXF/DWG) – dầm vẽ 2 nét song song (hoặc 1 nét tim) trên layer chứa DAM / BEAM / GIANG, cột là đa tuyến kín trên layer COT / COLUMN, chữ tên cạnh cấu kiện "D1 220x400", "C1 (220x220)", chữ S1 trong ô sàn. Tên đã có trong hồ sơ → gán vị trí; chưa có → tạo cấu kiện mới với tiết diện đọc từ chữ. Trong AutoCAD có thể gõ **VXT_MB_KC** (nút **Gửi mặt bằng kết cấu → Triển khai hồ sơ**), chọn đối tượng – Việt Const tự hỏi nhận vào mặt bằng đang xem.
- **Ánh xạ KT (xref)…**: gắn mặt bằng kiến trúc của tầng (DXF/DWG) làm nền tham chiếu màu xám (không sửa file gốc, tự nạp lại khi file kiến trúc thay đổi; lưu đường dẫn tương đối theo thư mục hồ sơ); **Dời xref…** để trục kiến trúc trùng trục kết cấu; **Ẩn / hiện xref**.
- **Lanh tô theo cửa KT**: chương trình nhận các ô cửa (layer CUA / DOOR / WINDOW hoặc block cửa) và tường chứa cửa (110 / 220), đặt lanh tô trên mỗi ô cửa; các cửa giống nhau (bề rộng, chiều dày tường, loại) gộp thành một cấu kiện LT1, LT2… với số lượng = số ô cửa. **Ô văng trên cửa**: bấm vào ô cửa ở tường ngoài → ô văng OV… liền lanh tô vươn ra phía ngoài.
- Cây thư mục: lanh tô – ô văng của mỗi tầng nằm ở mục **Mặt bằng lanh tô – ô văng «tầng»**; khi xuất hồ sơ mỗi tầng có một tờ **LANH TÔ – Ô VĂNG «TẦNG»** gồm mặt bằng lanh tô của tầng và chi tiết các lanh tô.
- Bấm một cấu kiện trên mặt bằng → cây thư mục chọn đúng cấu kiện đó dưới đúng nút mặt bằng; bấm một cấu kiện trên cây → mặt bằng chuyển đúng tầng và đánh dấu vị trí; bấm đúp → mở chi tiết cấu kiện.

### 4.6. Lấy kích thước từ AutoCAD (lệnh VXT_KT)

1. Trong AutoCAD đã nạp add-in Việt Const: gõ **VXT_KT** (hoặc nút **Lấy kích thước → Triển khai hồ sơ** trên bảng VXT).
2. Chọn loại: **Dam** (chọn trục dầm, các hình chữ nhật cột → nhịp; 2 cạnh dầm → b; nhập h), **Cot** (các hình chữ nhật → b × h, tên lấy theo chữ bên trong), **San** (các đường trục → nhịp X, Y; đa tuyến kín bên trong → lỗ khoét), **Mong** (hình chữ nhật móng + cột bên trong), **MongBang** (trục băng + cột).
3. Việt Const (cửa sổ Triển khai hồ sơ) tự nhận trong vài giây và hỏi: **Có** – áp vào cấu kiện đang chọn (giữ thép, nội lực), **Không** – tạo cấu kiện mới; hoặc bấm **⇩ Từ AutoCAD** (Ctrl+K).

- Đơn vị bản vẽ lấy theo INSUNITS (mm / m); dữ liệu trao đổi qua thư mục %AppData%\KetCauVXT\AcadPick.

### 4.7. Móng đơn, móng băng, móng cọc

- **Móng đơn**: kích thước cột, N, ML, MB tính toán (N tiêu chuẩn = N/1,15 nếu để 0), R, Df. L, B, H = 0 → tự chọn: ptb ≤ R, pmax ≤ 1,2R, không tách đáy; H theo chọc thủng F ≤ Rbt·u·h0 và móng cứng; thép mép cột với áp lực hình thang.
- **Móng băng** (dầm móng chữ T ngược dưới dãy cột): chiều dài băng, bề rộng bản (0 = tự chọn), bản mép, sườn; bảng **cột trên móng băng** (vị trí X, N, M). Tính dầm trên nền đàn hồi Winkler (k = 120·R nếu để 0): áp lực nền, M+ tại cột (thép dưới), M− giữa nhịp (thép trên), Q (đai); bản cánh tính console.
- **Móng cọc**: cột, N, Mx, My; cọc D, [P] nén, [Pk] nhổ, số cọc (0 = tự chọn), khoảng cách (0 = 3D), tim cọc tới mép đài. Kiểm tra Pmax ≤ [P], nhổ, khoảng cách 3D, chọc thủng cột và cọc góc, cắt tiết diện nghiêng, thép mép cột.
- **Sát ranh đất** (móng đơn: phương L / B; đài cọc: phương X / Y; móng băng: cánh 1 bên; móng bè: cạnh sát ranh): bê tông móng và bê tông lót **không vươn sang đất nhà bên** (lót chỉ vươn phía trong), cột lệch về mép móng → móng lệch tâm: có **dầm móng cân bằng** (chương trình ghi mô men N·e cho dầm) hoặc kể N·e vào áp lực đáy móng / lực cọc; chu vi chọc thủng bị cắt ở cạnh ranh; đài cọc giữ tim cọc cách ranh ≥ 500 mm (máy ép). Bản vẽ ghi **RANH ĐẤT**, thành sát ranh xây gạch / tấm khuôn để lại (có trong khối lượng).
- Khối lượng kèm theo: bê tông lót, đào đất hố móng, số cọc, đập đầu cọc.

### 4.8. Móng bè

Khai báo Lx, Ly, chiều dày, Df, R và bảng **cột trên móng bè** (toạ độ X, Y từ góc móng, kích thước, N, Ntc, Mx, My). Phương pháp móng cứng (ACI 336.2R): áp lực đáy p = ΣNtc/A + γ·Df ± M·y/I, chia dải theo hàng cột, mô men dải theo cân bằng tĩnh, thép lớp trên / dưới 2 phương (0 = tự chọn), chọc thủng từng cột (cột giữa, biên, góc).

### 4.9. Bê tông dự ứng lực (căng sau)

Dầm hoặc dải sàn 1 m nhịp đơn giản: tiết diện (chữ nhật / chữ T), tải G2, Q; cáp T12.7 / T15.2, số sợi, độ lệch tâm giữa nhịp và đầu, σcon, ma sát μ, k, tụt neo. Chương trình tính tổn hao (chùng ứng suất, ma sát, tụt neo, co ngót, từ biến; tối thiểu 100 MPa), P0, P∞, tải cân bằng, ứng suất lúc buông neo và khai thác, Mu, độ vồng – độ võng (TCVN 5574:2018 mục 9), vẽ đường cáp parabol + mặt cắt, khối lượng cáp, neo, ống gen.

### 4.10. Bể nước BTCT

Bể ngầm hoặc bể trên mái: kích thước trong, chiều cao nước, chiều dày thành / đáy / nắp; đất (γ, φ, phụ tải, mực nước ngầm). Tính thành bể theo phương pháp dải (Grashof) với áp lực nước / đất, kiểm tra bề rộng vết nứt ≤ 0,2 mm (tự tăng thép nếu cần), đáy, nắp, chống đẩy nổi khi bể rỗng; khối lượng kèm băng cản nước, chống thấm.

### 4.11. Cầu thang bộ – bản thang, bậc gạch / BTCT, zíc zắc, xương cá, thang thép

- **Bản thang thường** (mặc định): chỉ thiết kế kết cấu **bản thang + chiếu nghỉ** (dầm chiếu nghỉ, dầm cốn nếu chọn); **bậc xây gạch** (thông dụng ở Việt Nam) chỉ là tải trọng và khối lượng xây bậc, không có thép.
- Bản vẽ: mặt bằng thang, **mặt bằng bố trí thép bản thang** (thanh đại diện có móc, chữ Ø..a.. + vòng số hiệu, đường phạm vi rải; dầm chiếu nghỉ / dầm chiếu tới nét đứt; mũi tên LÊN; vạch cắt 1-1, A-A), mặt cắt dọc vế 1-1, **mặt cắt ngang bản thang A-A**, chi tiết chiếu nghỉ, dầm – số hiệu khớp bảng thống kê thép.
- **Bậc: Đổ BTCT liền khối**: cộng bê tông bậc tam giác ½·b·h·B, trọng lượng bậc; mỗi bậc 1 thanh Ø8 (hoặc Ø6) góc mũi bậc + thép chữ L Ø6a200 ôm bậc; chiều dày tính toán vẫn là bản thang (không kể bậc vào h0); có hình **chi tiết bậc BTCT**.
- **Kết cấu thang → Thang zíc zắc**: bản gấp khúc dày t (không có bản nghiêng): tải bản thân 25·t·(b + h)/b, bản tựa đơn trên hình chiếu bằng M = q·L²/8, thép chịu lực đi theo đường gấp khúc, **thép góc chữ L tại góc lõm** (không uốn thép vòng qua góc lõm), kiểm tra cắt và độ mảnh L/t ≤ 20.
- **Thang xương cá**: dầm cốn giữa b × h đỡ bậc công xôn hai bên / một bên: bậc M = q·l²/2 + P·l (P = 1 kN ở mút), thép lớp trên neo qua dầm cốn; dầm cốn chịu uốn, cắt và **xoắn** (bậc một bên / hoạt tải lệch) – đai kín, thép dọc chịu xoắn theo TCVN 5574:2018.
- **Thang thép hình**: 2 cốn U160…U240, I, H hoặc hộp; mác CCT34 / CCT38 / Q235 / SS400; kiểm tra σ = M/W ≤ f·γc, τ, độ võng ≤ L/250; thép góc đỡ bậc L50×5 / L63×6 / L75×6; bản mã + 2 bu lông neo M16; khối lượng thép (kg), sơn chống gỉ (m²); bản vẽ mặt đứng cốn, mặt cắt, chi tiết chân cốn.

### 4.12. Hố pít – vách thang máy

- ＋ **Hố pít – vách thang máy**: kích thước giếng thông thuỷ, chiều dày vách, chiều sâu hố pít (thường 1,2…1,5 m), đáy hố, số tầng, chiều cao tầng, OH, lỗ cửa tầng; đất (γ, φ, phụ tải), mực nước ngầm, sức chịu tải nền; tải cabin, đối trọng.
- Thành hố pít tính như thành bể ngầm (áp lực đất chủ động + nước ngầm, phương pháp dải, vết nứt ≤ 0,2 mm); đáy hố chịu đẩy nổi / phản lực nền, chọc thủng dưới giảm chấn R = 4·(P + Q)·g; chống đẩy nổi khi thi công G ≥ 1,1·U.
- Vách giếng BTCT: thép 2 lớp mỗi phương, hàm lượng μ ≥ 0,25 %; lỗ cửa tầng có thép gia cường mép lỗ 2Ø14, thép xiên góc 2Ø12, dầm lanh tô cửa; chọn **vách đặt trên dầm móng** (mặc định) hoặc trên hố pít.
- Bản vẽ: mặt bằng giếng, mặt cắt 1-1 qua hố pít, chi tiết thép lỗ cửa; khối lượng bê tông, ván khuôn, thép, băng cản nước, chống thấm, đào đất.

### 4.13. Trích chi tiết, mặt cắt dầm, shop drawing, điểm cắt thép

- Tab Mặt bằng kết cấu → công cụ **Trích chi tiết**: bấm 2 góc khoanh vùng cần thể hiện rõ → hình **CHI TIẾT A** (tl 1/25) vẽ lại mặt bằng tại vùng đó (dầm, cột, ô sàn, thép sàn, kích thước) với chữ đúng cỡ in; trên mặt bằng có khung nét đứt và ký hiệu **CT-A**. Chuột phải lên khung: đổi tên, tỷ lệ 1/10…1/50, ghi chú, xoá.
- Mỗi nhịp dầm đều có mặt cắt tại **hai gối và giữa nhịp**; mặt cắt giống nhau dùng chung số hiệu. Trên mặt bằng kết cấu tầng có vạch cắt + số hiệu tại các vị trí đó (tắt / bật: chuột phải lên khung chi tiết → "Thể hiện vị trí mặt cắt dầm trên mặt bằng").
- **Shop drawing thép dầm, sàn**: tích "Xuất shop drawing" ở từng dầm / sàn (hoặc Vật liệu… → cho mọi dầm, sàn): từng số hiệu thép vẽ tách riêng đúng hình uốn, kích thước từng đoạn, vị trí nối; bảng gia công – cắt thép: chiều dài cắt (trừ 2d mỗi góc uốn 90°, đai −12d), số cây 11,7 m, hao hụt %. Thép chạy suốt dài hơn cây thép: tự chọn vị trí nối trong vùng cho phép, một nửa số thanh nối so le ≥ 1,3·ll. Excel có thêm trang "Gia công cắt thép".
- **Điểm cắt / nối thép tuỳ chỉnh** (dầm): vươn mũ gối từng gối, khoảng cắt tăng cường dưới từng nhịp, vị trí nối thép dưới / trên. Chương trình **cảnh báo** khi: mũ gối vươn < L0/4 (gối biên) / L0/3 (gối giữa) hoặc < lan; tăng cường dưới cắt xa mép gối hơn L0/6; nối thép dưới trong 1/3 giữa nhịp; nối thép trên trong L0/4 từ mép gối hoặc trên gối; kháng chấn: nối trong 2h từ mép gối. Điểm sai được đánh dấu X đỏ + "!" trên mặt đứng dầm.
- **Ghi chú cấu tạo thép sàn** (dưới mặt cắt sàn và trong thuyết minh): nguyên tắc theo miền chịu kéo – thép lớp dưới phương NGẮN đặt trước (sát đáy), phương dài đặt trên; thép mũ phương ngắn đặt sau cùng (trên cùng); mũ vươn ≥ L0/4 cạnh ngắn; thứ tự đặt thép 1)…7) (lớp dưới ngắn → lớp dưới dài → con kê → chân chó → mũ phương dài → mũ phương ngắn → buộc, kiểm tra). Dầm có thứ tự lắp dựng: thép dọc dưới → đai → thép dọc trên → mũ gối.

### 4.14. Xuất hồ sơ

- Xuất hồ sơ → **Bản vẽ DXF – gộp 1 file** (các khung A1 đặt cạnh nhau + bảng thống kê cốt thép) hoặc **tách từng cấu kiện** (KC-01_Dầm_D1.dxf … + KC-xx_ThongKeThep.dxf).
- **Bảng khối lượng Excel**: tổng hợp bê tông, bê tông lót, ván khuôn, cốt thép theo nhóm Ø ≤ 10 / 10–18 / > 18, cáp DƯL, công tác khác; trang theo cấu kiện (hàm lượng kg/m³); thống kê cốt thép đầy đủ.
- **Thuyết minh tính toán Word**: căn cứ pháp lý – tiêu chuẩn, vật liệu, từng cấu kiện (công thức thay số, bảng kiểm tra, thông báo), tổng hợp khối lượng.
- **Xuất tất cả vào thư mục…**: cả bản vẽ gộp, bản vẽ tách, Excel và Word.

> **Lưu ý:** Ở chế độ triển khai, nội lực do kỹ sư nhập (từ tính tay, chế độ mô hình hoặc phần mềm khác) – chương trình không kiểm tra nội lực đó có đúng với sơ đồ kết cấu hay không.

### 4.15. Dàn trang in, xuất PDF toàn bộ, in trực tiếp

- Thanh công cụ → **🖨 Dàn trang in** mở cửa sổ dàn trang: danh sách tờ bên trái, tờ đang chọn ở giữa (khung, khung tên, các hình đúng tỷ lệ in), hình chưa đặt và thuộc tính tờ bên phải.
- **⟳ Tự dàn trang**: xếp mọi hình theo thứ tự hồ sơ – mặt bằng kết cấu móng → chi tiết móng → mặt bằng định vị cột → chi tiết cột → mặt bằng tầng, chi tiết dầm → sàn → lanh tô, ô văng → cầu thang → bể nước, hố pít, kết cấu khác → thống kê thép, vật liệu. Mỗi mục bắt đầu tờ mới; hình xếp trái → phải, trên → dưới, cách nhau ≥ 10 mm, không đè khung tên; tự chọn khổ nhỏ nhất (A3 → A2 → A1 → A0) vừa các hình của mục (bỏ chọn để dùng khổ mặc định).
- Tờ đầu là **danh mục bản vẽ** (số hiệu, tên, khổ, tỷ lệ); tờ đánh số KC-01, KC-02… Mỗi hình in đúng tỷ lệ riêng (mặt bằng 1/100, chi tiết 1/50, mặt cắt 1/25), khung tên ghi các tỷ lệ của tờ.
- **Dàn trang tay**: kéo hình để dời; chọn hình rồi **Chuyển** sang tờ khác hoặc **Gỡ hình khỏi tờ**; đặt hình chưa đặt vào tờ đang chọn; thêm / xoá / đổi thứ tự tờ, đổi khổ giấy, khổ đứng, tên tờ, ngày ghi khung tên. Tờ đã chỉnh tay **tự khoá** – lần tự dàn trang sau giữ nguyên tờ khoá. Khung đỏ báo hình chồng hình khác, đè khung tên hoặc ra ngoài khung. Bố cục lưu cùng hồ sơ .kcvxth.
- **Xuất PDF toàn bộ…**: một file PDF dạng nét, mỗi tờ một trang đúng khổ giấy, nét theo layer (màu, bề dày in), chữ tiếng Việt bằng phông Arial / Times New Roman của máy (nhúng vào PDF); tuỳ chọn **đen trắng**.
- **In…**: chọn máy in đã cài driver trên máy, số bản, khoảng trang; tờ tự thu vừa khổ giấy máy in (xoay nếu hợp hơn) hoặc chọn **In 1:1**. **Xuất DWG/DXF…**: mỗi tờ một file AutoCAD theo định dạng xuất đang chọn.

> **Lưu ý:** In và xuất PDF chỉ dùng máy in, phông chữ có sẵn trên máy – không kết nối mạng, không gửi dữ liệu đi đâu.

### 4.16. Chia dầm nhiều đoạn, xuất tất cả theo bộ dàn trang

- Dầm nhiều nhịp có mặt đứng dài hơn vùng vẽ của khổ đang dùng (ở tỷ lệ chuẩn 1/50) được **chia đoạn tại gối**: mỗi đoạn gồm các nhịp liên tiếp, kéo qua gối đầu / cuối tới đường cắt ngắt (hai đoạn kề nhau cùng thể hiện gối chung), ghi "XEM TIẾP ĐOẠN 2/3" / "TIẾP THEO ĐOẠN 1/3"; tiêu đề "D1 (SL=2; L=20300) – ĐOẠN 1/3 (trục 1–4)".
- Số hiệu thép, vùng đai, vị trí mặt cắt giữ như dầm nguyên; thanh chạy qua nhiều đoạn ghi nhãn ở mỗi đoạn, đoạn sau ghi "(tiếp)" và không vẽ móc ở đầu bị cắt ngắt. Mặt cắt là hình riêng, vẽ một lần, xếp thành hàng ngay dưới mặt đứng.
- Cửa sổ **Dàn trang in** → mục **CHIA DẦM NHIỀU ĐOẠN**: Tự động | Không chia | Theo số nhịp/đoạn (vd. 2) | Tại các trục (vd. 3, 5 – số thứ tự gối hoặc tên trục); tuỳ chọn **cho thu tới 1/75 trước khi chia**; **Áp dụng cho dầm đang chọn** hoặc **cho mọi dầm**. Không chia: mặt đứng dài tự hạ tỷ lệ (khổ chốt) hoặc nâng khổ (khổ tự động).
- **Xuất tất cả…** dùng chung bộ dàn trang: nếu đã dàn trang tay thì xuất đúng bộ tờ đó (hình mới được xếp thêm vào cuối), chưa dàn thì tự dàn – khổ, tỷ lệ tự động, cấu kiện cùng nhóm mặt bằng gộp tờ, tờ danh mục ở đầu. File tách rời đặt tên theo số hiệu + nội dung (KC-02_MB_KetCauMong, KC-05_Dầm_D1-D3…).

### 4.17. Hoàn thiện bản vẽ: cửa trên mặt bằng, lanh tô từ mô hình, cột lệch tâm, tham chiếu tờ

- Mô hình có **cửa gắn tường** (giao diện kiểu Revit): mặt bằng kết cấu các tầng vẽ thêm tường (cắt tại ô cửa ở cao 1,2 m), cánh và cung mở cửa đi, 3 nét cửa sổ, ô thoáng nét đứt, thẻ D1 / S1; bộ bản vẽ có tờ **KC-09 BẢNG THỐNG KÊ CỬA** (kiểu, kích thước, số lượng theo tầng) và bảng khối lượng (Excel / Word) có bảng **Thống kê cửa**. Diện tích tường xây đã trừ diện tích cửa thật.
- Triển khai hồ sơ → tab **Mặt bằng kết cấu** → nút **Lanh tô từ cửa mô hình**: mọi cửa gắn tường của mô hình đang mở (cửa sổ chế độ Thiết kế) thành lanh tô trên **Mặt bằng lanh tô tầng X** (mặt bằng cùng tên / cùng cao độ, chưa có thì tạo); cửa giống nhau (bề rộng, tường 110 / 220, loại cửa) gộp LT1, LT2…, số lượng = số ô cửa; chạy lại không nhân đôi. Lanh tô tường 110 được kiểm tra / gợi ý tiết diện, gối như lanh tô nhập tay.
- **Mở mặt bằng CAD** trong Triển khai hồ sơ: bản vẽ vừa đọc được gắn làm **ánh xạ kiến trúc (xref)** của mặt bằng đang xem (nếu chưa có), dời để trục đầu tiên trùng trục hồ sơ.
- Cột **lệch tâm** (trục qua mép cột): mặt bằng kết cấu và mặt bằng móng ghi kích thước trục → mép cột; xuất **ETABS (.e2k)** dùng điểm chèn CARDINALPT (trục qua mép trái → 4, mép phải → 6, dưới → 2, trên → 8, góc → 1/3/7/9); độ lệch không bằng nửa cạnh ghi dòng chú thích "$ COLUMN … OFFSET" để chỉnh tay. **SAP2000 (.s2k)**: bảng FRAME INSERTION POINT ASSIGNMENTS với độ dời đầu nút XI, YI, XJ, YJ đúng độ lệch.
- Dầm chia đoạn: ghi chú nối tiếp có **số tờ** – "XEM TIẾP ĐOẠN 2/3 – TỜ KC-05", "TIẾP THEO ĐOẠN 1/3 – TỜ KC-04" (tự cập nhật khi dàn / đánh số lại); điểm cắt / nối thép sai (dấu X đỏ) hiện cả trên các đoạn. Bản vẽ dầm chế độ Thiết kế (KC-04) cũng tự chia đoạn khi mặt đứng dài hơn bề rộng khổ giấy.
- **DANH MỤC MẶT CẮT** dưới các mặt cắt mỗi dầm: số mặt cắt, các vị trí dùng chung (gối / giữa nhịp), b×h, thép trên, thép dưới, đai. Tắt trong bộ tờ (SectionCatalog).

### 4.18. Khổ giấy và tỷ lệ tự động

- Cửa sổ **Dàn trang in** → mục **BỘ TỜ**: **Khổ** (Tự động, A4, A3, A2, A1, A0), **hướng** (Ngang, Đứng, Tự động), **Tỷ lệ mặt bằng** (Tự động hoặc 1/50, 1/75, 1/100…), **Khổ lớn nhất** khi tự chọn khổ → **Áp dụng cho toàn bộ (tự dàn trang)**.
- Khổ chốt (vd. A3): mặt bằng lấy **tỷ lệ lớn nhất** (trong dãy chuẩn 1/50 … 1/500) mà hình – kể cả kích thước, đầu trục, tiêu đề – vừa vùng vẽ trừ khung tên và lề; chi tiết giữ tỷ lệ chuẩn (1/20 – 1/50), mặt cắt 1/10 – 1/25, thu nhỏ nếu không vừa.
- Khổ **Tự động**: tỷ lệ chốt (vd. mặt bằng 1/100) → chọn **khổ nhỏ nhất** chứa vừa; nếu để tỷ lệ tự động: mặt bằng nhỏ dùng 1/50 (vừa A3), mặt bằng lớn 1/100…; hình vượt khổ lớn nhất cho phép thì hạ tỷ lệ.
- Mục **TỜ ĐANG CHỌN**: khổ, hướng, **Tỷ lệ hình chính** → **Áp dụng cho tờ đang chọn** (các hình phụ đổi tỷ lệ cùng hệ số; tờ được khoá).
- Hình được **vẽ lại** ở tỷ lệ mới (không phóng / thu hình cũ): chữ ghi chú luôn cao 2,5 mm khi in, tiêu đề 3,5 mm, vòng số hiệu, đầu trục, mũi tên kích thước đúng cỡ; tiêu đề hình ghi đúng "tl: 1/xx", khung tên ghi các tỷ lệ của tờ.
- Mặt bằng kết cấu tự tránh đè nhãn: nhãn dầm đặt giữa nhịp, đụng nhãn cột / dầm khác thì dời dọc dầm hoặc sang phía bên kia; nhãn lanh tô, ô văng tránh dầm; chữ thép sàn Ø..a.. luôn cách vòng số hiệu và không đè nhau.

### 4.19. Khung tên, tên và số hiệu bản vẽ

- Cửa sổ **Dàn trang in** → mục **KHUNG TÊN** chọn: **Mặc định** (khung tên dựng sẵn), **Tự tạo** hoặc **Tải lên (AutoCAD)**.
- **Thiết kế khung tên…**: bảng các ô – hàng, trường (Đơn vị thiết kế, Công trình, Hạng mục, Địa điểm, Chủ đầu tư, Chủ trì, Thiết kế, Kiểm tra, Duyệt, Tên bản vẽ, Số hiệu, Tỷ lệ, Ngày, Ghi chú, Lần sửa, Tờ số…), nhãn ô, bề rộng (mm), cỡ chữ, bật / tắt ô, hiện nhãn; chiều cao từng hàng; vị trí **góc phải dưới** hoặc **dải dọc bên phải**. Bấm **Xem trước**, **Khôi phục mặc định**, **Dùng khung tên này**.
- **Tải khung tên AutoCAD (DWG/DXF)…**: lấy đường, đa tuyến, cung, tròn, chữ, block chèn; trường điền tự động lấy từ thuộc tính ATTDEF/ATTRIB (thẻ TENBANVE, SOHIEU, TYLE, NGAY, CONGTRINH… – có / không dấu, có / không gạch nối) hoặc chữ giữ chỗ **{TEN_BAN_VE}**, **{SO_HIEU}**, **{TY_LE}**, **{NGAY}**, **{CONG_TRINH}**, **{CHU_DAU_TU}**, **{THIET_KE}**, **{TRANG}/{TONG_TRANG}**… Khung ngoài cùng bằng khổ giấy A0…A4 (kể cả vẽ theo tỷ lệ 1/10…1/200) được nhận là **khung bao toàn tờ** và co vừa khổ từng tờ; nếu chỉ là bảng nhỏ thì đặt vào **góc phải dưới** khung viền. Hồ sơ lưu bản sao khung tên – mở lại không cần file gốc (đường dẫn file gốc lưu tương đối so với thư mục hồ sơ).
- **Thông tin khung tên…**: nhập Đơn vị thiết kế, Hạng mục, Chủ trì, Kiểm tra, Duyệt, Lần sửa, Ghi chú. Tên công trình, chủ đầu tư, địa điểm, người thiết kế lấy từ Thông tin hồ sơ; tên bản vẽ, số hiệu, tỷ lệ, ngày, tờ số điền theo từng tờ. Khung tên dùng chung cho xem trước, DWG/DXF, PDF và in.
- **Tên bản vẽ tự động theo cây thư mục công trình**: tờ mặt bằng mang tên mặt bằng (MẶT BẰNG KẾT CẤU MÓNG, MẶT BẰNG KẾT CẤU TẦNG 2…); tờ chi tiết ngay sau mặt bằng của nó, tên ghi loại và cấu kiện trên tờ – vd. CHI TIẾT MÓNG ĐƠN M1, M2; CHI TIẾT DẦM D1–D3; tờ thống kê ghi tên bảng. Danh mục bản vẽ tự cập nhật.
- **Đánh số bản vẽ**: tiền tố (KC, KT, KCM…), mẫu **KC-01** (liên tục), **KC01** (không gạch) hoặc **KC-1.01** (theo nhóm mặt bằng – tờ danh mục là nhóm 0), số bắt đầu, số chữ số → **Đánh số lại**.
- **Đổi tên bản vẽ**: sửa ô **Tên bản vẽ** / **Số hiệu** của tờ đang chọn (nháy đúp vào tờ trong danh sách để sửa nhanh). Tên / số đã sửa tay được giữ khi tự dàn trang và đánh số lại; **Đặt lại tên tự động** trả tờ đang chọn về tên – số tự động (giữ Shift khi bấm: mọi tờ).

### 4.20. Bản vẽ kiểu Revit: trình duyệt dự án, khung nhìn, bản vẽ (sheet), thuộc tính, cỡ chữ

- Cột trái có **thẻ dọc**: **Cấu kiện** (cây cấu kiện như trước) và **Trình duyệt** – trình duyệt dự án kiểu Revit: **Khung nhìn** (mọi hình sinh từ cấu kiện, nhóm theo mặt bằng kết cấu: mặt bằng + chi tiết các cấu kiện đặt trên mặt bằng đó; cấu kiện chưa đặt nhóm theo loại), **Bảng thống kê** (thống kê thép, vật liệu, DANH MỤC BẢN VẼ) và **Bản vẽ (Sheets)**. Khung nhìn đã đặt trên tờ có dấu ■ và số hiệu tờ.
- **Tạo bản vẽ**: nút **＋ Bản vẽ** (hoặc chuột phải mục Bản vẽ → Tạo bản vẽ mới…) – số hiệu **tự nhảy tiếp** (KC-01, KC-02… theo mẫu đánh số), bạn đặt tên; **F2** hoặc bảng Thuộc tính để đổi tên / số hiệu; xoá bản vẽ thì khung nhìn trở về danh sách chưa đặt.
- **Dàn khung nhìn**: mở bản vẽ (kích đúp) → thẻ **Bản vẽ (sheet)** hiện tờ giấy + khung tên; **kéo khung nhìn từ Trình duyệt thả vào tờ** (vị trí thả là tâm khung). Kéo để dời (tự bắt thẳng hàng cạnh trái / dưới với khung khác), **Delete** gỡ khung. Khung viền đỏ: chồng khung khác, đè khung tên hoặc ra ngoài vùng vẽ. Mỗi khung nhìn chỉ nằm trên **một** tờ (thả vào tờ khác là chuyển tờ); bảng thống kê đặt được nhiều tờ.
- **Kích đúp khung nhìn** (trên tờ hoặc trong trình duyệt) để **sửa cấu kiện** (mở bảng nhập + bản vẽ xem trước) hoặc **mặt bằng** (mở không gian vẽ đúng tầng) như *Activate View* của Revit; xong bấm **↩ Quay lại bản vẽ** hoặc **Esc** – khung nhìn vẽ lại theo cấu kiện đã sửa.
- **Bảng THUỘC TÍNH** (dưới trình duyệt) theo đối tượng đang chọn: *Bản vẽ* (số hiệu, tên, khổ, khổ đứng, khoá, ngày, thông tin dự án trong khung tên); *Khung nhìn trên tờ* (tên khung nhìn, hiện tiêu đề tên + tỷ lệ dưới hình, **tỷ lệ** – hình được vẽ lại, **cỡ chữ riêng** ×0,7…×2, vị trí X/Y, nút sửa cấu kiện / gỡ khỏi tờ); *Khung nhìn* (mục, tỷ lệ gốc, tờ đang chứa, đặt vào bản vẽ đang mở); *Cấu kiện* (tên, số lượng).
- **Cỡ chữ in**: chọn nút gốc trình duyệt (hoặc bỏ chọn) → mục **CỠ CHỮ IN**: ghi chú, nhãn thép, tên hình, tên mặt cắt, kích thước, chữ trục, bảng thống kê, vòng số hiệu Ø, đầu trục Ø (mm khi in; để trống = theo Thiết lập bản vẽ) và **hệ số chung**. Mọi hình triển khai (xem trước, bản vẽ, xuất DWG/DXF/PDF, in) vẽ lại theo cỡ mới; chữ trong mô hình = cỡ in × mẫu số tỷ lệ.
- Khi chọn **cấu kiện**, bảng Thuộc tính liệt kê **mọi trường nhập** của cấu kiện theo nhóm (như bảng nhập ở giữa): sửa một ô rồi Enter / chuyển ô – cấu kiện được tính lại, bảng nhập giữa và các khung nhìn trên tờ cập nhật ngay.
- **Hoàn tác / làm lại trên bộ bản vẽ**: ở thẻ Bản vẽ (sheet), **Ctrl+Z** hoàn tác, **Ctrl+Y** làm lại các thao tác tạo / xoá / đổi tên / đánh số tờ, đặt / dời / gỡ khung nhìn, đổi tỷ lệ và cỡ chữ khung nhìn (tối đa 50 bước; mở hồ sơ khác thì xoá lịch sử). Cửa sổ xem trước bộ bản vẽ cũ cũng vẽ theo cỡ chữ riêng của từng khung nhìn.
- **DANH MỤC BẢN VẼ** là một khung nhìn bảng: kéo vào tờ đầu – bảng tự cập nhật khi thêm, xoá, đổi tên, đánh số bản vẽ. **Tự dàn trang** xếp các khung nhìn chưa đặt vào tờ (tờ đã khoá giữ nguyên). Xuất bản vẽ / PDF / in dùng đúng bộ bản vẽ bạn đã dàn.
- Chế độ Thiết kế: **Hiển thị → Cỡ chữ nhãn mặt bằng…** đặt cỡ chữ (px màn hình, 7–30) cho tên cột, dầm, sàn trên mặt bằng; lưu trên máy (ui.json).

## 5. Quy trình làm việc tổng quát

Một công trình đi qua các bước dưới đây. Có thể quay lại bước trước bất kỳ lúc nào; sửa mô hình sẽ yêu cầu chạy lại phân tích và thiết kế.

### 5.1. Sơ đồ các bước

| Bước | Việc làm | Ở đâu trong chương trình | Kết quả |
|---|---|---|---|
| 1. Khởi tạo | Dự án mới, mẫu định dạng, hoặc nhập từ bản vẽ kiến trúc / AutoCAD / Revit / ETABS | **Tệp → Mới**; **Tệp → Nhập bản vẽ kiến trúc (triển khai kết cấu)...**; **Tệp → Nhận dữ liệu từ AutoCAD…**; **Tệp → Nhập từ Revit (JSON)...**; **Tệp → Nhập mô hình ETABS/SAP (.e2k/.s2k)…** | Dự án .kcvxt |
| 2. Lưới, tầng | Bước cột X, Y; chiều cao tầng; cao độ chân cột; liên kết chân cột | Tab Công trình → Tạo mô hình | Lưới trục, tầng, khung sơ bộ |
| 3. Tiết diện, vật liệu | Tiết diện dầm/cột/sàn, cấp bê tông, nhóm thép | Tab Tiết diện; **Khai báo → Tiết diện dầm/cột…**; tab Công trình → Vật liệu | Thư viện tiết diện |
| 4. Mô hình | Cột, dầm, dầm phụ, công xôn, dầm cong, sàn, ban công, cầu thang, giếng trời, tường | Tab Mặt bằng (thanh công cụ sửa + công cụ dầm kiểu ETABS) | Mô hình hình học |
| 5. Tải trọng, tổ hợp | Tải sàn theo công năng, cấu tạo sàn, tải tường, tải nhập tay; tổ hợp | Tab Tải trọng; **Khai báo → Tự tạo tổ hợp theo TCVN 2737:2023** | Trường hợp tải, tổ hợp |
| 6. Gió, động đất | Địa danh → vùng gió, agR; dạng địa hình; loại nền; hệ số q; dao động riêng | Tab Công trình; **Khai báo → Nguồn khối lượng & dao động riêng…** | GX, GY, DDX, DDY |
| 7. Địa chất, móng | Hố khoan, chỉ tiêu nền, loại móng, cọc, ranh giới đất, cừ tràm | Tab Địa chất; tab Mô hình móng; **Thiết kế → Thiết kế móng…** | Phương án móng |
| 8. Phân tích | Tĩnh, dao động riêng, phổ phản ứng | **Phân tích → Chạy phân tích** (F5) | Nội lực, phản lực, chuyển vị |
| 9. Kiểm tra kết quả | Biểu đồ, phản lực, cảnh báo, so sánh ETABS/SAP | Tab Kết quả; Biểu đồ nội lực; **Phân tích → So sánh với ETABS/SAP/SAFE…** | Kết quả đã kiểm tra |
| 10. Thiết kế | Thép dầm, cột, sàn, móng, cầu thang; chọn bố trí thép; kiểm tra thép | **Thiết kế → Thiết kế BTCT**; **Thiết kế → Thiết kế móng…**; **Thiết kế → Chọn bố trí thép…**; **Thiết kế → Kiểm tra thép** | Cốt thép, kết luận đạt/không đạt |
| 11. Triển khai hồ sơ | Bản vẽ DXF, dàn trang, shop drawing, khối lượng, thuyết minh | Menu Triển khai hồ sơ; **Triển khai hồ sơ → Xuất tất cả vào thư mục...** | Bộ hồ sơ |
| 12. Kiểm chứng | Báo cáo kiểm chứng, đối chiếu ETABS/SAP, kỹ sư kiểm tra | **Trợ giúp → Kiểm chứng phần mềm…**; **Phân tích → So sánh với ETABS/SAP/SAFE…** | Hồ sơ đủ cơ sở |

> **Mẹo:** Bảng **Trợ giúp → Bắt đầu nhanh…** hiện các bước này kèm trạng thái của dự án đang mở.

### 5.2. Bảng Bắt đầu nhanh

Bảng **Bắt đầu nhanh** tự hiện khi mở chương trình lần đầu và khi tạo dự án mới (tích **Không hiện lại** để tắt; mở lại bằng **Trợ giúp → Bắt đầu nhanh…**). Mỗi dòng là một bước của quy trình:

- **Trạng thái** tự nhận biết từ dự án: ✓ đã làm (vd. đã có lưới trục, đã có cột – dầm – sàn, đã có tường, đã có kết quả phân tích, đã thiết kế), ○ chưa làm, ? cần bạn tự kiểm tra (vd. kiểm tra kết quả).
- **Làm bước này**: chuyển đến tab, khung nhìn hoặc công cụ tương ứng (vd. tab Công trình, công cụ ＋ Tường, chạy phân tích).
- **Xem hướng dẫn**: mở mục hướng dẫn của bước đó.
- Bảng không chặn cửa sổ chính; trạng thái được cập nhật mỗi khi bạn quay lại bảng (hoặc bấm **Làm mới**).

### 5.3. Nguyên tắc chung

- Lưu dự án thường xuyên (Ctrl+S). File .kcvxt là JSON, có thể sao lưu, so sánh phiên bản.
- "Tạo mô hình" ở tab Công trình **xoá mô hình hiện có** và sinh lại; "Áp dụng thông số" chỉ cập nhật thông tin, vật liệu, gió, động đất, nền đất.
- Trước khi thiết kế, xem tab Kết quả → **Cảnh báo / thông tin** và tab **Biểu đồ nội lực** để chắc mô hình làm việc đúng.
- Các lệnh xuất tự chạy phân tích và thiết kế nếu kết quả chưa có hoặc đã cũ.

### 5.4. Ví dụ: nhà phố 4 tầng 5×13,5 m

Ví dụ dùng đúng số liệu của dự án mẫu **NhaPho.kcvxt** (thư mục Dự án mẫu khi cài đặt; bộ kiểm thử tự lập lại và xuất toàn bộ hồ sơ của dự án này): nhà phố tại Thái Nguyên, mặt tiền 5 m, sâu 13,5 m (3 nhịp 4,5 m), 4 tầng cao 3,9 + 3,3 + 3,3 + 3,3 m, chân cột tại cao độ −0,6 m.

#### 5.4.1. Bước 1 – Khởi tạo lưới trục và tầng

1. **Tệp → Mới** (Ctrl+N). Ở tab **Công trình**, nhóm **Thông tin công trình**: Tên công trình "Nhà phố mẫu 4 tầng", Địa điểm xây dựng "Thái Nguyên", Chủ đầu tư, Người thiết kế.
2. Nhóm **Lưới trục và tầng (m)**: Bước cột theo X = **5**; Bước cột theo Y = **4.5 4.5 4.5** (hoặc 3*4.5); Chiều cao các tầng = **3.9 3.3 3.3 3.3**; Cao độ chân cột = **-0.6**; giữ **Ngàm** cho liên kết chân cột.
3. Nhóm **Tiết diện mặc định**: Cột **C22x30**, Dầm phương X **D22x30**, Dầm phương Y **D22x40**, Sàn **S100**.
4. Nhóm **Tải trọng (giá trị tiêu chuẩn)**: Hoàn thiện sàn 1.2; Hoạt tải sàn 1.5; Hoạt tải mái 0.75; Tường biên trên dầm **7.5** kN/m.
5. Nhóm **Địa điểm xây dựng**: Tỉnh/Thành phố gõ "Thái Nguyên" → chọn xã/phường → **Vùng áp lực gió** tự điền, W0 điền vào nhóm Gió. Kiểm tra lại với QCVN 02:2022 cho địa danh cụ thể.
6. Bấm **Tạo mô hình** và xác nhận. Thanh trạng thái báo số nút, thanh, ô sàn và tổ hợp đã tạo. Lưu dự án (**Tệp → Lưu thành...**).

> **Lưu ý:** Mô hình gồm 2 trục X (1, 2) × 4 trục Y (A…D), 4 tầng: cột tại mọi giao trục, dầm trên mọi đường trục, sàn trong mọi ô; tải hoàn thiện, hoạt tải sàn dồn 45° về dầm; tải tường biên 7,5 kN/m trên dầm biên.

#### 5.4.2. Bước 2 – Hoàn thiện mô hình trên mặt bằng

1. Chọn tab **Mặt bằng**, ô **Tầng đang xem** = tầng 2. Kiểm tra lưới, tên cột, dầm (bật **Tên cấu kiện**).
2. Nếu ô 4,5 × 5 m cần dầm phụ đỡ tường: bật **Dầm phụ**, bấm vào ô, chọn "Song song trục X", **Chia đều – số dầm n** = 1, tiết diện D22x30, giữ **Chia lại ô sàn theo dầm phụ** → Đồng ý. Đặt **Phạm vi tầng** = "Tất cả tầng" nếu muốn áp cho mọi tầng.
3. Ô cầu thang: bật **＋ Cầu thang**, bấm vào ô giữa nhà; chọn ô vừa tạo rồi bấm **Khai báo cầu thang…** để khai báo dạng thang, số bậc, bản thang, dầm chiếu nghỉ.
4. Phòng vệ sinh: bật **Sàn vệ sinh**, bấm vào ô sàn WC (hoàn thiện 2,0 kN/m²).
5. Ban công tầng 2–4: bật **＋ Ban công**, nhập **Vươn (m)** = 1.2, **Lan can (kN/m)** = 1.0, bấm vào dầm biên mặt tiền.
6. Tường trong nhà: chọn **Loại tường** (vd. tường 110), bật **＋ Tường** hoặc **Gán tường** rồi bấm dầm. Ctrl+Z để hoàn tác khi bấm nhầm.

#### 5.4.3. Bước 3 – Tải trọng và tổ hợp

1. Tab **Tải trọng**: kiểm tra trường hợp tải TT (trọng lượng bản thân, γf 1,1), HTHT (hoàn thiện, 1,3), HT (hoạt tải, 1,3), GX, GY (gió, 1,2).
2. Bật **Hiện tải tự sinh (sàn)** để xem tải sàn đã dồn lên dầm; thêm tải tập trung / phân bố riêng nếu cần (**+ Cho thanh đang chọn**).
3. Tab Công trình → nhóm **Gió**: bật **Tính tải gió**, dạng địa hình B (hoặc theo thực tế), Cp. Nhóm **Động đất**: nếu agR đáng kể, bật **Tính động đất**, nhập agR, loại nền, q → **Áp dụng thông số**.
4. **Khai báo → Tự tạo tổ hợp theo TCVN 2737:2023**: chương trình sinh tổ hợp TTGH I (TH1, TH2…), TTGH II (TC1…) và tổ hợp bao **BAO**.

#### 5.4.4. Bước 4 – Địa chất và móng

1. Có hồ sơ khảo sát: tab **Địa chất** → **+ Hố khoan**, nhập toạ độ, mực nước ngầm, các lớp đất (hoặc **Chọn theo đất tham khảo…**), tích **Dùng hố khoan cho thiết kế móng**. Chưa có: dùng nhóm **Nền đất dưới móng đơn** ở tab Công trình (φ, c, γ, Df).
2. Tab **Mô hình móng**: **Loại móng** = Móng đơn (hoặc Móng cọc / Móng băng); **Thông số…** để khai báo cọc, móng băng.
3. Nhà phố thường sát ranh: **Ranh giới đất…** → khai báo cạnh trái/phải; cột sát ranh sẽ dùng móng lệch tâm có dầm móng cân bằng.

#### 5.4.5. Bước 5 – Phân tích và kiểm tra kết quả

1. Nhấn **F5** (**Phân tích → Chạy phân tích**). Thanh trạng thái báo số phương trình, trường hợp tải, cảnh báo.
2. Tab **Kết quả**: chọn trường hợp TT, xem **Phản lực chân cột**; tổng phản lực đứng phải bằng tổng tải đứng (bộ kiểm thử của chương trình kiểm tra đúng điều này cho dự án mẫu).
3. Tab **Biểu đồ nội lực**: Đại lượng M3, khung nhìn "Mặt đứng theo trục", trục A → xem biểu đồ mô men khung; đổi sang BAO để xem đường max/min.
4. Tuỳ chọn: **Tệp → Xuất mô hình → Mô hình ETABS .e2k...**, chạy trong ETABS, xuất bảng kết quả rồi **Phân tích → So sánh với ETABS/SAP/SAFE…** để đối chiếu.

#### 5.4.6. Bước 6 – Thiết kế

1. **Thiết kế → Thiết kế BTCT**: tab **Thiết kế** hiện bảng Dầm, Cột, Sàn, Móng, Cầu thang; dòng tô đỏ là cấu kiện không đạt.
2. Cấu kiện không đạt: tăng tiết diện (**Khai báo → Tiết diện dầm/cột…**) hoặc **Thiết kế → Tự tăng tiết diện đến khi đạt** rồi thiết kế lại.
3. Chọn một dầm trên mặt bằng → **Chọn bố trí thép…** để chọn phương án thép dưới, mũ gối, chạy suốt, đai theo ý muốn.
4. **Thiết kế → Thiết kế móng…**: xem kích thước móng, thép đáy, kiểm tra áp lực đáy, chọc thủng, lún ở tab Mô hình móng → **Kiểm tra an toàn…**.

#### 5.4.7. Bước 7 – Triển khai hồ sơ

1. **Tuỳ chọn → Thiết lập bản vẽ (tỷ lệ, cỡ chữ)…** và **Tuỳ chọn → Chuẩn layer CAD…**: chọn tỷ lệ, cỡ chữ, kiểu kích thước, tên layer theo chuẩn của đơn vị (một lần, dùng cho mọi dự án).
2. **Triển khai hồ sơ → Xuất tất cả vào thư mục...**: chọn thư mục → chọn Gộp 1 file / Tách rời → xem/đổi tên layer → chương trình tạo thư mục <tên công trình>_<ngày giờ> gồm MoHinh.kcvxt, BanVe (KC-01…KC-08), GiaCongThep, ThuyetMinh.docx, BangTinh.xlsx, bảng khối lượng, NoiLuc (CSV), MoHinh.e2k, MoHinh.s2k.
3. **Triển khai hồ sơ → Dàn trang & in bản vẽ…**: **Tạo bộ bản vẽ mặc định**, chỉnh khung tên, bố cục, in hoặc **Xuất PDF…**.
4. **Triển khai hồ sơ → Lập thuyết minh tự động…**: chọn chương, nhập trang bìa → thuyết minh có mục lục, số trang; mở bằng Word và chọn Yes để cập nhật mục lục.
5. Kỹ sư kiểm tra lại toàn bộ số liệu, ký hồ sơ.

> **Mẹo:** Các file kết quả mẫu của ví dụ này (DXF, Word, Excel, .e2k, .s2k) được bộ kiểm thử tạo trong thư mục KetQuaMau/NhaPho mỗi lần build – dùng để so sánh khi học chương trình.

## 6. Khởi tạo dự án

Có sáu cách bắt đầu một dự án; chọn cách phù hợp với dữ liệu đang có. Ngoài ra có thể gắn bản vẽ AutoCAD làm nền để vẽ theo (*mục 6.12 (Liên kết bản vẽ AutoCAD (DWG/DXF) làm nền mặt bằng)*).

### 6.1. Dự án mới từ lưới trục (tab Công trình)

1. **Tệp → Mới** (Ctrl+N) – mẫu định dạng mặc định (nếu đã đặt) được áp dụng tự động.
2. Nhập **Thông tin công trình** (tên, địa điểm, chủ đầu tư, người thiết kế – dùng cho khung tên, trang bìa thuyết minh).
3. Nhập **Bước cột theo X (trục 1, 2, 3...)** và **Bước cột theo Y (trục A, B, C...)**: các khoảng cách cách nhau dấu cách, viết gọn được "3*3.3" = 3.3 3.3 3.3.
4. Không nhớ kích thước? Bấm **Tải mặt bằng để lấy kích thước…** (ngay dưới ô Liên kết chân cột): mở **bản vẽ CAD DWG/DXF** – chương trình tự đọc trục (layer nhóm Trục và tên trục; không có thì theo tim tường), chọn mặt bằng tầng, chiều cao tầng lấy theo các mặt bằng tầng nếu có; hoặc mở **ảnh chụp / scan mặt bằng** (JPG, PNG): ① bấm 2 điểm của một kích thước đã biết và nhập chiều dài thật (mm), ② bấm lần lượt các trục đứng 1, 2, 3…, ③ bấm các trục ngang A, B, C… – chương trình tính bước trục (làm tròn 10 / 50 / 100 mm). Kiểm tra rồi **Đồng ý** để điền vào các ô.
5. **Chiều cao các tầng (dưới → trên)**, **Cao độ chân cột (m)** (thường âm, vd. −0,6 … −1,5), **Liên kết chân cột** (Ngàm / bỏ chọn = khớp).
6. Chọn **Tiết diện mặc định** (cột, dầm phương X, dầm phương Y, sàn), **Tải trọng**, **Gió**, **Động đất**, **Vật liệu**, **Nền đất dưới móng đơn**.
7. Bấm **Tạo mô hình** (**Vẽ → Tạo khung BTCT từ lưới trục** cùng tác dụng). Sau này chỉ đổi thông số (không xoá mô hình) thì bấm **Áp dụng thông số**.

> **Cảnh báo:** **Tạo mô hình** xoá mô hình hiện có và sinh lại lưới trục, tầng, cột, dầm, sàn và tải. Các sửa đổi trên mặt bằng sẽ mất – lưu dự án trước.

### 6.2. Mở mặt bằng CAD lấy dữ liệu: chọn trục, trục qua tim / mép cột

Trong hộp **Tải mặt bằng để lấy kích thước…**, sau khi mở bản vẽ DWG/DXF, bản vẽ hiện ở giữa (cuộn chuột để thu phóng, giữ nút giữa để kéo; bật / tắt từng nhóm nét theo màu: tường, trục, cột, cửa, thang, dầm, sàn). Cột tô **xanh** = đúng tim trục, **cam** = lệch tâm so với trục, **xám** = không nằm trên giao trục.

- **Nguồn trục – Tự nhận theo layer trục**: đọc các nét trên layer trục (TRUC, AXIS, GRID…). Bản vẽ đặt tên layer khác: mở mục **Layer trục**, tích các layer là trục rồi **Đọc lại**.
- **Nguồn trục – Tự nhận theo cột**: gom các cột thẳng hàng (sai số mặc định 50 mm) thành trục – dùng khi bản vẽ không có layer trục.
- **Nguồn trục – Người dùng chọn trục**: bấm lên nét trục để thêm trục theo phương nét; bấm lên cột để thêm trục qua cột (chọn trục đứng X hoặc trục ngang Y); chuột phải lên trục để xoá. **Lấy trục tự nhận** chép trục layer / cột sang danh sách để sửa tay. Chọn trục trong danh sách để **Đổi tên** / **Xoá**; **Tên tự động** đặt 1, 2, 3… / A, B, C….
- **Trục đi qua**: Tim cột; Mép cột – mép trái / dưới; Mép cột – mép phải / trên; Mép cột – **mép ngoài nhà** (trục biên qua mép ngoài cột, trục giữa qua tim – hay gặp ở nhà phố sát ranh đất). Cột có tâm lệch khỏi trục được ghi **lệch tâm** (vd. ±110 mm với cột 220): mặt bằng kết cấu, mặt bằng định vị cột và mặt bằng móng vẽ cột / móng đúng vị trí thật; sơ đồ tính giữ nút tại giao trục.
- **Đơn vị bản vẽ**: Tự động (theo $INSUNITS và kích thước bản vẽ) hoặc chọn mm / cm / m nếu bản vẽ khai báo sai.
- **Thông số đọc được**: bước trục X, Y; cột trên giao trục và tiết diện (vd. C30x22 – cạnh theo Y × cạnh theo X); tường trên trục (tổng chiều dài tường 110, 220); công năng ô sàn theo tên phòng (WC → vệ sinh, ban công, hành lang, kho…); lỗ sàn cầu thang / thông tầng.
- **Áp dụng**: tích các mục cần cập nhật (tiết diện cột, lệch tâm cột, xoá cột ở giao trục không có cột, tường → tải tường lên dầm, công năng ô sàn, lỗ sàn). **Đồng ý** điền bước trục vào ô Lưới trục và tầng, dựng lại mô hình theo lưới mới (nếu chọn hoặc lưới khác) rồi áp dụng; hộp xác nhận liệt kê các thay đổi, hoàn tác bằng Ctrl+Z.
- Chế độ **Triển khai hồ sơ** (nút **Tải mặt bằng…** ở không gian vẽ mặt bằng kết cấu): cập nhật bước trục của mặt bằng kết cấu và đặt cột lên mặt bằng đang xem tại vị trí thật (giao trục + lệch tâm); cột cùng tiết diện dùng chung một cấu kiện (C1, C2…).

> **Mẹo:** Tường, công năng, lỗ sàn được áp cho các tầng điển hình (trừ tầng mái). Kiểm tra lại các tầng có mặt bằng khác (tầng 1 kinh doanh, tầng tum…).

### 6.3. Mẫu định dạng (template)

**Tuỳ chọn → Mẫu định dạng (template)…** mở thư viện mẫu của người dùng (%AppData%\KetCauVXT\Templates). Mẫu **không chứa mô hình** mà chứa các thiết lập dùng lại giữa các dự án: chuẩn CAD (layer, font, kiểu bản vẽ), khung tên người dùng, tờ mẫu, thông tin khung tên (đơn vị thiết kế, người ký, logo, tiền tố số hiệu), tuỳ chọn thuyết minh, định mức khối lượng, tuỳ chọn thép.

- **Tạo từ dự án hiện tại…**: chọn các phần đưa vào mẫu, đặt tên → lưu thành file *.kcvxt-mau.
- **Áp dụng vào dự án…**: Gộp (giá trị của mẫu ghi đè, giữ phần mẫu không có) hoặc Thay thế (phần có trong mẫu thay toàn bộ phần tương ứng).
- **Đặt làm mặc định**: mẫu được áp dụng tự động cho mọi dự án mới; **Bỏ mặc định** để thôi.
- **Nhập file… / Xuất file…** để gửi mẫu cho đồng nghiệp; **Đổi tên…**, **Mở thư mục**.

### 6.4. Khung thép tiền chế

1. **Vẽ → Tạo khung thép tiền chế...** (hoặc nút **Khung tiền chế**).
2. Nhập Nhịp khung L, Chiều cao đỉnh cột H, Độ dốc mái i (%), Số bước khung, Bước khung B, Khoảng cách giằng / xà gồ, liên kết chân cột (Khớp / ngàm).
3. Nhập Tĩnh tải mái (tôn + xà gồ), Hoạt tải mái, hệ số khí động Ce cột và mái (đón gió / khuất gió).
4. Tiết diện cột, kèo chữ I: h1 (đầu I), h2 (đầu J – vát), bf, tf, tw và mác thép → **Tạo khung**. Chương trình hỏi có xoá mô hình hiện có không (No = thêm vào mô hình).
5. Chạy phân tích (F5) rồi **Thiết kế → Kiểm tra thép**.

> **Lưu ý:** Khung phẳng trong mặt phẳng XZ, đặt tại các trục Y = 0, B, 2B… Gió ngang nhà gán trực tiếp lên cột và kèo theo W0, địa hình ở tab Công trình.

### 6.5. Từ bản vẽ kiến trúc DWG/DXF (triển khai kết cấu tự động)

**Tệp → Nhập bản vẽ kiến trúc (triển khai kết cấu)...** hoặc nút **Từ bản vẽ KT**: đọc bản vẽ kiến trúc sơ bộ, nhận diện các mặt bằng tầng, đề xuất hệ kết cấu và tải trọng, cho xem trước, sửa rồi tạo mô hình.

1. **Chọn file DXF/DWG...** (chọn được nhiều file; một file có thể chứa nhiều mặt bằng tầng đặt cạnh nhau). **Đơn vị bản vẽ**: Theo file / mm / cm / m.
2. Bấm **Nhận diện**: chương trình tách từng mặt bằng, đọc tên hình ("MẶT BẰNG TẦNG 1/2/…/TUM/MÁI" – đọc được Unicode, mã \U+, font TCVN3), nhận trục và tên trục (kể cả block có thuộc tính), tường (2 nét song song dày 80–450 mm, khe ≤ 1,6 m là cửa), cột kiến trúc, cửa (block), phòng theo chữ ghi, cầu thang, ban công.
3. Bảng **Các mặt bằng tầng**: sửa cột Dùng, Tầng, Tum, Mái, H (m) rồi bấm **Đề xuất kết cấu**.
4. **Thông số triển khai**: nhịp dầm lớn nhất, cạnh ngắn ô sàn lớn nhất, công xôn lớn nhất, bề rộng dầm, cạnh cột nhỏ nhất, trọng lượng riêng khối xây, cao trình chân cột, chiều cao tầng, cấp bê tông, **Đặt dầm phụ đỡ tường không nằm trên trục**, **Chân cột ngàm**.
5. **Từ khoá tên lớp (layer)** và bảng **Tải theo công năng phòng** (TT hoàn thiện, hoạt tải, không sàn, từ khoá) sửa được; tab **Layer trong file** cho phép gán từng layer vào nhóm.
6. Xem trước trên **Mặt bằng** từng cao trình: dầm chính xanh dương, dầm phụ xanh ngọc, công xôn cam, dầm biên tím; bấm vào dầm/cột/ô sàn để bỏ hoặc chọn lại (nét đứt xám = đã bỏ). Bảng **Cột** sửa được vị trí, kích thước, tầng lên đến.
7. Bấm **Tạo mô hình**. Chương trình hỏi có chạy **Tự tăng tiết diện đến khi đạt** không: đồng ý thì lặp phân tích → thiết kế → tăng 50 mm cho dầm/cột không đạt, 10 mm cho sàn.

> **Mẹo:** Để nhận diện tốt: tường trên lớp chứa TUONG/WALL, trục trên lớp TRUC/GRID (hoặc kiểu nét CENTER), cầu thang trên lớp chứa THANG, cửa trên lớp CUA/DOOR; ghi tên phòng bằng chữ trong phòng; ghi "MẶT BẰNG TẦNG …" ngay dưới hoặc trên mỗi mặt bằng. Quy tắc nhận diện layer chỉnh ở **Tuỳ chọn → Chuẩn layer CAD…** → tab **Nhận diện layer kiến trúc**.

> **Lưu ý:** Giới hạn: chưa hỗ trợ tường xiên/cong, tầng hầm, tầng lửng (nhập số tầng thủ công), bản vẽ PDF/ảnh. Bản vẽ kiến trúc mẫu để thử: DuAnMau\KienTrucMau_DauVao.dxf.

### 6.6. Nhập mặt bằng kết cấu DWG/DXF

**Tệp → Nhập mặt bằng kết cấu DWG/DXF...** (nút **Nhập DWG/DXF**) đọc một mặt bằng kết cấu đã vẽ sẵn: trục (layer TRUC), cột (COT), dầm (DAM), sàn (SAN) và dựng mô hình theo các tầng đã khai báo. Kết thúc, hộp thông báo liệt kê số cột, dầm, ô sàn đã nhập và cảnh báo (nếu có).

File DWG được đọc trực tiếp (AutoCAD R14 … 2018+). Nếu đọc lỗi, chương trình thử chuyển đổi bằng ODA File Converter (miễn phí) nếu máy có cài; nếu vẫn không được, trong AutoCAD dùng SAVEAS → DXF.

### 6.7. Từ AutoCAD (add-in, lệnh VXT_*)

Add-in cho phép tích chọn trực tiếp trên bản vẽ kiến trúc các nhóm đối tượng rồi gửi sang chương trình (cài đặt: *mục 2.4 (Add-in AutoCAD (liên kết AutoCAD → Việt Const))*).

| Lệnh | Việc làm |
|---|---|
| VXT (VXT_MENU) | Bảng lệnh: gán nhóm, khai báo tầng, xem, xoá, chuẩn layer, gửi dữ liệu. |
| VXT_TUONG, VXT_COT, VXT_DAM, VXT_SAN, VXT_TRUC, VXT_CUA, VXT_THANG, VXT_TENPHONG, VXT_BOQUA | Gán nhóm: [DoiTuong] quét chọn (lưu XData trong DWG) hoặc [Layer] – mọi đối tượng trên layer đó thuộc nhóm. |
| VXT_TANG | Khai báo tầng [Them/Danhsach/Xoa]: tên, cao độ mặt sàn, chiều cao tầng, điểm gốc (cùng một giao trục ở mọi tầng), vùng mặt bằng (2 góc). |
| VXT_XEM / VXT_XOA | Xem số đối tượng theo nhóm, tô sáng / bỏ gán nhóm. |
| VXT_LAYER | Tạo layer kết cấu KC-…, chuyển đối tượng đã gán sang layer kết cấu, tạo kiểu chữ VXT_ARIAL / VXT_TIMES. |
| VXT_CHUANLAYER (VXT_CL) | Chuẩn hoá layer bản vẽ bất kỳ về layer chuẩn Việt Const VXT-… để chương trình đọc chắc chắn (xem *mục 6.8 (Chuẩn hoá layer bản vẽ về layer chuẩn Việt Const (VXT-…))*). |
| VXT_GUI | Ghi %AppData%\KetCauVXT\Link\acad_<ngày>_<giờ>.json và báo chương trình (đang mở thì hỏi "Có dữ liệu mới từ AutoCAD… Mở hộp thoại nhập?"; chưa mở thì tự chạy). |

Trong chương trình: **Tệp → Nhận dữ liệu từ AutoCAD…** chọn file acad_*.json; hộp thoại **Nhập bản vẽ kiến trúc** mở với các tầng đã khai báo và phương án kết cấu đề xuất – tiếp tục như *mục 6.5 (Từ bản vẽ kiến trúc DWG/DXF (triển khai kết cấu tự động))*.

> **Mẹo:** Tường vẽ 2 nét song song (tường 1 nét lấy dày 110 mm); cột là đa tuyến kín, miền tô hoặc 4 nét rời 0,12–1,0 m; tên trục ≤ 4 ký tự gần đầu trục; cửa giữ dạng block. Elip, spline chưa hỗ trợ (bị bỏ qua, có báo số lượng).

### 6.8. Chuẩn hoá layer bản vẽ về layer chuẩn Việt Const (VXT-…)

Bản vẽ kiến trúc / kết cấu của mỗi văn phòng đặt tên layer khác nhau (A-WALL, KT_TUONG 220, Tường, LAYER1…). Chuẩn hoá layer chuyển chúng về bộ layer chuẩn để Việt Const đọc chắc chắn: **VXT-TRUC** (đường trục), **VXT-TRUC-KH** (vòng ký hiệu, tên trục), **VXT-COT**, **VXT-TUONG-110**, **VXT-TUONG-220**, **VXT-TUONG**, **VXT-CUA-DI**, **VXT-CUA-SO**, **VXT-THANG**, **VXT-LO-SAN**, **VXT-TEN-PHONG**, **VXT-DAM**, **VXT-SAN**, **VXT-KICH-THUOC**, **VXT-BO-QUA**. Mọi bộ đọc của chương trình nhận layer VXT-… trước mọi quy tắc khác.

- **Tự nhận**: theo TÊN layer (tiếng Việt / Anh, có hoặc không dấu, tiền tố A-…, S-…, xref NHA|…) và theo NỘI DUNG: cặp nét song song cách 110 / 220 → tường 110 / 220; nhiều chữ nhật kín 0,1–1,2 m → cột; nét gạch chấm dài + vòng tròn + chữ 1–2 ký tự → trục; block CUA*, DOOR*, D1… → cửa đi, WINDOW*, S1… → cửa sổ; chữ "Phòng ngủ", "WC", "Bếp"… → tên phòng; chữ toàn số, đường kích thước → kích thước; nét bậc cách đều 0,24–0,34 m → cầu thang. Mỗi dòng có độ tin cậy và căn cứ; ô vàng = tin cậy thấp, nên xem lại.
- **Trong AutoCAD – lệnh VXT_CHUANLAYER (VXT_CL)** hoặc nút **Chuẩn hoá layer bản vẽ → VXT-…** trên bảng lệnh VXT: bảng layer gốc → layer chuẩn (sửa cột **Layer chuẩn**), **Lọc**, **Chỉ hiện layer chưa nhận**, **Tô sáng layer đang chọn**, **Chọn đối tượng trên bản vẽ…** (layer của các đối tượng chọn được gán vào layer chuẩn đang chọn ở ô **Gán vào**), **Tự nhận lại**.
- **Cách áp dụng** (AutoCAD): ① **Tạo bản sao chuẩn hoá <tên>_VXT.dwg** cạnh file – bản vẽ gốc giữ nguyên (mặc định, khuyên dùng; có thể bind xref đã nạp trong bản sao); ② **Chuyển đối tượng sang layer chuẩn** ngay trong bản vẽ đang mở; ③ **Đổi tên layer** khi một layer gốc ứng với một layer chuẩn, phần còn lại chuyển đối tượng. ② và ③ là một lệnh – gõ **U** để hoàn tác. Tuỳ chọn: sửa cả đối tượng trong định nghĩa block, block cửa → layer cửa, lưu thêm DXF (<tên>_VXT.dxf) để mở trong Việt Const, gửi sang Việt Const (VXT_GUI) sau khi chuẩn hoá. Xref chưa nạp được cảnh báo (nạp lại rồi chạy lệnh).
- **Trong Việt Const (không cần AutoCAD)**: hộp **Tải mặt bằng để lấy kích thước…** → mở bản vẽ → nút **Chuẩn hoá layer…**: cùng bảng đề xuất; **Đồng ý** đọc lại bản vẽ theo bảng (file gốc không bị sửa); **Xuất DXF đã chuẩn hoá…** ghi bản sao DXF với layer VXT-….
- **Bộ ánh xạ** (layer gốc → layer chuẩn): **Lưu bộ ánh xạ…** / **Mở bộ ánh xạ…** để dùng lại cho các bản vẽ cùng văn phòng kiến trúc; lưu trong %AppData%\VietConst\layer-profiles (chỉ trên máy, không gửi đi đâu). Gán trong bộ ánh xạ được ưu tiên trước tự nhận; xref NHA|A-WALL dùng lại gán của A-WALL.

> **Mẹo:** Kiểm tra lại các dòng tin cậy thấp và các layer "(giữ nguyên)" trước khi áp dụng; layer 0, DEFPOINTS và layer của xref không bị đổi tên.

### 6.9. Cao độ từ móng đến mái, bộ môn Kiến trúc – Kết cấu – Điện nước, IFC / SketchUp / Revit

Mô hình đi từ **đáy móng → mặt móng (chân cột) → nền tầng 1 (±0.000) → sàn tầng 2, sàn tầng 3… → sàn mái (→ sàn tum)**. Các sàn do khung đỡ là tầng phân tích; **sàn nền tầng 1** nằm trên đất (on grade) – không dồn tải lên dầm cột, chỉ tính khối lượng, bản vẽ, IFC. Độ sâu móng dùng chung với thiết kế móng (Địa chất → chiều sâu chôn móng).

- **BIM → Tạo cao độ tầng…**: số tầng, chiều cao tầng 1 / tầng trên, cao độ nền, chiều sâu móng, mặt móng, kiểu mái, tầng tum; tuỳ chọn sinh khung + sàn theo lưới trục và sàn nền tầng 1 (chỉ dùng cho mô hình chưa có cấu kiện).
- **Chèn tầng…** (phía trên tầng đang xem: sao dầm, sàn, cắt cột, nâng các tầng trên), **Xoá tầng** (nối cột, hạ các tầng trên), tên tầng tự đặt lại T2, T3…, MAI, TUM; **Sàn mọi ô** tạo ô sàn còn thiếu; **Sàn nền** tạo sàn trên đất theo ô lưới. Vẫn vẽ sàn trực tiếp bằng công cụ Sàn.
- **Bộ môn**: công tắc **Kiến trúc | Kết cấu | Điện nước** ở đầu ribbon – mỗi bộ môn hiện tab của mình; đối tượng bộ môn khác vẽ **mờ như xref**, không chọn được. **BIM → Bộ môn hiển thị…** bật / tắt, chỉnh độ mờ. Tường, cửa, phòng, trần, mái, đồ đạc thuộc Kiến trúc; cột, dầm, sàn, móng thuộc Kết cấu; ống, phụ kiện, thiết bị vệ sinh, điện, đèn thuộc Điện nước.
- **Kiến trúc**: vẽ tường, đặt cửa (tường tự cắt ô cửa); **Tìm phòng** tạo phòng từ mạng tường (tên, diện tích), **Đặt tên phòng…** (tên quyết định công năng sàn); **Kiến trúc → kết cấu**: tường → tải dầm / sàn, phòng → công năng ô sàn (WC → sàn vệ sinh…), cửa → lanh tô, giao tường chữ L/T chưa có cột → đề xuất (và tuỳ chọn thêm) cột.
- **Thư viện 3D…**: nhập đồ đạc / thiết bị từ SketchUp (File → Export → 3D Model → OBJ hoặc COLLADA .dae; OBJ hỏi hệ số đơn vị) hoặc từ Revit (*.vxtmesh.json); lưu trên máy tại %AppData%\VietConst\ThuVien3D (không gửi đi đâu); **Đặt vào mặt bằng (bắt điểm)** – rê chuột, tự bám mặt tường / góc phòng / giao trục (mục Đặt đồ đạc bên dưới).
- **Kiểm tra va chạm**: giữa các bộ môn – **Cứng** (giao nhau), **Ống lồng** (ống xuyên dầm / tường – cần ống lồng), **Khe hở** (gần hơn khe hở yêu cầu). Nháy đúp một dòng để xem vị trí trên mặt bằng. Không báo nút khung (dầm–cột), cửa với tường chủ.
- **Trao đổi**: **Xuất IFC…** (IFC2X3 – Revit: File → Open → IFC; SketchUp Pro: File → Import → IFC), **Xuất SketchUp…** (COLLADA .dae, nhóm theo bộ môn / loại), **Xuất OBJ…** (+ MTL), chọn bộ môn cần xuất. **Nhập IFC…** đọc cột, dầm, sàn (sàn nền), tường, cửa đi / cửa sổ, phòng. Add-in Revit (schema 2.0) xuất thêm tường (lớp cấu tạo), cửa, phòng, ống – thiết bị điện nước; **Cập nhật từ Revit…** so khớp theo UniqueId: thêm / sửa / xoá tường và thiết bị điện nước đã đổi trong Revit.
- **Hồ sơ theo bộ môn…**: chọn bộ môn – Kiến trúc (KT-01…: mặt bằng các tầng có tường, cửa, tên + diện tích phòng, đồ đạc; thống kê cửa; thống kê phòng), Kết cấu (KC-…: bộ bản vẽ kết cấu), Điện nước (DN-…); trên mặt bằng, bộ môn khác hiện nét xám làm nền đối chiếu.

> **Mẹo:** Ở bộ môn Điện nước, phần kiến trúc là nền tham chiếu: sửa tường / cửa ở bộ môn Kiến trúc rồi chạy **Kiểm tra va chạm** để thấy thiết bị, ống bị ảnh hưởng.

### 6.10. Chi tiết kiến trúc kiểu Revit: mái, trần, lan can, phào chỉ, ốp – sơn, chép tầng, nhập mô hình SketchUp

Nhóm **BIM → Hoàn thiện KT** (cũng có ở menu BIM). Mọi phần tử hiện trên mặt bằng, 3D, IFC / SketchUp / OBJ, bảng khối lượng; có hoàn tác Ctrl+Z.

- **Mái…**: mái bằng, 1 dốc, 2 dốc (tự có 2 tường thu hồi 220), 4 dốc (mặt bằng vuông → mái chóp); độ dốc %, đua mái, chân mái, đường bao (mặc định = hộp bao ô sàn tầng). Tuỳ chọn **dồn tải lớp mái** lên các ô sàn trong đường bao khi phân tích (tôn ~0,3, ngói ~0,9 kN/m² mặt bằng).
- **Trần theo phòng…**: một trần cho mỗi phòng của tầng (cao độ, loại trần) – cần **Tìm phòng** trước.
- **Lan can**: chọn loại (inox tay vịn gỗ, kính cường lực, sắt hoa văn, xây 110) và chiều cao, bấm các điểm trên mặt bằng, **Enter** kết thúc, **Backspace** bỏ điểm cuối, **Esc** dừng. Khi phân tích, tải lan can (0,3 / 0,5 / 0,4 kN/m; xây ≈ 2,52·H kN/m) tự dồn lên dầm đỡ bên dưới, phần không có dầm → tải phân bố lên ô sàn.
- **Phào chỉ…**: mẫu phào chân tường, gờ chỉ ngang tầng, gờ bậu cửa sổ, gờ chắn nước, phào trần, chỉ đỉnh tường chắn mái; bấm sát **mặt tường** cần đặt (phía bấm = mặt tường) hoặc đặt cho mọi tường biên (mặt ngoài) / mọi tường (mặt trong). Phào tự cắt qua ô cửa.
- **Ốp / sơn tường…**: ốp đá granite, gạch thẻ, gạch men WC, gỗ, sơn màu nhấn trong khoảng cao độ chọn; diện tích tự trừ ô cửa.
- **Chép KT sang tầng…**: tường (kèm cửa, phào, ốp), phòng, trần, lan can, đồ đạc của tầng đang xem sang các tầng khác (Revit: Copy to Levels); tường trùng vị trí được bỏ qua.
- **KL hoàn thiện KT…**: bảng khối lượng mái (m² nghiêng, tường thu hồi), trần (m²), lan can (md), phào chỉ (md), ốp / sơn (m²) theo tầng – xuất CSV.
- **Nhập mô hình SketchUp / Revit…**: mô hình 3D OBJ / DAE / *.vxtmesh.json → chương trình tự tìm cao độ sàn, cắt ngang mỗi tầng ở +1,2 m lấy tường, cột (vòng kín ≤ 0,8 m), cửa đi / cửa sổ (khe hở trên tường), ghi bản vẽ DXF rồi mở bảng **Nhập bản vẽ kiến trúc** để nhận diện và triển khai kết cấu (cột, dầm, sàn, tải) như với bản vẽ CAD. SketchUp xuất OBJ thường "Y lên" – để **Tự nhận** hoặc chọn trục đứng.

- **Mái nhà chữ L, T, U**: chọn đường bao "Theo đường bao ô sàn" – mái chia theo khối, mái cánh chạy vào mái chính tới đường nóc (giao nóc như mái thật); tải mái chỉ dồn lên ô sàn nằm trong đường bao.
- **Lan can – tải ngang** tay vịn theo TCVN 2737: nhà ở 0,3; công trình công cộng 0,8; khán đài 1,5 kN/m (hoạt tải HT, hướng ra ngoài) dồn lên dầm đỡ cùng tải đứng.
- **Họ đồ đạc tham số** (Thư viện 3D, nguồn "Họ tham số"): bàn ăn, bàn trà, bàn làm việc, ghế, giường đôi / đơn, tủ quần áo, sofa, kệ tivi, tủ bếp dưới / trên, bàn thờ, bồn cầu, lavabo, bồn tắm – chọn đồ đạc trên mặt bằng, bấm **E** (hoặc Enter) sửa Rộng × Sâu × Cao, cao độ, góc xoay: chân bàn, đầu giường, tay sofa… tự dựng lại.
- **Nhập mô hình**: nhận thêm vùng sàn, ban công, lỗ sàn (layer SAN), cầu thang (cụm mặt bậc, layer THANG) và mái dốc từ các mặt nghiêng (độ dốc, 1 / 2 / 4 dốc) – sau khi triển khai kết cấu, mái được thêm theo đường bao sàn tầng trên cùng.
- **Plugin SketchUp…**: cài VietConst_SketchUp.rbz (thư mục SketchUp trong thư mục cài) bằng Window → Extension Manager → Install Extension; Extensions → Việt Const → Xuất… ghi *.vxtmesh.json (m, Z lên, theo tag) để nhập trực tiếp, không phải chọn đơn vị / trục.

> **Mẹo:** Dựng nhanh mô hình khối trong SketchUp (sàn, tường, cột, ô cửa) → Export OBJ → Nhập mô hình → kiểm tra mặt bằng nhận diện → triển khai kết cấu → chạy phân tích, thiết kế BTCT.

### 6.11. Đặt đồ đạc bằng bắt điểm, kích thước tạm, thao tác 3D, mặt cắt tầng và mặt cắt công trình

Như Revit: **không nhập toạ độ X, Y**. Chọn đồ đạc / thiết bị rồi rê chuột trên mặt bằng – chương trình tự nhận diện vị trí và hiện **kích thước tạm** màu xanh; bấm vào con số để nhập khoảng cách chính xác.

1. Tab **Đặt & xem → Đặt đồ đạc…** (hoặc BIM → Thư viện 3D… → **Đặt vào mặt bằng (bắt điểm)**): chọn mục thư viện.
2. Rê chuột: bóng đồ đạc tự **bám mặt tường** (lưng tựa mặt tường hoàn thiện, khe 10 mm, tự xoay theo phương tường), **góc phòng** (đầu tường / mặt tường vuông góc), **giao trục**, **cạnh đồ đạc** khác. **Space** xoay 90°. Bấm để đặt, bấm tiếp để đặt thêm, **Esc** dừng.
3. Chọn đồ đạc trên mặt bằng: kích thước tạm tới mặt tường / trục / đồ đạc gần nhất ở 4 phía – **bấm số → nhập mm** → đồ đạc dời đúng khoảng cách (Ctrl+Z hoàn tác). **R / Space** xoay, **Delete** xoá.
4. Môi trường Điện nước: khi đặt thiết bị hiện bóng trên mặt tường gần nhất + kích thước tạm tới đầu tường / ô cửa; chọn thiết bị gắn tường → bấm số kích thước tạm để sửa vị trí.

- **3D**: bấm chọn đồ đạc / thiết bị điện nước; **kéo** đồ đạc đang chọn để dời trên mặt sàn (vẫn bắt mặt tường); thanh nổi: **Xoay 90°**, **Cao độ…** (đáy đồ đạc / chiều cao lắp thiết bị), **Xoá**, **Ẩn**, **Cô lập**, **Hiện tất cả**, **Hộp cắt** (chỉ hiện tới tầng đang xem). Bộ môn tắt hiển thị cũng ẩn trong 3D.
- **Mặt cắt ngang tầng**: Trình duyệt dự án → *Mặt cắt ngang tầng (theo bộ môn)* – mỗi cao độ (nền tầng 1, sàn tầng 2…, mái) có một khung nhìn KT / KC / DN (mặt phẳng cắt cao 1,2 m), tự sinh lại khi chèn / xoá tầng.
- **Mặt cắt công trình**: **1-1** (theo phương X, qua giữa nhịp) và **2-2** (theo phương Y) tự sinh – tường cắt trừ ô cửa, dầm, sàn, cột tô đặc; tự ghi **cao độ** các mức (±0.000, +3.600…), **chuỗi kích thước chiều cao tầng + tổng**, **trục và kích thước trục**. **Đặt & xem → Mặt cắt**: bấm 2 điểm trên mặt bằng để thêm mặt cắt (3-3, 4-4…).
- Hồ sơ Kiến trúc (Hồ sơ theo bộ môn) có thêm các tờ **MẶT CẮT 1-1, 2-2…**; khung nhìn mặt cắt có nút **Xuất DXF…**.

> **Mẹo:** Kích thước tạm đo tới **mặt tường hoàn thiện** (kể cả lớp trát / ốp) – đúng cách đo khi thi công lắp đồ.

### 6.12. Liên kết bản vẽ AutoCAD (DWG/DXF) làm nền mặt bằng

Như Xref / "Import DXF as background" của ETABS: bản vẽ kiến trúc được gắn vào dự án làm **nền mờ** cho mặt bằng từng tầng, bắt điểm vào nét bản vẽ, tự cập nhật khi bản vẽ được lưu trong AutoCAD.

1. **Tệp → Liên kết bản vẽ AutoCAD (DWG/DXF)…**: cửa sổ **Bản vẽ liên kết (AutoCAD DWG/DXF)** (không chặn) → **Thêm bản vẽ…**.
2. Thuộc tính từng bản vẽ: tầng, điểm gốc, điểm chèn, góc xoay, **Tỷ lệ ×**, đơn vị, độ đậm, màu, chữ, bắt điểm, tự cập nhật. **Căn theo 2 điểm** để khớp bản vẽ với lưới trục; **Đặt lại vị trí**; **Phóng tới bản vẽ**, **Vừa mô hình + bản vẽ**.
3. Bảng layer của bản vẽ: ẩn / hiện (**Ẩn layer đã chọn**, **Chỉ hiện layer đã chọn**, **Hiện tất cả**) và gán nhóm cấu kiện.
4. Bật **Bắt điểm bản vẽ link (đầu nét, trung điểm, giao nét, tâm)** – dùng cho ＋ Dầm, công cụ dầm kiểu ETABS, tường.
5. Sửa bản vẽ trong AutoCAD (**Mở bằng AutoCAD**) và lưu: chương trình tự đọc lại nền, báo "Bản vẽ link đã thay đổi – đã cập nhật" kèm nút **Cập nhật mô hình từ link…**.
6. **Cập nhật mô hình từ link…**: danh sách khác biệt bản vẽ ↔ mô hình của tầng (thêm / dời / không còn trên bản vẽ) – xanh = thêm, cam = dời, đỏ = xoá; mục xoá và dời cột mặc định không đánh dấu; **Chọn mọi mục thêm**, **Chọn tất cả**, **Bỏ chọn tất cả** → **Áp dụng** (hoàn tác được).

- Đường dẫn bản vẽ lưu tương đối theo file dự án; không thấy file khi mở dự án → thông báo chọn lại (**Chọn lại file…**). **Đọc lại**, **Mở thư mục**, **Bỏ liên kết**.
- Add-in AutoCAD gửi dữ liệu có đường dẫn DWG → chương trình đề nghị tạo / làm mới liên kết cho bản vẽ đó.

### 6.13. Từ Revit (JSON)

1. Trong Revit: Add-Ins → External Tools → **Xuất sang Việt Const**, chọn nơi lưu file *.vxtrevit.json. Dữ liệu xuất: Levels, Grids (đường thẳng), Structural Columns, Structural Framing, Floors.
2. Trong Việt Const: **Tệp → Nhập từ Revit (JSON)...** (nút **Nhập Revit**), chọn file JSON.
3. Xác nhận thay thế mô hình hiện tại (giữ lại vật liệu, tiết diện, trường hợp tải, thông số gió/động đất). Gốc toạ độ tự dời về giao trục đầu tiên.

### 6.14. Từ ETABS / SAP2000 (.e2k / .s2k)

**Tệp → Nhập mô hình ETABS/SAP (.e2k/.s2k)…** đọc file mô hình văn bản ETABS (.e2k, .$et) hoặc SAP2000 (.s2k, .$2k): nút, thanh, tiết diện, vật liệu, tầng, tải trọng. Sau khi nhập nên kiểm tra tiết diện, tầng và tải trên mặt bằng, chạy phân tích và so sánh với kết quả ETABS (*mục 9.8 (So sánh với ETABS / SAP2000 / SAFE)*).

### 6.15. Mở, lưu dự án, dự án mẫu

- **Tệp → Mở .kcvxt...** (Ctrl+O), **Tệp → Lưu** (Ctrl+S), **Tệp → Lưu thành...**. File dự án **.kcvxt** là JSON (UTF-8) – có thể sinh bằng công cụ khác (Python, Dynamo, Grasshopper) rồi mở bằng chương trình.
- Khi đã liên kết file, bấm đúp file .kcvxt trong Explorer để mở; dòng lệnh: KetCauVXT.exe "đường dẫn\file.kcvxt".
- Mở dự án trích dẫn văn bản đã hết hiệu lực: thanh trạng thái cảnh báo (không chặn) – xem *mục 13.2 (Cập nhật danh mục)*.
- Dự án mẫu: menu Start → Việt Const → Dự án mẫu (NhaPho.kcvxt, TuBanVeKienTruc.kcvxt).

## 7. Lập mô hình kết cấu

### 7.1. Lưới trục và tầng

Lưới trục và tầng được tạo ở tab Công trình (*mục 6.1 (Dự án mới từ lưới trục (tab Công trình))*) hoặc từ bản vẽ/Revit/ETABS. Ô **Tầng đang xem** (trên khung nhìn) chọn tầng hiển thị và tầng áp dụng của các công cụ sửa.

- Muốn đổi bước cột / số tầng của mô hình đơn giản: sửa ở tab Công trình rồi **Tạo mô hình** lại (mô hình bị sinh lại).
- Với mô hình đã sửa nhiều, thêm dầm theo trục + khoảng cách bằng **Dầm theo toạ độ / trục…** thay vì sinh lại (*mục 7.10 (Công cụ dầm kiểu ETABS)*).
- Bắt điểm **Giao trục**, **Trên trục** dùng lưới trục hiện có.

### 7.2. Quét chọn mọi đối tượng (khung bao / khung cắt)

Ở chế độ **Chọn** của tab Mặt bằng, kéo chuột trái trên vùng trống để quét chọn – áp dụng cho cột, dầm, ô sàn, tường, cửa, đồ đạc, thiết bị và tuyến điện nước, trục, đường mặt cắt của tầng đang xem (mọi tầng khi xem tất cả).

| Thao tác | Kết quả |
|---|---|
| Kéo trái → phải (khung xanh dương, nét liền) | Khung bao: chỉ chọn đối tượng nằm trọn trong khung. |
| Kéo phải → trái (khung xanh lá, nét đứt) | Khung cắt: chọn mọi đối tượng chạm khung. |
| Ctrl + kéo | Thêm vào nhóm đang chọn. |
| Shift + kéo | Bỏ bớt khỏi nhóm đang chọn. |
| Delete | Xoá cả nhóm – một bước hoàn tác (Ctrl + Z). |
| Esc | Bỏ chọn. |

- Thanh trạng thái báo số đối tượng theo loại, vd. "Đã chọn: 5 dầm, 3 cột, 2 tường".
- Tab **Trục & tầng** → **Bộ lọc…**: đánh dấu các loại cần giữ (kiểu Revit), bỏ các loại còn lại khỏi nhóm.
- Cột, dầm, ô sàn trong nhóm dùng chung cho các lệnh sửa nhiều cấu kiện (gán tải, tiết diện, sao chép…).
- Dời khung nhìn bằng nút giữa chuột (kéo trái ở chế độ Chọn dành cho quét chọn).

- **Quét chọn trong khung nhìn 3D**: tích ô **Quét chọn (kéo khung)** trên thanh nổi của tab 3D rồi kéo chuột trái – trái → phải là khung bao (chọn khối nằm trọn khung), phải → trái là khung cắt (chọn khối chạm khung); Ctrl + kéo thêm, Shift + kéo bớt. Nhóm chọn dùng chung với mặt bằng: thanh trạng thái báo số đối tượng, **Delete** xoá cả nhóm (một bước hoàn tác), **Esc** bỏ chọn, bảng thuộc tính sửa được nhóm như trên mặt bằng. Bỏ tích ô để kéo chuột xoay mô hình như cũ.

> **Mẹo:** Trong cửa sổ Triển khai hồ sơ, tab Mặt bằng kết cấu, công cụ **Chọn** cũng quét chọn được nhiều vị trí cấu kiện; Shift + bấm thêm / bớt từng vị trí, Delete xoá khỏi mặt bằng.

### 7.3. Thêm, dời, xoá lưới trục

1. Tab **Trục & tầng** → **Vẽ trục**: bấm điểm đầu rồi điểm cuối trên mặt bằng. Đường gần đứng tạo trục số (1, 2, 3…), đường gần ngang tạo trục chữ (A, B, C…); tên tự đặt tiếp theo trục có sẵn. Bấm tiếp để vẽ trục khác, Esc để dừng.
2. **Sửa lưới trục…**: chọn trục trong danh sách để đổi tên, **Dời tới toạ độ** (đánh dấu *Kéo theo cấu kiện* để cột, đầu dầm, góc sàn, đầu tường nằm trên trục dời cùng), **Sao chép lệch** một khoảng, thêm trục X / Y theo toạ độ, **Xoá trục** (có cảnh báo khi còn cấu kiện nằm trên trục – cấu kiện giữ nguyên), **Đánh số lại**.
3. Bấm đúp gần đầu trục (bong bóng) trên mặt bằng để đổi tên nhanh.
4. Mọi thay đổi trong một lần mở hộp thoại là một bước hoàn tác.

- Có thể quét chọn trục rồi nhấn Delete để xoá nhiều trục (*mục 7.2 (Quét chọn mọi đối tượng (khung bao / khung cắt))*).
- Triển khai hồ sơ: nút **Thêm trục…** trên mặt bằng kết cấu; chuột phải lên đường trục → dời trục (kéo theo / giữ cấu kiện), thêm trục song song, xoá trục. Ô bước trục tự cập nhật.
- **Kéo trục bằng chuột**: ở chế độ **Chọn** (bật nắm kéo), bấm giữ bong bóng trục rồi kéo – đường trục mới hiện nét đứt kèm **kích thước tạm** (mm) tới trục kề hai bên, vị trí bắt bước 10 mm. Thả chuột để dời trục; cột, đầu dầm, góc sàn, đầu tường nằm trên trục dời theo. Giữ **Ctrl** khi thả để chỉ dời trục. Mỗi lần kéo là một bước hoàn tác.
- **Bong bóng trục**: chuột phải lên bong bóng → *Cả hai đầu*, *Chỉ đầu dưới / trái*, *Chỉ đầu trên / phải* hoặc *Không bong bóng*.
- **Ẩn trục theo tầng**: khi đang xem một tầng, chuột phải lên bong bóng → **Ẩn trục … trên tầng …**; trục vẫn hiện ở các tầng khác và khi xem mọi tầng. **Hiện lại mọi trục đã ẩn** (trên tầng này) trả lại. Thiết lập bong bóng và ẩn trục lưu trong dự án, áp dụng cho mặt bằng trên màn hình và bản vẽ mặt bằng kết cấu xuất DXF / DWG.

> **Lưu ý:** Sau khi dời trục có kéo theo cấu kiện, chạy lại phân tích để cập nhật nội lực.

### 7.4. Thêm tầng, sửa cao độ tầng

1. Tab **Trục & tầng** → **Thêm tầng…**: chọn tầng tham chiếu, vị trí (phía trên / phía dưới), chiều cao tầng mới và cách chép cấu kiện: *Không chép*, *Chỉ chép cột* hoặc *Chép toàn bộ* (cột, dầm, sàn). Các tầng phía trên được nâng lên tương ứng; thêm tầng trên cùng sẽ nối cột từ đỉnh cột tầng dưới.
2. **Sửa cao độ…**: chọn tầng, nhập cao độ mới rồi **Đặt cao độ** – nút ở cao độ cũ dời theo nên cột, dầm, sàn co giãn, chiều cao các tầng tự tính lại. Cao độ được giới hạn để mỗi tầng cao ít nhất 0,5 m.
3. Cũng trong hộp đó: **Đổi tên** tầng, **Xoá tầng**.

Các nút **Thêm tầng…** và **Sửa cao độ…** cũng có trên khung nhìn mặt đứng (*mục 7.5 (Mặt đứng công trình và vẽ trục trên mặt đứng)*).

### 7.5. Mặt đứng công trình và vẽ trục trên mặt đứng

Tab **Trục & tầng** → **Mặt đứng** (hoặc cây dự án → Mặt đứng) mở khung nhìn mặt đứng 4 hướng: trước, sau, trái, phải. Mặt đứng thể hiện đường cao độ các tầng kèm ký hiệu cao độ, trục thẳng đứng có bong bóng ở trên và dưới, dãy kích thước trục, hình chiếu tường, cột, dầm, cửa đi, cửa sổ.

1. Chọn hướng nhìn trong danh sách.
2. Bật **Thêm trục (bấm lên hình)** rồi bấm vào vị trí cần thêm trục: chương trình đổi vị trí bấm sang toạ độ mặt bằng, hiện để sửa cho chính xác, rồi thêm trục số (mặt đứng trước / sau) hoặc trục chữ (mặt đứng trái / phải).
3. **Kéo đường cao độ**: khi ô Thêm trục đang tắt, bấm giữ gần đường cao độ một tầng (trong khoảng 0,3 m) rồi kéo lên / xuống – dòng thông tin hiện cao độ mới (bắt 10 mm) và chiều cao tầng; thả chuột để đặt. Cột, dầm, sàn co giãn theo, mỗi tầng vẫn cao ít nhất 0,5 m; một lần kéo là một bước hoàn tác.
4. **Xuất DXF…** lưu bản vẽ mặt đứng đang xem.

- Bộ hồ sơ kiến trúc (KT) có thêm 4 bản vẽ mặt đứng KT_MatDung_1…4.dxf.
- Lưới trục và cao độ dùng chung với mặt bằng (*mục 7.3 (Thêm, dời, xoá lưới trục)*, *mục 7.4 (Thêm tầng, sửa cao độ tầng)*).

### 7.6. Vật liệu

- Tab Công trình → nhóm **Vật liệu**: cấp bê tông cột, dầm, sàn, móng; thép dọc (Ø ≥ 10) và thép đai, thép sàn (Ø < 10). Bấm **Áp dụng thông số** sau khi đổi.
- **Khai báo → Vật liệu…**: hộp thoại **Khai báo vật liệu (Material Properties)** – các bảng **Bê tông**, **Cốt thép**, **Thép hình** (E, γ, Rb, Rbt, Rs…); **＋ Thêm**, **Xoá (không dùng)**, **Đặt lại theo thư viện**. Giá trị tính toán theo TCVN 5574:2018 (bê tông Bảng 7, 10; cốt thép Bảng 13) và TCVN 5575:2024 (thép hình f = fy/γM).
- Bảng tra cường độ đầy đủ: **Trợ giúp → Phụ lục tính toán & hướng dẫn…**.

### 7.7. Tiết diện dầm, cột, sàn

Có hai nơi khai báo tiết diện:

- Tab **Tiết diện**: **+ Chữ nhật BTCT**, **+ Chữ I tổ hợp**, **Xoá**; bảng sửa trực tiếp kích thước (mm). Chữ nhật: b (song song trục 3) × h (song song trục 2). Chữ I: h1 đầu I, h2 đầu J (vát), bf × tf cánh, tw bụng. Phần dưới: **Tiết diện sàn** (+ Thêm, Xoá, chiều dày).
- **Khai báo → Tiết diện dầm/cột…** (hoặc nút **Khai báo tiết diện…** trên hàng công cụ dầm) – cửa sổ **Khai báo tiết diện dầm / cột (Frame Sections)** như Define → Frame Sections của ETABS, không chặn cửa sổ chính.

| Nút / cột | Ý nghĩa |
|---|---|
| ＋ Dầm chữ nhật… / ＋ Cột chữ nhật… | Thêm tiết diện mới (tên tự động dạng D22x40, C22x30). |
| Sao chép, Đổi tên…, Tên tự động, Màu… | Nhân bản, đổi tên (tên mới áp cho mọi thanh đang dùng), đặt tên theo kích thước, màu hiển thị. |
| Xoá… | Tiết diện đang dùng chỉ xoá được khi gán lại các thanh sang tiết diện khác. |
| Nhập từ dự án… | Lấy tiết diện từ một file .kcvxt khác. |
| Hệ số I33, I22, J, A | Hệ số điều chỉnh độ cứng như Property Modifiers của ETABS (vd. dầm I33 = 0,35, cột 0,7 khi phân tích kể nứt). |
| Dùng làm tiết diện hiện hành (bấm đúp) | Tiết diện dùng cho ＋ Dầm, Dầm phụ, Công xôn, Gán tiết diện (ô **Tiết diện dầm**). |
| Tự đổi tên theo kích thước khi sửa b, h | Bật thì sửa b, h sẽ đổi tên tương ứng. |

> **Lưu ý:** Tiết diện dầm khai báo là chữ nhật b×h. Cánh sàn chữ T / L chỉ được vẽ trên mặt cắt dầm (lấy theo ô sàn thực tế hai bên dầm); tính toán vẫn theo chữ nhật b×h – xem *mục 7.36 (Dầm có cánh sàn (chữ T / L) trên bản vẽ)*.

### 7.8. Cột

- Cột được tạo tại mọi giao trục khi **Tạo mô hình**, hoặc theo bản vẽ kiến trúc (bảng **Cột** của hộp thoại nhập: vị trí, Bx, By, tầng lên đến), bản vẽ kết cấu, Revit, ETABS.
- Đổi tiết diện cột: chọn cột trên mặt bằng, xem thông tin ở ô dưới; đổi tiết diện trong bảng tiết diện hoặc **Khai báo → Tiết diện dầm/cột…**.
- Xoá cột: chế độ **Xoá**, bấm vào cột (chương trình hỏi lại).
- Liên kết chân cột: tab Công trình → **Liên kết chân cột** (Ngàm / Khớp).

> **Lưu ý:** Phiên bản hiện tại chưa có công cụ vẽ thêm cột bằng chuột (menu Vẽ chưa có mục cột): thêm cột bằng cách tạo lại khung, nhập bản vẽ / Revit / ETABS.

### 7.9. Vẽ dầm bằng 2 điểm (＋ Dầm)

1. Chọn tiết diện ở ô **Tiết diện dầm** (hoặc bấm đúp tiết diện trong cửa sổ khai báo).
2. Bật **＋ Dầm**, bấm điểm đầu rồi điểm cuối. Chương trình bắt đầu dầm, nút, giao trục, giao điểm, trung điểm, vuông góc, điểm gần nhất, điểm trên trục (bật/tắt ở nhóm **Bắt điểm**; tắt ô **ETABS** = bắt nút/giao trục/lưới 50 mm như cũ).
3. Bấm tiếp để vẽ nối; **chuột phải** kết thúc chuỗi, chuột phải lần nữa hoặc **Esc** thoát công cụ.
4. Gõ số trên mặt bằng để mở ô nhập toạ độ: "x,y" tuyệt đối, "@dx,dy" tương đối điểm trước, "L<góc" toạ độ cực, "L" chiều dài theo hướng chuột; đơn vị theo ô **Gõ toạ độ** (mm / m) hoặc hậu tố mm, cm, m.

Dầm mới được chia tại giao điểm với dầm có sẵn; ô sàn bị chia lại tương ứng. Phạm vi tầng áp dụng theo ô **Phạm vi tầng** (*mục 7.10 (Công cụ dầm kiểu ETABS)*).

### 7.10. Công cụ dầm kiểu ETABS

Hàng công cụ **Dầm (kiểu ETABS):** dưới thanh sửa mặt bằng:

| Công cụ | Cách dùng |
|---|---|
| Vẽ nhanh theo lưới | Bấm vào đoạn trục giữa hai giao trục để đặt dầm; kéo khung (trái → phải: đoạn nằm trọn trong khung; phải → trái: đoạn chạm khung) để đặt nhiều dầm. Đoạn đã có dầm được bỏ qua. Shift + kéo hoặc chuột giữa để di chuyển. |
| Dầm phụ | Bấm vào trong một ô (kín bởi dầm hoặc ô lưới) → chọn phương (song song trục X / Y), **Chia đều – số dầm n**, **Cách đều – khoảng cách s (mm)** (kèm khoảng đầu) hoặc **Danh sách khoảng cách tới mốc (mm)**; **Mốc đo (từ trục)**, **Đo từ mép cuối ô**, tiết diện, **Chia lại ô sàn theo dầm phụ** (tải sàn truyền vào dầm phụ như ETABS). |
| Công xôn | Bấm điểm gốc (nút, đầu dầm, giao trục, điểm trên dầm) rồi nhập độ vươn và hướng (ban công, ô văng). |
| Dầm theo toạ độ / trục… | Tab **Theo trục + khoảng cách** (vd. trục 2 + 1200 từ A đến C), **Toạ độ tuyệt đối / tương đối**, **Dầm công xôn**. |
| Chọn nhiều | Bấm để thêm/bớt dầm; kéo khung trái → phải chọn dầm nằm trọn, phải → trái chọn dầm chạm khung (giữ Ctrl để bỏ chọn); Delete xoá. |
| Gán tiết diện | Gán tiết diện hiện hành cho các dầm đã chọn. |
| Điểm chèn… | Điểm chèn (cardinal point) và dời ngang tiết diện – vd. mép dầm trùng trục / mép tường. |
| Sao chép / Đối xứng… | Tab **Tịnh tiến** (Δx, Δy, n bản), **Đối xứng qua trục**, **Sang tầng khác** (Replicate). |
| Phạm vi tầng | Tầng hiện tại / Các tầng tương tự / Tất cả tầng / Tầng đánh dấu (nút **Chọn tầng…**) – áp dụng cho mọi công cụ vẽ dầm (như One Story / Similar Stories / All Stories của ETABS). |
| Bắt điểm | ETABS (bật/tắt chung), Nút, Giao trục, Giao, Giữa, Vuông góc, Gần, Trên trục. |
| Gõ toạ độ | Đơn vị số khi gõ toạ độ (mm / m). |

> **Mẹo:** Mọi thao tác hoàn tác được bằng Ctrl+Z. Thanh trạng thái luôn ghi bước tiếp theo của công cụ đang bật.

### 7.11. Dầm cong

- **＋ Dầm cong**: bấm điểm đầu, điểm cuối rồi một điểm bất kỳ trên cung (dây chun hình cung ghi R và chiều dài cung). Dầm thẳng bị cung cắt qua được chia tại giao điểm.
- **Uốn cong** dầm có sẵn: chọn dầm, chọn **Độ vồng f** hoặc **Bán kính R**, nhập giá trị (m; 0 = nắn thẳng; số âm = vồng về phía phải hướng I→J) rồi bấm **Uốn cong**.
- Phân tích chia dầm cong thành các đoạn thẳng ≤ 10°, tải theo chiều dài cung; thiết kế kể **xoắn** (*mục 10.8 (Dầm cong – xoắn)*).
- Sàn kề dầm cong lấy theo dây cung, tải dồn lên dây cung được rải lại lên cung; phần cung vồng ra ngoài ô sàn được cộng tải viên phân.

### 7.12. Sàn và cấu tạo sàn

- **＋ Ô sàn**: bấm vào ô kín bởi dầm; công năng theo ô **Công năng** (Phòng ở, Vệ sinh, Ban công, Hành lang, Kho, Mái) quyết định hoạt tải và hoàn thiện.
- Chiều dày sàn: tiết diện sàn (tab Tiết diện → Tiết diện sàn; mặc định S100, S120).
- **Khai báo → Cấu tạo sàn…**: thư viện **Cấu tạo sàn** – các lớp (dày, trọng lượng riêng, tải cố định), hoạt tải tiêu chuẩn; tải hoàn thiện = Σ(dày × γ + tải cố định); không khai báo bản BTCT (trọng lượng bản thân tính theo tiết diện sàn). Nút **Gán cho ô sàn đang chọn**.
- Chế độ **Gán cấu tạo**: chọn cấu tạo ở ô **Cấu tạo sàn** rồi bấm các ô sàn.
- Phương án thép sàn (thép mũ + lưới dưới, lưới 2 lớp, lưới 2 lớp + mũ tăng cường): *mục 10.4 (Sàn – phương án thép sàn)*.

### 7.13. Ban công công xôn

1. Nhập **Vươn (m)** (0,5 – 3,0 m) và **Lan can (kN/m)** trên thanh công cụ.
2. Bật **＋ Ban công**, bấm vào dầm biên ngoài nhà.
3. Chương trình thêm dầm công xôn tại các cột, dầm biên đầu công xôn, ô sàn ban công (hoạt tải 2,0 kN/m²) và tải lan can trên dầm biên.

> **Mẹo:** Ban công không có cột đỡ ở giữa: dùng công cụ **Công xôn** để đặt từng dầm công xôn tại vị trí mong muốn.

### 7.14. Cầu thang

1. Bật **＋ Cầu thang**, bấm vào ô sàn / ô kín bởi dầm: sàn được bỏ, ký hiệu cầu thang vẽ trong ô; tải bản thang (6,5 kN/m²) và hoạt tải (3,0 kN/m², nửa vế) truyền vào dầm viền.
2. Chọn ô cầu thang rồi bấm **Khai báo cầu thang…**: phác thảo trực tiếp mặt bằng và mặt cắt vế 1, kiểm tra 2h + b.
3. **Hình học**: tên, dạng thang, phương vế (tự động theo cạnh dài ô / theo trục X / Y), chân thang ở phía toạ độ lớn, tổng số bậc (0 = chiều cao tầng / ~170 mm), số bậc vế 1, bề rộng bậc b (0,63 − 2h trong 260…300 mm), bề rộng vế, khe giữa 2 vế, chiều dày bản (0 = L/30 ≥ 100), chiều sâu và chiều dày chiếu nghỉ.
4. **Sơ đồ chịu lực**: bản thang tựa dầm chiếu nghỉ / dầm sàn (bản chịu lực) hoặc có dầm cốn (limon, 1 hoặc 2 bên); dầm chiếu nghỉ; tuỳ chọn gối ngàm một phần (M = qL²/10).
5. **Bậc thang, hoàn thiện, tải**: bậc xây gạch / BTCT, ốp mặt bậc (đá granite, gạch ốp, gỗ…), tải trọng → Áp dụng (hoàn tác được).

Kết quả thiết kế ở tab Thiết kế → Cầu thang, bản vẽ KC-08 (*mục 10.7 (Cầu thang)*).

### 7.15. Giếng trời, thông tầng, sàn vệ sinh

- **＋ Giếng trời**: bấm vào ô sàn → bỏ sàn (ô thông tầng), lan can 0,5 kN/m quanh lỗ.
- **Sàn vệ sinh**: bấm vào ô sàn → hoàn thiện 2,0 kN/m² (chống thấm, hạ cốt); bấm lại để trả về phòng ở.
- Xoá lỗ sàn (chế độ **Xoá**) sẽ đặt lại sàn.

### 7.16. Kéo, trim, extend dầm; pick đối tượng CAD

Sửa dầm như AutoCAD – không cần nhớ kích thước: các nút ở tab **Dầm** của thanh lệnh trên mặt bằng, gõ lệnh ở dòng lệnh (ST, M, TR, EX) hoặc menu **Vẽ**. Mọi thao tác hoàn tác được (Ctrl+Z); nút giao được chia lại để mô hình chạy phân tích ngay.

1. **Kéo dầm** (**Vẽ → Kéo đầu dầm / kéo dầm**): bấm gần ĐẦU dầm cần kéo → bấm điểm mới (bắt nút, giao trục, điểm trên dầm, nét bản vẽ link). Đầu dầm đang nối cột / dầm khác được tách sang nút mới, không kéo theo cấu kiện khác. Giữ **Shift** khi bấm dầm để dời cả dầm.
2. **Dời dầm** (**Vẽ → Dời dầm song song**): bấm dầm tại điểm gốc (hoặc dùng các dầm đã *Chọn nhiều*) → bấm điểm đích; dầm dời song song.
3. **Trim** (**Vẽ → Trim dầm (cắt tại dầm giao)**): bấm vào đoạn dầm cần bỏ – phần giữa hai dầm / cột giao gần nhất, hoặc phần nhô quá dầm giao, bị cắt; đoạn còn lại giữ tiết diện và tải.
4. **Extend** (**Vẽ → Extend dầm (kéo dài tới dầm chọn / vuông góc)**): bấm gần đầu dầm cần kéo dài → bấm dầm đích (kể cả khi phải vượt qua dầm gần hơn), hoặc **chuột phải / Enter** để tự kéo tới dầm vuông góc gần nhất phía trước. Dầm đích được chia tại điểm nối.
5. **Nối dầm hở** (**Vẽ → Nối dầm hở vào dầm vuông góc gần nhất**): mọi đầu dầm hở của tầng đang xem tự kéo dài tới dầm vuông góc gần nhất trong **khoảng nối** (mặc định 1500 mm) – dùng sau khi vẽ nhanh theo bản vẽ kiến trúc.
6. **Pick CAD** (**Vẽ → Pick đối tượng CAD: dầm / sàn / thang / lỗ**): nạp mặt bằng kiến trúc sơ bộ bằng **Tệp → Liên kết bản vẽ AutoCAD (DWG/DXF)…**, chọn ô **tạo**: *Dầm* – bấm giữa hai mép dầm / tường song song (100…600 mm) → dầm đúng trục, bề rộng đo được (tự tạo tiết diện b×h, h theo tiết diện đang chọn); bấm sát một nét → dầm theo nét; *Sàn / Cầu thang / Giếng trời / Chỉ dầm bao* – bấm trong đa tuyến kín → dầm bao các cạnh còn thiếu + ô sàn / ô thang / lỗ.

> **Mẹo:** Vẽ nhanh theo kiến trúc: Pick CAD các dầm → **Nối dầm hở** → Pick CAD *Sàn* trong từng phòng → Pick CAD *Cầu thang* ở ô thang.

### 7.17. Thanh lệnh theo cấu kiện, chọn nhiều, gán tải, dòng lệnh kiểu AutoCAD

Trên tab **Mặt bằng**, các nút được xếp thành tab theo cấu kiện: **Lưới – Tầng | Móng | Cột | Dầm | Sàn | Tường – Tải | Link CAD**. Cụm bên trái (Chọn, Chọn nhiều, Xoá, Hoàn tác, Chọn tương tự, Bỏ chọn) dùng chung. Nháy đúp tên tab để thu gọn / mở lại thanh lệnh; F11 ẩn cả thanh lệnh.

1. **Chọn nhiều**: bấm *Chọn nhiều* (hoặc gõ CH) rồi bấm từng đối tượng hoặc kéo khung – trái → phải (khung xanh liền) chọn đối tượng nằm TRỌN trong khung, phải → trái (khung xanh lá nét đứt) chọn đối tượng CHẠM khung; giữ Ctrl để bỏ chọn. Tab đang mở quyết định loại được chọn: Dầm → dầm, Cột / Móng → cột, Sàn → ô sàn, các tab khác → mọi loại.
2. **Chọn tương tự**: cùng tiết diện (tầng đang xem / mọi tầng), cùng loại – công năng, cùng tầng.
3. **Gán tải trọng** (nhóm *Gán tải* ở tab Dầm, Sàn, Cột, Móng): chọn trường hợp tải, nhập giá trị, chọn *Thay thế / Cộng thêm / Xoá* rồi bấm **Gán** – áp cho MỌI đối tượng đang chọn, một bước hoàn tác (Ctrl+Z). Dầm: q (kN/m) và lực tập trung P tại x/L; Sàn: q (kN/m²), *Theo công năng* (hoàn thiện / hoạt tải TCVN 2737 theo từng ô), tường xây trên sàn quy đổi q = g·h·L/A; Cột: N, Mx, My tại đầu cột; Móng: N, Mx, My tại chân cột.
4. Dòng *Đang chọn* cho biết giá trị chung của các đối tượng; khác nhau thì hiện **«khác nhau»** và khi *Thay thế* chương trình hỏi trước khi ghi đè. Chỉ tải nhập tay bị thay / xoá – tải tự dồn từ sàn, gió, động đất giữ nguyên.
5. **Tải tường**: tab *Tường – Tải* tính q = g_tường · h · (1 − tỷ lệ cửa) theo loại tường / chiều cao (0 = chiều cao tầng trên − 0,35 m); nút *q tường* điền giá trị vào ô q của tab Dầm.
6. **Link CAD** (nền như xref theo tầng): *Liên kết DWG/DXF…*, *Quản lý…* (layer, dời / xoay / tỷ lệ), *Ẩn / hiện*, *Độ mờ*, *Đọc lại*, *Căn 2 điểm*, *Khoá*, *Bắt điểm CAD*, *Pick CAD*. **Tường CAD → tải dầm**: cặp nét song song cách 110 / 220 mm trên layer tường nằm trên dầm (lệch ≤ t/2 + 50 mm) → tạo tường, tải tường dồn vào dầm khi phân tích.

**Dòng lệnh** dưới mặt bằng (gõ thẳng khi đang trỏ mặt bằng): L dầm, C cột, S sàn, W tường, M dời, ST kéo, TR trim, EX extend, CO sao chép (dx,dy n), AR mảng (nx ny dx dy), O offset, MI đối xứng (tên trục), E xoá, U hoàn tác, Z / ZE zoom, DI đo khoảng cách, MA sao tiết diện, TAI gán tải, SS chọn tương tự, ? trợ giúp – gõ có dấu / không dấu đều được. Toạ độ: x,y tuyệt đối, @dx,dy tương đối, @L<góc (vd @3000<90), hoặc chỉ gõ chiều dài theo hướng chuột. **F3** bắt điểm (□ đầu, △ giữa, × giao, ⊥ vuông góc, ○ tâm nét CAD), **F8** trực giao, **F10** góc cực 45°; Enter / Space lặp lệnh, Esc huỷ, ↑↓ lịch sử lệnh; con lăn chuột phóng to tại con trỏ, chuột giữa để kéo.

> **Mẹo:** Gán tải nhanh cho cả tầng: tab Sàn → *Chọn nhiều* → kéo khung phải → trái qua toàn mặt bằng → trường hợp HT → *Theo công năng* → làm lại với HTHT.

### 7.18. Giao diện và lệnh vẽ kiểu Revit: kiểu tường nhiều lớp, cửa tự cắt tường

Chế độ Thiết kế mặc định dùng **giao diện kiểu Revit**: ribbon một hàng **Kiến trúc | Kết cấu | Tải trọng | Phân tích | Xem | Quản lý** (nút lớn, chữ dưới biểu tượng), bên trái **Thuộc tính** (bộ chọn kiểu + xem trước, thông số theo nhóm Ràng buộc / Kích thước / Kết cấu / Tải trọng) và **Trình duyệt dự án** (mặt bằng các tầng, 3D, bảng thống kê, họ & kiểu tường / cửa / tiết diện), giữa là vùng vẽ lớn, dưới mặt bằng là **thanh tuỳ chọn** của công cụ đang dùng. Các ô nhập liệu cũ (công trình, lưới trục, tầng, tải, gió, động đất, vật liệu, địa chất, kết quả) nằm ở **Thông tin dự án** (nút *Thông tin dự án ►* trên khung Thuộc tính hoặc Quản lý → Thông tin dự án). Muốn trở lại bố cục cũ: Quản lý → **Giao diện cổ điển** (menu Xem → *Giao diện kiểu Revit* để bật lại).

1. **Khai báo kiểu tường**: Kiến trúc → **Kiểu tường…** → chọn kiểu ở danh sách trái (Gạch đặc 110/220 trát 2 mặt, Gạch rỗng, Gạch AAC, Tường 220 ốp đá granite mặt ngoài, Tường 110 ốp gạch men WC, Tường BTCT 200…) hoặc *＋ Mới / Nhân bản*. Bảng lớp xếp từ **mặt ngoài** (trên) vào **mặt trong** (dưới): *Lõi kết cấu* (gạch / bê tông), *Lớp vữa trát*, *Ốp đá*, *Ốp gạch men*, *Sơn bả*, *Cách nhiệt* – chọn vật liệu (γ lấy theo thư viện *Vật liệu…*), nhập chiều dày; *＋ Chèn lớp / Xoá lớp / ▲ / ▼*. Ô xem trước vẽ mặt cắt các lớp; dòng tổng cho biết tổng dày, dày lõi và **tải 1 m² = Σ dày × γ**.
2. **Vẽ tường**: Kiến trúc → **Tường** → thanh tuỳ chọn: *Kiểu tường*, *Đường định vị* (Tim tường, Tim lõi, Mặt hoàn thiện ngoài / trong, Mặt lõi ngoài / trong), *Cao* (0 = tới tầng trên), *Chuỗi* (vẽ nối tiếp). Bấm điểm đầu – điểm cuối; khi vẽ có **kích thước tạm** (chiều dài + góc) cạnh con trỏ, gõ số + Enter để nhập chiều dài; F8 trực giao, F3 bắt điểm. Tường tự nối góc L / T gọn, tô gạch chéo theo mức chi tiết.
3. **Đặt cửa**: Kiến trúc → **Cửa đi / Cửa sổ / Ô thoáng** → chọn kiểu ở thanh tuỳ chọn (D1 900x2200, S1 1200x1400…; *Kiểu cửa…* để thêm / sửa rộng, cao, bậu, cách mở) → rê chuột lên tường: cửa xem trước bắt vào tường kèm kích thước tạm tới đầu tường / cửa bên cạnh; **Space** lật hướng mở, **Shift+Space** đổi bên bản lề; bấm để đặt. Tường **tự cắt ô cửa** (mặt bằng, khối lượng xây trát), tải tường lên dầm trừ đúng diện tích cửa; đặt sát đầu tường hoặc chồng cửa khác thì cửa tự dời vào chỗ hợp lệ gần nhất. Esc để dừng.
4. **Sửa cửa**: bấm vào cửa → tab ngữ cảnh **Sửa | Cửa** và kích thước tạm màu xanh: **bấm vào số** để nhập khoảng cách mới (như Revit); đổi kiểu ở bộ chọn kiểu; Space lật; Delete xoá (tường liền lại). Dời / xoá tường thì cửa trên tường đi theo / xoá theo.
5. **Chọn đối tượng** (tường, dầm, cột, ô sàn): khung Thuộc tính hiện thông số và tải (tường: chiều cao tính tải, diện tích cửa, q lên dầm); ribbon hiện tab **Sửa | …** với các lệnh hợp ngữ cảnh (Sửa kiểu, Đặt cửa, Di chuyển, Kéo giãn, Cắt, Kéo dài, Gán tải, Xoá). Đổi kiểu tường / tiết diện dầm – cột / sàn ngay ở bộ chọn kiểu.
6. **Gán tải**: tab Tải trọng → Tải dầm / sàn / cột / móng / tường mở thanh lệnh cấu kiện (chọn nhiều, Thay thế / Cộng thêm / Xoá); *Gợi ý lanh tô* liệt kê lanh tô đề xuất cho mọi cửa đã đặt.

- **Hiển thị / Đồ hoạ** (tab Xem): bật / tắt thẻ dầm (hoặc chỉ tên dầm), thẻ cột, thẻ sàn, công năng, thẻ tường, thẻ cửa; mức chi tiết *Thô* (tường tô đặc) / *Trung bình* / *Chi tiết* (thêm đường ranh lõi); **tự tránh chồng nhãn** – nhãn đụng nhau được dời, không còn chỗ thì ẩn.
- Kiểu tường cũ (dày, γ, trát) tự đổi sang nhiều lớp **giữ nguyên tải 1 m²**; file cũ mở bình thường. Bảng *Loại tường* cũ vẫn dùng được: sửa dày / γ chỉ đổi lớp lõi, giữ lớp ốp.
- Gợi ý lanh tô: gối mỗi bên ≥ 200 mm (≥ 250 mm khi L0 > 1,5 m), h ≥ max(L0/10; 150 mm) (≥ 200 mm với tường 110 khi L0 > 1,5 m), cửa > 1,8 m trên tường 110 cảnh báo bổ trụ BTCT – chi tiết thép ở chế độ Triển khai hồ sơ → Lanh tô.

> **Mẹo:** Làm nhanh một tầng: Kiến trúc → Tường (kiểu Gạch đặc 220, đường định vị *Mặt hoàn thiện ngoài*) vẽ vòng tường bao theo chiều kim đồng hồ → Tường kiểu 110 cho tường ngăn → Cửa đi / Cửa sổ đặt lên tường → Tải trọng → Tải tường kiểm tra q → Phân tích (F5).

### 7.19. Kiểu Revit – tay nắm, Tab, làm lại, lệnh sửa bằng chuột, 3D có ô cửa

1. **Tay nắm (grip)**: chọn một tường hoặc dầm → hai đầu hiện ô vuông xanh, giữa hiện ô thoi trắng. **Kéo ô đầu** để kéo dài / rút ngắn (bắt điểm trục, nút, nét CAD; F8 trực giao), **kéo ô giữa** để dời cả đối tượng. Cửa trên tường giữ nguyên vị trí và tự nằm gọn trong tường; rút tường ngắn hơn cửa thì báo lỗi, tường giữ nguyên. Mỗi lần kéo là một bước hoàn tác.
2. **Vừa màn hình**: mặt bằng tự vừa đúng cỡ vùng vẽ khi mở, đổi tầng, thu / mở khung bên hoặc đổi cỡ cửa sổ (chừa lề cho bong bóng trục và kích thước). Đã lăn chuột phóng / kéo mặt bằng thì giữ nguyên khung nhìn của bạn; bấm **Vừa màn hình** (hoặc gõ Z, ZE) để vừa lại.
3. **Nhãn mặt bằng**: chữ giữ cỡ ≈ 11 px trên màn hình, có nền trắng mờ cho dễ đọc; nhãn được đặt theo thứ tự ưu tiên trục → cột → dầm → sàn → chữ khác, tự dời sang chỗ trống; nhãn dầm theo phương dọc viết xoay dọc theo dầm. Nhãn không còn chỗ bị ẩn – rê chuột vào vị trí đó để xem chú giải.
4. **Tab**: đưa chuột vào chỗ nhiều đối tượng chồng nhau (cột – dầm – tường – ô sàn) rồi nhấn **Tab** để chọn lần lượt từng đối tượng (thanh trạng thái ghi đối tượng đang chọn, ví dụ 2/4).
5. **Hoàn tác / Làm lại**: **Ctrl+Z** hoàn tác, **Ctrl+Y** (menu Sửa → Làm lại, hoặc gõ lệnh REDO) làm lại; sửa mô hình mới thì danh sách làm lại được xoá (tối đa 30 bước).
6. **Lệnh sửa bằng chuột** (gõ ở dòng lệnh dưới mặt bằng, áp cho dầm đang chọn và tường đang chọn): **CO** sao chép – bấm điểm gốc rồi điểm đích (bấm tiếp để sao nhiều bản, Esc kết thúc); **M** dời – điểm gốc → điểm đích; **MI** đối xứng – bấm 2 điểm của trục đối xứng bất kỳ (hoặc gõ tên trục, vd 2 / B); **AR** mảng – gõ số phần tử (tính cả bản gốc) rồi bấm điểm gốc và điểm thứ hai (khoảng cách giữa 2 phần tử liền nhau); **O** offset – gõ khoảng cách rồi bấm phía cần offset. Trong lúc chờ điểm đích có **bóng đối tượng** và dây chun ghi chiều dài + góc; có thể gõ chiều dài theo hướng chuột hoặc @dx,dy. Cách gõ số cũ vẫn dùng được (CO: dx,dy số bản; AR: số cột số hàng dx dy).
7. **3D**: tường xây hiện trong khung 3D với **ô cửa khoét thật** (giữ phần dưới bậu cửa sổ và phần trên đầu cửa); ô tích *Tường (có ô cửa)* ở góc trên để ẩn / hiện tường.

- Tab **Xem → Hiện nhanh**: bật / tắt ngay thẻ dầm, thẻ cột, thẻ sàn, thẻ cửa, kích thước bước trục, tải đã gán (không cần mở hộp Hiển thị / Đồ hoạ).
- Chữ nhãn luôn giữ cùng cỡ trên màn hình khi phóng to / thu nhỏ; thu nhỏ quá mức thì nhãn cấu kiện tự ẩn (tên trục, kích thước vẫn hiện); nhãn đụng nhau được dời hoặc ẩn – cả ở giao diện cổ điển.
- Ribbon và thanh lệnh cấu kiện luôn giữ **một hàng tab**: khung hẹp thì lăn chuột trên hàng tab để cuộn, hoặc bấm **»** để chọn tab bị khuất.
- Giao diện kiểu Revit ẩn bảng thông tin dưới mặt bằng để vùng vẽ rộng tối đa (thông tin đối tượng xem ở khung Thuộc tính); đổi tầng thì mặt bằng tự **vừa màn hình**.
- Link CAD: **khoá** bản vẽ nền (tab Link CAD → 🔒 Khoá) được lưu trong dự án cùng độ mờ, ẩn / hiện và vị trí căn – mở lại dự án vẫn giữ; bản vẽ đang khoá không căn 2 điểm được.

### 7.20. Bộ môn Điện nước: thiết bị chôn ngầm trong tường, tuyến ống / dây, cảnh báo khi kiến trúc thay đổi

Menu **Thiết kế → Môi trường Điện nước** mở cửa sổ riêng của bộ môn điện nước. Phần kiến trúc (tường, cửa) hiện **mờ như bản vẽ tham chiếu (xref)** – không sửa được ở đây. Mặc định **thiết bị, ống cấp nước, khớp nối, điểm chia chôn ngầm trong tường** (cấp nước đi ngang trong tường ở 0,3 m trên sàn); **ống gen điện ưu tiên đi ngang trong sàn / trần**, chỉ đi đứng trong tường tới thiết bị, không đi qua cột và dầm (xem *mục 7.21 (Thông số theo TCVN và tuyến ống gen đi trong trần / sàn)*); thoát nước đi dưới sàn.

1. **Đặt thiết bị**: tab *Cấp thoát nước* (bồn cầu, lavabo, sen tắm, vòi nước, thoát sàn, van, điểm nối ống, bình nóng lạnh) hoặc *Điện* (ổ cắm đơn / đôi, công tắc 1–3 hạt, đèn ốp trần / tuýp / downlight / hắt tường, quạt trần / treo tường, hộp chia, aptomat, tủ điện) → bấm gần tường (≤ 1 m): thiết bị gắn vào **mặt tường phía bấm**, cao độ lắp mặc định theo họ (ổ cắm 0,4 m, công tắc 1,25 m, lavabo 0,8 m…; đèn, quạt trần đặt trên trần). Esc để thôi.
2. **Nối tuyến**: chọn hệ thống (cấp nước lạnh / nóng, thoát nước thải / xám / mưa, điện chiếu sáng / ổ cắm / điều hoà / điện nhẹ) → *Nối tuyến ống* hoặc *Nối dây (ống gen)* → bấm thiết bị nguồn rồi thiết bị đích. Tuyến tự đi trong các tường liền nhau (đường ngắn nhất), tự sinh **cút 90° / chếch 45°** tại chỗ đổi hướng, **măng sông / khớp nối ống gen** theo chiều dài cây ống (ống nước 4 m, ống gen 2,9 m), **tê / tê giảm** (điện: **hộp nối**) tại điểm có ≥ 3 tuyến, **côn thu** khi đổi tiết diện tại điểm nối ống.
3. **Tự chọn cỡ** (bảng tiêu chuẩn – *mục 7.21 (Thông số theo TCVN và tuyến ống gen đi trong trần / sàn)*): cấp nước theo đương lượng N (nhà ở q = 0,2·a·√N + K·N, a theo tiêu chuẩn dùng nước – 125 l → 2,15, K = 0,002…0,006 theo N; công trình công cộng q = 0,2·α·√N – TCVN 4513, ống PPR v ≤ 1,5 m/s); thoát nước theo tổng DFU (có bồn cầu → Ø110); dây điện I = P/(U·cosφ) → aptomat → dây Cu/PVC trong ống gen (IEC 60364-5-52 cách lắp B1, hiệu chỉnh nhiệt độ / nhóm mạch, sụt áp ≤ 3 % chiếu sáng, ≤ 5 % mạch khác; tối thiểu chiếu sáng 1,5 mm², ổ cắm 2,5 mm²) → ống gen sao cho dây chiếm ≤ 40 % tiết diện.
4. **Kiến trúc thay đổi**: khi tường dời, đổi dài / dày, bị xoá hoặc thêm cửa, lúc quay lại cửa sổ Điện nước (hoặc bấm *Kiểm tra → Cập nhật theo kiến trúc*) chương trình tự **dời thiết bị theo tường**, đi lại tuyến, tính lại phụ kiện và liệt kê **cảnh báo**: thiết bị nằm trong ô cửa, tường bị xoá (thiết bị mồ côi), tường quá mỏng không chôn được ống (Ø ngoài > ½ lõi + 1 lớp trát, vd ống Ø110 trong tường 110), thiết bị chồng nhau, tuyến xuyên cột / dầm (cần ống lồng). *Đi tới* phóng tới chỗ lỗi, *Sửa nhanh* dời thiết bị ra cạnh ô cửa / gắn vào tường gần nhất.
5. **Thống kê và bản vẽ**: *Kiểm tra → Bảng thống kê vật tư* (thiết bị, ống theo Ø – m, ống gen, dây theo tiết diện – m ruột, cáp, cút / chếch / tê / côn / măng sông / hộp nối); *Xuất bản vẽ điện nước (DXF)* ra các mặt bằng **cấp nước, thoát nước, điện chiếu sáng, điện ổ cắm** từng tầng (nền kiến trúc, ký hiệu thiết bị, nhãn tuyến, **bảng ký hiệu**) và bảng thống kê, số hiệu DN-01….

- Thư viện điện nước: *Thư viện MEP → Xem thư viện / Thêm họ thiết bị*. Họ thêm được lưu trong dự án và trong thư viện người dùng **chỉ trên máy này** (%AppData%\VietConst\ThuVienMEP.json).
- Chọn thiết bị để sửa ký hiệu, cao độ, vị trí dọc tường, mạch điện, đổi mặt tường chôn; chọn tuyến để sửa cỡ hoặc bật / tắt *Tự chọn cỡ*. Delete xoá; mọi thao tác hoàn tác được (Ctrl+Z ở cửa sổ chính).
- Thiết bị và tuyến điện nước có khối 3D (dùng cho khung nhìn 3D, IFC / OBJ và kiểm tra va chạm giữa các bộ môn).

### 7.21. Thông số theo TCVN và tuyến ống gen đi trong trần / sàn

Mọi thông số điện nước – ĐHKK lấy từ **một bảng tiêu chuẩn** (Mep/MepStandards.cs), mỗi dòng ghi nguồn "Theo TCVN … Bảng …". *Cài đặt điện nước* hiện bảng: giá trị **Tiêu chuẩn** (chỉ xem), ô **Tuỳ chỉnh** (trống = theo tiêu chuẩn) và **Nguồn**; dòng có dấu **(*)** là số trích theo thực hành / tài liệu thứ cấp – cần đối chiếu bản gốc tiêu chuẩn trước khi dùng cho hồ sơ.

- **Cấp nước (TCVN 4513:1988)**: chọn *Loại công trình* – nhà ở q = 0,2·a·√N + K·N (a theo tiêu chuẩn dùng nước: 100 → 2,2; 125 → 2,15; 150 → 2,1; 200 → 2,05…; K = 0,002 khi N ≤ 300, 0,003 ≤ 500, 0,004 ≤ 800, 0,005 ≤ 1200, 0,006 lớn hơn); công trình công cộng q = 0,2·α·√N (nhà trẻ 1,2; bệnh viện 1,4; cơ quan 1,5; trường học 1,8; an dưỡng 2,0; khách sạn 2,5); đương lượng lavabo 0,33, chậu bếp 1,0, sen 0,67, xí có két 0,5; v ≤ 1,5 m/s; bể – bơm 150 l/người·ngđ, Kngày 1,3.
- **Thoát nước (TCVN 4474:1987)**: ống có xí ≥ Ø110; độ dốc tiêu chuẩn / nhỏ nhất Ø50: 3,5 % / 2,5 %, Ø100: 2 % / 1,2 %, Ø150: 1 % / 0,7 %; ống thông hơi vượt mái ≥ 0,7 m.
- **Điện (TCVN 9206 / 9207:2012, IEC 60364-5-52)**: dòng cho phép dây Cu/PVC cách lắp B1 (1,5 → 17,5 A; 2,5 → 24 A; 4 → 32 A; 6 → 41 A; 10 → 57 A; 16 → 76 A; 25 → 101 A) × hệ số nhiệt độ (Bảng B.52.14: 35 °C → 0,94) × hệ số nhóm mạch (Bảng B.52.17: 2 mạch → 0,80); sụt áp ≤ 3 % chiếu sáng, ≤ 5 % mạch khác (Phụ lục G, ρ = 0,0225 Ω·mm²/m); Kc chiếu sáng 0,9, ổ cắm 0,5, điều hoà 0,8, bình nóng 0,7; ổ cắm 0,4 m, công tắc 1,25 m, tủ điện 1,5 m.
- **ĐHKK (TCVN 5687:2010)**: trong nhà mùa hè 25 °C, 60 % (Bảng A.1, nghỉ ngơi tĩnh tại 25…28 °C, 60…70 %); ngoài nhà (Phụ lục B, m = 35 h/năm) Hà Nội 37,8 °C – 53,4 %, TP.HCM 36,8 °C – 56 % (chọn *Khí hậu ngoài nhà*); gió tươi 25 m³/h·người; vận tốc ống gió chính 6 m/s, nhánh 4 m/s.

1. **Tuyến ống gen** (*Cài đặt điện nước → Tuyến ống gen điện*): **Trần / sàn (khuyến nghị, mặc định)** – ổ cắm, điều hoà đặt thấp đi trong **sàn dưới chân** (tim ống ở giữa chiều dày sàn; tầng nền không có sàn: trong lớp bê tông lót), chiếu sáng và thiết bị đặt cao đi trong **sàn trên (trần)**; trong tường chỉ có **đoạn đứng** từ thiết bị xuống mặt sàn / lên đáy sàn. *Trong tường* – cách cũ, đi ngang theo tim tường.
2. **Không đi qua cột, dầm**: tuyến trong sàn được dò trực giao tránh tiết diện cột (khe hở 50 mm) và dải bề rộng dầm. Chỉ được **qua dầm trong chiều dày sàn** khi sàn ≥ 100 mm, ống gen ≤ Ø25 và điểm qua nằm trong **1/3 giữa nhịp** (cách gối ≥ L/3); không đạt thì vòng đường khác; không có đường → tuyến đi theo tường và có cảnh báo **ONG_GEN_TRAN_SAN**.
3. Thiết bị trên tường đặt trên dầm (hoặc dưới dầm): ống đi đứng tới **lớp cán nền** (mặc định 30 mm trên mặt sàn) / dưới đáy dầm rồi chuyển ra khỏi bề rộng dầm trước khi vào sàn.
4. **Kiểm tra kết cấu** cho ống gen trong sàn: **ONG_GEN_QUA_DAM** (qua dầm: đạt → Gợi ý; sàn mỏng, Ø lớn, sát gối → Cảnh báo), **SAN_MONG** (Ø ống > 1/3 chiều dày sàn), **ONG_GEN_NGANG_TUONG** (còn đoạn ngang > 0,3 m trong tường khi đã chọn trần / sàn). Rãnh đục tường và khối lượng tường trừ rãnh giảm tương ứng vì đoạn ngang không còn nằm trong tường.

> **Mẹo:** Cấp nước vẫn đi trong tường như trước; chỉ ống gen điện (dây trong ống gen) áp dụng tuyến trần / sàn. Tuyến vẽ tay (điểm trung gian) giữ nguyên như người dùng vẽ.

### 7.22. Khoảng hở ống gen, khí hậu theo tỉnh, chiếu sáng LPD, ống đứng thoát nước và bể tự hoại

Các phép kiểm tra bổ sung cho điện nước (Mep/MepRound5.cs). Thông số nằm trong bảng tiêu chuẩn của *mục 7.21 (Thông số theo TCVN và tuyến ống gen đi trong trần / sàn)* và tuỳ chỉnh được; dòng có dấu **(*)** là giá trị theo thực hành, cần đối chiếu bản gốc tiêu chuẩn.

- **Ống gen song song trong sàn**: khoảng hở giữa 2 ống ≥ **max(3·Ø, 50 mm)** khi đi cạnh nhau ≥ 0,5 m. Vi phạm → cảnh báo **ONG_GEN_SAT_NHAU**; khi *Đi lại tuyến* / đối chiếu, tuyến vẽ sau được **giãn** sang bên: dời đoạn giữa, 2 đoạn kề kéo dài theo, vẫn tránh cột và chỉ qua dầm ở 1/3 giữa nhịp.
- **Số ống trong một dải sàn**: quá 4 ống gen song song trong dải rộng 1 m → **ONG_GEN_BO**. Tại chỗ 2 ống giao nhau, tổng Ø chồng lên phải ≤ 1/3 chiều dày sàn, nếu không → **ONG_GEN_GIAO** (cùng quy tắc với **SAN_MONG** cho một ống).
- **Khí hậu ĐHKK**: *Khí hậu ngoài nhà* chọn theo tỉnh: Hà Nội, TP.HCM, **Thái Nguyên**, Bắc Ninh, Bắc Giang, Vĩnh Phúc, Hưng Yên, Bình Dương, Đồng Nai, Long An. Tỉnh chưa có số liệu riêng lấy theo trạm gần nhất; tên tỉnh ghi rõ trạm. Thái Nguyên tạm lấy giá trị trạm Hà Nội (*) và sửa được qua khoá *dhkk.t_tn* / *dhkk.phi_tn*. *Phiên bản TCVN 5687*: **2010** (Phụ lục B) hoặc **2024**. Bản 2024 dẫn chiếu số liệu khí hậu của QCVN 02:2022/BXD; giá trị mặc định vẫn theo bảng 2010 nên cần nhập lại theo QCVN 02:2022.
- **Tải lạnh theo khí hậu** (*Tải lạnh chi tiết: Δt, Δw, hiệu chỉnh CLTD theo khí hậu địa phương* = Có): Δt = t ngoài − t trong (Hà Nội 37,8 − 25 = 12,8 °C). Δw = w ngoài − w trong, với áp suất hơi bão hoà theo công thức Magnus. CLTD tường / mái cộng thêm hiệu chỉnh (25,5 − t trong) + (t ngoài − DR/2 − 29,4), trong đó DR là biên độ nhiệt ngày (mặc định 8 °C, Hà Nội → +4,9 °C). Mặc định là Không: giữ Δt 9 °C như trước.
- **Chiếu sáng (TCVN 7114-1:2008, QCVN 09:2017/BXD)**: loại phòng nhận theo tên phòng. Mỗi loại có độ rọi yêu cầu và mật độ công suất LPD lớn nhất: văn phòng / phòng họp 500 lx, 11 W/m²; lớp học 300 lx; phòng khách 150 lx; phòng ngủ 100 lx; bếp 200 lx; vệ sinh 200 lx; hành lang 100 lx; cầu thang 150 lx; kho 100 lx. Kiểm tra Σ W đèn trong phòng / diện tích ≤ LPD. Số đèn cần tính theo phương pháp hệ số sử dụng **N = E·A / (Φ·UF·MF)** (UF 0,6; MF 0,8). Quang thông Φ lấy theo thư viện (ốp trần 2200 lm, tuýp LED 1,2 m 1800 lm, downlight 800 lm, hắt tường 960 lm); họ chưa khai báo thì lấy 100 lm/W. Sửa LPD / độ rọi bằng khoá *cs.lpd.(mã)* / *cs.e.(mã)* (mã: vanphong, hop, lophoc, khach, ngu, bep, wc, hanhlang, cauthang, kho, khac).
- **Ống đứng thoát nước (TCVN 4474:1987; sức chứa DFU tham khảo IPC (*))**: DFU cộng dồn **từ tầng trên xuống**. Ø ống đứng chọn tại chân ống và giữ suốt chiều cao; ống có xí ≥ Ø110. Sức chứa theo số tầng có thiết bị (≤ 3 hoặc > 3 khoảng nhánh), ví dụ Ø110 chứa 240 / 500 DFU. Ống không có thông hơi lấy 0,5 sức chứa (*Ống đứng thoát nước có ống thông hơi* = Không). **Ống thoát ra** hố ga / bể tự hoại chọn Ø theo tổng DFU và độ dốc tiêu chuẩn của Ø đó.
- **Bể tự hoại 3 ngăn (TCVN 10334:2014)**: chỉ lập khi đã khai báo số người dùng nước. Thể tích nước Wn = t·q·N/1000 (lưu 1 ngày, 150 l/người·ngđ). Thể tích cặn Wc = a·T·(100 − W1)·b·c·N / ((100 − W2)·1000), với a = 0,6; T = 180 ngày; W1 = 95 %; W2 = 90 %; b = 0,7; c = 1,2. Dung tích W = Wn + Wc. Lớp nước sâu 1,5 m, bề rộng ≥ 0,8 m; chia ngăn chứa L/2, ngăn lắng L/4, ngăn lọc L/4. Bộ bản vẽ có hình mặt cắt dọc và mặt bằng bể (tỷ lệ 1:50) với ống vào / ra Ø110 và ống thông hơi.

> **Mẹo:** Bảng chiếu sáng chỉ xuất khi phòng có đèn. Bảng ống đứng và bể tự hoại chỉ xuất khi đã khai báo số người, nên các dự án cũ không đổi số bảng.

### 7.23. Thoát nước đi dưới sàn có độ dốc; cảnh báo điểm nguy hiểm cho kết cấu và chi tiết gia cường

Ống **thoát nước** (thải, xám, mưa) **không đi trong tường**: từ thiết bị chỉ có đoạn đứng ngắn xuyên sàn ngay trước mặt tường, đoạn ngang **treo dưới đáy sàn** (trong không gian trần tầng dưới; ô sàn **hạ cốt** đủ sâu thì đi trong lớp hạ cốt) và **dốc về ống đứng** (hộp gen kỹ thuật). Tầng thấp nhất: ống chôn trong lớp đất đắp dưới sàn nền.

1. Tab *Cấp thoát nước* → **Nối thoát về ống đứng**: chương trình đề xuất **ống đứng** tại góc tường gần nhóm thiết bị vệ sinh (đã có thì dùng lại) và nối mọi thiết bị thoát chưa nối.
2. **Độ dốc**: mặc định theo Ø (TCVN 4474 – thực hành): Ø ≤ 50: **3 %**; Ø60…Ø90: **2 %**; Ø ≥ 110: **2 %** (tối thiểu 1 %). Chọn tuyến → ô *Độ dốc (%)* để nhập riêng (0 = mặc định); ngoài khoảng tối thiểu…8 % có cảnh báo. Cao độ tim ống giảm dần từ thiết bị: z = z₀ − i·s; khung thuộc tính ghi cao độ **đáy ống đầu / cuối tuyến**.
3. **Phụ kiện thoát**: đổi hướng dùng **2 chếch 45°** (không cút 90°), nhánh vào ống đứng / điểm nối dùng **tê xiên 45° (chữ Y)**, **ống kiểm tra (nắp thăm)** ở chân ống đứng và mỗi 12 m tuyến ngang; thống kê có **quang treo + ty ren** (≤ 0,8 / 1,0 / 1,2 m theo Ø) và gợi ý **ống thông hơi** vượt mái ≥ 0,7 m.
4. Cảnh báo thoát nước: **ép đi trong tường** (ô *Ép đi trong tường* – không khuyến khích), ống tụt thấp hơn **trần giả** (đáy sàn − 0,6 m mặc định), vượt khỏi lớp hạ cốt, độ dốc ngoài khoảng.

- **Ảnh hưởng tới kết cấu** (danh sách cảnh báo, có mức **Cho phép / Gợi ý / Cảnh báo / Nguy hiểm / Cấm** và đề xuất xử lý):
- **Xuyên dầm**: cho phép khi Ø lỗ (Ø ống + 20 mm ống lồng) ≤ h/3, mép lỗ trong **1/3 giữa chiều cao**, tâm lỗ trong **1/3 giữa nhịp**, cách mép gối ≥ h (≥ 2h vùng tới hạn kháng chấn), hai lỗ cách ≥ 3Ø; Ø > h/4 hoặc ngoài 1/3 giữa nhịp → gia cường; vi phạm → **Nguy hiểm** (dời lỗ / đi dưới đáy dầm / tăng chiều cao dầm). Ống **đứng** xuyên dầm: không cho phép.
- **Gia cường lỗ xuyên dầm**: ống lồng thép chờ sẵn; 2 đai (3 đai khi Ø > h/4) Ø8a50 mỗi bên lỗ; 2Ø12 trên + 2Ø12 dưới lỗ dài Ø + 2·max(40d; 500 mm).
- **Xuyên cột**: **Cấm** (ống gen ≤ Ø25 trong cột cạnh ≥ 400: cảnh báo, chỉ đặt ống chờ khi đổ).
- **Lỗ sàn**: cách mép cột < 2,5·hs → **Nguy hiểm** (vùng chọc thủng); < 6·hs → cảnh báo kiểm tra lại chọc thủng; lỗ > 300 mm → **thép gia cường mép lỗ** bù phần thép bị cắt (As cắt chia đều 2 bên, tối thiểu 2Ø12 mỗi bên, dài cạnh lỗ + 2·40d) + 1Ø12 xiên mỗi góc; ≥ 3 lỗ thẳng hàng cách nhau < 1 m → đường yếu.
- **Rãnh đục tường**: tường BTCT không đục (đặt ống chờ); rãnh ngang sâu > 1/3 lõi hoặc dài > 1 m trong tường 110 → cảnh báo.
- **Va chạm giữa bộ môn**: ống xuyên dầm ở vị trí không cho phép được tính là **va chạm cứng** thay vì *ống lồng*.
- Hồ sơ điện nước (DN-) kèm **CHI TIẾT GIA CƯỜNG LỖ XUYÊN DẦM** và **CHI TIẾT GIA CƯỜNG MÉP LỖ SÀN** (tỷ lệ 1/20); mặt bằng thoát nước ghi mũi tên dốc, **i=2%**, cao độ đáy ống.

### 7.24. Lỗ xuyên điện nước trong hồ sơ kết cấu; tường trừ rãnh điện nước; kích thước tạm tường / cột

Mọi lỗ xuyên dầm (trừ mức **Cấm**) và lỗ sàn lớn có thép gia cường từ bộ môn điện nước được **đánh ký hiệu** **LX1, LX2…** (lỗ xuyên dầm) và **LS1, LS2…** (lỗ sàn); lỗ giống nhau (cùng cấu kiện, cùng Ø / kích thước) dùng chung ký hiệu.

- Trong **hồ sơ kết cấu (KC)**:
- Tờ **KC-10 "CHI TIẾT GIA CƯỜNG LỖ XUYÊN KẾT CẤU"**: mỗi ký hiệu một hình (tỷ lệ 1/20) kèm số lượng và tầng.
- **Mặt bằng kết cấu**: vòng tròn tại vị trí lỗ + ký hiệu LX / LS.
- **Mặt đứng dầm**: vẽ lỗ + ống lồng, đai gia cường mỗi bên lỗ, thép bổ sung trên / dưới lỗ và ghi chú "xem chi tiết LX1".
- **Kiểm tra chịu cắt tại lỗ xuyên** (bảng kiểm tra dầm): tiết diện thực h0n = h0 − Ø lỗ, đai quanh lỗ bước 50 mm, Qu = Qb + 0,75·qsw·2h0n ≤ 0,3·Rb·b·h0n so với **Q tại vị trí lỗ** – nội suy bao lực cắt của nhịp (Qmax ở gối, giảm tuyến tính về 0 giữa nhịp).
- **Bảng thống kê cốt thép**: thêm cấu kiện "D1 – gia cường lỗ xuyên" / "S1 – gia cường lỗ xuyên" chứa thép gia cường (số lượng, Ø, chiều dài, trọng lượng).

- **Triển khai hồ sơ**:
- Dầm: bảng **LỖ XUYÊN DẦM – ỐNG LỒNG** (vị trí từ tim gối đầu, cách đáy, Ø); sàn: bảng **LỖ XUYÊN SÀN** (tâm lỗ từ góc ô, cạnh lỗ) – thép gia cường vào thép cấu kiện, lỗ vẽ trên mặt đứng / mặt bằng, cảnh báo lỗ sai vùng.
- Mặt bằng kết cấu → nút **Lỗ xuyên từ điện nước**: chép lỗ từ mô hình (chế độ Thiết kế) vào dầm / sàn hồ sơ **cùng tên** (không nhân đôi khi chạy lại).
- Lỗ sàn của ô sàn **đã đặt trên mặt bằng kết cấu** được đo khoảng cách tới **mép cột gần nhất** trên cùng mặt bằng: < 2,5·hs → *NGUY HIỂM* (vùng chọc thủng, có dòng kiểm tra không đạt), < 6·hs → *CẢNH BÁO* (giảm chu vi chọc thủng).

- **Tường trừ phần điện nước chiếm chỗ** (bảng khối lượng):
- Ống / ống gen chôn tường: rãnh rộng Ø + 20 mm, sâu Ø + 10 mm (≤ lõi tường) × chiều dài trong tường; hộp âm tường (ổ cắm, công tắc, hộp chia, aptomat, tủ điện): W × H × sâu.
- Khối xây (gạch, vữa xây) trừ thể tích này; thêm công tác **Đục rãnh tường (m)**, **Trát vá rãnh (m²)**, **Đục lỗ hộp âm tường (cái)**. Ống thoát nước đi dưới sàn không đục tường. Tải tường giữ nguyên (phần trừ nhỏ, thiên về an toàn).

- **Mặt cắt** và **kích thước tạm**:
- Mặt cắt 1-1 / 2-2 tự chọn nhịp **cắt được nhiều sàn nhất** (tránh ô cầu thang); mức sàn không có sàn trên đường cắt ghi chú "ô cầu thang / lỗ sàn"; phần tử **nhìn thấy phía sau** mặt cắt (tới trục kế tiếp, ≤ 6 m) vẽ nét mảnh.
- Giao diện kiểu Revit: chọn **tường** → kích thước tạm tới tường / trục song song hai bên (bấm số → dời tường song song); chọn **cột** → khoảng cách tâm cột tới trục gần nhất (bấm số → đổi lệch tâm cột).

### 7.25. Điều hoà – thông gió, cài đặt điện nước, kéo cao độ trên mặt cắt, kiểu trục mặt bằng kết cấu

Cửa sổ Môi trường Điện nước có thêm tab **Điều hoà – thông gió** và nút **Cài đặt điện nước…** (tab Kiểm tra). Mọi thay đổi là một bước hoàn tác (Ctrl+Z ở cửa sổ chính).

- **Điều hoà – thông gió (ĐHKK)**:
- Đặt **dàn lạnh / dàn nóng** (nhập công suất BTU/h), **miệng gió cấp / hồi** (sát trần).
- **Vẽ ống gió**: chọn hệ (gió cấp / hồi / thải / tươi), nhập lưu lượng Q (m³/h), bấm điểm đầu – điểm cuối (vẽ nối tiếp được). Ống đi trực giao sát đáy sàn trên (khe 0,1 m). Cỡ ống tự chọn: **A = Q / (3600·v)**; ống tròn lấy Ø chuẩn ≥ √(4A/π), ống chữ nhật lấy chiều cao chuẩn nhỏ nhất sao cho w/h ≤ 4, w làm tròn bội 50 mm.
- Phụ kiện ống gió tự sinh: **cút** ở điểm đổi hướng, **tê** khi đầu ống chạm thân ống khác, **côn** khi hai ống nối đầu khác tiết diện.
- **Ống đồng gas**: bấm dàn lạnh rồi dàn nóng – cặp ống lỏng / gas theo công suất dàn lạnh (≤ 12000 BTU: Ø6,35/Ø9,52; 18000: Ø6,35/Ø12,7; 24000: Ø6,35/Ø15,88; tới 48000: Ø9,52/Ø15,88).
- **Ống nước ngưng**: bấm dàn lạnh rồi điểm xả – uPVC, cao độ giảm theo độ dốc cài đặt (tối thiểu **1 %**).
- **Va chạm ống gió – dầm**: ống có đỉnh cao hơn đáy dầm tại chỗ cắt qua dầm được cảnh báo (mã VA_CHAM_ONG_GIO) kèm đề xuất hạ ống / dùng ống dẹt – không khoét dầm.
- Bảng vật tư điện nước thêm: **tôn mạ kẽm (m²) = chu vi × chiều dài** (độ dày 0,5 / 0,75 / 1,0 / 1,2 mm theo cạnh lớn), ống gió theo cỡ (m), bảo ôn, giá treo, phụ kiện ống gió, **cặp ống đồng (m)** và bảo ôn, ống nước ngưng (m), dàn lạnh / nóng, miệng gió.
- Bộ bản vẽ điện nước thêm **MẶT BẰNG ĐIỀU HOÀ – THÔNG GIÓ** từng tầng: ống gió 2 nét + tim và nhãn cỡ – lưu lượng, ký hiệu miệng gió (MGC / MGH), dàn lạnh (DL), dàn nóng (DN), ống gas (G), ống nước ngưng (N, ghi i%), bảng ký hiệu. Khung nhìn 3D / IFC có khối ống gió đúng tiết diện, dàn lạnh / nóng, miệng gió, ống đồng.
- Nhập Revit (đối tượng điện nước): **ống gió** (Duct) thành ống gió ĐHKK giữ tiết diện chữ nhật / tròn và hệ cấp / hồi / thải theo tên hệ; **miệng gió** (Air Terminal); thiết bị cơ (Mechanical Equipment) có tên dàn lạnh / indoor / FCU / cassette hoặc dàn nóng / outdoor thành dàn lạnh / nóng (đọc công suất "… BTU" trong tên). Phụ kiện ống gió bỏ qua (tự sinh).

- **Cài đặt điện nước…**:
- Thông số chung: cao độ tuyến chôn tường, tuyến chiếu sáng cách trần, chiều dài cây ống / ống gen, chiều cao tầng mặc định, độ dốc mặc định theo Ø, không gian trần, khoảng cách nắp thăm, **khoảng cách quang treo** (0 = theo Ø), thông số ĐHKK (vận tốc ống chính / nhánh, tỷ lệ cạnh, khe trần, giá treo ống gió, độ dốc và Ø ống nước ngưng).
- Bảng **độ dốc theo hệ thống + dải Ø**: hệ thống, Ø từ – Ø đến (0 = mọi Ø), độ dốc %, **Min / Max %** (kẹp cả độ dốc nhập tay của tuyến thuộc hệ đó). Áp dụng xong mọi tuyến được đi lại – cao độ đáy ống theo độ dốc mới.
- Bảng **cao độ mặc định theo nhóm thiết bị** (ổ cắm, công tắc…): dùng khi đặt thiết bị mới thay cho cao độ của họ; thiết bị trên trần vẫn theo trần.

- **Thao tác kiểu Revit bổ sung**:
- Khung **mặt cắt** (xem mặt cắt): bấm giữ chuột trái gần một đường cao độ tầng rồi kéo để đổi cao độ tầng (bắt 10 mm, không vượt quá tầng kề ±0,5 m) – giống kéo trên mặt đứng; cột, dầm, sàn dời theo.
- Mặt bằng (chế độ Thiết kế): nắm trục ở **phần đuôi đường trục** ngoài khung lưới (không chỉ ở bong bóng) để kéo; giữ **Alt** để nắm cả đoạn trục bên trong khung lưới.
- Mặt bằng kết cấu (Triển khai hồ sơ): chuột phải lên đường trục → bong bóng **cả hai đầu / chỉ đầu dưới–trái / chỉ đầu trên–phải / không**, **ẩn trục trên mặt bằng này**, **hiện lại mọi trục đã ẩn**. Kiểu trục đi theo tên trục khi thêm / xoá / dời trục và áp dụng cả trên bản vẽ DXF (chuỗi kích thước bỏ trục ẩn).
- Khung **3D**: chọn thiết bị điện nước rồi **kéo** – thiết bị gắn tường trượt dọc tường chủ, thiết bị tự do dời trên mặt sàn; bắt bước 5 cm, tuyến nối với thiết bị đi lại ngay, thả chuột là một bước hoàn tác.

### 7.26. Điều hoà – thông gió: sửa từng đối tượng, hạ ống qua dầm, chọn máy theo phòng

Tab **Điều hoà – thông gió** của cửa sổ Điện nước có thêm hàng lệnh **Sửa** và **Máy theo phòng**. Mỗi thao tác (kể cả một lần kéo) là một bước hoàn tác.

- **Chọn và sửa trên mặt bằng** (công cụ Chọn / sửa, hoặc chuột phải để về công cụ chọn):
- Bấm vào **ống gió** (trong bề rộng ống), **miệng gió**, **dàn lạnh / dàn nóng**, **ống đồng / ống nước ngưng** để chọn – viền đỏ nét đứt; **Ctrl + bấm** thêm / bớt đối tượng.
- **Quét khung**: kéo từ trái sang phải chọn đối tượng **nằm trọn** trong khung (khung xanh dương); kéo từ phải sang trái chọn đối tượng **chạm** khung (khung xanh lá, nét đứt).
- **Kéo** đối tượng đã chọn để dời (bắt bước 5 cm). Kéo đúng **đỉnh tuyến** ống gió để nắn tuyến. Dời dàn lạnh / dàn nóng thì ống đồng đi lại tuyến, ống nước ngưng đi lại từ dàn mới và giữ điểm xả, độ dốc.
- **Delete** xoá đối tượng đã chọn; xoá dàn lạnh / nóng xoá kèm ống đồng, ống ngưng nối với nó.
- Bảng **Thuộc tính** ống gió: hệ, ống tròn / chữ nhật, w × h hoặc Ø (đổi cỡ **giữ cao độ đỉnh ống** – ống vẫn sát trần), lưu lượng Q (tự chọn lại cỡ theo vận tốc), cao độ đỉnh so với sàn, nút **Hạ ống qua dầm**, **Đưa lại sát trần**, **Vẽ lại tuyến** (bấm 2 điểm, giữ cỡ – hệ – cao độ). Hiển thị chiều dài, m² tôn, vận tốc thực, số cút (cút 45°) / tê / côn.
- Thuộc tính **dàn lạnh / nóng**: công suất nhập theo **BTU/h hoặc kW** (1 BTU/h = 0,000293 kW), cao độ, hướng, toạ độ; đổi công suất thì cặp ống đồng chọn lại. Hiển thị lưu lượng gió, cỡ ống đồng, công suất điện, aptomat và dây cấp cho máy.
- Thuộc tính **miệng gió** (cấp / hồi, kích thước, lưu lượng, cao độ) và **ống** (Ø ống lỏng / gas; ống ngưng: Ø, độ dốc ≥ 1 %).

- **Ống gió đi qua dầm**:
- Khi vẽ ống (ô **Tự hạ ống qua dầm** đang bật – mặc định), ống sát trần mà va dầm được **hạ xuống dưới đáy dầm** (khe 50 mm): mỗi phía dầm một đoạn xiên **45°** (2 cút 45°), đoạn xiên chạy ngang bằng độ hạ; chiều dài vùng thấp = bề rộng dầm (tính theo góc ống – dầm) + bề rộng ống chiếu + 2 × khe.
- Chỉ hạ khi trần còn chỗ: đáy ống sau khi hạ ≥ sàn + max(**chiều cao thông thủy tối thiểu** 2,4 m; cao độ trần của phòng chứa điểm cắt). Không đủ → giữ nguyên ống và báo cảnh báo **ONG_GIO_KHONG_QUA_DAM** (ghi rõ thiếu bao nhiêu mm) kèm đề xuất: đổi ống dẹt hơn, đi vòng, nâng trần, hoặc lỗ chờ dầm có gia cường được kết cấu duyệt.
- Đoạn ống quá ngắn để xiên 45° → hạ cả tuyến dưới dầm (nếu trần cho phép). Lệnh **Hạ ống qua dầm** áp dụng cho ống đang chọn hoặc mọi ống gió của tầng; hai dầm sát nhau gộp thành một lần hạ.

- **Chọn máy theo phòng** (phòng vẽ / nhận diện ở môi trường Kiến trúc):
- Tải lạnh theo phương pháp diện tích: **Q = 600 · A · max(1; h/3) · (1 + 0,15 nếu phòng có tường ngoài) + 500 · max(0; N − 2)** (BTU/h; A diện tích phòng m², h cao độ trần phòng hoặc chiều cao tầng, N số người). Đây là ước tính sơ bộ cho nhà ở – công trình lớn cần tính tải nhiệt chi tiết.
- Chọn máy treo tường **9000 / 12000 / 18000 / 24000 BTU** nhỏ nhất ≥ Q; Q > 24000 → nhiều máy cùng cỡ. Nút **Tải lạnh các phòng** lập bảng cho mọi phòng của tầng.
- **Đặt máy cho phòng**: bấm vào trong phòng – dàn lạnh đặt trên tường **không có cửa đi**, **xa cửa đi nhất** (thường là tường đối diện cửa), **ưu tiên tường ngoài**; dàn nóng ở mặt ngoài tường ngoài gần nhất; tự nối ống đồng (cỡ theo công suất) và ống nước ngưng xả ra ngoài tường.
- Điện máy lạnh: P = Q · 0,000293 / COP (COP = 3), I = P / (220 · 0,85), dòng thiết kế 1,25 · I (động cơ máy nén) → aptomat chuẩn ≥ dòng thiết kế, dây có dòng cho phép ≥ aptomat và ≥ 2,5 mm².

- **3D và IFC**:
- Khung **3D**: chọn dàn lạnh / dàn nóng, miệng gió, ống gió hoặc ống đồng rồi **kéo** trên mặt phẳng ngang (bắt 5 cm); cả tuyến ống gió được tô sáng; Delete xoá. Phụ kiện ống gió hiện thành khối nhỏ tại điểm nối.
- Xuất **IFC2X3**: ống gió → IfcFlowSegment + **IfcDuctSegmentType**; cút / tê / côn → IfcFlowFitting + **IfcDuctFittingType** (BEND / JUNCTION / TRANSITION); miệng gió → IfcFlowTerminal + **IfcAirTerminalType**; dàn lạnh / nóng → **IfcEnergyConversionDevice** + **IfcUnitaryEquipmentType** (SPLITSYSTEM); ống đồng / ống ngưng → **IfcPipeSegmentType**; gắn kiểu bằng **IfcRelDefinesByType** (mỗi kích thước / tên một kiểu).

### 7.27. Điện nước – ĐHKK: bám tường khi kéo, dò tuyến tránh cột, tải lạnh chi tiết, tủ điện, bể nước – bơm, sơ đồ đứng

- **Kéo trong 3D có bám mặt tường**:
- Kéo **dàn lạnh / dàn nóng**: thiết bị tự bám **mặt tường gần nhất** (trong vùng 0,3 m) – tâm cách mặt tường đúng nửa chiều sâu thiết bị, trục song song tường, vị trí dọc tường bắt bước **5 cm** và không vượt khỏi đầu tường; ống đồng, ống nước ngưng đi lại theo. Ra xa tường thì bắt lưới 5 cm theo X, Y.
- Kéo **thiết bị điện nước tự do** (đặt trên sàn, không gắn trần): bám mặt tường như trên; thiết bị **gắn tường** giữ tường chủ, chỉ trượt dọc tường. Đồ đạc nội thất vẫn bám tường / góc phòng như trước.
- Thanh trạng thái hiện **✓ Bám mặt tường …** khi đã bám, cùng toạ độ đang kéo.

- **Dò tuyến ống gió tránh cột** (nút **Dò tuyến tránh cột**, hàng Sửa của tab Điều hoà – thông gió):
- Áp dụng cho ống gió đang chọn, hoặc mọi ống gió của tầng nếu chưa chọn. Giữ hai đầu ống và cao độ sát trần, tìm tuyến **trực giao** trên lưới mặt bằng (bước mặc định 0,1 m) bằng thuật toán A*.
- **Cột** là vật cản (nở thêm nửa bề rộng ống + khe 50 mm); mỗi lần **cắt dầm** phạt 2 m (phải hạ ống), mỗi lần **xuyên tường** phạt 3 m, mỗi lần **đổi hướng** phạt 1 m – chọn tuyến có tổng chiều dài + phạt nhỏ nhất, nên ít cút. Sau đó ống tự hạ qua dầm (nếu bật) và sinh lại phụ kiện.
- Bước lưới và các mức phạt sửa trong **Cài đặt điện nước**.

- **Dàn nóng và tải lạnh**:
- **Cài đặt điện nước → ĐHKK – cách lắp dàn nóng mặc định**: **Đặt sàn / ban công** (cao độ tâm 0,4 m, mặc định) hoặc **Giá treo tường** (mặc định 2,2 m so với sàn, sửa được). Áp dụng cho dàn nóng đặt mới (kể cả **Đặt máy cho phòng**).
- **ĐHKK – phương pháp tải lạnh**: **Ước lượng nhanh** (phương pháp diện tích, mặc định) hoặc **Chi tiết (CLTD)**. Khi chọn Chi tiết, **Đặt máy cho phòng** và **Tải lạnh các phòng** dùng tải chi tiết.
- Tải chi tiết (CLTD rút gọn, nút **Tải lạnh chi tiết**): tường ngoài Q = U·A·CLTD theo hướng (U = 2,0 W/m²K; CLTD Bắc 6, Đông Bắc 10, Đông 14, Đông Nam 12, Nam 9, Tây Nam 14, Tây 16, Tây Bắc 11 °C); kính Q = A·(U·Δt + SHGF·SC·CLF) (U = 5,8; Δt = 9 °C; SHGF Bắc 120, Nam 200, Đông / Tây 500, các hướng chéo 400 W/m²; SC = 0,6; CLF = 0,7); mái (tầng cao nhất) U = 1,8 × CLTD 22 °C; người 75 W hiện + 55 W ẩn; đèn 10 W/m²; thiết bị 15 W/m²; gió lọt 0,5 lần/h: hiện 1,23·Q(l/s)·Δt, ẩn 3010·Q(l/s)·Δw (Δw = 0,008 kg/kg); tổng × 1,1. Hướng tính theo pháp tuyến ra ngoài của tường, trục +Y mặt bằng là hướng Bắc.
- Bảng tải chi tiết từng phòng (kèm cột ước lượng nhanh để so sánh) có trong sheet Excel **Vật tư điện nước** khi chọn phương pháp Chi tiết.

- **Tủ điện** (tab Điện: **Bảng tủ điện**, **Gán mạch theo tủ**):
- Chia mạch theo tầng: **chiếu sáng** (đèn, quạt) ≤ 10 điểm / mạch, **ổ cắm** ≤ 8 điểm / mạch, mỗi **máy lạnh** và mỗi **bình nóng lạnh** một mạch riêng.
- Mỗi mạch: P = tổng công suất thiết bị, I = P / (220 · cosφ) (cosφ 0,85) → aptomat chuẩn ≥ I (chiếu sáng tối thiểu 10 A, ổ cắm / bình nóng lạnh tối thiểu 16 A, kèm chống dòng rò RCD 30 mA), dây Cu/PVC có dòng cho phép ≥ aptomat (chiếu sáng ≥ 1,5 mm², ổ cắm ≥ 2,5 mm²); máy lạnh theo dòng thiết kế 1,25·I.
- Công suất tính toán Ptt = Σ Kc·P (Kc mặc định: chiếu sáng 0,9; ổ cắm 0,5; điều hoà 0,8; bình nóng lạnh 0,7). Tủ **1 pha** khi Ptt ≤ 12 kW, lớn hơn chuyển **3 pha** (hoặc chọn cố định 1 pha / 3 pha trong Cài đặt).
- Tủ 3 pha **cân pha**: mạch lớn xếp trước, lần lượt đưa vào pha đang nhẹ nhất; báo tải từng pha và độ lệch pha = max |Pi − P trung bình| / P trung bình.
- **Aptomat tổng** ≥ max(dòng tính toán, aptomat nhánh lớn nhất) (1 pha: MCB 2P; 3 pha: MCCB 4P), **cáp tổng** Cu/XLPE có dòng cho phép ≥ aptomat tổng.
- **Sơ đồ nguyên lý tủ điện** (nguồn vào – aptomat tổng – thanh cái – các nhánh ghi aptomat, dây, tên mạch, công suất, tầng, pha) có trong bộ bản vẽ điện nước; **bảng tủ điện** có trong sheet Excel. **Gán mạch theo tủ** ghi tên mạch C1, C2… vào thiết bị.

- **Bể nước – bơm** (tab Cấp thoát nước: **Bể nước – bơm**; khai báo **số người** trong Cài đặt điện nước):
- Dung tích V = N · q · số ngày dự trữ / 1000 (m³), q mặc định 150 l/người·ngày; bể mái 1 ngày, bể ngầm 2 ngày + dự trữ chữa cháy; chọn bồn chuẩn 500 … 6000 l (lớn hơn: bội số 1000 l).
- Bơm: Q = Q ngày · Kngày (1,3) / số giờ bơm (4 h); ống đẩy PPR chọn theo v ≤ 1,5 m/s; cột áp H = H tĩnh (miệng bể mái − mực nước thấp bể ngầm) + 1,3 × tổn thất Hazen-Williams (C = 140, chiều dài = chiều cao đẩy + 10 m ngang) + 2 m tự do; công suất N = ρ·g·Q·H / η (η = 0,6).
- Bảng bể nước và bơm có trong sheet Excel khi đã khai báo số người.

- **Sơ đồ đứng** (tự thêm vào bộ bản vẽ điện nước, vẽ tỷ lệ 1 như bảng thống kê):
- **Sơ đồ không gian cấp nước**: bể ngầm – bơm – ống đẩy lên **bể nước mái** – trục cấp xuống từng tầng; nhánh tầng vẽ xiên 45° với tên thiết bị; Ø trục và Ø nhánh theo tổng đương lượng; ghi dung tích bể, Q, H bơm khi đã khai báo số người.
- **Sơ đồ ống đứng thoát nước**: ống đứng thoát xí (vào bể tự hoại) và ống đứng thoát rửa (vào hố ga), ống thông hơi trên mái; nhánh tầng xiên 45°; Ø ống đứng theo tổng đương lượng thoát luỹ kế (ống thoát xí tối thiểu Ø110).

### 7.28. Chọc thủng đài cọc, cắt mép móng, cọc chịu nhổ, phản lực dầm móng

Các kiểm tra móng theo **TCVN 5574:2018** (chọc thủng mục 8.1.6, cắt tiết diện nghiêng mục 8.1.3.3, neo thép mục 10.3.5) và **TCVN 10304:2014** (cọc), dùng chung cho chế độ Thiết kế và Triển khai hồ sơ.

- **Đài cọc**:
- Phản lực cọc Pi = (N + G)/n ± Mx·yi/Σy² ± My·xi/Σx² (đài cứng).
- Chọc thủng do cột: tháp 45° từ mép cột tới đầu cọc; cọc có tim nằm trong đáy tháp không gây chọc thủng → F = N − Σ phản lực cọc trong tháp; Fb,u = Rbt·u·h0 (thiết kế: kể hệ số h0/c, chặn 1…2,5).
- Chọc thủng **có kể mô men**: F/Fb,u + ΣM/Mb,u ≤ 1, Mb,u = Rbt·Wb·h0 (Wb của đường bao cách mép cột h0/2); M lấy bằng 0,5 mô men không cân bằng của phản lực các cọc ngoài tháp; phần mô men không lớn hơn F/Fb,u.
- Chọc thủng do **cọc góc / cọc biên**: đường bao cách mép cọc h0/2, bỏ các cạnh ra ngoài mép đài.
- Cắt trên tiết diện nghiêng giữa mép cột và hàng cọc: Q = Σ phản lực cọc ngoài tiết diện ≤ Qb = 1,5·Rbt·b·h0²/c (0,5 ≤ Qb/Rbt·b·h0 ≤ 2,5).
- Bản vẽ mặt cắt đài có **tháp chọc thủng 45°** nét đứt.

- **Cọc chịu nhổ** (khi Pmin < 0):
- [Pk] nhập tay, hoặc nhập **Σ(fi·li)** (ma sát thành theo các lớp đất) → [Pk] = 0,8·u·Σfi·li/1,4 + 0,9·Gcọc (TCVN 10304:2014).
- Thép cọc / râu thép chịu kéo: Nt ≤ Rs·As.
- Neo râu thép vào đài chịu kéo: l0,an = Rs·d/(4·2,5·Rbt), lan = l0,an·As,cal/As,ef ≥ max(0,3l0,an; 15d; 200 mm) – so với chiều dài neo bố trí.
- Ghi chú mối nối cọc (bản mã hàn) phải chịu được lực kéo Nt.

- **Móng đơn** (Triển khai hồ sơ):
- Chọc thủng có kể mô men; móng **sát ranh** (lệch tâm): đường bao 3 cạnh (2 cạnh khi cột ở góc), trọng tâm đường bao lệch e0 → kể thêm F·e0.
- **Cắt theo mép móng** hai phương: Q = p·b·(l1 − c) ≤ Qb(c), xét c tới min(l1; 3h0); móng vát dùng h0 trung bình đoạn công xôn.
- Chiều cao tự chọn tăng tới khi đạt cả chọc thủng có mô men và cắt mép móng; mặt cắt móng vẽ tháp chọc thủng 45°.

- **Dầm móng / giằng móng** (dầm Vị trí = Dầm móng → mục *Dầm móng: tải trọng & phản lực*):
- **Giằng tựa lên móng**: dầm liên tục gối tại tim móng, q = bản thân + tường xây (1,1·18·t·h) + tải khác; phương trình ba mô men → M, Q dùng kiểm tra dầm khi ô nội lực nhập tay = 0, và **phản lực từng gối** ghi trong thông báo.
- **Dầm móng trên nền đàn hồi (Winkler)**: k = 120·R nếu không nhập; lực cột tại gối nhập ở *Lực tập trung tại các gối*; kiểm tra áp lực nền pmax ≤ 1,2R; tổng phản lực nền bằng tổng tải.
- Móng đơn / đài cọc có ô **Phản lực dầm móng / giằng cộng thêm vào N**: nhập ΣR các giằng tựa lên móng đó.
- Chế độ Thiết kế: dầm móng có trong mô hình phân tích thì phản lực chân cột (tải móng) đã gồm tải dầm móng.
- **Tự chuyển phản lực giằng**: ghi tên móng ở ô *Móng tại các gối* (vd. M1; M2; Đ1, gối không có móng để trống) → khi tính toàn bộ hồ sơ, phản lực từng gối tự cộng vào N của móng đơn / đài cọc cùng tên (thuyết minh ghi rõ dầm, gối); ô nhập tay khi đó không cộng thêm (tránh cộng hai lần).

- **Các trường hợp bổ sung**:
- **Móng bậc** (Hình dạng đế móng = Bậc): Hb là chiều cao bậc dưới, bậc trên StepL × StepB (0 = tự chọn) → kiểm tra thêm **chọc thủng tại bậc**: F = N − pnet·(Lb + 2h0b)(Bb + 2h0b) ≤ Rbt·u·h0b, u = 2(Lb + Bb + 2h0b); mặt cắt móng vẽ bậc thẳng đứng, khối lượng bê tông / ván khuôn theo bậc.
- **Cột chọc thủng móng băng**: mỗi cột (thêm ô *Cạnh cột ngang băng*) kiểm tra chọc thủng có kể mô men với h0 của sườn; đường bao bị cắt bởi mép bản đáy và đầu băng (cột ở đầu băng → 3 cạnh).
- **Nhổ khối nhóm cọc** (≥ 2 cọc, có Σfi·li): [Pk,khối] = 0,9·(W khối đất bao nhóm cọc + Gđài) + 0,8·u_khối·Σfi·li/γk ≥ ΣNt các cọc chịu nhổ.

### 7.29. Cọc khoan nhồi mini (D300–600) và cọc khoan nhồi

Đài cọc (Triển khai hồ sơ) → **Loại cọc** = *Cọc khoan nhồi mini (D300–600)* hoặc *Cọc khoan nhồi*: cọc tròn, thép dọc = ô *Râu thép đầu cọc* (số thanh, Ø), bê tông cọc chọn riêng. Tính theo **TCVN 10304:2014**.

- **Sức chịu tải theo đất nền** (bật *Tính [P] cọc theo địa chất*, nhập bảng lớp đất từ mặt đất: bề dày, loại đất, IL, SPT N, cu, γ, E, qb nhập tay):
- Rc,u = γc·(γcq·qb·Ab + u·Σγcf·fi·li), γc = γcq = 1; thân cọc chia đoạn ≤ 2 m.
- *Chỉ tiêu cơ lý*: fi theo **Bảng 3** (độ sâu trung bình z, IL; cát thô/vừa, mịn, bụi), qb mũi trong đất dính theo **Bảng 7** (cọc khoan nhồi, z và IL ≤ 0,6); mũi trong cát dùng qb = 150·N hoặc qb nhập tay.
- *SPT (Phụ lục G.3.2)*: cát fs = 10·N/3, sét fc = αp·cu (αp theo cu/σ'v); mũi cát qb = 150·N, sét qb = 9·cu (cu = 6,25·N nếu không nhập).
- γcf theo công nghệ khoan: dung dịch bentonite 0,6; ống vách / khoan khô 0,7 (sửa được). [P]đ = γ0·Rc,u/(γn·γk) với γ0 = 1,15, γn = 1,15, γk = 1,4 (1,75 khi ít hố khoan).
- Sức chịu tải vật liệu Pvl = φ·(γcb·γ'cb·Rb·Ab + Rsc·As), γcb = 0,85, γ'cb = 0,7 (đổ trong dung dịch / dưới nước), φ = 1 (cọc ngàm hoàn toàn trong đất) → [P] = min([P]đ; Pvl). Chế độ Thiết kế (cọc khoan nhồi theo hố khoan SPT) cũng dùng γcb·γ'cb khi tính Pvl.

- **Kiểm tra bổ sung**:
- Hàm lượng thép dọc μ ≥ 0,4 %, khuyến nghị ≥ 6 thanh.
- **Tải ngang** (ô *Tải ngang tại chân cột H*, K của nền): αbd = (K·bp/(γc·Eb·I))^(1/5), bp = 1,5d + 0,5, γc = 3; đầu cọc ngàm vào đài: M = H·B0/(αbd·C0), y0 = H·(A0 − B0²/C0)/(αbd³·Eb·I) (A0 = 2,441; B0 = 1,621; C0 = 1,751) – kiểm tra M ≤ Mu ≈ Rs·As·2rs/π, y0 ≤ 10 mm.
- **Lún khối móng quy ước**: Bm = Bg + 2L·tan(φtb/4); cộng lún các lớp dưới mũi s = Σ0,8·σz·h/E tới khi σz ≤ 0,2σbt; S ≤ 80 mm.
- **Lồng thép**: dài ≥ max(L/3; 6 m; 4/αbd + 1 m), toàn chiều dài khi cọc chịu nhổ; đai xoắn Ø6 (D ≤ 400) / Ø8 bước 100 vùng đầu cọc (≥ 2 m), 200 thân cọc; đai gia cường Ø16 và con kê 4 cái mỗi 2 m.

- **Bản vẽ & khối lượng**:
- Chi tiết cọc (mặt đứng có lồng thép, hai vùng bước đai, chân lồng thép, cao độ đầu / mũi), mặt cắt cọc, trụ địa chất với chiều dài cọc và [P].
- Thống kê thép cọc (thép dọc, đai xoắn, đai gia cường); bê tông cọc kể hao hụt mở rộng thành lỗ (×1,15), khoan tạo lỗ (m, từ mặt đất), dung dịch bentonite / ống vách.
- Ghi chú thi công và **thử tải**: tremie, bentonite, vét lắng; thử tĩnh ≥ max(1 %; 1 cọc) (TCVN 9393:2012), PIT / siêu âm (TCVN 9396:2012).

- **Cọc khoan nhồi đường kính lớn (D800–1500+) và tính năng nâng cao**:
- *Khoảng cách tim cọc* tối thiểu: cọc mini 3D; D ≥ 800 lấy max(2,5D; D + 1 m). Tim cọc tới mép đài ≥ D/2 + max(0,25 m; D/3). Đài tự chọn dùng các giá trị này.
- *Bê tông B22,5* (nội suy B20 – B25): Rb = 13,0 MPa, Rbt = 0,975 MPa, Eb = 28 750 MPa – chọn được cho cọc và mọi cấu kiện.
- *Ma sát âm*: đánh dấu lớp **Đất yếu cố kết / đắp nền** trong bảng lớp đất. Mặt trung hoà lấy ở đáy lớp cố kết sâu nhất; bỏ ma sát dương phía trên và trừ lực kéo xuống Pn = u·Σfi·li: [P]đ,net = γ0·(Rc,u − ΣQ)/(γn·γk) − Pn.
- *Tự chọn chiều dài cọc* (ô [P] là sức chịu tải yêu cầu): L nhỏ nhất, bước 0,5 m, để [P] ≥ yêu cầu và mũi nằm trong lớp tốt (cát N ≥ 15, sét IL ≤ 0,5 hoặc N ≥ 15, hoặc có qb nhập tay). Không đạt: lấy L có [P] lớn nhất và cảnh báo. Bản vẽ có thêm **đường cong sức chịu tải Rc,u và [P] theo độ sâu mũi cọc**.
- *Hệ số nhóm Converse–Labarre* (tuỳ chọn): η = 1 − θ·[(n1 − 1)·n2 + (n2 − 1)·n1]/(90·n1·n2), θ = arctan(D/s); kiểm tra N + G ≤ η·n·[P].
- *Đài 1 cọc*: cạnh ≥ max(D + 2·mép; cạnh cột + 200), H ≥ max(600; neo râu thép + ngàm + 100). *Đài 2/3/4 cọc* với D ≥ 800 hoặc đài sâu ((r − c/2)/h0 ≤ 1): mô hình **thanh chống – giằng Blévot** – a = r − k·c (k = 0,25 / 0,30 / √2/4), θ = arctan(h0/a) ≥ 40° (nên 45°–55°), giằng T = R·a/h0 (2 cọc), chia √3 (3 cọc), √2 (4 cọc) theo cạnh, As ≥ T/Rs trên dải đầu cọc; ứng suất nút cột ≤ ν'·Rb, nút đầu cọc ≤ 0,85·ν'·Rb, ν' = 1 − fck/250.
- *Lồng thép dài* hơn cây thép 11,7 m: chia đoạn, nối chồng 40d so le 50 % (lệch ≥ 1,3 lần chiều dài nối), thể hiện trên mặt đứng cọc; móc cẩu lồng; con kê 4 / 6 / 8 cái mỗi tầng (D < 800 / < 1200 / ≥ 1200), a2000.
- *Ống siêu âm* thép D60×2 (TCVN 9396:2012): 2 ống khi D800–<1000, 3 ống khi D1000–1500, 4 ống khi > 1500; D ≥ 1000 thêm ống khoan lõi mũi cọc D114. Có trong bảng khối lượng, mặt đứng và mặt cắt cọc.
- *Hệ số hao hụt bê tông gợi ý* theo địa chất thân cọc (đất yếu 1,20; cát 1,15; sét cứng 1,10); *lún nhóm cọc theo móng tương đương* đặt ở 2L/3 để so sánh với khối móng quy ước.
- *Chế độ Thiết kế*: trong hộp thoại Thiết kế móng chọn cọc khoan nhồi và phương pháp TCVN 10304:2014 (Bảng 3/7 hoặc SPT): hố khoan được chia lớp tại mực nước ngầm (γ' = γsat − 10 dưới nước), đất đắp / bùn được coi là cố kết (ma sát âm); có tuỳ chọn tự chọn chiều dài cọc, bê tông cọc riêng và hệ số nhóm. Để trống phương pháp: tính như phiên bản trước.

- **Mặt bằng định vị cọc** (Triển khai hồ sơ):
- Khi có đài cọc đặt trên mặt bằng móng, cây thư mục thêm nút **Mặt bằng định vị cọc** ngay sau Mặt bằng móng; bản vẽ gồm lưới trục, đường bao đài, cọc đánh số P1…Pn (theo đài từ trên xuống, trong đài theo hàng), kể cả đài xoay.
- **Bảng toạ độ cọc** (X, Y theo hệ trục công trình, lực lớn nhất lên cọc) và **bảng thống kê cọc** (đài, D, L, cao độ đầu cọc = −Df + ngàm, cao độ mũi = −Df − L, [P], số lượng) – dùng cho định vị cọc ngoài hiện trường.

### 7.30. Sàn hạ cốt (hạ cos)

Ô sàn có mặt trên thấp hơn cao độ sàn tầng một khoảng Δ (âm = hạ; trong khoảng −500 … 0 mm): sàn vệ sinh (chống thấm + vữa tạo dốc; WC hạ sâu đặt xí bệt âm −100 … −300 mm), ban công, lô gia, sân thượng, bếp…

1. Thanh sửa mặt bằng: nhập độ hạ ở ô **Hạ cốt (mm)** (vd. −50; gõ 50 cũng hiểu là hạ 50 mm).
2. Chọn chế độ **Hạ cốt sàn** rồi bấm vào từng ô sàn → ô được tô gạch chéo, viền nét đứt và ghi "▽ −0.050 (−50)"; bấm lại ô đang có đúng Δ đó để **bỏ** hạ cốt. Góc dưới trái mặt bằng hiện chú thích các mức hạ của tầng.
3. Gán hàng loạt: nút **Hạ cốt sàn…** → hộp **Hạ cốt sàn (sàn vệ sinh, ban công, lô gia…)**: **Áp dụng cho** (Ô đang chọn / Mọi ô cùng công năng – tầng đang xem / mọi tầng / Mọi ô đang hạ cốt), **Hạ cốt Δ (mm)** (0 = bỏ), **Ghi chú** (lý do, in lên bản vẽ và thuyết minh).
4. Cùng hộp: bảng **Hạ cốt mặc định theo công năng** (Phòng ở 0; **Vệ sinh −50**; **Ban công, lô gia −50**; Hành lang, Kho, Mái 0 – sửa được), ô **Tự đặt hạ cốt mặc định khi đổi công năng** và **Ngưỡng bậc sàn (mm)** (mặc định 30). → **Đồng ý** (hoàn tác Ctrl+Z).

- Mặc định chỉ áp dụng khi **đổi** công năng ô sàn (nút Sàn vệ sinh, thêm ô sàn, ban công) – ô đã chỉnh tay giữ nguyên, mở dự án cũ không đổi gì.
- Phân tích: tải sàn giữ nguyên, nút ô sàn vẫn ở cao độ tầng (Δ là thông tin hình học / cấu tạo).
- Thiết kế sàn: cạnh chung giữa hai ô chênh cốt > ngưỡng bậc tính **khớp** (không liên tục mô men qua bậc); mỗi ô có thép mũ cấu tạo riêng neo vào dầm, không có thép mũ / lưới trên vắt liên tục qua bậc.
- Dầm đỡ ô hạ cốt: kiểm tra **h ≥ hs1 + Δ + hs2** – không đạt thì cảnh báo và đề nghị chiều cao dầm (tab Kiểm tra mô hình, thuyết minh mục sàn).
- Bản vẽ: mặt cắt dầm vẽ bậc sàn hai bên theo đúng cao độ; mặt bằng kết cấu đánh dấu "SÀN HẠ COS −50" + gạch chéo; bảng khối lượng tính đúng cao độ.

### 7.31. Tường – loại tường, vẽ và gán tường

- **Khai báo → Loại tường…**: thư viện **Loại tường** – tên, chiều dày, γ xây, hoàn thiện, trát mỗi mặt, γ trát, tỷ lệ cửa (%), tải tường đặc (kN/m²). Tải 1 m² tường đặc = dày × γ xây + số mặt trát × dày trát × γ trát; tải lên dầm = tải 1 m² × chiều cao × (1 − tỷ lệ cửa). **Khôi phục thư viện mặc định** nếu lỡ xoá.
- **＋ Tường**: chọn **Loại tường**, **Cao (m)** (0 = tự động: chiều cao tầng trên − chiều cao dầm), **Diện tích cửa (m²)** (trống = theo tỷ lệ cửa của loại tường; 0 = không có cửa) rồi bấm 2 điểm.
- **Gán tường**: bấm vào dầm để đặt tường suốt chiều dài dầm (thay tường cũ trùng vị trí). Tường gán lên dầm cong được coi là tường cong theo dầm.
- **Vẽ → Tường đã khai báo…**: bảng mọi đoạn tường – sửa loại, chiều cao, diện tích cửa, xoá.
- Tường 110 vẽ nhạt, 220 trung bình, 330 đậm; tường không trát vẽ viền nét đứt. Tải tường dồn vào dầm bên dưới (nguồn tải WALL).

### 7.32. Chọn, xoá, chia thanh

- Chế độ **Chọn**: bấm chọn cột, dầm, ô sàn, lỗ sàn; rê chuột sẽ sáng nhẹ đối tượng dưới con trỏ. Thông tin hiện ở ô dưới khung nhìn.
- Chế độ **Xoá** hoặc phím **Delete**: xoá tường, dầm, cột (hỏi lại), ô sàn, lỗ sàn.
- **Sửa → Chia thanh tại nút**: chia các thanh tại nút trung gian nằm trên thanh (sau khi nhập mô hình từ nguồn khác).
- Hoàn tác: Ctrl+Z (30 bước).

### 7.33. Chọn đối tượng (menu Chọn)

- **Chọn → Chọn từng đối tượng** (Esc) và **Chọn → Chọn nhiều (bấm / kéo khung)** – bấm để thêm/bớt, kéo khung trái → phải chọn đối tượng nằm trọn, phải → trái chọn đối tượng chạm khung; giữ Ctrl để bỏ chọn.
- Mọi dầm / mọi cột / **Chọn → Mọi thanh tầng đang xem** của tầng đang xem (**Ctrl+A** khi đang ở khung nhìn Mặt bằng và không gõ chữ trong ô nhập).
- **Chọn → Theo tiết diện…**: chọn mọi thanh dùng một tiết diện; **Chọn → Bỏ chọn**.
- Các lệnh menu **Gán** áp dụng cho đối tượng đang chọn; cột đang chọn nhiều được tô viền trên mặt bằng.

### 7.34. Gán tải trọng, liên kết, giải phóng, tiết diện (menu Gán)

Chọn đối tượng trước (*mục 7.33 (Chọn đối tượng (menu Chọn))*) rồi dùng menu **Gán** (như Assign của ETABS). Mọi thao tác hoàn tác được bằng Ctrl+Z; tải gán là **tải nhập tay** (khác tải dồn từ sàn / tường được sinh lại mỗi lần phân tích).

| Lệnh | Nội dung |
|---|---|
| **Gán → Tải trên thanh (đều / hình thang / tập trung)…** | Hộp **Gán tải trên thanh**: trường hợp tải; phương (Trọng lực −Z, dương hướng xuống; phương tổng thể hoặc địa phương: trục 1 dọc thanh I→J, trục 2 hướng lên, trục 3 = 1×2); dạng tải **Phân bố đều** (q1), **Hình thang / tam giác** (Điểm 1, Điểm 2: khoảng cách + q), **Lực tập trung** (P, khoảng cách); khoảng cách tương đối (0…1) hoặc tuyệt đối (m từ đầu I); cách gán **Cộng thêm vào tải đã có / Thay thế tải nhập tay cùng trường hợp / Xoá tải nhập tay cùng trường hợp**. |
| **Gán → Tải trên sàn…** | Tải phân bố đều lên **Ô sàn đang chọn / Mọi ô sàn tầng đang xem / Mọi ô sàn của công trình**; tải sàn được dồn về dầm theo đường phân giác 45° khi phân tích. |
| **Gán → Tải tại nút…** | Lực, mô men tại nút trong hệ toạ độ tổng thể (Fz dương hướng lên – tải trọng lực nhập số âm). |
| **Gán → Liên kết nút (ngàm / khớp / tự do)…** | Nút **Ngàm** (giữ 6 bậc tự do), **Khớp** (UX, UY, UZ), **Con lăn (UZ)**, **Tự do**; nút nhanh **Mọi nút chân cột thấp nhất**. Gối thật (chân cột / móng) cần giữ UZ. |
| **Gán → Giải phóng đầu thanh…** | Giải phóng mô men đầu thanh: **Khớp đầu I**, **Khớp đầu J**, **Khớp hai đầu (M2, M3)**, **Không giải phóng** (vd. dầm phụ gác khớp lên dầm chính). |
| **Gán → Tiết diện thanh…** | Gán tiết diện cho mọi thanh đang chọn (dầm và cột). |
| **Gán → Hệ số độ cứng thanh…** | Hệ số độ cứng thanh (nhân thêm với hệ số của tiết diện): nút **Dầm nứt (I33 = 0,35)**, **Cột nứt (I = 0,7)**, **Đặt lại 1** – chỉ ảnh hưởng độ cứng khi phân tích. |
| **Gán → Điểm chèn / dời ngang…** | Điểm chèn / dời ngang tiết diện (*mục 7.10 (Công cụ dầm kiểu ETABS)*). |
| **Gán → Sàn cứng tầng đang xem…** | Đánh dấu sàn tuyệt đối cứng cho tầng (*mục 7.35 (Khai báo trường hợp tải, trường hợp phân tích, phổ phản ứng, sàn cứng (menu Khai báo))*). |
| **Gán → Gán tường lên dầm**, **Gán → Gán cấu tạo sàn**, **Gán → Sàn vệ sinh** | Chế độ bấm trên mặt bằng như nút **Gán tường**, **Gán cấu tạo**, **Sàn vệ sinh**. |

**Hiển thị → Hiển thị tải gán**: vẽ tải nhập tay trên mặt bằng (đa giác tải + mũi tên + giá trị cho tải thanh, giá trị tại tâm ô cho tải sàn, mũi tên cho tải nút), lọc theo trường hợp tải chọn trên cây mô hình hoặc ở ô **Biểu đồ M3 theo**.

### 7.35. Khai báo trường hợp tải, trường hợp phân tích, phổ phản ứng, sàn cứng (menu Khai báo)

- **Khai báo → Trường hợp tải (Load Patterns)…**: hộp **Trường hợp tải (Load Patterns)** – tên, loại (Dead = tĩnh tải có trọng lượng bản thân hệ số 1, SuperDead = hoàn thiện / tường, Live / RoofLive = hoạt tải, Wind = gió, Seismic = động đất), hệ số trọng lượng bản thân, γf; thêm, đổi tên (cập nhật mọi tham chiếu), xoá.
- **Khai báo → Trường hợp phân tích (Load Cases)…**: hộp **Trường hợp phân tích (Load Cases)** – mỗi trường hợp tải được giải bằng một trường hợp phân tích tĩnh tuyến tính cùng tên (như ETABS); tổ hợp cộng tuyến tính kết quả; nút **Nguồn khối lượng & dao động…** và **Phổ phản ứng / động đất…**.
- **Khai báo → Nguồn khối lượng & dao động riêng…**: nguồn khối lượng và dao động riêng (*mục 9.2 (Dao động riêng (tab Dao động))*).
- **Khai báo → Phổ phản ứng / động đất…**: hộp **Phổ phản ứng / động đất (TCVN 9386-1:2025)** – **Tính động đất (trường hợp DDX, DDY)**, gia tốc nền agR (g), loại nền, hệ số ứng xử q, hệ số tầm quan trọng γI, hệ số tổ hợp hoạt tải ψE, phương pháp; đồ thị phổ thiết kế Sd(T).
- **Khai báo → Sàn cứng (diaphragm) theo tầng…** / **Gán → Sàn cứng tầng đang xem…**: **Dùng sàn tuyệt đối cứng trong mặt phẳng** (chung cho phân tích tĩnh và dao động); tầng được đánh dấu: các nút sàn cùng cao trình chuyển vị ngang và xoay quanh Z như một khối (nút chủ tại trọng tâm).
- Tổ hợp tải trọng: *mục 8.8 (Sửa tổ hợp tải trọng (Load Combinations))*.

### 7.36. Dầm có cánh sàn (chữ T / L) trên bản vẽ

- Bản vẽ chi tiết dầm (KC-04) thể hiện **cánh sàn liền khối theo mô hình** (kiểu Kết cấu Soft / XSCad): chữ **T** khi có sàn hai bên, chữ **L** ở dầm biên, chữ nhật khi không có sàn (lỗ cầu thang, giếng trời); ghi kích thước hs, đường cắt ở đầu cánh; mặt đứng dầm có nét mép dưới sàn. Cánh lấy theo ô sàn thực tế hai bên từng mặt cắt (lỗ sàn, độ lệch tim dầm so với trục).
- Tuỳ chọn ở **Tuỳ chọn → Thiết lập bản vẽ (tỷ lệ, cỡ chữ)…** (tab **Tỷ lệ, cỡ chữ, chú thích**): **Cánh sàn trên mặt cắt dầm** (bật/tắt), **Thép sàn trong cánh** (thép lớp dưới, thép mũ vắt qua dầm, thép phân bố, nhãn "Ø8a200 (sàn)" theo kết quả thiết kế sàn; chưa thiết kế sàn thì vẽ quy ước nét đứt), **Chiều dài cánh sàn thể hiện (mỗi bên dầm)** (30…3000 mm, mặc định 50 – đoạn sàn ngắn chỉ để nhận biết dầm chữ T / L), **Đường cắt đầu cánh sàn**: Dích dắc / Nét thẳng.

> **Cảnh báo:** **Thiết kế** dầm vẫn theo tiết diện chữ nhật b×h (tính uốn bằng tiết diện chữ nhật – không kể cánh chịu nén), thiên về an toàn. Cánh sàn chỉ là thể hiện bản vẽ.

### 7.37. Địa chất – hố khoan (tab Địa chất)

- **Hố khoan khảo sát địa chất**: **+ Hố khoan**, **Nhân bản**, **Xoá**; tên, toạ độ X, Y, cao độ miệng, mực nước ngầm sâu (−1 = không gặp).
- **Các lớp đất** (độ sâu tính từ miệng hố khoan): loại đất, chiều dày/độ sâu, γ, γsat, φ, c, IL (−1 = đất rời), SPT N…; **+ Lớp**, **Xoá lớp**, **Sắp xếp / nối liền**, **Chọn theo đất tham khảo…** (khoảng giá trị điển hình, giá trị đại diện, tương quan SPT).
- **Hình trụ hố khoan** vẽ tô theo loại đất.
- **Áp dụng cho thiết kế móng**: tích **Dùng hố khoan cho thiết kế móng** (móng đơn, móng băng lấy φ, c, γ lớp đặt móng và kiểm tra lún; móng cọc dùng hố khoan gần chân cột nhất); **Gán chỉ tiêu lớp đặt móng → tab Công trình**; **Mặc định nền đất tốt khi chưa có khảo sát**; **Đặt lại nền đất tốt giả định**.
- **Gợi ý phương án móng…**: chương trình đề xuất móng đơn / băng / cọc theo hố khoan và tải chân cột.

> **Lưu ý:** Không có hố khoan: thiết kế móng dùng chỉ tiêu nhập tay ở tab Công trình → **Nền đất dưới móng đơn (TCVN 9362:2012)** (φ, c, γ, Df, R tiêu chuẩn nhập tay – 0 = tính).

### 7.38. Móng – loại móng và tab Mô hình móng

- Tab **Mô hình móng**: mặt bằng móng (lưới trục, cột, móng đơn L×B, đài cọc, móng băng, lực dọc N, màu đạt/không đạt) và mặt cắt móng đang chọn (bản đế vát, cổ móng, cốt ±0.000, thép lớp dưới). Bật/tắt **Ghi kích thước móng**.
- **Loại móng** (cả công trình): Móng đơn, Móng cọc, Móng băng. **Tại chân cột này**: chọn một móng trên mặt bằng rồi đặt loại riêng (Theo công trình / Móng đơn / Móng cọc / Móng băng).
- **Thông số…** (**Thiết kế → Thiết kế móng…**): hộp thoại **Thiết kế móng** – móng cọc (cọc vuông/tròn, cạnh/đường kính, chiều dài, [P] nén, [P] kéo, khoảng cách tim cọc, mép đài, đoạn ngàm, chiều cao đài tối thiểu, tính [P] và chiều dài cọc từ hố khoan theo SPT – TCVN 10304:2014, cọc khoan nhồi hay ép/đóng, FS, [H]); móng băng (phương dải: tự chọn, dọc trục X, dọc trục Y, giao thoa 2 phương; bề rộng sườn; hệ số nền); tuỳ chọn bản vẽ (tháp chọc thủng 45°, biểu đồ mô men sườn).
- **Thiết kế móng**: thiết kế lại toàn bộ móng; **Kiểm tra an toàn…**: bảng kiểm tra an toàn và sự làm việc của móng đang chọn.

Thiết kế và ý nghĩa kết quả móng: *mục 10.5 (Móng – thiết kế và kiểm tra)*.

### 7.39. Móng cọc – bố trí cọc

1. Tab **Mô hình móng**: chọn **Loại móng** = Móng cọc (hoặc đặt riêng chân cột), khai báo cọc ở **Thông số…** → Thiết kế móng.
2. Chọn một đài trên mặt bằng móng → **Bố trí cọc…**: chọn số cọc, **Sơ đồ bố trí cọc**, khoảng cách cọc, kích thước / chiều cao đài (**Thông số bố trí (0 = tự chọn)**). Bảng kiểm tra an toàn cập nhật trực tiếp.
3. **Áp dụng** (hoàn tác được) hoặc **Tự chọn (bỏ bố trí riêng)** để trả về bố trí tự động.

> **Lưu ý:** [P] = min(Rc,u/FS; Pvl), Pvl = Rb·Ab + Rsc·As. Khi tính từ hố khoan: mũi tự chọn tại mặt lớp đầu tiên có N ≥ 30 (đất rời) / N ≥ 20 (đất dính) + 3d, ≤ 40 m. [P] nên lấy theo thí nghiệm nén tĩnh khi có.

### 7.40. Móng lệch tâm, ranh giới đất, lùi cọc

- **Ranh giới đất…**: chọn các cạnh ranh (trái / phải / trước / sau) theo trục + khoảng cách ra phía ngoài lô (vd. cột 220 tim trục sát ranh: 110 mm), khoảng hở mép móng – ranh, dung sai nhận cột sát ranh, phương án lệch tâm mặc định; xem trước (nét đứt đỏ là ranh giới, cột sát ranh tô cam). Móng sát ranh tự lệch tâm, không vượt ranh.
- **Lệch tâm…** cho chân cột đang chọn: (a) Có dầm móng cân bằng, (b) Lệch tâm tự cân bằng, (c) Móng kết hợp, hoặc Tự chọn phương án; có thể nhập trực tiếp độ lệch tâm tâm móng – tim cột; kích thước dầm cân bằng / móng kết hợp.
- **Lùi cọc khỏi ranh giới** (móng cọc, trong hộp Ranh giới đất): khoảng lùi chung đo tới tim / mép cọc theo phương pháp ép, khoảng lùi riêng từng cạnh (trống = theo chung, 0 = phía không có nhà bên cạnh), tuỳ chọn mép đài. Mặt bằng móng vẽ đường giới hạn tim cọc.
- Bản vẽ móng thể hiện ranh giới đất, móng lệch tâm, dầm móng cân bằng (DM b×h).

### 7.41. Gia cố nền bằng cừ tràm

- Nút **Cừ tràm…** trên tab Mô hình móng (hộp thoại không chặn): chiều dài cừ, Ø gốc / ngọn, mật độ, bê tông lót, cát đệm, MNN thấp nhất, FS, η, αs, chỉ tiêu sau gia cố, phương pháp tính.
- **Phạm vi áp dụng**: cả công trình (móng đơn + móng băng) hoặc riêng chân cột đang chọn.
- Kết quả tức thời của hai phương pháp cho móng đang chọn (hoặc móng thử 1,2 × 1,2 m), bảng kiểm tra tô màu và cảnh báo; **Áp dụng + thiết kế lại** (hoàn tác được).

> **Cảnh báo:** Giá trị mặc định là kinh nghiệm thực hành – người thiết kế kiểm tra. Đầu cừ phải nằm dưới mực nước ngầm thấp nhất.

## 8. Tải trọng và tổ hợp

### 8.1. Trường hợp tải (tab Tải trọng)

Bảng **Trường hợp tải** (+ Thêm, Xoá) gồm tên, loại tải, hệ số trọng lượng bản thân, hệ số độ tin cậy γf. Dự án mới có sẵn:

| Tên | Loại | γf mặc định | Ghi chú |
|---|---|---|---|
| TT | Tĩnh tải | 1,1 | Có trọng lượng bản thân dầm, cột, sàn (hệ số 1). |
| HTHT | Hoàn thiện | 1,3 | Lớp hoàn thiện, tường, cấu tạo sàn. |
| HT | Hoạt tải | 1,3 | Hoạt tải sàn, mái theo công năng. |
| GX, GY | Gió | 1,2 | Sinh tự động khi bật Tính tải gió. |
| DDX, DDY | Động đất | 1,0 | Thêm khi bật Tính động đất. |

> **Lưu ý:** γf là lựa chọn mặc định của chương trình – người dùng sửa theo TCVN 2737:2023 và loại tải của công trình.

### 8.2. Tải sàn theo công năng và cấu tạo sàn

- Tab Công trình → **Hoàn thiện sàn**, **Hoạt tải sàn**, **Hoạt tải mái** (giá trị tiêu chuẩn, kN/m²) dùng khi tạo mô hình.
- Công năng ô sàn (Phòng ở, Vệ sinh, Ban công, Hành lang, Kho, Mái) và cấu tạo sàn gán cho ô (*mục 7.12 (Sàn và cấu tạo sàn)*) thay đổi hoàn thiện, hoạt tải của ô.
- Tải sàn được dồn về dầm theo đường phân giác 45° (tam giác/hình thang); bật **Hiện tải tự sinh (sàn)** ở bảng **Tải trên thanh** để xem.

### 8.3. Tải tường

- Tường biên: ô **Tường biên trên dầm (kN/m)** ở tab Công trình (dùng khi tạo mô hình). Ví dụ tường 220 gạch đặc cao 3 m ≈ 0,22·18·(3 − 0,3) ≈ 10,7 kN/m.
- Tường khai báo theo loại tường (*mục 7.31 (Tường – loại tường, vẽ và gán tường)*): tải tính theo chiều dày, trát, chiều cao, diện tích cửa và dồn lên dầm bên dưới.

### 8.4. Tải trên thanh và tải trên sàn nhập tay

- **Tải trên thanh (kN/m)**: chọn thanh trên mặt bằng → **+ Cho thanh đang chọn**; sửa trường hợp tải, phương, các điểm tải trong bảng; **Xoá**.
- **Tải trên sàn (kN/m²)**: **+ Cho các ô sàn tầng đang xem** rồi sửa giá trị, trường hợp tải.
- Tải tự sinh (sàn, tường, cầu thang, lỗ sàn) được chương trình lập lại mỗi khi mô hình thay đổi – sửa ở nguồn (công năng, cấu tạo sàn, tường), không sửa trực tiếp.

### 8.5. Gió – địa danh, vùng gió

1. Tab Công trình → nhóm **Địa điểm xây dựng – vùng gió, động đất (QCVN 02:2022/BXD)**: **Tỉnh/Thành phố (từ 01/7/2025)** – 34 tỉnh/thành sau sắp xếp, gõ được tên tỉnh cũ (vd. Bắc Kạn → Thái Nguyên), không dấu cũng được.
2. **Xã/phường (huyện cũ)**: chọn từ danh mục hoặc gõ tên mới; **Vùng áp lực gió** I…V → W0 tự điền.
3. **Lưu địa danh** để lưu tỉnh, địa danh, vùng gió, agR, loại nền vào danh mục của bạn; **Danh mục địa danh…** (**Tuỳ chọn → Danh mục địa danh (QCVN 02:2022)…**) để xem/sửa, **Nhập từ Excel/CSV…** Bảng 5.1, 6.1, 6.2 QCVN 02:2022, **Xuất CSV mẫu…**, **Dùng cho công trình**.
4. Nhóm **Gió (TCVN 2737:2023)**: **Tính tải gió** (Bật), **Áp lực gió W0 (kN/m²)**, **Dạng địa hình** A/B/C, **Hệ số khí động tổng Cp** → **Áp dụng thông số**.

> **Cảnh báo:** Chương trình chỉ có sẵn bảng W0 theo vùng gió I–V và danh sách 34 tỉnh/thành; phân vùng chi tiết đến xã/phường do người dùng nhập – **đối chiếu QCVN 02:2022 cho địa danh cụ thể**. Chưa tính thành phần động của gió.

### 8.6. Động đất (TCVN 9386-1:2025)

- Nhóm **Động đất**: **Tính động đất** (thêm trường hợp DDX, DDY), **Gia tốc nền agR (g)** (tra QCVN 02:2022 Bảng 6.1, 6.2 theo địa danh), **Loại nền đất** A…E, **Hệ số ứng xử q**.
- Mặc định: phương pháp tĩnh lực ngang tương đương, T1 = Ct·H^¾, phổ thiết kế loại 1. Có thể dùng phổ phản ứng sau phân tích dao động riêng (*mục 9.3 (Phổ phản ứng)*).
- Tổ hợp đặc biệt: G + ψE·Q ± E1 ± 0,3·E2.

> **Lưu ý:** Tải trọng động đất tính theo TCVN 9386-1:2025 (thay thế TCVN 9386:2012; cùng cơ sở EN 1998-1 với TCVN 9386:2012) bằng phương pháp tĩnh lực ngang tương đương, phổ thiết kế loại 1 – người dùng đối chiếu phổ phản ứng, hệ số tầm quan trọng, gia tốc nền theo bản 2025.

### 8.7. Tổ hợp tải trọng

**Khai báo → Tự tạo tổ hợp theo TCVN 2737:2023** (hoặc nút **Tạo tổ hợp tải** ở tab Tải trọng) tự sinh tổ hợp theo TCVN 2737:2023:

- **TH1, TH2, …** – TTGH I: tổ hợp cơ bản 1 (tĩnh tải + 1 tải tạm thời, đủ γf) và tổ hợp cơ bản 2 (tĩnh tải + ≥ 2 tải tạm thời nhân ψ = 0,9); tổ hợp đặc biệt có động đất.
- **TC1, TC2, …** – TTGH II: giá trị tiêu chuẩn (hệ số 1,0) dùng cho võng, nứt, nền móng.
- **BAO** – tổ hợp bao các tổ hợp TTGH I (biểu đồ vẽ cả max và min).
- Bảng **Tổ hợp tải trọng** ở tab Tải trọng hiện công thức từng tổ hợp (vd. 1.1TT + 1.3HTHT + 1.2HT).

> **Mẹo:** Tạo lại tổ hợp sau khi thêm/xoá trường hợp tải hoặc bật/tắt gió, động đất.

### 8.8. Sửa tổ hợp tải trọng (Load Combinations)

1. **Khai báo → Tổ hợp tải trọng…**: cửa sổ **Tổ hợp tải trọng (Load Combinations)** (không chặn) – danh sách tổ hợp bên trái.
2. **＋ Mới** / **Sao chép** / **Xoá** tổ hợp; sửa **Tên tổ hợp**, **Loại**: TTGH I (tổ hợp tính toán – thiết kế cường độ), TTGH II (tổ hợp tiêu chuẩn – võng, nứt, lún), Bao (max/min của các thành phần).
3. Thành phần: **＋ Dòng**, **Xoá dòng**, **Thêm mọi trường hợp tải** – mỗi dòng là một trường hợp tải hoặc một tổ hợp khác × hệ số.
4. **Lưu tổ hợp** / **Bỏ sửa**. **Tự sinh TCVN 2737:2023** sinh lại bộ tổ hợp chuẩn – tích **giữ tổ hợp tự đặt** để không mất tổ hợp do bạn thêm.

Chương trình kiểm tra tham chiếu (trường hợp / tổ hợp không còn tồn tại); mọi thao tác hoàn tác được. Lệnh nhanh **Khai báo → Tự tạo tổ hợp theo TCVN 2737:2023** chỉ tự sinh theo tiêu chuẩn (*mục 8.7 (Tổ hợp tải trọng)*).

### 8.9. Nguồn khối lượng (cho dao động, động đất)

**Khai báo → Nguồn khối lượng & dao động riêng…** → **Nguồn khối lượng (Mass Source)**: khối lượng = Σ hệ số × tải theo phương trọng lực / g. Mặc định: tĩnh tải × 1,0 + hoạt tải × ψE (ψE = φ·ψ2 = 0,8 × 0,3 = 0,24; mái 0). Trường hợp có trọng lượng bản thân (TT) gồm dầm, cột, sàn; tường và hoàn thiện dồn lên dầm được quy về nút. Xem *mục 9.2 (Dao động riêng (tab Dao động))*.

## 9. Phân tích và đọc kết quả

### 9.1. Chạy phân tích

- **Phân tích → Chạy phân tích** – phím **F5** hoặc nút **▶ Phân tích**. Chương trình lập ma trận độ cứng khung không gian (sàn cứng trong mặt phẳng), giải mọi trường hợp tải và tổ hợp; thanh trạng thái báo số phương trình, trường hợp tải, số cảnh báo.
- Nếu đã bật dao động riêng trong **Khai báo → Nguồn khối lượng & dao động riêng…**, phân tích dao động chạy cùng lúc.
- Nếu kết quả hiện hành là kết quả nhập từ ETABS/SAP, chương trình hỏi trước khi thay bằng kết quả tự tính.
- Mô hình rỗng hoặc kết cấu biến hình: chương trình báo lỗi rõ ràng (*mục 9.12 (Mô hình không ổn định, lỗi phân tích)*).

### 9.2. Dao động riêng (tab Dao động)

1. **Khai báo → Nguồn khối lượng & dao động riêng…**: tích **Chạy phân tích dao động riêng cùng phân tích tĩnh (F5)**.
2. **Nguồn khối lượng**: dùng mặc định hoặc bảng trường hợp tải × hệ số (Thêm dòng / Xoá dòng); **Sàn tuyệt đối cứng (diaphragm)**; **Kể khối lượng theo phương đứng (UZ)**.
3. **Số dạng dao động** (0 = tự động max(12; 3 × số tầng)), **Phương pháp giải trị riêng** (tự động: giải đầy đủ khi ≤ 800 bậc tự do động, ngược lại lặp không gian con).
4. **Động đất**: phương pháp (tĩnh lực ngang tương đương / phổ phản ứng), tổ hợp dạng CQC / SRSS, tỷ số cản ζ, xoắn ngẫu nhiên e·L, hiệu chỉnh lực cắt đáy phổ (tuỳ chọn), dùng T1 từ dao động cho phương pháp tĩnh (giới hạn ≤ k × Ct·H^0,75) → Đồng ý, F5.
5. Khung nhìn **Dao động**: chọn **Dạng**, **Hiển thị**, **Tầng**, **Biên độ**; nút **⏸ Dừng** / chạy hoạt hình; **Thiết lập…** mở lại hộp thoại. Các tab dưới: **Tỷ lệ khối lượng tham gia** (như Modal Participating Mass Ratios của ETABS), **Khối lượng tầng**, **Phổ phản ứng**, **Ghi chú**.

### 9.3. Phổ phản ứng

Chọn phương pháp **phổ phản ứng** trong **Khai báo → Nguồn khối lượng & dao động riêng…**: DDX/DDY là tổ hợp CQC/SRSS các dạng dao động – giá trị **không dấu**; các tổ hợp G + ψE·Q ± E1 ± 0,3·E2 lấy cả hai dấu như cặp bao +/− của ETABS. Có thể hiệu chỉnh lực cắt đáy phổ ≥ k × lực cắt đáy tĩnh (tuỳ chọn, TCVN 9386 không bắt buộc). Mặc định vẫn là tĩnh lực ngang tương đương.

### 9.4. Tab Kết quả

- **Trường hợp / tổ hợp** và **Tầng**: lọc bảng nội lực các thanh (P, V2, V3, T, M2, M3 tại các mặt cắt – cột giống bảng ETABS).
- **Phản lực chân cột (kN, kN.m)**: FX, FY, FZ, MX, MY, MZ từng nút chân cột. Kiểm tra nhanh: tổng FZ theo tĩnh tải bằng tổng tải đứng.
- **Cảnh báo / thông tin**: thông báo của phân tích (mô hình, tải, sàn, động đất…) – đọc kỹ trước khi thiết kế.

### 9.5. Biểu đồ nội lực

- Tab **Biểu đồ nội lực**: **Đại lượng** M3, M2, V2, V3, N, T; trường hợp/tổ hợp theo ô **Biểu đồ M3 theo** (BAO vẽ cả max và min).
- **Khung nhìn**: Mặt bằng tầng; Mặt đứng theo trục (chọn **Trục** – mô men vẽ phía thớ chịu kéo); 3D.
- **Tỉ lệ** tung độ, **Ghi giá trị**, **Vừa màn hình**; đỏ = dương, xanh = âm.
- Chọn một thanh ở bất kỳ khung nhìn nào: biểu đồ của thanh hiện ở ô dưới khung nhìn.

### 9.6. Chuyển vị, phản lực, lực ngang tầng

Chuyển vị nút, phản lực, lực ngang tầng (gió, động đất) và kết quả dao động có trong **Bảng tính Excel** (**Triển khai hồ sơ → Bảng tính Excel...**: các sheet Chuyen vi, Phan luc, Luc ngang tang, Dao động riêng, Noi luc) và **Nội lực CSV** (**Tệp → Xuất mô hình → Nội lực CSV...**: Element Forces – Frames, Joint Reactions, Joint Displacements, Story Forces theo dạng bảng ETABS). Chuyển vị đỉnh, võng được kiểm tra trong thiết kế (*mục 10.1 (Thiết kế BTCT – quy trình chung)*).

### 9.7. Nhập kết quả ETABS / SAP2000 / SAFE / phần mềm khác

1. **Phân tích → Nhập kết quả nội lực (ETABS/SAP/SAFE/khác)…**: chọn một hoặc nhiều file bảng kết quả (.xlsx, .csv, .txt, .s2k, .$2k, .$et, .$sf).
2. Tab **Bảng dữ liệu**: chương trình nhận diện bảng (nội lực thanh, phản lực nút, chuyển vị nút, phản lực tổng, chuyển vị lệch tầng, toạ độ nút, nối thanh, dải sàn SAFE, mô men phần tử sàn, áp lực đất nền, chọc thủng), số dòng, đơn vị.
3. Tab **Trường hợp / tổ hợp**: phân loại từng trường hợp – TTGH I (thiết kế), TTGH II (tiêu chuẩn), TTGH I + II, Trường hợp tải, Bỏ qua (tự nhận theo tên; tổ hợp bao Max/Min được tách thành "Tên (Max)", "Tên (Min)").
4. Tab **Khớp cấu kiện**: khớp thanh theo tên gốc, tên xuất VXT hoặc toạ độ; dòng KHÔNG KHỚP ghi lý do. Tuỳ chọn lấy nội lực của chương trình cho thanh không có trong file.
5. Tab **Ánh xạ cột (phần mềm khác)** cho CSV tự do (trạm đo tương đối, đổi dấu M3, đổi dấu P); tab **SAFE / sàn, móng**; tab **Xem trước M3**.
6. **Áp dụng** → bộ nội lực nhập trở thành kết quả hiện hành: thiết kế, biểu đồ, bản vẽ, thuyết minh dùng như kết quả của chương trình.

> **Cảnh báo:** Kết quả nhập **không được lưu vào file dự án** – mở lại dự án phải nhập lại. Đặt tên trường hợp tải/tổ hợp trong ETABS giống trong chương trình để ghép tự động.

### 9.8. So sánh với ETABS / SAP2000 / SAFE

1. **Phân tích → So sánh với ETABS/SAP/SAFE…**: nếu chưa có kết quả tự tính, chương trình chạy phân tích trước (kết quả hiện hành **không bị thay thế**).
2. Chưa có mô hình ETABS: bấm **Xuất mô hình sang ETABS (.e2k)…** hoặc **SAP2000 (.s2k)…**, chạy trong ETABS/SAP; xuất bảng: Display → Show Tables → Analysis Results: Element Forces – Beams/Columns (SAP: Frames), Joint Reactions, Base Reactions, Joint Displacements, Story Drifts, Modal Periods → File → Export to Excel (.xlsx) hoặc CSV.
3. **Thêm file kết quả…** → **Nhận diện**; chọn **Đơn vị khi bảng không ghi đơn vị**, **Ngưỡng cảnh báo (%)**, **không đạt (%)**.
4. Tab **Ghép trường hợp**: ghép trường hợp/tổ hợp ngoài ↔ của chương trình (tự theo tên, sửa được).
5. Tab **Cấu kiện**: sai lệch lớn nhất từng thanh – xanh ≤ ngưỡng cảnh báo, cam cảnh báo, đỏ vượt ngưỡng, xám thiếu số liệu; lọc theo tầng/loại, **Chỉ hiện cấu kiện vượt ngưỡng cảnh báo**; nhấp đúp → biểu đồ M3 hai phía.
6. Tab **Phản lực, chuyển vị, dao động** và **Tổng hợp & nguyên nhân**; xuất báo cáo Excel + Word.

Nguyên nhân lệch thường gặp: xem *mục 14.3 (Kết quả lệch so với ETABS – nguyên nhân)*.

### 9.9. Bảng kết quả (Show Tables)

- **Hiển thị → Bảng kết quả…** (**Ctrl+T**, nút **Bảng kết quả**): cửa sổ không chặn; tự chạy phân tích nếu chưa có kết quả. Tiêu đề ghi nguồn kết quả (tự tính hay nhập từ ETABS/SAP).
- **Bảng**: Chuyển vị nút (Joint Displacements, mm / rad), Phản lực gối (Joint Reactions), Nội lực dầm (Element Forces – Beams), Nội lực cột (Element Forces – Columns), Chuyển vị lệch tầng (Story Drifts), Chu kỳ & tỷ lệ khối lượng (Modal Periods & Mass Ratios), Phản lực đáy (Base Reactions).
- Lọc theo **Trường hợp / tổ hợp** và **Tầng**; **Xuất Excel…**, **Xuất CSV…**, **Sao chép** (dán vào Excel).
- **Hiển thị → Phản lực gối…** mở thẳng bảng Phản lực gối.

### 9.10. Kiểm tra mô hình (Check Model)

1. **Phân tích → Kiểm tra mô hình…** (nút **Kiểm tra MH**) trước khi phân tích: cửa sổ **Kiểm tra mô hình (Check Model)** (không chặn) liệt kê vấn đề theo mức độ, mã, tầng, nội dung.
2. Mục kiểm tra: nút trùng (theo **Dung sai nút (mm)**), thanh trùng / chồng, thanh dài 0, nút tự do, nhóm thanh không nối gối (biến hình), cột không có gối / móng ở chân, dầm cắt nhau không có nút, nút nằm giữa thanh chưa chia, **Cạnh sàn không có dầm**, tiết diện thiếu / không dùng, **Trường hợp tải rỗng**, tổ hợp tham chiếu thiếu, tải gán cho đối tượng / trường hợp không tồn tại. Tích **Hiện mục thông tin** để xem cả mục không phải lỗi.
3. Bấm đúp một dòng (hoặc **Phóng tới**) để chọn và phóng tới đối tượng trên mặt bằng.
4. **Sửa mục đang chọn** / **Sửa tự động tất cả**: gộp nút trùng, xoá thanh dài 0 / thanh trùng (chuyển tải sang thanh giữ lại), thêm nút tại giao điểm dầm và chia thanh, chia thanh tại nút nằm giữa, xoá nút tự do, xoá tải mồ côi, bỏ thành phần tổ hợp thiếu. Hoàn tác bằng Ctrl+Z; **Kiểm tra lại**.

### 9.11. Hình biến dạng (Deformed Shape)

- **Hiển thị → Hình biến dạng…**: khung dây biến dạng theo **Trường hợp / tổ hợp** tuyến tính, **Khung nhìn** 3D (kéo chuột để xoay) hoặc mặt bằng **Tầng**; **Tỉ lệ** phóng tự động × hệ số; tuỳ chọn **Hình chưa biến dạng** mờ phía sau.
- Đường đàn hồi mỗi thanh nội suy Hermite từ chuyển vị và góc xoay hai nút (đầu có giải phóng mô men bỏ góc xoay đầu đó) – dùng để phát hiện nhanh mô hình sai (thanh rời, gối thiếu, chuyển vị bất thường).
- **Hiển thị → Biểu đồ nội lực**, **Hiển thị → Dạng dao động** chuyển sang khung nhìn Biểu đồ nội lực / Dao động.

### 9.12. Mô hình không ổn định, lỗi phân tích

- Thông báo kết cấu biến hình / ma trận suy biến: thường do cột không có liên kết chân, dầm treo không nối vào nút, thanh trùng, tất cả liên kết chân là khớp với khung một tầng không có giằng, hoặc giải phóng liên kết quá mức.
- Cách xử lý: xem cảnh báo ở tab Kết quả; trên mặt bằng kiểm tra dầm có nối vào cột/dầm khác; chạy **Sửa → Chia thanh tại nút** sau khi nhập mô hình; kiểm tra **Liên kết chân cột**.
- Kiểm tra mô hình tự động trước khi phân tích: **Phân tích → Kiểm tra mô hình…** (*mục 9.10 (Kiểm tra mô hình (Check Model))*); gộp nút trùng: **Sửa → Gộp nút trùng**; xem hình biến dạng (*mục 9.11 (Hình biến dạng (Deformed Shape))*).

## 10. Thiết kế cấu kiện

### 10.1. Thiết kế BTCT – quy trình chung

- **Thiết kế → Thiết kế BTCT** (nút **Thiết kế BTCT**): tự chạy phân tích nếu cần, thiết kế dầm, cột, sàn, móng, cầu thang theo TCVN 5574:2018 và TCVN 9362:2012 / TCVN 10304:2014; thanh trạng thái báo số cấu kiện không đạt.
- Tab **Thiết kế** → Dầm, Cột, Sàn, Móng, Cầu thang, Thép: mỗi dòng một cấu kiện / vùng thép; dòng **tô đỏ** = không đạt. Chọn dòng hoặc chọn cấu kiện trên mặt bằng để xem chi tiết ở ô thông tin.
- Mỗi cấu kiện có danh sách hạng mục kiểm tra (yêu cầu / khả năng, hệ số sử dụng k); hạng mục bất lợi nhất quyết định kết luận.
- Đổi tuỳ chọn thép, lựa chọn thép riêng, tiết diện → thiết kế lại tự động hoặc khi xuất.

### 10.2. Dầm

- Thép dọc theo mô men bao tại gối và nhịp (thép dưới, mũ gối, thép chạy suốt), chịu cắt với c = 2h0, đai; võng dài hạn kể nứt theo thép thực bố trí (B = Eb/(1+φb,cr)·Ieff, φb,cr = 2,5).
- Khoảng hở thép theo TCVN 5574:2018 mục 10.3.5; cho phép 2 hàng thép khi bật trong tuỳ chọn.
- Bản vẽ: KC-04.n chi tiết dầm theo tầng (mặt đứng, mặt cắt, tag thép kiểu Kết cấu Soft).

### 10.3. Cột

Cốt thép đối xứng, nén lệch tâm xiên bằng đường bao tải trọng (load contour), uốn dọc theo hệ số η; đai theo cấu tạo và vùng tới hạn. Bản vẽ KC-03.

### 10.4. Sàn – phương án thép sàn

- Ô chữ nhật, phương pháp Marcus theo điều kiện biên; võng dài hạn kể nứt + từ biến.
- Phương án thép: **Thép mũ + lưới dưới** (mặc định), **Lưới 2 lớp**, **Lưới 2 lớp + mũ tăng cường** – đặt mặc định ở **Thiết kế → Tuỳ chọn chọn thép & gia công…** (Phương án thép sàn mặc định), từng ô chọn riêng được bằng **Chọn bố trí thép…** ở tab Thiết kế → Sàn.
- Bản vẽ KC-05 mặt bằng thép sàn.

### 10.5. Móng – thiết kế và kiểm tra

- Móng đơn, móng băng trên nền thiên nhiên: R nền (TCVN 9362:2012), áp lực đáy (pmax ≤ 1,2R), chọc thủng, uốn, lún cộng lớp phân tố (Sgh = 8 cm).
- Móng cọc: [P] theo SPT / chỉ tiêu cơ lý (TCVN 10304:2014), phản lực đầu cọc, chọc thủng đài, uốn đài; bố trí cọc (*mục 7.39 (Móng cọc – bố trí cọc)*).
- Móng băng: dầm trên nền đàn hồi (hệ số nền), sườn và bản cánh.
- Móng lệch tâm / dầm móng cân bằng / móng kết hợp (*mục 7.40 (Móng lệch tâm, ranh giới đất, lùi cọc)*); cừ tràm (*mục 7.41 (Gia cố nền bằng cừ tràm)*).
- Tab Mô hình móng → **Kiểm tra an toàn…**: bảng kiểm tra an toàn và sự làm việc của móng đang chọn; tab Thiết kế → Móng: **Chọn bố trí thép…** (Ø – bước thép đáy).
- Bản vẽ KC-02 mặt bằng và chi tiết móng.

### 10.6. Bản vẽ móng, mặt cắt và điện nước bổ sung

- **Móng và cọc**:
- Mặt bằng móng (Triển khai hồ sơ): móng bậc vẽ thêm **đường bao bậc trên** quanh tim cột và hai chuỗi kích thước (mép đế → bậc → mép đế) theo hai phương.
- Chế độ Thiết kế, móng cọc: tờ **KC-02.1** gồm *mặt bằng định vị cọc* (cọc đánh số P1…Pn từ trên xuống, trái sang phải; trục, đường bao đài, **bảng toạ độ cọc** X, Y, cao độ đầu / mũi, **bảng thống kê cọc**) và – khi chọn phương pháp TCVN 10304 cho cọc khoan nhồi – **đường cong sức chịu tải theo chiều dài cọc** của hố khoan đang chọn (*mục 7.39 (Móng cọc – bố trí cọc)*).
- Bê tông **B22,5** có trong mọi danh sách chọn bê tông của chế độ Thiết kế.

- **Mặt cắt công trình**:
- Móng bè (Móng → bề dày bè > 0): mặt cắt vẽ bản bè liên tục (vươn ra ngoài tim cột biên), sườn bè, cổ cột và lót bê tông liền.
- Móng băng chạy **dọc theo đường cắt**: vẽ thành một dải liên tục (bản cánh + sườn) thay cho các chữ T ngược rời.
- Lớp hoàn thiện sàn vẽ **theo từng phòng** trên đường cắt: WC / vệ sinh / tắm có chống thấm + vữa + gạch men; phòng lát đá, sàn gỗ, phòng thường theo tên phòng / ô *Hoàn thiện sàn*. Ngoài phạm vi phòng không vẽ lớp hoàn thiện.

- **Điện nước**:
- Bộ bản vẽ điện nước có thêm **mặt cắt dọc tuyến thoát** cho mỗi chuỗi ống thoát (tỉ lệ ngang 1/50, đứng 1/10): đáy sàn, đường đáy / đỉnh ống, nhãn Ø – i – L từng đoạn, phụ kiện, bảng khoảng cách – cao độ đáy ống – độ dốc. Chênh cao đáy ống mỗi đoạn = L·i.
- **Độ dốc riêng theo hệ thống**: **Cài đặt điện nước…** → bảng luật (hệ thống, Ø từ – đến, độ dốc, min / max); ví dụ thoát nước mưa 1 % mọi Ø. Thứ tự ưu tiên: độ dốc nhập tay của tuyến → luật hệ thống → mặc định theo Ø.
- Xuất khối lượng Excel có thêm sheet **Vật tư điện nước** (thiết bị, ống theo Ø, ống gen, dây, cáp, phụ kiện, quang treo) và bảng độ dốc từng tuyến thoát.
- Nhập Revit: bật *Nhập ống / thiết bị Revit thành đối tượng điện nước* để thiết bị vệ sinh, ổ cắm, công tắc, đèn, quạt… được đặt theo họ trong thư viện điện nước (so tên họ Revit), ống / ống luồn dây thành tuyến giữ đúng hình học Revit, đầu ống trùng nhau nối chung một điểm. Phụ kiện ống bỏ qua (chương trình tự sinh); ống gió / miệng gió / dàn lạnh – dàn nóng thành đối tượng điều hoà – thông gió; khay cáp và phần tử không nhận ra vẫn nhập dạng khối chung. Cập nhật từ Revit xoá / thêm theo UniqueId.

### 10.7. Cầu thang

Tab Thiết kế → **Cầu thang**: mỗi bộ phận một dòng (bản thang, chiếu nghỉ, dầm cốn, dầm chiếu nghỉ). Bản vẽ KC-08.n: mặt bằng, mặt cắt, bố trí thép, thống kê thép. Khai báo: *mục 7.14 (Cầu thang)*.

### 10.8. Dầm cong – xoắn

Thiết kế trên chiều dài cung: uốn + cắt + **xoắn** (T ≤ 0,1·Rb·b²·h tương tác tuyến tính với Q; đai kín Asw1/s = T/(2·Ak·Rsw) + qsw/(2·Rsw), bước ≤ min(uk/8; 300 mm); cốt dọc Asl = T·uk/(2·Ak·Rs) chia ½ trên + ½ dưới) theo TCVN 5574:2018 mục 8.1.4 (giản lược). Mặt đứng khung chỉ thể hiện hình chiếu của dầm cong.

### 10.9. Kết cấu thép tiền chế và tab Thép

- **Thiết kế → Kiểm tra thép**: bền, ổn định trong/ngoài mặt phẳng, độ mảnh [λ] = 180 − 60α, ổn định cục bộ bụng, tiết diện hữu hiệu bụng mảnh, võng kèo, chuyển vị đỉnh cột (TCVN 5575).
- Tab Thiết kế → **Thép**: bảng kiểm tra cấu kiện thép; nút **Xuất shop drawing thép…** và **Tuỳ chọn…** (thép cốt BTCT) cũng đặt ở đây.
- Chưa thiết kế liên kết bu lông, bản mã.

### 10.10. Chọn bố trí thép

1. Chọn dầm/cột/ô sàn/móng trên mặt bằng (hoặc dòng ở tab Thiết kế), bấm **Chọn bố trí thép…** (**Thiết kế → Chọn bố trí thép…**).
2. Trái: **Vùng thép** của cấu kiện (★ = đang dùng lựa chọn của người dùng). Giữa: danh sách phương án thoả As yêu cầu và khoảng hở (As yêu cầu, As bố trí…). Phải: **Mặt cắt phương án**.
3. **Áp dụng phương án**; **Bỏ chọn vùng này (tự động)**; **Bỏ chọn cả cấu kiện**. Lựa chọn lưu vào dự án, hoàn tác được, thiết kế chạy lại.

> **Lưu ý:** Mọi phương án hiển thị đều thoả As yêu cầu và khoảng hở theo TCVN 5574:2018 mục 10.3.5.

### 10.11. Tuỳ chọn chọn thép và gia công

**Thiết kế → Tuỳ chọn chọn thép & gia công…** (hoặc nút **Tuỳ chọn…** ở tab Thiết kế → Thép):

- **Chọn thép**: Ø ưu tiên thép dọc, số thanh tối đa một hàng dầm, Ø đai cố định, bước đai nhỏ nhất / lớn nhất, phương án thép sàn mặc định, ưu tiên thép tăng cường cùng Ø thép chạy suốt, cho phép thép dầm 2 hàng.
- **Gia công thép (shop drawing)**: chiều dài cây thép thương phẩm (vd. 11,7 m), đầu thừa tái sử dụng khi ≥ …, trừ dãn khi uốn 90° / 135° / 180°.
- **Nguyên tắc nối thép (không nối trong vùng nguy hiểm)**: vùng tới hạn dầm lcr = k·h (thường / kháng chấn), vùng tới hạn cột, hệ số α chiều dài nối vùng kéo / nén, khoảng so le ≥ k·ll, nối so le (≤ 50 % số thanh tại một tiết diện), ống nối ren, không nối thép móng đơn / đài cọc.

> **Lưu ý:** Đổi tuỳ chọn sẽ thiết kế lại BTCT (nếu đã có kết quả phân tích); lựa chọn thép riêng từng cấu kiện vẫn được giữ.

### 10.12. Tự tăng tiết diện đến khi đạt

**Thiết kế → Tự tăng tiết diện đến khi đạt**: lặp phân tích → thiết kế → tăng 50 mm cho dầm/cột không đạt, 10 mm cho sàn, đến khi mọi cấu kiện đạt hoặc hết số vòng lặp. Kiểm tra lại kiến trúc (chiều cao thông thuỷ, kích thước cột) sau khi tăng.

### 10.13. Ý nghĩa các cảnh báo và kết luận

| Thông báo | Ý nghĩa | Xử lý |
|---|---|---|
| Dòng tô đỏ / KHÔNG ĐẠT | Một hạng mục kiểm tra có hệ số k > 1 (yêu cầu > khả năng). | Xem hạng mục bất lợi nhất; tăng tiết diện, cấp bê tông, chọn thép khác. |
| LỖI … (trong thông báo cấu kiện) | Không bố trí được thép thoả mãn (vượt hàm lượng, không đủ chỗ, tiết diện quá bé). | Tăng tiết diện; cho phép 2 hàng thép; đổi Ø ưu tiên. |
| Võng vượt giới hạn | Độ võng dài hạn lớn hơn [f]. | Tăng h dầm / chiều dày sàn, thêm dầm phụ. |
| Áp lực đáy / lún vượt | Móng không đủ kích thước hoặc nền yếu. | Tăng kích thước móng, đổi loại móng, cừ tràm, móng cọc. |
| Mô hình đã thay đổi – cần chạy lại phân tích | Kết quả cũ đã bị xoá do sửa mô hình. | F5 rồi thiết kế lại. |
| Dự án trích dẫn văn bản đã hết hiệu lực | Ghi chú / thuyết minh còn trích văn bản cũ. | **Trợ giúp → Văn bản pháp lý & tiêu chuẩn áp dụng…** → Kiểm tra dự án. |

## 11. Triển khai hồ sơ

### 11.1. Bộ bản vẽ DXF KC-01 … KC-08

**Triển khai hồ sơ → Bản vẽ DXF...**: chọn kiểu xuất, thư mục, xem/đổi tên layer → chương trình vẽ bộ bản vẽ (tag thép, bảng thống kê kiểu Kết cấu Soft, ghi chú chung theo danh mục văn bản):

| Số hiệu | File | Nội dung |
|---|---|---|
| KC-01 | KC-01_MatBangKetCau.dxf | Mặt bằng kết cấu các tầng |
| KC-02 | KC-02_Mong.dxf | Mặt bằng và chi tiết móng (móng đơn, đài cọc, móng băng, lệch tâm, cừ tràm) |
| KC-03 | KC-03_Cot.dxf | Chi tiết cột |
| KC-04.n | KC-04_Dam_<tầng>.dxf | Chi tiết dầm từng tầng |
| KC-05 | KC-05_San.dxf | Mặt bằng thép sàn |
| KC-06 | KC-06_ThongKeThep.dxf | Thống kê cốt thép |
| KC-07… | KC-07_PhuongAnCatThep.dxf, KC-07.n_GiaCongThep_….dxf | Gia công thép (shop drawing – *mục 11.9 (Shop drawing thép (gia công, nối thép, vùng cấm nối))*) |
| KC-08.n | KC-08.n_CauThang_<tên>.dxf | Cầu thang: mặt bằng, mặt cắt, bố trí thép |

> **Lưu ý:** Bộ bản vẽ hiện dành cho kết cấu BTCT; khung thép tiền chế chưa có bản vẽ chi tiết.

### 11.2. Thiết lập bản vẽ (tỷ lệ, cỡ chữ, kiểu kích thước)

- **Tuỳ chọn → Thiết lập bản vẽ (tỷ lệ, cỡ chữ)…**: tab **Tỷ lệ, cỡ chữ, chú thích** – tỷ lệ theo loại bản vẽ (mặt bằng: tự chọn 1/50 nếu mặt bằng ≤ 25 m, ngược lại 1/100; mặt cắt dầm và cột; móng; cầu thang…), cỡ chữ từng thành phần, chú thích tuỳ chọn (số hiệu phần tử nội bộ, giá trị nội lực, giá trị thiết kế, ký hiệu cao độ ▽, kích thước lưới trục).
- Tab **Kiểu kích thước (Dimension)**: kiểu VXT_1-25…, chữ, gạch xiên / mũi tên đặc, DIMSCALE.
- Lưu trong chuẩn CAD (mặc định người dùng hoặc riêng dự án) – dùng chung cho bản vẽ DXF, tờ in.

### 11.3. Chuẩn layer CAD

- **Tuỳ chọn → Chuẩn layer CAD…**: tab **Nhận diện layer kiến trúc** – mỗi layer bản vẽ kiến trúc được xếp vào nhóm Tường, Cột, Dầm, Sàn, Trục, Cửa, Cầu thang, Tên phòng hoặc Bỏ qua; gán trực tiếp (ưu tiên) và quy tắc theo mẫu tên (có ưu tiên, biểu thức chính quy), **Bỏ qua layer đang tắt (off)**.
- Tab **Layer bản vẽ kết cấu**: layer nội bộ → tên layer xuất, màu, nét in (mm), In; **Đổi tên layer hàng loạt** (tiền tố, hậu tố, tìm/thay).
- Nhập / xuất chuẩn (CadStandard.json) để dùng chung trong đơn vị; trước mỗi lần xuất DXF hộp **Xem/đổi tên layer trước khi xuất** cho phép đổi lần cuối.

### 11.4. Xuất gộp một file / tách rời, mẫu tên file

- Hộp **Tuỳ chọn xuất**: **Kiểu xuất** Gộp 1 file (các bản vẽ đặt cạnh nhau, mỗi bản giữ tỷ lệ trong khung bao riêng có tên ở trên; PDF: nhiều trang) hoặc Tách rời từng bản vẽ (mỗi bản một file).
- **Tên file gộp** (mặc định {duan}_BoBanVe), **Mẫu tên từng file**: {so} số hiệu, {ten} tên bản vẽ, {duan} tên công trình, {stt} thứ tự 01, 02…, {ngay} ngày xuất. Để trống = tên mặc định (KC-01_MatBangKetCau…).
- **Ghi nhớ lựa chọn cho lần sau** (XuatBanVe.json).

### 11.5. Định dạng xuất DXF R12 / DXF 2018 / DWG 2018 và mẫu tô HATCH

| Định dạng | Đặc điểm |
|---|---|
| DXF R12 (tương thích cũ) – mặc định | Bộ ghi gốc của chương trình, mở được bằng mọi phần mềm CAD; mẫu tô HATCH được mô phỏng bằng LINE / SOLID; không lưu được nét in theo layer. |
| DXF 2018 (có HATCH) | AC1032, UTF-8: HATCH thật, LWPOLYLINE, nét in theo layer (ghi qua thư viện ACadSharp). |
| DWG 2018 (có HATCH) | File .dwg AC1032 có HATCH thật (ghi qua ACadSharp). |

- Chọn định dạng trong chuẩn CAD (**Tuỳ chọn → Chuẩn layer CAD…** – mặc định người dùng và theo dự án) hoặc ô **Định dạng file** ở hộp **Tuỳ chọn xuất** trước mỗi lần xuất. File chuẩn cũ không có mục này → DXF R12.
- Tô vật liệu mặt cắt: cửa sổ Kiểu bản vẽ → ô **Tô vật liệu (hatch) mặt cắt**: **Tự động** (bật khi xuất DXF 2018 / DWG, tắt với DXF R12), **Bật** (cả R12 – mô phỏng bằng nét), **Tắt**. Bê tông AR-CONC (dầm, cột, móng, bản thang), bê tông lót ANSI31, đất EARTH dưới móng, bậc gạch – trên layer HATCH (màu 8).
- Mẫu tô: SOLID, ANSI31…, mẫu kiến trúc AR-CONC, AR-SAND, EARTH, BRICK (bản rút gọn cùng dáng mẫu AutoCAD; định nghĩa nét ghi kèm trong HATCH nên mọi phần mềm CAD hiển thị giống nhau).
- Phần mở rộng file theo định dạng: .dwg cho DWG 2018, .dxf cho hai loại còn lại.

### 11.6. Dàn trang và in bản vẽ

1. **Triển khai hồ sơ → Dàn trang & in bản vẽ…**: cửa sổ **Dàn trang & in bản vẽ** – danh sách tờ bên trái (Thêm, Trang trắng, Tạo tờ từ mẫu, Lưu tờ làm mẫu, Nhân bản, Xoá, ▲▼, Đánh số lại, **Tạo bộ bản vẽ mặc định**).
2. **Thuộc tính tờ**: khổ A0…A4 / tuỳ chỉnh, ngang/dọc, khung viền, khung tên, **Khung tên công trình…** (thông tin dùng chung cho mọi tờ, logo, người thiết kế / kiểm tra / duyệt).
3. **Thêm khung nhìn** (mặt bằng, mặt đứng, mặt cắt, bảng…) → **Thêm vào tờ**; kéo / đổi cỡ khung nhìn trên trang; **Thuộc tính khung nhìn**: tỷ lệ, vùng cắt, ẩn tên hình, viền khung, khoá, **Co vừa khung**.
4. Đối tượng tự do: Chữ, Đường, Mũi tên, Khung, Tròn, Mây, Ảnh, Bảng, Kích thước; Chép / Dán / Nhân đôi / Xoá, ↶ ↷, Bắt lưới, Bắt đối tượng, căn hàng, phân bố đều, lên trên / xuống dưới, khoá.
5. **In…** (chọn "Microsoft Print to PDF" nếu cần), **Xuất PDF…**, **Xuất DXF…** (tuỳ chọn **Chỉ các tờ đang chọn**), **Hiệu chỉnh tờ…**.

Các tờ lưu trong dự án (.kcvxt); tờ mẫu lưu trong thư viện mẫu định dạng (*mục 6.3 (Mẫu định dạng (template))*).

### 11.7. Khung tên người dùng, tờ mẫu

- **Tạo tờ từ mẫu** → nhóm **Khung tên người dùng (tải từ DWG / DXF / ảnh)** → **Tải khung tên từ file (DWG/DXF/ảnh)…** mở hộp thoại **Khung tên người dùng**.
- Tab **Chữ trong file → trường**: chữ có chỗ trống {TEN_CONG_TRINH}, {SO_HIEU}… hoặc thẻ ATTRIB/ATTDEF trùng tên trường được nhận tự động; nhấp vào chữ trên hình để gán trường. Tab **Chữ trường đặt thêm**: thêm trường tại toạ độ mm.
- Tuỳ chọn: điểm gốc, đơn vị, **Lấy miền tô (viền)**, **Vẽ khung viền trong của chương trình**, ô logo, **Xem với giá trị của dự án / tờ hiện tại** → **Đồng ý – dùng khung tên này**; **Dùng khung tên mặc định của chương trình** để quay lại.

### 11.8. Hiệu chỉnh bản vẽ sau khi xuất

- **Triển khai hồ sơ → Hiệu chỉnh bản vẽ…**: mở một bản vẽ tự sinh (bộ KC-xx, gia công thép, tờ dàn trang), chọn thực thể (chữ, nhãn thép, kích thước, nét; **Chọn cả nhóm**), kéo dời, **Sửa chữ… (F2)**, **Xoá (Del)**, **Lớp / màu…**, thêm chữ / mũi tên / đường / mây / khung; ↶ ↷.
- Chỉnh sửa lưu vào dự án và **được áp lại sau mỗi lần dựng lại / xuất**; chỉnh sửa không còn khớp (do mô hình đổi) liệt kê ở mục **Chỉnh sửa không còn áp dụng được** – **Xoá các chỉnh sửa này** hoặc **Khôi phục bản gốc**.
- **Triển khai hồ sơ → Nạp lại bản vẽ đã chỉnh sửa (DXF/DWG)…**: khoá bản vẽ bằng file đã sửa trong AutoCAD – các lần xuất / in sau dùng file này tới khi **Bỏ khoá**. **Xuất DXF bản vẽ này…** để xuất riêng.

> **Cảnh báo:** Bản vẽ đã khoá không cập nhật theo mô hình. Sau khi sửa mô hình, bỏ khoá rồi sửa lại trong CAD nếu cần.

### 11.9. Shop drawing thép (gia công, nối thép, vùng cấm nối)

- **Triển khai hồ sơ → Xuất shop drawing thép…** (hoặc nút ở tab Thiết kế → Thép): bảng gia công DXF theo loại cấu kiện (KC-07.n), phương án cắt và tổng hợp (sơ đồ cắt cây 11,7 m – KC-07) và bảng Excel "Gia công thép".
- Nối thép theo nguyên tắc ở **Thiết kế → Tuỳ chọn chọn thép & gia công…**: không nối trong vùng tới hạn dầm (lcr = k·h) và cột, chiều dài nối theo α vùng kéo / nén, nối so le, ống nối ren khi vùng nối cho phép ngắn hơn ll; móng đơn / đài cọc dùng thanh nguyên.
- Chiều dài cắt có thể trừ độ dãn dài khi uốn; đầu thừa đủ dài được tái sử dụng.

### 11.10. Bảng khối lượng vật tư

- **Triển khai hồ sơ → Bảng khối lượng vật tư…**: khối lượng bê tông, cốt thép, ván khuôn, khối xây… theo cấu kiện và tầng; **Tính lại**, **Xuất Excel…**, **Xuất Word…**.
- Tab **Định mức**: cấp phối, định mức vật liệu; **Lấy danh mục vật tư từ bảng**, **Khôi phục định mức mặc định**, **Nhập định mức từ Excel…** (cột: mã hiệu, tên, đơn vị, vật liệu, định mức), **Xuất file mẫu định mức…**.

> **Cảnh báo:** Cấp phối mặc định là **THAM KHẢO** (số liệu hệ định mức cũ). Nguồn ghi trên bảng: Thông tư 38/2026/TT-BXD – đối chiếu và nhập lại theo định mức hiện hành; bảng khối lượng không thay thế dự toán.

### 11.11. Thuyết minh tính toán (Word)

- **Triển khai hồ sơ → Thuyết minh Word...**: thuyết minh đầy đủ theo mặc định.
- **Triển khai hồ sơ → Lập thuyết minh tự động…**: hộp **Lập thuyết minh tính toán tự động** – chọn chương (giới thiệu công trình; căn cứ pháp lý và tiêu chuẩn; vật liệu, tiết diện; sơ đồ tính, tải trọng, tổ hợp; gió, động đất; kết quả nội lực; thiết kế dầm, cột, sàn, cầu thang, móng; địa chất; thống kê cốt thép, kết cấu thép; khối lượng vật tư; phụ lục công thức). Luôn có: trang bìa, mục lục, chương KẾT LUẬN VÀ KIẾN NGHỊ, phụ lục cảnh báo.
- **Thông tin trang bìa**: tên công trình, chủ đầu tư, địa điểm, đơn vị thiết kế, chủ trì thiết kế, người lập, kiểm tra, ngày lập.
- **Tuỳ chọn**: số ví dụ tính toán chi tiết cho mỗi loại cấu kiện, bảng nội lực bao từng thanh, mục lục tự động, đánh số trang, đánh số tên bảng (Bảng n.k). Font theo chuẩn CAD/thuyết minh.

> **Mẹo:** Mở file bằng Word, chọn **Yes** khi được hỏi cập nhật trường để có mục lục và số trang đúng (hoặc Ctrl+A → F9).

### 11.12. Bảng tính Excel, nội lực CSV, mô hình ETABS/SAP

- **Triển khai hồ sơ → Bảng tính Excel...**: các sheet Thông tin, Tải trọng, Nội lực, Phản lực, Chuyển vị, Lực ngang tầng, Dao động riêng, Dầm, Cột, Sàn, Móng, Đài cọc, Móng băng, Thép, Thống kê thép, Gia công thép, Cảnh báo, Phụ lục.
- **Tệp → Xuất mô hình → Nội lực CSV...**: bảng nội lực, phản lực, chuyển vị dạng ETABS (dùng cho kiểm tra chéo hoặc phần mềm khác).
- **Tệp → Xuất mô hình → Mô hình ETABS .e2k...**: sàn xuất là phần tử Membrane kèm tải sàn (ETABS tự dồn tải – kiểm tra cả bước dồn tải). Mở trong ETABS: File > Import > ETABS .e2k Text File. Tên thanh C{Id} (cột), B{Id} (dầm), D{Id} (xiên).
- **Tệp → Xuất mô hình → Mô hình SAP2000 .s2k...**: mô hình giống hệt (tải sàn đã dồn lên dầm, Diaphragm, thanh vát chia đoạn) – nội lực phải trùng gần như tuyệt đối. Mở: File > Import > SAP2000 .s2k Text File.

### 11.13. Xuất tất cả vào thư mục

**Triển khai hồ sơ → Xuất tất cả vào thư mục...** (nút **Xuất tất cả**): chọn thư mục gốc → tự phân tích, thiết kế nếu cần → chọn kiểu xuất bản vẽ → xem/đổi layer → tạo thư mục <tên công trình>_<yyyyMMdd_HHmm> gồm: MoHinh.kcvxt, BanVe\ (bộ DXF), GiaCongThep\, ThuyetMinh.docx, BangTinh.xlsx, bảng khối lượng, NoiLuc\ (CSV), MoHinh.e2k, MoHinh.s2k. Hạng mục lỗi được liệt kê, các hạng mục khác vẫn xuất.

### 11.14. Phụ lục tính toán

**Trợ giúp → Phụ lục tính toán & hướng dẫn…** (Shift+F1): mọi công thức, chỉ số, hệ số, hệ số an toàn và giải thích dễ hiểu của các phép tính chương trình thực sự dùng (PL1…PLn), bảng tra tính lại bằng chính hàm của chương trình (cường độ vật liệu, k(z), phổ động đất, …), có giá trị của công trình đang mở. Ô tìm kiếm không dấu, **Xuất Word…**, **In…**.

## 12. Kiểm chứng và kiểm tra chéo

### 12.1. Báo cáo kiểm chứng phần mềm

1. **Trợ giúp → Kiểm chứng phần mềm…**: bấm **Chạy kiểm chứng** – chương trình chạy bộ bài toán chuẩn (phân tích, thiết kế, móng) và so với lời giải tham chiếu.
2. Bảng: mã, bài toán, đại lượng, tham chiếu, Việt Const, sai số %, ngưỡng %, kết luận. Dòng tóm tắt: số bài toán, số đạt / không đạt, sai số lớn nhất.
3. **Xuất báo cáo Word + Excel…**: báo cáo gồm 1. Mục đích và phương pháp, 2. Phạm vi, 3. Môi trường và phiên bản, **4. Kết quả kiểm chứng** (tự kiểm chứng), **5. Kiểm chứng độc lập**, 6. Kết luận, 7. Giới hạn đã biết, Phụ lục A lời giải tham chiếu tự lập.

### 12.2. Kiểm chứng độc lập (mục 5 của báo cáo)

- **Mục 4 – Tự kiểm chứng**: tham chiếu là lời giải giải tích / tính tay độc lập theo công thức tiêu chuẩn.
- **Mục 5 – Kiểm chứng độc lập**: nhập ở các cột màu vàng – giá trị theo tài liệu / bên thứ ba, nguồn tài liệu (tên, trang/ví dụ), loại nguồn, người/đơn vị xác nhận, ngày; ngưỡng so với tài liệu = max(ngưỡng; 3 %).
- **Lưu giá trị kiểm chứng độc lập** (KiemChung.json); hoặc gửi file Excel báo cáo cho bên kiểm chứng điền rồi **Nhập kết quả kiểm chứng độc lập từ Excel…**.

### 12.3. Kiểm tra chéo với ETABS / SAP2000

- SAP2000 (.s2k): mô hình giống hệt – nội lực phải trùng gần như tuyệt đối; dùng để kiểm tra lời giải khung.
- ETABS (.e2k): kiểm tra cả bước dồn tải sàn (ETABS tự dồn từ phần tử Membrane).
- So sánh tự động: *mục 9.8 (So sánh với ETABS / SAP2000 / SAFE)*; nguyên nhân lệch: *mục 14.3 (Kết quả lệch so với ETABS – nguyên nhân)*.

### 12.4. Trách nhiệm của người dùng

Báo cáo kiểm chứng chỉ áp dụng cho phạm vi bài toán đã kiểm chứng và giả thiết của từng bài toán; không thay thế việc kỹ sư chịu trách nhiệm kiểm tra kết quả thiết kế, đặc biệt hệ số tải trọng, điều kiện địa chất và các giới hạn nêu ở *mục 1.4 (Giới hạn đã biết của phiên bản hiện tại)*.

## 13. Văn bản pháp lý và tiêu chuẩn

### 13.1. Danh mục văn bản

- **Trợ giúp → Văn bản pháp lý & tiêu chuẩn áp dụng…**: danh mục văn bản (luật, nghị định, thông tư, quy chuẩn, tiêu chuẩn) lọc theo **Phạm vi**, **Chỉ văn bản đang áp dụng**, màu theo trạng thái; phần **Chi tiết**: thay thế / được thay thế, ghi chú, cần kiểm tra, nguồn.
- Thuyết minh (chương CĂN CỨ PHÁP LÝ VÀ TIÊU CHUẨN ÁP DỤNG), phụ lục, ghi chú chung bản vẽ và bảng khối lượng trích dẫn theo danh mục này qua "chủ đề" (vd. tai-trong, btct, dong-dat, mong-coc).
- Danh mục có sẵn chỉ gồm văn bản đã kiểm tra nguồn; mục ghi "kiểm tra" cần người dùng đối chiếu văn bản gốc.

### 13.2. Cập nhật danh mục

- **＋ Thêm…**, **Sửa…**, **Xoá**, **Đánh dấu hết hiệu lực…** (ghi văn bản thay thế).
- **Nhập JSON… / Xuất JSON…** để chia sẻ; **Lưu làm mặc định** (%AppData%\KetCauVXT\VanBanPhapLy.json); **Khôi phục danh mục có sẵn**.
- **Kiểm tra dự án**: dò các ghi chú, thuyết minh của dự án còn trích dẫn văn bản cũ. Khi mở dự án, thanh trạng thái cảnh báo nếu dự án trích văn bản đã hết hiệu lực.

## 14. Câu hỏi thường gặp và xử lý lỗi

### 14.1. Không đọc được file DWG

- Chương trình đọc DWG AutoCAD R14 … 2018+. Bản vẽ lưu bằng phiên bản mới hơn hoặc lỗi: trong AutoCAD dùng SAVEAS → DXF (hoặc DWG 2018) rồi nhập lại.
- Cài ODA File Converter (miễn phí, opendesign.com) – chương trình tự dùng khi đọc trực tiếp lỗi.
- Bản vẽ dùng đơn vị lạ: chọn **Đơn vị bản vẽ** (mm / cm / m) thay vì "Theo file".
- Không nhận được tường/trục: kiểm tra tên layer và quy tắc ở **Tuỳ chọn → Chuẩn layer CAD…**; dùng tab **Layer trong file** để gán tay.

### 14.2. Windows chặn bộ cài / chương trình

Xem *mục 2.3 (Cảnh báo Windows SmartScreen)*. Bộ cài báo file hỏng: tải lại VietConst-Setup.exe (kiểm tra dung lượng đủ khoảng 50 MB).

### 14.3. Kết quả lệch so với ETABS – nguyên nhân

- ETABS mặc định có **vùng cứng đầu thanh** (End length offsets) và có thể đã gán **hệ số giảm độ cứng** (Property modifiers): tắt hoặc khai báo giống nhau ở hai bên.
- Dồn tải sàn: .e2k xuất sàn Membrane để ETABS tự dồn – khác cách dồn 45° của chương trình; dùng .s2k (tải đã dồn) để so riêng lời giải khung.
- Sàn cứng (diaphragm), liên kết chân cột, trọng lượng bản thân (hệ số TT), γf và tên tổ hợp phải giống nhau.
- Dầm cong: chương trình báo nội lực theo hệ trục tiếp tuyến cung, ETABS/SAP theo trục từng đoạn → lệch nhỏ (cỡ sin 5°) giữa M3 và T ở đầu đoạn.
- Phổ phản ứng: DDX/DDY không dấu; so sánh theo cặp bao +/−.
- Đơn vị bảng xuất từ ETABS: chọn đúng **Đơn vị khi bảng không ghi đơn vị** trong cửa sổ so sánh.

### 14.4. Mô hình không ổn định / không phân tích được

Xem *mục 9.12 (Mô hình không ổn định, lỗi phân tích)*.

### 14.5. Nhiều cấu kiện không đạt

- Kiểm tra tải: tải tường có bị gán trùng (tường biên + tường khai báo)? Hoạt tải công năng đúng chưa?
- Kiểm tra tiết diện mặc định quá nhỏ so với nhịp; dùng **Thiết kế → Tự tăng tiết diện đến khi đạt**.
- Kiểm tra cấp bê tông, nhóm thép ở tab Công trình.

### 14.6. Mất các sửa đổi trên mặt bằng

Bấm **Tạo mô hình** ở tab Công trình sinh lại toàn bộ mô hình. Dùng Ctrl+Z ngay sau đó, hoặc mở lại file đã lưu. Chỉ đổi thông số thì bấm **Áp dụng thông số**.

### 14.7. Mở lại dự án mất kết quả nhập từ ETABS

Kết quả nhập không lưu trong file .kcvxt (giới hạn đã biết). Nhập lại bằng **Phân tích → Nhập kết quả nội lực (ETABS/SAP/SAFE/khác)…** sau khi mở dự án.

### 14.8. Thuyết minh Word không có số trang / mục lục trống

Mục lục và số trang là trường (field) – Word cập nhật khi mở nếu chọn Yes; hoặc Ctrl+A → F9. LibreOffice: Tools → Update → Update All.

### 14.9. AutoCAD không thấy lệnh VXT

- Kiểm tra đã cài add-in (tuỳ chọn khi cài, hoặc CaiDatAutoCAD.cmd) và chọn Always Load khi AutoCAD hỏi.
- AutoCAD 2027 chưa được hỗ trợ (dùng .NET 10). Thử NETLOAD DLL đúng năm trong thư mục AutoCAD\KetCauVXT.bundle\Contents\<năm>.
- VXT_GUI không tự mở chương trình: mở KetCauVXT.exe một lần để ghi app_path.txt.

## 15. Phụ lục

### 15.1. Phím tắt và thao tác chuột

| Phím / chuột | Tác dụng |
|---|---|
| Ctrl+N / Ctrl+O / Ctrl+S | Dự án mới / mở / lưu |
| F5 | Chạy phân tích |
| F1 | Hướng dẫn sử dụng tại mục của ngữ cảnh hiện tại (tab, khung nhìn, công cụ, hộp thoại) |
| Shift+F1 | Phụ lục tính toán |
| Ctrl+Z | Hoàn tác thao tác sửa mô hình (30 bước) |
| Ctrl+E | Hiện / ẩn cây mô hình |
| Ctrl+T | Bảng kết quả (tự phân tích nếu cần) |
| Ctrl+A | Chọn mọi thanh tầng đang xem (khi đang ở khung nhìn Mặt bằng, không gõ trong ô nhập) |
| Delete | Xoá đối tượng đang chọn trên mặt bằng |
| Esc | Dừng công cụ vẽ, về chế độ Chọn; đóng hộp thoại |
| Chuột phải (khi vẽ dầm) | Kết thúc chuỗi vẽ; lần nữa: thoát công cụ |
| Gõ số khi vẽ dầm | Ô nhập toạ độ: x,y \| @dx,dy \| L<góc \| L |
| Con lăn chuột | Thu phóng mặt bằng, 3D, biểu đồ, mặt bằng móng |
| Kéo chuột trái (3D) | Xoay mô hình |
| Shift + kéo / chuột giữa (công cụ dầm) | Di chuyển mặt bằng |
| Ctrl+F (phụ lục, hướng dẫn) | Tìm kiếm |
| F2 (hiệu chỉnh bản vẽ) | Sửa chữ |

### 15.2. Định dạng file

| Phần mở rộng | Nội dung |
|---|---|
| .kcvxt | Dự án (JSON UTF-8): mô hình, tải, tổ hợp, địa chất, móng, lựa chọn thép, tờ dàn trang, chỉnh sửa bản vẽ, chuẩn CAD riêng dự án. |
| .kcvxt-mau | Mẫu định dạng (chuẩn CAD, khung tên, tờ mẫu, thuyết minh, định mức, tuỳ chọn thép). |
| .dwg / .dxf | Nhập: bản vẽ kiến trúc, mặt bằng kết cấu, khung tên. Xuất: bộ bản vẽ DXF. |
| acad_*.json | Dữ liệu trao đổi từ add-in AutoCAD (lược đồ Version 1, docs/acad-link.md). |
| *.vxtrevit.json | Mô hình xuất từ add-in Revit. |
| .e2k / .$et, .s2k / .$2k | Mô hình ETABS / SAP2000 dạng văn bản (nhập và xuất). |
| .xlsx / .csv / .txt | Bảng kết quả ETABS/SAP/SAFE (nhập, so sánh); danh mục địa danh; định mức; báo cáo kiểm chứng độc lập. |
| .docx / .xlsx | Thuyết minh, phụ lục, báo cáo kiểm chứng, bảng tính, bảng khối lượng, hướng dẫn sử dụng. |
| .json | Chuẩn layer CAD, danh mục văn bản pháp lý (nhập/xuất). |

### 15.3. Thư mục dữ liệu

- Chương trình: %LocalAppData%\Programs\KetCauVXT (cài cho một người) hoặc Program Files\KetCauVXT (mọi người dùng); dự án mẫu ở DuAnMau; HuongDanSuDung.docx ở thư mục cài.
- Thiết lập người dùng: %AppData%\KetCauVXT (*mục 2.9 (Dữ liệu người dùng (%AppData%\KetCauVXT))*).
- Add-in AutoCAD: %AppData%\Autodesk\ApplicationPlugins\KetCauVXT.bundle (hoặc ProgramData khi cài cho mọi người).

### 15.4. Dòng lệnh

| Lệnh | Tác dụng |
|---|---|
| KetCauVXT.exe <file.kcvxt> | Mở dự án. |
| KetCauVXT.exe --acad "<file.json>" | Mở hộp thoại nhập với dữ liệu gửi từ AutoCAD. |
| KetCauVXT.exe --selftest <file.kcvxt> <thư mục> | Tự kiểm tra giao diện: mở dự án, phân tích, thiết kế, mở các cửa sổ và chụp ảnh vào thư mục (CI dùng mỗi lần build). |

### 15.5. Bảng đường dẫn menu được nhắc trong tài liệu

Mục [SẮP CÓ] là menu dự kiến của tính năng đang phát triển.

| Mã | Đường dẫn |
|---|---|
| new | Tệp → Mới |
| open | Tệp → Mở .kcvxt... |
| save | Tệp → Lưu |
| saveAs | Tệp → Lưu thành... |
| importDxf | Tệp → Nhập mặt bằng kết cấu DWG/DXF... |
| importArch | Tệp → Nhập bản vẽ kiến trúc (triển khai kết cấu)... |
| importRevit | Tệp → Nhập từ Revit (JSON)... |
| importCsi | Tệp → Nhập mô hình ETABS/SAP (.e2k/.s2k)… |
| acad | Tệp → Nhận dữ liệu từ AutoCAD… |
| cadLinks | Tệp → Liên kết bản vẽ AutoCAD (DWG/DXF)… |
| detailingMode | Tệp → Chuyển sang chế độ Triển khai hồ sơ… |
| startMode | Tệp → Chọn chế độ làm việc khi khởi động… |
| e2k | Tệp → Xuất mô hình → Mô hình ETABS .e2k... |
| s2k | Tệp → Xuất mô hình → Mô hình SAP2000 .s2k... |
| csv | Tệp → Xuất mô hình → Nội lực CSV... |
| exit | Tệp → Thoát |
| undo | Sửa → Hoàn tác |
| deleteSel | Sửa → Xoá đối tượng đang chọn |
| replicate | Sửa → Sao chép / Đối xứng dầm đã chọn… |
| split | Sửa → Chia thanh tại nút |
| mergeNodes | Sửa → Gộp nút trùng |
| explorer | Xem → Cây mô hình |
| viewPlan | Xem → Mặt bằng |
| view3d | Xem → Khung nhìn 3D |
| viewFnd | Xem → Mô hình móng |
| fit | Xem → Vừa màn hình |
| labels | Xem → Tên cấu kiện |
| materials | Khai báo → Vật liệu… |
| sections | Khai báo → Tiết diện dầm/cột… |
| wallTypes | Khai báo → Loại tường… |
| buildUps | Khai báo → Cấu tạo sàn… |
| loadPatterns | Khai báo → Trường hợp tải (Load Patterns)… |
| loadCases | Khai báo → Trường hợp phân tích (Load Cases)… |
| comboEditor | Khai báo → Tổ hợp tải trọng… |
| combos | Khai báo → Tự tạo tổ hợp theo TCVN 2737:2023 |
| modal | Khai báo → Nguồn khối lượng & dao động riêng… |
| spectrum | Khai báo → Phổ phản ứng / động đất… |
| diaphragms | Khai báo → Sàn cứng (diaphragm) theo tầng… |
| buildGrid | Vẽ → Tạo khung BTCT từ lưới trục |
| portal | Vẽ → Tạo khung thép tiền chế... |
| drawBeam | Vẽ → Dầm (2 điểm) |
| drawCurved | Vẽ → Dầm cong |
| drawQuick | Vẽ → Dầm nhanh theo lưới |
| drawSecondary | Vẽ → Dầm phụ |
| drawCantilever | Vẽ → Dầm công xôn |
| beamByPos | Vẽ → Dầm theo toạ độ / trục… |
| stretchBeam | Vẽ → Kéo đầu dầm / kéo dầm |
| moveBeam | Vẽ → Dời dầm song song |
| trimBeam | Vẽ → Trim dầm (cắt tại dầm giao) |
| extendBeam | Vẽ → Extend dầm (kéo dài tới dầm chọn / vuông góc) |
| joinBeams | Vẽ → Nối dầm hở vào dầm vuông góc gần nhất |
| pickCad | Vẽ → Pick đối tượng CAD: dầm / sàn / thang / lỗ |
| drawSlab | Vẽ → Ô sàn |
| drawBalcony | Vẽ → Ban công |
| drawStair | Vẽ → Ô cầu thang |
| drawVoid | Vẽ → Giếng trời |
| stairDecl | Vẽ → Khai báo cầu thang… |
| drawWall | Vẽ → Tường |
| walls | Vẽ → Tường đã khai báo… |
| selOne | Chọn → Chọn từng đối tượng |
| selMulti | Chọn → Chọn nhiều (bấm / kéo khung) |
| selAll | Chọn → Mọi thanh tầng đang xem |
| selBySection | Chọn → Theo tiết diện… |
| selClear | Chọn → Bỏ chọn |
| assignFrameLoad | Gán → Tải trên thanh (đều / hình thang / tập trung)… |
| assignAreaLoad | Gán → Tải trên sàn… |
| assignJointLoad | Gán → Tải tại nút… |
| assignRestraint | Gán → Liên kết nút (ngàm / khớp / tự do)… |
| assignRelease | Gán → Giải phóng đầu thanh… |
| assignSection | Gán → Tiết diện thanh… |
| assignModifiers | Gán → Hệ số độ cứng thanh… |
| insertion | Gán → Điểm chèn / dời ngang… |
| assignDiaphragm | Gán → Sàn cứng tầng đang xem… |
| assignWall | Gán → Gán tường lên dầm |
| assignBuildUp | Gán → Gán cấu tạo sàn |
| assignWc | Gán → Sàn vệ sinh |
| modelCheck | Phân tích → Kiểm tra mô hình… |
| run | Phân tích → Chạy phân tích |
| importResults | Phân tích → Nhập kết quả nội lực (ETABS/SAP/SAFE/khác)… |
| crossCheck | Phân tích → So sánh với ETABS/SAP/SAFE… |
| deformed | Hiển thị → Hình biến dạng… |
| diagrams | Hiển thị → Biểu đồ nội lực |
| modes | Hiển thị → Dạng dao động |
| reactions | Hiển thị → Phản lực gối… |
| resultTables | Hiển thị → Bảng kết quả… |
| showLoads | Hiển thị → Hiển thị tải gán |
| designRc | Thiết kế → Thiết kế BTCT |
| checkSteel | Thiết kế → Kiểm tra thép |
| autoResize | Thiết kế → Tự tăng tiết diện đến khi đạt |
| designFnd | Thiết kế → Thiết kế móng… |
| rebarChoice | Thiết kế → Chọn bố trí thép… |
| rebarPrefs | Thiết kế → Tuỳ chọn chọn thép & gia công… |
| expDxf | Triển khai hồ sơ → Bản vẽ DXF... |
| drawEdit | Triển khai hồ sơ → Hiệu chỉnh bản vẽ… |
| drawAttach | Triển khai hồ sơ → Nạp lại bản vẽ đã chỉnh sửa (DXF/DWG)… |
| shop | Triển khai hồ sơ → Xuất shop drawing thép… |
| sheets | Triển khai hồ sơ → Dàn trang & in bản vẽ… |
| expWord | Triển khai hồ sơ → Thuyết minh Word... |
| autoReport | Triển khai hồ sơ → Lập thuyết minh tự động… |
| expExcel | Triển khai hồ sơ → Bảng tính Excel... |
| quantities | Triển khai hồ sơ → Bảng khối lượng vật tư… |
| expAll | Triển khai hồ sơ → Xuất tất cả vào thư mục... |
| cadStd | Tuỳ chọn → Chuẩn layer CAD… |
| drawStyle | Tuỳ chọn → Thiết lập bản vẽ (tỷ lệ, cỡ chữ)… |
| templates | Tuỳ chọn → Mẫu định dạng (template)… |
| locations | Tuỳ chọn → Danh mục địa danh (QCVN 02:2022)… |
| guide | Trợ giúp → Hướng dẫn sử dụng… |
| quickStart | Trợ giúp → Bắt đầu nhanh… |
| appendix | Trợ giúp → Phụ lục tính toán & hướng dẫn… |
| regs | Trợ giúp → Văn bản pháp lý & tiêu chuẩn áp dụng… |
| verify | Trợ giúp → Kiểm chứng phần mềm… |
| about | Trợ giúp → Giới thiệu |

