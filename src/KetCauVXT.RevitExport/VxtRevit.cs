using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.DB.Structure;

namespace KetCauVXT.RevitExport
{
    /// <summary>Tiện ích Revit dùng chung cho các lệnh chuẩn hoá / dựng / đồng bộ với Việt Const.</summary>
    internal static class VxtRevit
    {
        public const double Ft = 0.3048;
        public static double ToFt(double m) => m / Ft;
        public static double ToM(double ft) => ft * Ft;

        // ------------------------------------------------------------ mã liên kết Việt Const (Extensible Storage)
        private static readonly Guid SchemaGuid = new Guid("5d3c9a71-2b4e-4f8a-9c61-7e2a0b5d4c13");

        private static Schema LinkSchema()
        {
            var s = Schema.Lookup(SchemaGuid);
            if (s != null) return s;
            var b = new SchemaBuilder(SchemaGuid);
            b.SetSchemaName("VietConstLink");
            b.SetReadAccessLevel(AccessLevel.Public);
            b.SetWriteAccessLevel(AccessLevel.Public);
            // VendorId phải hợp lệ theo quy tắc Revit (mã nhà phát triển); "VXT" bị Revit 2025 từ chối → thử lần lượt
            foreach (var v in new[] { "VXT", "VietConst", "VXTC", "com.vietconst" })
                if (SchemaBuilder.VendorIdIsValid(v)) { b.SetVendorId(v); break; }
            b.AddSimpleField("VxtId", typeof(string));
            return b.Finish();
        }

        /// <summary>Mã cấu kiện Việt Const đã gắn ("C12", "B30", "S4"); "" nếu không có.</summary>
        public static string GetVxtId(Element e)
        {
            try
            {
                var s = Schema.Lookup(SchemaGuid);
                if (s == null) return "";
                var ent = e.GetEntity(s);
                return ent != null && ent.IsValid() ? ent.Get<string>("VxtId") ?? "" : "";
            }
            catch { return ""; }
        }

        /// <summary>Gắn mã Việt Const (cần trong Transaction).</summary>
        public static void SetVxtId(Element e, string id)
        {
            // mã liên kết là phụ: lỗi gắn mã không được làm hỏng việc dựng phần tử
            try
            {
                var s = LinkSchema();
                var ent = new Entity(s);
                ent.Set("VxtId", id ?? "");
                e.SetEntity(ent);
            }
            catch (Exception ex) { if (LastTagError == null) LastTagError = ex.Message; }
        }

        /// <summary>Lỗi gắn mã đầu tiên (null = không lỗi) – để báo cáo một lần.</summary>
        public static string LastTagError;

        // ------------------------------------------------------------ tham số
        public static double TypeSize(Element sym, params string[] names)
        {
            foreach (var n in names)
            {
                var p = sym?.LookupParameter(n);
                if (p != null && p.HasValue && p.StorageType == StorageType.Double && p.AsDouble() > 1e-6) return ToM(p.AsDouble());
            }
            return 0;
        }

        public static bool SetSize(Element sym, double m, params string[] names)
        {
            foreach (var n in names)
            {
                var p = sym?.LookupParameter(n);
                if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double) { p.Set(ToFt(m)); return true; }
            }
            return false;
        }

        public static readonly string[] BNames = { "b", "B", "Width", "Bề rộng", "b (Chiều rộng)" };
        public static readonly string[] HNames = { "h", "H", "Depth", "Height", "Chiều cao", "h (Chiều cao)" };

        // ------------------------------------------------------------ vật liệu
        public static ElementId MaterialId(Document doc, string name, string kind)
        {
            if (string.IsNullOrWhiteSpace(name)) return ElementId.InvalidElementId;
            var m = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>().FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (m != null) return m.Id;
            var id = Autodesk.Revit.DB.Material.Create(doc, name);
            var mm = doc.GetElement(id) as Material;
            if (mm != null)
            {
                bool conc = (kind ?? "").IndexOf("Concrete", StringComparison.OrdinalIgnoreCase) >= 0 || name.StartsWith("Bê tông", StringComparison.OrdinalIgnoreCase);
                bool steel = (kind ?? "").IndexOf("Rebar", StringComparison.OrdinalIgnoreCase) >= 0 || name.StartsWith("Thép", StringComparison.OrdinalIgnoreCase);
                mm.MaterialClass = conc ? "Concrete" : steel ? "Metal" : "Masonry";
                mm.Color = conc ? new Color(192, 192, 192) : steel ? new Color(120, 120, 130) : name.IndexOf("Vữa", StringComparison.OrdinalIgnoreCase) >= 0 ? new Color(225, 220, 205) : new Color(200, 110, 80);
            }
            return id;
        }

        // ------------------------------------------------------------ họ cột / dầm BTCT chữ nhật
        /// <summary>Họ có loại mang tham số b, h (họ BTCT chữ nhật). Không có thì nạp từ thư viện Revit.</summary>
        public static Family RectFamily(Document doc, BuiltInCategory cat, List<string> log)
        {
            Family best = null;
            foreach (var sym in new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory(cat).Cast<FamilySymbol>())
            {
                if (TypeSizeRaw(sym, BNames) && TypeSizeRaw(sym, HNames))
                {
                    var f = sym.Family;
                    if (best == null || f.Name.IndexOf("Concrete", StringComparison.OrdinalIgnoreCase) >= 0 || f.Name.IndexOf("BTCT", StringComparison.OrdinalIgnoreCase) >= 0) best = f;
                    if (best != null && (best.Name.IndexOf("Concrete", StringComparison.OrdinalIgnoreCase) >= 0 || best.Name.IndexOf("BTCT", StringComparison.OrdinalIgnoreCase) >= 0)) break;
                }
            }
            if (best != null) return best;
            string[] files = cat == BuiltInCategory.OST_StructuralColumns
                ? new[] { "M_Concrete-Rectangular-Column.rfa", "Concrete-Rectangular-Column.rfa", "M_Concrete-Rectangular Column.rfa" }
                : new[] { "M_Concrete-Rectangular Beam.rfa", "Concrete-Rectangular Beam.rfa", "M_Concrete-Rectangular-Beam.rfa" };
            foreach (var path in LibraryFiles(doc.Application, files))
            {
                try
                {
                    if (doc.LoadFamily(path, out Family fam) && fam != null) { log.Add("Nạp họ " + Path.GetFileName(path)); return fam; }
                }
                catch (Exception ex) { log.Add("Không nạp được " + path + ": " + ex.Message); }
            }
            return null;
        }

        private static bool TypeSizeRaw(Element sym, string[] names)
        {
            foreach (var n in names)
            {
                var p = sym.LookupParameter(n);
                if (p != null && p.StorageType == StorageType.Double && !p.IsReadOnly) return true;
            }
            return false;
        }

        private static IEnumerable<string> LibraryFiles(Application app, string[] names)
        {
            var roots = new List<string>();
            try { foreach (var kv in app.GetLibraryPaths()) if (Directory.Exists(kv.Value)) roots.Add(kv.Value); } catch { }
            try
            {
                string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string lib = Path.Combine(pd, "Autodesk", "RVT " + app.VersionNumber, "Libraries");
                if (Directory.Exists(lib)) roots.Add(lib);
            }
            catch { }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in roots)
            {
                IEnumerable<string> found;
                try { found = Directory.EnumerateFiles(r, "*.rfa", SearchOption.AllDirectories).Where(f => names.Any(n => Path.GetFileName(f).Equals(n, StringComparison.OrdinalIgnoreCase))).ToList(); }
                catch { continue; }
                foreach (var f in found) if (seen.Add(f)) yield return f;
            }
        }

        /// <summary>Loại cột / dầm tên <paramref name="name"/> với b × h (m): có thì dùng (chỉnh lại kích thước nếu khác), chưa có thì nhân bản rồi đặt b, h.</summary>
        public static FamilySymbol RectType(Document doc, Family fam, string name, double b, double h)
        {
            FamilySymbol any = null;
            foreach (var id in fam.GetFamilySymbolIds())
            {
                var s = doc.GetElement(id) as FamilySymbol;
                if (s == null) continue;
                any = any ?? s;
                if (s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    if (Math.Abs(TypeSize(s, BNames) - b) > 1e-4) SetSize(s, b, BNames);
                    if (Math.Abs(TypeSize(s, HNames) - h) > 1e-4) SetSize(s, h, HNames);
                    return s;
                }
            }
            if (any == null) return null;
            var dup = any.Duplicate(name) as FamilySymbol;
            SetSize(dup, b, BNames);
            SetSize(dup, h, HNames);
            return dup;
        }

        // ------------------------------------------------------------ loại sàn / tường
        public static FloorType SlabType(Document doc, string name, double t, string material)
        {
            var all = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().ToList();
            var ft = all.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (ft == null)
            {
                var src = all.FirstOrDefault(x => x.GetCompoundStructure() != null && x.IsFoundationSlab == false) ?? all.FirstOrDefault();
                if (src == null) return null;
                ft = src.Duplicate(name) as FloorType;
            }
            var mid = MaterialId(doc, string.IsNullOrEmpty(material) ? "Bê tông B25" : material, "Concrete");
            // giữ cấu trúc của loại gốc (điều kiện EndCap / bọc lỗ hợp với loại sàn), chỉ thay lớp: Revit 2025 từ chối cấu trúc tạo mới
            var cs = ft.GetCompoundStructure();
            var layer = new CompoundStructureLayer(ToFt(t), MaterialFunctionAssignment.Structure, mid);
            if (cs != null)
            {
                cs.SetLayers(new List<CompoundStructureLayer> { layer });
                try { cs.SetNumberOfShellLayers(ShellLayerType.Exterior, 0); cs.SetNumberOfShellLayers(ShellLayerType.Interior, 0); } catch { }
            }
            else
            {
                cs = CompoundStructure.CreateSingleLayerCompoundStructure(MaterialFunctionAssignment.Structure, ToFt(t), mid);
                try { cs.EndCap = EndCapCondition.NoEndCap; cs.OpeningWrapping = OpeningWrappingCondition.None; } catch { }
            }
            cs.StructuralMaterialIndex = 0;
            ft.SetCompoundStructure(cs);
            return ft;
        }

        public static WallType EnsureWallType(Document doc, VxtWallType w)
        {
            var all = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().Where(x => x.Kind == WallKind.Basic).ToList();
            var wt = all.FirstOrDefault(x => x.Name.Equals(w.Name, StringComparison.OrdinalIgnoreCase));
            if (wt == null)
            {
                var src = all.FirstOrDefault();
                if (src == null) return null;
                wt = src.Duplicate(w.Name) as WallType;
            }
            var layers = new List<CompoundStructureLayer>();
            int core = -1;
            for (int i = 0; i < w.Layers.Count; i++)
            {
                var l = w.Layers[i];
                if (l.Thickness <= 1e-4) continue;
                bool isCore = l.Function.Equals("Structure", StringComparison.OrdinalIgnoreCase);
                var fn = isCore ? MaterialFunctionAssignment.Structure : (layers.Count == 0 ? MaterialFunctionAssignment.Finish1 : MaterialFunctionAssignment.Finish2);
                if (isCore && core < 0) core = layers.Count;
                layers.Add(new CompoundStructureLayer(ToFt(l.Thickness), fn, MaterialId(doc, l.Material, "Masonry")));
            }
            if (layers.Count == 0) return wt;
            var cs = CompoundStructure.CreateSimpleCompoundStructure(layers);
            if (core >= 0) cs.StructuralMaterialIndex = core;
            wt.SetCompoundStructure(cs);
            return wt;
        }

        // ------------------------------------------------------------ cao độ
        public static List<Level> Levels(Document doc) =>
            new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.ProjectElevation).ToList();

        /// <summary>Cao độ gần z (m, hệ nội bộ) nhất không cao hơn z + 5 mm; không có thì thấp nhất.</summary>
        public static Level LevelBelow(List<Level> lv, double zM)
        {
            double z = ToFt(zM);
            return lv.Where(l => l.ProjectElevation <= z + ToFt(0.005)).OrderByDescending(l => l.ProjectElevation).FirstOrDefault() ?? lv.FirstOrDefault();
        }

        public static Level LevelAt(List<Level> lv, double zM, double tolM = 0.02)
        {
            double z = ToFt(zM);
            return lv.FirstOrDefault(l => Math.Abs(l.ProjectElevation - z) <= ToFt(tolM));
        }

        public static string IdText(ElementId id) => id == null ? "" : id.ToString();
    }
}
