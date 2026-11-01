using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using VXT.KetCau.Core.Geometry;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Revit.Util;

namespace VXT.KetCau.Revit.Model
{
    /// <summary>
    /// Đọc mô hình kết cấu đã dựng trong Revit (cột, dầm, sàn, móng kết cấu) → StructModel
    /// để xuất sang ETABS/SAP2000, thiết kế và tạo thép. Dùng khi kỹ sư tự dựng kết cấu.
    /// </summary>
    public class RevitStructReader
    {
        readonly Document doc;
        public List<string> Log { get; } = new List<string>();
        public RevitStructReader(Document d) { doc = d; }

        public StructModel Read(DesignSettings set)
        {
            var m = new StructModel { ProjectName = doc.ProjectInformation?.Name ?? doc.Title, Settings = set };
            var cols = Rv.Instances(doc, BuiltInCategory.OST_StructuralColumns).ToList();
            var beams = Rv.Instances(doc, BuiltInCategory.OST_StructuralFraming).Where(b => b.StructuralType == StructuralType.Beam).ToList();
            var floors = Rv.All<Floor>(doc).Where(f => f.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL)?.AsInteger() == 1).ToList();
            if (cols.Count == 0) throw new InvalidOperationException("Mô hình chưa có cột kết cấu (Structural Columns)");

            // ----- Tầng: lấy theo cao độ đỉnh dầm/sàn, gán tên Level gần nhất
            var levels = Rv.All<Level>(doc).OrderBy(l => l.Elevation).ToList();
            var zs = new List<double>();
            foreach (var c in cols) { var (z0, z1) = ColZ(c); zs.Add(z0); zs.Add(z1); }
            foreach (var b in beams) if (b.Location is LocationCurve lc) zs.Add(Rv.Mm(lc.Curve.GetEndPoint(0).Z));
            var storyZ = GeomUtil.Cluster(zs, 100);
            for (int i = 0; i < storyZ.Count; i++)
            {
                var near = levels.OrderBy(l => Math.Abs(Rv.Mm(l.Elevation) - storyZ[i])).FirstOrDefault();
                string name = near != null && Math.Abs(Rv.Mm(near.Elevation) - storyZ[i]) < 300 ? near.Name : (i == 0 ? "Móng" : $"Z{storyZ[i]:0}");
                if (m.Stories.Any(s => s.Name == name)) name += $" ({storyZ[i]:0})";
                m.Stories.Add(new Story { Name = name, Elevation = storyZ[i], Height = i == 0 ? 0 : storyZ[i] - storyZ[i - 1], IsRoof = i == storyZ.Count - 1 });
            }
            Func<double, Story> storyAt = z => m.Stories.OrderBy(s => Math.Abs(s.Elevation - z)).First();

            foreach (var g in Rv.All<Grid>(doc))
                if (g.Curve is Line ln)
                {
                    var s = new Seg2(Rv.P(ln.GetEndPoint(0)), Rv.P(ln.GetEndPoint(1)));
                    m.Grids.Add(new GridLine { Name = g.Name, Line = s, IsXDir = s.IsHorizontal(5) });
                }

            // ----- Cột (chia theo tầng nếu cột chạy nhiều tầng)
            foreach (var c in cols)
            {
                if (!(c.Location is LocationPoint lp)) continue;
                var (z0, z1) = ColZ(c);
                var (b, h) = SectionSize(c);
                var sec = m.GetOrAddSection("C", b, h);
                double rot = Math.Round(lp.Rotation * 180 / Math.PI % 90, 1);
                var inside = m.Stories.Where(s => s.Elevation > z0 + 50 && s.Elevation < z1 - 50).Select(s => s.Elevation).ToList();
                var cuts = new List<double> { z0 }; cuts.AddRange(inside); cuts.Add(z1);
                for (int i = 0; i + 1 < cuts.Count; i++)
                    m.Columns.Add(new ColumnElem { Location = Rv.P(lp.Point), BaseZ = cuts[i], TopZ = cuts[i + 1], BaseStory = storyAt(cuts[i]).Name, TopStory = storyAt(cuts[i + 1]).Name,
                                                   Section = sec.Name, RotationDeg = rot, RevitId = c.UniqueId });
            }

            // ----- Dầm (đỉnh dầm = đường định vị khi căn Top; cộng offset z nếu có)
            foreach (var bm in beams)
            {
                if (!(bm.Location is LocationCurve lc) || !(lc.Curve is Line ln)) continue;
                var (b, h) = SectionSize(bm);
                double zTop = Rv.Mm(Math.Max(ln.GetEndPoint(0).Z, ln.GetEndPoint(1).Z));
                var zj = bm.get_Parameter(BuiltInParameter.Z_JUSTIFICATION)?.AsInteger() ?? 0; // 0 Top, 1 Center, 2 Origin, 3 Bottom
                if (zj == 1) zTop += h / 2; else if (zj == 3) zTop += h;
                var sec = m.GetOrAddSection(h <= 300 ? "DP" : "D", b, h);
                m.Beams.Add(new BeamElem { Start = Rv.P(ln.GetEndPoint(0)), End = Rv.P(ln.GetEndPoint(1)), TopZ = zTop, Story = storyAt(zTop).Name, Section = sec.Name, RevitId = bm.UniqueId });
            }
            // nối điểm đầu dầm vào tim cột gần nhất (dầm vẽ tới mép cột)
            foreach (var bm in m.Beams)
            {
                var c0 = m.Columns.Where(c => c.Location.DistanceTo(bm.Start) < 400).OrderBy(c => c.Location.DistanceTo(bm.Start)).FirstOrDefault();
                var c1 = m.Columns.Where(c => c.Location.DistanceTo(bm.End) < 400).OrderBy(c => c.Location.DistanceTo(bm.End)).FirstOrDefault();
                if (c0 != null && Math.Abs((c0.Location - bm.Start).Cross((bm.End - bm.Start).Unit)) < 50) bm.Start = c0.Location;
                if (c1 != null && Math.Abs((c1.Location - bm.End).Cross((bm.End - bm.Start).Unit)) < 50) bm.End = c1.Location;
            }

            // tách dầm chạy qua nhiều cột thành từng nhịp (mỗi nhịp là một phần tử tính toán, cùng RevitId)
            foreach (var bm in m.Beams.ToList())
            {
                var seg = new Seg2(bm.Start, bm.End);
                var mids = m.Columns.Where(c => Math.Abs(c.TopZ - bm.TopZ) < 100 && seg.DistanceTo(c.Location) < 50)
                                    .Select(c => c.Location).Where(p => p.DistanceTo(bm.Start) > 300 && p.DistanceTo(bm.End) > 300)
                                    .Distinct().OrderBy(p => seg.Param(p)).ToList();
                if (mids.Count == 0) continue;
                var nodes = new List<P2> { bm.Start }; nodes.AddRange(mids); nodes.Add(bm.End);
                m.Beams.Remove(bm);
                for (int i = 0; i + 1 < nodes.Count; i++)
                    m.Beams.Add(new BeamElem { Start = nodes[i], End = nodes[i + 1], TopZ = bm.TopZ, Story = bm.Story, Section = bm.Section, RevitId = bm.RevitId });
            }

            // ----- Sàn: chia theo lưới dầm nếu sàn lớn; giữ đường bao Revit
            int sn = 0;
            foreach (var f in floors)
            {
                double t = Rv.Mm(f.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM)?.AsDouble() ?? 0);
                foreach (var r in HostObjectUtils.GetTopFaces(f))
                {
                    if (!(f.GetGeometryObjectFromReference(r) is PlanarFace face)) continue;
                    var loop = face.GetEdgesAsCurveLoops().OrderByDescending(cl => cl.GetExactLength()).First();
                    var pts = new List<P2>();
                    foreach (var cv in loop) { var tp = cv.Tessellate(); for (int i = 0; i + 1 < tp.Count; i++) pts.Add(Rv.P(tp[i])); }
                    double z = Rv.Mm(face.Origin.Z);
                    var st = storyAt(z);
                    foreach (var panel in SplitByBeams(pts, m.Beams.Where(b => b.Story == st.Name).ToList()))
                        m.Slabs.Add(new SlabElem { Boundary = panel, Story = st.Name, TopZ = z, Section = m.GetOrAddSlab(Math.Round(t / 10) * 10).Name,
                                                   LiveLoad = st.IsRoof ? 0.75 : 1.5, SuperDead = st.IsRoof ? 2.5 : 1.5, Usage = st.IsRoof ? "Mái" : "Phòng ở", Label = $"S{++sn}", RevitId = f.UniqueId });
                    break;
                }
            }

            // ----- Móng đơn
            int fn = 0;
            foreach (var ft in Rv.Instances(doc, BuiltInCategory.OST_StructuralFoundation))
            {
                if (!(ft.Location is LocationPoint lp)) continue;
                var col = m.Columns.Where(c => c.Location.DistanceTo(Rv.P(lp.Point)) < 300).OrderBy(c => c.BaseZ).FirstOrDefault();
                var bb = ft.get_BoundingBox(null);
                if (bb == null) continue;
                m.Footings.Add(new FootingElem { Center = Rv.P(lp.Point), ColumnId = col?.Id, Label = $"M{++fn}", RevitId = ft.UniqueId,
                    B = Rv.Mm(bb.Max.X - bb.Min.X), L = Rv.Mm(bb.Max.Y - bb.Min.Y), H = Rv.Mm(bb.Max.Z - bb.Min.Z), BottomZ = Rv.Mm(bb.Min.Z) });
            }
            Log.Add($"Đọc kết cấu Revit: {m.Stories.Count} tầng, {m.Columns.Count} đoạn cột, {m.Beams.Count} dầm, {m.Slabs.Count} ô sàn, {m.Footings.Count} móng");
            return m;
        }

        (double z0, double z1) ColZ(FamilyInstance c)
        {
            var bl = doc.GetElement(c.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId()) as Level;
            var tl = doc.GetElement(c.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId()) as Level;
            double bo = c.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)?.AsDouble() ?? 0;
            double to = c.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.AsDouble() ?? 0;
            if (bl != null && tl != null) return (Rv.Mm(bl.Elevation + bo), Rv.Mm(tl.Elevation + to));
            var bb = c.get_BoundingBox(null);
            return (Rv.Mm(bb.Min.Z), Rv.Mm(bb.Max.Z));
        }

        static (double b, double h) SectionSize(FamilyInstance fi)
        {
            double b = Rv.ParamMm(fi.Symbol, "b", "Width", "B", "Rộng"), h = Rv.ParamMm(fi.Symbol, "h", "Depth", "Height", "H", "Cao");
            if (b <= 0) b = Rv.ParamMm(fi, "b", "Width");
            if (h <= 0) h = Rv.ParamMm(fi, "h", "Depth");
            if (b <= 0 || h <= 0) throw new InvalidOperationException($"Không đọc được kích thước tiết diện của '{fi.Symbol.FamilyName}: {fi.Symbol.Name}' (cần tham số b, h)");
            return (Math.Round(b / 10) * 10, Math.Round(h / 10) * 10);
        }

        /// <summary>Chia sàn chữ nhật theo các dầm cắt ngang trực giao (để mỗi ô bản được cấu tạo thép riêng).</summary>
        static IEnumerable<List<P2>> SplitByBeams(List<P2> pts, List<BeamElem> beams)
        {
            double x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X), y0 = pts.Min(p => p.Y), y1 = pts.Max(p => p.Y);
            bool rect = pts.Count == 4 && Math.Abs(Math.Abs(GeomUtil.Area(pts)) - (x1 - x0) * (y1 - y0)) < 1e3;
            if (!rect) { yield return pts; yield break; }
            var X = new List<double> { x0 }; X.AddRange(beams.Where(b => Math.Abs(b.Start.X - b.End.X) < 5 && b.Start.X > x0 + 300 && b.Start.X < x1 - 300).Select(b => b.Start.X).Distinct().OrderBy(v => v)); X.Add(x1);
            var Y = new List<double> { y0 }; Y.AddRange(beams.Where(b => Math.Abs(b.Start.Y - b.End.Y) < 5 && b.Start.Y > y0 + 300 && b.Start.Y < y1 - 300).Select(b => b.Start.Y).Distinct().OrderBy(v => v)); Y.Add(y1);
            X = GeomUtil.Cluster(X, 50); Y = GeomUtil.Cluster(Y, 50);
            for (int i = 0; i + 1 < X.Count; i++)
                for (int j = 0; j + 1 < Y.Count; j++)
                    yield return new List<P2> { new P2(X[i], Y[j]), new P2(X[i + 1], Y[j]), new P2(X[i + 1], Y[j + 1]), new P2(X[i], Y[j + 1]) };
        }
    }
}
