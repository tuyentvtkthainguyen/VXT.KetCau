using KetCauVXT.Drawing;
using KetCauVXT.Model;

namespace KetCauVXT.Mep;

// =====================================================================
// ĐIỆN NƯỚC – ĐHKK ĐỢT 5
// • ConduitSpacing: ống gen song song trong sàn – khoảng hở ≥ max(3·Ø, 50 mm), số ống tối đa trong một dải sàn 1 m,
//   chồng 2 ống tại chỗ giao (Ø1 + Ø2 ≤ 1/3 chiều dày sàn); giãn tuyến (dời đoạn giữa sang bên) khi quá sát.
// • ClimateCooling: Δt, Δw, hiệu chỉnh CLTD theo địa phương (TCVN 5687 – bảng tỉnh, phiên bản 2010 / 2024).
// • LightingDesign: độ rọi yêu cầu, mật độ công suất chiếu sáng LPD theo loại phòng; số đèn theo phương pháp hệ số sử dụng
//   N = E·A / (Φ·UF·MF); kiểm tra Σ W đèn / A ≤ LPD.
// • DrainStacks: sức chứa ống đứng (DFU, có / không thông hơi), cộng dồn từ trên xuống, chọn Ø ống đứng tại chân; ống thoát ra (building drain).
// • SepticTank: bể tự hoại 3 ngăn W = Wn + Wc (lưu nước + cặn), kích thước, hình vẽ mặt bằng – mặt cắt.
// Giá trị chưa đối chiếu bản gốc được đánh dấu (*) trong bảng thông số và có thể tuỳ chỉnh (MepStdOptions.Overrides).
// =====================================================================

/// <summary>Kiểm tra / giãn ống gen điện song song trong sàn.</summary>
public static class ConduitSpacing
{
    /// <summary>Đoạn ngang trục song song X (AlongX) hoặc Y của tuyến ống gen nằm trong chiều dày sàn. Index = chỉ số điểm CUỐI đoạn.</summary>
    public sealed record Seg(MepRun Run, int Index, double X1, double Y1, double X2, double Y2, double Z, double Dia, bool AlongX, string Story, double SlabT)
    {
        /// <summary>Toạ độ ngang (vuông góc trục đoạn).</summary>
        public double Lat => AlongX ? Y1 : X1;
        public double Lo => AlongX ? Math.Min(X1, X2) : Math.Min(Y1, Y2);
        public double Hi => AlongX ? Math.Max(X1, X2) : Math.Max(Y1, Y2);
    }

    /// <summary>Khoảng hở yêu cầu (m) giữa 2 ống gen Ø d1, d2 (mm): max(k·Ø lớn, khoảng hở nhỏ nhất).</summary>
    public static double Required(MepStdOptions o, double d1, double d2) =>
        Math.Max(MepStandards.Get(o, "gen.kc_d") * Math.Max(d1, d2) / 1000, MepStandards.Get(o, "gen.kc_min"));

    private static bool Conduit(MepRun r) => r != null && MepSystems.IsElectric(r.System) && r.Kind != MepRunKind.Ong;

    /// <summary>Mọi đoạn ngang song song trục của ống gen nằm trong chiều dày sàn.</summary>
    public static List<Seg> Segments(Project p)
    {
        var res = new List<Seg>();
        if (p?.Mep == null) return res;
        foreach (var r in p.Mep.Runs.Where(Conduit))
        {
            var path = r.Path; if (path == null || path.Count < 2) continue;
            double dia = StructuralImpact.RunDia(r), rad = dia / 2000;
            for (int i = 1; i < path.Count; i++)
            {
                var a = path[i - 1]; var c = path[i];
                if (c.WallId > 0 || Math.Abs(a.Z - c.Z) > 1e-6) continue;
                bool ax = Math.Abs(a.Y - c.Y) < 1e-6, ay = Math.Abs(a.X - c.X) < 1e-6;
                if (ax == ay) continue;   // xiên hoặc điểm trùng
                var sl = SlabConduit.SlabAtZ(p, (a.X + c.X) / 2, (a.Y + c.Y) / 2, a.Z, rad);
                if (sl == null) continue;
                res.Add(new Seg(r, i, a.X, a.Y, c.X, c.Y, a.Z, dia, ax, sl.Value.Story, sl.Value.T));
            }
        }
        return res;
    }

    private static double Overlap(Seg a, Seg b) => Math.Min(a.Hi, b.Hi) - Math.Max(a.Lo, b.Lo);

    /// <summary>Hai đoạn song song cùng lớp (cùng cao độ) đi cạnh nhau quá sát: trả khoảng hở thực (m); null nếu không vi phạm.</summary>
    public static double? Violation(MepStdOptions o, Seg a, Seg b)
    {
        if (a.Run == b.Run || a.AlongX != b.AlongX || a.Story != b.Story) return null;
        if (Math.Abs(a.Z - b.Z) > (a.Dia + b.Dia) / 2000 + 1e-6) return null;
        if (Overlap(a, b) < MepStandards.Get(o, "gen.overlap") - 1e-9) return null;
        double clear = Math.Abs(a.Lat - b.Lat) - (a.Dia + b.Dia) / 2000;
        return clear < Required(o, a.Dia, b.Dia) - 1e-9 ? clear : null;
    }

    private const string RefS = "TCVN 9207:2012 (lắp đặt ống luồn dây); TCVN 5574:2018 (cấu tạo sàn)";

    /// <summary>Cảnh báo: ONG_GEN_SAT_NHAU (khoảng hở &lt; max(3Ø, 50 mm)), ONG_GEN_BO (quá N ống trong dải sàn), ONG_GEN_GIAO (chồng ống tại chỗ giao &gt; 1/3 h sàn).</summary>
    public static List<StructuralImpactItem> Check(Project p)
    {
        var list = new List<StructuralImpactItem>();
        if (p?.Mep == null) return list;
        var o = p.Mep.Settings?.Standards ?? new MepStdOptions();
        var segs = Segments(p);
        var pairDone = new HashSet<(int, int)>();
        for (int i = 0; i < segs.Count; i++)
            for (int j = i + 1; j < segs.Count; j++)
            {
                var a = segs[i]; var b = segs[j];
                var key = (Math.Min(a.Run.Id, b.Run.Id), Math.Max(a.Run.Id, b.Run.Id));
                if (pairDone.Contains(key)) continue;
                var v = Violation(o, a, b);
                if (v == null) continue;
                pairDone.Add(key);
                double req = Required(o, a.Dia, b.Dia);
                double mid = (Math.Max(a.Lo, b.Lo) + Math.Min(a.Hi, b.Hi)) / 2;
                list.Add(new StructuralImpactItem
                {
                    Code = "ONG_GEN_SAT_NHAU", Severity = ImpactSeverity.CanhBao, Story = a.Story,
                    X = a.AlongX ? mid : (a.Lat + b.Lat) / 2, Y = a.AlongX ? (a.Lat + b.Lat) / 2 : mid, Z = a.Z, RunId = b.Run.Id, Dia = Math.Max(a.Dia, b.Dia),
                    Message = $"Ống gen tuyến {a.Run.Id} Ø{a.Dia:0} và tuyến {b.Run.Id} Ø{b.Dia:0} đi song song {Overlap(a, b):0.##} m trong sàn, khoảng hở {Math.Max(0, v.Value) * 1000:0} mm < {req * 1000:0} mm",
                    Rule = $"Khoảng hở giữa ống gen song song ≥ {MepStandards.Num(MepStandards.Get(o, "gen.kc_d"))}·Ø và ≥ {MepStandards.Get(o, "gen.kc_min") * 1000:0} mm (bê tông bao kín ống)",
                    Reference = RefS, Proposal = "Đi lại tuyến (tự giãn đoạn giữa sang bên) hoặc gom dây vào một ống gen lớn hơn nếu sàn đủ dày"
                });
            }
        // số ống gen trong một dải sàn
        int maxN = (int)Math.Round(MepStandards.Get(o, "gen.max_bo"));
        double band = MepStandards.Get(o, "gen.dai_rong");
        var bundleDone = new HashSet<string>();
        foreach (var s in segs)
        {
            double at = (s.Lo + s.Hi) / 2;
            var near = segs.Where(q => q.AlongX == s.AlongX && q.Story == s.Story && Math.Abs(q.Z - s.Z) <= (q.Dia + s.Dia) / 2000 + 1e-6
                                       && q.Lo <= at + 1e-9 && q.Hi >= at - 1e-9 && Math.Abs(q.Lat - s.Lat) <= band / 2 + 1e-9)
                           .Select(q => q.Run.Id).Distinct().OrderBy(x => x).ToList();
            if (near.Count <= maxN) continue;
            string k = s.Story + "|" + string.Join(",", near);
            if (!bundleDone.Add(k)) continue;
            list.Add(new StructuralImpactItem
            {
                Code = "ONG_GEN_BO", Severity = ImpactSeverity.CanhBao, Story = s.Story, X = s.AlongX ? at : s.Lat, Y = s.AlongX ? s.Lat : at, Z = s.Z, RunId = s.Run.Id, Dia = s.Dia,
                Message = $"{near.Count} ống gen (tuyến {string.Join(", ", near)}) đi chung trong dải sàn rộng {band:0.##} m > {maxN} ống",
                Rule = $"Tối đa {maxN} ống gen song song trong một dải sàn {band:0.##} m; mỗi ống Ø ≤ 1/3 chiều dày sàn", Reference = RefS,
                Proposal = "Tách tuyến sang dải sàn khác, đi trong trần giả / máng cáp, hoặc dùng hộp chia gom mạch"
            });
        }
        // chồng ống tại chỗ giao: Ø1 + Ø2 ≤ ty_san · h sàn
        var crossDone = new HashSet<(int, int)>();
        double ty = MepStandards.Get(o, "gen.ty_san");
        foreach (var a in segs)
            foreach (var b in segs)
            {
                if (a.Run.Id >= b.Run.Id || a.AlongX == b.AlongX || a.Story != b.Story) continue;
                if (Math.Abs(a.Z - b.Z) > (a.Dia + b.Dia) / 2000 + 1e-6) continue;
                if (crossDone.Contains((a.Run.Id, b.Run.Id))) continue;
                var h = a.AlongX ? a : b; var v = a.AlongX ? b : a;
                double x = v.Lat, y = h.Lat;
                if (x < h.Lo - 1e-9 || x > h.Hi + 1e-9 || y < v.Lo - 1e-9 || y > v.Hi + 1e-9) continue;
                double t = Math.Min(a.SlabT, b.SlabT);
                if ((a.Dia + b.Dia) / 1000 <= t * ty + 1e-9) continue;
                crossDone.Add((a.Run.Id, b.Run.Id));
                list.Add(new StructuralImpactItem
                {
                    Code = "ONG_GEN_GIAO", Severity = ImpactSeverity.CanhBao, Story = a.Story, X = x, Y = y, Z = a.Z, RunId = b.Run.Id, Dia = a.Dia + b.Dia,
                    Message = $"Tuyến {a.Run.Id} Ø{a.Dia:0} giao tuyến {b.Run.Id} Ø{b.Dia:0} trong sàn {t * 1000:0} mm: chồng {a.Dia + b.Dia:0} mm > 1/3 h = {t * ty * 1000:0} mm",
                    Rule = "Tổng Ø các ống chồng nhau tại chỗ giao ≤ 1/3 chiều dày sàn", Reference = RefS,
                    Proposal = "Dời chỗ giao vào lớp cán nền / trần giả, hoặc giảm Ø ống gen (tách mạch)"
                });
            }
        return list;
    }

    /// <summary>Giãn tuyến: đoạn giữa (không phải đoạn ra khỏi thiết bị) của tuyến đi sau quá sát tuyến trước → dời song song sang bên
    /// (2 đoạn kề vuông góc kéo dài theo), giữ tránh cột, chỉ qua dầm trong 1/3 giữa nhịp khi được phép. Trả số tuyến đã giãn.</summary>
    public static int Spread(Project p)
    {
        if (p?.Mep == null) return 0;
        var o = p.Mep.Settings?.Standards ?? new MepStdOptions();
        var moved = new HashSet<int>();
        for (int iter = 0; iter < 20; iter++)
        {
            var segs = Segments(p);
            bool any = false, progress = false;
            for (int i = 0; i < segs.Count && !progress; i++)
                for (int j = i + 1; j < segs.Count && !progress; j++)
                {
                    var a = segs[i]; var b = segs[j];
                    if (Violation(o, a, b) == null) continue;
                    any = true;
                    // dời tuyến có Id lớn hơn (tuyến vẽ sau); không được thì thử tuyến kia
                    var (mv, fix) = a.Run.Id > b.Run.Id ? (a, b) : (b, a);
                    if (TryShift(p, o, mv, fix, segs) || TryShift(p, o, fix, mv, segs)) { progress = true; moved.Add(mv.Run.Id); }
                }
            if (!any || !progress) break;
        }
        return moved.Count;
    }

    private static bool TryShift(Project p, MepStdOptions o, Seg s, Seg other, List<Seg> segs)
    {
        var path = s.Run.Path; int i = s.Index;
        if (i < 2 || i + 1 >= path.Count) return false;
        MepPt p0 = path[i - 2], p1 = path[i - 1], p2 = path[i], p3 = path[i + 1];
        if (p1.WallId > 0 || p3.WallId > 0 || Math.Abs(p0.Z - s.Z) > 1e-6 || Math.Abs(p3.Z - s.Z) > 1e-6) return false;
        // 2 đoạn kề phải vuông góc với đoạn dời (để kéo dài theo mà vẫn trực giao)
        bool perp0 = s.AlongX ? Math.Abs(p0.X - p1.X) < 1e-6 : Math.Abs(p0.Y - p1.Y) < 1e-6;
        bool perp3 = s.AlongX ? Math.Abs(p3.X - p2.X) < 1e-6 : Math.Abs(p3.Y - p2.Y) < 1e-6;
        if (!perp0 || !perp3) return false;
        double need = Required(o, s.Dia, other.Dia) + (s.Dia + other.Dia) / 2000 + 0.005;
        double side0 = Math.Sign(s.Lat - other.Lat);
        var sides = side0 == 0 ? new[] { 1.0, -1.0 } : new[] { side0, -side0 };
        double rad = s.Dia / 2000;
        var cols = SlabConduit.Columns(p, s.Z, rad + MepStandards.Get(o, "gen.cach_cot"));
        var beams = SlabConduit.Beams(p, s.Z, rad, rad + 0.02);
        bool canCross = SlabConduit.CrossAllowed(o, s.SlabT, s.Dia);
        foreach (double sg in sides)
            for (int k = 0; k < 4; k++)
            {
                double lat = other.Lat + sg * (need + k * need);
                var q1 = s.AlongX ? new MepPt(p1.X, lat, p1.Z, p1.WallId) : new MepPt(lat, p1.Y, p1.Z, p1.WallId);
                var q2 = s.AlongX ? new MepPt(p2.X, lat, p2.Z, p2.WallId) : new MepPt(lat, p2.Y, p2.Z, p2.WallId);
                if (!Clear(p0, q1, cols, beams, canCross) || !Clear(q1, q2, cols, beams, canCross) || !Clear(q2, p3, cols, beams, canCross)) continue;
                var ns = s with { X1 = q1.X, Y1 = q1.Y, X2 = q2.X, Y2 = q2.Y };
                if (segs.Any(t => t.Run != s.Run && Violation(o, ns, t) != null)) continue;
                path[i - 1] = q1; path[i] = q2;
                SlabConduit.Simplify(path);
                return true;
            }
        return false;
    }

    /// <summary>Đoạn a→b không vào cột, chỉ qua dầm khi được phép và trong 1/3 giữa nhịp (lấy mẫu 2 cm).</summary>
    private static bool Clear(MepPt a, MepPt b, List<SlabConduit.ColRect> cols, List<SlabConduit.BeamStrip> beams, bool canCross)
    {
        double L = MepGeom.Dist2D(a.X, a.Y, b.X, b.Y);
        int n = Math.Max(1, (int)Math.Ceiling(L / 0.02 - 1e-9));
        for (int k = 0; k <= n; k++)
        {
            double t = (double)k / n, x = a.X + t * (b.X - a.X), y = a.Y + t * (b.Y - a.Y);
            if (cols.Any(c => c.Inside(x, y))) return false;
            foreach (var bm in beams)
            {
                var h = bm.Hit(x, y);
                if (h != null && (!canCross || !SlabConduit.InMiddleThird(h.Value))) return false;
            }
        }
        return true;
    }
}

/// <summary>Thông số khí hậu tải lạnh theo địa phương (TCVN 5687) và trắc ẩm.</summary>
public static class ClimateCooling
{
    /// <summary>Áp suất hơi bão hoà (Pa) – công thức Magnus (Alduchov – Eskridge 1996).</summary>
    public static double Psat(double t) => 610.94 * Math.Exp(17.625 * t / (t + 243.04));

    /// <summary>Độ chứa ẩm (kg/kg kk khô) tại t (°C), φ (%), áp suất khí quyển P (Pa).</summary>
    public static double W(double t, double phi, double P = 101325)
    {
        double pv = Math.Clamp(phi, 0, 100) / 100 * Psat(t);
        return 0.622 * pv / (P - pv);
    }

    public sealed record DesignData(string Province, string Source, double Ti, double PhiI, double To, double PhiO, double DT, double DW, double DR, double CltdCorr);

    /// <summary>Thông số thiết kế: Δt = t ngoài − t trong; Δw = w ngoài − w trong; hiệu chỉnh CLTD = (25,5 − t trong) + (t ngoài − DR/2 − 29,4).</summary>
    public static DesignData Of(MepStdOptions o)
    {
        o ??= new MepStdOptions();
        var (ti, fi) = MepStandards.Indoor(o);
        var (to, fo) = MepStandards.Outdoor(o);
        double dr = MepStandards.Get(o, "dhkk.dr");
        double dw = Math.Max(0, W(to, fo) - W(ti, fi));
        return new DesignData(MepStandards.Province(o).Name, MepStandards.ClimateSource(o), ti, fi, to, fo, to - ti, dw, dr, (25.5 - ti) + (to - dr / 2 - 29.4));
    }

    /// <summary>Gán Δt, Δw, hiệu chỉnh CLTD vào thông số tải lạnh.</summary>
    public static void Apply(CoolingInput ci, MepStdOptions o)
    {
        var d = Of(o);
        ci.DeltaT = d.DT; ci.DeltaW = d.DW; ci.CltdCorrection = d.CltdCorr;
    }

    public static CoolingInput Input(MepStdOptions o)
    {
        var ci = new CoolingInput();
        Apply(ci, o);
        return ci;
    }
}

/// <summary>Loại không gian: độ rọi yêu cầu (lx) và mật độ công suất chiếu sáng LPD lớn nhất (W/m²).</summary>
public sealed record LightSpace(string Code, string Name, string[] Keys, double Lux, double Lpd, string LuxSource, string LpdSource);

/// <summary>Kết quả kiểm tra chiếu sáng một phòng.</summary>
public sealed record LightingCheck(int RoomId, string Room, string Story, LightSpace Space, double Area, int Lamps, double SumW, double Lpd, double LpdMax,
    double LuxReq, double Lumen, double Uf, double Mf, double LuxAchieved, int LampsRequired, string Family, bool LpdOk, bool LuxOk);

public static class LightingDesign
{
    public const string SrcLux = "TCVN 7114-1:2008 (chiếu sáng nơi làm việc trong nhà)";
    public const string SrcLpd = "QCVN 09:2017/BXD (mật độ công suất chiếu sáng)";
    public const string SrcHome = "Thực hành thiết kế nhà ở (*)";

    /// <summary>Bảng loại phòng. (*) Giá trị cần đối chiếu bản gốc; nhà ở không thuộc phạm vi bắt buộc của QCVN 09 – LPD theo thực hành.</summary>
    public static readonly IReadOnlyList<LightSpace> Spaces = new List<LightSpace>
    {
        new("vanphong", "Văn phòng / phòng làm việc", new[] { "văn phòng", "làm việc", "office", "phòng kỹ thuật" }, 500, 11, SrcLux, SrcLpd + " (*)"),
        new("hop", "Phòng họp", new[] { "họp", "hội nghị", "meeting" }, 500, 11, SrcLux, SrcLpd + " (*)"),
        new("lophoc", "Lớp học", new[] { "lớp", "học" }, 300, 10, SrcLux, SrcLpd + " (*)"),
        new("khach", "Phòng khách / sinh hoạt chung", new[] { "khách", "sinh hoạt", "living" }, 150, 7, SrcHome, SrcHome),
        new("ngu", "Phòng ngủ", new[] { "ngủ", "bed" }, 100, 6, SrcHome, SrcHome),
        new("bep", "Bếp / phòng ăn", new[] { "bếp", "ăn", "kitchen" }, 200, 8, SrcHome, SrcHome),
        new("wc", "Vệ sinh / tắm", new[] { "wc", "vệ sinh", "tắm", "toilet" }, 200, 7, SrcLux, SrcHome),
        new("hanhlang", "Hành lang / sảnh", new[] { "hành lang", "sảnh", "lobby", "corridor" }, 100, 5, SrcLux, SrcLpd + " (*)"),
        new("cauthang", "Cầu thang", new[] { "thang", "stair" }, 150, 6, SrcLux, SrcLpd + " (*)"),
        new("kho", "Kho", new[] { "kho", "store" }, 100, 5, SrcLux, SrcLpd + " (*)"),
        new("khac", "Không gian khác", Array.Empty<string>(), 150, 8, SrcHome, SrcHome),
    };

    /// <summary>Loại không gian theo tên phòng (từ khoá, không phân biệt hoa thường); không khớp → "Không gian khác".</summary>
    public static LightSpace SpaceOf(string roomName)
    {
        string n = (roomName ?? "").ToLowerInvariant();
        return Spaces.FirstOrDefault(s => s.Keys.Any(k => n.Contains(k))) ?? Spaces[^1];
    }

    /// <summary>Độ rọi yêu cầu (lx): tuỳ chỉnh khoá "cs.e.&lt;mã&gt;" nếu có.</summary>
    public static double Lux(MepStdOptions o, LightSpace s) => MepStandards.IsOverridden(o, "cs.e." + s.Code) ? MepStandards.Get(o, "cs.e." + s.Code) : s.Lux;
    /// <summary>LPD lớn nhất (W/m²): tuỳ chỉnh khoá "cs.lpd.&lt;mã&gt;" nếu có.</summary>
    public static double LpdMax(MepStdOptions o, LightSpace s) => MepStandards.IsOverridden(o, "cs.lpd." + s.Code) ? MepStandards.Get(o, "cs.lpd." + s.Code) : s.Lpd;

    /// <summary>Quang thông một đèn (lm): khai báo của họ, chưa có → Power × hiệu suất mặc định.</summary>
    public static double LampLumen(MepFamily f, MepStdOptions o = null) => f == null ? 0 : f.Lumen > 0 ? f.Lumen : Math.Max(0, f.Power) * MepStandards.Get(o, "cs.lm_w");

    /// <summary>Phương pháp hệ số sử dụng: N = ⌈E·A / (Φ·UF·MF)⌉.</summary>
    public static int LampCount(double lux, double area, double lumen, double uf, double mf)
    {
        double d = lumen * uf * mf;
        if (d <= 0 || lux <= 0 || area <= 0) return 0;
        return (int)Math.Ceiling(lux * area / d - 1e-9);
    }

    /// <summary>Độ rọi trung bình đạt được (lx) = N·Φ·UF·MF / A.</summary>
    public static double LuxOf(int n, double lumen, double area, double uf, double mf) => area <= 0 ? 0 : n * lumen * uf * mf / area;

    /// <summary>Đèn (nhóm Đèn) đặt trong phòng.</summary>
    public static List<(MepDevice D, MepFamily F)> Lamps(Project p, Room r)
    {
        var m = p.Mep; var res = new List<(MepDevice, MepFamily)>();
        if (m == null || r?.Outline == null || r.Outline.Count < 3) return res;
        foreach (var d in m.Devices.Where(d => d.Story == r.Story))
        {
            var f = m.FamilyOf(d);
            if (f == null || f.Category != MepCategory.Den) continue;
            if (Geo2.Contains(r.Outline, d.X, d.Y)) res.Add((d, f));
        }
        return res;
    }

    public static LightingCheck Check(Project p, Room r)
    {
        var o = p.Mep?.Settings?.Standards ?? new MepStdOptions();
        var sp = SpaceOf(r.Name);
        double A = r.Area, uf = MepStandards.Get(o, "cs.uf"), mf = MepStandards.Get(o, "cs.mf");
        var lamps = Lamps(p, r);
        double sumW = lamps.Sum(x => x.F.Power), sumLm = lamps.Sum(x => LampLumen(x.F, o));
        double lux = Lux(o, sp), lmax = LpdMax(o, sp), lpd = A > 0 ? sumW / A : 0;
        // họ đèn dùng nhiều nhất trong phòng; chưa có đèn → đèn ốp trần mặc định
        var fam = lamps.GroupBy(x => x.F.Name).OrderByDescending(g => g.Count()).Select(g => g.First().F).FirstOrDefault()
                  ?? p.Mep?.Library?.Find("Đèn ốp trần");
        double lm1 = LampLumen(fam, o);
        int nReq = LampCount(lux, A, lm1, uf, mf);
        double eAch = A > 0 ? sumLm * uf * mf / A : 0;
        return new LightingCheck(r.Id, string.IsNullOrEmpty(r.Name) ? $"Phòng {r.Id}" : r.Name, r.Story, sp, A, lamps.Count, sumW, lpd, lmax,
            lux, lm1, uf, mf, eAch, nReq, fam?.Name ?? "", lpd <= lmax + 1e-9, eAch >= lux - 1e-9);
    }

    /// <summary>Kiểm tra mọi phòng có đèn.</summary>
    public static List<LightingCheck> Checks(Project p) =>
        (p.Arch?.Rooms ?? new List<Room>()).Where(r => r.Outline.Count >= 3).Select(r => Check(p, r)).Where(c => c.Lamps > 0).ToList();

    public const string TableTitle = "Kiểm tra chiếu sáng: mật độ công suất LPD và số đèn theo phương pháp hệ số sử dụng";

    public static KetCauVXT.Report.QtyTable Table(Project p)
    {
        var t = new KetCauVXT.Report.QtyTable
        {
            Title = TableTitle,
            Headers = new[] { "Phòng", "Tầng", "Loại", "A (m²)", "E yêu cầu (lx)", "Số đèn", "Σ W", "LPD (W/m²)", "LPD max", "Đạt LPD", "E đạt (lx)", "Đèn tính", "Φ (lm)", "N cần = E·A/(Φ·UF·MF)", "Nguồn" },
            Widths = new double[] { 16, 8, 18, 8, 10, 8, 8, 10, 9, 8, 10, 16, 8, 12, 40 }
        };
        foreach (var c in Checks(p))
            t.Add(c.Room, c.Story, c.Space.Name, Math.Round(c.Area, 1), c.LuxReq, c.Lamps, Math.Round(c.SumW, 0), Math.Round(c.Lpd, 2), c.LpdMax,
                c.LpdOk ? "Đạt" : "VƯỢT", Math.Round(c.LuxAchieved, 0), c.Family, Math.Round(c.Lumen, 0), c.LampsRequired, c.Space.LuxSource + "; " + c.Space.LpdSource);
        return t;
    }
}

/// <summary>Ống đứng thoát nước, ống thoát ra (building drain) theo đương lượng DFU.</summary>
public static class DrainStacks
{
    public const string Src = "TCVN 4474:1987 (thoát nước bên trong) – sức chứa theo DFU tham khảo IPC Bảng 710.1 (*)";

    /// <summary>Sức chứa ống đứng (Ø ngoài uPVC mm, DFU khi ≤ 3 khoảng nhánh / tầng, DFU khi &gt; 3).</summary>
    public static readonly (double Od, double Le3, double Gt3)[] StackCap =
        { (60, 10, 24), (76, 20, 42), (90, 48, 72), (110, 240, 500), (140, 540, 1100), (160, 960, 1900) };

    /// <summary>Sức chứa ống thoát ra (Ø, DFU ở độ dốc 1 %, 2 %, 4 %); 0 = không dùng ở độ dốc đó.</summary>
    public static readonly (double Od, double I1, double I2, double I4)[] DrainCap =
        { (60, 0, 21, 26), (76, 0, 24, 31), (90, 0, 42, 50), (110, 180, 216, 250), (140, 390, 480, 575), (160, 700, 840, 1000) };

    /// <summary>Sức chứa ống đứng Ø od với số khoảng nhánh (tầng có thiết bị) n; không thông hơi × hệ số giảm.</summary>
    public static double Capacity(double od, int intervals, bool vented, MepStdOptions o = null)
    {
        var row = StackCap.FirstOrDefault(x => Math.Abs(x.Od - od) < 1e-6);
        if (row.Od <= 0) return 0;
        return (intervals <= 3 ? row.Le3 : row.Gt3) * (vented ? 1 : MepStandards.Get(o, "thoat.k_khong_th"));
    }

    /// <summary>Ø ống đứng nhỏ nhất đủ sức chứa (ống có xí ≥ Ø110); vượt bảng → Ø lớn nhất.</summary>
    public static double StackSize(double dfu, int intervals, bool wc, bool vented, MepStdOptions o = null)
    {
        double min = wc ? MepStandards.Get(o, "thoat.d_xi") : 60;
        foreach (var r in StackCap)
            if (r.Od >= min - 1e-9 && Capacity(r.Od, intervals, vented, o) >= dfu - 1e-9) return r.Od;
        return StackCap[^1].Od;
    }

    public static double DrainCapacity(double od, double slope)
    {
        var r = DrainCap.FirstOrDefault(x => Math.Abs(x.Od - od) < 1e-6);
        if (r.Od <= 0) return 0;
        return slope >= 0.04 - 1e-9 ? r.I4 : slope >= 0.02 - 1e-9 ? r.I2 : slope >= 0.01 - 1e-9 ? r.I1 : 0;
    }

    /// <summary>Ống thoát ra: Ø nhỏ nhất (≥ Ø110 khi có xí) với độ dốc tiêu chuẩn của Ø đó đủ sức chứa.</summary>
    public static (double Od, double Slope, double Cap) BuildingDrain(double dfu, bool wc, MepStdOptions o = null)
    {
        double min = wc ? MepStandards.Get(o, "thoat.d_xi") : 60;
        foreach (var r in DrainCap)
        {
            if (r.Od < min - 1e-9) continue;
            double i = MepStandards.DrainSlopeStd(r.Od), cap = DrainCapacity(r.Od, i);
            if (cap >= dfu - 1e-9) return (r.Od, i, cap);
        }
        var last = DrainCap[^1];
        double il = MepStandards.DrainSlopeStd(last.Od);
        return (last.Od, il, DrainCapacity(last.Od, il));
    }

    public sealed record Level(string Story, double Elevation, double Dfu, double Acc, double Od);
    public sealed record Stack(string Name, bool Wc, List<Level> Levels, double Total, int Intervals, double Od, double Cap, bool Vented, bool Ok);
    public sealed record Result(List<Stack> Stacks, double TotalDfu, bool AnyWc, double DrainOd, double DrainSlope, double DrainCapacity);

    /// <summary>2 ống đứng như sơ đồ ống đứng (thoát xí / thoát rửa): cộng dồn DFU từ tầng trên xuống; Ø chọn tại chân ống, giữ suốt chiều cao.</summary>
    public static Result Build(Project p)
    {
        var m = p.Mep; var o = m?.Settings?.Standards ?? new MepStdOptions();
        var stacks = new List<Stack>();
        if (m == null) return new Result(stacks, 0, false, 0, 0, 0);
        var devs = m.Devices.Select(d => (D: d, F: m.FamilyOf(d))).Where(x => x.F != null && x.F.DrainUnits > 0).ToList();
        var sts = p.Stories.OrderByDescending(s => s.Elevation).ToList();
        foreach (var (name, wc) in new[] { ("Ống đứng thoát xí", true), ("Ống đứng thoát rửa", false) })
        {
            var list = devs.Where(x => (x.F.Symbol == "boncau") == wc).ToList();
            if (list.Count == 0) continue;
            var lv = new List<Level>(); double acc = 0;
            int nInt = sts.Count(s => list.Any(x => x.D.Story == s.Name));
            foreach (var s in sts)
            {
                double dfu = list.Where(x => x.D.Story == s.Name).Sum(x => x.F.DrainUnits);
                if (dfu <= 0 && acc <= 0) continue;
                acc += dfu;
                lv.Add(new Level(s.Name, s.Elevation, dfu, acc, StackSize(acc, nInt, wc, o.StackVented, o)));
            }
            double od = StackSize(acc, nInt, wc, o.StackVented, o), cap = Capacity(od, nInt, o.StackVented, o);
            stacks.Add(new Stack(name, wc, lv, acc, nInt, od, cap, o.StackVented, cap >= acc - 1e-9));
        }
        double tot = stacks.Sum(s => s.Total); bool anyWc = stacks.Any(s => s.Wc);
        var bd = tot > 0 ? BuildingDrain(tot, anyWc, o) : (0.0, 0.0, 0.0);
        return new Result(stacks, tot, anyWc, bd.Item1, bd.Item2, bd.Item3);
    }

    public const string TableTitle = "Ống đứng thoát nước và ống thoát ra (sức chứa theo DFU)";

    public static KetCauVXT.Report.QtyTable Table(Project p)
    {
        var r = Build(p);
        var t = new KetCauVXT.Report.QtyTable
        {
            Title = TableTitle,
            Headers = new[] { "Ống đứng", "Tầng", "DFU tầng", "DFU cộng dồn", "Ø ống đứng (mm)", "Sức chứa (DFU)", "Thông hơi", "Kết luận", "Nguồn" },
            Widths = new double[] { 20, 10, 10, 12, 14, 12, 10, 12, 50 }
        };
        foreach (var s in r.Stacks)
        {
            foreach (var l in s.Levels) t.Add(s.Name, l.Story, l.Dfu, l.Acc, "", "", "", "", "");
            t.AddTotal(s.Name, "Chân ống", "", s.Total, s.Od, s.Cap, s.Vented ? "Có" : "Không", s.Ok ? "Đạt" : "VƯỢT – tăng Ø / chia ống", Src);
        }
        if (r.TotalDfu > 0)
            t.AddTotal("Ống thoát ra (ra hố ga / bể tự hoại)", "", "", r.TotalDfu, r.DrainOd, r.DrainCapacity, $"i = {r.DrainSlope * 100:0.#} %",
                r.DrainCapacity >= r.TotalDfu - 1e-9 ? "Đạt" : "VƯỢT", Src);
        return t;
    }
}

/// <summary>Bể tự hoại 3 ngăn: W = Wn + Wc.</summary>
public static class SepticTank
{
    public const string Title = "BỂ TỰ HOẠI 3 NGĂN";
    public const string Src = "TCVN 10334:2014; TCVN 4474:1987; giáo trình cấp thoát nước trong nhà (*)";

    public sealed record Result(int People, double Q, double Wn, double Wc, double W, double H, double B, double L, double L1, double L2, double L3, double WReal);

    private static double Ceil05(double x) => Math.Ceiling(x / 0.05 - 1e-9) * 0.05;

    /// <summary>Wn = t·q·N/1000; Wc = a·T·(100 − W1)·b·c·N / ((100 − W2)·1000); W = Wn + Wc; H lớp nước; B = max(B min, ⌈√(A/2)⌉ 0,05);
    /// L = ⌈A/B⌉ 0,05 (A = W/H); ngăn 1 = L/2, ngăn 2 = ngăn 3 = L/4.</summary>
    public static Result Compute(int people, MepStdOptions o = null)
    {
        int n = Math.Max(1, people);
        double q = MepStandards.Get(o, "tuhoai.q"), tn = MepStandards.Get(o, "tuhoai.t_nuoc");
        double a = MepStandards.Get(o, "tuhoai.a"), T = MepStandards.Get(o, "tuhoai.t_hut");
        double w1 = MepStandards.Get(o, "tuhoai.w1"), w2 = MepStandards.Get(o, "tuhoai.w2");
        double b = MepStandards.Get(o, "tuhoai.b"), c = MepStandards.Get(o, "tuhoai.c");
        double wn = tn * q * n / 1000;
        double wc = a * T * (100 - w1) * b * c * n / ((100 - w2) * 1000);
        double W = wn + wc, H = MepStandards.Get(o, "tuhoai.h");
        double A = W / H;
        double B = Math.Max(MepStandards.Get(o, "tuhoai.b_min"), Ceil05(Math.Sqrt(A / 2)));
        double L = Ceil05(A / B);
        return new Result(n, q, wn, wc, W, H, B, L, L / 2, L / 4, L / 4, B * L * H);
    }

    public static KetCauVXT.Report.QtyTable Table(Result r, MepStdOptions o = null)
    {
        var t = new KetCauVXT.Report.QtyTable
        {
            Title = "Bể tự hoại 3 ngăn",
            Headers = new[] { "Hạng mục", "Công thức", "Giá trị", "Đơn vị", "Nguồn" },
            Widths = new double[] { 26, 46, 10, 8, 40 }
        };
        t.Add("Số người", "N", r.People, "người", "Cài đặt bể nước");
        t.Add("Thể tích nước", $"Wn = t·q·N/1000 = {MepStandards.Num(MepStandards.Get(o, "tuhoai.t_nuoc"))}·{MepStandards.Num(r.Q)}·{r.People}/1000", Math.Round(r.Wn, 3), "m³", MepStandards.Item("tuhoai.q").Label);
        t.Add("Thể tích cặn", "Wc = a·T·(100 − W1)·b·c·N / ((100 − W2)·1000)", Math.Round(r.Wc, 3), "m³", MepStandards.Item("tuhoai.a").Label);
        t.AddTotal("Dung tích tính toán", "W = Wn + Wc", Math.Round(r.W, 3), "m³", Src);
        t.Add("Chiều sâu lớp nước", "H", r.H, "m", MepStandards.Item("tuhoai.h").Label);
        t.Add("Kích thước trong", "B × L", $"{r.B:0.00} × {r.L:0.00}", "m", Src);
        t.Add("Ngăn chứa – lắng – lọc", "L/2 + L/4 + L/4", $"{r.L1:0.00} + {r.L2:0.00} + {r.L3:0.00}", "m", Src);
        t.AddTotal("Dung tích thực", "B·L·H", Math.Round(r.WReal, 3), "m³", Src);
        return t;
    }

    /// <summary>Hình mặt cắt dọc + mặt bằng bể (tỷ lệ 1:50, đơn vị bản vẽ mm giấy).</summary>
    public static Figure Sketch(Result r)
    {
        const double k = 20, Th = 2.5, tw = 0.2, top = 0.3;   // 1 m = 20 mm giấy; thành 200; khoảng không khí trên mặt nước 300
        var w = new DxfWriter(1);
        double L = r.L * k, H = (r.H + top) * k, T = tw * k, x1 = r.L1 * k, x2 = (r.L1 + r.L2) * k, hw = r.H * k;
        // ---- mặt cắt dọc ----
        w.Rect(Lay.BeTong, -T, -T, L + T, H + T);
        w.Rect(Lay.BeTong, 0, 0, L, H);
        w.Line(Lay.BeTong, x1, 0, x1, H); w.Line(Lay.BeTong, x2, 0, x2, H);
        w.Line(Lay.KyHieu, 0, hw, L, hw, "DASHED");
        // lỗ thông giữa các ngăn (ở 2/3 chiều sâu nước)
        double ho = hw * 0.6;
        w.Rect(Lay.KyHieu, x1 - 1, ho - 2, x1 + 1, ho + 2); w.Rect(Lay.KyHieu, x2 - 1, ho - 2, x2 + 1, ho + 2);
        // ống vào (chữ T) ngăn 1, ống ra ngăn 3, ống thông hơi
        double yin = hw + 4;
        w.Line(Lay.Thep, -T - 15, yin, 4, yin); w.Line(Lay.Thep, 4, yin + 4, 4, hw - 12);
        w.Text(Lay.Chu, -T - 15, yin + 2, Th, "ỐNG VÀO Ø110", 0, 0, 0);
        double yout = hw - 1;
        w.Line(Lay.Thep, L - 4, yout + 4, L - 4, hw - 12); w.Line(Lay.Thep, L - 4, yout, L + T + 15, yout);
        w.Text(Lay.Chu, L + T + 15, yout + 2, Th, "ỐNG RA Ø110", 0, 2, 0);
        w.Line(Lay.Thep, x1 / 2, H, x1 / 2, H + T + 20);
        w.Text(Lay.Chu, x1 / 2 + 2, H + T + 18, Th, "ỐNG THÔNG HƠI Ø60", 0, 0, 0);
        w.Text(Lay.Chu, x1 / 2, 4, Th, "NGĂN CHỨA", 0, 1, 0);
        w.Text(Lay.Chu, (x1 + x2) / 2, 4, Th, "NGĂN LẮNG", 0, 1, 0);
        w.Text(Lay.Chu, (x2 + L) / 2, 4, Th, "NGĂN LỌC", 0, 1, 0);
        w.Text(Lay.Chu, L / 2, hw + 2, Th * 0.8, $"MNTT (H = {r.H:0.00} m)", 0, 1, 0);
        w.Dim(0, -T, x1, -T, -8, 1, $"{r.L1 * 1000:0}");
        w.Dim(x1, -T, x2, -T, -8, 1, $"{r.L2 * 1000:0}");
        w.Dim(x2, -T, L, -T, -8, 1, $"{r.L3 * 1000:0}");
        w.Dim(L + T, 0, L + T, hw, 10, 1, $"{r.H * 1000:0}");
        w.Text(Lay.Chu, L / 2, -T - 22, Th * 1.2, "MẶT CẮT DỌC", 0, 1, 0);
        // ---- mặt bằng ----
        double yb = -T - 40 - r.B * k, B = r.B * k;
        w.Rect(Lay.BeTong, -T, yb - T, L + T, yb + B + T);
        w.Rect(Lay.BeTong, 0, yb, L, yb + B);
        w.Line(Lay.BeTong, x1, yb, x1, yb + B); w.Line(Lay.BeTong, x2, yb, x2, yb + B);
        w.Circle(Lay.KyHieu, x1 / 2, yb + B / 2, 3); w.Circle(Lay.KyHieu, (x1 + x2) / 2, yb + B / 2, 3); w.Circle(Lay.KyHieu, (x2 + L) / 2, yb + B / 2, 3);
        w.Dim(0, yb, L, yb, -8, 1, $"{r.L * 1000:0}");
        w.Dim(-T, yb, -T, yb + B, -8, 1, $"{r.B * 1000:0}");
        w.Text(Lay.Chu, L / 2, yb - 22, Th * 1.2, "MẶT BẰNG (NẮP THĂM Ø600 MỖI NGĂN)", 0, 1, 0);
        // ---- ghi chú ----
        double yt = H + T + 34;
        w.Text(Lay.Chu, L / 2, yt + 10, Th * 1.6, Title, 0, 1, 0);
        w.Text(Lay.Chu, L / 2, yt + 4, Th, $"N = {r.People} người; Wn = {r.Wn:0.00} m³; Wc = {r.Wc:0.00} m³; W = {r.W:0.00} m³; thực B×L×H = {r.B:0.00}×{r.L:0.00}×{r.H:0.00} = {r.WReal:0.00} m³", 0, 1, 0);
        w.Text(Lay.Chu, L / 2, yt, Th * 0.8, "Theo " + Src, 0, 1, 0);
        return new Figure(w, Title);
    }
}

/// <summary>Gom phần đợt 5 vào Excel "Vật tư điện nước" và bộ bản vẽ điện nước (chỉ khi có dữ liệu – không đổi số bảng cũ).</summary>
public static class MepRound5
{
    public static bool IsSchematic(string name) => name == SepticTank.Title;

    public static List<KetCauVXT.Report.QtyTable> Tables(Project p)
    {
        var res = new List<KetCauVXT.Report.QtyTable>();
        if (p?.Mep == null || p.Mep.IsEmpty) return res;
        var o = p.Mep.Settings?.Standards;
        try { var t = LightingDesign.Table(p); if (t.Rows.Count > 0) res.Add(t); } catch (Exception) { }
        int n = p.Mep.Settings?.Water?.Occupants ?? 0;
        if (n > 0)
        {
            try { var t = DrainStacks.Table(p); if (t.Rows.Count > 0) res.Add(t); } catch (Exception) { }
            try { res.Add(SepticTank.Table(SepticTank.Compute(n, o), o)); } catch (Exception) { }
        }
        return res;
    }

    public static List<Figure> Figures(Project p)
    {
        var res = new List<Figure>();
        if (p?.Mep == null || p.Mep.IsEmpty) return res;
        int n = p.Mep.Settings?.Water?.Occupants ?? 0;
        if (n > 0) try { res.Add(SepticTank.Sketch(SepticTank.Compute(n, p.Mep.Settings?.Standards))); } catch (Exception) { }
        return res;
    }
}
