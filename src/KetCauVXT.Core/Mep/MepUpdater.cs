using KetCauVXT.Model;

namespace KetCauVXT.Mep;

/// <summary>Kiến trúc là "xref" của bộ môn điện nước: khi tường / cửa thay đổi, Reconcile tự cập nhật vị trí thiết bị
/// (giữ vị trí dọc tường, kẹp vào trong tường), đi lại tuyến, tính lại phụ kiện và lập danh sách CẢNH BÁO:
/// tường bị xoá (thiết bị mồ côi), thiết bị nằm trong ô cửa, tường mỏng không chôn được ống, thiết bị chồng nhau,
/// tuyến xuyên cột / dầm (cần ống lồng).</summary>
public static class MepUpdater
{
    /// <summary>Kết quả đối chiếu.</summary>
    public sealed class Result
    {
        public List<MepWarning> Warnings { get; } = new();
        /// <summary>Tường đã đổi (dời, đổi dài, đổi dày, thêm / bớt cửa) kể từ lần cập nhật trước</summary>
        public List<int> ChangedWalls { get; } = new();
        public List<int> DeletedWalls { get; } = new();
        /// <summary>Thiết bị đã dời theo tường</summary>
        public List<int> MovedDevices { get; } = new();
        public int Rerouted { get; set; }
        public string Summary => $"{ChangedWalls.Count} tường đổi, {DeletedWalls.Count} tường xoá, {MovedDevices.Count} thiết bị dời theo, {Rerouted} tuyến đi lại, {Warnings.Count} cảnh báo";
    }

    private static string OpeningsKey(Project p, WallLine w) =>
        string.Join(";", (w.Openings ?? new()).OrderBy(o => o.Id).Select(o => $"{o.Id}:{o.Type}:{o.Offset:0.###}"));

    public static MepWallSnap Snap(Project p, WallLine w) => new()
    {
        WallId = w.Id, X1 = w.X1, Y1 = w.Y1, X2 = w.X2, Y2 = w.Y2, Thickness = MepRouting.WallInfo(p, w).Total, Openings = OpeningsKey(p, w)
    };

    /// <summary>Có thay đổi kiến trúc liên quan tới điện nước kể từ lần cập nhật trước?</summary>
    public static bool HasArchChanges(Project p)
    {
        var m = p.Mep;
        var hosted = m.Devices.Where(d => d.WallId > 0).Select(d => d.WallId).ToHashSet();
        foreach (var s in m.WallSnaps.Where(s => hosted.Contains(s.WallId)))
        {
            var w = p.Walls.FirstOrDefault(x => x.Id == s.WallId);
            if (w == null || Changed(p, s, w)) return true;
        }
        return hosted.Any(id => m.WallSnaps.All(s => s.WallId != id));
    }

    private static bool Changed(Project p, MepWallSnap s, WallLine w)
    {
        var n = Snap(p, w);
        return Math.Abs(n.X1 - s.X1) > 1e-6 || Math.Abs(n.Y1 - s.Y1) > 1e-6 || Math.Abs(n.X2 - s.X2) > 1e-6 || Math.Abs(n.Y2 - s.Y2) > 1e-6
            || Math.Abs(n.Thickness - s.Thickness) > 1e-6 || n.Openings != s.Openings;
    }

    /// <summary>Đối chiếu với kiến trúc hiện tại: cập nhật thiết bị, tuyến, phụ kiện; lập cảnh báo (lưu vào Mep.Warnings).</summary>
    public static Result Reconcile(Project p)
    {
        var m = p.Mep; var res = new Result();
        foreach (var s in m.WallSnaps)
        {
            var w = p.Walls.FirstOrDefault(x => x.Id == s.WallId);
            if (w == null) res.DeletedWalls.Add(s.WallId);
            else if (Changed(p, s, w)) res.ChangedWalls.Add(s.WallId);
        }

        // 1. thiết bị gắn tường: theo tường (giữ vị trí dọc tường, kẹp trong tường)
        foreach (var d in m.Devices)
        {
            if (d.WallId <= 0) continue;
            var w = p.Walls.FirstOrDefault(x => x.Id == d.WallId);
            if (w == null)
            {
                res.Warnings.Add(new MepWarning
                {
                    Code = "TUONG_XOA", DeviceId = d.Id, WallId = d.WallId, Story = d.Story, X = d.X, Y = d.Y,
                    Message = $"{Label(m, d)}: tường {d.WallId} đã bị xoá – thiết bị không còn chỗ gắn",
                    Fix = "Gắn vào tường gần nhất"
                });
                continue;
            }
            var fam = m.FamilyOf(d);
            double half = Math.Min((fam?.W ?? 0.1) / 2, w.Chord / 2);
            double off = Math.Clamp(d.Offset, half, Math.Max(half, w.Chord - half));
            if (Math.Abs(off - d.Offset) > 1e-9) d.Offset = off;
            var pos = MepRouting.DevicePos(p, d);
            if (Math.Abs(pos.X - d.X) > 1e-6 || Math.Abs(pos.Y - d.Y) > 1e-6) res.MovedDevices.Add(d.Id);
            d.X = pos.X; d.Y = pos.Y;
            if (d.Story != w.Story) d.Story = w.Story;
        }

        // 2. đi lại tuyến + phụ kiện
        foreach (var r in m.Runs)
        {
            var before = r.Path.Select(q => q.ToString()).ToList();
            bool ok = MepRouting.Route(p, r);
            if (!before.SequenceEqual(r.Path.Select(q => q.ToString()))) res.Rerouted++;
            if (!ok)
                res.Warnings.Add(new MepWarning
                {
                    Code = "KHONG_TUYEN", RunId = r.Id, Story = r.Story, X = r.Path.FirstOrDefault().X, Y = r.Path.FirstOrDefault().Y,
                    Message = $"Tuyến {r.Id} ({MepSystems.Name(r.System)}): không có tường liền nối 2 thiết bị – đi thẳng ngoài tường",
                    Fix = "Vẽ tay tuyến hoặc thêm tường"
                });
        }
        try { res.Rerouted += ConduitSpacing.Spread(p); } catch (Exception) { }   // giãn ống gen song song quá sát (Mep/MepRound5.cs)
        var fit = new List<MepFitting>();
        foreach (var r in m.Runs) fit.AddRange(MepRouting.RunFittings(p, r));
        fit.AddRange(MepRouting.NodeFittings(p));
        m.Fittings = fit;

        // 3. cảnh báo
        res.Warnings.AddRange(CheckOpenings(p));
        res.Warnings.AddRange(CheckThinWalls(p));
        res.Warnings.AddRange(CheckOverlaps(p));
        res.Warnings.AddRange(CheckStructure(p));
        res.Warnings.AddRange(MepDrainage.Check(p));
        try { res.Warnings.AddRange(HvacClashCheck.Warnings(p)); } catch (Exception) { }
        try { res.Warnings.AddRange(HvacBeamAvoid.Warnings(p)); } catch (Exception) { }

        // 4. ảnh chụp mới
        var used = m.Devices.Where(d => d.WallId > 0).Select(d => d.WallId).Concat(m.Runs.SelectMany(r => r.Path.Select(q => q.WallId))).Where(id => id > 0).ToHashSet();
        m.WallSnaps = p.Walls.Where(w => used.Contains(w.Id)).Select(w => Snap(p, w)).ToList();
        m.Warnings = res.Warnings;
        return res;
    }

    public static string Label(MepModel m, MepDevice d) => string.IsNullOrWhiteSpace(d.Mark) ? $"{d.Family} #{d.Id}" : d.Mark;

    /// <summary>Thiết bị gắn tường nằm trong ô cửa (theo dọc tường và chiều cao).</summary>
    public static List<MepWarning> CheckOpenings(Project p)
    {
        var m = p.Mep; var list = new List<MepWarning>();
        foreach (var d in m.Devices.Where(d => d.WallId > 0))
        {
            var w = p.Walls.FirstOrDefault(x => x.Id == d.WallId);
            if (w == null || w.Openings == null) continue;
            var fam = m.FamilyOf(d);
            double hw = (fam?.W ?? 0.1) / 2, hh = (fam?.H ?? 0.1) / 2;
            foreach (var o in w.Openings)
            {
                var t = WallAssembly.TypeOf(p, o);
                if (t == null) continue;
                bool alongHit = d.Offset + hw > o.Offset - t.Width / 2 + 1e-9 && d.Offset - hw < o.Offset + t.Width / 2 - 1e-9;
                bool upHit = d.Height + hh > t.Sill + 1e-9 && d.Height - hh < t.Sill + t.Height - 1e-9;
                if (alongHit && upHit)
                    list.Add(new MepWarning
                    {
                        Code = "TRONG_CUA", DeviceId = d.Id, WallId = w.Id, Story = d.Story, X = d.X, Y = d.Y,
                        Message = $"{Label(m, d)} nằm trong ô cửa {t.Tag} trên tường {w.Id} – không lắp được",
                        Fix = "Dời ra cạnh ô cửa"
                    });
            }
        }
        return list;
    }

    /// <summary>Tường quá mỏng để chôn ống / ống gen (Ø ngoài &gt; ½ lõi + 1 lớp trát).</summary>
    public static List<MepWarning> CheckThinWalls(Project p)
    {
        var m = p.Mep; var list = new List<MepWarning>();
        foreach (var r in m.Runs)
        {
            double dia = r.Kind == MepRunKind.Ong ? r.Size : (r.ConduitSize > 0 ? r.ConduitSize : MepSizing.ConduitFor(r.Size, Math.Max(1, r.Conductors)));
            foreach (var wid in r.Path.Select(q => q.WallId).Where(id => id > 0).Distinct())
            {
                var w = p.Walls.FirstOrDefault(x => x.Id == wid);
                if (w == null) continue;
                var info = MepRouting.WallInfo(p, w);
                if (dia / 1000 > info.MaxEmbed + 1e-9)
                {
                    var q = r.Path.First(x => x.WallId == wid);
                    list.Add(new MepWarning
                    {
                        Code = "TUONG_MONG", RunId = r.Id, WallId = wid, Story = r.Story, X = q.X, Y = q.Y,
                        Message = $"Tuyến {r.Id} Ø{dia:0} không chôn được trong tường {w.Id} dày {info.Total * 1000:0} mm (lõi {info.Core * 1000:0}) – tối đa Ø{info.MaxEmbed * 1000:0}",
                        Fix = "Đi ống nổi / hộp kỹ thuật, hoặc đổi tường dày hơn"
                    });
                }
            }
        }
        return list;
    }

    /// <summary>Thiết bị chồng nhau trên cùng mặt tường.</summary>
    public static List<MepWarning> CheckOverlaps(Project p)
    {
        var m = p.Mep; var list = new List<MepWarning>();
        var hosted = m.Devices.Where(d => d.WallId > 0).ToList();
        for (int i = 0; i < hosted.Count; i++)
            for (int j = i + 1; j < hosted.Count; j++)
            {
                var a = hosted[i]; var b = hosted[j];
                if (a.WallId != b.WallId || a.Side != b.Side) continue;
                var fa = m.FamilyOf(a); var fb = m.FamilyOf(b);
                double gapX = Math.Abs(a.Offset - b.Offset) - ((fa?.W ?? 0.1) + (fb?.W ?? 0.1)) / 2;
                double gapY = Math.Abs(a.Height - b.Height) - ((fa?.H ?? 0.1) + (fb?.H ?? 0.1)) / 2;
                if (gapX < -1e-9 && gapY < -1e-9)
                    list.Add(new MepWarning
                    {
                        Code = "VA_CHAM", DeviceId = b.Id, WallId = a.WallId, Story = a.Story, X = b.X, Y = b.Y,
                        Message = $"{Label(m, a)} và {Label(m, b)} chồng lên nhau trên tường {a.WallId}",
                        Fix = "Dời thiết bị sau sang bên"
                    });
            }
        return list;
    }

    /// <summary>Ảnh hưởng tới kết cấu (StructuralImpact): xuyên dầm (đánh giá vị trí / Ø lỗ, đề xuất gia cường), xuyên cột (cấm),
    /// lỗ sàn gần cột / lỗ lớn / thẳng hàng, rãnh đục tường, quang treo ống thoát. Mã giữ nguyên XUYEN_DAM / XUYEN_COT.</summary>
    public static List<MepWarning> CheckStructure(Project p) => StructuralImpact.ToWarnings(StructuralImpact.Scan(p));

    // ------------------------------------------------------------------
    // Sửa nhanh
    // ------------------------------------------------------------------
    /// <summary>Dời thiết bị gắn tường tới chỗ hợp lệ gần nhất (ra khỏi ô cửa, không chồng thiết bị khác). Trả false nếu không có chỗ.</summary>
    public static bool MoveToValid(Project p, MepDevice d, double step = 0.05)
    {
        var w = p.Walls.FirstOrDefault(x => x.Id == d.WallId);
        if (w == null) return false;
        double orig = d.Offset;
        var fam = p.Mep.FamilyOf(d);
        double half = Math.Min((fam?.W ?? 0.1) / 2, w.Chord / 2);
        for (int k = 0; k <= (int)(w.Chord / step) + 1; k++)
            foreach (int sg in new[] { 1, -1 })
            {
                double off = orig + sg * k * step;
                if (off < half - 1e-9 || off > w.Chord - half + 1e-9) continue;
                d.Offset = off;
                if (!CheckOpenings(p).Any(x => x.DeviceId == d.Id) && !OverlapsAny(p, d))
                {
                    var pos = MepRouting.DevicePos(p, d); d.X = pos.X; d.Y = pos.Y;
                    return true;
                }
            }
        d.Offset = orig;
        return false;
    }

    /// <summary>Thiết bị d chồng thiết bị khác cùng mặt tường?</summary>
    public static bool OverlapsAny(Project p, MepDevice d)
    {
        var m = p.Mep; var fa = m.FamilyOf(d);
        foreach (var b in m.Devices)
        {
            if (b.Id == d.Id || b.WallId != d.WallId || b.Side != d.Side) continue;
            var fb = m.FamilyOf(b);
            double gapX = Math.Abs(d.Offset - b.Offset) - ((fa?.W ?? 0.1) + (fb?.W ?? 0.1)) / 2;
            double gapY = Math.Abs(d.Height - b.Height) - ((fa?.H ?? 0.1) + (fb?.H ?? 0.1)) / 2;
            if (gapX < -1e-9 && gapY < -1e-9) return true;
        }
        return false;
    }

    /// <summary>Gắn thiết bị mồ côi (tường bị xoá) vào tường gần nhất.</summary>
    public static bool Rehost(Project p, MepDevice d, double maxDist = 2.0)
    {
        var nw = MepRouting.NearestWall(p, d.Story, d.X, d.Y, maxDist);
        if (nw.Wall == null) return false;
        d.WallId = nw.Wall.Id; d.Offset = nw.Offset; d.Side = nw.Side;
        var pos = MepRouting.DevicePos(p, d); d.X = pos.X; d.Y = pos.Y;
        return true;
    }

    /// <summary>Đổi mặt tường của thiết bị (chôn mặt kia).</summary>
    public static void FlipSide(Project p, MepDevice d)
    {
        d.Side = d.Side >= 0 ? -1 : 1;
        var pos = MepRouting.DevicePos(p, d); d.X = pos.X; d.Y = pos.Y;
    }

    /// <summary>Áp sửa nhanh theo cảnh báo.</summary>
    public static bool ApplyFix(Project p, MepWarning w)
    {
        var d = p.Mep.Device(w.DeviceId);
        return w.Code switch
        {
            "TRONG_CUA" or "VA_CHAM" => d != null && MoveToValid(p, d),
            "TUONG_XOA" => d != null && Rehost(p, d),
            _ => false
        };
    }
}
