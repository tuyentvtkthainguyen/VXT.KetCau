using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using VXT.KetCau.Core.Model;
using VXT.KetCau.Revit.Util;

namespace VXT.KetCau.Revit.Model
{
    /// <summary>
    /// Dựng mô hình kết cấu trong Revit từ StructModel: cao độ, cột, dầm, sàn, móng đơn.
    /// Tự tải họ bê tông chữ nhật từ thư viện Revit nếu dự án chưa có, nhân bản loại theo tiết diện (C220x300, D220x400…).
    /// </summary>
    public class RevitModelBuilder
    {
        readonly Document doc;
        public List<string> Log { get; } = new List<string>();
        readonly Dictionary<string, Level> levelOf = new Dictionary<string, Level>();
        readonly Dictionary<string, double> offsetOf = new Dictionary<string, double>();
        ElementId concreteMat = ElementId.InvalidElementId;

        public RevitModelBuilder(Document d) { doc = d; }

        /// <summary>Gọi trong Transaction.</summary>
        public void Build(StructModel m, bool replaceExisting)
        {
            if (replaceExisting) DeleteGenerated();
            MapStories(m);
            concreteMat = GetConcreteMaterial(m.Settings.Concrete);

            var colFam = FindOrLoadSymbol(BuiltInCategory.OST_StructuralColumns, new[] { "Concrete-Rectangular", "Bê tông", "BTCT", "Concrete" }, "M_Concrete-Rectangular-Column.rfa");
            var beamFam = FindOrLoadSymbol(BuiltInCategory.OST_StructuralFraming, new[] { "Concrete-Rectangular", "Bê tông", "BTCT", "Concrete" }, "M_Concrete-Rectangular Beam.rfa");
            if (colFam == null || beamFam == null)
                throw new InvalidOperationException("Không tìm thấy họ cột/dầm bê tông chữ nhật. Hãy tải 'M_Concrete-Rectangular-Column' và 'M_Concrete-Rectangular Beam' từ thư viện Revit (Structural Columns/Framing → Concrete) rồi chạy lại.");

            int nc = 0, nb = 0, ns = 0, nf = 0;
            foreach (var c in m.Columns)
            {
                var sec = m.Section(c.Section);
                var sym = TypeFor(colFam, c.Section, sec.B, sec.H);
                var (bl, bo) = LevelAt(c.BaseStory, c.BaseZ);
                var (tl, to) = LevelAt(c.TopStory, c.TopZ);
                var pt = Rv.X(c.Location, Rv.Mm(bl.Elevation));
                var fi = doc.Create.NewFamilyInstance(pt, sym, bl, StructuralType.Column);
                fi.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)?.Set(Rv.Ft(bo));
                fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(tl.Id);
                fi.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(Rv.Ft(to));
                if (Math.Abs(c.RotationDeg) > 0.01)
                    ElementTransformUtils.RotateElement(doc, fi.Id, Line.CreateBound(pt, pt + XYZ.BasisZ), c.RotationDeg * Math.PI / 180);
                Rv.SetComment(fi, "VXT:" + c.Id);
                c.RevitId = fi.UniqueId; nc++;
            }
            foreach (var b in m.Beams)
            {
                var sec = m.Section(b.Section);
                var sym = TypeFor(beamFam, b.Section, sec.B, sec.H);
                var (lv, off) = LevelAt(b.Story, b.TopZ);
                double z = Rv.Mm(lv.Elevation);
                var line = Line.CreateBound(Rv.X(b.Start, z), Rv.X(b.End, z));
                var fi = doc.Create.NewFamilyInstance(line, sym, lv, StructuralType.Beam);
                fi.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION)?.Set(Rv.Ft(off));
                fi.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION)?.Set(Rv.Ft(off));
                Rv.SetComment(fi, "VXT:" + b.Id);
                b.RevitId = fi.UniqueId; nb++;
            }
            foreach (var s in m.Slabs)
            {
                var t = m.SlabSection(s.Section).Thickness;
                var ft = FloorTypeFor(t);
                var (lv, off) = LevelAt(s.Story, s.TopZ);
                double z = Rv.Mm(lv.Elevation);
                var loop = new CurveLoop();
                for (int i = 0; i < s.Boundary.Count; i++)
                    loop.Append(Line.CreateBound(Rv.X(s.Boundary[i], z), Rv.X(s.Boundary[(i + 1) % s.Boundary.Count], z)));
                var fl = Floor.Create(doc, new List<CurveLoop> { loop }, ft.Id, lv.Id, true, null, 0);
                fl.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(Rv.Ft(off));
                Rv.SetComment(fl, "VXT:" + s.Id);
                s.RevitId = fl.UniqueId; ns++;
            }
            var footFam = FindOrLoadSymbol(BuiltInCategory.OST_StructuralFoundation, new[] { "Footing-Rectangular", "Móng", "Footing" }, "M_Footing-Rectangular.rfa");
            if (footFam != null)
                foreach (var f in m.Footings)
                {
                    var sym = FootingType(footFam, f);
                    var (lv, off) = LevelAt(m.Stories[0].Name, f.BottomZ + f.H);
                    var fi = doc.Create.NewFamilyInstance(Rv.X(f.Center, Rv.Mm(lv.Elevation)), sym, lv, StructuralType.Footing);
                    var p = fi.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM) ?? fi.LookupParameter("Height Offset From Level") ?? fi.LookupParameter("Cao độ so với mức");
                    if (p != null && !p.IsReadOnly) p.Set(Rv.Ft(off));
                    Rv.SetComment(fi, "VXT:" + f.Id);
                    f.RevitId = fi.UniqueId; nf++;
                }
            else Log.Add("Không có họ móng đơn — bỏ qua dựng móng (vẫn cấu tạo thép móng trong bảng thống kê)");
            Log.Add($"Đã dựng trong Revit: {nc} cột, {nb} dầm, {ns} sàn, {nf} móng");
        }

        /// <summary>Xoá các phần tử do VXT tạo trước đó (nhận biết qua Comments = "VXT:…").</summary>
        public void DeleteGenerated()
        {
            var ids = new List<ElementId>();
            foreach (var cat in new[] { BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_Floors, BuiltInCategory.OST_StructuralFoundation })
                ids.AddRange(new FilteredElementCollector(doc).OfCategory(cat).WhereElementIsNotElementType()
                                 .Where(e => Rv.GetComment(e).StartsWith("VXT:")).Select(e => e.Id));
            if (ids.Count > 0) { doc.Delete(ids); Log.Add($"Đã xoá {ids.Count} phần tử VXT cũ"); }
        }

        // ------------------------------------------------------------ Cao độ
        void MapStories(StructModel m)
        {
            var levels = Rv.All<Level>(doc).ToList();
            foreach (var st in m.Stories)
            {
                var lv = levels.FirstOrDefault(l => l.Name == st.Name);
                if (lv == null && st.Name != m.Stories[0].Name)
                {
                    // tạo cao độ mới theo tên tầng kiến trúc (cao độ hoàn thiện = kết cấu + lớp hoàn thiện)
                    lv = Level.Create(doc, Rv.Ft(st.Elevation));
                    try { lv.Name = st.Name; } catch { lv.Name = st.Name + " (KC)"; }
                    levels.Add(lv);
                    Log.Add($"Tạo cao độ '{lv.Name}' tại {st.Elevation:0}");
                }
                if (lv == null) lv = levels.OrderBy(l => l.Elevation).First(); // tầng móng: tham chiếu cao độ thấp nhất
                levelOf[st.Name] = lv;
                offsetOf[st.Name] = st.Elevation - Rv.Mm(lv.Elevation);
            }
        }

        (Level lv, double offMm) LevelAt(string story, double zMm)
        {
            if (story != null && levelOf.TryGetValue(story, out var lv)) return (lv, zMm - Rv.Mm(lv.Elevation));
            var near = levelOf.Values.OrderBy(l => Math.Abs(Rv.Mm(l.Elevation) - zMm)).First();
            return (near, zMm - Rv.Mm(near.Elevation));
        }

        // ------------------------------------------------------------ Họ & loại
        FamilySymbol FindOrLoadSymbol(BuiltInCategory cat, string[] hints, string fileName)
        {
            var syms = new FilteredElementCollector(doc).OfCategory(cat).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().ToList();
            foreach (var h in hints)
            {
                var s = syms.FirstOrDefault(x => x.FamilyName.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0 && HasSizeParams(x, cat));
                if (s != null) return s;
            }
            // tìm trong thư viện Revit
            foreach (var root in doc.Application.GetLibraryPaths().Values.Where(Directory.Exists))
            {
                string path = null;
                try { path = Directory.GetFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault(); } catch { }
                if (path == null) continue;
                if (doc.LoadFamily(path, out Family fam))
                {
                    Log.Add($"Đã tải họ {Path.GetFileName(path)}");
                    var id = fam.GetFamilySymbolIds().FirstOrDefault();
                    return id != null ? doc.GetElement(id) as FamilySymbol : null;
                }
            }
            return null;
        }

        static bool HasSizeParams(FamilySymbol s, BuiltInCategory cat)
        {
            if (cat == BuiltInCategory.OST_StructuralFoundation) return s.LookupParameter("Width") != null || s.LookupParameter("Rộng") != null;
            return (s.LookupParameter("b") != null && s.LookupParameter("h") != null) || (s.LookupParameter("Width") != null && s.LookupParameter("Depth") != null);
        }

        readonly Dictionary<string, FamilySymbol> typeCache = new Dictionary<string, FamilySymbol>();

        FamilySymbol TypeFor(FamilySymbol baseSym, string name, double b, double h)
        {
            string key = baseSym.Family.Id + "|" + name;
            if (typeCache.TryGetValue(key, out var s)) return s;
            var existing = baseSym.Family.GetFamilySymbolIds().Select(id => doc.GetElement(id) as FamilySymbol).FirstOrDefault(x => x.Name == name);
            s = existing ?? (FamilySymbol)baseSym.Duplicate(name);
            if (!Rv.SetParamMm(s, b, "b", "Width")) Log.Add($"Loại {name}: không đặt được bề rộng");
            if (!Rv.SetParamMm(s, h, "h", "Depth")) Log.Add($"Loại {name}: không đặt được chiều cao");
            var mp = s.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            if (mp != null && !mp.IsReadOnly && concreteMat != ElementId.InvalidElementId) mp.Set(concreteMat);
            if (!s.IsActive) s.Activate();
            typeCache[key] = s;
            return s;
        }

        FamilySymbol FootingType(FamilySymbol baseSym, FootingElem f)
        {
            string name = $"{f.Label} {f.B:0}x{f.L:0}x{f.H:0}";
            var s = baseSym.Family.GetFamilySymbolIds().Select(id => doc.GetElement(id) as FamilySymbol).FirstOrDefault(x => x.Name == name) ?? (FamilySymbol)baseSym.Duplicate(name);
            Rv.SetParamMm(s, f.B, "Width", "Rộng");
            Rv.SetParamMm(s, f.L, "Length", "Dài");
            Rv.SetParamMm(s, f.H, "Foundation Thickness", "Thickness", "Chiều dày");
            if (!s.IsActive) s.Activate();
            return s;
        }

        FloorType FloorTypeFor(double tMm)
        {
            string name = $"VXT - Sàn BTCT {tMm:0}";
            var ft = Rv.All<FloorType>(doc).FirstOrDefault(x => x.Name == name);
            if (ft != null) return ft;
            var baseType = Rv.All<FloorType>(doc).FirstOrDefault(x => x.IsFoundationSlab == false && x.GetCompoundStructure() != null)
                           ?? throw new InvalidOperationException("Dự án không có loại sàn nào để nhân bản");
            ft = (FloorType)baseType.Duplicate(name);
            var cs = CompoundStructure.CreateSingleLayerCompoundStructure(MaterialFunctionAssignment.Structure, Rv.Ft(tMm), concreteMat);
            ft.SetCompoundStructure(cs);
            return ft;
        }

        ElementId GetConcreteMaterial(string grade)
        {
            string name = $"Bê tông {grade}";
            var mat = Rv.All<Material>(doc).FirstOrDefault(x => x.Name == name);
            if (mat != null) return mat.Id;
            var id = Material.Create(doc, name);
            var m = (Material)doc.GetElement(id);
            m.MaterialClass = "Concrete";
            m.Color = new Color(192, 192, 192);
            return id;
        }
    }
}
