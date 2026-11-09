using System.Globalization;
using System.Text.Json;
using KetCauVXT.Analysis;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Design.Foundation;

// =====================================================================
// [Truyền tải] TRUYỀN TẢI TRỌNG ĐỨNG XUỐNG CHÂN CỘT THEO DIỆN CHỊU TẢI (cách tính tay của kỹ sư, độc lập với mô hình phần tử hữu hạn)
//   • Tải lấy đúng như mô hình đã chất: TLBT sàn (chiều dày × γ), tải phân bố trên sàn (hoàn thiện, tường trên sàn, hoạt tải),
//     tải trên dầm (tường – tự sinh từ tường kiến trúc, cầu thang, tải nhập), tải tập trung tại nút, TLBT dầm và cột.
//   • Mỗi phần tải (lưới điểm 0,1 m trên sàn, điểm chia dọc dầm) dồn về CỘT GẦN NHẤT đỡ sàn đó (diện chịu tải dạng Voronoi);
//     cột đặt trên dầm (cột hụt) dồn xuống cột gần nhất ở cao độ dưới.
//   • Cộng dồn từ mái xuống chân cột: G (tĩnh tải), P (hoạt tải); giảm hoạt tải theo diện tích và số tầng
//     ψA1 = 0,4 + 0,6/√(A/A1) (A1 = 9 m²), ψn1 = 0,4 + (ψA1 − 0,4)/√n (TCVN 2737) – tắt được.
//   • Ntc = G + ψ·P (tiêu chuẩn), Ntt = Σ γf·tải (γf theo trường hợp tải; mặc định TT 1,1, tải hoàn thiện 1,2, HT 1,2).
//   • So sánh với phản lực chân cột của phân tích khung (nếu có) để phát hiện chất tải trùng / thiếu.
// Dùng cho gợi ý phương án móng (FoundationAdvisor) – tải đứng, không kể mô men chân cột (do giằng móng chịu).
// =====================================================================

/// <summary>Tuỳ chọn truyền tải.</summary>
public sealed class TakedownOptions
{
    /// <summary>Giảm hoạt tải theo diện tích chịu tải và số tầng (ψA1, ψn1)</summary>
    public bool LiveReduction { get; set; } = true;
    /// <summary>Diện tích A1 (m²) – nhà ở, phòng ở: 9 m²</summary>
    public double A1 { get; set; } = 9;
    /// <summary>Bước lưới điểm chia sàn (m)</summary>
    public double Step { get; set; } = 0.1;
}

/// <summary>Tải dồn về một chân cột (móng).</summary>
public sealed class ColumnTakedown
{
    public int Node { get; set; }
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Tiết diện cột chân (tên), kích thước theo X / Y (m)</summary>
    public string Section { get; set; } = "";
    public double Bx { get; set; }
    public double By { get; set; }
    /// <summary>Tĩnh tải tiêu chuẩn G (kN)</summary>
    public double G { get; set; }
    /// <summary>Hoạt tải tiêu chuẩn chưa giảm (kN)</summary>
    public double P { get; set; }
    /// <summary>Phần hoạt tải được giảm (sàn sử dụng, không gồm mái) (kN)</summary>
    public double PReducible { get; set; }
    public double Gtt { get; set; }
    public double Ptt { get; set; }
    public double PReducibleTt { get; set; }
    /// <summary>Hệ số giảm hoạt tải ψn1 (1 = không giảm)</summary>
    public double Psi { get; set; } = 1;
    /// <summary>Số sàn có hoạt tải trên cột (n)</summary>
    public int LiveFloors { get; set; }
    /// <summary>Diện chịu tải trung bình mỗi sàn (m²)</summary>
    public double AreaPerFloor { get; set; }
    /// <summary>Tổng diện chịu tải các sàn (m²)</summary>
    public double Area { get; set; }
    /// <summary>Diện chịu tải từng sàn (tên tầng → m²)</summary>
    public Dictionary<string, double> Floors { get; } = new();
    /// <summary>Tải tiêu chuẩn theo nguồn (kN)</summary>
    public Dictionary<string, double> Sources { get; } = new();
    public double PReduced => P - PReducible + Psi * PReducible;
    public double PReducedTt => Ptt - PReducibleTt + Psi * PReducibleTt;
    /// <summary>Lực nén tiêu chuẩn Ntc = G + ψ·P (kN)</summary>
    public double Ntc => G + PReduced;
    /// <summary>Lực nén tính toán Ntt (kN)</summary>
    public double Ntt => Gtt + PReducedTt;
    // ---- so sánh phân tích khung ----
    /// <summary>Phản lực đứng lớn nhất theo tổ hợp TTGH I của phân tích khung (kN); NaN = chưa phân tích</summary>
    public double FeNtt { get; set; } = double.NaN;
    /// <summary>Phản lực đứng do tĩnh tải + hoạt tải tiêu chuẩn của phân tích khung (kN)</summary>
    public double FeG { get; set; } = double.NaN;
    public double FeP { get; set; } = double.NaN;
    /// <summary>Mô men chân cột lớn nhất (kN.m) theo tổ hợp TTGH I của phân tích khung</summary>
    public double FeM { get; set; } = double.NaN;
}

/// <summary>Tổng hợp tải từng sàn.</summary>
public sealed class StoryTakedown
{
    public string Story { get; set; } = "";
    public double Elevation { get; set; }
    public double FloorArea { get; set; }
    public double G { get; set; }
    public double P { get; set; }
    public Dictionary<string, double> Sources { get; } = new();
    /// <summary>(G + P)/diện tích sàn (kN/m²)</summary>
    public double Intensity => FloorArea > 1e-6 ? (G + P) / FloorArea : double.NaN;
}

public sealed class TakedownResult
{
    public List<ColumnTakedown> Columns { get; } = new();
    public List<StoryTakedown> Stories { get; } = new();
    public List<string> Notes { get; } = new();
    public List<string> Warnings { get; } = new();
    public double TotalG => Columns.Sum(c => c.G);
    public double TotalP => Columns.Sum(c => c.P);
    public double TotalNtc => Columns.Sum(c => c.Ntc);
    public double TotalNtt => Columns.Sum(c => c.Ntt);
    public double FloorArea => Stories.Sum(s => s.FloorArea);
    /// <summary>Tổng tải đã chất trong mô hình (kiểm tra cân bằng: = Σ chân cột)</summary>
    public double AppliedG { get; set; }
    public double AppliedP { get; set; }
    public bool HasFe => Columns.Any(c => double.IsFinite(c.FeNtt));
    public TakedownOptions Options { get; set; } = new();
}

public static class LoadTakedown
{
    public const string SrcSlab = "Sàn – TLBT", SrcFinish = "Sàn – hoàn thiện, tải phụ", SrcLive = "Hoạt tải", SrcWall = "Tường",
        SrcStair = "Cầu thang", SrcBeam = "Dầm – TLBT", SrcCol = "Cột – TLBT", SrcBeamLoad = "Tải khác trên dầm", SrcNode = "Tải tập trung";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string F(double v, string f = "0.##") => double.IsFinite(v) ? v.ToString(f, Inv) : "-";

    private static bool IsG(LoadType t) => t is LoadType.Dead or LoadType.SuperDead;
    private static bool IsP(LoadType t) => t is LoadType.Live or LoadType.RoofLive;

    /// <summary>Hệ số độ tin cậy của trường hợp tải (γf khai báo &gt; 1, ngược lại mặc định).</summary>
    public static double GammaOf(LoadPattern p) =>
        p == null ? 1 : p.Gamma > 1.0001 ? p.Gamma : p.Type switch
        {
            LoadType.Dead => 1.1,
            LoadType.SuperDead => 1.2,
            LoadType.Live or LoadType.RoofLive => 1.2,
            _ => 1.0
        };

    private sealed class Acc
    {
        public double G, P, Pr, Gtt, Ptt, Prtt;
        public readonly Dictionary<string, double> Src = new();
        public readonly Dictionary<string, double> Floor = new();
        public readonly HashSet<string> LiveFloors = new();

        public void Add(LoadPattern pat, double v, string src)
        {
            if (pat == null || Math.Abs(v) < 1e-12) return;
            double g = GammaOf(pat);
            if (IsG(pat.Type)) { G += v; Gtt += v * g; }
            else if (IsP(pat.Type))
            {
                P += v; Ptt += v * g;
                if (pat.Type == LoadType.Live) { Pr += v; Prtt += v * g; }
            }
            else return;
            Src[src] = (Src.TryGetValue(src, out var o) ? o : 0) + v;
        }

        public void Merge(Acc o)
        {
            G += o.G; P += o.P; Pr += o.Pr; Gtt += o.Gtt; Ptt += o.Ptt; Prtt += o.Prtt;
            foreach (var kv in o.Src) Src[kv.Key] = (Src.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
            foreach (var kv in o.Floor) Floor[kv.Key] = (Floor.TryGetValue(kv.Key, out var v) ? v : 0) + kv.Value;
            foreach (var s in o.LiveFloors) LiveFloors.Add(s);
        }
    }

    private sealed class Col
    {
        public FrameElement F;
        public Node Bot, Top;
        public readonly Acc A = new();
    }

    /// <summary>Truyền tải. Không thay đổi dự án (tải tường / cầu thang tự sinh tính trên bản sao).</summary>
    public static TakedownResult Run(Project p0, AnalysisResults r = null, TakedownOptions opt = null)
    {
        opt ??= new TakedownOptions();
        var res = new TakedownResult { Options = opt };
        if (p0 == null) return res;
        // bản sao: sinh tải tường / cầu thang như khi phân tích, không đụng dự án đang mở
        Project p;
        try { p = JsonSerializer.Deserialize<Project>(JsonSerializer.Serialize(p0, Project.JsonOptions), Project.JsonOptions); }
        catch (Exception) { p = p0; }
        p.InvalidateIndex();
        try { res.Warnings.AddRange(WallLoadGenerator.Apply(p)); } catch (Exception ex) { res.Warnings.Add("Không sinh được tải tường: " + ex.Message); }
        try { res.Warnings.AddRange(StairLoadGenerator.Apply(p)); } catch (Exception ex) { res.Warnings.Add("Không sinh được tải cầu thang: " + ex.Message); }
        p.InvalidateIndex();

        var pats = p.Patterns.ToDictionary(x => x.Name, x => x);
        var cols = new List<Col>();
        foreach (var f in p.Frames.Where(f => f.IsVerticalKind))
        {
            Node a, b;
            try { a = p.Node(f.NodeI); b = p.Node(f.NodeJ); } catch (Exception) { continue; }
            cols.Add(a.Z <= b.Z ? new Col { F = f, Bot = a, Top = b } : new Col { F = f, Bot = b, Top = a });
        }
        if (cols.Count == 0) { res.Warnings.Add("Mô hình chưa có cột."); return res; }
        var groups = cols.GroupBy(c => Math.Round(c.Top.Z, 2)).OrderBy(g => g.Key).Select(g => (Z: g.Key, Cols: g.ToList())).ToList();

        List<Col> GroupFor(double z, double below = double.MaxValue)
        {
            var cand = groups.Where(g => g.Z <= z + 0.5 && g.Z < below - 0.05).ToList();
            if (cand.Count > 0) return cand[^1].Cols;
            var any = groups.Where(g => g.Z < below - 0.05).ToList();
            return any.Count > 0 ? any[0].Cols : null;
        }
        Col Nearest(List<Col> g, double x, double y)
        {
            Col best = null; double bd = double.MaxValue;
            foreach (var c in g)
            {
                double d = (c.Top.X - x) * (c.Top.X - x) + (c.Top.Y - y) * (c.Top.Y - y);
                if (d < bd - 1e-12) { bd = d; best = c; }
            }
            return best;
        }
        var stories = new Dictionary<string, StoryTakedown>();
        StoryTakedown St(string name, double z)
        {
            name = string.IsNullOrEmpty(name) ? "(không tên)" : name;
            if (!stories.TryGetValue(name, out var s)) stories[name] = s = new StoryTakedown { Story = name, Elevation = z };
            return s;
        }
        void StAdd(StoryTakedown s, LoadPattern pat, double v, string src)
        {
            if (pat == null) return;
            if (IsG(pat.Type)) s.G += v; else if (IsP(pat.Type)) s.P += v; else return;
            s.Sources[src] = (s.Sources.TryGetValue(src, out var o) ? o : 0) + v;
        }
        double appliedG = 0, appliedP = 0;
        void Applied(LoadPattern pat, double v) { if (pat == null) return; if (IsG(pat.Type)) appliedG += v; else if (IsP(pat.Type)) appliedP += v; }

        // ---- sàn: TLBT + tải phân bố ----
        foreach (var sl in p.Slabs)
        {
            List<Node> nodes;
            try { nodes = sl.Nodes.Select(id => p.Node(id)).ToList(); } catch (Exception) { continue; }
            if (nodes.Count < 3) continue;
            var poly = nodes.Select(n => (n.X, n.Y)).ToList();
            double area = PolyGeom.Properties(poly).Area;
            if (area < 1e-6) continue;
            double z = nodes.Average(n => n.Z);
            var g = GroupFor(z);
            if (g == null) continue;
            var st = St(sl.Story, z);
            st.FloorArea += area;
            double t = 0, gam = 25;
            try { var sec = p.SlabSection(sl.Section); t = sec.Thickness; gam = p.Material(sec.Material).UnitWeight; } catch (Exception) { }
            var loadsHere = new List<(LoadPattern Pat, double Q, string Src)>();
            foreach (var pat in p.Patterns)
            {
                if (pat.SelfWeight != 0 && t > 0) loadsHere.Add((pat, pat.SelfWeight * gam * t, SrcSlab));
                foreach (var al in p.AreaLoads.Where(a => a.Slab == sl.Id && a.Pattern == pat.Name))
                {
                    string src = IsP(pat.Type) ? SrcLive : al.Source == StairLoadGenerator.Source ? SrcStair
                        : al.Source == WallLoadGenerator.WallSource || (al.Note ?? "").IndexOf("tường", StringComparison.OrdinalIgnoreCase) >= 0 ? SrcWall : SrcFinish;
                    loadsHere.Add((pat, al.Q, src));
                }
            }
            if (loadsHere.Count == 0) continue;
            // lưới điểm
            double x0 = poly.Min(q => q.X), x1 = poly.Max(q => q.X), y0 = poly.Min(q => q.Y), y1 = poly.Max(q => q.Y);
            double h = Math.Max(0.02, Math.Min(opt.Step, Math.Min(x1 - x0, y1 - y0) / 4));
            var share = new Dictionary<Col, double>();
            int cnt = 0;
            for (double x = x0 + h / 2; x < x1; x += h)
                for (double y = y0 + h / 2; y < y1; y += h)
                {
                    if (!PolyGeom.Inside(poly, x, y)) continue;
                    var c = Nearest(g, x, y);
                    share[c] = (share.TryGetValue(c, out var v) ? v : 0) + 1;
                    cnt++;
                }
            if (cnt == 0) { var c = Nearest(g, poly.Average(q => q.X), poly.Average(q => q.Y)); share[c] = 1; cnt = 1; }
            bool hasLive = loadsHere.Any(l => l.Pat.Type == LoadType.Live && Math.Abs(l.Q) > 1e-9);
            foreach (var (c, k) in share)
            {
                double a = area * k / cnt;
                c.A.Floor[st.Story] = (c.A.Floor.TryGetValue(st.Story, out var o) ? o : 0) + a;
                if (hasLive) c.A.LiveFloors.Add(st.Story);
                foreach (var (pat, q, src) in loadsHere) c.A.Add(pat, q * a, src);
            }
            foreach (var (pat, q, src) in loadsHere) { StAdd(st, pat, q * area, src); Applied(pat, q * area); }
        }

        // ---- dầm: TLBT + tải trên dầm (trừ tải dồn từ sàn – đã tính theo diện tích) ----
        var horiz = p.Frames.Where(f => !f.IsVerticalKind).ToList();
        foreach (var f in horiz)
        {
            Node a, b;
            try { a = p.Node(f.NodeI); b = p.Node(f.NodeJ); } catch (Exception) { continue; }
            double L = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y) + (b.Z - a.Z) * (b.Z - a.Z));
            if (L < 1e-6) continue;
            double zb = Math.Max(a.Z, b.Z);
            var g = GroupFor(zb);
            if (g == null) continue;
            var st = St(f.Story, zb);
            var items = new List<(LoadPattern Pat, Func<double, double> Q, List<(double T, double P)> Pts, string Src)>();
            double A = 0, gam = 25;
            try { var sec = p.Section(f.Section); A = sec.Props().A; gam = p.Material(sec.Material).UnitWeight; } catch (Exception) { }
            foreach (var pat in p.Patterns.Where(x => x.SelfWeight != 0))
                if (A > 0) { double q = pat.SelfWeight * gam * A; items.Add((pat, _ => q, null, SrcBeam)); }
            foreach (var fl in p.FrameLoads.Where(l => l.Element == f.Id && l.Source != SlabLoadDistributor.Source && !l.Moment))
            {
                if (!pats.TryGetValue(fl.Pattern, out var pat)) continue;
                double sgn = fl.Dir == LoadDir.Gravity ? 1 : fl.Dir == LoadDir.GlobalZ ? -1 : 0;
                if (sgn == 0) continue;
                string src = fl.Source == WallLoadGenerator.WallSource || (fl.Note ?? "").IndexOf("tường", StringComparison.OrdinalIgnoreCase) >= 0 ? SrcWall
                    : fl.Source == StairLoadGenerator.Source ? SrcStair : IsP(pat.Type) ? SrcLive : SrcBeamLoad;
                var pts = fl.Points.Where(q => q != null && q.Length >= 2).OrderBy(q => q[0]).ToList();
                Func<double, double> qf = t =>
                {
                    if (pts.Count == 0) return 0;
                    if (t <= pts[0][0]) return t < pts[0][0] - 1e-9 ? 0 : pts[0][1] * sgn;
                    for (int i = 0; i + 1 < pts.Count; i++)
                        if (t <= pts[i + 1][0] + 1e-12)
                        {
                            double dt = pts[i + 1][0] - pts[i][0];
                            return sgn * (dt < 1e-12 ? pts[i + 1][1] : pts[i][1] + (t - pts[i][0]) / dt * (pts[i + 1][1] - pts[i][1]));
                        }
                    return 0;
                };
                var pl = fl.PointLoads.Where(q => q != null && q.Length >= 2).Select(q => (q[0], q[1] * sgn)).ToList();
                items.Add((pat, qf, pl, src));
            }
            if (items.Count == 0) continue;
            int ns = Math.Max(10, (int)Math.Ceiling(L / 0.1));
            foreach (var (pat, q, pl, src) in items)
            {
                double tot = 0;
                for (int i = 0; i < ns; i++)
                {
                    double t = (i + 0.5) / ns;
                    double v = q(t) * L / ns;
                    if (Math.Abs(v) < 1e-12) continue;
                    double x = a.X + t * (b.X - a.X), y = a.Y + t * (b.Y - a.Y);
                    Nearest(g, x, y).A.Add(pat, v, src);
                    tot += v;
                }
                if (pl != null)
                    foreach (var (t, P) in pl)
                    {
                        double x = a.X + t * (b.X - a.X), y = a.Y + t * (b.Y - a.Y);
                        Nearest(g, x, y).A.Add(pat, P, src);
                        tot += P;
                    }
                StAdd(st, pat, tot, src); Applied(pat, tot);
            }
        }

        // ---- tải tập trung tại nút (không kể nút gối) ----
        foreach (var nl in p.NodeLoads.Where(l => l.Source != SlabLoadDistributor.Source))
        {
            if (!pats.TryGetValue(nl.Pattern, out var pat) || Math.Abs(nl.Fz) < 1e-12) continue;
            Node n;
            try { n = p.Node(nl.Node); } catch (Exception) { continue; }
            if (n.IsSupport) continue;
            var g = GroupFor(n.Z);
            if (g == null) continue;
            double v = -nl.Fz;   // Fz dương hướng lên
            string src = nl.Source == StairLoadGenerator.Source ? SrcStair : SrcNode;
            Nearest(g, n.X, n.Y).A.Add(pat, v, src);
            StAdd(St(n.Story, n.Z), pat, v, src); Applied(pat, v);
        }

        // ---- cột: TLBT + cộng dồn từ trên xuống ----
        var byTop = new Dictionary<int, List<Col>>();
        foreach (var c in cols) { if (!byTop.TryGetValue(c.Top.Id, out var l)) byTop[c.Top.Id] = l = new List<Col>(); l.Add(c); }
        var bases = new Dictionary<int, (Acc A, Col C)>();
        foreach (var c in cols.OrderByDescending(c => c.Top.Z).ThenByDescending(c => c.Bot.Z))
        {
            double A = 0, gam = 25;
            try { var sec = p.Section(c.F.Section); A = sec.Props().A; gam = p.Material(sec.Material).UnitWeight; } catch (Exception) { }
            double Lc = c.Top.Z - c.Bot.Z;
            foreach (var pat in p.Patterns.Where(x => x.SelfWeight != 0))
                if (A > 0 && Lc > 0)
                {
                    double v = pat.SelfWeight * gam * A * Lc;
                    c.A.Add(pat, v, SrcCol);
                    StAdd(St(c.F.Story, c.Top.Z), pat, v, SrcCol); Applied(pat, v);
                }
            // cột ngay dưới (đỉnh = chân cột này)
            Col below = null;
            if (byTop.TryGetValue(c.Bot.Id, out var lst)) below = lst.FirstOrDefault(x => !ReferenceEquals(x, c));
            below ??= cols.FirstOrDefault(x => !ReferenceEquals(x, c) && Math.Abs(x.Top.X - c.Bot.X) < 1e-3 && Math.Abs(x.Top.Y - c.Bot.Y) < 1e-3 && Math.Abs(x.Top.Z - c.Bot.Z) < 1e-3);
            if (below != null) { below.A.Merge(c.A); continue; }
            if (c.Bot.IsSupport)
            {
                if (bases.TryGetValue(c.Bot.Id, out var e)) e.A.Merge(c.A);
                else { var acc = new Acc(); acc.Merge(c.A); bases[c.Bot.Id] = (acc, c); }
                continue;
            }
            // cột đặt trên dầm (cột hụt / cột trốn): dồn xuống cột gần nhất ở cao độ thấp hơn
            var g = GroupFor(c.Bot.Z, c.Top.Z);
            var tgt = g == null ? null : Nearest(g.Where(x => !ReferenceEquals(x, c)).ToList(), c.Bot.X, c.Bot.Y);
            if (tgt != null) { tgt.A.Merge(c.A); res.Notes.Add($"Cột {c.F.Label} (tầng {c.F.Story}) đặt trên dầm – tải dồn về cột gần nhất phía dưới"); }
            else res.Warnings.Add($"Cột {c.F.Label} không tìm được cột / gối đỡ phía dưới – tải bị bỏ qua");
        }

        // ---- kết quả chân cột ----
        var uls = r?.DesignCombos() ?? new List<string>();
        foreach (var (node, (acc, c)) in bases.OrderBy(kv => Math.Round(kv.Value.C.Bot.Y, 2)).ThenBy(kv => Math.Round(kv.Value.C.Bot.X, 2)))
        {
            var n = c.Bot;
            var ct = new ColumnTakedown
            {
                Node = node, X = n.X, Y = n.Y, G = acc.G, P = acc.P, PReducible = acc.Pr, Gtt = acc.Gtt, Ptt = acc.Ptt, PReducibleTt = acc.Prtt,
                Section = c.F.Section, LiveFloors = acc.LiveFloors.Count
            };
            try { ct.Name = DesignUtil.GridName(p, n.X, n.Y); } catch (Exception) { ct.Name = "Nút " + node; }
            if (string.IsNullOrWhiteSpace(ct.Name)) ct.Name = "Nút " + node;
            try { ct.Bx = DesignUtil.ColumnDimAlong(p, c.F, new Vec3(1, 0, 0)); ct.By = DesignUtil.ColumnDimAlong(p, c.F, new Vec3(0, 1, 0)); } catch (Exception) { ct.Bx = ct.By = 0.22; }
            foreach (var kv in acc.Src) ct.Sources[kv.Key] = kv.Value;
            foreach (var kv in acc.Floor) ct.Floors[kv.Key] = kv.Value;
            ct.Area = acc.Floor.Values.Sum();
            ct.AreaPerFloor = acc.Floor.Count > 0 ? ct.Area / acc.Floor.Count : 0;
            if (opt.LiveReduction && ct.LiveFloors >= 1 && ct.PReducible > 1e-9)
            {
                double Al = acc.LiveFloors.Select(s => acc.Floor.TryGetValue(s, out var v) ? v : 0).DefaultIfEmpty(0).Average();
                double psiA = Al > opt.A1 ? 0.4 + 0.6 / Math.Sqrt(Al / opt.A1) : 1.0;
                int nfl = Math.Max(1, ct.LiveFloors);
                ct.Psi = Math.Min(1, nfl >= 2 ? 0.4 + (psiA - 0.4) / Math.Sqrt(nfl) : psiA);
            }
            if (r != null)
            {
                try
                {
                    var ld = FoundationUtil.Loads(r, node, uls, 1.0, false);
                    if (ld.Count > 0)
                    {
                        ct.FeNtt = ld.Max(l => l.N);
                        ct.FeM = ld.Max(l => Math.Sqrt(l.Mx * l.Mx + l.My * l.My));
                    }
                    double fg = 0, fp = 0; bool any = false;
                    foreach (var pat in p.Patterns.Where(x => IsG(x.Type) || IsP(x.Type)))
                    {
                        CaseResult cr;
                        try { cr = r.Get(pat.Name); } catch (Exception) { continue; }
                        if (cr == null || !cr.Reactions.TryGetValue(node, out var R) || R == null || R.Length < 3) continue;
                        any = true;
                        if (IsG(pat.Type)) fg += R[2]; else fp += R[2];
                    }
                    if (any) { ct.FeG = fg; ct.FeP = fp; }
                }
                catch (Exception) { }
            }
            res.Columns.Add(ct);
        }
        res.Stories.AddRange(stories.Values.OrderBy(s => s.Elevation));
        res.AppliedG = appliedG; res.AppliedP = appliedP;

        // ---- nhận xét ----
        double floorArea = res.FloorArea;
        if (floorArea > 0)
        {
            res.Notes.Insert(0, $"Tổng diện tích sàn {F(floorArea, "0.#")} m², {res.Stories.Count(s => s.FloorArea > 0)} sàn; tổng tải tiêu chuẩn G + P = {F(res.TotalG + res.TotalP, "0")} kN "
                                + $"→ trung bình {F((res.TotalG + res.TotalP) / floorArea, "0.0")} kN/m² sàn (nhà phố BTCT thường 9–13 kN/m²/sàn kể cả tường, cột, dầm).");
            foreach (var s in res.Stories.Where(s => s.FloorArea > 0 && s.Intensity > 16))
                res.Warnings.Add($"Sàn {s.Story}: {F(s.Intensity, "0.0")} kN/m² – lớn bất thường (> 16 kN/m²); kiểm tra tải tường / hoàn thiện / hoạt tải chất trùng ("
                                 + string.Join(", ", s.Sources.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key} {F(kv.Value / s.FloorArea, "0.0")} kN/m²")) + ")");
        }
        double sumBase = res.TotalG + res.TotalP, sumApplied = appliedG + appliedP;
        if (sumApplied > 0 && Math.Abs(sumBase - sumApplied) > 0.01 * sumApplied + 1)
            res.Warnings.Add($"Σ tải chân cột {F(sumBase, "0")} kN ≠ Σ tải đã chất {F(sumApplied, "0")} kN – có tải không tìm được cột đỡ");
        if (res.HasFe)
        {
            double feG = res.Columns.Where(c => double.IsFinite(c.FeG)).Sum(c => c.FeG), feP = res.Columns.Where(c => double.IsFinite(c.FeP)).Sum(c => c.FeP);
            if (feG + feP > 0)
            {
                res.Notes.Add($"Đối chiếu phân tích khung: Σ phản lực TT = {F(feG, "0")} kN (truyền tải {F(res.TotalG, "0")}), Σ phản lực HT = {F(feP, "0")} kN (truyền tải {F(res.TotalP, "0")})"
                              + (Math.Abs(feG + feP - sumBase) > 0.05 * sumBase ? " – CHÊNH > 5%: kiểm tra trường hợp tải / tải sàn tự dồn" : " – khớp (cân bằng tải)."));
            }
            double rmax = res.Columns.Where(c => double.IsFinite(c.FeNtt) && c.Ntt > 1).Select(c => c.FeNtt / c.Ntt).DefaultIfEmpty(1).Max();
            if (rmax > 1.3)
                res.Notes.Add($"Phản lực tổ hợp TTGH I của phân tích khung lớn hơn truyền tải tới {F(rmax, "0.00")} lần ở một số chân cột: do tổ hợp có tải gió / động đất, "
                              + "phân phối lại nội lực của khung liên tục, và mô men chân cột ngàm. Thiết kế móng nhà phố thường lấy tải đứng (giằng móng chịu mô men) – "
                              + "xem tuỳ chọn \"Móng: bỏ mô men chân cột\" / \"Chỉ tổ hợp tải đứng\".");
        }
        if (opt.LiveReduction) res.Notes.Add("Hoạt tải sàn giảm theo diện chịu tải và số tầng: ψA1 = 0,4 + 0,6/√(A/A1), A1 = 9 m²; ψn1 = 0,4 + (ψA1 − 0,4)/√n (TCVN 2737); hoạt tải mái không giảm.");
        res.Notes.Add("γf: tĩnh tải 1,1 (trường hợp TT), tải hoàn thiện / tường 1,2 (TT phụ), hoạt tải 1,2 – hoặc theo hệ số γf khai báo trong trường hợp tải.");
        return res;
    }
}
