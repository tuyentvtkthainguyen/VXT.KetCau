// =====================================================================
// VIỆT CONST – ADD-IN REVIT: XUẤT KIẾN TRÚC + ĐIỆN NƯỚC (schema 2.0)
// Walls: đường định vị, chiều cao, lớp cấu tạo (ngoài → trong); Openings: cửa đi / cửa sổ (tường chủ, vị trí, kích thước, bậu);
// Rooms: đường bao; Mep: ống nước, phụ kiện, thiết bị vệ sinh, ống luồn dây điện, đèn, thiết bị điện, ống gió.
// Id = Element.UniqueId (ổn định giữa các lần xuất) – Việt Const dùng để "Cập nhật từ Revit".
// =====================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KetCauVXT.RevitExport
{
    public partial class ExportCommand
    {
        private int WriteWalls(Document doc, JsonWriter w)
        {
            int n = 0;
            w.BeginArray("Walls");
            foreach (var wall in new FilteredElementCollector(doc).OfClass(typeof(Wall)).Cast<Wall>())
            {
                try
                {
                    var lc = wall.Location as LocationCurve;
                    if (lc == null || lc.Curve == null) continue;
                    if (!(lc.Curve is Line)) _warnings.Add($"Tường {wall.Id} cong – thay bằng dây cung.");
                    XYZ a = lc.Curve.GetEndPoint(0), b = lc.Curve.GetEndPoint(1);
                    double h = ParamDouble(wall, BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                    double off = ParamDouble(wall, BuiltInParameter.WALL_BASE_OFFSET);
                    var wt = wall.WallType;
                    w.BeginObject();
                    w.Prop("Id", wall.UniqueId);
                    w.Prop("Level", LevelName(doc, wall.LevelId));
                    w.Prop("X1", a.X * Ft); w.Prop("Y1", a.Y * Ft);
                    w.Prop("X2", b.X * Ft); w.Prop("Y2", b.Y * Ft);
                    w.Prop("Height", h * Ft);
                    w.Prop("BaseOffset", off * Ft);
                    w.Prop("TypeName", wt != null ? wt.Name : "");
                    w.BeginArray("Layers");
                    var cs = wt != null ? wt.GetCompoundStructure() : null;
                    if (cs != null)
                        foreach (var layer in cs.GetLayers())
                        {
                            string mat = "";
                            if (layer.MaterialId != null && layer.MaterialId != ElementId.InvalidElementId)
                            {
                                var m = doc.GetElement(layer.MaterialId) as Material;
                                if (m != null) mat = m.Name;
                            }
                            w.BeginObject();
                            w.Prop("Function", layer.Function.ToString());
                            w.Prop("Material", mat);
                            w.Prop("Thickness", layer.Width * Ft);
                            w.EndObject();
                        }
                    w.EndArray();
                    w.EndObject();
                    n++;
                }
                catch (Exception ex) { _warnings.Add($"Tường {wall.Id}: {ex.Message}"); }
            }
            w.EndArray();
            return n;
        }

        private static double Size(FamilyInstance fi, BuiltInParameter bip, params string[] names)
        {
            double v = ParamDouble(fi, bip);
            if (v <= 1e-6 && fi.Symbol != null) v = ParamDouble(fi.Symbol, bip);
            if (v <= 1e-6) v = FindSize(fi, names);
            return v;
        }

        private int WriteOpenings(Document doc, JsonWriter w)
        {
            int n = 0;
            w.BeginArray("Openings");
            foreach (var cat in new[] { BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows })
            {
                bool door = cat == BuiltInCategory.OST_Doors;
                foreach (var fi in new FilteredElementCollector(doc).OfCategory(cat).WhereElementIsNotElementType().OfType<FamilyInstance>())
                {
                    try
                    {
                        var lp = fi.Location as LocationPoint;
                        if (lp == null) continue;
                        double width = door ? Size(fi, BuiltInParameter.DOOR_WIDTH, "Width", "Rough Width", "Chiều rộng")
                                            : Size(fi, BuiltInParameter.WINDOW_WIDTH, "Width", "Rough Width", "Chiều rộng");
                        double height = door ? Size(fi, BuiltInParameter.DOOR_HEIGHT, "Height", "Rough Height", "Chiều cao")
                                             : Size(fi, BuiltInParameter.WINDOW_HEIGHT, "Height", "Rough Height", "Chiều cao");
                        double sill = ParamDouble(fi, BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
                        w.BeginObject();
                        w.Prop("Id", fi.UniqueId);
                        w.Prop("HostId", fi.Host != null ? fi.Host.UniqueId : "");
                        w.Prop("Category", door ? "Door" : "Window");
                        w.Prop("TypeName", fi.Symbol != null ? fi.Symbol.Name : "");
                        w.Prop("X", lp.Point.X * Ft); w.Prop("Y", lp.Point.Y * Ft);
                        w.Prop("Width", width * Ft); w.Prop("Height", height * Ft); w.Prop("Sill", sill * Ft);
                        w.Prop("Flipped", fi.FacingFlipped);
                        w.EndObject();
                        n++;
                    }
                    catch (Exception ex) { _warnings.Add($"Cửa {fi.Id}: {ex.Message}"); }
                }
            }
            w.EndArray();
            return n;
        }

        private int WriteRooms(Document doc, JsonWriter w)
        {
            int n = 0;
            w.BeginArray("Rooms");
            var opt = new SpatialElementBoundaryOptions();
            foreach (var se in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().OfType<SpatialElement>())
            {
                try
                {
                    if (se.Area <= 1e-6) continue;
                    var loops = se.GetBoundarySegments(opt);
                    if (loops == null || loops.Count == 0) continue;
                    var pts = new List<XYZ>();
                    foreach (var seg in loops[0])
                    {
                        var c = seg.GetCurve();
                        if (c is Line) pts.Add(c.GetEndPoint(0));
                        else { var t = c.Tessellate(); for (int i = 0; i + 1 < t.Count; i++) pts.Add(t[i]); }
                    }
                    if (pts.Count < 3) continue;
                    var pn = se.get_Parameter(BuiltInParameter.ROOM_NAME);
                    var pnum = se.get_Parameter(BuiltInParameter.ROOM_NUMBER);
                    w.BeginObject();
                    w.Prop("Id", se.UniqueId);
                    w.Prop("Level", LevelName(doc, se.LevelId));
                    w.Prop("Name", pn != null ? (pn.AsString() ?? "") : "");
                    w.Prop("Number", pnum != null ? (pnum.AsString() ?? "") : "");
                    w.BeginArray("Outline");
                    foreach (var p in pts) w.Pair(p.X * Ft, p.Y * Ft);
                    w.EndArray();
                    w.EndObject();
                    n++;
                }
                catch (Exception ex) { _warnings.Add($"Phòng {se.Id}: {ex.Message}"); }
            }
            w.EndArray();
            return n;
        }

        private int WriteMep(Document doc, JsonWriter w)
        {
            int n = 0;
            var cats = new[]
            {
                (BuiltInCategory.OST_PipeCurves, "Pipe"), (BuiltInCategory.OST_PipeFitting, "PipeFitting"),
                (BuiltInCategory.OST_PlumbingFixtures, "PlumbingFixture"), (BuiltInCategory.OST_Conduit, "Conduit"),
                (BuiltInCategory.OST_LightingFixtures, "LightingFixture"), (BuiltInCategory.OST_ElectricalFixtures, "ElectricalFixture"),
                (BuiltInCategory.OST_DuctCurves, "Duct")
            };
            w.BeginArray("Mep");
            foreach (var (cat, name) in cats)
            {
                foreach (var e in new FilteredElementCollector(doc).OfCategory(cat).WhereElementIsNotElementType())
                {
                    try
                    {
                        var lc = e.Location as LocationCurve;
                        w.BeginObject();
                        w.Prop("Id", e.UniqueId);
                        w.Prop("Category", name);
                        w.Prop("Name", e.Name ?? "");
                        w.Prop("Level", LevelName(doc, e.LevelId));
                        if (lc != null && lc.Curve != null)
                        {
                            XYZ a = lc.Curve.GetEndPoint(0), b = lc.Curve.GetEndPoint(1);
                            double d = ParamDouble(e, BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                            if (d <= 0) d = ParamDouble(e, BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM);
                            if (d <= 0)
                            {
                                var bb0 = e.get_BoundingBox(null);
                                if (bb0 != null) d = Math.Min(bb0.Max.Z - bb0.Min.Z, Math.Min(bb0.Max.X - bb0.Min.X, bb0.Max.Y - bb0.Min.Y));
                            }
                            w.Prop("X1", a.X * Ft); w.Prop("Y1", a.Y * Ft); w.Prop("Z1", a.Z * Ft);
                            w.Prop("X2", b.X * Ft); w.Prop("Y2", b.Y * Ft); w.Prop("Z2", b.Z * Ft);
                            w.Prop("Diameter", d * Ft);
                        }
                        else
                        {
                            var bb = e.get_BoundingBox(null);
                            if (bb != null)
                            {
                                w.Prop("X1", (bb.Min.X + bb.Max.X) / 2 * Ft); w.Prop("Y1", (bb.Min.Y + bb.Max.Y) / 2 * Ft); w.Prop("Z1", bb.Min.Z * Ft);
                                w.Prop("W", (bb.Max.X - bb.Min.X) * Ft); w.Prop("D", (bb.Max.Y - bb.Min.Y) * Ft); w.Prop("H", (bb.Max.Z - bb.Min.Z) * Ft);
                            }
                        }
                        w.EndObject();
                        n++;
                    }
                    catch (Exception ex) { _warnings.Add($"Điện nước {e.Id}: {ex.Message}"); }
                }
            }
            w.EndArray();
            return n;
        }
    }
}
