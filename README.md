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

## 3a. Triển khai kết cấu từ bản vẽ kiến trúc sơ bộ

Menu **Tệp → Nhập bản vẽ kiến trúc (triển khai kết cấu)…**, hoặc nút **Từ bản vẽ KT** trên thanh công cụ:

1. **Chọn file** DXF (hoặc DWG nếu máy có [ODA File Converter](https://www.opendesign.com/guestfiles/oda_file_converter), miễn phí; không có thì trong AutoCAD dùng SAVEAS → DXF). Có thể chọn nhiều file. Một file cũng có thể chứa nhiều mặt bằng tầng đặt cạnh nhau.
2. **Nhận diện**: chương trình tách từng mặt bằng và đọc tên tầng ("MẶT BẰNG TẦNG 1/2/…/TUM/MÁI"; đọc được chữ Unicode, mã `\U+`, font TCVN3 .VnTime). Sau đó nó nhận ra:
   - trục và tên trục (kể cả ký hiệu trục là block có thuộc tính);
   - tường: 2 nét song song, chiều dày 80–450 mm, khe ≤ 1,6 m coi là cửa;
   - cột kiến trúc, cửa đi/cửa sổ (block);
   - phòng theo chữ ghi (P.NGỦ, WC, BẾP, BAN CÔNG, CẦU THANG, THÔNG TẦNG, KHO, GARA…);
   - cầu thang (lớp *THANG*), ban công nhô ra ngoài tường.
   
   Các tầng được dời về chung hệ toạ độ bằng cách khớp tên trục, nếu không khớp được thì khớp vị trí tường.
3. **Đề xuất kết cấu**, xem trước trên mặt bằng từng cao trình. Bấm vào dầm/cột/ô sàn để bỏ hoặc giữ. Chương trình đề xuất:
   - cột tại giao trục trong nhà, chạy liên tục tới tầng cao nhất còn đỡ sàn;
   - dầm chính trên trục, dầm biên, dầm công xôn ban công;
   - dầm phụ đỡ tường không nằm trên trục, dầm viền lỗ cầu thang/thông tầng, dầm chia ô sàn lớn hơn 4,5 m;
   - tiết diện sơ bộ;
   - tải trọng: tường (trừ cửa) trên dầm, hoàn thiện và hoạt tải theo công năng phòng (bảng sửa được), tường chắn mái, cầu thang truyền về dầm viền lỗ.
4. **Tạo mô hình**. Chương trình hỏi có chạy **Tự tăng tiết diện đến khi đạt** không. Nếu đồng ý, nó lặp: phân tích → thiết kế → tăng 50 mm cho dầm/cột không đạt, 10 mm cho sàn → … Mục này cũng có trong menu **Thiết kế**.

Để nhận diện tốt, bản vẽ nên:
- đặt tường trên lớp có chữ TUONG/WALL, trục trên lớp TRUC/GRID (hoặc kiểu nét CENTER), cầu thang trên lớp có chữ THANG, cửa trên lớp CUA/DOOR. Tên lớp sửa được trong mục *Thông số*;
- ghi tên phòng bằng chữ trong phòng;
- ghi tên hình "MẶT BẰNG TẦNG …" ngay dưới hoặc trên mỗi mặt bằng.

Giới hạn hiện tại: chưa hỗ trợ tường xiên/cong, tầng hầm, tầng lửng (nhập số tầng thủ công), bản vẽ PDF/ảnh. Tường tầng 1 coi như đặt trên giằng móng, chưa đưa vào khung.

## 3b. Sửa mô hình trên mặt bằng, biểu đồ nội lực, mô hình móng

**Công cụ trên tab Mặt bằng** (chọn tầng đang xem trước):

| Nút | Thao tác |
|---|---|
| Chọn | Bấm chọn cột, dầm, lỗ sàn hoặc ô sàn. Đối tượng chọn sáng màu vàng trên mặt bằng, 3D, biểu đồ và mô hình móng. Rê chuột sẽ sáng nhẹ đối tượng dưới con trỏ. |
| ＋ Ô sàn | Bấm vào ô kín bởi dầm → thêm ô sàn theo công năng đang chọn: phòng ở, vệ sinh, ban công, hành lang, kho, mái. |
| ＋ Ban công | Bấm vào dầm biên → dầm công xôn tại các cột (vươn theo ô "Vươn"), dầm biên đầu công xôn, ô sàn ban công (hoạt tải 2,0), lan can (ô "Lan can", kN/m). |
| ＋ Cầu thang | Bấm vào ô sàn → bỏ sàn, vẽ ký hiệu cầu thang. Tải bản thang 6,5 kN/m² và hoạt tải 3,0 kN/m² (nửa vế) truyền vào dầm viền. |
| ＋ Giếng trời | Bấm vào ô sàn → bỏ sàn (ô thông tầng), lan can 0,5 kN/m quanh lỗ. |
| Sàn vệ sinh | Bấm vào ô sàn → hoàn thiện 2,0 kN/m² (chống thấm, hạ cốt). Bấm lại để trả về phòng thường. |
| Xoá | Xoá ô sàn hoặc lỗ sàn (xoá lỗ sẽ đặt lại sàn). |
| Hoàn tác | Ctrl+Z, tối đa 30 bước. Phím Delete xoá đối tượng đang chọn, Esc về chế độ Chọn. |

**Tab Biểu đồ nội lực:** chọn đại lượng M3, M2, V2, V3, N, T và trường hợp/tổ hợp tải (BAO: vẽ cả max và min). Có 3 khung nhìn:
- *Mặt bằng tầng*;
- *Mặt đứng theo trục*: khung trong mặt phẳng trục, mô men vẽ phía thớ chịu kéo;
- *3D*.

**Tab Mô hình móng:** mặt bằng móng với kích thước L×B×H, lực dọc N, màu đạt/không đạt. Bấm vào một móng để xem mặt cắt (cổ móng, cao độ, thép đáy). Móng cũng hiện trong khung 3D.

**Tự kiểm tra giao diện:** `KetCauVXT.exe --selftest <file.kcvxt> <thư mục>` mở dự án, phân tích, thiết kế, chọn, sửa và chụp ảnh từng tab. CI chạy lệnh này mỗi lần build. Mở dự án trực tiếp: `KetCauVXT.exe <file.kcvxt>`.

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
| Tải trọng | TCVN 2737:2023 | γf theo trường hợp tải (người dùng sửa; mặc định TT 1,1, hoàn thiện 1,3, hoạt tải 1,3, gió 1,2). Gió tĩnh: W = W0·k(z)·c, k(z) dạng hàm mũ theo địa hình A/B/C. W0 chọn theo **vùng gió I–V (QCVN 02:2022 Bảng 5.1)** qua danh mục tỉnh/địa danh; bảng phân vùng chi tiết đến xã/phường có thể nhập từ CSV/XLSX – **cần đối chiếu với QCVN 02:2022 cho địa danh cụ thể**. Chưa tính thành phần động của gió (nhà ≤ 40 m thường không cần). |
| Động đất | TCVN 9386:2012 | Phương pháp tĩnh lực ngang tương đương, T1 = Ct·H^¾, phổ loại 1. Chưa xét độ lệch tâm ngẫu nhiên. |
| Phân tích | – | Tuyến tính bậc nhất (chưa P-Delta). Sàn tuyệt đối cứng trong mặt phẳng, không tham gia chịu uốn của khung; tải sàn dồn 45°. |
| Dầm | TCVN 5574:2018 | Tiết diện chữ nhật (chưa kể cánh T – thiên về an toàn); cắt với c = 2h0; võng dài hạn với độ cứng B = Eb/(1+φb,cr)·Ieff (kể nứt theo thép thực bố trí, φb,cr = 2,5), dầm liên tục f = 5/48·[Mg − 0,1(Mt+Mp)]·L²/B. |
| Cột | TCVN 5574:2018 | Cốt thép đối xứng, lệch tâm xiên bằng đường bao tải trọng (load contour), uốn dọc theo hệ số η. |
| Sàn | – | Ô chữ nhật, phương pháp Marcus theo điều kiện biên, bỏ qua giảm mô men xoắn. Võng dài hạn kể nứt + từ biến, hệ số liên kết cạnh. |
| Móng | TCVN 9362:2012, TCVN 5574:2018 | Móng đơn, móng băng trên nền thiên nhiên (R, áp lực đáy, chọc thủng, uốn, lún cộng lớp phân tố); móng cọc (TCVN 10304:2014, sức chịu tải theo SPT/chỉ tiêu cơ lý). Địa chất: hố khoan khảo sát hoặc nền mặc định “đất tốt”; chương trình gợi ý phương án móng. Xem Phụ lục tính toán (menu Trợ giúp). |
| Thép tiền chế | TCVN 5575 | Bền, ổn định trong/ngoài mặt phẳng (φ theo độ mảnh quy ước), độ mảnh [λ] = 180 − 60α, ổn định cục bộ bụng theo α = (σ−σ1)/σ (TCVN 5575:2012 7.6.2), bụng mảnh kể tiết diện hữu hiệu, võng kèo, chuyển vị đỉnh cột. **Cần đối chiếu công thức với bản TCVN 5575:2024** trước khi dùng cho hồ sơ chính thức. Chưa thiết kế liên kết bu lông, bản mã. |

Chương trình là công cụ hỗ trợ; kỹ sư chịu trách nhiệm kiểm tra kết quả, đặc biệt các hệ số tải trọng và điều kiện địa chất của từng công trình.

## 6. Kiểm chứng

`tests/KetCauVXT.Tests` (52 phép kiểm, chạy tự động trên CI mỗi lần đẩy mã):
1. Dầm ngàm/khớp với tải đều, tải tam giác, tải tập trung; cột công xôn (kể biến dạng trượt); cột thép vát – so với lời giải giải tích.
2. Khung sàn cứng; cân bằng phản lực toàn nhà theo từng trường hợp tải; tổng tải sàn dồn về dầm; đối xứng khung zamil.
3. Nhà 7 tầng 3×3 nhịp có gió + động đất (23 tổ hợp): cân bằng lực ngang, mọi tổ hợp hữu hạn.
3a. Bản vẽ kiến trúc mẫu (nhà phố 5×15 m, 3 tầng + tum, 4 mặt bằng trong 1 file, có block trục, block cửa, chữ TCVN3, cửa cuốn 3,8 m, ban công, cầu thang, WC): nhận diện tầng/trục/tường/phòng, đề xuất cột-dầm-sàn, cân bằng tải, tự tăng tiết diện tới khi mọi cấu kiện đạt.
4. Lưu → mở lại mô hình → phân tích lại cho kết quả trùng khớp tuyệt đối.
5. Mô hình rỗng và kết cấu biến hình phải báo lỗi rõ ràng, không làm sập chương trình.
6. Chạy trọn quy trình thiết kế → bản vẽ → thuyết minh → Excel → ETABS/SAP cho 3 công trình mẫu. Mọi file DXF được đọc lại và audit bằng thư viện độc lập `ezdxf`; Word/Excel được LibreOffice mở và xuất PDF; giao diện được mở thử và kiểm tra không bị treo.

## 7. Định dạng file dự án `.kcvxt`

JSON (UTF-8), đọc/ghi bằng `Project.Load / Project.Save`. Có thể sinh bằng công cụ khác (Python, Dynamo, Grasshopper) rồi mở bằng chương trình.

## 8. Hướng phát triển

Móng cọc và móng băng · Cốt thép dầm chữ T · P-Delta · Gió động · Liên kết thép · Đọc trực tiếp ETABS qua API (`ETABSv1.dll`) · Bản vẽ Revit qua add-in.
