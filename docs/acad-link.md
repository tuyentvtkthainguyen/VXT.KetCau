# Liên kết AutoCAD → Kết cấu VXT

Add-in **KetCauVXT.AcadLink** cho AutoCAD 2021–2026 cho phép tích chọn trực tiếp trên bản vẽ kiến trúc các đối tượng
**tường, cột, dầm, sàn, lưới trục, cửa, cầu thang, tên phòng**, khai báo các tầng (nhiều mặt bằng vẽ cạnh nhau trong một DWG)
rồi gửi sang chương trình **Kết cấu VXT** để triển khai hồ sơ kết cấu (đề xuất cột, dầm, sàn, tải → tạo mô hình).

## 1. Cài đặt

1. Giải nén bản phát hành `KetCauVXT-win-x64.zip`, mở thư mục `AutoCAD`.
2. Đóng AutoCAD, bấm đúp **`CaiDatAutoCAD.cmd`** (chạy `CaiDatAutoCAD.ps1`): thư mục `KetCauVXT.bundle` được chép vào
   `%APPDATA%\Autodesk\ApplicationPlugins\`. Gỡ cài đặt: `CaiDatAutoCAD.cmd -Go`.
3. Mở AutoCAD (nếu được hỏi có nạp add-in không, chọn *Always Load / Luôn nạp*), gõ **`VXT`** để hiện bảng lệnh.
4. Mở chương trình `KetCauVXT.exe` ít nhất một lần: chương trình ghi đường dẫn của nó vào `%AppData%\KetCauVXT\app_path.txt`
   để add-in tự mở chương trình khi gửi dữ liệu.

Gói `KetCauVXT.bundle` (autoloader của AutoCAD):

```
KetCauVXT.bundle\
  PackageContents.xml            (SeriesMin/SeriesMax theo từng phiên bản)
  Contents\2021\KetCauVXT.AcadLink.dll   R24.0  (.NET Framework 4.8)
  Contents\2022\…                         R24.1
  Contents\2023\…                         R24.2
  Contents\2024\…                         R24.3
  Contents\2025\…                         R25.0  (.NET 8)
  Contents\2026\…                         R25.1  (.NET 8)
```

Có thể nạp thủ công bằng lệnh `NETLOAD` (chọn DLL đúng năm AutoCAD).

## 2. Các lệnh

| Lệnh | Việc làm |
|---|---|
| `VXT` (hoặc `VXT_MENU`) | Bảng lệnh: các nút gán nhóm, khai báo tầng, xem, xoá, chuẩn layer, gửi dữ liệu; danh sách số đối tượng đã gán, layer đã gán, các tầng. |
| `VXT_TUONG`, `VXT_COT`, `VXT_DAM`, `VXT_SAN`, `VXT_TRUC`, `VXT_CUA`, `VXT_THANG`, `VXT_TENPHONG` (và `VXT_BOQUA`) | Gán nhóm cho đối tượng. Chọn trước rồi gọi lệnh, hoặc gọi lệnh rồi chọn **[DoiTuong/Layer]**: <br>• *DoiTuong*: quét chọn (đường, đa tuyến, đa tuyến cũ, cung, đường tròn, block, chữ, MText, miền tô) – nhóm được ghi vào XData `KETCAU_VXT` của từng đối tượng (lưu trong DWG). <br>• *Layer*: chọn một đối tượng → **mọi** đối tượng trên layer đó thuộc nhóm; bảng layer → nhóm được ghi nhớ trong DWG. |
| `VXT_TANG` | Khai báo tầng **[Them/Danhsach/Xoa]**: tên (vd. *Tầng 1*, *Tầng 2*, *Tum*, *Mái*), cao độ mặt sàn (m), chiều cao tầng (m), **điểm gốc** (chọn cùng một giao trục, vd. 1–A, ở mọi tầng) và **vùng** mặt bằng (2 góc). Khung vùng vẽ trên layer `VXT-PHAMVI-TANG` (không in). Khai báo lại cùng tên → thay thế. |
| `VXT_XEM` | In số đối tượng theo nhóm, tô sáng + chọn sẵn các đối tượng của một nhóm (hoặc tất cả). Esc/REGEN để bỏ. |
| `VXT_XOA` | Bỏ gán nhóm **[DoiTuong/Layer/TatCa]**. |
| `VXT_LAYER` | Tạo/cập nhật layer kết cấu (KC-TUONG, KC-COT, KC-DAM, KC-SAN, KC-TRUC, KC-CUA, KC-THANG, KC-TENPHONG + các layer xuất của chuẩn layer), tuỳ chọn **chuyển đối tượng đã gán nhóm sang layer kết cấu** (nhóm được giữ bằng XData), tạo kiểu chữ `VXT_ARIAL` (Arial) hoặc `VXT_TIMES` (Times New Roman) và đặt hiện hành. |
| `VXT_GUI` | Gửi dữ liệu: ghi `%AppData%\KetCauVXT\Link\acad_<ngày>_<giờ>.json` rồi báo chương trình (đang mở → chương trình hỏi *“Có dữ liệu mới từ AutoCAD (n tầng). Mở hộp thoại nhập?”*; chưa mở → tự chạy `KetCauVXT.exe --acad "<file>"`). |

Thứ tự xác định nhóm của một đối tượng khi gửi: **XData của đối tượng** → **bảng layer → nhóm của bản vẽ** → (nếu chọn *Co* ở câu hỏi
“nhận theo tên layer”) **chuẩn layer của Kết cấu VXT** (`%AppData%\KetCauVXT\CadStandard.json`: `InputLayerMap`, rồi `InputRules`
theo ưu tiên; không có file → từ khoá mặc định TUONG/WALL, TRUC/GRID, COT/COLUMN, CUA/DOOR, THANG/STAIR, DAM/BEAM, SAN/SLAB…).
Nhóm `BoQua` và layer đóng băng bị bỏ qua. Mỗi đối tượng thuộc tầng có vùng chứa điểm đại diện của nó (tâm khung bao; chữ/block: điểm chèn).
Chưa khai báo tầng → có thể gửi toàn bộ bản vẽ thành một tầng.

Chuỗi nhóm cố định (dùng chung với chương trình): `Tuong`, `Cot`, `Dam`, `San`, `Truc`, `Cua`, `Thang`, `TenPhong`, `BoQua`.

### Gợi ý vẽ để nhận diện tốt

* Tường vẽ **2 nét song song** (80–450 mm); tường 1 nét lấy chiều dày mặc định 110 mm. Khe ≤ 1,6 m giữa hai đoạn thẳng hàng = lỗ cửa.
* Cột: đa tuyến kín, miền tô, hoặc 4 nét rời (tự ghép thành hình chữ nhật); cột tròn quy về hình vuông ngoại tiếp. Kích thước 0,12–1,0 m.
* Lưới trục: nét thẳng + chữ/thuộc tính tên trục (≤ 4 ký tự) gần đầu trục (≤ 1,5 m); gán cả đường tròn ký hiệu trục vào nhóm Trục.
* Cửa: block cửa (giữ tên block, vd. `CUA_D1`) – block được phân rã ảo trong bộ nhớ, bản vẽ không bị sửa.
* Tên phòng: chữ “P.NGỦ”, “BẾP”, “WC”, “BAN CÔNG”, “THÔNG TẦNG”… quyết định tải hoàn thiện và hoạt tải.
* Dầm: nét tim hoặc 2 nét mép; sàn: đa tuyến kín (đa tuyến nằm trong đa tuyến khác = lỗ sàn).

## 3. Trong chương trình Kết cấu VXT

* **Tệp → Nhận dữ liệu từ AutoCAD…**: chọn file `acad_*.json` (mặc định mở thư mục `%AppData%\KetCauVXT\Link`).
* Chương trình theo dõi thư mục `Link`: file mới → hỏi mở hộp thoại nhập.
* Dòng lệnh: `KetCauVXT.exe --acad "D:\…\acad_20261102_081500.json"`.

Hộp thoại *Nhập bản vẽ kiến trúc* mở với các tầng đã nhận diện (tên, chiều cao lấy theo khai báo `VXT_TANG`) và phương án kết cấu
đề xuất; sửa thông số, bỏ chọn tầng, bật/tắt cột rồi bấm *Tạo mô hình*.

Cách đọc: mỗi đối tượng được đổi thành thực thể DXF trên **layer tổng hợp** theo nhóm — `VXT_TUONG`, `VXT_COT`, `VXT_DAM`, `VXT_SAN`,
`VXT_TRUC`, `VXT_CUA`, `VXT_THANG`, `VXT_TENPHONG` — dời về điểm gốc tầng, rồi chạy bộ nhận diện bản vẽ kiến trúc
(`ArchPlanReader.FromEntities`) cho từng tầng; các layer tổng hợp được gán thẳng vào nhóm (bảng gán layer + từ khoá lớp).
Các tầng được dời về chung gốc = giao trục X nhỏ nhất / trục Y nhỏ nhất của tầng dưới cùng. Nếu mọi tầng chưa có điểm gốc riêng
(cùng toạ độ gốc nhưng vùng khác nhau) thì căn theo tên trục.

Mã nguồn: `src/KetCauVXT.Core/Import/Acad/AcadExchange.cs` (lược đồ), `AcadLinkReader.cs` (chuyển đổi),
`src/KetCauVXT.App/MainWindow.AcadLink.cs`, `ArchImportDialog.AcadLink.cs` (giao diện), `src/KetCauVXT.AcadLink/` (add-in).

## 4. Lược đồ file trao đổi (Version 1)

```json
{
  "Version": 1,
  "Source": "AutoCAD 2025",
  "Dwg": "D:\\DuAn\\NhaPho.dwg",
  "Units": "m",
  "Created": "2026-11-02T08:15:00",
  "Storeys": [
    {
      "Name": "Tầng 1", "Elevation": 0, "Height": 3.6,
      "BaseX": 0, "BaseY": 0,
      "X0": -4, "Y0": -4, "X1": 9, "Y1": 13,
      "Items": [
        { "Category": "Tuong", "Layer": "KT-TUONG", "Kind": "Line", "Points": [[0, -0.11], [5, -0.11]] },
        { "Category": "Cot", "Layer": "KT-COT", "Kind": "Polyline", "Points": [[-0.11,-0.11],[0.11,-0.11],[0.11,0.11],[-0.11,0.11]], "Closed": true },
        { "Category": "Tuong", "Layer": "KT-TUONG", "Kind": "Polyline", "Points": [[0,0],[2,0],[2,1]], "Bulges": [1, 0, 0] },
        { "Category": "Truc", "Layer": "KT-TRUC", "Kind": "Circle", "Points": [[0, 11.4]], "Radius": 0.4 },
        { "Category": "Truc", "Layer": "KT-TRUC", "Kind": "Text", "Points": [[0, 11.4]], "Text": "1", "Height": 0.35, "Block": "TRUC_BUBBLE" },
        { "Category": "Cua", "Layer": "KT-CUA", "Kind": "Block", "Points": [[3.8, 4.5]], "Block": "CUA_D1", "Rotation": 0 },
        { "Category": "Cua", "Layer": "KT-CUA", "Kind": "Arc", "Points": [[3.8, 4.5]], "Radius": 0.9, "StartAngle": 0, "EndAngle": 90, "Block": "CUA_D1" },
        { "Category": "TenPhong", "Layer": "KT-TEXT", "Kind": "Text", "Points": [[2.5, 2.2]], "Text": "P.KHÁCH", "Height": 0.25, "Rotation": 0 }
      ]
    }
  ]
}
```

| Trường | Ý nghĩa |
|---|---|
| `Version` | 1 |
| `Source` | Phần mềm nguồn (`AutoCAD 2021` … `AutoCAD 2026`) |
| `Dwg` | Đường dẫn bản vẽ |
| `Units` | Đơn vị toạ độ: add-in luôn ghi `"m"` (đã đổi theo `$INSUNITS`: 1 inch, 2 feet, 4 mm, 5 cm, 6 m, 14 dm; không khai báo → coi là mm). Chương trình đọc được `"m"`, `"cm"`, `"mm"`, `"dm"`, `"in"`, `"ft"`. |
| `Storeys[].Name` | Tên tầng. Chứa “TUM” → tầng tum; chứa “MÁI” (không có “TẦNG n”) → mặt bằng mái. Các tầng còn lại đánh số 1, 2, … theo cao độ. |
| `Storeys[].Elevation` | Cao độ mặt sàn, **m** |
| `Storeys[].Height` | Chiều cao tầng, **m** (0 → hiệu cao độ với tầng trên, hoặc mặc định 3,6 / 3,3 / 3,0 m) |
| `Storeys[].BaseX/BaseY` | Điểm gốc tầng (cùng đơn vị `Units`) – mọi toạ độ của tầng được trừ đi điểm này |
| `Storeys[].X0…Y1` | Vùng tầng (tham khảo) |
| `Items[].Category` | `Tuong`, `Cot`, `Dam`, `San`, `Truc`, `Cua`, `Thang`, `TenPhong`, `BoQua` |
| `Items[].Layer` | Layer gốc trong AutoCAD |
| `Items[].Kind` | `Line` (2 điểm), `Polyline` (các đỉnh, `Closed`, `Bulges`), `Circle` (tâm, `Radius`), `Arc` (tâm, `Radius`, `StartAngle`, `EndAngle` – độ, ngược chiều kim đồng hồ), `Text` (điểm chèn, `Text`, `Height`, `Rotation`), `Block` (điểm chèn, `Block`, `Rotation` – chỉ để tham khảo; hình học của block đã được phân rã thành các Item khác mang cùng tên `Block`) |
| `Items[].Bulges` | Độ phình từng đỉnh đa tuyến = tan(góc ở tâm / 4), dương = ngược chiều kim đồng hồ; đỉnh cuối dùng khi `Closed`. Chương trình chia cung thành đoạn ≤ 10°. |
| `Items[].Block` | Tên block ngoài cùng chứa đối tượng (block động → tên block gốc). Nét trên layer “0” trong block lấy layer của block. |

Miền tô (HATCH) được ghi thành `Polyline` kín theo biên (đoạn thẳng, cung tròn). Elip, spline chưa hỗ trợ (bỏ qua, có báo số lượng).

## 5. Build add-in

```
dotnet build src/KetCauVXT.AcadLink -c Release -p:AcadVersion=2025      (2021 … 2026)
```

Máy có cài AutoCAD đúng năm → tham chiếu `accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll` trong `C:\Program Files\Autodesk\AutoCAD <năm>\`;
không có (CI) → gói NuGet **AutoCAD.NET** của Autodesk (chỉ để biên dịch, `ExcludeAssets=runtime`):

| AutoCAD | Mã | Framework | Gói `AutoCAD.NET` |
|---|---|---|---|
| 2021 | R24.0 | net48 | 24.0.0 |
| 2022 | R24.1 | net48 | 24.1.51000 |
| 2023 | R24.2 | net48 | 24.2.0 |
| 2024 | R24.3 | net48 | 24.3.0 |
| 2025 | R25.0 | net8.0-windows | 25.0.1 |
| 2026 | R25.1 | net8.0-windows | 25.1.0 |

(AutoCAD 2027 dùng .NET 10 – gói 26.0.0 – chưa đưa vào.) CI (`.github/workflows/ketcau-vxt.yml`, job *build-test*) build cả 6 phiên bản,
đóng gói `publish\AutoCAD\KetCauVXT.bundle` + `CaiDatAutoCAD.ps1/.cmd` + hướng dẫn này vào file zip phát hành.
