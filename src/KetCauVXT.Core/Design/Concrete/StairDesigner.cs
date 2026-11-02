using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Concrete;

/// <summary>Thiết kế cầu thang bộ BTCT theo TCVN 5574:2018 (uốn RcSection.DesignRect, cắt RcSection.Shear / QbConcrete):
/// <list type="bullet">
/// <item>Bản thang chịu lực: dải 1 m nghiêng, nhịp tính theo hình chiếu bằng L0 = (n−1)·b giữa dầm sàn và dầm chiếu nghỉ
///   (tải q theo m² mặt bằng → M = q·L0²/8 tương đương dầm nghiêng chịu tải đứng). Gối khớp: M nhịp = qL0²/8, thép mũ cấu tạo qL0²/24;
///   ngàm một phần: qL0²/10 và qL0²/16. Không có dầm chiếu nghỉ: bản gãy khúc nhịp L0 = (n−1)·b + chiều sâu chiếu nghỉ.</item>
/// <item>Bản tựa dầm cốn: dải 1 m ngang vế nhịp = khoảng cách giữa dầm cốn (hoặc dầm cốn – tường), tải vuông góc q·cos²α.</item>
/// <item>Chiếu nghỉ: bản một phương nhịp = chiều sâu chiếu nghỉ (dầm chiếu nghỉ – tường đầu ô).</item>
/// <item>Dầm chiếu nghỉ, dầm cốn: dầm đơn giản; tải từ phản lực bản (quy về phân bố đều) + trọng lượng bản thân.</item>
/// <item>Thép rải (phân bố) ≥ 15% thép chịu lực, bước ≤ 250 mm; thép mũ dài L0/4 tính từ mép gối + đoạn neo, đầu bẻ móc xuống.</item>
/// <item>Thép bản thang chạy suốt một thanh (không nối trong nhịp); thanh dài &gt; chiều dài cây thép → nối tại vùng gối chiếu nghỉ.</item>
/// </list></summary>
public static class StairDesigner
{
    public const double MinRatioSlab = 0.10;
    private static readonly int[] DefaultSlabDias = { 6, 8, 10, 12 };

    public static List<StairDesign> Design(Project p, List<string> warnings)
    {
        warnings ??= new List<string>();
        var res = new List<StairDesign>();
        if (p == null) return res;
        foreach (var s in Stair.Effective(p))
        {
            try
            {
                var d = DesignOne(p, s);
                if (d == null) continue;
                d.IsDefault = s.Id <= 0;
                res.Add(d);
                if (!d.Ok)
                {
                    var failed = d.Checks.Where(c => !c.Ok).Select(c => c.Name).ToList();
                    warnings.Add("Cấu kiện " + d.Name + " không đạt: " + string.Join("; ", failed));
                }
            }
            catch (Exception ex) { warnings.Add("LỖI thiết kế cầu thang " + s.Name + ": " + ex.Message); }
        }
        return res;
    }

    private sealed class Mats
    {
        public Material Slab, Beam, Small, Main;
        public double CoverSlab, CoverBeam;
        public int[] SlabDias, BeamDias;
        public double MinBeam;
        public Material Steel(int d) => d < 10 ? Small : Main;
    }

    /// <summary>Thiết kế một cầu thang (null nếu hình học không hợp lệ).</summary>
    public static StairDesign DesignOne(Project p, Stair s)
    {
        var L = s.Layout(p);
        var cs = p.Concrete ?? new ConcreteDesignSettings();
        var d = new StairDesign
        {
            Name = string.IsNullOrWhiteSpace(s.Name) ? "CT" : s.Name, Story = L.TopStory, StairId = s.Id, OpeningId = s.OpeningId,
            Stair = s.Clone(), TypeText = Stair.TypeName(s.Type), SupportText = Stair.SupportName(s.Support, s.StringerSides)
        };
        d.Messages.AddRange(L.Warnings);
        if (!L.Valid) { d.Messages.Add("LỖI hình học cầu thang không hợp lệ"); return d; }
        var m = new Mats
        {
            Slab = DesignUtil.ConcreteOf(p, cs.ConcreteSlab, "B20"), Beam = DesignUtil.ConcreteOf(p, cs.ConcreteBeam, "B20"),
            Small = p.Material(cs.RebarStirrup), Main = p.Material(cs.RebarMain),
            CoverSlab = cs.CoverSlab > 0 ? cs.CoverSlab : 0.015, CoverBeam = cs.CoverBeam > 0 ? cs.CoverBeam : 0.025,
            SlabDias = DesignUtil.Sorted(cs.SlabBars, DefaultSlabDias).Union(new[] { 10, 12 }).OrderBy(x => x).ToArray(),
            BeamDias = DesignUtil.Sorted(cs.BeamBars, new[] { 12, 14, 16, 18, 20 }).Union(new[] { 12 }).OrderBy(x => x).ToArray(),
            MinBeam = cs.MinRatioBeam
        };
        double stock = p.RebarPrefs?.StockLength > 0 ? p.RebarPrefs.StockLength : 11.7;
        var set = StairLoadCalc.Compute(p, L);
        d.Loads = set;
        d.Height = L.H; d.Risers = L.NRisers; d.Riser = L.Riser; d.Tread = L.Tread; d.FlightWidth = L.FlightWidth;
        d.WaistT = L.WaistT; d.LandingT = L.LandingT; d.CosA = L.Flights[0].CosA; d.ZBottom = L.ZBot; d.ZTop = L.ZTop;
        double qf = set.FlightDesign, ql = set.LandingDesign;
        string nm = d.Name;
        bool bent = !L.HasLandingBeam && L.Landings.Count > 0 && s.Type == StairType.TwoFlightU;

        // ---- bản thang từng vế ----
        var flightParts = new List<StairPart>();
        foreach (var f in L.Flights)
        {
            StairPart pt;
            if (L.Stringers && f.DU != 0)
            {
                double clear = Math.Max(0.3, f.Width - L.StringerCount * s.StringerB);
                double qn = qf * f.CosA * f.CosA;
                pt = SlabPart(m, $"{nm} BT{f.Index}", "Bản thang", clear, qn, L.WaistT, s.PartialFixity, "q·cos²α",
                    lenMain: f.Width - 2 * m.CoverSlab, widthAcross: f.Inclined, lenDist: f.Inclined, anchorMain: 0, stock: stock, mainAlong: false, hookAtSupport: s.StringerB);
            }
            else
            {
                double L0 = f.Run, qq = qf;
                if (bent && f.DU != 0) { L0 = f.Run + L.Landings[0].Span; qq = Math.Max(qf, ql); }
                double anchor = Math.Max(0.15, 10 * 12 / 1000.0);
                double lenIncl = bent && f.DU != 0 ? f.Inclined + L.Landings[0].Span : f.Inclined;
                pt = SlabPart(m, $"{nm} BT{f.Index}", "Bản thang", L0, qq, L.WaistT, s.PartialFixity, bent ? "bản gãy khúc" : "",
                    lenMain: lenIncl, widthAcross: L.SlabWidth(f), lenDist: L.SlabWidth(f) - 2 * m.CoverSlab, anchorMain: anchor, stock: stock, mainAlong: true,
                    hookAtSupport: 0, cosA: f.CosA);
            }
            pt.Flight = f.Index; pt.Flights.Add(f.Index); pt.Story = L.TopStory; pt.CosA = f.CosA; pt.Length = f.Inclined;
            flightParts.Add(pt);
        }
        d.Parts.AddRange(Merge(flightParts, nm, "BT"));

        // ---- chiếu nghỉ ----
        var landParts = new List<StairPart>();
        int li = 1;
        foreach (var ld in L.Landings)
        {
            double span = L.LandingSlabSpan(ld);
            var pt = SlabPart(m, $"{nm} CN{li}", "Chiếu nghỉ", Math.Max(span, 0.3), ql, L.LandingT, s.PartialFixity, "",
                lenMain: span, widthAcross: ld.Width, lenDist: ld.Width - 2 * m.CoverSlab, anchorMain: 0.15, stock: stock, mainAlong: true, hookAtSupport: 0);
            pt.Flight = 0; pt.Flights.Add(li); pt.Story = L.TopStory; pt.Length = span;
            landParts.Add(pt);
            li++;
        }
        d.Parts.AddRange(Merge(landParts, nm, "CN"));

        // ---- dầm cốn ----
        if (L.Stringers)
        {
            var sp = new List<StairPart>();
            foreach (var f in L.Flights)
            {
                double share = L.StringerCount >= 2 ? f.Width / 2 : f.Width / 2;   // 1 dầm cốn: nửa bản về tường
                double sw = StairUnits.Concrete * s.StringerB * Math.Max(0, s.StringerH - L.WaistT) / f.CosA * set.GammaTT;
                double q = qf * share + sw;
                var pt = BeamPart(m, $"{nm} DC{f.Index}", "Dầm cốn", f.Run, f.Inclined, q, s.StringerB, s.StringerH, stock, f.CosA);
                pt.Quantity = L.StringerCount; pt.Flight = f.Index; pt.Flights.Add(f.Index); pt.Story = L.TopStory;
                sp.Add(pt);
            }
            d.Parts.AddRange(Merge(sp, nm, "DC"));
        }

        // ---- dầm chiếu nghỉ ----
        if (L.HasLandingBeam)
        {
            double Lb = L.Wr, tot = 0;
            foreach (var f in L.Flights)
            {
                double load = qf * f.Run * f.Width;
                if (f.DV != 0) { tot += load / 2; continue; }
                bool atBeam = Math.Abs(f.ZTop - L.LandingBeamZ) < 1e-6 || Math.Abs(f.ZBottom - L.LandingBeamZ) < 1e-6
                              || (s.Type == StairType.ThreeFlightU && f.Index == 3 && Math.Abs(f.ZBottom - L.Landings[^1].Z) < 1e-6);
                if (atBeam) tot += load / 2;
            }
            foreach (var ld in L.Landings) tot += ql * ld.Area / 2;
            double sw = StairUnits.Concrete * s.LandingBeamB * Math.Max(0, s.LandingBeamH - L.LandingT) * set.GammaTT;
            double q = tot / Lb + sw;
            var pt = BeamPart(m, $"{nm} DCN", "Dầm chiếu nghỉ", Lb, Lb, q, s.LandingBeamB, s.LandingBeamH, stock, 1.0);
            pt.Story = L.TopStory;
            pt.Messages.Add($"Tải quy đổi phân bố đều: phản lực bản thang + ½ chiếu nghỉ = {tot:0.00} kN trên {Lb:0.00} m, bản thân {sw:0.00} kN/m.");
            d.Parts.Add(pt);
        }

        foreach (var pt in d.Parts)
        {
            RebarNumbering.Renumber(pt.Bars);
            d.Checks.AddRange(pt.Checks);
            d.Messages.AddRange(pt.Messages.Where(x => x.StartsWith("LỖI") || x.StartsWith("Thép")));
        }
        return d;
    }

    /// <summary>Gộp các bộ phận giống hệt (cùng bố trí thép, cùng nhịp) thành một cấu kiện có SL &gt; 1.</summary>
    private static List<StairPart> Merge(List<StairPart> parts, string nm, string code)
    {
        var res = new List<StairPart>();
        foreach (var pt in parts)
        {
            RebarNumbering.Renumber(pt.Bars);
            string sig = RebarNumbering.Signature(pt.Bars) + "|" + Math.Round(pt.Span * 1000) + "|" + Math.Round(pt.Q * 100);
            var same = res.FirstOrDefault(x => (RebarNumbering.Signature(x.Bars) + "|" + Math.Round(x.Span * 1000) + "|" + Math.Round(x.Q * 100)) == sig);
            if (same != null)
            {
                same.Quantity += pt.Quantity;
                same.Flights.AddRange(pt.Flights);
            }
            else res.Add(pt);
        }
        foreach (var pt in res.Where(x => x.Flights.Count > 1))
            pt.Name = $"{nm} {code}{string.Join(",", pt.Flights)}";
        return res;
    }

    // ==================================================================
    // Bản (dải 1 m)
    // ==================================================================
    /// <param name="span">nhịp tính toán L0 (m)</param>
    /// <param name="q">tải tính toán (kN/m² – theo mặt bằng với bản chịu lực)</param>
    /// <param name="lenMain">chiều dài phần bản theo phương thép chịu lực (m, chưa kể neo)</param>
    /// <param name="widthAcross">kích thước bản theo phương rải thép chịu lực (m)</param>
    /// <param name="lenDist">chiều dài thanh thép rải (m)</param>
    private static StairPart SlabPart(Mats m, string name, string kind, double span, double q, double t, bool partial, string note,
        double lenMain, double widthAcross, double lenDist, double anchorMain, double stock, bool mainAlong, double hookAtSupport, double cosA = 1)
    {
        double c = m.CoverSlab;
        var pt = new StairPart
        {
            Name = name, Kind = kind, Span = span, B = 1.0, H = t, Q = q, Width = widthAcross, MainAlongFlight = mainAlong, CosA = cosA,
            Quantity = 1
        };
        double kM = partial ? 10 : 8, kS = partial ? 16 : 24;
        pt.M = q * span * span / kM;
        pt.MSupport = q * span * span / kS;
        pt.V = q * span / 2;
        pt.MomentFormula = $"M = q·L0²/{kM:0} = {q:0.00}×{span:0.000}²/{kM:0}" + (string.IsNullOrEmpty(note) ? "" : $" ({note})");

        double H0(int dia) => t - c - dia / 2000.0;
        double AsFor(double M, int dia) => RcSection.DesignRect(M, 1.0, H0(dia), 0.02, m.Slab, m.Steel(dia), MinRatioSlab).As;
        var main = RcSection.ChooseSpacing(dia => AsFor(pt.M, dia), m.SlabDias, 0.20, 0.10, 0.15, 8);
        var top = RcSection.ChooseSpacing(dia => AsFor(pt.MSupport, dia), m.SlabDias, 0.20, 0.10, 0.15, 8);
        pt.H0 = H0(main.Dia);
        pt.AsReq = main.AsReqPerM; pt.AsProv = main.AsPerM; pt.MainText = main.Text;
        pt.AsTopReq = top.AsReqPerM; pt.AsTopProv = top.AsPerM; pt.TopText = top.Text;
        // thép phân bố ≥ 15% thép chịu lực, bước ≤ 250
        int dd = 6; double sd = 0.25;
        while (RcSection.BarArea(dd) / sd < 0.15 * main.AsPerM && sd > 0.151) sd -= 0.05;
        if (RcSection.BarArea(dd) / sd < 0.15 * main.AsPerM) { dd = 8; sd = 0.20; }
        sd = Math.Round(sd, 2);
        pt.DistText = $"Ø{dd}a{sd * 1000:0}";

        double mu = RcSection.MomentCapacity(1.0, pt.H0, main.AsPerM, 0, 0.02, m.Slab, m.Steel(main.Dia));
        double muTop = RcSection.MomentCapacity(1.0, H0(top.Dia), top.AsPerM, 0, 0.02, m.Slab, m.Steel(top.Dia));
        double qb = RcSection.QbConcrete(1.0, pt.H0, m.Slab);
        pt.Checks.Add(new CheckItem { Name = $"{name}: uốn nhịp", Demand = pt.M, Capacity = mu, Unit = "kN.m/m", Note = main.Text });
        pt.Checks.Add(new CheckItem { Name = $"{name}: uốn gối (thép mũ)", Demand = pt.MSupport, Capacity = muTop, Unit = "kN.m/m", Note = top.Text });
        pt.Checks.Add(new CheckItem { Name = $"{name}: cắt (bê tông chịu)", Demand = pt.V, Capacity = qb, Unit = "kN/m", Note = "Qb = 1,5·Rbt·b·h0²/c, c = 2h0" });
        if (!main.Ok) pt.Messages.Add($"LỖI {name}: không chọn được thép chịu lực với bước ≥ 100 mm – tăng chiều dày bản.");

        // ---- thanh thép ----
        double lmain = DesignUtil.R10(lenMain + 2 * anchorMain);
        int nMain = Math.Max(2, (int)Math.Floor((widthAcross - 2 * c) / main.Spacing + 1e-9) + 1);
        if (lmain > stock)
            pt.Messages.Add($"Thép {name}: thanh chịu lực dài {lmain:0.00} m > cây {stock:0.0} m – nối chồng tại vùng gối chiếu nghỉ, không nối trong nhịp.");
        pt.Bars.Add(new BarMark { Role = "Dưới", Dia = main.Dia, Count = nMain, Length = lmain, Shape = "Thẳng", Segments = { lmain * 1000 }, Spacing = main.Spacing, Start = -anchorMain, End = lenMain + anchorMain });
        // mũ gối 2 đầu: đoạn L0/4 (đo theo phương thanh) + neo vào gối, móc xuống (t − 2c)
        double lm = (mainAlong ? span / 4 / Math.Max(cosA, 0.3) : span / 4) + Math.Max(0.15, hookAtSupport);
        double hook = Math.Max(0.05, t - 2 * c);
        double ltop = DesignUtil.R10(lm + hook);
        int nTop = Math.Max(2, (int)Math.Floor((widthAcross - 2 * c) / top.Spacing + 1e-9) + 1);
        var segs = new List<double> { Math.Round(hook * 1000), Math.Round(lm * 1000) };
        pt.Bars.Add(new BarMark { Role = "Mũ gối", Dia = top.Dia, Count = nTop, Length = ltop, Shape = "L", Segments = segs.ToList(), Spacing = top.Spacing, Start = 0, End = lm, IsTop = true });
        pt.Bars.Add(new BarMark { Role = "Mũ gối", Dia = top.Dia, Count = nTop, Length = ltop, Shape = "L", Segments = segs.ToList(), Spacing = top.Spacing, Start = lenMain - lm, End = lenMain, IsTop = true });
        // thép rải dưới (vuông góc thép chịu lực) và rải trên dưới mũ
        double ld = DesignUtil.R10(Math.Max(0.2, lenDist));
        int nDist = Math.Max(2, (int)Math.Floor((lenMain - 0.10) / sd + 1e-9) + 1);
        pt.Bars.Add(new BarMark { Role = "Thép rải dưới", Dia = dd, Count = nDist, Length = ld, Shape = "Thẳng", Segments = { ld * 1000 }, Spacing = sd });
        int nDt = Math.Max(2, (int)Math.Floor(lm / sd + 1e-9) + 1);
        pt.Bars.Add(new BarMark { Role = "Thép rải trên", Dia = dd, Count = nDt, Length = ld, Shape = "Thẳng", Segments = { ld * 1000 }, Spacing = sd, IsTop = true, Start = 0, End = lm });
        pt.Bars.Add(new BarMark { Role = "Thép rải trên", Dia = dd, Count = nDt, Length = ld, Shape = "Thẳng", Segments = { ld * 1000 }, Spacing = sd, IsTop = true, Start = lenMain - lm, End = lenMain });
        return pt;
    }

    // ==================================================================
    // Dầm (dầm chiếu nghỉ, dầm cốn) – dầm đơn giản tải đều
    // ==================================================================
    /// <param name="span">nhịp tính toán (hình chiếu bằng với dầm cốn)</param>
    /// <param name="length">chiều dài thật của dầm (nghiêng với dầm cốn)</param>
    /// <param name="q">tải tính toán kN/m (theo hình chiếu bằng)</param>
    private static StairPart BeamPart(Mats m, string name, string kind, double span, double length, double q, double b, double h, double stock, double cosA)
    {
        double c = m.CoverBeam;
        var pt = new StairPart { Name = name, Kind = kind, Span = span, Length = length, B = b, H = h, Q = q, Width = b, Quantity = 1, CosA = cosA };
        pt.M = q * span * span / 8;
        pt.V = q * span / 2;
        pt.MomentFormula = $"M = q·L²/8 = {q:0.00}×{span:0.000}²/8";
        var stirStl = m.Small;
        double a = c + 0.008;
        double h0 = h - a;
        pt.H0 = h0;
        var fx = RcSection.DesignRect(pt.M, b, h0, a, m.Beam, m.Main, m.MinBeam);
        var bot = RcSection.ChooseBars(fx.As, b, c, m.BeamDias, 2, 1);
        double topReq = Math.Max(0.3 * fx.As, 2 * RcSection.BarArea(12));
        var top = RcSection.ChooseBars(Math.Max(topReq, fx.AsComp), b, c, m.BeamDias, 2, 1);
        pt.AsReq = fx.As; pt.AsProv = bot.Area; pt.MainText = bot.Text;
        pt.AsTopReq = Math.Max(fx.AsComp, 0); pt.AsTopProv = top.Area; pt.TopText = top.Text;
        int sd = 6;
        var sh = RcSection.Shear(pt.V, b, h0, m.Beam, stirStl, sd, 2);
        if (!sh.Ok) { sd = 8; sh = RcSection.Shear(pt.V, b, h0, m.Beam, stirStl, sd, 2); }
        double ss = Math.Min(sh.SSupport, 0.20);
        pt.StirrupText = $"Ø{sd}a{ss * 1000:0}";
        double mu = RcSection.MomentCapacity(b, h0, bot.Area, top.Area, a, m.Beam, m.Main);
        double qu = RcSection.ShearCapacity(b, h0, m.Beam, stirStl, sd, 2, ss);
        pt.Checks.Add(new CheckItem { Name = $"{name}: uốn nhịp", Demand = pt.M, Capacity = mu, Unit = "kN.m", Note = bot.Text });
        pt.Checks.Add(new CheckItem { Name = $"{name}: cắt", Demand = pt.V, Capacity = Math.Max(qu, sh.Qb), Unit = "kN", Note = pt.StirrupText });
        if (!bot.Ok) pt.Messages.Add($"LỖI {name}: không bố trí đủ thép dọc trong bề rộng {b * 1000:0} mm – tăng tiết diện.");
        if (!string.IsNullOrEmpty(fx.Note)) pt.Messages.Add($"{name}: {fx.Note}");

        // ---- thanh thép ----
        int dmax = Math.Max(bot.MaxDia, top.MaxDia);
        double anch = Math.Max(0.20, 15 * dmax / 1000.0);
        double lb = DesignUtil.R10(length + 2 * anch);
        if (lb > stock) pt.Messages.Add($"Thép {name}: thanh dài {lb:0.00} m > cây {stock:0.0} m – nối chồng tại gối.");
        foreach (var (dia, cnt) in bot.Groups)
        {
            double l = DesignUtil.R10(length + 2 * Math.Max(0.20, 15 * dia / 1000.0));
            pt.Bars.Add(new BarMark { Role = "Dưới", Dia = dia, Count = cnt, Length = l, Shape = "Thẳng", Segments = { l * 1000 }, Start = -anch, End = length + anch });
        }
        foreach (var (dia, cnt) in top.Groups)
        {
            double l = DesignUtil.R10(length + 2 * Math.Max(0.20, 15 * dia / 1000.0));
            pt.Bars.Add(new BarMark { Role = "Trên", Dia = dia, Count = cnt, Length = l, Shape = "Thẳng", Segments = { l * 1000 }, Start = -anch, End = length + anch, IsTop = true });
        }
        double s1 = b - 2 * c, s2 = h - 2 * c;
        int ns = Math.Max(2, (int)Math.Floor((length - 0.10) / ss + 1e-9) + 1);
        pt.Bars.Add(new BarMark
        {
            Role = "Đai", Dia = sd, Count = ns, Length = DesignUtil.StirrupLength(s1, s2, sd), Shape = "Đai",
            Segments = { Math.Round(s1 * 1000), Math.Round(s2 * 1000) }, Spacing = ss, Start = 0.05, End = length - 0.05
        });
        return pt;
    }
}
