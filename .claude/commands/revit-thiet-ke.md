---
description: Điều phối đội agent – từ mô hình Revit kiến trúc (*.vxtrevit.json) đến hồ sơ kết cấu Việt Const, dừng ở các điểm người dùng quyết định.
argument-hint: <file.vxtrevit.json> [thư mục hồ sơ] [địa điểm]
---

Bạn điều phối đội agent làm hồ sơ kết cấu từ mô hình Revit: $ARGUMENTS

Con người là người quyết định cuối cùng. Sau mỗi điểm chốt dưới đây, trình bày kết quả ngắn gọn và CHỜ người dùng đồng ý rồi mới làm tiếp.

1. Giao **kts-kiem-tra-revit** soát file *.vxtrevit.json.
   → Điểm chốt 1: nếu còn mục "Cần sửa" quan trọng (thiếu phòng, phòng Cầu thang quá lớn, tên "Room"), đề nghị người dùng sửa trong Revit và xuất lại; hoặc người dùng đồng ý dùng `--cong-nang` thay thế.
2. Giao **ks-ket-cau** chạy với `--den-buoc tai`, kèm vùng gió theo địa điểm và các `--cong-nang` đã thống nhất.
   → Điểm chốt 2: bảng tải trọng (công năng, tường, thiết bị). Người dùng xác nhận.
3. Giao **ks-ket-cau** chạy hết quy trình.
   → Điểm chốt 3: mặt bằng kết cấu, cảnh báo, T1, cấu kiện không đạt, chi phí. Người dùng quyết định chấp nhận hoặc sửa (quay lại bước 2 hoặc sửa Revit).
4. Chạy song song **trien-khai-ho-so** và **kiem-tra-doc-lap** trên thư mục hồ sơ vừa xuất.
5. Tổng hợp cho người dùng: thư mục hồ sơ, danh mục hồ sơ, kết luận kiểm tra độc lập, việc còn phải làm, và bước đưa kết cấu về Revit (lệnh "Dựng kết cấu từ Việt Const" – chỉ chạy khi người dùng đồng ý vì nó thêm cấu kiện vào file Revit).

Luôn nhắc: hồ sơ phải được người có chứng chỉ hành nghề kiểm tra và ký trước khi phát hành. Không gửi dữ liệu dự án ra ngoài máy.
