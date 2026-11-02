using System.Globalization;
using KetCauVXT.Loads;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Arch;

/// <summary>Triển khai kết cấu khung BTCT từ mặt bằng kiến trúc: lưới cột, dầm chính/phụ/công xôn, ô sàn, tiết diện sơ bộ, tải tường và tải sàn theo công năng.
/// <para>Quy ước tầng: mặt bằng tầng k (k ≥ 2) đặt trên cao trình "Tk"; tường tầng k đặt lên dầm cao trình Tk. Tầng 1 (trệt) đặt trên giằng móng
/// (không mô hình hoá). Mái = cao trình đỉnh tầng trên cùng; tum (nếu có) đứng trên mái, có mái tum "MAITUM".</para>
/// <para>Kích thước sơ bộ: dầm chính h = L/12, dầm phụ h = L/14, công xôn h = L/5 (làm tròn 50 mm, h ≥ 300 / 250 mm);
/// sàn t = Lngắn/45 (bản kê) hoặc /35 (bản dầm), ≥ 100 mm; cột A = k·N/Rb, N = Σ q·Atruyền tải (q mặc định 12 kN/m²/sàn),
/// k = 1,2 cột giữa, 1,35 cột biên/góc; tiết diện cột giữ nguyên 2 tầng một lần đổi.</para>
/// <para>Tường xiên / tường cong (không song song trục): lưới trục, dầm trục, ô sàn vẫn trực giao; dưới mỗi tường xiên đặt dầm xiên
/// (dầm cong dưới tường cong) nối tới dầm/cột gần nhất ở hai đầu (kéo dài ≤ 1,0 m hoặc rút vào ≤ 0,6 m), chia đoạn tại giao với dầm trục
/// (nút mới được thêm, dầm trục được chia khi ghi mô hình). Tải tường cong tính theo chiều dài cung. Cột xoay giữ góc xoay theo bản vẽ.</para></summary>
public static class ArchToStructure
{
    private const double Tol = 0.02;

    private sealed class Ln
    {
        public bool AlongX;        // dầm song song trục X (y = C)
        public double C, A, B;     // toạ độ cố định, đoạn [A,B] theo phương dầm
        public BeamRole Role;
        public string Grid = "";
        public string Tag = "";    // "wall", "hole", "split", "edge", "grid", "drawn"
        public double W;           // bề rộng dầm vẽ sẵn (0 = mặc định)
        public double Len => B - A;
    }

    // ==================================================================
    // ĐỀ XUẤT
    // ==================================================================
    /// <summary>Lập phương án kết cấu đề xuất (chưa ghi vào mô hình) – dùng để xem trước và chỉnh sửa.</summary>
    public static StructuralProposal Propose(ArchDrawing d, ArchImportOptions o)
    {
        o ??= new ArchImportOptions();
        var s = new StructuralProposal();
        var plans = d.Floors.Where(f => f.Include && !f.IsRoofPlan && f.FloorNo > 0 && f.Walls.Count > 0).OrderBy(f => f.FloorNo).ToList();
        if (plans.Count == 0) throw new InvalidOperationException("Không có mặt bằng tầng nào được chọn (hoặc mặt bằng không nhận diện được tường).");
        var roofPlan = d.Floors.FirstOrDefault(f => f.Include && f.IsRoofPlan);
        var ground = plans[0];
        if (ground.FloorNo != 1) s.Warnings.Add($"Không có mặt bằng tầng 1 – lấy \"{ground.Title}\" làm tầng dưới cùng.");
        if (plans.Count == 1) s.Warnings.Add("Chỉ có 1 mặt bằng – chỉ tạo được 1 cao trình mái.");
        for (int i = 1; i < plans.Count; i++)
            if (!plans[i].IsTum && plans[i].FloorNo != plans[i - 1].FloorNo + 1)
                s.Warnings.Add($"Thiếu mặt bằng giữa tầng {plans[i - 1].FloorNo} và {plans[i].FloorNo} – tầng thiếu được coi như không có.");

        // ---- lưới trục chung ----
        var gx = new List<(double c, string n)>(); var gy = new List<(double c, string n)>();
        foreach (var p in plans)
            foreach (var g in p.Grids)
            {
                var list = g.Dir == GridDir.X ? gx : gy;
                if (!list.Any(q => Math.Abs(q.c - g.Coord) < 0.05)) list.Add((g.Coord, p == ground ? g.Name : ""));
            }
        gx = gx.OrderBy(q => q.c).ToList(); gy = gy.OrderBy(q => q.c).ToList();
        s.GridX = gx.Select(q => q.c).ToList(); s.GridY = gy.Select(q => q.c).ToList();
        s.GridXNames = Names(gx, GridDir.X); s.GridYNames = Names(gy, GridDir.Y);
        if (s.GridX.Count < 2 || s.GridY.Count < 2) throw new InvalidOperationException("Không đủ trục (cần ≥ 2 trục mỗi phương).");
        foreach (var p in plans.Append(roofPlan).Where(p => p != null)) SnapPlan(p, s);

        // ---- cao trình ----
        double z = 0;
        for (int i = 0; i < plans.Count; i++)
        {
            var below = plans[i]; var above = i + 1 < plans.Count ? plans[i + 1] : null;
            z += below.Height;
            string name = above == null ? (below.IsTum ? "MAITUM" : "MAI") : above.IsTum ? "MAI" : "T" + above.FloorNo.ToString(CultureInfo.InvariantCulture);
            if (s.Levels.Any(l => l.Name == name)) name += "_" + (i + 1);
            s.Levels.Add(new ProposalLevel { Name = name, Elevation = Math.Round(z, 4), StoreyHeight = below.Height, Roof = above == null || above.IsTum, FromPlan = above?.Title ?? below.Title });
            s.StoreyHeights.Add(below.Height);
        }

        // ---- cột: giao trục nằm trong nhà tầng 1 + cột kiến trúc nằm trên 1 trục ----
        var colPts = new List<(double x, double y, string note)>();
        foreach (double x in s.GridX)
            foreach (double y in s.GridY)
                if (ArchPlanReader.PointInPoly(ground.Outline, x, y, 0.2)) colPts.Add((x, y, ""));
        foreach (var c in ground.Columns)
        {
            double sx = s.GridX.OrderBy(x => Math.Abs(x - c.X)).First(), sy = s.GridY.OrderBy(y => Math.Abs(y - c.Y)).First();
            bool onX = Math.Abs(sx - c.X) <= 0.3, onY = Math.Abs(sy - c.Y) <= 0.3;
            double px = onX ? sx : Math.Round(c.X, 3), py = onY ? sy : Math.Round(c.Y, 3);
            if (colPts.Any(q => Math.Abs(q.x - px) < 0.05 && Math.Abs(q.y - py) < 0.05)) continue;
            if (onX || onY) colPts.Add((px, py, "cột kiến trúc"));
            else s.Warnings.Add($"Cột kiến trúc tại ({c.X:0.00}; {c.Y:0.00}) không nằm trên trục nào – bỏ qua, thêm trục nếu cần.");
        }
        if (colPts.Count == 0) throw new InvalidOperationException("Không có giao trục nào nằm trong đường bao nhà tầng 1 – kiểm tra lại tường bao / lưới trục.");
        foreach (var (x, y, note) in colPts)
        {
            var pc = new PropColumn { X = x, Y = y, Note = note };
            // góc xoay / cột tròn theo cột kiến trúc tại vị trí này
            var ac = ground.Columns.Where(c => Math.Abs(c.X - x) <= 0.3 && Math.Abs(c.Y - y) <= 0.3).OrderBy(c => Math.Abs(c.X - x) + Math.Abs(c.Y - y)).FirstOrDefault();
            if (ac != null)
            {
                pc.Angle = ac.Angle; pc.Diameter = ac.Diameter;
                if (Math.Abs(ac.Angle) > 1e-9) pc.Note = (pc.Note.Length > 0 ? pc.Note + ", " : "") + $"cột xoay {ac.Angle.ToString("0.#", CultureInfo.InvariantCulture)}°";
                if (ac.Diameter > 0) pc.Note = (pc.Note.Length > 0 ? pc.Note + ", " : "") + $"cột tròn D{ac.Diameter * 1000:0} quy vuông tương đương";
            }
            int top = -1;
            for (int i = 0; i < s.Levels.Count; i++)
            {
                if (!ArchPlanReader.PointInPoly(plans[i].Outline, x, y, 0.25)) break;
                top = i;
            }
            if (top < 0) continue;
            pc.TopLevel = s.Levels[top].Name;
            s.Columns.Add(pc);
        }

        // ---- từng cao trình ----
        var allLines = new List<List<Ln>>();
        for (int i = 0; i < s.Levels.Count; i++)
        {
            var lv = s.Levels[i];
            var below = plans[i]; var above = i + 1 < plans.Count ? plans[i + 1] : null;
            var cols = s.Columns.Where(c => LevelIndex(s, c.TopLevel) >= i).ToList();
            var lines = BuildLevel(s, lv, below, above, roofPlan, cols, o);
            allLines.Add(lines);
        }

        SizeColumns(s, plans, o);
        CheckSpans(s, o);
        s.Notes.Add("Kích thước sơ bộ: dầm chính h = L/12, dầm phụ h = L/14, công xôn h = L/5; sàn t = Lngắn/45 (bản kê) hoặc Lngắn/35 (bản dầm), ≥ 100 mm; cột A = k·N/Rb với N = " +
                    o.ColumnPreloadPerFloor.ToString("0.#", CultureInfo.InvariantCulture) + " kN/m² × diện tích truyền tải × số sàn. Cần chạy phân tích + thiết kế để kiểm tra lại.");
        s.Notes.Add("Tường tầng 1 đặt trên giằng móng – chưa đưa vào mô hình khung (cần thiết kế giằng/móng băng riêng nếu tường nặng).");
        s.Notes.Add($"Tải tường = (t·{o.WallUnitWeight:0.#} + 2·{o.PlasterThickness * 1000:0}mm trát·18) kN/m² × (H tầng − 0,35 m), trừ lỗ cửa (rộng × 2,0 m); gán vào trường hợp HTHT.");
        return s;
    }

    private static List<string> Names(List<(double c, string n)> g, GridDir dir)
    {
        var names = g.Select(q => q.n).ToList();
        if (names.Any(string.IsNullOrEmpty) || names.Distinct().Count() != names.Count)
            names = g.Select((q, i) => ImportGeometry.AutoName(dir, i)).ToList();
        return names;
    }

    private static int LevelIndex(StructuralProposal s, string name)
    {
        for (int i = 0; i < s.Levels.Count; i++) if (s.Levels[i].Name == name) return i;
        return -1;
    }

    // ------------------------------------------------------------------
    // Mạng dầm – ô sàn của một cao trình
    // ------------------------------------------------------------------
    private static List<Ln> BuildLevel(StructuralProposal s, ProposalLevel lv, ArchFloorPlan below, ArchFloorPlan above, ArchFloorPlan roofPlan,
                                       List<PropColumn> cols, ArchImportOptions o)
    {
        // vùng sàn = trần tầng dưới ∪ sàn tầng trên (gồm ban công)
        var polys = new List<List<(double X, double Y)>> { below.Outline };
        if (above != null)
        {
            polys.Add(above.Outline);
            foreach (var z in above.Zones.Where(z => z.Kind == RoomKind.Balcony))
                polys.Add(new List<(double X, double Y)> { (z.X0, z.Y0), (z.X1, z.Y0), (z.X1, z.Y1), (z.X0, z.Y1) });
        }
        // vùng sàn vẽ sẵn (layer nhóm Sàn) của sàn cao trình này: mặt bằng tầng trên (hoặc mặt bằng mái / tầng dưới khi là mái)
        var slabPlan = above ?? roofPlan ?? below;
        var slabRegs = (slabPlan?.SlabRegions ?? new List<ArchSlabRegion>()).ToList();
        if (above == null && roofPlan != null && roofPlan.SlabRegions.Count == 0) slabRegs = below.SlabRegions.ToList();
        foreach (var r in slabRegs.Where(r => !r.IsOpening && r.Points.Count >= 3)) polys.Add(r.Points.ToList());
        polys = polys.Where(p => p != null && p.Count >= 3).ToList();
        bool Inside(double x, double y, double tol) => polys.Any(p => ArchPlanReader.PointInPoly(p, x, y, tol));
        lv.Outline = UnionOutline(polys);
        double bx0 = polys.SelectMany(p => p).Min(q => q.X), bx1 = polys.SelectMany(p => p).Max(q => q.X);
        double by0 = polys.SelectMany(p => p).Min(q => q.Y), by1 = polys.SelectMany(p => p).Max(q => q.Y);

        // lỗ sàn: cầu thang (đi lên từ tầng dưới), thông tầng (ghi trên tầng trên)
        var holes = new List<ArchZone>();
        if (above != null)
        {
            holes.AddRange(below.Zones.Where(z => z.Kind == RoomKind.Stair));
            holes.AddRange(above.Zones.Where(z => z.Kind == RoomKind.Void));
        }
        foreach (var r in slabRegs.Where(r => r.IsOpening && r.X1 - r.X0 > 0.3 && r.Y1 - r.Y0 > 0.3))
            holes.Add(new ArchZone { Kind = RoomKind.Void, X0 = r.X0, Y0 = r.Y0, X1 = r.X1, Y1 = r.Y1, Note = "lỗ sàn vẽ trên layer sàn " + r.Layer });
        if (slabRegs.Count > 0)
            s.Notes.Add($"{lv.Name}: dùng {slabRegs.Count(r => !r.IsOpening)} vùng sàn và {slabRegs.Count(r => r.IsOpening)} lỗ sàn vẽ sẵn trên layer sàn.");

        var xsBreak = s.GridX.Concat(polys.SelectMany(p => p.Select(q => q.X))).Concat(holes.SelectMany(h => new[] { h.X0, h.X1 })).Append(bx0).Append(bx1);
        var ysBreak = s.GridY.Concat(polys.SelectMany(p => p.Select(q => q.Y))).Concat(holes.SelectMany(h => new[] { h.Y0, h.Y1 })).Append(by0).Append(by1);
        var xb = ImportGeometry.Cluster(xsBreak.Where(x => x >= bx0 - 1e-6 && x <= bx1 + 1e-6), 0.01);
        var yb = ImportGeometry.Cluster(ysBreak.Where(y => y >= by0 - 1e-6 && y <= by1 + 1e-6), 0.01);

        var lines = new List<Ln>();
        // 1. dầm trên trục
        for (int k = 0; k < s.GridX.Count; k++)
            foreach (var (a, b) in Runs(yb, y => Inside(s.GridX[k], y, 0.05)))
                lines.Add(new Ln { AlongX = false, C = s.GridX[k], A = a, B = b, Role = BeamRole.Main, Grid = s.GridXNames[k], Tag = "grid" });
        for (int k = 0; k < s.GridY.Count; k++)
            foreach (var (a, b) in Runs(xb, x => Inside(x, s.GridY[k], 0.05)))
                lines.Add(new Ln { AlongX = true, C = s.GridY[k], A = a, B = b, Role = BeamRole.Main, Grid = s.GridYNames[k], Tag = "grid" });

        // 2. dầm biên theo đường bao (khi không trùng trục)
        foreach (var poly in polys)
            for (int k = 0; k < poly.Count; k++)
            {
                var p1 = poly[k]; var p2 = poly[(k + 1) % poly.Count];
                bool ax = Math.Abs(p1.Y - p2.Y) < 1e-6, ay = Math.Abs(p1.X - p2.X) < 1e-6;
                if (!ax && !ay) continue;
                double mx = (p1.X + p2.X) / 2, my = (p1.Y + p2.Y) / 2;
                // chỉ cạnh nằm trên biên vùng hợp (một phía trong, một phía ngoài)
                bool sideA = ax ? Inside(mx, my + 0.05, 0) : Inside(mx + 0.05, my, 0);
                bool sideB = ax ? Inside(mx, my - 0.05, 0) : Inside(mx - 0.05, my, 0);
                if (sideA == sideB) continue;
                var ln = ax ? new Ln { AlongX = true, C = p1.Y, A = Math.Min(p1.X, p2.X), B = Math.Max(p1.X, p2.X), Role = BeamRole.Edge, Tag = "edge" }
                            : new Ln { AlongX = false, C = p1.X, A = Math.Min(p1.Y, p2.Y), B = Math.Max(p1.Y, p2.Y), Role = BeamRole.Edge, Tag = "edge" };
                AddLine(lines, ln, 0.10);
            }

        // 3. dầm viền lỗ cầu thang / thông tầng (lỗ dạng chữ nhật)
        foreach (var h in holes.Where(h => h.X1 - h.X0 > 0.5 && h.Y1 - h.Y0 > 0.5))
        {
            AddLine(lines, new Ln { AlongX = true, C = h.Y0, A = h.X0, B = h.X1, Role = BeamRole.Secondary, Tag = "hole" }, 0.25);
            AddLine(lines, new Ln { AlongX = true, C = h.Y1, A = h.X0, B = h.X1, Role = BeamRole.Secondary, Tag = "hole" }, 0.25);
            AddLine(lines, new Ln { AlongX = false, C = h.X0, A = h.Y0, B = h.Y1, Role = BeamRole.Secondary, Tag = "hole" }, 0.25);
            AddLine(lines, new Ln { AlongX = false, C = h.X1, A = h.Y0, B = h.Y1, Role = BeamRole.Secondary, Tag = "hole" }, 0.25);
        }
        foreach (var ln in lines.Where(l => l.Tag == "hole").ToList()) ExtendToSupports(lines, ln, 1.0, 0.3);

        // 3b. dầm vẽ sẵn trên bản vẽ (layer nhóm Dầm): mặt bằng tầng dưới (dầm trần, nét đứt) hoặc tầng trên
        var drawnSrc = o.DrawnBeamsAtCeiling ? below : (above ?? below);
        int drawn = 0;
        foreach (var db in (drawnSrc?.Beams ?? new List<ArchBeamLine>()).OrderByDescending(b => b.Length))
        {
            if (!db.AlongX && !db.AlongY) continue;
            bool ax = db.AlongX;
            double c = ax ? db.Ay : db.Ax, a = ax ? Math.Min(db.Ax, db.Bx) : Math.Min(db.Ay, db.By), b = ax ? Math.Max(db.Ax, db.Bx) : Math.Max(db.Ay, db.By);
            var grids = ax ? s.GridY : s.GridX;
            double gNear = grids.OrderBy(g => Math.Abs(g - c)).First();
            if (Math.Abs(gNear - c) <= 0.2) c = gNear; else c = Math.Round(c / 0.05) * 0.05;
            a = Math.Round(a / 0.05) * 0.05; b = Math.Round(b / 0.05) * 0.05;
            if (b - a < 0.5) continue;
            drawn++;
            var same = lines.FirstOrDefault(q => q.AlongX == ax && Math.Abs(q.C - c) <= Math.Max(0.1, db.Width / 2) && Math.Min(q.B, b) - Math.Max(q.A, a) >= 0.8 * (b - a));
            if (same != null) { if (db.Width >= 0.1) same.W = Math.Max(same.W, db.Width); continue; }
            var ln = new Ln { AlongX = ax, C = c, A = a, B = b, Role = BeamRole.Secondary, Tag = "drawn", W = db.Width };
            ExtendToSupports(lines, ln, 1.0, 0.3);
            AddLine(lines, ln, 0.02);
        }
        if (drawn > 0) s.Notes.Add($"{lv.Name}: {drawn} dầm theo dầm vẽ sẵn trên \"{drawnSrc.Title}\" (layer dầm)" + (o.DrawnBeamsReplaceAuto ? " – không tự đặt thêm dầm đỡ tường." : "."));

        // 4. dầm phụ đỡ tường (tường đặt trên cao trình này)
        var wallsOn = WallsOnLevel(above, lv);
        if (o.BeamsUnderWalls && !(drawn > 0 && o.DrawnBeamsReplaceAuto))
            foreach (var w in wallsOn.OrderByDescending(w => w.Length))
            {
                if (w.Length < 1.0 || !w.IsOrthogonal) continue;   // tường xiên/cong: đặt dầm riêng sau khi chia đoạn dầm trục giao
                bool ax = w.AlongX;
                double c = ax ? w.Ay : w.Ax, a = ax ? Math.Min(w.Ax, w.Bx) : Math.Min(w.Ay, w.By), b = ax ? Math.Max(w.Ax, w.Bx) : Math.Max(w.Ay, w.By);
                if (!Inside(ax ? (a + b) / 2 : c, ax ? c : (a + b) / 2, 0.05)) continue;
                // bắt về trục gần (≤ 0,2 m)
                var grids = ax ? s.GridY : s.GridX;
                double gNear = grids.OrderBy(g => Math.Abs(g - c)).First();
                if (Math.Abs(gNear - c) <= 0.2) c = gNear;
                c = Math.Round(c / 0.05) * 0.05;
                if (Covered(lines, ax, c, a, b, Math.Max(0.15, w.Thickness / 2 + 0.05), 0.6)) continue;
                var ln = new Ln { AlongX = ax, C = c, A = a, B = b, Role = BeamRole.Secondary, Tag = "wall" };
                if (!ExtendToSupports(lines, ln, o.MaxBeamSpan, 0.6)) continue;   // không tìm được gối → tải tường về sàn
                AddLine(lines, ln, 0.02);
            }

        // 5. chia ô sàn lớn
        List<(double x0, double y0, double x1, double y1)> panels = null;
        for (int iter = 0; iter < 4; iter++)
        {
            panels = Panels(lines, bx0, by0, bx1, by1, Inside);
            bool added = false;
            foreach (var pn in panels)
            {
                double w = pn.x1 - pn.x0, h = pn.y1 - pn.y0;
                if (Math.Min(w, h) <= o.MaxSlabSpan + 0.05) continue;
                if (holes.Any(z => HoleHit(z, pn))) continue;
                // phương án A: chia cạnh dài (ô gần vuông) – B: chia cạnh ngắn
                double sa = Math.Min(Math.Min(w, h), Math.Max(w, h) / 2), sb = Math.Min(w, h) / 2;
                bool splitLong = sa <= sb + 1e-6;
                bool cutX = splitLong ? w >= h : w < h;   // cắt bằng đường x = const
                if (cutX) AddLine(lines, new Ln { AlongX = false, C = Math.Round((pn.x0 + pn.x1) / 2 / 0.05) * 0.05, A = pn.y0, B = pn.y1, Role = BeamRole.Secondary, Tag = "split" }, 0.02);
                else AddLine(lines, new Ln { AlongX = true, C = Math.Round((pn.y0 + pn.y1) / 2 / 0.05) * 0.05, A = pn.x0, B = pn.x1, Role = BeamRole.Secondary, Tag = "split" }, 0.02);
                added = true;
            }
            if (!added) break;
        }

        // ---- phân đoạn dầm tại nút, phân loại ----
        var colAt = cols.Where(c => c.Include).Select(c => (c.X, c.Y)).ToList();
        bool HasCol(double x, double y) => colAt.Any(c => Math.Abs(c.X - x) < 0.05 && Math.Abs(c.Y - y) < 0.05);
        bool InBelow(double x, double y) => ArchPlanReader.PointInPoly(below.Outline, x, y, 0.15);
        var segs = new List<(Ln line, double a, double b)>();
        foreach (var ln in lines)
        {
            var cuts = new List<double> { ln.A, ln.B };
            foreach (var o2 in lines.Where(q => q.AlongX != ln.AlongX))
                if (o2.C > ln.A + Tol && o2.C < ln.B - Tol && ln.C >= o2.A - Tol && ln.C <= o2.B + Tol) cuts.Add(o2.C);
            foreach (var (cx, cy) in colAt)
            {
                double along = ln.AlongX ? cx : cy, across = ln.AlongX ? cy : cx;
                if (Math.Abs(across - ln.C) < 0.05 && along > ln.A + Tol && along < ln.B - Tol) cuts.Add(along);
            }
            var cl = ImportGeometry.Cluster(cuts, Tol);
            for (int k = 0; k + 1 < cl.Count; k++) if (cl[k + 1] - cl[k] > 0.05) segs.Add((ln, cl[k], cl[k + 1]));
        }
        (double X, double Y) P(Ln l, double t) => l.AlongX ? (t, l.C) : (l.C, t);
        int Degree(double x, double y) => segs.Count(sg =>
        {
            var p1 = P(sg.line, sg.a); var p2 = P(sg.line, sg.b);
            return (Math.Abs(p1.X - x) < Tol && Math.Abs(p1.Y - y) < Tol) || (Math.Abs(p2.X - x) < Tol && Math.Abs(p2.Y - y) < Tol);
        });

        var beams = new List<PropBeam>();
        var drawnWidth = new Dictionary<PropBeam, double>();
        foreach (var (ln, a, b) in segs)
        {
            var p1 = P(ln, a); var p2 = P(ln, b);
            bool out1 = !InBelow(p1.X, p1.Y), out2 = !InBelow(p2.X, p2.Y);
            bool c1 = HasCol(p1.X, p1.Y), c2 = HasCol(p2.X, p2.Y);
            BeamRole role;
            if (out1 ^ out2) role = (out1 ? c2 || Degree(p2.X, p2.Y) > 1 : c1 || Degree(p1.X, p1.Y) > 1) ? BeamRole.Cantilever : BeamRole.Edge;
            else if (out1 && out2) role = BeamRole.Edge;
            else role = BeamRole.Secondary;
            double span = b - a;
            if (role == BeamRole.Secondary && ln.Tag == "grid")
            {
                // đoạn trên trục nằm giữa 2 cột (có thể có dầm phụ gác vào giữa nhịp) → dầm chính, nhịp = khoảng cách 2 cột
                var along = colAt.Where(c => Math.Abs((ln.AlongX ? c.Y : c.X) - ln.C) < 0.05).Select(c => ln.AlongX ? c.X : c.Y).ToList();
                var lo = along.Where(t => t <= a + Tol).DefaultIfEmpty(double.NaN).Max();
                var hi = along.Where(t => t >= b - Tol).DefaultIfEmpty(double.NaN).Min();
                if (!double.IsNaN(lo) && !double.IsNaN(hi)) { role = BeamRole.Main; span = hi - lo; }
            }
            var pb = new PropBeam { Ax = p1.X, Ay = p1.Y, Bx = p2.X, By = p2.Y, Role = role, Grid = ln.Grid, Span = span };
            if (ln.W >= 0.1) drawnWidth[pb] = ln.W;
            if (ln.Tag == "drawn") pb.Note = "Dầm vẽ sẵn trên bản vẽ (layer dầm).";
            bool free1 = !c1 && Degree(p1.X, p1.Y) <= 1, free2 = !c2 && Degree(p2.X, p2.Y) <= 1;
            if (role != BeamRole.Cantilever && (free1 || free2)) pb.Note = (pb.Note.Length > 0 ? pb.Note + " " : "") + "Đầu dầm tự do (không có cột/dầm đỡ) – kiểm tra lại.";
            if (role == BeamRole.Cantilever && pb.Length > o.MaxCantilever + 0.05)
                s.Warnings.Add($"{lv.Name}: dầm công xôn dài {pb.Length:0.00} m > {o.MaxCantilever:0.0} m tại ({p1.X:0.00};{p1.Y:0.00})–({p2.X:0.00};{p2.Y:0.00}).");
            beams.Add(pb);
        }

        // ---- dầm xiên / dầm cong đỡ tường xiên, tường cong ----
        var specialCover = new Dictionary<ArchWall, List<(PropBeam pb, double ov)>>();
        if (o.BeamsUnderWalls && !(drawn > 0 && o.DrawnBeamsReplaceAuto))
            specialCover = AddSpecialWallBeams(s, lv, wallsOn, lines, beams, colAt, Inside);

        // ---- ô sàn ----
        panels = Panels(lines, bx0, by0, bx1, by1, Inside);
        var slabs = new List<PropSlab>();
        double tMax = 0.10;
        foreach (var pn in panels)
        {
            if (holes.Any(z => HoleHit(z, pn)))
            {
                var hz = holes.First(z => HoleHit(z, pn));
                lv.Holes.Add(new ArchZone { Kind = hz.Kind, X0 = pn.x0, Y0 = pn.y0, X1 = pn.x1, Y1 = pn.y1, Note = hz.Kind == RoomKind.Stair ? "Cầu thang" : "Thông tầng" });
                continue;
            }
            double w = pn.x1 - pn.x0, h = pn.y1 - pn.y0, ls = Math.Min(w, h), ll = Math.Max(w, h);
            double t = ll / ls <= 2 ? ls / 45 : ls / 35;
            tMax = Math.Max(tMax, Math.Ceiling(t * 100 - 1e-6) / 100);
            var sl = new PropSlab { X0 = pn.x0, Y0 = pn.y0, X1 = pn.x1, Y1 = pn.y1 };
            AssignRoom(sl, lv, below, above, roofPlan, o);
            slabs.Add(sl);
        }
        foreach (var sl in slabs) sl.Thickness = tMax;
        lv.Slabs = slabs;

        // ---- tải tường, lan can mái, cầu thang ----
        double hWall = (above?.Height ?? 0) - 0.35;
        foreach (var w in wallsOn)
        {
            double unit = w.Thickness * o.WallUnitWeight + 2 * o.PlasterThickness * 18.0;   // kN/m²
            double hw = Math.Max(hWall, 0.5);
            double open = Math.Min(0.7, w.OpeningLength * 2.0 / Math.Max(w.Length * hw, 1e-6));
            double q = unit * hw * (1 - open);                 // kN/m chiều dài tường
            if (!w.IsOrthogonal)
            {
                // tường xiên / cong: tải lên các đoạn dầm xiên/cong đặt dưới tường, phần còn lại quy về ô sàn chứa điểm giữa tường
                double cov = 0;
                if (specialCover.TryGetValue(w, out var pieces))
                    foreach (var (pb, ov) in pieces) { pb.WallLoad += q * ov / Math.Max(pb.AxisLength, 1e-6); cov += ov; }
                double restS = w.Length - cov;
                if (restS > 0.2)
                {
                    var (mx, my) = WallMid(w);
                    var sl = slabs.FirstOrDefault(q2 => mx >= q2.X0 - 0.01 && mx <= q2.X1 + 0.01 && my >= q2.Y0 - 0.01 && my <= q2.Y1 + 0.01);
                    if (sl != null)
                    {
                        double area = (sl.X1 - sl.X0) * (sl.Y1 - sl.Y0);
                        sl.Finish += q * restS / area;
                        sl.WallExtra += q * restS / area;
                        sl.RoomName += $" (+tường {(w.IsCurved ? "cong" : "xiên")} {w.Thickness * 1000:0} dài {restS:0.0} m quy đổi {q * restS / area:0.00} kN/m²)";
                    }
                }
                continue;
            }
            bool ax = w.AlongX;
            double c = ax ? w.Ay : w.Ax, a = ax ? Math.Min(w.Ax, w.Bx) : Math.Min(w.Ay, w.By), b = ax ? Math.Max(w.Ax, w.Bx) : Math.Max(w.Ay, w.By);
            double covered = 0;
            foreach (var pb in beams)
            {
                if (!IsOrthoBeam(pb)) continue;
                bool bx = Math.Abs(pb.Ay - pb.By) < 1e-6;
                if (bx != ax) continue;
                double bc = bx ? pb.Ay : pb.Ax;
                if (Math.Abs(bc - c) > Math.Max(0.15, w.Thickness / 2 + 0.06)) continue;
                double ba = bx ? Math.Min(pb.Ax, pb.Bx) : Math.Min(pb.Ay, pb.By), bb = bx ? Math.Max(pb.Ax, pb.Bx) : Math.Max(pb.Ay, pb.By);
                double ov = Math.Min(b, bb) - Math.Max(a, ba);
                if (ov <= 0.01) continue;
                pb.WallLoad += q * ov / pb.Length;
                covered += ov;
            }
            double rest = (b - a) - covered;
            if (rest > 0.2)
            {
                double mx = ax ? (a + b) / 2 : c, my = ax ? c : (a + b) / 2;
                var sl = slabs.FirstOrDefault(q2 => mx >= q2.X0 - 0.01 && mx <= q2.X1 + 0.01 && my >= q2.Y0 - 0.01 && my <= q2.Y1 + 0.01);
                if (sl != null)
                {
                    double area = (sl.X1 - sl.X0) * (sl.Y1 - sl.Y0);
                    sl.Finish += q * rest / area;
                    sl.WallExtra += q * rest / area;
                    sl.RoomName += $" (+tường {w.Thickness * 1000:0} dài {rest:0.0} m quy đổi {q * rest / area:0.00} kN/m²)";
                }
            }
        }
        if (lv.Roof)
        {
            // tường chắn mái trên dầm biên mái (trừ đoạn có tường tum đứng trên)
            double unit = 0.11 * o.WallUnitWeight + 2 * o.PlasterThickness * 18.0;
            int np = 0;
            foreach (var pb in beams.Where(pb => IsOrthoBeam(pb) && OnOutline(lv.Outline, pb)))
            {
                if (above != null && ArchPlanReader.PointInPoly(above.Outline, (pb.Ax + pb.Bx) / 2, (pb.Ay + pb.By) / 2, 0.05)) continue;
                pb.WallLoad += unit * o.ParapetHeight; pb.Parapet = true; np++;
            }
            if (np > 0) s.Notes.Add($"{lv.Name}: tường chắn mái 110 cao {o.ParapetHeight:0.0} m trên {np} đoạn dầm biên.");
        }
        foreach (var h in lv.Holes.Where(h => h.Kind == RoomKind.Stair))
        {
            double area = (h.X1 - h.X0) * (h.Y1 - h.Y0), per = 2 * ((h.X1 - h.X0) + (h.Y1 - h.Y0));
            var edge = beams.Where(pb => OnRect(h, pb)).ToList();
            double len = edge.Sum(pb => pb.Length);
            if (len < 0.5) continue;
            // bản thang + bậc + hoàn thiện ≈ 6,5 kN/m², hoạt tải 3,0 kN/m² – một nửa vế thang truyền về cao trình này
            foreach (var pb in edge) { pb.OtherDead += 0.5 * area * 6.5 / len; pb.LiveLoad += 0.5 * area * 3.0 / len; }
        }

        SizeBeams(beams, lines, o);
        // bề rộng dầm vẽ sẵn (2 nét) giữ theo bản vẽ
        foreach (var (pb, w) in drawnWidth)
        {
            pb.B = Math.Round(w, 2);
            pb.Section = ImportGeometry.RectName("D", pb.B, pb.H);
        }
        lv.Beams = beams;
        return lines;
    }

    // ------------------------------------------------------------------
    // Dầm xiên / dầm cong dưới tường xiên, tường cong
    // ------------------------------------------------------------------
    /// <summary>Dầm thẳng song song trục X hoặc Y.</summary>
    private static bool IsOrthoBeam(PropBeam b) => Math.Abs(b.Sagitta) <= 1e-6 && (Math.Abs(b.Ay - b.By) < 1e-6 || Math.Abs(b.Ax - b.Bx) < 1e-6);

    /// <summary>Điểm giữa tim tường (tường cong: điểm giữa cung).</summary>
    private static (double X, double Y) WallMid(ArchWall w)
    {
        if (w.IsCurved)
        {
            var arc = CurvedBeam.Arc(new Vec3(w.Ax, w.Ay, 0), new Vec3(w.Bx, w.By, 0), w.Sagitta);
            if (arc != null) { var m = arc.Mid; return (m.X, m.Y); }
        }
        return ((w.Ax + w.Bx) / 2, (w.Ay + w.By) / 2);
    }

    private static double R4(double v) => Math.Round(v, 4);

    /// <summary>Chọn gối ở đầu dầm: giao tại đầu (≤ 0,05 m), hoặc kéo dài ra ngoài ≤ reachOut, hoặc rút vào ≤ reachIn (toạ độ dọc trục dầm).</summary>
    private static double? PickSupport(List<double> cand, double end, int dir, double reachOut, double reachIn)
    {
        var at = cand.Where(c => Math.Abs(c - end) <= 0.05).OrderBy(c => Math.Abs(c - end)).ToList();
        if (at.Count > 0) return at[0];
        var outw = cand.Where(c => dir * (c - end) > 0 && Math.Abs(c - end) <= reachOut).OrderBy(c => Math.Abs(c - end)).ToList();
        if (outw.Count > 0) return outw[0];
        var inw = cand.Where(c => dir * (c - end) < 0 && Math.Abs(c - end) <= reachIn).OrderBy(c => Math.Abs(c - end)).ToList();
        return inw.Count > 0 ? inw[0] : null;
    }

    /// <summary>Đặt dầm xiên dưới tường xiên, dầm cong dưới tường cong (tường đặt trên cao trình này).
    /// Gối: dầm trục giao (Ln), cột, dầm xiên đã đặt; dầm chia đoạn tại giao với dầm trục giao.
    /// Trả về: tường → các đoạn dầm đỡ và chiều dài tường nằm trên từng đoạn (để dồn tải tường).</summary>
    private static Dictionary<ArchWall, List<(PropBeam pb, double ov)>> AddSpecialWallBeams(StructuralProposal s, ProposalLevel lv, List<ArchWall> walls, List<Ln> lines,
                                                                                            List<PropBeam> beams, List<(double X, double Y)> colAt, Func<double, double, double, bool> inside)
    {
        var res = new Dictionary<ArchWall, List<(PropBeam pb, double ov)>>();
        var placed = new List<PropBeam>();
        int nDiag = 0, nCurved = 0;
        const double reachOut = 1.0, reachIn = 0.6;
        foreach (var w in walls.Where(w => !w.IsOrthogonal).OrderByDescending(w => w.Length))
        {
            if (w.Length < 1.0) continue;
            var list = new List<(PropBeam pb, double ov)>();
            if (!w.IsCurved)
            {
                // ---- dầm xiên ----
                double L = w.Chord, ux = (w.Bx - w.Ax) / L, uy = (w.By - w.Ay) / L;
                if (!inside((w.Ax + w.Bx) / 2, (w.Ay + w.By) / 2, 0.05)) continue;
                var cand = new List<double>();
                var cuts = new List<double>();
                foreach (var ln in lines)
                {
                    double sp;
                    if (ln.AlongX)
                    {
                        if (Math.Abs(uy) < 1e-9) continue;
                        sp = (ln.C - w.Ay) / uy;
                        double x = w.Ax + sp * ux;
                        if (x < ln.A - 0.05 || x > ln.B + 0.05) continue;
                    }
                    else
                    {
                        if (Math.Abs(ux) < 1e-9) continue;
                        sp = (ln.C - w.Ax) / ux;
                        double y = w.Ay + sp * uy;
                        if (y < ln.A - 0.05 || y > ln.B + 0.05) continue;
                    }
                    cand.Add(sp); cuts.Add(sp);
                }
                foreach (var (cx, cy) in colAt)
                    if (Math.Abs((cx - w.Ax) * uy - (cy - w.Ay) * ux) < 0.05) cand.Add((cx - w.Ax) * ux + (cy - w.Ay) * uy);
                foreach (var q in placed.Where(q => Math.Abs(q.Sagitta) <= 1e-6))
                {
                    // giao với dầm xiên đã đặt
                    double vx = q.Bx - q.Ax, vy = q.By - q.Ay, den = ux * vy - uy * vx;
                    if (Math.Abs(den) < 1e-9) continue;
                    double sp = ((q.Ax - w.Ax) * vy - (q.Ay - w.Ay) * vx) / den;
                    double tq = ((q.Ax - w.Ax) * uy - (q.Ay - w.Ay) * ux) / den;
                    if (tq < -0.01 || tq > 1.01) continue;
                    cand.Add(sp); cuts.Add(sp);
                }
                var sa = PickSupport(cand, 0, -1, reachOut, reachIn);
                var sb = PickSupport(cand, L, 1, reachOut, reachIn);
                if (sa == null || sb == null || sb.Value - sa.Value < 0.5)
                {
                    s.Warnings.Add($"{lv.Name}: tường xiên ({w.Ax:0.00};{w.Ay:0.00})–({w.Bx:0.00};{w.By:0.00}) không tìm được dầm/cột đỡ ở hai đầu – tải tường quy về sàn (thêm dầm thủ công nếu cần).");
                    continue;
                }
                var pts = ImportGeometry.Cluster(cuts.Where(c => c > sa.Value + 0.05 && c < sb.Value - 0.05).Append(sa.Value).Append(sb.Value), 0.02);
                for (int k = 0; k + 1 < pts.Count; k++)
                {
                    double s0 = pts[k], s1 = pts[k + 1];
                    if (s1 - s0 < 0.05) continue;
                    var pb = new PropBeam
                    {
                        Ax = R4(w.Ax + s0 * ux), Ay = R4(w.Ay + s0 * uy), Bx = R4(w.Ax + s1 * ux), By = R4(w.Ay + s1 * uy),
                        Role = BeamRole.Secondary, Span = sb.Value - sa.Value,
                        Note = $"Dầm xiên {w.AngleDeg.ToString("0.#", CultureInfo.InvariantCulture)}° đỡ tường {w.Thickness * 1000:0}."
                    };
                    beams.Add(pb); placed.Add(pb);
                    double ov = Math.Max(0, Math.Min(s1, L) - Math.Max(s0, 0));
                    if (ov > 1e-6) list.Add((pb, ov));
                }
                nDiag++;
            }
            else
            {
                // ---- dầm cong: giữ đường tròn của tim tường, đầu dầm trượt theo cung tới gối ----
                var arc = CurvedBeam.Arc(new Vec3(w.Ax, w.Ay, 0), new Vec3(w.Bx, w.By, 0), w.Sagitta);
                if (arc == null) continue;
                double R = arc.Radius, sgn = Math.Sign(arc.Sweep), sw = Math.Abs(arc.Sweep), Lw = arc.Length;
                double SOf(double x, double y)
                {
                    double d = (Math.Atan2(y - arc.Cy, x - arc.Cx) - arc.StartAngle) * sgn;
                    double lo = -(2 * Math.PI - sw) / 2;
                    d = (d - lo) % (2 * Math.PI);
                    if (d < 0) d += 2 * Math.PI;
                    return (d + lo) * R;
                }
                var cand = new List<double>();
                var cuts = new List<double>();
                foreach (var ln in lines)
                {
                    // giao đường tròn với đường thẳng y = C (hoặc x = C)
                    double off = ln.AlongX ? ln.C - arc.Cy : ln.C - arc.Cx;
                    if (Math.Abs(off) > R) continue;
                    double h = Math.Sqrt(R * R - off * off);
                    foreach (double sg2 in new[] { -1.0, 1.0 })
                    {
                        double x = ln.AlongX ? arc.Cx + sg2 * h : ln.C, y = ln.AlongX ? ln.C : arc.Cy + sg2 * h;
                        double along = ln.AlongX ? x : y;
                        if (along < ln.A - 0.05 || along > ln.B + 0.05) continue;
                        double sp = SOf(x, y);
                        cand.Add(sp); cuts.Add(sp);
                    }
                }
                foreach (var (cx, cy) in colAt)
                    if (Math.Abs(Math.Sqrt((cx - arc.Cx) * (cx - arc.Cx) + (cy - arc.Cy) * (cy - arc.Cy)) - R) < 0.05) cand.Add(SOf(cx, cy));
                var sa = PickSupport(cand, 0, -1, reachOut, reachIn);
                var sb = PickSupport(cand, Lw, 1, reachOut, reachIn);
                if (sa == null || sb == null || sb.Value - sa.Value < 0.5)
                {
                    s.Warnings.Add($"{lv.Name}: tường cong R={R:0.00} m ({w.Ax:0.00};{w.Ay:0.00})–({w.Bx:0.00};{w.By:0.00}) không tìm được dầm/cột đỡ ở hai đầu – tải tường quy về sàn; "
                                   + "thêm dầm cong thủ công (Dầm cong qua 3 điểm) nếu cần.");
                    continue;
                }
                var pts = ImportGeometry.Cluster(cuts.Where(c => c > sa.Value + 0.05 && c < sb.Value - 0.05).Append(sa.Value).Append(sb.Value), 0.02);
                // mỗi đoạn ≤ 180°
                var all = new List<double>();
                for (int k = 0; k + 1 < pts.Count; k++)
                {
                    int m = Math.Max(1, (int)Math.Ceiling((pts[k + 1] - pts[k]) / R / Math.PI - 1e-9));
                    for (int j = 0; j < m; j++) all.Add(pts[k] + (pts[k + 1] - pts[k]) * j / m);
                }
                all.Add(pts[^1]);
                (double X, double Y) At(double sp) { double a = arc.StartAngle + sgn * sp / R; return (arc.Cx + R * Math.Cos(a), arc.Cy + R * Math.Sin(a)); }
                for (int k = 0; k + 1 < all.Count; k++)
                {
                    double s0 = all[k], s1 = all[k + 1];
                    if (s1 - s0 < 0.05) continue;
                    var p0 = At(s0); var p1 = At(s1); var pm = At((s0 + s1) / 2);
                    double ax0 = R4(p0.X), ay0 = R4(p0.Y), bx0 = R4(p1.X), by0 = R4(p1.Y);
                    double sag = CurvedBeam.SagittaThrough(ax0, ay0, bx0, by0, pm.X, pm.Y);
                    var pb = new PropBeam
                    {
                        Ax = ax0, Ay = ay0, Bx = bx0, By = by0, Sagitta = Math.Round(sag, 4),
                        Role = BeamRole.Secondary, Span = sb.Value - sa.Value,
                        Note = $"Dầm cong R={R:0.00} m đỡ tường cong {w.Thickness * 1000:0}."
                    };
                    beams.Add(pb); placed.Add(pb);
                    double ov = Math.Max(0, Math.Min(s1, Lw) - Math.Max(s0, 0));
                    if (ov > 1e-6) list.Add((pb, ov));
                }
                nCurved++;
            }
            res[w] = list;
        }
        if (nDiag > 0) s.Notes.Add($"{lv.Name}: {nDiag} dầm xiên đỡ tường xiên (nút mới tại giao với dầm trục, dầm trục được chia khi ghi mô hình; ô sàn giữ dạng chữ nhật).");
        if (nCurved > 0) s.Notes.Add($"{lv.Name}: {nCurved} dầm cong đỡ tường cong (dầm cong theo cung tim tường, tải tường theo chiều dài cung).");
        return res;
    }

    private static IEnumerable<(double a, double b)> Runs(List<double> breaks, Func<double, bool> inside)
    {
        double? start = null; double last = 0;
        for (int k = 0; k + 1 < breaks.Count; k++)
        {
            double m = (breaks[k] + breaks[k + 1]) / 2;
            if (breaks[k + 1] - breaks[k] < 1e-6) continue;
            if (inside(m)) { start ??= breaks[k]; last = breaks[k + 1]; }
            else if (start != null) { if (last - start.Value > 0.1) yield return (start.Value, last); start = null; }
        }
        if (start != null && last - start.Value > 0.1) yield return (start.Value, last);
    }

    private static void AddLine(List<Ln> lines, Ln ln, double near)
    {
        if (ln.B - ln.A < 0.1) return;
        if (Covered(lines, ln.AlongX, ln.C, ln.A, ln.B, near, 0.9)) return;
        // gộp với đoạn thẳng hàng chồng/kề
        var same = lines.Where(q => q.AlongX == ln.AlongX && Math.Abs(q.C - ln.C) < Tol && q.A <= ln.B + Tol && ln.A <= q.B + Tol).ToList();
        foreach (var q in same)
        {
            if (q.Role == BeamRole.Main || q.Tag == "grid") continue;
            ln.A = Math.Min(ln.A, q.A); ln.B = Math.Max(ln.B, q.B);
            lines.Remove(q);
        }
        lines.Add(ln);
    }

    /// <summary>Đoạn [a,b] tại toạ độ c đã có dầm song song trong phạm vi near phủ ≥ frac chiều dài chưa.</summary>
    private static bool Covered(List<Ln> lines, bool alongX, double c, double a, double b, double near, double frac)
    {
        double cov = 0;
        var iv = lines.Where(q => q.AlongX == alongX && Math.Abs(q.C - c) <= near)
            .Select(q => (Math.Max(q.A, a), Math.Min(q.B, b))).Where(t => t.Item2 > t.Item1).OrderBy(t => t.Item1).ToList();
        double cur = a;
        foreach (var (x0, x1) in iv) { if (x1 <= cur) continue; cov += x1 - Math.Max(x0, cur); cur = Math.Max(cur, x1); }
        return cov >= frac * (b - a) - 1e-6;
    }

    /// <summary>Kéo dài đầu dầm ra ngoài tới dầm vuông góc gần nhất (≤ reachOut), hoặc rút vào (≤ reachIn).
    /// Trả về false nếu một đầu không có gối.</summary>
    private static bool ExtendToSupports(List<Ln> lines, Ln ln, double reachOut, double reachIn)
    {
        var perp = lines.Where(q => q.AlongX != ln.AlongX && q != ln && ln.C >= q.A - 0.05 && ln.C <= q.B + 0.05).Select(q => q.C).ToList();
        if (perp.Count == 0) return false;
        double? Best(double end, int dir)
        {
            var at = perp.Where(c => Math.Abs(c - end) <= 0.05).OrderBy(c => Math.Abs(c - end)).ToList();
            if (at.Count > 0) return at[0];
            var outw = perp.Where(c => dir * (c - end) > 0 && Math.Abs(c - end) <= reachOut).OrderBy(c => Math.Abs(c - end)).ToList();
            if (outw.Count > 0) return outw[0];
            var inw = perp.Where(c => dir * (c - end) < 0 && Math.Abs(c - end) <= reachIn).OrderBy(c => Math.Abs(c - end)).ToList();
            return inw.Count > 0 ? inw[0] : null;
        }
        var na = Best(ln.A, -1); var nb = Best(ln.B, 1);
        if (na == null || nb == null || nb.Value - na.Value < 0.5) return false;
        ln.A = na.Value; ln.B = nb.Value;
        return true;
    }

    /// <summary>Bám đường bao sàn, vùng ban công / cầu thang về trục gần (≤ 0,25 m – tường đặt tim trục, mép tường lệch nửa chiều dày);
    /// toạ độ còn lại làm tròn 50 mm.</summary>
    private static void SnapPlan(ArchFloorPlan p, StructuralProposal s)
    {
        double SX(double x) { double g = s.GridX.OrderBy(v => Math.Abs(v - x)).First(); return Math.Abs(g - x) <= 0.25 ? g : Math.Round(x / 0.05) * 0.05; }
        double SY(double y) { double g = s.GridY.OrderBy(v => Math.Abs(v - y)).First(); return Math.Abs(g - y) <= 0.25 ? g : Math.Round(y / 0.05) * 0.05; }
        if (p.Outline.Count >= 3)
        {
            var q = p.Outline.Select(v => (SX(v.X), SY(v.Y))).ToList();
            q = ImportGeometry.RemoveCollinear(q, 0.005);
            if (q.Count >= 3 && Math.Abs(ImportGeometry.SignedArea(q)) > 1.0) p.Outline = q;
        }
        foreach (var z in p.Zones)
        {
            bool point = z.X1 - z.X0 < 0.05 && z.Y1 - z.Y0 < 0.05;
            if (point) continue;
            z.X0 = SX(z.X0); z.X1 = SX(z.X1); z.Y0 = SY(z.Y0); z.Y1 = SY(z.Y1);
        }
    }

    private static List<(double x0, double y0, double x1, double y1)> Panels(List<Ln> lines, double bx0, double by0, double bx1, double by1, Func<double, double, double, bool> inside)
    {
        var segs = lines.Select(l => l.AlongX ? new Seg2(l.A, l.C, l.B, l.C) : new Seg2(l.C, l.A, l.C, l.B)).ToList();
        var cells = ImportGeometry.RectCells(segs, bx0, by0, bx1, by1, true, null, "");
        return cells.Where(c => inside((c.x0 + c.x1) / 2, (c.y0 + c.y1) / 2, 0) && c.x1 - c.x0 > 0.3 && c.y1 - c.y0 > 0.3).ToList();
    }

    private static bool HoleHit(ArchZone z, (double x0, double y0, double x1, double y1) pn)
    {
        double cx = (pn.x0 + pn.x1) / 2, cy = (pn.y0 + pn.y1) / 2;
        if (z.X1 - z.X0 < 0.05 && z.Y1 - z.Y0 < 0.05)   // vùng điểm (từ chữ)
            return z.X0 > pn.x0 && z.X0 < pn.x1 && z.Y0 > pn.y0 && z.Y0 < pn.y1;
        return z.Contains(cx, cy, 0.05);
    }

    private static List<ArchWall> WallsOnLevel(ArchFloorPlan above, ProposalLevel lv) =>
        above == null ? new List<ArchWall>() : above.Walls.Where(w => w.Thickness >= 0.08).ToList();

    private static void AssignRoom(PropSlab sl, ProposalLevel lv, ArchFloorPlan below, ArchFloorPlan above, ArchFloorPlan roofPlan, ArchImportOptions o)
    {
        double cx = (sl.X0 + sl.X1) / 2, cy = (sl.Y0 + sl.Y1) / 2;
        bool roofHere = above == null || (above.IsTum && !ArchPlanReader.PointInPoly(above.Outline, cx, cy, 0));
        if (roofHere)
        {
            bool terrace = (roofPlan?.Rooms ?? new List<ArchRoom>()).Concat(above?.Rooms ?? new List<ArchRoom>()).Any(r => r.Kind == RoomKind.RoofTerrace);
            var rt = o.Rooms.FirstOrDefault(r => r.Kind == RoomKind.RoofTerrace);
            sl.Kind = RoomKind.RoofTerrace;
            sl.RoomName = terrace ? "Sân thượng" : "Mái BTCT";
            sl.Finish = o.RoofFinish;
            sl.Live = terrace ? (rt?.Live ?? 1.5) : o.RoofLive;
            return;
        }
        var bal = above.Zones.FirstOrDefault(z => z.Kind == RoomKind.Balcony && z.Contains(cx, cy, 0.05));
        RoomType best = null; string name = "";
        if (bal != null) { best = o.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Balcony); name = "Ban công"; }
        foreach (var r in above.Rooms.Where(r => r.X > sl.X0 && r.X < sl.X1 && r.Y > sl.Y0 && r.Y < sl.Y1 && r.Kind != RoomKind.Void && r.Kind != RoomKind.Stair))
        {
            var rt = o.Rooms.FirstOrDefault(q => q.Kind == r.Kind);
            if (rt != null && (best == null || rt.Live > best.Live || (rt.Live == best.Live && rt.Finish > best.Finish))) { best = rt; name = r.Text; }
        }
        best ??= o.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Living);
        sl.Kind = best?.Kind ?? RoomKind.Living;
        sl.RoomName = string.IsNullOrEmpty(name) ? (best?.Name ?? "Phòng") + " (mặc định)" : name;
        sl.Finish = best?.Finish ?? 1.2;
        sl.Live = best?.Live ?? 1.5;
    }

    private static bool OnOutline(List<(double X, double Y)> poly, PropBeam b)
    {
        if (poly == null || poly.Count < 3) return false;
        double mx = (b.Ax + b.Bx) / 2, my = (b.Ay + b.By) / 2;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var c = poly[(i + 1) % poly.Count];
            var (t, dist) = ImportGeometry.ProjectPoint(new Seg2(a.X, a.Y, c.X, c.Y), mx, my);
            if (t >= -0.001 && t <= 1.001 && dist < 0.03) return true;
        }
        return false;
    }

    private static bool OnRect(ArchZone h, PropBeam b)
    {
        double mx = (b.Ax + b.Bx) / 2, my = (b.Ay + b.By) / 2;
        bool onX = (Math.Abs(my - h.Y0) < 0.03 || Math.Abs(my - h.Y1) < 0.03) && mx > h.X0 - 0.03 && mx < h.X1 + 0.03 && Math.Abs(b.Ay - b.By) < 1e-6;
        bool onY = (Math.Abs(mx - h.X0) < 0.03 || Math.Abs(mx - h.X1) < 0.03) && my > h.Y0 - 0.03 && my < h.Y1 + 0.03 && Math.Abs(b.Ax - b.Bx) < 1e-6;
        return onX || onY;
    }

    /// <summary>Hợp các đa giác trực giao → đường bao (để vẽ / tải lan can mái).</summary>
    private static List<(double X, double Y)> UnionOutline(List<List<(double X, double Y)>> polys)
    {
        var xs = ImportGeometry.Cluster(polys.SelectMany(p => p.Select(q => q.X)), 0.005);
        var ys = ImportGeometry.Cluster(polys.SelectMany(p => p.Select(q => q.Y)), 0.005);
        if (xs.Count < 2 || ys.Count < 2) return new List<(double X, double Y)>();
        var inside = new bool[xs.Count - 1, ys.Count - 1];
        for (int i = 0; i + 1 < xs.Count; i++)
            for (int j = 0; j + 1 < ys.Count; j++)
                inside[i, j] = polys.Any(p => ArchPlanReader.PointInPoly(p, (xs[i] + xs[i + 1]) / 2, (ys[j] + ys[j + 1]) / 2));
        return ArchPlanReader.TraceOutline(xs, ys, inside);
    }

    // ------------------------------------------------------------------
    // Tiết diện sơ bộ
    // ------------------------------------------------------------------
    private static double Ceil50(double m) => Math.Ceiling(m * 20 - 1e-6) / 20;

    private static void SizeBeams(List<PropBeam> beams, List<Ln> lines, ArchImportOptions o)
    {
        foreach (var b in beams)
        {
            double L = b.Span > 0 ? b.Span : b.Length;
            b.H = b.Role switch
            {
                BeamRole.Main => Math.Max(0.30, Ceil50(L / 12)),
                BeamRole.Cantilever => Math.Max(0.30, Ceil50(L / 5)),
                _ => Math.Max(0.25, Ceil50(L / 14)),
            };
            if (b.WallLoad > 6 && b.Role != BeamRole.Cantilever) b.H = Math.Max(b.H, Math.Max(0.30, Ceil50(L / 11)));
        }
        // đồng nhất theo đường dầm (cùng toạ độ, cùng phương, cùng loại chính/phụ)
        foreach (var g in beams.Where(IsOrthoBeam).GroupBy(b => (Math.Abs(b.Ay - b.By) < 1e-6, Math.Round(Math.Abs(b.Ay - b.By) < 1e-6 ? b.Ay : b.Ax, 2), b.Role == BeamRole.Main || b.Role == BeamRole.Edge)))
        {
            double h = g.Max(b => b.Role == BeamRole.Cantilever ? 0 : b.H);
            foreach (var b in g) if (b.Role != BeamRole.Cantilever) b.H = h;
        }
        double secMax = beams.Where(b => b.Role == BeamRole.Secondary).Select(b => b.H).DefaultIfEmpty(0).Max();
        foreach (var b in beams.Where(b => b.Role == BeamRole.Main)) b.H = Math.Max(b.H, secMax);
        foreach (var b in beams)
        {
            b.B = Math.Max(o.BeamWidth, Ceil50(b.H / 3.0) > o.BeamWidth + 0.04 ? Ceil50(b.H / 3.0) : o.BeamWidth);
            b.Section = ImportGeometry.RectName("D", b.B, b.H);
        }
    }

    private static void SizeColumns(StructuralProposal s, List<ArchFloorPlan> plans, ArchImportOptions o)
    {
        var conc = Material.Concrete(o.Concrete);
        var ground = plans[0];
        foreach (var c in s.Columns)
        {
            int top = LevelIndex(s, c.TopLevel);
            var areas = new double[top + 1];
            double spanX = 0, spanY = 0;
            for (int i = 0; i <= top; i++)
            {
                var here = s.Columns.Where(q => q.Include && LevelIndex(s, q.TopLevel) >= i).ToList();
                double Half(bool alongX, int dir)
                {
                    var nb = here.Where(q => alongX ? Math.Abs(q.Y - c.Y) < 0.05 && dir * (q.X - c.X) > 0.05 : Math.Abs(q.X - c.X) < 0.05 && dir * (q.Y - c.Y) > 0.05)
                                 .Select(q => Math.Abs(alongX ? q.X - c.X : q.Y - c.Y)).DefaultIfEmpty(-1).Min();
                    if (nb > 0) return nb / 2;
                    // không có cột kề → tới mép sàn (≤ công xôn)
                    var outline = s.Levels[i].Outline;
                    double ext = 0;
                    for (double t = 0.1; t <= o.MaxCantilever + 1e-6; t += 0.1)
                    {
                        double px = alongX ? c.X + dir * t : c.X, py = alongX ? c.Y : c.Y + dir * t;
                        if (ArchPlanReader.PointInPoly(outline, px, py, 0.01)) ext = t; else break;
                    }
                    return ext;
                }
                double ax = Half(true, -1) + Half(true, 1), ay = Half(false, -1) + Half(false, 1);
                areas[i] = ax * ay;
                if (i == 0) { spanX = ax; spanY = ay; }
            }
            c.TributaryArea = Math.Round(areas[0], 2);
            bool edge = ArchPlanReader.PointInPoly(ground.Outline, c.X, c.Y, 0.3) && !ArchPlanReader.PointInPoly(ShrinkTest(ground.Outline), c.X, c.Y);
            double k = edge ? 1.35 : 1.2;
            var need = new double[top + 1];
            for (int i = 0; i <= top; i++)
            {
                double N = 0;
                for (int j = i; j <= top; j++) N += areas[j] * o.ColumnPreloadPerFloor * (s.Levels[j].Roof ? 0.8 : 1.0);
                need[i] = k * N / conc.Rb;
            }
            // nhóm 2 tầng/lần đổi tiết diện, không tăng lên trên
            double prevB = double.MaxValue, prevH = double.MaxValue;
            bool longX = spanX >= spanY;
            for (int i = 0; i <= top; i += 2)
            {
                double A = need[i];
                double b = Math.Max(o.MinColumnSize, o.BeamWidth);
                double h = Math.Max(b, Ceil50(A / b));
                if (h > 0.6) { b = 0.30; h = Math.Max(b, Ceil50(A / b)); }
                if (h > 0.8) s.Warnings.Add($"Cột ({c.X:0.00};{c.Y:0.00}) cần tiết diện lớn ({b * 1000:0}×{h * 1000:0}) – xem lại lưới cột.");
                h = Math.Max(h, 0.30);   // cột nhà phố tối thiểu 220×300
                b = Math.Min(b, prevB); h = Math.Min(h, prevH);
                prevB = b; prevH = h;
                string sec = ImportGeometry.RectName("C", b, h);
                for (int j = i; j <= Math.Min(top, i + 1); j++) c.SectionByLevel[s.Levels[j].Name] = sec;
                if (i == 0) { c.Bx = longX ? h : b; c.By = longX ? b : h; }
            }
            if (c.Note == "") c.Note = edge ? "cột biên" : "cột giữa";
        }
    }

    /// <summary>Đa giác thu vào 0,3 m (gần đúng: chỉ dùng để phân biệt cột biên/cột giữa bằng phép thử điểm).</summary>
    private static List<(double X, double Y)> ShrinkTest(List<(double X, double Y)> poly)
    {
        if (poly.Count < 3) return poly;
        double cx = poly.Average(p => p.X), cy = poly.Average(p => p.Y);
        return poly.Select(p => (p.X + Math.Sign(cx - p.X) * 0.3, p.Y + Math.Sign(cy - p.Y) * 0.3)).ToList();
    }

    private static void CheckSpans(StructuralProposal s, ArchImportOptions o)
    {
        foreach (var lv in s.Levels)
            foreach (var b in lv.Beams.Where(b => b.Role == BeamRole.Main && b.Length > o.MaxBeamSpan + 0.05))
                s.Warnings.Add($"{lv.Name}: nhịp dầm chính {b.Length:0.00} m > {o.MaxBeamSpan:0.0} m (trục {b.Grid}) – cân nhắc thêm cột.");
    }

    // ==================================================================
    // GHI VÀO MÔ HÌNH
    // ==================================================================
    /// <summary>Ghi phương án vào Project (xoá mô hình cũ): lưới trục, tầng, nút, cột, dầm, sàn, tải tường (HTHT), tải sàn theo phòng, tổ hợp.</summary>
    public static ImportReport Apply(Project p, ArchDrawing d, StructuralProposal s, ArchImportOptions o)
    {
        o ??= new ArchImportOptions();
        var rep = new ImportReport { Source = d?.Source ?? "" };
        if (s == null || s.Levels.Count == 0) throw new InvalidOperationException("Chưa có phương án kết cấu.");

        ImportGeometry.ClearModel(p);
        ImportGeometry.EnsurePatterns(p);
        if (p.Materials.All(m => m.Name != o.Concrete)) p.Materials.Add(Material.Concrete(o.Concrete));
        p.Concrete.ConcreteColumn = p.Concrete.ConcreteBeam = p.Concrete.ConcreteSlab = p.Concrete.ConcreteFooting = o.Concrete;

        // trục
        p.Grids.Clear();
        for (int i = 0; i < s.GridX.Count; i++) p.Grids.Add(new GridLine { Name = s.GridXNames[i], Dir = GridDir.X, Coord = s.GridX[i] });
        for (int i = 0; i < s.GridY.Count; i++) p.Grids.Add(new GridLine { Name = s.GridYNames[i], Dir = GridDir.Y, Coord = s.GridY[i] });
        rep.Grids = p.Grids.Count;

        // tầng
        p.BaseElevation = o.BaseElevation;
        p.Stories.Clear();
        double prev = o.BaseElevation;
        foreach (var lv in s.Levels)
        {
            p.Stories.Add(new Story { Name = lv.Name, Elevation = lv.Elevation, Height = Math.Round(lv.Elevation - prev, 4) });
            prev = lv.Elevation;
        }
        rep.Stories = p.Stories.Count;

        // cột
        var cols = s.Columns.Where(c => c.Include).OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
        for (int k = 0; k < cols.Count; k++)
        {
            var c = cols[k];
            int top = LevelIndex(s, c.TopLevel);
            bool longX = c.Bx >= c.By;
            for (int i = 0; i <= top; i++)
            {
                var lv = s.Levels[i];
                double zb = i == 0 ? o.BaseElevation : s.Levels[i - 1].Elevation;
                string story0 = i == 0 ? "Base" : s.Levels[i - 1].Name;
                var a = p.GetOrAddNode(c.X, c.Y, zb, story0);
                var b = p.GetOrAddNode(c.X, c.Y, lv.Elevation, lv.Name);
                if (i == 0) a.Restraint = o.FixedBase ? Node.Fixed : Node.Pinned;
                string secName = c.SectionByLevel.TryGetValue(lv.Name, out var sn) ? sn : ImportGeometry.RectName("C", Math.Min(c.Bx, c.By), Math.Max(c.Bx, c.By));
                var (bb, hh) = ParseRect(secName);
                string sec = ImportGeometry.EnsureRect(p, "C", bb, hh, o.Concrete);
                // h (cạnh dài) theo phương X khi góc 0°
                if (ImportGeometry.AddFrame(p, a, b, FrameKind.Column, sec, lv.Name, $"C{k + 1}", $"C{k + 1}", (longX ? 0 : 90) + c.Angle) != null) rep.Columns++;
            }
        }

        // dầm, sàn
        int bi = 1, nCurvedBeams = 0;
        foreach (var lv in s.Levels)
        {
            int pk = 1;
            foreach (var pb in lv.Beams.Where(b => b.Include))
            {
                var a = p.GetOrAddNode(pb.Ax, pb.Ay, lv.Elevation, lv.Name);
                var b = p.GetOrAddNode(pb.Bx, pb.By, lv.Elevation, lv.Name);
                var (bb, hh) = ParseRect(pb.Section);
                string sec = ImportGeometry.EnsureRect(p, "D", bb > 0 ? bb : pb.B, hh > 0 ? hh : pb.H, o.Concrete);
                string group = !string.IsNullOrEmpty(pb.Grid) && pb.Role == BeamRole.Main ? $"D{lv.Name}-{pb.Grid}" : $"D{lv.Name}-P{pk++}";
                var f = ImportGeometry.AddFrame(p, a, b, FrameKind.Beam, sec, lv.Name, $"D{bi++}", group);
                if (f == null && Math.Abs(pb.Sagitta) > 1e-6 && a.Id != b.Id)
                {
                    // dầm cong nối cùng 2 nút với một dầm thẳng (vd. ô văng cong giữa 2 cột) – vẫn là thanh riêng
                    f = new FrameElement { Id = p.NextFrameId(), NodeI = a.Id, NodeJ = b.Id, Kind = FrameKind.Beam, Section = sec, Story = lv.Name, Label = $"D{bi - 1}", DesignGroup = group };
                    p.Frames.Add(f);
                }
                if (f == null) continue;
                if (Math.Abs(pb.Sagitta) > 1e-6) { f.CurveMidOffset = Math.Round(pb.Sagitta, 4); nCurvedBeams++; }
                rep.Beams++;
                double dead = pb.OtherDead;   // tải tường: khai báo thành WallLine bên dưới, sinh tải khi phân tích
                if (dead > 1e-6)
                {
                    var fl = FrameLoad.Uniform(f.Id, "HTHT", Math.Round(dead, 3), LoadDir.Gravity, "Cầu thang");
                    fl.Source = "ARCH";
                    p.FrameLoads.Add(fl);
                }
                if (pb.LiveLoad > 1e-6)
                {
                    var fl = FrameLoad.Uniform(f.Id, "HT", Math.Round(pb.LiveLoad, 3), LoadDir.Gravity, "Hoạt tải cầu thang");
                    fl.Source = "ARCH";
                    p.FrameLoads.Add(fl);
                }
            }
            foreach (var h in lv.Holes)
                p.Openings.Add(new FloorOpening
                {
                    Id = p.Openings.Count + 1, Story = lv.Name, Kind = h.Kind == RoomKind.Stair ? OpeningKind.Stair : OpeningKind.Void,
                    X0 = h.X0, Y0 = h.Y0, X1 = h.X1, Y1 = h.Y1, Note = h.Note
                });
            int sk = 1;
            foreach (var sl in lv.Slabs.Where(q => q.Include))
            {
                var n1 = p.GetOrAddNode(sl.X0, sl.Y0, lv.Elevation, lv.Name);
                var n2 = p.GetOrAddNode(sl.X1, sl.Y0, lv.Elevation, lv.Name);
                var n3 = p.GetOrAddNode(sl.X1, sl.Y1, lv.Elevation, lv.Name);
                var n4 = p.GetOrAddNode(sl.X0, sl.Y1, lv.Elevation, lv.Name);
                var slab = new Slab
                {
                    Id = p.NextSlabId(), Label = $"S{lv.Name}-{sk++}", Story = lv.Name,
                    Section = ImportGeometry.EnsureSlabSection(p, sl.Thickness > 0 ? sl.Thickness : 0.1, o.Concrete),
                    Nodes = new List<int> { n1.Id, n2.Id, n3.Id, n4.Id }
                };
                p.Slabs.Add(slab); rep.Slabs++;
                double fin = sl.Finish - sl.WallExtra;
                if (fin > 0) p.AreaLoads.Add(new AreaLoad { Slab = slab.Id, Pattern = "HTHT", Q = Math.Round(fin, 3), Note = "Hoàn thiện – " + sl.RoomName });
                if (sl.Live > 0) p.AreaLoads.Add(new AreaLoad { Slab = slab.Id, Pattern = "HT", Q = sl.Live, Note = "Hoạt tải – " + sl.RoomName });
            }
        }

        // tường: khai báo theo vị trí (sửa được trên mặt bằng), tải sinh khi phân tích
        p.Walls.Clear();
        string TypeFor(double t)
        {
            var wt = p.WallTypes.FirstOrDefault(q => Math.Abs(q.Thickness - t) < 0.006 && q.Finish == WallFinish.Plaster2Sides && q.OpeningRatio == 0 && Math.Abs(q.UnitWeight - o.WallUnitWeight) < 1e-6);
            if (wt != null) return wt.Name;
            string tt = (t * 1000).ToString("0", CultureInfo.InvariantCulture);
            wt = new WallType { Name = $"Tường {tt} trát 2 mặt" + (Math.Abs(o.WallUnitWeight - 18) > 1e-6 ? $" (γ={o.WallUnitWeight:0.#})" : ""), Thickness = Math.Round(t, 3), UnitWeight = o.WallUnitWeight, PlasterThickness = o.PlasterThickness };
            if (p.WallTypes.Any(q => q.Name == wt.Name)) wt.Name += $" {p.WallTypes.Count + 1}";
            p.WallTypes.Add(wt);
            return wt.Name;
        }
        int wid = 1;
        for (int i = 0; i < s.Levels.Count; i++)
        {
            var lv = s.Levels[i];
            var plan = d?.Floors.FirstOrDefault(f => f.Title == lv.FromPlan);
            // mặt bằng phía trên cao trình này: tầng có FromPlan khác mặt bằng tầng dưới (mái trên cùng không có)
            bool isTop = i == s.Levels.Count - 1;
            if (plan != null && !isTop)
                foreach (var w in plan.Walls.Where(w => w.Thickness >= 0.08 && w.Length >= 0.5))
                    p.Walls.Add(new WallLine { Id = wid++, Story = lv.Name, X1 = w.Ax, Y1 = w.Ay, X2 = w.Bx, Y2 = w.By, Sagitta = w.IsCurved ? Math.Round(w.Sagitta, 4) : 0, Type = TypeFor(w.Thickness), OpeningArea = Math.Round(w.OpeningLength * 2.0, 2),
                                             Note = w.IsCurved ? $"Từ bản vẽ kiến trúc – tường cong R={w.Radius:0.00} m" : w.IsDiagonal ? $"Từ bản vẽ kiến trúc – tường xiên {w.AngleDeg:0.#}°" : "Từ bản vẽ kiến trúc" });
            foreach (var pb in lv.Beams.Where(b => b.Include && b.Parapet))
                p.Walls.Add(new WallLine { Id = wid++, Story = lv.Name, X1 = pb.Ax, Y1 = pb.Ay, X2 = pb.Bx, Y2 = pb.By, Type = TypeFor(0.11), Height = o.ParapetHeight, OpeningArea = 0, Note = "Tường chắn mái" });
        }
        rep.Info.Add($"Đã khai báo {p.Walls.Count} đoạn tường (sửa được trên mặt bằng: Xoá / Gán tường / Loại tường)"
                     + (p.Walls.Any(w => w.IsCurved) ? $", gồm {p.Walls.Count(w => w.IsCurved)} tường cong (tải theo chiều dài cung)" : "") + ".");
        if (nCurvedBeams > 0) rep.Info.Add($"Đã tạo {nCurvedBeams} dầm cong đỡ tường cong (độ vồng theo bản vẽ kiến trúc).");
        int rotCols = cols.Count(c => Math.Abs(c.Angle) > 1e-9);
        if (rotCols > 0) rep.Info.Add($"{rotCols} cột xoay theo bản vẽ kiến trúc – góc tiết diện = góc xoay của cột.");

        p.InvalidateIndex();
        rep.SplitCount = ModelBuilder.SplitFramesAtNodes(p);
        // nút treo không thuộc thanh nào (góc ô sàn trùng nút dầm nên không còn)
        ComboGenerator.Generate(p);
        p.InvalidateIndex();
        rep.Nodes = p.Nodes.Count; rep.Frames = p.Frames.Count;
        rep.Info.AddRange(s.Notes);
        rep.Warnings.AddRange(s.Warnings);
        foreach (var e in p.Validate()) rep.Warnings.Add("Kiểm tra mô hình: " + e);
        return rep;
    }

    private static (double b, double h) ParseRect(string name)
    {
        var m = System.Text.RegularExpressions.Regex.Match(name ?? "", @"(\d+(?:\.\d+)?)x(\d+(?:\.\d+)?)");
        if (!m.Success) return (0, 0);
        double b = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) / 100, h = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) / 100;
        return (b, h);
    }
}
