# Đội agent Việt Const

Bốn agent (thư mục `.claude/agents`) và lệnh điều phối `/revit-thiet-ke` (thư mục `.claude/commands`) làm hồ sơ kết cấu từ mô hình Revit kiến trúc. Người dùng quyết định ở 3 điểm chốt: soát mô hình Revit, bảng tải trọng, kết quả thiết kế.

| Agent | Vai trò | Sửa file? |
|---|---|---|
| kts-kiem-tra-revit | Soát phòng, cầu thang, cao độ, trục trong file *.vxtrevit.json | Không |
| ks-ket-cau | Chạy Việt Const không giao diện, đọc báo cáo, đề xuất sửa | Chỉ ghi thư mục hồ sơ |
| trien-khai-ho-so | Soát bộ hồ sơ, lập DanhMucHoSo.md, hướng dẫn đưa kết cấu về Revit | Chỉ ghi DanhMucHoSo.md |
| kiem-tra-doc-lap | Kiểm tra tải, phản lực, dao động, tiết diện bằng tính gần đúng | Không |

## Dùng

1. Trong Revit: tab Việt Const → **Xuất sang Việt Const** → được `Project1.vxtrevit.json`.
2. Mở Claude Code trong thư mục repo VXT.KetCau, gõ:
   `/revit-thiet-ke "C:\...\Project1.vxtrevit.json" "C:\...\HoSo" Thái Nguyên`

## Chạy Việt Const không giao diện (dòng lệnh)

```powershell
$p = Start-Process "$env:LOCALAPPDATA\Programs\KetCauVXT\KetCauVXT.exe" `
     -ArgumentList '--revit-thiet-ke','"C:\...\Project1.vxtrevit.json"','"C:\...\HoSo"','--vung-gio','II' -Wait -PassThru
$p.ExitCode   # 0 đạt · 1 cần duyệt / dừng sớm · 2 lỗi
```

Tuỳ chọn: `--ten`, `--vung-gio I..V`, `--w0`, `--dia-hinh A|B|C`, `--khong-gio`, `--cong-nang "Tên phòng=Công năng"`, `--don-gia bt,thep,van`, `--khong-toi-uu`, `--doi-be-rong`, `--den-buoc doc|tai|bo-tri|kiem-tra|phan-tich|toi-uu|ho-so`, `--khong-ho-so`.

Kết quả trong thư mục `<tên>_<ngày giờ>`: BaoCao.md, BaoCao.json (mục "Cần người duyệt"), MoHinh.kcvxt, *.vxt2revit.json, BanVe/, GiaCongThep/, ThuyetMinh.docx, BangTinh.xlsx, BangKhoiLuong.xlsx, NoiLuc/, MoHinh.e2k, MoHinh.s2k.

Chương trình chỉ đọc file đầu vào và ghi vào thư mục ra; không kết nối mạng, không thu thập dữ liệu.

## Đội agent kiến trúc: phương án → mô hình → hồ sơ → phối cảnh

Lệnh điều phối: `/phuong-an-den-ho-so <mô tả hoặc đường dẫn ảnh / PDF / DWG> [ảnh mặt tiền] [thư mục dự án]`.

| Agent | Việc | Công cụ |
|---|---|---|
| kts-phuong-an | Lời / ảnh / PDF / DWG / ảnh mạng → `*.vxtphuongan.json`, kiểm tra 0 lỗi | `KetCauVXT.exe --phuong-an-mau`, `--cad-sang-phuong-an`, `--kiem-tra-phuong-an` |
| kts-mat-tien | Ảnh mặt tiền → phong cách, vật liệu, màu, hướng; đề xuất chỉnh cửa mặt tiền | đọc ảnh, sửa JSON |
| dung-mo-hinh | Dựng Revit (kiến trúc) + SketchUp (khối, vật liệu, 3 cảnh) | `agent\GuiLenh.ps1` → cầu nối Revit / SketchUp |
| trien-khai-ho-so-kien-truc | Mặt bằng, 4 mặt đứng, mặt cắt, phối cảnh, thống kê, tờ KT-xx, PDF + DWG | Revit `tao-ban-ve`, `xuat-pdf`, `xuat-dwg` |
| phoi-canh | Ảnh khối 3 cảnh, render AI (khi người dùng đồng ý), bàn giao Enscape / Vantage | SketchUp `anh`, `render\ai_render.py` |

### Cầu nối agent (mặc định tắt)
- Revit: tab **Việt Const → Phương án → hồ sơ → Cho phép agent** (hỏi xác nhận; mỗi lần mở Revit phải bật lại).
- SketchUp: **Extensions → Việt Const → Cho phép agent điều khiển SketchUp**.
- Lệnh đặt ở `%LOCALAPPDATA%\VietConst\Agent\<revit|sketchup>\in\*.json`, kết quả ở `out\*.ket-qua.json`, trạng thái `trang-thai.json`. Chỉ các lệnh trong danh sách; lệnh dựng / đổi kết cấu luôn hiện hộp thoại để người dùng duyệt. Không mạng, không thu thập dữ liệu.

### Nút mới trên ribbon Revit (panel "Phương án → hồ sơ")
- **Dựng kiến trúc từ phương án**: cao độ + mặt bằng tầng, trục, loại tường trát 2 mặt, tường, sàn chừa lỗ thang, cửa (tạo loại theo kích thước, cần có sẵn 1 họ cửa đi / cửa sổ), phòng có tên, thử dựng cầu thang 2 vế. Phần tử gắn mã `KT-…` → chạy lại chỉ thêm phần thiếu.
- **Bộ bản vẽ kiến trúc**: KT-01… (mặt bằng có tag, mặt đứng, mặt cắt A-A, phối cảnh, thống kê). Kích thước, cao độ, ghi chú, chi tiết: kỹ sư hoàn thiện.
- **Ảnh phối cảnh 3D**: PNG để so / làm nền render.

### Render AI
`python ai_render.py --anh <ảnh khối> --phuong-an <file> [--gio ngay|chieu|toi] [--goc elevation]` – giữ nguyên hình khối, phủ vật liệu theo mô tả mặt tiền. Khoá `GEMINI_API_KEY` của người dùng đặt trên máy; ảnh + câu lệnh chỉ gửi tới Google khi chạy không có `--chi-cau-lenh`.

### Phương án chi tiết (đã chạy thật trên Project1 làm mới)
- Cầu thang ghi theo vế (`Runs`: đường tim theo hướng đi lên + số bậc đứng; `Landings`) → Revit dựng đúng số bậc, mặt bậc, chiếu nghỉ; SketchUp dựng khối bậc. Việt Const kiểm tra h 150–175 mm, b ≥ 250 mm, 2h + b.
- Cửa ghi `Family` + `TypeName`, phương án khai `FamilySources` (VD dự án cũ `Project1.rvt`) → add-in mở ngầm, chép họ cửa nhôm kính, cửa lùa + ô fix, cửa cuốn sang dự án mới, tạo loại đúng kích thước.
- Lệnh cầu nối mới: `liet-ke-ho` (liệt kê họ cửa trong dự án / file mẫu), `dung-kien-truc` + `lam_lai` (dựng lại tường / dầm / cửa / cầu thang / sàn / mặt tiền theo phương án đã sửa, giữ phòng, tag và phần người dùng tự vẽ).
- Phối hợp hình học: `KetCauVXT.exe --phoi-hop <phương án>` – cột, dầm khung theo nhịp, dầm bo, dầm đỡ tường, tường tới đáy dầm / sàn, dời cửa khỏi cột, kiểm tra thang ↔ tường / cột / thông thủy. Revit dựng cột + dầm BTCT, đỉnh tường gắn cao độ tầng trên (offset âm), join hình học cột > sàn > dầm > tường.
- Chi tiết mặt tiền `Facade.Elements` (LanCan kính / thép / xây, Khoi – khung viền, ốp đá, ô văng…, Lam, MaiDoc – mái lệch / mái kính): kts-mat-tien thiết kế; Revit dựng DirectShape có vật liệu màu + sơn mặt ngoài tường, SketchUp dựng nhóm có vật liệu để render V-Ray.
- Mặt đứng 4 phía dựng bằng khung nhìn cắt ngoài mặt nhà; tỷ lệ bản vẽ tự chọn theo kích thước nhà; ảnh 3D / mặt bằng cắt ôm công trình.
