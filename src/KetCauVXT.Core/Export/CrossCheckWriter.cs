using System.Globalization;
using KetCauVXT.Report;

namespace KetCauVXT.Export;

/// <summary>Ghi báo cáo so sánh với ETABS/SAP2000/SAFE: Excel "SoSanh_ETABS.xlsx" (Tổng hợp, Thanh, Phản lực, Chuyển vị, Ghi chú – ô % tô màu
/// xanh ≤ ngưỡng cảnh báo, cam cảnh báo, đỏ vượt ngưỡng) và Word "BaoCaoSoSanh_ETABS.docx" (tóm tắt, 20 cấu kiện sai lệch lớn nhất, nguyên nhân thường gặp).</summary>
public static class CrossCheckWriter
{
    public const string ExcelName = "SoSanh_ETABS.xlsx";
    public const string WordName = "BaoCaoSoSanh_ETABS.docx";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Ghi cả hai file vào thư mục; trả về (excel, word).</summary>
    public static (string excel, string word) SaveAll(CrossCheckReport r, string dir)
    {
        Directory.CreateDirectory(dir);
        string x = Path.Combine(dir, ExcelName), w = Path.Combine(dir, WordName);
        SaveExcel(r, x);
        SaveWord(r, w);
        return (x, w);
    }

    private static XlStyle Fill(CrossLevel l) => l switch
    {
        CrossLevel.Ok => XlStyle.FillOk,
        CrossLevel.Warn => XlStyle.FillWarn,
        CrossLevel.Fail => XlStyle.FillFail,
        _ => XlStyle.Number2
    };

    private static object Pct(double v, CrossCheckOptions o) => double.IsNaN(v) ? "—" : (object)new XlCell(Math.Round(v, 2), Fill(o.Classify(v)));
    private static object Lv(CrossLevel l) => new XlCell(CrossCheckReport.LevelText(l), l == CrossLevel.Fail ? XlStyle.Fail : l == CrossLevel.Ok ? XlStyle.Ok : l == CrossLevel.Warn ? XlStyle.Bold : XlStyle.Default);
    private static string P2(double v) => double.IsNaN(v) ? "—" : v.ToString("0.00", Inv);
    private static string F(double v, string fmt = "0.00") => double.IsNaN(v) ? "—" : v.ToString(fmt, Inv);

    // ==================================================================
    // EXCEL
    // ==================================================================
    public static void SaveExcel(CrossCheckReport r, string path)
    {
        var o = r.Options;
        var x = new XlsxWriter();

        // ---- Tổng hợp ----
        var sh = x.AddSheet("Tổng hợp");
        sh.FirstRowIsHeader = false; sh.FreezeRows = 0;
        sh.SetColumnWidths(30, 12, 12, 12, 12, 12, 12, 14, 50);
        sh.AddRow(new XlCell("SO SÁNH KẾT QUẢ PHÂN TÍCH VIỆT CONST – " + r.Software, XlStyle.Title));
        sh.AddRow("Công trình", r.ProjectName);
        sh.AddRow("Phần mềm đối chiếu", r.Software);
        sh.AddRow("File kết quả", string.Join("; ", r.Files.Select(Path.GetFileName)));
        sh.AddRow("Đơn vị trong file", string.IsNullOrEmpty(r.Units) ? "kN, m" : r.Units + " → kN, m");
        sh.AddRow("Ngày lập", r.Created.ToString("dd/MM/yyyy HH:mm", Inv));
        sh.AddRow("Ngưỡng", $"≤ {o.WarnPercent:0.#} % phù hợp; {o.WarnPercent:0.#}–{o.FailPercent:0.#} % cảnh báo; > {o.FailPercent:0.#} % sai lệch lớn");
        sh.AddRow("Sàn giá trị nhỏ", $"mẫu số % ≥ max({o.FloorRatio * 100:0.#} % giá trị lớn nhất cùng loại của cấu kiện; {o.ForceFloor:0.##} kN; {o.MomentFloor:0.##} kN.m)");
        sh.AddRow("Đánh giá chung", Lv(r.Overall));
        sh.AddEmptyRow();
        sh.AddHeaderRow("Nhóm", "Số mục", "TB %", "Lớn nhất %", "Phù hợp", "Cảnh báo", "Sai lệch lớn", "Thiếu số liệu", "Lớn nhất tại");
        foreach (var s in r.Stats)
            sh.AddRow(s.Group, s.Count, Pct(s.Mean, o), Pct(s.Max, o), s.Ok, s.Warn, s.Fail, s.NoData, s.MaxAt);
        sh.AddEmptyRow();
        sh.AddRow(new XlCell($"{Math.Min(o.WorstCount, r.Members.Count)} CẤU KIỆN SAI LỆCH LỚN NHẤT", XlStyle.Bold));
        sh.AddHeaderRow("Cấu kiện", "Id", "Tầng", "Loại", "Sai lệch %", "Thành phần", "|M3| VXT", "|M3| ngoài", "Trường hợp");
        foreach (var m in r.Worst)
            sh.AddRow($"{m.Label} ({m.ExternalName})", m.FrameId, m.Story, m.Kind, Pct(m.WorstPercent, o), m.WorstComponent, m.OurM3, m.ExtM3, m.WorstCase);
        sh.AddEmptyRow();
        sh.AddRow(new XlCell("GHÉP TRƯỜNG HỢP / TỔ HỢP", XlStyle.Bold));
        sh.AddHeaderRow("Trường hợp ngoài", "So sánh", "Việt Const", "Cách ghép");
        foreach (var p in r.Pairs) sh.AddRow(p.External, p.Active ? "Có" : "Không", p.Ours, p.Method);

        // ---- Thanh ----
        var st = x.AddSheet("Thanh");
        var head = new List<object> { "Id", "Cấu kiện", "Tầng", "Loại", "Tên ngoài", "TH ngoài", "TH VXT" };
        foreach (var c in new[] { "M3", "M2", "V2", "V3", "T", "P" }) head.AddRange(new object[] { $"|{c}| VXT", $"|{c}| ngoài", $"Δ{c} %" });
        head.AddRange(new object[] { "Lớn nhất %", "Thành phần", "Đánh giá", "Số trạm" });
        st.AddRow(head.ToArray());
        st.SetColumnWidths(6, 12, 9, 8, 14, 16, 16, 10, 10, 8, 10, 10, 8, 10, 10, 8, 10, 10, 8, 10, 10, 8, 10, 10, 8, 10, 10, 12, 8);
        int[] order = { 5, 4, 1, 2, 3, 0 };
        foreach (var mc in r.MemberCases.OrderBy(m => m.Story).ThenBy(m => m.FrameId).ThenBy(m => m.ExternalCase))
        {
            var row = new List<object> { mc.FrameId, mc.Label, mc.Story, mc.Kind, mc.ExternalName, mc.ExternalCase, mc.OurCase };
            foreach (int k in order) row.AddRange(new object[] { mc.OurMax[k], mc.ExtMax[k], Pct(mc.Percent[k], o) });
            row.AddRange(new object[] { Pct(mc.WorstPercent, o), mc.WorstComponent, Lv(mc.Level), mc.Stations });
            st.AddRow(row.ToArray());
        }
        // thanh không có số liệu
        foreach (var m in r.Members.Where(m => m.CaseCount == 0))
            st.AddRow(m.FrameId, m.Label, m.Story, m.Kind, m.ExternalName, "—", "—", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
                "—", "", Lv(CrossLevel.NoData), 0);

        // ---- Phản lực ----
        var sr = x.AddSheet("Phản lực");
        var rh = new List<object> { "Nút", "X (m)", "Y (m)", "Z (m)", "TH ngoài", "TH VXT" };
        foreach (var c in CrossCheckSession.ReacComp) rh.AddRange(new object[] { c + " VXT", c + " ngoài", "Δ" + c + " %" });
        rh.AddRange(new object[] { "Lớn nhất %", "Đánh giá", "Ghi chú" });
        sr.AddRow(rh.ToArray());
        sr.SetColumnWidths(6, 8, 8, 8, 14, 14, 10, 10, 8, 10, 10, 8, 10, 10, 8, 10, 10, 8, 10, 10, 8, 10, 10, 8, 10, 12, 30);
        foreach (var re in r.Reactions.OrderBy(q => q.NodeId).ThenBy(q => q.ExternalCase))
        {
            var row = new List<object> { re.NodeId, re.X, re.Y, re.Z, re.ExternalCase, re.OurCase };
            for (int k = 0; k < 6; k++) row.AddRange(new object[] { re.Ours[k], re.Ext[k], re.Level == CrossLevel.NoData ? "—" : Pct(re.Percent[k], o) });
            row.AddRange(new object[] { Pct(re.WorstPercent, o), Lv(re.Level), re.Note });
            sr.AddRow(row.ToArray());
        }
        sr.AddEmptyRow();
        sr.AddRow(new XlCell("TỔNG PHẢN LỰC (BASE REACTIONS) – mô men quanh gốc toạ độ / điểm tham chiếu của bảng", XlStyle.Bold));
        var bh = new List<object> { "TH ngoài", "TH VXT", "Nguồn", "", "", "" };
        foreach (var c in CrossCheckSession.ReacComp) bh.AddRange(new object[] { c + " VXT", c + " ngoài", "Δ" + c + " %" });
        bh.AddRange(new object[] { "Lớn nhất (lực) %", "Đánh giá" });
        sr.AddHeaderRow(bh.ToArray());
        foreach (var b in r.BaseReactions)
        {
            var row = new List<object> { b.ExternalCase, b.OurCase, b.Source, null, null, null };
            for (int k = 0; k < 6; k++) row.AddRange(new object[] { b.Ours[k], b.Ext[k], Pct(b.Percent[k], o) });
            row.AddRange(new object[] { Pct(b.WorstPercent, o), Lv(b.Level) });
            sr.AddRow(row.ToArray());
        }

        // ---- Chuyển vị ----
        var sd = x.AddSheet("Chuyển vị");
        sd.SetColumnWidths(12, 12, 9, 16, 16, 7, 14, 14, 14, 10, 12);
        sd.AddRow("Đại lượng", "Tầng", "Cao độ (m)", "TH ngoài", "TH VXT", "Phương", "VXT", "Ngoài", "Sai lệch", "Sai lệch %", "Đánh giá");
        foreach (var s in r.Stories.OrderBy(q => q.Quantity).ThenByDescending(q => q.Elevation).ThenBy(q => q.ExternalCase).ThenBy(q => q.Direction))
        {
            bool disp = s.Quantity == "Chuyển vị";
            sd.AddRow(s.Quantity + (disp ? " (mm)" : ""), s.Story + (s.Top ? " (mái)" : ""), s.Elevation, s.ExternalCase, s.OurCase, s.Direction,
                new XlCell(disp ? s.Ours * 1000 : s.Ours, disp ? XlStyle.Number2 : XlStyle.Number3), new XlCell(disp ? s.Ext * 1000 : s.Ext, disp ? XlStyle.Number2 : XlStyle.Number3),
                new XlCell(disp ? s.Diff * 1000 : s.Diff, disp ? XlStyle.Number2 : XlStyle.Number3), Pct(s.Percent, o), Lv(s.Level));
        }
        if (r.Modal.Count > 0)
        {
            sd.AddEmptyRow();
            sd.AddRow(new XlCell("CHU KỲ DAO ĐỘNG RIÊNG (s)", XlStyle.Bold));
            sd.AddHeaderRow("Mode", "Ngoài", "VXT", "Sai lệch %", "Ghi chú");
            foreach (var m in r.Modal)
                sd.AddRow(m.Mode, new XlCell(m.ExtPeriod, XlStyle.Number3), double.IsNaN(m.OurPeriod) ? "—" : (object)new XlCell(m.OurPeriod, XlStyle.Number3),
                    double.IsNaN(m.Percent) ? "—" : (object)Math.Round(m.Percent, 2), m.Note);
        }

        // ---- Ghi chú ----
        var sn = x.AddSheet("Ghi chú");
        sn.FirstRowIsHeader = false; sn.FreezeRows = 0;
        sn.SetColumnWidths(140);
        sn.AddRow(new XlCell("GỢI Ý NGUYÊN NHÂN (theo kết quả so sánh)", XlStyle.Bold));
        foreach (var h in r.Hints) sn.AddRow("• " + h);
        sn.AddEmptyRow();
        sn.AddRow(new XlCell("NGUYÊN NHÂN SAI LỆCH THƯỜNG GẶP GIỮA VIỆT CONST VÀ ETABS/SAP2000", XlStyle.Bold));
        foreach (var g in CrossCheckReport.Guidance) sn.AddRow("• " + g);
        sn.AddEmptyRow();
        sn.AddRow(new XlCell($"THANH TRONG FILE NGOÀI KHÔNG KHỚP ({r.UnmatchedExternal.Count})", XlStyle.Bold));
        foreach (var m in r.UnmatchedExternal) sn.AddRow($"{m.External}" + (m.Story.Length > 0 ? " @" + m.Story : "") + " – " + m.Reason);
        sn.AddEmptyRow();
        sn.AddRow(new XlCell($"THANH CỦA DỰ ÁN KHÔNG CÓ SỐ LIỆU TRONG FILE NGOÀI ({r.FramesWithoutData.Count})", XlStyle.Bold));
        if (r.FramesWithoutData.Count > 0) sn.AddRow(string.Join(", ", r.FramesWithoutData.Take(400).Select(i => "#" + i.ToString(Inv))));
        sn.AddEmptyRow();
        sn.AddRow(new XlCell("THÔNG BÁO", XlStyle.Bold));
        foreach (var m in r.Messages) sn.AddRow(m);
        x.Save(path);
    }

    // ==================================================================
    // WORD
    // ==================================================================
    public static void SaveWord(CrossCheckReport r, string path)
    {
        var o = r.Options;
        var d = new DocxWriter();
        d.Title("BÁO CÁO SO SÁNH KẾT QUẢ PHÂN TÍCH", 16);
        d.Paragraph("Việt Const ↔ " + r.Software, true, false, ParaAlign.Center);
        d.Paragraph("Công trình: " + r.ProjectName, false, false, ParaAlign.Center);
        d.EmptyLine();

        d.Heading("1. Số liệu đối chiếu", 1);
        d.Bullet("Phần mềm đối chiếu: " + r.Software + "; file: " + string.Join("; ", r.Files.Select(Path.GetFileName)) + ".");
        d.Bullet("Đơn vị trong file: " + (string.IsNullOrEmpty(r.Units) ? "kN, m" : r.Units) + " – đã đổi về kN, m.");
        d.Bullet($"Trường hợp/tổ hợp so sánh: {r.Pairs.Count(p => p.Active)}/{r.Pairs.Count} ("
                 + string.Join(", ", r.Pairs.Where(p => p.Active).Take(12).Select(p => p.External == p.Ours ? p.External : p.External + " ↔ " + p.Ours))
                 + (r.Pairs.Count(p => p.Active) > 12 ? ", …" : "") + ").");
        d.Bullet($"Ngưỡng đánh giá: ≤ {o.WarnPercent:0.#} % phù hợp; {o.WarnPercent:0.#}–{o.FailPercent:0.#} % cảnh báo; > {o.FailPercent:0.#} % sai lệch lớn. "
                 + $"Sai lệch % = |VXT − ngoài| lớn nhất tại các trạm chung / max(|giá trị| lớn nhất của cấu kiện; {o.FloorRatio * 100:0.#} % giá trị lớn nhất cùng loại; {o.ForceFloor:0.##} kN hoặc {o.MomentFloor:0.##} kN.m).");
        d.Bullet($"Khớp thanh: {r.Members.Count(m => m.CaseCount > 0)}/{r.Members.Count} thanh của dự án có số liệu; {r.UnmatchedExternal.Count} thanh trong file ngoài không khớp.");

        d.Heading("2. Tổng hợp sai lệch", 1);
        d.Table(new[] { "Nhóm", "Số mục", "TB %", "Lớn nhất %", "Phù hợp", "Cảnh báo", "Sai lệch lớn", "Thiếu" },
            r.Stats.Select(s => new[] { s.Group, s.Count.ToString(Inv), P2(s.Mean), P2(s.Max), s.Ok.ToString(Inv), s.Warn.ToString(Inv), s.Fail.ToString(Inv), s.NoData.ToString(Inv) }),
            new[] { 4.0, 1.6, 1.6, 1.9, 1.7, 1.7, 2.1, 1.4 }, 10);
        d.Paragraph("Đánh giá chung: " + CrossCheckReport.LevelText(r.Overall).ToUpperInvariant() + ".", true);

        d.Heading($"3. {Math.Min(o.WorstCount, r.Members.Count(m => m.CaseCount > 0))} cấu kiện sai lệch lớn nhất", 1);
        var worst = r.Worst.ToList();
        if (worst.Count == 0) d.Paragraph("Không có cấu kiện nào được so sánh.");
        else
            d.Table(new[] { "Cấu kiện", "Tầng", "Loại", "Trường hợp", "TP", "Sai lệch %", "|M3| VXT", "|M3| ngoài", "Đánh giá" },
                worst.Select(m => new[] { $"{m.Label} #{m.FrameId}", m.Story, m.Kind, m.WorstCase, m.WorstComponent, P2(m.WorstPercent), F(m.OurM3), F(m.ExtM3), CrossCheckReport.LevelText(m.Level) }),
                new[] { 2.4, 1.4, 1.3, 2.6, 1.0, 1.7, 1.8, 1.8, 2.0 }, 9.5);

        d.Heading("4. Tổng phản lực chân công trình", 1);
        var bases = r.BaseReactions.Where(b => b.Level != CrossLevel.NoData).ToList();
        if (bases.Count == 0) d.Paragraph("Không có số liệu phản lực để so sánh.");
        else
            d.Table(new[] { "Trường hợp", "FX VXT", "FX ngoài", "FY VXT", "FY ngoài", "FZ VXT", "FZ ngoài", "Lớn nhất %" },
                bases.Select(b => new[] { b.ExternalCase, F(b.Ours[0], "0.0"), F(b.Ext[0], "0.0"), F(b.Ours[1], "0.0"), F(b.Ext[1], "0.0"), F(b.Ours[2], "0.0"), F(b.Ext[2], "0.0"), P2(b.WorstPercent) }),
                new[] { 3.0, 1.9, 1.9, 1.9, 1.9, 1.9, 1.9, 1.6 }, 9.5);

        d.Heading("5. Chuyển vị đỉnh và lệch tầng", 1);
        var top = r.Stories.Where(s => s.Top).OrderBy(s => s.Quantity).ThenBy(s => s.ExternalCase).ThenBy(s => s.Direction).ToList();
        if (top.Count == 0) d.Paragraph("File ngoài không có bảng Joint Displacements / Story Drifts.");
        else
            d.Table(new[] { "Đại lượng", "Tầng", "Trường hợp", "Phương", "VXT", "Ngoài", "Sai lệch %" },
                top.Select(s => new[] { s.Quantity + (s.Quantity == "Chuyển vị" ? " (mm)" : ""), s.Story, s.ExternalCase, s.Direction,
                    s.Quantity == "Chuyển vị" ? F(s.Ours * 1000) : F(s.Ours, "0.00000"), s.Quantity == "Chuyển vị" ? F(s.Ext * 1000) : F(s.Ext, "0.00000"), P2(s.Percent) }),
                new[] { 2.8, 1.8, 3.0, 1.4, 2.4, 2.4, 2.0 }, 9.5);
        if (r.Modal.Count > 0)
        {
            d.Paragraph("Chu kỳ dao động riêng:", true);
            d.Table(new[] { "Mode", "Ngoài (s)", "VXT (s)", "Ghi chú" },
                r.Modal.Take(12).Select(m => new[] { m.Mode.ToString(Inv), F(m.ExtPeriod, "0.000"), F(m.OurPeriod, "0.000"), m.Note }),
                new[] { 1.4, 2.2, 2.2, 10.5 }, 9.5);
        }

        d.Heading("6. Nhận xét và nguyên nhân có thể", 1);
        foreach (var h in r.Hints) d.Bullet(h);
        d.Paragraph("Các nguyên nhân sai lệch thường gặp giữa hai phần mềm:", true);
        foreach (var g in CrossCheckReport.Guidance) d.Bullet(g);
        if (r.UnmatchedExternal.Count > 0)
        {
            d.Paragraph($"Thanh trong file ngoài không khớp ({r.UnmatchedExternal.Count}):", true);
            foreach (var m in r.UnmatchedExternal.Take(30)) d.Bullet($"{m.External}" + (m.Story.Length > 0 ? " @" + m.Story : "") + " – " + m.Reason);
            if (r.UnmatchedExternal.Count > 30) d.Bullet($"… và {r.UnmatchedExternal.Count - 30} thanh khác (xem sheet Ghi chú của file Excel).");
        }
        d.Paragraph("Chi tiết từng cấu kiện, từng trường hợp: file Excel " + ExcelName + ".", false, true);
        d.Save(path);
    }
}
