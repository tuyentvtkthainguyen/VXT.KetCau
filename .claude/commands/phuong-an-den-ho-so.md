---
description: Điều phối đội agent kiến trúc – từ phương án mặt bằng + ảnh mặt tiền đến mô hình Revit / SketchUp, hồ sơ kiến trúc, phối cảnh (và kết cấu Việt Const nếu người dùng muốn), dừng ở các điểm người dùng quyết định.
argument-hint: <mô tả / đường dẫn ảnh, PDF, DWG mặt bằng> [ảnh mặt tiền …] [thư mục dự án]
---

Bạn điều phối đội agent làm hồ sơ từ phương án của người dùng: $ARGUMENTS

Con người là người quyết định. Sau mỗi điểm chốt, trình bày ngắn gọn (bảng, ảnh) và CHỜ người dùng đồng ý rồi mới làm tiếp. Không gửi dữ liệu dự án ra ngoài máy, trừ bước render AI khi người dùng đã đồng ý rõ ràng.

0. Chuẩn bị: thư mục dự án (hỏi nếu chưa có, VD `D:\DuAn\<tên>`), kích thước lô đất, số tầng, địa điểm, hướng mặt tiền. Thiếu thì hỏi gộp một lần.
1. **kts-phuong-an** lập `<thư mục>\<tên>.vxtphuongan.json` từ đầu vào (lời, ảnh / PDF, DWG / DXF, ảnh tham khảo trên mạng).
   → **Điểm chốt 1 – mặt bằng**: bảng phòng / diện tích từng tầng + các giả định. Người dùng sửa / duyệt.
2. **kts-mat-tien** điền mặt tiền từ ảnh + mô tả, đề xuất chỉnh cửa mặt tiền.
   → **Điểm chốt 2 – mặt tiền**: phong cách, bảng vật liệu – màu, chỉnh cửa. Người dùng duyệt.
3. **dung-mo-hinh** dựng Revit + SketchUp (cầu nối agent phải được người dùng bật trong từng ứng dụng – nếu chưa, hướng dẫn bật rồi chờ).
   → **Điểm chốt 3 – mô hình**: ảnh 3D Revit + ảnh khối SketchUp, phần chưa dựng được. Người dùng duyệt hoặc quay lại bước 1 / 2.
4. Chạy song song:
   - **trien-khai-ho-so-kien-truc**: bộ tờ KT, PDF + DWG, danh sách việc làm tay.
   - **phoi-canh**: ảnh khối 3 cảnh. Hỏi người dùng: "Gửi ảnh khối + mô tả mặt tiền tới Google Gemini để render AI (dùng khoá GEMINI_API_KEY của bạn trên máy)?" — chỉ render khi người dùng trả lời đồng ý; không thì đưa câu lệnh render + hướng dẫn Enscape / Vantage.
   → **Điểm chốt 4 – hồ sơ và phối cảnh**.
5. (Tuỳ người dùng) Kết cấu: Revit `xuat-viet-const` → chạy quy trình `/revit-thiet-ke` với file vừa xuất (điểm chốt riêng của quy trình đó). Đưa kết cấu về Revit ("Dựng kết cấu từ Việt Const") chỉ khi người dùng đồng ý – lệnh này luôn hiện hộp thoại để người dùng duyệt.
6. Tổng hợp: các file (.vxtphuongan.json, .rvt, .skp, PDF, DWG, ảnh), việc còn lại, ai làm.

Luôn nhắc: hồ sơ phải được người có chứng chỉ hành nghề kiểm tra, ký trước khi phát hành; ảnh render AI chỉ minh hoạ.
