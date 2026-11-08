// =====================================================================
// VIỆT CONST – ADD-IN REVIT: ĐƯA HỌ (FAMILY) VÀO THƯ VIỆN 3D CỦA VIỆT CONST
// Chọn các đối tượng họ (đồ đạc, thiết bị vệ sinh, đèn, thiết bị điện, cửa…) → mỗi loại (Family : Type) ghi một mục
// %AppData%\VietConst\ThuVien3D\<Nhóm>\<Họ - Loại>.vxtlib.json: lưới tam giác lấy từ hình học của HỌ (toạ độ của họ,
// gốc chèn của họ, không phụ thuộc vị trí đặt), đơn vị m, trục Z lên; gốc thư viện = tâm đáy hộp bao.
// Không chọn gì: lấy mọi loại đang đặt trong dự án thuộc các nhóm đồ đạc / thiết bị.
// Chỉ ghi file trên máy, không kết nối mạng, không thu thập dữ liệu.
// =====================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KetCauVXT.RevitExport
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportLibraryCommand : IExternalCommand
    {
        private const double Ft = 0.3048;

        private static readonly BuiltInCategory[] Cats =
        {
            BuiltInCategory.OST_Furniture, BuiltInCategory.OST_FurnitureSystems, BuiltInCategory.OST_Casework,
            BuiltInCategory.OST_PlumbingFixtures, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_SpecialityEquipment,
            BuiltInCategory.OST_GenericModel, BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows, BuiltInCategory.OST_Planting
        };

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc?.Document;
            if (doc == null) { message = "Chưa mở dự án Revit."; return Result.Failed; }

            var picked = uidoc.Selection.GetElementIds().Select(id => doc.GetElement(id)).OfType<FamilyInstance>().ToList();
            if (picked.Count == 0)
                foreach (var c in Cats)
                    picked.AddRange(new FilteredElementCollector(doc).OfCategory(c).WhereElementIsNotElementType().OfType<FamilyInstance>());
            // một mẫu cho mỗi loại họ
            var bySymbol = new Dictionary<ElementId, FamilyInstance>();
            foreach (var fi in picked) if (fi.Symbol != null && !bySymbol.ContainsKey(fi.Symbol.Id)) bySymbol[fi.Symbol.Id] = fi;
            if (bySymbol.Count == 0) { TaskDialog.Show("Việt Const", "Không có đối tượng họ (family) nào để xuất."); return Result.Cancelled; }

            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string root = Path.Combine(appdata, "VietConst", "ThuVien3D");
            int n = 0;
            var skipped = new List<string>();
            foreach (var fi in bySymbol.Values)
            {
                string name = (fi.Symbol.FamilyName + " - " + fi.Symbol.Name).Trim();
                try
                {
                    var verts = new List<XYZ>(); var faces = new List<int[]>();
                    var opt = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false, IncludeNonVisibleObjects = false };
                    var ge = fi.Symbol.get_Geometry(opt) ?? fi.get_Geometry(opt);
                    Collect(ge, Transform.Identity, verts, faces, fi.Symbol.get_Geometry(opt) == null ? fi.GetTotalTransform().Inverse : null);
                    if (faces.Count == 0) { skipped.Add(name); continue; }
                    double x0 = verts.Min(v => v.X), x1 = verts.Max(v => v.X), y0 = verts.Min(v => v.Y), y1 = verts.Max(v => v.Y), z0 = verts.Min(v => v.Z), z1 = verts.Max(v => v.Z);
                    double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
                    string cat = Category(fi);
                    string disc = cat == "Thiết bị vệ sinh" || cat == "Đèn" || cat == "Điện" ? "DienNuoc" : "KienTruc";
                    var w = new JsonWriter();
                    w.BeginObject();
                    w.Prop("Id", Safe(cat) + "/" + Safe(name));
                    w.Prop("Name", name);
                    w.Prop("Category", cat);
                    w.Prop("Source", "Revit (add-in thư viện)");
                    w.BeginArray("Vertices");
                    foreach (var v in verts) w.Triple((v.X - cx) * Ft, (v.Y - cy) * Ft, (v.Z - z0) * Ft);
                    w.EndArray();
                    w.BeginArray("Faces");
                    foreach (var f in faces) w.IntTriple(f[0], f[1], f[2]);
                    w.EndArray();
                    w.Prop("Width", (x1 - x0) * Ft); w.Prop("Depth", (y1 - y0) * Ft); w.Prop("Height", (z1 - z0) * Ft);
                    w.Prop("Discipline", disc);
                    w.EndObject();
                    string dir = Path.Combine(root, Safe(cat));
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, Safe(name) + ".vxtlib.json"), w.ToString(), new UTF8Encoding(false));
                    n++;
                }
                catch (Exception ex) { skipped.Add(name + ": " + ex.Message); }
            }
            var sb = new StringBuilder();
            sb.AppendLine($"Đã đưa {n} loại họ vào thư viện Việt Const:");
            sb.AppendLine(root);
            sb.AppendLine("Trong Việt Const: BIM → Thư viện 3D… (mở lại để thấy mục mới).");
            if (skipped.Count > 0) { sb.AppendLine(); sb.AppendLine("Bỏ qua (không có hình học):"); foreach (var s in skipped.Take(15)) sb.AppendLine(" – " + s); }
            TaskDialog.Show("Việt Const – Thư viện", sb.ToString());
            return Result.Succeeded;
        }

        /// <summary>Gom tam giác từ hình học (khối đặc, lưới, thể hiện họ lồng) theo hệ toạ độ của họ.</summary>
        private static void Collect(GeometryElement ge, Transform tr, List<XYZ> verts, List<int[]> faces, Transform toLocal)
        {
            if (ge == null) return;
            foreach (var go in ge)
            {
                if (go is GeometryInstance gi)
                {
                    // hình học họ lồng: SymbolGeometry theo hệ của họ con + phép biến đổi đặt họ con
                    Collect(gi.GetSymbolGeometry(), tr.Multiply(gi.Transform), verts, faces, toLocal);
                }
                else if (go is Solid so && so.Faces.Size > 0 && so.Volume > 1e-9)
                {
                    foreach (Face f in so.Faces)
                    {
                        Mesh m;
                        try { m = f.Triangulate(); } catch { continue; }
                        if (m != null) AddMesh(m, tr, verts, faces, toLocal);
                    }
                }
                else if (go is Mesh mm) AddMesh(mm, tr, verts, faces, toLocal);
            }
        }

        private static void AddMesh(Mesh m, Transform tr, List<XYZ> verts, List<int[]> faces, Transform toLocal)
        {
            int b = verts.Count;
            for (int i = 0; i < m.Vertices.Count; i++)
            {
                var p = tr.OfPoint(m.Vertices[i]);
                if (toLocal != null) p = toLocal.OfPoint(p);
                verts.Add(p);
            }
            for (int i = 0; i < m.NumTriangles; i++)
            {
                var t = m.get_Triangle(i);
                faces.Add(new[] { b + (int)t.get_Index(0), b + (int)t.get_Index(1), b + (int)t.get_Index(2) });
            }
        }

        private static string Category(FamilyInstance fi)
        {
            var c = fi.Category;
            if (c == null) return "Khác";
var bic = c.BuiltInCategory;
            long id = (long)bic;
            if (id == (long)BuiltInCategory.OST_PlumbingFixtures) return "Thiết bị vệ sinh";
            if (id == (long)BuiltInCategory.OST_LightingFixtures) return "Đèn";
            if (id == (long)BuiltInCategory.OST_ElectricalFixtures || id == (long)BuiltInCategory.OST_ElectricalEquipment) return "Điện";
            if (id == (long)BuiltInCategory.OST_Doors || id == (long)BuiltInCategory.OST_Windows) return "Cửa";
            if (id == (long)BuiltInCategory.OST_Planting) return "Cây xanh";
            return "Nội thất";
        }

        private static string Safe(string s)
        {
            var bad = Path.GetInvalidFileNameChars();
            return new string((s ?? "").Select(ch => bad.Contains(ch) ? '_' : ch).ToArray()).Trim();
        }
    }
}
