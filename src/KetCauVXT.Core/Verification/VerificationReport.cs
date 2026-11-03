using System.Globalization;
using KetCauVXT.Model;
using KetCauVXT.Report;

namespace KetCauVXT.Verification;

/// <summary>"Báo cáo kiểm chứng phần mềm" (Word qua DocxWriter, Excel qua XlsxWriter) – tương tự tài liệu "Software Verification"
/// của các hãng phần mềm: mục đích &amp; phương pháp, phạm vi, môi trường, bảng kết quả theo nhóm, kết luận, giới hạn đã biết.</summary>
public static class VerificationReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v, string fmt = null) => BenchmarkCase.Fmt(v, fmt);
    private static string Pct(double v) => double.IsFinite(v) ? v.ToString(v < 0.001 ? "0.0000" : v < 0.1 ? "0.000" : "0.00", Inv) : "-";

    public static readonly string[] TableHeaders =
        { "STT", "Bài toán", "Nguồn lời giải tham chiếu", "Đại lượng", "Giá trị tham chiếu", "Kết cấu VXT", "Sai số %", "Ngưỡng", "Kết luận" };

    /// <summary>Giới hạn đã biết của chương trình (ghi trung thực trong báo cáo).</summary>
    public static List<string> KnownLimitations() => new()
    {
        "Động đất: " + Std.SeismicMethodNote + " Chương trình chưa có phân tích dao động riêng (modal) – chu kỳ T1 lấy theo công thức gần đúng T1 = Ct·H^(3/4); "
        + "số điều khoản ghi theo bản TCVN 9386:2012 (cùng cơ sở EN 1998-1), người dùng đối chiếu bản 2025.",
        "Phân tích tĩnh tuyến tính bậc nhất: không kể hiệu ứng P-delta, phi tuyến vật liệu, phân phối lại nội lực; chưa tính bề rộng vết nứt acrc.",
        "Ô sàn bị tường hoặc dầm xiên (chéo) cắt vẫn được tính như ô chữ nhật (dải bản / phương pháp Marcus theo hình bao chữ nhật) – người dùng tự kiểm tra các ô sàn hình dạng đặc biệt.",
        "Kết quả nội lực nhập từ ETABS/SAP2000/SAFE chỉ dùng trong phiên làm việc, KHÔNG được lưu vào file dự án – mở lại dự án phải nhập lại kết quả.",
        "Khối lượng, định mức trong bảng khối lượng là giá trị tham khảo (" + Std.Norms + ") – không thay thế dự toán được lập và thẩm tra theo quy định.",
        "Chịu cắt: chương trình kiểm tra tiết diện nghiêng với c = c0 = 2h0 (đơn giản hoá). Khi lực cắt gần như không đổi trên đoạn dài (lực tập trung xa gối), "
        + "kiểm tra đầy đủ theo TCVN 5574:2018 mục 8.1.3 với c trong khoảng h0…3h0 có thể cho Qu nhỏ hơn đến khoảng 0,25·Rbt·b·h0 (xem bài toán B04-2) – nên kiểm tra lại các dầm chịu lực tập trung lớn.",
        "Gió: chỉ tính thành phần tĩnh W = W0·k(z)·c (nhà thấp tầng); k(z) dùng dạng hàm mũ (tái tạo bảng k(z) của TCVN 2737:1995) – người dùng đối chiếu bảng của TCVN 2737:2023 (bài toán D01).",
        "Hệ số độ tin cậy γf và hệ số tổ hợp ψ = 0,9 là lựa chọn mặc định của chương trình (người dùng sửa theo tiêu chuẩn và loại tải); bài toán D03 kiểm chứng việc lập tổ hợp đúng với các hệ số đó, không kiểm chứng bản thân bảng hệ số.",
        "Thanh vát (khung tiền chế) được chia thành các đoạn lăng trụ; dầm cong được chia thành các đoạn thẳng – sai số giảm khi tăng số đoạn (bài toán A09, A11).",
        "Độ võng dùng độ cứng gần đúng (Bischoff, φb,cr = 2,5) – không phải phương pháp đầy đủ của TCVN 5574:2018 mục 8.2; bài toán B07, B08 kiểm chứng lập trình đúng công thức đã công bố.",
        "Lún: hệ số β = 0,8 cho mọi loại đất; ứng suất dưới tâm móng theo Boussinesq (nền đồng nhất đàn hồi).",
        "Giá trị tham chiếu là lời giải giải tích hoặc tính tay độc lập viết lại từ công thức tiêu chuẩn; việc so sánh với ví dụ của sách / tài liệu đã xuất bản CHƯA thực hiện – "
        + "cột \"Giá trị theo tài liệu\" để người dùng nhập. Báo cáo này không thay thế việc kỹ sư chịu trách nhiệm kiểm tra kết quả thiết kế."
    };

    // ==================================================================
    // WORD
    // ==================================================================
    public static void SaveDocx(VerificationResult r, string path)
    {
        var d = new DocxWriter { UpdateFieldsOnOpen = true, FooterText = "Báo cáo kiểm chứng phần mềm Kết cấu VXT", FooterSkipFirstPage = true };
        // ---- bìa ----
        d.EmptyLine().EmptyLine().EmptyLine();
        d.Title("BÁO CÁO KIỂM CHỨNG PHẦN MỀM", 22);
        d.Title("KẾT CẤU VXT", 18);
        d.Paragraph("Kiểm chứng độ tin cậy của lõi tính toán: phân tích kết cấu so với lời giải giải tích; thiết kế BTCT, nền móng, tải trọng so với tính tay độc lập theo TCVN",
            false, true, ParaAlign.Center);
        d.EmptyLine();
        d.Paragraph(r.Version, true, false, ParaAlign.Center);
        d.Paragraph("Ngày chạy kiểm chứng: " + r.Started.ToString("dd/MM/yyyy HH:mm", Inv), false, false, ParaAlign.Center);
        d.EmptyLine();
        d.Paragraph($"Kết quả: {r.ProblemCount} bài toán, {r.Count} đại lượng so sánh – {r.Passed} đạt, {r.Failed} không đạt.", true, false, ParaAlign.Center);
        d.Paragraph(r.AllPass ? "Mọi đại lượng nằm trong ngưỡng sai số cho phép." : "CÓ ĐẠI LƯỢNG VƯỢT NGƯỠNG – xem mục Kết luận.", true, false, ParaAlign.Center);
        d.EmptyLine().EmptyLine();
        d.Paragraph("Tài liệu được lập tự động bởi chức năng Trợ giúp → Kiểm chứng phần mềm. Giá trị tham chiếu là lời giải giải tích hoặc tính tay độc lập; "
                    + "so sánh với ví dụ của sách / tài liệu đã xuất bản chờ người dùng nhập.", false, true, ParaAlign.Center);
        d.PageBreak();
        d.TableOfContents("MỤC LỤC", 2);
        d.PageBreak();

        // ---- 1. mục đích, phương pháp ----
        d.Heading("1. Mục đích và phương pháp kiểm chứng", 1);
        d.Paragraph("Mục đích: chứng minh lõi tính của Kết cấu VXT cho kết quả đúng trên một tập bài toán chuẩn có lời giải đã biết, theo cách các hãng phần mềm kết cấu công bố tài liệu "
                    + "\"Software Verification\": mỗi bài toán được mô tả đầy đủ số liệu, giá trị tham chiếu kèm nguồn, giá trị chương trình, sai số và ngưỡng chấp nhận.");
        d.Paragraph("Phương pháp:");
        d.Bullet("Nhóm A – phân tích kết cấu: các mô hình nhỏ (dầm, khung, cột, thanh xoắn, dầm cong, dầm trên nền đàn hồi, thanh vát, khung nhiều tầng) được dựng bằng mã, "
                 + "phân tích bằng chính bộ giải của chương trình (FrameAnalysis – phần tử thanh Timoshenko 12 bậc tự do) và so với lời giải giải tích dạng kín của sức bền vật liệu / cơ học kết cấu. "
                 + "Vì phần tử kể biến dạng trượt, lời giải tham chiếu dùng lý thuyết dầm Timoshenko (số hạng uốn Euler–Bernoulli + số hạng trượt, diện tích chịu cắt 5/6·b·h); giá trị Euler–Bernoulli ghi ở cột ghi chú.");
        d.Bullet("Nhóm B, C, D – thiết kế theo tiêu chuẩn: giá trị tham chiếu tính bằng một bộ công thức tính tay ĐỘC LẬP viết lại trực tiếp từ công thức tiêu chuẩn (lớp HandCalc, không gọi mã thiết kế của chương trình); "
                 + "giá trị chương trình lấy từ đúng các hàm thiết kế mà chương trình dùng (RcSection, SlabDesigner, FootingDesigner, EccentricFooting, GeotechCalc, LateralLoadGenerator, ComboGenerator) với cùng số liệu đầu vào. "
                 + "Hằng số vật liệu của phép tính tay nhập trực tiếp theo bảng của tiêu chuẩn, không đọc từ thư viện vật liệu của chương trình.");
        d.Bullet("Sai số tương đối = |VXT − tham chiếu| / |tham chiếu| × 100%. Ngưỡng: 0,01–0,1% cho cân bằng / công thức đóng; 0,5–1% cho đại lượng có làm tròn bảng hoặc khác con đường tính; "
                 + "1–3% cho đại lượng phụ thuộc mức chia phần tử (dầm cong, thanh vát, nền Winkler) và mô hình tiết diện khác nhau (cột nén lệch tâm).");
        d.Bullet("Cột \"Giá trị theo tài liệu\": người dùng nhập giá trị của sách, ví dụ mẫu hoặc phần mềm khác (lưu tại %AppData%\\KetCauVXT\\" + VerificationUserData.FileName + ") – báo cáo tự tính sai số so với tài liệu.");

        // ---- 2. phạm vi ----
        d.Heading("2. Phạm vi kiểm chứng", 1);
        var gs = r.GroupSummary();
        d.Table(new[] { "Nhóm", "Số bài toán", "Số đại lượng", "Đạt", "Sai số lớn nhất %" },
            gs.Select(g => new[]
            {
                g.Group, r.Cases.Where(c => c.Group == g.Group).Select(c => VerificationResult.ProblemId(c.Id)).Distinct().Count().ToString(Inv),
                g.Count.ToString(Inv), g.Passed.ToString(Inv), Pct(g.MaxError)
            }), new[] { 8.5, 2.0, 2.0, 1.5, 2.5 }, 10);
        if (r.Skipped.Count > 0)
        {
            d.Paragraph("Bài toán chưa đưa vào (chương trình chưa có chức năng tương ứng):");
            foreach (var s in r.Skipped) d.Bullet(s);
        }

        // ---- 3. môi trường ----
        d.Heading("3. Môi trường và phiên bản", 1);
        d.Table(new[] { "Mục", "Giá trị" }, new[]
        {
            new[] { "Phần mềm", r.Version },
            new[] { "Môi trường chạy", r.RuntimeInfo },
            new[] { "Thời điểm chạy", r.Started.ToString("dd/MM/yyyy HH:mm:ss", Inv) },
            new[] { "Thời gian chạy", r.Elapsed.TotalSeconds.ToString("0.0", Inv) + " s" },
            new[] { "Tiêu chuẩn áp dụng (danh mục của chương trình)", string.Join("; ", new[] { Std.Concrete, Std.Loads, Std.Soil, Std.Piles, Std.Seismic }) },
            new[] { "Cách chạy lại", "Trợ giúp → Kiểm chứng phần mềm… → Chạy kiểm chứng; hoặc chương trình kiểm thử tests/KetCauVXT.Tests (VerificationTests)" }
        }, new[] { 4.5, 12.0 }, 10);

        // ---- 4. kết quả ----
        d.Heading("4. Kết quả kiểm chứng", 1);
        d.Paragraph($"Tổng hợp: {r.Summary}.", true);
        int gi = 1;
        foreach (var g in r.Groups())
        {
            var cs = r.Cases.Where(c => c.Group == g).ToList();
            d.Heading($"4.{gi++}. {g}", 2);
            int stt = 1;
            d.Table(TableHeaders, cs.Select(c => Row(c, stt++)), new[] { 0.8, 3.0, 3.3, 2.4, 1.6, 1.6, 1.2, 1.1, 1.5 }, 8.5);
            d.Paragraph("Mô tả bài toán và ghi chú:", true);
            foreach (var pg in cs.GroupBy(c => VerificationResult.ProblemId(c.Id)))
            {
                var first = pg.First();
                d.RichParagraph(ParaAlign.Justify, (pg.Key + " – " + first.Title + ". ", true, false), (first.Description ?? "", false, false));
                foreach (var c in pg.Where(c => !string.IsNullOrWhiteSpace(c.Notes) || c.Error != null))
                    d.Bullet($"{c.Id} ({c.Quantity}): {(c.Error != null ? "LỖI – " + c.Error : c.Notes)}");
            }
        }

        // ---- 5. so với tài liệu ----
        d.Heading("5. So sánh với tài liệu (sách, ví dụ mẫu)", 1);
        var book = r.Cases.Where(c => c.UserBookValue.HasValue).ToList();
        if (book.Count == 0)
            d.Paragraph("Chưa có giá trị theo tài liệu nào được nhập. Người dùng nhập tại Trợ giúp → Kiểm chứng phần mềm (cột \"Giá trị theo tài liệu\" và \"Nguồn tài liệu\"), "
                        + "sau đó xuất lại báo cáo. Các giá trị tham chiếu ở mục 4 là lời giải giải tích / tính tay độc lập, chưa phải giá trị trích từ sách.", false, true);
        else
            d.Table(new[] { "Mã", "Đại lượng", "Nguồn tài liệu", "Theo tài liệu", "Kết cấu VXT", "Sai số %" },
                book.Select(c => new[] { c.Id, c.Quantity + (string.IsNullOrEmpty(c.Unit) ? "" : " (" + c.Unit + ")"), c.UserBookSource ?? "", F(c.UserBookValue.Value), F(c.Computed), c.BookErrorPercent is double e ? Pct(e) : "-" }),
                new[] { 1.4, 4.0, 5.1, 2.0, 2.0, 2.0 }, 9);

        // ---- 6. kết luận ----
        d.Heading("6. Kết luận", 1);
        if (r.AllPass)
            d.Paragraph($"Toàn bộ {r.Count} đại lượng của {r.ProblemCount} bài toán chuẩn cho sai số nằm trong ngưỡng cho phép (sai số lớn nhất {Pct(r.MaxErrorPercent)}%). "
                        + "Lõi phân tích cho kết quả trùng lời giải giải tích (sai khác chỉ do làm tròn số học với các bài toán phần tử cho lời giải chính xác, và do mức chia phần tử với dầm cong, thanh vát, nền Winkler). "
                        + "Các hàm thiết kế BTCT, nền móng, tải trọng cho kết quả trùng phép tính tay độc lập theo công thức tiêu chuẩn với cùng giả thiết.");
        else
        {
            d.Paragraph($"{r.Failed} trên {r.Count} đại lượng KHÔNG đạt ngưỡng – cần xem xét trước khi sử dụng kết quả của các chức năng liên quan:", true);
            foreach (var c in r.Cases.Where(c => !c.Pass)) d.Bullet(c.ToString() + (c.Error != null ? " – " + c.Error : ""));
        }
        d.Paragraph("Kết luận trên chỉ áp dụng cho phạm vi bài toán đã kiểm chứng và các giả thiết nêu trong từng bài toán; xem mục Giới hạn đã biết.", false, true);

        // ---- 7. giới hạn ----
        d.Heading("7. Giới hạn đã biết", 1);
        foreach (var s in KnownLimitations()) d.Bullet(s);

        // ---- phụ lục ----
        d.Heading("Phụ lục A. Lời giải tham chiếu tự lập", 1);
        d.Paragraph("A06 – Khung cổng chân ngàm chịu lực ngang H (bỏ qua biến dạng dọc trục), phương pháp chuyển vị với hệ số độ cứng thanh Timoshenko φ = 12EI/(GAs·l²):", true);
        d.Equations(new[]
        {
            "a = (4 + φ)/(1 + φ)·EI/l ;  b = (2 − φ)/(1 + φ)·EI/l ;  s = 6EI/((1 + φ)·l²) ;  kb = 6EIb/((1 + φb)·L)",
            "Cân bằng nút đỉnh cột:  (ac + kb)·θ = sc·Δ",
            "Cân bằng lực cắt tầng:  2·(2sc·Δ − (ac + bc)·θ)/h = H",
            "→ Δ = H·h / [2sc·(2 − (ac + bc)/(ac + kb))] ;  M chân = sc·Δ − bc·θ ;  M đỉnh cột = M đầu dầm = kb·θ",
            "Kiểm tra: dầm cứng vô cùng → Δ = H·h³/(24EIc), M chân = M đỉnh = H·h/4 ; luôn có M chân + M đỉnh = H·h/2"
        });
        d.Paragraph("A03, A05 – Dầm một đầu ngàm – một đầu khớp kể biến dạng trượt (phương pháp lực):", true);
        d.Equation("R = (qL⁴/8EI + qL²/2GAs) / (L³/3EI + L/GAs) ;  M ngàm = qL²/2 − R·L ;  Mmax nhịp = R²/(2q)");
        d.Paragraph("A08 – Hằng số xoắn chính xác tiết diện chữ nhật a ≥ c (chuỗi Fourier):", true);
        d.Equation("J = a·c³/3·[1 − 192c/(π⁵a)·Σ(n = 1, 3, 5…) tanh(nπa/2c)/n⁵]");
        d.Paragraph("A10 – Dầm dài vô hạn trên nền Winkler (Hetényi):", true);
        d.Equation("λ = (k·B/4EI)^¼ ;  M0 = P/(4λ) ;  w0 = P·λ/(2k·B)");
        d.Paragraph("C01 – Hệ số A, B, D tra Bảng 14 TCVN 9362:2012 dùng làm tham chiếu (nhập tay):", true);
        var abd = new List<string[]>();
        foreach (int phi in new[] { 0, 10, 16, 20, 24, 30 })
            if (HandCalc.TableABD(phi, out double A, out double B, out double D))
                abd.Add(new[] { phi.ToString(Inv) + "°", A.ToString("0.00", Inv), B.ToString("0.00", Inv), D.ToString("0.00", Inv) });
        d.Table(new[] { "φII", "A", "B", "D" }, abd, new[] { 3.0, 3.0, 3.0, 3.0 }, 10);
        d.Paragraph("Hằng số vật liệu của phép tính tay: B25 Rb = 14,5 MPa, Rbt = 1,05 MPa, Eb = 30 000 MPa (TCVN 5574:2018 Bảng 7, 10); CB400-V Rs = Rsc = 350 MPa; "
                    + "CB300-V Rs = 260 MPa; CB240-T Rs = 210 MPa, Rsw = 170 MPa; Es = 200 000 MPa (Bảng 13); thép hình E = 210 000 MPa, ν = 0,3; bê tông ν = 0,2.");
        d.Save(path);
    }

    private static string[] Row(BenchmarkCase c, int stt)
    {
        string unit = string.IsNullOrEmpty(c.Unit) ? "" : " (" + c.Unit + ")";
        return new[]
        {
            stt.ToString(Inv), c.Id + " " + c.Title, c.ReferenceSource ?? "", c.Quantity + unit,
            F(c.Reference), F(c.Computed), Pct(c.ErrorPercent), "≤ " + c.Tolerance.ToString("0.###", Inv) + "%", c.Verdict
        };
    }

    // ==================================================================
    // EXCEL
    // ==================================================================
    public static void SaveXlsx(VerificationResult r, string path)
    {
        var x = new XlsxWriter();
        var sum = x.AddSheet("Tổng hợp");
        sum.FirstRowIsHeader = false; sum.FreezeRows = 0;
        sum.SetColumnWidths(48, 18, 14, 10, 18);
        sum.AddRow(new XlCell("BÁO CÁO KIỂM CHỨNG PHẦN MỀM KẾT CẤU VXT", XlStyle.Title));
        sum.AddRow(r.Version);
        sum.AddRow("Ngày chạy: " + r.Started.ToString("dd/MM/yyyy HH:mm", Inv));
        sum.AddRow("Môi trường: " + r.RuntimeInfo);
        sum.AddRow(r.Summary);
        sum.AddEmptyRow();
        sum.AddHeaderRow("Nhóm", "Số bài toán", "Số đại lượng", "Đạt", "Sai số lớn nhất %");
        foreach (var g in r.GroupSummary())
            sum.AddRow(g.Group, r.Cases.Where(c => c.Group == g.Group).Select(c => VerificationResult.ProblemId(c.Id)).Distinct().Count(), g.Count,
                new XlCell(g.Passed, g.Passed == g.Count ? XlStyle.Ok : XlStyle.Fail), new XlCell(g.MaxError, XlStyle.Number3));
        sum.AddBoldRow("Tổng", r.ProblemCount, r.Count, r.Passed, new XlCell(r.MaxErrorPercent, XlStyle.Number3));
        sum.AddEmptyRow();
        sum.AddRow("Giá trị tham chiếu: lời giải giải tích (nhóm A) và tính tay độc lập theo công thức tiêu chuẩn (nhóm B, C, D). Cột \"Giá trị theo tài liệu\" do người dùng nhập (chưa có = chờ nhập).");

        var sh = x.AddSheet("Kết quả");
        sh.SetColumnWidths(5, 8, 30, 36, 48, 30, 9, 14, 14, 10, 9, 12, 14, 30, 12, 50, 60);
        sh.AddRow("STT", "Mã", "Nhóm", "Bài toán", "Nguồn lời giải tham chiếu", "Đại lượng", "Đơn vị", "Giá trị tham chiếu", "Kết cấu VXT",
            "Sai số %", "Ngưỡng %", "Kết luận", "Giá trị theo tài liệu (sách/ví dụ)", "Nguồn tài liệu", "Sai số so với tài liệu %", "Mô tả bài toán", "Ghi chú");
        int stt = 1;
        foreach (var c in r.Cases)
        {
            int row = sh.RowCount + 1;   // số hàng Excel (1-based) của dòng sắp thêm
            object bookErr = null;
            if (c.UserBookValue.HasValue && c.BookErrorPercent is double be)
                bookErr = new XlFormula($"IF(M{row}=\"\",\"\",ABS(I{row}-M{row})/ABS(M{row})*100)", be, XlStyle.Number3);
            sh.AddRow(stt++, c.Id, c.Group, c.Title, c.ReferenceSource, c.Quantity, c.Unit,
                new XlCell(c.Reference, XlStyle.Default), new XlCell(c.Computed, XlStyle.Default),
                new XlCell(c.ErrorPercent, XlStyle.Number3), new XlCell(c.Tolerance, XlStyle.Default),
                new XlCell(c.Verdict, c.Pass ? XlStyle.Ok : XlStyle.Fail),
                c.UserBookValue.HasValue ? (object)new XlCell(c.UserBookValue.Value, XlStyle.Default) : null,
                c.UserBookSource ?? "", bookErr, c.Description, c.Error != null ? "LỖI: " + c.Error : c.Notes);
        }

        var lim = x.AddSheet("Giới hạn đã biết");
        lim.SetColumnWidths(5, 160);
        lim.AddRow("STT", "Giới hạn");
        int k = 1;
        foreach (var s in KnownLimitations()) lim.AddRow(k++, s);
        foreach (var s in r.Skipped) lim.AddRow(k++, "Chưa kiểm chứng: " + s);
        x.Save(path);
    }

    /// <summary>Xuất cả hai file vào thư mục: BaoCaoKiemChung.docx và BaoCaoKiemChung.xlsx; trả về hai đường dẫn.</summary>
    public static (string Docx, string Xlsx) SaveAll(VerificationResult r, string folder, string baseName = "BaoCaoKiemChung")
    {
        Directory.CreateDirectory(folder);
        string d = Path.Combine(folder, baseName + ".docx"), x = Path.Combine(folder, baseName + ".xlsx");
        SaveDocx(r, d);
        SaveXlsx(r, x);
        return (d, x);
    }
}
