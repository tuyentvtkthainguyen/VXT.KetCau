using KetCauVXT.Model;

namespace KetCauVXT.Import.Link;

// =====================================================================
// PICK ĐỐI TƯỢNG TRÊN BẢN VẼ LIÊN KẾT (mặt bằng kiến trúc sơ bộ) → tạo dầm / sàn / cầu thang / lỗ sàn đúng vị trí:
//   • BeamFromPair: bấm giữa hai nét song song (mép dầm / mép tường, cách nhau 100…600 mm) → trục dầm (đường giữa, đoạn chồng nhau) + bề rộng;
//   • BeamFromLine: bấm vào một nét → trục dầm theo nét đó;
//   • ClosedLoopAt: bấm trong một đa tuyến kín → đa giác (ô sàn, ô cầu thang, lỗ / giếng trời);
//   • CreateBeam / CreateFromLoop: tạo dầm (tiết diện có sẵn hoặc tự tạo b×h theo bề rộng đo được), dầm bao + sàn / thang / lỗ.
// Toạ độ mô hình (m) – đã qua phép đặt của CadLink (CadLinkView). Hàm thuần nhận danh sách đoạn / đa giác để kiểm thử độc lập.
// =====================================================================

/// <summary>Kết quả pick dầm: trục (x1,y1)–(x2,y2), bề rộng (m; 0 = không đo được – pick một nét).</summary>
public sealed class CadBeamPick
{
    public double X1, Y1, X2, Y2, Width;
    public double Length => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
    public string Note = "";
}

public static class CadPick
{
    public const double MinWidth = 0.10, MaxWidth = 0.60;

    private static double Hyp(double x, double y) => Math.Sqrt(x * x + y * y);
    private static double R4(double v) => Math.Round(v, 4);

    /// <summary>Khoảng cách điểm – đoạn và tham số chiếu (0…1).</summary>
    public static double DistToSeg(double x, double y, (double X1, double Y1, double X2, double Y2) s, out double t)
    {
        double dx = s.X2 - s.X1, dy = s.Y2 - s.Y1, L2 = dx * dx + dy * dy;
        t = L2 < 1e-12 ? 0 : Math.Clamp(((x - s.X1) * dx + (y - s.Y1) * dy) / L2, 0, 1);
        return Hyp(x - (s.X1 + t * dx), y - (s.Y1 + t * dy));
    }

    /// <summary>Hai nét song song gần điểm bấm nhất, nằm hai phía điểm bấm, cách nhau minW…maxW → trục dầm = đường giữa trên đoạn chồng nhau.
    /// Trả về null nếu không có cặp phù hợp.</summary>
    public static CadBeamPick BeamFromPair(IList<(double X1, double Y1, double X2, double Y2)> segs, double x, double y,
        double minW = MinWidth, double maxW = MaxWidth, double angTolDeg = 2)
    {
        if (segs == null || segs.Count < 2) return null;
        var near = new List<(int I, double D, double Side, double Ux, double Uy)>();
        for (int i = 0; i < segs.Count; i++)
        {
            var s = segs[i];
            double L = Hyp(s.X2 - s.X1, s.Y2 - s.Y1);
            if (L < 0.05) continue;
            double ux = (s.X2 - s.X1) / L, uy = (s.Y2 - s.Y1) / L;
            // khoảng cách vuông góc tới đường thẳng chứa nét (có dấu), điểm chiếu phải nằm trong nét
            double along = (x - s.X1) * ux + (y - s.Y1) * uy;
            if (along < -0.01 || along > L + 0.01) continue;
            double side = -(x - s.X1) * uy + (y - s.Y1) * ux;
            if (Math.Abs(side) > maxW) continue;
            near.Add((i, Math.Abs(side), side, ux, uy));
        }
        CadBeamPick best = null; double bestScore = double.MaxValue;
        foreach (var a in near)
            foreach (var b in near)
            {
                if (a.I >= b.I) continue;
                double dot = a.Ux * b.Ux + a.Uy * b.Uy;
                if (Math.Abs(dot) < Math.Cos(angTolDeg * Math.PI / 180)) continue;   // không song song
                // hai phía điểm bấm (dấu "side" theo cùng hướng của a)
                double sb = dot > 0 ? b.Side : -b.Side;
                if (Math.Sign(a.Side) == Math.Sign(sb)) continue;
                var sa = segs[a.I]; var s2 = segs[b.I];
                // khoảng cách giữa hai đường song song
                double w = Math.Abs(-(s2.X1 - sa.X1) * a.Uy + (s2.Y1 - sa.Y1) * a.Ux);
                if (w < minW - 1e-6 || w > maxW + 1e-6) continue;
                // đoạn chồng nhau theo phương a
                double a0 = 0, a1 = Hyp(sa.X2 - sa.X1, sa.Y2 - sa.Y1);
                double b0 = (s2.X1 - sa.X1) * a.Ux + (s2.Y1 - sa.Y1) * a.Uy, b1 = (s2.X2 - sa.X1) * a.Ux + (s2.Y2 - sa.Y1) * a.Uy;
                double lo = Math.Max(a0, Math.Min(b0, b1)), hi = Math.Min(a1, Math.Max(b0, b1));
                if (hi - lo < 0.10) continue;
                double score = a.D + b.D;
                if (score >= bestScore) continue;
                // đường giữa: dời nét a sang phía nét b một nửa bề rộng
                double nx = -a.Uy, ny = a.Ux;
                double sgn = Math.Sign(-(s2.X1 - sa.X1) * a.Uy + (s2.Y1 - sa.Y1) * a.Ux);
                double ox = sa.X1 + nx * sgn * w / 2, oy = sa.Y1 + ny * sgn * w / 2;
                bestScore = score;
                best = new CadBeamPick
                {
                    X1 = R4(ox + a.Ux * lo), Y1 = R4(oy + a.Uy * lo), X2 = R4(ox + a.Ux * hi), Y2 = R4(oy + a.Uy * hi),
                    Width = Math.Round(w, 4), Note = $"cặp nét song song cách {w * 1000:0} mm"
                };
            }
        return best;
    }

    /// <summary>Nét gần điểm bấm nhất (trong tol) → trục dầm theo nét.</summary>
    public static CadBeamPick BeamFromLine(IList<(double X1, double Y1, double X2, double Y2)> segs, double x, double y, double tol = 0.3)
    {
        if (segs == null) return null;
        int bi = -1; double bd = double.MaxValue;
        for (int i = 0; i < segs.Count; i++)
        {
            if (Hyp(segs[i].X2 - segs[i].X1, segs[i].Y2 - segs[i].Y1) < 0.05) continue;
            double d = DistToSeg(x, y, segs[i], out _);
            if (d < bd) { bd = d; bi = i; }
        }
        if (bi < 0 || bd > tol) return null;
        var s = segs[bi];
        return new CadBeamPick { X1 = R4(s.X1), Y1 = R4(s.Y1), X2 = R4(s.X2), Y2 = R4(s.Y2), Width = 0, Note = "theo một nét" };
    }

    /// <summary>Điểm trong đa giác (tia ngang).</summary>
    public static bool Inside(IList<(double X, double Y)> poly, double x, double y)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if (((a.Y > y) != (b.Y > y)) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y + 1e-300) + a.X) c = !c;
        }
        return c;
    }

    public static double Area(IList<(double X, double Y)> poly)
    {
        double s = 0;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) s += poly[j].X * poly[i].Y - poly[i].X * poly[j].Y;
        return Math.Abs(s) / 2;
    }

    /// <summary>Đa tuyến kín nhỏ nhất chứa điểm bấm; null nếu không có.</summary>
    public static List<(double X, double Y)> ClosedLoopAt(IEnumerable<IList<(double X, double Y)>> polys, double x, double y)
    {
        List<(double X, double Y)> best = null; double ba = double.MaxValue;
        foreach (var p in polys ?? Enumerable.Empty<IList<(double X, double Y)>>())
        {
            if (p == null || p.Count < 3) continue;
            if (!Inside(p, x, y)) continue;
            double a = Area(p);
            if (a > 1e-6 && a < ba) { ba = a; best = p.ToList(); }
        }
        return best;
    }

    /// <summary>Đa giác gần chữ nhật song song trục: trả về khung bao (sai số tol, m).</summary>
    public static bool IsAxisRect(IList<(double X, double Y)> poly, out double x0, out double y0, out double x1, out double y1, double tol = 0.01)
    {
        x0 = poly.Min(q => q.X); x1 = poly.Max(q => q.X); y0 = poly.Min(q => q.Y); y1 = poly.Max(q => q.Y);
        double lx0 = x0, lx1 = x1, ly0 = y0, ly1 = y1;
        if (poly.Count < 4) return false;
        return poly.All(q => (Math.Abs(q.X - lx0) < tol || Math.Abs(q.X - lx1) < tol) && (Math.Abs(q.Y - ly0) < tol || Math.Abs(q.Y - ly1) < tol))
               && Math.Abs(Area(poly) - (x1 - x0) * (y1 - y0)) < tol * (x1 - x0 + y1 - y0) * 2;
    }

    // ------------------------------------------------------------------
    // Đọc nét từ các bản vẽ liên kết đang hiện của tầng
    // ------------------------------------------------------------------
    /// <summary>Các đoạn thẳng (toạ độ mô hình) của bản vẽ liên kết gần điểm (x, y) trong bán kính r.</summary>
    public static List<(double X1, double Y1, double X2, double Y2)> SegmentsNear(Project p, string story, double x, double y, double r)
    {
        var res = new List<(double, double, double, double)>();
        foreach (var (l, g) in CadLinkSnap.Active(p, story))
        {
            var v = g.View(l);
            var vis = CadLinkSnap.LayerFilter(l, g);
            foreach (int i in v.SegmentsNear(x, y, r, vis, 2000))
                res.Add((v.Seg[4 * i], v.Seg[4 * i + 1], v.Seg[4 * i + 2], v.Seg[4 * i + 3]));
        }
        return res;
    }

    /// <summary>Các đa tuyến kín (không phải vòng tròn) của bản vẽ liên kết đang hiện, toạ độ mô hình.</summary>
    public static List<IList<(double X, double Y)>> ClosedPolylines(Project p, string story)
    {
        var res = new List<IList<(double X, double Y)>>();
        foreach (var (l, g) in CadLinkSnap.Active(p, story))
        {
            var vis = CadLinkSnap.LayerFilter(l, g);
            foreach (var pr in g.Prims)
            {
                if (!pr.Closed || pr.IsCircle || pr.Xs.Length < 3) continue;
                if (vis != null && !vis(pr.Layer)) continue;
                var poly = new List<(double X, double Y)>(pr.Xs.Length);
                for (int i = 0; i < pr.Xs.Length; i++) poly.Add(l.ToModel(pr.Xs[i], pr.Ys[i]));
                res.Add(poly);
            }
        }
        return res;
    }

    // ------------------------------------------------------------------
    // Tạo đối tượng mô hình
    // ------------------------------------------------------------------
    /// <summary>Tiết diện cho dầm pick: section có sẵn đúng bề rộng (±5 mm) → dùng; không thì tạo b×h (b làm tròn 10 mm, h = h của
    /// tiết diện mặc định / 300 mm). width = 0 → dùng defaultSection.</summary>
    public static string SectionFor(Project p, double width, string defaultSection, out string note)
    {
        note = "";
        var def = p.FrameSections.FirstOrDefault(s => s.Name == defaultSection);
        if (width <= 0 || def == null && p.FrameSections.Count == 0) return defaultSection;
        double b = Math.Round(width * 100) / 100.0;
        if (def != null && Math.Abs(def.B - b) <= 0.005) return def.Name;
        double h = def?.H > 0 ? def.H : 0.30;
        var same = p.FrameSections.FirstOrDefault(s => s.Shape == SectionShape.RectConcrete && Math.Abs(s.B - b) <= 0.005 && Math.Abs(s.H - h) <= 0.005);
        if (same != null) return same.Name;
        var r = SectionLibrary.AddRect(p, null, b * 1000, h * 1000, def?.Material ?? p.Concrete.ConcreteBeam);
        if (!r.Ok) return defaultSection;
        var made = p.FrameSections.Last();
        note = $" (tạo tiết diện {made.Name} theo bề rộng đo được {b * 1000:0} mm)";
        return made.Name;
    }

    /// <summary>Tạo dầm theo kết quả pick (dầm có sẵn trùng đoạn được bỏ qua; nút giao được chia).</summary>
    public static EditResult CreateBeam(Project p, string story, CadBeamPick pick, string defaultSection)
    {
        if (pick == null) return EditResult.Fail("Không nhận được nét dầm tại vị trí bấm – bấm giữa hai mép dầm / tường hoặc vào một nét.");
        string sec = SectionFor(p, pick.Width, defaultSection, out string note);
        var r = BeamTools.AddBeamExact(p, story, pick.X1, pick.Y1, pick.X2, pick.Y2, sec, "CAD", true);
        if (r.Ok) r.Message += note + " – " + pick.Note + ".";
        return r;
    }

    public enum LoopTarget { Slab, Stair, Void, BeamsOnly }

    /// <summary>Từ đa giác kín: dầm bao các cạnh (phần chưa có), rồi tạo ô sàn / ô cầu thang / lỗ giếng trời (ô chữ nhật).</summary>
    public static EditResult CreateFromLoop(Project p, string story, IList<(double X, double Y)> poly, LoopTarget target, string beamSection)
    {
        if (poly == null || poly.Count < 3) return EditResult.Fail("Không tìm thấy đa tuyến kín tại vị trí bấm.");
        var res = new EditResult { Ok = true };
        int beams = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            if (Hyp(b.X - a.X, b.Y - a.Y) < 0.05) continue;
            var r = BeamTools.AddBeamExact(p, story, a.X, a.Y, b.X, b.Y, beamSection, "CAD", false);
            if (r.Ok) { beams++; res.NewFrames.AddRange(r.NewFrames); }
        }
        if (beams > 0) BeamTools.SplitSlabsByBeams(p, story);
        double cx = poly.Average(q => q.X), cy = poly.Average(q => q.Y);
        if (!Inside(poly, cx, cy))
        {
            // đa giác lõm: lấy điểm trong gần trọng tâm
            var tri = (X: (poly[0].X + poly[1].X + poly[2].X) / 3, Y: (poly[0].Y + poly[1].Y + poly[2].Y) / 3);
            cx = tri.X; cy = tri.Y;
        }
        string what = "";
        if (target != LoopTarget.BeamsOnly)
        {
            EditResult r2 = target switch
            {
                LoopTarget.Slab => ModelEdit.AddSlab(p, story, cx, cy),
                LoopTarget.Stair => ModelEdit.AddOpening(p, story, cx, cy, OpeningKind.Stair),
                _ => ModelEdit.AddOpening(p, story, cx, cy, OpeningKind.Void)
            };
            if (r2.Ok) { res.NewSlabs.AddRange(r2.NewSlabs); what = " " + r2.Message; }
            else what = " – " + r2.Message;
        }
        res.Message = (beams > 0 ? $"Đã thêm dầm bao theo đa tuyến ({beams} cạnh mới)." : "Các cạnh đã có dầm.") + what;
        p.InvalidateIndex();
        return res;
    }
}
