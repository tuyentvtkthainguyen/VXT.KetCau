# Kết cấu VXT

Phần mềm phân tích – thiết kế kết cấu cho **nhà dân dụng dưới 7 tầng** (khung BTCT) và **nhà thép tiền chế** (khung zamil), xuất **bản vẽ DXF**, **thuyết minh Word**, **bảng tính Excel**, và **mô hình ETABS (.e2k) / SAP2000 (.s2k)** để kiểm tra chéo nội lực.

Tác giả: Vũ Xuân Tuyến · Nền tảng: C# .NET 8, WPF (Windows) · Không dùng gói NuGet nào.

---

## 1. Cài đặt và build

Yêu cầu: Windows 10/11, **Visual Studio 2022 (17.8 trở lên)** với workload *.NET desktop development* (đã gồm .NET 8 SDK).

```
Mở KetCauVXT.sln → chọn KetCauVXT.App làm Startup Project → F5
```

Hoặc dòng lệnh:

```
dotnet build KetCauVXT.sln -c Release
dotnet run --project tests/KetCauVXT.Tests            # chạy kiểm chứng + ví dụ mẫu
dotnet run --project tests/KetCauVXT.Tests -- D:\KetQua   # ghi kết quả ví dụ vào D:\KetQua
dotnet run --project src/KetCauVXT.App                 # mở giao diện
```

**Không muốn build?** Mỗi lần đẩy mã lên nhánh `ketcau-vxt`, GitHub Actions (`.github/workflows/ketcau-vxt.yml`) tự build trên Windows, chạy toàn bộ kiểm thử và đóng gói. Vào tab *Actions* của repo → lần chạy mới nhất → mục *Artifacts*:
- `KetCauVXT-win-x64`: file `KetCauVXT.exe` chạy ngay, không cần cài .NET;
- `KetQuaMau`: bản vẽ DXF, thuyết minh Word, bảng tính Excel, file .e2k/.s2k của 3 công trình mẫu, kèm PDF xuất bằng LibreOffice và ảnh chụp màn hình giao diện;
- `KetCauVXT.RevitExport-2023…2026`: add-in Revit cho từng phiên bản.

Add-in Revit: máy có cài Revit thì tham chiếu DLL trong thư mục cài đặt; máy không có Revit tự dùng gói tham chiếu `Nice3point.Revit.Api` từ NuGet. Chọn phiên bản bằng `-p:RevitVersion=2025` (2025 trở đi tự chuyển sang .NET 8).

## 2. Cấu trúc mã nguồn

```
KetCauVXT.sln
src/KetCauVXT.Core/            Thư viện lõi (dùng chung cho giao diện, dòng lệnh, add-in)
  Model/        Vật liệu, tiết diện, trục, tầng, nút, thanh, sàn, tải, tổ hợp, dự án (.kcvxt = JSON)
                ModelBuilder: tạo khung BTCT từ lưới trục, khung thép tiền chế, chia thanh tại nút
  Analysis/     Phần tử hữu hạn khung không gian: ma trận skyline LDLᵀ, phần tử Timoshenko 12 bậc tự do,
                giải phóng liên kết, thanh vát chia đoạn, sàn cứng (diaphragm), nội lực theo mặt cắt
  Loads/        Dồn tải sàn 45°, gió TCVN 2737:2023, động đất TCVN 9386:2012, tự sinh tổ hợp
  Design/       Kết quả thiết kế (hợp đồng dữ liệu)
    Concrete/   Dầm, cột (lệch tâm xiên), sàn, móng đơn – TCVN 5574:2018, TCVN 9362:2012
    Steel/      Khung thép tiền chế, xà gồ – TCVN 5575
  Drawing/      Bản vẽ DXF theo quy tắc tag thép kiểu Kết cấu Soft + bảng thống kê cốt thép
  Report/       Thuyết minh .docx, bảng tính .xlsx, bảng nội lực CSV (cột giống bảng ETABS)
  Export/       ETABS .e2k, SAP2000 .s2k, so sánh nội lực với bảng ETABS/SAP
  Import/       Đọc DXF (trục, cột, dầm, sàn), đọc JSON từ add-in Revit
src/KetCauVXT.App/             Giao diện WPF
src/KetCauVXT.RevitExport/     Add-in Revit xuất mô hình kết cấu sang JSON (build riêng, cần RevitAPI.dll)
tests/KetCauVXT.Tests/         Kiểm chứng lời giải giải tích + chạy toàn bộ chuỗi trên nhà phố và nhà xưởng mẫu
```

Đơn vị nội bộ: **kN, m**; ứng suất kN/m². Giao diện, bản vẽ, thuyết minh hiển thị mm, MPa, kN.m.

## 3. Quy trình làm việc

1. **Công trình** – nhập bước cột theo X, Y (vd. `5`, `4.5 4.5 4.5`), chiều cao tầng (`3.9 3.3 3.3 3.3`), cao độ chân cột, tiết diện, tải hoàn thiện / hoạt tải / tường biên, thông số gió, động đất, nền đất → **Tạo mô hình**.
   Hoặc **Tệp → Nhập DXF** (layer `TRUC`, `COT`, `DAM`, `SAN`) / **Nhập từ Revit (JSON)**.
   Nhà xưởng: **Mô hình → Tạo khung thép tiền chế** (nhịp, cao đỉnh cột, độ dốc, số bước khung, tiết diện vát).
2. **Tải trọng** – sửa trường hợp tải, γf, tải tường trên dầm, tải sàn; **Phân tích → Tạo tổ hợp tải**.
3. **Phân tích → Chạy phân tích**. Xem nội lực, phản lực, biểu đồ M3 khi chọn thanh trên mặt bằng.
4. **Thiết kế → Thiết kế BTCT / Kiểm tra thép**. Cấu kiện không đạt tô đỏ.
5. **Xuất → Xuất tất cả vào thư mục**: bản vẽ DXF, thuyết minh, bảng tính, nội lực CSV, `.e2k`, `.s2k`, file dự án.

## 4. Kiểm tra chéo với ETABS / SAP2000

| Cách | File | Ghi chú |
|---|---|---|
| SAP2000 – mô hình giống hệt | `MoHinh.s2k` | Tải sàn đã dồn sẵn lên dầm, sàn cứng bằng ràng buộc Diaphragm, thanh vát chia đoạn như khi tính. Nội lực phải trùng gần như tuyệt đối. Mở: *File > Import > SAP2000 .s2k Text File*. |
| ETABS – kiểm tra cả bước dồn tải | `MoHinh.e2k` | Sàn xuất là phần tử Membrane kèm tải sàn, ETABS tự dồn tải. Mở: *File > Import > ETABS .e2k Text File*. |
| So sánh tự động | `ForceComparer` | Xuất bảng *Element Forces – Frames* từ ETABS/SAP ra CSV, nạp vào chương trình → báo sai lệch từng thanh/trường hợp tải. |

Tên thanh: `.e2k` dùng `C{Id}` (cột), `B{Id}` (dầm), `D{Id}` (thanh xiên); `.s2k` dùng `{Id}` (đoạn vát `{Id}_k`). Quy ước dấu nội lực giống ETABS (P kéo dương, M3 dương căng thớ dưới dầm), cột vẽ từ dưới lên.

Nguyên nhân sai lệch thường gặp khi so với ETABS: ETABS mặc định có vùng cứng đầu thanh (*End length offsets*) và hệ số giảm độ cứng (*Property modifiers*) nếu người dùng gán; hãy tắt hoặc khai báo giống nhau ở hai bên.

## 5. Tiêu chuẩn và các giả thiết đơn giản hoá

| Hạng mục | Cơ sở | Giả thiết / giới hạn của phiên bản 1.0 |
|---|---|---|
| Tải trọng | TCVN 2737:2023 | γf theo trường hợp tải (người dùng sửa). Gió tĩnh: W = W0·k(z)·c, k(z) dạng hàm mũ theo địa hình A/B/C – **cần đối chiếu W0 và bảng k(z) của địa danh theo QCVN 02:2022 / TCVN 2737:2023**. Chưa tính thành phần động của gió (nhà ≤ 40 m thường không cần). |
| Động đất | TCVN 9386:2012 | Phương pháp tĩnh lực ngang tương đương, T1 = Ct·H^¾, phổ loại 1. Chưa xét độ lệch tâm ngẫu nhiên. |
| Phân tích | – | Tuyến tính bậc nhất (chưa P-Delta). Sàn tuyệt đối cứng trong mặt phẳng, không tham gia chịu uốn của khung; tải sàn dồn 45°. |
| Dầm | TCVN 5574:2018 | Tiết diện chữ nhật (chưa kể cánh T – thiên về an toàn); cắt với c = 2h0; võng dài hạn với độ cứng B = Eb/(1+φb,cr)·Ieff (kể nứt theo thép thực bố trí, φb,cr = 2,5), dầm liên tục f = 5/48·[Mg − 0,1(Mt+Mp)]·L²/B. |
| Cột | TCVN 5574:2018 | Cốt thép đối xứng, lệch tâm xiên bằng đường bao tải trọng (load contour), uốn dọc theo hệ số η. |
| Sàn | – | Ô chữ nhật, phương pháp Marcus theo điều kiện biên, bỏ qua giảm mô men xoắn. Võng dài hạn kể nứt + từ biến, hệ số liên kết cạnh. |
| Móng | TCVN 9362:2012, TCVN 5574:2018 | Móng đơn trên nền thiên nhiên: sức chịu tải R, áp lực đáy móng, chọc thủng, uốn. **Chưa tính lún**, chưa có móng cọc / móng băng (dự kiến phiên bản sau, TCVN 10304:2014). |
| Thép tiền chế | TCVN 5575 | Bền, ổn định trong/ngoài mặt phẳng (φ theo độ mảnh quy ước), độ mảnh [λ] = 180 − 60α, ổn định cục bộ bụng theo α = (σ−σ1)/σ (TCVN 5575:2012 7.6.2), bụng mảnh kể tiết diện hữu hiệu, võng kèo, chuyển vị đỉnh cột. **Cần đối chiếu công thức với bản TCVN 5575:2024** trước khi dùng cho hồ sơ chính thức. Chưa thiết kế liên kết bu lông, bản mã. |

Chương trình là công cụ hỗ trợ; kỹ sư chịu trách nhiệm kiểm tra kết quả, đặc biệt các hệ số tải trọng và điều kiện địa chất của từng công trình.

## 6. Kiểm chứng

`tests/KetCauVXT.Tests` (52 phép kiểm, chạy tự động trên CI mỗi lần đẩy mã):
1. Dầm ngàm/khớp với tải đều, tải tam giác, tải tập trung; cột công xôn (kể biến dạng trượt); cột thép vát – so với lời giải giải tích.
2. Khung sàn cứng; cân bằng phản lực toàn nhà theo từng trường hợp tải; tổng tải sàn dồn về dầm; đối xứng khung zamil.
3. Nhà 7 tầng 3×3 nhịp có gió + động đất (23 tổ hợp): cân bằng lực ngang, mọi tổ hợp hữu hạn.
4. Lưu → mở lại mô hình → phân tích lại cho kết quả trùng khớp tuyệt đối.
5. Mô hình rỗng và kết cấu biến hình phải báo lỗi rõ ràng, không làm sập chương trình.
6. Chạy trọn quy trình thiết kế → bản vẽ → thuyết minh → Excel → ETABS/SAP cho 3 công trình mẫu. Mọi file DXF được đọc lại và audit bằng thư viện độc lập `ezdxf`; Word/Excel được LibreOffice mở và xuất PDF; giao diện được mở thử và kiểm tra không bị treo.

## 7. Định dạng file dự án `.kcvxt`

JSON (UTF-8), đọc/ghi bằng `Project.Load / Project.Save`. Có thể sinh bằng công cụ khác (Python, Dynamo, Grasshopper) rồi mở bằng chương trình.

## 8. Hướng phát triển

Móng cọc và móng băng · Cốt thép dầm chữ T · P-Delta · Gió động · Liên kết thép · Đọc trực tiếp ETABS qua API (`ETABSv1.dll`) · Bản vẽ Revit qua add-in.
