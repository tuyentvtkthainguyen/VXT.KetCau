using KetCauVXT.Model;

namespace KetCauVXT.Report;

/// <summary>Một công tác cầu thang ngoài bê tông: xây bậc gạch, ốp bậc (granite / gỗ / gạch), vữa lót, trát mặt dưới bản, len chân tường.</summary>
public class StairWorkItem
{
    public string Story { get; set; } = "";
    /// <summary>Tên cầu thang</summary>
    public string Member { get; set; } = "";
    /// <summary>Tên công tác, ví dụ "Xây bậc thang gạch", "Ốp lát bậc thang đá granite"</summary>
    public string Work { get; set; } = "";
    /// <summary>"Xây", "Ốp", "Trát", "Len"</summary>
    public string Kind { get; set; } = "";
    public string Unit { get; set; } = "";
    public double Quantity { get; set; }
    public string Dims { get; set; } = "";
    /// <summary>Vật liệu ốp (tên vật tư) – với Kind = "Ốp"/"Len"</summary>
    public string Material { get; set; } = "";
    /// <summary>Gạch (viên), vữa (m³) cho công tác xây; vữa lót / vữa trát (m³)</summary>
    public double Bricks { get; set; }
    public double Mortar { get; set; }
}

/// <summary>Khối lượng cầu thang (QuantityCalculator.StairQty):
/// <list type="bullet">
/// <item>Bê tông CHỈ gồm bản thang (L nghiêng × bề rộng bản × t), chiếu nghỉ, dầm cốn (nếu có), dầm chiếu nghỉ; bậc đổ BTCT liền khối mới tính thêm bê tông bậc.</item>
/// <item>Bậc xây gạch: V = Σ ½·b·h·w (n − 1 bậc mỗi vế) – công tác "Xây bậc thang gạch", gạch + vữa theo định mức.</item>
/// <item>Ốp bậc: diện tích mặt bậc + cổ bậc ((n−1)·b + n·h)·w + mặt chiếu nghỉ; vữa lót = diện tích × chiều dày lót.</item>
/// <item>Trát mặt dưới bản thang + chiếu nghỉ.</item>
/// <item>Lỗ sàn cầu thang chưa khai báo → cầu thang 2 vế mặc định (Stair.FromOpening, chiều dày theo QuantitySettings).</item>
/// </list></summary>
public static class StairQuantity
{
    public static void Compute(Project p, QuantityResult q, QuantitySettings qs, ConcreteDesignSettings cs)
    {
        foreach (var s in Stair.Effective(p, qs))
        {
            StairLayout L;
            try { L = s.Layout(p); }
            catch (Exception ex) { q.Warnings.Add($"Cầu thang {s.Name}: {ex.Message}"); continue; }
            if (!L.Valid) { q.Warnings.AddRange(L.Warnings); continue; }
            Add(q, qs, cs, L);
        }
    }

    private static string F2(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    private static string Mm(double v) => (v * 1000).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    public static void Add(QuantityResult q, QuantitySettings qs, ConcreteDesignSettings cs, StairLayout L)
    {
        var s = L.S;
        string st = L.TopStory, nm = string.IsNullOrWhiteSpace(s.Name) ? "CT" : s.Name;
        string gs = QuantityCalculator.Grade(cs.ConcreteSlab, "B20"), gb = QuantityCalculator.Grade(cs.ConcreteBeam, "B20");
        double t = L.WaistT;
        // ---- bê tông bản thang ----
        foreach (var f in L.Flights)
        {
            double sw = L.SlabWidth(f);
            double fw = f.Inclined * sw + (L.Stringers ? 0 : f.Inclined * t);
            q.Concrete.Add(new ConcreteItem
            {
                Category = QtyCat.Stair, Story = st, Member = $"{nm} bản thang vế {f.Index}", Grade = gs,
                Dims = $"L nghiêng {F2(f.Inclined)} × {F2(sw)} × {F2(t)} ({f.Risers} bậc {Mm(L.Riser)}×{Mm(L.Tread)})",
                Volume = L.FlightSlabVolume(f), Formwork = fw,
                FormworkNote = L.Stringers ? $"đáy {F2(f.Inclined)}×{F2(sw)}" : $"đáy {F2(f.Inclined)}×{F2(sw)} + thành {F2(f.Inclined)}×{F2(t)}",
                Note = "không kể bậc"
            });
            if (s.Steps == StepConstruction.Concrete)
                q.Concrete.Add(new ConcreteItem
                {
                    Category = QtyCat.Stair, Story = st, Member = $"{nm} bậc BTCT vế {f.Index}", Grade = gs,
                    Dims = $"{f.Treads} × ½×{F2(L.Tread)}×{F2(L.Riser)}×{F2(f.Width)}",
                    Volume = L.StepVolume(f), Formwork = f.Risers * L.Riser * f.Width,
                    FormworkNote = $"cổ bậc {f.Risers}×{F2(L.Riser)}×{F2(f.Width)}", Note = "bậc đổ liền khối"
                });
            if (L.Stringers)
            {
                int ns = L.StringerCount;
                double hb = s.StringerH, bb = s.StringerB;
                q.Concrete.Add(new ConcreteItem
                {
                    Category = QtyCat.Stair, Story = st, Member = $"{nm} dầm cốn vế {f.Index}", Grade = gb, Count = ns,
                    Dims = $"{Mm(bb)}×{Mm(hb)}, L nghiêng = {F2(f.Inclined)}",
                    Volume = ns * bb * hb * f.Inclined, Formwork = ns * (bb + hb + Math.Max(0, hb - t)) * f.Inclined,
                    FormworkNote = $"{ns}×({F2(bb)} + {F2(hb)} + {F2(hb - t)})×{F2(f.Inclined)}"
                });
            }
        }
        // ---- chiếu nghỉ ----
        int k = 1;
        foreach (var ld in L.Landings)
        {
            double sp = L.LandingSlabSpan(ld);
            q.Concrete.Add(new ConcreteItem
            {
                Category = QtyCat.Stair, Story = st, Member = $"{nm} chiếu nghỉ {k++}", Grade = gs,
                Dims = $"{F2(sp)} × {F2(ld.Width)} × {F2(ld.Thickness)}", Volume = sp * ld.Width * ld.Thickness,
                Formwork = sp * ld.Width, FormworkNote = $"đáy {F2(sp)}×{F2(ld.Width)}"
            });
        }
        // ---- dầm chiếu nghỉ ----
        if (L.HasLandingBeam)
        {
            double b = s.LandingBeamB, h = s.LandingBeamH, Lb = L.Wr;
            q.Concrete.Add(new ConcreteItem
            {
                Category = QtyCat.Stair, Story = st, Member = $"{nm} dầm chiếu nghỉ", Grade = gb,
                Dims = $"{Mm(b)}×{Mm(h)}, L = {F2(Lb)}", Volume = b * h * Lb,
                Formwork = b * Lb + (h + Math.Max(0, h - L.LandingT)) * Lb,
                FormworkNote = $"{F2(b)}×{F2(Lb)} + ({F2(h)} + {F2(h - L.LandingT)})×{F2(Lb)}"
            });
        }

        // ---- xây bậc gạch ----
        if (s.Steps == StepConstruction.Brick)
        {
            double v = L.Flights.Sum(L.StepVolume);
            int nt = L.Flights.Sum(f => f.Treads);
            if (v > 1e-9)
                q.StairWorks.Add(new StairWorkItem
                {
                    Story = st, Member = nm, Work = "Xây bậc thang gạch", Kind = "Xây", Unit = "m³", Quantity = v,
                    Dims = $"Σ ½·b·h·w = {nt} × ½×{F2(L.Tread)}×{F2(L.Riser)}×{F2(L.FlightWidth)}",
                    Material = qs.BrickName, Bricks = v * qs.StepBricks, Mortar = v * qs.StepMortar
                });
        }
        // ---- ốp bậc + chiếu nghỉ ----
        if (s.Finish != StepFinish.None)
        {
            double aSteps = L.Flights.Sum(L.FinishArea), aLand = L.Landings.Sum(ld => ld.Area);
            double a = aSteps + aLand;
            string work = s.Finish switch
            {
                StepFinish.Granite => "Ốp lát bậc thang đá granite",
                StepFinish.Wood => "Ốp bậc thang gỗ",
                _ => "Ốp lát bậc thang gạch"
            };
            string mat = s.Finish switch { StepFinish.Granite => qs.GraniteName, StepFinish.Wood => qs.WoodName, _ => qs.StairTileName };
            q.StairWorks.Add(new StairWorkItem
            {
                Story = st, Member = nm, Work = work, Kind = "Ốp", Unit = "m²", Quantity = a, Material = mat,
                Dims = $"bậc Σ((n−1)·b + n·h)·w = {F2(aSteps)} + chiếu nghỉ {F2(aLand)}; dày {Mm(s.FinishT)} mm",
                Mortar = a * Math.Max(0, s.BeddingThickness)
            });
            if (s.Skirting)
            {
                double len = L.Flights.Sum(f => f.Inclined) + L.Landings.Sum(ld => 2 * ld.Span + ld.Width);
                q.StairWorks.Add(new StairWorkItem
                {
                    Story = st, Member = nm, Work = "Len chân tường cầu thang", Kind = "Len", Unit = "m", Quantity = len, Material = mat,
                    Dims = "một bên tường dọc vế + chu vi chiếu nghỉ sát tường"
                });
            }
        }
        // ---- trát mặt dưới ----
        if (s.PlasterThickness > 0)
        {
            double a = L.Flights.Sum(f => f.Inclined * f.Width) + L.Landings.Sum(ld => ld.Area);
            q.StairWorks.Add(new StairWorkItem
            {
                Story = st, Member = nm, Work = "Trát mặt dưới bản thang, chiếu nghỉ", Kind = "Trát", Unit = "m²", Quantity = a,
                Dims = $"Σ L nghiêng × w + chiếu nghỉ; dày {Mm(s.PlasterThickness)} mm",
                Mortar = a * qs.PlasterMortarPerM2 * s.PlasterThickness / 0.015
            });
        }
    }

    /// <summary>Phân tích vật tư công tác cầu thang (gọi trong QuantityCalculator.MaterialQty).</summary>
    public static void AddMaterials(QuantityResult q, QuantitySettings qs,
        Action<string, string, double, string, string, double, string, string> add, Action<string, double, string> mortar)
    {
        var w = q.StairWorks;
        if (w == null || w.Count == 0) return;
        double vb = w.Where(x => x.Kind == "Xây").Sum(x => x.Quantity);
        add("Xây bậc thang", "Xây bậc thang gạch", vb, "m³", qs.BrickName, qs.StepBricks, "viên/m³", "viên");
        mortar("Vữa xây bậc thang", w.Where(x => x.Kind == "Xây").Sum(x => x.Mortar), qs.MasonryMortar);
        foreach (var g in w.Where(x => x.Kind == "Ốp").GroupBy(x => (x.Work, x.Material)))
            add("Ốp bậc thang", g.Key.Work, g.Sum(x => x.Quantity), "m²", g.Key.Material, 1, "m²/m²", "m²");
        foreach (var g in w.Where(x => x.Kind == "Len").GroupBy(x => x.Material))
            add("Ốp bậc thang", "Len chân tường cầu thang", g.Sum(x => x.Quantity), "m", g.Key + " (len)", 1, "m/m", "m");
        mortar("Vữa lót ốp bậc thang", w.Where(x => x.Kind == "Ốp").Sum(x => x.Mortar), qs.FloorMortar);
        mortar("Vữa trát bản thang", w.Where(x => x.Kind == "Trát").Sum(x => x.Mortar), qs.PlasterMortar);
    }

    /// <summary>Bảng công tác cầu thang (Excel trang "Tường-Trát", Word, cửa sổ khối lượng).</summary>
    public static QtyTable Table(QuantityResult q)
    {
        var t = new QtyTable
        {
            Title = "Cầu thang: xây bậc, ốp bậc, trát (bê tông bản thang, chiếu nghỉ, dầm xem trang Bê tông)",
            Headers = new[] { "STT", "Tầng", "Cầu thang", "Công tác", "Diễn giải", "Đơn vị", "Khối lượng", "Gạch (viên)", "Vữa (m³)" },
            Widths = new double[] { 6, 9, 10, 32, 60, 8, 12, 11, 10 },
            WordWidths = new[] { 0.9, 1.4, 1.4, 3.6, 4.6, 1.2, 1.6, 1.6, 1.4 }
        };
        int k = 1;
        foreach (var x in q.StairWorks)
            t.Add(k++, x.Story, x.Member, x.Work, x.Dims, x.Unit, Math.Round(x.Quantity, 3), x.Bricks > 0 ? (object)(long)Math.Round(x.Bricks) : null, x.Mortar > 0 ? (object)Math.Round(x.Mortar, 3) : null);
        return t;
    }

    /// <summary>Các dòng tổng hợp cho bảng "Tổng hợp khối lượng chính".</summary>
    public static IEnumerable<(string Name, string Unit, double Qty)> SummaryLines(QuantityResult q) =>
        (q.StairWorks ?? new List<StairWorkItem>()).GroupBy(x => (x.Work, x.Unit)).Select(g => (g.Key.Work, g.Key.Unit, g.Sum(x => x.Quantity)));
}
