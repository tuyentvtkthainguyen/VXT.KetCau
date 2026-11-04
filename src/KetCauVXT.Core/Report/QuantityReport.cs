using System.Globalization;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Một bảng của bảng khối lượng (dùng chung cho Excel, Word và cửa sổ xem trong chương trình).
/// Ô: string, double (số thực), int/long (số nguyên); hàng tổng có <see cref="TotalRows"/>.</summary>
public class QtyTable
{
    public string Title { get; set; } = "";
    public string[] Headers { get; set; } = Array.Empty<string>();
    public List<object[]> Rows { get; } = new();
    /// <summary>Chỉ số hàng in đậm (tổng cộng)</summary>
    public HashSet<int> TotalRows { get; } = new();
    /// <summary>Độ rộng cột Excel (ký tự); null = tự đặt</summary>
    public double[] Widths { get; set; }
    /// <summary>Độ rộng cột Word (cm); null = chia đều</summary>
    public double[] WordWidths { get; set; }

    public QtyTable Add(params object[] cells) { Rows.Add(cells); return this; }
    public QtyTable AddTotal(params object[] cells) { TotalRows.Add(Rows.Count); Rows.Add(cells); return this; }
}

public class QtySheet
{
    public string Name { get; set; } = "";
    public List<QtyTable> Tables { get; } = new();
}

/// <summary>Xuất bảng khối lượng vật tư: Excel "BangKhoiLuong.xlsx" (8 trang) và chương trong thuyết minh Word.</summary>
public static class QuantityReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public const string DefaultFileName = "BangKhoiLuong.xlsx";

    public static readonly string[] SheetNames =
        { "Tổng hợp", "Bê tông", "Ván khuôn", "Cốt thép", "Tường-Trát", "Hoàn thiện sàn", "Phân tích vật tư", "Định mức" };

    public static string Fmt(object v, string fmt = "0.00") => v switch
    {
        null => "",
        double d => double.IsFinite(d) ? d.ToString(fmt, Inv) : "-",
        float f => ((double)f).ToString(fmt, Inv),
        int i => i.ToString(Inv),
        long l => l.ToString(Inv),
        XlFormula x => x.Cached.ToString(fmt, Inv),
        _ => Convert.ToString(v, Inv) ?? ""
    };

    private static double R(double v, int d = 3) => Math.Round(v, d);

    // ==================================================================
    // DỰNG BẢNG
    // ==================================================================
    public static List<QtySheet> BuildSheets(Project p, QuantityResult q)
    {
        var qs = q.Settings ?? new QuantitySettings();
        bool prices = qs.HasPrices;
        var sheets = SheetNames.Select(n => new QtySheet { Name = n }).ToList();

        // ---------------- TỔNG HỢP ----------------
        var sum = sheets[0];
        sum.Tables.Add(MainSummary(q));
        sum.Tables.Add(ConcreteByStory(q));
        sum.Tables.Add(MaterialTotals(q, prices));
        var notes = new QtyTable { Title = "Quy ước tính khối lượng", Headers = new[] { "STT", "Nội dung" }, Widths = new double[] { 6, 140 }, WordWidths = new[] { 1.0, 15.5 } };
        int k = 1;
        foreach (var n in q.Notes) notes.Add(k++, n);
        foreach (var w in q.Warnings) notes.Add(k++, "Lưu ý: " + w);
        sum.Tables.Add(notes);

        // ---------------- BÊ TÔNG ----------------
        var bt = new QtyTable
        {
            Title = "Khối lượng bê tông theo cấu kiện", Headers = new[] { "STT", "Loại cấu kiện", "Tầng", "Cấu kiện", "Mác BT", "SL", "Kích thước / diễn giải (m)", "Thể tích (m³)" },
            Widths = new double[] { 6, 16, 10, 16, 8, 6, 60, 13 }
        };
        k = 1;
        foreach (var g in q.Concrete.GroupBy(c => c.Category).OrderBy(g => QtyCat.Rank(g.Key)))
        {
            foreach (var c in g) bt.Add(k++, c.Category, c.Story, c.Member, c.Grade, c.Count, c.Dims + (string.IsNullOrEmpty(c.Note) ? "" : " – " + c.Note), R(c.Volume));
            bt.AddTotal(null, "Cộng " + g.Key.ToLowerInvariant(), null, null, null, null, null, R(g.Sum(c => c.Volume)));
        }
        bt.AddTotal(null, "TỔNG BÊ TÔNG KẾT CẤU", null, null, null, null, null, R(q.ConcreteTotal));
        sheets[1].Tables.Add(bt);
        if (q.Lean.Count > 0)
        {
            var lt = new QtyTable { Title = "Bê tông lót", Headers = new[] { "STT", "Cấu kiện", "Mác BT", "SL", "Kích thước (m)", "Thể tích (m³)" }, Widths = new double[] { 6, 24, 8, 6, 40, 13 } };
            k = 1;
            foreach (var c in q.Lean) lt.Add(k++, c.Member, c.Grade, c.Count, c.Dims, R(c.Volume));
            lt.AddTotal(null, "TỔNG BÊ TÔNG LÓT", null, null, null, R(q.LeanTotal));
            sheets[1].Tables.Add(lt);
        }
        if (q.Piles != null)
        {
            var pt = new QtyTable { Title = "Cọc", Headers = new[] { "Tiết diện (mm)", "Mác BT", "Số cọc", "Chiều dài 1 cọc (m)", "Tổng chiều dài (m)", "Thể tích (m³)" } };
            pt.Add(q.Piles.Size, q.Piles.Grade, q.Piles.Count, R(q.Piles.LengthEach, 2), R(q.Piles.TotalLength, 2), R(q.Piles.Volume));
            sheets[1].Tables.Add(pt);
        }
        if (q.GroundWorks.Count > 0) sheets[1].Tables.Add(CuTramQuantity.Table(q));   // [CừTràm] Report/CuTramQuantity.cs

        // ---------------- VÁN KHUÔN ----------------
        var vk = new QtyTable
        {
            Title = "Diện tích ván khuôn", Headers = new[] { "STT", "Loại cấu kiện", "Tầng", "Cấu kiện", "Diễn giải (m)", "Diện tích (m²)" },
            Widths = new double[] { 6, 16, 10, 16, 60, 13 }
        };
        k = 1;
        foreach (var g in q.Concrete.Where(c => c.Formwork > 1e-9).GroupBy(c => c.Category).OrderBy(g => QtyCat.Rank(g.Key)))
        {
            foreach (var c in g) vk.Add(k++, c.Category, c.Story, c.Member, c.FormworkNote, R(c.Formwork));
            vk.AddTotal(null, "Cộng " + g.Key.ToLowerInvariant(), null, null, null, R(g.Sum(c => c.Formwork)));
        }
        vk.AddTotal(null, "TỔNG VÁN KHUÔN", null, null, null, R(q.FormworkTotal));
        sheets[2].Tables.Add(vk);

        // ---------------- CỐT THÉP ----------------
        sheets[3].Tables.Add(RebarByCategory(q));
        var dias = q.RebarByDia.Select(d => d.Dia).ToList();
        if (dias.Count > 0)
        {
            var hd = new List<string> { "Loại cấu kiện" };
            hd.AddRange(dias.Select(d => "Ø" + d.ToString(Inv) + " (kg)"));
            hd.Add("Tổng (kg)");
            var bd = new QtyTable { Title = "Cốt thép theo đường kính", Headers = hd.ToArray() };
            foreach (var g in q.RebarRows.GroupBy(r => r.Category))
            {
                var row = new List<object> { g.Key };
                row.AddRange(dias.Select(d => (object)R(g.Where(r => r.Dia == d).Sum(r => r.WeightKg), 2)));
                row.Add(R(g.Sum(r => r.WeightKg), 2));
                bd.Add(row.ToArray());
            }
            var tot = new List<object> { "TỔNG" };
            tot.AddRange(q.RebarByDia.Select(d => (object)R(d.WeightKg, 2)));
            tot.Add(R(q.RebarTotal, 2));
            bd.AddTotal(tot.ToArray());
            var len = new List<object> { "Chiều dài (m)" };
            len.AddRange(q.RebarByDia.Select(d => (object)R(d.LengthM, 2)));
            len.Add(null);
            bd.Add(len.ToArray());
            sheets[3].Tables.Add(bd);
            var det = new QtyTable
            {
                Title = "Bảng thống kê cốt thép (chi tiết)", Headers = new[] { "Loại", "Cấu kiện", "Tầng", "Số hiệu", "Hình dạng", "Ø (mm)", "Dài 1 thanh (mm)", "SL cấu kiện", "Số thanh 1 CK", "Tổng số thanh", "Tổng dài (m)", "Trọng lượng (kg)" },
                Widths = new double[] { 10, 14, 9, 8, 24, 7, 12, 9, 9, 9, 11, 13 }
            };
            foreach (var r in q.RebarRows)
                det.Add(r.Category, r.MemberLabel, r.Story, r.Mark, r.ShapeText, r.Dia, r.LengthMm, r.MemberCount, r.BarsPerMember, r.BarsTotal, R(r.TotalLengthM, 2), R(r.WeightKg, 2));
            det.AddTotal("TỔNG", null, null, null, null, null, null, null, null, null, null, R(q.RebarTotal, 2));
            sheets[3].Tables.Add(det);
        }

        // ---------------- TƯỜNG – TRÁT ----------------
        sheets[4].Tables.Add(MasonrySummary(q, qs));
        var wd = new QtyTable
        {
            Title = "Khối lượng xây, trát từng đoạn tường",
            Headers = new[] { "Tường", "Tầng", "Loại tường", "Dày (m)", "Dài trục (m)", "Dài trừ cột (m)", "Cao (m)", "S tường (m²)", "S cửa (m²)", "S lanh tô (m²)", "S xây (m²)", "V xây (m³)", "Số mặt trát", "S trát (m²)", "Gạch (viên)", "Vữa xây (m³)", "Vữa trát (m³)" },
            Widths = new double[] { 7, 9, 24, 7, 9, 9, 7, 9, 9, 9, 9, 9, 7, 9, 10, 10, 10 }
        };
        foreach (var m in q.Masonry)
            wd.Add("#" + m.WallId.ToString(Inv), m.Story, m.WallType, R(m.Thickness, 3), R(m.Length, 2), R(m.NetLength, 2), R(m.Height, 2), R(m.GrossArea, 2), R(m.OpeningArea, 2), R(m.LintelArea, 2), R(m.NetArea, 2), R(m.Volume), m.PlasterSides, R(m.PlasterArea, 2), (long)Math.Round(m.Bricks), R(m.Mortar), R(m.PlasterMortar));
        if (q.Masonry.Count > 0)
            wd.AddTotal("TỔNG", null, null, null, null, null, null, R(q.Masonry.Sum(m => m.GrossArea), 2), R(q.Masonry.Sum(m => m.OpeningArea), 2), R(q.Masonry.Sum(m => m.LintelArea), 2), R(q.Masonry.Sum(m => m.NetArea), 2), R(q.MasonryTotal), null, R(q.PlasterTotal, 2), (long)Math.Round(q.Masonry.Sum(m => m.Bricks)), R(q.Masonry.Sum(m => m.Mortar)), R(q.Masonry.Sum(m => m.PlasterMortar)));
        sheets[4].Tables.Add(wd);
        if (q.StairWorks.Count > 0) sheets[4].Tables.Add(StairQuantity.Table(q));   // cầu thang (Report/StairQuantity.cs)

        // ---------------- HOÀN THIỆN SÀN ----------------
        sheets[5].Tables.Add(FinishSummary(q));
        var fd = new QtyTable { Title = "Hoàn thiện sàn theo tầng", Headers = new[] { "Tầng", "Cấu tạo sàn", "Lớp", "Loại", "Diện tích (m²)", "Dày (mm)", "Thể tích (m³)" }, Widths = new double[] { 10, 20, 30, 10, 13, 9, 13 } };
        foreach (var g in q.Finishes.GroupBy(f => (f.Story, f.BuildUp, f.Layer)).OrderBy(g => q.StoryOrder.IndexOf(g.Key.Story)))
        {
            var f0 = g.First();
            fd.Add(g.Key.Story, g.Key.BuildUp, g.Key.Layer, f0.Kind, R(g.Sum(f => f.Area), 2), R(f0.Thickness * 1000, 0), R(g.Sum(f => f.Volume)));
        }
        sheets[5].Tables.Add(fd);
        var dropT = SlabDropReport.QuantityTable(p);   // [Sàn hạ cốt] danh sách ô hạ cốt (thông tin) – Report/SlabDropReport.cs
        if (dropT != null) sheets[5].Tables.Add(dropT);

        // ---------------- PHÂN TÍCH VẬT TƯ ----------------
        sheets[6].Tables.Add(MaterialLines(q, prices));
        sheets[6].Tables.Add(MaterialTotals(q, prices));

        // ---------------- ĐỊNH MỨC ----------------
        foreach (var t in NormTables(qs)) sheets[7].Tables.Add(t);
        return sheets;
    }

    // ------------------------------------------------------------------
    public static QtyTable MainSummary(QuantityResult q)
    {
        var t = new QtyTable { Title = "Tổng hợp khối lượng chính", Headers = new[] { "STT", "Hạng mục công việc", "Đơn vị", "Khối lượng" }, Widths = new double[] { 6, 60, 10, 14 }, WordWidths = new[] { 1.2, 10.3, 2.0, 3.0 } };
        int k = 1;
        foreach (var g in q.Concrete.GroupBy(c => (c.Category, c.Grade)).OrderBy(g => QtyCat.Rank(g.Key.Category)).ThenBy(g => g.Key.Grade, StringComparer.Ordinal))
            t.Add(k++, $"Bê tông {g.Key.Category.ToLowerInvariant()} {g.Key.Grade}", "m³", R(g.Sum(c => c.Volume)));
        foreach (var g in q.Lean.GroupBy(c => c.Grade))
            t.Add(k++, $"Bê tông lót móng {g.Key}", "m³", R(g.Sum(c => c.Volume)));
        if (q.Piles != null) t.Add(k++, $"Cọc BTCT {q.Piles.Size} ({q.Piles.Count} cọc)", "m", R(q.Piles.TotalLength, 2));
        foreach (var g in q.Concrete.Where(c => c.Formwork > 1e-9).GroupBy(c => c.Category).OrderBy(g => QtyCat.Rank(g.Key)))
            t.Add(k++, $"Ván khuôn {g.Key.ToLowerInvariant()}", "m²", R(g.Sum(c => c.Formwork), 2));
        var gr = q.RebarGroups;
        if (gr != null && gr.Total > 0)
        {
            t.Add(k++, "Cốt thép Ø≤10", "kg", R(gr.WeightLe10, 2));
            t.Add(k++, "Cốt thép 10<Ø≤18", "kg", R(gr.Weight10To18, 2));
            t.Add(k++, "Cốt thép Ø>18", "kg", R(gr.WeightGt18, 2));
        }
        foreach (var g in q.Masonry.GroupBy(m => m.Thickness).OrderBy(g => g.Key))
            t.Add(k++, $"Xây tường dày {g.Key * 1000:0} mm", "m³", R(g.Sum(m => m.Volume)));
        foreach (var g in q.Masonry.Where(m => m.PlasterArea > 0).GroupBy(m => m.PlasterThickness).OrderBy(g => g.Key))
            t.Add(k++, $"Trát tường dày {g.Key * 1000:0} mm", "m²", R(g.Sum(m => m.PlasterArea), 2));
        foreach (var g in q.Finishes.GroupBy(f => f.Layer))
            t.Add(k++, $"Hoàn thiện sàn: {g.Key}", "m²", R(g.Sum(f => f.Area), 2));
        foreach (var (name, unit, qty) in StairQuantity.SummaryLines(q)) t.Add(k++, name, unit, R(qty, 3));   // cầu thang
        t.AddTotal(null, "Tổng bê tông kết cấu (không kể bê tông lót)", "m³", R(q.ConcreteTotal));
        if (q.RebarTotal > 0) t.AddTotal(null, $"Tổng cốt thép ({q.RebarTotal / Math.Max(q.ConcreteTotal, 1e-9):0} kg/m³ bê tông)", "tấn", R(q.RebarTotal / 1000, 3));
        return t;
    }

    public static QtyTable ConcreteByStory(QuantityResult q)
    {
        var cats = q.Concrete.Select(c => c.Category).Distinct().OrderBy(QtyCat.Rank).ToList();
        var hd = new List<string> { "Tầng" };
        hd.AddRange(cats.Select(c => c + " (m³)"));
        hd.Add("Tổng (m³)");
        var t = new QtyTable { Title = "Bê tông theo tầng", Headers = hd.ToArray() };
        foreach (var st in q.StoryOrder)
        {
            var items = q.Concrete.Where(c => c.Story == st).ToList();
            if (items.Count == 0) continue;
            var row = new List<object> { st };
            row.AddRange(cats.Select(c => (object)R(items.Where(i => i.Category == c).Sum(i => i.Volume))));
            row.Add(R(items.Sum(i => i.Volume)));
            t.Add(row.ToArray());
        }
        var tot = new List<object> { "TỔNG" };
        tot.AddRange(cats.Select(c => (object)R(q.Volume(c))));
        tot.Add(R(q.ConcreteTotal));
        t.AddTotal(tot.ToArray());
        return t;
    }

    public static QtyTable RebarByCategory(QuantityResult q)
    {
        var t = new QtyTable { Title = "Cốt thép theo loại cấu kiện và nhóm đường kính", Headers = new[] { "Loại cấu kiện", "Ø≤10 (kg)", "10<Ø≤18 (kg)", "Ø>18 (kg)", "Tổng (kg)", "Bê tông (m³)", "Hàm lượng (kg/m³)" } };
        foreach (var r in q.RebarByCategory)
            t.Add(r.Category, R(r.Le10, 2), R(r.From10To18, 2), R(r.Gt18, 2), R(r.Total, 2), R(r.ConcreteVolume), R(r.Ratio, 1));
        var g = q.RebarGroups ?? new KetCauVXT.Drawing.GroupSummary();
        double vol = q.RebarByCategory.Sum(r => r.ConcreteVolume);
        t.AddTotal("TỔNG", R(g.WeightLe10, 2), R(g.Weight10To18, 2), R(g.WeightGt18, 2), R(g.Total, 2), R(vol), R(vol > 1e-9 ? g.Total / vol : 0, 1));
        return t;
    }

    public static QtyTable MasonrySummary(QuantityResult q, QuantitySettings qs)
    {
        var t = new QtyTable
        {
            Title = "Tổng hợp xây, trát theo loại tường",
            Headers = new[] { "Loại tường", "Dày (mm)", "S xây (m²)", "V xây (m³)", "S trát (m²)", qs.BrickName + " (viên)", $"Vữa xây {qs.MasonryMortar} (m³)", $"Vữa trát {qs.PlasterMortar} (m³)" },
            Widths = new double[] { 26, 9, 11, 11, 11, 18, 14, 14 }
        };
        foreach (var g in q.Masonry.GroupBy(m => m.WallType))
            t.Add(g.Key, R(g.First().Thickness * 1000, 0), R(g.Sum(m => m.NetArea), 2), R(g.Sum(m => m.Volume)), R(g.Sum(m => m.PlasterArea), 2), (long)Math.Round(g.Sum(m => m.Bricks)), R(g.Sum(m => m.Mortar)), R(g.Sum(m => m.PlasterMortar)));
        t.AddTotal("TỔNG", null, R(q.Masonry.Sum(m => m.NetArea), 2), R(q.MasonryTotal), R(q.PlasterTotal, 2), (long)Math.Round(q.Masonry.Sum(m => m.Bricks)), R(q.Masonry.Sum(m => m.Mortar)), R(q.Masonry.Sum(m => m.PlasterMortar)));
        return t;
    }

    public static QtyTable FinishSummary(QuantityResult q)
    {
        var t = new QtyTable { Title = "Tổng hợp hoàn thiện sàn theo lớp cấu tạo", Headers = new[] { "Cấu tạo sàn", "Lớp", "Loại", "Diện tích (m²)", "Dày (mm)", "Thể tích (m³)" }, Widths = new double[] { 20, 30, 10, 13, 9, 13 } };
        foreach (var g in q.Finishes.GroupBy(f => (f.BuildUp, f.Layer)))
        {
            var f0 = g.First();
            t.Add(g.Key.BuildUp, g.Key.Layer, f0.Kind, R(g.Sum(f => f.Area), 2), R(f0.Thickness * 1000, 0), R(g.Sum(f => f.Volume)));
        }
        return t;
    }

    public static QtyTable MaterialLines(QuantityResult q, bool prices)
    {
        var t = new QtyTable
        {
            Title = "Phân tích vật tư (khối lượng công tác × định mức)",
            Headers = new[] { "STT", "Nhóm", "Công tác", "Khối lượng", "Đơn vị", "Vật tư", "Định mức", "Đơn vị ĐM", "Nhu cầu", "Đơn vị VT" },
            Widths = new double[] { 6, 14, 34, 12, 8, 22, 11, 12, 13, 9 }
        };
        int k = 1;
        foreach (var l in q.MaterialLines)
            t.Add(k++, l.Group, l.Source, R(l.SourceQty, 4), l.SourceUnit, l.Material, l.Norm, l.NormUnit, R(l.Quantity, 3), l.Unit);
        return t;
    }

    public static QtyTable MaterialTotals(QuantityResult q, bool prices)
    {
        var hd = new List<string> { "STT", "Vật tư", "Đơn vị", "Khối lượng" };
        if (prices) { hd.Add("Đơn giá (đ)"); hd.Add("Thành tiền (đ)"); }
        var t = new QtyTable { Title = "Tổng hợp vật tư", Headers = hd.ToArray(), Widths = new double[] { 6, 30, 9, 14, 14, 16 } };
        int k = 1;
        foreach (var m in q.Materials)
        {
            var row = new List<object> { k++, m.Material, m.Unit, R(m.Quantity, 3) };
            if (prices) { row.Add(m.Price > 0 ? (object)m.Price : null); row.Add(m.Price > 0 ? (object)Math.Round(m.Amount, 0) : null); }
            t.Add(row.ToArray());
        }
        if (prices)
        {
            t.AddTotal(null, "TỔNG CỘNG", null, null, null, Math.Round(q.Materials.Sum(m => m.Amount), 0));
        }
        // quy đổi
        double cem = q.Materials.Where(m => m.Material == "Xi măng").Sum(m => m.Quantity);
        if (cem > 0) t.Add(null, "Xi măng quy đổi (tấn)", "tấn", R(cem / 1000, 3));
        return t;
    }

    public static List<QtyTable> NormTables(QuantitySettings qs)
    {
        var l = new List<QtyTable>();
        var src = new QtyTable { Title = "Nguồn định mức", Headers = new[] { "Ghi chú" }, Widths = new double[] { 140 } };
        src.Add(qs.EffectiveSource());
        src.Add("Các giá trị dưới đây là MẶC ĐỊNH THAM KHẢO – người dùng kiểm tra, chỉnh sửa trong chương trình (Triển khai hồ sơ → Bảng khối lượng vật tư… → Định mức; nhập bảng định mức hiện hành bằng nút “Nhập định mức từ Excel…”).");
        l.Add(src);
        var c = new QtyTable { Title = "Cấp phối 1 m³ bê tông", Headers = new[] { "Cấp độ bền", "Mác tương đương", "Xi măng (kg)", "Cát vàng (m³)", "Đá dăm (m³)", "Nước (lít)", "Ghi chú" }, Widths = new double[] { 11, 12, 12, 12, 12, 10, 26 } };
        foreach (var n in qs.ConcreteNorms ?? new List<ConcreteMixNorm>()) c.Add(n.Grade, n.Mark, n.Cement, n.Sand, n.Stone, n.Water, n.Note);
        l.Add(c);
        var m = new QtyTable { Title = "Cấp phối 1 m³ vữa xi măng – cát vàng", Headers = new[] { "Mác vữa", "Xi măng (kg)", "Cát vàng (m³)", "Nước (lít)", "Ghi chú" } };
        foreach (var n in qs.MortarNorms ?? new List<MortarMixNorm>()) m.Add(n.Grade, n.Cement, n.Sand, n.Water, n.Note);
        l.Add(m);
        var b = new QtyTable { Title = "Định mức xây 1 m³ tường (" + qs.BrickName + ")", Headers = new[] { "Áp dụng", "Dày ≤ (m)", "Gạch (viên/m³)", "Vữa (m³/m³)", "Ghi chú" } };
        foreach (var n in qs.MasonryNorms ?? new List<MasonryNorm>()) b.Add(n.Name, n.MaxThickness, n.Bricks, n.Mortar, n.Note);
        l.Add(b);
        var o = new QtyTable { Title = "Định mức, thông số khác", Headers = new[] { "Thông số", "Giá trị", "Đơn vị" }, Widths = new double[] { 50, 12, 16 } };
        o.Add("Vữa trát (lớp dày 15 mm)", qs.PlasterMortarPerM2, "m³/m²");
        o.Add("Dây thép buộc", qs.TieWirePerTon, "kg/tấn thép");
        o.Add("Gỗ ván khuôn", qs.FormworkWoodPer100, "m³/100 m²");
        o.Add("Đinh", qs.FormworkNailsPer100, "kg/100 m²");
        o.Add("Mác vữa xây / trát / lót sàn", $"{qs.MasonryMortar} / {qs.PlasterMortar} / {qs.FloorMortar}", "");
        o.Add("Bê tông lót: mác, dày, mở rộng mỗi phía", $"{qs.LeanGrade}; {qs.LeanThickness:0.00}; {qs.LeanOffset:0.00}", "m");
        o.Add("Cầu thang mặc định – lỗ sàn chưa khai báo (bản dày)", qs.IncludeStairs ? qs.StairSlabThickness.ToString("0.00", Inv) : "không tính", "m");
        o.Add("Lanh tô ước tính (cao; gác mỗi bên; cao cửa quy ước)", qs.IncludeLintels ? $"{qs.LintelHeight:0.00}; {qs.LintelBearing:0.00}; {qs.DoorHeight:0.00}" : "không tính", "m");
        l.Add(o);
        if (qs.ImportedNorms != null && qs.ImportedNorms.Count > 0)
        {
            var im = new QtyTable
            {
                Title = "Định mức nhập từ Excel" + (string.IsNullOrWhiteSpace(qs.ImportedNormFile) ? "" : " (" + qs.ImportedNormFile + ")"),
                Headers = new[] { "Mã hiệu", "Tên công tác", "Đơn vị", "Vật liệu", "Đơn vị VL", "Định mức" }
            };
            foreach (var r in qs.ImportedNorms) im.Add(r.Code, r.Name, r.Unit, r.Material, r.MaterialUnit, r.Value);
            l.Add(im);
        }
        if (qs.Prices != null && qs.Prices.Count > 0)
        {
            var pr = new QtyTable { Title = "Đơn giá vật tư (người dùng nhập)", Headers = new[] { "Vật tư", "Đơn vị", "Đơn giá (đ)" } };
            foreach (var u in qs.Prices) pr.Add(u.Item, u.Unit, u.Price);
            l.Add(pr);
        }
        return l;
    }

    // ==================================================================
    // EXCEL
    // ==================================================================
    /// <summary>Ghi "BangKhoiLuong.xlsx": Tổng hợp, Bê tông, Ván khuôn, Cốt thép, Tường-Trát, Hoàn thiện sàn, Phân tích vật tư, Định mức.
    /// Trang Phân tích vật tư dùng công thức (Nhu cầu = Khối lượng × Định mức, Thành tiền = Khối lượng × Đơn giá, tổng hợp bằng SUMIFS).</summary>
    public static void ExportExcel(Project p, QuantityResult q, string path)
    {
        if (q == null) throw new ArgumentNullException(nameof(q));
        var x = new XlsxWriter();
        var sheets = BuildSheets(p, q);
        foreach (var s in sheets)
        {
            var sh = x.AddSheet(s.Name);
            sh.FirstRowIsHeader = false; sh.FreezeRows = 0; sh.NumberStyle = XlStyle.Number3;
            if (s.Name == "Phân tích vật tư") { MaterialSheet(sh, q); continue; }
            sh.AddRow(new XlCell(s.Name.ToUpperInvariant() + " – " + (p?.Name ?? ""), XlStyle.Title));
            double[] widths = null;
            foreach (var t in s.Tables)
            {
                sh.AddEmptyRow();
                sh.AddBoldRow(t.Title);
                sh.AddHeaderRow(t.Headers.Cast<object>().ToArray());
                for (int i = 0; i < t.Rows.Count; i++)
                {
                    if (t.TotalRows.Contains(i)) sh.AddBoldRow(t.Rows[i]);
                    else sh.AddRow(t.Rows[i]);
                }
                if (widths == null || (t.Widths != null && t.Widths.Length > widths.Length)) widths = t.Widths ?? t.Headers.Select(h => Math.Clamp(h.Length + 4.0, 10, 30)).ToArray();
            }
            if (widths != null) sh.SetColumnWidths(widths);
        }
        x.Save(path);
    }

    private static void MaterialSheet(XlsxSheet sh, QuantityResult q)
    {
        var qs = q.Settings ?? new QuantitySettings();
        bool prices = qs.HasPrices;
        sh.AddRow(new XlCell("PHÂN TÍCH VẬT TƯ", XlStyle.Title));
        sh.AddRow("Nhu cầu = Khối lượng × Định mức (sửa định mức ở cột G, Excel tự tính lại). " + qs.EffectiveSource());
        sh.AddEmptyRow();
        sh.AddHeaderRow("STT", "Nhóm", "Công tác", "Khối lượng", "Đơn vị", "Vật tư", "Định mức", "Đơn vị ĐM", "Nhu cầu", "Đơn vị VT");
        int first = sh.RowCount + 1;
        int k = 1;
        foreach (var l in q.MaterialLines)
        {
            int r = sh.RowCount + 1;
            sh.AddRow(k++, l.Group, l.Source, R(l.SourceQty, 4), l.SourceUnit, l.Material, l.Norm, l.NormUnit,
                new XlFormula($"D{r}*G{r}", R(l.SourceQty, 4) * l.Norm), l.Unit);
        }
        int last = sh.RowCount;
        sh.AddEmptyRow();
        sh.AddBoldRow("TỔNG HỢP VẬT TƯ");
        if (prices) sh.AddHeaderRow("STT", "Vật tư", "Đơn vị", "Khối lượng", "Đơn giá (đ)", "Thành tiền (đ)");
        else sh.AddHeaderRow("STT", "Vật tư", "Đơn vị", "Khối lượng");
        k = 1;
        int tFirst = sh.RowCount + 1;
        foreach (var m in q.Materials)
        {
            int r = sh.RowCount + 1;
            double cached = q.MaterialLines.Where(l => l.Material == m.Material && l.Unit == m.Unit).Sum(l => R(l.SourceQty, 4) * l.Norm);
            var f = last >= first
                ? new XlFormula($"SUMIFS($I${first}:$I${last},$F${first}:$F${last},B{r},$J${first}:$J${last},C{r})", cached)
                : new XlFormula("0", 0);
            if (prices)
                sh.AddRow(k++, m.Material, m.Unit, f, m.Price, new XlFormula($"D{r}*E{r}", cached * m.Price, XlStyle.Integer));
            else sh.AddRow(k++, m.Material, m.Unit, f);
        }
        int tLast = sh.RowCount;
        if (prices && tLast >= tFirst)
            sh.AddBoldRow(null, "TỔNG CỘNG", null, null, null, new XlFormula($"SUM(F{tFirst}:F{tLast})", q.Materials.Sum(m => m.Amount), XlStyle.Integer));
        sh.SetColumnWidths(6, 14, 34, 13, 9, 22, 11, 12, 13, 9);
    }

    // ==================================================================
    // WORD
    // ==================================================================
    /// <summary>Chương "BẢNG KHỐI LƯỢNG VẬT TƯ" trong thuyết minh (các bảng tổng hợp; chi tiết xem file Excel).</summary>
    public static void WriteWordChapter(Project p, QuantityResult q, DocxWriter d, int n)
    {
        var qs = q.Settings ?? new QuantitySettings();
        d.Heading($"{n}. KHỐI LƯỢNG VẬT TƯ CHỦ YẾU", 1);
        d.Paragraph("Khối lượng được chương trình tính tự động từ mô hình kết cấu và kết quả thiết kế (kích thước móng, bố trí cốt thép). Bảng chi tiết từng cấu kiện, diễn giải ván khuôn, thống kê cốt thép và phân tích vật tư được xuất trong file Excel " + DefaultFileName + " kèm theo.");
        d.Heading($"{n}.1. Quy ước tính khối lượng", 2);
        foreach (var s in q.Notes) d.Bullet(s);
        foreach (var w in q.Warnings) d.Paragraph("Lưu ý: " + w, false, true);
        int k = 2;
        d.Heading($"{n}.{k++}. Tổng hợp khối lượng", 2);
        WordTable(d, MainSummary(q));
        d.Heading($"{n}.{k++}. Bê tông theo loại cấu kiện và theo tầng", 2);
        var byCat = new QtyTable { Title = "Bê tông, ván khuôn theo loại cấu kiện", Headers = new[] { "Loại cấu kiện", "Mác", "Số cấu kiện", "Bê tông (m³)", "Ván khuôn (m²)" }, WordWidths = new[] { 4.5, 2.0, 2.8, 3.5, 3.7 } };
        foreach (var g in q.Concrete.GroupBy(c => (c.Category, c.Grade)).OrderBy(g => QtyCat.Rank(g.Key.Category)))
            byCat.Add(g.Key.Category, g.Key.Grade, g.Sum(c => c.Count), R(g.Sum(c => c.Volume)), R(g.Sum(c => c.Formwork), 2));
        byCat.AddTotal("TỔNG", null, q.Concrete.Sum(c => c.Count), R(q.ConcreteTotal), R(q.FormworkTotal, 2));
        if (q.Lean.Count > 0) byCat.Add(QtyCat.Lean, q.Lean[0].Grade, q.Lean.Sum(c => c.Count), R(q.LeanTotal), null);
        WordTable(d, byCat);
        WordTable(d, ConcreteByStory(q), 9);
        if (q.Piles != null)
            d.Paragraph($"Cọc {q.Piles.Size} mm, bê tông {q.Piles.Grade}: {q.Piles.Count} cọc × {Fmt(q.Piles.LengthEach)} m = {Fmt(q.Piles.TotalLength)} m; thể tích {Fmt(q.Piles.Volume, "0.000")} m³.");
        if (q.RebarRows.Count > 0)
        {
            d.Heading($"{n}.{k++}. Cốt thép", 2);
            WordTable(d, RebarByCategory(q), 10);
        }
        if (q.Masonry.Count > 0)
        {
            d.Heading($"{n}.{k++}. Xây tường, trát", 2);
            WordTable(d, MasonrySummary(q, qs), 9);
        }
        if (q.Finishes.Count > 0)
        {
            d.Heading($"{n}.{k++}. Hoàn thiện sàn", 2);
            WordTable(d, FinishSummary(q), 10);
        }
        if (q.StairWorks.Count > 0)
        {
            d.Heading($"{n}.{k++}. Cầu thang: xây bậc, ốp bậc, trát", 2);
            WordTable(d, StairQuantity.Table(q), 9);
        }
        if (q.GroundWorks.Count > 0)   // [CừTràm]
        {
            d.Heading($"{n}.{k++}. Gia cố nền bằng cừ tràm", 2);
            WordTable(d, CuTramQuantity.Table(q), 9);
        }
        d.Heading($"{n}.{k++}. Tổng hợp vật tư", 2);
        d.Paragraph("Nhu cầu vật tư = khối lượng công tác × định mức. " + qs.EffectiveSource(), false, true);
        WordTable(d, MaterialTotals(q, qs.HasPrices), 10);
    }

    /// <summary>Ghi một bảng vào Word (tên bảng đánh số + bảng; hàng tổng giữ nguyên).</summary>
    public static void WordTable(DocxWriter d, QtyTable t, double fontPt = 11)
    {
        if (t == null || t.Rows.Count == 0) return;
        d.TableCaption(t.Title);
        d.Table(t.Headers, t.Rows.Select(r => r.Select(c => Fmt(c, c is double dd && Math.Abs(dd) >= 1000 ? "0.0" : "0.00")).ToArray()), t.WordWidths, fontPt);
    }

    /// <summary>File Word riêng chỉ gồm bảng khối lượng (nút "Xuất Word" trong cửa sổ khối lượng).</summary>
    public static void ExportWord(Project p, QuantityResult q, string path)
    {
        var d = new DocxWriter { FooterText = "Bảng khối lượng vật tư – " + (p?.Name ?? ""), FooterSkipFirstPage = false };
        d.Title("BẢNG KHỐI LƯỢNG VẬT TƯ", 18);
        d.Title("CÔNG TRÌNH: " + (p?.Name ?? "").ToUpperInvariant(), 14);
        d.Paragraph("Ngày lập: " + DateTime.Now.ToString("dd/MM/yyyy", Inv), false, true, ParaAlign.Center);
        WriteWordChapter(p, q, d, 1);
        d.Save(path);
    }
}
