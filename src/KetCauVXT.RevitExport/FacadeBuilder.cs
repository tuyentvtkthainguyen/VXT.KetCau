using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KetCauVXT.RevitExport
{
    // =====================================================================
    // DẦM + MẶT TIỀN khi dựng kiến trúc từ phương án
    //  • Dầm (Beams): họ dầm BTCT chữ nhật (thư viện Revit), mặt trên dầm = cao độ tầng – tường đã hạ xuống đáy dầm trong phương án.
    //  • Chi tiết mặt tiền (Facade.Elements): DirectShape loại Generic Model, vật liệu màu theo bảng vật liệu mặt tiền:
    //      LanCan (kính / thép / xây), Khoi (khung viền, trụ ốp đá, ô văng…), MaiDoc (mái kính / mái lệch), Lam (lam đứng).
    //  • Sơn mặt ngoài tường (mặt không quay vào phòng nào) bằng vật liệu "Tường ngoài".
    // =====================================================================
    internal static class FacadeBuilder
    {
        private static XYZ P(double x, double y, double z) => new XYZ(VxtRevit.ToFt(x), VxtRevit.ToFt(y), VxtRevit.ToFt(z));

        // ------------------------------------------------------------------ dầm
        internal static int BuildBeams(Document d, BuildArchitectureCommand.Ctx c, List<AsBeam> beams)
        {
            var fam = VxtRevit.RectFamily(d, BuiltInCategory.OST_StructuralFraming, c.Log);
            if (fam == null) { c.Log.Add("Không có họ dầm BTCT chữ nhật (thư viện Revit) – chưa dựng dầm."); return 0; }
            int n = 0;
            foreach (var b in beams)
            {
                try
                {
                    if (!c.Lv.TryGetValue(b.Level, out var lv)) { c.Log.Add($"Dầm {b.Id}: không có cao độ \"{b.Level}\"."); continue; }
                    // mặt trên dầm: cao độ tầng, hoặc Z riêng (dầm ngược, dầm lệch cốt)
                    double z = Math.Abs(b.Z) > 1e-9 || Math.Abs(lv.Elevation) < 1e-9 ? VxtRevit.ToFt(b.Z) : lv.Elevation; // ft
                    var a = new XYZ(VxtRevit.ToFt(b.X1), VxtRevit.ToFt(b.Y1), z);
                    var e = new XYZ(VxtRevit.ToFt(b.X2), VxtRevit.ToFt(b.Y2), z);
                    if (a.DistanceTo(e) < 0.2) continue;
                    string name = $"D{Math.Round(b.B * 1000)}x{Math.Round(b.H * 1000)}";
                    var sym = VxtRevit.RectType(d, fam, name, b.B, b.H);
                    if (sym == null) { c.Log.Add($"Dầm {b.Id}: không tạo được loại {name}."); continue; }
                    if (!sym.IsActive) { sym.Activate(); d.Regenerate(); }
                    var fi = d.Create.NewFamilyInstance(Line.CreateBound(a, e), sym, lv, StructuralType.Beam);
                    fi.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set(b.Id);
                    VxtRevit.SetVxtId(fi, BuildArchitectureCommand.Prefix + b.Id);
                    n++;
                }
                catch (Exception ex) { c.Log.Add($"Dầm {b.Id}: {ex.Message}"); }
            }
            return n;
        }

        // ------------------------------------------------------------------ cột
        internal static int BuildColumns(Document d, BuildArchitectureCommand.Ctx c, List<AsColumn> cols)
        {
            var fam = VxtRevit.RectFamily(d, BuiltInCategory.OST_StructuralColumns, c.Log);
            if (fam == null) { c.Log.Add("Không có họ cột BTCT chữ nhật (thư viện Revit) – chưa dựng cột."); return 0; }
            int n = 0;
            foreach (var k in cols)
            {
                try
                {
                    if (!c.Lv.TryGetValue(k.BaseLevel, out var bl) || !c.Lv.TryGetValue(k.TopLevel, out var tl)) { c.Log.Add($"Cột {k.Id}: thiếu cao độ {k.BaseLevel} / {k.TopLevel}."); continue; }
                    string name = $"C{Math.Round(k.B * 1000)}x{Math.Round(k.H * 1000)}";
                    var sym = VxtRevit.RectType(d, fam, name, k.B, k.H);
                    if (sym == null) { c.Log.Add($"Cột {k.Id}: không tạo được loại {name}."); continue; }
                    if (!sym.IsActive) { sym.Activate(); d.Regenerate(); }
                    var fi = d.Create.NewFamilyInstance(new XYZ(VxtRevit.ToFt(k.X), VxtRevit.ToFt(k.Y), bl.Elevation), sym, bl, StructuralType.Column);
                    fi.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM)?.Set(bl.Id);
                    fi.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)?.Set(0.0);
                    fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(tl.Id);
                    fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(0.0);
                    if (Math.Abs(k.Rot) > 1e-6)
                    {
                        var p = new XYZ(VxtRevit.ToFt(k.X), VxtRevit.ToFt(k.Y), 0);
                        ElementTransformUtils.RotateElement(d, fi.Id, Line.CreateBound(p, p + XYZ.BasisZ), k.Rot * Math.PI / 180);
                    }
                    fi.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set(k.Id);
                    VxtRevit.SetVxtId(fi, BuildArchitectureCommand.Prefix + k.Id);
                    n++;
                }
                catch (Exception ex) { c.Log.Add($"Cột {k.Id}: {ex.Message}"); }
            }
            return n;
        }

        // ------------------------------------------------------------------ kết nối hình học
        private static int Rank(Element e)
        {
            var cat = e.Category?.BuiltInCategory ?? BuiltInCategory.INVALID;
            if (cat == BuiltInCategory.OST_StructuralColumns) return 4;
            if (cat == BuiltInCategory.OST_Floors) return 3;
            if (cat == BuiltInCategory.OST_StructuralFraming) return 2;
            if (cat == BuiltInCategory.OST_Walls) return 1;
            return 0;
        }

        /// <summary>
        /// Join các phần tử KT- theo cấu tạo để khối lượng vật tư không bị tính trùng: cột &gt; sàn &gt; dầm &gt; tường;
        /// dầm – dầm: dầm cao hơn (dầm chính) cắt dầm thấp; dầm – cột: cột cắt dầm. Chỉ nối cặp giao nhau thật
        /// (khối đặc chồng lấn) – cặp đã nối mà không giao thì bỏ nối (hết cảnh báo "joined but do not intersect").
        /// Tường – tường do Revit tự nối ở đầu tường.
        /// </summary>
        internal static string JoinAll(Document d)
        {
            var els = new List<(Element E, int R, BoundingBoxXYZ B)>();
            foreach (var bic in new[] { BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_Floors, BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_Walls })
                foreach (var e in new FilteredElementCollector(d).OfCategory(bic).WhereElementIsNotElementType())
                {
                    string id; try { id = VxtRevit.GetVxtId(e); } catch { continue; }
                    if (!id.StartsWith(BuildArchitectureCommand.Prefix, StringComparison.Ordinal)) continue;
                    var bb = e.get_BoundingBox(null);
                    if (bb != null) els.Add((e, Rank(e), bb));
                }
            double tol = VxtRevit.ToFt(0.002);
            int joined = 0, switched = 0, unjoined = 0, failed = 0;
            var failedPairs = new List<string>();
            for (int i = 0; i < els.Count; i++)
                for (int j = i + 1; j < els.Count; j++)
                {
                    var a = els[i]; var b = els[j];
                    if (a.R == b.R && a.R != 2) continue;                         // tường – tường (Revit tự nối), sàn – sàn, cột – cột
                    if (a.B.Max.X < b.B.Min.X - tol || b.B.Max.X < a.B.Min.X - tol || a.B.Max.Y < b.B.Min.Y - tol || b.B.Max.Y < a.B.Min.Y - tol || a.B.Max.Z < b.B.Min.Z - tol || b.B.Max.Z < a.B.Min.Z - tol) continue;
                    bool hit;
                    try { hit = new ElementIntersectsElementFilter(a.E).PassesFilter(b.E); } catch { hit = false; }
                    bool isJ = false;
                    try { isJ = JoinGeometryUtils.AreElementsJoined(d, a.E, b.E); } catch { }
                    if (!hit)
                    {
                        if (isJ) { try { JoinGeometryUtils.UnjoinGeometry(d, a.E, b.E); unjoined++; } catch { } }
                        continue;
                    }
                    Element hi, lo;
                    if (a.R != b.R) { hi = a.R > b.R ? a.E : b.E; lo = a.R > b.R ? b.E : a.E; }
                    else
                    {
                        // hai dầm: dầm cao hơn cắt; bằng nhau thì dầm dài hơn cắt
                        double ha = a.B.Max.Z - a.B.Min.Z, hb = b.B.Max.Z - b.B.Min.Z;
                        double la = Math.Max(a.B.Max.X - a.B.Min.X, a.B.Max.Y - a.B.Min.Y), lb = Math.Max(b.B.Max.X - b.B.Min.X, b.B.Max.Y - b.B.Min.Y);
                        bool aCut = Math.Abs(ha - hb) > tol ? ha > hb : la >= lb;
                        hi = aCut ? a.E : b.E; lo = aCut ? b.E : a.E;
                    }
                    try
                    {
                        if (!isJ) { JoinGeometryUtils.JoinGeometry(d, hi, lo); joined++; }
                        if (!JoinGeometryUtils.IsCuttingElementInJoin(d, hi, lo)) { JoinGeometryUtils.SwitchJoinOrder(d, hi, lo); switched++; }
                    }
                    catch
                    {
                        failed++;
                        if (failedPairs.Count < 8) { try { failedPairs.Add(VxtRevit.GetVxtId(hi) + " / " + VxtRevit.GetVxtId(lo)); } catch { } }
                    }
                }
            return $"Kết nối hình học: {joined} cặp mới, {switched} cặp đổi thứ tự cắt" + (unjoined > 0 ? $", bỏ nối {unjoined} cặp không giao" : "")
                 + (failed > 0 ? $"; {failed} cặp chồng lấn chưa nối được (khối lượng có thể bị tính trùng): " + string.Join("; ", failedPairs) : "; không còn chồng lấn giữa cột / dầm / sàn / tường") + ".";
        }

        // ------------------------------------------------------------------ vật liệu
        private static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Tường ngoài", "#F1ECE3" }, { "Kính", "#8FB3C4" }, { "Tay vịn", "#2B2B2B" }, { "Khung viền", "#3F4448" },
            { "Ốp đá", "#5F6266" }, { "Lam", "#8B5A2B" }, { "Mái", "#4A4F55" }, { "Lan can", "#2B2B2B" },
        };

        internal static string ColorOf(ArchSpec s, string part, string explicitHex = "")
        {
            if (!string.IsNullOrWhiteSpace(explicitHex)) return explicitHex;
            string p = (part ?? "").Trim();
            var m = s.FacadeMaterials.FirstOrDefault(x => x.Part.Equals(p, StringComparison.OrdinalIgnoreCase))
                 ?? s.FacadeMaterials.FirstOrDefault(x => p.Length > 0 && (x.Part.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf(x.Part, StringComparison.OrdinalIgnoreCase) >= 0));
            if (m != null && !string.IsNullOrWhiteSpace(m.Color)) return m.Color;
            foreach (var kv in Defaults) if (p.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0) return kv.Value;
            return "#BBBBBB";
        }

        internal static ElementId Mat(Document d, string name, string hex, int transparency = 0)
        {
            var m = new FilteredElementCollector(d).OfClass(typeof(Material)).Cast<Material>().FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (m == null) m = d.GetElement(Material.Create(d, name)) as Material;
            if (m == null) return ElementId.InvalidElementId;
            var col = Hex(hex);
            if (col != null) { try { m.Color = col; } catch { } try { m.SurfaceForegroundPatternColor = col; } catch { } }
            try { m.Transparency = transparency; } catch { }
            try { m.Shininess = transparency > 0 ? 100 : 20; } catch { }
            return m.Id;
        }

        private static Color Hex(string hex)
        {
            string h = (hex ?? "").Trim().TrimStart('#');
            if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return null;
            return new Color((byte)((v >> 16) & 255), (byte)((v >> 8) & 255), (byte)(v & 255));
        }

        private static ElementId MatFor(Document d, ArchSpec s, string part, string hex = "")
        {
            string key = string.IsNullOrWhiteSpace(part) ? "Mặt tiền" : part.Trim();
            bool glass = key.IndexOf("kính", StringComparison.OrdinalIgnoreCase) >= 0;
            return Mat(d, "VXT - " + key, ColorOf(s, key, hex), glass ? 65 : 0);
        }

        // ------------------------------------------------------------------ hình khối
        private static Solid Prism(IList<double[]> poly, double z0, double z1, ElementId mat)
        {
            if (z1 - z0 < 0.002 || poly.Count < 3) return null;
            var loop = new CurveLoop();
            var pts = poly.Select(p => P(p[0], p[1], z0)).ToList();
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % pts.Count];
                if (a.DistanceTo(b) > 1e-4) loop.Append(Line.CreateBound(a, b));
            }
            return GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, VxtRevit.ToFt(z1 - z0), new SolidOptions(mat, ElementId.InvalidElementId));
        }

        /// <summary>Hộp dọc đoạn a→b, rộng w (m), từ z0 tới z1.</summary>
        private static Solid Bar(double ax, double ay, double bx, double by, double w, double z0, double z1, ElementId mat)
        {
            double dx = bx - ax, dy = by - ay, l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 0.005) return null;
            double nx = -dy / l * w / 2, ny = dx / l * w / 2;
            return Prism(new List<double[]> { new[] { ax + nx, ay + ny }, new[] { bx + nx, by + ny }, new[] { bx - nx, by - ny }, new[] { ax - nx, ay - ny } }, z0, z1, mat);
        }

        private static Solid Post(double x, double y, double w, double z0, double z1, ElementId mat) =>
            Prism(new List<double[]> { new[] { x - w / 2, y - w / 2 }, new[] { x + w / 2, y - w / 2 }, new[] { x + w / 2, y + w / 2 }, new[] { x - w / 2, y + w / 2 } }, z0, z1, mat);

        private static double[] Box(List<double[]> pts) =>
            new[] { pts.Min(p => p[0]), pts.Min(p => p[1]), pts.Max(p => p[0]), pts.Max(p => p[1]) };

        private static List<double[]> Rect(double[] b) =>
            new List<double[]> { new[] { b[0], b[1] }, new[] { b[2], b[1] }, new[] { b[2], b[3] }, new[] { b[0], b[3] } };

        private static List<GeometryObject> Geometry(Document d, ArchSpec s, AsFacadeElement e, double levelZ)
        {
            var g = new List<GeometryObject>();
            void Add(Solid x) { if (x != null && x.Volume > 1e-9) g.Add(x); }
            switch (e.Type)
            {
                case "LanCan":
                {
                    double z0 = levelZ + e.Z0, h = e.Height > 0.3 ? e.Height : 1.1;
                    string st = (e.Style ?? "").ToLowerInvariant();
                    var rail = MatFor(d, s, "Tay vịn");
                    if (st.Contains("xây"))
                    {
                        var wm = MatFor(d, s, string.IsNullOrEmpty(e.Part) ? "Tường ngoài" : e.Part, e.Color);
                        for (int i = 0; i + 1 < e.Points.Count; i++) Add(Bar(e.Points[i][0], e.Points[i][1], e.Points[i + 1][0], e.Points[i + 1][1], 0.11, z0, z0 + h, wm));
                        break;
                    }
                    bool glass = !st.Contains("thép") && !st.Contains("sắt");
                    var fill = glass ? MatFor(d, s, "Kính lan can", e.Color.Length > 0 ? e.Color : ColorOf(s, "Kính")) : rail;
                    for (int i = 0; i + 1 < e.Points.Count; i++)
                    {
                        double ax = e.Points[i][0], ay = e.Points[i][1], bx = e.Points[i + 1][0], by = e.Points[i + 1][1];
                        double l = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                        if (l < 0.01) continue;
                        Add(Bar(ax, ay, bx, by, 0.06, z0 + h - 0.05, z0 + h, rail));                // tay vịn
                        if (glass) Add(Bar(ax, ay, bx, by, 0.015, z0 + 0.08, z0 + h - 0.06, fill)); // tấm kính
                        else
                        {
                            Add(Bar(ax, ay, bx, by, 0.04, z0 + 0.08, z0 + 0.12, rail));
                            int nb = Math.Max(1, (int)Math.Floor(l / 0.12));
                            for (int k = 1; k < nb; k++) Add(Post(ax + (bx - ax) * k / nb, ay + (by - ay) * k / nb, 0.02, z0 + 0.12, z0 + h - 0.05, rail));
                        }
                        int np = Math.Max(1, (int)Math.Ceiling(l / 1.2));
                        for (int k = 0; k <= np; k++)
                        {
                            if (k == np && i + 2 < e.Points.Count) continue; // góc dùng chung
                            Add(Post(ax + (bx - ax) * k / np, ay + (by - ay) * k / np, 0.05, z0, z0 + h - 0.05, rail));
                        }
                    }
                    break;
                }
                case "MaiDoc":
                {
                    var b = Box(e.Points); double t = e.Thickness > 0.005 ? e.Thickness : 0.12;
                    var m = MatFor(d, s, string.IsNullOrEmpty(e.Part) ? "Mái" : e.Part, e.Color);
                    string ax = (e.SlopeAxis ?? "+X").Replace("−", "-").Trim().ToUpperInvariant();
                    bool alongX = ax.EndsWith("X"); bool neg = ax.StartsWith("-");
                    double lo = e.Z0, hi = e.Z1;
                    double zA = neg ? hi : lo, zB = neg ? lo : hi; // A = phía min, B = phía max
                    var loop = new CurveLoop(); XYZ dir; double len;
                    List<XYZ> q;
                    if (alongX)
                    {
                        q = new List<XYZ> { P(b[0], b[1], zA), P(b[2], b[1], zB), P(b[2], b[1], zB + t), P(b[0], b[1], zA + t) };
                        dir = XYZ.BasisY; len = b[3] - b[1];
                    }
                    else
                    {
                        q = new List<XYZ> { P(b[0], b[1], zA), P(b[0], b[3], zB), P(b[0], b[3], zB + t), P(b[0], b[1], zA + t) };
                        dir = XYZ.BasisX; len = b[2] - b[0];
                    }
                    for (int i = 0; i < 4; i++) loop.Append(Line.CreateBound(q[i], q[(i + 1) % 4]));
                    if (len > 0.01) Add(GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, dir, VxtRevit.ToFt(len), new SolidOptions(m, ElementId.InvalidElementId)));
                    break;
                }
                case "Lam":
                {
                    var b = Box(e.Points); var m = MatFor(d, s, string.IsNullOrEmpty(e.Part) ? "Lam" : e.Part, e.Color);
                    double th = e.Thickness > 0.005 ? e.Thickness : 0.05, sp = e.Spacing > th ? e.Spacing : 0.15;
                    bool alongY = b[3] - b[1] >= b[2] - b[0];
                    double L = alongY ? b[3] - b[1] : b[2] - b[0];
                    int n = Math.Max(1, (int)Math.Floor((L - th) / sp) + 1);
                    double start = (L - (n - 1) * sp) / 2;
                    for (int k = 0; k < n; k++)
                    {
                        double u = start + k * sp;
                        var r = alongY ? new[] { b[0], b[1] + u - th / 2, b[2], b[1] + u + th / 2 } : new[] { b[0] + u - th / 2, b[1], b[0] + u + th / 2, b[3] };
                        Add(Prism(Rect(r), e.Z0, e.Z1, m));
                    }
                    break;
                }
                default: // Khoi
                {
                    var m = MatFor(d, s, string.IsNullOrEmpty(e.Part) ? "Khung viền" : e.Part, e.Color);
                    var poly = e.Points.Count == 2 ? Rect(Box(e.Points)) : e.Points;
                    Add(Prism(poly, e.Z0, e.Z1, m));
                    break;
                }
            }
            return g;
        }

        internal static int BuildElements(Document d, BuildArchitectureCommand.Ctx c, List<AsFacadeElement> els)
        {
            int n = 0;
            var cat = new ElementId(BuiltInCategory.OST_GenericModel);
            foreach (var e in els)
            {
                try
                {
                    double lz = 0;
                    if (!string.IsNullOrEmpty(e.Level))
                    {
                        var l = c.S.Levels.FirstOrDefault(x => x.Name.Equals(e.Level, StringComparison.OrdinalIgnoreCase));
                        if (l != null) lz = l.Elevation;
                    }
                    var geo = Geometry(d, c.S, e, lz);
                    if (geo.Count == 0) { c.Log.Add($"Chi tiết mặt tiền {e.Id}: không tạo được hình (kiểm tra Points / Z0 / Z1)."); continue; }
                    var ds = DirectShape.CreateElement(d, cat);
                    ds.ApplicationId = "VietConst"; ds.ApplicationDataId = e.Id;
                    ds.SetShape(geo);
                    string label = (e.Type == "LanCan" ? "Lan can " + e.Style : e.Type == "MaiDoc" ? "Mái dốc" : e.Type == "Lam" ? "Lam" : "Khối") + (string.IsNullOrEmpty(e.Part) ? "" : " – " + e.Part);
                    try { ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(label.Trim()); } catch { }
                    try { ds.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.Set(e.Id); } catch { }
                    VxtRevit.SetVxtId(ds, BuildArchitectureCommand.Prefix + e.Id);
                    n++;
                }
                catch (Exception ex) { c.Log.Add($"Chi tiết mặt tiền {e.Id}: {ex.Message}"); }
            }
            return n;
        }

        // ------------------------------------------------------------------ sơn mặt ngoài tường
        private static bool Outdoor(string room) =>
            room.IndexOf("ban công", StringComparison.OrdinalIgnoreCase) >= 0 || room.IndexOf("lô gia", StringComparison.OrdinalIgnoreCase) >= 0 ||
            room.IndexOf("logia", StringComparison.OrdinalIgnoreCase) >= 0 || room.IndexOf("sân", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool InPoly(List<double[]> poly, double x, double y)
        {
            bool c = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                double xi = poly[i][0], yi = poly[i][1], xj = poly[j][0], yj = poly[j][1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) c = !c;
            }
            return c;
        }

        /// <summary>Sơn mặt tường không quay vào phòng trong nhà (theo đường bao phòng của phương án) bằng vật liệu "Tường ngoài".</summary>
        internal static int PaintExterior(Document d, BuildArchitectureCommand.Ctx c)
        {
            var s = c.S;
            var mat = Mat(d, "VXT - Tường ngoài", ColorOf(s, "Tường ngoài"));
            var byId = s.Walls.ToDictionary(w => BuildArchitectureCommand.Prefix + w.Id, w => w, StringComparer.OrdinalIgnoreCase);
            int n = 0;
            foreach (var wall in new FilteredElementCollector(d).OfClass(typeof(Wall)).Cast<Wall>())
            {
                string id; try { id = VxtRevit.GetVxtId(wall); } catch { continue; }
                if (!byId.TryGetValue(id, out var spec)) continue;
                var rooms = s.Rooms.Where(r => r.Level.Equals(spec.Level, StringComparison.OrdinalIgnoreCase) && r.Outline.Count >= 3 && !Outdoor(r.Name)).ToList();
                foreach (var side in new[] { ShellLayerType.Exterior, ShellLayerType.Interior })
                {
                    IList<Reference> refs;
                    try { refs = HostObjectUtils.GetSideFaces(wall, side); } catch { continue; }
                    foreach (var r in refs)
                    {
                        if (!(wall.GetGeometryObjectFromReference(r) is Face f)) continue;
                        var bb = f.GetBoundingBox();
                        var uv = new UV((bb.Min.U + bb.Max.U) / 2, (bb.Min.V + bb.Max.V) / 2);
                        XYZ p, nrm;
                        try { p = f.Evaluate(uv); nrm = f.ComputeNormal(uv); } catch { continue; }
                        double off = VxtRevit.ToFt(0.3);
                        var q = p + new XYZ(nrm.X, nrm.Y, 0).Normalize() * off;
                        double qx = q.X / VxtRevit.ToFt(1), qy = q.Y / VxtRevit.ToFt(1);
                        if (rooms.Any(rm => InPoly(rm.Outline, qx, qy))) continue;
                        try { d.Paint(wall.Id, f, mat); n++; } catch { }
                    }
                }
            }
            // mép sàn (ban công, mái đua, sàn tầng) quay ra ngoài
            var lvOrder = s.Levels.OrderBy(l => l.Elevation).Select(l => l.Name).ToList();
            var fById = s.Floors.ToDictionary(f => BuildArchitectureCommand.Prefix + f.Id, f => f, StringComparer.OrdinalIgnoreCase);
            foreach (var fl in new FilteredElementCollector(d).OfClass(typeof(Floor)).Cast<Floor>())
            {
                string id; try { id = VxtRevit.GetVxtId(fl); } catch { continue; }
                if (!fById.TryGetValue(id, out var fs)) continue;
                int li = lvOrder.FindIndex(x => x.Equals(fs.Level, StringComparison.OrdinalIgnoreCase));
                var lvNames = new List<string> { fs.Level };
                if (li > 0) lvNames.Add(lvOrder[li - 1]);
                var rooms = s.Rooms.Where(r => lvNames.Contains(r.Level, StringComparer.OrdinalIgnoreCase) && r.Outline.Count >= 3 && !Outdoor(r.Name)).ToList();
                var geo = fl.get_Geometry(new Options { ComputeReferences = true });
                if (geo == null) continue;
                foreach (var go in geo)
                {
                    if (!(go is Solid so) || so.Volume <= 0) continue;
                    foreach (Face f in so.Faces)
                    {
                        var bb = f.GetBoundingBox();
                        var uv = new UV((bb.Min.U + bb.Max.U) / 2, (bb.Min.V + bb.Max.V) / 2);
                        XYZ p, nrm;
                        try { p = f.Evaluate(uv); nrm = f.ComputeNormal(uv); } catch { continue; }
                        if (Math.Abs(nrm.Z) > 0.2) continue;
                        var q = p + new XYZ(nrm.X, nrm.Y, 0).Normalize() * VxtRevit.ToFt(0.3);
                        double qx = q.X / VxtRevit.ToFt(1), qy = q.Y / VxtRevit.ToFt(1);
                        if (rooms.Any(rm => InPoly(rm.Outline, qx, qy))) continue;
                        try { d.Paint(fl.Id, f, mat); n++; } catch { }
                    }
                }
            }
            return n;
        }
    }
}
