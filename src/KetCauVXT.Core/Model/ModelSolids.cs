namespace KetCauVXT.Model;

// =====================================================================
// KHỐI 3D DÙNG CHUNG (IFC, OBJ, DAE, kiểm tra va chạm)
// Mỗi đối tượng = một khối lăng trụ: đa giác phẳng (toạ độ địa phương X–Y) kéo theo trục Z địa phương một đoạn Depth.
// Hệ địa phương: gốc Origin, trục X = XDir, trục Z = ZDir (hướng kéo), Y = Z × X.
// Bộ môn Điện nước (Model/Mep) đăng ký khối của mình qua ModelSolids.Providers.
// =====================================================================

/// <summary>Khối lăng trụ của một đối tượng mô hình.</summary>
public sealed class Solid
{
    /// <summary>Mã ổn định: "COT-12", "DAM-5", "SAN-3", "TUONG-2", "CUA-2-1", "LO-2-1", "SN-1", "DD-4"…</summary>
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Discipline Discipline { get; set; } = Discipline.KetCau;
    /// <summary>Nhóm: "Cột", "Dầm", "Sàn", "Sàn nền", "Tường", "Cửa đi", "Cửa sổ", "Lỗ cửa", "Mái", "Đồ đạc", "Ống", "Thiết bị"…</summary>
    public string Category { get; set; } = "";
    /// <summary>Kiểu IFC: IfcColumn, IfcBeam, IfcSlab, IfcWallStandardCase, IfcDoor, IfcWindow, IfcOpeningElement, IfcRoof,
    /// IfcFurnishingElement, IfcFlowSegment, IfcFlowTerminal, IfcFlowFitting, IfcBuildingElementProxy</summary>
    public string IfcType { get; set; } = "IfcBuildingElementProxy";
    public string Story { get; set; } = "";
    public Vec3 Origin { get; set; }
    public Vec3 XDir { get; set; } = Vec3.UnitX;
    public Vec3 ZDir { get; set; } = Vec3.UnitZ;
    /// <summary>Đa giác mặt cắt (toạ độ địa phương X, Y), ngược chiều kim đồng hồ</summary>
    public List<double[]> Profile { get; set; } = new();
    public double Depth { get; set; }
    /// <summary>Khối là lỗ khoét (IfcOpeningElement) của HostId</summary>
    public bool IsVoid { get; set; }
    public string HostId { get; set; } = "";
    /// <summary>Ống/thiết bị cho phép xuyên dầm/tường bằng ống lồng (va chạm loại "khe hở / ống lồng")</summary>
    public bool AllowSleeve { get; set; }
    /// <summary>Màu RGB 0…1 cho OBJ/DAE</summary>
    public double[] Color { get; set; } = { 0.75, 0.75, 0.75 };

    public Vec3 YDir => ZDir.Cross(XDir).Normalize();

    public Vec3 ToWorld(double x, double y, double z) => Origin + XDir * x + YDir * y + ZDir * z;

    /// <summary>Đỉnh đáy (z = 0) và đỉnh nắp (z = Depth) theo thứ tự Profile.</summary>
    public (List<Vec3> Bottom, List<Vec3> Top) Rings()
    {
        var b = Profile.Select(q => ToWorld(q[0], q[1], 0)).ToList();
        var t = Profile.Select(q => ToWorld(q[0], q[1], Depth)).ToList();
        return (b, t);
    }

    /// <summary>Hộp bao trục toạ độ (min, max).</summary>
    public (Vec3 Min, Vec3 Max) Box()
    {
        var (b, t) = Rings();
        var all = b.Concat(t).ToList();
        if (all.Count == 0) return (Origin, Origin);
        return (new Vec3(all.Min(v => v.X), all.Min(v => v.Y), all.Min(v => v.Z)), new Vec3(all.Max(v => v.X), all.Max(v => v.Y), all.Max(v => v.Z)));
    }

    public double Volume => Geo2.Area(Profile) * Depth;

    public static List<double[]> Rect(double x0, double y0, double x1, double y1) =>
        new() { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } };
}

/// <summary>Sinh khối 3D cho toàn mô hình.</summary>
public static class ModelSolids
{
    /// <summary>Nguồn khối bổ sung (vd. bộ môn Điện nước đăng ký ống, thiết bị). Mỗi nguồn trả về các khối của mình.</summary>
    public static readonly List<Func<Project, IEnumerable<Solid>>> Providers = new();

    private static readonly double[] ColConc = { 0.62, 0.62, 0.66 }, ColWall = { 0.93, 0.86, 0.74 }, ColDoor = { 0.55, 0.35, 0.2 },
        ColWin = { 0.55, 0.75, 0.9 }, ColSlab = { 0.7, 0.72, 0.75 }, ColRoof = { 0.6, 0.25, 0.2 }, ColFurn = { 0.8, 0.7, 0.55 };

    /// <summary>Mọi khối (lọc theo bộ môn nếu disciplines != null).</summary>
    public static List<Solid> Build(Project p, IEnumerable<Discipline> disciplines = null, bool includeVoids = true)
    {
        var set = disciplines?.ToHashSet();
        var res = new List<Solid>();
        bool Want(Discipline d) => set == null || set.Contains(d);
        if (Want(Discipline.KetCau)) res.AddRange(Structural(p));
        if (Want(Discipline.KienTruc)) res.AddRange(Architectural(p, includeVoids));
        foreach (var prov in Providers)
        {
            IEnumerable<Solid> extra;
            try { extra = prov(p)?.ToList() ?? new List<Solid>(); } catch { extra = new List<Solid>(); }
            res.AddRange(extra.Where(s => Want(s.Discipline)));
        }
        return res;
    }

    private static (double B, double H) Dims(Project p, string sec)
    {
        var fs = p.FrameSections.FirstOrDefault(s => s.Name == sec);
        if (fs == null) return (0.22, 0.3);
        double b = fs.B > 0 ? fs.B : Math.Max(fs.BfTop, 0.2);
        double h = fs.H > 0 ? fs.H : Math.Max(fs.HStart, 0.3);
        return (b, h);
    }

    public static IEnumerable<Solid> Structural(Project p)
    {
        foreach (var f in p.Frames)
        {
            Node a, b;
            try { a = p.Node(f.NodeI); b = p.Node(f.NodeJ); } catch { continue; }
            var (bw, bh) = Dims(p, f.Section);
            if (f.IsVerticalKind)
            {
                var lo = a.Z <= b.Z ? a : b; var hi = a.Z <= b.Z ? b : a;
                yield return new Solid
                {
                    Id = $"COT-{f.Id}", Name = string.IsNullOrEmpty(f.Label) ? $"Cột {f.Id}" : f.Label, Category = "Cột", IfcType = "IfcColumn",
                    Story = f.Story, Origin = new Vec3(lo.X + f.ColOffsetX, lo.Y + f.ColOffsetY, lo.Z), ZDir = Vec3.UnitZ, XDir = Vec3.UnitX,
                    Profile = Solid.Rect(-bw / 2, -bh / 2, bw / 2, bh / 2), Depth = hi.Z - lo.Z, Color = ColConc
                };
            }
            else
            {
                var d = b.P - a.P; double L = d.Length;
                if (L < 1e-6) continue;
                var z = d * (1 / L);
                var x = Vec3.UnitZ.Cross(z);
                if (x.Length < 1e-6) x = Vec3.UnitX; else x = x.Normalize();
                yield return new Solid
                {
                    Id = $"DAM-{f.Id}", Name = string.IsNullOrEmpty(f.Label) ? $"Dầm {f.Id}" : f.Label, Category = "Dầm", IfcType = "IfcBeam",
                    Story = f.Story, Origin = a.P, ZDir = z, XDir = x,
                    Profile = Solid.Rect(-bw / 2, -bh, bw / 2, 0), Depth = L, Color = ColConc
                };
            }
        }
        foreach (var s in p.Slabs)
        {
            List<Node> ns;
            try { ns = s.Nodes.Select(p.Node).ToList(); } catch { continue; }
            if (ns.Count < 3) continue;
            double t = p.SlabSections.FirstOrDefault(x => x.Name == s.Section)?.Thickness ?? 0.1;
            double z = ns.Average(n => n.Z) + s.LevelOffset;
            var prof = ns.Select(n => new[] { n.X, n.Y }).ToList();
            if (Geo2.SignedArea(prof) < 0) prof.Reverse();
            yield return new Solid
            {
                Id = $"SAN-{s.Id}", Name = string.IsNullOrEmpty(s.Label) ? $"Sàn {s.Id}" : s.Label, Category = "Sàn", IfcType = "IfcSlab", Story = s.Story,
                Origin = new Vec3(0, 0, z - t), Profile = prof, Depth = t, Color = ColSlab
            };
        }
    }

    /// <summary>Chiều dày tường theo kiểu.</summary>
    public static double WallThickness(Project p, WallLine w)
    {
        var t = p.WallTypes.FirstOrDefault(x => x.Name == w.Type);
        return t == null ? 0.22 : Math.Max(0.05, t.TotalThickness);
    }

    public static double StoryZ(Project p, string story) =>
        p.Stories.FirstOrDefault(s => s.Name == story)?.Elevation ?? (story == "NEN" || story == "Base" ? p.GroundElevation : p.BaseElevation);

    public static IEnumerable<Solid> Architectural(Project p, bool includeVoids = true)
    {
        foreach (var w in p.Walls)
        {
            if (w.Chord < 1e-6) continue;
            double t = WallThickness(p, w), H = WallSolid3D.DefaultHeight(p, w), L = w.Chord;
            var x = new Vec3((w.X2 - w.X1) / L, (w.Y2 - w.Y1) / L, 0);
            double z0 = StoryZ(p, w.Story);
            var wall = new Solid
            {
                Id = $"TUONG-{w.Id}", Name = string.IsNullOrEmpty(w.Type) ? $"Tường {w.Id}" : w.Type, Discipline = Discipline.KienTruc, Category = "Tường",
                IfcType = "IfcWallStandardCase", Story = w.Story, Origin = new Vec3(w.X1, w.Y1, z0), XDir = x, ZDir = Vec3.UnitZ,
                Profile = Solid.Rect(0, -t / 2, L, t / 2), Depth = H, Color = ColWall
            };
            yield return wall;
            foreach (var o in w.Openings ?? new List<WallOpening>())
            {
                var ot = WallAssembly.TypeOf(p, o);
                if (ot == null) continue;
                double a = Math.Max(0, o.Offset - ot.Width / 2), b = Math.Min(L, o.Offset + ot.Width / 2);
                double hh = Math.Min(ot.Height, Math.Max(0.05, H - ot.Sill));
                if (includeVoids)
                    yield return new Solid
                    {
                        Id = $"LO-{w.Id}-{o.Id}", Name = "Lỗ " + ot.Name, Discipline = Discipline.KienTruc, Category = "Lỗ cửa", IfcType = "IfcOpeningElement",
                        Story = w.Story, IsVoid = true, HostId = wall.Id, Origin = new Vec3(w.X1, w.Y1, z0 + ot.Sill), XDir = x, ZDir = Vec3.UnitZ,
                        Profile = Solid.Rect(a, -t / 2 - 0.01, b, t / 2 + 0.01), Depth = hh
                    };
                bool door = ot.Kind == WallOpeningKind.Door;
                yield return new Solid
                {
                    Id = $"CUA-{w.Id}-{o.Id}", Name = ot.Name, Discipline = Discipline.KienTruc, Category = door ? "Cửa đi" : "Cửa sổ",
                    IfcType = door ? "IfcDoor" : "IfcWindow", Story = w.Story, HostId = wall.Id,
                    Origin = new Vec3(w.X1, w.Y1, z0 + ot.Sill), XDir = x, ZDir = Vec3.UnitZ,
                    Profile = Solid.Rect(a, -0.025, b, 0.025), Depth = hh, Color = door ? ColDoor : ColWin
                };
            }
        }
        var arch = p.Arch ?? new ArchModel();
        foreach (var g in arch.GroundSlabs)
        {
            if (g.Outline.Count < 3) continue;
            var prof = g.Outline.Select(q => new[] { q[0], q[1] }).ToList();
            if (Geo2.SignedArea(prof) < 0) prof.Reverse();
            yield return new Solid
            {
                Id = $"SN-{g.Id}", Name = g.Label, Discipline = Discipline.KienTruc, Category = "Sàn nền", IfcType = "IfcSlab", Story = "NEN",
                Origin = new Vec3(0, 0, g.Elevation - g.Thickness), Profile = prof, Depth = g.Thickness, Color = ColSlab
            };
        }
        foreach (var r in arch.Roofs)
        {
            if (r.Outline.Count < 3 || r.Shape != RoofShape.Bang) continue;   // mái dốc: Model/ArchDetail.cs
            var prof = r.Outline.Select(q => new[] { q[0], q[1] }).ToList();
            if (Geo2.SignedArea(prof) < 0) prof.Reverse();
            yield return new Solid
            {
                Id = $"MAI-{r.Id}", Name = r.Type, Discipline = Discipline.KienTruc, Category = "Mái", IfcType = "IfcRoof", Story = r.Story,
                Origin = new Vec3(0, 0, StoryZ(p, r.Story) + Math.Max(0, r.RidgeHeight) * 0.5), Profile = prof, Depth = 0.05, Color = ColRoof
            };
        }
        foreach (var c in arch.Ceilings)
        {
            if (c.Outline.Count < 3) continue;
            var prof = c.Outline.Select(q => new[] { q[0], q[1] }).ToList();
            if (Geo2.SignedArea(prof) < 0) prof.Reverse();
            yield return new Solid
            {
                Id = $"TRAN-{c.Id}", Name = c.Type, Discipline = Discipline.KienTruc, Category = "Trần", IfcType = "IfcCovering", Story = c.Story,
                Origin = new Vec3(0, 0, StoryZ(p, c.Story) + c.Height), Profile = prof, Depth = 0.012, Color = new[] { 0.95, 0.95, 0.95 }
            };
        }
        foreach (var f in arch.Furniture)
        {
            double ang = f.Rotation * Math.PI / 180;
            var x = new Vec3(Math.Cos(ang), Math.Sin(ang), 0);
            yield return new Solid
            {
                Id = $"DD-{f.Id}", Name = f.Name, Discipline = f.Discipline, Category = string.IsNullOrEmpty(f.Category) ? "Đồ đạc" : f.Category,
                IfcType = f.Discipline == Discipline.DienNuoc ? ((f.Category ?? "").Contains("Ống") ? "IfcFlowSegment" : "IfcFlowTerminal") : "IfcFurnishingElement",
                AllowSleeve = f.Discipline == Discipline.DienNuoc && (f.Category ?? "").Contains("Ống"), Story = f.Story, Origin = new Vec3(f.X, f.Y, StoryZ(p, f.Story) + f.Elevation), XDir = x, ZDir = Vec3.UnitZ,
                Profile = Solid.Rect(-f.Width / 2, -f.Depth / 2, f.Width / 2, f.Depth / 2), Depth = Math.Max(0.01, f.Height), Color = ColFurn
            };
        }
    }
}
