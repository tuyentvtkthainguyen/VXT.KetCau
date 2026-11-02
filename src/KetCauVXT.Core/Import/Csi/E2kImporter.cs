using System.Text;
using KetCauVXT.Model;

namespace KetCauVXT.Import.Csi;

/// <summary>Nhập mô hình từ file văn bản ETABS (.e2k / .$et – ETABS 9.7 → v22): tầng, lưới trục, vật liệu, tiết diện thanh
/// (chữ nhật BTCT, chữ I thép, hộp/ống, tổng quát), điểm, cột/dầm/thanh xiên theo tầng, sàn (FLOOR), liên kết gối, giải phóng liên kết,
/// góc xoay, trường hợp tải, tổ hợp, tải trên thanh/sàn/nút.
/// <para>Quy ước ETABS: điểm khai báo theo mặt bằng (x, y), cao độ lấy theo tầng gán; cột LINE "C1" COLUMN "p" "p" n nối điểm p ở tầng gán
/// với điểm p ở n tầng phía dưới; dầm/thanh xiên: điểm 1 ở tầng gán, điểm 2 ở (tầng gán − n). Mỗi dòng LINEASSIGN là một đối tượng
/// (nhãn ETABS lặp lại ở các tầng) → FrameElement.ExternalName = nhãn, ExternalStory = tầng (để khớp bảng nội lực nhập về).
/// Cột và thanh xiên: đầu I ở dưới (như ETABS). Vật liệu được ánh xạ sang thư viện TCVN gần nhất. Vách (PANEL) chưa hỗ trợ – bỏ qua có cảnh báo.</para></summary>
public static class E2kImporter
{
    private sealed class Line
    {
        public string Name = "", Type = "", P1 = "", P2 = "";
        public int N;
    }

    private sealed class Area
    {
        public string Name = "", Type = "";
        public List<string> Pts = new();
        public List<int> Offs = new();
    }

    /// <summary>Tạo dự án mới từ file .e2k.</summary>
    public static Project ImportNew(string path, out ImportReport rep)
    {
        var p = CsiModelBuilder.NewProject(Path.GetFileNameWithoutExtension(path));
        rep = Import(p, path);
        return p;
    }

    /// <summary>Thay mô hình của dự án bằng mô hình trong file .e2k (giữ thông số thiết kế, địa chất, thuyết minh…).</summary>
    public static ImportReport Import(Project p, string path) => Import(p, ExternalTableReader.ReadLines(path), Path.GetFileName(path));

    public static ImportReport Import(Project p, List<string> lines, string source = "e2k")
    {
        var rep = new ImportReport { Source = "ETABS " + source };
        ImportGeometry.ClearModel(p);
        p.Grids.Clear(); p.Stories.Clear(); p.Patterns.Clear(); p.Combinations.Clear();
        p.Wind.Enabled = false; p.Seismic.Enabled = false;
        var b = new CsiModelBuilder(p, rep);

        double fF = 1, fL = 1;
        var storiesTop = new List<(string name, double h, double elev, bool hasElev)>();
        var mats = new Dictionary<string, (string type, double fc, double fy)>(StringComparer.OrdinalIgnoreCase);
        var secs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var secOrder = new List<string>();
        var shells = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var points = new Dictionary<string, (double x, double y)>(StringComparer.OrdinalIgnoreCase);
        var lineDefs = new Dictionary<string, Line>(StringComparer.OrdinalIgnoreCase);
        var areaDefs = new Dictionary<string, Area>(StringComparer.OrdinalIgnoreCase);
        var pointAssigns = new List<List<string>>();
        var lineAssigns = new List<List<string>>();
        var areaAssigns = new List<List<string>>();
        var loadLines = new List<List<string>>();
        var caseToPat = new Dictionary<string, List<(string pat, double sf)>>(StringComparer.OrdinalIgnoreCase);
        var combos = new List<List<string>>();
        var patterns = new List<List<string>>();
        bool diaph = false;
        string title = "";

        foreach (var raw in lines)
        {
            var t = raw.Trim();
            if (t.Length == 0 || t.StartsWith("$")) continue;
            var tk = Tokens(t);
            if (tk.Count == 0) continue;
            string kw = tk[0].ToUpperInvariant();
            switch (kw)
            {
                case "UNITS":
                    if (tk.Count >= 3)
                    {
                        if (UnitConv.TryFactor(tk[1], out double a)) fF = a; else rep.Warnings.Add("Không hiểu đơn vị lực " + tk[1] + " – coi là kN");
                        if (UnitConv.TryFactor(tk[2], out double c)) fL = c; else rep.Warnings.Add("Không hiểu đơn vị dài " + tk[2] + " – coi là m");
                        rep.Info.Add($"Đơn vị file: {tk[1]}, {tk[2]} → kN, m.");
                    }
                    break;
                case "TITLE1": if (tk.Count > 1) title = tk[1]; break;
                case "STORY":
                    if (tk.Count >= 2)
                    {
                        double h = Num(tk, "HEIGHT"), e = Num(tk, "ELEV");
                        storiesTop.Add((tk[1], double.IsNaN(h) ? 0 : h * fL, double.IsNaN(e) ? 0 : e * fL, !double.IsNaN(e)));
                    }
                    break;
                case "GRID":
                    {
                        string lab = Val(tk, "LABEL"), dir = Val(tk, "DIR");
                        double c = Num(tk, "COORD");
                        if (lab != null && !double.IsNaN(c) && (dir == "X" || dir == "Y"))
                            p.Grids.Add(new GridLine { Name = lab, Dir = dir == "X" ? GridDir.X : GridDir.Y, Coord = Math.Round(c * fL, 6) });
                    }
                    break;
                case "MATERIAL":
                    if (tk.Count >= 2)
                    {
                        var cur = mats.TryGetValue(tk[1], out var m) ? m : ("", 0.0, 0.0);
                        string ty = Val(tk, "TYPE") ?? Val(tk, "DESIGNTYPE");
                        if (ty != null) cur.Item1 = ty;
                        double fc = Num(tk, "FC"), fy = Num(tk, "FY");
                        if (!double.IsNaN(fc)) cur.Item2 = fc * fF / (fL * fL);
                        if (!double.IsNaN(fy)) cur.Item3 = fy * fF / (fL * fL);
                        mats[tk[1]] = cur;
                    }
                    break;
                case "FRAMESECTION":
                    if (tk.Count >= 2)
                    {
                        if (!secs.TryGetValue(tk[1], out var d)) { secs[tk[1]] = d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); secOrder.Add(tk[1]); }
                        for (int i = 2; i + 1 < tk.Count; i += 2) d[tk[i]] = tk[i + 1];
                    }
                    break;
                case "SHELLPROP": case "SLABPROP": case "WALLPROP": case "DECKPROP":
                    if (tk.Count >= 2)
                    {
                        if (!shells.TryGetValue(tk[1], out var d)) shells[tk[1]] = d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["__KW"] = kw };
                        for (int i = 2; i + 1 < tk.Count; i += 2) d[tk[i]] = tk[i + 1];
                    }
                    break;
                case "POINT":
                    if (tk.Count >= 4 && ExtTable.TryNum(tk[2], out double px) && ExtTable.TryNum(tk[3], out double py)) points[tk[1]] = (px * fL, py * fL);
                    break;
                case "LINE":
                    if (tk.Count >= 5)
                        lineDefs[tk[1]] = new Line { Name = tk[1], Type = tk[2].ToUpperInvariant(), P1 = tk[3], P2 = tk[4], N = tk.Count > 5 && int.TryParse(tk[5], out int n) ? n : 0 };
                    break;
                case "AREA":
                    if (tk.Count >= 4 && int.TryParse(tk[3], out int np))
                    {
                        var ar = new Area { Name = tk[1], Type = tk[2].ToUpperInvariant() };
                        for (int i = 0; i < np && 4 + i < tk.Count; i++) ar.Pts.Add(tk[4 + i]);
                        for (int i = 0; i < np && 4 + np + i < tk.Count; i++) ar.Offs.Add(int.TryParse(tk[4 + np + i], out int o) ? o : 0);
                        areaDefs[tk[1]] = ar;
                    }
                    break;
                case "POINTASSIGN": pointAssigns.Add(tk); if (Val(tk, "DIAPH") != null) diaph = true; break;
                case "LINEASSIGN": lineAssigns.Add(tk); break;
                case "AREAASSIGN": areaAssigns.Add(tk); if (Val(tk, "DIAPH") != null) diaph = true; break;
                case "POINTLOAD": case "LINELOAD": case "AREALOAD": loadLines.Add(tk); break;
                case "LOADPATTERN": patterns.Add(tk); break;
                case "LOADCASE":
                    if (tk.Count >= 2)
                    {
                        string lp = Val(tk, "LOADPAT");
                        if (lp != null)
                        {
                            double sf = Num(tk, "SF");
                            if (!caseToPat.TryGetValue(tk[1], out var l)) caseToPat[tk[1]] = l = new List<(string, double)>();
                            l.Add((lp, double.IsNaN(sf) ? 1 : sf));
                        }
                    }
                    break;
                case "COMBO": combos.Add(tk); break;
            }
        }
        if (!string.IsNullOrWhiteSpace(title)) p.Name = title;

        // ---------------- tầng ----------------
        var levels = new List<(string name, double z)>();   // từ dưới lên, phần tử 0 = Base
        if (storiesTop.Count == 0) throw new InvalidDataException("File .e2k không có khai báo tầng (STORY).");
        var baseSt = storiesTop[^1];
        double z0 = Math.Round(baseSt.hasElev ? baseSt.elev : 0, 6);
        p.BaseElevation = Math.Round(z0, 6);
        levels.Add((baseSt.name, z0));
        double zc = z0;
        for (int i = storiesTop.Count - 2; i >= 0; i--)
        {
            var s = storiesTop[i];
            zc = Math.Round(s.hasElev && s.h <= 0 ? s.elev : zc + s.h, 6);
            levels.Add((s.name, zc));
            p.Stories.Add(new Story { Name = s.name, Elevation = Math.Round(zc, 6), Height = Math.Round(s.h, 6) });
        }
        var levelIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < levels.Count; i++) levelIndex[levels[i].name] = i;

        // ---------------- vật liệu, tiết diện ----------------
        string MatOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return "B25";
            var m = mats.TryGetValue(name, out var mm) ? mm : ("Concrete", 0.0, 0.0);
            return b.MapMaterial(name, m.Item1, m.Item2, m.Item3);
        }
        foreach (var sn in secOrder)
        {
            var d = secs[sn];
            string shape = (d.TryGetValue("SHAPE", out var shp) ? shp : "").ToUpperInvariant();
            string extMat = d.TryGetValue("MATERIAL", out var mn) ? mn : "";
            string mat = MatOf(extMat);
            double D = G(d, "D") * fL, B = G(d, "B") * fL, TF = G(d, "TF") * fL, TW = G(d, "TW") * fL, BB = G(d, "BB") * fL, TFB = G(d, "TFB") * fL;
            double A = G(d, "AREA") * fL * fL, I33 = G(d, "I33") * Math.Pow(fL, 4), I22 = G(d, "I22") * Math.Pow(fL, 4), J = G(d, "J") * Math.Pow(fL, 4);
            double S33 = G(d, "S33POS") * Math.Pow(fL, 3), S22 = G(d, "S22POS") * Math.Pow(fL, 3);
            bool steelMat = b.IsSteelMaterial(extMat);
            if (shape.Contains("RECTANGULAR") && shape.Contains("CONCRETE") || (shape == "RECTANGULAR" && !steelMat))
                b.AddRect(sn, mat, B, D);
            else if (shape.Contains("I/WIDE FLANGE") || shape.Contains("I-SECTION") || shape == "I")
                b.AddI(sn, mat, D, B, TF, TW, BB, TFB);
            else if (shape.Contains("CIRCLE") && !shape.Contains("PIPE"))
            {
                double a = D * Math.Sqrt(Math.PI) / 2;
                b.AddRect(sn, mat, a, a);
                rep.Warnings.Add($"Tiết diện tròn {sn} (D = {D * 1000:0} mm) quy đổi thành vuông {a * 1000:0}×{a * 1000:0} mm cùng diện tích.");
            }
            else if (A > 0)
                b.AddGeneral(sn, mat, D, B, A, I33, I22, J, S33, S22);
            else if (shape.Contains("TUBE") || shape.Contains("BOX"))
            {
                double tf = TF > 0 ? TF : TW, tw = TW > 0 ? TW : TF;
                double a = D * B - (D - 2 * tf) * (B - 2 * tw);
                double i33 = (B * D * D * D - (B - 2 * tw) * Math.Pow(D - 2 * tf, 3)) / 12, i22 = (D * B * B * B - (D - 2 * tf) * Math.Pow(B - 2 * tw, 3)) / 12;
                double jj = 2 * tf * tw * Math.Pow(D - tf, 2) * Math.Pow(B - tw, 2) / Math.Max(1e-12, (B - tw) * tw + (D - tf) * tf) / 1;
                b.AddGeneral(sn, mat, D, B, a, i33, i22, jj, 0, 0);
            }
            else if (shape.Contains("PIPE"))
            {
                double tt = TW > 0 ? TW : TF, ri = D / 2 - tt;
                double a = Math.PI * (D * D / 4 - ri * ri), ii = Math.PI / 4 * (Math.Pow(D / 2, 4) - Math.Pow(ri, 4));
                b.AddGeneral(sn, mat, D, D, a, ii, ii, 2 * ii, 0, 0);
            }
            else
            {
                double bb = B > 0 ? B : 0.2, dd = D > 0 ? D : 0.3;
                b.AddRect(sn, mat, bb, dd);
                rep.Warnings.Add($"Tiết diện {sn} (\"{shape}\") chưa hỗ trợ – quy đổi chữ nhật {bb * 1000:0}×{dd * 1000:0} mm, cần kiểm tra lại.");
            }
            var fs = p.FrameSections.First(x => x.Name == sn);
            double am = G(d, "AMOD"), i2m = G(d, "I2MOD"), i3m = G(d, "I3MOD"), jm = G(d, "JMOD");
            if (am > 0) fs.ModA = am;
            if (i2m > 0) fs.ModI22 = i2m;
            if (i3m > 0) fs.ModI33 = i3m;
            if (jm > 0) fs.ModJ = jm;
        }

        // ---------------- thanh ----------------
        double ZOf(string story, int down)
        {
            if (!levelIndex.TryGetValue(story, out int i)) throw new InvalidDataException("Tầng không khai báo: " + story);
            int k = Math.Max(0, i - down);
            return levels[k].z;
        }
        string StoryName(double z)
        {
            foreach (var l in levels) if (Math.Abs(l.z - z) < 1e-4) return l.name;
            return "";
        }
        var frameOf = new Dictionary<string, FrameElement>(StringComparer.OrdinalIgnoreCase);   // "tầng|nhãn"
        int skipped = 0;
        foreach (var tk in lineAssigns)
        {
            if (tk.Count < 3) continue;
            string name = tk[1], story = tk[2];
            if (!lineDefs.TryGetValue(name, out var ld)) { skipped++; continue; }
            if (!points.TryGetValue(ld.P1, out var q1) || !points.TryGetValue(ld.P2, out var q2)) { rep.Warnings.Add($"Thanh {name}: thiếu toạ độ điểm"); continue; }
            if (!levelIndex.ContainsKey(story)) { rep.Warnings.Add($"Thanh {name}: tầng {story} không khai báo"); continue; }
            string sec = Val(tk, "SECTION") ?? "";
            if (!p.FrameSections.Any(s => s.Name == sec))
            {
                rep.Warnings.Add($"Thanh {name} tầng {story}: tiết diện \"{sec}\" không khai báo – dùng C22x22");
                sec = p.FrameSections.Any(s => s.Name == "C22x22") ? "C22x22" : p.FrameSections[0].Name;
            }
            double za = ZOf(story, 0), zb = ZOf(story, ld.N);
            string pa = ld.P1, pb = ld.P2;
            if (ld.Type == "COLUMN") { pb = ld.P1; zb = ZOf(story, Math.Max(1, ld.N)); }
            var na = b.NodeAt(q1.x, q1.y, za, StoryName(za), pa);
            var nb = b.NodeAt(ld.Type == "COLUMN" ? q1.x : q2.x, ld.Type == "COLUMN" ? q1.y : q2.y, zb, StoryName(zb), pb);
            if (na.Id == nb.Id) { rep.Warnings.Add($"Thanh {name} tầng {story} có chiều dài 0 – bỏ qua"); continue; }
            // cột / thanh xiên: đầu I ở dưới
            if (ld.Type is "COLUMN" or "BRACE" && na.Z > nb.Z) (na, nb) = (nb, na);
            var kind = b.KindOf(na.P, nb.P, sec, ld.Type);
            var f = new FrameElement
            {
                Id = p.Frames.Count + 1, NodeI = na.Id, NodeJ = nb.Id, Kind = kind, Section = sec, Story = story, Label = name,
                ExternalName = name, ExternalStory = story,
                DesignGroup = kind is FrameKind.Column or FrameKind.SteelColumn ? "C-" + ld.P1 : ""
            };
            double ang = Num(tk, "ANG");
            if (!double.IsNaN(ang)) f.Angle = ang;
            string rel = Val(tk, "RELEASE");
            if (rel != null) CsiModelBuilder.ParseReleases(rel, f.ReleaseI, f.ReleaseJ);
            p.Frames.Add(f);
            frameOf[story + "|" + name] = f;
        }
        if (skipped > 0) rep.Warnings.Add($"{skipped} dòng LINEASSIGN không có LINE tương ứng – bỏ qua.");

        // ---------------- gối ----------------
        foreach (var tk in pointAssigns)
        {
            string res = Val(tk, "RESTRAINT");
            if (tk.Count < 3 || res == null || !points.TryGetValue(tk[1], out var q) || !levelIndex.ContainsKey(tk[2])) continue;
            double z = ZOf(tk[2], 0);
            var n = p.Nodes.FirstOrDefault(x => Math.Abs(x.X - q.x) < 1e-3 && Math.Abs(x.Y - q.y) < 1e-3 && Math.Abs(x.Z - z) < 1e-3);
            if (n == null) continue;
            string[] dofs = { "UX", "UY", "UZ", "RX", "RY", "RZ" };
            var flags = res.ToUpperInvariant().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            n.Restraint = dofs.Select(d => flags.Contains(d)).ToArray();
        }
        p.UseRigidDiaphragm = diaph;
        if (!diaph) rep.Info.Add("File không gán sàn cứng (diaphragm) – tắt giả thiết sàn tuyệt đối cứng.");

        // ---------------- sàn ----------------
        var slabOf = new Dictionary<string, Slab>(StringComparer.OrdinalIgnoreCase);
        int walls = 0, others = 0;
        foreach (var tk in areaAssigns)
        {
            if (tk.Count < 3 || !areaDefs.TryGetValue(tk[1], out var ad) || !levelIndex.ContainsKey(tk[2])) continue;
            string prop = Val(tk, "SECTION") ?? "";
            var sp = shells.TryGetValue(prop, out var spd) ? spd : null;
            string ptype = sp != null && sp.TryGetValue("PROPTYPE", out var pt) ? pt.ToUpperInvariant() : sp != null && sp["__KW"] == "WALLPROP" ? "WALL" : "SLAB";
            if (ad.Type == "PANEL" || ptype == "WALL") { walls++; continue; }
            if (ad.Type != "FLOOR" || ptype is "NONE" or "NULL") { others++; continue; }
            double z = ZOf(tk[2], 0);
            var ids = new List<int>();
            bool ok = true;
            for (int i = 0; i < ad.Pts.Count; i++)
            {
                if (!points.TryGetValue(ad.Pts[i], out var q)) { ok = false; break; }
                double zz = ad.Offs.Count > i && ad.Offs[i] != 0 ? ZOf(tk[2], ad.Offs[i]) : z;
                ids.Add(b.NodeAt(q.x, q.y, zz, StoryName(zz), ad.Pts[i]).Id);
            }
            if (!ok || ids.Count < 3) { rep.Warnings.Add($"Sàn {ad.Name} tầng {tk[2]}: thiếu điểm – bỏ qua"); continue; }
            double th = sp == null ? 0.1 : (G(sp, "SLABTHICKNESS") > 0 ? G(sp, "SLABTHICKNESS") : G(sp, "THICKNESS") > 0 ? G(sp, "THICKNESS") : G(sp, "DECKSLABDEPTH")) * fL;
            string smat = MatOf(sp != null && sp.TryGetValue("MATERIAL", out var sm) ? sm : "");
            string sname = b.EnsureSlabSection(string.IsNullOrEmpty(prop) ? "S" + Math.Round(th * 1000) : prop, th, smat);
            var slab = new Slab { Id = p.Slabs.Count + 1, Label = ad.Name, Nodes = ids, Section = sname, Story = tk[2] };
            p.Slabs.Add(slab);
            slabOf[tk[2] + "|" + ad.Name] = slab;
            var pts = ids.Select(id => (p.Node(id).X, p.Node(id).Y)).ToList();
            if (!ImportGeometry.IsAxisRect(pts, out _, out _, out _, out _))
                rep.Warnings.Add($"Sàn {ad.Name} tầng {tk[2]} không phải ô chữ nhật song song trục – thiết kế sàn theo hệ số sẽ bỏ qua ô này (dùng mô men SAFE nếu có).");
        }
        if (walls > 0) rep.Warnings.Add($"{walls} vách/tường (PANEL) chưa hỗ trợ – bỏ qua (nội lực vách không nhập).");
        if (others > 0) rep.Info.Add($"{others} phần tử mặt khác (ramp/null) bỏ qua.");

        // ---------------- tải ----------------
        foreach (var tk in patterns)
        {
            double sw = Num(tk, "SELFWEIGHT");
            b.AddPattern(tk[1], Val(tk, "TYPE") ?? "Other", double.IsNaN(sw) ? 0 : sw);
        }
        string PatOf(string lc)
        {
            if (lc == null) return null;
            if (p.Pattern(lc) != null) return lc;
            if (caseToPat.TryGetValue(lc, out var l) && l.Count == 1 && Math.Abs(l[0].sf - 1) < 1e-9 && p.Pattern(l[0].pat) != null) return l[0].pat;
            return null;
        }
        int badLoads = 0;
        foreach (var tk in loadLines)
        {
            string kw = tk[0].ToUpperInvariant();
            if (tk.Count < 3) continue;
            string pat = PatOf(Val(tk, "LC"));
            if (pat == null) { badLoads++; continue; }
            string type = (Val(tk, "TYPE") ?? "").ToUpperInvariant();
            string dir = (Val(tk, "DIR") ?? "GRAV").ToUpperInvariant();
            LoadDir ld = dir switch { "GRAV" => LoadDir.Gravity, "X" or "XPROJ" => LoadDir.GlobalX, "Y" or "YPROJ" => LoadDir.GlobalY, "Z" or "ZPROJ" => LoadDir.GlobalZ, "1" => LoadDir.Local1, "2" => LoadDir.Local2, "3" => LoadDir.Local3, _ => LoadDir.Gravity };
            if (kw == "LINELOAD")
            {
                if (!frameOf.TryGetValue(tk[2] + "|" + tk[1], out var f)) { badLoads++; continue; }
                double L = p.Length(f);
                if (type == "UNIFF")
                {
                    double q = Num(tk, "FVAL");
                    if (!double.IsNaN(q)) p.FrameLoads.Add(FrameLoad.Uniform(f.Id, pat, q * fF / fL, ld, "ETABS"));
                }
                else if (type == "TRAPF")
                {
                    double q0 = Num(tk, "FSTART"), q1 = Num(tk, "FEND"), r0 = Num(tk, "RDSTART"), r1 = Num(tk, "RDEND");
                    if (double.IsNaN(r0)) r0 = Num(tk, "DSTART") * fL / L;
                    if (double.IsNaN(r1)) r1 = Num(tk, "DEND") * fL / L;
                    if (double.IsNaN(q0) || double.IsNaN(q1) || double.IsNaN(r0) || double.IsNaN(r1)) { badLoads++; continue; }
                    p.FrameLoads.Add(new FrameLoad
                    {
                        Element = f.Id, Pattern = pat, Dir = ld, Note = "ETABS",
                        Points = new List<double[]> { new[] { r0, q0 * fF / fL }, new[] { r1, q1 * fF / fL } }
                    });
                }
                else if (type == "POINTF")
                {
                    double v = Num(tk, "FVAL"), r = Num(tk, "RDIST");
                    if (double.IsNaN(r)) r = Num(tk, "DIST") * fL / L;
                    if (double.IsNaN(v) || double.IsNaN(r)) { badLoads++; continue; }
                    p.FrameLoads.Add(new FrameLoad { Element = f.Id, Pattern = pat, Dir = ld, Note = "ETABS", PointLoads = new List<double[]> { new[] { r, v * fF } } });
                }
                else badLoads++;
            }
            else if (kw == "AREALOAD")
            {
                if (!slabOf.TryGetValue(tk[2] + "|" + tk[1], out var s) || type != "UNIFF" || ld != LoadDir.Gravity) { badLoads++; continue; }
                double q = Num(tk, "FVAL");
                if (!double.IsNaN(q)) p.AreaLoads.Add(new AreaLoad { Slab = s.Id, Pattern = pat, Q = q * fF / (fL * fL), Note = "ETABS" });
            }
            else if (kw == "POINTLOAD")
            {
                if (!points.TryGetValue(tk[1], out var q) || !levelIndex.ContainsKey(tk[2])) { badLoads++; continue; }
                double z = ZOf(tk[2], 0);
                var n = p.Nodes.FirstOrDefault(x => Math.Abs(x.X - q.x) < 1e-3 && Math.Abs(x.Y - q.y) < 1e-3 && Math.Abs(x.Z - z) < 1e-3);
                if (n == null) { badLoads++; continue; }
                double V(string k) { double v = Num(tk, k); return double.IsNaN(v) ? 0 : v; }
                p.NodeLoads.Add(new NodeLoad
                {
                    Node = n.Id, Pattern = pat, Fx = V("FX") * fF, Fy = V("FY") * fF, Fz = V("FZ") * fF,
                    Mx = V("MX") * fF * fL, My = V("MY") * fF * fL, Mz = V("MZ") * fF * fL
                });
            }
        }
        if (badLoads > 0) rep.Warnings.Add($"{badLoads} dòng tải không nhập được (trường hợp tải lạ, kiểu tải mô men/nhiệt, đối tượng không có).");

        // ---------------- tổ hợp ----------------
        var comboTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tk in combos) { string ty = Val(tk, "TYPE"); if (ty != null && tk.Count >= 2) comboTypes[tk[1]] = ty; }
        foreach (var tk in combos)
        {
            if (tk.Count < 2) continue;
            string cname = tk[1];
            var c = p.Combinations.FirstOrDefault(x => x.Name == cname);
            if (c == null)
            {
                c = new LoadCombination { Name = cname, Type = CsiModelBuilder.ComboTypeOf(comboTypes.TryGetValue(cname, out var ct) ? ct : "", cname) };
                p.Combinations.Add(c);
            }
            string lc = Val(tk, "LOADCASE"), lco = Val(tk, "LOADCOMBO");
            double sf = Num(tk, "SF");
            if (double.IsNaN(sf)) sf = 1;
            if (lc != null)
            {
                string pat = PatOf(lc);
                if (pat != null) c.Items.Add(new ComboItem(pat, sf));
                else if (caseToPat.TryGetValue(lc, out var l))
                {
                    foreach (var (pp, s2) in l)
                        if (p.Pattern(pp) != null) c.Items.Add(new ComboItem(pp, sf * s2));
                }
                else rep.Warnings.Add($"Tổ hợp {cname}: trường hợp {lc} không phải tải tĩnh – bỏ qua thành phần này.");
            }
            else if (lco != null) c.Items.Add(new ComboItem(lco, sf));
        }
        p.Combinations.RemoveAll(c => c.Items.Count == 0);

        b.Finish(true);
        rep.Info.Insert(0, $"{p.Stories.Count} tầng, {p.Grids.Count} trục, {p.FrameSections.Count} tiết diện, {p.Patterns.Count} trường hợp tải, {p.Combinations.Count} tổ hợp.");
        return rep;
    }

    // ------------------------------------------------------------------ cú pháp
    /// <summary>Tách dòng e2k thành các từ; chuỗi trong ngoặc kép là một từ (bỏ ngoặc).</summary>
    public static List<string> Tokens(string line)
    {
        var res = new List<string>();
        var sb = new StringBuilder();
        bool q = false, any = false;
        foreach (char ch in line)
        {
            if (ch == '"') { q = !q; any = true; continue; }
            if (!q && char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0 || any) { res.Add(sb.ToString()); sb.Clear(); any = false; }
                continue;
            }
            sb.Append(ch);
        }
        if (sb.Length > 0 || any) res.Add(sb.ToString());
        return res;
    }

    /// <summary>Giá trị sau từ khoá (không phân biệt hoa thường); null nếu không có.</summary>
    public static string Val(List<string> tk, string key)
    {
        for (int i = 2; i + 1 < tk.Count; i++)
            if (tk[i].Equals(key, StringComparison.OrdinalIgnoreCase)) return tk[i + 1];
        return null;
    }

    public static double Num(List<string> tk, string key)
    {
        var v = Val(tk, key);
        return v != null && ExtTable.TryNum(v, out double d) ? d : double.NaN;
    }

    private static double G(Dictionary<string, string> d, string k) =>
        d.TryGetValue(k, out var s) && ExtTable.TryNum(s, out double v) ? v : 0;
}
