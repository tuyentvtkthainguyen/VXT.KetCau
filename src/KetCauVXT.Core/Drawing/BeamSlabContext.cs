using System.Globalization;
using KetCauVXT.Design;
using KetCauVXT.Model;

namespace KetCauVXT.Drawing;

// =====================================================================
// BỐI CẢNH SÀN HAI BÊN DẦM (để vẽ cánh sàn trên mặt cắt dầm kiểu Kết cấu Soft / XSCad):
// tại một vị trí dọc dầm, mỗi bên trái/phải có ô sàn nào (chiều dày hs, chênh cao dz, thép sàn) hay là mép ngoài
// (dầm biên) / lỗ sàn (cầu thang, giếng trời). "Trái/phải" theo hướng nhìn dọc dầm từ đầu x = 0 tới cuối
// (hướng của mặt đứng dầm): mặt cắt vẽ bên trái là bên trái hướng dầm.
// Mô hình hiện chưa có khai báo hạ cốt sàn riêng: dz lấy theo cao độ nút ô sàn so với đỉnh dầm (thường = 0) cộng
// LevelOffsetHook (null = 0) – chỗ móc khi dự án có thuộc tính hạ cốt sàn vệ sinh / ban công.
// =====================================================================

/// <summary>Ô sàn ở một bên dầm tại mặt cắt. Kích thước mm.</summary>
public sealed class SlabSideInfo
{
    public int SlabId;
    public string Label = "";
    /// <summary>Chiều dày sàn hs (mm).</summary>
    public double Hs;
    /// <summary>Chênh cao mặt trên sàn so với mặt trên dầm (mm; 0 = phẳng mặt dầm, âm = sàn thấp hơn – hạ cốt).</summary>
    public double Dz;
    /// <summary>Thép lớp dưới chạy vuông góc dầm (vẽ thành đường trong cánh) và song song dầm (vẽ chấm).</summary>
    public int BotPerpDia = 8, BotParDia = 8;
    public double BotPerpSpacing = 200, BotParSpacing = 200;
    /// <summary>Thép mũ (lớp trên) vắt qua dầm, chạy vuông góc dầm.</summary>
    public int TopDia = 8;
    public double TopSpacing = 200;
    /// <summary>Lấy theo kết quả thiết kế sàn (false = thể hiện quy ước Ø8a200).</summary>
    public bool BotFromDesign, TopFromDesign;
    public string BotPerpText => BarText(BotPerpDia, BotPerpSpacing);
    public string BotParText => BarText(BotParDia, BotParSpacing);
    public string TopText => BarText(TopDia, TopSpacing);

    internal static string BarText(int dia, double sMm) => "Ø" + dia.ToString(CultureInfo.InvariantCulture) + "a" + Math.Round(sMm).ToString("0", CultureInfo.InvariantCulture);
}

/// <summary>Bối cảnh sàn tại một mặt cắt dầm: cánh trái/phải (null = không có sàn), lỗ sàn, độ lệch tim dầm so với trục.</summary>
public sealed class BeamSlabSection
{
    /// <summary>Vị trí dọc dầm (mm, từ tim gối đầu).</summary>
    public double X;
    public SlabSideInfo Left, Right;
    /// <summary>Bên đó là lỗ sàn (cầu thang / giếng trời) – không có cánh.</summary>
    public bool LeftOpening, RightOpening;
    /// <summary>Độ lệch tim tiết diện so với đường nối nút (mm, dương = sang trái hướng dầm): điểm chèn + dời ngang.</summary>
    public double Offset;
    public double HsL => Left?.Hs ?? 0;
    public double HsR => Right?.Hs ?? 0;
    public double DzL => Left?.Dz ?? 0;
    public double DzR => Right?.Dz ?? 0;
    /// <summary>"T" (sàn hai bên), "L" (một bên – dầm biên), "R" (không có sàn: dầm mái, dầm thang, dầm đỡ tường…).</summary>
    public string Shape => Left != null && Right != null ? "T" : Left != null || Right != null ? "L" : "R";
    /// <summary>Khoá so sánh để đánh số mặt cắt (hai mặt cắt khác bối cảnh sàn là hai mặt cắt khác nhau).</summary>
    public string Key => Side(Left) + "/" + Side(Right) + "/" + Math.Round(Offset).ToString("0", CultureInfo.InvariantCulture);
    private static string Side(SlabSideInfo s) => s == null ? "-" :
        Math.Round(s.Hs).ToString("0", CultureInfo.InvariantCulture) + ":" + Math.Round(s.Dz).ToString("0", CultureInfo.InvariantCulture) + ":" + s.BotPerpText + ":" + s.BotParText + ":" + s.TopText;
}

public static class BeamSlabContext
{
    /// <summary>Chỗ móc hạ cốt sàn (m, âm = mặt sàn thấp hơn mặt dầm) khi dự án có khai báo riêng; null = 0.</summary>
    public static Func<Project, Slab, double> LevelOffsetHook { get; set; }

    /// <summary>Khoảng cách điểm dò từ mặt bên dầm vào ô sàn (m).</summary>
    public const double Probe = 0.15;

    private sealed class Piece
    {
        public FrameElement F;
        public bool Rev;
        public double X0, Len;       // m dọc dầm
        public ArcGeometry Arc;
        public Vec3 A, B;            // đầu/cuối theo hướng dầm
    }

    /// <summary>Hình học đường dầm: các đoạn theo thứ tự x tăng (giống BeamDesigner.BuildLines).</summary>
    private static List<Piece> Pieces(Project p, BeamLineDesign d)
    {
        var res = new List<Piece>();
        var frames = (d?.Frames ?? new List<int>()).Select(id => p.Frames.FirstOrDefault(f => f.Id == id)).Where(f => f != null).ToList();
        if (frames.Count == 0) return res;
        bool curved = frames.Any(f => f.IsCurved && CurvedBeam.Arc(p, f) != null);
        if (!curved)
        {
            var u = (p.Node(frames[0].NodeJ).P - p.Node(frames[0].NodeI).P);
            u = new Vec3(u.X, u.Y, 0).Normalize();
            if (Math.Abs(u.X) >= Math.Abs(u.Y)) { if (u.X < 0) u = u * -1; }
            else if (u.Y < 0) u = u * -1;
            var nodes = frames.SelectMany(f => new[] { f.NodeI, f.NodeJ }).Distinct().Select(id => p.Node(id).P).ToList();
            var o = nodes.OrderBy(n => n.Dot(u)).First();
            foreach (var f in frames)
            {
                var a = p.Node(f.NodeI).P; var b = p.Node(f.NodeJ).P;
                double ti = (a - o).Dot(u), tj = (b - o).Dot(u);
                bool rev = ti > tj;
                res.Add(new Piece { F = f, Rev = rev, X0 = Math.Min(ti, tj), Len = Math.Abs(tj - ti), A = rev ? b : a, B = rev ? a : b });
            }
            return res.OrderBy(x => x.X0).ToList();
        }
        // dầm cong: chuỗi nối đầu–cuối; một phần tử → bắt đầu tại nút I
        int start;
        if (frames.Count == 1) start = frames[0].NodeI;
        else
        {
            var cnt = new Dictionary<int, int>();
            foreach (var f in frames) foreach (int n in new[] { f.NodeI, f.NodeJ }) cnt[n] = (cnt.TryGetValue(n, out int k0) ? k0 : 0) + 1;
            var ends = cnt.Where(kv => kv.Value == 1).Select(kv => kv.Key).OrderBy(k => k).ToList();
            start = ends.Count > 0 ? ends[0] : frames[0].NodeI;
        }
        var left = new List<FrameElement>(frames);
        int cur = start;
        double x0 = 0;
        while (left.Count > 0)
        {
            var f = left.FirstOrDefault(q => q.NodeI == cur || q.NodeJ == cur) ?? left[0];
            left.Remove(f);
            bool rev = f.NodeJ == cur && f.NodeI != cur;
            double len = p.Length(f);
            var a = p.Node(f.NodeI).P; var b = p.Node(f.NodeJ).P;
            res.Add(new Piece { F = f, Rev = rev, X0 = x0, Len = len, Arc = CurvedBeam.Arc(p, f), A = rev ? b : a, B = rev ? a : b });
            x0 += len;
            cur = rev ? f.NodeI : f.NodeJ;
        }
        return res;
    }

    /// <summary>Điểm trên trục dầm (m, mặt bằng), véc tơ hướng dầm và đoạn chứa vị trí x (m dọc dầm).</summary>
    private static (Vec3 P, Vec3 U, Piece Pc) Locate(List<Piece> pcs, double x)
    {
        var pc = pcs.FirstOrDefault(q => x >= q.X0 - 1e-6 && x <= q.X0 + q.Len + 1e-6)
                 ?? (x < pcs[0].X0 ? pcs[0] : pcs[^1]);
        double s = pc.Len > 1e-9 ? Math.Clamp((x - pc.X0) / pc.Len, 0, 1) : 0;
        if (pc.Arc != null)
        {
            double t = pc.Rev ? 1 - s : s;
            var tg = pc.Arc.TangentAt(t);
            if (pc.Rev) tg = tg * -1;
            return (pc.Arc.PointAt(t), new Vec3(tg.X, tg.Y, 0).Normalize(), pc);
        }
        var u = new Vec3(pc.B.X - pc.A.X, pc.B.Y - pc.A.Y, 0).Normalize();
        return (pc.A + (pc.B - pc.A) * s, u, pc);
    }

    /// <summary>Bối cảnh sàn tại vị trí xMm (mm dọc dầm, gốc tại tim gối đầu – như toạ độ mặt đứng dầm).
    /// ds = kết quả thiết kế (lấy thép sàn thực tế); null → thể hiện quy ước Ø8a200.</summary>
    public static BeamSlabSection At(Project p, BeamLineDesign d, double xMm, DesignSummary ds = null)
    {
        var res = new BeamSlabSection { X = xMm };
        if (p == null || d == null) return res;
        List<Piece> pcs;
        try { pcs = Pieces(p, d); } catch (KeyNotFoundException) { return res; }
        if (pcs.Count == 0) return res;
        var (P, U, pc) = Locate(pcs, xMm / 1000);
        var n = new Vec3(-U.Y, U.X, 0);
        // dời ngang: BeamTools.LateralOffset theo hướng I→J của phần tử; đoạn đảo chiều → đổi dấu
        double off = BeamTools.LateralOffset(p, pc.F) * (pc.Rev ? -1 : 1);
        res.Offset = off * 1000;
        double b = d.B > 0 ? d.B : SafeB(p, d);
        double h = d.H > 0 ? d.H : 0.3;
        var C = P + n * off;
        res.Left = SideAt(p, d, ds, C + n * (b / 2 + Probe), C, U, h, out res.LeftOpening);
        res.Right = SideAt(p, d, ds, C - n * (b / 2 + Probe), C, U, h, out res.RightOpening);
        return res;
    }

    /// <summary>Bối cảnh tại giữa từng nhịp (để vẽ dải sàn trên mặt đứng dầm).</summary>
    public static List<BeamSlabSection> PerSpan(Project p, BeamLineDesign d, DesignSummary ds = null)
    {
        var res = new List<BeamSlabSection>();
        double x0 = 0;
        foreach (var sp in d?.Spans ?? new List<BeamSpanResult>())
        {
            res.Add(At(p, d, (x0 + sp.L / 2) * 1000, ds));
            x0 += sp.L;
        }
        return res;
    }

    private static double SafeB(Project p, BeamLineDesign d)
    {
        var sec = p.FrameSections.FirstOrDefault(s => s.Name == d.Section);
        return sec != null && sec.B > 0 ? sec.B : 0.22;
    }

    private static bool SameLevel(Project p, BeamLineDesign d, Slab s, out double zSlab)
    {
        zSlab = d.TopElevation;
        var nodes = new List<Node>();
        foreach (int id in s.Nodes) { try { nodes.Add(p.Node(id)); } catch (KeyNotFoundException) { } }
        if (nodes.Count < 3) return false;
        zSlab = nodes.Average(q => q.Z);
        bool story = !string.IsNullOrEmpty(d.Story) && !string.IsNullOrEmpty(s.Story);
        if (story && s.Story != d.Story) return false;
        return Math.Abs(zSlab - d.TopElevation) <= 0.6;
    }

    private static bool Inside(List<(double X, double Y)> poly, double x, double y)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var (xi, yi) = poly[i]; var (xj, yj) = poly[j];
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) c = !c;
        }
        return c;
    }

    private static SlabSideInfo SideAt(Project p, BeamLineDesign d, DesignSummary ds, Vec3 q, Vec3 c, Vec3 u, double h, out bool opening)
    {
        opening = (p.Openings ?? new List<FloorOpening>()).Any(o => (string.IsNullOrEmpty(d.Story) || o.Story == d.Story) && o.Contains(q.X, q.Y));
        if (opening) return null;
        foreach (var s in p.Slabs ?? new List<Slab>())
        {
            if (!SameLevel(p, d, s, out double zs)) continue;
            var poly = new List<(double X, double Y)>();
            foreach (int id in s.Nodes) { try { var nd = p.Node(id); poly.Add((nd.X, nd.Y)); } catch (KeyNotFoundException) { } }
            if (poly.Count < 3 || !Inside(poly, q.X, q.Y)) continue;
            var info = new SlabSideInfo { SlabId = s.Id, Label = s.Label ?? "" };
            var sd = ds?.Slabs?.FirstOrDefault(x => x.SlabId == s.Id || (x.RebarKeys != null && x.RebarKeys.Contains("S:" + s.Id.ToString(CultureInfo.InvariantCulture))));
            double t = 0;
            var sec = p.SlabSections?.FirstOrDefault(x => x.Name == s.Section);
            if (sec != null && sec.Thickness > 0) t = sec.Thickness;
            else if (sd != null && sd.Thickness > 0) t = sd.Thickness;
            else t = ParseThickness(s.Section);
            info.Hs = Math.Round(t * 1000);
            double dz = zs - d.TopElevation;
            if (LevelOffsetHook != null) { try { dz += LevelOffsetHook(p, s); } catch (Exception) { } }
            info.Dz = Math.Round(Math.Clamp(dz * 1000, -Math.Max(0, h * 1000 - info.Hs), 0));
            if (sd != null) FillBars(info, sd, poly, u, c);
            return info;
        }
        return null;
    }

    private static double ParseThickness(string sec)
    {
        var digits = new string((sec ?? "").Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mm) && mm >= 50 && mm <= 400 ? mm / 1000.0 : 0.10;
    }

    /// <summary>"Ø8a200" → (8, 200 mm); sai → false.</summary>
    public static bool TryParseBar(string s, out int dia, out double spacingMm)
    {
        dia = 0; spacingMm = 0;
        if (string.IsNullOrEmpty(s)) return false;
        int i = s.IndexOf('Ø');
        if (i < 0) i = s.IndexOf('D');
        if (i < 0) return false;
        int j = i + 1, d0 = j;
        while (j < s.Length && char.IsDigit(s[j])) j++;
        if (j == d0 || !int.TryParse(s.Substring(d0, j - d0), NumberStyles.Integer, CultureInfo.InvariantCulture, out dia)) return false;
        if (j >= s.Length || (s[j] != 'a' && s[j] != 'A')) return false;
        int k = j + 1, s0 = k;
        while (k < s.Length && (char.IsDigit(s[k]) || s[k] == '.')) k++;
        return k > s0 && double.TryParse(s.Substring(s0, k - s0), NumberStyles.Float, CultureInfo.InvariantCulture, out spacingMm) && spacingMm > 0 && dia > 0;
    }

    private static void FillBars(SlabSideInfo info, SlabDesign sd, List<(double X, double Y)> poly, Vec3 u, Vec3 c)
    {
        // cạnh ô song song dầm dài nhất → dầm nằm trên cạnh dài hay cạnh ngắn của ô
        double le = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            double dx = b.X - a.X, dy = b.Y - a.Y, l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1e-6) continue;
            if (Math.Abs(dx / l * u.X + dy / l * u.Y) > 0.95) le = Math.Max(le, l);
        }
        bool alongLong = le <= 0 || Math.Abs(le - sd.Ly) <= Math.Abs(le - sd.Lx);
        // thép theo phương ngắn (BotX) vuông góc cạnh dài; dầm trên cạnh dài → BotX vuông góc dầm
        string perp = alongLong ? sd.BotX : sd.BotY, par = alongLong ? sd.BotY : sd.BotX;
        if (TryParseBar(perp, out int d1, out double s1)) { info.BotPerpDia = d1; info.BotPerpSpacing = s1; info.BotFromDesign = true; }
        if (TryParseBar(par, out int d2, out double s2)) { info.BotParDia = d2; info.BotParSpacing = s2; info.BotFromDesign = true; }
        // thép mũ: thanh vẽ trên cạnh trùng trục dầm (SlabEdgeBar) → theo phương án; không có → TopX/TopY
        SlabEdgeBar eb = null;
        foreach (var e in sd.EdgeBars ?? new List<SlabEdgeBar>())
        {
            if (e?.Bar == null) continue;
            double ex = e.X2 - e.X1, ey = e.Y2 - e.Y1, l = Math.Sqrt(ex * ex + ey * ey);
            if (l < 1e-6 || Math.Abs(ex / l * u.X + ey / l * u.Y) < 0.95) continue;
            double t = ((c.X - e.X1) * ex + (c.Y - e.Y1) * ey) / (l * l);
            double dist = Math.Abs((c.X - e.X1) * ey - (c.Y - e.Y1) * ex) / l;
            if (t >= -0.02 && t <= 1.02 && dist < 0.6) { eb = e; break; }
        }
        if (eb != null && eb.Bar.Dia > 0 && eb.Bar.Spacing > 0)
        {
            info.TopDia = eb.Bar.Dia; info.TopSpacing = eb.Bar.Spacing * 1000; info.TopFromDesign = true;
        }
        else if (TryParseBar(alongLong ? sd.TopX : sd.TopY, out int d3, out double s3))
        {
            info.TopDia = d3; info.TopSpacing = s3; info.TopFromDesign = true;
        }
    }
}
