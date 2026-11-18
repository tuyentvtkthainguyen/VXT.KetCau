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
