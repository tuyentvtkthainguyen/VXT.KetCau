using System.Globalization;
using System.Text.Json.Serialization;
using KetCauVXT.Drawing;
using KetCauVXT.Import;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// KHUNG TÊN cho dàn trang in chế độ Triển khai hồ sơ:
//   • "Mặc định"  – khung tên dựng sẵn của chương trình (SheetComposer.Frame);
//   • "Tự tạo"    – người dùng chọn các ô (Công trình, Hạng mục, … Số hiệu, Tỷ lệ, Ngày), nhãn, độ rộng ô, chiều cao hàng,
//                   vị trí góc phải dưới hoặc dải dọc bên phải → dựng thành TitleBlockDef (mm giấy);
//   • "Tải lên"   – khung tên AutoCAD .dxf / .dwg (Import/TitleBlockImporter): hình học + ATTDEF/ATTRIB / chữ giữ chỗ {TEN_BAN_VE}…
//                   Tự nhận khung bao toàn tờ (hình chữ nhật ngoài cùng ≈ khổ giấy) hoặc chỉ ô khung tên (đặt góc phải dưới).
// Bản sao hình học đã chuẩn hoá lưu trong hồ sơ ⇒ mở lại không cần file gốc.
// =====================================================================

/// <summary>Một ô của khung tên tự tạo.</summary>
public class TitleCell
{
    /// <summary>Hàng (0 = hàng dưới cùng ở kiểu góc phải dưới; 0 = hàng trên cùng ở kiểu dải dọc).</summary>
    public int Row { get; set; }
    /// <summary>Khoá trường (TEN_CONG_TRINH, HANG_MUC, SO_HIEU… – xem TitleFields).</summary>
    public string Key { get; set; } = "";
    /// <summary>Nhãn ô (in chữ nhỏ ở góc trên trái ô).</summary>
    public string Label { get; set; } = "";
    /// <summary>Bề rộng ô (mm giấy).</summary>
    public double Width { get; set; } = 60;
    public bool Enabled { get; set; } = true;
    public bool ShowLabel { get; set; } = true;
    /// <summary>Chiều cao chữ giá trị (mm giấy).</summary>
    public double TextH { get; set; } = 2.5;
}

/// <summary>Khung tên của bộ tờ in.</summary>
public class DetailTitleBlock
{
    public const string KindDefault = "Mặc định", KindBuilder = "Tự tạo", KindImported = "Tải lên (AutoCAD)";
    public const string PosBottomRight = "Góc phải dưới", PosRightStrip = "Dải dọc bên phải";
    public static readonly string[] Kinds = { KindDefault, KindBuilder, KindImported };
    public static readonly string[] Positions = { PosBottomRight, PosRightStrip };
    /// <summary>Khoá trường tự đặt của khung tên tự tạo (ngoài TitleFields).</summary>
    public const string HangMuc = "HANG_MUC", GhiChu = "GHI_CHU";

    public string Kind { get; set; } = KindDefault;
    public string Position { get; set; } = PosBottomRight;
    /// <summary>Chiều cao các hàng (mm giấy), theo chỉ số hàng.</summary>
    public List<double> RowHeights { get; set; } = new();
    public List<TitleCell> Cells { get; set; } = new();
    /// <summary>Giá trị trường do người dùng nhập (Đơn vị thiết kế, Hạng mục, Chủ trì, Kiểm tra, Duyệt, Ghi chú…). Khoá đã chuẩn hoá.</summary>
    public Dictionary<string, string> Values { get; set; } = new();
    /// <summary>Khung tên tải lên (bản sao đã chuẩn hoá, mm giấy).</summary>
    public TitleBlockDef Imported { get; set; }
    /// <summary>Đường dẫn file gốc (tương đối so với thư mục hồ sơ nếu được) – chỉ để tham khảo / nạp lại.</summary>
    public string SourcePath { get; set; } = "";
    /// <summary>Khổ giấy nhận ra từ khung bao (vd. "A1", "A3 đứng"); "" = chỉ ô khung tên.</summary>
    public string DetectedPaper { get; set; } = "";

    /// <summary>Danh sách ô mặc định (khung tên 180 mm, 6 hàng).</summary>
    public static DetailTitleBlock DefaultBuilder()
    {
        var t = new DetailTitleBlock { Kind = KindBuilder, RowHeights = new List<double> { 8, 11, 8, 8, 8, 9 } };
        void C(int row, string key, string label, double w, double th = 2.5, bool on = true) =>
            t.Cells.Add(new TitleCell { Row = row, Key = key, Label = label, Width = w, TextH = th, Enabled = on });
        C(5, TitleFields.CongTy, "ĐƠN VỊ THIẾT KẾ", 180, 3.0);
        C(4, TitleFields.TenCongTrinh, "CÔNG TRÌNH", 180, 3.0);
        C(3, HangMuc, "HẠNG MỤC", 90); C(3, TitleFields.DiaDiem, "ĐỊA ĐIỂM", 90);
        C(2, TitleFields.ChuTri, "CHỦ TRÌ", 60); C(2, TitleFields.ThietKe, "THIẾT KẾ", 60); C(2, TitleFields.KiemTra, "KIỂM TRA", 60);
        C(1, TitleFields.TenBanVe, "TÊN BẢN VẼ", 180, 3.5);
        C(0, TitleFields.SoHieu, "SỐ HIỆU", 50, 3.5); C(0, TitleFields.TyLe, "TỶ LỆ", 50); C(0, TitleFields.Ngay, "NGÀY", 40); C(0, GhiChu, "GHI CHÚ", 40);
        C(3, TitleFields.ChuDauTu, "CHỦ ĐẦU TƯ", 60, 2.5, false);
        C(2, TitleFields.Duyet, "DUYỆT", 45, 2.5, false);
        C(0, TitleFields.LanSua, "LẦN SỬA", 25, 2.5, false);
        C(0, TitleFields.Trang, "TỜ", 25, 2.5, false);
        return t;
    }

    /// <summary>Các trường có thể chọn cho ô (khoá, tên).</summary>
    public static List<(string Key, string Label)> FieldChoices()
    {
        var l = TitleFields.All.Select(a => (a.Key, a.Label)).ToList();
        l.Insert(2, (HangMuc, "Hạng mục"));
        l.Add((GhiChu, "Ghi chú"));
        return l;
    }

    public double RowH(int row) => row >= 0 && row < RowHeights.Count && RowHeights[row] > 1 ? RowHeights[row] : 8;

    /// <summary>Khung tên tự tạo → TitleBlockDef (mm giấy). Góc phải dưới: gốc = góc phải dưới bảng; dải dọc: gốc = góc phải trên.</summary>
    public TitleBlockDef BuildDef()
    {
        var def = new TitleBlockDef { Name = "Khung tên tự tạo", Kind = TitleBlockKind.Vector, Mode = TitleBlockMode.TitleBox, DrawInnerFrame = true };
        var on = (Cells ?? new()).Where(c => c.Enabled && c.Width > 1).ToList();
        if (on.Count == 0) { def.UpdateExtents(); return def; }
        bool strip = Position == PosRightStrip;
        int rows = on.Max(c => c.Row) + 1;
        double width = Enumerable.Range(0, rows).Select(r => on.Where(c => c.Row == r).Sum(c => c.Width)).Max();
        double y = 0;
        for (int r = 0; r < rows; r++)
        {
            var cells = on.Where(c => c.Row == r).ToList();
            double h = RowH(r);
            double y0 = strip ? -(y + h) : y;   // đáy hàng
            double x = -cells.Sum(c => c.Width);
            if (strip) x = -width;              // dải dọc: các ô căn trái theo bề rộng dải
            foreach (var c in cells)
            {
                double cw = strip && ReferenceEquals(c, cells[^1]) ? -x : c.Width;   // dải dọc: ô cuối hàng kéo tới mép phải
                def.Entities.Add(new TitleBlockEntity { Type = "PLINE", Closed = true, Pts = new List<double> { x, y0, x + cw, y0, x + cw, y0 + h, x, y0 + h } });
                double vy = y0 + h / 2;
                if (c.ShowLabel && !string.IsNullOrWhiteSpace(c.Label))
                {
                    def.Entities.Add(new TitleBlockEntity { Type = "TEXT", Text = c.Label, H = 1.5, Ha = 0, Va = 3, Pts = new List<double> { x + 1, y0 + h - 0.8 } });
                    vy = y0 + Math.Max(0.4 * h, (h - 2.6) / 2);
                }
                def.Entities.Add(new TitleBlockEntity
                {
                    Type = "TEXT", Field = TitleFields.KeyOf(c.Key), Text = "{" + c.Key + "}", H = Math.Max(0.5, c.TextH), Ha = 0, Va = 2,
                    MaxW = Math.Max(2, cw - 3), Pts = new List<double> { x + 1.5, vy }
                });
                x += cw;
            }
            y += h;
        }
        // viền ngoài nét đậm
        double H = y;
        var outer = strip ? new List<double> { -width, -H, 0, -H, 0, 0, -width, 0 } : new List<double> { -width, 0, 0, 0, 0, H, -width, H };
        def.Entities.Add(new TitleBlockEntity { Type = "PLINE", Closed = true, Width = 0.5, Pts = outer });
        def.UpdateExtents();
        return def;
    }

    /// <summary>Định nghĩa khung tên đang dùng (null = khung tên mặc định của chương trình).</summary>
    [JsonIgnore]
    public TitleBlockDef ActiveDef => Kind switch
    {
        KindBuilder => BuildDef(),
        KindImported => Imported != null && (Imported.Entities?.Count > 0) ? Imported : null,
        _ => null
    };

    // ------------------------------------------------------------------
    // Tải lên DWG / DXF
    // ------------------------------------------------------------------
    /// <summary>Nạp khung tên AutoCAD (.dxf/.dwg; ảnh PNG/JPG cũng được). Tự nhận: khung ngoài cùng ≈ khổ giấy (có thể vẽ theo tỷ lệ 1/10…1/200)
    /// → khung bao toàn tờ (co vừa khổ từng tờ); bảng nhỏ → ô khung tên đặt ở góc phải dưới. projectDir: để lưu đường dẫn tương đối.
    /// Trả về thông báo. Ném InvalidDataException / FileNotFoundException khi không đọc được.</summary>
    public string Import(string path, string projectDir = null)
    {
        var res = TitleBlockImporter.FromFile(path, new TitleBlockImportOptions { Mode = TitleBlockMode.TitleBox, Base = TitleBlockBase.BottomRight, Block = "*" });
        var def = res.Def;
        string msg = res.Info;
        var (paper, portrait, s) = DetectPaper(def.W, def.H);
        if (paper != null)
        {
            def.Translate(-def.MinX, -def.MinY);
            def.Mode = TitleBlockMode.WholeFrame; def.FitToPaper = true; def.DrawInnerFrame = false;
            def.Scale = 1.0 / s;
            DetectedPaper = paper + (portrait ? " đứng" : "");
            msg += "; nhận khung bao toàn tờ khổ " + DetectedPaper + (s > 1 ? " (vẽ theo tỷ lệ 1/" + s + ")" : "");
        }
        else
        {
            DetectedPaper = "";
            def.Mode = TitleBlockMode.TitleBox; def.DrawInnerFrame = true;
            double sc = 1;
            if (def.W > 300 || def.H > 160)
                foreach (int k in new[] { 10, 20, 25, 50, 100, 200 })
                    if (def.W / k >= 80 && def.W / k <= 260 && def.H / k <= 140) { sc = 1.0 / k; break; }
            def.Scale = sc;
            msg += "; ô khung tên đặt góc phải dưới vùng vẽ" + (sc < 1 ? " (thu 1/" + Math.Round(1 / sc) + ")" : "");
        }
        Imported = def;
        Kind = KindImported;
        string full = Path.GetFullPath(path);
        SourcePath = full;
        if (!string.IsNullOrEmpty(projectDir))
            try { SourcePath = Path.GetRelativePath(projectDir, full); } catch (Exception) { SourcePath = full; }
        if (res.FieldCount == 0) msg += ". Chưa nhận ra trường nào – đặt chữ giữ chỗ {TEN_BAN_VE}, {SO_HIEU}, {TY_LE}, {NGAY}, {CONG_TRINH}… hoặc ATTDEF (TENBANVE, SOHIEU…).";
        foreach (var w in res.Warnings) msg += " | " + w;
        return msg;
    }

    /// <summary>Khung w × h (mm) có phải khổ giấy ISO (A0…A4, ngang/đứng), vẽ theo tỷ lệ 1/s (s ∈ 1, 10, 20, 25, 50, 100, 200)? Sai số 2 %.</summary>
    public static (string Paper, bool Portrait, int S) DetectPaper(double w, double h)
    {
        if (w < 1 || h < 1) return (null, false, 1);
        var papers = new[] { ("A0", 1189.0, 841.0), ("A1", 841.0, 594.0), ("A2", 594.0, 420.0), ("A3", 420.0, 297.0), ("A4", 297.0, 210.0) };
        foreach (int s in new[] { 1, 10, 20, 25, 50, 100, 200 })
            foreach (var (name, pw, ph) in papers)
            {
                bool land = Math.Abs(w / s - pw) <= 0.02 * pw && Math.Abs(h / s - ph) <= 0.02 * ph;
                bool port = Math.Abs(w / s - ph) <= 0.02 * ph && Math.Abs(h / s - pw) <= 0.02 * pw;
                if (land || port) return (name, port && !land, s);
            }
        return (null, false, 1);
    }

    // ------------------------------------------------------------------
    // Đặt lên tờ
    // ------------------------------------------------------------------
    /// <summary>Phép đặt khung tên lên tờ pw × ph: điểm cục bộ P → k·P + (dx, dy) (mm giấy).</summary>
    public static (double K, double Dx, double Dy) Placement(TitleBlockDef def, string position, double pw, double ph)
    {
        if (def == null) return (1, 0, 0);
        if (def.Mode == TitleBlockMode.WholeFrame)
        {
            double w = def.W, h = def.H;
            if (w > 1e-6 && h > 1e-6)
            {
                double k = Math.Min(pw / w, ph / h);
                return (k, -def.MinX * k + (pw - w * k) / 2, -def.MinY * k + (ph - h * k) / 2);
            }
            return (1, -def.MinX, -def.MinY);
        }
        double kk = def.Scale > 0 ? def.Scale : 1;
        double x2 = pw - SheetGeometry.MarginOther;
        return position == PosRightStrip ? (kk, x2, ph - SheetGeometry.MarginOther) : (kk, x2, SheetGeometry.MarginOther);
    }

    /// <summary>Vùng khung tên trên tờ (mm giấy, đã nới khe hở) – hình không được đặt vào.</summary>
    public Rect2 Zone(double pw, double ph)
    {
        var def = ActiveDef;
        double g = SheetGeometry.Gap;
        if (def == null)
            return new Rect2(pw - SheetGeometry.MarginOther - SheetGeometry.TitleW - g, 0, pw, SheetGeometry.MarginOther + SheetGeometry.TitleH + g);
        var (k, dx, dy) = Placement(def, Position, pw, ph);
        if (def.Mode == TitleBlockMode.WholeFrame)
        {
            // khung bao toàn tờ: ô khung tên ước theo phạm vi các chữ, kéo tới mép phải – dưới tờ
            var texts = (def.Entities ?? new()).Where(e => e.Type == "TEXT" && e.Pts?.Count >= 2).ToList();
            if (texts.Count > 0)
            {
                double x1 = texts.Min(e => e.Pts[0]) * k + dx, y2 = texts.Max(e => e.Pts[1] + e.H) * k + dy;
                if (pw - x1 <= 0.6 * pw) return new Rect2(x1 - 3 - g, 0, pw, Math.Min(ph, y2 + 3 + g));
            }
            return new Rect2(pw - SheetGeometry.MarginOther - SheetGeometry.TitleW - g, 0, pw, SheetGeometry.MarginOther + SheetGeometry.TitleH + g);
        }
        double zx1 = def.MinX * k + dx, zy1 = def.MinY * k + dy, zy2 = def.MaxY * k + dy;
        if (Kind == KindBuilder && Position == PosRightStrip) return new Rect2(zx1 - g, 0, pw, ph);
        return new Rect2(zx1 - g, Math.Min(0, zy1), pw, zy2 + g);
    }

    /// <summary>Vẽ khung tên (khung giấy, khung viền nếu cần, khung tên người dùng với trường đã điền) vào w (tỷ lệ w.Scale, mm giấy × S).</summary>
    public void Draw(DxfWriter w, double pw, double ph, IReadOnlyDictionary<string, string> values)
    {
        var def = ActiveDef;
        if (def == null) return;
        double S = w.Scale;
        string L = Lay.KhungTen;
        if (def.Mode != TitleBlockMode.WholeFrame)
        {
            w.Rect(L, 0, 0, pw * S, ph * S);
            if (def.DrawInnerFrame)
            {
                double x1 = SheetGeometry.MarginLeft * S, y1 = SheetGeometry.MarginOther * S, x2 = (pw - SheetGeometry.MarginOther) * S, y2 = (ph - SheetGeometry.MarginOther) * S;
                w.Polyline(L, new List<(double X, double Y)> { (x1, y1), (x2, y1), (x2, y2), (x1, y2) }, true, 0.5 * S);
            }
        }
        var (k, dx, dy) = Placement(def, Position, pw, ph);
        var fig = SheetBuilder.TitleDefFigure(def, values);
        w.AppendScaled(fig, k * S, dx * S, dy * S);
        if (Kind == KindBuilder && Position == PosRightStrip && def.W > 0)
        {
            // dải dọc: đường biên trái dải kéo hết chiều cao khung
            double xs = (pw - SheetGeometry.MarginOther - def.W * k) * S;
            w.Line(L, xs, SheetGeometry.MarginOther * S, xs, (ph - SheetGeometry.MarginOther) * S);
        }
    }

    /// <summary>Giá trị trường cho một tờ: thông tin hồ sơ, giá trị người dùng nhập, tên / số hiệu / tỷ lệ / ngày / thứ tự tờ.</summary>
    public Dictionary<string, string> ValuesFor(DetailingProject p, DetailSheetSet set, DetailSheet sh, string scaleText)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [TitleFields.TenCongTrinh] = p?.ProjectName ?? "", [TitleFields.ChuDauTu] = p?.Owner ?? "", [TitleFields.DiaDiem] = p?.Location ?? "",
            [TitleFields.ThietKe] = p?.Designer ?? ""
        };
        foreach (var kv in Values ?? new())
        {
            string k = TitleFields.KeyOf(kv.Key);
            if (k.Length > 0 && !string.IsNullOrWhiteSpace(kv.Value)) d[k] = kv.Value;
        }
        int idx = set?.Sheets?.IndexOf(sh) ?? -1;
        d[TitleFields.TenBanVe] = sh?.Title ?? "";
        d[TitleFields.SoHieu] = sh?.No ?? "";
        d[TitleFields.TyLe] = scaleText ?? "";
        d[TitleFields.Ngay] = set?.Date ?? "";
        d[TitleFields.Trang] = idx >= 0 ? (idx + 1).ToString(CultureInfo.InvariantCulture) : "";
        d[TitleFields.TongTrang] = set?.Sheets != null && idx >= 0 ? set.Sheets.Count.ToString(CultureInfo.InvariantCulture) : "";
        return d;
    }
}
