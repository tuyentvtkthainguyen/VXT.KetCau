using KetCauVXT.Design;
using KetCauVXT.Design.Concrete;
using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Mep;

// =====================================================================
// SỔ LỖ XUYÊN KẾT CẤU: chuyển các cảnh báo điện nước có đề xuất gia cường (StructuralImpact) sang HỒ SƠ KẾT CẤU
//  – ký hiệu LX1, LX2… (lỗ xuyên dầm) và LS1, LS2… (lỗ sàn có thép gia cường mép lỗ); lỗ giống nhau dùng chung ký hiệu
//  – tờ KC "CHI TIẾT GIA CƯỜNG LỖ XUYÊN KẾT CẤU", dấu trên mặt bằng kết cấu, lỗ trên mặt đứng dầm (BeamLineDesign.Holes)
//  – thép gia cường vào bảng thống kê (cấu kiện phụ "… – gia cường lỗ xuyên" trong DesignSummary.Others)
//  – kiểm tra chịu cắt tại tiết diện có lỗ (tiết diện thực h0 − Ø)
// =====================================================================

/// <summary>Một lỗ xuyên kết cấu đã đánh ký hiệu.</summary>
public sealed class PenetrationMark
{
    /// <summary>"LX1" (dầm) / "LS1" (sàn)</summary>
    public string Tag { get; set; } = "";
    /// <summary>"LoXuyenDam" | "LoSan"</summary>
    public string Kind { get; set; } = "";
    public int FrameId { get; set; }
    public int SlabId { get; set; }
    public string Story { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    /// <summary>Cao độ mặt bằng kết cấu chứa lỗ: đỉnh dầm / cao độ sàn (m)</summary>
    public double LevelZ { get; set; }
    /// <summary>Ø lỗ (dầm) hoặc cạnh lỗ (sàn), mm</summary>
    public double HoleMm { get; set; }
    public ImpactSeverity Severity { get; set; }
    public StructuralImpactItem Item { get; set; }
    public StrengtheningItem S { get; set; }
}

/// <summary>Cấu kiện phụ chứa thép gia cường lỗ của một dầm / sàn (để vào bảng thống kê thép và khối lượng).</summary>
public sealed class PenetrationMember : MemberDesignBase { }

public static class PenetrationRegister
{
    public const string SheetTitle = "CHI TIẾT GIA CƯỜNG LỖ XUYÊN KẾT CẤU";
    public const string MemberSuffix = " – gia cường lỗ xuyên";

    /// <summary>Lập sổ lỗ xuyên: mọi lỗ xuyên dầm (trừ mức Cấm) và lỗ sàn có thép gia cường; đánh ký hiệu theo nhóm giống nhau
    /// (loại, cấu kiện, Ø / kích thước lỗ), thứ tự theo cao độ rồi toạ độ.</summary>
    public static List<PenetrationMark> Build(Project p, ImpactOptions o = null)
    {
        var res = new List<PenetrationMark>();
        if (p?.Mep == null || p.Mep.IsEmpty) return res;
        List<StructuralImpactItem> items;
        try { items = StructuralImpact.Scan(p, o); }
        catch (Exception) { return res; }
        foreach (var it in items)
        {
            if (it.Strengthening == null || it.Severity >= ImpactSeverity.Cam) continue;
            var s = it.Strengthening;
            var m = new PenetrationMark
            {
                Kind = s.Kind, FrameId = it.FrameId, SlabId = it.SlabId, Story = it.Story, X = it.X, Y = it.Y, Z = it.Z,
                Severity = it.Severity, Item = it, S = s, HoleMm = s.Kind == "LoXuyenDam" ? s.HoleDia : Math.Max(s.HoleW, s.HoleL)
            };
            if (s.Kind == "LoXuyenDam")
            {
                var f = p.Frames.FirstOrDefault(x => x.Id == it.FrameId);
                if (f == null) continue;
                try { m.LevelZ = Math.Max(p.Node(f.NodeI).Z, p.Node(f.NodeJ).Z); } catch { continue; }
            }
            else m.LevelZ = MepRouting.StoryElevation(p, it.Story);
            res.Add(m);
        }
        int nx = 0, ns = 0;
        foreach (var g in res.OrderBy(m => m.Kind == "LoXuyenDam" ? 0 : 1).ThenBy(m => m.LevelZ).ThenBy(m => Math.Round(m.X, 2)).ThenBy(m => Math.Round(m.Y, 2))
                     .GroupBy(m => (m.Kind, m.S.Element, Math.Round(m.S.HoleDia), Math.Round(m.S.HoleW), Math.Round(m.S.HoleL))))
        {
            string tag = g.Key.Kind == "LoXuyenDam" ? "LX" + (++nx) : "LS" + (++ns);
            foreach (var m in g) m.Tag = tag;
        }
        return res;
    }

    // ------------------------------------------------------------------
    // Hình chi tiết (tờ KC)
    // ------------------------------------------------------------------
    /// <summary>Một hình chi tiết cho mỗi ký hiệu: tiêu đề có ký hiệu (vd. "LX1 – CHI TIẾT GIA CƯỜNG LỖ XUYÊN DẦM D3 Ø130 (SL=2)").</summary>
    public static List<Figure> Figures(Project p, List<PenetrationMark> marks = null)
    {
        marks ??= Build(p);
        var figs = new List<Figure>();
        foreach (var g in marks.GroupBy(m => m.Tag).OrderBy(g => g.First().Kind == "LoXuyenDam" ? 0 : 1).ThenBy(g => TagNo(g.Key)))
        {
            var s = g.First().S;
            var f = s.Kind == "LoXuyenDam" ? StructuralImpact.BeamFigure(s, g.Count()) : StructuralImpact.SlabFigure(s, g.Count());
            var e = f.W.Extents;
            double th = f.W.Th;
            // ký hiệu chi tiết: vòng tròn + chữ ở góc trên trái hình
            double cx = e.X1 + 1.6 * th, cy = e.Y2 + 2.2 * th;
            f.W.Circle(Lay.KyHieu, cx, cy, 1.3 * th);
            f.W.Text(Lay.Chu, cx, cy, 0.9 * th, g.Key, 0, 1, 2);
            f.W.Text(Lay.Chu, cx + 2.0 * th, cy, th, (g.Key + ": " + string.Join(", ", g.Select(m => m.Story).Distinct())).Trim(), 0, 0, 2);
            f.Name = g.Key + " – " + f.Name;
            figs.Add(f);
        }
        return figs;
    }

    private static int TagNo(string tag) => int.TryParse(new string((tag ?? "").Where(char.IsDigit).ToArray()), out int n) ? n : 0;

    /// <summary>Dấu lỗ trên MẶT BẰNG KẾT CẤU cao độ levelZ (m): vòng tròn tại vị trí lỗ + ký hiệu (mm bản vẽ = m × 1000). Trả về số dấu.</summary>
    public static int DrawPlanMarks(DxfWriter w, Project p, double levelZ, List<PenetrationMark> marks = null)
    {
        marks ??= Build(p);
        int n = 0;
        double th = w.Th;
        foreach (var m in marks.Where(m => Math.Abs(m.LevelZ - levelZ) < 0.06))
        {
            double x = m.X * 1000, y = m.Y * 1000, r = Math.Max(m.HoleMm / 2, 0.6 * th);
            w.Circle(Lay.KyHieu, x, y, r);
            w.Line(Lay.KyHieu, x + r * 0.7, y + r * 0.7, x + r + 1.2 * th, y + r + 1.2 * th);
            w.Text(Lay.Chu, x + r + 1.3 * th, y + r + 1.2 * th, 0.9 * th, m.Tag, 0, 0, 0);
            n++;
        }
        return n;
    }

    // ------------------------------------------------------------------
    // Thép gia cường → BarMark
    // ------------------------------------------------------------------
    /// <summary>Thép gia cường của một lỗ dạng BarMark (vai trò "Gia cường lỗ LX1 – …").</summary>
    public static List<BarMark> BarsOf(StrengtheningItem s, string tag, int count = 1)
    {
        var res = new List<BarMark>();
        if (s == null) return res;
        foreach (var b in s.Bars)
        {
            bool st = b.Desc.StartsWith("Đai");
            var bm = new BarMark
            {
                Role = $"{BeamHoleDraw.RolePrefix} {tag} – {b.Desc}", Dia = b.Dia, Count = b.Count * Math.Max(1, count),
                Length = Math.Round(b.Length * 100) / 100, Shape = st ? "Đai" : "Thẳng",
                Segments = st ? new List<double> { Math.Round(Math.Max(0, s.B - 0.05) * 1000), Math.Round(Math.Max(0, s.H - 0.05) * 1000) } : new List<double> { Math.Round(b.Length * 1000) },
                Spacing = st ? 0.05 : 0
            };
            res.Add(bm);
        }
        return res;
    }

    // ------------------------------------------------------------------
    // Kiểm tra cắt tại tiết diện có lỗ
    // ------------------------------------------------------------------
    /// <summary>Khả năng chịu cắt tại tiết diện có lỗ: tiết diện thực h0n = h0 − Ø lỗ; đai trong vùng lỗ bước min(s; 50 mm)
    /// (đai gia cường); Qu = Qb + 0,75·qsw·2h0n ≤ 0,3·Rb·b·h0n (TCVN 5574:2018 mục 8.1.3, áp cho tiết diện thực).</summary>
    public static (double Capacity, double H0n, string Note) ShearAtHole(double b, double h, double cover, double holeMm, Material conc, Material stirrup, int stirDia, double spacing)
    {
        double h0 = h - cover - 0.01;
        double h0n = Math.Max(0.05, h0 - holeMm / 1000);
        double s = Math.Min(spacing > 0 ? spacing : 0.15, 0.05);
        double qu = RcSection.ShearCapacity(b, h0n, conc, stirrup, stirDia, 2, s);
        return (qu, h0n, $"tiết diện thực h0n = h0 − Ø = {h0 * 1000:0} − {holeMm:0} = {h0n * 1000:0} mm; đai Ø{stirDia}a{s * 1000:0} quanh lỗ");
    }

    // ------------------------------------------------------------------
    // Gắn vào kết quả thiết kế (chế độ Thiết kế)
    // ------------------------------------------------------------------
    /// <summary>Vị trí (m) trên mặt đứng dầm liên tục d tính từ tim gối đầu, của điểm (x, y) thuộc phần tử frameId; NaN nếu không thuộc.</summary>
    /// <summary>Lực cắt tại vị trí x trong nhịp L (m) từ bao lực cắt dạng tải phân bố đều: V(x) = Qmax·|1 − 2x/L| (Qmax ở gối, 0 giữa nhịp);
    /// x ngoài [0; L] kẹp về gối.</summary>
    public static double ShearAt(double qmax, double L, double x)
    {
        if (L <= 1e-9) return Math.Abs(qmax);
        double t = Math.Clamp(x, 0, L) / L;
        return Math.Abs(qmax) * Math.Abs(1 - 2 * t);
    }

    /// <summary>Toạ độ đầu nhịp (m, dọc dải dầm) của nhịp chứa thanh frameId = tổng chiều dài các nhịp trước; NaN nếu không có.</summary>
    public static double SpanStart(BeamLineDesign d, int frameId)
    {
        double cum = 0;
        foreach (var sp in d?.Spans ?? new List<BeamSpanResult>())
        {
            if (sp.Frame == frameId) return cum;
            cum += sp.L;
        }
        return double.NaN;
    }

    public static double LineX(Project p, BeamLineDesign d, int frameId, double x, double y)
    {
        var spans = d?.Spans ?? new List<BeamSpanResult>();
        double cum = 0;
        for (int k = 0; k < spans.Count; k++)
        {
            var sp = spans[k];
            if (sp.Frame == frameId)
            {
                var f = p.Frames.FirstOrDefault(q => q.Id == frameId);
                if (f == null) return double.NaN;
                int start = f.NodeI;
                if (k > 0)
                {
                    var prev = p.Frames.FirstOrDefault(q => q.Id == spans[k - 1].Frame);
                    if (prev != null && (prev.NodeI == f.NodeJ || prev.NodeJ == f.NodeJ)) start = f.NodeJ;
                }
                else if (spans.Count > 1)
                {
                    var next = p.Frames.FirstOrDefault(q => q.Id == spans[1].Frame);
                    if (next != null && (next.NodeI == f.NodeI || next.NodeJ == f.NodeI)) start = f.NodeJ;
                }
                Node a;
                try { a = p.Node(start); } catch { return double.NaN; }
                return cum + MepGeom.Dist2D(a.X, a.Y, x, y);
            }
            cum += sp.L;
        }
        return double.NaN;
    }

    /// <summary>Đưa lỗ xuyên vào kết quả thiết kế: lỗ trên mặt đứng dầm (Holes), kiểm tra cắt tại lỗ, cấu kiện phụ chứa thép gia cường
    /// (DesignSummary.Others, nhóm "Dầm" / "Sàn"). Gọi lại nhiều lần không nhân đôi. Trả về số lỗ đã gắn.</summary>
    public static int AttachToDesign(Project p, DesignSummary s, List<PenetrationMark> marks = null)
    {
        if (p == null || s == null) return 0;
        s.Others ??= new();
        s.Others.RemoveAll(o => o.Member is PenetrationMember);
        foreach (var d in s.Beams)
        {
            d.Holes?.Clear();
            d.Checks.RemoveAll(c => c.Name.StartsWith("Chịu cắt tại lỗ xuyên", StringComparison.Ordinal));
        }
        marks ??= Build(p);
        if (marks.Count == 0) return 0;
        int n = 0;
        Material conc = null, st = null;
        try { conc = Material.Concrete(p.Concrete.ConcreteBeam); st = Material.Rebar(p.Concrete.RebarStirrup); } catch { }

        foreach (var m in marks.Where(m => m.Kind == "LoXuyenDam"))
        {
            var d = s.Beams.FirstOrDefault(b => b.Frames.Contains(m.FrameId));
            if (d == null) continue;
            double xm = LineX(p, d, m.FrameId, m.X, m.Y);
            if (double.IsNaN(xm)) continue;
            var stBar = m.S.Bars.FirstOrDefault(b => b.Desc.StartsWith("Đai"));
            var lb = m.S.Bars.FirstOrDefault(b => b.Desc.Contains("trên"));
            d.Holes.Add(new BeamHole
            {
                X = xm * 1000, Zb = m.S.HoleZ * 1000, Dia = m.S.HoleDia, Tag = m.Tag, Severity = StructuralImpact.SeverityText(m.Severity),
                StirrupsPerSide = stBar != null ? Math.Max(1, stBar.Count / 2) : 2, StirrupDia = stBar?.Dia ?? 8,
                BarDia = lb?.Dia ?? 12, BarLen = (lb?.Length ?? 1.0) * 1000
            });
            // kiểm tra cắt tại lỗ: Q tại VỊ TRÍ LỖ – nội suy bao lực cắt của nhịp (tải phân bố: Q tuyến tính từ Qmax ở gối về 0 giữa nhịp)
            var sp = d.Spans.FirstOrDefault(q => q.Frame == m.FrameId);
            if (conc != null && st != null && sp != null && sp.QMax > 0)
            {
                double b = d.B > 0 ? d.B : m.S.B, h = d.H > 0 ? d.H : m.S.H;
                double xs = SpanStart(d, m.FrameId);
                double xl = double.IsNaN(xs) ? sp.L / 2 : xm - xs;
                double vAt = ShearAt(Math.Abs(sp.QMax), sp.L, xl);
                var sh = ShearAtHole(b, h, p.Concrete.CoverBeam, m.S.HoleDia, conc, st, stBar?.Dia ?? Math.Max(6, sp.StirrupDia), sp.StirrupMid);
                d.Checks.Add(new CheckItem
                {
                    Name = $"Chịu cắt tại lỗ xuyên {m.Tag} (Ø{m.S.HoleDia:0})", Demand = vAt, Capacity = sh.Capacity, Unit = "kN",
                    Note = sh.Note + $"; Q tại lỗ (x = {xl:0.##} m / L = {sp.L:0.##} m) = {vAt:0.#} kN nội suy từ Qmax = {Math.Abs(sp.QMax):0.#} kN"
                });
            }
            n++;
        }
        // cấu kiện phụ chứa thép gia cường: mỗi dầm / sàn một cấu kiện
        foreach (var g in marks.GroupBy(m => (m.Kind, m.S.Element, m.Story)))
        {
            var pm = new PenetrationMember { Name = g.Key.Element + MemberSuffix, Story = g.Key.Story, Quantity = 1 };
            foreach (var t in g.GroupBy(m => m.Tag))
                pm.Bars.AddRange(BarsOf(t.First().S, t.Key, t.Count()));
            for (int i = 0; i < pm.Bars.Count; i++) pm.Bars[i].Mark = i + 1;
            pm.Messages.Add($"Thép gia cường lỗ xuyên ({string.Join(", ", g.Select(m => m.Tag).Distinct())}) – theo cảnh báo bộ môn điện nước");
            s.Others.Add((pm, g.Key.Kind == "LoXuyenDam" ? "Dầm" : "Sàn"));
        }
        foreach (var m in marks.Where(m => m.Kind == "LoSan"))
        {
            var sd = s.Slabs.FirstOrDefault(x => x.SlabId == m.SlabId);
            if (sd != null && !sd.Messages.Any(t => t.Contains(m.Tag + ":")))
                sd.Messages.Add($"Lỗ sàn {m.Tag}: {m.S.HoleW:0}×{m.S.HoleL:0} mm – thép gia cường mép lỗ theo chi tiết {m.Tag} (bù thép bị cắt)");
        }
        return n;
    }
}
