using KetCauVXT.Model;

namespace KetCauVXT.Mep;

// =====================================================================
// THOÁT NƯỚC ĐI DƯỚI SÀN, CÓ ĐỘ DỐC
// - Mặc định ống thoát (thải / xám / mưa) KHÔNG đi trong tường: từ thiết bị chỉ có đoạn đứng ngắn xuyên sàn, đoạn ngang
//   treo dưới đáy sàn (trong không gian trần tầng dưới) – hoặc trong lớp hạ cốt nếu ô sàn hạ cốt đủ sâu – dốc về ống đứng
//   (hộp gen kỹ thuật). Ép đi trong tường (MepRun.DrainInWall) → cảnh báo "không khuyến khích".
// - Độ dốc mặc định theo Ø (TCVN 4474 – thực hành): Ø ≤ 50: 3 %; Ø60…Ø90: 2 %; Ø ≥ 110: 2 % (tối thiểu 1 %).
//   Cao độ tim ống giảm dần từ đầu tuyến (thiết bị xa nhất) theo chiều dài ngang: z = z₀ − i·s.
// - Phụ kiện thoát: đổi hướng ≥ 60° = 2 chếch 45° (không dùng cút 90°), nhánh = tê xiên 45° (chữ Y) + chếch,
//   ống kiểm tra (nắp thăm) ở chân ống đứng và mỗi CleanoutSpacing m tuyến ngang.
// =====================================================================
public static class MepDrainage
{
    public const string StackFamily = "Ống đứng thoát nước";

    public static bool IsStack(MepFamily f) => f?.Symbol == "ongdung";
    public static bool IsStack(MepModel m, MepDevice d) => d != null && IsStack(m.FamilyOf(d));

    // ------------------------------------------------------------------
    // Độ dốc
    // ------------------------------------------------------------------
    /// <summary>Độ dốc mặc định theo đường kính ống (mm).</summary>
    public static double DefaultSlope(MepSettings s, double dia) =>
        dia <= 50 + 1e-9 ? s.DrainSlopeSmall : dia < 110 - 1e-9 ? s.DrainSlopeMedium : s.DrainSlopeLarge;

    /// <summary>Độ dốc tối thiểu theo Ø (TCVN 4474 giản lược): Ø ≤ 50: 2 %; Ø60…Ø90: 1,5 %; Ø ≥ 110: 1 %.</summary>
    public static double MinSlope(double dia) => dia <= 50 + 1e-9 ? 0.02 : dia < 110 - 1e-9 ? 0.015 : 0.01;

    /// <summary>Độ dốc dùng cho tuyến: nhập tay (Slope &gt; 0) hoặc mặc định theo Ø.</summary>
    public static double SlopeOf(Project p, MepRun r) => r.Slope > 0 ? r.Slope : DefaultSlope(p.Mep.Settings, r.Size, r.System);

    /// <summary>Độ dốc mặc định theo hệ thống + Ø: luật riêng của hệ (MepSettings.SlopeRules – DiaMax nhỏ nhất còn ≥ Ø, 0 = mọi Ø) nếu có,
    /// không thì theo Ø (DefaultSlope).</summary>
    public static double DefaultSlope(MepSettings s, double dia, MepSystemKind system)
    {
        var rule = (s.SlopeRules ?? new List<MepSlopeRule>())
            .Where(q => q.System == system && q.Slope > 0 && (q.DiaMax <= 0 || dia <= q.DiaMax + 1e-9))
            .OrderBy(q => q.DiaMax <= 0 ? double.MaxValue : q.DiaMax).FirstOrDefault();
        return rule != null ? rule.Slope : DefaultSlope(s, dia);
    }

    /// <summary>Khoảng cách quang treo ống ngang (m) theo Ø: ≤ 50: 0,8; ≤ 90: 1,0; ≥ 110: 1,2.</summary>
    public static double HangerSpacing(double dia) => dia <= 50 + 1e-9 ? 0.8 : dia < 110 - 1e-9 ? 1.0 : 1.2;

    // ------------------------------------------------------------------
    // Sàn tại một điểm
    // ------------------------------------------------------------------
    /// <summary>Ô sàn của tầng chứa điểm (x, y) (theo đa giác nút); null nếu không có.</summary>
    public static Slab SlabAt(Project p, string story, double x, double y)
    {
        foreach (var s in p.Slabs.Where(s => s.Story == story && s.Nodes.Count >= 3))
        {
            var pts = new List<(double X, double Y)>();
            foreach (var id in s.Nodes)
            {
                var n = p.Nodes.FirstOrDefault(q => q.Id == id);
                if (n != null) pts.Add((n.X, n.Y));
            }
            if (pts.Count >= 3 && Inside(pts, x, y)) return s;
        }
        return null;
    }

    private static bool Inside(List<(double X, double Y)> poly, double x, double y)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if (((a.Y > y) != (b.Y > y)) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y + 1e-15) + a.X) c = !c;
        }
        // điểm đúng trên cạnh: coi là trong
        if (!c)
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if (MepGeom.Project(x, y, poly[j].X, poly[j].Y, poly[i].X, poly[i].Y).Dist < 1e-6) return true;
        return c;
    }

    /// <summary>Chiều dày ô sàn tại điểm (m); không có ô sàn: chiều dày ô sàn bất kỳ của tầng hoặc 0,12.</summary>
    public static double SlabThicknessAt(Project p, string story, double x, double y)
    {
        var s = SlabAt(p, story, x, y) ?? p.Slabs.FirstOrDefault(q => q.Story == story);
        double t = s == null ? 0 : p.SlabSections.FirstOrDefault(q => q.Name == s.Section)?.Thickness ?? 0;
        return t > 1e-6 ? t : 0.12;
    }

    /// <summary>Tầng thấp nhất (nền – ống đi trong lớp đất đắp dưới sàn nền, không treo).</summary>
    public static bool IsLowestStory(Project p, string story)
    {
        if (p.Stories.Count == 0) return true;
        double min = p.Stories.Min(s => s.Elevation);
        var st = p.Stories.FirstOrDefault(s => s.Name == story);
        return st == null || st.Elevation <= min + 1e-9;
    }

    /// <summary>Chế độ đi ống thoát của tuyến.</summary>
    public enum DrainMode { DuoiSan, HaCot, TrongSan, TrongTuong }

    public static DrainMode ModeOf(Project p, MepRun r, (double X, double Y) start)
    {
        if (r.DrainInWall) return DrainMode.TrongTuong;
        if (!p.Mep.Settings.DrainBelowSlab) return DrainMode.TrongSan;
        var sl = SlabAt(p, r.Story, start.X, start.Y);
        if (sl != null && sl.LevelOffset < 0 && -sl.LevelOffset >= r.Size / 1000 + 0.05 - 1e-9) return DrainMode.HaCot;
        return DrainMode.DuoiSan;
    }

    /// <summary>Cao độ tim ống ở đầu tuyến ngang (m tuyệt đối).</summary>
    public static double StartZ(Project p, MepRun r, DrainMode mode, (double X, double Y) start)
    {
        var set = p.Mep.Settings;
        double el = MepRouting.StoryElevation(p, r.Story), rad = r.Size / 2000;
        return mode switch
        {
            DrainMode.HaCot => el - 0.02 - rad,
            DrainMode.TrongSan => el + set.DrainLevel,
            DrainMode.TrongTuong => el + 0.15,
            _ => el - SlabThicknessAt(p, r.Story, start.X, start.Y) - set.DrainHangerGap - rad
        };
    }

    // ------------------------------------------------------------------
    // Đi tuyến thoát
    // ------------------------------------------------------------------
    /// <summary>Điểm xuống của thiết bị: thiết bị gắn tường → trước mặt tường (phía phòng) cách mặt tường Ø/2 + 20 mm (đoạn đứng không nằm trong tường);
    /// thiết bị tự do → tại thiết bị.</summary>
    public static (double X, double Y) DropPoint(Project p, MepDevice d, double dia)
    {
        var pos = MepRouting.DevicePos(p, d);
        if (pos.Wall == null) return (pos.X, pos.Y);
        double L = Math.Max(1e-9, pos.Wall.Chord);
        double ux = (pos.Wall.X2 - pos.Wall.X1) / L, uy = (pos.Wall.Y2 - pos.Wall.Y1) / L;
        int s = d.Side >= 0 ? 1 : -1;
        double nx = -uy * s, ny = ux * s, off = dia / 2000 + 0.02;
        return (pos.X + nx * off, pos.Y + ny * off);
    }

    /// <summary>Đi tuyến thoát từ thiết bị a (đầu dốc) tới b (cuối dốc – ống đứng / phễu thu). Trả false nếu ép trong tường mà không có tường liền.</summary>
    public static bool Route(Project p, MepRun r, MepDevice a, MepDevice b)
    {
        var m = p.Mep;
        r.Path = new List<MepPt>();
        var pa = MepRouting.DevicePos(p, a); var pb = MepRouting.DevicePos(p, b);
        bool stackB = IsStack(m, b);
        bool ok = true;
        var plan = new List<(double X, double Y, int Wall)>();
        if (r.DrainInWall)
        {
            var ca = MepRouting.DeviceChase(p, a); var cb = MepRouting.DeviceChase(p, b);
            plan.Add((ca.X, ca.Y, a.WallId));
            var ea = EntryWall(p, a, ca); var eb = EntryWall(p, b, cb);
            List<(double X, double Y, int Wall)> mid = null;
            if (ea.Wall != null && eb.Wall != null) mid = MepRouting.WallPath(p, a.Story, ea.Wall, ea.S, eb.Wall, eb.S);
            if (mid == null) { ok = false; Ortho(plan, ca.X, ca.Y, cb.X, cb.Y); }
            else
            {
                if (a.WallId == 0) Ortho(plan, ca.X, ca.Y, mid[0].X, mid[0].Y);
                foreach (var q in mid) AddPlan(plan, (q.X, q.Y, q.Wall));
                if (b.WallId == 0) Ortho(plan, mid[^1].X, mid[^1].Y, cb.X, cb.Y);
            }
            AddPlan(plan, (cb.X, cb.Y, b.WallId));
        }
        else
        {
            var da = DropPoint(p, a, r.Size);
            var db = stackB ? (pb.X, pb.Y) : DropPoint(p, b, r.Size);
            plan.Add((da.X, da.Y, 0));
            if (r.ManualPoints != null && r.ManualPoints.Count > 0)
            {
                foreach (var q in r.ManualPoints.Where(q => q != null && q.Length >= 2))
                {
                    var nw = MepRouting.NearestWall(p, a.Story, q[0], q[1], 0.05);
                    AddPlan(plan, (q[0], q[1], nw.Wall?.Id ?? 0));
                }
                AddPlan(plan, (db.X, db.Y, 0));
            }
            else Ortho(plan, da.X, da.Y, db.X, db.Y);
        }

        var mode = ModeOf(p, r, (plan[0].X, plan[0].Y));
        double z0 = StartZ(p, r, mode, (plan[0].X, plan[0].Y));
        double i = SlopeOf(p, r);
        // đoạn đứng từ thiết bị xuống đầu tuyến ngang
        int vw = r.DrainInWall ? a.WallId : 0;
        r.Path.Add(new MepPt(plan[0].X, plan[0].Y, pa.Z, vw));
        Add(r.Path, new MepPt(plan[0].X, plan[0].Y, z0, vw));
        double s = 0;
        for (int k = 1; k < plan.Count; k++)
        {
            s += MepGeom.Dist2D(plan[k - 1].X, plan[k - 1].Y, plan[k].X, plan[k].Y);
            Add(r.Path, new MepPt(plan[k].X, plan[k].Y, z0 - i * s, plan[k].Wall));
        }
        // cuối tuyến: vào ống đứng ngay tại cao độ tim ống; thiết bị thường (phễu thu…) → đoạn đứng lên tới thiết bị
        if (!stackB) Add(r.Path, new MepPt(plan[^1].X, plan[^1].Y, pb.Z, r.DrainInWall ? b.WallId : 0));
        return ok;
    }

    private static (WallLine Wall, double S) EntryWall(Project p, MepDevice d, (double X, double Y) c)
    {
        if (d.WallId > 0)
        {
            var w = p.Walls.FirstOrDefault(x => x.Id == d.WallId);
            if (w != null) return (w, Math.Clamp(d.Offset, 0, w.Chord));
        }
        var nw = MepRouting.NearestWall(p, d.Story, c.X, c.Y, 3.0);
        return nw.Wall == null ? (null, 0) : (nw.Wall, nw.Offset);
    }

    private static void AddPlan(List<(double X, double Y, int Wall)> plan, (double X, double Y, int Wall) q)
    {
        if (plan.Count > 0 && MepGeom.Dist2D(plan[^1].X, plan[^1].Y, q.X, q.Y) < 1e-6) return;
        plan.Add(q);
    }

    private static void Ortho(List<(double X, double Y, int Wall)> plan, double x1, double y1, double x2, double y2)
    {
        AddPlan(plan, (x1, y1, 0));
        if (Math.Abs(x2 - x1) > 1e-6 && Math.Abs(y2 - y1) > 1e-6) AddPlan(plan, (x2, y1, 0));
        AddPlan(plan, (x2, y2, 0));
    }

    private static void Add(List<MepPt> path, MepPt q)
    {
        if (path.Count > 0 && MepGeom.Dist(path[^1], q) < 1e-6) return;
        path.Add(q);
    }

    // ------------------------------------------------------------------
    // Mặt cắt dọc tuyến (cao độ đáy ống)
    // ------------------------------------------------------------------
    /// <summary>Phần ngang của tuyến (từ điểm thứ 2 tới trước đoạn đứng cuối): (s – chiều dài ngang tích luỹ, cao độ tim, cao độ đáy ống).</summary>
    public static List<(double S, double Z, double Invert, double X, double Y)> Profile(MepRun r)
    {
        var res = new List<(double, double, double, double, double)>();
        var path = r.Path; if (path == null || path.Count < 2) return res;
        double rad = r.Size / 2000, s = 0;
        int first = 1, last = path.Count - 1;
        // bỏ đoạn đứng cuối (lên thiết bị) nếu có
        if (last >= 1 && MepGeom.Dist2D(path[last].X, path[last].Y, path[last - 1].X, path[last - 1].Y) < 1e-6) last--;
        for (int k = first; k <= last; k++)
        {
            if (k > first) s += MepGeom.Dist2D(path[k - 1].X, path[k - 1].Y, path[k].X, path[k].Y);
            res.Add((s, path[k].Z, path[k].Z - rad, path[k].X, path[k].Y));
        }
        return res;
    }

    /// <summary>Chiều dài ngang (mặt bằng) của tuyến.</summary>
    public static double HorizontalLength(MepRun r)
    {
        double L = 0;
        for (int k = 1; k < r.Path.Count; k++) L += MepGeom.Dist2D(r.Path[k - 1].X, r.Path[k - 1].Y, r.Path[k].X, r.Path[k].Y);
        return L;
    }

    // ------------------------------------------------------------------
    // Ống đứng
    // ------------------------------------------------------------------
    /// <summary>Đề xuất ống đứng thoát của tầng: tại đầu tường (góc phòng) gần trọng tâm các thiết bị thoát nhất, dịch vào phòng 0,15 m.
    /// Đã có ống đứng trên tầng → trả ống đứng đó.</summary>
    public static MepDevice ProposeStack(Project p, string story)
    {
        var m = p.Mep;
        var exist = m.Devices.FirstOrDefault(d => d.Story == story && IsStack(m, d));
        if (exist != null) return exist;
        var drains = m.Devices.Where(d => d.Story == story && (m.FamilyOf(d)?.DrainUnits ?? 0) > 0).ToList();
        if (drains.Count == 0) return null;
        double cx = drains.Average(d => MepRouting.DevicePos(p, d).X), cy = drains.Average(d => MepRouting.DevicePos(p, d).Y);
        var corners = p.Walls.Where(w => w.Story == story).SelectMany(w => new[] { (w.X1, w.Y1), (w.X2, w.Y2) }).ToList();
        double sx = cx, sy = cy;
        if (corners.Count > 0)
        {
            var c = corners.OrderBy(q => MepGeom.Dist2D(q.Item1, q.Item2, cx, cy)).First();
            double dx = Math.Sign(cx - c.Item1), dy = Math.Sign(cy - c.Item2);
            sx = c.Item1 + 0.15 * dx; sy = c.Item2 + 0.15 * dy;
        }
        return MepRouting.PlaceFree(p, StackFamily, story, sx, sy, 0);
    }

    /// <summary>Nối mọi thiết bị thoát chưa có tuyến thoát của tầng vào ống đứng (tự đề xuất nếu chưa có). Trả các tuyến mới.</summary>
    public static List<MepRun> ConnectToStack(Project p, string story)
    {
        var m = p.Mep; var res = new List<MepRun>();
        var stack = ProposeStack(p, story);
        if (stack == null) return res;
        foreach (var d in m.Devices.Where(d => d.Story == story && d.Id != stack.Id).ToList())
        {
            var fam = m.FamilyOf(d);
            if (fam == null || IsStack(fam)) continue;
            var con = fam.Connectors.FirstOrDefault(c => MepSystems.IsDrain(c.System));
            if (con == null) continue;
            if (m.Runs.Any(r => MepSystems.IsDrain(r.System) && (r.FromId == d.Id || r.ToId == d.Id))) continue;
            var r = MepRouting.Connect(p, d.Id, stack.Id, con.System, con.Size);
            r.AutoSize = false;
            res.Add(r);
        }
        return res;
    }

    // ------------------------------------------------------------------
    // Phụ kiện thoát
    // ------------------------------------------------------------------
    /// <summary>Phụ kiện dọc tuyến thoát: đổi hướng ≥ 60° → 2 chếch 45°; 10…60° → 1 chếch 45°; măng sông theo cây ống;
    /// ống kiểm tra mỗi CleanoutSpacing m chiều dài ngang.</summary>
    public static List<MepFitting> RunFittings(Project p, MepRun r)
    {
        var res = new List<MepFitting>();
        var path = r.Path;
        if (path == null || path.Count < 2) return res;
        for (int k = 1; k < path.Count - 1; k++)
        {
            double ang = MepRouting.TurnAngle(path[k - 1], path[k], path[k + 1]);
            if (ang < 10) continue;
            int n = ang >= 60 ? 2 : 1;
            for (int j = 0; j < n; j++)
                res.Add(new MepFitting { Kind = MepFittingKind.Chech45, System = r.System, Size = r.Size, X = path[k].X, Y = path[k].Y, Z = path[k].Z, RunId = r.Id, Story = r.Story });
        }
        double stock = p.Mep.Settings.StockPipe;
        for (int k = 1; k < path.Count; k++)
        {
            double L = MepGeom.Dist(path[k - 1], path[k]);
            int n = stock > 0 ? (int)Math.Floor((L - 1e-6) / stock) : 0;
            for (int j = 1; j <= n; j++)
            {
                double t = j * stock / L;
                res.Add(new MepFitting
                {
                    Kind = MepFittingKind.MangSong, System = r.System, Size = r.Size, RunId = r.Id, Story = r.Story,
                    X = path[k - 1].X + t * (path[k].X - path[k - 1].X), Y = path[k - 1].Y + t * (path[k].Y - path[k - 1].Y), Z = path[k - 1].Z + t * (path[k].Z - path[k - 1].Z)
                });
            }
        }
        double sp = p.Mep.Settings.CleanoutSpacing;
        if (sp > 0)
        {
            var prof = Profile(r);
            double Lh = prof.Count > 0 ? prof[^1].S : 0;
            int nc = (int)Math.Floor((Lh + 1e-9) / sp);
            for (int j = 1; j <= nc; j++)
            {
                double at = j * sp;
                var q = prof.LastOrDefault(x => x.S <= at + 1e-9);
                res.Add(new MepFitting { Kind = MepFittingKind.KiemTra, System = r.System, Size = r.Size, RunId = r.Id, Story = r.Story, X = q.X, Y = q.Y, Z = q.Z });
            }
        }
        return res;
    }

    /// <summary>Phụ kiện tại thiết bị cho nhóm tuyến thoát: ống đứng → mỗi nhánh vào 1 tê xiên (Y); chân ống đứng (tầng thấp nhất của cột ống) → 1 ống kiểm tra;
    /// điểm nối ≥ 3 tuyến → (n − 2) tê xiên + (n − 2) chếch 45°; 2 tuyến khác cỡ → côn thu.</summary>
    public static List<MepFitting> NodeFittings(Project p, MepDevice d, List<MepRun> runs, MepSystemKind sys)
    {
        var m = p.Mep; var res = new List<MepFitting>();
        var pos = MepRouting.DevicePos(p, d);
        double big = runs.Count > 0 ? runs.Max(r => r.Size) : 0, small = runs.Count > 0 ? runs.Min(r => r.Size) : 0;
        if (IsStack(m, d))
        {
            double ds = m.FamilyOf(d)?.Size > 0 ? m.FamilyOf(d).Size : 110;
            foreach (var r in runs)
                res.Add(new MepFitting { Kind = MepFittingKind.TeXien, System = sys, Size = Math.Max(ds, r.Size), Size2 = r.Size, X = pos.X, Y = pos.Y, Z = r.Path.Count > 0 ? r.Path[^1].Z : pos.Z, DeviceId = d.Id, Story = d.Story });
            return res;
        }
        if (runs.Count >= 3)
        {
            for (int k = 0; k < runs.Count - 2; k++)
            {
                res.Add(new MepFitting { Kind = MepFittingKind.TeXien, System = sys, Size = big, Size2 = small, X = pos.X, Y = pos.Y, Z = pos.Z, DeviceId = d.Id, Story = d.Story });
                res.Add(new MepFitting { Kind = MepFittingKind.Chech45, System = sys, Size = small, X = pos.X, Y = pos.Y, Z = pos.Z, DeviceId = d.Id, Story = d.Story });
            }
        }
        else if (runs.Count == 2 && Math.Abs(runs[0].Size - runs[1].Size) > 1e-6)
            res.Add(new MepFitting { Kind = MepFittingKind.Con, System = sys, Size = big, Size2 = small, X = pos.X, Y = pos.Y, Z = pos.Z, DeviceId = d.Id, Story = d.Story });
        return res;
    }

    /// <summary>Ống kiểm tra ở chân mỗi cột ống đứng (ống đứng ở tầng thấp nhất trong các ống đứng cùng vị trí ±0,1 m).</summary>
    public static List<MepFitting> StackBaseCleanouts(Project p)
    {
        var m = p.Mep; var res = new List<MepFitting>();
        var stacks = m.Devices.Where(d => IsStack(m, d)).ToList();
        foreach (var d in stacks)
        {
            double el = MepRouting.StoryElevation(p, d.Story);
            bool lower = stacks.Any(o => o.Id != d.Id && MepGeom.Dist2D(o.X, o.Y, d.X, d.Y) < 0.1 && MepRouting.StoryElevation(p, o.Story) < el - 1e-9);
            if (lower) continue;
            double ds = m.FamilyOf(d)?.Size > 0 ? m.FamilyOf(d).Size : 110;
            res.Add(new MepFitting { Kind = MepFittingKind.KiemTra, System = MepSystemKind.ThoatNuocThai, Size = ds, X = d.X, Y = d.Y, Z = el, DeviceId = d.Id, Story = d.Story });
        }
        return res;
    }

    // ------------------------------------------------------------------
    // Cảnh báo thoát nước
    // ------------------------------------------------------------------
    /// <summary>Cảnh báo riêng tuyến thoát: THOAT_TRONG_TUONG (ép đi tường), DO_DOC (độ dốc nhập ngoài khoảng), KHONG_GIAN_TRAN (ống thấp hơn trần giả),
    /// HA_COT (vượt khỏi lớp hạ cốt), THONG_HOI (gợi ý ống thông hơi cho ống đứng).</summary>
    public static List<MepWarning> Check(Project p)
    {
        var m = p.Mep; var set = m.Settings; var list = new List<MepWarning>();
        foreach (var r in m.Runs.Where(r => MepSystems.IsDrain(r.System)))
        {
            var q0 = r.Path.Count > 0 ? r.Path[0] : default;
            if (r.DrainInWall)
                list.Add(new MepWarning
                {
                    Code = "THOAT_TRONG_TUONG", RunId = r.Id, Story = r.Story, X = q0.X, Y = q0.Y, Severity = "Cảnh báo",
                    Message = $"Tuyến thoát {r.Id} Ø{r.Size:0} đi trong tường – không khuyến khích (khó thông tắc, đục tường làm yếu kết cấu, dễ thấm)",
                    Fix = "Bỏ \"đi trong tường\": ống thoát đi dưới sàn tới ống đứng"
                });
            if (r.Slope > 0 && (r.Slope < MinSlope(r.Size) - 1e-9 || r.Slope > set.DrainSlopeMax + 1e-9))
                list.Add(new MepWarning
                {
                    Code = "DO_DOC", RunId = r.Id, Story = r.Story, X = q0.X, Y = q0.Y, Severity = "Cảnh báo",
                    Message = $"Tuyến thoát {r.Id} Ø{r.Size:0}: độ dốc {r.Slope * 100:0.##} % ngoài khoảng {MinSlope(r.Size) * 100:0.#}…{set.DrainSlopeMax * 100:0.#} %",
                    Fix = $"Dùng độ dốc mặc định {DefaultSlope(set, r.Size, r.System) * 100:0.#} %"
                });
            var prof = Profile(r);
            if (prof.Count == 0) continue;
            var mode = ModeOf(p, r, (prof[0].X, prof[0].Y));
            double el = MepRouting.StoryElevation(p, r.Story);
            double rad = r.Size / 2000, bottom = prof.Min(x => x.Invert);
            if (mode == DrainMode.DuoiSan && !IsLowestStory(p, r.Story))
            {
                double soffit = el - SlabThicknessAt(p, r.Story, prof[0].X, prof[0].Y);
                double limit = soffit - set.CeilingVoid;
                if (bottom < limit - 1e-9)
                    list.Add(new MepWarning
                    {
                        Code = "KHONG_GIAN_TRAN", RunId = r.Id, Story = r.Story, X = prof[^1].X, Y = prof[^1].Y, Severity = "Cảnh báo",
                        Message = $"Tuyến thoát {r.Id}: đáy ống cuối tuyến {bottom:0.###} thấp hơn trần giả ({limit:0.###} = đáy sàn − {set.CeilingVoid:0.##} m) – dài {prof[^1].S:0.##} m dốc {SlopeOf(p, r) * 100:0.#} %",
                        Fix = "Thêm ống đứng gần hơn / rút ngắn tuyến / hạ trần giả"
                    });
            }
            if (mode == DrainMode.HaCot)
            {
                var sl = SlabAt(p, r.Story, prof[0].X, prof[0].Y);
                double top = el + (sl?.LevelOffset ?? 0);
                if (bottom < top - 1e-9)
                    list.Add(new MepWarning
                    {
                        Code = "HA_COT", RunId = r.Id, Story = r.Story, X = prof[^1].X, Y = prof[^1].Y, Severity = "Cảnh báo",
                        Message = $"Tuyến thoát {r.Id}: đáy ống {bottom:0.###} thấp hơn mặt sàn hạ cốt {top:0.###} – lớp hạ cốt không đủ sâu cho chiều dài tuyến",
                        Fix = "Hạ cốt sâu hơn hoặc đưa ống đứng gần thiết bị"
                    });
            }
            _ = rad;
        }
        foreach (var d in m.Devices.Where(d => IsStack(m, d)))
        {
            if (m.Devices.Any(o => o.Id != d.Id && IsStack(m, o) && MepGeom.Dist2D(o.X, o.Y, d.X, d.Y) < 0.1 &&
                                   MepRouting.StoryElevation(p, o.Story) > MepRouting.StoryElevation(p, d.Story) + 1e-9)) continue;   // chỉ ống trên cùng
            double ds = m.FamilyOf(d)?.Size > 0 ? m.FamilyOf(d).Size : 110;
            list.Add(new MepWarning
            {
                Code = "THONG_HOI", DeviceId = d.Id, Story = d.Story, X = d.X, Y = d.Y, Severity = "Gợi ý",
                Message = $"{MepUpdater.Label(m, d)}: nối ống thông hơi Ø{Math.Min(ds, 90):0} từ đỉnh ống đứng, vượt mái ≥ 0,7 m (cách cửa sổ ≥ 3 m)",
                Fix = ""
            });
        }
        return list;
    }

    /// <summary>Số quang treo cho tuyến thoát treo dưới sàn (0 với tầng thấp nhất – ống chôn trong lớp đất nền).</summary>
    public static int Hangers(Project p, MepRun r)
    {
        if (!MepSystems.IsDrain(r.System) || r.DrainInWall || IsLowestStory(p, r.Story)) return 0;
        var prof = Profile(r);
        if (prof.Count == 0) return 0;
        if (ModeOf(p, r, (prof[0].X, prof[0].Y)) != DrainMode.DuoiSan) return 0;
        double Lh = prof[^1].S;
        if (Lh < 1e-6) return 0;
        return (int)Math.Ceiling(Lh / HangerSpacing(r.Size) - 1e-9) + 1;
    }
}
