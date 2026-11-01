// =====================================================================
// KẾT CẤU VXT – ADD-IN XUẤT MÔ HÌNH TỪ REVIT
// ---------------------------------------------------------------------
// HƯỚNG DẪN (README):
//  1. Mở KetCauVXT.RevitExport.csproj bằng Visual Studio 2022 trên máy có cài Revit 2024.
//     (Revit 2025/2026: xem ghi chú trong .csproj – đổi TargetFramework sang net8.0-windows.)
//  2. Build Release → được KetCauVXT.RevitExport.dll.
//  3. Chép KetCauVXT.RevitExport.dll + KetCauVXT.RevitExport.addin vào
//     %AppData%\Autodesk\Revit\Addins\2024\
//  4. Mở Revit → tab Add-Ins → External Tools → "Xuất sang Kết cấu VXT".
//  5. Chọn nơi lưu file *.vxtrevit.json → trong Kết cấu VXT chọn Tệp → Nhập từ Revit (JSON).
//
// Dữ liệu xuất (đơn vị m, độ): Levels, Grids (đường thẳng), Structural Columns (b, h lấy từ tham số
// "b"/"h" của họ cột, nếu không có thì lấy theo hộp bao), Structural Framing (đường định vị, b/h),
// Floors (chiều dày theo loại sàn, đường bao lấy từ mặt trên).
// Toạ độ là toạ độ nội bộ (internal origin) của Revit; Kết cấu VXT tự dời gốc về giao trục đầu tiên.
// =====================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace KetCauVXT.RevitExport
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportCommand : IExternalCommand
    {
        /// <summary>1 ft (đơn vị nội bộ Revit) = 0.3048 m</summary>
        private const double Ft = 0.3048;

        private readonly List<string> _warnings = new List<string>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument?.Document;
            if (doc == null) { message = "Chưa mở dự án Revit."; return Result.Failed; }

            string path;
            using (var dlg = new System.Windows.Forms.SaveFileDialog())
            {
                dlg.Title = "Xuất mô hình sang Kết cấu VXT";
                dlg.Filter = "File trao đổi Kết cấu VXT (*.vxtrevit.json)|*.vxtrevit.json|JSON (*.json)|*.json";
                dlg.FileName = SafeFileName(doc.Title) + ".vxtrevit.json";
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return Result.Cancelled;
                path = dlg.FileName;
            }

            try
            {
                var w = new JsonWriter();
                w.BeginObject();
                w.Prop("ProjectName", doc.Title);
                w.Prop("SchemaVersion", "1.0");
                int nl = WriteLevels(doc, w);
                int ng = WriteGrids(doc, w);
                int nc = WriteColumns(doc, w);
                int nb = WriteBeams(doc, w);
                int nf = WriteFloors(doc, w);
                w.EndObject();
                File.WriteAllText(path, w.ToString(), new UTF8Encoding(false));

                var sb = new StringBuilder();
                sb.AppendLine("Đã xuất: " + path);
                sb.AppendLine($"Cao độ: {nl}, trục: {ng}, cột: {nc}, dầm: {nb}, sàn: {nf}");
                if (_warnings.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Cảnh báo:");
                    foreach (var s in _warnings.Take(25)) sb.AppendLine("- " + s);
                    if (_warnings.Count > 25) sb.AppendLine($"... và {_warnings.Count - 25} cảnh báo khác");
                }
                TaskDialog.Show("Kết cấu VXT", sb.ToString());
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = "Lỗi xuất dữ liệu: " + ex.Message;
                TaskDialog.Show("Kết cấu VXT", message + "\n\n" + ex.StackTrace);
                return Result.Failed;
            }
        }

        // ------------------------------------------------------------------
        private static string SafeFileName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        private static string LevelName(Document doc, ElementId id)
        {
            if (id == null || id == ElementId.InvalidElementId) return "";
            var lv = doc.GetElement(id) as Level;
            return lv != null ? lv.Name : "";
        }

        private static double ParamDouble(Element e, BuiltInParameter bip)
        {
            var p = e.get_Parameter(bip);
            return p != null && p.HasValue && p.StorageType == StorageType.Double ? p.AsDouble() : 0;
        }

        private static ElementId ParamId(Element e, BuiltInParameter bip)
        {
            var p = e.get_Parameter(bip);
            return p != null && p.StorageType == StorageType.ElementId ? p.AsElementId() : ElementId.InvalidElementId;
        }

        /// <summary>Tìm tham số kích thước (ft) theo danh sách tên – ở cấu kiện trước, sau đó ở loại (type).</summary>
        private static double FindSize(FamilyInstance fi, params string[] names)
        {
            foreach (var n in names)
            {
                var p = fi.LookupParameter(n);
                if (p != null && p.HasValue && p.StorageType == StorageType.Double && p.AsDouble() > 1e-6) return p.AsDouble();
            }
            var sym = fi.Symbol;
            if (sym != null)
                foreach (var n in names)
                {
                    var p = sym.LookupParameter(n);
                    if (p != null && p.HasValue && p.StorageType == StorageType.Double && p.AsDouble() > 1e-6) return p.AsDouble();
                }
            return 0;
        }

        private static string MaterialName(Document doc, FamilyInstance fi)
        {
            try
            {
                var id = fi.StructuralMaterialId;
                if (id != null && id != ElementId.InvalidElementId)
                {
                    var m = doc.GetElement(id) as Material;
                    if (m != null) return m.Name;
                }
            }
            catch (Exception) { /* một số họ không có vật liệu kết cấu */ }
            return "";
        }

        // ------------------------------------------------------------------
        private int WriteLevels(Document doc, JsonWriter w)
        {
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(l => l.ProjectElevation).ToList();
            w.BeginArray("Levels");
            foreach (var l in levels)
            {
                w.BeginObject();
                w.Prop("Name", l.Name);
                // ProjectElevation: cao độ so với gốc nội bộ – cùng hệ với toạ độ Z của cấu kiện
                w.Prop("Elevation", l.ProjectElevation * Ft);
                w.EndObject();
            }
            w.EndArray();
            return levels.Count;
        }

        private int WriteGrids(Document doc, JsonWriter w)
        {
            int n = 0;
            w.BeginArray("Grids");
            foreach (var g in new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>())
            {
                var line = g.Curve as Line;
                if (line == null) { _warnings.Add($"Trục {g.Name} là đường cong – bỏ qua."); continue; }
                XYZ a = line.GetEndPoint(0), b = line.GetEndPoint(1);
                w.BeginObject();
                w.Prop("Name", g.Name);
                w.Prop("X1", a.X * Ft); w.Prop("Y1", a.Y * Ft);
                w.Prop("X2", b.X * Ft); w.Prop("Y2", b.Y * Ft);
                w.EndObject();
                n++;
            }
            w.EndArray();
            return n;
        }

        private int WriteColumns(Document doc, JsonWriter w)
        {
            int n = 0;
            var cols = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType().OfType<FamilyInstance>()
                .Where(f => f.StructuralType == StructuralType.Column).ToList();
            w.BeginArray("Columns");
            foreach (var fi in cols)
            {
                XYZ pt;
                double rot = 0;
                var lp = fi.Location as LocationPoint;
                if (lp != null) { pt = lp.Point; rot = lp.Rotation * 180.0 / Math.PI; }
                else
                {
                    var lc = fi.Location as LocationCurve;
                    if (lc == null) { _warnings.Add($"Cột {fi.Id}: không có vị trí – bỏ qua."); continue; }
                    pt = lc.Curve.GetEndPoint(0);
                    _warnings.Add($"Cột {fi.Id} nghiêng – lấy toạ độ chân cột.");
                }
                string baseLv = LevelName(doc, ParamId(fi, BuiltInParameter.FAMILY_BASE_LEVEL_PARAM));
                string topLv = LevelName(doc, ParamId(fi, BuiltInParameter.FAMILY_TOP_LEVEL_PARAM));
                double baseOff = ParamDouble(fi, BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                double topOff = ParamDouble(fi, BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);

                double b = FindSize(fi, "b", "B", "Width", "Bề rộng");
                double h = FindSize(fi, "h", "H", "Depth", "Chiều cao");
                if (b <= 0 || h <= 0)
                {
                    var bb = fi.get_BoundingBox(null);
                    if (bb != null)
                    {
                        double dx = bb.Max.X - bb.Min.X, dy = bb.Max.Y - bb.Min.Y;
                        // hộp bao đã xoay theo trục tổng thể → coi như không xoay
                        if (b <= 0) b = dx;
                        if (h <= 0) h = dy;
                        rot = 0;
                        _warnings.Add($"Cột {fi.Id} ({fi.Symbol?.Name}): không có tham số b/h – lấy theo hộp bao.");
                    }
                }

                w.BeginObject();
                w.Prop("Id", fi.Id.ToString());
                w.Prop("X", pt.X * Ft); w.Prop("Y", pt.Y * Ft);
                w.Prop("BaseLevel", baseLv); w.Prop("TopLevel", topLv);
                w.Prop("BaseOffset", baseOff * Ft); w.Prop("TopOffset", topOff * Ft);
                w.Prop("B", b * Ft); w.Prop("H", h * Ft);
                w.Prop("RotationDeg", rot);
                w.Prop("Material", MaterialName(doc, fi));
                w.EndObject();
                n++;
            }
            w.EndArray();
            return n;
        }

        private int WriteBeams(Document doc, JsonWriter w)
        {
            int n = 0;
            var beams = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType().OfType<FamilyInstance>().ToList();
            w.BeginArray("Beams");
            foreach (var fi in beams)
            {
                if (fi.StructuralType != StructuralType.Beam && fi.StructuralType != StructuralType.Brace
                    && fi.StructuralType != StructuralType.Other) continue;
                var lc = fi.Location as LocationCurve;
                if (lc == null || lc.Curve == null) continue;
                var c = lc.Curve;
                if (!(c is Line)) _warnings.Add($"Dầm {fi.Id} là đường cong – thay bằng dây cung.");
                XYZ a = c.GetEndPoint(0), e = c.GetEndPoint(1);

                double b = FindSize(fi, "b", "B", "Width", "Bề rộng");
                double h = FindSize(fi, "h", "H", "Depth", "Chiều cao");
                if (b <= 0 || h <= 0)
                {
                    var bb = fi.get_BoundingBox(null);
                    if (bb != null)
                    {
                        if (h <= 0) h = bb.Max.Z - bb.Min.Z;
                        if (b <= 0)
                        {
                            // dầm song song X hoặc Y: bề rộng là kích thước nhỏ trên mặt bằng
                            double dx = bb.Max.X - bb.Min.X, dy = bb.Max.Y - bb.Min.Y;
                            b = Math.Min(dx, dy);
                        }
                        _warnings.Add($"Dầm {fi.Id} ({fi.Symbol?.Name}): không có tham số b/h – lấy theo hộp bao.");
                    }
                }
                string lv = LevelName(doc, ParamId(fi, BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM));
                if (lv == "" && fi.Host is Level) lv = ((Level)fi.Host).Name;

                w.BeginObject();
                w.Prop("Id", fi.Id.ToString());
                w.Prop("X1", a.X * Ft); w.Prop("Y1", a.Y * Ft); w.Prop("Z1", a.Z * Ft);
                w.Prop("X2", e.X * Ft); w.Prop("Y2", e.Y * Ft); w.Prop("Z2", e.Z * Ft);
                w.Prop("B", b * Ft); w.Prop("H", h * Ft);
                w.Prop("Level", lv);
                w.Prop("Material", MaterialName(doc, fi));
                w.EndObject();
                n++;
            }
            w.EndArray();
            return n;
        }

        private int WriteFloors(Document doc, JsonWriter w)
        {
            int n = 0;
            w.BeginArray("Floors");
            foreach (var fl in new FilteredElementCollector(doc).OfClass(typeof(Floor)).Cast<Floor>())
            {
                // thickness: từ cấu tạo loại sàn
                double t = 0;
                var ft = doc.GetElement(fl.GetTypeId()) as FloorType;
                if (ft != null)
                {
                    var cs = ft.GetCompoundStructure();
                    if (cs != null) t = cs.GetWidth();
                    if (t <= 0) t = ParamDouble(ft, BuiltInParameter.FLOOR_ATTR_DEFAULT_THICKNESS_PARAM);
                }
                if (t <= 0) t = ParamDouble(fl, BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);

                var loop = TopOutline(fl);
                if (loop == null || loop.Count < 3) { _warnings.Add($"Sàn {fl.Id}: không lấy được đường bao mặt trên."); continue; }

                w.BeginObject();
                w.Prop("Id", fl.Id.ToString());
                w.Prop("Level", LevelName(doc, fl.LevelId));
                w.Prop("Thickness", t * Ft);
                w.BeginArray("Outline");
                foreach (var p in loop) w.Pair(p.X * Ft, p.Y * Ft);
                w.EndArray();
                w.EndObject();
                n++;
            }
            w.EndArray();
            return n;
        }

        /// <summary>Đường bao ngoài mặt trên của sàn (đỉnh đa giác, ft). Lấy vòng có diện tích lớn nhất.</summary>
        private static List<XYZ> TopOutline(Floor fl)
        {
            List<XYZ> best = null;
            double bestArea = 0;
            IList<Reference> refs;
            try { refs = HostObjectUtils.GetTopFaces(fl); }
            catch (Exception) { return null; }
            foreach (var r in refs)
            {
                var face = fl.GetGeometryObjectFromReference(r) as Face;
                if (face == null) continue;
                foreach (CurveLoop cl in face.GetEdgesAsCurveLoops())
                {
                    var pts = new List<XYZ>();
                    foreach (Curve c in cl)
                    {
                        if (c is Line) pts.Add(c.GetEndPoint(0));
                        else
                        {
                            var tess = c.Tessellate();
                            for (int i = 0; i + 1 < tess.Count; i++) pts.Add(tess[i]);
                        }
                    }
                    double a = Math.Abs(Area(pts));
                    if (a > bestArea) { bestArea = a; best = pts; }
                }
            }
            return best;
        }

        private static double Area(List<XYZ> pts)
        {
            double a = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i]; var q = pts[(i + 1) % pts.Count];
                a += p.X * q.Y - q.X * p.Y;
            }
            return a / 2;
        }
    }
}
