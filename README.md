# VXT Giải Kết Cấu

Chọn dạng bài, điền số, nhận lời giải từng bước (công thức → thay số → kết quả → kiểm tra). Xuất CAD (DXF), Word, PowerPoint, PDF.

## 35 dạng bài

- **Sức bền vật liệu:** đặc trưng hình học; thanh bậc kéo nén; uốn dầm tĩnh định (Q, M, σ, τ, võng); xoắn trục; vòng Mohr, thuyết bền; nén lệch tâm, uốn xiên; ổn định thanh nén.
- **Cơ học kết cấu:** dầm nhiều nhịp có khớp; dầm liên tục ba mômen; khung theo phương pháp chuyển vị; khung theo phương pháp lực; dàn tách nút; vòm ba khớp; đường ảnh hưởng, đoàn tải.
- **BTCT (TCVN 5574:2018):** dầm chữ nhật tính và kiểm tra; dầm chữ T; cốt đai; cột lệch tâm phẳng, lệch tâm xiên; sàn; vết nứt, độ võng; chọc thủng.
- **Nền móng (TCVN 9362:2012, TCVN 10304:2014):** sức chịu tải R; móng đơn; móng băng; lún phân tầng; sức chịu tải cọc; móng cọc, đài cọc; tường chắn.
- **Kết cấu thép (TCVN 5575):** dầm I; cột nén đúng tâm; cột nén lệch tâm; bu lông; hàn.

## Chạy từ mã nguồn

```
pip install -r requirements.txt
python run_app.py            # cửa sổ riêng
python run_app.py --browser  # mở bằng trình duyệt
python run_app.py --selftest # giải và xuất thử mọi dạng bài
python -m pytest tests -q    # kiểm thử
```

## Tạo bộ cài Windows

Đẩy thư mục này lên một kho GitHub. GitHub Actions (`.github/workflows/build.yml`) tự chạy kiểm thử, đóng gói, tự kiểm tra file chạy, tạo `VXTGiaiKetCau_Setup_x.y.z.exe` trong mục Artifacts của lần chạy. Máy dùng cần Windows 10/11 có WebView2 (Windows 11 có sẵn); không có thì phần mềm tự mở bằng trình duyệt.

## Thêm dạng bài

Viết một hàm trong `vxtgiai/solvers/`, gắn `@problem(...)` mô tả ô nhập. Giao diện và 4 định dạng xuất tự có.

## Giới hạn cần biết

Xem màn hình lời giải: các dạng bài dùng cách gần đúng đều ghi rõ lưu ý ngay trong kết quả. Kết quả là công cụ hỗ trợ, kỹ sư chịu trách nhiệm kiểm tra trước khi dùng cho hồ sơ thật.

Phông chữ Liberation: giấy phép SIL OFL, xem `vxtgiai/fonts/LICENSE-Liberation.txt`.
