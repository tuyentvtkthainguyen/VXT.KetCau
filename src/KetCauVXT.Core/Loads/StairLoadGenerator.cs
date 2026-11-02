using KetCauVXT.Import;
using KetCauVXT.Model;

namespace KetCauVXT.Loads;

/// <summary>Một lớp tải trên 1 m² mặt bằng của vế thang / chiếu nghỉ.</summary>
public class StairLoadRow
{
    public string Name { get; set; } = "";
    /// <summary>Chiều dày lớp (m); 0 = không áp dụng</summary>
    public double Thickness { get; set; }
    /// <summary>Trọng lượng riêng (kN/m³)</summary>
    public double Gamma { get; set; }
    /// <summary>Hệ số quy đổi về mặt bằng (1/cosα, (b+h)/b, ½·h…) – diễn giải</summary>
    public string Factor { get; set; } = "";
    /// <summary>Tải tiêu chuẩn (kN/m² mặt bằng)</summary>
    public double Gk { get; set; }
    /// <summary>Hệ số độ tin cậy γf</summary>
    public double Gf { get; set; } = 1;
    /// <summary>Trường hợp tải đưa vào mô hình khung: TT, HTHT, HT</summary>
    public string Pattern { get; set; } = "HTHT";
    public double G => Gk * Gf;
}

/// <summary>Tải trọng một cầu thang (kN/m² mặt bằng).</summary>
public class StairLoadSet
{
    public List<StairLoadRow> Flight { get; } = new();
    public List<StairLoadRow> Landing { get; } = new();
    public double FlightStd => Flight.Sum(r => r.Gk);
    public double FlightDesign => Flight.Sum(r => r.G);
    public double LandingStd => Landing.Sum(r => r.Gk);
    public double LandingDesign => Landing.Sum(r => r.G);
    public double FlightOf(string pattern) => Flight.Where(r => r.Pattern == pattern).Sum(r => r.Gk);
    public double LandingOf(string pattern) => Landing.Where(r => r.Pattern == pattern).Sum(r => r.Gk);
    /// <summary>Hệ số γf theo trường hợp tải (TT, HTHT, HT)</summary>
    public double GammaTT = 1.1, GammaSD = 1.3, GammaLive = 1.2;
}

/// <summary>Tính tải cầu thang theo TCVN 2737:2023:
/// <list type="bullet">
/// <item>Bản thang BTCT: γ = 25 kN/m³ × t / cosα (quy về mặt bằng).</item>
/// <item>Bậc xây gạch γ = 18 (đổ BTCT γ = 25) × h/2 (tam giác bậc quy đổi).</item>
/// <item>Lớp ốp mặt bậc + cổ bậc: granite γ ≈ 28, gỗ ≈ 7, gạch ốp ≈ 20 kN/m³ × t × (b + h)/b; vữa lót γ = 18.</item>
/// <item>Trát mặt dưới bản γ = 18 × t / cosα; lan can quy đổi q/bề rộng vế.</item>
/// <item>Hoạt tải cầu thang 3,0 kN/m² (TCVN 2737:2023 Bảng 4), γf lấy theo trường hợp tải HT của dự án.</item>
/// </list></summary>
public static class StairLoadCalc
{
    public static StairLoadSet Compute(Project p, StairLayout L)
    {
        var s = L.S;
        var set = new StairLoadSet
        {
            GammaTT = p?.Pattern("TT")?.Gamma ?? 1.1,
            GammaSD = p?.Pattern("HTHT")?.Gamma ?? 1.3,
            GammaLive = p?.Pattern("HT")?.Gamma ?? 1.2
        };
        if (set.GammaTT <= 0) set.GammaTT = 1.1;
        if (set.GammaSD <= 0) set.GammaSD = 1.3;
        if (set.GammaLive <= 0) set.GammaLive = 1.2;
        double h = L.Riser, b = Math.Max(L.Tread, 1e-3);
        double cosA = L.Flights.Count > 0 ? L.Flights[0].CosA : 1;
        if (cosA <= 0) cosA = 1;
        double kFin = (b + h) / b;
        string fa = $"1/cosα = {1 / cosA:0.000}", fk = $"(b+h)/b = {kFin:0.000}";
        StairLoadRow R(string n, double t, double g, string f, double gk, double gf, string pat) =>
            new() { Name = n, Thickness = t, Gamma = g, Factor = f, Gk = Math.Round(gk, 4), Gf = gf, Pattern = pat };

        // ---- vế thang ----
        set.Flight.Add(R("Bản thang BTCT", L.WaistT, StairUnits.Concrete, fa, StairUnits.Concrete * L.WaistT / cosA, set.GammaTT, "TT"));
        if (s.Steps == StepConstruction.Concrete)
            set.Flight.Add(R("Bậc thang BTCT", h, StairUnits.Concrete, "h/2", StairUnits.Concrete * h / 2, set.GammaTT, "TT"));
        else
            set.Flight.Add(R("Bậc thang xây gạch", h, StairUnits.Brick, "h/2", StairUnits.Brick * h / 2, set.GammaSD, "HTHT"));
        if (s.Finish != StepFinish.None)
        {
            set.Flight.Add(R("Ốp " + Stair.FinishName(s.Finish).ToLowerInvariant() + " mặt bậc, cổ bậc", s.FinishT, Stair.FinishUnitWeight(s.Finish), fk,
                Stair.FinishUnitWeight(s.Finish) * s.FinishT * kFin, set.GammaSD, "HTHT"));
            if (s.BeddingThickness > 0)
                set.Flight.Add(R("Vữa lót", s.BeddingThickness, StairUnits.Mortar, fk, StairUnits.Mortar * s.BeddingThickness * kFin, set.GammaSD, "HTHT"));
        }
        if (s.PlasterThickness > 0)
            set.Flight.Add(R("Vữa trát mặt dưới bản", s.PlasterThickness, StairUnits.Mortar, fa, StairUnits.Mortar * s.PlasterThickness / cosA, set.GammaSD, "HTHT"));
        if (s.RailingQ > 0 && L.FlightWidth > 0)
            set.Flight.Add(R($"Lan can {s.RailingQ:0.##} kN/m quy đổi", 0, 0, $"q/w = 1/{L.FlightWidth:0.00}", s.RailingQ / L.FlightWidth, set.GammaSD, "HTHT"));
        double live = s.LiveLoad > 0 ? s.LiveLoad : StairUnits.LiveDefault;
        set.Flight.Add(R("Hoạt tải cầu thang (TCVN 2737:2023)", 0, 0, "", live, set.GammaLive, "HT"));

        // ---- chiếu nghỉ ----
        if (L.Landings.Count > 0)
        {
            set.Landing.Add(R("Bản chiếu nghỉ BTCT", L.LandingT, StairUnits.Concrete, "", StairUnits.Concrete * L.LandingT, set.GammaTT, "TT"));
            if (s.Finish != StepFinish.None)
            {
                set.Landing.Add(R("Ốp " + Stair.FinishName(s.Finish).ToLowerInvariant(), s.FinishT, Stair.FinishUnitWeight(s.Finish), "", Stair.FinishUnitWeight(s.Finish) * s.FinishT, set.GammaSD, "HTHT"));
                if (s.BeddingThickness > 0)
                    set.Landing.Add(R("Vữa lót", s.BeddingThickness, StairUnits.Mortar, "", StairUnits.Mortar * s.BeddingThickness, set.GammaSD, "HTHT"));
            }
            if (s.PlasterThickness > 0)
                set.Landing.Add(R("Vữa trát trần", s.PlasterThickness, StairUnits.Mortar, "", StairUnits.Mortar * s.PlasterThickness, set.GammaSD, "HTHT"));
            set.Landing.Add(R("Hoạt tải cầu thang (TCVN 2737:2023)", 0, 0, "", live, set.GammaLive, "HT"));
        }
        return set;
    }
}

/// <summary>Truyền tải cầu thang khai báo (Project.Stairs) vào mô hình khung mỗi lần phân tích (Source = "STAIR"):
/// <list type="bullet">
/// <item>Bản thang chịu lực: phản lực đầu vế g·L0/2 (kN/m, L0 = hình chiếu bằng vế) → tải phân bố trên dầm sàn tầng dưới / tầng trên
///   tại mép vế, đầu vế tại chiếu nghỉ → dầm chiếu nghỉ.</item>
/// <item>Chiếu nghỉ: nửa tải về dầm chiếu nghỉ, nửa về tường/dầm đầu ô (dầm tầng dưới cùng đường trục).</item>
/// <item>Dầm chiếu nghỉ: phản lực hai đầu (tải tập trung) → dầm tầng dưới tại đầu dầm (qua cột trốn / tường); không có → dầm tầng trên (dầm treo).</item>
/// <item>Dầm cốn: phản lực tập trung tại đầu dầm cốn; 1 dầm cốn → nửa bản về tường bên (dầm tầng dưới theo mép ngoài vế).</item>
/// <item>Không có dầm đỡ → tải phân bố đều lên ô sàn chứa điểm đặt; chân thang tầng trệt tựa lên móng/nền → không đưa vào khung.</item>
/// </list>
/// Lỗ sàn có cầu thang khai báo: tải ước tính cũ của lỗ sàn (OPEN{id}, ARCH "Cầu thang") được bỏ (StairEdit.RemoveLegacyLoads).
/// Không có cầu thang khai báo → giữ nguyên cách tính cũ.</summary>
public static class StairLoadGenerator
{
    public const string Source = "STAIR";
    private static readonly string[] Pats = { "TT", "HTHT", "HT" };

    public static List<string> Apply(Project p)
    {
        var warn = new List<string>();
        p.FrameLoads.RemoveAll(l => l.Source == Source);
        p.AreaLoads.RemoveAll(a => a.Source == Source);
        if (p.Stairs == null || p.Stairs.Count == 0) return warn;
        ImportGeometry.EnsurePatterns(p);
        foreach (var s in p.Stairs)
        {
            try
            {
                if (s.OpeningId > 0) StairEdit.RemoveLegacyLoads(p, s.OpeningId);
                var L = s.Layout(p);
                warn.AddRange(L.Warnings);
                if (!L.Valid) continue;
                var set = StairLoadCalc.Compute(p, L);
                foreach (var pat in Pats) Transfer(p, L, set.FlightOf(pat), set.LandingOf(pat), pat, warn);
            }
            catch (Exception ex) { warn.Add($"Cầu thang {s.Name}: lỗi truyền tải – {ex.Message}"); }
        }
        p.InvalidateIndex();
        return warn;
    }

    /// <summary>Tổng tải (kN) của một trường hợp tải truyền vào mô hình (để kiểm tra cân bằng).</summary>
    public static double TotalLoad(Project p, StairLayout L, string pattern)
    {
        var set = StairLoadCalc.Compute(p, L);
        double gf = set.FlightOf(pattern), gl = set.LandingOf(pattern);
        double t = L.Flights.Sum(f => gf * f.Run * f.Width) + L.Landings.Sum(ld => gl * ld.Area);
        if (pattern == "TT")
        {
            if (L.HasLandingBeam) t += StairUnits.Concrete * L.S.LandingBeamB * Math.Max(0, L.S.LandingBeamH - L.LandingT) * L.Wr;
            if (L.Stringers) t += L.Flights.Sum(f => L.StringerCount * StairUnits.Concrete * L.S.StringerB * Math.Max(0, L.S.StringerH - L.WaistT) * f.Inclined);
        }
        return t;
    }

    // ------------------------------------------------------------------
    private sealed class Ctx
    {
        public Project P; public StairLayout L; public string Pat; public List<string> Warn; public string Note;
        public double LandingBeamLoad;    // kN tổng lên dầm chiếu nghỉ
        public bool BaseWarned;
    }

    private static void Transfer(Project p, StairLayout L, double gf, double gl, string pat, List<string> warn)
    {
        if (gf <= 0 && gl <= 0 && pat != "TT") return;
        var c = new Ctx { P = p, L = L, Pat = pat, Warn = warn, Note = $"Cầu thang {L.S.Name}" };
        var s = L.S;
        double far = L.Lr;
        bool bent = !L.HasLandingBeam && L.Landings.Count > 0 && s.Type == StairType.TwoFlightU;
        foreach (var f in L.Flights)
        {
            double load = gf * f.Run * f.Width;       // kN của vế
            if (f.DV != 0)
            {
                // vế giữa thang 3 vế: tựa lên 2 chiếu nghỉ góc → dồn vào vùng chiếu nghỉ (nửa dầm chiếu nghỉ, nửa tường đầu ô)
                c.LandingBeamLoad += load / 2;
                FarEdge(c, load / 2);
                continue;
            }
            double uB = f.U0, uT = f.U0 + f.DU * f.Run;
            if (bent && Math.Abs(f.ZTop - L.Landings[0].Z) < 1e-6 || bent && Math.Abs(f.ZBottom - L.Landings[0].Z) < 1e-6)
            {
                // bản gãy khúc vế + chiếu nghỉ (theo dải bề rộng vế) nhịp từ mép sàn tới tường đầu ô
                var ld = L.Landings[0];
                double run = f.Run, ldd = ld.Span, L0 = run + ldd;
                double wl = gl * ldd, wf = gf * run;   // kN/m theo bề rộng
                double rFar = (wf * run / 2 + wl * (run + ldd / 2)) / L0;
                double rNear = wf + wl - rFar;
                double uNear = Math.Abs(f.ZTop - ld.Z) < 1e-6 ? uB : uT;
                double zNear = Math.Abs(f.ZTop - ld.Z) < 1e-6 ? f.ZBottom : f.ZTop;
                EndLine(c, uNear, f.Vmin, f.Vmax, zNear, rNear);
                FarEdgeStrip(c, f.Vmin, f.Vmax, rFar);
                continue;
            }
            if (L.Stringers)
            {
                int ns = L.StringerCount;
                double sw = pat == "TT" ? StairUnits.Concrete * s.StringerB * Math.Max(0, s.StringerH - L.WaistT) * f.Inclined : 0;
                double onStr = ns >= 2 ? load : load / 2;           // 1 dầm cốn: nửa bản về tường bên
                double each = onStr / ns / 2 + sw / 2;              // mỗi đầu mỗi dầm cốn
                var vs = ns >= 2 ? new[] { f.Vmin + s.StringerB / 2, f.Vmax - s.StringerB / 2 } : new[] { InnerV(L, f) };
                foreach (double v in vs)
                {
                    EndPoint(c, uB, v, f.ZBottom, each);
                    EndPoint(c, uT, v, f.ZTop, each);
                }
                if (ns < 2 && load > 0)
                {
                    // nửa bản tựa tường bên (mép ngoài vế) → dầm tầng dưới dọc mép ngoài
                    double vOut = OuterV(L, f);
                    var (x1, y1) = L.ToXY(f.Umin, vOut); var (x2, y2) = L.ToXY(f.Umax, vOut);
                    Line(c, LowerZ(L), L.BottomStory, x1, y1, x2, y2, load / 2 / Math.Max(f.Run, 0.1), "tường bên vế " + f.Index, true);
                }
                continue;
            }
            double r = gf * f.Run / 2;   // kN/m
            EndLine(c, uB, f.Vmin, f.Vmax, f.ZBottom, r);
            EndLine(c, uT, f.Vmin, f.Vmax, f.ZTop, r);
        }
        // ---- chiếu nghỉ ----
        if (!bent)
            foreach (var ld in L.Landings)
            {
                double load = gl * ld.Area;
                if (L.HasLandingBeam) { c.LandingBeamLoad += load / 2; FarEdge(c, load / 2, ld.Vmin, ld.Vmax); }
                else FarEdge(c, load, ld.Vmin, ld.Vmax);
            }
        else
        {
            // phần chiếu nghỉ ngoài dải vế (khe thang) → tường đầu ô
            var ld = L.Landings[0];
            double covered = L.Flights.Where(f => f.DU != 0).Sum(f => f.Width);
            double rest = Math.Max(0, ld.Width - covered);
            if (rest > 1e-3) FarEdge(c, gl * ld.Span * rest);
        }
        // ---- dầm chiếu nghỉ → 2 đầu ----
        if (L.HasLandingBeam)
        {
            if (pat == "TT") c.LandingBeamLoad += StairUnits.Concrete * s.LandingBeamB * Math.Max(0, s.LandingBeamH - L.LandingT) * L.Wr;
            if (c.LandingBeamLoad > 1e-9)
                foreach (double v in new[] { 0.0, L.Wr })
                {
                    var (x, y) = L.ToXY(L.LandingBeamU, v);
                    PointLower(c, x, y, c.LandingBeamLoad / 2, "đầu dầm chiếu nghỉ");
                }
        }
    }

    private static double LowerZ(StairLayout L) => L.ZBot;

    /// <summary>Mép trong (phía khe thang) của vế.</summary>
    private static double InnerV(StairLayout L, StairFlight f) =>
        f.Vmin < 1e-6 ? f.Vmax - L.S.StringerB / 2 : f.Vmin + L.S.StringerB / 2;

    private static double OuterV(StairLayout L, StairFlight f) => f.Vmin < 1e-6 ? 0 : L.Wr;

    /// <summary>Đầu vế tại u: tải q (kN/m) dọc v ∈ [v0, v1] ở cao độ z (sàn dưới / sàn trên / chiếu nghỉ).</summary>
    private static void EndLine(Ctx c, double u, double v0, double v1, double z, double q)
    {
        if (Math.Abs(q) < 1e-12) return;
        var L = c.L;
        if (Math.Abs(z - L.ZTop) < 1e-6)
        {
            var (x1, y1) = L.ToXY(u, v0); var (x2, y2) = L.ToXY(u, v1);
            Line(c, L.ZTop, L.TopStory, x1, y1, x2, y2, q, "đầu vế trên", false);
        }
        else if (Math.Abs(z - L.ZBot) < 1e-6)
        {
            var (x1, y1) = L.ToXY(u, v0); var (x2, y2) = L.ToXY(u, v1);
            Line(c, L.ZBot, L.BottomStory, x1, y1, x2, y2, q, "chân thang", true);
        }
        else if (L.HasLandingBeam) c.LandingBeamLoad += q * (v1 - v0);
        else FarEdge(c, q * (v1 - v0), v0, v1);
    }

    private static void EndPoint(Ctx c, double u, double v, double z, double P)
    {
        if (Math.Abs(P) < 1e-12) return;
        var L = c.L;
        var (x, y) = L.ToXY(u, v);
        if (Math.Abs(z - L.ZTop) < 1e-6) Point(c, L.ZTop, L.TopStory, x, y, P, "đầu dầm cốn", null);
        else if (Math.Abs(z - L.ZBot) < 1e-6) Point(c, L.ZBot, L.BottomStory, x, y, P, "chân dầm cốn", null, true);
        else if (L.HasLandingBeam) c.LandingBeamLoad += P;
        else FarEdge(c, P);
    }

    /// <summary>Tải tổng P (kN) lên tường/dầm đầu ô (u = Lr) trên đoạn v ∈ [v0, v1] (mặc định toàn bề rộng).</summary>
    private static void FarEdge(Ctx c, double P, double v0 = 0, double v1 = double.NaN)
    {
        var L = c.L;
        if (double.IsNaN(v1)) v1 = L.Wr;
        FarEdgeStrip(c, v0, v1, P / Math.Max(v1 - v0, 0.1));
    }

    private static void FarEdgeStrip(Ctx c, double v0, double v1, double q)
    {
        if (Math.Abs(q) < 1e-12) return;
        var L = c.L;
        var (x1, y1) = L.ToXY(L.Lr, v0); var (x2, y2) = L.ToXY(L.Lr, v1);
        if (!Line(c, L.ZBot, L.BottomStory, x1, y1, x2, y2, q, "tường đầu ô (chiếu nghỉ)", true, quiet: true))
            Line(c, L.ZTop, L.TopStory, x1, y1, x2, y2, q, "đầu ô (giả thiết chiếu nghỉ treo vào dầm tầng trên)", false);
    }

    private static void PointLower(Ctx c, double x, double y, double P, string what)
    {
        var L = c.L;
        if (!Point(c, L.ZBot, L.BottomStory, x, y, P, what + " (qua cột trốn / tường)", null, true, quiet: true))
            Point(c, L.ZTop, L.TopStory, x, y, P, what + " (giả thiết treo vào dầm tầng trên)", null);
    }

    /// <summary>Tải phân bố q (kN/m) trên đoạn (x1,y1)–(x2,y2) cao độ z: phần có dầm → tải dầm, còn lại → ô sàn; trả về false nếu không có chỗ đặt.</summary>
    private static bool Line(Ctx c, double z, string story, double x1, double y1, double x2, double y2, double q, string what, bool lower, bool quiet = false)
    {
        var p = c.P;
        var wl = new WallLine { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Story = story };
        double len = wl.Length;
        if (len < 1e-6) return false;
        var beams = WallLoadGenerator.BeamsUnder(p, wl, z, 0.25);
        double covered = 0;
        foreach (var (f, r0, r1, l) in beams)
        {
            var fl = new FrameLoad { Element = f.Id, Pattern = c.Pat, Dir = LoadDir.Gravity, Note = $"{c.Note}: {what}", Source = Source };
            fl.Points.Add(new[] { r0, q }); fl.Points.Add(new[] { r1, q });
            p.FrameLoads.Add(fl);
            covered += l;
        }
        double rest = len - covered;
        if (rest <= 0.02) return true;
        double P = q * rest;
        if (ToSlab(c, story, (x1 + x2) / 2, (y1 + y2) / 2, P, what)) return true;
        if (covered > 0) return true;
        if (quiet) return false;
        Unplaced(c, story, lower, what, P);
        return false;
    }

    private static bool Point(Ctx c, double z, string story, double x, double y, double P, string what, string _, bool lower = false, bool quiet = false)
    {
        var p = c.P;
        FrameElement best = null; double bd = 0.25, bt = 0;
        foreach (var f in p.Frames)
        {
            if (f.IsVerticalKind || f.IsCurved) continue;
            var a = p.Node(f.NodeI); var b = p.Node(f.NodeJ);
            if (Math.Abs(a.Z - z) > 0.01 || Math.Abs(b.Z - z) > 0.01) continue;
            double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
            if (l2 < 1e-9) continue;
            double t = Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / l2, 0, 1);
            double ex = a.X + t * dx - x, ey = a.Y + t * dy - y, d = Math.Sqrt(ex * ex + ey * ey);
            if (d < bd) { bd = d; best = f; bt = t; }
        }
        if (best != null)
        {
            var fl = new FrameLoad { Element = best.Id, Pattern = c.Pat, Dir = LoadDir.Gravity, Note = $"{c.Note}: {what}", Source = Source };
            fl.PointLoads.Add(new[] { bt, P });
            p.FrameLoads.Add(fl);
            return true;
        }
        if (ToSlab(c, story, x, y, P, what)) return true;
        if (quiet) return false;
        Unplaced(c, story, lower, what, P);
        return false;
    }

    private static bool ToSlab(Ctx c, string story, double x, double y, double P, string what)
    {
        if (string.IsNullOrEmpty(story)) return false;
        var sl = ModelEdit.SlabAt(c.P, story, x, y);
        if (sl == null) return false;
        var pts = sl.Nodes.Select(id => c.P.Node(id)).Select(n => (n.X, n.Y)).ToList();
        double a = Math.Abs(ImportGeometry.SignedArea(pts));
        if (a < 1e-6) return false;
        c.P.AreaLoads.Add(new AreaLoad { Slab = sl.Id, Pattern = c.Pat, Q = Math.Round(P / a, 5), Note = $"{c.Note}: {what} (không có dầm đỡ)", Source = Source });
        return true;
    }

    private static void Unplaced(Ctx c, string story, bool lower, string what, double P)
    {
        if (lower && string.IsNullOrEmpty(story))
        {
            if (!c.BaseWarned && c.Pat == "TT") c.Warn.Add($"{c.Note}: chân thang tựa lên móng / nền tầng trệt – tải phần này không đưa vào mô hình khung.");
            c.BaseWarned = true;
            return;
        }
        if (c.Pat == "TT" || P > 0.5) c.Warn.Add($"{c.Note}: không tìm thấy dầm / sàn đỡ ({what}, {c.Pat} {P:0.0} kN) – bỏ qua.");
    }
}
