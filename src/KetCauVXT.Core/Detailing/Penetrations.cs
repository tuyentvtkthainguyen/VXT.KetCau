using KetCauVXT.Design;
using KetCauVXT.Drawing;
using KetCauVXT.Mep;
using KetCauVXT.Model;

namespace KetCauVXT.Detailing;

// =====================================================================
// LỖ XUYÊN KẾT CẤU ở chế độ TRIỂN KHAI HỒ SƠ: dầm (ống lồng) và sàn (lỗ có thép gia cường mép lỗ).
// Nhập tay hoặc chuyển từ mô hình (ModelToDetailing.PenetrationsFromModel – theo cảnh báo bộ môn điện nước).
// Đánh giá theo cùng quy tắc với Mep/StructuralImpact.cs; thép gia cường vào thép cấu kiện (bảng thống kê), lỗ vẽ trên mặt đứng / mặt bằng.
// =====================================================================

/// <summary>Một lỗ xuyên dầm (ống lồng).</summary>
public class BeamPenetration
{
    [Field("Ký hiệu", Hint = "vd. LX1 (trống = tự đặt)")] public string Tag { get; set; } = "";
    [Field("Vị trí từ tim gối đầu dầm", Unit = "m")] public double X { get; set; } = 1.0;
    [Field("Tâm lỗ cách đáy dầm (0 = giữa chiều cao)", Unit = "mm", Scale = 1000)] public double Zb { get; set; }
    [Field("Ø lỗ / ống lồng", Unit = "mm")] public double Dia { get; set; } = 110;
    [Field("Ghi chú")] public string Note { get; set; } = "";

    /// <summary>Đánh giá các lỗ của dầm: thêm thép gia cường (vai trò "Gia cường lỗ …") vào d.Bars, kiểm tra cắt tại lỗ (khi có Q),
    /// cảnh báo vị trí nguy hiểm, khối lượng ống lồng; trả về danh sách lỗ để vẽ trên mặt đứng (gán sau khi đánh số thép).</summary>
    public static List<BeamHole> Apply(ManualBeam mb, BeamLineDesign d, DetailResult r, double[] nodeX, double[] lcl, double w,
        Material conc, Material stirrup, double stirSupport, DetailSettings s)
    {
        var holes = new List<BeamHole>();
        if (mb?.Penetrations == null || mb.Penetrations.Count == 0 || nodeX == null || nodeX.Length < 2) return holes;
        int idx = 0;
        var placed = new List<(double X, double Dia)>();
        foreach (var pe in mb.Penetrations.OrderBy(q => q.X))
        {
            idx++;
            if (pe.Dia <= 0) continue;
            string tag = string.IsNullOrWhiteSpace(pe.Tag) ? "LX" + idx : pe.Tag.Trim();
            int k = 0;
            while (k + 1 < nodeX.Length - 1 && pe.X > nodeX[k + 1]) k++;
            double l0 = lcl[Math.Min(k, lcl.Length - 1)];
            double dSup = Math.Max(0, Math.Min(pe.X - nodeX[k] - w / 2, nodeX[k + 1] - w / 2 - pe.X));
            double zb = pe.Zb > 0 ? pe.Zb : mb.H / 2;
            double? other = null; double otherDia = 0;
            if (placed.Count > 0) { var nh = placed.OrderBy(q => Math.Abs(q.X - pe.X)).First(); other = Math.Abs(nh.X - pe.X); otherDia = nh.Dia; }
            var ev = StructuralImpact.EvaluateBeamHole(mb.B, mb.H, l0, dSup, zb, pe.Dia, false, other, otherDia, Math.Max(mb.StirD, 6), 12);
            placed.Add((pe.X, pe.Dia));
            ev.Strengthening.Element = mb.Name;
            d.Bars.AddRange(PenetrationRegister.BarsOf(ev.Strengthening, tag));
            string sev = StructuralImpact.SeverityText(ev.Severity);
            if (ev.Severity >= ImpactSeverity.CanhBao)
                d.Messages.Add($"CẢNH BÁO lỗ xuyên {tag} Ø{pe.Dia:0} tại x = {Dt.F(pe.X)} m ({sev}): {string.Join("; ", ev.Reasons)} – {ev.Proposal}");
            r.Calc.Add($"Lỗ xuyên {tag}: Ø{pe.Dia:0} tại x = {Dt.F(pe.X)} m, cách đáy {Dt.Mm(zb)} mm, cách mép gối {Dt.F(dSup, "0.###")} m – {sev}: {string.Join("; ", ev.Reasons)}");
            r.Calc.Add($"Gia cường {tag}: {ev.Strengthening.Note}");
            if (mb.Q > 0 && conc != null && stirrup != null)
            {
                var sh = PenetrationRegister.ShearAtHole(mb.B, mb.H, s?.CoverBeam ?? 0.025, pe.Dia, conc, stirrup, ev.Strengthening.Bars.FirstOrDefault()?.Dia ?? 8, stirSupport);
                d.Checks.Add(Dt.Check($"Chịu cắt tại lỗ xuyên {tag} (Ø{pe.Dia:0})", mb.Q, sh.Capacity, "kN", sh.Note));
                r.Calc.Add($"Cắt tại lỗ {tag}: {sh.Note}; Qu = {Dt.F(sh.Capacity, "0.0")} kN ≥ Q = {Dt.F(mb.Q, "0.0")} kN?");
            }
            r.Extra.Add(($"Ống lồng thép Ø{pe.Dia:0} chờ xuyên dầm (L = b)", "cái", 1));
            var stb = ev.Strengthening.Bars.FirstOrDefault(b => b.Desc.StartsWith("Đai"));
            var lb = ev.Strengthening.Bars.FirstOrDefault(b => b.Desc.Contains("trên"));
            holes.Add(new BeamHole
            {
                X = pe.X * 1000, Zb = zb * 1000, Dia = pe.Dia, Tag = tag, Severity = sev,
                StirrupsPerSide = stb != null ? Math.Max(1, stb.Count / 2) : 2, StirrupDia = stb?.Dia ?? 8, BarDia = lb?.Dia ?? 12, BarLen = (lb?.Length ?? 1.0) * 1000
            });
        }
        return holes;
    }
}

/// <summary>Một lỗ xuyên sàn (hộp kỹ thuật / ống đứng) – lỗ &gt; 300 mm có thép gia cường mép lỗ.</summary>
public class SlabPenetration
{
    [Field("Ký hiệu", Hint = "vd. LS1 (trống = tự đặt)")] public string Tag { get; set; } = "";
    [Field("Tâm lỗ X (từ góc ô sàn)", Unit = "m")] public double X { get; set; } = 0.6;
    [Field("Tâm lỗ Y (từ góc ô sàn)", Unit = "m")] public double Y { get; set; } = 0.6;
    [Field("Cạnh lỗ theo X", Unit = "mm", Scale = 1000)] public double W { get; set; } = 0.4;
    [Field("Cạnh lỗ theo Y", Unit = "mm", Scale = 1000)] public double L { get; set; } = 0.4;

    /// <summary>Đánh giá lỗ của ô sàn: thép gia cường mép lỗ vào r.Bars, ghi chú, khối lượng bê tông trừ lỗ.</summary>
    public static void Apply(ManualSlab ms, DetailResult r, int barDia, double spacing)
    {
        if (ms?.Penetrations == null) return;
        int idx = 0;
        foreach (var pe in ms.Penetrations)
        {
            idx++;
            if (pe.W <= 0 || pe.L <= 0) continue;
            string tag = string.IsNullOrWhiteSpace(pe.Tag) ? "LS" + idx : pe.Tag.Trim();
            var ev = StructuralImpact.EvaluateSlabOpening(pe.W, pe.L, ms.T, null, barDia, spacing, 12);
            r.Calc.Add($"Lỗ sàn {tag}: {Dt.Mm(pe.W)}×{Dt.Mm(pe.L)} mm – {string.Join("; ", ev.Reasons)}");
            if (ev.Strengthening != null)
            {
                ev.Strengthening.Element = ms.Name;
                r.Bars.AddRange(PenetrationRegister.BarsOf(ev.Strengthening, tag).Select(b => { b.Role = b.Role.Replace(BeamHoleDraw.RolePrefix, "Gia cường lỗ sàn"); return b; }));
                r.Messages.Add($"Lỗ sàn {tag} {Dt.Mm(pe.W)}×{Dt.Mm(pe.L)}: thép gia cường mép lỗ – {ev.Strengthening.Note}");
            }
            r.ConcreteVolume -= pe.W * pe.L * ms.T;
        }
    }

    /// <summary>Vẽ lỗ + thép gia cường mép lỗ trên mặt bằng ô sàn (mm, gốc góc ô).</summary>
    public static void DrawPlan(DxfWriter w, ManualSlab ms)
    {
        if (ms?.Penetrations == null) return;
        int idx = 0;
        double th = w.Th;
        foreach (var pe in ms.Penetrations)
        {
            idx++;
            if (pe.W <= 0 || pe.L <= 0) continue;
            string tag = string.IsNullOrWhiteSpace(pe.Tag) ? "LS" + idx : pe.Tag.Trim();
            double cx = pe.X * 1000, cy = pe.Y * 1000, a = pe.W * 500, b = pe.L * 500;
            w.Rect(Lay.BeTong, cx - a, cy - b, cx + a, cy + b);
            w.Line(Lay.BeTong, cx - a, cy - b, cx + a, cy + b); w.Line(Lay.BeTong, cx - a, cy + b, cx + a, cy - b);
            if (Math.Max(pe.W, pe.L) > 0.3 + 1e-9)
            {
                double e = 50, ext = 480;
                w.Line(Lay.Thep, cx - a - e, cy - b - ext, cx - a - e, cy + b + ext); w.Line(Lay.Thep, cx + a + e, cy - b - ext, cx + a + e, cy + b + ext);
                w.Line(Lay.Thep, cx - a - ext, cy - b - e, cx + a + ext, cy - b - e); w.Line(Lay.Thep, cx - a - ext, cy + b + e, cx + a + ext, cy + b + e);
            }
            w.Text(Lay.Chu, cx + a + 0.5 * th, cy + b + 0.5 * th, 0.9 * th, $"{tag} {Dt.Mm(pe.W)}×{Dt.Mm(pe.L)} (xem chi tiết {tag})", 0, 0, 0);
        }
    }
}

/// <summary>Dấu lỗ xuyên (LX / LS) trên MẶT BẰNG KẾT CẤU của hồ sơ triển khai, theo vị trí cấu kiện đặt trên mặt bằng.</summary>
public static class PenetrationPlan
{
    /// <summary>Trả về số dấu đã vẽ (toạ độ mặt bằng m → bản vẽ mm).</summary>
    public static int Draw(DxfWriter w, PlanLevel lv, Dictionary<string, (IDetailMember M, DetailResult R)> map)
    {
        if (lv?.Items == null || map == null) return 0;
        int n = 0;
        double th = w.Th;
        void Mark(double x, double y, double rMm, string tag)
        {
            double r = Math.Max(rMm, 0.6 * th);
            w.Circle(Lay.KyHieu, x, y, r);
            w.Line(Lay.KyHieu, x + 0.7 * r, y + 0.7 * r, x + r + 1.2 * th, y + r + 1.2 * th);
            w.Text(Lay.Chu, x + r + 1.3 * th, y + r + 1.2 * th, 0.9 * th, tag, 0, 0, 0);
            n++;
        }
        foreach (var it in lv.Items)
        {
            if (!map.TryGetValue(it.Member ?? "", out var mr)) continue;
            if (it.Kind == PlanKinds.Beam && mr.M is ManualBeam mb && mb.Penetrations.Count > 0)
            {
                double L = it.Length;
                if (L < 1e-6) continue;
                double ux = (it.X2 - it.X1) / L, uy = (it.Y2 - it.Y1) / L;
                int k = 0;
                foreach (var pe in mb.Penetrations.OrderBy(q => q.X))
                {
                    k++;
                    if (pe.X < 0 || pe.X > L + 1e-6) continue;
                    Mark((it.X1 + ux * pe.X) * 1000, (it.Y1 + uy * pe.X) * 1000, pe.Dia / 2, string.IsNullOrWhiteSpace(pe.Tag) ? "LX" + k : pe.Tag.Trim());
                }
            }
            else if (it.Kind == PlanKinds.Slab && mr.M is ManualSlab ms && ms.Penetrations.Count > 0)
            {
                double x0 = Math.Min(it.X1, it.X2), y0 = Math.Min(it.Y1, it.Y2);
                int k = 0;
                foreach (var pe in ms.Penetrations)
                {
                    k++;
                    double cx = (x0 + pe.X) * 1000, cy = (y0 + pe.Y) * 1000;
                    w.Rect(Lay.KyHieu, cx - pe.W * 500, cy - pe.L * 500, cx + pe.W * 500, cy + pe.L * 500);
                    Mark(cx, cy, 0, string.IsNullOrWhiteSpace(pe.Tag) ? "LS" + k : pe.Tag.Trim());
                }
            }
        }
        return n;
    }
}

/// <summary>Chuyển lỗ xuyên từ MÔ HÌNH (cảnh báo bộ môn điện nước) sang cấu kiện hồ sơ triển khai cùng tên.</summary>
public static class ModelPenetrations
{
    /// <summary>Dầm: cấu kiện ManualBeam trùng tên dầm của mô hình, vị trí x tính từ nút đầu phần tử dầm (NodeI).
    /// Sàn: ManualSlab trùng nhãn ô sàn, toạ độ từ góc dưới trái ô sàn. Không nhân đôi (cùng ký hiệu + vị trí). Trả về nhật ký.</summary>
    public static List<string> Apply(Project model, DetailingProject dp, List<PenetrationMark> marks = null)
    {
        var log = new List<string>();
        if (model == null || dp == null) return log;
        marks ??= PenetrationRegister.Build(model);
        if (marks.Count == 0) { log.Add("Mô hình không có lỗ xuyên dầm / sàn cần gia cường (bộ môn điện nước)."); return log; }
        int nb = 0, ns = 0;
        var missing = new HashSet<string>();
        foreach (var m in marks)
        {
            if (m.Kind == "LoXuyenDam")
            {
                var mb = dp.Beams.FirstOrDefault(b => string.Equals(b.Name, m.S.Element, StringComparison.OrdinalIgnoreCase));
                var f = model.Frames.FirstOrDefault(q => q.Id == m.FrameId);
                if (mb == null || f == null) { missing.Add(m.S.Element); continue; }
                Node a;
                try { a = model.Node(f.NodeI); } catch { continue; }
                double x = Math.Round(MepGeom.Dist2D(a.X, a.Y, m.X, m.Y), 3);
                if (mb.Penetrations.Any(q => q.Tag == m.Tag && Math.Abs(q.X - x) < 0.01)) continue;
                mb.Penetrations.Add(new BeamPenetration { Tag = m.Tag, X = x, Zb = m.S.HoleZ, Dia = m.S.HoleDia, Note = "Từ bộ môn điện nước – " + StructuralImpact.SeverityText(m.Severity) });
                nb++;
            }
            else
            {
                var sl = model.Slabs.FirstOrDefault(q => q.Id == m.SlabId);
                var ms = dp.Slabs.FirstOrDefault(q => string.Equals(q.Name, m.S.Element, StringComparison.OrdinalIgnoreCase));
                if (ms == null || sl == null) { missing.Add(m.S.Element); continue; }
                double x0 = double.MaxValue, y0 = double.MaxValue;
                foreach (int nid in sl.Nodes) { try { var nd = model.Node(nid); x0 = Math.Min(x0, nd.X); y0 = Math.Min(y0, nd.Y); } catch { } }
                if (x0 == double.MaxValue) continue;
                double px = Math.Round(m.X - x0, 3), py = Math.Round(m.Y - y0, 3);
                if (ms.Penetrations.Any(q => q.Tag == m.Tag && Math.Abs(q.X - px) < 0.01 && Math.Abs(q.Y - py) < 0.01)) continue;
                ms.Penetrations.Add(new SlabPenetration { Tag = m.Tag, X = px, Y = py, W = m.S.HoleW / 1000, L = m.S.HoleL / 1000 });
                ns++;
            }
        }
        log.Add($"Thêm {nb} lỗ xuyên dầm, {ns} lỗ xuyên sàn vào cấu kiện hồ sơ (thép gia cường + kiểm tra cắt tại lỗ).");
        if (missing.Count > 0) log.Add("Chưa có cấu kiện hồ sơ trùng tên: " + string.Join(", ", missing) + " – tạo cấu kiện cùng tên rồi chạy lại.");
        return log;
    }
}
