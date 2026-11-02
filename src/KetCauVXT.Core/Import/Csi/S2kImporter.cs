using KetCauVXT.Model;

namespace KetCauVXT.Import.Csi;

/// <summary>Nhập mô hình từ file văn bản SAP2000 (.s2k / .$2k, dạng bảng "TABLE:" Khoá=Giá trị):
/// JOINT COORDINATES, CONNECTIVITY - FRAME, FRAME SECTION PROPERTIES 01 - GENERAL, FRAME SECTION ASSIGNMENTS, FRAME LOCAL AXES,
/// FRAME RELEASE ASSIGNMENTS, MATERIAL PROPERTIES, CONNECTIVITY - AREA, AREA SECTION ASSIGNMENTS/PROPERTIES, JOINT RESTRAINT ASSIGNMENTS,
/// LOAD PATTERN DEFINITIONS, COMBINATION DEFINITIONS, FRAME LOADS - DISTRIBUTED/POINT, JOINT LOADS - FORCE, AREA LOADS - UNIFORM, GRID LINES.
/// Đơn vị theo PROGRAM CONTROL CurrUnits → kN, m. Tầng suy ra từ cao độ các dầm/đỉnh cột. Tên Frame/Joint lưu vào ExternalName.</summary>
public static class S2kImporter
{
    public static Project ImportNew(string path, out ImportReport rep)
    {
        var p = CsiModelBuilder.NewProject(Path.GetFileNameWithoutExtension(path));
        rep = Import(p, path);
        return p;
    }

    public static ImportReport Import(Project p, string path) =>
        Import(p, ExternalTableReader.ReadKeyValue(ExternalTableReader.ReadLines(path), path), Path.GetFileName(path));

    public static ImportReport Import(Project p, List<ExtTable> tables, string source = "s2k")
    {
        var rep = new ImportReport { Source = "SAP2000 " + source };
        ImportGeometry.ClearModel(p);
        p.Grids.Clear(); p.Stories.Clear(); p.Patterns.Clear(); p.Combinations.Clear();
        p.Wind.Enabled = false; p.Seismic.Enabled = false;
        var b = new CsiModelBuilder(p, rep);
        ExtTable T(string name) => tables.FirstOrDefault(t => ExtTable.Norm(t.Name) == ExtTable.Norm(name));
        IEnumerable<(ExtTable t, string[] r)> Rows(string name)
        {
            var t = T(name);
            if (t == null) yield break;
            foreach (var r in t.Rows) yield return (t, r);
        }
        string S(ExtTable t, string[] r, params string[] cols) => ExtTable.Cell(r, t.Col(cols));
        double D(ExtTable t, string[] r, params string[] cols) => ExtTable.TryNum(S(t, r, cols), out double v) ? v : double.NaN;
        bool Y(ExtTable t, string[] r, string col) => S(t, r, col).StartsWith("Y", StringComparison.OrdinalIgnoreCase);

        var any = tables.FirstOrDefault();
        string fu = any?.FileForceUnit ?? "", lu = any?.FileLengthUnit ?? "";
        double fF = UnitConv.ByQuantity(Qty.Force, fu, lu), fL = UnitConv.ByQuantity(Qty.Length, fu, lu);
        rep.Info.Add($"Đơn vị file: {(fu.Length > 0 ? fu : "KN")}, {(lu.Length > 0 ? lu : "m")} → kN, m.");

        // ---------------- vật liệu ----------------
        var matType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var matFc = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var matFy = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (t, r) in Rows("MATERIAL PROPERTIES 01 - GENERAL")) matType[S(t, r, "Material")] = S(t, r, "Type");
        foreach (var (t, r) in Rows("MATERIAL PROPERTIES 03A - STEEL DATA")) { double v = D(t, r, "Fy"); if (!double.IsNaN(v)) matFy[S(t, r, "Material")] = v * fF / (fL * fL); }
        foreach (var (t, r) in Rows("MATERIAL PROPERTIES 03B - CONCRETE DATA")) { double v = D(t, r, "Fc"); if (!double.IsNaN(v)) matFc[S(t, r, "Material")] = v * fF / (fL * fL); }
        foreach (var (t, r) in Rows("MATERIAL PROPERTIES 03E - REBAR DATA")) { double v = D(t, r, "Fy"); if (!double.IsNaN(v)) { matFy[S(t, r, "Material")] = v * fF / (fL * fL); matType[S(t, r, "Material")] = "Rebar"; } }
        string MatOf(string n) => b.MapMaterial(n, matType.TryGetValue(n ?? "", out var ty) ? ty : "Concrete",
            matFc.TryGetValue(n ?? "", out var fc) ? fc : 0, matFy.TryGetValue(n ?? "", out var fy) ? fy : 0);

        // ---------------- tiết diện ----------------
        foreach (var (t, r) in Rows("FRAME SECTION PROPERTIES 01 - GENERAL"))
        {
            string name = S(t, r, "SectionName"), extMat = S(t, r, "Material"), shape = S(t, r, "Shape").ToUpperInvariant();
            if (name.Length == 0) continue;
            string mat = MatOf(extMat);
            double V(string c) { double v = D(t, r, c); return double.IsNaN(v) ? 0 : v; }
            double t3 = V("t3") * fL, t2 = V("t2") * fL, tf = V("tf") * fL, tw = V("tw") * fL, t2b = V("t2b") * fL, tfb = V("tfb") * fL;
            double A = V("Area") * fL * fL, I33 = V("I33") * Math.Pow(fL, 4), I22 = V("I22") * Math.Pow(fL, 4), J = V("TorsConst") * Math.Pow(fL, 4);
            double S33 = V("S33") * Math.Pow(fL, 3), S22 = V("S22") * Math.Pow(fL, 3);
            if (shape.Contains("RECTANGULAR") && !b.IsSteelMaterial(extMat)) b.AddRect(name, mat, t2, t3);
            else if (shape.Contains("I/WIDE FLANGE") || shape == "I") b.AddI(name, mat, t3, t2, tf, tw, t2b, tfb);
            else if (shape.Contains("CIRCLE"))
            {
                double a = t3 * Math.Sqrt(Math.PI) / 2;
                b.AddRect(name, mat, a, a);
                rep.Warnings.Add($"Tiết diện tròn {name} quy đổi thành vuông {a * 1000:0} mm cùng diện tích.");
            }
            else if (A > 0) b.AddGeneral(name, mat, t3, t2, A, I33, I22, J, S33, S22);
            else
            {
                b.AddRect(name, mat, t2 > 0 ? t2 : 0.2, t3 > 0 ? t3 : 0.3);
                rep.Warnings.Add($"Tiết diện {name} ({shape}) chưa hỗ trợ – quy đổi chữ nhật, cần kiểm tra lại.");
            }
        }

        // ---------------- nút ----------------
        var joints = new Dictionary<string, Vec3>(StringComparer.OrdinalIgnoreCase);
        foreach (var (t, r) in Rows("JOINT COORDINATES"))
        {
            string j = S(t, r, "Joint");
            double x = D(t, r, "GlobalX"), y = D(t, r, "GlobalY"), z = D(t, r, "GlobalZ");
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z))
            {
                if (!S(t, r, "CoordType").StartsWith("Cart", StringComparison.OrdinalIgnoreCase) && S(t, r, "CoordType").Length > 0)
                { rep.Warnings.Add($"Nút {j}: toạ độ không phải Cartesian (thiếu GlobalX/Y/Z) – bỏ qua"); continue; }
                x = D(t, r, "XorR"); y = D(t, r, "Y"); z = D(t, r, "Z");
            }
            if (j.Length == 0 || double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z)) continue;
            joints[j] = new Vec3(x * fL, y * fL, z * fL);
        }
        var restr = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (t, r) in Rows("JOINT RESTRAINT ASSIGNMENTS"))
            restr[S(t, r, "Joint")] = new[] { Y(t, r, "U1"), Y(t, r, "U2"), Y(t, r, "U3"), Y(t, r, "R1"), Y(t, r, "R2"), Y(t, r, "R3") };

        // ---------------- thanh ----------------
        var conn = Rows("CONNECTIVITY - FRAME").Select(x => (f: S(x.t, x.r, "Frame"), i: S(x.t, x.r, "JointI"), j: S(x.t, x.r, "JointJ"))).Where(x => x.f.Length > 0).ToList();
        var secOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (t, r) in Rows("FRAME SECTION ASSIGNMENTS"))
        {
            string s = S(t, r, "AnalSect", "DesignSect", "SectionName");
            if (s.Length > 0) secOf[S(t, r, "Frame")] = s;
        }
        var angOf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (t, r) in Rows("FRAME LOCAL AXES ASSIGNMENTS 1 - TYPICAL")) { double a = D(t, r, "Angle"); if (!double.IsNaN(a)) angOf[S(t, r, "Frame")] = a; }

        // tầng: cao độ dầm / đỉnh cột
        double zBase = restr.Count > 0 && restr.Keys.Any(joints.ContainsKey)
            ? restr.Keys.Where(joints.ContainsKey).Min(k => joints[k].Z) : joints.Count > 0 ? joints.Values.Min(v => v.Z) : 0;
        var levelZ = new List<double>();
        foreach (var c in conn)
        {
            if (!joints.TryGetValue(c.i, out var a) || !joints.TryGetValue(c.j, out var bb)) continue;
            if (Math.Abs(a.Z - bb.Z) < 1e-3) levelZ.Add(a.Z);
            else levelZ.Add(Math.Max(a.Z, bb.Z));
        }
        var levels = ImportGeometry.Cluster(levelZ.Where(z => z > zBase + 0.05), 0.01);
        p.BaseElevation = Math.Round(zBase, 6);
        double prev = zBase;
        for (int i = 0; i < levels.Count; i++)
        {
            string name = i == levels.Count - 1 && levels.Count > 1 ? "MAI" : "T" + (i + 2);
            p.Stories.Add(new Story { Name = name, Elevation = Math.Round(levels[i], 6), Height = Math.Round(levels[i] - prev, 6) });
            prev = levels[i];
        }
        string StoryAt(double z)
        {
            if (Math.Abs(z - zBase) < 0.01) return "Base";
            var st = p.Stories.FirstOrDefault(s => Math.Abs(s.Elevation - z) < 0.01);
            return st?.Name ?? "";
        }
        Node NodeOf(string j)
        {
            var v = joints[j];
            var n = b.NodeAt(v.X, v.Y, v.Z, StoryAt(v.Z), j);
            if (restr.TryGetValue(j, out var rr)) n.Restraint = (bool[])rr.Clone();
            return n;
        }
        var frameOf = new Dictionary<string, FrameElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in conn)
        {
            if (!joints.ContainsKey(c.i) || !joints.ContainsKey(c.j)) { rep.Warnings.Add($"Thanh {c.f}: thiếu toạ độ nút"); continue; }
            string sec = secOf.TryGetValue(c.f, out var s0) ? s0 : "";
            if (!p.FrameSections.Any(s => s.Name == sec))
            {
                rep.Warnings.Add($"Thanh {c.f}: tiết diện \"{sec}\" không khai báo – dùng C22x22");
                sec = p.FrameSections.Any(s => s.Name == "C22x22") ? "C22x22" : p.FrameSections[0].Name;
            }
            var na = NodeOf(c.i); var nb = NodeOf(c.j);
            if (na.Id == nb.Id) continue;
            var kind = b.KindOf(na.P, nb.P, sec, "");
            var f = new FrameElement
            {
                Id = p.Frames.Count + 1, NodeI = na.Id, NodeJ = nb.Id, Kind = kind, Section = sec, Story = StoryAt(Math.Max(na.Z, nb.Z)),
                Label = c.f, ExternalName = c.f, ExternalStory = "",
                DesignGroup = kind is FrameKind.Column or FrameKind.SteelColumn ? $"C-{Math.Round(na.X, 2):0.##}_{Math.Round(na.Y, 2):0.##}" : "",
                Angle = angOf.TryGetValue(c.f, out var an) ? an : 0
            };
            p.Frames.Add(f);
            frameOf[c.f] = f;
        }
        // gối cho nút không nối thanh (bỏ qua) – nút đã tạo đã gán ở NodeOf
        foreach (var (t, r) in Rows("FRAME RELEASE ASSIGNMENTS 1 - GENERAL"))
        {
            if (!frameOf.TryGetValue(S(t, r, "Frame"), out var f)) continue;
            string[] ci = { "PI", "V2I", "V3I", "TI", "M2I", "M3I" }, cj = { "PJ", "V2J", "V3J", "TJ", "M2J", "M3J" };
            for (int k = 0; k < 6; k++) { f.ReleaseI[k] = Y(t, r, ci[k]); f.ReleaseJ[k] = Y(t, r, cj[k]); }
        }
        foreach (var (t, r) in Rows("FRAME PROPERTY MODIFIERS"))
        {
            if (!frameOf.TryGetValue(S(t, r, "Frame"), out var f)) continue;
            var sec = p.FrameSections.First(s => s.Name == f.Section);
            double a = D(t, r, "AMod"), i2 = D(t, r, "I2Mod"), i3 = D(t, r, "I3Mod"), jm = D(t, r, "JMod");
            if (!double.IsNaN(a)) sec.ModA = a;
            if (!double.IsNaN(i2)) sec.ModI22 = i2;
            if (!double.IsNaN(i3)) sec.ModI33 = i3;
            if (!double.IsNaN(jm)) sec.ModJ = jm;
        }
        p.UseRigidDiaphragm = Rows("JOINT CONSTRAINT ASSIGNMENTS").Any(x => S(x.t, x.r, "Type").StartsWith("Diaph", StringComparison.OrdinalIgnoreCase));

        // ---------------- lưới trục ----------------
        foreach (var (t, r) in Rows("GRID LINES"))
        {
            string ax = S(t, r, "AxisDir").ToUpperInvariant(), id = S(t, r, "GridID");
            double c = D(t, r, "XRYZCoord", "Coord");
            if ((ax == "X" || ax == "Y") && id.Length > 0 && !double.IsNaN(c))
                p.Grids.Add(new GridLine { Name = id, Dir = ax == "X" ? GridDir.X : GridDir.Y, Coord = Math.Round(c * fL, 6) });
        }

        // ---------------- sàn ----------------
        var areaSec = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (t, r) in Rows("AREA SECTION ASSIGNMENTS")) areaSec[S(t, r, "Area")] = S(t, r, "Section");
        var shell = new Dictionary<string, (double th, string mat, string type)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (t, r) in Rows("AREA SECTION PROPERTIES"))
        {
            double th = D(t, r, "Thickness", "BendThick", "MembThick");
            shell[S(t, r, "Section")] = (double.IsNaN(th) ? 0.1 : th * fL, S(t, r, "Material", "MatProp"), S(t, r, "AreaType", "Type"));
        }
        var slabOf = new Dictionary<string, Slab>(StringComparer.OrdinalIgnoreCase);
        int vertical = 0;
        foreach (var (t, r) in Rows("CONNECTIVITY - AREA"))
        {
            string an = S(t, r, "Area");
            int nj = (int)(double.IsNaN(D(t, r, "NumJoints")) ? 4 : D(t, r, "NumJoints"));
            var js = new List<string>();
            for (int k = 1; k <= nj; k++) { string j = S(t, r, "Joint" + k); if (j.Length > 0) js.Add(j); }
            if (js.Count < 3 || js.Any(j => !joints.ContainsKey(j))) continue;
            var zs = js.Select(j => joints[j].Z).ToList();
            if (zs.Max() - zs.Min() > 1e-3) { vertical++; continue; }
            string sn = areaSec.TryGetValue(an, out var sx) ? sx : "";
            var sp = shell.TryGetValue(sn, out var spp) ? spp : (0.1, "", "");
            string sec = b.EnsureSlabSection(sn.Length > 0 ? sn : "S" + Math.Round(sp.Item1 * 1000), sp.Item1, MatOf(sp.Item2));
            var slab = new Slab
            {
                Id = p.Slabs.Count + 1, Label = an, Section = sec, Story = StoryAt(zs[0]),
                Nodes = js.Select(j => b.NodeAt(joints[j].X, joints[j].Y, joints[j].Z, StoryAt(joints[j].Z), j).Id).ToList()
            };
            p.Slabs.Add(slab);
            slabOf[an] = slab;
        }
        if (vertical > 0) rep.Warnings.Add($"{vertical} phần tử mặt đứng (vách) chưa hỗ trợ – bỏ qua.");

        // ---------------- tải ----------------
        foreach (var (t, r) in Rows("LOAD PATTERN DEFINITIONS"))
        {
            double sw = D(t, r, "SelfWtMult");
            b.AddPattern(S(t, r, "LoadPat"), S(t, r, "DesignType"), double.IsNaN(sw) ? 0 : sw);
        }
        var caseToPat = new Dictionary<string, List<(string, double)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (t, r) in Rows("CASE - STATIC 1 - LOAD ASSIGNMENTS"))
        {
            if (!S(t, r, "LoadType").StartsWith("Load pattern", StringComparison.OrdinalIgnoreCase)) continue;
            string cs = S(t, r, "Case");
            if (!caseToPat.TryGetValue(cs, out var l)) caseToPat[cs] = l = new List<(string, double)>();
            double sf = D(t, r, "LoadSF");
            l.Add((S(t, r, "LoadName"), double.IsNaN(sf) ? 1 : sf));
        }
        string PatOf(string n)
        {
            if (p.Pattern(n) != null) return n;
            if (caseToPat.TryGetValue(n, out var l) && l.Count == 1 && Math.Abs(l[0].Item2 - 1) < 1e-9 && p.Pattern(l[0].Item1) != null) return l[0].Item1;
            return null;
        }
        LoadDir DirOf(string cs, string dir)
        {
            string d = dir.ToUpperInvariant();
            bool local = cs.StartsWith("Local", StringComparison.OrdinalIgnoreCase);
            if (d.StartsWith("GRAV")) return LoadDir.Gravity;
            if (local) return d == "1" ? LoadDir.Local1 : d == "2" ? LoadDir.Local2 : LoadDir.Local3;
            return d switch { "X" or "XPROJ" => LoadDir.GlobalX, "Y" or "YPROJ" => LoadDir.GlobalY, "Z" or "ZPROJ" => LoadDir.GlobalZ, _ => LoadDir.Gravity };
        }
        int bad = 0;
        foreach (var (t, r) in Rows("FRAME LOADS - DISTRIBUTED"))
        {
            string pat = PatOf(S(t, r, "LoadPat"));
            if (pat == null || !frameOf.TryGetValue(S(t, r, "Frame"), out var f) || !S(t, r, "Type").StartsWith("Force", StringComparison.OrdinalIgnoreCase)) { bad++; continue; }
            double L = p.Length(f);
            double ra = D(t, r, "RelDistA"), rb = D(t, r, "RelDistB"), qa = D(t, r, "FOverLA"), qb = D(t, r, "FOverLB");
            if (double.IsNaN(ra)) ra = D(t, r, "AbsDistA") * fL / L;
            if (double.IsNaN(rb)) rb = D(t, r, "AbsDistB") * fL / L;
            if (double.IsNaN(ra) || double.IsNaN(rb) || double.IsNaN(qa) || double.IsNaN(qb)) { bad++; continue; }
            p.FrameLoads.Add(new FrameLoad
            {
                Element = f.Id, Pattern = pat, Dir = DirOf(S(t, r, "CoordSys"), S(t, r, "Dir")), Note = "SAP2000",
                Points = new List<double[]> { new[] { ra, qa * fF / fL }, new[] { rb, qb * fF / fL } }
            });
        }
        foreach (var (t, r) in Rows("FRAME LOADS - POINT"))
        {
            string pat = PatOf(S(t, r, "LoadPat"));
            if (pat == null || !frameOf.TryGetValue(S(t, r, "Frame"), out var f) || !S(t, r, "Type").StartsWith("Force", StringComparison.OrdinalIgnoreCase)) { bad++; continue; }
            double rd = D(t, r, "RelDist"), v = D(t, r, "Force");
            if (double.IsNaN(rd)) rd = D(t, r, "AbsDist") * fL / p.Length(f);
            if (double.IsNaN(rd) || double.IsNaN(v)) { bad++; continue; }
            p.FrameLoads.Add(new FrameLoad { Element = f.Id, Pattern = pat, Dir = DirOf(S(t, r, "CoordSys"), S(t, r, "Dir")), Note = "SAP2000", PointLoads = new List<double[]> { new[] { rd, v * fF } } });
        }
        foreach (var (t, r) in Rows("JOINT LOADS - FORCE"))
        {
            string pat = PatOf(S(t, r, "LoadPat")), j = S(t, r, "Joint");
            if (pat == null || !joints.ContainsKey(j)) { bad++; continue; }
            var n = NodeOf(j);
            double V(string c) { double v = D(t, r, c); return double.IsNaN(v) ? 0 : v; }
            p.NodeLoads.Add(new NodeLoad { Node = n.Id, Pattern = pat, Fx = V("F1") * fF, Fy = V("F2") * fF, Fz = V("F3") * fF, Mx = V("M1") * fF * fL, My = V("M2") * fF * fL, Mz = V("M3") * fF * fL });
        }
        foreach (var (t, r) in Rows("AREA LOADS - UNIFORM"))
        {
            string pat = PatOf(S(t, r, "LoadPat"));
            if (pat == null || !slabOf.TryGetValue(S(t, r, "Area"), out var s)) { bad++; continue; }
            string dir = S(t, r, "Dir").ToUpperInvariant();
            double q = D(t, r, "UnifLoad");
            if (double.IsNaN(q)) { bad++; continue; }
            double qq = dir.StartsWith("GRAV") ? q : dir == "Z" || dir == "3" ? -q : double.NaN;
            if (double.IsNaN(qq)) { bad++; continue; }
            p.AreaLoads.Add(new AreaLoad { Slab = s.Id, Pattern = pat, Q = qq * fF / (fL * fL), Note = "SAP2000" });
        }
        if (bad > 0) rep.Warnings.Add($"{bad} dòng tải không nhập được (trường hợp tải lạ, kiểu tải khác lực, đối tượng không có).");

        // ---------------- tổ hợp ----------------
        LoadCombination cur = null;
        foreach (var (t, r) in Rows("COMBINATION DEFINITIONS"))
        {
            string cn = S(t, r, "ComboName"), ct = S(t, r, "ComboType");
            if (cn.Length == 0) continue;
            cur = p.Combinations.FirstOrDefault(c => c.Name == cn);
            if (cur == null)
            {
                cur = new LoadCombination { Name = cn, Type = CsiModelBuilder.ComboTypeOf(ct, cn) };
                p.Combinations.Add(cur);
            }
            else if (ct.Length > 0) cur.Type = CsiModelBuilder.ComboTypeOf(ct, cn);
            string item = S(t, r, "CaseName");
            double sf = D(t, r, "ScaleFactor");
            if (double.IsNaN(sf)) sf = 1;
            if (item.Length == 0) continue;
            bool isCombo = S(t, r, "CaseType").IndexOf("Combo", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isCombo) cur.Items.Add(new ComboItem(item, sf));
            else
            {
                string pat = PatOf(item);
                if (pat != null) cur.Items.Add(new ComboItem(pat, sf));
                else if (caseToPat.TryGetValue(item, out var l))
                {
                    foreach (var (pp, s2) in l)
                        if (p.Pattern(pp) != null) cur.Items.Add(new ComboItem(pp, sf * s2));
                }
                else rep.Warnings.Add($"Tổ hợp {cn}: trường hợp {item} không phải tải tĩnh – bỏ qua thành phần này.");
            }
        }
        p.Combinations.RemoveAll(c => c.Items.Count == 0);

        b.Finish(true);
        rep.Info.Insert(0, $"{joints.Count} nút, {conn.Count} thanh, {p.Slabs.Count} ô sàn; {p.Stories.Count} cao trình tầng (suy từ cao độ dầm), {p.Patterns.Count} trường hợp tải, {p.Combinations.Count} tổ hợp.");
        return rep;
    }
}
