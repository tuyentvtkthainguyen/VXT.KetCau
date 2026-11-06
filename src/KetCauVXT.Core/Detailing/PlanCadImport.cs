using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using KetCauVXT.Import;
using KetCauVXT.Import.Arch;

namespace KetCauVXT.Detailing;

// =====================================================================
// NHẬP MẶT BẰNG KẾT CẤU VẼ TRONG AUTOCAD + ÁNH XẠ (XREF) MẶT BẰNG KIẾN TRÚC → LANH TÔ CỬA CÁC TẦNG
//  • StructuralPlanImport: DXF (hoặc DWG qua bộ đọc DWG nếu có) / dữ liệu lệnh VXT_MB_KC của add-in AutoCAD →
//      dầm (nét trên layer chứa DAM / BEAM / GIANG: cặp nét song song → tim + bề rộng; 1 nét → tim, bề rộng theo chữ ghi "D1 220x400"),
//      cột (đa tuyến kín trên layer COT / COLUMN → tâm + b × h), tên cấu kiện theo chữ gần nhất (D1, C1 (220x220), S1…),
//      sàn (chữ S1… nằm trong ô dầm). Tên đã có trong hồ sơ → gán; chưa có → tạo cấu kiện mới với tiết diện đọc từ chữ / nét.
//  • ArchUnderlay: đọc mặt bằng kiến trúc của tầng làm nền tham chiếu (tường, cửa) – không sửa; tự nạp lại khi file thay đổi.
//  • DoorLintels: nhận cửa đi / cửa sổ (layer CUA / DOOR / WINDOW hoặc block tên CUA, DOOR, D1, S1…) + tường chứa cửa (chiều dày 110 / 220)
//      → đề xuất lanh tô mỗi ô cửa (Lo = bề rộng ô cửa, b = chiều dày tường), gộp các ô cửa giống nhau (Lo, tường, loại) thành cùng một
//      cấu kiện LT1, LT2… (SL = số ô cửa). Ô văng: OV… trên cửa ở tường ngoài (người dùng bấm chọn cửa).
// Toạ độ m.
// =====================================================================

/// <summary>Một nét / chữ đọc từ bản vẽ (đã đổi về m).</summary>
public sealed class CadSeg
{
    public double X1, Y1, X2, Y2;
    public string Layer = "";
    public double Length => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
}

/// <summary>Kết quả nhập mặt bằng kết cấu.</summary>
public sealed class PlanImportResult
{
    public int Beams, Columns, Slabs, NewMembers;
    public List<string> Log { get; } = new();
    public List<PlanItem> Items { get; } = new();
    public double OffsetX, OffsetY;
    public override string ToString() => $"{Beams} dầm, {Columns} cột, {Slabs} sàn ({NewMembers} cấu kiện mới)";
}

/// <summary>Một ô cửa trên mặt bằng kiến trúc (đã dời theo ánh xạ).</summary>
public sealed class DoorOpening
{
    public double X, Y;
    /// <summary>Bề rộng thông thuỷ ô cửa (m).</summary>
    public double Width;
    /// <summary>"Cửa đi" / "Cửa sổ".</summary>
    public string Kind = "Cửa đi";
    /// <summary>Chiều dày tường chứa cửa (m), 0 = không xác định.</summary>
    public double WallT;
    /// <summary>Phương tường (vector đơn vị).</summary>
    public double Ux = 1, Uy;
    /// <summary>Tường bao ngoài nhà (để gợi ý ô văng).</summary>
    public bool Exterior;
    /// <summary>Phía ngoài nhà theo pháp tuyến (+1 bên trái hướng U, −1 bên phải), 0 = không rõ.</summary>
    public int OutSide;
    public string Block = "";
    public double X1 => X - Ux * Width / 2;
    public double Y1 => Y - Uy * Width / 2;
    public double X2 => X + Ux * Width / 2;
    public double Y2 => Y + Uy * Width / 2;
}

/// <summary>Nền tham chiếu mặt bằng kiến trúc của một tầng.</summary>
public sealed class ArchUnderlay
{
    public string Path = "";
    public DateTime Stamp;
    public List<CadSeg> Lines { get; } = new();
    public List<CadSeg> Walls { get; } = new();
    public List<(CadSeg Center, double T)> WallCenters { get; } = new();
    public List<DoorOpening> Openings { get; } = new();
    public List<string> Warnings { get; } = new();

    private static readonly Dictionary<string, ArchUnderlay> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Đường dẫn thật của ánh xạ: ưu tiên đường dẫn tương đối theo thư mục hồ sơ, sau đó đường dẫn tuyệt đối.</summary>
    public static string Resolve(PlanLevel lv, string projectDir)
    {
        if (lv == null) return null;
        try
        {
            if (!string.IsNullOrEmpty(lv.UnderlayRel) && !string.IsNullOrEmpty(projectDir))
            {
                string p = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectDir, lv.UnderlayRel));
                if (File.Exists(p)) return p;
            }
            if (!string.IsNullOrEmpty(lv.Underlay) && File.Exists(lv.Underlay)) return lv.Underlay;
        }
        catch { }
        return null;
    }

    /// <summary>Gán ánh xạ cho tầng (lưu cả đường dẫn tương đối nếu biết thư mục hồ sơ).</summary>
    public static void Attach(PlanLevel lv, string file, string projectDir)
    {
        if (lv == null) return;
        lv.Underlay = file ?? "";
        lv.UnderlayRel = "";
        try { if (!string.IsNullOrEmpty(projectDir) && !string.IsNullOrEmpty(file)) lv.UnderlayRel = System.IO.Path.GetRelativePath(projectDir, file); } catch { }
        lv.UnderlayVisible = true;
    }

    /// <summary>Nạp (có bộ nhớ đệm theo thời điểm sửa file – file đổi thì đọc lại). null nếu không có / không đọc được.</summary>
    public static ArchUnderlay Load(PlanLevel lv, string projectDir)
    {
        string path = Resolve(lv, projectDir);
        if (path == null) return null;
        DateTime t;
        try { t = File.GetLastWriteTimeUtc(path); } catch { return null; }
        lock (Cache)
        {
            if (Cache.TryGetValue(path, out var c) && c.Stamp == t) return c;
        }
        var r = CadFileReaders.Read(path, 0, out string err);
        var u = FromEntities(r?.Entities ?? new List<DxfEntity>());
        u.Path = path; u.Stamp = t;
        if (!string.IsNullOrEmpty(err)) u.Warnings.Add(err);
        if (r != null) u.Warnings.AddRange(r.Warnings);
        lock (Cache) Cache[path] = u;
        return u;
    }

    /// <summary>Lập nền tham chiếu từ thực thể DXF (toạ độ bản vẽ, chưa dời).</summary>
    public static ArchUnderlay FromEntities(List<DxfEntity> ents, ArchLayerKeys keys = null)
    {
        keys ??= new ArchLayerKeys();
        var u = new ArchUnderlay();
        foreach (var s in PlanCadGeom.Segments(ents))
        {
            u.Lines.Add(s);
            if (keys.IsWall(s.Layer)) u.Walls.Add(s);
        }
        u.WallCenters.AddRange(PlanCadGeom.PairParallel(u.Walls, 0.06, 0.45));
        u.Openings.AddRange(DoorLintels.Detect(ents, u.WallCenters, keys));
        return u;
    }
}

/// <summary>Từ khoá tên layer / block (không dấu, không phân biệt hoa thường).</summary>
public sealed class ArchLayerKeys
{
    public string[] Wall = { "TUONG", "WALL", "XAY", "A-WALL" };
    public string[] Door = { "CUA", "DOOR", "WINDOW", "A-DOOR", "A-GLAZ" };
    public string[] Beam = { "DAM", "BEAM", "GIANG", "S-BEAM" };
    public string[] Column = { "COT", "COLUMN", "S-COLS" };
    public string[] Slab = { "SAN", "SLAB", "S-SLAB" };

    private static bool Has(string layer, string[] keys)
    {
        string n = ArchPlanReader.Norm(layer);
        return keys.Any(k => n.Contains(k));
    }
    // layer chuẩn VXT-… (lệnh VXT_CHUANLAYER / "Chuẩn hoá layer") quyết định trước từ khoá
    private static string Std(string l) => KetCauVXT.Import.Arch.StdLayers.CategoryOf(l);
    public bool IsWall(string l) { string c = Std(l); return c.Length > 0 ? c == "Tuong" : Has(l, Wall) && !Has(l, Door); }
    public bool IsDoor(string l) { string c = Std(l); return c.Length > 0 ? c == "Cua" : Has(l, Door); }
    public bool IsBeam(string l) { string c = Std(l); return c.Length > 0 ? c == "Dam" : Has(l, Beam); }
    public bool IsColumn(string l) { string c = Std(l); return c.Length > 0 ? c == "Cot" : Has(l, Column) && !Has(l, Beam); }
    public bool IsSlab(string l) { string c = Std(l); return c.Length > 0 ? c == "San" : Has(l, Slab); }
    public static bool DoorBlock(string block)
    {
        string n = ArchPlanReader.Norm(block);
        if (n.Length == 0) return false;
        return n.Contains("CUA") || n.Contains("DOOR") || n.Contains("WINDOW") || Regex.IsMatch(n, @"^(D|S|CD|CS|DD)\d");
    }
    public static bool WindowBlock(string block, string layer)
    {
        string n = ArchPlanReader.Norm(block) + " " + ArchPlanReader.Norm(layer);
        return n.Contains("WINDOW") || n.Contains("CUA SO") || n.Contains("CUASO") || n.Contains("GLAZ") || Regex.IsMatch(ArchPlanReader.Norm(block), @"^(S|CS)\d");
    }
}

/// <summary>Hình học dùng chung khi đọc bản vẽ.</summary>
public static class PlanCadGeom
{
    /// <summary>Các đoạn thẳng của LINE / POLYLINE (bỏ chữ, đường tròn).</summary>
    public static IEnumerable<CadSeg> Segments(IEnumerable<DxfEntity> ents)
    {
        foreach (var e in ents ?? Enumerable.Empty<DxfEntity>())
        {
            if (e.Type != DxfEntityType.Line && e.Type != DxfEntityType.Polyline) continue;
            var p = e.Points;
            if (p == null || p.Count < 2) continue;
            int n = p.Count - 1 + (e.Closed && p.Count > 2 ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                var a = p[i]; var b = p[(i + 1) % p.Count];
                var s = new CadSeg { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Layer = e.Layer ?? "" };
                if (s.Length > 1e-4) yield return s;
            }
        }
    }

    /// <summary>Ghép cặp nét song song cách nhau [dMin, dMax] và chồng nhau ≥ 50 % đoạn ngắn → tim + khoảng cách.
    /// Nét không ghép được bị bỏ (trả về riêng qua singles nếu cần).</summary>
    public static List<(CadSeg Center, double T)> PairParallel(List<CadSeg> segs, double dMin, double dMax, List<CadSeg> singles = null)
    {
        var res = new List<(CadSeg, double)>();
        var used = new bool[segs.Count];
        var order = Enumerable.Range(0, segs.Count).OrderByDescending(i => segs[i].Length).ToList();
        foreach (int i in order)
        {
            if (used[i]) continue;
            var a = segs[i];
            double L = a.Length; if (L < 0.2) continue;
            double ux = (a.X2 - a.X1) / L, uy = (a.Y2 - a.Y1) / L;
            int bj = -1; double bestD = double.MaxValue; double bt0 = 0, bt1 = 0;
            for (int j = 0; j < segs.Count; j++)
            {
                if (j == i || used[j]) continue;
                var b = segs[j];
                double Lb = b.Length; if (Lb < 0.2) continue;
                double vx = (b.X2 - b.X1) / Lb, vy = (b.Y2 - b.Y1) / Lb;
                if (Math.Abs(ux * vy - uy * vx) > 0.02) continue;          // không song song
                double d = Math.Abs(-(b.X1 - a.X1) * uy + (b.Y1 - a.Y1) * ux);
                if (d < dMin - 1e-6 || d > dMax + 1e-6) continue;
                double s0 = (b.X1 - a.X1) * ux + (b.Y1 - a.Y1) * uy, s1 = (b.X2 - a.X1) * ux + (b.Y2 - a.Y1) * uy;
                double lo = Math.Max(0, Math.Min(s0, s1)), hi = Math.Min(L, Math.Max(s0, s1));
                if (hi - lo < 0.5 * Math.Min(L, Lb)) continue;
                if (d < bestD) { bestD = d; bj = j; bt0 = lo; bt1 = hi; }
            }
            if (bj < 0) continue;
            used[i] = used[bj] = true;
            var bb = segs[bj];
            // tim: trung điểm hai nét, phạm vi = hợp hai nét chiếu lên a
            double sb0 = (bb.X1 - a.X1) * ux + (bb.Y1 - a.Y1) * uy, sb1 = (bb.X2 - a.X1) * ux + (bb.Y2 - a.Y1) * uy;
            double t0 = Math.Min(0, Math.Min(sb0, sb1)), t1 = Math.Max(L, Math.Max(sb0, sb1));
            double side = -(bb.X1 - a.X1) * uy + (bb.Y1 - a.Y1) * ux;     // dấu: phía của b so với a
            double nx = -uy * Math.Sign(side) * bestD / 2, ny = ux * Math.Sign(side) * bestD / 2;
            res.Add((new CadSeg
            {
                X1 = Math.Round(a.X1 + ux * t0 + nx, 4), Y1 = Math.Round(a.Y1 + uy * t0 + ny, 4),
                X2 = Math.Round(a.X1 + ux * t1 + nx, 4), Y2 = Math.Round(a.Y1 + uy * t1 + ny, 4), Layer = a.Layer
            }, Math.Round(bestD, 4)));
        }
        if (singles != null) for (int i = 0; i < segs.Count; i++) if (!used[i]) singles.Add(segs[i]);
        return res;
    }

    public static double DistToSeg(double x, double y, CadSeg s) => StructuralPlan.DistSeg(x, y, s.X1, s.Y1, s.X2, s.Y2);

    /// <summary>Kích thước "220x400", "220 x 400", "220×400" (mm) trong chữ; null nếu không có.</summary>
    public static (double B, double H)? Section(string text)
    {
        var m = Regex.Match(text ?? "", @"(\d{2,4})\s*[xX×\*]\s*(\d{2,4})");
        if (!m.Success) return null;
        double b = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) / 1000, h = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) / 1000;
        return b > 0.05 && h > 0.05 ? (b, h) : null;
    }

    /// <summary>Tên cấu kiện đầu chữ: "D1", "DM2", "C1", "S3", "LT1", "D1-T3"…; "" nếu không có.</summary>
    public static string MemberName(string text)
    {
        var m = Regex.Match((text ?? "").Trim().ToUpperInvariant(), @"^([A-Z]{1,3}\d{1,3}[A-Z]?(?:-[A-Z0-9.]{1,6})?)");
        return m.Success ? m.Groups[1].Value : "";
    }
}

/// <summary>Nhập mặt bằng kết cấu vẽ trong AutoCAD.</summary>
public static class StructuralPlanImport
{
    /// <summary>Đọc file DXF / DWG → thực thể (m). Lỗi ghi vào log.</summary>
    public static List<DxfEntity> ReadFile(string path, List<string> log)
    {
        var r = CadFileReaders.Read(path, 0, out string err);
        if (!string.IsNullOrEmpty(err)) log?.Add(err);
        if (r != null) log?.AddRange(r.Warnings);
        return r?.Entities ?? new List<DxfEntity>();
    }

    /// <summary>Nhập vào mặt bằng lv. alignOrigin: dời sao cho điểm trái – dưới nhất của cột / dầm trùng gốc trục 1-A (0; 0)
    /// (khi bản vẽ dùng toạ độ tuỳ ý); replace: xoá vị trí dầm / cột / sàn cũ của mặt bằng trước khi nhập.</summary>
    public static PlanImportResult Import(DetailingProject p, PlanLevel lv, List<DxfEntity> ents, bool alignOrigin = true, bool replace = false, ArchLayerKeys keys = null)
    {
        keys ??= new ArchLayerKeys();
        var res = new PlanImportResult();
        if (p == null || lv == null) return res;
        var plan = p.Plan ??= new StructuralPlan();
        ents ??= new();
        var texts = ents.Where(e => e.Type == DxfEntityType.Text && !string.IsNullOrWhiteSpace(e.Text) && e.Points.Count > 0)
                        .Select(e => (X: e.Points[0].X, Y: e.Points[0].Y, S: DxfReader.CleanMText(e.Text).Trim(), Layer: e.Layer ?? "")).ToList();

        // ---- cột: đa tuyến kín trên layer cột ----
        var cols = new List<(double X, double Y, double B, double H)>();
        foreach (var e in ents.Where(e => e.Type == DxfEntityType.Polyline && keys.IsColumn(e.Layer) && e.Points.Count >= 3))
        {
            bool closed = e.Closed || Math.Abs(e.Points[0].X - e.Points[^1].X) + Math.Abs(e.Points[0].Y - e.Points[^1].Y) < 1e-6;
            if (!closed) continue;
            double x0 = e.Points.Min(q => q.X), x1 = e.Points.Max(q => q.X), y0 = e.Points.Min(q => q.Y), y1 = e.Points.Max(q => q.Y);
            double b = x1 - x0, h = y1 - y0;
            if (b < 0.1 || h < 0.1 || b > 1.5 || h > 1.5) continue;
            double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
            if (cols.Any(c => Math.Abs(c.X - cx) < 0.05 && Math.Abs(c.Y - cy) < 0.05)) continue;
            cols.Add((cx, cy, Math.Round(b, 3), Math.Round(h, 3)));
        }
        // ---- dầm: cặp nét song song (tim + b), nét đơn (tim) ----
        var beamSegs = PlanCadGeom.Segments(ents.Where(e => keys.IsBeam(e.Layer))).ToList();
        var singles = new List<CadSeg>();
        var beams = PlanCadGeom.PairParallel(beamSegs, 0.10, 0.60, singles).Select(t => (S: t.Center, B: t.T)).ToList();
        foreach (var s in singles.Where(q => q.Length >= 0.8)) beams.Add((s, 0));

        // gốc toạ độ
        double ox = 0, oy = 0;
        if (alignOrigin)
        {
            var xsAll = cols.Select(c => c.X).Concat(beams.SelectMany(b => new[] { b.S.X1, b.S.X2 })).ToList();
            var ysAll = cols.Select(c => c.Y).Concat(beams.SelectMany(b => new[] { b.S.Y1, b.S.Y2 })).ToList();
            if (xsAll.Count > 0) { ox = xsAll.Min(); oy = ysAll.Min(); }
        }
        res.OffsetX = ox; res.OffsetY = oy;
        if (replace) lv.Items.RemoveAll(i => i.Kind == PlanKinds.Beam || i.Kind == PlanKinds.Column || i.Kind == PlanKinds.Slab);

        // chữ gần nhất (đã dùng thì không dùng lại)
        var usedText = new HashSet<int>();
        int NearText(double x, double y, double maxD, Func<string, bool> ok)
        {
            int bi = -1; double bd = maxD;
            for (int i = 0; i < texts.Count; i++)
            {
                if (usedText.Contains(i) || !ok(texts[i].S)) continue;
                double d = Math.Sqrt((texts[i].X - x) * (texts[i].X - x) + (texts[i].Y - y) * (texts[i].Y - y));
                if (d < bd) { bd = d; bi = i; }
            }
            return bi;
        }
        int NearTextSeg(CadSeg s, double maxD, Func<string, bool> ok)
        {
            int bi = -1; double bd = maxD;
            for (int i = 0; i < texts.Count; i++)
            {
                if (usedText.Contains(i) || !ok(texts[i].S)) continue;
                double d = PlanCadGeom.DistToSeg(texts[i].X, texts[i].Y, s);
                if (d < bd) { bd = d; bi = i; }
            }
            return bi;
        }
        static bool IsCol(string t) { string n = PlanCadGeom.MemberName(t); return n.StartsWith("C") || n.StartsWith("K"); }
        static bool IsBeamName(string t) { string n = PlanCadGeom.MemberName(t); return n.StartsWith("D") || n.StartsWith("G") || n.StartsWith("B"); }
        static bool IsSlabName(string t) { string n = PlanCadGeom.MemberName(t); return n.StartsWith("S") || n.StartsWith("O"); }

        IDetailMember Member(string kind, string name, (double B, double H)? sec, string groupKey)
        {
            IDetailMember m = null;
            if (!string.IsNullOrEmpty(name)) m = p.All.FirstOrDefault(x => x.Name == name && PlanKinds.Accepts(kind, x));
            if (m == null && sec != null)   // cấu kiện chưa tên cùng tiết diện đã tạo trong lần nhập này
                m = p.All.FirstOrDefault(x => string.IsNullOrEmpty(name) && PlanKinds.Accepts(kind, x) && x is ManualBeam mb && kind == PlanKinds.Beam && Math.Abs(mb.B - sec.Value.B) < 1e-6 && Math.Abs(mb.H - sec.Value.H) < 1e-6
                                                 && res.Items.Any(i => i.Member == x.Name))
                    ?? p.All.FirstOrDefault(x => string.IsNullOrEmpty(name) && kind == PlanKinds.Column && x is ManualColumn mc && Math.Abs(mc.B - sec.Value.B) < 1e-6 && Math.Abs(mc.H - sec.Value.H) < 1e-6
                                                 && res.Items.Any(i => i.Member == x.Name));
            if (m != null) return m;
            m = p.AddNew(groupKey);
            if (!string.IsNullOrEmpty(name) && !p.All.Any(x => x != m && x.Name == name)) m.Name = name;
            m.Quantity = 1;
            if (sec is { } sc)
            {
                if (m is ManualBeam b) { b.B = sc.B; b.H = sc.H; }
                else if (m is ManualColumn c) { c.B = sc.B; c.H = sc.H; }
            }
            res.NewMembers++;
            return m;
        }

        foreach (var c in cols)
        {
            int ti = NearText(c.X, c.Y, Math.Max(1.0, 3 * Math.Max(c.B, c.H)), IsCol);
            string nm = ti >= 0 ? PlanCadGeom.MemberName(texts[ti].S) : "";
            var sec = ti >= 0 ? PlanCadGeom.Section(texts[ti].S) : null;
            if (ti >= 0) usedText.Add(ti);
            var m = Member(PlanKinds.Column, nm, sec ?? (c.B, c.H), "Cot");
            var it = new PlanItem { Kind = PlanKinds.Column, Member = m.Name, X1 = Math.Round(c.X - ox, 4), Y1 = Math.Round(c.Y - oy, 4) };
            it.X2 = it.X1; it.Y2 = it.Y1;
            lv.Items.Add(it); res.Items.Add(it); res.Columns++;
        }
        foreach (var (s, bw) in beams)
        {
            int ti = NearTextSeg(s, 1.0, IsBeamName);
            string nm = ti >= 0 ? PlanCadGeom.MemberName(texts[ti].S) : "";
            var sec = ti >= 0 ? PlanCadGeom.Section(texts[ti].S) : null;
            if (ti >= 0) usedText.Add(ti);
            if (sec == null && bw > 0) sec = (Math.Round(bw, 3), Math.Max(0.30, Math.Round(Math.Ceiling(s.Length / 12 / 0.05) * 0.05, 3)));
            // bám đầu dầm vào tâm cột gần (≤ nửa cạnh cột + 0,1 m)
            double x1 = s.X1, y1 = s.Y1, x2 = s.X2, y2 = s.Y2;
            foreach (var c in cols)
            {
                double r = Math.Max(c.B, c.H) / 2 + 0.10;
                if (Math.Abs(x1 - c.X) <= r && Math.Abs(y1 - c.Y) <= r) { if (Math.Abs(y1 - y2) < 1e-3) x1 = c.X; else if (Math.Abs(x1 - x2) < 1e-3) y1 = c.Y; }
                if (Math.Abs(x2 - c.X) <= r && Math.Abs(y2 - c.Y) <= r) { if (Math.Abs(y1 - y2) < 1e-3) x2 = c.X; else if (Math.Abs(x1 - x2) < 1e-3) y2 = c.Y; }
            }
            var it = new PlanItem { Kind = PlanKinds.Beam, X1 = Math.Round(x1 - ox, 4), Y1 = Math.Round(y1 - oy, 4), X2 = Math.Round(x2 - ox, 4), Y2 = Math.Round(y2 - oy, 4) };
            var m = Member(PlanKinds.Beam, nm, sec, "Dam");
            it.Member = m.Name;
            lv.Items.Add(it); res.Items.Add(it); res.Beams++;
            if (m is ManualBeam mb && res.Items.Count(i => i.Member == m.Name) == 1) plan.ApplyGeometry(mb, it, lv);
        }
        // ---- sàn: chữ S… nằm trong ô dầm ----
        for (int i = 0; i < texts.Count; i++)
        {
            if (usedText.Contains(i) || !IsSlabName(texts[i].S)) continue;
            var bay = PlanSnap.BayAt(plan, lv, texts[i].X - ox, texts[i].Y - oy);
            if (bay == null) continue;
            var (x0, y0, x1, y1) = bay.Value;
            string nm = PlanCadGeom.MemberName(texts[i].S);
            var m = Member(PlanKinds.Slab, nm, null, "San");
            var it = new PlanItem { Kind = PlanKinds.Slab, Member = m.Name, X1 = x0, Y1 = y0, X2 = x1, Y2 = y1 };
            lv.Items.Add(it); res.Items.Add(it); res.Slabs++;
            if (res.Items.Count(q => q.Member == m.Name) == 1) plan.ApplyGeometry(m, it, lv);
            usedText.Add(i);
        }
        plan.SyncQuantities(p);
        res.Log.Add("Nhập mặt bằng kết cấu vào " + lv.Name + ": " + res + (alignOrigin && (Math.Abs(ox) + Math.Abs(oy) > 1e-6) ? $" – dời gốc ({Dt.F(ox, "0.###")}; {Dt.F(oy, "0.###")}) m về giao trục đầu" : ""));
        return res;
    }

    // ------------------------------------------------------------------
    // trao đổi với add-in AutoCAD: lệnh VXT_MB_KC ghi %AppData%\KetCauVXT\AcadPick\plan_*.json
    // { "kind":"matbang", "unit":"m", "entities":[ {"t":"line","layer":"DAM","pts":[[x,y],[x,y]],"closed":false}, {"t":"text","layer":"..","x":..,"y":..,"s":"D1 220x400"} ] }
    // ------------------------------------------------------------------
    /// <summary>File plan_*.json mới nhất trong thư mục trao đổi (mới hơn newerThan); null nếu không có.</summary>
    public static string LatestExchange(DateTime? newerThan = null)
    {
        try
        {
            if (!Directory.Exists(AcadPick.Folder)) return null;
            var f = new DirectoryInfo(AcadPick.Folder).GetFiles("plan_*.json").OrderByDescending(x => x.LastWriteTime).ThenByDescending(x => x.Name).FirstOrDefault();
            if (f == null || (newerThan.HasValue && f.LastWriteTime <= newerThan.Value)) return null;
            return f.FullName;
        }
        catch { return null; }
    }

    /// <summary>Đọc dữ liệu VXT_MB_KC → thực thể (m). Lỗi ghi vào log.</summary>
    public static List<DxfEntity> ParseExchange(string json, List<string> log = null)
    {
        var ents = new List<DxfEntity>();
        try
        {
            using var doc = JsonDocument.Parse(json ?? "", new JsonDocumentOptions { AllowTrailingCommas = true });
            var root = doc.RootElement;
            double k = 1;
            if (root.TryGetProperty("unit", out var u) && u.ValueKind == JsonValueKind.String)
            {
                string un = (u.GetString() ?? "").ToLowerInvariant();
                k = un == "mm" ? 0.001 : un == "cm" ? 0.01 : 1;
            }
            if (!root.TryGetProperty("entities", out var arr) || arr.ValueKind != JsonValueKind.Array) { log?.Add("Dữ liệu không có \"entities\""); return ents; }
            foreach (var e in arr.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                string t = e.TryGetProperty("t", out var tt) ? tt.GetString() ?? "" : "";
                string layer = e.TryGetProperty("layer", out var ly) ? ly.GetString() ?? "" : "";
                if (t == "text")
                {
                    double x = e.TryGetProperty("x", out var xx) && xx.TryGetDouble(out double xv) ? xv * k : 0;
                    double y = e.TryGetProperty("y", out var yy) && yy.TryGetDouble(out double yv) ? yv * k : 0;
                    string s = e.TryGetProperty("s", out var ss) ? ss.GetString() ?? "" : "";
                    ents.Add(new DxfEntity { Type = DxfEntityType.Text, Layer = layer, Text = s, Points = new() { (x, y) } });
                    continue;
                }
                var pts = new List<(double X, double Y)>();
                if (e.TryGetProperty("pts", out var pa) && pa.ValueKind == JsonValueKind.Array)
                    foreach (var q in pa.EnumerateArray())
                        if (q.ValueKind == JsonValueKind.Array && q.GetArrayLength() >= 2 && q[0].TryGetDouble(out double qx) && q[1].TryGetDouble(out double qy))
                            pts.Add((qx * k, qy * k));
                if (pts.Count < 2) continue;
                bool closed = e.TryGetProperty("closed", out var cl) && cl.ValueKind == JsonValueKind.True;
                ents.Add(new DxfEntity { Type = pts.Count == 2 && !closed ? DxfEntityType.Line : DxfEntityType.Polyline, Layer = layer, Points = pts, Closed = closed,
                    Block = e.TryGetProperty("block", out var bl) ? bl.GetString() ?? "" : "" });
            }
        }
        catch (Exception ex) { log?.Add("Không đọc được dữ liệu AutoCAD: " + ex.Message); }
        return ents;
    }
}

/// <summary>Lanh tô từ mặt bằng cửa kiến trúc.</summary>
public static class DoorLintels
{
    /// <summary>Nhận ô cửa: thực thể trên layer cửa hoặc thuộc block cửa, gom cụm theo khung bao (khe ≤ 0,10 m); gán tường chứa cửa gần nhất
    /// (tim tường cách tâm cụm ≤ bề rộng cụm), bề rộng = kích thước cụm theo phương tường (0,5 … 4,0 m).</summary>
    public static List<DoorOpening> Detect(List<DxfEntity> ents, List<(CadSeg Center, double T)> walls, ArchLayerKeys keys = null)
    {
        keys ??= new ArchLayerKeys();
        var res = new List<DoorOpening>();
        var de = (ents ?? new()).Where(e => e.Type != DxfEntityType.Text && e.Points.Count > 0 && (keys.IsDoor(e.Layer) || ArchLayerKeys.DoorBlock(e.Block))).ToList();
        // gom cụm
        var boxes = de.Select(e => (X0: e.Points.Min(q => q.X), Y0: e.Points.Min(q => q.Y), X1: e.Points.Max(q => q.X), Y1: e.Points.Max(q => q.Y), E: e)).ToList();
        var groups = new List<List<int>>();
        var gid = new int[boxes.Count]; for (int i = 0; i < gid.Length; i++) gid[i] = -1;
        for (int i = 0; i < boxes.Count; i++)
        {
            if (gid[i] >= 0) continue;
            var g = new List<int> { i }; gid[i] = groups.Count;
            for (int q = 0; q < g.Count; q++)
            {
                var a = boxes[g[q]];
                for (int j = 0; j < boxes.Count; j++)
                {
                    if (gid[j] >= 0) continue;
                    var b = boxes[j];
                    bool sameBlock = !string.IsNullOrEmpty(a.E.Block) && a.E.Block == b.E.Block;
                    double gap = 0.10;
                    if (b.X0 <= a.X1 + gap && b.X1 >= a.X0 - gap && b.Y0 <= a.Y1 + gap && b.Y1 >= a.Y0 - gap && (sameBlock || string.IsNullOrEmpty(a.E.Block) || string.IsNullOrEmpty(b.E.Block)))
                    { gid[j] = groups.Count; g.Add(j); }
                }
            }
            groups.Add(g);
        }
        foreach (var g in groups)
        {
            double x0 = g.Min(i => boxes[i].X0), x1 = g.Max(i => boxes[i].X1), y0 = g.Min(i => boxes[i].Y0), y1 = g.Max(i => boxes[i].Y1);
            double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
            double size = Math.Max(x1 - x0, y1 - y0);
            if (size < 0.4 || size > 4.5) continue;
            // tường chứa cửa
            (CadSeg Center, double T)? wall = null; double bd = double.MaxValue;
            foreach (var w in walls ?? new())
            {
                double d = PlanCadGeom.DistToSeg(cx, cy, w.Center);
                if (d < bd && d <= size + 0.05) { bd = d; wall = w; }
            }
            double ux = x1 - x0 >= y1 - y0 ? 1 : 0, uy = 1 - ux, wt = 0;
            double px = cx, py = cy;
            if (wall is { } ww)
            {
                var s = ww.Center; double L = s.Length;
                ux = (s.X2 - s.X1) / L; uy = (s.Y2 - s.Y1) / L; wt = ww.T;
                if (ux < -1e-9 || (Math.Abs(ux) < 1e-9 && uy < 0)) { ux = -ux; uy = -uy; }
                // bề rộng theo phương tường: phạm vi các điểm của cụm chiếu lên phương tường
                var proj = g.SelectMany(i => boxes[i].E.Points).Select(q => (q.X - s.X1) * ux + (q.Y - s.Y1) * uy).ToList();
                double p0 = proj.Min(), p1 = proj.Max();
                double tc = (p0 + p1) / 2;
                px = s.X1 + ux * tc; py = s.Y1 + uy * tc;
                size = p1 - p0;
            }
            else size = ux > 0 ? x1 - x0 : y1 - y0;
            if (size < 0.4 || size > 4.5) continue;
            bool win = g.Any(i => ArchLayerKeys.WindowBlock(boxes[i].E.Block, boxes[i].E.Layer));
            bool hasArc = g.Any(i => boxes[i].E.Arcs != null && boxes[i].E.Arcs.Count > 0);
            res.Add(new DoorOpening
            {
                X = Math.Round(px, 4), Y = Math.Round(py, 4), Width = Math.Round(size, 3), Ux = ux, Uy = uy, WallT = Math.Round(wt, 3),
                Kind = win && !hasArc ? "Cửa sổ" : "Cửa đi", Block = g.Select(i => boxes[i].E.Block).FirstOrDefault(b => !string.IsNullOrEmpty(b)) ?? ""
            });
        }
        // tường ngoài: tim tường nằm trên biên khung bao của toàn bộ tường
        if ((walls?.Count ?? 0) > 0 && res.Count > 0)
        {
            double wx0 = walls.Min(w => Math.Min(w.Center.X1, w.Center.X2)), wx1 = walls.Max(w => Math.Max(w.Center.X1, w.Center.X2));
            double wy0 = walls.Min(w => Math.Min(w.Center.Y1, w.Center.Y2)), wy1 = walls.Max(w => Math.Max(w.Center.Y1, w.Center.Y2));
            foreach (var o in res)
            {
                double tolE = Math.Max(o.WallT, 0.11);
                if (Math.Abs(o.Uy) < 1e-6 && Math.Abs(o.Y - wy0) <= tolE) { o.Exterior = true; o.OutSide = -1; }
                else if (Math.Abs(o.Uy) < 1e-6 && Math.Abs(o.Y - wy1) <= tolE) { o.Exterior = true; o.OutSide = 1; }
                else if (Math.Abs(o.Ux) < 1e-6 && Math.Abs(o.X - wx0) <= tolE) { o.Exterior = true; o.OutSide = 1; }
                else if (Math.Abs(o.Ux) < 1e-6 && Math.Abs(o.X - wx1) <= tolE) { o.Exterior = true; o.OutSide = -1; }
            }
        }
        return res;
    }

    /// <summary>Khoá gộp cấu kiện lanh tô: (Lo làm tròn 50 mm, tường 110 / 220, loại cửa).</summary>
    public static (double Lo, double T, string Kind) Key(DoorOpening o) =>
        (Math.Round(Math.Ceiling(o.Width / 0.05 - 1e-6) * 0.05, 3), o.WallT > 0 && o.WallT <= 0.15 ? 0.11 : 0.22, o.Kind);

    /// <summary>Đề xuất / đặt lanh tô cho các ô cửa trên mặt bằng lv (toạ độ ô cửa đã dời theo ánh xạ).
    /// Ô cửa đã có lanh tô tại chỗ (tâm cách ≤ 0,2 m) bỏ qua. Các ô cửa cùng khoá → cùng cấu kiện LTn (SL = số ô cửa, đồng bộ theo mặt bằng).
    /// Trả về các vị trí lanh tô mới.</summary>
    public static List<PlanItem> Place(DetailingProject p, PlanLevel lv, IEnumerable<DoorOpening> openings)
    {
        var added = new List<PlanItem>();
        if (p == null || lv == null) return added;
        var plan = p.Plan ??= new StructuralPlan();
        var byKey = new Dictionary<(double, double, string), ManualLintel>();
        foreach (var o in openings ?? Enumerable.Empty<DoorOpening>())
        {
            if (lv.Items.Any(i => i.Kind == PlanKinds.Lintel && Math.Abs((i.X1 + i.X2) / 2 - o.X) < 0.2 && Math.Abs((i.Y1 + i.Y2) / 2 - o.Y) < 0.2)) continue;
            var key = Key(o);
            if (!byKey.TryGetValue(key, out var lt))
            {
                lt = p.Lintels.FirstOrDefault(l => Math.Abs(l.Lo - key.Lo) < 1e-6 && Math.Abs(l.Bw - key.T) < 0.02 && l.Type == ManualLintel.TypeLintel
                                                   && (l.Name ?? "").StartsWith("LT"));
                if (lt == null)
                {
                    lt = new ManualLintel { Name = PlanMemberMap.NextName(p, "LT"), Type = ManualLintel.TypeLintel, Lo = key.Lo, Bw = key.T, Quantity = 1, AnchoredEnds = false };
                    p.Lintels.Add(lt);
                }
                byKey[key] = lt;
            }
            var it = new PlanItem { Kind = PlanKinds.Lintel, Member = lt.Name, X1 = Math.Round(o.X1, 4), Y1 = Math.Round(o.Y1, 4), X2 = Math.Round(o.X2, 4), Y2 = Math.Round(o.Y2, 4),
                Label = o.Kind + " " + Dt.Mm(o.Width) };
            lv.Items.Add(it); added.Add(it);
        }
        plan.SyncQuantities(p);
        return added;
    }

    /// <summary>Ô văng trên cửa ở tường ngoài: hình chữ nhật dài Lo + 2·0,2 m, vươn lc ra phía ngoài (OutSide; không rõ → +1).
    /// Cấu kiện OVn loại "Lanh tô + ô văng liền khối" (gộp theo Lo, tường, vươn). Trả về vị trí ô văng (và đổi lanh tô của cửa sang cấu kiện liền khối).</summary>
    public static PlanItem PlaceCanopy(DetailingProject p, PlanLevel lv, DoorOpening o, double lc = 0.6)
    {
        if (p == null || lv == null || o == null) return null;
        var plan = p.Plan ??= new StructuralPlan();
        var key = Key(o);
        var ov = p.Lintels.FirstOrDefault(l => (l.Name ?? "").StartsWith("OV") && Math.Abs(l.Lo - key.Lo) < 1e-6 && Math.Abs(l.Bw - key.T) < 0.02 && Math.Abs(l.Lc - lc) < 1e-6);
        if (ov == null)
        {
            ov = new ManualLintel { Name = PlanMemberMap.NextName(p, "OV"), Type = ManualLintel.TypeCombined, Lo = key.Lo, Bw = key.T, Lc = lc, Quantity = 1, AnchoredEnds = true };
            p.Lintels.Add(ov);
        }
        int side = o.OutSide != 0 ? o.OutSide : 1;
        double nx = -o.Uy * side, ny = o.Ux * side;
        double half = o.Width / 2 + 0.2, off = Math.Max(o.WallT, 0.11) / 2;
        double ax = o.X - o.Ux * half + nx * off, ay = o.Y - o.Uy * half + ny * off;
        double bx = o.X + o.Ux * half + nx * (off + lc), by = o.Y + o.Uy * half + ny * (off + lc);
        // lanh tô của cửa → cấu kiện liền khối (lanh tô + ô văng)
        foreach (var lt in lv.Items.Where(i => i.Kind == PlanKinds.Lintel && Math.Abs((i.X1 + i.X2) / 2 - o.X) < 0.2 && Math.Abs((i.Y1 + i.Y2) / 2 - o.Y) < 0.2)) lt.Member = ov.Name;
        if (!lv.Items.Any(i => i.Kind == PlanKinds.Lintel && i.Member == ov.Name && Math.Abs((i.X1 + i.X2) / 2 - o.X) < 0.2 && Math.Abs((i.Y1 + i.Y2) / 2 - o.Y) < 0.2))
            lv.Items.Add(new PlanItem { Kind = PlanKinds.Lintel, Member = ov.Name, X1 = o.X1, Y1 = o.Y1, X2 = o.X2, Y2 = o.Y2, Label = o.Kind });
        var it = new PlanItem { Kind = PlanKinds.Canopy, Member = ov.Name, X1 = Math.Round(Math.Min(ax, bx), 4), Y1 = Math.Round(Math.Min(ay, by), 4),
            X2 = Math.Round(Math.Max(ax, bx), 4), Y2 = Math.Round(Math.Max(ay, by), 4), Rotation = side };
        lv.Items.Add(it);
        plan.SyncQuantities(p);
        // lanh tô cũ không còn vị trí nào → giữ trong hồ sơ (người dùng tự xoá) nhưng SL không đổi
        return it;
    }

    /// <summary>Ô cửa dời theo ánh xạ của tầng (UnderlayDx/Dy).</summary>
    public static List<DoorOpening> Shifted(ArchUnderlay u, PlanLevel lv) =>
        (u?.Openings ?? new()).Select(o => new DoorOpening
        {
            X = o.X + (lv?.UnderlayDx ?? 0), Y = o.Y + (lv?.UnderlayDy ?? 0), Width = o.Width, Kind = o.Kind, WallT = o.WallT, Ux = o.Ux, Uy = o.Uy,
            Exterior = o.Exterior, OutSide = o.OutSide, Block = o.Block
        }).ToList();
}
