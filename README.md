# VXT Kết cấu

Phần mềm triển khai hồ sơ kết cấu BTCT tự động từ hồ sơ kiến trúc:

**Kiến trúc (Revit hoặc DWG) → khung kết cấu → ETABS / SAP2000 → thiết kế TCVN 5574:2018 → cấu tạo & đánh số thép (kiểu Kết cấu Soft) → thép 3D Revit → shop drawing (dim, ghi chú, thống kê) → thuyết minh.**

## Cấu trúc giải pháp

| Dự án | Nền tảng | Vai trò |
|---|---|---|
| `VXT.KetCau.Core` | .NET Standard 2.0 | Lõi độc lập: mô hình, vật liệu TCVN 5574:2018, tải TCVN 2737:2023, phổ TCVN 9386, sinh khung từ kiến trúc, thiết kế dầm/cột/sàn/móng, cấu tạo thép, đánh số, thống kê, dựng bản vẽ, thuyết minh |
| `VXT.KetCau.Csi` | net48 + net8 | Kết nối ETABS 21–23 / SAP2000 v25–26 qua CSi OAPI (`CSiAPIv1.dll`) |
| `VXT.KetCau.Dwg` | .NET Standard 2.0 | Đọc mặt bằng DWG/DXF (trục, tường, cột, cửa, phòng, thang); ghi shop drawing DXF |
| `VXT.KetCau.Revit` | Revit 2022–2026 | Add-in ribbon “VXT Kết cấu” |
| `VXT.KetCau.Cli` | net8 | Chạy toàn quy trình không cần Revit (`vxtkc`) |
| `tests/VXT.KetCau.Core.Tests` | xUnit | Kiểm tra số liệu TCVN, đánh số, quy trình đầu-cuối |

## Build

Cần **Visual Studio 2022** (hoặc .NET 8 SDK) trên Windows.

Nhanh nhất: chạy `.\build.ps1` trong PowerShell. Lệnh này test phần lõi rồi build CLI và add-in cho Revit 2022–2026. Đẩy lên GitHub thì `.github/workflows/build.yml` tự build và test phần lõi.

```bat
:: Revit (chọn đúng phiên bản; Release tự chép add-in vào %AppData%\Autodesk\Revit\Addins\<năm>)
dotnet build src\VXT.KetCau.Revit -c "Release R24"
dotnet build src\VXT.KetCau.Revit -c "Release R25"

:: Dòng lệnh
dotnet build src\VXT.KetCau.Cli -c Release

:: Kiểm thử lõi
dotnet test tests\VXT.KetCau.Core.Tests
```

- Revit API lấy từ NuGet `Nice3point.Revit.Api.*` — không cần cài Revit để build.
- `CSiAPIv1.dll`: mặc định tìm trong `C:\Program Files\Computers and Structures\ETABS 22`. Nếu cài nơi khác: sửa `CsiApiDir` trong `Directory.Build.props` hoặc thêm `-p:CsiApiDir="..."`.
- DWG cần **ODA File Converter** (miễn phí) để chuyển sang DXF; DXF đọc trực tiếp.

## Triển khai hồ sơ trong AutoCAD (lệnh `VXTHS`)

Add-in AutoCAD 2022–2026 lập bộ hồ sơ gồm cả kiến trúc và kết cấu, có khung tên và layout in. Bộ cài lấy ở mục Releases.

| Bước | Lệnh | Việc làm |
|---|---|---|
| Đầu vào | `VXTHS_QUET` | Nhiều tầng vẽ chung một bản vẽ: khoanh khung từng tầng, chọn điểm gốc. Kết quả nhận diện vẽ chồng lên bản vẽ gốc ở lớp `VXT_NHANDIEN` để đối chiếu (`VXTHS_XOALOP` để xoá) |
| | `VXTHS_FILE` | Mỗi tầng một file DWG/DXF |
| | `VXTHS_THONGSO` | Khai nhịp trục, cao độ tầng, tiết diện, ô thang |
| | `VXTHS_ETABS` | Đọc dầm, cột, sàn chữ nhật và nội lực từ ETABS/SAP2000 đang mở; có thể lấy diện tích thép đã thiết kế để đổi ra số thanh. Không có kiến trúc nên chỉ ra phần kết cấu |
| Kiểm tra | `VXTHS_KIEMTRA` | Sửa trục, tầng, tường, cột, cửa, phòng, thang dạng bảng |
| | `VXTHS_SUAKC` | Sửa tiết diện, đặt thép tay (`3Ø20`, `Ø6a150`); thép đặt tay được so với yêu cầu nếu có nội lực |
| | `VXTHS_THONGTIN` | Khung tên, khổ giấy, tỉ lệ; thông số thiết kế |
| Lập hồ sơ | `VXTHS_VE` | Vẽ các tờ vào Model, mỗi tờ một layout máy in `DWG To PDF.pc3` |
| | `VXTHS_DXF` | Xuất ra file DXF riêng |

**Kiến trúc:** bìa và danh mục, mặt bằng các tầng và mái (3 chuỗi kích thước, trục, cửa, phòng + diện tích, thang), 4 mặt đứng, 2 mặt cắt, thống kê cửa, chi tiết thang đánh số bậc, chi tiết vệ sinh.
**Kết cấu:** ghi chú chung, mặt bằng móng, chi tiết móng, giằng, mặt bằng dầm sàn, thép sàn, chi tiết dầm, cột, bảng thống kê và tổng hợp thép.

Giới hạn: mặt đứng và mặt cắt dựng từ tường, cửa, cao độ nên chỉ là hình cơ bản, chưa có mái dốc, ban công, trang trí. Bố trí thiết bị vệ sinh là gợi ý. Hoàn thiện (trần, lát, ốp), bể phốt, thang kết cấu chưa có.

## Quy trình trong Revit

| Bước | Nút | Việc làm |
|---|---|---|
| 0 | **Thông số** | Bê tông, thép, lớp bảo vệ, W0 gió, agR động đất, sức chịu tải nền, quy tắc sinh khung, ETABS/SAP2000 |
| 1a | **Kiến trúc → Kết cấu** | Đọc file hiện hành hoặc file kiến trúc liên kết; chọn cao độ làm tầng; tự sinh cột, dầm khung, dầm phụ đỡ tường, sàn, móng, giằng; tải sàn theo công năng phòng, tải tường lên dầm |
| 1b | **DWG kiến trúc → Kết cấu** | Khai báo file mặt bằng + cao độ từng tầng |
| 1c | **Đọc kết cấu Revit** | Dùng kết cấu bạn tự dựng (cột/dầm/sàn kết cấu, cần tham số `b`, `h`) |
| 2 | **ETABS / SAP – Tạo & chạy** | Tạo vật liệu, tiết diện, tầng, phần tử, ngàm chân cột, diaphragm, tải, gió, phổ động đất, tổ hợp; chạy; đọc nội lực |
| 2' | **Đọc lại nội lực** | Sau khi bạn chỉnh tay và chạy lại trong ETABS/SAP |
| 3 | **Thiết kế & cấu tạo** | Thiết kế, bố trí thép, đánh số, gộp cấu kiện giống nhau; xuất *Thuyết minh kết cấu.html* và *Thống kê cốt thép.csv* |
| 4 | **Tạo thép 3D** | Rebar Revit đúng hình dạng, Partition = tên cấu kiện, Mark = số hiệu |
| 5 | **Shop drawing** | Mặt đứng + mặt cắt dầm (thép thật, dim thật), chi tiết cột, bảng thống kê, Rebar Schedule; xếp tờ KC-xx |
| 6 | **Xuất DXF** | Toàn bộ shop drawing ra AutoCAD |

Trạng thái dự án lưu ở `<tên file>.vxtkc.json` cạnh file `.rvt`; kết quả xuất ở thư mục `<tên file>_KetCau`.

## Quy trình dòng lệnh (không cần Revit)

```bat
vxtkc mau cauhinh.json        :: tạo cấu hình mẫu → sửa đường dẫn DWG, cao độ, thông số
vxtkc chay cauhinh.json       :: đọc DWG → khung → ETABS/SAP (hoặc "Sơ bộ") → thiết kế → DXF + thuyết minh + CSV
```

## Quy ước lớp DWG kiến trúc

Nhận theo từ khoá trong tên lớp (không phân biệt hoa thường), sửa được trong `LayerMap`:
trục `TRUC/AXIS/GRID`, tường `TUONG/WALL`, cột `COT/COLUMN`, cửa `CUA/DOOR/WINDOW`, thang `THANG/STAIR`, sàn `SAN/SLAB`, tên phòng là chữ chứa *phòng, ngủ, khách, WC, bếp, ban công, sân thượng…*
Tường nhận bằng cặp nét song song cách nhau 90–450 mm. Đơn vị m/cm/mm tự nhận biết.

## Thể hiện bản vẽ (kiểu Kết cấu Soft)

- Mỗi cấu kiện đánh số từ 1; thứ tự: thép dưới, trên, mũ gối, tăng cường, giá → đai, đai C, đai lồng, thép rải; trái → phải.
- Khác vai trò/Ø/chiều dài/hình dạng → khác số; trên và dưới giống hệt vẫn khác số; thanh đối xứng dùng chung số.
- Chiều dài làm tròn 10 mm; đai tính 2 móc 135° (max(75, 10d) mỗi móc).
- Nhãn: chấm → đường dẫn → vai có chữ → vòng tròn số hiệu. Thép dọc `3Ø22`, đai/thép rải `Ø6a100` (đúng bước tại vị trí cắt).
- Mặt đứng dầm: chuỗi vùng đai trên cùng (ranh giới giữa 2 đai, tổng = nhịp thông thuỷ), nhãn nửa trên/dưới xếp tầng, vạch cắt `1|`, kích thước nhịp + tổng, trục, cao độ ▽.
- Tên hình `D2-8 (SL=2; L=20300)` / `C2 (SL=4)`, tỉ lệ mặt đứng 1/50, mặt cắt 1/25, chữ in 2,5 mm.
- Bảng thống kê đủ cột theo mẫu + bảng tổng hợp theo Ø và nhóm Ø≤10, 10<Ø≤18, Ø>18.
- DXF: lớp `BE_TONG, THEP, DAI, KICH_THUOC, CHU, KY_HIEU`, mm, chữ Việt mã hoá `\U+XXXX`.

## Phạm vi và giới hạn hiện tại

- Khung BTCT cột–dầm–sàn, móng đơn; lưới trục trực giao cho sinh sàn tự động (lưới xiên: sàn lấy theo đường bao kiến trúc, chưa cấu tạo thép sàn không chữ nhật).
- Vách, lõi, móng cọc, dầm cong, sàn không dầm: mô hình đọc/xuất được phần khung, **chưa** có thiết kế & cấu tạo thép riêng.
- Nội lực sàn: dùng phương pháp tra bảng (bản kê 4 cạnh ngàm / bản loại dầm); nội lực dầm, cột lấy từ ETABS/SAP.
- Xoắn dầm: chỉ cảnh báo khi đáng kể. Kiểm tra võng, nứt: chưa tính chi tiết.
- Chế độ **Sơ bộ** (không chạy ETABS/SAP) chỉ để tham khảo.
- Hệ số tải, W0, agR, R nền mặc định cần kỹ sư kiểm tra lại theo hồ sơ thực tế. **Kỹ sư thiết kế chịu trách nhiệm cuối cùng về hồ sơ phát hành.**
