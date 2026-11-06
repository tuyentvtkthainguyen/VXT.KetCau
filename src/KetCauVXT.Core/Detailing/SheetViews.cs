using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// KHUNG NHÌN – BẢN VẼ KIỂU REVIT cho chế độ Triển khai hồ sơ:
//  · Cỡ chữ in (DetailTextSizes): chữ chú thích, nhãn thép, tiêu đề hình, mặt cắt, kích thước, chữ trục, vòng số hiệu, đầu trục
//    + hệ số chung; áp cho MỌI hình vẽ triển khai (xem trước, dàn trang, xuất) qua DrawingStyle.Use.
//  · Trình duyệt dự án: khung nhìn (hình sinh từ cấu kiện) nhóm theo mặt bằng kết cấu; bảng thống kê; bản vẽ (sheet).
//  · Tạo bản vẽ → số hiệu nhảy tiếp; đặt / kéo khung nhìn vào tờ (mỗi khung nhìn chỉ trên MỘT tờ, bảng thống kê đặt nhiều tờ được);
//    tỷ lệ, cỡ chữ riêng từng khung nhìn; tiêu đề khung nhìn (tên + tỷ lệ) dưới hình như Revit.
//  · Bảng DANH MỤC BẢN VẼ sinh tự động theo danh sách tờ (khung nhìn đặt được lên tờ, cập nhật khi thêm / đổi tên / đánh số).
//  · Kích đúp khung nhìn → cấu kiện / mặt bằng tương ứng (ViewTarget).
// =====================================================================

/// <summary>Cỡ chữ in (mm giấy) cho bản vẽ triển khai. 0 = theo Thiết lập bản vẽ (chuẩn CAD đang áp dụng). Factor nhân thêm cho mọi cỡ.</summary>
public class DetailTextSizes
{
    /// <summary>Chữ ghi chú, tên cấu kiện, cao độ (mặc định chuẩn 2,5)</summary>
    public double Note { get; set; }
    /// <summary>Chữ nhãn thép (2,5)</summary>
    public double Tag { get; set; }
    /// <summary>Tên hình cấu kiện / mặt bằng (3,5)</summary>
    public double Title { get; set; }
    /// <summary>Tên mặt cắt (3,5)</summary>
    public double SectionTitle { get; set; }
    /// <summary>Chữ số kích thước (2,5)</summary>
    public double Dimension { get; set; }
    /// <summary>Tên trục (3,0)</summary>
    public double Axis { get; set; }
    /// <summary>Chữ bảng thống kê (2,5)</summary>
    public double Schedule { get; set; }
    /// <summary>Đường kính vòng tròn số hiệu thép (5,5)</summary>
    public double MarkDiameter { get; set; }
    /// <summary>Đường kính vòng tròn đầu trục (7,5)</summary>
    public double BubbleDiameter { get; set; }
    /// <summary>Hệ số nhân chung cho mọi cỡ chữ (1 = giữ nguyên)</summary>
    public double Factor { get; set; } = 1;

    /// <summary>Không đổi gì so với Thiết lập bản vẽ.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDefault => Note <= 0 && Tag <= 0 && Title <= 0 && SectionTitle <= 0 && Dimension <= 0 && Axis <= 0 && Schedule <= 0
                             && MarkDiameter <= 0 && BubbleDiameter <= 0 && (Factor <= 0 || Math.Abs(Factor - 1) < 1e-9);

    public DetailTextSizes Clone() => (DetailTextSizes)MemberwiseClone();

    /// <summary>Cỡ đang hiệu lực (mm giấy) của một vai trò chữ (đã nhân Factor).</summary>
    public double Effective(TextRole r)
    {
        var st = DrawingStyle.Current;
        double v = r switch
        {
            TextRole.Dimension => Dimension, TextRole.Tag => Tag, TextRole.MemberTitle => Title, TextRole.SectionTitle => SectionTitle,
            TextRole.Axis => Axis, TextRole.Schedule => Schedule, _ => Note
        };
        return (v > 0 ? v : st.Height(r)) * F(Factor);
    }

    private static double F(double f) => f > 0 && !double.IsNaN(f) ? f : 1;

    // ---- hệ số riêng của khung nhìn đang vẽ lại (đặt bởi SheetFigure.At(s, tf)) ----
    [ThreadStatic] private static double _ambient;
    /// <summary>Hệ số cỡ chữ riêng của khung nhìn đang vẽ (1 = không).</summary>
    public static double Ambient => _ambient > 0 ? _ambient : 1;

    /// <summary>Trong phạm vi using: hệ số khung nhìn tf nhân thêm vào cỡ chữ.</summary>
    public static IDisposable AmbientFactor(double tf)
    {
        double prev = _ambient;
        _ambient = tf > 0 ? tf : 1;
        return new Act(() => _ambient = prev);
    }

    /// <summary>Phạm vi áp cỡ chữ của hồ sơ (t có thể null) × hệ số khung nhìn hiện hành. Không đổi gì → phạm vi rỗng.</summary>
    public static IDisposable Scope(DetailTextSizes t, double extra = 1)
    {
        double k = Ambient * (extra > 0 ? extra : 1);
        if ((t == null || t.IsDefault) && Math.Abs(k - 1) < 1e-9) return new Act(null);
        var st = DrawingStyle.Current.Clone();
        t ??= new DetailTextSizes();
        double f = F(t.Factor) * k;
        void Set(TextRole r, double v) => st.SetHeight(r, (v > 0 ? v : st.Height(r)) * f);
        Set(TextRole.Note, t.Note);
        Set(TextRole.Tag, t.Tag);
        Set(TextRole.MemberTitle, t.Title);
        Set(TextRole.SectionTitle, t.SectionTitle);
        Set(TextRole.Dimension, t.Dimension);
        Set(TextRole.Axis, t.Axis);
        Set(TextRole.Schedule, t.Schedule);
        st.MarkDiameter = (t.MarkDiameter > 0 ? t.MarkDiameter : st.MarkDiameter) * f;
        st.AxisBubbleDiameter = (t.BubbleDiameter > 0 ? t.BubbleDiameter : st.AxisBubbleDiameter) * f;
        return DrawingStyle.Use(st);
    }

    private sealed class Act : IDisposable
    {
        private Action _a;
        public Act(Action a) { _a = a; }
        public void Dispose() { var a = _a; _a = null; a?.Invoke(); }
    }
}

/// <summary>Đối tượng mà kích đúp khung nhìn dẫn tới (Revit "Activate view").</summary>
public sealed class ViewTarget
{
    /// <summary>"member" = mở cấu kiện; "plan" = mở mặt bằng kết cấu; "schedule" = bảng; "index" = danh mục.</summary>
    public string Kind = "";
    /// <summary>Tên cấu kiện (Kind = member) hoặc tên mặt bằng (Kind = plan).</summary>
    public string Name = "";
    /// <summary>Loại cấu kiện (DetailResult.Kind) – phân biệt cấu kiện cùng tên khác loại.</summary>
    public string MemberKind = "";
    /// <summary>Tên lớp cấu kiện (ManualBeam…) lấy từ mã khung nhìn.</summary>
    public string TypeName = "";
}

/// <summary>Nhóm khung nhìn trong trình duyệt dự án.</summary>
public sealed class ViewGroup
{
    public string Title = "";
    /// <summary>"plan" (theo mặt bằng kết cấu), "unplaced" (chưa đặt trên mặt bằng), "schedule"</summary>
    public string Kind = "";
    public List<SheetFigure> Views = new();
}

/// <summary>Thao tác khung nhìn – bản vẽ kiểu Revit (lõi, kiểm thử được).</summary>
public static class SheetViews
{
    /// <summary>Mã khung nhìn bảng danh mục bản vẽ.</summary>
    public const string IndexViewId = "DMBV:0";
    public const string IndexCaption = "DANH MỤC BẢN VẼ";

    /// <summary>Khung nhìn bảng (thống kê / danh mục): đặt được trên nhiều tờ.</summary>
    public static bool IsSchedule(SheetFigure f) => f != null && (f.Part == 2 || f.Id == IndexViewId);
    public static bool IsSchedule(string id) => id == IndexViewId || (id ?? "").StartsWith("TKTHEP:") || (id ?? "").StartsWith("TKVL:");

    // ------------------------------------------------------------------ trình duyệt
    /// <summary>Danh mục hình + khung nhìn bảng danh mục bản vẽ (sinh theo danh sách tờ hiện tại).</summary>
    public static List<SheetFigure> WithIndexView(IList<SheetFigure> catalog, DetailSheetSet set)
    {
        var list = catalog.Where(f => f.Id != IndexViewId).ToList();
        list.Add(new SheetFigure { Id = IndexViewId, Section = int.MaxValue, Part = 2, Caption = IndexCaption, SectionTitle = "BẢNG THỐNG KÊ", F = new Figure(IndexTable(set, list.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First())), IndexCaption) });
        return list;
    }

    /// <summary>Nhóm khung nhìn cho trình duyệt: theo mục cây (mặt bằng + chi tiết thuộc mặt bằng), cấu kiện chưa đặt, bảng thống kê.
    /// Thứ tự giữ theo danh mục hình (thứ tự hồ sơ).</summary>
    public static List<ViewGroup> Groups(IList<SheetFigure> catalog)
    {
        var res = new List<ViewGroup>();
        var sched = new ViewGroup { Title = "Bảng thống kê", Kind = "schedule" };
        foreach (var f in catalog)
        {
            if (IsSchedule(f)) { sched.Views.Add(f); continue; }
            string t = string.IsNullOrWhiteSpace(f.SectionTitle) ? "Khác" : f.SectionTitle;
            var g = res.FirstOrDefault(x => x.Title == t && x.Kind != "schedule");
            if (g == null)
            {
                bool plan = catalog.Any(x => x.SectionTitle == f.SectionTitle && x.Part == 0);
                g = new ViewGroup { Title = t, Kind = plan ? "plan" : "unplaced" };
                res.Add(g);
            }
            g.Views.Add(f);
        }
        if (sched.Views.Count > 0) res.Add(sched);
        return res;
    }

    /// <summary>Tờ đang chứa khung nhìn id (null = chưa đặt). Bảng có thể nằm ở nhiều tờ: trả tờ đầu tiên.</summary>
    public static DetailSheet SheetOf(DetailSheetSet set, string id) =>
        set?.Sheets?.FirstOrDefault(s => s.Figures.Any(pf => pf.Id == id));

    /// <summary>Kích đúp khung nhìn → đối tượng cần mở.</summary>
    public static ViewTarget TargetOf(SheetFigure f)
    {
        if (f == null) return null;
        if (f.Id == IndexViewId) return new ViewTarget { Kind = "index", Name = IndexCaption };
        if (f.Part == 2) return new ViewTarget { Kind = "schedule", Name = f.Caption };
        if (f.Part == 0 && (f.Id.StartsWith("MB:") || f.Id.StartsWith("MBLT:")))
            return new ViewTarget { Kind = "plan", Name = f.Id.Substring(f.Id.IndexOf(':') + 1) };
        if (!string.IsNullOrEmpty(f.Member))
        {
            string tn = f.Id.Contains(':') ? f.Id.Substring(0, f.Id.IndexOf(':')) : "";
            return new ViewTarget { Kind = "member", Name = f.Member, MemberKind = f.Kind ?? "", TypeName = tn };
        }
        return new ViewTarget { Kind = "schedule", Name = f.Caption };
    }

    /// <summary>Cấu kiện của hồ sơ mà khung nhìn trỏ tới (theo loại lớp + tên).</summary>
    public static IDetailMember MemberOf(DetailingProject p, ViewTarget t)
    {
        if (p == null || t == null || t.Kind != "member") return null;
        var all = p.Groups.SelectMany(g => g.List.Cast<IDetailMember>()).ToList();
        return all.FirstOrDefault(m => m.Name == t.Name && (string.IsNullOrEmpty(t.TypeName) || m.GetType().Name == t.TypeName))
               ?? all.FirstOrDefault(m => m.Name == t.Name);
    }

    // ------------------------------------------------------------------ bản vẽ (sheet)
    /// <summary>Số hiệu cho tờ mới: số thứ tự lớn nhất đang dùng (cùng tiền tố) + 1; mẫu theo nhóm: nhóm của tờ cuối, thứ tự + 1.</summary>
    public static string NextNo(DetailSheetSet set)
    {
        int max = set.StartNo - 1, lastG = Math.Max(1, set.StartNo), lastI = 0;
        string pre = (set.NumberPrefix ?? "").Trim();
        foreach (var s in set.Sheets)
        {
            var (ok, g, i) = ParseNo(s.No, pre, set.NumberStyle);
            if (!ok) continue;
            if (set.NumberStyle == DetailSheetSet.StyleGroup) { if (g > lastG || g == lastG && i > lastI) { lastG = g; lastI = i; } }
            else max = Math.Max(max, i);
        }
        return set.NumberStyle == DetailSheetSet.StyleGroup ? set.FormatNo(0, lastG, lastI + 1) : set.FormatNo(max + 1, 0, 0);
    }

    /// <summary>Tách số hiệu: (đúng mẫu, nhóm, số thứ tự).</summary>
    public static (bool Ok, int Group, int Index) ParseNo(string no, string prefix, string style)
    {
        if (string.IsNullOrWhiteSpace(no)) return (false, 0, 0);
        string s = no.Trim();
        if (!string.IsNullOrEmpty(prefix))
        {
            if (!s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return (false, 0, 0);
            s = s.Substring(prefix.Length).TrimStart('-', '_', ' ');
        }
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (style == DetailSheetSet.StyleGroup && s.Contains('.'))
        {
            var parts = s.Split('.');
            if (parts.Length == 2 && int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, inv, out int g) && int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, inv, out int i)) return (true, g, i);
            return (false, 0, 0);
        }
        return int.TryParse(s, System.Globalization.NumberStyles.Integer, inv, out int n) ? (true, 0, n) : (false, 0, 0);
    }

    /// <summary>Tạo bản vẽ mới (cuối bộ hoặc tại vị trí at): số hiệu nhảy tiếp, tên người dùng đặt (giữ khi tự dàn trang), tờ khoá.</summary>
    public static DetailSheet CreateSheet(DetailSheetSet set, string title, string paper = null, bool portrait = false, int at = -1)
    {
        set.Sheets ??= new List<DetailSheet>();
        var sh = new DetailSheet
        {
            No = NextNo(set), Title = string.IsNullOrWhiteSpace(title) ? "BẢN VẼ MỚI" : title.Trim(),
            Paper = SheetGeometry.Papers.Contains(paper ?? "") ? paper : set.DefaultPaper, Portrait = portrait,
            Locked = true, NameLocked = !string.IsNullOrWhiteSpace(title)
        };
        if (at >= 0 && at < set.Sheets.Count) set.Sheets.Insert(at, sh); else set.Sheets.Add(sh);
        if (at >= 0 && at < set.Sheets.Count - 1) RenumberSequential(set);   // chèn giữa → đánh số lại cho liền mạch
        return sh;
    }

    /// <summary>Đánh số lại liên tục theo thứ tự tờ (bỏ qua tờ khoá số hiệu).</summary>
    public static void RenumberSequential(DetailSheetSet set) => SheetLayoutEngine.Renumber(set, null);

    /// <summary>Xoá tờ: các khung nhìn trên tờ trở về danh sách chưa đặt; renumber = đánh số lại cho liền mạch.</summary>
    public static bool DeleteSheet(DetailSheetSet set, DetailSheet sh, bool renumber = true)
    {
        if (set?.Sheets == null || sh == null || !set.Sheets.Remove(sh)) return false;
        if (renumber) RenumberSequential(set);
        return true;
    }

    /// <summary>Đổi tên bản vẽ (khoá tên – tự dàn trang không ghi đè).</summary>
    public static void Rename(DetailSheet sh, string title)
    {
        if (sh == null) return;
        sh.Title = (title ?? "").Trim();
        sh.NameLocked = true;
    }

    /// <summary>Đổi số hiệu bản vẽ (khoá số – đánh số lại không ghi đè).</summary>
    public static void Renumber(DetailSheet sh, string no)
    {
        if (sh == null || string.IsNullOrWhiteSpace(no)) return;
        sh.No = no.Trim();
        sh.NumberLocked = true;
    }

    /// <summary>ĐẶT khung nhìn lên tờ, tâm khung tại (cx, cy) mm giấy (null = chỗ trống đầu tiên). scale ≤ 0 = tự chọn theo khổ.
    /// Khung nhìn thường chỉ ở MỘT tờ (gỡ khỏi tờ cũ); bảng thống kê đặt được nhiều tờ (không trùng trên cùng tờ).
    /// Trả về khung đã đặt (null = không còn chỗ).</summary>
    public static PlacedFigure PlaceView(DetailSheetSet set, DetailSheet sh, SheetFigure f, IReadOnlyDictionary<string, SheetFigure> map,
        double? cx = null, double? cy = null, double scale = 0)
    {
        if (set == null || sh == null || f == null) return null;
        bool multi = IsSchedule(f);
        if (multi) sh.Figures.RemoveAll(pf => pf.Id == f.Id);
        else foreach (var s in set.Sheets) s.Figures.RemoveAll(pf => pf.Id == f.Id);
        double sc = scale > 0 ? scale : f.CanRescale ? (sh.Scale > 0 && f.ScaleClass <= SheetLayoutEngine.MainClass(sh, map) ? sh.Scale : SheetLayoutEngine.ChooseScale(set, f, 0, sh.Paper)) : 0;
        double w = f.WAt(sc), h = f.HAt(sc);
        double x, y;
        if (cx.HasValue && cy.HasValue) { x = cx.Value - w / 2; y = cy.Value - h / 2; }
        else
        {
            var spot = SheetLayoutEngine.FindSpot(sh, SheetLayoutEngine.PlacedRects(sh, map), w, h, set);
            if (spot == null) return null;
            (x, y) = spot.Value;
        }
        var (pw, ph) = sh.Size;
        x = Math.Round(Math.Clamp(x, 0, Math.Max(0, pw - w)), 1);
        y = Math.Round(Math.Clamp(y, 0, Math.Max(0, ph - h)), 1);
        var placed = new PlacedFigure { Id = f.Id, X = x, Y = y, Scale = sc };
        sh.Figures.Add(placed);
        sh.Locked = true;
        return placed;
    }

    /// <summary>Gỡ khung nhìn khỏi tờ (về danh sách chưa đặt).</summary>
    public static bool RemoveView(DetailSheet sh, string id) => sh != null && sh.Figures.RemoveAll(pf => pf.Id == id) > 0;

    /// <summary>Đổi tỷ lệ khung nhìn (vẽ lại hình) – giữ tâm khung.</summary>
    public static void SetViewportScale(DetailSheet sh, PlacedFigure pf, SheetFigure f, double scale)
    {
        if (sh == null || pf == null || f == null || !f.CanRescale || scale <= 0) return;
        double tf = pf.TextFactor;
        double cx = pf.X + f.WAt(pf.Scale, tf) / 2, cy = pf.Y + f.HAt(pf.Scale, tf) / 2;
        pf.Scale = scale;
        pf.X = Math.Round(cx - f.WAt(scale, tf) / 2, 1);
        pf.Y = Math.Round(cy - f.HAt(scale, tf) / 2, 1);
        sh.Locked = true;
    }

    /// <summary>Khung nhìn tại điểm (x, y) mm giấy trên tờ (ưu tiên khung đặt sau).</summary>
    public static PlacedFigure ViewportAt(DetailSheet sh, IReadOnlyDictionary<string, SheetFigure> map, double x, double y)
    {
        if (sh == null) return null;
        for (int i = sh.Figures.Count - 1; i >= 0; i--)
        {
            var pf = sh.Figures[i];
            if (!map.TryGetValue(pf.Id, out var f)) continue;
            double w = f.WAt(pf.Scale, pf.TextFactor), h = f.HAt(pf.Scale, pf.TextFactor);
            if (x >= pf.X && x <= pf.X + w && y >= pf.Y && y <= pf.Y + h) return pf;
        }
        return null;
    }

    /// <summary>Tiêu đề khung nhìn (Revit: tên + tỷ lệ dưới hình).</summary>
    public static string ViewportTitle(PlacedFigure pf, SheetFigure f)
    {
        string name = !string.IsNullOrWhiteSpace(pf?.ViewTitle) ? pf.ViewTitle : f?.Caption ?? "";
        double s = f == null ? 0 : f.ScaleAt(pf?.Scale ?? 0);
        return name + (s > 1.0001 ? "  –  TL 1/" + Math.Round(s) : "");
    }

    // ------------------------------------------------------------------ danh mục bản vẽ
    /// <summary>Các dòng danh mục: STT, số hiệu, tên, khổ, tỷ lệ, ngày.</summary>
    public static List<string[]> IndexRows(DetailSheetSet set, IReadOnlyDictionary<string, SheetFigure> map)
    {
        var rows = new List<string[]>();
        int i = 1;
        foreach (var s in set?.Sheets ?? new List<DetailSheet>())
            rows.Add(new[] { (i++).ToString(), s.No ?? "", s.Title ?? "", s.Paper + (s.Portrait ? " đứng" : ""), s.IsIndex ? "" : SheetLayoutEngine.ScaleText(s, map), set.Date ?? "" });
        return rows;
    }

    /// <summary>Bảng DANH MỤC BẢN VẼ (tỷ lệ 1/1, mm giấy, gốc góc trái trên = (0, 0), trục y lên).</summary>
    public static DxfWriter IndexTable(DetailSheetSet set, IReadOnlyDictionary<string, SheetFigure> map)
    {
        var w = new DxfWriter(1);
        double[] cw = { 12, 26, 130, 20, 34, 24 };
        string[] hd = { "STT", "SỐ HIỆU", "TÊN BẢN VẼ", "KHỔ", "TỶ LỆ", "NGÀY" };
        double rh = 7, th = Math.Max(1.5, DrawingStyle.Current.Height(TextRole.Schedule)), tw = cw.Sum();
        w.Text(Lay.Chu, tw / 2, -4, 1.4 * th, IndexCaption, 0, 1, 2);
        double y = -10;
        var rows = new List<string[]> { hd };
        rows.AddRange(IndexRows(set, map ?? new Dictionary<string, SheetFigure>()));
        w.Line(Lay.KhungTen, 0, y, tw, y);
        foreach (var row in rows)
        {
            double cx = 0;
            for (int c = 0; c < cw.Length; c++)
            {
                string t = c < row.Length ? row[c] : "";
                if (!string.IsNullOrEmpty(t)) w.Text(Lay.Chu, c == 2 ? cx + 2 : cx + cw[c] / 2, y - rh / 2, th, t, 0, c == 2 ? 0 : 1, 2);
                cx += cw[c];
            }
            y -= rh;
            w.Line(Lay.KhungTen, 0, y, tw, y);
        }
        double xx = 0;
        for (int c = 0; c <= cw.Length; c++) { w.Line(Lay.KhungTen, xx, -10, xx, y); if (c < cw.Length) xx += cw[c]; }
        return w;
    }

    /// <summary>Thống kê bộ bản vẽ: số tờ, số khung nhìn đã đặt / chưa đặt.</summary>
    public static (int Sheets, int Placed, int Unplaced) Stats(DetailSheetSet set, IList<SheetFigure> catalog)
    {
        var on = new HashSet<string>(set?.Sheets?.SelectMany(s => s.Figures.Select(pf => pf.Id)) ?? Enumerable.Empty<string>());
        int placed = catalog.Count(f => on.Contains(f.Id));
        return (set?.Sheets?.Count ?? 0, placed, catalog.Count(f => !IsSchedule(f)) - catalog.Count(f => !IsSchedule(f) && on.Contains(f.Id)));
    }
}
