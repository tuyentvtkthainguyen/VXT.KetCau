using System.Globalization;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Arch;

// =====================================================================
// REVIT CHỈ CÓ KIẾN TRÚC → MẶT BẰNG KIẾN TRÚC CHUNG (ArchDrawing)
// Đọc file *.vxtrevit.json (add-in KetCauVXT.RevitExport) khi mô hình chưa có cột / dầm kết cấu: tường (lớp cấu tạo → trọng lượng),
// cửa (trừ lỗ trên tường chủ), phòng (tên → công năng, đường bao thật → vùng cầu thang / thông tầng / ban công), trục, cao độ.
// Sau đó dùng chung bộ đề xuất kết cấu của "Nhập bản vẽ kiến trúc": ArchToStructure.Propose → Apply.
//   Mặt bằng tầng = cao độ (Level) có tường cao ≥ 1,5 m; chiều cao tầng = cao độ kế trên − cao độ này.
//   Cao độ trên cùng không có tường cao (chỉ tường chắn mái / sàn) = mặt bằng mái (phòng sân thượng nếu có).
// =====================================================================
public static class RevitArchReader
{
    /// <summary>Tường thấp hơn giá trị này coi là tường chắn mái / lan can, không tạo mặt bằng tầng (m).</summary>
    public const double MinStoreyWall = 1.5;

    /// <summary>Mô hình Revit có cột / dầm kết cấu (Structural Columns / Framing) – khi đó dùng RevitJsonImporter như cũ.</summary>
    public static bool HasStructure(RevitExchange ex) => ex != null && ((ex.Columns?.Count ?? 0) > 0 || (ex.Beams?.Count ?? 0) > 0);

    /// <summary>Lập ArchDrawing từ phần kiến trúc của file Revit. Trọng lượng tường theo lớp cấu tạo (RevitMaterials).</summary>
    public static ArchDrawing ToDrawing(RevitExchange ex, ArchImportOptions o)
    {
        if (ex == null) throw new ArgumentNullException(nameof(ex));
        o ??= new ArchImportOptions();
        var d = new ArchDrawing { Source = "Revit: " + ex.ProjectName };
        var walls = (ex.Walls ?? new List<RvWall>()).Where(w => w != null).ToList();
        var levels = (ex.Levels ?? new List<RvLevel>()).Where(l => l != null).OrderBy(l => l.Elevation).ToList();
        if (walls.Count == 0) throw new InvalidOperationException("File Revit không có tường – cần mô hình kiến trúc có tường (Walls) để triển khai kết cấu.");
        if (levels.Count == 0) throw new InvalidOperationException("File Revit không có cao độ (Level).");
        var lvZ = levels.GroupBy(l => l.Name ?? "").ToDictionary(g => g.Key, g => g.First().Elevation);

        // ---- gốc toạ độ: trục nhỏ nhất (hoặc mép tường nhỏ nhất) ----
        var rg = (ex.Grids ?? new List<RvGrid>()).Where(g => g != null).ToList();
        var gxs = rg.Where(IsVert).ToList(); var gys = rg.Where(IsHorz).ToList();
        double ox = gxs.Count > 0 ? gxs.Min(g => (g.X1 + g.X2) / 2) : walls.Min(w => Math.Min(w.X1, w.X2));
        double oy = gys.Count > 0 ? gys.Min(g => (g.Y1 + g.Y2) / 2) : walls.Min(w => Math.Min(w.Y1, w.Y2));
        ox = Math.Round(ox, 4); oy = Math.Round(oy, 4);

        // ---- cao độ có tường tầng ----
        bool Tall(RvWall w) => w.Height >= MinStoreyWall;
        var storeyLevels = levels.Where(l => walls.Any(w => w.Level == l.Name && Tall(w))).ToList();
        if (storeyLevels.Count == 0) throw new InvalidOperationException($"Không có tường nào cao ≥ {MinStoreyWall} m – không xác định được tầng.");

        var plans = new List<ArchFloorPlan>();
        int no = 1;
        foreach (var lv in storeyLevels)
        {
            var above = levels.FirstOrDefault(l => l.Elevation > lv.Elevation + 1.5);
            var lvWalls = walls.Where(w => w.Level == lv.Name && Tall(w)).ToList();
            double h = above != null ? above.Elevation - lv.Elevation : lvWalls.Max(w => w.Height + Math.Max(0, w.BaseOffset));
            var plan = new ArchFloorPlan
            {
                Title = "MẶT BẰNG " + (string.IsNullOrWhiteSpace(lv.Name) ? "TẦNG " + no : lv.Name.ToUpperInvariant()) + $" (+{lv.Elevation.ToString("0.000", CultureInfo.InvariantCulture)})",
                FloorNo = no++, Height = Math.Round(Math.Max(h, 2.2), 3),
            };
            ReadPlan(plan, lv.Name, lvWalls, ex, o, ox, oy, gxs, gys, d);
            plans.Add(plan);
        }
        // tum: tầng trên cùng có diện tích < 50 % tầng 1
        if (plans.Count >= 2)
        {
            double a1 = Math.Abs(ImportGeometry.SignedArea(plans[0].Outline)), an = Math.Abs(ImportGeometry.SignedArea(plans[^1].Outline));
            if (a1 > 1 && an < 0.5 * a1) { plans[^1].IsTum = true; plans[^1].Notes.Add("Diện tích < 50 % tầng 1 – coi là tum thang."); }
        }
        // mặt bằng mái: cao độ trên tầng trên cùng (phòng sân thượng, tường chắn mái)
        var top = storeyLevels[^1];
        var roofLv = levels.FirstOrDefault(l => l.Elevation > top.Elevation + 1.5);
        if (roofLv != null)
        {
            var roof = new ArchFloorPlan { Title = "MẶT BẰNG MÁI " + (roofLv.Name ?? "").ToUpperInvariant(), FloorNo = 99, IsRoofPlan = true, Height = 0 };
            ReadRooms(roof, roofLv.Name, ex, o, ox, oy);
            var parapet = walls.Where(w => w.Level == roofLv.Name && !Tall(w)).ToList();
            if (parapet.Count > 0)
            {
                double ph = parapet.Average(w => w.Height);
                roof.Notes.Add($"{parapet.Count} đoạn tường chắn mái cao trung bình {ph:0.00} m.");
                o.ParapetHeight = Math.Round(ph, 2);
            }
            plans.Add(roof);
        }
        d.Floors = plans;
        d.OriginX = ox; d.OriginY = oy; d.GroundZ = storeyLevels[0].Elevation;
        d.Info.Add($"Revit: {plans.Count(p => !p.IsRoofPlan)} mặt bằng tầng"
                   + (plans.Any(p => p.IsRoofPlan) ? " + mặt bằng mái" : "") + $", gốc toạ độ dời về ({ox:0.000}; {oy:0.000}).");
        foreach (var p in plans)
            d.Info.Add($"  {p.Title}: {p.Walls.Count} đoạn tường ({p.Walls.Sum(w => w.Length):0.0} m), {p.Grids.Count} trục{(p.Grids.Any(g => g.Auto) ? " (tự lập theo tường)" : "")}, {p.Rooms.Count} phòng, {p.Openings.Count} cửa, {p.Zones.Count} vùng đặc biệt");
        return d;
    }

    private static void ReadPlan(ArchFloorPlan plan, string level, List<RvWall> rw, RevitExchange ex, ArchImportOptions o, double ox, double oy,
                                 List<RvGrid> gxs, List<RvGrid> gys, ArchDrawing d)
    {
        // ---- tường: quy về song song trục nếu lệch < 1°, nối thẳng hàng ----
        var byId = new Dictionary<string, ArchWall>();
        foreach (var w in rw)
        {
            double ax = w.X1 - ox, ay = w.Y1 - oy, bx = w.X2 - ox, by = w.Y2 - oy;
            double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            if (len < o.MinWallLength) continue;
            double ang = Math.Abs(Math.Atan2(by - ay, bx - ax) * 180 / Math.PI) % 180;
            if (ang < 1 || ang > 179) { double yy = (ay + by) / 2; ay = by = yy; }
            else if (Math.Abs(ang - 90) < 1) { double xx = (ax + bx) / 2; ax = bx = xx; }
            if ((Math.Abs(ay - by) < 1e-9 && ax > bx) || (Math.Abs(ax - bx) < 1e-9 && ay > by)) { (ax, bx) = (bx, ax); (ay, by) = (by, ay); }
            var mass = RevitMaterials.WallMass(w, o);
            var aw = new ArchWall
            {
                Ax = R(ax), Ay = R(ay), Bx = R(bx), By = R(by),
                Thickness = mass.Thickness, UnitWeight = mass.EquivalentUnitWeight, TypeName = w.TypeName ?? "", Layer = "Revit:" + (w.TypeName ?? ""),
            };
            if (aw.Thickness < o.MinWallThickness) continue;   // vách kính, tường trang trí mỏng
            plan.Walls.Add(aw);
            if (!string.IsNullOrEmpty(w.Id)) byId[w.Id] = aw;
            plan.Preview.Add(new PreviewLine(aw.Ax, aw.Ay, aw.Bx, aw.By, 1));
        }

        // ---- cửa: trừ trên tường chủ ----
        foreach (var op in (ex.Openings ?? new List<RvOpening>()).Where(q => q != null && q.HostId != null && byId.ContainsKey(q.HostId)))
        {
            var w = byId[op.HostId];
            w.OpeningLength += Math.Max(0, op.Width);
            plan.Openings.Add(new ArchOpening { X = op.X - ox, Y = op.Y - oy, Width = Math.Round(op.Width, 2), Kind = op.Category == "Window" ? "Cửa sổ" : "Cửa đi" });
        }

        // ---- trục ----
        foreach (var g in gxs) plan.Grids.Add(new ArchGrid { Name = g.Name ?? "", Dir = GridDir.X, Coord = R((g.X1 + g.X2) / 2 - ox) });
        foreach (var g in gys) plan.Grids.Add(new ArchGrid { Name = g.Name ?? "", Dir = GridDir.Y, Coord = R((g.Y1 + g.Y2) / 2 - oy) });
        if (plan.Grids.Count(g => g.Dir == GridDir.X) < 2 || plan.Grids.Count(g => g.Dir == GridDir.Y) < 2)
        {
            plan.Grids.Clear();
            plan.Grids.AddRange(ArchPlanReader.GridsFromWalls(plan.Walls, GridDir.X));
            plan.Grids.AddRange(ArchPlanReader.GridsFromWalls(plan.Walls, GridDir.Y));
            plan.Notes.Add("Mô hình Revit không có đủ lưới trục – tự lập trục theo tim tường chính, kiểm tra lại.");
        }

        // ---- đường bao, tường ngoài ----
        plan.Outline = ArchPlanReader.Footprint(plan.Walls, plan.Columns);
        foreach (var w in plan.Walls) w.Exterior = ArchPlanReader.OnOutline(plan.Outline, w);
        if (plan.Outline.Count >= 3)
            for (int i = 0; i < plan.Outline.Count; i++)
            {
                var a = plan.Outline[i]; var b = plan.Outline[(i + 1) % plan.Outline.Count];
                plan.Preview.Add(new PreviewLine(a.X, a.Y, b.X, b.Y, 7));
            }

        ReadRooms(plan, level, ex, o, ox, oy);
        if (plan.Walls.Count == 0) d.Warnings.Add($"{plan.Title}: không còn tường nào sau khi lọc (tường quá ngắn / quá mỏng).");
    }

    /// <summary>Phòng → tên + công năng tại tâm; cầu thang / thông tầng / ban công → vùng theo đường bao thật.</summary>
    private static void ReadRooms(ArchFloorPlan plan, string level, RevitExchange ex, ArchImportOptions o, double ox, double oy)
    {
        foreach (var r in (ex.Rooms ?? new List<RvRoom>()).Where(q => q != null && q.Level == level && q.Outline != null && q.Outline.Count >= 3))
        {
            var pts = r.Outline.Where(a => a != null && a.Length >= 2).Select(a => (X: a[0] - ox, Y: a[1] - oy)).ToList();
            if (pts.Count < 3) continue;
            var (cx, cy) = Center(pts);
            var rt = ArchPlanReader.Classify(ArchPlanReader.Norm(r.Name ?? ""), o.Rooms);
            var kind = rt?.Kind ?? RoomKind.Unknown;
            plan.Rooms.Add(new ArchRoom { Text = string.IsNullOrWhiteSpace(r.Name) ? "Phòng " + r.Number : r.Name.Trim(), Kind = kind, X = cx, Y = cy });
            if (kind is RoomKind.Stair or RoomKind.Void or RoomKind.Balcony)
                plan.Zones.Add(new ArchZone { Kind = kind, X0 = R(pts.Min(q => q.X)), Y0 = R(pts.Min(q => q.Y)), X1 = R(pts.Max(q => q.X)), Y1 = R(pts.Max(q => q.Y)), Note = "phòng Revit \"" + r.Name + "\"" });
        }
    }

    /// <summary>Trọng tâm đa giác; nếu nằm ngoài (phòng chữ L, U) lấy trung điểm cạnh dài nhất dịch vào trong.</summary>
    internal static (double X, double Y) Center(List<(double X, double Y)> pts)
    {
        double a = 0, cx = 0, cy = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i]; var q = pts[(i + 1) % pts.Count];
            double cr = p.X * q.Y - q.X * p.Y;
            a += cr; cx += (p.X + q.X) * cr; cy += (p.Y + q.Y) * cr;
        }
        if (Math.Abs(a) < 1e-9) return (pts.Average(p => p.X), pts.Average(p => p.Y));
        cx /= 3 * a; cy /= 3 * a;
        if (ArchPlanReader.PointInPoly(pts, cx, cy)) return (cx, cy);
        // thử các điểm lưới trong hộp bao, lấy điểm trong đa giác gần trọng tâm nhất
        double x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X), y0 = pts.Min(p => p.Y), y1 = pts.Max(p => p.Y);
        (double, double) best = (pts.Average(p => p.X), pts.Average(p => p.Y)); double bd = double.MaxValue;
        for (int i = 1; i < 10; i++)
            for (int j = 1; j < 10; j++)
            {
                double x = x0 + (x1 - x0) * i / 10, y = y0 + (y1 - y0) * j / 10;
                if (!ArchPlanReader.PointInPoly(pts, x, y)) continue;
                double dd = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (dd < bd) { bd = dd; best = (x, y); }
            }
        return best;
    }

    private static bool IsVert(RvGrid g)
    {
        double dx = g.X2 - g.X1, dy = g.Y2 - g.Y1, L = Math.Sqrt(dx * dx + dy * dy);
        return L > 1e-6 && Math.Abs(dx) < 0.005 * L;
    }

    private static bool IsHorz(RvGrid g)
    {
        double dx = g.X2 - g.X1, dy = g.Y2 - g.Y1, L = Math.Sqrt(dx * dx + dy * dy);
        return L > 1e-6 && Math.Abs(dy) < 0.005 * L;
    }

    private static double R(double v) => Math.Round(v, 4);
}

/// <summary>Trọng lượng tường theo lớp cấu tạo Revit: nhận vật liệu theo tên (tiếng Việt / tiếng Anh, có / không dấu).</summary>
public static class RevitMaterials
{
    /// <summary>Một nhóm vật liệu: từ khoá (không dấu, chữ hoa), trọng lượng riêng kN/m³, tên hiển thị.</summary>
    public sealed record Kind(string Name, double Gamma, string[] Keys);

    /// <summary>Thứ tự quan trọng: từ khoá cụ thể trước (gạch rỗng trước gạch, bê tông nhẹ trước bê tông).</summary>
    public static readonly IReadOnlyList<Kind> Kinds = new List<Kind>
    {
        new("Kính, vách nhẹ", 0, new[] { "KINH", "GLASS", "GLAZ", "CURTAIN" }),
        new("Thạch cao, tấm nhẹ", 10, new[] { "THACH CAO", "GYPSUM", "PLASTERBOARD", "CEMBOARD", "CEMENT BOARD", "PANEL" }),
        new("Bê tông nhẹ / gạch AAC", 8, new[] { "AAC", "BE TONG NHE", "BT NHE", "BE TONG KHI", "LIGHTWEIGHT", "BLOCK NHE", "ALC", "EPS" }),
        new("Gạch rỗng", 15, new[] { "GACH RONG", "GACH LO", "HOLLOW", "2 LO", "4 LO", "6 LO", "GACH ONG" }),
        new("Gạch đặc", 18, new[] { "GACH DAC", "GACH CHI", "GACH THE", "SOLID BRICK", "BRICK", "GACH", "MASONRY", "XAY" }),
        new("Bê tông cốt thép", 25, new[] { "BE TONG", "BETONG", "BTCT", "CONCRETE", "BT " }),
        new("Đá ốp", 26, new[] { "DA OP", "GRANITE", "MARBLE", "STONE", "DA " }),
        new("Gạch ốp, ceramic", 20, new[] { "OP GACH", "GACH OP", "CERAMIC", "TILE", "PORCELAIN" }),
        new("Vữa trát", 18, new[] { "VUA", "TRAT", "PLASTER", "MORTAR", "RENDER", "STUCCO" }),
        new("Sơn, bả", 0, new[] { "SON", "PAINT", "BA MATIT", "PUTTY", "FINISH" }),
    };

    /// <summary>Nhận vật liệu theo tên; null = không nhận ra.</summary>
    public static Kind Match(string name)
    {
        // so khớp theo từ: "Gạch rỗng 2 lỗ" → " GACH RONG 2 LO "; "Masonry-Brick" → " MASONRY BRICK "
        string s = " " + System.Text.RegularExpressions.Regex.Replace(ArchPlanReader.Norm(name ?? ""), "[^A-Z0-9]+", " ").Trim() + " ";
        if (s.Trim().Length == 0) return null;
        foreach (var k in Kinds)
            foreach (var key in k.Keys)
                if (s.Contains(" " + key.Trim() + " ", StringComparison.Ordinal)) return k;
        return null;
    }

    /// <summary>Kết quả tính trọng lượng một loại tường.</summary>
    public sealed class Mass
    {
        /// <summary>Chiều dày tổng, m (làm tròn 5 mm)</summary>
        public double Thickness;
        /// <summary>Trọng lượng 1 m² tường (kN/m²) theo lớp cấu tạo</summary>
        public double PerArea;
        /// <summary>γ tương đương để t·γ + 2·trát·18 = PerArea (dùng trong đề xuất / loại tường của dự án)</summary>
        public double EquivalentUnitWeight;
        /// <summary>Vật liệu lớp chính</summary>
        public string Core = "";
        /// <summary>Nhận được vật liệu mọi lớp (false → giả thiết gạch đặc 18 kN/m³, cần người dùng kiểm tra)</summary>
        public bool Sure;
        public string Note = "";
    }

    /// <summary>Trọng lượng tường: Σ tᵢ·γᵢ theo lớp; không có lớp → chiều dày theo tên loại (vd. "Tường 220") × 18.</summary>
    public static Mass WallMass(RvWall w, ArchImportOptions o)
    {
        var m = new Mass();
        var layers = (w.Layers ?? new List<RvLayer>()).Where(l => l != null && l.Thickness > 1e-4).ToList();
        double t = layers.Sum(l => l.Thickness);
        if (t < 0.03) t = ThicknessFromName(w.TypeName) ?? 0.22;
        double per = 0; bool sure = layers.Count > 0;
        Kind coreKind = null; double coreT = 0;
        foreach (var l in layers)
        {
            var k = Match(l.Material) ?? Match(w.TypeName);
            if (k == null) { sure = false; k = Kinds.First(q => q.Name == "Gạch đặc"); }
            per += l.Thickness * k.Gamma;
            if (l.Thickness > coreT) { coreT = l.Thickness; coreKind = k; }
        }
        if (layers.Count == 0)
        {
            var k = Match(w.TypeName);
            sure = k != null;
            k ??= Kinds.First(q => q.Name == "Gạch đặc");
            per = t * k.Gamma;
            coreKind = k;
            m.Note = "Loại tường không có lớp cấu tạo – chiều dày theo tên loại.";
        }
        m.Thickness = Math.Round(t * 200) / 200;   // 5 mm
        m.PerArea = Math.Round(per, 3);
        m.Core = coreKind?.Name ?? "";
        m.Sure = sure;
        double plaster = 2 * (o?.PlasterThickness ?? 0.015) * 18.0;
        m.EquivalentUnitWeight = m.Thickness > 1e-6 ? Math.Round(Math.Max(0, per - plaster) / m.Thickness, 3) : 0;
        if (m.EquivalentUnitWeight <= 0) m.EquivalentUnitWeight = 0.01;   // vách kính / rất nhẹ: gần như không tải (0 = dùng γ chung)
        return m;
    }

    /// <summary>"Tường 220", "Wall 110mm", "T22" → 0,22 / 0,11 m.</summary>
    public static double? ThicknessFromName(string name)
    {
        var m = System.Text.RegularExpressions.Regex.Match(name ?? "", @"(\d{2,3})\s*(mm)?");
        if (!m.Success) return null;
        double v = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        if (v >= 60 && v <= 600) return v / 1000;
        if (v >= 6 && v <= 60) return v / 100;   // "T22" = 22 cm
        return null;
    }
}
