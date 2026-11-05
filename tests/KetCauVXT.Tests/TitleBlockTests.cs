using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KetCauVXT.Detailing;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Tests;

/// <summary>Test 66 – Khung tên dàn trang in (Triển khai hồ sơ): khung tên tự tạo, tải lên khung tên AutoCAD DXF (khung bao toàn tờ /
/// chỉ ô khung tên, ATTDEF + chữ giữ chỗ {TEN_BAN_VE}…), điền trường từng tờ; tên – số hiệu tự động theo cây thư mục công trình,
/// mẫu số hiệu KC-01 / KC01 / KC-1.01, tên sửa tay giữ khi dàn lại, lưu / mở.</summary>
public static class TitleBlockTests
{
    private static double B(bool b) => b ? 1 : 0;
    /// <summary>Hình vừa khổ A0 (hình lớn hơn được đặt cưỡng bức, có cảnh báo riêng).</summary>
    private static bool FitsA0(Rect2 r) { var a = SheetGeometry.Drawable("A0"); return r.W <= a.W - SheetGeometry.TitleW - 20 || r.H <= a.H - SheetGeometry.TitleH - 20; }

    public static void Run(Action<string, double, double, double> check, string outDir)
    {
        string dir = Path.Combine(outDir, "TrienKhai", "KhungTen");
        Directory.CreateDirectory(dir);

        // ---- nhận khổ giấy ----
        var d1 = DetailTitleBlock.DetectPaper(841, 594);
        check("Nhận khổ: 841 × 594 → A1 ngang, 1/1", B(d1.Paper == "A1" && !d1.Portrait && d1.S == 1), 1, 0);
        var d2 = DetailTitleBlock.DetectPaper(84100, 59400);
        check("Nhận khổ: 84100 × 59400 → A1 vẽ theo tỷ lệ 1/100", B(d2.Paper == "A1" && d2.S == 100), 1, 0);
        var d3 = DetailTitleBlock.DetectPaper(297, 420);
        check("Nhận khổ: 297 × 420 → A3 đứng", B(d3.Paper == "A3" && d3.Portrait), 1, 0);
        check("Nhận khổ: bảng 180 × 40 → không phải khổ giấy", B(DetailTitleBlock.DetectPaper(180, 40).Paper == null), 1, 0);

        // ---- mẫu số hiệu ----
        var ns = new DetailSheetSet();
        check("Mẫu KC-01", B(ns.FormatNo(1, 1, 1) == "KC-01"), 1, 0);
        ns.NumberStyle = DetailSheetSet.StyleNoDash;
        check("Mẫu KC01", B(ns.FormatNo(7, 1, 1) == "KC07"), 1, 0);
        ns.NumberStyle = DetailSheetSet.StyleGroup;
        check("Mẫu KC-1.01 (nhóm 2, tờ 3 → KC-2.03)", B(ns.FormatNo(9, 2, 3) == "KC-2.03"), 1, 0);
        ns.NumberStyle = DetailSheetSet.StyleDash; ns.NumberPrefix = "KCM"; ns.Digits = 3;
        check("Tiền tố KCM, 3 chữ số → KCM-012", B(ns.FormatNo(12, 1, 1) == "KCM-012"), 1, 0);

        // ---- gộp tên cấu kiện ----
        check("Gộp tên D1, D2, D3, D5 → D1–D3, D5", B(SheetLayoutEngine.CompressNames(new[] { "D1", "D2", "D3", "D5" }) == "D1–D3, D5"), 1, 0);
        check("Gộp tên M1, M2 → M1, M2", B(SheetLayoutEngine.CompressNames(new[] { "M1", "M2" }) == "M1, M2"), 1, 0);

        // ---- khung tên tự tạo ----
        var tb = DetailTitleBlock.DefaultBuilder();
        var def = tb.BuildDef();
        check("Khung tên tự tạo mặc định: rộng 180 mm", def.W, 180, 1e-6);
        check("Khung tên tự tạo: cao = tổng chiều cao hàng", def.H, tb.RowHeights.Sum(), 1e-6);
        check("Khung tên tự tạo: gốc góc phải dưới (MaxX = 0, MinY = 0)", Math.Abs(def.MaxX) + Math.Abs(def.MinY), 0, 1e-9);
        var used = def.UsedFields();
        check("Khung tên tự tạo: có trường tên bản vẽ, số hiệu, tỷ lệ, hạng mục",
            B(used.Contains(TitleFields.TenBanVe)) + B(used.Contains(TitleFields.SoHieu)) + B(used.Contains(TitleFields.TyLe)) + B(used.Contains(DetailTitleBlock.HangMuc)), 4, 0);
        check("Khung tên tự tạo: ô tắt không vẽ (chủ đầu tư)", B(!used.Contains(TitleFields.ChuDauTu)), 1, 0);
        tb.Position = DetailTitleBlock.PosRightStrip;
        var defS = tb.BuildDef();
        check("Dải dọc bên phải: gốc góc phải trên (MaxY = 0)", Math.Abs(defS.MaxY) + Math.Abs(defS.MaxX), 0, 1e-9);
        var zS = tb.Zone(841, 594);
        check("Dải dọc: vùng khung tên cao hết tờ", zS.H, 594, 1e-9);
        tb.Position = DetailTitleBlock.PosBottomRight;

        // ---- dàn trang hồ sơ mẫu với khung tên tự tạo ----
        var p = DetailingProject.Sample();
        p.ProjectName = "Nhà ở gia đình";
        var res = p.RunAll();
        var cat = SheetLayoutEngine.Catalog(p, res);
        var map = cat.ToDictionary(f => f.Id);
        var set = new DetailSheetSet { TitleBlock = tb, Date = "11/2026" };
        tb.Values[DetailTitleBlock.HangMuc] = "Kết cấu phần thân";
        SheetLayoutEngine.AutoLayout(set, cat);
        int onTitle = 0;
        foreach (var s in set.Sheets)
        {
            var z = SheetLayoutEngine.TitleZone(set, s);
            onTitle += SheetLayoutEngine.PlacedRects(s, map).Count(r => FitsA0(r) && SheetGeometry.Overlap(r, z));
        }
        check("Khung tên tự tạo: hình không đè khung tên", onTitle, 0, 0);
        var det = set.Sheets.First(s => !s.IsIndex && s.Figures.Any(pf => map[pf.Id].Part == 1));
        string dx = DxfWriter.Decode(SheetLayoutEngine.Compose(p, set, det, map).ToDxfString());
        check("Khung tên tự tạo: điền tên bản vẽ, số hiệu, công trình, hạng mục, ngày",
            B(dx.Contains(det.Title)) + B(dx.Contains(det.No)) + B(dx.Contains("Nhà ở gia đình")) + B(dx.Contains("Kết cấu phần thân")) + B(dx.Contains("11/2026")), 5, 0);
        check("Khung tên tự tạo: nhãn ô HẠNG MỤC, TÊN BẢN VẼ", B(dx.Contains("HẠNG MỤC")) + B(dx.Contains("TÊN BẢN VẼ")), 2, 0);
        check("Khung tên tự tạo: điền tỷ lệ", B(dx.Contains("1/")), 1, 0);

        // ---- tên tự động theo cây thư mục ----
        check("Tên tự động: tờ chi tiết bắt đầu \"CHI TIẾT \"", B(det.Title.StartsWith("CHI TIẾT ")), 1, 0);
        var planSheet = set.Sheets.FirstOrDefault(s => !s.IsIndex && s.Figures.Any(pf => map[pf.Id].Part == 0 && !map[pf.Id].Caption.StartsWith("MẶT CẮT ")));
        if (planSheet != null)
        {
            var pc = planSheet.Figures.Select(pf => map[pf.Id]).First(f => f.Part == 0 && !f.Caption.StartsWith("MẶT CẮT ")).Caption.ToUpperInvariant();
            check("Tên tự động: tờ mặt bằng = tên mặt bằng", B(planSheet.Title.StartsWith(pc)), 1, 0);
        }
        var memberNames = det.Figures.Select(pf => map[pf.Id].Member).Distinct().ToList();
        check("Tên tự động: tờ chi tiết ghi tên cấu kiện trên tờ", B(memberNames.All(n => det.Title.Contains(n) || det.Title.Contains("–") || det.Title.EndsWith("…"))), 1, 0);
        check("Tờ danh mục: tên DANH MỤC BẢN VẼ", B(set.Sheets[0].Title == SheetLayoutEngine.IndexTitle), 1, 0);
        string idx = DxfWriter.Decode(SheetLayoutEngine.Compose(p, set, set.Sheets[0], map).ToDxfString());
        check("Danh mục bản vẽ cập nhật tên các tờ", set.Sheets.Count(s => idx.Contains(s.Title.Length > 20 ? s.Title.Substring(0, 20) : s.Title)), set.Sheets.Count, 0);

        // ---- đánh số theo nhóm ----
        set.NumberStyle = DetailSheetSet.StyleGroup;
        SheetLayoutEngine.Renumber(set, cat);
        check("Theo nhóm: tờ danh mục KC-0.01", B(set.Sheets[0].No == "KC-0.01"), 1, 0);
        check("Theo nhóm: tờ đầu nhóm thứ nhất KC-1.01", B(set.Sheets.Count > 1 && set.Sheets[1].No == "KC-1.01"), 1, 0);
        check("Theo nhóm: mọi số hiệu dạng KC-n.mm", set.Sheets.Count(s => Regex.IsMatch(s.No, @"^KC-\d+\.\d{2}$")), set.Sheets.Count, 0);
        int badGroup = 0;
        for (int i = 2; i < set.Sheets.Count; i++)
        {
            int sa = set.Sheets[i - 1].Figures.Min(pf => map[pf.Id].Section), sb = set.Sheets[i].Figures.Min(pf => map[pf.Id].Section);
            string ga = set.Sheets[i - 1].No.Split('.')[0], gb = set.Sheets[i].No.Split('.')[0];
            if ((sa == sb) != (ga == gb)) badGroup++;
        }
        check("Theo nhóm: cùng nhóm mặt bằng ⇔ cùng số nhóm", badGroup, 0, 0);
        set.NumberStyle = DetailSheetSet.StyleDash;
        SheetLayoutEngine.Renumber(set, cat);
        check("Liên tục: KC-01, KC-02…", set.Sheets.Select((s, i) => s.No == "KC-" + (i + 1).ToString("00") ? 0 : 1).Sum(), 0, 0);

        // ---- đổi tên tay: giữ khi dàn lại / đánh số lại ----
        var ren = set.Sheets[Math.Min(2, set.Sheets.Count - 1)];
        string firstId = ren.Figures[0].Id;
        ren.Title = "MẶT BẰNG SỬA TAY"; ren.NameLocked = true;
        ren.No = "KC-99"; ren.NumberLocked = true;
        SheetLayoutEngine.AutoLayout(set, cat);
        var after = set.Sheets.FirstOrDefault(s => s.Figures.Any(pf => pf.Id == firstId));
        check("Tên sửa tay giữ sau khi tự dàn lại", B(after != null && after.Title == "MẶT BẰNG SỬA TAY" && after.NameLocked), 1, 0);
        check("Số hiệu sửa tay giữ sau khi đánh số lại", B(after != null && after.No == "KC-99"), 1, 0);
        after.NameLocked = false; after.NumberLocked = false;
        SheetLayoutEngine.ApplyNames(set, cat); SheetLayoutEngine.Renumber(set, cat);
        check("Đặt lại tên tự động: bỏ tên sửa tay", B(after.Title != "MẶT BẰNG SỬA TAY" && after.No != "KC-99"), 1, 0);

        // ---- tải lên khung tên AutoCAD: khung bao toàn tờ A1 ----
        string fA1 = Path.Combine(dir, "khung_A1.dxf");
        File.WriteAllText(fA1, FrameDxf(1), new UTF8Encoding(false));
        var ti = new DetailTitleBlock();
        string msg = ti.Import(fA1, dir);
        Console.WriteLine("   · " + msg);
        check("Tải khung tên DXF: kiểu tải lên, khung bao toàn tờ A1", B(ti.Kind == DetailTitleBlock.KindImported) + B(ti.DetectedPaper == "A1") + B(ti.Imported?.Mode == TitleBlockMode.WholeFrame), 3, 0);
        check("Tải khung tên DXF: đường dẫn tương đối", B(ti.SourcePath == "khung_A1.dxf"), 1, 0);
        var fu = ti.Imported.UsedFields();
        check("Tải khung tên DXF: nhận {TEN_BAN_VE}, {SO_HIEU}, {TY_LE}, {CONG_TRINH}, ATTDEF NGAY",
            B(fu.Contains(TitleFields.TenBanVe)) + B(fu.Contains(TitleFields.SoHieu)) + B(fu.Contains(TitleFields.TyLe)) + B(fu.Contains(TitleFields.TenCongTrinh)) + B(fu.Contains(TitleFields.Ngay)), 5, 0);
        set.TitleBlock = ti;
        SheetLayoutEngine.AutoLayout(set, cat);
        var a1 = set.Sheets.First(s => !s.IsIndex);
        var wA = SheetLayoutEngine.Compose(p, set, a1, map);
        string tA = DxfWriter.Decode(wA.ToDxfString());
        check("Khung tên tải lên: điền tên bản vẽ, số hiệu, công trình, ngày, tỷ lệ",
            B(tA.Contains(a1.Title)) + B(tA.Contains(a1.No)) + B(tA.Contains("Nhà ở gia đình")) + B(tA.Contains("11/2026")) + B(tA.Contains("TL: 1/")), 5, 0);
        check("Khung tên tải lên: chữ giữ chỗ đã thay hết", B(!tA.Contains("{TEN_BAN_VE}") && !tA.Contains("{SO_HIEU}")), 1, 0);
        var (pw, ph) = a1.Size;
        check("Khung bao toàn tờ co vừa khổ tờ (rộng)", wA.Extents.W / wA.Scale, pw, 1e-3);
        var z1 = SheetLayoutEngine.TitleZone(set, a1);
        check("Khung bao toàn tờ: vùng khung tên theo các chữ ở góc phải dưới", B(z1.X1 > 0.5 * pw && z1.Y2 < 0.5 * ph), 1, 0);
        int over = set.Sheets.Sum(s => SheetLayoutEngine.PlacedRects(s, map).Count(r => FitsA0(r) && SheetGeometry.Overlap(r, SheetLayoutEngine.TitleZone(set, s))));
        check("Khung tên tải lên: hình không đè khung tên", over, 0, 0);
        CadFileWriters.Write(wA, Path.Combine(dir, "To_KhungTenTaiLen.dxf"), CadOutputFormat.DxfR12);

        // ---- khung vẽ theo tỷ lệ 1/100 ----
        string f100 = Path.Combine(dir, "khung_A1_tl100.dxf");
        File.WriteAllText(f100, FrameDxf(100), new UTF8Encoding(false));
        var t100 = new DetailTitleBlock();
        t100.Import(f100);
        check("Khung vẽ theo 1/100: nhận A1, khung bao toàn tờ", B(t100.DetectedPaper == "A1" && t100.Imported.Mode == TitleBlockMode.WholeFrame), 1, 0);

        // ---- chỉ ô khung tên ----
        string fBox = Path.Combine(dir, "o_khung_ten.dxf");
        File.WriteAllText(fBox, BoxDxf(), new UTF8Encoding(false));
        var tbx = new DetailTitleBlock();
        tbx.Import(fBox);
        check("Ô khung tên: đặt góc phải dưới (không phải khung bao)", B(tbx.Imported.Mode == TitleBlockMode.TitleBox && tbx.DetectedPaper == ""), 1, 0);
        var (kk, ddx, ddy) = DetailTitleBlock.Placement(tbx.Imported, tbx.Position, 841, 594);
        check("Ô khung tên: mép phải sát khung viền (841 − 10)", tbx.Imported.MaxX * kk + ddx, 831, 1e-6);
        check("Ô khung tên: mép dưới sát khung viền (10)", tbx.Imported.MinY * kk + ddy, 10, 1e-6);
        var zb = tbx.Zone(841, 594);
        check("Ô khung tên: vùng cấm = ô 180 × 40 + khe", zb.W, 180 + SheetGeometry.Gap + SheetGeometry.MarginOther, 1e-6);
        set.TitleBlock = tbx;
        var sh3 = new DetailSheet { Paper = "A3", Title = "TỜ THỬ", No = "KC-05" };
        set.Sheets.Add(sh3);
        string t3 = DxfWriter.Decode(SheetLayoutEngine.Compose(p, set, sh3, map).ToDxfString());
        check("Ô khung tên trên A3: điền tên tờ và số hiệu", B(t3.Contains("TỜ THỬ")) + B(t3.Contains("KC-05")), 2, 0);
        set.Sheets.Remove(sh3);

        // ---- lưu / mở ----
        set.TitleBlock = ti;
        p.Sheets = set;
        var p2 = DetailingProject.FromJson(p.ToJson());
        var t2 = p2.Sheets?.TitleBlock;
        check("Lưu / mở: khung tên tải lên (kiểu, khổ nhận ra, số thực thể, đường dẫn)",
            B(t2?.Kind == DetailTitleBlock.KindImported) + B(t2?.DetectedPaper == "A1") + B(t2?.Imported?.Entities.Count == ti.Imported.Entities.Count) + B(t2?.SourcePath == ti.SourcePath), 4, 0);
        check("Lưu / mở: mẫu số hiệu, tên sửa tay", B(p2.Sheets.NumberStyle == set.NumberStyle) + B(p2.Sheets.Sheets.Count(s => s.NameLocked) == set.Sheets.Count(s => s.NameLocked)), 2, 0);
        p.Sheets.TitleBlock = tb;
        var p3 = DetailingProject.FromJson(p.ToJson());
        check("Lưu / mở: khung tên tự tạo (số ô, giá trị hạng mục)", B(p3.Sheets.TitleBlock.Cells.Count == tb.Cells.Count) + B(p3.Sheets.TitleBlock.Values.TryGetValue(DetailTitleBlock.HangMuc, out var hm) && hm == "Kết cấu phần thân"), 2, 0);
        // không cần file gốc khi mở lại
        File.Delete(fA1);
        p2.Sheets.TitleBlock.Kind = DetailTitleBlock.KindImported;
        string t4 = DxfWriter.Decode(SheetLayoutEngine.Compose(p2, p2.Sheets, p2.Sheets.Sheets.First(s => !s.IsIndex), map).ToDxfString());
        check("Mở lại không cần file khung tên gốc: vẫn vẽ khung tên", B(t4.Contains("Nhà ở gia đình")), 1, 0);

        // ---- PDF với khung tên ----
        string pdf = Path.Combine(dir, "HoSo_KhungTen.pdf");
        SheetLayoutEngine.ExportPdf(p2, p2.Sheets, cat, pdf);
        check("PDF với khung tên tải lên: ghi được", B(File.Exists(pdf) && new FileInfo(pdf).Length > 1000), 1, 0);
    }

    private static void P(StringBuilder sb, int c, object v) => sb.Append(c).Append('\n').Append(Convert.ToString(v, CultureInfo.InvariantCulture)).Append('\n');

    /// <summary>Khung bản vẽ A1 (khung giấy + khung viền + ô khung tên góc phải dưới) theo tỷ lệ 1/s, chữ giữ chỗ + ATTDEF.</summary>
    public static string FrameDxf(double s)
    {
        var sb = new StringBuilder();
        void Q(int c, object v) => P(sb, c, v);
        void Rect(double x1, double y1, double x2, double y2)
        {
            Q(0, "LWPOLYLINE"); Q(8, "KHUNG"); Q(90, 4); Q(70, 1);
            Q(10, x1 * s); Q(20, y1 * s); Q(10, x2 * s); Q(20, y1 * s); Q(10, x2 * s); Q(20, y2 * s); Q(10, x1 * s); Q(20, y2 * s);
        }
        void T(double x, double y, string t) { Q(0, "TEXT"); Q(8, "CHU"); Q(10, x * s); Q(20, y * s); Q(30, 0); Q(40, 2.5 * s); Q(1, t); }
        Q(0, "SECTION"); Q(2, "HEADER"); Q(9, "$ACADVER"); Q(1, "AC1021"); Q(9, "$INSUNITS"); Q(70, 4); Q(0, "ENDSEC");
        Q(0, "SECTION"); Q(2, "ENTITIES");
        Rect(0, 0, 841, 594);
        Rect(25, 10, 831, 584);
        Rect(651, 10, 831, 55);
        T(655, 45, "{CONG_TRINH}");
        T(655, 35, "{TEN_BAN_VE}");
        T(655, 25, "SỐ HIỆU: {SO_HIEU}");
        T(655, 15, "TL: {TY_LE}");
        Q(0, "ATTDEF"); Q(8, "CHU"); Q(10, 760 * s); Q(20, 15 * s); Q(30, 0); Q(40, 2.5 * s); Q(1, "NGÀY"); Q(3, "Ngày"); Q(2, "NGAY"); Q(70, 0);
        Q(0, "ENDSEC"); Q(0, "EOF");
        return sb.ToString();
    }

    /// <summary>Chỉ ô khung tên 180 × 40 mm đặt ở toạ độ bất kỳ.</summary>
    public static string BoxDxf()
    {
        var sb = new StringBuilder();
        void Q(int c, object v) => P(sb, c, v);
        Q(0, "SECTION"); Q(2, "HEADER"); Q(9, "$ACADVER"); Q(1, "AC1021"); Q(9, "$INSUNITS"); Q(70, 4); Q(0, "ENDSEC");
        Q(0, "SECTION"); Q(2, "ENTITIES");
        Q(0, "LWPOLYLINE"); Q(8, "KHUNG"); Q(90, 4); Q(70, 1);
        Q(10, 1000); Q(20, 500); Q(10, 1180); Q(20, 500); Q(10, 1180); Q(20, 540); Q(10, 1000); Q(20, 540);
        Q(0, "LINE"); Q(8, "KHUNG"); Q(10, 1000); Q(20, 520); Q(30, 0); Q(11, 1180); Q(21, 520); Q(31, 0);
        Q(0, "TEXT"); Q(8, "CHU"); Q(10, 1005); Q(20, 528); Q(30, 0); Q(40, 3.0); Q(1, "{TEN_BAN_VE}");
        Q(0, "TEXT"); Q(8, "CHU"); Q(10, 1005); Q(20, 508); Q(30, 0); Q(40, 2.5); Q(1, "{SO_HIEU}");
        Q(0, "ENDSEC"); Q(0, "EOF");
        return sb.ToString();
    }
}
