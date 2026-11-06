using System.Globalization;
using System.Text;
using KetCauVXT.Export;
using KetCauVXT.Model;

namespace KetCauVXT.Import;

// =====================================================================
// NHẬP IFC (IFC2X3 / IFC4, mức cơ bản) – phương án thay add-in Revit: Revit → File → Export → IFC.
// Đọc: IfcBuildingStorey (cao độ), IfcColumn, IfcBeam, IfcSlab, IfcWall/IfcWallStandardCase, IfcDoor, IfcWindow, IfcSpace
// có hình học IfcExtrudedAreaSolid (IfcRectangleProfileDef / IfcArbitraryClosedProfileDef + IfcPolyline),
// đặt bằng chuỗi IfcLocalPlacement. Đơn vị: theo IfcSIUnit LENGTHUNIT (.MILLI. → ×0,001).
// =====================================================================
public sealed class IfcEntity
{
    public int Id;
    public string Type = "";
    public List<object> Args = new();
}

public sealed class IfcImportReport
{
    public int Storeys, Columns, Beams, Slabs, Walls, Doors, Windows, Spaces;
    public List<string> Warnings { get; } = new();
    public override string ToString() =>
        $"IFC: {Storeys} cao độ, {Columns} cột, {Beams} dầm, {Slabs} sàn, {Walls} tường, {Doors} cửa đi, {Windows} cửa sổ, {Spaces} phòng" +
        (Warnings.Count > 0 ? $" ({Warnings.Count} cảnh báo)" : "");
}

public static class IfcImporter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Phân tích phần DATA của file STEP thành các thực thể.</summary>
    public static Dictionary<int, IfcEntity> Parse(string text)
    {
        var res = new Dictionary<int, IfcEntity>();
        int di = text.IndexOf("DATA;", StringComparison.Ordinal);
        if (di < 0) return res;
        // tách câu theo ';' ngoài chuỗi
        var sb = new StringBuilder();
        bool inStr = false;
        for (int i = di + 5; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'') inStr = !inStr;
            if (c == ';' && !inStr)
            {
                var stmt = sb.ToString().Trim(); sb.Clear();
                if (stmt.StartsWith("ENDSEC")) break;
                var e = ParseStmt(stmt);
                if (e != null) res[e.Id] = e;
            }
            else sb.Append(c);
        }
        return res;
    }

    private static IfcEntity ParseStmt(string s)
    {
        if (!s.StartsWith("#")) return null;
        int eq = s.IndexOf('=');
        if (eq < 0) return null;
        if (!int.TryParse(s.AsSpan(1, eq - 1).Trim(), out int id)) return null;
        string rest = s[(eq + 1)..].Trim();
        int par = rest.IndexOf('(');
        if (par < 0) return null;
        var e = new IfcEntity { Id = id, Type = rest[..par].Trim().ToUpperInvariant() };
        int pos = par;
        e.Args = ParseList(rest, ref pos);
        return e;
    }

    // list: '(' item (',' item)* ')'; item: list | 'str' | #ref | number | .ENUM. | $ | * | TYPE(args)
    private static List<object> ParseList(string s, ref int pos)
    {
        var res = new List<object>();
        pos++; // '('
        while (pos < s.Length)
        {
            SkipWs(s, ref pos);
            if (pos >= s.Length) break;
            char c = s[pos];
            if (c == ')') { pos++; break; }
            if (c == ',') { pos++; continue; }
            if (c == '(') { res.Add(ParseList(s, ref pos)); continue; }
            if (c == '\'')
            {
                int st = pos++;
                while (pos < s.Length) { if (s[pos] == '\'') { if (pos + 1 < s.Length && s[pos + 1] == '\'') { pos += 2; continue; } pos++; break; } pos++; }
                res.Add(s[st..pos]); continue;
            }
            int start = pos;
            while (pos < s.Length && s[pos] != ',' && s[pos] != ')' && s[pos] != '(') pos++;
            string tok = s[start..pos].Trim();
            if (pos < s.Length && s[pos] == '(' && tok.Length > 0 && char.IsLetter(tok[0]))
            {
                var inner = ParseList(s, ref pos);   // typed value: IFCLABEL('x') → lấy giá trị trong
                res.Add(inner.Count == 1 ? inner[0] : inner); continue;
            }
            res.Add(tok);
        }
        return res;
    }

    private static void SkipWs(string s, ref int pos) { while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++; }

    private static int RefOf(object o) => o is string s && s.StartsWith("#") && int.TryParse(s[1..], out int r) ? r : -1;
    private static double Num(object o) => o is string s && double.TryParse(s, NumberStyles.Float, Inv, out double v) ? v : 0;
    private static List<object> L(object o) => o as List<object> ?? new List<object>();
    private static string StrOf(object o) => o is string s && s.StartsWith("'") ? IfcWriter.Unstr(s) : "";

    private sealed class Frame3 { public Vec3 O; public Vec3 X = Vec3.UnitX; public Vec3 Y = Vec3.UnitY; public Vec3 Z = Vec3.UnitZ;
        public Vec3 Apply(Vec3 p) => O + X * p.X + Y * p.Y + Z * p.Z;
        public Vec3 Dir(Vec3 d) => X * d.X + Y * d.Y + Z * d.Z; }

    private static Vec3 Point(Dictionary<int, IfcEntity> m, int id)
    {
        if (!m.TryGetValue(id, out var e)) return default;
        var c = L(e.Args.FirstOrDefault()).Select(Num).ToList();
        return new Vec3(c.ElementAtOrDefault(0), c.ElementAtOrDefault(1), c.ElementAtOrDefault(2));
    }

    private static Frame3 Axis(Dictionary<int, IfcEntity> m, int id)
    {
        var f = new Frame3();
        if (!m.TryGetValue(id, out var e)) return f;
        f.O = Point(m, RefOf(e.Args.ElementAtOrDefault(0)));
        if (e.Type == "IFCAXIS2PLACEMENT2D")
        {
            int rx2 = RefOf(e.Args.ElementAtOrDefault(1));
            if (rx2 > 0) { var d = Point(m, rx2).Normalize(); f.X = d; f.Y = Vec3.UnitZ.Cross(d).Normalize(); }
            return f;
        }
        int rz = RefOf(e.Args.ElementAtOrDefault(1)), rx = RefOf(e.Args.ElementAtOrDefault(2));
        if (rz > 0) f.Z = Point(m, rz).Normalize();
        if (rx > 0) f.X = Point(m, rx).Normalize();
        else f.X = Math.Abs(f.Z.Z) > 0.9 ? Vec3.UnitX : Vec3.UnitZ.Cross(f.Z).Normalize();
        // trực giao hoá X theo Z
        f.X = (f.X - f.Z * f.X.Dot(f.Z)).Normalize();
        f.Y = f.Z.Cross(f.X).Normalize();
        return f;
    }

    private static Frame3 Compose(Frame3 parent, Frame3 child) => new()
    {
        O = parent.Apply(child.O), X = parent.Dir(child.X), Y = parent.Dir(child.Y), Z = parent.Dir(child.Z)
    };

    private static Frame3 Placement(Dictionary<int, IfcEntity> m, int id, int depth = 0)
    {
        if (depth > 50 || !m.TryGetValue(id, out var e) || e.Type != "IFCLOCALPLACEMENT") return new Frame3();
        var parent = RefOf(e.Args.ElementAtOrDefault(0)) is int pr && pr > 0 ? Placement(m, pr, depth + 1) : new Frame3();
        var local = Axis(m, RefOf(e.Args.ElementAtOrDefault(1)));
        return Compose(parent, local);
    }

    /// <summary>Đọc khối lăng trụ đầu tiên của một sản phẩm (đơn vị theo file, chưa nhân hệ số đơn vị).</summary>
    private static Solid ReadSolid(Dictionary<int, IfcEntity> m, IfcEntity prod)
    {
        int plc = RefOf(prod.Args.ElementAtOrDefault(5)), rep = RefOf(prod.Args.ElementAtOrDefault(6));
        if (!m.TryGetValue(rep, out var pds)) return null;
        foreach (var r in L(pds.Args.ElementAtOrDefault(2)).Select(RefOf))
        {
            if (!m.TryGetValue(r, out var sr)) continue;
            foreach (var it in L(sr.Args.ElementAtOrDefault(3)).Select(RefOf))
            {
                if (!m.TryGetValue(it, out var ex) || ex.Type != "IFCEXTRUDEDAREASOLID") continue;
                var prof = Profile(m, RefOf(ex.Args[0]));
                if (prof == null || prof.Count < 3) continue;
                var pos = Axis(m, RefOf(ex.Args.ElementAtOrDefault(1)));
                var dir = Point(m, RefOf(ex.Args.ElementAtOrDefault(2))).Normalize();
                double depth = Num(ex.Args.ElementAtOrDefault(3));
                var world = Compose(Placement(m, plc), pos);
                // hướng kéo trong hệ của khối; nếu không trùng Z thì quy đổi bằng cách lấy ZDir = hướng kéo thực
                var z = world.Dir(dir).Normalize();
                return new Solid { Origin = world.O, XDir = world.X, ZDir = z, Profile = prof, Depth = depth };
            }
        }
        return null;
    }

    private static List<double[]> Profile(Dictionary<int, IfcEntity> m, int id)
    {
        if (!m.TryGetValue(id, out var e)) return null;
        if (e.Type == "IFCRECTANGLEPROFILEDEF")
        {
            var ax = Axis(m, RefOf(e.Args.ElementAtOrDefault(2)));
            double x = Num(e.Args.ElementAtOrDefault(3)) / 2, y = Num(e.Args.ElementAtOrDefault(4)) / 2;
            return new[] { (-x, -y), (x, -y), (x, y), (-x, y) }
                .Select(q => { var v = ax.Apply(new Vec3(q.Item1, q.Item2, 0)); return new[] { v.X, v.Y }; }).ToList();
        }
        if (e.Type is "IFCARBITRARYCLOSEDPROFILEDEF")
        {
            if (!m.TryGetValue(RefOf(e.Args.ElementAtOrDefault(2)), out var pl) || pl.Type != "IFCPOLYLINE") return null;
            var pts = L(pl.Args[0]).Select(RefOf).Select(i => Point(m, i)).Select(v => new[] { v.X, v.Y }).ToList();
            if (pts.Count > 1 && Math.Abs(pts[0][0] - pts[^1][0]) < 1e-9 && Math.Abs(pts[0][1] - pts[^1][1]) < 1e-9) pts.RemoveAt(pts.Count - 1);
            return pts;
        }
        return null;
    }

    /// <summary>Hệ số đơn vị chiều dài (m / đơn vị file).</summary>
    public static double LengthUnit(Dictionary<int, IfcEntity> m)
    {
        foreach (var e in m.Values.Where(e => e.Type == "IFCSIUNIT"))
            if (e.Args.ElementAtOrDefault(1) as string == ".LENGTHUNIT.")
                return (e.Args.ElementAtOrDefault(2) as string) switch { ".MILLI." => 0.001, ".CENTI." => 0.01, _ => 1.0 };
        return 1.0;
    }

    /// <summary>Nhập IFC vào dự án (thêm vào mô hình hiện có; tầng theo IfcBuildingStorey).</summary>
    public static IfcImportReport Import(Project p, string text)
    {
        var rep = new IfcImportReport();
        var m = Parse(text);
        double k = LengthUnit(m);
        Vec3 S(Vec3 v) => v * k;
        // cao độ
        var storeys = m.Values.Where(e => e.Type == "IFCBUILDINGSTOREY")
            .Select(e => (Name: StrOf(e.Args.ElementAtOrDefault(2)), Z: Num(e.Args.ElementAtOrDefault(9)) * k)).OrderBy(s => s.Z).ToList();
        foreach (var s in storeys)
        {
            if (s.Name.Contains("móng", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Nền", StringComparison.OrdinalIgnoreCase) || Math.Abs(s.Z) < 1e-6) continue;
            if (p.Stories.Any(x => Math.Abs(x.Elevation - s.Z) < 0.05)) continue;
            string name = s.Name.Contains("mái", StringComparison.OrdinalIgnoreCase) ? "MAI" : $"T{p.Stories.Count + 2}";
            if (p.Stories.Any(x => x.Name == name)) name += "_" + p.Stories.Count;
            p.Stories.Add(new Story { Name = name, Elevation = Math.Round(s.Z, 4), Kind = name.StartsWith("MAI") ? "Mai" : "San" });
            rep.Storeys++;
        }
        var ordered = p.Stories.OrderBy(s => s.Elevation).ToList();
        for (int i = 0; i < ordered.Count; i++) ordered[i].Height = Math.Round(ordered[i].Elevation - (i == 0 ? p.BaseElevation : ordered[i - 1].Elevation), 4);
        string StoryAt(double z) => p.Stories.OrderBy(s => Math.Abs(s.Elevation - z)).FirstOrDefault()?.Name ?? "";
        string StoryBelowOrAt(double z) => p.Stories.Where(s => s.Elevation <= z + 0.05).OrderByDescending(s => s.Elevation).FirstOrDefault()?.Name ?? "NEN";

        var walls = new List<(IfcEntity E, WallLine W)>();
        foreach (var e in m.Values.OrderBy(e => e.Id))
        {
            string t = e.Type;
            if (t is not ("IFCCOLUMN" or "IFCBEAM" or "IFCSLAB" or "IFCWALL" or "IFCWALLSTANDARDCASE" or "IFCSPACE")) continue;
            var s = ReadSolid(m, e);
            if (s == null) { rep.Warnings.Add($"#{e.Id} {t}: không có IfcExtrudedAreaSolid – bỏ qua"); continue; }
            s.Origin = S(s.Origin); s.Depth *= k; s.Profile = s.Profile.Select(q => new[] { q[0] * k, q[1] * k }).ToList();
            var (bmin, bmax) = s.Box();
            string name = StrOf(e.Args.ElementAtOrDefault(2));
            switch (t)
            {
                case "IFCCOLUMN":
                {
                    double bx = s.Profile.Max(q => q[0]) - s.Profile.Min(q => q[0]), by = s.Profile.Max(q => q[1]) - s.Profile.Min(q => q[1]);
                    string sec = ImportGeometry.EnsureRect(p, "C", Math.Round(bx, 3), Math.Round(by, 3), "B25");
                    double cx = (bmin.X + bmax.X) / 2, cy = (bmin.Y + bmax.Y) / 2;
                    var a = p.GetOrAddNode(cx, cy, bmin.Z, StoryAt(bmin.Z));
                    var b = p.GetOrAddNode(cx, cy, bmax.Z, StoryAt(bmax.Z));
                    if (Math.Abs(bmin.Z - p.BaseElevation) < 0.05) a.Restraint = Node.Fixed;
                    p.Frames.Add(new FrameElement { Id = p.NextFrameId(), NodeI = a.Id, NodeJ = b.Id, Kind = FrameKind.Column, Section = sec, Story = StoryAt(bmax.Z), Label = string.IsNullOrEmpty(name) ? "" : name });
                    rep.Columns++; break;
                }
                case "IFCBEAM":
                {
                    var p0 = s.Origin; var p1 = s.Origin + s.ZDir * s.Depth;
                    double bw = s.Profile.Max(q => q[0]) - s.Profile.Min(q => q[0]), bh = s.Profile.Max(q => q[1]) - s.Profile.Min(q => q[1]);
                    string sec = ImportGeometry.EnsureRect(p, "D", Math.Round(bw, 3), Math.Round(bh, 3), "B25");
                    double z = Math.Max(p0.Z, p1.Z);
                    string st = StoryAt(z);
                    double zs = p.Stories.FirstOrDefault(x => x.Name == st)?.Elevation ?? z;
                    var a = p.GetOrAddNode(p0.X, p0.Y, zs, st); var b = p.GetOrAddNode(p1.X, p1.Y, zs, st);
                    if (a.Id != b.Id) { p.Frames.Add(new FrameElement { Id = p.NextFrameId(), NodeI = a.Id, NodeJ = b.Id, Kind = FrameKind.Beam, Section = sec, Story = st, Label = name }); rep.Beams++; }
                    break;
                }
                case "IFCSLAB":
                {
                    double top = bmax.Z, th = Math.Round(bmax.Z - bmin.Z, 3);
                    var world = s.Profile.Select(q => s.ToWorld(q[0], q[1], 0)).ToList();
                    if (Math.Abs(top - p.GroundElevation) < 0.15 || p.Stories.All(x => Math.Abs(x.Elevation - top) > 0.15))
                    {
                        p.Arch ??= new ArchModel();
                        p.Arch.GroundSlabs.Add(new GroundSlab { Id = p.Arch.GroundSlabs.Count + 1, Label = string.IsNullOrEmpty(name) ? $"SN{p.Arch.GroundSlabs.Count + 1}" : name, Thickness = th, Elevation = top, Outline = world.Select(v => new[] { v.X, v.Y }).ToList() });
                    }
                    else
                    {
                        string st = StoryAt(top);
                        double zs = p.Stories.First(x => x.Name == st).Elevation;
                        string secName = $"S{Math.Round(th * 1000):0}";
                        if (p.SlabSections.All(x => x.Name != secName)) p.SlabSections.Add(new SlabSection { Name = secName, Thickness = th });
                        var ids = world.Select(v => p.GetOrAddNode(v.X, v.Y, zs, st).Id).ToList();
                        p.Slabs.Add(new Slab { Id = p.NextSlabId(), Label = name, Story = st, Section = secName, Nodes = ids });
                    }
                    rep.Slabs++; break;
                }
                case "IFCWALL":
                case "IFCWALLSTANDARDCASE":
                {
                    // tường: trục dài nhất của mặt cắt theo phương ngang
                    var world = s.Profile.Select(q => s.ToWorld(q[0], q[1], 0)).ToList();
                    double x0 = s.Profile.Min(q => q[0]), x1 = s.Profile.Max(q => q[0]), y0 = s.Profile.Min(q => q[1]), y1 = s.Profile.Max(q => q[1]);
                    bool alongX = x1 - x0 >= y1 - y0;
                    double th = alongX ? y1 - y0 : x1 - x0;
                    var a = alongX ? s.ToWorld(x0, (y0 + y1) / 2, 0) : s.ToWorld((x0 + x1) / 2, y0, 0);
                    var b = alongX ? s.ToWorld(x1, (y0 + y1) / 2, 0) : s.ToWorld((x0 + x1) / 2, y1, 0);
                    string typeName = WallTypeFor(p, th);
                    var wl = new WallLine { Id = p.Walls.Count == 0 ? 1 : p.Walls.Max(x => x.Id) + 1, Story = StoryBelowOrAt(bmin.Z), X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Type = typeName, Height = Math.Round(bmax.Z - bmin.Z, 3), Note = name };
                    p.Walls.Add(wl); walls.Add((e, wl)); rep.Walls++; break;
                }
                case "IFCSPACE":
                {
                    p.Arch ??= new ArchModel();
                    var world = s.Profile.Select(q => s.ToWorld(q[0], q[1], 0)).ToList();
                    string longName = StrOf(e.Args.ElementAtOrDefault(7));
                    p.Arch.Rooms.Add(new Room { Id = p.Arch.Rooms.Count + 1, Story = StoryBelowOrAt(bmin.Z), Name = string.IsNullOrEmpty(longName) ? (string.IsNullOrEmpty(name) ? "Phòng" : name) : longName, Number = name, Outline = world.Select(v => new[] { v.X, v.Y }).ToList() });
                    rep.Spaces++; break;
                }
            }
        }
        // cửa đi / cửa sổ → gắn vào tường gần nhất
        foreach (var e in m.Values.Where(e => e.Type is "IFCDOOR" or "IFCWINDOW").OrderBy(e => e.Id))
        {
            var s = ReadSolid(m, e);
            if (s == null) continue;
            s.Origin = S(s.Origin); s.Depth *= k; s.Profile = s.Profile.Select(q => new[] { q[0] * k, q[1] * k }).ToList();
            var (bmin, bmax) = s.Box();
            double cx = (bmin.X + bmax.X) / 2, cy = (bmin.Y + bmax.Y) / 2;
            WallLine best = null; double bd = 1e9, bt = 0;
            foreach (var (_, w) in walls)
            {
                double t = WallAssembly.ProjectPoint(w, cx, cy, out double d);
                if (d < bd) { bd = d; best = w; bt = t; }
            }
            if (best == null || bd > 0.5) { rep.Warnings.Add($"#{e.Id} {e.Type}: không tìm thấy tường chủ"); continue; }
            bool door = e.Type == "IFCDOOR";
            double width = Num(e.Args.ElementAtOrDefault(9)) * k, height = Num(e.Args.ElementAtOrDefault(8)) * k;
            if (width <= 0) width = Math.Max(bmax.X - bmin.X, bmax.Y - bmin.Y);
            if (height <= 0) height = bmax.Z - bmin.Z;
            double sill = Math.Max(0, bmin.Z - ModelSolids.StoryZ(p, best.Story));
            string tname = OpeningTypeFor(p, door, width, height, sill);
            var r = WallAssembly.PlaceOpening(p, best, tname, bt);
            if (r.Ok) { if (door) rep.Doors++; else rep.Windows++; }
        }
        p.InvalidateIndex();
        return rep;
    }

    private static string WallTypeFor(Project p, double t)
    {
        var best = p.WallTypes.OrderBy(w => Math.Abs(w.TotalThickness - t)).FirstOrDefault();
        return best?.Name ?? "";
    }

    private static string OpeningTypeFor(Project p, bool door, double w, double h, double sill)
    {
        var kind = door ? WallOpeningKind.Door : WallOpeningKind.Window;
        var ex = p.OpeningTypes.FirstOrDefault(o => o.Kind == kind && Math.Abs(o.Width - w) < 0.02 && Math.Abs(o.Height - h) < 0.02 && Math.Abs(o.Sill - sill) < 0.05);
        if (ex != null) return ex.Name;
        string name = $"{(door ? "D" : "S")}{p.OpeningTypes.Count(o => o.Kind == kind) + 1} {Math.Round(w * 1000)}x{Math.Round(h * 1000)}";
        p.OpeningTypes.Add(new OpeningType { Name = name, Kind = kind, Width = Math.Round(w, 3), Height = Math.Round(h, 3), Sill = door ? 0 : Math.Round(sill, 3), Operation = door ? "1 cánh" : "2 cánh" });
        return name;
    }
}
