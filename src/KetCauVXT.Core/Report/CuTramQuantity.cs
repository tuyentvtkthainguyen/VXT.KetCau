using KetCauVXT.Design;
using KetCauVXT.Design.Foundation;
using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>[CừTràm] Một công tác gia cố nền: đóng cừ tràm (cây, m dài), cát đệm đầu cừ (m³).</summary>
public class GroundWorkItem
{
    public string Story { get; set; } = "";
    public string Member { get; set; } = "";
    /// <summary>"Cừ tràm" (cây), "Chiều dài cừ" (m), "Cát đệm" (m³)</summary>
    public string Kind { get; set; } = "";
    public string Work { get; set; } = "";
    public string Unit { get; set; } = "";
    /// <summary>Số móng giống hệt</summary>
    public int Count { get; set; } = 1;
    public double Quantity { get; set; }
    public string Dims { get; set; } = "";
    public string Note { get; set; } = "";
}

/// <summary>[CừTràm] Khối lượng gia cố nền cừ tràm (gọi từ QuantityCalculator.Compute sau phần móng):
/// số cây n = ⌈mật độ × L × B⌉ mỗi móng (× số móng giống hệt), tổng chiều dài n·Lc, cát đệm (L + 2·mở rộng lót)·(B + 2·mở rộng lót)·t_cát;
/// bê tông lót đã tính ở mục bê tông lót (không tính trùng).</summary>
public static class CuTramQuantity
{
    private static string F2(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    public static void Compute(Project p, QuantityResult q, DesignSummary s, QuantitySettings qs)
    {
        if (p == null || q == null || s == null) return;
        double lo = Math.Max(0, qs?.LeanOffset ?? 0);
        void Add(string member, int n, CuTramResult r, double L, double B)
        {
            if (r == null) return;
            n = Math.Max(1, n);
            string spec = FormattableString.Invariant($"Ø{r.D1 * 100:0}–{r.D2 * 100:0.#} cm, L = {r.Length:0.00} m");
            q.GroundWorks.Add(new GroundWorkItem
            {
                Story = QuantityCalculator.FoundationStory, Member = member, Kind = "Cừ tràm", Work = "Đóng cừ tràm " + spec, Unit = "cây", Count = n,
                Quantity = n * r.Count, Dims = (n > 1 ? n + "×" : "") + FormattableString.Invariant($"⌈{r.Density:0}×{L:0.00}×{B:0.00}⌉ = ") + (n > 1 ? n + "×" : "") + r.Count + " cây", Note = FormattableString.Invariant($"{r.Density:0} cây/m²")
            });
            q.GroundWorks.Add(new GroundWorkItem
            {
                Story = QuantityCalculator.FoundationStory, Member = member, Kind = "Chiều dài cừ", Work = "Tổng chiều dài cừ tràm", Unit = "m", Count = n,
                Quantity = n * r.TotalLength, Dims = (n > 1 ? n + "×" : "") + $"{r.Count}×{F2(r.Length)}"
            });
            if (r.SandCushion > 1e-6)
            {
                double v = (L + 2 * lo) * (B + 2 * lo) * r.SandCushion;
                q.GroundWorks.Add(new GroundWorkItem
                {
                    Story = QuantityCalculator.FoundationStory, Member = member, Kind = "Cát đệm", Work = "Cát đệm đầu cừ", Unit = "m³", Count = n,
                    Quantity = n * v, Dims = (n > 1 ? n + "×" : "") + $"{F2(L + 2 * lo)}×{F2(B + 2 * lo)}×{F2(r.SandCushion)}"
                });
            }
        }
        foreach (var f in s.Footings.Where(x => x.CuTram != null)) Add(f.Name, f.Quantity, f.CuTram, f.L, f.B);
        foreach (var st in s.StripFootings.Where(x => x.CuTram != null)) Add(st.Name, st.Quantity, st.CuTram, st.Length, st.Width);
        if (q.GroundWorks.Count > 0)
            q.Notes.Add("Gia cố nền cừ tràm: số cây = ⌈mật độ × diện tích đáy móng⌉ mỗi móng; cát đệm phủ đầu cừ theo phạm vi bê tông lót; bê tông lót tính ở mục 'Bê tông lót'. "
                        + "Đầu cừ phải nằm dưới mực nước ngầm thấp nhất.");
    }

    /// <summary>Tổng hợp vật tư (gọi trong QuantityCalculator.MaterialQty).</summary>
    public static void AddMaterials(QuantityResult q, Action<string, string, double, string, string, double, string, string> add)
    {
        var w = q?.GroundWorks;
        if (w == null || w.Count == 0) return;
        foreach (var g in w.Where(x => x.Kind == "Cừ tràm").GroupBy(x => x.Work))
            add("Gia cố nền", g.Key, g.Sum(x => x.Quantity), "cây", "Cừ tràm (" + g.Key.Replace("Đóng cừ tràm ", "") + ")", 1, "cây/cây", "cây");
        add("Gia cố nền", "Cát đệm đầu cừ", w.Where(x => x.Kind == "Cát đệm").Sum(x => x.Quantity), "m³", "Cát đệm (cát vàng / cát san lấp)", 1.2, "m³/m³ (kể hệ số đầm chặt)", "m³");
    }

    /// <summary>Bảng công tác gia cố nền (Excel trang Bê tông, Word, cửa sổ khối lượng).</summary>
    public static QtyTable Table(QuantityResult q)
    {
        var t = new QtyTable
        {
            Title = "Gia cố nền bằng cừ tràm (số cây, chiều dài, cát đệm)",
            Headers = new[] { "STT", "Móng", "Công tác", "Diễn giải", "Đơn vị", "Khối lượng", "Ghi chú" },
            Widths = new double[] { 6, 10, 34, 40, 8, 12, 16 },
            WordWidths = new[] { 0.9, 1.4, 4.0, 4.4, 1.2, 1.6, 2.0 }
        };
        int k = 1;
        foreach (var x in q.GroundWorks)
            t.Add(k++, x.Member, x.Work, x.Dims, x.Unit, x.Unit == "cây" ? (object)(long)Math.Round(x.Quantity) : Math.Round(x.Quantity, 3), x.Note);
        foreach (var g in q.GroundWorks.GroupBy(x => (x.Kind, x.Unit)))
            t.AddTotal(null, null, "Tổng " + g.Key.Kind.ToLowerInvariant(), null, g.Key.Unit, g.Key.Unit == "cây" ? (object)(long)Math.Round(g.Sum(x => x.Quantity)) : Math.Round(g.Sum(x => x.Quantity), 3), null);
        return t;
    }
}
