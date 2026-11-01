using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Revit.Util;

namespace VXT.KetCau.Revit.Model
{
    /// <summary>
    /// Đọc hồ sơ kiến trúc từ tài liệu Revit hiện hành hoặc từ file kiến trúc liên kết (Revit Link)
    /// → ArchModel: trục, cao độ, tường, cột KT, sàn, phòng (công năng), cửa, cầu thang.
    /// </summary>
    public class RevitArchReader
    {
        readonly Document src;
        readonly Transform T;
        public List<string> Log { get; } = new List<string>();

        public RevitArchReader(Document source, Transform toHost = null)
        {
            src = source;
            T = toHost ?? Transform.Identity;
        }

        P2 Pt(XYZ p) => Rv.P(T.OfPoint(p));
        double Zmm(double ft) => Rv.Mm(ft + T.Origin.Z);

        public ArchModel Read()
        {
            var a = new ArchModel { ProjectName = src.ProjectInformation?.Name ?? src.Title, Source = "Revit: " + src.Title };

            // ----- Cao độ (bỏ cao độ trùng nhau < 100 mm)
            foreach (var l in Rv.All<Level>(src).OrderBy(l => l.Elevation))
            {
                double z = Zmm(l.Elevation);
                if (a.Levels.Any(x => Math.Abs(x.Elevation - z) < 100)) continue;
                a.Levels.Add(new ArchLevel { Name = l.Name, Elevation = z, IsRoof = l.Name.ToLowerInvariant().Contains("mái") || l.Name.ToLowerInvariant().Contains("roof") });
            }
            Func<ElementId, string> levelName = id => (src.GetElement(id) as Level)?.Name;
            Func<string, string> canon = n => a.Levels.FirstOrDefault(l => l.Name == n)?.Name
                                              ?? a.Levels.OrderBy(l => Math.Abs(l.Elevation - Zmm(Rv.All<Level>(src).First(x => x.Name == n).Elevation))).First().Name;

            // ----- Trục
            foreach (var g in Rv.All<Grid>(src))
                if (g.Curve is Line ln)
                    a.Grids.Add(new ArchGrid { Name = g.Name, Line = new Seg2(Pt(ln.GetEndPoint(0)), Pt(ln.GetEndPoint(1))) });

            // ----- Tường
            foreach (var w in Rv.All<Wall>(src))
            {
                if (!(w.Location is LocationCurve lc) || !(lc.Curve is Line ln)) continue;
                if (w.CurtainGrid != null) continue; // vách kính
                string bl = levelName(w.LevelId);
                if (bl == null) continue;
                double baseOff = Rv.Mm(w.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0);
                double h = Rv.Mm(w.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 0);
                if (h <= 0) { var bb = w.get_BoundingBox(null); if (bb != null) h = Rv.Mm(bb.Max.Z - bb.Min.Z); }
                var topId = w.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.AsElementId();
                var lvl = src.GetElement(w.LevelId) as Level;
                a.Walls.Add(new ArchWall
                {
                    Id = w.UniqueId,
                    Line = new Seg2(Pt(ln.GetEndPoint(0)), Pt(ln.GetEndPoint(1))),
                    Thickness = Math.Round(Rv.Mm(w.Width) / 10) * 10,
                    BaseLevel = canon(bl),
                    TopLevel = topId != null && topId != ElementId.InvalidElementId ? levelName(topId) : null,
                    BaseElevation = Zmm(lvl.Elevation) + baseOff,
                    Height = h,
                    IsExterior = w.WallType.Function == WallFunction.Exterior,
                    Material = w.WallType.Name,
                });
            }

            // ----- Cột kiến trúc và cột kết cấu có sẵn (định vị cột)
            foreach (var cat in new[] { BuiltInCategory.OST_Columns, BuiltInCategory.OST_StructuralColumns })
                foreach (var fi in Rv.Instances(src, cat))
                {
                    if (!(fi.Location is LocationPoint lp)) continue;
                    var sym = fi.Symbol;
                    double b = Rv.ParamMm(sym, "b", "Width", "Rộng", "B") ; double h = Rv.ParamMm(sym, "h", "Depth", "Cao", "H");
                    if (b <= 0 || h <= 0)
                    {
                        var bb = fi.get_BoundingBox(null);
                        if (bb == null) continue;
                        b = Rv.Mm(bb.Max.X - bb.Min.X); h = Rv.Mm(bb.Max.Y - bb.Min.Y);
                    }
                    double rot = lp.Rotation * 180 / Math.PI + Math.Atan2(T.BasisX.Y, T.BasisX.X) * 180 / Math.PI;
                    var bl = levelName(fi.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM)?.AsElementId() ?? fi.LevelId);
                    var tl = levelName(fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId);
                    if (bl == null) continue;
                    a.Columns.Add(new ArchColumn { Id = fi.UniqueId, Center = Pt(lp.Point), B = Math.Round(b / 10) * 10, H = Math.Round(h / 10) * 10, RotationDeg = Math.Round(rot % 90, 1), BaseLevel = canon(bl), TopLevel = tl });
                }

            // ----- Sàn: đường bao mặt trên
            foreach (var f in Rv.All<Floor>(src))
            {
                var lvl = src.GetElement(f.LevelId) as Level;
                if (lvl == null) continue;
                foreach (var r in HostObjectUtils.GetTopFaces(f))
                {
                    if (!(f.GetGeometryObjectFromReference(r) is PlanarFace face)) continue;
                    var loop = face.GetEdgesAsCurveLoops().OrderByDescending(cl => cl.GetExactLength()).FirstOrDefault();
                    if (loop == null) continue;
                    var pts = new List<P2>();
                    foreach (var c in loop) foreach (var p in c.Tessellate().Take(c.Tessellate().Count - 1)) pts.Add(Pt(p));
                    a.Floors.Add(new ArchFloor { Id = f.UniqueId, Boundary = pts, Level = canon(lvl.Name), Elevation = Zmm(lvl.Elevation),
                                                 Thickness = Rv.Mm(f.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM)?.AsDouble() ?? 0) });
                    break;
                }
            }

            // ----- Phòng (công năng → hoạt tải)
            var opt = new SpatialElementBoundaryOptions();
            foreach (var room in new FilteredElementCollector(src).OfCategory(BuiltInCategory.OST_Rooms).Cast<SpatialElement>().OfType<Room>())
            {
                if (room.Area <= 0 || room.Level == null) continue;
                var segs = room.GetBoundarySegments(opt);
                if (segs == null || segs.Count == 0) continue;
                var pts = segs[0].Select(s => Pt(s.GetCurve().GetEndPoint(0))).ToList();
                string name = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? room.Name;
                a.Rooms.Add(new ArchRoom { Name = name, Usage = name, Level = canon(room.Level.Name), Boundary = pts });
            }

            // ----- Cửa (giảm tải tường)
            foreach (var cat in new[] { BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows })
                foreach (var fi in Rv.Instances(src, cat))
                {
                    if (!(fi.Host is Wall host) || !(fi.Location is LocationPoint lp)) continue;
                    double wd = Rv.ParamMm(fi.Symbol, "Width", "Rộng", "b") ; if (wd <= 0) wd = Rv.Mm(fi.Symbol.get_Parameter(BuiltInParameter.FAMILY_WIDTH_PARAM)?.AsDouble() ?? 0);
                    double ht = Rv.ParamMm(fi.Symbol, "Height", "Cao", "h"); if (ht <= 0) ht = Rv.Mm(fi.Symbol.get_Parameter(BuiltInParameter.FAMILY_HEIGHT_PARAM)?.AsDouble() ?? 0);
                    a.Openings.Add(new ArchOpening { Id = fi.UniqueId, HostWallId = host.UniqueId, Location = Pt(lp.Point), Width = wd, Height = ht,
                        SillHeight = Rv.Mm(fi.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM)?.AsDouble() ?? 0),
                        Kind = cat == BuiltInCategory.OST_Doors ? "Cửa đi" : "Cửa sổ" });
                }

            // ----- Cầu thang (ô thang không đổ sàn)
            foreach (var e in new FilteredElementCollector(src).OfCategory(BuiltInCategory.OST_Stairs).WhereElementIsNotElementType())
            {
                var bb = e.get_BoundingBox(null);
                if (bb == null) continue;
                var p0 = Pt(bb.Min); var p1 = Pt(bb.Max);
                var bl = levelName(e.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId);
                var tl = levelName(e.get_Parameter(BuiltInParameter.STAIRS_TOP_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId);
                a.Stairs.Add(new ArchStair { Id = e.UniqueId, BaseLevel = bl, TopLevel = tl,
                    Boundary = { new P2(Math.Min(p0.X, p1.X), Math.Min(p0.Y, p1.Y)), new P2(Math.Max(p0.X, p1.X), Math.Min(p0.Y, p1.Y)),
                                 new P2(Math.Max(p0.X, p1.X), Math.Max(p0.Y, p1.Y)), new P2(Math.Min(p0.X, p1.X), Math.Max(p0.Y, p1.Y)) } });
            }

            Log.Add($"Đọc từ '{src.Title}': {a.Levels.Count} cao độ, {a.Grids.Count} trục, {a.Walls.Count} tường, {a.Columns.Count} cột, {a.Floors.Count} sàn, {a.Rooms.Count} phòng, {a.Openings.Count} cửa");
            return a;
        }
    }
}
